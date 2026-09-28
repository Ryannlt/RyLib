using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace RyLib
{
    public static class KeyHints
    {
        private const float RefreshSeconds = 0.5f;
        private const float SearchSeconds = 5f;
        private const float MaxSkew = 0.22f;
        private const float LooseGap = 40f;
        private const string Prefix = "RyLib_Hint_";

        private static readonly Regex SpriteIndex = new Regex(@"<sprite index=(\d+)", RegexOptions.Compiled);

        private sealed class Host
        {
            public string Name;
            public Func<GameObject> Find;
            public Func<GameObject, TMP_Text> Keys;
            public bool AtEnd;
            public GameObject Template;
            public TMP_Text KeySource;
        }

        private sealed class Copy
        {
            public GameObject Root;
            public TMP_Text Bind;
            public string Signature;
        }

        private sealed class Entry
        {
            public string Owner;
            public string Label;
            public Func<KeyCode[]> Keys;
            public Func<bool> Visible;
            public Copy[] Copies;
            public bool[] Skipped;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly List<Transform> Roots = new List<Transform>();
        private static readonly Host[] Hosts =
        {
            new Host { Name = "free roam", Find = FreeRoamTemplate, Keys = FirstBind },
            new Host { Name = "spectating", Find = SpectateTemplate, Keys = SquareBind, AtEnd = true }
        };

        private static UISpectatingPanel _spectating;
        private static GameObject _spectateEntry;
        private static float _nextSearch;
        private static float _nextRefresh;
        private static bool _warned;
        private static bool _dumped;

        public static bool Add(string owner, string label, Func<KeyCode[]> keys, Func<bool> visible = null)
        {
            if (!Owners.Valid(owner, "KeyHints.Add")) return false;

            if (string.IsNullOrEmpty(label) || keys == null)
            {
                Log.Warn(owner + ": KeyHints.Add needs a label and its keys.");
                return false;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (!Owners.Same(Entries[i].Owner, owner) || !Owners.Same(Entries[i].Label, label)) continue;

                Log.Warn(owner + ": key hint '" + label + "' is already registered, so the second one was ignored.");
                return false;
            }

            Entry entry = new Entry();
            entry.Owner = owner;
            entry.Label = label;
            entry.Keys = keys;
            entry.Visible = visible;
            entry.Copies = new Copy[Hosts.Length];
            entry.Skipped = new bool[Hosts.Length];
            Entries.Add(entry);

            Log.Info(owner + " added key hint '" + label + "'.");
            return true;
        }

        internal static void Tick()
        {
            if (Entries.Count == 0 || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            try
            {
                for (int i = 0; i < Hosts.Length; i++) Refresh(i);
            }
            catch (Exception error)
            {
                if (_warned) return;
                _warned = true;
                Log.Error("key hints failed: " + error.Message);
            }
        }

        private static GameObject FreeRoamTemplate()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null || client.clientFreeflightCameraManager == null) return null;
            return client.clientFreeflightCameraManager.AdminRaygunHotkey;
        }

        private static GameObject SpectateTemplate()
        {
            if (_spectateEntry != null) return _spectateEntry;
            if (Time.unscaledTime < _nextSearch) return null;
            _nextSearch = Time.unscaledTime + SearchSeconds;

            List<Transform> roots = SpectateRoots();
            _spectateEntry = EntryFor(roots, PlayerInputAction.ToggleSpectating);
            if (_spectateEntry == null) _spectateEntry = EntryFor(roots, PlayerInputAction.FreeflightCameraToggle);

            if (_spectateEntry == null && roots.Count > 0 && !_dumped)
            {
                _dumped = true;
                Log.Warn("the spectating panel has no labelled key entry to copy, so key hints are skipped there.");
                for (int i = 0; i < roots.Count; i++) Probe.Dump(roots[i], 6, "spectating panel");
            }

            return _spectateEntry;
        }

        private static List<Transform> SpectateRoots()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            CanvasGroup panel = (client == null || client.clientSpectatorManager == null) ? null : client.clientSpectatorManager.SpectatePanel;

            if (_spectating == null)
            {
                if (panel != null)
                {
                    _spectating = panel.GetComponentInChildren<UISpectatingPanel>(true);
                    if (_spectating == null) _spectating = panel.GetComponentInParent<UISpectatingPanel>();
                }

                if (_spectating == null) _spectating = UnityEngine.Object.FindObjectOfType<UISpectatingPanel>(true);
            }

            Roots.Clear();
            if (_spectating != null)
            {
                AddRoot(_spectating.transform);
                if (_spectating.spectatingPanelRootTransform != null) AddRoot(_spectating.spectatingPanelRootTransform);
            }

            if (panel != null) AddRoot(panel.transform);
            return Roots;
        }

        private static void AddRoot(Transform root)
        {
            for (int i = Roots.Count - 1; i >= 0; i--)
            {
                if (root.IsChildOf(Roots[i])) return;
                if (Roots[i].IsChildOf(root)) Roots.RemoveAt(i);
            }

            Roots.Add(root);
        }

        private static GameObject EntryFor(List<Transform> roots, PlayerInputAction action)
        {
            for (int r = 0; r < roots.Count; r++)
            {
                global::Hotkey[] hotkeys = roots[r].GetComponentsInChildren<global::Hotkey>(true);
                for (int i = 0; i < hotkeys.Length; i++)
                {
                    if (!Bound(hotkeys[i], action)) continue;

                    GameObject entry = EntryOf(hotkeys[i].transform, roots[r]);
                    if (entry != null) return entry;
                }
            }

            return null;
        }

        private static bool Bound(global::Hotkey hotkey, PlayerInputAction action)
        {
            if (hotkey.Type == HotkeyType.PlayerAction) return hotkey.PlayerAction == action;
            return hotkey.Type == HotkeyType.ActionName && !string.IsNullOrEmpty(hotkey.ActionName) &&
                   Flatten(hotkey.ActionName) == Flatten(action.ToString());
        }

        private static GameObject EntryOf(Transform bind, Transform root)
        {
            Transform node = bind;
            TMP_Text label = null;
            while (label == null && node.parent != null && node.parent != root)
            {
                node = node.parent;
                label = LabelIn(node, null, null);
            }

            if (label == null) return null;

            string text = label.text.Trim();
            while (node.parent != null && node.parent != root && LabelIn(node.parent, node, text) == null)
            {
                node = node.parent;
            }

            return node.gameObject;
        }

        private static TMP_Text LabelIn(Transform node, Transform skip, string same)
        {
            TMP_Text[] texts = node.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (!IsLabel(text) || (skip != null && text.transform.IsChildOf(skip))) continue;
                if (same != null && text.text.Trim() == same) continue;
                return text;
            }

            return null;
        }

        private static bool IsLabel(TMP_Text text)
        {
            if (text == null || text.text == null || text.GetComponent<global::Hotkey>() != null) return false;
            return text.text.Trim().Length > 2 && text.text.IndexOf("<sprite", StringComparison.Ordinal) < 0;
        }

        private static TMP_Text SquareBind(GameObject template)
        {
            List<Transform> roots = SpectateRoots();
            TMP_Text best = null;
            float bestSkew = MaxSkew;

            for (int r = 0; r < roots.Count; r++)
            {
                global::Hotkey[] hotkeys = roots[r].GetComponentsInChildren<global::Hotkey>(true);
                for (int i = 0; i < hotkeys.Length; i++)
                {
                    TMP_Text text = hotkeys[i].GetComponent<TMP_Text>();
                    float skew = Skew(text);
                    if (skew > bestSkew) continue;

                    bestSkew = skew;
                    best = text;
                }
            }

            return best;
        }

        private static float Skew(TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || text.spriteAsset == null || string.IsNullOrEmpty(text.text)) return float.MaxValue;

            MatchCollection matches = SpriteIndex.Matches(text.text);
            if (matches.Count != 1) return float.MaxValue;

            int index = int.Parse(matches[0].Groups[1].Value);
            List<TMP_SpriteCharacter> characters = text.spriteAsset.spriteCharacterTable;
            if (characters == null || index >= characters.Count || characters[index] == null || characters[index].glyph == null) return float.MaxValue;

            GlyphRect rect = characters[index].glyph.glyphRect;
            if (rect.width <= 0 || rect.height <= 0) return float.MaxValue;

            return Mathf.Abs(Mathf.Log((float)rect.width / rect.height));
        }

        private static void Refresh(int index)
        {
            Host host = Hosts[index];
            GameObject template = host.Find();

            if (template != host.Template)
            {
                host.Template = template;
                host.KeySource = null;
                for (int i = 0; i < Entries.Count; i++)
                {
                    Entries[i].Copies[index] = null;
                    Entries[i].Skipped[index] = false;
                }
            }

            if (host.Template == null) return;

            bool shown = host.Template.activeInHierarchy && ClientRemoteConsoleAccessManager.loggedOn;
            if (shown && host.KeySource == null) host.KeySource = host.Keys(host.Template);

            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                if (entry.Skipped[index]) continue;

                Copy copy = entry.Copies[index];
                if (copy == null || copy.Root == null)
                {
                    copy = Build(host, entry);
                    entry.Copies[index] = copy;
                    if (copy == null)
                    {
                        entry.Skipped[index] = true;
                        continue;
                    }
                }

                bool visible = shown && Owners.Ask(entry.Owner, entry.Label + " visibility", entry.Visible, true);
                if (copy.Root.activeSelf != visible) copy.Root.SetActive(visible);
                if (visible) UpdateKeys(host, entry, copy);
            }
        }

        private static TMP_Text FirstBind(GameObject root)
        {
            if (root == null) return null;

            global::Hotkey[] hotkeys = root.GetComponentsInChildren<global::Hotkey>(true);
            for (int i = 0; i < hotkeys.Length; i++)
            {
                TMP_Text text = hotkeys[i].GetComponent<TMP_Text>();
                if (text != null) return text;
            }

            return null;
        }

        private static Copy Build(Host host, Entry entry)
        {
            GameObject clone = UnityEngine.Object.Instantiate(host.Template, host.Template.transform.parent);
            clone.name = Prefix + entry.Label;

            List<TMP_Text> binds = new List<TMP_Text>();
            global::Hotkey[] hotkeys = clone.GetComponentsInChildren<global::Hotkey>(true);
            for (int i = 0; i < hotkeys.Length; i++)
            {
                TMP_Text text = hotkeys[i].GetComponent<TMP_Text>();
                if (text != null) binds.Add(text);
                UnityEngine.Object.DestroyImmediate(hotkeys[i]);
            }

            UiClone.Strip(clone);

            TMP_Text title = null;
            TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (binds.Contains(texts[i]) || texts[i].text == null || texts[i].text.Trim().Length <= 2) continue;
                title = texts[i];
                break;
            }

            TMP_Text bind = (binds.Count > 0) ? binds[0] : null;
            if (title == null || bind == null)
            {
                Log.Warn("the " + host.Name + " panel's key entry has no label or key to copy, so hint '" + entry.Label + "' was skipped there.");
                Probe.Dump(host.Template.transform, 4, host.Name + " key entry");
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != title && texts[i] != bind) Collapse(clone.transform, texts[i].transform, title.transform, bind.transform);
            }

            title.gameObject.SetActive(true);
            bind.gameObject.SetActive(true);
            title.text = entry.Label;

            Place(host, clone);

            Copy copy = new Copy();
            copy.Root = clone;
            copy.Bind = bind;
            clone.SetActive(false);
            return copy;
        }

        private static void Place(Host host, GameObject clone)
        {
            Transform own = clone.transform;
            Transform parent = own.parent;

            if (host.AtEnd) own.SetAsLastSibling();
            else own.SetSiblingIndex(host.Template.transform.GetSiblingIndex() + 1);

            if (parent == null || parent.GetComponent<LayoutGroup>() != null) return;

            Transform last = null;
            Transform before = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == own) continue;

                if (last == null || child.localPosition.x > last.localPosition.x)
                {
                    before = last;
                    last = child;
                }
                else if (before == null || child.localPosition.x > before.localPosition.x)
                {
                    before = child;
                }
            }

            if (last == null) return;

            Vector3 step = (before == null) ? new Vector3(Width(last) + LooseGap, 0f, 0f) : last.localPosition - before.localPosition;
            if (step.x <= 0f) step = new Vector3(Width(last) + LooseGap, 0f, 0f);
            own.localPosition = last.localPosition + step;

            Log.Info("the " + host.Name + " key bar has no layout group, so hints are placed by hand.");
            Probe.Dump(parent, 2, host.Name + " key bar");
        }

        private static float Width(Transform node)
        {
            RectTransform rect = node as RectTransform;
            return (rect == null) ? 0f : rect.rect.width;
        }

        private static void Collapse(Transform root, Transform node, Transform title, Transform bind)
        {
            Transform top = node;
            while (top.parent != null && top.parent != root && !title.IsChildOf(top.parent) && !bind.IsChildOf(top.parent))
            {
                top = top.parent;
            }

            if (title.IsChildOf(top) || bind.IsChildOf(top)) return;
            top.gameObject.SetActive(false);
        }

        private static void UpdateKeys(Host host, Entry entry, Copy copy)
        {
            KeyCode[] keys = null;
            Owners.Run(entry.Owner, entry.Label + " keys", delegate { keys = entry.Keys(); });
            if (keys == null) keys = new KeyCode[0];

            string pattern = (host.KeySource == null) ? string.Empty : host.KeySource.text;
            string signature = pattern + "|" + string.Join(",", Array.ConvertAll(keys, Name));
            if (signature == copy.Signature) return;
            copy.Signature = signature;

            TMP_SpriteAsset icons = (host.KeySource == null) ? null : host.KeySource.spriteAsset;
            Match source = SpriteIndex.Match(pattern);
            int sourceIndex = source.Success ? int.Parse(source.Groups[1].Value) : -1;

            StringBuilder text = new StringBuilder();
            bool usedIcons = false;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) text.Append(" / ");

                string token = Token(icons, sourceIndex, pattern, keys[i]);
                if (token == null)
                {
                    text.Append(Character(keys[i]));
                    continue;
                }

                text.Append(token);
                usedIcons = true;
            }

            if (usedIcons) copy.Bind.spriteAsset = icons;
            copy.Bind.text = text.ToString();
        }

        private static string Token(TMP_SpriteAsset icons, int sourceIndex, string pattern, KeyCode key)
        {
            if (icons == null || sourceIndex < 0) return null;

            int glyph = Glyph(icons, key);
            if (glyph >= 0) return SpriteIndex.Replace(pattern, "<sprite index=" + glyph, 1);

            string drawn = KeyCaps.Name(icons, sourceIndex, key);
            return (drawn == null) ? null : SpriteIndex.Replace(pattern, "<sprite name=\"" + drawn + "\"", 1);
        }

        private static int Glyph(TMP_SpriteAsset icons, KeyCode key)
        {
            List<TMP_SpriteCharacter> characters = icons.spriteCharacterTable;
            if (characters == null) return -1;

            string wanted = Flatten(key.ToString());
            string symbol = Flatten(Character(key));

            for (int i = 0; i < characters.Count; i++)
            {
                TMP_SpriteCharacter character = characters[i];
                if (character == null || string.IsNullOrEmpty(character.name)) continue;

                string name = Flatten(character.name);
                if (name.EndsWith(wanted, StringComparison.Ordinal) || name == symbol) return i;
            }

            return -1;
        }

        private static string Flatten(string text)
        {
            return text.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Name(KeyCode key)
        {
            return key.ToString();
        }

        private static string Character(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.BackQuote: return "`";
                default: return key.ToString();
            }
        }
    }
}
