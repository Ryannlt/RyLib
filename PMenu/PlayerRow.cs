using System;
using System.Collections.Generic;
using HarmonyLib;
using HoldfastGame;
using UnityEngine;
using UnityEngine.UI;

namespace RyLib
{
    public enum StockRowButton
    {
        Slay,
        Revive,
        Slap,
        Heal,
        Bring,
        GoTo,
        Message,
        Kick,
        Ban
    }

    public static class PlayerRow
    {
        private const string Prefix = "RyLib_RowAction_";
        private const float Inset = 10f;
        private const float RowGap = 25f;
        private const float RefreshSeconds = 0.25f;

        private sealed class Entry
        {
            public string Owner;
            public string Label;
            public StockRowButton Icon;
            public Action<int> OnClick;
            public Func<bool> Visible;
        }

        private struct Column
        {
            public float Min;
            public float Max;
        }

        private sealed class Built
        {
            public UIRoundPlayersControlPanelItemRow Row;
            public RectTransform Actions;
            public RectTransform Admin;
            public RectTransform Cell;
            public LayoutElement Element;
            public List<Column> Columns;
            public GameObject[] Buttons;
            public float Height;
            public float Pitch;
            public float Bottom;
            public float ActionsHeight;
            public float AdminY;
            public float CellMin;
            public float CellPreferred;
            public float CellHeight;
            public int Rows = -1;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly List<Built> BuiltRows = new List<Built>();
        private static readonly List<float> Heights = new List<float>();

        private static bool[] _visible = new bool[0];
        private static float _nextRefresh;

        public static bool AddAction(string owner, string label, StockRowButton icon, Action<int> onClick,
            Func<bool> visible = null)
        {
            if (!Owners.Valid(owner, "PlayerRow.AddAction")) return false;

            if (string.IsNullOrEmpty(label) || onClick == null)
            {
                Log.Warn(owner + ": PlayerRow.AddAction needs a label and a click handler.");
                return false;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (!Owners.Same(Entries[i].Owner, owner)) continue;
                if (!string.Equals(Entries[i].Label, label, StringComparison.OrdinalIgnoreCase)) continue;

                Log.Warn(owner + ": row action '" + label + "' is already registered, so the second one was ignored.");
                return false;
            }

            Entry entry = new Entry();
            entry.Owner = owner;
            entry.Label = label;
            entry.Icon = icon;
            entry.OnClick = onClick;
            entry.Visible = visible;
            Entries.Add(entry);

            Array.Resize(ref _visible, Entries.Count);
            _visible[Entries.Count - 1] = Owners.Ask(owner, label + " visibility", visible, true);

            Log.Info(owner + " added row action '" + label + "'.");
            return true;
        }

        internal static int ActionCount
        {
            get { return Entries.Count; }
        }

        internal static bool ActionVisible(int index)
        {
            Entry entry = Entries[index];
            return Owners.Ask(entry.Owner, entry.Label + " visibility", entry.Visible, true);
        }

        internal static string ActionOwner(int index)
        {
            return Entries[index].Owner;
        }

        internal static string ActionLabel(int index)
        {
            return Entries[index].Label;
        }

        internal static void RunAction(int index, int playerId)
        {
            Entry entry = Entries[index];
            Owners.Run(entry.Owner, entry.Label, delegate { entry.OnClick(playerId); });
        }

        internal static void Tick()
        {
            if (BuiltRows.Count == 0 || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            bool changed = false;
            for (int i = 0; i < Entries.Count; i++)
            {
                bool visible = Owners.Ask(Entries[i].Owner, Entries[i].Label + " visibility", Entries[i].Visible, true);
                if (visible == _visible[i]) continue;

                _visible[i] = visible;
                changed = true;
            }

            for (int i = BuiltRows.Count - 1; i >= 0; i--)
            {
                if (BuiltRows[i].Row == null)
                {
                    BuiltRows.RemoveAt(i);
                    continue;
                }

                if (changed) Layout(BuiltRows[i]);
            }
        }

        internal static void Build(UIRoundPlayersControlPanelItemRow row)
        {
            if (Entries.Count == 0 || row == null) return;
            if (row.bringButton == null || row.kickButton == null || row.banButton == null) return;

            RectTransform actions = row.bringButton.transform.parent as RectTransform;
            RectTransform admin = row.kickButton.transform.parent as RectTransform;
            if (actions == null || admin == null || actions == admin) return;
            if (actions.Find(Prefix + "0") != null) return;

            List<Column> columns = ReadColumns(actions);
            if (columns.Count < 2)
            {
                Log.Warn("the player actions row has no readable columns, so row actions were skipped.");
                return;
            }

            RectTransform template = row.bringButton.transform as RectTransform;

            Built built = new Built();
            built.Row = row;
            built.Actions = actions;
            built.Admin = admin;
            built.Columns = columns;
            built.Height = template.sizeDelta.y;
            built.Pitch = Pitch(admin, built.Height);
            built.Bottom = Bottom(actions);
            built.ActionsHeight = actions.sizeDelta.y;
            built.AdminY = admin.anchoredPosition.y;
            built.Cell = actions.parent as RectTransform;
            built.Element = (built.Cell == null) ? null : built.Cell.GetComponent<LayoutElement>();
            built.CellHeight = (built.Cell == null) ? 0f : built.Cell.rect.height;
            if (built.Element != null)
            {
                built.CellMin = built.Element.minHeight;
                built.CellPreferred = built.Element.preferredHeight;
            }

            built.Buttons = new GameObject[Entries.Count];
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];

                Button button = UiClone.Make(row.banButton, actions, Prefix + i);
                if (button == null) continue;

                UiClone.SetLabel(button.gameObject, entry.Label);
                UiClone.CopyIcon(button.gameObject, Stock(row, entry.Icon));

                button.onClick.AddListener(delegate { Click(row, entry); });
                built.Buttons[i] = button.gameObject;
            }

            BuiltRows.Add(built);
            Layout(built);
        }

        private static void Click(UIRoundPlayersControlPanelItemRow row, Entry entry)
        {
            ClientRoundPlayer player = (row == null) ? null : row.clientRoundPlayer;
            if (player == null) return;

            int playerId = player.NetworkPlayerID;
            Owners.Run(entry.Owner, entry.Label, delegate { entry.OnClick(playerId); });
        }

        private static void Layout(Built built)
        {
            int count = 0;
            for (int i = 0; i < built.Buttons.Length; i++)
            {
                if (built.Buttons[i] != null && Visible(i)) count++;
            }

            int slot = 0;
            for (int i = 0; i < built.Buttons.Length; i++)
            {
                GameObject button = built.Buttons[i];
                if (button == null) continue;

                bool show = Visible(i);
                if (show) Place((RectTransform)button.transform, built, slot++, count);
                if (button.activeSelf != show) button.SetActive(show);
            }

            int columns = built.Columns.Count;
            int rows = (count + columns - 1) / columns;
            if (rows == built.Rows) return;
            built.Rows = rows;

            float growth = built.Pitch * rows;
            built.Actions.sizeDelta = new Vector2(built.Actions.sizeDelta.x, built.ActionsHeight + growth);
            built.Admin.anchoredPosition = new Vector2(built.Admin.anchoredPosition.x, built.AdminY - growth);

            if (built.Element != null)
            {
                if (rows == 0)
                {
                    built.Element.minHeight = built.CellMin;
                    built.Element.preferredHeight = built.CellPreferred;
                    return;
                }

                if (built.CellMin > 0f) built.Element.minHeight = built.CellMin + growth;

                if (built.CellPreferred > 0f) built.Element.preferredHeight = built.CellPreferred + growth;
                else if (built.CellMin <= 0f) built.Element.preferredHeight = built.CellHeight + growth;
                return;
            }

            if (built.Cell != null)
            {
                built.Cell.sizeDelta = new Vector2(built.Cell.sizeDelta.x, built.CellHeight + growth);
            }
        }

        private static bool Visible(int index)
        {
            return index < _visible.Length && _visible[index];
        }

        private static void Place(RectTransform rect, Built built, int slot, int count)
        {
            int columns = built.Columns.Count;
            int row = slot / columns;
            int col = slot % columns;
            bool last = col == columns - 1 || slot == count - 1;

            Column column = built.Columns[col];
            float left = (col == 0) ? 0f : Inset;
            float right = last ? 0f : Inset;

            rect.anchorMin = new Vector2(column.Min, 1f);
            rect.anchorMax = new Vector2(column.Max, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(-(left + right), built.Height);
            rect.anchoredPosition = new Vector2(left, built.Bottom - built.Pitch * (row + 1));
            rect.localScale = Vector3.one;
        }

        private static Component Stock(UIRoundPlayersControlPanelItemRow row, StockRowButton icon)
        {
            switch (icon)
            {
                case StockRowButton.Slay: return row.slayButton;
                case StockRowButton.Revive: return row.reviveButton;
                case StockRowButton.Slap: return row.slapButton;
                case StockRowButton.Heal: return row.healButton;
                case StockRowButton.Bring: return row.bringButton;
                case StockRowButton.GoTo: return row.goToButton;
                case StockRowButton.Message: return row.messageButton;
                case StockRowButton.Kick: return row.kickButton;
                default: return row.banButton;
            }
        }

        private static List<Column> ReadColumns(RectTransform parent)
        {
            List<Column> columns = new List<Column>();

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                if (child.GetComponent<Button>() == null) continue;

                RectTransform rect = child as RectTransform;
                if (rect == null) continue;

                Column column;
                column.Min = rect.anchorMin.x;
                column.Max = rect.anchorMax.x;

                bool known = false;
                for (int j = 0; j < columns.Count; j++)
                {
                    if (Math.Abs(columns[j].Min - column.Min) > 0.001f) continue;
                    known = true;
                    break;
                }

                if (!known) columns.Add(column);
            }

            columns.Sort(delegate(Column left, Column right) { return left.Min.CompareTo(right.Min); });
            return columns;
        }

        private static float Bottom(RectTransform parent)
        {
            float bottom = 0f;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                if (child.GetComponent<Button>() == null) continue;

                RectTransform rect = child as RectTransform;
                if (rect == null) continue;

                if (rect.anchoredPosition.y < bottom) bottom = rect.anchoredPosition.y;
            }

            return bottom;
        }

        private static float Pitch(RectTransform admin, float height)
        {
            Heights.Clear();

            for (int i = 0; i < admin.childCount; i++)
            {
                Transform child = admin.GetChild(i);
                if (child.GetComponent<Button>() == null) continue;

                RectTransform rect = child as RectTransform;
                if (rect == null) continue;

                float y = rect.anchoredPosition.y;

                bool known = false;
                for (int j = 0; j < Heights.Count; j++)
                {
                    if (Math.Abs(Heights[j] - y) > 1f) continue;
                    known = true;
                    break;
                }

                if (!known) Heights.Add(y);
            }

            if (Heights.Count < 2) return height + RowGap;

            Heights.Sort();
            return Heights[Heights.Count - 1] - Heights[Heights.Count - 2];
        }
    }

    [HarmonyPatch(typeof(UIRoundPlayersControlPanelItemRow), "Initialize")]
    internal static class PlayerRowInitializePatch
    {
        private static void Postfix(UIRoundPlayersControlPanelItemRow __instance)
        {
            try
            {
                PlayerRow.Build(__instance);
            }
            catch (Exception error)
            {
                Log.Error("row actions could not be added: " + error.Message);
            }
        }
    }
}
