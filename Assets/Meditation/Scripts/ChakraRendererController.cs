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

        MaterialPropertyBlock properties;
        int appliedChakra = -1;

        void OnEnable()
        {
            ResolveReferences();
            Apply(true);
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
                appliedChakra = index;
            }

            // The timeline owns intensity; this component owns the chakra color.
            properties.SetColor(ChakraColorId, chakra.color * sequence.Intensity);
            properties.SetFloat(ChakraIntensityId, 1f);
            targetRenderer.SetPropertyBlock(properties);
        }

        void OnValidate()
        {
            appliedChakra = -1;
            if (isActiveAndEnabled)
                Apply(true);
        }
    }
}
