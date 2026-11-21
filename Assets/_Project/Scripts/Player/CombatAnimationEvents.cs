using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Va en el objeto del Animator (el modelo). Los Animation Events solo llegan a los componentes de ese
    /// objeto, así que esto reenvía OnHit al PlayerCombat del héroe.
    /// </summary>
    public class CombatAnimationEvents : MonoBehaviour
    {
        private PlayerCombat combat;

        private void Awake() => combat = GetComponentInParent<PlayerCombat>();

        public void OnHit()
        {
            if (combat != null) combat.OnHit();
        }
    }
}
