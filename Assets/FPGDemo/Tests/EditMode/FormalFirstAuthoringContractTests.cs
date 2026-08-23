using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FormalFirstAuthoringContractTests
    {
        private const string CatalogPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_PlayableCharacterCatalog.asset";
        private const string BootScenePath =
            "Assets/FPGDemo/Scenes/Boot.unity";
        private const string RoomEditorLayoutPath =
            "Assets/FPGDemo/Editor/LevelAuthoring/FpgRoomEditor.uxml";
        private const string FormalRoomScenePath =
            "Assets/FPGDemo/Scenes/FormalRoom.unity";
        private static readonly string[] FormalSkillPaths =
        {
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset",
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Reload.asset",
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Immediate.asset",
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Charge.asset",
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack.asset",
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_HeavyBreak.asset",
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_Volley.asset",
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Attack.asset",
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Attack_Summon.asset"
        };

        [Test]
        public void PlayableCharacterCatalogContainsOneCompleteDefaultFei()
        {
            FpgPlayableCharacterCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(
                catalog.TryValidate(out string catalogError),
                Is.True,
                catalogError);
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(catalog.DefaultCharacter, Is.Not.Null);
            Assert.That(catalog.DefaultCharacter.CharacterId, Is.EqualTo("fei"));

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Entries.Count; index++)
            {
                FpgPlayableCharacterCatalogEntry entry = catalog.Entries[index];
                Assert.That(entry, Is.Not.Null);
                Assert.That(
                    entry.TryCreateSelection(
                        out FpgPlayableCharacterSelection selection,
                        out string entryError),
                    Is.True,
                    entryError);
                Assert.That(ids.Add(selection.CharacterId), Is.True,
                    $"Duplicate playable character ID '{selection.CharacterId}'.");
                Assert.That(selection.CharacterDefinition.EntityPrefab, Is.Not.Null);
                Assert.That(selection.CharacterDefinition.Weapon, Is.Not.Null);
                Assert.That(selection.CharacterDefinition.ActorPresentation, Is.Not.Null);
                Assert.That(selection.ThreeCProfile, Is.Not.Null);
                Assert.That(selection.SelectionPreviewPrefab, Is.Not.Null);
                Assert.That(
                    selection.SelectionPreviewPrefab.GetComponentInChildren<
                        D0ActorEntityView>(true),
                    Is.Null,
                    "A Boot preview prefab must not contain a gameplay Entity.");
            }

            Assert.That(
                catalog.TryResolveDefault(
                    out FpgPlayableCharacterSelection defaultSelection,
                    out string defaultError),
                Is.True,
                defaultError);
            Assert.That(defaultSelection.CharacterId, Is.EqualTo("fei"));
            Assert.That(
                defaultSelection.CharacterDefinition,
                Is.SameAs(catalog.DefaultCharacter));
            Assert.That(
                defaultSelection.SupportedSecondaryModes,
                Is.EquivalentTo(new[]
                {
                    FPG.Demo.Player.SecondaryTriggerMode.ImmediateRepeatWhileHeld,
                    FPG.Demo.Player.SecondaryTriggerMode.ChargeRelease
                }));
            Assert.That(
                defaultSelection.SelectedSecondaryTriggerMode,
                Is.EqualTo(
                    FPG.Demo.Player.SecondaryTriggerMode.ImmediateRepeatWhileHeld));
        }

        [Test]
        public void BootAuthorsOnlyOneVisualCharacterChoice()
        {
            WithPreviewScene(
                BootScenePath,
                scene =>
                {
                    Assert.That(
                        FindComponents<FpgPlayerEntityView>(scene),
                        Is.Empty,
                        "Boot must not contain a gameplay player Entity.");
                    Assert.That(
                        FindComponents<FpgBootCharacterChoice>(scene),
                        Has.Count.EqualTo(1));
                    Assert.That(
                        FindComponents<GameBootstrap>(scene),
                        Has.Count.EqualTo(1));
                    List<FpgRoomTransitionCurtain> curtains =
                        FindComponents<FpgRoomTransitionCurtain>(scene);
                    Assert.That(curtains, Has.Count.EqualTo(1));
                    Assert.That(
                        curtains[0].FadeDuration,
                        Is.EqualTo(FpgRoomTransitionCurtain.DefaultFadeDuration));
                    Assert.That(curtains[0].CanvasGroup, Is.Not.Null);
                    Assert.That(
                        curtains[0].TryValidateAuthoring(out string curtainError),
                        Is.True,
                        curtainError);
                    GameBootstrap bootstrap =
                        FindComponents<GameBootstrap>(scene)[0];
                    List<FpgBootSecondaryModeSelector> selectors =
                        FindComponents<FpgBootSecondaryModeSelector>(scene);
                    Assert.That(selectors, Has.Count.EqualTo(1));
                    Assert.That(
                        bootstrap.SecondaryModeSelector,
                        Is.SameAs(selectors[0]));
                    Assert.That(
                        selectors[0].TryValidateAuthoring(
                            out string selectorError),
                        Is.True,
                        selectorError);
                    Assert.That(
                        selectors[0].CanvasGroup.blocksRaycasts,
                        Is.False,
                        "The modal must block world input only while it is visible.");
                    List<EventSystem> eventSystems =
                        FindComponents<EventSystem>(scene);
                    Assert.That(eventSystems, Has.Count.EqualTo(1));
                    Assert.That(
                        eventSystems[0].GetComponent<InputSystemUIInputModule>(),
                        Is.Not.Null);
                    Assert.That(
                        bootstrap.TryValidateConfiguration(out string bootstrapError),
                        Is.True,
                        bootstrapError);
                });
        }

        [Test]
        public void FormalRoomAuthorsComposerHostAndNoGameplayPlayer()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    Assert.That(
                        FindComponents<FpgPlayerEntityView>(scene),
                        Is.Empty,
                        "FormalRoom player Entity must be composed at runtime.");

                    List<FpgFormalPlayerComposer> composers =
                        FindComponents<FpgFormalPlayerComposer>(scene);
                    List<FpgFormalEncounterHost> formalHosts =
                        FindComponents<FpgFormalEncounterHost>(scene);
                    List<FpgRoomArtSceneLoader> artLoaders =
                        FindComponents<FpgRoomArtSceneLoader>(scene);
                    List<Camera> cameras = FindComponents<Camera>(scene);
                    Assert.That(composers, Has.Count.EqualTo(1));
                    Assert.That(formalHosts, Has.Count.EqualTo(1));
                    Assert.That(artLoaders, Has.Count.EqualTo(1));
                    Assert.That(cameras, Has.Count.EqualTo(1));
                    Assert.That(
                        formalHosts[0].PlayerComposer,
                        Is.SameAs(composers[0]));
                    Assert.That(
                        formalHosts[0].RoomArtSceneLoader,
                        Is.SameAs(artLoaders[0]));
                    Assert.That(
                        formalHosts[0].WorldCamera,
                        Is.SameAs(cameras[0]));
                    FpgFormalPlayerPresentationBridge presentationBridge =
                        composers[0].PresentationBridge;
                    Assert.That(presentationBridge, Is.Not.Null);
                    Assert.That(
                        presentationBridge.TargetCamera,
                        Is.SameAs(cameras[0]));
                    Assert.That(
                        presentationBridge.CameraRig,
                        Is.SameAs(formalHosts[0].CameraRoot));
                    Assert.That(
                        presentationBridge.CameraFeedback.TargetCamera,
                        Is.SameAs(cameras[0]));
                    Assert.That(
                        presentationBridge.CameraFeedback.CameraRig,
                        Is.SameAs(formalHosts[0].CameraRoot));
                    Assert.That(cameras[0].clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                    List<CombatAimReticle> reticles =
                        FindComponents<CombatAimReticle>(scene);
                    Assert.That(reticles, Has.Count.EqualTo(1));
                    UnityEngine.UI.Image chargeImage =
                        reticles[0].ChargeProgressImage;
                    Assert.That(chargeImage, Is.Not.Null);
                    Assert.That(
                        chargeImage.type,
                        Is.EqualTo(UnityEngine.UI.Image.Type.Filled));
                    Assert.That(
                        chargeImage.fillMethod,
                        Is.EqualTo(UnityEngine.UI.Image.FillMethod.Radial360));
                    Assert.That(chargeImage.fillAmount, Is.Zero);
                    Assert.That(chargeImage.raycastTarget, Is.False);
                    Assert.That(chargeImage.gameObject.activeSelf, Is.False);
                    Assert.That(
                        FindComponents<FpgRoomArtRoot>(scene),
                        Is.Empty,
                        "FormalRoom must not own room art or lighting roots.");

                    int directionalLightCount = 0;
                    List<Light> lights = FindComponents<Light>(scene);
                    for (int index = 0; index < lights.Count; index++)
                    {
                        if (lights[index].type == LightType.Directional)
                        {
                            directionalLightCount++;
                        }
                    }

                    Assert.That(
                        directionalLightCount,
                        Is.Zero,
                        "FormalRoom must not own the room's main light.");

                    Component additionalCameraData = null;
                    Component[] cameraComponents =
                        cameras[0].GetComponents<Component>();
                    for (int index = 0;
                        index < cameraComponents.Length;
                        index++)
                    {
                        if (cameraComponents[index] != null
                            && string.Equals(
                                cameraComponents[index].GetType().FullName,
                                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData",
                                StringComparison.Ordinal))
                        {
                            additionalCameraData = cameraComponents[index];
                            break;
                        }
                    }

                    Assert.That(additionalCameraData, Is.Not.Null);
                    SerializedObject cameraData =
                        new SerializedObject(additionalCameraData);
                    Assert.That(
                        cameraData.FindProperty("m_RenderPostProcessing").boolValue,
                        Is.True);
                    Assert.That(
                        cameraData.FindProperty("m_VolumeLayerMask").intValue,
                        Is.EqualTo(1));
                    Assert.That(
                        formalHosts[0].TryValidateAuthoring(
                            out string authoringError),
                        Is.True,
                        authoringError);
                });
        }

        [Test]
        public void FormalRoomUsesOneRuntimeRootWithoutLegacyPlaceholder()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    GameObject[] roots = scene.GetRootGameObjects();

                    Assert.That(roots, Has.Length.EqualTo(1));
                    Assert.That(roots[0].name, Is.EqualTo("__FormalRoom"));
                    Assert.That(
                        roots[0].GetComponent<FpgFormalEncounterHost>(),
                        Is.Not.Null);
                });
        }

        [Test]
        public void FormalRoomFloorIsResolvableEnvironmentBlocker()
        {
            WithPreviewScene(
                FormalRoomScenePath,
                scene =>
                {
                    List<BoxCollider> colliders =
                        FindComponents<BoxCollider>(scene);
                    BoxCollider floor = null;
                    for (int index = 0; index < colliders.Count; index++)
                    {
                        if (string.Equals(
                                colliders[index].name,
                                "FormalFloorBlocker",
                                StringComparison.Ordinal))
                        {
                            floor = colliders[index];
                            break;
                        }
                    }

                    Assert.That(floor, Is.Not.Null);
                    Assert.That(floor.transform.parent, Is.Not.Null);
                    Assert.That(floor.transform.parent.name, Is.EqualTo("World"));
                    Assert.That(floor.isTrigger, Is.False);
                    Assert.That(floor.gameObject.layer, Is.EqualTo(28));
                    Assert.That(
                        floor.bounds.max.y,
                        Is.EqualTo(0f).Within(0.0001f),
                        "The floor blocker must end at the gameplay plane so projectile anchors are not spawned inside it.");

                    List<FpgFormalCombatPortFactory> factories =
                        FindComponents<FpgFormalCombatPortFactory>(scene);
                    Assert.That(factories, Has.Count.EqualTo(1));
                    FpgFormalCombatPortFactory factory = factories[0];
                    UnityAttackQueryTechnicalSettings querySettings =
                        factory.AttackQueryTechnicalSettings;
                    Assert.That(querySettings.IsValid, Is.True);
                    Assert.That(
                        querySettings.BlockerLayerMask
                            & (1 << floor.gameObject.layer),
                        Is.Not.Zero);

                    HitboxRegistry registry = factory.StaticHitboxRegistry;
                    Assert.That(registry, Is.Not.Null);
                    Assert.That(registry.StaticBindingCount, Is.EqualTo(2));
                    UnityAttackQuerySettings validationSettings =
                        new UnityAttackQuerySettings(
                            50f,
                            0.04f,
                            3f,
                            querySettings.HitboxLayerMask,
                            querySettings.BlockerLayerMask);
                    Assert.That(
                        registry.TryValidateStaticBindings(
                            validationSettings,
                            out string validationError),
                        Is.True,
                        validationError);
                    Assert.That(
                        registry.ResetForSession(
                            new RuntimeId(1L),
                            new RuntimeId(2L),
                            out string registrationError),
                        Is.True,
                        registrationError);
                    Assert.That(
                        registry.TryResolve(
                            floor,
                            out RegisteredHitbox byCollider),
                        Is.True);
                    Assert.That(
                        byCollider.TargetKind,
                        Is.EqualTo(QueryTargetKind.EnvironmentBlocker));
                    Assert.That(byCollider.GeometryId, Is.EqualTo(new GeometryId(3002)));
                    Assert.That(byCollider.Team, Is.EqualTo(Team.Neutral));
                    Assert.That(byCollider.RuntimeId, Is.EqualTo(RuntimeId.Invalid));
                    Assert.That(byCollider.HitPart, Is.EqualTo(HitPart.Body));
                    Assert.That(
                        registry.TryResolve(
                            byCollider.GeometryId,
                            out RegisteredHitbox byGeometry),
                        Is.True);
                    Assert.That(byGeometry.Collider, Is.SameAs(floor));
                });
        }

        [Test]
        public void FormalProjectileQueriesHitBothSidesOfPlanarCoverBlockers()
        {
            Assert.That(
                Physics.queriesHitBackfaces,
                Is.True,
                "Formal cover blockers use planar MeshColliders and must be queryable from both projectile directions.");
        }

        [Test]
        public void FormalSkillAssetsAreV3OnlyAndCompileWithoutRemovedFields()
        {
            Assert.That(FormalSkillPaths, Has.Length.EqualTo(9));
            for (int pathIndex = 0;
                pathIndex < FormalSkillPaths.Length;
                pathIndex++)
            {
                string path = FormalSkillPaths[pathIndex];
                FpgSkillTimelineDefinition definition =
                    AssetDatabase.LoadAssetAtPath<FpgSkillTimelineDefinition>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(
                    definition.AuthoringSchemaVersion,
                    Is.EqualTo(
                        FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion),
                    path);
                Assert.That(
                    definition.AuthoringSchemaState,
                    Is.EqualTo(FpgSkillAuthoringSchemaState.V3Only),
                    path);

                SerializedObject serialized = new SerializedObject(definition);
                Assert.That(
                    serialized.FindProperty("payloadSlots"),
                    Is.Null,
                    path);
                for (int sequenceIndex = 0;
                    sequenceIndex < definition.Sequences.Count;
                    sequenceIndex++)
                {
                    Assert.That(
                        definition.Sequences[sequenceIndex],
                        Is.Not.Null,
                        path);
                }

                Assert.That(
                    definition.TryCompile(
                        out FpgCompiledSkillDefinition _,
                        out string compileError),
                    Is.True,
                    path + ": " + compileError);
            }
        }

        [Test]
        public void FormalEnemyCatalogResolvesEveryAuthoredSkillSocket()
        {
            const string enemyCatalogPath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_NormalRoom_EnemyCatalog.asset";
            FpgEnemyDefinitionCatalog enemyCatalog =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinitionCatalog>(
                    enemyCatalogPath);

            Assert.That(enemyCatalog, Is.Not.Null, enemyCatalogPath);
            Assert.That(
                enemyCatalog.TryValidate(out string error),
                Is.True,
                error);
        }

        [Test]
        public void FormalLuanSummonChainTargetsHudie()
        {
            const string attackPath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Attack_Summon.asset";
            const string hudiePath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Enemy.asset";

            FpgEnemyAttackDefinition attack =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(attackPath);
            FpgEnemyDefinition hudie =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(hudiePath);

            Assert.That(attack, Is.Not.Null, attackPath);
            Assert.That(hudie, Is.Not.Null, hudiePath);
            Assert.That(attack.TryValidate(out string attackError), Is.True, attackError);
            Assert.That(hudie.TryValidate(out string hudieError), Is.True, hudieError);
            Assert.That(
                attack.TryCompile(
                    out FpgCompiledEnemySkillDefinition compiled,
                    out string compileError),
                Is.True,
                compileError);
            Assert.That(compiled.SummonActions.Count, Is.EqualTo(1));
            Assert.That(
                compiled.SelfDestructOwnerActions.Count,
                Is.EqualTo(1));
            FpgCompiledEnemySummonPayload summon =
                compiled.SummonActions[0].SummonPayload;
            FpgCompiledEnemySkillAction selfDestruct =
                compiled.SelfDestructOwnerActions[0];
            Assert.That(
                selfDestruct.Kind,
                Is.EqualTo(FpgEnemySkillActionKind.SelfDestructOwner));
            Assert.That(
                selfDestruct.BoundGameplayEventId,
                Is.Zero);
            Assert.That(
                summon.OccupancyMode,
                Is.EqualTo(FpgSummonOccupancyMode.ReplaceOwner));
            Assert.That(
                summon.PlacementMode,
                Is.EqualTo(FpgSummonPlacementMode.OwnerPosition));
            Assert.That(summon.MaxSummonsPerOwner, Is.Zero);
            Assert.That(summon.MaxTotalSummonsPerEncounter, Is.Zero);
            Assert.That(summon.MaxRecursionDepth, Is.EqualTo(1));
            Assert.That(summon.CandidateCount, Is.EqualTo(1));
            Assert.That(summon.GetCandidate(0).Definition, Is.SameAs(hudie));
            Assert.That(summon.GetCandidate(0).Weight, Is.EqualTo(1));
            Assert.That(hudie.EnemyDefinitionId, Is.EqualTo("hudie"));

            FpgSkillSequenceDefinition sequence = attack.Sequences[0];
            Assert.That(sequence.SummonEvents[0].Tick, Is.EqualTo(44));
            Assert.That(
                sequence.SummonEvents[0].AuthoredOrdinal,
                Is.EqualTo(0));
            Assert.That(
                sequence.SelfDestructOwnerEvents[0].Tick,
                Is.EqualTo(71));
            Assert.That(
                sequence.SelfDestructOwnerEvents[0].AuthoredOrdinal,
                Is.EqualTo(3));
            Assert.That(
                sequence.SelfDestructOwnerEvents[0].BoundGameplayEventId,
                Is.Empty);
        }

        [Test]
        public void Level1ChallengePreflightCountsOnlyActualReplacementOwners()
        {
            const string roomPath =
                "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";
            const string profilePath =
                "Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_Profile.asset";
            const string overridePath =
                "Assets/FPGDemo/Config/FormalEncounter/Level1/FPG_L1_01_04_Challenge.asset";
            const string enemyCatalogPath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_NormalRoom_EnemyCatalog.asset";

            FpgRoomDefinition room =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
            FpgEncounterProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgEncounterProfile>(profilePath);
            FpgEncounterOverrideDefinition encounterOverride =
                AssetDatabase.LoadAssetAtPath<FpgEncounterOverrideDefinition>(overridePath);
            FpgEnemyDefinitionCatalog enemyCatalog =
                AssetDatabase.LoadAssetAtPath<FpgEnemyDefinitionCatalog>(enemyCatalogPath);

            Assert.That(room, Is.Not.Null, roomPath);
            Assert.That(profile, Is.Not.Null, profilePath);
            Assert.That(encounterOverride, Is.Not.Null, overridePath);
            Assert.That(enemyCatalog, Is.Not.Null, enemyCatalogPath);

            FpgEncounterRunContext runContext = new FpgEncounterRunContext(
                runSeed: 1UL,
                regionId: "level1-contract",
                depth: 0,
                difficultyMultiplierBasisPoints: FpgEncounterRunContext.BasisPointsOne,
                roomVisitOrdinal: 0);
            FpgRoomRunRequest request = FpgFormalRoomRequestFactory.Create(
                room,
                profile,
                encounterOverride,
                runContext);
            FpgEncounterPlanGenerationResult generated =
                FpgEncounterPlanGenerator.Generate(request);

            Assert.That(generated.IsSuccess, Is.True, generated.Error);
            FpgEncounterPreflightResult preflight = FpgEncounterPreflight.Validate(
                request,
                generated.Plan,
                enemyCatalog);

            Assert.That(preflight.IsSuccess, Is.True, preflight.Error);
            Assert.That(preflight.Requirements.PlannedEnemies, Is.EqualTo(11));
            Assert.That(preflight.Requirements.SummonUpperBound, Is.EqualTo(1));
            Assert.That(
                preflight.Requirements.GameplayQuotaSummonUpperBound,
                Is.Zero);
            Assert.That(preflight.Requirements.EntitySlots, Is.EqualTo(12));
            Assert.That(preflight.Requirements.EntityPoolSlots, Is.EqualTo(7));
            Assert.That(preflight.Requirements.SimultaneousCombatants, Is.EqualTo(3));
            Assert.That(
                preflight.Requirements.RequiredSummonRecursionDepth,
                Is.EqualTo(1));
            Assert.That(preflight.Requirements.RequiredRoomSpawnPoints, Is.EqualTo(2));
            Assert.That(
                preflight.Requirements.EnemyPoolRequirements.Count,
                Is.EqualTo(3));

            int luanPoolCount = 0;
            int hudiePoolCount = 0;
            for (int index = 0;
                index < preflight.Requirements.EnemyPoolRequirements.Count;
                index++)
            {
                FpgEnemyPoolCapacityRequirement poolRequirement =
                    preflight.Requirements.EnemyPoolRequirements[index];
                if (string.Equals(
                        poolRequirement.EnemyDefinitionId,
                        "luan",
                        StringComparison.Ordinal))
                {
                    luanPoolCount = poolRequirement.Count;
                }
                else if (string.Equals(
                    poolRequirement.EnemyDefinitionId,
                    "hudie",
                    StringComparison.Ordinal))
                {
                    hudiePoolCount = poolRequirement.Count;
                }
            }

            Assert.That(luanPoolCount, Is.EqualTo(1));
            Assert.That(hudiePoolCount, Is.EqualTo(3));
        }

        [Test]
        public void RoomEditorExposesIndependentCoverAuthoringControls()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                RoomEditorLayoutPath);
            Assert.That(layout, Is.Not.Null, RoomEditorLayoutPath);

            VisualElement root = new VisualElement();
            layout.CloneTree(root);

            Assert.That(root.Q("scenario-field"), Is.Null);
            Assert.That(root.Q("play-room-button"), Is.Null);
            Assert.That(root.Q("cover-position-field"), Is.Null);
            Assert.That(root.Q("reset-cover-position-button"), Is.Null);
            Assert.That(root.Q("place-cover-button"), Is.Not.Null);
            Assert.That(root.Q("show-cover-toggle"), Is.Not.Null);
            Toggle markerHandlesToggle =
                root.Q<Toggle>("show-marker-handles-toggle");
            Assert.That(markerHandlesToggle, Is.Not.Null);
            Assert.That(markerHandlesToggle.value, Is.False);
        }

        private static List<T> FindComponents<T>(Scene scene)
            where T : Component
        {
            List<T> values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                values.AddRange(
                    roots[rootIndex].GetComponentsInChildren<T>(true));
            }

            return values;
        }

        private static void WithPreviewScene(
            string scenePath,
            Action<Scene> assertion)
        {
            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Assert.That(sceneAsset, Is.Not.Null, scenePath);

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
    }
}
