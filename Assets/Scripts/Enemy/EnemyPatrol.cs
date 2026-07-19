using UnityEngine;

public class EnemyPatrol : MonoBehaviour {
    [Header("Referencias de Movimiento")]
    [Tooltip("El primer punto al que irá el enemigo (Punto A)")]
    [SerializeField] private Transform pointA;
    [Tooltip("El segundo punto al que irá el enemigo (Punto B)")]
    [SerializeField] private Transform pointB;

    [Header("Configuración")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float arrivalThreshold = 0.1f;

    private Transform _currentTarget;
    private bool _isMovingToB = false;

    private void Start() {
        // Validamos que los puntos existan para evitar errores de referencia nula
        if (pointA == null || pointB == null) {
            Debug.LogError("Faltan los puntos de referencia A o B en el script de patrulla.");
            enabled = false;
            return;
        }

        // El enemigo empieza yendo hacia el Punto A (desde su posición actual C)
        _currentTarget = pointA;
    }

    private void Update() {
        MoveEnemy();
        RotateTowardsTarget();
        CheckDestination();
    }

    private void MoveEnemy() {
        // Movimiento linear constante y fluido
        transform.position = Vector3.MoveTowards(
            transform.position,
            _currentTarget.position,
            moveSpeed * Time.deltaTime
        );
    }

    private void RotateTowardsTarget() {
        // Rotación profesional usando Interpolación Lineal (Lerp) para evitar giros bruscos
        Vector3 direction = (_currentTarget.position - transform.position).normalized;
        if (direction != Vector3.zero) {
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z)); // Mantiene el enemigo nivelado
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void CheckDestination() {
        // Usamos sqrMagnitude para optimizar el rendimiento (evita el cálculo de raíz cuadrada de Distance)
        float distanceSqr = (transform.position - _currentTarget.position).sqrMagnitude;

        if (distanceSqr < arrivalThreshold * arrivalThreshold) {
            SwitchTarget();
        }
    }

    private void SwitchTarget() {
        // Alterna entre el punto A y el punto B
        _isMovingToB = !_isMovingToB;
        _currentTarget = _isMovingToB ? pointB : pointA;
    }
}