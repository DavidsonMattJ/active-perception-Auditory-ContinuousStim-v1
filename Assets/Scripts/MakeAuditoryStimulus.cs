using System.Collections;
using UnityEngine;

public class MakeAuditoryStimulus : MonoBehaviour
{
    /// <summary>
    /// Generates a continuous beep train for a tempo change detection task.
    ///
    /// Participants hear a steady train of identical beeps (1000Hz, 200ms ISI).
    /// At random times, the ISI shifts by ± delta (faster or slower).
    /// Participants press any button when they detect the tempo change.
    /// After detection or timeout, the train resets to the standard tempo.
    ///
    /// Public API:
    ///   - PlayStandardSequence()  → coroutine: plays beeps at trial start before walking
    ///   - RunBeepTrain()          → coroutine: continuous beep train during walking
    ///   - StopBeepTrain()         → stops the beep train
    ///   - SetDelta(float)         → updates the ISI delta (called by staircase)
    /// </summary>

    // ──────────────────────────────────────────────────────────────────
    //  Inspector-readable parameters
    // ──────────────────────────────────────────────────────────────────

    
    [Header("Beep Train")]
    [SerializeField]
    private float baseFrequencyHz ;
    [SerializeField]
    private float beepDurationMs;
    
    public float baseISIMs; // public to be read by AdaptiveStaircase
    
    [SerializeField]
    [Range(0f, 1f)]
    private float toneAmplitude;// = 0.8f;
    [Tooltip("Cosine ramp duration in ms to prevent click artifacts")]
    [SerializeField]
    private float rampDurationMs;// = 25f;

    [Header("ISI Change")]
    
    
    public float initialDeltaMs;// public to be read by AdpativeStaircase
    
    public float minDeltaMs ;// public to be read by AdpativeStaircase
    
    public float maxDeltaMs;// public to be read by AdpativeStaircase

    [Header("Timing")]
    [SerializeField]
    private float minStandardPeriodSec;// = 0.5f; // min period between changes, appended to detectionwindowSec when calculating onsets for minEventDUration
    [SerializeField]
    private float maxStandardPeriodSec ;//= 1.5f; // max period between changes
    [SerializeField]
    private float detectionWindowSec;// = 1f;

    [Header("Standard Presentation (pre-walk)")]
    [SerializeField]
    private int standardRepetitions;

    // ──────────────────────────────────────────────────────────────────
    //  Public state
    // ──────────────────────────────────────────────────────────────────

    public struct BeepTrainState
    {
        public float baseFrequencyHz;
        public float baseISIMs;
        public float currentDeltaMs;    // staircase-controlled ISI shift
        public bool isFaster;           // was this change faster or slower?
        public float changeOnsetTime;   // trial-relative time change started
        public bool isChanged;          // currently in changed-ISI phase?
        public int changeCount;         // how many changes so far this trial
    }

    public BeepTrainState trainState;

    // ──────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────

    private AudioSource audioSource;
    private AudioClip beepClip;
    private const int sampleRate = 44100;
    private Coroutine beepTrainCoroutine;
    private bool trainRunning;

    [SerializeField]
    GameObject scriptHolder;
    experimentParameters experimentParameters;
    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Start()
    {
        experimentParameters= scriptHolder.GetComponent<experimentParameters>();
        // Ensure we have an AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D audio (non-spatialized, same in both ears)

        // Pre-generate the single beep clip (all beeps are identical)
        float beepSec = beepDurationMs / 1000f;
        beepClip = GenerateToneClip(beepSec, baseFrequencyHz, toneAmplitude);

        // set defaults (will be shown in inspector);
        // ISI and delta ms also read by adaptive staircase.
        baseFrequencyHz= 500f;
        beepDurationMs=50f;
        baseISIMs= 100f;
        toneAmplitude = 0.8f;
        rampDurationMs = 15f;
        initialDeltaMs = 50f;
        minDeltaMs = 1f;
        maxDeltaMs = 550f;
        minStandardPeriodSec = 0.5f; // min period between changes, appended to detectionwindowSec when calculating onsets for minEventDUration
        maxStandardPeriodSec = 1.5f; // max period between changes
        detectionWindowSec = experimentParameters.responseWindow;
        standardRepetitions = 5;

        // Initialize state
        trainState.baseFrequencyHz = baseFrequencyHz;
        trainState.baseISIMs = baseISIMs;
        trainState.currentDeltaMs = initialDeltaMs;
        trainState.isChanged = false;
        trainState.changeCount = 0;
        trainRunning = false;

        Debug.Log($"MakeAuditoryStimulus initialized: freq={baseFrequencyHz}Hz, " +
                  $"beep={beepDurationMs}ms, ISI={baseISIMs}ms, " +
                  $"delta={initialDeltaMs}ms, ramp={rampDurationMs}ms");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public methods
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays standard beeps at trial start before walking begins.
    /// Uses steady base ISI.
    /// </summary>
    public IEnumerator PlayStandardSequence()
    {
        float isiSec = baseISIMs / 1000f;

        for (int i = 0; i < standardRepetitions; i++)
        {
            audioSource.PlayOneShot(beepClip);
            yield return new WaitForSecondsRealtime(isiSec);
        }

        Debug.Log($"Standard sequence complete: {standardRepetitions} beeps at {baseISIMs}ms ISI");
    }

    /// <summary>
    /// Returns the total duration of the standard sequence in seconds.
    /// </summary>
    public float GetStandardSequenceDuration()
    {
        return (baseISIMs / 1000f) * standardRepetitions;
    }

    /// <summary>
    /// Main beep train coroutine. Runs continuously during the walk.
    /// Manages standard→changed→reset transitions.
    /// Checks player input each beep cycle for hits, misses, and false alarms.
    /// </summary>
    public IEnumerator RunBeepTrain(float trialDuration, runExperiment runner)
    {
        trainRunning = true;
        trainState.changeCount = 0;
        CollectPlayerInput playerInput = runner.GetComponent<CollectPlayerInput>();
        float baseISISec = baseISIMs / 1000f;

        while (trainRunning && runner.trialTime < trialDuration)
        {
            // ── Phase 1: Standard beeps ──────────────────────────────
            float standardDuration = Random.Range(minStandardPeriodSec, maxStandardPeriodSec);
            float standardElapsed = 0f;
            trainState.isChanged = false;

            Debug.Log($"[BeepTrain] Standard phase: {standardDuration:F2}s of steady ISI");

            while (standardElapsed < standardDuration && trainRunning && runner.trialTime < trialDuration)
            {
                audioSource.PlayOneShot(beepClip);
                yield return new WaitForSecondsRealtime(baseISISec);
                standardElapsed += baseISISec;

                // Check for false alarm (button press during standard phase)
                if (playerInput.leftisPressed || playerInput.rightisPressed)
                {
                    bool respondedFaster = runner.DeriveFasterResponse(playerInput.leftisPressed);
                    Debug.Log("[BeepTrain] FALSE ALARM during standard phase");
                    runner.RecordFalseAlarm(respondedFaster);
                    // Wait for button release before continuing
                    yield return new WaitUntil(() => !playerInput.leftisPressed && !playerInput.rightisPressed);
                }
            }

            // Check if trial ended during standard phase
            if (!trainRunning || runner.trialTime >= trialDuration)
                break;

            // ── Phase 2: Changed ISI ─────────────────────────────────
            // Randomly choose faster or slower
            trainState.isFaster = Random.Range(0f, 1f) < 0.5f;
            float deltaMs = Mathf.Clamp(trainState.currentDeltaMs, minDeltaMs, maxDeltaMs);

            float changedISIMs;
            if (trainState.isFaster)
            {
                changedISIMs = baseISIMs - deltaMs;
            }
            else
            {
                changedISIMs = baseISIMs + deltaMs;
            }

            // Clamp: ISI must be > beep duration + 10ms (no overlap)
            float minISIMs = beepDurationMs + 10f;
            changedISIMs = Mathf.Max(changedISIMs, minISIMs);

            float changedISISec = changedISIMs / 1000f;

            // Record change onset
            trainState.changeOnsetTime = runner.trialTime;
            trainState.isChanged = true;
            trainState.changeCount++;

            // Create immutable StimulusEvent snapshot
            var evt = new experimentParameters.StimulusEvent(
                baseFrequencyHz: baseFrequencyHz,
                baseISIMs: baseISIMs,
                deltaMs: deltaMs,
                isFaster: trainState.isFaster,
                changeOnsetTime: trainState.changeOnsetTime,
                changeIndex: trainState.changeCount - 1
            );

            Debug.Log($"[BeepTrain] CHANGE #{trainState.changeCount}: ISI {baseISIMs}→{changedISIMs:F1}ms " +
                      $"({(trainState.isFaster ? "FASTER" : "SLOWER")}), delta={deltaMs:F1}ms");

            // Play changed beeps until detection or timeout
            float changeElapsed = 0f;
            bool detected = false;

            while (changeElapsed < detectionWindowSec && trainRunning && runner.trialTime < trialDuration)
            {
                audioSource.PlayOneShot(beepClip);
                yield return new WaitForSecondsRealtime(changedISISec);
                changeElapsed += changedISISec;

                // Check for 2AFC response (left or right button press)
                if (playerInput.leftisPressed || playerInput.rightisPressed)
                {
                    detected = true;
                    bool respondedFaster = runner.DeriveFasterResponse(playerInput.leftisPressed);
                    float rt = runner.trialTime - trainState.changeOnsetTime;
                    Debug.Log($"[BeepTrain] Response at RT={rt:F3}s — {(respondedFaster ? "Faster" : "Slower")}");

                    runner.RecordChangeEvent(evt, respondedFaster);

                    // Wait for button release before continuing
                    yield return new WaitUntil(() => !playerInput.leftisPressed && !playerInput.rightisPressed);
                    break;
                }
            }

            // If no response within window → MISS (always incorrect)
            if (!detected && trainRunning)
            {
                Debug.Log($"[BeepTrain] MISS (no response within {detectionWindowSec}s)");
                runner.RecordNoResponse(evt);
            }

            // Reset to standard phase
            trainState.isChanged = false;

        } // while trial in progress

        trainRunning = false;
        Debug.Log($"[BeepTrain] Train ended. Total changes: {trainState.changeCount}");
    }

    /// <summary>
    /// Stops the beep train and any playing audio.
    /// </summary>
    public void StopBeepTrain()
    {
        trainRunning = false;
        audioSource.Stop();

        if (beepTrainCoroutine != null)
        {
            StopCoroutine(beepTrainCoroutine);
            beepTrainCoroutine = null;
        }
    }

    /// <summary>
    /// Stops any currently playing audio (alias for trial boundary safety).
    /// </summary>
    public void StopAllAudio()
    {
        StopBeepTrain();
    }

    /// <summary>
    /// Updates the ISI delta. Called by the staircase after each change event.
    /// </summary>
    public void SetDelta(float deltaMs)
    {
        trainState.currentDeltaMs = Mathf.Clamp(deltaMs, minDeltaMs, maxDeltaMs);
        Debug.Log($"[BeepTrain] Delta updated to {trainState.currentDeltaMs:F1}ms");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tone generation (private) — reused from previous version
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a pure sine tone AudioClip with cosine onset/offset ramps.
    /// </summary>
    private AudioClip GenerateToneClip(float durationSec, float frequencyHz, float amplitude)
    {
        int sampleCount = Mathf.CeilToInt(durationSec * sampleRate);
        if (sampleCount < 1) sampleCount = 1;

        float[] samples = new float[sampleCount];

        float rampSec = rampDurationMs / 1000f;
        int rampSamples = Mathf.CeilToInt(rampSec * sampleRate);
        rampSamples = Mathf.Min(rampSamples, sampleCount / 2);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float sample = amplitude * Mathf.Sin(2f * Mathf.PI * frequencyHz * t);

            // Cosine onset ramp
            if (i < rampSamples)
            {
                float rampFraction = (float)i / rampSamples;
                sample *= 0.5f * (1f - Mathf.Cos(Mathf.PI * rampFraction));
            }
            // Cosine offset ramp
            else if (i >= sampleCount - rampSamples)
            {
                float rampFraction = (float)(i - (sampleCount - rampSamples)) / rampSamples;
                sample *= 0.5f * (1f + Mathf.Cos(Mathf.PI * rampFraction));
            }

            samples[i] = sample;
        }

        AudioClip clip = AudioClip.Create(
            $"beep_{durationSec * 1000f:F0}ms_{frequencyHz:F0}Hz",
            sampleCount,
            1,          // mono
            sampleRate,
            false       // not streaming
        );
        clip.SetData(samples, 0);
        return clip;
    }
}
