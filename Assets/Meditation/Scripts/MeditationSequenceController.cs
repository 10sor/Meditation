using System;
using UnityEngine;

namespace Meditation
{
    public enum ChakraSessionStage
    {
        Pause,
        Countdown,
        Breathing,
        ReverseCountdown
    }

    [DisallowMultipleComponent]
    public sealed class MeditationSequenceController : MonoBehaviour
    {
        [SerializeField] ChakraBreathController breathController;
        [SerializeField] SceneVisualController visualController;
        [Tooltip("Starts the sequence automatically when the scene has no start screen.")]
        [SerializeField] bool autoStart;
        [Min(0f)] [SerializeField] float pauseDuration = 5f;
        [Min(0f)] [SerializeField] float countdownDuration = 3f;
        [Tooltip("Duration of the visual-only inhale and exhale during countdowns.")]
        [Min(0f)] [SerializeField] float countdownBreathDuration = 3f;
        [Min(1)] [SerializeField] int cycleCount = 8;

        public event Action<ChakraSessionStage> StageStarted;
        public event Action<ChakraSessionStage> StageCompleted;
        public event Action<int> ChakraChanged;
        public event Action MeditationCompleted;

        public int CurrentChakraIndex { get; private set; }
        public ChakraSessionStage CurrentStage { get; private set; }
        public float CurrentStageProgress { get; private set; }
        public int CurrentBreathCycleIndex { get; private set; } = -1;
        public int CycleCount => cycleCount;
        public bool IsRunning { get; private set; }

        const int ChakraCount = 7;
        double stageStartTime;

        void Awake()
        {
            if (breathController == null)
                breathController = GetComponent<ChakraBreathController>();
            if (visualController == null)
                visualController = FindAnyObjectByType<SceneVisualController>();
            ResetSequence();
        }

        void OnEnable()
        {
            if (breathController != null)
                breathController.BreathCycleCompleted += OnBreathCycleCompleted;
        }

        void Start()
        {
            if (autoStart)
                StartMeditation();
        }

        void OnDisable()
        {
            if (breathController != null)
            {
                breathController.BreathCycleCompleted -= OnBreathCycleCompleted;
                breathController.StopBreathCycle();
            }
            IsRunning = false;
        }

        void Update()
        {
            if (!IsRunning || CurrentStage == ChakraSessionStage.Breathing)
                return;

            float duration = CurrentStage == ChakraSessionStage.Pause
                ? pauseDuration : countdownDuration;
            float elapsed = Mathf.Max(0f,
                (float)(AudioSettings.dspTime - stageStartTime));
            float progress = duration <= 0f ? 1f :
                Mathf.Clamp01(elapsed / duration);
            CurrentStageProgress = CurrentStage == ChakraSessionStage.ReverseCountdown
                ? 1f - progress : progress;
            ApplyStageVisual();
            ApplyCountdownBreath(elapsed);

            if (elapsed >= duration)
                CompleteTimedStage();
        }

        public void StartMeditation()
        {
            if (IsRunning)
                return;

            ResetSequence();
            IsRunning = true;
            ChakraChanged?.Invoke(CurrentChakraIndex);
            BeginStage(ChakraSessionStage.Pause);
        }

        public void StopMeditation()
        {
            IsRunning = false;
            if (breathController != null)
                breathController.StopBreathCycle();
            CurrentStageProgress = 0f;
            CurrentBreathCycleIndex = -1;
            SetVisualBlend(0f);
        }

        void CompleteTimedStage()
        {
            ChakraSessionStage completed = CurrentStage;
            StageCompleted?.Invoke(completed);
            switch (completed)
            {
                case ChakraSessionStage.Pause:
                    BeginStage(ChakraSessionStage.Countdown);
                    break;
                case ChakraSessionStage.Countdown:
                    BeginBreathing();
                    break;
                case ChakraSessionStage.ReverseCountdown:
                    AdvanceChakraOrComplete();
                    break;
            }
        }

        void BeginStage(ChakraSessionStage stage)
        {
            CurrentStage = stage;
            CurrentStageProgress = stage == ChakraSessionStage.ReverseCountdown ? 1f : 0f;
            stageStartTime = AudioSettings.dspTime;
            ApplyStageVisual();
            if (stage == ChakraSessionStage.ReverseCountdown)
                ApplyCountdownBreath(0f);
            else if (stage == ChakraSessionStage.Pause)
                breathController?.StopVisualOnlyBreath();
            StageStarted?.Invoke(stage);
        }

        void BeginBreathing()
        {
            BeginStage(ChakraSessionStage.Breathing);
            CurrentBreathCycleIndex = 0;
            StartCurrentBreathCycle();
        }

        void StartCurrentBreathCycle()
        {
            CurrentStageProgress = (float)CurrentBreathCycleIndex / cycleCount;
            if (breathController == null)
            {
                OnBreathCycleCompleted();
                return;
            }
            breathController.StartBreathCycle();
        }

        void OnBreathCycleCompleted()
        {
            if (!IsRunning || CurrentStage != ChakraSessionStage.Breathing)
                return;

            double nextCycleStart = breathController != null
                ? breathController.CycleEndDspTime : AudioSettings.dspTime;
            CurrentBreathCycleIndex++;
            CurrentStageProgress = Mathf.Clamp01(
                (float)CurrentBreathCycleIndex / cycleCount);
            if (CurrentBreathCycleIndex < cycleCount)
            {
                CurrentStageProgress =
                    (float)CurrentBreathCycleIndex / cycleCount;
                if (breathController != null)
                    breathController.StartBreathCycle(nextCycleStart);
                else
                    OnBreathCycleCompleted();
                return;
            }

            StageCompleted?.Invoke(ChakraSessionStage.Breathing);
            CurrentBreathCycleIndex = -1;
            BeginStage(ChakraSessionStage.ReverseCountdown);
        }

        void AdvanceChakraOrComplete()
        {
            if (CurrentChakraIndex + 1 >= ChakraCount)
            {
                IsRunning = false;
                CurrentStageProgress = 0f;
                CurrentBreathCycleIndex = -1;
                SetVisualBlend(0f);
                if (breathController != null)
                    breathController.StopVisualOnlyBreath();
                MeditationCompleted?.Invoke();
                return;
            }

            CurrentChakraIndex++;
            CurrentBreathCycleIndex = -1;
            ChakraChanged?.Invoke(CurrentChakraIndex);
            BeginStage(ChakraSessionStage.Pause);
        }

        void ResetSequence()
        {
            if (breathController != null)
                breathController.StopBreathCycle();
            IsRunning = false;
            CurrentChakraIndex = 0;
            CurrentStage = ChakraSessionStage.Pause;
            CurrentStageProgress = 0f;
            CurrentBreathCycleIndex = -1;
            SetVisualBlend(0f);
        }

        void ApplyStageVisual()
        {
            switch (CurrentStage)
            {
                case ChakraSessionStage.Countdown:
                case ChakraSessionStage.ReverseCountdown:
                    SetVisualBlend(CurrentStageProgress);
                    break;
                case ChakraSessionStage.Breathing:
                    SetVisualBlend(1f);
                    break;
                default:
                    SetVisualBlend(0f);
                    break;
            }
        }

        void SetVisualBlend(float value)
        {
            if (visualController != null)
                visualController.SetLerp(Mathf.Clamp01(value));
        }

        void ApplyCountdownBreath(float elapsed)
        {
            if (breathController == null)
                return;

            float breathDuration = Mathf.Min(countdownBreathDuration,
                countdownDuration);
            float breathStart = Mathf.Max(0f, countdownDuration - breathDuration);
            switch (CurrentStage)
            {
                case ChakraSessionStage.Countdown:
                    if (elapsed <= breathStart && breathDuration > 0f)
                    {
                        breathController.StopVisualOnlyBreath();
                        break;
                    }

                    float inhaleProgress = breathDuration <= 0f ? 1f :
                        Mathf.Clamp01((elapsed - breathStart) / breathDuration);
                    breathController.SetVisualOnlyBreath(
                        inhaleProgress, -1);
                    break;
                case ChakraSessionStage.ReverseCountdown:
                    float exhaleProgress = breathDuration <= 0f ? 1f :
                        Mathf.Clamp01(elapsed / breathDuration);
                    breathController.SetVisualOnlyBreath(
                        1f - exhaleProgress, 1);
                    break;
                default:
                    breathController.StopVisualOnlyBreath();
                    break;
            }
        }

        void OnValidate()
        {
            pauseDuration = Mathf.Max(0f, pauseDuration);
            countdownDuration = Mathf.Max(0f, countdownDuration);
            countdownBreathDuration = Mathf.Max(0f, countdownBreathDuration);
            cycleCount = Mathf.Max(1, cycleCount);
        }
    }
}
