using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Combat System/Enemy Data")]
public class EnemyData : ScriptableObject {
    [Header("Identificación")]
    public string enemyName;
    public GameObject prefab;

    [Header("Clasificación de Rol")]
    public EnemyClass enemyClass;
    public bool isFlyer;

    [Header("Pesos y Presupuestos")]
    [Tooltip("Cuánto consume del límite de población global de la arena.")]
    public int populationCost = 1;

    [Tooltip("El 'peso' físico. Enemigos Melee pesados deben tener un valor mayor a 0 para no bloquear al jugador.")]
    public int meleeWeight = 0;
}

public enum EnemyClass {
    Fodder,
    Heavy,
    SuperHeavy
}