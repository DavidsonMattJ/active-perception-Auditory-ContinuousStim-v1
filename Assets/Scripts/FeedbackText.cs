using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class FeedbackText : MonoBehaviour
{
    // this  script receives calls from other scipts (e.g. RunExperiment) and updats/removes the text from Canvas accordingly.


   public enum TextType
    {
        Hide = -1,
        Correct = 1,
        Incorrect=0,        
        
    }

    private TextMeshProUGUI textMesh;
    private Dictionary<TextType, string> textStrings;

    [SerializeField]
    experimentParameters expParams;
    runExperiment runExperiment;
    controlWalkingGuide controlWalkingGuide;

    

    void Start()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        

        // Initialize text dictionary
        textStrings = new Dictionary<TextType, string>
        {
            [TextType.Hide] = "", // blank screen

            [TextType.Correct] = "Correct!",

            [TextType.Incorrect] = "Incorrect!",


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

        if (textStrings.ContainsKey(textType))
        {
            // Update the text mesh with the corresponding string
            if (textType == TextType.Correct)
            {


                textMesh.color = Color.green; // stationary - white


            }
            else if (textType == TextType.Incorrect)
            {
                textMesh.color = Color.red; // slow - blue

            }
            
            //set:
            textMesh.text = textStrings[textType];
        }
        else
        {
            Debug.LogWarning($"TextType {textType} not found in dictionary");
        }
    }

}
