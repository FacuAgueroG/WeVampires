using UnityEngine;

public class EnemyHealth : MonoBehaviour {
    public float currentHealth = 10f;

    // Referencia al cerebro base de nuestro enemigo
    private EnemyBase enemyBase;

    private void Start() {
        // Lo buscamos al nacer
        enemyBase = GetComponent<EnemyBase>();
    }

    public void TakeDamage(float amount) {
        currentHealth -= amount;

        if (currentHealth <= 0) {
            if (enemyBase != null) {
                // Llamamos al método oficial que limpia el punto y avisa a la IA Central
                enemyBase.Die();
            }
            else {
                // Por si acaso usas este script en un objeto destructible que no sea un enemigo
                Destroy(gameObject);
            }
        }
    }
}