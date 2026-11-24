using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Sensación de impacto de cada golpe (tarea 12): una llamada junta la pausa del golpe (hit stop), la sacudida de cámara
    /// (CinemachineImpulseSource) y el número de daño (pool). Se llama una vez por enemigo golpeado; la pausa no se acumula
    /// y la sacudida se limita a una por frame, así un golpe de área contra varios enemigos no se multiplica.
    /// Todos los valores salen de ImpactSettings (Resources/ImpactSettings); sin el asset usa los valores por defecto.
    /// </summary>
    public static class ImpactFeedback
    {
        private static ImpactSettings settings;
        private static bool prewarmed;
        private static int lastShakeFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            settings = null;
            prewarmed = false;
            lastShakeFrame = -1;
        }

        public static ImpactSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                settings = Resources.Load<ImpactSettings>("ImpactSettings");
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<ImpactSettings>();
                    settings.hideFlags = HideFlags.DontSave;
                }

                return settings;
            }
        }

        /// <summary>Crea de antemano el pool de números, la fuente de impulso y el controlador de pausa. Se llama al aparecer el héroe.</summary>
        public static void Prewarm()
        {
            if (prewarmed) return;
            prewarmed = true;
            HitStopDriver.Ensure();
            CameraShake.Prepare();
            if (Settings.showDamageNumbers) DamageNumberDisplay.Prepare(Settings);
        }

        /// <summary>Un golpe conectó en un punto con cierto daño.</summary>
        public static void Hit(Vector3 point, float damage, ImpactKind kind)
        {
            ImpactSettings s = Settings;
            HitStopDriver.Request(s.HitStop(kind), s.hitStopTimeScale, s.hitStopMinInterval);

            if (Time.frameCount != lastShakeFrame)
            {
                lastShakeFrame = Time.frameCount;
                CameraShake.Shake(point, s.Shake(kind), kind >= ImpactKind.Finisher, s);
            }

            if (s.showDamageNumbers) DamageNumberDisplay.Show(point, damage, kind >= ImpactKind.Heavy, s);
        }
    }
}
