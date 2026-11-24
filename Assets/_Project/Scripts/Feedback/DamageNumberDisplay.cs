using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace TheLastBeat.Feedback
{
    /// <summary>
    /// Números de daño flotantes con pooling (tarea 12, el "DamageNumberPool" del issue). Cada número es un Text de uGUI de
    /// un ObjectPool: sube sobre el punto del golpe y se desvanece, y vuelve al pool al terminar. Una sola corrutina los
    /// mueve y solo corre mientras hay números en pantalla. Usa tiempo real, así que siguen moviéndose durante el hit stop.
    /// Los textos de los enteros se guardan en caché: mostrar un número no reserva memoria.
    /// </summary>
    public class DamageNumberDisplay : MonoBehaviour
    {
        private static DamageNumberDisplay instance;
        private static readonly string[] Texts = new string[1000];

        private class Entry
        {
            public GameObject go;
            public RectTransform rect;
            public Text text;
            public Vector3 world;
            public float start, duration, rise, xOffset;
            public Color color;
        }

        private readonly List<Entry> active = new List<Entry>();
        private ObjectPool<Entry> pool;
        private Canvas canvas;
        private Font font;
        private Camera cam;
        private Coroutine loop;
        private ImpactSettings settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        internal static void Prepare(ImpactSettings settings)
        {
            if (instance != null) return;
            var go = new GameObject("DamageNumbers");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DamageNumberDisplay>();
            instance.Build(settings);
        }

        internal static void Show(Vector3 world, float damage, bool big, ImpactSettings settings)
        {
            Prepare(settings);
            instance.Add(world, damage, big);
        }

        // ---------------------------------------------------------------- Construcción

        private void Build(ImpactSettings s)
        {
            settings = s;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // por debajo del HUD (10)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            pool = new ObjectPool<Entry>(CreateEntry, e => e.go.SetActive(true), e => e.go.SetActive(false),
                e => { if (e.go != null) Destroy(e.go); }, false, s.prewarmNumbers, s.maxNumbers);

            // Se crean de antemano para no instanciar durante el combate.
            var created = new List<Entry>();
            for (int i = 0; i < s.prewarmNumbers; i++) created.Add(pool.Get());
            foreach (Entry e in created) pool.Release(e);
        }

        private Entry CreateEntry()
        {
            var go = new GameObject("Numero", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 80f);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);

            go.SetActive(false);
            return new Entry { go = go, rect = rect, text = text };
        }

        // ---------------------------------------------------------------- Mostrar

        private void Add(Vector3 world, float damage, bool big)
        {
            // Si ya hay demasiados, se recicla el más antiguo.
            if (active.Count >= settings.maxNumbers) Release(0);

            Entry e = pool.Get();
            e.world = world + Vector3.up * 0.3f;
            e.start = Time.unscaledTime;
            e.duration = settings.numberDuration;
            e.rise = settings.numberRise;
            e.xOffset = Random.Range(-settings.numberSpread, settings.numberSpread);
            e.color = big ? settings.colorBig : settings.colorNormal;
            e.text.text = Format(damage);
            e.text.fontSize = big ? settings.fontSizeBig : settings.fontSize;
            e.text.color = e.color;
            active.Add(e);
            if (loop == null) loop = StartCoroutine(Run());
        }

        private static string Format(float damage)
        {
            int value = Mathf.Clamp(Mathf.RoundToInt(damage), 0, Texts.Length - 1);
            return Texts[value] ?? (Texts[value] = value.ToString());
        }

        private IEnumerator Run()
        {
            while (active.Count > 0)
            {
                if (cam == null) cam = Camera.main;
                float now = Time.unscaledTime;
                float inverseScale = 1f / Mathf.Max(0.0001f, canvas.scaleFactor);

                for (int i = active.Count - 1; i >= 0; i--)
                {
                    Entry e = active[i];
                    float t = (now - e.start) / e.duration;
                    if (t >= 1f || cam == null)
                    {
                        Release(i);
                        continue;
                    }

                    float rise = e.rise * (1f - (1f - t) * (1f - t)); // sube rápido y frena
                    Vector3 screen = cam.WorldToScreenPoint(e.world + Vector3.up * rise);
                    e.go.SetActive(screen.z > 0f);
                    e.rect.anchoredPosition = new Vector2(screen.x * inverseScale + e.xOffset, screen.y * inverseScale);

                    float pop = 1f + 0.35f * Mathf.Clamp01(1f - t / 0.15f); // pequeño "salto" al aparecer
                    e.rect.localScale = new Vector3(pop, pop, 1f);
                    Color color = e.color;
                    color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    e.text.color = color;
                }

                yield return null;
            }

            loop = null;
        }

        private void Release(int index)
        {
            Entry e = active[index];
            active.RemoveAt(index);
            pool.Release(e);
        }
    }
}
