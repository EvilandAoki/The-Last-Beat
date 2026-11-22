using UnityEngine;

namespace TheLastBeat.Companions
{
    /// <summary>
    /// Que se note cuando la canción se interrumpe (tarea 18): la bardo parpadea y suena un efecto. Va en el mismo objeto
    /// que SongBuff. El sonido es opcional: asigna un clip (tarea 31) y se reproduce solo, sin tocar código. Los golpes que
    /// reinician el contador con la canción ya cortada también parpadean y suenan, para que el jugador vea que sigue sin cantar.
    /// </summary>
    [RequireComponent(typeof(SongBuff))]
    public class SongInterruptFeedback : MonoBehaviour
    {
        [Header("Parpadeo")]
        [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.15f);
        [SerializeField, Min(0.05f)] private float flashDuration = 0.35f;
        [Tooltip("Parpadeo más corto cuando el golpe solo reinicia el contador.")]
        [SerializeField, Range(0.1f, 1f)] private float repeatFlashScale = 0.6f;

        [Header("Sonido (opcional)")]
        [SerializeField] private AudioClip interruptClip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private SongBuff song;
        private Renderer[] renderers;
        private Color[] originalColors;
        private MaterialPropertyBlock block;
        private float flashTimer;
        private float flashTotal = 1f;

        private void Awake()
        {
            song = GetComponent<SongBuff>();
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                originalColors[i] = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
            }

            block = new MaterialPropertyBlock();
        }

        private void OnEnable() => song.OnInterrupted += OnInterrupted;

        private void OnDisable()
        {
            song.OnInterrupted -= OnInterrupted;
            Restore();
        }

        private void OnInterrupted(bool restarted)
        {
            flashTotal = flashDuration * (restarted ? repeatFlashScale : 1f);
            flashTimer = flashTotal;
            if (interruptClip != null) AudioSource.PlayClipAtPoint(interruptClip, transform.position, volume);
        }

        private void Update()
        {
            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                Restore();
                return;
            }

            float t = flashTimer / flashTotal;
            for (int i = 0; i < renderers.Length; i++)
            {
                block.SetColor(BaseColor, Color.Lerp(originalColors[i], flashColor, t));
                renderers[i].SetPropertyBlock(block);
            }
        }

        private void Restore()
        {
            flashTimer = 0f;
            if (renderers == null) return;
            foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(null);
        }
    }
}
