using System;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;

using FPG.Demo.Skills;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgMultiEnemyCombatTransactionTests
    {
        [Test]
        public void BatchCapacityFailureQueuesNoPartialCommands()
        {
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 1,
                impactHistoryCapacity: 4,
                impactQueueCapacity: 2);
            RuntimeId first = fixture.RegisterEnemy(100);
            RuntimeId second = fixture.RegisterEnemy(100);
            QueryCandidate[] candidates =
            {
                CreateCandidate(first, 1, 0),
                CreateCandidate(second, 2, 1)
            };
            FpgPlayerHitCommand[] commands =
            {
                fixture.CreateCommand(0L, 1L, first, new TickIndex(0L)),
                fixture.CreateCommand(1L, 2L, second, new TickIndex(0L))
            };

            DomainResult preflight = fixture.Port.ValidatePlayerHitBatch(
                fixture.PlayerId,
                new TickIndex(0L),
                candidates,
                candidates.Length,
                0L);
            DomainResult submitted = fixture.Port.TrySubmitPlayerHits(
                commands,
                commands.Length);

            AssertAll(() =>
            {
                Assert.That(preflight.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(submitted.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(fixture.Port.PendingPlayerHitCount, Is.Zero);
            });
        }

        [Test]
        public void InvalidSecondHitQueuesNoValidPrefix()
        {
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 2,
                impactHistoryCapacity: 4,
                impactQueueCapacity: 2);
            RuntimeId liveTarget = fixture.RegisterEnemy(100);
            RuntimeId missingTarget = new RuntimeId(999L);
            FpgPlayerHitCommand[] commands =
            {
                fixture.CreateCommand(0L, 1L, liveTarget, new TickIndex(0L)),
                fixture.CreateCommand(1L, 2L, missingTarget, new TickIndex(0L))
            };

            DomainResult submitted = fixture.Port.TrySubmitPlayerHits(
                commands,
                commands.Length);

            AssertAll(() =>
            {
                Assert.That(submitted.RejectReason, Is.EqualTo(RejectReason.InvalidTarget));
                Assert.That(fixture.Port.PendingPlayerHitCount, Is.Zero);
            });
        }

        [Test]
        public void ValidMultipleHitBatchQueuesAllCommandsTogether()
        {
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 2,
                impactHistoryCapacity: 4,
                impactQueueCapacity: 2);
            RuntimeId first = fixture.RegisterEnemy(100);
            RuntimeId second = fixture.RegisterEnemy(100);
            FpgPlayerHitCommand[] commands =
            {
                fixture.CreateCommand(0L, 1L, first, new TickIndex(0L)),
                fixture.CreateCommand(1L, 2L, second, new TickIndex(0L))
            };

            DomainResult submitted = fixture.Port.TrySubmitPlayerHits(
                commands,
                commands.Length);

            Assert.That(submitted.IsSuccess, Is.True);
            Assert.That(fixture.Port.PendingPlayerHitCount, Is.EqualTo(2));
        }

        [Test]
        public void PlayerHitCompensationRemovesOnlyTailAndAllowsSequenceReuse()
        {
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 3,
                impactHistoryCapacity: 4,
                impactQueueCapacity: 3);
            RuntimeId first = fixture.RegisterEnemy(100);
            RuntimeId second = fixture.RegisterEnemy(100);
            FpgPlayerHitCommand[] committed =
            {
                fixture.CreateCommand(0L, 1L, first, new TickIndex(0L))
            };
            FpgPlayerHitCommand[] compensated =
            {
                fixture.CreateCommand(1L, 2L, second, new TickIndex(0L))
            };

            Assert.That(
                fixture.Port.TrySubmitPlayerHits(committed, 1).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.TrySubmitPlayerHits(compensated, 1).IsSuccess,
                Is.True);
            Assert.That(
                fixture.Port.TryCompensatePlayerHitBatch(compensated, 1)
                    .IsSuccess,
                Is.True);
            Assert.That(fixture.Port.PendingPlayerHitCount, Is.EqualTo(1));
            Assert.That(
                fixture.Port.TrySubmitPlayerHits(compensated, 1).IsSuccess,
                Is.True);
            Assert.That(fixture.Port.PendingPlayerHitCount, Is.EqualTo(2));
            Assert.That(
                fixture.Port.TryCompensatePlayerHitBatch(committed, 1)
                    .RejectReason,
                Is.EqualTo(RejectReason.InvalidState));
        }

        [Test]
        public void EnemyAttackCompensationRemovesScheduleAndInlinePayload()
        {
            RetryThenQueueSummonSink summonSink =
                new RetryThenQueueSummonSink();
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 1,
                impactHistoryCapacity: 2,
                impactQueueCapacity: 1,
                summonRequestSink: summonSink);
            RuntimeId ownerId = fixture.RegisterEnemy(100);
            FpgSummonRequest request = new FpgSummonRequest(
                ownerId,
                "compensated-child",
                recursionDepth: 1,
                requestSequence: 0L,
                summonActionId: "compensated-action",
                maxSummonsPerOwner: 1,
                occupancyMode: FpgSummonOccupancyMode.AdditionalEntity,
                placementMode: FpgSummonPlacementMode.EncounterSpawnPoint);
            FpgEnemyAttackPayload payload = FpgEnemyAttackPayload.ForSummon(
                new FpgFormalSummonPayload(request, maxSummonsPerOwner: 1));
            FpgAttackScheduleRequest schedule =
                new FpgAttackScheduleRequest(
                    ownerId,
                    new TickIndex(0L),
                    priority: 0,
                    scheduleSequence: 0L,
                    attackPatternId: "compensated-summon",
                    skillExecutionId: new SkillExecutionId(1L),
                    gameplayEventId: 1);
            FpgEnemyAttackSpatialContext spatial =
                new FpgEnemyAttackSpatialContext(
                    new TickIndex(0L),
                    FpgSkillTargetSource.CurrentTarget,
                    0,
                    new FpgSkillOffset(0, 0, 0),
                    fixture.PlayerId,
                    SpatialVectorKey.Zero,
                    SpatialVectorKey.Zero);
            FpgEnemyAttackCommand command = new FpgEnemyAttackCommand(
                schedule,
                spawnSequence: 0,
                payload,
                FpgEnemySkillCapacityReservation.Invalid,
                default(ReservationToken),
                spatial);

            Assert.That(fixture.Port.TrySubmitEnemyAttack(command).IsSuccess,
                Is.True);
            Assert.That(fixture.Port.PendingAttackCount, Is.EqualTo(1));
            Assert.That(
                fixture.Port.TryCompensateSummonAttack(0L).IsSuccess,
                Is.True);
            Assert.That(fixture.Port.PendingAttackCount, Is.Zero);
            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.EnemyAttackDirector,
                    new TickIndex(0L),
                    new FpgEnemyRoster(1)).IsSuccess,
                Is.True);
            Assert.That(summonSink.CallCount, Is.Zero);
            Assert.That(
                fixture.Port.TryCompensateSummonAttack(0L).RejectReason,
                Is.EqualTo(RejectReason.InvalidTarget));
        }

        [Test]
        public void EncounterCompletionReopensImpactLedgerForLongSessions()
        {
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 1,
                impactHistoryCapacity: 1,
                impactQueueCapacity: 1);
            RuntimeId enemyId = fixture.RegisterEnemy(100);
            FpgEnemyRoster roster = new FpgEnemyRoster(1);

            for (int value = 0; value < 12; value++)
            {
                TickIndex tick = new TickIndex(value);
                FpgPlayerHitCommand[] command =
                {
                    fixture.CreateCommand(value, value + 1L, enemyId, tick)
                };
                Assert.That(
                    fixture.Port.TrySubmitPlayerHits(command, 1).IsSuccess,
                    Is.True,
                    "submit tick " + value);
                Assert.That(
                    fixture.Port.Process(
                        FpgBattleTickPhase.PlayerAttackAndHit,
                        tick,
                        roster).IsSuccess,
                    Is.True,
                    "resolve tick " + value);
                Assert.That(fixture.Kernel.ImpactLedger.Count, Is.EqualTo(1));
                Assert.That(
                    fixture.Port.Process(
                        FpgBattleTickPhase.EncounterCompletion,
                        tick,
                        roster).IsSuccess,
                    Is.True,
                    "complete tick " + value);
                Assert.That(fixture.Kernel.ImpactLedger.Count, Is.Zero);
            }

            Assert.That(
                fixture.Port.TryGetEnemyRuntime(enemyId, out var enemy),
                Is.True);
            Assert.That(enemy.Combatant.Life, Is.EqualTo(88));
        }

        [Test]
        public void HealthChangedSubscriberFailureIsDiagnosticOnly()
        {
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 1,
                impactHistoryCapacity: 2,
                impactQueueCapacity: 1);
            RuntimeId enemyId = fixture.RegisterEnemy(100);
            int healthySubscriberCalls = 0;
            fixture.Port.HealthChanged += _ => throw new InvalidOperationException("test");
            fixture.Port.HealthChanged += _ => healthySubscriberCalls++;
            FpgPlayerHitCommand[] command =
            {
                fixture.CreateCommand(0L, 1L, enemyId, new TickIndex(0L))
            };
            Assert.That(fixture.Port.TrySubmitPlayerHits(command, 1).IsSuccess, Is.True);

            DomainResult processed = fixture.Port.Process(
                FpgBattleTickPhase.PlayerAttackAndHit,
                new TickIndex(0L),
                new FpgEnemyRoster(1));

            Assert.That(
                fixture.Port.TryGetEnemyRuntime(enemyId, out var enemy),
                Is.True);
            AssertAll(() =>
            {
                Assert.That(processed.IsSuccess, Is.True);
                Assert.That(enemy.Combatant.Life, Is.EqualTo(99));
                Assert.That(healthySubscriberCalls, Is.EqualTo(1));
                Assert.That(fixture.Port.PresentationCallbackFaultCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void DieAfterSuccessfulSummonRetriesBeforeKillingOwnerAndNotifiesOnce()
        {
            RetryThenQueueSummonSink summonSink = new RetryThenQueueSummonSink();
            PortFixture fixture = new PortFixture(
                playerHitCapacity: 1,
                impactHistoryCapacity: 2,
                impactQueueCapacity: 1,
                summonRequestSink: summonSink);
            RuntimeId ownerId = fixture.RegisterEnemy(100);
            FpgSummonRequest summonRequest = new FpgSummonRequest(
                ownerId,
                "generic-child",
                recursionDepth: 1,
                requestSequence: 0L,
                summonActionId: "generic-replacement",
                maxSummonsPerOwner: 0,
                occupancyMode: FpgSummonOccupancyMode.ReplaceOwner,
                placementMode: FpgSummonPlacementMode.OwnerPosition);
            FpgEnemyAttackPayload payload = FpgEnemyAttackPayload.ForSummon(
                new FpgFormalSummonPayload(
                    summonRequest,
                    maxSummonsPerOwner: 0,
                    releaseDelayTicks: 0,
                    ownerOutcome: FpgSummonOwnerOutcome.DieAfterSuccessfulSummon));
            FpgAttackScheduleRequest schedule =
                new FpgAttackScheduleRequest(
                    ownerId,
                    new TickIndex(0L),
                    priority: 0,
                    scheduleSequence: 0L,
                    attackPatternId: "generic-summon",
                    skillExecutionId: new SkillExecutionId(1L),
                    gameplayEventId: 1);
            FpgEnemyAttackSpatialContext spatialContext =
                new FpgEnemyAttackSpatialContext(
                    new TickIndex(0L),
                    FpgSkillTargetSource.CurrentTarget,
                    socketId: 0,
                    new FpgSkillOffset(0, 0, 0),
                    fixture.PlayerId,
                    SpatialVectorKey.Zero,
                    SpatialVectorKey.Zero);
            FpgEnemyAttackCommand command = new FpgEnemyAttackCommand(
                schedule,
                spawnSequence: 0,
                payload,
                FpgEnemySkillCapacityReservation.Invalid,
                default(ReservationToken),
                spatialContext);
            FpgEnemyRoster roster = new FpgEnemyRoster(1);
            int enemyDiedCount = 0;
            int summonRequestedCount = 0;
            FpgEnemyDiedEvent died = default(FpgEnemyDiedEvent);
            fixture.Port.EnemyDied += value =>
            {
                enemyDiedCount++;
                died = value;
            };
            fixture.Port.SummonRequested += _ => throw new InvalidOperationException("test");
            fixture.Port.SummonRequested += _ => summonRequestedCount++;

            Assert.That(fixture.Port.TrySubmitEnemyAttack(command).IsSuccess, Is.True);

            DomainResult retry = fixture.Port.Process(
                FpgBattleTickPhase.EnemyAttackDirector,
                new TickIndex(0L),
                roster);

            Assert.That(fixture.Port.TryGetEnemyRuntime(ownerId, out var ownerAfterRetry), Is.True);
            AssertAll(() =>
            {
                Assert.That(retry.IsSuccess, Is.True);
                Assert.That(summonSink.CallCount, Is.EqualTo(1));
                Assert.That(ownerAfterRetry.Combatant.IsDead, Is.False);
                Assert.That(fixture.Port.CanAttack(ownerId), Is.True);
                Assert.That(fixture.Port.PendingAttackCount, Is.EqualTo(1));
                Assert.That(enemyDiedCount, Is.Zero);
                Assert.That(summonRequestedCount, Is.Zero);
            });

            DomainResult queued = fixture.Port.Process(
                FpgBattleTickPhase.EnemyAttackDirector,
                new TickIndex(1L),
                roster);

            Assert.That(fixture.Port.TryGetEnemyRuntime(ownerId, out var ownerAfterQueue), Is.True);
            AssertAll(() =>
            {
                Assert.That(queued.IsSuccess, Is.True);
                Assert.That(summonSink.CallCount, Is.EqualTo(2));
                Assert.That(summonSink.LastRequest.OwnerRuntimeId, Is.EqualTo(ownerId));
                Assert.That(summonSink.LastRequest.EnemyDefinitionId, Is.EqualTo("generic-child"));
                Assert.That(
                    summonSink.LastRequest.OccupancyMode,
                    Is.EqualTo(FpgSummonOccupancyMode.ReplaceOwner));
                Assert.That(
                    summonSink.LastRequest.PlacementMode,
                    Is.EqualTo(FpgSummonPlacementMode.OwnerPosition));
                Assert.That(
                    summonSink.LastRequest.SummonActionId,
                    Is.EqualTo("generic-replacement"));
                Assert.That(ownerAfterQueue.Combatant.IsDead, Is.True);
                Assert.That(fixture.Port.CanAttack(ownerId), Is.False);
                Assert.That(fixture.Port.PendingAttackCount, Is.Zero);
                Assert.That(enemyDiedCount, Is.EqualTo(1));
                Assert.That(summonRequestedCount, Is.EqualTo(1));
                Assert.That(fixture.Port.PresentationCallbackFaultCount, Is.EqualTo(1));
                Assert.That(died.RuntimeId, Is.EqualTo(ownerId));
                Assert.That(died.Tick, Is.EqualTo(new TickIndex(1L)));
            });

            Assert.That(
                fixture.Port.Process(
                    FpgBattleTickPhase.EnemyAttackDirector,
                    new TickIndex(2L),
                    roster).IsSuccess,
                Is.True);
            Assert.That(enemyDiedCount, Is.EqualTo(1));
            Assert.That(summonRequestedCount, Is.EqualTo(1));
        }


        [TestCase(8, 8, 136, 128, 128, true)]
        [TestCase(8, 7, 136, 128, 128, false)]
        [TestCase(8, 8, 136, 7, 128, false)]
        [TestCase(8, 8, 136, 128, 7, false)]
        [TestCase(8, 8, 135, 128, 128, false)]
        public void FormalFactoryComposesMaximumReleaseWithFixedCapacities(
            int maximumImpactCount,
            int commandCapacity,
            int ledgerCapacity,
            int queueCapacity,
            int queryCapacity,
            bool expected)
        {
            MethodInfo method = typeof(FpgFormalCombatPortFactory).GetMethod(
                "TryValidatePlayerAttackCapacity",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                maximumImpactCount,
                commandCapacity,
                ledgerCapacity,
                queueCapacity,
                queryCapacity,
                null
            };

            bool actual = (bool)method.Invoke(null, arguments);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(
                string.IsNullOrEmpty((string)arguments[5]),
                Is.EqualTo(expected));
        }

        private static QueryCandidate CreateCandidate(
            RuntimeId targetId,
            int geometryId,
            int ordinal)
        {
            return new QueryCandidate(
                AttackQueryStage.Pellet,
                0,
                targetId,
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(geometryId),
                100 + ordinal,
                SpatialVectorKey.Zero,
                ordinal);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }

        private sealed class PortFixture
        {
            private readonly SessionIdAllocator ids = new SessionIdAllocator();
            private int nextSpawnSequence;

            public PortFixture(
                int playerHitCapacity,
                int impactHistoryCapacity,
                int impactQueueCapacity,
                IFpgSummonRequestSink summonRequestSink = null)
            {
                PlayerId = ids.NextRuntimeId();
                Kernel = new CombatKernel(
                    projectileBudgetCapacity: 4,
                    impactCapacity: impactHistoryCapacity,
                    shotTargetCapacity: 4,
                    impactQueueCapacity: impactQueueCapacity,
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
                        playerHitCommandCapacity: playerHitCapacity,
                        attackScheduleCapacity: 4,
                        projectileCapacity: 4,
                        threatAdvanceCapacity: 4,
                        perEnemyThreatCapacity: 2,
                        summonCapacity: 2,
                        maxTotalSummons: 2,
                        maxSummonRecursionDepth: 1,
                        vitalsEventCapacity: 64,
                        damageFeedbackCapacity: 64),
                    new TickDuration(3),
                    new FpgEmptyProjectileWorldPort(),
                    summonRequestSink ?? new RejectingSummonSink());
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

            public FpgPlayerHitCommand CreateCommand(
                long commandSequence,
                long impactId,
                RuntimeId targetId,
                TickIndex tick)
            {
                return new FpgPlayerHitCommand(
                    commandSequence,
                    new ImpactIntent(
                        new ImpactId(impactId),
                        new AttackId(1L),
                        new ShotId(1L),
                        PlayerId,
                        targetId,
                        tick,
                        new DamageSpec(1, 0),
                        HitPart.Body,
                        DamageType.Normal,
                        CombatTags.Primary),
                    ImpactPhasePriority.PlayerCombatantHit);
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

        private sealed class RetryThenQueueSummonSink : IFpgSummonRequestSink
        {
            public int CallCount { get; private set; }
            public FpgSummonRequest LastRequest { get; private set; }

            public FpgSummonQueueAck TryQueueSummon(
                FpgSummonRequest request,
                TickIndex tick)
            {
                CallCount++;
                LastRequest = request;
                return CallCount == 1
                    ? FpgSummonQueueAck.Retry(RejectReason.BudgetExceeded)
                    : FpgSummonQueueAck.Queued;
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
    }
}
