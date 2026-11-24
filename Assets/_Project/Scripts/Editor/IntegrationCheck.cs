using System.Text;
using TheLastBeat.Audio;
using TheLastBeat.Companions;
using TheLastBeat.DebugTools;
using TheLastBeat.Enemies;
using TheLastBeat.Level;
using TheLastBeat.Player;
using TheLastBeat.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Revisión final de Pueblo (tarea 36). Menú The Last Beat/Pueblo/2:
    ///  - Quita la prueba de 30 enemigos (EnemyStressTest, tarea 14): es una herramienta de desarrollo.
    ///  - Comprueba que estén todas las piezas de la primera entrega y bien enlazadas: héroes, HUD, pool, hordas, flujo
    ///    del nivel, mensajes, pantallas de fin, audio y Build Profiles. Muestra la lista con lo que está bien (OK) y lo
    ///    que falta (FALTA), en una ventana y en la consola.
    /// No crea nada: si algo falta, dice qué menú lo monta. Es repetible.
    /// </summary>
    public static class IntegrationCheck
    {
        private const string Tag = "IntegrationCheck";

        [MenuItem("The Last Beat/Pueblo/2. Revisión final de la escena (tarea 36)")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            int removed = 0;
            foreach (EnemyStressTest test in SetupUtils.FindAllInScene<EnemyStressTest>())
            {
                Undo.DestroyObjectImmediate(test);
                removed++;
            }

            var report = new StringBuilder();
            int missing = 0;

            // Héroes y HUD (Pueblo > 1).
            PlayerController knight = SetupUtils.FindInScene<PlayerController>();
            missing += Check(report, knight != null && PrefabUtility.GetCorrespondingObjectFromSource(knight.gameObject) != null,
                "Caballero del prefab Prefabs/Heroes/Knight", "The Last Beat > Pueblo > 1");
            BardCompanion bard = SetupUtils.FindInScene<BardCompanion>();
            bool bardOnNavMesh = bard != null && NavMesh.SamplePosition(bard.transform.position, out NavMeshHit hit, 0.5f, NavMesh.AllAreas);
            missing += Check(report, bardOnNavMesh, "Bardo sobre el NavMesh", "The Last Beat > Pueblo > 1 (o muévela sobre el NavMesh)");
            missing += Check(report, SetupUtils.FindInScene<GameHud>() != null, "HUD (tarea 34)", "The Last Beat > Pueblo > 1");

            // Enemigos y hordas.
            EnemyPool pool = SetupUtils.FindInScene<EnemyPool>();
            missing += Check(report, pool != null && HasReference(pool, "prefab"), "Pool de enemigos con el prefab del soldado", "The Last Beat > Enemigos > 3");
            missing += Check(report, SetupUtils.FindRoot(EnemySetup.TestRootName) == null, "Sin soldados de prueba sueltos", "The Last Beat > Enemigos > 3");
            WaveSpawner spawner = SetupUtils.FindInScene<WaveSpawner>();
            missing += Check(report, spawner != null && HasReference(spawner, "data") && HasReference(spawner, "pool") && HasReference(spawner, "spawnPointsRoot"),
                "Generador de hordas con WaveData, pool y puntos de aparición", "The Last Beat > Nivel > 1");
            missing += Check(report, spawner != null && !spawner.AutoStart, "Las hordas esperan al combate (Auto Start apagado)", "The Last Beat > Nivel > 2");

            // Flujo del nivel.
            LevelFlow flow = SetupUtils.FindInScene<LevelFlow>();
            missing += Check(report, flow != null && HasReference(flow, "spawner") && HasReference(flow, "knight") && HasReference(flow, "bard"),
                "LevelFlow enlazado a las hordas, el caballero y la bardo", "The Last Beat > Nivel > 2");
            ZoneTrigger trigger = SetupUtils.FindInScene<ZoneTrigger>();
            bool triggerOk = trigger != null && trigger.GetComponent<Collider>() != null && trigger.GetComponent<Collider>().isTrigger &&
                             trigger.OnPlayerEnter.GetPersistentEventCount() > 0 &&
                             trigger.OnPlayerEnter.GetPersistentMethodName(0) == nameof(LevelFlow.StartCombat);
            missing += Check(report, triggerOk, "Trigger de la plaza que llama a LevelFlow.StartCombat", "The Last Beat > Nivel > 2");
            SongBuff song = bard != null ? bard.GetComponent<SongBuff>() : null;
            missing += Check(report, song != null && !song.enabled, "Canción apagada hasta el combate (pueblo en silencio)", "The Last Beat > Nivel > 2");
            missing += Check(report, SetupUtils.FindInScene<LevelBannerUI>() != null, "Mensajes y contador de hordas", "The Last Beat > Nivel > 2");

            // Audio.
            missing += Check(report, SetupUtils.FindInScene<SfxPlayer>() != null && SetupUtils.FindInScene<GameSfx>() != null,
                "Efectos de sonido (SfxPlayer y GameSfx)", "The Last Beat > Audio > Montar el audio en Pueblo");
            missing += Check(report, SetupUtils.FindInScene<MusicLayerController>() != null && SetupUtils.FindInScene<AmbiencePlayer>() != null,
                "Música por capas y ambiente del pueblo", "The Last Beat > Audio > Montar el audio en Pueblo");
            missing += Check(report, SetupUtils.FindInScene<AudioListener>() != null, "AudioListener (en la cámara)", "añade un AudioListener a Main Camera");

            // Menús y build.
            missing += Check(report, SetupUtils.FindInScene<EndScreenUI>() != null, "Pantallas de victoria y derrota", "The Last Beat > Menús > Crear menú principal");
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool buildOk = scenes.Length >= 2 && scenes[0].path == MenuSetup.MainMenuPath && scenes[0].enabled &&
                           scenes[1].path == SetupUtils.PuebloPath && scenes[1].enabled;
            missing += Check(report, buildOk, "Build Profiles: MainMenu (0) y Pueblo (1)", "The Last Beat > Menús > Crear menú principal");

            SetupUtils.SaveScene();

            string header = (removed > 0 ? $"Quité la prueba de 30 enemigos ({removed}).\n\n" : string.Empty) +
                            (missing == 0 ? "Todo listo para la prueba completa." : $"Faltan {missing} piezas:") + "\n\n";
            string text = header + report;
            if (missing == 0) Debug.Log($"[{Tag}] {text}");
            else Debug.LogWarning($"[{Tag}] {text}");
            EditorUtility.DisplayDialog("Revisión final de Pueblo (tarea 36)", text, "Cerrar");
        }

        private static int Check(StringBuilder report, bool ok, string what, string fix)
        {
            report.AppendLine(ok ? $"OK      {what}" : $"FALTA   {what}  →  {fix}");
            return ok ? 0 : 1;
        }

        private static bool HasReference(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            return property != null && property.objectReferenceValue != null;
        }
    }
}
