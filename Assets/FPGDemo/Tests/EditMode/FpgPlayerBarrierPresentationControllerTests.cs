
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgPlayerBarrierPresentationControllerTests
    {
        [TestCase(FpgEncounterPhase.Combat, false, PlayerExposureState.Withdrawn, 1, true)]
        [TestCase(FpgEncounterPhase.Combat, false, PlayerExposureState.Exposed, 60, false)]
        [TestCase(FpgEncounterPhase.Combat, false, PlayerExposureState.Withdrawn, 0, false)]
        [TestCase(FpgEncounterPhase.Combat, true, PlayerExposureState.Withdrawn, 60, false)]
        [TestCase(FpgEncounterPhase.Cleared, false, PlayerExposureState.Withdrawn, 60, false)]
        public void BarrierVisibilityUsesCommittedFormalSnapshot(
            FpgEncounterPhase phase,
            bool paused,
            PlayerExposureState exposureState,
            int barrier,
            bool expected)
        {
            FpgFormalPlayerPresentationSnapshot snapshot =
                new FpgFormalPlayerPresentationSnapshot(
                    new TickIndex(1L),
                    new RuntimeId(1L),
                    phase,
                    paused,
                    100,
                    100,
                    barrier,
                    100,
                    6,
                    6,
                    exposureState,
                    WeaponState.Ready);

            Assert.That(
                FpgPlayerBarrierPresentationController.ShouldShowBarrier(snapshot),
                Is.EqualTo(expected));
        }
    }
}
