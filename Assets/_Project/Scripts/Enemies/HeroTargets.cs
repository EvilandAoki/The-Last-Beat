using TheLastBeat.Combat;
using TheLastBeat.Companions;
using TheLastBeat.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastBeat.Enemies
{
    /// <summary>
    /// A quién atacan los demonios (tarea 13): el caballero (el Health del PlayerController) y la bardo (el Health del
    /// BardCompanion). Los busca en la escena y los recuerda, así no hay que añadir nada a los prefabs de los héroes.
    /// La bardo tiene prioridad: si está cerca de un demonio, ese demonio va por ella aunque el caballero esté más cerca.
    /// Si un héroe muere, deja de ser objetivo. Al cargar otra escena se vuelven a buscar.
    /// </summary>
    public static class HeroTargets
    {
        /// <summary>Si falta algún héroe, no lo vuelve a buscar antes de este tiempo (Find recorre toda la escena).</summary>
        private const float SearchInterval = 1f;

        private static Health knight;
        private static Health bard;
        private static float nextSearch;

        public static Health Knight
        {
            get
            {
                Refresh();
                return knight;
            }
        }

        public static Health Bard
        {
            get
            {
                Refresh();
                return bard;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            knight = null;
            bard = null;
            nextSearch = 0f;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => nextSearch = 0f;

        /// <summary>
        /// El mejor objetivo para un demonio en <paramref name="from"/>: la bardo si está viva y a menos de
        /// <paramref name="bardPriorityRadius"/>; si no, el héroe vivo más cercano. Null si no queda ninguno.
        /// </summary>
        public static Health FindBest(Vector3 from, float bardPriorityRadius)
        {
            Refresh();
            bool knightAlive = IsAlive(knight);
            bool bardAlive = IsAlive(bard);

            if (bardAlive && FlatDistance(from, bard.transform.position) <= bardPriorityRadius) return bard;
            if (knightAlive && bardAlive)
                return FlatDistance(from, knight.transform.position) <= FlatDistance(from, bard.transform.position) ? knight : bard;
            if (knightAlive) return knight;
            return bardAlive ? bard : null;
        }

        /// <summary>Distancia horizontal al héroe vivo más cercano (las hordas no hacen aparecer demonios encima de él).</summary>
        public static float NearestDistance(Vector3 from)
        {
            Refresh();
            float best = float.MaxValue;
            if (IsAlive(knight)) best = Mathf.Min(best, FlatDistance(from, knight.transform.position));
            if (IsAlive(bard)) best = Mathf.Min(best, FlatDistance(from, bard.transform.position));
            return best;
        }

        public static bool IsAlive(Health hero) => hero != null && !hero.IsDead && hero.isActiveAndEnabled;

        private static void Refresh()
        {
            // "== null" de Unity también es true para un objeto destruido (por ejemplo, tras recargar la escena).
            if (knight != null && bard != null) return;
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + SearchInterval;

            if (knight == null)
            {
                PlayerController player = Object.FindFirstObjectByType<PlayerController>();
                knight = player != null ? player.GetComponent<Health>() : null;
            }

            if (bard == null)
            {
                BardCompanion companion = Object.FindFirstObjectByType<BardCompanion>();
                bard = companion != null ? companion.GetComponent<Health>() : null;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
