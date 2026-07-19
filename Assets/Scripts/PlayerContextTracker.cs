using UnityEngine;
using System;

public class PlayerContextTracker : MonoBehaviour {
    public static PlayerContextTracker Instance { get; private set; }

    [Header("Referencias")]
    public Transform playerCamera;
    public Transform respawnPoint; // NUEVO: Arrastra un transform vacío aquí

    [Header("Salud")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Detección de Pasividad")]
    public float minActiveVelocity = 2.0f;
    public float timeToBecomePassive = 2.0f;

    public bool IsPassive { get; private set; }
    public float HealthPercentage => currentHealth / maxHealth;

    // NUEVO: Ahora el evento envía la vida actual y la máxima para la UI
    public event Action<float, float> OnHealthChanged;
    // NUEVO: Evento para la IA (sigue usando porcentaje)
    public event Action<float> OnHealthPercentageChanged;

    private Vector3 lastPosition;
    private float passiveTimer = 0f;

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    private void Start() {
        currentHealth = maxHealth;
        lastPosition = transform.position;
        if (playerCamera == null) playerCamera = Camera.main.transform;
    }

    private void Update() {
        TrackPassivity();
    }

    private void TrackPassivity() {
        float currentSpeed = (transform.position - lastPosition).magnitude / Time.deltaTime;
        lastPosition = transform.position;

        if (currentSpeed < minActiveVelocity) {
            passiveTimer += Time.deltaTime;
            if (passiveTimer >= timeToBecomePassive) IsPassive = true;
        }
        else {
            passiveTimer = 0f;
            IsPassive = false;
        }
    }

    public void TakeDamage(float amount) {
        currentHealth -= amount;

        if (currentHealth <= 0) {
            Respawn();
        }
        else {
            // Avisar a la UI y a la IA
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHealthPercentageChanged?.Invoke(HealthPercentage);
        }
    }

    public void Heal(float amount) {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);

        // Avisar a la UI y a la IA
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthPercentageChanged?.Invoke(HealthPercentage);
    }

    // NUEVO: Lógica de muerte sin destruir al player
    private void Respawn() {
        currentHealth = maxHealth;

        if (respawnPoint != null) {
            // Nota: Si usas CharacterController, debes deshabilitarlo antes de moverlo y luego habilitarlo
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = respawnPoint.position;

            if (cc != null) cc.enabled = true;
        }
        else {
            Debug.LogWarning("No asignaste un Respawn Point en el PlayerContextTracker");
        }

        // Avisar a la UI y a la IA que reviviste
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnHealthPercentageChanged?.Invoke(HealthPercentage);
    }
}