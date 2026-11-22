using System.Collections;
using TheLastBeat.Combat;
using UnityEngine;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Maniquí de pruebas del combo (tarea 8). Ponlo junto a un Health en la capa Enemy: parpadea al recibir
    /// daño, muestra en la consola el daño y la vida restante, y se reinicia solo unos segundos después de morir.
    /// Bórralo de la escena cuando ya no lo necesites.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 2f;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashTime = 0.1f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Health health;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Vector3 startScale;
        private Vector3 startPosition;

        private void Awake()
        {
            health = GetComponent<Health>();
            // El empuje lo resuelve el componente compartido: así un maniquí se comporta como lo hará un enemigo.
            if (GetComponent<KnockbackReceiver>() == null) gameObject.AddComponent<KnockbackReceiver>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            startScale = transform.localScale;
            startPosition = transform.position;
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

        private void OnDamaged(Health source, DamageInfo info)
        {
            Debug.Log($"[{name}] -{info.Amount:0.#}  vida {source.CurrentHealth:0.#}/{source.MaxHealth:0.#}{(info.IsHeavy ? "  (golpe pesado)" : "")}", this);
            StopAllCoroutines();
            StartCoroutine(Flash());
        }

        private void OnDied(Health source, DamageInfo info)
        {
            Debug.Log($"[{name}] derrotado.", this);
            StartCoroutine(Respawn());
        }

        private IEnumerator Flash()
        {
            SetColor(flashColor);
            transform.localScale = startScale * 1.08f;
            yield return new WaitForSeconds(flashTime);
            SetColor(null);
            transform.localScale = startScale;
        }

        private IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            health.ResetHealth();
            transform.position = startPosition;
            SetColor(null);
            transform.localScale = startScale;
        }

        private void SetColor(Color? color)
        {
            foreach (Renderer r in renderers)
            {
                if (color.HasValue)
                {
                    block.SetColor(BaseColor, color.Value);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
