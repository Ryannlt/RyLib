using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HoldfastGame;
using TMPro;
using UnityEngine;

namespace RyLib
{
    public static class PMenu
    {
        public const string ModsHeader = "Mods";

        private static readonly string[] Reserved = { "Players", "Rules", "Admin", "Maps", "Kill Log", "Artillery" };

        public static ModTab ModTab(string owner, string name, string icon = null, bool adminOnly = true, Func<bool> visible = null)
        {
            if (!Owners.Valid(owner, "PMenu.ModTab")) return null;

            if (string.IsNullOrEmpty(name))
            {
                Log.Warn(owner + ": PMenu.ModTab needs a name.");
                return null;
            }

            HeaderTab header = Header(RyLibPlugin.Guid, ModsHeader, StockIcon.Admin);
            if (header == null) return null;

            Page page = header.Claim(RyLibPlugin.Guid, ModsHeader, StockIcon.Admin, false, PageKind.Mods, null);
            return (page == null) ? null : page.ClaimModTab(owner, name, icon, adminOnly, visible);
        }

        public static HeaderTab Header(string owner, string name, string icon = null)
        {
            if (!Owners.Valid(owner, "PMenu.Header")) return null;

            if (string.IsNullOrEmpty(name))
            {
                Log.Warn(owner + ": PMenu.Header needs a name.");
                return null;
            }

            for (int i = 0; i < Reserved.Length; i++)
            {
                if (!Owners.Same(Reserved[i], name)) continue;

                Log.Warn(owner + ": '" + name + "' is one of the game's own tabs, so it cannot be claimed as a header.");
                return null;
            }

            HeaderTab existing = Menu.FindHeader(name);
            if (existing != null) return existing;

            HeaderTab header = new HeaderTab(owner, name, icon);
            Menu.Headers.Add(header);
            Menu.MarkDirty();
            Log.Info(owner + " created P menu header '" + name + "'.");
            return header;
        }
    }

    internal enum PageKind
    {
        Sections,
        Map,
        Mods
    }

    public enum MapShape
    {
        Ring,
        Flag,
        Dot,
        Outline
    }

    public struct MapMark
    {
        public string Key;
        public bool OnPlayer;
        public int PlayerId;
        public Vector3 Position;
        public MapShape Shape;
        public Color Colour;
        public float Size;
        public string Tooltip;
    }

    internal sealed class MapLayerSpec
    {
        public string Owner;
        public Action<List<MapMark>> Fill;
        public readonly List<MapMark> Marks = new List<MapMark>();
    }

    public sealed class HeaderTab
    {
        internal readonly List<Page> Pages = new List<Page>();
        internal readonly string Creator;
        internal readonly string IconName;

        internal GameObject Root;
        internal RectTransform Rect;
        internal UnityEngine.UI.Toggle Switch;
        internal TMP_Text Label;
        internal GameObject Strip;
        internal UnityEngine.UI.ToggleGroup StripGroup;
        internal Page Selected;

        internal HeaderTab(string creator, string name, string icon)
        {
            Creator = creator;
            Name = name;
            IconName = icon;
        }

        public string Name { get; private set; }

        public Page Page(string owner, string name, string icon = null, bool adminOnly = true, Func<bool> visible = null)
        {
            return Claim(owner, name, icon, adminOnly, PageKind.Sections, visible);
        }

        public Page MapPage(string owner, string name, string icon = null, Func<bool> visible = null)
        {
            return Claim(owner, name, icon, true, PageKind.Map, visible);
        }

        internal Page Claim(string owner, string name, string icon, bool adminOnly, PageKind kind, Func<bool> visible)
        {
            if (!Owners.Valid(owner, "HeaderTab.Page")) return null;

            if (string.IsNullOrEmpty(name))
            {
                Log.Warn(owner + ": a page needs a name.");
                return null;
            }

            for (int i = 0; i < Pages.Count; i++)
            {
                Page existing = Pages[i];
                if (!Owners.Same(existing.Name, name)) continue;

                if (existing.Kind == kind) return existing;

                Log.Warn(owner + " asked for a " + kind + " page named '" + name + "' under '" + Name + "', but " +
                         existing.Creator + " already made it a " + existing.Kind + " page. The request was refused.");
                return null;
            }

            Page page = new Page(this, owner, name, icon, adminOnly, kind, visible);
            Pages.Add(page);
            Menu.MarkDirty();
            Log.Info(owner + " created " + kind + " page '" + name + "' under '" + Name + "'.");
            return page;
        }
    }

    internal sealed class SectionSpec
    {
        public string Owner;
        public string Title;
        public Action<Section> Build;
        public bool Built;

        internal static bool Add(List<SectionSpec> sections, string where, string owner, string title, Action<Section> build)
        {
            if (string.IsNullOrEmpty(title) || build == null)
            {
                Log.Warn(owner + ": a section needs a title and a build callback.");
                return false;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (!Owners.Same(sections[i].Owner, owner) || !Owners.Same(sections[i].Title, title)) continue;

                Log.Warn(owner + ": section '" + title + "' is already on " + where + ", so the second one was ignored.");
                return false;
            }

            SectionSpec spec = new SectionSpec();
            spec.Owner = owner;
            spec.Title = title;
            spec.Build = build;
            sections.Add(spec);

            Menu.MarkDirty();
            return true;
        }
    }

    public sealed class ModTab
    {
        internal readonly Page Page;
        internal readonly string Creator;
        internal readonly string IconName;
        internal readonly bool AdminOnly;
        internal readonly Func<bool> Shown;
        internal readonly List<SectionSpec> Sections = new List<SectionSpec>();

        internal RectTransform Content;
        internal Control Button;

        internal ModTab(Page page, string creator, string name, string icon, bool adminOnly, Func<bool> shown)
        {
            Page = page;
            Creator = creator;
            Name = name;
            IconName = icon;
            AdminOnly = adminOnly;
            Shown = shown;
        }

        public string Name { get; private set; }

        internal bool Visible
        {
            get
            {
                if (AdminOnly && !ClientRemoteConsoleAccessManager.loggedOn) return false;
                return Owners.Ask(Creator, "mod tab '" + Name + "' visibility", Shown, true);
            }
        }

        public bool Section(string owner, string title, Action<Section> build)
        {
            if (!Owners.Valid(owner, "ModTab.Section")) return false;
            return SectionSpec.Add(Sections, "mod tab '" + Name + "'", owner, title, build);
        }
    }

    public sealed class Page
    {
        internal readonly HeaderTab Header;
        internal readonly string Creator;
        internal readonly string IconName;
        internal readonly bool AdminOnly;
        internal readonly PageKind Kind;
        internal readonly Func<bool> Shown;
        internal readonly List<SectionSpec> Sections = new List<SectionSpec>();
        internal readonly List<MapLayerSpec> MapLayers = new List<MapLayerSpec>();
        internal readonly List<ModTab> ModTabs = new List<ModTab>();
        internal MapView Map;
        internal ModTab CurrentModTab;
        internal RectTransform Nav;
        internal UnityEngine.UI.ScrollRect Scroller;

        internal bool HasType;
        internal PMenuTabType Type;

        internal GameObject Root;
        internal PageTab Tab;
        internal GameObject Button;
        internal UnityEngine.UI.Toggle Switch;
        internal RectTransform Content;
        internal TMP_Text TitleTemplate;

        internal Page(HeaderTab header, string creator, string name, string icon, bool adminOnly, PageKind kind, Func<bool> shown)
        {
            Header = header;
            Creator = creator;
            Name = name;
            IconName = icon;
            AdminOnly = adminOnly;
            Kind = kind;
            Shown = shown;
        }

        public string Name { get; private set; }

        internal bool Visible
        {
            get
            {
                if (Kind != PageKind.Mods)
                {
                    if (AdminOnly && !ClientRemoteConsoleAccessManager.loggedOn) return false;
                    return Owners.Ask(Creator, "page '" + Name + "' visibility", Shown, true);
                }

                for (int i = 0; i < ModTabs.Count; i++)
                {
                    if (ModTabs[i].Visible) return true;
                }

                return false;
            }
        }

        internal ModTab ClaimModTab(string owner, string name, string icon, bool adminOnly, Func<bool> visible)
        {
            for (int i = 0; i < ModTabs.Count; i++)
            {
                if (Owners.Same(ModTabs[i].Name, name)) return ModTabs[i];
            }

            ModTab tab = new ModTab(this, owner, name, icon, adminOnly, visible);
            ModTabs.Add(tab);
            Menu.MarkDirty();
            Log.Info(owner + " created mod tab '" + name + "'.");
            return tab;
        }

        public bool MapLayer(string owner, Action<List<MapMark>> fill)
        {
            if (!Owners.Valid(owner, "Page.MapLayer")) return false;

            if (Kind != PageKind.Map)
            {
                Log.Warn(owner + ": '" + Name + "' is a " + Kind + " page, which does not take map layers.");
                return false;
            }

            if (fill == null)
            {
                Log.Warn(owner + ": a map layer needs a fill callback.");
                return false;
            }

            for (int i = 0; i < MapLayers.Count; i++)
            {
                if (!Owners.Same(MapLayers[i].Owner, owner)) continue;

                Log.Warn(owner + " already has a layer on map page '" + Name + "', so the second one was ignored.");
                return false;
            }

            MapLayerSpec layer = new MapLayerSpec();
            layer.Owner = owner;
            layer.Fill = fill;
            MapLayers.Add(layer);

            Log.Info(owner + " added a layer to map page '" + Name + "'.");
            return true;
        }

        public bool Section(string owner, string title, Action<Section> build)
        {
            if (!Owners.Valid(owner, "Page.Section")) return false;

            if (Kind != PageKind.Sections)
            {
                Log.Warn(owner + ": '" + Name + "' is a " + Kind + " page, which does not take sections.");
                return false;
            }

            return SectionSpec.Add(Sections, "page '" + Name + "'", owner, title, build);
        }
    }

    public sealed class Section
    {
        private readonly string _owner;
        private readonly Transform _grid;

        internal Control First;

        internal Section(string owner, Transform grid)
        {
            _owner = owner;
            _grid = grid;
        }

        public void Toggle(string label, ConfigEntry<bool> entry, string icon = null)
        {
            if (string.IsNullOrEmpty(label) || entry == null) return;
            Add(Controls.Toggle(_owner, label, entry, icon));
        }

        public void Cycle<T>(string label, ConfigEntry<T> entry, Func<T, string> text = null, Func<T, bool> isOn = null,
            string icon = null) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(label) || entry == null) return;
            Add(Controls.Cycle(_owner, label, entry, text, isOn, icon));
        }

        public void Stepper(string label, ConfigEntry<float> entry, float step, string format = "0.00", string icon = null)
        {
            if (string.IsNullOrEmpty(label) || entry == null || step <= 0f) return;
            Add(Controls.Stepper(_owner, label, entry, step, format, icon));
        }

        public void Button(string label, Action onClick, string icon = null)
        {
            if (string.IsNullOrEmpty(label) || onClick == null) return;
            Add(Controls.Button(_owner, label, onClick, icon));
        }

        private void Add(ControlSpec spec)
        {
            Control control = Controls.Make(spec, _grid, Menu.ControlTemplate);
            if (First == null) First = control;
        }
    }
}
