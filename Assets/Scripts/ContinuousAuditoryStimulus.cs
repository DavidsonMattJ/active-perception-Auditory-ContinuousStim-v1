using System.Collections;
using UnityEngine;

public class ContinuousAuditoryStimulus : MonoBehaviour
{
    /// <summary>
    /// Generates a continuous beep train whose ISI drifts via a bounded random walk.
    ///
    /// Each beep cycle the ISI is updated by a random step in [-maxStepMs, +maxStepMs],
    /// then clamped to [minISIMs, maxISIMs]. There are no discrete "change" phases —
    /// the rate varies continuously throughout the trial.
    ///
    /// Public API:
    ///   PlayStandardSequence()           → coroutine: steady beeps before walking
    ///   RunContinuousBeepTrain()         → coroutine: random-walk beep train during walking
    ///   StopBeepTrain()                  → stops the train
    ///   currentISIMs                     → current ISI, sampled each frame by data logger
    /// </summary>

    // ──────────────────────────────────────────────────────────────────
    //  Inspector parameters
    // ──────────────────────────────────────────────────────────────────

    [Header("Beep")]
    [SerializeField] private float baseFrequencyHz = 500f;
    [SerializeField] private float beepDurationMs = 50f;
    [SerializeField, Range(0f, 1f)] private float toneAmplitude = 0.8f;
    [Tooltip("Cosine ramp duration in ms to prevent click artifacts")]
    [SerializeField] private float rampDurationMs = 15f;

    [Header("Random Walk ISI")]
    public float baseISIMs = 100f;
    [Tooltip("Hard lower boundary for ISI (ms). Must be > beep duration.")]
    public float minISIMs = 50f;
    [Tooltip("Hard upper boundary for ISI (ms).")]
    public float maxISIMs = 200f;

    [Tooltip("Maximum velocity (ms/beep). Sets the fastest the ISI can drift per beep.")]
    public float maxVelocityMs = 5f;
    [Tooltip("How much the velocity can change per beep (ms/beep²). " +
             "Small = smooth persistent drift. Large = more frequent direction changes. " +
             "Good range: 0.5–3. At 1: ~5–10 beeps to reverse direction.")]
    public float velocityJitterMs = 1f;

    // velocityitterMs = 0.5 ; very sticky, long smooth runs rare reversals (~10-20 beeps per direction)
    // velocityitterMs = 1 ; moderate,  ~5-10 beeps per direction
    // velocityitterMs = 2.5 ; frequent turns ~3-5 beeps
    // velocityitterMs = 5 ; approaching random walk / direction change possible per beep.

    [Header("Standard Presentation (pre-walk)")]
    [SerializeField] private int standardRepetitions = 5;

    // ──────────────────────────────────────────────────────────────────
    //  Public state (read each frame by ContinuousRecordData)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Current ISI in ms. Updated every beep cycle. Visible in Inspector.</summary>
    public float currentISIMs;

    /// <summary>Current drift velocity in ms/beep. Positive = ISI increasing (slowing down).
    /// Visible in Inspector for monitoring.</summary>
    public float currentVelocityMs;

    // ──────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────

    private AudioSource audioSource;
    private AudioClip beepClip;
    private const int sampleRate = 44100;
    private bool trainRunning;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D audio

        // Pre-generate the beep clip (all beeps are identical)
        float beepSec = beepDurationMs / 1000f;
        beepClip = GenerateToneClip(beepSec, baseFrequencyHz, toneAmplitude);

        currentISIMs = baseISIMs;
        trainRunning = false;

        Debug.Log($"[ContinuousAuditoryStimulus] Init: freq={baseFrequencyHz}Hz, " +
                  $"beep={beepDurationMs}ms, baseISI={baseISIMs}ms, " +
                  $"range=[{minISIMs}, {maxISIMs}]ms, " +
                  $"maxVel={maxVelocityMs}ms/beep, jitter={velocityJitterMs}ms/beep²");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public coroutines
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a steady beep sequence at baseISIMs before walking begins.
    /// </summary>
    public IEnumerator PlayStandardSequence()
    {
        float isiSec = baseISIMs / 1000f;
        currentISIMs = baseISIMs;

        for (int i = 0; i < standardRepetitions; i++)
        {
            audioSource.PlayOneShot(beepClip);
            yield return new WaitForSecondsRealtime(isiSec);
        }

        Debug.Log($"[ContinuousAuditoryStimulus] Standard sequence done: " +
                  $"{standardRepetitions} beeps at {baseISIMs}ms ISI");
    }

    /// <summary>Returns total duration of the standard sequence in seconds.</summary>
    public float GetStandardSequenceDuration()
    {
        return (baseISIMs / 1000f) * standardRepetitions;
    }

    /// <summary>
    /// Main continuous beep train. ISI drifts via bounded random walk each beep.
    /// Runs until trialTime >= trialDuration or StopBeepTrain() is called.
    /// </summary>
    public IEnumerator RunContinuousBeepTrain(float trialDuration, runContinuousExperiment runner)
    {
        trainRunning = true;
        currentISIMs = baseISIMs;

        // Start with a small random velocity so the walk doesn't always begin flat
        currentVelocityMs = Random.Range(-velocityJitterMs, velocityJitterMs);

        // Minimum ISI must exceed beep duration to prevent overlap
        float absoluteMinISI = beepDurationMs + 10f;
        float effectiveMin = Mathf.Max(minISIMs, absoluteMinISI);

        Debug.Log($"[ContinuousAuditoryStimulus] Starting autocorrelated beep train. " +
                  $"Duration={trialDuration:F1}s, ISI range=[{effectiveMin}, {maxISIMs}]ms, " +
                  $"maxVel={maxVelocityMs}, jitter={velocityJitterMs}");

        while (trainRunning && runner.trialTime < trialDuration)
        {
            // Play beep
            audioSource.PlayOneShot(beepClip);

            // Wait for current ISI
            yield return new WaitForSecondsRealtime(currentISIMs / 1000f);

            // ── Autocorrelated velocity update ────────────────────────
            // Nudge the velocity by a small random jitter each beep.
            // This preserves direction most of the time (persistence/stickiness)
            // while still allowing gradual direction changes.
            currentVelocityMs += Random.Range(-velocityJitterMs, velocityJitterMs);

            // Clamp velocity so ISI doesn't accelerate without bound
            currentVelocityMs = Mathf.Clamp(currentVelocityMs, -maxVelocityMs, maxVelocityMs);

            // Soft boundary reflection: if ISI is near a limit, nudge velocity
            // back toward centre so the walk doesn't stick to the wall.
            if (currentISIMs <= effectiveMin)
                currentVelocityMs = Mathf.Abs(currentVelocityMs);   // force positive (increasing ISI)
            else if (currentISIMs >= maxISIMs)
                currentVelocityMs = -Mathf.Abs(currentVelocityMs);  // force negative (decreasing ISI)

            // Apply velocity and hard-clamp as a safety net
            currentISIMs = Mathf.Clamp(currentISIMs + currentVelocityMs, effectiveMin, maxISIMs);
        }

        trainRunning = false;
        Debug.Log($"[ContinuousAuditoryStimulus] Beep train ended at ISI={currentISIMs:F1}ms");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public control methods
    // ──────────────────────────────────────────────────────────────────

    public void StopBeepTrain()
    {
        trainRunning = false;
        audioSource.Stop();
    }

    public void StopAllAudio()
    {
        StopBeepTrain();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tone generation — cosine-ramped sine wave
    // ──────────────────────────────────────────────────────────────────

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
            1,
            sampleRate,
            false
        );
        clip.SetData(samples, 0);
        return clip;
    }
}
