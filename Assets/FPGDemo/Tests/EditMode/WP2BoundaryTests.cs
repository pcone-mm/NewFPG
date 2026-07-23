using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class WP2BoundaryTests
    {
        [Test]
        public void ImpactLedgerRejectsAtItsFixedCapacityWithoutDroppingExistingIds()
        {
            ImpactLedger ledger = new ImpactLedger(257);

            for (long value = 1L; value <= 257L; value++)
            {
                Assert.That(
                    ledger.TryConsume(new ImpactId(value)).IsSuccess,
                    Is.True,
                    "ImpactId " + value + " should be accepted.");
            }

            Assert.That(ledger.Count, Is.EqualTo(257));
            DomainResult overflow = ledger.TryConsume(new ImpactId(258L));
            Assert.That(overflow.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(ledger.Count, Is.EqualTo(257));
            DomainResult duplicate = ledger.TryConsume(new ImpactId(257L));
            Assert.That(duplicate.RejectReason, Is.EqualTo(RejectReason.DuplicateImpact));
        }

        [Test]
        public void ProjectileBudgetHonorsAConfiguredThirtyThreeReservationCapacity()
        {
            ProjectileBudget budget = new ProjectileBudget(33, 33);
            ReservationToken[] tokens = new ReservationToken[33];

            for (int index = 0; index < tokens.Length; index++)
            {
                Assert.That(budget.TryReserve(1, out tokens[index]).IsSuccess, Is.True);
            }

            Assert.That(budget.ReservedUnits, Is.EqualTo(33));
            Assert.That(budget.ActiveUnits, Is.Zero);
        }

        [Test]
        public void ImpactQueueUsesRuntimeIdBeforeImpactIdForStableSameTickOrdering()
        {
            ImpactQueue queue = new ImpactQueue(2);
            ImpactIntent firstInserted = CreateImpact(1L, 2L);
            ImpactIntent secondInserted = CreateImpact(2L, 1L);

            Assert.That(
                queue.TryEnqueue(
                    firstInserted,
                    ImpactPhasePriority.EnemyImpact,
                    new RuntimeId(2L)).IsSuccess,
                Is.True);
            Assert.That(
                queue.TryEnqueue(
                    secondInserted,
                    ImpactPhasePriority.EnemyImpact,
                    new RuntimeId(1L)).IsSuccess,
                Is.True);

            QueuedImpact[] output = new QueuedImpact[2];
            Assert.That(queue.DrainDue(new TickIndex(0L), output), Is.EqualTo(2));
            Assert.That(output[0].StableOrderId, Is.EqualTo(new RuntimeId(1L)));
            Assert.That(output[0].Intent.ImpactId, Is.EqualTo(new ImpactId(2L)));
            Assert.That(output[1].StableOrderId, Is.EqualTo(new RuntimeId(2L)));
            Assert.That(output[1].Intent.ImpactId, Is.EqualTo(new ImpactId(1L)));
        }

        [Test]
        public void ZeroDurationThreatReleasesOnItsStartTickAndPositiveDurationsRespectBoundaries()
        {
            ProjectileBudget zeroBudget = new ProjectileBudget(1, 1);
            ThreatRuntime zeroThreat = new ThreatRuntime(
                CombatLabHarness.CreateThreatDefinition(
                    telegraphTicks: 0,
                    windupTicks: 0,
                    recoveryTicks: 1));
            SessionIdAllocator zeroIds = new SessionIdAllocator();

            Assert.That(zeroThreat.TryStart(
                new TickIndex(0L),
                EnemyControlState.Active,
                zeroBudget,
                zeroIds).IsSuccess, Is.True);
            Assert.That(zeroThreat.AdvanceBeforeRelease(new TickIndex(0L)).IsSuccess, Is.True);
            Assert.That(zeroThreat.TryCommitRelease(
                new TickIndex(0L),
                zeroBudget,
                out ThreatRelease ignored).IsSuccess, Is.True);

            ProjectileBudget positiveBudget = new ProjectileBudget(1, 1);
            ThreatRuntime positiveThreat = new ThreatRuntime(
                CombatLabHarness.CreateThreatDefinition(
                    telegraphTicks: 1,
                    windupTicks: 1,
                    recoveryTicks: 1));
            SessionIdAllocator positiveIds = new SessionIdAllocator();
            Assert.That(positiveThreat.TryStart(
                new TickIndex(0L),
                EnemyControlState.Active,
                positiveBudget,
                positiveIds).IsSuccess, Is.True);
            Assert.That(
                positiveThreat.AdvanceBeforeRelease(new TickIndex(0L)).RejectReason,
                Is.EqualTo(RejectReason.WrongTick));
            Assert.That(positiveThreat.AdvanceBeforeRelease(new TickIndex(1L)).IsSuccess, Is.True);
            Assert.That(
                positiveThreat.TryCommitRelease(
                    new TickIndex(1L),
                    positiveBudget,
                    out ThreatRelease notYetDue).RejectReason,
                Is.EqualTo(RejectReason.WrongTick));
            Assert.That(positiveThreat.TryCommitRelease(
                new TickIndex(2L),
                positiveBudget,
                out ThreatRelease released).IsSuccess, Is.True);
            Assert.That(released.ReleaseTick, Is.EqualTo(new TickIndex(2L)));
        }

        [Test]
        public void CompleteCancelsAReleasedRecoveryThreatAndReturnsActiveBudget()
        {
            BattleSession session = CombatLabHarness.CreateSession(projectileBudgetCapacity: 1);
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(session.TryAddThreat(
                    CombatLabHarness.CreateThreatDefinition(
                        telegraphTicks: 0,
                        windupTicks: 0,
                        flightTicks: 10,
                        recoveryTicks: 10),
                    out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);

                CombatLabHarness.PumpOneTick(session);
                Assert.That(session.GetThreatSnapshot(threatIndex).State,
                    Is.EqualTo(ThreatState.Recovery));
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.EqualTo(1));

                Assert.That(session.ApplyControl(
                    Control(2L, SessionControlCommandType.Complete)).IsSuccess, Is.True);
                Assert.That(session.GetThreatSnapshot(threatIndex).State,
                    Is.EqualTo(ThreatState.Canceled));
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
                Assert.That(session.PendingImpactCount, Is.Zero);
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void SixHundredTickHarnessCoversPauseResumeRestartAndDispose()
        {
            BattleSessionFactory factory = new BattleSessionFactory();
            BattleSession session = factory.Create(
                CombatLabHarness.CreateScenario(seed: 600UL),
                new NullAttackResolutionPort());

            Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
            for (int frame = 0; frame < 600; frame++)
            {
                if (frame == 100)
                {
                    Assert.That(session.ApplyControl(
                        Control(2L, SessionControlCommandType.Pause)).IsSuccess, Is.True);
                    DomainResult paused = session.Pump(
                        TimeSpan.TicksPerSecond,
                        new TickEchoInputSource(),
                        out int pausedSteps);
                    Assert.That(paused.IsSuccess, Is.False);
                    Assert.That(paused.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                    Assert.That(pausedSteps, Is.Zero);
                    Assert.That(session.ApplyControl(
                        Control(3L, SessionControlCommandType.Resume)).IsSuccess, Is.True);
                }

                long delta = TimeSpan.TicksPerSecond / GameplayClock.DefaultTickRate;
                if (frame % 60 < 40)
                {
                    delta++;
                }

                DomainResult result = session.Pump(
                    delta,
                    new TickEchoInputSource(),
                    out int executedSteps);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Assert.That(session.ExecutedTickCount, Is.EqualTo(600L));
            BattleSession restarted = factory.Restart(session, null);
            try
            {
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Disposed));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Restarted));
                Assert.That(restarted.State, Is.EqualTo(BattleSessionState.NotStarted));
                Assert.That(restarted.PendingImpactCount, Is.Zero);
                Assert.That(restarted.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(restarted.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
            finally
            {
                restarted.Dispose();
            }
        }

        [Test]
        public void HundredRandomizedThreeToSixHundredTickRunsReplayExactly()
        {
            for (int corpus = 0; corpus < 100; corpus++)
            {
                int tickCount = 300 + ((corpus * 37) % 301);
                ulong seed = unchecked((ulong)(0xBADC0FFEEUL + (uint)corpus * 7919U));
                ReplaySummary first = RunCorpus(seed, tickCount);
                ReplaySummary second = RunCorpus(seed, tickCount);

                Assert.That(second.CanonicalDigest, Is.EqualTo(first.CanonicalDigest),
                    "Corpus=" + corpus + ", ticks=" + tickCount);
                Assert.That(second.TraceEventCount, Is.EqualTo(first.TraceEventCount));
                Assert.That(second.ExecutedTickCount, Is.EqualTo(tickCount));
                Assert.That(second.FinalSnapshot.PlayerLife, Is.InRange(0, 100));
                Assert.That(second.FinalSnapshot.PlayerBarrier, Is.InRange(0, 60));
                Assert.That(second.FinalSnapshot.PlayerAmmo, Is.InRange(0, 8));
                Assert.That(
                    second.FinalSnapshot.ReservedProjectileUnits
                    + second.FinalSnapshot.ActiveProjectileUnits,
                    Is.LessThanOrEqualTo(6));
            }
        }

        [Test]
        public void DefinitionHashChangesWhenAWeaponFieldOrCapacityChanges()
        {
            ScenarioDefinition baseline = CombatLabHarness.CreateScenario();
            WeaponDefinition weapon = baseline.PlayerWeapon;
            WeaponDefinition changedWeapon = new WeaponDefinition(
                weapon.DefinitionId,
                weapon.MagazineCapacity,
                weapon.PrimaryAmmoCost,
                weapon.PrimaryInterval,
                new DamageSpec(
                    weapon.PrimaryDamage.BaseDamage + 1,
                    weapon.PrimaryDamage.BreakDamage,
                    weapon.PrimaryDamage.WeakpointDamageMultiplierBasisPoints,
                    weapon.PrimaryDamage.WeakpointBreakMultiplierBasisPoints),
                weapon.SecondaryAmmoCost,
                weapon.SecondaryMinimumCharge,
                weapon.SecondaryRecovery,
                weapon.SecondaryDamage,
                weapon.ReloadDuration,
                weapon.SecondaryMaxImpactCount,
                weapon.SecondaryTriggerMode,
                weapon.PrimaryQueryMode,
                weapon.PrimaryAdditionalPenetrationCount,
                weapon.SecondaryQueryMode,
                weapon.SecondaryAreaProjectileLimit,
                weapon.PrimaryAllowedTargetKinds,
                weapon.SecondaryAllowedTargetKinds);
            ScenarioDefinition changedWeaponScenario = CopyScenario(
                baseline,
                changedWeapon,
                baseline.ProjectileCapacity,
                baseline.ThreatCapacity);
            WeaponDefinition changedSecondaryCharge = new WeaponDefinition(
                weapon.DefinitionId,
                weapon.MagazineCapacity,
                weapon.PrimaryAmmoCost,
                weapon.PrimaryInterval,
                weapon.PrimaryDamage,
                weapon.SecondaryAmmoCost,
                new TickDuration(weapon.SecondaryMinimumCharge.Value + 1),
                weapon.SecondaryRecovery,
                weapon.SecondaryDamage,
                weapon.ReloadDuration,
                weapon.SecondaryMaxImpactCount,
                weapon.SecondaryTriggerMode,
                weapon.PrimaryQueryMode,
                weapon.PrimaryAdditionalPenetrationCount,
                weapon.SecondaryQueryMode,
                weapon.SecondaryAreaProjectileLimit,
                weapon.PrimaryAllowedTargetKinds,
                weapon.SecondaryAllowedTargetKinds);
            ScenarioDefinition changedSecondaryChargeScenario = CopyScenario(
                baseline,
                changedSecondaryCharge,
                baseline.ProjectileCapacity,
                baseline.ThreatCapacity);
            WeaponDefinition changedSecondaryTriggerMode = new WeaponDefinition(
                weapon.DefinitionId,
                weapon.MagazineCapacity,
                weapon.PrimaryAmmoCost,
                weapon.PrimaryInterval,
                weapon.PrimaryDamage,
                weapon.SecondaryAmmoCost,
                weapon.SecondaryMinimumCharge,
                weapon.SecondaryRecovery,
                weapon.SecondaryDamage,
                weapon.ReloadDuration,
                weapon.SecondaryMaxImpactCount,
                SecondaryTriggerMode.ImmediateRepeatWhileHeld,
                weapon.PrimaryQueryMode,
                weapon.PrimaryAdditionalPenetrationCount,
                weapon.SecondaryQueryMode,
                weapon.SecondaryAreaProjectileLimit,
                weapon.PrimaryAllowedTargetKinds,
                weapon.SecondaryAllowedTargetKinds);
            ScenarioDefinition changedSecondaryTriggerModeScenario = CopyScenario(
                baseline,
                changedSecondaryTriggerMode,
                baseline.ProjectileCapacity,
                baseline.ThreatCapacity);
            WeaponDefinition changedPrimaryPenetration = new WeaponDefinition(
                weapon.DefinitionId,
                weapon.MagazineCapacity,
                weapon.PrimaryAmmoCost,
                weapon.PrimaryInterval,
                weapon.PrimaryDamage,
                weapon.SecondaryAmmoCost,
                weapon.SecondaryMinimumCharge,
                weapon.SecondaryRecovery,
                weapon.SecondaryDamage,
                weapon.ReloadDuration,
                weapon.SecondaryMaxImpactCount,
                weapon.SecondaryTriggerMode,
                weapon.PrimaryQueryMode,
                weapon.PrimaryAdditionalPenetrationCount + 1,
                weapon.SecondaryQueryMode,
                weapon.SecondaryAreaProjectileLimit,
                weapon.PrimaryAllowedTargetKinds,
                weapon.SecondaryAllowedTargetKinds);
            ScenarioDefinition changedPrimaryPenetrationScenario = CopyScenario(
                baseline,
                changedPrimaryPenetration,
                baseline.ProjectileCapacity,
                baseline.ThreatCapacity);
            WeaponDefinition changedSecondaryProjectileLimit = new WeaponDefinition(
                weapon.DefinitionId,
                weapon.MagazineCapacity,
                weapon.PrimaryAmmoCost,
                weapon.PrimaryInterval,
                weapon.PrimaryDamage,
                weapon.SecondaryAmmoCost,
                weapon.SecondaryMinimumCharge,
                weapon.SecondaryRecovery,
                weapon.SecondaryDamage,
                weapon.ReloadDuration,
                weapon.SecondaryMaxImpactCount,
                weapon.SecondaryTriggerMode,
                weapon.PrimaryQueryMode,
                weapon.PrimaryAdditionalPenetrationCount,
                weapon.SecondaryQueryMode,
                weapon.SecondaryAreaProjectileLimit + 1,
                weapon.PrimaryAllowedTargetKinds,
                weapon.SecondaryAllowedTargetKinds);
            ScenarioDefinition changedSecondaryProjectileLimitScenario = CopyScenario(
                baseline,
                changedSecondaryProjectileLimit,
                baseline.ProjectileCapacity,
                baseline.ThreatCapacity);
            ScenarioDefinition changedCapacityScenario = CopyScenario(
                baseline,
                baseline.PlayerWeapon,
                baseline.ProjectileCapacity + 1,
                baseline.ThreatCapacity);

            Assert.That(changedWeaponScenario.DefinitionHash, Is.Not.EqualTo(baseline.DefinitionHash));
            Assert.That(
                changedSecondaryChargeScenario.DefinitionHash,
                Is.Not.EqualTo(baseline.DefinitionHash));
            Assert.That(
                changedSecondaryTriggerModeScenario.DefinitionHash,
                Is.Not.EqualTo(baseline.DefinitionHash));
            Assert.That(
                changedPrimaryPenetrationScenario.DefinitionHash,
                Is.Not.EqualTo(baseline.DefinitionHash));
            Assert.That(
                changedSecondaryProjectileLimitScenario.DefinitionHash,
                Is.Not.EqualTo(baseline.DefinitionHash));
            Assert.That(changedCapacityScenario.DefinitionHash, Is.Not.EqualTo(baseline.DefinitionHash));
        }

        [Test]
        public void LethalPlayerImpactCompletesVictoryBeforeAThreatCanRelease()
        {
            ScenarioDefinition source = CombatLabHarness.CreateScenario();
            WeaponDefinition lethalWeapon = new WeaponDefinition(
                source.PlayerWeapon.DefinitionId,
                1,
                1,
                new TickDuration(1),
                new DamageSpec(500, 0),
                1,
                source.PlayerWeapon.SecondaryMinimumCharge,
                source.PlayerWeapon.SecondaryRecovery,
                source.PlayerWeapon.SecondaryDamage,
                source.PlayerWeapon.ReloadDuration,
                source.PlayerWeapon.SecondaryMaxImpactCount,
                source.PlayerWeapon.SecondaryTriggerMode,
                source.PlayerWeapon.PrimaryQueryMode,
                source.PlayerWeapon.PrimaryAdditionalPenetrationCount,
                source.PlayerWeapon.SecondaryQueryMode,
                source.PlayerWeapon.SecondaryAreaProjectileLimit,
                source.PlayerWeapon.PrimaryAllowedTargetKinds,
                source.PlayerWeapon.SecondaryAllowedTargetKinds);
            ScenarioDefinition scenario = CopyScenario(
                source,
                lethalWeapon,
                source.ProjectileCapacity,
                source.ThreatCapacity);
            BattleSession session = new BattleSessionFactory().Create(
                scenario,
                new FixedHitPort(new RuntimeId(2L)));
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(session.TryAddThreat(
                    CombatLabHarness.CreateThreatDefinition(
                        telegraphTicks: 0,
                        windupTicks: 0,
                        flightTicks: 10),
                    out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);

                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true, primaryHeld: true));

                Assert.That(session.State, Is.EqualTo(BattleSessionState.Completed));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Victory));
                Assert.That(session.GetThreatSnapshot(threatIndex).State,
                    Is.EqualTo(ThreatState.Canceled));
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void LethalEnemyProjectileCompletesDefeatAndCancelsTheSession()
        {
            ScenarioDefinition source = CombatLabHarness.CreateScenario();
            ThreatDefinition lethalThreat = CombatLabHarness.CreateThreatDefinition(
                projectileDamage: 100,
                telegraphTicks: 0,
                windupTicks: 0,
                flightTicks: 1,
                recoveryTicks: 10);
            BattleSession session = new BattleSessionFactory().Create(
                source,
                new NullAttackResolutionPort(),
                null,
                CombatLabHarness.CreateProjectileWorldPort());
            try
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                Assert.That(session.TryAddThreat(lethalThreat, out int threatIndex).IsSuccess, Is.True);
                Assert.That(session.TryStartThreat(threatIndex).IsSuccess, Is.True);
                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true));
                Assert.That(session.State, Is.EqualTo(BattleSessionState.Running));
                CombatLabHarness.PumpOneTick(
                    session,
                    tick => PlayerInputFrame.Empty(tick, aimHeld: true));

                Assert.That(session.State, Is.EqualTo(BattleSessionState.Completed));
                Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Defeat));
                Assert.That(session.GetFinalSnapshot().PlayerLife, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ReservedProjectileUnits, Is.Zero);
                Assert.That(session.GetFinalSnapshot().ActiveProjectileUnits, Is.Zero);
            }
            finally
            {
                session.Dispose();
            }
        }

        private static ReplaySummary RunCorpus(ulong seed, int tickCount)
        {
            using (BattleSession session = CombatLabHarness.CreateSession(seed))
            {
                Assert.That(session.ApplyControl(Control(1L, SessionControlCommandType.Start)).IsSuccess, Is.True);
                for (int frame = 0; frame < tickCount; frame++)
                {
                    long delta = TimeSpan.TicksPerSecond / GameplayClock.DefaultTickRate;
                    if (frame % 60 < 40)
                    {
                        delta++;
                    }

                    DomainResult result = session.Pump(
                        delta,
                        new ScriptedInputSource(frame),
                        out int executedSteps);
                    Assert.That(result.IsSuccess, Is.True);
                    Assert.That(executedSteps, Is.EqualTo(1));

                    FinalSnapshot snapshot = session.GetFinalSnapshot();
                    Assert.That(snapshot.PlayerLife, Is.InRange(0, 100));
                    Assert.That(snapshot.PlayerBarrier, Is.InRange(0, 60));
                    Assert.That(snapshot.PlayerAmmo, Is.InRange(0, 8));
                    Assert.That(snapshot.ReservedProjectileUnits, Is.GreaterThanOrEqualTo(0));
                    Assert.That(snapshot.ActiveProjectileUnits, Is.GreaterThanOrEqualTo(0));
                }

                return session.GetReplaySummary();
            }
        }

        private static ScenarioDefinition CopyScenario(
            ScenarioDefinition source,
            WeaponDefinition weapon,
            int projectileCapacity,
            int threatCapacity)
        {
            return new ScenarioDefinition(
                source.ScenarioSeed,
                weapon,
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
                projectileCapacity,
                threatCapacity);
        }

        private static ImpactIntent CreateImpact(long impactId, long stableTargetId)
        {
            return new ImpactIntent(
                new ImpactId(impactId),
                new AttackId(1L),
                new ShotId(1L),
                new RuntimeId(9L),
                new RuntimeId(stableTargetId),
                new TickIndex(0L),
                new DamageSpec(1, 0),
                HitPart.Body,
                DamageType.Normal,
                CombatTags.EnemyAttack);
        }

        private static SessionControlCommand Control(
            long sequence,
            SessionControlCommandType type)
        {
            return new SessionControlCommand(new ControlSequence(sequence), type);
        }

        private sealed class TickEchoInputSource : IPlayerInputSource
        {
            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                return PlayerInputFrame.Empty(tick);
            }
        }

        private sealed class ScriptedInputSource : IPlayerInputSource
        {
            private readonly int frame;

            public ScriptedInputSource(int frame)
            {
                this.frame = frame;
            }

            public PlayerInputFrame GetFrame(TickIndex tick)
            {
                bool aimHeld = frame % 5 != 0;
                bool primaryHeld = frame % 7 == 0;
                return PlayerInputFrame.Empty(tick, aimHeld, primaryHeld);
            }
        }

        private sealed class FixedHitPort : IAttackResolutionPort
        {
            private readonly RuntimeId targetId;

            public FixedHitPort(RuntimeId targetId)
            {
                this.targetId = targetId;
            }

            public int Resolve(
                AttackSnapshot attack,
                PelletSample[] pellets,
                int pelletCount,
                ResolvedAttackHit[] output)
            {
                output[0] = new ResolvedAttackHit(targetId, HitPart.Body, 0, 0);
                return 1;
            }
        }
    }
}
