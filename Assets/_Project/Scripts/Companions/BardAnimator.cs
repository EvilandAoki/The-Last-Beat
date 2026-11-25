using TheLastBeat.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Companions
{
    /// <summary>
    /// Mueve el Animator de la bardo según lo que hace (tarea 26/28). Va en la raíz de Bardo.prefab, junto a BardCompanion.
    /// Parámetros del Animator (los crea el menú The Last Beat > Bardo > Instalar modelo 3D):
    ///  - Speed (float): velocidad del NavMeshAgent. Más de 0.1 = Run.
    ///  - Singing (bool): SongBuff cantando. Con ella quieta y cantando toca el laúd (Sing).
    ///  - Hit (trigger): recibió daño y sigue viva.
    ///  - Die (trigger): murió. Al revivir (BardCompanion.Revive) el Animator se reinicia.
    ///  - Attack (trigger): reservado; la bardo no ataca, pero el parámetro existe por el contrato de la tarea 28.
    /// No toca la lógica de la bardo: solo la escucha.
    /// </summary>
    [RequireComponent(typeof(BardCompanion))]
    public class BardAnimator : MonoBehaviour
    {
        [Tooltip("Animator del modelo. Vacío = el primero que haya en los hijos.")]
        [SerializeField] private Animator animator;
        [Tooltip("Segundos que tarda el parámetro Speed en seguir a la velocidad real (suaviza el paso Idle/Run).")]
        [SerializeField, Min(0f)] private float speedDamping = 0.08f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int SingingHash = Animator.StringToHash("Singing");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DieHash = Animator.StringToHash("Die");

        private BardCompanion bard;
        private Health health;
        private NavMeshAgent agent;
        private SongBuff song;
        private bool dead;

        private void Awake() => CacheReferences();

        // Awake y OnEnable pueden llegar en distinto orden según cómo se instancie el prefab: las referencias se piden
        // aquí y se vuelven a pedir en OnEnable/OnDisable si faltan.
        private void CacheReferences()
        {
            if (bard == null) bard = GetComponent<BardCompanion>();
            if (health == null) health = GetComponent<Health>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (song == null) song = GetComponent<SongBuff>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            if (animator == null)
                Debug.LogWarning("[BardAnimator] No hay Animator: pon el modelo como hijo o ejecuta The Last Beat > Bardo > Instalar modelo 3D.", this);
        }

        private void OnEnable()
        {
            CacheReferences();
            if (health != null) health.Damaged += OnDamaged;
            if (bard != null)
            {
                bard.Died += OnDied;
                bard.StateChanged += OnStateChanged;
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (bard != null)
            {
                bard.Died -= OnDied;
                bard.StateChanged -= OnStateChanged;
            }
        }

        private void Update()
        {
            if (animator == null || dead || agent == null) return;

            float speed = agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedHash, speed, speedDamping, Time.deltaTime);
            animator.SetBool(SingingHash, song != null && song.isActiveAndEnabled && song.IsSinging);
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (animator != null && !source.IsDead) animator.SetTrigger(HitHash);
        }

        private void OnDied()
        {
            dead = true;
            if (animator == null) return;
            animator.SetBool(SingingHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.SetTrigger(DieHash);
        }

        private void OnStateChanged(BardCompanion.CompanionState state)
        {
            // Revive() deja a la bardo en Following: se vuelve a la pose inicial y se limpia lo que quedó pendiente.
            if (!dead || state == BardCompanion.CompanionState.Dead) return;
            dead = false;
            if (animator != null) animator.Rebind();
        }
    }
}
