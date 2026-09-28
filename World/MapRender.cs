using System;
using System.Collections.Generic;
using HarmonyLib;
using HoldfastGame;
using UnityEngine;

namespace RyLib
{
    internal static class MapRender
    {
        private const int LongSide = 2048;
        private const int DetailedLongSide = 4096;
        private const float DetailedShare = 0.6f;
        private const float BattlePadShare = 0.25f;
        private const float BattlePadMinimum = 120f;
        private const int Samples = 32;
        private const float Headroom = 150f;
        private const float FieldOfView = 60f;
        private const float CaptureLodBias = 4f;

        private static readonly int[] HiddenLayers = { 5, 7, 8, 11, 12, 26, 29 };
        private static readonly string[] HiddenLayerWords = { "player", "character", "actor", "ragdoll" };
        private static readonly string[] FogKeywords = { "FOG_LINEAR", "FOG_EXP", "FOG_EXP2" };
        private static readonly string[] MutedWords = { "distancefog", "depthoffield" };
        private static readonly string[] MutedTypes = { "HeightFogGlobal", "HeightFogOverride", "VolumetricFog" };

        private static readonly AccessTools.FieldRef<UIMinimapPanelMapBorderAreaTrackingPointer, BoxCollider> BorderField =
            AccessTools.FieldRefAccess<UIMinimapPanelMapBorderAreaTrackingPointer, BoxCollider>("borderArea");

        internal enum State
        {
            Empty,
            Ready,
            Failed
        }

        internal static State Status;
        internal static Texture2D Texture;
        internal static Rect Area;
        internal static Rect Battle;
        internal static float Top;

        private static float _bottom;
        private static float _ground;
        private static Color _flat;

        private sealed class Scene
        {
            public bool Fog;
            public float Ambient;
            public float LodBias;
            public readonly List<string> Keywords = new List<string>();
            public readonly List<Behaviour> Muted = new List<Behaviour>();
            public readonly List<Terrain> Terrains = new List<Terrain>();
            public readonly List<bool> Heightmaps = new List<bool>();
            public readonly List<bool> Foliage = new List<bool>();
            public readonly List<float> TreeDistances = new List<float>();
        }

        internal static void Request()
        {
            if (Status != State.Empty) return;

            if (!Measure())
            {
                Status = State.Failed;
                Log.Warn("no map border or terrain was found, so the map shows a plain grid sized to the players.");
                return;
            }

            try
            {
                Status = Capture() ? State.Ready : State.Failed;
            }
            catch (Exception error)
            {
                Log.Error("the overhead map could not be rendered: " + error);
                Status = State.Failed;
            }
        }

        internal static void Invalidate()
        {
            if (Texture != null)
            {
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }

            Status = State.Empty;
        }

        private static bool Measure()
        {
            Bounds bounds = new Bounds();
            bool found = false;

            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            UIMinimapPanelTracking tracking = (client == null) ? null : client.uiMinimapPanelTracking;
            if (tracking != null)
            {
                for (int i = 0; i < tracking.allTrackingPointers.Count; i++)
                {
                    UIMinimapPanelMapBorderAreaTrackingPointer border =
                        tracking.allTrackingPointers[i] as UIMinimapPanelMapBorderAreaTrackingPointer;
                    if (border == null) continue;

                    BoxCollider box = BorderField(border);
                    if (box == null) continue;

                    if (found) bounds.Encapsulate(box.bounds);
                    else bounds = box.bounds;
                    found = true;
                }
            }

            bool fromBorder = found;
            float top = float.MinValue;
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                Vector3 size = terrain.terrainData.size;
                Vector3 origin = terrain.transform.position;
                top = Mathf.Max(top, origin.y + size.y);

                if (fromBorder) continue;

                Bounds piece = new Bounds(origin + size * 0.5f, size);
                if (found) bounds.Encapsulate(piece);
                else bounds = piece;
                found = true;
            }

            if (!found || bounds.size.x < 10f || bounds.size.z < 10f) return false;

            float margin = Mathf.Max(bounds.size.x, bounds.size.z) * 0.02f;
            Area = new Rect(bounds.min.x - margin, bounds.min.z - margin, bounds.size.x + margin * 2f, bounds.size.z + margin * 2f);
            Top = Mathf.Max(top, bounds.max.y) + Headroom;
            _bottom = Mathf.Min(bounds.min.y, 0f) - 100f;
            _ground = Ground(terrains, bounds.center.y);
            Battle = BattleArea();
            return true;
        }

        private static Rect BattleArea()
        {
            ClientComponentReferenceManager client = ClientComponentReferenceManager.ClientInstance;
            if (client == null || client.clientSpawnSectionManager == null) return Area;

            Dictionary<int, SpawnSection> sections = client.clientSpawnSectionManager.availableSpawnSections;
            if (sections == null) return Area;

            int count = 0;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            foreach (SpawnSection section in sections.Values)
            {
                if (section == null) continue;

                Vector3 position = section.transform.position;
                if (position.sqrMagnitude < 0.01f) continue;

                Vector2 flat = new Vector2(position.x, position.z);
                if (!Area.Contains(flat)) continue;

                if (count == 0)
                {
                    min = max = flat;
                }
                else
                {
                    min = Vector2.Min(min, flat);
                    max = Vector2.Max(max, flat);
                }

                count++;
            }

            if (count < 2) return Area;

            Vector2 size = max - min;
            float pad = Mathf.Max(Mathf.Max(size.x, size.y) * BattlePadShare, BattlePadMinimum);
            return Rect.MinMaxRect(Mathf.Max(min.x - pad, Area.xMin), Mathf.Max(min.y - pad, Area.yMin),
                Mathf.Min(max.x + pad, Area.xMax), Mathf.Min(max.y + pad, Area.yMax));
        }

        private static float Ground(Terrain[] terrains, float fallback)
        {
            float sum = 0f;
            int count = 0;

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    Vector3 point = new Vector3(Area.xMin + Area.width * (x + 0.5f) / 5f, 0f, Area.yMin + Area.height * (y + 0.5f) / 5f);
                    for (int i = 0; i < terrains.Length; i++)
                    {
                        Terrain terrain = terrains[i];
                        if (terrain == null || terrain.terrainData == null) continue;

                        Vector3 origin = terrain.transform.position;
                        Vector3 size = terrain.terrainData.size;
                        if (point.x < origin.x || point.z < origin.z || point.x > origin.x + size.x || point.z > origin.z + size.z) continue;

                        sum += terrain.SampleHeight(point) + origin.y;
                        count++;
                        break;
                    }
                }
            }

            return (count > 0) ? sum / count : fallback;
        }

        private static bool Capture()
        {
            Scene scene = Prepare();
            try
            {
                if (Shoot(true))
                {
                    Log.Info("map rendered: " + Area.width.ToString("0") + " x " + Area.height.ToString("0") + " m, perspective, " +
                             "average colour " + _flat + ", muted " + Describe(scene) + ".");
                    return true;
                }

                if (Shoot(false))
                {
                    Log.Info("map rendered: " + Area.width.ToString("0") + " x " + Area.height.ToString("0") + " m, orthographic, " +
                             "average colour " + _flat + ", muted " + Describe(scene) + ".");
                    return true;
                }

                Log.Warn("the overhead map came back as one flat colour both ways (colour " + _flat + ", culling mask 0x" +
                         Mask().ToString("X8") + ", muted " + Describe(scene) + "), so the map shows a plain grid instead.");
                return false;
            }
            finally
            {
                Restore(scene);
            }
        }

        private static Scene Prepare()
        {
            Scene scene = new Scene();

            scene.Fog = RenderSettings.fog;
            scene.Ambient = RenderSettings.ambientIntensity;
            scene.LodBias = QualitySettings.lodBias;
            RenderSettings.fog = false;
            RenderSettings.ambientIntensity = Mathf.Max(RenderSettings.ambientIntensity, 1f);
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, CaptureLodBias);

            for (int i = 0; i < FogKeywords.Length; i++)
            {
                if (!Shader.IsKeywordEnabled(FogKeywords[i])) continue;
                Shader.DisableKeyword(FogKeywords[i]);
                scene.Keywords.Add(FogKeywords[i]);
            }

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled) continue;
                if (!MutedType(behaviour.GetType().Name) && !Muted(behaviour.GetType().Name) && !Muted(behaviour.gameObject.name)) continue;

                behaviour.enabled = false;
                scene.Muted.Add(behaviour);
            }

            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;

                scene.Terrains.Add(terrain);
                scene.Heightmaps.Add(terrain.drawHeightmap);
                scene.Foliage.Add(terrain.drawTreesAndFoliage);
                scene.TreeDistances.Add(terrain.treeDistance);
                terrain.drawHeightmap = true;
                terrain.drawTreesAndFoliage = true;
                terrain.treeDistance = Mathf.Max(terrain.treeDistance, 5000f);
            }

            return scene;
        }

        private static void Restore(Scene scene)
        {
            RenderSettings.fog = scene.Fog;
            RenderSettings.ambientIntensity = scene.Ambient;
            QualitySettings.lodBias = scene.LodBias;

            for (int i = 0; i < scene.Keywords.Count; i++) Shader.EnableKeyword(scene.Keywords[i]);

            for (int i = 0; i < scene.Muted.Count; i++)
            {
                if (scene.Muted[i] != null) scene.Muted[i].enabled = true;
            }

            for (int i = 0; i < scene.Terrains.Count; i++)
            {
                Terrain terrain = scene.Terrains[i];
                if (terrain == null) continue;

                terrain.drawHeightmap = scene.Heightmaps[i];
                terrain.drawTreesAndFoliage = scene.Foliage[i];
                terrain.treeDistance = scene.TreeDistances[i];
            }
        }

        private static string Describe(Scene scene)
        {
            if (scene.Muted.Count == 0) return "nothing";

            List<string> names = new List<string>();
            for (int i = 0; i < scene.Muted.Count; i++)
            {
                if (scene.Muted[i] == null) continue;

                string name = scene.Muted[i].GetType().Name;
                if (!names.Contains(name)) names.Add(name);
            }

            return scene.Muted.Count + " (" + string.Join(", ", names.ToArray()) + ")";
        }

        private static bool MutedType(string name)
        {
            for (int i = 0; i < MutedTypes.Length; i++)
            {
                if (name == MutedTypes[i]) return true;
            }

            return false;
        }

        private static bool Muted(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string flat = name.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty);
            for (int i = 0; i < MutedWords.Length; i++)
            {
                if (flat.Contains(MutedWords[i])) return true;
            }

            return false;
        }

        private static bool Shoot(bool perspective)
        {
            float share = Mathf.Max(Battle.width / Area.width, Battle.height / Area.height);
            int longSide = (share > 0f && share < DetailedShare) ? DetailedLongSide : LongSide;

            int width;
            int height;
            if (Area.width >= Area.height)
            {
                width = longSide;
                height = Mathf.Max(64, Mathf.RoundToInt(longSide * Area.height / Area.width / 4f) * 4);
            }
            else
            {
                height = longSide;
                width = Mathf.Max(64, Mathf.RoundToInt(longSide * Area.width / Area.height / 4f) * 4);
            }

            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            GameObject host = new GameObject("RyLib_MapCamera");
            host.transform.SetParent(World.Root, false);

            try
            {
                Camera camera = host.AddComponent<Camera>();
                camera.enabled = false;
                Setup(camera, perspective);
                camera.targetTexture = target;

                camera.Render();
                camera.Render();

                Texture2D copy = new Texture2D(width, height, TextureFormat.RGB24, true);
                copy.name = "RyLib Map";
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                RenderTexture.active = previous;

                if (Uniform(copy, width, height))
                {
                    UnityEngine.Object.Destroy(copy);
                    return false;
                }

                copy.Apply(true, false);
                copy.Compress(false);
                copy.Apply(false, true);
                if (Texture != null) UnityEngine.Object.Destroy(Texture);
                Texture = copy;
                return true;
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(host);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void Setup(Camera camera, bool perspective)
        {
            Camera main = GameView.ActiveCamera;
            if (main != null)
            {
                camera.renderingPath = main.renderingPath;
                camera.allowHDR = main.allowHDR;
            }

            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.DepthNormals;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.25f, 0.35f, 1f);
            camera.cullingMask = Mask();
            camera.aspect = Area.width / Area.height;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            float half = Area.height * 0.5f;
            if (perspective)
            {
                float lift = half / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
                float fov = FieldOfView;
                if (_ground + lift < Top)
                {
                    lift = Top - _ground;
                    fov = 2f * Mathf.Atan(half / lift) * Mathf.Rad2Deg;
                }

                camera.orthographic = false;
                camera.fieldOfView = fov;
                camera.transform.position = new Vector3(Area.center.x, _ground + lift, Area.center.y);
                camera.nearClipPlane = 5f;
                camera.farClipPlane = lift + (_ground - _bottom) + 50f;
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = half;
            camera.transform.position = new Vector3(Area.center.x, Top, Area.center.y);
            camera.nearClipPlane = 1f;
            camera.farClipPlane = Top - _bottom;
        }

        private static int Mask()
        {
            int mask = ~0;
            for (int i = 0; i < HiddenLayers.Length; i++) mask &= ~(1 << HiddenLayers[i]);

            for (int layer = 0; layer < 32; layer++)
            {
                string name = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(name)) continue;

                string lower = name.ToLowerInvariant();
                for (int i = 0; i < HiddenLayerWords.Length; i++)
                {
                    if (lower.Contains(HiddenLayerWords[i])) mask &= ~(1 << layer);
                }
            }

            return mask;
        }

        private static bool Uniform(Texture2D copy, int width, int height)
        {
            Color[] samples = new Color[Samples * Samples];
            Color mean = Color.clear;

            for (int y = 0; y < Samples; y++)
            {
                for (int x = 0; x < Samples; x++)
                {
                    Color colour = copy.GetPixel((x * 2 + 1) * width / (Samples * 2), (y * 2 + 1) * height / (Samples * 2));
                    samples[y * Samples + x] = colour;
                    mean += colour;
                }
            }

            mean /= samples.Length;
            _flat = mean;

            int different = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                Color colour = samples[i];
                float delta = Mathf.Abs(colour.r - mean.r) + Mathf.Abs(colour.g - mean.g) + Mathf.Abs(colour.b - mean.b);
                if (delta > 0.06f) different++;
            }

            return different < samples.Length / 30;
        }
    }
}
