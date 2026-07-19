using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FodderManager : MonoBehaviour {
    [Header("Configuración de Morralla")]
    public EnemyData fodderData;

    [Tooltip("El mínimo global de enemigos Fodder que debe haber vivos en la arena.")]
    public int minimoFodderGlobal = 4;

    [Header("Piscina de Puntos (Pool)")]
    public List<PuntoEstrategico> todosLosPuntos;

    private void Start() {
        // Escaneo periódico para gestionar la población constante de recursos
        InvokeRepeating("CheckYSpawn", 1f, 2f);
    }

    private void CheckYSpawn() {
        // 1. ¿Hay espacio en la cuota global dictada por el Coordinador?
        if (CentralAI.Instance == null || !CentralAI.Instance.CanSpawn(fodderData.populationCost))
            return;

        // 2. Censamos cuántos morrallas hay vivos sumando los ocupantes de todos los puntos
        int cuentaActual = todosLosPuntos.Count(p => p.ocupante != null || p.estaReservado);

        if (cuentaActual < minimoFodderGlobal) {
            var puntosDisponibles = todosLosPuntos.Where(p => p.ocupante == null && !p.estaReservado).ToList();

            if (puntosDisponibles.Count > 0) {
                PuntoEstrategico puntoElegido = CalcularMejorPunto(puntosDisponibles);

                if (puntoElegido != null) {
                    EjecutarSpawnDirecto(puntoElegido);
                }
            }
        }
    }

    private PuntoEstrategico CalcularMejorPunto(List<PuntoEstrategico> disponibles) {
        var posicionesOcupadas = todosLosPuntos
            .Where(p => p.ocupante != null || p.estaReservado)
            .Select(p => p.transform.position)
            .ToList();

        if (posicionesOcupadas.Count == 0) {
            var ordenadosPorPeso = disponibles.OrderByDescending(p => p.pesoProbabilidad).ToList();
            int numFinalistasVacios = Mathf.Min(3, ordenadosPorPeso.Count);
            return ordenadosPorPeso[Random.Range(0, numFinalistasVacios)];
        }

        var puntosConPuntaje = disponibles.Select(p => new {
            Punto = p,
            Score = posicionesOcupadas.Min(pos => Vector3.Distance(p.transform.position, pos)) * p.pesoProbabilidad
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        int numFinalistas = Mathf.Min(3, puntosConPuntaje.Count);
        int indiceElegido = Random.Range(0, numFinalistas);

        return puntosConPuntaje[indiceElegido].Punto;
    }

    private void EjecutarSpawnDirecto(PuntoEstrategico punto) {
        punto.estaReservado = true;

        GameObject nuevoEnemigo = Instantiate(fodderData.prefab, punto.transform.position, punto.transform.rotation);

        EnemyBase enemyComponent = nuevoEnemigo.GetComponent<EnemyBase>();
        enemyComponent.Initialize(fodderData);

        enemyComponent.VincularPunto(punto);
        punto.ocupante = enemyComponent;
        punto.estaReservado = false;

        CentralAI.Instance.RegisterEnemy(enemyComponent);
    }

    private void OnDrawGizmos() {
        if (todosLosPuntos == null) return;
        foreach (var p in todosLosPuntos) {
            if (p == null || p.transform == null) continue;

            Gizmos.color = (p.ocupante != null || p.estaReservado) ? Color.red : Color.green;
            float radioVisual = 0.4f * Mathf.Clamp(p.pesoProbabilidad, 0.5f, 2f);
            Gizmos.DrawSphere(p.transform.position, radioVisual);
        }
    }
}