using UnityEngine;

namespace TheLastBeat.Core
{
    /// <summary>
    /// Texto en pantalla legible sobre cualquier fondo: un color con contorno de otro (por ejemplo rojo con contorno negro).
    /// Solo se puede llamar desde OnGUI.
    /// Lo usan los avisos de gameplay hasta que exista el HUD real (tarea 34).
    /// </summary>
    public static class GuiText
    {
        private static GUIStyle style;

        public static void DrawOutlined(Rect area, string text, int fontSize, Color color, Color outline, float thickness = 2f,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            if (style == null) style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.alignment = alignment;

            style.normal.textColor = outline;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    GUI.Label(new Rect(area.x + dx * thickness, area.y + dy * thickness, area.width, area.height), text, style);

            style.normal.textColor = color;
            GUI.Label(area, text, style);
        }
    }
}
