using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Menú principal (tarea 35): el título del juego, Jugar (carga Pueblo) y Salir (cierra el juego; en el editor sale
    /// de Play). Construye su propio Canvas por código con el estilo del HUD y crea un EventSystem si la escena no tiene.
    /// Jugar queda seleccionado para poder empezar con Enter o con el mando.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Nombre exacto de la escena del nivel en Build Profiles.")]
        [SerializeField] private string gameSceneName = "Pueblo";

        [Header("Textos")]
        [SerializeField] private string title = "EL ÚLTIMO COMPÁS";
        [SerializeField] private string subtitle = "The Last Beat";
        [SerializeField] private string controls = "Mover: WASD o flechas   ·   Atacar: J o clic   ·   Golpe fuerte: K   ·   Habilidades: Q y E";
        [SerializeField] private string footer = "Desarrollo de Videojuegos · Univalle";
        [SerializeField] private Color titleColor = new Color(0.85f, 0.6f, 1f);

        private Button play;

        private void Awake()
        {
            Time.timeScale = 1f;
            UiKit.EnsureEventSystem();
            Build();
        }

        private void Start() => UiKit.Select(play);

        private void Play()
        {
            if (Application.CanStreamedLevelBeLoaded(gameSceneName)) SceneManager.LoadScene(gameSceneName);
            else Debug.LogError($"[MainMenuUI] La escena '{gameSceneName}' no está en Build Profiles. Ejecuta The Last Beat > Menús > Crear menú principal y pantallas de fin.", this);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Build()
        {
            Transform canvas = UiKit.CreateCanvas(transform, "CanvasMenu", 0, true).transform;
            RectTransform root = UiKit.Stretch(canvas, "Menu");
            UiKit.Fill(root, new Color(0.07f, 0.04f, 0.12f, 1f));

            UiKit.Label(UiKit.Rect(root, "Titulo", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 300f), new Vector2(-160f, 140f)), title, 110, TextAnchor.MiddleCenter, titleColor);
            UiKit.Label(UiKit.Rect(root, "Subtitulo", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 195f), new Vector2(-160f, 60f)), subtitle, 40, TextAnchor.MiddleCenter, Color.white);

            play = UiKit.CreateButton(root, "Jugar", "JUGAR", new Vector2(0f, 20f), new Vector2(420f, 90f), Play);
            UiKit.CreateButton(root, "Salir", "SALIR", new Vector2(0f, -95f), new Vector2(420f, 90f), Quit);

            UiKit.Label(UiKit.Rect(root, "Controles", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(-160f, 40f)), controls, 26, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.9f));
            UiKit.Label(UiKit.Rect(root, "Pie", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(-160f, 40f)), footer, 24, TextAnchor.MiddleCenter, new Color(0.65f, 0.65f, 0.7f));
        }
    }
}
