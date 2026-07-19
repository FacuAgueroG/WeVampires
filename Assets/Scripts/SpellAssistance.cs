using UnityEngine;
using System.Collections.Generic;

public class SpellAssistance : MonoBehaviour {
    [Header("Asistencia por Ángulo")]
    [Tooltip("Ángulo máximo desde el centro de la mira para ayudar (ej. 15 grados)")]
    public float maxAssistanceAngle = 15f;
    public float maxDistance = 60f;
    public LayerMask enemyLayer;

    public Transform GetIntendedTarget(Camera cam) {
        // Buscamos a todos los enemigos en el área general
        Collider[] potentials = Physics.OverlapSphere(cam.transform.position, maxDistance, enemyLayer);

        Transform bestTarget = null;
        float smallestAngle = maxAssistanceAngle; // Empezamos con el límite máximo permitido

        foreach (var col in potentials) {
            Vector3 dirToEnemy = (col.transform.position - cam.transform.position).normalized;

            // Calculamos el ángulo exacto entre donde miro y donde está el enemigo
            float angleToEnemy = Vector3.Angle(cam.transform.forward, dirToEnemy);

            // REGLA 1: Solo si está dentro de nuestro cono de visión (Crosshair)
            if (angleToEnemy < maxAssistanceAngle) {
                // REGLA 2: Prioridad absoluta al que esté más cerca del CENTRO (menor ángulo)
                if (angleToEnemy < smallestAngle) {
                    // REGLA 3: Verificar que no haya una pared en medio
                    if (!Physics.Linecast(cam.transform.position, col.transform.position, out _, 1 << 0)) { // 1 << 0 es la capa Default/Escenario
                        smallestAngle = angleToEnemy;
                        bestTarget = col.transform;
                    }
                }
            }
        }
        return bestTarget;
    }
}