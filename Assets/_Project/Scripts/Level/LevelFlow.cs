using System;
using System.Collections;
using TheLastBeat.Combat;
using TheLastBeat.Companions;
using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.Level
{
    public enum GameState
    {
        /// <summary>El caballero recorre el pueblo en silencio, todavía sin enemigos.</summary>
        Exploring,
        /// <summary>Las hordas están activas.</summary>
        Combat,
        Victory,
        Defeat,
    }

    /// <summary>
    /// Flujo del nivel (tarea 20). Une las piezas que ya existen:
    ///   inicio con el pueblo en silencio → el caballero entra a la plaza (ZoneTrigger llama a StartCombat) → hordas
    ///   (WaveSpawner) → victoria al caer la última horda, o derrota si cae el caballero o la bardo.
    ///
    /// "En silencio": con Silent Start, la canción de la bardo (SongBuff) queda apagada hasta que empieza el combate.
    /// La interfaz no se conoce aquí: StateChanged y Message la avisan (mensajes, tarea 20; pantallas de fin, tarea 35).
    /// Restart vuelve a cargar la escena, así que se reinicia sin salir de Play ni recargar el editor.
    /// Se ejecuta antes que los demás scripts para apagar la canción antes de que empiece a sonar.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LevelFlow : MonoBehaviour
    {
        [Header("Referencias (vacío = se buscan en la escena)")]
        [SerializeField] private WaveSpawner spawner;
        [SerializeField] private PlayerController knight;
        [SerializeField] private BardCompanion bard;

        [Header("Inicio")]
        [Tooltip("La bardo no canta hasta que empieza el combate: el pueblo llega en silencio.")]
        [SerializeField] private bool silentStart = true;
        [Tooltip("Segundos antes de mostrar el mensaje de inicio.")]
        [SerializeField, Min(0f)] private float introDelay = 0.75f;

        [Header("Final")]
        [Tooltip("Pausa entre la caída de la última horda y la pantalla de victoria.")]
        [SerializeField, Min(0f)] private float victoryDelay = 1.5f;
        [Tooltip("Pausa entre la caída de un héroe y la pantalla de derrota.")]
        [SerializeField, Min(0f)] private float defeatDelay = 1.5f;

        [Header("Mensajes")]
        [SerializeField] private string introMessage = "El pueblo está en silencio...";
        [SerializeField] private string combatMessage = "¡Defiende la canción!";
        [SerializeField] private string waveClearedMessage = "¡Horda superada!";
        [SerializeField] private string knightDownMessage = "El caballero cayó";
        [SerializeField] private string bardDownMessage = "La bardo cayó: la música se apagó";

        [Header("Escenas")]
        [Tooltip("Nombre de la escena del menú principal (tarea 35). Si no está en la build, Menú reinicia el nivel.")]
        [SerializeField] private string menuSceneName = "MainMenu";

        private Health knightHealth;
        private PlayerInputReader input;
        private SongBuff song;
        private bool ending;

        public GameState State { get; private set; } = GameState.Exploring;
        public WaveSpawner Spawner => spawner;

        /// <summary>Por qué terminó en derrota (el mensaje del héroe que cayó). Vacío si no hubo derrota.</summary>
        public string DefeatReason { get; private set; } = string.Empty;

        /// <summary>Cambió el estado del nivel.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Un mensaje corto para el centro de la pantalla.</summary>
        public event Action<string> Message;

        private void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<WaveSpawner>();
            if (knight == null) knight = FindFirstObjectByType<PlayerController>();
            if (bard == null) bard = FindFirstObjectByType<BardCompanion>();

            if (knight != null)
            {
                knightHealth = knight.GetComponent<Health>();
                input = knight.GetComponent<PlayerInputReader>();
            }

            if (input == null) input = FindFirstObjectByType<PlayerInputReader>();
            if (bard != null) song = bard.GetComponent<SongBuff>();

            // Las hordas las lanza el combate, no el Play.
            if (spawner != null) spawner.AutoStart = false;
            if (silentStart && song != null) song.enabled = false;

            if (spawner == null) Debug.LogWarning("[LevelFlow] No hay WaveSpawner en la escena: el combate no tendrá hordas.", this);
            if (knightHealth == null) Debug.LogWarning("[LevelFlow] No encuentro al caballero (PlayerController con Health).", this);
            if (bard == null) Debug.LogWarning("[LevelFlow] No encuentro a la bardo (BardCompanion).", this);
        }

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.OnWaveStarted += HandleWaveStarted;
                spawner.OnWaveCleared += HandleWaveCleared;
                spawner.OnAllWavesCleared += HandleAllWavesCleared;
            }

            if (knightHealth != null) knightHealth.Died += HandleKnightDied;
            if (bard != null) bard.Died += HandleBardDied;
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.OnWaveStarted -= HandleWaveStarted;
                spawner.OnWaveCleared -= HandleWaveCleared;
                spawner.OnAllWavesCleared -= HandleAllWavesCleared;
            }

            if (knightHealth != null) knightHealth.Died -= HandleKnightDied;
            if (bard != null) bard.Died -= HandleBardDied;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            StateChanged?.Invoke(State);
            StartCoroutine(SendIntro());
        }

        /// <summary>Empieza el combate: la bardo empieza a cantar y salen las hordas. Lo llama el trigger de la plaza.</summary>
        public void StartCombat()
        {
            if (State != GameState.Exploring) return;

            if (song != null && !song.enabled && bard != null && !bard.IsDead) song.enabled = true;
            SetState(GameState.Combat);
            Message?.Invoke(combatMessage);
            if (spawner != null) spawner.StartWaves();
        }

        /// <summary>Vuelve a cargar el nivel desde cero. La escena tiene que estar en Build Profiles (lo asegura el menú de la tarea 20).</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        /// <summary>Vuelve al menú principal; si no está en la build, reinicia el nivel.</summary>
        public void GoToMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(menuSceneName)) SceneManager.LoadScene(menuSceneName);
            else Restart();
        }

        private void HandleWaveStarted(int index, Wave wave)
        {
            if (!ending && wave != null && !string.IsNullOrEmpty(wave.name)) Message?.Invoke(wave.name);
        }

        private void HandleWaveCleared(int index)
        {
            if (!ending && spawner != null && index < spawner.WaveCount - 1) Message?.Invoke(waveClearedMessage);
        }

        private void HandleAllWavesCleared()
        {
            if (ending) return;
            ending = true;
            StartCoroutine(EndAfter(GameState.Victory, victoryDelay));
        }

        private void HandleKnightDied(Health source, DamageInfo info) => HeroDown(knightDownMessage);
        private void HandleBardDied() => HeroDown(bardDownMessage);

        private void HeroDown(string message)
        {
            if (ending) return;
            ending = true;
            DefeatReason = message;
            if (input != null) input.SetGameplayEnabled(false);
            Message?.Invoke(message);
            StartCoroutine(EndAfter(GameState.Defeat, defeatDelay));
        }

        private IEnumerator EndAfter(GameState result, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (input != null) input.SetGameplayEnabled(false);
            SetState(result);
            if (result == GameState.Defeat) Time.timeScale = 0f; // congela a los demonios detrás de la pantalla
        }

        private IEnumerator SendIntro()
        {
            yield return new WaitForSecondsRealtime(introDelay);
            if (State == GameState.Exploring && !string.IsNullOrEmpty(introMessage)) Message?.Invoke(introMessage);
        }

        private void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            Debug.Log($"[Nivel] {next}", this);
            StateChanged?.Invoke(next);
        }
    }
}
