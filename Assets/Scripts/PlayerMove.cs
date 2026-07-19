using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

// --- NUEVA INTERFAZ (Puedes ponerla en su propio archivo IForceReceiver.cs) ---
public interface IForceReceiver {
    void ApplyForce(Vector3 force);
}
// ------------------------------------------------------------------------------

public class PlayerMove : MonoBehaviour, IForceReceiver {
    [Header("Movement Settings")]
    public float maxSpeed = 20f;
    public float acceleration = 15f;
    public float deceleration = 12f; // Lineal para tierra

    [Header("Air Control & Friction (Curve)")]
    public float airControlInput = 0.5f;
    public float airDeceleration = 0.5f;
    [Tooltip("Curva para el frenado orgánico en el AIRE únicamente.")]
    public AnimationCurve airDecelerationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private float airStopTimer = 0f;

    [Header("Jumping & Coyote Time")]
    public float jumpForce = 9f;
    public float gravity = -25f;
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.15f;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private int jumpCount = 0;
    private int maxJumps = 2;
    private bool wasGrounded;
    public Action OnLand;

    [Header("Dash System")]
    public float dashForce = 45f;
    public float dashDuration = 0.2f;
    public int maxDashCharges = 2;
    public float dashCooldown = 1.5f;
    public float timeBetweenDashes = 0.3f;
    public float dashBufferTime = 0.15f;
    private int currentDashCharges;
    private int availableDashCharges;
    private float[] dashRechargeTimers;
    private float interDashTimer;
    private float dashBufferCounter;
    private bool isDashing;

    [Header("Ledge Vaulting (Mantle)")]
    public LayerMask climbableLayer;
    public float vaultSpeed = 18f;
    public float detectionDistance = 0.6f;
    public float ledgeMaxHeight = 2.0f;
    public float ledgeRaySpacing = 0.25f;
    public float mantleConfirmTime = 0.1f;
    private float mantleTimer;
    private bool isVaulting;

    [Header("External Forces")]
    private Vector3 externalImpact = Vector3.zero; // <-- NUEVO

    [Header("References")]
    private CharacterController myCC;
    public Transform playerCamera;

    private Vector2 moveInput;
    private Vector3 currentVelocity;
    private float verticalVelocity;

    void Start() {
        myCC = GetComponent<CharacterController>();
        currentDashCharges = maxDashCharges;
        availableDashCharges = maxDashCharges;
        dashRechargeTimers = new float[maxDashCharges];
    }

    void Update() {
        if (isVaulting) return; // Bloqueo total de lógica durante el vault

        GetInput();
        HandleJumpLogic();
        HandleDashLogic();
        HandleDashRecharge();
        CalculateVelocity();

        if (!myCC.isGrounded && moveInput.y > 0) {
            CheckForLedge();
        }
        else {
            mantleTimer = 0;
        }

        ApplyMovement();

        if (myCC.isGrounded) {
            coyoteTimeCounter = coyoteTime;
            availableDashCharges = currentDashCharges;
            if (!wasGrounded) OnLand?.Invoke();
        }
        else {
            coyoteTimeCounter -= Time.deltaTime;
        }

        wasGrounded = myCC.isGrounded;
        if (interDashTimer > 0) interDashTimer -= Time.deltaTime;
    }

    void GetInput() {
        if (Keyboard.current == null) return;

        moveInput = new Vector2(
            (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
            (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)
        ).normalized;

        if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (Keyboard.current.leftShiftKey.wasPressedThisFrame) dashBufferCounter = dashBufferTime;
        else dashBufferCounter -= Time.deltaTime;
    }

    void HandleJumpLogic() {
        if (coyoteTimeCounter > 0f) {
            jumpCount = 0;
            if (verticalVelocity < 0) verticalVelocity = -2f;
            if (jumpBufferCounter > 0) {
                ExecuteJump();
                jumpBufferCounter = 0;
                coyoteTimeCounter = 0;
                airStopTimer = 0f;
            }
        }
        else {
            if (jumpCount == 0) jumpCount = 1;
            if (Keyboard.current.spaceKey.wasPressedThisFrame && jumpCount < maxJumps) {
                ExecuteJump();
                airStopTimer = 0f;
            }
        }
        verticalVelocity += gravity * Time.deltaTime;
    }

    void ExecuteJump() {
        verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        jumpCount++;
    }

    void HandleDashLogic() {
        bool canDash = availableDashCharges > 0 && !isDashing && moveInput.magnitude > 0 && interDashTimer <= 0;
        if (dashBufferCounter > 0 && canDash) {
            StartCoroutine(ExecuteDash());
            dashBufferCounter = 0;
        }
    }

    IEnumerator ExecuteDash() {
        isDashing = true;
        currentDashCharges--;
        availableDashCharges--;
        interDashTimer = timeBetweenDashes;
        Vector3 dashDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        currentVelocity = dashDir * dashForce;
        verticalVelocity = 0;
        yield return new WaitForSeconds(dashDuration);
        airStopTimer = 0f;
        verticalVelocity = -1f;
        isDashing = false;
    }

    void HandleDashRecharge() {
        for (int i = 0; i < maxDashCharges; i++) {
            if (currentDashCharges < maxDashCharges && i >= currentDashCharges) {
                dashRechargeTimers[i] += Time.deltaTime;
                if (dashRechargeTimers[i] >= dashCooldown) {
                    currentDashCharges++;
                    if (myCC.isGrounded) availableDashCharges = currentDashCharges;
                    dashRechargeTimers[i] = 0;
                }
            }
        }
    }

    void CalculateVelocity() {
        if (isDashing) return;
        Vector3 targetVelocity = (transform.right * moveInput.x + transform.forward * moveInput.y) * maxSpeed;

        if (myCC.isGrounded) {
            airStopTimer = 0f;
            // Tierra: Deceleración Lineal
            float lerpSpeed = moveInput.magnitude > 0 ? acceleration : deceleration;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, lerpSpeed * Time.deltaTime);
        }
        else {
            // Aire: Deceleración Curva
            if (moveInput.magnitude > 0) {
                float bufferMulti = currentVelocity.magnitude > maxSpeed ? 2f : 1f;
                currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, (acceleration * airControlInput / bufferMulti) * Time.deltaTime);
                airStopTimer = 0f;
            }
            else {
                airStopTimer += Time.deltaTime * airDeceleration;
                float curveValue = airDecelerationCurve.Evaluate(airStopTimer);
                currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, curveValue * Time.deltaTime * 10f);
            }
        }
    }

    // --- NUEVO: IMPLEMENTACIÓN DEL KNOCKBACK ---
    public void ApplyForce(Vector3 force) {
        externalImpact += force;
    }

    void ApplyMovement() {
        // Disipar el impacto gradualmente (Fricción del knockback)
        if (externalImpact.magnitude > 0.2f) {
            externalImpact = Vector3.Lerp(externalImpact, Vector3.zero, 5f * Time.deltaTime);
        }
        else {
            externalImpact = Vector3.zero;
        }

        // Sumar el impacto externo al movimiento normal
        Vector3 finalVelocity = currentVelocity + (Vector3.up * verticalVelocity) + externalImpact;
        myCC.Move(finalVelocity * Time.deltaTime);
    }
    // -------------------------------------------

    void CheckForLedge() {
        Vector3 eyeLevel = transform.position + (Vector3.up * playerCamera.localPosition.y);
        Vector3 safeOrigin = eyeLevel - transform.forward * 0.2f;
        RaycastHit hitL, hitR;
        float dist = detectionDistance + 0.2f;
        bool collisionL = Physics.Raycast(safeOrigin - (transform.right * ledgeRaySpacing), transform.forward, out hitL, dist, climbableLayer);
        bool collisionR = Physics.Raycast(safeOrigin + (transform.right * ledgeRaySpacing), transform.forward, out hitR, dist, climbableLayer);

        if (collisionL && collisionR) {
            mantleTimer += Time.deltaTime;
            if (mantleTimer >= mantleConfirmTime) {
                Vector3 midPoint = (hitL.point + hitR.point) / 2f;
                Vector3 ledgeOrigin = midPoint + (Vector3.up * ledgeMaxHeight) + (transform.forward * 0.2f);
                RaycastHit ledgeHit;
                if (Physics.Raycast(ledgeOrigin, Vector3.down, out ledgeHit, ledgeMaxHeight + 0.2f, climbableLayer)) {
                    float r = myCC.radius * 0.9f;
                    if (!Physics.CheckCapsule(ledgeHit.point + Vector3.up * (r + 0.1f), ledgeHit.point + Vector3.up * (myCC.height - r), r, climbableLayer)) {
                        StartCoroutine(Vault(ledgeHit.point));
                        mantleTimer = 0;
                    }
                }
            }
        }
        else mantleTimer = 0;
    }

    IEnumerator Vault(Vector3 targetPos) {
        // --- INICIO PROFESIONAL DEL VAULT ---
        isVaulting = true;
        jumpBufferCounter = 0; // Limpiar buffer de salto
        dashBufferCounter = 0; // Limpiar buffer de dash

        verticalVelocity = 0;
        currentVelocity = Vector3.zero;
        Vector3 start = transform.position;
        Vector3 end = new Vector3(targetPos.x, targetPos.y + (myCC.height / 2f) + 0.05f, targetPos.z);

        float t = 0;
        while (t < 1) {
            t += Time.deltaTime * vaultSpeed;
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        // --- FINAL PROFESIONAL DEL VAULT ---
        jumpCount = 0; // Resetear saltos: el vault cuenta como tocar superficie
        availableDashCharges = currentDashCharges; // Habilitar dashes
        isVaulting = false;
    }

    private void OnDrawGizmosSelected() {
        if (playerCamera == null) return;
        Gizmos.color = Color.cyan;
        Vector3 safeOrigin = (transform.position + Vector3.up * playerCamera.localPosition.y) - transform.forward * 0.2f;
        Gizmos.DrawRay(safeOrigin - (transform.right * ledgeRaySpacing), transform.forward * (detectionDistance + 0.2f));
        Gizmos.DrawRay(safeOrigin + (transform.right * ledgeRaySpacing), transform.forward * (detectionDistance + 0.2f));
    }

    public Vector3 GetCurrentVelocity() => currentVelocity;
    public Vector2 GetMoveInput() => moveInput;
    public bool IsGrounded() => myCC.isGrounded;
    public bool IsDashingPlayer() => isDashing;
}