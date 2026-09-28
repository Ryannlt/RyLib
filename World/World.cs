using System;
using System.Collections.Generic;
using UnityEngine;

namespace RyLib
{
    public struct WorldMark
    {
        public string Key;
        public Transform Follow;
        public Vector3 Position;
        public GameObject Glow;
        public Color Colour;
        public string Label;
        public float LabelHeight;
        public float RingRadius;
        public float BeamHeight;
    }

    public static class World
    {
        private const float NearMetres = 40f;
        private const float MidMetres = 150f;
        private const float OffScreenSeconds = 0.25f;
        private const float MidSeconds = 0.05f;
        private const float FarSeconds = 0.1f;

        private sealed class LayerSpec
        {
            public string Owner;
            public Action<List<WorldMark>> Fill;
            public float Interval;
            public float Next;
            public readonly List<WorldMark> Marks = new List<WorldMark>();
        }

        internal sealed class Live
        {
            public string Id;
            public WorldMark Mark;
            public Ring Ring;
            public Label Label;
            public float NextMove;
            public int Seen;
        }

        private static readonly List<LayerSpec> Layers = new List<LayerSpec>();
        private static readonly Dictionary<string, Live> Marks = new Dictionary<string, Live>(StringComparer.Ordinal);
        private static readonly List<string> Gone = new List<string>();

        private static GameObject _root;
        private static int _generation;

        public static bool Layer(string owner, Action<List<WorldMark>> fill, float hz = 10f)
        {
            if (!Owners.Valid(owner, "World.Layer")) return false;

            if (fill == null)
            {
                Log.Warn(owner + ": World.Layer needs a fill callback.");
                return false;
            }

            for (int i = 0; i < Layers.Count; i++)
            {
                if (!Owners.Same(Layers[i].Owner, owner)) continue;

                Log.Warn(owner + " already has a world layer, so the second one was ignored.");
                return false;
            }

            LayerSpec layer = new LayerSpec();
            layer.Owner = owner;
            layer.Fill = fill;
            layer.Interval = 1f / Mathf.Clamp(hz, 1f, 60f);
            Layers.Add(layer);

            Log.Info(owner + " added a world layer at " + Mathf.Clamp(hz, 1f, 60f) + " Hz.");
            return true;
        }

        internal static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    _root = new GameObject("RyLib_World");
                    UnityEngine.Object.DontDestroyOnLoad(_root);
                }

                return _root.transform;
            }
        }

        internal static void Tick()
        {
            if (Layers.Count == 0) return;

            float now = Time.unscaledTime;
            bool refilled = false;

            for (int i = 0; i < Layers.Count; i++)
            {
                LayerSpec layer = Layers[i];
                if (now < layer.Next) continue;

                layer.Next = now + layer.Interval;
                layer.Marks.Clear();
                Owners.Run(layer.Owner, "world layer", delegate { layer.Fill(layer.Marks); });
                refilled = true;
            }

            if (refilled) Reconcile();
            Follow(now);
        }

        private static void Reconcile()
        {
            _generation++;
            Glows.Begin();

            for (int i = 0; i < Layers.Count; i++)
            {
                LayerSpec layer = Layers[i];
                List<WorldMark> marks = layer.Marks;

                for (int j = 0; j < marks.Count; j++)
                {
                    WorldMark mark = marks[j];
                    if (string.IsNullOrEmpty(mark.Key)) continue;

                    string id = layer.Owner + "|" + mark.Key;

                    Live live;
                    if (!Marks.TryGetValue(id, out live))
                    {
                        live = new Live();
                        live.Id = id;
                        Marks[id] = live;
                    }

                    if (live.Seen == _generation) continue;
                    live.Seen = _generation;
                    live.Mark = mark;
                    live.NextMove = 0f;

                    Rings.Apply(live);
                    Labels.Apply(live);
                    if (mark.Glow != null) Glows.Want(mark.Glow, mark.Colour);
                }
            }

            Glows.End();

            Gone.Clear();
            foreach (KeyValuePair<string, Live> pair in Marks)
            {
                if (pair.Value.Seen != _generation) Gone.Add(pair.Key);
            }

            for (int i = 0; i < Gone.Count; i++)
            {
                Live live = Marks[Gone[i]];
                Rings.Release(live);
                Labels.Release(live);
                Marks.Remove(Gone[i]);
            }
        }

        private static void Follow(float now)
        {
            if (Marks.Count == 0) return;

            Camera camera = GameView.ActiveCamera;

            foreach (KeyValuePair<string, Live> pair in Marks)
            {
                Live live = pair.Value;
                Vector3 position = Where(live.Mark);

                if (live.Ring != null && now >= live.NextMove)
                {
                    Rings.Move(live, position);
                    live.NextMove = now + Interval(camera, position);
                }

                if (live.Label != null) Labels.Move(live, camera, position);
            }
        }

        private static Vector3 Where(WorldMark mark)
        {
            return (mark.Follow != null) ? mark.Follow.position : mark.Position;
        }

        private static float Interval(Camera camera, Vector3 position)
        {
            if (camera == null) return OffScreenSeconds;

            Vector3 viewport = camera.WorldToViewportPoint(position);
            bool inView = viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f &&
                          viewport.y > -0.1f && viewport.y < 1.1f;

            if (!inView) return OffScreenSeconds;
            if (viewport.z < NearMetres) return 0f;
            return (viewport.z < MidMetres) ? MidSeconds : FarSeconds;
        }
    }
}
