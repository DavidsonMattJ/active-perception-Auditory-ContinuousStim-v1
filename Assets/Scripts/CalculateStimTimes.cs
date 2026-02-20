//using System;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.AR;


public class CalculateStimTimes : MonoBehaviour
{
    // To be linked to values in other scripts
    public int nTrials;

    //float tTotal = 7.5f;
    float tTotal;
    float seqUnit;

    float startBuffer;   // Start and end buffers set to zero as we have Acc and Dec provisions
    float endBuffer;

    // Jitter related
    float jitMax;   // max jitter between stim  in sequence

    float stimDur;  // Stimulus Duration, called from exp params.
    float respWin;  // Response Window length, called from exp params.

    //string outputFile;
    string filePath;

    public List<float[]> allOnsets = new List<float[]>(); // This list will hold a row vector for each trial (list instead of array as the number of columns may vary)

    // Scripts referenced

    experimentParameters expParams;
    
    RecordData recordData;
    runExperiment runExp;

    

    // Start is called before the first frame update
    void Start()
    {
    
        // Find referenced scripts
        expParams = GetComponent<experimentParameters>();
        recordData = GetComponent<RecordData>();
        runExp = GetComponent<runExperiment>();

    }

    

    public void CalculateStimulusPresentationTimes()
    {

        // some values fixed for all trials:
        
        stimDur = expParams.targDurationsec;
        respWin = expParams.responseWindow;
        jitMax  = expParams.jittermax;
        seqUnit =stimDur + respWin;// + jitMax;

        // Load nTrials value from trialParams
        nTrials = expParams.nTrialsperBlock * expParams.nBlocks;

        startBuffer = expParams.preTrialsec;
        endBuffer = expParams.responseWindow; ;  // symmetric for now

        for (int itrial = 0; itrial < nTrials; itrial++)
        {



            // Load tTotal from walkParams

            // NOTE WELL - tTotal in this script is equal to tWalk in walkParams (not tTotal in walk Params)
            // byt this stage tWalk has been calculated either based on 
            // walk calibration or default values in the case of !calibrateWalk.
            tTotal = expParams.walkDuration;

            float prejit = Random.Range(0f, 1f);    // In addition to the preTrial buffer, so the first presentation time is expPamarams.preTrialSec + preJit)



            // on half our trial types, we want to shift the onset times all slightly, to infill different periods of the walk.
            int infillTargs = Random.Range(0f, 1f) < 0.5f ? 1 : 0;
            
            float infillOffset = infillTargs==1 ? (seqUnit / 2f):0f; // shift or no shift after coin flip.



            float tAvail = tTotal - startBuffer - endBuffer - prejit;

            //calculate base spacing using all available time:
            int nTargs = (int)Mathf.Floor(tAvail / seqUnit);
            
            // Calculate base spacing that uses all available time
            float baseSpacing = tAvail / nTargs;

            float[] onsets = new float[nTargs];

            // Apply infill offset to the FIRST onset
            onsets[0] = startBuffer + prejit + infillOffset;

            for (int n = 1; n < nTargs; n++)
            {
                // Use baseSpacing instead of seqUnit, add random jitter
                float jitter = Random.Range(0f, jitMax);
                onsets[n] = onsets[n - 1] + baseSpacing + jitter;
                
                // Ensure minimum spacing is maintained
                float minNext = onsets[n - 1] + seqUnit;
                if (onsets[n] < minNext)
                {
                    onsets[n] = minNext;
                }
            }

            // // Start Buffer-> prejit-> [stimPres->respWin->j(n)]->End Buffer


            // float remainder = tAvail - (nTargs * seqUnit);

            // // After filling max number of target sequences, there will be some time left over
            // // // because seqUnit is based on Max jitter, we can adjust based on the jitters for each presentation.
            // // // first calculate the jitter between each target:
            // // float[] jitsare = new float[nTargs];
            // // for (int j = 0; j < nTargs; j++)
            // // {
            // //     jitsare[j] = Random.Range(0f, jitMax);
            // // }
            // // // what is remaining after the jitteR?
            // // float allJit = jitsare.Sum();
            // // float remjit = remainder- allJit;  // Remaining jitter time to be randomly split between nTargs presentations

            // // //float remJit = allJit + remainder - prejit - endjit;  // Remaining jitter time to be randomly split between nTargs-1 presentations (as the last presentation's jitter has already beena allocated
            // // // float remjit = allJit + remainder - prejit;


            // //DIVIDE REMAINING JITTER TIME RANDOMLY
            // // Make nTarg+1 break points on a line from 0 to remjit (inclusive) to divide it into n randomly proportioned segments

            // float[] breakPoints = new float[nTargs];  // we need to creat 1 per target.
            // // Add Start and end points
            // breakPoints[0] = 0;

            // for (int i = 1; i < nTargs; i++)
            // {
            //     breakPoints[i] = Random.Range(0, remainder);
            // }

            // // Add end point
            // // breakPoints[nTargs-1] = remainder;


            // // Sort breakPoints into ascending order
            // System.Array.Sort(breakPoints);


            // // Declare function to calculate difference between consecutive entries to find separate jitter amounts (durations)
            // float[] Diff(float[] x) => x.Zip(x.Skip(1), (a, b) => b - a).ToArray();

            // // Apply diff function to find differentials
            // float[] jitAmounts = Diff(breakPoints);


            // // Calculate what onset times

            // float[] onsets = new float[nTargs];

            // onsets[0] = startBuffer + prejit; // set first onset manually

            // for (int n = 1; n < nTargs; n++)  // set second onset onwards using loop
            // {
            //     onsets[n] = onsets[n - 1] + seqUnit + jitAmounts[n - 1]; // Based on the onset before, calculate this onset time
            // }


            // Add the onsets for this walk to the list of experiment onsets
            allOnsets.Add(onsets);

        }  // end of itrial loop


        Debug.Log("All Times Scripted");

        // Export Stmiulus time schedule as csv for checking later
        // recDat.saveTrialOnsetstoDisk(allOnsets);
        

        // filePath = recDat.outputFolder + runExp.participant + "_" + recDat.startTime + "_stimSchedule.csv";
        string tmpfilepath = recordData.outputFolder + runExp.participant  + recordData.startTime + "_stimSchedule.csv";

        int maxColumns = allOnsets.Max(row => row.Length); // Find longest row
        List<string> lines = new List<string>();

        foreach (var row in allOnsets)
        {
            // Convert row to CSV, padding shorter rows with empty values
            string line = string.Join(",", row.Select(n => n.ToString()).Concat(Enumerable.Repeat("", maxColumns - row.Length)));
            lines.Add(line);
        }

        File.WriteAllLines(tmpfilepath, lines);

        Debug.Log("Stimulus Presentation Times Calculated and Saved");
        
    }

}
