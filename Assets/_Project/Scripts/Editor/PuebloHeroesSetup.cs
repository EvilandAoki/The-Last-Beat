using TheLastBeat.Companions;
using TheLastBeat.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Pone en Pueblo lo que la línea A dejó listo, tal como dice docs/INTEGRACION.md (sección 3):
    ///  1. Cambia el Knight provisional de la escena por el prefab del caballero, con el menú de Sebastian
    ///     (KnightPrefabTool), que conserva la posición y reenlaza la cámara de Cinemachine.
    ///  2. Pone la bardo (Prefabs/Heroes/Bardo.prefab) sobre el NavMesh, junto al caballero.
    ///  3. Pone el HUD (Prefabs/UI/Hud.prefab): encuentra solo al caballero y a la bardo.
    /// Pueblo es de la línea B. Menú: The Last Beat/Pueblo/1. Es repetible: no duplica nada.
    /// </summary>
    public static class PuebloHeroesSetup
    {
        private const string Tag = "PuebloHeroesSetup";
        private const string BardPrefabPath = "Assets/_Project/Prefabs/Heroes/Bardo.prefab";
        private const string HudPrefabPath = "Assets/_Project/Prefabs/UI/Hud.prefab";
        private static readonly Vector3 BardOffset = new Vector3(-1.5f, 0f, -1.5f);

        [MenuItem("The Last Beat/Pueblo/1. Poner caballero, bardo y HUD")]
        public static void Run()
        {
            if (!SetupUtils.NotPlaying(Tag) || !SetupUtils.OpenPueblo()) return;

            if (GameObject.Find("Knight") == null)
            {
                Debug.LogError($"[{Tag}] Pueblo no tiene un objeto 'Knight'.");
                return;
            }

            // 1. El caballero: el menú de Sebastian avisa solo si ya es el prefab.
            KnightPrefabTool.ReplaceWithPrefab();
            GameObject knight = GameObject.Find("Knight");
            if (knight == null || PrefabUtility.GetCorrespondingObjectFromSource(knight) == null)
            {
                Debug.LogError($"[{Tag}] No se pudo poner el prefab del caballero. Mira el error de KnightPrefabTool en la consola.");
                return;
            }

            // 2. La bardo.
            string bardNote = "ya estaba";
            if (SetupUtils.FindInScene<BardCompanion>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BardPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[{Tag}] No existe {BardPrefabPath}.");
                    return;
                }

                var bard = (GameObject)PrefabUtility.InstantiatePrefab(prefab, knight.scene);
                Undo.RegisterCreatedObjectUndo(bard, "Poner la bardo");
                bard.name = "Bardo";
                Vector3 position = knight.transform.position + BardOffset;
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) position = hit.position;
                else Debug.LogWarning($"[{Tag}] No encontré NavMesh junto al caballero: revisa que la bardo quede sobre el suelo azul del NavMesh.");
                bard.transform.SetPositionAndRotation(position, knight.transform.rotation);
                bard.transform.SetSiblingIndex(knight.transform.GetSiblingIndex() + 1);
                bardNote = "puesta en " + position;
            }

            // 3. El HUD.
            string hudNote = "ya estaba";
            if (SetupUtils.FindInScene<GameHud>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[{Tag}] No existe {HudPrefabPath}.");
                    return;
                }

                var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, knight.scene);
                Undo.RegisterCreatedObjectUndo(hud, "Poner el HUD");
                hud.name = "HUD";
                hudNote = "puesto";
            }

            SetupUtils.SaveScene();
            Debug.Log($"[{Tag}] Listo en Pueblo: caballero del prefab, bardo {bardNote}, HUD {hudNote}.");
        }
    }
}
