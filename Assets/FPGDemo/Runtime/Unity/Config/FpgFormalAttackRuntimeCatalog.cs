using System;
using FPG.Demo.Combat;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Completes an authored attack with the fixed-budget and presentation
    /// values required by the pure combat runtime. No runtime fallback values
    /// are used by the formal attack scheduler.
    /// </summary>
    [Serializable]
    public sealed class FpgFormalAttackRuntimeEntry
    {
        [SerializeField]
        private FpgEnemyAttackDefinition attack = null;

        [SerializeField, Min(1)]
        private int threatDefinitionId = 1;

        [SerializeField, Min(0)]
        private int weakpointDamageMultiplierBasisPoints = DamageSpec.BasisPoints;

        [SerializeField, Min(0)]
        private int weakpointBreakMultiplierBasisPoints = DamageSpec.BasisPoints;

        [Header("Projectile")]
        [SerializeField, Min(0)]
        private int projectileMaxHitPoints = 0;

        [SerializeField, Min(1)]
        private int projectileBudgetUnits = 1;

        [SerializeField, Min(1)]
        private int projectilePresentationKey = 1;

        [SerializeField, Min(1)]
        private int projectileSweepRadiusKey = 1;

        [Header("Timed Impact")]
        [SerializeField, Min(0)]
        private int timedImpactDelayTicks = 0;

        [SerializeField, Min(1)]
        private int timedImpactPresentationKey = 1;

        public FpgEnemyAttackDefinition Attack => attack;
        public int ThreatDefinitionId => threatDefinitionId;
        public int WeakpointDamageMultiplierBasisPoints => weakpointDamageMultiplierBasisPoints;
        public int WeakpointBreakMultiplierBasisPoints => weakpointBreakMultiplierBasisPoints;
        public int ProjectileMaxHitPoints => projectileMaxHitPoints;
        public int ProjectileBudgetUnits => projectileBudgetUnits;
        public int ProjectilePresentationKey => projectilePresentationKey;
        public int ProjectileSweepRadiusKey => projectileSweepRadiusKey;
        public int TimedImpactDelayTicks => timedImpactDelayTicks;
        public int TimedImpactPresentationKey => timedImpactPresentationKey;

        public bool TryValidate(out string error)
        {
            if (attack == null)
            {
                error = "Formal attack runtime entry is missing its attack asset.";
                return false;
            }

            if (!attack.TryValidate(out string attackError))
            {
                error = $"Formal attack runtime entry for '{attack.AttackId}' is invalid: {attackError}";
                return false;
            }

            if (weakpointDamageMultiplierBasisPoints < 0
                || weakpointBreakMultiplierBasisPoints < 0)
            {
                error = $"Formal attack runtime entry '{attack.AttackId}' has invalid weakpoint multipliers.";
                return false;
            }

            switch (attack.Kind)
            {
                case FpgEnemyAttackKind.Projectile:
                    if (threatDefinitionId <= 0
                        || projectileMaxHitPoints < 0
                        || (attack.Interceptable && projectileMaxHitPoints <= 0)
                        || projectileBudgetUnits <= 0
                        || projectilePresentationKey <= 0
                        || projectileSweepRadiusKey <= 0)
                    {
                        error = $"Formal projectile runtime entry '{attack.AttackId}' has invalid capacity or presentation values.";
                        return false;
                    }

                    break;

                case FpgEnemyAttackKind.TimedImpact:
                    if (threatDefinitionId <= 0
                        || timedImpactDelayTicks < 0
                        || timedImpactPresentationKey <= 0)
                    {
                        error = $"Formal timed-impact runtime entry '{attack.AttackId}' has invalid timing or presentation values.";
                        return false;
                    }

                    break;

                case FpgEnemyAttackKind.Summon:
                    break;

                default:
                    error = $"Formal attack runtime entry '{attack.AttackId}' has an unsupported attack kind.";
                    return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "FpgFormalAttackRuntimeCatalog",
        menuName = "FPG Demo/Formal Encounter/Attack Runtime Catalog")]
    public sealed class FpgFormalAttackRuntimeCatalog : ScriptableObject
    {
        [SerializeField]
        private FpgFormalAttackRuntimeEntry[] entries = Array.Empty<FpgFormalAttackRuntimeEntry>();

        public int EntryCount => entries == null ? 0 : entries.Length;

        public FpgFormalAttackRuntimeEntry GetEntry(int index)
        {
            if (entries == null || index < 0 || index >= entries.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return entries[index];
        }

        public bool TryResolve(
            FpgEnemyAttackDefinition attack,
            out FpgFormalAttackRuntimeEntry entry)
        {
            if (attack != null && entries != null)
            {
                for (int index = 0; index < entries.Length; index++)
                {
                    FpgFormalAttackRuntimeEntry candidate = entries[index];
                    if (candidate != null && candidate.Attack == attack)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (entries == null || entries.Length == 0)
            {
                error = "Formal attack runtime catalog requires at least one entry.";
                return false;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                FpgFormalAttackRuntimeEntry entry = entries[index];
                if (entry == null)
                {
                    error = $"Formal attack runtime catalog entry {index} is missing.";
                    return false;
                }

                if (!entry.TryValidate(out string entryError))
                {
                    error = entryError;
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    FpgFormalAttackRuntimeEntry other = entries[previous];
                    if (other != null && other.Attack == entry.Attack)
                    {
                        error = $"Formal attack runtime catalog repeats attack '{entry.Attack.AttackId}'.";
                        return false;
                    }

                    if (entry.Attack.Kind != FpgEnemyAttackKind.Summon
                        && other != null
                        && other.Attack != null
                        && other.Attack.Kind != FpgEnemyAttackKind.Summon
                        && other.ThreatDefinitionId == entry.ThreatDefinitionId)
                    {
                        error = $"Formal attack runtime catalog repeats threat definition ID {entry.ThreatDefinitionId}.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
