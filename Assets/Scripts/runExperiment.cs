using UnityEngine;
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements.Experimental;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Tweenables.Primitives;
using Unity.VisualScripting;
using TMPro;
using JetBrains.Annotations;
using System.Collections;

public class runExperiment : MonoBehaviour
{
    // This is the launch script for the experiment, useful for toggling certain input states. 

    //Navon v1  -UTS 


    [Header("User Input")]
    public bool playinVR;
    public string participant;
    public bool skipWalkCalibration;


    [Header("Experiment State")]

    public string responseMapping = "L:Faster R:Slower"; // show for experimenter (default, randomised at start)
    public int trialCount;
    public float trialTime;
    public float thisTrialDuration;
    public bool trialinProgress;
    [SerializeField] public int responseMap; // +1: L=Faster R=Slower; -1: L=Slower R=Faster


    [HideInInspector]
    public int detectIndex, targState, blockType; //


    [HideInInspector]
    public bool isStationary, collectTrialSummary, collectEventSummary, hasResponded;
    
    bool SetUpSession;

    //todo
    //public bool forceheightCalibration;
    //public bool forceEyecalibration;
    //public bool recordEEG;
    //public bool isEyetracked;


    CollectPlayerInput playerInput;
    experimentParameters expParams;
    controlWalkingGuide controlWalkingGuide;
    WalkSpeedCalibrator walkCalibrator;
    ShowText ShowText;
    FeedbackText FeedbackText;
    targetAppearance targetAppearance;
    RecordData RecordData;
    AdaptiveStaircase adaptiveStaircase;
    
    MakeAuditoryStimulus makeAuditoryStimulus;

    //use  serialize field to require drag-drop in inspector. less expensive than GameObject.Find() .
    [SerializeField] GameObject TextScreen;
    [SerializeField] GameObject TextFeedback;
    [SerializeField] GameObject StimulusScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {


        adaptiveStaircase = GetComponent<AdaptiveStaircase>();
        playerInput = GetComponent<CollectPlayerInput>();
        expParams = GetComponent<experimentParameters>();
        controlWalkingGuide = GetComponent<controlWalkingGuide>();
        walkCalibrator = GetComponent<WalkSpeedCalibrator>();
        RecordData = GetComponent<RecordData>();

        ShowText = TextScreen.GetComponent<ShowText>();
        FeedbackText = TextFeedback.GetComponent<FeedbackText>();

        targetAppearance = StimulusScreen.GetComponent<targetAppearance>();
        makeAuditoryStimulus = StimulusScreen.GetComponent<MakeAuditoryStimulus>();
        // hide player camera if not in VR (useful for debugging).
        togglePlayers();

        // Randomise response mapping (L/R → faster/slower)
        assignResponses();

        trialCount = 0;
        trialinProgress = false;

        trialTime = 0f;
        collectEventSummary = false; // send info after each target to csv file.

        hasResponded = false;

        SetUpSession = true;

    }

    // Update is called once per frame
    void Update()
    {
        if (SetUpSession && ShowText.isInitialized)
        {
            if (skipWalkCalibration)
            {
                // show welcome 
                ShowText.UpdateText(ShowText.TextType.CalibrationComplete);                
            }
            else
            {
                // show welcome 
                ShowText.UpdateText(ShowText.TextType.Welcome);
            }
            SetUpSession = false;
        }


        //pseudo code: 
        // listen for trial start (input)/
        // if input. 1) start the walking guide movement
        //           2) start the within trial co-routine
        //           3) start the data recording.

        if (!trialinProgress && playerInput.botharePressed)
        {

            // if we have not yet calibrated walk speed, simply move the wlaking guide to start loc:
            if (playinVR)
            {
                if (walkCalibrator.isCalibrationComplete())
                {
                    //start trial sequence, including:
                    // movement, co-routine, datarecording.

                    Debug.Log("Starting Trial in VR mode");
                    startTrial();
                }
                else
                {
                    Debug.Log("button pressed but walk calibration still in progress");
                    // lets hide the walking guide temporarily. 
                    controlWalkingGuide.setGuidetoHidden();
                }
            }
            else // not in VR, skip calibration:
            {
                // Non-VR mode: skip calibration check and start trial directly
                Debug.Log("Starting Trial (Non-VR mode)");
                startTrial();
            }

        }

        // increment trial time.
        if (trialinProgress)
        {
            trialTime += Time.deltaTime; // increment timer.

            if (trialTime > thisTrialDuration)
            {
                trialPackDown(); // includes trial incrementer
                trialCount++;
            }

            // Note: player input is now checked inside the beep train coroutine
            // (MakeAuditoryStimulus.RunBeepTrain), not here. The beep train calls
            // RecordChangeEvent(), RecordNoResponse(), and RecordFalseAlarm() directly.
        }

    } //end Update()

    
        
        
    

    void togglePlayers()
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
    // ──────────────────────────────────────────────────────────────────
    //  Public methods called by MakeAuditoryStimulus.RunBeepTrain()
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines whether a left or right button press maps to "responded faster"
    /// based on the randomised responseMap. Called by the beep train coroutine.
    /// </summary>
    public bool DeriveFasterResponse(bool leftPressed)
    {
        // responseMap == 1: L=Faster, R=Slower → left press = faster
        // responseMap == -1: L=Slower, R=Faster → left press = slower (i.e. not faster)
        return (responseMap == 1) ? leftPressed : !leftPressed;
    }

    /// <summary>
    /// Called by the beep train coroutine when the participant responds during a
    /// changed-ISI phase. Scores the direction judgement, records to CSV, and
    /// updates the staircase.
    /// </summary>
    /// <param name="evt">Immutable snapshot of the ISI change event</param>
    /// <param name="respondedFaster">Did the participant indicate "faster"?</param>
    public void RecordChangeEvent(experimentParameters.StimulusEvent evt, bool respondedFaster)
    {
        bool isCorrect = (respondedFaster == evt.isFaster);

        expParams.trialD.targCorrect = isCorrect ? 1 : 0;
        expParams.trialD.targResponse = respondedFaster ? 1f : 0f; // 1=faster, 0=slower
        expParams.trialD.clickOnsetTime = trialTime;

        // Log the immutable event to CSV
        RecordData.extractEventSummary(evt, isFalseAlarm: false);

        string dirLabel = respondedFaster ? "Faster" : "Slower";
        string actualLabel = evt.isFaster ? "Faster" : "Slower";
        Debug.Log($"[BeepTrain] {(isCorrect ? "CORRECT" : "INCORRECT")} — responded {dirLabel}, was {actualLabel}");

        // Update staircase (skip during standing-still practice trials)
        if (trialCount >= expParams.nstandingStilltrials)
        {
            string condition = GetConditionLabel(expParams.trialD.blockType);

            if (condition != null)
            {
                float nextDelta = adaptiveStaircase.ProcessResponse(condition, isCorrect);
                makeAuditoryStimulus.SetDelta(nextDelta);

                Debug.Log($"[Staircase:{condition}] {(isCorrect ? "✓" : "✗")} → Next delta: {nextDelta:F1}ms");
            }
        }
        else
        {
            // Practice trials: provide visual feedback, no staircase update
            if (isCorrect)
            {
                FeedbackText.UpdateText(FeedbackText.TextType.Correct);
                Invoke(nameof(HideFeedbackText), 0.2f);
            }
            else
            {
                FeedbackText.UpdateText(FeedbackText.TextType.Incorrect);
                Invoke(nameof(HideFeedbackText), 0.2f);
            }
        }
    }

    /// <summary>
    /// Called by the beep train coroutine when no response was made within the
    /// detection window (miss / timeout). Always counts as incorrect for staircase.
    /// </summary>
    public void RecordNoResponse(experimentParameters.StimulusEvent evt)
    {
        expParams.trialD.targCorrect = 0;
        expParams.trialD.targResponse = -1f; // sentinel: no response
        expParams.trialD.clickOnsetTime = -1f; // no click

        RecordData.extractEventSummary(evt, isFalseAlarm: false);

        Debug.Log($"[BeepTrain] MISS (no response) — change was {(evt.isFaster ? "Faster" : "Slower")}");

        // Update staircase (always incorrect)
        if (trialCount >= expParams.nstandingStilltrials)
        {
            string condition = GetConditionLabel(expParams.trialD.blockType);

            if (condition != null)
            {
                float nextDelta = adaptiveStaircase.ProcessResponse(condition, false);
                makeAuditoryStimulus.SetDelta(nextDelta);

                Debug.Log($"[Staircase:{condition}] ✗ (no response) → Next delta: {nextDelta:F1}ms");
            }
        }
        else
        {
            // Practice trials: show feedback
            FeedbackText.UpdateText(FeedbackText.TextType.Incorrect);
            Invoke(nameof(HideFeedbackText), 0.2f);
        }
    }

    /// <summary>
    /// Called by the beep train coroutine when the participant presses a button
    /// during the standard (steady) phase. Logged but does NOT update the staircase.
    /// </summary>
    /// <param name="respondedFaster">Did the participant indicate "faster"?</param>
    public void RecordFalseAlarm(bool respondedFaster)
    {
        // Create a minimal event for logging
        var faEvt = new experimentParameters.StimulusEvent(
            baseFrequencyHz: makeAuditoryStimulus.trainState.baseFrequencyHz,
            baseISIMs: makeAuditoryStimulus.trainState.baseISIMs,
            deltaMs: 0f,
            isFaster: false,
            changeOnsetTime: trialTime,
            changeIndex: -1 // sentinel: false alarm, no actual change
        );

        expParams.trialD.targCorrect = 0;
        expParams.trialD.targResponse = respondedFaster ? 1f : 0f;
        expParams.trialD.clickOnsetTime = trialTime;

        RecordData.extractEventSummary(faEvt, isFalseAlarm: true);

        Debug.Log($"[BeepTrain] FALSE ALARM at t={trialTime:F3}s — responded {(respondedFaster ? "Faster" : "Slower")}");
    }

    private void HideFeedbackText()
    {
        FeedbackText.UpdateText(FeedbackText.TextType.Hide);
    }

    /// <summary>
    /// Randomises the L/R → faster/slower response mapping per participant.
    /// </summary>
    void assignResponses()
    {
        bool switchMapping = UnityEngine.Random.Range(0f, 1f) < 0.5f;

        if (switchMapping)
        {
            responseMap = -1;
            responseMapping = "L:Slower R:Faster";
        }
        else
        {
            responseMap = 1;
            responseMapping = "L:Faster R:Slower";
        }

        Debug.Log($"Response mapping assigned: {responseMapping} (responseMap={responseMap})");
    }

    void startTrial()
    {
        // This method handles the trial sequence.
        // First play the standard tone sequence (~1s), then start walking + comparisons.

        //recalibrate screen height to participants HMD
        controlWalkingGuide.updateScreenHeight();
        //remove text
        ShowText.UpdateText(ShowText.TextType.Hide);
        FeedbackText.UpdateText(FeedbackText.TextType.Hide);

        //establish trial parameters:
        if (expParams.maxTargsbySpeed == null)
        {
            // expParams.CalculateMaxTargetsBySpeed();
        }

        trialinProgress = true; // for coroutine (handled in targetAppearance.cs).
        ShowText.UpdateText(ShowText.TextType.Hide);
        trialTime = 0;
        targState = 0; //target is hidden.

        //Establish (this trial) specific parameters:
        blockType = expParams.blockTypeArray[trialCount, 2]; //third column [0,1,2].

        thisTrialDuration = expParams.walkDuration; // all trials the same duration now, distance varies instead.

        //query if stationary (restricts movement guide)
        isStationary = blockType == 0;

        //populate public trialD structure for extraction in recordData.cs
        expParams.trialD.trialNumber = trialCount;
        expParams.trialD.blockID = expParams.blockTypeArray[trialCount, 0];
        expParams.trialD.trialID = expParams.blockTypeArray[trialCount, 1]; // count within block
        expParams.trialD.isStationary = isStationary;
        expParams.trialD.blockType = blockType; // 0,1,2

        //updated phases for flow managers:
        RecordData.recordPhase = RecordData.phase.collectResponse;

        //start coroutine to control target onset and target behaviour:
        print("Starting Trial " + (trialCount + 1) + " of " + expParams.nTrialsperBlock);

        // Play standard tone sequence first (~1s), then start walking + comparisons
        StartCoroutine(StandardThenWalkSequence());
    }

    /// <summary>
    /// Coroutine: plays the standard tone sequence, then starts the walking guide
    /// and comparison stimulus sequence.
    /// </summary>
    IEnumerator StandardThenWalkSequence()
    {
        // 1. Play standard tone sequence (3x 75ms tones with 200ms gaps ≈ 825ms)
        yield return StartCoroutine(makeAuditoryStimulus.PlayStandardSequence());

        // 2. Now start movement guide (if not stationary)
        if (!isStationary)
        {
            controlWalkingGuide.moveGuideatWalkSpeed();
        }

        // 3. Start the comparison stimulus coroutine
        targetAppearance.startSequence();
    }

    void trialPackDown()
    {
        // This method handles the end of a trial, including data recording and cleanup.
        Debug.Log("End of Trial " + (trialCount + 1));

        // For safety
        RecordData.recordPhase = RecordData.phase.stop;
        //determine next start position for walking guide.

        controlWalkingGuide.SetGuideForNextTrial(); //uses current trialcount +1 to determine next position.

        // Reset trial state
        trialinProgress = false;
        trialTime = 0f;

        // Stop any playing audio
        makeAuditoryStimulus.StopAllAudio();


        // Update text screen to show next steps or end of experiment
        ShowText.UpdateText(ShowText.TextType.TrialStart); //using the previous trial count to show next trial info.


    }

    /// <summary>
    /// Maps a blockType integer to a condition label for the adaptive staircase.
    /// Returns null for stationary trials (blockType 0) which don't use the staircase.
    /// Add new entries here if new walking conditions are added.
    /// </summary>
    string GetConditionLabel(int blockType)
    {
        switch (blockType)
        {
            case 1: return "slow";
            case 2: return "natural";
            default: return null; // stationary — no staircase
        }
    }




}
