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
    public sealed class FpgLayeredAimIndicatorTests
    {
        [Test]
        public void BaseStateResolutionUsesFormalPriorityOrder()
        {
            Assert.That(
                CombatAimReticle.ResolveBaseState(true, true, true, true, true),
                Is.EqualTo(FpgAimIndicatorBaseState.Hidden));
            Assert.That(
                CombatAimReticle.ResolveBaseState(false, true, true, true, true),
                Is.EqualTo(FpgAimIndicatorBaseState.Reloading));
            Assert.That(
                CombatAimReticle.ResolveBaseState(false, false, true, true, true),
                Is.EqualTo(FpgAimIndicatorBaseState.CurrentCoverBlocked));
            Assert.That(
                CombatAimReticle.ResolveBaseState(false, false, false, true, true),
                Is.EqualTo(FpgAimIndicatorBaseState.Unavailable));
            Assert.That(
                CombatAimReticle.ResolveBaseState(false, false, false, false, true),
                Is.EqualTo(FpgAimIndicatorBaseState.Enemy));
            Assert.That(
                CombatAimReticle.ResolveBaseState(false, false, false, false, false),
                Is.EqualTo(FpgAimIndicatorBaseState.Normal));
        }

        [Test]
        public void ClearedRoomKeepsAimIndicatorVisibleForExitInteraction()
        {
            FpgFormalPlayerPresentationSnapshot snapshot =
                new FpgFormalPlayerPresentationSnapshot(
                    new TickIndex(30L),
                    new RuntimeId(1L),
                    FpgEncounterPhase.Cleared,
                    false,
                    100,
                    100,
                    50,
                    50,
                    8,
                    8,
                    PlayerExposureState.Withdrawn,
                    WeaponState.Ready);

            Assert.That(
                snapshot.AimIndicatorBaseState,
                Is.EqualTo(FpgAimIndicatorBaseState.Normal));
        }

        [Test]
        public void ShotAndHitLayersOverlapAndHitDeduplicatesPerAttack()
        {
            ReticleFixture fixture = new ReticleFixture();
            try
            {
                fixture.Reticle.SetTargetState(FpgReticleTargetState.Hittable);
                fixture.Reticle.PresentShot();
                fixture.Reticle.AdvanceFeedback(0.04f, false);
                fixture.Reticle.PresentHit(1001L);

                Assert.That(fixture.Reticle.IsShotFeedbackActive, Is.True);
                Assert.That(fixture.Reticle.IsHitFeedbackActive, Is.True);
                Assert.That(fixture.Graphic.ShotAlpha, Is.GreaterThan(0f));
                Assert.That(fixture.Graphic.HitAlpha, Is.GreaterThan(0f));
                Assert.That(
                    fixture.Reticle.BaseState,
                    Is.EqualTo(FpgAimIndicatorBaseState.Enemy));

                fixture.Reticle.AdvanceFeedback(0.05f, false);
                float elapsedHitRemaining = fixture.Reticle.HitTimeRemaining;
                fixture.Reticle.PresentHit(1001L);
                Assert.That(
                    fixture.Reticle.HitTimeRemaining,
                    Is.EqualTo(elapsedHitRemaining).Within(0.0001f));

                fixture.Reticle.PresentHit(1002L);
                Assert.That(
                    fixture.Reticle.HitTimeRemaining,
                    Is.GreaterThan(elapsedHitRemaining));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SnapshotDrivesReloadProgressSpreadAndSecondaryReferenceRing()
        {
            ReticleFixture fixture = new ReticleFixture();
            try
            {
                fixture.Reticle.SetFormalPresentation(CreateSnapshot(
                    WeaponState.Reloading,
                    FpgAimIndicatorBaseState.Reloading,
                    reloadProgress: 0.625f,
                    charging: false,
                    chargeProgress: 0f));

                Assert.That(
                    fixture.Graphic.BaseState,
                    Is.EqualTo(FpgAimIndicatorBaseState.Reloading));
                Assert.That(
                    fixture.Graphic.ReloadProgress,
                    Is.EqualTo(0.625f).Within(0.0001f));
                Assert.That(fixture.Graphic.PrimarySpreadRadius, Is.Zero);

                fixture.Reticle.SetFormalPresentation(CreateSnapshot(
                    WeaponState.Ready,
                    FpgAimIndicatorBaseState.Normal,
                    reloadProgress: 0f,
                    charging: false,
                    chargeProgress: 0f));
                float expectedSpread =
                    CombatAimReticle.CalculateReferencePixelRadius(
                        0.04f,
                        fixture.Camera.fieldOfView,
                        1080f);
                Assert.That(
                    fixture.Graphic.PrimarySpreadRadius,
                    Is.EqualTo(expectedSpread).Within(0.001f));

                fixture.Reticle.SetFormalPresentation(CreateSnapshot(
                    WeaponState.AltCharging,
                    FpgAimIndicatorBaseState.Normal,
                    reloadProgress: 0f,
                    charging: true,
                    chargeProgress: 0.5f));
                float expectedSecondary =
                    CombatAimReticle.CalculateReferencePixelRadius(
                        3f / 20f,
                        fixture.Camera.fieldOfView,
                        1080f);
                Assert.That(fixture.Graphic.IsSecondaryRangeVisible, Is.True);
                Assert.That(
                    fixture.Graphic.SecondaryRangeRadius,
                    Is.EqualTo(expectedSecondary).Within(0.001f));
                Assert.That(
                    fixture.Graphic.SecondaryChargeProgress,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(fixture.Graphic.PrimarySpreadRadius, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReloadProgressUsesAuthoritativeStartTickAndDuration()
        {
            TickIndex startTick = new TickIndex(100L);

            Assert.That(
                FpgFormalPlayerTickDriver.CalculateReloadProgress01(
                    startTick,
                    new TickIndex(99L),
                    4),
                Is.Zero);
            Assert.That(
                FpgFormalPlayerTickDriver.CalculateReloadProgress01(
                    startTick,
                    startTick,
                    4),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(
                FpgFormalPlayerTickDriver.CalculateReloadProgress01(
                    startTick,
                    new TickIndex(103L),
                    4),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                FpgFormalPlayerTickDriver.CalculateReloadProgress01(
                    startTick,
                    new TickIndex(200L),
                    4),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PrimarySpreadRadiusUsesFixedTwentyMeterReferenceAtAllDistances()
        {
            ReticleFixture fixture = new ReticleFixture();
            try
            {
                FpgFormalPlayerPresentationSnapshot snapshot = CreateSnapshot(
                    WeaponState.Ready,
                    FpgAimIndicatorBaseState.Normal,
                    reloadProgress: 0f,
                    charging: false,
                    chargeProgress: 0f);
                float expectedRadius =
                    CombatAimReticle.CalculateReferenceDistancePixelRadius(
                        worldRadius: 20f * 0.04f,
                        referenceDistance: 20f,
                        verticalFieldOfViewDegrees:
                            fixture.Camera.fieldOfView,
                        referenceHeight: 1080f);

                float[] aimDistances = { 5f, 20f, 50f };
                for (int index = 0; index < aimDistances.Length; index++)
                {
                    fixture.Reticle.SetResolvedAimContext(
                        CreateResolvedAimContext(
                            fixture.Camera,
                            aimDistances[index]));
                    fixture.Reticle.SetFormalPresentation(snapshot);

                    Assert.That(
                        fixture.Graphic.PrimarySpreadRadius,
                        Is.EqualTo(expectedRadius).Within(0.001f),
                        $"Aim distance {aimDistances[index]}m changed the reticle size.");
                }
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static FpgResolvedAimContext CreateResolvedAimContext(
            Camera camera,
            float aimDistance)
        {
            Vector2 viewport = new Vector2(0.88f, 0.78f);
            Ray cameraRay = camera.ViewportPointToRay(viewport);
            Vector3 targetPoint = cameraRay.GetPoint(aimDistance);
            Vector3 shotOrigin = new Vector3(-0.65f, -0.55f, 1.1f);
            return new FpgResolvedAimContext(
                viewport,
                cameraRay.origin,
                cameraRay.direction,
                targetPoint,
                shotOrigin,
                (targetPoint - shotOrigin).normalized,
                targetPoint,
                FpgResolvedAimTargetType.None,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                GeometryId.Invalid,
                string.Empty,
                string.Empty,
                1L,
                0L,
                Vector3.Distance(shotOrigin, targetPoint));
        }

        private static FpgFormalPlayerPresentationSnapshot CreateSnapshot(
            WeaponState weaponState,
            FpgAimIndicatorBaseState baseState,
            float reloadProgress,
            bool charging,
            float chargeProgress)
        {
            return new FpgFormalPlayerPresentationSnapshot(
                new TickIndex(30L),
                new RuntimeId(1L),
                FpgEncounterPhase.Combat,
                false,
                100,
                100,
                50,
                50,
                8,
                8,
                PlayerExposureState.Withdrawn,
                weaponState,
                charging,
                chargeProgress,
                charging ? new TickIndex(20L) : TickIndex.Invalid,
                false,
                TickIndex.Invalid,
                "cover-a",
                false,
                false,
                baseState,
                reloadProgress,
                0.04f,
                3f,
                77L);
        }

        private sealed class ReticleFixture
        {
            private readonly GameObject reticleObject;
            private readonly GameObject cameraObject;
            private readonly D0CombatFeelProfile combatFeel;

            public ReticleFixture()
            {
                reticleObject = new GameObject(
                    "LayeredReticleTest",
                    typeof(RectTransform));
                reticleObject.SetActive(false);
                Reticle = reticleObject.AddComponent<CombatAimReticle>();
                SetPrivateField(Reticle, "lockSystemCursor", false);

                cameraObject = new GameObject(
                    "LayeredReticleCamera",
                    typeof(Camera));
                Camera = cameraObject.GetComponent<Camera>();
                Camera.fieldOfView = 60f;
                combatFeel = ScriptableObject.CreateInstance<
                    D0CombatFeelProfile>();
                Assert.That(
                    Reticle.TrySetAimIndicatorPresentation(
                        new PlayerAimIndicatorPresentationDefinition(),
                        combatFeel,
                        Camera,
                        out string error),
                    Is.True,
                    error);
                reticleObject.SetActive(true);
                Graphic = Reticle.LayeredGraphic;
                Assert.That(Graphic, Is.Not.Null);
            }

            public CombatAimReticle Reticle { get; }
            public LayeredAimIndicatorGraphic Graphic { get; }
            public Camera Camera { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(combatFeel);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(reticleObject);
            }

            private static void SetPrivateField<T>(
                object target,
                string fieldName,
                T value)
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(target, value);
            }
        }
    }
}
