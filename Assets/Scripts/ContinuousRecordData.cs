using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ContinuousRecordData : MonoBehaviour
{
    /// <summary>
    /// Data logger for the continuous auditory tracking task.
    ///
    /// Writes one row per frame (~60 Hz) during each trial:
    ///   trialTime, currentISIMs, controllerRollDeg, trackingResponse,
    ///   head_X, head_Y, head_Z, clickstate_L, clickstate_R
    ///
    /// Output:
    ///   {participant}_{timestamp}_continuous.csv
    ///   ~/Documents/Unity Projects/UnityOutputData/Auditory-Continuous-Stim-v1/
    /// </summary>

    // ──────────────────────────────────────────────────────────────────
    //  Inspector references
    // ──────────────────────────────────────────────────────────────────

    [Tooltip("Drag the HMD/camera GameObject here.")]
    public GameObject objHMD;

    // ──────────────────────────────────────────────────────────────────
    //  Recording state machine (set by runContinuousExperiment)
    // ──────────────────────────────────────────────────────────────────

    public enum Phase { Idle, CollectResponse, Stop }
    public Phase recordPhase = Phase.Idle;

    // ──────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────

    private string outputFile;
    private string outputFolder;
    private string startTime;
    private List<string> outputData = new List<string>();
    private const string projectName = "Auditory-Continuous-Stim-v1";

    // Component references (resolved in Start)
    private runContinuousExperiment runner;
    private CollectPlayerInput playerInput;
    private ContinuousAuditoryStimulus continuousStim;
    private ContinuousControllerInput controllerInput;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Start()
    {
        runner = GetComponent<runContinuousExperiment>();
        playerInput = GetComponent<CollectPlayerInput>();
        continuousStim = runner.stimulusScreen.GetComponent<ContinuousAuditoryStimulus>();
        controllerInput = GetComponent<ContinuousControllerInput>();

        outputFolder = GetOutputFolder();
        startTime = System.DateTime.Now.ToString("yyyy-MM-dd-hh-mm");

        CreateContinuousFile();

        Debug.Log($"[ContinuousRecordData] Output: {outputFile}");
    }

    void Update()
    {
        if (recordPhase == Phase.CollectResponse)
            WriteFrameData();

        if (recordPhase == Phase.Stop)
            FlushToDisk();
    }

    // ──────────────────────────────────────────────────────────────────
    //  File creation
    // ──────────────────────────────────────────────────────────────────

    void CreateContinuousFile()
    {
        outputFile = outputFolder + runner.participant + "_" + startTime + "_continuous.csv";

        string dir = Path.GetDirectoryName(outputFile);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Debug.Log($"[ContinuousRecordData] Created output directory: {dir}");
        }

        string header =
            "trialTime," +
            "trial," +
            "block," +
            "blockType," +
            "currentISIMs," +
            "controllerRollDeg," +
            "trackingResponse," +
            "head_X," +
            "head_Y," +
            "head_Z," +
            "clickstate_L," +
            "clickstate_R" +
            "\r\n";

        File.WriteAllText(outputFile, header);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Per-frame write (called from Update while CollectResponse)
    // ──────────────────────────────────────────────────────────────────

    void WriteFrameData()
    {
        Vector3 headPos = (objHMD != null)
            ? objHMD.transform.position
            : Vector3.zero;

        string row =
            runner.trialTime + "," +
            runner.trialCount + "," +
            runner.currentBlockID + "," +
            runner.currentBlockType + "," +
            continuousStim.currentISIMs.ToString("F2") + "," +
            controllerInput.controllerRollDeg.ToString("F2") + "," +
            controllerInput.trackingResponse.ToString("F4") + "," +
            headPos.x.ToString("F4") + "," +
            headPos.y.ToString("F4") + "," +
            headPos.z.ToString("F4") + "," +
            playerInput.leftisPressed + "," +
            playerInput.rightisPressed;

        outputData.Add(row);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Disk I/O
    // ──────────────────────────────────────────────────────────────────

    public void FlushToDisk()
    {
        if (outputData.Count == 0) return;

        using (StreamWriter writer = File.AppendText(outputFile))
        {
            foreach (string row in outputData)
                writer.WriteLine(row);
        }

        outputData.Clear();
        recordPhase = Phase.Idle;

        Debug.Log($"[ContinuousRecordData] Flushed to disk: {outputFile}");
    }

    private void OnApplicationQuit()
    {
        FlushToDisk();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Output folder (mirrors RecordData.cs pattern)
    // ──────────────────────────────────────────────────────────────────

    private string GetOutputFolder()
    {
        string userHome = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.Personal);

        string baseOutputPath = Path.Combine(
            userHome, "Documents", "Unity Projects", "UnityOutputData");

        return Path.Combine(baseOutputPath, projectName) + Path.DirectorySeparatorChar;
    }
}
