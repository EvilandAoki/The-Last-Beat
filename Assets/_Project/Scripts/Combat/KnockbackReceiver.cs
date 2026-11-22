using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Empuja a quien lo lleva cuando recibe un golpe con DamageInfo.Knockback > 0. Añádelo a cualquier enemigo (o a lo
    /// que deba reaccionar al empuje) junto a su Health; no hace falta tocar el código del enemigo.
    /// Mueve con lo que tenga el objeto, en este orden: NavMeshAgent (no se sale del NavMesh), CharacterController y,
    /// si no hay ninguno, el Transform. Mientras dura el empuje, IsBeingPushed es true para que la IA espere.
    /// Sin asignaciones: sirve con pooling (al desactivar el objeto se cancela el empuje).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class KnockbackReceiver : MonoBehaviour
    {
        [Tooltip("Metros de empuje por cada unidad de Knockback del golpe.")]
        [SerializeField, Min(0f)] private float metersPerKnockback = 0.25f;
        [Tooltip("Segundos que dura el empuje.")]
        [SerializeField, Min(0.01f)] private float pushTime = 0.12f;
        [Tooltip("Distancia máxima de un empuje, en metros.")]
        [SerializeField, Min(0f)] private float maxDistance = 3f;
        [Tooltip("Los enemigos pesados se empujan menos (1 = normal, 0 = inmóvil).")]
        [SerializeField, Range(0f, 1f)] private float resistance;

        private Health health;
        private NavMeshAgent agent;
        private CharacterController character;
        private Vector3 velocity;
        private float timeLeft;

        public bool IsBeingPushed => timeLeft > 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            character = GetComponent<CharacterController>();
        }

        private void OnEnable() => health.Damaged += OnDamaged;

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            timeLeft = 0f;
        }

        /// <summary>Empuja en una dirección (en el plano) una distancia en metros. También lo usan otros sistemas.</summary>
        public void Push(Vector3 direction, float meters)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            meters = Mathf.Min(meters * (1f - resistance), maxDistance);
            if (meters <= 0f) return;

            velocity = direction.normalized * (meters / pushTime);
            timeLeft = pushTime;
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (info.Knockback <= 0f) return;
            Push(info.Direction, info.Knockback * metersPerKnockback);
        }

        private void Update()
        {
            if (timeLeft <= 0f) return;

            float step = Mathf.Min(Time.deltaTime, timeLeft);
            timeLeft -= step;
            Vector3 delta = velocity * step;

            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(delta);
            else if (character != null && character.enabled) character.Move(delta);
            else transform.position += delta;
        }
    }
}
