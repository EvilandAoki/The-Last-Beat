using System;
using System.Collections.Generic;
using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Combo del héroe con ataque débil (J, clic izquierdo) y fuerte (K, botón sur) (tarea 8).
    /// Los golpes forman un árbol: cada golpe dice a cuál pasa si el siguiente botón es débil o fuerte
    /// (-1 = ninguno, el combo termina). Los tiempos viven aquí y no en el Animator:
    ///  - comboWindow: segundos, tras terminar un golpe, en los que seguir el combo. Pasado ese tiempo,
    ///    el siguiente botón empieza un combo nuevo (lightOpener o heavyOpener).
    ///  - chainFrom (por golpe): desde qué punto del golpe (0-1) se puede encadenar el siguiente.
    ///  - inputBuffer: una pulsación hecha un poco antes de poder encadenar se recuerda este tiempo.
    ///  - stepDelay: pausa entre un golpe y el siguiente (más alto = golpes más pausados).
    /// El daño se aplica cuando el clip dispara el Animation Event OnHit (ver CombatAnimationEvents).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        public enum AttackKind { Light, Heavy }

        [Serializable]
        public class ComboStep
        {
            [Tooltip("Nombre del estado del Animator (igual al del clip).")]
            public string stateName = "Light1";
            public float damage = 10f;
            [Tooltip("Distancia, delante del héroe, al centro del golpe.")]
            public float range = 1.4f;
            [Tooltip("Radio del golpe.")]
            public float radius = 1.2f;
            public float knockback = 3f;
            [Tooltip("Metros que avanza el héroe durante el golpe.")]
            public float lunge = 0.5f;
            [Tooltip("Golpe fuerte: efectos más fuertes.")]
            public bool heavy;
            [Tooltip("Fracción del golpe (0-1) desde la que se puede encadenar el siguiente. Debe quedar después del impacto.")]
            [Range(0f, 1f)] public float chainFrom = 0.55f;
            [Tooltip("Índice del golpe al que se pasa con ataque débil (-1 = ninguno).")]
            public int nextOnLight = -1;
            [Tooltip("Índice del golpe al que se pasa con ataque fuerte (-1 = ninguno).")]
            public int nextOnHeavy = -1;

            public int Next(AttackKind kind) => kind == AttackKind.Light ? nextOnLight : nextOnHeavy;
            public bool IsFinisher => nextOnLight < 0 && nextOnHeavy < 0;
        }

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("El Animator del modelo (puede estar en un hijo).")]
        [SerializeField] private Animator animator;

        [Header("Árbol de combos")]
        [SerializeField]
        private List<ComboStep> steps = new List<ComboStep>
        {
            new ComboStep { stateName = "Light1", damage = 10f, lunge = 0.5f, chainFrom = 0.5f, nextOnLight = 1, nextOnHeavy = 2 },
            new ComboStep { stateName = "Light2", damage = 12f, lunge = 0.5f, chainFrom = 0.5f, nextOnLight = 0, nextOnHeavy = 3 },
            new ComboStep { stateName = "Heavy1", damage = 24f, range = 1.5f, radius = 1.4f, knockback = 6f, lunge = 0.9f, heavy = true, chainFrom = 0.6f, nextOnLight = 1, nextOnHeavy = 3 },
            new ComboStep { stateName = "Heavy2", damage = 32f, range = 1.2f, radius = 1.9f, knockback = 9f, lunge = 0.6f, heavy = true, chainFrom = 1f },
        };
        [Tooltip("Golpe con el que empieza un combo al pulsar ataque débil.")]
        [SerializeField] private int lightOpener = 0;
        [Tooltip("Golpe con el que empieza un combo al pulsar ataque fuerte.")]
        [SerializeField] private int heavyOpener = 2;

        [Header("Tiempos")]
        [Tooltip("Segundos tras terminar un golpe en los que se puede seguir el combo. Más alto = combos más pausados.")]
        [SerializeField, Min(0f)] private float comboWindow = 1.5f;
        [Tooltip("Segundos que se recuerda una pulsación hecha antes de poder encadenar.")]
        [SerializeField, Min(0f)] private float inputBuffer = 0.35f;
        [Tooltip("Pausa, en segundos, entre un golpe y el siguiente.")]
        [SerializeField, Min(0f)] private float stepDelay = 0.08f;
        [Tooltip("Pausa tras el golpe final antes de poder empezar otro combo.")]
        [SerializeField, Min(0f)] private float finisherRecovery = 0.3f;
        [Tooltip("Fracción de la velocidad de movimiento mientras se ataca (0 = quieto).")]
        [SerializeField, Range(0f, 1f)] private float movementWhileAttacking = 0.2f;
        [Tooltip("Mezcla, en segundos, al entrar en la animación del golpe.")]
        [SerializeField, Min(0f)] private float crossFade = 0.08f;
        [Tooltip("Parte inicial del golpe (0-1) durante la que el héroe avanza.")]
        [SerializeField, Range(0.05f, 1f)] private float lungeShare = 0.45f;

        [Header("Impacto")]
        [Tooltip("Capas que se golpean. Vacío = capa Enemy.")]
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private Team team = Team.Heroes;
        [Tooltip("Escribe cada golpe en la consola. Asigna memoria: déjalo apagado al revisar el Profiler.")]
        [SerializeField] private bool logHits;

        private PlayerController controller;
        private AbilityCaster abilities;
        private readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
        private readonly Collider[] overlaps = new Collider[24];
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();

        private bool swinging;
        private int currentStep = -1;
        private int lastStep = -1;      // último golpe terminado, mientras el combo siga vigente
        private int pendingStep = -1;   // golpe ya elegido por encadenado, a la espera del stepDelay
        private float stepStart;
        private float stepLength = 0.8f;
        private float comboExpiresAt;
        private float bufferedUntil = -1f;
        private AttackKind bufferedKind;
        private float readyAt;

        public bool IsAttacking => swinging;
        public int CurrentStep => currentStep;
        public int StepCount => steps.Count;

        /// <summary>Se lanza al empezar cada golpe con su índice.</summary>
        public event Action<int> StepStarted;

        /// <summary>Se lanza por cada enemigo golpeado: golpe, enemigo, daño.</summary>
        public event Action<int, IDamageable, float> EnemyHit;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            abilities = GetComponent<AbilityCaster>();
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
            if (input == null) return;
            input.AttackPressed += OnLightPressed;
            input.HeavyAttackPressed += OnHeavyPressed;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.AttackPressed -= OnLightPressed;
                input.HeavyAttackPressed -= OnHeavyPressed;
            }

            if (swinging) EndSwing(natural: false);
            pendingStep = -1;
        }

        private void OnLightPressed() => Buffer(AttackKind.Light);
        private void OnHeavyPressed() => Buffer(AttackKind.Heavy);

        private void Buffer(AttackKind kind)
        {
            // Mientras se lanza una habilidad no se encadenan ataques.
            if (abilities != null && abilities.IsCasting) return;
            bufferedKind = kind;
            bufferedUntil = Time.time + inputBuffer;
        }

        private void Update()
        {
            float now = Time.time;

            if (swinging)
            {
                ComboStep step = steps[currentStep];
                float t = (now - stepStart) / stepLength;
                if (t < lungeShare && step.lunge > 0f)
                    controller.ExternalMove(transform.forward * (step.lunge * Time.deltaTime / (stepLength * lungeShare)));

                if (bufferedUntil >= now && t >= step.chainFrom)
                {
                    int target = step.Next(bufferedKind);
                    if (target >= 0 && target < steps.Count)
                    {
                        bufferedUntil = -1f;
                        EndSwing(natural: false);
                        pendingStep = target;
                        readyAt = now + stepDelay;
                        return;
                    }
                }

                if (t >= 1f) EndSwing(natural: true);
                return;
            }

            if (pendingStep >= 0)
            {
                if (now >= readyAt)
                {
                    int target = pendingStep;
                    pendingStep = -1;
                    StartStep(target);
                }
                return;
            }

            if (bufferedUntil >= now && now >= readyAt)
            {
                int target = -1;
                if (lastStep >= 0 && now <= comboExpiresAt) target = steps[lastStep].Next(bufferedKind);
                if (target < 0 || target >= steps.Count) target = bufferedKind == AttackKind.Light ? lightOpener : heavyOpener;
                bufferedUntil = -1f;
                StartStep(Mathf.Clamp(target, 0, steps.Count - 1));
            }
        }

        private void StartStep(int index)
        {
            ComboStep step = steps[index];
            currentStep = index;
            swinging = true;
            stepStart = Time.time;
            stepLength = clipLengths.TryGetValue(step.stateName, out float length) ? length : 0.8f;

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

            ComboStep step = steps[currentStep];
            if (step.IsFinisher)
            {
                lastStep = -1;
                comboExpiresAt = 0f;
                if (natural) readyAt = Time.time + finisherRecovery;
            }
            else
            {
                lastStep = currentStep;
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

            if (logHits) Debug.Log($"[PlayerCombat] {step.stateName}: {damaged.Count} enemigo(s) en el área.", this);
        }

        private void OnValidate()
        {
            lightOpener = Mathf.Max(0, lightOpener);
            heavyOpener = Mathf.Max(0, heavyOpener);
        }

        private void OnDrawGizmosSelected()
        {
            foreach (ComboStep step in steps)
            {
                Gizmos.color = step.heavy ? new Color(1f, 0.2f, 0.1f, 0.6f) : new Color(1f, 0.8f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * step.range, step.radius);
            }
        }
    }
}
