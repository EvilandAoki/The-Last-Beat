using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Contrato de todo lo que puede recibir daño. Los sistemas de ataque
    /// (HitDetector, proyectiles, explosiones) solo conocen esta interfaz.
    /// </summary>
    public interface IDamageable
    {
        Team Team { get; }
        bool IsDead { get; }
        Transform Transform { get; }

        /// <summary>
        /// Aplica el daño. Devuelve true si se aplicó, false si se ignoró
        /// (mismo bando, ya muerto, esquivado o reducido a 0 por un filtro).
        /// </summary>
        bool TakeDamage(DamageInfo info);
    }
}
