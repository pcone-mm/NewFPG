using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    public sealed class SpatialPortTranscript : ISpatialDigestView
    {
        private const ulong AttackQueryConfigurationExtensionTag = 0x5152595F4D4F4445UL;

        private readonly Operation[] operations;
        private readonly QueryCandidate[] queryCandidates;
        private readonly int operationCapacity;
        private int operationCount;
        private int queryCandidateCount;
        private int replayCursor;
        private int reservedProjectileReleaseCount;
        private ulong digest;

        public SpatialPortTranscript(int operationCapacity, int queryCandidateCapacity)
        {
            if (operationCapacity <= 0 || queryCandidateCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationCapacity));
            }

            this.operationCapacity = operationCapacity;
            operations = new Operation[checked(operationCapacity + 1)];
            queryCandidates = new QueryCandidate[queryCandidateCapacity];
            digest = CreateInitialDigest();
        }

        public int Capacity => operationCapacity;
        public int StorageCapacity => operations.Length;
        public int QueryCandidateCapacity => queryCandidates.Length;
        public int Count => operationCount;
        public int ReservedProjectileReleaseCount => reservedProjectileReleaseCount;
        public int ContractVersion => SpatialContract.Version;
        public ulong CanonicalDigest => digest;
        public int ReplayRemaining => operationCount - replayCursor;

        public DomainResult TryRecordAttackQuery(
            in AttackQueryRequest request,
            DomainResult portResult,
            in AttackQueryResult queryResult,
            QueryCandidate[] candidates)
        {
            if (!CanAppendOperation())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            int candidateCount = portResult.IsSuccess ? queryResult.CandidateCount : 0;
            if (portResult.IsSuccess)
            {
                if (candidates == null
                    || candidateCount < 0
                    || candidateCount > candidates.Length)
                {
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                if (queryCandidateCount + candidateCount > queryCandidates.Length)
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                for (int index = 0; index < candidateCount; index++)
                {
                    if (!candidates[index].IsValid)
                    {
                        return DomainResult.Rejected(RejectReason.InvalidState);
                    }
                }
            }

            int candidateOffset = queryCandidateCount;
            for (int index = 0; index < candidateCount; index++)
            {
                queryCandidates[queryCandidateCount++] = candidates[index];
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.AttackQuery,
                RequestHash = ComputeAttackQueryRequestHash(request),
                ResultIsSuccess = portResult.IsSuccess,
                RejectReason = portResult.RejectReason,
                QueryResult = queryResult,
                QueryCandidateOffset = candidateOffset,
                QueryCandidateCount = candidateCount
            };
            operation.PayloadHash = ComputeAttackQueryPayloadHash(operation, candidateOffset, candidateCount);
            Commit(operation);
            return DomainResult.Success;
        }

        public DomainResult TryRecordProjectileRegister(
            in ProjectileSpawnRequest request,
            DomainResult portResult,
            in ProjectilePathSnapshot path)
        {
            if (!CanAppendOperation())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileRegister,
                RequestHash = ComputeProjectileSpawnRequestHash(request),
                ResultIsSuccess = portResult.IsSuccess,
                RejectReason = portResult.RejectReason,
                Path = path
            };
            operation.PayloadHash = AppendPath(StableHash.Mix(0x4650475F50415448UL), path);
            Commit(operation);
            return DomainResult.Success;
        }

        public DomainResult TryRecordProjectileSweep(
            in ProjectileSweepRequest request,
            DomainResult portResult,
            in ProjectileSweepHit hit)
        {
            if (!CanAppendOperation())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            if (portResult.IsSuccess && !hit.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileSweep,
                RequestHash = ComputeProjectileSweepRequestHash(request),
                ResultIsSuccess = portResult.IsSuccess,
                RejectReason = portResult.RejectReason,
                SweepHit = hit
            };
            operation.PayloadHash = AppendSweepHit(StableHash.Mix(0x4650475F53574545UL), hit);
            Commit(operation);
            return DomainResult.Success;
        }

        public DomainResult TryRecordProjectileRelease(
            in ProjectileReleaseRequest request,
            DomainResult portResult)
        {
            if (!CanAppendOperation())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileRelease,
                RequestHash = ComputeProjectileReleaseRequestHash(request),
                ResultIsSuccess = portResult.IsSuccess,
                RejectReason = portResult.RejectReason,
                PayloadHash = StableHash.Mix(0x4650475F52454C53UL)
            };
            Commit(operation);
            return DomainResult.Success;
        }

        internal DomainResult TryRecordReservedProjectileRelease(
            in ProjectileReleaseRequest request,
            DomainResult portResult)
        {
            if (reservedProjectileReleaseCount <= 0
                || operationCount >= operations.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileRelease,
                RequestHash = ComputeProjectileReleaseRequestHash(request),
                ResultIsSuccess = portResult.IsSuccess,
                RejectReason = portResult.RejectReason,
                PayloadHash = StableHash.Mix(0x4650475F52454C53UL)
            };
            Commit(operation);
            reservedProjectileReleaseCount--;
            return DomainResult.Success;
        }

        internal DomainResult TryRecordProjectileReleaseFailure(
            in ProjectileReleaseRequest request,
            DomainResult portResult)
        {
            if (portResult.IsSuccess
                || reservedProjectileReleaseCount <= 0
                || !CanAppendTerminalCapacityFailure())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileRelease,
                RequestHash = ComputeProjectileReleaseRequestHash(request),
                ResultIsSuccess = false,
                RejectReason = portResult.RejectReason,
                PayloadHash = StableHash.Mix(0x4650475F52454C53UL)
            };
            Commit(operation);
            return DomainResult.Success;
        }

        internal DomainResult TryRecordTerminalAttackQueryCapacityFailure(
            in AttackQueryRequest request)
        {
            if (!CanAppendTerminalCapacityFailure())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.AttackQuery,
                RequestHash = ComputeAttackQueryRequestHash(request),
                ResultIsSuccess = false,
                RejectReason = RejectReason.BufferCapacity,
                QueryResult = AttackQueryResult.Empty,
                QueryCandidateOffset = queryCandidateCount,
                QueryCandidateCount = 0
            };
            operation.PayloadHash = ComputeAttackQueryPayloadHash(operation, queryCandidateCount, 0);
            Commit(operation);
            return DomainResult.Success;
        }

        internal DomainResult TryRecordTerminalProjectileRegisterCapacityFailure(
            in ProjectileSpawnRequest request)
        {
            if (!CanAppendTerminalCapacityFailure())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileRegister,
                RequestHash = ComputeProjectileSpawnRequestHash(request),
                ResultIsSuccess = false,
                RejectReason = RejectReason.BufferCapacity,
                Path = default(ProjectilePathSnapshot)
            };
            operation.PayloadHash = AppendPath(
                StableHash.Mix(0x4650475F50415448UL),
                operation.Path);
            Commit(operation);
            return DomainResult.Success;
        }

        internal DomainResult TryRecordTerminalProjectileSweepCapacityFailure(
            in ProjectileSweepRequest request)
        {
            if (!CanAppendTerminalCapacityFailure())
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            Operation operation = new Operation
            {
                Kind = SpatialDecisionKind.ProjectileSweep,
                RequestHash = ComputeProjectileSweepRequestHash(request),
                ResultIsSuccess = false,
                RejectReason = RejectReason.BufferCapacity,
                SweepHit = ProjectileSweepHit.None
            };
            operation.PayloadHash = AppendSweepHit(
                StableHash.Mix(0x4650475F53574545UL),
                operation.SweepHit);
            Commit(operation);
            return DomainResult.Success;
        }

        public void ResetReplay()
        {
            replayCursor = 0;
        }

        internal DomainResult ReplayAttackQuery(
            in AttackQueryRequest request,
            QueryCandidate[] output,
            out AttackQueryResult result)
        {
            result = AttackQueryResult.Empty;
            if (!TryPeekExpected(
                SpatialDecisionKind.AttackQuery,
                ComputeAttackQueryRequestHash(request),
                out Operation operation))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (!operation.ResultIsSuccess)
            {
                replayCursor++;
                result = operation.QueryResult;
                return DomainResult.Rejected(operation.RejectReason);
            }

            if (output == null || output.Length < operation.QueryCandidateCount)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            for (int index = 0; index < operation.QueryCandidateCount; index++)
            {
                output[index] = queryCandidates[operation.QueryCandidateOffset + index];
            }

            replayCursor++;
            result = operation.QueryResult;
            return DomainResult.Success;
        }

        internal DomainResult ReplayProjectileRegister(
            in ProjectileSpawnRequest request,
            out ProjectilePathSnapshot path)
        {
            path = default(ProjectilePathSnapshot);
            if (!TryPeekExpected(
                SpatialDecisionKind.ProjectileRegister,
                ComputeProjectileSpawnRequestHash(request),
                out Operation operation))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            replayCursor++;
            path = operation.Path;
            return RestoreResult(operation);
        }

        internal DomainResult ReplayProjectileSweep(
            in ProjectileSweepRequest request,
            out ProjectileSweepHit hit)
        {
            hit = ProjectileSweepHit.None;
            if (!TryPeekExpected(
                SpatialDecisionKind.ProjectileSweep,
                ComputeProjectileSweepRequestHash(request),
                out Operation operation))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            replayCursor++;
            hit = operation.SweepHit;
            return RestoreResult(operation);
        }

        internal DomainResult ReplayProjectileRelease(in ProjectileReleaseRequest request)
        {
            if (!TryPeekExpected(
                SpatialDecisionKind.ProjectileRelease,
                ComputeProjectileReleaseRequestHash(request),
                out Operation operation))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            replayCursor++;
            return RestoreResult(operation);
        }

        private bool CanAppendOperation()
        {
            return operationCount + reservedProjectileReleaseCount < operationCapacity;
        }

        private bool CanAppendTerminalCapacityFailure()
        {
            return operationCount + reservedProjectileReleaseCount < operations.Length;
        }

        internal DomainResult ValidateCanRecordOperation()
        {
            return CanAppendOperation()
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.BufferCapacity);
        }

        internal DomainResult TryReserveProjectileLifecycle()
        {
            if ((long)operationCount + reservedProjectileReleaseCount + 2L
                > operationCapacity)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            reservedProjectileReleaseCount++;
            return DomainResult.Success;
        }

        internal void CancelProjectileReleaseReservation()
        {
            if (reservedProjectileReleaseCount <= 0)
            {
                throw new InvalidOperationException(
                    "Projectile release reservation accounting is inconsistent.");
            }

            reservedProjectileReleaseCount--;
        }

        internal DomainResult ValidateCanRecordReservedProjectileRelease()
        {
            return reservedProjectileReleaseCount > 0
                && operationCount < operations.Length
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.BufferCapacity);
        }

        private void Commit(Operation operation)
        {
            operation.Sequence = operationCount + 1L;
            operations[operationCount++] = operation;
            digest = StableHash.Append(digest, unchecked((ulong)operation.Sequence));
            digest = StableHash.Append(digest, (ulong)operation.Kind);
            digest = StableHash.Append(digest, operation.RequestHash);
            digest = StableHash.Append(digest, operation.ResultIsSuccess ? 1UL : 0UL);
            digest = StableHash.Append(digest, (ulong)operation.RejectReason);
            digest = StableHash.Append(digest, operation.PayloadHash);
        }

        private bool TryPeekExpected(
            SpatialDecisionKind kind,
            ulong requestHash,
            out Operation operation)
        {
            operation = default(Operation);
            if (replayCursor < 0 || replayCursor >= operationCount)
            {
                return false;
            }

            operation = operations[replayCursor];
            return operation.Kind == kind && operation.RequestHash == requestHash;
        }

        private static DomainResult RestoreResult(Operation operation)
        {
            return operation.ResultIsSuccess
                ? DomainResult.Success
                : DomainResult.Rejected(operation.RejectReason);
        }

        private ulong ComputeAttackQueryPayloadHash(Operation operation, int offset, int count)
        {
            ulong hash = StableHash.Mix(0x4650475F51525950UL);
            hash = StableHash.Append(hash, unchecked((ulong)operation.QueryResult.CandidateCount));
            hash = StableHash.Append(hash, unchecked((ulong)operation.QueryResult.DroppedCandidateCount));
            for (int index = 0; index < count; index++)
            {
                hash = AppendCandidate(hash, queryCandidates[offset + index]);
            }

            return hash;
        }

        private static ulong ComputeAttackQueryRequestHash(in AttackQueryRequest request)
        {
            ulong hash = StableHash.Mix(0x4650475F51525952UL);
            BattleTickInput input = request.TickInput;
            hash = StableHash.Append(hash, unchecked((ulong)input.Tick.Value));
            hash = StableHash.Append(hash, input.AimHeld ? 1UL : 0UL);
            hash = StableHash.Append(hash, input.PrimaryHeld ? 1UL : 0UL);
            hash = StableHash.Append(hash, input.SecondaryHeld ? 1UL : 0UL);
            hash = AppendAimPose(hash, input.AimPose);
            hash = StableHash.Append(hash, unchecked((ulong)input.EdgeCommandCount));
            for (int index = 0; index < input.EdgeCommandCount; index++)
            {
                InputEdgeCommand edge = input.GetEdgeCommand(index);
                hash = StableHash.Append(hash, unchecked((ulong)edge.Sequence.Value));
                hash = StableHash.Append(hash, (ulong)edge.Type);
            }

            AttackSnapshot attack = request.Attack;
            hash = StableHash.Append(hash, unchecked((ulong)attack.AttackId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)attack.ShotId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)attack.DefinitionId));
            hash = StableHash.Append(hash, unchecked((ulong)attack.OwnerId.Value));
            hash = StableHash.Append(hash, (ulong)attack.Team);
            hash = StableHash.Append(hash, unchecked((ulong)attack.ReleaseTick.Value));
            hash = AppendDamage(hash, attack.DamageSpec);
            hash = StableHash.Append(hash, (ulong)attack.QueryPolicy);
            hash = StableHash.Append(hash, unchecked((ulong)attack.PayloadCount));
            hash = StableHash.Append(hash, unchecked((ulong)attack.MaxImpactCount));
            hash = StableHash.Append(hash, unchecked((ulong)attack.AmmoCost));
            hash = StableHash.Append(hash, unchecked((ulong)attack.RngVersion));
            if (attack.QueryMode != AttackQueryMode.Legacy
                || attack.AdditionalPenetrationCount != 0
                || attack.AreaCombatantLimit != 0
                || attack.AreaProjectileLimit != 0
                || attack.AllowedTargetKinds != AttackSnapshot.DefaultAllowedTargetKinds)
            {
                hash = StableHash.Append(hash, AttackQueryConfigurationExtensionTag);
                hash = StableHash.Append(hash, (ulong)attack.QueryMode);
                hash = StableHash.Append(hash, unchecked((ulong)attack.AdditionalPenetrationCount));
                hash = StableHash.Append(hash, unchecked((ulong)attack.AreaCombatantLimit));
                hash = StableHash.Append(hash, unchecked((ulong)attack.AreaProjectileLimit));
                hash = StableHash.Append(hash, (ulong)attack.AllowedTargetKinds);
            }

            hash = StableHash.Append(hash, unchecked((ulong)request.PelletCount));
            for (int index = 0; index < request.PelletCount; index++)
            {
                PelletSample pellet = request.GetPellet(index);
                hash = StableHash.Append(hash, unchecked((ulong)pellet.ShotId.Value));
                hash = StableHash.Append(hash, unchecked((ulong)pellet.PelletIndex));
                hash = StableHash.Append(hash, unchecked((ulong)pellet.SpreadU24));
                hash = StableHash.Append(hash, unchecked((ulong)pellet.SpreadV24));
            }

            return hash;
        }

        private static ulong ComputeProjectileSpawnRequestHash(in ProjectileSpawnRequest request)
        {
            ulong hash = StableHash.Mix(0x4650475F5053504EUL);
            hash = StableHash.Append(hash, unchecked((ulong)request.Tick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.ArrivalTick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.ProjectileId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.RuntimeId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.AttackId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.OwnerId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.TargetId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)(int)request.Team));
            hash = StableHash.Append(hash, unchecked((ulong)request.DefinitionId));
            hash = StableHash.Append(hash, unchecked((ulong)request.SweepRadiusKey));
            hash = StableHash.Append(hash, unchecked((ulong)request.PresentationKey));
            hash = StableHash.Append(hash, request.Interceptable ? 1UL : 0UL);
            if (request.HasExplicitPath)
            {
                hash = StableHash.Append(hash, 1UL);
                hash = StableHash.Append(hash, unchecked((ulong)request.ExplicitStart.X));
                hash = StableHash.Append(hash, unchecked((ulong)request.ExplicitStart.Y));
                hash = StableHash.Append(hash, unchecked((ulong)request.ExplicitStart.Z));
                hash = StableHash.Append(hash, unchecked((ulong)request.ExplicitEnd.X));
                hash = StableHash.Append(hash, unchecked((ulong)request.ExplicitEnd.Y));
                hash = StableHash.Append(hash, unchecked((ulong)request.ExplicitEnd.Z));
            }

            return hash;
        }
        private static ulong ComputeProjectileSweepRequestHash(in ProjectileSweepRequest request)
        {
            ulong hash = StableHash.Mix(0x4650475F50535751UL);
            hash = StableHash.Append(hash, unchecked((ulong)request.Tick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.ProjectileId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.RuntimeId.Value));
            hash = AppendVector(hash, request.From);
            hash = AppendVector(hash, request.To);
            return StableHash.Append(hash, unchecked((ulong)request.SweepRadiusKey));
        }

        private static ulong ComputeProjectileReleaseRequestHash(in ProjectileReleaseRequest request)
        {
            ulong hash = StableHash.Mix(0x4650475F50524C51UL);
            hash = StableHash.Append(hash, unchecked((ulong)request.Tick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.ProjectileId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)request.RuntimeId.Value));
            return StableHash.Append(hash, (ulong)request.Reason);
        }

        private static ulong AppendAimPose(ulong hash, AimPoseSnapshot pose)
        {
            hash = StableHash.Append(hash, unchecked((ulong)pose.Tick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)pose.PoseVersion));
            hash = AppendVector(hash, pose.Origin);
            hash = AppendVector(hash, pose.Forward);
            hash = AppendVector(hash, pose.Right);
            return AppendVector(hash, pose.Up);
        }

        private static ulong AppendCandidate(ulong hash, QueryCandidate candidate)
        {
            hash = StableHash.Append(hash, (ulong)candidate.QueryStage);
            hash = StableHash.Append(hash, unchecked((ulong)candidate.SampleIndex));
            hash = StableHash.Append(hash, unchecked((ulong)candidate.TargetId.Value));
            hash = StableHash.Append(hash, (ulong)candidate.TargetKind);
            hash = StableHash.Append(hash, (ulong)candidate.HitPart);
            hash = StableHash.Append(hash, unchecked((ulong)candidate.GeometryId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)candidate.DistanceKey));
            hash = AppendVector(hash, candidate.ImpactPointKey);
            return StableHash.Append(hash, unchecked((ulong)candidate.QueryOrdinal));
        }

        private static ulong AppendPath(ulong hash, ProjectilePathSnapshot path)
        {
            hash = StableHash.Append(hash, unchecked((ulong)path.ProjectileId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)path.RuntimeId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)path.SpawnTick.Value));
            hash = StableHash.Append(hash, unchecked((ulong)path.ArrivalTick.Value));
            hash = AppendVector(hash, path.Start);
            return AppendVector(hash, path.End);
        }

        private static ulong AppendSweepHit(ulong hash, ProjectileSweepHit hit)
        {
            hash = StableHash.Append(hash, (ulong)hit.Kind);
            hash = StableHash.Append(hash, unchecked((ulong)hit.TargetId.Value));
            hash = StableHash.Append(hash, (ulong)hit.HitPart);
            hash = StableHash.Append(hash, unchecked((ulong)hit.GeometryId.Value));
            hash = StableHash.Append(hash, unchecked((ulong)hit.DistanceKey));
            return AppendVector(hash, hit.Point);
        }

        private static ulong AppendDamage(ulong hash, DamageSpec damage)
        {
            hash = StableHash.Append(hash, unchecked((ulong)damage.BaseDamage));
            hash = StableHash.Append(hash, unchecked((ulong)damage.BreakDamage));
            hash = StableHash.Append(hash, unchecked((ulong)damage.WeakpointDamageMultiplierBasisPoints));
            return StableHash.Append(hash, unchecked((ulong)damage.WeakpointBreakMultiplierBasisPoints));
        }

        private static ulong AppendVector(ulong hash, SpatialVectorKey vector)
        {
            hash = StableHash.Append(hash, unchecked((ulong)vector.X));
            hash = StableHash.Append(hash, unchecked((ulong)vector.Y));
            return StableHash.Append(hash, unchecked((ulong)vector.Z));
        }

        private static ulong CreateInitialDigest()
        {
            ulong hash = StableHash.Mix(0x4650475F53505254UL);
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.Version));
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.PositionUnitsPerMeter));
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.DirectionUnits));
            hash = StableHash.Append(hash, unchecked((ulong)SpatialContract.DistanceUnitsPerMeter));
            return StableHash.Append(
                hash,
                unchecked((ulong)SpatialContract.AttackQueryCandidateCapacity));
        }

        private struct Operation
        {
            public long Sequence;
            public SpatialDecisionKind Kind;
            public ulong RequestHash;
            public bool ResultIsSuccess;
            public RejectReason RejectReason;
            public ulong PayloadHash;
            public AttackQueryResult QueryResult;
            public int QueryCandidateOffset;
            public int QueryCandidateCount;
            public ProjectilePathSnapshot Path;
            public ProjectileSweepHit SweepHit;
        }
    }

    public sealed class RecordingAttackQueryPort : IAttackQueryPort
    {
        private readonly IAttackQueryPort inner;
        private readonly SpatialPortTranscript transcript;

        public RecordingAttackQueryPort(IAttackQueryPort inner, SpatialPortTranscript transcript)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        }

        public DomainResult Query(in AttackQueryRequest request, QueryCandidate[] output, out AttackQueryResult result)
        {
            DomainResult capacity = transcript.ValidateCanRecordOperation();
            if (!capacity.IsSuccess)
            {
                result = AttackQueryResult.Empty;
                DomainResult recordedFailure =
                    transcript.TryRecordTerminalAttackQueryCapacityFailure(request);
                return recordedFailure.IsSuccess ? capacity : recordedFailure;
            }

            DomainResult portResult = inner.Query(request, output, out result);
            DomainResult recorded = transcript.TryRecordAttackQuery(request, portResult, result, output);
            if (!recorded.IsSuccess && recorded.RejectReason == RejectReason.BufferCapacity)
            {
                result = AttackQueryResult.Empty;
                DomainResult recordedFailure =
                    transcript.TryRecordTerminalAttackQueryCapacityFailure(request);
                return recordedFailure.IsSuccess ? recorded : recordedFailure;
            }

            return recorded.IsSuccess ? portResult : recorded;
        }
    }

    public sealed class RecordingProjectileWorldPort : IProjectileWorldPort
    {
        private readonly IProjectileWorldPort inner;
        private readonly SpatialPortTranscript transcript;
        private readonly ReleaseReservation[] releaseReservations;

        public RecordingProjectileWorldPort(IProjectileWorldPort inner, SpatialPortTranscript transcript)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
            releaseReservations = new ReleaseReservation[transcript.Capacity];
        }

        public DomainResult Register(in ProjectileSpawnRequest request, out ProjectilePathSnapshot path)
        {
            path = default(ProjectilePathSnapshot);
            if (FindReservation(request.ProjectileId, request.RuntimeId) >= 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int reservationSlot = FindFreeReservation();
            if (reservationSlot < 0)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            DomainResult capacity = transcript.TryReserveProjectileLifecycle();
            if (!capacity.IsSuccess)
            {
                DomainResult recordedFailure =
                    transcript.TryRecordTerminalProjectileRegisterCapacityFailure(request);
                return recordedFailure.IsSuccess ? capacity : recordedFailure;
            }

            DomainResult portResult = inner.Register(request, out path);
            DomainResult recorded = transcript.TryRecordProjectileRegister(request, portResult, path);
            if (!recorded.IsSuccess)
            {
                if (portResult.IsSuccess)
                {
                    inner.Release(new ProjectileReleaseRequest(
                        request.Tick,
                        request.ProjectileId,
                        request.RuntimeId,
                        ProjectileTerminalReason.OwnerCanceled));
                }

                transcript.CancelProjectileReleaseReservation();
                return recorded;
            }

            if (!portResult.IsSuccess)
            {
                transcript.CancelProjectileReleaseReservation();
                return portResult;
            }

            releaseReservations[reservationSlot] = new ReleaseReservation(
                request.ProjectileId,
                request.RuntimeId);
            return portResult;
        }

        public DomainResult Sweep(in ProjectileSweepRequest request, out ProjectileSweepHit hit)
        {
            if (FindReservation(request.ProjectileId, request.RuntimeId) < 0)
            {
                hit = ProjectileSweepHit.None;
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            DomainResult capacity = transcript.ValidateCanRecordOperation();
            if (!capacity.IsSuccess)
            {
                hit = ProjectileSweepHit.None;
                DomainResult recordedFailure =
                    transcript.TryRecordTerminalProjectileSweepCapacityFailure(request);
                return recordedFailure.IsSuccess ? capacity : recordedFailure;
            }

            DomainResult portResult = inner.Sweep(request, out hit);
            DomainResult recorded = transcript.TryRecordProjectileSweep(request, portResult, hit);
            return recorded.IsSuccess ? portResult : recorded;
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            int reservationSlot = FindReservation(request.ProjectileId, request.RuntimeId);
            if (reservationSlot < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            DomainResult capacity = transcript.ValidateCanRecordReservedProjectileRelease();
            if (!capacity.IsSuccess)
            {
                return capacity;
            }

            DomainResult portResult = inner.Release(request);
            DomainResult recorded = portResult.IsSuccess
                ? transcript.TryRecordReservedProjectileRelease(request, portResult)
                : transcript.TryRecordProjectileReleaseFailure(request, portResult);
            if (recorded.IsSuccess && portResult.IsSuccess)
            {
                releaseReservations[reservationSlot] = default(ReleaseReservation);
            }

            return recorded.IsSuccess ? portResult : recorded;
        }

        private int FindReservation(ProjectileId projectileId, RuntimeId runtimeId)
        {
            for (int index = 0; index < releaseReservations.Length; index++)
            {
                ReleaseReservation reservation = releaseReservations[index];
                if (reservation.Active
                    && reservation.ProjectileId == projectileId
                    && reservation.RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeReservation()
        {
            for (int index = 0; index < releaseReservations.Length; index++)
            {
                if (!releaseReservations[index].Active)
                {
                    return index;
                }
            }

            return -1;
        }

        private readonly struct ReleaseReservation
        {
            public ReleaseReservation(ProjectileId projectileId, RuntimeId runtimeId)
            {
                ProjectileId = projectileId;
                RuntimeId = runtimeId;
                Active = true;
            }

            public ProjectileId ProjectileId { get; }
            public RuntimeId RuntimeId { get; }
            public bool Active { get; }
        }
    }

    public sealed class ReplayAttackQueryPort : IAttackQueryPort
    {
        private readonly SpatialPortTranscript transcript;

        public ReplayAttackQueryPort(SpatialPortTranscript transcript)
        {
            this.transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        }

        public DomainResult Query(in AttackQueryRequest request, QueryCandidate[] output, out AttackQueryResult result)
        {
            return transcript.ReplayAttackQuery(request, output, out result);
        }
    }

    public sealed class ReplayProjectileWorldPort : IProjectileWorldPort
    {
        private readonly SpatialPortTranscript transcript;

        public ReplayProjectileWorldPort(SpatialPortTranscript transcript)
        {
            this.transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        }

        public DomainResult Register(in ProjectileSpawnRequest request, out ProjectilePathSnapshot path)
        {
            return transcript.ReplayProjectileRegister(request, out path);
        }

        public DomainResult Sweep(in ProjectileSweepRequest request, out ProjectileSweepHit hit)
        {
            return transcript.ReplayProjectileSweep(request, out hit);
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            return transcript.ReplayProjectileRelease(request);
        }
    }
}
