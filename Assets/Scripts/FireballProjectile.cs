using UnityEngine;

public class FireballProjectile : MonoBehaviour {
    [HideInInspector] public float damage, speed;
    [HideInInspector] public bool isCharged;
    [HideInInspector] public Transform targetEnemy;

    [Header("Visuals & VFX")]
    public Transform visualModel;
    public float convergenceSpeed = 15f;
    public GameObject explosionVFX;

    [Header("Collision & Damage")]
    public float detectionRadius = 0.3f; // Radio del barrido (ajustado a la esfera visual)
    public float explosionRadius = 5f;
    public LayerMask hitLayers; // ¡IMPORTANTE! Aquí debe estar Todo: Enemigos, Suelo y Default

    [Header("Physics")]
    public float gravityForce = 20f;
    private float verticalVelocity;
    private bool exploded = false;

    private Rigidbody rb;
    private Vector3 visualOffset;
    private float spawnTime;

    public void Setup(Vector3 handPosition) {
        visualOffset = handPosition - transform.position;
    }

    void Start() {
        spawnTime = Time.time;
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = transform.forward * speed;
        verticalVelocity = rb.linearVelocity.y;
        Destroy(gameObject, 5f);
    }

    void FixedUpdate() {
        if (exploded) return;

        Vector3 currentPos = transform.position;
        float stepDistance = rb.linearVelocity.magnitude * Time.fixedDeltaTime;

        // 1. MOVIMIENTO
        if (isCharged) {
            verticalVelocity -= gravityForce * Time.fixedDeltaTime;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, verticalVelocity, rb.linearVelocity.z);
        }
        else if (targetEnemy != null) {
            Vector3 desiredDir = (targetEnemy.position - currentPos).normalized;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredDir * speed, 5f * Time.fixedDeltaTime);
        }
        transform.forward = rb.linearVelocity.normalized;

        // 2. DETECCIÓN CONTINUA (Evita el Tunelismo)
        // Lanzamos el barrido contra TODO lo que esté en hitLayers
        if (Physics.SphereCast(currentPos, detectionRadius, transform.forward, out RaycastHit hit, stepDistance, hitLayers)) {
            // SEGURIDAD: Evitar colisión con el propio jugador al nacer
            if (Time.time < spawnTime + 0.05f && hit.collider.CompareTag("Player")) return;

            // Movemos la bala exactamente al punto de choque antes de explotar
            transform.position = hit.point;
            Explode();
            return;
        }

        // 3. CONVERGENCIA VISUAL
        if (visualModel != null) {
            visualOffset = Vector3.Lerp(visualOffset, Vector3.zero, Time.fixedDeltaTime * convergenceSpeed);
            visualModel.localPosition = visualOffset;
        }
    }

    // Ya no usamos OnTriggerEnter, el SphereCast es mucho más preciso para altas velocidades
    public void Explode() {
        if (exploded) return;
        exploded = true;

        if (explosionVFX != null) Instantiate(explosionVFX, transform.position, Quaternion.identity);

        // DAÑO: Buscamos enemigos en el radio de la explosión
        Collider[] targets = Physics.OverlapSphere(transform.position, explosionRadius, hitLayers);
        foreach (Collider col in targets) {
            // Buscamos el componente de vida en el enemigo
            if (col.TryGetComponent(out EnemyHealth h)) {
                h.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }
}