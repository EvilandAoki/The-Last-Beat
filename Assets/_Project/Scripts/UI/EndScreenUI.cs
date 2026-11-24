using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Pantallas de victoria y derrota (tarea 35). Aparecen cuando LevelFlow termina el nivel, por encima del HUD, con
    /// Reintentar (vuelve a cargar Pueblo desde cero) y Menú (vuelve al menú principal). En la derrota dice qué héroe cayó.
    /// Construye su propio Canvas por código y crea un EventSystem si la escena no tiene, para que los botones respondan
    /// al ratón, al teclado y al mando (el primer botón queda seleccionado).
    /// </summary>
    public class EndScreenUI : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;

        [Header("Victoria")]
        [SerializeField] private string victoryTitle = "¡EL PUEBLO RECUPERÓ SU RITMO!";
        [SerializeField] private string victorySubtitle = "La música volvió a las calles.";
        [SerializeField] private Color victoryColor = new Color(1f, 0.85f, 0.35f);

        [Header("Derrota")]
        [SerializeField] private string defeatTitle = "LA MÚSICA SE APAGÓ";
        [SerializeField] private string defeatHint = "Protege a la bardo: sin su canción no hay victoria.";
        [SerializeField] private Color defeatColor = new Color(0.95f, 0.3f, 0.3f);

        private GameObject screen;
        private Text title;
        private Text subtitle;
        private Button retry;

        private void Awake()
        {
            if (flow == null) flow = FindFirstObjectByType<LevelFlow>();
            UiKit.EnsureEventSystem();
            Build();
            screen.SetActive(false);
        }

        private void OnEnable()
        {
            if (flow != null) flow.StateChanged += HandleLevelState;
        }

        private void OnDisable()
        {
            if (flow != null) flow.StateChanged -= HandleLevelState;
        }

        private void HandleLevelState(GameState state)
        {
            if (state == GameState.Victory) Open(victoryTitle, victorySubtitle, victoryColor);
            else if (state == GameState.Defeat) Open(defeatTitle, DefeatText(), defeatColor);
        }

        private string DefeatText()
        {
            if (flow == null || string.IsNullOrEmpty(flow.DefeatReason)) return defeatHint;
            return flow.DefeatReason + ".\n" + defeatHint;
        }

        private void Open(string heading, string body, Color color)
        {
            title.text = heading;
            title.color = color;
            subtitle.text = body;
            screen.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            UiKit.Select(retry);
        }

        private void Retry()
        {
            if (flow != null) flow.Restart();
        }

        private void Menu()
        {
            if (flow != null) flow.GoToMenu();
        }

        private void Build()
        {
            Transform canvas = UiKit.CreateCanvas(transform, "CanvasFin", 30, true).transform;
            RectTransform root = UiKit.Stretch(canvas, "PantallaFin");
            UiKit.Fill(root, new Color(0.03f, 0.02f, 0.06f, 0.82f), true); // tapa el juego y bloquea clics detrás
            screen = root.gameObject;

            title = UiKit.Label(UiKit.Rect(root, "Titulo", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 230f), new Vector2(-160f, 120f)), string.Empty, 72, TextAnchor.MiddleCenter, Color.white);
            subtitle = UiKit.Label(UiKit.Rect(root, "Subtitulo", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 110f), new Vector2(-400f, 110f)), string.Empty, 34, TextAnchor.MiddleCenter, Color.white);

            retry = UiKit.CreateButton(root, "Reintentar", "REINTENTAR", new Vector2(0f, -40f), new Vector2(420f, 86f), Retry);
            UiKit.CreateButton(root, "Menu", "MENÚ", new Vector2(0f, -150f), new Vector2(420f, 86f), Menu);
        }
    }
}
