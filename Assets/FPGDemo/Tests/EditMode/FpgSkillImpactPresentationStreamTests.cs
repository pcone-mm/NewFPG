using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillImpactPresentationStreamTests
    {
        [TestCase(
            ProjectileTerminalReason.TargetImpact,
            FpgSkillImpactContactKind.TargetImpact)]
        [TestCase(
            ProjectileTerminalReason.EnvironmentBlocked,
            FpgSkillImpactContactKind.EnvironmentBlocked)]
        [TestCase(
            ProjectileTerminalReason.Intercepted,
            FpgSkillImpactContactKind.Intercepted)]
        public void CollisionTerminalReasonsMapToExplicitContactKinds(
            ProjectileTerminalReason reason,
            FpgSkillImpactContactKind expected)
        {
            Assert.That(
                FpgSkillImpactPresentationRules
                    .TryResolveProjectileContactKind(
                        reason,
                        out FpgSkillImpactContactKind actual),
                Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(ProjectileTerminalReason.Missed)]
        [TestCase(ProjectileTerminalReason.LifetimeExpired)]
        [TestCase(ProjectileTerminalReason.OwnerCanceled)]
        [TestCase(ProjectileTerminalReason.SessionEnded)]
        public void NonCollisionTerminalReasonsDoNotCreateContacts(
            ProjectileTerminalReason reason)
        {
            Assert.That(
                FpgSkillImpactPresentationRules
                    .TryResolveProjectileContactKind(
                        reason,
                        out FpgSkillImpactContactKind ignored),
                Is.False);
        }

        [Test]
        public void FixedStreamRetainsNewestFactsAndReportsCursorGap()
        {
            FixedFpgSkillImpactPresentationStream stream =
                new FixedFpgSkillImpactPresentationStream(2);
            FpgSkillImpactCorrelation correlation =
                new FpgSkillImpactCorrelation(
                    new RuntimeId(1L),
                    new SkillExecutionId(2L),
                    3);
            FpgSkillImpactContact contact = new FpgSkillImpactContact(
                correlation,
                FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                new TickIndex(4L),
                new AttackId(5L),
                ProjectileId.Invalid,
                new ImpactId(6L),
                new RuntimeId(7L),
                FpgSkillImpactContactKind.TargetImpact,
                new SpatialVectorKey(10, 20, 30),
                HitPart.Weakpoint,
                0);
            FpgSkillImpactGroupCompletion completion =
                new FpgSkillImpactGroupCompletion(
                    correlation,
                    FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                    new TickIndex(4L),
                    new AttackId(5L));

            Assert.That(stream.TryRecordContact(contact), Is.True);
            Assert.That(
                stream.TryRecordGroupCompletion(completion),
                Is.True);
            Assert.That(stream.TryRecordContact(contact), Is.True);

            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[2];
            int count = stream.CopyAfter(0L, output, out bool hasGap);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(hasGap, Is.True);
            Assert.That(stream.DroppedEventCount, Is.EqualTo(1));
            Assert.That(stream.FirstRetainedSequence, Is.EqualTo(2L));
            Assert.That(stream.LastSequence, Is.EqualTo(3L));
            Assert.That(
                output[0].Type,
                Is.EqualTo(
                    FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(output[1].Contact.HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(output[1].Correlation, Is.EqualTo(correlation));
        }

        [Test]
        public void AcceptedZeroDamageAttackPublishesEveryContactThenOneBatchCompletion()
        {
            PortFixture fixture = new PortFixture();
            RuntimeId first = fixture.RegisterEnemy(100);
            RuntimeId second = fixture.RegisterEnemy(100);
            SkillExecutionId executionId = new SkillExecutionId(41L);
            FpgPlayerHitCommand[] commands =
            {
                fixture.CreateHitCommand(
                    0L,
                    101L,
                    first,
                    executionId,
                    42,
                    0,
                    HitPart.Body),
                fixture.CreateHitCommand(
                    1L,
                    102L,
                    second,
                    executionId,
                    42,
                    1,
                    HitPart.Weakpoint)
            };

            Assert.That(
                fixture.Port.TrySubmitPlayerHits(commands, commands.Length)
                    .IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.PlayerAttackAndHit,
                    new TickIndex(0L),
                    new FpgEnemyRoster(2)).IsSuccess,
                Is.True);

            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[8];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);

            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(3));
            Assert.That(
                output[0].Type,
                Is.EqualTo(FpgSkillImpactPresentationEventType.Contact));
            Assert.That(
                output[1].Type,
                Is.EqualTo(FpgSkillImpactPresentationEventType.Contact));
            Assert.That(
                output[2].Type,
                Is.EqualTo(
                    FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(output[0].Contact.ContactOrdinal, Is.EqualTo(0));
            Assert.That(output[1].Contact.ContactOrdinal, Is.EqualTo(1));
            Assert.That(output[1].Contact.HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(
                output[2].Correlation.SourceRuntimeId,
                Is.EqualTo(fixture.PlayerId));
            Assert.That(
                output[2].Correlation.SkillExecutionId,
                Is.EqualTo(executionId));
            Assert.That(output[2].Correlation.GameplayEventId, Is.EqualTo(42));
        }

        [Test]
        public void EmptyImmediateAttackPublishesOneGroupCompletion()
        {
            PortFixture fixture = new PortFixture();
            SkillExecutionId executionId = new SkillExecutionId(43L);

            Assert.That(
                fixture.Port.TryCompleteImmediateSkillPresentationGroup(
                    fixture.PlayerId,
                    executionId,
                    44,
                    new TickIndex(0L),
                    new AttackId(45L)),
                Is.True);

            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[1];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);

            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(
                output[0].Type,
                Is.EqualTo(
                    FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(output[0].Correlation.SkillExecutionId,
                Is.EqualTo(executionId));
            Assert.That(output[0].Correlation.GameplayEventId, Is.EqualTo(44));
        }

        [Test]
        public void ImmediateEnvironmentContactPublishesBeforeOneGroupCompletion()
        {
            PortFixture fixture = new PortFixture();
            TickIndex tick = new TickIndex(0L);
            AttackId attackId = new AttackId(45L);
            SkillExecutionId executionId = new SkillExecutionId(46L);
            SpatialVectorKey contactPoint =
                new SpatialVectorKey(100, 200, 300);

            Assert.That(
                fixture.Port.TryPublishImmediateEnvironmentContact(
                    fixture.PlayerId,
                    executionId,
                    47,
                    tick,
                    attackId,
                    contactPoint,
                    0),
                Is.True);
            Assert.That(
                fixture.Port.TryCompleteImmediateSkillPresentationGroup(
                    fixture.PlayerId,
                    executionId,
                    47,
                    tick,
                    attackId),
                Is.True);

            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[2];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);
            FpgSkillImpactCorrelation correlation =
                new FpgSkillImpactCorrelation(
                    fixture.PlayerId,
                    executionId,
                    47);

            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(
                output[0].Type,
                Is.EqualTo(FpgSkillImpactPresentationEventType.Contact));
            Assert.That(
                output[1].Type,
                Is.EqualTo(FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(output[0].Correlation, Is.EqualTo(correlation));
            Assert.That(output[1].Correlation, Is.EqualTo(correlation));
            Assert.That(
                output[0].Contact.GroupKind,
                Is.EqualTo(FpgSkillImpactPresentationGroupKind.ImmediateAttack));
            Assert.That(
                output[0].Contact.ContactKind,
                Is.EqualTo(FpgSkillImpactContactKind.EnvironmentBlocked));
            Assert.That(output[0].Contact.ContactPoint, Is.EqualTo(contactPoint));
            Assert.That(output[0].Contact.HitPart, Is.EqualTo(HitPart.Body));
            Assert.That(output[0].Contact.ContactOrdinal, Is.Zero);
            Assert.That(
                output[0].Contact.TargetRuntimeId,
                Is.EqualTo(RuntimeId.Invalid));
            Assert.That(
                output[0].Contact.ProjectileId,
                Is.EqualTo(ProjectileId.Invalid));
            Assert.That(
                output[0].Contact.ImpactId,
                Is.EqualTo(ImpactId.Invalid));
            Assert.That(output[0].Contact.AttackId, Is.EqualTo(attackId));
            Assert.That(output[1].Completion.AttackId, Is.EqualTo(attackId));
            Assert.That(
                output[1].Completion.GroupKind,
                Is.EqualTo(FpgSkillImpactPresentationGroupKind.ImmediateAttack));
            Assert.That(
                fixture.Port.SkillImpactPresentation.LastSequence,
                Is.EqualTo(2L));
        }

        [Test]
        public void CommittedEnemyTimedImpactPublishesExactTargetPointAndCompletion()
        {
            PortFixture fixture = new PortFixture();
            RuntimeId ownerId = fixture.RegisterEnemy(100);
            TickIndex tick = new TickIndex(0L);
            SkillExecutionId executionId = new SkillExecutionId(46L);
            SpatialVectorKey targetPoint = new SpatialVectorKey(100, 200, 300);
            ThreatPayloadDefinition threatPayload =
                ThreatPayloadDefinition.TimedImpact(
                    new DamageSpec(0, 0),
                    ThreatTargetPolicy.PlayerCombatant,
                    TickDuration.Zero,
                    presentationKey: 1,
                    presentationKind:
                        FpgThreatPresentationKind.HeavyWeakpoint);
            FpgEnemyAttackPayload payload = FpgEnemyAttackPayload.ForThreat(
                new ThreatDefinition(
                    definitionId: 47,
                    telegraphDuration: TickDuration.Zero,
                    windupDuration: TickDuration.Zero,
                    recoveryDuration: TickDuration.Zero,
                    payload: threatPayload));
            FpgAttackScheduleRequest schedule =
                new FpgAttackScheduleRequest(
                    ownerId,
                    tick,
                    priority: 0,
                    scheduleSequence: 0L,
                    attackPatternId: "timed-impact-presentation",
                    skillExecutionId: executionId,
                    gameplayEventId: 48);
            FpgEnemyAttackSpatialContext spatial =
                new FpgEnemyAttackSpatialContext(
                    tick,
                    FpgSkillTargetSource.CurrentTarget,
                    socketId: 0,
                    new FpgSkillOffset(0, 0, 0),
                    fixture.PlayerId,
                    SpatialVectorKey.Zero,
                    targetPoint);

            Assert.That(
                fixture.Port.TrySubmitEnemyAttack(
                    new FpgEnemyAttackCommand(
                        schedule,
                        spawnSequence: 0,
                        payload: payload,
                        capacityReservation:
                            FpgEnemySkillCapacityReservation.Invalid,
                        projectileBudgetReservation:
                            default(ReservationToken),
                        spatialContext: spatial)).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.ImpactResolution,
                    tick,
                    new FpgEnemyRoster(1)).IsSuccess,
                Is.True);

            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[2];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);

            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(output[0].Type,
                Is.EqualTo(FpgSkillImpactPresentationEventType.Contact));
            Assert.That(output[0].Contact.ContactPoint, Is.EqualTo(targetPoint));
            Assert.That(output[0].Contact.TargetRuntimeId,
                Is.EqualTo(fixture.PlayerId));
            Assert.That(output[1].Type,
                Is.EqualTo(FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(output[1].Correlation.SourceRuntimeId,
                Is.EqualTo(ownerId));
            Assert.That(output[1].Correlation.SkillExecutionId,
                Is.EqualTo(executionId));
        }

        [Test]
        public void NonCollisionProjectileTerminalsOnlyCloseAfterWholeGroupEnds()
        {
            PortFixture fixture = new PortFixture();
            TickIndex tick = new TickIndex(0L);
            FpgEnemyRoster roster = new FpgEnemyRoster(1);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.LifecycleBoundary,
                    tick,
                    roster).IsSuccess,
                Is.True);
            FpgPlayerAreaProjectileRequest request =
                fixture.CreateProjectileRequest(
                    tick,
                    new SkillExecutionId(51L),
                    52);

            Assert.That(
                fixture.Port.TrySpawnPlayerAreaProjectile(
                    request,
                    out RuntimeId first).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.TrySpawnPlayerAreaProjectile(
                    request,
                    out RuntimeId second).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.TryCancelPlayerAreaProjectile(first, tick)
                    .IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.SkillImpactPresentation.LastSequence,
                Is.Zero);

            Assert.That(
                fixture.Port.TryCancelPlayerAreaProjectile(second, tick)
                    .IsSuccess,
                Is.True);
            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[4];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);

            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(
                output[0].Type,
                Is.EqualTo(
                    FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(
                output[0].GroupKind,
                Is.EqualTo(FpgSkillImpactPresentationGroupKind.Projectile));
        }

        [Test]
        public void SessionEndClearsContactsAndPublishesOnlyProjectileGroupClosure()
        {
            PortFixture fixture = new PortFixture();
            TickIndex tick = new TickIndex(0L);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.LifecycleBoundary,
                    tick,
                    new FpgEnemyRoster(1)).IsSuccess,
                Is.True);
            FpgPlayerAreaProjectileRequest request =
                fixture.CreateProjectileRequest(
                    tick,
                    new SkillExecutionId(71L),
                    72);
            Assert.That(
                fixture.Port.TrySpawnPlayerAreaProjectile(
                    request,
                    out RuntimeId first).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.TrySpawnPlayerAreaProjectile(
                    request,
                    out RuntimeId second).IsSuccess,
                Is.True);

            fixture.Port.ClearAll();

            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[4];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);
            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(
                output[0].Type,
                Is.EqualTo(
                    FpgSkillImpactPresentationEventType.GroupCompleted));
            Assert.That(
                output[0].Correlation.GameplayEventId,
                Is.EqualTo(72));
        }

        [Test]
        public void EnvironmentCollisionPublishesExactPointBeforeProjectileCompletion()
        {
            SpatialVectorKey collisionPoint =
                new SpatialVectorKey(100, 200, 300);
            PortFixture fixture = new PortFixture(
                new EnvironmentProjectileWorldPort(collisionPoint),
                new EmptyAreaQueryPort());
            TickIndex spawnTick = new TickIndex(0L);
            FpgEnemyRoster roster = new FpgEnemyRoster(1);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.LifecycleBoundary,
                    spawnTick,
                    roster).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.TrySpawnPlayerAreaProjectile(
                    fixture.CreateProjectileRequest(
                        spawnTick,
                        new SkillExecutionId(61L),
                        62),
                    out RuntimeId ignored).IsSuccess,
                Is.True);

            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.ThreatAndProjectileAdvance,
                    new TickIndex(1L),
                    roster).IsSuccess,
                Is.True);
            FpgSkillImpactPresentationEvent[] output =
                new FpgSkillImpactPresentationEvent[4];
            int count = fixture.Port.SkillImpactPresentation.CopyAfter(
                0L,
                output,
                out bool hasGap);

            Assert.That(hasGap, Is.False);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(
                output[0].Contact.ContactKind,
                Is.EqualTo(
                    FpgSkillImpactContactKind.EnvironmentBlocked));
            Assert.That(
                output[0].Contact.ContactPoint,
                Is.EqualTo(collisionPoint));
            Assert.That(
                output[1].Type,
                Is.EqualTo(
                    FpgSkillImpactPresentationEventType.GroupCompleted));
        }

        private sealed class PortFixture
        {
            private readonly SessionIdAllocator ids =
                new SessionIdAllocator();
            private int nextSpawnSequence;

            public PortFixture(
                IProjectileWorldPort projectileWorldPort = null,
                IPlayerProjectileAreaQueryPort areaQueryPort = null)
            {
                PlayerId = ids.NextRuntimeId();
                CombatKernel kernel = new CombatKernel(
                    projectileBudgetCapacity: 8,
                    impactCapacity: 16,
                    shotTargetCapacity: 8,
                    impactQueueCapacity: 16,
                    traceCapacity: 128,
                    projectileReservationCapacity: 8);
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
                    kernel,
                    player,
                    ids,
                    new FpgMultiEnemyCombatCapacity(
                        enemyCapacity: 4,
                        playerHitCommandCapacity: 8,
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
                    projectileWorldPort ?? new FpgEmptyProjectileWorldPort(),
                    RejectingSummonSink.Instance,
                    playerProjectileAreaQueryPort: areaQueryPort);
            }

            public RuntimeId PlayerId { get; }
            public FpgMultiEnemyCombatPort Port { get; }

            public RuntimeId RegisterEnemy(int life)
            {
                RuntimeId runtimeId = ids.NextRuntimeId();
                Assert.That(
                    Port.TryRegisterEnemy(
                        new FpgEnemyCombatantRegistration(
                            runtimeId,
                            nextSpawnSequence++,
                            life,
                            0,
                            new TickDuration(3),
                            new TickIndex(0L))).IsSuccess,
                    Is.True);
                return runtimeId;
            }

            public FpgPlayerHitCommand CreateHitCommand(
                long commandSequence,
                long impactId,
                RuntimeId targetId,
                SkillExecutionId executionId,
                int gameplayEventId,
                int impactOrdinal,
                HitPart hitPart)
            {
                return new FpgPlayerHitCommand(
                    commandSequence,
                    new ImpactIntent(
                        new ImpactId(impactId),
                        new AttackId(100L),
                        new ShotId(101L),
                        PlayerId,
                        targetId,
                        new TickIndex(0L),
                        new DamageSpec(0, 0),
                        hitPart,
                        DamageType.Normal,
                        CombatTags.Primary,
                        pelletIndex: impactOrdinal,
                        impactOrdinal: impactOrdinal,
                        spatialContext: new ImpactSpatialContext(
                            new SpatialVectorKey(
                                impactOrdinal + 1,
                                impactOrdinal + 2,
                                impactOrdinal + 3),
                            new GeometryId(impactOrdinal + 1),
                            QueryTargetKind.Combatant,
                            hitPart)),
                    executionId,
                    gameplayEventId);
            }

            public FpgPlayerAreaProjectileRequest CreateProjectileRequest(
                TickIndex tick,
                SkillExecutionId executionId,
                int gameplayEventId)
            {
                AttackSnapshot attack = new AttackSnapshot(
                    new AttackId(200L),
                    new ShotId(201L),
                    202,
                    PlayerId,
                    Team.Player,
                    tick,
                    new DamageSpec(1, 0),
                    QueryPolicy.DirectThenArea,
                    1,
                    1,
                    1,
                    1,
                    AttackQueryMode.AreaAtFirstSurface,
                    0,
                    1,
                    0,
                    AttackTargetKinds.Combatant);
                ProjectileDefinition definition = new ProjectileDefinition(
                    203,
                    new TickDuration(2),
                    new TickDuration(3),
                    attack.DamageSpec,
                    0,
                    false,
                    1,
                    1);
                return new FpgPlayerAreaProjectileRequest(
                    tick,
                    attack,
                    definition,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(0, 0, 1000),
                    executionId,
                    gameplayEventId);
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
            public static readonly RejectingSummonSink Instance =
                new RejectingSummonSink();

            public FpgSummonQueueAck TryQueueSummon(
                FpgSummonRequest request,
                TickIndex tick)
            {
                return FpgSummonQueueAck.Rejected(RejectReason.InvalidState);
            }
        }

        private sealed class EmptyAreaQueryPort :
            IPlayerProjectileAreaQueryPort
        {
            public DomainResult QueryAreaAtPoint(
                in PlayerProjectileAreaQueryRequest request,
                QueryCandidate[] output,
                out AttackQueryResult result)
            {
                result = AttackQueryResult.Empty;
                return DomainResult.Success;
            }
        }

        private sealed class EnvironmentProjectileWorldPort :
            IProjectileWorldPort
        {
            private readonly SpatialVectorKey collisionPoint;

            public EnvironmentProjectileWorldPort(
                SpatialVectorKey collisionPoint)
            {
                this.collisionPoint = collisionPoint;
            }

            public DomainResult Register(
                in ProjectileSpawnRequest request,
                out ProjectilePathSnapshot path)
            {
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
                hit = ProjectileSweepHit.EnvironmentBlocked(
                    new GeometryId(1),
                    1,
                    collisionPoint);
                return DomainResult.Success;
            }

            public DomainResult Release(in ProjectileReleaseRequest request)
            {
                return DomainResult.Success;
            }
        }
    }
}
