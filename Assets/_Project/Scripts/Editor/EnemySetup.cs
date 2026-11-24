using TheLastBeat.Combat;
using TheLastBeat.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Demonio soldado (tarea 13). Menús de The Last Beat/Enemigos:
    ///  1. Crea Prefabs/Enemies/Soldado.prefab: una cápsula provisional roja con cuernos, un "frente" para ver hacia dónde
    ///     mira y el aviso rojo del ataque en el suelo. Cumple docs/INTEGRACION.md: capa Enemy, collider sólido, Health
    ///     del equipo Demons, KnockbackReceiver y NavMeshAgent. El modelo real (tareas 24, 27 y 28) reemplazará la cápsula.
    ///  2. Pone tres soldados de prueba en la plaza de Pueblo, para probar la IA antes del pool y las hordas.
    /// Los dos son repetibles. Volver a crear el prefab conserva su GUID, así que no rompe las referencias.
    /// </summary>
    public static class EnemySetup
    {
        private const string Tag = "EnemySetup";
        public const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Soldado.prefab";
        public const string TestRootName = "Soldados_Prueba";

        private const string MaterialFolder = "Assets/_Project/Art/Models/Soldado";
        private const string BodyMaterialPath = MaterialFolder + "/SoldadoProvisional.mat";
        private const string DetailMaterialPath = MaterialFolder + "/SoldadoDetalles.mat";
        private const string WarningMaterialPath = MaterialFolder + "/AvisoAtaque.mat";

        private static readonly Vector3[] TestPositions =
        {
            new Vector3(0f, 0f, 14f), new Vector3(-4f, 0f, 16f), new Vector3(4f, 0f, 16f),
        };

        [MenuItem("The Last Beat/Enemigos/1. Crear prefab del soldado (tarea 13)")]
        public static void CreatePrefab()
        {
            if (!SetupUtils.NotPlaying(Tag)) return;
            if (LayerMask.NameToLayer("Enemy") < 0)
            {
                Debug.LogError($"[{Tag}] Falta la capa 'Enemy' en Project Settings > Tags and Layers.");
                return;
            }

            SetupUtils.EnsureFolder("Assets/_Project/Prefabs/Enemies");
            GameObject soldier = BuildSoldier();
            PrefabUtility.SaveAsPrefabAsset(soldier, PrefabPath);
            Object.DestroyImmediate(soldier);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{Tag}] Prefab del soldado en {PrefabPath}. Commitéalo con su .meta y los materiales de {MaterialFolder}.");
        }

        [MenuItem("The Last Beat/Enemigos/2. Poner soldados de prueba en Pueblo (tarea 13)")]
        public static void PlaceTestSoldiers()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[{Tag}] Primero ejecuta The Last Beat > Enemigos > 1. Crear prefab del soldado.");
                return;
            }

            GameObject root = SetupUtils.GetOrCreateRoot(TestRootName);
            for (int i = root.transform.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

            int placed = 0;
            for (int i = 0; i < TestPositions.Length; i++)
            {
                if (!NavMesh.SamplePosition(TestPositions[i], out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"[{Tag}] No hay NavMesh cerca de {TestPositions[i]}; ese soldado no se puso.");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Soldado de prueba");
                instance.transform.SetParent(root.transform, true);
                instance.transform.SetPositionAndRotation(hit.position, Quaternion.LookRotation(Vector3.back));
                instance.name = "Soldado_Prueba_" + (i + 1);
                placed++;
            }

            SetupUtils.SaveScene();
            Debug.Log($"[{Tag}] {placed} soldados de prueba en la plaza ({TestRootName}). Dale Play: van por el caballero, o por la bardo si la tienen cerca.");
        }

        private static GameObject BuildSoldier()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            Material body = SetupUtils.CreateMaterial(BodyMaterialPath, new Color(0.55f, 0.08f, 0.1f), false);
            Material detail = SetupUtils.CreateMaterial(DetailMaterialPath, new Color(0.08f, 0.07f, 0.09f), false);
            Material warning = SetupUtils.CreateMaterial(WarningMaterialPath, new Color(0.95f, 0.12f, 0.08f), true);

            var root = new GameObject("Soldado");
            root.layer = enemyLayer;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.radius = 0.45f;
            collider.height = 2f;
            collider.isTrigger = false;

            // EnemySoldier exige NavMeshAgent y Health: Unity los añade solos.
            var soldier = root.AddComponent<EnemySoldier>();
            root.AddComponent<KnockbackReceiver>();

            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 2f;
            agent.baseOffset = 0f;
            agent.speed = 3.2f;
            agent.acceleration = 12f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 1.2f;
            agent.avoidancePriority = 50;

            var health = root.GetComponent<Health>();
            SetupUtils.SetFloat(health, "maxHealth", 60f);
            SetupUtils.SetInt(health, "team", (int)Team.Demons);

            Part(root, "Cuerpo", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1f, 0.9f), body, enemyLayer);
            Part(root, "Frente", PrimitiveType.Cube, new Vector3(0f, 1.5f, 0.42f), new Vector3(0.2f, 0.2f, 0.3f), detail, enemyLayer);
            Part(root, "Cuerno_I", PrimitiveType.Cylinder, new Vector3(-0.2f, 2.05f, 0.1f), new Vector3(0.12f, 0.18f, 0.12f), detail, enemyLayer);
            Part(root, "Cuerno_D", PrimitiveType.Cylinder, new Vector3(0.2f, 2.05f, 0.1f), new Vector3(0.12f, 0.18f, 0.12f), detail, enemyLayer);
            GameObject indicator = Part(root, "AvisoAtaque", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 1f), new Vector3(2.2f, 0.01f, 2.2f), warning, enemyLayer);
            indicator.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            indicator.SetActive(false);

            SetupUtils.Set(soldier, "attackIndicator", indicator);
            return root;
        }

        /// <summary>Una pieza visual sin collider (el único collider es la cápsula de la raíz).</summary>
        private static GameObject Part(GameObject parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, int layer)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.layer = layer;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
