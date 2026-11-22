using TheLastBeat.Combat;
using TheLastBeat.Companions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Crea la bardo provisional (una cápsula con NavMeshAgent, BardCompanion y Health), la guarda como prefab
    /// y la pone en Test_Combo cerca del Knight. La bardo real (tarea 23) sustituirá la cápsula.
    /// Menú: The Last Beat/Bardo/Crear bardo de prueba en Test_Combo. Es repetible.
    /// </summary>
    public static class BardSetup
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Bardo.prefab";
        private const string MaterialPath = "Assets/_Project/Art/Models/Bard/BardoProvisional.mat";
        private const string TestScenePath = "Assets/_Project/Scenes/Test_Combo.unity";

        [MenuItem("The Last Beat/Bardo/Crear bardo de prueba en Test_Combo")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[BardSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != TestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            GameObject knight = GameObject.Find("Knight");
            if (knight == null)
            {
                Debug.LogError("[BardSetup] No hay un 'Knight' en la escena abierta.");
                return;
            }

            // El NavMesh se vuelve a hornear antes de poner a la bardo (para incluir la pared de prueba y los maniquíes).
            HeroTestSetup.RebakeNavMesh();

            GameObject old = GameObject.Find("Bardo");
            if (old != null) Object.DestroyImmediate(old);

            GameObject bard = BuildBard();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bard, PrefabPath);
            Object.DestroyImmediate(bard);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Vector3 position = knight.transform.position + new Vector3(-3f, 0f, -3f);
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) position = hit.position;
            instance.name = "Bardo";
            instance.transform.position = position;

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BardSetup] Listo. Bardo creada en Test_Combo y guardada en " + PrefabPath + ".");
        }

        private static GameObject BuildBard()
        {
            GameObject bard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bard.name = "Bardo";
            bard.layer = LayerMask.NameToLayer("Ally");
            bard.transform.localScale = new Vector3(0.8f, 0.85f, 0.8f);
            bard.GetComponent<Renderer>().sharedMaterial = BardMaterial();

            // BardCompanion exige NavMeshAgent y Health: Unity los añade solos.
            bard.AddComponent<BardCompanion>();

            var agent = bard.GetComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.7f;
            agent.baseOffset = 0f;
            agent.acceleration = 20f;
            agent.angularSpeed = 540f;
            agent.avoidancePriority = 60;

            var health = bard.GetComponent<Health>();
            var serialized = new SerializedObject(health);
            serialized.FindProperty("maxHealth").floatValue = 80f;
            serialized.FindProperty("team").enumValueIndex = (int)Team.Heroes;
            serialized.ApplyModifiedProperties();
            return bard;
        }

        private static Material BardMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.85f, 0.45f, 0.85f));
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
