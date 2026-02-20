using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class CollectPlayerInput : MonoBehaviour
{
    [Header("State")]
    public bool leftisPressed;
    public bool rightisPressed;
    public bool botharePressed;
    public bool anyarePressed;

    // Previous state for change detection
    private bool previousLeftTriggerState = false;
    private bool previousRightTriggerState = false;
    private bool previousBothTriggerState = false;

    // XR InputDevice references (using alias to avoid conflicts)
    private XRInputDevice leftControllerDevice;
    private XRInputDevice rightControllerDevice;
    private bool devicesInitialized = false;

    runContinuousExperiment runExperiment;
    private bool playinVR;

    void Start()
    {
        runExperiment = GetComponent<runContinuousExperiment>();
        playinVR = runExperiment.playinVR;

        if (playinVR)
        {
            InitializeControllerDevices();
        }

        // Check for both triggers pressed simultaneously
        botharePressed = false;
        leftisPressed = false;
        rightisPressed = false;
        anyarePressed = false;
    }

    void Update()
    {
        if (playinVR)
        {
            checkVRcontrollers();
        }
        else
        {
            checkKeyboardcontrollers();
        }

        // Check for both keys pressed simultaneously
        botharePressed = leftisPressed && rightisPressed;
        anyarePressed = leftisPressed || rightisPressed;
    }

    void InitializeControllerDevices()
    {
        List<XRInputDevice> devices = new List<XRInputDevice>();
        
        // Get left controller
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, 
            devices);
        
        if (devices.Count > 0)
        {
            leftControllerDevice = devices[0];
            Debug.Log("Found left controller: " + leftControllerDevice.name);
        }

        devices.Clear();
        
        // Get right controller
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, 
            devices);
        
        if (devices.Count > 0)
        {
            rightControllerDevice = devices[0];
            Debug.Log("Found right controller: " + rightControllerDevice.name);
            devicesInitialized = true;
        }
        
        if (!devicesInitialized)
        {
            Debug.LogWarning("VR Controllers not found. Will retry...");
        }
    }

    void checkVRcontrollers()
    {
        // Retry initialization if devices weren't found initially
        if (!devicesInitialized)
        {
            InitializeControllerDevices();
            return;
        }

        // Check left controller trigger
        if (leftControllerDevice.isValid)
        {
            bool currentLeftPressed;
            if (leftControllerDevice.TryGetFeatureValue(XRCommonUsages.triggerButton, out currentLeftPressed))
            {
                leftisPressed = currentLeftPressed;
                
                // Log on state change
                if (leftisPressed && !previousLeftTriggerState)
                {
                    Debug.Log("Left Trigger Pressed");
                }
                previousLeftTriggerState = leftisPressed;
            }
            else
            {
                // Try trigger value as alternative
                float triggerValue;
                if (leftControllerDevice.TryGetFeatureValue(XRCommonUsages.trigger, out triggerValue))
                {
                    leftisPressed = triggerValue > 0.5f;
                    
                    if (leftisPressed && !previousLeftTriggerState)
                    {
                        Debug.Log("Left Trigger Pressed - Value: " + triggerValue);
                    }
                    previousLeftTriggerState = leftisPressed;
                }
            }
        }

        // Check right controller trigger
        if (rightControllerDevice.isValid)
        {
            bool currentRightPressed;
            if (rightControllerDevice.TryGetFeatureValue(XRCommonUsages.triggerButton, out currentRightPressed))
            {
                rightisPressed = currentRightPressed;
                
                // Log on state change
                if (rightisPressed && !previousRightTriggerState)
                {
                    Debug.Log("Right Trigger Pressed");
                }
                previousRightTriggerState = rightisPressed;
            }
            else
            {
                // Try trigger value as alternative
                float triggerValue;
                if (rightControllerDevice.TryGetFeatureValue(XRCommonUsages.trigger, out triggerValue))
                {
                    rightisPressed = triggerValue > 0.5f;
                    
                    if (rightisPressed && !previousRightTriggerState)
                    {
                        Debug.Log("Right Trigger Pressed - Value: " + triggerValue);
                    }
                    previousRightTriggerState = rightisPressed;
                }
            }
        }
    }

    void checkKeyboardcontrollers()
    {
        // Your existing keyboard input code
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            Debug.Log("Left Shift pressed!");
            leftisPressed = true;
        }

        if (Input.GetKeyDown(KeyCode.RightShift))
        {
            Debug.Log("Right Shift pressed!");
            rightisPressed = true;
        }

        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            leftisPressed = false;
        }

        if (Input.GetKeyUp(KeyCode.RightShift))
        {
            rightisPressed = false;
        }

        // Check for both keys pressed simultaneously
        botharePressed = leftisPressed && rightisPressed;
        
        // Log both key state changes  
        if (botharePressed && !previousBothTriggerState)
        {
            Debug.Log("Both Shift Keys Pressed Simultaneously");
        }
        else if (!botharePressed && previousBothTriggerState)
        {
            Debug.Log("Both Shift Keys Released");
        }
        previousBothTriggerState = botharePressed;
    }

    // Optional: Call this if you need to refresh controller connections
    public void RefreshControllers()
    {
        devicesInitialized = false;
        InitializeControllerDevices();
    }
}