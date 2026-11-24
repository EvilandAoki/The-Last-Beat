using UnityEngine;

namespace TheLastBeat.Feedback
{
    /// <summary>Tipos de golpe para la sensación de impacto, de menos a más fuerte.</summary>
    public enum ImpactKind
    {
        Tick,       // golpes sueltos y repetidos (el giro): sin pausa, sacudida mínima
        Light,      // ataque débil
        Heavy,      // ataque fuerte y habilidades
        Finisher,   // remate del combo (el "tercer golpe")
        Explosion,  // explosión final del giro
    }

    /// <summary>
    /// Todos los valores de la sensación de impacto (tarea 12): pausa del golpe, sacudida de cámara y números de daño.
    /// Se edita en el asset (Resources/ImpactSettings); si no existe, el juego usa estos mismos valores por defecto.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Ajustes de impacto", fileName = "ImpactSettings")]
    public class ImpactSettings : ScriptableObject
    {
        [Header("Hit stop (segundos de pausa por golpe)")]
        [Min(0f)] public float hitStopLight = 0.04f;
        [Min(0f)] public float hitStopHeavy = 0.07f;
        [Min(0f)] public float hitStopFinisher = 0.09f;
        [Min(0f)] public float hitStopExplosion = 0.11f;
        [Tooltip("Velocidad del tiempo durante la pausa (0 = congelado del todo).")]
        [Range(0f, 0.2f)] public float hitStopTimeScale = 0.02f;
        [Tooltip("Tiempo mínimo entre una pausa y la siguiente: evita que una ráfaga de golpes congele el juego.")]
        [Min(0f)] public float hitStopMinInterval = 0.12f;

        [Header("Sacudida de cámara (fuerza)")]
        [Min(0f)] public float shakeTick = 0.04f;
        [Min(0f)] public float shakeLight = 0.08f;
        [Min(0f)] public float shakeHeavy = 0.2f;
        [Tooltip("El remate del combo (el tercer golpe) sacude más que un golpe fuerte normal.")]
        [Min(0f)] public float shakeFinisher = 0.35f;
        [Tooltip("La explosión final del giro es la sacudida más fuerte.")]
        [Min(0f)] public float shakeExplosion = 0.55f;
        [Min(0.05f)] public float shakeDuration = 0.18f;
        [Min(0.05f)] public float shakeDurationBig = 0.4f;

        [Header("Números de daño")]
        public bool showDamageNumbers = true;
        [Tooltip("Cuántos números caben en el pool y cuántos pueden verse a la vez.")]
        [Min(4)] public int maxNumbers = 40;
        [Min(1)] public int prewarmNumbers = 16;
        [Min(0.1f)] public float numberDuration = 0.8f;
        [Tooltip("Metros que sube el número mientras se desvanece.")]
        [Min(0f)] public float numberRise = 1.4f;
        [Tooltip("Desplazamiento lateral aleatorio, en píxeles de referencia, para que no se tapen.")]
        [Min(0f)] public float numberSpread = 35f;
        [Min(8)] public int fontSize = 36;
        [Min(8)] public int fontSizeBig = 52;
        public Color colorNormal = Color.white;
        public Color colorBig = new Color(1f, 0.65f, 0.15f);

        public float HitStop(ImpactKind kind)
        {
            switch (kind)
            {
                case ImpactKind.Light: return hitStopLight;
                case ImpactKind.Heavy: return hitStopHeavy;
                case ImpactKind.Finisher: return hitStopFinisher;
                case ImpactKind.Explosion: return hitStopExplosion;
                default: return 0f;
            }
        }

        public float Shake(ImpactKind kind)
        {
            switch (kind)
            {
                case ImpactKind.Tick: return shakeTick;
                case ImpactKind.Light: return shakeLight;
                case ImpactKind.Heavy: return shakeHeavy;
                case ImpactKind.Finisher: return shakeFinisher;
                default: return shakeExplosion;
            }
        }
    }
}
