using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class SpellManager : MonoBehaviour {
    public BaseSpell currentSpell;
    public Transform shootPoint;
    public Camera playerCamera;
    public TextMeshProUGUI ammoText;
    public PlayerMove playerMove;

    [Header("Asistencia de Apuntado")]
    public float aimRadius = 2.5f; // Radio del "imán" de la mira
    public LayerMask enemyLayer;

    void Update() {
        if (currentSpell == null) return;

        // Obtenemos el objetivo del asistente antes de disparar
        SpellAssistance assistance = GetComponent<SpellAssistance>();
        Transform target = assistance != null ? assistance.GetIntendedTarget(playerCamera) : null;

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            currentSpell.OnPointerDown(target);
        }
        if (Mouse.current.leftButton.wasReleasedThisFrame) {
            currentSpell.OnPointerUp(target);
        }
    }

    Transform GetTarget() {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        // El SphereCast es como un cilindro grueso, mucho más fácil de acertar
        if (Physics.SphereCast(ray, aimRadius, out RaycastHit hit, 100f, enemyLayer)) {
            if (hit.collider.GetComponent<EnemyHealth>()) return hit.transform;
        }
        return null;
    }

    public void UpdateUI() {
        if (ammoText == null || currentSpell == null) return;
        ammoText.text = currentSpell.IsReloading ? "RECARGA" : $"Municion: {currentSpell.CurrentCharges} / {currentSpell.maxCharges}";
    }
}