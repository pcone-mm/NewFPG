using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
/// <summary>
    /// One reusable attack pattern. Timing is relative to the owning enemy's
    /// activation tick, so a stalled/dead enemy cannot block another owner.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgEnemyAttackDefinition",
        menuName = "FPG Demo/Formal Encounter/Enemy Attack")]
    public sealed class FpgEnemyAttackDefinition : ScriptableObject
    {
        [D0PlannerSection("Identity")]
        [D0PlannerField("Attack ID", "Stable attack identity used in per-owner scheduling and diagnostics.")]
        [SerializeField]
        private string attackId = "attack";

        [D0PlannerField("Display Name", "Authoring-only display name.")]
        [SerializeField]
        private string displayName = "Attack";

        [D0PlannerField("Priority", "Tie-break priority after ReadyTick and SpawnSequence.")]
        [SerializeField]
        private int priority;

        [D0PlannerField("Kind", "Projectile, TimedImpact, or generic Summon.")]
        [SerializeField]
        private FpgEnemyAttackKind kind = FpgEnemyAttackKind.Projectile;

        [D0PlannerSection("Timing")]
        [D0PlannerField("First Ready Offset (Ticks)", "Offset from the owner's activation tick at which this attack first becomes eligible.")]
        [SerializeField, Min(0)]
        private int firstReadyOffsetTicks = 60;

        [D0PlannerField("Cooldown (Ticks)", "Per-owner cooldown measured from release.")]
        [SerializeField, Min(0)]
        private int cooldownTicks = 90;

        [D0PlannerField("Telegraph (Ticks)", "Warning period. No hitbox, threat, or damage is active during this period.")]
        [SerializeField, Min(0)]
        private int telegraphTicks = 20;

        [D0PlannerField("Windup (Ticks)", "Delay between warning end and payload release.")]
        [SerializeField, Min(0)]
        private int windupTicks = 10;

        [D0PlannerField("Recovery (Ticks)", "Post-release recovery period for this owner only.")]
        [SerializeField, Min(0)]
        private int recoveryTicks = 25;

        [D0PlannerSection("Payload")]
        [D0PlannerField("Damage", "Base player damage for projectile or timed-impact payloads.")]
        [SerializeField, Min(0)]
        private int damage = 10;

        [D0PlannerField("Break Damage", "Base player resilience damage for projectile or timed-impact payloads.")]
        [SerializeField, Min(0)]
        private int breakDamage;

        [D0PlannerField("Projectile Count", "Number of projectiles released by one attack; ignored for non-projectile kinds.")]
        [SerializeField, Min(1)]
        private int projectileCount = 1;

        [D0PlannerField("Projectile Definition ID", "Stable projectile identity used by the projectile pool.")]
        [SerializeField, Min(1)]
        private int projectileDefinitionId = 1;

        [D0PlannerField("Projectile Flight (Ticks)", "Projectile flight duration.")]
        [SerializeField, Min(1)]
        private int projectileFlightTicks = 30;

        [D0PlannerField("Projectile Lifetime (Ticks)", "Maximum projectile lifetime; must be at least flight duration.")]
        [SerializeField, Min(1)]
        private int projectileLifetimeTicks = 45;

        [D0PlannerField("Interceptable", "Whether the projectile can be intercepted by player attacks.")]
        [SerializeField]
        private bool interceptable;

        [D0PlannerField("Summon Action", "Generic summon payload; it enters the same Spawn Queue and capacity checks.")]
        [SerializeField]
        private FpgSummonActionDefinition summon;

        [D0PlannerSection("Presentation")]
        [D0PlannerField("Animation Slot", "Stable animation key for the formal entity view.")]
        [SerializeField]
        private string animationSlot = "attack";

        [D0PlannerField("Warning Slot", "Stable warning/telegraph key for the formal presentation pool.")]
        [SerializeField]
        private string warningSlot = "enemy-warning";

        public string AttackId => attackId;
        public string DisplayName => displayName;
        public int Priority => priority;
        public FpgEnemyAttackKind Kind => kind;
        public int FirstReadyOffsetTicks => firstReadyOffsetTicks;
        public int CooldownTicks => cooldownTicks;
        public int TelegraphTicks => telegraphTicks;
        public int WindupTicks => windupTicks;
        public int RecoveryTicks => recoveryTicks;
        public int Damage => damage;
        public int BreakDamage => breakDamage;
        public int ProjectileCount => projectileCount;
        public int ProjectileDefinitionId => projectileDefinitionId;
        public int ProjectileFlightTicks => projectileFlightTicks;
        public int ProjectileLifetimeTicks => projectileLifetimeTicks;
        public bool Interceptable => interceptable;
        public FpgSummonActionDefinition Summon => summon;
        public string AnimationSlot => animationSlot;
        public string WarningSlot => warningSlot;

        public int ActiveDurationTicks => telegraphTicks + windupTicks + recoveryTicks;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(attackId)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(animationSlot)
                || string.IsNullOrWhiteSpace(warningSlot))
            {
                error = "Formal attack requires identity and presentation keys.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FpgEnemyAttackKind), kind)
                || firstReadyOffsetTicks < 0
                || cooldownTicks < 0
                || telegraphTicks < 0
                || windupTicks < 0
                || recoveryTicks < 0
                || damage < 0
                || breakDamage < 0)
            {
                error = $"Formal attack '{attackId}' has invalid timing or damage values.";
                return false;
            }

            switch (kind)
            {
                case FpgEnemyAttackKind.Projectile:
                    if (projectileCount <= 0
                        || projectileDefinitionId <= 0
                        || projectileFlightTicks <= 0
                        || projectileLifetimeTicks < projectileFlightTicks)
                    {
                        error = $"Formal projectile attack '{attackId}' has invalid projectile values.";
                        return false;
                    }

                    break;

                case FpgEnemyAttackKind.TimedImpact:
                    if (projectileCount != 1)
                    {
                        error = $"Formal timed-impact attack '{attackId}' must use one payload.";
                        return false;
                    }

                    break;

                case FpgEnemyAttackKind.Summon:
                    if (summon == null)
                    {
                        error = $"Formal summon attack '{attackId}' requires a summon action.";
                        return false;
                    }

                    if (!summon.TryValidate(out error))
                    {
                        error = $"Formal summon attack '{attackId}' is invalid: {error}";
                        return false;
                    }

                    break;
            }

            if (kind != FpgEnemyAttackKind.Summon && summon != null)
            {
                error = $"Formal attack '{attackId}' has a summon action but is not a Summon attack.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
