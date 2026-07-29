using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    /// <summary>
    /// Explicit, user-triggered authoring operations that keep a RoomDefinition,
    /// its Art Scene root, the production catalog, and Build Settings in sync.
    /// </summary>
    public static class FpgRoomAuthoringOperations
    {
        private const string EditorBuildSettingsAssetPath =
            "ProjectSettings/EditorBuildSettings.asset";

        public static bool TryDuplicateRoomWithArtScene(
            FpgRoomDefinition sourceRoom,
            string requestedRoomAssetPath,
            bool registerForProduction,
            out FpgRoomDefinition duplicateRoom,
            out string error)
        {
            duplicateRoom = null;
            error = string.Empty;
            string roomAssetPath = string.Empty;
            string artScenePath = string.Empty;
            bool canDeleteCreatedAssets = true;

            if (sourceRoom == null)
            {
                error = "Cannot duplicate a missing RoomDefinition.";
                return false;
            }

            if (!TryValidateRoomAssetPath(
                    requestedRoomAssetPath,
                    out roomAssetPath,
                    out error))
            {
                return false;
            }

            if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    sourceRoom,
                    out error))
            {
                error = $"Source room '{sourceRoom.RoomId}' is invalid: {error}";
                return false;
            }

            if (EditorUtility.IsDirty(sourceRoom))
            {
                error =
                    "Save the source RoomDefinition before duplicating it.";
                return false;
            }

            Scene sourceScene = SceneManager.GetSceneByPath(
                sourceRoom.ArtScenePath);
            if (sourceScene.IsValid() && sourceScene.isLoaded && sourceScene.isDirty)
            {
                error =
                    "Save the source Art Scene before duplicating the room.";
                return false;
            }

            if (!FpgRoomArtSceneContractValidator.TryValidateScene(
                    sourceRoom,
                    out error))
            {
                error = $"Source Art Scene is invalid: {error}";
                return false;
            }

            string sourceSceneDirectory = Path.GetDirectoryName(
                    sourceRoom.ArtScenePath)
                ?.Replace('\\', '/');
            string sourceSceneName = Path.GetFileNameWithoutExtension(
                sourceRoom.ArtScenePath);
            if (string.IsNullOrWhiteSpace(sourceSceneDirectory)
                || string.IsNullOrWhiteSpace(sourceSceneName))
            {
                error = "Source Art Scene path is not a valid Assets-relative path.";
                return false;
            }

            artScenePath = AssetDatabase.GenerateUniqueAssetPath(
                sourceSceneDirectory + "/" + sourceSceneName + "_Copy.unity");
            roomAssetPath = AssetDatabase.GenerateUniqueAssetPath(roomAssetPath);

            try
            {
                if (!AssetDatabase.CopyAsset(
                        sourceRoom.ArtScenePath,
                        artScenePath))
                {
                    error = $"Could not copy Art Scene to '{artScenePath}'.";
                    return false;
                }

                duplicateRoom = UnityEngine.Object.Instantiate(sourceRoom);
                duplicateRoom.name = Path.GetFileNameWithoutExtension(
                    roomAssetPath);
                SerializedObject roomData = new SerializedObject(duplicateRoom);
                roomData.FindProperty("roomId").stringValue = GenerateRoomId();
                SerializedProperty displayName = roomData.FindProperty(
                    "displayName");
                displayName.stringValue = string.IsNullOrWhiteSpace(
                        displayName.stringValue)
                    ? duplicateRoom.name
                    : displayName.stringValue + " Copy";
                SerializedProperty artScene = roomData.FindProperty("artScene");
                artScene.FindPropertyRelative("sceneGuid").stringValue =
                    AssetDatabase.AssetPathToGUID(artScenePath);
                artScene.FindPropertyRelative("scenePath").stringValue =
                    artScenePath;
                roomData.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(duplicateRoom, roomAssetPath);
                AssetDatabase.SaveAssetIfDirty(duplicateRoom);

                if (!TryBindArtSceneRoot(duplicateRoom, out error))
                {
                    return false;
                }

                if (!FpgRoomArtSceneContractValidator.TryValidateScene(
                        duplicateRoom,
                        out error))
                {
                    return false;
                }

                if (registerForProduction
                    && !TryRegisterRoomForProductionCore(
                        duplicateRoom,
                        out canDeleteCreatedAssets,
                        out error))
                {
                    if (!canDeleteCreatedAssets)
                    {
                        error +=
                            " Registration rollback was incomplete; the created RoomDefinition and Art Scene were preserved to avoid dangling production references.";
                    }
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    CleanupFailedDuplication(
                        roomAssetPath,
                        artScenePath,
                        ref canDeleteCreatedAssets,
                        ref duplicateRoom,
                        ref error);
                }
            }
        }

        private static void CleanupFailedDuplication(
            string roomAssetPath,
            string artScenePath,
            ref bool canDeleteCreatedAssets,
            ref FpgRoomDefinition duplicateRoom,
            ref string error)
        {
            try
            {
                Scene loadedDuplicate = SceneManager.GetSceneByPath(
                    artScenePath);
                if (loadedDuplicate.IsValid()
                    && loadedDuplicate.isLoaded
                    && !EditorSceneManager.CloseScene(loadedDuplicate, true))
                {
                    canDeleteCreatedAssets = false;
                    error +=
                        $" Could not close created Art Scene '{artScenePath}', so its assets were preserved.";
                }
            }
            catch (Exception exception)
            {
                canDeleteCreatedAssets = false;
                error +=
                    $" Could not close created Art Scene '{artScenePath}': {exception.GetBaseException().Message}. Its assets were preserved.";
            }

            if (canDeleteCreatedAssets
                && !TryDeleteCreatedAssetsAtomically(
                    roomAssetPath,
                    artScenePath,
                    out string cleanupError))
            {
                canDeleteCreatedAssets = false;
                error += " " + cleanupError;
            }

            try
            {
                if (canDeleteCreatedAssets
                    && duplicateRoom != null
                    && !AssetDatabase.Contains(duplicateRoom))
                {
                    UnityEngine.Object.DestroyImmediate(duplicateRoom);
                }

                if (canDeleteCreatedAssets)
                {
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception exception)
            {
                canDeleteCreatedAssets = false;
                error +=
                    " Cleanup refresh failed: "
                    + exception.GetBaseException().Message;
            }
            finally
            {
                duplicateRoom = null;
            }

            if (!canDeleteCreatedAssets
                && !error.Contains(
                    "preserved",
                    StringComparison.OrdinalIgnoreCase))
            {
                error +=
                    $" Cleanup did not complete; inspect '{roomAssetPath}' and '{artScenePath}'.";
            }
        }

        private static bool TryDeleteCreatedAssetsAtomically(
            string roomAssetPath,
            string artScenePath,
            out string error)
        {
            error = string.Empty;
            bool roomExists = AssetExists(roomAssetPath);
            bool sceneExists = AssetExists(artScenePath);
            if (!roomExists && !sceneExists)
            {
                return true;
            }

            if (!roomExists || !sceneExists)
            {
                string existingPath = roomExists
                    ? roomAssetPath
                    : artScenePath;
                try
                {
                    if (AssetDatabase.DeleteAsset(existingPath)
                        && !AssetExists(existingPath))
                    {
                        return true;
                    }

                    error =
                        $"Could not delete created asset '{existingPath}'; it was preserved.";
                    return false;
                }
                catch (Exception exception)
                {
                    error =
                        $"Could not delete created asset '{existingPath}': {exception.GetBaseException().Message}. It was preserved.";
                    return false;
                }
            }

            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                error =
                    "Could not resolve the Unity project root, so the created RoomDefinition and Art Scene were preserved.";
                return false;
            }

            string backupDirectory = Path.Combine(
                projectRoot,
                "Temp",
                "FpgRoomDuplicationCleanup_" + Guid.NewGuid().ToString("N"));
            string roomBackupPath = Path.Combine(
                backupDirectory,
                "RoomDefinition.asset");
            string sceneBackupPath = Path.Combine(
                backupDirectory,
                "ArtScene.unity");
            string roomAbsolutePath = ToAbsoluteProjectPath(
                projectRoot,
                roomAssetPath);
            string sceneAbsolutePath = ToAbsoluteProjectPath(
                projectRoot,
                artScenePath);
            string roomGuid = AssetDatabase.AssetPathToGUID(roomAssetPath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(artScenePath);

            try
            {
                Directory.CreateDirectory(backupDirectory);
                CopyAssetFiles(roomAbsolutePath, roomBackupPath);
                CopyAssetFiles(sceneAbsolutePath, sceneBackupPath);
            }
            catch (Exception exception)
            {
                TryDeleteBackupDirectory(backupDirectory);
                error =
                    $"Could not create a rollback backup: {exception.GetBaseException().Message}. The created RoomDefinition and Art Scene were preserved.";
                return false;
            }

            string deletionError = string.Empty;
            try
            {
                if (!AssetDatabase.DeleteAsset(artScenePath)
                    || AssetExists(artScenePath))
                {
                    deletionError =
                        $"Could not delete created Art Scene '{artScenePath}'.";
                }
                else if (!AssetDatabase.DeleteAsset(roomAssetPath)
                    || AssetExists(roomAssetPath))
                {
                    deletionError =
                        $"Could not delete created RoomDefinition '{roomAssetPath}'.";
                }
            }
            catch (Exception exception)
            {
                deletionError =
                    "Created asset deletion failed: "
                    + exception.GetBaseException().Message;
            }

            if (string.IsNullOrWhiteSpace(deletionError))
            {
                TryDeleteBackupDirectory(backupDirectory);
                return true;
            }

            if (TryRestoreAssetPair(
                    roomAssetPath,
                    roomAbsolutePath,
                    roomBackupPath,
                    roomGuid,
                    artScenePath,
                    sceneAbsolutePath,
                    sceneBackupPath,
                    sceneGuid,
                    out string restoreError))
            {
                TryDeleteBackupDirectory(backupDirectory);
                error =
                    deletionError
                    + " The created RoomDefinition and Art Scene were restored as an intact pair.";
                return false;
            }

            error =
                deletionError
                + " Automatic rollback also failed: "
                + restoreError
                + $". Exact recovery files were kept at '{backupDirectory}'.";
            return false;
        }

        private static bool TryRestoreAssetPair(
            string roomAssetPath,
            string roomAbsolutePath,
            string roomBackupPath,
            string expectedRoomGuid,
            string artScenePath,
            string sceneAbsolutePath,
            string sceneBackupPath,
            string expectedSceneGuid,
            out string error)
        {
            string lastAttemptError = string.Empty;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                List<string> failures = new List<string>();
                if (!TryRestoreAssetFiles(
                        roomAbsolutePath,
                        roomBackupPath,
                        out string roomRestoreError))
                {
                    failures.Add(
                        "RoomDefinition files: " + roomRestoreError);
                }
                if (!TryRestoreAssetFiles(
                        sceneAbsolutePath,
                        sceneBackupPath,
                        out string sceneRestoreError))
                {
                    failures.Add("Art Scene files: " + sceneRestoreError);
                }

                try
                {
                    AssetDatabase.Refresh(
                        ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception exception)
                {
                    failures.Add(
                        "AssetDatabase refresh: "
                        + exception.GetBaseException().Message);
                }

                bool roomRestored = AssetHasGuid(
                    roomAssetPath,
                    expectedRoomGuid);
                bool sceneRestored = AssetHasGuid(
                    artScenePath,
                    expectedSceneGuid);
                if (failures.Count == 0
                    && roomRestored && sceneRestored)
                {
                    error = string.Empty;
                    return true;
                }

                if (!roomRestored || !sceneRestored)
                {
                    failures.Add(
                        $"identity check (RoomDefinition={roomRestored}, Art Scene={sceneRestored})");
                }
                lastAttemptError =
                    $"attempt {attempt}: " + string.Join("; ", failures);
            }

            error = lastAttemptError;
            return false;
        }

        private static bool TryRestoreAssetFiles(
            string absoluteAssetPath,
            string backupAssetPath,
            out string error)
        {
            List<string> failures = new List<string>();
            if (!TryRestoreFile(
                    backupAssetPath + ".meta",
                    absoluteAssetPath + ".meta",
                    out string metaError))
            {
                failures.Add("meta: " + metaError);
            }
            if (!TryRestoreFile(
                    backupAssetPath,
                    absoluteAssetPath,
                    out string assetError))
            {
                failures.Add("asset: " + assetError);
            }

            error = string.Join("; ", failures);
            return failures.Count == 0;
        }

        private static bool TryRestoreFile(
            string sourcePath,
            string destinationPath,
            out string error)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    error = $"backup file '{sourcePath}' is missing";
                    return false;
                }

                if (File.Exists(destinationPath)
                    && FilesHaveSameContents(sourcePath, destinationPath))
                {
                    error = string.Empty;
                    return true;
                }

                string destinationDirectory = Path.GetDirectoryName(
                    destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                File.Copy(sourcePath, destinationPath, true);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool FilesHaveSameContents(
            string leftPath,
            string rightPath)
        {
            FileInfo leftInfo = new FileInfo(leftPath);
            FileInfo rightInfo = new FileInfo(rightPath);
            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }

            const int bufferSize = 81920;
            byte[] leftBuffer = new byte[bufferSize];
            byte[] rightBuffer = new byte[bufferSize];
            using (FileStream left = File.OpenRead(leftPath))
            using (FileStream right = File.OpenRead(rightPath))
            {
                while (true)
                {
                    int leftRead = left.Read(
                        leftBuffer,
                        0,
                        leftBuffer.Length);
                    int rightRead = right.Read(
                        rightBuffer,
                        0,
                        rightBuffer.Length);
                    if (leftRead != rightRead)
                    {
                        return false;
                    }
                    if (leftRead == 0)
                    {
                        return true;
                    }

                    for (int index = 0; index < leftRead; index++)
                    {
                        if (leftBuffer[index] != rightBuffer[index])
                        {
                            return false;
                        }
                    }
                }
            }
        }

        private static void CopyAssetFiles(
            string sourceAssetPath,
            string destinationAssetPath)
        {
            string sourceMetaPath = sourceAssetPath + ".meta";
            if (!File.Exists(sourceAssetPath) || !File.Exists(sourceMetaPath))
            {
                throw new FileNotFoundException(
                    $"Asset or meta file is missing for '{sourceAssetPath}'.");
            }

            string destinationDirectory = Path.GetDirectoryName(
                destinationAssetPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceAssetPath, destinationAssetPath, true);
            File.Copy(sourceMetaPath, destinationAssetPath + ".meta", true);
        }

        private static string ToAbsoluteProjectPath(
            string projectRoot,
            string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static bool AssetHasGuid(
            string assetPath,
            string expectedGuid)
        {
            return AssetExists(assetPath)
                && !string.IsNullOrWhiteSpace(expectedGuid)
                && string.Equals(
                    AssetDatabase.AssetPathToGUID(assetPath),
                    expectedGuid,
                    StringComparison.Ordinal);
        }

        private static bool AssetExists(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
        }

        private static void TryDeleteBackupDirectory(string backupDirectory)
        {
            try
            {
                if (Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not remove temporary duplication rollback backup '{backupDirectory}': {exception.GetBaseException().Message}");
            }
        }

        public static bool TryBindArtSceneRoot(
            FpgRoomDefinition room,
            out string error)
        {
            error = string.Empty;
            if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    room,
                    out error))
            {
                return false;
            }

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(room.ArtScenePath);
            bool openedForRepair = !scene.IsValid() || !scene.isLoaded;
            FpgRoomArtRoot root = null;
            FpgRoomDefinition previousBinding = null;
            bool bindingChanged = false;
            try
            {
                if (openedForRepair)
                {
                    scene = EditorSceneManager.OpenScene(
                        room.ArtScenePath,
                        OpenSceneMode.Additive);
                }

                if (!TryFindArtRoot(scene, out root, out error))
                {
                    return CompleteArtSceneOperation(
                        false,
                        openedForRepair,
                        scene,
                        previousActive,
                        ref error);
                }

                if (scene.isDirty)
                {
                    error =
                        $"Art Scene '{scene.path}' has unsaved changes; save it before repairing the RoomDefinition binding.";
                    return CompleteArtSceneOperation(
                        false,
                        openedForRepair,
                        scene,
                        previousActive,
                        ref error);
                }

                previousBinding = root.RoomDefinition;
                if (previousBinding != room)
                {
                    SerializedObject rootData = new SerializedObject(root);
                    SerializedProperty roomProperty = rootData.FindProperty(
                        "roomDefinition");
                    if (roomProperty == null)
                    {
                        error =
                            "FpgRoomArtRoot has no serialized roomDefinition field.";
                        return CompleteArtSceneOperation(
                            false,
                            openedForRepair,
                            scene,
                            previousActive,
                            ref error);
                    }

                    roomProperty.objectReferenceValue = room;
                    rootData.ApplyModifiedPropertiesWithoutUndo();
                    bindingChanged = true;
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                if (!FpgRoomArtSceneContractValidator.TryValidateSceneForBindingRepair(
                        room,
                        out error))
                {
                    if (bindingChanged)
                    {
                        AppendRollbackError(
                            ref error,
                            TryRestoreRootBinding(
                                root,
                                previousBinding,
                                scene,
                                out string rollbackError),
                            rollbackError);
                    }

                    return CompleteArtSceneOperation(
                        false,
                        openedForRepair,
                        scene,
                        previousActive,
                        ref error);
                }

                if (bindingChanged
                    && !EditorSceneManager.SaveScene(scene, room.ArtScenePath))
                {
                    error = $"Could not save repaired Art Scene '{scene.path}'.";
                    AppendRollbackError(
                        ref error,
                        TryRestoreRootBinding(
                            root,
                            previousBinding,
                            scene,
                            out string rollbackError),
                        rollbackError);
                    return CompleteArtSceneOperation(
                        false,
                        openedForRepair,
                        scene,
                        previousActive,
                        ref error);
                }

                return CompleteArtSceneOperation(
                    true,
                    openedForRepair,
                    scene,
                    previousActive,
                    ref error);
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                if (bindingChanged && root != null)
                {
                    AppendRollbackError(
                        ref error,
                        TryRestoreRootBinding(
                            root,
                            previousBinding,
                            scene,
                            out string rollbackError),
                        rollbackError);
                }

                return CompleteArtSceneOperation(
                    false,
                    openedForRepair,
                    scene,
                    previousActive,
                    ref error);
            }
        }

        private static bool CompleteArtSceneOperation(
            bool succeeded,
            bool openedForOperation,
            Scene scene,
            Scene previousActive,
            ref string error)
        {
            try
            {
                if (previousActive.IsValid()
                    && previousActive.isLoaded
                    && SceneManager.GetActiveScene() != previousActive
                    && !SceneManager.SetActiveScene(previousActive))
                {
                    succeeded = false;
                    error +=
                        $" Could not restore active Scene '{previousActive.path}'.";
                }

                if (!openedForOperation
                    || !scene.IsValid()
                    || !scene.isLoaded)
                {
                    return succeeded;
                }

                if (scene.isDirty)
                {
                    error +=
                        $" Art Scene '{scene.path}' remains open because it has unsaved changes.";
                    return false;
                }

                if (!EditorSceneManager.CloseScene(scene, true))
                {
                    error += $" Could not close Art Scene '{scene.path}'.";
                    return false;
                }

                return succeeded;
            }
            catch (Exception exception)
            {
                error +=
                    " Art Scene cleanup failed: "
                    + exception.GetBaseException().Message;
                return false;
            }
        }

        public static bool TryRegisterRoomForProduction(
            FpgRoomDefinition room,
            out string error)
        {
            try
            {
                return TryRegisterRoomForProductionCore(
                    room,
                    out _,
                    out error);
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryRegisterRoomForProductionCore(
            FpgRoomDefinition room,
            out bool failureCleanupSafe,
            out string error)
        {
            failureCleanupSafe = true;
            error = string.Empty;
            if (room == null)
            {
                error = "Cannot register a missing RoomDefinition.";
                return false;
            }

            if (EditorUtility.IsDirty(room))
            {
                error =
                    $"Save RoomDefinition '{room.RoomId}' before registering it for production.";
                return false;
            }

            Scene roomScene = SceneManager.GetSceneByPath(room.ArtScenePath);
            if (roomScene.IsValid() && roomScene.isLoaded && roomScene.isDirty)
            {
                error =
                    $"Save Art Scene '{roomScene.path}' before registering it for production.";
                return false;
            }

            FpgRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(
                FpgRoomArtSceneEditorUtility.RoomCatalogPath);
            if (catalog == null)
            {
                error = "Room catalog is missing.";
                return false;
            }

            if (EditorUtility.IsDirty(catalog))
            {
                error =
                    "Save the production RoomCatalog before registering another room.";
                return false;
            }

            if (!TryLoadEditorBuildSettingsAsset(
                    out EditorBuildSettings buildSettingsAsset,
                    out error))
            {
                return false;
            }

            if (EditorUtility.IsDirty(buildSettingsAsset))
            {
                error =
                    "Save Build Settings before changing production registration.";
                return false;
            }

            UnityEngine.Object dirtyBuildProfile = FindDirtyBuildProfile();
            if (dirtyBuildProfile != null)
            {
                error =
                    $"Save Build Profile '{AssetDatabase.GetAssetPath(dirtyBuildProfile)}' before changing production registration.";
                return false;
            }

            FpgRoomDefinition[] originalRooms = catalog.Rooms.ToArray();
            EditorBuildSettingsScene[] originalGlobalBuildSettings =
                EditorBuildSettings.globalScenes;
            EditorBuildSettingsScene[] originalEffectiveBuildSettings =
                EditorBuildSettings.scenes;
            List<FpgRoomDefinition> prospectiveRooms =
                new List<FpgRoomDefinition>(originalRooms);
            if (!prospectiveRooms.Contains(room))
            {
                prospectiveRooms.Add(room);
            }


            if (!TryValidateRoomIdentitySet(prospectiveRooms, out error)
                || !FpgRoomArtSceneContractValidator.TryValidateUniqueReferences(
                    prospectiveRooms,
                    out error))
            {
                return false;
            }

            if (!TryValidateProductionRoomSet(prospectiveRooms, out error))
            {
                return false;
            }

            prospectiveRooms.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.RoomId, right.RoomId));
            if (!FpgProductionSceneList.TryBuild(
                    prospectiveRooms,
                    out string[] expectedScenes,
                    out error))
            {
                return false;
            }

            EditorBuildSettingsScene[] targetBuildSettings = expectedScenes
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
            bool catalogChanged = !originalRooms.SequenceEqual(prospectiveRooms);
            bool globalBuildSettingsChanged =
                !FpgProductionSceneList.TryValidateConfiguredScenes(
                    originalGlobalBuildSettings,
                    expectedScenes,
                    out _);
            bool effectiveBuildSettingsValid =
                FpgProductionSceneList.TryValidateConfiguredScenes(
                    originalEffectiveBuildSettings,
                    expectedScenes,
                    out _);
            if (!globalBuildSettingsChanged
                && !effectiveBuildSettingsValid)
            {
                error =
                    "Active Build Settings override the global production scene list. Update or disable the active Build Profile override before registering rooms.";
                return false;
            }

            bool buildSettingsChanged = globalBuildSettingsChanged;
            if (!catalogChanged && !globalBuildSettingsChanged)
            {
                return true;
            }

            try
            {
                if (catalogChanged
                    && !TrySetCatalogRooms(catalog, prospectiveRooms, out error))
                {
                    return false;
                }
                if (!FpgRoomArtSceneContractValidator.TryValidateCatalog(
                        out error))
                {
                    failureCleanupSafe = RestoreProductionRegistration(
                        catalog,
                        originalRooms,
                        originalGlobalBuildSettings,
                        catalogChanged,
                        buildSettingsChanged,
                        ref error);
                    return false;
                }

                if (catalogChanged)
                {
                    if (!TryPersistCatalog(catalog, out error))
                    {
                        failureCleanupSafe = RestoreProductionRegistration(
                            catalog,
                            originalRooms,
                            originalGlobalBuildSettings,
                            catalogChanged,
                            buildSettingsChanged,
                            ref error);
                        return false;
                    }
                }

                if (buildSettingsChanged)
                {
                    EditorBuildSettings.globalScenes = targetBuildSettings;
                }

                if (!FpgProductionSceneList.TryValidateConfiguredScenes(
                        EditorBuildSettings.globalScenes,
                        expectedScenes,
                        out error))
                {
                    error = "Global Build Settings are invalid: " + error;
                    failureCleanupSafe = RestoreProductionRegistration(
                        catalog,
                        originalRooms,
                        originalGlobalBuildSettings,
                        catalogChanged,
                        buildSettingsChanged,
                        ref error);
                    return false;
                }

                if (!FpgProductionSceneList.TryValidateConfiguredScenes(
                        EditorBuildSettings.scenes,
                        expectedScenes,
                        out error))
                {
                    error = "Active Build Settings are invalid: " + error;
                    failureCleanupSafe = RestoreProductionRegistration(
                        catalog,
                        originalRooms,
                        originalGlobalBuildSettings,
                        catalogChanged,
                        buildSettingsChanged,
                        ref error);
                    return false;
                }

                if (buildSettingsChanged
                    && !TryPersistEditorBuildSettings(
                        buildSettingsAsset,
                        out error))
                {
                    failureCleanupSafe = RestoreProductionRegistration(
                        catalog,
                        originalRooms,
                        originalGlobalBuildSettings,
                        catalogChanged,
                        buildSettingsChanged,
                        ref error);
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                failureCleanupSafe = RestoreProductionRegistration(
                    catalog,
                    originalRooms,
                    originalGlobalBuildSettings,
                    catalogChanged,
                    buildSettingsChanged,
                    ref error);
                return false;
            }
        }

        public static bool IsRoomRegistered(FpgRoomDefinition room)
        {
            if (room == null)
            {
                return false;
            }

            return FpgRoomArtSceneEditorUtility.LoadCatalogRooms()
                .Any(candidate => candidate == room);
        }

        [MenuItem("FPG Demo/Room Authoring/Repair Selected Room Production Contract")]
        private static void RepairSelectedRoomProductionContract()
        {
            FpgRoomDefinition room = Selection.activeObject as FpgRoomDefinition;
            if (room == null)
            {
                EditorUtility.DisplayDialog(
                    "Repair Room",
                    "Select a FpgRoomDefinition asset first.",
                    "OK");
                return;
            }

            if (!TryBindArtSceneRoot(room, out string bindingError))
            {
                EditorUtility.DisplayDialog(
                    "Repair Room Failed",
                    bindingError,
                    "OK");
                return;
            }

            if (!TryRegisterRoomForProduction(
                    room,
                    out string registrationError))
            {
                EditorUtility.DisplayDialog(
                    "Repair Room Failed",
                    registrationError,
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Room Repaired",
                $"Room '{room.RoomId}' now has a valid Art Scene root binding and is registered for production.",
                "OK");
        }

        [MenuItem("FPG Demo/Room Authoring/Repair Selected Room Production Contract", true)]
        private static bool ValidateRepairSelectedRoomMenu()
        {
            return Selection.activeObject is FpgRoomDefinition;
        }

        private static bool TryFindArtRoot(
            Scene scene,
            out FpgRoomArtRoot root,
            out string error)
        {
            root = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "Art Scene must be valid and loaded before repairing its root.";
                return false;
            }

            GameObject[] sceneRoots = scene.GetRootGameObjects()
                .Where(candidate =>
                    (candidate.hideFlags & HideFlags.DontSaveInEditor) == 0)
                .ToArray();
            if (sceneRoots.Length != 1)
            {
                error =
                    $"Art Scene '{scene.path}' must contain exactly one scene root; found {sceneRoots.Length}.";
                return false;
            }

            FpgRoomArtRoot[] candidates = sceneRoots[0]
                .GetComponentsInChildren<FpgRoomArtRoot>(true);
            if (candidates.Length != 1 || candidates[0].gameObject != sceneRoots[0])
            {
                error =
                    $"Art Scene '{scene.path}' must contain exactly one FpgRoomArtRoot on its sole scene root.";
                return false;
            }

            root = candidates[0];
            error = string.Empty;
            return true;
        }

        private static bool TryRestoreRootBinding(
            FpgRoomArtRoot root,
            FpgRoomDefinition previousBinding,
            Scene scene,
            out string error)
        {
            try
            {
                if (root == null)
                {
                    error = "The Art Scene root is missing.";
                    return false;
                }

                SerializedObject rootData = new SerializedObject(root);
                SerializedProperty roomProperty = rootData.FindProperty(
                    "roomDefinition");
                if (roomProperty == null)
                {
                    error = "FpgRoomArtRoot has no serialized roomDefinition field.";
                    return false;
                }

                roomProperty.objectReferenceValue = previousBinding;
                rootData.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, scene.path))
                {
                    error = $"Could not save restored Art Scene '{scene.path}'.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error =
                    "Could not restore the Art Scene root binding: "
                    + exception.GetBaseException().Message;
                return false;
            }
        }

        private static void AppendRollbackError(
            ref string error,
            bool restored,
            string rollbackError)
        {
            if (!restored)
            {
                error += $" Rollback failed: {rollbackError}";
            }
        }

        private static bool TrySetCatalogRooms(
            FpgRoomCatalog catalog,
            IReadOnlyList<FpgRoomDefinition> rooms,
            out string error)
        {
            SerializedObject catalogData = new SerializedObject(catalog);
            SerializedProperty array = catalogData.FindProperty("rooms");
            if (array == null)
            {
                error = "RoomCatalog has no serialized rooms field.";
                return false;
            }

            array.arraySize = rooms.Count;
            for (int index = 0; index < rooms.Count; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue =
                    rooms[index];
            }

            catalogData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            error = string.Empty;
            return true;
        }

        private static UnityEngine.Object FindDirtyBuildProfile()
        {
            return Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .FirstOrDefault(candidate =>
                {
                    if (candidate == null
                        || !EditorUtility.IsDirty(candidate)
                        || string.IsNullOrWhiteSpace(
                            AssetDatabase.GetAssetPath(candidate)))
                    {
                        return false;
                    }

                    Type type = candidate.GetType();
                    return string.Equals(
                            type.Name,
                            "BuildProfile",
                            StringComparison.Ordinal)
                        || (type.FullName?.EndsWith(
                            ".BuildProfile",
                            StringComparison.Ordinal) ?? false);
                });
        }

        private static bool TryPersistCatalog(
            FpgRoomCatalog catalog,
            out string error)
        {
            AssetDatabase.SaveAssetIfDirty(catalog);
            if (EditorUtility.IsDirty(catalog))
            {
                error =
                    $"Could not persist RoomCatalog '{AssetDatabase.GetAssetPath(catalog)}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryLoadEditorBuildSettingsAsset(
            out EditorBuildSettings buildSettings,
            out string error)
        {
            buildSettings = AssetDatabase
                .LoadAllAssetsAtPath(EditorBuildSettingsAssetPath)
                .OfType<EditorBuildSettings>()
                .FirstOrDefault();
            if (buildSettings == null)
            {
                error =
                    $"Could not load '{EditorBuildSettingsAssetPath}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryPersistEditorBuildSettings(
            EditorBuildSettings buildSettings,
            out string error)
        {
            List<UnityEngine.Object> unrelatedDirtyAssets =
                Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                    .Where(candidate =>
                        candidate != null
                        && candidate != buildSettings
                        && EditorUtility.IsDirty(candidate)
                        && !string.IsNullOrWhiteSpace(
                            AssetDatabase.GetAssetPath(candidate)))
                    .ToList();
            List<UnityEngine.Object> clearedDirtyAssets =
                new List<UnityEngine.Object>(unrelatedDirtyAssets.Count);
            try
            {
                // Unity 6 only persists this ProjectSettings object through
                // SaveAssets. Shield unrelated dirty assets from that save.
                for (int index = 0; index < unrelatedDirtyAssets.Count; index++)
                {
                    UnityEngine.Object candidate = unrelatedDirtyAssets[index];
                    EditorUtility.ClearDirty(candidate);
                    clearedDirtyAssets.Add(candidate);
                }

                EditorUtility.SetDirty(buildSettings);
                AssetDatabase.SaveAssets();
                if (EditorUtility.IsDirty(buildSettings))
                {
                    error = "Unity did not persist EditorBuildSettings.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error =
                    "Could not persist EditorBuildSettings: "
                    + exception.GetBaseException().Message;
                return false;
            }
            finally
            {
                for (int index = 0; index < clearedDirtyAssets.Count; index++)
                {
                    UnityEngine.Object candidate = clearedDirtyAssets[index];
                    if (candidate != null)
                    {
                        EditorUtility.SetDirty(candidate);
                    }
                }
            }
        }

        private static bool RestoreProductionRegistration(
            FpgRoomCatalog catalog,
            IReadOnlyList<FpgRoomDefinition> originalRooms,
            EditorBuildSettingsScene[] originalGlobalBuildSettings,
            bool restoreCatalog,
            bool restoreBuildSettings,
            ref string error)
        {
            List<string> rollbackErrors = new List<string>();
            if (restoreCatalog)
            {
                try
                {
                    if (TrySetCatalogRooms(
                            catalog,
                            originalRooms,
                            out string catalogError))
                    {
                        if (!TryPersistCatalog(catalog, out catalogError))
                        {
                            rollbackErrors.Add(catalogError);
                        }
                    }
                    else
                    {
                        rollbackErrors.Add(catalogError);
                    }
                }
                catch (Exception exception)
                {
                    rollbackErrors.Add(
                        "RoomCatalog rollback failed: "
                        + exception.GetBaseException().Message);
                }
            }

            if (restoreBuildSettings)
            {
                try
                {
                    EditorBuildSettings.globalScenes =
                        originalGlobalBuildSettings;
                    if (!TryLoadEditorBuildSettingsAsset(
                            out EditorBuildSettings buildSettings,
                            out string loadError))
                    {
                        rollbackErrors.Add(loadError);
                    }
                    else if (!TryPersistEditorBuildSettings(
                                 buildSettings,
                                 out string persistenceError))
                    {
                        rollbackErrors.Add(persistenceError);
                    }
                }
                catch (Exception exception)
                {
                    rollbackErrors.Add(
                        "Build Settings rollback failed: "
                        + exception.GetBaseException().Message);
                }
            }

            if (rollbackErrors.Count > 0)
            {
                string rollbackMessage = string.Join(" ", rollbackErrors);
                error = string.IsNullOrWhiteSpace(error)
                    ? rollbackMessage
                    : error + " Rollback error: " + rollbackMessage;
            }
            return rollbackErrors.Count == 0;

        }

        private static bool TryValidateProductionRoomSet(
            IReadOnlyList<FpgRoomDefinition> rooms,
            out string error)
        {
            for (int index = 0; index < rooms.Count; index++)
            {
                FpgRoomDefinition candidate = rooms[index];
                if (EditorUtility.IsDirty(candidate))
                {
                    error =
                        $"Save RoomDefinition '{candidate.RoomId}' before changing production registration.";
                    return false;
                }

                Scene candidateScene = SceneManager.GetSceneByPath(
                    candidate.ArtScenePath);
                if (candidateScene.IsValid()
                    && candidateScene.isLoaded
                    && candidateScene.isDirty)
                {
                    error =
                        $"Save Art Scene '{candidateScene.path}' before changing production registration.";
                    return false;
                }

                if (!candidate.TryValidate(
                        out FpgRoomValidationResult validation))
                {
                    string detail = validation.FirstError == null
                        ? "the room definition is invalid"
                        : validation.FirstError.Message;
                    error =
                        $"Room '{candidate.RoomId}' cannot be registered: {detail}";
                    return false;
                }

                if (candidate.ExitSlots.Count == 0)
                {
                    error =
                        $"Room '{candidate.RoomId}' cannot be registered without an exit slot.";
                    return false;
                }

                if (!FpgRoomArtSceneContractValidator.TryValidateScene(
                        candidate,
                        out error))
                {
                    error =
                        $"Room '{candidate.RoomId}' has an invalid Art Scene: {error}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateRoomIdentitySet(
            IReadOnlyList<FpgRoomDefinition> rooms,
            out string error)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < rooms.Count; index++)
            {
                FpgRoomDefinition room = rooms[index];
                if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
                {
                    error = $"Room catalog entry {index} has no stable room ID.";
                    return false;
                }

                if (!ids.Add(room.RoomId))
                {
                    error = $"Room catalog contains duplicate room ID '{room.RoomId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateRoomAssetPath(
            string requestedPath,
            out string normalizedPath,
            out string error)
        {
            normalizedPath = (requestedPath ?? string.Empty).Replace('\\', '/');
            if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal)
                || !normalizedPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                error = "Duplicate Room asset path must be an Assets-relative .asset path.";
                return false;
            }

            string directory = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory)
                || AssetDatabase.IsValidFolder(directory) == false)
            {
                error = $"Room asset folder '{directory}' does not exist.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string GenerateRoomId()
        {
            return "room-" + Guid.NewGuid().ToString("N");
        }
    }
}
