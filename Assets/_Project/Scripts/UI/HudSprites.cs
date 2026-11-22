using UnityEngine;

namespace TheLastBeat.UI
{
    /// <summary>
    /// Sprites sencillos generados por código (cuadrado, círculo y anillo) para el HUD, así no hace falta ningún asset
    /// de imagen. Se crean una vez y se reutilizan.
    /// </summary>
    public static class HudSprites
    {
        private static Sprite square, circle, ring;

        public static Sprite Square => square != null ? square : square = Make(8, (x, y, r) => 1f);

        public static Sprite Circle => circle != null ? circle : circle = Make(128, (x, y, r) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(r - d + 0.5f);
        });

        /// <summary>Anillo fino: sirve de aura alrededor del icono de la canción.</summary>
        public static Sprite Ring => ring != null ? ring : ring = Make(128, (x, y, r) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float outer = Mathf.Clamp01(r - d + 0.5f);
            float inner = Mathf.Clamp01(r * 0.86f - d + 0.5f);
            return outer * (1f - inner);
        });

        private static Sprite Make(int size, System.Func<float, float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float radius = size * 0.5f - 1f;
            float center = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte a = (byte)(Mathf.Clamp01(alpha(x - center, y - center, radius)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
