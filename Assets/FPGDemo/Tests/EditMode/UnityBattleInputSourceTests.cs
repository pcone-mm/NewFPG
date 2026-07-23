using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class UnityBattleInputSourceTests
    {
        [Test]
        public void CapturedEdgesAreConsumedOnceAcrossCatchUpTicks()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: true,
                secondaryReleased: true,
                reloadPressed: true,
                pausePressed: false,
                restartPressed: false));

            PlayerInputFrame first = source.GetFrame(new TickIndex(0));
            PlayerInputFrame catchUp = source.GetFrame(new TickIndex(1));

            Assert.That(first.EdgeCommandCount, Is.EqualTo(3));
            Assert.That(first.EdgeCommands[0].Type, Is.EqualTo(InputEdgeType.SecondaryPressed));
            Assert.That(first.EdgeCommands[1].Type, Is.EqualTo(InputEdgeType.SecondaryReleased));
            Assert.That(first.EdgeCommands[2].Type, Is.EqualTo(InputEdgeType.ReloadPressed));
            Assert.That(first.EdgeCommands[0].Sequence.Value, Is.EqualTo(1));
            Assert.That(first.EdgeCommands[1].Sequence.Value, Is.EqualTo(2));
            Assert.That(first.EdgeCommands[2].Sequence.Value, Is.EqualTo(3));
            Assert.That(catchUp.EdgeCommandCount, Is.Zero);
            Assert.That(catchUp.AimHeld, Is.True);
            Assert.That(catchUp.PrimaryHeld, Is.True);
        }

        [Test]
        public void SecondaryHeldPersistsAcrossCatchUpTicksAndClearsExplicitly()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false,
                secondaryHeld: true));

            PlayerInputFrame pressed = source.GetFrame(new TickIndex(0));
            PlayerInputFrame catchUp = source.GetFrame(new TickIndex(1));

            Assert.That(pressed.SecondaryHeld, Is.True);
            Assert.That(pressed.HasSecondaryInput, Is.True);
            Assert.That(catchUp.SecondaryHeld, Is.True);
            Assert.That(catchUp.HasSecondaryInput, Is.True);

            source.ClearGameplayInput();
            PlayerInputFrame cleared = source.GetFrame(new TickIndex(2));
            Assert.That(cleared.SecondaryHeld, Is.False);
            Assert.That(cleared.HasSecondaryInput, Is.False);
        }

        [Test]
        public void CapturesAppendEdgesAndRetainThoseBeyondOneBattleTick()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: true,
                pausePressed: false,
                restartPressed: false));
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: true,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: true,
                pausePressed: false,
                restartPressed: false));

            PlayerInputFrame first = source.GetFrame(new TickIndex(0));

            Assert.That(first.AimHeld, Is.False);
            Assert.That(first.PrimaryHeld, Is.False);
            Assert.That(first.EdgeCommandCount, Is.EqualTo(BattleTickInput.MaxEdgeCommandCount));
            Assert.That(first.EdgeCommands[0].Type, Is.EqualTo(InputEdgeType.SecondaryPressed));
            Assert.That(first.EdgeCommands[1].Type, Is.EqualTo(InputEdgeType.ReloadPressed));
            Assert.That(first.EdgeCommands[2].Type, Is.EqualTo(InputEdgeType.SecondaryReleased));
            Assert.That(first.EdgeCommands[0].Sequence.Value, Is.EqualTo(1));
            Assert.That(first.EdgeCommands[1].Sequence.Value, Is.EqualTo(2));
            Assert.That(first.EdgeCommands[2].Sequence.Value, Is.EqualTo(3));

            PlayerInputFrame second = source.GetFrame(new TickIndex(1));

            Assert.That(second.EdgeCommandCount, Is.EqualTo(1));
            Assert.That(second.EdgeCommands[0].Type, Is.EqualTo(InputEdgeType.ReloadPressed));
            Assert.That(second.EdgeCommands[0].Sequence.Value, Is.EqualTo(4));
        }

        [Test]
        public void SessionControlEdgesAreConsumedIndependently()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: true,
                restartPressed: true));
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            Assert.That(source.ConsumePausePressed(), Is.True);
            Assert.That(source.ConsumePausePressed(), Is.False);
            Assert.That(source.ConsumeRestartPressed(), Is.True);
            Assert.That(source.ConsumeRestartPressed(), Is.False);
        }

        [Test]
        public void ClearGameplayInputDropsQueuedGameplayStateAndRetainsControlLatches()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: true,
                pausePressed: true,
                restartPressed: true));

            source.ClearGameplayInput();

            PlayerInputFrame cleared = source.GetFrame(new TickIndex(0));
            Assert.That(cleared.AimHeld, Is.False);
            Assert.That(cleared.PrimaryHeld, Is.False);
            Assert.That(cleared.EdgeCommandCount, Is.Zero);
            Assert.That(cleared.CancelSecondary, Is.True);
            Assert.That(source.ConsumePausePressed(), Is.True);
            Assert.That(source.ConsumeRestartPressed(), Is.True);

            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: true,
                pausePressed: false,
                restartPressed: false));
            PlayerInputFrame afterClear = source.GetFrame(new TickIndex(1));

            Assert.That(afterClear.EdgeCommandCount, Is.EqualTo(1));
            Assert.That(afterClear.EdgeCommands[0].Type, Is.EqualTo(InputEdgeType.ReloadPressed));
            Assert.That(afterClear.EdgeCommands[0].Sequence.Value, Is.EqualTo(3));
        }

        [Test]
        public void BeginRoomInteractionPreservesHeldAttacksAndDropsOldEdges()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: true,
                pausePressed: false,
                restartPressed: false,
                secondaryHeld: true));

            source.BeginRoomInteraction();
            PlayerInputFrame frame = source.GetFrame(new TickIndex(0L));

            Assert.That(source.PrimaryHeld, Is.True);
            Assert.That(source.SecondaryHeld, Is.True);
            Assert.That(frame.PrimaryHeld, Is.True);
            Assert.That(frame.SecondaryHeld, Is.True);
            Assert.That(frame.EdgeCommandCount, Is.Zero);
            Assert.That(frame.CancelSecondary, Is.True);
        }

        [Test]
        public void BeginRoomInteractionWithoutHeldSecondaryDoesNotInjectCancellation()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false,
                secondaryHeld: false));

            source.BeginRoomInteraction();
            PlayerInputFrame frame = source.GetFrame(new TickIndex(0L));

            Assert.That(frame.CancelSecondary, Is.False);
        }

        [Test]
        public void AimWithdrawalRequestsOneSecondaryCancellationFrame()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.GetFrame(new TickIndex(0));

            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            PlayerInputFrame withdrawn = source.GetFrame(new TickIndex(1));
            PlayerInputFrame following = source.GetFrame(new TickIndex(2));

            Assert.That(withdrawn.AimHeld, Is.False);
            Assert.That(withdrawn.CancelSecondary, Is.True);
            Assert.That(following.CancelSecondary, Is.False);
        }

[Test]
        public void SharedAimSecondaryReleaseWinsOverAimWithdrawal()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            PlayerInputFrame pressed = source.GetFrame(new TickIndex(0));
            Assert.That(pressed.CancelSecondary, Is.False);
            Assert.That(pressed.EdgeCommandCount, Is.EqualTo(1));
            Assert.That(
                pressed.EdgeCommands[0].Type,
                Is.EqualTo(InputEdgeType.SecondaryPressed));

            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: true,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            PlayerInputFrame released = source.GetFrame(new TickIndex(1));
            Assert.That(released.AimHeld, Is.False);
            Assert.That(released.CancelSecondary, Is.False);
            Assert.That(released.EdgeCommandCount, Is.EqualTo(1));
            Assert.That(
                released.EdgeCommands[0].Type,
                Is.EqualTo(InputEdgeType.SecondaryReleased));
        }


        [Test]
        public void AimWithdrawalCancellationSurvivesBattleTickInputRoundTrip()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.CaptureAimPose(Vector3.zero, Vector3.forward, Vector3.up);
            source.GetTickInput(new TickIndex(0));

            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            BattleTickInput tickInput = source.GetTickInput(new TickIndex(1));
            PlayerInputFrame frame = tickInput.CopyToPlayerInputFrame(
                new InputEdgeCommand[BattleTickInput.MaxEdgeCommandCount]);

            Assert.That(tickInput.CancelSecondary, Is.True);
            Assert.That(frame.CancelSecondary, Is.True);
        }

        [Test]
        public void TickInputRequiresAndQuantizesAnExplicitAimPose()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: true,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            Assert.That(source.GetTickInput(new TickIndex(0)).IsValid, Is.False);

            GameObject anchor = new GameObject("WP4 Aim Anchor");
            try
            {
                anchor.transform.position = new Vector3(1.25f, -2f, 0.5f);
                source.CaptureAimPose(anchor.transform);

                BattleTickInput first = source.GetTickInput(new TickIndex(0));
                BattleTickInput catchUp = source.GetTickInput(new TickIndex(1));

                Assert.That(first.IsValid, Is.True);
                Assert.That(first.AimPose.Origin, Is.EqualTo(new SpatialVectorKey(1250, -2000, 500)));
                Assert.That(first.AimPose.Forward, Is.EqualTo(new SpatialVectorKey(0, 0, SpatialContract.DirectionUnits)));
                Assert.That(first.EdgeCommandCount, Is.EqualTo(1));
                Assert.That(catchUp.IsValid, Is.True);
                Assert.That(catchUp.AimPose.Tick, Is.EqualTo(new TickIndex(1)));
                Assert.That(catchUp.AimPose.PoseVersion, Is.EqualTo(first.AimPose.PoseVersion));
                Assert.That(catchUp.EdgeCommandCount, Is.Zero);

                source.CaptureAimPose(anchor.transform);
                BattleTickInput unchanged = source.GetTickInput(new TickIndex(2));
                Assert.That(unchanged.AimPose.PoseVersion, Is.EqualTo(first.AimPose.PoseVersion));

                anchor.transform.position += new Vector3(0.001f, 0f, 0f);
                source.CaptureAimPose(anchor.transform);
                BattleTickInput moved = source.GetTickInput(new TickIndex(3));
                Assert.That(moved.AimPose.PoseVersion, Is.GreaterThan(first.AimPose.PoseVersion));
            }
            finally
            {
                Object.DestroyImmediate(anchor);
            }
        }

        [Test]
        public void ExplicitAimPoseKeepsOriginAndBuildsAnOrthonormalBasis()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            source.CaptureAimPose(
                new Vector3(1.2f, 1.55f, -0.4f),
                new Vector3(2f, 0.5f, 4f),
                Vector3.up);

            BattleTickInput input = source.GetTickInput(new TickIndex(7));
            Assert.That(input.IsValid, Is.True);
            Assert.That(input.AimPose.Origin, Is.EqualTo(new SpatialVectorKey(1200, 1550, -400)));
            Assert.That(input.AimPose.Forward, Is.EqualTo(new SpatialVectorKey(444444, 111111, 888889)));
            Assert.That(input.AimPose.Right, Is.EqualTo(new SpatialVectorKey(894427, 0, -447214)));
            Assert.That(input.AimPose.Up, Is.EqualTo(new SpatialVectorKey(-49690, 993808, -99381)));
        }

        [Test]
        public void ExplicitAimPoseUsesFallbackUpForCollinearReference()
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: false,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));

            source.CaptureAimPose(Vector3.zero, Vector3.up, Vector3.up);

            BattleTickInput input = source.GetTickInput(new TickIndex(0));
            Assert.That(input.IsValid, Is.True);
            Assert.That(input.AimPose.Forward, Is.EqualTo(new SpatialVectorKey(0, SpatialContract.DirectionUnits, 0)));
            Assert.That(input.AimPose.Right, Is.Not.EqualTo(default(SpatialVectorKey)));
            Assert.That(input.AimPose.Up, Is.Not.EqualTo(default(SpatialVectorKey)));
        }
    }
}
