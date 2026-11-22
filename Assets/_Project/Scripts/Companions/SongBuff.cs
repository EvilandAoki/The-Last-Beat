using System;
using System.Collections.Generic;
using TheLastBeat.Buffs;
using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Companions
{
    public enum SongState
    {
        /// <summary>Canta: los aliados dentro del radio están potenciados.</summary>
        Singing,
        /// <summary>Acaba de ser interrumpida: el grupo ya perdió el efecto.</summary>
        Interrupted,
        /// <summary>Se recupera y todavía no puede volver a cantar.</summary>
        Recovering,
    }

    /// <summary>
    /// Canción de la bardo (tarea 17). Va en el mismo objeto que BardCompanion. Mientras canta aplica un BuffEffect a los
    /// BuffReceiver de los aliados dentro del radio, y se lo quita cuando salen de él o cuando la canción se corta.
    ///
    /// Estados: Singing, Interrupted y Recovering. Interrupt() corta la canción: pasa a Interrupted
    /// (interruptedDuration), luego a Recovering (recoveryDuration) y vuelve a cantar. Un golpe con
    /// DamageInfo.Interrupts a la bardo la interrumpe (tarea 18); si ya estaba interrumpida o recuperándose, el golpe
    /// reinicia el contador. Audio, VFX e interfaz escuchan OnSongStateChanged y OnInterrupted.
    /// </summary>
    public class SongBuff : MonoBehaviour
    {
        [Header("Canción")]
        [Tooltip("Radio, en metros, dentro del que los aliados reciben el efecto.")]
        [SerializeField, Min(0.5f)] private float radius = 8f;
        [SerializeField] private BuffEffect effect = new BuffEffect();
        [Tooltip("Segundos en estado Interrumpida tras un golpe.")]
        [SerializeField, Min(0f)] private float interruptedDuration = 1f;
        [Tooltip("Segundos en estado Recuperando antes de volver a cantar. Más alto = más castigo por interrumpirla.")]
        [SerializeField, Min(0f)] private float recoveryDuration = 3f;
        [Tooltip("Empieza cantando al aparecer en la escena.")]
        [SerializeField] private bool startSinging = true;
        [Tooltip("La bardo también recibe el efecto, si tiene un BuffReceiver.")]
        [SerializeField] private bool includeSelf = true;

        [Header("Detección")]
        [Tooltip("Cada cuántos segundos busca aliados en el radio.")]
        [SerializeField, Min(0.05f)] private float scanInterval = 0.2f;
        [Tooltip("Capas de los aliados. Vacío = Player y Ally.")]
        [SerializeField] private LayerMask alliesMask;

        private Health health;
        private readonly Collider[] overlaps = new Collider[32];
        private readonly HashSet<BuffReceiver> inRange = new HashSet<BuffReceiver>();
        private readonly HashSet<BuffReceiver> found = new HashSet<BuffReceiver>();
        private readonly List<BuffReceiver> leaving = new List<BuffReceiver>();
        private float stateEndsAt;
        private float nextScan;
        private bool started;

        public SongState State { get; private set; } = SongState.Recovering;
        public bool IsSinging => State == SongState.Singing;
        public float Radius => radius;
        public BuffEffect Effect => effect;

        /// <summary>Aliados que reciben el efecto ahora mismo.</summary>
        public int AlliesInRange => inRange.Count;

        /// <summary>Segundos que faltan para que termine el estado actual (0 si está cantando).</summary>
        public float StateTimeRemaining => State == SongState.Singing ? 0f : Mathf.Max(0f, stateEndsAt - Time.time);

        /// <summary>Tiempo total desde que se interrumpe hasta que vuelve a cantar. Útil para rellenar una barra.</summary>
        public float TotalDowntime => interruptedDuration + recoveryDuration;

        /// <summary>Se lanza cada vez que cambia el estado de la canción (audio, VFX e interfaz).</summary>
        public event Action<SongState> OnSongStateChanged;

        /// <summary>
        /// Se lanza con cada golpe que interrumpe la canción, también los que llegan con ella ya cortada.
        /// El parámetro es true si el golpe reinició un contador en lugar de cortar una canción que sonaba.
        /// Es lo que debe usar el audio y el VFX de "canción interrumpida", porque un golpe repetido no cambia de estado.
        /// </summary>
        public event Action<bool> OnInterrupted;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (alliesMask.value == 0) alliesMask = GameLayers.HeroesMask;
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnBardDamaged;
                health.Died += OnBardDied;
            }

            if (started) return;

            started = true;
            if (startSinging) SetState(SongState.Singing);
            else BeginRecovery();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnBardDamaged;
                health.Died -= OnBardDied;
            }

            ClearAll();
        }

        private void Update()
        {
            float now = Time.time;
            switch (State)
            {
                case SongState.Singing:
                    if (now >= nextScan)
                    {
                        nextScan = now + scanInterval;
                        Scan();
                    }

                    break;
                case SongState.Interrupted:
                    if (now >= stateEndsAt) BeginRecovery();
                    break;
                case SongState.Recovering:
                    if (now >= stateEndsAt)
                    {
                        nextScan = 0f;
                        SetState(SongState.Singing);
                    }

                    break;
            }
        }

        /// <summary>
        /// Corta la canción: el grupo pierde el efecto al instante y ella tarda interruptedDuration + recoveryDuration
        /// en volver a cantar. Si ya estaba cortada, reinicia el contador del estado en que esté (Interrupted o Recovering),
        /// así que seguir golpeándola la mantiene sin cantar.
        /// </summary>
        public void Interrupt()
        {
            float now = Time.time;
            switch (State)
            {
                case SongState.Singing:
                    ClearAll();
                    stateEndsAt = now + interruptedDuration;
                    SetState(SongState.Interrupted);
                    OnInterrupted?.Invoke(false);
                    break;
                case SongState.Interrupted:
                    stateEndsAt = now + interruptedDuration;
                    OnInterrupted?.Invoke(true);
                    break;
                case SongState.Recovering:
                    stateEndsAt = now + recoveryDuration;
                    OnInterrupted?.Invoke(true);
                    break;
            }
        }

        private void OnBardDamaged(Health source, DamageInfo info)
        {
            // Solo los golpes que interrumpen (DamageInfo.Interrupts); veneno o quemadura no la cortan.
            if (!info.Interrupts || source.IsDead) return;
            Interrupt();
        }

        /// <summary>Vuelve a activar la canción y empieza a cantar (la bardo revive en un reinicio del nivel).</summary>
        public void ResetSong()
        {
            ClearAll();
            enabled = true;
            nextScan = 0f;
            SetState(SongState.Singing);
        }

        private void BeginRecovery()
        {
            stateEndsAt = Time.time + recoveryDuration;
            SetState(SongState.Recovering);
        }

        private void SetState(SongState next)
        {
            if (State == next) return;
            State = next;
            OnSongStateChanged?.Invoke(next);
        }

        private void OnBardDied(Health source, DamageInfo info)
        {
            // Muerta no vuelve a cantar: se corta y se desactiva (OnDisable también quita el efecto).
            ClearAll();
            stateEndsAt = float.MaxValue;
            SetState(SongState.Interrupted);
            enabled = false;
        }

        // ---------------------------------------------------------------- Aplicar y quitar el efecto

        private void Scan()
        {
            found.Clear();
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, overlaps, alliesMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                BuffReceiver receiver = overlaps[i].GetComponentInParent<BuffReceiver>();
                if (receiver == null || (!includeSelf && receiver.gameObject == gameObject)) continue;

                // El radio se mide hasta el centro del aliado, no hasta el borde de su collider.
                Vector3 offset = receiver.transform.position - origin;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius) continue;
                found.Add(receiver);
            }

            // Entran (o siguen dentro: Apply es idempotente y recoge cambios del efecto en el Inspector).
            foreach (BuffReceiver receiver in found)
            {
                inRange.Add(receiver);
                receiver.Apply(this, effect);
            }

            // Salen del radio, o dejaron de existir.
            leaving.Clear();
            foreach (BuffReceiver receiver in inRange)
                if (receiver == null || !found.Contains(receiver)) leaving.Add(receiver);
            for (int i = 0; i < leaving.Count; i++)
            {
                inRange.Remove(leaving[i]);
                if (leaving[i] != null) leaving[i].Remove(this);
            }
        }

        private void ClearAll()
        {
            foreach (BuffReceiver receiver in inRange)
                if (receiver != null) receiver.Remove(this);
            inRange.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
