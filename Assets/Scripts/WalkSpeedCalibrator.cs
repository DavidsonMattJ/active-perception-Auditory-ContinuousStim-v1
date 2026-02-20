using UnityEngine;
using System.Collections.Generic;
using System; // for Math

public class WalkSpeedCalibrator : MonoBehaviour
{
    [Header("Zone References")]
    public GameObject startZone;
    public GameObject endZone;

    [Header("Calibration Settings")]
    public int requiredLaps = 6;

    [Header("Lap Validation")]
    public float minLapTime = 5f;  // Minimum valid lap time
    public float maxLapTime = 10f; // Maximum valid lap time


    [Header("Results")]
    public float walkDuration = 0f;
    public float normSpeed = 0f; // calculated later, based on distance between zones / walkDuration, else defaults from expParams.
    public float slowSpeed = 0f; // 60% of normSpeed.
    public float currentTimer = 0f;
    public bool timerRunning = false;
    // private bool playerInStartZone;
    // private bool playerInEndZone ;
    
    private List<float> lapTimes = new List<float>();
    public int currentLap = 0;

    [SerializeField] GameObject TextScreen;
    [SerializeField] GameObject TextStartzone;
    [SerializeField] GameObject TextEndzone;
    [SerializeField] GameObject walkingGuide; // to hide during calibration.


    ShowText ShowText;
    CalibrationText CalibrationTextStartzone;
    CalibrationText CalibrationTextEndzone;
    controlWalkingGuide controlWalkingGuide;
    // runExperiment runExperiment;
    runContinuousExperiment runExperiment;
    experimentParameters experimentParameters;
    CalculateStimTimes CalculateStimTimes;

    private enum CalibrationState //enum is a lit of constants, providing more meaningful names.
    {
        WaitingForStart, // system is ready and waitng for the player to enter the start zone.
        TimingToEnd, // player left start zone, timer is running, waiting for endzone.
        TimingToStart, // left endzone, timer running, waiting to reach start zone (opp direction).

        CalibrationComplete // once fixed, walkDuration is passed to experimentParams.
    }
    
    //create a variable that can only hold one of these states.
    private CalibrationState currentState = CalibrationState.WaitingForStart;
    
    void Start()
    {
        //dependencies:
        ShowText = TextScreen.GetComponent<ShowText>(); // on separate HameObject.
        // get handle to both, for simultaneous update:
        CalibrationTextStartzone = TextStartzone.GetComponent<CalibrationText>();
        CalibrationTextEndzone = TextEndzone.GetComponent<CalibrationText>();

        controlWalkingGuide = GetComponent<controlWalkingGuide>();
        runExperiment = GetComponent<runContinuousExperiment>();
        experimentParameters = GetComponent<experimentParameters>();
        CalculateStimTimes = GetComponent<CalculateStimTimes>();

        // current walk speed based on default duration:
        normSpeed = experimentParameters.normSpeed;
        slowSpeed = experimentParameters.slowSpeed; //check, but prob 60% of normal speed.  
        // these speeds will update the distances walked on each trial (fixed duration now).

        //parameters
        walkDuration = 0f; //reset
        //set up walk explanation: 
        walkingGuide.SetActive(false);
        ShowText.UpdateText(ShowText.TextType.WalkingInstructions);
        //start up routines:
        SetupZoneTriggers(); // Ensure zones have the proper triggers set up

        if (!runExperiment.skipWalkCalibration)
        {
            CalibrationTextStartzone.UpdateText(CalibrationText.TextType.Welcome);
            
            CalibrationTextEndzone.UpdateText(CalibrationText.TextType.Welcome);

        } else {
            CalibrationTextStartzone.UpdateText(CalibrationText.TextType.Hide);
            CalibrationTextEndzone.UpdateText(CalibrationText.TextType.Hide);
        }
        // } else {

        Debug.Log("Walk Speed Calibration started. Please walk between zones 6 times.");
    }
    
    void Update()
    {
    // // if not in VR mode, and calibration not complete, skip calibration entirely:
        if (runExperiment.skipWalkCalibration && currentState != CalibrationState.CalibrationComplete ) // ||!runExperiment.playinVR) )
        {
            Debug.Log("Not in VR mode - skipping walk speed calibration");

            walkingGuide.SetActive(true);
            // Set a default walk duration for keyboard mode
            // note that walkDuration is now set the same for both speeds. We adjust distance instead.
            // walkDuration = WalkSpeedCalibrator.distanceBetweenZones / normSpeed; // seconds

            // with default speeds, calculate adjusted distance on slow trials:
            controlWalkingGuide.calculateAdjustedStartEndZones();
            
            //with default speeds, calculate max Targets bySpeed
            // CalculateStimTimes.CalculateStimulusPresentationTimes();

            // calculate adjusted start and end zone positions, to match the duration walked at both speeds.
            // controlWalkingGuide.adjustStartandEndZones();


            // Skip directly to calibration complete state
            currentState = CalibrationState.CalibrationComplete;

            // Set up the walking guide for experiment
            controlWalkingGuide = GetComponent<controlWalkingGuide>();
            // controlWalkingGuide.setGuidetoCentre();

            // Update text to show we're ready        
            ShowText.UpdateText(ShowText.TextType.CalibrationComplete);
            CalibrationTextStartzone.UpdateText(CalibrationText.TextType.Hide);
            CalibrationTextEndzone.UpdateText(CalibrationText.TextType.Hide);
            TextEndzone.SetActive(false); //as back  up
            TextStartzone.SetActive(false); //as back  up
            // Disable this component to save performance
            this.enabled = false;
            return;
        }
        // Update timer if running
        if (timerRunning)
        {
            currentTimer += Time.deltaTime;
        }
    }
    
    void SetupZoneTriggers()
    {
        // Add trigger components if they don't exist
        if (startZone != null)
        {
            var startTrigger = startZone.GetComponent<WalkZoneTrigger>();
            if (startTrigger == null)
            {
                startTrigger = startZone.AddComponent<WalkZoneTrigger>();
            }
            startTrigger.Initialize(this, true);
        }
        
        if (endZone != null)
        {
            var endTrigger = endZone.GetComponent<WalkZoneTrigger>();
            if (endTrigger == null)
            {
                endTrigger = endZone.AddComponent<WalkZoneTrigger>();
            }
            endTrigger.Initialize(this, false);
        }
    }
    
    public void OnPlayerEnterStartZone()
    {
        // playerInStartZone = true;
        
        // show calibration state (laps remaining).
        CalibrationTextStartzone.UpdateText(CalibrationText.TextType.CalibState);
        CalibrationTextEndzone.UpdateText(CalibrationText.TextType.CalibState);

        if (currentState == CalibrationState.TimingToStart)
        {
            // Complete lap back to start
            StopTimer();
            RecordLapTime();
            if (currentState != CalibrationState.CalibrationComplete) // don't restart if we are finished.
            {
                currentState = CalibrationState.WaitingForStart;
            }
        }
    }
    
    public void OnPlayerExitStartZone()
    {
        // playerInStartZone = false;
        
        CalibrationTextStartzone.UpdateText(CalibrationText.TextType.CalibState);
        CalibrationTextEndzone.UpdateText(CalibrationText.TextType.CalibState);

        if (currentState == CalibrationState.WaitingForStart)
        {
            // Start timing to end zone
            StartTimer();
            currentState = CalibrationState.TimingToEnd;
        }
    }
    
    public void OnPlayerEnterEndZone()
    {
        
        CalibrationTextStartzone.UpdateText(CalibrationText.TextType.CalibState);
        CalibrationTextEndzone.UpdateText(CalibrationText.TextType.CalibState);

        // playerInEndZone = true;
        
        if (currentState == CalibrationState.TimingToEnd)
        {
            // Complete lap to end
            StopTimer();
            RecordLapTime();
            if (currentState != CalibrationState.CalibrationComplete) // don't restart if we are finished.
            {
                currentState = CalibrationState.TimingToStart;
            }
        }
    }
    
    public void OnPlayerExitEndZone()
    {
        // playerInEndZone = false;

        CalibrationTextStartzone.UpdateText(CalibrationText.TextType.CalibState);
        CalibrationTextEndzone.UpdateText(CalibrationText.TextType.CalibState);
        
        if (currentState == CalibrationState.TimingToStart)
        {
            // Start timing back to start zone
            StartTimer();
        }
    }
    
    void StartTimer()
    {
        currentTimer = 0f;
        timerRunning = true;
    }
    
    void StopTimer()
    {
        timerRunning = false;
    }
    void RecordLapTime()
    {
        if (currentTimer > 0f)
        {
            // Validate lap time is within sensible bounds
            if (currentTimer < minLapTime || currentTimer > maxLapTime)
            {
                Debug.Log($"Lap {currentLap + 1}: {currentTimer:F2} seconds - INVALID (outside {minLapTime}-{maxLapTime}s range). Please try again.");
                return; // Don't record this lap
            }

            lapTimes.Add(currentTimer);
            currentLap++;

            Debug.Log($"Lap {currentLap}: {currentTimer:F2} seconds - VALID");

            // Check if we have enough laps
            if (lapTimes.Count >= requiredLaps)
            {

                CalculateAverageWalkDuration();

                SetGuideForExperiment(); //sets state to CalibrationComplete

            }
            else
            {
                Debug.Log($"Progress: {lapTimes.Count}/{requiredLaps} laps completed");
               

            }
        }
    }

    void CalculateAverageWalkDuration()
    {
        // Only use the last 4 laps (omit first 2)
        int lapsToUse = 4;
        int startIndex = lapTimes.Count - lapsToUse;

        float total = 0f;
        for (int i = startIndex; i < lapTimes.Count; i++)
        {
            total += lapTimes[i];
        }

        walkDuration = (float)Math.Round(total / lapsToUse,2);

        Debug.Log($"Calibration Complete! Used laps {startIndex + 1}-{lapTimes.Count} for average.");
        Debug.Log($"Average walk duration: {walkDuration:F2} seconds (based on last {lapsToUse} laps)");
        Debug.Log("You can now use this walkDuration value for your walk speed calculations.");

        // Update experiment parameters with calibrated speeds
        
        normSpeed = experimentParameters.distanceBetweenZones / walkDuration; // m/s
                                                                              // clamp if too fast:
        if (normSpeed > experimentParameters.defaultMaxSpeed)
        {
            normSpeed = experimentParameters.defaultMaxSpeed;
            Debug.Log("Calculated normal speed exceeds default max speed. Clamping to default max speed: " + normSpeed + " m/s");
            Debug.Log("Reverted to default duration: " + experimentParameters.walkDuration + " seconds");
            walkDuration = experimentParameters.walkDuration;
        } else
        {
             Debug.Log("Calculated normal speed: " + normSpeed + " m/s");
            
        }

        slowSpeed = normSpeed * experimentParameters.slowSpdPcnt; // e.g. 80% of normal speed
        // update params for downstream use:
        Debug.Log("Calculated slow speed: " + slowSpeed + " m/s");

        experimentParameters.normSpeed = normSpeed;
        experimentParameters.slowSpeed = slowSpeed;


        //
        
        // Reset for potential recalibration
        lapTimes.Clear();
        currentLap = 0;
        currentState = CalibrationState.CalibrationComplete;
        
    }

    void SetGuideForExperiment()
    {
        // show walking guide again
        walkingGuide.SetActive(true);
        //replace walking guide to centre.
        controlWalkingGuide.setGuidetoCentre();

        controlWalkingGuide.calculateAdjustedStartEndZones();
        //with new durations, calculate max Targets bySpeed
        // experimentParameters.CalculateMaxTargetsBySpeed();
        CalculateStimTimes.CalculateStimulusPresentationTimes();

        //update text shown to pp
        Debug.Log("Walk speed calibration complete. You are now ready to start the practice trials. " +
         "Preparing for practice trials...");
        ShowText.UpdateText(ShowText.TextType.CalibrationComplete);
        CalibrationTextStartzone.UpdateText(CalibrationText.TextType.Hide);
        CalibrationTextEndzone.UpdateText(CalibrationText.TextType.Hide);
        TextEndzone.SetActive(false); //as back  up
        TextStartzone.SetActive(false); //as back  up
        //to prepar to recalibrate walking speed, set enum too:
        //currentState = CalibrationState.WaitingForStart;
    }

    public bool isCalibrationComplete()
    {
        return currentState == CalibrationState.CalibrationComplete; //return true if so.
    }
}

// Helper component for zone triggers
public class WalkZoneTrigger : MonoBehaviour
{
    private WalkSpeedCalibrator calibrator;
    private bool isStartZone;
    
    public void Initialize(WalkSpeedCalibrator cal, bool startZone)
    {
        calibrator = cal;
        isStartZone = startZone;
        
        // Ensure collider is set as trigger
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        
        if (isStartZone)
        {
            calibrator.OnPlayerEnterStartZone();
        }
        else
        {
            calibrator.OnPlayerEnterEndZone();
        }
        
    }
    
    void OnTriggerExit(Collider other)
    {
        
        if (isStartZone)
        {
            calibrator.OnPlayerExitStartZone();
        }
        else
        {
            calibrator.OnPlayerExitEndZone();
        }
        
    }
}