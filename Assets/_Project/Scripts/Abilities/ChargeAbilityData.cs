using System.Collections;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Embestida (tarea 11): carga corta hacia delante que golpea todo lo que hay en la línea. Sirve para llegar
    /// rápido a un enemigo que va por la bardo. Usa los datos de AbilityData: damage es el daño de la carga a cada
    /// enemigo (una sola vez por carga), radius el semiancho de la línea de golpe y las mejoras escalan ambos.
    ///
    /// Golpea con OverlapBoxNonAlloc a lo largo del recorrido, frame a frame. No atraviesa paredes: la distancia se
    /// recorta con un SphereCast contra las capas de obstáculos y el CharacterController hace el resto. Sí atraviesa a
    /// los enemigos (los golpea y los empuja) para poder llegar hasta el que va por la bardo.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Habilidades/Embestida", fileName = "Embestida")]
    public class ChargeAbilityData : AbilityData
    {
        [Header("Embestida")]
        [Tooltip("Metros que avanza como máximo (menos si hay una pared).")]
        [Min(0.5f)] public float distance = 7f;
        [Tooltip("Segundos que dura la carga: la velocidad es distance / chargeDuration.")]
        [Min(0.05f)] public float chargeDuration = 0.35f;
        [Tooltip("Pausa, en segundos, antes de salir disparado.")]
        [Min(0f)] public float windup = 0.1f;
        [Tooltip("Segundos que queda quieto tras la carga.")]
        [Min(0f)] public float recovery = 0.25f;
        [Tooltip("Alto de la caja de golpe (la mitad hacia arriba y hacia abajo).")]
        [Min(0.1f)] public float halfHeight = 1f;

        public override IEnumerator Cast(AbilityContext context)
        {
            AbilityCaster caster = context.Caster;
            var controller = context.Controller;
            Transform body = context.Transform;
            var character = body.GetComponent<CharacterController>();

            // Durante la carga el jugador no controla al héroe.
            caster.LockMovement();
            controller.StopImmediately();
            if (controller.DesiredDirection.sqrMagnitude > 0.01f) controller.FaceDirection(controller.DesiredDirection);
            Vector3 forward = body.forward;
            forward.y = 0f;
            forward.Normalize();
            Quaternion boxRotation = Quaternion.LookRotation(forward);

            if (windup > 0f) yield return new WaitForSeconds(windup);

            // Atraviesa a los enemigos (los golpea), pero no las paredes.
            LayerMask previousExclude = character.excludeLayers;
            character.excludeLayers = previousExclude.value | caster.HitMask.value;
            context.RegisterCleanup(() => character.excludeLayers = previousExclude);

            // Distancia real: se recorta si hay una pared delante.
            float planned = distance;
            Vector3 origin = body.position + character.center;
            if (Physics.SphereCast(origin, character.radius * 0.9f, forward, out RaycastHit wall,
                    distance + character.skinWidth, TheLastBeat.Core.GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
                planned = Mathf.Max(0f, wall.distance - character.skinWidth);

            var alreadyHit = caster.NewHitSession();
            float speed = distance / chargeDuration;
            float traveled = 0f;
            int blockedFrames = 0;
            while (traveled < planned)
            {
                float step = Mathf.Min(speed * Time.deltaTime, planned - traveled);
                Vector3 before = body.position;
                controller.ExternalMove(forward * step);
                Vector3 moved = body.position - before;
                moved.y = 0f;
                float length = moved.magnitude;
                traveled += length;

                // Caja sobre el tramo recorrido en este frame (con el radio del héroe para no dejar huecos).
                Vector3 center = (before + body.position) * 0.5f + Vector3.up * halfHeight;
                Vector3 halfExtents = new Vector3(context.Radius, halfHeight, length * 0.5f + character.radius);
                caster.HitBox(center, halfExtents, boxRotation, context.Damage, knockback, true, alreadyHit);

                // Si algo lo frena (pared que el SphereCast no vio), termina la carga.
                if (length < step * 0.25f)
                {
                    if (++blockedFrames >= 2) break;
                }
                else
                {
                    blockedFrames = 0;
                }

                yield return null;
            }

            if (recovery > 0f) yield return new WaitForSeconds(recovery);
        }
    }
}
