using UnityEngine;

namespace Meditation
{
    /// <summary>
    /// A standing vortex of many small glowing dots, anchored at a body
    /// point (the local origin). Every dot travels a spiral path that BEGINS
    /// and ENDS inside that point: the path opens out of the point, sweeps
    /// in wide luminous loops around the body (with a gentle arc above the
    /// point), then funnels down and dissolves toward the earth below -
    /// like a tornado of light wrapped around the meditator.
    ///
    /// Dots are distributed along a few strands, so dense dot-trails read
    /// as flowing ribbons; a small fraction are brighter sparkles. The flow
    /// direction is signed: negative = dots stream INTO the point (inhale),
    /// positive = dots stream OUT of it (exhale). <see cref="intensity"/>
    /// fades the whole vortex in/out at phase edges. Simulated manually via
    /// ParticleSystem.SetParticles - no allocations, Quest-friendly.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ParticleSystem))]
    public class VortexParticles : MonoBehaviour
    {
        [Header("Spiral path (local space, anchored at the origin = body point)")]
        [Tooltip("How far below the point the spiral dissolves, metres.")]
        public float depth = 5f;
        [Tooltip("Widest loop radius - how far the ribbons sweep around the body, metres.")]
        public float maxRadius = 1.25f;
        [Tooltip("Radius of the funnel at the very bottom, metres.")]
        public float bottomRadius = 0.35f;
        [Tooltip("How high the first loop arcs above the point (wraps the torso), metres.")]
        public float riseBulge = 0.5f;
        [Tooltip("Full revolutions a path makes from the point to the bottom.")]
        public float turns = 3f;

        [Header("Swarm")]
        [Range(64, 24576)] public int count = 24576;
        [Range(1, 12)] public int strands = 6;
        [Tooltip("Thickness of each dotted ribbon, metres.")]
        public float strandThickness = 0.05f;
        [Tooltip("Fraction of dots that are fine stray micro-currents drifting between the ribbons.")]
        [Range(0f, 0.5f)] public float wispFraction = 0.15f;
        [Tooltip("Fraction of dots that are large, very faint glow patches (inner luminosity).")]
        [Range(0f, 0.3f)] public float hazeFraction = 0.05f;
        [Tooltip("Fraction of dots that are larger bright sparkles.")]
        [Range(0f, 0.3f)] public float sparkleFraction = 0.03f;

        [Header("Motion")]
        [Tooltip("Signed flow along the path in path-lengths/second: negative = into the point (inhale), positive = out of it (exhale). Set by GroundingEnergySystem.")]
        public float flow = -0.075f;
        [Tooltip("Slow rotation of the whole spiral, radians/second.")]
        public float swirlSpeed = 0.3f;
        [Tooltip("Velocity-stretch factor: long overlapping streaks fuse the dots into continuous ribbons of light.")]
        public float streakStretch = 0.12f;

        [Header("Look")]
        [ColorUsage(false, true)] public Color color = new Color(0.5f, 0.8f, 1.6f);
        public float dotSizeMin = 0.015f;
        public float dotSizeMax = 0.04f;

        [Header("Control")]
        [Tooltip("Global fade of the vortex, 0..1. Animated by GroundingEnergySystem at breath-phase edges.")]
        [Range(0f, 1f)] public float intensity = 1f;

        ParticleSystem _ps;
        ParticleSystemRenderer _renderer;
        ParticleSystem.Particle[] _buf;
        float _time;

        void OnEnable() => Configure();

        void Configure()
        {
            _ps = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();

            var main = _ps.main;
            main.maxParticles = 24576;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            // Scripted (SetParticles) systems must not be frustum-culled:
            // culled paused systems get stale/empty render buffers.
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = _ps.emission;
            emission.enabled = false;
            var shape = _ps.shape;
            shape.enabled = false;

            var shader = Shader.Find("Meditation/GlowParticle");
            if (shader != null &&
                (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != shader))
            {
                _renderer.sharedMaterial = new Material(shader) { name = "VortexParticlesMat" };
            }
            ApplyMaterialProps();

            _renderer.renderMode = ParticleSystemRenderMode.Stretch;
            _renderer.velocityScale = streakStretch;
            _renderer.lengthScale = 0f;
            _renderer.cameraVelocityScale = 0f;
            _renderer.sortMode = ParticleSystemSortMode.None;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        /// <summary>Push the HDR colour to the material (cheap, any time).</summary>
        public void ApplyMaterialProps()
        {
            if (_renderer != null && _renderer.sharedMaterial != null)
                _renderer.sharedMaterial.SetColor("_Color", color);
        }

        static float Hash(int i, int salt)
        {
            float x = Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        // HLSL-style smoothstep: 0 below `a`, 1 above `b`. (Mathf.SmoothStep
        // interpolates BETWEEN a and b instead - not what we want here.)
        static float Step01(float a, float b, float x)
        {
            x = Mathf.Clamp01((x - a) / (b - a));
            return x * x * (3f - 2f * x);
        }

        // The spiral path: u = 0 inside the body point, u = 1 dissolving at
        // the bottom. Radius blooms open from zero, sweeps wide around the
        // body, then tapers into the lower funnel; height arcs briefly above
        // the point before descending. Near the point the strands' angular
        // offsets collapse to zero, so ONE braided stream enters/leaves the
        // point and only fans out into separate ribbons around the body.
        void PathPoint(float u, float angle0, float t, out Vector3 pos, out float r, out float ang)
        {
            float bloom = Step01(0f, 0.22f, u);
            float taper = Mathf.Lerp(1f, bottomRadius / Mathf.Max(0.01f, maxRadius),
                                     Step01(0.35f, 1f, u));
            r = maxRadius * bloom * taper;

            float y = riseBulge * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 3f))
                    - depth * Mathf.Pow(u, 1.4f);

            float spread = Step01(0.04f, 0.3f, u);   // strands merge at the point
            ang = angle0 * spread + turns * 2f * Mathf.PI * u + swirlSpeed * t;
            pos = new Vector3(r * Mathf.Cos(ang), y, r * Mathf.Sin(ang));
        }

        void LateUpdate()
        {
            if (_ps == null) Configure();

            // A fully stopped ParticleSystem silently discards SetParticles.
            if (Application.isPlaying)
            {
                if (!_ps.isPlaying) _ps.Play(true);
            }
            else if (!_ps.isPaused)
            {
                _ps.Play(true);
                _ps.Pause(true);
            }

            float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            _time += dt;

            if (intensity <= 0.005f)
            {
                _ps.SetParticles(System.Array.Empty<ParticleSystem.Particle>(), 0);
                return;
            }

            int n = Mathf.Clamp(count, 64, 24576);
            if (_buf == null || _buf.Length < n) _buf = new ParticleSystem.Particle[n];

            int written = 0;
            for (int i = 0; i < n; i++)
            {
                float angle0 = (i % strands) * 2f * Mathf.PI / strands;

                // Stream along the path; each dot recycles through the point.
                float u = Mathf.Repeat(Hash(i, 1) + _time * flow, 1f);

                // Dot kinds: main ribbon dots, fine stray micro-currents
                // (wisps) drifting between the ribbons, soft glow patches
                // (haze) for inner luminosity, and bright sparkles.
                float roll = Hash(i, 8);
                bool sparkle = roll < sparkleFraction;
                bool haze = !sparkle && roll < sparkleFraction + hazeFraction;
                bool wisp = !sparkle && !haze
                          && roll < sparkleFraction + hazeFraction + wispFraction;

                PathPoint(u, angle0, _time, out Vector3 pos, out float r, out float ang);

                // Ribbon thickness: jitter around the path, thin near the
                // point (everything threads through it), thicker further out.
                float th = strandThickness * (0.25f + 1.5f * Step01(0f, 0.3f, u));
                if (wisp) th *= 2.5f;      // strays wander between the bands
                else if (haze) th *= 2f;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                pos.x += ca * (Hash(i, 2) - 0.5f) * th;
                pos.z += sa * (Hash(i, 2) - 0.5f) * th;
                pos.y += (Hash(i, 3) - 0.5f) * th * 1.6f;

                // Living texture: a gentle micro-oscillation of every dot.
                pos.y += 0.01f * Mathf.Sin(_time * 2.1f + Hash(i, 11) * 6.28f);

                // Velocity along the path (for the motion streaks): numeric
                // tangent * flow rate, plus the swirl's tangential component.
                PathPoint(Mathf.Clamp01(u + 0.004f), angle0, _time, out Vector3 pB, out _, out _);
                PathPoint(Mathf.Clamp01(u - 0.004f), angle0, _time, out Vector3 pA, out _, out _);
                Vector3 vel = (pB - pA) / 0.008f * flow;
                vel += new Vector3(-sa, 0f, ca) * (swirlSpeed * r);
                float vm = vel.magnitude;
                if (vm > 4f) vel *= 4f / vm;

                // Wisps slowly drift around the axis relative to the bands -
                // the "small currents" inside the vortex.
                if (wisp)
                {
                    float da = (Hash(i, 9) - 0.5f) * 0.6f
                             + 0.25f * Mathf.Sin(_time * 0.45f + Hash(i, 10) * 6.28f);
                    float cda = Mathf.Cos(da), sda = Mathf.Sin(da);
                    pos = new Vector3(pos.x * cda - pos.z * sda, pos.y,
                                      pos.x * sda + pos.z * cda);
                    vel = new Vector3(vel.x * cda - vel.z * sda, vel.y,
                                      vel.x * sda + vel.z * cda);
                }

                // Dots are born and die INSIDE the point (u ~ 0) and
                // dissolve at the bottom end of the funnel.
                float aIn = Step01(0f, 0.015f, u);
                float aOut = 1f - Step01(0.93f, 1f, u);

                float twinkle = 0.6f + 0.4f * Mathf.Sin(_time * 2.4f + Hash(i, 5) * 6.28f);
                float kindAlpha = sparkle ? 1.4f : haze ? 0.07f : wisp ? 0.55f : 1f;
                float alpha = intensity * aIn * aOut * (0.5f + 0.5f * twinkle) * kindAlpha;
                if (alpha <= 0.012f) continue;

                float kindSize = sparkle ? 2.2f : haze ? 4f : wisp ? 0.75f : 1f;
                float size = Mathf.Lerp(dotSizeMin, dotSizeMax, Hash(i, 6)) * kindSize;

                _buf[written].position = pos;
                _buf[written].velocity = vel;
                _buf[written].startSize = size;
                _buf[written].startColor = new Color(1f, 1f, 1f, Mathf.Min(0.6f, alpha));
                _buf[written].rotation = 0f;
                _buf[written].remainingLifetime = 1000f;
                _buf[written].startLifetime = 1000f;
                written++;
            }

            _ps.SetParticles(_buf, written);
        }
    }
}
