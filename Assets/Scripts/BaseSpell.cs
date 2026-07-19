using UnityEngine;
using System.Collections;

public abstract class BaseSpell : MonoBehaviour {
    [Header("Configuración Base")]
    public string spellName;
    public int maxCharges = 3;
    public float timeBetweenShots = 0.4f;
    public float reloadTime = 1.2f;
    public float minHoldTime = 0.5f;

    protected int currentCharges;
    protected bool isReloading = false;
    protected float lastShotTime;
    protected float holdStartTime;
    protected bool isHolding = false;

    public int CurrentCharges => currentCharges;
    public bool IsReloading => isReloading;

    protected virtual void Start() => currentCharges = maxCharges;

    // AHORA AMBAS ACEPTAN EL TRANSFORM DEL OBJETIVO
    public virtual void OnPointerDown(Transform target) {
        if (isReloading || Time.time < lastShotTime + timeBetweenShots) return;

        if (currentCharges == 1) {
            isHolding = true;
            holdStartTime = Time.time;
        }
        else {
            ExecuteAttack(false, target);
        }
    }

    public virtual void OnPointerUp(Transform target) {


        if (!isHolding) return;
        float holdDuration = Time.time - holdStartTime;
        ExecuteAttack(holdDuration >= minHoldTime, target);
        isHolding = false;
    }

    protected abstract void ExecuteAttack(bool isCharged, Transform target);

    protected IEnumerator ReloadRoutine(System.Action onReloadComplete) {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentCharges = maxCharges;
        isReloading = false;
        onReloadComplete?.Invoke();
    }
}