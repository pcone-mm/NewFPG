using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Skills;

namespace FPG.Demo.Run
{
    public readonly struct ThreatScheduleEntry
    {
        public ThreatScheduleEntry(
            long scheduleSequence,
            TickIndex dueTick,
            int definitionId,
            TickDuration telegraphDuration,
            TickDuration windupDuration,
            TickDuration recoveryDuration,
            ThreatPayloadDefinition payload,
            ThreatRetryPolicy retryPolicy)
        {
            if (scheduleSequence <= 0L || !dueTick.IsValid || definitionId <= 0)
            {
                throw new ArgumentException("Threat schedule identity and due tick must be valid.");
            }

            if (!payload.IsValid || !Enum.IsDefined(typeof(ThreatRetryPolicy), retryPolicy))
            {
                throw new ArgumentException("Threat schedule payload and retry policy must be valid.");
            }

            ScheduleSequence = scheduleSequence;
            DueTick = dueTick;
            DefinitionId = definitionId;
            TelegraphDuration = telegraphDuration;
            WindupDuration = windupDuration;
            RecoveryDuration = recoveryDuration;
            Payload = payload;
            RetryPolicy = retryPolicy;
        }

        public long ScheduleSequence { get; }
        public TickIndex DueTick { get; }
        public int DefinitionId { get; }
        public TickDuration TelegraphDuration { get; }
        public TickDuration WindupDuration { get; }
        public TickDuration RecoveryDuration { get; }
        public ThreatPayloadDefinition Payload { get; }
        public ThreatRetryPolicy RetryPolicy { get; }
        public bool IsValid => ScheduleSequence > 0L
            && DueTick.IsValid
            && DefinitionId > 0
            && Payload.IsValid
            && Enum.IsDefined(typeof(ThreatRetryPolicy), RetryPolicy);

        public ThreatDefinition CreateThreatDefinition()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "An invalid threat schedule entry cannot create a runtime definition.");
            }

            return new ThreatDefinition(
                DefinitionId,
                TelegraphDuration,
                WindupDuration,
                RecoveryDuration,
                Payload);
        }

        public ulong AppendStableHash(ulong hash)
        {
            hash = StableHash.Append(hash, unchecked((ulong)ScheduleSequence));
            hash = StableHash.Append(hash, unchecked((ulong)DueTick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)DefinitionId));
            hash = StableHash.Append(hash, unchecked((ulong)TelegraphDuration.Value));
            hash = StableHash.Append(hash, unchecked((ulong)WindupDuration.Value));
            hash = StableHash.Append(hash, unchecked((ulong)RecoveryDuration.Value));
            hash = StableHash.Append(hash, (ulong)RetryPolicy);
            return Payload.AppendStableHash(hash);
        }
    }

    public readonly struct AimPoseSnapshot
    {
        public AimPoseSnapshot(
            TickIndex tick,
            SpatialVectorKey origin,
            SpatialVectorKey forward,
            SpatialVectorKey right,
            SpatialVectorKey up,
            long poseVersion)
        {
            if (!tick.IsValid)
            {
                throw new ArgumentException("Aim pose tick must be valid.", nameof(tick));
            }

            if (forward.IsZero || right.IsZero || up.IsZero)
            {
                throw new ArgumentException("Aim pose directions must be non-zero.");
            }

            if (poseVersion <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(poseVersion));
            }

            Tick = tick;
            Origin = origin;
            Forward = forward;
            Right = right;
            Up = up;
            PoseVersion = poseVersion;
        }

        public TickIndex Tick { get; }
        public SpatialVectorKey Origin { get; }
        public SpatialVectorKey Forward { get; }
        public SpatialVectorKey Right { get; }
        public SpatialVectorKey Up { get; }
        public long PoseVersion { get; }
        public bool IsValid => Tick.IsValid
            && !Forward.IsZero
            && !Right.IsZero
            && !Up.IsZero
            && PoseVersion > 0L;
    }

    public readonly struct BattleTickInput
    {
        public const int MaxEdgeCommandCount = 3;

        public BattleTickInput(PlayerInputFrame playerInput, AimPoseSnapshot aimPose)
        {
            if (playerInput.Tick != aimPose.Tick)
            {
                throw new ArgumentException("Player input and aim pose must use the same tick.");
            }

            if (playerInput.EdgeCommandCount < 0 || playerInput.EdgeCommandCount > MaxEdgeCommandCount)
            {
                throw new ArgumentOutOfRangeException(nameof(playerInput));
            }

            for (int index = 0; index < playerInput.EdgeCommandCount; index++)
            {
                InputEdgeCommand edge = playerInput.EdgeCommands[index];
                if (!edge.Sequence.IsValid || !Enum.IsDefined(typeof(InputEdgeType), edge.Type))
                {
                    throw new ArgumentException("Battle tick input edge commands must be valid.", nameof(playerInput));
                }
            }

            Tick = playerInput.Tick;
            AimHeld = playerInput.AimHeld;
            PrimaryHeld = playerInput.PrimaryHeld;
            SecondaryHeld = playerInput.SecondaryHeld;
            CancelSecondary = playerInput.CancelSecondary;
            AimPose = aimPose;
            EdgeCommandCount = playerInput.EdgeCommandCount;
            Edge0 = EdgeCommandCount > 0 ? playerInput.EdgeCommands[0] : default(InputEdgeCommand);
            Edge1 = EdgeCommandCount > 1 ? playerInput.EdgeCommands[1] : default(InputEdgeCommand);
            Edge2 = EdgeCommandCount > 2 ? playerInput.EdgeCommands[2] : default(InputEdgeCommand);
        }

        public TickIndex Tick { get; }
        public bool AimHeld { get; }
        public bool PrimaryHeld { get; }
        public bool SecondaryHeld { get; }
        public bool CancelSecondary { get; }
        public AimPoseSnapshot AimPose { get; }
        public int EdgeCommandCount { get; }
        public bool IsValid => Tick.IsValid && AimPose.IsValid && Tick == AimPose.Tick;
        private InputEdgeCommand Edge0 { get; }
        private InputEdgeCommand Edge1 { get; }
        private InputEdgeCommand Edge2 { get; }

        public InputEdgeCommand GetEdgeCommand(int index)
        {
            if (index < 0 || index >= EdgeCommandCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index == 0 ? Edge0 : index == 1 ? Edge1 : Edge2;
        }

        public PlayerInputFrame CopyToPlayerInputFrame(InputEdgeCommand[] edgeBuffer)
        {
            if (EdgeCommandCount > 0 && (edgeBuffer == null || edgeBuffer.Length < EdgeCommandCount))
            {
                throw new ArgumentException("The destination edge buffer is too small.", nameof(edgeBuffer));
            }

            for (int index = 0; index < EdgeCommandCount; index++)
            {
                edgeBuffer[index] = GetEdgeCommand(index);
            }

            return new PlayerInputFrame(
                Tick,
                AimHeld,
                PrimaryHeld,
                EdgeCommandCount == 0 ? null : edgeBuffer,
                EdgeCommandCount,
                CancelSecondary,
                SecondaryHeld);
        }
    }

    public interface IBattleTickInputSource
    {
        BattleTickInput GetTickInput(TickIndex tick);
    }

    public readonly struct AttackQueryRequest
    {
        public const int MaxPelletCount = WeaponDefinition.PrimaryPelletCount;

        public AttackQueryRequest(
            BattleTickInput tickInput,
            AttackSnapshot attack,
            PelletSample[] pellets,
            int pelletCount)
        {
            if (!tickInput.IsValid
                || !attack.AttackId.IsValid || !attack.ShotId.IsValid || !attack.OwnerId.IsValid
                || attack.PayloadCount <= 0 || attack.MaxImpactCount <= 0
                || attack.QueryPolicy != QueryPolicy.PelletRays && attack.QueryPolicy != QueryPolicy.DirectThenArea)
            {
                throw new ArgumentException("Attack query identity and policy must be valid.", nameof(attack));
            }

            if (tickInput.Tick != attack.ReleaseTick)
            {
                throw new ArgumentException("Attack release tick must match the frozen battle input tick.", nameof(attack));
            }

            if (pelletCount < 0 || pelletCount > MaxPelletCount
                || pellets == null && pelletCount != 0
                || pellets != null && pelletCount > pellets.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pelletCount));
            }

            if (attack.QueryPolicy == QueryPolicy.PelletRays && pelletCount != attack.PayloadCount
                || attack.QueryPolicy == QueryPolicy.DirectThenArea && pelletCount != 0)
            {
                throw new ArgumentException("Pellet count does not match the attack query policy.", nameof(pelletCount));
            }

            for (int index = 0; index < pelletCount; index++)
            {
                if (pellets[index].ShotId != attack.ShotId)
                {
                    throw new ArgumentException("Every pellet must belong to the attack shot.", nameof(pellets));
                }
            }

            TickInput = tickInput;
            Attack = attack;
            PelletCount = pelletCount;
            Pellet0 = pelletCount > 0 ? pellets[0] : default(PelletSample);
            Pellet1 = pelletCount > 1 ? pellets[1] : default(PelletSample);
            Pellet2 = pelletCount > 2 ? pellets[2] : default(PelletSample);
            Pellet3 = pelletCount > 3 ? pellets[3] : default(PelletSample);
            Pellet4 = pelletCount > 4 ? pellets[4] : default(PelletSample);
            Pellet5 = pelletCount > 5 ? pellets[5] : default(PelletSample);
            Pellet6 = pelletCount > 6 ? pellets[6] : default(PelletSample);
            Pellet7 = pelletCount > 7 ? pellets[7] : default(PelletSample);
        }

        public BattleTickInput TickInput { get; }
        public AttackSnapshot Attack { get; }
        public int PelletCount { get; }
        private PelletSample Pellet0 { get; }
        private PelletSample Pellet1 { get; }
        private PelletSample Pellet2 { get; }
        private PelletSample Pellet3 { get; }
        private PelletSample Pellet4 { get; }
        private PelletSample Pellet5 { get; }
        private PelletSample Pellet6 { get; }
        private PelletSample Pellet7 { get; }

        public PelletSample GetPellet(int index)
        {
            if (index < 0 || index >= PelletCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0: return Pellet0;
                case 1: return Pellet1;
                case 2: return Pellet2;
                case 3: return Pellet3;
                case 4: return Pellet4;
                case 5: return Pellet5;
                case 6: return Pellet6;
                default: return Pellet7;
            }
        }
    }

    public interface IAttackQueryPort
    {
        DomainResult Query(in AttackQueryRequest request, QueryCandidate[] output, out AttackQueryResult result);
    }

    public sealed class NullAttackQueryPort : IAttackQueryPort
    {
        public DomainResult Query(in AttackQueryRequest request, QueryCandidate[] output, out AttackQueryResult result)
        {
            result = AttackQueryResult.Empty;
            return DomainResult.Rejected(RejectReason.InvalidState);
        }
    }

    public sealed class EmptyAttackQueryPort : IAttackQueryPort
    {
        public DomainResult Query(in AttackQueryRequest request, QueryCandidate[] output, out AttackQueryResult result)
        {
            result = AttackQueryResult.Empty;
            return output == null
                ? DomainResult.Rejected(RejectReason.InvalidState)
                : DomainResult.Success;
        }
    }

    /// <summary>
    /// A deferred player projectile resolves its area at the terminal point,
    /// after the world sweep has decided where that terminal is.
    /// </summary>
    public readonly struct PlayerProjectileAreaQueryRequest
    {
        public PlayerProjectileAreaQueryRequest(
            TickIndex tick,
            AttackSnapshot attack,
            SpatialVectorKey center)
        {
            if (!tick.IsValid
                || !attack.AttackId.IsValid
                || !attack.ShotId.IsValid
                || !attack.OwnerId.IsValid
                || attack.Team != Team.Player
                || !attack.IsQueryConfigurationValid
                || attack.QueryPolicy != QueryPolicy.DirectThenArea
                || attack.QueryMode != AttackQueryMode.AreaAtFirstSurface
                || attack.PayloadCount != 1
                || attack.MaxImpactCount <= 0)
            {
                throw new ArgumentException(
                    "Player projectile area queries require a valid area attack.");
            }

            Tick = tick;
            Attack = attack;
            Center = center;
        }

        public TickIndex Tick { get; }
        public AttackSnapshot Attack { get; }
        public SpatialVectorKey Center { get; }
    }

    public interface IPlayerProjectileAreaQueryPort
    {
        DomainResult QueryAreaAtPoint(
            in PlayerProjectileAreaQueryRequest request,
            QueryCandidate[] output,
            out AttackQueryResult result);
    }

    public sealed class NullPlayerProjectileAreaQueryPort :
        IPlayerProjectileAreaQueryPort
    {
        public static readonly NullPlayerProjectileAreaQueryPort Instance =
            new NullPlayerProjectileAreaQueryPort();

        private NullPlayerProjectileAreaQueryPort()
        {
        }

        public DomainResult QueryAreaAtPoint(
            in PlayerProjectileAreaQueryRequest request,
            QueryCandidate[] output,
            out AttackQueryResult result)
        {
            result = AttackQueryResult.Empty;
            return DomainResult.Rejected(RejectReason.InvalidState);
        }
    }

    public enum ProjectileTargetingMode
    {
        LockedTarget = 0,
        FirstSurface
    }

    public readonly struct ProjectileSpawnRequest
    {
        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                FpgThreatPresentationKind.FastUninterceptable,
                ProjectileTargetingMode.LockedTarget,
                false,
                default(SpatialVectorKey),
                default(SpatialVectorKey))
        {
        }

        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            FpgThreatPresentationKind presentationKind)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                presentationKind,
                ProjectileTargetingMode.LockedTarget,
                false,
                default(SpatialVectorKey),
                default(SpatialVectorKey))
        {
        }

        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            SpatialVectorKey explicitStart,
            SpatialVectorKey explicitEnd)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                FpgThreatPresentationKind.FastUninterceptable,
                ProjectileTargetingMode.LockedTarget,
                true,
                explicitStart,
                explicitEnd)
        {
        }

        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            FpgThreatPresentationKind presentationKind,
            SpatialVectorKey explicitStart,
            SpatialVectorKey explicitEnd)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                presentationKind,
                ProjectileTargetingMode.LockedTarget,
                true,
                explicitStart,
                explicitEnd)
        {
        }

        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            FpgThreatPresentationKind presentationKind,
            ProjectileTargetingMode targetingMode,
            SpatialVectorKey explicitStart,
            SpatialVectorKey explicitEnd,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                presentationKind,
                targetingMode,
                true,
                explicitStart,
                explicitEnd)
        {
            if (!skillExecutionId.IsValid || gameplayEventId <= 0)
            {
                throw new ArgumentException(
                    "Projectile skill correlation must be valid.");
            }

            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
        }

        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            ProjectileTargetingMode targetingMode,
            SpatialVectorKey explicitStart,
            SpatialVectorKey explicitEnd)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                FpgThreatPresentationKind.FastUninterceptable,
                targetingMode,
                true,
                explicitStart,
                explicitEnd)
        {
        }

        public ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            FpgThreatPresentationKind presentationKind,
            ProjectileTargetingMode targetingMode,
            SpatialVectorKey explicitStart,
            SpatialVectorKey explicitEnd)
            : this(
                tick,
                arrivalTick,
                projectileId,
                runtimeId,
                attackId,
                ownerId,
                targetId,
                team,
                definitionId,
                sweepRadiusKey,
                interceptable,
                presentationKind,
                targetingMode,
                true,
                explicitStart,
                explicitEnd)
        {
        }

        private ProjectileSpawnRequest(
            TickIndex tick,
            TickIndex arrivalTick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            RuntimeId targetId,
            Team team,
            int definitionId,
            int sweepRadiusKey,
            bool interceptable,
            FpgThreatPresentationKind presentationKind,
            ProjectileTargetingMode targetingMode,
            bool hasExplicitPath,
            SpatialVectorKey explicitStart,
            SpatialVectorKey explicitEnd)
        {
            if (!tick.IsValid || !arrivalTick.IsValid || arrivalTick <= tick
                || !projectileId.IsValid || !runtimeId.IsValid || !attackId.IsValid
                || !ownerId.IsValid
                || (targetingMode == ProjectileTargetingMode.LockedTarget
                    && !targetId.IsValid))
            {
                throw new ArgumentException("Projectile registration identifiers must be valid.");
            }

            if (!Enum.IsDefined(typeof(Team), team) || team == Team.Neutral
                || !Enum.IsDefined(typeof(ProjectileTargetingMode), targetingMode)
                || !Enum.IsDefined(
                    typeof(FpgThreatPresentationKind),
                    presentationKind)
                || definitionId <= 0 || sweepRadiusKey <= 0
                || (hasExplicitPath && explicitStart == explicitEnd)
                || (targetingMode == ProjectileTargetingMode.FirstSurface
                    && (team != Team.Player || targetId.IsValid || interceptable
                        || !hasExplicitPath)))
            {
                throw new ArgumentOutOfRangeException(nameof(definitionId));
            }

            Tick = tick;
            ArrivalTick = arrivalTick;
            ProjectileId = projectileId;
            RuntimeId = runtimeId;
            AttackId = attackId;
            OwnerId = ownerId;
            TargetId = targetId;
            Team = team;
            DefinitionId = definitionId;
            SweepRadiusKey = sweepRadiusKey;
            Interceptable = interceptable;
            PresentationKind = presentationKind;
            TargetingMode = targetingMode;
            HasExplicitPath = hasExplicitPath;
            ExplicitStart = explicitStart;
            ExplicitEnd = explicitEnd;
            SkillExecutionId = FPG.Demo.Skills.SkillExecutionId.Invalid;
            GameplayEventId = 0;
        }

        public TickIndex Tick { get; }
        public TickIndex ArrivalTick { get; }
        public ProjectileId ProjectileId { get; }
        public RuntimeId RuntimeId { get; }
        public AttackId AttackId { get; }
        public RuntimeId OwnerId { get; }
        public RuntimeId TargetId { get; }
        public Team Team { get; }
        public int DefinitionId { get; }
        public int SweepRadiusKey { get; }
        public bool Interceptable { get; }
        public FpgThreatPresentationKind PresentationKind { get; }
        public ProjectileTargetingMode TargetingMode { get; }
        public bool HasExplicitPath { get; }
        public SpatialVectorKey ExplicitStart { get; }
        public SpatialVectorKey ExplicitEnd { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public int GameplayEventId { get; }
        public bool HasSkillCorrelation => SkillExecutionId.IsValid
            && GameplayEventId > 0;
    }
    public readonly struct ProjectilePathSnapshot
    {
        public ProjectilePathSnapshot(
            ProjectileId projectileId,
            RuntimeId runtimeId,
            TickIndex spawnTick,
            TickIndex arrivalTick,
            SpatialVectorKey start,
            SpatialVectorKey end)
        {
            if (!projectileId.IsValid || !runtimeId.IsValid || !spawnTick.IsValid || !arrivalTick.IsValid || arrivalTick <= spawnTick)
            {
                throw new ArgumentException("Projectile path identifiers and ticks must be valid.");
            }

            ProjectileId = projectileId;
            RuntimeId = runtimeId;
            SpawnTick = spawnTick;
            ArrivalTick = arrivalTick;
            Start = start;
            End = end;
        }

        public ProjectileId ProjectileId { get; }
        public RuntimeId RuntimeId { get; }
        public TickIndex SpawnTick { get; }
        public TickIndex ArrivalTick { get; }
        public SpatialVectorKey Start { get; }
        public SpatialVectorKey End { get; }

        public bool Matches(in ProjectileSpawnRequest request)
        {
            return ProjectileId == request.ProjectileId
                && RuntimeId == request.RuntimeId
                && SpawnTick == request.Tick
                && ArrivalTick == request.ArrivalTick
                && (!request.HasExplicitPath
                    || (Start == request.ExplicitStart
                        && End == request.ExplicitEnd));
        }

        public SpatialVectorKey PositionAtTick(TickIndex tick)
        {
            if (!tick.IsValid || tick <= SpawnTick)
            {
                return Start;
            }

            if (tick >= ArrivalTick)
            {
                return End;
            }

            long elapsed = tick - SpawnTick;
            long duration = ArrivalTick - SpawnTick;
            return new SpatialVectorKey(
                Interpolate(Start.X, End.X, elapsed, duration),
                Interpolate(Start.Y, End.Y, elapsed, duration),
                Interpolate(Start.Z, End.Z, elapsed, duration));
        }

        public DomainResult TryGetSegment(
            TickIndex tick,
            out SpatialVectorKey from,
            out SpatialVectorKey to)
        {
            from = default(SpatialVectorKey);
            to = default(SpatialVectorKey);
            if (!tick.IsValid || tick <= SpawnTick || tick > ArrivalTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            from = PositionAtTick(new TickIndex(tick.Value - 1L));
            to = PositionAtTick(tick);
            return DomainResult.Success;
        }

        private static int Interpolate(int start, int end, long elapsed, long duration)
        {
            long delta = (long)end - start;
            long offset = delta * elapsed / duration;
            return checked((int)(start + offset));
        }
    }

    public readonly struct ProjectileSweepRequest
    {
        public ProjectileSweepRequest(
            TickIndex tick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            SpatialVectorKey from,
            SpatialVectorKey to,
            int sweepRadiusKey)
        {
            if (!tick.IsValid || !projectileId.IsValid || !runtimeId.IsValid || sweepRadiusKey <= 0)
            {
                throw new ArgumentException("Projectile sweep identifiers and radius must be valid.");
            }

            Tick = tick;
            ProjectileId = projectileId;
            RuntimeId = runtimeId;
            From = from;
            To = to;
            SweepRadiusKey = sweepRadiusKey;
        }

        public TickIndex Tick { get; }
        public ProjectileId ProjectileId { get; }
        public RuntimeId RuntimeId { get; }
        public SpatialVectorKey From { get; }
        public SpatialVectorKey To { get; }
        public int SweepRadiusKey { get; }
    }

    public enum ProjectileSweepHitKind
    {
        None = 0,
        Target,
        EnvironmentBlocked
    }

    public readonly struct ProjectileSweepHit
    {
        private ProjectileSweepHit(
            ProjectileSweepHitKind kind,
            RuntimeId targetId,
            HitPart hitPart,
            GeometryId geometryId,
            int distanceKey,
            SpatialVectorKey point)
        {
            Kind = kind;
            TargetId = targetId;
            HitPart = hitPart;
            GeometryId = geometryId;
            DistanceKey = distanceKey;
            Point = point;
        }

        public ProjectileSweepHitKind Kind { get; }
        public RuntimeId TargetId { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }
        public int DistanceKey { get; }
        public SpatialVectorKey Point { get; }
        public bool IsValid
        {
            get
            {
                if (!Enum.IsDefined(typeof(ProjectileSweepHitKind), Kind)
                    || !Enum.IsDefined(typeof(FPG.Demo.Combat.HitPart), HitPart)
                    || DistanceKey < 0)
                {
                    return false;
                }

                if (Kind == ProjectileSweepHitKind.None)
                {
                    return !TargetId.IsValid && !GeometryId.IsValid;
                }

                if (!GeometryId.IsValid)
                {
                    return false;
                }

                return Kind == ProjectileSweepHitKind.Target
                    ? TargetId.IsValid
                    : !TargetId.IsValid;
            }
        }

        public static ProjectileSweepHit None => default(ProjectileSweepHit);

        public static ProjectileSweepHit Target(
            RuntimeId targetId,
            HitPart hitPart,
            GeometryId geometryId,
            int distanceKey,
            SpatialVectorKey point)
        {
            if (!targetId.IsValid || !geometryId.IsValid || distanceKey < 0
                || !Enum.IsDefined(typeof(HitPart), hitPart))
            {
                throw new ArgumentException("Target sweep hits require stable target, geometry and distance keys.");
            }

            return new ProjectileSweepHit(
                ProjectileSweepHitKind.Target,
                targetId,
                hitPart,
                geometryId,
                distanceKey,
                point);
        }

        public static ProjectileSweepHit EnvironmentBlocked(GeometryId geometryId, int distanceKey, SpatialVectorKey point)
        {
            if (!geometryId.IsValid || distanceKey < 0)
            {
                throw new ArgumentException("Environment hits require stable geometry and distance keys.");
            }

            return new ProjectileSweepHit(
                ProjectileSweepHitKind.EnvironmentBlocked,
                RuntimeId.Invalid,
                HitPart.Body,
                geometryId,
                distanceKey,
                point);
        }
    }

    public readonly struct ProjectileReleaseRequest
    {
        public ProjectileReleaseRequest(
            TickIndex tick,
            ProjectileId projectileId,
            RuntimeId runtimeId,
            ProjectileTerminalReason reason)
        {
            if (!tick.IsValid || !projectileId.IsValid || !runtimeId.IsValid
                || !Enum.IsDefined(typeof(ProjectileTerminalReason), reason)
                || reason == ProjectileTerminalReason.None)
            {
                throw new ArgumentException("Projectile release fields must be valid.");
            }

            Tick = tick;
            ProjectileId = projectileId;
            RuntimeId = runtimeId;
            Reason = reason;
        }

        public TickIndex Tick { get; }
        public ProjectileId ProjectileId { get; }
        public RuntimeId RuntimeId { get; }
        public ProjectileTerminalReason Reason { get; }
    }

    public interface IProjectileWorldPort
    {
        DomainResult Register(in ProjectileSpawnRequest request, out ProjectilePathSnapshot path);
        DomainResult Sweep(in ProjectileSweepRequest request, out ProjectileSweepHit hit);
        DomainResult Release(in ProjectileReleaseRequest request);
    }

    public sealed class NullProjectileWorldPort : IProjectileWorldPort
    {
        public DomainResult Register(in ProjectileSpawnRequest request, out ProjectilePathSnapshot path)
        {
            path = default(ProjectilePathSnapshot);
            return DomainResult.Rejected(RejectReason.InvalidState);
        }

        public DomainResult Sweep(in ProjectileSweepRequest request, out ProjectileSweepHit hit)
        {
            hit = ProjectileSweepHit.None;
            return DomainResult.Rejected(RejectReason.InvalidState);
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            return DomainResult.Rejected(RejectReason.InvalidState);
        }
    }
}
