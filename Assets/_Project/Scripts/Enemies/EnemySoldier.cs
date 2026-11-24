using System;
using System.Collections.Generic;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Enemies
{
    /// <summary>
    /// Demonio soldado (tarea 13). Máquina de estados sobre un NavMeshAgent:
    ///
    ///   Perseguir ──(en rango y sin recarga)──▶ Atacar: aviso rojo en el suelo ──▶ golpe ──▶ Recuperar ──▶ Perseguir
    ///   Golpe pesado recibido ──▶ Aturdido un momento (cancela el ataque). Stun(segundos) también lo aturde.
    ///   Vida 0 ──▶ Muerto: deja de colisionar, se hunde en el suelo y avisa con Released para volver al pool (tarea 14).
    ///
    /// Objetivo (HeroTargets): el héroe vivo más cercano, salvo que la bardo esté a menos de Bard Priority Radius.
    /// Cumple lo que pide docs/INTEGRACION.md para funcionar con el combo, las habilidades y la huida de la bardo: capa
    /// Enemy, collider sólido, Health del equipo Demons y KnockbackReceiver (mientras lo empujan no persigue ni ataca).
    /// Golpea con DamageInfo.Create(..., Team.Demons, ...) sobre las capas Player y Ally: a la bardo le corta la canción.
    ///
    /// Animación (tarea 28): si se asigna un Animator, escribe el float Speed y dispara los triggers Attack, Hit y Die.
    /// Audio (tarea 32): AnyDamaged y AnyDied avisan de cualquier soldado sin tener que conocerlos uno por uno.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class EnemySoldier : MonoBehaviour
    {
        public enum SoldierState { Chase, Attack, Recover, Stunned, Dead }

        [Header("Referencias")]
        [Tooltip("Disco rojo en el suelo que se enciende mientras prepara el golpe.")]
        [SerializeField] private GameObject attackIndicator;
        [Tooltip("Opcional hasta la tarea 28. Parámetros: Speed (float), Attack, Hit y Die (triggers).")]
        [SerializeField] private Animator animator;

        [Header("Ataque")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [Tooltip("Distancia al objetivo a la que se detiene y empieza a preparar el golpe.")]
        [SerializeField, Min(0.1f)] private float attackRange = 1.7f;
        [Tooltip("Radio de la esfera del golpe.")]
        [SerializeField, Min(0.1f)] private float hitRadius = 1.1f;
        [Tooltip("Distancia hacia delante del centro del golpe.")]
        [SerializeField, Min(0f)] private float hitForward = 1f;
        [Tooltip("Segundos de aviso antes de golpear: el tiempo que tiene el jugador para reaccionar.")]
        [SerializeField, Min(0f)] private float windupTime = 0.6f;
        [Tooltip("Segundos quieto después de golpear.")]
        [SerializeField, Min(0f)] private float recoverTime = 0.5f;
        [Tooltip("Segundos entre el final de un ataque y el siguiente.")]
        [SerializeField, Min(0f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0f)] private float knockback = 2f;
        [Tooltip("Si el golpe corta la canción de la bardo.")]
        [SerializeField] private bool interrupts = true;

        [Header("Reacciones")]
        [Tooltip("Segundos aturdido tras un golpe pesado (tercer golpe del combo, explosión del Giro...).")]
        [SerializeField, Min(0f)] private float heavyHitStun = 0.35f;
        [Tooltip("Grados por segundo al girar hacia el objetivo mientras prepara el golpe.")]
        [SerializeField, Min(0f)] private float turnSpeed = 540f;

        [Header("Objetivo")]
        [Tooltip("Si la bardo está más cerca que esto, va por ella aunque el caballero esté más cerca.")]
        [SerializeField, Min(0f)] private float bardPriorityRadius = 6f;
        [Tooltip("Cada cuántos segundos vuelve a elegir objetivo mientras persigue.")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.4f;
        [Tooltip("Cada cuántos segundos actualiza el destino del NavMeshAgent.")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;

        [Header("Muerte")]
        [Tooltip("Segundos que el cuerpo se queda donde cayó antes de hundirse.")]
        [SerializeField, Min(0f)] private float sinkDelay = 0.4f;
        [Tooltip("Segundos que tarda en hundirse. Al terminar vuelve al pool.")]
        [SerializeField, Min(0f)] private float sinkDuration = 1f;
        [SerializeField, Min(0f)] private float sinkDistance = 1.5f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private readonly Collider[] overlaps = new Collider[8];
        private readonly List<IDamageable> struck = new List<IDamageable>(4);

        private NavMeshAgent agent;
        private Health health;
        private KnockbackReceiver knockbackReceiver;
        private Collider[] colliders;
        private LayerMask heroesMask;

        private SoldierState state = SoldierState.Chase;
        private float stateTimer;
        private float stunDuration;
        private float cooldownTimer;
        private float retargetTimer;
        private float repathTimer;
        private Health target;
        private Vector3 deathStart;
        private bool released;

        /// <summary>Cualquier soldado recibió daño (por ejemplo, para el sonido del impacto).</summary>
        public static event Action<EnemySoldier, DamageInfo> AnyDamaged;

        /// <summary>Cualquier soldado murió.</summary>
        public static event Action<EnemySoldier> AnyDied;

        /// <summary>Este soldado murió (las hordas cuentan las bajas con esto).</summary>
        public event Action<EnemySoldier> Died;

        /// <summary>Terminó de hundirse: puede volver al pool. Si nadie escucha, se desactiva solo.</summary>
        public event Action<EnemySoldier> Released;

        public SoldierState State => state;
        public bool IsDead => state == SoldierState.Dead;
        public Health Health => health;

        /// <summary>true mientras prepara un golpe (el aviso rojo está encendido).</summary>
        public bool IsPreparingAttack => state == SoldierState.Attack;

        /// <summary>El héroe al que persigue o va a golpear. Puede ser null.</summary>
        public Health Target => target;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyDamaged = null;
            AnyDied = null;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            knockbackReceiver = GetComponent<KnockbackReceiver>();
            colliders = GetComponentsInChildren<Collider>(true);
            heroesMask = GameLayers.HeroesMask;
            if (attackIndicator != null) attackIndicator.SetActive(false);
            cooldownTimer = UnityEngine.Random.Range(0f, 0.5f);
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        /// <summary>Deja al soldado como nuevo en una posición. Lo llama EnemyPool al sacarlo del pool.</summary>
        public void ResetForSpawn(Vector3 position, Quaternion rotation)
        {
            released = false;
            health.ResetHealth();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = true;

            agent.enabled = true;
            agent.Warp(position);
            transform.rotation = rotation;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }

            target = null;
            retargetTimer = 0f;
            repathTimer = 0f;
            cooldownTimer = UnityEngine.Random.Range(0f, 0.5f); // que no ataquen todos a la vez
            SetState(SoldierState.Chase);

            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        /// <summary>Lo deja aturdido: no se mueve ni ataca. Un aturdimiento más largo reemplaza al que tenga.</summary>
        public void Stun(float seconds)
        {
            if (state == SoldierState.Dead || seconds <= 0f) return;
            float remaining = state == SoldierState.Stunned ? stunDuration - stateTimer : 0f;
            StopAgent();
            SetState(SoldierState.Stunned);
            stunDuration = Mathf.Max(seconds, remaining);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (state == SoldierState.Dead)
            {
                UpdateDeath(dt);
                return;
            }

            if (!agent.enabled || !agent.isOnNavMesh) return;

            cooldownTimer -= dt;
            stateTimer += dt;

            switch (state)
            {
                case SoldierState.Chase:
                    UpdateChase(dt);
                    break;
                case SoldierState.Attack:
                    UpdateAttack(dt);
                    break;
                case SoldierState.Recover:
                    if (stateTimer >= recoverTime)
                    {
                        cooldownTimer = attackCooldown;
                        SetState(SoldierState.Chase);
                    }

                    break;
                case SoldierState.Stunned:
                    if (stateTimer >= stunDuration) SetState(SoldierState.Chase);
                    break;
            }

            if (animator != null)
            {
                float speed = agent.speed > 0f ? agent.velocity.magnitude / agent.speed : 0f;
                animator.SetFloat(SpeedHash, speed, 0.1f, dt);
            }
        }

        private void UpdateChase(float dt)
        {
            retargetTimer -= dt;
            if (retargetTimer <= 0f || !HeroTargets.IsAlive(target))
            {
                target = HeroTargets.FindBest(transform.position, bardPriorityRadius);
                retargetTimer = retargetInterval;
            }

            // Sin objetivo, o mientras lo empujan: se queda quieto para que el empuje no pelee con el NavMeshAgent.
            if (target == null || (knockbackReceiver != null && knockbackReceiver.IsBeingPushed))
            {
                agent.isStopped = true;
                return;
            }

            agent.isStopped = false;
            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                agent.SetDestination(target.transform.position);
                repathTimer = repathInterval;
            }

            if (cooldownTimer <= 0f && FlatDistanceTo(target.transform.position) <= attackRange) StartAttack();
        }

        private void StartAttack()
        {
            StopAgent();
            SetState(SoldierState.Attack);
            if (attackIndicator != null) attackIndicator.SetActive(true);
            if (animator != null) animator.SetTrigger(AttackHash);
        }

        private void UpdateAttack(float dt)
        {
            if (HeroTargets.IsAlive(target)) FaceTowards(target.transform.position, dt);
            if (stateTimer < windupTime) return;

            Strike();
            SetState(SoldierState.Recover);
        }

        /// <summary>Daña una vez a cada héroe dentro de la esfera del golpe, delante del soldado.</summary>
        private void Strike()
        {
            Vector3 center = transform.position + Vector3.up + transform.forward * hitForward;
            int count = Physics.OverlapSphereNonAlloc(center, hitRadius, overlaps, heroesMask, QueryTriggerInteraction.Ignore);
            struck.Clear();
            for (int i = 0; i < count; i++)
            {
                IDamageable hero = overlaps[i].GetComponentInParent<IDamageable>();
                if (hero == null || hero.IsDead || struck.Contains(hero)) continue;
                struck.Add(hero);

                Vector3 direction = hero.Transform.position - transform.position;
                direction.y = 0f;
                DamageInfo info = DamageInfo.Create(damage, gameObject, health.Team, hero.Transform.position + Vector3.up,
                    direction, knockback, interrupts);
                hero.TakeDamage(info);
            }
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (state == SoldierState.Dead) return;
            AnyDamaged?.Invoke(this, info);
            if (source.CurrentHealth <= 0f) return; // el golpe mortal: de eso se encarga OnDied

            if (animator != null) animator.SetTrigger(HitHash);
            if (info.IsHeavy) Stun(heavyHitStun); // un golpe pesado cancela el ataque que estaba preparando
        }

        private void OnDied(Health source, DamageInfo info)
        {
            StopAgent();
            agent.enabled = false;
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            SetState(SoldierState.Dead);
            deathStart = transform.position;

            if (animator != null)
            {
                animator.ResetTrigger(AttackHash);
                animator.ResetTrigger(HitHash);
                animator.SetTrigger(DieHash);
            }

            AnyDied?.Invoke(this);
            Died?.Invoke(this);
        }

        private void UpdateDeath(float dt)
        {
            stateTimer += dt;
            float sinking = stateTimer - sinkDelay;
            float k = sinking <= 0f ? 0f : (sinkDuration > 0f ? Mathf.Clamp01(sinking / sinkDuration) : 1f);
            transform.position = deathStart + Vector3.down * (sinkDistance * k);

            if (released || stateTimer < sinkDelay + sinkDuration) return;
            released = true;
            if (Released != null) Released.Invoke(this);
            else gameObject.SetActive(false); // sin pool (soldado puesto a mano en la escena)
        }

        private void SetState(SoldierState next)
        {
            state = next;
            stateTimer = 0f;
            if (next != SoldierState.Attack && attackIndicator != null) attackIndicator.SetActive(false);
        }

        private void StopAgent()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private void FaceTowards(Vector3 point, float dt)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
        }

        private float FlatDistanceTo(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * hitForward, hitRadius);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, bardPriorityRadius);
        }
    }
}
