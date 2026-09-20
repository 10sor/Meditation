using System;
using UnityEngine;
using UnityEngine.Serialization;

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
            [FormerlySerializedAs("color")]
            [ColorUsage(false, true)] public Color rendererColor;
            [ColorUsage(false, true)] public Color lightColor;
            [ColorUsage(false, true)] public Color effectColor;
            [HideInInspector] public bool separateColorsInitialized;
        }

        [Header("References")]
        [SerializeField] ChakraBreathController sequence;
        [SerializeField] Renderer[] targetRenderers;
        [FormerlySerializedAs("targetRenderer")]
        [SerializeField, HideInInspector] Renderer legacyTargetRenderer;

        [Header("Vortex")]
        [SerializeField] ParticleVortex[] vortexes;
        [SerializeField] Material sharedVortexMaterial;
        [SerializeField] Light[] vortexLights;
        [FormerlySerializedAs("vortexLight")]
        [SerializeField, HideInInspector] Light legacyVortexLight;
        [SerializeField] Renderer emissionRenderer;
        [SerializeField] Transform targetPoints;
        [SerializeField] Transform vortexStartPointA;
        [SerializeField] Transform vortexStartPointB;
        [Min(0f)] [SerializeField] float vortexFadeTime = 1f;

        [Header("Chakras (root to crown)")]
        [SerializeField] ChakraSettings[] chakras =
        {
            CreateChakra("Root",        0.94f, new Color(0.90f, 0.10f, 0.12f)),
            CreateChakra("Sacral",      1.06f, new Color(0.95f, 0.45f, 0.10f)),
            CreateChakra("SolarPlexus", 1.18f, new Color(0.97f, 0.83f, 0.15f)),
            CreateChakra("Heart",       1.30f, new Color(0.20f, 0.80f, 0.30f)),
            CreateChakra("Throat",      1.46f, new Color(0.20f, 0.55f, 0.95f)),
            CreateChakra("ThirdEye",    1.58f, new Color(0.35f, 0.22f, 0.75f)),
            CreateChakra("Crown",       1.70f, new Color(0.70f, 0.40f, 0.95f))
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

        void Awake()
        {
            HideVisualization();
        }

        void OnEnable()
        {
            ResolveReferences();
            if (Application.isPlaying)
                Apply(true);
            else
                ClearTargetPropertyBlocks();
        }

        void LateUpdate() => Apply(false);

        void ResolveReferences()
        {
            EnsureSeparateChakraColors();

            if (sequence == null)
                sequence = FindFirstObjectByType<ChakraBreathController>();

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                if (legacyTargetRenderer != null)
                {
                    targetRenderers = new[] { legacyTargetRenderer };
                }
                else
                {
                    GameObject avatar = GameObject.Find("poseStaticMonoMaterial");
                    if (avatar != null)
                        targetRenderers = avatar.GetComponentsInChildren<Renderer>(true);
                }
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

            if ((vortexLights == null || vortexLights.Length == 0) && legacyVortexLight != null)
                vortexLights = new[] { legacyVortexLight };

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
            if (sequence == null || !HasTargetRenderer() || chakras == null || chakras.Length == 0)
                return;

            if (properties == null)
                properties = new MaterialPropertyBlock();

            int index = Mathf.Clamp(sequence.CurrentChakraIndex, 0, chakras.Length - 1);
            ChakraSettings chakra = chakras[index];

            if (force || index != appliedChakra)
            {
                if (targetPoints != null)
                {
                    Vector3 position = targetPoints.position;
                    position.y = chakra.height;
                    targetPoints.position = position;
                }
                appliedChakra = index;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(properties);
                properties.SetFloat(ChakraHeightId, chakra.height);
                properties.SetColor(ChakraColorId, chakra.rendererColor);
                properties.SetFloat(ChakraIntensityId, sequence.Intensity);
                targetRenderer.SetPropertyBlock(properties);
            }

            int direction = IsVortexExhaling() ? 1 : -1;
            float vortexVisibility = EvaluateVortexVisibility();
            Color visibleEffectColor = chakra.effectColor * sequence.Intensity * vortexVisibility;
            if (sharedVortexMaterial != null)
            {
                sharedVortexMaterial.SetColor(VortexColorId, visibleEffectColor);
                sharedVortexMaterial.SetFloat(VortexDirectionId, direction);
            }

            if (vortexLights != null)
            {
                Color visibleLightColor = chakra.lightColor * sequence.Intensity * vortexVisibility;
                for (int i = 0; i < vortexLights.Length; i++)
                {
                    if (vortexLights[i] != null)
                        vortexLights[i].color = visibleLightColor;
                }
            }

            if (emissionRenderer != null)
            {
                if (emissionProperties == null)
                    emissionProperties = new MaterialPropertyBlock();

                emissionRenderer.GetPropertyBlock(emissionProperties);
                emissionProperties.SetColor(EmissionColorId, visibleEffectColor);
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

        static ChakraSettings CreateChakra(string name, float height, Color color)
        {
            return new ChakraSettings
            {
                name = name,
                height = height,
                rendererColor = color,
                lightColor = color,
                effectColor = color,
                separateColorsInitialized = true
            };
        }

        void EnsureSeparateChakraColors()
        {
            if (chakras == null)
                return;

            for (int i = 0; i < chakras.Length; i++)
            {
                ChakraSettings chakra = chakras[i];
                if (chakra.separateColorsInitialized)
                    continue;

                chakra.lightColor = chakra.rendererColor;
                chakra.effectColor = chakra.rendererColor;
                chakra.separateColorsInitialized = true;
                chakras[i] = chakra;
            }
        }

        void OnValidate()
        {
            appliedChakra = -1;
            vortexFadeTime = Mathf.Max(0f, vortexFadeTime);

            if (!Application.isPlaying)
            {
                ResolveReferences();
                ClearTargetPropertyBlocks();
            }
            else if (isActiveAndEnabled)
                Apply(true);
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                HideVisualization();
            else
                ClearTargetPropertyBlocks();
        }

        public void HideVisualization()
        {
            ResolveReferences();

            if (properties == null)
                properties = new MaterialPropertyBlock();

            if (targetRenderers != null)
            {
                for (int i = 0; i < targetRenderers.Length; i++)
                {
                    Renderer targetRenderer = targetRenderers[i];
                    if (targetRenderer == null)
                        continue;

                    targetRenderer.GetPropertyBlock(properties);
                    properties.SetFloat(ChakraIntensityId, 0f);
                    targetRenderer.SetPropertyBlock(properties);
                }
            }

            if (sharedVortexMaterial != null)
                sharedVortexMaterial.SetColor(VortexColorId, Color.clear);

            if (vortexLights != null)
            {
                for (int i = 0; i < vortexLights.Length; i++)
                {
                    if (vortexLights[i] != null)
                        vortexLights[i].color = Color.clear;
                }
            }

            if (emissionRenderer != null)
            {
                if (emissionProperties == null)
                    emissionProperties = new MaterialPropertyBlock();

                emissionRenderer.GetPropertyBlock(emissionProperties);
                emissionProperties.SetColor(EmissionColorId, Color.clear);
                emissionRenderer.SetPropertyBlock(emissionProperties);
            }
        }

        bool HasTargetRenderer()
        {
            if (targetRenderers == null)
                return false;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                    return true;
            }

            return false;
        }

        void ClearTargetPropertyBlocks()
        {
            if (targetRenderers == null)
                return;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                    targetRenderers[i].SetPropertyBlock(null);
            }
        }
    }
}
