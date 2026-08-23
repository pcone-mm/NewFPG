using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSupplementalFeedbackTests
    {
        private const string PresentationProfilePath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset";

        [Test]
        public void TargetedFeedbackCarriesTargetWithoutChangingGlobalEvents()
        {
            RuntimeId targetId = new RuntimeId(42L);
            FpgSupplementalFeedbackEvent targeted =
                FpgSupplementalFeedbackEvent.CreateTargeted(
                    FpgSupplementalFeedbackKind.BodyHit,
                    targetId,
                    0.5f);

            Assert.That(targeted.HasTarget, Is.True);
            Assert.That(targeted.TargetId, Is.EqualTo(targetId));
            Assert.That(targeted.Intensity, Is.EqualTo(0.5f));
            Assert.That(targeted.HasResourceValues, Is.False);

            FpgSupplementalFeedbackEvent global =
                FpgSupplementalFeedbackEvent.Create(
                    FpgSupplementalFeedbackKind.PrimaryFire);
            Assert.That(global.HasTarget, Is.False);
            Assert.That(global.TargetId, Is.EqualTo(RuntimeId.Invalid));

            Assert.Throws<System.ArgumentException>(() =>
                FpgSupplementalFeedbackEvent.CreateTargeted(
                    FpgSupplementalFeedbackKind.BodyHit,
                    RuntimeId.Invalid));
        }

        [Test]
        public void TargetedHitAggregationMergesPerTargetAndKeepsTargetsSeparate()
        {
            RuntimeId firstTarget = new RuntimeId(10L);
            RuntimeId secondTarget = new RuntimeId(20L);
            RuntimeId[] targets = new RuntimeId[4];
            FpgSupplementalFeedbackKind[] kinds =
                new FpgSupplementalFeedbackKind[4];
            int count = 0;

            count = FpgFormalCombatFeedbackBridge
                .CollectTargetedSupplementalHit(
                    firstTarget,
                    FpgSupplementalFeedbackKind.BodyHit,
                    targets,
                    kinds,
                    count);
            count = FpgFormalCombatFeedbackBridge
                .CollectTargetedSupplementalHit(
                    firstTarget,
                    FpgSupplementalFeedbackKind.BodyHit,
                    targets,
                    kinds,
                    count);
            count = FpgFormalCombatFeedbackBridge
                .CollectTargetedSupplementalHit(
                    secondTarget,
                    FpgSupplementalFeedbackKind.BodyHit,
                    targets,
                    kinds,
                    count);
            count = FpgFormalCombatFeedbackBridge
                .CollectTargetedSupplementalHit(
                    firstTarget,
                    FpgSupplementalFeedbackKind.WeakpointHit,
                    targets,
                    kinds,
                    count);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(targets[0], Is.EqualTo(firstTarget));
            Assert.That(
                kinds[0],
                Is.EqualTo(FpgSupplementalFeedbackKind.WeakpointHit));
            Assert.That(targets[1], Is.EqualTo(secondTarget));
            Assert.That(
                kinds[1],
                Is.EqualTo(FpgSupplementalFeedbackKind.BodyHit));
        }

        [Test]
        public void TargetedHitAggregationSuppressesInvalidOrNonEnemyTargets()
        {
            RuntimeId[] targets = new RuntimeId[2];
            FpgSupplementalFeedbackKind[] kinds =
                new FpgSupplementalFeedbackKind[2];

            int count = FpgFormalCombatFeedbackBridge
                .CollectTargetedSupplementalHit(
                    RuntimeId.Invalid,
                    FpgSupplementalFeedbackKind.BodyHit,
                    targets,
                    kinds,
                    0);
            count = FpgFormalCombatFeedbackBridge
                .CollectTargetedSupplementalHit(
                    new RuntimeId(8L),
                    FpgSupplementalFeedbackKind.ProjectileIntercept,
                    targets,
                    kinds,
                    count);

            Assert.That(count, Is.Zero);
        }

        [Test]
        public void CombatBridgeResolvesSupplementalHitKindWithPriority()
        {
            Assert.That(
                FpgFormalCombatFeedbackBridge.ResolveSupplementalHitKind(
                    CreateFeedback(1L, HitPart.Body, DamageChannel.Life)),
                Is.EqualTo(FpgSupplementalFeedbackKind.BodyHit));

            Assert.That(
                FpgFormalCombatFeedbackBridge.ResolveSupplementalHitKind(
                    CreateFeedback(2L, HitPart.Weakpoint, DamageChannel.Life)),
                Is.EqualTo(FpgSupplementalFeedbackKind.WeakpointHit));

            Assert.That(
                FpgFormalCombatFeedbackBridge.ResolveSupplementalHitKind(
                    CreateFeedback(3L, HitPart.Projectile, DamageChannel.ProjectileHp)),
                Is.EqualTo(FpgSupplementalFeedbackKind.ProjectileIntercept));

            Assert.That(
                FpgFormalCombatFeedbackBridge.ResolveSupplementalHitKind(
                    CreateFeedback(4L, HitPart.Weakpoint, DamageChannel.ProjectileHp)),
                Is.EqualTo(FpgSupplementalFeedbackKind.WeakpointHit));
        }

        [Test]
        public void HudPresenterPublishesSupplementalFeedbackOnlyOnRealChanges()
        {
            GameObject root = null;
            try
            {
                FpgFormalPlayerHudPresenter presenter = CreateHudPresenter(out root);
                List<FpgSupplementalFeedbackEvent> events =
                    new List<FpgSupplementalFeedbackEvent>();
                presenter.SupplementalFeedback += events.Add;

                presenter.Refresh(CreateSnapshot(
                    paused: false,
                    life: 100,
                    maxLife: 100,
                    barrier: 25,
                    maxBarrier: 100,
                    ammo: 8,
                    magazineCapacity: 8));
                Assert.That(events, Is.Empty);

                presenter.Refresh(CreateSnapshot(
                    paused: false,
                    life: 100,
                    maxLife: 100,
                    barrier: 25,
                    maxBarrier: 100,
                    ammo: 8,
                    magazineCapacity: 8));
                Assert.That(events, Is.Empty);

                presenter.Refresh(CreateSnapshot(
                    paused: true,
                    life: 90,
                    maxLife: 100,
                    barrier: 25,
                    maxBarrier: 100,
                    ammo: 7,
                    magazineCapacity: 8));
                Assert.That(events, Is.Empty);

                presenter.Refresh(CreateSnapshot(
                    paused: false,
                    life: 80,
                    maxLife: 100,
                    barrier: 0,
                    maxBarrier: 100,
                    ammo: 7,
                    magazineCapacity: 8,
                    coverDestroyed: true));
                Assert.That(events.Count, Is.EqualTo(2));
                Assert.That(events[0].Kind, Is.EqualTo(
                    FpgSupplementalFeedbackKind.HudLifeChanged));
                Assert.That(events[0].PreviousValue, Is.EqualTo(90));
                Assert.That(events[0].CurrentValue, Is.EqualTo(80));
                Assert.That(events[1].Kind, Is.EqualTo(
                    FpgSupplementalFeedbackKind.HudBarrierChanged));
                Assert.That(events[1].PreviousValue, Is.EqualTo(25));
                Assert.That(events[1].CurrentValue, Is.EqualTo(0));

                presenter.Refresh(CreateSnapshot(
                    paused: false,
                    life: 80,
                    maxLife: 100,
                    barrier: 0,
                    maxBarrier: 100,
                    ammo: 6,
                    magazineCapacity: 8,
                    coverDestroyed: true));
                Assert.That(events.Count, Is.EqualTo(3));
                Assert.That(events[2].Kind, Is.EqualTo(
                    FpgSupplementalFeedbackKind.HudAmmoChanged));
                Assert.That(events[2].PreviousValue, Is.EqualTo(7));
                Assert.That(events[2].CurrentValue, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static FpgFormalPlayerPresentationSnapshot CreateSnapshot(
            bool paused,
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            int ammo,
            int magazineCapacity,
            bool coverDestroyed = false,
            bool coverMoving = false)
        {
            return new FpgFormalPlayerPresentationSnapshot(
                new TickIndex(1L),
                new RuntimeId(1L),
                FpgEncounterPhase.Combat,
                paused,
                life,
                maxLife,
                barrier,
                maxBarrier,
                ammo,
                magazineCapacity,
                PlayerExposureState.Exposed,
                WeaponState.Ready,
                false,
                0f,
                TickIndex.Invalid,
                false,
                TickIndex.Invalid,
                "cover-center",
                coverDestroyed,
                coverMoving);
        }

        private static FpgFormalPlayerHudPresenter CreateHudPresenter(
            out GameObject root)
        {
            root = new GameObject(
                "FormalHud",
                typeof(RectTransform),
                typeof(FpgFormalPlayerHudPresenter));
            FpgFormalPlayerHudPresenter presenter =
                root.GetComponent<FpgFormalPlayerHudPresenter>();
            FpgFormalBarView life = CreateBar(
                "LifeBar",
                200f,
                out GameObject lifeObject);
            lifeObject.transform.SetParent(root.transform, false);
            FpgFormalBarView barrier = CreateBar(
                "BarrierBar",
                200f,
                out GameObject barrierObject);
            barrierObject.transform.SetParent(root.transform, false);
            FpgFormalBarView ammo = CreateBar(
                "AmmoBar",
                200f,
                out GameObject ammoObject);
            ammoObject.transform.SetParent(root.transform, false);
            Text lifeText = CreateText(root.transform, "LifeText");
            Text barrierText = CreateText(root.transform, "BarrierText");
            Text ammoText = CreateText(root.transform, "AmmoText");
            Text stateText = CreateText(root.transform, "StateText");

            CombatPresentationProfile profile =
                AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                    PresentationProfilePath);
            Assert.That(profile, Is.Not.Null, PresentationProfilePath);

            SerializedObject data = new SerializedObject(presenter);
            SetReference(data, "presentationProfile", profile);
            SetReference(data, "lifeBar", life);
            SetReference(data, "barrierBar", barrier);
            SetReference(data, "ammoBar", ammo);
            SetReference(data, "lifeText", lifeText);
            SetReference(data, "barrierText", barrierText);
            SetReference(data, "ammoText", ammoText);
            SetReference(data, "stateText", stateText);
            data.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(presenter.TryPrepare(out string error), Is.True, error);
            return presenter;
        }

        private static FpgFormalBarView CreateBar(
            string name,
            float width,
            out GameObject root)
        {
            root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(FpgFormalBarView));
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(width, 20f);

            GameObject fillArea = new GameObject(
                "FillArea",
                typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform areaRect = (RectTransform)fillArea.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(10f, 2f);
            areaRect.offsetMax = new Vector2(-10f, -2f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);

            FpgFormalBarView bar = root.GetComponent<FpgFormalBarView>();
            SerializedObject data = new SerializedObject(bar);
            SetReference(data, "fillRect", fillRect);
            data.ApplyModifiedPropertiesWithoutUndo();
            bar.SetNormalizedValue(1f);
            return bar;
        }

        private static Text CreateText(Transform parent, string name)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            value.transform.SetParent(parent, false);
            return value.GetComponent<Text>();
        }

        private static void SetReference(
            SerializedObject data,
            string propertyName,
            Object value)
        {
            SerializedProperty property = data.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.objectReferenceValue = value;
        }

        private static FpgResolvedDamageFeedback CreateFeedback(
            long sequence,
            HitPart hitPart,
            DamageChannel channel)
        {
            QueryTargetKind targetKind = hitPart == HitPart.Projectile
                ? QueryTargetKind.Projectile
                : QueryTargetKind.Combatant;
            ImpactId impactId = new ImpactId(sequence);
            ImpactIntent intent = new ImpactIntent(
                impactId,
                new AttackId(sequence),
                new ShotId(sequence),
                new RuntimeId(1L),
                new RuntimeId(2L),
                new TickIndex(sequence),
                new DamageSpec(5, 0),
                hitPart,
                DamageType.Normal,
                CombatTags.Primary,
                impactOrdinal: (int)sequence - 1,
                spatialContext: new ImpactSpatialContext(
                    new SpatialVectorKey(1, 2, 3),
                    targetKind,
                    hitPart));
            return new FpgResolvedDamageFeedback(
                sequence,
                intent,
                new DamagePacket(
                    impactId,
                    channel,
                    5,
                    0,
                    100,
                    95),
                false);
        }
    }
}
