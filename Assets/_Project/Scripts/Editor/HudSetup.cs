using TheLastBeat.Abilities;
using TheLastBeat.DebugTools;
using TheLastBeat.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Crea el prefab del HUD (Prefabs/UI/Hud.prefab) y lo pone en Test_Combo en lugar del HUD de recargas de pruebas.
    /// Menú: The Last Beat/HUD/Añadir HUD a Test_Combo (tarea 34). Es repetible.
    /// Para usarlo en otra escena (por ejemplo Pueblo) basta con arrastrar el prefab: encuentra solo al caballero y a la bardo.
    /// </summary>
    public static class HudSetup
    {
        private const string Folder = "Assets/_Project/Prefabs/UI";
        private const string PrefabPath = Folder + "/Hud.prefab";
        private const string TestScenePath = "Assets/_Project/Scenes/Test_Combo.unity";

        [MenuItem("The Last Beat/HUD/Añadir HUD a Test_Combo (tarea 34)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[HudSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");

            var template = new GameObject("HUD");
            template.AddComponent<GameHud>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            Object.DestroyImmediate(template);

            if (EditorSceneManager.GetActiveScene().path != TestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.GetActiveScene();

            // El HUD de recargas de pruebas queda reemplazado por el real.
            foreach (AbilityDebugHud old in Object.FindObjectsByType<AbilityDebugHud>(FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            if (Object.FindFirstObjectByType<GameHud>() == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "HUD";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HudSetup] Listo. HUD en Test_Combo (prefab en " + PrefabPath + "). Para otra escena, arrastra el prefab.");
        }
    }
}
