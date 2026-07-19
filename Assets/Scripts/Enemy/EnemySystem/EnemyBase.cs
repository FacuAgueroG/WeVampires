using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public enum EnemyState { Spawning, Idle, Wandering, Attacking, Recovering }

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBase : MonoBehaviour {
    public EnemyData Data { get; private set; }
    private PuntoEstrategico puntoAsignado;

    [Header("Referencias")]
    public Transform objetivoActual;
    public GameObject proyectilPrefab;
    public Transform puntoDisparo;

    [Header("Salud y Drops")]
    public float health = 50f;
    public GameObject healthPickupPrefab;
    public Transform healthSpawnPoint;

    [Header("Tiempos y Mini-CDs (Game Feel)")]
    public float tiempoReaccionSpawn = 1.5f;
    public float tiempoRecuperacionAccion = 0.5f;

    [Header("Configuración de Movimiento")]
    public float velocidadCaminar = 2.5f;
    public float velocidadRotacion = 8f;
    public float radioDePaseo = 3.5f;
    public bool permitirCambioPlataforma = false;

    [Header("Configuración de Ataque Coreografiado (Cono)")]
    [Tooltip("Total de proyectiles que salen en una ráfaga.")]
    public int proyectilesTotales = 10;
    [Tooltip("Cuántos de esos proyectiles van EXACTAMENTE al jugador.")]
    public int proyectilesDirigidos = 5;
    [Tooltip("Daño de las balas que te apuntan.")]
    public float dañoProyectilDirigido = 10f;
    [Tooltip("Daño de las balas de 'ruido'.")]
    public float dañoProyectilErratico = 2f;
    [Tooltip("Qué tan abierto es el cono de las balas erráticas (en grados).")]
    public float anguloConoDispersión = 30f;
    public float tiempoEntreBalas = 0.1f;

    private float tiempoAtascadoFailsafe = 0f;

    [Header("Costos de Acciones")]
    public int costoTokenMovimiento = 1;
    public int costoTokenAtaque = 1;

    public float AttackPriority { get; private set; } = 0f;
    public float MovePriority { get; private set; } = 0f;

    private EnemyState currentState = EnemyState.Spawning;
    private NavMeshAgent agent;
    private bool tieneTokenMovimiento = false;

    private Quaternion rotacionObjetivoEstatico;
    private bool estaRotandoHaciaObjetivo = false;

    private void Awake() {
        agent = GetComponent<NavMeshAgent>();
        if (objetivoActual == null) {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) objetivoActual = playerObj.transform;
        }
    }

    public void Initialize(EnemyData data) {
        Data = data;
        if (agent != null) {
            agent.speed = velocidadCaminar;
            agent.updateRotation = false;
        }
        AttackPriority = Random.Range(0f, 2f);
        MovePriority = Random.Range(0f, 2f);
        StartCoroutine(RutinaDespertar());
    }

    public void VincularPunto(PuntoEstrategico punto) { puntoAsignado = punto; }

    private void Update() {
        if (puntoAsignado == null || agent == null) return;
        switch (currentState) {
            case EnemyState.Idle: UpdateIdle(); break;
            case EnemyState.Wandering: UpdateWandering(); break;
            case EnemyState.Attacking: UpdateAttacking(); break; // NUEVO: Ahora actualiza al atacar
        }
    }

    private IEnumerator RutinaDespertar() {
        currentState = EnemyState.Spawning;
        yield return new WaitForSeconds(tiempoReaccionSpawn);
        EntrarEnColaDeEspera();
    }

    private void UpdateIdle() {
        AttackPriority += Time.deltaTime;
        MovePriority += Time.deltaTime;

        if (estaRotandoHaciaObjetivo) {
            transform.rotation = Quaternion.Slerp(transform.rotation, rotacionObjetivoEstatico, Time.deltaTime * velocidadRotacion);
            if (Quaternion.Angle(transform.rotation, rotacionObjetivoEstatico) < 1f) {
                transform.rotation = rotacionObjetivoEstatico;
                estaRotandoHaciaObjetivo = false;
            }
        }
    }

    // --- NUEVA LÓGICA DE SEGUIMIENTO CONTINUO ---
    private void UpdateAttacking() {
        if (objetivoActual == null) return;

        Vector3 dir = (objetivoActual.position - transform.position).normalized;
        dir.y = 0; // Mantenemos el eje Y en 0 para que no se incline hacia el suelo/cielo

        if (dir != Vector3.zero) {
            Quaternion rotDeseada = Quaternion.LookRotation(dir);
            // El enemigo te sigue constantemente con la mirada mientras este estado esté activo
            transform.rotation = Quaternion.Slerp(transform.rotation, rotDeseada, Time.deltaTime * velocidadRotacion);
        }
    }

    public void EjecutarAtaque() {
        AttackPriority = 0f;
        StartCoroutine(SecuenciaAtaqueRafaga());
    }

    public void EjecutarMovimiento() {
        MovePriority = 0f;
        tieneTokenMovimiento = true;
        currentState = EnemyState.Wandering;
        estaRotandoHaciaObjetivo = false;
        MoverAPuntoAleatorio();
    }

    private void EntrarEnColaDeEspera() {
        currentState = EnemyState.Idle;
        FijarMiradaAlObjetivoActual();
        if (Data.enemyClass == EnemyClass.Fodder && FodderQueueManager.Instance != null) {
            FodderQueueManager.Instance.Inscribir(this);
        }
    }

    private IEnumerator SecuenciaAtaqueRafaga() {
        currentState = EnemyState.Attacking; // Al entrar acá, el UpdateAttacking() empieza a rotarlo automáticamente
        float tiempoMaximoApuntado = 1.5f;

        // 1. Fase de "Carga": Solo esperamos a estar lo suficientemente alineados con el jugador
        while (tiempoMaximoApuntado > 0f) {
            Vector3 dir = (objetivoActual.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero && Vector3.Angle(transform.forward, dir) < 5f) {
                break; // Ya lo tenemos en la mira, empezamos a disparar
            }
            tiempoMaximoApuntado -= Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        int dirigidosLanzados = 0;

        // 2. Fase de Disparo: El Update() sigue rotando el cuerpo mientras este bucle dispara las balas
        for (int i = 0; i < proyectilesTotales; i++) {
            bool esDirigido = false;

            if (dirigidosLanzados < proyectilesDirigidos) {
                esDirigido = true;
                dirigidosLanzados++;
            }

            // NUEVO: Calculamos la posición del jugador EN ESTE EXACTO FRAME, no antes del bucle
            Vector3 posicionActualizadaJugador = objetivoActual.position;
            if (objetivoActual.TryGetComponent<Collider>(out Collider col)) {
                posicionActualizadaJugador = col.bounds.center;
            }

            DispararConDispersion(esDirigido, posicionActualizadaJugador);

            // Pausa entre balas. Durante esta pausa de 0.1s, UpdateAttacking() sigue corriendo y ajustando el cuerpo
            yield return new WaitForSeconds(tiempoEntreBalas);
        }

        // 3. Terminamos
        CentralAI.Instance.DevolverTokenAtaque(costoTokenAtaque, Data.enemyClass);
        StartCoroutine(RutinaRecuperacion());
    }

    private void DispararConDispersion(bool esDirigido, Vector3 destino) {
        if (proyectilPrefab == null || puntoDisparo == null) return;

        Vector3 direccionPerfecta = (destino - puntoDisparo.position).normalized;
        if (direccionPerfecta == Vector3.zero) direccionPerfecta = transform.forward;

        Quaternion rotacionFinal = Quaternion.LookRotation(direccionPerfecta);

        if (!esDirigido) {
            float dispersionX = Random.Range(-anguloConoDispersión / 2, anguloConoDispersión / 2);
            float dispersionY = Random.Range(-anguloConoDispersión / 2, anguloConoDispersión / 2);
            rotacionFinal *= Quaternion.Euler(dispersionX, dispersionY, 0);
        }

        GameObject bullet = Instantiate(proyectilPrefab, puntoDisparo.position, rotacionFinal);
        ProyectilEnemigo scriptBala = bullet.GetComponent<ProyectilEnemigo>();

        if (scriptBala != null) {
            scriptBala.dañoBase = esDirigido ? dañoProyectilDirigido : dañoProyectilErratico;
            scriptBala.esAtaqueIntencional = esDirigido;
        }
    }

    private void FijarMiradaAlObjetivoActual() {
        if (objetivoActual == null) return;
        Vector3 dir = (objetivoActual.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero) {
            rotacionObjetivoEstatico = Quaternion.LookRotation(dir);
            estaRotandoHaciaObjetivo = true;
        }
    }

    private void MoverAPuntoAleatorio() {
        Vector3 randomPos = Random.insideUnitSphere * radioDePaseo + puntoAsignado.transform.position;
        NavMeshHit hit;
        int mask = permitirCambioPlataforma ? NavMesh.AllAreas : agent.areaMask;

        if (NavMesh.SamplePosition(randomPos, out hit, radioDePaseo, mask)) {
            agent.updateRotation = true;
            agent.SetDestination(hit.position);
        }
        else {
            if (tieneTokenMovimiento) CentralAI.Instance.DevolverTokenMovimiento(costoTokenMovimiento, Data.enemyClass);
            tieneTokenMovimiento = false;
            StartCoroutine(RutinaRecuperacion());
        }
    }

    private void UpdateWandering() {
        tiempoAtascadoFailsafe += Time.deltaTime;
        if (tiempoAtascadoFailsafe > 4f) { TerminarPaseo(); return; }

        if (!agent.pathPending) {
            if (agent.remainingDistance <= agent.stoppingDistance) TerminarPaseo();
            else if (agent.pathStatus == NavMeshPathStatus.PathInvalid || agent.pathStatus == NavMeshPathStatus.PathPartial) TerminarPaseo();
        }
    }

    private void TerminarPaseo() {
        if (tieneTokenMovimiento) CentralAI.Instance.DevolverTokenMovimiento(costoTokenMovimiento, Data.enemyClass);
        tieneTokenMovimiento = false;
        agent.updateRotation = false;
        if (agent.isOnNavMesh) agent.ResetPath();
        StartCoroutine(RutinaRecuperacion());
    }

    private IEnumerator RutinaRecuperacion() {
        currentState = EnemyState.Recovering;
        yield return new WaitForSeconds(tiempoRecuperacionAccion);
        EntrarEnColaDeEspera();
    }

    public void TakeDamage(float damage) {
        health -= damage;
        if (health <= 0) Die();
    }

    public void Die() {
        if (tieneTokenMovimiento) CentralAI.Instance.DevolverTokenMovimiento(costoTokenMovimiento, Data.enemyClass);
        if (currentState == EnemyState.Attacking) CentralAI.Instance.DevolverTokenAtaque(costoTokenAtaque, Data.enemyClass);

        if (puntoAsignado != null) puntoAsignado.ocupante = null;
        if (CentralAI.Instance != null) CentralAI.Instance.OnEnemyDeath(this);
        if (FodderQueueManager.Instance != null) FodderQueueManager.Instance.Quitar(this);

        if (healthPickupPrefab != null) {
            Instantiate(healthPickupPrefab, healthSpawnPoint.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}