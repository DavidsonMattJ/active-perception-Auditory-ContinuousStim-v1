using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using System;

public class ContinuousControllerInput : MonoBehaviour
{
    /// <summary>
    /// Reads the right-hand controller roll (long-axis spin) each frame and exposes
    /// a normalised tracking response in [-1, +1]:
    ///   +1 = full right tilt → participant perceives sounds as faster
    ///   -1 = full left tilt  → participant perceives sounds as slower
    ///    0 = neutral (controller upright)
    ///
    /// VR:      samples XR deviceRotation quaternion, extracts the roll component
    /// Keyboard: Left/Right arrow keys drive the response for debug/non-VR testing
    /// </summary>

    // ──────────────────────────────────────────────────────────────────
    //  Inspector parameters
    // ──────────────────────────────────────────────────────────────────

    [Header("Normalisation")]
    [Tooltip("Controller roll angle (deg) that maps to ±1 tracking response. " +
             "e.g. 90 means a 90° tilt = full faster/slower report.")]
    [SerializeField] private float fullScaleRollDeg = 90f;

    [Header("Keyboard (non-VR) settings")]
    [Tooltip("Speed at which keyboard response lerps toward ±1 when key held, " +
             "and back to 0 when released. Higher = snappier.")]
    [SerializeField] private float keyboardLerpSpeed = 5f;

    // ──────────────────────────────────────────────────────────────────
    //  Public state (read each frame by ContinuousRecordData)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Raw right controller roll in degrees (−180 to +180). 0 in non-VR mode.</summary>
    public float controllerRollDeg;

    /// <summary>
    /// Normalised tracking response in [−1, +1].
    ///  +1 = tilted fully right → perceived as faster
    ///  −1 = tilted fully left  → perceived as slower
    ///   0 = neutral / upright
    /// </summary>
    public float trackingResponse;

    // ──────────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────────

    private XRInputDevice rightControllerDevice;
    private bool deviceInitialized = false;
    private bool playinVR;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Start()
    {
        // runContinuousExperiment sits on the same ScriptHolder GameObject
        runContinuousExperiment runner = GetComponent<runContinuousExperiment>();
        playinVR = runner != null && runner.playinVR;

        controllerRollDeg = 0f;
        trackingResponse = 0f;

        if (playinVR)
            InitializeRightController();
    }

    void Update()
    {
        if (playinVR)
            ReadVRRoll();
        else
            ReadKeyboardResponse();
    }

    // ──────────────────────────────────────────────────────────────────
    //  VR roll reading
    // ──────────────────────────────────────────────────────────────────

    void InitializeRightController()
    {
        var devices = new List<XRInputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
            devices);

        if (devices.Count > 0)
        {
            rightControllerDevice = devices[0];
            deviceInitialized = true;
            Debug.Log($"[ContinuousControllerInput] Right controller found: {rightControllerDevice.name}");
        }
        else
        {
            Debug.LogWarning("[ContinuousControllerInput] Right controller not found. Will retry.");
        }
    }

    void ReadVRRoll()
    {
        if (!deviceInitialized)
        {
            InitializeRightController();
            return;
        }

        if (!rightControllerDevice.isValid)
            return;

        Quaternion rot;
        if (!rightControllerDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out rot))
            return;

        // Extract roll (Z-axis rotation in Euler) and remap to [-180, +180]
        float rawRoll = rot.eulerAngles.z +180f;
        // Extract roll (Z-axis rotation in Euler) and remap to [-180, +180]
        // float rawRoll = rot.eulerAngles.y;


        

        if (rawRoll > 180f) rawRoll -= 360f;

        controllerRollDeg = Mathf.Round(rawRoll);

        // Normalise: divide by fullScaleRollDeg, clamp to [-1, +1]
        // Right tilt → positive roll → +1 ("faster")
        // Left tilt  → negative roll → -1 ("slower")
        trackingResponse = Mathf.Clamp(controllerRollDeg / fullScaleRollDeg, -1f, 1f);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Keyboard fallback (non-VR debug)
    // ──────────────────────────────────────────────────────────────────

    void ReadKeyboardResponse()
    {
        bool rightHeld = Input.GetKey(KeyCode.RightShift);
        bool leftHeld  = Input.GetKey(KeyCode.LeftShift);

        // Only move while a key is held — release leaves the value where it is.
        // Both held simultaneously = no movement (ambiguous).
        float delta = 0f;
        if      (rightHeld && !leftHeld) delta =  keyboardLerpSpeed * Time.deltaTime;
        else if (leftHeld  && !rightHeld) delta = -keyboardLerpSpeed * Time.deltaTime;

        float prev = trackingResponse;
        trackingResponse = Mathf.Clamp(trackingResponse + delta, -1f, 1f);

        // Mirror as a roll angle for uniform logging
        controllerRollDeg = trackingResponse * fullScaleRollDeg;

        // Log whenever value changes noticeably (editor debugging only)
        if (Mathf.Abs(trackingResponse - prev) > 0.005f)
            Debug.Log($"[ContinuousControllerInput] trackingResponse={trackingResponse:F3} " +
                      $"(L={leftHeld} R={rightHeld})");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Utility
    // ──────────────────────────────────────────────────────────────────

    public void RefreshController()
    {
        deviceInitialized = false;
        InitializeRightController();
    }
}
