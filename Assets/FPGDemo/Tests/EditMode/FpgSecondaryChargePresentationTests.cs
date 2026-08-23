using System.Reflection;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSecondaryChargePresentationTests
    {
        [Test]
        public void PresentationSnapshotCarriesSimulationChargeProgress()
        {
            TickIndex chargeStartedTick = new TickIndex(12L);
            FpgFormalPlayerPresentationSnapshot snapshot =
                new FpgFormalPlayerPresentationSnapshot(
                    new TickIndex(27L),
                    new RuntimeId(1L),
                    FpgEncounterPhase.Combat,
                    false,
                    100,
                    100,
                    50,
                    50,
                    8,
                    10,
                    PlayerExposureState.Exposed,
                    WeaponState.AltCharging,
                    true,
                    0.5f,
                    chargeStartedTick);

            Assert.That(snapshot.IsSecondaryCharging, Is.True);
            Assert.That(snapshot.SecondaryChargeProgress, Is.EqualTo(0.5f));
            Assert.That(
                snapshot.SecondaryChargeStartedTick,
                Is.EqualTo(chargeStartedTick));

            FpgFormalPlayerPresentationSnapshot legacy =
                new FpgFormalPlayerPresentationSnapshot(
                    new TickIndex(1L),
                    new RuntimeId(1L),
                    FpgEncounterPhase.Combat,
                    false,
                    100,
                    100,
                    50,
                    50,
                    8,
                    10,
                    PlayerExposureState.Exposed,
                    WeaponState.Ready);
            Assert.That(legacy.IsSecondaryCharging, Is.False);
            Assert.That(legacy.SecondaryChargeProgress, Is.Zero);
            Assert.That(legacy.SecondaryChargeStartedTick.IsValid, Is.False);
        }

        [Test]
        public void ChargeRingIsIndependentFromReticleStrokeFeedback()
        {
            GameObject root = new GameObject(
                "ChargeReticle",
                typeof(RectTransform));
            root.SetActive(false);
            try
            {
                Image stroke = CreateImage("Stroke", root.transform);
                Image ring = CreateImage("ChargeRing", root.transform);
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;

                CombatAimReticle reticle = root.AddComponent<CombatAimReticle>();
                SetPrivateField(reticle, "lockSystemCursor", false);
                SetPrivateField(reticle, "chargeProgressImage", ring);
                SetPrivateField(
                    reticle,
                    "presentation",
                    new FpgReticlePresentation());
                root.SetActive(true);

                reticle.SetChargeProgress(true, 0f);
                AssertChargeRingState(reticle, ring, true, 0f, "zero");

                reticle.SetChargeProgress(true, 0.5f);
                AssertChargeRingState(reticle, ring, true, 0.5f, "midpoint");
                Color ringColor = ring.color;
                Vector2 ringSize = ring.rectTransform.sizeDelta;
                reticle.SetTargetState(FpgReticleTargetState.Blocked);
                reticle.PresentHit();

                Assert.That(ring.gameObject.activeSelf, Is.True);
                Assert.That(ring.fillAmount, Is.EqualTo(0.5f));
                Assert.That(ring.color, Is.EqualTo(ringColor));
                Assert.That(ring.rectTransform.sizeDelta, Is.EqualTo(ringSize));
                Assert.That(stroke.color, Is.Not.EqualTo(ring.color));
                Assert.That(reticle.TryValidate(out string error), Is.True, error);

                reticle.SetChargeProgress(true, 1f);
                reticle.SetChargeProgress(true, 1f);
                reticle.AdvanceFeedback(1f, false);
                AssertChargeRingState(reticle, ring, true, 1f, "held full");

                reticle.SetChargeProgress(false, 1f);
                AssertChargeRingState(reticle, ring, false, 0f, "explicit end");

                reticle.SetChargeProgress(true, 1f);
                reticle.ResetFeedback();
                AssertChargeRingState(reticle, ring, false, 0f, "feedback reset");

                reticle.SetChargeProgress(true, 1f);
                InvokePrivate(reticle, "OnDisable");
                AssertChargeRingState(reticle, ring, false, 0f, "disable");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ChargeVfxDriverResetsScaleAndEmissionForPoolReuse()
        {
            GameObject root = new GameObject("ChargeVfx");
            root.SetActive(false);
            try
            {
                root.transform.localScale = Vector3.one * 2f;
                ParticleSystem particleSystem = root.AddComponent<ParticleSystem>();
                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTimeMultiplier = 8f;
                ChargeProgressVfxDriver driver =
                    root.AddComponent<ChargeProgressVfxDriver>();
                SetPrivateField(driver, "minimumScaleMultiplier", 0.5f);
                SetPrivateField(driver, "maximumScaleMultiplier", 1.5f);
                SetPrivateField(driver, "minimumEmissionMultiplier", 0.25f);
                SetPrivateField(driver, "maximumEmissionMultiplier", 1.25f);
                root.SetActive(true);

                driver.SetProgress(0f);
                AssertChargeVfxState(
                    driver,
                    root.transform,
                    particleSystem,
                    0f,
                    Vector3.one,
                    2f,
                    "zero");

                driver.SetProgress(0.5f);
                AssertChargeVfxState(
                    driver,
                    root.transform,
                    particleSystem,
                    0.5f,
                    Vector3.one * 2f,
                    6f,
                    "midpoint");

                driver.SetProgress(1f);
                driver.SetProgress(1f);
                AssertChargeVfxState(
                    driver,
                    root.transform,
                    particleSystem,
                    1f,
                    Vector3.one * 3f,
                    10f,
                    "held full");

                driver.ResetForPool();
                AssertChargeVfxState(
                    driver,
                    root.transform,
                    particleSystem,
                    0f,
                    Vector3.one * 2f,
                    8f,
                    "pool release");

                driver.SetProgress(1f);
                AssertChargeVfxState(
                    driver,
                    root.transform,
                    particleSystem,
                    1f,
                    Vector3.one * 3f,
                    10f,
                    "reused full");

                InvokePrivate(driver, "OnDisable");
                AssertChargeVfxState(
                    driver,
                    root.transform,
                    particleSystem,
                    0f,
                    Vector3.one * 2f,
                    8f,
                    "disable");
                Assert.That(driver.TryValidate(out string error), Is.True, error);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BridgeEndingSnapshotsAndLifecycleClearChargeRingImmediately()
        {
            GameObject reticleRoot = new GameObject(
                "BridgeChargeReticle",
                typeof(RectTransform));
            GameObject driverRoot = new GameObject("BridgeTickDriver");
            GameObject bridgeRoot = new GameObject("BridgePresentation");
            reticleRoot.SetActive(false);
            try
            {
                Image ring = CreateImage("ChargeRing", reticleRoot.transform);
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                CombatAimReticle reticle =
                    reticleRoot.AddComponent<CombatAimReticle>();
                SetPrivateField(reticle, "lockSystemCursor", false);
                SetPrivateField(reticle, "chargeProgressImage", ring);
                SetPrivateField(
                    reticle,
                    "presentation",
                    new FpgReticlePresentation());
                reticleRoot.SetActive(true);

                FpgFormalPlayerTickDriver tickDriver =
                    driverRoot.AddComponent<FpgFormalPlayerTickDriver>();
                SetPrivateField(tickDriver, "aimViewportSource", reticle);
                FpgFormalPlayerPresentationBridge bridge =
                    bridgeRoot.AddComponent<FpgFormalPlayerPresentationBridge>();
                SetPrivateField(bridge, "playerTickDriver", tickDriver);
                AudioSource heldAudio = bridgeRoot.AddComponent<AudioSource>();
                FpgPresentationHandle heldAudioHandle =
                    new FpgPresentationHandle(99);

                FpgFormalPlayerPresentationSnapshot[] endingSnapshots =
                {
                    CreateSnapshot(
                        FpgEncounterPhase.Combat,
                        paused: false,
                        life: 100,
                        isCharging: false),
                    CreateSnapshot(
                        FpgEncounterPhase.Combat,
                        paused: true,
                        life: 100,
                        isCharging: true),
                    CreateSnapshot(
                        FpgEncounterPhase.Combat,
                        paused: false,
                        life: 0,
                        isCharging: true),
                    CreateSnapshot(
                        FpgEncounterPhase.Cleared,
                        paused: false,
                        life: 100,
                        isCharging: true),
                    FpgFormalPlayerPresentationSnapshot.Unavailable
                };
                string[] labels =
                {
                    "release or cancel",
                    "pause",
                    "death",
                    "room clear",
                    "scene unavailable"
                };

                for (int index = 0; index < endingSnapshots.Length; index++)
                {
                    reticle.SetChargeProgress(true, 1f);
                    SetPrivateField(
                        bridge,
                        "secondaryChargeAudioHandle",
                        heldAudioHandle);
                    SetPrivateField(
                        bridge,
                        "secondaryChargeAudioInstance",
                        heldAudio);
                    SetPrivateField(
                        bridge,
                        "secondaryChargeAudioSource",
                        driverRoot.transform);
                    SetPrivateField(bridge, "snapshot", endingSnapshots[index]);
                    InvokePrivate(bridge, "UpdateSecondaryChargeFeedback");
                    AssertChargeRingState(
                        reticle,
                        ring,
                        false,
                        0f,
                        labels[index]);
                    bool preservesBinding = index == 1;
                    Assert.That(bridge.HasSecondaryChargeAudio, Is.False);
                    Assert.That(
                        GetPrivateField<FpgPresentationHandle>(
                            bridge,
                            "secondaryChargeAudioHandle").IsValid,
                        Is.EqualTo(preservesBinding),
                        labels[index] + " held-audio handle");
                    Assert.That(
                        GetPrivateField<Transform>(
                            bridge,
                            "secondaryChargeAudioSource") != null,
                        Is.EqualTo(preservesBinding),
                        labels[index] + " held-audio source");
                }

                SetPrivateField(
                    bridge,
                    "secondaryChargeAudioHandle",
                    heldAudioHandle);
                SetPrivateField(
                    bridge,
                    "secondaryChargeAudioInstance",
                    heldAudio);
                SetPrivateField(
                    bridge,
                    "secondaryChargeAudioSource",
                    driverRoot.transform);
                InvokePrivate(
                    bridge,
                    "HandleEncounterLifecycle",
                    new FpgEncounterLifecycleEvent(
                        FpgEncounterLifecycleEventType.Restarted,
                        new TickIndex(31L),
                        FpgEncounterPhase.Combat));
                Assert.That(bridge.HasSecondaryChargeAudio, Is.False);
                Assert.That(
                    GetPrivateField<FpgPresentationHandle>(
                        bridge,
                        "secondaryChargeAudioHandle").IsValid,
                    Is.False,
                    "restart held-audio handle");

                reticle.SetChargeProgress(true, 1f);
                bridge.Clear();
                AssertChargeRingState(
                    reticle,
                    ring,
                    false,
                    0f,
                    "bridge clear");

                reticle.SetChargeProgress(true, 1f);
                InvokePrivate(bridge, "OnDisable");
                AssertChargeRingState(
                    reticle,
                    ring,
                    false,
                    0f,
                    "bridge disable");
            }
            finally
            {
                Object.DestroyImmediate(bridgeRoot);
                Object.DestroyImmediate(driverRoot);
                Object.DestroyImmediate(reticleRoot);
            }
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject target = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            target.transform.SetParent(parent, false);
            return target.GetComponent<Image>();
        }

        private static FpgFormalPlayerPresentationSnapshot CreateSnapshot(
            FpgEncounterPhase phase,
            bool paused,
            int life,
            bool isCharging)
        {
            return new FpgFormalPlayerPresentationSnapshot(
                new TickIndex(30L),
                new RuntimeId(1L),
                phase,
                paused,
                life,
                100,
                50,
                50,
                8,
                10,
                PlayerExposureState.Exposed,
                isCharging ? WeaponState.AltCharging : WeaponState.Ready,
                isCharging,
                1f,
                isCharging ? new TickIndex(1L) : TickIndex.Invalid);
        }

        private static void AssertChargeRingState(
            CombatAimReticle reticle,
            Image ring,
            bool expectedActive,
            float expectedProgress,
            string label)
        {
            Assert.That(
                reticle.IsChargeProgressActive,
                Is.EqualTo(expectedActive),
                label + " active state");
            Assert.That(
                reticle.ChargeProgress,
                Is.EqualTo(expectedProgress).Within(0.0001f),
                label + " reticle progress");
            Assert.That(
                ring.gameObject.activeSelf,
                Is.EqualTo(expectedActive),
                label + " visibility");
            Assert.That(
                ring.fillAmount,
                Is.EqualTo(expectedProgress).Within(0.0001f),
                label + " fill");
        }

        private static void AssertChargeVfxState(
            ChargeProgressVfxDriver driver,
            Transform particleRoot,
            ParticleSystem particleSystem,
            float expectedProgress,
            Vector3 expectedScale,
            float expectedEmission,
            string label)
        {
            Assert.That(
                driver.Progress,
                Is.EqualTo(expectedProgress).Within(0.0001f),
                label + " progress");
            Assert.That(
                (particleRoot.localScale - expectedScale).sqrMagnitude,
                Is.LessThan(0.000001f),
                label + " scale");
            Assert.That(
                particleSystem.emission.rateOverTimeMultiplier,
                Is.EqualTo(expectedEmission).Within(0.0001f),
                label + " emission");
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

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }
    }
}
