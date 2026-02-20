using UnityEngine;
using DG.Tweening;
using System.Net.Mail;
public class controlWalkingGuide : MonoBehaviour
{
    /// <summary>
    /// This script controls the movement of the walking guide, using the doTween package.
    /// The start/end location, and movement speed are predefined in the createTrialTypes.cs
    /// 
    /// </summary> Start is called once before the first execution of Update after the MonoBehaviour is created

    //dependencies
    runContinuousExperiment runExperiment; // to access trial increment
    experimentParameters expParams;

    public GameObject startZone;
    public GameObject endZone; // drag and drop.

    private Vector3 startZone_normal, startZone_slow, endZone_slow, endZone_normal, adjustmentVector; // to store adjusted start/end zone positions based on speed.
    public GameObject walkingGuide;
    public GameObject HMD; // head mounted display camera.
    private float offsetTowardCentre, reachBelowPcnt;
    public int nextSpeedIndex; // to store the speed index for the next trial. [ 0,1,2] = [stationary, slow, normal];
    private Vector3 centreLocation; // store the central location

    void Start()
    {
        //
        runExperiment = GetComponent<runContinuousExperiment>();
        expParams = GetComponent<experimentParameters>();
        //prefill params:
        offsetTowardCentre = 1f;//units
        reachBelowPcnt = 0.65f; //pcnt     
        centreLocation = walkingGuide.transform.position; //starts at centre.

        // start off with the start zones as calibrated by experimenter:
        startZone_normal = startZone.transform.position; 
        endZone_normal = endZone.transform.position;
        // note only endZone_slow is adjusted below, keep start zone fixed.
        
        //  slow positions (closer in) are calculated on walk calibration complete:

        DOTween.defaultEaseType = Ease.Linear; // Changes default for all tweens

        //move to centre at launch:
        setGuidetoCentre(); // quick set the walking guide to screen centre.

        

    }

    public void calculateAdjustedStartEndZones() // should be called only once at walkspeed calibration end.
    {
        // called from walk speed calibrator on completion of walk speed calibration.
        // normDuration is the time taken to walk the full physical distance. 
        // what is the normal distance, based on start and end zone positions?
        float defaultDistance = expParams.distanceBetweenZones;  // m
        //we now know that the calibrated duration is something equal to longer than expParams.walkDuration (default).
        // int the case of slow walkers, we don't want them seeing more trials (on average), so shorten the runway:

         // using normSpeed, what is the duration?
        float normSpeed = expParams.normSpeed; // m/s
        float walkDur = expParams.walkDuration; // s
        // given the difference in walking speeds, what distance should we set for slow speed to match duration?
        float slowSpeed = expParams.slowSpeed; // m/s
        float slowDistance = slowSpeed * walkDur; // m
        float normDistance = normSpeed * walkDur;
        Debug.Log("calculated slow distance to match duration: " + slowDistance + "m");
        Debug.Log("calculated normal distance to match duration: " + normDistance + "m");

        // first, adjust the start and end zones (only once), for the natural speed condition.
        //
        float distanceDiff_n = defaultDistance - normDistance; // m

        Debug.Log("adjusting start and end zones based on calibraed speed: " + distanceDiff_n + "m");
        Vector3 adjustmentVector = new Vector3(distanceDiff_n/2, 0, 0); // adjust along x axis only.
        endZone_normal = endZone.transform.position - adjustmentVector; // move inward along x axis.
        startZone_normal = startZone.transform.position + adjustmentVector; // move  along x axis. (keeps walk centred at zero.)
    
        // now also calculat the slow speed start and end zones:
        
        // set new start and end zone positions based on these distances. Do this by adjusting only the end zone, to match the distance.
        float distanceDiff_s = normDistance - slowDistance; // m

        Debug.Log("distance difference between normal and slow: " + distanceDiff_s + "m");
        // adjust end zone position inward by  this distance.
        adjustmentVector = new Vector3(distanceDiff_s/2, 0, 0); // adjust along x axis only.
        endZone_slow = endZone_normal - adjustmentVector; // move inward along x axis.
        startZone_slow= startZone_normal + adjustmentVector; // move  along x axis. (keeps walk centred at zero.)
        Debug.Log("set slow end zone position to: " + endZone_slow);

    }

    public void SetGuideForNextTrial()
    {
        // set the walking guide to the next position, based on the current trial type.
        // if stationary, set to centre.
        // if moving, set to start or end zone.
        // based on the next trial speed, adjust the  end zone accordingly.

        int nextTrialIndex = runExperiment.trialCount + 1; // next trial index;
        
        nextSpeedIndex = expParams.blockTypeArray[nextTrialIndex, 2]; // speed index for next trial. [ 0,1,2] = [stationary, slow, normal];
        Debug.Log("adjusting end zone for speed index: " + nextSpeedIndex);

        // if next is slow, set closer end zones
        // if next is normal or stationary, leave as is.

        // adjust the distance between zones while walking at slow/natural speed to be matched in duration 
        if (nextSpeedIndex == 1) // slow speed
        {
            // set closer zones:
            endZone.transform.position = endZone_slow; 
            
            startZone.transform.position = startZone_slow; 
            Debug.Log("set start/end zones for slow speed");
        }
        else if (nextSpeedIndex == 2) // normal speed
        {
            // set further zones:

            endZone.transform.position = endZone_normal;
            startZone.transform.position = startZone_normal;
            Debug.Log("set start/end zones for normal speed");
        }

        // now determine starting location:
        if (expParams.blockTypeArray[nextTrialIndex, 2] == 0) // next trial is stationary
        {
            setGuidetoCentre();
        }
        else // next trial is not stationary, so set to either start or end position based on trial index.
        {
            if (nextTrialIndex % 2 == 0) // currnt trial just ended, set to endzone position
            {
                setGuidetoEnd();
            }
            else // odd trial index just ended, back to start
            {
                setGuidetoStart();// also applies rotation toward centre?
            }
        }

        // we can also calculate next trial speed here if needed.
        nextSpeedIndex = expParams.blockTypeArray[nextTrialIndex, 2]; // speed index for next trial. [ 0,1,2] = [stationary, slow, normal];
        Debug.Log("next speed index is: " + nextSpeedIndex);

    }
    public void setGuidetoCentre()
    {
        Vector3 currentPos = walkingGuide.transform.position;
        currentPos.x = centreLocation.x;
        // apply :
        walkingGuide.transform.position = currentPos;
        //ensure we are facing the centre of the environment.
        walkingGuide.transform.LookAt(new Vector3(0, walkingGuide.transform.position.y, 0));
    }

    public void setGuidetoStart()
    {

        Debug.Log("Setting guide to start zone");
        //ensure we are facing the centre of the environment.

        Vector3 currentPos = walkingGuide.transform.position;
        Vector3 desiredPos = startZone.transform.position;
        desiredPos.x = desiredPos.x + offsetTowardCentre; //start zone is neg(left of centre).
        desiredPos.y = currentPos.y;


        walkingGuide.transform.position = desiredPos;
        //apply rotation:
        walkingGuide.transform.LookAt(new Vector3(0, walkingGuide.transform.position.y, 0));

        Debug.Log("original Pos: " + currentPos);
        Debug.Log("desired Pos: " + desiredPos);
        Debug.Log("actual Pos: " + walkingGuide.transform.position);

        setGuidetoHidden();
        walkingGuide.transform.DOMove(desiredPos,0.5f).SetEase(Ease.Linear);
        
    }

    public void setGuidetoEnd()
    {

        Debug.Log("Setting guide to End zone");

        Vector3 currentPos = walkingGuide.transform.position;
        Vector3 desiredPos = endZone.transform.position;
        desiredPos.x = desiredPos.x - offsetTowardCentre; //end zone is positive (right of centre).
        desiredPos.y = currentPos.y;//apply:

        walkingGuide.transform.position = desiredPos;
        //ensure we are facing the centre of the environment.
        walkingGuide.transform.LookAt(new Vector3(0, walkingGuide.transform.position.y, 0));

        Debug.Log("original Pos: " + currentPos);
        Debug.Log("desired Pos: " + desiredPos);
        Debug.Log("actual Pos: " + walkingGuide.transform.position);
        // walkingGuide.
        setGuidetoHidden();
        walkingGuide.transform.DOMove(desiredPos, 0.5f).SetEase(Ease.Linear);
        // or move instantly?
        // need to turn off rigitbody first.
        // walkingGuide.transform.position = desiredPos; 
        
    }

    public void setGuidetoHidden()
    {
        // shoot skyward so its out of sight:
        Vector3 currentPos = walkingGuide.transform.position;
        currentPos.x = 0; //adjust to centre.
        currentPos.y = 20; //adjust to above (out of view).
        walkingGuide.transform.DOMove(currentPos, 0.15f).SetEase(Ease.Linear);

        // walkingGuide.transform.position = currentPos; // not working?
    }
    public void updateScreenHeight()
    {
        // Pseudocodde:
        // if in VR mode, get the current head height and adjust the walking guide accordingly.:
        if (runExperiment.playinVR)
        {

            Debug.Log("updating screen height based on HMD");
            Vector3 headPosition = HMD.transform.position;
            Vector3 currentPos = walkingGuide.transform.position;

            currentPos.y = HMD.transform.position.y * reachBelowPcnt; //proportion of height.
            //update:.

            walkingGuide.transform.position = currentPos;
        }

    }

    // we have a method that is called from the runExperiment script, if start trial Input is passed.

    public void moveGuideatWalkSpeed()
    {
        // based on current location, set end point / direction. (between start and end, or stationary).
        // based on current trial information, determine guide speed.

        float currentPos = walkingGuide.transform.position.x;
        float currentHeight = walkingGuide.transform.position.y; 

        // if x is negative, we are at the start pos, if positive, we are at the end pos
        // (negative to positive is left to right of central origin)

        Vector3 endPoint = new Vector3(0, 0, 0);


        if (currentPos > 0) // then right of centre (at endZone)
        {
            endPoint = startZone.transform.position;
            
        }
        else //then left of centre (at startZone).
        {
            endPoint = endZone.transform.position;
            
        }
        // included to avoid sloped trajectory:
        endPoint.y = currentHeight;

        // move at the appropriate speed for this trial. 
        Debug.Log("moving now....");

        // note that the default DOMove contains 'quad' easing, which is asymmetric at start and end.
        walkingGuide.transform.DOMove(endPoint, runExperiment.thisTrialDuration).SetEase(Ease.Linear); // should already be default.


        //alternatives to trial:
        //Fast start, slow end(default)
        //transform.DOMove(endPoint, walkParams.normDuration).SetEase(Ease.OutQuad); //default

        // Slow start, fast end
        //transform.DOMove(endPoint, walkParams.normDuration).SetEase(Ease.InQuad);

        //// Slow start and end, fast middle
        //transform.DOMove(endPoint, walkParams.normDuration).SetEase(Ease.InOutQuad);

        //// Bouncy effect
        //transform.DOMove(endPoint, walkParams.normDuration).SetEase(Ease.OutBounce);

        //// Elastic effect
        //transform.DOMove(endPoint, walkParams.normDuration).SetEase(Ease.OutElastic);


        //can also change the global default once happy:
        // 

    }

}
