using UnityEngine;
using VIVE.OpenXR;
using UnityEngine.XR;
using System.Collections.Generic;
// using VIVE.OpenXR.FacialTracking; // (not needed since we use OpenXR namespace directly)
using UnityEngine.XR.OpenXR;
using NUnit.Framework;

public class GazeandEyeMethods : MonoBehaviour
{
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor gazeInteractor;

    [Header("Ray Visualization")]
    [SerializeField] private bool showRay = false;
    [SerializeField] private float rayLength = 20f;

    [Header("Hit Point Visualization")]
    [SerializeField] private bool showHitPoint = false;
    [SerializeField] private float hitPointSize = 0.2f;


    // Publicly accessible hit point data
    [Header("Eye tracking Data")]
    [SerializeField, ReadOnly] private Vector3 gazeHitPosition;
    [SerializeField, ReadOnly] private GameObject gazeHitObject;
    [SerializeField, ReadOnly] private bool isHittingSomething;
    [SerializeField, ReadOnly] private float gazeAngularSpeed = 0f; // deg/sec for saccade detection

    // // Publicly accessible pupil data (// not available for now)
    // [Header("Pupil Data")]
    // [SerializeField, ReadOnly] private float leftPupilDiameter = 0f;
    // [SerializeField, ReadOnly] private float rightPupilDiameter = 0f;
    // [SerializeField, ReadOnly] private float averagePupilDiameter = 0f;
    // [SerializeField, ReadOnly] private bool isPupilDataAvailable = false;

    // Public properties for easy access
    public Vector3 GazeHitPosition => gazeHitPosition;
    public GameObject GazeHitObject => gazeHitObject;
    public bool IsHittingSomething => isHittingSomething;
    public float GazeAngularSpeed => gazeAngularSpeed;
    // public float LeftPupilDiameter => leftPupilDiameter;
    // public float RightPupilDiameter => rightPupilDiameter;
    // public float AveragePupilDiameter => averagePupilDiameter;
    // public bool IsPupilDataAvailable => isPupilDataAvailable;

    private GameObject hitPointObject;
    private GameObject rayObject;
    private GameObject endPointObject;
    private Vector3 lastGazeDirection = Vector3.zero;
    private float lastUpdateTimeStamp = 0f;


    runContinuousExperiment runExp;
    

    // Eye tracking device reference
    private InputDevice eyeTrackingDevice;
    private bool deviceFound = false;

    // OpenXR references
    private List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();
    // Eye tracking feature usages (created once for performance)
    private InputFeatureUsage<Vector3> gazeAngularVelocityUsage = new InputFeatureUsage<Vector3>("gazeAngularVelocity");

    // private ViveFacialTracking viveFacialTracking;
    void OnEnable()
    {
        Debug.Log("GazeVisualizer enabled");

        // Find all XR input subsystems
        SubsystemManager.GetSubsystems(inputSubsystems);
        Debug.Log($"Found {inputSubsystems.Count} XR input subsystems");


        // // Try to get ViveFacialTracking instance
        // viveFacialTracking= OpenXRSettings.Instance.GetFeature<ViveFacialTracking>();
        // if (viveFacialTracking != null && viveFacialTracking.enabled)
        // {
        //     Debug.Log("ViveFacialTracking feature found.");
        // }
        // else
        // {
        //     Debug.LogWarning("ViveFacialTracking feature not found.");
        // }
    }

    private void Start()
    {

        // only continue if playing in VR mode:
        runExp = GetComponent<runContinuousExperiment>();

        if (runExp != null && !runExp.playinVR)
        {
            Debug.Log("Not in VR mode - disabling gaze visualizer");
            this.enabled = false;
            return; //exit to avoid running rest of start.
        }


        Debug.Log("GazeVisualizer started");

        // If not assigned in Inspector, try to get the component
        if (gazeInteractor == null)
        {
            gazeInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            Debug.Log("Found gazeInteractor: " + (gazeInteractor != null));
        }

        if (gazeInteractor == null)
        {
            Debug.Log("Gaze Interactor not found!");
            return;
        }

        // Find eye tracking device
        TryInitializeEyeTracking();

        // Create ray object (cylinder)
        rayObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rayObject.name = "GazeRay";
        Destroy(rayObject.GetComponent<Collider>()); // Remove collider
        rayObject.GetComponent<Renderer>().material.color = Color.red;
        rayObject.SetActive(showRay);

        // Create hit point object (sphere)
        hitPointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hitPointObject.name = "GazeHitPoint";
        Destroy(hitPointObject.GetComponent<Collider>()); // Remove collider
        hitPointObject.transform.localScale = Vector3.one * hitPointSize;
        hitPointObject.GetComponent<Renderer>().material.color = Color.green;
        hitPointObject.SetActive(showHitPoint);

        // Create end point object (always visible at end of ray)
        endPointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        endPointObject.name = "GazeEndPoint";
        Destroy(endPointObject.GetComponent<Collider>()); // Remove collider
        endPointObject.transform.localScale = Vector3.one * (hitPointSize * 0.5f);
        endPointObject.GetComponent<Renderer>().material.color = Color.yellow;
        endPointObject.SetActive(showRay);

        Debug.Log("Visualization objects created");
    }

    private void Update()
    {

        if (!runExp.trialinProgress)
        {
            return; // only continue if within a trial.
        } 
        if (!deviceFound)
        {
            TryInitializeEyeTracking();
        }
            

        if (gazeInteractor == null) return;

        Vector3 origin = gazeInteractor.transform.position;
        Vector3 direction = gazeInteractor.transform.forward;
        // Calculate angular velocity for saccade detection
        float currentTime = Time.realtimeSinceStartup;
        if (lastGazeDirection != Vector3.zero && lastUpdateTimeStamp > 0)
        {
            float angle = Vector3.Angle(lastGazeDirection, direction);
            float deltaTime = currentTime - lastUpdateTimeStamp;
            if (deltaTime > 0)
            {
                gazeAngularSpeed = angle / deltaTime; // degrees per second
            }
        }
        lastGazeDirection = direction;
        lastUpdateTimeStamp = currentTime;
       

        // Update ray visualization and hit data
        float distance = rayLength;
        isHittingSomething = false;
        gazeHitObject = null;

        // If we hit something, adjust ray length and store hit data
        if (gazeInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            distance = hit.distance;
            isHittingSomething = true;
            gazeHitObject = hit.collider.gameObject;
            gazeHitPosition = hit.point;

            //if (Time.frameCount % 300 == 0)
            //{
            //    Debug.Log($"Hit object: {hit.collider.gameObject.name} at distance: {distance}");
            //}
        }
        else
        {
            // If not hitting anything, set hit position to the end of the ray
            gazeHitPosition = origin + (direction * distance);
        }

        // Only update visuals if they're active
        if (showRay)
        {
            // Position cylinder at midpoint
            rayObject.transform.position = origin + (direction * distance / 2f);
            // Point it in the right direction
            rayObject.transform.up = direction;
            // Scale it to the right length and width
            rayObject.transform.localScale = new Vector3(0.01f, distance / 2f, 0.01f); // Thicker ray
            rayObject.SetActive(true);

            // Update end point position
            if (endPointObject != null)
            {
                endPointObject.transform.position = origin + (direction * distance);
                endPointObject.SetActive(true);
            }
        }
        else
        {
            rayObject.SetActive(false);
            endPointObject.SetActive(false);
        }

        // Update hit point visualization
        if (showHitPoint && hitPointObject != null)
        {
            if (isHittingSomething)
            {
                hitPointObject.SetActive(true);
                // Use the exact hit position without offset for the stored value
                hitPointObject.transform.position = gazeHitPosition;

                // Only apply offset to the visual representation, not the stored value
                if (hit.normal != Vector3.zero) // Ensure normal is valid
                {
                    hitPointObject.transform.position += hit.normal * 0.001f; // Small offset
                }
            }
            else
            {
                hitPointObject.SetActive(false);
            }
        }
        else
        {
            hitPointObject.SetActive(false);
        }
    }

    private void TryInitializeEyeTracking()
    {
        List<InputDevice> devices = new List<InputDevice>();

        // Get the eye tracking device
        InputDeviceCharacteristics eyeTrackingCharacteristics =
            InputDeviceCharacteristics.EyeTracking |
            InputDeviceCharacteristics.HeadMounted;

        InputDevices.GetDevicesWithCharacteristics(eyeTrackingCharacteristics, devices);

        if (devices.Count > 0)
        {
            eyeTrackingDevice = devices[0];
            deviceFound = true;
            Debug.Log($"Eye tracking device found: {eyeTrackingDevice.name}");

            // Log available feature usages for debugging
            List<InputFeatureUsage> featureUsages = new List<InputFeatureUsage>();
            if (eyeTrackingDevice.TryGetFeatureUsages(featureUsages))
            {
                Debug.Log("Available eye tracking feature usages:");
                foreach (var feature in featureUsages)
                {
                    Debug.Log($"- {feature.name} (type: {feature.type})");
                }
            }

        }

        // log if pupil data available: 
        // bool pupilSuccess = ViveFacialTracking;

        else
        {
            deviceFound = false;
            // isPupilDataAvailable = false;
        }
    }


    

    // Clean up
    private void OnDestroy()
    {
        if (hitPointObject != null)
            Destroy(hitPointObject);

        if (rayObject != null)
            Destroy(rayObject);

        if (endPointObject != null)
            Destroy(endPointObject);
    }
}

// Custom attribute to make serialized fields read-only in the inspector
public class ReadOnlyAttribute : UnityEngine.PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif