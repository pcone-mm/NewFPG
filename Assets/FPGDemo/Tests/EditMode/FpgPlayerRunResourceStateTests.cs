using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgPlayerRunResourceStateTests
    {
        [Test]
        public void CaptureAndRestore_CarriesCurrentResourcesAndDropsTransientState()
        {
            PlayerRuntime source = CreatePlayer(1);
            Assert.That(
                source.Combatant.RestoreResources(
                    new CombatantResourceSnapshot(
                        source.RuntimeId,
                        73,
                        0,
                        0)).IsSuccess,
                Is.True);
            Assert.That(source.Weapon.Magazine.RestoreAmmo(3).IsSuccess, Is.True);

            DomainResult captured = FpgPlayerRunResourceTransfer.TryCapture(
                source,
                "fei",
                "fei-weapon",
                out FpgPlayerRunResourceState state);

            Assert.That(captured.IsSuccess, Is.True);
            Assert.That(state.Life, Is.EqualTo(73));
            Assert.That(state.Barrier, Is.Zero);
            Assert.That(state.Ammo, Is.EqualTo(3));

            PlayerRuntime target = CreatePlayer(2);
            DomainResult restored =
                FpgPlayerRunResourceTransfer.TryRestoreRoomEntry(
                    target,
                    "fei",
                    "fei-weapon",
                    state);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(target.Combatant.Life, Is.EqualTo(73));
            Assert.That(target.Combatant.Barrier, Is.Zero);
            Assert.That(target.Weapon.Magazine.Ammo, Is.EqualTo(3));
            Assert.That(target.Weapon.State, Is.EqualTo(WeaponState.Ready));
            Assert.That(target.Weapon.LastProcessedTick, Is.EqualTo(TickIndex.Invalid));
            Assert.That(target.Weapon.LastInputSequence.Value, Is.Zero);
            Assert.That(target.Exposure.State, Is.EqualTo(PlayerExposureState.Exposed));
        }

        [Test]
        public void CaptureAndRestore_DropsLegacyBarrierAcrossRooms()
        {
            PlayerRuntime source = CreatePlayer(1);
            Assert.That(
                source.Combatant.RestoreResources(
                    new CombatantResourceSnapshot(
                        source.RuntimeId,
                        80,
                        25,
                        0)).IsSuccess,
                Is.True);

            DomainResult captured = FpgPlayerRunResourceTransfer.TryCapture(
                source,
                "fei",
                "fei-weapon",
                out FpgPlayerRunResourceState state);

            Assert.That(captured.IsSuccess, Is.True);
            Assert.That(state.Life, Is.EqualTo(80));
            Assert.That(state.Barrier, Is.Zero);

            PlayerRuntime target = CreatePlayer(2);
            DomainResult restored =
                FpgPlayerRunResourceTransfer.TryRestoreRoomEntry(
                    target,
                    "fei",
                    "fei-weapon",
                    state);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(target.Combatant.Life, Is.EqualTo(80));
            Assert.That(target.Combatant.Barrier, Is.Zero);
        }

        [Test]
        public void Restore_RejectsCompatibilityMismatchWithoutChangingTarget()
        {
            PlayerRuntime target = CreatePlayer(2);
            FpgPlayerRunResourceState state = new FpgPlayerRunResourceState(
                "other-character",
                "fei-weapon",
                50,
                25,
                2);

            DomainResult restored =
                FpgPlayerRunResourceTransfer.TryRestoreRoomEntry(
                    target,
                    "fei",
                    "fei-weapon",
                    state);

            Assert.That(restored.IsSuccess, Is.False);
            Assert.That(restored.RejectReason, Is.EqualTo(RejectReason.InvalidDefinition));
            Assert.That(target.Combatant.Life, Is.EqualTo(100));
            Assert.That(target.Combatant.Barrier, Is.EqualTo(100));
            Assert.That(target.Weapon.Magazine.Ammo, Is.EqualTo(8));
        }

        private static PlayerRuntime CreatePlayer(long runtimeId)
        {
            WeaponDefinition weapon = new WeaponDefinition(
                101,
                8,
                1,
                new TickDuration(3),
                new DamageSpec(10, 5, 15000, 20000),
                2,
                new TickDuration(5),
                new DamageSpec(24, 12, 15000, 20000),
                new TickDuration(12),
                8);
            return new PlayerRuntime(
                new CombatantState(
                    new RuntimeId(runtimeId),
                    CombatantKind.Player,
                    100,
                    100,
                    0),
                new ExposureRuntime(PlayerExposureState.Exposed),
                new WeaponRuntime(weapon));
        }
    }
}
