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
                    CombatEventType.InputAccepted,
                    PlayerRuntimeId,
                    RuntimeId.Invalid,
                    (int)WeaponState.Ready,
                    (int)WeaponState.AltCharging),
                CombatAudioCue.PlayerSecondaryCharge);
            AssertCue(
                CreateTrace(
                    CombatEventType.ReloadStarted,
                    PlayerRuntimeId,
                    RuntimeId.Invalid,
                    0,
                    0),
                CombatAudioCue.PlayerReload);
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

            CombatEvent otherActorReload = CreateTrace(
                CombatEventType.ReloadStarted,
                EnemyRuntimeId,
                RuntimeId.Invalid,
                0,
                0);
            Assert.That(
                CombatAudioCueRouting.TryGetTraceCue(
                    otherActorReload,
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out _),
                Is.False);
        }

        [Test]
        public void ThreatTransitionsMapOnlyTelegraphAndCommittedReleaseForTheThreeD0Keys()
        {
            AssertThreatCue(
                CombatPresentationProfile.FastThreatPresentationKey,
                ThreatState.Scheduled,
                ThreatState.Telegraph,
                CombatAudioCue.EnemyFastThreatTelegraph);
            AssertThreatCue(
                CombatPresentationProfile.FastThreatPresentationKey,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted,
                CombatAudioCue.EnemyFastThreatRelease);
            AssertThreatCue(
                CombatPresentationProfile.InterceptableVolleyThreatPresentationKey,
                ThreatState.Scheduled,
                ThreatState.Telegraph,
                CombatAudioCue.EnemyInterceptableThreatTelegraph);
            AssertThreatCue(
                CombatPresentationProfile.InterceptableVolleyThreatPresentationKey,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted,
                CombatAudioCue.EnemyInterceptableThreatRelease);
            AssertThreatCue(
                CombatPresentationProfile.HeavyWeakpointThreatPresentationKey,
                ThreatState.Scheduled,
                ThreatState.Telegraph,
                CombatAudioCue.EnemyHeavyThreatTelegraph);
            AssertThreatCue(
                CombatPresentationProfile.HeavyWeakpointThreatPresentationKey,
                ThreatState.Windup,
                ThreatState.ReleaseCommitted,
                CombatAudioCue.EnemyHeavyThreatRelease);

            Assert.That(
                CombatAudioCueRouting.TryGetThreatTransitionCue(
                    CombatPresentationProfile.FastThreatPresentationKey,
                    ThreatState.ReleaseCommitted,
                    ThreatState.Recovery,
                    out _),
                Is.False);
            Assert.That(
                CombatAudioCueRouting.TryGetThreatTransitionCue(
                    99,
                    ThreatState.Scheduled,
                    ThreatState.Telegraph,
                    out _),
                Is.False);
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
            int presentationKey,
            ThreatState previousState,
            ThreatState currentState,
            CombatAudioCue expectedCue)
        {
            Assert.That(
                CombatAudioCueRouting.TryGetThreatTransitionCue(
                    presentationKey,
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
