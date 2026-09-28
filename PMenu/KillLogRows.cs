using System;
using HarmonyLib;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RyLib
{
    internal sealed class KillLogRowClick : MonoBehaviour, IPointerClickHandler
    {
        internal UIAdminPlayerKillLogPanelRow Row;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || Row == null) return;

            try
            {
                KillLogRows.Toggle(Row);
            }
            catch (Exception error)
            {
                Log.Error("the kill log actions could not be opened: " + error.Message);
            }
        }
    }

    internal static class KillLogRows
    {
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 36f;
        private const float Spacing = 6f;
        private const float Padding = 10f;
        private const float TitleHeight = 26f;
        private const float ArmSeconds = 4f;
        private const int Columns = 3;

        private static readonly Color PanelColour = new Color(0.04f, 0.05f, 0.06f, 0.94f);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private sealed class Half
        {
            public TMP_Text Title;
            public RectTransform Grid;
            public int Buttons;
        }

        private static KillLogInfo _open;
        private static UIAdminPlayerKillLogPanel _log;
        private static GameObject _panel;
        private static LayoutElement _size;
        private static Half _killer;
        private static Half _victim;
        private static int _armedPlayer = -1;
        private static string _armedAction;
        private static float _armedUntil;

        internal static void Attach(UIAdminPlayerKillLogPanelRow row)
        {
            if (row == null || !KillLog.Requested || row.GetComponent<KillLogRowClick>() != null) return;

            if (row.GetComponent<Graphic>() == null)
            {
                Image catcher = row.gameObject.AddComponent<Image>();
                catcher.color = Clear;
            }

            row.gameObject.AddComponent<KillLogRowClick>().Row = row;
        }

        internal static void Toggle(UIAdminPlayerKillLogPanelRow row)
        {
            if (!ClientRemoteConsoleAccessManager.loggedOn || !KillLog.ActionsOn) return;

            KillLogInfo info = row.CurrentInfo;
            if (info == null) return;

            if (_open == info && _panel != null && _panel.activeSelf)
            {
                Close();
                return;
            }

            _log = row.GetComponentInParent<UIAdminPlayerKillLogPanel>();
            _open = info;
            _armedPlayer = -1;

            if (!Ensure(row))
            {
                _open = null;
                return;
            }

            Fill(info);
            Place();
        }

        internal static void Place()
        {
            if (_panel == null) return;

            UIAdminPlayerKillLogPanelRow row = (_open == null || !KillLog.ActionsOn) ? null : FindRow(_open);
            if (row == null || !row.gameObject.activeSelf)
            {
                Hide();
                return;
            }

            Transform parent = row.transform.parent;
            if (_panel.transform.parent != parent) _panel.transform.SetParent(parent, false);

            int rowIndex = row.transform.GetSiblingIndex();
            int panelIndex = _panel.transform.GetSiblingIndex();
            _panel.transform.SetSiblingIndex((panelIndex < rowIndex) ? rowIndex : rowIndex + 1);

            if (!_panel.activeSelf) _panel.SetActive(true);
        }

        internal static void Close()
        {
            _open = null;
            _armedPlayer = -1;
            Hide();
        }

        private static void Hide()
        {
            if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
        }

        private static UIAdminPlayerKillLogPanelRow FindRow(KillLogInfo info)
        {
            if (_log == null || _log.rowsRootTransform == null) return null;

            Transform root = _log.rowsRootTransform;
            for (int i = 0; i < root.childCount; i++)
            {
                UIAdminPlayerKillLogPanelRow row = root.GetChild(i).GetComponent<UIAdminPlayerKillLogPanelRow>();
                if (row != null && row.CurrentInfo == info) return row;
            }

            return null;
        }

        private static bool Ensure(UIAdminPlayerKillLogPanelRow row)
        {
            if (_panel != null) return true;
            if (Menu.ControlTemplate == null) return false;

            GameObject panel = new GameObject("RyLib_KillLogActions", typeof(RectTransform));
            panel.transform.SetParent(row.transform.parent, false);

            RectTransform rect = (RectTransform)panel.transform;
            RectTransform rowRect = (RectTransform)row.transform;
            rect.anchorMin = rowRect.anchorMin;
            rect.anchorMax = rowRect.anchorMax;
            rect.pivot = rowRect.pivot;
            rect.sizeDelta = new Vector2(rowRect.sizeDelta.x, rect.sizeDelta.y);

            Image back = panel.AddComponent<Image>();
            back.color = PanelColour;

            _size = panel.AddComponent<LayoutElement>();
            _size.flexibleHeight = 0f;
            _size.flexibleWidth = 1f;

            HorizontalLayoutGroup halves = panel.AddComponent<HorizontalLayoutGroup>();
            halves.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
            halves.spacing = 20f;
            halves.childAlignment = TextAnchor.UpperLeft;
            halves.childControlWidth = true;
            halves.childControlHeight = true;
            halves.childForceExpandWidth = true;
            halves.childForceExpandHeight = true;

            _killer = BuildHalf(rect, "Killer");
            _victim = BuildHalf(rect, "Victim");

            _panel = panel;
            panel.SetActive(false);
            return true;
        }

        private static Half BuildHalf(RectTransform parent, string name)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            VerticalLayoutGroup stack = host.AddComponent<VerticalLayoutGroup>();
            stack.spacing = Spacing;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            GameObject titleHost = new GameObject("Title", typeof(RectTransform));
            titleHost.transform.SetParent(host.transform, false);
            TextMeshProUGUI title = titleHost.AddComponent<TextMeshProUGUI>();
            if (Menu.FontAsset != null) title.font = Menu.FontAsset;
            title.fontSize = 18f;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Overflow;
            title.raycastTarget = false;

            LayoutElement titleSize = titleHost.AddComponent<LayoutElement>();
            titleSize.minHeight = TitleHeight;
            titleSize.preferredHeight = TitleHeight;

            GameObject gridHost = new GameObject("Actions", typeof(RectTransform));
            gridHost.transform.SetParent(host.transform, false);

            GridLayoutGroup grid = gridHost.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(ButtonWidth, ButtonHeight);
            grid.spacing = new Vector2(Spacing, Spacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;

            Half half = new Half();
            half.Title = title;
            half.Grid = (RectTransform)gridHost.transform;
            return half;
        }

        private static void Fill(KillLogInfo info)
        {
            ClientRoundPlayer killer = info.KillerIsVictim ? null : info.Killer;
            SetHalf(_killer, info.KillerIsVictim ? "Killer: suicide" : "Killer: " + Name(info.KillerName, killer), killer);
            SetHalf(_victim, "Victim: " + Name(info.VictimName, info.Victim), info.Victim);

            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(_killer.Buttons, _victim.Buttons) / (float)Columns));
            float height = Padding * 2f + TitleHeight + Spacing + rows * ButtonHeight + (rows - 1) * Spacing;

            _size.minHeight = height;
            _size.preferredHeight = height;
            RectTransform rect = (RectTransform)_panel.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static string Name(string shown, ClientRoundPlayer player)
        {
            if (!string.IsNullOrEmpty(shown)) return shown;
            return (player == null) ? "none" : "Player " + player.NetworkPlayerID;
        }

        private static void SetHalf(Half half, string title, ClientRoundPlayer player)
        {
            for (int i = half.Grid.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(half.Grid.GetChild(i).gameObject);

            half.Title.text = title;
            half.Buttons = 0;
            if (player == null) return;

            int id = player.NetworkPlayerID;
            Add(half, "Spectate", StockIcon.Unspawned, delegate { PlayerActions.Spectate(id); });
            Add(half, "Go To", StockIcon.Maps, delegate { PlayerActions.GoTo(id); });
            Add(half, "Bring", StockIcon.Players, delegate { PlayerActions.Bring(id); });
            Confirmed(half, id, "Slay", StockIcon.Skull, PlayerActions.Slay);
            Add(half, "Revive", "hui-p-revive", delegate { PlayerActions.Revive(id); });
            Add(half, "Heal", "hui-p-heal", delegate { PlayerActions.Heal(id); });
            Confirmed(half, id, "Slap", "hui-p-slap", PlayerActions.Slap);
            Confirmed(half, id, "Kick", "hui-p-kick", PlayerActions.Kick);

            for (int i = 0; i < PlayerRow.ActionCount; i++)
            {
                if (!PlayerRow.ActionVisible(i)) continue;

                int index = i;
                Add(half, PlayerRow.ActionLabel(i), StockIcon.Admin, delegate { PlayerRow.RunAction(index, id); }, PlayerRow.ActionOwner(i));
            }
        }

        private static void Add(Half half, string label, string icon, Action click, string owner = null)
        {
            Make(half, Controls.Button(owner ?? RyLibPlugin.Guid, label, click, icon));
        }

        private static void Confirmed(Half half, int id, string action, string icon, Action<int> run)
        {
            ControlSpec spec = Controls.Button(RyLibPlugin.Guid, action, delegate
            {
                if (Armed(id, action))
                {
                    _armedPlayer = -1;
                    run(id);
                    return;
                }

                _armedPlayer = id;
                _armedAction = action;
                _armedUntil = Time.unscaledTime + ArmSeconds;
            }, icon);

            spec.Text = delegate { return Armed(id, action) ? "Confirm " + action : action; };
            Make(half, spec);
        }

        private static bool Armed(int id, string action)
        {
            return _armedPlayer == id && _armedAction == action && Time.unscaledTime <= _armedUntil;
        }

        private static void Make(Half half, ControlSpec spec)
        {
            Control control = Controls.Make(spec, half.Grid, Menu.ControlTemplate);
            if (control == null) return;

            half.Buttons++;
            if (control.Label == null) return;

            control.Label.enableWordWrapping = false;
            control.Label.fontSizeMax = control.Label.fontSize;
            control.Label.fontSizeMin = 9f;
            control.Label.enableAutoSizing = true;
        }
    }

    [HarmonyPatch(typeof(UIAdminPlayerKillLogPanelRow), nameof(UIAdminPlayerKillLogPanelRow.Setup))]
    internal static class KillLogRowSetupPatch
    {
        private static void Postfix(UIAdminPlayerKillLogPanelRow __instance)
        {
            try
            {
                KillLogRows.Attach(__instance);
            }
            catch (Exception error)
            {
                Log.Error("kill log row actions could not be attached: " + error.Message);
            }
        }
    }

    [HarmonyPatch(typeof(UIAdminPlayerKillLogPanel), "PopulateKillLogRows")]
    internal static class KillLogPopulatePatch
    {
        private static void Postfix()
        {
            KillLogRows.Place();
        }
    }

    [HarmonyPatch(typeof(UIAdminPlayerKillLogPanel), "UpdateFilteredPlayers")]
    internal static class KillLogFilterPlacePatch
    {
        private static void Postfix()
        {
            KillLogRows.Place();
        }
    }

    [HarmonyPatch(typeof(UIAdminPlayerKillLogPanel), nameof(UIAdminPlayerKillLogPanel._ResetObject))]
    internal static class KillLogResetClosePatch
    {
        private static void Postfix()
        {
            KillLogRows.Close();
        }
    }

    [HarmonyPatch(typeof(UIAdminPlayerKillLogPanel), nameof(UIAdminPlayerKillLogPanel._ToggleShowing))]
    internal static class KillLogHideClosePatch
    {
        private static void Postfix(bool show)
        {
            if (!show) KillLogRows.Close();
        }
    }
}
