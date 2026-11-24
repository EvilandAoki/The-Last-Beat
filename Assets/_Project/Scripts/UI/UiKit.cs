using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Piezas de interfaz construidas por código con el mismo estilo que el HUD (tarea 34): Canvas escalado a 1920x1080,
    /// texto blanco en negrita con contorno negro y paneles oscuros. Las usan los mensajes del nivel (tarea 20), las
    /// pantallas de fin y el menú principal (tarea 35). No necesitan assets: todo sale de la fuente integrada de Unity.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.12f, 0.22f, 0.95f);
        public static readonly Color ButtonHighlight = new Color(0.55f, 0.3f, 0.75f, 1f);

        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Canvas en pantalla. Con interactive=true lleva GraphicRaycaster para que funcionen los botones.</summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (interactive) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
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

        /// <summary>Un rectángulo que ocupa todo el padre.</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rect;
        }

        /// <summary>Un color plano (sin sprite) que ocupa todo el rectángulo dado.</summary>
        public static Image Fill(RectTransform rect, Color color, bool raycastTarget = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text Label(RectTransform rect, string text, int fontSize, TextAnchor alignment, Color color)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>Botón con texto. Se resalta al pasar el ratón y al seleccionarlo con teclado o mando.</summary>
        public static Button CreateButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityAction onClick)
        {
            RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            Image background = Fill(rect, Color.white, true);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = new Color(0.35f, 0.2f, 0.5f, 1f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            Label(Stretch(rect, "Texto"), text, 34, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        /// <summary>
        /// Crea un EventSystem si la escena no tiene uno, con el módulo del Input System (el proyecto solo usa el Input
        /// System) y sus acciones por defecto: ratón, teclado y mando.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>Selecciona un botón para que el teclado y el mando tengan desde dónde empezar.</summary>
        public static void Select(Selectable selectable)
        {
            if (selectable == null) return;
            EventSystem system = EventSystem.current != null ? EventSystem.current : Object.FindFirstObjectByType<EventSystem>();
            if (system != null) system.SetSelectedGameObject(selectable.gameObject);
        }
    }
}
