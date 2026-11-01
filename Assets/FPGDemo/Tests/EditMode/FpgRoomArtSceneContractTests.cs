using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FPG.Demo.Editor.LevelAuthoring;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgRoomArtSceneContractTests
    {
        private const string CatalogPath =
            "Assets/FPGDemo/Config/Level/FPG_RoomCatalog.asset";
        private const string ForestRoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/Room_forest.asset";
        private const string ForestScenePath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest.unity";
        private const string Root1RoomPath =
            "Assets/FPGDemo/Config/Level/Rooms/root1.asset";
        private const string Root1ScenePath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_root1.unity";
        private const string ForestLightingSettingsPath =
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest_LightingSettings.asset";
        private const string FogProfilePath =
            "Assets/FPGDemo/Presentation/Level/Environment/ForestArt/ForestScene_VolumetricFog2Profile.asset";
        private const string SunBeamProfilePath =
            "Assets/FPGDemo/Presentation/Level/Environment/ForestArt/ForestScene_SunBeam_VLProfile.asset";

        [Test]
        public void ProductionCatalogReferencesAreSynchronizedUniqueAndValid()
        {
            FpgRoomCatalog catalog =
                AssetDatabase.LoadAssetAtPath<FpgRoomCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(
                FpgRoomArtSceneContractValidator.TryValidateCatalog(
                    out string error),
                Is.True,
                error);

            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Rooms.Count; index++)
            {
                FpgRoomDefinition room = catalog.Rooms[index];
                Assert.That(
                    FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                        room,
                        out error),
                    Is.True,
                    error);
                Assert.That(guids.Add(room.ArtScene.SceneGuid), Is.True);
                Assert.That(paths.Add(room.ArtScenePath), Is.True);
                Assert.That(names.Add(room.ArtScene.SceneName), Is.True);
            }
        }

        [Test]
        public void DuplicateAndMissingArtSceneReferencesAreRejected()
        {
            FpgRoomDefinition source =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(ForestRoomPath);
            FpgRoomDefinition first = UnityEngine.Object.Instantiate(source);
            FpgRoomDefinition second = UnityEngine.Object.Instantiate(source);
            try
            {
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateUniqueReferences(
                        new[] { first, second },
                        out string duplicateError),
                    Is.False);
                StringAssert.Contains("one-to-one", duplicateError);

                SerializedObject secondData = new SerializedObject(second);
                SerializedProperty artScene = secondData.FindProperty("artScene");
                artScene.FindPropertyRelative("sceneGuid").stringValue =
                    string.Empty;
                secondData.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                        second,
                        out string missingError),
                    Is.False);
                Assert.That(missingError, Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void StoredReferenceValidationRejectsMismatchWithoutRepairingIt()
        {
            FpgRoomDefinition room =
                ScriptableObject.CreateInstance<FpgRoomDefinition>();
            try
            {
                string guid = AssetDatabase.AssetPathToGUID(ForestScenePath);
                const string stalePath = "Assets/Stale/ART_Forest.unity";
                SetArtSceneReference(room, guid, stalePath);

                Assert.That(
                    FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                        room,
                        out string error),
                    Is.False);
                StringAssert.Contains("not stored path", error);
                Assert.That(room.ArtScene.SceneGuid, Is.EqualTo(guid));
                Assert.That(room.ArtScenePath, Is.EqualTo(stalePath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void SceneMoveRefreshesPathFromStableGuid()
        {
            string folder = CreateTemporaryFolder();
            string sourcePath = folder + "/Source.unity";
            string movedPath = folder + "/Moved.unity";
            string roomPath = folder + "/Room.asset";
            FpgRoomDefinition room =
                ScriptableObject.CreateInstance<FpgRoomDefinition>();
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, sourcePath),
                    Is.True);
                string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                SetArtSceneReference(room, guid, sourcePath);
                AssetDatabase.CreateAsset(room, roomPath);
                AssetDatabase.SaveAssetIfDirty(room);
                string serializedBefore = File.ReadAllText(roomPath);
                Assert.That(AssetDatabase.MoveAsset(sourcePath, movedPath), Is.Empty);
                Assert.That(
                    FpgRoomArtSceneEditorUtility.TrySynchronizeReference(
                        room,
                        out string error),
                    Is.True,
                    error);
                Assert.That(room.ArtScene.SceneGuid, Is.EqualTo(guid));
                Assert.That(room.ArtScenePath, Is.EqualTo(movedPath));
                Assert.That(EditorUtility.IsDirty(room), Is.True);
                Assert.That(
                    File.ReadAllText(roomPath),
                    Is.EqualTo(serializedBefore),
                    "Synchronizing a moved scene may mark the RoomDefinition dirty, but must not save it.");
            }
            finally
            {
                if (room != null && !AssetDatabase.Contains(room))
                {
                    UnityEngine.Object.DestroyImmediate(room);
                }
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RoomDeletionPreflightRejectsArtSceneSharedByAnotherDraft()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/Shared.unity";
            string roomPath = folder + "/Room.asset";
            string otherRoomPath = folder + "/OtherRoom.asset";
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, otherRoomPath),
                    Is.True);

                FpgRoomDefinition room =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                FpgRoomDefinition otherRoom =
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(otherRoomPath);
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                SetArtSceneReference(room, sceneGuid, scenePath);
                SetArtSceneReference(otherRoom, sceneGuid, scenePath);
                AssetDatabase.SaveAssetIfDirty(room);
                AssetDatabase.SaveAssetIfDirty(otherRoom);

                Assert.That(
                    FpgRoomAuthoringOperations.TryGetRoomDeletionPlan(
                        room,
                        out string report,
                        out bool canDelete,
                        out string error),
                    Is.True,
                    error);
                Assert.That(canDelete, Is.False);
                StringAssert.Contains(
                    $"Art Scene is also assigned to RoomDefinition '{otherRoomPath}'.",
                    report);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RoomDeletionPreflightRejectsUnsavedDedicatedCameraProfile()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/Room.unity";
            string roomPath = folder + "/Room.asset";
            string profilePath = folder + "/Room_CoverCamera.asset";
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);

                FpgCoverCameraProfile profile =
                    ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                FpgRoomDefinition room =
                    ScriptableObject.CreateInstance<FpgRoomDefinition>();
                SetArtSceneReference(
                    room,
                    AssetDatabase.AssetPathToGUID(scenePath),
                    scenePath);
                AssetDatabase.CreateAsset(room, roomPath);

                SerializedObject roomData = new SerializedObject(room);
                SerializedProperty coverSlots = roomData.FindProperty("coverSlots");
                coverSlots.arraySize = 1;
                coverSlots.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("cameraProfile")
                    .objectReferenceValue = profile;
                roomData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(room);

                SerializedObject profileData = new SerializedObject(profile);
                profileData.FindProperty("designerNotes").stringValue =
                    "Unsaved deletion preflight test change";
                profileData.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(EditorUtility.IsDirty(profile), Is.True);

                Assert.That(
                    FpgRoomAuthoringOperations.TryGetRoomDeletionPlan(
                        room,
                        out string report,
                        out bool canDelete,
                        out string error),
                    Is.True,
                    error);
                Assert.That(canDelete, Is.False);
                StringAssert.Contains(
                    $"Camera profile '{profilePath}' has unsaved changes",
                    report);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void DeletionRollbackBackupPathsDoNotCollideWhenFlattenedPathsWould()
        {
            MethodInfo buildBackupPath = typeof(FpgRoomAuthoringOperations).GetMethod(
                "BuildDeletionBackupAssetPath",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(buildBackupPath, Is.Not.Null);

            const string backupDirectory = "Temp/FpgRoomDeletion_Test";
            string firstPath = (string)buildBackupPath.Invoke(
                null,
                new object[]
                {
                    backupDirectory,
                    "Assets/A_B/C.asset",
                    0
                });
            string secondPath = (string)buildBackupPath.Invoke(
                null,
                new object[]
                {
                    backupDirectory,
                    "Assets/A/B_C.asset",
                    1
                });

            Assert.That(firstPath, Is.Not.EqualTo(secondPath));
        }

        [Test]
        public void DirtyLoadedSceneCollectionIncludesAdditiveScenes()
        {
            MethodInfo getDirtyScenes = typeof(FpgRoomEditorWindow).GetMethod(
                "GetDirtyLoadedScenes",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(getDirtyScenes, Is.Not.Null);

            Scene previousActive = SceneManager.GetActiveScene();
            Scene firstAdditive = default(Scene);
            Scene secondAdditive = default(Scene);
            try
            {
                firstAdditive = EditorSceneManager.OpenScene(
                    "Assets/FPGDemo/Scenes/Boot.unity",
                    OpenSceneMode.Additive);
                secondAdditive = EditorSceneManager.OpenScene(
                    "Assets/FPGDemo/Scenes/FormalRoom.unity",
                    OpenSceneMode.Additive);
                Assert.That(firstAdditive.IsValid() && firstAdditive.isLoaded, Is.True);
                Assert.That(secondAdditive.IsValid() && secondAdditive.isLoaded, Is.True);

                EditorSceneManager.MarkSceneDirty(firstAdditive);
                EditorSceneManager.MarkSceneDirty(secondAdditive);
                if (previousActive.IsValid()
                    && previousActive.isLoaded
                    && SceneManager.GetActiveScene() != previousActive)
                {
                    Assert.That(SceneManager.SetActiveScene(previousActive), Is.True);
                }

                List<Scene> dirtyScenes = (List<Scene>)getDirtyScenes.Invoke(
                    null,
                    null);
                Assert.That(
                    dirtyScenes.Any(scene => scene.handle == firstAdditive.handle),
                    Is.True);
                Assert.That(
                    dirtyScenes.Any(scene => scene.handle == secondAdditive.handle),
                    Is.True);
            }
            finally
            {
                if (previousActive.IsValid()
                    && previousActive.isLoaded
                    && SceneManager.GetActiveScene() != previousActive)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
                if (secondAdditive.IsValid() && secondAdditive.isLoaded)
                {
                    EditorSceneManager.CloseScene(secondAdditive, true);
                }
                if (firstAdditive.IsValid() && firstAdditive.isLoaded)
                {
                    EditorSceneManager.CloseScene(firstAdditive, true);
                }
            }
        }

        [Test]
        public void SceneValidatorRejectsGameplayHostsAndAllowsPresentationOptions()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/Contract.unity";
            string roomPath = folder + "/Room.asset";
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = default(Scene);
            FpgRoomDefinition room = null;
            FpgRoomDefinition mismatchRoom = null;
            try
            {
                Assert.That(
                    AssetDatabase.CopyAsset(ForestScenePath, scenePath),
                    Is.True);
                Assert.That(
                    AssetDatabase.CopyAsset(ForestRoomPath, roomPath),
                    Is.True);
                room = AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                SetArtSceneReference(
                    room,
                    AssetDatabase.AssetPathToGUID(scenePath),
                    scenePath);
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssetIfDirty(room);

                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
                if (SceneManager.GetActiveScene() != scene)
                {
                    Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                }
                GameObject rootObject = scene.GetRootGameObjects()[0];
                FpgRoomArtRoot root = rootObject.GetComponent<FpgRoomArtRoot>();
                Material authoredSkybox = RenderSettings.skybox;
                Assert.That(authoredSkybox, Is.Not.Null);

                SerializedObject rootData = new SerializedObject(root);
                rootData.FindProperty("roomDefinition").objectReferenceValue = room;
                rootData.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string validError),
                    Is.True,
                    validError);

                GameObject extraRoot = new GameObject("Extra Art Root");
                SceneManager.MoveGameObjectToScene(extraRoot, scene);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string extraRootError),
                    Is.True,
                    extraRootError);
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out _,
                        out string extraRootResolveError),
                    Is.True,
                    extraRootResolveError);

                GameObject transientPreview = new GameObject(
                    "Transient Editor Preview")
                {
                    hideFlags = HideFlags.DontSaveInEditor
                };
                SceneManager.MoveGameObjectToScene(transientPreview, scene);
                transientPreview.AddComponent<FpgEncounterHost>();
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out _,
                        out string transientHostError),
                    Is.True,
                    transientHostError);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string transientPreviewError),
                    Is.True,
                    transientPreviewError);
                UnityEngine.Object.DestroyImmediate(transientPreview);

                GameObject nestedTransientPreview = new GameObject(
                    "Nested Transient Preview")
                {
                    hideFlags = HideFlags.DontSaveInEditor
                };
                nestedTransientPreview.transform.SetParent(
                    extraRoot.transform,
                    false);
                nestedTransientPreview.AddComponent<FpgRoomArtRoot>();
                nestedTransientPreview.AddComponent<FpgEncounterHost>();
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out _,
                        out string nestedTransientError),
                    Is.True,
                    nestedTransientError);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateLoadedSceneForAuthoring(
                        room,
                        scene,
                        out string nestedTransientValidationError),
                    Is.True,
                    nestedTransientValidationError);
                UnityEngine.Object.DestroyImmediate(nestedTransientPreview);

                GameObject cameraObject = new GameObject("Optional Art Camera");
                cameraObject.transform.SetParent(rootObject.transform, false);
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string cameraError),
                    Is.True,
                    cameraError);
                UnityEngine.Object.DestroyImmediate(cameraObject);

                RenderSettings.sun = null;
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string noSunError),
                    Is.True,
                    noSunError);
                Assert.That(RenderSettings.skybox, Is.SameAs(authoredSkybox));

                Type[] forbiddenHosts =
                {
                    typeof(FpgEncounterHost),
                    typeof(FpgFormalEncounterHost)
                };
                for (int index = 0; index < forbiddenHosts.Length; index++)
                {
                    GameObject hostObject = new GameObject(
                        "Forbidden " + forbiddenHosts[index].Name);
                    SceneManager.MoveGameObjectToScene(hostObject, scene);
                    hostObject.AddComponent(forbiddenHosts[index]);
                    Assert.That(
                        EditorSceneManager.SaveScene(scene, scenePath),
                        Is.True);
                    Assert.That(
                        FpgRoomArtSceneContractValidator.TryValidateScene(
                            room,
                            out string hostError),
                        Is.False);
                    StringAssert.Contains(forbiddenHosts[index].Name, hostError);
                    Assert.That(
                        FpgRoomArtRoot.TryResolve(
                            scene,
                            room,
                            out _,
                            out string runtimeHostError),
                        Is.False);
                    StringAssert.Contains(
                        forbiddenHosts[index].Name,
                        runtimeHostError);
                    UnityEngine.Object.DestroyImmediate(hostObject);
                }

                UnityEngine.Object.DestroyImmediate(rootObject);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string missingRootError),
                    Is.False);
                StringAssert.Contains(nameof(FpgRoomArtRoot), missingRootError);
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out _,
                        out string runtimeMissingRootError),
                    Is.False);
                StringAssert.Contains(
                    nameof(FpgRoomArtRoot),
                    runtimeMissingRootError);

                mismatchRoom = ScriptableObject.CreateInstance<FpgRoomDefinition>();
                AssetDatabase.CreateAsset(
                    mismatchRoom,
                    folder + "/Mismatch.asset");
                rootObject = new GameObject("Replacement Art Root");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
                root = rootObject.AddComponent<FpgRoomArtRoot>();
                SetArtRootRoom(root, mismatchRoom);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string mismatchError),
                    Is.False);
                StringAssert.Contains("requested", mismatchError);
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out _,
                        out string runtimeMismatchError),
                    Is.False);
                StringAssert.Contains("requested", runtimeMismatchError);

                SetArtRootRoom(root, room);
                GameObject duplicateRootObject = new GameObject(
                    "Duplicate Art Root");
                SceneManager.MoveGameObjectToScene(duplicateRootObject, scene);
                SetArtRootRoom(
                    duplicateRootObject.AddComponent<FpgRoomArtRoot>(),
                    room);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string duplicateRootError),
                    Is.False);
                StringAssert.Contains("exactly one FpgRoomArtRoot", duplicateRootError);
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out _,
                        out string runtimeDuplicateRootError),
                    Is.False);
                StringAssert.Contains(
                    "exactly one FpgRoomArtRoot",
                    runtimeDuplicateRootError);

            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
                if (mismatchRoom != null && !AssetDatabase.Contains(mismatchRoom))
                {
                    UnityEngine.Object.DestroyImmediate(mismatchRoom);
                }
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void SceneValidatorRejectsMissingPrefabInstancesWithHierarchyLocation()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/BrokenRoom.unity";
            string roomPath = folder + "/BrokenRoom.asset";
            string prefabPath = folder + "/MissingPresentation.prefab";
            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            Scene scene = default;
            GameObject prefabSource = null;
            FpgRoomDefinition room = null;
            try
            {
                room = ScriptableObject.CreateInstance<FpgRoomDefinition>();
                AssetDatabase.CreateAsset(room, roomPath);
                room = AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                Assert.That(room, Is.Not.Null);

                prefabSource = new GameObject("Missing Presentation");
                Assert.That(
                    PrefabUtility.SaveAsPrefabAsset(
                        prefabSource,
                        prefabPath),
                    Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(prefabSource);
                prefabSource = null;

                scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                GameObject artRootObject = new GameObject("FpgRoomArtRoot");
                SceneManager.MoveGameObjectToScene(artRootObject, scene);
                FpgRoomArtRoot artRoot =
                    artRootObject.AddComponent<FpgRoomArtRoot>();
                SerializedObject rootData = new SerializedObject(artRoot);
                rootData.FindProperty("roomDefinition").objectReferenceValue = room;
                rootData.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefabAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    prefabAsset) as GameObject;
                Assert.That(instance, Is.Not.Null);
                instance.name = "MissingPresentationInstance";
                SceneManager.MoveGameObjectToScene(instance, scene);

                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                room = AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath);
                Assert.That(room, Is.Not.Null);
                artRoot = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<FpgRoomArtRoot>())
                    .FirstOrDefault(root => root != null);
                Assert.That(artRoot, Is.Not.Null);
                SetArtRootRoom(artRoot, room);
                SetArtSceneReference(
                    room,
                    AssetDatabase.AssetPathToGUID(scenePath),
                    scenePath);
                AssetDatabase.SaveAssetIfDirty(room);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);

                Assert.That(AssetDatabase.DeleteAsset(prefabPath), Is.True);
                if (scene.isDirty)
                {
                    Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                }

                GameObject missingInstance = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    Transform[] descendants = roots[rootIndex]
                        .GetComponentsInChildren<Transform>(true);
                    for (int index = 0; index < descendants.Length; index++)
                    {
                        if (PrefabUtility.GetPrefabInstanceStatus(
                                descendants[index].gameObject)
                            == PrefabInstanceStatus.MissingAsset)
                        {
                            missingInstance = descendants[index].gameObject;
                            break;
                        }
                    }
                    if (missingInstance != null)
                    {
                        break;
                    }
                }
                Assert.That(missingInstance, Is.Not.Null);

                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateLoadedSceneForAuthoring(
                        room,
                        scene,
                        out string error),
                    Is.False);
                StringAssert.Contains("MissingPresentationInstance", error);
                StringAssert.Contains("missing Prefab", error);
            }
            finally
            {
                if (prefabSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefabSource);
                }
                try
                {
                    if (scene.IsValid()
                        && scene.isLoaded
                        && scene.isDirty
                        && !string.IsNullOrWhiteSpace(scene.path))
                    {
                        EditorSceneManager.SaveScene(scene, scenePath);
                    }

                    bool hasRestorableSetup = previousSetup != null
                        && Array.Exists(
                            previousSetup,
                            setup => setup.isLoaded && setup.isActive);
                    if (hasRestorableSetup)
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                    }
                    else
                    {
                        EditorSceneManager.NewScene(
                            NewSceneSetup.EmptyScene,
                            NewSceneMode.Single);
                    }
                }
                catch
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
                finally
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }

        [Test]
        public void DraftRoomCanBeCopiedWithRequestedNameAndBothDraftsCanBeDeleted()
        {
            string roomName = "__RoomDraftTest_" + Guid.NewGuid().ToString("N");
            string duplicateName = "UserNamedRoom_" + Guid.NewGuid().ToString("N");
            string roomPath =
                "Assets/FPGDemo/Config/Level/Rooms/" + roomName + ".asset";
            string duplicateRoomPath =
                "Assets/FPGDemo/Config/Level/Rooms/" + duplicateName + ".asset";
            string fixtureFolder = CreateTemporaryFolder();
            string harnessScenePath = fixtureFolder + "/Harness.unity";
            string scenePath =
                "Assets/FPGDemo/Presentation/Level/Rooms/" + roomName + ".unity";
            string duplicateScenePath =
                "Assets/FPGDemo/Presentation/Level/Rooms/" + duplicateName + ".unity";
            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            FpgRoomDefinition room = null;
            FpgRoomDefinition duplicateRoom = null;
            try
            {
                Scene harnessScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                Assert.That(
                    EditorSceneManager.SaveScene(
                        harnessScene,
                        harnessScenePath),
                    Is.True);

                Assert.That(
                    FpgRoomAuthoringOperations.TryCreateRoomDraft(
                        roomPath,
                        out room,
                        out string createError),
                    Is.True,
                    createError);

                Assert.That(room, Is.Not.Null);
                Assert.That(room.name, Is.EqualTo(roomName));
                Assert.That(room.DisplayName, Is.EqualTo(roomName));
                Assert.That(room.RoomId, Is.Not.Empty);
                Assert.That(
                    FpgRoomAuthoringOperations.IsRoomRegistered(room),
                    Is.False);
                Assert.That(room.ArtScenePath, Is.EqualTo(scenePath));
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateLoadedSceneForAuthoring(
                        room,
                        SceneManager.GetSceneByPath(scenePath),
                        out string sceneError),
                    Is.True,
                    sceneError);
                Assert.That(
                    Array.Exists(
                        EditorBuildSettings.globalScenes,
                        scene => string.Equals(
                            scene.path,
                            scenePath,
                            StringComparison.Ordinal)),
                    Is.False);

                Scene collisionScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
                Assert.That(
                    EditorSceneManager.SaveScene(
                        collisionScene,
                        duplicateScenePath),
                    Is.True);
                Assert.That(
                    FpgRoomAuthoringOperations.TryDuplicateRoomWithArtScene(
                        room,
                        duplicateRoomPath,
                        duplicateName,
                        false,
                        out _,
                        out string collisionError),
                    Is.False);
                StringAssert.Contains("already exists", collisionError);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                        duplicateRoomPath),
                    Is.Null);
                Assert.That(
                    EditorSceneManager.CloseScene(collisionScene, true),
                    Is.True);
                Assert.That(AssetDatabase.DeleteAsset(duplicateScenePath), Is.True);

                Assert.That(
                    FpgRoomAuthoringOperations.TryDuplicateRoomWithArtScene(
                        room,
                        duplicateRoomPath,
                        duplicateName,
                        false,
                        out duplicateRoom,
                        out string duplicateError),
                    Is.True,
                    duplicateError);
                Assert.That(duplicateRoom.name, Is.EqualTo(duplicateName));
                Assert.That(duplicateRoom.DisplayName, Is.EqualTo(duplicateName));
                Assert.That(duplicateRoom.RoomId, Is.Not.EqualTo(room.RoomId));
                Assert.That(
                    duplicateRoom.ArtScenePath,
                    Is.EqualTo(duplicateScenePath));
                Assert.That(
                    FpgRoomAuthoringOperations.IsRoomRegistered(duplicateRoom),
                    Is.False);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        duplicateRoom,
                        out string duplicateSceneError),
                    Is.True,
                    duplicateSceneError);

                Assert.That(
                    FpgRoomAuthoringOperations.TryDeleteRoomCompletely(
                        duplicateRoom,
                        out string duplicateDeleteError),
                    Is.True,
                    duplicateDeleteError);
                duplicateRoom = null;
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                        duplicateRoomPath),
                    Is.Null);

                Assert.That(
                    FpgRoomAuthoringOperations.TryDeleteRoomCompletely(
                        room,
                        out string deleteError),
                    Is.True,
                    deleteError);
                Assert.That(AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath), Is.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Null);
                Assert.That(File.Exists(roomPath), Is.False);
                Assert.That(File.Exists(scenePath), Is.False);
            }
            finally
            {
                Scene duplicateScene = SceneManager.GetSceneByPath(
                    duplicateScenePath);
                if (duplicateScene.IsValid() && duplicateScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(duplicateScene, true);
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        duplicateScenePath) != null)
                {
                    AssetDatabase.DeleteAsset(duplicateScenePath);
                }
                if (AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(
                        duplicateRoomPath) != null)
                {
                    AssetDatabase.DeleteAsset(duplicateRoomPath);
                }

                Scene roomScene = string.IsNullOrEmpty(scenePath)
                    ? default
                    : SceneManager.GetSceneByPath(scenePath);
                if (roomScene.IsValid() && roomScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(roomScene, true);
                }

                if (!string.IsNullOrEmpty(scenePath)
                    && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                {
                    AssetDatabase.DeleteAsset(scenePath);
                }
                if (AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(roomPath) != null)
                {
                    AssetDatabase.DeleteAsset(roomPath);
                }

                bool hasRestorableSetup = previousSetup != null
                    && Array.Exists(
                        previousSetup,
                        setup => setup.isLoaded && setup.isActive);
                if (hasRestorableSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }

                AssetDatabase.DeleteAsset(fixtureFolder);
            }
        }

        [Test]
        public void ForestSceneOwnsLightingFogSunBeamAndAutomaticBinding()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ForestScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(
                    ForestScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                if (SceneManager.GetActiveScene() != scene)
                {
                    Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                }

                GameObject[] sceneRoots = scene.GetRootGameObjects();
                Assert.That(sceneRoots, Has.Length.EqualTo(1));
                FpgRoomArtRoot root =
                    sceneRoots[0].GetComponent<FpgRoomArtRoot>();
                Assert.That(root, Is.Not.Null);
                Assert.That(root.RoomDefinition,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(ForestRoomPath)));
                Assert.That(RenderSettings.sun, Is.Not.Null);
                Assert.That(RenderSettings.sun.type, Is.EqualTo(LightType.Directional));
                Assert.That(RenderSettings.sun.gameObject.scene, Is.EqualTo(scene));
                Assert.That(
                    AssetDatabase.GetAssetPath(Lightmapping.lightingSettings),
                    Is.EqualTo(ForestLightingSettingsPath));

                MonoBehaviour fog = FindBehaviour(
                    sceneRoots[0],
                    "VolumetricFogAndMist2.VolumetricFog");
                MonoBehaviour sunBeam = FindBehaviour(
                    sceneRoots[0],
                    "VolumetricLights.VolumetricLight");
                MonoBehaviour binding = FindBehaviour(
                    sceneRoots[0],
                    "FPG.Demo.Unity.FpgVolumetricRoomArtBinding");
                Assert.That(fog, Is.Not.Null);
                Assert.That(sunBeam, Is.Not.Null);
                Assert.That(binding, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(
                        new SerializedObject(fog)
                            .FindProperty("profile").objectReferenceValue),
                    Is.EqualTo(FogProfilePath));
                Assert.That(
                    AssetDatabase.GetAssetPath(
                        new SerializedObject(sunBeam)
                            .FindProperty("profile").objectReferenceValue),
                    Is.EqualTo(SunBeamProfilePath));

                Transform fogTransform = fog.transform;
                Assert.That(
                    fogTransform.localPosition,
                    Is.EqualTo(new Vector3(0f, 11.5f, 17.485f)));
                Assert.That(
                    fogTransform.localScale,
                    Is.EqualTo(new Vector3(60f, 24f, 41.23f)));

                SerializedObject bindingData = new SerializedObject(binding);
                Assert.That(bindingData.FindProperty("fogManager"), Is.Null);
                Assert.That(bindingData.FindProperty("fogVolumes"), Is.Null);
                Assert.That(bindingData.FindProperty("volumetricLights"), Is.Null);
                Assert.That(bindingData.FindProperty("directionalSyncs"), Is.Null);
                Assert.That(
                    sceneRoots[0].GetComponentsInChildren<Camera>(true),
                    Is.Empty);
                Assert.That(
                    sceneRoots[0].GetComponentsInChildren<AudioListener>(true),
                    Is.Empty);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded
                    && SceneManager.GetActiveScene() != previousActive)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void ForestPresentationBindingDiscoversBindsAndClearsReferences()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ForestScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(
                    ForestScenePath,
                    OpenSceneMode.Additive);
            }

            GameObject cameraObject = null;
            FpgRoomArtRoot root = null;
            try
            {
                if (SceneManager.GetActiveScene() != scene)
                {
                    Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                }

                root = scene.GetRootGameObjects()[0]
                    .GetComponent<FpgRoomArtRoot>();
                MonoBehaviour binding = FindBehaviour(
                    root.gameObject,
                    "FPG.Demo.Unity.FpgVolumetricRoomArtBinding");
                Assert.That(binding, Is.Not.Null);

                MonoBehaviour manager = FindBehaviour(
                    root.gameObject,
                    "VolumetricFogAndMist2.VolumetricFogManager");
                MonoBehaviour fog = FindBehaviour(
                    root.gameObject,
                    "VolumetricFogAndMist2.VolumetricFog");
                MonoBehaviour sync = FindBehaviour(
                    root.gameObject,
                    "VolumetricLights.VolumetricLightDirectionalSync");
                MonoBehaviour volumetricLight = FindBehaviour(
                    root.gameObject,
                    "VolumetricLights.VolumetricLight");
                Assert.That(manager, Is.Not.Null);
                Assert.That(fog, Is.Not.Null);
                Assert.That(sync, Is.Not.Null);
                Assert.That(volumetricLight, Is.Not.Null);

                SerializedObject managerData = new SerializedObject(manager);
                SerializedObject fogData = new SerializedObject(fog);
                SerializedObject syncData = new SerializedObject(sync);
                SerializedObject volumetricLightData =
                    new SerializedObject(volumetricLight);
                bool managerEnabled = manager.enabled;
                bool fogEnabled = fog.enabled;
                bool syncEnabled = sync.enabled;
                bool volumetricLightEnabled = volumetricLight.enabled;
                Light mainLight = RenderSettings.sun;
                Assert.That(mainLight, Is.Not.Null);

                cameraObject = new GameObject("Room Art Binding Test Camera")
                {
                    hideFlags = HideFlags.HideInHierarchy
                        | HideFlags.DontSaveInEditor
                        | HideFlags.NotEditable
                };
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(
                    new Vector3(0f, 6.78f, -9.96f),
                    Quaternion.Euler(0.86f, 0f, 0f));
                bool dirtyBeforeBinding = scene.isDirty;

                Assert.That(
                    root.TryBindPresentation(
                        new FpgRoomArtPresentationContext(
                            camera,
                            mainLight,
                            null),
                        out string error),
                    Is.True,
                    error);
                Assert.That(root.IsPresentationBound, Is.True);

                managerData.Update();
                fogData.Update();
                syncData.Update();
                volumetricLightData.Update();
                Assert.That(
                    managerData.FindProperty("sun").objectReferenceValue,
                    Is.SameAs(mainLight));
                Assert.That(
                    fogData.FindProperty("updateModeCamera").objectReferenceValue,
                    Is.SameAs(camera));
                Assert.That(
                    fogData.FindProperty("fadeController").objectReferenceValue,
                    Is.SameAs(camera.transform));
                Assert.That(
                    volumetricLightData.FindProperty("targetCamera")
                        .objectReferenceValue,
                    Is.SameAs(camera.transform));
                Assert.That(
                    syncData.FindProperty("directionalLight").objectReferenceValue,
                    Is.SameAs(mainLight));
                Assert.That(
                    syncData.FindProperty("follow").objectReferenceValue,
                    Is.SameAs(camera.transform));
                Assert.That(manager.enabled, Is.EqualTo(managerEnabled));
                Assert.That(fog.enabled, Is.EqualTo(fogEnabled));
                Assert.That(sync.enabled, Is.EqualTo(syncEnabled));
                Assert.That(
                    volumetricLight.enabled,
                    Is.EqualTo(volumetricLightEnabled));

                Assert.That(
                    root.TryBindPresentation(
                        new FpgRoomArtPresentationContext(
                            camera,
                            null,
                            null),
                        out string noSunError),
                    Is.True,
                    noSunError);
                managerData.Update();
                fogData.Update();
                syncData.Update();
                Assert.That(
                    managerData.FindProperty("sun").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    fogData.FindProperty("updateModeCamera").objectReferenceValue,
                    Is.SameAs(camera));
                Assert.That(
                    syncData.FindProperty("directionalLight").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    syncData.FindProperty("follow").objectReferenceValue,
                    Is.SameAs(camera.transform));
                root.UnbindPresentation();
                Assert.That(root.IsPresentationBound, Is.False);
                managerData.Update();
                fogData.Update();
                syncData.Update();
                volumetricLightData.Update();
                Assert.That(
                    managerData.FindProperty("sun").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    fogData.FindProperty("updateModeCamera").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    fogData.FindProperty("fadeController").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    volumetricLightData.FindProperty("targetCamera")
                        .objectReferenceValue,
                    Is.Null);
                Assert.That(
                    syncData.FindProperty("directionalLight").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    syncData.FindProperty("follow").objectReferenceValue,
                    Is.Null);
                Assert.That(manager.enabled, Is.EqualTo(managerEnabled));
                Assert.That(fog.enabled, Is.EqualTo(fogEnabled));
                Assert.That(sync.enabled, Is.EqualTo(syncEnabled));
                Assert.That(
                    volumetricLight.enabled,
                    Is.EqualTo(volumetricLightEnabled));
                Assert.That(scene.isDirty, Is.EqualTo(dirtyBeforeBinding));
            }
            finally
            {
                root?.UnbindPresentation();
                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }
                if (previousActive.IsValid() && previousActive.isLoaded
                    && SceneManager.GetActiveScene() != previousActive)
                {
                    SceneManager.SetActiveScene(previousActive);
                }
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void Root1PresentationBindingBindsWithoutManualArraysInMultiRootScene()
        {
            FpgRoomDefinition room =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(Root1RoomPath);
            Assert.That(room, Is.Not.Null, Root1RoomPath);

            Scene scene = EditorSceneManager.NewPreviewScene();

            GameObject cameraObject = null;
            GameObject authoredObject = null;
            FpgRoomArtRoot root = null;
            try
            {
                authoredObject = new GameObject("Root1 Authored Object");
                SceneManager.MoveGameObjectToScene(authoredObject, scene);

                GameObject rootObject = new GameObject("Root1 Art Root");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
                root = rootObject.AddComponent<FpgRoomArtRoot>();
                SetArtRootRoom(root, room);

                Type bindingType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(
                        "FPG.Demo.Unity.FpgVolumetricRoomArtBinding",
                        false))
                    .FirstOrDefault(type => type != null);
                Assert.That(bindingType, Is.Not.Null);
                MonoBehaviour bindingComponent =
                    rootObject.AddComponent(bindingType) as MonoBehaviour;
                Assert.That(bindingComponent, Is.Not.Null);

                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out root,
                        out string resolveError),
                    Is.True,
                    resolveError);

                Assert.That(FindBehaviour(
                    root.gameObject,
                    "FPG.Demo.Unity.FpgVolumetricRoomArtBinding"),
                    Is.SameAs(bindingComponent));
                SerializedObject bindingData =
                    new SerializedObject(bindingComponent);
                Assert.That(bindingData.FindProperty("fogManager"), Is.Null);
                Assert.That(bindingData.FindProperty("fogVolumes"), Is.Null);
                Assert.That(bindingData.FindProperty("volumetricLights"), Is.Null);
                Assert.That(bindingData.FindProperty("directionalSyncs"), Is.Null);

                cameraObject = new GameObject("Root1 Formal Camera")
                {
                    hideFlags = HideFlags.HideInHierarchy
                        | HideFlags.DontSaveInEditor
                        | HideFlags.NotEditable
                };
                Camera camera = cameraObject.AddComponent<Camera>();
                Assert.That(
                    root.TryBindPresentation(
                        new FpgRoomArtPresentationContext(
                            camera,
                            RenderSettings.sun,
                            null),
                        out string bindError),
                    Is.True,
                    bindError);
                Assert.That(root.IsPresentationBound, Is.True);

                root.UnbindPresentation();
                Assert.That(root.IsPresentationBound, Is.False);
            }
            finally
            {
                root?.UnbindPresentation();
                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        [Test]
        public void PresentationBindingAllowsNoAdaptersAndNoMainLight()
        {
            GameObject rootObject = new GameObject("Room Art Without Effects");
            GameObject cameraObject = new GameObject("Formal Camera");
            try
            {
                FpgRoomArtRoot root = rootObject.AddComponent<FpgRoomArtRoot>();
                Camera camera = cameraObject.AddComponent<Camera>();

                Assert.That(
                    root.TryBindPresentation(
                        new FpgRoomArtPresentationContext(
                            camera,
                            null,
                            null),
                        out string error),
                    Is.True,
                    error);
                Assert.That(root.IsPresentationBound, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PresentationBindingExceptionDoesNotRejectOtherBindings()
        {
            GameObject rootObject = new GameObject("Room Art Binding Isolation");
            GameObject cameraObject = new GameObject("Formal Camera");
            try
            {
                FpgRoomArtRoot root = rootObject.AddComponent<FpgRoomArtRoot>();
                ThrowingRoomArtPresentationBinding throwing =
                    new GameObject("Throwing Binding")
                        .AddComponent<ThrowingRoomArtPresentationBinding>();
                throwing.transform.SetParent(rootObject.transform, false);
                TrackingRoomArtPresentationBinding tracking =
                    new GameObject("Tracking Binding")
                        .AddComponent<TrackingRoomArtPresentationBinding>();
                tracking.transform.SetParent(rootObject.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();

                LogAssert.Expect(
                    LogType.Warning,
                    new Regex(
                        "presentation binding 'ThrowingRoomArtPresentationBinding' "
                        + "threw while binding;.*Expected room art binding failure"));
                Assert.That(
                    root.TryBindPresentation(
                        new FpgRoomArtPresentationContext(
                            camera,
                            null,
                            null),
                        out string error),
                    Is.True,
                    error);
                Assert.That(throwing.UnbindCount, Is.EqualTo(1));
                Assert.That(tracking.IsBound, Is.True);
                Assert.That(root.IsPresentationBound, Is.True);

                root.UnbindPresentation();
                Assert.That(tracking.IsBound, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static MonoBehaviour FindBehaviour(
            GameObject root,
            string fullTypeName)
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] != null
                    && string.Equals(
                        behaviours[index].GetType().FullName,
                        fullTypeName,
                        StringComparison.Ordinal))
                {
                    return behaviours[index];
                }
            }

            return null;
        }

        private static string CreateTemporaryFolder()
        {
            const string parent = "Assets/FPGDemo/Tests/EditMode";
            string name = "__RoomArtTemp_" + Guid.NewGuid().ToString("N");
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

        private static void SetArtRootRoom(
            FpgRoomArtRoot root,
            FpgRoomDefinition room)
        {
            SerializedObject data = new SerializedObject(root);
            data.FindProperty("roomDefinition").objectReferenceValue = room;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public sealed class ThrowingRoomArtPresentationBinding : MonoBehaviour,
        IFpgRoomArtPresentationBinding
    {
        public int UnbindCount { get; private set; }

        public bool TryBind(
            FpgRoomArtPresentationContext context,
            out string error)
        {
            throw new InvalidOperationException(
                "Expected room art binding failure");
        }

        public void Unbind()
        {
            UnbindCount++;
        }
    }

    public sealed class TrackingRoomArtPresentationBinding : MonoBehaviour,
        IFpgRoomArtPresentationBinding
    {
        public bool IsBound { get; private set; }

        public bool TryBind(
            FpgRoomArtPresentationContext context,
            out string error)
        {
            IsBound = true;
            error = string.Empty;
            return true;
        }

        public void Unbind()
        {
            IsBound = false;
        }
    }
}
