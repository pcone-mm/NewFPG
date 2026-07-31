using FPG.Demo.Unity;
using FPG.Demo.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgRoomDefinitionTests
    {
        private const string RoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";
        private const string Root1RoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/root1.asset";
        private const string ForestCopyRoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest_Copy.asset";
        private const string DefaultCoverPath =
            "Assets/FPGDemo/Presentation/FormalEncounter/Covers/PF_FPG_DefaultCover.prefab";
        private const string TreeCoverPath =
            "Assets/FPGDemo/Presentation/FormalEncounter/Covers/PF_FPG_Root1TreeCover.prefab";

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
        public void RoomValidationRequiresValidCoverCameraProfiles()
        {
            FpgRoomDefinition clone = Object.Instantiate(
                LoadRequired<FpgRoomDefinition>(RoomPath));
            FpgCoverCameraProfile invalidProfile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                SerializedObject serializedRoom = new SerializedObject(clone);
                SerializedProperty firstCover = serializedRoom
                    .FindProperty("coverSlots")
                    .GetArrayElementAtIndex(0);
                firstCover.FindPropertyRelative("cameraProfile")
                    .objectReferenceValue = null;
                serializedRoom.ApplyModifiedPropertiesWithoutUndo();

                FpgRoomValidationIssue missingIssue = AssertRoomIssue(
                    clone.Validate(),
                    FpgRoomValidationCode.MissingCoverCameraProfile);
                Assert.That(missingIssue.MarkerKind,
                    Is.EqualTo(FpgRoomMarkerKind.Cover));
                Assert.That(missingIssue.MarkerId,
                    Is.EqualTo(clone.CoverSlots[0].MarkerId));

                SerializedObject serializedProfile =
                    new SerializedObject(invalidProfile);
                serializedProfile.FindProperty("farClipPlane").floatValue = 0.1f;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();

                serializedRoom.Update();
                firstCover = serializedRoom.FindProperty("coverSlots")
                    .GetArrayElementAtIndex(0);
                firstCover.FindPropertyRelative("cameraProfile")
                    .objectReferenceValue = invalidProfile;
                serializedRoom.ApplyModifiedPropertiesWithoutUndo();

                FpgRoomValidationIssue invalidIssue = AssertRoomIssue(
                    clone.Validate(),
                    FpgRoomValidationCode.InvalidCoverCameraProfile);
                Assert.That(invalidIssue.MarkerKind,
                    Is.EqualTo(FpgRoomMarkerKind.Cover));
                Assert.That(invalidIssue.MarkerId,
                    Is.EqualTo(clone.CoverSlots[0].MarkerId));
            }
            finally
            {
                Object.DestroyImmediate(invalidProfile);
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

        [Test]
        public void Root1UsesTreeOnlyForLeftCoverAndPreservesSlotRules()
        {
            FpgRoomDefinition root1 = LoadRequired<FpgRoomDefinition>(Root1RoomPath);
            Assert.That(root1.TryValidate(out FpgRoomValidationResult validation),
                Is.True,
                validation.FirstError?.Message);
            Assert.That(root1.CoverSlots.Count, Is.EqualTo(3));

            FpgRoomCoverSlot left = root1.CoverSlots[0];
            FpgRoomCoverSlot center = root1.CoverSlots[1];
            FpgRoomCoverSlot right = root1.CoverSlots[2];
            AssertCoverSlot(
                left,
                "cover-left",
                TreeCoverPath,
                "Assets/FPGDemo/Config/Level/CameraProfiles/root1/CAM_root1_cover-left.asset",
                new Vector3(-6f, 0.5f, 0f),
                false);
            AssertCoverSlot(
                center,
                "cover-center",
                DefaultCoverPath,
                "Assets/FPGDemo/Config/Level/CameraProfiles/root1/CAM_root1_cover-center.asset",
                new Vector3(0f, 0.5f, 0f),
                true);
            AssertCoverSlot(
                right,
                "cover-right",
                DefaultCoverPath,
                "Assets/FPGDemo/Config/Level/CameraProfiles/root1/CAM_root1_cover-right.asset",
                new Vector3(5.5f, 0.5f, 0f),
                false);
        }

        [Test]
        public void OtherProductionCoverRoomsRemainValidAndUseDefaultStyle()
        {
            string[] paths = { RoomPath, ForestCopyRoomPath };
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                string path = paths[pathIndex];
                FpgRoomDefinition room = LoadRequired<FpgRoomDefinition>(path);
                Assert.That(
                    room.TryValidate(out FpgRoomValidationResult validation),
                    Is.True,
                    path + ": " + validation.FirstError?.Message);
                for (int coverIndex = 0;
                    coverIndex < room.CoverSlots.Count;
                    coverIndex++)
                {
                    Assert.That(
                        AssetDatabase.GetAssetPath(
                            room.CoverSlots[coverIndex].Prefab),
                        Is.EqualTo(DefaultCoverPath),
                        path + ": " + room.CoverSlots[coverIndex].MarkerId);
                }
            }
        }

        [Test]
        public void Root1TreeCoverAppliesSnapshotsAndReinitializesIntact()
        {
            GameObject host = new GameObject("Root1RoomInstanceTestHost");
            try
            {
                FpgRoomDefinition root1 =
                    LoadRequired<FpgRoomDefinition>(Root1RoomPath);
                FpgRoomInstance instance = host.AddComponent<FpgRoomInstance>();
                Assert.That(instance.TryInitialize(root1, out string error),
                    Is.True,
                    error);
                Assert.That(instance.CoverInstances, Has.Count.EqualTo(3));
                Assert.That(instance.TryGetCoverView("cover-left", out var view),
                    Is.True);

                AssertTreeCoverState(view, false);
                view.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-left", 0, 0, 100, false, false, false));
                AssertTreeCoverState(view, true);

                Assert.That(instance.TryInitialize(root1, out error), Is.True, error);
                Assert.That(instance.CoverInstances, Has.Count.EqualTo(3));
                Assert.That(instance.TryGetCoverView("cover-left", out view), Is.True);
                AssertTreeCoverState(view, false);
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

        private static void AssertCoverSlot(
            FpgRoomCoverSlot slot,
            string markerId,
            string prefabPath,
            string cameraPath,
            Vector3 localPosition,
            bool isStartingCover)
        {
            Assert.That(slot.MarkerId, Is.EqualTo(markerId));
            Assert.That(AssetDatabase.GetAssetPath(slot.Prefab), Is.EqualTo(prefabPath));
            Assert.That(
                AssetDatabase.GetAssetPath(slot.CameraProfile),
                Is.EqualTo(cameraPath));
            Assert.That(slot.LocalPosition, Is.EqualTo(localPosition));
            Assert.That(slot.LocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(slot.PlayerReachableLocalPosition, Is.EqualTo(localPosition));
            Assert.That(slot.PlayerReachableLocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(slot.MaxDurability, Is.EqualTo(100));
            Assert.That(slot.IsStartingCover, Is.EqualTo(isStartingCover));
        }

        private static void AssertTreeCoverState(
            FpgCoverEntityView view,
            bool destroyed)
        {
            SerializedObject serialized = new SerializedObject(view);
            GameObject intactRoot = serialized.FindProperty("intactRoot")
                .objectReferenceValue as GameObject;
            GameObject destroyedRoot = serialized.FindProperty("destroyedRoot")
                .objectReferenceValue as GameObject;
            Assert.That(view.IsDestroyed, Is.EqualTo(destroyed));
            Assert.That(intactRoot.activeSelf, Is.EqualTo(!destroyed));
            Assert.That(destroyedRoot.activeSelf, Is.EqualTo(destroyed));
            Assert.That(serialized.FindProperty("blockingColliders").arraySize, Is.EqualTo(0));
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required asset is missing: {path}");
            return asset;
        }
    }
}
