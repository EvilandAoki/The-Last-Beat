using TheLastBeat.Abilities;
using UnityEngine;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Muestra en pantalla la recarga de las dos habilidades (tarea 9). Demuestra cómo la interfaz puede leer
    /// CooldownNormalized y CooldownRemaining. Se reemplaza por el HUD real (tarea 34); bórralo cuando ya no lo necesites.
    /// </summary>
    public class AbilityDebugHud : MonoBehaviour
    {
        [SerializeField] private AbilityCaster caster;

        private void Awake()
        {
            if (caster == null) caster = FindFirstObjectByType<AbilityCaster>();
        }

        private void OnGUI()
        {
            if (caster == null) return;

            const float width = 220f, height = 22f;
            float x = 16f, y = Screen.height - 16f - AbilityCaster.SlotCount * (height + 6f);
            for (int slot = 0; slot < AbilityCaster.SlotCount; slot++)
            {
                AbilityData ability = caster.GetAbility(slot);
                string label = ability != null
                    ? $"{(slot == 0 ? "Q" : "E")}  {ability.displayName}  (nv {caster.GetLevel(slot)})"
                    : $"{(slot == 0 ? "Q" : "E")}  (vacía)";
                float remaining = caster.CooldownRemaining(slot);
                if (remaining > 0f) label += $"  {remaining:0.0}s";

                Rect box = new Rect(x, y, width, height);
                GUI.Box(box, GUIContent.none);
                Color previous = GUI.color;
                GUI.color = remaining > 0f ? new Color(0.9f, 0.4f, 0.3f) : new Color(0.4f, 0.9f, 0.5f);
                GUI.DrawTexture(new Rect(x, y, width * (1f - caster.CooldownNormalized(slot)), height), Texture2D.whiteTexture);
                GUI.color = previous;
                GUI.Label(new Rect(x + 6f, y + 2f, width, height), label);
                y += height + 6f;
            }
        }
    }
}
