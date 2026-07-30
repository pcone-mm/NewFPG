using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgRoomDefinitionTests
    {
        private const string RoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";

        [Test]
        public void RoomValidationReportsRequiredReferencesAndNonFiniteMarkerPose()
        {
            FpgRoomDefinition clone = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                serialized.FindProperty("roomId").stringValue = string.Empty;
                SerializedProperty artScene = serialized.FindProperty("artScene");
                artScene.FindPropertyRelative("sceneGuid").stringValue = string.Empty;
                artScene.FindPropertyRelative("scenePath").stringValue = string.Empty;
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
                AssertRoomIssue(validation, FpgRoomValidationCode.MissingArtScene);
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
                string duplicateMarkerId = clone.EnemySpawnPoints[0].MarkerId;
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty exits = serialized.FindProperty("exitSlots");
                exits.arraySize = 1;
                SerializedProperty duplicate = exits.GetArrayElementAtIndex(0);
                duplicate.FindPropertyRelative("markerId").stringValue = duplicateMarkerId;
                duplicate.FindPropertyRelative("displayName").stringValue = "Duplicate exit";
                duplicate.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                duplicate.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgRoomValidationResult validation = clone.Validate();
                Assert.That(validation.IsValid, Is.False);
                FpgRoomValidationIssue issue =
                    AssertRoomIssue(validation, FpgRoomValidationCode.DuplicateMarkerId);
                Assert.That(issue.MarkerId, Is.EqualTo(duplicateMarkerId));
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
        public void RoomValidationEnforcesIndependentCoverAuthoringContracts()
        {
            FpgRoomDefinition clone = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomPath));
            try
            {
                SerializedObject serialized = new SerializedObject(clone);
                SerializedProperty covers = serialized.FindProperty("coverSlots");
                for (int index = 0; index < covers.arraySize; index++)
                {
                    covers.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("isStartingCover")
                        .boolValue = false;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssertRoomIssue(
                    clone.Validate(),
                    FpgRoomValidationCode.MissingStartingCover);

                serialized.Update();
                covers = serialized.FindProperty("coverSlots");
                SerializedProperty first = covers.GetArrayElementAtIndex(0);
                SerializedProperty second = covers.GetArrayElementAtIndex(1);
                first.FindPropertyRelative("isStartingCover").boolValue = true;
                second.FindPropertyRelative("isStartingCover").boolValue = true;
                first.FindPropertyRelative("prefab").objectReferenceValue = null;
                first.FindPropertyRelative("maxDurability").intValue = 0;
                first.FindPropertyRelative("playerReachableLocalPosition")
                    .vector3Value = second.FindPropertyRelative(
                        "playerReachableLocalPosition").vector3Value;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                FpgRoomValidationResult validation = clone.Validate();
                AssertRoomIssue(
                    validation,
                    FpgRoomValidationCode.MultipleStartingCovers);
                AssertRoomIssue(
                    validation,
                    FpgRoomValidationCode.MissingCoverPrefab);
                AssertRoomIssue(
                    validation,
                    FpgRoomValidationCode.InvalidCoverDurability);
                AssertRoomIssue(
                    validation,
                    FpgRoomValidationCode.OverlappingCoverReachablePosition);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void SemanticIdUtilitiesNormalizeAndAvoidCollisions()
        {
            Assert.That(
                FpgRoomIdUtility.GenerateRoomId(
                    "Room Forest",
                    new[] { "room-forest" }),
                Is.EqualTo("room-forest-02"));
            Assert.That(
                FpgRoomIdUtility.GenerateMarkerId(
                    FpgRoomMarkerKind.EnemySpawn,
                    "Melee 01",
                    new[] { "enemy-melee-01" }),
                Is.EqualTo("enemy-melee-01-02"));
            Assert.That(
                FpgRoomIdUtility.GenerateMarkerId(
                    FpgRoomMarkerKind.Cover,
                    "Left",
                    new[] { "cover-left" }),
                Is.EqualTo("cover-left-02"));
        }

        [Test]
        public void RoomInstanceCreatesCoversWithoutInstantiatingArtEnvironment()
        {
            GameObject host = new GameObject("RoomInstanceTestHost");
            try
            {
                FpgRoomInstance instance = host.AddComponent<FpgRoomInstance>();
                Assert.That(
                    instance.TryInitialize(
                        LoadRequired<FpgRoomDefinition>(RoomPath),
                        out string error),
                    Is.True,
                    error);
                Assert.That(instance.DestructibleInstances, Is.Empty);
                Assert.That(instance.CoverInstances, Has.Count.EqualTo(3));
                Assert.That(host.transform.childCount, Is.EqualTo(3));
                Assert.That(
                    host.GetComponentsInChildren<FpgCoverEntityView>(true),
                    Has.Length.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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
