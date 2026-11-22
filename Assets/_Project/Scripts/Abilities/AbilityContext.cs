using System;
using System.Collections.Generic;
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

        private readonly List<Action> cleanups = new List<Action>();

        /// <summary>
        /// Registra algo que debe deshacerse cuando la habilidad termina o se cancela (restaurar la rotación del
        /// modelo, la velocidad, etc.). El AbilityCaster lo ejecuta siempre, aunque la rutina se interrumpa.
        /// </summary>
        public void RegisterCleanup(Action cleanup) => cleanups.Add(cleanup);

        /// <summary>Cambia la velocidad de movimiento (1 = normal, 0.35 = lento) hasta que termine la habilidad.</summary>
        public void SetMovementSpeed(float multiplier)
        {
            if (Caster.Controller == null) return;
            Caster.Controller.SpeedMultiplier = multiplier;
            if (!speedRestoreRegistered)
            {
                speedRestoreRegistered = true;
                RegisterCleanup(() => Caster.Controller.SpeedMultiplier = 1f);
            }
        }

        private bool speedRestoreRegistered;

        internal void RunCleanups()
        {
            for (int i = cleanups.Count - 1; i >= 0; i--)
            {
                try { cleanups[i](); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            }

            cleanups.Clear();
        }

        public Transform Transform => Caster.transform;
        public PlayerController Controller => Caster.Controller;
        public Animator Animator => Caster.Animator;
    }
}
