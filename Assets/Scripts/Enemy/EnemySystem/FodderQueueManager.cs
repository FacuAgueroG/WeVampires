using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ColaEntry {
    public EnemyBase enemigo;
    public float PuntosPrioridad => enemigo != null ? enemigo.AttackPriority : 0f;

    public ColaEntry(EnemyBase e) {
        enemigo = e;
    }
}

public class FodderQueueManager : MonoBehaviour {
    public static FodderQueueManager Instance { get; private set; }

    [Header("Cola Dinámica (Visible en Play)")]
    public List<ColaEntry> colaDeEspera = new List<ColaEntry>();

    [Header("Configuración Off-Screen")]
    public float offScreenPenalty = 0.1f; // Penalización por estar a la espalda

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Inscribir(EnemyBase enemigo) {
        if (!colaDeEspera.Exists(x => x.enemigo == enemigo)) {
            colaDeEspera.Add(new ColaEntry(enemigo));
        }
    }

    public void Quitar(EnemyBase enemigo) {
        colaDeEspera.RemoveAll(x => x.enemigo == enemigo);
    }

    private void Update() {
        if (colaDeEspera.Count == 0) return;

        // 1. Limpieza de seguridad
        colaDeEspera.RemoveAll(x => x.enemigo == null);

        // --- NUEVO: OBTENER CONTEXTO DEL JUGADOR ---
        Transform cam = PlayerContextTracker.Instance != null ? PlayerContextTracker.Instance.playerCamera : null;
        bool isPassive = PlayerContextTracker.Instance != null && PlayerContextTracker.Instance.IsPassive;

        // 2. Ordenamos al vuelo usando la Prioridad Efectiva (FOV + Pasividad)
        colaDeEspera.Sort((a, b) =>
            CalcularPrioridadEfectiva(b.enemigo, cam, isPassive).CompareTo(
            CalcularPrioridadEfectiva(a.enemigo, cam, isPassive)));

        bool tokenAtaqueOtorgado = false;

        // 3. Evaluamos la fila
        for (int i = 0; i < colaDeEspera.Count; i++) {
            EnemyBase candidato = colaDeEspera[i].enemigo;

            if (!tokenAtaqueOtorgado && CentralAI.Instance.SolicitarTokenAtaque(candidato.costoTokenAtaque, EnemyClass.Fodder)) {
                colaDeEspera.RemoveAt(i);
                candidato.EjecutarAtaque();
                tokenAtaqueOtorgado = true;
                i--;
                continue;
            }

            if (CentralAI.Instance.SolicitarTokenMovimiento(candidato.costoTokenMovimiento, EnemyClass.Fodder)) {
                colaDeEspera.RemoveAt(i);
                candidato.EjecutarMovimiento();
                i--;
            }
        }
    }

    // --- NUEVO: LÓGICA OFF-SCREEN ---
    private float CalcularPrioridadEfectiva(EnemyBase enemy, Transform cam, bool isPassive) {
        float basePriority = enemy.AttackPriority;
        if (cam == null) return basePriority; // Failsafe

        Vector3 dirToEnemy = (enemy.transform.position - cam.position).normalized;
        float dotProduct = Vector3.Dot(cam.forward, dirToEnemy);

        bool isOffScreen = dotProduct < 0; // Si es negativo, está detrás



        // Si está a la espalda y el jugador NO está siendo pasivo, se hunde en la cola
        if (isOffScreen && !isPassive) {
            Debug.Log($"<color=orange>PUNTUACIÓN HUNDIDA:</color> {enemy.name} está a tu espalda. Prioridad bajó de {basePriority} a {basePriority * offScreenPenalty}");
            return basePriority * offScreenPenalty;
        }
        else if (isOffScreen && isPassive) {
            Debug.Log($"<color=red>CASTIGO POR PASIVIDAD:</color> Estás quieto! {enemy.name} ataca por la espalda con máxima prioridad: {basePriority}");
        }

        return basePriority;
    }
}