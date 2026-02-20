using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework.Constraints;

public class ShowText : MonoBehaviour
{
    // this  script receives calls from other scipts (e.g. RunExperiment) and updats/removes the text from Canvas accordingly.


   public enum TextType
    {
        Hide = 0,
        Welcome = 1,
        CalibrationComplete=2,        
        TrialStart = 3,
        ExperimentComplete = 4,
        StandingInstructions = 5,
        WalkingInstructions = 6
    }

    private TextMeshProUGUI textMesh;
    private Dictionary<TextType, string> textStrings;

    [SerializeField]
    GameObject scriptHolder;
    [SerializeField]
    GameObject StimulusScreen;
    experimentParameters expParams;
    runContinuousExperiment runExperiment;
    controlWalkingGuide controlWalkingGuide;
    
    
    [SerializeField]
    GameObject TextBG; //assign in inspector
    public bool isInitialized= false;
    void Start()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        expParams = scriptHolder.GetComponent<experimentParameters>();
        runExperiment = scriptHolder.GetComponent<runContinuousExperiment>();
        controlWalkingGuide = scriptHolder.GetComponent<controlWalkingGuide>();
        

        // Initialize text dictionary, without dynamic strings.
        textStrings = new Dictionary<TextType, string>
        {
            [TextType.Hide] = "", // blank screen

            [TextType.Welcome] = "", // updated below

            [TextType.CalibrationComplete] = "", //dynamically updated below

            [TextType.TrialStart] = "", //dynamically updated with trial/block index below
            // ... add other strings


        };
        isInitialized=true; // mark as ready

    }

    public void UpdateText(TextType textType) // called at trialPackdown (.:. trial number is off by 1)
    {
        // Ensure dictionary is initialized
        if (!isInitialized)
        {
            Debug.LogWarning($"ShowText dictionary not yet initialized. Attempting to show: {textType}");
            return;
        }

        if (textStrings.ContainsKey(textType))
        {
            // Update the text mesh with the corresponding string
            if (textType == TextType.TrialStart)
            {
                //for convenience, show the next trial speed. 
                string speedText = "Stationary"; // default

                if (controlWalkingGuide.nextSpeedIndex == 0)
                {

                    textMesh.color = Color.black; // stationary - white
                    speedText = "Stationary";

                }
                else if (controlWalkingGuide.nextSpeedIndex == 1)
                {
                    textMesh.color = Color.blue; // slow - blue
                    speedText = "Walking Slowly";

                }
                else if (controlWalkingGuide.nextSpeedIndex == 2)
                {
                    textMesh.color = Color.yellow; // normal - yellow
                    speedText = "Walking Normally";

                }

                int nextTrialNum = 0;
                int nextBlockNum = 0;
                // if block end, we need to hard code the next:
                if (expParams.trialD.trialID == expParams.nTrialsperBlock - 1)
                {  // last in block
                    nextTrialNum = 1;
                    nextBlockNum = expParams.trialD.blockID + 2; // block IDs start at 0, 
                }
                else
                { //adapt with trial number: 
                    nextTrialNum = expParams.trialD.trialID + 2; // starts at zero, but new trial next trial (+1)
                    nextBlockNum = expParams.trialD.blockID + 1; // starts at zero, but same block next trial.
                }


                // this one needs to be updated with the current trial and block info. +2 since Unity starts index at 0 (+1), and we are preparing the next trial (+1).//
                textMesh.text = "Next trial, you will be " + speedText + "." + "\n\n" + " Pull both triggers to begin Trial " + (nextTrialNum) + " / " + expParams.nTrialsperBlock + "\n\n" +
                "(Block " + (nextBlockNum) + " of " + expParams.nBlocks + "). \n\n" +                
                "Remember: \n\n  Left= slower, Right = faster";
                    
                TextBG.SetActive(true); //show background to enhance text.
            }
            else if (textType == TextType.CalibrationComplete)
            {
                
                textMesh.text = "Well done! \n  Let's now practice the main task standing still. \n " +
                "Listen to instructions, then pull  <both triggers> to begin a practice trial \n\n" +                
                "Remember: \n\n  Left= slower, Right = faster";
                TextBG.SetActive(true);
            }
            else if (textType == TextType.Welcome) // update dynamically with response mapping
            {
                
                textMesh.text = "Welcome! \n Please listen to your experimenter for instructions. \n\n" +
                                "Remember: \n\n  Left= slower, Right = faster";
                TextBG.SetActive(true); //show background to enhance text.
            } else
            {
                textMesh.text = textStrings[textType];
                if (textType == TextType.Hide)
                {
                    TextBG.SetActive(false); //hide background
                }
                else
                {
                     TextBG.SetActive(true); //show background to enhance text.
                }
                
            }
            
        }
        else
        {
            Debug.LogWarning($"TextType {textType} not found in dictionary");
        }
    }

}
