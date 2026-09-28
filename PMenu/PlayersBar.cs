using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace RyLib
{
    public static class PlayersBar
    {
        private static readonly List<ControlSpec> Specs = new List<ControlSpec>();
        private static readonly List<Control> Built = new List<Control>();

        public static bool Toggle(string owner, string label, ConfigEntry<bool> entry, string icon = null)
        {
            if (!Owners.Valid(owner, "PlayersBar.Toggle") || !Usable(owner, label, entry)) return false;
            return Add(Controls.Toggle(owner, label, entry, icon));
        }

        public static bool Cycle<T>(string owner, string label, ConfigEntry<T> entry, Func<T, string> text = null,
            Func<T, bool> isOn = null, string icon = null) where T : struct, Enum
        {
            if (!Owners.Valid(owner, "PlayersBar.Cycle") || !Usable(owner, label, entry)) return false;
            return Add(Controls.Cycle(owner, label, entry, text, isOn, icon));
        }

        private static bool Usable(string owner, string label, object entry)
        {
            if (!string.IsNullOrEmpty(label) && entry != null) return true;

            Log.Warn(owner + ": a Players bar control needs a label and a config entry.");
            return false;
        }

        private static bool Add(ControlSpec spec)
        {
            for (int i = 0; i < Specs.Count; i++)
            {
                if (!Owners.Same(Specs[i].Owner, spec.Owner)) continue;
                if (!string.Equals(Specs[i].Key, spec.Key, StringComparison.OrdinalIgnoreCase)) continue;

                Log.Warn(spec.Owner + ": Players bar control '" + spec.Key + "' is already registered, so the second one was ignored.");
                return false;
            }

            Specs.Add(spec);
            Menu.MarkDirty();
            Log.Info(spec.Owner + " added Players bar control '" + spec.Key + "'.");
            return true;
        }

        internal static void Forget()
        {
            Built.Clear();
        }

        internal static void Build(GameObject template)
        {
            if (template == null) return;

            Transform bar = template.transform.parent;
            if (bar == null) return;

            while (Built.Count < Specs.Count) Built.Add(null);

            for (int i = 0; i < Specs.Count; i++)
            {
                if (Built[i] != null && Built[i].Root != null) continue;

                Control control = Controls.Make(Specs[i], bar, template);
                if (control == null) continue;

                control.Root.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1 + i);
                Built[i] = control;
            }
        }

        internal static void Refresh(GameObject template)
        {
            bool show = template != null && template.activeSelf;

            for (int i = 0; i < Built.Count; i++)
            {
                Control control = Built[i];
                if (control == null || control.Root == null) continue;
                if (control.Root.activeSelf != show) control.Root.SetActive(show);
            }
        }
    }
}
