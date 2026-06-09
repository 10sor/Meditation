using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meditation
{
    /// <summary>
    /// Procedurally builds a realistic dome of stars around the viewer as a
    /// single mesh (one draw call, cheap on Quest). Stars vary in colour
    /// (blue-white through gold to orange), brightness (many faint, a few bright
    /// "hero" stars that catch the bloom), and twinkle gently over time. Drop on
    /// an empty GameObject at the origin; it creates its own MeshFilter,
    /// MeshRenderer and material. Use the gear-menu "Regenerate" to preview.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class StarfieldGenerator : MonoBehaviour
    {
        [Header("Layout")]
        public int starCount = 1600;
        public float radius = 200f;
        public int seed = 12345;

        [Header("Appearance")]
        public float minSize = 0.35f;
        public float maxSize = 1.1f;
        [Tooltip("Overall brightness multiplier for the whole field.")]
        public float brightness = 1.1f;

        [Header("Hero stars")]
        [Range(0f, 0.2f)]
        [Tooltip("Fraction of stars that are large and bright (bloom-catching).")]
        public float heroFraction = 0.04f;
        public float heroSizeMul = 2.6f;
        public float heroBrightness = 1.8f;

        [Header("Twinkle")]
        [Range(0f, 1f)] public float twinkleAmount = 0.6f;

        MeshFilter _filter;
        MeshRenderer _renderer;

        // Realistic stellar colours, weighted toward white/blue-white.
        static Color StarColor(System.Random rng)
        {
            float t = (float)rng.NextDouble();
            if (t < 0.55f) return new Color(0.75f, 0.83f, 1.0f);   // blue-white
            if (t < 0.80f) return new Color(1.0f, 1.0f, 0.97f);    // white
            if (t < 0.93f) return new Color(1.0f, 0.93f, 0.78f);   // gold
            return new Color(1.0f, 0.80f, 0.62f);                  // orange
        }

        void OnEnable() => Build();

        [ContextMenu("Regenerate")]
        public void Build()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            int n = Mathf.Max(0, starCount);
            var rng = new System.Random(seed);

            var verts = new Vector3[n * 4];
            var uvs = new Vector2[n * 4];
            var uv2 = new Vector2[n * 4];   // x = twinkle phase, y = twinkle amount
            var colors = new Color[n * 4];
            var tris = new int[n * 6];

            for (int i = 0; i < n; i++)
            {
                // Uniform random direction on the unit sphere.
                float cosT = (float)(rng.NextDouble() * 2.0 - 1.0);
                float phi = (float)(rng.NextDouble() * 2.0 * Mathf.PI);
                float sinT = Mathf.Sqrt(Mathf.Max(0f, 1f - cosT * cosT));
                var dir = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                Vector3 centre = dir * radius;

                Vector3 up = Mathf.Abs(dir.y) > 0.99f ? Vector3.right : Vector3.up;
                Vector3 right = Vector3.Normalize(Vector3.Cross(up, dir));
                up = Vector3.Cross(dir, right);

                bool hero = rng.NextDouble() < heroFraction;

                // Bias toward dim stars (product of randoms); hero stars are bright.
                float b = Mathf.Lerp(0.2f, 1f, (float)(rng.NextDouble() * rng.NextDouble()));
                float size = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());
                if (hero) { b *= heroBrightness; b = Mathf.Max(b, 1.2f); size *= heroSizeMul; }

                Vector3 r = right * size;
                Vector3 u = up * size;

                int v = i * 4;
                verts[v + 0] = centre - r - u;
                verts[v + 1] = centre + r - u;
                verts[v + 2] = centre + r + u;
                verts[v + 3] = centre - r + u;

                uvs[v + 0] = new Vector2(0, 0);
                uvs[v + 1] = new Vector2(1, 0);
                uvs[v + 2] = new Vector2(1, 1);
                uvs[v + 3] = new Vector2(0, 1);

                float phase = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                // Bright stars twinkle a little less (steadier), faint ones more.
                float amt = twinkleAmount * (hero ? 0.4f : 1f);
                var ph = new Vector2(phase, amt);
                uv2[v + 0] = uv2[v + 1] = uv2[v + 2] = uv2[v + 3] = ph;

                Color c = StarColor(rng) * b;
                c.a = Mathf.Min(b, 1f);
                colors[v + 0] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;

                int ti = i * 6;
                tris[ti + 0] = v + 0; tris[ti + 1] = v + 2; tris[ti + 2] = v + 1;
                tris[ti + 3] = v + 0; tris[ti + 4] = v + 3; tris[ti + 5] = v + 2;
            }

            var mesh = new Mesh { name = "Starfield" };
            mesh.indexFormat = n * 4 > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetUVs(1, new List<Vector2>(uv2));
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 3f));
            _filter.sharedMesh = mesh;

            var shader = Shader.Find("Meditation/StarUnlit");
            if (shader != null)
            {
                if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != shader)
                    _renderer.sharedMaterial = new Material(shader) { name = "StarfieldMat" };
                _renderer.sharedMaterial.SetFloat("_Brightness", brightness);
            }

            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }
    }
}
