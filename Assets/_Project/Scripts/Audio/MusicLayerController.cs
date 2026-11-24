using TheLastBeat.Companions;
using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// Música por capas (tarea 32) con las pistas de las tareas 29 y 30, según el mapa de Tools/Audio/README.md:
    ///  - Al empezar el combate arranca todas las capas a la vez con PlayScheduled (el mismo dspTime) y en bucle. Desde
    ///    ahí solo suben y bajan de volumen; nunca se paran, así que no se desfasan (todas duran 16 compases a 120 BPM).
    ///  - La base y la percusión de combate suenan todo el combate; la melodía de combate, solo mientras hay una horda.
    ///  - La melodía y el laúd de la bardo siguen a SongBuff.OnSongStateChanged: cantando = arriba; interrumpida = se
    ///    cortan al instante; recuperándose = vuelven con un fundido que dura lo que falta para que vuelva a cantar.
    ///  - Victoria: todas las capas arriba. Derrota: todo se apaga en Defeat Fade segundos.
    /// Antes del combate no suena música (el pueblo en silencio: de eso se encarga AmbiencePlayer).
    /// Usa tiempo real porque la derrota congela el juego. Sale por el grupo Música del Audio Mixer.
    /// </summary>
    public class MusicLayerController : MonoBehaviour
    {
        private class Layer
        {
            public AudioSource source;
            public float level;
            public float target;
            public float speed = 1f;
        }

        [SerializeField] private LevelFlow flow;
        [Tooltip("Grupo Música del Audio Mixer.")]
        [SerializeField] private AudioMixerGroup output;

        [Header("Combate (tarea 30)")]
        [SerializeField] private AudioClip combatBase;
        [SerializeField] private AudioClip combatPercussion;
        [SerializeField] private AudioClip combatMelody;

        [Header("Canción de la bardo (tarea 29)")]
        [SerializeField] private AudioClip bardMelody;
        [SerializeField] private AudioClip bardLute;

        [Header("Mezcla")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [Tooltip("Volumen de la melodía de combate entre una horda y la siguiente (0 = callada).")]
        [SerializeField, Range(0f, 1f)] private float melodyBetweenWaves = 0f;
        [Tooltip("Segundos que tarda una capa en entrar.")]
        [SerializeField, Min(0.01f)] private float fadeIn = 1.5f;
        [Tooltip("Segundos que tardan las capas de la bardo en cortarse cuando la interrumpen.")]
        [SerializeField, Min(0.01f)] private float cutTime = 0.12f;
        [Tooltip("Segundos que tarda la música en apagarse en la derrota.")]
        [SerializeField, Min(0.01f)] private float defeatFade = 2.5f;

        private Layer baseLayer, percussionLayer, melodyLayer, bardMelodyLayer, luteLayer;
        private Layer[] layers;
        private WaveSpawner spawner;
        private SongBuff song;
        private bool started;
        private bool waveActive;
        private bool subscribed;

        public bool IsPlaying => started;

        private void Awake()
        {
            if (flow == null) flow = FindFirstObjectByType<LevelFlow>();
            spawner = flow != null ? flow.Spawner : FindFirstObjectByType<WaveSpawner>();
            BardCompanion bard = FindFirstObjectByType<BardCompanion>();
            if (bard != null) song = bard.GetComponent<SongBuff>();

            baseLayer = CreateLayer("Combate_Base", combatBase);
            percussionLayer = CreateLayer("Combate_Percusion", combatPercussion);
            melodyLayer = CreateLayer("Combate_Melodia", combatMelody);
            bardMelodyLayer = CreateLayer("Bardo_Melodia", bardMelody);
            luteLayer = CreateLayer("Bardo_Laud", bardLute);
            layers = new[] { baseLayer, percussionLayer, melodyLayer, bardMelodyLayer, luteLayer };

            if (flow == null) Debug.LogWarning("[MusicLayerController] No hay LevelFlow en la escena: la música no va a empezar.", this);
        }

        private void OnEnable() => Subscribe(true);
        private void OnDisable() => Subscribe(false);

        private void Subscribe(bool on)
        {
            if (on == subscribed) return;
            subscribed = on;

            if (flow != null)
            {
                if (on) flow.StateChanged += HandleLevelState;
                else flow.StateChanged -= HandleLevelState;
            }

            if (spawner != null)
            {
                if (on)
                {
                    spawner.OnWaveStarted += HandleWaveStarted;
                    spawner.OnWaveCleared += HandleWaveCleared;
                }
                else
                {
                    spawner.OnWaveStarted -= HandleWaveStarted;
                    spawner.OnWaveCleared -= HandleWaveCleared;
                }
            }

            if (song != null)
            {
                if (on) song.OnSongStateChanged += HandleSongState;
                else song.OnSongStateChanged -= HandleSongState;
            }
        }

        private void Update()
        {
            if (!started) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = layers[i];
                if (layer.source == null) continue;
                layer.level = Mathf.MoveTowards(layer.level, layer.target, layer.speed * dt);
                layer.source.volume = layer.level * volume;
            }
        }

        // ---------------------------------------------------------------- Eventos

        private void HandleLevelState(GameState state)
        {
            switch (state)
            {
                case GameState.Combat:
                    StartMusic();
                    break;
                case GameState.Victory:
                    for (int i = 0; i < layers.Length; i++) SetTarget(layers[i], 1f, fadeIn);
                    break;
                case GameState.Defeat:
                    for (int i = 0; i < layers.Length; i++) SetTarget(layers[i], 0f, defeatFade);
                    break;
            }
        }

        private void HandleWaveStarted(int index, Wave wave)
        {
            waveActive = true;
            if (started) SetTarget(melodyLayer, 1f, 1f);
        }

        private void HandleWaveCleared(int index)
        {
            waveActive = false;
            if (started) SetTarget(melodyLayer, melodyBetweenWaves, 2f);
        }

        private void HandleSongState(SongState state)
        {
            if (!started) return;
            ApplySong(state);
        }

        // ---------------------------------------------------------------- Capas

        private void StartMusic()
        {
            if (started) return;
            started = true;

            // Todas a la vez, en el mismo instante del reloj de audio: así quedan alineadas para siempre.
            double startAt = AudioSettings.dspTime + 0.1;
            for (int i = 0; i < layers.Length; i++)
            {
                Layer layer = layers[i];
                if (layer.source == null) continue;
                layer.level = 0f;
                layer.source.volume = 0f;
                layer.source.PlayScheduled(startAt);
            }

            SetTarget(baseLayer, 1f, fadeIn);
            SetTarget(percussionLayer, 1f, fadeIn);
            SetTarget(melodyLayer, waveActive ? 1f : melodyBetweenWaves, fadeIn);
            if (song != null && song.enabled) ApplySong(song.State);
            else SetBard(0f, cutTime);
        }

        private void ApplySong(SongState state)
        {
            switch (state)
            {
                case SongState.Singing:
                    SetBard(1f, 0.5f);
                    break;
                case SongState.Interrupted:
                    SetBard(0f, cutTime);
                    break;
                case SongState.Recovering:
                    SetBard(1f, Mathf.Max(0.25f, song.StateTimeRemaining));
                    break;
            }
        }

        private void SetBard(float target, float seconds)
        {
            SetTarget(bardMelodyLayer, target, seconds);
            SetTarget(luteLayer, target, seconds);
        }

        /// <summary>Lleva la capa a target (0 a 1) en más o menos "seconds" segundos desde donde esté.</summary>
        private static void SetTarget(Layer layer, float target, float seconds)
        {
            if (layer == null) return;
            layer.target = target;
            float distance = Mathf.Abs(target - layer.level);
            layer.speed = distance > 0f ? distance / Mathf.Max(0.01f, seconds) : 1f;
        }

        private Layer CreateLayer(string name, AudioClip clip)
        {
            var layer = new Layer();
            if (clip == null)
            {
                Debug.LogWarning($"[MusicLayerController] Falta el clip de la capa {name}.", this);
                return layer;
            }

            var holder = new GameObject("Capa_" + name);
            holder.transform.SetParent(transform, false);
            AudioSource source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 0; // la música no se queda sin voz aunque suenen muchos efectos
            source.volume = 0f;
            source.outputAudioMixerGroup = output;
            layer.source = source;
            return layer;
        }
    }
}
