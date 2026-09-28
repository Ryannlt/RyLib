using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace RyLib
{
    public static class TextIcons
    {
        private const int GlyphSize = 64;
        private const int GlyphPad = 2;
        private const float Rise = 0.78f;
        private const float RetrySeconds = 10f;
        private const string Version = "1.1.0";

        private static readonly Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> Failed = new Dictionary<string, float>(StringComparer.Ordinal);

        public static string Tag(string gameSprite)
        {
            if (string.IsNullOrEmpty(gameSprite)) return null;

            string tag;
            if (Tags.TryGetValue(gameSprite, out tag) && Registered(gameSprite)) return tag;

            float retry;
            if (Failed.TryGetValue(gameSprite, out retry) && Time.unscaledTime < retry) return null;

            try
            {
                tag = Build(gameSprite);
            }
            catch (Exception error)
            {
                Log.Warn("text icon '" + gameSprite + "' could not be built: " + error.Message);
                tag = null;
            }

            if (tag == null)
            {
                Failed[gameSprite] = Time.unscaledTime + RetrySeconds;
                return null;
            }

            Tags[gameSprite] = tag;
            Failed.Remove(gameSprite);
            return tag;
        }

        private static string Key(string gameSprite)
        {
            return "RyLib_" + gameSprite;
        }

        private static bool Registered(string gameSprite)
        {
            return RegisteredName(Key(gameSprite));
        }

        internal static bool RegisteredName(string key)
        {
            TMP_SpriteAsset root = TMP_Settings.defaultSpriteAsset;
            if (root == null || root.fallbackSpriteAssets == null) return false;

            for (int i = 0; i < root.fallbackSpriteAssets.Count; i++)
            {
                TMP_SpriteAsset asset = root.fallbackSpriteAssets[i];
                if (asset != null && asset.name == key) return true;
            }

            return false;
        }

        private static string Build(string gameSprite)
        {
            Texture source;
            Rect region;
            if (!Find(gameSprite, out source, out region)) return null;

            Texture2D sheet = Tintable.Plain(source, region, GlyphSize, GlyphPad);
            if (sheet == null) return null;

            string key = Key(gameSprite);
            GlyphMetrics metrics = new GlyphMetrics(GlyphSize, GlyphSize, 0f, GlyphSize * Rise, GlyphSize);
            return Register(key, sheet, metrics, 1f, null) ? "<sprite name=\"" + key + "\" tint=1>" : null;
        }

        internal static bool Register(string key, Texture2D sheet, GlyphMetrics metrics, float scale, TMP_SpriteAsset style)
        {
            TMP_SpriteAsset root = TMP_Settings.defaultSpriteAsset;
            Material template = (style != null && style.material != null) ? style.material : (root == null ? null : root.material);
            if (root == null || template == null) return false;

            sheet.name = key;

            Material material = new Material(template);
            material.name = key;
            material.mainTexture = sheet;
            material.hideFlags = HideFlags.HideAndDontSave;

            TMP_SpriteAsset asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = key;
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.spriteSheet = sheet;
            asset.material = material;
            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(key);
            asset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(material.name);
            AccessTools.Property(typeof(TMP_SpriteAsset), "version").SetValue(asset, Version, null);
            if (style != null) AccessTools.Property(typeof(TMP_SpriteAsset), "faceInfo").SetValue(asset, style.faceInfo, null);

            TMP_SpriteGlyph glyph = new TMP_SpriteGlyph(0, metrics, new GlyphRect(0, 0, sheet.width, sheet.height), scale, 0);
            TMP_SpriteCharacter character = new TMP_SpriteCharacter(0xE000, asset, glyph);
            character.name = key;

            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(character);
            asset.UpdateLookupTables();

            if (root.fallbackSpriteAssets == null) root.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
            root.fallbackSpriteAssets.Add(asset);

            Log.Info("text icon ready: " + key + ".");
            return true;
        }

        private static bool Find(string name, out Texture texture, out Rect region)
        {
            texture = null;
            region = new Rect();

            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite == null || sprite.name != name || sprite.texture == null) continue;

                texture = sprite.texture;
                region = sprite.textureRect;
                return true;
            }

            Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D candidate = textures[i];
                if (candidate == null || candidate.name != name) continue;

                texture = candidate;
                region = new Rect(0f, 0f, candidate.width, candidate.height);
                return true;
            }

            Log.Warn("text icon '" + name + "' is not loaded.");
            return false;
        }
    }
}
