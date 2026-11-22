using TheLastBeat.Buffs;
using TheLastBeat.Companions;
using TheLastBeat.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Añade la canción a la bardo (SongBuff y BuffReceiver en Bardo.prefab), un BuffReceiver al Knight y un HUD de
    /// prueba en Test_Combo. Menú: The Last Beat/Bardo/Añadir canción (tarea 17). Es repetible.
    /// </summary>
    public static class SongSetup
    {
        private const string BardPrefab = "Assets/_Project/Prefabs/Heroes/Bardo.prefab";
        private const string TestScenePath = "Assets/_Project/Scenes/Test_Combo.unity";

        [MenuItem("The Last Beat/Bardo/Añadir canción (tarea 17)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SongSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BardPrefab) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabTool.PrefabPath) == null)
            {
                Debug.LogError("[SongSetup] Faltan Bardo.prefab o Knight.prefab. Ejecuta antes 'Crear bardo de prueba en Test_Combo'.");
                return;
            }

            EditPrefab(BardPrefab, root =>
            {
                if (root.GetComponent<BuffReceiver>() == null) root.AddComponent<BuffReceiver>();
                if (root.GetComponent<SongBuff>() == null) root.AddComponent<SongBuff>();
            });
            EditPrefab(KnightPrefabTool.PrefabPath, root =>
            {
                if (root.GetComponent<BuffReceiver>() == null) root.AddComponent<BuffReceiver>();
            });

            AddHud();
            AssetDatabase.SaveAssets();
            Debug.Log("[SongSetup] Listo. La bardo canta en Test_Combo. Tecla I = interrumpir la canción (prueba).");
        }

        [MenuItem("The Last Beat/Bardo/Añadir feedback de interrupción (tarea 18)")]
        public static void AddInterruptFeedback()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SongSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(BardPrefab) == null)
            {
                Debug.LogError("[SongSetup] Falta Bardo.prefab.");
                return;
            }

            EditPrefab(BardPrefab, root =>
            {
                if (root.GetComponent<SongBuff>() == null)
                {
                    Debug.LogError("[SongSetup] La bardo no tiene SongBuff. Ejecuta antes 'Añadir canción (tarea 17)'.");
                    return;
                }

                if (root.GetComponent<SongInterruptFeedback>() == null) root.AddComponent<SongInterruptFeedback>();
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[SongSetup] Listo. Prueba en Test_Combo: O = golpe a la bardo que interrumpe, P = golpe que no interrumpe.");
        }

        private static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddHud()
        {
            if (EditorSceneManager.GetActiveScene().path != TestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (Object.FindFirstObjectByType<SongDebugHud>() == null)
            {
                var hud = new GameObject("HUD_Cancion_Debug");
                hud.AddComponent<SongDebugHud>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
