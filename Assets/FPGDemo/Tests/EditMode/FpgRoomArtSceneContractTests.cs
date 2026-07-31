using System;
using System.Collections.Generic;
using System.IO;
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
            "Assets/FPGDemo/Presentation/Level/Rooms/Forest/ART_Forest 1_Copy.unity";
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
        public void SceneValidatorRejectsGameplayHostsAndAllowsPresentationOptions()
        {
            string folder = CreateTemporaryFolder();
            string scenePath = folder + "/Contract.unity";
            string roomPath = folder + "/Room.asset";
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = default(Scene);
            FpgRoomDefinition room = null;
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

                GameObject hostObject = new GameObject("Forbidden Gameplay Host");
                hostObject.transform.SetParent(rootObject.transform, false);
                hostObject.AddComponent<FpgEncounterHost>();
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                Assert.That(
                    FpgRoomArtSceneContractValidator.TryValidateScene(
                        room,
                        out string hostError),
                    Is.False);
                StringAssert.Contains(nameof(FpgEncounterHost), hostError);
                UnityEngine.Object.DestroyImmediate(hostObject);

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
                AssetDatabase.DeleteAsset(folder);
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
        public void Root1CopyPresentationBindsWithoutManualArrays()
        {
            FpgRoomDefinition room =
                AssetDatabase.LoadAssetAtPath<FpgRoomDefinition>(Root1RoomPath);
            Assert.That(room, Is.Not.Null, Root1RoomPath);

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(Root1ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(
                    Root1ScenePath,
                    OpenSceneMode.Additive);
            }

            GameObject cameraObject = null;
            FpgRoomArtRoot root = null;
            try
            {
                Assert.That(SceneManager.SetActiveScene(scene), Is.True);
                Assert.That(
                    FpgRoomArtRoot.TryResolve(
                        scene,
                        room,
                        out root,
                        out string resolveError),
                    Is.True,
                    resolveError);

                MonoBehaviour binding = FindBehaviour(
                    root.gameObject,
                    "FPG.Demo.Unity.FpgVolumetricRoomArtBinding");
                Assert.That(binding, Is.Not.Null);
                SerializedObject bindingData = new SerializedObject(binding);
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
