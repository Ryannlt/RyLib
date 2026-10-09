using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RyLib
{
    internal sealed class Ring
    {
        public GameObject Root;
        public LineRenderer Circle;
        public LineRenderer Beam;
        public Material Material;
        public float Radius = -1f;
        public float BeamHeight = -1f;
        public Color Color = new Color(-1f, -1f, -1f, -1f);
        public Vector3 Placed = new Vector3(float.NaN, float.NaN, float.NaN);
    }

    internal static class Rings
    {
        private const int Segments = 48;
        private const float Width = 0.14f;
        private const float GroundOffset = 0.08f;
        private const float ProbeLift = 0.5f;
        private const float ProbeDepth = 12f;
        private const float StructureGap = 0.6f;
        private const float Settle = 0.05f;
        private const float TerrainSeconds = 2f;
        private const int GroundMask = (1 << 9) | (1 << 13) | (1 << 14) | (1 << 25);

        private static Terrain[] _terrains = new Terrain[0];
        private static float _nextTerrains;

        private static readonly List<Ring> Pool = new List<Ring>();
        private static readonly Vector3[] Points = new Vector3[Segments + 1];

        private static Shader _shader;

        public static void Apply(World.Live live)
        {
            WorldMark mark = live.Mark;
            if (mark.RingRadius <= 0f && mark.BeamHeight <= 0f)
            {
                Release(live);
                return;
            }

            Ring ring = live.Ring;
            if (ring == null || ring.Root == null)
            {
                ring = Take();
                live.Ring = ring;
            }

            if (!ring.Root.activeSelf) ring.Root.SetActive(true);

            if (!Mathf.Approximately(ring.Radius, mark.RingRadius))
            {
                ring.Radius = mark.RingRadius;
                ring.Circle.enabled = mark.RingRadius > 0f;
                ring.Placed = new Vector3(float.NaN, float.NaN, float.NaN);
            }

            if (!Mathf.Approximately(ring.BeamHeight, mark.BeamHeight))
            {
                ring.BeamHeight = mark.BeamHeight;
                ring.Beam.enabled = mark.BeamHeight > 0f;

                if (mark.BeamHeight > 0f)
                {
                    ring.Beam.positionCount = 2;
                    ring.Beam.SetPosition(0, new Vector3(0f, GroundOffset, 0f));
                    ring.Beam.SetPosition(1, new Vector3(0f, mark.BeamHeight, 0f));
                }
            }

            if (ring.Color != mark.Color)
            {
                ring.Color = mark.Color;
                ring.Circle.startColor = ring.Circle.endColor = mark.Color;
                ring.Beam.startColor = ring.Beam.endColor = mark.Color;
                if (ring.Material != null) ring.Material.color = mark.Color;
            }
        }

        public static void Move(World.Live live, Vector3 position)
        {
            Ring ring = live.Ring;
            if (ring == null || ring.Root == null) return;

            bool flat;
            float ground = GroundUnder(position, out flat);
            Vector3 center = new Vector3(position.x, ground, position.z);
            ring.Root.transform.position = center;

            if (ring.Radius <= 0f) return;
            if (!float.IsNaN(ring.Placed.x) && (ring.Placed - center).sqrMagnitude < Settle * Settle) return;
            ring.Placed = center;

            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float x = center.x + Mathf.Cos(angle) * ring.Radius;
                float z = center.z + Mathf.Sin(angle) * ring.Radius;
                float y = flat ? ground : TerrainHeight(x, z, ground);
                Points[i] = new Vector3(x, y + GroundOffset, z);
            }

            ring.Circle.positionCount = Segments + 1;
            ring.Circle.SetPositions(Points);
        }

        private static float GroundUnder(Vector3 position, out bool flat)
        {
            float terrain = TerrainHeight(position.x, position.z, float.NaN);

            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * ProbeLift, Vector3.down, out hit, ProbeDepth, GroundMask, QueryTriggerInteraction.Ignore))
            {
                flat = float.IsNaN(terrain) || hit.point.y > terrain + StructureGap;
                return flat ? hit.point.y : terrain;
            }

            flat = float.IsNaN(terrain);
            return flat ? position.y : terrain;
        }

        private static float TerrainHeight(float x, float z, float fallback)
        {
            if (Time.unscaledTime >= _nextTerrains)
            {
                _nextTerrains = Time.unscaledTime + TerrainSeconds;
                _terrains = Terrain.activeTerrains;
            }

            for (int i = 0; i < _terrains.Length; i++)
            {
                Terrain terrain = _terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (x < origin.x || z < origin.z || x > origin.x + size.x || z > origin.z + size.z) continue;

                return terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
            }

            return fallback;
        }

        public static void Release(World.Live live)
        {
            Ring ring = live.Ring;
            live.Ring = null;
            if (ring == null || ring.Root == null) return;

            ring.Root.SetActive(false);
            Pool.Add(ring);
        }

        private static Ring Take()
        {
            while (Pool.Count > 0)
            {
                Ring pooled = Pool[Pool.Count - 1];
                Pool.RemoveAt(Pool.Count - 1);
                if (pooled.Root != null) return pooled;
            }

            Ring ring = new Ring();
            ring.Material = new Material(ResolveShader());

            ring.Root = new GameObject("RyLib_Ring");
            ring.Root.transform.SetParent(World.Root, false);
            ring.Circle = Line(ring.Root, ring.Material, Segments + 1, true);

            GameObject beam = new GameObject("Beam");
            beam.transform.SetParent(ring.Root.transform, false);
            ring.Beam = Line(beam, ring.Material, 2, false);

            return ring;
        }

        private static LineRenderer Line(GameObject host, Material material, int points, bool world)
        {
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = world;
            line.loop = false;
            line.positionCount = points;
            line.startWidth = line.endWidth = Width;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            return line;
        }

        private static Shader ResolveShader()
        {
            if (_shader != null) return _shader;

            _shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (_shader == null) _shader = Shader.Find("Sprites/Default");
            if (_shader == null) _shader = Shader.Find("Hidden/Internal-Colored");

            return _shader;
        }
    }
}
