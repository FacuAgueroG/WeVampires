using UnityEngine;

public class FireballSpell : BaseSpell {
    [Header("Settings")]
    public GameObject fireballPrefab;
    public GameObject normalExplosionVFX;
    public GameObject chargedExplosionVFX;
    public float normalDamage = 10f, normalSpeed = 100f, normalRadius = 3f;
    public float bombDamage = 40f, bombSpeed = 40f, bombRadius = 8f;
    public LayerMask targetLayers;

    protected override void ExecuteAttack(bool isCharged, Transform target) {
        if (currentCharges <= 0) return;
        SpellManager manager = GetComponentInParent<SpellManager>();

        Ray cameraRay = manager.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        bool hasHit = Physics.Raycast(cameraRay, out RaycastHit hit, 100f, targetLayers);
        Vector3 targetPoint = hasHit ? hit.point : cameraRay.GetPoint(100);

        // Nace en la cámara para precisión total
        Vector3 spawnPos = manager.playerCamera.transform.position + manager.playerCamera.transform.forward * 0.8f;
        Vector3 launchDir = (targetPoint - spawnPos).normalized;

        GameObject projObj = Instantiate(fireballPrefab, spawnPos, Quaternion.LookRotation(launchDir));
        FireballProjectile proj = projObj.GetComponent<FireballProjectile>();

        proj.isCharged = isCharged;
        if (isCharged) {
            proj.targetEnemy = null;
            proj.damage = bombDamage;
            proj.speed = bombSpeed;
            proj.explosionRadius = bombRadius;
            proj.explosionVFX = chargedExplosionVFX;
            // Solo escalamos la visual, NO la física
            if (proj.visualModel != null) proj.visualModel.localScale *= 2.5f;
        }
        else {
            proj.targetEnemy = target;
            proj.damage = normalDamage;
            proj.speed = normalSpeed;
            proj.explosionRadius = normalRadius;
            proj.explosionVFX = normalExplosionVFX;
        }

        proj.Setup(manager.shootPoint.position);
        ApplyShotConsumption(manager);
    }

    private void ApplyShotConsumption(SpellManager manager) {
        currentCharges--;
        lastShotTime = Time.time;
        if (currentCharges <= 0) StartCoroutine(ReloadRoutine(manager.UpdateUI));
        manager.UpdateUI();
    }
}