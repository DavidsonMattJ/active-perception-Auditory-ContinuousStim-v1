using System.Collections;
using UnityEngine;

public class targetAppearance : MonoBehaviour
{
    /// <summary>
    /// Manages the beep train lifecycle during each walking trial.
    ///
    /// Starts the continuous beep train coroutine on MakeAuditoryStimulus
    /// when the trial begins, and stops it when the trial ends.
    /// All ISI change detection, response handling, and staircase updates
    /// are managed internally by the beep train coroutine.
    ///
    /// Main method called from runExperiment.StandardThenWalkSequence().
    /// </summary>

    runExperiment runExperiment;
    MakeAuditoryStimulus makeAuditoryStimulus;

    [SerializeField]
    GameObject scriptHolder;

    private Coroutine trialCoroutine;

    private void Start()
    {
        runExperiment = scriptHolder.GetComponent<runExperiment>();
        makeAuditoryStimulus = GetComponent<MakeAuditoryStimulus>();
    }

    public void startSequence()
    {
        trialCoroutine = StartCoroutine(trialProgress());
    }

    /// <summary>
    /// Coroutine: starts the continuous beep train, waits for trial end, then stops it.
    /// </summary>
    IEnumerator trialProgress()
    {
        float trialDuration = runExperiment.thisTrialDuration;

        // Start the continuous beep train (it runs its own internal loop
        // with standard→changed→reset phases and input checking)
        StartCoroutine(makeAuditoryStimulus.RunBeepTrain(trialDuration, runExperiment));

        // Wait for trial to end
        while (runExperiment.trialTime < trialDuration)
        {
            yield return null; // wait until next frame
        }

        // Stop the beep train
        makeAuditoryStimulus.StopBeepTrain();
    }
}
