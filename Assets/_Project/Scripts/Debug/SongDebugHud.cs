using TheLastBeat.Buffs;
using TheLastBeat.Combat;
using TheLastBeat.Companions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Muestra en pantalla el estado de la canción y si el caballero está potenciado (tareas 17 y 18). Teclas de prueba:
    /// I = interrumpir directamente, O = golpear a la bardo con un golpe que interrumpe, P = golpe que NO interrumpe.
    /// Se reemplaza por el HUD real (tarea 34); bórralo cuando ya no lo necesites.
    /// </summary>
    public class SongDebugHud : MonoBehaviour
    {
        [SerializeField] private SongBuff song;
        [SerializeField] private BuffReceiver knight;
        [SerializeField] private Key interruptKey = Key.I;
        [SerializeField] private Key hitInterruptsKey = Key.O;
        [SerializeField] private Key hitNoInterruptKey = Key.P;
        [SerializeField, Min(0f)] private float testDamage = 5f;

        private Health bardHealth;
        private float messageUntil;
        private bool messageIsRepeat;
        private int interruptions;

        private void Awake()
        {
            if (song == null) song = FindFirstObjectByType<SongBuff>();
            if (song != null) bardHealth = song.GetComponent<Health>();
            if (knight == null)
            {
                var player = FindFirstObjectByType<TheLastBeat.Player.PlayerController>();
                if (player != null) knight = player.GetComponent<BuffReceiver>();
            }
        }

        private void OnEnable()
        {
            if (song != null) song.OnInterrupted += OnInterrupted;
        }

        private void OnDisable()
        {
            if (song != null) song.OnInterrupted -= OnInterrupted;
        }

        private void OnInterrupted(bool restarted)
        {
            interruptions++;
            messageIsRepeat = restarted;
            messageUntil = Time.time + 1.2f;
        }

        private void Update()
        {
            if (song == null || Keyboard.current == null) return;
            if (Keyboard.current[interruptKey].wasPressedThisFrame) song.Interrupt();
            if (Keyboard.current[hitInterruptsKey].wasPressedThisFrame) HitBard(true);
            if (Keyboard.current[hitNoInterruptKey].wasPressedThisFrame) HitBard(false);
        }

        private void HitBard(bool interrupts)
        {
            if (bardHealth == null) return;
            DamageInfo info = DamageInfo.Create(testDamage, gameObject, Team.Demons, bardHealth.transform.position + Vector3.up,
                Vector3.forward, 0f, interrupts, false);
            bardHealth.TakeDamage(info);
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
            GUI.Label(new Rect(x, y, width + 200f, height), $"[{interruptKey}] interrumpir   [{hitInterruptsKey}] golpe a la bardo (interrumpe)   [{hitNoInterruptKey}] golpe (no interrumpe)");
            y += height;
            if (bardHealth != null)
                GUI.Label(new Rect(x, y, width + 200f, height), $"Bardo: vida {bardHealth.CurrentHealth:0}/{bardHealth.MaxHealth:0}   interrupciones: {interruptions}");

            // Aviso grande en el centro: blanco con contorno oscuro y fondo, para que se lea sobre cualquier suelo o ambiente.
            if (Time.time < messageUntil)
            {
                string text = messageIsRepeat ? "¡CONTADOR REINICIADO!" : "¡CANCIÓN INTERRUMPIDA!";
                DrawBanner(new Rect(0f, Screen.height * 0.18f, Screen.width, 64f), text);
            }
        }

        private static void DrawBanner(Rect area, string text)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // Fondo oscuro semitransparente detrás del texto.
            float width = Mathf.Min(area.width, style.CalcSize(new GUIContent(text)).x + 60f);
            Rect panel = new Rect(area.x + (area.width - width) * 0.5f, area.y, width, area.height);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            // Contorno negro (ocho desplazamientos) y texto blanco encima.
            style.normal.textColor = Color.black;
            for (int dx = -2; dx <= 2; dx += 2)
                for (int dy = -2; dy <= 2; dy += 2)
                    if (dx != 0 || dy != 0)
                        GUI.Label(new Rect(area.x + dx, area.y + dy, area.width, area.height), text, style);
            style.normal.textColor = Color.white;
            GUI.Label(area, text, style);
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
