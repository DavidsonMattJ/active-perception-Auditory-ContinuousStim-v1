using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CalibrationText : MonoBehaviour
{
    // this  script receives calls from other scipts (e.g. RunExperiment) and updats/removes the text from Canvas accordingly.


   public enum TextType
    {
        Hide = 0,
        Welcome = 1,
        CalibState = 2,
                
        
    }
    // new comment
    private TextMeshProUGUI textMesh;
    private Dictionary<TextType, string> textStrings;
    
    [SerializeField]
    experimentParameters expParams;
    runExperiment runExperiment;
    controlWalkingGuide controlWalkingGuide;
    WalkSpeedCalibrator walkSpeedCalibrator;

    [SerializeField]
    GameObject TextBG; //assign in inspector

    [SerializeField]
    GameObject scriptHolder; //assign in inspector
    

    void Start()
    {
        textMesh = GetComponent<TextMeshProUGUI>(); // get text attached.
        
        walkSpeedCalibrator = scriptHolder.GetComponent<WalkSpeedCalibrator>(); 
        // walkSpeedCalibrator= 

        // Initialize text dictionary
        textStrings = new Dictionary<TextType, string>
        {
            [TextType.Hide] = "", // blank screen

            [TextType.Welcome] = "Welcome!  Please listen to your experimenter for instructions", //temp, updated below.

            [TextType.CalibState] = "", //temp, updated below.
            


        };
    }

    public void UpdateText(TextType textType)
    {
        // Ensure dictionary is initialized
        if (textStrings == null)
        {
            Debug.LogWarning($"ShowText dictionary not yet initialized. Attempting to show: {textType}");
            return;
        }


        // Update the text mesh with the corresponding string
        if (textType == TextType.CalibState)
        {

            //set:

            // this one needs to be updated with the current trial and block info. +2 since Unity starts index at 0 (+1), and we are preparing the next trial (+1).// +2 because the trialID is incremented after left click.
            textMesh.text = "Walk speed calibration in progress. " + "\n\n" +
            "Trial " + (walkSpeedCalibrator.currentLap+1) + " of " + walkSpeedCalibrator.requiredLaps;
            TextBG.SetActive(true); //show background to enhance text.
        }
        else if (textType == TextType.Welcome)
        {
            textMesh.text = textStrings[textType];
            TextBG.SetActive(true); //"hide";
        }
        else if (textType == TextType.Hide)
        {
            textMesh.text = textStrings[textType];
            TextBG.SetActive(false); //"hide";
        }


        //now set:

    }

}
