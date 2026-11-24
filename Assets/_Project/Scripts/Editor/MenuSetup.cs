using TheLastBeat.Level;
using TheLastBeat.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Menú principal, victoria y derrota (tarea 35). Menú The Last Beat/Menús/Crear menú principal y pantallas de fin:
    ///  1. Crea Scenes/MainMenu.unity: cámara, EventSystem del Input System y MainMenuUI (Jugar y Salir).
    ///  2. En Pueblo pone "Pantallas_Fin" con EndScreenUI (victoria y derrota, con Reintentar y Menú).
    ///  3. Build Profiles: MainMenu primero y Pueblo segundo; quita SampleScene y cualquier otra escena de la build
    ///     (Test_Combo sigue en el proyecto para pruebas, solo no entra en el juego).
    /// Si MainMenu ya existe, no la vuelve a crear. Es repetible.
    /// </summary>
    public static class MenuSetup
    {
        private const string Tag = "MenuSetup";
        public const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string EndScreenName = "Pantallas_Fin";

        [MenuItem("The Last Beat/Menús/Crear menú principal y pantallas de fin (tarea 35)")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool created = false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null)
            {
                CreateMainMenu();
                created = true;
            }

            if (!SetupUtils.OpenPueblo()) return;
            LevelFlow flow = SetupUtils.FindInScene<LevelFlow>();
            if (flow == null)
            {
                Debug.LogError($"[{Tag}] Pueblo no tiene LevelFlow. Ejecuta antes The Last Beat > Nivel > 2. Montar el flujo del nivel.");
                return;
            }

            GameObject endScreens = SetupUtils.GetOrCreateRoot(EndScreenName);
            EndScreenUI screen = SetupUtils.GetOrAdd<EndScreenUI>(endScreens);
            SetupUtils.Set(screen, "flow", flow);
            SetupUtils.SaveScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(SetupUtils.PuebloPath, true),
            };

            Debug.Log($"[{Tag}] Listo: {(created ? "escena MainMenu creada" : "MainMenu ya existía")}, '{EndScreenName}' en Pueblo y " +
                      "Build Profiles con MainMenu (0) y Pueblo (1). Para probar todo, abre MainMenu y dale Play.");
        }

        private static void CreateMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.04f, 0.12f);
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var module = eventSystem.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            var menu = new GameObject("Menu");
            menu.AddComponent<MainMenuUI>();

            EditorSceneManager.SaveScene(scene, MainMenuPath);
            AssetDatabase.SaveAssets();
        }
    }
}
