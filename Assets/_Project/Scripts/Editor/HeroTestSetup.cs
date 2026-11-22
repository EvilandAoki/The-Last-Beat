using System.Linq;
using TheLastBeat.Combat;
using TheLastBeat.DebugTools;
using TheLastBeat.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Prepara el héroe provisional (Hero.fbx, hecho con Tools/Blender/build_hero.py) y la escena de
    /// pruebas Test_Combo (copia reducida de Pueblo): importación Humanoid, Animator con Idle/Run/Light1-2/Heavy1-2,
    /// escena sin casas ni muros y NavMesh propio. No toca Pueblo.unity ni su NavMesh.
    /// Menú: The Last Beat/Hero/Crear escenario de prueba de combate. Es repetible.
    /// </summary>
    public static class HeroTestSetup
    {
        private const string Root = "Assets/_Project/Art/Models/Hero";
        private const string ModelPath = Root + "/Hero.fbx";
        private const string ControllerPath = Root + "/Hero.controller";
        private const string PuebloPath = "Assets/_Project/Scenes/Pueblo.unity";
        private const string ScenePath = "Assets/_Project/Scenes/Test_Combo.unity";
        private const string NavMeshFolder = "Assets/_Project/Scenes/Test_Combo";
        private const string NavMeshPath = NavMeshFolder + "/NavMesh-Test_Combo.asset";
        private const float Fps = 30f;

        // Nombre del take exportado por Blender -> (nombre del clip, ¿en bucle?, frame de impacto, frames del clip).
        // Los frames de impacto y la duración vienen de HIT_FRAMES y crear_clips() en build_hero.py.
        private static readonly (string take, string clip, bool loop, int hitFrame, int frames)[] Clips =
        {
            ("Armature|Idle", "Idle", true, -1, 60),
            ("Armature|Run", "Run", true, -1, 20),
            ("Armature|Spin", "Spin", true, -1, 20),
            ("Armature|Charge", "Charge", true, -1, 16),
            ("Armature|Light1", "Light1", false, 9, 22),
            ("Armature|Light2", "Light2", false, 9, 22),
            ("Armature|Heavy1", "Heavy1", false, 16, 34),
            ("Armature|Heavy2", "Heavy2", false, 17, 38),
        };

        [MenuItem("The Last Beat/Hero/Crear escenario de prueba de combate")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[HeroTestSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (AssetImporter.GetAtPath(ModelPath) == null)
            {
                Debug.LogError($"[HeroTestSetup] No existe {ModelPath}. Genera el FBX con Tools/Blender/build_hero.py.");
                return;
            }

            if (!SetupModel()) return;
            AnimatorController controller = SetupController();
            if (controller == null) return;

            // Con el Knight ya convertido en prefab, rehacer la escena destruiría componentes del prefab y fallaría.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabTool.PrefabPath) != null)
            {
                Debug.LogWarning("[HeroTestSetup] El Knight ya es un prefab: solo se reconfiguraron Hero.fbx y Hero.controller. " +
                                 "Para habilidades, bardo y canción usa los menús The Last Beat > Habilidades / Bardo.");
                return;
            }

            SetupScene(controller);
            Debug.Log("[HeroTestSetup] Listo. Abre Test_Combo, dale Play y ataca con J (débil) y K (fuerte).");
        }

        /// <summary>
        /// Reconfigura solo Hero.fbx (clips y avatar) y Hero.controller, sin tocar ninguna escena ni prefab.
        /// Lo usan los menús de habilidades cuando se añade un clip nuevo.
        /// </summary>
        public static AnimatorController ConfigureModelAndController()
        {
            if (AssetImporter.GetAtPath(ModelPath) == null)
            {
                Debug.LogError($"[HeroTestSetup] No existe {ModelPath}. Genera el FBX con Tools/Blender/build_hero.py.");
                return null;
            }

            return SetupModel() ? SetupController() : null;
        }

        private static bool SetupModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.meshCompression = ModelImporterMeshCompression.High;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;
            importer.SaveAndReimport();

            // Segunda pasada: ya existen los takes por defecto.
            importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            var result = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (var spec in Clips)
            {
                ModelImporterClipAnimation clip = defaults.FirstOrDefault(d => d.takeName == spec.take);
                if (clip == null)
                {
                    Debug.LogError($"[HeroTestSetup] El FBX no tiene el take '{spec.take}'. Takes: {string.Join(", ", defaults.Select(d => d.takeName))}");
                    return false;
                }

                clip.name = spec.clip;
                clip.loopTime = spec.loop;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false; // el desplazamiento de las caderas queda como root motion y se descarta
                if (spec.hitFrame >= 0)
                {
                    // En el importador el tiempo de un evento es normalizado (0 a 1).
                    clip.events = new[]
                    {
                        new AnimationEvent { functionName = "OnHit", time = spec.hitFrame / (float)spec.frames },
                    };
                }

                result.Add(clip);
            }

            importer.clipAnimations = result.ToArray();
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[HeroTestSetup] El avatar de Hero.fbx no es un Humanoid válido. Abre Rig > Configure y revisa los huesos.");
                return false;
            }

            return true;
        }

        private static AnimationClip FindClip(string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == clipName);
        }

        private static AnimatorController SetupController()
        {
            var clips = Clips.ToDictionary(c => c.clip, c => FindClip(c.clip));
            foreach (var pair in clips)
            {
                if (pair.Value == null)
                {
                    Debug.LogError($"[HeroTestSetup] Falta el clip '{pair.Key}' en Hero.fbx.");
                    return null;
                }
            }

            // Se edita en sitio (no se recrea) para conservar el GUID y que la escena no pierda la referencia.
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states.ToArray()) machine.RemoveState(child.state);
            foreach (AnimatorControllerParameter p in controller.parameters.ToArray()) controller.RemoveParameter(p);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);

            AnimatorState Add(string name, AnimationClip clip, Vector3 position)
            {
                AnimatorState state = machine.AddState(name, position);
                state.motion = clip;
                return state;
            }

            AnimatorState idle = Add("Idle", clips["Idle"], new Vector3(250, 0, 0));
            AnimatorState run = Add("Run", clips["Run"], new Vector3(250, 120, 0));
            machine.defaultState = idle;

            // Locomoción
            AnimatorStateTransition t = idle.AddTransition(run);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = run.AddTransition(idle);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Spin (giro con explosión): lo lanza AbilityCaster con CrossFade y la propia habilidad decide cuándo salir.
            Add("Spin", clips["Spin"], new Vector3(520, -120, 0));
            Add("Charge", clips["Charge"], new Vector3(780, -120, 0));

            // Los golpes los lanza PlayerCombat (CrossFade) y encadena el combo en código.
            // Aquí solo hace falta que cada golpe vuelva a Idle cuando termina sin que lo continúen.
            float y = 0f;
            foreach (string name in new[] { "Light1", "Light2", "Heavy1", "Heavy2" })
            {
                AnimatorState attack = Add(name, clips[name], new Vector3(520, y, 0));
                y += 90f;
                AnimatorStateTransition back = attack.AddTransition(idle);
                back.hasExitTime = true; back.exitTime = 0.92f; back.duration = 0.12f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void SetupScene(AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(PuebloPath, ScenePath))
                {
                    Debug.LogError($"[HeroTestSetup] No se pudo copiar {PuebloPath} a {ScenePath}.");
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Reducir: sin casas ni muros (quedan suelos y plaza).
            Transform blockout = GameObject.Find("Blockout")?.transform;
            foreach (string name in new[] { "Casas", "Muros" })
            {
                Transform t = blockout != null ? blockout.Find(name) : null;
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            GameObject knight = GameObject.Find("Knight");
            if (knight == null)
            {
                Debug.LogError("[HeroTestSetup] No hay un GameObject 'Knight' en la escena.");
                return;
            }

            PlaceKnightInPlaza(knight, blockout);
            SetupHero(knight, controller);
            RebakeNavMesh();
            SetupDummies(knight);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void PlaceKnightInPlaza(GameObject knight, Transform blockout)
        {
            Transform plaza = blockout != null ? blockout.Find("Plaza") : null;
            var renderers = plaza != null ? plaza.GetComponentsInChildren<Renderer>() : new Renderer[0];
            Vector3 position = knight.transform.position;
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                position = new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.min.z + Mathf.Min(4f, bounds.size.z * 0.25f));
            }
            else
            {
                Debug.LogWarning("[HeroTestSetup] No se encontró Blockout/Plaza; el Knight conserva su posición.");
            }

            Physics.SyncTransforms();
            if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 100f, LayerMask.GetMask("Ground")))
                position.y = hit.point.y;

            var controller = knight.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            knight.transform.position = position;
            if (controller != null) controller.enabled = true;

            // La cámara cenital conserva su desfase respecto al Knight (0, 12, -8).
            foreach (string cameraName in new[] { "CM_Seguimiento", "Main Camera" })
            {
                GameObject cam = GameObject.Find(cameraName);
                if (cam != null) cam.transform.position = position + new Vector3(0f, 12f, -8f);
            }
        }

        private static void SetupHero(GameObject knight, AnimatorController controller)
        {
            Transform placeholder = knight.transform.Find("Modelo_Temporal");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            Transform old = knight.transform.Find("Modelo_Heroe");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, knight.transform);
            instance.name = "Modelo_Heroe";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // Los Animation Events (OnHit) solo llegan al objeto del Animator; este componente los reenvía.
            if (instance.GetComponent<CombatAnimationEvents>() == null) instance.AddComponent<CombatAnimationEvents>();

            var player = knight.GetComponent<PlayerController>();
            var serialized = new SerializedObject(player);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.ApplyModifiedProperties();

            // El árbol de combos cambió (Light1-2, Heavy1-2): se recrea el componente, pero se conservan
            // los tiempos que ya hayas ajustado en el Inspector.
            string[] tunables = { "comboWindow", "inputBuffer", "stepDelay", "finisherRecovery", "movementWhileAttacking" };
            var saved = new System.Collections.Generic.Dictionary<string, float>();
            var oldCombat = knight.GetComponent<PlayerCombat>();
            if (oldCombat != null)
            {
                var oldSerialized = new SerializedObject(oldCombat);
                foreach (string name in tunables)
                {
                    SerializedProperty prop = oldSerialized.FindProperty(name);
                    if (prop != null) saved[name] = prop.floatValue;
                }

                Object.DestroyImmediate(oldCombat);
            }

            var combat = knight.AddComponent<PlayerCombat>();
            var combatSerialized = new SerializedObject(combat);
            combatSerialized.FindProperty("animator").objectReferenceValue = animator;
            foreach (var pair in saved) combatSerialized.FindProperty(pair.Key).floatValue = pair.Value;
            combatSerialized.ApplyModifiedProperties();
        }

        private static void SetupDummies(GameObject knight)
        {
            GameObject old = GameObject.Find("Maniquies");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("Maniquies");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Vector3[] offsets = { new Vector3(0f, 0f, 3f), new Vector3(2.5f, 0f, 4.5f), new Vector3(-2.5f, 0f, 4.5f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"Maniqui_{i + 1}";
                dummy.transform.SetParent(root.transform);
                dummy.transform.position = knight.transform.position + offsets[i] + Vector3.up;
                dummy.layer = enemyLayer;

                var health = dummy.AddComponent<Health>();
                var serialized = new SerializedObject(health);
                serialized.FindProperty("maxHealth").floatValue = 200f;
                serialized.FindProperty("team").enumValueIndex = (int)Team.Demons;
                serialized.ApplyModifiedProperties();

                dummy.AddComponent<TrainingDummy>();
            }
        }

        /// <summary>Vuelve a hornear el NavMesh de la escena abierta en un asset propio (nunca el de otra escena).</summary>
        public static void RebakeNavMesh()
        {
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[HeroTestSetup] No hay NavMeshSurface en la escena; se omite el NavMesh.");
                return;
            }

            // Datos propios: nunca se sobrescribe el NavMesh de Pueblo.
            surface.navMeshData = null;
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogError("[HeroTestSetup] El NavMesh no generó datos.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(NavMeshFolder)) AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Test_Combo");
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }
    }
}
