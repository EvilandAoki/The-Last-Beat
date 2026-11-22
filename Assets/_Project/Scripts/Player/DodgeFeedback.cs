using TheLastBeat.Combat;
using TheLastBeat.Core;
using UnityEngine;

namespace TheLastBeat.Player
{
    /// <summary>
    /// Aviso visual de la esquiva automática (tarea 19): un "¡ESQUIVA!" rojo con contorno negro que sube sobre el
    /// caballero y se desvanece. Va en el mismo objeto que KnightAutoDodge. Se dibuja con IMGUI sobre la posición del
    /// caballero en pantalla, así se lee sobre cualquier suelo; el HUD real (tarea 34) puede sustituirlo.
    /// </summary>
    [RequireComponent(typeof(KnightAutoDodge))]
    public class DodgeFeedback : MonoBehaviour
    {
        [SerializeField] private string message = "¡ESQUIVA!";
        [SerializeField] private Color color = new Color(0.92f, 0.08f, 0.08f);
        [SerializeField, Min(0.1f)] private float duration = 0.9f;
        [Tooltip("Altura inicial del texto sobre el suelo, en metros.")]
        [SerializeField, Min(0f)] private float startHeight = 2.4f;
        [Tooltip("Cuánto sube el texto mientras se desvanece, en metros.")]
        [SerializeField, Min(0f)] private float rise = 1f;
        [SerializeField, Min(8)] private int fontSize = 30;

        private KnightAutoDodge dodge;
        private float startedAt = float.NegativeInfinity;

        private void Awake() => dodge = GetComponent<KnightAutoDodge>();
        private void OnEnable() => dodge.Dodged += OnDodged;
        private void OnDisable() => dodge.Dodged -= OnDodged;

        private void OnDodged(DamageInfo info) => startedAt = Time.time;

        private void OnGUI()
        {
            float t = (Time.time - startedAt) / duration;
            if (t < 0f || t >= 1f) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * (startHeight + rise * t));
            if (screen.z <= 0f) return;

            float alpha = 1f - t * t;
            var area = new Rect(screen.x - 150f, Screen.height - screen.y - 20f, 300f, 40f);
            GuiText.DrawOutlined(area, message, fontSize, new Color(color.r, color.g, color.b, alpha), new Color(0f, 0f, 0f, alpha));
        }
    }
}
