using System;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Companions
{
    /// <summary>
    /// IA mínima de la bardo (tarea 16). Con un NavMeshAgent:
    ///  - Sigue al caballero a distancia: se queda dentro de un anillo (followDistance) alrededor de él y no se mueve
    ///    mientras esté dentro de la banda, así no se agita.
    ///  - Se aparta si el caballero se le acerca demasiado (minDistance), para no estorbarle el paso.
    ///  - Se aleja de los enemigos que entran en su radio de peligro (dangerRadius) y vuelve al anillo cuando están
    ///    lejos (safeRadius, mayor que dangerRadius para que no oscile entre huir y volver).
    ///  - Si se atasca o el caballero se aleja demasiado, se recoloca en el NavMesh cerca de él.
    ///  - Tiene Health propio; al morir se lanza Died (derrota).
    /// El canto y su potenciación son de la tarea 17 y se añaden como otro componente en el mismo objeto.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class BardCompanion : MonoBehaviour
    {
        public enum CompanionState { Following, Fleeing, Dead }

        [Header("Líder")]
        [Tooltip("A quién sigue. Vacío = el PlayerController de la escena.")]
        [SerializeField] private Transform leader;

        [Header("Seguimiento")]
        [Tooltip("Distancia a la que se queda del caballero.")]
        [SerializeField, Min(0.5f)] private float followDistance = 3.5f;
        [Tooltip("Si el caballero se acerca más que esto, ella retrocede (no le bloquea el paso).")]
        [SerializeField, Min(0.1f)] private float minDistance = 2f;
        [Tooltip("Margen sobre followDistance antes de volver a moverse.")]
        [SerializeField, Min(0f)] private float tolerance = 0.75f;
        [SerializeField, Min(0.1f)] private float followSpeed = 5.5f;
        [Tooltip("Cada cuántos segundos recalcula su destino.")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;

        [Header("Peligro")]
        [Tooltip("Si un enemigo entra en este radio, huye.")]
        [SerializeField, Min(0.5f)] private float dangerRadius = 5f;
        [Tooltip("Deja de huir cuando no hay enemigos dentro de este radio (mayor que dangerRadius).")]
        [SerializeField, Min(0.5f)] private float safeRadius = 7.5f;
        [Tooltip("Cuánto se aleja en cada paso de huida.")]
        [SerializeField, Min(1f)] private float fleeDistance = 6f;
        [SerializeField, Min(0.1f)] private float fleeSpeed = 6.5f;
        [Tooltip("Cuánto prefiere huir hacia el caballero (0 = directo lejos del enemigo, 1 = muy hacia el caballero).")]
        [SerializeField, Range(0f, 2f)] private float leaderBias = 0.5f;
        [Tooltip("Capas de los enemigos. Vacío = capa Enemy.")]
        [SerializeField] private LayerMask enemyMask;

        [Header("Atascos")]
        [Tooltip("Segundos sin avanzar antes de recolocarse.")]
        [SerializeField, Min(0.5f)] private float stuckTime = 1.5f;
        [Tooltip("Si el caballero está más lejos que esto, se teletransporta junto a él.")]
        [SerializeField, Min(5f)] private float teleportDistance = 25f;

        private NavMeshAgent agent;
        private Health health;
        private readonly Collider[] overlaps = new Collider[16];
        private float nextRepath;
        private float stuckTimer;

        public CompanionState State { get; private set; } = CompanionState.Following;
        public Health Health => health;
        public bool IsDead => State == CompanionState.Dead;

        /// <summary>Se lanza al cambiar de estado (por ejemplo para animación o interfaz).</summary>
        public event Action<CompanionState> StateChanged;

        /// <summary>Se lanza una vez cuando la bardo muere: derrota.</summary>
        public event Action Died;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            if (enemyMask.value == 0) enemyMask = GameLayers.EnemyMask;
            agent.stoppingDistance = 0.1f;
            agent.speed = followSpeed;
        }

        private void Start()
        {
            if (leader == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) leader = player.transform;
            }

            if (leader == null) Debug.LogWarning("[BardCompanion] No hay a quién seguir: asigna 'leader' o pon un PlayerController en la escena.", this);
        }

        private void OnEnable() => health.Died += OnHealthDied;
        private void OnDisable() => health.Died -= OnHealthDied;

        private void Update()
        {
            if (IsDead || leader == null) return;
            if (!agent.isOnNavMesh)
            {
                Recover();
                return;
            }

            // Estado: huir con histéresis (entra en dangerRadius, sale en safeRadius).
            bool threatened = FindThreat(State == CompanionState.Fleeing ? safeRadius : dangerRadius, out Vector3 threat);
            if (State == CompanionState.Following && threatened) SetState(CompanionState.Fleeing);
            else if (State == CompanionState.Fleeing && !threatened) SetState(CompanionState.Following);

            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                if (State == CompanionState.Fleeing) Flee(threat);
                else Follow();
            }

            CheckStuck();
        }

        // ---------------------------------------------------------------- Comportamientos

        private void Follow()
        {
            Vector3 fromLeader = Flat(transform.position - leader.position);
            float distance = fromLeader.magnitude;
            if (distance < 0.01f) fromLeader = -Flat(leader.forward);

            bool outsideBand = distance > followDistance + tolerance || distance < minDistance;
            if (!outsideBand)
            {
                agent.isStopped = true;
                return;
            }

            // Va al punto del anillo que tiene más cerca, sin importar hacia dónde mire el caballero.
            Vector3 destination = leader.position + fromLeader.normalized * followDistance;
            agent.speed = followSpeed;
            agent.isStopped = false;
            MoveTo(destination);
        }

        private void Flee(Vector3 threat)
        {
            Vector3 away = Flat(transform.position - threat);
            if (away.sqrMagnitude < 0.01f) away = -Flat(transform.forward);
            Vector3 toLeader = Flat(leader.position - transform.position);
            Vector3 direction = (away.normalized + toLeader.normalized * leaderBias).normalized;

            agent.speed = fleeSpeed;
            agent.isStopped = false;
            if (!MoveTo(transform.position + direction * fleeDistance))
                MoveTo(transform.position + direction * fleeDistance * 0.5f);
        }

        private bool MoveTo(Vector3 target)
        {
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return false;
            return agent.SetDestination(hit.position);
        }

        private bool FindThreat(float radius, out Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlaps, enemyMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            position = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                IDamageable enemy = overlaps[i].GetComponentInParent<IDamageable>();
                if (enemy == null || enemy.IsDead) continue;

                Vector3 point = enemy.Transform.position;
                float sqr = (point - transform.position).sqrMagnitude;
                if (sqr >= best) continue;
                best = sqr;
                position = point;
                found = true;
            }

            return found;
        }

        // ---------------------------------------------------------------- Atascos y recolocación

        private void CheckStuck()
        {
            float leaderDistance = Flat(leader.position - transform.position).magnitude;
            if (leaderDistance > teleportDistance)
            {
                WarpNearLeader();
                return;
            }

            bool tryingToMove = !agent.isStopped && agent.hasPath && !agent.pathPending && agent.remainingDistance > 1f;
            stuckTimer = tryingToMove && agent.velocity.sqrMagnitude < 0.04f ? stuckTimer + Time.deltaTime : 0f;
            if (stuckTimer >= stuckTime)
            {
                stuckTimer = 0f;
                WarpNearLeader();
            }
        }

        private void WarpNearLeader()
        {
            Vector3 fromLeader = Flat(transform.position - leader.position);
            if (fromLeader.sqrMagnitude < 0.01f) fromLeader = -Flat(leader.forward);
            Vector3 target = leader.position + fromLeader.normalized * followDistance;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas) ||
                NavMesh.SamplePosition(leader.position, out hit, 6f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        private void Recover()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else if (leader != null)
                WarpNearLeader();
        }

        // ---------------------------------------------------------------- Estado y muerte

        private void SetState(CompanionState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        private void OnHealthDied(Health source, DamageInfo info)
        {
            SetState(CompanionState.Dead);
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            Debug.Log("[BardCompanion] La bardo cayó: derrota.", this);
            Died?.Invoke();
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, dangerRadius);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, safeRadius);
            if (leader == null) return;
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(leader.position, followDistance);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(leader.position, minDistance);
        }
    }
}
