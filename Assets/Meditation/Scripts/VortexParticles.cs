using UnityEngine;

namespace Meditation
{
    /// <summary>
    /// A compact travelling vortex - a small tornado of energy - modelled
    /// with real vortex maths. Tangential speed follows the Lamb-Oseen
    /// profile v_theta(r) = Gamma/(2*pi*r) * (1 - exp(-r^2/rc^2)): solid-body
    /// rotation in the core, free vortex outside. The resulting differential
    /// rotation (inner particles orbit much faster than outer ones) winds the
    /// particle filaments into spirals - the visual signature of a real
    /// whirl. Particles render as velocity-stretched streaks, so the fast
    /// core draws long curved strokes instead of dots.
    ///
    /// The whirl is cone-shaped (narrow tip up, wide skirt below), travels
    /// along the local -Y axis via <see cref="center"/> (the tip's position:
    /// 0 = at the body point, 1 = at the earth core) and is absorbed into
    /// the point as center goes negative - particles shrink and fade into
    /// y = 0, so the vortex visibly enters the point and later re-emerges.
    /// Driven per frame via ParticleSystem.SetParticles; no allocations.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ParticleSystem))]
    public class VortexParticles : MonoBehaviour
    {
        [Header("Path (local space, runs down -Y)")]
        public float length = 30f;

        [Header("Whirl shape")]
        [Tooltip("Radius of the wide lower skirt, metres.")]
        public float baseRadius = 0.9f;
        [Tooltip("Radius of the narrow tip that meets the point, metres.")]
        public float tipRadius = 0.06f;
        [Tooltip("Height of the whirl from tip to skirt, metres.")]
        public float skirtMeters = 2.8f;
        [Tooltip("How steeply the whirl may open just below the body point (radius per metre). Keeps entry/exit through the point a tight thread instead of a wide flickering ring.")]
        public float pointConeSlope = 0.35f;

        [Header("Vortex dynamics (Lamb-Oseen)")]
        [Tooltip("Circulation Gamma, m^2/s. More = faster swirl overall.")]
        public float circulation = 3.5f;
        [Tooltip("Viscous core radius rc, metres. Inside it the whirl rotates as a solid body.")]
        public float vortexCoreRadius = 0.25f;
        [Tooltip("Cap on angular speed, rad/s, so the innermost particles stay readable.")]
        public float maxAngularSpeed = 8f;
        [Tooltip("How fast particles cycle tip-to-skirt inside the whirl, cycles/s.")]
        public float internalCirculation = 0.18f;

        [Header("Swarm")]
        [Range(64, 2048)] public int count = 1100;
        public float sizeMin = 0.05f;
        public float sizeMax = 0.12f;
        [Tooltip("Velocity-stretch factor: how long the motion streaks are.")]
        public float streakStretch = 0.08f;

        [Header("Look")]
        [ColorUsage(false, true)] public Color color = new Color(0.5f, 0.8f, 1.6f);

        [Header("Control")]
        [Tooltip("Tip position along the path: 0 = at the body point, 1 = at the core; negative = absorbed into the point. Animated by GroundingEnergySystem.")]
        public float center = 0.3f;

        /// <summary>Whirl height as a fraction of the path length.</summary>
        public float SkirtFrac => skirtMeters / Mathf.Max(1f, length);

        ParticleSystem _ps;
        ParticleSystemRenderer _renderer;
        ParticleSystem.Particle[] _buf;
        float[] _theta;
        float _time;
        float _prevCenter;
        float _travelVel;

        void OnEnable()
        {
            Configure();
            _prevCenter = center;
        }

        void Configure()
        {
            _ps = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();

            var main = _ps.main;
            main.maxParticles = 2048;
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

            // Stretched billboards: each particle draws as a streak along its
            // velocity - fast core particles become long curved strokes.
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

        void LateUpdate()
        {
            if (_ps == null) Configure();

            // A fully stopped ParticleSystem silently discards SetParticles.
            // In play mode keep it PLAYING (paused systems can be skipped by
            // secondary cameras); we overwrite every particle each frame, so
            // its own simulation step never accumulates. In edit mode there
            // is no reliable play loop, so park it paused instead.
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

            // Vertical travel speed of the whirl (m/s), for streak direction.
            // Smoothed and tightly clamped: a one-frame jump of `center`
            // (phase change) must not stretch every particle into a long
            // bright streak for a frame - that reads as a flash of light.
            float rawVel = dt > 1e-5f ? (_prevCenter - center) * length / dt : 0f;
            rawVel = Mathf.Clamp(rawVel, -6f, 6f);
            _travelVel = Mathf.Lerp(_travelVel, rawVel, 0.2f);
            _prevCenter = center;

            int n = Mathf.Clamp(count, 64, 2048);
            if (_buf == null || _buf.Length < n) _buf = new ParticleSystem.Particle[n];
            if (_theta == null || _theta.Length < n)
            {
                _theta = new float[n];
                for (int i = 0; i < n; i++) _theta[i] = Hash(i, 7) * 2f * Mathf.PI;
            }

            float skirtFrac = SkirtFrac;
            float rc = Mathf.Max(0.02f, vortexCoreRadius);
            int written = 0;

            for (int i = 0; i < n; i++)
            {
                // Internal circulation: each particle cycles tip -> skirt so
                // the whirl churns instead of being a frozen shape.
                float cyc = Mathf.Repeat(Hash(i, 1) + _time * internalCirculation, 1f);
                float zrel = skirtFrac * cyc * cyc;          // dense near the tip
                float v = center + zrel;

                if (v <= -0.002f || v >= 1.05f) continue;    // absorbed / below core

                // Mask the tip->skirt recycle pop, fade whirl edges. Wide
                // zones: a recycled particle takes ~1 s to fade back in.
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(cyc / 0.15f, (1f - cyc) / 0.15f));

                // Cone: narrow at the tip, wide at the skirt.
                float cone = Mathf.Lerp(tipRadius, baseRadius, Mathf.Pow(cyc, 1.3f));
                float fill = 0.3f + 0.7f * Mathf.Sqrt(Hash(i, 2));
                float r = cone * fill;

                // Squeeze and fade into the body point as it gets absorbed:
                // a wider, gentler alpha zone so particles never pop in.
                float absorb = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.05f));
                r *= Mathf.Lerp(0.12f, 1f, absorb);
                r *= 1f + 0.06f * Mathf.Sin(_time * 1.9f + Hash(i, 3) * 6.28f);

                // Near the point everything must thread through it: cap the
                // radius with a cone anchored at the point, so entry and exit
                // stay a tight funnel instead of a wide flickering ring.
                float coneCap = tipRadius + pointConeSlope * Mathf.Max(v, 0f) * length;
                if (r > coneCap) r = coneCap;
                r = Mathf.Max(0.015f, r);

                // Lamb-Oseen tangential speed -> angular speed; inner
                // particles whirl fast, outer slowly (differential rotation).
                float vth = circulation / (2f * Mathf.PI * r) * (1f - Mathf.Exp(-(r * r) / (rc * rc)));
                float omega = Mathf.Min(vth / r, maxAngularSpeed);
                _theta[i] += omega * dt;
                float a = _theta[i];

                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float y = -Mathf.Max(v, 0f) * length;

                // Tip is slightly larger but NOT brighter - brightness spikes
                // near the point read as flashes of light.
                float boost = 1f + 0.5f * Mathf.Exp(-cyc * 6f);
                float size = Mathf.Lerp(sizeMin, sizeMax, Hash(i, 4)) * boost;
                float twinkle = 0.75f + 0.25f * Mathf.Sin(_time * 2.6f + Hash(i, 5) * 6.28f);
                float alpha = Mathf.Min(0.85f, 0.85f * edge * absorb * twinkle);
                if (alpha <= 0.01f) continue;

                _buf[written].position = new Vector3(r * ca, y, r * sa);
                // Streak direction: swirl + vertical travel; streaks shrink
                // toward the point so nothing whips across the body.
                float streak = 0.3f + 0.7f * absorb;
                _buf[written].velocity = new Vector3(-sa * vth * streak,
                    _travelVel * 0.6f * streak, ca * vth * streak);
                _buf[written].startSize = size;
                _buf[written].startColor = new Color(1f, 1f, 1f, alpha);
                _buf[written].rotation = 0f;
                _buf[written].remainingLifetime = 1000f;
                _buf[written].startLifetime = 1000f;
                written++;
            }

            _ps.SetParticles(_buf, written);
        }
    }
}
