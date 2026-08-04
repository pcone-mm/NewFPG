using System;
using System.Collections.Generic;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    internal sealed class FpgRoomSceneTool : IDisposable
    {
        private const HideFlags PreviewHideFlags =
            HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.NotEditable;
        private const float PositionWriteEpsilon = 0.0005f;
        private const float RotationWriteEpsilonDegrees = 0.05f;
        private static readonly System.Reflection.PropertyInfo AllowGpuDrivenRenderingProperty =
            typeof(Renderer).GetProperty(
                "allowGPUDrivenRendering",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

        private readonly Dictionary<FpgRoomMarkerKind, bool> visibility =
            new Dictionary<FpgRoomMarkerKind, bool>();
        private readonly RaycastHit[] environmentRaycastHits = new RaycastHit[256];


        private ScriptableObject room;
        private Scene roomPreviewScene;

        private GameObject previewRoot;
        private GameObject environmentPreview;
        private FpgRoomArtRoot roomArtRoot;
        private GameObject cameraPreviewRoot;
        private GameObject cameraPreviewPlayer;
        private Transform cameraPreviewPlayerAnchor;
        private Transform cameraPreviewRig;
        private Camera cameraPreviewCamera;
        private FpgCoverCameraProfile cameraPreviewProfile;
        private FpgCoverCameraProfile cameraTemplate;
        private D0CharacterDefinition cameraPreviewCharacter;
        private D0ThreeCProfile cameraPreviewThreeC;
        private FpgRoomMarkerKind? placementKind;
        private FpgRoomMarkerHandle selectedMarker;
        private int cameraPreviewCoverIndex = -1;
        private int previousCameraPreviewCoverIndex = -1;
        private int cameraPreviewProfileDirtyCount;
        private int cameraPreviewRoomDirtyCount;
        private bool cameraTransitionActive;
        private double cameraTransitionStartedAt;
        private float cameraTransitionDuration;
        private FpgResolvedCameraShot cameraTransitionSource;
        private FpgResolvedCameraShot cameraTransitionTarget;
        private Pose cameraTransitionSourcePlayerPose;
        private Pose cameraTransitionTargetPlayerPose;
        private bool cameraPreviewActive;
        private bool disposed;
        private bool previewRefreshQueued;
        private bool cameraPreviewRefreshQueued;

        internal FpgRoomSceneTool()
        {
            foreach (FpgRoomMarkerKind kind in Enum.GetValues(typeof(FpgRoomMarkerKind)))
            {
                visibility[kind] = true;
            }

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        internal event Action<FpgRoomMarkerHandle> SelectionChanged;
        internal event Action RoomChanged;
        internal event Action<bool, string> CameraPreviewStateChanged;

        internal ScriptableObject Room => room;
        internal FpgRoomMarkerHandle SelectedMarker => selectedMarker;
        internal FpgRoomMarkerKind? PlacementKind => placementKind;
        internal bool IsCameraPreviewActive => cameraPreviewActive;
        internal float GridSnap { get; set; } = 0.5f;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            cameraPreviewActive = false;
            cameraPreviewProfile = null;
            cameraPreviewCharacter = null;
            DestroyPreview();
        }

        internal void SetRoom(ScriptableObject value)
        {
            if (room == value)
            {
                return;
            }

            room = value;
            placementKind = null;
            SetSelectedMarker(null);
            if (cameraPreviewActive && room != null
                && !TryChooseInitialCameraCover(out string cameraError))
            {
                StopCameraPreviewInternal(cameraError, true);
            }
            RebuildPreview();
            SceneView.RepaintAll();
        }

        internal void SetCameraTemplate(FpgCoverCameraProfile profile)
        {
            cameraTemplate = profile;
        }

        internal bool TryStartCameraPreview(
            D0CharacterDefinition character,
            out string error)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Formal camera preview is only available in Edit Mode.";
                return false;
            }

            cameraPreviewCharacter = character;
            cameraPreviewActive = false;
            DestroyCameraPreviewObjects();
            if (previewRoot == null)
            {
                RebuildPreview();
            }

            if (!TryChooseInitialCameraCover(out error))
            {
                return false;
            }

            cameraPreviewActive = true;
            if (!TryRebuildCameraPreview(out error))
            {
                StopCameraPreviewInternal(error, true);
                return false;
            }

            CameraPreviewStateChanged?.Invoke(
                true,
                $"Cover camera preview is active for cover {cameraPreviewCoverIndex + 1}.");
            return true;
        }

        internal void StopCameraPreview()
        {
            StopCameraPreviewInternal("Formal camera preview stopped.", true);
        }

        internal void PrepareForSceneSaveOrSwitch()
        {
            DestroyPreview();
        }

        internal bool TryRefreshCameraPreview(out string error)
        {
            if (!cameraPreviewActive)
            {
                error = string.Empty;
                return true;
            }

            if (!TryApplyCurrentCameraShot(out error)
                && !TryRebuildCameraPreview(out error))
            {
                StopCameraPreviewInternal(error, true);
                return false;
            }

            CameraPreviewStateChanged?.Invoke(true, "Formal camera preview updated.");
            return true;
        }

        internal void SetPlacementKind(FpgRoomMarkerKind? kind)
        {
            placementKind = placementKind == kind ? null : kind;
            SceneView.RepaintAll();
        }

        internal void SetVisibility(FpgRoomMarkerKind kind, bool visible)
        {
            visibility[kind] = visible;
            SceneView.RepaintAll();
        }

        internal void SelectMarker(FpgRoomMarkerHandle marker, bool frame)
        {
            SetSelectedMarker(marker);
            if (frame)
            {
                FrameSelection();
            }

            SceneView.RepaintAll();
        }

        internal void AddMarker(FpgRoomMarkerKind kind, Vector3 localPosition, Quaternion localRotation)
        {
            if (room == null)
            {
                return;
            }

            string markerId = FpgRoomAuthoringSchema.CreateSemanticMarkerId(
                room,
                kind);
            FpgCoverCameraProfile createdCameraProfile = null;
            if (kind == FpgRoomMarkerKind.Cover)
            {
                if (!(room is FpgRoomDefinition definition))
                {
                    Debug.LogError(
                        "A RoomDefinition is required before placing a cover.",
                        room);
                    return;
                }

                if (!FpgCoverCameraProfileAuthoring.TryCloneForCover(
                        cameraTemplate,
                        definition,
                        markerId,
                        out createdCameraProfile,
                        out string cameraError))
                {
                    Debug.LogError(
                        string.IsNullOrWhiteSpace(cameraError)
                            ? "A valid cover camera template is required before placing a cover."
                            : cameraError,
                        room);
                    return;
                }
            }

            Undo.RecordObject(room, "鏀剧疆鎴块棿鏍囪");
            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty array = serializedRoom.FindProperty(FpgRoomAuthoringSchema.MarkerArrayName(kind));
            if (array == null || !array.isArray)
            {
                Debug.LogError(
                    $"Room marker array '{FpgRoomAuthoringSchema.MarkerArrayName(kind)}' is unavailable.",
                    room);
                return;
            }

            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            SerializedProperty marker = array.GetArrayElementAtIndex(index);
            SetString(marker, markerId, "markerId", "id");
            SetString(marker, FpgRoomAuthoringSchema.MarkerKindName(kind) + " " + (index + 1), "displayName", "name");
            FpgRoomAuthoringSchema.SetMarkerPosition(marker, Snap(localPosition));
            FpgRoomAuthoringSchema.SetMarkerRotation(marker, localRotation);

            if (kind == FpgRoomMarkerKind.Destructible)
            {
                SerializedProperty prefab = FpgRoomAuthoringSchema.FindRelative(marker, "prefab", "destructiblePrefab");
                if (prefab != null)
                {
                    prefab.objectReferenceValue = null;
                }
            }
            else if (kind == FpgRoomMarkerKind.EnemySpawn)
            {
                SerializedProperty role = FpgRoomAuthoringSchema.FindRelative(marker, "role", "enemyRole");
                if (role != null)
                {
                    role.intValue = 0;
                }
            }
            else if (kind == FpgRoomMarkerKind.Cover)
            {
                SerializedProperty prefab =
                    FpgRoomAuthoringSchema.FindRelative(marker, "prefab");
                SerializedProperty durability =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "maxDurability");
                SerializedProperty cameraProfile =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "cameraProfile");
                SerializedProperty startingCover =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "isStartingCover");
                SerializedProperty reachablePosition =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "playerReachableLocalPosition");
                SerializedProperty reachableRotation =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "playerReachableLocalEulerAngles");
                SerializedProperty leftPeekPosition =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "playerLeftPeekLocalPosition");
                SerializedProperty rightPeekPosition =
                    FpgRoomAuthoringSchema.FindRelative(
                        marker,
                        "playerRightPeekLocalPosition");
                if (prefab != null) prefab.objectReferenceValue = null;
                if (cameraProfile != null)
                {
                    cameraProfile.objectReferenceValue = createdCameraProfile;
                }
                if (durability != null) durability.intValue = 100;
                if (startingCover != null) startingCover.boolValue = index == 0;
                if (reachablePosition != null)
                {
                    reachablePosition.vector3Value = Snap(localPosition);
                }
                if (reachableRotation != null)
                {
                    reachableRotation.vector3Value = localRotation.eulerAngles;
                }
                Vector3 reachable = Snap(localPosition);
                if (leftPeekPosition != null)
                {
                    leftPeekPosition.vector3Value = Snap(
                        reachable
                        + localRotation * new Vector3(-1.35f, 0f, 0f));
                }
                if (rightPeekPosition != null)
                {
                    rightPeekPosition.vector3Value = Snap(
                        reachable
                        + localRotation * new Vector3(1.35f, 0f, 0f));
                }
            }

            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            SetSelectedMarker(new FpgRoomMarkerHandle(kind, index, markerId, string.Empty));
            RefreshMarkerVisualization(kind);
            if (kind == FpgRoomMarkerKind.PlayerEntry)
            {
                QueueCameraPreviewRefresh();
            }
            RoomChanged?.Invoke();
        }

        internal void DuplicateSelectedMarker()
        {
            if (room == null || selectedMarker == null)
            {
                return;
            }

            Undo.RecordObject(room, "澶嶅埗鎴块棿鏍囪");
            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty array = serializedRoom.FindProperty(
                FpgRoomAuthoringSchema.MarkerArrayName(selectedMarker.Kind));
            if (array == null || !array.isArray || selectedMarker.Index < 0 || selectedMarker.Index >= array.arraySize)
            {
                return;
            }

            FpgRoomMarkerKind markerKind = selectedMarker.Kind;
            string id = FpgRoomAuthoringSchema.CreateSemanticMarkerId(
                room,
                markerKind);
            FpgCoverCameraProfile duplicatedCameraProfile = null;
            if (markerKind == FpgRoomMarkerKind.Cover)
            {
                SerializedProperty sourceCover = array.GetArrayElementAtIndex(
                    selectedMarker.Index);
                FpgCoverCameraProfile sourceProfile =
                    sourceCover.FindPropertyRelative("cameraProfile")
                        ?.objectReferenceValue as FpgCoverCameraProfile;
                if (!(room is FpgRoomDefinition definition))
                {
                    Debug.LogError(
                        "A RoomDefinition is required before duplicating a cover.",
                        room);
                    return;
                }

                if (!FpgCoverCameraProfileAuthoring.TryCloneForCover(
                        sourceProfile,
                        definition,
                        id,
                        out duplicatedCameraProfile,
                        out string cameraError))
                {
                    Debug.LogError(
                        string.IsNullOrWhiteSpace(cameraError)
                            ? "The selected cover requires a saved camera profile before it can be duplicated."
                            : cameraError,
                        room);
                    return;
                }
            }

            int duplicateIndex = selectedMarker.Index;
            array.InsertArrayElementAtIndex(duplicateIndex);
            SerializedProperty duplicate = array.GetArrayElementAtIndex(duplicateIndex);
            SetString(duplicate, id, "markerId", "id");
            float duplicateOffset = float.IsNaN(GridSnap) || float.IsInfinity(GridSnap) || GridSnap <= 0f
                ? 0.5f
                : GridSnap;
            Vector3 offset = new Vector3(duplicateOffset, 0f, 0f);
            FpgRoomAuthoringSchema.SetMarkerPosition(
                duplicate,
                Snap(FpgRoomAuthoringSchema.GetMarkerPosition(duplicate) + offset));
            if (markerKind == FpgRoomMarkerKind.Cover)
            {
                SerializedProperty cameraProfile =
                    duplicate.FindPropertyRelative("cameraProfile");
                SerializedProperty reachablePosition =
                    FpgRoomAuthoringSchema.FindRelative(
                        duplicate,
                        "playerReachableLocalPosition");
                SerializedProperty leftPeekPosition =
                    FpgRoomAuthoringSchema.FindRelative(
                        duplicate,
                        "playerLeftPeekLocalPosition");
                SerializedProperty rightPeekPosition =
                    FpgRoomAuthoringSchema.FindRelative(
                        duplicate,
                        "playerRightPeekLocalPosition");
                SerializedProperty startingCover =
                    FpgRoomAuthoringSchema.FindRelative(
                        duplicate,
                        "isStartingCover");
                if (cameraProfile != null)
                {
                    cameraProfile.objectReferenceValue = duplicatedCameraProfile;
                }
                if (reachablePosition != null)
                {
                    reachablePosition.vector3Value =
                        reachablePosition.vector3Value + offset;
                }
                if (leftPeekPosition != null)
                {
                    leftPeekPosition.vector3Value =
                        leftPeekPosition.vector3Value + offset;
                }
                if (rightPeekPosition != null)
                {
                    rightPeekPosition.vector3Value =
                        rightPeekPosition.vector3Value + offset;
                }

                if (startingCover != null)
                {
                    startingCover.boolValue = false;
                }
            }

            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            SetSelectedMarker(new FpgRoomMarkerHandle(markerKind, duplicateIndex, id, string.Empty));
            RefreshMarkerVisualization(markerKind);
            if (markerKind == FpgRoomMarkerKind.PlayerEntry)
            {
                QueueCameraPreviewRefresh();
            }
            RoomChanged?.Invoke();
        }

        internal void DeleteSelectedMarker()
        {
            if (room == null || selectedMarker == null)
            {
                return;
            }

            Undo.RecordObject(room, "鍒犻櫎鎴块棿鏍囪");
            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty array = serializedRoom.FindProperty(
                FpgRoomAuthoringSchema.MarkerArrayName(selectedMarker.Kind));
            if (array == null || !array.isArray || selectedMarker.Index < 0 || selectedMarker.Index >= array.arraySize)
            {
                return;
            }

            FpgRoomMarkerKind markerKind = selectedMarker.Kind;
            array.DeleteArrayElementAtIndex(selectedMarker.Index);
            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            SetSelectedMarker(null);
            RefreshMarkerVisualization(markerKind);
            if (markerKind == FpgRoomMarkerKind.PlayerEntry)
            {
                QueueCameraPreviewRefresh();
            }
            RoomChanged?.Invoke();
        }

        private void RefreshMarkerVisualization(FpgRoomMarkerKind kind)
        {
            if (kind == FpgRoomMarkerKind.Destructible
                || kind == FpgRoomMarkerKind.Cover)
            {
                RebuildPreview();
                return;
            }

            SceneView.RepaintAll();
        }

        internal void FrameSelection()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            if (room != null && selectedMarker != null)
            {
                SerializedObject serializedRoom = new SerializedObject(room);
                SerializedProperty marker = FpgRoomAuthoringSchema.FindMarkerProperty(
                    serializedRoom, selectedMarker.Kind, selectedMarker.Index);
                if (marker != null)
                {
                    sceneView.Frame(new Bounds(FpgRoomAuthoringSchema.GetMarkerPosition(marker), Vector3.one * 2f), false);
                    return;
                }
            }

            if (TryGetPreviewBounds(out Bounds bounds))
            {
                sceneView.Frame(bounds, false);
            }
        }

        internal void RebuildPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopCameraPreviewInternal(
                    "Formal camera preview is unavailable in Play Mode.",
                    true);
                DestroyPreview();
                return;
            }

            DestroyPreview();
            if (room == null)
            {
                if (cameraPreviewActive)
                {
                    StopCameraPreviewInternal(
                        "Formal camera preview requires a selected room.",
                        true);
                }
                return;
            }

            if (!(room is FpgRoomDefinition definition))
            {
                if (cameraPreviewActive)
                {
                    StopCameraPreviewInternal(
                        "Room preview requires a valid RoomDefinition.",
                        true);
                }
                return;
            }

            roomPreviewScene = SceneManager.GetActiveScene();
            if (!roomPreviewScene.IsValid()
                || !roomPreviewScene.isLoaded
                || !string.Equals(
                    roomPreviewScene.path,
                    definition.ArtScenePath,
                    StringComparison.Ordinal))
            {
                roomPreviewScene = default;
                return;
            }

            if (!FpgRoomArtRoot.TryResolve(
                    roomPreviewScene,
                    definition,
                    out roomArtRoot,
                    out string artRootError))
            {
                StopCameraPreviewInternal(artRootError, true);
                roomPreviewScene = default;
                return;
            }

            environmentPreview = roomArtRoot.gameObject;

            previewRoot = new GameObject("__FPG Room Authoring Preview__")
            {
                hideFlags = PreviewHideFlags
            };
            SceneManager.MoveGameObjectToScene(previewRoot, roomPreviewScene);
            previewRoot.SetActive(false);

            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty destructibles = serializedRoom.FindProperty("destructibleSlots");
            if (destructibles != null && destructibles.isArray)
            {
                for (int index = 0; index < destructibles.arraySize; index++)
                {
                    SerializedProperty slot = destructibles.GetArrayElementAtIndex(index);
                    GameObject prefab = FpgRoomAuthoringSchema.FindRelative(
                        slot,
                        "prefab",
                        "destructiblePrefab")?.objectReferenceValue as GameObject;
                    if (prefab == null)
                    {
                        continue;
                    }

                    InstantiatePreview(
                        prefab,
                        previewRoot.transform,
                        FpgRoomAuthoringSchema.GetMarkerPosition(slot),
                        FpgRoomAuthoringSchema.GetMarkerRotation(slot));
                }
            }

            SerializedProperty covers = serializedRoom.FindProperty("coverSlots");
            if (covers != null && covers.isArray)
            {
                for (int index = 0; index < covers.arraySize; index++)
                {
                    SerializedProperty slot = covers.GetArrayElementAtIndex(index);
                    GameObject prefab = FpgRoomAuthoringSchema.FindRelative(
                        slot,
                        "prefab")?.objectReferenceValue as GameObject;
                    if (prefab == null)
                    {
                        continue;
                    }

                    InstantiatePreview(
                        prefab,
                        previewRoot.transform,
                        FpgRoomAuthoringSchema.GetMarkerPosition(slot),
                        FpgRoomAuthoringSchema.GetMarkerRotation(slot));
                }
            }

            previewRoot.SetActive(true);
            Physics.SyncTransforms();
            if (cameraPreviewActive && !TryRebuildCameraPreview(out string cameraError))
            {
                StopCameraPreviewInternal(cameraError, true);
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (room == null
                || EditorApplication.isPlayingOrWillChangePlaymode
                || !roomPreviewScene.IsValid()
                || !roomPreviewScene.isLoaded)
            {
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }

            HandleKeyboardShortcuts();
            HandlePlacement();
            DrawMarkers();
            DrawSelectedHandle();
            DrawCameraComposition();
        }

        private void HandleKeyboardShortcuts()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || selectedMarker == null)
            {
                return;
            }

            if (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace)
            {
                DeleteSelectedMarker();
                current.Use();
            }
            else if (current.keyCode == KeyCode.D && (current.control || current.command))
            {
                DuplicateSelectedMarker();
                current.Use();
            }
            else if (current.keyCode == KeyCode.F)
            {
                FrameSelection();
                current.Use();
            }
        }

        private void HandlePlacement()
        {
            if (!placementKind.HasValue)
            {
                return;
            }

            Event current = Event.current;
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (current.type != EventType.MouseDown || current.button != 0 || current.alt)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 position;
            Quaternion rotation = Quaternion.identity;
            if (TryRaycastEnvironment(ray, out RaycastHit hit))
            {
                position = hit.point;
                Vector3 normal = hit.normal.sqrMagnitude > 0.001f
                    ? hit.normal.normalized
                    : Vector3.up;
                Vector3 forward = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.7f
                    ? normal
                    : Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
                if (forward.sqrMagnitude > 0.001f)
                {
                    rotation = Quaternion.LookRotation(forward, Vector3.up);
                }
            }
            else
            {
                Plane plane = new Plane(Vector3.up, Vector3.zero);
                if (!plane.Raycast(ray, out float distance))
                {
                    return;
                }

                position = ray.GetPoint(distance);
            }

            AddMarker(placementKind.Value, position, rotation);
            current.Use();
        }

        private void DrawMarkers()
        {
            SerializedObject serializedRoom = new SerializedObject(room);
            foreach (FpgRoomMarkerHandle handle in FpgRoomAuthoringSchema.GetMarkers(room))
            {
                if (!visibility.TryGetValue(handle.Kind, out bool visible) || !visible)
                {
                    continue;
                }

                SerializedProperty marker = FpgRoomAuthoringSchema.FindMarkerProperty(
                    serializedRoom, handle.Kind, handle.Index);
                if (marker == null)
                {
                    continue;
                }

                Vector3 position = FpgRoomAuthoringSchema.GetMarkerPosition(marker);
                Quaternion rotation = FpgRoomAuthoringSchema.GetMarkerRotation(marker);
                float size = HandleUtility.GetHandleSize(position) * 0.12f;
                bool isSelected = IsSameMarker(handle, selectedMarker);
                Handles.color = isSelected ? Color.yellow : FpgRoomAuthoringSchema.MarkerColor(handle.Kind);
                if (Handles.Button(position, rotation, size, size * 1.25f, Handles.SphereHandleCap))
                {
                    SetSelectedMarker(handle);
                }

                Handles.ArrowHandleCap(0, position, rotation, size * 2.2f, EventType.Repaint);
                string label = string.IsNullOrWhiteSpace(handle.DisplayName) ? handle.MarkerId : handle.DisplayName;
                Handles.Label(position + Vector3.up * size * 1.4f, label);

                if (handle.Kind == FpgRoomMarkerKind.Cover)
                {
                    SerializedProperty reachablePositionProperty =
                        FpgRoomAuthoringSchema.FindRelative(
                            marker,
                            "playerReachableLocalPosition");
                    SerializedProperty reachableRotationProperty =
                        FpgRoomAuthoringSchema.FindRelative(
                            marker,
                            "playerReachableLocalEulerAngles");
                    SerializedProperty leftPeekPositionProperty =
                        FpgRoomAuthoringSchema.FindRelative(
                            marker,
                            "playerLeftPeekLocalPosition");
                    SerializedProperty rightPeekPositionProperty =
                        FpgRoomAuthoringSchema.FindRelative(
                            marker,
                            "playerRightPeekLocalPosition");
                    if (reachablePositionProperty == null
                        || reachableRotationProperty == null
                        || leftPeekPositionProperty == null
                        || rightPeekPositionProperty == null)
                    {
                        continue;
                    }

                    Vector3 reachablePosition =
                        reachablePositionProperty.vector3Value;
                    Quaternion reachableRotation = Quaternion.Euler(
                        reachableRotationProperty.vector3Value);
                    float reachableSize =
                        HandleUtility.GetHandleSize(reachablePosition) * 0.1f;
                    Handles.color = isSelected
                        ? new Color(1f, 0.95f, 0.4f)
                        : new Color(0.24f, 0.88f, 0.96f);
                    Handles.DrawDottedLine(position, reachablePosition, 4f);
                    if (Handles.Button(
                            reachablePosition,
                            reachableRotation,
                            reachableSize,
                            reachableSize * 1.25f,
                            Handles.CubeHandleCap))
                    {
                        SetSelectedMarker(handle);
                    }

                    Handles.ArrowHandleCap(
                        0,
                        reachablePosition,
                        reachableRotation,
                        reachableSize * 2.2f,
                        EventType.Repaint);
                    Handles.Label(
                        reachablePosition + Vector3.up * reachableSize * 1.4f,
                        "玩家到达点");
                    DrawCoverPeekMarker(
                        handle,
                        isSelected,
                        reachablePosition,
                        leftPeekPositionProperty.vector3Value,
                        "左侧探身点",
                        new Color(0.2f, 0.82f, 0.42f));
                    DrawCoverPeekMarker(
                        handle,
                        isSelected,
                        reachablePosition,
                        rightPeekPositionProperty.vector3Value,
                        "右侧探身点",
                        new Color(0.95f, 0.38f, 0.24f));
                }
            }
        }

        private void DrawCoverPeekMarker(
            FpgRoomMarkerHandle handle,
            bool isSelected,
            Vector3 reachablePosition,
            Vector3 peekPosition,
            string label,
            Color color)
        {
            float size = HandleUtility.GetHandleSize(peekPosition) * 0.085f;
            Handles.color = isSelected ? Color.Lerp(color, Color.white, 0.35f) : color;
            Handles.DrawDottedLine(reachablePosition, peekPosition, 3f);
            if (Handles.Button(
                    peekPosition,
                    Quaternion.identity,
                    size,
                    size * 1.2f,
                    Handles.SphereHandleCap))
            {
                SetSelectedMarker(handle);
            }

            Handles.Label(
                peekPosition + Vector3.up * size * 1.4f,
                label);
        }

        private void DrawSelectedHandle()
        {
            if (selectedMarker == null)
            {
                return;
            }

            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty marker = FpgRoomAuthoringSchema.FindMarkerProperty(
                serializedRoom, selectedMarker.Kind, selectedMarker.Index);
            if (marker == null)
            {
                SetSelectedMarker(null);
                return;
            }

            if (selectedMarker.Kind == FpgRoomMarkerKind.Cover)
            {
                DrawSelectedCoverReachableHandle(serializedRoom, marker);
                DrawSelectedCoverPeekHandle(
                    serializedRoom,
                    marker,
                    "playerLeftPeekLocalPosition",
                    "移动玩家左侧探身点");
                DrawSelectedCoverPeekHandle(
                    serializedRoom,
                    marker,
                    "playerRightPeekLocalPosition",
                    "移动玩家右侧探身点");
            }

            Vector3 position = FpgRoomAuthoringSchema.GetMarkerPosition(marker);
            Quaternion rotation = FpgRoomAuthoringSchema.GetMarkerRotation(marker);
            EditorGUI.BeginChangeCheck();
            if (Tools.current == Tool.Rotate)
            {
                rotation = Handles.RotationHandle(rotation, position);
            }
            else
            {
                position = Handles.PositionHandle(position, rotation);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            serializedRoom.Update();
            marker = FpgRoomAuthoringSchema.FindMarkerProperty(serializedRoom, selectedMarker.Kind, selectedMarker.Index);
            if (Tools.current == Tool.Rotate)
            {
                Quaternion currentRotation =
                    FpgRoomAuthoringSchema.GetMarkerRotation(marker);
                if (!HasMeaningfulRotationChange(currentRotation, rotation))
                {
                    return;
                }

                Undo.RecordObject(room, "Rotate Room Marker");
                FpgRoomAuthoringSchema.SetMarkerRotation(marker, rotation);
            }
            else
            {
                Vector3 nextPosition = Snap(position);
                if (!HasMeaningfulPositionChange(
                        FpgRoomAuthoringSchema.GetMarkerPosition(marker),
                        nextPosition))
                {
                    return;
                }

                Undo.RecordObject(room, "Move Room Marker");
                FpgRoomAuthoringSchema.SetMarkerPosition(marker, nextPosition);
            }

            if (!serializedRoom.ApplyModifiedProperties())
            {
                return;
            }

            EditorUtility.SetDirty(room);
            if (selectedMarker.Kind == FpgRoomMarkerKind.Destructible
                || selectedMarker.Kind == FpgRoomMarkerKind.Cover)
            {
                QueuePreviewRefresh();
            }
            else if (selectedMarker.Kind == FpgRoomMarkerKind.PlayerEntry)
            {
                QueueCameraPreviewRefresh();
            }

            RoomChanged?.Invoke();
        }

        private void DrawSelectedCoverReachableHandle(
            SerializedObject serializedRoom,
            SerializedProperty marker)
        {
            SerializedProperty reachablePositionProperty =
                FpgRoomAuthoringSchema.FindRelative(
                    marker,
                    "playerReachableLocalPosition");
            SerializedProperty reachableRotationProperty =
                FpgRoomAuthoringSchema.FindRelative(
                    marker,
                    "playerReachableLocalEulerAngles");
            if (reachablePositionProperty == null
                || reachableRotationProperty == null)
            {
                return;
            }

            Vector3 position = reachablePositionProperty.vector3Value;
            Quaternion rotation = Quaternion.Euler(
                reachableRotationProperty.vector3Value);
            EditorGUI.BeginChangeCheck();
            if (Tools.current == Tool.Rotate)
            {
                rotation = Handles.RotationHandle(rotation, position);
            }
            else
            {
                position = Handles.PositionHandle(position, rotation);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            serializedRoom.Update();
            marker = FpgRoomAuthoringSchema.FindMarkerProperty(
                serializedRoom,
                selectedMarker.Kind,
                selectedMarker.Index);
            reachablePositionProperty = FpgRoomAuthoringSchema.FindRelative(
                marker,
                "playerReachableLocalPosition");
            reachableRotationProperty = FpgRoomAuthoringSchema.FindRelative(
                marker,
                "playerReachableLocalEulerAngles");
            if (Tools.current == Tool.Rotate)
            {
                if (!HasMeaningfulEulerChange(
                        reachableRotationProperty.vector3Value,
                        rotation.eulerAngles))
                {
                    return;
                }

                Undo.RecordObject(room, "旋转玩家到达点");
                reachableRotationProperty.vector3Value =
                    rotation.eulerAngles;
            }
            else
            {
                Vector3 nextPosition = Snap(position);
                if (!HasMeaningfulPositionChange(
                        reachablePositionProperty.vector3Value,
                        nextPosition))
                {
                    return;
                }

                Undo.RecordObject(room, "移动玩家到达点");
                reachablePositionProperty.vector3Value = nextPosition;
            }

            if (!serializedRoom.ApplyModifiedProperties())
            {
                return;
            }

            EditorUtility.SetDirty(room);
            QueueCameraPreviewRefresh();
            RoomChanged?.Invoke();
        }

        private void DrawSelectedCoverPeekHandle(
            SerializedObject serializedRoom,
            SerializedProperty marker,
            string propertyName,
            string undoName)
        {
            if (Tools.current == Tool.Rotate)
            {
                return;
            }

            SerializedProperty positionProperty =
                FpgRoomAuthoringSchema.FindRelative(marker, propertyName);
            if (positionProperty == null)
            {
                return;
            }

            Vector3 position = positionProperty.vector3Value;
            EditorGUI.BeginChangeCheck();
            position = Handles.PositionHandle(position, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            serializedRoom.Update();
            marker = FpgRoomAuthoringSchema.FindMarkerProperty(
                serializedRoom,
                selectedMarker.Kind,
                selectedMarker.Index);
            positionProperty = FpgRoomAuthoringSchema.FindRelative(
                marker,
                propertyName);
            Vector3 nextPosition = Snap(position);
            if (positionProperty == null
                || !HasMeaningfulPositionChange(
                    positionProperty.vector3Value,
                    nextPosition))
            {
                return;
            }

            Undo.RecordObject(room, undoName);
            positionProperty.vector3Value = nextPosition;
            if (!serializedRoom.ApplyModifiedProperties())
            {
                return;
            }

            EditorUtility.SetDirty(room);
            RoomChanged?.Invoke();
        }

        private bool TryRaycastEnvironment(Ray ray, out RaycastHit closest)
        {
            closest = default;
            if (environmentPreview == null ||
                !roomPreviewScene.IsValid() ||
                !roomPreviewScene.isLoaded)
            {
                return false;
            }

            PhysicsScene physicsScene = roomPreviewScene.GetPhysicsScene();
            if (!physicsScene.IsValid())
            {
                return false;
            }

            int hitCount = physicsScene.Raycast(
                ray.origin,
                ray.direction,
                environmentRaycastHits,
                float.MaxValue,
                ~0,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = environmentRaycastHits[index];
                if (!hit.transform.IsChildOf(environmentPreview.transform) &&
                    hit.transform != environmentPreview.transform)
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closest = hit;
                    closestDistance = hit.distance;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetPreviewBounds(out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
            if (roomArtRoot == null && previewRoot == null)
            {
                return false;
            }

            bool found = false;
            if (roomArtRoot != null)
            {
                EncapsulateBounds(
                    roomArtRoot.GetComponentsInChildren<Renderer>(true),
                    roomArtRoot.GetComponentsInChildren<Collider>(true),
                    ref bounds,
                    ref found);
            }

            if (previewRoot != null)
            {
                EncapsulateBounds(
                    previewRoot.GetComponentsInChildren<Renderer>(true),
                    previewRoot.GetComponentsInChildren<Collider>(true),
                    ref bounds,
                    ref found);
            }

            return found;
        }

        private static void EncapsulateBounds(
            Renderer[] renderers,
            Collider[] colliders,
            ref Bounds bounds,
            ref bool found)
        {
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        private void OnUndoRedo()
        {
            RebuildPreview();
            RoomChanged?.Invoke();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            QueuePreviewRefresh();
        }

        private void OnSceneClosed(Scene scene)
        {
            QueuePreviewRefresh();
        }

        private void OnSceneSaving(Scene scene, string path)
        {
            if (room is FpgRoomDefinition definition
                && string.Equals(
                    scene.path,
                    definition.ArtScenePath,
                    StringComparison.Ordinal))
            {
                DestroyPreview();
            }
        }

        private void OnSceneSaved(Scene scene)
        {
            if (room is FpgRoomDefinition definition
                && string.Equals(
                    scene.path,
                    definition.ArtScenePath,
                    StringComparison.Ordinal))
            {
                QueuePreviewRefresh();
            }
        }

        internal void QueuePreviewRefresh()
        {
            if (disposed || room == null || EditorApplication.isPlayingOrWillChangePlaymode
                || previewRefreshQueued)
            {
                return;
            }

            previewRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                previewRefreshQueued = false;
                if (!disposed && room != null
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    RebuildPreview();
                }
            };
        }

        internal void QueueCameraPreviewRefresh()
        {
            if (disposed || !cameraPreviewActive
                || EditorApplication.isPlayingOrWillChangePlaymode
                || cameraPreviewRefreshQueued)
            {
                return;
            }

            cameraPreviewRefreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                cameraPreviewRefreshQueued = false;
                if (disposed || !cameraPreviewActive
                    || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                if (!TryRefreshCameraPreview(out string error))
                {
                    Debug.LogWarning(error, room);
                }
            };
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                StopCameraPreviewInternal(
                    "Formal camera preview stopped before entering Play Mode.",
                    true);
                DestroyPreview();
            }
            else if (state == PlayModeStateChange.EnteredEditMode && !disposed)
            {
                RebuildPreview();
            }
        }


        private void SetSelectedMarker(FpgRoomMarkerHandle marker)
        {
            selectedMarker = marker;
            SelectionChanged?.Invoke(marker);
            if (cameraPreviewActive
                && marker != null
                && marker.Kind == FpgRoomMarkerKind.Cover
                && marker.Index != cameraPreviewCoverIndex)
            {
                previousCameraPreviewCoverIndex = cameraPreviewCoverIndex;
                cameraPreviewCoverIndex = marker.Index;
                if (!TryRefreshCameraPreview(out string error))
                {
                    Debug.LogWarning(error, room);
                }
            }
        }

        private Vector3 Snap(Vector3 value)
        {
            if (float.IsNaN(GridSnap) || float.IsInfinity(GridSnap) || GridSnap <= 0f
                || float.IsNaN(value.x) || float.IsInfinity(value.x)
                || float.IsNaN(value.y) || float.IsInfinity(value.y)
                || float.IsNaN(value.z) || float.IsInfinity(value.z))
            {
                return value;
            }

            return new Vector3(
                Mathf.Round(value.x / GridSnap) * GridSnap,
                Mathf.Round(value.y / GridSnap) * GridSnap,
                Mathf.Round(value.z / GridSnap) * GridSnap);
        }

        private static bool HasMeaningfulPositionChange(
            Vector3 current,
            Vector3 next)
        {
            return (current - next).sqrMagnitude
                > PositionWriteEpsilon * PositionWriteEpsilon;
        }

        private static bool HasMeaningfulRotationChange(
            Quaternion current,
            Quaternion next)
        {
            return Quaternion.Angle(current, next)
                > RotationWriteEpsilonDegrees;
        }

        private static bool HasMeaningfulEulerChange(
            Vector3 currentEuler,
            Vector3 nextEuler)
        {
            return HasMeaningfulRotationChange(
                Quaternion.Euler(currentEuler),
                Quaternion.Euler(nextEuler));
        }

        private static bool IsSameMarker(FpgRoomMarkerHandle left, FpgRoomMarkerHandle right)
        {
            return left != null && right != null && left.Kind == right.Kind && left.Index == right.Index;
        }

        private static void SetString(SerializedProperty marker, string value, params string[] names)
        {
            SerializedProperty property = FpgRoomAuthoringSchema.FindRelative(marker, names);
            if (property != null && property.propertyType == SerializedPropertyType.String)
            {
                property.stringValue = value;
            }
        }

        private static GameObject InstantiatePreview(
            GameObject prefab,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            Vector3 prefabScale = instance.transform.localScale;
            instance.name = prefab.name + " (鎴块棿棰勮)";
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = prefabScale;
            ApplyPreviewFlags(instance);
            foreach (Behaviour behaviour in instance.GetComponentsInChildren<Behaviour>(true))
            {
                if (!(behaviour is Light))
                {
                    behaviour.enabled = false;
                }
            }

            // ProBuilder preview meshes are editor-owned and may be destroyed immediately.
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                AllowGpuDrivenRenderingProperty?.SetValue(renderer, false);
            }

            foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            return instance;
        }

        private static void ApplyPreviewFlags(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = PreviewHideFlags;
            }
        }

        private bool TryRebuildCameraPreview(out string error)
        {
            DestroyCameraPreviewObjects();
            if (previewRoot == null
                || !roomPreviewScene.IsValid()
                || !roomPreviewScene.isLoaded)
            {
                error = "Formal camera preview requires an active room preview.";
                return false;
            }

            if (!(room is FpgRoomDefinition))
            {
                error = "Cover camera preview requires a valid room definition.";
                return false;
            }

            if (!TryResolvePlayableCharacter(
                    cameraPreviewCharacter,
                    out FpgPlayableCharacterSelection selection,
                    out error)
                || !TryResolveCoverShot(
                    cameraPreviewCoverIndex,
                    out FpgResolvedCameraShot shot,
                    out Pose playerPose,
                    out error)
                || !TryGetCoverProfile(
                    cameraPreviewCoverIndex,
                    out FpgCoverCameraProfile profile,
                    out error))
            {
                return false;
            }

            cameraPreviewRoot = new GameObject("__FPG Formal Camera Preview__")
            {
                hideFlags = PreviewHideFlags
            };
            SceneManager.MoveGameObjectToScene(
                cameraPreviewRoot,
                roomPreviewScene);
            if (cameraPreviewRoot.scene != roomPreviewScene)
            {
                error =
                    "Formal camera preview could not enter the active Art Scene.";
                DestroyCameraPreviewObjects();
                return false;
            }

            GameObject playerAnchor = new GameObject("Player Anchor")
            {
                hideFlags = PreviewHideFlags
            };
            ParentPreviewObject(playerAnchor, cameraPreviewRoot.transform);
            cameraPreviewPlayerAnchor = playerAnchor.transform;
            playerAnchor.transform.SetPositionAndRotation(
                playerPose.position,
                playerPose.rotation);

            if (!TryCreatePlayerPreview(
                    selection,
                    playerAnchor.transform,
                    out error))
            {
                DestroyCameraPreviewObjects();
                return false;
            }

            GameObject rig = new GameObject("Camera Rig")
            {
                hideFlags = PreviewHideFlags
            };
            ParentPreviewObject(rig, cameraPreviewRoot.transform);
            cameraPreviewRig = rig.transform;

            GameObject cameraObject = new GameObject("Formal Preview Camera")
            {
                hideFlags = PreviewHideFlags,
                tag = "Untagged"
            };
            ParentPreviewObject(cameraObject, rig.transform);
            cameraPreviewCamera = cameraObject.AddComponent<Camera>();

            ConfigureFormalPreviewCamera(cameraPreviewCamera);

            if (!FpgFormalCameraPoseUtility.TryApplyShot(
                    shot,
                    cameraPreviewRig,
                    cameraPreviewCamera,
                    out error))
            {
                DestroyCameraPreviewObjects();
                return false;
            }

            ApplyPreviewFlags(cameraPreviewRoot);
            cameraPreviewProfile = profile;
            cameraPreviewThreeC = selection.ThreeCProfile;
            cameraPreviewProfileDirtyCount = EditorUtility.GetDirtyCount(profile);
            cameraPreviewRoomDirtyCount = EditorUtility.GetDirtyCount(room);
            cameraTransitionActive = false;
            cameraPreviewCamera.enabled = true;
            EditorApplication.QueuePlayerLoopUpdate();
            error = string.Empty;
            return true;
        }

        internal bool TrySelectAdjacentCover(int offset, out string error)
        {
            error = string.Empty;
            if (!(room is FpgRoomDefinition))
            {
                error = "Select a room before navigating cover cameras.";
                return false;
            }

            SerializedObject roomData = new SerializedObject(room);
            SerializedProperty covers = roomData.FindProperty("coverSlots");
            if (covers == null || !covers.isArray || covers.arraySize == 0)
            {
                error = "The selected room has no covers.";
                return false;
            }

            int current = cameraPreviewCoverIndex >= 0
                ? cameraPreviewCoverIndex
                : FindStartingCoverIndex(covers);
            int target = (current + offset) % covers.arraySize;
            if (target < 0)
            {
                target += covers.arraySize;
            }

            FpgRoomMarkerHandle handle = FpgRoomAuthoringSchema.GetMarkers(room)
                .Find(candidate => candidate.Kind == FpgRoomMarkerKind.Cover
                    && candidate.Index == target);
            if (handle == null)
            {
                error = $"Could not resolve cover {target}.";
                return false;
            }

            SetSelectedMarker(handle);
            SceneView.RepaintAll();
            return true;
        }

        internal bool TryPreviewCoverTransition(out string error)
        {
            error = string.Empty;
            if (!cameraPreviewActive || cameraPreviewCamera == null
                || cameraPreviewRig == null)
            {
                error = "Enable cover camera preview before previewing a transition.";
                return false;
            }

            int sourceIndex = previousCameraPreviewCoverIndex;
            int targetIndex = cameraPreviewCoverIndex;
            if (sourceIndex < 0 || sourceIndex == targetIndex)
            {
                SerializedObject roomData = new SerializedObject(room);
                SerializedProperty covers = roomData.FindProperty("coverSlots");
                if (covers == null || covers.arraySize < 2)
                {
                    error = "Transition preview requires at least two covers.";
                    return false;
                }

                sourceIndex = targetIndex == 0 ? 1 : targetIndex - 1;
            }

            if (!TryResolveCoverShot(sourceIndex, out cameraTransitionSource,
                    out cameraTransitionSourcePlayerPose, out error)
                || !TryResolveCoverShot(targetIndex, out cameraTransitionTarget,
                    out cameraTransitionTargetPlayerPose, out error))
            {
                return false;
            }

            cameraTransitionDuration = cameraPreviewThreeC == null
                ? 0.25f
                : cameraPreviewThreeC.CoverTraversalSeconds;
            cameraTransitionDuration = Mathf.Max(0.0001f, cameraTransitionDuration);
            cameraTransitionStartedAt = EditorApplication.timeSinceStartup;
            cameraTransitionActive = true;
            if (!FpgFormalCameraPoseUtility.TryApplyShot(
                    cameraTransitionSource,
                    cameraPreviewRig,
                    cameraPreviewCamera,
                    out error))
            {
                cameraTransitionActive = false;
                return false;
            }

            cameraPreviewPlayerAnchor?.SetPositionAndRotation(
                cameraTransitionSourcePlayerPose.position,
                cameraTransitionSourcePlayerPose.rotation);

            EditorApplication.QueuePlayerLoopUpdate();
            return true;
        }

        internal bool TryCaptureSceneViewCamera(out string error)
        {
            if (!TryGetActiveCoverProfile(out FpgCoverCameraProfile profile,
                    out Pose playerPose, out error))
            {
                return false;
            }

            Camera sceneCamera = SceneView.lastActiveSceneView?.camera;
            if (sceneCamera == null)
            {
                error = "A Scene View camera is required.";
                return false;
            }

            if (sceneCamera.orthographic)
            {
                error = "Switch Scene View to Perspective before capturing a cover camera.";
                return false;
            }

            Undo.RecordObject(profile, "Capture Cover Camera From Scene View");
            SerializedObject data = new SerializedObject(profile);
            Quaternion inversePlayerRotation = Quaternion.Inverse(
                playerPose.rotation);
            data.FindProperty("cameraRigLocalPosition").vector3Value =
                inversePlayerRotation
                * (sceneCamera.transform.position - playerPose.position);
            data.FindProperty("cameraRigLocalEulerAngles").vector3Value =
                (inversePlayerRotation * sceneCamera.transform.rotation)
                    .eulerAngles;
            data.FindProperty("cameraLocalPosition").vector3Value = Vector3.zero;
            data.FindProperty("cameraLocalEulerAngles").vector3Value = Vector3.zero;
            data.FindProperty("fieldOfView").floatValue = sceneCamera.fieldOfView;
            data.FindProperty("nearClipPlane").floatValue =
                Mathf.Max(0.001f, sceneCamera.nearClipPlane);
            data.FindProperty("farClipPlane").floatValue = Mathf.Max(
                sceneCamera.farClipPlane,
                sceneCamera.nearClipPlane + 0.001f);
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            return TryApplyCurrentCameraShot(out error);
        }

        internal bool TryRestoreCameraTemplate(out string error)
        {
            if (!TryGetActiveCoverProfile(out FpgCoverCameraProfile profile,
                    out _, out error))
            {
                return false;
            }

            if (cameraTemplate == null)
            {
                error = "Select a camera template before restoring it.";
                return false;
            }

            if (!FpgCoverCameraProfileAuthoring.TryCopyValues(
                    cameraTemplate,
                    profile,
                    "Restore Cover Camera Template",
                    out error))
            {
                return false;
            }

            return TryApplyCurrentCameraShot(out error);
        }

        private bool TryCreatePlayerPreview(
            FpgPlayableCharacterSelection selection,
            Transform playerAnchor,
            out string error)
        {
            if (!selection.TryValidate(out error))
            {
                return false;
            }

            FpgPlayerEntityView entityPrefab =
                selection.CharacterDefinition.EntityPrefab;

            cameraPreviewPlayer = InstantiatePreview(
                entityPrefab.gameObject,
                playerAnchor,
                Vector3.zero,
                Quaternion.identity);
            cameraPreviewPlayer.name = entityPrefab.name + " (姝ｅ紡闀滃ご棰勮)";

            foreach (Collider collider in
                     cameraPreviewPlayer.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Collider2D collider in
                     cameraPreviewPlayer.GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
            }

            FpgPlayerEntityView playerView =
                cameraPreviewPlayer.GetComponent<FpgPlayerEntityView>();
            if (playerView == null || !playerView.TryValidate(out error))
            {
                return false;
            }

            FpgPlayerBarrierPresentationController peek = playerView?.Barrier;
            if (peek == null
                || !peek.TrySetThreeCProfile(selection.ThreeCProfile, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal camera preview player requires a peek presentation controller.";
                }

                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolvePlayableCharacter(
            D0CharacterDefinition character,
            out FpgPlayableCharacterSelection selection,
            out string error)
        {
            selection = default;
            if (character == null)
            {
                error = "Select a playable character for camera preview.";
                return false;
            }

            bool found = false;
            string[] catalogGuids = AssetDatabase.FindAssets(
                "t:FpgPlayableCharacterCatalog");
            for (int catalogIndex = 0;
                 catalogIndex < catalogGuids.Length;
                 catalogIndex++)
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(
                    catalogGuids[catalogIndex]);
                FpgPlayableCharacterCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                        catalogPath);
                if (catalog == null)
                {
                    continue;
                }

                IReadOnlyList<FpgPlayableCharacterCatalogEntry> entries = catalog.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    FpgPlayableCharacterCatalogEntry entry = entries[entryIndex];
                    if (entry == null || entry.Character != character)
                    {
                        continue;
                    }

                    if (!entry.TryCreateSelection(
                            out FpgPlayableCharacterSelection candidate,
                            out error))
                    {
                        return false;
                    }

                    if (found
                        && (selection.CharacterDefinition
                                != candidate.CharacterDefinition
                            || selection.ThreeCProfile
                                != candidate.ThreeCProfile))
                    {
                        error =
                            $"Playable character '{character.name}' resolves to multiple catalog configurations.";
                        return false;
                    }

                    selection = candidate;
                    found = true;
                }
            }

            if (!found)
            {
                error =
                    $"Character '{character.name}' is not present in a playable character catalog.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryChooseInitialCameraCover(out string error)
        {
            SerializedObject roomData = new SerializedObject(room);
            SerializedProperty covers = roomData.FindProperty("coverSlots");
            if (covers == null || !covers.isArray || covers.arraySize == 0)
            {
                error = "Cover camera preview requires at least one cover.";
                return false;
            }

            int startingIndex = FindStartingCoverIndex(covers);
            if (startingIndex < 0)
            {
                error = "Cover camera preview requires exactly one starting cover.";
                return false;
            }

            previousCameraPreviewCoverIndex = -1;
            cameraPreviewCoverIndex = startingIndex;
            FpgRoomMarkerHandle handle = FpgRoomAuthoringSchema.GetMarkers(room)
                .Find(candidate => candidate.Kind == FpgRoomMarkerKind.Cover
                    && candidate.Index == startingIndex);
            if (handle != null)
            {
                SetSelectedMarker(handle);
            }

            error = string.Empty;
            return true;
        }

        private static int FindStartingCoverIndex(SerializedProperty covers)
        {
            if (covers == null || !covers.isArray)
            {
                return -1;
            }

            int found = -1;
            for (int index = 0; index < covers.arraySize; index++)
            {
                SerializedProperty starting = covers.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("isStartingCover");
                if (starting?.boolValue != true)
                {
                    continue;
                }

                if (found >= 0)
                {
                    return -1;
                }

                found = index;
            }

            return found;
        }

        private bool TryResolveCoverShot(
            int coverIndex,
            out FpgResolvedCameraShot shot,
            out Pose playerPose,
            out string error)
        {
            shot = default;
            if (!TryGetCoverData(
                    coverIndex,
                    out FpgCoverCameraProfile profile,
                    out playerPose,
                    out error))
            {
                return false;
            }

            return FpgFormalCameraPoseUtility.TryResolveShot(
                playerPose,
                profile,
                out shot,
                out error);
        }

        private bool TryGetCoverProfile(
            int coverIndex,
            out FpgCoverCameraProfile profile,
            out string error)
        {
            return TryGetCoverData(
                coverIndex,
                out profile,
                out _,
                out error);
        }

        private bool TryGetActiveCoverProfile(
            out FpgCoverCameraProfile profile,
            out Pose playerPose,
            out string error)
        {
            return TryGetCoverData(
                cameraPreviewCoverIndex,
                out profile,
                out playerPose,
                out error);
        }

        private bool TryGetCoverData(
            int coverIndex,
            out FpgCoverCameraProfile profile,
            out Pose playerPose,
            out string error)
        {
            profile = null;
            playerPose = default;
            if (room == null)
            {
                error = "A room is required to resolve a cover camera.";
                return false;
            }

            SerializedObject roomData = new SerializedObject(room);
            SerializedProperty covers = roomData.FindProperty("coverSlots");
            if (covers == null || !covers.isArray
                || coverIndex < 0 || coverIndex >= covers.arraySize)
            {
                error = $"Cover index {coverIndex} is unavailable.";
                return false;
            }

            SerializedProperty cover = covers.GetArrayElementAtIndex(coverIndex);
            profile = cover.FindPropertyRelative("cameraProfile")
                ?.objectReferenceValue as FpgCoverCameraProfile;
            if (profile == null)
            {
                error = $"Cover {coverIndex + 1} has no camera profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                error = $"Cover {coverIndex + 1} camera profile is invalid: {error}";
                return false;
            }

            SerializedProperty position = cover.FindPropertyRelative(
                "playerReachableLocalPosition");
            SerializedProperty rotation = cover.FindPropertyRelative(
                "playerReachableLocalEulerAngles");
            if (position == null || rotation == null)
            {
                error = $"Cover {coverIndex + 1} has no player arrival pose.";
                return false;
            }

            playerPose = new Pose(
                position.vector3Value,
                Quaternion.Euler(rotation.vector3Value));
            error = string.Empty;
            return true;
        }

        private bool TryApplyCurrentCameraShot(out string error)
        {
            if (cameraPreviewRig == null || cameraPreviewCamera == null)
            {
                error = "Cover camera preview objects are unavailable.";
                return false;
            }

            if (!TryResolveCoverShot(
                    cameraPreviewCoverIndex,
                    out FpgResolvedCameraShot shot,
                    out Pose playerPose,
                    out error)
                || !TryGetCoverProfile(
                    cameraPreviewCoverIndex,
                    out cameraPreviewProfile,
                    out error))
            {
                return false;
            }

            cameraTransitionActive = false;
            cameraPreviewProfileDirtyCount =
                EditorUtility.GetDirtyCount(cameraPreviewProfile);
            cameraPreviewRoomDirtyCount = EditorUtility.GetDirtyCount(room);
            cameraPreviewPlayerAnchor?.SetPositionAndRotation(
                playerPose.position,
                playerPose.rotation);
            bool applied = FpgFormalCameraPoseUtility.TryApplyShot(
                shot,
                cameraPreviewRig,
                cameraPreviewCamera,
                out error);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            return applied;
        }

        private void OnEditorUpdate()
        {
            if (disposed || !cameraPreviewActive
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (cameraTransitionActive)
            {
                float linear = (float)((EditorApplication.timeSinceStartup
                    - cameraTransitionStartedAt) / cameraTransitionDuration);
                float progress = Mathf.Clamp01(linear);
                float smooth = progress * progress * (3f - 2f * progress);
                FpgResolvedCameraShot shot = FpgFormalCameraPoseUtility.Interpolate(
                    cameraTransitionSource,
                    cameraTransitionTarget,
                    smooth);
                cameraPreviewPlayerAnchor?.SetPositionAndRotation(
                    Vector3.LerpUnclamped(
                        cameraTransitionSourcePlayerPose.position,
                        cameraTransitionTargetPlayerPose.position,
                        smooth),
                    Quaternion.SlerpUnclamped(
                        cameraTransitionSourcePlayerPose.rotation,
                        cameraTransitionTargetPlayerPose.rotation,
                        smooth));
                if (!FpgFormalCameraPoseUtility.TryApplyShot(
                        shot,
                        cameraPreviewRig,
                        cameraPreviewCamera,
                        out string transitionError))
                {
                    cameraTransitionActive = false;
                    Debug.LogWarning(transitionError, room);
                }
                else if (progress >= 1f)
                {
                    cameraTransitionActive = false;
                }

                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            if (!TryGetCoverProfile(
                    cameraPreviewCoverIndex,
                    out FpgCoverCameraProfile currentProfile,
                    out _))
            {
                return;
            }

            int profileDirtyCount = EditorUtility.GetDirtyCount(currentProfile);
            int roomDirtyCount = EditorUtility.GetDirtyCount(room);
            if (currentProfile != cameraPreviewProfile
                || profileDirtyCount != cameraPreviewProfileDirtyCount
                || roomDirtyCount != cameraPreviewRoomDirtyCount)
            {
                QueueCameraPreviewRefresh();
            }
        }

        private void DrawCameraComposition()
        {
            if (!cameraPreviewActive || cameraPreviewCamera == null
                || cameraPreviewRig == null || cameraPreviewProfile == null)
            {
                return;
            }

            DrawCameraFrustum(cameraPreviewCamera);
            if (TryGetActiveCoverProfile(
                    out FpgCoverCameraProfile profile,
                    out Pose playerPose,
                    out _))
            {
                DrawViewportGuides(cameraPreviewCamera, profile, playerPose);
                if (!cameraTransitionActive)
                {
                    DrawCameraProfileHandle(profile, playerPose);
                }
            }
        }

        private static void DrawCameraFrustum(Camera camera)
        {
            Vector3[] near = new Vector3[4];
            Vector3[] far = new Vector3[4];
            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            camera.CalculateFrustumCorners(
                viewport,
                camera.nearClipPlane,
                Camera.MonoOrStereoscopicEye.Mono,
                near);
            camera.CalculateFrustumCorners(
                viewport,
                camera.farClipPlane,
                Camera.MonoOrStereoscopicEye.Mono,
                far);
            for (int index = 0; index < 4; index++)
            {
                near[index] = camera.transform.TransformPoint(near[index]);
                far[index] = camera.transform.TransformPoint(far[index]);
            }

            Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            for (int index = 0; index < 4; index++)
            {
                int next = (index + 1) % 4;
                Handles.DrawLine(near[index], near[next]);
                Handles.DrawLine(far[index], far[next]);
                Handles.DrawDottedLine(near[index], far[index], 5f);
            }
        }

        private static void DrawViewportGuides(
            Camera camera,
            FpgCoverCameraProfile profile,
            Pose playerPose)
        {
            float playerDepth = Vector3.Dot(
                playerPose.position - camera.transform.position,
                camera.transform.forward);
            float guideDepth = Mathf.Clamp(
                playerDepth,
                camera.nearClipPlane + 0.01f,
                camera.farClipPlane - 0.01f);
            Vector2 focus = profile.FocusViewportAnchor;
            Vector3 horizontalStart = camera.ViewportToWorldPoint(
                new Vector3(0f, focus.y, guideDepth));
            Vector3 horizontalEnd = camera.ViewportToWorldPoint(
                new Vector3(1f, focus.y, guideDepth));
            Vector3 verticalStart = camera.ViewportToWorldPoint(
                new Vector3(focus.x, 0f, guideDepth));
            Vector3 verticalEnd = camera.ViewportToWorldPoint(
                new Vector3(focus.x, 1f, guideDepth));
            Handles.color = new Color(1f, 0.72f, 0.18f, 0.85f);
            Handles.DrawDottedLine(horizontalStart, horizontalEnd, 4f);
            Handles.DrawDottedLine(verticalStart, verticalEnd, 4f);

            Vector3 playerViewport = camera.WorldToViewportPoint(
                playerPose.position);
            Handles.color = new Color(0.3f, 1f, 0.55f, 0.95f);
            float size = HandleUtility.GetHandleSize(playerPose.position) * 0.07f;
            Handles.DrawSolidDisc(
                playerPose.position,
                camera.transform.forward,
                size);
            Handles.Label(
                playerPose.position + Vector3.up * size * 1.5f,
                $"Viewport {playerViewport.x:F3}, {playerViewport.y:F3} | Target {profile.PlayerViewportAnchor.x:F3}, {profile.PlayerViewportAnchor.y:F3}");
        }

        private void DrawCameraProfileHandle(
            FpgCoverCameraProfile profile,
            Pose playerPose)
        {
            if (!TryResolveCoverShot(
                    cameraPreviewCoverIndex,
                    out FpgResolvedCameraShot shot,
                    out _,
                    out _))
            {
                return;
            }

            bool editCameraChild = Event.current.shift;
            Pose pose;
            if (editCameraChild)
            {
                pose = new Pose(
                    shot.RigWorldPose.position
                        + shot.RigWorldPose.rotation
                            * shot.CameraLocalPose.position,
                    shot.RigWorldPose.rotation
                        * shot.CameraLocalPose.rotation);
                Handles.color = new Color(1f, 0.62f, 0.2f, 1f);
            }
            else
            {
                pose = shot.RigWorldPose;
                Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
            }

            Vector3 position = pose.position;
            Quaternion rotation = pose.rotation;
            EditorGUI.BeginChangeCheck();
            if (Tools.current == Tool.Rotate)
            {
                rotation = Handles.RotationHandle(rotation, position);
            }
            else if (Tools.current == Tool.Move)
            {
                position = Handles.PositionHandle(position, rotation);
            }

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            SerializedObject data = new SerializedObject(profile);
            if (editCameraChild)
            {
                Quaternion inverseRig = Quaternion.Inverse(
                    shot.RigWorldPose.rotation);
                SerializedProperty localPosition =
                    data.FindProperty("cameraLocalPosition");
                SerializedProperty localEuler =
                    data.FindProperty("cameraLocalEulerAngles");
                Vector3 nextLocalPosition =
                    inverseRig * (position - shot.RigWorldPose.position);
                Vector3 nextLocalEuler =
                    (inverseRig * rotation).eulerAngles;
                if (!HasMeaningfulPositionChange(
                        localPosition.vector3Value,
                        nextLocalPosition)
                    && !HasMeaningfulEulerChange(
                        localEuler.vector3Value,
                        nextLocalEuler))
                {
                    return;
                }

                Undo.RecordObject(profile, "Edit Cover Camera Local Pose");
                localPosition.vector3Value = nextLocalPosition;
                localEuler.vector3Value = nextLocalEuler;
            }
            else
            {
                Quaternion inversePlayer = Quaternion.Inverse(
                    playerPose.rotation);
                SerializedProperty rigPosition =
                    data.FindProperty("cameraRigLocalPosition");
                SerializedProperty rigEuler =
                    data.FindProperty("cameraRigLocalEulerAngles");
                Vector3 nextRigPosition =
                    inversePlayer * (position - playerPose.position);
                Vector3 nextRigEuler =
                    (inversePlayer * rotation).eulerAngles;
                if (!HasMeaningfulPositionChange(
                        rigPosition.vector3Value,
                        nextRigPosition)
                    && !HasMeaningfulEulerChange(
                        rigEuler.vector3Value,
                        nextRigEuler))
                {
                    return;
                }

                Undo.RecordObject(profile, "Edit Cover Camera Rig Pose");
                rigPosition.vector3Value = nextRigPosition;
                rigEuler.vector3Value = nextRigEuler;
            }

            if (!data.ApplyModifiedProperties())
            {
                return;
            }

            EditorUtility.SetDirty(profile);
            TryApplyCurrentCameraShot(out _);
        }

        private static void ConfigureFormalPreviewCamera(Camera target)
        {
            target.enabled = false;
            target.clearFlags = CameraClearFlags.Skybox;
            target.backgroundColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            target.cullingMask = ~0;
            target.overrideSceneCullingMask = ulong.MaxValue;
            target.orthographic = false;
            target.depth = 100f;
            target.allowHDR = true;
            target.allowMSAA = true;
            target.allowDynamicResolution = false;
            target.useOcclusionCulling = true;
            target.targetTexture = null;
            target.targetDisplay = 0;
            target.aspect = 16f / 9f;

            Type additionalCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, "
                + "Unity.RenderPipelines.Universal.Runtime",
                false);
            if (additionalCameraDataType != null
                && typeof(Component).IsAssignableFrom(additionalCameraDataType))
            {
                Component additionalData =
                    target.gameObject.AddComponent(additionalCameraDataType);
                SerializedObject data = new SerializedObject(additionalData);
                SerializedProperty postProcessing =
                    data.FindProperty("m_RenderPostProcessing");
                SerializedProperty volumeLayerMask =
                    data.FindProperty("m_VolumeLayerMask");
                if (postProcessing != null)
                {
                    postProcessing.boolValue = true;
                }

                if (volumeLayerMask != null)
                {
                    volumeLayerMask.intValue = 1 << 0;
                }

                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private void StopCameraPreviewInternal(string message, bool notify)
        {
            bool wasActive = cameraPreviewActive || cameraPreviewRoot != null;
            cameraPreviewActive = false;
            cameraPreviewProfile = null;
            cameraPreviewCharacter = null;
            cameraPreviewThreeC = null;
            cameraPreviewCoverIndex = -1;
            previousCameraPreviewCoverIndex = -1;
            cameraTransitionActive = false;
            DestroyCameraPreviewObjects();
            if (notify && wasActive)
            {
                CameraPreviewStateChanged?.Invoke(false, message);
            }
        }

        private void DestroyCameraPreviewObjects()
        {
            ClearSelectionIfInsidePreview(cameraPreviewRoot);
            cameraPreviewPlayer = null;
            cameraPreviewPlayerAnchor = null;
            cameraPreviewRig = null;
            cameraPreviewCamera = null;
            cameraTransitionActive = false;
            if (cameraPreviewRoot != null)
            {
                cameraPreviewRoot.SetActive(false);
                UnityEngine.Object.DestroyImmediate(cameraPreviewRoot);
                cameraPreviewRoot = null;
            }
        }

        private void DestroyPreview()
        {
            DestroyCameraPreviewObjects();
            environmentPreview = null;
            if (previewRoot != null)
            {
                ClearSelectionIfInsidePreview(previewRoot);
                previewRoot.SetActive(false);
                UnityEngine.Object.DestroyImmediate(previewRoot);
                previewRoot = null;
            }

            roomArtRoot = null;
            roomPreviewScene = default;

        }

        private void ClearSelectionIfInsidePreview(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform rootTransform = root.transform;
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected == null)
                {
                    continue;
                }

                Transform selectedTransform = selected.transform;
                if (selectedTransform == rootTransform
                    || selectedTransform.IsChildOf(rootTransform))
                {
                    Selection.objects = room == null
                        ? new UnityEngine.Object[0]
                        : new UnityEngine.Object[] { room };
                    return;
                }
            }
        }

        private static void ParentPreviewObject(
            GameObject child,
            Transform parent)
        {
            Scene targetScene = parent.gameObject.scene;
            if (targetScene.IsValid() && targetScene.isLoaded
                && child.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(child, targetScene);
            }

            child.transform.SetParent(parent, false);
        }

    }
}
