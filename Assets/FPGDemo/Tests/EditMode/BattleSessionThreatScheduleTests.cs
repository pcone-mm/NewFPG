using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattleSessionThreatScheduleTests
    {
        private const string BurstbugScenarioAssetPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset";

        [Test]
        public void DueEntriesAreSortedStartedInTheCurrentTickAndRemainOutsideExternalThreatCommands()
        {
            ThreatScheduleEntry[] schedule =
            {
                CreateSweptEntry(20, 0, 220),
                CreateSweptEntry(10, 0, 210)
            };
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(CreateScenario(schedule), world))
            {
                Start(session);

                PumpAiming(session);

                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(2));
                Assert.That(session.PendingThreatScheduleSequence, Is.Zero);
                Assert.That(session.ThreatCount, Is.EqualTo(2));
                Assert.That(session.GetThreatSnapshot(0).DefinitionId, Is.EqualTo(210));
                Assert.That(session.GetThreatSnapshot(1).DefinitionId, Is.EqualTo(220));
                Assert.That(session.ThreatCommandCount, Is.Zero);
                Assert.That(session.ThreatScheduleDecisionCount, Is.EqualTo(6));
                Assert.That(world.RegisterCount, Is.EqualTo(2));
                Assert.That(world.GetRegisterCall(0).Tick.Value, Is.Zero);
                Assert.That(world.GetRegisterCall(1).Tick.Value, Is.Zero);
            }
        }

        [Test]
        public void TimedImpactUsesNoProjectileResourcesAndResolvesAtItsConfiguredTick()
        {
            ThreatScheduleEntry[] schedule =
            {
                CreateTimedImpactEntry(1, 0, 301, damage: 7, delayTicks: 0),
                CreateTimedImpactEntry(2, 0, 302, damage: 11, delayTicks: 2)
            };
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(CreateScenario(schedule), world))
            {
                Start(session);
                int initialLife = session.GetFinalSnapshot().PlayerLife;

                PumpAiming(session);
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialLife - 7));
                Assert.That(session.PendingImpactCount, Is.EqualTo(1));
                Assert.That(world.RegisterCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);

                PumpAiming(session);
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialLife - 7));
                Assert.That(session.PendingImpactCount, Is.EqualTo(1));

                PumpAiming(session);
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialLife - 18));
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
        }

        [Test]
        public void BarrierHitDuringReloadDoesNotCancelIt()
        {
            ThreatScheduleEntry[] schedule =
            {
                CreateTimedImpactEntry(1, 5, 303, damage: 10, delayTicks: 0)
            };
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(CreateScenario(schedule), world))
            {
                Start(session);
                int initialLife = session.GetFinalSnapshot().PlayerLife;

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, primaryHeld: true));
                CombatLabHarness.PumpTicks(
                    session,
                    3,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true));
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Ready));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(7));

                CombatLabHarness.PumpOneTick(session, CreateReloadFrame);
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Reloading));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Reloading));
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialLife));
                Assert.That(session.GetFinalSnapshot().PlayerBarrier, Is.LessThan(60));

                CombatLabHarness.PumpTicks(session, 11);
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Ready));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(8));
            }
        }

        [Test]
        public void DepletedBarrierAllowsReloadAndLifeHitCancelsWithoutRefill()
        {
            ThreatScheduleEntry[] schedule =
            {
                CreateTimedImpactEntry(1, 4, 304, damage: 120, delayTicks: 0),
                CreateTimedImpactEntry(2, 6, 305, damage: 5, delayTicks: 0)
            };
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(CreateScenario(schedule), world))
            {
                Start(session);
                int initialLife = session.GetFinalSnapshot().PlayerLife;

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, primaryHeld: true));
                CombatLabHarness.PumpTicks(
                    session,
                    3,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true));
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Ready));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(7));

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.GetFinalSnapshot().PlayerBarrier, Is.Zero);
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialLife));

                CombatLabHarness.PumpOneTick(session, CreateReloadFrame);
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Reloading));
                Assert.That(session.PlayerExposureState, Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(session.GetFinalSnapshot().PlayerBarrier, Is.Zero);

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.PlayerWeaponState, Is.EqualTo(WeaponState.Ready));
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialLife - 5));
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(7));

                CombatLabHarness.PumpTicks(session, 11);
                Assert.That(session.GetFinalSnapshot().PlayerAmmo, Is.EqualTo(7));
            }
        }

        [Test]
        public void BudgetBlockedScheduleEntryHoldsCursorUntilEarlierProjectileReleases()
        {
            ThreatScheduleEntry[] schedule =
            {
                CreateSweptEntry(1, 0, 401, flightTicks: 2),
                CreateSweptEntry(2, 0, 402, flightTicks: 2)
            };
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);
            ScenarioDefinition definition = CreateScenario(
                schedule,
                projectileBudgetCapacity: 1,
                projectileCapacity: 1);

            using (BattleSession session = CreateSession(definition, world))
            {
                Start(session);

                PumpAiming(session);
                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(1));
                Assert.That(session.PendingThreatScheduleSequence, Is.EqualTo(2));
                Assert.That(session.ThreatScheduleDecisionCount, Is.EqualTo(4));
                Assert.That(world.RegisterCount, Is.EqualTo(1));

                PumpAiming(session);
                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(1));
                Assert.That(session.ThreatScheduleDecisionCount, Is.EqualTo(5));
                Assert.That(world.RegisterCount, Is.EqualTo(1));

                PumpAiming(session);
                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(1));
                Assert.That(session.ThreatScheduleDecisionCount, Is.EqualTo(6));
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);

                PumpAiming(session);
                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(2));
                Assert.That(session.PendingThreatScheduleSequence, Is.Zero);
                Assert.That(session.ThreatScheduleDecisionCount, Is.EqualTo(9));
                Assert.That(world.RegisterCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void ThreatSnapshotsExposePresentationFieldsAndCopyInStableThreatSlotOrder()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(CreateScenario(null), world))
            {
                Start(session);

                Assert.That(session.TryAddThreat(
                    CreateSnapshotSweptThreat(),
                    out int firstThreatIndex).IsSuccess, Is.True);
                Assert.That(session.TryAddThreat(
                    CreateSnapshotTimedImpactThreat(),
                    out int secondThreatIndex).IsSuccess, Is.True);
                Assert.That(firstThreatIndex, Is.Zero);
                Assert.That(secondThreatIndex, Is.EqualTo(1));

                ThreatSnapshot[] undersized = new ThreatSnapshot[1];
                DomainResult capacity = session.CopyThreatSnapshots(undersized, out int requiredCount);
                Assert.That(capacity.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
                Assert.That(requiredCount, Is.EqualTo(2));

                ThreatSnapshot[] snapshots = new ThreatSnapshot[2];
                Assert.That(session.CopyThreatSnapshots(snapshots, out int copiedCount).IsSuccess, Is.True);
                Assert.That(copiedCount, Is.EqualTo(2));
                Assert.That(snapshots[0].RuntimeId,
                    Is.EqualTo(session.GetThreatSnapshot(firstThreatIndex).RuntimeId));
                Assert.That(snapshots[1].RuntimeId,
                    Is.EqualTo(session.GetThreatSnapshot(secondThreatIndex).RuntimeId));
                Assert.That(snapshots[0].DefinitionId, Is.EqualTo(820));
                Assert.That(snapshots[1].DefinitionId, Is.EqualTo(810));
                Assert.That(snapshots[0].PayloadKind,
                    Is.EqualTo(ThreatPayloadKind.SweptProjectile));
                Assert.That(snapshots[0].PresentationKey, Is.EqualTo(17));
                Assert.That(snapshots[0].TargetPolicy,
                    Is.EqualTo(ThreatTargetPolicy.PlayerCombatant));
                Assert.That(snapshots[1].PayloadKind,
                    Is.EqualTo(ThreatPayloadKind.TimedImpact));
                Assert.That(snapshots[1].PresentationKey, Is.EqualTo(23));
                Assert.That(snapshots[1].TargetPolicy,
                    Is.EqualTo(ThreatTargetPolicy.PlayerCombatant));
            }
        }

        [Test]
        public void TryStartThreatRecordsScheduledToTelegraphTransition()
        {
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(CreateScenario(null), world))
            {
                Start(session);
                Assert.That(session.TryAddThreat(
                    CreateSnapshotSweptThreat(),
                    out int threatIndex).IsSuccess, Is.True);

                ThreatSnapshot scheduled = session.GetThreatSnapshot(threatIndex);
                Assert.That(scheduled.State, Is.EqualTo(ThreatState.Scheduled));
                int traceCountBeforeStart = session.Trace.Count;

                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
                Assert.That(session.GetThreatSnapshot(threatIndex).State,
                    Is.EqualTo(ThreatState.Telegraph));

                bool foundTransition = false;
                for (int index = traceCountBeforeStart; index < session.Trace.Count; index++)
                {
                    CombatEvent combatEvent = session.Trace.GetOldest(index);
                    if (combatEvent.EventType != CombatEventType.ThreatStateChanged
                        || combatEvent.TargetId != scheduled.RuntimeId)
                    {
                        continue;
                    }

                    Assert.That(combatEvent.ValueBefore, Is.EqualTo((int)ThreatState.Scheduled));
                    Assert.That(combatEvent.ValueAfter, Is.EqualTo((int)ThreatState.Telegraph));
                    Assert.That(combatEvent.Tick.Value, Is.EqualTo(0));
                    foundTransition = true;
                    break;
                }

                Assert.That(foundTransition, Is.True);
            }
        }

        [Test]
        public void RecordedAndReplayedScheduleKeepsDecisionAndCanonicalDigestsStable()
        {
            ThreatScheduleEntry[] schedule = { CreateSweptEntry(1, 0, 501, flightTicks: 2) };
            ScenarioDefinition definition = CreateScenario(schedule);
            SpatialPortTranscript transcript = new SpatialPortTranscript(4, 1);
            ReplaySummary recorded;

            using (BattleSession session = CreateSession(
                definition,
                new RecordingProjectileWorldPort(
                    CombatLabHarness.CreateProjectileWorldPort(),
                    transcript),
                transcript))
            {
                Start(session);
                PumpAiming(session);
                PumpAiming(session);
                PumpAiming(session);
                recorded = session.GetReplaySummary();
                AssertSummaryCanonicalDigest(session, recorded);
            }

            Assert.That(transcript.Count, Is.EqualTo(4));
            Assert.That(recorded.SpatialDecisionDigest, Is.EqualTo(transcript.CanonicalDigest));
            transcript.ResetReplay();

            ReplaySummary replayed;
            using (BattleSession session = CreateSession(
                definition,
                new ReplayProjectileWorldPort(transcript),
                transcript))
            {
                Start(session);
                PumpAiming(session);
                PumpAiming(session);
                PumpAiming(session);
                replayed = session.GetReplaySummary();
                AssertSummaryCanonicalDigest(session, replayed);
            }

            Assert.That(transcript.ReplayRemaining, Is.Zero);
            Assert.That(replayed.ThreatScheduleDecisionCount,
                Is.EqualTo(recorded.ThreatScheduleDecisionCount));
            Assert.That(replayed.ThreatScheduleDecisionDigest,
                Is.EqualTo(recorded.ThreatScheduleDecisionDigest));
            Assert.That(replayed.SpatialDecisionDigest,
                Is.EqualTo(recorded.SpatialDecisionDigest));
            Assert.That(replayed.FinalSnapshot.PlayerLife,
                Is.EqualTo(recorded.FinalSnapshot.PlayerLife));
            Assert.That(replayed.FinalSnapshot.PlayerBarrier,
                Is.EqualTo(recorded.FinalSnapshot.PlayerBarrier));
            Assert.That(replayed.CanonicalDigest, Is.EqualTo(recorded.CanonicalDigest));
        }

        [Test]
        public void D0FirstHeavyWarningDefeatsAnExposedIdlePlayerWithoutProjectileBudget()
        {
            ScenarioDefinition definition = LoadBurstbugScenarioDefinition();

            D0NoHitProjectileWorldPort world = new D0NoHitProjectileWorldPort();
            using (BattleSession session = CreateSession(definition, world))
            {
                Start(session);
                for (int tick = 0; tick <= 675; tick++)
                {
                    try
                    {
                        Assert.That(CombatLabHarness.PumpOneTick(
                            session,
                            currentTick => PlayerInputFrame.Empty(currentTick, aimHeld: true)),
                            Is.EqualTo(1));
                    }
                    catch (System.InvalidOperationException exception)
                    {
                        Assert.Fail($"D0 idle run rejected at tick {tick}: {exception.Message}");
                    }
                }

                Assert.That(session.State, Is.EqualTo(BattleSessionState.Completed));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Defeat));
                Assert.That(session.CurrentTick.Value, Is.EqualTo(675));
                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(3));
                Assert.That(world.RegisterCount, Is.EqualTo(4));
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.Zero);
            }
        }

        [Test]
        public void D0FastScheduleRegistersExactlyOneNonInterceptableTwentyEightDamageProjectile()
        {
            ScenarioDefinition authored = LoadBurstbugScenarioDefinition();
            ThreatScheduleEntry entry = authored.GetThreatScheduleEntry(0);
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(
                CreateScenarioWithOnlyEntry(authored, entry),
                world))
            {
                Start(session);
                long releaseTick = GetReleaseTick(entry);

                PumpAimingThroughTick(session, releaseTick);

                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(1));
                Assert.That(session.ProjectileSlotCount, Is.EqualTo(1));
                Assert.That(world.RegisterCount, Is.EqualTo(1));
                Assert.That(world.GetRegisterCall(0).Tick.Value, Is.EqualTo(releaseTick));
                Assert.That(world.GetRegisterCall(0).DefinitionId, Is.EqualTo(301));
                Assert.That(world.GetRegisterCall(0).PresentationKey, Is.EqualTo(1));
                Assert.That(world.GetRegisterCall(0).Interceptable, Is.False);

                ProjectileSnapshot projectile = session.GetProjectileSnapshot(0);
                Assert.That(projectile.DefinitionId, Is.EqualTo(301));
                Assert.That(projectile.PresentationKey, Is.EqualTo(1));
                Assert.That(projectile.HitPoints, Is.Zero);
                Assert.That(projectile.State, Is.EqualTo(ProjectileState.Travelling));
                Assert.That(entry.Payload.ProjectileDefinition.DamageSpec.BaseDamage, Is.EqualTo(28));
                Assert.That(entry.Payload.ProjectileDefinition.Interceptable, Is.False);
            }
        }

        [Test]
        public void D0SlowTripleScheduleRegistersThreeInterceptableProjectiles()
        {
            ScenarioDefinition authored = LoadBurstbugScenarioDefinition();
            ThreatScheduleEntry entry = authored.GetThreatScheduleEntry(1);
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);

            using (BattleSession session = CreateSession(
                CreateScenarioWithOnlyEntry(authored, entry),
                world))
            {
                Start(session);
                long releaseTick = GetReleaseTick(entry);

                PumpAimingThroughTick(session, releaseTick);

                Assert.That(session.ThreatScheduleCursor, Is.EqualTo(1));
                Assert.That(session.ProjectileSlotCount, Is.EqualTo(3));
                Assert.That(world.RegisterCount, Is.EqualTo(3));
                Assert.That(entry.Payload.ProjectileDefinition.DamageSpec.BaseDamage, Is.EqualTo(12));
                Assert.That(entry.Payload.ProjectileDefinition.MaxHitPoints, Is.EqualTo(4));
                Assert.That(entry.Payload.ProjectileDefinition.Interceptable, Is.True);
                for (int index = 0; index < 3; index++)
                {
                    ProjectileSpawnRequest request = world.GetRegisterCall(index);
                    ProjectileSnapshot projectile = session.GetProjectileSnapshot(index);
                    Assert.That(request.Tick.Value, Is.EqualTo(releaseTick));
                    Assert.That(request.DefinitionId, Is.EqualTo(302));
                    Assert.That(request.PresentationKey, Is.EqualTo(2));
                    Assert.That(request.Interceptable, Is.True);
                    Assert.That(projectile.DefinitionId, Is.EqualTo(302));
                    Assert.That(projectile.PresentationKey, Is.EqualTo(2));
                    Assert.That(projectile.HitPoints, Is.EqualTo(4));
                    Assert.That(projectile.State, Is.EqualTo(ProjectileState.Travelling));
                }

                // The authored schedule owns this test. Focused projectile-world
                // tests cover player interception timing independently of an
                // encounter's finite travel window.
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
            }
        }

        [Test]
        public void D0HeavyWarningIsCanceledWhenWeakpointBreakStartsGroggyBeforeOriginalRelease()
        {
            ScenarioDefinition authored = LoadBurstbugScenarioDefinition();
            ThreatScheduleEntry entry = authored.GetThreatScheduleEntry(2);
            ScriptedProjectileWorldPort world = CombatLabHarness.CreateProjectileWorldPort(
                ScriptedProjectileSweepMode.None);
            AllPrimaryPelletsWeakpointPort attackPort = new AllPrimaryPelletsWeakpointPort();

            using (BattleSession session = CreateSession(
                CreateScenarioWithOnlyEntry(authored, entry),
                attackPort,
                world))
            {
                Start(session);
                int initialPlayerLife = session.GetFinalSnapshot().PlayerLife;
                long originalReleaseTick = GetReleaseTick(entry);

                PumpAimingThroughTick(session, entry.DueTick.Value);

                Assert.That(session.ThreatCount, Is.EqualTo(1));
                Assert.That(session.GetThreatSnapshot(0).State, Is.EqualTo(ThreatState.Telegraph));
                Assert.That(session.GetThreatSnapshot(0).HasReleased, Is.False);
                Assert.That(session.PendingImpactCount, Is.Zero);

                attackPort.TargetId = session.EnemyRuntimeId;
                while (attackPort.ResolveCount < 2
                    && session.CurrentTick.Value < originalReleaseTick)
                {
                    PumpAimingPrimary(session);
                }

                ThreatSnapshot canceled = session.GetThreatSnapshot(0);
                Assert.That(attackPort.ResolveCount, Is.EqualTo(2));
                Assert.That(session.CurrentTick.Value, Is.LessThan(originalReleaseTick));
                Assert.That(canceled.State, Is.EqualTo(ThreatState.Canceled));
                Assert.That(canceled.HasReleased, Is.False);
                Assert.That(session.GetFinalSnapshot().EnemyBreak, Is.Zero);
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(HasGroggyStarted(session.Trace, session.EnemyRuntimeId), Is.True);

                PumpAimingThroughTick(session, originalReleaseTick + 1L);

                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.None));
                Assert.That(session.GetThreatSnapshot(0).State, Is.EqualTo(ThreatState.Canceled));
                Assert.That(session.GetThreatSnapshot(0).HasReleased, Is.False);
                Assert.That(session.PendingImpactCount, Is.Zero);
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(initialPlayerLife));
                Assert.That(HasPlayerDamageAtOrAfter(
                    session.Trace,
                    session.PlayerRuntimeId,
                    originalReleaseTick), Is.False);
            }
        }

        private static BattleSession CreateSession(
            ScenarioDefinition definition,
            IProjectileWorldPort projectileWorldPort,
            ISpatialDigestView spatialDecisionView = null)
        {
            return new BattleSessionFactory().Create(
                definition,
                new NullAttackResolutionPort(),
                null,
                projectileWorldPort,
                spatialDecisionView);
        }

        private static BattleSession CreateSession(
            ScenarioDefinition definition,
            IAttackResolutionPort attackResolutionPort,
            IProjectileWorldPort projectileWorldPort)
        {
            return new BattleSessionFactory().Create(
                definition,
                attackResolutionPort,
                null,
                projectileWorldPort);
        }

        private sealed class D0NoHitProjectileWorldPort : IProjectileWorldPort
        {
            public int RegisterCount { get; private set; }

            public DomainResult Register(
                in ProjectileSpawnRequest request,
                out ProjectilePathSnapshot path)
            {
                RegisterCount++;
                path = new ProjectilePathSnapshot(
                    request.ProjectileId,
                    request.RuntimeId,
                    request.Tick,
                    request.ArrivalTick,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(0, 0, 1000));
                return DomainResult.Success;
            }

            public DomainResult Sweep(
                in ProjectileSweepRequest request,
                out ProjectileSweepHit hit)
            {
                hit = ProjectileSweepHit.None;
                return DomainResult.Success;
            }

            public DomainResult Release(in ProjectileReleaseRequest request)
            {
                return DomainResult.Success;
            }
        }

        private static ScenarioDefinition CreateScenario(
            ThreatScheduleEntry[] schedule,
            int projectileBudgetCapacity = 6,
            int projectileCapacity = 16)
        {
            return CombatLabHarness.CreateScenario(
                projectileBudgetCapacity: projectileBudgetCapacity,
                projectileCapacity: projectileCapacity,
                threatSchedule: schedule);
        }

        private static ScenarioDefinition LoadBurstbugScenarioDefinition()
        {
            D0CombatScenarioDefinition authoredScenario =
                AssetDatabase.LoadAssetAtPath<D0CombatScenarioDefinition>(
                    BurstbugScenarioAssetPath);
            Assert.That(authoredScenario, Is.Not.Null);

            D0CombatScenarioTechnicalSettings technicalSettings =
                new D0CombatScenarioTechnicalSettings(
                    32,
                    32,
                    8,
                    4096,
                    1024);
            Assert.That(authoredScenario.TryCreateDefinition(
                technicalSettings,
                out ScenarioDefinition definition,
                out string error), Is.True, error);
            return definition;
        }

        private static ScenarioDefinition CreateScenarioWithOnlyEntry(
            ScenarioDefinition authored,
            ThreatScheduleEntry entry)
        {
            return new ScenarioDefinition(
                authored.ScenarioSeed,
                authored.PlayerWeapon,
                authored.PlayerLife,
                authored.PlayerBarrier,
                authored.EnemyLife,
                authored.EnemyBreak,
                authored.PerfectRetractWindow,
                authored.PerfectRetractMultiplierBasisPoints,
                authored.BarrierLockDuration,
                authored.BarrierRestoreBasisPoints,
                authored.EnemyGroggyDuration,
                authored.ProjectileBudgetCapacity,
                authored.ProjectileCapacity,
                authored.ThreatCapacity,
                authored.ImpactHistoryCapacity,
                authored.ShotTargetHistoryCapacity,
                new[] { entry });
        }

        private static long GetReleaseTick(ThreatScheduleEntry entry)
        {
            return entry.DueTick.Value
                + entry.TelegraphDuration.Value
                + entry.WindupDuration.Value;
        }

        private static ThreatScheduleEntry CreateSweptEntry(
            long sequence,
            int dueTick,
            int definitionId,
            int flightTicks = 3)
        {
            ProjectileDefinition projectile = new ProjectileDefinition(
                definitionId + 1000,
                new TickDuration(flightTicks),
                new TickDuration(flightTicks + 2),
                new DamageSpec(10, 0),
                10,
                true,
                1);
            return new ThreatScheduleEntry(
                sequence,
                new TickIndex(dueTick),
                definitionId,
                new TickDuration(0),
                new TickDuration(0),
                new TickDuration(1),
                ThreatPayloadDefinition.SweptProjectile(projectile, 1),
                ThreatRetryPolicy.HoldPendingNextTick);
        }

        private static ThreatScheduleEntry CreateTimedImpactEntry(
            long sequence,
            int dueTick,
            int definitionId,
            int damage,
            int delayTicks)
        {
            return new ThreatScheduleEntry(
                sequence,
                new TickIndex(dueTick),
                definitionId,
                new TickDuration(0),
                new TickDuration(0),
                new TickDuration(1),
                ThreatPayloadDefinition.TimedImpact(
                    new DamageSpec(damage, 0),
                    ThreatTargetPolicy.PlayerCombatant,
                    new TickDuration(delayTicks),
                    1),
                ThreatRetryPolicy.HoldPendingNextTick);
        }

        private static ThreatDefinition CreateSnapshotSweptThreat()
        {
            ProjectileDefinition projectile = new ProjectileDefinition(
                821,
                new TickDuration(3),
                new TickDuration(5),
                new DamageSpec(10, 0),
                10,
                true,
                1,
                presentationKey: 17);
            return new ThreatDefinition(
                820,
                new TickDuration(2),
                new TickDuration(1),
                new TickDuration(1),
                ThreatPayloadDefinition.SweptProjectile(projectile, 1));
        }

        private static ThreatDefinition CreateSnapshotTimedImpactThreat()
        {
            return new ThreatDefinition(
                810,
                new TickDuration(2),
                new TickDuration(1),
                new TickDuration(1),
                ThreatPayloadDefinition.TimedImpact(
                    new DamageSpec(6, 0),
                    ThreatTargetPolicy.PlayerCombatant,
                    new TickDuration(1),
                    23));
        }

        private static void Start(BattleSession session)
        {
            Assert.That(session.ApplyControl(new SessionControlCommand(
                new ControlSequence(1),
                SessionControlCommandType.Start)).IsSuccess, Is.True);
        }

        private static void PumpAiming(BattleSession session)
        {
            Assert.That(CombatLabHarness.PumpOneTick(
                session,
                tick => PlayerInputFrame.Empty(tick, aimHeld: true)),
                Is.EqualTo(1));
        }

        private static PlayerInputFrame CreateReloadFrame(TickIndex tick)
        {
            return new PlayerInputFrame(
                tick,
                true,
                false,
                new[]
                {
                    new InputEdgeCommand(
                        new InputSequence(1L),
                        InputEdgeType.ReloadPressed)
                },
                1);
        }

        private static void PumpAimingThroughTick(BattleSession session, long inclusiveTick)
        {
            while (!session.CurrentTick.IsValid || session.CurrentTick.Value < inclusiveTick)
            {
                PumpAiming(session);
            }
        }

        private static void PumpAimingPrimary(BattleSession session)
        {
            Assert.That(CombatLabHarness.PumpOneTick(
                session,
                tick => PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true)),
                Is.EqualTo(1));
        }

        private static bool HasGroggyStarted(ICombatTraceView trace, RuntimeId enemyRuntimeId)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.GroggyStarted
                    && combatEvent.TargetId == enemyRuntimeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPlayerDamageAtOrAfter(
            ICombatTraceView trace,
            RuntimeId playerRuntimeId,
            long tick)
        {
            for (int index = 0; index < trace.Count; index++)
            {
                CombatEvent combatEvent = trace.GetOldest(index);
                if (combatEvent.EventType == CombatEventType.DamageApplied
                    && combatEvent.TargetId == playerRuntimeId
                    && combatEvent.Tick.Value >= tick)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertSummaryCanonicalDigest(
            BattleSession session,
            ReplaySummary summary)
        {
            ulong expected = StableHash.Append(
                session.Trace.CanonicalDigest,
                unchecked((ulong)summary.SpatialContractVersion));
            expected = StableHash.Append(
                expected,
                unchecked((ulong)summary.SpatialDecisionCount));
            expected = StableHash.Append(expected, summary.SpatialDecisionDigest);
            expected = StableHash.Append(
                expected,
                unchecked((ulong)summary.ThreatScheduleDecisionCount));
            expected = StableHash.Append(expected, summary.ThreatScheduleDecisionDigest);
            Assert.That(summary.CanonicalDigest, Is.EqualTo(expected));
        }

        private sealed class AllPrimaryPelletsWeakpointPort : IAttackResolutionPort
        {
            public RuntimeId TargetId { get; set; } = RuntimeId.Invalid;
            public int ResolveCount { get; private set; }

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                if (!TargetId.IsValid)
                {
                    return 0;
                }

                ResolveCount++;
                for (int index = 0; index < pelletCount; index++)
                {
                    output[index] = new ResolvedAttackHit(
                        TargetId,
                        HitPart.Weakpoint,
                        index,
                        index);
                }

                return pelletCount;
            }
        }
    }
}
