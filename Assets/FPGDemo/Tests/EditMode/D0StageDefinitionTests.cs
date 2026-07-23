using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0StageDefinitionTests
    {
        private const string StagePath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_Stage.asset";

        private const string BurstbugScenarioPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset";

        private const string HudieScenarioPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsHudie.asset";

        private const string RoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_combatlab-forest.asset";

        private const string RoomGroupPath =
            "Assets/FPGDemo/Config/Level/Groups/RoomGroup_NormalCombat.asset";

        private const string RoomTagPath =
            "Assets/FPGDemo/Config/Level/Tags/RoomTag_MigratedD0.asset";

        private const string EnvironmentPrefabPath =
            "Assets/FPGDemo/Presentation/Level/Environment/ENV_combatlab-forest.prefab";

        [Test]
        public void CombatLabScenarioUsesThePlannerOwnedSingleEncounterStage()
        {
            D0StageDefinition stage = LoadRequired<D0StageDefinition>(StagePath);
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(BurstbugScenarioPath);

            Assert.That(stage.TryValidate(out string error), Is.True, error);
            Assert.That(scenario.StageDefinition, Is.SameAs(stage));
            Assert.That(stage.StageId, Is.EqualTo("combatlab-forest"));
            Assert.That(stage.SpawnPoints.Count, Is.EqualTo(2));
            Assert.That(stage.ForestLayers.Count, Is.EqualTo(14));

            Assert.That(
                stage.TryGetSpawnPoint(
                    "player-main",
                    out D0StageSpawnPointDefinition playerPoint),
                Is.True);
            Assert.That(playerPoint.LocalPosition, Is.EqualTo(new Vector3(0f, 1.04f, 0f)));
            Assert.That(playerPoint.LocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(
                stage.TryGetSpawnPoint(
                    "enemy-main",
                    out D0StageSpawnPointDefinition enemyPoint),
                Is.True);
            Assert.That(enemyPoint.LocalPosition, Is.EqualTo(new Vector3(0f, 1f, 13f)));
            Assert.That(enemyPoint.LocalEulerAngles, Is.EqualTo(new Vector3(0f, 180f, 0f)));
            Assert.That(stage.TryGetSpawnPoint("missing", out _), Is.False);

            for (int index = 0; index < stage.ForestLayers.Count; index++)
            {
                D0StageForestLayerDefinition layer = stage.ForestLayers[index];
                Assert.That(layer, Is.Not.Null);
                Assert.That(layer.TryValidate(out string layerError), Is.True, layerError);
                string spritePath = AssetDatabase.GetAssetPath(layer.Sprite);
                Assert.That(
                    spritePath.StartsWith("Assets/Art/Scenes/树林切图/", System.StringComparison.Ordinal),
                    Is.True,
                    $"Stage layer '{layer.LayerId}' must expose its direct Sprite reference to planners.");
            }
        }

        [TestCase(BurstbugScenarioPath, 1)]
        [TestCase(HudieScenarioPath, 1)]
        public void InstalledScenariosCrossReferenceOnlyNamedStageSpawnPoints(
            string scenarioPath,
            int expectedEnemySlotCount)
        {
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(scenarioPath);
            D0StageDefinition stage = scenario.StageDefinition;

            Assert.That(stage, Is.SameAs(LoadRequired<D0StageDefinition>(StagePath)));
            Assert.That(scenario.TryValidate(out string error), Is.True, error);
            Assert.That(scenario.PlayerSpawnPointId, Is.EqualTo("player-main"));
            Assert.That(
                stage.TryGetSpawnPoint(
                    scenario.PlayerSpawnPointId,
                    out D0StageSpawnPointDefinition playerPoint),
                Is.True);
            Assert.That(playerPoint.LocalPosition, Is.EqualTo(new Vector3(0f, 1.04f, 0f)));
            Assert.That(playerPoint.LocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(scenario.Encounter.SpawnSlotCount, Is.EqualTo(expectedEnemySlotCount));

            for (int index = 0; index < scenario.Encounter.SpawnSlotCount; index++)
            {
                D0EncounterSpawnSlot slot = scenario.Encounter.GetSpawnSlot(index);
                Assert.That(slot.SpawnPointId, Is.EqualTo("enemy-main"));
                Assert.That(
                    stage.TryGetSpawnPoint(
                        slot.SpawnPointId,
                        out D0StageSpawnPointDefinition enemyPoint),
                    Is.True);
                Assert.That(enemyPoint.LocalPosition, Is.EqualTo(new Vector3(0f, 1f, 13f)));
                Assert.That(
                    enemyPoint.LocalEulerAngles,
                    Is.EqualTo(new Vector3(0f, 180f, 0f)));
            }
        }

        [Test]
        public void StageRejectsDuplicateSpawnPointIds()
        {
            D0StageDefinition clone = Object.Instantiate(
                LoadRequired<D0StageDefinition>(StagePath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty spawnPoints = serialized.FindProperty("spawnPoints");
                Assert.That(spawnPoints, Is.Not.Null);
                Assert.That(spawnPoints.arraySize, Is.GreaterThan(1));
                string firstSpawnPointId = spawnPoints.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("spawnPointId").stringValue;
                spawnPoints.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("spawnPointId").stringValue = firstSpawnPointId;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("unique"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void StageRejectsDuplicateForestLayerIds()
        {
            D0StageDefinition clone = Object.Instantiate(
                LoadRequired<D0StageDefinition>(StagePath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty layers = serialized.FindProperty("forestLayers");
                Assert.That(layers, Is.Not.Null);
                Assert.That(layers.arraySize, Is.GreaterThan(1));
                string firstLayerId = layers.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("layerId").stringValue;
                layers.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("layerId").stringValue = firstLayerId;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("unique"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void MigratedCombatLabRoomPreservesLegacyStageAndCategorization()
        {
            D0StageDefinition stage = LoadRequired<D0StageDefinition>(StagePath);
            FpgRoomDefinition room = LoadRequired<FpgRoomDefinition>(RoomPath);
            FpgRoomGroupDefinition group =
                LoadRequired<FpgRoomGroupDefinition>(RoomGroupPath);
            FpgRoomTagDefinition tag =
                LoadRequired<FpgRoomTagDefinition>(RoomTagPath);
            GameObject environment =
                LoadRequired<GameObject>(EnvironmentPrefabPath);

            FpgRoomValidationResult validation = room.Validate();
            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.ErrorCount, Is.Zero);
            Assert.That(validation.WarningCount, Is.EqualTo(2));
            AssertRoomIssue(validation, FpgRoomValidationCode.MissingExitSlot);
            AssertRoomIssue(validation, FpgRoomValidationCode.MissingReachablePoint);

            Assert.That(room.RoomId, Is.EqualTo("room-combatlab-forest"));
            Assert.That(room.DisplayName, Is.EqualTo(stage.DisplayName));
            Assert.That(room.EnvironmentPrefab, Is.SameAs(environment));
            Assert.That(room.MainGroup, Is.SameAs(group));
            Assert.That(group.GroupId, Is.EqualTo("normal-combat"));
            Assert.That(group.TryValidate(out string groupError), Is.True, groupError);
            Assert.That(room.Tags.Count, Is.EqualTo(1));
            Assert.That(room.Tags[0], Is.SameAs(tag));
            Assert.That(tag.TagId, Is.EqualTo("migrated-d0-stage"));
            Assert.That(tag.TryValidate(out string tagError), Is.True, tagError);

            Assert.That(
                room.TryGetPlayerEntryPoint(
                    "player-main",
                    out FpgRoomPlayerEntryPoint playerPoint),
                Is.True);
            Assert.That(playerPoint.LocalPosition, Is.EqualTo(new Vector3(0f, 1.04f, 0f)));
            Assert.That(playerPoint.LocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(
                room.TryGetEnemySpawnPoint(
                    "enemy-main",
                    out FpgRoomEnemySpawnPoint enemyPoint),
                Is.True);
            Assert.That(enemyPoint.LocalPosition, Is.EqualTo(new Vector3(0f, 1f, 13f)));
            Assert.That(enemyPoint.LocalEulerAngles, Is.EqualTo(new Vector3(0f, 180f, 0f)));
            Assert.That(enemyPoint.Role, Is.EqualTo(FpgRoomEnemySpawnRole.Any));
            Assert.That(room.TryGetMarker("player-main", out FpgRoomMarker marker), Is.True);
            Assert.That(marker, Is.SameAs(playerPoint));
            Assert.That(room.TryGetMarker("missing", out _), Is.False);

            Assert.That(stage.TryValidate(out string stageError), Is.True, stageError);
            Assert.That(AssetDatabase.GetAssetPath(stage), Is.EqualTo(StagePath));
            Assert.That(stage.StageId, Is.EqualTo("combatlab-forest"));
            Assert.That(stage.SpawnPoints.Count, Is.EqualTo(2));
            Assert.That(stage.ForestLayers.Count, Is.EqualTo(14));
        }

        [Test]
        public void MigratedEnvironmentPrefabMatchesEveryLegacyForestLayer()
        {
            D0StageDefinition stage = LoadRequired<D0StageDefinition>(StagePath);
            GameObject environment =
                LoadRequired<GameObject>(EnvironmentPrefabPath);
            D0ForestParallax parallax =
                environment.GetComponent<D0ForestParallax>();
            D0ForestParallaxLayer[] migratedLayers =
                environment.GetComponentsInChildren<D0ForestParallaxLayer>(true);

            Assert.That(parallax, Is.Not.Null);
            Assert.That(parallax.AimReticle, Is.Null,
                "The room binding supplies the CombatLab reticle at runtime.");
            Assert.That(parallax.LayerCount, Is.EqualTo(stage.ForestLayers.Count));
            Assert.That(migratedLayers.Length, Is.EqualTo(stage.ForestLayers.Count));
            Assert.That(environment.transform.childCount, Is.EqualTo(14));

            for (int index = 0; index < stage.ForestLayers.Count; index++)
            {
                D0StageForestLayerDefinition source = stage.ForestLayers[index];
                Transform child = environment.transform.Find(source.LayerId);
                Assert.That(child, Is.Not.Null, source.LayerId);

                D0ForestParallaxLayer migrated =
                    child.GetComponent<D0ForestParallaxLayer>();
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                Assert.That(migrated, Is.Not.Null, source.LayerId);
                Assert.That(renderer, Is.Not.Null, source.LayerId);
                Assert.That(renderer.sprite, Is.SameAs(source.Sprite), source.LayerId);
                Assert.That(child.localPosition, Is.EqualTo(source.BaseLocalPosition), source.LayerId);
                Assert.That(migrated.BaseLocalPosition, Is.EqualTo(source.BaseLocalPosition), source.LayerId);
                Assert.That(
                    migrated.ViewportOffsetMultiplier,
                    Is.EqualTo(source.ViewportOffsetMultiplier),
                    source.LayerId);
                Assert.That(renderer.sortingOrder, Is.EqualTo(source.SortingOrder), source.LayerId);
                Assert.That(renderer.flipX, Is.EqualTo(source.FlipX), source.LayerId);
                Assert.That(renderer.color, Is.EqualTo(source.Color), source.LayerId);

                float expectedScale =
                    source.DesiredWorldWidth / source.Sprite.bounds.size.x;
                Assert.That(
                    child.localScale.x,
                    Is.EqualTo(expectedScale).Within(0.0001f),
                    source.LayerId);
                Assert.That(
                    child.localScale.y,
                    Is.EqualTo(expectedScale).Within(0.0001f),
                    source.LayerId);
                Assert.That(child.localScale.z, Is.EqualTo(1f), source.LayerId);
            }
        }

        [Test]
        public void RoomValidationReportsRequiredReferencesAndNonFiniteMarkerPose()
        {
            FpgRoomDefinition clone = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                serialized.FindProperty("roomId").stringValue = string.Empty;
                serialized.FindProperty("environmentPrefab").objectReferenceValue = null;
                serialized.FindProperty("mainGroup").objectReferenceValue = null;
                serialized.FindProperty("playerEntryPoints").arraySize = 0;
                SerializedProperty enemy = serialized.FindProperty("enemySpawnPoints")
                    .GetArrayElementAtIndex(0);
                enemy.FindPropertyRelative("localPosition").vector3Value =
                    new Vector3(float.NaN, 1f, 13f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    clone.TryValidate(out FpgRoomValidationResult validation),
                    Is.False);
                AssertRoomIssue(validation, FpgRoomValidationCode.MissingRoomId);
                AssertRoomIssue(validation, FpgRoomValidationCode.MissingEnvironmentPrefab);
                AssertRoomIssue(validation, FpgRoomValidationCode.MissingMainGroup);
                AssertRoomIssue(validation, FpgRoomValidationCode.MissingPlayerEntryPoint);
                AssertRoomIssue(validation, FpgRoomValidationCode.InvalidMarkerPose);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void RoomValidationRejectsDuplicateMarkerIdsAcrossMarkerTypes()
        {
            FpgRoomDefinition clone = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty exits = serialized.FindProperty("exitSlots");
                exits.arraySize = 1;
                SerializedProperty duplicate = exits.GetArrayElementAtIndex(0);
                duplicate.FindPropertyRelative("markerId").stringValue = "enemy-main";
                duplicate.FindPropertyRelative("displayName").stringValue = "Duplicate exit";
                duplicate.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                duplicate.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgRoomValidationResult validation = clone.Validate();
                Assert.That(validation.IsValid, Is.False);
                FpgRoomValidationIssue issue =
                    AssertRoomIssue(validation, FpgRoomValidationCode.DuplicateMarkerId);
                Assert.That(issue.MarkerId, Is.EqualTo("enemy-main"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void RoomValidationRejectsDuplicateTagsAndGlobalRoomIds()
        {
            FpgRoomDefinition first = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomPath));
            FpgRoomDefinition second = Object.Instantiate(first);
            try
            {
                SerializedObject serialized = new SerializedObject(first);
                SerializedProperty tags = serialized.FindProperty("tags");
                tags.arraySize = 2;
                tags.GetArrayElementAtIndex(1).objectReferenceValue =
                    tags.GetArrayElementAtIndex(0).objectReferenceValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgRoomValidationResult roomValidation = first.Validate();
                Assert.That(roomValidation.IsValid, Is.False);
                AssertRoomIssue(roomValidation, FpgRoomValidationCode.DuplicateTag);

                FpgRoomValidationResult collectionValidation =
                    FpgRoomCollectionValidator.Validate(new[] { first, second });
                Assert.That(collectionValidation.IsValid, Is.False);
                AssertRoomIssue(
                    collectionValidation,
                    FpgRoomValidationCode.DuplicateRoomId);
            }
            finally
            {
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void SemanticIdUtilitiesNormalizeAndAvoidCollisions()
        {
            Assert.That(
                FpgRoomIdUtility.GenerateRoomId(
                    "Room Combat Lab",
                    new[] { "room-combat-lab" }),
                Is.EqualTo("room-combat-lab-02"));
            Assert.That(
                FpgRoomIdUtility.GenerateMarkerId(
                    FpgRoomMarkerKind.EnemySpawn,
                    "Melee 01",
                    new[] { "enemy-melee-01" }),
                Is.EqualTo("enemy-melee-01-02"));
        }

        private static FpgRoomValidationIssue AssertRoomIssue(
            FpgRoomValidationResult result,
            FpgRoomValidationCode expectedCode)
        {
            for (int index = 0; index < result.Issues.Count; index++)
            {
                FpgRoomValidationIssue issue = result.Issues[index];
                if (issue.Code == expectedCode)
                {
                    return issue;
                }
            }

            Assert.Fail($"Expected room validation issue '{expectedCode}'.");
            return null;
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required asset is missing: {path}");
            return asset;
        }
    }
}
