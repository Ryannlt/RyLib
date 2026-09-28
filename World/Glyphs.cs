using System.Collections.Generic;
using UnityEngine;

namespace RyLib
{
    internal enum ClassShape
    {
        Circle,
        Star,
        Diamond,
        Triangle,
        Square,
        Pentagon,
        Target,
        Cross
    }

    internal static class Glyphs
    {
        private const int Size = 64;
        private const float Pixels = Size * 0.5f;
        private const float Border = 0.16f;
        private const int HaloSize = 80;
        private const int HaloPad = 8;
        private const int HaloGrow = 6;

        private static readonly Dictionary<Sprite, Sprite> Halos = new Dictionary<Sprite, Sprite>();

        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _arrow;
        private static Sprite _brackets;
        private static Sprite _crosshair;
        private static Texture2D _grid;
        private static readonly Sprite[] Classes = new Sprite[8];

        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = Make("RyLib Circle", CircleShape);
                return _circle;
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (_ring == null) _ring = Make("RyLib Ring", RingShape);
                return _ring;
            }
        }

        public static Sprite Arrow
        {
            get
            {
                if (_arrow == null) _arrow = Make("RyLib Arrow", ArrowShape);
                return _arrow;
            }
        }

        public static Sprite Brackets
        {
            get
            {
                if (_brackets == null) _brackets = Outlined("RyLib Brackets", BracketsField, 0.1f, null);
                return _brackets;
            }
        }

        public static Sprite Crosshair
        {
            get
            {
                if (_crosshair == null) _crosshair = Outlined("RyLib Crosshair", CrosshairField, 0.1f, null);
                return _crosshair;
            }
        }

        public static Texture2D Grid
        {
            get
            {
                if (_grid == null) _grid = MakeGrid();
                return _grid;
            }
        }

        public static Sprite Mark(MapShape shape)
        {
            switch (shape)
            {
                case MapShape.Flag: return FlagGlyph.Sprite;
                case MapShape.Dot: return Circle;
                default: return Ring;
            }
        }

        public static Sprite Class(ClassShape shape)
        {
            int index = (int)shape;
            if (Classes[index] == null) Classes[index] = MakeClass(shape);
            return Classes[index];
        }

        private delegate float Shape(float x, float y);

        private static Sprite MakeClass(ClassShape shape)
        {
            switch (shape)
            {
                case ClassShape.Star: return Outlined("RyLib Star", StarField, 0f, null);
                case ClassShape.Diamond: return Outlined("RyLib Diamond", DiamondField, 0f, null);
                case ClassShape.Triangle: return Outlined("RyLib Triangle", TriangleField, 0f, null);
                case ClassShape.Square: return Outlined("RyLib Square", SquareField, 0f, null);
                case ClassShape.Pentagon: return Outlined("RyLib Pentagon", PentagonField, 0f, null);
                case ClassShape.Target: return Outlined("RyLib Target", TargetField, 0f, TargetFill);
                case ClassShape.Cross: return Outlined("RyLib Cross", CrossField, 0f, null);
                default: return Outlined("RyLib Dot", DotField, 0f, null);
            }
        }

        private static float CircleShape(float x, float y)
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((0.92f - distance) * Size * 0.5f);
        }

        private static float RingShape(float x, float y)
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            float edge = Mathf.Abs(distance - 0.8f);
            return Mathf.Clamp01((0.12f - edge) * Size * 0.5f);
        }

        private static float ArrowShape(float x, float y)
        {
            float half = (0.95f - y) * 0.42f;
            if (y < -0.9f || y > 0.95f) return 0f;
            if (y < -0.55f && Mathf.Abs(x) < 0.2f) return 0f;
            return Mathf.Clamp01((half - Mathf.Abs(x) * 0.95f) * Size * 0.5f + 0.5f) *
                   Mathf.Clamp01((0.95f - y) * Size * 0.5f);
        }

        private static float DotField(float x, float y)
        {
            return Mathf.Sqrt(x * x + y * y) - 0.8f;
        }

        private static float SquareField(float x, float y)
        {
            return Box(x, y, 0.7f, 0.7f);
        }

        private static float DiamondField(float x, float y)
        {
            return (Mathf.Abs(x) + Mathf.Abs(y) - 0.95f) * 0.70710678f;
        }

        private static float TriangleField(float x, float y)
        {
            return Polygon(x, y + 0.25f, 3, 1f);
        }

        private static float PentagonField(float x, float y)
        {
            return Polygon(x, y + 0.09f, 5, 0.9f);
        }

        private static float StarField(float x, float y)
        {
            return Star(x, y + 0.08f, 0.98f, 0.45f);
        }

        private static float TargetField(float x, float y)
        {
            return Mathf.Sqrt(x * x + y * y) - 0.9f;
        }

        private static float TargetFill(float x, float y)
        {
            float distance = Mathf.Sqrt(x * x + y * y);
            return Mathf.Min(Mathf.Abs(distance - 0.6f) - 0.14f, distance - 0.24f);
        }

        private static float CrossField(float x, float y)
        {
            return Mathf.Min(Box(x, y, 0.82f, 0.26f), Box(x, y, 0.26f, 0.82f));
        }

        private static float BracketsField(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float frame = Mathf.Abs(Mathf.Max(ax, ay) - 0.8f) - 0.07f;
            return Mathf.Max(frame, 0.42f - Mathf.Min(ax, ay));
        }

        private static float CrosshairField(float x, float y)
        {
            float ring = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.55f) - 0.06f;
            float vertical = Box(x, Mathf.Abs(y) - 0.6f, 0.06f, 0.26f);
            float horizontal = Box(Mathf.Abs(x) - 0.6f, y, 0.26f, 0.06f);
            float centre = Mathf.Sqrt(x * x + y * y) - 0.09f;
            return Mathf.Min(Mathf.Min(ring, centre), Mathf.Min(vertical, horizontal));
        }

        private static float Box(float x, float y, float halfWidth, float halfHeight)
        {
            float qx = Mathf.Abs(x) - halfWidth;
            float qy = Mathf.Abs(y) - halfHeight;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        private static float Polygon(float x, float y, int sides, float radius)
        {
            float apothem = radius * Mathf.Cos(Mathf.PI / sides);
            float distance = float.MinValue;

            for (int k = 0; k < sides; k++)
            {
                float angle = Mathf.PI * 0.5f + Mathf.PI / sides + k * 2f * Mathf.PI / sides;
                distance = Mathf.Max(distance, x * Mathf.Cos(angle) + y * Mathf.Sin(angle) - apothem);
            }

            return distance;
        }

        private static float Star(float x, float y, float radius, float inner)
        {
            const float k1x = 0.809016994f;
            const float k1y = -0.587785252f;

            x = Mathf.Abs(x);
            float fold = Mathf.Max(k1x * x + k1y * y, 0f);
            x -= 2f * fold * k1x;
            y -= 2f * fold * k1y;

            fold = Mathf.Max(-k1x * x + k1y * y, 0f);
            x += 2f * fold * k1x;
            y -= 2f * fold * k1y;

            x = Mathf.Abs(x);
            y -= radius;

            float bx = inner * -k1y;
            float by = inner * k1x - 1f;
            float h = Mathf.Min(Mathf.Clamp01((x * bx + y * by) / (bx * bx + by * by)), radius);
            float dx = x - bx * h;
            float dy = y - by * h;
            float sign = (y * bx - x * by >= 0f) ? 1f : -1f;
            return Mathf.Sqrt(dx * dx + dy * dy) * sign;
        }

        private static Sprite Make(string name, Shape shape)
        {
            Color32[] pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(shape(x, y)) * 255f);
                    pixels[py * Size + px] = new Color32(255, 255, 255, alpha);
                }
            }

            return Publish(name, pixels);
        }

        private static Sprite Outlined(string name, Shape field, float grow, Shape fill)
        {
            Color32[] pixels = new Color32[Size * Size];
            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;

                    float distance = field(x, y);
                    float edge = (grow > 0f) ? distance - grow : distance;
                    float inner = (fill != null) ? fill(x, y) : (grow > 0f ? distance : distance + Border);

                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - edge * Pixels) * 255f);
                    byte light = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - inner * Pixels) * 255f);
                    pixels[py * Size + px] = new Color32(light, light, light, alpha);
                }
            }

            return Publish(name, pixels);
        }

        internal static Sprite Publish(string name, Color32[] pixels)
        {
            return Publish(name, pixels, Size);
        }

        private static Sprite Publish(string name, Color32[] pixels, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            texture.hideFlags = HideFlags.HideAndDontSave;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static Sprite Halo(Sprite icon)
        {
            if (icon == null) return null;

            Sprite halo;
            if (Halos.TryGetValue(icon, out halo) && halo != null) return halo;

            Texture2D texture = icon.texture;
            if (texture == null || !texture.isReadable || texture.width != Size || texture.height != Size) return null;

            Color32[] source = texture.GetPixels32();
            float[] alpha = new float[HaloSize * HaloSize];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    alpha[(y + HaloPad) * HaloSize + x + HaloPad] = source[y * Size + x].a / 255f;
                }
            }

            Color32[] pixels = new Color32[HaloSize * HaloSize];
            for (int y = 0; y < HaloSize; y++)
            {
                for (int x = 0; x < HaloSize; x++)
                {
                    float best = 0f;
                    for (int dy = -HaloGrow - 1; dy <= HaloGrow + 1; dy++)
                    {
                        int sy = y + dy;
                        if (sy < 0 || sy >= HaloSize) continue;

                        for (int dx = -HaloGrow - 1; dx <= HaloGrow + 1; dx++)
                        {
                            int sx = x + dx;
                            if (sx < 0 || sx >= HaloSize) continue;

                            float reach = Mathf.Clamp01(HaloGrow + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                            float value = alpha[sy * HaloSize + sx] * reach;
                            if (value > best) best = value;
                        }
                    }

                    pixels[y * HaloSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(best * 255f));
                }
            }

            halo = Publish("RyLib Halo " + icon.name, pixels, HaloSize);
            Halos[icon] = halo;
            return halo;
        }

        private static Texture2D MakeGrid()
        {
            const int cell = 64;
            Color32 line = new Color32(70, 78, 88, 255);
            Color32 fill = new Color32(22, 26, 31, 255);

            Color32[] pixels = new Color32[cell * cell];
            for (int y = 0; y < cell; y++)
            {
                for (int x = 0; x < cell; x++)
                {
                    pixels[y * cell + x] = (x == 0 || y == 0) ? line : fill;
                }
            }

            Texture2D texture = new Texture2D(cell, cell, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
    }
}
