using System.Linq;
using TheLastBeat.Combat;
using TheLastBeat.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Deja los modelos de arte (tareas 22 a 28) listos en sus prefabs. Repetible.
    /// Menú: The Last Beat/Arte/...  También se puede lanzar sin abrir el editor:
    ///   Unity -batchmode -quit -projectPath . -executeMethod TheLastBeat.EditorTools.CharacterArtSetup.SetupKnight
    /// </summary>
    public static class CharacterArtSetup
    {
        private const string KnightModelPath = "Assets/_Project/Art/Models/Caballero/Caballero.fbx";
        private const string SwordPath = "Assets/_Project/Art/Models/Caballero/Espadon.fbx";
        private const string KnightTexturePath = "Assets/_Project/Art/Models/Caballero/Caballero_Color.jpg";
        private const string KnightMaterialPath = "Assets/_Project/Art/Models/Caballero/Caballero.mat";
        private const string SwordTexturePath = "Assets/_Project/Art/Models/Caballero/Espadon_Color.jpg";
        private const string SwordMaterialPath = "Assets/_Project/Art/Models/Caballero/Espadon.mat";

        private const string HeroModelPath = "Assets/_Project/Art/Models/Hero/Hero.fbx";
        private const string SoldierRoot = "Assets/_Project/Art/Models/Soldado";
        private const string SoldierModelPath = SoldierRoot + "/Soldado.fbx";
        private const string SoldierTexturePath = SoldierRoot + "/Soldado_Color.jpg";
        private const string SoldierMaterialPath = SoldierRoot + "/Soldado.mat";
        private const string ScimitarPath = SoldierRoot + "/Cimitarra.fbx";
        private const string ScimitarTexturePath = SoldierRoot + "/Cimitarra_Color.jpg";
        private const string ScimitarMaterialPath = SoldierRoot + "/Cimitarra.mat";
        private const string SoldierControllerPath = SoldierRoot + "/Soldado.controller";
        private const string SoldierPrefabPath = "Assets/_Project/Prefabs/Enemies/Soldado.prefab";
        private const float SoldierHeight = 1.8f;
        // Piezas de la cápsula provisional; AvisoAtaque se queda (es el aviso del golpe).
        private static readonly string[] SoldierPlaceholderParts = { "Cuerpo", "Frente", "Cuerno_D", "Cuerno_I" };

        private const float HeroHeight = 1.8f;

        /// <summary>
        /// Pone el caballero de Tripo3D (rig de Mixamo) en Knight.prefab en lugar del modelo provisional:
        /// importa Caballero.fbx como Humanoid a 1,8 m, lo anima con Hero.controller (los mismos clips, por
        /// retargeting), copia los componentes del modelo viejo (CombatAnimationEvents, DamageAnimations),
        /// reconecta las referencias al Animator y pone el espadón en la mano derecha.
        /// </summary>
        [MenuItem("The Last Beat/Arte/Caballero: poner el modelo de Tripo3D en Knight.prefab")]
        public static void SetupKnightModel()
        {
            GameObject model = ImportHumanoid(KnightModelPath, HeroHeight);
            GameObject swordAsset = ImportStatic(SwordPath);
            if (model == null || swordAsset == null) return;
            UseTexturedMaterial(KnightModelPath, KnightTexturePath, KnightMaterialPath);
            UseTexturedMaterial(SwordPath, SwordTexturePath, SwordMaterialPath);

            GameObject root = PrefabUtility.LoadPrefabContents(KnightPrefabTool.PrefabPath);
            try
            {
                Animator oldAnimator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault();
                if (oldAnimator == null)
                {
                    Debug.LogError("[CharacterArtSetup] Knight.prefab no tiene Animator.");
                    return;
                }

                GameObject oldModel = oldAnimator.gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromSource(oldModel) == model)
                {
                    // Ya está puesto: solo se rehace el espadón (por si cambió el modelo o el arma).
                    foreach (Transform t in oldModel.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Espadon").ToArray())
                        Object.DestroyImmediate(t.gameObject);
                    PlaceSword(root.transform, oldAnimator, swordAsset);
                    PrefabUtility.SaveAsPrefabAsset(root, KnightPrefabTool.PrefabPath);
                    LogKnight(oldModel, oldAnimator);
                    return;
                }

                var newModel = (GameObject)PrefabUtility.InstantiatePrefab(model, oldModel.transform.parent);
                newModel.name = "Modelo_Caballero";
                newModel.transform.SetLocalPositionAndRotation(oldModel.transform.localPosition, oldModel.transform.localRotation);
                newModel.transform.SetSiblingIndex(oldModel.transform.GetSiblingIndex());

                Animator animator = newModel.GetComponent<Animator>() ?? newModel.AddComponent<Animator>();
                animator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = oldAnimator.cullingMode;
                animator.updateMode = oldAnimator.updateMode;

                foreach (MonoBehaviour behaviour in oldModel.GetComponents<MonoBehaviour>())
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(behaviour);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newModel);
                }

                Remap(root, oldModel, newModel, animator);
                // El cubo "Frente" marcaba hacia dónde miraba el héroe provisional; con el modelo real sobra.
                Transform frente = root.transform.Find("Frente");
                if (frente != null && frente.TryGetComponent(out MeshRenderer marker)) marker.enabled = false;
                PlaceSword(root.transform, animator, swordAsset);
                Object.DestroyImmediate(oldModel);
                PrefabUtility.SaveAsPrefabAsset(root, KnightPrefabTool.PrefabPath);
                LogKnight(newModel, animator);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// En Test_Combo el Knight arrastraba cambios de cuando se armó la escena (un Modelo_Heroe y un PlayerCombat
        /// añadidos en la escena y dos campos animator cambiados), que con el modelo nuevo lo hacían verse doble.
        /// Lo deja como el prefab (igual que en Pueblo) sin tocar su posición.
        /// </summary>
        [MenuItem("The Last Beat/Arte/Test_Combo: dejar el Knight igual al prefab")]
        public static void CleanTestComboKnight()
        {
            const string scenePath = "Assets/_Project/Scenes/Test_Combo.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabTool.PrefabPath);
            GameObject knight = scene.GetRootGameObjects()
                .FirstOrDefault(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == prefab);
            if (knight == null)
            {
                Debug.LogError("[CharacterArtSetup] No hay un Knight del prefab en Test_Combo.");
                return;
            }

            foreach (var added in PrefabUtility.GetAddedGameObjects(knight))
            {
                Debug.Log($"[CharacterArtSetup] Test_Combo: quito el objeto añadido '{added.instanceGameObject.name}'.");
                added.Revert();
            }

            foreach (var added in PrefabUtility.GetAddedComponents(knight))
            {
                Debug.Log($"[CharacterArtSetup] Test_Combo: quito el componente añadido {added.instanceComponent.GetType().Name}.");
                added.Revert();
            }

            foreach (var removed in PrefabUtility.GetRemovedComponents(knight))
            {
                Debug.Log($"[CharacterArtSetup] Test_Combo: restauro el componente quitado {removed.assetComponent.GetType().Name}.");
                removed.Revert();
            }

            foreach (MonoBehaviour behaviour in knight.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var so = new SerializedObject(behaviour);
                SerializedProperty animatorField = so.FindProperty("animator");
                if (animatorField == null || !animatorField.prefabOverride) continue;
                PrefabUtility.RevertPropertyOverride(animatorField, InteractionMode.AutomatedAction);
                Debug.Log($"[CharacterArtSetup] Test_Combo: {behaviour.GetType().Name}.animator vuelve al del prefab.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        private static void LogKnight(GameObject model, Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Material material = model.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMaterial;
            Debug.Log($"[CharacterArtSetup] Caballero listo: cabeza a {head.position.y:F2} m; mano a {hand.position.y:F2} m y " +
                      $"hombro a {shoulder.position.y:F2} m (en T-pose deben coincidir); material '{material?.name}' con " +
                      $"shader '{material?.shader.name}' y textura '{(material != null && material.mainTexture != null ? material.mainTexture.name : "NINGUNA")}'.");
        }

        /// <summary>
        /// Pone el demonio soldado de Tripo3D (rig de Mixamo, reducido a menos de 8.000 triángulos) en Soldado.prefab:
        /// modelo Humanoid a 1,8 m, Soldado.controller con los parámetros que usa EnemySoldier (Speed, Attack, Hit y
        /// Die) y los clips del caballero (Idle, Run, Light1 como ataque, Hit y Die), la cimitarra en la mano derecha,
        /// el Animator asignado a EnemySoldier y las piezas de la cápsula apagadas. Repetible.
        /// </summary>
        [MenuItem("The Last Beat/Arte/Soldado: modelo, animaciones y cimitarra en Soldado.prefab")]
        public static void SetupSoldier()
        {
            GameObject model = ImportHumanoid(SoldierModelPath, SoldierHeight);
            GameObject scimitar = ImportStatic(ScimitarPath);
            if (model == null || scimitar == null) return;
            UseTexturedMaterial(SoldierModelPath, SoldierTexturePath, SoldierMaterialPath);
            UseTexturedMaterial(ScimitarPath, ScimitarTexturePath, ScimitarMaterialPath);
            AnimatorController controller = BuildSoldierController();
            if (controller == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(SoldierPrefabPath);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().Where(t => t.name == "Modelo_Soldado").ToArray())
                    Object.DestroyImmediate(child.gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "Modelo_Soldado";
                Vector3 s = root.transform.localScale;
                instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
                Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                instance.AddComponent<AnimationEventSink>();

                foreach (Transform part in root.GetComponentsInChildren<Transform>(true).Where(t => SoldierPlaceholderParts.Contains(t.name)))
                    if (part.TryGetComponent(out MeshRenderer placeholder)) placeholder.enabled = false;

                var soldier = root.GetComponent<EnemySoldier>();
                var so = new SerializedObject(soldier);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();

                PlaceSword(root.transform, animator, scimitar, "Cimitarra");
                PrefabUtility.SaveAsPrefabAsset(root, SoldierPrefabPath);
                int tris = instance.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r => r.sharedMesh.triangles.Length / 3);
                Debug.Log($"[CharacterArtSetup] Soldado listo: {tris} triángulos, {instance.GetComponentsInChildren<Renderer>().Length} " +
                          "renderers (cuerpo y cimitarra), Animator asignado a EnemySoldier.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Controller del soldado con los clips del caballero (Humanoid: sirven para cualquier personaje).</summary>
        private static AnimatorController BuildSoldierController()
        {
            AnimationClip[] heroClips = AssetDatabase.LoadAllAssetsAtPath(HeroModelPath).OfType<AnimationClip>().ToArray();
            AnimationClip Clip(string n) => heroClips.FirstOrDefault(c => c.name == n);
            AnimationClip idle = Clip("Idle"), run = Clip("Run"), attack = Clip("Light1"), hit = Clip("Hit"), die = Clip("Die");
            if (new[] { idle, run, attack, hit, die }.Any(c => c == null))
            {
                Debug.LogError("[CharacterArtSetup] Faltan clips en Hero.fbx (Idle, Run, Light1, Hit o Die).");
                return null;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SoldierControllerPath)
                                            ?? AnimatorController.CreateAnimatorControllerAtPath(SoldierControllerPath);
            while (controller.layers.Length > 1) controller.RemoveLayer(controller.layers.Length - 1);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states.ToArray()) machine.RemoveState(child.state);
            foreach (AnimatorStateTransition any in machine.anyStateTransitions.ToArray()) machine.RemoveAnyStateTransition(any);
            foreach (AnimatorControllerParameter p in controller.parameters.ToArray()) controller.RemoveParameter(p);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorState Add(string name, AnimationClip clip, float x, float y)
            {
                AnimatorState state = machine.AddState(name, new Vector3(x, y, 0));
                state.motion = clip;
                return state;
            }

            AnimatorState idleState = Add("Idle", idle, 250, 0);
            AnimatorState runState = Add("Run", run, 250, 120);
            machine.defaultState = idleState;
            AnimatorStateTransition t = idleState.AddTransition(runState);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = runState.AddTransition(idleState);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Ataque y golpe: desde cualquier estado por trigger, y de vuelta a Idle al terminar. Morir no tiene salida:
            // el pool hace Rebind al reutilizar el soldado.
            foreach (var (trigger, clip, back, self) in new[] { ("Attack", attack, true, false), ("Hit", hit, true, true), ("Die", die, false, false) })
            {
                AnimatorState state = Add(trigger, clip, 560, trigger == "Attack" ? -60 : trigger == "Hit" ? 60 : 180);
                AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
                enter.hasExitTime = false; enter.duration = 0.06f; enter.canTransitionToSelf = self;
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                if (!back) continue;
                AnimatorStateTransition exit = state.AddTransition(idleState);
                exit.hasExitTime = true; exit.exitTime = 0.9f; exit.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>Importa un FBX con rig de Mixamo como Humanoid y lo escala a la altura indicada.</summary>
        private static GameObject ImportHumanoid(string path, float height)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterArtSetup] No existe {path}.");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();

            // Si el FBX cambió de jerarquía (por ejemplo, al pasarlo por Blender para la T-pose), el mapa de huesos
            // guardado en el .meta ya no sirve: se borra y Unity vuelve a mapear el Humanoid solo.
            if (!IsValidHuman(path))
            {
                importer.humanDescription = new HumanDescription();
                importer.SaveAndReimport();
            }

            float measured = Height(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            if (measured > 0f && Mathf.Abs(measured - height) > 0.01f)
            {
                importer.globalScale *= height / measured;
                importer.SaveAndReimport();
            }

            if (!IsValidHuman(path))
            {
                Debug.LogError($"[CharacterArtSetup] El avatar de {path} no es un Humanoid válido.");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// Unity no toma la textura que viene dentro del FBX de Mixamo: se crea un material URP Lit con la textura de
        /// Tripo3D y se le asigna en lugar del material del FBX (remap), así la textura queda como un archivo más.
        /// </summary>
        private static void UseTexturedMaterial(string modelPath, string texturePath, string materialPath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"[CharacterArtSetup] Falta la textura {texturePath}.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            importer.SaveAndReimport();
        }

        private static bool IsValidHuman(string path)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            return avatar != null && avatar.isValid && avatar.isHuman;
        }

        private static GameObject ImportStatic(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterArtSetup] No existe {path}. Prepáralo con Tools/Blender/preparar_objeto.py.");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static float Height(GameObject asset)
        {
            GameObject temp = Object.Instantiate(asset);
            try
            {
                Renderer[] renderers = temp.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return 0f;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                return bounds.size.y;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        /// <summary>Cambia toda referencia al modelo viejo (Animator, objeto, transform, renderers) por la del nuevo.</summary>
        private static void Remap(GameObject root, GameObject oldModel, GameObject newModel, Animator animator)
        {
            Renderer newRenderer = newModel.GetComponentInChildren<Renderer>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                // Los Transform se saltan: su lista de hijos la mantiene Unity al crear y borrar objetos.
                if (component == null || component is Transform || component.transform.IsChildOf(oldModel.transform) ||
                    component.transform.IsChildOf(newModel.transform)) continue;
                var so = new SerializedObject(component);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object value = it.objectReferenceValue;
                    Object replacement =
                        value is Animator a && a.gameObject == oldModel ? animator :
                        value == oldModel ? newModel :
                        value == oldModel.transform ? newModel.transform :
                        value is Renderer r && r.transform.IsChildOf(oldModel.transform) ? newRenderer : null;
                    if (replacement == null) continue;
                    it.objectReferenceValue = replacement;
                    changed = true;
                    Debug.Log($"[CharacterArtSetup] {component.GetType().Name}.{it.propertyPath} -> {replacement.name}");
                }

                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// El espadón va de hijo de la mano derecha, con la hoja en la prolongación del antebrazo y los filos hacia
        /// delante y atrás (como en el héroe provisional, para el que se hicieron las animaciones). Los ejes se
        /// calculan con la malla, así no depende de cómo convierta Unity los ejes del FBX.
        /// </summary>
        private static void PlaceSword(Transform knight, Animator animator, GameObject swordAsset, string objectName = "Espadon")
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Vector3 arm = (hand.position - forearm.position).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(knight.forward, arm).normalized;

            var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset, hand);
            sword.name = objectName;
            sword.transform.localScale = Vector3.one / hand.lossyScale.x;
            sword.transform.position = Vector3.Lerp(hand.position, middle != null ? middle.position : hand.position, 0.6f);

            MeshFilter filter = sword.GetComponentInChildren<MeshFilter>();
            Bounds b = filter.sharedMesh.bounds;
            Vector3 size = b.size;
            int largo = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            int ancho = Enumerable.Range(0, 3).Where(i => i != largo).OrderByDescending(i => size[i]).First();
            Vector3 Axis(int i) => filter.transform.TransformDirection(i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
            // La hoja va hacia el lado donde está casi toda la malla (el origen está en la empuñadura).
            Vector3 blade = Axis(largo) * Mathf.Sign(b.center[largo]);
            sword.transform.rotation = Quaternion.FromToRotation(blade, arm) * sword.transform.rotation;
            Vector3 width = Vector3.ProjectOnPlane(Axis(ancho), arm).normalized;
            sword.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(width, forward, arm), arm) * sword.transform.rotation;
            Bounds world = sword.GetComponentInChildren<Renderer>().bounds;
            Debug.Log($"[CharacterArtSetup] {objectName} en la mano derecha: {world.size.magnitude:F2} m de diagonal, " +
                      $"hoja hacia {Vector3.Dot((world.center - hand.position).normalized, arm):F2} (1 = en la línea del brazo).");
        }
        [MenuItem("The Last Beat/Arte/Caballero: reimportar Hero.fbx con golpe y muerte")]
        public static void SetupKnight()
        {
            // Clips (con Hit y Die), avatar y Hero.controller (con el estado Die y la capa de golpes).
            if (HeroTestSetup.ConfigureModelAndController() == null)
            {
                Debug.LogError("[CharacterArtSetup] No se pudo configurar Hero.fbx ni Hero.controller.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(KnightPrefabTool.PrefabPath);
            try
            {
                Animator animator = root.GetComponentsInChildren<Animator>(true).FirstOrDefault();
                if (animator == null)
                {
                    Debug.LogError("[CharacterArtSetup] Knight.prefab no tiene Animator.");
                    return;
                }

                if (animator.GetComponent<DamageAnimations>() == null)
                    animator.gameObject.AddComponent<DamageAnimations>();
                PrefabUtility.SaveAsPrefabAsset(root, KnightPrefabTool.PrefabPath);
                Debug.Log($"[CharacterArtSetup] Caballero listo: DamageAnimations en '{animator.name}' de Knight.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
