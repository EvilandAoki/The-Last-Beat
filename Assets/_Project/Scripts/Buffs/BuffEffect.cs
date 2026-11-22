using UnityEngine;

namespace TheLastBeat.Buffs
{
    /// <summary>
    /// Lo que una potenciación (como la canción de la bardo) le da a quien la recibe. Es un ScriptableObject: los
    /// valores se editan en el asset, sin tocar código. Los valores de varias fuentes se combinan en el BuffReceiver.
    /// Crear uno nuevo: Create > The Last Beat > Potenciación.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Potenciación", fileName = "Potenciacion")]
    public class BuffEffect : ScriptableObject
    {
        [Tooltip("Multiplicador del daño del héroe (1 = sin cambio).")]
        [Min(1f)] public float damageMultiplier = 1.25f;
        [Tooltip("Multiplicador del área de las habilidades (1 = sin cambio).")]
        [Min(1f)] public float abilityAreaMultiplier = 1.3f;
        [Tooltip("Probabilidad (0-1) de evitar un golpe.")]
        [Range(0f, 1f)] public float dodgeChance = 0.25f;
    }
}
