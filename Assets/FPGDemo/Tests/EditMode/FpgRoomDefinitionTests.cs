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
                    "Room Forest",
                    new[] { "room-forest" }),
                Is.EqualTo("room-forest-02"));
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
