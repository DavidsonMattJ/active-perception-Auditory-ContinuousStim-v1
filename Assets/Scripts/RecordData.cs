using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Linq;
using UnityEditor;

public class RecordData : MonoBehaviour
{
    /// <summary>
    /// This script produces a simple output to test write/save features.
    /// upon Left click, the data stream is recorded (head pos - xyz pozition).
    /// after 1 sec duration, it is then written to disk at location *outputFolder*
    /// </summary>


    // updated 2025-05 MD to enable gaze origin and direction recordings.
    // preallocate output file, and folder
    public string outputFile_pos, outputFile_posEye, outputFile_summary, outputFolder;
    List<string> outputData_pos = new List<string>();
    List<string> outputData_summary = new List<string>();
    public string startTime;
    float data_trialTime;
    //assign public GameObj for easey access to hmd and gaze daa (controllers too if needed.)
    public GameObject objHMD; // drag and drop
    public GameObject objGazeInteractor;

    //talk to:
    CollectPlayerInput CollectPlayerInput;
    GazeandEyeMethods GazeandEyeMethods;
    runExperiment runExperiment;

    experimentParameters experimentParameters;
    private bool clickStateL, clickStateR; // to test if input is saved from triggers

    //access the gaze data
    float trialTime;
    //flow handler:
    
    string gazeObject;
    string projectName = "Auditory-Single-Stim-v1";


    public enum phase // these fields can be set by other scripts (runExperiment) to control record state.
    {
        idle,
        collectResponse,
        collectTrialSummary,
        stop
    };

    //set to idle to begin with:
    public phase recordPhase = phase.idle;

    // Start is called before the first frame update
    void Start()
    {

        //make sure we have access to all components. 
        CollectPlayerInput = GetComponent<CollectPlayerInput>(); // on same GameObj.
        runExperiment = GetComponent<runExperiment>();
        experimentParameters = GetComponent<experimentParameters>();
        GazeandEyeMethods = GetComponent<GazeandEyeMethods>();

        //set up output details:
        outputFolder = GetOutputFolder();
        Debug.Log("saving to location " + outputFolder);

        startTime = System.DateTime.Now.ToString("yyyy-MM-dd-hh-mm");
        

        // only create if playing in VR. 
        if (runExperiment.playinVR)
        {
            createPositionTextfile();

        }
        createSummaryTextfile(); // each row is populated based on single events (targets shown or not).



    }

    // Update is called once per frame
    void Update()
    {

        if (recordPhase == phase.idle)
        {
            if (data_trialTime > 0)
            {
                data_trialTime = 0; //reset
            }
        }

        if (recordPhase == phase.collectResponse)
        {
            // write each frame to our file:
            if (runExperiment.playinVR)
            {
                writePositionData(); // also increments trialTime for the datasave.
            }
        }

        if (recordPhase == phase.stop) //
        {
            //write to disk.
        
            writeFiletoDisk();
            
            
        }

    }



    public void createPositionTextfile()
    {
        outputFile_pos = outputFolder + runExperiment.participant  + "_" + startTime + ".csv";

        // Ensure the output directory exists
        string dir = System.IO.Path.GetDirectoryName(outputFile_pos);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Debug.Log("Created output directory: " + dir);
        }

        string columnNamesPos = "trialTime," +
            "clickstate_L," +
            "clickstate_R," +
            "head_X," +
            "head_Y," +
            "head_Z," +
            "gazeOrigin_X," +
            "gazeOrigin_Y," +
            "gazeOrigin_Z," +
            "gazeDirection_X," +
            "gazeDirection_Y," +
            "gazeDirection_Z," +
            "gazeHit_X," +
            "gazeHit_Y," +
            "gazeHit_Z," +
            "gazeHitObject," +
            "gazeAngularSpeed" +
            "\r\n";

        File.WriteAllText(outputFile_pos, columnNamesPos);

    }


    public void writePositionData()
    {

        Vector3 currentHead = objHMD.transform.position;
        clickStateL = CollectPlayerInput.leftisPressed;
        clickStateR = CollectPlayerInput.rightisPressed;

        // NB that the position in the Transform of the GazeInteration object is the gaze origin. 
        //This is where the gaze ray starts from, calculated as the centre point between the eyes in the virtual space.
        //The Rotation (and forward vector derived from this rotation) represents the gaze direction/which way you are looking.

        Vector3 gazeDirection = objGazeInteractor.transform.forward;
        Vector3 gazeOrigin = objGazeInteractor.transform.position;
        GameObject gazeHitObject = GazeandEyeMethods.GazeHitObject;
        float gazeAngularSpeed = GazeandEyeMethods.GazeAngularSpeed;
        
        // test if gazeHitObject is the "Screen" object, 
        if (gazeHitObject != null)
        {
            gazeObject = gazeHitObject.name;
            //shorten the string to make things easier:
            // if contains Screen (UnityEngine.GameObject)), =1, 0 otherwise
            if (gazeObject.Contains("Screen"))
            {
                gazeObject = "Screen";
            }   else
            {
                //
                gazeObject = "notScreen";
            }
        }
        else
        {
            gazeObject = "null";
        }

        // gazeHitObject='test';
        //attempt to get the hitpoint in world space of the ray cast from eyes: 
        //Currently NOT WORKING: TO DO AND UPDATE.
        
        Vector3 gazeHit = GazeandEyeMethods.GazeHitPosition;
        // Vector3 gazeHit = gazeDirection;
        // float pupilDiameter = GazeandEyeMethods.AveragePupilDiameter;
        // float pupilDiameter = 0f; // UPDATE

        // DATA input must match the order in create_positionTextFile() above:
        // left, right, xyz.
        // per frame, append the relevant data to per column of our datastructure:
        string data =
                runExperiment.trialTime + "," +
                clickStateL + "," +//    "clickstateL," +
                clickStateR + "," +//"clickstateR," +
                currentHead.x + "," + //"headX," +
                currentHead.y + "," + //"headY," +
                currentHead.z + "," + //"headZ," +
                gazeOrigin.x + "," + //"gazeOX," +
                gazeOrigin.y + "," + //             
                gazeOrigin.z + "," + //             
                gazeDirection.x + "," + //gazeDirection (forward)
                gazeDirection.y + "," + //
                gazeDirection.z + "," + //
                gazeHit.x + "," + // hit in world space
                gazeHit.y + "," + //
                gazeHit.z + "," + //
                gazeHitObject + "," + //
                gazeAngularSpeed; // +
                // pupilDiameter; // average of left and right.


        outputData_pos.Add(data);


    }
    private void createSummaryTextfile()
    {
        outputFile_summary = outputFolder + runExperiment.participant + "_" + startTime + "_trialsummary.csv";

        // Ensure the output directory exists
        string dir = System.IO.Path.GetDirectoryName(outputFile_summary);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Debug.Log("Created output directory: " + dir);
        }

        string columnNamesSumm = "date," +
           "participant," +
           "respMap," +
           "trial," +
           "block," +
           "trialID," +
           "walkSpeed," +
           "changeIndex," +
           "baseFrequencyHz," +
           "baseISIMs," +
           "deltaMs," +
           "isFaster," +
           "changeOnsetTime," +
           "clickOnsetTime," +
           "reactionTime," +
           "respondedFaster," +
           "isCorrect," +
           "isFalseAlarm";

        columnNamesSumm += "\r\n";

        File.WriteAllText(outputFile_summary, columnNamesSumm);
    }


    // Called per change event (response/miss) or false alarm by runExperiment.
    // Receives the immutable StimulusEvent snapshot — this guarantees the logged
    // data matches what was actually played, regardless of subsequent state changes.
    //
    // Data sources:
    //   evt            → stimulus properties (baseFrequencyHz, baseISIMs, deltaMs, isFaster, changeOnsetTime, changeIndex)
    //   trialD         → trial context (blockID, trialID, blockType) and response (targCorrect, targResponse, clickOnsetTime)
    //   runExperiment  → session-level info (participant, trialCount, responseMap)
    public void extractEventSummary(experimentParameters.StimulusEvent evt, bool isFalseAlarm)
    {
        // CSV columns (same order as header in createSummaryTextfile):
        //   date, participant, respMap, trial, block, trialID, walkSpeed,
        //   changeIndex, baseFrequencyHz, baseISIMs, deltaMs, isFaster,
        //   changeOnsetTime, clickOnsetTime, reactionTime,
        //   respondedFaster, isCorrect, isFalseAlarm

        // Calculate reaction time: click time minus change onset (both trial-relative)
        // For misses (clickOnsetTime == -1) or false alarms, RT may be meaningless
        float reactionTime = experimentParameters.trialD.clickOnsetTime - evt.changeOnsetTime;

        // targResponse: 1=faster, 0=slower, -1=no response
        float respondedFaster = experimentParameters.trialD.targResponse;
        int isCorrect = experimentParameters.trialD.targCorrect;

        string data =
                  System.DateTime.Now.ToString("yyyy-MM-dd") + "," +
                  runExperiment.participant + "," +
                  runExperiment.responseMap + "," +                          // response mapping (1 or -1)
                  runExperiment.trialCount + "," +
                  experimentParameters.trialD.blockID + "," +               // trial context
                  experimentParameters.trialD.trialID + "," +               // trial context
                  experimentParameters.trialD.blockType + "," +             // walk speed (0=stat, 1=slow, 2=natural)
                  evt.changeIndex + "," +                                    // which change in trial (-1 = false alarm)
                  evt.baseFrequencyHz + "," +                               // beep frequency
                  evt.baseISIMs + "," +                                     // standard ISI
                  evt.deltaMs + "," +                                       // ISI shift magnitude
                  (evt.isFaster ? 1 : 0) + "," +                           // actual direction: 1=faster, 0=slower
                  evt.changeOnsetTime + "," +                               // when ISI changed
                  experimentParameters.trialD.clickOnsetTime + "," +        // when they clicked (-1 if no response)
                  reactionTime + "," +                                      // RT
                  respondedFaster + "," +                                   // 1=faster, 0=slower, -1=no response
                  isCorrect + "," +                                         // 1=correct direction, 0=wrong/miss
                  (isFalseAlarm ? 1 : 0);                                  // 1=false alarm, 0=real event

        outputData_summary.Add(data);

        // reset listener:
        runExperiment.collectEventSummary = false;
    }

    public void saveonBlockEnd()
    {
        saveRecordedDataList(outputFile_pos, outputData_pos);
        saveRecordedDataList(outputFile_summary, outputData_summary);

        // clear cache
        outputData_pos = new List<string>();
        outputData_summary = new List<string>();


        // if (runExperiment.isEyeTracked) // now saving eye tracking data as a separate thing.
        // {
        //     saveRecordedDataList(outputFile_posEye, outputData_posEye);
        //     outputData_posEye = new List<string>();
        // }
    }

    public void writeFiletoDisk()
    {
        if (runExperiment.playinVR)
        { // both
            saveRecordedDataList(outputFile_pos, outputData_pos);
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }
        else
        {
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }

        // clear cache
        outputData_pos = new List<string>();
        outputData_summary = new List<string>();



    }

    private void OnApplicationQuit() // for safety.
    {
        if (runExperiment.playinVR)
        { // both
            saveRecordedDataList(outputFile_pos, outputData_pos);
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }
        else
        {
            saveRecordedDataList(outputFile_summary, outputData_summary);
        }
    }

    static void saveRecordedDataList(string filePath, List<string> dataList)
    {
        // Robert Tobin Keys:
        // I wrote this with System.IO ----- this is super efficient

        using (StreamWriter writeText = File.AppendText(filePath))
        {
            foreach (var item in dataList)
                writeText.WriteLine(item);
        }
    }

    
    



    private string GetOutputFolder()
    {
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        //  projectName defined above
        string parentDir = System.IO.Path.GetDirectoryName(projectRoot);
        string baseOutputPath;

#if UNITY_EDITOR_WIN
            // Windows: C:/Users/[username]/Documents/Unity Projects/UnityOutputData/
            string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            baseOutputPath = System.IO.Path.Combine(userProfile, "Documents", "Unity Projects", "UnityOutputData");
#elif UNITY_EDITOR_OSX
        // Mac: /Users/[username]/Documents/Unity Projects/UnityOutputData/
        string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        baseOutputPath = System.IO.Path.Combine(userHome, "Documents", "Unity Projects", "UnityOutputData");
#else
            // Linux or other: ~/Documents/Unity Projects/UnityOutputData/
            string userHome = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            baseOutputPath = System.IO.Path.Combine(userHome, "Documents", "Unity Projects", "UnityOutputData");
#endif

        return System.IO.Path.Combine(baseOutputPath, projectName) + System.IO.Path.DirectorySeparatorChar;


    }

}
