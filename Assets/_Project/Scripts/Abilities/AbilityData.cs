using System;
using System.Collections;
using UnityEngine;

namespace TheLastBeat.Abilities
{
    /// <summary>
    /// Una mejora de habilidad. Cada mejora multiplica los valores base; las mejoras se acumulan por nivel.
    /// </summary>
    [Serializable]
    public class AbilityUpgrade
    {
        public string title = "Mejora";
        [TextArea] public string description;
        [Min(0f)] public float damageMultiplier = 1f;
        [Min(0f)] public float cooldownMultiplier = 1f;
        [Min(0f)] public float radiusMultiplier = 1f;
    }

    /// <summary>
    /// Datos de una habilidad del héroe (tarea 9). Es un ScriptableObject: daño, recarga, radio y mejoras se
    /// cambian en el Inspector sin tocar código. Cada habilidad concreta hereda de esta clase e implementa Cast
    /// (por ejemplo el giro con explosión final o la embestida).
    ///
    /// Nivel 1 = valores base. El nivel N aplica las mejoras 1 a N-1, hasta un máximo de 8 mejoras (nivel 9).
    /// </summary>
    public abstract class AbilityData : ScriptableObject
    {
        public const int MaxUpgrades = 8;

        [Header("Presentación")]
        public string displayName = "Habilidad";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Valores base (nivel 1)")]
        [Min(0f)] public float cooldown = 6f;
        [Min(0f)] public float damage = 20f;
        [Min(0f)] public float radius = 3f;
        [Min(0f)] public float knockback = 5f;

        [Header("Animación (opcional)")]
        [Tooltip("Estado del Animator que se reproduce al lanzarla. Vacío = ninguno.")]
        public string animatorState;

        [Header("Mejoras (hasta 8)")]
        public AbilityUpgrade[] upgrades = new AbilityUpgrade[0];

        /// <summary>Nivel máximo: nivel 1 más una por cada mejora definida.</summary>
        public int MaxLevel => 1 + (upgrades != null ? upgrades.Length : 0);

        public float GetCooldown(int level) => cooldown * Product(level, u => u.cooldownMultiplier);
        public float GetDamage(int level) => damage * Product(level, u => u.damageMultiplier);
        public float GetRadius(int level) => radius * Product(level, u => u.radiusMultiplier);

        /// <summary>Lo que hace la habilidad. El AbilityCaster ya inició la recarga; esta rutina dura mientras se lanza.</summary>
        public abstract IEnumerator Cast(AbilityContext context);

        private float Product(int level, Func<AbilityUpgrade, float> selector)
        {
            if (upgrades == null) return 1f;
            int applied = Mathf.Clamp(level - 1, 0, upgrades.Length);
            float result = 1f;
            for (int i = 0; i < applied; i++) result *= selector(upgrades[i]);
            return result;
        }

        protected virtual void OnValidate()
        {
            if (upgrades != null && upgrades.Length > MaxUpgrades) Array.Resize(ref upgrades, MaxUpgrades);
        }
    }
}
