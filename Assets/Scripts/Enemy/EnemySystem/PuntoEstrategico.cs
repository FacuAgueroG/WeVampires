using UnityEngine;

public class PuntoEstrategico : MonoBehaviour {
    [Tooltip("Multiplicador de calor. 1.0 = normal. 2.0 = el doble de prioridad. 0.5 = baja prioridad.")]
    public float pesoProbabilidad = 1.0f;

    [HideInInspector] public EnemyBase ocupante;
    [HideInInspector] public bool estaReservado = false;
}