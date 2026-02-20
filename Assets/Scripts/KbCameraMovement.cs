using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 90f; // degrees per second

    private bool isMovingForward = false;
    private bool isMovingBackward = false;
    private bool isRotatingLeft = false;
    private bool isRotatingRight = false;

    void Update()
    {
        HandleInput();
        ApplyMovement();
    }

    void HandleInput()
    {
        // Handle Up Arrow (Move Forward)
        if (Input.GetKeyDown(KeyCode.UpArrow))
            isMovingForward = true;
        if (Input.GetKeyUp(KeyCode.UpArrow))
            isMovingForward = false;

        // Handle Down Arrow (Move Backward)
        if (Input.GetKeyDown(KeyCode.DownArrow))
            isMovingBackward = true;
        if (Input.GetKeyUp(KeyCode.DownArrow))
            isMovingBackward = false;

        // Handle Left Arrow (Rotate Left)
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            isRotatingLeft = true;
        if (Input.GetKeyUp(KeyCode.LeftArrow))
            isRotatingLeft = false;

        // Handle Right Arrow (Rotate Right)
        if (Input.GetKeyDown(KeyCode.RightArrow))
            isRotatingRight = true;
        if (Input.GetKeyUp(KeyCode.RightArrow))
            isRotatingRight = false;
    }

    void ApplyMovement()
    {
        // Apply forward/backward movement
        if (isMovingForward)
            transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
        if (isMovingBackward)
            transform.Translate(Vector3.back * moveSpeed * Time.deltaTime);

        // Apply rotation
        if (isRotatingLeft)
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime);
        if (isRotatingRight)
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}