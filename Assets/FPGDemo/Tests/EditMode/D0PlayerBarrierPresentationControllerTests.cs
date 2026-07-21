using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0PlayerBarrierPresentationControllerTests
    {
        [TestCase(BattleSessionState.Running, PlayerExposureState.Withdrawn, 1, true)]
        [TestCase(BattleSessionState.Running, PlayerExposureState.Exposed, 60, false)]
        [TestCase(BattleSessionState.Running, PlayerExposureState.Withdrawn, 0, false)]
        [TestCase(BattleSessionState.Paused, PlayerExposureState.Withdrawn, 60, false)]
        [TestCase(BattleSessionState.Completed, PlayerExposureState.Withdrawn, 60, false)]
        public void BarrierVisibilityUsesCommittedSessionPosture(
            BattleSessionState state,
            PlayerExposureState exposureState,
            int barrier,
            bool expected)
        {
            Assert.That(
                D0PlayerBarrierPresentationController.ShouldShowBarrier(
                    state,
                    exposureState,
                    barrier),
                Is.EqualTo(expected));
        }
    }
}
