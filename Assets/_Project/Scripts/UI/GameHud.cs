using System.Collections;
using TheLastBeat.Abilities;
using TheLastBeat.Buffs;
using TheLastBeat.Combat;
using TheLastBeat.Companions;
using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// HUD de la primera entrega (tarea 34): vida del caballero y de la bardo, recarga de las dos habilidades y estado de
    /// la canción. Construye su propio Canvas por código (no necesita assets) y busca solo al caballero y a la bardo.
    ///
    /// Se actualiza por eventos (Health.HealthChanged, AbilityCaster.CooldownStarted/Finished, SongBuff.OnSongStateChanged
    /// y OnInterrupted, BuffReceiver.BuffChanged). Una corrutina solo corre mientras hay algo que animar (una recarga o el
    /// temporizador de la canción) y se apaga sola. El Canvas escala con la pantalla (referencia 1920x1080), así que se lee
    /// igual a 1080p y a 720p: texto blanco con contorno negro sobre paneles oscuros.
    ///
    /// La canción se entiende sin leer texto: icono verde = canta; rojo con una X = interrumpida; ámbar con un temporizador
    /// en forma de tarta que se vacía = recuperándose.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("Objetivos (vacío = los busca en la escena)")]
        [SerializeField] private PlayerController knight;
        [SerializeField] private BardCompanion bard;

        [Header("Colores")]
        [SerializeField] private Color knightColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color bardColor = new Color(0.75f, 0.35f, 0.85f);
        [SerializeField] private Color readyColor = new Color(0.4f, 0.95f, 0.5f);
        [SerializeField] private Color coolingColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color buffColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] private Color singingColor = new Color(0.3f, 0.9f, 0.45f);
        [SerializeField] private Color interruptedColor = new Color(0.95f, 0.2f, 0.2f);
        [SerializeField] private Color recoveringColor = new Color(1f, 0.7f, 0.2f);
        [Tooltip("Canción apagada (el pueblo en silencio, antes del combate).")]
        [SerializeField] private Color silentColor = new Color(0.45f, 0.47f, 0.52f);

        private class SlotView
        {
            public Image border, icon, overlay;
            public Text key, abilityName, seconds;
            public int shownSeconds = -1;
        }

        private Health knightHealth, bardHealth;
        private BuffReceiver knightBuffs;
        private AbilityCaster abilities;
        private SongBuff song;

        private Font font;
        private Image knightFill, bardFill;
        private Text knightValue, bardValue;
        private GameObject buffBadge;
        private readonly SlotView[] slots = { new SlotView(), new SlotView() };
        private Image songDisc, songRing, songTimer, songCrossA, songCrossB;
        private float songPopUntil;
        private Coroutine ticker;

        private const float PopDuration = 0.35f;

        // ---------------------------------------------------------------- Ciclo de vida

        private void Start()
        {
            if (knight == null) knight = FindFirstObjectByType<PlayerController>();
            if (bard == null) bard = FindFirstObjectByType<BardCompanion>();
            if (knight != null)
            {
                knightHealth = knight.GetComponent<Health>();
                knightBuffs = knight.GetComponent<BuffReceiver>();
                abilities = knight.GetComponent<AbilityCaster>();
            }

            if (bard != null)
            {
                bardHealth = bard.GetComponent<Health>();
                song = bard.GetComponent<SongBuff>();
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
            Subscribe();
            RefreshAll();
        }

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (knightHealth != null) knightHealth.HealthChanged += OnKnightHealth;
            if (bardHealth != null) bardHealth.HealthChanged += OnBardHealth;
            if (knightBuffs != null) knightBuffs.BuffChanged += OnKnightBuffChanged;
            if (abilities != null)
            {
                abilities.CooldownStarted += OnCooldownStarted;
                abilities.CooldownFinished += OnCooldownFinished;
            }

            if (song != null)
            {
                song.OnSongStateChanged += OnSongState;
                song.OnInterrupted += OnSongInterrupted;
                song.OnActiveChanged += OnSongActive;
            }
        }

        private void Unsubscribe()
        {
            if (knightHealth != null) knightHealth.HealthChanged -= OnKnightHealth;
            if (bardHealth != null) bardHealth.HealthChanged -= OnBardHealth;
            if (knightBuffs != null) knightBuffs.BuffChanged -= OnKnightBuffChanged;
            if (abilities != null)
            {
                abilities.CooldownStarted -= OnCooldownStarted;
                abilities.CooldownFinished -= OnCooldownFinished;
            }

            if (song != null)
            {
                song.OnSongStateChanged -= OnSongState;
                song.OnInterrupted -= OnSongInterrupted;
                song.OnActiveChanged -= OnSongActive;
            }
        }

        /// <summary>Vuelve a leer todo (por ejemplo tras cambiar la habilidad de una ranura o reiniciar el nivel).</summary>
        public void RefreshAll()
        {
            if (knightHealth != null) UpdateBar(knightFill, knightValue, knightHealth);
            if (bardHealth != null) UpdateBar(bardFill, bardValue, bardHealth);
            if (buffBadge != null) buffBadge.SetActive(knightBuffs != null && knightBuffs.IsBuffed);
            for (int i = 0; i < slots.Length; i++) RefreshSlot(i);
            if (song != null) ApplySongState(song.State);
            EnsureTicker();
        }

        // ---------------------------------------------------------------- Eventos

        private void OnKnightHealth(Health health) => UpdateBar(knightFill, knightValue, health);
        private void OnBardHealth(Health health) => UpdateBar(bardFill, bardValue, health);
        private void OnKnightBuffChanged(bool buffed) => buffBadge.SetActive(buffed);

        private void OnCooldownStarted(int slot, float duration)
        {
            if (slot < 0 || slot >= slots.Length) return;
            slots[slot].border.color = coolingColor;
            EnsureTicker();
        }

        private void OnCooldownFinished(int slot)
        {
            if (slot < 0 || slot >= slots.Length) return;
            ResetSlotVisual(slots[slot]);
        }

        private void OnSongState(SongState state)
        {
            ApplySongState(state);
            EnsureTicker();
        }

        private void OnSongActive(bool active)
        {
            if (songDisc == null) return;
            ApplySongState(song.State);
            EnsureTicker();
        }

        private void OnSongInterrupted(bool restarted)
        {
            songPopUntil = Time.time + PopDuration;
            EnsureTicker();
        }

        // ---------------------------------------------------------------- Corrutina solo mientras hay algo que animar

        private void EnsureTicker()
        {
            if (ticker == null && isActiveAndEnabled && NeedsTick()) ticker = StartCoroutine(Tick());
        }

        private bool NeedsTick()
        {
            if (song != null && song.isActiveAndEnabled && !song.IsSinging) return true;
            if (Time.time < songPopUntil) return true;
            if (abilities == null) return false;
            for (int i = 0; i < slots.Length; i++)
                if (abilities.CooldownRemaining(i) > 0f) return true;
            return false;
        }

        private IEnumerator Tick()
        {
            while (NeedsTick())
            {
                if (abilities != null)
                    for (int i = 0; i < slots.Length; i++) TickSlot(i);

                if (song != null)
                {
                    if (!song.IsSinging) songTimer.fillAmount = Mathf.Clamp01(song.DowntimeRemaining / Mathf.Max(0.01f, song.TotalDowntime));
                    float pop = Mathf.Clamp01((songPopUntil - Time.time) / PopDuration);
                    songDisc.rectTransform.localScale = Vector3.one * (1f + 0.3f * pop);
                }

                yield return null;
            }

            if (songDisc != null) songDisc.rectTransform.localScale = Vector3.one;
            ticker = null;
        }

        private void TickSlot(int index)
        {
            SlotView view = slots[index];
            float remaining = abilities.CooldownRemaining(index);
            if (remaining <= 0f) return;

            view.overlay.fillAmount = abilities.CooldownNormalized(index);
            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != view.shownSeconds)
            {
                view.shownSeconds = seconds;
                view.seconds.text = seconds.ToString();
            }
        }

        // ---------------------------------------------------------------- Actualizar elementos

        private static void UpdateBar(Image fill, Text value, Health health)
        {
            if (fill == null) return;
            fill.fillAmount = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
            value.text = $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}";
        }

        private void RefreshSlot(int index)
        {
            SlotView view = slots[index];
            AbilityData ability = abilities != null ? abilities.GetAbility(index) : null;
            view.abilityName.text = ability != null ? ability.displayName : "—";
            view.icon.sprite = ability != null && ability.icon != null ? ability.icon : HudSprites.Square;
            view.icon.color = ability != null && ability.icon != null ? Color.white : new Color(0.25f, 0.28f, 0.34f);
            if (abilities != null && abilities.CooldownRemaining(index) > 0f) view.border.color = coolingColor;
            else ResetSlotVisual(view);
        }

        private void ResetSlotVisual(SlotView view)
        {
            view.border.color = readyColor;
            view.overlay.fillAmount = 0f;
            view.seconds.text = string.Empty;
            view.shownSeconds = -1;
        }

        /// <summary>Canción apagada: el componente está desactivado y no es porque la bardo cayó (eso es Interrupted).</summary>
        private bool SongSilent => song != null && !song.isActiveAndEnabled && song.State != SongState.Interrupted;

        private void ApplySongState(SongState state)
        {
            if (SongSilent)
            {
                songDisc.color = silentColor;
                songRing.enabled = false;
                songTimer.enabled = false;
                SetCross(false);
                return;
            }

            switch (state)
            {
                case SongState.Singing:
                    songDisc.color = singingColor;
                    songRing.enabled = true;
                    songTimer.enabled = false;
                    SetCross(false);
                    break;
                case SongState.Interrupted:
                    songDisc.color = interruptedColor;
                    songRing.enabled = false;
                    songTimer.enabled = true;
                    SetCross(true);
                    break;
                default:
                    songDisc.color = recoveringColor;
                    songRing.enabled = false;
                    songTimer.enabled = true;
                    SetCross(false);
                    break;
            }
        }

        private void SetCross(bool visible)
        {
            songCrossA.enabled = visible;
            songCrossB.enabled = visible;
        }

        // ---------------------------------------------------------------- Construcción del Canvas

        private void BuildUi()
        {
            var canvasObject = new GameObject("HudCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasObject.transform;

            BuildStatusPanel(root);
            BuildAbilityBar(root);
        }

        private void BuildStatusPanel(Transform root)
        {
            RectTransform panel = Rect(root, "PanelEstado", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(700f, 172f));
            Img(panel, "Fondo", HudSprites.Square, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one);

            // Caballero
            Label(panel, "EtiquetaCaballero", "CABALLERO", 26, TextAnchor.UpperLeft, new Vector2(16f, -8f), new Vector2(300f, 32f));
            Text badge = Label(panel, "Potenciado", "POTENCIADO", 22, TextAnchor.UpperRight, new Vector2(256f, -10f), new Vector2(280f, 30f));
            badge.color = buffColor;
            buffBadge = badge.gameObject;
            buffBadge.SetActive(false);
            knightFill = Bar(panel, "BarraCaballero", new Vector2(16f, -42f), knightColor, out knightValue);

            // Bardo
            Label(panel, "EtiquetaBardo", "BARDO", 26, TextAnchor.UpperLeft, new Vector2(16f, -88f), new Vector2(300f, 32f));
            bardFill = Bar(panel, "BarraBardo", new Vector2(16f, -122f), bardColor, out bardValue);
            if (bard == null) bardFill.transform.parent.gameObject.SetActive(false);

            // Icono de la canción: disco de color + aura + X + temporizador.
            RectTransform icon = Rect(panel, "IconoCancion", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(612f, -72f), new Vector2(104f, 104f));
            songRing = Img(icon, "Aura", HudSprites.Ring, new Color(1f, 1f, 1f, 0.9f), new Vector2(-0.12f, -0.12f), new Vector2(1.12f, 1.12f));
            songDisc = Img(icon, "Disco", HudSprites.Circle, singingColor, Vector2.zero, Vector2.one);
            songTimer = Img(icon, "Temporizador", HudSprites.Circle, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.one);
            songTimer.type = Image.Type.Filled;
            songTimer.fillMethod = Image.FillMethod.Radial360;
            songTimer.fillOrigin = 2;
            songTimer.fillClockwise = true;
            songTimer.fillAmount = 1f;
            songCrossA = Cross(icon, "CruzA", 45f);
            songCrossB = Cross(icon, "CruzB", -45f);
            Label(panel, "EtiquetaCancion", "CANCIÓN", 20, TextAnchor.UpperCenter, new Vector2(552f, -128f), new Vector2(120f, 30f));
            if (song == null) icon.gameObject.SetActive(false);
        }

        private void BuildAbilityBar(Transform root)
        {
            string[] keys = { "Q", "E" };
            float[] x = { -90f, 90f };
            for (int i = 0; i < slots.Length; i++)
            {
                SlotView view = slots[i];
                RectTransform slot = Rect(root, "Habilidad" + (i + 1), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x[i], 40f), new Vector2(130f, 130f));
                view.border = Img(slot, "Borde", HudSprites.Square, readyColor, Vector2.zero, Vector2.one);
                Img(slot, "Fondo", HudSprites.Square, new Color(0.08f, 0.09f, 0.12f, 0.95f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
                view.icon = Img(slot, "Icono", HudSprites.Square, new Color(0.25f, 0.28f, 0.34f), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
                view.overlay = Img(slot, "Recarga", HudSprites.Square, new Color(0f, 0f, 0f, 0.7f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
                view.overlay.type = Image.Type.Filled;
                view.overlay.fillMethod = Image.FillMethod.Radial360;
                view.overlay.fillOrigin = 2;
                view.overlay.fillClockwise = true;
                view.overlay.fillAmount = 0f;
                view.seconds = Label(slot, "Segundos", string.Empty, 54, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(130f, 130f));
                view.key = Label(slot, "Tecla", keys[i], 34, TextAnchor.UpperLeft, new Vector2(10f, -4f), new Vector2(60f, 40f));
                view.abilityName = Label(slot, "Nombre", string.Empty, 22, TextAnchor.LowerCenter, new Vector2(-45f, 134f), new Vector2(220f, 30f));
            }
        }

        // ---------------------------------------------------------------- Ayudas de construcción

        private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Imagen que ocupa una región relativa (anchors) del padre.</summary>
        private static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Vector2 position, Vector2 size)
        {
            // Anclado arriba a la izquierda del padre; los textos centrados usan toda la caja.
            RectTransform rect = Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>Barra de vida (fondo + relleno horizontal + valor numérico). Devuelve el relleno.</summary>
        private Image Bar(Transform parent, string name, Vector2 position, Color color, out Text value)
        {
            RectTransform bar = Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(520f, 34f));
            Img(bar, "Fondo", HudSprites.Square, new Color(0.12f, 0.12f, 0.14f, 0.95f), Vector2.zero, Vector2.one);
            Image fill = Img(bar, "Relleno", HudSprites.Square, color, new Vector2(0.006f, 0.1f), new Vector2(0.994f, 0.9f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            value = Label(bar, "Valor", string.Empty, 24, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(520f, 34f));
            return fill;
        }

        /// <summary>Una barra blanca girada: dos juntas forman la X del estado "interrumpida".</summary>
        private static Image Cross(Transform parent, string name, float angle)
        {
            RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 12f));
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudSprites.Square;
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }
    }
}
