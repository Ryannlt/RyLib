using System;
using System.Collections.Generic;
using System.IO;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace RyLib
{
    internal enum ClassKind
    {
        Line,
        Officer,
        Sergeant,
        Surgeon,
        Grenadier,
        Guard,
        FlagBearer,
        Musician,
        Cannoneer,
        Rocketeer,
        Sapper,
        Carpenter,
        Hussar,
        Dragoon,
        LightInfantry,
        Rifleman,
        Captain,
        Midshipman,
        Sailor,
        Marine,
        CoastGuard,
        Customs,
        FrontlinesOfficer,
        FrontlinesRifleman,
        FrontlinesArtilleryman,
        FrontlinesTrenchRaider,
        FrontlinesEngineer,
        FrontlinesMedic,
        Other
    }

    internal static class ClassIcons
    {
        private const string OfficerMarker = "nations_player_officer_ally";
        private const string FrontlinesOfficerMarker = "player_officer_ally";
        private const string EngineerMarker = "player_engineer_ally";
        private const string GasMask = "#gas-mask.png";
        private const string SailorHat = "#sailor-hat.png";
        private const string TextGlyph = "tmp:";

        private sealed class Entry
        {
            public readonly string Label;
            public readonly string Source;
            public readonly ClassShape Fallback;
            public Sprite Icon;
            public bool Tried;

            public Entry(string label, string source, ClassShape fallback)
            {
                Label = label;
                Source = source;
                Fallback = fallback;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("Line Infantry", TextGlyph + "ArmyLineInfantry", ClassShape.Circle),
            new Entry("Officer", OfficerMarker, ClassShape.Star),
            new Entry("Sergeant", "hui-ability-nco", ClassShape.Star),
            new Entry("Surgeon", "hui-ability-medicine", ClassShape.Cross),
            new Entry("Grenadier", TextGlyph + "Grenadier", ClassShape.Target),
            new Entry("Guard", TextGlyph + "Guard", ClassShape.Target),
            new Entry("Flag Bearer", "hui-ability-standard-bearer", ClassShape.Star),
            new Entry("Musician", "ability_bar_musician_white", ClassShape.Pentagon),
            new Entry("Cannoneer", "hui-ability-artillery-operator", ClassShape.Square),
            new Entry("Rocketeer", "hui-ability-rocket-operator", ClassShape.Square),
            new Entry("Sapper", "hui-ability-engineering", ClassShape.Square),
            new Entry("Carpenter", "hui-trait-axe-specialisation", ClassShape.Square),
            new Entry("Hussar", TextGlyph + "Hussar", ClassShape.Diamond),
            new Entry("Dragoon", TextGlyph + "Dragoon", ClassShape.Diamond),
            new Entry("Light Infantry", "hui-trait-lightfooted", ClassShape.Triangle),
            new Entry("Rifleman", TextGlyph + "Rifleman", ClassShape.Triangle),
            new Entry("Captain", "ShipWheel", ClassShape.Star),
            new Entry("Midshipman", "hui-midshipman-orders", ClassShape.Star),
            new Entry("Sailor", SailorHat, ClassShape.Circle),
            new Entry("Marine", TextGlyph + "NavalMarine", ClassShape.Triangle),
            new Entry("Coast Guard", TextGlyph + "CoastGuard", ClassShape.Square),
            new Entry("Customs", "hui-trait-able-swimmer", ClassShape.Circle),
            new Entry("Officer", FrontlinesOfficerMarker, ClassShape.Star),
            new Entry("Rifleman", "hui-radial-frontlines-rifle", ClassShape.Circle),
            new Entry("Artilleryman", "frontlines-artillery", ClassShape.Square),
            new Entry("Trench Raider", GasMask, ClassShape.Target),
            new Entry("Engineer", EngineerMarker, ClassShape.Square),
            new Entry("Medic", "hui-ability-medicine", ClassShape.Cross),
            new Entry("Other", null, ClassShape.Circle)
        };

        private static readonly Dictionary<string, Entry> Wanted = new Dictionary<string, Entry>(StringComparer.Ordinal);

        internal static int KindCount
        {
            get { return Entries.Length; }
        }

        internal static ClassKind Of(RoundPlayer player)
        {
            PlayerClass type = player.PlayerStartData.ClassType;
            bool frontlines = Frontlines();
            if (Naval(type)) return OfClass(type, frontlines);

            PlayerClassTraitsData traits = (player.PlayerBase == null) ? null : player.PlayerBase.PlayerClassTraitsData;
            if (traits != null)
            {
                if (traits.distinguishableOfficer) return frontlines ? ClassKind.FrontlinesOfficer : ClassKind.Officer;
                if (traits.distinguishableSurgeon) return frontlines ? ClassKind.FrontlinesMedic : ClassKind.Surgeon;
                if (frontlines && traits.distinguishableEngineer) return ClassKind.FrontlinesEngineer;
                if (!frontlines && traits.distinguishableSergeant) return ClassKind.Sergeant;
            }

            return OfClass(type, frontlines);
        }

        private static bool Naval(PlayerClass type)
        {
            switch (type)
            {
                case PlayerClass.NavalCaptain:
                case PlayerClass.NavalSailor2:
                case PlayerClass.NavalSailor:
                case PlayerClass.NavalMarine:
                case PlayerClass.CoastGuard:
                case PlayerClass.Customs:
                    return true;
                default:
                    return false;
            }
        }

        internal static ClassKind OfClass(PlayerClass type, bool frontlines)
        {
            switch (type)
            {
                case PlayerClass.NavalCaptain: return ClassKind.Captain;
                case PlayerClass.NavalSailor2: return ClassKind.Midshipman;
                case PlayerClass.NavalSailor: return ClassKind.Sailor;
                case PlayerClass.NavalMarine: return ClassKind.Marine;
                case PlayerClass.CoastGuard: return ClassKind.CoastGuard;
                case PlayerClass.Customs: return ClassKind.Customs;
            }

            if (frontlines)
            {
                switch (type)
                {
                    case PlayerClass.ArmyInfantryOfficer: return ClassKind.FrontlinesOfficer;
                    case PlayerClass.ArmyLineInfantry: return ClassKind.FrontlinesRifleman;
                    case PlayerClass.Cannoneer: return ClassKind.FrontlinesArtilleryman;
                    case PlayerClass.Carpenter: return ClassKind.FrontlinesTrenchRaider;
                    case PlayerClass.Sapper: return ClassKind.FrontlinesEngineer;
                    case PlayerClass.Surgeon: return ClassKind.FrontlinesMedic;
                }
            }

            switch (type)
            {
                case PlayerClass.ArmyLineInfantry: return ClassKind.Line;
                case PlayerClass.ArmyInfantryOfficer: return ClassKind.Officer;
                case PlayerClass.Sergeant: return ClassKind.Sergeant;
                case PlayerClass.Surgeon: return ClassKind.Surgeon;
                case PlayerClass.Grenadier: return ClassKind.Grenadier;
                case PlayerClass.Guard: return ClassKind.Guard;
                case PlayerClass.FlagBearer: return ClassKind.FlagBearer;
                case PlayerClass.Musician: return ClassKind.Musician;
                case PlayerClass.Cannoneer: return ClassKind.Cannoneer;
                case PlayerClass.Deprecated_Rocketeer: return ClassKind.Rocketeer;
                case PlayerClass.Sapper: return ClassKind.Sapper;
                case PlayerClass.Carpenter: return ClassKind.Carpenter;
                case PlayerClass.Hussar: return ClassKind.Hussar;
                case PlayerClass.Dragoon: return ClassKind.Dragoon;
                case PlayerClass.LightInfantry: return ClassKind.LightInfantry;
                case PlayerClass.Rifleman: return ClassKind.Rifleman;
                default: return ClassKind.Other;
            }
        }

        internal static string Label(ClassKind kind)
        {
            return Entries[(int)kind].Label;
        }

        internal static Sprite Icon(ClassKind kind)
        {
            Entry entry = Entries[(int)kind];
            if (!entry.Tried) Resolve();
            return (entry.Icon != null) ? entry.Icon : Glyphs.Class(entry.Fallback);
        }

        internal static void Retry()
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Icon == null) Entries[i].Tried = false;
            }
        }

        internal static bool Frontlines()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null || client.clientGameModeManager == null) return false;

            RoundGameDetails details = client.clientGameModeManager.CurrentRoundGameDetails;
            return details != null && details.FrontlinesMode;
        }

        private static void Resolve()
        {
            Wanted.Clear();

            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];
                if (entry.Tried) continue;

                entry.Tried = true;
                if (entry.Source == null) continue;

                if (entry.Source[0] == '#')
                {
                    entry.Icon = Embedded(entry.Source.Substring(1), entry);
                    continue;
                }

                if (entry.Source.StartsWith(TextGlyph, StringComparison.Ordinal))
                {
                    entry.Icon = TextSprite(entry.Source.Substring(TextGlyph.Length), entry);
                    continue;
                }

                entry.Icon = Convert(MinimapSprite(entry.Source), entry);
                if (entry.Icon != null) continue;

                Entry shared;
                if (Wanted.TryGetValue(entry.Source, out shared))
                {
                    entry.Tried = false;
                    continue;
                }

                Wanted[entry.Source] = entry;
            }

            if (Wanted.Count > 0)
            {
                Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < sprites.Length && Wanted.Count > 0; i++)
                {
                    Sprite sprite = sprites[i];
                    Entry entry;
                    if (sprite == null || !Wanted.TryGetValue(sprite.name, out entry)) continue;

                    entry.Icon = Convert(sprite, entry);
                    if (entry.Icon != null) Wanted.Remove(sprite.name);
                }
            }

            if (Wanted.Count > 0)
            {
                Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
                for (int i = 0; i < textures.Length && Wanted.Count > 0; i++)
                {
                    Texture2D texture = textures[i];
                    Entry entry;
                    if (texture == null || !Wanted.TryGetValue(texture.name, out entry)) continue;

                    entry.Icon = Convert(texture, entry);
                    if (entry.Icon != null) Wanted.Remove(texture.name);
                }
            }

            foreach (KeyValuePair<string, Entry> pair in Wanted)
            {
                Log.Warn("map icon '" + pair.Key + "' for " + pair.Value.Label + " is not loaded, so it is drawn as a " +
                         pair.Value.Fallback.ToString().ToLowerInvariant() + " until the next round.");
            }

            for (int i = 0; i < Entries.Length; i++)
            {
                if (!Entries[i].Tried) Share(Entries[i]);
            }
        }

        private static void Share(Entry entry)
        {
            entry.Tried = true;
            for (int i = 0; i < Entries.Length; i++)
            {
                Entry other = Entries[i];
                if (other == entry || other.Icon == null || other.Source != entry.Source) continue;

                entry.Icon = other.Icon;
                return;
            }
        }

        private static Sprite MinimapSprite(string source)
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null || client.uiMinimapPanelTracking == null) return null;

            MinimapProfile profile = client.uiMinimapPanelTracking.minimapProfile;
            if (profile == null) return null;

            MinimapSpriteVariation variation;
            switch (source)
            {
                case OfficerMarker: variation = profile.officer; break;
                case FrontlinesOfficerMarker: variation = profile.frontlinesOfficer; break;
                case EngineerMarker: variation = profile.engineer; break;
                default: return null;
            }

            return (variation == null) ? null : variation.sprite;
        }

        private static Sprite TextSprite(string name, Entry entry)
        {
            List<TMP_SpriteAsset> assets = new List<TMP_SpriteAsset>();
            Collect(TMP_Settings.defaultSpriteAsset, assets);

            TMP_SpriteAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>();
            for (int i = 0; i < loaded.Length; i++) Collect(loaded[i], assets);

            for (int i = 0; i < assets.Count; i++)
            {
                TMP_SpriteAsset asset = assets[i];
                List<TMP_SpriteCharacter> characters = asset.spriteCharacterTable;
                if (characters == null) continue;

                for (int j = 0; j < characters.Count; j++)
                {
                    TMP_SpriteCharacter character = characters[j];
                    if (character == null || character.name != name || character.glyph == null) continue;

                    try
                    {
                        TMP_SpriteGlyph glyph = character.glyph as TMP_SpriteGlyph;
                        Sprite icon = (glyph != null && glyph.sprite != null)
                            ? Tintable.From(glyph.sprite)
                            : Tintable.From(asset.spriteSheet, Region(character.glyph.glyphRect), name);

                        if (icon != null) Log.Info("map icon for " + entry.Label + ": " + name + " from " + asset.name + ".");
                        return icon;
                    }
                    catch (Exception error)
                    {
                        Log.Warn("map icon '" + name + "' could not be converted: " + error.Message);
                        return null;
                    }
                }
            }

            Log.Warn("map icon '" + name + "' for " + entry.Label + " is not in any loaded text sprite asset, so it is drawn as a " +
                     entry.Fallback.ToString().ToLowerInvariant() + " until the next round.");
            return null;
        }

        private static void Collect(TMP_SpriteAsset asset, List<TMP_SpriteAsset> assets)
        {
            if (asset == null || assets.Contains(asset)) return;

            assets.Add(asset);
            if (asset.fallbackSpriteAssets == null) return;

            for (int i = 0; i < asset.fallbackSpriteAssets.Count; i++) Collect(asset.fallbackSpriteAssets[i], assets);
        }

        private static Rect Region(GlyphRect rect)
        {
            return new Rect(rect.x, rect.y, rect.width, rect.height);
        }

        private static Sprite Embedded(string file, Entry entry)
        {
            Texture2D texture = null;

            try
            {
                using (Stream stream = typeof(ClassIcons).Assembly.GetManifestResourceStream("RyLib.Icons." + file))
                {
                    if (stream == null)
                    {
                        Log.Warn("map icon '" + file + "' for " + entry.Label + " is missing from RyLib.dll.");
                        return null;
                    }

                    byte[] bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int count = stream.Read(bytes, read, bytes.Length - read);
                        if (count <= 0) break;
                        read += count;
                    }

                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, bytes, false))
                    {
                        Log.Warn("map icon '" + file + "' for " + entry.Label + " could not be decoded.");
                        return null;
                    }
                }

                return Tintable.From(texture, new Rect(0f, 0f, texture.width, texture.height), file);
            }
            catch (Exception error)
            {
                Log.Warn("map icon '" + file + "' could not be converted: " + error.Message);
                return null;
            }
            finally
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }

        private static Sprite Convert(Sprite sprite, Entry entry)
        {
            if (sprite == null) return null;

            try
            {
                Sprite icon = Tintable.From(sprite);
                if (icon != null) Log.Info("map icon for " + entry.Label + ": " + sprite.name + ".");
                return icon;
            }
            catch (Exception error)
            {
                Log.Warn("map icon '" + sprite.name + "' could not be converted: " + error.Message);
                return null;
            }
        }

        private static Sprite Convert(Texture2D texture, Entry entry)
        {
            try
            {
                Sprite icon = Tintable.From(texture, new Rect(0f, 0f, texture.width, texture.height), texture.name);
                if (icon != null) Log.Info("map icon for " + entry.Label + ": " + texture.name + " (texture).");
                return icon;
            }
            catch (Exception error)
            {
                Log.Warn("map icon '" + texture.name + "' could not be converted: " + error.Message);
                return null;
            }
        }
    }
}
