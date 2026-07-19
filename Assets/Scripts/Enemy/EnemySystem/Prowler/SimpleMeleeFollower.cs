using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimpleMeleeFollower : MonoBehaviour {
    private NavMeshAgent agent;
    private Transform target;

    [Tooltip("Cada cuántos segundos recalcula la ruta hacia el jugador")]
    public float pathUpdateDelay = 0.2f;
    private float pathUpdateTimer;

    void Start() {
        agent = GetComponent<NavMeshAgent>();

        // Buscamos al jugador usando tu Singleton para no gastar recursos
        if (PlayerContextTracker.Instance != null) {
            target = PlayerContextTracker.Instance.transform;
        }
        else {
            GameObject p = GameObject.FindGameObjectWithTag("player");
            if (p != null) target = p.transform;
        }
    }

    void Update() {
        if (target == null) return;

        // CRÍTICO: Si está atravesando un link de salto, NO le tocamos el destino
        // para que tu script DynamicEnemyJumper pueda hacer su magia con la parábola.
        if (agent.isOnOffMeshLink) return;

        // Actualizamos la ruta cada cierto tiempo, no todos los frames (optimización básica)
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0f) {
            agent.SetDestination(target.position);
            pathUpdateTimer = pathUpdateDelay;
        }
    }
}