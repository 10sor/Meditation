using System;
using UnityEngine;

namespace Meditation
{
    [DisallowMultipleComponent]
    public sealed class ChakraRendererController : MonoBehaviour
    {
        [Serializable]
        public struct ChakraSettings
        {
            public string name;
            public float height;
            [ColorUsage(false, true)] public Color color;
        }

        [Header("References")]
        [SerializeField] ChakraBreathController sequence;
        [SerializeField] Renderer targetRenderer;

        [Header("Vortex")]
        [SerializeField] ParticleVortex[] vortexes;
        [SerializeField] Material sharedVortexMaterial;
        [SerializeField] Light vortexLight;
        [SerializeField] Renderer emissionRenderer;
        [SerializeField] Transform targetPoints;
        [SerializeField] Transform vortexStartPointA;
        [SerializeField] Transform vortexStartPointB;
        [Min(0f)] [SerializeField] float vortexFadeTime = 1f;

        [Header("Chakras (root to crown)")]
        [SerializeField] ChakraSettings[] chakras =
        {
            new ChakraSettings { name = "Root",        height = 0.94f, color = new Color(0.90f, 0.10f, 0.12f) },
            new ChakraSettings { name = "Sacral",      height = 1.06f, color = new Color(0.95f, 0.45f, 0.10f) },
            new ChakraSettings { name = "SolarPlexus", height = 1.18f, color = new Color(0.97f, 0.83f, 0.15f) },
            new ChakraSettings { name = "Heart",       height = 1.30f, color = new Color(0.20f, 0.80f, 0.30f) },
            new ChakraSettings { name = "Throat",      height = 1.46f, color = new Color(0.20f, 0.55f, 0.95f) },
            new ChakraSettings { name = "ThirdEye",    height = 1.58f, color = new Color(0.35f, 0.22f, 0.75f) },
            new ChakraSettings { name = "Crown",       height = 1.70f, color = new Color(0.70f, 0.40f, 0.95f) }
        };

        static readonly int ChakraHeightId = Shader.PropertyToID("_ChakraHeight");
        static readonly int ChakraColorId = Shader.PropertyToID("_ChakraColor");
        static readonly int ChakraIntensityId = Shader.PropertyToID("_ChakraIntensity");
        static readonly int VortexColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int VortexDirectionId = Shader.PropertyToID("_Dir");

        MaterialPropertyBlock properties;
        MaterialPropertyBlock emissionProperties;
        int appliedChakra = -1;

        void OnEnable()
        {
            ResolveReferences();
            if (Application.isPlaying)
                Apply(true);
            else if (targetRenderer != null)
                targetRenderer.SetPropertyBlock(null);
        }

        void LateUpdate() => Apply(false);

        void ResolveReferences()
        {
            if (sequence == null)
                sequence = FindFirstObjectByType<ChakraBreathController>();

            if (targetRenderer == null)
            {
                GameObject avatar = GameObject.Find("poseStaticMonoMaterial");
                if (avatar != null)
                    targetRenderer = avatar.GetComponentInChildren<Renderer>(true);
            }

            if (targetPoints == null)
            {
                GameObject points = GameObject.Find("TargetPoints");
                if (points != null)
                    targetPoints = points.transform;
            }

            if (vortexes == null || vortexes.Length == 0)
            {
                GameObject holder = GameObject.Find("VortexHolder");
                if (holder != null)
                    vortexes = holder.GetComponentsInChildren<ParticleVortex>(true);
            }

            if (sharedVortexMaterial == null && vortexes != null)
            {
                for (int i = 0; i < vortexes.Length; i++)
                {
                    if (vortexes[i] == null)
                        continue;

                    ParticleSystemRenderer vortexRenderer =
                        vortexes[i].GetComponent<ParticleSystemRenderer>();
                    if (vortexRenderer != null)
                    {
                        sharedVortexMaterial = vortexRenderer.sharedMaterial;
                        if (sharedVortexMaterial != null)
                            break;
                    }
                }
            }
        }

        void Apply(bool force)
        {
            if (sequence == null || targetRenderer == null || chakras == null || chakras.Length == 0)
                return;

            if (properties == null)
                properties = new MaterialPropertyBlock();

            int index = Mathf.Clamp(sequence.CurrentChakraIndex, 0, chakras.Length - 1);
            ChakraSettings chakra = chakras[index];
            targetRenderer.GetPropertyBlock(properties);

            if (force || index != appliedChakra)
            {
                properties.SetFloat(ChakraHeightId, chakra.height);
                if (targetPoints != null)
                {
                    Vector3 position = targetPoints.position;
                    position.y = chakra.height;
                    targetPoints.position = position;
                }
                appliedChakra = index;
            }

            properties.SetColor(ChakraColorId, chakra.color);
            properties.SetFloat(ChakraIntensityId, sequence.Intensity);
            targetRenderer.SetPropertyBlock(properties);

            int direction = IsVortexExhaling() ? 1 : -1;
            float vortexVisibility = EvaluateVortexVisibility();
            Color vortexColor = chakra.color * sequence.Intensity;
            Color visibleVortexColor = vortexColor * vortexVisibility;
            if (sharedVortexMaterial != null)
            {
                sharedVortexMaterial.SetColor(VortexColorId, visibleVortexColor);
                sharedVortexMaterial.SetFloat(VortexDirectionId, direction);
            }

            if (vortexLight != null)
                vortexLight.color = visibleVortexColor;

            if (emissionRenderer != null)
            {
                if (emissionProperties == null)
                    emissionProperties = new MaterialPropertyBlock();

                emissionRenderer.GetPropertyBlock(emissionProperties);
                emissionProperties.SetColor(EmissionColorId, visibleVortexColor);
                emissionRenderer.SetPropertyBlock(emissionProperties);
            }

            float vortexSpeed = direction * 0.5f;
            Transform vortexStart = direction > 0 && index == 6
                ? vortexStartPointB
                : vortexStartPointA;
            if (vortexes != null)
            {
                for (int i = 0; i < vortexes.Length; i++)
                {
                    if (vortexes[i] != null)
                    {
                        vortexes[i].Speed = vortexSpeed;
                        if (vortexStart != null)
                            vortexes[i].StartPoint = vortexStart;
                    }
                }
            }
        }

        float EvaluateVortexVisibility()
        {
            float phaseTime = sequence.PhaseTime;
            float inhaleEnd = sequence.InhaleDuration;
            float exhaleStart = inhaleEnd + sequence.HoldDuration;

            if (phaseTime < inhaleEnd)
            {
                float fadeTime = Mathf.Min(vortexFadeTime,
                    sequence.InhaleDuration);
                if (fadeTime <= 0.0001f)
                    return 1f;

                float fadeStartsAt = inhaleEnd - fadeTime;
                return 1f - Smooth01((phaseTime - fadeStartsAt) / fadeTime);
            }

            if (phaseTime < exhaleStart)
                return 0f;

            float exhaleFadeTime = Mathf.Min(vortexFadeTime,
                sequence.ExhaleDuration);
            if (exhaleFadeTime <= 0.0001f)
                return 1f;

            return Smooth01((phaseTime - exhaleStart) / exhaleFadeTime);
        }

        bool IsVortexExhaling()
        {
            return sequence.Direction > 0;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        void OnValidate()
        {
            appliedChakra = -1;
            vortexFadeTime = Mathf.Max(0f, vortexFadeTime);

            if (!Application.isPlaying)
            {
                ResolveReferences();
                if (targetRenderer != null)
                    targetRenderer.SetPropertyBlock(null);
            }
            else if (isActiveAndEnabled)
                Apply(true);
        }

        void OnDisable()
        {
            if (!Application.isPlaying && targetRenderer != null)
                targetRenderer.SetPropertyBlock(null);
        }
    }
}
