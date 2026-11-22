using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastBeat.Buffs
{
    /// <summary>
    /// Va en todo lo que puede ser potenciado (el caballero, la bardo). Guarda las potenciaciones activas, una por
    /// fuente, y expone el resultado combinado. Quien aplica el efecto (por ejemplo SongBuff) llama a Apply y Remove;
    /// quien lo usa (daño, habilidades, esquiva) lee DamageMultiplier, AreaMultiplier y DodgeChance.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuffReceiver : MonoBehaviour
    {
        private readonly Dictionary<object, BuffEffect> active = new Dictionary<object, BuffEffect>();

        /// <summary>true mientras haya al menos una potenciación activa.</summary>
        public bool IsBuffed => active.Count > 0;

        public float DamageMultiplier { get; private set; } = 1f;
        public float AreaMultiplier { get; private set; } = 1f;
        public float DodgeChance { get; private set; }

        /// <summary>Se lanza cuando pasa de no estar potenciado a estarlo (true) y al revés (false).</summary>
        public event Action<bool> BuffChanged;

        /// <summary>Aplica un efecto de una fuente. Llamarlo otra vez con la misma fuente y el mismo efecto no hace nada.</summary>
        public void Apply(object source, BuffEffect effect)
        {
            if (source == null || effect == null) return;
            if (active.TryGetValue(source, out BuffEffect current) && ReferenceEquals(current, effect)) return;

            bool wasBuffed = IsBuffed;
            active[source] = effect;
            Recompute();
            if (!wasBuffed) BuffChanged?.Invoke(true);
        }

        /// <summary>Quita el efecto de una fuente.</summary>
        public void Remove(object source)
        {
            if (source == null || !active.Remove(source)) return;
            Recompute();
            if (!IsBuffed) BuffChanged?.Invoke(false);
        }

        private void Recompute()
        {
            float damage = 1f, area = 1f, noDodge = 1f;
            foreach (KeyValuePair<object, BuffEffect> pair in active)
            {
                damage *= pair.Value.damageMultiplier;
                area *= pair.Value.abilityAreaMultiplier;
                noDodge *= 1f - pair.Value.dodgeChance;
            }

            DamageMultiplier = damage;
            AreaMultiplier = area;
            DodgeChance = 1f - noDodge;
        }

        private void OnDisable()
        {
            if (active.Count == 0) return;
            active.Clear();
            Recompute();
            BuffChanged?.Invoke(false);
        }
    }
}
