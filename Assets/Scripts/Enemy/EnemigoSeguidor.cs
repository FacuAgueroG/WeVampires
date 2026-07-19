using UnityEngine;
using UnityEngine.AI; // Imprescindible para usar NavMesh

public class EnemigoSeguidor : MonoBehaviour {
    private NavMeshAgent agente;
    public Transform objetivo;
    public float velocidadPersonalizada = 3.5f;

    void Start() {
        agente = GetComponent<NavMeshAgent>();

        // Si no asignaste el objetivo en el inspector, lo buscamos por Tag
        if (objetivo == null) {
            objetivo = GameObject.FindGameObjectWithTag("player").transform;
        }
    }

    void Update() {
        if (objetivo != null) {
            // Ajustamos la velocidad por si quieres cambiarla en tiempo real
            agente.speed = velocidadPersonalizada;

            // Le decimos al agente a dónde ir
            agente.SetDestination(objetivo.position);
        }
    }
}