using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    /// <summary>
    /// Converts unordered spatial query candidates into deterministic domain targets.
    /// Blockers are isolated by query lane: pellet sample for primary attacks and query
    /// stage for secondary attacks. A target at the same DistanceKey as a blocker is blocked.
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

            int requiredCount = attack.QueryPolicy == QueryPolicy.PelletRays
                ? CountPrimarySelections(attack, candidates, candidateCount)
                : CountSecondarySelections(attack, candidates, candidateCount);
            if (requiredCount > output.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (requiredCount == 0)
            {
                return DomainResult.Success;
            }

            if (attack.QueryPolicy == QueryPolicy.PelletRays)
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
            return attack.AttackId.IsValid
                && attack.ShotId.IsValid
                && attack.OwnerId.IsValid
                && attack.ReleaseTick.IsValid
                && attack.PayloadCount > 0
                && attack.MaxImpactCount > 0
                && (attack.QueryPolicy == QueryPolicy.PelletRays
                    || attack.QueryPolicy == QueryPolicy.DirectThenArea);
        }

        private static bool MatchesPolicy(in QueryCandidate candidate, in AttackSnapshot attack)
        {
            if (attack.QueryPolicy == QueryPolicy.PelletRays)
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
                    || candidate.TargetKind == QueryTargetKind.EnvironmentBlocker
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
                if (!IsSecondaryEligible(candidate, directBlockerDistance, areaBlockerDistance))
                {
                    continue;
                }

                bool duplicateTarget = false;
                for (int previous = 0; previous < index; previous++)
                {
                    QueryCandidate prior = candidates[previous];
                    if (IsSecondaryEligible(prior, directBlockerDistance, areaBlockerDistance)
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
                    if (!IsSecondaryEligible(candidate, directBlockerDistance, areaBlockerDistance)
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
            in QueryCandidate candidate,
            long directBlockerDistance,
            long areaBlockerDistance)
        {
            if (candidate.TargetKind == QueryTargetKind.EnvironmentBlocker)
            {
                return false;
            }

            long blockerDistance = candidate.QueryStage == AttackQueryStage.Direct
                ? directBlockerDistance
                : areaBlockerDistance;
            return candidate.DistanceKey < blockerDistance;
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
            for (int index = 0; index < selectedCount; index++)
            {
                if (selected[index].TargetId == targetId)
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
