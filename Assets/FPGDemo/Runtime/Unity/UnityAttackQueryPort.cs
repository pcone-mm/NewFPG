using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public struct UnityAttackQuerySettings
    {
        [SerializeField, Min(0.01f)]
        private float maxDistance;

        [SerializeField, Min(0f)]
        private float primarySpreadTangent;

        [SerializeField, Min(0.01f)]
        private float secondaryAreaRadius;

        [SerializeField]
        private int hitboxLayerMask;

        [SerializeField]
        private int blockerLayerMask;

        public UnityAttackQuerySettings(
            float maxDistance,
            float primarySpreadTangent,
            float secondaryAreaRadius,
            int hitboxLayerMask,
            int blockerLayerMask)
        {
            this.maxDistance = maxDistance;
            this.primarySpreadTangent = primarySpreadTangent;
            this.secondaryAreaRadius = secondaryAreaRadius;
            this.hitboxLayerMask = hitboxLayerMask;
            this.blockerLayerMask = blockerLayerMask;

            if (!IsValid)
            {
                throw new ArgumentException("Attack query settings require finite distances and separate non-empty hitbox/blocker layer masks.");
            }
        }

        public float MaxDistance => maxDistance;
        public float PrimarySpreadTangent => primarySpreadTangent;
        public float SecondaryAreaRadius => secondaryAreaRadius;
        public int HitboxLayerMask => hitboxLayerMask;
        public int BlockerLayerMask => blockerLayerMask;
        public int PhysicsLayerMask => hitboxLayerMask | blockerLayerMask;
        public bool IsValid => IsFinite(maxDistance) && maxDistance > 0f
            && IsFinite(primarySpreadTangent) && primarySpreadTangent >= 0f
            && IsFinite(secondaryAreaRadius) && secondaryAreaRadius > 0f
            && hitboxLayerMask != 0
            && blockerLayerMask != 0
            && (hitboxLayerMask & blockerLayerMask) == 0;

        public static UnityAttackQuerySettings Default => new UnityAttackQuerySettings(
            50f,
            0.04f,
            3f,
            1 << 29,
            1 << 28);

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class UnityAttackQueryPort : IAttackQueryPort
    {
        private const int UInt24Max = 0xFFFFFF;

        private readonly HitboxRegistry registry;
        private readonly IFpgFormalHitboxLookup formalHitboxLookup;
        private readonly UnityAttackQuerySettings settings;
        private readonly IUnityPhysicsQueryBackend physics;
        private readonly IPlayerShotQueryCaptureSink playerShotCaptureSink;
        private readonly UnityPhysicsHit[] hitBuffer;
        private readonly Collider[] overlapBuffer;
        private readonly QueryCandidate[] canonicalBuffer;

        public UnityAttackQueryPort(
            HitboxRegistry registry,
            UnityAttackQuerySettings settings,
            IUnityPhysicsQueryBackend physics = null,
            IPlayerShotQueryCaptureSink playerShotCaptureSink = null)
            : this(
                registry ?? throw new ArgumentNullException(nameof(registry)),
                null,
                settings,
                physics,
                playerShotCaptureSink)
        {
        }

        public UnityAttackQueryPort(
            IFpgFormalHitboxLookup formalHitboxLookup,
            UnityAttackQuerySettings settings,
            IUnityPhysicsQueryBackend physics = null,
            IPlayerShotQueryCaptureSink playerShotCaptureSink = null)
            : this(
                null,
                formalHitboxLookup ?? throw new ArgumentNullException(nameof(formalHitboxLookup)),
                settings,
                physics,
                playerShotCaptureSink)
        {
        }

        private UnityAttackQueryPort(
            HitboxRegistry registry,
            IFpgFormalHitboxLookup formalHitboxLookup,
            UnityAttackQuerySettings settings,
            IUnityPhysicsQueryBackend physics,
            IPlayerShotQueryCaptureSink playerShotCaptureSink)
        {
            if (registry == null && formalHitboxLookup == null)
            {
                throw new ArgumentNullException(
                    registry == null ? nameof(registry) : nameof(formalHitboxLookup));
            }

            if (registry != null && formalHitboxLookup != null)
            {
                throw new ArgumentException("Attack query port accepts exactly one hitbox lookup.");
            }

            if (!settings.IsValid)
            {
                throw new ArgumentException("Attack query settings are invalid.", nameof(settings));
            }

            this.registry = registry;
            this.formalHitboxLookup = formalHitboxLookup;
            this.settings = settings;
            this.physics = physics ?? new UnityPhysicsQueryBackend();
            this.playerShotCaptureSink = playerShotCaptureSink;
            if (this.physics.Capacity < SpatialContract.AttackQueryCandidateCapacity)
            {
                throw new ArgumentException("The Physics backend capacity is below the spatial query contract capacity.", nameof(physics));
            }

            hitBuffer = new UnityPhysicsHit[SpatialContract.AttackQueryCandidateCapacity];
            overlapBuffer = new Collider[SpatialContract.AttackQueryCandidateCapacity];
            canonicalBuffer = new QueryCandidate[SpatialContract.AttackQueryCandidateCapacity];
        }

        private bool IsHitboxLookupReady => registry != null
            ? registry.IsReadyForQueries
            : formalHitboxLookup != null;

        /// <summary>
        /// Best-effort count of failures while copying successful physics-query
        /// data into the non-authoritative player-shot presentation bridge.
        /// This counter never affects a query result or combat transaction.
        /// </summary>
        public int PresentationCaptureFaultCount { get; private set; }

        public DomainResult Query(
            in AttackQueryRequest request,
            QueryCandidate[] output,
            out AttackQueryResult result)
        {
            result = AttackQueryResult.Empty;
            if (output == null || !IsHitboxLookupReady
                || !settings.IsValid || !IsRequestValid(request))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            physics.SyncTransforms();
            int candidateCount = 0;
            DomainResult queried;
            if (request.Attack.QueryPolicy == QueryPolicy.PelletRays)
            {
                queried = QueryPellets(request, ref candidateCount, out int droppedCandidateCount);
                if (!queried.IsSuccess)
                {
                    result = new AttackQueryResult(0, droppedCandidateCount);
                    return queried;
                }
            }
            else
            {
                queried = QueryDirectThenArea(request, ref candidateCount, out int droppedCandidateCount);
                if (!queried.IsSuccess)
                {
                    result = new AttackQueryResult(0, droppedCandidateCount);
                    return queried;
                }
            }

            if (candidateCount > output.Length)
            {
                result = new AttackQueryResult(0, candidateCount - output.Length);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Canonicalize(candidateCount);
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = canonicalBuffer[index];
                output[index] = new QueryCandidate(
                    candidate.QueryStage,
                    candidate.SampleIndex,
                    candidate.TargetId,
                    candidate.TargetKind,
                    candidate.HitPart,
                    candidate.GeometryId,
                    candidate.DistanceKey,
                    candidate.ImpactPointKey,
                    index);
            }

            result = new AttackQueryResult(candidateCount, 0);
            TryCapturePlayerShotPresentation(request, candidateCount);
            return DomainResult.Success;
        }

        private void TryCapturePlayerShotPresentation(
            in AttackQueryRequest request,
            int candidateCount)
        {
            if (playerShotCaptureSink == null)
            {
                return;
            }

            try
            {
                if (!TryBuildPlayerShotCapture(request, candidateCount, out PlayerShotQueryCapture capture)
                    || !playerShotCaptureSink.TryCaptureSuccessfulQuery(capture))
                {
                    PresentationCaptureFaultCount++;
                }
            }
            catch (Exception)
            {
                PresentationCaptureFaultCount++;
            }
        }

        private bool TryBuildPlayerShotCapture(
            in AttackQueryRequest request,
            int candidateCount,
            out PlayerShotQueryCapture capture)
        {
            capture = default(PlayerShotQueryCapture);
            if (!TryGetAimBasis(request, out Vector3 origin, out Vector3 forward, out Vector3 right, out Vector3 up)
                || candidateCount < 0 || candidateCount > canonicalBuffer.Length)
            {
                return false;
            }

            SpatialVectorKey originKey = request.TickInput.AimPose.Origin;
            if (request.Attack.QueryPolicy == QueryPolicy.PelletRays)
            {
                capture = new PlayerShotQueryCapture(
                    request,
                    request.PelletCount,
                    SpatialVectorKey.Zero,
                    0);
                for (int pelletIndex = 0; pelletIndex < request.PelletCount; pelletIndex++)
                {
                    PelletSample pellet = request.GetPellet(pelletIndex);
                    if (!TryGetPelletDirection(pellet, pelletIndex, forward, right, up, out Vector3 direction)
                        || !TryQuantizePosition(
                            origin + direction * settings.MaxDistance,
                            out SpatialVectorKey rangeEnd))
                    {
                        return false;
                    }

                    PlayerShotTrajectory trajectory;
                    if (TryFindNearestCandidate(
                            AttackQueryStage.Pellet,
                            pelletIndex,
                            candidateCount,
                            out QueryCandidate terminal))
                    {
                        trajectory = CreateTrajectory(
                            pelletIndex,
                            originKey,
                            terminal.ImpactPointKey,
                            terminal);
                    }
                    else
                    {
                        trajectory = new PlayerShotTrajectory(
                            pelletIndex,
                            originKey,
                            rangeEnd,
                            PlayerShotTerminalKind.Miss,
                            RuntimeId.Invalid,
                            HitPart.Body,
                            GeometryId.Invalid);
                    }

                    capture.SetTrajectory(pelletIndex, trajectory);
                }

                return true;
            }

            if (request.Attack.QueryPolicy != QueryPolicy.DirectThenArea
                || !TryQuantizePosition(
                    origin + forward * settings.MaxDistance,
                    out SpatialVectorKey directRangeEnd)
                || !TryQuantizeDistance(settings.SecondaryAreaRadius, out int areaRadiusKey))
            {
                return false;
            }

            SpatialVectorKey areaCenter = directRangeEnd;
            PlayerShotTrajectory directTrajectory;
            if (TryFindNearestCandidate(
                    AttackQueryStage.Direct,
                    -1,
                    candidateCount,
                    out QueryCandidate directTerminal))
            {
                areaCenter = directTerminal.ImpactPointKey;
                directTrajectory = CreateTrajectory(
                    -1,
                    originKey,
                    areaCenter,
                    directTerminal);
            }
            else
            {
                directTrajectory = new PlayerShotTrajectory(
                    -1,
                    originKey,
                    directRangeEnd,
                    PlayerShotTerminalKind.Miss,
                    RuntimeId.Invalid,
                    HitPart.Body,
                    GeometryId.Invalid);
            }

            capture = new PlayerShotQueryCapture(
                request,
                trajectoryCount: 1,
                areaCenter,
                areaRadiusKey);
            capture.SetTrajectory(0, directTrajectory);
            return true;
        }

        private bool TryFindNearestCandidate(
            AttackQueryStage stage,
            int sampleIndex,
            int candidateCount,
            out QueryCandidate candidate)
        {
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate current = canonicalBuffer[index];
                if (current.QueryStage == stage && current.SampleIndex == sampleIndex)
                {
                    candidate = current;
                    return true;
                }
            }

            candidate = default(QueryCandidate);
            return false;
        }

        private static PlayerShotTrajectory CreateTrajectory(
            int sampleIndex,
            SpatialVectorKey origin,
            SpatialVectorKey terminalPoint,
            in QueryCandidate candidate)
        {
            PlayerShotTerminalKind terminalKind = candidate.TargetKind == QueryTargetKind.EnvironmentBlocker
                ? PlayerShotTerminalKind.EnvironmentBlocker
                : candidate.TargetKind == QueryTargetKind.Projectile
                    ? PlayerShotTerminalKind.Projectile
                    : PlayerShotTerminalKind.Combatant;
            return new PlayerShotTrajectory(
                sampleIndex,
                origin,
                terminalPoint,
                terminalKind,
                candidate.TargetId,
                candidate.HitPart,
                candidate.GeometryId);
        }

        private DomainResult QueryPellets(
            in AttackQueryRequest request,
            ref int candidateCount,
            out int droppedCandidateCount)
        {
            droppedCandidateCount = 0;
            if (!TryGetAimBasis(request, out Vector3 origin, out Vector3 forward, out Vector3 right, out Vector3 up))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            for (int pelletIndex = 0; pelletIndex < request.PelletCount; pelletIndex++)
            {
                PelletSample pellet = request.GetPellet(pelletIndex);
                if (!TryGetPelletDirection(pellet, pelletIndex, forward, right, up, out Vector3 direction))
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }
                NonAllocPhysicsQueryResult batch = physics.RaycastNonAlloc(
                    origin,
                    direction,
                    hitBuffer,
                    settings.MaxDistance,
                    settings.PhysicsLayerMask,
                    QueryTriggerInteraction.Collide);
                DomainResult batchValidation = ValidateBatch(batch, hitBuffer.Length, out droppedCandidateCount);
                if (!batchValidation.IsSuccess)
                {
                    return batchValidation;
                }

                for (int hitIndex = 0; hitIndex < batch.Count; hitIndex++)
                {
                    if (TryCreateRayCandidate(
                        hitBuffer[hitIndex],
                        request.Attack,
                        AttackQueryStage.Pellet,
                        pelletIndex,
                        out QueryCandidate candidate))
                    {
                        DomainResult appended = TryAppend(candidate, ref candidateCount, out droppedCandidateCount);
                        if (!appended.IsSuccess)
                        {
                            return appended;
                        }
                    }
                }
            }

            return DomainResult.Success;
        }

        private DomainResult QueryDirectThenArea(
            in AttackQueryRequest request,
            ref int candidateCount,
            out int droppedCandidateCount)
        {
            droppedCandidateCount = 0;
            if (!TryGetAimBasis(request, out Vector3 origin, out Vector3 direction, out _, out _))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }
            NonAllocPhysicsQueryResult directBatch = physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                settings.MaxDistance,
                settings.PhysicsLayerMask,
                QueryTriggerInteraction.Collide);
            DomainResult directValidation = ValidateBatch(
                directBatch,
                hitBuffer.Length,
                out droppedCandidateCount);
            if (!directValidation.IsSuccess)
            {
                return directValidation;
            }

            Vector3 areaCenter = origin + direction * settings.MaxDistance;
            bool hasDirectAnchor = false;
            QueryCandidate directAnchor = default(QueryCandidate);
            for (int hitIndex = 0; hitIndex < directBatch.Count; hitIndex++)
            {
                UnityPhysicsHit hit = hitBuffer[hitIndex];
                if (!TryCreateRayCandidate(
                    hit,
                    request.Attack,
                    AttackQueryStage.Direct,
                    -1,
                    out QueryCandidate candidate))
                {
                    continue;
                }

                if (!hasDirectAnchor || CompareCanonical(candidate, directAnchor) < 0)
                {
                    directAnchor = candidate;
                    areaCenter = hit.Point;
                    hasDirectAnchor = true;
                }
            }

            if (hasDirectAnchor)
            {
                for (int hitIndex = 0; hitIndex < directBatch.Count; hitIndex++)
                {
                    if (!TryCreateRayCandidate(
                        hitBuffer[hitIndex],
                        request.Attack,
                        AttackQueryStage.Direct,
                        -1,
                        out QueryCandidate candidate)
                        || candidate.DistanceKey != directAnchor.DistanceKey)
                    {
                        continue;
                    }

                    DomainResult appended = TryAppend(
                        candidate,
                        ref candidateCount,
                        out droppedCandidateCount);
                    if (!appended.IsSuccess)
                    {
                        return appended;
                    }
                }
            }

            NonAllocPhysicsQueryResult areaBatch = physics.OverlapSphereNonAlloc(
                areaCenter,
                settings.SecondaryAreaRadius,
                overlapBuffer,
                settings.PhysicsLayerMask,
                QueryTriggerInteraction.Collide);
            DomainResult areaValidation = ValidateBatch(
                areaBatch,
                overlapBuffer.Length,
                out droppedCandidateCount);
            if (!areaValidation.IsSuccess)
            {
                return areaValidation;
            }

            for (int colliderIndex = 0; colliderIndex < areaBatch.Count; colliderIndex++)
            {
                if (TryCreateAreaCandidate(
                    overlapBuffer[colliderIndex],
                    areaCenter,
                    request.Attack,
                    out QueryCandidate candidate))
                {
                    DomainResult appended = TryAppend(candidate, ref candidateCount, out droppedCandidateCount);
                    if (!appended.IsSuccess)
                    {
                        return appended;
                    }
                }
            }

            return DomainResult.Success;
        }

        private bool TryCreateRayCandidate(
            in UnityPhysicsHit hit,
            in AttackSnapshot attack,
            AttackQueryStage stage,
            int sampleIndex,
            out QueryCandidate candidate)
        {
            candidate = default(QueryCandidate);
            if (!TryResolveEligible(hit.Collider, attack, out RegisteredHitbox registered)
                || !IsFinite(hit.Point) || !IsFinite(hit.Distance) || hit.Distance < 0f
                || !TryQuantizeDistance(hit.Distance, out int distanceKey)
                || !TryQuantizePosition(hit.Point, out SpatialVectorKey pointKey))
            {
                return false;
            }

            candidate = new QueryCandidate(
                stage,
                sampleIndex,
                registered.RuntimeId,
                registered.TargetKind,
                registered.HitPart,
                registered.GeometryId,
                distanceKey,
                pointKey,
                0);
            return true;
        }

        private bool TryCreateAreaCandidate(
            Collider collider,
            Vector3 areaCenter,
            in AttackSnapshot attack,
            out QueryCandidate candidate)
        {
            candidate = default(QueryCandidate);
            if (!TryResolveEligible(collider, attack, out RegisteredHitbox registered))
            {
                return false;
            }

            Vector3 point = collider.ClosestPoint(areaCenter);
            float distance = Vector3.Distance(areaCenter, point);
            if (!IsFinite(point) || !IsFinite(distance)
                || !TryQuantizeDistance(distance, out int distanceKey)
                || !TryQuantizePosition(point, out SpatialVectorKey pointKey))
            {
                return false;
            }

            candidate = new QueryCandidate(
                AttackQueryStage.Area,
                -1,
                registered.RuntimeId,
                registered.TargetKind,
                registered.HitPart,
                registered.GeometryId,
                distanceKey,
                pointKey,
                0);
            return true;
        }

        private bool TryResolveEligible(
            Collider collider,
            in AttackSnapshot attack,
            out RegisteredHitbox registered)
        {
            registered = default(RegisteredHitbox);
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
                || !TryResolveRegisteredHitbox(collider, out RegisteredHitbox candidate)
                || !IsLayerIncluded(collider.gameObject.layer, candidate.TargetKind)
                || collider.isTrigger && !candidate.AllowTrigger)
            {
                return false;
            }

            if (candidate.TargetKind != QueryTargetKind.EnvironmentBlocker
                && (candidate.RuntimeId == attack.OwnerId || candidate.Team == attack.Team))
            {
                return false;
            }

            registered = candidate;
            return true;
        }

        private bool TryResolveRegisteredHitbox(
            Collider collider,
            out RegisteredHitbox registered)
        {
            if (registry != null)
            {
                return registry.TryResolve(collider, out registered);
            }

            if (formalHitboxLookup != null)
            {
                return formalHitboxLookup.TryResolve(collider, out registered);
            }

            registered = default(RegisteredHitbox);
            return false;
        }

        private DomainResult TryAppend(
            in QueryCandidate candidate,
            ref int candidateCount,
            out int droppedCandidateCount)
        {
            droppedCandidateCount = 0;
            if (candidateCount >= canonicalBuffer.Length)
            {
                droppedCandidateCount = 1;
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            canonicalBuffer[candidateCount++] = candidate;
            return DomainResult.Success;
        }

        private static DomainResult ValidateBatch(
            in NonAllocPhysicsQueryResult batch,
            int capacity,
            out int droppedCandidateCount)
        {
            droppedCandidateCount = 0;
            if (batch.Count < 0 || batch.Count > capacity)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (batch.MayBeTruncated || batch.Count >= capacity)
            {
                droppedCandidateCount = 1;
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            return DomainResult.Success;
        }

        private void Canonicalize(int candidateCount)
        {
            for (int index = 1; index < candidateCount; index++)
            {
                QueryCandidate candidate = canonicalBuffer[index];
                int insertionIndex = index;
                while (insertionIndex > 0
                    && CompareCanonical(candidate, canonicalBuffer[insertionIndex - 1]) < 0)
                {
                    canonicalBuffer[insertionIndex] = canonicalBuffer[insertionIndex - 1];
                    insertionIndex--;
                }

                canonicalBuffer[insertionIndex] = candidate;
            }
        }

        private static int CompareCanonical(in QueryCandidate left, in QueryCandidate right)
        {
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

            int runtime = left.TargetId.CompareTo(right.TargetId);
            if (runtime != 0)
            {
                return runtime;
            }

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

            int kind = left.TargetKind.CompareTo(right.TargetKind);
            if (kind != 0)
            {
                return kind;
            }

            int hitPart = left.HitPart.CompareTo(right.HitPart);
            if (hitPart != 0)
            {
                return hitPart;
            }

            int pointX = left.ImpactPointKey.X.CompareTo(right.ImpactPointKey.X);
            if (pointX != 0)
            {
                return pointX;
            }

            int pointY = left.ImpactPointKey.Y.CompareTo(right.ImpactPointKey.Y);
            return pointY != 0
                ? pointY
                : left.ImpactPointKey.Z.CompareTo(right.ImpactPointKey.Z);
        }

        private bool IsLayerIncluded(int layer, QueryTargetKind targetKind)
        {
            int expectedMask = targetKind == QueryTargetKind.EnvironmentBlocker
                ? settings.BlockerLayerMask
                : settings.HitboxLayerMask;
            return layer >= 0 && layer < 32
                && (expectedMask & (1 << layer)) != 0;
        }

        private static bool IsRequestValid(in AttackQueryRequest request)
        {
            return request.TickInput.IsValid
                && request.Attack.AttackId.IsValid
                && request.Attack.ShotId.IsValid
                && request.Attack.OwnerId.IsValid
                && request.Attack.ReleaseTick == request.TickInput.Tick
                && request.Attack.Team != Team.Neutral
                && (request.Attack.QueryPolicy == QueryPolicy.PelletRays
                    && request.PelletCount == request.Attack.PayloadCount
                    || request.Attack.QueryPolicy == QueryPolicy.DirectThenArea
                    && request.PelletCount == 0);
        }

        private static Vector3 ToPosition(SpatialVectorKey key)
        {
            float scale = 1f / SpatialContract.PositionUnitsPerMeter;
            return new Vector3(key.X * scale, key.Y * scale, key.Z * scale);
        }

        private static Vector3 ToDirection(SpatialVectorKey key)
        {
            float scale = 1f / SpatialContract.DirectionUnits;
            return new Vector3(key.X * scale, key.Y * scale, key.Z * scale);
        }

        private static float ToSignedUnit(int uint24)
        {
            return (float)(uint24 * (2.0 / UInt24Max) - 1.0);
        }

        private static void MapConcentricDisk(
            float squareX,
            float squareY,
            out float diskX,
            out float diskY)
        {
            if (squareX == 0f && squareY == 0f)
            {
                diskX = 0f;
                diskY = 0f;
                return;
            }

            double radius;
            double theta;
            if (Math.Abs(squareX) > Math.Abs(squareY))
            {
                radius = squareX;
                theta = Math.PI * 0.25 * (squareY / squareX);
            }
            else
            {
                radius = squareY;
                theta = Math.PI * 0.5 - Math.PI * 0.25 * (squareX / squareY);
            }

            diskX = (float)(radius * Math.Cos(theta));
            diskY = (float)(radius * Math.Sin(theta));
        }

        private static bool TryGetAimBasis(
            in AttackQueryRequest request,
            out Vector3 origin,
            out Vector3 forward,
            out Vector3 right,
            out Vector3 up)
        {
            origin = ToPosition(request.TickInput.AimPose.Origin);
            forward = ToDirection(request.TickInput.AimPose.Forward);
            right = ToDirection(request.TickInput.AimPose.Right);
            up = ToDirection(request.TickInput.AimPose.Up);
            if (!IsFinite(origin) || !IsUsableDirection(forward)
                || !IsUsableDirection(right) || !IsUsableDirection(up))
            {
                return false;
            }

            forward.Normalize();
            right.Normalize();
            up.Normalize();
            return true;
        }

        private bool TryGetPelletDirection(
            in PelletSample pellet,
            int expectedPelletIndex,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            out Vector3 direction)
        {
            direction = Vector3.zero;
            if (pellet.PelletIndex != expectedPelletIndex
                || pellet.SpreadU24 < 0 || pellet.SpreadU24 > UInt24Max
                || pellet.SpreadV24 < 0 || pellet.SpreadV24 > UInt24Max)
            {
                return false;
            }

            MapConcentricDisk(
                ToSignedUnit(pellet.SpreadU24),
                ToSignedUnit(pellet.SpreadV24),
                out float diskU,
                out float diskV);
            float spreadU = diskU * settings.PrimarySpreadTangent;
            float spreadV = diskV * settings.PrimarySpreadTangent;
            direction = forward + right * spreadU + up * spreadV;
            if (!IsUsableDirection(direction))
            {
                return false;
            }

            direction.Normalize();
            return true;
        }

        private static bool TryQuantizeDistance(float distance, out int key)
        {
            return TryQuantize(distance, SpatialContract.DistanceUnitsPerMeter, out key);
        }

        private static bool TryQuantizePosition(Vector3 position, out SpatialVectorKey key)
        {
            key = default(SpatialVectorKey);
            if (!TryQuantize(position.x, SpatialContract.PositionUnitsPerMeter, out int x)
                || !TryQuantize(position.y, SpatialContract.PositionUnitsPerMeter, out int y)
                || !TryQuantize(position.z, SpatialContract.PositionUnitsPerMeter, out int z))
            {
                return false;
            }

            key = new SpatialVectorKey(x, y, z);
            return true;
        }

        private static bool TryQuantize(float value, int units, out int key)
        {
            double scaled = value * (double)units;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled)
                || scaled > int.MaxValue || scaled < int.MinValue)
            {
                key = 0;
                return false;
            }

            key = checked((int)Math.Round(scaled, MidpointRounding.AwayFromZero));
            return true;
        }

        private static bool IsUsableDirection(Vector3 value)
        {
            return IsFinite(value) && value.sqrMagnitude > 0.0000001f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}




