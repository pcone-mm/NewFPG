using System;
using System.IO;
using System.Reflection;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgRoomAuthoringSafetyTests
    {
        private const string ForestRoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";
        private const string ForestScenePath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest.unity";

        [Test]
        public void SynchronizeAllIgnoresUnassignedDraftRooms()
        {
            string folder = CreateTemporaryFolder();
            try
            {
                FpgRoomDefinition draft =
                    ScriptableObject.CreateInstance<FpgRoomDefinition>();
                draft.name = "DraftRoom";
                AssetDatabase.CreateAsset(
                    draft,
                    folder + "/DraftRoom.asset");

                Assert.That(
                    FpgRoomArtSceneEditorUtility.TrySynchronizeAll(
                        out string error),
                    Is.True,
                    error);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void BindingRepairRefusesDirtyLoadedSceneWithoutChangingIt()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/DirtyCopied.unity";
            string roomPath = folder + "/DirtyCopiedRoom.asset";
            Scene scene = default;
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                FpgRoomDefinition room =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                SetArtSceneReference(
                    room,
                    AssetDatabase.AssetPathToGUID(scenePath),
                    scenePath);
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssetIfDirty(room);

                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
                FpgRoomArtRoot root = scene.GetRootGameObjects()[0]
                    .GetComponent<FpgRoomArtRoot>();
                FpgRoomDefinition originalBinding = root.RoomDefinition;
                EditorSceneManager.MarkSceneDirty(scene);

                Assert.That(
                    FpgRoomAuthoringOperations.TryBindArtSceneRoot(
                        room,
                        out string error),
                    Is.False);
                StringAssert.Contains("unsaved changes", error);
                Assert.That(root.RoomDefinition, Is.SameAs(originalBinding));
                Assert.That(scene.isLoaded, Is.True);
                Assert.That(scene.isDirty, Is.True);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void PublicValidationRejectsDirtyLoadedSceneWithoutSavingOrClosingIt()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/DirtyValidation.unity";
            string roomPath = folder + "/DirtyValidationRoom.asset";
            Scene scene = default;
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                FpgRoomDefinition room =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                SetArtSceneReference(
                    room,
                    AssetDatabase.AssetPathToGUID(scenePath),
                    scenePath);
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssetIfDirty(room);
                Assert.That(
                    FpgRoomAuthoringOperations.TryBindArtSceneRoot(
                        room,
                        out string bindingError),
                    Is.True,
                    bindingError);

                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
                FpgRoomArtRoot root = scene.GetRootGameObjects()[0]
                    .GetComponent<FpgRoomArtRoot>();
                Assert.That(root.RoomDefinition, Is.SameAs(room));
                EditorSceneManager.MarkSceneDirty(scene);

                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string error),
                    Is.False);
                StringAssert.Contains("unsaved changes", error);
                Assert.That(scene.isLoaded, Is.True);
                Assert.That(scene.isDirty, Is.True);
                Assert.That(root.RoomDefinition, Is.SameAs(room));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void PairCleanupRestoresSceneWhenRoomDeletionIsRejected()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/CleanupScene.unity";
            string roomPath = folder + "/CleanupRoom.asset";
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                string originalSceneGuid =
                    AssetDatabase.AssetPathToGUID(scenePath);
                string originalRoomGuid =
                    AssetDatabase.AssetPathToGUID(roomPath);
                MethodInfo cleanup = typeof(FpgRoomAuthoringOperations)
                    .GetMethod(
                        "TryDeleteCreatedAssetsAtomically",
                        BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(cleanup, Is.Not.Null);

                FpgRoomAuthoringSafetyDeleteGuard.BlockedPath = roomPath;
                FpgRoomAuthoringSafetyDeleteGuard.DeleteMetaBeforeRejecting =
                    true;
                object[] arguments =
                {
                    roomPath,
                    scenePath,
                    null
                };
                bool deleted = (bool)cleanup.Invoke(null, arguments);

                Assert.That(deleted, Is.False);
                StringAssert.Contains(
                    "restored as an intact pair",
                    arguments[2] as string);
                Assert.That(
                    AssetDatabase.LoadMainAssetAtPath(roomPath),
                    Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadMainAssetAtPath(scenePath),
                    Is.Not.Null);
                Assert.That(
                    AssetDatabase.AssetPathToGUID(roomPath),
                    Is.EqualTo(originalRoomGuid));
                Assert.That(
                    AssetDatabase.AssetPathToGUID(scenePath),
                    Is.EqualTo(originalSceneGuid));
            }
            finally
            {
                FpgRoomAuthoringSafetyDeleteGuard.BlockedPath = null;
                FpgRoomAuthoringSafetyDeleteGuard.DeleteMetaBeforeRejecting =
                    false;
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RegistrationFailureDoesNotMutateProductionConfiguration()
        {
            string folder = CreateTemporaryFolder();
            string roomPath = folder + "/DuplicateIdentity.asset";
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                FpgRoomDefinition duplicateIdentity =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                FpgRoomCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(
                        FpgRoomArtSceneEditorUtility.RoomCatalogPath);
                Assert.That(catalog, Is.Not.Null);
                FpgRoomDefinition[] originalRooms =
                    new FpgRoomDefinition[catalog.Rooms.Count];
                for (int index = 0; index < originalRooms.Length; index++)
                {
                    originalRooms[index] = catalog.Rooms[index];
                }

                EditorBuildSettingsScene[] originalScenes =
                    EditorBuildSettings.scenes;

                Assert.That(
                    FpgRoomAuthoringOperations.TryRegisterRoomForProduction(
                        duplicateIdentity,
                        out string error),
                    Is.False);
                StringAssert.Contains(
                    "duplicate room id",
                    error.ToLowerInvariant());
                CollectionAssert.AreEqual(originalRooms, catalog.Rooms);
                AssertBuildSettingsEqual(
                    originalScenes,
                    EditorBuildSettings.scenes);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void AssertBuildSettingsEqual(
            EditorBuildSettingsScene[] expected,
            EditorBuildSettingsScene[] actual)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].path, Is.EqualTo(expected[index].path));
                Assert.That(
                    actual[index].enabled,
                    Is.EqualTo(expected[index].enabled));
            }
        }

        private static string CreateTemporaryFolder()
        {
            const string parent = "Assets/FPGDemo/Tests/EditMode";
            string name = "__RoomAuthoringSafetyTemp_"
                + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder(parent, name), Is.Not.Empty);
            return parent + "/" + name;
        }

        private static void SetArtSceneReference(
            FpgRoomDefinition room,
            string guid,
            string path)
        {
            SerializedObject data = new SerializedObject(room);
            SerializedProperty artScene = data.FindProperty("artScene");
            artScene.FindPropertyRelative("sceneGuid").stringValue = guid;
            artScene.FindPropertyRelative("scenePath").stringValue = path;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    internal sealed class FpgRoomAuthoringSafetyDeleteGuard
        : AssetModificationProcessor
    {
        internal static string BlockedPath { get; set; }

        internal static bool DeleteMetaBeforeRejecting { get; set; }

        private static AssetDeleteResult OnWillDeleteAsset(
            string assetPath,
            RemoveAssetOptions options)
        {
            if (!string.Equals(
                    assetPath,
                    BlockedPath,
                    StringComparison.Ordinal))
            {
                return AssetDeleteResult.DidNotDelete;
            }

            if (DeleteMetaBeforeRejecting)
            {
                string metaPath = Path.GetFullPath(assetPath + ".meta");
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }

            return AssetDeleteResult.FailedDelete;
        }
    }
}
