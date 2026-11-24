using TheLastBeat.Enemies;
using TheLastBeat.Level;
using UnityEditor;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Generador de hordas (tarea 15). Menú The Last Beat/Nivel/1:
    ///  - Crea ScriptableObjects/Waves/Hordas_Pueblo.asset con 3 hordas (5, 8 y 12 soldados). Si ya existe, no lo toca:
    ///    las hordas se ajustan en ese asset, sin código.
    ///  - En Pueblo crea el objeto "Hordas" con el WaveSpawner: el asset, el pool de la tarea 14 y como puntos de
    ///    aparición los hijos de "Spawns" (los que dejó el blockout, tarea 5). Arranca solo al dar Play para poder
    ///    probarlo; el flujo del nivel (tarea 20) lo apaga y lo lanza al entrar a la plaza.
    /// Es repetible.
    /// </summary>
    public static class WaveSetup
    {
        private const string Tag = "WaveSetup";
        public const string WavesPath = "Assets/_Project/ScriptableObjects/Waves/Hordas_Pueblo.asset";
        public const string RootName = "Hordas";
        private const string SpawnsName = "Spawns";

        [MenuItem("The Last Beat/Nivel/1. Crear las hordas en Pueblo (tarea 15)")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            EnemyPool pool = SetupUtils.FindInScene<EnemyPool>();
            if (pool == null)
            {
                Debug.LogError($"[{Tag}] Pueblo no tiene EnemyPool. Ejecuta antes The Last Beat > Enemigos > 3. Montar el pool en Pueblo.");
                return;
            }

            GameObject spawns = SetupUtils.FindRoot(SpawnsName);
            if (spawns == null || spawns.transform.childCount == 0)
            {
                Debug.LogError($"[{Tag}] Pueblo no tiene el objeto '{SpawnsName}' con puntos de aparición como hijos.");
                return;
            }

            var data = AssetDatabase.LoadAssetAtPath<WaveData>(WavesPath);
            if (data == null)
            {
                SetupUtils.EnsureFolder("Assets/_Project/ScriptableObjects/Waves");
                data = ScriptableObject.CreateInstance<WaveData>(); // trae las 3 hordas por defecto
                AssetDatabase.CreateAsset(data, WavesPath);
                AssetDatabase.SaveAssets();
            }

            GameObject root = SetupUtils.GetOrCreateRoot(RootName);
            WaveSpawner spawner = SetupUtils.GetOrAdd<WaveSpawner>(root);
            SetupUtils.Set(spawner, "data", data);
            SetupUtils.Set(spawner, "pool", pool);
            SetupUtils.Set(spawner, "spawnPointsRoot", spawns.transform);
            SetupUtils.SetFloat(spawner, "startDelay", 2f);

            SetupUtils.SaveScene();
            Debug.Log($"[{Tag}] Hordas listas: {data.waves.Length} hordas en {WavesPath} y {spawns.transform.childCount} puntos de aparición. " +
                      "Dale Play: empiezan solas a los 2 segundos.");
        }
    }
}
