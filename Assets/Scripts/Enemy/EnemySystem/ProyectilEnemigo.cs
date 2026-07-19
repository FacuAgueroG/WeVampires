using UnityEngine;

public class ProyectilEnemigo : MonoBehaviour {
    [Header("Ajustes Físicos")]
    public float velocidad = 20f;
    public float tiempoDeVida = 5f;

    [Header("Ajustes de Combate")]
    public float dañoBase = 10f; // Ahora lo setea el EnemyBase al instanciar
    [HideInInspector] public bool esAtaqueIntencional = true;

    void Start() {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.linearVelocity = transform.forward * velocidad;
        }
        Destroy(gameObject, tiempoDeVida);
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            // Aplicamos directamente el dañoBase (que ya viene calculado del enemigo)
            PlayerContextTracker tracker = other.GetComponent<PlayerContextTracker>();
            if (tracker != null) {
                tracker.TakeDamage(dañoBase);
            }

            Debug.Log($"Impacto: {(esAtaqueIntencional ? "Directo" : "Errático")} - Daño aplicado: {dañoBase}");
            Destroy(gameObject);
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("platform")) {
            Destroy(gameObject);
        }
    }
}