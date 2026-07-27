using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgPlayerAreaProjectileTests
    {
        [Test]
        public void FirstSurfaceTerminalQueriesAreaAndResolvesSecondaryExplosiveImpacts()
        {
            AssertFirstSurfaceTerminalQueriesAreaAndResolvesSecondaryExplosiveImpacts(
                HitPart.Body);
        }

        [Test]
        public void ProjectileHitPartFirstSurfaceTerminalQueriesAreaAndResolvesSecondaryExplosiveImpacts()
        {
            AssertFirstSurfaceTerminalQueriesAreaAndResolvesSecondaryExplosiveImpacts(
                HitPart.Projectile);
        }

        [Test]
        public void PlayerProjectileAreaQueryTranscriptReplaysCandidates()
        {
            SpatialVectorKey center = new SpatialVectorKey(300, 0, 700);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(901L),
                new ShotId(902L),
                903,
                new RuntimeId(904L),
                Team.Player,
                new TickIndex(0L),
                new DamageSpec(17, 0),
                QueryPolicy.DirectThenArea,
                1,
                2,
                1,
                1,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                2,
                0,
                AttackTargetKinds.Combatant);
            PlayerProjectileAreaQueryRequest request =
                new PlayerProjectileAreaQueryRequest(
                    new TickIndex(1L),
                    attack,
                    center);
            QueryCandidate first = CreateAreaCandidate(
                new RuntimeId(905L),
                11,
                10,
                center,
                0);
            QueryCandidate second = CreateAreaCandidate(
                new RuntimeId(906L),
                12,
                20,
                center,
                1);
            CapturingPlayerProjectileAreaQueryPort inner =
                new CapturingPlayerProjectileAreaQueryPort();
            inner.SetCandidates(first, second);
            SpatialPortTranscript transcript = new SpatialPortTranscript(1, 2);
            RecordingPlayerProjectileAreaQueryPort recording =
                new RecordingPlayerProjectileAreaQueryPort(inner, transcript);
            QueryCandidate[] recordedCandidates = new QueryCandidate[2];

            Assert.That(
                recording.QueryAreaAtPoint(
                    request,
                    recordedCandidates,
                    out AttackQueryResult recordedResult).IsSuccess,
                Is.True);
            Assert.That(recordedResult.CandidateCount, Is.EqualTo(2));
            Assert.That(inner.CallCount, Is.EqualTo(1));
            Assert.That(transcript.Count, Is.EqualTo(1));

            transcript.ResetReplay();
            ReplayPlayerProjectileAreaQueryPort replay =
                new ReplayPlayerProjectileAreaQueryPort(transcript);
            QueryCandidate[] replayedCandidates = new QueryCandidate[2];

            Assert.That(
                replay.QueryAreaAtPoint(
                    request,
                    replayedCandidates,
                    out AttackQueryResult replayedResult).IsSuccess,
                Is.True);
            Assert.That(replayedResult.CandidateCount, Is.EqualTo(2));
            Assert.That(replayedCandidates[0].TargetId, Is.EqualTo(first.TargetId));
            Assert.That(replayedCandidates[1].TargetId, Is.EqualTo(second.TargetId));
            Assert.That(replayedCandidates[0].ImpactPointKey, Is.EqualTo(center));
            Assert.That(replayedCandidates[1].ImpactPointKey, Is.EqualTo(center));
            Assert.That(inner.CallCount, Is.EqualTo(1));
            Assert.That(transcript.ReplayRemaining, Is.Zero);
        }

        private static void AssertFirstSurfaceTerminalQueriesAreaAndResolvesSecondaryExplosiveImpacts(
            HitPart terminalHitPart)
        {
            FirstSurfaceProjectileWorldPort projectileWorld =
                new FirstSurfaceProjectileWorldPort(
                    new SpatialVectorKey(300, 0, 700),
                    terminalHitPart);
            CapturingPlayerProjectileAreaQueryPort areaQuery =
                new CapturingPlayerProjectileAreaQueryPort();
            PortFixture fixture = new PortFixture(projectileWorld, areaQuery);
            RuntimeId firstTarget = fixture.RegisterEnemy(100);
            RuntimeId secondTarget = fixture.RegisterEnemy(100);
            projectileWorld.TerminalTarget = firstTarget;
            areaQuery.SetCandidates(
                CreateAreaCandidate(
                    firstTarget,
                    11,
                    10,
                    projectileWorld.TerminalPoint,
                    0),
                CreateAreaCandidate(
                    secondTarget,
                    12,
                    20,
                    projectileWorld.TerminalPoint,
                    1));
            FpgEnemyRoster roster = new FpgEnemyRoster(4);
            TickIndex spawnTick = new TickIndex(0L);
            TickIndex terminalTick = new TickIndex(1L);

            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.LifecycleBoundary,
                    spawnTick,
                    roster).IsSuccess,
                Is.True);

            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(901L),
                new ShotId(902L),
                903,
                fixture.PlayerId,
                Team.Player,
                spawnTick,
                new DamageSpec(17, 0),
                QueryPolicy.DirectThenArea,
                1,
                2,
                1,
                1,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                2,
                0,
                AttackTargetKinds.Combatant);
            ProjectileDefinition definition = new ProjectileDefinition(
                904,
                new TickDuration(2),
                new TickDuration(3),
                attack.DamageSpec,
                0,
                false,
                1,
                120);
            FpgPlayerAreaProjectileRequest request =
                new FpgPlayerAreaProjectileRequest(
                    spawnTick,
                    attack,
                    definition,
                    new SpatialVectorKey(100, 0, 0),
                    new SpatialVectorKey(100, 0, 1000),
                    new SkillExecutionId(906L),
                    907);

            DomainResult spawned = fixture.Port.TrySpawnPlayerAreaProjectile(
                request,
                out RuntimeId projectileRuntimeId);
            Assert.That(spawned.IsSuccess, Is.True);
            Assert.That(projectileRuntimeId.IsValid, Is.True);

            DomainResult advanced = fixture.Port.Process(
                FpgBattleTickPhase.ThreatAndProjectileAdvance,
                terminalTick,
                roster);

            Assert.That(advanced.IsSuccess, Is.True, advanced.RejectReason.ToString());
            Assert.That(projectileWorld.RegisterCount, Is.EqualTo(1));
            Assert.That(
                projectileWorld.Registration.TargetingMode,
                Is.EqualTo(ProjectileTargetingMode.FirstSurface));
            Assert.That(projectileWorld.Registration.TargetId.IsValid, Is.False);
            Assert.That(projectileWorld.Registration.HasExplicitPath, Is.True);
            Assert.That(projectileWorld.TerminalHitPart, Is.EqualTo(terminalHitPart));
            Assert.That(projectileWorld.SweepCount, Is.EqualTo(1));
            Assert.That(projectileWorld.ReleaseCount, Is.EqualTo(1));
            Assert.That(
                projectileWorld.LastRelease.Reason,
                Is.EqualTo(ProjectileTerminalReason.TargetImpact));
            Assert.That(areaQuery.CallCount, Is.EqualTo(1));
            Assert.That(areaQuery.LastRequest.Tick, Is.EqualTo(terminalTick));
            Assert.That(
                areaQuery.LastRequest.Center,
                Is.EqualTo(projectileWorld.TerminalPoint));
            Assert.That(fixture.Port.ActiveProjectileCount, Is.Zero);
            Assert.That(fixture.Kernel.ImpactQueue.Count, Is.EqualTo(2));

            DomainResult resolved = fixture.Port.Process(
                FpgBattleTickPhase.ImpactResolution,
                terminalTick,
                roster);
            FpgResolvedDamageFeedback[] feedback =
                new FpgResolvedDamageFeedback[2];
            int feedbackCount = fixture.Port.DamageFeedback.CopyAfter(
                0L,
                feedback,
                out bool feedbackGap);

            Assert.That(resolved.IsSuccess, Is.True);
            Assert.That(fixture.Port.TryGetEnemyRuntime(firstTarget, out EnemyRuntime first), Is.True);
            Assert.That(fixture.Port.TryGetEnemyRuntime(secondTarget, out EnemyRuntime second), Is.True);
            Assert.That(first.Combatant.Life, Is.EqualTo(83));
            Assert.That(second.Combatant.Life, Is.EqualTo(83));
            Assert.That(feedbackGap, Is.False);
            Assert.That(feedbackCount, Is.EqualTo(2));
            Assert.That(feedback[0].DamageType, Is.EqualTo(DamageType.Explosive));
            Assert.That(feedback[1].DamageType, Is.EqualTo(DamageType.Explosive));
            Assert.That(
                feedback[0].Tags & CombatTags.Secondary,
                Is.EqualTo(CombatTags.Secondary));
            Assert.That(
                feedback[1].Tags & CombatTags.Secondary,
                Is.EqualTo(CombatTags.Secondary));
            Assert.That(feedback[0].SourceId, Is.EqualTo(fixture.PlayerId));
            Assert.That(feedback[1].SourceId, Is.EqualTo(fixture.PlayerId));
            Assert.That(feedback[0].AttackId, Is.EqualTo(attack.AttackId));
            Assert.That(feedback[1].AttackId, Is.EqualTo(attack.AttackId));
            Assert.That(feedback[0].ShotId, Is.EqualTo(attack.ShotId));
            Assert.That(feedback[1].ShotId, Is.EqualTo(attack.ShotId));
        }

        private static QueryCandidate CreateAreaCandidate(
            RuntimeId targetId,
            int geometryId,
            int distanceKey,
            SpatialVectorKey impactPoint,
            int queryOrdinal)
        {
            return new QueryCandidate(
                AttackQueryStage.Area,
                -1,
                targetId,
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(geometryId),
                distanceKey,
                impactPoint,
                queryOrdinal);
        }

        private sealed class PortFixture
        {
            private readonly SessionIdAllocator ids = new SessionIdAllocator();
            private int nextSpawnSequence;

            public PortFixture(
                IProjectileWorldPort projectileWorldPort,
                IPlayerProjectileAreaQueryPort playerProjectileAreaQueryPort)
            {
                PlayerId = ids.NextRuntimeId();
                Kernel = new CombatKernel(
                    projectileBudgetCapacity: 4,
                    impactCapacity: 4,
                    shotTargetCapacity: 4,
                    impactQueueCapacity: 4,
                    traceCapacity: 128,
                    projectileReservationCapacity: 4);
                PlayerRuntime player = new PlayerRuntime(
                    new CombatantState(
                        PlayerId,
                        CombatantKind.Player,
                        100,
                        20,
                        0),
                    new ExposureRuntime(PlayerExposureState.Exposed),
                    new WeaponRuntime(CreateWeaponDefinition()));
                Port = new FpgMultiEnemyCombatPort(
                    Kernel,
                    player,
                    ids,
                    new FpgMultiEnemyCombatCapacity(
                        enemyCapacity: 4,
                        playerHitCommandCapacity: 4,
                        attackScheduleCapacity: 4,
                        projectileCapacity: 4,
                        threatAdvanceCapacity: 4,
                        perEnemyThreatCapacity: 2,
                        summonCapacity: 2,
                        maxTotalSummons: 2,
                        maxSummonRecursionDepth: 1,
                        vitalsEventCapacity: 16,
                        damageFeedbackCapacity: 16),
                    new TickDuration(3),
                    projectileWorldPort,
                    new RejectingSummonSink(),
                    playerProjectileAreaQueryPort: playerProjectileAreaQueryPort);
            }

            public RuntimeId PlayerId { get; }
            public CombatKernel Kernel { get; }
            public FpgMultiEnemyCombatPort Port { get; }

            public RuntimeId RegisterEnemy(int life)
            {
                RuntimeId runtimeId = ids.NextRuntimeId();
                DomainResult registered = Port.TryRegisterEnemy(
                    new FpgEnemyCombatantRegistration(
                        runtimeId,
                        nextSpawnSequence++,
                        life,
                        0,
                        new TickDuration(3),
                        new TickIndex(0L)));
                Assert.That(registered.IsSuccess, Is.True);
                return runtimeId;
            }

            private static WeaponDefinition CreateWeaponDefinition()
            {
                return new WeaponDefinition(
                    1,
                    8,
                    1,
                    new TickDuration(2),
                    new DamageSpec(1, 0),
                    2,
                    new TickDuration(3),
                    new DamageSpec(2, 0),
                    new TickDuration(2),
                    4);
            }
        }

        private sealed class RejectingSummonSink : IFpgSummonRequestSink
        {
            public FpgSummonQueueAck TryQueueSummon(
                FpgSummonRequest request,
                TickIndex tick)
            {
                return FpgSummonQueueAck.Rejected(RejectReason.InvalidState);
            }
        }

        private sealed class FirstSurfaceProjectileWorldPort : IProjectileWorldPort
        {
            public FirstSurfaceProjectileWorldPort(
                SpatialVectorKey terminalPoint,
                HitPart terminalHitPart)
            {
                TerminalPoint = terminalPoint;
                TerminalHitPart = terminalHitPart;
            }

            public RuntimeId TerminalTarget { get; set; }
            public SpatialVectorKey TerminalPoint { get; }
            public HitPart TerminalHitPart { get; }
            public int RegisterCount { get; private set; }
            public int SweepCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public ProjectileSpawnRequest Registration { get; private set; }
            public ProjectileReleaseRequest LastRelease { get; private set; }

            public DomainResult Register(
                in ProjectileSpawnRequest request,
                out ProjectilePathSnapshot path)
            {
                RegisterCount++;
                Registration = request;
                if (request.TargetingMode != ProjectileTargetingMode.FirstSurface
                    || request.Team != Team.Player
                    || request.TargetId.IsValid
                    || !request.HasExplicitPath)
                {
                    path = default(ProjectilePathSnapshot);
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                path = new ProjectilePathSnapshot(
                    request.ProjectileId,
                    request.RuntimeId,
                    request.Tick,
                    request.ArrivalTick,
                    request.ExplicitStart,
                    request.ExplicitEnd);
                return DomainResult.Success;
            }

            public DomainResult Sweep(
                in ProjectileSweepRequest request,
                out ProjectileSweepHit hit)
            {
                SweepCount++;
                if (!TerminalTarget.IsValid
                    || request.ProjectileId != Registration.ProjectileId
                    || request.RuntimeId != Registration.RuntimeId)
                {
                    hit = ProjectileSweepHit.None;
                    return DomainResult.Rejected(RejectReason.InvalidState);
                }

                hit = ProjectileSweepHit.Target(
                    TerminalTarget,
                    TerminalHitPart,
                    new GeometryId(1),
                    10,
                    TerminalPoint);
                return DomainResult.Success;
            }

            public DomainResult Release(in ProjectileReleaseRequest request)
            {
                ReleaseCount++;
                LastRelease = request;
                return DomainResult.Success;
            }
        }

        private sealed class CapturingPlayerProjectileAreaQueryPort :
            IPlayerProjectileAreaQueryPort
        {
            private QueryCandidate first;
            private QueryCandidate second;

            public int CallCount { get; private set; }
            public PlayerProjectileAreaQueryRequest LastRequest { get; private set; }

            public void SetCandidates(QueryCandidate first, QueryCandidate second)
            {
                this.first = first;
                this.second = second;
            }

            public DomainResult QueryAreaAtPoint(
                in PlayerProjectileAreaQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                CallCount++;
                LastRequest = request;
                if (output == null || output.Length < 2)
                {
                    result = new AttackQueryResult(0, 1);
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                output[0] = first;
                output[1] = second;
                result = new AttackQueryResult(2, 0);
                return DomainResult.Success;
            }
        }
    }
}
