using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public sealed class FpgRoomEditorWindow : EditorWindow
    {
        private const string LayoutPath = "Assets/FPGDemo/Editor/LevelAuthoring/FpgRoomEditor.uxml";
        private const string SelectedRoomSessionKey = "FPGDemo.RoomAuthoring.SelectedRoomGuid";
        private const string SelectedScenarioSessionKey = "FPGDemo.RoomAuthoring.SelectedScenarioGuid";

        private readonly List<FpgRoomRecord> allRooms = new List<FpgRoomRecord>();
        private readonly List<FpgRoomRecord> filteredRooms = new List<FpgRoomRecord>();
        private readonly List<FpgRoomMarkerHandle> markers = new List<FpgRoomMarkerHandle>();
        private readonly List<FpgRoomValidationItem> validation = new List<FpgRoomValidationItem>();

        private FpgRoomSceneTool sceneTool;
        private ScriptableObject selectedRoom;
        private ScriptableObject selectedScenario;
        private SerializedObject serializedRoom;
        private bool refreshQueued;

        private FpgEncounterProfile formalPreviewProfile;
        private FpgEncounterOverrideDefinition formalPreviewOverride;
        private long formalPreviewSeed = 1L;
        private int formalPreviewDepth;
        private int formalPreviewDifficultyBasisPoints =
            FpgEncounterRunContext.BasisPointsOne;
        private int formalPreviewRoomVisitOrdinal;

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
        private ObjectField scenarioField;
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
            sceneTool?.Dispose();
            sceneTool = new FpgRoomSceneTool();
            sceneTool.SelectionChanged += OnSceneMarkerSelectionChanged;
            sceneTool.RoomChanged += QueueCurrentRoomRefresh;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
            if (sceneTool != null)
            {
                sceneTool.SelectionChanged -= OnSceneMarkerSelectionChanged;
                sceneTool.RoomChanged -= QueueCurrentRoomRefresh;
                sceneTool.Dispose();
                sceneTool = null;
            }
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
            RestoreScenarioSelection();
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
            scenarioField = rootVisualElement.Q<ObjectField>("scenario-field");

            markerToolButtons.Clear();
            markerToolButtons[FpgRoomMarkerKind.Exit] = rootVisualElement.Q<Button>("place-exit-button");
            markerToolButtons[FpgRoomMarkerKind.PlayerEntry] = rootVisualElement.Q<Button>("place-player-button");
            markerToolButtons[FpgRoomMarkerKind.EnemySpawn] = rootVisualElement.Q<Button>("place-enemy-button");
            markerToolButtons[FpgRoomMarkerKind.Destructible] = rootVisualElement.Q<Button>("place-destructible-button");
            markerToolButtons[FpgRoomMarkerKind.Reachable] = rootVisualElement.Q<Button>("place-reachable-button");
        }

        private void ConfigureLists()
        {
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

            scenarioField.objectType = FpgRoomAuthoringSchema.ScenarioType ?? typeof(ScriptableObject);
            scenarioField.allowSceneObjects = false;
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

            rootVisualElement.Q<Button>("create-room-button").clicked += CreateRoom;
            rootVisualElement.Q<Button>("duplicate-room-button").clicked += DuplicateRoom;
            rootVisualElement.Q<Button>("save-room-button").clicked += SaveRoom;
            rootVisualElement.Q<Button>("frame-room-button").clicked += () => sceneTool?.FrameSelection();
            rootVisualElement.Q<Button>("play-room-button").clicked += StartPlaytest;
            rootVisualElement.Q<Button>("duplicate-marker-button").clicked += () => sceneTool?.DuplicateSelectedMarker();
            rootVisualElement.Q<Button>("delete-marker-button").clicked += () => sceneTool?.DeleteSelectedMarker();

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
            BindVisibility("show-reachable-toggle", FpgRoomMarkerKind.Reachable);

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

            scenarioField.RegisterValueChangedCallback(evt =>
            {
                selectedScenario = evt.newValue as ScriptableObject;
                string path = AssetDatabase.GetAssetPath(selectedScenario);
                SessionState.SetString(SelectedScenarioSessionKey, AssetDatabase.AssetPathToGUID(path));
                RefreshValidation();
            });
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
            FpgRoomRecord record = selection.OfType<FpgRoomRecord>().FirstOrDefault();
            if (record != null)
            {
                SelectRoom(record.Asset);
            }
        }

        private void SelectRoom(ScriptableObject room)
        {
            selectedRoom = room;
            serializedRoom = room == null ? null : new SerializedObject(room);
            SessionState.SetString(
                SelectedRoomSessionKey,
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(room)));
            sceneTool?.SetRoom(room);
            RebuildRoomDetails();
            RefreshMarkers();
            RefreshValidation();

            int roomIndex = filteredRooms.FindIndex(record => record.Asset == room);
            if (roomIndex >= 0)
            {
                roomList.SetSelectionWithoutNotify(new[] { roomIndex });
                roomList.ScrollToItem(roomIndex);
            }

            statusLabel.text = room == null
                ? "No room selected."
                : $"Editing: {FpgRoomAuthoringSchema.GetString(room, "displayName")}"
                  + " (changes are written to the selected room asset).";
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
                "roomId", "displayName", "designerNotes", "environmentPrefab", "mainGroup", "tags"
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

            if (propertyName == "environmentPrefab")
            {
                sceneTool?.RebuildPreview();
            }

            QueueCurrentRoomRefresh();
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
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                {
                    if (serializedRoom != null && serializedRoom.hasModifiedProperties)
                    {
                        EditorUtility.SetDirty(selectedRoom);
                    }
                    if (handle.Kind == FpgRoomMarkerKind.Destructible)
                    {
                        sceneTool?.RebuildPreview();
                    }
                    QueueCurrentRoomRefresh();
                    SceneView.RepaintAll();
                });
                markerDetails.Add(field);
            }
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
            }

            validation.AddRange(FpgRoomAuthoringSchema.ValidateScenarioCompatibility(selectedRoom, selectedScenario));
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
            if (selectedRoom == null)
            {
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(selectedRoom);
            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string defaultName = selectedRoom.name + "_Copy";
            string path = EditorUtility.SaveFilePanelInProject(
                "Duplicate Room",
                defaultName,
                "asset",
                "The copy receives a new room ID while marker IDs are preserved.",
                directory);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ScriptableObject copy = Instantiate(selectedRoom);
            copy.name = Path.GetFileNameWithoutExtension(path);
            SerializedObject serialized = new SerializedObject(copy);
            serialized.FindProperty("roomId").stringValue = GenerateRoomId();
            SerializedProperty displayName = serialized.FindProperty("displayName");
            displayName.stringValue = string.IsNullOrWhiteSpace(displayName.stringValue)
                ? copy.name
                : displayName.stringValue + " Copy";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(copy);
            RefreshRoomAssets();
            SelectRoom(copy);
            Selection.activeObject = copy;
        }

        private void SaveRoom()
        {
            if (selectedRoom == null)
            {
                return;
            }

            AssetDatabase.SaveAssetIfDirty(selectedRoom);
            statusLabel.text = $"Saved: {AssetDatabase.GetAssetPath(selectedRoom)}";
        }

        private void StartPlaytest()
        {
            RefreshValidation();
            FpgRoomValidationItem blocking = validation.FirstOrDefault(item =>
                item.Severity == FpgRoomValidationSeverity.Error);
            if (blocking != null)
            {
                EditorUtility.DisplayDialog("Cannot Start Playtest", "Fix validation errors first: " + blocking.Message, "OK");
                return;
            }

            if (selectedScenario == null)
            {
                EditorUtility.DisplayDialog("Cannot Start Playtest", "Select a D0 encounter scenario.", "OK");
                return;
            }

            if (!FpgRoomPlaytestController.TryStart(selectedRoom, selectedScenario, out string error))
            {
                EditorUtility.DisplayDialog("Cannot Start Playtest", error, "OK");
            }
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

        private void RestoreScenarioSelection()
        {
            string path = AssetDatabase.GUIDToAssetPath(
                SessionState.GetString(SelectedScenarioSessionKey, string.Empty));
            Type type = FpgRoomAuthoringSchema.ScenarioType;
            selectedScenario = type == null
                ? null
                : AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
            if (selectedScenario == null)
            {
                FPG.Demo.Unity.BattleScenarioConfig defaultConfig =
                    AssetDatabase.LoadAssetAtPath<FPG.Demo.Unity.BattleScenarioConfig>(
                        "Assets/FPGDemo/Config/BattleScenarioConfig.asset");
                selectedScenario = defaultConfig == null
                    ? null
                    : defaultConfig.AuthoredScenario;
            }

            scenarioField.SetValueWithoutNotify(selectedScenario);
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

