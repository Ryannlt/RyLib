using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RyLib
{
    internal sealed class ControlSpec
    {
        public string Owner;
        public string Key;
        public string Icon;
        public Func<string> Text;
        public Func<bool> IsOn;
        public Action Click;
        public Action RightClick;
    }

    internal sealed class RightClickRelay : MonoBehaviour, IPointerClickHandler
    {
        internal Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && Clicked != null) Clicked();
        }
    }

    internal sealed class Control
    {
        public ControlSpec Spec;
        public GameObject Root;
        public TMP_Text Label;
        public Image Icon;
    }

    internal static class Controls
    {
        private static readonly List<Control> Live = new List<Control>();

        private static Color _labelOn = new Color(1f, 1f, 1f, 0.588f);
        private static Color _labelOff = new Color(0.855f, 0.753f, 0.612f, 1f);
        private static Color _iconOn = new Color(1f, 1f, 1f, 0.588f);
        private static Color _iconOff = new Color(0.859f, 0f, 0f, 1f);

        public static void SetPalette(UIRoundPlayersControlPanel players)
        {
            if (players == null) return;

            _labelOn = players.enabledOptionColor;
            _labelOff = players.disabledOptionColor;
            _iconOn = players.voipUnmutedColor;
            _iconOff = players.voipMutedColor;
        }

        public static ControlSpec Toggle(string owner, string label, ConfigEntry<bool> entry, string icon)
        {
            ControlSpec spec = new ControlSpec();
            spec.Owner = owner;
            spec.Key = label;
            spec.Icon = icon;
            spec.Text = delegate { return label + (entry.Value ? " On" : " Off"); };
            spec.IsOn = delegate { return entry.Value; };
            spec.Click = delegate { entry.Value = !entry.Value; };
            return spec;
        }

        public static ControlSpec Cycle<T>(string owner, string label, ConfigEntry<T> entry, Func<T, string> text,
            Func<T, bool> isOn, string icon) where T : struct, Enum
        {
            Array values = Enum.GetValues(typeof(T));

            ControlSpec spec = new ControlSpec();
            spec.Owner = owner;
            spec.Key = label;
            spec.Icon = icon;
            spec.Text = delegate { return (text != null) ? text(entry.Value) : label + " " + entry.Value; };
            spec.IsOn = delegate { return isOn == null || isOn(entry.Value); };
            spec.Click = delegate { entry.Value = Next(values, entry.Value); };
            return spec;
        }

        public static ControlSpec Button(string owner, string label, Action onClick, string icon)
        {
            ControlSpec spec = new ControlSpec();
            spec.Owner = owner;
            spec.Key = label;
            spec.Icon = icon;
            spec.Text = delegate { return label; };
            spec.IsOn = delegate { return true; };
            spec.Click = onClick;
            return spec;
        }

        public static ControlSpec Stepper(string owner, string label, ConfigEntry<float> entry, float step, string format,
            string icon)
        {
            ControlSpec spec = new ControlSpec();
            spec.Owner = owner;
            spec.Key = label;
            spec.Icon = icon;
            spec.Text = delegate { return label + "  " + entry.Value.ToString(format); };
            spec.IsOn = delegate { return true; };
            spec.Click = delegate { Nudge(entry, step); };
            spec.RightClick = delegate { Nudge(entry, -step); };
            return spec;
        }

        private static void Nudge(ConfigEntry<float> entry, float delta)
        {
            float value = (float)Math.Round(entry.Value + delta, 3);
            AcceptableValueBase range = entry.Description.AcceptableValues;
            entry.Value = (range != null) ? (float)range.Clamp(value) : value;
            GlowStyle.Saved();
        }

        private static T Next<T>(Array values, T current)
        {
            int index = Array.IndexOf(values, current);
            return (T)values.GetValue((index + 1) % values.Length);
        }

        public static Control Make(ControlSpec spec, Transform parent, GameObject template)
        {
            if (template == null || parent == null) return null;

            GameObject root = UnityEngine.Object.Instantiate(template, parent);
            root.name = "RyLib_" + spec.Owner + "_" + spec.Key;
            UiClone.Strip(root);

            UnityEngine.UI.Button button = root.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button == null)
            {
                UnityEngine.Object.Destroy(root);
                Log.Warn(spec.Owner + ": the control template carried no button, so '" + spec.Key + "' was skipped.");
                return null;
            }

            Control control = new Control();
            control.Spec = spec;
            control.Root = root;
            control.Label = root.GetComponentInChildren<TMP_Text>(true);
            control.Icon = FindIcon(root.transform);

            UiClone.Mute(button.onClick);
            button.onClick.AddListener(delegate
            {
                Owners.Run(spec.Owner, spec.Key, spec.Click);
                Refresh(control);
            });

            if (spec.RightClick != null)
            {
                RightClickRelay relay = button.gameObject.AddComponent<RightClickRelay>();
                relay.Clicked = delegate
                {
                    Owners.Run(spec.Owner, spec.Key, spec.RightClick);
                    Refresh(control);
                };
            }

            if (!string.IsNullOrEmpty(spec.Icon) && control.Icon != null)
            {
                Sprite sprite = Sprites.Find(spec.Icon, Menu.SpriteRoot);
                if (sprite != null) control.Icon.sprite = sprite;
            }

            root.SetActive(true);
            Live.Add(control);
            Refresh(control);
            return control;
        }

        public static void Refresh(Control control)
        {
            if (control == null || control.Root == null) return;

            ControlSpec spec = control.Spec;
            bool on = Owners.Ask(spec.Owner, spec.Key + " state", spec.IsOn, true);

            if (control.Label != null)
            {
                control.Label.text = Owners.Say(spec.Owner, spec.Key + " text", spec.Text, spec.Key);
                control.Label.color = on ? _labelOn : _labelOff;
            }

            if (control.Icon != null) control.Icon.color = on ? _iconOn : _iconOff;
        }

        public static void RefreshVisible()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Control control = Live[i];
                if (control.Root == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }

                if (control.Root.activeInHierarchy) Refresh(control);
            }
        }

        private static Image FindIcon(Transform root)
        {
            Transform icon = root.Find("Icon");
            if (icon != null) return icon.GetComponent<Image>();

            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0) return images[i];
            }

            return null;
        }
    }
}
