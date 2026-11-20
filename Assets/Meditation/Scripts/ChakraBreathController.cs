using System;
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

        [Header("Rhythm")]
        [Min(1f)] [SerializeField] float beatsPerMinute = 60f;
        [Min(0)] [SerializeField] int exhaleIntervals = 8;
        [Min(0)] [SerializeField] int pauseIntervals = 8;
        [Min(0)] [SerializeField] int inhaleIntervals = 4;
        [Min(0)] [SerializeField] int endPauseIntervals = 8;

        public event Action BreathCycleCompleted;

        public float Intensity { get; private set; }
        public int Direction { get; private set; } = 1;
        public float PhaseTime { get; private set; }
        public float ExhaleDuration => exhaleIntervals * BeatDuration;
        public float HoldDuration => pauseIntervals * BeatDuration;
        public float InhaleDuration => inhaleIntervals * BeatDuration;
        public float EndPauseDuration => endPauseIntervals * BeatDuration;
        public float CycleDuration => ExhaleDuration + HoldDuration +
                                      InhaleDuration + EndPauseDuration;
        public bool IsCycleRunning { get; private set; }
        public double CycleEndDspTime => sequenceStartDspTime + CycleDuration;

        float BeatDuration => 60f / Mathf.Max(1f, beatsPerMinute);
        const double ScheduleLeadTime = 0.1;
        double sequenceStartDspTime;
        int nextBeatToSchedule;

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
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                StopBreathCycle();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!IsCycleRunning)
                return;

            ScheduleMetronomeAudio();
            double timeline = AudioSettings.dspTime - sequenceStartDspTime;
            if (timeline < 0d)
                return;

            if (timeline >= CycleDuration)
            {
                IsCycleRunning = false;
                StopSources();
                ResetVisualState();
                BreathCycleCompleted?.Invoke();
                return;
            }

            EvaluateTimeline((float)timeline);
        }

        public void StartBreathCycle()
        {
            StartBreathCycle(AudioSettings.dspTime + ScheduleLeadTime);
        }

        public void StartBreathCycle(double dspStartTime)
        {
            StopBreathCycle();
            sequenceStartDspTime = dspStartTime;
            nextBeatToSchedule = 0;
            IsCycleRunning = true;
            ScheduleAudio();

            if (CycleDuration <= 0f)
            {
                IsCycleRunning = false;
                BreathCycleCompleted?.Invoke();
            }
        }

        public void StopBreathCycle()
        {
            IsCycleRunning = false;
            StopSources();
            ResetVisualState();
        }

        void ScheduleAudio()
        {
            ScheduleMetronomeAudio();
            ScheduleStretchedClip(exhaleSource, exhaleClip,
                sequenceStartDspTime, ExhaleDuration);
            ScheduleStretchedClip(inhaleSource, inhaleClip,
                sequenceStartDspTime + ExhaleDuration + HoldDuration, InhaleDuration);
        }

        void ScheduleMetronomeAudio()
        {
            int intervalCount = exhaleIntervals + pauseIntervals +
                                inhaleIntervals + endPauseIntervals;
            double scheduleUntil = AudioSettings.dspTime + ScheduleLeadTime + 0.25;
            while (nextBeatToSchedule < intervalCount &&
                   sequenceStartDspTime + nextBeatToSchedule * BeatDuration <= scheduleUntil)
            {
                ScheduleMetronome(nextBeatToSchedule,
                    sequenceStartDspTime + nextBeatToSchedule * BeatDuration);
                nextBeatToSchedule++;
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
            PhaseTime = timeline;

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
            IsCycleRunning = false;
            ResetVisualState();
        }

        void ResetVisualState()
        {
            Intensity = 0f;
            Direction = 1;
            PhaseTime = 0f;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        void OnValidate()
        {
            beatsPerMinute = Mathf.Max(1f, beatsPerMinute);
            exhaleIntervals = Mathf.Max(0, exhaleIntervals);
            pauseIntervals = Mathf.Max(0, pauseIntervals);
            inhaleIntervals = Mathf.Max(0, inhaleIntervals);
            endPauseIntervals = Mathf.Max(0, endPauseIntervals);
        }
    }
}
