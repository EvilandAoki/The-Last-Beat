using System.Collections;
using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Lo que se ve del flujo del nivel (tarea 20), con el estilo del HUD y sin tocarlo:
    ///  - En el centro, los mensajes de LevelFlow ("El pueblo está en silencio...", el nombre de cada horda,
    ///    "¡Horda superada!"): aparecen, se quedan un momento y se desvanecen.
    ///  - Arriba a la derecha, durante el combate, la horda actual y los enemigos que quedan; al final, "PUEBLO LIBERADO".
    /// Construye su propio Canvas por código. Usa tiempo real para no congelarse en la derrota (timeScale 0).
    /// </summary>
    public class LevelBannerUI : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;

        [Header("Mensaje central")]
        [SerializeField, Min(0f)] private float fadeIn = 0.25f;
        [SerializeField, Min(0f)] private float hold = 2.2f;
        [SerializeField, Min(0f)] private float fadeOut = 0.6f;

        [Header("Hordas")]
        [SerializeField] private Color waveColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField] private string clearedText = "PUEBLO LIBERADO";

        private WaveSpawner spawner;
        private CanvasGroup bannerGroup;
        private Text bannerText;
        private GameObject wavePanel;
        private Text waveText;
        private Text enemiesText;
        private Coroutine running;
        private int shownAlive = -1;
        private bool subscribed;

        private void Awake()
        {
            if (flow == null) flow = FindFirstObjectByType<LevelFlow>();
            spawner = flow != null ? flow.Spawner : FindFirstObjectByType<WaveSpawner>();
            Build();
        }

        private void OnEnable() => Subscribe(true);
        private void OnDisable() => Subscribe(false);

        private void Subscribe(bool on)
        {
            if (on == subscribed) return;
            subscribed = on;

            if (flow != null)
            {
                if (on) flow.Message += Show;
                else flow.Message -= Show;
            }

            if (spawner != null)
            {
                if (on)
                {
                    spawner.OnWaveStarted += HandleWaveStarted;
                    spawner.OnAllWavesCleared += HandleAllCleared;
                }
                else
                {
                    spawner.OnWaveStarted -= HandleWaveStarted;
                    spawner.OnAllWavesCleared -= HandleAllCleared;
                }
            }
        }

        /// <summary>Muestra un mensaje en el centro de la pantalla.</summary>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            bannerText.text = message;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(PlayBanner());
        }

        private void HandleWaveStarted(int index, Wave wave)
        {
            wavePanel.SetActive(true);
            waveText.text = $"HORDA {index + 1} / {spawner.WaveCount}";
            shownAlive = -1;
        }

        private void HandleAllCleared()
        {
            wavePanel.SetActive(true);
            waveText.text = clearedText;
            enemiesText.text = string.Empty;
        }

        private void Update()
        {
            // El contador cambia muchas veces por segundo en combate: solo se reescribe cuando cambia el número.
            if (spawner == null || !spawner.IsRunning || !wavePanel.activeSelf) return;
            int alive = spawner.AliveCount;
            if (alive == shownAlive) return;
            shownAlive = alive;
            enemiesText.text = "Enemigos: " + alive;
        }

        private IEnumerator PlayBanner()
        {
            yield return Fade(bannerGroup.alpha, 1f, fadeIn);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeOut);
            running = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                bannerGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            bannerGroup.alpha = to;
        }

        // ---------------------------------------------------------------- Construcción

        private void Build()
        {
            Transform root = UiKit.CreateCanvas(transform, "CanvasNivel", 20, false).transform;

            // Franja central para los mensajes.
            RectTransform band = UiKit.Rect(root, "Mensaje", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 220f), new Vector2(0f, 110f));
            UiKit.Fill(band, new Color(0f, 0f, 0f, 0.45f));
            bannerGroup = band.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerText = UiKit.Label(UiKit.Stretch(band, "Texto"), string.Empty, 56, TextAnchor.MiddleCenter, Color.white);

            // Panel de hordas, arriba a la derecha (el HUD ocupa arriba a la izquierda y abajo al centro).
            RectTransform panel = UiKit.Rect(root, "Hordas", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -32f), new Vector2(420f, 110f));
            UiKit.Fill(panel, UiKit.PanelColor);
            waveText = UiKit.Label(UiKit.Rect(panel, "Horda", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(-32f, 52f)), string.Empty, 40, TextAnchor.MiddleCenter, waveColor);
            enemiesText = UiKit.Label(UiKit.Rect(panel, "Enemigos", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(-32f, 40f)), string.Empty, 28, TextAnchor.MiddleCenter, Color.white);
            wavePanel = panel.gameObject;
            wavePanel.SetActive(false);
        }
    }
}
