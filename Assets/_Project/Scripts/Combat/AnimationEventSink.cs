using UnityEngine;

namespace TheLastBeat.Combat
{
    /// <summary>
    /// Receptor vacío de eventos de animación para personajes que comparten clips con el caballero: el soldado ataca
    /// con Light1, que trae el evento OnHit del combo. En el soldado el daño lo decide su IA (EnemySoldier), así que
    /// el evento no hace nada; sin un receptor, Unity mostraría un error cada vez que suena.
    /// </summary>
    public class AnimationEventSink : MonoBehaviour
    {
        public void OnHit() { }
    }
}
