using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class WeaponRuntimeTests
    {
        private static readonly RuntimeId OwnerId = new RuntimeId(1L);

        [Test]
        public void PrimaryReleaseCommitsExactlyEightPelletsAmmoAndIds()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition());
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            DomainResult result = weapon.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);

            AssertAll(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.Kind, Is.EqualTo(WeaponReleaseKind.Primary));
                Assert.That(output.PelletCount, Is.EqualTo(WeaponDefinition.PrimaryPelletCount));
                Assert.That(output.Attack.PayloadCount, Is.EqualTo(WeaponDefinition.PrimaryPelletCount));
                Assert.That(output.Attack.MaxImpactCount, Is.EqualTo(WeaponDefinition.PrimaryPelletCount));
                Assert.That(output.Attack.QueryPolicy, Is.EqualTo(QueryPolicy.PelletRays));
                Assert.That(
                    output.Attack.QueryMode,
                    Is.EqualTo(AttackQueryMode.FirstSurfacePenetration));
                Assert.That(output.Attack.AdditionalPenetrationCount, Is.Zero);
                Assert.That(output.Attack.AreaCombatantLimit, Is.Zero);
                Assert.That(output.Attack.AreaProjectileLimit, Is.Zero);
                Assert.That(
                    output.Attack.AllowedTargetKinds,
                    Is.EqualTo(WeaponDefinition.PlayerAttackTargetKinds));
                Assert.That(output.Attack.IsQueryConfigurationValid, Is.True);
                Assert.That(output.Attack.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(output.Attack.ShotId, Is.EqualTo(new ShotId(1L)));
                Assert.That(output.Attack.ReleaseTick, Is.EqualTo(new TickIndex(0L)));
                Assert.That(output.Attack.RngVersion, Is.EqualTo(DeterministicRandomV1.Version));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(7));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.PrimaryRecovery));
            });

            for (int index = 0; index < output.PelletCount; index++)
            {
                PelletSample pellet = output.Pellets[index];
                AssertAll(() =>
                {
                    Assert.That(pellet.ShotId, Is.EqualTo(output.Attack.ShotId));
                    Assert.That(pellet.PelletIndex, Is.EqualTo(index));
                    Assert.That(pellet.SpreadU24, Is.InRange(0, 0xFFFFFF));
                    Assert.That(pellet.SpreadV24, Is.InRange(0, 0xFFFFFF));
                });
            }

            AttackShotReservation next = ids.ReserveAttackAndShot();
            AssertAll(() =>
            {
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(2L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(2L)));
            });
        }

        [Test]
        public void PrimarySnapshotTreatsOneAsOneSurfaceAfterTheFirstPerPellet()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                primaryAdditionalPenetrationCount: 1));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessPrimary(weapon, exposure, ids, output, 0L);

            AssertAll(() =>
            {
                Assert.That(
                    output.Attack.QueryMode,
                    Is.EqualTo(AttackQueryMode.FirstSurfacePenetration));
                Assert.That(output.Attack.AdditionalPenetrationCount, Is.EqualTo(1));
                Assert.That(
                    output.Attack.MaxImpactCount,
                    Is.EqualTo(WeaponDefinition.PrimaryPelletCount * 2));
                Assert.That(output.Attack.IsQueryConfigurationValid, Is.True);
            });
        }

        [Test]
        public void RejectedPrimaryDoesNotPartiallyCommitAmmoOrIds()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition());
            ExposureRuntime withdrawn = new ExposureRuntime(PlayerExposureState.Withdrawn);
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            DomainResult result = weapon.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), false, true),
                withdrawn,
                OwnerId,
                ids,
                123UL,
                output);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.NotExposed));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(weapon.Magazine.Capacity));
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(1L)));
            });
        }

        [Test]
        public void SecondaryReleaseDeductsAmmoAtomicallyAndFailureDoesNotConsumeNextIds()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(magazineCapacity: 4));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessEdge(weapon, exposure, ids, output, 0L, 1L, InputEdgeType.SecondaryPressed);
            AssertAll(() =>
            {
                Assert.That(weapon.State, Is.EqualTo(WeaponState.AltCharging));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(4));
                Assert.That(output.HasRelease, Is.False);
            });

            ProcessEdge(weapon, exposure, ids, output, 1L, 2L, InputEdgeType.SecondaryReleased);
            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.Kind, Is.EqualTo(WeaponReleaseKind.Secondary));
                Assert.That(output.Attack.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(output.Attack.ShotId, Is.EqualTo(new ShotId(1L)));
                Assert.That(output.Attack.AmmoCost, Is.EqualTo(4));
                Assert.That(output.Attack.QueryPolicy, Is.EqualTo(QueryPolicy.DirectThenArea));
                Assert.That(
                    output.Attack.QueryMode,
                    Is.EqualTo(AttackQueryMode.AreaAtFirstSurface));
                Assert.That(output.Attack.AdditionalPenetrationCount, Is.Zero);
                Assert.That(output.Attack.AreaCombatantLimit, Is.EqualTo(4));
                Assert.That(
                    output.Attack.AreaProjectileLimit,
                    Is.EqualTo(WeaponDefinition.DefaultSecondaryAreaProjectileLimit));
                Assert.That(output.Attack.MaxImpactCount, Is.EqualTo(8));
                Assert.That(
                    output.Attack.AllowedTargetKinds,
                    Is.EqualTo(WeaponDefinition.PlayerAttackTargetKinds));
                Assert.That(output.Attack.IsQueryConfigurationValid, Is.True);
                Assert.That(weapon.Magazine.Ammo, Is.Zero);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.AltRecovery));
            });

            ProcessEmpty(weapon, exposure, ids, output, 2L);
            ProcessEmpty(weapon, exposure, ids, output, 3L);
            ProcessEdge(weapon, exposure, ids, output, 4L, 3L, InputEdgeType.SecondaryPressed);
            ProcessEdge(weapon, exposure, ids, output, 5L, 4L, InputEdgeType.SecondaryReleased);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.NotEnoughAmmo));
                Assert.That(weapon.Magazine.Ammo, Is.Zero);
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(2L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(2L)));
            });
        }

        [Test]
        public void SecondarySnapshotFreezesIndependentEnemyAndProjectileLimits()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                secondaryMaxImpactCount: 3,
                secondaryAreaProjectileLimit: 2));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessEdge(weapon, exposure, ids, output, 0L, 1L, InputEdgeType.SecondaryPressed);
            ProcessEdge(weapon, exposure, ids, output, 1L, 2L, InputEdgeType.SecondaryReleased);

            AssertAll(() =>
            {
                Assert.That(output.Attack.QueryMode, Is.Not.EqualTo(AttackQueryMode.Legacy));
                Assert.That(
                    output.Attack.QueryMode,
                    Is.EqualTo(AttackQueryMode.AreaAtFirstSurface));
                Assert.That(output.Attack.AreaCombatantLimit, Is.EqualTo(3));
                Assert.That(output.Attack.AreaProjectileLimit, Is.EqualTo(2));
                Assert.That(output.Attack.MaxImpactCount, Is.EqualTo(5));
                Assert.That(
                    output.Attack.AllowedTargetKinds,
                    Is.EqualTo(
                        AttackTargetKinds.Combatant | AttackTargetKinds.Projectile));
                Assert.That(output.Attack.IsQueryConfigurationValid, Is.True);
            });
        }

        [Test]
        public void SecondaryMinimumChargeCancelsEarlyReleaseAndCommitsAtTwentyNineTicks()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                secondaryAmmoCost: 2,
                secondaryMinimumCharge: 29));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessEdge(weapon, exposure, ids, output, 0L, 1L, InputEdgeType.SecondaryPressed);
            for (long tick = 1L; tick < 28L; tick++)
            {
                ProcessEmpty(weapon, exposure, ids, output, tick);
            }

            ProcessEdge(weapon, exposure, ids, output, 28L, 2L, InputEdgeType.SecondaryReleased);
            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(weapon.SecondaryChargeStartedTick, Is.EqualTo(TickIndex.Invalid));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(8));
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.None));
            });

            ProcessEdge(weapon, exposure, ids, output, 29L, 3L, InputEdgeType.SecondaryPressed);
            for (long tick = 30L; tick < 58L; tick++)
            {
                ProcessEmpty(weapon, exposure, ids, output, tick);
            }

            ProcessEdge(weapon, exposure, ids, output, 58L, 4L, InputEdgeType.SecondaryReleased);
            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.Kind, Is.EqualTo(WeaponReleaseKind.Secondary));
                Assert.That(output.Attack.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(output.Attack.ShotId, Is.EqualTo(new ShotId(1L)));
                Assert.That(output.Attack.AmmoCost, Is.EqualTo(2));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(6));
                Assert.That(weapon.SecondaryChargeStartedTick, Is.EqualTo(TickIndex.Invalid));
            });
        }

        [Test]
        public void SecondaryChargeWithInsufficientAmmoStaysReadyAndDoesNotReserveIds()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                magazineCapacity: 2,
                secondaryAmmoCost: 2));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();
            Assert.That(weapon.Magazine.RestoreAmmo(1).IsSuccess, Is.True);

            ProcessEdge(
                weapon,
                exposure,
                ids,
                output,
                0L,
                1L,
                InputEdgeType.SecondaryPressed);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(weapon.SecondaryChargeStartedTick,
                    Is.EqualTo(TickIndex.Invalid));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(1));
                Assert.That(weapon.LastRejectReason,
                    Is.EqualTo(RejectReason.NotEnoughAmmo));
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(1L)));
            });
        }

        [Test]
        public void CancelForWithdrawnClearsSecondaryChargeWithoutSpendingAmmoOrIds()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(secondaryMinimumCharge: 29));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessEdge(weapon, exposure, ids, output, 0L, 1L, InputEdgeType.SecondaryPressed);
            weapon.CancelForWithdrawn();
            ProcessEdge(weapon, exposure, ids, output, 1L, 2L, InputEdgeType.SecondaryReleased);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(weapon.SecondaryChargeStartedTick, Is.EqualTo(TickIndex.Invalid));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(weapon.Magazine.Capacity));
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(1L)));
            });
        }

        [Test]
        public void ImmediateSecondaryFiresOnPressAndRepeatsAtRecoveryBoundary()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                secondaryAmmoCost: 2,
                secondaryMinimumCharge: 29,
                secondaryTriggerMode: SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessSecondaryInput(
                weapon,
                exposure,
                ids,
                output,
                tick: 0L,
                held: true,
                sequence: 1L,
                edgeType: InputEdgeType.SecondaryPressed);
            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.Kind, Is.EqualTo(WeaponReleaseKind.Secondary));
                Assert.That(output.Attack.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(output.Attack.ReleaseTick, Is.EqualTo(new TickIndex(0L)));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(6));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.AltRecovery));
                Assert.That(weapon.StateUntilTick, Is.EqualTo(new TickIndex(3L)));
            });

            ProcessSecondaryInput(weapon, exposure, ids, output, 1L, held: true);
            Assert.That(output.HasRelease, Is.False);
            ProcessSecondaryInput(weapon, exposure, ids, output, 2L, held: true);
            Assert.That(output.HasRelease, Is.False);
            ProcessSecondaryInput(weapon, exposure, ids, output, 3L, held: true);
            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.Attack.AttackId, Is.EqualTo(new AttackId(2L)));
                Assert.That(output.Attack.ReleaseTick, Is.EqualTo(new TickIndex(3L)));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(4));
                Assert.That(weapon.StateUntilTick, Is.EqualTo(new TickIndex(6L)));
            });

            ProcessSecondaryInput(
                weapon,
                exposure,
                ids,
                output,
                tick: 4L,
                held: false,
                sequence: 2L,
                edgeType: InputEdgeType.SecondaryReleased);
            Assert.That(output.HasRelease, Is.False);
            Assert.That(weapon.Magazine.Ammo, Is.EqualTo(4));
        }

        [Test]
        public void ImmediateSecondaryWithoutAmmoStaysReadyAndNeverAutoReloads()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                magazineCapacity: 2,
                secondaryAmmoCost: 2,
                secondaryTriggerMode: SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessSecondaryInput(
                weapon,
                exposure,
                ids,
                output,
                tick: 0L,
                held: true,
                sequence: 1L,
                edgeType: InputEdgeType.SecondaryPressed);
            ProcessSecondaryInput(weapon, exposure, ids, output, 1L, held: true);
            ProcessSecondaryInput(weapon, exposure, ids, output, 2L, held: true);
            ProcessSecondaryInput(weapon, exposure, ids, output, 3L, held: true);
            AttackShotReservation next = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.NotEnoughAmmo));
                Assert.That(weapon.Magazine.Ammo, Is.Zero);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(weapon.StateUntilTick, Is.EqualTo(TickIndex.Invalid));
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(2L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(2L)));
            });

            ProcessSecondaryInput(
                weapon,
                exposure,
                ids,
                output,
                tick: 4L,
                held: false,
                sequence: 2L,
                edgeType: InputEdgeType.SecondaryReleased);
            Assert.That(output.HasRelease, Is.False);
            Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
        }

        [Test]
        public void PrimaryAndReloadHonorConfiguredTickDurations()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                magazineCapacity: 2,
                secondaryAmmoCost: 2,
                primaryInterval: 39,
                reloadDuration: 84));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessPrimary(weapon, exposure, ids, output, 0L);
            for (long tick = 1L; tick < 38L; tick++)
            {
                ProcessEmpty(weapon, exposure, ids, output, tick);
            }

            DomainResult beforeInterval = weapon.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(38L), true, true),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            AssertAll(() =>
            {
                Assert.That(beforeInterval.IsSuccess, Is.True);
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.Cooldown));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(1));
            });

            ProcessPrimary(weapon, exposure, ids, output, 39L);
            for (long tick = 40L; tick < 78L; tick++)
            {
                ProcessEmpty(weapon, exposure, ids, output, tick);
            }

            Assert.That(
                exposure.ApplyCombatPosture(
                    false,
                    new TickIndex(78L),
                    false,
                    out bool withdrewForReload).IsSuccess,
                Is.True);
            Assert.That(withdrewForReload, Is.True);
            ProcessEdge(weapon, exposure, ids, output, 78L, 1L, InputEdgeType.ReloadPressed);
            for (long tick = 79L; tick < 162L; tick++)
            {
                ProcessEmpty(weapon, exposure, ids, output, tick);
            }

            AssertAll(() =>
            {
                Assert.That(weapon.Magazine.Ammo, Is.Zero);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Reloading));
            });

            ProcessEmpty(weapon, exposure, ids, output, 162L);
            AssertAll(() =>
            {
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(2));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
            });
        }

        [Test]
        public void ReloadCompletionIsAppliedBeforeSameTickFireInput()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(
                magazineCapacity: 2,
                secondaryAmmoCost: 2,
                primaryInterval: 1,
                reloadDuration: 2));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessPrimary(weapon, exposure, ids, output, 0L);
            ProcessPrimary(weapon, exposure, ids, output, 1L);
            Assert.That(
                exposure.ApplyCombatPosture(
                    false,
                    new TickIndex(2L),
                    false,
                    out bool withdrewForReload).IsSuccess,
                Is.True);
            Assert.That(withdrewForReload, Is.True);
            ProcessEdge(weapon, exposure, ids, output, 2L, 1L, InputEdgeType.ReloadPressed);
            ProcessEmpty(weapon, exposure, ids, output, 3L);
            Assert.That(
                exposure.ApplyCombatPosture(
                    true,
                    new TickIndex(4L),
                    false,
                    out bool exposedAtCompletion).IsSuccess,
                Is.True);
            Assert.That(exposedAtCompletion, Is.True);

            DomainResult atCompletion = weapon.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(4L), true, true),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);

            AssertAll(() =>
            {
                Assert.That(atCompletion.IsSuccess, Is.True);
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.Kind, Is.EqualTo(WeaponReleaseKind.Primary));
                Assert.That(output.Attack.ReleaseTick, Is.EqualTo(new TickIndex(4L)));
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(1));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.PrimaryRecovery));
            });
        }

        [Test]
        public void InputSequenceStartsAtOneAndOlderSequencesAreIgnored()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition());
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessEdge(weapon, exposure, ids, output, 0L, 0L, InputEdgeType.SecondaryPressed);
            AssertAll(() =>
            {
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(weapon.LastInputSequence, Is.EqualTo(new InputSequence(0L)));
            });

            ProcessEdge(weapon, exposure, ids, output, 1L, 1L, InputEdgeType.SecondaryPressed);
            AssertAll(() =>
            {
                Assert.That(weapon.State, Is.EqualTo(WeaponState.AltCharging));
                Assert.That(weapon.LastInputSequence, Is.EqualTo(new InputSequence(1L)));
            });

            ProcessEdge(weapon, exposure, ids, output, 2L, 1L, InputEdgeType.SecondaryReleased);
            AssertAll(() =>
            {
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.State, Is.EqualTo(WeaponState.AltCharging));
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.DuplicateSequence));
            });
        }

        [Test]
        public void ReloadWithdrawsExposedPlayerEvenWhenBarrierIsDepleted()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition(primaryInterval: 1));
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            ProcessPrimary(weapon, exposure, ids, output, 0L);
            DomainResult lockedPosture = exposure.ApplyCombatPosture(
                false,
                new TickIndex(1L),
                true,
                out bool postureChanged);

            AssertAll(() =>
            {
                Assert.That(lockedPosture.IsSuccess, Is.False);
                Assert.That(lockedPosture.RejectReason, Is.EqualTo(RejectReason.BarrierLocked));
                Assert.That(postureChanged, Is.False);
                Assert.That(exposure.State, Is.EqualTo(PlayerExposureState.Exposed));
            });

            ProcessEdge(
                weapon,
                exposure,
                ids,
                output,
                1L,
                1L,
                InputEdgeType.ReloadPressed);

            AssertAll(() =>
            {
                Assert.That(exposure.State, Is.EqualTo(PlayerExposureState.Withdrawn));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Reloading));
                Assert.That(weapon.LastRejectReason, Is.EqualTo(RejectReason.None));
            });
        }

        [Test]
        public void PreparedPrimaryCommitsAmmoRecoveryAndIdsOnlyOnExplicitCommit()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition());
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();

            DomainResult prepared = weapon.PrepareFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            AttackShotReservation beforeCommit = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(prepared.IsSuccess, Is.True);
                Assert.That(output.HasRelease, Is.True);
                Assert.That(output.IsCommitted, Is.False);
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(8));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(beforeCommit.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(beforeCommit.ShotId, Is.EqualTo(new ShotId(1L)));
            });

            DomainResult committed = weapon.CommitPreparedRelease(output, ids);
            DomainResult duplicateCommit = weapon.CommitPreparedRelease(output, ids);
            AttackShotReservation afterCommit = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(committed.IsSuccess, Is.True);
                Assert.That(duplicateCommit.RejectReason, Is.EqualTo(RejectReason.InvalidState));
                Assert.That(output.IsCommitted, Is.True);
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(7));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.PrimaryRecovery));
                Assert.That(afterCommit.AttackId, Is.EqualTo(new AttackId(2L)));
                Assert.That(afterCommit.ShotId, Is.EqualTo(new ShotId(2L)));
            });
        }

        [Test]
        public void AbandonedPreparedPrimaryRestoresQueryFailureWithoutConsumingIds()
        {
            WeaponRuntime weapon = new WeaponRuntime(CreateDefinition());
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer output = new WeaponReleaseBuffer();
            WeaponRuntimeSnapshot beforeQuery = weapon.CaptureRoomSnapshot();

            DomainResult prepared = weapon.PrepareFrame(
                PlayerInputFrame.Empty(new TickIndex(0L), true, true),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            DomainResult restored = weapon.RestoreRoomSnapshot(beforeQuery);
            output.Reset();
            AttackShotReservation next = ids.ReserveAttackAndShot();

            AssertAll(() =>
            {
                Assert.That(prepared.IsSuccess, Is.True);
                Assert.That(restored.IsSuccess, Is.True);
                Assert.That(output.HasRelease, Is.False);
                Assert.That(weapon.LastProcessedTick.IsValid, Is.False);
                Assert.That(weapon.Magazine.Ammo, Is.EqualTo(8));
                Assert.That(weapon.State, Is.EqualTo(WeaponState.Ready));
                Assert.That(next.AttackId, Is.EqualTo(new AttackId(1L)));
                Assert.That(next.ShotId, Is.EqualTo(new ShotId(1L)));
            });
        }

        private static WeaponDefinition CreateDefinition(
            int magazineCapacity = 8,
            int secondaryAmmoCost = 4,
            int secondaryMinimumCharge = 0,
            int primaryInterval = 2,
            int reloadDuration = 2,
            SecondaryTriggerMode secondaryTriggerMode = SecondaryTriggerMode.ChargeRelease,
            int secondaryMaxImpactCount = 4,
            int primaryAdditionalPenetrationCount = 0,
            int secondaryAreaProjectileLimit =
                WeaponDefinition.DefaultSecondaryAreaProjectileLimit)
        {
            return new WeaponDefinition(
                101,
                magazineCapacity,
                1,
                new TickDuration(primaryInterval),
                new DamageSpec(10, 2),
                secondaryAmmoCost,
                new TickDuration(secondaryMinimumCharge),
                new TickDuration(3),
                new DamageSpec(20, 5),
                new TickDuration(reloadDuration),
                secondaryMaxImpactCount,
                secondaryTriggerMode,
                primaryQueryMode: AttackQueryMode.FirstSurfacePenetration,
                primaryAdditionalPenetrationCount: primaryAdditionalPenetrationCount,
                secondaryQueryMode: AttackQueryMode.AreaAtFirstSurface,
                secondaryAreaProjectileLimit: secondaryAreaProjectileLimit,
                primaryAllowedTargetKinds: WeaponDefinition.PlayerAttackTargetKinds,
                secondaryAllowedTargetKinds: WeaponDefinition.PlayerAttackTargetKinds);
        }

        private static void ProcessPrimary(
            WeaponRuntime weapon,
            ExposureRuntime exposure,
            SessionIdAllocator ids,
            WeaponReleaseBuffer output,
            long tick)
        {
            DomainResult result = weapon.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(tick), true, true),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(output.HasRelease, Is.True);
        }

        private static void ProcessEmpty(
            WeaponRuntime weapon,
            ExposureRuntime exposure,
            SessionIdAllocator ids,
            WeaponReleaseBuffer output,
            long tick)
        {
            DomainResult result = weapon.ProcessFrame(
                PlayerInputFrame.Empty(new TickIndex(tick), true, false),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(output.HasRelease, Is.False);
        }

        private static void ProcessEdge(
            WeaponRuntime weapon,
            ExposureRuntime exposure,
            SessionIdAllocator ids,
            WeaponReleaseBuffer output,
            long tick,
            long sequence,
            InputEdgeType type)
        {
            InputEdgeCommand[] commands =
            {
                new InputEdgeCommand(new InputSequence(sequence), type)
            };
            DomainResult result = weapon.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(tick),
                    false,
                    false,
                    commands,
                    commands.Length),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            Assert.That(result.IsSuccess, Is.True);
        }

        private static void ProcessSecondaryInput(
            WeaponRuntime weapon,
            ExposureRuntime exposure,
            SessionIdAllocator ids,
            WeaponReleaseBuffer output,
            long tick,
            bool held,
            long sequence = 0L,
            InputEdgeType? edgeType = null)
        {
            InputEdgeCommand[] commands = edgeType.HasValue
                ? new[]
                {
                    new InputEdgeCommand(
                        new InputSequence(sequence),
                        edgeType.Value)
                }
                : null;
            DomainResult result = weapon.ProcessFrame(
                new PlayerInputFrame(
                    new TickIndex(tick),
                    aimHeld: true,
                    primaryHeld: false,
                    edgeCommands: commands,
                    edgeCommandCount: commands == null ? 0 : commands.Length,
                    secondaryHeld: held),
                exposure,
                OwnerId,
                ids,
                123UL,
                output);
            Assert.That(result.IsSuccess, Is.True);
        }

        private static void AssertAll(Action assertions)
        {
            assertions();
        }
    }
}
