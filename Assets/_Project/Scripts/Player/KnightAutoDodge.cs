using System;
using TheLastBeat.Buffs;
using TheLastBeat.Combat;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Esquiva automática del caballero (tarea 19): mientras está potenciado (canción de la bardo), cada golpe que
    /// recibe tiene una probabilidad de ser evitado. Es un IDamageFilter: va en el mismo objeto que Health y cancela el
    /// golpe antes de que se aplique. La probabilidad sale del BuffReceiver (BuffEffect.dodgeChance), así que sin
    /// potenciación es 0 y no esquiva nada. Dodged avisa para el aviso visual, el sonido o las estadísticas.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(BuffReceiver))]
    public class KnightAutoDodge : MonoBehaviour, IDamageFilter
    {
        private BuffReceiver buffs;

        /// <summary>Esquivas desde que apareció el héroe.</summary>
        public int DodgeCount { get; private set; }

        /// <summary>Se lanza cuando se esquiva un golpe, con el golpe evitado.</summary>
        public event Action<DamageInfo> Dodged;

        private void Awake() => buffs = GetComponent<BuffReceiver>();

        public bool Filter(ref DamageInfo info)
        {
            float chance = buffs != null ? buffs.DodgeChance : 0f;
            if (chance <= 0f || info.Amount <= 0f || Random.value >= chance) return true;

            DodgeCount++;
            Dodged?.Invoke(info);
            return false;
        }
    }
}
