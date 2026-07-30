using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public sealed class FpgRoomEditorWindow : EditorWindow
    {
        private const string LayoutPath = "Assets/FPGDemo/Editor/LevelAuthoring/FpgRoomEditor.uxml";
        private const string SelectedRoomSessionKey = "FPGDemo.RoomAuthoring.SelectedRoomGuid";
        private const string CameraTemplateSessionKey =
            "FPGDemo.RoomAuthoring.CameraTemplateGuid";
        private const string PreviewCharacterSessionKey =
            "FPGDemo.RoomAuthoring.PreviewCharacterGuid";

        private readonly List<FpgRoomRecord> allRooms = new List<FpgRoomRecord>();
        private readonly List<FpgRoomRecord> filteredRooms = new List<FpgRoomRecord>();
        private readonly List<FpgRoomMarkerHandle> markers = new List<FpgRoomMarkerHandle>();
        private readonly List<FpgRoomValidationItem> validation = new List<FpgRoomValidationItem>();

        private FpgGameViewAspectSession gameViewAspectSession;
        private FpgCoverCameraProfile selectedCameraTemplate;
        private D0CharacterDefinition selectedPreviewCharacter;
        private FpgRoomSceneTool sceneTool;
        private ScriptableObject selectedRoom;
        private SerializedObject serializedRoom;
        private bool refreshQueued;
        private bool suppressRoomSelectionChanged;

        private FpgEncounterProfile formalPreviewProfile;
        private FpgEncounterOverrideDefinition formalPreviewOverride;
        private long formalPreviewSeed = 1L;
        private int formalPreviewDepth;
        private int formalPreviewDifficultyBasisPoints =
            FpgEncounterRunContext.BasisPointsOne;
        private int formalPreviewRoomVisitOrdinal;

        private ObjectField cameraTemplateField;
        private ObjectField previewCharacterField;
        private Button applyCameraPreviewButton;
        private Button stopCameraPreviewButton;
        private Button previousCameraCoverButton;
        private Button nextCameraCoverButton;
        private Button previewCameraTransitionButton;
        private Button captureSceneViewCameraButton;
        private Button restoreCameraTemplateButton;
        private Label cameraPreviewStateLabel;
        private ToolbarSearchField searchField;
        private DropdownField groupFilter;
        private DropdownField tagFilter;
        private DropdownField statusFilter;
        private ListView roomList;
        private Label roomCountLabel;
        private VisualElement roomDetails;
        private ListView markerList;
        private VisualElement markerDetails;
        private ListView validationList;
        private Label validationSummaryLabel;
        private Label statusLabel;
        private VisualElement formalPreviewOutput;
        private readonly Dictionary<FpgRoomMarkerKind, Button> markerToolButtons =
            new Dictionary<FpgRoomMarkerKind, Button>();

        [MenuItem("FPG Demo/Room Editor", priority = 120)]
        public static void Open()
        {
            FpgRoomEditorWindow window = GetWindow<FpgRoomEditorWindow>();
            window.titleContent = new GUIContent("Room Editor");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            DisposeSceneTool();
            sceneTool = new FpgRoomSceneTool();
            sceneTool.SelectionChanged += OnSceneMarkerSelectionChanged;
            sceneTool.RoomChanged += QueueCurrentRoomRefresh;
            sceneTool.CameraPreviewStateChanged += OnCameraPreviewStateChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.delayCall -= RebuildScenePreviewAfterReload;
            EditorApplication.delayCall += RebuildScenePreviewAfterReload;

        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RebuildScenePreviewAfterReload;

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            RestoreGameViewAspect(true);
            DisposeSceneTool();
        }

        private void OnBeforeAssemblyReload()
        {
            EditorApplication.delayCall -= RebuildScenePreviewAfterReload;

            RestoreGameViewAspect(true);
            DisposeSceneTool();
        }

        private void OnEditorQuitting()
        {
            EditorApplication.delayCall -= RebuildScenePreviewAfterReload;

            RestoreGameViewAspect(true);
            DisposeSceneTool();
        }

        private void RebuildScenePreviewAfterReload()
        {
            if (sceneTool == null ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (selectedRoom != null && sceneTool.Room != selectedRoom)
            {
                sceneTool.SetRoom(selectedRoom);
            }
            else
            {
                sceneTool.RebuildPreview();
            }
        }

        private void DisposeSceneTool()
        {
            if (sceneTool == null)
            {
                return;
            }

            sceneTool.SelectionChanged -= OnSceneMarkerSelectionChanged;
            sceneTool.RoomChanged -= QueueCurrentRoomRefresh;
            sceneTool.CameraPreviewStateChanged -= OnCameraPreviewStateChanged;
            sceneTool.Dispose();
            sceneTool = null;
        }


        private void OnEditorPlayModeStateChanged(PlayModeStateChange state)
        {
            UpdateCameraPreviewControls();
        }

        private void OnUndoRedo()
        {
        }


        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new HelpBox("Room Editor UXML could not be loaded.", HelpBoxMessageType.Error));
                return;
            }

            layout.CloneTree(rootVisualElement);
            QueryElements();
            ConfigureLists();
            RegisterCallbacks();
            RestoreCameraTemplateSelection();
            RestorePreviewCharacterSelection();
            RefreshRoomAssets();
            RestoreRoomSelection();
        }

        private void QueryElements()
        {
            searchField = rootVisualElement.Q<ToolbarSearchField>("search-field");
            groupFilter = rootVisualElement.Q<DropdownField>("group-filter");
            tagFilter = rootVisualElement.Q<DropdownField>("tag-filter");
            statusFilter = rootVisualElement.Q<DropdownField>("status-filter");
            roomList = rootVisualElement.Q<ListView>("room-list");
            roomCountLabel = rootVisualElement.Q<Label>("room-count-label");
            roomDetails = rootVisualElement.Q<VisualElement>("room-details");
            markerList = rootVisualElement.Q<ListView>("marker-list");
            markerDetails = rootVisualElement.Q<VisualElement>("marker-details");
            validationList = rootVisualElement.Q<ListView>("validation-list");
            validationSummaryLabel = rootVisualElement.Q<Label>("validation-summary-label");
            statusLabel = rootVisualElement.Q<Label>("status-label");
            cameraTemplateField = rootVisualElement.Q<ObjectField>("camera-template-field");
            previewCharacterField = rootVisualElement.Q<ObjectField>("preview-character-field");
            applyCameraPreviewButton = rootVisualElement.Q<Button>("apply-camera-preview-button");
            stopCameraPreviewButton = rootVisualElement.Q<Button>("stop-camera-preview-button");
            previousCameraCoverButton = rootVisualElement.Q<Button>("previous-camera-cover-button");
            nextCameraCoverButton = rootVisualElement.Q<Button>("next-camera-cover-button");
            previewCameraTransitionButton = rootVisualElement.Q<Button>("preview-camera-transition-button");
            captureSceneViewCameraButton = rootVisualElement.Q<Button>("capture-scene-view-camera-button");
            restoreCameraTemplateButton = rootVisualElement.Q<Button>("restore-camera-template-button");
            cameraPreviewStateLabel = rootVisualElement.Q<Label>("camera-preview-state-label");

            markerToolButtons.Clear();
            markerToolButtons[FpgRoomMarkerKind.Exit] = rootVisualElement.Q<Button>("place-exit-button");
            markerToolButtons[FpgRoomMarkerKind.PlayerEntry] = rootVisualElement.Q<Button>("place-player-button");
            markerToolButtons[FpgRoomMarkerKind.EnemySpawn] = rootVisualElement.Q<Button>("place-enemy-button");
            markerToolButtons[FpgRoomMarkerKind.Destructible] = rootVisualElement.Q<Button>("place-destructible-button");
            markerToolButtons[FpgRoomMarkerKind.Cover] = rootVisualElement.Q<Button>("place-cover-button");
        }

        private void ConfigureLists()
        {
            cameraTemplateField.objectType = typeof(FpgCoverCameraProfile);
            cameraTemplateField.allowSceneObjects = false;
            previewCharacterField.objectType = typeof(D0CharacterDefinition);
            previewCharacterField.allowSceneObjects = false;
            UpdateCameraPreviewControls();

            statusFilter.choices = new List<string> { "All", "Valid", "Warning", "Error" };
            statusFilter.SetValueWithoutNotify("All");

            roomList.itemsSource = filteredRooms;
            roomList.fixedItemHeight = 40f;
            roomList.makeItem = MakeRoomListItem;
            roomList.bindItem = BindRoomListItem;

            markerList.itemsSource = markers;
            markerList.fixedItemHeight = 31f;
            markerList.makeItem = MakeMarkerListItem;
            markerList.bindItem = BindMarkerListItem;

            validationList.itemsSource = validation;
            validationList.fixedItemHeight = 27f;
            validationList.makeItem = MakeValidationListItem;
            validationList.bindItem = BindValidationListItem;

        }

        private void RegisterCallbacks()
        {
            searchField.RegisterValueChangedCallback(_ => ApplyFilters());
            groupFilter.RegisterValueChangedCallback(_ => ApplyFilters());
            tagFilter.RegisterValueChangedCallback(_ => ApplyFilters());
            statusFilter.RegisterValueChangedCallback(_ => ApplyFilters());
            roomList.selectionChanged += OnRoomSelectionChanged;
            markerList.selectionChanged += OnMarkerSelectionChanged;
            validationList.selectionChanged += OnValidationSelectionChanged;
            cameraTemplateField.RegisterValueChangedCallback(OnCameraTemplateChanged);
            previewCharacterField.RegisterValueChangedCallback(OnPreviewCharacterChanged);
            applyCameraPreviewButton.clicked += ApplyCameraPreview;
            stopCameraPreviewButton.clicked += StopCameraPreview;
            previousCameraCoverButton.clicked += () => NavigateCameraCover(-1);
            nextCameraCoverButton.clicked += () => NavigateCameraCover(1);
            previewCameraTransitionButton.clicked += PreviewCameraTransition;
            captureSceneViewCameraButton.clicked += CaptureSceneViewCamera;
            restoreCameraTemplateButton.clicked += RestoreCurrentCameraTemplate;


            rootVisualElement.Q<Button>("create-room-button").clicked += CreateRoom;
            rootVisualElement.Q<Button>("duplicate-room-button").clicked += DuplicateRoom;
            rootVisualElement.Q<Button>("save-room-button").clicked += SaveRoom;
            rootVisualElement.Q<Button>("frame-room-button").clicked += () => sceneTool?.FrameSelection();
            rootVisualElement.Q<Button>("duplicate-marker-button").clicked += () => sceneTool?.DuplicateSelectedMarker();
            rootVisualElement.Q<Button>("delete-marker-button").clicked += () => sceneTool?.DeleteSelectedMarker();
            rootVisualElement.Q<Button>("audit-camera-profiles-button").clicked += AuditCameraProfiles;

            foreach (KeyValuePair<FpgRoomMarkerKind, Button> pair in markerToolButtons)
            {
                FpgRoomMarkerKind kind = pair.Key;
                pair.Value.clicked += () =>
                {
                    sceneTool?.SetPlacementKind(kind);
                    RefreshMarkerToolStyles();
                };
            }

            BindVisibility("show-exit-toggle", FpgRoomMarkerKind.Exit);
            BindVisibility("show-player-toggle", FpgRoomMarkerKind.PlayerEntry);
            BindVisibility("show-enemy-toggle", FpgRoomMarkerKind.EnemySpawn);
            BindVisibility("show-destructible-toggle", FpgRoomMarkerKind.Destructible);
            BindVisibility("show-cover-toggle", FpgRoomMarkerKind.Cover);

            FloatField snapField = rootVisualElement.Q<FloatField>("snap-field");
            snapField.RegisterValueChangedCallback(evt =>
            {
                float normalizedSnap = float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)
                    ? 0.5f
                    : Mathf.Max(0f, evt.newValue);
                snapField.SetValueWithoutNotify(normalizedSnap);
                if (sceneTool != null)
                {
                    sceneTool.GridSnap = normalizedSnap;
                }
            });

        }

        private void RestoreCameraTemplateSelection()
        {
            string guid = SessionState.GetString(CameraTemplateSessionKey, string.Empty);
            string path = string.IsNullOrEmpty(guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid);
            selectedCameraTemplate = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<FpgCoverCameraProfile>(path);
            if (selectedCameraTemplate == null)
            {
                selectedCameraTemplate =
                    AssetDatabase.LoadAssetAtPath<FpgCoverCameraProfile>(
                        FpgCoverCameraProfileAuthoring.DefaultTemplatePath);
            }
            cameraTemplateField.SetValueWithoutNotify(selectedCameraTemplate);
            sceneTool?.SetCameraTemplate(selectedCameraTemplate);
            UpdateCameraPreviewControls();
        }

        private void RestorePreviewCharacterSelection()
        {
            string guid = SessionState.GetString(
                PreviewCharacterSessionKey,
                string.Empty);
            string path = string.IsNullOrEmpty(guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid);
            selectedPreviewCharacter = string.IsNullOrEmpty(path)
                ? FindDefaultPreviewCharacter()
                : AssetDatabase.LoadAssetAtPath<D0CharacterDefinition>(path);
            if (selectedPreviewCharacter == null)
            {
                selectedPreviewCharacter = FindDefaultPreviewCharacter();
            }

            previewCharacterField.SetValueWithoutNotify(selectedPreviewCharacter);
            UpdateCameraPreviewControls();
        }

        private void OnCameraTemplateChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            selectedCameraTemplate = evt.newValue as FpgCoverCameraProfile;
            string path = selectedCameraTemplate == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(selectedCameraTemplate);
            SessionState.SetString(
                CameraTemplateSessionKey,
                string.IsNullOrEmpty(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path));
            sceneTool?.SetCameraTemplate(selectedCameraTemplate);
            UpdateCameraPreviewControls();
        }

        private void OnPreviewCharacterChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            selectedPreviewCharacter = evt.newValue as D0CharacterDefinition;
            string path = selectedPreviewCharacter == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(selectedPreviewCharacter);
            SessionState.SetString(
                PreviewCharacterSessionKey,
                string.IsNullOrEmpty(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path));
            UpdateCameraPreviewControls();
            if (sceneTool?.IsCameraPreviewActive == true)
            {
                ApplyCameraPreview();
            }
        }

        private void ApplyCameraPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetCameraPreviewStatus(
                    false,
                    "\u6b63\u5f0f\u955c\u5934\u9884\u89c8\u4ec5\u5728\u7f16\u8f91\u6a21\u5f0f\u53ef\u7528\u3002");
                return;
            }

            if (selectedPreviewCharacter == null)
            {
                SetCameraPreviewStatus(
                    false,
                    "Select a playable character for camera preview.");
                return;
            }

            if (selectedRoom == null || sceneTool == null)
            {
                SetCameraPreviewStatus(
                    false,
                    "\u8bf7\u5148\u9009\u62e9\u623f\u95f4\u3002");
                return;
            }

            if (gameViewAspectSession == null)
            {
                if (!FpgGameViewAspectSession.TryBegin16By9(
                        out gameViewAspectSession,
                        out string aspectError))
                {
                    SetCameraPreviewStatus(false, aspectError);
                    return;
                }
            }

            try
            {
                if (!sceneTool.TryStartCameraPreview(
                        selectedPreviewCharacter,
                        out string error))
                {
                    string restoreError = RestoreGameViewAspect(false);
                    if (!string.IsNullOrEmpty(restoreError))
                    {
                        error += " " + restoreError;
                    }

                    SetCameraPreviewStatus(false, error);
                    return;
                }
            }
            catch (Exception exception)
            {
                sceneTool.StopCameraPreview();
                string error = exception.GetBaseException().Message;
                string restoreError = RestoreGameViewAspect(false);
                if (!string.IsNullOrEmpty(restoreError))
                {
                    error += " " + restoreError;
                }

                SetCameraPreviewStatus(false, error);
                return;
            }

            SetCameraPreviewStatus(
                true,
                $"16:9 Game View | {selectedPreviewCharacter.DisplayName}");
        }

        private void StopCameraPreview()
        {
            if (sceneTool?.IsCameraPreviewActive == true)
            {
                sceneTool.StopCameraPreview();
                return;
            }

            string restoreError = RestoreGameViewAspect(false);
            SetCameraPreviewStatus(
                false,
                string.IsNullOrEmpty(restoreError)
                    ? "\u6b63\u5f0f\u955c\u5934\u9884\u89c8\u5df2\u5173\u95ed\u3002"
                    : restoreError);
        }

        private void NavigateCameraCover(int offset)
        {
            if (sceneTool == null)
            {
                UpdateLevelStatus("Room scene tool is unavailable.");
                return;
            }

            if (!sceneTool.TrySelectAdjacentCover(offset, out string error))
            {
                UpdateLevelStatus(error);
                return;
            }

            UpdateLevelStatus("Cover camera preview updated.");
        }

        private void PreviewCameraTransition()
        {
            if (sceneTool == null)
            {
                UpdateLevelStatus("Room scene tool is unavailable.");
                return;
            }

            if (!sceneTool.TryPreviewCoverTransition(out string error))
            {
                UpdateLevelStatus(error);
                return;
            }

            UpdateLevelStatus("Cover camera transition preview started.");
        }

        private void CaptureSceneViewCamera()
        {
            if (sceneTool == null)
            {
                UpdateLevelStatus("Room scene tool is unavailable.");
                return;
            }

            if (!sceneTool.TryCaptureSceneViewCamera(out string error))
            {
                UpdateLevelStatus(error);
                return;
            }

            QueueCurrentRoomRefresh();
            UpdateLevelStatus("Captured the Scene View camera into the selected cover profile.");
        }

        private void RestoreCurrentCameraTemplate()
        {
            if (sceneTool == null)
            {
                UpdateLevelStatus("Room scene tool is unavailable.");
                return;
            }

            if (!sceneTool.TryRestoreCameraTemplate(out string error))
            {
                UpdateLevelStatus(error);
                return;
            }

            QueueCurrentRoomRefresh();
            UpdateLevelStatus("Restored the selected cover profile from the camera template.");
        }

        private void AuditCameraProfiles()
        {
            IReadOnlyList<FpgCoverCameraProfile> orphans =
                FpgCoverCameraProfileAuthoring.FindOrphanProfiles();
            if (orphans.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Camera Profile Audit",
                    "No orphan cover camera profiles were found.",
                    "OK");
                return;
            }

            Selection.objects = orphans.Cast<UnityEngine.Object>().ToArray();
            EditorUtility.DisplayDialog(
                "Camera Profile Audit",
                $"Found {orphans.Count} orphan cover camera profile(s). They are selected in the Project window; no assets were deleted.",
                "OK");
        }

        private static D0CharacterDefinition FindDefaultPreviewCharacter()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:FpgPlayableCharacterCatalog");
            Array.Sort(guids, StringComparer.Ordinal);
            for (int index = 0; index < guids.Length; index++)
            {
                FpgPlayableCharacterCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                        AssetDatabase.GUIDToAssetPath(guids[index]));
                if (catalog != null
                    && catalog.TryResolveDefault(
                        out FpgPlayableCharacterSelection selection,
                        out _))
                {
                    return selection.CharacterDefinition;
                }
            }

            return null;
        }

        private void OnCameraPreviewStateChanged(bool active, string message)
        {
            if (!active)
            {
                string restoreError = RestoreGameViewAspect(false);
                if (!string.IsNullOrEmpty(restoreError))
                {
                    SetCameraPreviewStatus(false, restoreError);
                    return;
                }
            }

            SetCameraPreviewStatus(active, message);
        }

        private void SetCameraPreviewStatus(bool active, string message)
        {
            if (cameraPreviewStateLabel != null)
            {
                cameraPreviewStateLabel.text = active
                    ? "\u9884\u89c8\u4e2d (16:9)"
                    : "\u672a\u542f\u7528";
            }

            if (!string.IsNullOrEmpty(message))
            {
                UpdateLevelStatus(message);
            }

            UpdateCameraPreviewControls();
        }

        private void UpdateLevelStatus(string message = null)
        {
            if (statusLabel == null)
            {
                return;
            }

            if (selectedRoom == null)
            {
                statusLabel.text = string.IsNullOrWhiteSpace(message)
                    ? "未选择房间。"
                    : message;
                return;
            }

            string roomState = EditorUtility.IsDirty(selectedRoom)
                ? "RoomDefinition：未保存"
                : "RoomDefinition：已保存";
            string sceneState;
            FpgRoomDefinition definition =
                selectedRoom as FpgRoomDefinition;
            string referenceError = string.Empty;
            if (definition == null
                || !FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    definition,
                    out referenceError))
            {
                sceneState = "Art Scene：错误（" + referenceError + "）";
            }
            else
            {
                Scene artScene =
                    SceneManager.GetSceneByPath(definition.ArtScenePath);
                sceneState = !artScene.IsValid() || !artScene.isLoaded
                    ? "Art Scene：未打开"
                    : artScene.isDirty
                        ? "Art Scene：未保存"
                        : "Art Scene：已保存";
            }

            if (definition != null
                && FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    definition,
                    out _))
            {
                Scene loadedArtScene = SceneManager.GetSceneByPath(
                    definition.ArtScenePath);
                if (loadedArtScene.IsValid()
                    && loadedArtScene.isLoaded
                    && !FpgRoomArtRoot.TryResolve(
                        loadedArtScene,
                        definition,
                        out _,
                        out string rootError))
                {
                    sceneState = "Art Scene: contract error (" + rootError + ")";
                }
            }

            string productionState = definition == null
                ? string.Empty
                : !FpgRoomAuthoringOperations.IsRoomRegistered(definition)
                    ? " | Production: not registered"
                    : !FpgProductionSceneList.TryValidateEditorBuildSettings(out _)
                        ? " | Production: Build Settings mismatch"
                        : " | Production: registered";
            string cameraState = sceneTool?.IsCameraPreviewActive == true
                ? $" | Camera: 16:9 {selectedPreviewCharacter?.DisplayName}"
                : string.Empty;
            string cameraTemplateState = selectedCameraTemplate == null
                ? string.Empty
                : EditorUtility.IsDirty(selectedCameraTemplate)
                    ? " | Camera Template: unsaved"
                    : " | Camera Template: saved";
            string prefix = string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message + " | ";
            statusLabel.text =
                prefix + roomState + " | " + sceneState + cameraState
                + cameraTemplateState + productionState;
        }

        private void OnSceneDirtied(Scene scene)
        {
            if (IsSelectedArtScene(scene))
            {
                UpdateLevelStatus();
            }
        }

        private void OnSceneSaved(Scene scene)
        {
            if (IsSelectedArtScene(scene))
            {
                UpdateLevelStatus();
            }
        }

        private bool IsSelectedArtScene(Scene scene)
        {
            return selectedRoom is FpgRoomDefinition definition
                && scene.IsValid()
                && string.Equals(
                    scene.path,
                    definition.ArtScenePath,
                    StringComparison.Ordinal);
        }

        private void UpdateCameraPreviewControls()
        {
            bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
            cameraTemplateField?.SetEnabled(!isPlaying);
            previewCharacterField?.SetEnabled(!isPlaying);
            applyCameraPreviewButton?.SetEnabled(
                !isPlaying
                && selectedRoom != null
                && selectedPreviewCharacter != null);
            stopCameraPreviewButton?.SetEnabled(
                !isPlaying
                && (sceneTool?.IsCameraPreviewActive == true
                    || gameViewAspectSession != null));
            bool previewActive = !isPlaying
                && sceneTool?.IsCameraPreviewActive == true;
            previousCameraCoverButton?.SetEnabled(previewActive);
            nextCameraCoverButton?.SetEnabled(previewActive);
            previewCameraTransitionButton?.SetEnabled(previewActive);
            captureSceneViewCameraButton?.SetEnabled(previewActive);
            restoreCameraTemplateButton?.SetEnabled(
                previewActive && selectedCameraTemplate != null);
            if (markerToolButtons.TryGetValue(
                    FpgRoomMarkerKind.Cover,
                    out Button coverButton))
            {
                coverButton.SetEnabled(!isPlaying
                    && selectedRoom != null
                    && selectedCameraTemplate != null);
            }
        }

        private string RestoreGameViewAspect(bool logFailure)
        {
            FpgGameViewAspectSession session = gameViewAspectSession;
            if (session == null)
            {
                return string.Empty;
            }

            if (session.TryRestore(out string error))
            {
                gameViewAspectSession = null;
                return string.Empty;
            }

            if (logFailure)
            {
                Debug.LogWarning(error);
            }

            return error;
        }

        private void BindVisibility(string toggleName, FpgRoomMarkerKind kind)
        {
            rootVisualElement.Q<Toggle>(toggleName).RegisterValueChangedCallback(evt =>
                sceneTool?.SetVisibility(kind, evt.newValue));
        }

        private static VisualElement MakeRoomListItem()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("room-list-row");
            VisualElement dot = new VisualElement { name = "status-dot" };
            dot.AddToClassList("status-dot");
            row.Add(dot);
            VisualElement copy = new VisualElement();
            copy.AddToClassList("room-list-copy");
            Label name = new Label { name = "room-name" };
            name.AddToClassList("room-list-name");
            Label id = new Label { name = "room-id" };
            id.AddToClassList("room-list-id");
            copy.Add(name);
            copy.Add(id);
            row.Add(copy);
            return row;
        }

        private void BindRoomListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= filteredRooms.Count)
            {
                return;
            }

            FpgRoomRecord record = filteredRooms[index];
            element.Q<Label>("room-name").text = record.DisplayName;
            element.Q<Label>("room-id").text = record.RoomId;
            VisualElement dot = element.Q<VisualElement>("status-dot");
            dot.RemoveFromClassList("status-dot--valid");
            dot.RemoveFromClassList("status-dot--warning");
            dot.RemoveFromClassList("status-dot--error");
            dot.AddToClassList(record.Status == FpgRoomValidationStatus.Error
                ? "status-dot--error"
                : record.Status == FpgRoomValidationStatus.Warning
                    ? "status-dot--warning"
                    : "status-dot--valid");
            element.tooltip = string.IsNullOrWhiteSpace(record.MainGroupName)
                ? record.RoomId
                : record.MainGroupName + " / " + record.RoomId;
        }

        private static VisualElement MakeMarkerListItem()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("marker-list-row");
            VisualElement swatch = new VisualElement { name = "marker-swatch" };
            swatch.AddToClassList("marker-swatch");
            row.Add(swatch);
            VisualElement copy = new VisualElement();
            copy.AddToClassList("marker-copy");
            copy.Add(new Label { name = "marker-name" });
            Label kind = new Label { name = "marker-kind" };
            kind.AddToClassList("marker-kind");
            copy.Add(kind);
            row.Add(copy);
            return row;
        }

        private void BindMarkerListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= markers.Count)
            {
                return;
            }

            FpgRoomMarkerHandle marker = markers[index];
            element.Q<VisualElement>("marker-swatch").style.backgroundColor =
                FpgRoomAuthoringSchema.MarkerColor(marker.Kind);
            element.Q<Label>("marker-name").text = string.IsNullOrWhiteSpace(marker.DisplayName)
                ? marker.MarkerId
                : marker.DisplayName;
            element.Q<Label>("marker-kind").text = FpgRoomAuthoringSchema.MarkerKindName(marker.Kind)
                + " | " + marker.MarkerId;
        }

        private static VisualElement MakeValidationListItem()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("validation-row");
            Label icon = new Label { name = "validation-icon" };
            icon.AddToClassList("validation-icon");
            row.Add(icon);
            Label message = new Label { name = "validation-message" };
            message.AddToClassList("validation-message");
            row.Add(message);
            return row;
        }

        private void BindValidationListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= validation.Count)
            {
                return;
            }

            FpgRoomValidationItem item = validation[index];
            Label icon = element.Q<Label>("validation-icon");
            icon.RemoveFromClassList("validation-icon--error");
            icon.RemoveFromClassList("validation-icon--warning");
            icon.RemoveFromClassList("validation-icon--info");
            switch (item.Severity)
            {
                case FpgRoomValidationSeverity.Error:
                    icon.text = "E";
                    icon.AddToClassList("validation-icon--error");
                    break;
                case FpgRoomValidationSeverity.Warning:
                    icon.text = "W";
                    icon.AddToClassList("validation-icon--warning");
                    break;
                default:
                    icon.text = "I";
                    icon.AddToClassList("validation-icon--info");
                    break;
            }

            element.Q<Label>("validation-message").text = item.Message;
            element.tooltip = "Select to locate the related room field or marker.";
        }

        private void RefreshRoomAssets()
        {
            if (groupFilter == null || tagFilter == null || roomList == null || roomCountLabel == null)
            {
                return;
            }

            ScriptableObject keepSelection = selectedRoom;
            List<ScriptableObject> assets = FpgRoomAuthoringSchema.FindAllRooms();
            Dictionary<string, int> idCounts = assets
                .Select(room => FpgRoomAuthoringSchema.GetString(room, "roomId"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            allRooms.Clear();
            allRooms.AddRange(assets.Select(room =>
                new FpgRoomRecord(room, FpgRoomAuthoringSchema.Validate(room, idCounts))));

            List<string> groups = new List<string> { "All Groups" };
            groups.AddRange(allRooms.Select(room => room.MainGroupName)
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().OrderBy(name => name));
            groupFilter.choices = groups;
            if (!groups.Contains(groupFilter.value))
            {
                groupFilter.SetValueWithoutNotify("All Groups");
            }

            List<string> tags = new List<string> { "All Tags" };
            tags.AddRange(allRooms.SelectMany(room => room.TagNames)
                .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().OrderBy(name => name));
            tagFilter.choices = tags;
            if (!tags.Contains(tagFilter.value))
            {
                tagFilter.SetValueWithoutNotify("All Tags");
            }

            ApplyFilters();
            if (keepSelection != null && allRooms.Any(record => record.Asset == keepSelection))
            {
                SelectRoom(keepSelection);
            }
        }

        private void ApplyFilters()
        {
            string search = searchField?.value ?? string.Empty;
            string group = groupFilter?.value ?? "All Groups";
            string tag = tagFilter?.value ?? "All Tags";
            string status = statusFilter?.value ?? "All";

            filteredRooms.Clear();
            filteredRooms.AddRange(allRooms.Where(record =>
                (string.IsNullOrWhiteSpace(search)
                 || record.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                 || record.RoomId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                && (group == "All Groups" || record.MainGroupName == group)
                && (tag == "All Tags" || record.TagNames.Contains(tag))
                && MatchesStatus(record.Status, status)));
            roomList?.RefreshItems();
            roomCountLabel.text = $"{filteredRooms.Count} / {allRooms.Count} rooms";
        }

        private static bool MatchesStatus(FpgRoomValidationStatus value, string filter)
        {
            return filter == "All"
                || filter == "Valid" && value == FpgRoomValidationStatus.Valid
                || filter == "Warning" && value == FpgRoomValidationStatus.Warning
                || filter == "Error" && value == FpgRoomValidationStatus.Error;
        }

        private void OnRoomSelectionChanged(IEnumerable<object> selection)
        {
            if (suppressRoomSelectionChanged)
            {
                return;
            }

            FpgRoomRecord record = selection.OfType<FpgRoomRecord>().FirstOrDefault();
            if (record != null && !SelectRoom(record.Asset))
            {
                RestoreRoomListSelection(selectedRoom);
            }
        }

        private bool SelectRoom(ScriptableObject room)
        {
            if (ReferenceEquals(selectedRoom, room))
            {
                serializedRoom?.Update();
                if (sceneTool?.Room != room)
                {
                    sceneTool?.SetRoom(room);
                }
                else
                {
                    sceneTool?.RebuildPreview();
                }

                UpdateLevelStatus();
                return true;
            }

            string selectionMessage = string.Empty;
            if (room is FpgRoomDefinition definition
                && !TryOpenArtSceneForRoom(
                    definition,
                    out selectionMessage))
            {
                if (!string.IsNullOrWhiteSpace(selectionMessage))
                {
                    statusLabel.text = selectionMessage;
                }

                return false;
            }

            selectedRoom = room;
            serializedRoom = room == null ? null : new SerializedObject(room);
            SessionState.SetString(
                SelectedRoomSessionKey,
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(room)));
            bool cameraPreviewWasActive = sceneTool?.IsCameraPreviewActive == true;
            sceneTool?.SetRoom(room);
            RebuildRoomDetails();
            RefreshMarkers();
            RefreshValidation();

            RestoreRoomListSelection(room);

            bool cameraPreviewIsActive = sceneTool?.IsCameraPreviewActive == true;
            if (!cameraPreviewWasActive || cameraPreviewIsActive)
            {
                UpdateLevelStatus(selectionMessage);
            }

            UpdateCameraPreviewControls();
            return true;
        }

        private bool TryOpenArtSceneForRoom(
            FpgRoomDefinition room,
            out string message)
        {
            message = string.Empty;
            if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    room,
                    out string referenceError))
            {
                sceneTool?.PrepareForSceneSaveOrSwitch();
                message =
                    $"Room '{room.RoomId}' Art Scene is unavailable: {referenceError}";
                return true;
            }

            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.IsValid()
                && currentScene.isLoaded
                && string.Equals(
                    currentScene.path,
                    room.ArtScenePath,
                    StringComparison.Ordinal))
            {
                return true;
            }

            bool roomDirty = selectedRoom != null
                && EditorUtility.IsDirty(selectedRoom);
            bool sceneDirty = currentScene.IsValid()
                && currentScene.isLoaded
                && currentScene.isDirty;
            if (roomDirty || sceneDirty)
            {
                string currentRoomName = selectedRoom == null
                    ? "<none>"
                    : selectedRoom.name;
                string currentSceneName = currentScene.IsValid()
                    ? currentScene.name
                    : "<none>";
                int choice = EditorUtility.DisplayDialogComplex(
                    "切换关卡美术场景",
                    $"当前 RoomDefinition '{currentRoomName}' 或场景 '{currentSceneName}' 有未保存修改。",
                    "保存",
                    "放弃",
                    "取消");
                if (choice == 2)
                {
                    message = "已取消切换关卡美术场景。";
                    return false;
                }

                sceneTool?.PrepareForSceneSaveOrSwitch();
                if (choice == 0)
                {
                    if (selectedRoom != null)
                    {
                        AssetDatabase.SaveAssetIfDirty(selectedRoom);
                    }

                    if (sceneDirty
                        && !EditorSceneManager.SaveScene(currentScene))
                    {
                        message =
                            $"无法保存场景 '{currentScene.path}'，已取消切换。";
                        sceneTool?.RebuildPreview();
                        return false;
                    }

                    if (selectedRoom != null
                        && EditorUtility.IsDirty(selectedRoom))
                    {
                        message =
                            $"无法保存 RoomDefinition '{selectedRoom.name}'，已取消切换。";
                        sceneTool?.RebuildPreview();
                        return false;
                    }
                }
                else if (selectedRoom != null)
                {
                    string roomPath =
                        AssetDatabase.GetAssetPath(selectedRoom);
                    AssetDatabase.ImportAsset(
                        roomPath,
                        ImportAssetOptions.ForceUpdate);
                }
            }
            else
            {
                sceneTool?.PrepareForSceneSaveOrSwitch();
            }

            try
            {
                Scene artScene = EditorSceneManager.OpenScene(
                    room.ArtScenePath,
                    OpenSceneMode.Single);
                if (!SceneManager.SetActiveScene(artScene))
                {
                    message =
                        $"无法将关卡美术场景 '{room.ArtScenePath}' 设为 Active。";
                    return false;
                }
            }
            catch (Exception exception)
            {
                message =
                    $"打开关卡美术场景 '{room.ArtScenePath}' 失败："
                    + exception.GetBaseException().Message;
                return false;
            }

            message = $"已打开关卡美术场景：{room.ArtScenePath}";
            return true;
        }

        private void RestoreRoomListSelection(ScriptableObject room)
        {
            if (roomList == null)
            {
                return;
            }

            int roomIndex =
                filteredRooms.FindIndex(record => record.Asset == room);
            suppressRoomSelectionChanged = true;
            try
            {
                if (roomIndex >= 0)
                {
                    roomList.SetSelectionWithoutNotify(new[] { roomIndex });
                    roomList.ScrollToItem(roomIndex);
                }
                else
                {
                    roomList.ClearSelection();
                }
            }
            finally
            {
                suppressRoomSelectionChanged = false;
            }
        }

        private void RebuildRoomDetails()
        {
            roomDetails.Unbind();
            roomDetails.Clear();
            if (serializedRoom == null)
            {
                Label empty = new Label("Select a room to edit its properties.");
                empty.AddToClassList("empty-state");
                roomDetails.Add(empty);
                return;
            }

            string[] properties =
            {
                "roomId", "displayName", "designerNotes", "artScene", "mainGroup", "tags"
            };
            foreach (string propertyName in properties)
            {
                SerializedProperty property = serializedRoom.FindProperty(propertyName);
                if (property == null)
                {
                    continue;
                }

                PropertyField field = new PropertyField(property, FpgRoomAuthoringSchema.ChinesePropertyName(propertyName));
                field.BindProperty(property);
                if (propertyName == "roomId")
                {
                    List<ScriptableObject> rooms = FpgRoomAuthoringSchema.FindAllRooms();
                    int sameIdCount = rooms.Count(candidate =>
                        string.Equals(
                            FpgRoomAuthoringSchema.GetString(candidate, "roomId"),
                            property.stringValue,
                            StringComparison.Ordinal));
                    bool canRepair = string.IsNullOrWhiteSpace(property.stringValue)
                        || sameIdCount > 1;
                    field.SetEnabled(canRepair);
                    field.tooltip = canRepair
                        ? "Room ID is missing or duplicated. Saving will generate a new stable ID."
                        : "Room ID is stable and read-only. Duplicate the room to generate a new ID.";
                }

                field.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnRoomPropertyChanged(propertyName));
                roomDetails.Add(field);
            }

            BuildFormalEncounterPreviewPanel();
        }

        private void BuildFormalEncounterPreviewPanel()
        {
            Foldout foldout = new Foldout
            {
                text = "Formal Encounter Preview",
                value = true
            };
            foldout.AddToClassList("formal-encounter-preview");

            ObjectField profileField = new ObjectField("Encounter Profile")
            {
                objectType = typeof(FpgEncounterProfile),
                allowSceneObjects = false
            };
            profileField.SetValueWithoutNotify(formalPreviewProfile);
            foldout.Add(profileField);

            ObjectField overrideField = new ObjectField("Encounter Override")
            {
                objectType = typeof(FpgEncounterOverrideDefinition),
                allowSceneObjects = false
            };
            overrideField.SetValueWithoutNotify(formalPreviewOverride);
            foldout.Add(overrideField);

            LongField seedField = new LongField("Run Seed")
            {
                isDelayed = true
            };
            seedField.SetValueWithoutNotify(formalPreviewSeed);
            foldout.Add(seedField);

            IntegerField depthField = new IntegerField("Depth")
            {
                isDelayed = true
            };
            depthField.SetValueWithoutNotify(formalPreviewDepth);
            foldout.Add(depthField);

            IntegerField difficultyField = new IntegerField("Difficulty (Basis Points)")
            {
                isDelayed = true
            };
            difficultyField.SetValueWithoutNotify(formalPreviewDifficultyBasisPoints);
            foldout.Add(difficultyField);

            IntegerField visitField = new IntegerField("Room Visit Ordinal")
            {
                isDelayed = true
            };
            visitField.SetValueWithoutNotify(formalPreviewRoomVisitOrdinal);
            foldout.Add(visitField);

            Button generateButton = new Button(GenerateFormalEncounterPreview)
            {
                text = "Generate Formal Preview"
            };
            generateButton.AddToClassList("formal-encounter-preview__generate");
            foldout.Add(generateButton);

            Button playtestButton = new Button(StartFormalEncounterPlaytest)
            {
                text = "Run in Active Formal Host"
            };
            foldout.Add(playtestButton);

            formalPreviewOutput = new VisualElement();
            formalPreviewOutput.AddToClassList("formal-encounter-preview__output");
            foldout.Add(formalPreviewOutput);
            ResetFormalEncounterPreviewOutput("No formal preview generated.");

            profileField.RegisterValueChangedCallback(evt =>
            {
                formalPreviewProfile = evt.newValue as FpgEncounterProfile;
                ResetFormalEncounterPreviewOutput("Preview inputs changed.");
            });
            overrideField.RegisterValueChangedCallback(evt =>
            {
                formalPreviewOverride = evt.newValue as FpgEncounterOverrideDefinition;
                ResetFormalEncounterPreviewOutput("Preview inputs changed.");
            });
            seedField.RegisterValueChangedCallback(evt =>
            {
                formalPreviewSeed = evt.newValue;
                ResetFormalEncounterPreviewOutput("Preview inputs changed.");
            });
            depthField.RegisterValueChangedCallback(evt =>
            {
                formalPreviewDepth = Math.Max(0, evt.newValue);
                depthField.SetValueWithoutNotify(formalPreviewDepth);
                ResetFormalEncounterPreviewOutput("Preview inputs changed.");
            });
            difficultyField.RegisterValueChangedCallback(evt =>
            {
                formalPreviewDifficultyBasisPoints = Math.Max(1, evt.newValue);
                difficultyField.SetValueWithoutNotify(formalPreviewDifficultyBasisPoints);
                ResetFormalEncounterPreviewOutput("Preview inputs changed.");
            });
            visitField.RegisterValueChangedCallback(evt =>
            {
                formalPreviewRoomVisitOrdinal = Math.Max(0, evt.newValue);
                visitField.SetValueWithoutNotify(formalPreviewRoomVisitOrdinal);
                ResetFormalEncounterPreviewOutput("Preview inputs changed.");
            });

            roomDetails.Add(foldout);
        }

        private void GenerateFormalEncounterPreview()
        {
            if (formalPreviewOutput == null)
            {
                return;
            }

            formalPreviewOutput.Clear();
            FpgRoomDefinition room = selectedRoom as FpgRoomDefinition;
            if (!FpgEncounterPreviewUtility.TryGenerate(
                    room,
                    formalPreviewProfile,
                    formalPreviewOverride,
                    formalPreviewSeed,
                    "default",
                    formalPreviewDepth,
                    formalPreviewDifficultyBasisPoints,
                    formalPreviewRoomVisitOrdinal,
                    out FpgEncounterPlan plan,
                    out string error))
            {
                formalPreviewOutput.Add(new HelpBox(error, HelpBoxMessageType.Error));
                return;
            }

            FpgEncounterConcurrencyEstimate concurrency =
                FpgEncounterPreviewUtility.EstimateConcurrency(plan, formalPreviewProfile);
            Label summary = new Label(
                $"Digest {plan.Digest:X16} | Total budget {plan.TotalBudget} | "
                + $"Layout {plan.WaveLayoutId} | Waves {plan.WaveCount} | Entries {plan.EntryCount}");
            summary.AddToClassList("formal-encounter-preview__summary");
            formalPreviewOutput.Add(summary);

            Label concurrencyLabel = new Label(
                $"Estimated peak: cap {concurrency.CapWeight} / entities {concurrency.EntityCount}");
            concurrencyLabel.AddToClassList("formal-encounter-preview__muted");
            formalPreviewOutput.Add(concurrencyLabel);

            for (int waveIndex = 0; waveIndex < plan.Waves.Count; waveIndex++)
            {
                FpgEncounterWavePlan wave = plan.Waves[waveIndex];
                string composition = string.Join(", ", wave.Entries
                    .GroupBy(entry => entry.EnemyDefinitionId, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => group.Key + " x" + group.Count()));
                Label waveLabel = new Label(
                    $"Wave {waveIndex + 1}: share {wave.BudgetShareBasisPoints} bp, "
                    + $"requested {wave.RequestedBudget}, spent {wave.Budget}; "
                    + (string.IsNullOrEmpty(composition) ? "no enemies" : composition));
                waveLabel.AddToClassList("formal-encounter-preview__wave");
                formalPreviewOutput.Add(waveLabel);
            }

            SortedDictionary<string, FpgEnemyRole> plannedEnemies =
                new SortedDictionary<string, FpgEnemyRole>(StringComparer.Ordinal);
            for (int waveIndex = 0; waveIndex < plan.Waves.Count; waveIndex++)
            {
                IReadOnlyList<FpgSpawnEntry> entries = plan.Waves[waveIndex].Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    plannedEnemies[entries[entryIndex].EnemyDefinitionId] =
                        entries[entryIndex].Role;
                }
            }

            Label spawnTitle = new Label("SpawnPoint Compatibility");
            spawnTitle.AddToClassList("formal-encounter-preview__subheading");
            formalPreviewOutput.Add(spawnTitle);

            IReadOnlyList<FpgRoomEnemySpawnPoint> spawnPoints = room.EnemySpawnPoints;
            for (int pointIndex = 0; pointIndex < spawnPoints.Count; pointIndex++)
            {
                FpgRoomEnemySpawnPoint point = spawnPoints[pointIndex];
                List<string> compatible = new List<string>();
                foreach (KeyValuePair<string, FpgEnemyRole> enemy in plannedEnemies)
                {
                    if (FpgEncounterPreviewUtility.IsSpawnPointCompatible(point.Role, enemy.Value))
                    {
                        compatible.Add(enemy.Key);
                    }
                }

                string pointId = string.IsNullOrWhiteSpace(point.MarkerId)
                    ? "SpawnPoint " + pointIndex
                    : point.MarkerId;
                Label pointLabel = new Label(
                    $"{pointId} [{point.Role}] -> "
                    + (compatible.Count == 0
                        ? "no compatible planned enemy"
                        : string.Join(", ", compatible)));
                pointLabel.AddToClassList(compatible.Count == 0
                    ? "formal-encounter-preview__missing"
                    : "formal-encounter-preview__spawn");
                formalPreviewOutput.Add(pointLabel);
            }

            foreach (KeyValuePair<string, FpgEnemyRole> enemy in plannedEnemies)
            {
                bool matched = false;
                for (int pointIndex = 0; pointIndex < spawnPoints.Count; pointIndex++)
                {
                    if (FpgEncounterPreviewUtility.IsSpawnPointCompatible(
                            spawnPoints[pointIndex].Role,
                            enemy.Value))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    formalPreviewOutput.Add(new HelpBox(
                        $"Missing compatible SpawnPoint: {enemy.Key} ({enemy.Value}).",
                        HelpBoxMessageType.Error));
                }
            }
        }

        private void StartFormalEncounterPlaytest()
        {
            if (!EditorApplication.isPlaying)
            {
                ResetFormalEncounterPreviewOutput(
                    "Enter Play Mode in a formal encounter scene before starting a formal playtest.");
                return;
            }

            FpgRoomDefinition room = selectedRoom as FpgRoomDefinition;
            if (!FpgEncounterPreviewUtility.TryGenerate(
                    room,
                    formalPreviewProfile,
                    formalPreviewOverride,
                    formalPreviewSeed,
                    "default",
                    formalPreviewDepth,
                    formalPreviewDifficultyBasisPoints,
                    formalPreviewRoomVisitOrdinal,
                    out FpgEncounterPlan previewPlan,
                    out string previewError))
            {
                ResetFormalEncounterPreviewOutput(previewError);
                return;
            }

            FpgEncounterHost[] discoveredHosts =
                UnityEngine.Object.FindObjectsByType<FpgEncounterHost>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            FpgEncounterHost host = null;
            FpgFormalEncounterHost formalSceneHost = null;
            int hostCount = 0;
            for (int index = 0; index < discoveredHosts.Length; index++)
            {
                FpgEncounterHost candidate = discoveredHosts[index];
                if (candidate == null
                    || EditorUtility.IsPersistent(candidate)
                    || !candidate.gameObject.scene.IsValid()
                    || !candidate.gameObject.scene.isLoaded)
                {
                    continue;
                }

                FpgFormalEncounterHost[] sceneHosts =
                    candidate.gameObject.scene.GetRootGameObjects()
                        .SelectMany(root =>
                            root.GetComponentsInChildren<FpgFormalEncounterHost>(true))
                        .ToArray();
                if (sceneHosts.Length != 1)
                {
                    continue;
                }

                host = candidate;
                formalSceneHost = sceneHosts[0];
                hostCount++;
            }

            if (hostCount != 1 || formalSceneHost == null)
            {
                ResetFormalEncounterPreviewOutput(
                    $"Formal playtest requires exactly one paired FpgEncounterHost/FpgFormalEncounterHost; found {hostCount}.");
                return;
            }

            FpgEncounterRunContext context = new FpgEncounterRunContext(
                unchecked((ulong)formalPreviewSeed),
                "default",
                Math.Max(0, formalPreviewDepth),
                Math.Max(1, formalPreviewDifficultyBasisPoints),
                Math.Max(0, formalPreviewRoomVisitOrdinal));
            bool composedForPlaytest = false;
            try
            {
                if (!formalSceneHost.IsPlayerComposed)
                {
                    if (!formalSceneHost.TryComposeDefaultPlayer(
                            out string composeError))
                    {
                        ResetFormalEncounterPreviewOutput(
                            "Formal default-player composition failed: "
                            + composeError);
                        return;
                    }

                    composedForPlaytest = true;
                }

                if (!formalSceneHost.TryValidateRuntime(
                        out string formalHostError))
                {
                    if (composedForPlaytest)
                    {
                        formalSceneHost.ClearPlayerComposition();
                    }

                    ResetFormalEncounterPreviewOutput(formalHostError);
                    return;
                }

                FpgFormalEncounterPlaytestOverrides.Set(
                    room,
                    formalPreviewProfile,
                    formalPreviewOverride,
                    context);
                if (!host.TryPrepareAndStart(out string hostError))
                {
                    host.StopAndClear();
                    if (composedForPlaytest)
                    {
                        formalSceneHost.ClearPlayerComposition();
                    }

                    ResetFormalEncounterPreviewOutput(hostError);
                    return;
                }

                if (host.Plan == null || host.Plan.Digest != previewPlan.Digest)
                {
                    string runtimeDigest = host.Plan == null
                        ? "missing"
                        : host.Plan.Digest.ToString("X16");
                    host.StopAndClear();
                    if (composedForPlaytest)
                    {
                        formalSceneHost.ClearPlayerComposition();
                    }

                    ResetFormalEncounterPreviewOutput(
                        $"Formal host plan mismatch: preview {previewPlan.Digest:X16}, runtime {runtimeDigest}.");
                    return;
                }

                if (!formalSceneHost.TryActivatePlayerPresentation(
                        out string presentationError))
                {
                    host.StopAndClear();
                    formalSceneHost.ClearPlayerComposition();
                    ResetFormalEncounterPreviewOutput(
                        "Formal player presentation activation failed: "
                        + presentationError);
                    return;
                }

                formalSceneHost.SetPresentationEnabled(true);
                GenerateFormalEncounterPreview();
                formalPreviewOutput.Add(new HelpBox(
                    $"Formal host started with default player "
                    + $"{formalSceneHost.ActivePlayerDefinition.CharacterId} "
                    + $"and matching plan {previewPlan.Digest:X16}.",
                    HelpBoxMessageType.Info));
            }
            catch (Exception exception)
            {
                host?.StopAndClear();
                if (composedForPlaytest)
                {
                    formalSceneHost?.ClearPlayerComposition();
                }

                ResetFormalEncounterPreviewOutput(exception.Message);
            }
            finally
            {
                FpgFormalEncounterPlaytestOverrides.Clear();
            }
        }

        private void ResetFormalEncounterPreviewOutput(string message)
        {
            if (formalPreviewOutput == null)
            {
                return;
            }

            formalPreviewOutput.Clear();
            Label label = new Label(message);
            label.AddToClassList("formal-encounter-preview__muted");
            formalPreviewOutput.Add(label);
        }

        private void OnRoomPropertyChanged(string propertyName)
        {
            if (selectedRoom != null)
            {
                if (serializedRoom != null && serializedRoom.hasModifiedProperties)
                {
                    EditorUtility.SetDirty(selectedRoom);
                }
            }

            string statusMessage = string.Empty;
            if (propertyName == "artScene"
                && selectedRoom is FpgRoomDefinition definition)
            {
                TryOpenArtSceneForRoom(
                    definition,
                    out statusMessage);
                sceneTool?.RebuildPreview();
            }

            QueueCurrentRoomRefresh();
            UpdateLevelStatus(statusMessage);
        }

        private void RefreshMarkers()
        {
            FpgRoomMarkerHandle keep = sceneTool?.SelectedMarker;
            markers.Clear();
            markers.AddRange(FpgRoomAuthoringSchema.GetMarkers(selectedRoom));
            markerList.Rebuild();
            if (keep != null)
            {
                int index = markers.FindIndex(marker => marker.Kind == keep.Kind && marker.Index == keep.Index);
                if (index >= 0)
                {
                    markerList.SetSelectionWithoutNotify(new[] { index });
                }
            }

            RebuildMarkerDetails(sceneTool?.SelectedMarker);
        }

        private void OnMarkerSelectionChanged(IEnumerable<object> selection)
        {
            FpgRoomMarkerHandle marker = selection.OfType<FpgRoomMarkerHandle>().FirstOrDefault();
            sceneTool?.SelectMarker(marker, false);
        }

        private void OnSceneMarkerSelectionChanged(FpgRoomMarkerHandle marker)
        {
            int index = marker == null
                ? -1
                : markers.FindIndex(item => item.Kind == marker.Kind && item.Index == marker.Index);
            if (marker != null && index < 0)
            {
                RefreshMarkers();
                index = markers.FindIndex(item => item.Kind == marker.Kind && item.Index == marker.Index);
            }

            if (index >= 0)
            {
                markerList.SetSelectionWithoutNotify(new[] { index });
                markerList.ScrollToItem(index);
            }
            else
            {
                markerList.SetSelectionWithoutNotify(Array.Empty<int>());
            }

            RebuildMarkerDetails(marker);
        }

        private void RebuildMarkerDetails(FpgRoomMarkerHandle handle)
        {
            markerDetails.Unbind();
            markerDetails.Clear();
            if (serializedRoom == null || handle == null)
            {
                Label empty = new Label("Select a marker in the list or Scene View.");
                empty.AddToClassList("empty-state");
                markerDetails.Add(empty);
                return;
            }

            serializedRoom.Update();
            SerializedProperty marker = FpgRoomAuthoringSchema.FindMarkerProperty(
                serializedRoom, handle.Kind, handle.Index);
            if (marker == null)
            {
                return;
            }

            SerializedProperty iterator = marker.Copy();
            SerializedProperty end = marker.GetEndProperty();
            int childDepth = marker.depth + 1;
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != childDepth)
                {
                    continue;
                }

                SerializedProperty child = iterator.Copy();
                PropertyField field = new PropertyField(child, FpgRoomAuthoringSchema.ChinesePropertyName(child.name));
                field.BindProperty(child);
                field.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                {
                    if (serializedRoom != null && serializedRoom.hasModifiedProperties)
                    {
                        EditorUtility.SetDirty(selectedRoom);
                    }
                    if (handle.Kind == FpgRoomMarkerKind.Destructible
                        || handle.Kind == FpgRoomMarkerKind.Cover)
                    {
                        sceneTool?.QueuePreviewRefresh();
                    }
                    else if (handle.Kind == FpgRoomMarkerKind.PlayerEntry)
                    {
                        sceneTool?.QueueCameraPreviewRefresh();
                    }
                    QueueCurrentRoomRefresh();
                    SceneView.RepaintAll();
                });
                markerDetails.Add(field);
            }

            BuildCoverCameraDetails(handle);
        }

        private void BuildCoverCameraDetails(FpgRoomMarkerHandle handle)
        {
            if (handle == null || handle.Kind != FpgRoomMarkerKind.Cover
                || serializedRoom == null)
            {
                return;
            }

            SerializedProperty cover = FpgRoomAuthoringSchema.FindMarkerProperty(
                serializedRoom,
                FpgRoomMarkerKind.Cover,
                handle.Index);
            FpgCoverCameraProfile profile = cover
                ?.FindPropertyRelative("cameraProfile")
                ?.objectReferenceValue as FpgCoverCameraProfile;
            if (profile == null)
            {
                markerDetails.Add(new HelpBox(
                    "This cover requires a camera profile before preview and runtime start are available.",
                    HelpBoxMessageType.Error));
                return;
            }

            int referenceCount =
                FpgCoverCameraProfileAuthoring.CountReferences(profile);
            Label referenceLabel = new Label(
                $"Camera profile references: {referenceCount}");
            referenceLabel.AddToClassList("camera-profile-reference-count");
            markerDetails.Add(referenceLabel);
            if (referenceCount > 1)
            {
                markerDetails.Add(new HelpBox(
                    "This profile is shared by multiple covers. Edit the asset itself if you want all of them to change.",
                    HelpBoxMessageType.Warning));
            }

            markerDetails.Add(new HelpBox(
                "Edit the camera settings on the profile asset itself. This cover only stores a reference to that asset.",
                HelpBoxMessageType.Info));

            VisualElement actions = new VisualElement();
            actions.AddToClassList("camera-profile-actions");
            if (referenceCount > 1)
            {
                Button makeUnique = new Button(() =>
                {
                    if (!(selectedRoom is FpgRoomDefinition definition))
                    {
                        UpdateLevelStatus("Select a valid room first.");
                        return;
                    }

                    if (!FpgCoverCameraProfileAuthoring.TryMakeCoverProfileUnique(
                            definition,
                            handle.Index,
                            out FpgCoverCameraProfile unique,
                            out string error))
                    {
                        UpdateLevelStatus(error);
                        return;
                    }

                    Selection.activeObject = unique;
                    sceneTool?.RebuildPreview();
                    QueueCurrentRoomRefresh();
                    UpdateLevelStatus(
                        $"Created independent camera profile '{unique.name}'.");
                })
                {
                    text = "Create Unique Profile"
                };
                actions.Add(makeUnique);
            }

            actions.Add(new Button(() =>
            {
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
            })
            {
                text = "Edit Profile Asset"
            });
            markerDetails.Add(actions);
        }

        private void RefreshValidation()
        {
            validation.Clear();
            if (selectedRoom != null)
            {
                List<ScriptableObject> rooms = FpgRoomAuthoringSchema.FindAllRooms();
                Dictionary<string, int> idCounts = rooms
                    .Select(room => FpgRoomAuthoringSchema.GetString(room, "roomId"))
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .GroupBy(id => id, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                validation.AddRange(FpgRoomAuthoringSchema.Validate(selectedRoom, idCounts));

                if (selectedRoom is FpgRoomDefinition definition)
                {
                    if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                            definition,
                            out string referenceError))
                    {
                        validation.Add(new FpgRoomValidationItem(
                            FpgRoomValidationSeverity.Error,
                            "Art Scene reference is invalid: " + referenceError,
                            "artScene"));
                    }
                    else
                    {
                        Scene loadedArtScene = SceneManager.GetSceneByPath(
                            definition.ArtScenePath);
                        if (loadedArtScene.IsValid()
                            && loadedArtScene.isLoaded
                            && !FpgRoomArtRoot.TryResolve(
                                loadedArtScene,
                                definition,
                                out _,
                                out string contractError))
                        {
                            validation.Add(new FpgRoomValidationItem(
                                FpgRoomValidationSeverity.Error,
                                "Art Scene contract is invalid: "
                                + contractError,
                                "artScene"));
                        }
                    }

                    if (!FpgRoomAuthoringOperations.IsRoomRegistered(definition))
                    {
                        validation.Add(new FpgRoomValidationItem(
                            FpgRoomValidationSeverity.Warning,
                            "Room is not registered in the production RoomCatalog; runtime routing and builds cannot use it."));
                    }
                    else if (!FpgProductionSceneList.TryValidateEditorBuildSettings(
                            out string buildSettingsError))
                    {
                        validation.Add(new FpgRoomValidationItem(
                            FpgRoomValidationSeverity.Error,
                            "Production Build Settings are invalid: "
                            + buildSettingsError));
                    }
                }
            }

            if (selectedRoom != null && validation.Count == 0)
            {
                validation.Add(new FpgRoomValidationItem(FpgRoomValidationSeverity.Info, "Room validation passed."));
            }

            validationList.RefreshItems();
            int errors = validation.Count(item => item.Severity == FpgRoomValidationSeverity.Error);
            int warnings = validation.Count(item => item.Severity == FpgRoomValidationSeverity.Warning);
            validationSummaryLabel.text = $"{errors} errors | {warnings} warnings";
        }

        private void OnValidationSelectionChanged(IEnumerable<object> selection)
        {
            FpgRoomValidationItem item = selection.OfType<FpgRoomValidationItem>().FirstOrDefault();
            if (item == null)
            {
                return;
            }

            if (item.MarkerKind.HasValue && item.MarkerIndex >= 0)
            {
                FpgRoomMarkerHandle marker = markers.FirstOrDefault(candidate =>
                    candidate.Kind == item.MarkerKind.Value && candidate.Index == item.MarkerIndex);
                sceneTool?.SelectMarker(marker, true);
            }
            else if (!string.IsNullOrWhiteSpace(item.PropertyPath))
            {
                rootVisualElement.Q<ScrollView>("room-details-scroll")?.ScrollTo(roomDetails);
            }
        }

        private void QueueCurrentRoomRefresh()
        {
            if (refreshQueued)
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                if (this == null)
                {
                    return;
                }

                serializedRoom?.Update();
                RefreshMarkers();
                RefreshRoomAssets();
                RefreshValidation();
                UpdateLevelStatus();
                Repaint();
            };
        }

        private void OnProjectChanged()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                sceneTool?.RebuildPreview();
                RefreshRoomAssets();
            };
        }

        private void CreateRoom()
        {
            Type roomType = FpgRoomAuthoringSchema.RoomType;
            if (roomType == null)
            {
                EditorUtility.DisplayDialog("Cannot Create Room", "FPG.Unity does not expose FpgRoomDefinition.", "OK");
                return;
            }

            EnsureAssetFolder(FpgRoomAuthoringSchema.DefaultRoomAssetFolder);
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Room",
                "Room_New",
                "asset",
                "Choose a location for the room asset.",
                FpgRoomAuthoringSchema.DefaultRoomAssetFolder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ScriptableObject asset = ScriptableObject.CreateInstance(roomType);
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("roomId").stringValue = GenerateRoomId();
            serialized.FindProperty("displayName").stringValue = Path.GetFileNameWithoutExtension(path);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(asset);
            RefreshRoomAssets();
            SelectRoom(asset);
            Selection.activeObject = asset;
        }

        private void DuplicateRoom()
        {
            if (!(selectedRoom is FpgRoomDefinition sourceRoom))
            {
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceRoom);
            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string defaultName = sourceRoom.name + "_Copy";
            string path = EditorUtility.SaveFilePanelInProject(
                "Duplicate Room",
                defaultName,
                "asset",
                "Copies the room and Art Scene, repairs the scene root binding, and registers both for production.",
                directory);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            sceneTool?.PrepareForSceneSaveOrSwitch();
            if (!FpgRoomAuthoringOperations.TryDuplicateRoomWithArtScene(
                    sourceRoom,
                    path,
                    true,
                    out FpgRoomDefinition copy,
                    out string error))
            {
                sceneTool?.RebuildPreview();
                EditorUtility.DisplayDialog(
                    "Duplicate Room Failed",
                    error,
                    "OK");
                UpdateLevelStatus("Duplicate room failed: " + error);
                return;
            }

            RefreshRoomAssets();
            SelectRoom(copy);
            Selection.activeObject = copy;
            UpdateLevelStatus(
                $"Duplicated room '{copy.RoomId}' with a bound Art Scene and production registration.");
        }

        private void SaveRoom()
        {
            if (!(selectedRoom is FpgRoomDefinition definition))
            {
                return;
            }

            if (!FpgRoomArtSceneEditorUtility.TryValidateStoredReference(
                    definition,
                    out string referenceError))
            {
                UpdateLevelStatus("保存关卡失败：" + referenceError);
                return;
            }

            Scene artScene =
                SceneManager.GetSceneByPath(definition.ArtScenePath);
            if (!artScene.IsValid() || !artScene.isLoaded
                || SceneManager.GetActiveScene() != artScene)
            {
                UpdateLevelStatus(
                    $"保存关卡失败：当前 Active Scene 必须是 '{definition.ArtScenePath}'。");
                return;
            }

            sceneTool?.PrepareForSceneSaveOrSwitch();
            AssetDatabase.SaveAssetIfDirty(selectedRoom);
            if (selectedCameraTemplate != null)
            {
                AssetDatabase.SaveAssetIfDirty(selectedCameraTemplate);
            }
            HashSet<FpgCoverCameraProfile> cameraProfiles =
                new HashSet<FpgCoverCameraProfile>();
            for (int index = 0; index < definition.CoverSlots.Count; index++)
            {
                FpgCoverCameraProfile profile =
                    definition.CoverSlots[index].CameraProfile;
                if (profile != null && cameraProfiles.Add(profile))
                {
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
            }
            bool sceneSaved = !artScene.isDirty
                || EditorSceneManager.SaveScene(artScene);
            if (EditorUtility.IsDirty(selectedRoom)
                || (selectedCameraTemplate != null
                    && EditorUtility.IsDirty(selectedCameraTemplate))
                || cameraProfiles.Any(EditorUtility.IsDirty)
                || !sceneSaved)
            {
                sceneTool?.RebuildPreview();
                UpdateLevelStatus("保存关卡失败，请检查 Console。");
                return;
            }

            if (!FpgRoomArtSceneContractValidator.TryValidateScene(
                    definition,
                    out string contractError))
            {
                sceneTool?.RebuildPreview();
                UpdateLevelStatus("关卡已保存，但 Art Scene 契约无效：" + contractError);
                return;
            }

            sceneTool?.RebuildPreview();
            UpdateLevelStatus("关卡已保存");
        }


        private void RefreshMarkerToolStyles()
        {
            foreach (KeyValuePair<FpgRoomMarkerKind, Button> pair in markerToolButtons)
            {
                pair.Value.EnableInClassList(
                    "marker-tool--active",
                    sceneTool?.PlacementKind == pair.Key);
            }
        }

        private void RestoreRoomSelection()
        {
            string path = AssetDatabase.GUIDToAssetPath(SessionState.GetString(SelectedRoomSessionKey, string.Empty));
            Type type = FpgRoomAuthoringSchema.RoomType;
            ScriptableObject room = type == null ? null : AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            if (room != null)
            {
                SelectRoom(room);
            }
            else if (filteredRooms.Count > 0)
            {
                SelectRoom(filteredRooms[0].Asset);
            }
            else
            {
                SelectRoom(null);
            }
        }


        private static string GenerateRoomId()
        {
            return "room-" + Guid.NewGuid().ToString("N");
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
}
}

