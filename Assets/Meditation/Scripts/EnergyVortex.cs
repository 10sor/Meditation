using UnityEngine;
using UnityEngine.Rendering;

namespace Meditation
{
    /// <summary>
    /// One vortex of energy running down the local -Y axis from y = 0 (the
    /// body end) to y = -length (the earth core end). Two visual styles:
    ///
    /// - Shell (default): a smooth funnel surface of revolution rendered with
    ///   the "Meditation/VortexShell" shader, whose rotating spiral bands make
    ///   it read as a solid swirling vortex (stirred-water / tornado look).
    /// - Strands: discrete helix ribbons ("Meditation/EnergyStream"), a more
    ///   electric, lightning-like look.
    ///
    /// The funnel is wide at the core and converges into the body point. All
    /// motion happens in the shaders; the mesh is built once per shape change
    /// and never touched per frame (Quest-friendly).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class EnergyVortex : MonoBehaviour
    {
        public enum Style { Shell, Strands }

        [Header("Style")]
        public Style style = Style.Shell;

        [Header("Path (local space, runs down -Y)")]
        public float length = 30f;
        [Tooltip("Funnel radius at the top (body end), metres.")]
        public float topRadius = 0.06f;
        [Tooltip("Funnel radius at the bottom (core end), metres.")]
        public float bottomRadius = 1.2f;
        [Tooltip("Funnel shape: high values flare open right below the body point (tornado); 1 = straight cone.")]
        public float funnelPower = 8f;

        [Header("Shell")]
        [Range(8, 64)] public int radialSegments = 32;
        [Tooltip("Number of spiral bands around the shell.")]
        public float stripes = 6f;
        [Tooltip("How many wraps a band makes from body to core.")]
        public float twist = 3.5f;

        [Header("Strands")]
        [Range(1, 8)] public int strands = 6;
        [Tooltip("Full revolutions each strand makes from top to bottom.")]
        public float turns = 12f;
        public float ribbonWidth = 0.11f;

        [Header("Shared")]
        [Range(8, 512)] public int segments = 140;
        [Tooltip("1 = single shell, 2 = adds an inner counter-leaning layer for depth.")]
        [Range(1, 2)] public int layers = 2;
        [Tooltip("Radius of the inner layer relative to the outer one.")]
        [Range(0.2f, 0.9f)] public float innerScale = 0.6f;

        [Header("Look")]
        [ColorUsage(false, true)] public Color color = new Color(0.5f, 0.8f, 1.6f);
        [Tooltip("Pulse travel speed; positive = body -> core (descending), negative = core -> body (ascending).")]
        public float flowSpeed = 1f;
        [Tooltip("Rotation of the spiral pattern, radians/second. Sign flips the screw direction.")]
        public float swirlSpeed = 2f;
        public float pulseCount = 6f;
        [Range(0f, 1f)] public float baseGlow = 0.25f;

        [Header("Moving front")]
        [Tooltip("uv.y of the visible front: 1 = nothing shown, 0 = full stream up to the body. Animated by GroundingEnergySystem.")]
        [Range(0f, 1f)] public float head = 0f;
        public float headGlow = 2f;

        MeshFilter _filter;
        MeshRenderer _renderer;
        bool _dirty;

        void OnEnable() => Build();
        void OnValidate() => _dirty = true;

        void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Build();
            }
        }

        // Funnel radius at v (0 = top/body, 1 = bottom/core).
        float Radius(float v) =>
            bottomRadius + (topRadius - bottomRadius) * Mathf.Pow(1f - v, Mathf.Max(0.01f, funnelPower));

        [ContextMenu("Regenerate")]
        public void Build()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            // Release the previous mesh so per-chakra rebuilds don't leak
            // GPU buffers over a long session.
            var old = _filter.sharedMesh;
            if (old != null)
            {
                if (Application.isPlaying) Destroy(old);
                else DestroyImmediate(old);
            }

            Mesh mesh = style == Style.Shell ? BuildShell() : BuildStrands();
            float maxR = Mathf.Max(topRadius, bottomRadius) + ribbonWidth;
            mesh.bounds = new Bounds(
                new Vector3(0f, -length * 0.5f, 0f),
                new Vector3(maxR * 2f, length + 1f, maxR * 2f));
            _filter.sharedMesh = mesh;

            var shader = Shader.Find(style == Style.Shell
                ? "Meditation/VortexShell"
                : "Meditation/EnergyStream");
            if (shader != null)
            {
                if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != shader)
                    _renderer.sharedMaterial = new Material(shader) { name = "EnergyVortexMat" };
                ApplyMaterialProps();
            }

            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        /// <summary>
        /// Pushes the animatable fields (colour, flow, head...) to the
        /// material without rebuilding the mesh. Cheap; safe every frame.
        /// </summary>
        public void ApplyMaterialProps()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            var m = _renderer != null ? _renderer.sharedMaterial : null;
            if (m == null) return;
            m.SetColor("_Color", color);
            m.SetFloat("_FlowSpeed", flowSpeed);
            m.SetFloat("_SwirlSpeed", swirlSpeed);
            m.SetFloat("_PulseCount", pulseCount);
            m.SetFloat("_BaseGlow", baseGlow);
            m.SetFloat("_HeadV", head);
            m.SetFloat("_HeadGlow", headGlow);
            if (style == Style.Shell)
            {
                m.SetFloat("_Stripes", stripes);
                m.SetFloat("_Twist", twist);
            }
        }

        // ------------------------------------------------------------------
        // Shell: smooth funnel surface of revolution.
        // ------------------------------------------------------------------
        Mesh BuildShell()
        {
            int n = Mathf.Max(8, segments);
            int m = Mathf.Clamp(radialSegments, 8, 64);
            int L = Mathf.Clamp(layers, 1, 2);

            int vertsPerLayer = (n + 1) * (m + 1);
            var verts = new Vector3[L * vertsPerLayer];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[L * n * m * 6];

            for (int l = 0; l < L; l++)
            {
                float scale = l == 0 ? 1f : innerScale;
                int vBase = l * vertsPerLayer;

                for (int i = 0; i <= n; i++)
                {
                    float v = (float)i / n;
                    float r = Radius(v) * scale;

                    // Radius slope for the surface normal.
                    float dv = 1f / n;
                    float v0 = Mathf.Max(0f, v - dv);
                    float v1 = Mathf.Min(1f, v + dv);
                    float dRdV = (Radius(v1) - Radius(v0)) * scale / (v1 - v0);

                    for (int j = 0; j <= m; j++)
                    {
                        float u = (float)j / m;
                        float a = u * 2f * Mathf.PI;
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);

                        int vi = vBase + i * (m + 1) + j;
                        verts[vi] = new Vector3(r * ca, -v * length, r * sa);

                        // Surface normal = cross of the two tangents of the
                        // revolve surface P(v,a).
                        var tv = new Vector3(dRdV * ca, -length, dRdV * sa);
                        var ta = new Vector3(-sa, 0f, ca);
                        Vector3 nrm = Vector3.Cross(ta, tv).normalized;
                        if (Vector3.Dot(nrm, new Vector3(ca, 0f, sa)) < 0f) nrm = -nrm;
                        norms[vi] = nrm;

                        // Inner layer gets mirrored uv.x so its bands lean and
                        // scroll the opposite way -> visual depth.
                        uvs[vi] = new Vector2(l == 0 ? u : 1f - u, v);
                    }
                }

                int tBase = l * n * m * 6;
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < m; j++)
                    {
                        int a = vBase + i * (m + 1) + j;
                        int b = a + (m + 1);
                        int t = tBase + (i * m + j) * 6;
                        tris[t + 0] = a; tris[t + 1] = b; tris[t + 2] = b + 1;
                        tris[t + 3] = a; tris[t + 4] = b + 1; tris[t + 5] = a + 1;
                    }
                }
            }

            var mesh = new Mesh { name = "VortexShell" };
            mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            return mesh;
        }

        // ------------------------------------------------------------------
        // Strands: discrete helix ribbons (the older, electric look).
        // ------------------------------------------------------------------
        Vector3 StrandPoint(float angle0, float v, float radiusScale)
        {
            float a = angle0 + v * turns * 2f * Mathf.PI;
            float r = Radius(v) * radiusScale;
            return new Vector3(r * Mathf.Cos(a), -v * length, r * Mathf.Sin(a));
        }

        Mesh BuildStrands()
        {
            int n = Mathf.Max(8, segments);
            int outerStrands = Mathf.Clamp(strands, 1, 8);
            int s = outerStrands * Mathf.Clamp(layers, 1, 2);

            // Per strand: (n+1) rings, each with 2 crossed ribbons of 2 verts.
            int vertsPerStrand = (n + 1) * 4;
            var verts = new Vector3[s * vertsPerStrand];
            var uvs = new Vector2[verts.Length];
            var tris = new int[s * n * 12];

            for (int k = 0; k < s; k++)
            {
                bool innerLayer = k >= outerStrands;
                float scale = innerLayer ? innerScale : 1f;
                float angle0 = (k % outerStrands) * 2f * Mathf.PI / outerStrands
                             + (innerLayer ? Mathf.PI / outerStrands : 0f);
                int vBase = k * vertsPerStrand;

                for (int i = 0; i <= n; i++)
                {
                    float v = (float)i / n;
                    Vector3 p = StrandPoint(angle0, v, scale);

                    Vector3 tangent = (StrandPoint(angle0, Mathf.Min(1f, v + 0.002f), scale)
                                     - StrandPoint(angle0, Mathf.Max(0f, v - 0.002f), scale)).normalized;
                    Vector3 radial = new Vector3(p.x, 0f, p.z);
                    if (radial.sqrMagnitude < 1e-6f) radial = Vector3.right;
                    radial.Normalize();
                    Vector3 n1 = Vector3.Cross(tangent, radial).normalized;
                    Vector3 n2 = Vector3.Cross(tangent, n1).normalized;

                    float w = 0.5f * ribbonWidth * Mathf.Lerp(0.3f, 1.25f, v);

                    int vi = vBase + i * 4;
                    verts[vi + 0] = p - n1 * w;
                    verts[vi + 1] = p + n1 * w;
                    verts[vi + 2] = p - n2 * w;
                    verts[vi + 3] = p + n2 * w;

                    uvs[vi + 0] = new Vector2(0f, v);
                    uvs[vi + 1] = new Vector2(1f, v);
                    uvs[vi + 2] = new Vector2(0f, v);
                    uvs[vi + 3] = new Vector2(1f, v);
                }

                int tBase = k * n * 12;
                for (int i = 0; i < n; i++)
                {
                    int a = vBase + i * 4;
                    int b = a + 4;
                    int t = tBase + i * 12;
                    tris[t + 0] = a + 0; tris[t + 1] = b + 0; tris[t + 2] = b + 1;
                    tris[t + 3] = a + 0; tris[t + 4] = b + 1; tris[t + 5] = a + 1;
                    tris[t + 6] = a + 2; tris[t + 7] = b + 2; tris[t + 8] = b + 3;
                    tris[t + 9] = a + 2; tris[t + 10] = b + 3; tris[t + 11] = a + 3;
                }
            }

            var mesh = new Mesh { name = "VortexStrands" };
            mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            return mesh;
        }
    }
}
