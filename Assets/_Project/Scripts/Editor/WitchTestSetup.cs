using System.Linq;
using TheLastBeat.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Prueba de la bruja (rig Mixamo): configura importación, clips (con espejo para strafe derecho),
    /// Animator y reemplaza el modelo temporal del Knight en la escena Pueblo.
    /// Menú: The Last Beat/Test/Setup Witch.
    /// </summary>
    public static class WitchTestSetup
    {
        private const string Root = "Assets/_Project/Art/Models/Witch";
        private const string ModelPath = Root + "/Witch.fbx";
        private const string TexturePath = Root + "/Textures/Witch_BaseColor.png";
        private const string MaterialPath = Root + "/Witch.mat";
        private const string RunningPath = Root + "/Animations/Witch_Running.fbx";
        private const string StrafePath = Root + "/Animations/Witch_LeftStrafe.fbx";
        private const string ControllerPath = Root + "/Witch.controller";
        private const string ScenePath = "Assets/_Project/Scenes/Pueblo.unity";
        private const string FbxMaterialName = "tripo_mat_fd76630c";

        [MenuItem("The Last Beat/Test/Setup Witch")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[WitchTestSetup] Sal de Play antes de ejecutar el setup.");
                return;
            }

            if (!SetupTexture()) return;
            Material material = SetupMaterial();
            Avatar avatar = SetupModel(material);
            if (avatar == null) return;

            SetupAnimationFile(RunningPath, avatar, ("Running", false));
            SetupAnimationFile(StrafePath, avatar, ("LeftStrafe", false), ("RightStrafe", true));

            AnimatorController controller = SetupController();
            if (controller == null) return;

            SetupScene(controller);
            Debug.Log("[WitchTestSetup] Listo. Revisa el avatar de Witch.fbx (Rig > Configure) y prueba en Play.");
        }

        private const string StandingIdlePath = Root + "/Animations/Witch_StandingIdle.fbx";

        /// <summary>
        /// Versión simple: Standing Idle + Running, sin strafe. El Animator pasa de Idle a Run según 'Speed'
        /// y el personaje vuelve a girar hacia donde se mueve (strafeMode apagado).
        /// </summary>
        [MenuItem("The Last Beat/Witch/Apply Simple Locomotion")]
        public static void ApplySimpleLocomotion()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[WitchTestSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError("[WitchTestSetup] Witch.fbx no tiene un avatar Humanoid. Ejecuta primero 'Setup Witch'.");
                return;
            }

            SetupAnimationFile(StandingIdlePath, avatar, ("StandingIdle", false));

            AnimationClip idle = FindClip(StandingIdlePath, "StandingIdle");
            AnimationClip running = FindClip(RunningPath, "Running");
            if (idle == null || running == null)
            {
                Debug.LogError($"[WitchTestSetup] Faltan clips. Idle={idle != null} Running={running != null}");
                return;
            }

            // Se edita el controller en sitio (no se recrea) para conservar su GUID y que la escena no pierda la referencia.
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states.ToArray()) machine.RemoveState(child.state);
            foreach (AnimatorControllerParameter p in controller.parameters.ToArray()) controller.RemoveParameter(p);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState runState = machine.AddState("Run");
            runState.motion = running;
            machine.defaultState = idleState;

            AnimatorStateTransition toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.12f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.12f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            GameObject knight = GameObject.Find("Knight");
            Transform model = knight != null ? knight.transform.Find("Modelo_Bruja") : null;
            if (model == null)
            {
                Debug.LogError("[WitchTestSetup] No hay Knight/Modelo_Bruja en Pueblo. Ejecuta primero 'Setup Witch'.");
                return;
            }

            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var serialized = new SerializedObject(knight.GetComponent<PlayerController>());
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("strafeMode").boolValue = false;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(knight.scene);
            EditorSceneManager.SaveScene(knight.scene);
            Debug.Log("[WitchTestSetup] Locomoción simple lista: Idle <-> Run por 'Speed'.");
        }

        private static bool SetupTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[WitchTestSetup] No existe la textura en {TexturePath}");
                return false;
            }

            importer.maxTextureSize = 512;
            importer.mipmapEnabled = true;
            importer.crunchedCompression = true;
            importer.compressionQuality = 50;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return true;
        }

        private static Material SetupMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Avatar SetupModel(Material material)
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[WitchTestSetup] No existe el modelo en {ModelPath}");
                return null;
            }

            // Pasada 1: rig humanoide, malla ligera, material propio.
            importer.meshCompression = ModelImporterMeshCompression.High;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), FbxMaterialName), material);
            importer.SaveAndReimport();

            // Pasada 2: el clip de 2 frames del FBX rigeado sirve como idle provisional.
            importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults.Length > 0)
            {
                ModelImporterClipAnimation idle = defaults[0];
                idle.name = "Idle";
                idle.loopTime = true;
                importer.clipAnimations = new[] { idle };
                importer.SaveAndReimport();
            }

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[WitchTestSetup] El avatar de Witch.fbx no es un Humanoid válido. Abre Rig > Configure y revisa los huesos.");
                return null;
            }

            return avatar;
        }

        private static void SetupAnimationFile(string path, Avatar avatar, params (string name, bool mirror)[] clips)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[WitchTestSetup] No existe la animación en {path}");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();

            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var result = new ModelImporterClipAnimation[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                // ModelImporterClipAnimation es una clase: cada llamada a defaultClipAnimations da una copia nueva.
                ModelImporterClipAnimation clip = importer.defaultClipAnimations[0];
                clip.name = clips[i].name;
                clip.loopTime = true;
                clip.mirror = clips[i].mirror;
                // El desplazamiento de la cadera sale como root motion y se descarta (Animator.applyRootMotion = false).
                clip.lockRootPositionXZ = false;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                result[i] = clip;
            }

            importer.clipAnimations = result;
            importer.SaveAndReimport();
        }

        private static AnimationClip FindClip(string path, string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == clipName);
        }

        private static AnimatorController SetupController()
        {
            AnimationClip idle = FindClip(ModelPath, "Idle");
            AnimationClip running = FindClip(RunningPath, "Running");
            AnimationClip left = FindClip(StrafePath, "LeftStrafe");
            AnimationClip right = FindClip(StrafePath, "RightStrafe");
            if (idle == null || running == null || left == null || right == null)
            {
                Debug.LogError($"[WitchTestSetup] Faltan clips. Idle={idle != null} Running={running != null} Left={left != null} Right={right != null}");
                return null;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);

            controller.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, Vector2.zero);
            tree.AddChild(running, new Vector2(0f, 1f));
            tree.AddChild(running, new Vector2(0f, -1f));
            tree.AddChild(left, new Vector2(-1f, 0f));
            tree.AddChild(right, new Vector2(1f, 0f));

            // Correr hacia atrás = el mismo clip de correr reproducido al revés.
            ChildMotion[] children = tree.children;
            children[2].timeScale = -1f;
            tree.children = children;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void SetupScene(AnimatorController controller)
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            GameObject knight = GameObject.Find("Knight");
            if (knight == null)
            {
                Debug.LogError("[WitchTestSetup] No hay un GameObject 'Knight' en Pueblo.");
                return;
            }

            Transform placeholder = knight.transform.Find("Modelo_Temporal");
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            Transform old = knight.transform.Find("Modelo_Bruja");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, knight.transform);
            instance.name = "Modelo_Bruja";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * 1.8f;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var player = knight.GetComponent<PlayerController>();
            var serialized = new SerializedObject(player);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("strafeMode").boolValue = true;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(knight.scene);
            EditorSceneManager.SaveScene(knight.scene);
        }
    }
}
