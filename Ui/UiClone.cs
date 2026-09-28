using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RyLib
{
    internal static class UiClone
    {
        public const string IconSquare = "Button Icon Square";

        public static Button Make(Button template, Transform parent, string name)
        {
            GameObject host = UnityEngine.Object.Instantiate(template.gameObject, parent);
            host.name = name;

            Button button = host.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(host);
                return null;
            }

            Mute(button.onClick);
            Strip(host);
            return button;
        }

        public static void Mute(UnityEventBase target)
        {
            int count = target.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                target.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
        }

        public static void Strip(GameObject target)
        {
            MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;

                string type = behaviours[i].GetType().Name;
                if (type.IndexOf("Localize", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    type == "UIAutoScrollableListItem" || type == "UIBasicTooltipEntry")
                {
                    UnityEngine.Object.Destroy(behaviours[i]);
                }
            }
        }

        public static void SetLabel(GameObject target, string label)
        {
            TMP_Text[] texts = target.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (IsHotkey(texts[i].text)) texts[i].gameObject.SetActive(false);
                else texts[i].text = label;
            }

            Text[] legacy = target.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < legacy.Length; i++)
            {
                if (IsHotkey(legacy[i].text)) legacy[i].gameObject.SetActive(false);
                else legacy[i].text = label;
            }
        }

        public static bool IsHotkey(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]) && !char.IsWhiteSpace(text[i])) return false;
            }

            return true;
        }

        public static int CopyIcon(GameObject host, Component source)
        {
            Transform target = Icon(host.transform);
            Transform origin = (source == null) ? null : Icon(source.transform);
            if (target == null || origin == null) return 0;

            return Paint(target, origin);
        }

        private static Transform Icon(Transform button)
        {
            Transform icon = button.Find(IconSquare);
            if (icon != null) return icon;

            for (int i = 0; i < button.childCount; i++)
            {
                Transform child = button.GetChild(i);
                if (child.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0) return child;
            }

            return null;
        }

        private static int Paint(Transform target, Transform origin)
        {
            int copied = 0;

            for (int i = 0; i < target.childCount; i++)
            {
                Transform child = target.GetChild(i);
                Transform match = origin.Find(child.name);
                if (match == null) continue;

                Image to = child.GetComponent<Image>();
                Image from = match.GetComponent<Image>();
                if (to != null && from != null)
                {
                    to.sprite = from.sprite;
                    to.overrideSprite = from.overrideSprite;
                    to.color = from.color;
                    to.type = from.type;
                    to.preserveAspect = from.preserveAspect;
                    copied++;
                }

                TMP_Text toText = child.GetComponent<TMP_Text>();
                TMP_Text fromText = match.GetComponent<TMP_Text>();
                if (toText != null && fromText != null && !IsHotkey(fromText.text))
                {
                    toText.text = fromText.text;
                    copied++;
                }

                copied += Paint(child, match);
            }

            return copied;
        }
    }
}
