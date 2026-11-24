using System.Collections;
using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Pausa breve del juego al golpear (hit stop). Baja Time.timeScale unos milisegundos y lo restaura. No se acumula:
    /// si llega otro golpe mientras hay una pausa, o antes del intervalo mínimo, se ignora. Respeta una pausa del juego
    /// (timeScale 0) y no pisa un cambio de timeScale hecho por otro sistema mientras dura.
    /// </summary>
    public class HitStopDriver : MonoBehaviour
    {
        private static HitStopDriver instance;

        private bool active;
        private float endsAt;
        private float lastEnd = float.NegativeInfinity;
        private float previousScale = 1f;
        private float appliedScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("HitStop");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitStopDriver>();
        }

        /// <summary>Pide una pausa de duration segundos (tiempo real). No hace nada si ya hay una o es demasiado pronto.</summary>
        public static void Request(float duration, float timeScale, float minInterval)
        {
            if (duration <= 0f) return;
            Ensure();
            instance.Begin(duration, timeScale, minInterval);
        }

        private void Begin(float duration, float timeScale, float minInterval)
        {
            float now = Time.unscaledTime;
            if (active || now < lastEnd + minInterval) return;   // no se acumula
            if (Time.timeScale <= 0f) return;                     // el juego ya está en pausa

            previousScale = Time.timeScale;
            appliedScale = timeScale;
            Time.timeScale = timeScale;
            endsAt = now + duration;
            active = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (Time.unscaledTime < endsAt) yield return null;
            End();
        }

        private void End()
        {
            if (!active) return;
            active = false;
            lastEnd = Time.unscaledTime;
            if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = previousScale;
        }

        private void OnDisable() => End();
        private void OnDestroy() => End();
    }
}
