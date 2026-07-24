using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FormalHudGeometryTests
    {
        private const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";
        private const string OverheadBarPrefabPath =
            "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_OverheadHealthBar.prefab";
        private const string DamagePopupPrefabPath =
            "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_DamagePopup.prefab";
        private const string BehaviorScriptPath =
            "Assets/FPGDemo/Runtime/Unity/Config/FpgEnemyBehaviorDefinition.cs";
        private const string PresentationProfilePath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset";
        private const string HitTipArtRoot =
            "Assets/FPGDemo/Presentation/HUD/HitTip";
        private const string HitTipNormalBackgroundPath =
            HitTipArtRoot + "/di_nomal&critical.png";
        private const string HitTipElementalBackgroundPath =
            HitTipArtRoot + "/di_elemental.png";
        private const string ThreeCProfilePath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_ThreeC.asset";
        private const string CombatFeelProfilePath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_CombatFeel.asset";

        [Test]
        public void FormalBarChangesAnchorAndActualRectWidth()
        {
            GameObject root = null;
            try
            {
                FpgFormalBarView bar = CreateBar("Bar", 200f, out root);
                Assert.That(bar.TryValidate(out string error), Is.True, error);

                AssertBarRatio(bar, 1f);
                AssertBarRatio(bar, 0.5f);
                AssertBarRatio(bar, 0f);
                Assert.That(bar.SetNormalizedValue(2f), Is.True);
                AssertBarRatio(bar, 1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormalBarSeparatesImmediateValueFromPausedVisualTransition()
        {
            GameObject root = null;
            try
            {
                FpgFormalBarView bar = CreateBar("Bar", 200f, out root);
                Assert.That(bar.TrySetTransitionDuration(1f), Is.True);
                Assert.That(bar.SetValue(50, 100), Is.True);
                Assert.That(bar.TargetNormalizedValue, Is.EqualTo(0.5f));
                Assert.That(bar.NormalizedValue, Is.EqualTo(1f));

                bar.Advance(0.5f);
                float halfway = bar.NormalizedValue;
                Assert.That(halfway, Is.GreaterThan(0.5f).And.LessThan(1f));

                bar.SetPaused(true);
                bar.Advance(1f);
                Assert.That(bar.NormalizedValue, Is.EqualTo(halfway));

                bar.SetPaused(false);
                bar.Advance(0.5f);
                Assert.That(bar.NormalizedValue, Is.EqualTo(0.5f).Within(0.0001f));
                Canvas.ForceUpdateCanvases();
                Assert.That(
                    bar.FillRect.rect.width,
                    Is.EqualTo(
                        ((RectTransform)bar.FillRect.parent).rect.width * 0.5f)
                        .Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormalReticlePulseStateFreezesWhilePausedAndReturnsToAimState()
        {
            GameObject root = new GameObject("FormalReticle", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                CombatAimReticle reticle = root.AddComponent<CombatAimReticle>();
                GameObject stroke = new GameObject(
                    "Horizontal",
                    typeof(RectTransform),
                    typeof(Image));
                stroke.transform.SetParent(root.transform, false);
                RectTransform horizontal = (RectTransform)stroke.transform;
                horizontal.sizeDelta = new Vector2(30f, 2f);
                GameObject verticalObject = new GameObject(
                    "Vertical",
                    typeof(RectTransform),
                    typeof(Image));
                verticalObject.transform.SetParent(root.transform, false);
                RectTransform vertical =
                    (RectTransform)verticalObject.transform;
                vertical.sizeDelta = new Vector2(2f, 30f);
                CombatPresentationProfile profile =
                    AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                        PresentationProfilePath);

                Assert.That(profile, Is.Not.Null, PresentationProfilePath);
                Assert.That(
                    reticle.TrySetPresentationProfile(profile, out string error),
                    Is.True,
                    error);
                reticle.SetTargetState(FpgReticleTargetState.Hittable);
                AssertReticleGeometry(
                    (RectTransform)root.transform,
                    horizontal,
                    vertical,
                    profile.FormalReticle.HittableSize);

                reticle.PresentShot();
                AssertReticleGeometry(
                    (RectTransform)root.transform,
                    horizontal,
                    vertical,
                    profile.FormalReticle.ShotPulseSize);
                float shotRemaining = reticle.PulseTimeRemaining;
                Assert.That(
                    reticle.PulseState,
                    Is.EqualTo(FpgReticlePulseState.Shot));
                reticle.AdvanceFeedback(shotRemaining, true);
                Assert.That(reticle.PulseTimeRemaining, Is.EqualTo(shotRemaining));

                reticle.PresentHit();
                Assert.That(
                    reticle.PulseState,
                    Is.EqualTo(FpgReticlePulseState.Hit));
                AssertReticleGeometry(
                    (RectTransform)root.transform,
                    horizontal,
                    vertical,
                    profile.FormalReticle.HitPulseSize);
                reticle.AdvanceFeedback(
                    profile.FormalReticle.HitPulseDuration,
                    false);
                Assert.That(
                    reticle.PulseState,
                    Is.EqualTo(FpgReticlePulseState.None));
                Assert.That(
                    reticle.TargetState,
                    Is.EqualTo(FpgReticleTargetState.Hittable));
                AssertReticleGeometry(
                    (RectTransform)root.transform,
                    horizontal,
                    vertical,
                    profile.FormalReticle.HittableSize);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerHudRefreshesGeometryWhenCurrentOrMaximumChanges()
        {
            GameObject root = null;
            try
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
                ((RectTransform)lifeObject.transform).anchoredPosition =
                    new Vector2(0f, 10f);
                ((RectTransform)barrierObject.transform).anchoredPosition =
                    new Vector2(0f, 70f);
                ((RectTransform)ammoObject.transform).anchoredPosition =
                    new Vector2(0f, 40f);
                float[] authoredSlots = { 10f, 70f, 40f };
                Text lifeText = CreateText(root.transform, "LifeText");
                Text barrierText = CreateText(root.transform, "BarrierText");
                Text ammoText = CreateText(root.transform, "AmmoText");
                Text stateText = CreateText(root.transform, "StateText");

                CombatPresentationProfile profile =
                    AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                        PresentationProfilePath);
                Assert.That(profile, Is.Not.Null, PresentationProfilePath);
                SerializedObject data = new SerializedObject(presenter);
                SetReference(
                    data,
                    "presentationProfile",
                    profile);
                SetReference(data, "lifeBar", life);
                SetReference(data, "barrierBar", barrier);
                SetReference(data, "ammoBar", ammo);
                SetReference(data, "lifeText", lifeText);
                SetReference(data, "barrierText", barrierText);
                SetReference(data, "ammoText", ammoText);
                SetReference(data, "stateText", stateText);
                data.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(presenter.TryPrepare(out string error), Is.True, error);
                AssertHudOrderUsesExistingSlots(
                    profile,
                    life,
                    barrier,
                    ammo,
                    authoredSlots);
                presenter.Refresh(CreateSnapshot(
                    life: 50,
                    maxLife: 100,
                    barrier: 25,
                    maxBarrier: 100,
                    ammo: 2,
                    magazineCapacity: 8));

                AssertBarRatio(life, 0.5f);
                AssertBarRatio(barrier, 0.25f);
                AssertBarRatio(ammo, 0.25f);
                AssertConfiguredValue(
                    profile,
                    FpgHudResourceKind.Life,
                    lifeText,
                    50,
                    100);

                presenter.Refresh(CreateSnapshot(
                    life: 50,
                    maxLife: 200,
                    barrier: 25,
                    maxBarrier: 50,
                    ammo: 2,
                    magazineCapacity: 4));

                AssertBarRatio(life, 0.25f);
                AssertBarRatio(barrier, 0.5f);
                AssertBarRatio(ammo, 0.5f);
                AssertConfiguredValue(
                    profile,
                    FpgHudResourceKind.Life,
                    lifeText,
                    50,
                    200);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormalRoomAndOverheadPrefabBindValidRectBars()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    FpgFormalPlayerHudPresenter presenter =
                        FindSingle<FpgFormalPlayerHudPresenter>(scene);
                    Assert.That(
                        presenter.TryValidate(out string error),
                        Is.True,
                        error);
                    AssertBarRatio(presenter.LifeBar, 0.5f);
                    AssertBarRatio(presenter.BarrierBar, 0.5f);
                    AssertBarRatio(presenter.AmmoBar, 0.5f);
                });

            FpgOverheadHealthBarView prefab =
                AssetDatabase.LoadAssetAtPath<FpgOverheadHealthBarView>(
                    OverheadBarPrefabPath);
            Assert.That(prefab, Is.Not.Null, OverheadBarPrefabPath);

            FpgOverheadHealthBarView instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                Assert.That(
                    instance.TryValidate(out string error),
                    Is.True,
                    error);
                AssertBarRatio(instance.LifeBar, 0.4f);
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.gameObject);
                }
            }
        }

        [Test]
        public void FormalRoomAuthorsOneConfiguredDamageFeedbackBridge()
        {
            FpgDamagePopupView popupPrefab =
                AssetDatabase.LoadAssetAtPath<FpgDamagePopupView>(
                    DamagePopupPrefabPath);
            Assert.That(popupPrefab, Is.Not.Null, DamagePopupPrefabPath);
            Assert.That(
                popupPrefab.TryValidate(out string prefabError),
                Is.True,
                prefabError);
            Assert.That(popupPrefab.transform, Is.TypeOf<RectTransform>());
            Assert.That(popupPrefab.GetComponent<Text>(), Is.Null);
            Assert.That(popupPrefab.Background, Is.Not.Null);
            Assert.That(popupPrefab.DigitsRoot, Is.Not.Null);
            Assert.That(popupPrefab.DigitImages.Count, Is.EqualTo(10));
            CanvasGroup prefabCanvasGroup =
                popupPrefab.GetComponent<CanvasGroup>();
            Assert.That(prefabCanvasGroup, Is.Not.Null);
            Assert.That(prefabCanvasGroup.blocksRaycasts, Is.False);
            Assert.That(popupPrefab.gameObject.activeSelf, Is.False);

            CombatPresentationProfile profile =
                AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                    PresentationProfilePath);
            Assert.That(profile, Is.Not.Null, PresentationProfilePath);
            AssertDamagePopupStyle(
                profile,
                CombatHitPresentationKind.Body,
                HitTipNormalBackgroundPath,
                HitTipArtRoot + "/zi_normal");
            AssertDamagePopupStyle(
                profile,
                CombatHitPresentationKind.Weakpoint,
                HitTipNormalBackgroundPath,
                HitTipArtRoot + "/zi_critcal");
            AssertDamagePopupStyle(
                profile,
                CombatHitPresentationKind.Intercept,
                HitTipElementalBackgroundPath,
                HitTipArtRoot + "/zi_elemental");

            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    FpgFormalCombatFeedbackBridge bridge =
                        FindSingle<FpgFormalCombatFeedbackBridge>(scene);
                    FpgFormalEncounterHost host =
                        FindSingle<FpgFormalEncounterHost>(scene);
                    FpgRoomEncounterDirector director =
                        FindSingle<FpgRoomEncounterDirector>(scene);
                    FpgFormalPlayerTickDriver playerTickDriver =
                        FindSingle<FpgFormalPlayerTickDriver>(scene);
                    CombatAimReticle aimReticle =
                        FindSingle<CombatAimReticle>(scene);
                    Camera worldCamera = FindSingle<Camera>(scene);
                    FpgFormalPlayerHudPresenter hud =
                        FindSingle<FpgFormalPlayerHudPresenter>(scene);
                    Canvas targetCanvas = hud.GetComponent<Canvas>();
                    Assert.That(targetCanvas, Is.Not.Null);
                    Assert.That(hud.PresentationProfile, Is.SameAs(profile));

                    SerializedObject data = new SerializedObject(bridge);
                    Assert.That(
                        GetReference<FpgRoomEncounterDirector>(
                            data,
                            "encounterDirector"),
                        Is.SameAs(director));
                    Assert.That(
                        GetReference<FpgFormalPlayerTickDriver>(
                            data,
                            "playerTickDriver"),
                        Is.SameAs(playerTickDriver));
                    Assert.That(
                        GetReference<CombatAimReticle>(data, "aimReticle"),
                        Is.SameAs(aimReticle));
                    Assert.That(
                        GetReference<CombatPresentationProfile>(
                            data,
                            "presentationProfile"),
                        Is.SameAs(profile));
                    Assert.That(
                        GetReference<Camera>(data, "worldCamera"),
                        Is.SameAs(worldCamera));
                    Assert.That(
                        GetReference<Canvas>(data, "targetCanvas"),
                        Is.SameAs(targetCanvas));
                    Assert.That(
                        GetReference<FpgDamagePopupView>(data, "popupPrefab"),
                        Is.SameAs(popupPrefab));

                    RectTransform popupRoot =
                        GetReference<RectTransform>(data, "popupRoot");
                    Assert.That(popupRoot, Is.Not.Null);
                    Assert.That(popupRoot.parent, Is.SameAs(targetCanvas.transform));
                    Assert.That(popupRoot.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(popupRoot.anchorMax, Is.EqualTo(Vector2.one));
                    Assert.That(popupRoot.offsetMin, Is.EqualTo(Vector2.zero));
                    Assert.That(popupRoot.offsetMax, Is.EqualTo(Vector2.zero));
                    Assert.That(popupRoot.childCount, Is.Zero);
                    Assert.That(bridge.transform, Is.SameAs(host.PresentationRoot));

                    SerializedProperty capacity =
                        data.FindProperty("feedbackReadCapacity");
                    Assert.That(capacity, Is.Not.Null);
                    Assert.That(capacity.intValue, Is.EqualTo(128));
                });
        }

        [Test]
        public void FormalPresentationProfileExplicitlySerializesFormalSystems()
        {
            string yaml = File.ReadAllText(PresentationProfilePath);
            Assert.That(yaml, Does.Contain("formalHudResources:"));
            Assert.That(yaml, Does.Contain("formalDamagePopup:"));
            Assert.That(yaml, Does.Contain("formalReticle:"));

            CombatPresentationProfile profile =
                AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                    PresentationProfilePath);
            Assert.That(profile, Is.Not.Null, PresentationProfilePath);
            Assert.That(
                profile.TryValidateStatic(out string error),
                Is.True,
                error);
        }

        [Test]
        public void FormalFeedbackBridgeRecordsStablePrepareDiagnostics()
        {
            GameObject root = new GameObject(
                "FeedbackBridge",
                typeof(FpgFormalCombatFeedbackBridge));
            root.SetActive(false);
            try
            {
                FpgFormalCombatFeedbackBridge bridge =
                    root.GetComponent<FpgFormalCombatFeedbackBridge>();
                Assert.That(
                    (bool)InvokePrivate(
                        bridge,
                        "TryPrepareWithDiagnostics"),
                    Is.False);
                Assert.That(bridge.PrepareFaultCount, Is.EqualTo(1));
                Assert.That(bridge.LastPrepareError, Is.Not.Empty);

                Assert.That(
                    (bool)InvokePrivate(
                        bridge,
                        "TryPrepareWithDiagnostics"),
                    Is.False);
                Assert.That(
                    bridge.PrepareFaultCount,
                    Is.EqualTo(1),
                    "The same unresolved authoring fault should not spam diagnostics.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormalFeedbackEnableDropsStateMissedWhileDisabled()
        {
            GameObject root = new GameObject(
                "FeedbackBridge",
                typeof(FpgFormalCombatFeedbackBridge));
            root.SetActive(false);
            try
            {
                FpgFormalCombatFeedbackBridge bridge =
                    root.GetComponent<FpgFormalCombatFeedbackBridge>();
                FpgResolvedDamageFeedback[] buffer =
                {
                    CreateFeedback(6L)
                };
                SetPrivateField(bridge, "prepared", true);
                SetPrivateField(bridge, "feedbackBuffer", buffer);
                SetPrivateField(bridge, "damageCursor", 6L);
                SetPrivateField(bridge, "framePositionCount", 1);

                InvokePrivate(bridge, "OnEnable");

                Assert.That(
                    GetPrivateField<long>(bridge, "damageCursor"),
                    Is.Zero);
                Assert.That(
                    GetPrivateField<int>(bridge, "framePositionCount"),
                    Is.Zero);
                Assert.That(buffer[0].Sequence, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormalFeedbackRestartResetsCursorPoolAndReticleState()
        {
            GameObject root = new GameObject(
                "FeedbackBridge",
                typeof(FpgFormalCombatFeedbackBridge));
            root.SetActive(false);
            FpgDamagePopupView popup = null;
            try
            {
                FpgFormalCombatFeedbackBridge bridge =
                    root.GetComponent<FpgFormalCombatFeedbackBridge>();
                GameObject reticleObject = new GameObject(
                    "Reticle",
                    typeof(RectTransform),
                    typeof(CombatAimReticle));
                reticleObject.transform.SetParent(root.transform, false);
                CombatAimReticle reticle =
                    reticleObject.GetComponent<CombatAimReticle>();
                CombatPresentationProfile profile =
                    AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                        PresentationProfilePath);
                Assert.That(
                    reticle.TrySetPresentationProfile(profile, out string error),
                    Is.True,
                    error);
                reticle.SetTargetState(FpgReticleTargetState.Blocked);
                reticle.PresentHit();

                popup = CreateDamagePopup();
                Assert.That(
                    profile.FormalDamagePopup.TryGetSpriteStyle(
                        CombatHitPresentationKind.Body,
                        out FpgDamagePopupSpriteStyle popupStyle),
                    Is.True);
                Assert.That(
                    popup.TryShow(
                        Vector2.zero,
                        1208,
                        popupStyle,
                        1f),
                    Is.True);
                Assert.That(popup.VisibleDigitCount, Is.EqualTo(4));
                int[] digits = { 1, 2, 0, 8 };
                float expectedDigitsWidth =
                    popupStyle.DigitSpacing * (digits.Length - 1);
                for (int index = 0; index < digits.Length; index++)
                {
                    Sprite sprite = popupStyle.GetDigitSprite(digits[index]);
                    Assert.That(
                        popup.DigitImages[index].sprite,
                        Is.SameAs(sprite));
                    expectedDigitsWidth +=
                        popupStyle.DigitHeight
                        * sprite.rect.width
                        / sprite.rect.height;
                }
                float expectedBackgroundWidth = Mathf.Max(
                    popupStyle.BackgroundMinSize.x,
                    expectedDigitsWidth
                    + popupStyle.BackgroundHorizontalPadding * 2f);
                Assert.That(
                    popup.LastDigitsWidth,
                    Is.EqualTo(expectedDigitsWidth).Within(0.001f));
                Assert.That(
                    popup.LastBackgroundWidth,
                    Is.EqualTo(expectedBackgroundWidth).Within(0.001f));
                Assert.That(
                    popup.RectTransform.sizeDelta.x,
                    Is.EqualTo(expectedBackgroundWidth).Within(0.001f));
                SetPrivateField(
                    bridge,
                    "aimReticle",
                    reticle);
                SetPrivateField(
                    bridge,
                    "popupPool",
                    new[] { popup });
                SetPrivateField(bridge, "damageCursor", 19L);
                SetPrivateField(bridge, "framePositionCount", 2);
                SetPrivateField(
                    bridge,
                    "feedbackBuffer",
                    new[] { CreateFeedback(1L), CreateFeedback(2L) });

                InvokePrivate(
                    bridge,
                    "HandleEncounterLifecycle",
                    new FpgEncounterLifecycleEvent(
                        FpgEncounterLifecycleEventType.Restarted,
                        new TickIndex(10L),
                        FpgEncounterPhase.Combat));

                Assert.That(GetPrivateField<long>(bridge, "damageCursor"), Is.Zero);
                Assert.That(
                    GetPrivateField<int>(bridge, "framePositionCount"),
                    Is.Zero);
                Assert.That(popup.IsActive, Is.False);
                Assert.That(
                    reticle.PulseState,
                    Is.EqualTo(FpgReticlePulseState.None));
                Assert.That(
                    reticle.TargetState,
                    Is.EqualTo(FpgReticleTargetState.Idle));
                FpgResolvedDamageFeedback[] buffer =
                    GetPrivateField<FpgResolvedDamageFeedback[]>(
                        bridge,
                        "feedbackBuffer");
                Assert.That(buffer[0].Sequence, Is.Zero);
                Assert.That(buffer[1].Sequence, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (popup != null)
                {
                    UnityEngine.Object.DestroyImmediate(popup.gameObject);
                }
            }
        }

        [Test]
        public void FormalFeedbackGapDropsRetainedTailAndAdvancesCursor()
        {
            GameObject root = new GameObject(
                "FeedbackBridge",
                typeof(FpgFormalCombatFeedbackBridge));
            root.SetActive(false);
            try
            {
                FpgFormalCombatFeedbackBridge bridge =
                    root.GetComponent<FpgFormalCombatFeedbackBridge>();
                FpgResolvedDamageFeedback[] buffer =
                {
                    CreateFeedback(4L),
                    CreateFeedback(5L)
                };
                SetPrivateField(bridge, "feedbackBuffer", buffer);
                SetPrivateField(bridge, "damageCursor", 3L);
                SetPrivateField(bridge, "framePositionCount", 2);

                InvokePrivate(
                    bridge,
                    "DiscardFeedbackBatch",
                    new StubDamageFeedbackView(8L),
                    buffer.Length);

                Assert.That(bridge.FeedbackGapCount, Is.EqualTo(1));
                Assert.That(
                    GetPrivateField<long>(bridge, "damageCursor"),
                    Is.EqualTo(8L));
                Assert.That(
                    GetPrivateField<int>(bridge, "framePositionCount"),
                    Is.Zero);
                Assert.That(buffer[0].Sequence, Is.Zero);
                Assert.That(buffer[1].Sequence, Is.Zero);
                Assert.That(bridge.ActivePopupCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FormalFeedbackPoolCleanupLeavesRetryStateEmpty()
        {
            GameObject root = new GameObject(
                "FeedbackBridge",
                typeof(FpgFormalCombatFeedbackBridge));
            root.SetActive(false);
            FpgDamagePopupView popup = null;
            try
            {
                FpgFormalCombatFeedbackBridge bridge =
                    root.GetComponent<FpgFormalCombatFeedbackBridge>();
                popup = CreateDamagePopup();
                SetPrivateField(bridge, "popupPool", new[] { popup });
                SetPrivateField(
                    bridge,
                    "feedbackBuffer",
                    new FpgResolvedDamageFeedback[2]);
                SetPrivateField(
                    bridge,
                    "framePositions",
                    new Vector2[2]);
                SetPrivateField(bridge, "prepared", true);

                InvokePrivate(bridge, "DestroyPreparedPool");

                Assert.That(popup == null, Is.True);
                Assert.That(
                    GetPrivateField<FpgDamagePopupView[]>(bridge, "popupPool"),
                    Is.Empty);
                Assert.That(
                    GetPrivateField<FpgResolvedDamageFeedback[]>(
                        bridge,
                        "feedbackBuffer"),
                    Is.Empty);
                Assert.That(
                    GetPrivateField<Vector2[]>(bridge, "framePositions"),
                    Is.Empty);
                Assert.That(
                    GetPrivateField<bool>(bridge, "prepared"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (popup != null)
                {
                    UnityEngine.Object.DestroyImmediate(popup.gameObject);
                }
            }
        }

        [Test]
        public void FormalAttackQuerySceneDataContainsOnlyTechnicalMasks()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    FpgFormalCombatPortFactory factory =
                        FindSingle<FpgFormalCombatPortFactory>(scene);
                    SerializedObject data = new SerializedObject(factory);
                    Assert.That(data.FindProperty("attackQuerySettings"), Is.Null);
                    SerializedProperty technical =
                        data.FindProperty("attackQueryTechnicalSettings");
                    Assert.That(technical, Is.Not.Null);
                    Assert.That(
                        technical.FindPropertyRelative("maxDistance"),
                        Is.Null);
                    Assert.That(
                        technical.FindPropertyRelative("primarySpreadTangent"),
                        Is.Null);
                    Assert.That(
                        technical.FindPropertyRelative("secondaryAreaRadius"),
                        Is.Null);
                    int hitboxMask = technical
                        .FindPropertyRelative("hitboxLayerMask").intValue;
                    int blockerMask = technical
                        .FindPropertyRelative("blockerLayerMask").intValue;
                    Assert.That(hitboxMask, Is.Not.Zero);
                    Assert.That(blockerMask, Is.Not.Zero);
                    Assert.That(hitboxMask & blockerMask, Is.Zero);

                    D0ThreeCProfile threeC =
                        AssetDatabase.LoadAssetAtPath<D0ThreeCProfile>(
                            ThreeCProfilePath);
                    D0CombatFeelProfile feel =
                        AssetDatabase.LoadAssetAtPath<D0CombatFeelProfile>(
                            CombatFeelProfilePath);
                    Assert.That(threeC, Is.Not.Null, ThreeCProfilePath);
                    Assert.That(feel, Is.Not.Null, CombatFeelProfilePath);
                    Assert.That(
                        feel.TryCreateAttackQuerySettings(
                            threeC,
                            new UnityAttackQueryTechnicalSettings(
                                hitboxMask,
                                blockerMask),
                            out UnityAttackQuerySettings composed,
                            out string error),
                        Is.True,
                        error);
                    Assert.That(
                        composed.MaxDistance,
                        Is.EqualTo(threeC.MaximumAimDistance));
                    Assert.That(
                        composed.PrimarySpreadTangent,
                        Is.EqualTo(feel.PrimaryBaseSpreadTangent));
                    Assert.That(
                        composed.SecondaryAreaRadius,
                        Is.EqualTo(feel.SecondaryAreaRadius));
                });
        }

        [Test]
        public void FormalRoomOnlySerializesTechnicalAttackQuerySettings()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    FpgFormalCombatPortFactory factory =
                        FindSingle<FpgFormalCombatPortFactory>(scene);
                    SerializedObject data = new SerializedObject(factory);
                    Assert.That(
                        data.FindProperty("attackQuerySettings"),
                        Is.Null,
                        "FormalRoom must not serialize profile-owned aim feel.");

                    SerializedProperty technical =
                        data.FindProperty("attackQueryTechnicalSettings");
                    Assert.That(technical, Is.Not.Null);
                    SerializedProperty hitbox =
                        technical.FindPropertyRelative("hitboxLayerMask");
                    SerializedProperty blocker =
                        technical.FindPropertyRelative("blockerLayerMask");
                    Assert.That(hitbox, Is.Not.Null);
                    Assert.That(blocker, Is.Not.Null);
                    Assert.That(hitbox.intValue, Is.EqualTo(1 << 29));
                    Assert.That(blocker.intValue, Is.EqualTo(1 << 28));
                });
        }

        [TestCase("FPG_Burstbug_Behavior.asset")]
        [TestCase("FPG_Hudie_Behavior.asset")]
        [TestCase("FPG_Luan_Behavior.asset")]
        public void FormalBehaviorAssetResolvesBehaviorDefinitionScript(
            string assetName)
        {
            string path =
                "Assets/FPGDemo/Config/FormalEncounter/" + assetName;
            FpgEnemyBehaviorDefinition definition =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(path);
            Assert.That(definition, Is.Not.Null, path);
            Assert.That(
                definition.TryValidate(out string error),
                Is.True,
                error);

            MonoScript script = MonoScript.FromScriptableObject(definition);
            Assert.That(script, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(script),
                Is.EqualTo(BehaviorScriptPath));
            Assert.That(script.GetClass(), Is.EqualTo(typeof(FpgEnemyBehaviorDefinition)));
        }

        private static FpgFormalPlayerPresentationSnapshot CreateSnapshot(
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            int ammo,
            int magazineCapacity)
        {
            return new FpgFormalPlayerPresentationSnapshot(
                new TickIndex(1L),
                new RuntimeId(1L),
                FpgEncounterPhase.Combat,
                false,
                life,
                maxLife,
                barrier,
                maxBarrier,
                ammo,
                magazineCapacity,
                PlayerExposureState.Exposed,
                WeaponState.Ready);
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

        private static void AssertReticleGeometry(
            RectTransform root,
            RectTransform horizontal,
            RectTransform vertical,
            float configuredSize)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(root.sizeDelta.x, Is.EqualTo(configuredSize));
            Assert.That(root.sizeDelta.y, Is.EqualTo(configuredSize));
            Assert.That(
                horizontal.rect.width,
                Is.EqualTo(configuredSize).Within(0.001f));
            Assert.That(horizontal.rect.height, Is.EqualTo(2f).Within(0.001f));
            Assert.That(vertical.rect.width, Is.EqualTo(2f).Within(0.001f));
            Assert.That(
                vertical.rect.height,
                Is.EqualTo(configuredSize).Within(0.001f));
        }

        private static void AssertHudOrderUsesExistingSlots(
            CombatPresentationProfile profile,
            FpgFormalBarView life,
            FpgFormalBarView barrier,
            FpgFormalBarView ammo,
            float[] authoredSlots)
        {
            Assert.That(
                profile.TryGetFormalHudResource(
                    FpgHudResourceKind.Life,
                    out FpgHudResourcePresentation lifePresentation),
                Is.True);
            Assert.That(
                profile.TryGetFormalHudResource(
                    FpgHudResourceKind.Barrier,
                    out FpgHudResourcePresentation barrierPresentation),
                Is.True);
            Assert.That(
                profile.TryGetFormalHudResource(
                    FpgHudResourceKind.Ammo,
                    out FpgHudResourcePresentation ammoPresentation),
                Is.True);

            float[] sortedSlots = (float[])authoredSlots.Clone();
            Array.Sort(sortedSlots);
            AssertResourceSlot(
                life,
                lifePresentation.Order,
                barrierPresentation.Order,
                ammoPresentation.Order,
                sortedSlots);
            AssertResourceSlot(
                barrier,
                barrierPresentation.Order,
                lifePresentation.Order,
                ammoPresentation.Order,
                sortedSlots);
            AssertResourceSlot(
                ammo,
                ammoPresentation.Order,
                lifePresentation.Order,
                barrierPresentation.Order,
                sortedSlots);
        }

        private static void AssertResourceSlot(
            FpgFormalBarView bar,
            int order,
            int otherOrderA,
            int otherOrderB,
            float[] sortedSlots)
        {
            int rank = 0;
            if (otherOrderA < order)
            {
                rank++;
            }
            if (otherOrderB < order)
            {
                rank++;
            }

            float expected = sortedSlots[sortedSlots.Length - 1 - rank];
            Assert.That(
                ((RectTransform)bar.transform).anchoredPosition.y,
                Is.EqualTo(expected).Within(0.001f));
        }

        private static void AssertConfiguredValue(
            CombatPresentationProfile profile,
            FpgHudResourceKind kind,
            Text text,
            int current,
            int maximum)
        {
            Assert.That(
                profile.TryGetFormalHudResource(
                    kind,
                    out FpgHudResourcePresentation presentation),
                Is.True);
            string expected = presentation.Label + " " + string.Format(
                CultureInfo.InvariantCulture,
                presentation.ValueFormat,
                current,
                maximum);
            Assert.That(text.text, Is.EqualTo(expected));
        }

        private static FpgDamagePopupView CreateDamagePopup()
        {
            GameObject root = new GameObject(
                "DamagePopup",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(FpgDamagePopupView));
            GameObject backgroundObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(Image));
            backgroundObject.transform.SetParent(root.transform, false);
            GameObject digitsObject = new GameObject(
                "Digits",
                typeof(RectTransform));
            digitsObject.transform.SetParent(root.transform, false);
            Image[] digitImages = new Image[10];
            for (int index = 0; index < digitImages.Length; index++)
            {
                GameObject digitObject = new GameObject(
                    "Digit_" + index,
                    typeof(RectTransform),
                    typeof(Image));
                digitObject.transform.SetParent(digitsObject.transform, false);
                digitImages[index] = digitObject.GetComponent<Image>();
                digitObject.SetActive(false);
            }

            FpgDamagePopupView view =
                root.GetComponent<FpgDamagePopupView>();
            SerializedObject data = new SerializedObject(view);
            SetReference(data, "root", (RectTransform)root.transform);
            SetReference(data, "background", backgroundObject.GetComponent<Image>());
            SetReference(data, "digitsRoot", (RectTransform)digitsObject.transform);
            SerializedProperty digitReferences = data.FindProperty("digitImages");
            Assert.That(digitReferences, Is.Not.Null);
            digitReferences.arraySize = digitImages.Length;
            for (int index = 0; index < digitImages.Length; index++)
            {
                digitReferences.GetArrayElementAtIndex(index)
                    .objectReferenceValue = digitImages[index];
            }
            SetReference(data, "canvasGroup", root.GetComponent<CanvasGroup>());
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(view.TryValidate(out string error), Is.True, error);
            return view;
        }

        private static void AssertDamagePopupStyle(
            CombatPresentationProfile profile,
            CombatHitPresentationKind kind,
            string backgroundPath,
            string digitsFolder)
        {
            Assert.That(
                profile.FormalDamagePopup.TryGetSpriteStyle(
                    kind,
                    out FpgDamagePopupSpriteStyle style),
                Is.True,
                kind.ToString());
            Assert.That(
                AssetDatabase.GetAssetPath(style.BackgroundSprite),
                Is.EqualTo(backgroundPath));
            Assert.That(style.DigitHeight, Is.EqualTo(60f).Within(0.001f));
            Assert.That(style.DigitSpacing, Is.EqualTo(-2f).Within(0.001f));
            Assert.That(
                style.BackgroundHorizontalPadding,
                Is.EqualTo(34f).Within(0.001f));
            Assert.That(
                style.BackgroundMinSize,
                Is.EqualTo(new Vector2(133f, 50f)));
            for (int digit = 0; digit < 10; digit++)
            {
                Assert.That(
                    AssetDatabase.GetAssetPath(style.GetDigitSprite(digit)),
                    Is.EqualTo(digitsFolder + "/" + digit + ".png"));
            }
        }

        private static FpgResolvedDamageFeedback CreateFeedback(long sequence)
        {
            ImpactId impactId = new ImpactId(sequence);
            ImpactIntent intent = new ImpactIntent(
                impactId,
                new AttackId(sequence),
                new ShotId(sequence),
                new RuntimeId(1L),
                new RuntimeId(2L),
                new TickIndex(sequence),
                new DamageSpec(5, 0),
                HitPart.Body,
                DamageType.Normal,
                CombatTags.Primary,
                impactOrdinal: (int)sequence - 1);
            return new FpgResolvedDamageFeedback(
                sequence,
                intent,
                new DamagePacket(
                    impactId,
                    DamageChannel.Life,
                    5,
                    0,
                    100,
                    95),
                false);
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
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

        private static T GetPrivateField<T>(
            object target,
            string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void AssertBarRatio(
            FpgFormalBarView bar,
            float expected)
        {
            Assert.That(bar, Is.Not.Null);
            Assert.That(bar.SetNormalizedValue(expected), Is.True);
            Canvas.ForceUpdateCanvases();

            RectTransform fill = bar.FillRect;
            RectTransform fillArea = fill.parent as RectTransform;
            Assert.That(fillArea, Is.Not.Null);
            Assert.That(fill.anchorMin.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(fill.anchorMax.x, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(
                fill.rect.width,
                Is.EqualTo(fillArea.rect.width * expected).Within(0.001f));
        }

        private static void SetReference(
            SerializedObject data,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = data.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.objectReferenceValue = value;
        }

        private static T GetReference<T>(
            SerializedObject data,
            string propertyName)
            where T : UnityEngine.Object
        {
            SerializedProperty property = data.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            T value = property.objectReferenceValue as T;
            Assert.That(value, Is.Not.Null, propertyName);
            return value;
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T result = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T[] values = roots[index].GetComponentsInChildren<T>(true);
                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    Assert.That(result, Is.Null, typeof(T).Name);
                    result = values[valueIndex];
                }
            }

            Assert.That(result, Is.Not.Null, typeof(T).Name);
            return result;
        }

        private static void WithPreviewScene(
            string scenePath,
            Action<Scene> assertion)
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
            Assert.That(scene.IsValid(), Is.True, scenePath);
            try
            {
                assertion(scene);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private sealed class StubDamageFeedbackView :
            IFpgResolvedDamageFeedbackView
        {
            public StubDamageFeedbackView(long lastSequence)
            {
                LastSequence = lastSequence;
            }

            public int Capacity => 2;
            public int DroppedEventCount => 1;
            public long FirstRetainedSequence => LastSequence - 1L;
            public long LastSequence { get; }

            public int CopyAfter(
                long lastSeenSequence,
                FpgResolvedDamageFeedback[] output,
                out bool hasGap)
            {
                hasGap = true;
                return 0;
            }
        }
    }
}
