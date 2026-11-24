using System.IO;
using TheLastBeat.Feedback;
using UnityEditor;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Crea el asset de ajustes de impacto (VFX/Resources/ImpactSettings.asset) para poder editar la pausa, la sacudida y los
    /// números de daño desde el Inspector. No es obligatorio: sin él el juego usa los mismos valores por defecto.
    /// Menú: The Last Beat/Feedback/Crear ajustes de impacto (tarea 12). Si ya existe, lo selecciona sin pisarlo.
    /// </summary>
    public static class ImpactSetup
    {
        private const string Folder = "Assets/_Project/VFX/Resources";
        private const string AssetPath = Folder + "/ImpactSettings.asset";

        [MenuItem("The Last Beat/Feedback/Crear ajustes de impacto (tarea 12)")]
        public static void Run()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ImpactSettings>(AssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Folder);
                asset = ScriptableObject.CreateInstance<ImpactSettings>();
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[ImpactSetup] Listo. Ajustes creados en " + AssetPath + ". Edítalos en el Inspector (también en Play).");
            }
            else
            {
                Debug.Log("[ImpactSetup] Los ajustes ya existen en " + AssetPath + ": no se tocan.");
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
