using UnityEngine;
using UnityEngine.XR.OpenXR;
// using VIVE.OpenXR.FacialTracking;

public class PupilDataReader : MonoBehaviour
{
    
//     private ViveFacialTracking viveFacialTracking;
//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {
//         // establish connection
//         viveFacialTracking = OpenXRSettings.Instance.GetFeature<ViveFacialTracking>();
//     }

//     // Update is called once per frame
//     void Update()
//     {
//         // try to extract pupil data.
//         float[] eyeExps;
//         if (viveFacialTracking.GetFacialExpressions(XrFacialTrackingTypeHTC.XR_FACIAL_TRACKING_TYPE_EYE_DEFAULT_HTC, out eyeExps))
//         {
//             // log all Eye expressions to see whats available.
//             for (int i = 0; i < eyeExps.Length; i++)
//             {
//                 Debug.Log($"Eye Expression {i}: {eyeExps[i]}");
//             }       
//         }
//     }
}
