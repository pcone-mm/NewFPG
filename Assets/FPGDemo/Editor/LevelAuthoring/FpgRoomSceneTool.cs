using System;
using System.Collections.Generic;
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
        private static readonly System.Reflection.PropertyInfo AllowGpuDrivenRenderingProperty =
            typeof(Renderer).GetProperty(
                "allowGPUDrivenRendering",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

        private readonly Dictionary<FpgRoomMarkerKind, bool> visibility =
            new Dictionary<FpgRoomMarkerKind, bool>();

        private ScriptableObject room;
        private GameObject previewRoot;
        private GameObject environmentPreview;
        private FpgRoomMarkerKind? placementKind;
        private FpgRoomMarkerHandle selectedMarker;
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
        }

        internal event Action<FpgRoomMarkerHandle> SelectionChanged;
        internal event Action RoomChanged;

        internal ScriptableObject Room => room;
        internal FpgRoomMarkerHandle SelectedMarker => selectedMarker;
        internal FpgRoomMarkerKind? PlacementKind => placementKind;
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
            DestroyPreview();
            if (room == null)
            {
                return;
            }

            previewRoot = new GameObject("__FPG Room Authoring Preview__")
            {
                hideFlags = PreviewHideFlags
            };
            previewRoot.SetActive(false);

            SerializedObject serializedRoom = new SerializedObject(room);
            GameObject environmentPrefab = serializedRoom.FindProperty("environmentPrefab")?.objectReferenceValue as GameObject;
            if (environmentPrefab != null)
            {
                environmentPreview = InstantiatePreview(environmentPrefab, previewRoot.transform, Vector3.zero, Quaternion.identity);
            }

            SerializedProperty destructibles = serializedRoom.FindProperty("destructibleSlots");
            if (destructibles != null && destructibles.isArray)
            {
                for (int index = 0; index < destructibles.arraySize; index++)
                {
                    SerializedProperty slot = destructibles.GetArrayElementAtIndex(index);
                    GameObject prefab = FpgRoomAuthoringSchema.FindRelative(slot, "prefab", "destructiblePrefab")
                        ?.objectReferenceValue as GameObject;
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
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (room == null
                || EditorApplication.isPlayingOrWillChangePlaymode
                || PrefabStageUtility.GetCurrentPrefabStage() != null)
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

            RoomChanged?.Invoke();
        }

        private bool TryRaycastEnvironment(Ray ray, out RaycastHit closest)
        {
            closest = default;
            if (environmentPreview == null)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, float.MaxValue, ~0, QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (!hit.transform.IsChildOf(environmentPreview.transform) && hit.transform != environmentPreview.transform)
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
            if (previewRoot == null)
            {
                return false;
            }

            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = previewRoot.GetComponentsInChildren<Collider>(true);
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
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

            foreach (Collider collider in colliders)
            {
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

            return found;
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

        private void DestroyPreview()
        {
            environmentPreview = null;
            if (previewRoot != null)
            {
                previewRoot.SetActive(false);
                UnityEngine.Object.DestroyImmediate(previewRoot);
                previewRoot = null;
            }
        }
    }
}
