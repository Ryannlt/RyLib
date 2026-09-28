using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace RyLib
{
    internal static class KeyCaps
    {
        private const byte Solid = 200;
        private const int Inset = 2;
        private const float Stroke = 0.16f;
        private const float Span = 0.42f;
        private const float Reach = 0.08f;

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal);

        private struct Box
        {
            public float Left;
            public float Right;
            public float Bottom;
            public float Top;
        }

        internal static string Name(TMP_SpriteAsset source, int sourceIndex, KeyCode key)
        {
            if (source == null || (key != KeyCode.LeftBracket && key != KeyCode.RightBracket)) return null;

            string name = "RyLib_Key_" + key;
            string cached;
            if (Names.TryGetValue(name, out cached) && TextIcons.RegisteredName(cached)) return cached;

            try
            {
                if (!Build(source, sourceIndex, key == KeyCode.LeftBracket, name)) return null;
            }
            catch (Exception error)
            {
                Log.Warn("key cap for " + key + " could not be drawn: " + error.Message);
                return null;
            }

            Names[name] = name;
            return name;
        }

        private static bool Build(TMP_SpriteAsset source, int sourceIndex, bool left, string name)
        {
            List<TMP_SpriteCharacter> characters = source.spriteCharacterTable;
            if (characters == null || sourceIndex < 0 || sourceIndex >= characters.Count || source.spriteSheet == null) return false;

            Glyph glyph = characters[sourceIndex].glyph;
            if (glyph == null) return false;

            GlyphRect rect = glyph.glyphRect;
            int width = rect.width;
            int height = rect.height;
            if (width < 8 || height < 8) return false;

            Color32[] pixels = Tintable.ReadColours(source.spriteSheet, new Rect(rect.x, rect.y, width, height), width, height);
            if (!Repaint(pixels, width, height, left)) return false;

            Texture2D sheet = new Texture2D(width, height, TextureFormat.RGBA32, true);
            sheet.wrapMode = TextureWrapMode.Clamp;
            sheet.filterMode = FilterMode.Trilinear;
            sheet.SetPixels32(pixels);
            sheet.Apply(true, false);
            sheet.hideFlags = HideFlags.HideAndDontSave;

            return TextIcons.Register(name, sheet, glyph.metrics, glyph.scale, source);
        }

        private static bool Repaint(Color32[] pixels, int width, int height, bool left)
        {
            bool[] inner = new bool[pixels.Length];
            float capLight = 0f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!Inside(pixels, width, height, x, y)) continue;
                    inner[y * width + x] = true;
                    capLight = Mathf.Max(capLight, Light(pixels[y * width + x]));
                }
            }

            if (capLight <= 0f) return false;

            Color cap = Color.clear;
            int capCount = 0;
            Color ink = Color.clear;
            int inkCount = 0;
            Box letter = new Box { Left = width, Right = -1f, Bottom = height, Top = -1f };

            for (int i = 0; i < pixels.Length; i++)
            {
                if (!inner[i]) continue;

                float light = Light(pixels[i]);
                if (light >= capLight * 0.85f)
                {
                    cap += (Color)pixels[i];
                    capCount++;
                }
                else if (light <= capLight * 0.5f)
                {
                    ink += (Color)pixels[i];
                    inkCount++;

                    int x = i % width;
                    int y = i / width;
                    letter.Left = Mathf.Min(letter.Left, x);
                    letter.Right = Mathf.Max(letter.Right, x + 1);
                    letter.Bottom = Mathf.Min(letter.Bottom, y);
                    letter.Top = Mathf.Max(letter.Top, y + 1);
                }
            }

            if (capCount == 0) return false;
            cap /= capCount;
            ink = (inkCount > 0) ? ink / inkCount : new Color(0.16f, 0.16f, 0.17f, 1f);

            if (inkCount == 0)
            {
                letter.Left = width * 0.3f;
                letter.Right = width * 0.7f;
                letter.Bottom = height * 0.3f;
                letter.Top = height * 0.7f;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                if (!inner[i] || Light(pixels[i]) >= capLight * 0.92f) continue;

                byte alpha = pixels[i].a;
                Color32 fill = cap;
                fill.a = alpha;
                pixels[i] = fill;
            }

            Draw(pixels, width, height, inner, letter, left, ink);
            return true;
        }

        private static void Draw(Color32[] pixels, int width, int height, bool[] inner, Box letter, bool left, Color ink)
        {
            float tall = letter.Top - letter.Bottom;
            float bottom = letter.Bottom - tall * Reach;
            float top = letter.Top + tall * Reach;
            tall = top - bottom;

            float stroke = Mathf.Max(1.5f, tall * Stroke);
            float span = Mathf.Max(stroke * 2.5f, tall * Span);
            float centre = (letter.Left + letter.Right) * 0.5f;
            float near = left ? centre - span * 0.5f : centre + span * 0.5f - stroke;
            float far = left ? centre - span * 0.5f : centre - span * 0.5f;

            Box bar = new Box { Left = near, Right = near + stroke, Bottom = bottom, Top = top };
            Box cap = new Box { Left = far, Right = far + span, Bottom = top - stroke, Top = top };
            Box foot = new Box { Left = far, Right = far + span, Bottom = bottom, Top = bottom + stroke };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (!inner[i]) continue;

                    float cover = Mathf.Max(Cover(bar, x, y), Mathf.Max(Cover(cap, x, y), Cover(foot, x, y)));
                    if (cover <= 0f) continue;

                    Color blended = Color.Lerp(pixels[i], ink, cover);
                    blended.a = pixels[i].a / 255f;
                    pixels[i] = blended;
                }
            }
        }

        private static float Cover(Box box, int x, int y)
        {
            float across = Mathf.Clamp01(Mathf.Min(box.Right, x + 1f) - Mathf.Max(box.Left, x));
            float down = Mathf.Clamp01(Mathf.Min(box.Top, y + 1f) - Mathf.Max(box.Bottom, y));
            return across * down;
        }

        private static bool Inside(Color32[] pixels, int width, int height, int x, int y)
        {
            for (int dy = -Inset; dy <= Inset; dy++)
            {
                for (int dx = -Inset; dx <= Inset; dx++)
                {
                    int sx = x + dx;
                    int sy = y + dy;
                    if (sx < 0 || sy < 0 || sx >= width || sy >= height) return false;
                    if (pixels[sy * width + sx].a < Solid) return false;
                }
            }

            return true;
        }

        private static float Light(Color32 pixel)
        {
            return (0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b) / 255f;
        }
    }
}
