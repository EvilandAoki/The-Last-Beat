using System;
using System.Collections.Generic;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Combo básico de 3 golpes (tarea 8). Los tiempos viven aquí y no en el Animator, así el combo
    /// se puede afinar desde el Inspector sin tocar transiciones:
    ///  - comboWindow: cuánto tiempo, después de terminar un golpe, sigue vigente el combo. Pulsar dentro
    ///    de esa ventana continúa con el siguiente golpe aunque hayas esperado; fuera, vuelve al golpe 1.
    ///  - chainFrom: desde qué punto del golpe (0-1) se puede encadenar el siguiente.
    ///  - inputBuffer: una pulsación hecha un poco antes de poder encadenar se recuerda este tiempo.
    ///  - stepDelay: pausa entre un golpe y el siguiente (más alto = golpes más pausados).
    /// El daño se aplica cuando el clip dispara el Animation Event OnHit (ver CombatAnimationEvents).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Serializable]
        public class ComboStep
        {
            [Tooltip("Nombre del estado del Animator (igual al del clip).")]
            public string stateName = "Attack1";
            public float damage = 10f;
            [Tooltip("Distancia, delante del héroe, al centro del golpe.")]
            public float range = 1.4f;
            [Tooltip("Radio del golpe.")]
            public float radius = 1.2f;
            public float knockback = 3f;
            [Tooltip("Metros que avanza el héroe durante el golpe.")]
            public float lunge = 0.5f;
            [Tooltip("Golpe final: efectos más fuertes.")]
            public bool heavy;
        }

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("El Animator del modelo (puede estar en un hijo).")]
        [SerializeField] private Animator animator;

        [Header("Combo")]
        [SerializeField]
        private List<ComboStep> steps = new List<ComboStep>
        {
            new ComboStep { stateName = "Attack1", damage = 10f, lunge = 0.5f },
            new ComboStep { stateName = "Attack2", damage = 12f, lunge = 0.5f },
            new ComboStep { stateName = "Attack3", damage = 20f, lunge = 1.0f, knockback = 6f, heavy = true },
        };

        [Tooltip("Segundos tras terminar un golpe en los que se puede seguir el combo. Más alto = combos más pausados.")]
        [SerializeField, Min(0f)] private float comboWindow = 1.5f;
        [Tooltip("Fracción del golpe (0-1) desde la que se puede encadenar el siguiente. Debe quedar después del impacto.")]
        [SerializeField, Range(0f, 1f)] private float chainFrom = 0.55f;
        [Tooltip("Segundos que se recuerda una pulsación hecha antes de poder encadenar.")]
        [SerializeField, Min(0f)] private float inputBuffer = 0.35f;
        [Tooltip("Pausa, en segundos, entre un golpe y el siguiente.")]
        [SerializeField, Min(0f)] private float stepDelay = 0.08f;
        [Tooltip("Pausa tras el golpe final antes de poder empezar otro combo.")]
        [SerializeField, Min(0f)] private float finisherRecovery = 0.3f;
        [Tooltip("Fracción de la velocidad de movimiento mientras se ataca (0 = quieto).")]
        [SerializeField, Range(0f, 1f)] private float movementWhileAttacking = 0.2f;
        [Tooltip("Mezcla, en segundos, al entrar en la animación del golpe.")]
        [SerializeField, Min(0f)] private float crossFade = 0.06f;
        [Tooltip("Parte inicial del golpe (0-1) durante la que el héroe avanza.")]
        [SerializeField, Range(0.05f, 1f)] private float lungeShare = 0.35f;

        [Header("Impacto")]
        [Tooltip("Capas que se golpean. Vacío = capa Enemy.")]
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private Team team = Team.Heroes;
        [SerializeField] private bool logHits = true;

        private PlayerController controller;
        private readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        private readonly Collider[] overlaps = new Collider[24];
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();

        private bool swinging;
        private int currentStep = -1;
        private int nextStep;
        private float stepStart;
        private float stepLength = 0.8f;
        private float comboExpiresAt;
        private float bufferedUntil = -1f;
        private float readyAt;

        public bool IsAttacking => swinging;
        public int CurrentStep => currentStep;
        public int StepCount => steps.Count;

        /// <summary>Se lanza al empezar cada golpe con su índice (0 a 2).</summary>
        public event Action<int> StepStarted;

        /// <summary>Se lanza por cada enemigo golpeado: golpe, enemigo, daño.</summary>
        public event Action<int, IDamageable, float> EnemyHit;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitMask.value == 0) hitMask = GameLayers.EnemyMask;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    clipLengths[clip.name] = clip.length;
            }
        }

        private void OnEnable()
        {
            if (input != null) input.AttackPressed += OnAttackPressed;
        }

        private void OnDisable()
        {
            if (input != null) input.AttackPressed -= OnAttackPressed;
            if (swinging) EndSwing(natural: false);
        }

        private void OnAttackPressed() => bufferedUntil = Time.time + inputBuffer;

        private void Update()
        {
            float now = Time.time;

            if (swinging)
            {
                float t = (now - stepStart) / stepLength;
                if (t < lungeShare && steps[currentStep].lunge > 0f)
                    controller.ExternalMove(transform.forward * (steps[currentStep].lunge * Time.deltaTime / (stepLength * lungeShare)));

                bool hasNext = currentStep + 1 < steps.Count;
                if (hasNext && t >= chainFrom && bufferedUntil >= now)
                {
                    EndSwing(natural: false);
                    readyAt = now + stepDelay;
                }
                else if (t >= 1f)
                {
                    EndSwing(natural: true);
                }
                return;
            }

            if (bufferedUntil >= now && now >= readyAt)
            {
                bool comboAlive = now <= comboExpiresAt && nextStep < steps.Count;
                StartStep(comboAlive ? nextStep : 0);
            }
        }

        private void StartStep(int index)
        {
            ComboStep step = steps[index];
            currentStep = index;
            swinging = true;
            stepStart = Time.time;
            stepLength = clipLengths.TryGetValue(step.stateName, out float length) ? length : 0.8f;
            bufferedUntil = -1f;

            if (controller.DesiredDirection.sqrMagnitude > 0.01f) controller.FaceDirection(controller.DesiredDirection);
            controller.SpeedMultiplier = movementWhileAttacking;
            if (animator != null) animator.CrossFadeInFixedTime(step.stateName, crossFade, 0, 0f);
            StepStarted?.Invoke(index);
        }

        private void EndSwing(bool natural)
        {
            if (!swinging) return;
            swinging = false;
            controller.SpeedMultiplier = 1f;

            bool finisher = currentStep + 1 >= steps.Count;
            if (finisher)
            {
                nextStep = 0;
                comboExpiresAt = 0f;
                if (natural) readyAt = Time.time + finisherRecovery;
            }
            else
            {
                nextStep = currentStep + 1;
                comboExpiresAt = Time.time + comboWindow + stepDelay;
            }
        }

        /// <summary>Animation Event en el frame de impacto de cada clip de ataque.</summary>
        public void OnHit()
        {
            if (!swinging) return;
            ComboStep step = steps[currentStep];
            Vector3 center = transform.position + Vector3.up + transform.forward * step.range;
            int count = Physics.OverlapSphereNonAlloc(center, step.radius, overlaps, hitMask, QueryTriggerInteraction.Ignore);

            damaged.Clear();
            for (int i = 0; i < count; i++)
            {
                IDamageable target = overlaps[i].GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || !damaged.Add(target)) continue;

                Vector3 direction = target.Transform.position - transform.position;
                direction.y = 0f;
                DamageInfo info = DamageInfo.Create(step.damage, gameObject, team,
                    overlaps[i].ClosestPoint(center), direction, step.knockback, true, step.heavy);
                if (target.TakeDamage(info)) EnemyHit?.Invoke(currentStep, target, step.damage);
            }

            if (logHits) Debug.Log($"[PlayerCombat] Golpe {currentStep + 1}: {damaged.Count} enemigo(s) en el área.", this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            foreach (ComboStep step in steps)
                Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * step.range, step.radius);
        }
    }
}
