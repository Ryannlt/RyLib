using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HighlightPlus;
using UnityEngine;
using UnityEngine.Rendering;

namespace RyLib
{
    internal static class Glows
    {
        private const int MaxGlows = 16;
        private const float RescanSeconds = 2f;
        private const int UiLayer = 5;
        private const float DarkLuminance = 0.35f;
        private const int MaxReports = 5;

        private static readonly MethodInfo Materials =
            AccessTools.Method(typeof(HighlightEffect), "CheckGeometrySupportDependencies");

        private sealed class Glow
        {
            public GameObject Target;
            public HighlightEffect Effect;
            public Renderer[] Renderers = new Renderer[0];
            public Color Colour;
            public float NextScan;
            public bool Wanted;
            public int Style;
        }

        private static readonly Dictionary<int, Glow> Live = new Dictionary<int, Glow>();
        private static readonly List<Glow> Pool = new List<Glow>();
        private static readonly List<int> Drop = new List<int>();
        private static readonly List<Renderer> Found = new List<Renderer>();
        private static readonly List<Renderer> Kept = new List<Renderer>();
        private static readonly HashSet<Renderer> Skip = new HashSet<Renderer>();

        private static int _wanted;
        private static int _reports;

        public static void Begin()
        {
            _wanted = 0;
            foreach (KeyValuePair<int, Glow> pair in Live) pair.Value.Wanted = false;
        }

        public static void Want(GameObject target, Color colour)
        {
            if (target == null || _wanted >= MaxGlows) return;

            int id = target.GetInstanceID();

            Glow glow;
            if (!Live.TryGetValue(id, out glow))
            {
                glow = Take();
                if (glow == null) return;

                glow.Target = target;
                glow.Renderers = new Renderer[0];
                glow.NextScan = 0f;
                glow.Colour = new Color(-1f, -1f, -1f, -1f);
                Live[id] = glow;
            }

            if (glow.Wanted) return;
            glow.Wanted = true;
            _wanted++;

            try
            {
                if (glow.Style != GlowStyle.Version)
                {
                    Style(glow.Effect);
                    glow.Style = GlowStyle.Version;
                    glow.Colour = new Color(-1f, -1f, -1f, -1f);
                }

                if (glow.Colour != colour) Paint(glow, colour);
                if (Time.unscaledTime >= glow.NextScan) Scan(glow);
                if (glow.Renderers.Length > 0 && !glow.Effect.highlighted) glow.Effect.SetHighlighted(true);
            }
            catch (Exception error)
            {
                Report(error);
                Live.Remove(id);
                Discard(glow);
            }
        }

        public static void End()
        {
            Drop.Clear();
            foreach (KeyValuePair<int, Glow> pair in Live)
            {
                if (!pair.Value.Wanted || pair.Value.Target == null) Drop.Add(pair.Key);
            }

            for (int i = 0; i < Drop.Count; i++)
            {
                Glow glow = Live[Drop[i]];
                Live.Remove(Drop[i]);
                Release(glow);
            }
        }

        internal static bool Dark(Color colour)
        {
            return 0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b < DarkLuminance;
        }

        private static void Paint(Glow glow, Color colour)
        {
            glow.Colour = colour;

            bool dark = Dark(colour);
            float boost = GlowStyle.Brightness.Value;
            Color bright = dark ? colour : new Color(colour.r * boost, colour.g * boost, colour.b * boost, 1f);

            HighlightEffect effect = glow.Effect;
            effect.glowBlendMode = dark ? GlowBlendMode.AlphaBlending : GlowBlendMode.Additive;
            effect.outlineColor = bright;
            effect.SetGlowColor(bright);
        }

        private static void Style(HighlightEffect effect)
        {
            float outline = GlowStyle.OutlineWidth.Value;

            effect.constantWidth = !GlowStyle.ScaleWithDistance.Value;
            effect.outline = (outline > 0f) ? 1f : 0f;
            effect.outlineWidth = outline;
            effect.glow = GlowStyle.Glow.Value;
            effect.glowWidth = GlowStyle.GlowWidth.Value;
            effect.glowAnimationSpeed = 0f;
            effect.glowDithering = 0f;
            effect.UpdateMaterialProperties();
        }

        private static void Scan(Glow glow)
        {
            glow.NextScan = Time.unscaledTime + RescanSeconds;

            Renderer[] renderers = Collect(glow.Target);
            if (Same(renderers, glow.Renderers)) return;

            glow.Renderers = renderers;
            if (renderers.Length == 0)
            {
                glow.Effect.SetHighlighted(false);
                return;
            }

            if (Materials != null) Materials.Invoke(glow.Effect, null);

            glow.Effect.SetTargets(glow.Target.transform, renderers);
            glow.Effect.UpdateMaterialProperties();
            glow.Effect.SetHighlighted(true);
        }

        private static bool Same(Renderer[] left, Renderer[] right)
        {
            if (left.Length != right.Length) return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }

        private static Renderer[] Collect(GameObject target)
        {
            Found.Clear();
            Kept.Clear();
            Skip.Clear();

            target.GetComponentsInChildren(false, Found);

            LODGroup[] groups = target.GetComponentsInChildren<LODGroup>(false);
            for (int g = 0; g < groups.Length; g++)
            {
                LOD[] lods = groups[g].GetLODs();
                for (int l = 1; l < lods.Length; l++)
                {
                    Renderer[] levels = lods[l].renderers;
                    for (int r = 0; r < levels.Length; r++)
                    {
                        if (levels[r] != null) Skip.Add(levels[r]);
                    }
                }
            }

            for (int i = 0; i < Found.Count; i++)
            {
                Renderer renderer = Found[i];
                if (renderer == null || !renderer.enabled || Skip.Contains(renderer)) continue;
                if (renderer is LineRenderer || renderer is TrailRenderer) continue;
                if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                if (renderer.gameObject.layer == UiLayer) continue;
                if (renderer.GetComponent("Cloth") != null) continue;

                string type = renderer.GetType().Name;
                if (type == "ParticleSystemRenderer" || type == "BillboardRenderer") continue;

                Kept.Add(renderer);
            }

            return Kept.ToArray();
        }

        private static Glow Take()
        {
            while (Pool.Count > 0)
            {
                Glow pooled = Pool[Pool.Count - 1];
                Pool.RemoveAt(Pool.Count - 1);
                if (pooled.Effect != null) return pooled;
            }

            try
            {
                GameObject host = new GameObject("RyLib_Glow");
                host.transform.SetParent(World.Root, false);

                HighlightEffect effect = host.AddComponent<HighlightEffect>();

                effect.outlineQuality = HighlightPlus.QualityLevel.High;
                effect.outlineBlurPasses = 1;
                effect.outlineSharpness = 1f;
                effect.outlineVisibility = Visibility.AlwaysOnTop;

                effect.glowQuality = HighlightPlus.QualityLevel.High;
                effect.glowDownsampling = 1;
                effect.glowVisibility = Visibility.AlwaysOnTop;
                effect.glowAnimationSpeed = 0f;
                effect.glowDithering = 0f;

                effect.overlay = 0f;
                effect.innerGlow = 0f;
                effect.targetFX = false;
                effect.seeThrough = SeeThroughMode.Never;
                effect.ignoreObjectVisibility = true;
                effect.cameraDistanceFade = false;
                effect.fadeInDuration = 0f;
                effect.fadeOutDuration = 0f;
                Style(effect);

                Glow glow = new Glow();
                glow.Effect = effect;
                glow.Style = GlowStyle.Version;
                return glow;
            }
            catch (Exception error)
            {
                Report(error);
                return null;
            }
        }

        private static void Release(Glow glow)
        {
            glow.Target = null;
            glow.Renderers = new Renderer[0];
            if (glow.Effect == null) return;

            try
            {
                glow.Effect.SetHighlighted(false);
                Pool.Add(glow);
            }
            catch (Exception error)
            {
                Report(error);
                Discard(glow);
            }
        }

        private static void Discard(Glow glow)
        {
            glow.Target = null;
            glow.Renderers = new Renderer[0];
            if (glow.Effect != null) UnityEngine.Object.Destroy(glow.Effect.gameObject);
            glow.Effect = null;
        }

        private static void Report(Exception error)
        {
            _reports++;
            if (_reports > MaxReports) return;

            Log.Error("a glow failed and was dropped (" + _reports + "): " + error);
            if (_reports == MaxReports) Log.Warn("further glow failures will not be logged this session.");
        }
    }
}
