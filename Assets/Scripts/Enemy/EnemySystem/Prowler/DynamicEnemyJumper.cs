using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DynamicEnemyJumper : MonoBehaviour {
    [Header("Configuración de Salto")]
    [Tooltip("Altura extra que se suma al arco del salto.")]
    public float jumpHeight = 2.0f;
    [Tooltip("Duración total del salto en segundos.")]
    public float jumpDuration = 0.5f;

    private NavMeshAgent agent;
    private bool isJumping = false;

    void Start() {
        agent = GetComponent<NavMeshAgent>();
        agent.autoTraverseOffMeshLink = false;
    }

    void Update() {
        if (agent.isOnOffMeshLink && !isJumping) {
            StartCoroutine(PerformDynamicJump());
        }
    }

    private IEnumerator PerformDynamicJump() {
        isJumping = true;

        // Usamos EXACTAMENTE los puntos que Unity sabe que son seguros
        OffMeshLinkData data = agent.currentOffMeshLinkData;
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = data.endPos + Vector3.up * agent.baseOffset;

        float timePassed = 0f;

        while (timePassed < jumpDuration) {
            timePassed += Time.deltaTime;
            float normalizedTime = timePassed / jumpDuration;

            // Movimiento lineal en X y Z
            Vector3 targetPos = Vector3.Lerp(startPos, endPos, normalizedTime);

            // Cálculo de parábola súper estable
            float heightOffset = jumpHeight * 4.0f * (normalizedTime - normalizedTime * normalizedTime);
            targetPos.y = Mathf.Lerp(startPos.y, endPos.y, normalizedTime) + heightOffset;

            agent.transform.position = targetPos;

            // Rotación suave mirando hacia el destino
            Vector3 direction = endPos - startPos;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f) {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                agent.transform.rotation = Quaternion.Slerp(agent.transform.rotation, targetRotation, Time.deltaTime * 10f);
            }

            yield return null;
        }

        // Aterrizaje seguro
        agent.transform.position = endPos;
        agent.CompleteOffMeshLink();
        isJumping = false;
    }
}