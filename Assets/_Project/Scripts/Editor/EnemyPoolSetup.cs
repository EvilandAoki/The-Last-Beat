using TheLastBeat.DebugTools;
using TheLastBeat.Enemies;
using UnityEditor;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Pool de enemigos (tarea 14). Menú The Last Beat/Enemigos/3: en Pueblo quita los soldados de prueba de la tarea 13
    /// y crea el objeto "Enemigos" con el EnemyPool (30 soldados creados al cargar la escena) y la prueba de 30 enemigos
    /// (EnemyStressTest: F5 en Play). Es repetible.
    /// </summary>
    public static class EnemyPoolSetup
    {
        private const string Tag = "EnemyPoolSetup";
        public const string RootName = "Enemigos";

        [MenuItem("The Last Beat/Enemigos/3. Montar el pool en Pueblo (tarea 14)")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemySetup.PrefabPath);
            EnemySoldier soldier = prefab != null ? prefab.GetComponent<EnemySoldier>() : null;
            if (soldier == null)
            {
                Debug.LogError($"[{Tag}] Primero ejecuta The Last Beat > Enemigos > 1. Crear prefab del soldado.");
                return;
            }

            // Los soldados sueltos de la tarea 13 ya no hacen falta: desde ahora salen del pool.
            GameObject tests = SetupUtils.FindRoot(EnemySetup.TestRootName);
            if (tests != null) Undo.DestroyObjectImmediate(tests);

            GameObject root = SetupUtils.GetOrCreateRoot(RootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            EnemyPool pool = SetupUtils.GetOrAdd<EnemyPool>(root);
            SetupUtils.Set(pool, "prefab", soldier);
            SetupUtils.SetInt(pool, "prewarm", 30);
            SetupUtils.SetInt(pool, "maxSize", 60);

            EnemyStressTest test = SetupUtils.GetOrAdd<EnemyStressTest>(root);
            SetupUtils.Set(test, "pool", pool);

            SetupUtils.SaveScene();
            Debug.Log($"[{Tag}] Pool listo en '{RootName}' (30 soldados de antemano). En Play, F5 saca 30 a la vez y abajo se ven los FPS.");
        }
    }
}
