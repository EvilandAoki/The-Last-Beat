using TheLastBeat.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Convierte el héroe (Knight) en un prefab para usarlo en cualquier escena, y lo pone en una escena
    /// sustituyendo al Knight que ya tenga (por ejemplo el de Pueblo.unity) sin perder la cámara.
    /// Menú: The Last Beat/Hero/...  Ambos pasos son repetibles.
    /// </summary>
    public static class KnightPrefabTool
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Knight.prefab";

        [MenuItem("The Last Beat/Hero/1. Guardar el Knight de la escena como prefab")]
        public static void SaveAsPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[KnightPrefabTool] Sal de Play antes de ejecutar esto.");
                return;
            }

            GameObject knight = GameObject.Find("Knight");
            if (knight == null || knight.GetComponent<PlayerCombat>() == null)
            {
                Debug.LogError("[KnightPrefabTool] Abre la escena Test_Combo: necesito un 'Knight' con PlayerCombat.");
                return;
            }

            // La instancia de la escena queda conectada al prefab.
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(knight, PrefabPath, InteractionMode.UserAction);
            if (prefab == null)
            {
                Debug.LogError("[KnightPrefabTool] No se pudo guardar el prefab.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(knight.scene);
            EditorSceneManager.SaveScene(knight.scene);
            Debug.Log($"[KnightPrefabTool] Prefab guardado en {PrefabPath}. Commitéalo junto con su .meta.");
        }

        [MenuItem("The Last Beat/Hero/2. Reemplazar el Knight de la escena por el prefab")]
        public static void ReplaceWithPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[KnightPrefabTool] Sal de Play antes de ejecutar esto.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[KnightPrefabTool] No existe {PrefabPath}. Ejecuta primero el paso 1 en Test_Combo.");
                return;
            }

            GameObject old = GameObject.Find("Knight");
            if (old == null)
            {
                Debug.LogError("[KnightPrefabTool] La escena abierta no tiene un 'Knight' que reemplazar.");
                return;
            }

            if (PrefabUtility.GetCorrespondingObjectFromSource(old) == prefab)
            {
                Debug.Log("[KnightPrefabTool] El Knight de esta escena ya es una instancia del prefab.");
                return;
            }

            Transform oldTransform = old.transform;
            Vector3 position = oldTransform.position;
            Quaternion rotation = oldTransform.rotation;

            // Cámaras que seguían o miraban al Knight viejo.
            var followers = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            var follow = new bool[followers.Length];
            var lookAt = new bool[followers.Length];
            for (int i = 0; i < followers.Length; i++)
            {
                follow[i] = followers[i].Follow != null && followers[i].Follow.IsChildOf(oldTransform);
                lookAt[i] = followers[i].LookAt != null && followers[i].LookAt.IsChildOf(oldTransform);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.scene);
            instance.name = "Knight";
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.SetSiblingIndex(oldTransform.GetSiblingIndex());
            Object.DestroyImmediate(old);

            for (int i = 0; i < followers.Length; i++)
            {
                if (follow[i]) followers[i].Follow = instance.transform;
                if (lookAt[i]) followers[i].LookAt = instance.transform;
                EditorUtility.SetDirty(followers[i]);
            }

            EditorSceneManager.MarkSceneDirty(instance.scene);
            EditorSceneManager.SaveScene(instance.scene);
            Debug.Log($"[KnightPrefabTool] Knight reemplazado por el prefab en '{instance.scene.name}'. Cámaras reenlazadas: {System.Linq.Enumerable.Count(follow, f => f)}.");
        }
    }
}
