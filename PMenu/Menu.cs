using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RyLib
{
    internal static class Menu
    {
        private const float RefreshSeconds = 0.25f;
        private const int FirstTabType = 100;
        private const float StripSpacing = 70f;
        private const float StripOffset = -25f;
        private const float HeaderFont = 18f;
        private const float MinHeaderFont = 12f;
        private const float HeaderPadding = 8f;
        private const float NavWidth = 240f;
        private const float NavGap = 20f;
        private const float NavButtonHeight = 48f;

        private static readonly PMenuTabType[] StockOrder =
        {
            PMenuTabType.RoundPlayers, PMenuTabType.Rules, PMenuTabType.QuickAccess,
            PMenuTabType.MapRotations, PMenuTabType.PlayerKillLog, PMenuTabType.ArtilleryPriority
        };

        internal static readonly List<HeaderTab> Headers = new List<HeaderTab>();

        private static readonly AccessTools.FieldRef<PMenuPanel, Dictionary<PMenuTabType, PMenuPanelTabBase>> TabsField =
            AccessTools.FieldRefAccess<PMenuPanel, Dictionary<PMenuTabType, PMenuPanelTabBase>>("tabs");

        private static readonly MethodInfo SwitchTabMethod =
            AccessTools.Method(typeof(PMenuPanel), "SwitchTab", new[] { typeof(PMenuTabType) });

        private static readonly Dictionary<PMenuTabType, PMenuTabButton> StockHeaders =
            new Dictionary<PMenuTabType, PMenuTabButton>();

        private static readonly List<HeaderTab> Visible = new List<HeaderTab>();
        private static readonly List<Page> VisiblePages = new List<Page>();
        private static readonly Dictionary<TMP_Text, Vector2> LabelHomes = new Dictionary<TMP_Text, Vector2>();

        private struct Anchoring
        {
            public Vector2 Min;
            public Vector2 Max;
            public Vector2 Size;
            public Vector2 Position;
        }

        private static PMenuPanel _panel;
        private static bool _dirty = true;
        private static bool _failed;
        private static bool _refreshFailed;
        private static float _nextRefresh;
        private static int _nextType = FirstTabType;
        private static string _layout;

        private static RectTransform _banner;
        private static RectTransform _players;
        private static RectTransform _rules;
        private static RectTransform _adminTabs;
        private static Anchoring _playersHome;
        private static Anchoring _rulesHome;
        private static Anchoring _adminHome;

        private static GameObject _headerTemplate;
        private static GameObject _stripTemplate;
        private static GameObject _childTemplate;
        private static GameObject _headerBackground;
        private static GameObject _scrollView;
        private static GameObject _controlTemplate;
        private static Canvas _panelCanvas;
        private static TMP_FontAsset _font;

        internal static Transform SpriteRoot
        {
            get { return (_panel == null) ? null : _panel.transform; }
        }

        internal static GameObject ControlTemplate
        {
            get { return _controlTemplate; }
        }

        internal static Canvas PanelCanvas
        {
            get { return _panelCanvas; }
        }

        internal static TMP_FontAsset FontAsset
        {
            get { return _font; }
        }

        internal static void MarkDirty()
        {
            _dirty = true;
        }

        internal static HeaderTab FindHeader(string name)
        {
            for (int i = 0; i < Headers.Count; i++)
            {
                if (Owners.Same(Headers[i].Name, name)) return Headers[i];
            }

            return null;
        }

        internal static void Tick()
        {
            PMenuPanel panel = CurrentPanel();
            if (panel != _panel)
            {
                Forget();
                _panel = panel;
                _dirty = true;
                _failed = false;
                _refreshFailed = false;
            }

            if (_panel == null) return;

            if (_dirty && !_failed && Ready(_panel))
            {
                _dirty = false;

                try
                {
                    Build();
                }
                catch (Exception error)
                {
                    _failed = true;
                    Log.Error("P menu extensions could not be built: " + error);
                }
            }

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            try
            {
                Refresh();
            }
            catch (Exception error)
            {
                if (_refreshFailed) return;
                _refreshFailed = true;
                Log.Error("P menu refresh failed: " + error);
            }
        }

        private static PMenuPanel CurrentPanel()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null) return null;

            PMenuPanel panel = client.pMenuPanel;
            return (panel == null) ? null : panel;
        }

        private static bool Ready(PMenuPanel panel)
        {
            if (panel.tabButtonsRootTransform == null || panel.tabsRootTransform == null) return false;

            Dictionary<PMenuTabType, PMenuPanelTabBase> tabs = TabsField(panel);
            return tabs != null && tabs.Count > 0;
        }

        private static void Forget()
        {
            for (int i = 0; i < Headers.Count; i++)
            {
                HeaderTab header = Headers[i];
                header.Root = null;
                header.Rect = null;
                header.Switch = null;
                header.Label = null;
                header.Strip = null;
                header.StripGroup = null;

                for (int j = 0; j < header.Pages.Count; j++)
                {
                    Page page = header.Pages[j];
                    page.Root = null;
                    page.Tab = null;
                    page.Button = null;
                    page.Switch = null;
                    page.Content = null;
                    page.TitleTemplate = null;
                    page.Map = null;
                    page.Nav = null;
                    page.Scroller = null;

                    for (int k = 0; k < page.Sections.Count; k++) page.Sections[k].Built = false;

                    for (int k = 0; k < page.ModTabs.Count; k++)
                    {
                        ModTab tab = page.ModTabs[k];
                        tab.Content = null;
                        tab.Button = null;
                        for (int m = 0; m < tab.Sections.Count; m++) tab.Sections[m].Built = false;
                    }
                }
            }

            PlayersBar.Forget();
            StockHeaders.Clear();
            LabelHomes.Clear();

            _banner = null;
            _players = null;
            _rules = null;
            _adminTabs = null;
            _headerTemplate = null;
            _stripTemplate = null;
            _childTemplate = null;
            _headerBackground = null;
            _scrollView = null;
            _controlTemplate = null;
            _panelCanvas = null;
            _layout = null;
        }

        private static void Build()
        {
            if (_banner == null) Capture();

            if (_controlTemplate != null) PlayersBar.Build(_controlTemplate);

            for (int i = 0; i < Headers.Count; i++)
            {
                HeaderTab header = Headers[i];
                if (header.Root == null && !BuildHeader(header)) continue;

                for (int j = 0; j < header.Pages.Count; j++)
                {
                    Page page = header.Pages[j];
                    if (page.Root == null && !BuildPage(header, page)) continue;

                    if (page.Kind == PageKind.Mods) BuildModTabs(page);
                    else BuildSections(page);
                }
            }

            _layout = null;
            Refresh();
        }

        private static void Capture()
        {
            _banner = _panel.tabButtonsRootTransform;

            for (Transform node = _banner; node != null; node = node.parent)
            {
                Canvas canvas = node.GetComponent<Canvas>();
                if (canvas != null) _panelCanvas = canvas;
            }

            TMP_Text anyText = _panel.tabsRootTransform.GetComponentInChildren<TMP_Text>(true);
            if (anyText != null && anyText.font != null) _font = anyText.font;

            PMenuTabButton[] headers = _banner.GetComponentsInChildren<PMenuTabButton>(true);
            for (int i = 0; i < headers.Length; i++)
            {
                PMenuTabButton header = headers[i];
                StockHeaders[header.tabType] = header;

                if (header.tabType == PMenuTabType.RoundPlayers) _players = header.transform as RectTransform;
                else if (header.tabType == PMenuTabType.Rules) _rules = header.transform as RectTransform;
                else if (header.tabType == PMenuTabType.QuickAccess) _adminTabs = header.transform.parent as RectTransform;

                bool off = header.toggle != null && !header.toggle.isOn;
                if (off && (_headerTemplate == null || header.tabType == PMenuTabType.Rules)) _headerTemplate = header.gameObject;
            }

            if (_players != null) _playersHome = Save(_players);
            if (_rules != null) _rulesHome = Save(_rules);
            if (_adminTabs != null && _adminTabs != _banner) _adminHome = Save(_adminTabs);
            else _adminTabs = null;

            if (_panel.playerChildTabButtonsRoot != null)
            {
                _stripTemplate = _panel.playerChildTabButtonsRoot;

                PMenuChildTabButton[] children = _stripTemplate.GetComponentsInChildren<PMenuChildTabButton>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].toggle != null && children[i].toggle.isOn) continue;
                    _childTemplate = children[i].gameObject;
                    break;
                }
            }

            UIRoundPlayersControlPanel players = _panel.tabsRootTransform.GetComponentInChildren<UIRoundPlayersControlPanel>(true);
            if (players != null && players.adminRaygunToggleButton != null)
            {
                Transform raygun = players.adminRaygunToggleButton.transform;
                _controlTemplate = (raygun.parent != null) ? raygun.parent.gameObject : raygun.gameObject;
                Controls.SetPalette(players);
            }

            UIAdminPlayerKillLogPanel killLog = _panel.tabsRootTransform.GetComponentInChildren<UIAdminPlayerKillLogPanel>(true);
            if (killLog != null)
            {
                Transform header = killLog.transform.Find("Header Background");
                Transform scroll = killLog.transform.Find("Scroll View");
                _headerBackground = (header == null) ? null : header.gameObject;
                _scrollView = (scroll == null) ? null : scroll.gameObject;
            }

            StringBuilder missing = new StringBuilder();
            if (_headerTemplate == null) missing.Append(" header");
            if (_stripTemplate == null || _childTemplate == null) missing.Append(" child-tab");
            if (_controlTemplate == null) missing.Append(" raygun");
            if (_headerBackground == null || _scrollView == null) missing.Append(" page-frame");
            if (_players == null || _rules == null) missing.Append(" stock-headers");
            if (missing.Length > 0) Log.Warn("P menu templates not found:" + missing + ". Those parts of the UI are skipped.");
        }

        private static bool BuildHeader(HeaderTab header)
        {
            if (_headerTemplate == null || _banner == null) return false;

            GameObject root = UnityEngine.Object.Instantiate(_headerTemplate, _banner);
            root.name = "RyLib_Header_" + header.Name;
            DropTabData(root);
            UiClone.Strip(root);

            Toggle toggle = root.GetComponent<Toggle>();
            if (toggle == null)
            {
                UnityEngine.Object.Destroy(root);
                return false;
            }

            UiClone.Mute(toggle.onValueChanged);
            toggle.SetIsOnWithoutNotify(false);

            TMP_Text label = root.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = header.Name;

            SetIcon(root.transform, header.IconName);

            HeaderTab captured = header;
            toggle.onValueChanged.AddListener(delegate(bool on) { HeaderToggled(captured, on); });

            header.Root = root;
            header.Rect = root.transform as RectTransform;
            header.Switch = toggle;
            header.Label = label;

            if (_stripTemplate != null)
            {
                GameObject strip = UnityEngine.Object.Instantiate(_stripTemplate, _banner);
                strip.name = "RyLib_Strip_" + header.Name;
                for (int i = strip.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(strip.transform.GetChild(i).gameObject);
                }

                ToggleGroup group = strip.GetComponent<ToggleGroup>();
                if (group == null) group = strip.AddComponent<ToggleGroup>();
                group.allowSwitchOff = false;

                strip.SetActive(false);
                header.Strip = strip;
                header.StripGroup = group;
            }

            return true;
        }

        private static bool BuildPage(HeaderTab header, Page page)
        {
            if (_headerBackground == null || _scrollView == null || header.Strip == null || _childTemplate == null) return false;

            if (!page.HasType)
            {
                page.Type = (PMenuTabType)(_nextType++);
                page.HasType = true;
            }

            GameObject button = UnityEngine.Object.Instantiate(_childTemplate, header.Strip.transform);
            button.name = "RyLib_PageButton_" + page.Name;
            DropTabData(button);
            UiClone.Strip(button);

            Toggle toggle = button.GetComponent<Toggle>();
            if (toggle != null)
            {
                UiClone.Mute(toggle.onValueChanged);
                toggle.group = header.StripGroup;
                toggle.SetIsOnWithoutNotify(false);

                Page captured = page;
                toggle.onValueChanged.AddListener(delegate(bool on) { if (on) OpenPage(captured); });
            }

            SetIcon(button.transform, page.IconName);
            button.SetActive(true);

            GameObject root = new GameObject("RyLib_Page_" + header.Name + "_" + page.Name, typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(_panel.tabsRootTransform, false);

            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            root.AddComponent<Canvas>();
            root.AddComponent<GraphicRaycaster>();

            PageTab tab = root.AddComponent<PageTab>();
            tab.TabType = page.Type;
            tab.Page = page;
            tab.ObjectToSelectOnOpen = header.Root;

            GameObject frame = UnityEngine.Object.Instantiate(_headerBackground, root.transform);
            frame.name = "Header Background";
            Transform search = frame.transform.Find("Search");
            TMP_InputField field = null;
            if (search != null && page.Kind == PageKind.Map) field = search.GetComponentInChildren<TMP_InputField>(true);
            if (search != null && field == null) UnityEngine.Object.DestroyImmediate(search.gameObject);
            UiClone.Strip(frame);

            Transform titleObject = frame.transform.Find("Title");
            TMP_Text title = (titleObject == null) ? frame.GetComponentInChildren<TMP_Text>(true) : titleObject.GetComponent<TMP_Text>();
            if (title != null) title.text = page.Name;

            if (page.Kind == PageKind.Map)
            {
                page.Map = new MapView(page, rect, title, field);

                TabsField(_panel)[page.Type] = tab;
                page.Root = root;
                page.Tab = tab;
                page.Button = button;
                page.Switch = toggle;
                page.TitleTemplate = title;
                return true;
            }

            GameObject scroll = UnityEngine.Object.Instantiate(_scrollView, root.transform);
            scroll.name = "Scroll View";
            UiClone.Strip(scroll);

            float left = 25f;
            if (page.Kind == PageKind.Mods)
            {
                GameObject nav = new GameObject("Mod Tabs", typeof(RectTransform));
                nav.transform.SetParent(root.transform, false);

                RectTransform navRect = (RectTransform)nav.transform;
                navRect.anchorMin = new Vector2(0f, 0f);
                navRect.anchorMax = new Vector2(0f, 1f);
                navRect.pivot = new Vector2(0f, 1f);
                navRect.offsetMin = new Vector2(25f, 25f);
                navRect.offsetMax = new Vector2(25f + NavWidth, -135f);

                VerticalLayoutGroup list = nav.AddComponent<VerticalLayoutGroup>();
                list.spacing = 8f;
                list.childAlignment = TextAnchor.UpperLeft;
                list.childControlWidth = true;
                list.childControlHeight = true;
                list.childForceExpandWidth = true;
                list.childForceExpandHeight = false;

                page.Nav = navRect;
                left = 25f + NavWidth + NavGap;
            }

            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(left, 25f);
            scrollRect.offsetMax = new Vector2(-25f, -135f);

            ScrollRect scroller = scroll.GetComponent<ScrollRect>();
            page.Scroller = scroller;
            RectTransform content = (scroller == null) ? null : scroller.content;
            if (content == null)
            {
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(button);
                Log.Warn("the page frame has no scroll content, so page '" + page.Name + "' was skipped.");
                return false;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
            }

            PrepareContent(content);

            TabsField(_panel)[page.Type] = tab;

            page.Root = root;
            page.Tab = tab;
            page.Button = button;
            page.Switch = toggle;
            page.Content = content;
            page.TitleTemplate = title;
            return true;
        }

        private static void PrepareContent(RectTransform content)
        {
            LayoutGroup existing = content.GetComponent<LayoutGroup>();
            if (existing != null && !(existing is VerticalLayoutGroup)) UnityEngine.Object.DestroyImmediate(existing);

            VerticalLayoutGroup column = content.GetComponent<VerticalLayoutGroup>();
            if (column == null) column = content.gameObject.AddComponent<VerticalLayoutGroup>();

            column.padding = new RectOffset(10, 10, 10, 10);
            column.spacing = 20f;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void BuildSections(Page page)
        {
            if (page.Kind != PageKind.Sections || page.Content == null || _controlTemplate == null) return;

            BuildSectionList(page, page.Sections, page.Content, 3);
        }

        private static void BuildModTabs(Page page)
        {
            if (page.Kind != PageKind.Mods || page.Content == null || page.Nav == null || _controlTemplate == null) return;

            for (int i = 0; i < page.ModTabs.Count; i++)
            {
                ModTab tab = page.ModTabs[i];
                if (tab.Content == null)
                {
                    GameObject box = new GameObject("RyLib_ModTab_" + tab.Name, typeof(RectTransform));
                    box.transform.SetParent(page.Content, false);

                    VerticalLayoutGroup stack = box.AddComponent<VerticalLayoutGroup>();
                    stack.spacing = 20f;
                    stack.childAlignment = TextAnchor.UpperLeft;
                    stack.childControlWidth = true;
                    stack.childControlHeight = true;
                    stack.childForceExpandWidth = true;
                    stack.childForceExpandHeight = false;

                    tab.Content = (RectTransform)box.transform;

                    ModTab captured = tab;
                    ControlSpec spec = Controls.Button(tab.Creator, tab.Name, delegate { SelectModTab(page, captured); },
                        tab.IconName ?? StockIcon.Admin);
                    spec.IsOn = delegate { return page.CurrentModTab == captured; };

                    tab.Button = Controls.Make(spec, page.Nav, _controlTemplate);
                    if (tab.Button != null)
                    {
                        LayoutElement size = tab.Button.Root.GetComponent<LayoutElement>();
                        if (size == null) size = tab.Button.Root.AddComponent<LayoutElement>();
                        size.minHeight = NavButtonHeight;
                        size.preferredHeight = NavButtonHeight;
                        size.flexibleHeight = 0f;
                        size.minWidth = -1f;
                        size.preferredWidth = -1f;
                        size.flexibleWidth = 1f;
                    }
                }

                BuildSectionList(page, tab.Sections, tab.Content, 2);
            }

            RefreshModTabs(page);
        }

        private static void SelectModTab(Page page, ModTab tab)
        {
            page.CurrentModTab = tab;
            ShowModTab(page);
        }

        private static void RefreshModTabs(Page page)
        {
            if (page.Kind != PageKind.Mods || page.Nav == null) return;

            ModTab first = null;
            for (int i = 0; i < page.ModTabs.Count; i++)
            {
                ModTab tab = page.ModTabs[i];
                bool shown = tab.Visible;
                if (shown && first == null) first = tab;

                if (tab.Button != null && tab.Button.Root != null && tab.Button.Root.activeSelf != shown) tab.Button.Root.SetActive(shown);
            }

            if (page.CurrentModTab != null && page.CurrentModTab.Visible) return;

            page.CurrentModTab = first;
            ShowModTab(page);
        }

        private static void ShowModTab(Page page)
        {
            for (int i = 0; i < page.ModTabs.Count; i++)
            {
                ModTab tab = page.ModTabs[i];
                bool shown = tab == page.CurrentModTab;
                if (tab.Content != null && tab.Content.gameObject.activeSelf != shown) tab.Content.gameObject.SetActive(shown);
                if (tab.Button != null) Controls.Refresh(tab.Button);
            }

            if (page.Scroller != null) page.Scroller.verticalNormalizedPosition = 1f;
        }

        private static void BuildSectionList(Page page, List<SectionSpec> sections, RectTransform parent, int columns)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                SectionSpec spec = sections[i];
                if (spec.Built) continue;
                spec.Built = true;

                GameObject section = new GameObject("RyLib_Section_" + spec.Owner + "_" + spec.Title, typeof(RectTransform));
                section.transform.SetParent(parent, false);

                VerticalLayoutGroup stack = section.AddComponent<VerticalLayoutGroup>();
                stack.spacing = 10f;
                stack.childAlignment = TextAnchor.UpperLeft;
                stack.childControlWidth = true;
                stack.childControlHeight = true;
                stack.childForceExpandWidth = true;
                stack.childForceExpandHeight = false;

                if (page.TitleTemplate != null)
                {
                    TMP_Text heading = UnityEngine.Object.Instantiate(page.TitleTemplate, section.transform);
                    heading.name = "Heading";
                    heading.text = spec.Title;
                    heading.fontSize = 24f;
                    heading.enableAutoSizing = false;
                    heading.alignment = TextAlignmentOptions.MidlineLeft;

                    LayoutElement height = heading.gameObject.AddComponent<LayoutElement>();
                    height.preferredHeight = 34f;
                }

                GameObject grid = new GameObject("Controls", typeof(RectTransform));
                grid.transform.SetParent(section.transform, false);

                GridLayoutGroup cells = grid.AddComponent<GridLayoutGroup>();
                cells.cellSize = new Vector2(330f, 48f);
                cells.spacing = new Vector2(15f, 10f);
                cells.startCorner = GridLayoutGroup.Corner.UpperLeft;
                cells.startAxis = GridLayoutGroup.Axis.Horizontal;
                cells.childAlignment = TextAnchor.UpperLeft;
                cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                cells.constraintCount = columns;

                Section builder = new Section(spec.Owner, grid.transform);
                Owners.Run(spec.Owner, "section '" + spec.Title + "'", delegate { spec.Build(builder); });

                if (builder.First != null && page.Tab != null && page.Tab.ObjectToSelectOnOpen == page.Header.Root)
                {
                    UnityEngine.UI.Button first = builder.First.Root.GetComponentInChildren<UnityEngine.UI.Button>(true);
                    if (first != null) page.Tab.ObjectToSelectOnOpen = first.gameObject;
                }
            }
        }

        private static void HeaderToggled(HeaderTab header, bool on)
        {
            if (on)
            {
                Page page = header.Selected;
                if (page == null || page.Tab == null || !page.Visible) page = FirstVisible(header);
                if (page != null) OpenPage(page);
            }

            UpdateStrip(header);
        }

        private static Page FirstVisible(HeaderTab header)
        {
            for (int i = 0; i < header.Pages.Count; i++)
            {
                Page page = header.Pages[i];
                if (page.Tab != null && page.Visible) return page;
            }

            return null;
        }

        private static void OpenPage(Page page)
        {
            if (_panel == null || page.Tab == null || SwitchTabMethod == null) return;

            HeaderTab header = page.Header;
            header.Selected = page;

            for (int i = 0; i < header.Pages.Count; i++)
            {
                Toggle toggle = header.Pages[i].Switch;
                if (toggle != null) toggle.SetIsOnWithoutNotify(header.Pages[i] == page);
            }

            SwitchTabMethod.Invoke(_panel, new object[] { page.Type });
            UpdateStrip(header);
        }

        private static void Refresh()
        {
            Visible.Clear();

            for (int i = 0; i < Headers.Count; i++)
            {
                HeaderTab header = Headers[i];
                if (header.Root == null) continue;

                bool show = FirstVisible(header) != null;
                if (header.Root.activeSelf != show) header.Root.SetActive(show);
                if (show) Visible.Add(header);
            }

            string layout = Key();
            if (layout != _layout)
            {
                _layout = layout;
                Layout();
            }

            for (int i = 0; i < Headers.Count; i++)
            {
                UpdateStrip(Headers[i]);

                List<Page> pages = Headers[i].Pages;
                for (int j = 0; j < pages.Count; j++) RefreshModTabs(pages[j]);
            }

            PlayersBar.Refresh(_controlTemplate);
            Controls.RefreshVisible();
        }

        private static string Key()
        {
            StringBuilder key = new StringBuilder();
            key.Append(AdminTabsShowing ? 'A' : 'a');
            for (int i = 0; i < Visible.Count; i++) key.Append('|').Append(Visible[i].Name);
            return key.ToString();
        }

        private static bool AdminTabsShowing
        {
            get { return _adminTabs != null && _adminTabs.gameObject.activeSelf; }
        }

        private static void Layout()
        {
            if (_banner == null || _players == null || _rules == null) return;

            if (Visible.Count == 0)
            {
                Load(_players, _playersHome);
                Load(_rules, _rulesHome);
                if (_adminTabs != null) Load(_adminTabs, _adminHome);
                RestoreLabels();
                return;
            }

            bool admin = AdminTabsShowing;
            int stock = admin ? 6 : 2;
            int count = stock + Visible.Count;

            Place(_players, 0, 1, count);
            Place(_rules, 1, 1, count);
            if (admin) Place(_adminTabs, 2, 4, count);

            for (int i = 0; i < Visible.Count; i++) Place(Visible[i].Rect, stock + i, 1, count);

            foreach (KeyValuePair<PMenuTabType, PMenuTabButton> pair in StockHeaders)
            {
                if (pair.Value == null) continue;
                FitHeader(pair.Value.transform as RectTransform, pair.Value.GetComponentInChildren<TMP_Text>(true));
            }

            for (int i = 0; i < Visible.Count; i++) FitHeader(Visible[i].Rect, Visible[i].Label);
        }

        private static void Place(RectTransform rect, int start, int span, int count)
        {
            if (rect == null) return;

            float left = (start == 0) ? 0f : 1f;
            float right = (start + span >= count) ? 0f : 1f;

            rect.anchorMin = new Vector2((float)start / count, rect.anchorMin.y);
            rect.anchorMax = new Vector2((float)(start + span) / count, rect.anchorMax.y);
            rect.sizeDelta = new Vector2(-(left + right), rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(left * (1f - rect.pivot.x) - right * rect.pivot.x, rect.anchoredPosition.y);
        }

        private static void FitHeader(RectTransform button, TMP_Text label)
        {
            if (button == null || label == null) return;

            RectTransform text = label.rectTransform;
            Vector2 home;
            if (!LabelHomes.TryGetValue(label, out home))
            {
                home = new Vector2(Mathf.Min(label.fontSize, HeaderFont), text.anchoredPosition.x);
                LabelHomes[label] = home;
            }

            text.anchoredPosition = new Vector2(home.y, text.anchoredPosition.y);
            RectTransform icon = HeaderIcon(label, button);

            float available = button.rect.width - HeaderPadding * 2f;
            float size = home.x;
            float left;
            float right;
            label.enableAutoSizing = false;

            while (true)
            {
                label.fontSize = size;
                Extent(label, icon, out left, out right);
                if (right - left <= available || size <= MinHeaderFont) break;
                size -= 1f;
            }

            float pivot = button.InverseTransformPoint(text.position).x;
            float centre = (0.5f - button.pivot.x) * button.rect.width;
            float shift = centre - (pivot + (left + right) * 0.5f);
            text.anchoredPosition = new Vector2(home.y + shift, text.anchoredPosition.y);
        }

        private static void Extent(TMP_Text label, RectTransform icon, out float left, out float right)
        {
            RectTransform text = label.rectTransform;
            float width = label.GetPreferredValues(label.text).x;
            left = -text.pivot.x * width;
            right = (1f - text.pivot.x) * width;

            if (icon == null || !icon.gameObject.activeSelf || icon.parent != text) return;

            float iconWidth = icon.rect.width;
            float anchor = (icon.anchorMin.x + icon.anchorMax.x) * 0.5f;
            float iconLeft = (anchor - text.pivot.x) * width + icon.anchoredPosition.x - icon.pivot.x * iconWidth;
            left = Mathf.Min(left, iconLeft);
            right = Mathf.Max(right, iconLeft + iconWidth);
        }

        private static RectTransform HeaderIcon(TMP_Text label, RectTransform button)
        {
            Transform icon = label.transform.Find("Icon");
            if (icon == null) icon = button.Find("Icon");
            return icon as RectTransform;
        }

        private static void RestoreLabels()
        {
            foreach (KeyValuePair<TMP_Text, Vector2> pair in LabelHomes)
            {
                if (pair.Key == null) continue;

                pair.Key.fontSize = pair.Value.x;
                RectTransform text = pair.Key.rectTransform;
                text.anchoredPosition = new Vector2(pair.Value.y, text.anchoredPosition.y);
            }
        }

        private static void UpdateStrip(HeaderTab header)
        {
            if (header.Strip == null) return;

            VisiblePages.Clear();
            for (int i = 0; i < header.Pages.Count; i++)
            {
                Page page = header.Pages[i];
                if (page.Button == null) continue;

                bool show = page.Visible;
                if (page.Button.activeSelf != show) page.Button.SetActive(show);
                if (show) VisiblePages.Add(page);
            }

            for (int i = 0; i < VisiblePages.Count; i++)
            {
                RectTransform rect = VisiblePages[i].Button.transform as RectTransform;
                if (rect == null) continue;

                rect.anchoredPosition = new Vector2(StripOffset - StripSpacing * (VisiblePages.Count - 1 - i), 0f);
            }

            bool open = header.Root != null && header.Root.activeSelf && header.Switch != null && header.Switch.isOn &&
                        VisiblePages.Count > 1;
            if (header.Strip.activeSelf != open) header.Strip.SetActive(open);
        }

        internal static bool Cycle(PMenuPanel panel, bool next)
        {
            if (panel == null || panel != _panel || !panel.CurrentlyShowing) return false;

            Visible.Clear();
            for (int i = 0; i < Headers.Count; i++)
            {
                if (Headers[i].Root != null && Headers[i].Root.activeSelf && Headers[i].Switch != null) Visible.Add(Headers[i]);
            }

            if (Visible.Count == 0) return false;

            bool admin = panel.currentlyAdminMode;
            int stock = admin ? 6 : 2;
            int total = stock + Visible.Count;

            int current = Slot(panel.CurrentActiveTabType, admin, stock);
            if (current < 0) return false;

            int target = (current + (next ? 1 : total - 1)) % total;
            if (target >= stock)
            {
                Visible[target - stock].Switch.isOn = true;
                return true;
            }

            PMenuTabButton button;
            if (!StockHeaders.TryGetValue(StockOrder[target], out button) || button == null || button.toggle == null) return false;

            button.toggle.isOn = true;
            return true;
        }

        private static int Slot(PMenuTabType type, bool admin, int stock)
        {
            if (type == PMenuTabType.RoundPlayers || type == PMenuTabType.RegimentPlayers ||
                type == PMenuTabType.LimboPlayers || type == PMenuTabType.LoggedInPlayers) return 0;

            if (type == PMenuTabType.Rules) return 1;

            if (admin)
            {
                for (int i = 2; i < StockOrder.Length; i++)
                {
                    if (StockOrder[i] == type) return i;
                }
            }

            for (int i = 0; i < Visible.Count; i++)
            {
                List<Page> pages = Visible[i].Pages;
                for (int j = 0; j < pages.Count; j++)
                {
                    if (pages[j].HasType && pages[j].Type == type) return stock + i;
                }
            }

            return -1;
        }

        private static void SetIcon(Transform root, string sprite)
        {
            if (string.IsNullOrEmpty(sprite)) return;

            Image icon = null;
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].name != "Icon") continue;
                icon = images[i];
                break;
            }

            if (icon == null) return;

            Sprite found = Sprites.Find(sprite, SpriteRoot);
            if (found != null) icon.sprite = found;
        }

        private static void DropTabData(GameObject root)
        {
            PMenuTabButton header = root.GetComponent<PMenuTabButton>();
            if (header != null) UnityEngine.Object.DestroyImmediate(header);

            PMenuChildTabButton child = root.GetComponent<PMenuChildTabButton>();
            if (child != null) UnityEngine.Object.DestroyImmediate(child);
        }

        private static Anchoring Save(RectTransform rect)
        {
            Anchoring anchoring;
            anchoring.Min = rect.anchorMin;
            anchoring.Max = rect.anchorMax;
            anchoring.Size = rect.sizeDelta;
            anchoring.Position = rect.anchoredPosition;
            return anchoring;
        }

        private static void Load(RectTransform rect, Anchoring anchoring)
        {
            if (rect == null) return;

            rect.anchorMin = anchoring.Min;
            rect.anchorMax = anchoring.Max;
            rect.sizeDelta = anchoring.Size;
            rect.anchoredPosition = anchoring.Position;
        }
    }

    [HarmonyPatch(typeof(PMenuPanel), "SwitchToNextTab")]
    internal static class NextTabPatch
    {
        private static bool Prefix(PMenuPanel __instance)
        {
            return !Safe(__instance, true);
        }

        internal static bool Safe(PMenuPanel panel, bool next)
        {
            try
            {
                return Menu.Cycle(panel, next);
            }
            catch (Exception error)
            {
                Log.Error("tab cycling failed: " + error.Message);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(PMenuPanel), "SwitchToPreviousTab")]
    internal static class PreviousTabPatch
    {
        private static bool Prefix(PMenuPanel __instance)
        {
            return !NextTabPatch.Safe(__instance, false);
        }
    }
}
