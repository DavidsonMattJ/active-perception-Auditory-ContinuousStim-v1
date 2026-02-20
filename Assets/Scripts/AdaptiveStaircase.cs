using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.FullSerializer;

public class AdaptiveStaircase : MonoBehaviour
{
    /// <summary>
    /// A single adaptive staircase component that manages independent staircase
    /// instances for any number of experimental conditions (e.g., slow/natural walking).
    ///
    /// Usage:
    ///   float nextDelta = staircase.ProcessResponse("slow", wasCorrect);
    ///   float nextDelta = staircase.ProcessResponse("natural", wasCorrect);
    ///
    /// Each condition string creates its own independent staircase on first use.
    /// All instances share the same configuration (step sizes, rules, etc.)
    /// but track their own state (trial count, reversals, threshold).
    /// </summary>

    [System.Serializable]
    public enum StaircaseType
    {
        SimpleUpDown,           // 1-up, 1-down (50% threshold)
        TwoUpOneDown,          // 2-up, 1-down (70.7% threshold)
        ThreeUpOneDown,        // 3-up, 1-down (79.4% threshold)
        OneUpTwoDown,          // 1-up, 2-down (70.7% threshold)
        OneUpThreeDown         // 1-up, 3-down (79.4% threshold)
    }

    // ──────────────────────────────────────────────────────────────────
    //  Inspector-readable parameters (shared across all instances)
    // ──────────────────────────────────────────────────────────────────

    [Header("Staircase Configuration")]
    public StaircaseType staircaseType = StaircaseType.TwoUpOneDown;
    [Tooltip("Starting ISI delta in ms")]
    public float initialDuration;
    [Tooltip("Minimum ISI delta in ms")]
    public float minDuration;
    [Tooltip("Maximum ISI delta in ms")]
    public float maxDuration;

    [Header("Step Sizes (ms)")]
    public float initialStepSize;   // 10 ms step
    public float finalStepSize;      // 1 ms step
    public int reversalsToReduceStep; // After how many reversals to halve step size

    public float currentDelta; // display current.

    // ──────────────────────────────────────────────────────────────────
    //  Per-condition staircase state
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Internal state for a single staircase instance.
    /// </summary>
    private class StaircaseState
    {
        public string conditionLabel;
        public float currentIntensity;
        public float currentStepSize;
        public int trialCount;
        public int reversalCount;
        public int consecutiveCorrect;
        public int consecutiveIncorrect;
        public bool isComplete;

        // History
        public List<float> intensityHistory = new List<float>();
        public List<bool> responseHistory = new List<bool>();
        public List<float> reversalIntensities = new List<float>();
        public List<int> reversalTrials = new List<int>();
        public bool lastDirectionWasUp;
        public bool hasHadFirstReversal;
    }

    // Dictionary of independent staircases, keyed by condition label
    private Dictionary<string, StaircaseState> staircases = new Dictionary<string, StaircaseState>();

    public GameObject Screen;
    MakeAuditoryStimulus makeAuditoryStimulus;
    // establish defauly parameters from other scripts.
    
    void Start()
    {
        makeAuditoryStimulus= Screen.GetComponent<MakeAuditoryStimulus>();

        initialDuration = makeAuditoryStimulus.baseISIMs; //starting ISI in ms
        minDuration = makeAuditoryStimulus.minDeltaMs; // min ISI delta in ms
        maxDuration = makeAuditoryStimulus.maxDeltaMs;
        currentDelta = initialDuration; // to be adapted.
    }
    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Process a response for a given condition. Creates the staircase instance
    /// on first use for that condition.
    /// </summary>
    /// <param name="condition">Condition label (e.g., "slow", "natural")</param>
    /// <param name="correct">Whether the participant's response was correct</param>
    /// <returns>The new intensity (duration delta in seconds) for the next trial</returns>
    public float ProcessResponse(string condition, bool correct)
    {
        StaircaseState s = GetOrCreateStaircase(condition);

        if (s.isComplete)
        {
            Debug.LogWarning($"Staircase [{condition}] is already complete!");
            return s.currentIntensity;
        }

        // Record the response
        s.trialCount++;
        s.responseHistory.Add(correct);
        s.intensityHistory.Add(s.currentIntensity);

        Debug.Log($"[Staircase:{condition}] Trial {s.trialCount}: correct={correct}, intensity={s.currentIntensity:F3}");

        // Update consecutive counters
        if (correct)
        {
            s.consecutiveCorrect++;
            s.consecutiveIncorrect = 0;
        }
        else
        {
            s.consecutiveIncorrect++;
            s.consecutiveCorrect = 0;
        }

        // Determine direction
        bool shouldGoUp = ShouldIncreaseIntensity(s);
        bool shouldGoDown = ShouldDecreaseIntensity(s);

        // Check for reversal before updating intensity
        bool isReversal = CheckForReversal(s, shouldGoUp, shouldGoDown);

        // Update intensity
        if (shouldGoUp)
        {
            s.currentIntensity += s.currentStepSize;
            s.lastDirectionWasUp = true;
            s.consecutiveCorrect = 0;
            s.consecutiveIncorrect = 0;
        }
        else if (shouldGoDown)
        {
            s.currentIntensity -= s.currentStepSize;
            s.lastDirectionWasUp = false;
            s.consecutiveCorrect = 0;
            s.consecutiveIncorrect = 0;
        }

        // Record reversal
        if (isReversal)
        {
            s.reversalCount++;
            s.reversalIntensities.Add(s.currentIntensity);
            s.reversalTrials.Add(s.trialCount);
            s.hasHadFirstReversal = true;

            Debug.Log($"[Staircase:{condition}] Reversal #{s.reversalCount} at trial {s.trialCount}, intensity {s.currentIntensity:F3}");

            // Reduce step size after every N reversals
            if (s.reversalCount > 0 && s.reversalCount % reversalsToReduceStep == 0)
            {
                float oldStep = s.currentStepSize;
                s.currentStepSize = Mathf.Max(finalStepSize, s.currentStepSize * 0.5f);
                Debug.Log($"[Staircase:{condition}] Step size reduced: {oldStep:F3} -> {s.currentStepSize:F3}");
            }
        }

        // Clamp
        s.currentIntensity = Mathf.Clamp(s.currentIntensity, minDuration, maxDuration);

        Debug.Log($"[Staircase:{condition}] New intensity: {s.currentIntensity:F3}, reversals: {s.reversalCount}");

        // display in inspector for debugging;
        currentDelta = s.currentIntensity;
        
        return s.currentIntensity;
    }

    /// <summary>
    /// Get the current intensity for a condition without processing a response.
    /// </summary>
    public float GetCurrentIntensity(string condition)
    {
        StaircaseState s = GetOrCreateStaircase(condition);
        return s.currentIntensity;
    }

    /// <summary>
    /// Get the estimated threshold for a condition (mean of last 6 reversals).
    /// </summary>
    public float GetEstimatedThreshold(string condition)
    {
        StaircaseState s = GetOrCreateStaircase(condition);
        return CalculateThreshold(s);
    }

    /// <summary>
    /// Get the number of trials completed for a condition.
    /// </summary>
    public int GetTrialCount(string condition)
    {
        if (staircases.TryGetValue(condition, out StaircaseState s))
            return s.trialCount;
        return 0;
    }

    /// <summary>
    /// Get the number of reversals for a condition.
    /// </summary>
    public int GetReversalCount(string condition)
    {
        if (staircases.TryGetValue(condition, out StaircaseState s))
            return s.reversalCount;
        return 0;
    }

    /// <summary>
    /// Get the intensity history for a condition (for plotting/analysis).
    /// </summary>
    public List<float> GetIntensityHistory(string condition)
    {
        if (staircases.TryGetValue(condition, out StaircaseState s))
            return new List<float>(s.intensityHistory);
        return new List<float>();
    }

    /// <summary>
    /// Get the response history for a condition.
    /// </summary>
    public List<bool> GetResponseHistory(string condition)
    {
        if (staircases.TryGetValue(condition, out StaircaseState s))
            return new List<bool>(s.responseHistory);
        return new List<bool>();
    }

    /// <summary>
    /// Reset a single condition's staircase.
    /// </summary>
    public void ResetCondition(string condition)
    {
        if (staircases.ContainsKey(condition))
        {
            staircases.Remove(condition);
            Debug.Log($"[Staircase:{condition}] Reset.");
        }
    }

    /// <summary>
    /// Reset all staircases.
    /// </summary>
    public void ResetAll()
    {
        staircases.Clear();
        Debug.Log("All staircases reset.");
    }

    /// <summary>
    /// Print summary statistics for all active conditions.
    /// </summary>
    public void PrintSummary()
    {
        Debug.Log("=== STAIRCASE SUMMARY ===");
        foreach (var kvp in staircases)
        {
            var s = kvp.Value;
            float accuracy = s.responseHistory.Count > 0
                ? s.responseHistory.Count(r => r) / (float)s.responseHistory.Count * 100f
                : 0f;

            Debug.Log($"[{kvp.Key}] Trials: {s.trialCount}, Reversals: {s.reversalCount}, " +
                      $"Final: {s.currentIntensity:F3}, Threshold: {CalculateThreshold(s):F3}, " +
                      $"Accuracy: {accuracy:F1}%");
            Debug.Log($"  Reversal intensities: {string.Join(", ", s.reversalIntensities.Select(r => r.ToString("F3")))}");
        }
    }

    /// <summary>
    /// Returns a list of all active condition labels.
    /// </summary>
    public List<string> GetActiveConditions()
    {
        return new List<string>(staircases.Keys);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────────

    private StaircaseState GetOrCreateStaircase(string condition)
    {
        if (!staircases.TryGetValue(condition, out StaircaseState s))
        {
            s = new StaircaseState
            {
                conditionLabel = condition,
                currentIntensity = initialDuration,
                currentStepSize = initialStepSize,
                trialCount = 0,
                reversalCount = 0,
                consecutiveCorrect = 0,
                consecutiveIncorrect = 0,
                isComplete = false,
                lastDirectionWasUp = false,
                hasHadFirstReversal = false
            };
            staircases[condition] = s;
            Debug.Log($"[Staircase:{condition}] Created new instance (initial={initialDuration:F3}, step={initialStepSize:F3})");
        }
        return s;
    }

    private bool ShouldIncreaseIntensity(StaircaseState s)
    {
        switch (staircaseType)
        {
            case StaircaseType.SimpleUpDown:
                return s.consecutiveIncorrect >= 1;
            case StaircaseType.TwoUpOneDown:
                return s.consecutiveIncorrect >= 1;
            case StaircaseType.ThreeUpOneDown:
                return s.consecutiveIncorrect >= 1;
            case StaircaseType.OneUpTwoDown:
                return s.consecutiveIncorrect >= 2;
            case StaircaseType.OneUpThreeDown:
                return s.consecutiveIncorrect >= 3;
            default:
                return false;
        }
    }

    private bool ShouldDecreaseIntensity(StaircaseState s)
    {
        switch (staircaseType)
        {
            case StaircaseType.SimpleUpDown:
                return s.consecutiveCorrect >= 1;
            case StaircaseType.TwoUpOneDown:
                return s.consecutiveCorrect >= 2;
            case StaircaseType.ThreeUpOneDown:
                return s.consecutiveCorrect >= 3;
            case StaircaseType.OneUpTwoDown:
                return s.consecutiveCorrect >= 1;
            case StaircaseType.OneUpThreeDown:
                return s.consecutiveCorrect >= 1;
            default:
                return false;
        }
    }

    private bool CheckForReversal(StaircaseState s, bool shouldGoUp, bool shouldGoDown)
    {
        if (!s.hasHadFirstReversal)
        {
            // First direction change counts as a reversal
            return shouldGoUp || shouldGoDown;
        }
        else
        {
            // Subsequent reversals: direction must flip
            return (shouldGoUp && !s.lastDirectionWasUp) || (shouldGoDown && s.lastDirectionWasUp);
        }
    }

    private float CalculateThreshold(StaircaseState s)
    {
        if (s.reversalIntensities.Count < 4)
        {
            return s.currentIntensity; // Not enough data
        }

        // Use last 6 reversals or all if fewer than 6
        int reversalsToUse = Mathf.Min(6, s.reversalIntensities.Count);
        int startIndex = s.reversalIntensities.Count - reversalsToUse;

        float sum = 0f;
        for (int i = startIndex; i < s.reversalIntensities.Count; i++)
        {
            sum += s.reversalIntensities[i];
        }

        return sum / reversalsToUse;
    }
}
