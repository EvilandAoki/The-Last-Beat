using System.Collections;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Habilidad de área sencilla: el héroe se detiene, espera la anticipación y daña a su alrededor.
    /// Sirve como ejemplo de cómo crear una habilidad (hereda de AbilityData e implementa Cast) y como base
    /// de pruebas del sistema. El giro con explosión final (#10) y la embestida (#11) siguen este mismo patrón.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Onda de choque", fileName = "OndaDeChoque")]
    public class ShockwaveAbilityData : AbilityData
    {
        [Header("Onda de choque")]
        [Tooltip("Segundos entre pulsar y el impacto.")]
        [Min(0f)] public float windup = 0.3f;
        [Tooltip("Segundos que el héroe sigue quieto después del impacto.")]
        [Min(0f)] public float recovery = 0.25f;
        [Tooltip("Distancia, delante del héroe, a la que está el centro del área. 0 = alrededor del héroe.")]
        public float forwardOffset;

        public override IEnumerator Cast(AbilityContext context)
        {
            AbilityCaster caster = context.Caster;
            caster.LockMovement();

            if (windup > 0f) yield return new WaitForSeconds(windup);

            Vector3 center = context.Transform.position + Vector3.up + context.Transform.forward * forwardOffset;
            caster.HitArea(center, context.Radius, context.Damage, knockback);

            if (recovery > 0f) yield return new WaitForSeconds(recovery);
            // El AbilityCaster libera el bloqueo de movimiento al terminar o si se cancela.
        }
    }
}
