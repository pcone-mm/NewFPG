using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatHud2DPresenterTests
    {
        private const string ProfilePath =
            "Assets/FPGDemo/Config/D0Slice/CombatPresentationProfile.asset";

        [Test]
        public void RefreshFormatsFormalReadouts_SelectsHighestPriorityThreat_AndMovesDevTextToOverlay()
        {
            using (HudFixture fixture = new HudFixture())
            {
                ScenarioDefinition definition = CombatLabHarness.CreateScenario();
                FinalSnapshot running = CreateSnapshot(
                    definition,
                    BattleSessionState.Running,
                    BattleCompletionReason.None);
                ThreatSnapshot[] threats =
                {
                    CreateThreat(1L, CombatPresentationProfile.FastThreatPresentationKey),
                    CreateThreat(2L, CombatPresentationProfile.InterceptableVolleyThreatPresentationKey),
                    CreateThreat(3L, CombatPresentationProfile.HeavyWeakpointThreatPresentationKey)
                };

                fixture.Hud.Refresh(running, definition, threats, threats.Length, new TickIndex(25L));

                Assert.That(fixture.EnemyName.text, Is.EqualTo("BURSTBUG"));
                Assert.That(fixture.PlayerName.text, Is.EqualTo("FEI_30048"));
                Assert.That(fixture.PlayerLife.text, Is.EqualTo("LIFE 100 / 100"));
                Assert.That(fixture.PlayerBarrier.text, Is.EqualTo("BARRIER 60 / 60"));
                Assert.That(fixture.Ammo.text, Is.EqualTo("AMMO 8 / 8"));
                Assert.That(fixture.EnemyLife.text, Is.EqualTo("HP 120 / 120"));
                Assert.That(fixture.EnemyBreak.text, Is.EqualTo("BREAK 40 / 40"));
                Assert.That(fixture.PlayerLifeFill.fillAmount, Is.EqualTo(1f));
                Assert.That(fixture.EnemyLifeFill.fillAmount, Is.EqualTo(1f));
                Assert.That(fixture.Hud.CurrentThreatLabel, Is.EqualTo("WEAKPOINT | BREAK"));
                Assert.That(fixture.Hud.IsDevelopmentOverlayVisible, Is.False);
                Assert.That(fixture.DevelopmentOverlay.activeSelf, Is.False);
                fixture.Hud.Advance(0f, false);
                Assert.That(fixture.ActionFill.fillAmount, Is.EqualTo(1f));

                fixture.Hud.ConsumeCombatTrace(
                    CreateWeaponEvent(
                        CombatEventType.ReloadStarted,
                        WeaponState.Ready,
                        WeaponState.Reloading),
                    fixture.PlayerRuntimeId);
                fixture.Hud.Refresh(running, definition, threats, threats.Length, new TickIndex(26L));
                Assert.That(fixture.Hud.CurrentActionLabel, Is.EqualTo("RELOADING"));
                fixture.Hud.Advance(1f, false);
                Assert.That(
                    fixture.ActionFill.fillAmount,
                    Is.EqualTo(1f),
                    "The action surface is a static state light until the weapon exposes real phase progress.");

                fixture.Hud.ConsumeCombatTrace(
                    CreateWeaponEvent(
                        CombatEventType.InputAccepted,
                        WeaponState.Ready,
                        WeaponState.AltCharging),
                    fixture.PlayerRuntimeId);
                fixture.Hud.Refresh(running, definition, threats, threats.Length, new TickIndex(27L));
                Assert.That(fixture.Hud.CurrentActionLabel, Is.EqualTo("CHARGING"));

                fixture.Hud.SetDevelopmentOverlayVisible(true);
                Assert.That(fixture.Hud.IsDevelopmentOverlayVisible, Is.True);
                Assert.That(fixture.DevelopmentOverlay.activeSelf, Is.True);
                Assert.That(fixture.DevelopmentText.text, Does.Contain("STATE: RUNNING"));
                Assert.That(fixture.DevelopmentText.text, Does.Contain(CombatHud2DPresenter.DevelopmentPrompt));

                fixture.Hud.Clear();
                Assert.That(fixture.Hud.CurrentThreatLabel, Is.EqualTo("THREAT | CLEAR"));
                Assert.That(fixture.Hud.CurrentActionLabel, Is.EqualTo("READY"));
                Assert.That(fixture.Threat.color, Is.EqualTo(new Color(0.66f, 0.88f, 0.94f, 0.92f)));
                Assert.That(fixture.Action.color, Is.EqualTo(new Color(0.68f, 1f, 0.85f, 1f)));
            }
        }

        [Test]
        public void TerminalOutcomeLatchesUntilClear_AndDefeatUsesStrongDim()
        {
            using (HudFixture victoryFixture = new HudFixture())
            using (HudFixture defeatFixture = new HudFixture())
            {
                ScenarioDefinition definition = CombatLabHarness.CreateScenario();
                FinalSnapshot victory = CreateSnapshot(
                    definition,
                    BattleSessionState.Completed,
                    BattleCompletionReason.Victory);
                FinalSnapshot defeat = CreateSnapshot(
                    definition,
                    BattleSessionState.Completed,
                    BattleCompletionReason.Defeat);

                victoryFixture.Hud.Refresh(victory, definition, null, 0, new TickIndex(64L));
                victoryFixture.Hud.Advance(0.25f, false);
                Assert.That(victoryFixture.Hud.IsTerminalLatched, Is.True);
                Assert.That(victoryFixture.Hud.IsTerminalPanelVisible, Is.True);
                Assert.That(victoryFixture.Hud.TerminalReason, Is.EqualTo(BattleCompletionReason.Victory));
                Assert.That(victoryFixture.TerminalTitle.text, Is.EqualTo("VICTORY"));
                Assert.That(victoryFixture.ScreenFx.IsShowing, Is.True);
                Assert.That(victoryFixture.ScreenFx.CurrentAlpha, Is.EqualTo(1f).Within(0.001f));

                victoryFixture.Hud.Refresh(defeat, definition, null, 0, new TickIndex(65L));
                Assert.That(victoryFixture.Hud.TerminalReason, Is.EqualTo(BattleCompletionReason.Victory));
                Assert.That(victoryFixture.TerminalTitle.text, Is.EqualTo("VICTORY"));

                defeatFixture.Hud.Refresh(defeat, definition, null, 0, new TickIndex(64L));
                defeatFixture.Hud.Advance(0.25f, false);
                Assert.That(defeatFixture.Hud.IsTerminalLatched, Is.True);
                Assert.That(defeatFixture.Hud.TerminalReason, Is.EqualTo(BattleCompletionReason.Defeat));
                Assert.That(defeatFixture.TerminalTitle.text, Is.EqualTo("DEFEAT"));
                Assert.That(defeatFixture.ScreenFx.IsShowing, Is.True);
                Assert.That(
                    defeatFixture.ScreenFx.DimmingColor.a,
                    Is.GreaterThan(victoryFixture.ScreenFx.DimmingColor.a));

                defeatFixture.Hud.Clear();
                Assert.That(defeatFixture.Hud.IsTerminalLatched, Is.False);
                Assert.That(defeatFixture.Hud.IsTerminalPanelVisible, Is.False);
                Assert.That(defeatFixture.ScreenFx.IsShowing, Is.False);
                Assert.That(defeatFixture.ScreenFx.CurrentAlpha, Is.Zero);
            }
        }

        [Test]
        public void RefreshUsesIntermediateSnapshotValuesForEveryCombatReadout()
        {
            using (HudFixture fixture = new HudFixture())
            {
                ScenarioDefinition definition = CombatLabHarness.CreateScenario();
                FinalSnapshot half = new FinalSnapshot(
                    BattleSessionState.Running,
                    BattleCompletionReason.None,
                    8L,
                    definition.PlayerLife / 2,
                    definition.PlayerBarrier / 2,
                    definition.PlayerWeapon.MagazineCapacity / 2,
                    definition.EnemyLife / 2,
                    definition.EnemyBreak / 2,
                    EnemyControlState.Active,
                    0,
                    0);

                fixture.Hud.Refresh(half, definition, null, 0, new TickIndex(8L));

                Assert.That(fixture.PlayerLife.text, Is.EqualTo("LIFE 50 / 100"));
                Assert.That(fixture.PlayerBarrier.text, Is.EqualTo("BARRIER 30 / 60"));
                Assert.That(fixture.Ammo.text, Is.EqualTo("AMMO 4 / 8"));
                Assert.That(fixture.EnemyLife.text, Is.EqualTo("HP 60 / 120"));
                Assert.That(fixture.EnemyBreak.text, Is.EqualTo("BREAK 20 / 40"));
                Assert.That(fixture.PlayerLifeFill.fillAmount, Is.EqualTo(0.5f));
                Assert.That(fixture.PlayerBarrierFill.fillAmount, Is.EqualTo(0.5f));
                Assert.That(fixture.AmmoFill.fillAmount, Is.EqualTo(0.5f));
                Assert.That(fixture.EnemyLifeFill.fillAmount, Is.EqualTo(0.5f));
                Assert.That(fixture.EnemyBreakFill.fillAmount, Is.EqualTo(0.5f));
            }
        }

        [Test]
        public void CoordinatorRefreshesD0HudWhenLegacyFeedbackIsNotPrepared()
        {
            using (HudFixture fixture = new HudFixture())
            {
                GameObject root = new GameObject("D0HudCoordinatorRoot");
                GameObject hostObject = new GameObject("D0HudCoordinatorHost");
                GameObject contextObject = new GameObject("D0HudCoordinatorContext");
                GameObject cameraObject = new GameObject("D0HudCoordinatorCamera");
                GameObject prefabObject = CreateProjectileViewPrefab("D0HudProjectileView");
                BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
                BattlePresentationCoordinator coordinator = root.AddComponent<BattlePresentationCoordinator>();
                BattleSceneContext context = contextObject.AddComponent<BattleSceneContext>();
                Camera camera = cameraObject.AddComponent<Camera>();
                BattlePresentationCatalog catalog = CreateProjectileCatalog(
                    prefabObject.GetComponent<ProjectileView>());
                ScenarioDefinition definition = CombatLabHarness.CreateScenario(projectileCapacity: 1);
                FixedProjectilePresentationFeed feed = new FixedProjectilePresentationFeed(1);
                BattleSession session = null;
                try
                {
                    Bind(coordinator, "sessionHost", host);
                    Bind(context, "mainCamera", camera);
                    SetHostContext(host, context);

                    Assert.That(
                        coordinator.TryPrepare(definition, catalog, root.transform, out string prepareError),
                        Is.True,
                        prepareError);
                    SetPrivateField(coordinator, "d0CombatHud2DPresenter", fixture.Hud);

                    session = new BattleSessionFactory().Create(
                        definition,
                        new NullAttackResolutionPort(),
                        null,
                        new NullProjectileWorldPort());
                    Assert.That(
                        session.ApplyControl(new SessionControlCommand(
                            new ControlSequence(1L),
                            SessionControlCommandType.Start)).IsSuccess,
                        Is.True);
                    Assert.That(coordinator.TryBind(session, feed, out string bindError), Is.True, bindError);
                    Assert.That(coordinator.IsFeedbackPrepared, Is.False);

                    InvokePrivate(coordinator, "LateUpdate");
                    Assert.That(fixture.Ammo.text, Is.EqualTo("AMMO 8 / 8"));

                    CombatLabHarness.PumpOneTick(
                        session,
                        tick => PlayerInputFrame.Empty(tick, primaryHeld: true));
                    InvokePrivate(coordinator, "LateUpdate");

                    Assert.That(fixture.Ammo.text, Is.EqualTo("AMMO 7 / 8"));
                    Assert.That(fixture.AmmoFill.fillAmount, Is.EqualTo(0.875f));
                    Assert.That(coordinator.PresentationFaultCount, Is.Zero);
                }
                finally
                {
                    session?.Dispose();
                    Object.DestroyImmediate(catalog);
                    Object.DestroyImmediate(prefabObject);
                    Object.DestroyImmediate(cameraObject);
                    Object.DestroyImmediate(contextObject);
                    Object.DestroyImmediate(hostObject);
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static FinalSnapshot CreateSnapshot(
            ScenarioDefinition definition,
            BattleSessionState state,
            BattleCompletionReason completionReason)
        {
            return new FinalSnapshot(
                state,
                completionReason,
                0L,
                definition.PlayerLife,
                definition.PlayerBarrier,
                definition.PlayerWeapon.MagazineCapacity,
                definition.EnemyLife,
                definition.EnemyBreak,
                EnemyControlState.Active,
                0,
                0);
        }

        private static ThreatSnapshot CreateThreat(long runtimeId, int presentationKey)
        {
            return new ThreatSnapshot(
                new RuntimeId(runtimeId),
                (int)runtimeId,
                ThreatState.Windup,
                AttackId.Invalid,
                new TickIndex(100L),
                false,
                false,
                ThreatPayloadKind.SweptProjectile,
                presentationKey,
                ThreatTargetPolicy.PlayerCombatant);
        }

        private static CombatEvent CreateWeaponEvent(
            CombatEventType eventType,
            WeaponState before,
            WeaponState after)
        {
            return new CombatEvent(
                1L,
                new TickIndex(1L),
                eventType,
                HudFixture.PlayerId,
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                (int)before,
                (int)after,
                RejectReason.None,
                0UL,
                DamageChannel.None,
                0,
                false);
        }

        private static GameObject CreateProjectileViewPrefab(string name)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gameObject.name = name;
            Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            gameObject.AddComponent<ProjectileView>();
            return gameObject;
        }

        private static BattlePresentationCatalog CreateProjectileCatalog(ProjectileView prefab)
        {
            BattlePresentationCatalog catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("projectileEntries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("presentationKey").intValue = 1;
            entry.FindPropertyRelative("viewPrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("prewarmCapacity").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static void SetHostContext(BattleSessionHost host, BattleSceneContext context)
        {
            FieldInfo contextField = typeof(BattleSessionHost).GetField(
                "<Context>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(contextField, Is.Not.Null);
            contextField.SetValue(host, context);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void Bind(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing serialized property '{propertyName}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class HudFixture : System.IDisposable
        {
            public static readonly RuntimeId PlayerId = new RuntimeId(701L);

            private readonly GameObject root;

            public HudFixture()
            {
                CombatPresentationProfile profile = AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                    ProfilePath);
                Assert.That(profile, Is.Not.Null, "The installed D0 profile is required for HUD contract tests.");
                root = new GameObject("CombatHud2DPresenterFixture", typeof(RectTransform));

                GameObject screenFxObject = CreateUiObject(root.transform, "ScreenFx");
                CanvasGroup screenGroup = screenFxObject.AddComponent<CanvasGroup>();
                Image dimming = CreateImage(screenFxObject.transform, "Dimming");
                ScreenFx = screenFxObject.AddComponent<D0TerminalScreenFxPresenter>();
                Bind(ScreenFx, "canvasGroup", screenGroup);
                Bind(ScreenFx, "dimmingImage", dimming);

                GameObject terminalPanel = CreateUiObject(root.transform, "TerminalPanel");
                CanvasGroup terminalGroup = terminalPanel.AddComponent<CanvasGroup>();
                TerminalTitle = CreateText(terminalPanel.transform, "TerminalTitle");
                Text terminalPrompt = CreateText(terminalPanel.transform, "TerminalPrompt");

                DevelopmentOverlay = CreateUiObject(root.transform, "DevelopmentOverlay");
                DevelopmentText = CreateText(DevelopmentOverlay.transform, "DevelopmentText");
                BattleSessionDiagnosticsPresenter diagnostics = root.AddComponent<BattleSessionDiagnosticsPresenter>();

                GameObject hudObject = CreateUiObject(root.transform, "Hud");
                Hud = hudObject.AddComponent<CombatHud2DPresenter>();
                EnemyLifeFill = CreateImage(hudObject.transform, "EnemyLifeFill");
                EnemyBreakFill = CreateImage(hudObject.transform, "EnemyBreakFill");
                Image threatIndicator = CreateImage(hudObject.transform, "ThreatIndicator");
                EnemyName = CreateText(hudObject.transform, "EnemyName");
                EnemyLife = CreateText(hudObject.transform, "EnemyLife");
                EnemyBreak = CreateText(hudObject.transform, "EnemyBreak");
                Threat = CreateText(hudObject.transform, "Threat");
                PlayerLifeFill = CreateImage(hudObject.transform, "PlayerLifeFill");
                PlayerBarrierFill = CreateImage(hudObject.transform, "PlayerBarrierFill");
                AmmoFill = CreateImage(hudObject.transform, "AmmoFill");
                ActionFill = CreateImage(hudObject.transform, "ActionFill");
                PlayerName = CreateText(hudObject.transform, "PlayerName");
                PlayerLife = CreateText(hudObject.transform, "PlayerLife");
                PlayerBarrier = CreateText(hudObject.transform, "PlayerBarrier");
                Ammo = CreateText(hudObject.transform, "Ammo");
                Action = CreateText(hudObject.transform, "Action");

                Bind(Hud, "presentationProfile", profile);
                Bind(Hud, "enemyLifeFill", EnemyLifeFill);
                Bind(Hud, "enemyBreakFill", EnemyBreakFill);
                Bind(Hud, "threatIndicator", threatIndicator);
                Bind(Hud, "enemyNameText", EnemyName);
                Bind(Hud, "enemyLifeText", EnemyLife);
                Bind(Hud, "enemyBreakText", EnemyBreak);
                Bind(Hud, "threatText", Threat);
                Bind(Hud, "playerLifeFill", PlayerLifeFill);
                Bind(Hud, "playerBarrierFill", PlayerBarrierFill);
                Bind(Hud, "ammoFill", AmmoFill);
                Bind(Hud, "actionFill", ActionFill);
                Bind(Hud, "playerNameText", PlayerName);
                Bind(Hud, "playerLifeText", PlayerLife);
                Bind(Hud, "playerBarrierText", PlayerBarrier);
                Bind(Hud, "ammoText", Ammo);
                Bind(Hud, "actionText", Action);
                Bind(Hud, "terminalPanel", terminalPanel);
                Bind(Hud, "terminalCanvasGroup", terminalGroup);
                Bind(Hud, "terminalTitleText", TerminalTitle);
                Bind(Hud, "terminalPromptText", terminalPrompt);
                Bind(Hud, "terminalScreenFx", ScreenFx);
                Bind(Hud, "developmentOverlay", DevelopmentOverlay);
                Bind(Hud, "developmentText", DevelopmentText);
                Bind(Hud, "diagnosticsPresenter", diagnostics);
                Assert.That(Hud.TryPrepare(out string error), Is.True, error);
            }

            public CombatHud2DPresenter Hud { get; }
            public D0TerminalScreenFxPresenter ScreenFx { get; }
            public Text EnemyName { get; }
            public Text EnemyLife { get; }
            public Text EnemyBreak { get; }
            public Text PlayerName { get; }
            public Text PlayerLife { get; }
            public Text PlayerBarrier { get; }
            public Text Ammo { get; }
            public Text TerminalTitle { get; }
            public Text DevelopmentText { get; }
            public Text Threat { get; }
            public Text Action { get; }
            public Image EnemyLifeFill { get; }
            public Image EnemyBreakFill { get; }
            public Image PlayerLifeFill { get; }
            public Image PlayerBarrierFill { get; }
            public Image AmmoFill { get; }
            public Image ActionFill { get; }
            public GameObject DevelopmentOverlay { get; }
            public RuntimeId PlayerRuntimeId => PlayerId;

            public void Dispose()
            {
                Object.DestroyImmediate(root);
            }

            private static GameObject CreateUiObject(Transform parent, string name)
            {
                GameObject gameObject = new GameObject(name, typeof(RectTransform));
                gameObject.transform.SetParent(parent, false);
                return gameObject;
            }

            private static Image CreateImage(Transform parent, string name)
            {
                GameObject gameObject = CreateUiObject(parent, name);
                gameObject.AddComponent<CanvasRenderer>();
                return gameObject.AddComponent<Image>();
            }

            private static Text CreateText(Transform parent, string name)
            {
                GameObject gameObject = CreateUiObject(parent, name);
                gameObject.AddComponent<CanvasRenderer>();
                return gameObject.AddComponent<Text>();
            }

            private static void Bind(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
            {
                SerializedObject serialized = new SerializedObject(target);
                SerializedProperty property = serialized.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, $"Missing serialized property '{propertyName}'.");
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
