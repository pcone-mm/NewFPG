using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
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
        private const string RoomCatalogPath =
            "Assets/FPGDemo/Config/Level/FPG_RoomCatalog.asset";
        private const string TreeCoverPath =
            "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_Root1TreeCover.prefab";
        private const string BoatLeftCoverPath =
            "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_BoatLeft.prefab";
        private const string BoatRightCoverPath =
            "Assets/FPGDemo/Presentation/Level/Covers/Prefabs/PF_FPG_BoatRight.prefab";

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
                first.FindPropertyRelative("playerLeftPeekLocalPosition")
                    .vector3Value = first.FindPropertyRelative(
                        "playerRightPeekLocalPosition").vector3Value;
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
                AssertRoomIssue(
                    validation,
                    FpgRoomValidationCode.InvalidCoverPeekPositions);

                serialized.Update();
                first = serialized.FindProperty("coverSlots")
                    .GetArrayElementAtIndex(0);
                first.FindPropertyRelative("playerLeftPeekLocalPosition")
                    .vector3Value = new Vector3(float.NaN, 0f, 0f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssertRoomIssue(
                    clone.Validate(),
                    FpgRoomValidationCode.InvalidCoverPeekPositions);
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

                host.transform.SetPositionAndRotation(
                    new Vector3(3f, 2f, -4f),
                    Quaternion.Euler(0f, 30f, 0f));
                FpgRoomCoverSlot center = instance.RoomDefinition.CoverSlots[1];
                Assert.That(
                    instance.TryResolveCoverPeekPosition(
                        center.MarkerId,
                        FpgPlayerFacingDirection.Left,
                        out Vector3 leftWorld),
                    Is.True);
                Assert.That(
                    leftWorld,
                    Is.EqualTo(host.transform.TransformPoint(
                        center.PlayerLeftPeekLocalPosition)));
                Assert.That(
                    instance.TryResolveCoverPeekPosition(
                        center.MarkerId,
                        FpgPlayerFacingDirection.Right,
                        out Vector3 rightWorld),
                    Is.True);
                Assert.That(
                    rightWorld,
                    Is.EqualTo(host.transform.TransformPoint(
                        center.PlayerRightPeekLocalPosition)));
                Assert.That(leftWorld, Is.Not.EqualTo(rightWorld));
                Assert.That(
                    instance.TryResolveCoverPeekPosition(
                        center.MarkerId,
                        (FpgPlayerFacingDirection)99,
                        out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Root1UsesRoomSpecificCoverPrefabsAndPreservesSlotRules()
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
                new Vector3(-0.39f, 0f, 0.29f),
                new Vector3(-6f, 0.5f, 0f),
                new Vector3(-7.35f, 0.5f, 0f),
                new Vector3(-4.65f, 0.5f, 0f),
                false);
            AssertCoverSlot(
                center,
                "cover-center",
                BoatLeftCoverPath,
                "Assets/FPGDemo/Config/Level/CameraProfiles/root1/CAM_root1_cover-center.asset",
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0.5f, 0f),
                new Vector3(-1f, 0.5f, 0f),
                new Vector3(1f, 0.5f, 0f),
                true);
            AssertCoverSlot(
                right,
                "cover-right",
                BoatRightCoverPath,
                "Assets/FPGDemo/Config/Level/CameraProfiles/root1/CAM_root1_cover-right.asset",
                new Vector3(4.33f, 0.48f, 0f),
                new Vector3(4.69f, 0.5f, 0f),
                new Vector3(2.82f, 0.5f, 0f),
                new Vector3(6.55f, 0.5f, 0f),
                false);
        }

        [Test]
        public void Root1CoverStagesApplySnapshotsAndReinitializeIntact()
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

                Assert.That(instance.TryGetCoverView("cover-left", out var left),
                    Is.True);
                Assert.That(instance.TryGetCoverView("cover-center", out var center),
                    Is.True);
                Assert.That(instance.TryGetCoverView("cover-right", out var right),
                    Is.True);

                AssertCoverStageState(left, false, 3, 0);
                left.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-left", 0, 66, 100, false, false, false));
                AssertCoverStageState(left, false, 3, 1);
                left.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-left", 0, 33, 100, false, false, false));
                AssertCoverStageState(left, false, 3, 2);
                left.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-left", 0, 0, 100, false, false, false));
                AssertCoverStageState(left, true, 3, -1);

                AssertCoverStageState(center, false, 2, 0);
                center.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-center", 1, 50, 100, true, false, false));
                AssertCoverStageState(center, false, 2, 1);
                center.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-center", 1, 0, 100, true, false, false));
                AssertCoverStageState(center, true, 2, -1);

                AssertCoverStageState(right, false, 2, 0);
                right.ApplySnapshot(new FpgCoverSnapshot(
                    "cover-right", 2, 50, 100, false, false, false));
                AssertCoverStageState(right, false, 2, 1);

                Assert.That(instance.TryInitialize(root1, out error), Is.True, error);
                Assert.That(instance.CoverInstances, Has.Count.EqualTo(3));
                Assert.That(instance.TryGetCoverView("cover-left", out left), Is.True);
                Assert.That(instance.TryGetCoverView("cover-center", out center), Is.True);
                Assert.That(instance.TryGetCoverView("cover-right", out right), Is.True);
                AssertCoverStageState(left, false, 3, 0);
                AssertCoverStageState(center, false, 2, 0);
                AssertCoverStageState(right, false, 2, 0);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EveryCatalogCoverRegistersItsMeshBlockersAndResolvesCoverId()
        {
            FpgRoomCatalog catalog =
                LoadRequired<FpgRoomCatalog>(RoomCatalogPath);
            Assert.That(catalog.Rooms, Is.Not.Empty);
            Assert.That(
                catalog.TryValidate(out string catalogError),
                Is.True,
                catalogError);

            for (int roomIndex = 0; roomIndex < catalog.Rooms.Count; roomIndex++)
            {
                FpgRoomDefinition definition = catalog.Rooms[roomIndex];
                GameObject host = new GameObject(
                    $"CatalogCoverRegistration_{definition.name}");
                GameObject registryObject = new GameObject(
                    $"CatalogHitboxRegistry_{definition.name}");
                try
                {
                    FpgRoomInstance instance =
                        host.AddComponent<FpgRoomInstance>();
                    HitboxRegistry registry =
                        registryObject.AddComponent<HitboxRegistry>();
                    Assert.That(
                        registry.TryInitialize(out string registryError),
                        Is.True,
                        definition.name + ": " + registryError);
                    Assert.That(
                        instance.TryInitialize(definition, out string roomError),
                        Is.True,
                        definition.name + ": " + roomError);
                    Assert.That(
                        instance.TryRegisterCoverBlockers(
                            registry,
                            UnityAttackQuerySettings.Default,
                            out string registrationError),
                        Is.True,
                        definition.name + ": " + registrationError);

                    int expectedBlockerCount = 0;
                    for (int slotIndex = 0;
                        slotIndex < definition.CoverSlots.Count;
                        slotIndex++)
                    {
                        FpgRoomCoverSlot slot = definition.CoverSlots[slotIndex];
                        Assert.That(
                            instance.TryGetCoverView(
                                slot.MarkerId,
                                out FpgCoverEntityView view),
                            Is.True,
                            definition.name + ": " + slot.MarkerId);
                        for (int blockerIndex = 0;
                            blockerIndex < view.BlockingColliderCount;
                            blockerIndex++)
                        {
                            expectedBlockerCount++;
                            Assert.That(
                                view.TryGetBlockingCollider(
                                    blockerIndex,
                                    out Collider blocker),
                                Is.True,
                                definition.name + ": " + slot.MarkerId);
                            MeshCollider meshCollider = blocker as MeshCollider;
                            Assert.That(meshCollider, Is.Not.Null);
                            AssertMeshColliderCanBeRaycast(
                                meshCollider,
                                definition.name + ": " + slot.MarkerId);

                            GeometryId geometryId =
                                FpgRoomInstance.DeriveCoverGeometryId(
                                    definition.RoomId,
                                    slot.MarkerId,
                                    blockerIndex);
                            Assert.That(
                                registry.TryResolve(
                                    geometryId,
                                    out RegisteredHitbox registered),
                                Is.True,
                                definition.name + ": " + slot.MarkerId);
                            Assert.That(registered.Collider, Is.SameAs(blocker));
                            Assert.That(
                                registered.TargetKind,
                                Is.EqualTo(QueryTargetKind.EnvironmentBlocker));
                            Assert.That(
                                instance.TryResolveCoverId(
                                    geometryId,
                                    out string coverId),
                                Is.True,
                                definition.name + ": " + slot.MarkerId);
                            Assert.That(coverId, Is.EqualTo(slot.MarkerId));
                        }
                    }

                    Assert.That(expectedBlockerCount, Is.GreaterThan(0));
                    Assert.That(registry.Count, Is.EqualTo(expectedBlockerCount));
                }
                finally
                {
                    Object.DestroyImmediate(host);
                    Object.DestroyImmediate(registryObject);
                }
            }
        }

        private static void AssertMeshColliderCanBeRaycast(
            MeshCollider collider,
            string context)
        {
            Mesh mesh = collider.sharedMesh;
            Assert.That(mesh, Is.Not.Null, context);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Assert.That(vertices, Is.Not.Empty, context);
            Assert.That(triangles.Length, Is.GreaterThanOrEqualTo(3), context);

            Vector3 first = default(Vector3);
            Vector3 second = default(Vector3);
            Vector3 third = default(Vector3);
            Vector3 normal = default(Vector3);
            float largestTriangleAreaSquared = 0f;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 candidateFirst = collider.transform.TransformPoint(
                    vertices[triangles[index]]);
                Vector3 candidateSecond = collider.transform.TransformPoint(
                    vertices[triangles[index + 1]]);
                Vector3 candidateThird = collider.transform.TransformPoint(
                    vertices[triangles[index + 2]]);
                Vector3 candidateNormal = Vector3.Cross(
                    candidateSecond - candidateFirst,
                    candidateThird - candidateFirst);
                if (candidateNormal.sqrMagnitude <= largestTriangleAreaSquared)
                {
                    continue;
                }

                first = candidateFirst;
                second = candidateSecond;
                third = candidateThird;
                normal = candidateNormal;
                largestTriangleAreaSquared = candidateNormal.sqrMagnitude;
            }

            Assert.That(
                largestTriangleAreaSquared,
                Is.GreaterThan(0.000000000001f),
                context);
            normal.Normalize();

            Vector3 triangleCenter = (first + second + third) / 3f;
            Ray frontRay = new Ray(triangleCenter + normal, -normal);
            Ray backRay = new Ray(triangleCenter - normal, normal);
            bool wasEnabled = collider.enabled;
            List<GameObject> activatedAncestors = new List<GameObject>();
            Transform ancestor = collider.transform;
            while (ancestor != null)
            {
                if (!ancestor.gameObject.activeSelf)
                {
                    ancestor.gameObject.SetActive(true);
                    activatedAncestors.Add(ancestor.gameObject);
                }

                ancestor = ancestor.parent;
            }

            collider.enabled = true;
            Physics.SyncTransforms();
            try
            {
                Assert.That(
                    collider.Raycast(frontRay, out RaycastHit hit, 2f)
                    || collider.Raycast(backRay, out hit, 2f),
                    Is.True,
                    context);
                Assert.That(hit.collider, Is.SameAs(collider), context);
            }
            finally
            {
                collider.enabled = wasEnabled;
                for (int index = activatedAncestors.Count - 1;
                    index >= 0;
                    index--)
                {
                    activatedAncestors[index].SetActive(false);
                }
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
            Vector3 playerReachableLocalPosition,
            Vector3 playerLeftPeekLocalPosition,
            Vector3 playerRightPeekLocalPosition,
            bool isStartingCover)
        {
            Assert.That(slot.MarkerId, Is.EqualTo(markerId));
            Assert.That(AssetDatabase.GetAssetPath(slot.Prefab), Is.EqualTo(prefabPath));
            Assert.That(
                AssetDatabase.GetAssetPath(slot.CameraProfile),
                Is.EqualTo(cameraPath));
            Assert.That(slot.LocalPosition, Is.EqualTo(localPosition));
            Assert.That(slot.LocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(
                slot.PlayerReachableLocalPosition,
                Is.EqualTo(playerReachableLocalPosition));
            Assert.That(slot.PlayerReachableLocalEulerAngles, Is.EqualTo(Vector3.zero));
            Assert.That(
                slot.PlayerLeftPeekLocalPosition,
                Is.EqualTo(playerLeftPeekLocalPosition));
            Assert.That(
                slot.PlayerRightPeekLocalPosition,
                Is.EqualTo(playerRightPeekLocalPosition));
            Assert.That(slot.MaxDurability, Is.EqualTo(100));
            Assert.That(slot.IsStartingCover, Is.EqualTo(isStartingCover));
        }

        private static void AssertCoverStageState(
            FpgCoverEntityView view,
            bool destroyed,
            int expectedStageCount,
            int expectedActiveStageIndex)
        {
            SerializedObject serialized = new SerializedObject(view);
            GameObject destroyedRoot = serialized.FindProperty("destroyedRoot")
                .objectReferenceValue as GameObject;
            Assert.That(view.IsDestroyed, Is.EqualTo(destroyed));
            Assert.That(destroyedRoot.activeSelf, Is.EqualTo(destroyed));
            Assert.That(view.HealthStageCount, Is.EqualTo(expectedStageCount));
            Assert.That(view.BlockingColliderCount, Is.EqualTo(expectedStageCount));
            Assert.That(
                view.ActiveHealthStageIndex,
                Is.EqualTo(expectedActiveStageIndex));

            SerializedProperty stages = serialized.FindProperty("healthStages");
            Transform activeRoot = null;
            for (int index = 0; index < stages.arraySize; index++)
            {
                GameObject stageRoot = stages.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("visualRoot")
                    .objectReferenceValue as GameObject;
                Assert.That(stageRoot, Is.Not.Null);
                bool active = index == expectedActiveStageIndex;
                Assert.That(stageRoot.activeSelf, Is.EqualTo(active));
                if (active)
                {
                    activeRoot = stageRoot.transform;
                }
            }

            for (int index = 0; index < view.BlockingColliderCount; index++)
            {
                Assert.That(
                    view.TryGetBlockingCollider(index, out Collider blocker),
                    Is.True);
                Assert.That(blocker, Is.TypeOf<MeshCollider>());
                bool enabled = !destroyed
                    && activeRoot != null
                    && blocker.transform.IsChildOf(activeRoot);
                Assert.That(blocker.enabled, Is.EqualTo(enabled));
            }
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required asset is missing: {path}");
            return asset;
        }
    }
}
