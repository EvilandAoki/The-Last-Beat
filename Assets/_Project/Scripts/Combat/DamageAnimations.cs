using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Anima el daño y la muerte de un personaje con Animator. Al recibir daño reproduce "Hit" en la capa de golpes
    /// (solo el torso, para no cortar el ataque en curso ni su evento OnHit) y sube el peso de esa capa mientras dura;
    /// al morir reproduce "Die" en la capa base, donde queda tendido hasta que un Rebind (Respawn) lo devuelve a Idle.
    /// Va en el objeto del Animator (el modelo) y busca el Health en sus padres: no hay que asignar nada.
    /// Si el controller no tiene esos estados o esa capa, no hace nada.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class DamageAnimations : MonoBehaviour
    {
        [SerializeField] private string hitState = "Hit";
        [SerializeField] private string dieState = "Die";
        [SerializeField] private string hitLayerName = "Golpes";
        [Tooltip("Cuánto tiempo pesa la capa de golpes tras recibir daño (s).")]
        [SerializeField, Min(0.05f)] private float hitDuration = 0.4f;
        [Tooltip("Tiempo para subir o bajar el peso de la capa de golpes (s).")]
        [SerializeField, Min(0.01f)] private float layerFade = 0.08f;

        private Animator animator;
        private Health health;
        private int hitLayer = -1;
        private float hitUntil;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            health = GetComponentInParent<Health>();
            hitLayer = animator.GetLayerIndex(hitLayerName);
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (hitLayer < 0) return;
            float target = Time.time < hitUntil ? 1f : 0f;
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(hitLayer), target, Time.deltaTime / layerFade);
            animator.SetLayerWeight(hitLayer, weight);
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (source.IsDead || hitLayer < 0 || !HasState(hitLayer, hitState)) return;
            animator.CrossFadeInFixedTime(hitState, 0.05f, hitLayer, 0f);
            hitUntil = Time.time + hitDuration;
        }

        private void OnDied(Health source, DamageInfo info)
        {
            hitUntil = 0f;
            if (HasState(0, dieState)) animator.CrossFadeInFixedTime(dieState, 0.1f, 0, 0f);
        }

        private bool HasState(int layer, string state) => animator.HasState(layer, Animator.StringToHash(state));
    }
}
