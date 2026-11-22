using System;
using UnityEngine;

namespace TheLastBeat.VFX
{
    /// <summary>Tipos de efecto del juego.</summary>
    public enum VfxKind
    {
        Impact,       // chispa azul al golpear
        ImpactHeavy,  // golpe fuerte: más chispas y una onda
        Spin,         // anillo que gira alrededor del caballero durante el giro
        Explosion,    // explosión final del giro
        Charge,       // estela de la embestida
        SongAura,     // aura morada del radio de la canción
        SongBreak,    // la canción se rompe
    }

    /// <summary>
    /// Los prefabs de efectos y cuántos se crean de antemano (tarea 33). Vive en Resources para que Vfx lo encuentre sin
    /// asignarlo en ninguna escena. Para cambiar un efecto basta con editar su prefab en VFX/Prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "The Last Beat/Biblioteca de efectos", fileName = "VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public PooledEffect prefab;
            [Tooltip("Cuántos se crean de antemano para no instanciar durante el combate.")]
            [Min(1)] public int prewarm = 4;
        }

        public Entry impact = new Entry { prewarm = 12 };
        public Entry impactHeavy = new Entry { prewarm = 6 };
        public Entry spin = new Entry { prewarm = 2 };
        public Entry explosion = new Entry { prewarm = 3 };
        public Entry charge = new Entry { prewarm = 2 };
        public Entry songAura = new Entry { prewarm = 2 };
        public Entry songBreak = new Entry { prewarm = 3 };

        public Entry Get(VfxKind kind)
        {
            switch (kind)
            {
                case VfxKind.Impact: return impact;
                case VfxKind.ImpactHeavy: return impactHeavy;
                case VfxKind.Spin: return spin;
                case VfxKind.Explosion: return explosion;
                case VfxKind.Charge: return charge;
                case VfxKind.SongAura: return songAura;
                default: return songBreak;
            }
        }
    }
}
