using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    [Category("AttackTiming")]
    public sealed class FpgAttackTimingHashAndWeaponSnapshotTests
    {
        private static readonly RuntimeId OwnerId = new RuntimeId(91L);

        [Test]
        public void TimingContractHashChangesForModeAndAuthoredTimingFields()
        {
            FpgCompiledSkillTimingDefinition fixedTiming =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.FixedCooldown,
                    1d,
                    40,
                    0);
            FpgCompiledSkillTimingDefinition characterTiming =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.CharacterAttackSpeed,
                    1d,
                    40,
                    0);
            FpgCompiledSkillTimingDefinition coefficientChanged =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.CharacterAttackSpeed,
                    0.5d,
                    40,
                    0);
            FpgCompiledSkillTimingDefinition markerChanged =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.CharacterAttackSpeed,
                    1d,
                    39,
                    0);
            FpgCompiledSkillTimingDefinition attackFrameChanged =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.CharacterAttackSpeed,
                    1d,
                    40,
                    1);

            Assert.That(
                characterTiming.TimingContractHash,
                Is.Not.EqualTo(fixedTiming.TimingContractHash));
            Assert.That(
                coefficientChanged.TimingContractHash,
                Is.Not.EqualTo(characterTiming.TimingContractHash));
            Assert.That(
                markerChanged.TimingContractHash,
                Is.Not.EqualTo(characterTiming.TimingContractHash));
            Assert.That(
                attackFrameChanged.TimingContractHash,
                Is.Not.EqualTo(characterTiming.TimingContractHash));
        }

        [Test]
        public void TimingSnapshotHashChangesForBonusAndResolvedTicks()
        {
            FpgCompiledSkillSequence sequence = CreateAttackSequence();
            FpgCompiledSkillTimingDefinition timing =
                new FpgCompiledSkillTimingDefinition(
                    FpgAttackTimingMode.CharacterAttackSpeed,
                    1d,
                    40,
                    0);
            FpgAttackSpeedProfile profile = new FpgAttackSpeedProfile(
                60d / 41d,
                1d,
                2.5d);

            FpgResolvedSkillSchedule baseline = Resolve(
                sequence,
                timing,
                profile,
                0d,
                0L);
            FpgResolvedSkillSchedule bonusApplied = Resolve(
                sequence,
                timing,
                profile,
                1d,
                0L);
            FpgResolvedSkillSchedule laterStart = Resolve(
                sequence,
                timing,
                profile,
                0d,
                1L);

            Assert.That(baseline.Timing.IntervalTicks, Is.EqualTo(41));
            Assert.That(bonusApplied.Timing.IntervalTicks, Is.EqualTo(25));
            Assert.That(
                bonusApplied.Timing.BonusAttackSpeed,
                Is.EqualTo(1d));
            Assert.That(
                laterStart.Timing.AttackFrameTick,
                Is.EqualTo(new TickIndex(1L)));
            Assert.That(
                bonusApplied.Timing.TimingSnapshotHash,
                Is.Not.EqualTo(baseline.Timing.TimingSnapshotHash));
            Assert.That(
                laterStart.Timing.TimingSnapshotHash,
                Is.Not.EqualTo(baseline.Timing.TimingSnapshotHash));
        }

        [Test]
        public void ResolvedReadyLockSurvivesSnapshotRestoreAndPreCommitRollback()
        {
            WeaponDefinition definition = CreateWeaponDefinition();
            WeaponRuntime weapon = new WeaponRuntime(definition);
            ExposureRuntime exposure = new ExposureRuntime();
            SessionIdAllocator ids = new SessionIdAllocator();
            WeaponReleaseBuffer release = new WeaponReleaseBuffer();
            TickIndex releaseTick = new TickIndex(100L);
            TickIndex resolvedReadyTick = new TickIndex(141L);

            Assert.That(
                weapon.AdvanceSkillFrame(releaseTick).IsSuccess,
                Is.True);
            Assert.That(
                weapon.TryBeginSkillAction(
                    WeaponSkillActionKind.Primary,
                    releaseTick,
                    new TickIndex(200L),
                    1,
                    exposure).IsSuccess,
                Is.True);
            WeaponRuntimeSnapshot beforeCommit = weapon.CaptureRoomSnapshot();

            Assert.That(
                weapon.PrepareSkillRelease(
                    releaseTick,
                    OwnerId,
                    ids,
                    123UL,
                    CreatePrimaryReleaseSpec(),
                    release).IsSuccess,
                Is.True);
            Assert.That(
                weapon.CommitPreparedSkillRelease(
                    release,
                    ids,
                    resolvedReadyTick).IsSuccess,
                Is.True);
            WeaponRuntimeSnapshot committed = weapon.CaptureRoomSnapshot();

            Assert.That(
                weapon.PrimaryRecastLockedUntilTick,
                Is.EqualTo(resolvedReadyTick));
            Assert.That(weapon.Magazine.Ammo, Is.EqualTo(7));

            Assert.That(weapon.RestoreRoomSnapshot(beforeCommit).IsSuccess, Is.True);
            Assert.That(
                weapon.PrimaryRecastLockedUntilTick,
                Is.EqualTo(TickIndex.Invalid));
            Assert.That(weapon.Magazine.Ammo, Is.EqualTo(8));

            WeaponRuntime restored = new WeaponRuntime(definition);
            Assert.That(restored.RestoreRoomSnapshot(committed).IsSuccess, Is.True);
            Assert.That(
                restored.PrimaryRecastLockedUntilTick,
                Is.EqualTo(resolvedReadyTick));
            Assert.That(restored.State, Is.EqualTo(WeaponState.PrimaryRecovery));
            Assert.That(restored.Magazine.Ammo, Is.EqualTo(7));
        }

        private static FpgResolvedSkillSchedule Resolve(
            FpgCompiledSkillSequence sequence,
            FpgCompiledSkillTimingDefinition timing,
            FpgAttackSpeedProfile profile,
            double bonusAttackSpeed,
            long startTick)
        {
            Assert.That(
                FpgAttackTimingResolver.TryResolve(
                    sequence,
                    timing,
                    12,
                    profile,
                    bonusAttackSpeed,
                    new TickIndex(startTick),
                    out FpgResolvedSkillSchedule schedule,
                    out string error),
                Is.True,
                error);
            return schedule;
        }

        private static FpgCompiledSkillSequence CreateAttackSequence()
        {
            return new FpgCompiledSkillSequence(
                FpgSkillSequenceKind.Execute,
                40,
                9001,
                false,
                new[]
                {
                    new FpgCompiledSkillEvent(
                        1,
                        0,
                        FpgSkillActionKind.Attack,
                        0,
                        0,
                        targetSource: FpgSkillTargetSource.CurrentAim)
                });
        }

        private static WeaponDefinition CreateWeaponDefinition()
        {
            return new WeaponDefinition(
                91,
                8,
                1,
                new TickDuration(2),
                new DamageSpec(10, 2),
                4,
                TickDuration.Zero,
                new TickDuration(3),
                new DamageSpec(20, 5),
                new TickDuration(2),
                4,
                SecondaryTriggerMode.ChargeRelease);
        }

        private static WeaponSkillReleaseSpec CreatePrimaryReleaseSpec()
        {
            return new WeaponSkillReleaseSpec(
                WeaponReleaseKind.Primary,
                new DamageSpec(10, 2),
                QueryPolicy.PelletRays,
                AttackQueryMode.FirstSurfacePenetration,
                WeaponDefinition.PrimaryPelletCount,
                WeaponDefinition.PrimaryPelletCount,
                1,
                0,
                0,
                0,
                WeaponDefinition.PlayerAttackTargetKinds);
        }
    }
}
