using UnityEngine;

namespace Meditation
{
    /// <summary>
    /// The grounding energy cycle of the meditation. For each chakra in turn
    /// (root -> crown) a standing vortex of small glowing dots
    /// (<see cref="VortexParticles"/>) wraps the body in wide spiral ribbons
    /// anchored at that chakra's point: every dot's path begins and ends
    /// inside the point. On the inhale the dots stream along the spiral INTO
    /// the point and it brightens; through the hold the point glows alone;
    /// on the exhale the dots stream back OUT and down toward the earth and
    /// the glow fades with them. Phases follow the metronome audio clock
    /// when one is assigned, otherwise the timers below. Drives
    /// <see cref="ChakraSystem"/> (External mode).
    /// </summary>
    [ExecuteAlways]
    public class GroundingEnergySystem : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Chakra system to sync with; auto-found next to the avatar when empty.")]
        public ChakraSystem chakras;

        [Header("Earth core")]
        [Tooltip("How far below this object the glowing earth core sits, metres.")]
        public float coreDepth = 12f;
        public float coreRadius = 1.6f;
        [ColorUsage(false, true)] public Color coreColor = new Color(2.2f, 1.0f, 0.35f);
        [Tooltip("How much the core brightens while energy streams up.")]
        public float coreFlare = 1.6f;

        [Header("Vortex")]
        [Tooltip("Dots' flow speed along the path, path-lengths/second.")]
        public float flowRate = 0.075f;
        [Tooltip("HDR boost applied to the chakra colour.")]
        public float colorBoost = 2.0f;

        [Header("Cycle timing (seconds, used when no metronome is playing)")]
        public float riseTime = 6f;
        public float holdTime = 2f;
        public float descendTime = 6f;
        public float pauseTime = 1f;

        [Header("Breath sync (optional)")]
        [Tooltip("Metronome audio. When assigned and playing, the cycle follows the audio clock instead of the timers above.")]
        public AudioSource metronome;
        [Tooltip("Seconds into the audio where the first inhale begins.")]
        public float breathOffset = 0.46f;
        [Tooltip("Length of one full breath cycle in the audio, seconds.")]
        public float breathCycle = 20.07f;
        [Tooltip("Inhale / hold / exhale durations within the cycle, seconds.")]
        public float inhaleTime = 4.04f;
        public float breathHoldTime = 8.01f;
        public float exhaleTime = 8.02f;

        VortexParticles[] _vortices = System.Array.Empty<VortexParticles>();
        Transform _vortexRoot;
        Transform _core;
        MeshRenderer _coreRenderer;
        int _chakra;
        float _timerT;
        bool _dirty;

        // AudioSource.time wraps every loop of the clip; count the wraps to
        // keep a monotonic timeline (else the chakra index would stick).
        float _prevAudioTime;
        int _audioLoops;

        void OnEnable()
        {
            _chakra = 0;
            _timerT = 0f;
            Apply();
        }

        void OnValidate() { if (isActiveAndEnabled) _dirty = true; }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // Fade the vortex in at a phase start and out before its end.
        static float Envelope(float t, float duration, float ramp)
        {
            return Smooth(t / ramp) * Smooth((duration - t) / ramp);
        }

        void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Apply();
            }

            if (_vortices.Length == 0) return;

            if (!Application.isPlaying)
            {
                // Editor preview: full spiral, streaming inward.
                SetVortexState(1f, -Mathf.Abs(flowRate));
                return;
            }

            if (chakras != null) chakras.mode = ChakraSystem.Mode.External;

            float inhale, hold, exhale, cycle, t;

            if (metronome != null && metronome.isPlaying)
            {
                inhale = inhaleTime; hold = breathHoldTime; exhale = exhaleTime;
                cycle = breathCycle;

                float at = metronome.time;
                if (at < _prevAudioTime - 1f) _audioLoops++;
                _prevAudioTime = at;
                float clipLen = metronome.clip != null ? metronome.clip.length : cycle;
                t = _audioLoops * clipLen + at - breathOffset;
                if (t < 0f)
                {
                    SetVortexIntensity(0f);   // intro before the first inhale
                    return;
                }
            }
            else
            {
                inhale = riseTime; hold = holdTime; exhale = descendTime;
                cycle = riseTime + holdTime + descendTime + pauseTime;
                _timerT += Time.deltaTime;
                t = _timerT;
            }

            int cycleIndex = Mathf.FloorToInt(t / cycle);
            float tc = t - cycleIndex * cycle;

            int chakraCount = chakras != null ? chakras.ChakraCount : 7;
            int chakra = cycleIndex % chakraCount;
            if (chakra != _chakra)
            {
                SetChakraLevel(_chakra, 0f);
                _chakra = chakra;
                SetupVortexForChakra(_chakra);
            }

            float glow;
            float coreBoost = 1f;

            if (tc < inhale)
            {
                // Dots stream along the spiral INTO the point.
                SetVortexState(Envelope(tc, inhale, 0.7f), -Mathf.Abs(flowRate));
                glow = Smooth(tc / inhale);
                coreBoost = coreFlare;
            }
            else if (tc < inhale + hold)
            {
                // Absorbed: only the point glows.
                SetVortexIntensity(0f);
                glow = 1f;
            }
            else if (tc < inhale + hold + exhale)
            {
                // Dots stream back OUT of the point, down toward the earth;
                // the glow leaves with them.
                float te = tc - inhale - hold;
                SetVortexState(Envelope(te, exhale, 0.7f), Mathf.Abs(flowRate));
                glow = 1f - Smooth(te / exhale);
            }
            else
            {
                SetVortexIntensity(0f);   // rest before the next colour
                glow = 0f;
            }

            SetChakraLevel(_chakra, glow);

            if (_coreRenderer != null && _coreRenderer.sharedMaterial != null)
                _coreRenderer.sharedMaterial.SetColor("_Color", coreColor * coreBoost);
        }

        void SetChakraLevel(int index, float level)
        {
            if (chakras != null) chakras.SetExternalLevel(index, level);
        }

        void SetVortexIntensity(float value)
        {
            for (int i = 0; i < _vortices.Length; i++)
                if (_vortices[i] != null) _vortices[i].intensity = value;
        }

        void SetVortexState(float intensity, float flow)
        {
            for (int i = 0; i < _vortices.Length; i++)
            {
                var vortex = _vortices[i];
                if (vortex == null) continue;
                vortex.intensity = intensity;
                vortex.flow = flow;
            }
        }

        Transform EnsureChild(string name)
        {
            var t = transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                t = go.transform;
                t.SetParent(transform, false);
            }
            return t;
        }

        /// <summary>Anchors the vortex at chakra i's point, in its colour.</summary>
        void SetupVortexForChakra(int index)
        {
            if (_vortices.Length == 0) return;

            float height = 0.10f;
            Color color = new Color(0.95f, 0.07f, 0.10f);
            if (chakras != null && index >= 0 && index < chakras.ChakraCount)
            {
                var c = chakras.GetChakra(index);
                height = c.height;
                color = c.color;
            }

            if (_vortexRoot != null)
                _vortexRoot.localPosition = new Vector3(0f, height, 0f);

            for (int i = 0; i < _vortices.Length; i++)
            {
                var vortex = _vortices[i];
                if (vortex == null) continue;
                vortex.color = color * colorBoost;
                vortex.ApplyMaterialProps();
            }
        }

        [ContextMenu("Rebuild")]
        public void Apply()
        {
            if (chakras == null)
            {
                var root = transform.parent != null ? transform.parent : transform;
                chakras = root.GetComponentInChildren<ChakraSystem>();
                if (chakras == null)
                    chakras = FindAnyObjectByType<ChakraSystem>();
            }

            _vortexRoot = EnsureChild("ChakraVortex");
            _vortices = _vortexRoot.GetComponentsInChildren<VortexParticles>(true);
            if (_vortices.Length == 0)
            {
                _vortexRoot.gameObject.AddComponent<VortexParticles>();
                _vortices = _vortexRoot.GetComponentsInChildren<VortexParticles>(true);
            }
            SetupVortexForChakra(_chakra);

            // The earth core: the glowing orb far below.
            _core = EnsureChild("EarthCore");
            _core.localPosition = new Vector3(0f, -coreDepth, 0f);
            _core.localScale = Vector3.one * (coreRadius * 2f);
            var mf = _core.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = _core.gameObject.AddComponent<MeshFilter>();
                var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }
            _coreRenderer = _core.GetComponent<MeshRenderer>();
            if (_coreRenderer == null) _coreRenderer = _core.gameObject.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Meditation/CoreGlow");
            if (shader != null)
            {
                if (_coreRenderer.sharedMaterial == null || _coreRenderer.sharedMaterial.shader != shader)
                    _coreRenderer.sharedMaterial = new Material(shader) { name = "EarthCoreMat" };
                _coreRenderer.sharedMaterial.SetColor("_Color", coreColor);
            }
            _coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _coreRenderer.receiveShadows = false;
        }
    }
}
