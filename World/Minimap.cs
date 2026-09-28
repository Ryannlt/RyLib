using System;
using System.Collections.Generic;
using HoldfastGame;
using UnityEngine;
using UnityEngine.UI;

namespace RyLib
{
    public struct MinimapMark
    {
        public string Key;
        public Transform Follow;
        public MapShape Shape;
        public FactionCountry Faction;
        public Color Colour;
        public bool AlwaysShow;
        public float Size;
    }

    public static class Minimap
    {
        public static readonly Color Friendly = new Color(0.33f, 0.62f, 1f);
        public static readonly Color Enemy = new Color(0.93f, 0.2f, 0.17f);

        private const float DefaultSize = 24f;
        private const float EnsureSeconds = 0.25f;
        private const int MaxReports = 5;

        private sealed class LayerSpec
        {
            public string Owner;
            public Action<List<MinimapMark>> Fill;
            public float Interval;
            public float Next;
            public readonly List<MinimapMark> Marks = new List<MinimapMark>();
        }

        private static readonly List<LayerSpec> Layers = new List<LayerSpec>();
        private static readonly Dictionary<string, MinimapPointer> Live = new Dictionary<string, MinimapPointer>(StringComparer.Ordinal);
        private static readonly List<MinimapPointer> Pool = new List<MinimapPointer>();
        private static readonly List<string> Gone = new List<string>();
        private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);

        private static UIMinimapPanelTracking _tracking;
        private static float _nextEnsure;
        private static int _reports;

        public static bool Layer(string owner, Action<List<MinimapMark>> fill, float hz = 4f)
        {
            if (!Owners.Valid(owner, "Minimap.Layer")) return false;

            if (fill == null)
            {
                Log.Warn(owner + ": Minimap.Layer needs a fill callback.");
                return false;
            }

            for (int i = 0; i < Layers.Count; i++)
            {
                if (!Owners.Same(Layers[i].Owner, owner)) continue;

                Log.Warn(owner + " already has a minimap layer, so the second one was ignored.");
                return false;
            }

            LayerSpec layer = new LayerSpec();
            layer.Owner = owner;
            layer.Fill = fill;
            layer.Interval = 1f / Mathf.Clamp(hz, 1f, 30f);
            Layers.Add(layer);

            Log.Info(owner + " added a minimap layer.");
            return true;
        }

        internal static void Tick()
        {
            if (Layers.Count == 0) return;

            UIMinimapPanelTracking tracking = CurrentTracking();
            if (tracking != _tracking)
            {
                Forget();
                _tracking = tracking;
            }

            if (_tracking == null) return;

            float now = Time.unscaledTime;
            bool refilled = false;

            for (int i = 0; i < Layers.Count; i++)
            {
                LayerSpec layer = Layers[i];
                if (now < layer.Next) continue;

                layer.Next = now + layer.Interval;
                layer.Marks.Clear();
                Owners.Run(layer.Owner, "minimap layer", delegate { layer.Fill(layer.Marks); });
                refilled = true;
            }

            try
            {
                if (refilled) Reconcile();

                if (now >= _nextEnsure)
                {
                    _nextEnsure = now + EnsureSeconds;
                    Ensure();
                }
            }
            catch (Exception error)
            {
                _reports++;
                if (_reports <= MaxReports) Log.Error("minimap markers hit an error (" + _reports + "): " + error);
            }
        }

        private static UIMinimapPanelTracking CurrentTracking()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null) return null;

            UIMinimapPanelTracking tracking = client.uiMinimapPanelTracking;
            return (tracking == null) ? null : tracking;
        }

        private static void Reconcile()
        {
            Seen.Clear();

            for (int i = 0; i < Layers.Count; i++)
            {
                LayerSpec layer = Layers[i];
                for (int j = 0; j < layer.Marks.Count; j++)
                {
                    MinimapMark mark = layer.Marks[j];
                    if (string.IsNullOrEmpty(mark.Key) || mark.Follow == null) continue;

                    string id = layer.Owner + "|" + mark.Key;
                    if (!Seen.Add(id)) continue;

                    MinimapPointer pointer;
                    if (!Live.TryGetValue(id, out pointer) || pointer == null)
                    {
                        pointer = Take();
                        if (pointer == null) continue;
                        Live[id] = pointer;
                    }

                    if (mark.Size <= 0f) mark.Size = DefaultSize;
                    pointer.Apply(mark);
                }
            }

            Gone.Clear();
            foreach (KeyValuePair<string, MinimapPointer> pair in Live)
            {
                if (!Seen.Contains(pair.Key)) Gone.Add(pair.Key);
            }

            for (int i = 0; i < Gone.Count; i++)
            {
                MinimapPointer pointer = Live[Gone[i]];
                Live.Remove(Gone[i]);
                Release(pointer);
            }
        }

        private static void Ensure()
        {
            List<UIMinimapPanelTrackingPointer> all = _tracking.allTrackingPointers;

            foreach (KeyValuePair<string, MinimapPointer> pair in Live)
            {
                MinimapPointer pointer = pair.Value;
                if (pointer == null || !pointer.Wanted) continue;
                if (!all.Contains(pointer)) all.Add(pointer);
                if (pointer.transform.GetSiblingIndex() != pointer.transform.parent.childCount - 1) pointer.transform.SetAsLastSibling();
            }
        }

        private static MinimapPointer Take()
        {
            while (Pool.Count > 0)
            {
                MinimapPointer pooled = Pool[Pool.Count - 1];
                Pool.RemoveAt(Pool.Count - 1);
                if (pooled != null) return pooled;
            }

            Transform parent = _tracking.playerPointParentTransform;
            if (parent == null) return null;

            GameObject host = new GameObject("RyLib_MinimapMark", typeof(RectTransform));
            host.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(DefaultSize, DefaultSize);

            Image image = host.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            host.AddComponent<CanvasGroup>();

            MinimapPointer pointer = host.AddComponent<MinimapPointer>();
            pointer.trackingImage = image;
            pointer.clampAtEdge = true;
            return pointer;
        }

        private static void Release(MinimapPointer pointer)
        {
            if (pointer == null) return;

            pointer.Wanted = false;
            if (_tracking != null) _tracking.allTrackingPointers.Remove(pointer);
            pointer.Unhook();
            Pool.Add(pointer);
        }

        private static void Forget()
        {
            if (_tracking != null)
            {
                foreach (KeyValuePair<string, MinimapPointer> pair in Live)
                {
                    if (pair.Value != null) _tracking.allTrackingPointers.Remove(pair.Value);
                }
            }

            foreach (KeyValuePair<string, MinimapPointer> pair in Live)
            {
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value.gameObject);
            }

            for (int i = 0; i < Pool.Count; i++)
            {
                if (Pool[i] != null) UnityEngine.Object.Destroy(Pool[i].gameObject);
            }

            Live.Clear();
            Pool.Clear();
        }
    }

    internal sealed class MinimapPointer : UIMinimapPanelTrackingPointer
    {
        private static readonly Vector2 FlagPivot = new Vector2(0.28f, 0.08f);
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        internal MinimapMark Mark;
        internal bool Wanted;

        public override bool FollowTrackingTransformRotation
        {
            get { return false; }
        }

        protected override bool _ShouldUpdate
        {
            get { return Wanted; }
        }

        internal void Apply(MinimapMark mark)
        {
            Mark = mark;
            Wanted = true;

            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            if (!Mathf.Approximately(rect.sizeDelta.x, mark.Size)) rect.sizeDelta = new Vector2(mark.Size, mark.Size);

            Vector2 pivot = (mark.Shape == MapShape.Flag) ? FlagPivot : Centre;
            if (rect.pivot != pivot) rect.pivot = pivot;

            Image image = GetComponent<Image>();
            Sprite sprite = Glyphs.Mark(mark.Shape);
            if (image != null && image.sprite != sprite) image.sprite = sprite;
        }

        public override Transform GetTrackingTransform()
        {
            return (Wanted && Mark.Follow != null) ? Mark.Follow : null;
        }

        protected override void _Awake()
        {
        }

        protected override bool _ShouldSettingsShowIndicator(FactionCountry factionCountry)
        {
            return Wanted && Mark.Follow != null;
        }

        protected override void GetTrackerOverrideData(MiniMapPanelTrackingPointerProperties trackingProperties,
            FactionCountry playerFaction, bool factionVsFaction, out Sprite sprite, out float alpha, out Color color)
        {
            sprite = Glyphs.Mark(Mark.Shape);
            color = Tint();
            alpha = Shown(trackingProperties, playerFaction, factionVsFaction) ? 1f : 0f;
        }

        private Color Tint()
        {
            FactionCountry viewer = ViewerFaction();
            if (Mark.Faction == FactionCountry.None || viewer == FactionCountry.None) return Mark.Colour;
            return (Mark.Faction == viewer) ? Minimap.Friendly : Minimap.Enemy;
        }

        private FactionCountry ViewerFaction()
        {
            if (clientRoundPlayerManager == null) return FactionCountry.None;

            ClientRoundPlayer player = clientRoundPlayerManager.GetOurRoundPlayer();
            if (player == null || player.PlayerStartData == null) return FactionCountry.None;
            return player.PlayerStartData.Faction;
        }

        private bool Shown(MiniMapPanelTrackingPointerProperties properties, FactionCountry playerFaction, bool factionVsFaction)
        {
            if (!Wanted) return false;
            if (Mark.AlwaysShow || Mark.Faction == FactionCountry.None) return true;

            if (factionVsFaction && Mark.Faction == playerFaction)
            {
                return properties == null || !properties.hideAllysOnDistance ||
                       properties.hideAllysDistance >= lastCalculatedDistanceBetweenEntities;
            }

            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null) return false;

            FactionCountry lastStand = (uiMinimapPanelTracking == null) ? FactionCountry.None : uiMinimapPanelTracking.LastStandFactionCountry;
            FactionCountry revealed = (client.commonGlobalVariables == null) ? FactionCountry.None : client.commonGlobalVariables.RevealedFaction;

            if (lastStand == Mark.Faction || revealed == Mark.Faction) return true;
            if (RevealsBoth(lastStand) || RevealsBoth(revealed)) return true;

            return client.allChargeManager != null && client.allChargeManager.AllChargeActive;
        }

        private bool RevealsBoth(FactionCountry faction)
        {
            if (faction == FactionCountry.None || clientGameManager == null) return false;

            RoundGameDetails details = clientGameManager.CurrentRoundGameDetails;
            if (details == null) return false;

            return faction != details.AttackingFaction && faction != details.DefendingFaction;
        }
    }

    internal static class FlagGlyph
    {
        private const int Size = 64;

        private static Sprite _sprite;

        public static Sprite Sprite
        {
            get
            {
                if (_sprite == null) _sprite = Draw();
                return _sprite;
            }
        }

        private static Sprite Draw()
        {
            bool[] shape = new bool[Size * Size];

            for (int y = 4; y < 60; y++)
            {
                for (int x = 13; x < 18; x++) shape[y * Size + x] = true;
            }

            for (int y = 32; y < 60; y++)
            {
                float wave = Mathf.Sin((y - 32) / 27f * Mathf.PI) * 3f;
                float notch = Mathf.Max(0f, 9f - Mathf.Abs(y - 46f)) * 0.9f;
                int right = Mathf.RoundToInt(56f + wave - notch);

                for (int x = 18; x < right; x++)
                {
                    int shift = Mathf.RoundToInt(Mathf.Sin((x - 18) / 38f * Mathf.PI * 2f) * 1.5f);
                    int row = y + shift;
                    if (row >= 0 && row < Size) shape[row * Size + x] = true;
                }
            }

            Color32[] pixels = new Color32[Size * Size];
            Color32 fill = new Color32(255, 255, 255, 255);
            Color32 edge = new Color32(18, 18, 20, 255);
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int index = y * Size + x;
                    if (shape[index]) pixels[index] = fill;
                    else if (Near(shape, x, y, 2)) pixels[index] = edge;
                    else pixels[index] = clear;
                }
            }

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.HideAndDontSave;

            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.28f, 0.08f), 100f);
            sprite.name = "RyLib Flag";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool Near(bool[] shape, int x, int y, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int row = y + dy;
                if (row < 0 || row >= Size) continue;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int column = x + dx;
                    if (column < 0 || column >= Size) continue;
                    if (shape[row * Size + column]) return true;
                }
            }

            return false;
        }
    }
}
