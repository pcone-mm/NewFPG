using System;
using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Skills;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor.SkillAuthoring
{
    public sealed class FpgSkillEditorWindow : EditorWindow
    {
        private const string EnemySkillTypeName =
            "FPG.Demo.Unity.FpgEnemyAttackDefinition";
        private const string AttackTargetKindsTypeName =
            "FPG.Demo.Combat.AttackTargetKinds";
        private const string LayoutPath =
            "Assets/FPGDemo/Editor/SkillAuthoring/FpgSkillEditor.uxml";
        private const string SelectedAssetSessionKey =
            "FPGDemo.SkillAuthoring.SelectedAssetPath";
        private const string SelectedSequenceIndexSessionKey =
            "FPGDemo.SkillAuthoring.SelectedSequenceIndex";
        private const string PreviewPrefabSessionKey =
            "FPGDemo.SkillAuthoring.PreviewPrefabPath";
        private const int TickRate = FpgSkillRuntimeConstants.TickRate;
        private const int MaximumLogEntries = 200;

        private readonly List<FpgSkillAssetRecord> allAssets =
            new List<FpgSkillAssetRecord>();
        private readonly List<FpgSkillAssetRecord> filteredAssets =
            new List<FpgSkillAssetRecord>();
        private readonly List<FpgSkillEventRecord> events =
            new List<FpgSkillEventRecord>();
        private readonly List<FpgSkillActivePresentationTrackRecord>
            presentationTracks =
                new List<FpgSkillActivePresentationTrackRecord>();
        private readonly List<FpgSkillCompiledTriggerRecord> compiledTriggers =
            new List<FpgSkillCompiledTriggerRecord>();
        private readonly FpgSkillPreviewExecution previewExecution =
            new FpgSkillPreviewExecution();
        private FpgSkillPreviewSimulationFrame previewSimulationFrame;
        private readonly List<FpgSkillValidationItem> validation =
            new List<FpgSkillValidationItem>();
        
        private readonly FpgSkillEventSelection eventSelection =
            new FpgSkillEventSelection();
        private readonly FpgSkillEventClipboard eventClipboard =
            new FpgSkillEventClipboard();
        private readonly List<FpgSkillLogEntry> eventLog =
            new List<FpgSkillLogEntry>();

        private SerializedObject serializedAsset;
        private UnityEngine.Object selectedAsset;
        private int selectedSequenceIndex;
        private int selectedPresentationTrackIndex = -1;
        private FpgSkillEventKey selectedEventKey;
        private bool selectedAnimationTrack;
        private int durationTicks = 120;
        private int currentTick;
        private int targetCount = 1;
        private int measuredAnimationDurationTicks = -1;
        private bool isPlaying;
        private bool hasCompiledSchedule;
        private FpgCompiledSkillSequence compiledSequence;
        private string previewCompileError;
        private string lastReportedPreviewFailureSignature;
        private bool refreshQueued;
        private double lastUpdateTime;
        private double tickAccumulator;
        private float playbackSpeed = 1f;

        private ToolbarSearchField assetSearchField;
        private DropdownField typeFilter;
        private ListView actionAssetList;
        private DropdownField sequenceDropdown;
        private ObjectField previewPrefabField;
        private DropdownField targetCountDropdown;
        private Toggle showGeometryToggle;
        private Label previewTickLabel;
        private Label animationLengthLabel;
        private DropdownField speedDropdown;
        private Toggle loopToggle;
        private Slider zoomSlider;
        private VisualElement inspectorContent;
        private Label inspectorTitle;
        private ListView validationList;
        private Label validationSummaryLabel;
        private ListView eventLogList;
        private Label assetStateLabel;
        private Label statusLabel;
        private Button addEventButton;
        private Button duplicateEventButton;
        private Button copyEventsButton;
        private Button pasteEventsButton;
        private Button deleteEventButton;
        private VisualElement presentationTrackTools;
        private DropdownField presentationTrackDropdown;
        private TextField presentationTrackNameField;
        private Button addPresentationTrackButton;
        private Button movePresentationTrackUpButton;
        private Button movePresentationTrackDownButton;
        private Button deletePresentationTrackButton;
        private Button captureAnimationLengthButton;
        private FpgSkillPreviewView previewView;
        private FpgSkillTimelineView timelineView;

        [MenuItem("FPG Demo/Skill Editor", priority = 125)]
        public static void Open()
        {
            FpgSkillEditorWindow window = GetWindow<FpgSkillEditorWindow>();
            window.titleContent = new GUIContent("技能编辑器");
            window.minSize = new Vector2(1080f, 680f);
            window.Show();
        }

        public static void OpenAsset(UnityEngine.Object asset)
        {
            if (asset != null)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    SessionState.SetString(SelectedAssetSessionKey, path);
                }
            }

            Open();
            if (asset == null)
            {
                return;
            }

            FpgSkillEditorWindow window = GetWindow<FpgSkillEditorWindow>();
            EditorApplication.delayCall += () =>
            {
                if (window == null || asset == null)
                {
                    return;
                }

                window.RefreshAssetRecords();
                window.SelectAsset(asset, false);
                window.Focus();
            };
        }


        private void OnEnable()
        {
            EditorApplication.update += OnEditorTick;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorTick;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            isPlaying = false;
            tickAccumulator = 0d;
            previewExecution.Reset();
            previewSimulationFrame = null;
            previewView?.ClearPresentationPreview();
            previewView?.SetPreviewPrefab(null);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                LayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new HelpBox(
                    "无法加载技能编辑器 UXML。",
                    HelpBoxMessageType.Error));
                return;
            }

            layout.CloneTree(rootVisualElement);
            QueryElements();
            CreateCustomViews();
            ConfigureLists();
            ConfigureChoices();
            RegisterCallbacks();
            RestorePreviewPrefab();
            RefreshAssetRecords();
            RestoreAssetSelection();
            RefreshFromSerialized();
        }

        private void QueryElements()
        {
            assetSearchField = rootVisualElement.Q<ToolbarSearchField>(
                "asset-search-field");
            typeFilter = rootVisualElement.Q<DropdownField>("type-filter");
            actionAssetList = rootVisualElement.Q<ListView>("action-asset-list");
            sequenceDropdown = rootVisualElement.Q<DropdownField>("sequence-dropdown");
            previewPrefabField = rootVisualElement.Q<ObjectField>(
                "preview-prefab-field");
            targetCountDropdown = rootVisualElement.Q<DropdownField>(
                "target-count-dropdown");
            showGeometryToggle = rootVisualElement.Q<Toggle>("show-geometry-toggle");
            previewTickLabel = rootVisualElement.Q<Label>("preview-tick-label");
            animationLengthLabel = rootVisualElement.Q<Label>(
                "animation-length-label");
            speedDropdown = rootVisualElement.Q<DropdownField>("speed-dropdown");
            loopToggle = rootVisualElement.Q<Toggle>("loop-toggle");
            zoomSlider = rootVisualElement.Q<Slider>("zoom-slider");
            inspectorContent = rootVisualElement.Q<VisualElement>("inspector-content");
            inspectorTitle = rootVisualElement.Q<Label>("inspector-title");
            validationList = rootVisualElement.Q<ListView>("validation-list");
            validationSummaryLabel = rootVisualElement.Q<Label>(
                "validation-summary-label");
            eventLogList = rootVisualElement.Q<ListView>("event-log-list");
            assetStateLabel = rootVisualElement.Q<Label>("asset-state-label");
            statusLabel = rootVisualElement.Q<Label>("status-label");
            addEventButton = rootVisualElement.Q<Button>("add-event-button");
            duplicateEventButton = rootVisualElement.Q<Button>(
                "duplicate-event-button");
            copyEventsButton = rootVisualElement.Q<Button>("copy-events-button");
            pasteEventsButton = rootVisualElement.Q<Button>("paste-events-button");
            deleteEventButton = rootVisualElement.Q<Button>("delete-event-button");
            presentationTrackTools = rootVisualElement.Q<VisualElement>(
                "presentation-track-tools");
            presentationTrackDropdown = rootVisualElement.Q<DropdownField>(
                "presentation-track-dropdown");
            presentationTrackNameField = rootVisualElement.Q<TextField>(
                "presentation-track-name-field");
            addPresentationTrackButton = rootVisualElement.Q<Button>(
                "add-presentation-track-button");
            movePresentationTrackUpButton = rootVisualElement.Q<Button>(
                "move-presentation-track-up-button");
            movePresentationTrackDownButton = rootVisualElement.Q<Button>(
                "move-presentation-track-down-button");
            deletePresentationTrackButton = rootVisualElement.Q<Button>(
                "delete-presentation-track-button");
            captureAnimationLengthButton = rootVisualElement.Q<Button>(
                "capture-animation-length-button");
        }

        private void CreateCustomViews()
        {
            previewView = new FpgSkillPreviewView();
            previewView.AnimationDurationMeasured +=
                OnAnimationDurationMeasured;
            rootVisualElement.Q<VisualElement>("preview-host").Add(previewView);

            timelineView = new FpgSkillTimelineView();
            timelineView.PlayheadChanged += OnTimelinePlayheadChanged;
            timelineView.EventSelectionChanged +=
                OnTimelineEventSelectionChanged;
            timelineView.EventsTickDeltaChanged +=
                OnTimelineEventsTickDeltaChanged;
            timelineView.BlockRangeChanged +=
                OnTimelineBlockRangeChanged;
            timelineView.BlockSelected += OnTimelineBlockSelected;
            timelineView.EventOrderDeltaChanged +=
                OnTimelineEventOrderDeltaChanged;
            timelineView.EventCreateRequested +=
                OnTimelineEventCreateRequested;
            rootVisualElement.Q<VisualElement>("timeline-host").Add(timelineView);
        }

        private void ConfigureLists()
        {
            actionAssetList.itemsSource = filteredAssets;
            actionAssetList.fixedItemHeight = 44f;
            actionAssetList.makeItem = MakeAssetRow;
            actionAssetList.bindItem = BindAssetRow;
            actionAssetList.selectionChanged += OnAssetListSelectionChanged;

            validationList.itemsSource = validation;
            validationList.fixedItemHeight = 31f;
            validationList.makeItem = MakeValidationRow;
            validationList.bindItem = BindValidationRow;
            validationList.selectionChanged += OnValidationSelectionChanged;

            eventLogList.itemsSource = eventLog;
            eventLogList.fixedItemHeight = 29f;
            eventLogList.makeItem = MakeLogRow;
            eventLogList.bindItem = BindLogRow;
            eventLogList.selectionChanged += OnLogSelectionChanged;
        }

        private void ConfigureChoices()
        {
            previewPrefabField.objectType = typeof(GameObject);
            previewPrefabField.allowSceneObjects = false;
            typeFilter.choices = new List<string> { "全部", "角色", "怪物", "通用" };
            typeFilter.SetValueWithoutNotify("全部");

            speedDropdown.choices = new List<string>
                { "0.25x", "0.5x", "1x", "2x" };
            speedDropdown.SetValueWithoutNotify("1x");

            targetCountDropdown.choices = new List<string>
                { "1 个目标", "2 个目标", "3 个目标", "4 个目标" };
            targetCountDropdown.SetValueWithoutNotify("1 个目标");
        }

        private void RegisterCallbacks()
        {
            assetSearchField.RegisterValueChangedCallback(_ => ApplyAssetFilters());
            typeFilter.RegisterValueChangedCallback(_ => ApplyAssetFilters());
            sequenceDropdown.RegisterValueChangedCallback(OnSequenceChanged);
            previewPrefabField.RegisterValueChangedCallback(OnPreviewPrefabChanged);
            targetCountDropdown.RegisterValueChangedCallback(OnTargetCountChanged);
            showGeometryToggle.RegisterValueChangedCallback(evt =>
                previewView.SetGeometryVisible(evt.newValue));
            speedDropdown.RegisterValueChangedCallback(OnSpeedChanged);
            zoomSlider.RegisterValueChangedCallback(evt =>
                timelineView.SetZoom(evt.newValue));

            rootVisualElement.Q<Button>("refresh-assets-button").clicked +=
                RefreshAssetsAndPreviewSource;
            rootVisualElement.Q<Button>("use-selection-button").clicked +=
                UseProjectSelection;
            rootVisualElement.Q<Button>("undo-button").clicked += Undo.PerformUndo;
            rootVisualElement.Q<Button>("redo-button").clicked += Undo.PerformRedo;
            rootVisualElement.Q<Button>("play-button").clicked += Play;
            rootVisualElement.Q<Button>("pause-button").clicked += Pause;
            rootVisualElement.Q<Button>("step-back-button").clicked += () =>
                Step(-1);
            rootVisualElement.Q<Button>("step-forward-button").clicked += () =>
                Step(1);
            rootVisualElement.Q<Button>("clear-log-button").clicked += ClearLog;

            addEventButton.clicked += ShowAddEventMenu;
            duplicateEventButton.clicked += DuplicateEvent;
            copyEventsButton.clicked += CopySelectedEvents;
            pasteEventsButton.clicked += PasteCopiedEvents;
            deleteEventButton.clicked += DeleteEvent;
            presentationTrackDropdown.RegisterValueChangedCallback(
                OnPresentationTrackChanged);
            presentationTrackNameField.RegisterValueChangedCallback(
                OnPresentationTrackRenamed);
            addPresentationTrackButton.clicked += AddPresentationTrack;
            movePresentationTrackUpButton.clicked += () =>
                MovePresentationTrack(-1);
            movePresentationTrackDownButton.clicked += () =>
                MovePresentationTrack(1);
            deletePresentationTrackButton.clicked +=
                DeletePresentationTrack;
            captureAnimationLengthButton.clicked += CaptureAnimationSourceDuration;

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
        }

        private void RestorePreviewPrefab()
        {
            string key = GetPreviewPrefabSessionKey();
            string path = string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : SessionState.GetString(key, string.Empty);
            GameObject prefab = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            previewPrefabField.SetValueWithoutNotify(prefab);
            previewView.SetPreviewPrefab(prefab);
            measuredAnimationDurationTicks =
                previewView.MeasuredAnimationDurationTicks;
        }

        private void OnPreviewPrefabChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            GameObject prefab = evt.newValue as GameObject;
            string key = GetPreviewPrefabSessionKey();
            if (!string.IsNullOrWhiteSpace(key))
            {
                SessionState.SetString(
                    key,
                    prefab == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(prefab));
            }

            measuredAnimationDurationTicks = -1;
            previewView.SetPreviewPrefab(prefab);
            measuredAnimationDurationTicks =
                previewView.MeasuredAnimationDurationTicks;
            RefreshPreview();
            QueueSerializedRefresh();
        }

        private string GetPreviewPrefabSessionKey()
        {
            string assetPath = selectedAsset == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(selectedAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            return PreviewPrefabSessionKey + "."
                + (string.IsNullOrWhiteSpace(assetGuid)
                    ? assetPath.GetHashCode().ToString("X8")
                    : assetGuid);
        }


        private void RefreshAssetsAndPreviewSource()
        {
            previewView?.RefreshPreviewSource();
            measuredAnimationDurationTicks = previewView == null
                ? -1
                : previewView.MeasuredAnimationDurationTicks;
            RefreshAssetRecords();
        }

        private void RefreshAssetRecords()
        {
            string selectedPath = selectedAsset == null
                ? SessionState.GetString(SelectedAssetSessionKey, string.Empty)
                : AssetDatabase.GetAssetPath(selectedAsset);
            allAssets.Clear();
            allAssets.AddRange(FpgSkillSerializedAdapter.FindAssets());
            ApplyAssetFilters();

            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                FpgSkillAssetRecord record = allAssets.FirstOrDefault(candidate =>
                    string.Equals(candidate.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
                if (record != null)
                {
                    SelectAsset(record.Asset, false);
                }
            }

            statusLabel.text = allAssets.Count == 0
                ? "未发现具有 skillId、displayName、sequences 字段的动作资产。"
                : "已发现 " + allAssets.Count + " 个动作资产。";
        }

        private void ApplyAssetFilters()
        {
            string search = assetSearchField == null
                ? string.Empty
                : assetSearchField.value ?? string.Empty;
            string ownerFilter = typeFilter == null
                ? "全部"
                : typeFilter.value ?? "全部";
            filteredAssets.Clear();
            for (int index = 0; index < allAssets.Count; index++)
            {
                FpgSkillAssetRecord record = allAssets[index];
                if (!string.Equals(ownerFilter, "全部", StringComparison.Ordinal)
                    && !string.Equals(ownerFilter, record.OwnerType, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(search)
                    && record.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && record.SkillId.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && record.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                filteredAssets.Add(record);
            }

            actionAssetList?.Rebuild();
            SelectCurrentAssetInList();
        }

        private void RestoreAssetSelection()
        {
            string path = SessionState.GetString(SelectedAssetSessionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(path))
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (FpgSkillSerializedAdapter.IsCompatible(asset))
                {
                    SelectAsset(asset, false);
                    return;
                }
            }

            if (filteredAssets.Count > 0)
            {
                SelectAsset(filteredAssets[0].Asset, false);
            }
        }

        private void UseProjectSelection()
        {
            UnityEngine.Object asset = Selection.activeObject;
            if (!FpgSkillSerializedAdapter.IsCompatible(asset))
            {
                statusLabel.text = "当前选中对象不是可识别的技能时间轴资产。";
                return;
            }

            SelectAsset(asset, true);
        }

        private void SelectAsset(
            UnityEngine.Object asset,
            bool revealInProject)
        {
            if (!FpgSkillSerializedAdapter.IsCompatible(asset))
            {
                return;
            }

            Pause();
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string storedAssetPath = SessionState.GetString(
                SelectedAssetSessionKey,
                string.Empty);
            int storedSequenceIndex = string.Equals(
                storedAssetPath,
                assetPath,
                StringComparison.OrdinalIgnoreCase)
                ? SessionState.GetInt(SelectedSequenceIndexSessionKey, -1)
                : -1;
            selectedAsset = asset;
            serializedAsset = new SerializedObject(asset);
            previewCompileError = string.Empty;
            selectedSequenceIndex = ResolveSequenceSelection(
                storedSequenceIndex);

            selectedEventKey = FpgSkillEventKey.Invalid;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            currentTick = 0;
            tickAccumulator = 0d;
            SessionState.SetString(
                SelectedAssetSessionKey,
                assetPath);
            SaveSequenceSelection();
            RestorePreviewPrefab();
            if (revealInProject)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            SelectCurrentAssetInList();
            RefreshFromSerialized();
        }

        private int ResolveSequenceSelection(int storedSequenceIndex)
        {
            SerializedProperty sequences = FpgSkillSerializedAdapter.GetSequences(
                serializedAsset);
            if (sequences == null || !sequences.isArray || sequences.arraySize == 0)
            {
                return -1;
            }

            if (storedSequenceIndex >= 0
                && storedSequenceIndex < sequences.arraySize)
            {
                return storedSequenceIndex;
            }

            int firstEventSequence = -1;
            int executeEventSequence = -1;
            int releaseEventSequence = -1;
            for (int index = 0; index < sequences.arraySize; index++)
            {
                SerializedProperty sequence = sequences.GetArrayElementAtIndex(index);
                List<FpgSkillEventRecord> sequenceEvents =
                    FpgSkillSerializedAdapter.ReadEvents(
                        sequence,
                        FpgSkillSerializedAdapter.GetDurationTicks(sequence));
                if (sequenceEvents.Count == 0)
                {
                    continue;
                }

                if (firstEventSequence < 0)
                {
                    firstEventSequence = index;
                }

                SerializedProperty kind = sequence.FindPropertyRelative("kind");
                if (kind == null
                    || kind.propertyType != SerializedPropertyType.Enum)
                {
                    continue;
                }

                if (kind.enumValueIndex == (int)FpgSkillSequenceKind.Release)
                {
                    releaseEventSequence = index;
                }
                else if (kind.enumValueIndex == (int)FpgSkillSequenceKind.Execute)
                {
                    executeEventSequence = index;
                }
            }

            bool isChargeReleaseSkill = string.Equals(
                GetSerializedEnumName(
                    serializedAsset.FindProperty("secondaryTriggerMode")),
                "ChargeRelease",
                StringComparison.Ordinal);

            return isChargeReleaseSkill && releaseEventSequence >= 0
                ? releaseEventSequence
                : executeEventSequence >= 0
                    ? executeEventSequence
                    : firstEventSequence >= 0
                        ? firstEventSequence
                        : 0;
        }

        private void SaveSequenceSelection()
        {
            if (selectedAsset != null && selectedSequenceIndex >= 0)
            {
                SessionState.SetInt(
                    SelectedSequenceIndexSessionKey,
                    selectedSequenceIndex);
            }
        }

        private void SelectCurrentAssetInList()
        {
            if (actionAssetList == null || selectedAsset == null)
            {
                return;
            }

            int index = filteredAssets.FindIndex(record => record.Asset == selectedAsset);
            if (index >= 0 && actionAssetList.selectedIndex != index)
            {
                actionAssetList.SetSelectionWithoutNotify(new[] { index });
            }
        }

        private void RefreshFromSerialized()
        {
            RefreshFromSerialized(true);
        }

        private void RefreshFromSerialized(bool refreshInspector)
        {
            refreshQueued = false;
            if (serializedAsset == null || serializedAsset.targetObject == null)
            {
                ClearSelectedData();
                return;
            }

            previewView?.ClearPresentationPreview();
            serializedAsset.UpdateIfRequiredOrScript();
            RefreshSequenceChoices();
            SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                serializedAsset,
                selectedSequenceIndex);
            durationTicks = FpgSkillSerializedAdapter.GetDurationTicks(sequence);
            currentTick = Mathf.Clamp(currentTick, 0, durationTicks);

            presentationTracks.Clear();
            presentationTracks.AddRange(
                FpgSkillSerializedAdapter.ReadActivePresentationTracks(
                    sequence));
            selectedPresentationTrackIndex = presentationTracks.Count == 0
                ? -1
                : Mathf.Clamp(
                    selectedPresentationTrackIndex,
                    0,
                    presentationTracks.Count - 1);

            events.Clear();
            events.AddRange(FpgSkillSerializedAdapter.ReadEvents(
                sequence,
                durationTicks));
            compiledTriggers.Clear();
            hasCompiledSchedule = FpgSkillSerializedAdapter.TryReadCompiledSchedule(
                serializedAsset,
                selectedSequenceIndex,
                events,
                compiledTriggers,
                out compiledSequence,
                out string compileError);
            if (hasCompiledSchedule
                && (!previewExecution.Bind(
                        compiledSequence,
                        out string executionError)
                    || !previewExecution.AdvanceTo(
                        currentTick,
                        out executionError)))
            {
                hasCompiledSchedule = false;
                compileError = executionError;
            }
            else if (!hasCompiledSchedule)
            {
                previewExecution.Reset();
            }
            else
            {
                previewExecution.ClearPendingResults();
            }

            previewView.SetAnimation(
                FpgSkillSerializedAdapter.GetMainAnimation(sequence),
                compiledSequence);
            measuredAnimationDurationTicks =
                previewView.MeasuredAnimationDurationTicks;
            validation.Clear();
            validation.AddRange(FpgSkillSerializedAdapter.Validate(
                serializedAsset,
                selectedSequenceIndex,
                events,
                durationTicks,
                measuredAnimationDurationTicks,
                previewPrefabField.value as GameObject,
                includeRuntimeValidation: false));
            previewCompileError = hasCompiledSchedule
                ? string.Empty
                : compileError;
            ReportPreviewFailureToConsole(previewCompileError);

            if (sequence == null)
            {
                selectedAnimationTrack = false;
            }
            selectedEventKey = NormalizeEventSelection(selectedEventKey);
            HashSet<FpgSkillEventKey> validEventKeys =
                new HashSet<FpgSkillEventKey>(events.Select(item => item.Key));
            eventSelection.Retain(validEventKeys);
            if (selectedEventKey.IsValid
                && !eventSelection.Contains(selectedEventKey))
            {
                eventSelection.SetSingle(selectedEventKey);
            }
            else
            {
                eventSelection.MakePrimary(selectedEventKey);
            }
            RefreshPresentationTrackControls();
            validationList.Rebuild();
            RefreshValidationSummary();
            RefreshAnimationLengthState(sequence);
            RefreshTimeline();
            RefreshPreview();
            if (refreshInspector)
            {
                RefreshInspector();
            }
            RefreshButtons();
            RefreshAssetState();
        }

        private void RefreshSequenceChoices()
        {
            SerializedProperty sequences = FpgSkillSerializedAdapter.GetSequences(
                serializedAsset);
            List<string> choices = new List<string>();
            int count = sequences == null || !sequences.isArray ? 0 : sequences.arraySize;
            for (int index = 0; index < count; index++)
            {
                string label = FpgSkillSerializedAdapter.GetSequenceLabel(
                    sequences.GetArrayElementAtIndex(index),
                    index);
                if (choices.Contains(label))
                {
                    label += " (" + (index + 1) + ")";
                }

                choices.Add(label);
            }

            sequenceDropdown.choices = choices;
            selectedSequenceIndex = count == 0
                ? -1
                : Mathf.Clamp(selectedSequenceIndex, 0, count - 1);
            sequenceDropdown.SetValueWithoutNotify(selectedSequenceIndex >= 0
                ? choices[selectedSequenceIndex]
                : string.Empty);
            sequenceDropdown.SetEnabled(count > 0);
        }

        private void RefreshTimeline()
        {
            List<FpgSkillTimelineEventViewModel> models =
                new List<FpgSkillTimelineEventViewModel>(events.Count);
            for (int index = 0; index < events.Count; index++)
            {
                models.Add(events[index].ToViewModel());
            }

            models.Sort((left, right) =>
            {
                int tickComparison = left.Tick.CompareTo(right.Tick);
                return tickComparison != 0
                    ? tickComparison
                    : left.AuthoredOrdinal.CompareTo(right.AuthoredOrdinal);
            });

            SerializedProperty sequence =
                FpgSkillSerializedAdapter.GetSequence(
                    serializedAsset,
                    selectedSequenceIndex);
            List<FpgSkillTimelineBlockViewModel> blocks =
                FpgSkillSerializedAdapter.ReadTimelineBlocks(
                    sequence,
                    durationTicks,
                    measuredAnimationDurationTicks);
            List<FpgSkillEventTrackKind> availableTracks =
                new List<FpgSkillEventTrackKind>
                {
                    FpgSkillEventTrackKind.GameplayAction
                };
            if (FpgSkillSerializedAdapter.CanAddEventTrack(
                    sequence,
                    FpgSkillEventTrackKind.Warning))
            {
                availableTracks.Add(FpgSkillEventTrackKind.Warning);
            }

            List<FpgSkillTimelinePresentationTrackViewModel>
                presentationTrackModels =
                    new List<FpgSkillTimelinePresentationTrackViewModel>(
                        presentationTracks.Count);
            for (int index = 0; index < presentationTracks.Count; index++)
            {
                FpgSkillActivePresentationTrackRecord track =
                    presentationTracks[index];
                presentationTrackModels.Add(
                    new FpgSkillTimelinePresentationTrackViewModel
                    {
                        Index = track.Index,
                        Label = track.Name
                    });
            }

            timelineView.SetAvailableTracks(availableTracks);
            timelineView.SetPresentationTracks(presentationTrackModels);
            timelineView.SetModel(durationTicks, models, blocks);
            timelineView.SetPlayhead(currentTick);
            if (selectedAnimationTrack)
            {
                timelineView.SelectBlock(
                    FpgSkillTimelineBlockKind.Animation,
                    0);
            }
            else
            {
                timelineView.SelectEvents(
                    eventSelection.Items,
                    eventSelection.PrimaryEventKey);
                if (eventSelection.Count == 0)
                {
                    timelineView.SelectBlock(
                        FpgSkillTimelineBlockKind.Animation,
                        -1);
                }
            }
        }

        private void RefreshPreview()
        {
            List<FpgSkillTimelineEventViewModel> active =
                new List<FpgSkillTimelineEventViewModel>();
            for (int index = 0; index < events.Count; index++)
            {
                FpgSkillTimelineEventViewModel model =
                    events[index].ToViewModel();
                if (model.IsActiveAt(currentTick))
                {
                    active.Add(model);
                }
            }

            active.Sort((left, right) =>
                left.AuthoredOrdinal.CompareTo(right.AuthoredOrdinal));

            previewSimulationFrame = hasCompiledSchedule
                ? FpgSkillPreviewSimulator.Evaluate(
                    compiledSequence,
                    currentTick,
                    compiledTriggers,
                    events,
                    previewView)
                : new FpgSkillPreviewSimulationFrame(currentTick);
            previewView.SetTickState(
                currentTick,
                active,
                previewSimulationFrame);
            previewTickLabel.text = string.Format(
                "Tick {0} / {1:0.000} 秒",
                currentTick,
                currentTick / (float)TickRate);
        }

        private void RefreshInspector()
        {
            inspectorContent.Unbind();
            inspectorContent.Clear();
            if (serializedAsset == null || selectedAsset == null)
            {
                inspectorTitle.text = "Inspector";
                AddInspectorEmptyState("请选择动作资产。");
                return;
            }

            SerializedProperty property;
            if (selectedAnimationTrack)
            {
                SerializedProperty sequence =
                    FpgSkillSerializedAdapter.GetSequence(
                        serializedAsset,
                        selectedSequenceIndex);
                inspectorTitle.text = "动画片段 Inspector";
                AddAnimationInspector(sequence);
                return;
            }

            if (selectedEventKey.IsValid)
            {
                property = FpgSkillSerializedAdapter.GetEventProperty(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedEventKey);
                FpgSkillEventRecord record = events.FirstOrDefault(item =>
                    item.Key == selectedEventKey);
                inspectorTitle.text = record == null
                    ? "事件 Inspector"
                    : record.Kind + " Inspector";
                AddEventInspector(property, record);
                return;
            }

            inspectorTitle.text = "载荷 Inspector";
            inspectorTitle.text = "动作 Inspector";
            AddTypedProperty(
                serializedAsset.FindProperty("displayName"),
                "显示名称");
            AddAdditionalAssetProperties();

            SerializedProperty currentSequence =
                FpgSkillSerializedAdapter.GetSequence(
                    serializedAsset,
                    selectedSequenceIndex);
            AddTypedProperty(
                currentSequence?.FindPropertyRelative("kind"),
                "序列类型");
            AddTypedProperty(
                currentSequence?.FindPropertyRelative("durationTicks"),
                "总时长 Tick");
        }

        private void AddAdditionalAssetProperties()
        {
            SerializedProperty iterator = serializedAsset.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.depth != 0
                    || IsEditorOwnedRootProperty(iterator.name)
                    || iterator.propertyType == SerializedPropertyType.Generic
                    || iterator.isArray
                    || !IsAdditionalAssetPropertyApplicable(iterator.name))
                {
                    continue;
                }

                AddTypedProperty(iterator.Copy(), iterator.displayName);
            }
        }

        private bool IsAdditionalAssetPropertyApplicable(
            string propertyName)
        {
            if (!string.Equals(
                    propertyName,
                    "minimumChargeTicks",
                    StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(
                GetSerializedEnumName(
                    serializedAsset.FindProperty("secondaryTriggerMode")),
                "ChargeRelease",
                StringComparison.Ordinal);
        }

        private static bool IsEditorOwnedRootProperty(string propertyName)
        {
            switch (propertyName)
            {
                case "m_Script":
                case "skillId":
                case "displayName":
                case "sequences":
                    return true;
                default:
                    return false;
            }
        }

        private void AddBoundProperty(SerializedProperty property, string label)
        {
            if (property == null)
            {
                return;
            }

            PropertyField field = new PropertyField(property.Copy(), label);
            field.RegisterCallback<SerializedPropertyChangeEvent>(_ => QueueSerializedRefresh());
            inspectorContent.Add(field);
            field.Bind(serializedAsset);
        }

        private void AddInspectorEmptyState(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("empty-state");
            inspectorContent.Add(label);
        }

        private void RefreshValidationSummary()
        {
            int errors = validation.Count(item =>
                item.Severity == FpgSkillIssueSeverity.Error);
            int warnings = validation.Count(item =>
                item.Severity == FpgSkillIssueSeverity.Warning);
            validationSummaryLabel.text = errors + " 错误 / " + warnings + " 警告";
        }

        private void ReportPreviewFailureToConsole(string failure = null)
        {
            List<string> messages = new List<string>();
            if (!string.IsNullOrWhiteSpace(failure))
            {
                messages.Add(failure);
            }

            for (int index = 0; index < validation.Count; index++)
            {
                FpgSkillValidationItem item = validation[index];
                if (item.Severity != FpgSkillIssueSeverity.Error
                    || string.IsNullOrWhiteSpace(item.Message)
                    || messages.Contains(item.Message))
                {
                    continue;
                }

                messages.Add(item.Message);
            }

            if (messages.Count == 0)
            {
                lastReportedPreviewFailureSignature = null;
                return;
            }

            string assetPath = selectedAsset == null
                ? "<no asset>"
                : AssetDatabase.GetAssetPath(selectedAsset);
            string message = string.Join(" | ", messages)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            string signature = (selectedAsset == null
                    ? "null"
                    : selectedAsset.GetInstanceID().ToString())
                + "|" + selectedSequenceIndex + "|" + message;
            if (string.Equals(
                    lastReportedPreviewFailureSignature,
                    signature,
                    StringComparison.Ordinal))
            {
                return;
            }

            lastReportedPreviewFailureSignature = signature;
            Debug.LogError(
                "[FPG Skill Preview] " + assetPath
                + " | sequence " + (selectedSequenceIndex + 1)
                + ": " + message,
                selectedAsset);
        }

        private void RefreshAssetState()
        {
            if (selectedAsset == null)
            {
                assetStateLabel.text = "未选择动作";
                return;
            }

            bool hasErrors = !hasCompiledSchedule || validation.Any(item =>
                item.Severity == FpgSkillIssueSeverity.Error);
            bool hasWarnings = validation.Any(item =>
                item.Severity == FpgSkillIssueSeverity.Warning);
            assetStateLabel.text = hasErrors
                ? "已阻塞"
                : hasWarnings
                    ? "有警告"
                    : "可运行";
            statusLabel.text = selectedAsset.name
                + " · 序列 " + (selectedSequenceIndex + 1)
                + " · " + durationTicks + " Tick";
        }

        private void RefreshButtons()
        {
            bool hasSequence = serializedAsset != null && selectedSequenceIndex >= 0;
            SerializedProperty sequence = hasSequence
                ? FpgSkillSerializedAdapter.GetSequence(
                    serializedAsset,
                    selectedSequenceIndex)
                : null;
            addEventButton.SetEnabled(hasSequence
                && (FpgSkillSerializedAdapter.CanAddEventTrack(
                        sequence,
                        FpgSkillEventTrackKind.GameplayAction)
                    || FpgSkillSerializedAdapter
                        .GetActivePresentationTracks(sequence) != null
                    || FpgSkillSerializedAdapter.CanAddEventTrack(
                        sequence,
                        FpgSkillEventTrackKind.Warning)));
            duplicateEventButton.SetEnabled(selectedEventKey.IsValid);
            copyEventsButton.SetEnabled(selectedEventKey.IsValid);
            pasteEventsButton.SetEnabled(hasSequence && !eventClipboard.IsEmpty);
            deleteEventButton.SetEnabled(selectedEventKey.IsValid);
            RefreshPresentationTrackControls();
            captureAnimationLengthButton.SetEnabled(
                hasSequence && measuredAnimationDurationTicks > 0);
        }

        private void RefreshPresentationTrackControls()
        {
            if (presentationTrackTools == null)
            {
                return;
            }

            bool visible = serializedAsset != null
                && selectedSequenceIndex >= 0;
            presentationTrackTools.EnableInClassList(
                "presentation-track-tools--hidden",
                !visible);
            if (!visible)
            {
                return;
            }

            List<string> choices = new List<string>(
                presentationTracks.Count);
            for (int index = 0; index < presentationTracks.Count; index++)
            {
                FpgSkillActivePresentationTrackRecord track =
                    presentationTracks[index];
                choices.Add((index + 1) + ". " + track.Name
                    + " (" + track.EventCount + ")");
            }

            selectedPresentationTrackIndex = choices.Count == 0
                ? -1
                : Mathf.Clamp(
                    selectedPresentationTrackIndex,
                    0,
                    choices.Count - 1);
            presentationTrackDropdown.choices = choices;
            presentationTrackDropdown.SetValueWithoutNotify(
                selectedPresentationTrackIndex >= 0
                    ? choices[selectedPresentationTrackIndex]
                    : string.Empty);
            presentationTrackDropdown.SetEnabled(choices.Count > 0);

            FpgSkillActivePresentationTrackRecord selectedTrack =
                selectedPresentationTrackIndex >= 0
                && selectedPresentationTrackIndex < presentationTracks.Count
                    ? presentationTracks[selectedPresentationTrackIndex]
                    : null;
            presentationTrackNameField.SetValueWithoutNotify(
                selectedTrack?.Name ?? string.Empty);
            presentationTrackNameField.SetEnabled(selectedTrack != null);
            addPresentationTrackButton.SetEnabled(true);
            movePresentationTrackUpButton.SetEnabled(
                selectedPresentationTrackIndex > 0);
            movePresentationTrackDownButton.SetEnabled(
                selectedPresentationTrackIndex >= 0
                && selectedPresentationTrackIndex
                    < presentationTracks.Count - 1);
            bool canDelete = selectedTrack != null
                && selectedTrack.EventCount == 0;
            deletePresentationTrackButton.SetEnabled(canDelete);
            deletePresentationTrackButton.tooltip = canDelete
                ? "删除空表现轨道"
                : "只有空表现轨道可以删除";
        }

        private void OnPresentationTrackChanged(ChangeEvent<string> evt)
        {
            int index = presentationTrackDropdown.choices.IndexOf(
                evt.newValue);
            if (index < 0 || index == selectedPresentationTrackIndex)
            {
                return;
            }

            selectedPresentationTrackIndex = index;
            RefreshPresentationTrackControls();
        }

        private void OnPresentationTrackRenamed(ChangeEvent<string> evt)
        {
            if (!FpgSkillSerializedAdapter.RenameActivePresentationTrack(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedPresentationTrackIndex,
                    evt.newValue))
            {
                RefreshPresentationTrackControls();
                return;
            }

            RefreshFromSerialized(false);
        }

        private void AddPresentationTrack()
        {
            int index = FpgSkillSerializedAdapter.AddActivePresentationTrack(
                serializedAsset,
                selectedSequenceIndex);
            if (index < 0)
            {
                statusLabel.text = "当前序列不支持表现轨道。";
                return;
            }

            selectedPresentationTrackIndex = index;
            RefreshFromSerialized(false);
        }

        private void MovePresentationTrack(int delta)
        {
            if (!FpgSkillSerializedAdapter.MoveActivePresentationTrack(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedPresentationTrackIndex,
                    delta,
                    out int movedTrackIndex))
            {
                return;
            }

            selectedPresentationTrackIndex = movedTrackIndex;
            selectedEventKey = FpgSkillEventKey.Invalid;
            eventSelection.Clear();
            RefreshFromSerialized();
        }

        private void DeletePresentationTrack()
        {
            if (!FpgSkillSerializedAdapter.DeleteActivePresentationTrack(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedPresentationTrackIndex))
            {
                statusLabel.text = "只有空表现轨道可以删除。";
                return;
            }

            selectedPresentationTrackIndex--;
            selectedEventKey = FpgSkillEventKey.Invalid;
            eventSelection.Clear();
            RefreshFromSerialized();
        }

        private void ClearSelectedData()
        {
            previewView?.ClearPresentationPreview();
            selectedAsset = null;
            serializedAsset = null;
            selectedSequenceIndex = -1;

            selectedPresentationTrackIndex = -1;
            selectedEventKey = FpgSkillEventKey.Invalid;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            durationTicks = 120;
            currentTick = 0;

            presentationTracks.Clear();
            events.Clear();
            compiledTriggers.Clear();
            hasCompiledSchedule = false;
            compiledSequence = default(FpgCompiledSkillSequence);
            previewCompileError = string.Empty;
            lastReportedPreviewFailureSignature = null;
            previewExecution.Reset();
            previewSimulationFrame = null;
            validation.Clear();
            RefreshAnimationLengthState(null);

            validationList?.Rebuild();
            timelineView?.SetModel(
                durationTicks,
                Array.Empty<FpgSkillTimelineEventViewModel>(),
                Array.Empty<FpgSkillTimelineBlockViewModel>());
            timelineView?.SetPresentationTracks(
                Array.Empty<FpgSkillTimelinePresentationTrackViewModel>());
            previewView?.SetAnimation(
                string.Empty,
                default(FpgCompiledSkillSequence));
            previewView?.SetTickState(0, null);
            RefreshInspector();
            RefreshButtons();
            RefreshAssetState();
        }

        private void QueueSerializedRefresh()
        {
            if (refreshQueued)
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += RefreshFromSerialized;
        }

        private void OnAnimationDurationMeasured(int duration)
        {
            if (measuredAnimationDurationTicks == duration)
            {
                return;
            }

            measuredAnimationDurationTicks = duration;
            QueueSerializedRefresh();
        }

        private void CaptureAnimationSourceDuration()
        {
            if (serializedAsset == null
                || selectedSequenceIndex < 0
                || measuredAnimationDurationTicks <= 0)
            {
                return;
            }

            if (!FpgSkillSerializedAdapter.SetAnimationSourceDurationTicks(
                    serializedAsset,
                    selectedSequenceIndex,
                    measuredAnimationDurationTicks))
            {
                statusLabel.text = "无法记录当前源动画长度。";
                return;
            }

            int capturedTicks = measuredAnimationDurationTicks;
            RefreshFromSerialized();
            statusLabel.text = "已记录源动画长度基准："
                + capturedTicks + " Tick。逻辑事件位置未改变。";
        }

        private void RefreshAnimationLengthState(SerializedProperty sequence)
        {
            if (animationLengthLabel == null)
            {
                return;
            }

            int baseline = FpgSkillSerializedAdapter
                .GetAnimationSourceDurationTicks(sequence);
            bool warning = false;
            if (sequence == null)
            {
                animationLengthLabel.text = "源长：未选择序列";
            }
            else if (measuredAnimationDurationTicks <= 0)
            {
                animationLengthLabel.text = baseline > 0
                    ? "源长：基准 " + baseline + " / 未测量"
                    : "源长：未测量";
            }
            else if (baseline <= 0)
            {
                warning = true;
                animationLengthLabel.text = "源长：未记录 / 实测 "
                    + measuredAnimationDurationTicks;
            }
            else if (baseline != measuredAnimationDurationTicks)
            {
                warning = true;
                animationLengthLabel.text = "源长：基准 " + baseline
                    + " / 实测 " + measuredAnimationDurationTicks;
            }
            else
            {
                animationLengthLabel.text = "源长："
                    + measuredAnimationDurationTicks + " Tick";
            }

            animationLengthLabel.EnableInClassList(
                "severity-warning",
                warning);
        }

        private void OnSequenceChanged(ChangeEvent<string> evt)
        {
            int index = sequenceDropdown.choices.IndexOf(evt.newValue);
            if (index < 0 || index == selectedSequenceIndex)
            {
                return;
            }

            Pause();
            selectedSequenceIndex = index;
            SaveSequenceSelection();

            selectedPresentationTrackIndex = -1;
            selectedEventKey = FpgSkillEventKey.Invalid;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            currentTick = 0;
            tickAccumulator = 0d;
            RefreshFromSerialized();
        }

        private void OnTargetCountChanged(ChangeEvent<string> evt)
        {
            int index = targetCountDropdown.choices.IndexOf(evt.newValue);
            targetCount = Mathf.Clamp(index + 1, 1, 4);
            previewView.SetTargetCount(targetCount);
            RefreshPreview();
        }

        private void OnSpeedChanged(ChangeEvent<string> evt)
        {
            switch (evt.newValue)
            {
                case "0.25x":
                    playbackSpeed = 0.25f;
                    break;
                case "0.5x":
                    playbackSpeed = 0.5f;
                    break;
                case "2x":
                    playbackSpeed = 2f;
                    break;
                default:
                    playbackSpeed = 1f;
                    break;
            }
        }

        private void OnAssetListSelectionChanged(IEnumerable<object> selection)
        {
            FpgSkillAssetRecord record = selection.OfType<FpgSkillAssetRecord>().FirstOrDefault();
            if (record != null && record.Asset != selectedAsset)
            {
                SelectAsset(record.Asset, false);
            }
        }

        private void OnValidationSelectionChanged(IEnumerable<object> selection)
        {
            FpgSkillValidationItem item = selection
                .OfType<FpgSkillValidationItem>()
                .FirstOrDefault();
            if (item == null)
            {
                return;
            }

            if (item.EventKey.IsValid)
            {
                SelectEvent(item.EventKey, true);
            }
            if (item.Tick >= 0)
            {
                SetCurrentTick(item.Tick, false, true);
            }
        }

        private void OnLogSelectionChanged(IEnumerable<object> selection)
        {
            FpgSkillLogEntry item = selection.OfType<FpgSkillLogEntry>().FirstOrDefault();
            if (item == null)
            {
                return;
            }

            SetCurrentTick(item.Tick, false, true);
            if (item.EventKey.IsValid)
            {
                SelectEvent(item.EventKey, false);
            }
        }

        private void OnTimelinePlayheadChanged(int tick)
        {
            Pause();
            SetCurrentTick(tick, true, false);
        }

        private void OnTimelineEventSelectionChanged(
            IReadOnlyList<FpgSkillEventKey> eventKeys)
        {
            eventSelection.Set(
                eventKeys,
                timelineView.SelectedEventKey);
            selectedEventKey = NormalizeEventSelection(
                eventSelection.PrimaryEventKey);

            selectedAnimationTrack = false;
            if (selectedEventKey.IsValid)
            {
                FpgSkillEventRecord record = events.FirstOrDefault(item =>
                    item.Key == selectedEventKey);
                if (record != null)
                {
                    if (record.PresentationTrackIndex >= 0)
                    {
                        selectedPresentationTrackIndex =
                            record.PresentationTrackIndex;
                    }

                    SetCurrentTick(record.Tick, false, false);
                }
            }

            RefreshInspector();
            RefreshButtons();
        }

        private void OnTimelineEventsTickDeltaChanged(
            IReadOnlyList<FpgSkillEventKey> eventKeys,
            int requestedDeltaTicks)
        {
            if (serializedAsset == null
                || !FpgSkillSerializedAdapter.MoveEventsByDelta(
                    serializedAsset,
                    selectedSequenceIndex,
                    eventKeys,
                    requestedDeltaTicks,
                    out _))
            {
                RefreshTimeline();
                return;
            }

            currentTick = Mathf.Clamp(
                timelineView.PlayheadTick,
                0,
                durationTicks);
            eventSelection.Set(
                eventKeys,
                timelineView.SelectedEventKey);
            selectedEventKey = eventSelection.PrimaryEventKey;
            RefreshFromSerialized();
        }

        private void OnTimelineBlockRangeChanged(
            FpgSkillTimelineBlockKind kind,
            int index,
            FpgSkillTimelineBlockEditMode editMode,
            int requestedStartTick,
            int requestedEndTick)
        {
            CommitTimelineBlockRange(
                kind,
                index,
                editMode,
                requestedStartTick,
                requestedEndTick);
        }

        private void CommitTimelineBlockRange(
            FpgSkillTimelineBlockKind kind,
            int index,
            FpgSkillTimelineBlockEditMode editMode,
            int requestedStartTick,
            int requestedEndTick,
            int requestedFocusTick = -1)
        {
            if (serializedAsset == null
                || !FpgSkillSerializedAdapter.EditTimelineBlockRange(
                    serializedAsset,
                    selectedSequenceIndex,
                    kind,
                    index,
                    editMode,
                    requestedStartTick,
                    requestedEndTick,
                    out int appliedStartTick,
                    out int appliedEndTick))
            {
                RefreshTimeline();
                RefreshInspector();
                return;
            }

            int maximumTick = Mathf.Max(durationTicks, appliedEndTick);
            int focusTick = requestedFocusTick >= 0
                ? Mathf.Min(requestedFocusTick, maximumTick)
                : editMode == FpgSkillTimelineBlockEditMode.ResizeEnd
                    ? appliedEndTick
                    : appliedStartTick;
            currentTick = Mathf.Clamp(focusTick, 0, maximumTick);
            selectedAnimationTrack =
                kind == FpgSkillTimelineBlockKind.Animation;
            selectedEventKey = FpgSkillEventKey.Invalid;

            eventSelection.Clear();
            RefreshFromSerialized();
        }


        private void OnTimelineEventOrderDeltaChanged(
            FpgSkillEventKey eventKey,
            int requestedDelta)
        {
            if (serializedAsset == null
                || !FpgSkillSerializedAdapter.MoveEventOrder(
                    serializedAsset,
                    selectedSequenceIndex,
                    eventKey,
                    requestedDelta))
            {
                RefreshTimeline();
                return;
            }

            selectedEventKey = eventKey;

            selectedAnimationTrack = false;
            eventSelection.SetSingle(eventKey);
            RefreshFromSerialized();
        }

        private void OnTimelineBlockSelected(
            FpgSkillTimelineBlockKind kind,
            int index)
        {
            Pause();
            selectedEventKey = FpgSkillEventKey.Invalid;

            eventSelection.Clear();
            selectedAnimationTrack =
                kind == FpgSkillTimelineBlockKind.Animation;
            RefreshInspector();
            RefreshButtons();
        }


        private void OnTimelineEventCreateRequested(
            FpgSkillTimelineCreateRequest request)
        {
            if (serializedAsset == null)
            {
                return;
            }

            currentTick = request.Tick;
            if (request.Track == FpgSkillEventTrackKind.GameplayAction)
            {
                GenericMenu actionMenu = new GenericMenu();
                BuildActionAddMenu(actionMenu, request.Tick);
                actionMenu.ShowAsContext();
                return;
            }

            if (request.PresentationTrackIndex >= 0)
            {
                selectedPresentationTrackIndex =
                    request.PresentationTrackIndex;
                RefreshPresentationTrackControls();
                GenericMenu presentationMenu = new GenericMenu();
                BuildActivePresentationAddMenu(
                    presentationMenu,
                    request.PresentationTrackIndex,
                    request.Tick,
                    string.Empty);
                presentationMenu.ShowAsContext();
                return;
            }

            if (request.Track != FpgSkillEventTrackKind.Warning)
            {
                return;
            }

            FpgSkillEventKey eventKey = FpgSkillSerializedAdapter.AddEvent(
                serializedAsset,
                selectedSequenceIndex,
                request.Tick,
                request.Track,
                request.DurationTicks);
            if (!eventKey.IsValid)
            {
                statusLabel.text = "当前轨道不能创建事件。";
                return;
            }

            eventSelection.SetSingle(eventKey);
            selectedEventKey = eventKey;
            currentTick = request.Tick;
            RefreshFromSerialized();
            SelectEvent(eventKey, true);
        }

        private void SelectEvent(FpgSkillEventKey eventKey, bool frame)
        {
            selectedEventKey = NormalizeEventSelection(eventKey);

            selectedAnimationTrack = false;
            eventSelection.SetSingle(selectedEventKey);
            timelineView.SelectEvents(
                eventSelection.Items,
                eventSelection.PrimaryEventKey);
            if (selectedEventKey.IsValid)
            {
                FpgSkillEventRecord record = events.First(item =>
                    item.Key == selectedEventKey);
                if (record.PresentationTrackIndex >= 0)
                {
                    selectedPresentationTrackIndex =
                        record.PresentationTrackIndex;
                    RefreshPresentationTrackControls();
                }

                SetCurrentTick(record.Tick, false, frame);
            }

            RefreshInspector();
            RefreshButtons();
        }

        private void ShowAddEventMenu()
        {
            if (serializedAsset == null)
            {
                return;
            }

            SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                serializedAsset,
                selectedSequenceIndex);
            GenericMenu menu = new GenericMenu();
            BuildActionAddMenu(menu, currentTick);
            menu.AddSeparator(string.Empty);
            BuildActivePresentationAddMenu(
                menu,
                selectedPresentationTrackIndex,
                currentTick,
                "主动表现/");
            AddEventMenuItem(
                menu,
                "预警区间",
                sequence,
                FpgSkillEventTrackKind.Warning);
            menu.DropDown(addEventButton.worldBound);
        }

        private void BuildActionAddMenu(
            GenericMenu menu,
            int tick)
        {
            bool enemy = selectedAsset != null
                && selectedAsset.GetType().Name.IndexOf(
                    "Enemy",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            AddTypedActionMenuItem(
                menu,
                "玩法动作/射线攻击",
                FpgSkillActionKind.Attack,
                1,
                !enemy,
                tick);
            AddTypedActionMenuItem(
                menu,
                "玩法动作/范围攻击",
                FpgSkillActionKind.Attack,
                2,
                !enemy,
                tick);
            AddTypedActionMenuItem(
                menu,
                "玩法动作/指定目标攻击",
                FpgSkillActionKind.Attack,
                3,
                enemy,
                tick);
            AddTypedActionMenuItem(
                menu,
                "玩法动作/发射投射物",
                FpgSkillActionKind.LaunchProjectile,
                enemy ? 2 : 1,
                true,
                tick);
            AddTypedActionMenuItem(
                menu,
                "玩法动作/完成换弹",
                FpgSkillActionKind.CommitReload,
                0,
                !enemy,
                tick);
            AddTypedActionMenuItem(
                menu,
                "玩法动作/召唤单位",
                FpgSkillActionKind.SummonActors,
                0,
                enemy,
                tick);
        }

        private void AddTypedActionMenuItem(
            GenericMenu menu,
            string label,
            FpgSkillActionKind actionKind,
            int modeValue,
            bool enabled,
            int tick)
        {
            GUIContent content = new GUIContent(label);
            if (enabled)
            {
                menu.AddItem(
                    content,
                    false,
                    () => AddAction(actionKind, modeValue, tick));
            }
            else
            {
                menu.AddDisabledItem(content);
            }
        }

        private void BuildActivePresentationAddMenu(
            GenericMenu menu,
            int presentationTrackIndex,
            int tick,
            string prefix)
        {
            string menuPrefix = prefix ?? string.Empty;
            if (presentationTrackIndex < 0
                || presentationTrackIndex >= presentationTracks.Count)
            {
                menu.AddDisabledItem(new GUIContent(
                    menuPrefix + "请先添加表现轨道"));
                return;
            }

            menu.AddItem(
                new GUIContent(menuPrefix + "特效"),
                false,
                () => AddActivePresentationEvent(
                    presentationTrackIndex,
                    FpgSkillEventTrackKind.PresentationVfx,
                    tick));
            menu.AddItem(
                new GUIContent(menuPrefix + "音效"),
                false,
                () => AddActivePresentationEvent(
                    presentationTrackIndex,
                    FpgSkillEventTrackKind.PresentationAudio,
                    tick));
            menu.AddItem(
                new GUIContent(menuPrefix + "震屏"),
                false,
                () => AddActivePresentationEvent(
                    presentationTrackIndex,
                    FpgSkillEventTrackKind.PresentationCameraShake,
                    tick));
        }

        private void AddActivePresentationEvent(
            int presentationTrackIndex,
            FpgSkillEventTrackKind eventTrack,
            int tick)
        {
            FpgSkillEventKey eventKey =
                FpgSkillSerializedAdapter.AddActivePresentationEvent(
                    serializedAsset,
                    selectedSequenceIndex,
                    presentationTrackIndex,
                    eventTrack,
                    tick);
            if (!eventKey.IsValid)
            {
                statusLabel.text = "无法创建该表现事件。";
                return;
            }

            currentTick = tick;
            selectedPresentationTrackIndex = presentationTrackIndex;
            selectedEventKey = eventKey;

            eventSelection.SetSingle(eventKey);
            RefreshFromSerialized();
            SelectEvent(eventKey, true);
        }

        private void AddEventMenuItem(
            GenericMenu menu,
            string label,
            SerializedProperty sequence,
            FpgSkillEventTrackKind track)
        {
            GUIContent content = new GUIContent(label);
            if (FpgSkillSerializedAdapter.CanAddEventTrack(sequence, track))
            {
                menu.AddItem(content, false, () => AddEvent(track));
            }
            else
            {
                menu.AddDisabledItem(content);
            }
        }

        private void AddEvent(FpgSkillEventTrackKind track)
        {
            if (serializedAsset == null)
            {
                return;
            }

            FpgSkillEventKey eventKey = FpgSkillSerializedAdapter.AddEvent(
                serializedAsset,
                selectedSequenceIndex,
                currentTick,
                track);
            if (!eventKey.IsValid)
            {
                statusLabel.text = "当前序列没有可编辑的事件数组。";
                return;
            }

            selectedEventKey = eventKey;

            RefreshFromSerialized();
            SelectEvent(eventKey, true);
        }

        private void AddAction(
            FpgSkillActionKind actionKind,
            int modeValue,
            int tick)
        {
            FpgSkillEventKey eventKey =
                FpgSkillSerializedAdapter.AddAction(
                    serializedAsset,
                    selectedSequenceIndex,
                    tick,
                    actionKind,
                    modeValue);
            if (!eventKey.IsValid)
            {
                statusLabel.text = "无法创建该玩法动作。";
                return;
            }

            selectedEventKey = eventKey;

            currentTick = tick;
            RefreshFromSerialized();
            SelectEvent(eventKey, true);
        }

        private void DuplicateEvent()
        {
            if (serializedAsset == null || !selectedEventKey.IsValid)
            {
                return;
            }

            FpgSkillEventKey eventKey = FpgSkillSerializedAdapter.DuplicateEvent(
                serializedAsset,
                selectedSequenceIndex,
                selectedEventKey,
                durationTicks);
            if (eventKey.IsValid)
            {
                selectedEventKey = eventKey;
                RefreshFromSerialized();
                SelectEvent(eventKey, true);
            }
        }

        private void DeleteEvent()
        {
            if (serializedAsset == null || !selectedEventKey.IsValid)
            {
                return;
            }

            IReadOnlyList<FpgSkillEventKey> selectedKeys =
                eventSelection.Count > 0
                ? eventSelection.Items
                : new[] { selectedEventKey };
            if (FpgSkillSerializedAdapter.DeleteEvents(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedKeys))
            {
                int deletedCount = selectedKeys.Count;
                selectedEventKey = FpgSkillEventKey.Invalid;
                eventSelection.Clear();
                statusLabel.text = "已删除 " + deletedCount + " 个事件。";
                RefreshFromSerialized();
            }
        }

        private void Play()
        {
            if (serializedAsset == null)
            {
                statusLabel.text = "请选择动作资产后再播放。";
                return;
            }

            if (!hasCompiledSchedule)
            {
                ReportPreviewFailureToConsole(previewCompileError);
                return;
            }

            if (validation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error))
            {
                ReportPreviewFailureToConsole();
                return;
            }

            if (currentTick >= durationTicks)
            {
                SetCurrentTick(0, false, false);
            }

            previewView.ClearPresentationPreview();
            if (currentTick == 0)
            {
                if (!RestartPreviewExecutionAtZero(true))
                {
                    return;
                }
            }

            isPlaying = true;
            tickAccumulator = 0d;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void Pause()
        {
            isPlaying = false;
            tickAccumulator = 0d;
            previewExecution.ClearPendingResults();
            previewView?.ClearPresentationPreview();
        }

        private void Step(int delta)
        {
            Pause();
            SetCurrentTick(currentTick + delta, true, true);
        }

        private void SetCurrentTick(int tick, bool writeLog, bool frame)
        {
            int normalizedTick = Mathf.Clamp(tick, 0, durationTicks);
            bool crossedForwardTick = normalizedTick > currentTick;
            if (hasCompiledSchedule
                && !previewExecution.AdvanceTo(
                    normalizedTick,
                    out string executionError))
            {
                Pause();
                ReportPreviewFailureToConsole(executionError);
                return;
            }

            currentTick = normalizedTick;
            timelineView.SetPlayhead(currentTick);
            if (frame)
            {
                timelineView.FrameTick(currentTick);
            }

            RefreshPreview();
            if (writeLog
                && crossedForwardTick
                && previewExecution.ResultCount > 0)
            {
                PlayExecutionPresentations(
                    timelineView == null
                    || !timelineView.IsDirectManipulationActive);
                LogExecutionResults();
            }
        }

        private void OnEditorTick()
        {
            previewView?.UpdatePresentationPreview();
            if (!isPlaying || serializedAsset == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double elapsed = Math.Max(0d, now - lastUpdateTime);
            lastUpdateTime = now;
            tickAccumulator += elapsed * TickRate * playbackSpeed;
            int steps = Math.Min(240, (int)Math.Floor(tickAccumulator));
            if (steps <= 0)
            {
                return;
            }

            tickAccumulator -= steps;
            for (int step = 0; step < steps; step++)
            {
                int nextTick = currentTick + 1;
                if (nextTick > durationTicks)
                {
                    if (loopToggle.value)
                    {
                        previewView.ClearPresentationPreview();
                        if (!RestartPreviewExecutionAtZero(true))
                        {
                            Pause();
                            break;
                        }

                        continue;
                    }
                    else
                    {
                        Pause();
                        nextTick = durationTicks;
                    }
                }

                SetCurrentTick(nextTick, true, false);
                if (!isPlaying)
                {
                    break;
                }
            }
        }

        private bool RestartPreviewExecutionAtZero(bool writeLog)
        {
            if (!previewExecution.Bind(
                    compiledSequence,
                    out string executionError)
                || !previewExecution.AdvanceTo(
                    0,
                    out executionError))
            {
                ReportPreviewFailureToConsole(executionError);
                return false;
            }

            currentTick = 0;
            timelineView.SetPlayhead(0);
            RefreshPreview();
            if (previewExecution.ResultCount > 0)
            {
                PlayExecutionPresentations(true);
                if (writeLog)
                {
                    LogExecutionResults();
                }
            }

            return true;
        }

        private void PlayExecutionPresentations(bool allowAudio)
        {
            if (serializedAsset == null
                || previewView == null
                || !previewExecution.IsBound)
            {
                return;
            }

            for (int index = 0;
                index < previewExecution.ResultCount;
                index++)
            {
                FpgSkillEventResult result = previewExecution.GetResult(index);
                FpgSkillCompiledTriggerRecord trigger = compiledTriggers
                    .FirstOrDefault(item =>
                        item.CompiledEventId == result.EventId);
                if (trigger == null
                    || (trigger.EventKey.Track
                        != FpgSkillEventTrackKind.PresentationVfx
                        && trigger.EventKey.Track
                        != FpgSkillEventTrackKind.PresentationAudio
                        && trigger.EventKey.Track
                        != FpgSkillEventTrackKind.PresentationCameraShake))
                {
                    continue;
                }

                SerializedProperty eventProperty =
                    FpgSkillSerializedAdapter.GetEventProperty(
                        serializedAsset,
                        selectedSequenceIndex,
                        trigger.EventKey);
                if (!previewView.TryPlayActivePresentation(
                        eventProperty,
                        trigger.EventKey.Track,
                        allowAudio,
                        out string error))
                {
                    statusLabel.text = "表现预览跳过：" + error;
                }
            }
        }

        private void LogExecutionResults()
        {
            if (!hasCompiledSchedule || !previewExecution.IsBound)
            {
                return;
            }

            for (int index = 0;
                index < previewExecution.ResultCount;
                index++)
            {
                FpgSkillEventResult result = previewExecution.GetResult(index);
                FpgSkillCompiledTriggerRecord trigger = compiledTriggers
                    .FirstOrDefault(item =>
                        item.CompiledEventId == result.EventId);
                if (trigger == null)
                {
                    continue;
                }

                string message;
                if (previewSimulationFrame != null
                    && previewSimulationFrame.TryGetEventResult(
                        result.EventId,
                        out FpgSkillPreviewEventResult simulationResult))
                {
                    message = simulationResult.BuildLogMessage();
                }
                else
                {
                    message = "#" + trigger.AuthoredOrdinal + " "
                        + trigger.Kind + " · " + trigger.Name;
                }

                eventLog.Add(new FpgSkillLogEntry
                {
                    Tick = checked((int)result.Tick.Value),
                    EventKey = trigger.EventKey,
                    Message = message
                });
            }

            while (eventLog.Count > MaximumLogEntries)
            {
                eventLog.RemoveAt(0);
            }

            eventLogList.Rebuild();
            if (eventLog.Count > 0)
            {
                eventLogList.ScrollToItem(eventLog.Count - 1);
            }
        }

        private void ClearLog()
        {
            eventLog.Clear();
            eventLogList.Rebuild();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            VisualElement target = evt.target as VisualElement;
            if (target is TextField
                || target?.GetFirstAncestorOfType<TextField>() != null)
            {
                return;
            }

            bool commandModifier = evt.ctrlKey || evt.commandKey;
            if (commandModifier && evt.keyCode == KeyCode.C)
            {
                CopySelectedEvents();
                evt.StopPropagation();
                return;
            }

            if (commandModifier && evt.keyCode == KeyCode.V)
            {
                PasteCopiedEvents();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Space)
            {
                if (isPlaying)
                {
                    Pause();
                }
                else
                {
                    Play();
                }

                evt.StopPropagation();
                return;
            }

            if ((evt.keyCode == KeyCode.Delete
                    || evt.keyCode == KeyCode.Backspace)
                && selectedEventKey.IsValid)
            {
                DeleteEvent();
                evt.StopPropagation();
            }
        }

        private void OnProjectChanged()
        {
            if (rootVisualElement.panel != null)
            {
                RefreshAssetsAndPreviewSource();
            }
        }

        private void OnUndoRedoPerformed()
        {
            if (serializedAsset != null && serializedAsset.targetObject != null)
            {
                serializedAsset.UpdateIfRequiredOrScript();
                RefreshFromSerialized();
            }
        }

        private FpgSkillEventKey NormalizeEventSelection(
            FpgSkillEventKey eventKey)
        {
            return events.Any(item => item.Key == eventKey)
                ? eventKey
                : FpgSkillEventKey.Invalid;
        }

        private static VisualElement MakeAssetRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("asset-row");
            VisualElement copy = new VisualElement();
            copy.AddToClassList("asset-copy");
            Label name = new Label { name = "asset-name" };
            name.AddToClassList("asset-name");
            Label meta = new Label { name = "asset-meta" };
            meta.AddToClassList("asset-meta");
            copy.Add(name);
            copy.Add(meta);
            row.Add(copy);
            Label badge = new Label { name = "asset-type" };
            badge.AddToClassList("type-badge");
            row.Add(badge);
            return row;
        }

        private void BindAssetRow(VisualElement element, int index)
        {
            if (index < 0 || index >= filteredAssets.Count)
            {
                return;
            }

            FpgSkillAssetRecord record = filteredAssets[index];
            element.Q<Label>("asset-name").text = record.DisplayName;
            element.Q<Label>("asset-meta").text = record.SkillId;
            element.Q<Label>("asset-type").text = record.OwnerType;
            element.tooltip = record.Path;
        }

        private static VisualElement MakeValidationRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("validation-row");
            Label badge = new Label { name = "severity" };
            badge.AddToClassList("severity-badge");
            row.Add(badge);
            Label message = new Label { name = "validation-message" };
            message.AddToClassList("validation-message");
            row.Add(message);
            return row;
        }

        private void BindValidationRow(VisualElement element, int index)
        {
            if (index < 0 || index >= validation.Count)
            {
                return;
            }

            FpgSkillValidationItem item = validation[index];
            Label badge = element.Q<Label>("severity");
            badge.RemoveFromClassList("severity-error");
            badge.RemoveFromClassList("severity-warning");
            badge.RemoveFromClassList("severity-info");
            switch (item.Severity)
            {
                case FpgSkillIssueSeverity.Error:
                    badge.text = "错误";
                    badge.AddToClassList("severity-error");
                    break;
                case FpgSkillIssueSeverity.Warning:
                    badge.text = "警告";
                    badge.AddToClassList("severity-warning");
                    break;
                default:
                    badge.text = "信息";
                    badge.AddToClassList("severity-info");
                    break;
            }

            element.Q<Label>("validation-message").text = item.Message;
        }

        private static VisualElement MakeLogRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("log-row");
            Label tick = new Label { name = "log-tick" };
            tick.AddToClassList("severity-badge");
            row.Add(tick);
            Label message = new Label { name = "log-message" };
            message.AddToClassList("log-message");
            row.Add(message);
            return row;
        }

        private void BindLogRow(VisualElement element, int index)
        {
            if (index < 0 || index >= eventLog.Count)
            {
                return;
            }

            FpgSkillLogEntry item = eventLog[index];
            element.Q<Label>("log-tick").text = "T" + item.Tick;
            element.Q<Label>("log-message").text = item.Message;
        }

        private sealed class FpgSkillLogEntry
        {
            public int Tick;
            public FpgSkillEventKey EventKey;
            public string Message;
        }

    

        private void CopySelectedEvents()
        {
            if (serializedAsset == null || !selectedEventKey.IsValid)
            {
                return;
            }

            IReadOnlyList<FpgSkillEventKey> selectedKeys =
                eventSelection.Count > 0
                ? eventSelection.Items
                : new[] { selectedEventKey };
            if (FpgSkillSerializedAdapter.CopyEvents(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedKeys,
                    eventClipboard))
            {
                statusLabel.text = "已复制 "
                    + eventClipboard.Count
                    + " 个事件。";
                RefreshButtons();
            }
        }

        private void PasteCopiedEvents()
        {
            if (serializedAsset == null || eventClipboard.IsEmpty)
            {
                return;
            }

            List<FpgSkillEventKey> pasted =
                FpgSkillSerializedAdapter.PasteEvents(
                serializedAsset,
                selectedSequenceIndex,
                eventClipboard,
                currentTick);
            if (pasted.Count == 0)
            {
                statusLabel.text = "当前序列无法粘贴这些事件。";
                return;
            }

            eventSelection.Set(pasted, pasted[pasted.Count - 1]);
            selectedEventKey = eventSelection.PrimaryEventKey;

            statusLabel.text = "已粘贴 "
                + pasted.Count
                + " 个事件，并生成新的内部引用。";
            RefreshFromSerialized();
            timelineView.FrameTick(currentTick);
        }


        private void AddAnimationInspector(SerializedProperty sequence)
        {
            if (sequence == null)
            {
                AddInspectorEmptyState("当前序列无法读取。");
                return;
            }

            SerializedProperty mainAnimation =
                sequence.FindPropertyRelative("mainAnimation");
            SerializedProperty alternateAnimations =
                sequence.FindPropertyRelative("alternateAnimations");
            List<string> currentAnimations = new List<string>();
            if (mainAnimation != null)
            {
                currentAnimations.Add(mainAnimation.stringValue);
            }

            if (alternateAnimations != null && alternateAnimations.isArray)
            {
                for (int index = 0;
                    index < alternateAnimations.arraySize;
                    index++)
                {
                    SerializedProperty variant =
                        alternateAnimations.GetArrayElementAtIndex(index);
                    if (variant.propertyType == SerializedPropertyType.String)
                    {
                        currentAnimations.Add(variant.stringValue);
                    }
                }
            }

            List<FpgSkillAuthoringChoice> animationChoices =
                FpgSkillAuthoringChoices.BuildAnimationChoices(
                    previewPrefabField.value as GameObject,
                    currentAnimations);
            AddStringChoiceProperty(
                mainAnimation,
                "动画片段",
                animationChoices,
                "修改主动画");
            AddAnimationVariants(alternateAnimations, animationChoices);

            SerializedProperty playbackMode =
                sequence.FindPropertyRelative("animationPlaybackMode");
            AddAnimationPlaybackModeProperty(playbackMode);

            int animationStartTick =
                FpgSkillSerializedAdapter.GetAnimationStartTick(sequence);
            int animationEndTick =
                FpgSkillSerializedAdapter.GetAnimationEndTick(sequence);
            int authoredDurationTicks = Mathf.Max(
                0,
                animationEndTick - animationStartTick);
            int sourceBaselineTicks = FpgSkillSerializedAdapter
                .GetAnimationSourceDurationTicks(sequence);
            int sourceDurationTicks = measuredAnimationDurationTicks > 0
                ? measuredAnimationDurationTicks
                : sourceBaselineTicks;
            string sourceSuffix = measuredAnimationDurationTicks > 0
                ? "（Spine 实测）"
                : sourceBaselineTicks > 0
                    ? "（记录基准）"
                    : string.Empty;

            bool naturalSpeed = playbackMode != null
                && playbackMode.propertyType == SerializedPropertyType.Enum
                && playbackMode.enumValueIndex
                    == (int)FpgSkillAnimationPlaybackMode.NaturalSpeed;
            bool loop = FpgSkillSerializedAdapter.GetAnimationLoop(sequence);
            bool showCompleteSourceRange = naturalSpeed
                && !loop
                && sourceDurationTicks > 0;
            long completeEndLong = (long)animationStartTick
                + sourceDurationTicks;
            int completeEndTick = (int)Math.Min(
                int.MaxValue,
                completeEndLong);
            int displayedEndTick = showCompleteSourceRange
                ? completeEndTick
                : animationEndTick;
            int displayedDurationTicks = showCompleteSourceRange
                ? sourceDurationTicks
                : authoredDurationTicks;
            string sequenceCutoff = showCompleteSourceRange
                && completeEndLong > durationTicks
                    ? " · 序列截止 Tick " + durationTicks
                    : string.Empty;

            AddReadOnlyInspectorValue(
                "源动画帧数 @60Hz",
                sourceDurationTicks > 0
                    ? FormatTickDuration(sourceDurationTicks)
                        + " " + sourceSuffix
                    : "未测量",
                "按 Spine Animation.Duration 换算为 60Hz 采样帧；"
                    + "这是技能时间轴帧数，不是 Spine 工程的原始关键帧数量。");
            AddReadOnlyInspectorValue(
                "当前播放区间",
                "Tick " + animationStartTick + "-"
                    + displayedEndTick + " · "
                    + FormatTickDuration(displayedDurationTicks)
                    + sequenceCutoff,
                "NaturalSpeed 按完整源动画自然播放；"
                    + "FitInterval 将完整源动画适配到作者区间。");

            if (showCompleteSourceRange
                && completeEndLong > durationTicks)
            {
                Button extendSequenceButton = new Button(() =>
                    CommitTimelineBlockRange(
                        FpgSkillTimelineBlockKind.Animation,
                        0,
                        FpgSkillTimelineBlockEditMode.Move,
                        animationStartTick,
                        completeEndTick,
                        completeEndTick))
                {
                    text = "延长序列到完整动画",
                    tooltip = "只延长当前序列以容纳完整源动画；"
                        + "已有逻辑事件 Tick 不会移动。"
                };
                extendSequenceButton.SetEnabled(
                    completeEndLong <= int.MaxValue);
                inspectorContent.Add(extendSequenceButton);
            }

            AddTypedProperty(
                sequence.FindPropertyRelative("loop"),
                "循环");
        }

        private void AddAnimationVariants(
            SerializedProperty variants,
            IList<FpgSkillAuthoringChoice> choices)
        {
            if (variants == null || !variants.isArray)
            {
                return;
            }

            Label title = new Label("确定性动画变体");
            title.AddToClassList("inspector-section-title");
            inspectorContent.Add(title);

            for (int index = 0; index < variants.arraySize; index++)
            {
                int capturedIndex = index;
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                DropdownField field = CreateStringChoiceField(
                    variants.GetArrayElementAtIndex(index),
                    "变体 " + (index + 1),
                    choices,
                    "修改动画变体");
                field.style.flexGrow = 1f;
                row.Add(field);

                Button removeButton = new Button(
                    () => RemoveAnimationVariant(capturedIndex))
                {
                    text = "删除",
                    tooltip = "删除这个动画变体"
                };
                row.Add(removeButton);
                inspectorContent.Add(row);
            }

            string propertyPath = variants.propertyPath;
            Button addButton = new Button(
                () => AddAnimationVariant(propertyPath, choices))
            {
                text = "添加动画变体"
            };
            inspectorContent.Add(addButton);
        }

        private void AddAnimationVariant(
            string arrayPropertyPath,
            IList<FpgSkillAuthoringChoice> choices)
        {
            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty array = serializedAsset.FindProperty(
                arrayPropertyPath);
            if (array == null || !array.isArray)
            {
                return;
            }

            string mainAnimation = FpgSkillSerializedAdapter.GetMainAnimation(
                FpgSkillSerializedAdapter.GetSequence(
                    serializedAsset,
                    selectedSequenceIndex));
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal)
            {
                mainAnimation
            };
            for (int index = 0; index < array.arraySize; index++)
            {
                used.Add(array.GetArrayElementAtIndex(index).stringValue);
            }

            string value = string.Empty;
            if (choices != null)
            {
                for (int index = 0; index < choices.Count; index++)
                {
                    if (!string.IsNullOrWhiteSpace(choices[index].Value)
                        && !used.Contains(choices[index].Value))
                    {
                        value = choices[index].Value;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                statusLabel.text = "当前没有可添加的其他动画。";
                return;
            }

            Undo.RecordObject(
                serializedAsset.targetObject,
                "添加动画变体");
            int newIndex = array.arraySize;
            array.arraySize++;
            array.GetArrayElementAtIndex(newIndex).stringValue = value;
            ApplyInspectorChanges();
        }

        private void RemoveAnimationVariant(int index)
        {
            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty sequence =
                FpgSkillSerializedAdapter.GetSequence(
                    serializedAsset,
                    selectedSequenceIndex);
            SerializedProperty variants =
                sequence?.FindPropertyRelative("alternateAnimations");
            if (variants == null
                || !variants.isArray
                || index < 0
                || index >= variants.arraySize)
            {
                return;
            }

            Undo.RecordObject(
                serializedAsset.targetObject,
                "删除动画变体");
            variants.DeleteArrayElementAtIndex(index);
            ApplyInspectorChanges();
        }

        private void AddAnimationPlaybackModeProperty(
            SerializedProperty property)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            List<string> labels = new List<string>
            {
                "自然速度",
                "适配区间"
            };
            List<FpgSkillAnimationPlaybackMode> values =
                new List<FpgSkillAnimationPlaybackMode>
                {
                    FpgSkillAnimationPlaybackMode.NaturalSpeed,
                    FpgSkillAnimationPlaybackMode.FitInterval
                };
            FpgSkillAnimationPlaybackMode current =
                Enum.IsDefined(
                    typeof(FpgSkillAnimationPlaybackMode),
                    property.enumValueIndex)
                    ? (FpgSkillAnimationPlaybackMode)property.enumValueIndex
                    : FpgSkillAnimationPlaybackMode.NaturalSpeed;
            int selectedIndex = Mathf.Max(0, values.IndexOf(current));
            DropdownField field = new DropdownField(
                "播放模式",
                labels,
                selectedIndex)
            {
                tooltip = "自然速度：按 Spine 源动画完整时长播放；"
                    + "适配区间：将完整源动画重定时到时间轴区间。"
                    + "修改动画区间不会移动逻辑事件。"
            };
            string propertyPath = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index >= 0 && index < values.Count)
                {
                    ApplyEnumChoice(
                        propertyPath,
                        values[index],
                        "修改动画播放模式");
                }
            });
            inspectorContent.Add(field);
        }

        private void AddReadOnlyInspectorValue(
            string label,
            string value,
            string tooltip)
        {
            TextField field = new TextField(label);
            field.SetValueWithoutNotify(value ?? string.Empty);
            field.SetEnabled(false);
            field.tooltip = tooltip ?? string.Empty;
            inspectorContent.Add(field);
        }

        private static string FormatTickDuration(int ticks)
        {
            int normalizedTicks = Mathf.Max(0, ticks);
            return normalizedTicks + " 帧 / "
                + (normalizedTicks / (double)TickRate).ToString("0.000")
                + " 秒";
        }



        private void AddEventInspector(
            SerializedProperty eventProperty,
            FpgSkillEventRecord record)
        {
            if (eventProperty == null || record == null)
            {
                AddInspectorEmptyState("当前事件无法读取。");
                return;
            }

            if (record.Key.ActionKind != FpgSkillActionKind.None)
            {
                AddActionInspector(eventProperty, record);
                return;
            }

            AddReadOnlyEventType(record.Track);
            GameObject previewPrefab = previewPrefabField.value as GameObject;
            switch (record.Track)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                case FpgSkillEventTrackKind.PresentationAudio:
                case FpgSkillEventTrackKind.PresentationCameraShake:
                    AddActivePresentationInspector(
                        eventProperty,
                        record,
                        previewPrefab);
                    break;

                case FpgSkillEventTrackKind.Warning:
                {
                    SerializedProperty warning =
                        eventProperty.FindPropertyRelative("warningId");
                    AddStringChoiceProperty(
                        warning,
                        "预警类型",
                        FpgSkillAuthoringChoices.BuildWarningChoices(
                            warning == null
                                ? string.Empty
                                : warning.stringValue),
                        "修改预警类型");
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("startTick"),
                        "开始 Tick");
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("endTick"),
                        "结束 Tick");
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("authoredOrdinal"),
                        "同 Tick 顺序");
                    AddStringChoiceProperty(
                        eventProperty.FindPropertyRelative("socketId"),
                        "Socket",
                        FpgSkillAuthoringChoices.BuildSocketChoices(
                            previewPrefab,
                            record.SocketId),
                        "修改预警 Socket");
                    break;
                }
            }
        }

        private void AddActivePresentationInspector(
            SerializedProperty eventProperty,
            FpgSkillEventRecord record,
            GameObject previewPrefab)
        {
            if (presentationTracks.Count > 0)
            {
                List<string> trackNames = presentationTracks
                    .Select(item => item.Name)
                    .ToList();
                int currentTrackIndex = Mathf.Clamp(
                    record.PresentationTrackIndex,
                    0,
                    trackNames.Count - 1);
                DropdownField trackField = new DropdownField(
                    "表现轨道",
                    trackNames,
                    currentTrackIndex);
                trackField.RegisterValueChangedCallback(evt =>
                {
                    int targetTrackIndex = trackNames.IndexOf(evt.newValue);
                    if (targetTrackIndex < 0
                        || targetTrackIndex
                            == record.PresentationTrackIndex)
                    {
                        return;
                    }

                    FpgSkillEventKey moved = FpgSkillSerializedAdapter
                        .MoveActivePresentationEventToTrack(
                            serializedAsset,
                            selectedSequenceIndex,
                            record.Key,
                            targetTrackIndex);
                    if (!moved.IsValid)
                    {
                        statusLabel.text = "无法移动主动表现事件。";
                        trackField.SetValueWithoutNotify(
                            trackNames[currentTrackIndex]);
                        return;
                    }

                    selectedPresentationTrackIndex = targetTrackIndex;
                    selectedEventKey = moved;
                    eventSelection.SetSingle(moved);
                    RefreshFromSerialized();
                    SelectEvent(moved, true);
                });
                inspectorContent.Add(trackField);
            }

            AddTypedProperty(
                eventProperty.FindPropertyRelative("tick"),
                "触发 Tick");
            AddTypedProperty(
                eventProperty.FindPropertyRelative("authoredOrdinal"),
                "同 Tick 顺序");
            SerializedProperty binding =
                eventProperty.FindPropertyRelative(
                    "boundGameplayEventId");
            AddStringChoiceProperty(
                binding,
                "关联逻辑事件",
                FpgSkillAuthoringChoices.BuildGameplayEventChoices(
                    events,
                    binding == null ? string.Empty : binding.stringValue),
                "修改表现事件关联");

            SerializedProperty presentation =
                eventProperty.FindPropertyRelative("presentation");
            switch (record.Track)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                    SerializedProperty anchor =
                        eventProperty.FindPropertyRelative("anchor");
                    AddVfxAnchorProperty(
                        anchor,
                        eventProperty.FindPropertyRelative("socketId"));
                    if (anchor != null && anchor.intValue == 1)
                    {
                        AddStringChoiceProperty(
                            eventProperty.FindPropertyRelative("socketId"),
                            "Owner Socket",
                            FpgSkillAuthoringChoices.BuildSocketChoices(
                                previewPrefab,
                                record.SocketId),
                            "修改特效挂点");
                    }

                    AddActionProperties(
                        presentation,
                        "prefab", "特效 Prefab",
                        "durationSeconds", "持续时间（秒）",
                        "scale", "缩放",
                        "rotationOffsetEuler", "旋转偏移");
                    break;

                case FpgSkillEventTrackKind.PresentationAudio:
                    AddActionProperties(
                        presentation,
                        "clip", "音效",
                        "volume", "音量");
                    break;

                case FpgSkillEventTrackKind.PresentationCameraShake:
                    AddActionProperties(
                        presentation,
                        "strength", "强度",
                        "durationSeconds", "持续时间（秒）");
                    break;
            }
        }

        private void AddVfxAnchorProperty(
            SerializedProperty anchor,
            SerializedProperty socketId)
        {
            if (anchor == null
                || anchor.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            List<string> labels = new List<string>
            {
                "Owner Root",
                "Owner Socket"
            };
            DropdownField field = new DropdownField(
                "挂载位置",
                labels,
                Mathf.Clamp(anchor.intValue, 0, labels.Count - 1));
            string anchorPath = anchor.propertyPath;
            string socketPath = socketId?.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                int value = labels.IndexOf(evt.newValue);
                if (value < 0
                    || serializedAsset == null
                    || serializedAsset.targetObject == null)
                {
                    return;
                }

                serializedAsset.UpdateIfRequiredOrScript();
                SerializedProperty currentAnchor =
                    serializedAsset.FindProperty(anchorPath);
                SerializedProperty currentSocket =
                    string.IsNullOrWhiteSpace(socketPath)
                        ? null
                        : serializedAsset.FindProperty(socketPath);
                if (currentAnchor == null)
                {
                    return;
                }

                Undo.RecordObject(
                    serializedAsset.targetObject,
                    "修改特效挂载位置");
                currentAnchor.intValue = value;
                if (value == 0
                    && currentSocket != null
                    && currentSocket.propertyType
                        == SerializedPropertyType.String)
                {
                    currentSocket.stringValue = string.Empty;
                }

                ApplyInspectorChanges();
            });
            inspectorContent.Add(field);
        }

        private void AddActionInspector(
            SerializedProperty action,
            FpgSkillEventRecord record)
        {
            AddReadOnlyInspectorValue(
                "玩法动作类型",
                GetActionKindLabel(record.Key.ActionKind),
                "节点类型只读；使用下方转换命令显式改变类型。");
            Button convertButton = new Button(
                () => ShowActionConversionMenu(record.Key))
            {
                text = "转换事件类型..."
            };
            inspectorContent.Add(convertButton);

            AddActionProperties(
                action,
                "tick", "触发 Tick",
                "authoredOrdinal", "同 Tick 顺序");
            bool enemy = selectedAsset != null
                && selectedAsset.GetType().Name.IndexOf(
                    "Enemy",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            FpgSkillActionAuthoringOptions spatialOptions =
                FpgSkillActionAuthoringRules.Get(
                    record.Key.ActionKind,
                    enemy);
            if (spatialOptions.SupportsTargetSourceSelection)
            {
                AddTargetSourceChoiceProperty(
                    action.FindPropertyRelative("targetSource"),
                    spatialOptions);
            }

            if (spatialOptions.SupportsSocket)
            {
                GameObject previewPrefab = previewPrefabField.value as GameObject;
                AddStringChoiceProperty(
                    action.FindPropertyRelative("socketId"),
                    "Socket",
                    FpgSkillAuthoringChoices.BuildSocketChoices(
                        previewPrefab,
                        record.SocketId),
                    "修改玩法动作 Socket");
            }

            if (spatialOptions.SupportsTargetOffset)
            {
                AddTargetOffsetProperty(
                    action.FindPropertyRelative("targetOffset"));
            }

            switch (record.Key.ActionKind)
            {
                case FpgSkillActionKind.Attack:
                    AddConfirmedAttackModeProperty(
                        action.FindPropertyRelative("mode"),
                        record.Key);
                    AddActionProperties(
                        action,
                        "ammoCost", "资源消耗",
                        "baseDamage", "基础伤害",
                        "breakDamage", "削韧伤害",
                        "weakpointDamageMultiplierBasisPoints", "弱点伤害倍率（万分比）",
                        "weakpointBreakMultiplierBasisPoints", "弱点削韧倍率（万分比）");
                    AddAttackModeProperties(action);
                    AddTypedProperty(
                        action.FindPropertyRelative(
                            "trajectoryPresentation"),
                        "攻击轨迹特效");
                    AddTypedProperty(
                        action.FindPropertyRelative(
                            "impactPresentation"),
                        "命中表现");
                    break;

                case FpgSkillActionKind.LaunchProjectile:
                    AddTypedProperty(
                        action.FindPropertyRelative("impactMode"),
                        "命中方式");
                    AddActionProperties(
                        action,
                        "ammoCost", "资源消耗",
                        "baseDamage", "基础伤害",
                        "breakDamage", "削韧伤害",
                        "weakpointDamageMultiplierBasisPoints", "弱点伤害倍率（万分比）",
                        "weakpointBreakMultiplierBasisPoints", "弱点削韧倍率（万分比）",
                        "projectileDefinitionId", "投射物定义 ID",
                        "projectileCount", "发射数量",
                        "projectileFlightTicks", "飞行 Tick",
                        "projectileLifetimeTicks", "寿命 Tick",
                        "projectileInterceptable", "可拦截",
                        "projectileMaxHitPoints", "拦截生命值",
                        "projectileBudgetUnits", "预算单位",
                        "projectileSweepRadiusKey", "碰撞半径 Key");
                    AddTypedProperty(
                        action.FindPropertyRelative("flightVfx"),
                        "投射物飞行特效");
                    AddTypedProperty(
                        action.FindPropertyRelative(
                            "collisionPresentation"),
                        "投射物命中表现");


                    if (GetSerializedEnumName(
                            action.FindPropertyRelative("impactMode"))
                        == "AreaAtFirstSurface")
                    {
                        AddActionProperties(
                            action,
                            "areaCombatantLimit", "命中战斗单位上限",
                            "areaProjectileLimit", "命中投射物上限",
                            "allowedTargetKinds", "允许命中目标");
                    }
                    else
                    {
                        AddTypedProperty(
                            action.FindPropertyRelative(
                                "threatDefinitionId"),
                            "威胁定义 ID");
                    }
                    break;

                case FpgSkillActionKind.CommitReload:
                    AddReadOnlyInspectorValue(
                        "提交行为",
                        "原子提交弹匣状态",
                        "执行器负责验证、提交与失败补偿，不开放事务内部步骤。");
                    SerializedProperty successAnimation =
                        action.FindPropertyRelative(
                            "successAnimationName");
                    AddStringChoiceProperty(
                        successAnimation,
                        "成功动画",
                        FpgSkillAuthoringChoices.BuildAnimationChoices(
                            previewPrefabField.value as GameObject,
                            new[]
                            {
                                successAnimation == null
                                    ? string.Empty
                                    : successAnimation.stringValue
                            }),
                        "修改换弹成功动画");
                    break;

                case FpgSkillActionKind.SummonActors:
                    AddActionProperties(
                        action,
                        "summonCandidates", "召唤候选",
                        "summonCandidateWeights", "候选权重",
                        "summonOccupancyMode", "占位方式",
                        "summonPlacementMode", "放置方式",
                        "summonOwnerOutcome", "召唤者结果",
                        "maxSummonsPerOwner", "每个召唤者上限",
                        "maxTotalSummonsPerEncounter", "遭遇总上限",
                        "maxSummonRecursionDepth", "递归深度上限");
                    break;
            }
        }

        private void AddAttackModeProperties(SerializedProperty action)
        {
            switch (GetSerializedEnumName(
                action.FindPropertyRelative("mode")))
            {
                case "PelletRays":
                    AddActionProperties(
                        action,
                        "pelletCount", "射线数量",
                        "additionalPenetrationCount", "额外穿透数量",
                        "allowedTargetKinds", "允许命中目标");
                    break;
                case "AreaAtFirstSurface":
                    AddActionProperties(
                        action,
                        "areaCombatantLimit", "命中战斗单位上限",
                        "areaProjectileLimit", "命中投射物上限",
                        "allowedTargetKinds", "允许命中目标");
                    break;
                case "BoundTarget":
                    AddActionProperties(
                        action,
                        "threatDefinitionId", "威胁定义 ID",
                        "boundTargetPolicy", "目标规则",
                        "delayTicks", "锁定后延时 Tick");


                    break;
            }
        }

        private void AddConfirmedAttackModeProperty(
            SerializedProperty property,
            FpgSkillEventKey eventKey)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            bool enemy = selectedAsset != null
                && selectedAsset.GetType().Name.IndexOf(
                    "Enemy",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            List<string> labels = enemy
                ? new List<string> { "指定目标攻击" }
                : new List<string> { "射线攻击", "范围攻击" };
            List<int> values = enemy
                ? new List<int> { 3 }
                : new List<int> { 1, 2 };
            int selectedIndex = Math.Max(
                0,
                values.IndexOf(property.intValue));
            DropdownField field = new DropdownField(
                "攻击模式",
                labels,
                selectedIndex);
            field.RegisterValueChangedCallback(evt =>
            {
                int newIndex = labels.IndexOf(evt.newValue);
                if (newIndex < 0
                    || values[newIndex] == property.intValue)
                {
                    return;
                }

                bool confirmed = EditorUtility.DisplayDialog(
                    "切换攻击模式",
                    "切换会保留伤害、资源消耗、事件 ID、Tick 和顺序，"
                        + "并重建模式专属查询参数。",
                    "切换",
                    "取消");
                if (!confirmed)
                {
                    field.SetValueWithoutNotify(labels[selectedIndex]);
                    return;
                }

                if (FpgSkillSerializedAdapter.SetActionMode(
                        serializedAsset,
                        selectedSequenceIndex,
                        eventKey,
                        values[newIndex]))
                {
                    QueueSerializedRefresh();
                }
            });
            inspectorContent.Add(field);
        }

        private void ShowActionConversionMenu(FpgSkillEventKey sourceKey)
        {
            GenericMenu menu = new GenericMenu();
            bool enemy = selectedAsset != null
                && selectedAsset.GetType().Name.IndexOf(
                    "Enemy",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            AddActionConversionItem(
                menu,
                sourceKey,
                "攻击",
                FpgSkillActionKind.Attack,
                enemy ? 3 : 1);
            AddActionConversionItem(
                menu,
                sourceKey,
                "发射投射物",
                FpgSkillActionKind.LaunchProjectile,
                enemy ? 2 : 1);
            if (enemy)
            {
                AddActionConversionItem(
                    menu,
                    sourceKey,
                    "召唤单位",
                    FpgSkillActionKind.SummonActors,
                    0);
            }
            else
            {
                AddActionConversionItem(
                    menu,
                    sourceKey,
                    "完成换弹",
                    FpgSkillActionKind.CommitReload,
                    0);
            }

            menu.ShowAsContext();
        }

        private void AddActionConversionItem(
            GenericMenu menu,
            FpgSkillEventKey sourceKey,
            string label,
            FpgSkillActionKind targetKind,
            int targetMode)
        {
            GUIContent content = new GUIContent(label);
            if (sourceKey.ActionKind == targetKind)
            {
                menu.AddDisabledItem(content, true);
                return;
            }

            menu.AddItem(
                content,
                false,
                () => ConvertAction(sourceKey, targetKind, targetMode));
        }

        private void ConvertAction(
            FpgSkillEventKey sourceKey,
            FpgSkillActionKind targetKind,
            int targetMode)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "转换玩法动作类型",
                "跨动作类型转换会保留事件 ID、Tick、同 Tick 顺序及兼容的伤害/消耗字段。"
                    + "原节点中与目标动作不兼容的轨迹、命中、飞行、碰撞或成功动画表现会被清空，"
                    + "其他参数使用目标类型的安全默认值。",
                "转换",
                "取消");
            if (!confirmed)
            {
                return;
            }

            FpgSkillEventKey converted =
                FpgSkillSerializedAdapter.ConvertAction(
                    serializedAsset,
                    selectedSequenceIndex,
                    sourceKey,
                    targetKind,
                    targetMode);
            if (!converted.IsValid)
            {
                statusLabel.text = "无法转换该玩法动作。";
                return;
            }

            selectedEventKey = converted;
            eventSelection.SetSingle(converted);
            RefreshFromSerialized();
            SelectEvent(converted, true);
        }

        private static string GetActionKindLabel(
            FpgSkillActionKind actionKind)
        {
            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                    return "攻击";
                case FpgSkillActionKind.LaunchProjectile:
                    return "发射投射物";
                case FpgSkillActionKind.CommitReload:
                    return "完成换弹";
                case FpgSkillActionKind.SummonActors:
                    return "召唤单位";
                default:
                    return "未知";
            }
        }

        private void AddReadOnlyEventType(FpgSkillEventTrackKind track)
        {
            string label;
            switch (track)
            {
                case FpgSkillEventTrackKind.PresentationVfx:
                    label = "特效";
                    break;
                case FpgSkillEventTrackKind.PresentationAudio:
                    label = "音效";
                    break;
                case FpgSkillEventTrackKind.PresentationCameraShake:
                    label = "震屏";
                    break;
                case FpgSkillEventTrackKind.Warning:
                    label = "预警";
                    break;
                default:
                    label = "玩法动作";
                    break;
            }

            AddReadOnlyInspectorValue("事件类型", label, string.Empty);
        }

        private void AddActionProperties(
            SerializedProperty parent,
            params string[] namesAndLabels)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = 0;
                index + 1 < namesAndLabels.Length;
                index += 2)
            {
                if (selectedAsset != null
                    && string.Equals(
                        selectedAsset.GetType().FullName,
                        EnemySkillTypeName,
                        StringComparison.Ordinal)
                    && string.Equals(
                        namesAndLabels[index],
                        "ammoCost",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                AddTypedProperty(
                    parent.FindPropertyRelative(namesAndLabels[index]),
                    namesAndLabels[index + 1]);
            }
        }

        private void AddTypedProperty(
            SerializedProperty property,
            string label)
        {
            if (property == null)
            {
                return;
            }

            if (property.propertyType == SerializedPropertyType.Enum)
            {
                AddEnumProperty(property, label);
                return;
            }

            AddBoundProperty(property, label);
        }

        private void AddTargetSourceChoiceProperty(
            SerializedProperty property,
            FpgSkillActionAuthoringOptions options)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum
                || options == null
                || !options.SupportsTargetSourceSelection)
            {
                return;
            }

            List<string> labels = new List<string>();
            List<FpgSkillTargetSource> values =
                new List<FpgSkillTargetSource>();
            for (int index = 0;
                index < options.TargetSourceChoices.Count;
                index++)
            {
                FpgSkillTargetSource source =
                    options.TargetSourceChoices[index];
                labels.Add(GetTargetSourceLabel(source));
                values.Add(source);
            }

            int selectedIndex = values.IndexOf(
                (FpgSkillTargetSource)property.enumValueIndex);
            selectedIndex = Mathf.Max(0, selectedIndex);
            DropdownField field = new DropdownField(
                "目标来源",
                labels,
                selectedIndex);
            field.name = "target-source-field";
            string propertyPath = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index >= 0 && index < values.Count)
                {
                    ApplyTargetSourceChoice(propertyPath, values[index]);
                }
            });
            inspectorContent.Add(field);
        }

        private void AddTargetOffsetProperty(SerializedProperty property)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Vector3)
            {
                return;
            }

            Vector3Field field = new Vector3Field("目标偏移");
            field.name = "target-offset-field";
            field.isDelayed = true;
            field.SetValueWithoutNotify(property.vector3Value);
            string propertyPath = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
                ApplyTargetOffset(propertyPath, evt.newValue));
            inspectorContent.Add(field);
        }

        private static string GetTargetSourceLabel(
            FpgSkillTargetSource source)
        {
            switch (source)
            {
                case FpgSkillTargetSource.CurrentAim:
                    return "当前瞄准";

                case FpgSkillTargetSource.CurrentTarget:
                    return "当前目标";

                case FpgSkillTargetSource.SocketForward:
                    return "Socket 正前方";

                default:
                    return source.ToString();
            }
        }

        private void ApplyTargetSourceChoice(
            string propertyPath,
            FpgSkillTargetSource source)
        {
            if (serializedAsset == null
                || serializedAsset.targetObject == null)
            {
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty property =
                serializedAsset.FindProperty(propertyPath);
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            int enumValueIndex = Array.IndexOf(
                property.enumNames,
                source.ToString());
            if (enumValueIndex < 0
                || property.enumValueIndex == enumValueIndex)
            {
                return;
            }

            Undo.RecordObject(serializedAsset.targetObject, "修改目标来源");
            property.enumValueIndex = enumValueIndex;
            ApplyInspectorChanges();
        }

        private void ApplyTargetOffset(string propertyPath, Vector3 value)
        {
            if (serializedAsset == null
                || serializedAsset.targetObject == null)
            {
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty property =
                serializedAsset.FindProperty(propertyPath);
            if (property == null
                || property.propertyType != SerializedPropertyType.Vector3
                || property.vector3Value == value)
            {
                return;
            }

            Undo.RecordObject(serializedAsset.targetObject, "修改目标偏移");
            property.vector3Value = value;
            serializedAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedAsset.targetObject);
            RefreshFromSerialized(false);
        }

        private void AddEnumProperty(
            SerializedProperty property,
            string label)
        {
            Enum currentValue;
            try
            {
                currentValue = property.boxedValue as Enum;
            }
            catch (InvalidOperationException)
            {
                currentValue = null;
            }

            if (currentValue == null)
            {
                AddBoundProperty(property, label);
                return;
            }

            if (IsAttackTargetKinds(currentValue))
            {
                AddAttackTargetKindsProperty(property, label, currentValue);
                return;
            }

            BaseField<Enum> field = currentValue.GetType().IsDefined(
                typeof(FlagsAttribute),
                false)
                ? (BaseField<Enum>)new EnumFlagsField(label, currentValue)
                : new EnumField(label, currentValue);
            string propertyPath = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
                ApplyEnumChoice(
                    propertyPath,
                    evt.newValue,
                    "修改" + label));
            inspectorContent.Add(field);
        }

        private void AddAttackTargetKindsProperty(
            SerializedProperty property,
            string label,
            Enum currentValue)
        {
            int knownMask = GetAttackTargetKindsMask(currentValue.GetType());
            int selectedMask = Convert.ToInt32(currentValue) & knownMask;
            string propertyPath = property.propertyPath;

            VisualElement field = new VisualElement();
            field.style.marginBottom = 4f;
            field.Add(new Label(label));

            VisualElement choices = new VisualElement();
            choices.style.flexDirection = FlexDirection.Row;
            foreach (object definedValue in Enum.GetValues(
                         currentValue.GetType()))
            {
                Enum enumValue = definedValue as Enum;
                int targetKind = enumValue == null
                    ? 0
                    : Convert.ToInt32(enumValue);
                if (!IsSingleFlag(targetKind))
                {
                    continue;
                }

                Toggle choice = new Toggle(
                    GetAttackTargetKindLabel(enumValue.ToString()));
                choice.style.marginRight = 12f;
                choice.SetValueWithoutNotify(
                    (selectedMask & targetKind) != 0);
                choice.RegisterValueChangedCallback(evt =>
                {
                    int nextMask = evt.newValue
                        ? selectedMask | targetKind
                        : selectedMask & ~targetKind;
                    if (nextMask == selectedMask)
                    {
                        return;
                    }

                    selectedMask = nextMask;
                    ApplyAttackTargetKindsChoice(
                        propertyPath,
                        selectedMask,
                        knownMask,
                        "修改" + label);
                });
                choices.Add(choice);
            }

            field.Add(choices);
            inspectorContent.Add(field);
        }

        private void ApplyAttackTargetKindsChoice(
            string propertyPath,
            int value,
            int knownMask,
            string undoName)
        {
            if (serializedAsset == null
                || serializedAsset.targetObject == null)
            {
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty property =
                serializedAsset.FindProperty(propertyPath);
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            int normalizedValue = value & knownMask;
            if (property.intValue == normalizedValue)
            {
                return;
            }

            Undo.RecordObject(serializedAsset.targetObject, undoName);
            property.intValue = normalizedValue;
            ApplyInspectorChanges();
        }

        private static bool IsAttackTargetKinds(Enum value)
        {
            return value != null
                && string.Equals(
                    value.GetType().FullName,
                    AttackTargetKindsTypeName,
                    StringComparison.Ordinal);
        }

        private static int GetAttackTargetKindsMask(Type enumType)
        {
            if (enumType == null)
            {
                return 0;
            }

            int mask = 0;
            foreach (object definedValue in Enum.GetValues(enumType))
            {
                int value = Convert.ToInt32(definedValue);
                if (IsSingleFlag(value))
                {
                    mask |= value;
                }
            }

            return mask;
        }

        private static bool IsSingleFlag(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static string GetAttackTargetKindLabel(string enumName)
        {
            switch (enumName)
            {
                case "Combatant":
                    return "战斗单位";

                case "Projectile":
                    return "投射物";

                default:
                    return enumName ?? string.Empty;
            }
        }

        private void AddStringChoiceProperty(
            SerializedProperty property,
            string label,
            IList<FpgSkillAuthoringChoice> choices,
            string undoName)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            inspectorContent.Add(
                CreateStringChoiceField(
                    property,
                    label,
                    choices,
                    undoName));
        }

        private DropdownField CreateStringChoiceField(
            SerializedProperty property,
            string label,
            IList<FpgSkillAuthoringChoice> choices,
            string undoName,
            Action<string> selectionHandler = null)
        {
            List<string> labels = new List<string>();
            List<string> values = new List<string>();
            int selectedIndex = -1;
            string currentValue = property.stringValue ?? string.Empty;
            if (choices != null)
            {
                for (int index = 0; index < choices.Count; index++)
                {
                    string choiceLabel = choices[index].Label;
                    string uniqueLabel = choiceLabel;
                    int suffix = 2;
                    while (labels.Contains(uniqueLabel))
                    {
                        uniqueLabel = choiceLabel + " (" + suffix + ")";
                        suffix++;
                    }

                    labels.Add(uniqueLabel);
                    values.Add(choices[index].Value);
                    if (string.Equals(
                            choices[index].Value,
                            currentValue,
                            StringComparison.Ordinal))
                    {
                        selectedIndex = index;
                    }
                }
            }

            if (labels.Count == 0)
            {
                labels.Add("无可用选项");
                values.Add(currentValue);
                selectedIndex = 0;
            }
            else if (selectedIndex < 0)
            {
                labels.Add("当前值（未找到）");
                values.Add(currentValue);
                selectedIndex = labels.Count - 1;
            }

            DropdownField field = new DropdownField(
                label,
                labels,
                selectedIndex);
            string propertyPath = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= values.Count)
                {
                    return;
                }

                if (selectionHandler != null)
                {
                    selectionHandler(values[index]);
                    return;
                }

                ApplyStringChoice(
                    propertyPath,
                    values[index],
                    undoName);
            });
            return field;
        }

        private void ApplyEnumChoice(
            string propertyPath,
            Enum value,
            string undoName)
        {
            if (serializedAsset == null
                || serializedAsset.targetObject == null
                || value == null)
            {
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty property =
                serializedAsset.FindProperty(propertyPath);
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            Undo.RecordObject(serializedAsset.targetObject, undoName);
            if (IsAttackTargetKinds(value))
            {
                property.intValue = Convert.ToInt32(value)
                    & GetAttackTargetKindsMask(value.GetType());
            }
            else
            {
                property.boxedValue = value;
            }
            ApplyInspectorChanges();
        }

        private void ApplyStringChoice(
            string propertyPath,
            string value,
            string undoName)
        {
            if (serializedAsset == null
                || serializedAsset.targetObject == null)
            {
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty property =
                serializedAsset.FindProperty(propertyPath);
            if (property == null
                || property.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            Undo.RecordObject(serializedAsset.targetObject, undoName);
            property.stringValue = value ?? string.Empty;
            ApplyInspectorChanges();
        }

        private void ApplyInspectorChanges()
        {
            serializedAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedAsset.targetObject);
            QueueSerializedRefresh();
        }

        private static string GetSerializedEnumName(
            SerializedProperty property)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum
                || property.enumValueIndex < 0
                || property.enumValueIndex >= property.enumNames.Length)
            {
                return string.Empty;
            }

            return property.enumNames[property.enumValueIndex];
        }

        private static bool IsHiddenStableReference(string propertyName)
        {
            switch (propertyName)
            {
                case "skillId":
                case "eventId":
                case "warningId":
                case "socketId":
                case "bindGameplayEventId":
                    return true;
                default:
                    return false;
            }
        }















}
}
