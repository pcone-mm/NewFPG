using System;
using System.Reflection;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgBootSecondaryModeSelectionTests
    {
        private const string CatalogPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset";

        private GameObject testRoot;
        private GameBootstrapConfig testConfig;
        private FpgPlayableCharacterCatalog transientCatalog;

        [TearDown]
        public void TearDown()
        {
            if (testRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(testRoot);
            }

            if (testConfig != null)
            {
                UnityEngine.Object.DestroyImmediate(testConfig);
            }

            if (transientCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(transientCatalog);
            }
        }

        [TestCase(SecondaryTriggerMode.ImmediateRepeatWhileHeld)]
        [TestCase(SecondaryTriggerMode.ChargeRelease)]
        public void MultiModeFeiSelectionBlocksWorldInputAndCommitsChosenMode(
            SecondaryTriggerMode selectedMode)
        {
            FpgPlayableCharacterCatalog catalog = LoadCatalog();
            SelectionHarness harness = CreateHarness(catalog);

            Assert.That(
                harness.Bootstrap.TrySelectCharacter(
                    harness.CharacterChoice,
                    out string selectionError),
                Is.True,
                selectionError);
            Assert.That(
                harness.Bootstrap.State,
                Is.EqualTo(BootstrapState.WaitingForSecondaryModeSelection));
            Assert.That(harness.Selector.IsVisible, Is.True);
            Assert.That(harness.Selector.CanvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(harness.Selector.CanvasGroup.interactable, Is.True);
            Assert.That(harness.Selector.CanvasGroup.blocksRaycasts, Is.True);
            Assert.That(harness.ImmediateButton.gameObject.activeSelf, Is.True);
            Assert.That(harness.ImmediateButton.interactable, Is.True);
            Assert.That(harness.ChargeButton.gameObject.activeSelf, Is.True);
            Assert.That(harness.ChargeButton.interactable, Is.True);

            Button selectedButton = selectedMode
                == SecondaryTriggerMode.ImmediateRepeatWhileHeld
                ? harness.ImmediateButton
                : harness.ChargeButton;
            selectedButton.onClick.Invoke();

            Assert.That(
                harness.Bootstrap.State,
                Is.EqualTo(BootstrapState.WaitingForRoomSelection));
            Assert.That(
                harness.Bootstrap.SelectedPlayerSelection
                    .SelectedSecondaryTriggerMode,
                Is.EqualTo(selectedMode));
            Assert.That(harness.Selector.IsVisible, Is.False);
            Assert.That(harness.Selector.CanvasGroup.alpha, Is.Zero);
            Assert.That(harness.Selector.CanvasGroup.interactable, Is.False);
            Assert.That(harness.Selector.CanvasGroup.blocksRaycasts, Is.False);
        }

        [Test]
        public void SingleModeCharacterAutoSelectsAndKeepsModePanelHidden()
        {
            FpgPlayableCharacterSelection feiSelection = LoadDefaultSelection();
            transientCatalog = CreateSingleModeCatalog(
                feiSelection,
                SecondaryTriggerMode.ImmediateRepeatWhileHeld);
            SelectionHarness harness = CreateHarness(transientCatalog);

            Assert.That(
                harness.Bootstrap.TrySelectCharacter(
                    harness.CharacterChoice,
                    out string selectionError),
                Is.True,
                selectionError);

            Assert.That(
                harness.Bootstrap.State,
                Is.EqualTo(BootstrapState.WaitingForRoomSelection));
            Assert.That(
                harness.Bootstrap.SelectedPlayerSelection
                    .SelectedSecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            Assert.That(harness.Selector.IsVisible, Is.False);
            Assert.That(harness.Selector.CanvasGroup.alpha, Is.Zero);
            Assert.That(harness.Selector.CanvasGroup.interactable, Is.False);
            Assert.That(harness.Selector.CanvasGroup.blocksRaycasts, Is.False);
        }

        [Test]
        public void DefaultImmediateModeSurvivesBootstrapSnapshotAndCompositionInput()
        {
            FpgPlayableCharacterCatalog catalog = LoadCatalog();
            FpgPlayableCharacterSelection defaultSelection =
                LoadDefaultSelection();
            Assert.That(
                defaultSelection.SelectedSecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));

            Type snapshotType = typeof(GameBootstrap).GetNestedType(
                "BootstrapSelectionSnapshot",
                BindingFlags.NonPublic);
            Assert.That(snapshotType, Is.Not.Null);
            ConstructorInfo snapshotConstructor = snapshotType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(FpgPlayableCharacterSelection),
                    typeof(FpgRoomDefinition)
                },
                null);
            Assert.That(snapshotConstructor, Is.Not.Null);
            object snapshot = snapshotConstructor.Invoke(
                new object[] { defaultSelection, null });
            PropertyInfo characterSelectionProperty = snapshotType.GetProperty(
                "CharacterSelection",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(characterSelectionProperty, Is.Not.Null);
            FpgPlayableCharacterSelection snapshotSelection =
                (FpgPlayableCharacterSelection)
                characterSelectionProperty.GetValue(snapshot);

            Assert.That(
                snapshotSelection.SelectedSecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            Assert.That(
                catalog.TryResolve(
                    snapshotSelection.CharacterId,
                    out FpgPlayableCharacterSelection catalogSelection,
                    out string catalogError),
                Is.True,
                catalogError);
            FpgPlayableCharacterSelection compositionSelection =
                catalogSelection.WithSecondaryMode(
                    snapshotSelection.SelectedSecondaryTriggerMode);
            Assert.That(
                compositionSelection.TryValidate(out string compositionError),
                Is.True,
                compositionError);
            Assert.That(
                compositionSelection.CharacterDefinition.Weapon.TryCreate(
                    compositionSelection.SelectedSecondaryTriggerMode,
                    out WeaponDefinition weaponDefinition,
                    out string weaponError),
                Is.True,
                weaponError);
            Assert.That(
                weaponDefinition.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
        }

        private SelectionHarness CreateHarness(
            FpgPlayableCharacterCatalog catalog)
        {
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection selection,
                    out string catalogError),
                Is.True,
                catalogError);

            testRoot = new GameObject("BootSecondaryModeSelectionTest");
            testRoot.SetActive(false);
            GameBootstrap bootstrap = testRoot.AddComponent<GameBootstrap>();

            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            canvasObject.transform.SetParent(testRoot.transform, false);
            GameObject selectorObject = new GameObject(
                "SecondaryModeSelector",
                typeof(RectTransform),
                typeof(CanvasGroup));
            selectorObject.transform.SetParent(canvasObject.transform, false);
            FpgBootSecondaryModeSelector selector =
                selectorObject.AddComponent<FpgBootSecondaryModeSelector>();
            Button immediateButton = CreateButton(
                "ImmediateModeButton",
                selectorObject.transform);
            Button chargeButton = CreateButton(
                "ChargeModeButton",
                selectorObject.transform);
            SetPrivateField(
                selector,
                "canvasGroup",
                selectorObject.GetComponent<CanvasGroup>());
            SetPrivateField(selector, "immediateModeButton", immediateButton);
            SetPrivateField(selector, "chargeModeButton", chargeButton);

            GameObject choiceObject = new GameObject("FeiCharacterChoice");
            choiceObject.transform.SetParent(testRoot.transform, false);
            FpgBootCharacterChoice characterChoice =
                choiceObject.AddComponent<FpgBootCharacterChoice>();
            GameObject previewRoot = new GameObject("PreviewRoot");
            previewRoot.transform.SetParent(choiceObject.transform, false);
            BoxCollider hitCollider = previewRoot.AddComponent<BoxCollider>();
            SetPrivateField(
                characterChoice,
                "character",
                selection.CharacterDefinition);
            SetPrivateField(characterChoice, "previewRoot", previewRoot);
            SetPrivateField(
                characterChoice,
                "hitColliders",
                new Collider[] { hitCollider });

            testConfig = ScriptableObject.CreateInstance<GameBootstrapConfig>();
            SetPrivateField(testConfig, "requireEntranceSelection", true);
            SetPrivateField(testConfig, "loadRoomOnStart", true);
            SetPrivateField(bootstrap, "config", testConfig);
            SetPrivateField(bootstrap, "playableCharacterCatalog", catalog);
            SetPrivateField(
                bootstrap,
                "characterChoices",
                new[] { characterChoice });
            SetPrivateField(bootstrap, "secondaryModeSelector", selector);

            testRoot.SetActive(true);
            SetPrivateProperty(
                bootstrap,
                "State",
                BootstrapState.WaitingForCharacterSelection);
            return new SelectionHarness(
                bootstrap,
                characterChoice,
                selector,
                immediateButton,
                chargeButton);
        }

        private static Button CreateButton(string name, Transform parent)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.GetComponent<Button>();
        }

        private static FpgPlayableCharacterCatalog LoadCatalog()
        {
            FpgPlayableCharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(
                catalog.TryValidate(out string catalogError),
                Is.True,
                catalogError);
            return catalog;
        }

        private static FpgPlayableCharacterSelection LoadDefaultSelection()
        {
            FpgPlayableCharacterCatalog catalog = LoadCatalog();
            Assert.That(
                catalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection selection,
                    out string selectionError),
                Is.True,
                selectionError);
            return selection;
        }

        private static FpgPlayableCharacterCatalog CreateSingleModeCatalog(
            FpgPlayableCharacterSelection source,
            SecondaryTriggerMode mode)
        {
            FpgPlayableCharacterCatalogEntry entry =
                new FpgPlayableCharacterCatalogEntry();
            SetPrivateField(entry, "character", source.CharacterDefinition);
            SetPrivateField(entry, "threeCProfile", source.ThreeCProfile);
            SetPrivateField(
                entry,
                "combatFeelProfile",
                source.CombatFeelProfile);
            SetPrivateField(
                entry,
                "selectionPreviewPrefab",
                source.SelectionPreviewPrefab);
            SetPrivateField(
                entry,
                "supportedSecondaryModes",
                new[] { mode });
            SetPrivateField(entry, "defaultSecondaryMode", mode);

            FpgPlayableCharacterCatalog catalog =
                ScriptableObject.CreateInstance<FpgPlayableCharacterCatalog>();
            SetPrivateField(
                catalog,
                "defaultCharacter",
                source.CharacterDefinition);
            SetPrivateField(catalog, "entries", new[] { entry });
            Assert.That(
                catalog.TryValidate(out string catalogError),
                Is.True,
                catalogError);
            return catalog;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetPrivateProperty(
            object target,
            string propertyName,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                property,
                Is.Not.Null,
                $"Missing property '{propertyName}' on {target.GetType().Name}.");
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(
                setter,
                Is.Not.Null,
                $"Missing setter for '{propertyName}' on {target.GetType().Name}.");
            setter.Invoke(target, new[] { value });
        }

        private readonly struct SelectionHarness
        {
            public SelectionHarness(
                GameBootstrap bootstrap,
                FpgBootCharacterChoice characterChoice,
                FpgBootSecondaryModeSelector selector,
                Button immediateButton,
                Button chargeButton)
            {
                Bootstrap = bootstrap;
                CharacterChoice = characterChoice;
                Selector = selector;
                ImmediateButton = immediateButton;
                ChargeButton = chargeButton;
            }

            public GameBootstrap Bootstrap { get; }
            public FpgBootCharacterChoice CharacterChoice { get; }
            public FpgBootSecondaryModeSelector Selector { get; }
            public Button ImmediateButton { get; }
            public Button ChargeButton { get; }
        }
    }
}
