using UnityEngine;
using UnityEngine.UI;
using TMPro; // Asumo que usas TextMeshPro por las carpetas en tu repo

public class HealthUI : MonoBehaviour {
    [Header("Referencias UI")]
    public Image healthBarFill; // Arrastra tu imagen verde aquí
    public TextMeshProUGUI healthText; // Arrastra tu texto de vida aquí

    private void Start() {
        // Nos suscribimos al evento de vida del jugador para que avise a la UI cuando cambie
        if (PlayerContextTracker.Instance != null) {
            PlayerContextTracker.Instance.OnHealthChanged += ActualizarUI;

            // Forzamos una actualización inicial
            ActualizarUI(PlayerContextTracker.Instance.currentHealth, PlayerContextTracker.Instance.maxHealth);
        }
    }

    private void ActualizarUI(float currentHealth, float maxHealth) {
        // Actualizamos la barra verde (valor entre 0 y 1)
        if (healthBarFill != null) {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }

        // Actualizamos el número (ej: "100")
        if (healthText != null) {
            healthText.text = Mathf.CeilToInt(currentHealth).ToString();
        }
    }

    private void OnDestroy() {
        // Limpieza de memoria (buena práctica)
        if (PlayerContextTracker.Instance != null) {
            PlayerContextTracker.Instance.OnHealthChanged -= ActualizarUI;
        }
    }
}