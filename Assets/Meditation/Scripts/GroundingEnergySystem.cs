using UnityEngine;

namespace Meditation
{
    /// <summary>
    /// The grounding energy cycle of the meditation. For each chakra in turn
    /// (root -> crown) a compact travelling whirl of that chakra's colour
    /// (<see cref="VortexParticles"/>, Lamb-Oseen vortex maths) rises from
    /// the earth's core, is absorbed into that chakra's point in the body -
    /// the point ignites exactly as the whirl enters - glows for the hold
    /// time, then the whirl re-emerges and descends back into the core while
    /// the point fades together with it.
    ///
    /// Two whirls work in relay: while one is still connected to its point,
    /// the next colour already launches from the core, so the point is never
    /// dark for long (the wait between colours is riseTime - launchLead).
    /// Drives <see cref="ChakraSystem"/> (External mode).
    /// </summary>
    [ExecuteAlways]
    public class GroundingEnergySystem : MonoBehaviour
    {
        enum Phase { Idle, Rise, Hold, Descend }

        [Header("References")]
        [Tooltip("Chakra system to sync with; auto-found next to the avatar when empty.")]
        public ChakraSystem chakras;

        [Header("Earth core")]
        [Tooltip("How far below this object the earth core sits, metres.")]
        public float coreDepth = 12f;
        public float coreRadius = 1.6f;
        [ColorUsage(false, true)] public Color coreColor = new Color(2.2f, 1.0f, 0.35f);
        [Tooltip("How much the core brightens while a whirl is rising.")]
        public float coreFlare = 1.6f;

        [Header("Whirl")]
        [Tooltip("Radius of the whirl's wide lower skirt, metres.")]
        public float whirlRadius = 0.9f;
        [Tooltip("Height of the whirl, metres.")]
        public float whirlHeight = 2.8f;
        [Tooltip("Lamb-Oseen circulation Gamma, m^2/s: overall swirl strength.")]
        public float circulation = 3.5f;
        [Tooltip("HDR boost applied to the chakra colour.")]
        public float colorBoost = 2.0f;

        [Header("Cycle timing (seconds)")]
        [Tooltip("Core -> into the chakra point.")]
        public float riseTime = 6f;
        [Tooltip("Absorbed in the point: it glows.")]
        public float holdTime = 2f;
        [Tooltip("Out of the point -> back down into the core.")]
        public float descendTime = 6f;
        [Tooltip("The next colour launches this many seconds before the current one leaves its point. Wait between colours at the point = riseTime - launchLead.")]
        public float launchLead = 1f;

        [Header("Breath sync (optional)")]
        [Tooltip("Metronome audio. When assigned and playing, the cycle follows the audio clock (inhale = rise, hold = glow, exhale = descend) instead of the timers above.")]
        public AudioSource metronome;
        [Tooltip("Seconds into the audio where the first inhale begins.")]
        public float breathOffset = 12.6f;
        [Tooltip("Length of one full breath cycle in the audio, seconds.")]
        public float breathCycle = 28.06f;
        [Tooltip("Inhale / hold / exhale durations within the cycle, seconds.")]
        public float inhaleTime = 4f;
        public float breathHoldTime = 16f;
        public float exhaleTime = 8f;

        class WhirlSlot
        {
            public VortexParticles vp;
            public int chakra;
            public Phase phase = Phase.Idle;
            public float t;
        }

        readonly WhirlSlot[] _slots = { new WhirlSlot(), new WhirlSlot() };
        Transform _core;
        MeshRenderer _coreRenderer;
        int _nextChakra;
        bool _dirty;

        // Breath-clock bookkeeping: AudioSource.time wraps every loop of the
        // clip, so count the wraps to keep a monotonic timeline (otherwise
        // the chakra index would stay stuck on the first colour).
        float _prevAudioTime;
        int _audioLoops;

        void OnEnable()
        {
            Apply();
            _nextChakra = 0;
            _slots[0].phase = Phase.Idle;
            _slots[1].phase = Phase.Idle;
            if (Application.isPlaying) Launch(_slots[0]);
        }

        void OnValidate() { if (isActiveAndEnabled) _dirty = true; }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // Near-constant travel speed across the whole phase (a breath is one
        // steady motion), with just a touch of softness at the ends.
        static float EaseTravel(float x)
        {
            x = Mathf.Clamp01(x);
            return Mathf.Lerp(x, x * x * (3f - 2f * x), 0.25f);
        }

        void Launch(WhirlSlot slot)
        {
            slot.chakra = _nextChakra;
            int count = chakras != null ? chakras.ChakraCount : 7;
            _nextChakra = (_nextChakra + 1) % count;
            slot.phase = Phase.Rise;
            slot.t = 0f;
            SetupVortexForChakra(slot);
        }

        void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Apply();
            }

            if (_slots[0].vp == null || _slots[1].vp == null) return;

            if (!Application.isPlaying)
            {
                // Editor preview: one whirl mid-path, the other hidden.
                _slots[0].vp.center = 0.3f;
                _slots[1].vp.center = 1.2f;
                return;
            }

            if (chakras != null) chakras.mode = ChakraSystem.Mode.External;

            // Breath-clock mode: the metronome audio drives everything.
            if (metronome != null && metronome.isPlaying)
            {
                UpdateBreathSync();
                return;
            }

            bool anyRising = false;

            for (int s = 0; s < 2; s++)
            {
                var slot = _slots[s];
                var other = _slots[1 - s];
                float skirt = slot.vp.SkirtFrac;
                float hidden = -(skirt + 0.03f);
                const float start = 1.08f;
                float c;

                slot.t += Time.deltaTime;

                switch (slot.phase)
                {
                    case Phase.Rise:
                        c = Mathf.Lerp(start, hidden, Smooth(slot.t / riseTime));
                        anyRising = true;
                        if (slot.t >= riseTime) { slot.phase = Phase.Hold; slot.t = 0f; }
                        break;

                    case Phase.Hold:
                        c = hidden;
                        // Relay: send the next colour up early enough that it
                        // arrives launchLead seconds after this one leaves.
                        if (other.phase == Phase.Idle &&
                            slot.t >= Mathf.Max(0f, holdTime - launchLead))
                            Launch(other);
                        if (slot.t >= holdTime) { slot.phase = Phase.Descend; slot.t = 0f; }
                        break;

                    case Phase.Descend:
                        c = Mathf.Lerp(hidden, start, Smooth(slot.t / descendTime));
                        if (slot.t >= descendTime)
                        {
                            slot.phase = Phase.Idle;
                            slot.t = 0f;
                            SetChakraLevel(slot.chakra, 0f);
                            if (other.phase == Phase.Idle) Launch(slot); // safety net
                        }
                        break;

                    default: // Idle
                        c = 1.2f;
                        break;
                }

                slot.vp.center = c;

                if (slot.phase != Phase.Idle)
                {
                    // The point's glow IS the absorbed fraction of its whirl.
                    float glow = Smooth((0.05f - c) / (0.05f - hidden));
                    SetChakraLevel(slot.chakra, glow);
                }
            }

            if (_coreRenderer != null && _coreRenderer.sharedMaterial != null)
                _coreRenderer.sharedMaterial.SetColor("_Color",
                    coreColor * (anyRising ? coreFlare : 1f));
        }

        void SetChakraLevel(int index, float level)
        {
            if (chakras != null) chakras.SetExternalLevel(index, level);
        }

        /// <summary>
        /// Breath-clock mode: phases follow the metronome audio position, so
        /// the whirl and the recorded breathing can never drift apart.
        /// Inhale = whirl rises core -> point, hold = the point glows,
        /// exhale = whirl returns point -> core. One breath = one chakra.
        /// </summary>
        void UpdateBreathSync()
        {
            var slot = _slots[0];
            var idle = _slots[1];
            idle.phase = Phase.Idle;
            idle.vp.center = 1.2f;

            float skirt = slot.vp.SkirtFrac;
            float hidden = -(skirt + 0.03f);
            const float start = 1.08f;

            // Monotonic audio timeline across clip loops.
            float at = metronome.time;
            if (at < _prevAudioTime - 1f) _audioLoops++;
            _prevAudioTime = at;
            float clipLen = metronome.clip != null ? metronome.clip.length : breathCycle;
            float t = _audioLoops * clipLen + at - breathOffset;

            if (t < 0f)
            {
                // Intro before the first inhale: rest in the core.
                slot.vp.center = 1.2f;
                return;
            }

            int cycleIndex = Mathf.FloorToInt(t / breathCycle);
            float tc = t - cycleIndex * breathCycle;

            int count = chakras != null ? chakras.ChakraCount : 7;
            int chakra = cycleIndex % count;
            if (chakra != slot.chakra || slot.phase == Phase.Idle)
            {
                SetChakraLevel(slot.chakra, 0f);
                slot.chakra = chakra;
                slot.phase = Phase.Rise;
                SetupVortexForChakra(slot);
            }

            float c;
            bool rising = false;
            if (tc < inhaleTime)
            {
                // Steady climb for the WHOLE inhale: 0..4 s = core..point.
                c = Mathf.Lerp(start, hidden, EaseTravel(tc / inhaleTime));
                rising = true;
            }
            else if (tc < inhaleTime + breathHoldTime)
            {
                c = hidden;
            }
            else if (tc < inhaleTime + breathHoldTime + exhaleTime)
            {
                // Steady descent for the WHOLE exhale.
                c = Mathf.Lerp(hidden, start,
                    EaseTravel((tc - inhaleTime - breathHoldTime) / exhaleTime));
            }
            else
            {
                c = 1.2f;   // slack at the end of the audio cycle
            }

            slot.vp.center = c;
            float glow = Smooth((0.05f - c) / (0.05f - hidden));
            SetChakraLevel(slot.chakra, glow);

            if (_coreRenderer != null && _coreRenderer.sharedMaterial != null)
                _coreRenderer.sharedMaterial.SetColor("_Color",
                    coreColor * (rising ? coreFlare : 1f));
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

        void DestroyChild(string name)
        {
            var t = transform.Find(name);
            if (t == null) return;
            if (Application.isPlaying) Destroy(t.gameObject);
            else DestroyImmediate(t.gameObject);
        }

        /// <summary>Points a slot's whirl at its chakra: height and colour.</summary>
        void SetupVortexForChakra(WhirlSlot slot)
        {
            if (slot.vp == null) return;

            float height = 0.10f;
            Color color = new Color(0.95f, 0.07f, 0.10f);
            if (chakras != null && slot.chakra >= 0 && slot.chakra < chakras.ChakraCount)
            {
                var c = chakras.GetChakra(slot.chakra);
                height = c.height;
                color = c.color;
            }

            slot.vp.transform.localPosition = new Vector3(0f, height, 0f);
            slot.vp.length = coreDepth + height;
            slot.vp.baseRadius = whirlRadius;
            slot.vp.skirtMeters = whirlHeight;
            slot.vp.circulation = circulation;
            slot.vp.color = color * colorBoost;
            slot.vp.ApplyMaterialProps();
        }

        VortexParticles EnsureWhirl(string name)
        {
            var t = EnsureChild(name);
            var vp = t.GetComponent<VortexParticles>();
            if (vp == null) vp = t.gameObject.AddComponent<VortexParticles>();
            return vp;
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

            // Retired children from earlier iterations.
            DestroyChild("DescendingVortex");
            DestroyChild("AscendingVortex");
            DestroyChild("ChakraVortex");

            _slots[0].vp = EnsureWhirl("ChakraVortexA");
            _slots[1].vp = EnsureWhirl("ChakraVortexB");
            SetupVortexForChakra(_slots[0]);
            SetupVortexForChakra(_slots[1]);

            // The earth core: the glowing orb the whirls rise from.
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
