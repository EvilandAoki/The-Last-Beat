using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TheLastBeat.Enemies
{
    /// <summary>
    /// Pool de demonios soldado (tarea 14), con UnityEngine.Pool.ObjectPool. Crea los soldados al cargar la escena
    /// (Prewarm) y después solo los activa y desactiva: ningún Instantiate ni Destroy durante el combate.
    /// Al sacar uno, EnemySoldier.ResetForSpawn le devuelve la vida, el estado y la posición; al terminar de hundirse
    /// tras morir avisa con Released y vuelve aquí solo.
    /// </summary>
    public class EnemyPool : MonoBehaviour
    {
        [SerializeField] private EnemySoldier prefab;
        [Tooltip("Cuántos se crean al empezar. Debe cubrir el máximo de enemigos vivos a la vez (y la prueba de 30).")]
        [SerializeField, Min(0)] private int prewarm = 30;
        [Tooltip("Máximo que se guardan inactivos; si se pasa, se destruyen los sobrantes.")]
        [SerializeField, Min(1)] private int maxSize = 60;

        private ObjectPool<EnemySoldier> pool;
        private int created;

        /// <summary>Enemigos en la escena ahora mismo (vivos o hundiéndose).</summary>
        public int ActiveCount => pool != null ? pool.CountActive : 0;

        /// <summary>Total de enemigos creados desde que empezó la escena. Si deja de subir, el pool está reutilizando.</summary>
        public int CreatedCount => created;

        public EnemySoldier Prefab => prefab;

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogError($"[{nameof(EnemyPool)}] Falta asignar el prefab del soldado.", this);
                enabled = false;
                return;
            }

            pool = new ObjectPool<EnemySoldier>(
                createFunc: Create,
                actionOnGet: null,
                actionOnRelease: soldier => soldier.gameObject.SetActive(false),
                actionOnDestroy: soldier => Destroy(soldier.gameObject),
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, prewarm),
                maxSize: Mathf.Max(maxSize, prewarm));

            var warm = new List<EnemySoldier>(prewarm);
            for (int i = 0; i < prewarm; i++) warm.Add(pool.Get());
            for (int i = 0; i < warm.Count; i++) pool.Release(warm[i]);
        }

        /// <summary>Saca un soldado del pool y lo deja listo para pelear en esa posición.</summary>
        public EnemySoldier Spawn(Vector3 position, Quaternion rotation)
        {
            if (pool == null) return null;

            EnemySoldier soldier = pool.Get();
            soldier.transform.SetPositionAndRotation(position, rotation);
            soldier.gameObject.SetActive(true);
            soldier.ResetForSpawn(position, rotation);
            return soldier;
        }

        private EnemySoldier Create()
        {
            EnemySoldier soldier = Instantiate(prefab, transform);
            created++;
            soldier.name = $"{prefab.name}_{created:00}";
            soldier.gameObject.SetActive(false);
            soldier.Released += OnReleased;
            return soldier;
        }

        private void OnReleased(EnemySoldier soldier) => pool.Release(soldier);
    }
}
