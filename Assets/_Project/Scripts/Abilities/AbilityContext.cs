using TheLastBeat.Player;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Todo lo que una habilidad necesita al lanzarse: quién la lanza y sus valores ya calculados para el nivel actual.
    /// Se crea una vez por lanzamiento.
    /// </summary>
    public class AbilityContext
    {
        public AbilityCaster Caster;
        public AbilityData Data;
        public int Level;

        /// <summary>Valores resueltos para el nivel (base por las mejoras acumuladas).</summary>
        public float Damage;
        public float Radius;
        public float Cooldown;

        public Transform Transform => Caster.transform;
        public PlayerController Controller => Caster.Controller;
        public Animator Animator => Caster.Animator;
    }
}
