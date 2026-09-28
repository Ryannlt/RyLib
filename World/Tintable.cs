using System;
using System.Collections.Generic;
using UnityEngine;

namespace RyLib
{
    internal static class Tintable
    {
        private const int Size = 64;
        private const int Pad = 5;
        private const int Outline = 4;
        private const int Threshold = 12;

        private static readonly List<int> OffsetsX = new List<int>();
        private static readonly List<int> OffsetsY = new List<int>();

        public static Sprite From(Sprite source)
        {
            if (source == null || source.texture == null) return null;
            return From(source.texture, Region(source), source.name);
        }

        public static Sprite From(Texture texture, Rect rect, string name)
        {
            if (texture == null || rect.width < 1f || rect.height < 1f) return null;

            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            byte[] alpha = Read(texture, rect, width, height);

            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (alpha[y * width + x] <= Threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return null;

            float[] mask = Fit(alpha, width, height, minX, minY, maxX - minX + 1, maxY - minY + 1, Size, Pad);
            float[] grown = Grow(mask);

            Color32[] pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float cover = Mathf.Max(mask[i], grown[i]);
                byte light = (cover <= 0f) ? (byte)0 : (byte)Mathf.RoundToInt(255f * mask[i] / cover);
                pixels[i] = new Color32(light, light, light, (byte)Mathf.RoundToInt(cover * 255f));
            }

            return Glyphs.Publish("RyLib Icon " + name, pixels);
        }

        private static Rect Region(Sprite sprite)
        {
            try
            {
                return sprite.textureRect;
            }
            catch (Exception)
            {
            }

            Vector2[] uv = sprite.uv;
            Vector2 min = Vector2.one;
            Vector2 max = Vector2.zero;
            for (int i = 0; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }

            Texture texture = sprite.texture;
            return new Rect(min.x * texture.width, min.y * texture.height,
                (max.x - min.x) * texture.width, (max.y - min.y) * texture.height);
        }

        internal static Color32[] ReadColours(Texture texture, Rect rect, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;

            try
            {
                Vector2 scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
                Vector2 offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
                Graphics.Blit(texture, target, scale, offset);

                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                copy.Apply(false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        private static byte[] Read(Texture texture, Rect rect, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;

            try
            {
                Vector2 scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
                Vector2 offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
                Graphics.Blit(texture, target, scale, offset);

                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                copy.Apply(false);

                Color32[] pixels = copy.GetPixels32();
                byte[] alpha = new byte[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;
                return alpha;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        public static Texture2D Plain(Texture texture, Rect rect, int size, int pad)
        {
            if (texture == null || rect.width < 1f || rect.height < 1f) return null;

            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            byte[] alpha = Read(texture, rect, width, height);

            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (alpha[y * width + x] <= Threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return null;

            float[] mask = Fit(alpha, width, height, minX, minY, maxX - minX + 1, maxY - minY + 1, size, pad);
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(mask[i]) * 255f));
            }

            Texture2D plain = new Texture2D(size, size, TextureFormat.RGBA32, true);
            plain.wrapMode = TextureWrapMode.Clamp;
            plain.filterMode = FilterMode.Trilinear;
            plain.SetPixels32(pixels);
            plain.Apply(true, false);
            plain.hideFlags = HideFlags.HideAndDontSave;
            return plain;
        }

        private static float[] Fit(byte[] alpha, int width, int height, int left, int bottom, int boxWidth, int boxHeight,
            int size, int pad)
        {
            float[] mask = new float[size * size];

            int inner = size - 2 * pad;
            float scale = inner / (float)Mathf.Max(boxWidth, boxHeight);
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(boxWidth * scale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(boxHeight * scale));
            int startX = (size - targetWidth) / 2;
            int startY = (size - targetHeight) / 2;
            int samples = Mathf.Clamp(Mathf.CeilToInt(1f / scale), 1, 8);

            for (int ty = 0; ty < targetHeight; ty++)
            {
                for (int tx = 0; tx < targetWidth; tx++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float fx = left + (tx + (sx + 0.5f) / samples) / scale - 0.5f;
                            float fy = bottom + (ty + (sy + 0.5f) / samples) / scale - 0.5f;
                            sum += Bilinear(alpha, width, height, fx, fy);
                        }
                    }

                    mask[(startY + ty) * size + startX + tx] = sum / (samples * samples * 255f);
                }
            }

            return mask;
        }

        private static float Bilinear(byte[] alpha, int width, int height, float x, float y)
        {
            x = Mathf.Clamp(x, 0f, width - 1);
            y = Mathf.Clamp(y, 0f, height - 1);

            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float u = x - x0;
            float v = y - y0;

            float bottom = Mathf.Lerp(alpha[y0 * width + x0], alpha[y0 * width + x1], u);
            float top = Mathf.Lerp(alpha[y1 * width + x0], alpha[y1 * width + x1], u);
            return Mathf.Lerp(bottom, top, v);
        }

        private static float[] Grow(float[] mask)
        {
            if (OffsetsX.Count == 0)
            {
                for (int dy = -Outline; dy <= Outline; dy++)
                {
                    for (int dx = -Outline; dx <= Outline; dx++)
                    {
                        if (dx * dx + dy * dy > Outline * Outline) continue;
                        OffsetsX.Add(dx);
                        OffsetsY.Add(dy);
                    }
                }
            }

            float[] grown = new float[mask.Length];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float best = 0f;
                    for (int k = 0; k < OffsetsX.Count; k++)
                    {
                        int sx = x + OffsetsX[k];
                        int sy = y + OffsetsY[k];
                        if (sx < 0 || sy < 0 || sx >= Size || sy >= Size) continue;

                        float value = mask[sy * Size + sx];
                        if (value > best) best = value;
                    }

                    grown[y * Size + x] = best;
                }
            }

            return grown;
        }
    }
}
