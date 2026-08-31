using UnityEngine;

namespace Meditation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ChakraBreathController : MonoBehaviour
    {
        [Header("Metronome")]
        [SerializeField] AudioSource metronome;
        [SerializeField] bool playOnEnable = true;

        [Header("Sequence")]
        [Min(1)] [SerializeField] int chakraCount = 7;
        [Min(0f)] [SerializeField] float breathOffset = 0.46f;
        [Min(0.01f)] [SerializeField] float breathCycle = 20.07f;
        [Min(0f)] [SerializeField] float inhaleTime = 4.04f;
        [Min(0f)] [SerializeField] float holdTime = 8.01f;
        [Min(0f)] [SerializeField] float exhaleTime = 8.02f;

        public int CurrentChakraIndex { get; private set; }
        public float Intensity { get; private set; }
        public int Direction { get; private set; } = -1;
        public float PhaseTime { get; private set; }
        public float InhaleDuration => inhaleTime;
        public float HoldDuration => holdTime;
        public float ExhaleDuration => exhaleTime;

        int previousSample;
        long completedSamples;
        float fallbackTime;

        void Reset() => metronome = GetComponent<AudioSource>();

        void Awake()
        {
            if (metronome == null)
                metronome = GetComponent<AudioSource>();
        }

        void OnEnable()
        {
            CurrentChakraIndex = 0;
            Intensity = 0f;
            Direction = -1;
            previousSample = 0;
            completedSamples = 0;
            fallbackTime = 0f;
            PhaseTime = 0f;

            if (Application.isPlaying && playOnEnable && metronome != null &&
                metronome.clip != null && !metronome.isPlaying)
            {
                metronome.Play();
            }
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            float timeline = GetTimelineSeconds() - breathOffset;
            EvaluateTimeline(timeline);
        }

        float GetTimelineSeconds()
        {
            if (metronome != null && metronome.clip != null && metronome.isPlaying)
            {
                int currentSample = metronome.timeSamples;
                int clipSamples = metronome.clip.samples;
                if (currentSample < previousSample - clipSamples / 2)
                    completedSamples += clipSamples;

                previousSample = currentSample;
                return (completedSamples + currentSample) / (float)metronome.clip.frequency;
            }

            fallbackTime += Time.deltaTime;
            return fallbackTime;
        }

        void EvaluateTimeline(float timeline)
        {
            if (timeline < 0f)
            {
                CurrentChakraIndex = 0;
                Intensity = 0f;
                Direction = -1;
                PhaseTime = 0f;
                return;
            }

            float cycle = Mathf.Max(0.01f, breathCycle);
            int cycleIndex = Mathf.FloorToInt(timeline / cycle);
            CurrentChakraIndex = cycleIndex % Mathf.Max(1, chakraCount);
            float phaseTime = timeline - cycleIndex * cycle;
            PhaseTime = phaseTime;

            if (phaseTime < inhaleTime)
            {
                Direction = -1;
                Intensity = Smooth01(phaseTime / Mathf.Max(0.001f, inhaleTime));
            }
            else if (phaseTime < inhaleTime + holdTime)
            {
                Direction = -1;
                Intensity = 1f;
            }
            else if (phaseTime < inhaleTime + holdTime + exhaleTime)
            {
                Direction = 1;
                float exhaleProgress = (phaseTime - inhaleTime - holdTime) /
                                       Mathf.Max(0.001f, exhaleTime);
                Intensity = 1f - Smooth01(exhaleProgress);
            }
            else
            {
                Direction = 1;
                Intensity = 0f;
            }
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        void OnValidate()
        {
            chakraCount = Mathf.Max(1, chakraCount);
            breathCycle = Mathf.Max(0.01f, breathCycle);
            inhaleTime = Mathf.Max(0f, inhaleTime);
            holdTime = Mathf.Max(0f, holdTime);
            exhaleTime = Mathf.Max(0f, exhaleTime);
        }
    }
}
