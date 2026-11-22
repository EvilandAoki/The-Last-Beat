using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TheLastBeat.VFX
{
    /// <summary>
    /// Punto único para lanzar efectos (tarea 33). Cada tipo tiene su ObjectPool: los efectos se crean una vez
    /// (Prewarm) y se reutilizan, así que no hay Instantiate ni Destroy durante el combate.
    ///  - Play: efecto de una sola vez en un punto (impactos, explosión).
    ///  - Follow: efecto que acompaña a un objeto hasta que se llama a Stop (giro, estela, aura).
    /// Si no hay biblioteca (Resources/VfxLibrary) avisa una vez y no hace nada, así que el juego funciona sin efectos.
    /// </summary>
    public static class Vfx
    {
        private static VfxLibrary library;
        private static bool libraryLoaded;
        private static Transform root;
        private static bool prewarmed;
        private static readonly Dictionary<VfxKind, ObjectPool<PooledEffect>> pools = new Dictionary<VfxKind, ObjectPool<PooledEffect>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            library = null;
            libraryLoaded = false;
            root = null;
            prewarmed = false;
            pools.Clear();
        }

        /// <summary>Crea de antemano los efectos de la biblioteca. Se llama una vez al aparecer el héroe.</summary>
        public static void Prewarm()
        {
            if (prewarmed) return;
            prewarmed = true;

            var created = new List<PooledEffect>();
            foreach (VfxKind kind in System.Enum.GetValues(typeof(VfxKind)))
            {
                ObjectPool<PooledEffect> pool = Pool(kind);
                VfxLibrary.Entry entry = Entry(kind);
                if (pool == null || entry == null) continue;
                for (int i = 0; i < entry.prewarm; i++) created.Add(pool.Get());
                foreach (PooledEffect effect in created) pool.Release(effect);
                created.Clear();
            }
        }

        public static void Play(VfxKind kind, Vector3 position, float scale = 1f) => Play(kind, position, Quaternion.identity, scale);

        public static void Play(VfxKind kind, Vector3 position, Quaternion rotation, float scale)
        {
            PooledEffect effect = Spawn(kind);
            if (effect == null) return;

            effect.transform.SetPositionAndRotation(position, rotation);
            effect.transform.localScale = Vector3.one * scale;
            effect.Play();
        }

        /// <summary>
        /// Efecto que acompaña a un objeto. scale es el tamaño en el mundo (se compensa la escala del padre). Devuelve el
        /// efecto para detenerlo con Stop; null si no hay biblioteca.
        /// </summary>
        public static PooledEffect Follow(VfxKind kind, Transform parent, Vector3 localOffset, float scale = 1f)
        {
            PooledEffect effect = Spawn(kind);
            if (effect == null) return null;

            Transform t = effect.transform;
            t.SetParent(parent, false);
            t.localPosition = localOffset;
            t.localRotation = Quaternion.identity;
            Vector3 parentScale = parent.lossyScale;
            t.localScale = new Vector3(Safe(scale, parentScale.x), Safe(scale, parentScale.y), Safe(scale, parentScale.z));
            effect.Play();
            return effect;
        }

        /// <summary>Detiene un efecto de Follow y deja la referencia en null. El efecto vuelve al pool cuando mueren sus partículas.</summary>
        public static void Stop(ref PooledEffect effect)
        {
            if (effect == null) return;
            effect.Stop();
            effect = null;
        }

        private static float Safe(float scale, float parentScale) => Mathf.Abs(parentScale) > 0.0001f ? scale / parentScale : scale;

        // ---------------------------------------------------------------- Pools

        private static PooledEffect Spawn(VfxKind kind)
        {
            ObjectPool<PooledEffect> pool = Pool(kind);
            if (pool == null) return null;

            // Un efecto puede haberse destruido con su padre (por ejemplo una bardo eliminada): se descarta y se pide otro.
            PooledEffect effect;
            do { effect = pool.Get(); } while (effect == null);
            return effect;
        }

        private static VfxLibrary Library()
        {
            if (libraryLoaded) return library;
            libraryLoaded = true;
            library = Resources.Load<VfxLibrary>("VfxLibrary");
            if (library == null)
                Debug.LogWarning("[Vfx] No hay biblioteca de efectos (Resources/VfxLibrary). Ejecuta The Last Beat > VFX > Crear efectos (tarea 33). El juego sigue sin efectos.");
            return library;
        }

        private static VfxLibrary.Entry Entry(VfxKind kind)
        {
            VfxLibrary lib = Library();
            if (lib == null) return null;
            VfxLibrary.Entry entry = lib.Get(kind);
            return entry != null && entry.prefab != null ? entry : null;
        }

        private static Transform Root()
        {
            if (root != null) return root;
            var go = new GameObject("VfxPool");
            Object.DontDestroyOnLoad(go);
            root = go.transform;
            return root;
        }

        private static ObjectPool<PooledEffect> Pool(VfxKind kind)
        {
            if (pools.TryGetValue(kind, out ObjectPool<PooledEffect> existing)) return existing;

            VfxLibrary.Entry entry = Entry(kind);
            if (entry == null) return null;

            ObjectPool<PooledEffect> pool = null;
            pool = new ObjectPool<PooledEffect>(
                createFunc: () =>
                {
                    PooledEffect effect = Object.Instantiate(entry.prefab, Root());
                    effect.Release = pool.Release;
                    effect.gameObject.SetActive(false);
                    return effect;
                },
                actionOnGet: effect => effect.gameObject.SetActive(true),
                actionOnRelease: effect =>
                {
                    effect.transform.SetParent(Root(), false);
                    effect.gameObject.SetActive(false);
                },
                actionOnDestroy: effect =>
                {
                    if (effect != null) Object.Destroy(effect.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: entry.prewarm,
                maxSize: entry.prewarm * 4);
            pools[kind] = pool;
            return pool;
        }
    }
}
