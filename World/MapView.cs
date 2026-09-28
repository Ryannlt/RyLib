using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using HoldfastGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RyLib
{
    internal sealed class MapSurface : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        internal MapView View;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (View != null) View.Click(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (View != null) View.BeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (View != null) View.Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (View != null) View.EndDrag();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (View != null) View.Scroll(eventData);
        }
    }

    internal sealed class MapView
    {
        private const float MapSize = 780f;
        private const float PanelWidth = 250f;
        private const float LegendGap = 10f;
        private const float LegendCellWidth = 150f;
        private const float AvailableSeconds = 2f;
        private const float Margin = 25f;
        private const float Gap = 20f;
        private const float TopOffset = 135f;
        private const float RefreshSeconds = 0.1f;
        private const float FillSeconds = 0.2f;
        private const float CountSeconds = 0.5f;
        private const float CameraSize = 22f;
        private const float MarkerSize = 30f;
        private const float MarkerSeconds = 1.5f;
        private const float LegendRowHeight = 22f;
        private const float LegendIcon = 15f;
        private const float ButtonHeight = 44f;
        private const float GridMetres = 100f;
        private const float ZoomStep = 1.25f;
        private const float MinView = 0.125f;
        private const float FullViewShare = 0.85f;
        private const float HaloScale = 1.25f;
        private const float SlayArmSeconds = 4f;
        private const float IconStep = 2f;

        private static readonly Color SelfColour = Color.white;
        private static readonly Color SelectedColour = new Color(1f, 0.9f, 0.3f);
        private static readonly Color TargetColour = new Color(0.35f, 1f, 0.45f);
        private static readonly Color LegendColour = new Color(0.85f, 0.86f, 0.88f);
        private static readonly Color FrameColour = new Color(0.05f, 0.06f, 0.07f, 0.92f);
        private static readonly Color TipColour = new Color(0.05f, 0.05f, 0.06f, 0.9f);
        private static readonly Rect FullView = new Rect(0f, 0f, 1f, 1f);
        private static readonly List<MapView> Views = new List<MapView>();

        private sealed class Dot
        {
            public int PlayerId;
            public RectTransform Root;
            public Image Halo;
            public Image Icon;
            public Vector2 Local;
            public string Name;
            public string Detail;
            public bool Seen;
            public bool Hidden;
        }

        private sealed class Overlay
        {
            public Image Image;
            public Vector3 World;
            public Vector2 Local;
            public string Tooltip;
            public bool OnPlayer;
            public int PlayerId;
            public bool Active;
        }

        private sealed class LegendRow
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Text;
        }

        private static float IconSize
        {
            get { return (MapStyle.IconSize == null) ? 22f : Mathf.Clamp(MapStyle.IconSize.Value, 10f, 40f); }
        }

        private static float PickRadius
        {
            get { return Mathf.Max(10f, IconSize * 0.55f); }
        }

        private readonly Page _page;
        private readonly TMP_Text _template;
        private readonly Dictionary<int, Dot> _dots = new Dictionary<int, Dot>();
        private readonly List<Dot> _dotPool = new List<Dot>();
        private readonly List<int> _gone = new List<int>();
        private readonly List<Overlay> _overlays = new List<Overlay>();
        private readonly Dictionary<int, Color> _outlines = new Dictionary<int, Color>();
        private readonly Dictionary<int, string> _outlineTips = new Dictionary<int, string>();
        private readonly StringBuilder _text = new StringBuilder();

        private readonly Dictionary<FactionCountry, int> _factionTotals = new Dictionary<FactionCountry, int>();
        private readonly Dictionary<FactionCountry, int> _factionAlive = new Dictionary<FactionCountry, int>();
        private readonly List<FactionCountry> _factionOrder = new List<FactionCountry>();
        private readonly List<LegendRow> _factionRows = new List<LegendRow>();
        private readonly Comparison<FactionCountry> _byTotal;
        private readonly int[] _kindCounts = new int[ClassIcons.KindCount];
        private readonly bool[] _kindAvailable = new bool[ClassIcons.KindCount];
        private readonly List<LegendRow> _classRows = new List<LegendRow>();

        private readonly RectTransform _frame;
        private readonly RawImage _image;
        private readonly AspectRatioFitter _fitter;
        private readonly RectTransform _dotRoot;
        private readonly RectTransform _overlayRoot;
        private readonly RectTransform _markRoot;
        private readonly Image _cameraMark;
        private readonly Image _selectMark;
        private readonly Image _selfMark;
        private readonly Image _targetMark;
        private readonly TMP_Text _heading;
        private readonly TMP_Text _info;
        private readonly RectTransform _buttons;
        private readonly TMP_Text _count;
        private readonly RectTransform _factionRoot;
        private readonly RectTransform _legendRoot;
        private readonly RectTransform _tip;
        private readonly TMP_Text _tipText;

        private bool _showing;
        private string[] _filter = new string[0];
        private float _nextRefresh;
        private float _nextFill;
        private float _nextCount;
        private float _nextAvailable;
        private int _selected = -1;
        private string _selectedName;
        private string _selectedDetail;
        private int _selfId = -1;
        private bool _panning;
        private Vector2 _panLast;
        private bool _sending;
        private int _sendTarget = -1;
        private string _sendName;
        private Vector3 _targetWorld;
        private float _targetUntil;
        private int _slayArmed = -1;
        private float _slayArmedUntil;
        private Rect _area;
        private bool _hasArea;
        private bool _grid;
        private Rect _view = FullView;
        private MapRender.State _shown = (MapRender.State)(-1);

        internal MapView(Page page, RectTransform root, TMP_Text template, TMP_InputField search)
        {
            _page = page;
            _template = template;
            _byTotal = delegate(FactionCountry left, FactionCountry right)
            {
                int order = _factionTotals[right].CompareTo(_factionTotals[left]);
                return (order != 0) ? order : ((int)left).CompareTo((int)right);
            };

            _frame = Box("Map Frame", root, new Vector2(Margin, -TopOffset), new Vector2(MapSize, MapSize));
            Image back = _frame.gameObject.AddComponent<Image>();
            back.color = FrameColour;
            back.raycastTarget = false;

            GameObject imageHost = new GameObject("Map Image", typeof(RectTransform));
            imageHost.transform.SetParent(_frame, false);
            Stretch((RectTransform)imageHost.transform);

            _image = imageHost.AddComponent<RawImage>();
            _image.color = Color.white;
            _image.raycastTarget = true;
            imageHost.AddComponent<RectMask2D>();

            _fitter = imageHost.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = 1f;

            imageHost.AddComponent<MapSurface>().View = this;

            _dotRoot = Layer("Dots", _image.rectTransform);
            _overlayRoot = Layer("Overlays", _image.rectTransform);
            _markRoot = Layer("Marks", _image.rectTransform);

            _cameraMark = Mark(_overlayRoot, Glyphs.Arrow, CameraSize);
            _cameraMark.color = SelfColour;
            _cameraMark.gameObject.SetActive(false);

            _selfMark = Mark(_markRoot, Glyphs.Ring, IconSize * 1.7f);
            _selfMark.color = SelfColour;
            _selfMark.gameObject.SetActive(false);

            _selectMark = Mark(_markRoot, Glyphs.Brackets, IconSize + 16f);
            _selectMark.color = SelectedColour;
            _selectMark.gameObject.SetActive(false);

            _targetMark = Mark(_markRoot, Glyphs.Crosshair, MarkerSize);
            _targetMark.color = TargetColour;
            _targetMark.gameObject.SetActive(false);

            RectTransform panel = Box("Map Panel", root, new Vector2(Margin + MapSize + Gap, -TopOffset), new Vector2(PanelWidth, MapSize));
            VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 8f;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            _heading = Text(template, panel, 22f, TextAlignmentOptions.TopLeft);
            Height(_heading.gameObject, 34f);

            _info = Text(template, panel, 15f, TextAlignmentOptions.TopLeft);
            _info.enableWordWrapping = true;
            Height(_info.gameObject, 70f);

            _buttons = Stack("Buttons", panel, 6f);

            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(panel, false);
            Element(spacer).flexibleHeight = 1f;

            RectTransform views = Pair("View Buttons", panel);
            AddAction(views, RyLibPlugin.Guid, "Battle Area", StockIcon.Maps, delegate { SetView(BattleView()); }, true);
            AddAction(views, RyLibPlugin.Guid, "Whole Map", StockIcon.Maps, delegate { SetView(FullView); }, true);

            RectTransform sizes = Pair("Icon Size Buttons", panel);
            AddAction(sizes, RyLibPlugin.Guid, "Icons -", StockIcon.Players, delegate { Resize(-IconStep); }, true);
            AddAction(sizes, RyLibPlugin.Guid, "Icons +", StockIcon.Players, delegate { Resize(IconStep); }, true);

            _count = Text(template, panel, 16f, TextAlignmentOptions.Left);
            Height(_count.gameObject, 24f);

            _factionRoot = Stack("Factions", panel, 2f);

            float legendWidth = MapSize + Gap + PanelWidth;
            float legendTop = TopOffset + MapSize + LegendGap;
            _legendRoot = Box("Map Legend", root, new Vector2(Margin, -legendTop), new Vector2(legendWidth, LegendRowHeight * 3f + 4f));
            GridLayoutGroup grid = _legendRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(LegendCellWidth, LegendRowHeight);
            grid.spacing = new Vector2(0f, 2f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt(legendWidth / LegendCellWidth));

            GameObject tip = new GameObject("Map Tooltip", typeof(RectTransform));
            tip.transform.SetParent(root, false);
            _tip = (RectTransform)tip.transform;
            _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, 0.5f);
            _tip.pivot = new Vector2(0f, 0f);
            Image tipBack = tip.AddComponent<Image>();
            tipBack.color = TipColour;
            tipBack.raycastTarget = false;

            _tipText = Text(template, _tip, 15f, TextAlignmentOptions.TopLeft);
            Stretch(_tipText.rectTransform);
            _tipText.rectTransform.offsetMin = new Vector2(8f, 5f);
            _tipText.rectTransform.offsetMax = new Vector2(-8f, -5f);
            tip.SetActive(false);

            if (search != null)
            {
                search.onValueChanged = new TMP_InputField.OnChangeEvent();
                search.text = string.Empty;
                search.onValueChanged.AddListener(Filter);
            }

            Views.Add(this);
            ShowSelection();
        }

        internal static void TickAll()
        {
            for (int i = Views.Count - 1; i >= 0; i--)
            {
                MapView view = Views[i];
                if (view._frame == null)
                {
                    Views.RemoveAt(i);
                    continue;
                }

                if (view._showing) view.Update();
            }
        }

        internal void Show(bool show)
        {
            _showing = show;
            if (!show)
            {
                StopSending();
                _panning = false;
                HideTip();
                return;
            }

            MapRender.Request();
            _nextRefresh = 0f;
            _nextFill = 0f;
            _nextCount = 0f;
            _nextAvailable = 0f;
            ShowSelection();
        }

        internal void Invalidate()
        {
            MapRender.Invalidate();
            _hasArea = false;
            _grid = false;
            _view = FullView;
            _shown = (MapRender.State)(-1);
            if (_image != null) _image.texture = null;

            foreach (KeyValuePair<int, Dot> pair in _dots) Park(pair.Value);
            _dots.Clear();
            _selected = -1;
            _selfId = -1;
            _slayArmed = -1;
            _targetUntil = 0f;
            ClassIcons.Retry();
            _nextAvailable = 0f;
            StopSending();
            if (_selectMark != null) _selectMark.gameObject.SetActive(false);
            if (_selfMark != null) _selfMark.gameObject.SetActive(false);
            if (_targetMark != null) _targetMark.gameObject.SetActive(false);
        }

        private void Update()
        {
            Sync();

            if (!ClientRemoteConsoleAccessManager.loggedOn || !_hasArea)
            {
                HideTip();
                return;
            }

            float now = Time.unscaledTime;
            if (_slayArmed >= 0 && now > _slayArmedUntil)
            {
                _slayArmed = -1;
                ShowSelection();
            }

            if (_sending && Input.GetMouseButtonDown(1) && Inside())
            {
                StopSending();
                ShowSelection();
            }

            if (now >= _nextFill)
            {
                _nextFill = now + FillSeconds;
                RefreshOverlays();
            }

            if (now >= _nextRefresh)
            {
                _nextRefresh = now + RefreshSeconds;
                RefreshPlayers();
                RefreshCamera();
                RefreshTarget();
            }

            if (now >= _nextCount)
            {
                _nextCount = now + CountSeconds;
                RefreshCounts();
            }

            Hover();
        }

        private void Sync()
        {
            if (MapRender.Status == MapRender.State.Empty) MapRender.Request();

            MapRender.State state = MapRender.Status;
            if (state == _shown && _hasArea) return;
            _shown = state;

            switch (state)
            {
                case MapRender.State.Ready:
                    _image.texture = MapRender.Texture;
                    _grid = false;
                    UseArea(MapRender.Area);
                    break;

                case MapRender.State.Failed:
                    if (_hasArea) break;
                    _image.texture = Glyphs.Grid;
                    _grid = true;
                    if (MapRender.Area.width > 1f && MapRender.Area.height > 1f) UseArea(MapRender.Area);
                    else PlayerArea();
                    break;
            }
        }

        private void UseArea(Rect area)
        {
            _area = area;
            _hasArea = area.width > 1f && area.height > 1f;
            _fitter.aspectRatio = _hasArea ? area.width / area.height : 1f;
            _view = BattleView();
            ApplyView();
        }

        private void PlayerArea()
        {
            ClientRoundPlayerManager manager = Players();
            if (manager == null) return;

            bool found = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;

            List<ClientRoundPlayerProxy> remotes = manager.roundPlayersList;
            for (int i = 0; i < remotes.Count; i++)
            {
                Vector3 position;
                if (!Where(remotes[i], out position)) continue;

                Vector2 flat = new Vector2(position.x, position.z);
                if (!found)
                {
                    min = max = flat;
                    found = true;
                }
                else
                {
                    min = Vector2.Min(min, flat);
                    max = Vector2.Max(max, flat);
                }
            }

            if (!found) return;

            Vector2 size = Vector2.Max(max - min, new Vector2(200f, 200f));
            Vector2 centre = (min + max) * 0.5f;
            size *= 1.3f;
            UseArea(new Rect(centre - size * 0.5f, size));
        }

        private Rect BattleView()
        {
            if (!_hasArea) return FullView;

            Rect battle = MapRender.Battle;
            if (battle.width < 1f || battle.height < 1f) return FullView;

            float share = Mathf.Max(battle.width / _area.width, battle.height / _area.height);
            if (share >= FullViewShare) return FullView;

            float centreX = (battle.center.x - _area.xMin) / _area.width;
            float centreY = (battle.center.y - _area.yMin) / _area.height;
            return ClampView(new Rect(centreX - share * 0.5f, centreY - share * 0.5f, share, share));
        }

        private static Rect ClampView(Rect view)
        {
            float size = Mathf.Clamp(view.width, MinView, 1f);
            float x = Mathf.Clamp(view.x, 0f, 1f - size);
            float y = Mathf.Clamp(view.y, 0f, 1f - size);
            return new Rect(x, y, size, size);
        }

        private void ApplyView()
        {
            if (_grid)
            {
                float across = _area.width / GridMetres;
                float down = _area.height / GridMetres;
                _image.uvRect = new Rect(_view.x * across, _view.y * down, _view.width * across, _view.height * down);
            }
            else
            {
                _image.uvRect = _view;
            }

            _nextRefresh = 0f;
            _nextFill = 0f;
        }

        private void SetView(Rect view)
        {
            if (!_hasArea) return;

            _view = ClampView(view);
            ApplyView();
            RefreshPlayers();
            RefreshCamera();
            RefreshTarget();
            RefreshOverlayPositions();
        }

        private void Resize(float delta)
        {
            ConfigEntry<float> entry = MapStyle.IconSize;
            if (entry == null) return;

            float value = entry.Value + delta;
            AcceptableValueBase range = entry.Description.AcceptableValues;
            entry.Value = (range != null) ? (float)range.Clamp(value) : value;
            GlowStyle.Saved();

            if (_hasArea) RefreshPlayers();
        }

        internal void Scroll(PointerEventData eventData)
        {
            if (!_hasArea) return;

            Vector2 local;
            if (!ToImage(eventData.position, eventData.enterEventCamera, out local)) return;

            Rect rect = _image.rectTransform.rect;
            float fx = local.x / rect.width + 0.5f;
            float fy = local.y / rect.height + 0.5f;
            float u = _view.x + fx * _view.width;
            float v = _view.y + fy * _view.height;

            float size = Mathf.Clamp(_view.width / Mathf.Pow(ZoomStep, eventData.scrollDelta.y), MinView, 1f);
            SetView(new Rect(u - fx * size, v - fy * size, size, size));
        }

        private void RefreshPlayers()
        {
            ClientRoundPlayerManager manager = Players();
            if (manager == null) return;

            foreach (KeyValuePair<int, Dot> pair in _dots) pair.Value.Seen = false;

            List<ClientRoundPlayerProxy> remotes = manager.roundPlayersList;
            for (int i = 0; i < remotes.Count; i++) Place(remotes[i], false);

            _selfId = (manager.LocalPlayer == null) ? -1 : manager.LocalPlayer.NetworkPlayerID;
            Place(manager.LocalPlayer, true);

            _gone.Clear();
            foreach (KeyValuePair<int, Dot> pair in _dots)
            {
                if (!pair.Value.Seen) _gone.Add(pair.Key);
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                Park(_dots[_gone[i]]);
                _dots.Remove(_gone[i]);
            }

            for (int i = 0; i < _overlays.Count; i++)
            {
                Overlay overlay = _overlays[i];
                if (!overlay.Active || !overlay.OnPlayer) continue;

                Dot dot;
                bool shown = _dots.TryGetValue(overlay.PlayerId, out dot) && !dot.Hidden;
                if (shown)
                {
                    overlay.Local = dot.Local;
                    overlay.Image.rectTransform.anchoredPosition = dot.Local;
                }

                if (overlay.Image.gameObject.activeSelf != shown) overlay.Image.gameObject.SetActive(shown);
            }

            float size = IconSize;
            _selfMark.rectTransform.sizeDelta = new Vector2(size * 1.7f, size * 1.7f);
            _selectMark.rectTransform.sizeDelta = new Vector2(size + 16f, size + 16f);
            Pin(_selfMark, _selfId);
            Pin(_selectMark, _selected);
        }

        private void Place(RoundPlayer player, bool self)
        {
            Vector3 position;
            if (!Where(player, out position)) return;

            int id = player.NetworkPlayerID;

            Dot dot;
            if (!_dots.TryGetValue(id, out dot))
            {
                dot = TakeDot();
                dot.PlayerId = id;
                _dots[id] = dot;
            }

            FactionCountry faction = player.PlayerStartData.Faction;
            PlayerClass type = player.PlayerStartData.ClassType;
            ClassKind kind = ClassIcons.Of(player);

            dot.Seen = true;
            dot.Local = ToLocal(position);
            dot.Root.anchoredPosition = dot.Local;
            dot.Name = NameOf(player);
            dot.Detail = Factions.Name(faction) + " " + ((kind == ClassKind.Other) ? Factions.ClassName(type) : ClassIcons.Label(kind));

            float size = self ? IconSize + 4f : IconSize;
            dot.Root.sizeDelta = new Vector2(size, size);
            dot.Halo.rectTransform.sizeDelta = new Vector2(size * HaloScale, size * HaloScale);

            Sprite icon = ClassIcons.Icon(kind);
            dot.Icon.sprite = icon;
            dot.Icon.color = Factions.Colour(faction);

            Color outline;
            Sprite halo = _outlines.TryGetValue(id, out outline) ? Glyphs.Halo(icon) : null;
            if (halo != null)
            {
                dot.Halo.sprite = halo;
                dot.Halo.color = outline;
            }

            bool haloShown = halo != null;
            if (dot.Halo.gameObject.activeSelf != haloShown) dot.Halo.gameObject.SetActive(haloShown);
            dot.Hidden = !Matches(dot);
            if (dot.Root.gameObject.activeSelf == dot.Hidden) dot.Root.gameObject.SetActive(!dot.Hidden);
        }

        private void Filter(string text)
        {
            _filter = string.IsNullOrEmpty(text)
                ? new string[0]
                : text.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (_hasArea) RefreshPlayers();
        }

        private bool Matches(Dot dot)
        {
            if (_filter.Length == 0) return true;

            string haystack = ((dot.Name ?? string.Empty) + " " + (dot.Detail ?? string.Empty)).ToLowerInvariant();
            for (int i = 0; i < _filter.Length; i++)
            {
                if (haystack.IndexOf(_filter[i], StringComparison.Ordinal) < 0) return false;
            }

            return true;
        }

        private void Pin(Image mark, int id)
        {
            Dot dot = null;
            bool shown = id >= 0 && _dots.TryGetValue(id, out dot) && !dot.Hidden;
            if (shown) mark.rectTransform.anchoredPosition = dot.Local;
            if (mark.gameObject.activeSelf != shown) mark.gameObject.SetActive(shown);
        }

        private void RefreshTarget()
        {
            bool shown = Time.unscaledTime < _targetUntil;
            if (shown) _targetMark.rectTransform.anchoredPosition = ToLocal(_targetWorld);
            if (_targetMark.gameObject.activeSelf != shown) _targetMark.gameObject.SetActive(shown);
        }

        private void RefreshCounts()
        {
            ClientRoundPlayerManager manager = Players();
            if (manager == null) return;

            _factionTotals.Clear();
            _factionAlive.Clear();
            _factionOrder.Clear();
            Array.Clear(_kindCounts, 0, _kindCounts.Length);

            int total = 0;
            int alive = 0;
            int unassigned = 0;

            List<ClientRoundPlayerProxy> remotes = manager.roundPlayersList;
            for (int i = 0; i < remotes.Count; i++) Count(remotes[i], ref total, ref alive, ref unassigned);
            Count(manager.LocalPlayer, ref total, ref alive, ref unassigned);

            _factionOrder.Sort(_byTotal);

            SetText(_count, "Spawned " + alive + " / " + total);

            int used = 0;
            for (int i = 0; i < _factionOrder.Count; i++)
            {
                FactionCountry faction = _factionOrder[i];
                LegendRow row = FactionRow(used++);
                row.Icon.color = Factions.Colour(faction);
                SetText(row.Text, Factions.Name(faction) + "  " + _factionAlive[faction] + " / " + _factionTotals[faction]);
            }

            if (unassigned > 0)
            {
                LegendRow row = FactionRow(used++);
                row.Icon.color = Factions.Neutral;
                SetText(row.Text, "Unassigned  " + unassigned);
            }

            for (int i = 0; i < _factionRows.Count; i++)
            {
                bool shown = i < used;
                if (_factionRows[i].Root.activeSelf != shown) _factionRows[i].Root.SetActive(shown);
            }

            if (Time.unscaledTime >= _nextAvailable)
            {
                _nextAvailable = Time.unscaledTime + AvailableSeconds;
                RefreshAvailable();
            }

            int listed = 0;
            for (int i = 0; i < _kindCounts.Length; i++)
            {
                if (!_kindAvailable[i] && _kindCounts[i] == 0) continue;

                ClassKind kind = (ClassKind)i;
                LegendRow row = ClassRow(listed++);
                row.Icon.sprite = ClassIcons.Icon(kind);
                SetText(row.Text, ClassIcons.Label(kind) + "  " + _kindCounts[i]);
            }

            for (int i = 0; i < _classRows.Count; i++)
            {
                bool shown = i < listed;
                if (_classRows[i].Root.activeSelf != shown) _classRows[i].Root.SetActive(shown);
            }
        }

        private void RefreshAvailable()
        {
            Array.Clear(_kindAvailable, 0, _kindAvailable.Length);

            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null || client.clientSpawnSectionManager == null || client.clientGameModeManager == null) return;

            RoundGameDetails details = client.clientGameModeManager.CurrentRoundGameDetails;
            if (details == null) return;

            Available(client.clientSpawnSectionManager, details.AttackingFaction, details.FrontlinesMode);
            Available(client.clientSpawnSectionManager, details.DefendingFaction, details.FrontlinesMode);
        }

        private void Available(ClientSpawnSectionManager spawns, FactionCountry faction, bool frontlines)
        {
            if (faction == FactionCountry.None) return;

            foreach (PlayerClass type in spawns.ResolveCurrentlyAvailableClasses(faction).Keys)
            {
                _kindAvailable[(int)ClassIcons.OfClass(type, frontlines)] = true;
            }
        }

        private void Count(RoundPlayer player, ref int total, ref int alive, ref int unassigned)
        {
            if (player == null) return;
            total++;

            bool living = player.PlayerBase != null && player.PlayerBase.SpawnedAndAlive && player.PlayerStartData != null;
            if (living)
            {
                alive++;
                _kindCounts[(int)ClassIcons.Of(player)]++;
            }

            FactionCountry faction = (player.PlayerStartData == null) ? FactionCountry.None : player.PlayerStartData.Faction;
            if (faction == FactionCountry.None)
            {
                unassigned++;
                return;
            }

            int count;
            if (!_factionTotals.TryGetValue(faction, out count))
            {
                _factionOrder.Add(faction);
                _factionAlive[faction] = 0;
            }

            _factionTotals[faction] = count + 1;
            if (living) _factionAlive[faction] = _factionAlive[faction] + 1;
        }

        private LegendRow FactionRow(int index)
        {
            while (_factionRows.Count <= index)
            {
                _factionRows.Add(Row(_factionRoot, Glyphs.Class(ClassShape.Circle), Factions.Neutral, string.Empty));
            }

            return _factionRows[index];
        }

        private LegendRow ClassRow(int index)
        {
            while (_classRows.Count <= index)
            {
                _classRows.Add(Row(_legendRoot, Glyphs.Class(ClassShape.Circle), LegendColour, string.Empty));
            }

            return _classRows[index];
        }

        private void RefreshCamera()
        {
            Camera camera = GameView.ActiveCamera;
            if (camera == null)
            {
                _cameraMark.gameObject.SetActive(false);
                return;
            }

            Vector2 local;
            Dot anchor;
            int followed = Followed();
            if (followed >= 0 && _dots.TryGetValue(followed, out anchor)) local = anchor.Local;
            else local = ToLocal(camera.transform.position);

            _cameraMark.rectTransform.anchoredPosition = local;
            _cameraMark.rectTransform.localEulerAngles = new Vector3(0f, 0f, -camera.transform.eulerAngles.y);
            if (!_cameraMark.gameObject.activeSelf) _cameraMark.gameObject.SetActive(true);
            _cameraMark.transform.SetAsLastSibling();
        }

        private int Followed()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null || PlayerActions.Freeflight(client)) return -1;

            ClientSpectatorManager spectator = client.clientSpectatorManager;
            if (spectator != null && spectator.CurrentlySpectating && spectator.currentlySpectatingPlayer != null)
            {
                return spectator.currentlySpectatingPlayer.NetworkPlayerID;
            }

            return _selfId;
        }

        private void RefreshOverlays()
        {
            int used = 0;
            _outlines.Clear();
            _outlineTips.Clear();

            for (int i = 0; i < _page.MapLayers.Count; i++)
            {
                MapLayerSpec layer = _page.MapLayers[i];
                layer.Marks.Clear();
                Owners.Run(layer.Owner, "map layer", delegate { layer.Fill(layer.Marks); });

                for (int j = 0; j < layer.Marks.Count; j++)
                {
                    MapMark mark = layer.Marks[j];

                    if (mark.Shape == MapShape.Outline)
                    {
                        if (!mark.OnPlayer || _outlines.ContainsKey(mark.PlayerId)) continue;
                        _outlines[mark.PlayerId] = mark.Colour;
                        if (!string.IsNullOrEmpty(mark.Tooltip)) _outlineTips[mark.PlayerId] = mark.Tooltip;
                        continue;
                    }

                    Overlay overlay = TakeOverlay(used++);

                    overlay.Active = true;
                    overlay.OnPlayer = mark.OnPlayer;
                    overlay.PlayerId = mark.PlayerId;
                    overlay.Tooltip = mark.Tooltip;
                    overlay.World = mark.Position;

                    overlay.Image.sprite = Glyphs.Mark(mark.Shape);
                    overlay.Image.color = mark.Colour;

                    float size = (mark.Size > 0f) ? mark.Size : DefaultSize(mark.Shape);
                    overlay.Image.rectTransform.sizeDelta = new Vector2(size, size);

                    Dot dot;
                    if (mark.OnPlayer && _dots.TryGetValue(mark.PlayerId, out dot)) overlay.Local = dot.Local;
                    else overlay.Local = ToLocal(mark.Position);

                    overlay.Image.rectTransform.anchoredPosition = overlay.Local;
                    bool shown = !mark.OnPlayer || (_dots.TryGetValue(mark.PlayerId, out dot) && !dot.Hidden);
                    if (overlay.Image.gameObject.activeSelf != shown) overlay.Image.gameObject.SetActive(shown);
                }
            }

            for (int i = used; i < _overlays.Count; i++)
            {
                _overlays[i].Active = false;
                if (_overlays[i].Image.gameObject.activeSelf) _overlays[i].Image.gameObject.SetActive(false);
            }
        }

        private void RefreshOverlayPositions()
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                Overlay overlay = _overlays[i];
                if (!overlay.Active || overlay.OnPlayer) continue;

                overlay.Local = ToLocal(overlay.World);
                overlay.Image.rectTransform.anchoredPosition = overlay.Local;
            }
        }

        private void Hover()
        {
            Vector2 local;
            if (!Mouse(out local))
            {
                HideTip();
                return;
            }

            if (_sending)
            {
                ShowTip((_sendTarget >= 0) ? "Send " + _sendName + " here" : "Go here", local);
                return;
            }

            int id = Pick(local);
            _text.Length = 0;

            if (id >= 0)
            {
                Dot dot = _dots[id];
                _text.Append(dot.Name).Append('\n').Append(dot.Detail);

                string outlineTip;
                if (_outlineTips.TryGetValue(id, out outlineTip)) _text.Append('\n').Append(outlineTip);

                for (int i = 0; i < _overlays.Count; i++)
                {
                    Overlay overlay = _overlays[i];
                    if (overlay.Active && overlay.OnPlayer && overlay.PlayerId == id && !string.IsNullOrEmpty(overlay.Tooltip))
                    {
                        _text.Append('\n').Append(overlay.Tooltip);
                    }
                }
            }
            else
            {
                float best = PickRadius * PickRadius;
                string tooltip = null;
                for (int i = 0; i < _overlays.Count; i++)
                {
                    Overlay overlay = _overlays[i];
                    if (!overlay.Active || overlay.OnPlayer || string.IsNullOrEmpty(overlay.Tooltip)) continue;

                    float distance = (overlay.Local - local).sqrMagnitude;
                    if (distance > best) continue;
                    best = distance;
                    tooltip = overlay.Tooltip;
                }

                if (tooltip == null)
                {
                    HideTip();
                    return;
                }

                _text.Append(tooltip);
            }

            ShowTip(_text.ToString(), local);
        }

        private void ShowTip(string text, Vector2 local)
        {
            if (_tipText.text != text)
            {
                _tipText.text = text;
                _tip.sizeDelta = _tipText.GetPreferredValues(text) + new Vector2(16f, 10f);
            }

            _tip.position = _image.rectTransform.TransformPoint(new Vector3(local.x + 14f, local.y + 14f, 0f));
            _tip.SetAsLastSibling();
            if (!_tip.gameObject.activeSelf) _tip.gameObject.SetActive(true);
        }

        internal void Click(PointerEventData eventData)
        {
            if (eventData.dragging) return;
            if (eventData.button != PointerEventData.InputButton.Left || !_hasArea) return;
            if (!ClientRemoteConsoleAccessManager.loggedOn) return;

            Vector2 local;
            if (!ToImage(eventData.position, eventData.pressEventCamera, out local)) return;

            if (_sending)
            {
                Vector3 world = ToWorld(local);
                world.y = PlayerActions.Ground(world);
                int target = _sendTarget;
                StopSending();

                if (target >= 0) PlayerActions.Teleport(target, world);
                else PlayerActions.GoThere(world);

                _targetWorld = world;
                _targetUntil = Time.unscaledTime + MarkerSeconds;
                RefreshTarget();
                ShowSelection();
                return;
            }

            int id = Pick(local);
            if (id >= 0) Select(id);
        }

        internal void BeginDrag(PointerEventData eventData)
        {
            _panning = false;
            if (eventData.button != PointerEventData.InputButton.Left || !_hasArea) return;

            Vector2 local;
            if (!ToImage(eventData.pressPosition, eventData.pressEventCamera, out local)) return;

            _panning = true;
            _panLast = local;
            Drag(eventData);
        }

        internal void Drag(PointerEventData eventData)
        {
            if (!_panning) return;

            Vector2 local;
            if (!ToImage(eventData.position, eventData.pressEventCamera, out local)) return;

            Rect rect = _image.rectTransform.rect;
            Vector2 delta = local - _panLast;
            _panLast = local;
            if (delta.sqrMagnitude < 0.01f) return;

            SetView(new Rect(_view.x - delta.x / rect.width * _view.width, _view.y - delta.y / rect.height * _view.height,
                _view.width, _view.height));
        }

        internal void EndDrag()
        {
            _panning = false;
        }

        private void StartSending(int target, string name)
        {
            _sending = true;
            _sendTarget = target;
            _sendName = name;
            ShowSelection();
        }

        private void StopSending()
        {
            _sending = false;
            _sendTarget = -1;
            _sendName = null;
        }

        private void Select(int id)
        {
            Dot dot;
            _selected = id;
            _slayArmed = -1;
            _selectedName = _dots.TryGetValue(id, out dot) ? dot.Name : ("Player " + id);
            _selectedDetail = (dot != null) ? dot.Detail : string.Empty;
            Pin(_selectMark, _selected);
            ShowSelection();
        }

        private void ShowSelection()
        {
            for (int i = _buttons.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_buttons.GetChild(i).gameObject);

            if (_sending)
            {
                _heading.text = (_sendTarget >= 0) ? "Teleport " + _sendName : "Teleport yourself";
                _info.text = (_sendTarget >= 0)
                    ? "Click the map where " + _sendName + " should go.\nRight-click cancels."
                    : "Click the map where you should go.\nRight-click cancels.";
                AddAction(_buttons, RyLibPlugin.Guid, "Cancel", StockIcon.Rules, delegate
                {
                    StopSending();
                    ShowSelection();
                }, false);
                return;
            }

            if (_selected < 0)
            {
                _heading.text = _page.Name;
                _info.text = "Click a player: actions\nScroll: zoom   Drag: pan";
                AddAction(_buttons, RyLibPlugin.Guid, "Teleport Me", StockIcon.Maps, delegate { StartSending(-1, null); }, false);
                return;
            }

            _heading.text = _selectedName;
            _info.text = _selectedDetail;

            int id = _selected;
            string name = _selectedName;
            AddAction(_buttons, RyLibPlugin.Guid, "Spectate", StockIcon.Unspawned, delegate { PlayerActions.Spectate(id); }, false);
            AddAction(_buttons, RyLibPlugin.Guid, "Go To", StockIcon.Maps, delegate { PlayerActions.GoTo(id); }, false);
            AddAction(_buttons, RyLibPlugin.Guid, "Bring", StockIcon.Players, delegate { PlayerActions.Bring(id); }, false);
            AddAction(_buttons, RyLibPlugin.Guid, "Teleport To", StockIcon.Maps, delegate { StartSending(id, name); }, false);

            bool armed = _slayArmed == id && Time.unscaledTime <= _slayArmedUntil;
            AddAction(_buttons, RyLibPlugin.Guid, armed ? "Confirm Slay" : "Slay", StockIcon.Skull, delegate
            {
                if (_slayArmed == id && Time.unscaledTime <= _slayArmedUntil)
                {
                    _slayArmed = -1;
                    PlayerActions.Slay(id);
                }
                else
                {
                    _slayArmed = id;
                    _slayArmedUntil = Time.unscaledTime + SlayArmSeconds;
                }

                ShowSelection();
            }, false);

            for (int i = 0; i < PlayerRow.ActionCount; i++)
            {
                if (!PlayerRow.ActionVisible(i)) continue;

                int index = i;
                AddAction(_buttons, PlayerRow.ActionOwner(i), PlayerRow.ActionLabel(i), StockIcon.Admin,
                    delegate { PlayerRow.RunAction(index, id); }, false);
            }

            AddAction(_buttons, RyLibPlugin.Guid, "Close", StockIcon.Rules, delegate
            {
                _selected = -1;
                _slayArmed = -1;
                Pin(_selectMark, _selected);
                ShowSelection();
            }, false);
        }

        private static void AddAction(RectTransform parent, string owner, string label, string icon, System.Action click, bool compact)
        {
            Control control = Controls.Make(Controls.Button(owner, label, click, icon), parent, Menu.ControlTemplate);
            if (control == null) return;

            LayoutElement size = Element(control.Root);
            size.minHeight = ButtonHeight;
            size.preferredHeight = ButtonHeight;
            size.flexibleHeight = 0f;
            size.minWidth = -1f;
            size.preferredWidth = -1f;
            size.flexibleWidth = 1f;

            if (!compact || control.Label == null) return;

            control.Label.enableWordWrapping = false;
            control.Label.fontSizeMax = control.Label.fontSize;
            control.Label.fontSizeMin = 9f;
            control.Label.enableAutoSizing = true;
        }

        private int Pick(Vector2 local)
        {
            int best = -1;
            float bestDistance = PickRadius * PickRadius;
            Rect rect = _image.rectTransform.rect;

            foreach (KeyValuePair<int, Dot> pair in _dots)
            {
                if (pair.Value.Hidden || !rect.Contains(pair.Value.Local)) continue;

                float distance = (pair.Value.Local - local).sqrMagnitude;
                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = pair.Key;
            }

            return best;
        }

        private bool Inside()
        {
            Vector2 local;
            return Mouse(out local);
        }

        private bool Mouse(out Vector2 local)
        {
            Canvas canvas = Menu.PanelCanvas;
            Camera camera = (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

            if (!ToImage(Input.mousePosition, camera, out local)) return false;
            return _image.rectTransform.rect.Contains(local);
        }

        private bool ToImage(Vector2 screen, Camera camera, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_image.rectTransform, screen, camera, out local);
        }

        private Vector2 ToLocal(Vector3 world)
        {
            Rect rect = _image.rectTransform.rect;
            float u = (world.x - _area.xMin) / _area.width;
            float v = (world.z - _area.yMin) / _area.height;
            float x = (u - _view.x) / _view.width;
            float y = (v - _view.y) / _view.height;
            return new Vector2((x - 0.5f) * rect.width, (y - 0.5f) * rect.height);
        }

        private Vector3 ToWorld(Vector2 local)
        {
            Rect rect = _image.rectTransform.rect;
            float u = _view.x + (local.x / rect.width + 0.5f) * _view.width;
            float v = _view.y + (local.y / rect.height + 0.5f) * _view.height;
            return new Vector3(_area.xMin + u * _area.width, 0f, _area.yMin + v * _area.height);
        }

        private static ClientRoundPlayerManager Players()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            return (client == null) ? null : client.clientRoundPlayerManager;
        }

        private static bool Where(RoundPlayer player, out Vector3 position)
        {
            position = Vector3.zero;
            if (player == null || player.PlayerBase == null || !player.PlayerBase.SpawnedAndAlive) return false;
            if (player.PlayerStartData == null) return false;

            if (player.PlayerObject != null)
            {
                position = player.PlayerObject.transform.position;
                return true;
            }

            if (player.PlayerTransformData == null) return false;
            position = player.PlayerTransformData.position;
            return true;
        }

        private static string NameOf(RoundPlayer player)
        {
            RoundPlayerInformation info = player.PlayerRoundInformation;
            if (info == null || info.InitialDetails == null || string.IsNullOrEmpty(info.InitialDetails.DisplayName))
            {
                return "Player " + player.NetworkPlayerID;
            }

            return info.InitialDetails.DisplayName;
        }

        private Dot TakeDot()
        {
            Dot dot;
            if (_dotPool.Count > 0)
            {
                dot = _dotPool[_dotPool.Count - 1];
                _dotPool.RemoveAt(_dotPool.Count - 1);
                return dot;
            }

            dot = new Dot();

            GameObject host = new GameObject("Dot", typeof(RectTransform));
            host.transform.SetParent(_dotRoot, false);
            dot.Root = (RectTransform)host.transform;
            dot.Root.anchorMin = dot.Root.anchorMax = new Vector2(0.5f, 0.5f);
            dot.Root.pivot = new Vector2(0.5f, 0.5f);
            dot.Root.sizeDelta = new Vector2(IconSize, IconSize);

            dot.Halo = Mark(dot.Root, null, IconSize * HaloScale);
            dot.Halo.gameObject.SetActive(false);

            dot.Icon = Mark(dot.Root, Glyphs.Class(ClassShape.Circle), IconSize);
            Stretch(dot.Icon.rectTransform);
            return dot;
        }

        private void Park(Dot dot)
        {
            if (dot.Root != null) dot.Root.gameObject.SetActive(false);
            _dotPool.Add(dot);
        }

        private Overlay TakeOverlay(int index)
        {
            while (_overlays.Count <= index)
            {
                Overlay overlay = new Overlay();
                overlay.Image = Mark(_overlayRoot, Glyphs.Ring, 20f);
                _overlays.Add(overlay);
            }

            return _overlays[index];
        }

        private static float DefaultSize(MapShape shape)
        {
            switch (shape)
            {
                case MapShape.Flag: return IconSize * 1.1f;
                case MapShape.Dot: return IconSize * 0.6f;
                default: return IconSize * 1.6f;
            }
        }

        private void HideTip()
        {
            if (_tip != null && _tip.gameObject.activeSelf) _tip.gameObject.SetActive(false);
        }

        private LegendRow Row(RectTransform parent, Sprite sprite, Color colour, string label)
        {
            GameObject host = new GameObject("Legend Row", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            Height(host, LegendRowHeight);
            RectTransform line = (RectTransform)host.transform;

            Image icon = Mark(line, sprite, LegendIcon);
            icon.color = colour;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;

            TMP_Text text = Text(_template, line, 14f, TextAlignmentOptions.Left);
            LayoutElement stray = text.GetComponent<LayoutElement>();
            if (stray != null) stray.ignoreLayout = true;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.offsetMin = new Vector2(LegendIcon + 7f, 0f);
            textRect.offsetMax = Vector2.zero;
            text.margin = Vector4.zero;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 14f;
            text.text = label;

            LegendRow row = new LegendRow();
            row.Root = host;
            row.Icon = icon;
            row.Text = text;
            return row;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text.text != value) text.text = value;
        }

        private static Image Mark(RectTransform parent, Sprite sprite, float size)
        {
            GameObject host = new GameObject("Mark", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            Image image = host.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Layer(string name, RectTransform parent)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)host.transform;
            Stretch(rect);
            return rect;
        }

        private static RectTransform Stack(string name, RectTransform parent, float spacing)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            VerticalLayoutGroup stack = host.AddComponent<VerticalLayoutGroup>();
            stack.spacing = spacing;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            return (RectTransform)host.transform;
        }

        private static RectTransform Pair(string name, RectTransform parent)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            HorizontalLayoutGroup pair = host.AddComponent<HorizontalLayoutGroup>();
            pair.spacing = 6f;
            pair.childAlignment = TextAnchor.MiddleCenter;
            pair.childControlWidth = true;
            pair.childControlHeight = true;
            pair.childForceExpandWidth = true;
            pair.childForceExpandHeight = false;
            Height(host, ButtonHeight);
            return (RectTransform)host.transform;
        }

        private static RectTransform Box(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static LayoutElement Element(GameObject host)
        {
            LayoutElement element = host.GetComponent<LayoutElement>();
            if (element == null) element = host.AddComponent<LayoutElement>();
            return element;
        }

        private static void Height(GameObject host, float height)
        {
            LayoutElement element = Element(host);
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        private static TMP_Text Text(TMP_Text template, RectTransform parent, float size, TextAlignmentOptions alignment)
        {
            TMP_Text text = UnityEngine.Object.Instantiate(template, parent);
            text.name = "Text";
            text.text = string.Empty;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.raycastTarget = false;

            ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
            if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);

            return text;
        }
    }
}
