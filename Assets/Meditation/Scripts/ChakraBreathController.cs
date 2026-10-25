using UnityEngine;

namespace Meditation
{
//    [DisallowMultipleComponent]
//    [RequireComponent(typeof(AudioSource))]
    public sealed class ChakraBreathController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] AudioSource metronomeSourceA;
        [SerializeField] AudioSource metronomeSourceB;
        [SerializeField] AudioSource exhaleSource;
        [SerializeField] AudioSource inhaleSource;
        [SerializeField] AudioClip metronomeClipA;
        [SerializeField] AudioClip metronomeClipB;
        [SerializeField] AudioClip exhaleClip;
        [SerializeField] AudioClip inhaleClip;
        [SerializeField] bool playOnEnable = true;

        [Header("Rhythm")]
        [Min(1f)] [SerializeField] float beatsPerMinute = 60f;
        [Min(1)] [SerializeField] int exhaleIntervals = 8;
        [Min(0)] [SerializeField] int pauseIntervals = 8;
        [Min(1)] [SerializeField] int inhaleIntervals = 4;
        [Min(0)] [SerializeField] int endPauseIntervals = 8;

        [Header("Sequence")]
        [Min(1)] [SerializeField] int chakraCount = 7;

        public int CurrentChakraIndex { get; private set; }
        public float Intensity { get; private set; }
        public int Direction { get; private set; } = 1;
        public float PhaseTime { get; private set; }
        public float ExhaleDuration => exhaleIntervals * BeatDuration;
        public float HoldDuration => pauseIntervals * BeatDuration;
        public float InhaleDuration => inhaleIntervals * BeatDuration;
        public float EndPauseDuration => endPauseIntervals * BeatDuration;

        float BeatDuration => 60f / Mathf.Max(1f, beatsPerMinute);
        int CycleIntervals => Mathf.Max(1,
            exhaleIntervals + pauseIntervals + inhaleIntervals + endPauseIntervals);

        const double ScheduleLeadTime = 0.1;
        double sequenceStartDspTime;
        long nextBeatToSchedule;
        long nextCycleToSchedule;
        bool sequenceRunning;

        void Reset() => metronomeSourceA = GetComponent<AudioSource>();

        void Awake()
        {
            if (metronomeSourceA == null)
                metronomeSourceA = GetComponent<AudioSource>();

            metronomeSourceB = EnsureSource(metronomeSourceB);
            exhaleSource = EnsureSource(exhaleSource);
            inhaleSource = EnsureSource(inhaleSource);
            CopyAudioSettings(metronomeSourceA, metronomeSourceB);
            CopyAudioSettings(metronomeSourceA, exhaleSource);
            CopyAudioSettings(metronomeSourceA, inhaleSource);
            ConfigureSource(metronomeSourceA);
            ConfigureSource(metronomeSourceB);
            ConfigureSource(exhaleSource);
            ConfigureSource(inhaleSource);
            LoadAudioData();
        }

        void OnEnable()
        {
            ResetState();
            if (Application.isPlaying && playOnEnable)
                StartSequence();
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                StopSequence();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!sequenceRunning)
                return;

            ScheduleAudio();
            float timeline = Mathf.Max(0f,
                (float)(AudioSettings.dspTime - sequenceStartDspTime));
            EvaluateTimeline(timeline);
        }

        void StartSequence()
        {
            StopSources();
            sequenceStartDspTime = AudioSettings.dspTime + ScheduleLeadTime;
            nextBeatToSchedule = 0;
            nextCycleToSchedule = 0;
            sequenceRunning = true;
            ScheduleAudio();
        }

        void StopSequence()
        {
            sequenceRunning = false;
            StopSources();
        }

        void ScheduleAudio()
        {
            double scheduleUntil = AudioSettings.dspTime + ScheduleLeadTime + 0.25;
            double beatDuration = BeatDuration;

            while (sequenceStartDspTime + nextBeatToSchedule * beatDuration <= scheduleUntil)
            {
                ScheduleMetronome(nextBeatToSchedule,
                    sequenceStartDspTime + nextBeatToSchedule * beatDuration);
                nextBeatToSchedule++;
            }

            double cycleDuration = CycleIntervals * beatDuration;
            while (sequenceStartDspTime + nextCycleToSchedule * cycleDuration <= scheduleUntil)
            {
                double cycleStart = sequenceStartDspTime + nextCycleToSchedule * cycleDuration;
                ScheduleStretchedClip(exhaleSource, exhaleClip, cycleStart, ExhaleDuration);
                ScheduleStretchedClip(inhaleSource, inhaleClip,
                    cycleStart + ExhaleDuration + HoldDuration, InhaleDuration);
                nextCycleToSchedule++;
            }
        }

        void ScheduleMetronome(long beatIndex, double dspTime)
        {
            bool useA = (beatIndex & 1) == 0;
            AudioSource source = useA ? metronomeSourceA : metronomeSourceB;
            AudioClip clip = useA ? metronomeClipA : metronomeClipB;
            if (source == null || clip == null)
                return;

            source.clip = clip;
            source.pitch = 1f;
            source.PlayScheduled(dspTime);
        }

        static void ScheduleStretchedClip(
            AudioSource source, AudioClip clip, double dspTime, float targetDuration)
        {
            if (source == null || clip == null || targetDuration <= 0f)
                return;

            source.clip = clip;
            source.pitch = Mathf.Clamp(clip.length / targetDuration, -3f, 3f);
            source.PlayScheduled(dspTime);
            source.SetScheduledEndTime(dspTime + targetDuration);
        }

        void EvaluateTimeline(float timeline)
        {
            float cycleDuration = CycleIntervals * BeatDuration;
            int cycleIndex = Mathf.FloorToInt(timeline / cycleDuration);
            CurrentChakraIndex = cycleIndex % Mathf.Max(1, chakraCount);
            PhaseTime = timeline - cycleIndex * cycleDuration;

            if (PhaseTime < ExhaleDuration)
            {
                Direction = 1;
                Intensity = 1f - Smooth01(PhaseTime / Mathf.Max(0.001f, ExhaleDuration));
            }
            else if (PhaseTime < ExhaleDuration + HoldDuration)
            {
                Direction = 1;
                Intensity = 0f;
            }
            else if (PhaseTime < ExhaleDuration + HoldDuration + InhaleDuration)
            {
                Direction = -1;
                float inhaleProgress = (PhaseTime - ExhaleDuration - HoldDuration) /
                                       Mathf.Max(0.001f, InhaleDuration);
                Intensity = Smooth01(inhaleProgress);
            }
            else
            {
                Direction = -1;
                Intensity = 1f;
            }
        }

        AudioSource EnsureSource(AudioSource source)
        {
            if (source != null)
                return source;

            source = gameObject.AddComponent<AudioSource>();
            ConfigureSource(source);
            return source;
        }

        static void ConfigureSource(AudioSource source)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.loop = false;
        }

        static void CopyAudioSettings(AudioSource template, AudioSource target)
        {
            if (template == null || target == null || template == target)
                return;

            target.outputAudioMixerGroup = template.outputAudioMixerGroup;
            target.volume = template.volume;
            target.spatialBlend = template.spatialBlend;
            target.spatialize = template.spatialize;
            target.dopplerLevel = template.dopplerLevel;
            target.rolloffMode = template.rolloffMode;
            target.minDistance = template.minDistance;
            target.maxDistance = template.maxDistance;
        }

        void LoadAudioData()
        {
            if (metronomeClipA != null)
                metronomeClipA.LoadAudioData();
            if (metronomeClipB != null)
                metronomeClipB.LoadAudioData();
            if (exhaleClip != null)
                exhaleClip.LoadAudioData();
            if (inhaleClip != null)
                inhaleClip.LoadAudioData();
        }

        void StopSources()
        {
            if (metronomeSourceA != null)
                metronomeSourceA.Stop();
            if (metronomeSourceB != null)
                metronomeSourceB.Stop();
            if (exhaleSource != null)
                exhaleSource.Stop();
            if (inhaleSource != null)
                inhaleSource.Stop();
        }

        void ResetState()
        {
            CurrentChakraIndex = 0;
            Intensity = 1f;
            Direction = 1;
            PhaseTime = 0f;
            sequenceRunning = false;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        void OnValidate()
        {
            chakraCount = Mathf.Max(1, chakraCount);
            beatsPerMinute = Mathf.Max(1f, beatsPerMinute);
            exhaleIntervals = Mathf.Max(1, exhaleIntervals);
            pauseIntervals = Mathf.Max(0, pauseIntervals);
            inhaleIntervals = Mathf.Max(1, inhaleIntervals);
            endPauseIntervals = Mathf.Max(0, endPauseIntervals);
        }
    }
}
