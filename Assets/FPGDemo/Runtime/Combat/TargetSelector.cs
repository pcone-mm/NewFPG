using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    /// <summary>
    /// Converts unordered spatial query candidates into deterministic domain targets.
    /// Legacy attacks retain their original priority rules. Formal pellet attacks truncate
    /// each sample at its first blocker, then order deduplicated runtime targets by first
    /// intersection. Formal area attacks select only overlap candidates with per-kind limits.
    /// </summary>
    public static class TargetSelector
    {
        // BattleSession uses the same bound for the raw Physics/query adapter
        // buffer. A port that cannot fit all raw candidates must report
        // DroppedCandidateCount instead of silently truncating.
        public const int DefaultCandidateCapacity = SpatialContract.AttackQueryCandidateCapacity;

        public static DomainResult Select(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            in AttackQueryResult queryResult,
            QueryCandidate[] output,
            out int selectedCount)
        {
            selectedCount = 0;
            if (candidates == null || output == null || ReferenceEquals(candidates, output))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!IsAttackValid(attack))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int candidateCount = queryResult.CandidateCount;
            if (candidateCount < 0 || candidateCount > candidates.Length
                || queryResult.DroppedCandidateCount > 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!candidate.IsValid || !MatchesPolicy(candidate, attack))
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
            }

            int requiredCount;
            switch (attack.QueryMode)
            {
                case AttackQueryMode.Legacy:
                    requiredCount = attack.QueryPolicy == QueryPolicy.PelletRays
                        ? CountPrimarySelections(attack, candidates, candidateCount)
                        : CountSecondarySelections(attack, candidates, candidateCount);
                    break;
                case AttackQueryMode.FirstSurfacePenetration:
                    requiredCount = CountFirstSurfaceSelections(attack, candidates, candidateCount);
                    break;
                case AttackQueryMode.AreaAtFirstSurface:
                    requiredCount = CountAreaAtFirstSurfaceSelections(attack, candidates, candidateCount);
                    break;
                default:
                    return DomainResult.Rejected(RejectReason.InvalidState);
            }
            if (requiredCount > output.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (requiredCount == 0)
            {
                return DomainResult.Success;
            }

            if (attack.QueryMode == AttackQueryMode.FirstSurfacePenetration)
            {
                FillFirstSurfaceSelections(
                    attack,
                    candidates,
                    candidateCount,
                    output,
                    requiredCount);
            }
            else if (attack.QueryMode == AttackQueryMode.AreaAtFirstSurface)
            {
                FillAreaAtFirstSurfaceSelections(
                    attack,
                    candidates,
                    candidateCount,
                    output,
                    requiredCount);
            }
            else if (attack.QueryPolicy == QueryPolicy.PelletRays)
            {
                FillPrimarySelections(attack, candidates, candidateCount, output, requiredCount);
            }
            else
            {
                FillSecondarySelections(attack, candidates, candidateCount, output, requiredCount);
            }

            selectedCount = requiredCount;
            return DomainResult.Success;
        }

        private static bool IsAttackValid(in AttackSnapshot attack)
        {
            if (!attack.AttackId.IsValid
                || !attack.ShotId.IsValid
                || !attack.OwnerId.IsValid
                || !attack.ReleaseTick.IsValid
                || attack.PayloadCount <= 0
                || attack.MaxImpactCount <= 0
                || !attack.IsQueryConfigurationValid)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesPolicy(in QueryCandidate candidate, in AttackSnapshot attack)
        {
            if (attack.QueryMode == AttackQueryMode.FirstSurfacePenetration
                || attack.QueryPolicy == QueryPolicy.PelletRays)
            {
                return candidate.QueryStage == AttackQueryStage.Pellet
                    && candidate.SampleIndex < attack.PayloadCount;
            }

            return candidate.QueryStage == AttackQueryStage.Direct
                || candidate.QueryStage == AttackQueryStage.Area;
        }

        private static int CountPrimarySelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount)
        {
            int count = 0;
            int previousSample = -1;
            while (count < attack.MaxImpactCount
                && TryFindNextSample(candidates, candidateCount, previousSample, out int sampleIndex))
            {
                previousSample = sampleIndex;
                if (TryFindBestPrimaryCandidate(
                    attack,
                    candidates,
                    candidateCount,
                    sampleIndex,
                    out QueryCandidate ignored))
                {
                    count++;
                }
            }

            return count;
        }

        private static void FillPrimarySelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            QueryCandidate[] output,
            int requiredCount)
        {
            int count = 0;
            int previousSample = -1;
            while (count < requiredCount
                && count < attack.MaxImpactCount
                && TryFindNextSample(candidates, candidateCount, previousSample, out int sampleIndex))
            {
                previousSample = sampleIndex;
                if (TryFindBestPrimaryCandidate(
                    attack,
                    candidates,
                    candidateCount,
                    sampleIndex,
                    out QueryCandidate selected))
                {
                    output[count++] = selected;
                }
            }
        }

        private static bool TryFindNextSample(
            QueryCandidate[] candidates,
            int candidateCount,
            int previousSample,
            out int sampleIndex)
        {
            sampleIndex = int.MaxValue;
            for (int index = 0; index < candidateCount; index++)
            {
                int candidateSample = candidates[index].SampleIndex;
                if (candidateSample > previousSample && candidateSample < sampleIndex)
                {
                    sampleIndex = candidateSample;
                }
            }

            return sampleIndex != int.MaxValue;
        }

        private static bool TryFindBestPrimaryCandidate(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            int sampleIndex,
            out QueryCandidate selected)
        {
            long blockerDistance = FindNearestBlockerDistance(
                candidates,
                candidateCount,
                AttackQueryStage.Pellet,
                sampleIndex);
            bool found = false;
            selected = default(QueryCandidate);
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.SampleIndex != sampleIndex
                    || !IsImpactTargetAllowed(attack, candidate.TargetKind)
                    || candidate.DistanceKey >= blockerDistance)
                {
                    continue;
                }

                if (!found || CompareTarget(candidate, selected) < 0)
                {
                    selected = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static int CountFirstSurfaceSelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount)
        {
            int totalCount = 0;
            int perPelletLimit = attack.AdditionalPenetrationCount + 1;
            int previousSample = -1;
            while (TryFindNextSample(candidates, candidateCount, previousSample, out int sampleIndex))
            {
                previousSample = sampleIndex;
                long blockerDistance = FindNearestBlockerDistance(
                    candidates,
                    candidateCount,
                    AttackQueryStage.Pellet,
                    sampleIndex);
                int laneCount = CountUniqueTargetsInPelletLane(
                    attack,
                    candidates,
                    candidateCount,
                    sampleIndex,
                    blockerDistance,
                    perPelletLimit);
                totalCount += laneCount;
            }

            return totalCount;
        }

        private static int CountUniqueTargetsInPelletLane(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            int sampleIndex,
            long blockerDistance,
            int limit)
        {
            int count = 0;
            for (int index = 0; index < candidateCount && count < limit; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!IsEligiblePelletTarget(attack, candidate, sampleIndex, blockerDistance)
                    || HasPriorEligibleTarget(
                        attack,
                        candidates,
                        index,
                        candidate.TargetId,
                        AttackQueryStage.Pellet,
                        sampleIndex,
                        blockerDistance))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void FillFirstSurfaceSelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            QueryCandidate[] output,
            int requiredCount)
        {
            int outputCount = 0;
            int perPelletLimit = attack.AdditionalPenetrationCount + 1;
            int previousSample = -1;
            while (outputCount < requiredCount
                && TryFindNextSample(candidates, candidateCount, previousSample, out int sampleIndex))
            {
                previousSample = sampleIndex;
                long blockerDistance = FindNearestBlockerDistance(
                    candidates,
                    candidateCount,
                    AttackQueryStage.Pellet,
                    sampleIndex);
                int pelletStart = outputCount;
                while (outputCount - pelletStart < perPelletLimit
                    && TryFindNextPelletTarget(
                        attack,
                        candidates,
                        candidateCount,
                        sampleIndex,
                        blockerDistance,
                        output,
                        pelletStart,
                        outputCount,
                        out QueryCandidate selected))
                {
                    output[outputCount++] = selected;
                }
            }
        }

        private static bool TryFindNextPelletTarget(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            int sampleIndex,
            long blockerDistance,
            QueryCandidate[] output,
            int selectedStart,
            int selectedCount,
            out QueryCandidate selected)
        {
            bool found = false;
            int selectedFirstDistance = 0;
            selected = default(QueryCandidate);
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!IsEligiblePelletTarget(attack, candidate, sampleIndex, blockerDistance)
                    || ContainsTarget(
                        output,
                        selectedStart,
                        selectedCount,
                        candidate.TargetId)
                    || !TryFindPreferredTargetObservation(
                        attack,
                        candidates,
                        candidateCount,
                        candidate.TargetId,
                        AttackQueryStage.Pellet,
                        sampleIndex,
                        blockerDistance,
                        out QueryCandidate representative,
                        out int firstDistance))
                {
                    continue;
                }

                if (!found || CompareTargetGroup(
                    firstDistance,
                    representative,
                    selectedFirstDistance,
                    selected) < 0)
                {
                    selected = representative;
                    selectedFirstDistance = firstDistance;
                    found = true;
                }
            }

            return found;
        }

        private static int CountAreaAtFirstSurfaceSelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount)
        {
            int combatantCount = CountUniqueAreaTargets(
                attack,
                candidates,
                candidateCount,
                QueryTargetKind.Combatant,
                attack.AreaCombatantLimit);
            int projectileCount = CountUniqueAreaTargets(
                attack,
                candidates,
                candidateCount,
                QueryTargetKind.Projectile,
                attack.AreaProjectileLimit);
            return combatantCount + projectileCount;
        }

        private static int CountUniqueAreaTargets(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            QueryTargetKind targetKind,
            int limit)
        {
            int count = 0;
            for (int index = 0; index < candidateCount && count < limit; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!IsEligibleAreaTarget(attack, candidate, targetKind)
                    || HasPriorEligibleAreaTarget(
                        attack,
                        candidates,
                        index,
                        candidate.TargetId,
                        targetKind))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void FillAreaAtFirstSurfaceSelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            QueryCandidate[] output,
            int requiredCount)
        {
            int outputCount = 0;
            int combatantCount = 0;
            int projectileCount = 0;
            while (outputCount < requiredCount)
            {
                bool found = false;
                int selectedFirstDistance = 0;
                QueryCandidate selected = default(QueryCandidate);
                for (int index = 0; index < candidateCount; index++)
                {
                    QueryCandidate candidate = candidates[index];
                    if (!IsEligibleAreaTarget(attack, candidate)
                        || candidate.TargetKind == QueryTargetKind.Combatant
                            && combatantCount >= attack.AreaCombatantLimit
                        || candidate.TargetKind == QueryTargetKind.Projectile
                            && projectileCount >= attack.AreaProjectileLimit
                        || ContainsTarget(
                            output,
                            0,
                            outputCount,
                            candidate.TargetId,
                            candidate.TargetKind)
                        || !TryFindPreferredAreaTargetObservation(
                            attack,
                            candidates,
                            candidateCount,
                            candidate.TargetId,
                            candidate.TargetKind,
                            out QueryCandidate representative,
                            out int firstDistance))
                    {
                        continue;
                    }

                    if (!found || CompareTargetGroup(
                        firstDistance,
                        representative,
                        selectedFirstDistance,
                        selected) < 0)
                    {
                        selected = representative;
                        selectedFirstDistance = firstDistance;
                        found = true;
                    }
                }

                if (!found)
                {
                    break;
                }

                output[outputCount++] = selected;
                if (selected.TargetKind == QueryTargetKind.Combatant)
                {
                    combatantCount++;
                }
                else
                {
                    projectileCount++;
                }
            }
        }

        private static bool IsEligiblePelletTarget(
            in AttackSnapshot attack,
            in QueryCandidate candidate,
            int sampleIndex,
            long blockerDistance)
        {
            return candidate.QueryStage == AttackQueryStage.Pellet
                && candidate.SampleIndex == sampleIndex
                && IsImpactTargetAllowed(attack, candidate.TargetKind)
                && candidate.DistanceKey < blockerDistance;
        }

        private static bool IsEligibleAreaTarget(
            in AttackSnapshot attack,
            in QueryCandidate candidate)
        {
            return candidate.QueryStage == AttackQueryStage.Area
                && IsImpactTargetAllowed(attack, candidate.TargetKind);
        }

        private static bool IsEligibleAreaTarget(
            in AttackSnapshot attack,
            in QueryCandidate candidate,
            QueryTargetKind targetKind)
        {
            return IsEligibleAreaTarget(attack, candidate)
                && candidate.TargetKind == targetKind;
        }

        private static bool HasPriorEligibleTarget(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateIndex,
            RuntimeId targetId,
            AttackQueryStage stage,
            int sampleIndex,
            long blockerDistance)
        {
            for (int index = 0; index < candidateIndex; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.TargetId == targetId
                    && candidate.QueryStage == stage
                    && candidate.SampleIndex == sampleIndex
                    && IsImpactTargetAllowed(attack, candidate.TargetKind)
                    && candidate.DistanceKey < blockerDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPriorEligibleAreaTarget(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateIndex,
            RuntimeId targetId,
            QueryTargetKind targetKind)
        {
            for (int index = 0; index < candidateIndex; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.TargetId == targetId
                    && IsEligibleAreaTarget(attack, candidate, targetKind))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindPreferredTargetObservation(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            RuntimeId targetId,
            AttackQueryStage stage,
            int sampleIndex,
            long blockerDistance,
            out QueryCandidate representative,
            out int firstDistance)
        {
            bool found = false;
            representative = default(QueryCandidate);
            firstDistance = int.MaxValue;
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.TargetId != targetId
                    || candidate.QueryStage != stage
                    || candidate.SampleIndex != sampleIndex
                    || !IsImpactTargetAllowed(attack, candidate.TargetKind)
                    || candidate.DistanceKey >= blockerDistance)
                {
                    continue;
                }

                if (candidate.DistanceKey < firstDistance)
                {
                    firstDistance = candidate.DistanceKey;
                }

                if (!found || CompareWithinRuntime(candidate, representative) < 0)
                {
                    representative = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryFindPreferredAreaTargetObservation(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            RuntimeId targetId,
            QueryTargetKind targetKind,
            out QueryCandidate representative,
            out int firstDistance)
        {
            bool found = false;
            representative = default(QueryCandidate);
            firstDistance = int.MaxValue;
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.TargetId != targetId
                    || !IsEligibleAreaTarget(attack, candidate, targetKind))
                {
                    continue;
                }

                if (candidate.DistanceKey < firstDistance)
                {
                    firstDistance = candidate.DistanceKey;
                }

                if (!found || CompareWithinRuntime(candidate, representative) < 0)
                {
                    representative = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static int CompareTargetGroup(
            int leftFirstDistance,
            in QueryCandidate left,
            int rightFirstDistance,
            in QueryCandidate right)
        {
            int distance = leftFirstDistance.CompareTo(rightFirstDistance);
            if (distance != 0)
            {
                return distance;
            }

            int runtime = left.TargetId.CompareTo(right.TargetId);
            if (runtime != 0)
            {
                return runtime;
            }

            int kind = left.TargetKind.CompareTo(right.TargetKind);
            return kind != 0 ? kind : CompareWithinRuntime(left, right);
        }

        private static int CompareWithinRuntime(
            in QueryCandidate left,
            in QueryCandidate right)
        {
            int hitPart = GetWithinRuntimePriority(left)
                .CompareTo(GetWithinRuntimePriority(right));
            if (hitPart != 0)
            {
                return hitPart;
            }

            int distance = left.DistanceKey.CompareTo(right.DistanceKey);
            if (distance != 0)
            {
                return distance;
            }

            int geometry = left.GeometryId.CompareTo(right.GeometryId);
            if (geometry != 0)
            {
                return geometry;
            }

            int pointX = left.ImpactPointKey.X.CompareTo(right.ImpactPointKey.X);
            if (pointX != 0)
            {
                return pointX;
            }

            int pointY = left.ImpactPointKey.Y.CompareTo(right.ImpactPointKey.Y);
            if (pointY != 0)
            {
                return pointY;
            }

            int pointZ = left.ImpactPointKey.Z.CompareTo(right.ImpactPointKey.Z);
            return pointZ != 0 ? pointZ : left.QueryOrdinal.CompareTo(right.QueryOrdinal);
        }

        private static int GetWithinRuntimePriority(in QueryCandidate candidate)
        {
            return candidate.HitPart == HitPart.Weakpoint ? 0 : 1;
        }

        private static int CountSecondarySelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount)
        {
            long directBlockerDistance = FindNearestBlockerDistance(
                candidates,
                candidateCount,
                AttackQueryStage.Direct,
                -1);
            long areaBlockerDistance = FindNearestBlockerDistance(
                candidates,
                candidateCount,
                AttackQueryStage.Area,
                -1);
            int count = 0;
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!IsSecondaryEligible(
                    attack,
                    candidate,
                    directBlockerDistance,
                    areaBlockerDistance))
                {
                    continue;
                }

                bool duplicateTarget = false;
                for (int previous = 0; previous < index; previous++)
                {
                    QueryCandidate prior = candidates[previous];
                    if (IsSecondaryEligible(
                            attack,
                            prior,
                            directBlockerDistance,
                            areaBlockerDistance)
                        && prior.TargetId == candidate.TargetId)
                    {
                        duplicateTarget = true;
                        break;
                    }
                }

                if (!duplicateTarget && ++count >= attack.MaxImpactCount)
                {
                    return attack.MaxImpactCount;
                }
            }

            return count;
        }

        private static void FillSecondarySelections(
            in AttackSnapshot attack,
            QueryCandidate[] candidates,
            int candidateCount,
            QueryCandidate[] output,
            int requiredCount)
        {
            long directBlockerDistance = FindNearestBlockerDistance(
                candidates,
                candidateCount,
                AttackQueryStage.Direct,
                -1);
            long areaBlockerDistance = FindNearestBlockerDistance(
                candidates,
                candidateCount,
                AttackQueryStage.Area,
                -1);
            int count = 0;
            while (count < requiredCount && count < attack.MaxImpactCount)
            {
                bool found = false;
                QueryCandidate selected = default(QueryCandidate);
                for (int index = 0; index < candidateCount; index++)
                {
                    QueryCandidate candidate = candidates[index];
                    if (!IsSecondaryEligible(
                            attack,
                            candidate,
                            directBlockerDistance,
                            areaBlockerDistance)
                        || ContainsTarget(output, count, candidate.TargetId))
                    {
                        continue;
                    }

                    if (!found || CompareTarget(candidate, selected) < 0)
                    {
                        selected = candidate;
                        found = true;
                    }
                }

                if (!found)
                {
                    break;
                }

                output[count++] = selected;
            }
        }

        private static bool IsSecondaryEligible(
            in AttackSnapshot attack,
            in QueryCandidate candidate,
            long directBlockerDistance,
            long areaBlockerDistance)
        {
            if (!IsImpactTargetAllowed(attack, candidate.TargetKind))
            {
                return false;
            }

            long blockerDistance = candidate.QueryStage == AttackQueryStage.Direct
                ? directBlockerDistance
                : areaBlockerDistance;
            return candidate.DistanceKey < blockerDistance;
        }

        private static bool IsImpactTargetAllowed(
            in AttackSnapshot attack,
            QueryTargetKind targetKind)
        {
            switch (targetKind)
            {
                case QueryTargetKind.Combatant:
                    return (attack.AllowedTargetKinds & AttackTargetKinds.Combatant)
                        != AttackTargetKinds.None;
                case QueryTargetKind.Projectile:
                    return (attack.AllowedTargetKinds & AttackTargetKinds.Projectile)
                        != AttackTargetKinds.None;
                default:
                    return false;
            }
        }

        private static long FindNearestBlockerDistance(
            QueryCandidate[] candidates,
            int candidateCount,
            AttackQueryStage stage,
            int sampleIndex)
        {
            long distance = long.MaxValue;
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.TargetKind == QueryTargetKind.EnvironmentBlocker
                    && candidate.QueryStage == stage
                    && candidate.SampleIndex == sampleIndex
                    && candidate.DistanceKey < distance)
                {
                    distance = candidate.DistanceKey;
                }
            }

            return distance;
        }

        private static bool ContainsTarget(
            QueryCandidate[] selected,
            int selectedCount,
            RuntimeId targetId)
        {
            return ContainsTarget(selected, 0, selectedCount, targetId);
        }

        private static bool ContainsTarget(
            QueryCandidate[] selected,
            int startIndex,
            int endIndex,
            RuntimeId targetId)
        {
            for (int index = startIndex; index < endIndex; index++)
            {
                if (selected[index].TargetId == targetId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTarget(
            QueryCandidate[] selected,
            int startIndex,
            int endIndex,
            RuntimeId targetId,
            QueryTargetKind targetKind)
        {
            for (int index = startIndex; index < endIndex; index++)
            {
                if (selected[index].TargetId == targetId
                    && selected[index].TargetKind == targetKind)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareTarget(in QueryCandidate left, in QueryCandidate right)
        {
            int priority = GetPriority(left).CompareTo(GetPriority(right));
            if (priority != 0)
            {
                return priority;
            }

            int distance = left.DistanceKey.CompareTo(right.DistanceKey);
            if (distance != 0)
            {
                return distance;
            }

            int runtime = left.TargetId.CompareTo(right.TargetId);
            if (runtime != 0)
            {
                return runtime;
            }

            int geometry = left.GeometryId.CompareTo(right.GeometryId);
            if (geometry != 0)
            {
                return geometry;
            }

            // Stable identity keys above decide normal candidates. The remaining fields
            // total-order duplicate observations without relying on input/Physics order.
            int stage = left.QueryStage.CompareTo(right.QueryStage);
            if (stage != 0)
            {
                return stage;
            }

            int sample = left.SampleIndex.CompareTo(right.SampleIndex);
            if (sample != 0)
            {
                return sample;
            }

            int pointX = left.ImpactPointKey.X.CompareTo(right.ImpactPointKey.X);
            if (pointX != 0)
            {
                return pointX;
            }

            int pointY = left.ImpactPointKey.Y.CompareTo(right.ImpactPointKey.Y);
            if (pointY != 0)
            {
                return pointY;
            }

            int pointZ = left.ImpactPointKey.Z.CompareTo(right.ImpactPointKey.Z);
            return pointZ != 0 ? pointZ : left.QueryOrdinal.CompareTo(right.QueryOrdinal);
        }

        private static int GetPriority(in QueryCandidate candidate)
        {
            if (candidate.TargetKind == QueryTargetKind.Projectile)
            {
                return 0;
            }

            return candidate.HitPart == HitPart.Weakpoint ? 1 : 2;
        }
    }
}
