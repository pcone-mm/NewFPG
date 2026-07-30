using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgPlayerBarrierPresentationControllerTests
    {
        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [TestCase(FpgEncounterPhase.Combat, false, PlayerExposureState.Withdrawn, 1, false)]
        [TestCase(FpgEncounterPhase.Combat, false, PlayerExposureState.Exposed, 60, false)]
        [TestCase(FpgEncounterPhase.Combat, false, PlayerExposureState.Withdrawn, 0, false)]
        [TestCase(FpgEncounterPhase.Combat, true, PlayerExposureState.Withdrawn, 60, false)]
        [TestCase(FpgEncounterPhase.Cleared, false, PlayerExposureState.Withdrawn, 60, false)]
        public void PlayerAttachedCoverVisibilityIsAlwaysDisabled(
            FpgEncounterPhase phase,
            bool paused,
            PlayerExposureState exposureState,
            int barrier,
            bool expected)
        {
            FpgFormalPlayerPresentationSnapshot snapshot = CreateSnapshot(
                tick: 1L,
                phase: phase,
                paused: paused,
                barrier: barrier,
                exposureState: exposureState);

            Assert.That(
                FpgPlayerBarrierPresentationController.ShouldShowBarrier(snapshot),
                Is.EqualTo(expected));
        }

        [Test]
        public void DefeatedSnapshotStopsCombatAndUsesDefeatPresentation()
        {
            FpgFormalPlayerPresentationSnapshot snapshot = CreateSnapshot(
                tick: 20L,
                phase: FpgEncounterPhase.Defeated,
                barrier: 0,
                life: 0,
                exposureState: PlayerExposureState.Withdrawn,
                weaponState: WeaponState.Disabled);

            Assert.That(snapshot.IsCombatActive, Is.False);
            Assert.That(
                snapshot.PresentationState,
                Is.EqualTo(FpgFormalPlayerPresentationState.Defeat));
            Assert.That(
                FpgPlayerBarrierPresentationController.ShouldShowBarrier(snapshot),
                Is.False);
        }

        [Test]
        public void PeekEntersSmoothlyAndCompletesAtFifthCommittedTick()
        {
            ControllerFixture fixture = CreateControllerFixture();
            Vector3 authoredPosition = fixture.PeekRoot.localPosition;

            fixture.Controller.ApplyCommittedSnapshot(
                CreateSnapshot(
                    tick: 10L,
                    peekRequested: true,
                    peekStartedTick: 10L),
                0.04f);

            Assert.That(
                fixture.Controller.CurrentPeekProgress,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                fixture.PeekRoot.localPosition.x,
                Is.EqualTo(authoredPosition.x + 0.675f).Within(0.0001f));

            fixture.Controller.ApplyCommittedSnapshot(
                CreateSnapshot(
                    tick: 15L,
                    peekRequested: true,
                    peekStartedTick: 10L),
                0f);

            Assert.That(fixture.Controller.CurrentPeekProgress, Is.EqualTo(1f));
            Assert.That(
                fixture.PeekRoot.localPosition,
                Is.EqualTo(authoredPosition + new Vector3(1.35f, 0f, 0f)));
        }

        [Test]
        public void ReleasedPeekSnapsToAuthoredPoseWithoutAccumulatingDrift()
        {
            ControllerFixture fixture = CreateControllerFixture();
            Vector3 authoredPosition = fixture.PeekRoot.localPosition;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                fixture.Controller.ApplyCommittedSnapshot(
                    CreateSnapshot(
                        tick: cycle * 10L + 5L,
                        peekRequested: true,
                        peekStartedTick: cycle * 10L),
                    0f);
                fixture.Controller.ApplyCommittedSnapshot(
                    CreateSnapshot(tick: cycle * 10L + 6L),
                    0f);

                Assert.That(fixture.Controller.CurrentPeekProgress, Is.Zero);
                Assert.That(
                    fixture.PeekRoot.localPosition,
                    Is.EqualTo(authoredPosition));
            }
        }

        [Test]
        public void PausedSnapshotFreezesCoverAndPeekPresentation()
        {
            ControllerFixture fixture = CreateControllerFixture();
            fixture.Controller.ApplyCommittedSnapshot(
                CreateSnapshot(
                    tick: 1L,
                    peekRequested: true,
                    peekStartedTick: 1L),
                0.04f);

            float opacity = fixture.Controller.CurrentOpacity;
            float progress = fixture.Controller.CurrentPeekProgress;
            Vector3 position = fixture.PeekRoot.localPosition;

            fixture.Controller.ApplyCommittedSnapshot(
                CreateSnapshot(
                    tick: 2L,
                    paused: true,
                    barrier: 0),
                1f);

            Assert.That(fixture.Controller.CurrentOpacity, Is.EqualTo(opacity));
            Assert.That(fixture.Controller.CurrentPeekProgress, Is.EqualTo(progress));
            Assert.That(fixture.PeekRoot.localPosition, Is.EqualTo(position));
            Assert.That(fixture.Controller.IsCoverMeshVisible, Is.False);
        }

        [Test]
        public void IndependentCoverIsNotDrivenByPlayerPresentationSnapshots()
        {
            ControllerFixture fixture = CreateControllerFixture();
            fixture.Controller.ApplyCommittedSnapshot(
                CreateSnapshot(tick: 1L),
                0.18f);

            Assert.That(fixture.Controller.IsCoverMeshVisible, Is.False);
            Assert.That(fixture.Controller.CurrentOpacity, Is.Zero);
            Assert.That(fixture.Controller.IsVisible, Is.False);

            fixture.Controller.ApplyCommittedSnapshot(
                CreateSnapshot(tick: 2L, barrier: 0),
                0.01f);

            Assert.That(fixture.Controller.IsCoverMeshVisible, Is.False);
            Assert.That(fixture.Controller.CurrentOpacity, Is.Zero);
        }

        [Test]
        public void PresentationSocketsResolveOnlyAuthoredPeekProxies()
        {
            ControllerFixture fixture = CreateControllerFixture();

            Assert.That(
                fixture.Controller.TryResolvePresentationSocket(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    out Transform primary),
                Is.True);
            Assert.That(primary, Is.SameAs(fixture.PrimaryMuzzle));
            Assert.That(
                fixture.Controller.TryResolvePresentationSocket(
                    D0ActorSocketRegistry.SecondaryMuzzleId,
                    out Transform secondary),
                Is.True);
            Assert.That(secondary, Is.SameAs(fixture.SecondaryMuzzle));
            Assert.That(
                fixture.Controller.TryResolvePresentationSocket(
                    D0ActorSocketRegistry.DefaultAttackOriginId,
                    out _),
                Is.False);
        }

        [Test]
        public void ThreeCProfileDoesNotMoveAnAttachedCoverVisual()
        {
            ControllerFixture fixture = CreateControllerFixture();
            D0ThreeCProfile profile = Track(
                ScriptableObject.CreateInstance<D0ThreeCProfile>());
            Vector3 expectedPosition = new Vector3(0.45f, 1.2f, 0.3f);
            SetField(profile, "coverLocalPosition", expectedPosition);

            Assert.That(
                fixture.Controller.TrySetThreeCProfile(profile, out string error),
                Is.True,
                error);
            Assert.That(fixture.Controller.CoverVisualRoot, Is.Null);
            Assert.That(fixture.Controller.CoverRenderer, Is.Null);
        }

        [Test]
        public void ThreeCProfileIgnoresLegacyNonFiniteCoverPosition()
        {
            ControllerFixture fixture = CreateControllerFixture();
            D0ThreeCProfile profile = Track(
                ScriptableObject.CreateInstance<D0ThreeCProfile>());
            SetField(
                profile,
                "coverLocalPosition",
                new Vector3(float.NaN, 1f, 0f));

            Assert.That(
                fixture.Controller.TrySetThreeCProfile(profile, out string error),
                Is.True,
                error);
            Assert.That(fixture.Controller.CoverVisualRoot, Is.Null);
        }

        [Test]
        public void CoverValidationRejectsPhysicsBelowPeekBranch()
        {
            ControllerFixture fixture = CreateControllerFixture();
            fixture.PeekRoot.gameObject.AddComponent<BoxCollider>();

            Assert.That(
                fixture.Controller.TryValidate(out string error),
                Is.False);
            Assert.That(error, Does.Contain("Collider"));
        }

        private ControllerFixture CreateControllerFixture()
        {
            GameObject entity = Track(new GameObject("PlayerEntity"));
            Transform peekRoot = CreateChild(entity.transform, "PeekRoot");
            peekRoot.localPosition = new Vector3(0.2f, 0f, 0f);
            Transform primaryMuzzle = CreateChild(
                peekRoot,
                "PrimaryPresentationMuzzle");
            Transform secondaryMuzzle = CreateChild(
                peekRoot,
                "SecondaryPresentationMuzzle");
            FpgPlayerBarrierPresentationController controller =
                entity.AddComponent<
                    FpgPlayerBarrierPresentationController>();

            SetField(controller, "peekRoot", peekRoot);
            SetField(controller, "primaryPresentationMuzzle", primaryMuzzle);
            SetField(controller, "secondaryPresentationMuzzle", secondaryMuzzle);
            controller.ResetPresentation();

            Assert.That(controller.TryValidate(out string error), Is.True, error);
            return new ControllerFixture(
                controller,
                peekRoot,
                primaryMuzzle,
                secondaryMuzzle);
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            createdObjects.Add(value);
            return value;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void SetField(
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

        private static FpgFormalPlayerPresentationSnapshot CreateSnapshot(
            long tick,
            FpgEncounterPhase phase = FpgEncounterPhase.Combat,
            bool paused = false,
            int barrier = 60,
            int life = 100,
            PlayerExposureState exposureState = PlayerExposureState.Withdrawn,
            WeaponState weaponState = WeaponState.Ready,
            bool peekRequested = false,
            long peekStartedTick = -1L)
        {
            return new FpgFormalPlayerPresentationSnapshot(
                new TickIndex(tick),
                new RuntimeId(1L),
                phase,
                paused,
                life,
                100,
                barrier,
                100,
                6,
                6,
                exposureState,
                weaponState,
                false,
                0f,
                TickIndex.Invalid,
                peekRequested,
                new TickIndex(peekStartedTick));
        }

        private readonly struct ControllerFixture
        {
            public ControllerFixture(
                FpgPlayerBarrierPresentationController controller,
                Transform peekRoot,
                Transform primaryMuzzle,
                Transform secondaryMuzzle)
            {
                Controller = controller;
                PeekRoot = peekRoot;
                PrimaryMuzzle = primaryMuzzle;
                SecondaryMuzzle = secondaryMuzzle;
            }

            public FpgPlayerBarrierPresentationController Controller { get; }
            public Transform PeekRoot { get; }
            public Transform PrimaryMuzzle { get; }
            public Transform SecondaryMuzzle { get; }
        }
    }
}
