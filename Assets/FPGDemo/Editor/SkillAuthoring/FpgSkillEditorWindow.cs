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
        private const string LayoutPath =
            "Assets/FPGDemo/Editor/SkillAuthoring/FpgSkillEditor.uxml";
        private const string SelectedAssetSessionKey =
            "FPGDemo.SkillAuthoring.SelectedAssetPath";
        private const string PreviewPrefabSessionKey =
            "FPGDemo.SkillAuthoring.PreviewPrefabPath";
        private const int TickRate = FpgSkillRuntimeConstants.TickRate;
        private const int MaximumLogEntries = 200;

        private readonly List<FpgSkillAssetRecord> allAssets =
            new List<FpgSkillAssetRecord>();
        private readonly List<FpgSkillAssetRecord> filteredAssets =
            new List<FpgSkillAssetRecord>();
        private readonly List<FpgSkillPayloadRecord> payloads =
            new List<FpgSkillPayloadRecord>();
        private readonly List<FpgSkillEventRecord> events =
            new List<FpgSkillEventRecord>();
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
        private int selectedPayloadIndex = -1;
        private int selectedEventIndex = -1;
        private int selectedPhaseIndex = -1;
        private bool selectedAnimationTrack;
        private int durationTicks = 120;
        private int currentTick;
        private int targetCount = 1;
        private int measuredAnimationDurationTicks = -1;
        private bool isPlaying;
        private bool hasCompiledSchedule;
        private FpgCompiledSkillSequence compiledSequence;
        private bool refreshQueued;
        private double lastUpdateTime;
        private double tickAccumulator;
        private float playbackSpeed = 1f;

        private ToolbarSearchField assetSearchField;
        private DropdownField typeFilter;
        private ListView actionAssetList;
        private DropdownField sequenceDropdown;
        private ListView payloadList;
        private Label payloadCountLabel;
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
        private Button addPayloadButton;
        private Button duplicatePayloadButton;
        private Button replacePayloadButton;
        private Button deletePayloadButton;
        private Button addEventButton;
        private Button duplicateEventButton;
        private Button copyEventsButton;
        private Button pasteEventsButton;
        private Button deleteEventButton;
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
            payloadList = rootVisualElement.Q<ListView>("payload-list");
            payloadCountLabel = rootVisualElement.Q<Label>("payload-count-label");
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
            addPayloadButton = rootVisualElement.Q<Button>("add-payload-button");
            duplicatePayloadButton = rootVisualElement.Q<Button>(
                "duplicate-payload-button");
            replacePayloadButton = rootVisualElement.Q<Button>(
                "replace-payload-button");
            deletePayloadButton = rootVisualElement.Q<Button>("delete-payload-button");
            addEventButton = rootVisualElement.Q<Button>("add-event-button");
            duplicateEventButton = rootVisualElement.Q<Button>(
                "duplicate-event-button");
            copyEventsButton = rootVisualElement.Q<Button>("copy-events-button");
            pasteEventsButton = rootVisualElement.Q<Button>("paste-events-button");
            deleteEventButton = rootVisualElement.Q<Button>("delete-event-button");
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

            payloadList.itemsSource = payloads;
            payloadList.fixedItemHeight = 38f;
            payloadList.makeItem = MakePayloadRow;
            payloadList.bindItem = BindPayloadRow;
            payloadList.selectionChanged += OnPayloadSelectionChanged;

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

            addPayloadButton.clicked += AddPayload;
            duplicatePayloadButton.clicked += DuplicatePayload;
            replacePayloadButton.clicked += ShowReplacePayloadMenu;
            deletePayloadButton.clicked += DeletePayload;
            addEventButton.clicked += ShowAddEventMenu;
            duplicateEventButton.clicked += DuplicateEvent;
            copyEventsButton.clicked += CopySelectedEvents;
            pasteEventsButton.clicked += PasteCopiedEvents;
            deleteEventButton.clicked += DeleteEvent;
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
            selectedAsset = asset;
            serializedAsset = new SerializedObject(asset);
            selectedSequenceIndex = 0;
            selectedPayloadIndex = -1;
            selectedEventIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            currentTick = 0;
            tickAccumulator = 0d;
            SessionState.SetString(
                SelectedAssetSessionKey,
                AssetDatabase.GetAssetPath(asset));
            RestorePreviewPrefab();
            if (revealInProject)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            SelectCurrentAssetInList();
            RefreshFromSerialized();
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
            refreshQueued = false;
            if (serializedAsset == null || serializedAsset.targetObject == null)
            {
                ClearSelectedData();
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            RefreshSequenceChoices();
            SerializedProperty sequence = FpgSkillSerializedAdapter.GetSequence(
                serializedAsset,
                selectedSequenceIndex);
            durationTicks = FpgSkillSerializedAdapter.GetDurationTicks(sequence);
            currentTick = Mathf.Clamp(currentTick, 0, durationTicks);

            payloads.Clear();
            payloads.AddRange(FpgSkillSerializedAdapter.ReadPayloads(sequence));
            events.Clear();
            events.AddRange(FpgSkillSerializedAdapter.ReadEvents(
                sequence,
                payloads,
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

            previewView.SetAnimation(
                FpgSkillSerializedAdapter.GetMainAnimation(sequence),
                compiledSequence);
            measuredAnimationDurationTicks =
                previewView.MeasuredAnimationDurationTicks;
            validation.Clear();
            validation.AddRange(FpgSkillSerializedAdapter.Validate(
                serializedAsset,
                selectedSequenceIndex,
                payloads,
                events,
                durationTicks,
                measuredAnimationDurationTicks,
                previewPrefabField.value as GameObject));
            if (!hasCompiledSchedule
                && !string.IsNullOrWhiteSpace(compileError)
                && !validation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error))
            {
                validation.Add(new FpgSkillValidationItem
                {
                    Severity = FpgSkillIssueSeverity.Error,
                    Message = "编译预览不可用：" + compileError
                });
            }

            selectedPayloadIndex = NormalizePayloadSelection(selectedPayloadIndex);
            
            SerializedProperty phases = sequence?.FindPropertyRelative("phases");
            if (selectedPhaseIndex < 0
                || phases == null
                || !phases.isArray
                || selectedPhaseIndex >= phases.arraySize)
            {
                selectedPhaseIndex = -1;
            }

            if (sequence == null)
            {
                selectedAnimationTrack = false;
            }
            selectedEventIndex = NormalizeEventSelection(selectedEventIndex);
            HashSet<int> validEventIndices = new HashSet<int>(
                events.Select(item => item.Index));
            eventSelection.Retain(validEventIndices);
            if (selectedEventIndex >= 0
                && !eventSelection.Contains(selectedEventIndex))
            {
                eventSelection.SetSingle(selectedEventIndex);
            }
            else
            {
                eventSelection.MakePrimary(selectedEventIndex);
            }
            payloadCountLabel.text = payloads.Count.ToString();
            payloadList.Rebuild();
            validationList.Rebuild();
            RefreshValidationSummary();
            RefreshAnimationLengthState(sequence);
            RefreshTimeline();
            RefreshPreview();
            RefreshInspector();
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
            timelineView.SetModel(durationTicks, models, blocks);
            timelineView.SetPlayhead(currentTick);
            if (selectedAnimationTrack)
            {
                timelineView.SelectBlock(
                    FpgSkillTimelineBlockKind.Animation,
                    0);
            }
            else if (selectedPhaseIndex >= 0)
            {
                timelineView.SelectBlock(
                    FpgSkillTimelineBlockKind.Phase,
                    selectedPhaseIndex);
            }
            else
            {
                timelineView.SelectEvents(
                    eventSelection.Items,
                    eventSelection.PrimaryEventIndex);
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
                    payloads,
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

            if (selectedPhaseIndex >= 0)
            {
                property = FpgSkillSerializedAdapter.GetPhaseProperty(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedPhaseIndex);
                inspectorTitle.text = "动作阶段 Inspector";
                AddPhaseInspector(property);
                return;
            }

            if (selectedEventIndex >= 0)
            {
                property = FpgSkillSerializedAdapter.GetEventProperty(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedEventIndex);
                FpgSkillEventRecord record = events.FirstOrDefault(item =>
                    item.Index == selectedEventIndex);
                inspectorTitle.text = record == null
                    ? "事件 Inspector"
                    : record.Kind + " Inspector";
                AddEventInspector(property, record);
                return;
            }

            if (selectedPayloadIndex >= 0)
            {
                property = FpgSkillSerializedAdapter.GetPayloadProperty(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedPayloadIndex);
                FpgSkillPayloadRecord record = payloads.FirstOrDefault(item =>
                    item.Index == selectedPayloadIndex);
                inspectorTitle.text = "载荷 Inspector";
                AddPayloadInspector(property, record);
                return;
            }

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
                    || iterator.isArray)
                {
                    continue;
                }

                AddTypedProperty(iterator.Copy(), iterator.displayName);
            }
        }

        private static bool IsEditorOwnedRootProperty(string propertyName)
        {
            switch (propertyName)
            {
                case "m_Script":
                case "skillId":
                case "displayName":
                case "sequences":
                case "payloadSlots":
                case "payloads":
                case "attackPayloads":
                case "slots":
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

        private void RefreshAssetState()
        {
            if (selectedAsset == null)
            {
                assetStateLabel.text = "未选择动作";
                return;
            }

            bool hasErrors = validation.Any(item =>
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
            addPayloadButton.SetEnabled(hasSequence
                && FpgSkillSerializedAdapter.GetPayloads(sequence) != null);
            duplicatePayloadButton.SetEnabled(selectedPayloadIndex >= 0);
            replacePayloadButton.SetEnabled(
                selectedPayloadIndex >= 0 && payloads.Count > 1);
            deletePayloadButton.SetEnabled(selectedPayloadIndex >= 0);
            addEventButton.SetEnabled(hasSequence
                && (FpgSkillSerializedAdapter.CanAddEventTrack(
                        sequence,
                        FpgSkillEventTrackKind.Generic)
                    || FpgSkillSerializedAdapter.CanAddEventTrack(
                        sequence,
                        FpgSkillEventTrackKind.Logic)
                    || FpgSkillSerializedAdapter.CanAddEventTrack(
                        sequence,
                        FpgSkillEventTrackKind.Presentation)
                    || FpgSkillSerializedAdapter.CanAddEventTrack(
                        sequence,
                        FpgSkillEventTrackKind.Warning)));
            duplicateEventButton.SetEnabled(selectedEventIndex >= 0);
            copyEventsButton.SetEnabled(selectedEventIndex >= 0);
            pasteEventsButton.SetEnabled(hasSequence && !eventClipboard.IsEmpty);
            deleteEventButton.SetEnabled(selectedEventIndex >= 0);
            captureAnimationLengthButton.SetEnabled(
                hasSequence && measuredAnimationDurationTicks > 0);
        }

        private void ClearSelectedData()
        {
            selectedAsset = null;
            serializedAsset = null;
            selectedSequenceIndex = -1;
            selectedPayloadIndex = -1;
            selectedEventIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            durationTicks = 120;
            currentTick = 0;
            payloads.Clear();
            events.Clear();
            compiledTriggers.Clear();
            hasCompiledSchedule = false;
            compiledSequence = default(FpgCompiledSkillSequence);
            previewExecution.Reset();
            previewSimulationFrame = null;
            validation.Clear();
            RefreshAnimationLengthState(null);
            payloadList?.Rebuild();
            validationList?.Rebuild();
            timelineView?.SetModel(
                durationTicks,
                Array.Empty<FpgSkillTimelineEventViewModel>(),
                Array.Empty<FpgSkillTimelineBlockViewModel>());
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
            selectedPayloadIndex = -1;
            selectedEventIndex = -1;
            selectedPhaseIndex = -1;
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

        private void OnPayloadSelectionChanged(
            IEnumerable<object> selection)
        {
            FpgSkillPayloadRecord record = selection
                .OfType<FpgSkillPayloadRecord>()
                .FirstOrDefault();
            selectedPayloadIndex = record == null ? -1 : record.Index;
            selectedEventIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            timelineView.SelectEvents(Array.Empty<int>(), -1);
            timelineView.SelectBlock(
                FpgSkillTimelineBlockKind.Animation,
                -1);
            RefreshInspector();
            RefreshButtons();
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

            if (item.EventIndex >= 0)
            {
                SelectEvent(item.EventIndex, true);
            }
            else if (item.PayloadIndex >= 0)
            {
                SelectPayload(item.PayloadIndex);
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
            if (item.EventIndex >= 0)
            {
                SelectEvent(item.EventIndex, false);
            }
        }

        private void OnTimelinePlayheadChanged(int tick)
        {
            Pause();
            SetCurrentTick(tick, true, false);
        }

        private void OnTimelineEventSelectionChanged(
            IReadOnlyList<int> eventIndices)
        {
            eventSelection.Set(
                eventIndices,
                timelineView.SelectedEventIndex);
            selectedEventIndex = NormalizeEventSelection(
                eventSelection.PrimaryEventIndex);
            selectedPayloadIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            if (selectedEventIndex >= 0)
            {
                FpgSkillEventRecord record = events.FirstOrDefault(item =>
                    item.Index == selectedEventIndex);
                if (record != null)
                {
                    SetCurrentTick(record.Tick, false, false);
                }
            }

            RefreshInspector();
            RefreshButtons();
        }

        private void OnTimelineEventsTickDeltaChanged(
            IReadOnlyList<int> eventIndices,
            int requestedDeltaTicks)
        {
            if (serializedAsset == null
                || !FpgSkillSerializedAdapter.MoveEventsByDelta(
                    serializedAsset,
                    selectedSequenceIndex,
                    eventIndices,
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
                eventIndices,
                timelineView.SelectedEventIndex);
            selectedEventIndex = eventSelection.PrimaryEventIndex;
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
            selectedPhaseIndex =
                kind == FpgSkillTimelineBlockKind.Phase ? index : -1;
            selectedEventIndex = -1;
            selectedPayloadIndex = -1;
            eventSelection.Clear();
            RefreshFromSerialized();
        }


        private void OnTimelineEventOrderDeltaChanged(
            int eventIndex,
            int requestedDelta)
        {
            if (serializedAsset == null
                || !FpgSkillSerializedAdapter.MoveEventOrder(
                    serializedAsset,
                    selectedSequenceIndex,
                    eventIndex,
                    requestedDelta))
            {
                RefreshTimeline();
                return;
            }

            selectedEventIndex = eventIndex;
            selectedPayloadIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            eventSelection.SetSingle(eventIndex);
            RefreshFromSerialized();
        }

        private void OnTimelineBlockSelected(
            FpgSkillTimelineBlockKind kind,
            int index)
        {
            Pause();
            selectedEventIndex = -1;
            selectedPayloadIndex = -1;
            eventSelection.Clear();
            selectedAnimationTrack =
                kind == FpgSkillTimelineBlockKind.Animation;
            selectedPhaseIndex =
                kind == FpgSkillTimelineBlockKind.Phase
                    ? index
                    : -1;
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

            FpgSkillPayloadRecord payload = payloads.FirstOrDefault(item =>
                item.Index == selectedPayloadIndex) ?? payloads.FirstOrDefault();
            int eventIndex = FpgSkillSerializedAdapter.AddEvent(
                serializedAsset,
                selectedSequenceIndex,
                request.Tick,
                payload,
                request.Track,
                request.DurationTicks);
            if (eventIndex < 0)
            {
                statusLabel.text = "当前轨道不能创建事件。";
                return;
            }

            eventSelection.SetSingle(eventIndex);
            selectedEventIndex = eventIndex;
            selectedPayloadIndex = -1;
            currentTick = request.Tick;
            RefreshFromSerialized();
            SelectEvent(eventIndex, true);
        }

        private void SelectEvent(int eventIndex, bool frame)
        {
            selectedEventIndex = NormalizeEventSelection(eventIndex);
            selectedPayloadIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            eventSelection.SetSingle(selectedEventIndex);
            timelineView.SelectEvents(
                eventSelection.Items,
                eventSelection.PrimaryEventIndex);
            if (selectedEventIndex >= 0)
            {
                FpgSkillEventRecord record = events.First(item =>
                    item.Index == selectedEventIndex);
                SetCurrentTick(record.Tick, false, frame);
            }

            RefreshInspector();
            RefreshButtons();
        }

        private void SelectPayload(int payloadIndex)
        {
            selectedPayloadIndex = NormalizePayloadSelection(payloadIndex);
            selectedEventIndex = -1;
            selectedPhaseIndex = -1;
            selectedAnimationTrack = false;
            eventSelection.Clear();
            timelineView.SelectEvents(Array.Empty<int>(), -1);
            timelineView.SelectBlock(
                FpgSkillTimelineBlockKind.Animation,
                -1);
            int listIndex = payloads.FindIndex(item =>
                item.Index == selectedPayloadIndex);
            if (listIndex >= 0)
            {
                payloadList.SetSelectionWithoutNotify(new[] { listIndex });
            }

            RefreshInspector();
            RefreshButtons();
        }

        private void AddPayload()
        {
            if (serializedAsset == null)
            {
                return;
            }

            int index = FpgSkillSerializedAdapter.AddPayload(
                serializedAsset,
                selectedSequenceIndex);
            if (index < 0)
            {
                statusLabel.text = "当前序列没有可编辑的载荷槽数组。";
                return;
            }

            selectedPayloadIndex = index;
            selectedEventIndex = -1;
            RefreshFromSerialized();
            SelectPayload(index);
        }

        private void DuplicatePayload()
        {
            if (serializedAsset == null || selectedPayloadIndex < 0)
            {
                return;
            }

            int index = FpgSkillSerializedAdapter.DuplicatePayload(
                serializedAsset,
                selectedSequenceIndex,
                selectedPayloadIndex);
            if (index >= 0)
            {
                selectedPayloadIndex = index;
                RefreshFromSerialized();
                SelectPayload(index);
            }
        }

        private void ShowReplacePayloadMenu()
        {
            if (serializedAsset == null
                || selectedPayloadIndex < 0
                || payloads.Count < 2)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            for (int index = 0; index < payloads.Count; index++)
            {
                FpgSkillPayloadRecord candidate = payloads[index];
                if (candidate.Index == selectedPayloadIndex)
                {
                    continue;
                }

                int targetPayloadIndex = candidate.Index;
                GUIContent label = new GUIContent(
                    candidate.Name + " · " + candidate.Kind);
                menu.AddItem(
                    label,
                    false,
                    () => ReplaceSelectedPayloadReferences(targetPayloadIndex));
            }

            menu.DropDown(replacePayloadButton.worldBound);
        }

        private void ReplaceSelectedPayloadReferences(int targetPayloadIndex)
        {
            int sourcePayloadIndex = selectedPayloadIndex;
            int replacementCount = FpgSkillSerializedAdapter.ReplacePayloadReferences(
                serializedAsset,
                selectedSequenceIndex,
                sourcePayloadIndex,
                targetPayloadIndex);
            RefreshFromSerialized();
            SelectPayload(sourcePayloadIndex);
            statusLabel.text = replacementCount > 0
                ? "已替换 " + replacementCount + " 个载荷引用，可删除原槽。"
                : "当前载荷槽没有可替换的引用。";
        }

        private void DeletePayload()
        {
            if (serializedAsset == null || selectedPayloadIndex < 0)
            {
                return;
            }

            FpgSkillPayloadRecord payload = payloads.FirstOrDefault(item =>
                item.Index == selectedPayloadIndex);
            if (payload != null && payload.UseCount > 0)
            {
                EditorUtility.DisplayDialog(
                    "无法删除载荷槽",
                    "该载荷槽仍被 " + payload.UseCount + " 个事件引用。请先替换或删除这些事件。",
                    "确定");
                return;
            }

            if (FpgSkillSerializedAdapter.DeletePayload(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedPayloadIndex))
            {
                selectedPayloadIndex = -1;
                RefreshFromSerialized();
            }
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
            if (FpgSkillSerializedAdapter.CanAddEventTrack(
                    sequence,
                    FpgSkillEventTrackKind.Logic))
            {
                if (payloads.Count == 0)
                {
                    menu.AddDisabledItem(
                        new GUIContent("逻辑载荷/没有可用载荷"));
                }
                else
                {
                    for (int index = 0; index < payloads.Count; index++)
                    {
                        FpgSkillPayloadRecord payload = payloads[index];
                        int payloadIndex = payload.Index;
                        string payloadName = string.IsNullOrWhiteSpace(payload.Name)
                            ? "载荷 " + (index + 1)
                            : payload.Name;
                        string payloadKind = string.IsNullOrWhiteSpace(payload.Kind)
                            ? "未分类"
                            : payload.Kind;
                        menu.AddItem(
                            new GUIContent(
                                "逻辑载荷/"
                                + payloadName
                                + " · "
                                + payloadKind
                                + " ("
                                + (index + 1)
                                + ")"),
                            false,
                            () => AddEvent(
                                FpgSkillEventTrackKind.Logic,
                                payloadIndex));
                    }
                }
            }
            else
            {
                menu.AddDisabledItem(
                    new GUIContent("逻辑载荷/当前序列不支持"));
            }

            AddEventMenuItem(
                menu,
                "演出事件",
                sequence,
                FpgSkillEventTrackKind.Presentation);
            AddEventMenuItem(
                menu,
                "预警区间",
                sequence,
                FpgSkillEventTrackKind.Warning);
            AddEventMenuItem(
                menu,
                "高级/通用事件",
                sequence,
                FpgSkillEventTrackKind.Generic);
            menu.DropDown(addEventButton.worldBound);
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

        private void AddEvent(
            FpgSkillEventTrackKind track,
            int payloadIndex = -1)
        {
            if (serializedAsset == null)
            {
                return;
            }

            FpgSkillPayloadRecord payload = payloads.FirstOrDefault(item =>
                item.Index == payloadIndex);
            if (payload == null
                && (track == FpgSkillEventTrackKind.Logic
                    || track == FpgSkillEventTrackKind.Generic))
            {
                payload = payloads.FirstOrDefault(item =>
                    item.Index == selectedPayloadIndex)
                    ?? payloads.FirstOrDefault();
            }

            int index = FpgSkillSerializedAdapter.AddEvent(
                serializedAsset,
                selectedSequenceIndex,
                currentTick,
                payload,
                track);
            if (index < 0)
            {
                statusLabel.text = "当前序列没有可编辑的事件数组。";
                return;
            }

            selectedEventIndex = index;
            selectedPayloadIndex = -1;
            RefreshFromSerialized();
            SelectEvent(index, true);
        }

        private void DuplicateEvent()
        {
            if (serializedAsset == null || selectedEventIndex < 0)
            {
                return;
            }

            int index = FpgSkillSerializedAdapter.DuplicateEvent(
                serializedAsset,
                selectedSequenceIndex,
                selectedEventIndex,
                durationTicks);
            if (index >= 0)
            {
                selectedEventIndex = index;
                RefreshFromSerialized();
                SelectEvent(index, true);
            }
        }

        private void DeleteEvent()
        {
            if (serializedAsset == null || selectedEventIndex < 0)
            {
                return;
            }

            IReadOnlyList<int> selectedIndices = eventSelection.Count > 0
                ? eventSelection.Items
                : new[] { selectedEventIndex };
            if (FpgSkillSerializedAdapter.DeleteEvents(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedIndices))
            {
                int deletedCount = selectedIndices.Count;
                selectedEventIndex = -1;
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
                statusLabel.text = "当前技能未通过运行时编译，无法播放触发逻辑。";
                return;
            }

            if (validation.Any(item =>
                    item.Severity == FpgSkillIssueSeverity.Error))
            {
                statusLabel.text = "当前技能存在阻塞错误，无法播放触发逻辑。";
                return;
            }

            if (currentTick >= durationTicks)
            {
                SetCurrentTick(0, false, false);
            }

            isPlaying = true;
            tickAccumulator = 0d;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void Pause()
        {
            isPlaying = false;
            tickAccumulator = 0d;
        }

        private void Step(int delta)
        {
            Pause();
            SetCurrentTick(currentTick + delta, true, true);
        }

        private void SetCurrentTick(int tick, bool writeLog, bool frame)
        {
            int normalizedTick = Mathf.Clamp(tick, 0, durationTicks);
            if (hasCompiledSchedule
                && !previewExecution.AdvanceTo(
                    normalizedTick,
                    out string executionError))
            {
                statusLabel.text = executionError;
                return;
            }

            currentTick = normalizedTick;
            timelineView.SetPlayhead(currentTick);
            if (frame)
            {
                timelineView.FrameTick(currentTick);
            }

            RefreshPreview();
            if (writeLog)
            {
                LogExecutionResults();
            }
        }

        private void OnEditorTick()
        {
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
                        nextTick = 0;
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
                    EventIndex = trigger.EventIndex,
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
                && selectedEventIndex >= 0)
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

        private int NormalizePayloadSelection(int index)
        {
            return payloads.Any(item => item.Index == index) ? index : -1;
        }

        private int NormalizeEventSelection(int index)
        {
            return events.Any(item => item.Index == index) ? index : -1;
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

        private static VisualElement MakePayloadRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("payload-row");
            VisualElement swatch = new VisualElement { name = "payload-swatch" };
            swatch.AddToClassList("payload-swatch");
            row.Add(swatch);
            VisualElement copy = new VisualElement();
            copy.AddToClassList("payload-copy");
            Label name = new Label { name = "payload-name" };
            name.AddToClassList("payload-name");
            Label meta = new Label { name = "payload-meta" };
            meta.AddToClassList("payload-meta");
            copy.Add(name);
            copy.Add(meta);
            row.Add(copy);
            Label badge = new Label { name = "payload-usage" };
            badge.AddToClassList("type-badge");
            row.Add(badge);
            return row;
        }

        private void BindPayloadRow(VisualElement element, int index)
        {
            if (index < 0 || index >= payloads.Count)
            {
                return;
            }

            FpgSkillPayloadRecord record = payloads[index];
            element.Q<VisualElement>("payload-swatch").style.backgroundColor =
                record.Color;
            element.Q<Label>("payload-name").text = record.Name;
            element.Q<Label>("payload-meta").text = record.Kind;
            element.Q<Label>("payload-usage").text = record.UseCount + " 次";
            element.tooltip = record.Kind
                + " · "
                + record.HitShape
                + " · 使用 "
                + record.UseCount
                + " 次";
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
            public int EventIndex;
            public string Message;
        }

    

        private void CopySelectedEvents()
        {
            if (serializedAsset == null || selectedEventIndex < 0)
            {
                return;
            }

            IReadOnlyList<int> selectedIndices = eventSelection.Count > 0
                ? eventSelection.Items
                : new[] { selectedEventIndex };
            if (FpgSkillSerializedAdapter.CopyEvents(
                    serializedAsset,
                    selectedSequenceIndex,
                    selectedIndices,
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

            List<int> pasted = FpgSkillSerializedAdapter.PasteEvents(
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
            selectedEventIndex = eventSelection.PrimaryEventIndex;
            selectedPayloadIndex = -1;
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

        private void AddPhaseInspector(SerializedProperty phase)
        {
            if (phase == null)
            {
                AddInspectorEmptyState("当前动作阶段无法读取。");
                return;
            }

            SerializedProperty kind = phase.FindPropertyRelative("kind");
            SerializedProperty start = phase.FindPropertyRelative("startTick");
            SerializedProperty end = phase.FindPropertyRelative("endTick");
            AddPhaseKindProperty(kind);

            int startTick = start == null ? 0 : start.intValue;
            int endTick = end == null ? startTick : end.intValue;
            AddPhaseRangeFields(
                selectedPhaseIndex,
                startTick,
                endTick);
            AddReadOnlyInspectorValue(
                "阶段持续时间",
                FormatTickDuration(Mathf.Max(0, endTick - startTick)),
                "动作阶段只标记时间结构，不会自行造成伤害、"
                    + "取消窗口、无敌或霸体效果。");
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

        private void AddPhaseKindProperty(SerializedProperty property)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            List<string> labels = new List<string>
            {
                "未配置（无效）",
                "前摇（Startup）",
                "生效（Active）",
                "后摇（Recovery）"
            };
            List<FpgSkillPhaseKind> values = new List<FpgSkillPhaseKind>
            {
                FpgSkillPhaseKind.None,
                FpgSkillPhaseKind.Startup,
                FpgSkillPhaseKind.Active,
                FpgSkillPhaseKind.Recovery
            };
            FpgSkillPhaseKind current = Enum.IsDefined(
                typeof(FpgSkillPhaseKind),
                property.enumValueIndex)
                    ? (FpgSkillPhaseKind)property.enumValueIndex
                    : FpgSkillPhaseKind.None;
            int selectedIndex = Mathf.Max(0, values.IndexOf(current));
            DropdownField field = new DropdownField(
                "动作阶段",
                labels,
                selectedIndex)
            {
                tooltip = "前摇、生效、后摇用于标记动作时间结构；"
                    + "阶段本身不会触发伤害或附加战斗状态。"
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
                        "修改动作阶段类型");
                }
            });
            inspectorContent.Add(field);
        }

        private void AddPhaseRangeFields(
            int phaseIndex,
            int startTick,
            int endTick)
        {
            IntegerField startField = new IntegerField("开始帧（Tick）")
            {
                isDelayed = true,
                tooltip = "拖动阶段左侧手柄或在此输入开始帧；"
                    + "范围会受相邻阶段和序列边界约束。"
            };
            IntegerField endField = new IntegerField("结束帧（Tick）")
            {
                isDelayed = true,
                tooltip = "拖动阶段右侧手柄或在此输入结束帧；"
                    + "范围会受相邻阶段和序列边界约束。"
            };
            startField.SetValueWithoutNotify(startTick);
            endField.SetValueWithoutNotify(endTick);
            startField.RegisterValueChangedCallback(evt =>
                CommitTimelineBlockRange(
                    FpgSkillTimelineBlockKind.Phase,
                    phaseIndex,
                    FpgSkillTimelineBlockEditMode.ResizeStart,
                    evt.newValue,
                    endField.value));
            endField.RegisterValueChangedCallback(evt =>
                CommitTimelineBlockRange(
                    FpgSkillTimelineBlockKind.Phase,
                    phaseIndex,
                    FpgSkillTimelineBlockEditMode.ResizeEnd,
                    startField.value,
                    evt.newValue));
            inspectorContent.Add(startField);
            inspectorContent.Add(endField);
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

            AddReadOnlyEventType(record.Track);
            GameObject previewPrefab = previewPrefabField.value as GameObject;
            switch (record.Track)
            {
                case FpgSkillEventTrackKind.Logic:
                case FpgSkillEventTrackKind.Generic:
                {
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("tick"),
                        "触发 Tick");
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("authoredOrdinal"),
                        "同 Tick 顺序");

                    SerializedProperty payloadReference =
                        eventProperty.FindPropertyRelative("payloadSlotId");
                    AddPayloadReferenceChoiceProperty(
                        payloadReference,
                        record.Index,
                        FpgSkillAuthoringChoices.BuildPayloadChoices(
                            payloads,
                            payloadReference == null
                                ? string.Empty
                                : payloadReference.stringValue));
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("targetSource"),
                        "目标来源");
                    AddStringChoiceProperty(
                        eventProperty.FindPropertyRelative("socketId"),
                        "Socket",
                        FpgSkillAuthoringChoices.BuildSocketChoices(
                            previewPrefab,
                            record.SocketId),
                        "修改事件 Socket");
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("targetOffset"),
                        "目标偏移");
                    break;
                }

                case FpgSkillEventTrackKind.Presentation:
                {
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("tick"),
                        "触发 Tick");
                    AddTypedProperty(
                        eventProperty.FindPropertyRelative("authoredOrdinal"),
                        "同 Tick 顺序");

                    SerializedProperty cue =
                        eventProperty.FindPropertyRelative("cueId");
                    AddStringChoiceProperty(
                        cue,
                        "演出类型",
                        FpgSkillAuthoringChoices.BuildCueChoices(
                            cue == null ? string.Empty : cue.stringValue),
                        "修改演出类型");
                    AddStringChoiceProperty(
                        eventProperty.FindPropertyRelative("socketId"),
                        "Socket",
                        FpgSkillAuthoringChoices.BuildSocketChoices(
                            previewPrefab,
                            record.SocketId),
                        "修改演出 Socket");

                    SerializedProperty binding =
                        eventProperty.FindPropertyRelative(
                            "bindGameplayEventId");
                    AddStringChoiceProperty(
                        binding,
                        "提交结果绑定",
                        FpgSkillAuthoringChoices.BuildGameplayEventChoices(
                            events,
                            binding == null
                                ? string.Empty
                                : binding.stringValue),
                        "修改演出提交结果绑定");
                    break;
                }

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

        private void AddReadOnlyEventType(FpgSkillEventTrackKind track)
        {
            List<string> choices = new List<string>
            {
                "逻辑事件",
                "演出事件",
                "预警事件",
                "通用事件"
            };
            int selectedIndex;
            switch (track)
            {
                case FpgSkillEventTrackKind.Presentation:
                    selectedIndex = 1;
                    break;
                case FpgSkillEventTrackKind.Warning:
                    selectedIndex = 2;
                    break;
                case FpgSkillEventTrackKind.Generic:
                    selectedIndex = 3;
                    break;
                default:
                    selectedIndex = 0;
                    break;
            }

            DropdownField field = new DropdownField(
                "事件类型",
                choices,
                selectedIndex);
            field.SetEnabled(false);
            inspectorContent.Add(field);
        }

        private void AddPayloadInspector(
            SerializedProperty payload,
            FpgSkillPayloadRecord record)
        {
            if (payload == null)
            {
                AddInspectorEmptyState("当前载荷无法读取。");
                return;
            }

            AddTypedProperty(
                payload.FindPropertyRelative("displayName"),
                "显示名称");
            SerializedProperty kind = payload.FindPropertyRelative("kind");
            AddPayloadKindProperty(
                kind,
                record == null ? selectedPayloadIndex : record.Index);

            string kindName = GetSerializedEnumName(kind);
            switch (kindName)
            {
                case "PelletRay":
                    AddPayloadProperties(
                        payload,
                        "ammoCost", "弹药消耗",
                        "baseDamage", "基础伤害",
                        "breakDamage", "削韧伤害",
                        "weakpointDamageMultiplierBasisPoints", "弱点伤害倍率（万分比）",
                        "weakpointBreakMultiplierBasisPoints", "弱点削韧倍率（万分比）",
                        "queryMode", "查询方式",
                        "pelletCount", "散射数量",
                        "additionalPenetrationCount", "额外穿透数量",
                        "allowedTargetKinds", "允许目标");
                    break;

                case "AreaAtFirstSurface":
                    AddPayloadProperties(
                        payload,
                        "ammoCost", "弹药消耗",
                        "baseDamage", "基础伤害",
                        "breakDamage", "削韧伤害",
                        "weakpointDamageMultiplierBasisPoints", "弱点伤害倍率（万分比）",
                        "weakpointBreakMultiplierBasisPoints", "弱点削韧倍率（万分比）",
                        "queryMode", "查询方式",
                        "areaCombatantLimit", "战斗单位上限",
                        "areaProjectileLimit", "弹体上限",
                        "allowedTargetKinds", "允许目标");
                    break;

                case "ReloadCommit":
                    break;

                case "Projectile":
                    AddPayloadProperties(
                        payload,
                        "threatDefinitionId", "威胁定义",
                        "baseDamage", "基础伤害",
                        "breakDamage", "削韧伤害",
                        "weakpointDamageMultiplierBasisPoints", "弱点伤害倍率（万分比）",
                        "weakpointBreakMultiplierBasisPoints", "弱点削韧倍率（万分比）",
                        "projectileDefinitionId", "弹体定义",
                        "projectileCount", "弹体数量",
                        "projectileFlightTicks", "飞行 Tick",
                        "projectileLifetimeTicks", "生命周期 Tick",
                        "projectileMaxHitPoints", "弹体生命",
                        "projectileInterceptable", "可拦截",
                        "projectileBudgetUnits", "容量单位",
                        "projectilePresentationKey", "表现类型",
                        "projectileSweepRadiusKey", "扫掠半径类型");
                    break;

                case "TimedImpact":
                    AddPayloadProperties(
                        payload,
                        "threatDefinitionId", "威胁定义",
                        "baseDamage", "基础伤害",
                        "breakDamage", "削韧伤害",
                        "weakpointDamageMultiplierBasisPoints", "弱点伤害倍率（万分比）",
                        "weakpointBreakMultiplierBasisPoints", "弱点削韧倍率（万分比）",
                        "timedImpactTargetPolicy", "目标策略",
                        "timedImpactDelayTicks", "延迟 Tick",
                        "timedImpactPresentationKey", "表现类型");
                    break;

                case "Summon":
                    AddPayloadProperties(
                        payload,
                        "summonCandidates", "召唤候选",
                        "summonCandidateWeights", "候选权重",
                        "summonOccupancyMode", "占位方式",
                        "summonPlacementMode", "放置方式",
                        "summonOwnerOutcome", "施法者结果",
                        "maxSummonsPerOwner", "单个施法者上限",
                        "maxTotalSummonsPerEncounter", "战斗总上限",
                        "maxSummonRecursionDepth", "递归深度上限");
                    break;

                default:
                    AddRemainingPayloadProperties(payload);
                    break;
            }
        }

        private void AddPayloadKindProperty(
            SerializedProperty property,
            int payloadIndex)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

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
                AddBoundProperty(property, "载荷类型");
                return;
            }

            EnumField field = new EnumField("载荷类型", currentValue);
            field.RegisterValueChangedCallback(evt =>
                ApplyPayloadKindChoice(payloadIndex, evt.newValue));
            inspectorContent.Add(field);
        }


        private void AddPayloadProperties(
            SerializedProperty payload,
            params string[] namesAndLabels)
        {
            for (int index = 0;
                index + 1 < namesAndLabels.Length;
                index += 2)
            {
                AddTypedProperty(
                    payload.FindPropertyRelative(namesAndLabels[index]),
                    namesAndLabels[index + 1]);
            }
        }

        private void AddRemainingPayloadProperties(SerializedProperty payload)
        {
            SerializedProperty iterator = payload.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren)
                   && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != payload.depth + 1
                    || IsHiddenStableReference(iterator.name)
                    || iterator.name == "displayName"
                    || iterator.name == "kind")
                {
                    continue;
                }

                AddTypedProperty(iterator.Copy(), iterator.displayName);
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

        private void AddPayloadReferenceChoiceProperty(
            SerializedProperty property,
            int eventIndex,
            IList<FpgSkillAuthoringChoice> choices)
        {
            if (property == null
                || property.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            inspectorContent.Add(
                CreateStringChoiceField(
                    property,
                    "载荷",
                    choices,
                    "替换事件载荷",
                    value => ApplyPayloadReferenceChoice(
                        eventIndex,
                        value)));
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
            property.boxedValue = value;
            ApplyInspectorChanges();
        }

        private void ApplyPayloadKindChoice(
            int payloadIndex,
            Enum value)
        {
            if (serializedAsset == null
                || serializedAsset.targetObject == null
                || value == null)
            {
                return;
            }

            serializedAsset.UpdateIfRequiredOrScript();
            SerializedProperty payload =
                FpgSkillSerializedAdapter.GetPayloadProperty(
                    serializedAsset,
                    selectedSequenceIndex,
                    payloadIndex);
            SerializedProperty kind = payload == null
                ? null
                : payload.FindPropertyRelative("kind");
            if (kind == null
                || kind.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            int enumValueIndex = Array.IndexOf(
                kind.enumNames,
                value.ToString());
            if (!FpgSkillSerializedAdapter.SetPayloadKindAndNormalize(
                    serializedAsset,
                    selectedSequenceIndex,
                    payloadIndex,
                    enumValueIndex))
            {
                statusLabel.text = "无法切换载荷类型。";
                QueueSerializedRefresh();
                return;
            }

            QueueSerializedRefresh();
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

        private void ApplyPayloadReferenceChoice(
            int eventIndex,
            string payloadId)
        {
            FpgSkillPayloadRecord payload = payloads.Find(item =>
                item != null
                && string.Equals(
                    item.Id,
                    payloadId,
                    StringComparison.Ordinal));
            if (payload == null
                || !FpgSkillSerializedAdapter.SetEventPayloadReference(
                    serializedAsset,
                    selectedSequenceIndex,
                    eventIndex,
                    payload.Index))
            {
                statusLabel.text = "无法替换事件载荷。";
                return;
            }

            QueueSerializedRefresh();
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
                case "phaseId":
                case "slotId":
                case "payloadSlotId":
                case "cueId":
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
