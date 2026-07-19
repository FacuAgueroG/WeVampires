using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ProwlerEnemy : MonoBehaviour {
    private enum ProwlerState { Chasing, Telegraphing, Leaping, MeleeCombo, Recovering }
    private ProwlerState currentState = ProwlerState.Chasing;

    public enum TacticalRole { Attacker, Interceptor }
    [Header("Táctica de Escuadrón")]
    public TacticalRole currentRole;
    public float predictionTime = 1.5f;

    private static List<ProwlerEnemy> prowlerSquad = new List<ProwlerEnemy>();
    private Vector3 lastTargetPos;
    private Vector3 estimatedTargetVelocity;

    [Header("Configuración de Movimiento")]
    public float moveSpeed = 6f;
    public float pathUpdateDelay = 0.2f;
    private float pathUpdateTimer;
    public float personalSpace = 3.5f;

    [Header("Lógica de Combate General")]
    public float attackCooldown = 3.0f;
    private float attackCooldownTimer;

    [Header("Ataque a Distancia (Salto)")]
    public float minAttackDistance = 5f;
    public float maxAttackDistance = 15f;
    private float currentChosenAttackDistance;
    public float maxLeapDistance = 20f;
    public float leapHeight = 3f;
    public float leapDuration = 0.6f;
    public float landingOffset = 1.0f;

    // --- NUEVO: Tiempos exclusivos del salto ---
    [Tooltip("Tiempo que brilla todo el cuerpo antes de SALTAR.")]
    public float leapTelegraphTime = 0.6f;
    [Tooltip("Tiempo que se queda quieto después de aterrizar del SALTO.")]
    public float leapRecoveryTime = 1.0f;

    [Header("Ataque Melee (Combo)")]
    public float meleeChargeSpeed = 15f;
    public float meleeChargeDuration = 0.15f;
    public float timeBetweenStrikes = 0.2f;
    public float strikeTrackingSpeed = 4f;
    public float comboAbortDistance = 8f;

    // --- NUEVO: Tiempos exclusivos del combo melee ---
    [Tooltip("Tiempo que brillan los brazos antes del PRIMER GOLPE.")]
    public float meleeTelegraphTime = 0.3f;
    [Tooltip("Tiempo que se queda respirando después de terminar el COMBO MELEE.")]
    public float meleeRecoveryTime = 0.6f;

    [Header("Impacto Melee")]
    public float meleeHitRadius = 2.0f;
    public float meleeKnockbackForce = 10f;

    [Header("Impacto de Área (Salto)")]
    public float bullseyeRadius = 1.5f;
    public float leapKnockbackForce = 25f;
    public float splashRadius = 3.5f;

    [Header("Materiales (Visual Feedback)")]
    public Renderer meshRenderer;
    public Renderer[] armRenderers;
    public Material normalMaterial;
    public Material telegraphMaterial;

    private NavMeshAgent agent;
    private Transform target;

    void Start() {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        SetFullBodyMaterial(normalMaterial);
        SetArmsMaterial(normalMaterial);

        if (PlayerContextTracker.Instance != null) {
            target = PlayerContextTracker.Instance.transform;
        }
        else {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (target != null) lastTargetPos = target.position;

        DecidirNuevaDistanciaDeAtaque();

        prowlerSquad.Add(this);
        int attackers = 0;
        foreach (var p in prowlerSquad) {
            if (p != this && p.currentRole == TacticalRole.Attacker) attackers++;
        }

        currentRole = (attackers >= 2) ? TacticalRole.Interceptor : TacticalRole.Attacker;
    }

    private void OnDestroy() {
        if (prowlerSquad.Contains(this)) prowlerSquad.Remove(this);
    }

    void Update() {
        if (target == null) return;

        Vector3 rawVelocity = (target.position - lastTargetPos) / Time.deltaTime;
        estimatedTargetVelocity = Vector3.Lerp(estimatedTargetVelocity, rawVelocity, Time.deltaTime * 5f);
        lastTargetPos = target.position;

        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;

        if (agent.isOnOffMeshLink) return;

        switch (currentState) {
            case ProwlerState.Chasing:
                UpdateChasing();
                break;
        }
    }

    private void UpdateChasing() {
        pathUpdateTimer -= Time.deltaTime;

        if (pathUpdateTimer <= 0f) {
            if (currentRole == TacticalRole.Attacker) {
                agent.stoppingDistance = personalSpace;
                agent.SetDestination(target.position);
            }
            else {
                agent.stoppingDistance = personalSpace;
                Vector3 interceptPoint = target.position + (estimatedTargetVelocity * predictionTime);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(interceptPoint, out hit, 5f, NavMesh.AllAreas)) {
                    agent.SetDestination(hit.position);
                }
                else {
                    agent.SetDestination(target.position);
                }
            }
            pathUpdateTimer = pathUpdateDelay;
        }

        float distToPlayer = Vector3.Distance(transform.position, target.position);
        if (distToPlayer < personalSpace && !agent.isStopped && agent.hasPath) {
            agent.velocity = Vector3.zero;
        }

        if (currentRole == TacticalRole.Interceptor) {
            if (distToPlayer <= maxAttackDistance) {
                EjecutarRelevoTactico();
            }
        }

        if (currentRole == TacticalRole.Attacker && !agent.pathPending && attackCooldownTimer <= 0f && agent.hasPath) {
            if (distToPlayer <= personalSpace + 1.0f) {
                StartCoroutine(ExecuteMeleeCombo());
            }
            else if (distToPlayer <= currentChosenAttackDistance + 0.5f) {
                StartCoroutine(ExecuteLeapAttack());
            }
        }
    }

    private void EjecutarRelevoTactico() {
        currentRole = TacticalRole.Attacker;
        ProwlerEnemy furthestAttacker = null;
        float maxDist = 0f;

        foreach (var prowler in prowlerSquad) {
            if (prowler != this && prowler.currentRole == TacticalRole.Attacker) {
                float dist = Vector3.Distance(prowler.transform.position, target.position);
                if (dist > maxDist) {
                    maxDist = dist;
                    furthestAttacker = prowler;
                }
            }
        }

        if (furthestAttacker != null) {
            furthestAttacker.currentRole = TacticalRole.Interceptor;
            furthestAttacker.pathUpdateTimer = 0f;
        }
    }

    private IEnumerator ExecuteMeleeCombo() {
        currentState = ProwlerState.MeleeCombo;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        SetArmsMaterial(telegraphMaterial);
        float t = 0;

        // --- USANDO EL TIEMPO SEPARADO DE MELEE ---
        while (t < meleeTelegraphTime) {
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.1f) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }
            t += Time.deltaTime;
            yield return null;
        }

        int strikeCount = Random.Range(1, 4);

        for (int i = 0; i < strikeCount; i++) {
            float currentDist = Vector3.Distance(transform.position, target.position);
            if (currentDist > comboAbortDistance) {
                Debug.Log("<color=grey>Prowler: Abortando combo, jugador fuera de alcance.</color>");
                break;
            }

            float dashTimer = 0f;
            while (dashTimer < meleeChargeDuration) {
                Vector3 trackingDir = target.position - transform.position;
                trackingDir.y = 0;
                if (trackingDir.sqrMagnitude > 0.1f) {
                    Quaternion targetRot = Quaternion.LookRotation(trackingDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * strikeTrackingSpeed);
                }

                agent.Move(transform.forward * meleeChargeSpeed * Time.deltaTime);

                dashTimer += Time.deltaTime;
                yield return null;
            }

            Vector3 hitCenter = transform.position + transform.forward * personalSpace;
            Collider[] hitColliders = Physics.OverlapSphere(hitCenter, meleeHitRadius);

            bool hitPlayer = false;
            foreach (var col in hitColliders) {
                IForceReceiver forceReceiver = col.GetComponent<IForceReceiver>();
                if (forceReceiver != null && col.transform == target) {
                    Vector3 knockbackDir = transform.forward;
                    knockbackDir.y = 0.2f;
                    forceReceiver.ApplyForce(knockbackDir * meleeKnockbackForce);
                    hitPlayer = true;
                }
            }

            if (hitPlayer) Debug.Log("<color=cyan>Prowler: ¡Golpe Melee conectado!</color>");

            if (i < strikeCount - 1) {
                yield return new WaitForSeconds(timeBetweenStrikes);
            }
        }

        SetArmsMaterial(normalMaterial);
        currentState = ProwlerState.Recovering;

        // --- USANDO EL RECOVERY EXCLUSIVO DE MELEE ---
        yield return new WaitForSeconds(meleeRecoveryTime);

        DecidirNuevaDistanciaDeAtaque();
        attackCooldownTimer = attackCooldown;
        currentState = ProwlerState.Chasing;
    }

    private IEnumerator ExecuteLeapAttack() {
        currentState = ProwlerState.Telegraphing;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        SetFullBodyMaterial(telegraphMaterial);

        float telegraphTimer = 0f;

        // --- USANDO EL TIEMPO SEPARADO DE SALTO ---
        while (telegraphTimer < leapTelegraphTime) {
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.1f) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }
            telegraphTimer += Time.deltaTime;
            yield return null;
        }

        currentState = ProwlerState.Leaping;
        Vector3 predictedPos = target.position + (estimatedTargetVelocity * leapDuration);
        Vector3 leapDirection = (predictedPos - transform.position).normalized;
        leapDirection.y = 0;
        Vector3 landingSpot = predictedPos - (leapDirection * personalSpace);

        if (Vector3.Distance(transform.position, landingSpot) > maxLeapDistance) {
            Vector3 safeDir = (landingSpot - transform.position).normalized;
            landingSpot = transform.position + (safeDir * maxLeapDistance);
        }

        agent.enabled = false;

        Vector3 startPos = transform.position;
        float timePassed = 0f;

        while (timePassed < leapDuration) {
            timePassed += Time.deltaTime;
            float normalizedTime = timePassed / leapDuration;

            Vector3 targetPos = Vector3.Lerp(startPos, landingSpot, normalizedTime);
            float parabola = 4.0f * leapHeight * normalizedTime * (1 - normalizedTime);
            targetPos.y = Mathf.Lerp(startPos.y, landingSpot.y, normalizedTime) + parabola;

            transform.position = targetPos;

            Vector3 airDir = target.position - transform.position;
            airDir.y = 0;
            if (airDir.sqrMagnitude > 0.1f) {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(airDir), Time.deltaTime * 5f);
            }

            yield return null;
        }

        transform.position = landingSpot;
        agent.enabled = true;

        float distAterrizaje = Vector3.Distance(transform.position, target.position);

        if (distAterrizaje <= bullseyeRadius) {
            Debug.Log("<color=red>Prowler: BULLSEYE! (Daño Maximo + Knockback)</color>");
            IForceReceiver forceReceiver = target.GetComponent<IForceReceiver>();
            if (forceReceiver != null) {
                Vector3 knockbackDir = (target.position - transform.position).normalized;
                knockbackDir.y = 0.5f;
                forceReceiver.ApplyForce(knockbackDir * leapKnockbackForce);
            }
        }
        else if (distAterrizaje <= splashRadius) {
            Debug.Log("<color=orange>Prowler: SPLASH! (Solo Daño Menor)</color>");
        }

        currentState = ProwlerState.Recovering;
        SetFullBodyMaterial(normalMaterial);

        // --- USANDO EL RECOVERY EXCLUSIVO DE SALTO ---
        yield return new WaitForSeconds(leapRecoveryTime);

        DecidirNuevaDistanciaDeAtaque();
        attackCooldownTimer = attackCooldown;
        currentState = ProwlerState.Chasing;
    }

    private void DecidirNuevaDistanciaDeAtaque() {
        float minVal = Mathf.Max(personalSpace, minAttackDistance);
        currentChosenAttackDistance = Random.Range(minVal, maxAttackDistance);
    }

    private void SetFullBodyMaterial(Material mat) {
        if (meshRenderer != null) meshRenderer.material = mat;
    }

    private void SetArmsMaterial(Material mat) {
        if (armRenderers != null && armRenderers.Length > 0) {
            foreach (var arm in armRenderers) {
                if (arm != null) arm.material = mat;
            }
        }
        else if (meshRenderer != null) {
            meshRenderer.material = mat;
        }
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.6f);
        Gizmos.DrawWireSphere(transform.position, bullseyeRadius);

        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, splashRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentChosenAttackDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, personalSpace);

        if (Application.isPlaying && currentState == ProwlerState.MeleeCombo) {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + transform.forward * personalSpace, meleeHitRadius);
        }
    }
}