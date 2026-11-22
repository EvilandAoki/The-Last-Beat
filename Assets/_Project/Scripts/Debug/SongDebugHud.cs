using TheLastBeat.Buffs;
using TheLastBeat.Companions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Muestra en pantalla el estado de la canción y si el caballero está potenciado (tarea 17). Con la tecla I
    /// interrumpe la canción para probar los estados Interrumpida y Recuperando sin esperar al daño (tarea 18).
    /// Se reemplaza por el HUD real (tarea 34); bórralo cuando ya no lo necesites.
    /// </summary>
    public class SongDebugHud : MonoBehaviour
    {
        [SerializeField] private SongBuff song;
        [SerializeField] private BuffReceiver knight;
        [SerializeField] private Key interruptKey = Key.I;

        private void Awake()
        {
            if (song == null) song = FindFirstObjectByType<SongBuff>();
            if (knight == null)
            {
                var player = FindFirstObjectByType<TheLastBeat.Player.PlayerController>();
                if (player != null) knight = player.GetComponent<BuffReceiver>();
            }
        }

        private void Update()
        {
            if (song != null && Keyboard.current != null && Keyboard.current[interruptKey].wasPressedThisFrame)
                song.Interrupt();
        }

        private void OnGUI()
        {
            const float width = 300f, height = 22f;
            float x = 16f, y = 16f;

            string songLine = song == null ? "Canción: (sin bardo)" : song.State switch
            {
                SongState.Singing => $"Canción: CANTANDO  (aliados: {song.AlliesInRange})",
                SongState.Interrupted => $"Canción: INTERRUMPIDA  {song.StateTimeRemaining:0.0}s",
                _ => $"Canción: RECUPERANDO  {song.StateTimeRemaining:0.0}s",
            };
            Draw(new Rect(x, y, width, height), songLine, song != null && song.IsSinging ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.3f));
            y += height + 4f;

            string buffLine = knight == null
                ? "Caballero: (sin BuffReceiver)"
                : knight.IsBuffed
                    ? $"Caballero: POTENCIADO  daño x{knight.DamageMultiplier:0.##}  área x{knight.AreaMultiplier:0.##}  esquiva {knight.DodgeChance:P0}"
                    : "Caballero: sin potenciar";
            Draw(new Rect(x, y, width + 140f, height), buffLine, knight != null && knight.IsBuffed ? new Color(0.4f, 0.8f, 1f) : new Color(0.6f, 0.6f, 0.6f));
            y += height + 4f;
            GUI.Label(new Rect(x, y, width, height), $"[{interruptKey}] interrumpir la canción (prueba)");
        }

        private static void Draw(Rect box, string text, Color color)
        {
            GUI.Box(box, GUIContent.none);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(box.x, box.y, 5f, box.height), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(box.x + 10f, box.y + 2f, box.width, box.height), text);
        }
    }
}
