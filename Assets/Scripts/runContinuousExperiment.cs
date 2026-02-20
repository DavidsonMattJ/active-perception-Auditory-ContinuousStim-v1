using UnityEngine;
using System.Collections;

public class runContinuousExperiment : MonoBehaviour
{
    /// <summary>
    /// Orchestrator for the continuous auditory tracking task.
    ///
    /// Trial flow:
    ///   1. Both triggers pressed  → start trial
    ///   2. Standard beep sequence plays (~0.5s at base ISI)
    ///   3. Walking guide starts moving
    ///   4. Random-walk beep train runs for walkDuration seconds
    ///   5. Trial ends → data flushed → inter-trial instructions shown
    ///
    /// Response:
    ///   Right controller roll (read by ContinuousControllerInput)
    ///   logged at 60 Hz alongside currentISIMs by ContinuousRecordData.
    ///   No discrete 2AFC, no staircase.
    /// </summary>

    // ──────────────────────────────────────────────────────────────────
    //  Inspector fields
    // ──────────────────────────────────────────────────────────────────

    [Header("Session")]
    public bool playinVR;
    public string participant;
    public bool skipWalkCalibration;

    [Header("Scene references")]
    [Tooltip("ScriptHolder's StimulusScreen child (holds ContinuousAuditoryStimulus + AudioSource).")]
    public GameObject stimulusScreen;
    [SerializeField] private GameObject textScreen;
    [SerializeField] private GameObject textFeedback;
    [Tooltip("TrackingResponseUI GameObject (child of Canvas). Shown during stationary practice trials only.")]
    [SerializeField] private TrackingResponseUI trackingResponseUI;

    [Header("State (read-only in inspector)")]
    public int trialCount;
    public float trialTime;
    public float thisTrialDuration;
    public bool trialinProgress;
    public int currentBlockID;
    public int currentBlockType;

    // ──────────────────────────────────────────────────────────────────
    //  Private component references
    // ──────────────────────────────────────────────────────────────────

    private CollectPlayerInput playerInput;
    private experimentParameters expParams;
    private controlWalkingGuide walkingGuide;
    private WalkSpeedCalibrator walkCalibrator;
    private ShowText showText;
    private FeedbackText feedbackText;
    private ContinuousAuditoryStimulus continuousStim;
    private ContinuousRecordData recordData;

    private bool setupDone;
    private bool isStationary;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Inject references into TrackingResponseUI before its Start() runs
        if (trackingResponseUI != null)
        {
            trackingResponseUI.controllerInput = GetComponent<ContinuousControllerInput>();
            trackingResponseUI.playinVR = playinVR;
        }
    }

    void Start()
    {
        playerInput = GetComponent<CollectPlayerInput>();
        expParams = GetComponent<experimentParameters>();
        walkingGuide = GetComponent<controlWalkingGuide>();
        walkCalibrator = GetComponent<WalkSpeedCalibrator>();
        recordData = GetComponent<ContinuousRecordData>();

        showText = textScreen.GetComponent<ShowText>();
        feedbackText = textFeedback.GetComponent<FeedbackText>();
        continuousStim = stimulusScreen.GetComponent<ContinuousAuditoryStimulus>();

        TogglePlayers();

        trialCount = 0;
        trialinProgress = false;
        trialTime = 0f;
        setupDone = true;

        Debug.Log("[runContinuousExperiment] Initialized.");
    }

    void Update()
    {
        // Show welcome text once UI is ready
        if (setupDone && showText.isInitialized)
        {
            if (skipWalkCalibration)
                showText.UpdateText(ShowText.TextType.CalibrationComplete);
            else
                showText.UpdateText(ShowText.TextType.Welcome);

            setupDone = false;
        }

        // ── Trial start: both triggers pressed ──────────────────────
        if (!trialinProgress && playerInput.botharePressed)
        {
            if (playinVR)
            {
                if (walkCalibrator.isCalibrationComplete())
                {
                    Debug.Log("[runContinuousExperiment] Starting trial (VR)");
                    StartTrial();
                }
                else
                {
                    Debug.Log("[runContinuousExperiment] Awaiting walk calibration.");
                    walkingGuide.setGuidetoHidden();
                }
            }
            else
            {
                Debug.Log("[runContinuousExperiment] Starting trial (non-VR)");
                StartTrial();
            }
        }

        // ── Trial timer ─────────────────────────────────────────────
        if (trialinProgress)
        {
            trialTime += Time.deltaTime;

            if (trialTime > thisTrialDuration)
            {
                TrialPackDown();
                trialCount++;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Trial start
    // ──────────────────────────────────────────────────────────────────

    void StartTrial()
    {
        walkingGuide.updateScreenHeight();
        showText.UpdateText(ShowText.TextType.Hide);
        feedbackText.UpdateText(FeedbackText.TextType.Hide);

        trialinProgress = true;
        trialTime = 0f;

        // Pull trial parameters from experimentParameters
        currentBlockID = expParams.blockTypeArray[trialCount, 0];
        currentBlockType = expParams.blockTypeArray[trialCount, 2];
        isStationary = (currentBlockType == 0);

        thisTrialDuration = expParams.walkDuration;

        // Populate trialD for any downstream use
        expParams.trialD.trialNumber = trialCount;
        expParams.trialD.blockID = currentBlockID;
        expParams.trialD.trialID = expParams.blockTypeArray[trialCount, 1];
        expParams.trialD.isStationary = isStationary;
        expParams.trialD.blockType = currentBlockType;

        // Begin frame-by-frame recording
        recordData.recordPhase = ContinuousRecordData.Phase.CollectResponse;

        // Show tracking UI on all trials
        if (trackingResponseUI != null)
            trackingResponseUI.Show();

        Debug.Log($"[runContinuousExperiment] Trial {trialCount + 1}: " +
                  $"blockType={currentBlockType}, duration={thisTrialDuration:F1}s, " +
                  $"showTrackingUI={isStationary}");

        StartCoroutine(StandardThenContinuousSequence());
    }

    // ──────────────────────────────────────────────────────────────────
    //  Trial sequence coroutine
    // ──────────────────────────────────────────────────────────────────

    IEnumerator StandardThenContinuousSequence()
    {
        // 1. Standard beep sequence (steady, before walking)
        yield return StartCoroutine(continuousStim.PlayStandardSequence());

        // 2. Start walking guide (if not stationary)
        if (!isStationary)
            walkingGuide.moveGuideatWalkSpeed();

        // 3. Launch random-walk beep train (runs until trial ends)
        yield return StartCoroutine(
            continuousStim.RunContinuousBeepTrain(thisTrialDuration, this));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Trial end
    // ──────────────────────────────────────────────────────────────────

    void TrialPackDown()
    {
        Debug.Log($"[runContinuousExperiment] End of trial {trialCount + 1}");

        // Stop audio and flush data
        continuousStim.StopAllAudio();
        recordData.recordPhase = ContinuousRecordData.Phase.Stop;

        // Reposition walking guide for next trial
        walkingGuide.SetGuideForNextTrial();

        // Always hide tracking UI at trial end
        if (trackingResponseUI != null)
            trackingResponseUI.Hide();

        trialinProgress = false;
        trialTime = 0f;

        // Show inter-trial instructions
        showText.UpdateText(ShowText.TextType.TrialStart);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Utilities
    // ──────────────────────────────────────────────────────────────────

    void TogglePlayers()
    {
        if (playinVR)
        {
            GameObject.Find("VR_Player").SetActive(true);
            GameObject.Find("Kb_Player").SetActive(false);
        }
        else
        {
            GameObject.Find("VR_Player").SetActive(false);
            GameObject.Find("Kb_Player").SetActive(true);
        }
    }
}
