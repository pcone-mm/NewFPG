using System;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    [Category("PlayerSkillExecution")]
    public sealed class FpgPlayerSkillExecutionControllerTests
    {
        [Test]
        public void WholeSequenceAmmoIsPreflightedBeforeTickZero()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 2,
                cooldownTicks: 0,
                ammoCost: 2,
                Event(10, 0, 101),
                Event(20, 2, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 3);

            DomainResult result = controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));
            Assert.That(
                player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotEnoughAmmo));
        }

        [Test]
        public void BoundSessionExecutionAllocatorContinuesAfterExternalConsumer()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)));
            FpgSkillExecutionIdAllocator executionIds =
                new FpgSkillExecutionIdAllocator();
            Assert.That(executionIds.Next().Value, Is.EqualTo(1L));
            Assert.That(
                controller.TryBindExecutionIdAllocator(
                    executionIds,
                    out string error),
                Is.True,
                error);

            PlayerRuntime player = CreatePlayer(magazineCapacity: 2);
            Assert.That(
                controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(0L),
                        true,
                        true),
                    player).IsSuccess,
                Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(
                controller.GetResult(0).RuntimeEvent.ExecutionId.Value,
                Is.EqualTo(2L));
            Assert.That(executionIds.Peek().Value, Is.EqualTo(3L));

            controller.Reset();
            Assert.That(executionIds.Peek().Value, Is.EqualTo(3L));
        }

        [Test]
        public void TickZeroAndEndpointAttacksConsumeAmmoPerCommittedEvent()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 2,
                cooldownTicks: 0,
                ammoCost: 1,
                Event(10, 0, 101),
                Event(20, 2, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            CommitPrimaryEvent(controller.GetResult(0), player, ids, release, 0L);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(1L), true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(2L), true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(20));
            CommitPrimaryEvent(controller.GetResult(0), player, ids, release, 2L);

            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(2));
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.PlannedLastAttackTick,
                Is.EqualTo(new TickIndex(2L)));
        }

        [Test]
        public void ExposureRequirementEndsAfterTheLastAuthoredAttackTick()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 5,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101),
                    Event(20, 2, 101)));
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(100L), true, true),
                player).IsSuccess, Is.True);

            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(
                controller.RequiresExposureAt(new TickIndex(99L)),
                Is.False);
            Assert.That(
                controller.RequiresExposureAt(new TickIndex(100L)),
                Is.True);
            Assert.That(
                controller.RequiresExposureAt(new TickIndex(102L)),
                Is.True);
            Assert.That(
                controller.RequiresExposureAt(new TickIndex(103L)),
                Is.False);
        }

        [Test]
        public void ReloadSequenceNeverRequiresCombatExposure()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)));
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            player.Weapon.Magazine.ConsumeValidated(1);
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(
                    new InputSequence(1L),
                    InputEdgeType.ReloadPressed)
            };

            Assert.That(controller.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(0L),
                    false,
                    false,
                    edges,
                    edges.Length),
                player).IsSuccess, Is.True);

            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSlot,
                Is.EqualTo(FpgPlayerSkillSlot.Reload));
            Assert.That(
                controller.RequiresExposureAt(new TickIndex(0L)),
                Is.False);
            Assert.That(
                controller.RequiresExposureAt(new TickIndex(1L)),
                Is.False);
        }

        [Test]
        public void CoverGateDelaysPrimaryUntilTheFifthTick()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                for (long tick = 0L; tick < 5L; tick++)
                {
                    CoverGateResult gated = GateCoverInput(
                        fixture,
                        tick,
                        aimHeld: false,
                        primaryHeld: true,
                        aimOriginX: checked((int)tick));
                    Assert.That(gated.Frame.PrimaryHeld, Is.False);
                    ProcessGatedFrame(fixture, gated);
                    Assert.That(fixture.Controller.ResultCount, Is.Zero);
                }

                CoverGateResult ready = GateCoverInput(
                    fixture,
                    5L,
                    aimHeld: false,
                    primaryHeld: true,
                    aimOriginX: 5);
                Assert.That(ready.Frame.PrimaryHeld, Is.True);
                ProcessGatedFrame(fixture, ready);

                Assert.That(fixture.Controller.ResultCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.GetResult(0).Slot,
                    Is.EqualTo(FpgPlayerSkillSlot.Primary));
                Assert.That(fixture.Driver.CoverPeekStartedTick,
                    Is.EqualTo(new TickIndex(0L)));
            }
        }

        [Test]
        public void AcceptedPrimaryLatchesFacingAndRejectsMidCycleSideChanges()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                FpgResolvedAimContext leftAim = CreateValidAimContext(0.25f);
                SetPrivateField(fixture.Driver, "liveAimContext", leftAim);
                SetPrivateField(fixture.Driver, "liveAttackAimContext", leftAim);

                GateCoverInput(
                    fixture,
                    0L,
                    aimHeld: false,
                    primaryHeld: true,
                    aimOriginX: 0);

                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.True);
                Assert.That(
                    fixture.Driver.CoverPeekDirection,
                    Is.EqualTo(FpgPlayerFacingDirection.Left));

                FpgResolvedAimContext rightAim = CreateValidAimContext(0.75f);
                SetPrivateField(fixture.Driver, "liveAimContext", rightAim);
                SetPrivateField(fixture.Driver, "liveAttackAimContext", rightAim);
                GateCoverInput(
                    fixture,
                    1L,
                    aimHeld: false,
                    primaryHeld: true,
                    aimOriginX: 1);

                Assert.That(
                    fixture.Driver.CoverPeekDirection,
                    Is.EqualTo(FpgPlayerFacingDirection.Left));
            }
        }

        [Test]
        public void AcceptedSecondaryChargeStartsWithCurrentFacingDirection()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                FpgResolvedAimContext leftAim = CreateValidAimContext(0.25f);
                SetPrivateField(fixture.Driver, "liveAimContext", leftAim);
                SetPrivateField(fixture.Driver, "liveAttackAimContext", leftAim);
                InputEdgeCommand[] edges =
                {
                    new InputEdgeCommand(
                        new InputSequence(1L),
                        InputEdgeType.SecondaryPressed)
                };

                GateCoverInput(
                    fixture,
                    0L,
                    aimHeld: false,
                    primaryHeld: false,
                    aimOriginX: 0,
                    edgeCommands: edges);

                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.True);
                Assert.That(
                    fixture.Driver.CoverPeekDirection,
                    Is.EqualTo(FpgPlayerFacingDirection.Left));
            }
        }

        [Test]
        public void RejectedAttackDoesNotLatchFacingOrStartPeek()
        {
            FpgCompiledPlayerSkillDefinition unaffordablePrimary =
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 9,
                    Event(10, 0, 101));
            using (CoverGateFixture fixture =
                CreateCoverGateFixture(unaffordablePrimary))
            {
                FpgResolvedAimContext leftAim = CreateValidAimContext(0.25f);
                SetPrivateField(fixture.Driver, "liveAimContext", leftAim);
                SetPrivateField(fixture.Driver, "liveAttackAimContext", leftAim);

                GateCoverInput(
                    fixture,
                    0L,
                    aimHeld: false,
                    primaryHeld: true,
                    aimOriginX: 0);

                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);
                Assert.That(
                    fixture.Driver.CoverPeekDirection,
                    Is.EqualTo(FpgPlayerFacingDirection.Right));
            }
        }

        [Test]
        public void CoverGateAimOnlyDoesNotPrechargePrimaryPeek()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                for (long tick = 0L; tick < 5L; tick++)
                {
                    CoverGateResult aiming = GateCoverInput(
                        fixture,
                        tick,
                        aimHeld: true,
                        primaryHeld: false,
                        aimOriginX: checked((int)tick));
                    Assert.That(
                        fixture.Driver.PrimaryAttackAvailability.Ready,
                        Is.True);
                    Assert.That(aiming.Frame.PrimaryHeld, Is.False);
                    Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);
                    Assert.That(
                        fixture.Driver.CoverPeekStartedTick.IsValid,
                        Is.False);
                    ProcessGatedFrame(fixture, aiming);
                }

                CoverGateResult attackStarted = GateCoverInput(
                    fixture,
                    5L,
                    aimHeld: true,
                    primaryHeld: true,
                    aimOriginX: 50);
                Assert.That(attackStarted.Frame.PrimaryHeld, Is.False);
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.True);
                Assert.That(
                    fixture.Driver.CoverPeekStartedTick,
                    Is.EqualTo(new TickIndex(5L)));
                ProcessGatedFrame(fixture, attackStarted);

                for (long tick = 6L; tick < 10L; tick++)
                {
                    CoverGateResult peeking = GateCoverInput(
                        fixture,
                        tick,
                        aimHeld: true,
                        primaryHeld: true,
                        aimOriginX: checked((int)tick));
                    Assert.That(peeking.Frame.PrimaryHeld, Is.False);
                    ProcessGatedFrame(fixture, peeking);
                }

                CoverGateResult readyAttack = GateCoverInput(
                    fixture,
                    10L,
                    aimHeld: true,
                    primaryHeld: true,
                    aimOriginX: 100);
                Assert.That(readyAttack.Frame.PrimaryHeld, Is.True);
                ProcessGatedFrame(fixture, readyAttack);

                Assert.That(fixture.Controller.ResultCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.GetResult(0).RuntimeEvent.Tick,
                    Is.EqualTo(new TickIndex(10L)));
            }
        }

        [Test]
        public void CoverGateQuickReleaseFiresOnceWithFrozenPressAim()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                int totalPrimaryEvents = 0;
                CoverGateResult pressed = GateCoverInput(
                    fixture,
                    0L,
                    aimHeld: true,
                    primaryHeld: true,
                    aimOriginX: 100);
                ProcessGatedFrame(fixture, pressed);
                totalPrimaryEvents += CountResults(
                    fixture.Controller,
                    FpgPlayerSkillSlot.Primary);

                const int ReleaseAimOriginX = 250;
                CoverGateResult released = GateCoverInput(
                    fixture,
                    1L,
                    aimHeld: true,
                    primaryHeld: false,
                    aimOriginX: ReleaseAimOriginX);
                ProcessGatedFrame(fixture, released);
                totalPrimaryEvents += CountResults(
                    fixture.Controller,
                    FpgPlayerSkillSlot.Primary);

                for (long tick = 2L; tick < 5L; tick++)
                {
                    CoverGateResult waiting = GateCoverInput(
                        fixture,
                        tick,
                        aimHeld: true,
                        primaryHeld: false,
                        aimOriginX: checked(1000 + (int)tick));
                    ProcessGatedFrame(fixture, waiting);
                    totalPrimaryEvents += CountResults(
                        fixture.Controller,
                        FpgPlayerSkillSlot.Primary);
                }

                CoverGateResult fired = GateCoverInput(
                    fixture,
                    5L,
                    aimHeld: true,
                    primaryHeld: false,
                    aimOriginX: 2000);
                Assert.That(fired.Frame.PrimaryHeld, Is.True);
                Assert.That(fired.TickInput.AimPose.Tick,
                    Is.EqualTo(new TickIndex(5L)));
                Assert.That(fired.TickInput.AimPose.Origin,
                    Is.EqualTo(SpatialVectorKey.Zero));
                Assert.That(fired.TickInput.AimPose.PoseVersion,
                    Is.EqualTo(1L));
                ProcessGatedFrame(fixture, fired);
                totalPrimaryEvents += CountResults(
                    fixture.Controller,
                    FpgPlayerSkillSlot.Primary);

                CoverGateResult following = GateCoverInput(
                    fixture,
                    6L,
                    aimHeld: true,
                    primaryHeld: false,
                    aimOriginX: 3000);
                Assert.That(following.Frame.PrimaryHeld, Is.False);
                ProcessGatedFrame(fixture, following);
                totalPrimaryEvents += CountResults(
                    fixture.Controller,
                    FpgPlayerSkillSlot.Primary);

                Assert.That(totalPrimaryEvents, Is.EqualTo(1));
            }
        }

        [Test]
        public void PlayerTickAutoReloadsHeldPrimaryOnceAndRestartsFiveTickPeek()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                FpgResolvedAimContext leftAim = CreateValidAimContext(0.25f);
                SetPrivateField(fixture.Driver, "liveAimContext", leftAim);
                SetPrivateField(fixture.Driver, "liveAttackAimContext", leftAim);
                Assert.That(
                    fixture.Player.Weapon.Magazine.RestoreAmmo(0).IsSuccess,
                    Is.True);
                int reloadStartedCount = 0;
                int primaryReleaseCount = 0;
                fixture.Driver.ActionCommitted += action =>
                {
                    if (action.Type == FpgFormalPlayerActionType.ReloadStarted)
                    {
                        reloadStartedCount++;
                    }
                    else if (action.Type
                        == FpgFormalPlayerActionType.PrimaryReleaseCommitted)
                    {
                        primaryReleaseCount++;
                    }
                };

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 0L,
                    primaryHeld: true);
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Reloading));
                Assert.That(fixture.Controller.ActiveSlot,
                    Is.EqualTo(FpgPlayerSkillSlot.Reload));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 1L,
                    primaryHeld: true);
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Reloading));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 2L,
                    primaryHeld: true);
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Ready));
                Assert.That(fixture.Player.Weapon.Magazine.Ammo,
                    Is.EqualTo(fixture.Player.Weapon.Magazine.Capacity));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);
                Assert.That(reloadStartedCount, Is.EqualTo(1));
                Assert.That(
                    fixture.Runtime.SkillExecutionIds.Peek().Value,
                    Is.EqualTo(2L));
                Assert.That(primaryReleaseCount, Is.Zero);

                for (long tick = 3L; tick < 8L; tick++)
                {
                    ProcessFormalPlayerTick(
                        fixture,
                        tick,
                        primaryHeld: true);
                    Assert.That(fixture.Driver.IsCoverPeekRequested, Is.True);
                    Assert.That(
                        fixture.Driver.CoverPeekStartedTick,
                        Is.EqualTo(new TickIndex(3L)));
                    Assert.That(fixture.Player.Exposure.State,
                        Is.EqualTo(PlayerExposureState.Withdrawn));
                    Assert.That(
                        fixture.Driver.CoverPeekDirection,
                        Is.EqualTo(FpgPlayerFacingDirection.Left));
                    Assert.That(primaryReleaseCount, Is.Zero);
                }

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 8L,
                    primaryHeld: true);

                Assert.That(fixture.Driver.CoverPeekStartedTick,
                    Is.EqualTo(new TickIndex(3L)));
                Assert.That(primaryReleaseCount, Is.EqualTo(1));
                Assert.That(fixture.ShotSink.SuccessfulQueryCount,
                    Is.EqualTo(1));
                Assert.That(fixture.ShotSink.CommittedShotCount,
                    Is.EqualTo(1));
                Assert.That(fixture.Player.Weapon.Magazine.Ammo,
                    Is.EqualTo(fixture.Player.Weapon.Magazine.Capacity - 1));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Exposed));
                Assert.That(reloadStartedCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void PlayerTickSecondaryReleaseDuringAutoReloadCancelsQueuedPeek()
        {
            using (CoverGateFixture fixture = CreateCoverGateFixture())
            {
                Assert.That(
                    fixture.Player.Weapon.Magazine.RestoreAmmo(0).IsSuccess,
                    Is.True);
                int reloadStartedCount = 0;
                int secondaryChargeStartedCount = 0;
                int secondaryReleaseCount = 0;
                fixture.Driver.ActionCommitted += action =>
                {
                    switch (action.Type)
                    {
                        case FpgFormalPlayerActionType.ReloadStarted:
                            reloadStartedCount++;
                            break;
                        case FpgFormalPlayerActionType.SecondaryChargeStarted:
                            secondaryChargeStartedCount++;
                            break;
                        case FpgFormalPlayerActionType.SecondaryReleaseCommitted:
                            secondaryReleaseCount++;
                            break;
                    }
                };

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 0L,
                    secondaryHeld: true,
                    secondaryPressed: true);
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Reloading));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 1L,
                    secondaryReleased: true);
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Reloading));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);

                ProcessFormalPlayerTick(fixture, tickValue: 2L);
                ProcessFormalPlayerTick(fixture, tickValue: 3L);

                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Ready));
                Assert.That(fixture.Player.Weapon.Magazine.Ammo,
                    Is.EqualTo(fixture.Player.Weapon.Magazine.Capacity));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);
                Assert.That(fixture.Driver.CoverPeekStartedTick.IsValid,
                    Is.False);
                Assert.That(
                    fixture.Driver.CoverPeekDirection,
                    Is.EqualTo(FpgPlayerFacingDirection.Right));
                Assert.That(fixture.Controller.IsExecuting, Is.False);
                Assert.That(reloadStartedCount, Is.EqualTo(1));
                Assert.That(
                    fixture.Runtime.SkillExecutionIds.Peek().Value,
                    Is.EqualTo(2L));
                Assert.That(secondaryChargeStartedCount, Is.Zero);
                Assert.That(secondaryReleaseCount, Is.Zero);
                Assert.That(fixture.ShotSink.SuccessfulQueryCount, Is.Zero);
                Assert.That(fixture.ShotSink.CommittedShotCount, Is.Zero);
            }
        }

        [Test]
        public void PlayerTickFinalAvailabilityFailureHasNoReleaseSideEffects()
        {
            FpgCompiledPlayerSkillDefinition delayedPrimary = CreatePrimary(
                durationTicks: 1,
                cooldownTicks: 0,
                ammoCost: 1,
                Event(10, 1, 101));
            using (CoverGateFixture fixture =
                CreateCoverGateFixture(delayedPrimary))
            {
                int primaryReleaseCount = 0;
                fixture.Driver.ActionCommitted += action =>
                {
                    if (action.Type
                        == FpgFormalPlayerActionType.PrimaryReleaseCommitted)
                    {
                        primaryReleaseCount++;
                    }
                };
                AttackShotReservation reservationBefore =
                    fixture.Runtime.IdAllocator.ReserveAttackAndShot();

                for (long tick = 0L; tick < 5L; tick++)
                {
                    ProcessFormalPlayerTick(
                        fixture,
                        tick,
                        primaryHeld: true);
                }

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 5L,
                    primaryHeld: true);
                Assert.That(fixture.Controller.IsExecuting, Is.True);
                Assert.That(fixture.Controller.ActiveSlot,
                    Is.EqualTo(FpgPlayerSkillSlot.Primary));
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.PrimaryRecovery));
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Exposed));
                Assert.That(primaryReleaseCount, Is.Zero);

                int ammoBeforeRejectedCommit =
                    fixture.Player.Weapon.Magazine.Ammo;
                fixture.Player.Weapon.Magazine.ConsumeValidated(
                    ammoBeforeRejectedCommit);
                Assert.That(fixture.Player.Weapon.Magazine.Ammo, Is.Zero);

                ProcessFormalPlayerTick(
                    fixture,
                    tickValue: 6L,
                    primaryHeld: true);

                AttackShotReservation reservationAfter =
                    fixture.Runtime.IdAllocator.ReserveAttackAndShot();
                Assert.That(fixture.Controller.IsExecuting, Is.False);
                Assert.That(fixture.Controller.ResultCount, Is.Zero);
                Assert.That(fixture.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Ready));
                Assert.That(fixture.Player.Weapon.StateUntilTick.IsValid,
                    Is.False);
                Assert.That(
                    fixture.Player.Weapon.PrimaryRecastLockedUntilTick.IsValid,
                    Is.False);
                Assert.That(fixture.Player.Weapon.Magazine.Ammo, Is.Zero);
                Assert.That(fixture.Player.Exposure.State,
                    Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(fixture.Driver.IsCoverPeekRequested, Is.False);
                Assert.That(
                    fixture.Driver.PrimaryAttackAvailability.Reason,
                    Is.EqualTo(FpgAttackUnavailableReason.NotEnoughAmmo));
                Assert.That(primaryReleaseCount, Is.Zero);
                Assert.That(fixture.ShotSink.SuccessfulQueryCount, Is.Zero);
                Assert.That(fixture.ShotSink.CommittedShotCount, Is.Zero);
                Assert.That(fixture.ShotSink.DiscardedShotCount, Is.Zero);
                Assert.That(fixture.Runtime.CombatPort.PendingPlayerHitCount,
                    Is.Zero);
                Assert.That(fixture.Runtime.CombatKernel.Trace.Count, Is.Zero);
                Assert.That(reservationAfter.AttackId,
                    Is.EqualTo(reservationBefore.AttackId));
                Assert.That(reservationAfter.ShotId,
                    Is.EqualTo(reservationBefore.ShotId));
            }
        }

        [Test]
        public void TypedActionIndexResolvesActionDuringExecution()
        {
            FpgCompiledSkillEvent actionEvent = new FpgCompiledSkillEvent(
                10,
                0,
                FpgSkillActionKind.Attack,
                0,
                targetSource: FpgSkillTargetSource.CurrentAim);
            FpgCompiledPlayerSkillDefinition primary =
                new FpgCompiledPlayerSkillDefinition(
                    new FpgCompiledSkillDefinition(
                        1,
                        new[]
                        {
                            new FpgCompiledSkillSequence(
                                FpgSkillSequenceKind.Execute,
                                0,
                                1001,
                                false,
                                new[] { actionEvent })
                        }),
                    0,
                    new[]
                    {
                        new FpgCompiledPlayerAttackAction(
                            FpgSkillAttackMode.PelletRays,
                            PelletPayload(101, 1))
                    },
                    Array.Empty<FpgCompiledPlayerProjectileAction>(),
                    Array.Empty<FpgCompiledPlayerReloadAction>());
            Assert.That(
                primary.TryResolveAction(
                    actionEvent,
                    out FpgCompiledPlayerSkillAction resolved),
                Is.True);
            Assert.That(resolved.Kind,
                Is.EqualTo(FpgPlayerSkillActionKind.PelletRay));

            FpgPlayerSkillExecutionController controller = CreateController(primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 2);
            Assert.That(
                controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(0L),
                        true,
                        true),
                    player).IsSuccess,
                Is.True);

            Assert.That(controller.ResultCount, Is.EqualTo(1));
            FpgPlayerSkillExecutionEvent result = controller.GetResult(0);
            Assert.That(result.HasGameplayAction, Is.True);
            Assert.That(result.Event.ActionKind,
                Is.EqualTo(FpgSkillActionKind.Attack));
            Assert.That(result.Event.ActionIndex, Is.Zero);
            Assert.That(result.Action.Kind,
                Is.EqualTo(FpgPlayerSkillActionKind.PelletRay));
            Assert.That(result.Action.AmmoCost, Is.EqualTo(1));
        }

        [Test]
        public void ActionLockUsesSequenceDurationAndRecastStartsOnlyOnCommit()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 5,
                cooldownTicks: 10,
                ammoCost: 1,
                Event(10, 2, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                primaryRecastTicks: 10);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(100L), true, true),
                player).IsSuccess, Is.True);

            Assert.That(controller.PlannedLastAttackTick,
                Is.EqualTo(new TickIndex(102L)));
            Assert.That(controller.ActionLockedUntilTick,
                Is.EqualTo(new TickIndex(106L)));
            Assert.That(player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(106L)));
            Assert.That(
                player.Weapon.PrimaryRecastLockedUntilTick.IsValid,
                Is.False);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(101L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(102L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));

            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();
            CommitPrimaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                102L);
            Assert.That(
                player.Weapon.PrimaryRecastLockedUntilTick,
                Is.EqualTo(new TickIndex(112L)));
            Assert.That(
                player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(106L)));

            for (long tick = 103L; tick <= 105L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(new TickIndex(tick), true, true),
                    player).IsSuccess, Is.True);
            }

            for (long tick = 106L; tick < 112L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(new TickIndex(tick), true, true),
                    player).IsSuccess, Is.True);
                Assert.That(controller.IsExecuting, Is.False);
                Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(player.Weapon.LastRejectReason,
                    Is.EqualTo(RejectReason.Cooldown));
            }

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(112L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSlot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
        }

        [Test]
        public void HardInterruptWithoutCommitDoesNotInventRecastCooldown()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 3,
                cooldownTicks: 4,
                ammoCost: 1,
                Event(10, 0, 101),
                Event(20, 2, 101),
                Event(30, 3, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);

            controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player);
            DomainResult interrupted = controller.HardInterrupt(
                new TickIndex(1L),
                player.Weapon);

            Assert.That(interrupted.IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(2));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(20));
            Assert.That(controller.GetResult(1).Event.EventId, Is.EqualTo(30));
            Assert.That(controller.GetResult(0).Outcome,
                Is.EqualTo(FpgSkillEventOutcome.Canceled));
            Assert.That(controller.GetResult(1).Outcome,
                Is.EqualTo(FpgSkillEventOutcome.Canceled));
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(
                player.Weapon.State,
                Is.EqualTo(WeaponState.Ready));
            Assert.That(
                player.Weapon.PrimaryRecastLockedUntilTick.IsValid,
                Is.False);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(
                controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(1L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(10));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
        }

        [Test]
        public void FailedGameplayAbortWithoutCommitDoesNotInventRecastCooldown()
        {
            FpgCompiledPlayerSkillDefinition primary = CreatePrimary(
                durationTicks: 3,
                cooldownTicks: 4,
                ammoCost: 1,
                Event(10, 0, 101),
                Event(20, 3, 101));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            PreparePrimaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                0L);
            Assert.That(release.Attack.AttackId, Is.EqualTo(new AttackId(1L)));
            Assert.That(release.Attack.ShotId, Is.EqualTo(new ShotId(1L)));

            release.Reset();
            controller.AbortAfterProcessedTick(player.Weapon);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(
                player.Weapon.State,
                Is.EqualTo(WeaponState.Ready));
            Assert.That(
                player.Weapon.PrimaryRecastLockedUntilTick.IsValid,
                Is.False);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
            Assert.That(next.AttackId, Is.EqualTo(new AttackId(1L)));
            Assert.That(next.ShotId, Is.EqualTo(new ShotId(1L)));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(
                controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(1L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(10));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
        }

        [Test]
        public void HardInterruptPreservesRecastFromCommittedAttackOnly()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 3,
                    cooldownTicks: 4,
                    ammoCost: 1,
                    Event(10, 0, 101),
                    Event(20, 3, 101)));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                primaryRecastTicks: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            CommitPrimaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                0L);

            Assert.That(controller.HardInterrupt(
                new TickIndex(1L),
                player.Weapon).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Event.EventId, Is.EqualTo(20));
            Assert.That(controller.GetResult(0).Outcome,
                Is.EqualTo(FpgSkillEventOutcome.Canceled));
            Assert.That(player.Weapon.State,
                Is.EqualTo(WeaponState.PrimaryRecovery));
            Assert.That(player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(4L)));
            Assert.That(player.Weapon.PrimaryRecastLockedUntilTick,
                Is.EqualTo(new TickIndex(4L)));

            for (long tick = 1L; tick < 4L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(new TickIndex(tick), true, true),
                    player).IsSuccess, Is.True);
                Assert.That(controller.IsExecuting, Is.False);
            }

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(4L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
        }

        [Test]
        public void ImmediateSecondaryHeldRepeatsAtCommittedRecastBoundary()
        {
            FpgCompiledPlayerSkillDefinition secondary =
                CreateImmediateSecondary(
                    durationTicks: 60,
                    cooldownTicks: 30,
                    ammoCost: 1);
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                secondary,
                SecondaryTriggerMode.ImmediateRepeatWhileHeld);
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                secondaryTriggerMode:
                    SecondaryTriggerMode.ImmediateRepeatWhileHeld,
                secondaryRecastTicks: 30);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Slot,
                Is.EqualTo(FpgPlayerSkillSlot.Secondary));
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                0L);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            SkillExecutionId firstExecution =
                controller.GetSequenceFrame(0).ExecutionId;

            for (long tick = 1L; tick < 30L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(tick),
                        aimHeld: true,
                        secondaryHeld: true),
                    player).IsSuccess, Is.True);
                Assert.That(controller.ResultCount, Is.Zero);
                Assert.That(controller.IsExecuting, Is.True);
                Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
                Assert.That(
                    controller.GetSequenceFrame(0).ExecutionId,
                    Is.EqualTo(firstExecution));
                Assert.That(
                    controller.GetSequenceFrame(0).State,
                    Is.EqualTo(FpgSkillExecutionState.Running));
            }
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.Cooldown));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(30L),
                    aimHeld: true,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(3));
            Assert.That(
                controller.GetSequenceFrame(0).ExecutionId,
                Is.EqualTo(firstExecution));
            Assert.That(
                controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(
                controller.GetSequenceFrame(1).ExecutionId,
                Is.EqualTo(firstExecution));
            Assert.That(
                controller.GetSequenceFrame(1).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));
            Assert.That(
                controller.GetSequenceFrame(2).ExecutionId,
                Is.Not.EqualTo(firstExecution));
            Assert.That(
                controller.GetSequenceFrame(2).StartTick,
                Is.EqualTo(new TickIndex(30L)));
            Assert.That(
                controller.GetSequenceFrame(2).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                30L);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(2));
            Assert.That(
                player.Weapon.SecondaryRecastLockedUntilTick,
                Is.EqualTo(new TickIndex(60L)));
        }

        [Test]
        public void ImmediateSecondaryFailedBoundaryCheckKeepsExecuteRunning()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateImmediateSecondary(
                    durationTicks: 60,
                    cooldownTicks: 30,
                    ammoCost: 1),
                SecondaryTriggerMode.ImmediateRepeatWhileHeld);
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 1,
                secondaryTriggerMode:
                    SecondaryTriggerMode.ImmediateRepeatWhileHeld,
                secondaryRecastTicks: 30);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                0L);
            SkillExecutionId firstExecution =
                controller.GetSequenceFrame(0).ExecutionId;
            Assert.That(player.Weapon.Magazine.Ammo, Is.Zero);

            for (long tick = 1L; tick <= 30L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(tick),
                        aimHeld: true,
                        secondaryHeld: true),
                    player).IsSuccess, Is.True);
            }

            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.Execute));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(
                controller.GetSequenceFrame(0).ExecutionId,
                Is.EqualTo(firstExecution));
            Assert.That(
                controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotEnoughAmmo));
        }

        [Test]
        public void ChargeLoopHoldsSingleExecutionUntilReleaseOrInterrupt()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(minimumChargeTicks: 4));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 4);

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.None));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(1L),
                    aimHeld: true,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.ChargeLoop));
            SkillExecutionId loopExecutionId =
                controller.GetSequenceFrame(0).ExecutionId;

            for (long tick = 2L; tick <= 6L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(
                        new TickIndex(tick),
                        aimHeld: true,
                        secondaryHeld: true),
                    player).IsSuccess, Is.True);
                Assert.That(controller.IsExecuting, Is.True);
                Assert.That(controller.ActiveSequenceKind,
                    Is.EqualTo(FpgSkillSequenceKind.ChargeLoop));
                Assert.That(
                    controller.GetSequenceFrame(0).ExecutionId,
                    Is.EqualTo(loopExecutionId));
                Assert.That(
                    controller.GetSequenceFrame(0).State,
                    Is.EqualTo(FpgSkillExecutionState.Running));
            }
        }

        [Test]
        public void EarlyChargeReleaseStartsCancelWithoutAmmoOrRecast()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 3,
                    cancelDurationTicks: 3));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 3);

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(1L, 2L, InputEdgeType.SecondaryReleased),
                player).IsSuccess, Is.True);

            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.Cancel));
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
            Assert.That(
                player.Weapon.SecondaryRecastLockedUntilTick.IsValid,
                Is.False);
            Assert.That(
                controller.GetSecondaryChargeProgress(
                    player.Weapon,
                    new TickIndex(1L)),
                Is.Zero);
        }

        [Test]
        public void FullChargeReleaseCommitsAttackAndRecastAtReleaseTick()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 2,
                    releaseDurationTicks: 2,
                    cancelDurationTicks: 2,
                    cooldownTicks: 4));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 2,
                secondaryRecastTicks: 4);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(1L),
                    aimHeld: true,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);
            Assert.That(
                controller.GetSecondaryChargeProgress(
                    player.Weapon,
                    new TickIndex(1L)),
                Is.EqualTo(0.5f));
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(2L, 2L, InputEdgeType.SecondaryReleased),
                player).IsSuccess, Is.True);

            Assert.That(controller.ResultCount, Is.EqualTo(1));
            Assert.That(controller.GetResult(0).Slot,
                Is.EqualTo(FpgPlayerSkillSlot.Secondary));
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.Release));
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                2L);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(3));
            Assert.That(player.Weapon.SecondaryRecastLockedUntilTick,
                Is.EqualTo(new TickIndex(6L)));
            Assert.That(player.Weapon.StateUntilTick,
                Is.EqualTo(new TickIndex(5L)));
        }

        [Test]
        public void CompletedReleaseAutomaticallyStartsAuthoredCancelPostDelay()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 1,
                    releaseDurationTicks: 1,
                    cancelDurationTicks: 3,
                    cooldownTicks: 1));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 1,
                secondaryRecastTicks: 1);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(1L, 2L, InputEdgeType.SecondaryReleased),
                player).IsSuccess, Is.True);
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                1L);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(2L), aimHeld: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.IsSecondaryEndPending, Is.True);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(3L), aimHeld: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.IsSecondaryEndPending, Is.False);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.Cancel));
            Assert.That(controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
        }

        [Test]
        public void PrimaryInterruptsCancelAfterItsOwnEligibilityChecksPass()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 2,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 1,
                    cancelDurationTicks: 5,
                    cooldownTicks: 1));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 1,
                secondaryRecastTicks: 1);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(1L, 2L, InputEdgeType.SecondaryReleased),
                player).IsSuccess, Is.True);
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                1L);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(2L), aimHeld: true),
                player).IsSuccess, Is.True);
            SkillExecutionId cancelExecution =
                controller.GetSequenceFrame(0).ExecutionId;

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(3L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSlot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.Execute));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(3));
            Assert.That(controller.GetSequenceFrame(0).ExecutionId,
                Is.EqualTo(cancelExecution));
            Assert.That(controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(controller.GetSequenceFrame(1).ExecutionId,
                Is.EqualTo(cancelExecution));
            Assert.That(controller.GetSequenceFrame(1).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));
            Assert.That(controller.GetSequenceFrame(2).Slot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
        }

        [Test]
        public void PrimaryCooldownFailureLeavesCancelTimelineRunning()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 6,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 1,
                    cancelDurationTicks: 10,
                    cooldownTicks: 1));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 1,
                primaryRecastTicks: 6,
                secondaryRecastTicks: 1);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            CommitPrimaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                0L);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    1L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(2L, 2L, InputEdgeType.SecondaryReleased),
                player).IsSuccess, Is.True);
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                2L);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(3L), aimHeld: true),
                player).IsSuccess, Is.True);
            SkillExecutionId cancelExecution =
                controller.GetSequenceFrame(0).ExecutionId;

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(4L), true, true),
                player).IsSuccess, Is.True);
            AssertCancelContinues(controller, cancelExecution);
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.Cooldown));

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(5L), aimHeld: true),
                player).IsSuccess, Is.True);
            AssertCancelContinues(controller, cancelExecution);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(6L), true, true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ActiveSlot,
                Is.EqualTo(FpgPlayerSkillSlot.None));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(3));
            Assert.That(controller.GetSequenceFrame(0).ExecutionId,
                Is.EqualTo(cancelExecution));
            Assert.That(controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(controller.GetSequenceFrame(1).ExecutionId,
                Is.EqualTo(cancelExecution));
            Assert.That(controller.GetSequenceFrame(1).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));
            Assert.That(controller.GetSequenceFrame(2).Slot,
                Is.EqualTo(FpgPlayerSkillSlot.Primary));
        }

        [Test]
        public void SecondaryCannotInterruptCancelUntilCooldownAmmoAndExposurePass()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 1,
                    cancelDurationTicks: 12,
                    cooldownTicks: 6));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 2,
                minimumChargeTicks: 1,
                secondaryRecastTicks: 6);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(1L, 2L, InputEdgeType.SecondaryReleased),
                player).IsSuccess, Is.True);
            CommitSecondaryEvent(
                controller.GetResult(0),
                player,
                ids,
                release,
                1L);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(2L), aimHeld: true),
                player).IsSuccess, Is.True);
            SkillExecutionId cancelExecution =
                controller.GetSequenceFrame(0).ExecutionId;

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    3L,
                    3L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            AssertCancelContinues(controller, cancelExecution);
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.Cooldown));

            for (long tick = 4L; tick < 7L; tick++)
            {
                Assert.That(controller.ProcessFrame(
                    PlayerInputFrame.Empty(new TickIndex(tick), aimHeld: true),
                    player).IsSuccess, Is.True);
            }

            Assert.That(player.Weapon.Magazine.RestoreAmmo(0).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    7L,
                    4L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            AssertCancelContinues(controller, cancelExecution);
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotEnoughAmmo));

            Assert.That(player.Weapon.Magazine.RestoreAmmo(1).IsSuccess, Is.True);
            Assert.That(player.Exposure.ApplyCombatPosture(
                false,
                new TickIndex(7L),
                false,
                out _).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    8L,
                    5L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            AssertCancelContinues(controller, cancelExecution);
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotExposed));

            player.Exposure.ForceExposed(new TickIndex(8L), out _);
            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    9L,
                    6L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(player.Weapon.State,
                Is.EqualTo(WeaponState.AltCharging));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(3));
            Assert.That(controller.GetSequenceFrame(0).ExecutionId,
                Is.EqualTo(cancelExecution));
            Assert.That(controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
            Assert.That(controller.GetSequenceFrame(1).ExecutionId,
                Is.EqualTo(cancelExecution));
            Assert.That(controller.GetSequenceFrame(1).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));
            Assert.That(controller.GetSequenceFrame(2).Sequence.Kind,
                Is.EqualTo(FpgSkillSequenceKind.ChargeEnter));
        }

        [Test]
        public void HardInterruptClearsHeldChargeAndPendingPostDelay()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 2,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary(
                    minimumChargeTicks: 4,
                    cancelDurationTicks: 3));
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                minimumChargeTicks: 4);

            Assert.That(controller.ProcessFrame(
                SecondaryEdge(
                    0L,
                    1L,
                    InputEdgeType.SecondaryPressed,
                    held: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(1L),
                    aimHeld: true,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.ChargeLoop));

            Assert.That(controller.HardInterrupt(
                new TickIndex(2L),
                player.Weapon).IsSuccess, Is.True);
            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.IsSecondaryEndPending, Is.False);
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(player.Weapon.SecondaryChargeStartedTick.IsValid,
                Is.False);
            Assert.That(
                player.Weapon.SecondaryRecastLockedUntilTick.IsValid,
                Is.False);
            Assert.That(player.Weapon.Magazine.Ammo, Is.EqualTo(4));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Canceled));
        }

        [Test]
        public void CancelingChargeStartsTheAuthoredCancelSequence()
        {
            FpgCompiledPlayerSkillDefinition secondary =
                CreateChargeSecondary();
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                secondary);
            PlayerRuntime player = CreatePlayer(magazineCapacity: 4);
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(
                    new InputSequence(1L),
                    InputEdgeType.SecondaryPressed)
            };

            Assert.That(controller.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(0L),
                    true,
                    false,
                    edges,
                    edges.Length,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);
            SkillExecutionId chargeExecutionId =
                controller.GetSequenceFrame(0).ExecutionId;

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(
                    new TickIndex(1L),
                    aimHeld: false,
                    cancelSecondary: true),
                player).IsSuccess, Is.True);

            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            FpgPlayerSkillSequenceFrame cancelFrame =
                controller.GetSequenceFrame(0);
            Assert.That(
                cancelFrame.Sequence.Kind,
                Is.EqualTo(FpgSkillSequenceKind.Cancel));
            Assert.That(
                cancelFrame.State,
                Is.EqualTo(FpgSkillExecutionState.Completed));
            Assert.That(
                cancelFrame.ExecutionId,
                Is.Not.EqualTo(chargeExecutionId));
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
        }

        [Test]
        public void SecondaryChargeWithInsufficientAmmoDoesNotStartPresentationTimeline()
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary());
            PlayerRuntime player = CreatePlayer(magazineCapacity: 2);
            Assert.That(player.Weapon.Magazine.RestoreAmmo(0).IsSuccess, Is.True);
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(
                    new InputSequence(1L),
                    InputEdgeType.SecondaryPressed)
            };

            Assert.That(controller.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(0L),
                    true,
                    false,
                    edges,
                    edges.Length,
                    secondaryHeld: true),
                player).IsSuccess, Is.True);

            Assert.That(controller.IsExecuting, Is.False);
            Assert.That(controller.ResultCount, Is.Zero);
            Assert.That(controller.SequenceFrameCount, Is.Zero);
            Assert.That(player.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(player.Weapon.Magazine.Ammo, Is.Zero);
            Assert.That(player.Weapon.LastRejectReason,
                Is.EqualTo(RejectReason.NotEnoughAmmo));
        }

        [Test]
        public void SequenceFramesResolveAnimationVariantFromExecutionId()
        {
            FpgCompiledSkillSequence sequence =
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    0,
                    1001,
                    false,
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    0,
                    0,
                    new[] { 1002 },
                    new[] { Event(10, 0, 101) });
            FpgCompiledPlayerSkillDefinition primary = Definition(
                1,
                0,
                sequence,
                PelletPayload(101, 1));
            FpgPlayerSkillExecutionController controller = CreateController(
                primary);
            PlayerRuntime player = CreatePlayer(
                magazineCapacity: 4,
                primaryRecastTicks: 1);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                player).IsSuccess, Is.True);
            FpgPlayerSkillSequenceFrame first =
                controller.GetSequenceFrame(0);
            Assert.That(first.ResolvedAnimationId, Is.EqualTo(1001));
            Assert.That(
                first.ResolvedAnimationId,
                Is.EqualTo(sequence.ResolveAnimation(first.ExecutionId)));
            CommitPrimaryEvent(controller.GetResult(0), player, ids, release, 0L);

            Assert.That(controller.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(1L), true, true),
                player).IsSuccess, Is.True);
            FpgPlayerSkillSequenceFrame second =
                controller.GetSequenceFrame(0);
            Assert.That(second.ExecutionId, Is.Not.EqualTo(first.ExecutionId));
            Assert.That(second.ResolvedAnimationId, Is.EqualTo(1002));
            Assert.That(
                second.ResolvedAnimationId,
                Is.EqualTo(sequence.ResolveAnimation(second.ExecutionId)));
        }

        private static CoverGateFixture CreateCoverGateFixture(
            FpgCompiledPlayerSkillDefinition primary = null)
        {
            FpgPlayerSkillExecutionController controller = CreateController(
                primary ?? CreatePrimary(
                    durationTicks: 0,
                    cooldownTicks: 0,
                    ammoCost: 1,
                    Event(10, 0, 101)),
                CreateChargeSecondary());
            PlayerRuntime player = CreatePlayer(magazineCapacity: 8);
            GameObject owner = new GameObject("CoverPeekGateTest");
            CombatKernel combatKernel = null;
            try
            {
                FpgFormalPlayerTickDriver driver =
                    owner.AddComponent<FpgFormalPlayerTickDriver>();
                FpgRoomEncounterDirector director =
                    owner.AddComponent<FpgRoomEncounterDirector>();
                SetPrivateField(
                    director,
                    "<Phase>k__BackingField",
                    FpgEncounterPhase.Combat);
                SetPrivateField(
                    driver,
                    "playerSecondaryTriggerMode",
                    SecondaryTriggerMode.ChargeRelease);
                SetPrivateField(
                    driver,
                    "skillExecutionController",
                    controller);
                SetPrivateField(driver, "encounterDirector", director);
                SetPrivateField(
                    driver,
                    "liveAimContext",
                    CreateValidAimContext());
                SetPrivateField(
                    driver,
                    "liveAttackAimContext",
                    CreateValidAimContext());
                SetPrivateField(
                    driver,
                    "attackQuerySettings",
                    UnityAttackQuerySettings.Default);
                UnityBattleInputSource inputSource =
                    new UnityBattleInputSource();
                inputSource.SetAimPose(new AimPoseSnapshot(
                    new TickIndex(0L),
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(
                        0,
                        0,
                        SpatialContract.DirectionUnits),
                    new SpatialVectorKey(
                        SpatialContract.DirectionUnits,
                        0,
                        0),
                    new SpatialVectorKey(
                        0,
                        SpatialContract.DirectionUnits,
                        0),
                    poseVersion: 1L));
                SetPrivateField(driver, "inputSource", inputSource);
                SetPrivateField(driver, "playerConfigured", true);
                FpgFormalCombatRuntimeBundle combatRuntime =
                    CreateReadyCombatRuntime(
                        owner,
                        player,
                        out combatKernel,
                        out CountingPlayerShotPresentationSink shotSink);
                SetPrivateField(director, "combatRuntime", combatRuntime);
                return new CoverGateFixture(
                    owner,
                    driver,
                    director,
                    controller,
                    player,
                    combatRuntime,
                    combatKernel,
                    shotSink);
            }
            catch
            {
                combatKernel?.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
                throw;
            }
        }

        private static FpgFormalCombatRuntimeBundle CreateReadyCombatRuntime(
            GameObject owner,
            PlayerRuntime player,
            out CombatKernel combatKernel,
            out CountingPlayerShotPresentationSink shotSink)
        {
            SessionIdAllocator ids = new SessionIdAllocator();
            combatKernel = new CombatKernel(
                projectileBudgetCapacity: 2,
                impactCapacity: 16,
                shotTargetCapacity: 16,
                impactQueueCapacity: 16,
                traceCapacity: 32,
                projectileReservationCapacity: 2);
            HitboxRegistry registry = owner.AddComponent<HitboxRegistry>();
            Assert.That(registry.TryInitialize(out string registryError),
                Is.True,
                registryError);
            shotSink = new CountingPlayerShotPresentationSink();
            UnityAttackQueryPort attackQueryPort = new UnityAttackQueryPort(
                registry,
                UnityAttackQuerySettings.Default,
                new EmptyPhysicsQueryBackend(),
                shotSink);
            FpgMultiEnemyCombatPort combatPort =
                new FpgMultiEnemyCombatPort(
                    combatKernel,
                    player,
                    ids,
                    new FpgMultiEnemyCombatCapacity(
                        enemyCapacity: 1,
                        playerHitCommandCapacity: 8,
                        attackScheduleCapacity: 2,
                        projectileCapacity: 2,
                        threatAdvanceCapacity: 2,
                        perEnemyThreatCapacity: 1,
                        summonCapacity: 1,
                        maxTotalSummons: 0,
                        maxSummonRecursionDepth: 0,
                        vitalsEventCapacity: 8,
                        damageFeedbackCapacity: 8,
                        skillImpactPresentationCapacity: 8),
                    new TickDuration(2),
                    new FpgEmptyProjectileWorldPort(),
                    new RejectingSummonSink(),
                    playerProjectileAreaQueryPort: attackQueryPort);
            ConstructorInfo[] constructors =
                typeof(FpgFormalCombatRuntimeBundle).GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructors, Has.Length.EqualTo(1));
            return (FpgFormalCombatRuntimeBundle)constructors[0].Invoke(
                new object[]
                {
                    ids,
                    new FpgEncounterRunContext(
                        123UL,
                        "player-tick-driver-tests",
                        0,
                        FpgEncounterRunContext.BasisPointsOne,
                        0),
                    new FpgSkillExecutionIdAllocator(),
                    combatKernel,
                    player,
                    combatPort,
                    null,
                    null,
                    null,
                    shotSink,
                    attackQueryPort,
                    null,
                    null,
                    null,
                    registry,
                    null,
                    null
                });
        }

        private static FpgResolvedAimContext CreateValidAimContext(
            float viewportX = 0.5f)
        {
            return new FpgResolvedAimContext(
                new Vector2(viewportX, 0.5f),
                Vector3.zero,
                Vector3.forward,
                Vector3.forward * 10f,
                Vector3.zero,
                Vector3.forward,
                Vector3.forward * 10f,
                FpgResolvedAimTargetType.None,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                GeometryId.Invalid,
                string.Empty,
                string.Empty,
                version: 1L,
                frozenVersion: 0L,
                distance: 10f);
        }

        private static CoverGateResult GateCoverInput(
            CoverGateFixture fixture,
            long tickValue,
            bool aimHeld,
            bool primaryHeld,
            int aimOriginX,
            InputEdgeCommand[] edgeCommands = null)
        {
            TickIndex tick = new TickIndex(tickValue);
            PlayerInputFrame frame = new PlayerInputFrame(
                tick,
                aimHeld,
                primaryHeld,
                edgeCommands,
                edgeCommands == null ? 0 : edgeCommands.Length);
            AimPoseSnapshot aimPose = new AimPoseSnapshot(
                tick,
                new SpatialVectorKey(aimOriginX, 0, 0),
                new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits),
                new SpatialVectorKey(SpatialContract.DirectionUnits, 0, 0),
                new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0),
                checked(tickValue + 1L));
            BattleTickInput tickInput = new BattleTickInput(frame, aimPose);
            MethodInfo gateMethod = typeof(FpgFormalPlayerTickDriver).GetMethod(
                "TryBuildCoverGatedInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(gateMethod, Is.Not.Null);
            object[] arguments =
            {
                tickInput,
                frame,
                fixture.Player,
                tick,
                default(BattleTickInput),
                default(PlayerInputFrame)
            };

            DomainResult result = (DomainResult)gateMethod.Invoke(
                fixture.Driver,
                arguments);
            Assert.That(result.IsSuccess, Is.True);
            return new CoverGateResult(
                result,
                (BattleTickInput)arguments[4],
                (PlayerInputFrame)arguments[5]);
        }

        private static void ProcessGatedFrame(
            CoverGateFixture fixture,
            CoverGateResult gated)
        {
            Assert.That(gated.Result.IsSuccess, Is.True);
            Assert.That(fixture.Controller.ProcessFrame(
                gated.Frame,
                fixture.Player).IsSuccess, Is.True);
        }

        private static void ProcessFormalPlayerTick(
            CoverGateFixture fixture,
            long tickValue,
            bool primaryHeld = false,
            bool secondaryHeld = false,
            bool secondaryPressed = false,
            bool secondaryReleased = false)
        {
            fixture.Driver.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: primaryHeld,
                secondaryPressed: secondaryPressed,
                secondaryReleased: secondaryReleased,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false,
                secondaryHeld: secondaryHeld));
            DomainResult result = fixture.Driver.ProcessPlayerTick(
                new TickIndex(tickValue),
                fixture.Runtime);
            Assert.That(
                result.IsSuccess,
                Is.True,
                $"Formal player tick {tickValue} was rejected: {result.RejectReason}.");
        }

        private static int CountResults(
            FpgPlayerSkillExecutionController controller,
            FpgPlayerSkillSlot slot)
        {
            int count = 0;
            for (int index = 0; index < controller.ResultCount; index++)
            {
                if (controller.GetResult(index).Slot == slot)
                {
                    count++;
                }
            }

            return count;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Could not find field " + fieldName + ".");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(
            object target,
            string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Could not find field " + fieldName + ".");
            return (T)field.GetValue(target);
        }

        private sealed class CoverGateFixture : IDisposable
        {
            public CoverGateFixture(
                GameObject owner,
                FpgFormalPlayerTickDriver driver,
                FpgRoomEncounterDirector director,
                FpgPlayerSkillExecutionController controller,
                PlayerRuntime player,
                FpgFormalCombatRuntimeBundle runtime,
                CombatKernel combatKernel,
                CountingPlayerShotPresentationSink shotSink)
            {
                Owner = owner;
                Driver = driver;
                Director = director;
                Controller = controller;
                Player = player;
                Runtime = runtime;
                CombatKernel = combatKernel;
                ShotSink = shotSink;
            }

            public GameObject Owner { get; }
            public FpgFormalPlayerTickDriver Driver { get; }
            public FpgRoomEncounterDirector Director { get; }
            public FpgPlayerSkillExecutionController Controller { get; }
            public PlayerRuntime Player { get; }
            public FpgFormalCombatRuntimeBundle Runtime { get; }
            public CombatKernel CombatKernel { get; }
            public CountingPlayerShotPresentationSink ShotSink { get; }

            public void Dispose()
            {
                if (Owner != null)
                {
                    SetPrivateField(Director, "combatRuntime", null);
                    CombatKernel?.Dispose();
                    UnityEngine.Object.DestroyImmediate(Owner);
                }
            }
        }

        private sealed class EmptyPhysicsQueryBackend :
            IUnityPhysicsQueryBackend
        {
            public int Capacity => SpatialContract.AttackQueryCandidateCapacity;

            public void SyncTransforms()
            {
            }

            public NonAllocPhysicsQueryResult RaycastNonAlloc(
                Vector3 origin,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }

            public NonAllocPhysicsQueryResult SphereCastNonAlloc(
                Vector3 origin,
                float radius,
                Vector3 direction,
                UnityPhysicsHit[] output,
                float maxDistance,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }

            public NonAllocPhysicsQueryResult OverlapSphereNonAlloc(
                Vector3 position,
                float radius,
                Collider[] output,
                int layerMask,
                QueryTriggerInteraction triggerInteraction)
            {
                return new NonAllocPhysicsQueryResult(0, false);
            }
        }

        private sealed class CountingPlayerShotPresentationSink :
            IPlayerShotQueryCaptureSink,
            ICommittedPlayerShotPresentationSink,
            IUncommittedPlayerShotPresentationSink
        {
            public int SuccessfulQueryCount { get; private set; }
            public int CommittedShotCount { get; private set; }
            public int DiscardedShotCount { get; private set; }

            public bool TryCaptureSuccessfulQuery(
                in PlayerShotQueryCapture capture)
            {
                SuccessfulQueryCount++;
                return true;
            }

            public void PublishCommittedShot(
                AttackId attackId,
                WeaponReleaseKind releaseKind)
            {
                CommittedShotCount++;
            }

            public void DiscardUncommittedShot(AttackId attackId)
            {
                DiscardedShotCount++;
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

        private readonly struct CoverGateResult
        {
            public CoverGateResult(
                DomainResult result,
                BattleTickInput tickInput,
                PlayerInputFrame frame)
            {
                Result = result;
                TickInput = tickInput;
                Frame = frame;
            }

            public DomainResult Result { get; }
            public BattleTickInput TickInput { get; }
            public PlayerInputFrame Frame { get; }
        }

        private static FpgPlayerSkillExecutionController CreateController(
            FpgCompiledPlayerSkillDefinition primary)
        {
            return CreateController(
                primary,
                CreateSecondary(),
                SecondaryTriggerMode.ChargeRelease);
        }

        private static FpgPlayerSkillExecutionController CreateController(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary)
        {
            return CreateController(
                primary,
                secondary,
                SecondaryTriggerMode.ChargeRelease);
        }

        private static FpgPlayerSkillExecutionController CreateController(
            FpgCompiledPlayerSkillDefinition primary,
            FpgCompiledPlayerSkillDefinition secondary,
            SecondaryTriggerMode secondaryTriggerMode)
        {
            bool created = FpgPlayerSkillExecutionController.TryCreate(
                primary,
                secondary,
                CreateReload(),
                secondaryTriggerMode,
                out FpgPlayerSkillExecutionController controller,
                out string error);
            Assert.That(created, Is.True, error);
            return controller;
        }

        private static FpgCompiledPlayerSkillDefinition CreatePrimary(
            int durationTicks,
            int cooldownTicks,
            int ammoCost,
            params FpgCompiledSkillEvent[] events)
        {
            return Definition(
                1,
                cooldownTicks,
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    durationTicks,
                    1001,
                    false,
                    events),
                PelletPayload(101, ammoCost));
        }

        private static FpgCompiledPlayerSkillDefinition CreateSecondary()
        {
            return CreateImmediateSecondary(
                durationTicks: 0,
                cooldownTicks: 2,
                ammoCost: 1);
        }

        private static FpgCompiledPlayerSkillDefinition CreateImmediateSecondary(
            int durationTicks,
            int cooldownTicks,
            int ammoCost)
        {
            return Definition(
                2,
                cooldownTicks,
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    durationTicks,
                    1002,
                    false,
                    new[] { Event(40, 0, 201) }),
                new FpgCompiledPlayerSkillAction(
                    FpgPlayerSkillActionKind.AreaAtFirstSurface,
                    ammoCost,
                    new DamageSpec(8, 2),
                    QueryPolicy.DirectThenArea,
                    AttackQueryMode.AreaAtFirstSurface,
                    1,
                    2,
                    0,
                    1,
                    1,
                    WeaponDefinition.PlayerAttackTargetKinds));
        }

        private static FpgCompiledPlayerSkillDefinition CreateChargeSecondary(
            int minimumChargeTicks = 2,
            int releaseDurationTicks = 0,
            int cancelDurationTicks = 0,
            int cooldownTicks = 2,
            int ammoCost = 1)
        {
            return new FpgCompiledPlayerSkillDefinition(
                new FpgCompiledSkillDefinition(
                    2,
                    new[]
                    {
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.Execute,
                            0,
                            2000,
                            false,
                            Array.Empty<FpgCompiledSkillEvent>()),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.ChargeEnter,
                            0,
                            2001,
                            false,
                            Array.Empty<FpgCompiledSkillEvent>()),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.ChargeLoop,
                            0,
                            2002,
                            true,
                            Array.Empty<FpgCompiledSkillEvent>(),
                            holdUntilCanceled: true),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.Release,
                            releaseDurationTicks,
                            2003,
                            false,
                            new[] { Event(40, 0, 201) }),
                        new FpgCompiledSkillSequence(
                            FpgSkillSequenceKind.Cancel,
                            cancelDurationTicks,
                            2004,
                            false,
                            Array.Empty<FpgCompiledSkillEvent>())
                    }),
                cooldownTicks,
                new[]
                {
                    new FpgCompiledPlayerAttackAction(
                        FpgSkillAttackMode.AreaAtFirstSurface,
                        new FpgCompiledPlayerSkillAction(
                            FpgPlayerSkillActionKind.AreaAtFirstSurface,
                            ammoCost,
                            new DamageSpec(8, 2),
                            QueryPolicy.DirectThenArea,
                            AttackQueryMode.AreaAtFirstSurface,
                            1,
                            2,
                            0,
                            1,
                            1,
                            WeaponDefinition.PlayerAttackTargetKinds))
                },
                Array.Empty<FpgCompiledPlayerProjectileAction>(),
                Array.Empty<FpgCompiledPlayerReloadAction>(),
                minimumChargeTicks);
        }

        private static FpgCompiledPlayerSkillDefinition CreateReload()
        {
            FpgCompiledSkillEvent reloadEvent = new FpgCompiledSkillEvent(
                50,
                1,
                FpgSkillActionKind.CommitReload,
                0,
                targetSource: FpgSkillTargetSource.Self);
            return Definition(
                3,
                0,
                new FpgCompiledSkillSequence(
                    FpgSkillSequenceKind.Execute,
                    1,
                    1003,
                    false,
                    new[] { reloadEvent }),
                new FpgCompiledPlayerSkillAction(
                    FpgPlayerSkillActionKind.ReloadCommit,
                    0,
                    new DamageSpec(0, 0),
                    QueryPolicy.None,
                    AttackQueryMode.Legacy,
                    0,
                    0,
                    0,
                    0,
                    0,
                    AttackTargetKinds.None));
        }

        private static FpgCompiledPlayerSkillDefinition Definition(
            int skillId,
            int cooldownTicks,
            FpgCompiledSkillSequence sequence,
            FpgCompiledPlayerSkillAction payload)
        {
            FpgCompiledPlayerAttackAction[] attacks =
                Array.Empty<FpgCompiledPlayerAttackAction>();
            FpgCompiledPlayerReloadAction[] reloads =
                Array.Empty<FpgCompiledPlayerReloadAction>();
            if (payload.Kind == FpgPlayerSkillActionKind.ReloadCommit)
            {
                reloads = new[]
                {
                    new FpgCompiledPlayerReloadAction(payload)
                };
            }
            else
            {
                FpgSkillAttackMode mode = payload.Kind
                        == FpgPlayerSkillActionKind.PelletRay
                    ? FpgSkillAttackMode.PelletRays
                    : FpgSkillAttackMode.AreaAtFirstSurface;
                attacks = new[]
                {
                    new FpgCompiledPlayerAttackAction(mode, payload)
                };
            }

            return new FpgCompiledPlayerSkillDefinition(
                new FpgCompiledSkillDefinition(skillId, new[] { sequence }),
                cooldownTicks,
                attacks,
                System.Array.Empty<FpgCompiledPlayerProjectileAction>(),
                reloads);
        }

        private static FpgCompiledPlayerSkillAction PelletPayload(
            int slotId,
            int ammoCost)
        {
            return new FpgCompiledPlayerSkillAction(
                FpgPlayerSkillActionKind.PelletRay,
                ammoCost,
                new DamageSpec(4, 1),
                QueryPolicy.PelletRays,
                AttackQueryMode.FirstSurfacePenetration,
                1,
                1,
                0,
                0,
                0,
                WeaponDefinition.PlayerAttackTargetKinds);
        }

        private static FpgCompiledSkillEvent Event(
            int eventId,
            int tick,
            int actionToken,
            int sortOrder = 0)
        {
            return new FpgCompiledSkillEvent(
                eventId,
                tick,
                FpgSkillActionKind.Attack,
                0,
                sortOrder: sortOrder,
                targetSource: FpgSkillTargetSource.CurrentAim);
        }

        private static PlayerRuntime CreatePlayer(
            int magazineCapacity,
            SecondaryTriggerMode secondaryTriggerMode =
                SecondaryTriggerMode.ChargeRelease,
            int minimumChargeTicks = 2,
            int primaryRecastTicks = 2,
            int secondaryRecastTicks = 2,
            int secondaryAmmoCost = 1)
        {
            WeaponDefinition weapon = new WeaponDefinition(
                900,
                magazineCapacity,
                1,
                new TickDuration(primaryRecastTicks),
                new DamageSpec(4, 1),
                secondaryAmmoCost,
                new TickDuration(minimumChargeTicks),
                new TickDuration(secondaryRecastTicks),
                new DamageSpec(8, 2),
                new TickDuration(2),
                1,
                secondaryTriggerMode,
                primaryPayloadCount: 1);
            return new PlayerRuntime(
                new CombatantState(
                    new RuntimeId(1L),
                    CombatantKind.Player,
                    100,
                    100,
                    0),
                new ExposureRuntime(),
                new WeaponRuntime(weapon));
        }

        private static void CommitPrimaryEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            CommitSkillEvent(
                skillEvent,
                WeaponReleaseKind.Primary,
                player,
                ids,
                release,
                tick);
        }

        private static void CommitSecondaryEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            CommitSkillEvent(
                skillEvent,
                WeaponReleaseKind.Secondary,
                player,
                ids,
                release,
                tick);
        }

        private static void CommitSkillEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            WeaponReleaseKind releaseKind,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            PrepareSkillEvent(
                skillEvent,
                releaseKind,
                player,
                ids,
                release,
                tick);
            Assert.That(player.Weapon.CommitPreparedSkillRelease(
                release,
                ids).IsSuccess, Is.True);
        }

        private static void PreparePrimaryEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            PrepareSkillEvent(
                skillEvent,
                WeaponReleaseKind.Primary,
                player,
                ids,
                release,
                tick);
        }

        private static void PrepareSkillEvent(
            FpgPlayerSkillExecutionEvent skillEvent,
            WeaponReleaseKind releaseKind,
            PlayerRuntime player,
            SessionIdAllocator ids,
            WeaponReleaseBuffer release,
            long tick)
        {
            FpgCompiledPlayerSkillAction payload = skillEvent.Action;
            WeaponSkillReleaseSpec spec = new WeaponSkillReleaseSpec(
                releaseKind,
                payload.Damage,
                payload.QueryPolicy,
                payload.QueryMode,
                payload.PayloadCount,
                payload.MaxImpactCount,
                payload.AmmoCost,
                payload.AdditionalPenetrationCount,
                payload.AreaCombatantLimit,
                payload.AreaProjectileLimit,
                payload.AllowedTargetKinds);
            Assert.That(player.Weapon.PrepareSkillRelease(
                new TickIndex(tick),
                player.RuntimeId,
                ids,
                123UL,
                spec,
                release).IsSuccess, Is.True);
        }

        private static PlayerInputFrame SecondaryEdge(
            long tick,
            long sequence,
            InputEdgeType type,
            bool held = false)
        {
            InputEdgeCommand[] edges =
            {
                new InputEdgeCommand(new InputSequence(sequence), type)
            };
            return new PlayerInputFrame(
                new TickIndex(tick),
                true,
                false,
                edges,
                edges.Length,
                secondaryHeld: held);
        }

        private static void AssertCancelContinues(
            FpgPlayerSkillExecutionController controller,
            SkillExecutionId expectedExecutionId)
        {
            Assert.That(controller.IsExecuting, Is.True);
            Assert.That(controller.ActiveSequenceKind,
                Is.EqualTo(FpgSkillSequenceKind.Cancel));
            Assert.That(controller.SequenceFrameCount, Is.EqualTo(1));
            Assert.That(controller.GetSequenceFrame(0).ExecutionId,
                Is.EqualTo(expectedExecutionId));
            Assert.That(controller.GetSequenceFrame(0).State,
                Is.EqualTo(FpgSkillExecutionState.Running));
        }
    }
}
