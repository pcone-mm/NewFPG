using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgEnemyPositionRejectionReason
    {
        None = 0,
        OutsideRoom = 1,
        Obstacle = 2,
        Crowded = 3,
        Unreachable = 4,
        RecentPosition = 5,
        OutsideCamera = 6,
        OutsideAttackRange = 7
    }

    public readonly struct FpgEnemyPositionCandidate
    {
        public FpgEnemyPositionCandidate(
            Vector3 position,
            float score,
            FpgEnemyPositionRejectionReason rejectionReason,
            bool hasLineOfSight)
        {
            Position = position;
            Score = score;
            RejectionReason = rejectionReason;
            HasLineOfSight = hasLineOfSight;
        }

        public Vector3 Position { get; }
        public float Score { get; }
        public FpgEnemyPositionRejectionReason RejectionReason { get; }
        public bool IsAccepted => RejectionReason == FpgEnemyPositionRejectionReason.None;
        public bool HasLineOfSight { get; }
    }

    [CreateAssetMenu(
        fileName = "FpgEnemyPositionQueryDefinition",
        menuName = "FPG Demo/Formal Encounter/Enemy Position Query")]
    public sealed class FpgEnemyPositionQueryDefinition : ScriptableObject
    {
        public const int MaximumAngularSamples = 16;
        public const int MaximumHeightLayers = 5;
        public const int MaximumCandidateCount = 3
            * MaximumAngularSamples
            * MaximumHeightLayers;

        [SerializeField, Min(0.1f)]
        private float minimumAttackDistance = 5f;

        [SerializeField, Min(0.1f)]
        private float preferredAttackDistance = 7f;

        [SerializeField, Min(0.1f)]
        private float maximumAttackDistance = 9f;

        [SerializeField, Range(4, MaximumAngularSamples)]
        private int angularSamples = 8;

        [SerializeField]
        private float[] heightLayerOffsets = { -2.5f, 0f, 2.5f };

        [SerializeField, Min(1)]
        private int retryIntervalTicks = 18;

        [SerializeField, Min(0f)]
        private float agentRadius = 0.38f;

        [SerializeField, Min(0f)]
        private float minimumAgentSeparation = 1.8f;

        [SerializeField, Min(0f)]
        private float minimumRepositionDistance = 1.5f;

        [SerializeField]
        private bool sampleAroundCurrentPosition;

        [SerializeField, Min(0f)]
        private float maximumLocalRepositionRadius = 4f;

        [SerializeField]
        private bool requireCameraViewport;

        [SerializeField, Min(0f)]
        private float distanceScoreWeight = 3f;

        [SerializeField, Min(0f)]
        private float travelScoreWeight = 0.45f;

        [SerializeField, Min(0f)]
        private float visibilityPenalty = 14f;

        [SerializeField, Min(0f)]
        private float crowdingScoreWeight = 1.5f;

        [SerializeField]
        private bool drawDebug = true;

        public float MinimumAttackDistance => minimumAttackDistance;
        public float PreferredAttackDistance => preferredAttackDistance;
        public float MaximumAttackDistance => maximumAttackDistance;
        public int AngularSamples => Mathf.Clamp(
            angularSamples,
            4,
            MaximumAngularSamples);
        public int HeightLayerCount => Mathf.Clamp(
            heightLayerOffsets == null ? 0 : heightLayerOffsets.Length,
            1,
            MaximumHeightLayers);
        public int RetryIntervalTicks => Mathf.Max(1, retryIntervalTicks);
        public float AgentRadius => Mathf.Max(0f, agentRadius);
        public float MinimumAgentSeparation => Mathf.Max(
            0f,
            minimumAgentSeparation);
        public float MinimumRepositionDistance => Mathf.Max(
            0f,
            minimumRepositionDistance);
        public bool SampleAroundCurrentPosition =>
            sampleAroundCurrentPosition;
        public float MaximumLocalRepositionRadius => Mathf.Max(
            0f,
            maximumLocalRepositionRadius);
        public bool RequireCameraViewport => requireCameraViewport;
        public bool DrawDebug => drawDebug;

        public float GetRingDistance(int ringIndex)
        {
            switch (ringIndex)
            {
                case 0:
                    return minimumAttackDistance;
                case 1:
                    return preferredAttackDistance;
                default:
                    return maximumAttackDistance;
            }
        }

        public float GetHeightLayerOffset(int layerIndex)
        {
            if (heightLayerOffsets == null
                || layerIndex < 0
                || layerIndex >= HeightLayerCount)
            {
                return 0f;
            }

            return heightLayerOffsets[layerIndex];
        }

        public bool IsAttackDistanceValid(float distance)
        {
            return distance >= minimumAttackDistance
                && distance <= maximumAttackDistance;
        }

        public bool TryValidate(out string error)
        {
            if (!IsFinitePositive(minimumAttackDistance)
                || !IsFinitePositive(preferredAttackDistance)
                || !IsFinitePositive(maximumAttackDistance)
                || minimumAttackDistance > preferredAttackDistance
                || preferredAttackDistance > maximumAttackDistance
                || angularSamples < 4
                || angularSamples > MaximumAngularSamples
                || heightLayerOffsets == null
                || heightLayerOffsets.Length == 0
                || heightLayerOffsets.Length > MaximumHeightLayers
                || retryIntervalTicks <= 0
                || !IsFiniteNonNegative(agentRadius)
                || !IsFiniteNonNegative(minimumAgentSeparation)
                || !IsFiniteNonNegative(minimumRepositionDistance)
                || !IsFiniteNonNegative(maximumLocalRepositionRadius)
                || !IsFiniteNonNegative(distanceScoreWeight)
                || !IsFiniteNonNegative(travelScoreWeight)
                || !IsFiniteNonNegative(visibilityPenalty)
                || !IsFiniteNonNegative(crowdingScoreWeight))
            {
                error = "Enemy position query has invalid distance, sampling or score values.";
                return false;
            }

            for (int index = 0; index < heightLayerOffsets.Length; index++)
            {
                if (float.IsNaN(heightLayerOffsets[index])
                    || float.IsInfinity(heightLayerOffsets[index]))
                {
                    error = "Enemy position query height layers must be finite.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        internal float Score(
            float targetDistance,
            float travelDistance,
            bool visible,
            float nearestAgentDistance,
            bool directPath)
        {
            float score = Mathf.Abs(targetDistance - preferredAttackDistance)
                * distanceScoreWeight
                + travelDistance * travelScoreWeight;
            if (!visible)
            {
                score += visibilityPenalty;
            }

            if (!float.IsNaN(nearestAgentDistance)
                && !float.IsInfinity(nearestAgentDistance)
                && nearestAgentDistance < minimumAgentSeparation * 2f)
            {
                score += (minimumAgentSeparation * 2f - nearestAgentDistance)
                    * crowdingScoreWeight;
            }

            if (!directPath)
            {
                score += 2f;
            }

            return score;
        }

        internal static void GenerateCandidate(
            Vector3 target,
            float distance,
            float heightOffset,
            int angularIndex,
            int angularCount,
            out Vector3 position)
        {
            float planarSquared = Mathf.Max(
                0f,
                distance * distance - heightOffset * heightOffset);
            float planarRadius = Mathf.Sqrt(planarSquared);
            float angle = angularIndex * (Mathf.PI * 2f / angularCount);
            position = target + new Vector3(
                Mathf.Cos(angle) * planarRadius,
                heightOffset,
                Mathf.Sin(angle) * planarRadius);
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
        }
    }

    /// <summary>
    /// Per-enemy fixed-capacity query state. Evaluation reuses its candidate
    /// buffer so fixed-tick planning does not allocate.
    /// </summary>
    public sealed class FpgEnemyPositionQueryRuntime
    {
        private readonly FpgEnemyPositionQueryDefinition definition;
        private readonly FpgEnemyPositionCandidate[] candidates =
            new FpgEnemyPositionCandidate[
                FpgEnemyPositionQueryDefinition.MaximumCandidateCount];

        public FpgEnemyPositionQueryRuntime(
            FpgEnemyPositionQueryDefinition definition)
        {
            this.definition = definition;
        }

        public int CandidateCount { get; private set; }
        internal FpgEnemyPositionQueryDefinition Definition => definition;

        public void ResetCandidates()
        {
            CandidateCount = 0;
        }

        public FpgEnemyPositionCandidate GetCandidate(int index)
        {
            if (index < 0 || index >= CandidateCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return candidates[index];
        }

        public bool TryFindBest(
            FpgRoomNavigationVolume volume,
            Vector3 origin,
            Vector3 target,
            Vector3[] agentPositions,
            bool[] agentActive,
            int agentCount,
            int ownAgentIndex,
            out Vector3 bestPosition)
        {
            return TryFindBest(
                volume,
                origin,
                target,
                agentPositions,
                agentActive,
                agentCount,
                ownAgentIndex,
                false,
                Vector3.zero,
                null,
                out bestPosition);
        }

        public bool TryFindBest(
            FpgRoomNavigationVolume volume,
            Vector3 origin,
            Vector3 target,
            Vector3[] agentPositions,
            bool[] agentActive,
            int agentCount,
            int ownAgentIndex,
            Camera worldCamera,
            out Vector3 bestPosition)
        {
            return TryFindBest(
                volume,
                origin,
                target,
                agentPositions,
                agentActive,
                agentCount,
                ownAgentIndex,
                false,
                Vector3.zero,
                worldCamera,
                out bestPosition);
        }

        public bool TryFindBest(
            FpgRoomNavigationVolume volume,
            Vector3 origin,
            Vector3 target,
            Vector3[] agentPositions,
            bool[] agentActive,
            int agentCount,
            int ownAgentIndex,
            bool hasAvoidPosition,
            Vector3 avoidPosition,
            out Vector3 bestPosition)
        {
            return TryFindBest(
                volume,
                origin,
                target,
                agentPositions,
                agentActive,
                agentCount,
                ownAgentIndex,
                hasAvoidPosition,
                avoidPosition,
                null,
                out bestPosition);
        }

        public bool TryFindBest(
            FpgRoomNavigationVolume volume,
            Vector3 origin,
            Vector3 target,
            Vector3[] agentPositions,
            bool[] agentActive,
            int agentCount,
            int ownAgentIndex,
            bool hasAvoidPosition,
            Vector3 avoidPosition,
            Camera worldCamera,
            out Vector3 bestPosition)
        {
            bestPosition = origin;
            CandidateCount = 0;
            if (definition == null
                || volume == null
                || !definition.TryValidate(out _))
            {
                return false;
            }

            bool enforceAvoidPosition = hasAvoidPosition
                && definition.MinimumRepositionDistance > 0f;
            Vector3 originTargetDelta = target - origin;
            if (definition.SampleAroundCurrentPosition)
            {
                originTargetDelta.y = 0f;
            }

            float originTargetDistance = originTargetDelta.magnitude;
            float bestScore = float.PositiveInfinity;
            bool found = false;
            int count = Mathf.Clamp(
                agentCount,
                0,
                agentPositions == null ? 0 : agentPositions.Length);
            int angularCount = definition.AngularSamples;
            for (int ring = 0; ring < 3; ring++)
            {
                float desiredDistance = definition.SampleAroundCurrentPosition
                    ? definition.MaximumLocalRepositionRadius
                        * (ring + 1) / 3f
                    : definition.GetRingDistance(ring);
                for (int layer = 0; layer < definition.HeightLayerCount; layer++)
                {
                    float height = definition.SampleAroundCurrentPosition
                        ? 0f
                        : definition.GetHeightLayerOffset(layer);
                    if ((definition.SampleAroundCurrentPosition && layer > 0)
                        || Mathf.Abs(height) > desiredDistance)
                    {
                        continue;
                    }

                    for (int angle = 0; angle < angularCount; angle++)
                    {
                        FpgEnemyPositionQueryDefinition.GenerateCandidate(
                            definition.SampleAroundCurrentPosition
                                ? origin
                                : target,
                            desiredDistance,
                            height,
                            angle,
                            angularCount,
                            out Vector3 candidate);
                        FpgEnemyPositionRejectionReason rejection =
                            FpgEnemyPositionRejectionReason.None;
                        Vector3 targetDelta = target - candidate;
                        if (definition.SampleAroundCurrentPosition)
                        {
                            targetDelta.y = 0f;
                        }

                        float targetDistance = targetDelta.magnitude;
                        if (definition.SampleAroundCurrentPosition
                            && !definition.IsAttackDistanceValid(targetDistance))
                        {
                            bool movesTowardAttackRange =
                                originTargetDistance
                                    > definition.MaximumAttackDistance
                                    && targetDistance < originTargetDistance - 0.01f;
                            bool movesAwayFromTooCloseTarget =
                                originTargetDistance
                                    < definition.MinimumAttackDistance
                                    && targetDistance > originTargetDistance + 0.01f;
                            if (!movesTowardAttackRange
                                && !movesAwayFromTooCloseTarget)
                            {
                                rejection =
                                    FpgEnemyPositionRejectionReason.OutsideAttackRange;
                            }
                        }
                        if (rejection == FpgEnemyPositionRejectionReason.None
                            && !volume.Contains(candidate, definition.AgentRadius))
                        {
                            rejection = FpgEnemyPositionRejectionReason.OutsideRoom;
                        }

                        if (rejection == FpgEnemyPositionRejectionReason.None
                            && (definition.SampleAroundCurrentPosition
                                ? !volume.CanOccupyGroundPosition(
                                    candidate,
                                    definition.AgentRadius)
                                : !volume.CanOccupy(
                                    candidate,
                                    definition.AgentRadius)))
                        {
                            rejection = FpgEnemyPositionRejectionReason.Obstacle;
                        }

                        if (rejection == FpgEnemyPositionRejectionReason.None
                            && enforceAvoidPosition
                            && Vector3.Distance(candidate, avoidPosition)
                                < definition.MinimumRepositionDistance)
                        {
                            rejection = FpgEnemyPositionRejectionReason.RecentPosition;
                        }

                        if (rejection == FpgEnemyPositionRejectionReason.None
                            && definition.RequireCameraViewport
                            && worldCamera != null
                            && !IsInsideCameraViewport(worldCamera, candidate))
                        {
                            rejection =
                                FpgEnemyPositionRejectionReason.OutsideCamera;
                        }

                        float nearestAgentDistance = float.PositiveInfinity;
                        if (rejection == FpgEnemyPositionRejectionReason.None)
                        {
                            for (int agent = 0; agent < count; agent++)
                            {
                                if (agent == ownAgentIndex
                                    || (agentActive != null
                                        && agent < agentActive.Length
                                        && !agentActive[agent]))
                                {
                                    continue;
                                }

                                nearestAgentDistance = Mathf.Min(
                                    nearestAgentDistance,
                                    Vector3.Distance(
                                        candidate,
                                        agentPositions[agent]));
                            }

                            if (nearestAgentDistance
                                < definition.MinimumAgentSeparation)
                            {
                                rejection = FpgEnemyPositionRejectionReason.Crowded;
                            }
                        }

                        bool directPath = definition.SampleAroundCurrentPosition
                            ? volume.HasClearGroundPathForPositionQuery(
                                origin,
                                candidate,
                                definition.AgentRadius)
                            : volume.HasClearPath(
                                origin,
                                candidate,
                                definition.AgentRadius);
                        if (rejection == FpgEnemyPositionRejectionReason.None
                            && !directPath
                            && !(definition.SampleAroundCurrentPosition
                                ? volume.HasReachableGroundPathForPositionQuery(
                                    origin,
                                    candidate,
                                    definition.AgentRadius)
                                : volume.HasReachablePath(
                                    origin,
                                    candidate,
                                    definition.AgentRadius)))
                        {
                            rejection = FpgEnemyPositionRejectionReason.Unreachable;
                        }

                        bool visible = volume.HasLineOfSight(candidate, target);
                        if (!definition.SampleAroundCurrentPosition)
                        {
                            targetDistance = Vector3.Distance(candidate, target);
                        }
                        float score = definition.Score(
                            targetDistance,
                            Vector3.Distance(origin, candidate),
                            visible,
                            nearestAgentDistance,
                            directPath);
                        if (CandidateCount < candidates.Length)
                        {
                            candidates[CandidateCount++] =
                                new FpgEnemyPositionCandidate(
                                    candidate,
                                    score,
                                    rejection,
                                    visible);
                        }

                        if (rejection != FpgEnemyPositionRejectionReason.None
                            || score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestPosition = candidate;
                        found = true;
                    }
                }
            }

            if (!found && enforceAvoidPosition)
            {
                // A small room may not contain a second valid attack point.
                // Preserve the original behavior as a deterministic fallback.
                return TryFindBest(
                    volume,
                    origin,
                    target,
                    agentPositions,
                    agentActive,
                    agentCount,
                    ownAgentIndex,
                    false,
                    Vector3.zero,
                    worldCamera,
                    out bestPosition);
            }

            return found;
        }

        private static bool IsInsideCameraViewport(
            Camera camera,
            Vector3 worldPosition)
        {
            if (camera == null)
            {
                return false;
            }

            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            return viewport.z >= camera.nearClipPlane
                && viewport.z <= camera.farClipPlane
                && viewport.x >= 0f
                && viewport.x <= 1f
                && viewport.y >= 0f
                && viewport.y <= 1f;
        }
    }
}
