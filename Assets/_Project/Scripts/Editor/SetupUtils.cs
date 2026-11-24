using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Ayudas comunes de los menús de la línea B (enemigos, hordas, flujo del nivel, audio y menús): abrir Pueblo,
    /// buscar o crear objetos raíz, asignar campos privados con SerializedObject, crear materiales provisionales y guardar.
    /// Los menús que las usan se pueden ejecutar varias veces: reutilizan lo que ya existe en lugar de duplicarlo.
    /// </summary>
    public static class SetupUtils
    {
        public const string PuebloPath = "Assets/_Project/Scenes/Pueblo.unity";

        /// <summary>false (y un error en la consola) si el editor está en Play.</summary>
        public static bool NotPlaying(string tag)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            Debug.LogError($"[{tag}] Sal de Play antes de ejecutar esto.");
            return false;
        }

        /// <summary>Abre Pueblo si no es la escena abierta (pregunta antes si hay cambios sin guardar). false si se canceló.</summary>
        public static bool OpenPueblo()
        {
            if (SceneManager.GetActiveScene().path == PuebloPath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(PuebloPath, OpenSceneMode.Single);
            return true;
        }

        public static GameObject FindRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        public static GameObject GetOrCreateRoot(string name)
        {
            GameObject root = FindRoot(name);
            if (root != null) return root;
            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Crear " + name);
            return root;
        }

        public static GameObject GetOrCreateChild(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        public static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        /// <summary>El primer componente de ese tipo en la escena abierta, aunque esté desactivado.</summary>
        public static T FindInScene<T>() where T : Component
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        public static List<T> FindAllInScene<T>() where T : Component
        {
            var all = new List<T>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                all.AddRange(root.GetComponentsInChildren<T>(true));
            return all;
        }

        // ---------------------------------------------------------------- Campos privados ([SerializeField])

        public static void Set(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = Find(serialized, field);
            if (property == null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = Find(serialized, field);
            if (property == null) return;
            property.boolValue = value;
            serialized.ApplyModifiedProperties();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = Find(serialized, field);
            if (property == null) return;
            property.floatValue = value;
            serialized.ApplyModifiedProperties();
        }

        public static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = Find(serialized, field);
            if (property == null) return;
            property.intValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static SerializedProperty Find(SerializedObject serialized, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) Debug.LogWarning($"[SetupUtils] {serialized.targetObject.GetType().Name} no tiene el campo '{field}'.");
            return property;
        }

        // ---------------------------------------------------------------- Assets

        /// <summary>Crea la carpeta (y las que falten antes) si no existe. Ruta del estilo Assets/_Project/...</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>Material de URP de un color. Si ya existe en esa ruta, lo reutiliza sin cambiarlo.</summary>
        public static Material CreateMaterial(string path, Color color, bool unlit)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            string shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
            material = new Material(Shader.Find(shaderName));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Pone la escena en Build Profiles si no está, en la posición indicada (o al final).</summary>
        public static void EnsureInBuild(string scenePath, int index = -1)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath || string.IsNullOrEmpty(s.path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) == null);
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (index < 0 || index > scenes.Count) scenes.Add(entry);
            else scenes.Insert(index, entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void SaveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
