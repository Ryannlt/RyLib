using System;
using System.Collections.Generic;
using System.Globalization;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RyLib
{
    internal sealed class Row
    {
        public string Owner;
        public string Key;
        public GameObject Root;
        public Action Pull;
        public Action Push;
    }

    internal static class Rows
    {
        private const float RowHeight = 44f;
        private const float BoxHeight = 40f;
        private const float LabelWidth = 300f;
        private const float ControlWidth = 380f;
        private const float ValueWidth = 90f;
        private const float TrackHeight = 8f;
        private const float HandleWidth = 20f;
        private const float SliderPadding = 16f;
        private const float LabelSize = 20f;
        private const float HeadingSize = 18f;
        private const float HeadingHeight = 34f;
        private const float DividerHeight = 2f;
        private const float TextPadding = 12f;
        private const float TextSize = 18f;

        private static readonly List<Row> Live = new List<Row>();
        private static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color BoxFallback = new Color(0.06f, 0.06f, 0.06f, 0.85f);
        private static readonly Color BorderFallback = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color TrackFallback = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color FillFallback = new Color(0.85f, 0.78f, 0.65f, 1f);
        private static readonly Color HeadingFallback = new Color(0.86f, 0.86f, 0.86f, 1f);
        private static readonly Color DividerFallback = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        private static readonly Color PlaceholderColor = new Color(1f, 1f, 1f, 0.35f);

        private sealed class Look
        {
            public Image Box;
            public Image Border;
            public Image Track;
            public GameObject Fill;
            public GameObject Handle;
            public GameObject Value;
            public TMP_Text Heading;
            public Image Divider;
        }

        private static UIAdminQuickAccessPanel _source;
        private static Look _look;

        internal static bool Available
        {
            get
            {
                UIAdminQuickAccessPanel panel = Menu.QuickAccess;
                return panel != null && panel.pollDurationSlider != null && panel.setWeatherDropdown != null && panel.messageInputField != null;
            }
        }

        internal static Transform Container(Transform section)
        {
            GameObject rows = new GameObject("Rows", typeof(RectTransform));
            rows.transform.SetParent(section, false);

            VerticalLayoutGroup stack = rows.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 6f;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            return rows.transform;
        }

        internal static void Heading(Transform section, string title, TMP_Text fallback)
        {
            Look look = Style();

            GameObject line = new GameObject("Heading", typeof(RectTransform));
            line.transform.SetParent(section, false);

            HorizontalLayoutGroup row = line.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 18f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            LayoutElement lineSize = line.AddComponent<LayoutElement>();
            lineSize.minHeight = HeadingHeight;
            lineSize.preferredHeight = HeadingHeight;

            TMP_Text source = (look != null && look.Heading != null) ? look.Heading : fallback;
            TMP_Text text;
            if (source != null)
            {
                text = UnityEngine.Object.Instantiate(source, line.transform);
                UiClone.Strip(text.gameObject);
                StripGame(text.gameObject);
                for (int i = text.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(text.transform.GetChild(i).gameObject);

                ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
                if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
            }
            else
            {
                GameObject host = new GameObject("Title", typeof(RectTransform));
                host.transform.SetParent(line.transform, false);
                text = host.AddComponent<TextMeshProUGUI>();
                if (Menu.FontAsset != null) text.font = Menu.FontAsset;
                text.color = HeadingFallback;
            }

            text.gameObject.name = "Title";
            text.gameObject.SetActive(true);
            text.text = title.ToUpperInvariant();
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            if (look == null || look.Heading == null) text.fontSize = HeadingSize;

            LayoutElement textSize = text.GetComponent<LayoutElement>();
            if (textSize == null) textSize = text.gameObject.AddComponent<LayoutElement>();
            textSize.ignoreLayout = false;
            textSize.flexibleWidth = 0f;
            textSize.minWidth = -1f;
            textSize.preferredWidth = -1f;
            textSize.preferredHeight = HeadingHeight;

            GameObject divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(line.transform, false);
            Image bar = divider.AddComponent<Image>();
            bar.raycastTarget = false;
            if (look != null && look.Divider != null)
            {
                bar.sprite = look.Divider.sprite;
                bar.type = look.Divider.type;
                bar.color = Visible(look.Divider.color, DividerFallback);
            }
            else
            {
                bar.color = DividerFallback;
            }

            LayoutElement barSize = divider.AddComponent<LayoutElement>();
            barSize.flexibleWidth = 1f;
            barSize.minHeight = DividerHeight;
            barSize.preferredHeight = DividerHeight;
        }

        internal static Row Slider(string owner, string label, Transform parent, float min, float max, float step, string format,
            Func<float> get, Action<float> set)
        {
            Look look = Style();
            GameObject row = NewRow(owner, label, parent);
            RectTransform box = Box(row.transform, look);

            GameObject sliderObject = new GameObject("Slider", typeof(RectTransform));
            sliderObject.transform.SetParent(box, false);
            RectTransform sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.offsetMin = new Vector2(SliderPadding, 0f);
            sliderRect.offsetMax = new Vector2(-(ValueWidth + SliderPadding), 0f);

            Image catcher = sliderObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            RectTransform track = Strip(sliderObject.transform, "Background", TrackHeight);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.raycastTarget = false;
            if (look != null && look.Track != null)
            {
                trackImage.sprite = look.Track.sprite;
                trackImage.type = look.Track.type;
                trackImage.color = Visible(look.Track.color, TrackFallback);
            }
            else
            {
                trackImage.color = TrackFallback;
            }

            RectTransform fillArea = Strip(sliderObject.transform, "Fill Area", TrackHeight);
            RectTransform fill = Part(look == null ? null : look.Fill, fillArea, "Fill", FillFallback);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = Vector2.zero;

            RectTransform handleArea = Strip(sliderObject.transform, "Handle Slide Area", TrackHeight);
            handleArea.offsetMin = new Vector2(HandleWidth * 0.5f, handleArea.offsetMin.y);
            handleArea.offsetMax = new Vector2(-HandleWidth * 0.5f, handleArea.offsetMax.y);
            RectTransform handle = Part(look == null ? null : look.Handle, handleArea, "Handle", Color.white);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(HandleWidth, TrackHeight);

            UnityEngine.UI.Slider slider = sliderObject.AddComponent<UnityEngine.UI.Slider>();
            slider.transition = Selectable.Transition.None;
            Navigation none = new Navigation();
            none.mode = Navigation.Mode.None;
            slider.navigation = none;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = catcher;
            slider.wholeNumbers = false;
            slider.minValue = min;
            slider.maxValue = max;

            TMP_InputField box2 = ValueBox(look, box);
            Border(box, look);

            float pending = float.NaN;
            Row entry = new Row();
            entry.Owner = owner;
            entry.Key = label;
            entry.Root = row;

            slider.onValueChanged.AddListener(delegate(float value)
            {
                pending = Snap(value, min, max, step);
                if (box2 != null) box2.SetTextWithoutNotify(pending.ToString(format, CultureInfo.InvariantCulture));
            });

            if (box2 != null)
            {
                box2.onEndEdit.AddListener(delegate(string text)
                {
                    float parsed;
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    {
                        pending = Snap(parsed, min, max, step);
                        slider.SetValueWithoutNotify(pending);
                    }

                    float shown = float.IsNaN(pending) ? Owners.Read(owner, label, get, slider.value) : pending;
                    box2.SetTextWithoutNotify(shown.ToString(format, CultureInfo.InvariantCulture));
                });
            }

            entry.Push = delegate
            {
                if (float.IsNaN(pending)) return;
                float value = pending;
                pending = float.NaN;
                Owners.Run(owner, label, delegate { set(value); });
            };

            entry.Pull = delegate
            {
                if (!float.IsNaN(pending)) return;
                float value = Owners.Read(owner, label, get, slider.value);
                if (!Mathf.Approximately(slider.value, value)) slider.SetValueWithoutNotify(value);
                if (box2 != null && !box2.isFocused) box2.SetTextWithoutNotify(value.ToString(format, CultureInfo.InvariantCulture));
            };

            return Finish(entry, row);
        }

        internal static Row Dropdown(string owner, string label, Transform parent, List<string> options, Func<int> get, Action<int> set)
        {
            GameObject row = NewRow(owner, label, parent);
            GameObject clone = Control(row.transform, Menu.QuickAccess.setWeatherDropdown.gameObject);

            TMP_Dropdown dropdown = clone.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                UnityEngine.Object.Destroy(row);
                Log.Warn(owner + ": the dropdown template had no dropdown, so '" + label + "' was skipped.");
                return null;
            }

            MuteAll(clone);
            Unblock(clone, dropdown, null);
            ShowBorders(clone, dropdown.template);
            dropdown.interactable = true;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);

            Row entry = new Row();
            entry.Owner = owner;
            entry.Key = label;
            entry.Root = row;

            dropdown.onValueChanged.AddListener(delegate(int index)
            {
                Owners.Run(owner, label, delegate { set(index); });
            });

            entry.Pull = delegate
            {
                int index = Owners.Read(owner, label, get, -1);
                if (index < 0 || index >= dropdown.options.Count || dropdown.value == index) return;
                dropdown.SetValueWithoutNotify(index);
                dropdown.RefreshShownValue();
            };

            return Finish(entry, row);
        }

        internal static Row Text(string owner, string label, Transform parent, Func<string> get, Action<string> set)
        {
            Look look = Style();
            GameObject row = NewRow(owner, label, parent);
            RectTransform box = Box(row.transform, look);
            box.GetComponent<Image>().raycastTarget = true;

            GameObject area = new GameObject("Text Area", typeof(RectTransform));
            area.transform.SetParent(box, false);
            RectTransform areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(TextPadding, 4f);
            areaRect.offsetMax = new Vector2(-TextPadding, -4f);
            area.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = TextIn(area.transform, "Placeholder", PlaceholderColor);
            placeholder.fontStyle = FontStyles.Italic;
            TextMeshProUGUI shown = TextIn(area.transform, "Text", Color.white);

            TMP_InputField field = box.gameObject.AddComponent<TMP_InputField>();
            field.transition = Selectable.Transition.None;
            field.textViewport = areaRect;
            field.textComponent = shown;
            field.placeholder = placeholder;
            field.fontAsset = shown.font;
            field.pointSize = shown.fontSize;
            field.customCaretColor = true;
            field.caretColor = Color.white;
            field.selectionColor = new Color(0.66f, 0.6f, 0.47f, 0.5f);
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.characterLimit = 200;

            Border(box, look);

            Row entry = new Row();
            entry.Owner = owner;
            entry.Key = label;
            entry.Root = row;

            field.onEndEdit.AddListener(delegate(string text)
            {
                Owners.Run(owner, label, delegate { set(text.Trim()); });
            });

            entry.Pull = delegate
            {
                if (field.isFocused) return;
                string value = Owners.Say(owner, label, get, string.Empty);
                if (field.text != value) field.SetTextWithoutNotify(value);
            };

            return Finish(entry, row);
        }

        internal static void RefreshVisible()
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Row row = Live[i];
                if (row.Root == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }

                if (row.Push != null) row.Push();
                if (row.Root.activeInHierarchy && row.Pull != null) row.Pull();
            }
        }

        private static Look Style()
        {
            UIAdminQuickAccessPanel panel = Menu.QuickAccess;
            if (panel == _source && _look != null) return _look;

            _source = panel;
            _look = null;
            if (panel == null) return null;

            Look look = new Look();
            if (panel.pollDurationSlider != null)
            {
                Transform root = panel.pollDurationSlider.transform;
                Transform container = Find(root, "Selectable Container");
                Transform slider = (container == null) ? Find(root, "Slider") : container.Find("Slider");

                look.Box = Get<Image>(container);
                look.Border = Get<Image>(container == null ? null : container.Find("Border"));
                look.Track = Get<Image>(slider == null ? null : slider.Find("Background"));
                look.Fill = Child(slider, "Fill Area/Fill");
                look.Handle = Child(slider, "Handle Slide Area/Handle");
                look.Value = (panel.pollDurationSlider.SliderValue == null) ? null : panel.pollDurationSlider.SliderValue.gameObject;
            }

            FindHeading(panel.transform, look);
            if (look.Heading == null || look.Divider == null) Log.Info("the Admin tab section heading was not found, so headings use a plain divider.");

            _look = look;
            return look;
        }

        private static void FindHeading(Transform root, Look look)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                RectTransform rect = image.rectTransform;
                float height = rect.rect.height > 0f ? rect.rect.height : rect.sizeDelta.y;
                if (height <= 0f || height > 4f || rect.parent == null) continue;
                if (Under(rect, "Header Background")) continue;

                TMP_Text label = null;
                for (int j = 0; j < rect.parent.childCount; j++)
                {
                    TMP_Text candidate = rect.parent.GetChild(j).GetComponent<TMP_Text>();
                    if (candidate != null && !string.IsNullOrEmpty(candidate.text) && candidate.fontSize < 26f) label = candidate;
                }

                if (label == null) continue;

                look.Divider = image;
                look.Heading = label;
                Log.Info("section headings copy the Admin tab's '" + label.text + "' heading and '" + image.name + "' divider.");
                return;
            }
        }

        private static bool Under(Transform node, string name)
        {
            for (Transform step = node; step != null; step = step.parent)
            {
                if (step.name == name) return true;
            }

            return false;
        }

        private static GameObject NewRow(string owner, string label, Transform parent)
        {
            GameObject row = new GameObject("RyLib_Row_" + owner + "_" + label, typeof(RectTransform));
            row.SetActive(false);
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup line = row.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 16f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;

            LayoutElement rowSize = row.AddComponent<LayoutElement>();
            rowSize.minHeight = RowHeight;
            rowSize.preferredHeight = RowHeight;

            GameObject name = new GameObject("Label", typeof(RectTransform));
            name.transform.SetParent(row.transform, false);
            TextMeshProUGUI text = name.AddComponent<TextMeshProUGUI>();
            if (Menu.FontAsset != null) text.font = Menu.FontAsset;
            text.text = label;
            text.fontSize = LabelSize;
            text.fontSizeMin = 12f;
            text.fontSizeMax = LabelSize;
            text.enableAutoSizing = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = LabelColor;
            text.raycastTarget = false;

            LayoutElement nameSize = name.AddComponent<LayoutElement>();
            nameSize.minWidth = LabelWidth;
            nameSize.preferredWidth = LabelWidth;
            nameSize.preferredHeight = RowHeight;
            return row;
        }

        private static TextMeshProUGUI TextIn(Transform parent, string name, Color color)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)host.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = host.AddComponent<TextMeshProUGUI>();
            if (Menu.FontAsset != null) text.font = Menu.FontAsset;
            text.fontSize = TextSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Box(Transform parent, Look look)
        {
            GameObject box = new GameObject("Control", typeof(RectTransform));
            box.transform.SetParent(parent, false);

            Image back = box.AddComponent<Image>();
            back.raycastTarget = false;
            if (look != null && look.Box != null)
            {
                back.sprite = look.Box.sprite;
                back.type = look.Box.type;
                back.color = Visible(look.Box.color, BoxFallback);
            }
            else
            {
                back.color = BoxFallback;
            }

            Size(box);
            return (RectTransform)box.transform;
        }

        private static void Border(RectTransform box, Look look)
        {
            GameObject border = new GameObject("Border", typeof(RectTransform));
            border.transform.SetParent(box, false);
            RectTransform rect = (RectTransform)border.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image line = border.AddComponent<Image>();
            line.raycastTarget = false;
            if (look != null && look.Border != null && look.Border.sprite != null)
            {
                line.sprite = look.Border.sprite;
                line.type = look.Border.type;
                line.color = Visible(look.Border.color, BorderFallback);
            }
            else
            {
                line.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private static TMP_InputField ValueBox(Look look, RectTransform box)
        {
            if (look == null || look.Value == null) return null;

            GameObject value = UnityEngine.Object.Instantiate(look.Value, box);
            value.name = "Value";
            value.SetActive(true);
            UiClone.Strip(value);
            StripGame(value);

            RectTransform rect = (RectTransform)value.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(ValueWidth, 0f);
            rect.anchoredPosition = Vector2.zero;

            TMP_InputField field = value.GetComponent<TMP_InputField>();
            if (field == null) field = value.GetComponentInChildren<TMP_InputField>(true);
            if (field == null) return null;

            Silence(field.onValueChanged);
            Silence(field.onEndEdit);
            Silence(field.onSubmit);
            Silence(field.onSelect);
            Silence(field.onDeselect);
            field.interactable = true;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            ShowBorders(value, null);
            return field;
        }

        private static GameObject Control(Transform row, GameObject template)
        {
            GameObject clone = UnityEngine.Object.Instantiate(template, row);
            clone.name = "Control";
            clone.SetActive(true);
            UiClone.Strip(clone);
            StripGame(clone);
            Size(clone);
            return clone;
        }

        private static void Size(GameObject control)
        {
            LayoutElement size = control.GetComponent<LayoutElement>();
            if (size == null) size = control.AddComponent<LayoutElement>();
            size.ignoreLayout = false;
            size.minWidth = ControlWidth;
            size.preferredWidth = ControlWidth;
            size.flexibleWidth = 0f;
            size.minHeight = BoxHeight;
            size.preferredHeight = BoxHeight;
        }

        private static RectTransform Strip(Transform parent, string name, float height)
        {
            GameObject strip = new GameObject(name, typeof(RectTransform));
            strip.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)strip.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static RectTransform Part(GameObject template, RectTransform parent, string name, Color fallback)
        {
            GameObject part;
            if (template != null)
            {
                part = UnityEngine.Object.Instantiate(template, parent);
                part.SetActive(true);
                StripGame(part);

                AspectRatioFitter[] fitters = part.GetComponentsInChildren<AspectRatioFitter>(true);
                for (int i = 0; i < fitters.Length; i++) UnityEngine.Object.DestroyImmediate(fitters[i]);

                Graphic[] graphics = part.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    graphics[i].raycastTarget = false;
                    graphics[i].color = Visible(graphics[i].color, fallback);
                }
            }
            else
            {
                part = new GameObject(name, typeof(RectTransform));
                part.transform.SetParent(parent, false);
                Image image = part.AddComponent<Image>();
                image.color = fallback;
                image.raycastTarget = false;
            }

            part.name = name;
            return (RectTransform)part.transform;
        }

        private static void StripGame(GameObject target)
        {
            UIGraphicsUtil[] hovers = target.GetComponentsInChildren<UIGraphicsUtil>(true);
            for (int i = 0; i < hovers.Length; i++) UnityEngine.Object.DestroyImmediate(hovers[i]);
        }

        private static void ShowBorders(GameObject root, Transform skip)
        {
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                Transform node = nodes[i];
                if (node.gameObject.activeSelf || node.name.IndexOf("Border", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (skip != null && node.IsChildOf(skip)) continue;
                node.gameObject.SetActive(true);
            }
        }

        private static Color Visible(Color color, Color fallback)
        {
            return (color.a < 0.05f) ? fallback : color;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].name == name) return nodes[i];
            }

            return null;
        }

        private static GameObject Child(Transform root, string path)
        {
            if (root == null) return null;
            Transform child = root.Find(path);
            return (child == null) ? null : child.gameObject;
        }

        private static T Get<T>(Transform node) where T : Component
        {
            return (node == null) ? null : node.GetComponent<T>();
        }

        private static Row Finish(Row entry, GameObject row)
        {
            row.SetActive(true);
            Live.Add(entry);
            if (entry.Pull != null) entry.Pull();
            return entry;
        }

        private static void MuteAll(GameObject clone)
        {
            UnityEngine.UI.Slider[] sliders = clone.GetComponentsInChildren<UnityEngine.UI.Slider>(true);
            for (int i = 0; i < sliders.Length; i++) Silence(sliders[i].onValueChanged);

            TMP_Dropdown[] dropdowns = clone.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++) Silence(dropdowns[i].onValueChanged);

            TMP_InputField[] fields = clone.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < fields.Length; i++)
            {
                Silence(fields[i].onValueChanged);
                Silence(fields[i].onEndEdit);
                Silence(fields[i].onSubmit);
                Silence(fields[i].onSelect);
                Silence(fields[i].onDeselect);
            }

            UnityEngine.UI.Button[] buttons = clone.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < buttons.Length; i++) Silence(buttons[i].onClick);
        }

        private static void Unblock(GameObject clone, Selectable keep, Selectable alsoKeep)
        {
            UnityEngine.UI.Button[] buttons = clone.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                UnityEngine.UI.Button button = buttons[i];
                if (button == null || button == keep || button == alsoKeep) continue;

                Graphic own = button.GetComponent<Graphic>();
                if (own != null) own.raycastTarget = false;
                UnityEngine.Object.DestroyImmediate(button);
            }

            Graphic[] graphics = clone.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Transform node = graphics[i].transform;
                if (Inside(node, keep) || Inside(node, alsoKeep)) continue;
                graphics[i].raycastTarget = false;
            }

            CanvasGroup[] groups = clone.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].interactable = true;
                groups[i].blocksRaycasts = true;
            }
        }

        private static bool Inside(Transform node, Selectable root)
        {
            return root != null && node.IsChildOf(root.transform);
        }

        private static void Silence(UnityEventBase target)
        {
            if (target == null) return;

            UiClone.Mute(target);
            target.RemoveAllListeners();
        }

        private static float Snap(float value, float min, float max, float step)
        {
            if (step > 0f) value = min + Mathf.Round((value - min) / step) * step;
            return Mathf.Clamp((float)Math.Round(value, 4), min, max);
        }
    }
}
