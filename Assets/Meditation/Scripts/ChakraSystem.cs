using UnityEngine;

namespace Meditation
{
    /// <summary>
    /// Drives the seven chakras as distinct coloured glows bound to specific
    /// body parts, lighting from inside the avatar via the
    /// "Meditation/ChakraBody" shader (fed through global shader arrays).
    ///
    /// Each chakra has its own height + depth so it sits on its body part, and
    /// a tight radius so neighbours stay distinct. In Play mode a wave travels
    /// root -> crown: each chakra rises over <see cref="riseTime"/> seconds and
    /// falls over <see cref="fallTime"/> seconds, handing off smoothly to the
    /// next. In the editor (not playing) all chakras show at full so placement
    /// can be checked.
    /// </summary>
    [ExecuteAlways]
    public class ChakraSystem : MonoBehaviour
    {
        public enum Mode { Wave, Manual }

        [System.Serializable]
        public struct Chakra
        {
            public string name;
            public Color color;
            [Tooltip("Height up the body (avatar-local metres).")]
            public float height;
            [Tooltip("Depth toward the viewer so the glow sits on this body part.")]
            public float depth;
        }

        [Header("Placement (avatar-local space)")]
        public float centerX = 0f;

        [Tooltip("Radius each glow reaches, in metres. Smaller = more distinct.")]
        public float glowRadius = 0.11f;

        [Header("Glow intensity")]
        public float awakenedIntensity = 3.2f;

        [Header("Mode")]
        public Mode mode = Mode.Wave;

        [Header("Wave timing (Play mode)")]
        [Tooltip("Seconds for a chakra to brighten 0 -> full.")]
        public float riseTime = 1f;
        [Tooltip("Seconds for a chakra to dim full -> 0.")]
        public float fallTime = 1f;
        [Tooltip("Seconds between the start of one chakra and the next (1 = the falling one hands off to the rising one).")]
        public float step = 1f;
        [Tooltip("Dark pause after the crown before the wave loops.")]
        public float loopPause = 1.5f;

        [Header("Manual sequence")]
        [Range(0, 7)] public int awakenedCount = 7;

        // Root -> crown, with body-part-tuned height + depth and traditional colours.
        [SerializeField]
        Chakra[] _chakras = new[]
        {
            new Chakra { name = "Root",        color = new Color(0.95f, 0.07f, 0.10f), height = 0.10f, depth = 0f },
            new Chakra { name = "Sacral",      color = new Color(1.00f, 0.40f, 0.04f), height = 0.22f, depth = 0f },
            new Chakra { name = "SolarPlexus", color = new Color(1.00f, 0.84f, 0.08f), height = 0.34f, depth = 0f },
            new Chakra { name = "Heart",       color = new Color(0.15f, 0.85f, 0.22f), height = 0.47f, depth = 0f },
            new Chakra { name = "Throat",      color = new Color(0.12f, 0.55f, 1.00f), height = 0.62f, depth = 0f },
            new Chakra { name = "ThirdEye",    color = new Color(0.32f, 0.16f, 0.88f), height = 0.75f, depth = 0f },
            new Chakra { name = "Crown",       color = new Color(0.66f, 0.28f, 1.00f), height = 0.88f, depth = 0f },
        };

        static readonly Vector4[] _posArr = new Vector4[7];
        static readonly Vector4[] _colArr = new Vector4[7];

        float Period => _chakras.Length * step + fallTime + loopPause;

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // Wave envelope for chakra i at time t: rise then fall, else 0.
        float WaveLevel(int i, float t)
        {
            float local = Mathf.Repeat(t, Period) - i * step;
            if (local < 0f) return 0f;
            if (local < riseTime) return Smooth(local / riseTime);
            if (local < riseTime + fallTime) return Smooth(1f - (local - riseTime) / fallTime);
            return 0f;
        }

        void Update()
        {
            bool playingWave = Application.isPlaying && mode == Mode.Wave;
            float t = Application.isPlaying ? Time.time : 0f;

            for (int i = 0; i < _chakras.Length && i < 7; i++)
            {
                var c = _chakras[i];

                float level;
                if (playingWave)
                    level = WaveLevel(i, t);
                else if (mode == Mode.Manual && Application.isPlaying)
                    level = i < awakenedCount ? 1f : 0f;
                else
                    level = 1f; // editor preview: show all for placement

                Vector3 world = transform.TransformPoint(new Vector3(centerX, c.height, c.depth));
                _posArr[i] = new Vector4(world.x, world.y, world.z, glowRadius);
                _colArr[i] = new Vector4(c.color.r, c.color.g, c.color.b, level * awakenedIntensity);
            }

            Shader.SetGlobalVectorArray("_ChakraPos", _posArr);
            Shader.SetGlobalVectorArray("_ChakraColor", _colArr);
            Shader.SetGlobalInt("_ChakraCount", Mathf.Min(_chakras.Length, 7));
        }

        /// <summary>Manual mode: light chakras up to a count, root->crown.</summary>
        public void SetAwakenedCount(int count)
        {
            mode = Mode.Manual;
            awakenedCount = Mathf.Clamp(count, 0, _chakras.Length);
        }

        public int ChakraCount => _chakras.Length;
    }
}
