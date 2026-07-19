using System.Collections.Generic;
using UnityEngine;

public class CentralAI : MonoBehaviour {
    public static CentralAI Instance { get; private set; }

    [Header("Configuración de Arena")]
    [SerializeField] private ArenaEncounter currentEncounter;
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Distancia mínima para spawnear un Fodder cerca del jugador")]
    public float playerExclusionRadius = 10f;

    private List<EnemyBase> activeEnemies = new List<EnemyBase>();
    private int currentQueueIndex = 0;
    private int currentMeleeWeight = 0;

    [Header("Tokens de Ataque (Permisos de Daño)")]
    public int maxHeavyAtaqueTokens = 2;
    public int maxFodderAtaqueTokens = 5;
    private int baseMaxFodderTokens;
    private int currentHeavyAtaqueTokens = 0;
    private int currentFodderAtaqueTokens = 0;

    [Header("Tokens de Movimiento (Engagement)")]
    public int maxHeavyMovimientoTokens = 3;
    public int maxFodderMovimientoTokens = 4;
    private int currentHeavyMovimientoTokens = 0;
    private int currentFodderMovimientoTokens = 0;

    [Header("Metrónomo de la Orquesta")]
    public float globalFodderAttackCD = 0.25f;
    public float globalHeavyAttackCD = 0.75f;

    private float currentFodderCDTimer = 0f;
    private float currentHeavyCDTimer = 0f;

    // --- NUEVO: SISTEMA DE RESPIRO TEMPORAL (BREATHER) ---
    [Header("Sistema de Respiro (Breather)")]
    [Tooltip("Porcentaje de vida donde salta la alarma (ej: 0.25 = 25%)")]
    [Range(0, 1)] public float umbralCritico = 0.25f;
    [Tooltip("Cuántos segundos dura el respiro antes de volver a masacrar")]
    public float tiempoDeRespiro = 4.0f;
    [Tooltip("Tokens maximos durante el respiro. 0 = Nadie ataca. 1 = Ataques muy lentos")]
    public int tokensDuranteRespiro = 0;

    private bool enRespiro = false;
    private float timerRespiro = 0f;

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start() {
        baseMaxFodderTokens = maxFodderAtaqueTokens;

        if (PlayerContextTracker.Instance != null) {
            PlayerContextTracker.Instance.OnHealthPercentageChanged += EvaluarSaludParaRespiro;
        }

        if (currentEncounter != null) InitializeStage0();
    }

    private void Update() {
        // Reducción de los enfriamientos globales
        if (currentFodderCDTimer > 0) currentFodderCDTimer -= Time.deltaTime;
        if (currentHeavyCDTimer > 0) currentHeavyCDTimer -= Time.deltaTime;

        // --- NUEVO: GESTIÓN DEL RELOJ DE RESPIRO ---
        if (enRespiro) {
            timerRespiro -= Time.deltaTime;

            // Si se acaba el tiempo, el jugador pierde su oportunidad de estar a salvo
            if (timerRespiro <= 0) {
                TerminarRespiro("<color=red>IA: FIN DEL RESPIRO (Tiempo agotado). ¡Vuelve la masacre!</color>");
            }
        }
    }

    private void EvaluarSaludParaRespiro(float healthPercentage) {
        // 1. Si la vida baja a nivel crítico y NO estamos ya en respiro: lo activamos
        if (healthPercentage <= umbralCritico && !enRespiro) {
            IniciarRespiro();
        }
        // 2. Si el jugador se cura (agarra orbes) por encima del umbral: le cortamos el respiro anticipadamente
        else if (healthPercentage > umbralCritico && enRespiro) {
            TerminarRespiro("<color=yellow>IA: El jugador se curó. Fin del respiro anticipado.</color>");
        }
    }

    private void IniciarRespiro() {
        enRespiro = true;
        timerRespiro = tiempoDeRespiro;
        maxFodderAtaqueTokens = tokensDuranteRespiro;

        // Failsafe: Limpiamos los tokens actuales para destrabar la cola si estaba bugeada
        currentFodderAtaqueTokens = 0;

        Debug.Log($"<color=cyan>IA: ¡TIEMPO DE RESPIRO! La presión baja por {tiempoDeRespiro} segundos.</color>");
    }

    private void TerminarRespiro(string mensajeDebug) {
        enRespiro = false;
        maxFodderAtaqueTokens = baseMaxFodderTokens; // Volvemos a la dificultad normal

        // Failsafe preventivo
        currentFodderAtaqueTokens = 0;

        Debug.Log(mensajeDebug);
    }

    // --- (A PARTIR DE AQUÍ, EL RESTO DE TU CÓDIGO SE MANTIENE EXACTAMENTE IGUAL) ---

    private void InitializeStage0() {
        for (int i = 0; i < currentEncounter.spawnQueue.Count; i++) {
            if (currentEncounter.spawnQueue[i].isStage0) {
                ExecuteSpawn(currentEncounter.spawnQueue[i]);
                currentQueueIndex = i + 1;
            }
        }
    }

    public void EvaluateNextSpawn() {
        if (currentQueueIndex >= currentEncounter.spawnQueue.Count) return;

        ArenaEncounter.SpawnEntry nextEntry = currentEncounter.spawnQueue[currentQueueIndex];
        EnemyData nextData = nextEntry.enemyData;

        bool hasPopulationSpace = (GetCurrentPopulation() + nextData.populationCost) <= currentEncounter.maxGlobalPopulation;
        bool hasMeleeSpace = (currentMeleeWeight + nextData.meleeWeight) <= currentEncounter.maxMeleeWeight;

        if (hasPopulationSpace && hasMeleeSpace) {
            ExecuteSpawn(nextEntry);
            currentQueueIndex++;
            EvaluateNextSpawn();
        }
    }

    private void ExecuteSpawn(ArenaEncounter.SpawnEntry entry) {
        Transform spawnPos = spawnPoints[entry.spawnPointID];

        if (PlayerContextTracker.Instance != null && entry.enemyData.enemyClass == EnemyClass.Fodder) {
            float distToPlayer = Vector3.Distance(PlayerContextTracker.Instance.transform.position, spawnPos.position);

            if (distToPlayer < playerExclusionRadius) {
                foreach (Transform sp in spawnPoints) {
                    if (Vector3.Distance(PlayerContextTracker.Instance.transform.position, sp.position) >= playerExclusionRadius) {
                        spawnPos = sp;
                        break;
                    }
                }
            }
        }

        GameObject go = Instantiate(entry.enemyData.prefab, spawnPos.position, spawnPos.rotation);
        EnemyBase enemyComponent = go.GetComponent<EnemyBase>();
        enemyComponent.Initialize(entry.enemyData);

        RegisterEnemy(enemyComponent);
    }

    public void OnEnemyDeath(EnemyBase enemy) {
        activeEnemies.Remove(enemy);
        currentMeleeWeight -= enemy.Data.meleeWeight;
        EvaluateNextSpawn();
    }

    private int GetCurrentPopulation() {
        int currentPop = 0;
        foreach (EnemyBase enemy in activeEnemies) currentPop += enemy.Data.populationCost;
        return currentPop;
    }

    public bool CanSpawn(int populationCost) {
        return (GetCurrentPopulation() + populationCost) <= currentEncounter.maxGlobalPopulation;
    }

    public void RegisterEnemy(EnemyBase enemy) {
        activeEnemies.Add(enemy);
        currentMeleeWeight += enemy.Data.meleeWeight;
    }

    public bool SolicitarTokenAtaque(int costo, EnemyClass tipo) {
        if (tipo == EnemyClass.Fodder) {
            if ((currentFodderAtaqueTokens + costo <= maxFodderAtaqueTokens) && currentFodderCDTimer <= 0) {
                currentFodderAtaqueTokens += costo;
                return true;
            }
        }
        else if (tipo == EnemyClass.Heavy) {
            if ((currentHeavyAtaqueTokens + costo <= maxHeavyAtaqueTokens) && currentHeavyCDTimer <= 0) {
                currentHeavyAtaqueTokens += costo;
                return true;
            }
        }
        return false;
    }

    public void DevolverTokenAtaque(int costo, EnemyClass tipo) {
        if (tipo == EnemyClass.Fodder) {
            currentFodderAtaqueTokens = Mathf.Max(0, currentFodderAtaqueTokens - costo);
            currentFodderCDTimer = globalFodderAttackCD;
        }
        else if (tipo == EnemyClass.Heavy) {
            currentHeavyAtaqueTokens = Mathf.Max(0, currentHeavyAtaqueTokens - costo);
            currentHeavyCDTimer = globalHeavyAttackCD;
        }
    }

    public bool SolicitarTokenMovimiento(int costo, EnemyClass tipoEnemigo) {
        if (tipoEnemigo == EnemyClass.Fodder && (currentFodderMovimientoTokens + costo <= maxFodderMovimientoTokens)) {
            currentFodderMovimientoTokens += costo;
            return true;
        }
        if (tipoEnemigo == EnemyClass.Heavy && (currentHeavyMovimientoTokens + costo <= maxHeavyMovimientoTokens)) {
            currentHeavyMovimientoTokens += costo;
            return true;
        }
        return (tipoEnemigo == EnemyClass.SuperHeavy);
    }

    public void DevolverTokenMovimiento(int costo, EnemyClass tipoEnemigo) {
        if (tipoEnemigo == EnemyClass.Fodder) currentFodderMovimientoTokens = Mathf.Max(0, currentFodderMovimientoTokens - costo);
        if (tipoEnemigo == EnemyClass.Heavy) currentHeavyMovimientoTokens = Mathf.Max(0, currentHeavyMovimientoTokens - costo);
    }
}