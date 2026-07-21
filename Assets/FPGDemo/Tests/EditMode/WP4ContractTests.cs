using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class WP4ContractTests
    {
        [Test]
        public void BattleTickInputCopiesEdgeCommandsAndRejectsMismatchedPoseTick()
        {
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(new InputSequence(7), InputEdgeType.ReloadPressed)
            };
            PlayerInputFrame frame = new PlayerInputFrame(new TickIndex(4), true, false, edges, 1);
            BattleTickInput input = new BattleTickInput(frame, CreateAimPose(new TickIndex(4)));
            edges[0] = new InputEdgeCommand(new InputSequence(8), InputEdgeType.SecondaryPressed);

            Assert.That(input.GetEdgeCommand(0).Sequence.Value, Is.EqualTo(7));
            Assert.That(input.GetEdgeCommand(0).Type, Is.EqualTo(InputEdgeType.ReloadPressed));
            Assert.Throws<System.ArgumentException>(() => new BattleTickInput(
                frame,
                CreateAimPose(new TickIndex(5))));
        }

        [Test]
        public void BattleTickInputCanRehydrateCallerOwnedFrameBuffer()
        {
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(new InputSequence(1), InputEdgeType.SecondaryPressed),
                new InputEdgeCommand(new InputSequence(2), InputEdgeType.SecondaryReleased)
            };
            BattleTickInput input = new BattleTickInput(
                new PlayerInputFrame(new TickIndex(0), true, true, edges, 2, cancelSecondary: true),
                CreateAimPose(new TickIndex(0)));

            InputEdgeCommand[] destination = new InputEdgeCommand[2];
            PlayerInputFrame copy = input.CopyToPlayerInputFrame(destination);
            Assert.That(copy.EdgeCommandCount, Is.EqualTo(2));
            Assert.That(copy.EdgeCommands[1].Type, Is.EqualTo(InputEdgeType.SecondaryReleased));
            Assert.That(copy.CancelSecondary, Is.True);
        }

        [Test]
        public void QueryRequestValidatesShotOwnershipAndNullPortProducesNoCandidates()
        {
            AttackSnapshot attack = CreateAttack(new TickIndex(0));
            PelletSample[] pellets = { new PelletSample(attack.ShotId, 0, 1, 2) };
            AttackQueryRequest request = new AttackQueryRequest(
                new BattleTickInput(PlayerInputFrame.Empty(new TickIndex(0)), CreateAimPose(new TickIndex(0))),
                attack,
                pellets,
                1);
            pellets[0] = new PelletSample(attack.ShotId, 0, 99, 99);
            QueryCandidate[] output = new QueryCandidate[2];

            Assert.That(new NullAttackQueryPort().Query(request, output, out AttackQueryResult rejected).RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(new EmptyAttackQueryPort().Query(request, output, out AttackQueryResult result).IsSuccess, Is.True);
            Assert.That(result.CandidateCount, Is.Zero);
            Assert.That(request.GetPellet(0).SpreadU24, Is.EqualTo(1));
            Assert.Throws<System.ArgumentException>(() => new AttackQueryRequest(
                request.TickInput,
                attack,
                new[] { new PelletSample(new ShotId(99), 0, 1, 2) },
                1));
            Assert.Throws<System.ArgumentException>(() => new AttackQueryRequest(
                default(BattleTickInput),
                attack,
                pellets,
                1));
        }

        [Test]
        public void RecordedDroppedQueryPreservesBufferCapacityForTargetSelector()
        {
            AttackSnapshot attack = CreateAttack(new TickIndex(0));
            AttackQueryRequest request = new AttackQueryRequest(
                new BattleTickInput(
                    PlayerInputFrame.Empty(new TickIndex(0)),
                    CreateAimPose(new TickIndex(0))),
                attack,
                new[] { new PelletSample(attack.ShotId, 0, 1, 2) },
                1);
            SpatialPortTranscript transcript = new SpatialPortTranscript(2, 2);
            RecordingAttackQueryPort recording = new RecordingAttackQueryPort(
                new DroppedCandidateQueryPort(),
                transcript);
            QueryCandidate[] candidates = new QueryCandidate[2];

            DomainResult recorded = recording.Query(
                request,
                candidates,
                out AttackQueryResult result);

            Assert.That(recorded.IsSuccess, Is.True);
            Assert.That(result.DroppedCandidateCount, Is.EqualTo(1));
            Assert.That(TargetSelector.Select(
                attack,
                candidates,
                result,
                new QueryCandidate[1],
                out int selectedCount).RejectReason,
                Is.EqualTo(RejectReason.BufferCapacity));

            transcript.ResetReplay();
            ReplayAttackQueryPort replay = new ReplayAttackQueryPort(transcript);
            Assert.That(replay.Query(
                request,
                candidates,
                out AttackQueryResult replayed).IsSuccess, Is.True);
            Assert.That(replayed.DroppedCandidateCount, Is.EqualTo(1));
        }

        [Test]
        public void QueryCandidateRequiresStableGeometryAndTargetIdentity()
        {
            QueryCandidate candidate = new QueryCandidate(
                AttackQueryStage.Pellet,
                0,
                new RuntimeId(9),
                QueryTargetKind.Combatant,
                HitPart.Weakpoint,
                new GeometryId(12),
                100,
                new SpatialVectorKey(1, 2, 3),
                4);

            Assert.That(candidate.TargetId.Value, Is.EqualTo(9));
            Assert.Throws<System.ArgumentException>(() => new QueryCandidate(
                AttackQueryStage.Pellet,
                0,
                RuntimeId.Invalid,
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(1),
                0,
                SpatialVectorKey.Zero,
                0));
            Assert.Throws<System.ArgumentException>(() => new QueryCandidate(
                AttackQueryStage.Direct,
                -1,
                new RuntimeId(1),
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                new GeometryId(1),
                0,
                SpatialVectorKey.Zero,
                0));
        }

        [Test]
        public void ProjectileWorldPortUsesExplicitRegisterSweepReleaseContract()
        {
            ProjectileSpawnRequest request = new ProjectileSpawnRequest(
                new TickIndex(2),
                new TickIndex(5),
                new ProjectileId(3),
                new RuntimeId(4),
                new AttackId(5),
                new RuntimeId(6),
                new RuntimeId(7),
                Team.Enemy,
                301,
                2,
                9,
                true);
            Assert.That(new NullProjectileWorldPort().Register(request, out ProjectilePathSnapshot path).IsSuccess, Is.False);
            Assert.That(path.ProjectileId.IsValid, Is.False);
            ProjectilePathSnapshot frozenPath = new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                SpatialVectorKey.Zero,
                new SpatialVectorKey(0, 0, 10));
            Assert.That(frozenPath.Matches(request), Is.True);
            Assert.That(frozenPath.PositionAtTick(new TickIndex(3)).Z, Is.EqualTo(3));
            Assert.That(frozenPath.TryGetSegment(
                new TickIndex(3),
                out SpatialVectorKey from,
                out SpatialVectorKey to).IsSuccess, Is.True);
            Assert.That(from.Z, Is.Zero);
            Assert.That(to.Z, Is.EqualTo(3));

            ProjectileSweepHit hit = ProjectileSweepHit.EnvironmentBlocked(
                new GeometryId(2),
                8,
                new SpatialVectorKey(2, 0, 0));
            Assert.That(hit.Kind, Is.EqualTo(ProjectileSweepHitKind.EnvironmentBlocked));
            Assert.That(hit.TargetId.IsValid, Is.False);
            Assert.Throws<System.ArgumentException>(() => new ProjectileSpawnRequest(
                new TickIndex(2),
                new TickIndex(5),
                ProjectileId.Invalid,
                new RuntimeId(4),
                new AttackId(5),
                new RuntimeId(6),
                new RuntimeId(7),
                Team.Enemy,
                301,
                2,
                9,
                false));
        }

        [Test]
        public void SpatialContractV2IncludesInterceptabilityInProjectileReplayIdentity()
        {
            Assert.That(SpatialContract.Version, Is.EqualTo(2));
            Assert.That(new SpatialDecisionTranscript(1).ContractVersion, Is.EqualTo(2));

            SpatialPortTranscript transcript = new SpatialPortTranscript(2, 1);
            Assert.That(transcript.ContractVersion, Is.EqualTo(2));
            ProjectileSpawnRequest recordedRequest = new ProjectileSpawnRequest(
                new TickIndex(2),
                new TickIndex(5),
                new ProjectileId(3),
                new RuntimeId(4),
                new AttackId(5),
                new RuntimeId(6),
                new RuntimeId(7),
                Team.Enemy,
                301,
                2,
                9,
                true);
            RecordingProjectileWorldPort recording = new RecordingProjectileWorldPort(
                new EchoProjectileWorldPort(),
                transcript);
            Assert.That(recording.Register(recordedRequest, out ProjectilePathSnapshot recordedPath).IsSuccess, Is.True);
            Assert.That(recordedPath.Matches(recordedRequest), Is.True);

            transcript.ResetReplay();
            ReplayProjectileWorldPort replay = new ReplayProjectileWorldPort(transcript);
            ProjectileSpawnRequest changedInterceptability = new ProjectileSpawnRequest(
                recordedRequest.Tick,
                recordedRequest.ArrivalTick,
                recordedRequest.ProjectileId,
                recordedRequest.RuntimeId,
                recordedRequest.AttackId,
                recordedRequest.OwnerId,
                recordedRequest.TargetId,
                recordedRequest.Team,
                recordedRequest.DefinitionId,
                recordedRequest.SweepRadiusKey,
                recordedRequest.PresentationKey,
                false);

            Assert.That(replay.Register(changedInterceptability, out ProjectilePathSnapshot rejectedPath).RejectReason,
                Is.EqualTo(RejectReason.InvariantFault));
            Assert.That(rejectedPath.ProjectileId.IsValid, Is.False);
            Assert.That(transcript.ReplayRemaining, Is.EqualTo(1));
            Assert.That(replay.Register(recordedRequest, out ProjectilePathSnapshot replayedPath).IsSuccess, Is.True);
            Assert.That(replayedPath.Start, Is.EqualTo(recordedPath.Start));
            Assert.That(replayedPath.End, Is.EqualTo(recordedPath.End));
            Assert.That(transcript.ReplayRemaining, Is.Zero);
        }

        [Test]
        public void ThreatPayloadSeparatesTimedImpactFromProjectileBudget()
        {
            ProjectileDefinition projectile = new ProjectileDefinition(
                301,
                new TickDuration(3),
                new TickDuration(5),
                new DamageSpec(20, 3),
                10,
                true,
                2,
                7,
                2);
            ThreatPayloadDefinition swept = ThreatPayloadDefinition.SweptProjectile(projectile, 3);
            ThreatPayloadDefinition timed = ThreatPayloadDefinition.TimedImpact(
                new DamageSpec(50, 10),
                ThreatTargetPolicy.PlayerCombatant,
                new TickDuration(1),
                8);

            Assert.That(swept.IsValid, Is.True);
            Assert.That(swept.TotalBudgetUnits, Is.EqualTo(6));
            Assert.That(swept.PresentationKey, Is.EqualTo(7));
            Assert.That(timed.IsTimedImpact, Is.True);
            Assert.That(timed.TotalBudgetUnits, Is.Zero);
            Assert.Throws<System.OverflowException>(() => ThreatPayloadDefinition.SweptProjectile(
                new ProjectileDefinition(
                    999,
                    new TickDuration(1),
                    new TickDuration(1),
                    new DamageSpec(1, 0),
                    1,
                    true,
                    int.MaxValue),
                2));
        }

        [Test]
        public void ScenarioDefinitionCanonicalizesAndHashesThreatSchedule()
        {
            ScenarioDefinition baseline = CombatLabHarness.CreateScenario();
            ProjectileDefinition projectile = new ProjectileDefinition(
                301,
                new TickDuration(3),
                new TickDuration(5),
                new DamageSpec(20, 0),
                10,
                true,
                1);
            ThreatPayloadDefinition payload = ThreatPayloadDefinition.SweptProjectile(projectile, 1);
            ThreatScheduleEntry[] entries =
            {
                new ThreatScheduleEntry(2, new TickIndex(4), 202, new TickDuration(1), new TickDuration(1), new TickDuration(1), payload, ThreatRetryPolicy.HoldPendingNextTick),
                new ThreatScheduleEntry(1, new TickIndex(2), 201, new TickDuration(1), new TickDuration(1), new TickDuration(1), payload, ThreatRetryPolicy.HoldPendingNextTick)
            };
            ScenarioDefinition scheduled = CopyScenario(baseline, entries);

            Assert.That(scheduled.ThreatScheduleCount, Is.EqualTo(2));
            Assert.That(scheduled.GetThreatScheduleEntry(0).DueTick.Value, Is.EqualTo(2));
            Assert.That(scheduled.DefinitionHash, Is.Not.EqualTo(baseline.DefinitionHash));

            entries[0] = new ThreatScheduleEntry(9, new TickIndex(99), 999, new TickDuration(1), new TickDuration(1), new TickDuration(1), payload, ThreatRetryPolicy.HoldPendingNextTick);
            Assert.That(scheduled.GetThreatScheduleEntry(1).ScheduleSequence, Is.EqualTo(2));

            ThreatPayloadDefinition oversized = ThreatPayloadDefinition.SweptProjectile(projectile, baseline.ProjectileCapacity + 1);
            Assert.Throws<System.ArgumentException>(() => CopyScenario(
                baseline,
                new[]
                {
                    new ThreatScheduleEntry(
                        3,
                        new TickIndex(3),
                        203,
                        new TickDuration(1),
                        new TickDuration(1),
                        new TickDuration(1),
                        oversized,
                        ThreatRetryPolicy.HoldPendingNextTick)
                }));
            Assert.Throws<System.ArgumentException>(() => CopyScenario(
                baseline,
                new[] { default(ThreatScheduleEntry) }));
        }

        [Test]
        public void ProjectileTerminalReasonsDistinguishMissAndEnvironmentBlock()
        {
            ProjectileRuntime missed = CreateProjectile();
            missed.StartTravelling();
            Assert.That(missed.TryMiss(new TickIndex(3)).IsSuccess, Is.True);
            Assert.That(missed.GetSnapshot().TerminalReason, Is.EqualTo(ProjectileTerminalReason.Missed));

            ProjectileRuntime blocked = CreateProjectile();
            blocked.StartTravelling();
            Assert.That(blocked.TryBlock(new TickIndex(1)).IsSuccess, Is.True);
            Assert.That(blocked.GetSnapshot().TerminalReason, Is.EqualTo(ProjectileTerminalReason.EnvironmentBlocked));
            Assert.That(blocked.GetSnapshot().TerminalTick.Value, Is.EqualTo(1));
        }

        [Test]
        public void BattleSessionExposesStableActorIdsAndCapacitySafeProjectileCopy()
        {
            using (BattleSession session = CombatLabHarness.CreateSession())
            {
                Assert.That(session.PlayerRuntimeId.IsValid, Is.True);
                Assert.That(session.EnemyRuntimeId.IsValid, Is.True);
                Assert.That(session.PlayerRuntimeId, Is.Not.EqualTo(session.EnemyRuntimeId));
                Assert.That(session.CopyActiveProjectileSnapshots(new ProjectileSnapshot[0], out int count).IsSuccess, Is.True);
                Assert.That(count, Is.Zero);
            }
        }

        [Test]
        public void SpatialDecisionTranscriptIsFixedCapacityAndReplayStable()
        {
            SpatialDecisionTranscript first = new SpatialDecisionTranscript(2);
            SpatialDecisionTranscript second = new SpatialDecisionTranscript(2);

            Assert.That(first.ContractVersion, Is.EqualTo(SpatialContract.Version));

            Assert.That(first.TryRecord(
                new TickIndex(1),
                SpatialDecisionKind.AttackQuery,
                new RuntimeId(2),
                new GeometryId(3),
                RejectReason.None,
                0xAAUL,
                out SpatialDecisionRecord record).IsSuccess, Is.True);
            Assert.That(second.TryRecord(
                new TickIndex(1),
                SpatialDecisionKind.AttackQuery,
                new RuntimeId(2),
                new GeometryId(3),
                RejectReason.None,
                0xAAUL,
                out SpatialDecisionRecord ignored).IsSuccess, Is.True);

            Assert.That(record.Sequence, Is.EqualTo(1));
            Assert.That(first.CanonicalDigest, Is.EqualTo(second.CanonicalDigest));
            Assert.That(first.TryRecord(
                new TickIndex(2),
                SpatialDecisionKind.ProjectileSweep,
                new RuntimeId(4),
                GeometryId.Invalid,
                RejectReason.None,
                0xBBUL,
                out ignored).IsSuccess, Is.True);
            Assert.That(first.TryRecord(
                new TickIndex(3),
                SpatialDecisionKind.ProjectileRelease,
                new RuntimeId(4),
                GeometryId.Invalid,
                RejectReason.None,
                0xCCUL,
                out ignored).RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
        }

        [Test]
        public void TypedSpatialTranscriptReplaysQueryPathSweepAndReleaseInOrder()
        {
            SpatialPortTranscript transcript = new SpatialPortTranscript(4, 4);
            RecordingAttackQueryPort recordingQuery = new RecordingAttackQueryPort(
                new SingleCandidateQueryPort(),
                transcript);
            RecordingProjectileWorldPort recordingWorld = new RecordingProjectileWorldPort(
                new EchoProjectileWorldPort(),
                transcript);

            AttackSnapshot attack = CreateAttack(new TickIndex(0));
            AttackQueryRequest queryRequest = new AttackQueryRequest(
                new BattleTickInput(PlayerInputFrame.Empty(new TickIndex(0)), CreateAimPose(new TickIndex(0))),
                attack,
                new[] { new PelletSample(attack.ShotId, 0, 1, 2) },
                1);
            QueryCandidate[] candidates = new QueryCandidate[2];
            Assert.That(recordingQuery.Query(queryRequest, candidates, out AttackQueryResult queryResult).IsSuccess, Is.True);
            Assert.That(queryResult.CandidateCount, Is.EqualTo(1));

            ProjectileSpawnRequest spawn = new ProjectileSpawnRequest(
                new TickIndex(2),
                new TickIndex(5),
                new ProjectileId(3),
                new RuntimeId(4),
                new AttackId(5),
                new RuntimeId(6),
                new RuntimeId(7),
                Team.Enemy,
                301,
                2,
                9,
                true);
            Assert.That(recordingWorld.Register(spawn, out ProjectilePathSnapshot recordedPath).IsSuccess, Is.True);
            ProjectileSweepRequest sweep = new ProjectileSweepRequest(
                new TickIndex(3),
                spawn.ProjectileId,
                spawn.RuntimeId,
                recordedPath.PositionAtTick(new TickIndex(2)),
                recordedPath.PositionAtTick(new TickIndex(3)),
                spawn.SweepRadiusKey);
            Assert.That(recordingWorld.Sweep(sweep, out ProjectileSweepHit recordedHit).IsSuccess, Is.True);
            ProjectileReleaseRequest release = new ProjectileReleaseRequest(
                new TickIndex(3),
                spawn.ProjectileId,
                spawn.RuntimeId,
                ProjectileTerminalReason.TargetImpact);
            Assert.That(recordingWorld.Release(release).IsSuccess, Is.True);
            Assert.That(transcript.Count, Is.EqualTo(4));

            transcript.ResetReplay();
            ReplayAttackQueryPort replayQuery = new ReplayAttackQueryPort(transcript);
            ReplayProjectileWorldPort replayWorld = new ReplayProjectileWorldPort(transcript);
            QueryCandidate[] replayedCandidates = new QueryCandidate[2];
            Assert.That(replayQuery.Query(queryRequest, replayedCandidates, out AttackQueryResult replayedResult).IsSuccess, Is.True);
            Assert.That(replayedResult.CandidateCount, Is.EqualTo(1));
            Assert.That(replayedCandidates[0].GeometryId.Value, Is.EqualTo(11));
            Assert.That(replayWorld.Register(spawn, out ProjectilePathSnapshot replayedPath).IsSuccess, Is.True);
            Assert.That(replayedPath.End, Is.EqualTo(recordedPath.End));
            Assert.That(replayWorld.Sweep(sweep, out ProjectileSweepHit replayedHit).IsSuccess, Is.True);
            Assert.That(replayedHit.GeometryId, Is.EqualTo(recordedHit.GeometryId));
            Assert.That(replayWorld.Release(release).IsSuccess, Is.True);
            Assert.That(transcript.ReplayRemaining, Is.Zero);

            using (BattleSession session = new BattleSessionFactory().Create(
                CombatLabHarness.CreateScenario(),
                null,
                replayQuery,
                replayWorld,
                transcript))
            {
                ReplaySummary summary = session.GetReplaySummary();
                Assert.That(summary.SpatialContractVersion, Is.EqualTo(SpatialContract.Version));
                Assert.That(summary.SpatialDecisionCount, Is.EqualTo(4));
                Assert.That(summary.SpatialDecisionDigest, Is.EqualTo(transcript.CanonicalDigest));
            }
        }

        private static AimPoseSnapshot CreateAimPose(TickIndex tick)
        {
            return new AimPoseSnapshot(
                tick,
                new SpatialVectorKey(0, 1000, 0),
                new SpatialVectorKey(0, 0, 1000000),
                new SpatialVectorKey(1000000, 0, 0),
                new SpatialVectorKey(0, 1000000, 0),
                1);
        }

        private static AttackSnapshot CreateAttack(TickIndex tick)
        {
            return new AttackSnapshot(
                new AttackId(1),
                new ShotId(1),
                1,
                new RuntimeId(1),
                Team.Player,
                tick,
                new DamageSpec(10, 2),
                QueryPolicy.PelletRays,
                1,
                1,
                1,
                1);
        }

        private static ProjectileRuntime CreateProjectile()
        {
            return new ProjectileRuntime(
                new ProjectileId(1),
                new RuntimeId(3),
                new AttackId(1),
                new RuntimeId(2),
                Team.Enemy,
                new ProjectileDefinition(
                    1,
                    new TickDuration(3),
                    new TickDuration(5),
                    new DamageSpec(10, 0),
                    10,
                    true,
                    1),
                new TickIndex(0),
                default(ReservationToken));
        }

        private static ScenarioDefinition CopyScenario(ScenarioDefinition source, ThreatScheduleEntry[] schedule)
        {
            return new ScenarioDefinition(
                source.ScenarioSeed,
                source.PlayerWeapon,
                source.PlayerLife,
                source.PlayerBarrier,
                source.EnemyLife,
                source.EnemyBreak,
                source.PerfectRetractWindow,
                source.PerfectRetractMultiplierBasisPoints,
                source.BarrierLockDuration,
                source.BarrierRestoreBasisPoints,
                source.EnemyGroggyDuration,
                source.ProjectileBudgetCapacity,
                source.ProjectileCapacity,
                source.ThreatCapacity,
                source.ImpactHistoryCapacity,
                source.ShotTargetHistoryCapacity,
                schedule);
        }

        private sealed class SingleCandidateQueryPort : IAttackQueryPort
        {
            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                if (output == null || output.Length == 0)
                {
                    result = new AttackQueryResult(0, 1);
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                output[0] = new QueryCandidate(
                    AttackQueryStage.Pellet,
                    0,
                    new RuntimeId(9),
                    QueryTargetKind.Combatant,
                    HitPart.Body,
                    new GeometryId(11),
                    5,
                    new SpatialVectorKey(0, 0, 5),
                    0);
                result = new AttackQueryResult(1, 0);
                return DomainResult.Success;
            }
        }

        private sealed class DroppedCandidateQueryPort : IAttackQueryPort
        {
            public DomainResult Query(
                in AttackQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                result = new AttackQueryResult(0, 1);
                return DomainResult.Success;
            }
        }

        private sealed class EchoProjectileWorldPort : IProjectileWorldPort
        {
            public DomainResult Register(
                in ProjectileSpawnRequest request,
                out ProjectilePathSnapshot path)
            {
                path = new ProjectilePathSnapshot(
                    request.ProjectileId,
                    request.RuntimeId,
                    request.Tick,
                    request.ArrivalTick,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(0, 0, 10));
                return DomainResult.Success;
            }

            public DomainResult Sweep(
                in ProjectileSweepRequest request,
                out ProjectileSweepHit hit)
            {
                hit = ProjectileSweepHit.Target(
                    new RuntimeId(7),
                    HitPart.Body,
                    new GeometryId(12),
                    3,
                    request.To);
                return DomainResult.Success;
            }

            public DomainResult Release(in ProjectileReleaseRequest request)
            {
                return DomainResult.Success;
            }
        }
    }
}
