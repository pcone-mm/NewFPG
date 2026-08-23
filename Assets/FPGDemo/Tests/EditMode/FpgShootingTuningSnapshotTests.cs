using FPG.Demo.Combat;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgShootingTuningSnapshotTests
    {
        private const string CatalogPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset";

        [TestCase(0f)]
        [TestCase(0.04f)]
        [TestCase(0.5f)]
        public void SpreadAngleConversionRoundTripsCompatibleTangent(
            float tangent)
        {
            float degrees = FpgShootingTuningSnapshot
                .SpreadTangentToHalfAngleDegrees(tangent);
            float roundTrip = FpgShootingTuningSnapshot
                .SpreadHalfAngleDegreesToTangent(degrees);

            Assert.That(roundTrip, Is.EqualTo(tangent).Within(0.00001f));
        }

        [Test]
        public void SpreadAngleConversionUsesHalfAngleInDegrees()
        {
            Assert.That(
                FpgShootingTuningSnapshot
                    .SpreadHalfAngleDegreesToTangent(45f),
                Is.EqualTo(1f).Within(0.00001f));
            Assert.That(
                FpgShootingTuningSnapshot
                    .SpreadTangentToHalfAngleDegrees(1f),
                Is.EqualTo(45f).Within(0.00001f));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(32, true)]
        [TestCase(33, false)]
        public void InputBufferTicksMustStayWithinPlannerRange(
            int inputBufferTicks,
            bool expectedValid)
        {
            FpgShootingTuningSnapshot snapshot = CaptureDefaultSnapshot();
            FpgShootingTuningSnapshot edited = snapshot.WithInputAndMovement(
                snapshot.MouseReticleSensitivity,
                snapshot.MouseReferenceResolution,
                snapshot.GamepadReticleSpeed,
                snapshot.GamepadReticleDeadzone,
                snapshot.GamepadReticleResponseExponent,
                inputBufferTicks,
                snapshot.PeekTransitionSeconds,
                snapshot.FacingFlipDelaySeconds,
                snapshot.FacingFlipDurationSeconds,
                snapshot.RetractTransitionSeconds,
                snapshot.CoverTraversalSeconds);

            Assert.That(
                edited.TryValidate(out string error),
                Is.EqualTo(expectedValid),
                error);
        }


        [TestCase(0.4999f, FpgPlayerFacingDirection.Left)]
        [TestCase(0.5f, FpgPlayerFacingDirection.Right)]
        [TestCase(1f, FpgPlayerFacingDirection.Right)]
        public void FacingHalfScreenBoundaryUsesCenterAsRight(
            float viewportX,
            FpgPlayerFacingDirection expected)
        {
            Assert.That(
                FpgPlayerFacingController.ResolveDirection(viewportX),
                Is.EqualTo(expected));
        }

        [Test]
        public void FacingDelayCancelsWhenAimReturnsToSettledSide()
        {
            FpgPlayerFacingTransitionState state =
                new FpgPlayerFacingTransitionState();
            state.Reset();

            Assert.That(
                state.Advance(
                    FpgPlayerFacingDirection.Left,
                    0.03f,
                    0.05f,
                    0.08f),
                Is.False);
            Assert.That(state.IsWaitingForDelay, Is.True);
            Assert.That(state.Phase, Is.Zero);

            Assert.That(
                state.Advance(
                    FpgPlayerFacingDirection.Right,
                    0.01f,
                    0.05f,
                    0.08f),
                Is.False);
            Assert.That(state.IsWaitingForDelay, Is.False);
            Assert.That(state.TargetDirection, Is.EqualTo(
                FpgPlayerFacingDirection.Right));
            Assert.That(state.Phase, Is.Zero);
        }

        [Test]
        public void FacingTransitionUsesSmoothStepAndReversesWithoutDelay()
        {
            FpgPlayerFacingTransitionState state =
                new FpgPlayerFacingTransitionState();
            state.Reset();
            state.Advance(
                FpgPlayerFacingDirection.Left,
                0.05f,
                0.05f,
                0.08f);
            state.Advance(
                FpgPlayerFacingDirection.Left,
                0.02f,
                0.05f,
                0.08f);

            Assert.That(state.Phase, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(
                FpgPlayerFacingController.EvaluateEasedPhase(state.Phase),
                Is.EqualTo(0.15625f).Within(0.0001f));

            state.Advance(
                FpgPlayerFacingDirection.Right,
                0.01f,
                0.05f,
                0.08f);
            Assert.That(state.IsWaitingForDelay, Is.False);
            Assert.That(state.TargetDirection, Is.EqualTo(
                FpgPlayerFacingDirection.Right));
            Assert.That(state.Phase, Is.EqualTo(0.125f).Within(0.0001f));
        }

        [Test]
        public void FacingZeroDurationAndAttackForceCompleteImmediately()
        {
            FpgPlayerFacingTransitionState state =
                new FpgPlayerFacingTransitionState();
            state.Reset();

            Assert.That(
                state.Advance(
                    FpgPlayerFacingDirection.Left,
                    0.05f,
                    0.05f,
                    0f),
                Is.True);
            Assert.That(state.Phase, Is.EqualTo(1f));

            Assert.That(
                state.Force(FpgPlayerFacingDirection.Right),
                Is.True);
            Assert.That(state.Phase, Is.Zero);
            Assert.That(state.IsWaitingForDelay, Is.False);
            Assert.That(state.IsTransitioning, Is.False);
        }

        [Test]
        public void FacingPauseLeavesProgressUntouchedAndResetRestoresRight()
        {
            FpgPlayerFacingTransitionState state =
                new FpgPlayerFacingTransitionState();
            state.Reset();
            state.Advance(
                FpgPlayerFacingDirection.Left,
                0f,
                0f,
                0.08f);
            state.Advance(
                FpgPlayerFacingDirection.Left,
                0.04f,
                0f,
                0.08f);
            float pausedPhase = state.Phase;
            Assert.That(
                state.Advance(
                    FpgPlayerFacingDirection.Left,
                    0f,
                    0f,
                    0.08f),
                Is.False);


            Assert.That(state.Phase, Is.EqualTo(pausedPhase));
            state.Reset();
            Assert.That(state.Phase, Is.Zero);
            Assert.That(state.TargetDirection, Is.EqualTo(
                FpgPlayerFacingDirection.Right));
            Assert.That(state.IsWaitingForDelay, Is.False);
        }

        [Test]
        public void FacingActionHoldCancelsDelayAndUsesFreshIdleAim()
        {
            FpgPlayerFacingTransitionState state =
                new FpgPlayerFacingTransitionState();
            state.Reset();
            state.Advance(
                FpgPlayerFacingDirection.Left,
                0.03f,
                0.05f,
                0.08f);

            state.Hold();

            Assert.That(state.IsWaitingForDelay, Is.False);
            Assert.That(state.Phase, Is.Zero);
            state.Advance(
                FpgPlayerFacingDirection.Right,
                0.04f,
                0.05f,
                0.08f);
            Assert.That(state.Phase, Is.Zero);

            state.Advance(
                FpgPlayerFacingDirection.Left,
                0.05f,
                0.05f,
                0.08f);
            state.Advance(
                FpgPlayerFacingDirection.Left,
                0.02f,
                0.05f,
                0.08f);
            Assert.That(state.Phase, Is.EqualTo(0.25f).Within(0.0001f));

            state.Hold();
            state.Advance(
                FpgPlayerFacingDirection.Right,
                0.01f,
                0.05f,
                0.08f);

            Assert.That(state.IsWaitingForDelay, Is.False);
            Assert.That(
                state.TargetDirection,
                Is.EqualTo(FpgPlayerFacingDirection.Right));
            Assert.That(state.Phase, Is.EqualTo(0.125f).Within(0.0001f));
        }

        [TestCase(-0.01f, 0.08f, false)]
        [TestCase(0.05f, -0.01f, false)]
        [TestCase(0f, 0f, true)]
        [TestCase(0.5f, 0.5f, true)]
        public void FacingDurationsMustBeFiniteAndNonNegative(
            float delaySeconds,
            float durationSeconds,
            bool expectedValid)
        {
            FpgShootingTuningSnapshot snapshot = CaptureDefaultSnapshot();
            FpgShootingTuningSnapshot edited = snapshot.WithInputAndMovement(
                snapshot.MouseReticleSensitivity,
                snapshot.MouseReferenceResolution,
                snapshot.GamepadReticleSpeed,
                snapshot.GamepadReticleDeadzone,
                snapshot.GamepadReticleResponseExponent,
                snapshot.InputBufferTicks,
                snapshot.PeekTransitionSeconds,
                delaySeconds,
                durationSeconds,
                snapshot.RetractTransitionSeconds,
                snapshot.CoverTraversalSeconds);

            Assert.That(
                edited.TryValidate(out string error),
                Is.EqualTo(expectedValid),
                error);
        }


        private static FpgShootingTuningSnapshot CaptureDefaultSnapshot()
        {
            FpgPlayableCharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(
                catalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection selection,
                    out string selectionError),
                Is.True,
                selectionError);
            Assert.That(
                FpgShootingTuningSnapshot.TryCapture(
                    selection,
                    out FpgShootingTuningSnapshot snapshot,
                    out string snapshotError),
                Is.True,
                snapshotError);
            return snapshot;
        }

        private static void AssertDamageEquals(
            DamageSpec expected,
            DamageSpec actual)
        {
            Assert.That(actual.BaseDamage, Is.EqualTo(expected.BaseDamage));
            Assert.That(actual.BreakDamage, Is.EqualTo(expected.BreakDamage));
            Assert.That(
                actual.WeakpointDamageMultiplierBasisPoints,
                Is.EqualTo(expected.WeakpointDamageMultiplierBasisPoints));
            Assert.That(
                actual.WeakpointBreakMultiplierBasisPoints,
                Is.EqualTo(expected.WeakpointBreakMultiplierBasisPoints));
        }
    }
}
