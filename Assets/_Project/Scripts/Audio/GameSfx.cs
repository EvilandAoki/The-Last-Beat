using System.Collections;
using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using TheLastBeat.Companions;
using TheLastBeat.Enemies;
using TheLastBeat.Player;
using UnityEngine;

namespace TheLastBeat.Audio
{
    /// <summary>
    /// Conecta los efectos de sonido de la tarea 31 con los eventos del juego (tarea 32), según el mapa de
    /// Tools/Audio/README.md y sin tocar el código de los héroes:
    ///  - Golpe del combo al empezar (PlayerCombat.StepStarted): los pasos 0 y 1 son débiles y los demás, fuertes.
    ///  - Impacto en un demonio, venga del combo o de una habilidad (EnemySoldier.AnyDamaged), y su muerte (AnyDied).
    ///  - Giro, su explosión al terminar y Embestida (AbilityCaster.Cast). La explosión no tiene evento propio: suena
    ///    spinDuration + explosionDelay segundos después del Giro, con los valores del asset de la habilidad.
    ///  - Daño al caballero (Health.Damaged) y canción interrumpida (SongBuff.OnInterrupted, también con golpes repetidos).
    /// Todo pasa por SfxPlayer, con variantes y variación de tono. Va en el objeto Audio de la escena.
    /// </summary>
    public class GameSfx : MonoBehaviour
    {
        [Header("Héroes (vacío = se buscan en la escena)")]
        [SerializeField] private PlayerController knight;
        [SerializeField] private BardCompanion bard;
        [Tooltip("Primer paso del combo que suena como golpe fuerte (Heavy1).")]
        [SerializeField, Min(0)] private int firstHeavyStep = 2;

        [Header("Caballero")]
        [SerializeField] private SfxCue comboLight = new SfxCue();
        [SerializeField] private SfxCue comboHeavy = new SfxCue();
        [SerializeField] private SfxCue spin = new SfxCue();
        [SerializeField] private SfxCue spinExplosion = new SfxCue();
        [SerializeField] private SfxCue charge = new SfxCue();
        [SerializeField] private SfxCue knightHurt = new SfxCue();

        [Header("Demonios")]
        [SerializeField] private SfxCue impactLight = new SfxCue();
        [SerializeField] private SfxCue impactHeavy = new SfxCue();
        [SerializeField] private SfxCue soldierDeath = new SfxCue();

        [Header("Bardo")]
        [SerializeField] private SfxCue songInterrupted = new SfxCue();

        private PlayerCombat combat;
        private AbilityCaster caster;
        private Health knightHealth;
        private SongBuff song;
        private Coroutine pendingExplosion;

        private void Awake()
        {
            if (knight == null) knight = FindFirstObjectByType<PlayerController>();
            if (bard == null) bard = FindFirstObjectByType<BardCompanion>();
            if (knight != null)
            {
                combat = knight.GetComponent<PlayerCombat>();
                caster = knight.GetComponent<AbilityCaster>();
                knightHealth = knight.GetComponent<Health>();
            }

            if (bard != null) song = bard.GetComponent<SongBuff>();

            SfxCue[] all = { comboLight, comboHeavy, spin, spinExplosion, charge, knightHurt, impactLight, impactHeavy, soldierDeath, songInterrupted };
            for (int i = 0; i < all.Length; i++) all[i].Preload();
        }

        private void OnEnable()
        {
            if (combat != null) combat.StepStarted += HandleStep;
            if (caster != null) caster.Cast += HandleCast;
            if (knightHealth != null) knightHealth.Damaged += HandleKnightDamaged;
            if (song != null) song.OnInterrupted += HandleSongInterrupted;
            EnemySoldier.AnyDamaged += HandleEnemyDamaged;
            EnemySoldier.AnyDied += HandleEnemyDied;
        }

        private void OnDisable()
        {
            if (combat != null) combat.StepStarted -= HandleStep;
            if (caster != null) caster.Cast -= HandleCast;
            if (knightHealth != null) knightHealth.Damaged -= HandleKnightDamaged;
            if (song != null) song.OnInterrupted -= HandleSongInterrupted;
            EnemySoldier.AnyDamaged -= HandleEnemyDamaged;
            EnemySoldier.AnyDied -= HandleEnemyDied;
            pendingExplosion = null;
        }

        private void HandleStep(int step) => SfxPlayer.Play(step >= firstHeavyStep ? comboHeavy : comboLight);

        private void HandleCast(int slot, AbilityData ability)
        {
            var spinData = ability as SpinAbilityData;
            if (spinData != null)
            {
                SfxPlayer.Play(spin);
                if (pendingExplosion != null) StopCoroutine(pendingExplosion);
                pendingExplosion = StartCoroutine(ExplosionAfter(spinData.spinDuration + spinData.explosionDelay));
                return;
            }

            if (ability is ChargeAbilityData) SfxPlayer.Play(charge);
        }

        private IEnumerator ExplosionAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            pendingExplosion = null;
            if (knightHealth == null || !knightHealth.IsDead) SfxPlayer.Play(spinExplosion);
        }

        private void HandleKnightDamaged(Health health, DamageInfo info) => SfxPlayer.Play(knightHurt);
        private void HandleEnemyDamaged(EnemySoldier soldier, DamageInfo info) => SfxPlayer.Play(info.IsHeavy ? impactHeavy : impactLight);
        private void HandleEnemyDied(EnemySoldier soldier) => SfxPlayer.Play(soldierDeath);
        private void HandleSongInterrupted(bool restarted) => SfxPlayer.Play(songInterrupted);
    }
}
