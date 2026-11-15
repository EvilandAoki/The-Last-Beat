using TheLastBeat.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Prueba de la tarea 4. Ponlo en cualquier objeto de la escena, asigna un Health
    /// y presiona K para dañarlo o H para curarlo. Revisa la consola.
    /// Bórralo de la escena cuando ya no lo necesites.
    /// </summary>
    public class DamageTester : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float heal = 15f;
        [SerializeField] private Team attackerTeam = Team.Heroes;

        private void OnEnable()
        {
            if (target == null) return;
            target.Damaged += OnDamaged;
            target.Died += OnDied;
            target.HealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            if (target == null) return;
            target.Damaged -= OnDamaged;
            target.Died -= OnDied;
            target.HealthChanged -= OnHealthChanged;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || target == null) return;

            if (keyboard.kKey.wasPressedThisFrame)
            {
                var info = DamageInfo.Create(damage, gameObject, attackerTeam,
                                             target.transform.position, target.transform.position - transform.position);
                bool applied = target.TakeDamage(info);
                if (!applied) Debug.Log($"[DamageTester] Golpe ignorado ({(target.IsDead ? "ya está muerto" : "mismo bando o filtrado")}).");
            }

            if (keyboard.hKey.wasPressedThisFrame) target.Heal(heal);
            if (keyboard.rKey.wasPressedThisFrame) target.ResetHealth();
        }

        private void OnDamaged(Health h, DamageInfo info) => Debug.Log($"[DamageTester] {h.name} recibió {info}");
        private void OnDied(Health h, DamageInfo info) => Debug.Log($"[DamageTester] {h.name} murió.");
        private void OnHealthChanged(Health h) => Debug.Log($"[DamageTester] Vida de {h.name}: {h.CurrentHealth:0.#}/{h.MaxHealth:0.#}");
    }
}
