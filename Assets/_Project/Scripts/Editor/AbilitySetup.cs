using System.IO;
using TheLastBeat.Abilities;
using TheLastBeat.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Crea dos habilidades de prueba (Golpe de suelo y Onda giratoria), las asigna a las dos ranuras del
    /// AbilityCaster del prefab del Knight y pone un HUD de recargas en Test_Combo.
    /// Menú: The Last Beat/Habilidades/Crear habilidades de prueba y asignarlas al Knight. Es repetible:
    /// no pisa los assets de habilidades que ya existan (así tus ajustes se conservan).
    /// </summary>
    public static class AbilitySetup
    {
        private const string Folder = "Assets/_Project/ScriptableObjects/Abilities";
        private const string SlamPath = Folder + "/GolpeDeSuelo.asset";
        private const string SpinPath = Folder + "/OndaGiratoria.asset";
        private const string SpinWithBlastPath = Folder + "/GiroConExplosion.asset";
        private const string TestScenePath = "Assets/_Project/Scenes/Test_Combo.unity";

        [MenuItem("The Last Beat/Habilidades/Crear habilidades de prueba y asignarlas al Knight")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[AbilitySetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabTool.PrefabPath) == null)
            {
                Debug.LogError($"[AbilitySetup] No existe {KnightPrefabTool.PrefabPath}.");
                return;
            }

            Directory.CreateDirectory(Folder);
            ShockwaveAbilityData slam = LoadOrCreate<ShockwaveAbilityData>(SlamPath, a =>
            {
                a.displayName = "Golpe de suelo";
                a.description = "Golpea el suelo delante del héroe y empuja a los enemigos.";
                a.cooldown = 5f; a.damage = 35f; a.radius = 3f; a.knockback = 8f;
                a.animatorState = "Heavy1";
                a.windup = 0.35f; a.recovery = 0.3f; a.forwardOffset = 1.2f;
                a.upgrades = new[]
                {
                    new AbilityUpgrade { title = "Más daño", description = "+25 % de daño.", damageMultiplier = 1.25f },
                    new AbilityUpgrade { title = "Más alcance", description = "+20 % de radio.", radiusMultiplier = 1.2f },
                    new AbilityUpgrade { title = "Recarga rápida", description = "-15 % de recarga.", cooldownMultiplier = 0.85f },
                };
            });
            ShockwaveAbilityData spin = LoadOrCreate<ShockwaveAbilityData>(SpinPath, a =>
            {
                a.displayName = "Onda giratoria";
                a.description = "Onda alrededor del héroe que alcanza a todos los que lo rodean.";
                a.cooldown = 8f; a.damage = 25f; a.radius = 4.5f; a.knockback = 6f;
                a.animatorState = "Heavy2";
                a.windup = 0.25f; a.recovery = 0.35f; a.forwardOffset = 0f;
                a.upgrades = new[]
                {
                    new AbilityUpgrade { title = "Más daño", description = "+30 % de daño.", damageMultiplier = 1.3f },
                    new AbilityUpgrade { title = "Recarga rápida", description = "-20 % de recarga.", cooldownMultiplier = 0.8f },
                };
            });

            AssignToPrefab(slam, spin);
            AddHudToTestScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[AbilitySetup] Listo. En Test_Combo: Q = Golpe de suelo, E = Onda giratoria. Edita los valores en " + Folder + ".");
        }

        private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        [MenuItem("The Last Beat/Habilidades/Configurar Giro con explosión (tarea 10)")]
        public static void SetupSpinWithBlast()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[AbilitySetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabTool.PrefabPath) == null)
            {
                Debug.LogError($"[AbilitySetup] No existe {KnightPrefabTool.PrefabPath}.");
                return;
            }

            // 1. Clip Spin en Hero.fbx y su estado en Hero.controller (sin tocar escenas ni prefabs).
            if (HeroTestSetup.ConfigureModelAndController() == null) return;

            // 2. El asset de la habilidad (si ya existe no se pisa: tus ajustes se conservan).
            Directory.CreateDirectory(Folder);
            SpinAbilityData spin = LoadOrCreate<SpinAbilityData>(SpinWithBlastPath, a =>
            {
                a.displayName = "Giro con explosión";
                a.description = "El caballero gira golpeando todo a su alrededor y termina con una explosión en área.";
                a.cooldown = 10f; a.damage = 8f; a.radius = 2.8f; a.knockback = 2f;
                a.animatorState = "Spin";
                a.upgrades = new[]
                {
                    new AbilityUpgrade { title = "Más daño", description = "+25 % de daño en el giro y la explosión.", damageMultiplier = 1.25f },
                    new AbilityUpgrade { title = "Más alcance", description = "+20 % de radio.", radiusMultiplier = 1.2f },
                    new AbilityUpgrade { title = "Recarga rápida", description = "-20 % de recarga.", cooldownMultiplier = 0.8f },
                };
            });

            // 3. En la ranura 2 (E) del prefab del Knight, sustituyendo a la Onda giratoria de prueba.
            GameObject root = PrefabUtility.LoadPrefabContents(KnightPrefabTool.PrefabPath);
            try
            {
                var caster = root.GetComponent<AbilityCaster>();
                if (caster == null)
                {
                    Debug.LogError("[AbilitySetup] El prefab del Knight no tiene AbilityCaster. Ejecuta primero 'Crear habilidades de prueba'.");
                    return;
                }

                var serialized = new SerializedObject(caster);
                SerializedProperty slots = serialized.FindProperty("slots");
                SerializedProperty ability = slots.GetArrayElementAtIndex(1).FindPropertyRelative("ability");
                var current = ability.objectReferenceValue as AbilityData;
                if (current == null || current.name == "OndaGiratoria") ability.objectReferenceValue = spin;
                serialized.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(root, KnightPrefabTool.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AbilitySetup] Listo. En Test_Combo: E = Giro con explosión. Ajusta los valores en " + SpinWithBlastPath + ".");
        }

        private static void AssignToPrefab(AbilityData first, AbilityData second)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(KnightPrefabTool.PrefabPath);
            try
            {
                var caster = root.GetComponent<AbilityCaster>();
                if (caster == null) caster = root.AddComponent<AbilityCaster>();

                var serialized = new SerializedObject(caster);
                serialized.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
                SerializedProperty slots = serialized.FindProperty("slots");
                AbilityData[] defaults = { first, second };
                for (int i = 0; i < AbilityCaster.SlotCount && i < slots.arraySize; i++)
                {
                    SerializedProperty ability = slots.GetArrayElementAtIndex(i).FindPropertyRelative("ability");
                    if (ability.objectReferenceValue == null) ability.objectReferenceValue = defaults[i];
                }

                serialized.ApplyModifiedProperties();
                PrefabUtility.SaveAsPrefabAsset(root, KnightPrefabTool.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddHudToTestScene()
        {
            if (EditorSceneManager.GetActiveScene().path != TestScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (Object.FindFirstObjectByType<AbilityDebugHud>() == null)
            {
                var hud = new GameObject("HUD_Habilidades_Debug");
                hud.AddComponent<AbilityDebugHud>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
