using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RyLib
{
    internal static class Probe
    {
        public static void Dump(Transform root, int depth, string tag)
        {
            Walk(tag, root, 0, depth);
        }

        public static void Chain(Transform from, Transform stop, string tag)
        {
            if (from == null) return;

            Transform node = from.parent;
            int depth = 0;
            while (node != null && depth < 8)
            {
                Line(tag, "up " + depth + " " + Describe(node));
                if (node == stop) break;
                node = node.parent;
                depth++;
            }
        }

        public static string Describe(Transform node)
        {
            StringBuilder line = new StringBuilder();
            line.Append(node.name);
            if (!node.gameObject.activeSelf) line.Append(" [off]");

            RectTransform rect = node as RectTransform;
            if (rect != null)
            {
                line.Append(" pos").Append(rect.anchoredPosition)
                    .Append(" size").Append(rect.sizeDelta)
                    .Append(" rect").Append(rect.rect.size)
                    .Append(" anch").Append(rect.anchorMin).Append(rect.anchorMax)
                    .Append(" piv").Append(rect.pivot);
            }

            Component[] components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null || components[i] is Transform) continue;
                line.Append(" +").Append(components[i].GetType().Name);
            }

            TMP_Text text = node.GetComponent<TMP_Text>();
            if (text != null)
            {
                line.Append(" text'").Append(text.text).Append("' font ").Append(text.fontSize);
                if (text.enableAutoSizing) line.Append(" auto");
            }

            Image image = node.GetComponent<Image>();
            if (image != null && image.sprite != null) line.Append(" sprite'").Append(image.sprite.name).Append("'");

            return line.ToString();
        }

        private static void Walk(string tag, Transform node, int depth, int limit)
        {
            if (node == null || depth > limit) return;

            StringBuilder line = new StringBuilder();
            for (int i = 0; i < depth; i++) line.Append("| ");
            line.Append(Describe(node));
            Line(tag, line.ToString());

            for (int i = 0; i < node.childCount; i++) Walk(tag, node.GetChild(i), depth + 1, limit);
        }

        private static void Line(string tag, string text)
        {
            Log.Info("probe: " + tag + ": " + text);
        }
    }
}
