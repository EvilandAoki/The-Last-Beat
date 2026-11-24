using TheLastBeat.Core;
using TheLastBeat.Enemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace TheLastBeat.DebugTools
{
    /// <summary>
    /// Prueba de la tarea 14: con F5 saca 30 soldados del pool alrededor de la plaza y muestra en pantalla los FPS,
    /// los enemigos activos y cuántos se han creado. Si "creados" no sube al repetir la prueba, el pool reutiliza.
    /// Solo funciona en el editor y en las builds de desarrollo; la revisión final (tarea 36) lo quita de Pueblo.
    /// </summary>
    public class EnemyStressTest : MonoBehaviour
    {
        [SerializeField] private EnemyPool pool;
        [SerializeField] private Key key = Key.F5;
        [SerializeField, Min(1)] private int count = 30;
        [Tooltip("Centro del círculo donde aparecen (la plaza de Pueblo).")]
        [SerializeField] private Vector3 center = new Vector3(0f, 0f, 18f);
        [SerializeField, Min(1f)] private float radius = 8f;

        private float smoothedDelta = 1f / 60f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            if (pool != null && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame) SpawnBatch();
        }

        private void SpawnBatch()
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                pool.Spawn(hit.position, Quaternion.LookRotation(Vector3.back));
            }
        }

        private void OnGUI()
        {
            if (pool == null) return;
            string text = $"{key}: {count} soldados   |   Activos: {pool.ActiveCount}   Creados: {pool.CreatedCount}   |   {1f / Mathf.Max(0.0001f, smoothedDelta):0} FPS";
            GuiText.DrawOutlined(new Rect(16f, Screen.height - 48f, Screen.width - 32f, 32f), text, 18, Color.white, Color.black, 2f, TextAnchor.MiddleLeft);
        }
#endif
    }
}
