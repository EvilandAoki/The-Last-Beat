using UnityEngine;

namespace TheLastBeat.Core
{
    /// <summary>
    /// Capas físicas del proyecto (tarea 3). Los nombres deben coincidir con
    /// Project Settings > Tags and Layers. Se resuelven por nombre, así que no
    /// importa en qué número de capa queden.
    /// </summary>
    public static class GameLayers
    {
        public const string GroundName = "Ground";
        public const string PlayerName = "Player";
        public const string AllyName = "Ally";
        public const string EnemyName = "Enemy";
        public const string ProjectileName = "Projectile";

        public static int Ground => LayerMask.NameToLayer(GroundName);
        public static int Player => LayerMask.NameToLayer(PlayerName);
        public static int Ally => LayerMask.NameToLayer(AllyName);
        public static int Enemy => LayerMask.NameToLayer(EnemyName);
        public static int Projectile => LayerMask.NameToLayer(ProjectileName);

        /// <summary>Lo que golpean los ataques de los héroes.</summary>
        public static LayerMask EnemyMask => LayerMask.GetMask(EnemyName);

        /// <summary>Lo que golpean los ataques de los demonios: el jugador y sus compañeros.</summary>
        public static LayerMask HeroesMask => LayerMask.GetMask(PlayerName, AllyName);

        /// <summary>Lo que bloquea proyectiles, cargas y líneas de visión.</summary>
        public static LayerMask ObstacleMask => LayerMask.GetMask(GroundName);
    }
}
