using System.Collections;
using TheLastBeat.VFX;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Giro con explosión final (tarea 10): el caballero gira golpeando todo a su alrededor cada pocos
    /// décimas de segundo y termina con una explosión en área mayor. Se puede mover lento mientras gira.
    /// Usa los datos de AbilityData: damage es el daño de cada golpe del giro, radius el radio del giro,
    /// y las mejoras escalan daño, radio y recarga de todo (giro y explosión).
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Giro con explosión", fileName = "GiroConExplosion")]
    public class SpinAbilityData : AbilityData
    {
        [Header("Giro")]
        [Tooltip("Segundos que dura el giro.")]
        [Min(0.1f)] public float spinDuration = 1.4f;
        [Tooltip("Vueltas completas durante el giro.")]
        [Min(0.5f)] public float turns = 3f;
        [Tooltip("Cada cuántos segundos golpea el giro a los enemigos del área.")]
        [Min(0.02f)] public float tickInterval = 0.2f;
        [Tooltip("Velocidad de movimiento mientras gira (1 = normal, 0 = quieto).")]
        [Range(0f, 1f)] public float moveSpeedWhileSpinning = 0.35f;
        [Tooltip("Empuje de cada golpe del giro.")]
        [Min(0f)] public float tickKnockback = 2f;

        [Header("Explosión final")]
        [Tooltip("Daño de la explosión = damage por este factor.")]
        [Min(0f)] public float explosionDamageMultiplier = 3f;
        [Tooltip("Radio de la explosión = radius por este factor.")]
        [Min(0.1f)] public float explosionRadiusMultiplier = 1.6f;
        [Min(0f)] public float explosionKnockback = 10f;
        [Tooltip("Pausa, en segundos, entre acabar el giro y la explosión.")]
        [Min(0f)] public float explosionDelay = 0.12f;
        [Tooltip("Segundos que el héroe queda quieto tras la explosión.")]
        [Min(0f)] public float recovery = 0.4f;
        [Tooltip("Estado del Animator que se reproduce en la explosión (el golpe vertical).")]
        public string explosionState = "Heavy1";
        [Tooltip("Punto del clip de la explosión (0-1) en el que entra, para llegar directo al golpe.")]
        [Range(0f, 1f)] public float explosionStateOffset = 0.4f;

        public override IEnumerator Cast(AbilityContext context)
        {
            AbilityCaster caster = context.Caster;
            Transform model = caster.Animator != null ? caster.Animator.transform : null;
            Quaternion baseRotation = model != null ? model.localRotation : Quaternion.identity;
            if (model != null) context.RegisterCleanup(() => model.localRotation = baseRotation);

            // Giro: el modelo da vueltas y el héroe puede desplazarse despacio. Un anillo de luz gira con él.
            context.SetMovementSpeed(moveSpeedWhileSpinning);
            PooledEffect spinFx = Vfx.Follow(VfxKind.Spin, context.Transform, Vector3.up, context.Radius);
            context.RegisterCleanup(() => Vfx.Stop(ref spinFx));
            float elapsed = 0f;
            float nextTick = 0f;
            while (elapsed < spinDuration)
            {
                elapsed += Time.deltaTime;
                if (model != null)
                    model.localRotation = baseRotation * Quaternion.Euler(0f, 360f * turns * Mathf.Clamp01(elapsed / spinDuration), 0f);

                if (elapsed >= nextTick)
                {
                    caster.HitArea(Center(context), context.Radius, context.Damage, tickKnockback, false);
                    nextTick += tickInterval;
                }

                yield return null;
            }

            // Explosión: se queda quieto, el modelo vuelve al frente y golpea con un área mayor.
            Vfx.Stop(ref spinFx);
            if (model != null) model.localRotation = baseRotation;
            context.SetMovementSpeed(1f);
            caster.LockMovement();
            caster.PlayAnimation(explosionState, 0.08f, explosionStateOffset);
            if (explosionDelay > 0f) yield return new WaitForSeconds(explosionDelay);

            Vector3 center = Center(context);
            caster.HitArea(center, context.Radius * explosionRadiusMultiplier,
                context.Damage * explosionDamageMultiplier, explosionKnockback, true);
            Vfx.Play(VfxKind.Explosion, context.Transform.position + Vector3.up * 0.05f, context.Radius * explosionRadiusMultiplier);

            if (recovery > 0f) yield return new WaitForSeconds(recovery);
        }

        private static Vector3 Center(AbilityContext context) => context.Transform.position + Vector3.up;
    }
}
