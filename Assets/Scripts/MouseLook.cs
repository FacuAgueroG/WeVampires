using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook : MonoBehaviour {
    [Header("Settings")]
    public float sensitivity = 0.5f;
    public float smoothing = 1.5f;
    public float verticalClamp = 85f;

    [Header("References")]
    public Transform playerCamera;

    private float xRotation = 0f;
    private Vector2 currentMouseInput;
    private Vector2 smoothedMouseInput;

    private void Start() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update() {
        GetInput();
        ModifyInput();
        ApplyRotation();
    }

    void GetInput() {
        if (Mouse.current != null) {
            currentMouseInput = Mouse.current.delta.ReadValue();
        }
    }

    void ModifyInput() {
        currentMouseInput *= sensitivity;
        smoothedMouseInput = Vector2.Lerp(smoothedMouseInput, currentMouseInput, 1f / smoothing);
    }

    void ApplyRotation() {
        // Rotación Vertical (X)
        xRotation -= smoothedMouseInput.y * Time.deltaTime * 50f;
        xRotation = Mathf.Clamp(xRotation, -verticalClamp, verticalClamp);

        // IMPORTANTE: Solo tocamos X e Y, dejamos que Z sea libre para el Tilt de otros scripts
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, playerCamera.localRotation.eulerAngles.z);

        // Rotación Horizontal (Jugador)
        transform.Rotate(Vector3.up * (smoothedMouseInput.x * Time.deltaTime * 50f));
    }
}