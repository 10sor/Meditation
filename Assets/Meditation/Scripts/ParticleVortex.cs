using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Meditation
{
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleVortex : MonoBehaviour
    {
        [Header("Path")]
        [SerializeField] Transform startPoint;
        [Tooltip("Guide used to shape the final convergence path.")]
        [SerializeField] Transform flowPoint;
        [Tooltip("Center of the final outer circle.")]
        [SerializeField] Transform targetPoint;

        [Tooltip("Normalized lifetime at which particles leave the spiral and begin converging.")]
        [Range(0.01f, 0.99f)]
        [SerializeField] float convergenceStartsAt = 0.7f;

        [Tooltip("Blends the spiral exit position toward Flow Point to create the intermediate waypoint.")]
        [Range(0f, 1f)]
        [SerializeField] float flowPointInfluence = 0.5f;

        [Header("Spiral")]
        [Min(0f)] [SerializeField] float radius = 1f;
        [Min(0f)] [SerializeField] float turns = 3f;
        [Tooltip("Static starting angle in turns. 0.25 is 90 degrees.")]
        [SerializeField] float startTurnOffset;
        [Tooltip("Signed rotation speed in revolutions per second.")]
        [SerializeField] float speed = 0.2f;

        [Header("Cheap Noise")]
        [Min(0f)] [SerializeField] float noiseAmount = 0.08f;
        [Min(0f)] [SerializeField] float noiseFrequency = 3f;
        [SerializeField] float noiseSpeed = 0.35f;
        [SerializeField] int noiseSeed;

        [Header("Editor Preview")]
        [SerializeField] bool previewInEditMode = true;

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        public Transform StartPoint
        {
            get => startPoint;
            set => startPoint = value;
        }

        ParticleSystem system;
        ParticleSystem.Particle[] particles;
        float rotationPhase;
        float noisePhase;

#if UNITY_EDITOR
        double previousEditorTime;
#endif

        void OnEnable()
        {
            system = GetComponent<ParticleSystem>();
            rotationPhase = 0f;
            noisePhase = 0f;

#if UNITY_EDITOR
            previousEditorTime = EditorApplication.timeSinceStartup;
#endif
        }

        void LateUpdate()
        {
            if (system == null)
                system = GetComponent<ParticleSystem>();

            if (startPoint == null || flowPoint == null || targetPoint == null)
                return;

            if (!Application.isPlaying && !previewInEditMode)
                return;

            float deltaTime = GetDeltaTime();
            if (deltaTime <= 0f)
                return;

            int aliveCount = system.particleCount;
            if (aliveCount == 0)
                return;

            EnsureBuffer(aliveCount);
            int count = system.GetParticles(particles);

            Vector3 start = WorldToSimulationSpace(startPoint.position);
            Vector3 flow = WorldToSimulationSpace(flowPoint.position);
            Vector3 target = WorldToSimulationSpace(targetPoint.position);
            BuildFrame(start, target, out Vector3 radialX, out Vector3 radialY);

            rotationPhase += speed * Mathf.PI * 2f * deltaTime;
            noisePhase += noiseSpeed * deltaTime;
            float totalAngle = turns * Mathf.PI * 2f;
            Vector3 streamExit = EvaluateSpiral(convergenceStartsAt,
                start, target, radialX, radialY, totalAngle);
            Vector3 waypoint = Vector3.LerpUnclamped(
                streamExit, flow, flowPointInfluence);

            const float tangentSample = 0.001f;
            float previousProgress = Mathf.Max(0f,
                convergenceStartsAt - tangentSample);
            Vector3 previousStreamPosition = EvaluateSpiral(previousProgress,
                start, target, radialX, radialY, totalAngle);
            Vector3 spiralDerivative = (streamExit - previousStreamPosition) /
                                       Mathf.Max(tangentSample,
                                           convergenceStartsAt - previousProgress);

            // The first half of the convergence occupies half of its remaining
            // normalized lifetime. This handle length makes its first derivative
            // match the spiral derivative exactly at the join.
            Vector3 exitControl = streamExit + spiralDerivative *
                ((1f - convergenceStartsAt) / 6f);

            Vector3 throughDirection = SafeDirection(target - streamExit,
                target - waypoint);
            float waypointHandleLength = Mathf.Min(
                Vector3.Distance(streamExit, waypoint),
                Vector3.Distance(waypoint, target)) * 0.25f;
            Vector3 waypointInControl = waypoint -
                                        throughDirection * waypointHandleLength;
            Vector3 waypointOutControl = waypoint +
                                         throughDirection * waypointHandleLength;

            Vector3 targetDirection = SafeDirection(target - waypoint,
                throughDirection);
            Vector3 targetControl = target - targetDirection *
                                    (Vector3.Distance(waypoint, target) * 0.3f);

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = particles[i];
                float lifetime = Mathf.Max(0.0001f, particle.startLifetime);
                float age = Mathf.Clamp01(1f - particle.remainingLifetime / lifetime);
                float progress = speed >= 0f ? age : 1f - age;

                if (progress < convergenceStartsAt)
                {
                    particle.position = EvaluateSpiral(progress, start, target,
                        radialX, radialY, totalAngle);
                }
                else
                {
                    float convergence = Mathf.InverseLerp(
                        convergenceStartsAt, 1f, progress);
                    if (convergence < 0.5f)
                    {
                        particle.position = CubicBezier(streamExit, exitControl,
                            waypointInControl, waypoint, convergence * 2f);
                    }
                    else
                    {
                        particle.position = CubicBezier(waypoint,
                            waypointOutControl, targetControl, target,
                            (convergence - 0.5f) * 2f);
                    }
                }
                particle.velocity = Vector3.zero;
                particles[i] = particle;
            }

            system.SetParticles(particles, count);

#if UNITY_EDITOR
            if (!Application.isPlaying && system.isPlaying)
                EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        Vector3 EvaluateSpiral(float progress, Vector3 start, Vector3 target,
            Vector3 radialX, Vector3 radialY, float totalAngle)
        {
            Vector3 center = Vector3.LerpUnclamped(start, target, progress);
            float spiralRadius = radius * Smooth01(progress);
            float angle = startTurnOffset * Mathf.PI * 2f +
                          rotationPhase + progress * totalAngle;
            Vector3 spiral =
                (radialX * Mathf.Cos(angle) + radialY * Mathf.Sin(angle)) * spiralRadius;

            float noiseCoordinate = progress * noiseFrequency + noisePhase;
            Vector3 noise = radialX * ValueNoise1D(noiseCoordinate, noiseSeed) +
                            radialY * ValueNoise1D(
                                noiseCoordinate * 1.371f + 17.17f, noiseSeed + 1013);
            noise *= noiseAmount * Smooth01(progress);
            return center + spiral + noise;
        }

        static float ValueNoise1D(float coordinate, int seed)
        {
            int cell = Mathf.FloorToInt(coordinate);
            float fraction = coordinate - cell;
            float smooth = fraction * fraction * (3f - 2f * fraction);
            return Mathf.Lerp(HashToSignedFloat(cell, seed),
                HashToSignedFloat(cell + 1, seed), smooth);
        }

        static float HashToSignedFloat(int value, int seed)
        {
            unchecked
            {
                uint hash = (uint)(value * 374761393 + seed * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) * (2f / 16777215f) - 1f;
            }
        }

        static Vector3 CubicBezier(Vector3 start, Vector3 controlA,
            Vector3 controlB, Vector3 end, float t)
        {
            float oneMinusT = 1f - t;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float t2 = t * t;
            return oneMinusT2 * oneMinusT * start +
                   3f * oneMinusT2 * t * controlA +
                   3f * oneMinusT * t2 * controlB +
                   t2 * t * end;
        }

        static Vector3 SafeDirection(Vector3 preferred, Vector3 fallback)
        {
            if (preferred.sqrMagnitude > 0.000001f)
                return preferred.normalized;
            if (fallback.sqrMagnitude > 0.000001f)
                return fallback.normalized;
            return Vector3.forward;
        }

        static void BuildFrame(Vector3 start, Vector3 target,
            out Vector3 radialX, out Vector3 radialY)
        {
            Vector3 axis = target - start;
            if (axis.sqrMagnitude < 0.000001f)
                axis = Vector3.up;
            else
                axis.Normalize();

            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            radialX = Vector3.Normalize(Vector3.Cross(axis, reference));
            radialY = Vector3.Normalize(Vector3.Cross(axis, radialX));
        }

        Vector3 WorldToSimulationSpace(Vector3 worldPosition)
        {
            ParticleSystem.MainModule main = system.main;
            switch (main.simulationSpace)
            {
                case ParticleSystemSimulationSpace.World:
                    return worldPosition;
                case ParticleSystemSimulationSpace.Custom:
                    return main.customSimulationSpace != null
                        ? main.customSimulationSpace.InverseTransformPoint(worldPosition)
                        : transform.InverseTransformPoint(worldPosition);
                default:
                    return transform.InverseTransformPoint(worldPosition);
            }
        }

        float GetDeltaTime()
        {
            if (Application.isPlaying)
                return Time.deltaTime;

#if UNITY_EDITOR
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Clamp((float)(now - previousEditorTime), 0f, 0.05f);
            previousEditorTime = now;
            return deltaTime;
#else
            return 0f;
#endif
        }

        void EnsureBuffer(int requiredSize)
        {
            if (particles == null || particles.Length < requiredSize)
                particles = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(requiredSize)];
        }

        void OnValidate()
        {
            radius = Mathf.Max(0f, radius);
            turns = Mathf.Max(0f, turns);
            noiseAmount = Mathf.Max(0f, noiseAmount);
            noiseFrequency = Mathf.Max(0f, noiseFrequency);
            convergenceStartsAt = Mathf.Clamp(convergenceStartsAt, 0.01f, 0.99f);
            flowPointInfluence = Mathf.Clamp01(flowPointInfluence);

#if UNITY_EDITOR
            previousEditorTime = EditorApplication.timeSinceStartup;
#endif
        }

        void OnDrawGizmosSelected()
        {
            if (startPoint == null || flowPoint == null || targetPoint == null)
                return;

            Gizmos.color = new Color(1f, 0.55f, 0.08f, 0.9f);
            Gizmos.DrawLine(startPoint.position, targetPoint.position);
            Gizmos.DrawWireSphere(startPoint.position, 0.025f);
            Gizmos.DrawWireSphere(flowPoint.position, 0.025f);
            Gizmos.DrawWireSphere(targetPoint.position, radius);
        }
    }
}
