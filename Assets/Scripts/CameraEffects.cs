using UnityEngine;

public class CameraEffects : MonoBehaviour {
    [Header("References")]
    public PlayerMove playerMovement;
    private Camera mainCam;

    [Header("Dynamic FOV")]
    public float baseFOV = 90f;
    public float maxFOV = 110f;
    public float fovSpeed = 8f;

    [Header("Tilt & Sway")]
    public float tiltAmount = 3f;
    public float tiltSpeed = 5f;

    [Header("Procedural Bobbing (Sway)")]
    public float bobSpeed = 10f;
    public float bobAmount = 0.05f;
    private float bobTimer;

    [Header("Landing Impact")]
    public float landAmount = 0.2f;
    public float landSpeed = 10f;
    private float currentLandImpact;

    private Vector3 originalLocalPos;

    void Start() {
        mainCam = GetComponent<Camera>();
        originalLocalPos = transform.localPosition;

        // Nos suscribimos al evento de aterrizaje del Player
        if (playerMovement != null) playerMovement.OnLand += ApplyLandImpact;
    }

    void Update() {
        if (playerMovement == null) return;

        HandleDynamicFOV();
        HandleTilt();
        HandleSway();

        // Recuperación suave del impacto de caída
        currentLandImpact = Mathf.Lerp(currentLandImpact, 0, landSpeed * Time.deltaTime);
        transform.localPosition = originalLocalPos + (Vector3.down * currentLandImpact);
    }

    void HandleDynamicFOV() {
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, playerMovement.GetCurrentVelocity().magnitude / playerMovement.dashForce);
        mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, fovSpeed * Time.deltaTime);
    }

    void HandleTilt() {
        float targetZ = -playerMovement.GetMoveInput().x * tiltAmount;
        float currentZ = Mathf.LerpAngle(transform.localEulerAngles.z, targetZ, tiltSpeed * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(transform.localEulerAngles.x, transform.localEulerAngles.y, currentZ);
    }

    void HandleSway() {
        // El balanceo solo ocurre si nos movemos y estamos en el suelo
        if (playerMovement.GetMoveInput().magnitude > 0.1f && playerMovement.IsGrounded()) {
            bobTimer += Time.deltaTime * bobSpeed;
            // Curva en "8" usando Seno y Coseno
            float xOffset = Mathf.Cos(bobTimer) * bobAmount;
            float yOffset = Mathf.Sin(bobTimer * 2f) * bobAmount;

            Vector3 targetPos = originalLocalPos + new Vector3(xOffset, yOffset, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * bobSpeed);
        }
        else {
            bobTimer = 0;
            transform.localPosition = Vector3.Lerp(transform.localPosition, originalLocalPos, Time.deltaTime * bobSpeed);
        }
    }

    void ApplyLandImpact() => currentLandImpact = landAmount;

    private void OnDestroy() {
        if (playerMovement != null) playerMovement.OnLand -= ApplyLandImpact;
    }
}