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
        private const string FormalPlayerEntryMarkerId = "player-main";
        private const HideFlags PreviewHideFlags =
            HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.NotEditable;
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
        private FpgPlayerBarrierPresentationController cameraPreviewCover;
        private Camera cameraPreviewCamera;
        private D0ThreeCProfile cameraPreviewProfile;
        private bool artPresentationBound;
        private FpgRoomMarkerKind? placementKind;
        private FpgRoomMarkerHandle selectedMarker;
        private bool cameraPreviewActive;
        private bool disposed;
        private bool previewRefreshQueued;

        internal FpgRoomSceneTool()
        {
            foreach (FpgRoomMarkerKind kind in Enum.GetValues(typeof(FpgRoomMarkerKind)))
            {
                visibility[kind] = true;
            }

            SceneView.duringSceneGui += OnSceneGUI;
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
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            cameraPreviewActive = false;
            cameraPreviewProfile = null;
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
            RebuildPreview();
            SceneView.RepaintAll();
        }

        internal bool TryStartCameraPreview(
            D0ThreeCProfile profile,
            out string error)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "Formal camera preview is only available in Edit Mode.";
                return false;
            }

            cameraPreviewProfile = profile;
            cameraPreviewActive = false;
            if (previewRoot == null)
            {
                RebuildPreview();
            }

            cameraPreviewActive = true;
            if (!TryRebuildCameraPreview(out error))
            {
                StopCameraPreviewInternal(error, true);
                return false;
            }

            CameraPreviewStateChanged?.Invoke(true, "Formal camera preview is active.");
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

            if (!TryRebuildCameraPreview(out error))
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

            Undo.RecordObject(room, "放置房间标记");
            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty array = serializedRoom.FindProperty(FpgRoomAuthoringSchema.MarkerArrayName(kind));
            if (array == null || !array.isArray)
            {
                Debug.LogError($"房间资产缺少标记数组 '{FpgRoomAuthoringSchema.MarkerArrayName(kind)}'。", room);
                return;
            }

            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            SerializedProperty marker = array.GetArrayElementAtIndex(index);
            string markerId = FpgRoomAuthoringSchema.CreateSemanticMarkerId(room, kind);
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
            else if (kind == FpgRoomMarkerKind.Reachable)
            {
                SerializedProperty audience = FpgRoomAuthoringSchema.FindRelative(marker, "audience", "actorMask");
                if (audience != null)
                {
                    audience.intValue = 3;
                }
            }

            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            SetSelectedMarker(new FpgRoomMarkerHandle(kind, index, markerId, string.Empty));
            RefreshMarkerVisualization(kind);
            if (kind == FpgRoomMarkerKind.PlayerEntry)
            {
                TryRefreshCameraPreview(out _);
            }
            RoomChanged?.Invoke();
        }

        internal void DuplicateSelectedMarker()
        {
            if (room == null || selectedMarker == null)
            {
                return;
            }

            Undo.RecordObject(room, "复制房间标记");
            SerializedObject serializedRoom = new SerializedObject(room);
            SerializedProperty array = serializedRoom.FindProperty(
                FpgRoomAuthoringSchema.MarkerArrayName(selectedMarker.Kind));
            if (array == null || !array.isArray || selectedMarker.Index < 0 || selectedMarker.Index >= array.arraySize)
            {
                return;
            }

            FpgRoomMarkerKind markerKind = selectedMarker.Kind;
            int duplicateIndex = selectedMarker.Index;
            array.InsertArrayElementAtIndex(duplicateIndex);
            SerializedProperty duplicate = array.GetArrayElementAtIndex(duplicateIndex);
            string id = FpgRoomAuthoringSchema.CreateSemanticMarkerId(room, markerKind);
            SetString(duplicate, id, "markerId", "id");
            float duplicateOffset = float.IsNaN(GridSnap) || float.IsInfinity(GridSnap) || GridSnap <= 0f
                ? 0.5f
                : GridSnap;
            Vector3 offset = new Vector3(duplicateOffset, 0f, 0f);
            FpgRoomAuthoringSchema.SetMarkerPosition(
                duplicate,
                Snap(FpgRoomAuthoringSchema.GetMarkerPosition(duplicate) + offset));
            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            SetSelectedMarker(new FpgRoomMarkerHandle(markerKind, duplicateIndex, id, string.Empty));
            RefreshMarkerVisualization(markerKind);
            if (markerKind == FpgRoomMarkerKind.PlayerEntry)
            {
                TryRefreshCameraPreview(out _);
            }
            RoomChanged?.Invoke();
        }

        internal void DeleteSelectedMarker()
        {
            if (room == null || selectedMarker == null)
            {
                return;
            }

            Undo.RecordObject(room, "删除房间标记");
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
                TryRefreshCameraPreview(out _);
            }
            RoomChanged?.Invoke();
        }

        private void RefreshMarkerVisualization(FpgRoomMarkerKind kind)
        {
            if (kind == FpgRoomMarkerKind.Destructible)
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
            }
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

            Undo.RecordObject(room, Tools.current == Tool.Rotate ? "旋转房间标记" : "移动房间标记");
            serializedRoom.Update();
            marker = FpgRoomAuthoringSchema.FindMarkerProperty(serializedRoom, selectedMarker.Kind, selectedMarker.Index);
            if (Tools.current == Tool.Rotate)
            {
                FpgRoomAuthoringSchema.SetMarkerRotation(marker, rotation);
            }
            else
            {
                FpgRoomAuthoringSchema.SetMarkerPosition(marker, Snap(position));
            }

            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            if (selectedMarker.Kind == FpgRoomMarkerKind.Destructible)
            {
                RebuildPreview();
            }
            else if (selectedMarker.Kind == FpgRoomMarkerKind.PlayerEntry)
            {
                TryRefreshCameraPreview(out _);
            }

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

        private void QueuePreviewRefresh()
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
            instance.name = prefab.name + " (房间预览)";
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

            if (!(room is FpgRoomDefinition definition))
            {
                error = "Formal camera preview requires a valid room definition.";
                return false;
            }

            if (cameraPreviewProfile == null)
            {
                error = "Formal camera preview requires a D0 3C profile.";
                return false;
            }

            if (!cameraPreviewProfile.TryValidate(out error))
            {
                return false;
            }

            if (!definition.TryGetPlayerEntryPoint(
                    FormalPlayerEntryMarkerId,
                    out FpgRoomPlayerEntryPoint playerEntry))
            {
                error = $"Room '{definition.RoomId}' does not contain player entry "
                    + $"'{FormalPlayerEntryMarkerId}'.";
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
            playerAnchor.transform.localPosition = playerEntry.LocalPosition;
            playerAnchor.transform.localRotation = playerEntry.LocalRotation;

            if (!TryCreatePlayerPreview(
                    cameraPreviewProfile,
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

            GameObject cameraObject = new GameObject("Formal Preview Camera")
            {
                hideFlags = PreviewHideFlags,
                tag = "Untagged"
            };
            ParentPreviewObject(cameraObject, rig.transform);
            cameraPreviewCamera = cameraObject.AddComponent<Camera>();

            ConfigureFormalPreviewCamera(cameraPreviewCamera);

            if (!FpgFormalCameraPoseUtility.TryApplyFixedPose(
                    cameraPreviewProfile,
                    playerAnchor.transform,
                    rig.transform,
                    cameraPreviewCamera,
                    out error))
            {
                DestroyCameraPreviewObjects();
                return false;
            }

            FpgRoomArtPresentationContext context =
                new FpgRoomArtPresentationContext(
                    cameraPreviewCamera,
                    roomArtRoot.MainDirectionalLight,
                    null);
            if (!roomArtRoot.TryBindPresentation(context, out error))
            {
                DestroyCameraPreviewObjects();
                return false;
            }

            artPresentationBound = true;
            ApplyPreviewFlags(cameraPreviewRoot);
            cameraPreviewCamera.enabled = true;
            EditorApplication.QueuePlayerLoopUpdate();
            error = string.Empty;
            return true;
        }

        internal bool TryRefreshCoverPreview(
            D0ThreeCProfile profile,
            out string error)
        {
            if (!cameraPreviewActive || cameraPreviewCover == null)
            {
                error = string.Empty;
                return true;
            }

            if (!cameraPreviewCover.TrySetThreeCProfile(profile, out error))
            {
                return false;
            }

            SetCoverPreviewVisible(cameraPreviewCover, profile);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
            error = string.Empty;
            return true;
        }

        private bool TryCreatePlayerPreview(
            D0ThreeCProfile profile,
            Transform playerAnchor,
            out string error)
        {
            if (!TryResolvePlayerEntityPrefab(
                    profile,
                    out FpgPlayerEntityView entityPrefab,
                    out error))
            {
                return false;
            }

            cameraPreviewPlayer = InstantiatePreview(
                entityPrefab.gameObject,
                playerAnchor,
                Vector3.zero,
                Quaternion.identity);
            cameraPreviewPlayer.name = entityPrefab.name + " (正式镜头预览)";

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

            FpgPlayerBarrierPresentationController cover = playerView?.Barrier;
            if (cover == null || !cover.TrySetThreeCProfile(profile, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal camera preview player requires a cover presentation controller.";
                }

                return false;
            }

            cameraPreviewCover = cover;
            SetCoverPreviewVisible(cover, profile);
            error = string.Empty;
            return true;
        }

        private static void SetCoverPreviewVisible(
            FpgPlayerBarrierPresentationController cover,
            D0ThreeCProfile profile)
        {
            cover.CoverRenderer.enabled = true;
            LineRenderer outline = cover.GetComponent<LineRenderer>();
            if (outline != null)
            {
                Color color = profile.BarrierColor;
                color.a *= profile.BarrierMaximumOpacity;
                outline.startColor = color;
                outline.endColor = color;
                outline.enabled = true;
            }
        }

        private static bool TryResolvePlayerEntityPrefab(
            D0ThreeCProfile profile,
            out FpgPlayerEntityView entityPrefab,
            out string error)
        {
            entityPrefab = null;
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

                IReadOnlyList<FpgPlayableCharacterCatalogEntry> entries =
                    catalog.Entries;
                for (int entryIndex = 0;
                     entryIndex < entries.Count;
                     entryIndex++)
                {
                    FpgPlayableCharacterCatalogEntry entry = entries[entryIndex];
                    if (entry == null || entry.ThreeCProfile != profile)
                    {
                        continue;
                    }

                    FpgPlayerEntityView candidate = entry.Character?.EntityPrefab;
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (entityPrefab != null && entityPrefab != candidate)
                    {
                        error =
                            $"D0 3C profile '{profile.name}' resolves to multiple player entity prefabs.";
                        entityPrefab = null;
                        return false;
                    }

                    entityPrefab = candidate;
                }
            }

            if (entityPrefab == null)
            {
                error =
                    $"D0 3C profile '{profile.name}' is not linked to a player entity prefab in a playable character catalog.";
                return false;
            }

            error = string.Empty;
            return true;
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
            DestroyCameraPreviewObjects();
            if (notify && wasActive)
            {
                CameraPreviewStateChanged?.Invoke(false, message);
            }
        }

        private void DestroyCameraPreviewObjects()
        {
            if (artPresentationBound && roomArtRoot != null)
            {
                roomArtRoot.UnbindPresentation();
            }

            artPresentationBound = false;
            cameraPreviewPlayer = null;
            cameraPreviewCover = null;
            cameraPreviewCamera = null;
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
                previewRoot.SetActive(false);
                UnityEngine.Object.DestroyImmediate(previewRoot);
                previewRoot = null;
            }

            roomArtRoot = null;
            roomPreviewScene = default;

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
