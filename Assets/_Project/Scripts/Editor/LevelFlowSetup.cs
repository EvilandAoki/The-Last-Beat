using TheLastBeat.Companions;
using TheLastBeat.Level;
using TheLastBeat.Player;
using TheLastBeat.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Flujo del nivel (tarea 20). Menú The Last Beat/Nivel/2. En Pueblo:
    ///  - Objeto "Nivel" con LevelFlow enlazado al generador de hordas, al caballero y a la bardo.
    ///  - "Trigger_Plaza" (hijo de Nivel): zona invisible en la entrada de la plaza que llama a LevelFlow.StartCombat.
    ///    Va en (0, 1.5, 2) y mide 10 x 3 x 2: cubre la calle entre las últimas casas.
    ///  - "Mensajes_Nivel" con LevelBannerUI: el mensaje del centro y el contador de hordas.
    ///  - El generador de hordas deja de arrancar solo y la canción de la bardo empieza apagada: el pueblo llega en
    ///    silencio y la bardo empieza a cantar al entrar a la plaza.
    ///  - Pone Pueblo en Build Profiles, para que Reintentar pueda recargarla.
    /// Es repetible.
    /// </summary>
    public static class LevelFlowSetup
    {
        private const string Tag = "LevelFlowSetup";
        public const string RootName = "Nivel";
        private const string TriggerName = "Trigger_Plaza";
        private const string BannerName = "Mensajes_Nivel";
        private static readonly Vector3 TriggerPosition = new Vector3(0f, 1.5f, 2f);
        private static readonly Vector3 TriggerSize = new Vector3(10f, 3f, 2f);

        [MenuItem("The Last Beat/Nivel/2. Montar el flujo del nivel en Pueblo (tarea 20)")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            WaveSpawner spawner = SetupUtils.FindInScene<WaveSpawner>();
            PlayerController knight = SetupUtils.FindInScene<PlayerController>();
            BardCompanion bard = SetupUtils.FindInScene<BardCompanion>();
            if (spawner == null || knight == null || bard == null)
            {
                Debug.LogError($"[{Tag}] Faltan piezas en Pueblo: " + (spawner == null ? "WaveSpawner (Nivel > 1) " : "") +
                               (knight == null ? "caballero (Pueblo > 1) " : "") + (bard == null ? "bardo (Pueblo > 1)" : ""));
                return;
            }

            // LevelFlow.
            GameObject root = SetupUtils.GetOrCreateRoot(RootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            LevelFlow flow = SetupUtils.GetOrAdd<LevelFlow>(root);
            SetupUtils.Set(flow, "spawner", spawner);
            SetupUtils.Set(flow, "knight", knight);
            SetupUtils.Set(flow, "bard", bard);

            // Zona de la plaza: al entrar el caballero, empieza el combate.
            GameObject triggerObject = SetupUtils.GetOrCreateChild(root, TriggerName);
            triggerObject.transform.position = TriggerPosition;
            triggerObject.transform.rotation = Quaternion.identity;
            BoxCollider box = SetupUtils.GetOrAdd<BoxCollider>(triggerObject);
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = TriggerSize;
            ZoneTrigger trigger = SetupUtils.GetOrAdd<ZoneTrigger>(triggerObject);
            for (int i = trigger.OnPlayerEnter.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(trigger.OnPlayerEnter, i);
            UnityEventTools.AddPersistentListener(trigger.OnPlayerEnter, flow.StartCombat);
            EditorUtility.SetDirty(trigger);

            // Mensajes y contador de hordas.
            GameObject bannerObject = SetupUtils.GetOrCreateRoot(BannerName);
            LevelBannerUI banner = SetupUtils.GetOrAdd<LevelBannerUI>(bannerObject);
            SetupUtils.Set(banner, "flow", flow);

            // Las hordas las lanza el combate y la bardo no canta hasta entonces (LevelFlow también lo asegura al empezar).
            SetupUtils.SetBool(spawner, "autoStart", false);
            SongBuff song = bard.GetComponent<SongBuff>();
            if (song != null)
            {
                Undo.RecordObject(song, "Canción apagada hasta el combate");
                song.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(song);
            }

            SetupUtils.EnsureInBuild(SetupUtils.PuebloPath);
            SetupUtils.SaveScene();
            Debug.Log($"[{Tag}] Flujo del nivel listo: '{RootName}' con LevelFlow y {TriggerName}, '{BannerName}' para los mensajes. " +
                      "Dale Play: el pueblo empieza en silencio y las hordas salen al entrar a la plaza.");
        }
    }
}
