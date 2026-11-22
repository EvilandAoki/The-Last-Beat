using System;
using System.Collections;
using System.Collections.Generic;
using TheLastBeat.Buffs;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using TheLastBeat.Player;
using TheLastBeat.VFX;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Lanza las habilidades del héroe y lleva sus recargas (tarea 9). Tiene dos ranuras: la 0 responde al botón
    /// de Habilidad 1 y la 1 al de Habilidad 2 (ver PlayerInputReader). La asignación y el nivel se editan en el Inspector.
    /// La interfaz lee las recargas con CooldownRemaining / CooldownNormalized o con los eventos
    /// CooldownStarted y CooldownFinished.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class AbilityCaster : MonoBehaviour, IRespawnable
    {
        public const int SlotCount = 2;

        [Serializable]
        public class Slot
        {
            public AbilityData ability;
            [Min(1)] public int level = 1;
            [NonSerialized] public float readyAt;
            [NonSerialized] public float duration;
            [NonSerialized] public bool onCooldown;
        }

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("El Animator del modelo (puede estar en un hijo).")]
        [SerializeField] private Animator animator;

        [Tooltip("Estado del Animator al que se vuelve si una habilidad se cancela a medias.")]
        [SerializeField] private string idleState = "Idle";

        [Header("Ranuras (0 = Habilidad 1, 1 = Habilidad 2)")]
        [SerializeField] private Slot[] slots = { new Slot(), new Slot() };

        [Header("Impacto")]
        [Tooltip("Capas que se golpean. Vacío = capa Enemy.")]
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private Team team = Team.Heroes;

        private PlayerController controller;
        private PlayerCombat combat;
        private BuffReceiver buffs;
        private readonly Collider[] overlaps = new Collider[32];
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> sessionHits = new HashSet<IDamageable>();
        private Coroutine routine;
        private AbilityContext current;
        private int locksHeld;

        public PlayerController Controller => controller;
        public LayerMask HitMask => hitMask;
        public Animator Animator => animator;
        public bool IsCasting { get; private set; }

        /// <summary>Se lanza al empezar una habilidad: ranura y datos.</summary>
        public event Action<int, AbilityData> Cast;

        /// <summary>Se lanza al empezar la recarga: ranura y duración total en segundos.</summary>
        public event Action<int, float> CooldownStarted;

        /// <summary>Se lanza cuando la habilidad vuelve a estar lista.</summary>
        public event Action<int> CooldownFinished;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            combat = GetComponent<PlayerCombat>();
            buffs = GetComponent<BuffReceiver>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitMask.value == 0) hitMask = GameLayers.EnemyMask;
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.Ability1Pressed += OnAbility1;
            input.Ability2Pressed += OnAbility2;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Ability1Pressed -= OnAbility1;
                input.Ability2Pressed -= OnAbility2;
            }

            Abort();
        }

        private void OnAbility1() => TryCast(0);
        private void OnAbility2() => TryCast(1);

        private void Update()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot.onCooldown && Time.time >= slot.readyAt)
                {
                    slot.onCooldown = false;
                    CooldownFinished?.Invoke(i);
                }
            }
        }

        /// <summary>Cancela la habilidad en curso y deja todas las recargas listas (reinicio del nivel).</summary>
        public void ResetForRestart()
        {
            Abort();
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].onCooldown) continue;
                slots[i].onCooldown = false;
                CooldownFinished?.Invoke(i);
            }
        }

        // ---------------------------------------------------------------- Consulta (para la UI)

        public AbilityData GetAbility(int slot) => InRange(slot) ? slots[slot].ability : null;
        public int GetLevel(int slot) => InRange(slot) ? Mathf.Clamp(slots[slot].level, 1, MaxLevelOf(slot)) : 1;

        public bool IsReady(int slot) => InRange(slot) && slots[slot].ability != null && !slots[slot].onCooldown;

        /// <summary>Segundos que faltan para poder usarla de nuevo (0 = lista).</summary>
        public float CooldownRemaining(int slot) =>
            InRange(slot) && slots[slot].onCooldown ? Mathf.Max(0f, slots[slot].readyAt - Time.time) : 0f;

        /// <summary>Duración total de la recarga en curso (o la que tendría si se lanzara ahora).</summary>
        public float CooldownTotal(int slot)
        {
            if (!InRange(slot) || slots[slot].ability == null) return 0f;
            return slots[slot].onCooldown ? slots[slot].duration : slots[slot].ability.GetCooldown(GetLevel(slot));
        }

        /// <summary>1 = recién lanzada, 0 = lista. Útil para rellenar una barra o un círculo.</summary>
        public float CooldownNormalized(int slot)
        {
            float total = CooldownTotal(slot);
            return total > 0f ? Mathf.Clamp01(CooldownRemaining(slot) / total) : 0f;
        }

        // ---------------------------------------------------------------- Cambios (mejoras, objetos, pruebas)

        public void SetAbility(int slot, AbilityData ability, int level = 1)
        {
            if (!InRange(slot)) return;
            slots[slot].ability = ability;
            slots[slot].level = Mathf.Max(1, level);
            slots[slot].onCooldown = false;
        }

        public void SetLevel(int slot, int level)
        {
            if (InRange(slot)) slots[slot].level = Mathf.Clamp(level, 1, MaxLevelOf(slot));
        }

        // ---------------------------------------------------------------- Lanzar

        /// <summary>Intenta lanzar la habilidad de la ranura. Devuelve false si no está lista, ya se está lanzando otra o se está atacando.</summary>
        public bool TryCast(int slot)
        {
            if (!IsReady(slot) || IsCasting) return false;
            if (combat != null && combat.IsAttacking) return false;

            Slot s = slots[slot];
            int level = GetLevel(slot);
            var context = new AbilityContext
            {
                Caster = this,
                Data = s.ability,
                Level = level,
                // Potenciado (canción de la bardo): más daño y más área en todas las habilidades (giro, embestida...).
                Damage = s.ability.GetDamage(level) * (buffs != null ? buffs.DamageMultiplier : 1f),
                Radius = s.ability.GetRadius(level) * (buffs != null ? buffs.AreaMultiplier : 1f),
                Cooldown = s.ability.GetCooldown(level),
            };

            s.duration = context.Cooldown;
            s.readyAt = Time.time + s.duration;
            s.onCooldown = s.duration > 0f;
            IsCasting = true;
            current = context;
            routine = StartCoroutine(Run(context));

            Cast?.Invoke(slot, s.ability);
            if (s.onCooldown) CooldownStarted?.Invoke(slot, s.duration);
            return true;
        }

        private IEnumerator Run(AbilityContext context)
        {
            PlayAnimation(context.Data.animatorState);
            IEnumerator cast = context.Data.Cast(context);
            while (cast.MoveNext()) yield return cast.Current;
            PlayAnimation(context.Data.endAnimatorState);
            Finish();
        }

        private void Finish()
        {
            if (current != null)
            {
                current.RunCleanups();
                current = null;
            }

            ReleaseLocks();
            IsCasting = false;
            routine = null;
        }

        private void Abort()
        {
            bool wasCasting = IsCasting;
            if (routine != null) StopCoroutine(routine);
            Finish();
            if (wasCasting) PlayAnimation(idleState);
        }

        // ---------------------------------------------------------------- Utilidades para las habilidades

        /// <summary>Bloquea el movimiento del héroe mientras dura el lanzamiento. Se libera solo al terminar o cancelar.</summary>
        public void LockMovement()
        {
            controller.AddMoveLock();
            locksHeld++;
        }

        public void UnlockMovement()
        {
            if (locksHeld <= 0) return;
            controller.RemoveMoveLock();
            locksHeld--;
        }

        private void ReleaseLocks()
        {
            while (locksHeld > 0) UnlockMovement();
        }

        /// <summary>
        /// Reproduce un estado del Animator con una mezcla corta. normalizedOffset (0-1) salta a un punto del clip,
        /// por ejemplo para entrar directamente en el momento del golpe.
        /// </summary>
        public void PlayAnimation(string state, float fade = 0.08f, float normalizedOffset = 0f)
        {
            if (animator == null || string.IsNullOrEmpty(state)) return;
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) return;
            if (normalizedOffset > 0f) animator.CrossFade(hash, fade, 0, normalizedOffset);
            else animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
        }

        /// <summary>
        /// Daña a todos los enemigos dentro de una esfera, una sola vez cada uno. Devuelve a cuántos golpeó.
        /// </summary>
        public int HitArea(Vector3 center, float radius, float damage, float knockback, bool heavy = true)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlaps, hitMask, QueryTriggerInteraction.Ignore);
            damaged.Clear();
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                IDamageable target = overlaps[i].GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || !damaged.Add(target)) continue;

                Vector3 direction = target.Transform.position - transform.position;
                direction.y = 0f;
                DamageInfo info = DamageInfo.Create(damage, gameObject, team,
                    overlaps[i].ClosestPoint(center), direction, knockback, true, heavy);
                if (target.TakeDamage(info))
                {
                    hits++;
                    Vfx.Play(heavy ? VfxKind.ImpactHeavy : VfxKind.Impact, info.HitPoint);
                }
            }

            return hits;
        }

        /// <summary>
        /// Conjunto reutilizable para recordar a quién ya se golpeó durante UNA habilidad (por ejemplo una carga que
        /// golpea en varios frames). Se vacía cada vez que se pide; solo hay uno a la vez porque solo se lanza una habilidad a la vez.
        /// </summary>
        public HashSet<IDamageable> NewHitSession()
        {
            sessionHits.Clear();
            return sessionHits;
        }

        /// <summary>
        /// Daña a los enemigos dentro de una caja orientada, salvo los que ya estén en alreadyHit (a los que golpea se
        /// añaden). Sirve para golpear a lo largo de un recorrido sin dañar dos veces al mismo enemigo. Devuelve cuántos golpeó ahora.
        /// </summary>
        public int HitBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, float damage, float knockback,
            bool heavy, HashSet<IDamageable> alreadyHit)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlaps, rotation, hitMask, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                IDamageable target = overlaps[i].GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || alreadyHit.Contains(target)) continue;

                Vector3 direction = target.Transform.position - transform.position;
                direction.y = 0f;
                DamageInfo info = DamageInfo.Create(damage, gameObject, team,
                    overlaps[i].ClosestPoint(center), direction, knockback, true, heavy);
                if (target.TakeDamage(info))
                {
                    alreadyHit.Add(target);
                    hits++;
                    Vfx.Play(heavy ? VfxKind.ImpactHeavy : VfxKind.Impact, info.HitPoint);
                }
            }

            return hits;
        }

        private bool InRange(int slot) => slot >= 0 && slot < slots.Length;
        private int MaxLevelOf(int slot) => slots[slot].ability != null ? slots[slot].ability.MaxLevel : 1;

        private void OnValidate()
        {
            if (slots == null || slots.Length != SlotCount) Array.Resize(ref slots, SlotCount);
            for (int i = 0; i < slots.Length; i++) if (slots[i] == null) slots[i] = new Slot();
        }
    }
}
