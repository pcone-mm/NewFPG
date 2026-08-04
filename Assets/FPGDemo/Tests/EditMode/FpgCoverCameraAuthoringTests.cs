using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgCoverCameraAuthoringTests
    {
        private const string CameraRoot =
            "Assets/FPGDemo/Config/Level/CameraProfiles";
        private const string DefaultTemplatePath = CameraRoot
            + "/FPG_Default_CoverCamera.asset";
        private const string RoomCatalogPath =
            "Assets/FPGDemo/Config/Level/FPG_RoomCatalog.asset";
        private const string ThreeCPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/"
            + "FPG_Fei_ThreeC.asset";

        private string temporaryFolder;

        [SetUp]
        public void SetUp()
        {
            const string parent = "Assets/FPGDemo/Tests/EditMode";
            string name = "__CoverCameraAuthoringTemp_"
                + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder(parent, name), Is.Not.Empty);
            temporaryFolder = parent + "/" + name;
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(temporaryFolder))
            {
                AssetDatabase.DeleteAsset(temporaryFolder);
            }
        }

        [Test]
        public void ProductionRoomsUseIndependentValidCoverProfiles()
        {
            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(RoomCatalogPath);
            FpgCoverCameraProfile template =
                AssetDatabase.LoadAssetAtPath<FpgCoverCameraProfile>(
                    DefaultTemplatePath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(template, Is.Not.Null);
            Assert.That(template.TryValidate(out string templateError),
                Is.True, templateError);
            Assert.That(catalog.TryValidate(out string catalogError),
                Is.True, catalogError);
            CollectionAssert.DoesNotContain(
                FpgCoverCameraProfileAuthoring.FindOrphanProfiles(),
                template);

            int coverCount = 0;
            for (int roomIndex = 0;
                 roomIndex < catalog.Rooms.Count;
                 roomIndex++)
            {
                FpgRoomDefinition room = catalog.Rooms[roomIndex];
                for (int coverIndex = 0;
                     coverIndex < room.CoverSlots.Count;
                     coverIndex++)
                {
                    FpgRoomCoverSlot cover = room.CoverSlots[coverIndex];
                    FpgCoverCameraProfile profile = cover.CameraProfile;
                    Assert.That(profile, Is.Not.Null,
                        $"{room.name}/{cover.MarkerId}");
                    Assert.That(profile.TryValidate(out string error),
                        Is.True, error);
                    Assert.That(AssetDatabase.GetAssetPath(profile),
                        Does.StartWith(CameraRoot + "/"));
                    coverCount++;
                }
            }

            Assert.That(coverCount, Is.GreaterThan(0));
        }

        [Test]
        public void ExplicitSharingCanBeCountedAndMadeUnique()
        {
            FpgCoverCameraProfile shared = CreateProfileAsset(
                temporaryFolder + "/Shared.asset",
                67f);
            string roomAssetPath = temporaryFolder + "/SharedRoom.asset";
            FpgRoomDefinition room =
                ScriptableObject.CreateInstance<FpgRoomDefinition>();
            AssetDatabase.CreateAsset(room, roomAssetPath);
            string profileFolder = CameraRoot + "/" + room.name;
            try
            {
                SerializedObject roomData = new SerializedObject(room);
                SerializedProperty covers = roomData.FindProperty("coverSlots");
                covers.arraySize = 2;
                for (int index = 0; index < covers.arraySize; index++)
                {
                    SerializedProperty cover =
                        covers.GetArrayElementAtIndex(index);
                    cover.FindPropertyRelative("markerId").stringValue =
                        "cover-" + index;
                    cover.FindPropertyRelative("cameraProfile")
                        .objectReferenceValue = shared;
                }

                roomData.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    FpgCoverCameraProfileAuthoring.CountReferences(shared),
                    Is.EqualTo(2));

                Assert.That(
                    FpgCoverCameraProfileAuthoring.TryMakeCoverProfileUnique(
                        room,
                        1,
                        out FpgCoverCameraProfile unique,
                        out string error),
                    Is.True,
                    error);
                Assert.That(unique, Is.Not.SameAs(shared));
                Assert.That(
                    FpgCoverCameraProfileAuthoring.CountReferences(shared),
                    Is.EqualTo(1));
                Assert.That(
                    FpgCoverCameraProfileAuthoring.CountReferences(unique),
                    Is.EqualTo(1));
            }
            finally
            {
                AssetDatabase.DeleteAsset(profileFolder);
            }
        }

        [Test]
        public void OrphanAuditFindsProfilesOutsideTheDefaultRoot()
        {
            FpgCoverCameraProfile orphan = CreateProfileAsset(
                temporaryFolder + "/ExternalOrphan.asset",
                62f);

            CollectionAssert.Contains(
                FpgCoverCameraProfileAuthoring.FindOrphanProfiles(),
                orphan);
        }

        [Test]
        public void PreviewCleanupMovesSelectionBackToTheRoom()
        {
            UnityEngine.Object previousSelection = Selection.activeObject;
            FpgRoomDefinition room =
                ScriptableObject.CreateInstance<FpgRoomDefinition>();
            GameObject previewRoot = new GameObject("Preview Root");
            GameObject previewChild = new GameObject("Preview Child");
            previewChild.transform.SetParent(previewRoot.transform, false);
            Type toolType = typeof(FpgCoverCameraProfileAuthoring).Assembly
                .GetType(
                    "FPG.Demo.Editor.LevelAuthoring.FpgRoomSceneTool",
                    true);
            IDisposable tool = null;
            try
            {
                tool = Activator.CreateInstance(toolType, true) as IDisposable;
                Assert.That(tool, Is.Not.Null);
                SetPrivateField(tool, "room", room);
                SetPrivateField(tool, "cameraPreviewRoot", previewRoot);
                Selection.activeObject = previewChild;

                InvokePrivate(tool, "DestroyCameraPreviewObjects");

                Assert.That(Selection.activeObject, Is.SameAs(room));
                Assert.That(previewRoot == null, Is.True);
            }
            finally
            {
                tool?.Dispose();
                if (previewRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(previewRoot);
                }

                UnityEngine.Object.DestroyImmediate(room);
                Selection.activeObject = previousSelection;
            }
        }

        [Test]
        public void DuplicateCoverOffsetsReachableAndBothPeekPositions()
        {
            const string SourceRoomPath =
                "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";
            string roomPath = temporaryFolder + "/MarkerRoom.asset";
            string profileFolder = CameraRoot + "/MarkerRoom";
            IDisposable tool = null;
            try
            {
                FpgRoomDefinition sourceRoom =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                        SourceRoomPath);
                Assert.That(sourceRoom, Is.Not.Null, SourceRoomPath);
                FpgRoomDefinition room =
                    UnityEngine.Object.Instantiate(sourceRoom);
                room.name = "MarkerRoom";
                AssetDatabase.CreateAsset(room, roomPath);
                FpgRoomCoverSlot source = room.CoverSlots[0];
                Vector3 sourceMarkerPosition = source.LocalPosition;
                Vector3 sourceReachable = source.PlayerReachableLocalPosition;
                Vector3 sourceLeft = source.PlayerLeftPeekLocalPosition;
                Vector3 sourceRight = source.PlayerRightPeekLocalPosition;

                Type toolType = typeof(FpgCoverCameraProfileAuthoring).Assembly
                    .GetType(
                        "FPG.Demo.Editor.LevelAuthoring.FpgRoomSceneTool",
                        true);
                Type handleType = typeof(FpgCoverCameraProfileAuthoring).Assembly
                    .GetType(
                        "FPG.Demo.Editor.LevelAuthoring.FpgRoomMarkerHandle",
                        true);
                tool = Activator.CreateInstance(toolType, true) as IDisposable;
                Assert.That(tool, Is.Not.Null);
                ConstructorInfo[] handleConstructors =
                    handleType.GetConstructors(
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic);
                Assert.That(handleConstructors, Has.Length.EqualTo(1));
                Type markerKindType = handleConstructors[0]
                    .GetParameters()[0]
                    .ParameterType;
                object handle = handleConstructors[0].Invoke(
                    new object[]
                    {
                        Enum.ToObject(
                            markerKindType,
                            (int)FPG.Demo.Unity.FpgRoomMarkerKind.Cover),
                        0,
                        source.MarkerId,
                        source.DisplayName
                    });
                SetPrivateField(tool, "room", room);
                SetPrivateField(tool, "selectedMarker", handle);
                PropertyInfo gridSnap = toolType.GetProperty(
                    "GridSnap",
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);
                Assert.That(gridSnap, Is.Not.Null);
                gridSnap.SetValue(tool, 0.5f);

                InvokePrivate(tool, "DuplicateSelectedMarker");

                Assert.That(room.CoverSlots.Count, Is.EqualTo(4));
                FpgRoomCoverSlot duplicate = room.CoverSlots[0];
                Vector3 offset = new Vector3(0.5f, 0f, 0f);
                Assert.That(duplicate.MarkerId, Is.Not.EqualTo(source.MarkerId));
                Assert.That(
                    duplicate.LocalPosition,
                    Is.EqualTo(sourceMarkerPosition + offset));
                Assert.That(
                    duplicate.PlayerReachableLocalPosition,
                    Is.EqualTo(sourceReachable + offset));
                Assert.That(
                    duplicate.PlayerLeftPeekLocalPosition,
                    Is.EqualTo(sourceLeft + offset));
                Assert.That(
                    duplicate.PlayerRightPeekLocalPosition,
                    Is.EqualTo(sourceRight + offset));
                Assert.That(duplicate.IsStartingCover, Is.False);
            }
            finally
            {
                tool?.Dispose();
                AssetDatabase.DeleteAsset(profileFolder);
            }
        }

        [Test]
        public void FormalThreeCNoLongerSerializesStaticCameraComposition()
        {
            D0ThreeCProfile threeC =
                AssetDatabase.LoadAssetAtPath<D0ThreeCProfile>(ThreeCPath);
            Assert.That(threeC, Is.Not.Null);

            SerializedObject data = new SerializedObject(threeC);
            string[] retiredProperties =
            {
                "fixedPlayerViewportAnchor",
                "cameraFocusViewport",
                "cameraPivotLocalPosition",
                "cameraPivotLocalEulerAngles",
                "cameraLocalPosition",
                "cameraLocalEulerAngles",
                "cameraFieldOfView",
                "cameraNearClipPlane",
                "cameraFarClipPlane"
            };
            for (int index = 0; index < retiredProperties.Length; index++)
            {
                Assert.That(data.FindProperty(retiredProperties[index]),
                    Is.Null, retiredProperties[index]);
            }
        }

        [Test]
        public void CloneForCoverAndCopyValuesKeepAssetsIndependent()
        {
            FpgCoverCameraProfile source = CreateProfileAsset(
                temporaryFolder + "/Template.asset",
                73f);
            FpgRoomDefinition room = ScriptableObject.CreateInstance<
                FpgRoomDefinition>();
            AssetDatabase.CreateAsset(room, temporaryFolder + "/Room.asset");
            string cloneFolder = CameraRoot + "/" + room.name;
            try
            {
                Assert.That(FpgCoverCameraProfileAuthoring.TryCloneForCover(
                    source,
                    room,
                    "cover-a",
                    out FpgCoverCameraProfile clone,
                    out string cloneError), Is.True, cloneError);
                Assert.That(clone, Is.Not.SameAs(source));
                Assert.That(clone.FieldOfView, Is.EqualTo(73f));
                Assert.That(
                    AssetDatabase.GetAssetPath(clone),
                    Does.StartWith(cloneFolder + "/"));

                SetFloat(clone, "fieldOfView", 91f);
                Assert.That(source.FieldOfView, Is.EqualTo(73f));
                Assert.That(clone.FieldOfView, Is.EqualTo(91f));

                Assert.That(FpgCoverCameraProfileAuthoring.TryCopyValues(
                    source,
                    clone,
                    "Test Restore Camera Template",
                    out string copyError), Is.True, copyError);
                Assert.That(clone.FieldOfView, Is.EqualTo(73f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(cloneFolder);
            }
        }

        [Test]
        public void ClipboardRoundTripCopiesEveryProfileSetting()
        {
            FpgCoverCameraProfile source = CreateProfileAsset(
                temporaryFolder + "/ClipboardSource.asset",
                73f);
            FpgCoverCameraProfile destination = CreateProfileAsset(
                temporaryFolder + "/ClipboardDestination.asset",
                41f);
            SerializedObject sourceData = new SerializedObject(source);
            sourceData.FindProperty("designerNotes").stringValue =
                "clipboard profile notes";
            sourceData.FindProperty("cameraRigLocalPosition").vector3Value =
                new Vector3(1f, 2f, 3f);
            sourceData.FindProperty("cameraRigLocalEulerAngles").vector3Value =
                new Vector3(4f, 5f, 6f);
            sourceData.FindProperty("cameraLocalPosition").vector3Value =
                new Vector3(7f, 8f, 9f);
            sourceData.FindProperty("cameraLocalEulerAngles").vector3Value =
                new Vector3(10f, 11f, 12f);
            sourceData.FindProperty("fieldOfView").floatValue = 73f;
            sourceData.FindProperty("nearClipPlane").floatValue = 0.25f;
            sourceData.FindProperty("farClipPlane").floatValue = 135f;
            sourceData.FindProperty("playerViewportAnchor").vector2Value =
                new Vector2(0.3f, 0.4f);
            sourceData.FindProperty("focusViewportAnchor").vector2Value =
                new Vector2(0.6f, 0.7f);
            sourceData.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                FpgCoverCameraProfileAuthoring.TryCreateClipboardText(
                    source,
                    out string clipboardText,
                    out string copyError),
                Is.True,
                copyError);
            Assert.That(
                FpgCoverCameraProfileAuthoring.TryPasteClipboardText(
                    clipboardText,
                    destination,
                    out string pasteError),
                Is.True,
                pasteError);

            Assert.That(destination.DesignerNotes,
                Is.EqualTo(source.DesignerNotes));
            Assert.That(destination.CameraRigLocalPosition,
                Is.EqualTo(source.CameraRigLocalPosition));
            Assert.That(destination.CameraRigLocalEulerAngles,
                Is.EqualTo(source.CameraRigLocalEulerAngles));
            Assert.That(destination.CameraLocalPosition,
                Is.EqualTo(source.CameraLocalPosition));
            Assert.That(destination.CameraLocalEulerAngles,
                Is.EqualTo(source.CameraLocalEulerAngles));
            Assert.That(destination.FieldOfView,
                Is.EqualTo(source.FieldOfView));
            Assert.That(destination.NearClipPlane,
                Is.EqualTo(source.NearClipPlane));
            Assert.That(destination.FarClipPlane,
                Is.EqualTo(source.FarClipPlane));
            Assert.That(destination.PlayerViewportAnchor,
                Is.EqualTo(source.PlayerViewportAnchor));
            Assert.That(destination.FocusViewportAnchor,
                Is.EqualTo(source.FocusViewportAnchor));
        }

        [Test]
        public void RoomDuplicateClonesEachSourceProfileOnce()
        {
            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(RoomCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Rooms.Count, Is.GreaterThan(0));

            FpgRoomDefinition duplicate = UnityEngine.Object.Instantiate(
                catalog.Rooms[0]);
            try
            {
                SerializedObject roomData = new SerializedObject(duplicate);
                SerializedProperty covers = roomData.FindProperty("coverSlots");
                Assert.That(covers.arraySize, Is.GreaterThanOrEqualTo(3));
                FpgCoverCameraProfile sharedSource = covers
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("cameraProfile")
                    .objectReferenceValue as FpgCoverCameraProfile;
                FpgCoverCameraProfile separateSource = covers
                    .GetArrayElementAtIndex(2)
                    .FindPropertyRelative("cameraProfile")
                    .objectReferenceValue as FpgCoverCameraProfile;
                covers.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("cameraProfile")
                    .objectReferenceValue = sharedSource;
                roomData.ApplyModifiedPropertiesWithoutUndo();

                List<string> createdPaths = new List<string>();
                MethodInfo method = typeof(FpgCoverCameraProfileAuthoring)
                    .GetMethod(
                        "TryCloneProfilesForRoomDuplicate",
                        BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                object[] arguments =
                {
                    duplicate,
                    temporaryFolder + "/RoomCopy.asset",
                    createdPaths,
                    string.Empty
                };
                Assert.That((bool)method.Invoke(null, arguments), Is.True,
                    arguments[3] as string);

                FpgCoverCameraProfile first = duplicate.CoverSlots[0]
                    .CameraProfile;
                FpgCoverCameraProfile second = duplicate.CoverSlots[1]
                    .CameraProfile;
                FpgCoverCameraProfile third = duplicate.CoverSlots[2]
                    .CameraProfile;
                Assert.That(first, Is.SameAs(second));
                Assert.That(first, Is.Not.SameAs(sharedSource));
                Assert.That(third, Is.Not.SameAs(separateSource));
                Assert.That(third, Is.Not.SameAs(first));
                Assert.That(createdPaths.Count, Is.EqualTo(2));
            }
            finally
            {
                AssetDatabase.DeleteAsset(CameraRoot + "/RoomCopy");
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        private static FpgCoverCameraProfile CreateProfileAsset(
            string path,
            float fieldOfView)
        {
            FpgCoverCameraProfile profile =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            SetFloat(profile, "fieldOfView", fieldOfView);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        private static void SetFloat(
            FpgCoverCameraProfile profile,
            string propertyName,
            float value)
        {
            SerializedObject data = new SerializedObject(profile);
            data.FindProperty(propertyName).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
