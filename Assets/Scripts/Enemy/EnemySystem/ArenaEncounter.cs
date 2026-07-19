using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewArenaEncounter", menuName = "Combat System/Arena Encounter")]
public class ArenaEncounter : ScriptableObject {
    [System.Serializable]
    public struct SpawnEntry {
        public EnemyData enemyData;
        public int spawnPointID; // ID o índice del Transform donde debe aparecer
        [Tooltip("Si es True, este enemigo ya está en la arena cuando el jugador entra (Stage 0).")]
        public bool isStage0;
    }

    [Header("Límites de la Arena")]
    [Tooltip("Límite máximo de agentes activos al mismo tiempo (ej: 12-16).")]
    public int maxGlobalPopulation = 16;

    [Tooltip("Límite de peso Melee para evitar asfixiar físicamente al jugador.")]
    public int maxMeleeWeight = 6;

    [Header("Secuencia de Spawn")]
    public List<SpawnEntry> spawnQueue;
}