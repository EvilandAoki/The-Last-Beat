using TheLastBeat.Level;
using UnityEngine;
using UnityEngine.Audio;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// El ambiente del pueblo en silencio (tarea 32): viento y pasos lejanos (Ambiente_Pueblo, tarea 30) en bucle
    /// mientras el caballero explora. Entra con un fundido al empezar el nivel y se apaga cuando LevelFlow pasa a
    /// combate, que es cuando entra la música. Sale por el grupo Ambiente del Audio Mixer.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbiencePlayer : MonoBehaviour
    {
        [SerializeField] private LevelFlow flow;
        [SerializeField] private AudioClip clip;
        [Tooltip("Grupo Ambiente del Audio Mixer.")]
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Min(0.01f)] private float fadeIn = 2f;
        [SerializeField, Min(0.01f)] private float fadeOut = 2.5f;

        private AudioSource source;
        private float level;
        private float target;
        private float speed = 1f;

        private void Awake()
        {
            if (flow == null) flow = FindFirstObjectByType<LevelFlow>();
            source = GetComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            if (output != null) source.outputAudioMixerGroup = output;
        }

        private void OnEnable()
        {
            if (flow != null) flow.StateChanged += HandleLevelState;
        }

        private void OnDisable()
        {
            if (flow != null) flow.StateChanged -= HandleLevelState;
        }

        private void Start()
        {
            if (clip == null) return;
            source.Play();
            bool exploring = flow == null || flow.State == GameState.Exploring;
            SetTarget(exploring ? 1f : 0f, fadeIn);
        }

        private void HandleLevelState(GameState state) => SetTarget(state == GameState.Exploring ? 1f : 0f,
            state == GameState.Exploring ? fadeIn : fadeOut);

        private void Update()
        {
            if (!source.isPlaying) return;
            level = Mathf.MoveTowards(level, target, speed * Time.unscaledDeltaTime);
            source.volume = level * volume;
            if (level <= 0f && target <= 0f) source.Stop(); // ya no vuelve en este nivel
        }

        private void SetTarget(float value, float seconds)
        {
            target = value;
            float distance = Mathf.Abs(target - level);
            speed = distance > 0f ? distance / seconds : 1f;
        }
    }
}
