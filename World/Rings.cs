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
        public Color Colour = new Color(-1f, -1f, -1f, -1f);
    }

    internal static class Rings
    {
        private const int Segments = 48;
        private const float Width = 0.14f;
        private const float GroundOffset = 0.05f;

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

                if (mark.RingRadius > 0f)
                {
                    for (int i = 0; i <= Segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / Segments;
                        Points[i] = new Vector3(Mathf.Cos(angle) * mark.RingRadius, GroundOffset, Mathf.Sin(angle) * mark.RingRadius);
                    }

                    ring.Circle.positionCount = Segments + 1;
                    ring.Circle.SetPositions(Points);
                }
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

            if (ring.Colour != mark.Colour)
            {
                ring.Colour = mark.Colour;
                ring.Circle.startColor = ring.Circle.endColor = mark.Colour;
                ring.Beam.startColor = ring.Beam.endColor = mark.Colour;
                if (ring.Material != null) ring.Material.color = mark.Colour;
            }
        }

        public static void Move(World.Live live, Vector3 position)
        {
            if (live.Ring != null && live.Ring.Root != null) live.Ring.Root.transform.position = position;
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
            ring.Circle = Line(ring.Root, ring.Material, Segments + 1);

            GameObject beam = new GameObject("Beam");
            beam.transform.SetParent(ring.Root.transform, false);
            ring.Beam = Line(beam, ring.Material, 2);

            return ring;
        }

        private static LineRenderer Line(GameObject host, Material material, int points)
        {
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
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
