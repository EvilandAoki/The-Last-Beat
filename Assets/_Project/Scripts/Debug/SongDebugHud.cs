using TheLastBeat.Buffs;
using TheLastBeat.Combat;
using TheLastBeat.Companions;
using TheLastBeat.Core;
using TheLastBeat.Player;
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
        [SerializeField] private Key hitKnightKey = Key.L;
        [SerializeField, Min(0f)] private float testDamage = 5f;

        private Health knightHealth;
        private KnightAutoDodge knightDodge;

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

        private void Start()
        {
            if (knight == null) return;
            knightHealth = knight.GetComponent<Health>();
            knightDodge = knight.GetComponent<KnightAutoDodge>();
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
            if (Keyboard.current[hitKnightKey].wasPressedThisFrame) HitKnight();
        }

        private void HitKnight()
        {
            if (knightHealth == null) return;
            DamageInfo info = DamageInfo.Create(testDamage, gameObject, Team.Demons, knightHealth.transform.position + Vector3.up,
                Vector3.forward, 0f, true, false);
            knightHealth.TakeDamage(info);
        }

        private void HitBard(bool interrupts)
        {
            if (bardHealth == null) return;
            DamageInfo info = DamageInfo.Create(testDamage, gameObject, Team.Demons, bardHealth.transform.position + Vector3.up,
                Vector3.forward, 0f, interrupts, false);
            bardHealth.TakeDamage(info);
        }

        private static readonly Color TextColor = new Color(0.92f, 0.08f, 0.08f);
        private static readonly Color OutlineColor = Color.black;

        private void OnGUI()
        {
            float y = 16f;

            string songLine = song == null ? "Canción: (sin bardo)" : song.State switch
            {
                SongState.Singing => $"Canción: CANTANDO  (aliados: {song.AlliesInRange})",
                SongState.Interrupted => $"Canción: INTERRUMPIDA  {song.StateTimeRemaining:0.0}s",
                _ => $"Canción: RECUPERANDO  {song.StateTimeRemaining:0.0}s",
            };
            Line(ref y, songLine);

            Line(ref y, knight == null
                ? "Caballero: (sin BuffReceiver)"
                : knight.IsBuffed
                    ? $"Caballero: POTENCIADO  daño x{knight.DamageMultiplier:0.##}  área x{knight.AreaMultiplier:0.##}  esquiva {knight.DodgeChance:P0}"
                    : "Caballero: sin potenciar");

            if (bardHealth != null)
                Line(ref y, $"Bardo: vida {bardHealth.CurrentHealth:0}/{bardHealth.MaxHealth:0}   interrupciones: {interruptions}");
            if (knightHealth != null)
                Line(ref y, $"Caballero: vida {knightHealth.CurrentHealth:0}/{knightHealth.MaxHealth:0}   esquivas: {(knightDodge != null ? knightDodge.DodgeCount : 0)}");

            Line(ref y, $"[{interruptKey}] interrumpir   [{hitInterruptsKey}] golpe a la bardo (interrumpe)   [{hitNoInterruptKey}] (no interrumpe)");
            Line(ref y, $"[{hitKnightKey}] golpe al caballero (prueba de esquiva)");

            // Aviso grande en el centro, también en rojo con contorno negro.
            if (Time.time < messageUntil)
            {
                string text = messageIsRepeat ? "¡CONTADOR REINICIADO!" : "¡CANCIÓN INTERRUMPIDA!";
                GuiText.DrawOutlined(new Rect(0f, Screen.height * 0.18f, Screen.width, 64f), text, 36, TextColor, OutlineColor, 2f);
            }
        }

        /// <summary>Una línea de texto a la izquierda: rojo con contorno negro, legible sobre un suelo blanco o uno oscuro.</summary>
        private static void Line(ref float y, string text)
        {
            const float height = 22f;
            GuiText.DrawOutlined(new Rect(16f, y, Screen.width - 32f, height), text, 15, TextColor, OutlineColor, 1f, TextAnchor.MiddleLeft);
            y += height;
        }
    }
}
