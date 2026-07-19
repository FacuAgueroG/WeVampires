using UnityEngine;

public class HealthPickup : MonoBehaviour {
    [Tooltip("Cantidad de vida que cura este objeto")]
    public float healAmount = 20f;

    // Este método se dispara automáticamente cuando un "Trigger" toca a otro Collider
    private void OnTriggerEnter(Collider other) {

        // Comprobamos que el que nos tocó sea el Jugador y no un enemigo o una bala
        if (other.CompareTag("Player")) {

            // Buscamos el tracker de vida del jugador
            PlayerContextTracker tracker = other.GetComponent<PlayerContextTracker>();

            if (tracker != null && tracker.currentHealth<100) {
                // Curamos al jugador
                tracker.Heal(healAmount);

                // (Opcional) Aquí podrías instanciar un sonido o partícula antes de destruirlo

                // Destruimos la poción/orbe de la escena para que no la agarre dos veces
                Destroy(gameObject);
            }
        }
    }
}