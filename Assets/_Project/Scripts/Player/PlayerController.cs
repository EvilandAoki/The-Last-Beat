using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Movimiento del héroe controlado por el jugador: relativo a la cámara cenital,
    /// con aceleración, giro suave y gravedad. Los ataques y habilidades no mueven
    /// al personaje directamente: piden bloquear el movimiento (AddMoveLock),
    /// cambiar la velocidad (SpeedMultiplier) o aplicar un desplazamiento (ExternalMove).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Si se deja vacío, usa la Main Camera.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Opcional hasta la tarea 28. Recibe el parámetro float 'Speed'.")]
        [SerializeField] private Animator animator;

        [Header("Movimiento")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float acceleration = 40f;
        [SerializeField, Min(0f)] private float deceleration = 50f;
        [Tooltip("Grados por segundo.")]
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField] private float gravity = -25f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private int moveLocks;

        /// <summary>Multiplicador de velocidad (giro lento = 0.4, potenciación, ralentización).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public bool CanMove => moveLocks == 0;
        public Vector3 Velocity => horizontalVelocity;
        public float NormalizedSpeed => moveSpeed > 0f ? horizontalVelocity.magnitude / moveSpeed : 0f;

        /// <summary>Dirección en la que el jugador está pidiendo moverse, en el mundo (magnitud 0 a 1).</summary>
        public Vector3 DesiredDirection { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            // Los aliados (la bardo) no bloquean el paso del héroe: él los atraviesa y ellos se apartan solos.
            controller.excludeLayers = controller.excludeLayers.value | LayerMask.GetMask(GameLayers.AllyName);
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            DesiredDirection = input != null ? CameraRelative(input.Move) : Vector3.zero;
            Vector3 targetVelocity = CanMove ? DesiredDirection * (moveSpeed * SpeedMultiplier) : Vector3.zero;

            float rate = targetVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * dt);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity += gravity * dt;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);

            if (CanMove && DesiredDirection.sqrMagnitude > 0.01f) RotateTowards(DesiredDirection, turnSpeed * dt);

            if (animator != null) animator.SetFloat(SpeedHash, NormalizedSpeed, 0.1f, dt);
        }

        /// <summary>Bloquea el movimiento. Cada AddMoveLock necesita su RemoveMoveLock.</summary>
        public void AddMoveLock() => moveLocks++;

        public void RemoveMoveLock() => moveLocks = Mathf.Max(0, moveLocks - 1);

        /// <summary>Gira hacia una dirección (por ejemplo, hacia el enemigo al atacar).</summary>
        public void FaceDirection(Vector3 direction, bool instant = true)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            if (instant) transform.rotation = Quaternion.LookRotation(direction);
            else RotateTowards(direction, turnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Desplazamiento aplicado por otros sistemas (embestida, empuje). Respeta las colisiones
        /// del CharacterController, así que no atraviesa paredes.
        /// </summary>
        public void ExternalMove(Vector3 delta)
        {
            delta.y = 0f;
            controller.Move(delta);
        }

        /// <summary>Detiene en seco la inercia (al terminar una embestida, al morir).</summary>
        public void StopImmediately() => horizontalVelocity = Vector3.zero;

        private Vector3 CameraRelative(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
        }

        private void RotateTowards(Vector3 direction, float maxDegrees)
        {
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDegrees);
        }
    }
}
