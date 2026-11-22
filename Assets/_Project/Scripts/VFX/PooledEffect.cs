using System;
using UnityEngine;

namespace TheLastBeat.VFX
{
    /// <summary>
    /// Va en la raíz de cada efecto de partículas (tarea 33). El sistema raíz tiene la acción de parada en Callback: cuando
    /// termina (o se le pide Stop y mueren sus partículas) el efecto se devuelve solo al pool, sin que nadie lo vigile
    /// cada frame. La raíz debe durar más que sus hijos (ondas, chispas) para que se devuelva cuando todo terminó.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledEffect : MonoBehaviour
    {
        private ParticleSystem root;
        private bool released = true;

        /// <summary>La pone el pool: devuelve este efecto al pool.</summary>
        internal Action<PooledEffect> Release;

        private void Awake()
        {
            root = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = root.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        internal void Play()
        {
            released = false;
            root.Clear(true);
            root.Play(true);
        }

        /// <summary>Deja de emitir; el efecto vuelve al pool cuando mueren las partículas que quedan.</summary>
        public void Stop()
        {
            if (root != null) root.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnParticleSystemStopped()
        {
            if (released) return;
            released = true;
            Release?.Invoke(this);
        }
    }
}
