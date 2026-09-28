using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RyLib
{
    internal sealed class Label
    {
        public GameObject Root;
        public RectTransform Rect;
        public TMP_Text Text;
        public string Shown;
        public Color Colour = new Color(-1f, -1f, -1f, -1f);
        public float Height;
        public bool Showing;
    }

    internal static class Labels
    {
        private const float FontSize = 15f;
        private const float DefaultHeight = 2.1f;

        private static readonly Color PanelColour = new Color(0.05f, 0.05f, 0.06f, 0.78f);
        private static readonly Vector2 Padding = new Vector2(14f, 8f);
        private static readonly List<Label> Pool = new List<Label>();

        private static Canvas _canvas;
        private static RectTransform _canvasRect;
        private static Canvas _source;
        private static bool _warnedFont;

        public static void Apply(World.Live live)
        {
            WorldMark mark = live.Mark;
            if (string.IsNullOrEmpty(mark.Label) || !EnsureCanvas())
            {
                Release(live);
                return;
            }

            Label label = live.Label;
            if (label == null || label.Root == null)
            {
                label = Take();
                live.Label = label;
                if (label == null) return;
            }

            if (label.Shown != mark.Label)
            {
                label.Shown = mark.Label;
                label.Text.text = mark.Label;
                label.Rect.sizeDelta = label.Text.GetPreferredValues(mark.Label) + Padding;
            }

            if (label.Colour != mark.Colour)
            {
                label.Colour = mark.Colour;
                label.Text.color = Glows.Dark(mark.Colour) ? Color.Lerp(mark.Colour, Color.white, 0.65f) : mark.Colour;
            }

            label.Height = (mark.LabelHeight > 0f) ? mark.LabelHeight : DefaultHeight;
        }

        public static void Move(World.Live live, Camera camera, Vector3 position)
        {
            Label label = live.Label;
            if (label == null || label.Root == null) return;

            if (camera == null)
            {
                Show(label, false);
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(position + Vector3.up * label.Height);
            if (screen.z <= 0f)
            {
                Show(label, false);
                return;
            }

            Camera canvasCamera = (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, canvasCamera, out local))
            {
                Show(label, false);
                return;
            }

            label.Rect.anchoredPosition = local;
            Show(label, true);
        }

        public static void Release(World.Live live)
        {
            Label label = live.Label;
            live.Label = null;
            if (label == null || label.Root == null) return;

            Show(label, false);
            Pool.Add(label);
        }

        private static void Show(Label label, bool show)
        {
            if (label.Showing == show) return;
            label.Showing = show;
            label.Root.SetActive(show);
        }

        private static bool EnsureCanvas()
        {
            if (_canvas == null)
            {
                GameObject host = new GameObject("RyLib_Labels");
                host.transform.SetParent(World.Root, false);

                _canvas = host.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvasRect = host.GetComponent<RectTransform>();
                _source = null;
            }

            Sync();
            return true;
        }

        private static void Sync()
        {
            Canvas menu = Menu.PanelCanvas;
            if (menu == null || menu == _source) return;
            _source = menu;

            if (menu.renderMode == RenderMode.ScreenSpaceCamera && menu.worldCamera != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = menu.worldCamera;
                _canvas.planeDistance = Mathf.Min(menu.planeDistance + 1f, menu.worldCamera.farClipPlane * 0.9f);
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            _canvas.sortingLayerID = menu.sortingLayerID;
            _canvas.sortingOrder = menu.sortingOrder - 1;
        }

        private static Label Take()
        {
            while (Pool.Count > 0)
            {
                Label pooled = Pool[Pool.Count - 1];
                Pool.RemoveAt(Pool.Count - 1);
                if (pooled.Root != null) return pooled;
            }

            TMP_FontAsset font = Menu.FontAsset;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                if (!_warnedFont) Log.Warn("no TMP font is loaded yet, so world labels wait until the P menu exists.");
                _warnedFont = true;
                return null;
            }

            Label label = new Label();

            label.Root = new GameObject("RyLib_Label", typeof(RectTransform));
            label.Root.transform.SetParent(_canvasRect, false);
            label.Rect = (RectTransform)label.Root.transform;
            label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            label.Rect.pivot = new Vector2(0.5f, 0f);

            Image panel = label.Root.AddComponent<Image>();
            panel.color = PanelColour;
            panel.raycastTarget = false;

            GameObject textHost = new GameObject("Text", typeof(RectTransform));
            textHost.transform.SetParent(label.Root.transform, false);

            RectTransform textRect = (RectTransform)textHost.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textHost.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            label.Text = text;

            label.Root.SetActive(false);
            label.Showing = false;
            return label;
        }
    }
}
