using UnityEngine;

namespace Meditation
{
    /// <summary>
    /// A standing vortex of many small glowing dots, anchored at a body
    /// point (the local origin). Every dot travels a spiral path that BEGINS
    /// and ENDS inside that point: the path opens out of the point, sweeps
    /// in wide luminous loops around the body, then funnels down and
    /// dissolves toward the earth below -
    /// like a tornado of light wrapped around the meditator.
    ///
    /// Dots are distributed along a few strands, so dense dot-trails read
    /// as flowing ribbons. The flow direction is signed: negative = dots
    /// stream INTO the point (inhale),
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
        [Tooltip("Distributes the turns over the full height without moving either end point. Positive values gather turns toward the upper point; negative values gather them toward the bottom.")]
        [Range(-1f, 1f)] public float riseBulge = 0f;
        [Tooltip("Shape of the funnel wall. 1 is linear; values above 1 make it more concave, values below 1 make it more convex.")]
        [Range(0.25f, 4f)] public float spiralConcavity = 2f;
        [Tooltip("Full revolutions a path makes from the point to the bottom.")]
        public float turns = 3f;

        [Header("Swarm")]
        [Range(64, 24576)] public int count = 24576;
        [Range(1, 12)] public int strands = 6;
        [Tooltip("Place particles at equal arc-length intervals on every strand. Useful when Count controls ribbon smoothness.")]
        public bool uniformDistribution;
        [Tooltip("Maximum distance particles wander around their spiral path, metres.")]
        [Min(0f)] public float particleSpread = 0.05f;
        [Tooltip("Spatial frequency of the spread waves along the spiral. Higher values create more waves.")]
        [Min(0.01f)] public float spreadFrequency = 1f;
        [Tooltip("Speed of the smooth wandering around the spiral path.")]
        [Min(0f)] public float wanderSpeed = 0.6f;

        [Header("Motion")]
        [Tooltip("Signed flow along the path in path-lengths/second: negative = into the point (inhale), positive = out of it (exhale). Set by GroundingEnergySystem.")]
        public float flow = -0.075f;
        [Tooltip("Slow rotation of the whole spiral, radians/second.")]
        public float swirlSpeed = 0.3f;

        [Header("Look")]
        [ColorUsage(false, true)] public Color color = new Color(0.5f, 0.8f, 1.6f);
        [Tooltip("Shader colour property changed through a MaterialPropertyBlock. Common values are _Color and _BaseColor.")]
        public string colorProperty = "_Color";
        [Tooltip("Shader property receiving -1 for negative flow and 1 for positive flow.")]
        public string directionProperty = "_Dir";
        public float dotSizeMin = 0.015f;
        public float dotSizeMax = 0.04f;
        [Tooltip("Portion of the path over which particles shrink into the upper convergence point.")]
        [Range(0.01f, 0.5f)] public float pointSizeFalloff = 0.15f;

        [Header("Control")]
        [Tooltip("Global fade of the vortex, 0..1. Animated by GroundingEnergySystem at breath-phase edges.")]
        [Range(0f, 1f)] public float intensity = 1f;

        ParticleSystem _ps;
        ParticleSystemRenderer _renderer;
        MaterialPropertyBlock _materialProps;
        ParticleSystem.Particle[] _buf;
        float _time;

        const int ArcSamples = 128;
        const int MaxStrands = 12;
        readonly float[,] _arcLengths = new float[MaxStrands, ArcSamples + 1];
        readonly float[] _totalArcLengths = new float[MaxStrands];
        float _appliedDirection;
        float _appliedIntensity = -1f;

        void OnEnable() => Configure();

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            if (_renderer == null) _renderer = GetComponent<ParticleSystemRenderer>();
            ApplyMaterialProps();
        }

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

            ApplyMaterialProps();
        }

        /// <summary>Push the HDR colour to the material (cheap, any time).</summary>
        public void ApplyMaterialProps()
        {
            if (_renderer == null) return;
            if (_materialProps == null) _materialProps = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_materialProps);

            string requestedColor = string.IsNullOrEmpty(colorProperty) ? "_Color" : colorProperty;
            // A ParticleSystemRenderer can use a particle material and a
            // separate trail material with different colour property names.
            // One property block serves both, so populate the common names.
            Color visibleColor = color * intensity;
            _materialProps.SetColor(requestedColor, visibleColor);
            _materialProps.SetColor("_Color", visibleColor);
            _materialProps.SetColor("_BaseColor", visibleColor);
            _appliedIntensity = intensity;
            ApplyDirectionToBlock();
            _renderer.SetPropertyBlock(_materialProps);
        }

        void ApplyIntensityIfChanged()
        {
            if (!Mathf.Approximately(intensity, _appliedIntensity))
                ApplyMaterialProps();
        }

        void ApplyDirectionToBlock()
        {
            float direction = flow < 0f ? 1f : -1f;
            string property = string.IsNullOrEmpty(directionProperty) ? "_Dir" : directionProperty;
            _materialProps.SetFloat(property, direction);
            _appliedDirection = direction;
        }

        void ApplyDirectionIfChanged()
        {
            float direction = flow < 0f ? 1f : -1f;
            if (Mathf.Approximately(direction, _appliedDirection) || _renderer == null) return;
            if (_materialProps == null) _materialProps = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_materialProps);
            ApplyDirectionToBlock();
            _renderer.SetPropertyBlock(_materialProps);
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
        // body, then tapers into the lower funnel. riseBulge redistributes the
        // path over its full height but keeps both end points fixed. Near the
        // point the strands' angular
        // offsets collapse to zero, so ONE braided stream enters/leaves the
        // point and only fans out into separate ribbons around the body.
        void PathPoint(float u, float angle0, float t, out Vector3 pos, out float r, out float ang)
        {
            u = Mathf.Clamp01(u);
            float bloom = Step01(0f, 0.22f, u);
            float taperProgress = Step01(0.35f, 1f, u);
            float funnelProfile = 1f - Mathf.Pow(1f - taperProgress,
                                                  Mathf.Max(0.25f, spiralConcavity));
            float taper = Mathf.Lerp(1f, bottomRadius / Mathf.Max(0.01f, maxRadius),
                                     funnelProfile);
            r = maxRadius * bloom * taper;

            // Exponential bias over the complete height. This replaces the
            // old local sine bump, which lifted only the first few turns and
            // could push particles above the anchor point.
            float heightExponent = Mathf.Pow(4f, riseBulge);
            float y = -depth * Mathf.Pow(u, heightExponent);

            float spread = Step01(0.04f, 0.3f, u);   // strands merge at the point
            ang = angle0 * spread + turns * 2f * Mathf.PI * u + swirlSpeed * t;
            pos = new Vector3(r * Mathf.Cos(ang), y, r * Mathf.Sin(ang));
        }

        // Converts a normalized travelled distance into the path parameter.
        // Without this mapping, a constant change in u visibly accelerates
        // wherever the spiral bends, widens or changes its vertical profile.
        void BuildArcLengthTable()
        {
            int strandCount = Mathf.Clamp(strands, 1, MaxStrands);
            for (int strand = 0; strand < strandCount; strand++)
            {
                float angle0 = strand * 2f * Mathf.PI / strandCount;
                PathPoint(0f, angle0, 0f, out Vector3 previous, out _, out _);
                _arcLengths[strand, 0] = 0f;

                for (int i = 1; i <= ArcSamples; i++)
                {
                    PathPoint(i / (float)ArcSamples, angle0, 0f,
                              out Vector3 current, out _, out _);
                    _arcLengths[strand, i] = _arcLengths[strand, i - 1]
                                           + Vector3.Distance(previous, current);
                    previous = current;
                }

                _totalArcLengths[strand] = Mathf.Max(0.0001f,
                                                      _arcLengths[strand, ArcSamples]);
            }
        }

        float DistanceToPathU(float distance01, int strand)
        {
            float target = Mathf.Clamp01(distance01) * _totalArcLengths[strand];
            int low = 0;
            int high = ArcSamples;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (_arcLengths[strand, mid] < target) low = mid;
                else high = mid;
            }

            float segmentLength = _arcLengths[strand, high] - _arcLengths[strand, low];
            float blend = segmentLength > 0.00001f
                ? (target - _arcLengths[strand, low]) / segmentLength
                : 0f;
            return (low + blend) / ArcSamples;
        }

        void LateUpdate()
        {
            if (_ps == null) Configure();
            ApplyIntensityIfChanged();
            ApplyDirectionIfChanged();

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
            BuildArcLengthTable();

            if (intensity <= 0.005f)
            {
                _ps.SetParticles(System.Array.Empty<ParticleSystem.Particle>(), 0);
                return;
            }

            int n = Mathf.Clamp(count, 64, 24576);
            int activeStrands = Mathf.Clamp(strands, 1, MaxStrands);
            if (uniformDistribution)
                n = Mathf.Max(activeStrands, n / activeStrands * activeStrands);
            if (_buf == null || _buf.Length < n) _buf = new ParticleSystem.Particle[n];

            int written = 0;
            for (int i = 0; i < n; i++)
            {
                int strand = i % activeStrands;
                float angle0 = strand * 2f * Mathf.PI / activeStrands;

                // Stream along the path. Uniform mode gives every strand
                // equal arc-length sampling, so Count controls ribbon detail.
                int strandPoint = i / activeStrands;
                int pointsOnStrand = (n - 1 - strand) / activeStrands + 1;
                float start = uniformDistribution
                    ? strandPoint / (float)pointsOnStrand
                    : Hash(i, 1);
                // A ribbon needs a stable open chain. Cycling individual
                // samples teleports one vertex between opposite endpoints
                // and makes Unity draw a long closing segment.
                float travel = uniformDistribution
                    ? start
                    : Mathf.Repeat(start + _time * flow, 1f);
                float u = DistanceToPathU(travel, strand);

                PathPoint(u, angle0, _time, out Vector3 pos, out float r, out float ang);

                // Smooth, time-varying wander around the path. Each particle
                // travels independently instead of keeping a fixed offset.
                // sin² has a zero slope at both ends: wandering disappears
                // smoothly as every strand converges into either point.
                float convergence = Mathf.Sin(Mathf.PI * u);
                float spread = particleSpread * convergence * convergence;
                float wanderTime = _time * wanderSpeed;
                float frequency = Mathf.Max(0.01f, spreadFrequency);
                float radialPhase = uniformDistribution
                    ? travel * 14.4513f * frequency + strand * 1.731f
                    : Hash(i, 2) * 6.28318f;
                float tangentPhase = uniformDistribution
                    ? travel * 10.6814f * frequency + strand * 2.417f
                    : Hash(i, 3) * 6.28318f;
                float verticalPhase = uniformDistribution
                    ? travel * 18.2212f * frequency + strand * 0.937f
                    : Hash(i, 4) * 6.28318f;
                float radialNoise = Mathf.Sin(wanderTime * 1.13f + radialPhase);
                float tangentNoise = Mathf.Sin(wanderTime * 0.83f + tangentPhase);
                float verticalNoise = Mathf.Sin(wanderTime * 1.37f + verticalPhase);
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                pos += new Vector3(ca, 0f, sa) * (radialNoise * spread);
                pos += new Vector3(-sa, 0f, ca) * (tangentNoise * spread);
                pos.y += verticalNoise * spread;

                // Velocity along the path (for the motion streaks): numeric
                // tangent * flow rate, plus the swirl's tangential component.
                const float tangentStep = 0.001f;
                float travelB = Mathf.Clamp01(travel + tangentStep);
                float travelA = Mathf.Clamp01(travel - tangentStep);
                PathPoint(DistanceToPathU(travelB, strand), angle0, _time, out Vector3 pB, out _, out _);
                PathPoint(DistanceToPathU(travelA, strand), angle0, _time, out Vector3 pA, out _, out _);
                float travelled = Mathf.Max(0.0001f, travelB - travelA);
                Vector3 vel = (pB - pA) / travelled * flow;
                vel += new Vector3(-sa, 0f, ca) * (swirlSpeed * r);
                float vm = vel.magnitude;
                if (vm > 4f) vel *= 4f / vm;

                // Dots are born and die INSIDE the point (u ~ 0) and
                // dissolve at the bottom end of the funnel.
                float aIn = Step01(0f, 0.015f, u);
                float aOut = 1f - Step01(0.93f, 1f, u);

                float twinkle = 0.6f + 0.4f * Mathf.Sin(_time * 2.4f + Hash(i, 5) * 6.28f);
                float alpha = aIn * aOut * (0.5f + 0.5f * twinkle);
                // Ribbon vertices must stay in the buffer even when fully
                // transparent, otherwise missing vertices create holes and
                // destabilize Unity's age-based ribbon ordering.
                if (!uniformDistribution && alpha <= 0.012f) continue;

                float pointSize = Step01(0f, Mathf.Max(0.01f, pointSizeFalloff), u);
                float size = Mathf.Lerp(dotSizeMin, dotSizeMax, Hash(i, 6)) * pointSize;

                _buf[written].position = pos;
                _buf[written].velocity = vel;
                _buf[written].startSize = size;
                _buf[written].startColor = new Color(1f, 1f, 1f, Mathf.Min(0.6f, alpha));
                _buf[written].rotation = 0f;
                if (uniformDistribution)
                {
                    float strandAgeOffset = strand
                        * (0.25f / pointsOnStrand / activeStrands);
                    _buf[written].remainingLifetime = 1f + start + strandAgeOffset;
                    _buf[written].startLifetime = 2f;
                }
                else
                {
                    _buf[written].remainingLifetime = 1000f;
                    _buf[written].startLifetime = 1000f;
                }
                written++;
            }

            _ps.SetParticles(_buf, written);
        }
    }
}
