using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Battle
{
    public static class TargetSelector
    {
        public static EnemyCombatState SelectTarget(BattleSessionContext context, ArtifactCombatProfile profile)
        {
            if (context == null)
            {
                return null;
            }

            TargetSelectorType selectorType = TargetSelectorType.none;
            if (profile != null)
            {
                selectorType = profile.targetSelectorType;
            }

            return SelectTarget(selectorType, context.enemies, context.playerPosition, context.focusTarget);
        }

        public static bool TrySelectTarget(BattleSessionContext context, ArtifactCombatProfile profile, out EnemyCombatState target)
        {
            target = SelectTarget(context, profile);
            return target != null;
        }

        public static EnemyCombatState SelectTarget(
            TargetSelectorType selectorType,
            IReadOnlyList<EnemyCombatState> candidates,
            Vector3 playerPosition,
            EnemyCombatState focusTarget = null)
        {
            List<EnemyCombatState> validTargets = CollectValidTargets(candidates);
            if (validTargets.Count == 0)
            {
                return null;
            }

            switch (selectorType)
            {
                case TargetSelectorType.none:
                    return null;
                case TargetSelectorType.focus_then_nearest:
                    if (IsValidTarget(focusTarget))
                    {
                        return focusTarget;
                    }

                    return SelectNearest(validTargets, playerPosition);
                case TargetSelectorType.nearest:
                    return SelectNearest(validTargets, playerPosition);
                case TargetSelectorType.lowest_hp:
                    return SelectLowestHp(validTargets, playerPosition);
                case TargetSelectorType.charging_then_near_player:
                {
                    List<EnemyCombatState> chargingTargets = CollectByState(validTargets, true, false, false);
                    if (chargingTargets.Count > 0)
                    {
                        return SelectNearest(chargingTargets, playerPosition);
                    }

                    return SelectNearest(validTargets, playerPosition);
                }
                case TargetSelectorType.interruptible_charging_only:
                {
                    List<EnemyCombatState> interruptibleChargingTargets = CollectInterruptibleChargingTargets(validTargets);
                    if (interruptibleChargingTargets.Count == 0)
                    {
                        return null;
                    }

                    return SelectNearest(interruptibleChargingTargets, playerPosition);
                }
                case TargetSelectorType.high_threat_near_player:
                {
                    List<EnemyCombatState> nearTargets = CollectByRangeBand(validTargets, RangeBand.Near, RangeBand.Melee);
                    if (nearTargets.Count == 0)
                    {
                        nearTargets = validTargets;
                    }

                    return SelectHighestThreat(nearTargets, playerPosition);
                }
                case TargetSelectorType.slowed_then_focus:
                {
                    List<EnemyCombatState> slowedTargets = CollectSlowedTargets(validTargets);
                    if (slowedTargets.Count > 0)
                    {
                        return SelectNearest(slowedTargets, playerPosition);
                    }

                    if (IsValidTarget(focusTarget))
                    {
                        return focusTarget;
                    }

                    return SelectNearest(validTargets, playerPosition);
                }
                default:
                    return SelectNearest(validTargets, playerPosition);
            }
        }

        public static bool TrySelectTarget(
            TargetSelectorType selectorType,
            IReadOnlyList<EnemyCombatState> candidates,
            Vector3 playerPosition,
            EnemyCombatState focusTarget,
            out EnemyCombatState target)
        {
            target = SelectTarget(selectorType, candidates, playerPosition, focusTarget);
            return target != null;
        }

        private static List<EnemyCombatState> CollectValidTargets(IReadOnlyList<EnemyCombatState> candidates)
        {
            List<EnemyCombatState> validTargets = new List<EnemyCombatState>();
            if (candidates == null)
            {
                return validTargets;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                if (IsValidTarget(candidate))
                {
                    validTargets.Add(candidate);
                }
            }

            return validTargets;
        }

        private static List<EnemyCombatState> CollectByState(
            IReadOnlyList<EnemyCombatState> candidates,
            bool includeCharging,
            bool includeSlowed,
            bool includeNearPlayer)
        {
            List<EnemyCombatState> filteredTargets = new List<EnemyCombatState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                bool matches = false;
                if (includeCharging && candidate.isCharging)
                {
                    matches = true;
                }

                if (includeSlowed && candidate.isSlowed)
                {
                    matches = true;
                }

                if (includeNearPlayer && (candidate.rangeBand == RangeBand.Near || candidate.rangeBand == RangeBand.Melee))
                {
                    matches = true;
                }

                if (matches)
                {
                    filteredTargets.Add(candidate);
                }
            }

            return filteredTargets;
        }

        private static List<EnemyCombatState> CollectInterruptibleChargingTargets(IReadOnlyList<EnemyCombatState> candidates)
        {
            List<EnemyCombatState> filteredTargets = new List<EnemyCombatState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                if (IsValidTarget(candidate) && candidate.isCharging && candidate.isInterruptible)
                {
                    filteredTargets.Add(candidate);
                }
            }

            return filteredTargets;
        }

        private static List<EnemyCombatState> CollectSlowedTargets(IReadOnlyList<EnemyCombatState> candidates)
        {
            List<EnemyCombatState> filteredTargets = new List<EnemyCombatState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                if (IsValidTarget(candidate) && candidate.isSlowed)
                {
                    filteredTargets.Add(candidate);
                }
            }

            return filteredTargets;
        }

        private static List<EnemyCombatState> CollectByRangeBand(
            IReadOnlyList<EnemyCombatState> candidates,
            RangeBand firstBand,
            RangeBand secondBand)
        {
            List<EnemyCombatState> filteredTargets = new List<EnemyCombatState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                if (!IsValidTarget(candidate))
                {
                    continue;
                }

                if (candidate.rangeBand == firstBand || candidate.rangeBand == secondBand)
                {
                    filteredTargets.Add(candidate);
                }
            }

            return filteredTargets;
        }

        private static EnemyCombatState SelectNearest(IReadOnlyList<EnemyCombatState> candidates, Vector3 playerPosition)
        {
            EnemyCombatState selectedTarget = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                float distance = DistanceSqrToPlayer(candidate, playerPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    selectedTarget = candidate;
                }
            }

            return selectedTarget;
        }

        private static EnemyCombatState SelectLowestHp(IReadOnlyList<EnemyCombatState> candidates, Vector3 playerPosition)
        {
            EnemyCombatState selectedTarget = null;
            float lowestHp = float.MaxValue;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                float hp = candidate.hp;
                float distance = DistanceSqrToPlayer(candidate, playerPosition);
                if (hp < lowestHp || (Mathf.Approximately(hp, lowestHp) && distance < bestDistance))
                {
                    lowestHp = hp;
                    bestDistance = distance;
                    selectedTarget = candidate;
                }
            }

            return selectedTarget;
        }

        private static EnemyCombatState SelectHighestThreat(IReadOnlyList<EnemyCombatState> candidates, Vector3 playerPosition)
        {
            EnemyCombatState selectedTarget = null;
            float highestThreat = float.MinValue;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                EnemyCombatState candidate = candidates[i];
                float threat = candidate.threatScore;
                float distance = DistanceSqrToPlayer(candidate, playerPosition);
                if (threat > highestThreat || (Mathf.Approximately(threat, highestThreat) && distance < bestDistance))
                {
                    highestThreat = threat;
                    bestDistance = distance;
                    selectedTarget = candidate;
                }
            }

            return selectedTarget;
        }

        private static float DistanceSqrToPlayer(EnemyCombatState enemy, Vector3 playerPosition)
        {
            Vector3 delta = enemy.position - playerPosition;
            return delta.sqrMagnitude;
        }

        private static bool IsValidTarget(EnemyCombatState enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            return enemy.CanBeTargeted;
        }
    }
}
