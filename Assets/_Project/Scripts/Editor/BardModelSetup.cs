using System.Collections.Generic;
using System.Linq;
using TheLastBeat.Companions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Instala el modelo 3D de la bardo (Bard.fbx, hecho con Tools/Blender/build_bard_tripo.py a partir del modelo de Tripo3D) en Bardo.prefab:
    /// importación Humanoid con los clips Idle, Run, Sing, Hit y Die, Bard.controller con los parámetros
    /// Speed, Singing, Hit, Die y Attack, y el modelo como hijo del prefab con BardAnimator en la raíz.
    /// No toca escenas. Menú: The Last Beat/Bardo/Instalar modelo 3D. Es repetible.
    /// </summary>
    public static class BardModelSetup
    {
        private const string Root = "Assets/_Project/Art/Models/Bard";
        private const string ModelPath = Root + "/Bard.fbx";
        private const string ControllerPath = Root + "/Bard.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Heroes/Bardo.prefab";
        private const string ModelChildName = "Modelo";
        // Materiales con textura de Tripo: nombre del material dentro del FBX -> (textura, material URP que lo sustituye).
        private static readonly (string name, string texture, string material)[] TexturedMaterials =
        {
            ("BardBody", Root + "/Textures/BardBody.jpg", Root + "/BardBody.mat"),
            ("BardLaud", Root + "/Textures/BardLaud.jpg", Root + "/BardLaud.mat"),
        };

        // Nombre del take exportado por Blender -> (nombre del clip, ¿en bucle?).
        private static readonly (string take, string clip, bool loop)[] Clips =
        {
            ("Armature|Idle", "Idle", true),
            ("Armature|Run", "Run", true),
            ("Armature|Sing", "Sing", true),
            ("Armature|Hit", "Hit", false),
            ("Armature|Die", "Die", false),
        };

        [MenuItem("The Last Beat/Bardo/Instalar modelo 3D")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[BardModelSetup] Sal de Play antes de ejecutar esto.");
                return;
            }

            if (AssetImporter.GetAtPath(ModelPath) == null)
            {
                Debug.LogError($"[BardModelSetup] No existe {ModelPath}. Genera el FBX con Tools/Blender/build_bard_tripo.py.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[BardModelSetup] No existe {PrefabPath}. Ejecuta antes The Last Beat > Bardo > Crear bardo de prueba en Test_Combo.");
                return;
            }

            if (!SetupModel()) return;
            AnimatorController controller = SetupController();
            if (controller == null) return;
            SetupPrefab(controller);
            Debug.Log("[BardModelSetup] Listo. Abre Test_Combo y dale Play: la bardo corre, toca el laúd al cantar y reacciona a golpes (O en SongDebugHud).");
        }

        private static bool SetupModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);

            // Si el FBX cambió (otro modelo, otro esqueleto), la configuración del avatar guardada en el .meta ya no
            // coincide con los huesos ("Avatar Rig Configuration mis-match"). Se pasa por Generic para regenerarla.
            if (importer.animationType == ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
                importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.meshCompression = ModelImporterMeshCompression.High;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;

            // El cuerpo y la guitarra usan la textura de Tripo: un material URP propio sustituye al que trae el FBX.
            foreach (var spec in TexturedMaterials)
            {
                Material material = EnsureTexturedMaterial(spec.texture, spec.material);
                if (material != null)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.name), material);
            }

            importer.SaveAndReimport();

            // Segunda pasada: ya existen los takes por defecto.
            importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            var result = new List<ModelImporterClipAnimation>();
            foreach (var spec in Clips)
            {
                ModelImporterClipAnimation clip = defaults.FirstOrDefault(d => d.takeName == spec.take);
                if (clip == null)
                {
                    Debug.LogError($"[BardModelSetup] El FBX no tiene el take '{spec.take}'. Takes: {string.Join(", ", defaults.Select(d => d.takeName))}");
                    return false;
                }

                clip.name = spec.clip;
                clip.loopTime = spec.loop;
                clip.lockRootHeightY = false;     // Die baja las caderas hasta el suelo: la altura debe conservarse
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false;
                clip.lockRootRotation = false;
                result.Add(clip);
            }

            importer.clipAnimations = result.ToArray();
            importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[BardModelSetup] El avatar de Bard.fbx no es un Humanoid válido. Abre Rig > Configure y revisa los huesos.");
                return false;
            }

            return true;
        }

        private static Material EnsureTexturedMaterial(string texturePath, string materialPath)
        {
            AssetDatabase.ImportAsset(texturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[BardModelSetup] No existe {texturePath}: ese material se queda como viene en el FBX. " +
                                 "Genera el modelo con Tools/Blender/build_bard_tripo.py (--textura y --textura-guitarra).");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
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
                if (pair.Value != null) continue;
                Debug.LogError($"[BardModelSetup] Falta el clip '{pair.Key}' en Bard.fbx.");
                return null;
            }

            // Se edita en sitio para conservar el GUID del controller.
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in machine.states.ToArray()) machine.RemoveState(child.state);
            foreach (AnimatorControllerParameter p in controller.parameters.ToArray()) controller.RemoveParameter(p);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Singing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger); // reservado (tarea 28): la bardo no ataca

            AnimatorState Add(string name, Vector3 position)
            {
                AnimatorState state = machine.AddState(name, position);
                state.motion = clips[name];
                return state;
            }

            AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration)
            {
                AnimatorStateTransition t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = duration;
                return t;
            }

            AnimatorState idle = Add("Idle", new Vector3(250, 0, 0));
            AnimatorState run = Add("Run", new Vector3(250, 120, 0));
            AnimatorState sing = Add("Sing", new Vector3(250, -120, 0));
            AnimatorState hit = Add("Hit", new Vector3(520, 0, 0));
            AnimatorState die = Add("Die", new Vector3(520, 120, 0));
            machine.defaultState = idle;

            // Locomoción: correr tiene prioridad; quieta y cantando toca el laúd.
            Link(idle, run, 0.12f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Link(run, idle, 0.12f).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            AnimatorStateTransition t = Link(idle, sing, 0.2f);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Singing");
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t = Link(sing, idle, 0.2f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Singing");
            Link(sing, run, 0.12f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Golpe: desde cualquier estado menos Die; al terminar vuelve a Idle (y de ahí a Run o Sing según los parámetros).
            AnimatorStateTransition any = machine.AddAnyStateTransition(hit);
            any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = true;
            any.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            AnimatorStateTransition back = hit.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.1f;

            // Muerte: sin salida. Al revivir BardAnimator reinicia el Animator.
            any = machine.AddAnyStateTransition(die);
            any.hasExitTime = false; any.duration = 0.1f; any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void SetupPrefab(AnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Escala 1 en la raíz: el modelo mide 1.65 m reales. El collider conserva el tamaño de la cápsula
                // (0.8 x 1.7 m) pero ahora está apoyado en el suelo en lugar de hundido en él.
                root.transform.localScale = Vector3.one;
                var collider = root.GetComponent<CapsuleCollider>();
                if (collider != null)
                {
                    collider.radius = 0.4f;
                    collider.height = 1.7f;
                    collider.center = new Vector3(0f, 0.85f, 0f);
                    collider.direction = 1;
                }

                var capsuleRenderer = root.GetComponent<MeshRenderer>();
                if (capsuleRenderer != null) capsuleRenderer.enabled = false;

                Transform old = root.transform.Find(ModelChildName);
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                model.name = ModelChildName;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;

                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var bardAnimator = root.GetComponent<BardAnimator>() ?? root.AddComponent<BardAnimator>();
                var serialized = new SerializedObject(bardAnimator);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
