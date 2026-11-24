using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.Events;

namespace TheLastBeat.Level
{
    /// <summary>
    /// Zona invisible que dispara algo cuando entra el caballero (tarea 20). Lo que dispara se conecta en el Inspector
    /// (On Player Enter): en Pueblo, la entrada de la plaza llama a LevelFlow.StartCombat. Sirve igual para los demás
    /// pasos del nivel de la entrega final (diálogos, la unión del tanque...). Necesita un collider marcado como Is Trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Tooltip("Si está marcado, se dispara una sola vez.")]
        [SerializeField] private bool once = true;
        [SerializeField] private UnityEvent onPlayerEnter = new UnityEvent();

        private bool fired;

        /// <summary>Lo que pasa cuando entra el caballero. El menú de editor de la tarea 20 conecta aquí LevelFlow.StartCombat.</summary>
        public UnityEvent OnPlayerEnter => onPlayerEnter;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired && once) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            fired = true;
            onPlayerEnter.Invoke();
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
