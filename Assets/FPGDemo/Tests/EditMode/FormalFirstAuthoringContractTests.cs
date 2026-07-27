using System;
using System.Collections.Generic;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary.asset",
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
                    Assert.That(composers, Has.Count.EqualTo(1));
                    Assert.That(formalHosts, Has.Count.EqualTo(1));
                    Assert.That(
                        formalHosts[0].PlayerComposer,
                        Is.SameAs(composers[0]));
                    Assert.That(
                        formalHosts[0].TryValidateAuthoring(
                            out string authoringError),
                        Is.True,
                        authoringError);
                });
        }

        [Test]
        public void FormalSkillAssetsAreV3OnlyAndCompileWithoutRemovedFields()
        {
            Assert.That(FormalSkillPaths, Has.Length.EqualTo(8));
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
            FpgCompiledEnemySummonPayload summon =
                compiled.SummonActions[0].SummonPayload;
            Assert.That(
                summon.OwnerOutcome,
                Is.EqualTo(FpgSummonOwnerOutcome.DieAfterSuccessfulSummon));
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
        public void RoomEditorDoesNotExposeLegacyD0ScenarioControls()
        {
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                RoomEditorLayoutPath);
            Assert.That(layout, Is.Not.Null, RoomEditorLayoutPath);

            VisualElement root = new VisualElement();
            layout.CloneTree(root);

            Assert.That(root.Q("scenario-field"), Is.Null);
            Assert.That(root.Q("play-room-button"), Is.Null);
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
