using System;
using System.Collections;
using System.Collections.Generic;
using TheLastBeat.Combat;
using TheLastBeat.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Level
{
    /// <summary>
    /// Lanza las hordas de un WaveData (tarea 15) sacando los soldados del EnemyPool. Cada horda empieza cuando cayó
    /// la anterior. Usa como puntos de aparición los hijos de Spawn Points Root y evita los que están cerca de un héroe,
    /// para que no salgan encima del jugador.
    /// Avisa con OnWaveStarted, OnWaveCleared y OnAllWavesCleared: el flujo del nivel (tarea 20), la música y la
    /// interfaz reaccionan a eso sin que este script los conozca.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private WaveData data;
        [SerializeField] private EnemyPool pool;
        [Tooltip("Objeto cuyos hijos son los puntos de aparición (Spawns en Pueblo).")]
        [SerializeField] private Transform spawnPointsRoot;

        [Header("Inicio")]
        [Tooltip("Empieza solo al dar Play. Con el flujo del nivel (tarea 20) va apagado: lo lanza LevelFlow.")]
        [SerializeField] private bool autoStart = true;
        [Tooltip("Pausa entre StartWaves y la primera horda.")]
        [SerializeField, Min(0f)] private float startDelay = 1f;

        [Header("Aparición")]
        [Tooltip("No usa puntos a menos de esta distancia de un héroe, si hay alguno más lejos.")]
        [SerializeField, Min(0f)] private float minDistanceFromHeroes = 8f;
        [SerializeField] private bool logToConsole = true;

        private readonly List<Transform> points = new List<Transform>();
        private readonly List<Transform> candidates = new List<Transform>();

        /// <summary>Empezó una horda (índice desde 0 y sus datos).</summary>
        public event Action<int, Wave> OnWaveStarted;

        /// <summary>Cayó el último enemigo de una horda (índice desde 0).</summary>
        public event Action<int> OnWaveCleared;

        /// <summary>Cayeron todas las hordas.</summary>
        public event Action OnAllWavesCleared;

        public bool AutoStart
        {
            get => autoStart;
            set => autoStart = value;
        }

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }

        /// <summary>Horda actual, desde 0. -1 antes de empezar.</summary>
        public int CurrentWave { get; private set; } = -1;

        /// <summary>Enemigos de la horda actual que siguen vivos.</summary>
        public int AliveCount { get; private set; }

        public int WaveCount => data != null && data.waves != null ? data.waves.Length : 0;

        private void Awake()
        {
            points.Clear();
            if (spawnPointsRoot != null)
                foreach (Transform child in spawnPointsRoot) points.Add(child);
        }

        private void Start()
        {
            if (autoStart) StartWaves();
        }

        /// <summary>Empieza la primera horda. Si ya empezaron, no hace nada.</summary>
        public void StartWaves()
        {
            if (IsRunning || IsFinished) return;
            if (data == null || pool == null || points.Count == 0 || WaveCount == 0)
            {
                Debug.LogError($"[{nameof(WaveSpawner)}] Falta el WaveData, el EnemyPool o los puntos de aparición.", this);
                return;
            }

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            IsRunning = true;
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            for (int i = 0; i < data.waves.Length; i++)
            {
                Wave wave = data.waves[i];
                if (wave.delayBefore > 0f) yield return new WaitForSeconds(wave.delayBefore);

                CurrentWave = i;
                if (logToConsole) Debug.Log($"[Hordas] Empieza {wave.name}: {wave.count} enemigos.", this);
                OnWaveStarted?.Invoke(i, wave);

                int spawned = 0;
                while (spawned < wave.count)
                {
                    while (AliveCount >= wave.maxAlive) yield return null;

                    if (SpawnOne())
                    {
                        spawned++;
                        if (wave.spawnInterval > 0f) yield return new WaitForSeconds(wave.spawnInterval);
                    }
                    else
                    {
                        yield return null;
                    }
                }

                while (AliveCount > 0) yield return null;

                if (logToConsole) Debug.Log($"[Hordas] {wave.name} superada.", this);
                OnWaveCleared?.Invoke(i);
            }

            IsRunning = false;
            IsFinished = true;
            if (logToConsole) Debug.Log("[Hordas] Todas las hordas superadas.", this);
            OnAllWavesCleared?.Invoke();
        }

        private bool SpawnOne()
        {
            // Un soldado fuera del NavMesh no se podría mover y la horda nunca terminaría: si el punto no tiene NavMesh
            // cerca, no sale y se prueba otro punto en el siguiente frame.
            Transform point = PickPoint();
            if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[{nameof(WaveSpawner)}] El punto {point.name} no tiene NavMesh a menos de 4 m.", point);
                return false;
            }

            Vector3 position = hit.position;

            // Mirando hacia el héroe al que va a perseguir.
            Quaternion rotation = point.rotation;
            Health hero = HeroTargets.FindBest(position, 0f);
            if (hero != null)
            {
                Vector3 direction = hero.transform.position - position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f) rotation = Quaternion.LookRotation(direction);
            }

            EnemySoldier soldier = pool.Spawn(position, rotation);
            if (soldier == null) return false;

            AliveCount++;
            soldier.Died += OnSoldierDied;
            return true;
        }

        private void OnSoldierDied(EnemySoldier soldier)
        {
            soldier.Died -= OnSoldierDied;
            AliveCount = Mathf.Max(0, AliveCount - 1);
        }

        private Transform PickPoint()
        {
            candidates.Clear();
            for (int i = 0; i < points.Count; i++)
                if (HeroTargets.NearestDistance(points[i].position) >= minDistanceFromHeroes) candidates.Add(points[i]);

            List<Transform> from = candidates.Count > 0 ? candidates : points;
            return from[UnityEngine.Random.Range(0, from.Count)];
        }

        private void OnDrawGizmos()
        {
            if (spawnPointsRoot == null) return;
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            foreach (Transform point in spawnPointsRoot)
            {
                Gizmos.DrawWireSphere(point.position + Vector3.up * 0.1f, 0.6f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 1.2f);
            }
        }
    }
}
