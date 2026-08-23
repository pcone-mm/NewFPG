using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatAudioCueRoutingTests
    {
        private static readonly RuntimeId PlayerRuntimeId = new RuntimeId(101);
        private static readonly RuntimeId EnemyRuntimeId = new RuntimeId(202);
        private static readonly RuntimeId ThreatRuntimeId = new RuntimeId(303);

        [Test]
        public void TraceRoutingOnlyPlaysPlayerAndEnemyCuesForTheBoundRuntimeIds()
        {
            AssertCue(
                CreateTrace(
                    CombatEventType.DamageApplied,
                    EnemyRuntimeId,
                    PlayerRuntimeId,
                    100,
                    80),
                CombatAudioCue.PlayerDamaged);
            AssertCue(
                CreateTrace(
                    CombatEventType.BarrierBroken,
                    EnemyRuntimeId,
                    PlayerRuntimeId,
                    40,
                    0),
                CombatAudioCue.PlayerBarrierBroken);
            AssertCue(
                CreateTrace(
                    CombatEventType.BreakTriggered,
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    20,
                    0),
                CombatAudioCue.EnemyBreak);
            AssertCue(
                CreateTrace(
                    CombatEventType.BattleCompleted,
                    RuntimeId.Invalid,
                    RuntimeId.Invalid,
                    0,
                    (int)BattleCompletionReason.Victory),
                CombatAudioCue.Victory);

            CombatEvent skillReload = CreateTrace(
                CombatEventType.ReloadStarted,
                PlayerRuntimeId,
                RuntimeId.Invalid,
                0,
                0);
            Assert.That(
                CombatAudioCueRouting.TryGetTraceCue(
                    skillReload,
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out _),
                Is.False,
                "Skill reload audio is owned by the ReloadCommit node.");
        }

        [Test]
        public void EnemyLifecycleRoutesCommittedActivationAndDeathExactlyOnce()
        {
            Assert.That(
                CombatAudioCueRouting.TryGetEnemyLifecycleCue(
                    FpgEncounterLifecycleEventType.EnemyActivated,
                    out CombatAudioCue spawnCue),
                Is.True);
            Assert.That(spawnCue, Is.EqualTo(CombatAudioCue.EnemySpawn));

            Assert.That(
                CombatAudioCueRouting.TryGetEnemyLifecycleCue(
                    FpgEncounterLifecycleEventType.EnemyDied,
                    out CombatAudioCue deathCue),
                Is.True);
            Assert.That(deathCue, Is.EqualTo(CombatAudioCue.EnemyDeath));

            Assert.That(
                CombatAudioCueRouting.TryGetEnemyLifecycleCue(
                    FpgEncounterLifecycleEventType.EnemyQueued,
                    out CombatAudioCue ignoredCue),
                Is.False);
            Assert.That(ignoredCue, Is.EqualTo(CombatAudioCue.None));
        }

        [Test]
        public void ThreatTransitionsMapOnlyTelegraphAndCommittedReleaseForTheThreeThreatKinds()
        {
            AssertThreatCue(
                FpgThreatPresentationKind.FastUninterceptable,
                ThreatState.Scheduled,
                ThreatState.Telegraph,
                CombatAudioCue.EnemyFastThreatTelegraph);
            AssertThreatCue(
                FpgThreatPresentationKind.FastUninterceptable,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted,
                CombatAudioCue.EnemyFastThreatRelease);
            AssertThreatCue(
                FpgThreatPresentationKind.InterceptableVolley,
                ThreatState.Scheduled,
                ThreatState.Telegraph,
                CombatAudioCue.EnemyInterceptableThreatTelegraph);
            AssertThreatCue(
                FpgThreatPresentationKind.InterceptableVolley,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted,
                CombatAudioCue.EnemyInterceptableThreatRelease);
            AssertThreatCue(
                FpgThreatPresentationKind.HeavyWeakpoint,
                ThreatState.Scheduled,
                ThreatState.Telegraph,
                CombatAudioCue.EnemyHeavyThreatTelegraph);
            AssertThreatCue(
                FpgThreatPresentationKind.HeavyWeakpoint,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted,
                CombatAudioCue.EnemyHeavyThreatRelease);

            Assert.That(
                CombatAudioCueRouting.TryGetThreatTransitionCue(
                    FpgThreatPresentationKind.FastUninterceptable,
                    ThreatState.ReleaseCommitted,
                    ThreatState.Recovery,
                    out _),
                Is.False);
            Assert.That(
                CombatAudioCueRouting.TryGetThreatTransitionCue(
                    (FpgThreatPresentationKind)99,
                    ThreatState.Scheduled,
                    ThreatState.Telegraph,
                    out _),
                Is.False);
        }

        [Test]
        public void ThreatRoutingUsesSemanticKindWhenResourceKeysAreIdentical()
        {
            RuntimeId threatRuntimeId = new RuntimeId(17L);
            CombatEvent release = CreateTrace(
                CombatEventType.ThreatStateChanged,
                EnemyRuntimeId,
                threatRuntimeId,
                (int)ThreatState.Windup,
                (int)ThreatState.ReleaseCommitted);

            Assert.That(
                D0ThreatPresentationRouting.TryResolve(
                    release,
                    EnemyRuntimeId,
                    threatRuntimeId,
                    FpgThreatPresentationKind.FastUninterceptable,
                    77,
                    out D0ThreatPresentationSignal fast),
                Is.True);
            Assert.That(
                D0ThreatPresentationRouting.TryResolve(
                    release,
                    EnemyRuntimeId,
                    threatRuntimeId,
                    FpgThreatPresentationKind.HeavyWeakpoint,
                    77,
                    out D0ThreatPresentationSignal heavy),
                Is.True);

            Assert.That(fast.PresentationKey, Is.EqualTo(77));
            Assert.That(heavy.PresentationKey, Is.EqualTo(77));
            Assert.That(
                fast.Command,
                Is.EqualTo(D0ThreatPresentationCommand.ReleaseFast));
            Assert.That(
                heavy.Command,
                Is.EqualTo(D0ThreatPresentationCommand.ReleaseHeavy));
            Assert.That(
                fast.AudioCue,
                Is.EqualTo(CombatAudioCue.EnemyFastThreatRelease));
            Assert.That(
                heavy.AudioCue,
                Is.EqualTo(CombatAudioCue.EnemyHeavyThreatRelease));
        }

        [Test]
        public void PresentationStateRoutingOnlyEmitsLockEntryAndPositiveCountdownChanges()
        {
            Assert.That(
                CombatAudioCueRouting.TryGetReticleLockCue(
                    wasLocked: false,
                    isLocked: false,
                    out _),
                Is.False);
            Assert.That(
                CombatAudioCueRouting.TryGetReticleLockCue(
                    wasLocked: false,
                    isLocked: true,
                    out CombatAudioCue lockCue),
                Is.True);
            Assert.That(lockCue, Is.EqualTo(CombatAudioCue.ReticleTargetLock));
            Assert.That(
                CombatAudioCueRouting.TryGetReticleLockCue(
                    wasLocked: true,
                    isLocked: true,
                    out _),
                Is.False);

            Assert.That(
                CombatAudioCueRouting.TryGetHeavyCountdownCue(
                    previousDisplayedSeconds: -1,
                    currentDisplayedSeconds: 3,
                    out CombatAudioCue firstTickCue),
                Is.True);
            Assert.That(firstTickCue, Is.EqualTo(CombatAudioCue.EnemyDangerTick));
            Assert.That(
                CombatAudioCueRouting.TryGetHeavyCountdownCue(
                    previousDisplayedSeconds: 3,
                    currentDisplayedSeconds: 3,
                    out _),
                Is.False);
            Assert.That(
                CombatAudioCueRouting.TryGetHeavyCountdownCue(
                    previousDisplayedSeconds: 3,
                    currentDisplayedSeconds: 2,
                    out CombatAudioCue nextTickCue),
                Is.True);
            Assert.That(nextTickCue, Is.EqualTo(CombatAudioCue.EnemyDangerTick));
            Assert.That(
                CombatAudioCueRouting.TryGetHeavyCountdownCue(
                    previousDisplayedSeconds: 1,
                    currentDisplayedSeconds: 0,
                    out _),
                Is.False,
                "The terminal release owns the zero boundary.");
        }

        [Test]
        public void HeavyCountdownUsesTheOfficialSixtyHertzWarningTicks()
        {
            Assert.That(
                CombatAudioCueRouting.GetHeavyDisplayedSeconds(135L),
                Is.EqualTo(3));
            Assert.That(
                CombatAudioCueRouting.GetHeavyDisplayedSeconds(120L),
                Is.EqualTo(2));
            Assert.That(
                CombatAudioCueRouting.GetHeavyDisplayedSeconds(60L),
                Is.EqualTo(1));
            Assert.That(
                CombatAudioCueRouting.GetHeavyDisplayedSeconds(0L),
                Is.Zero,
                "The release cue owns the zero boundary.");
        }

        private static void AssertCue(in CombatEvent combatEvent, CombatAudioCue expectedCue)
        {
            Assert.That(
                CombatAudioCueRouting.TryGetTraceCue(
                    combatEvent,
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out CombatAudioCue cue),
                Is.True);
            Assert.That(cue, Is.EqualTo(expectedCue));
        }

        private static void AssertThreatCue(
            FpgThreatPresentationKind presentationKind,
            ThreatState previousState,
            ThreatState currentState,
            CombatAudioCue expectedCue)
        {
            Assert.That(
                CombatAudioCueRouting.TryGetThreatTransitionCue(
                    presentationKind,
                    previousState,
                    currentState,
                    out CombatAudioCue cue),
                Is.True);
            Assert.That(cue, Is.EqualTo(expectedCue));
        }

        private static CombatEvent CreateTrace(
            CombatEventType eventType,
            RuntimeId sourceId,
            RuntimeId targetId,
            int valueBefore,
            int valueAfter)
        {
            return new CombatEvent(
                1L,
                new TickIndex(1L),
                eventType,
                sourceId,
                targetId,
                AttackId.Invalid,
                ImpactId.Invalid,
                valueBefore,
                valueAfter,
                RejectReason.None,
                0UL,
                DamageChannel.None,
                0,
                false);
        }
    }
}
