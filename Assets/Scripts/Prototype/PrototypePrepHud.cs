using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NewFPG.Battle;

namespace NewFPG.Prototype
{
    public sealed class PrototypePrepHud : MonoBehaviour
    {
        private const int QueueCapacity = 10;
        private const float SlotWidth = 132f;
        private const float SlotHeight = 88f;
        private const float SlotSpacing = 8f;
        private static readonly Color PanelColor = new Color(0.075f, 0.068f, 0.06f, 0.97f);
        private static readonly Color SubPanelColor = new Color(0.11f, 0.1f, 0.087f, 0.96f);
        private static readonly Color SlotColor = new Color(0.13f, 0.12f, 0.105f, 0.98f);
        private static readonly Color SelectedColor = new Color(0.34f, 0.24f, 0.12f, 0.98f);
        private static readonly Color DisabledColor = new Color(0.085f, 0.08f, 0.072f, 0.92f);

        [SerializeField] private ArtifactCatalog catalog;
        [SerializeField] private PrepLoadoutState loadoutState = new PrepLoadoutState();

        private Canvas canvas;
        private GameObject root;
        private RectTransform poolContent;
        private RectTransform queueRoot;
        private RectTransform queueSlotsRoot;
        private RectTransform queueBlocksRoot;
        private Text capacityText;
        private Text detailText;
        private Text statusText;
        private Button startButton;
        private Button cancelButton;
        private readonly List<Button> poolButtons = new List<Button>();
        private readonly List<Text> poolLabels = new List<Text>();
        private readonly List<Image> poolImages = new List<Image>();
        private readonly List<Text> queueSlotLabels = new List<Text>();
        private readonly List<Image> queueSlotImages = new List<Image>();
        private GameObject dragPreview;
        private RectTransform dragPreviewRect;
        private Image dragPreviewImage;
        private int selectedPoolIndex = -1;
        private string selectedQueueArtifactId = string.Empty;
        private string draggingArtifactId = string.Empty;
        private bool draggingFromPool;
        private bool isBuilt;
        private bool isVisible;

        public event Action StartRequested;
        public event Action CancelRequested;

        public ArtifactCatalog Catalog
        {
            get { return catalog; }
        }

        public PrepLoadoutState LoadoutState
        {
            get { return loadoutState; }
        }

        public bool IsVisible
        {
            get { return isVisible; }
        }

        public bool CanStart
        {
            get
            {
                if (loadoutState == null || catalog == null || loadoutState.EquippedCount <= 0)
                {
                    return false;
                }

                string reason;
                return loadoutState.IsValid(catalog, out reason);
            }
        }

        private void Awake()
        {
            EnsureData();
            BuildUi();
            RefreshAll();
            SetVisible(false);
        }

        public void Open(ArtifactCatalog nextCatalog, PrepLoadoutState nextLoadout)
        {
            EnsureData();
            catalog = nextCatalog ?? catalog ?? ArtifactCatalog.CreateDefault();
            loadoutState = nextLoadout ?? loadoutState ?? new PrepLoadoutState();
            loadoutState.Capacity = QueueCapacity;

            if (selectedPoolIndex < 0 && catalog.Count > 0)
            {
                selectedPoolIndex = 0;
            }

            SetVisible(true);
            RefreshAll();
            SetStatus(loadoutState.EquippedCount > 0 ? "当前队列可进入战斗。" : "从法宝池拖入底部器匣。");
        }

        public void Close()
        {
            SetVisible(false);
        }

        public ArtifactQueueState BuildQueueState()
        {
            EnsureData();
            return loadoutState != null ? loadoutState.ToQueueState(catalog) : new ArtifactQueueState();
        }

        public void SelectPoolArtifact(int catalogIndex)
        {
            selectedPoolIndex = catalogIndex;
            ArtifactCombatProfile profile = catalog.GetAt(catalogIndex);
            if (profile != null)
            {
                selectedQueueArtifactId = string.Empty;
                SetStatus("拖动 " + profile.displayName + " 到器匣空位。");
            }

            RefreshAll();
        }

        public void BeginPoolDrag(int catalogIndex, Vector2 screenPosition)
        {
            ArtifactCombatProfile profile = catalog.GetAt(catalogIndex);
            if (profile == null)
            {
                return;
            }

            selectedPoolIndex = catalogIndex;
            selectedQueueArtifactId = string.Empty;
            draggingArtifactId = profile.artifactId;
            draggingFromPool = true;
            ShowDragPreview(profile);
            UpdateDragPreview(screenPosition);
            SetStatus("拖到器匣目标槽位后松开。");
            RefreshDuringDrag();
        }

        public void BeginQueueDrag(string artifactId, Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                return;
            }

            draggingArtifactId = artifactId;
            draggingFromPool = false;
            selectedQueueArtifactId = artifactId;
            loadoutState.SelectedArtifactId = artifactId;
            ShowDragPreview(catalog.FindById(artifactId));
            UpdateDragPreview(screenPosition);
            SetStatus("拖到器匣空位可换位置，拖回法宝池可卸下。");
            RefreshDuringDrag();
        }

        public void DropOnQueueSlot(int targetSlotIndex)
        {
            DropOnQueueSlot(targetSlotIndex, false);
        }

        public void DropOnQueueSlot(int targetSlotIndex, bool insertAfterTarget)
        {
            if (string.IsNullOrWhiteSpace(draggingArtifactId))
            {
                return;
            }

            ArtifactCombatProfile profile = catalog.FindById(draggingArtifactId);
            if (profile == null)
            {
                ClearDrag();
                return;
            }

            string reason;
            PrepLoadoutEntry targetEntry = loadoutState.FindEntryAtSlot(catalog, targetSlotIndex);
            bool shouldInsert = !draggingFromPool
                && targetEntry != null
                && !string.Equals(targetEntry.artifactId, draggingArtifactId, StringComparison.Ordinal);
            bool success = draggingFromPool
                ? loadoutState.TryPlace(profile, catalog, targetSlotIndex, out reason)
                : shouldInsert
                    ? loadoutState.TryInsertMove(draggingArtifactId, catalog, targetSlotIndex, insertAfterTarget, out reason)
                    : loadoutState.TryMove(draggingArtifactId, catalog, targetSlotIndex, out reason);

            if (success)
            {
                selectedQueueArtifactId = draggingArtifactId;
                loadoutState.SelectedArtifactId = draggingArtifactId;
                SetStatus((draggingFromPool ? "已装入：" : shouldInsert ? "已插入：" : "已移动：") + profile.displayName);
            }
            else
            {
                SetStatus(reason);
            }

            ClearDrag();
            RefreshAll();
        }

        public void DropOnQueueBlock(string targetArtifactId, Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(targetArtifactId))
            {
                return;
            }

            PrepLoadoutEntry targetEntry = loadoutState.FindEntry(targetArtifactId);
            if (targetEntry == null)
            {
                DropOnQueueSlot(0);
                return;
            }

            Vector2 queueLocalPosition;
            bool insertAfterTarget = false;
            if (TryScreenPointToQueueLocal(screenPosition, out queueLocalPosition))
            {
                insertAfterTarget = IsPointerAfterEntry(targetEntry, queueLocalPosition);
            }

            DropOnQueueSlot(targetEntry.startSlotIndex, insertAfterTarget);
        }

        public void DropOnPool()
        {
            if (!draggingFromPool && !string.IsNullOrWhiteSpace(draggingArtifactId))
            {
                ArtifactCombatProfile profile = catalog.FindById(draggingArtifactId);
                if (loadoutState.TryRemove(draggingArtifactId))
                {
                    selectedQueueArtifactId = loadoutState.SelectedArtifactId;
                    SetStatus("已卸下：" + (profile != null ? profile.displayName : "法宝"));
                }
            }

            ClearDrag();
            RefreshAll();
        }

        public void EndDrag()
        {
            ClearDrag();
        }

        public void UpdateDragPreview(Vector2 screenPosition)
        {
            if (dragPreviewRect == null || canvas == null)
            {
                return;
            }

            Vector2 localPosition;
            Vector2 queueLocalPosition;
            if (TryScreenPointToQueueLocal(screenPosition, out queueLocalPosition) && IsInsideQueue(queueLocalPosition))
            {
                int targetSlotIndex = LocalPositionToSlotIndex(queueLocalPosition);
                PreviewDropAtSlot(targetSlotIndex, queueLocalPosition);
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root.transform as RectTransform,
                    screenPosition,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    out localPosition))
            {
                dragPreviewRect.anchoredPosition = localPosition;
                SetDragPreviewColor(new Color(0.12f, 0.105f, 0.086f, 0.82f));
            }
        }

        public void SelectQueueArtifact(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId))
            {
                selectedQueueArtifactId = string.Empty;
                loadoutState.SelectedArtifactId = string.Empty;
                RefreshAll();
                return;
            }

            ArtifactCombatProfile profile = catalog.FindById(artifactId);
            if (profile == null)
            {
                return;
            }

            selectedQueueArtifactId = artifactId;
            loadoutState.SelectedArtifactId = artifactId;
            SetStatus("已选中：" + profile.displayName);
            RefreshAll();
        }

        public void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = string.IsNullOrWhiteSpace(message) ? " " : message;
            }
        }

        private void EnsureData()
        {
            if (catalog == null)
            {
                catalog = ArtifactCatalog.CreateDefault();
            }

            if (loadoutState == null)
            {
                loadoutState = new PrepLoadoutState();
            }

            loadoutState.Capacity = QueueCapacity;
        }

        private void BuildUi()
        {
            if (isBuilt)
            {
                return;
            }

            Font font = CreateChineseFont();
            GameObject canvasObject = new GameObject("PrepHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root = CreateRectObject("PrepHudRoot", canvas.transform, typeof(Image));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.025f, 0.024f, 0.022f, 0.78f);

            GameObject panel = CreatePanel(root.transform, "PrepPanel", new Vector2(1560f, 900f), PanelColor);
            SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1560f, 900f), TextAnchor.MiddleCenter);

            Text title = CreateText(panel.transform, "Title", font, 38, TextAnchor.MiddleLeft);
            title.text = "洞口备战";
            title.color = new Color(0.96f, 0.84f, 0.55f, 1f);
            SetRect(title.rectTransform, new Vector2(-720f, 386f), new Vector2(360f, 52f), TextAnchor.MiddleLeft);

            Text hint = CreateText(panel.transform, "Hint", font, 20, TextAnchor.MiddleLeft);
            hint.text = "从法宝池拖入器匣。已装备法宝可拖拽换位，拖回法宝池可卸下。";
            hint.color = new Color(0.9f, 0.86f, 0.78f, 1f);
            SetRect(hint.rectTransform, new Vector2(-720f, 342f), new Vector2(960f, 34f), TextAnchor.MiddleLeft);

            capacityText = CreateText(panel.transform, "Capacity", font, 24, TextAnchor.MiddleRight);
            capacityText.color = new Color(0.95f, 0.87f, 0.62f, 1f);
            SetRect(capacityText.rectTransform, new Vector2(720f, 382f), new Vector2(280f, 42f), TextAnchor.MiddleRight);

            GameObject poolPanel = CreatePanel(panel.transform, "ArtifactPoolPanel", new Vector2(960f, 560f), SubPanelColor);
            SetRect(poolPanel.GetComponent<RectTransform>(), new Vector2(-270f, 50f), new Vector2(960f, 560f), TextAnchor.MiddleCenter);
            poolPanel.AddComponent<PrototypePrepPoolDropZone>().Initialize(this);

            Text poolTitle = CreateText(poolPanel.transform, "PoolTitle", font, 26, TextAnchor.MiddleLeft);
            poolTitle.text = "法宝池";
            poolTitle.color = new Color(0.94f, 0.84f, 0.64f, 1f);
            SetRect(poolTitle.rectTransform, new Vector2(-440f, 240f), new Vector2(180f, 34f), TextAnchor.MiddleLeft);

            poolContent = CreateRectObject("PoolContent", poolPanel.transform, typeof(GridLayoutGroup)).GetComponent<RectTransform>();
            SetRect(poolContent, new Vector2(0f, -22f), new Vector2(912f, 478f), TextAnchor.MiddleCenter);
            GridLayoutGroup poolGrid = poolContent.GetComponent<GridLayoutGroup>();
            poolGrid.cellSize = new Vector2(442f, 82f);
            poolGrid.spacing = new Vector2(14f, 12f);
            poolGrid.padding = new RectOffset(8, 8, 4, 4);
            poolGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            poolGrid.constraintCount = 2;

            GameObject detailPanel = CreatePanel(panel.transform, "DetailPanel", new Vector2(520f, 560f), SubPanelColor);
            SetRect(detailPanel.GetComponent<RectTransform>(), new Vector2(490f, 50f), new Vector2(520f, 560f), TextAnchor.MiddleCenter);
            Text detailTitle = CreateText(detailPanel.transform, "DetailTitle", font, 26, TextAnchor.MiddleLeft);
            detailTitle.text = "当前法宝";
            detailTitle.color = new Color(0.94f, 0.84f, 0.64f, 1f);
            SetRect(detailTitle.rectTransform, new Vector2(-220f, 240f), new Vector2(180f, 34f), TextAnchor.MiddleLeft);

            detailText = CreateText(detailPanel.transform, "DetailText", font, 22, TextAnchor.UpperLeft);
            detailText.color = new Color(0.93f, 0.9f, 0.82f, 1f);
            SetRect(detailText.rectTransform, new Vector2(-220f, 194f), new Vector2(440f, 448f), TextAnchor.UpperLeft);

            GameObject queuePanel = CreatePanel(panel.transform, "QueuePanel", new Vector2(1480f, 176f), new Color(0.08f, 0.074f, 0.066f, 0.98f));
            SetRect(queuePanel.GetComponent<RectTransform>(), new Vector2(0f, -262f), new Vector2(1480f, 176f), TextAnchor.MiddleCenter);
            Text queueTitle = CreateText(queuePanel.transform, "QueueTitle", font, 24, TextAnchor.MiddleLeft);
            queueTitle.text = "携带器匣";
            queueTitle.color = new Color(0.94f, 0.84f, 0.64f, 1f);
            SetRect(queueTitle.rectTransform, new Vector2(-700f, 62f), new Vector2(180f, 34f), TextAnchor.MiddleLeft);

            queueRoot = CreateRectObject("QueueRoot", queuePanel.transform).GetComponent<RectTransform>();
            SetRect(queueRoot, new Vector2(0f, -14f), new Vector2(QueueWidth(), SlotHeight), TextAnchor.MiddleCenter);
            queueSlotsRoot = CreateRectObject("QueueSlotsRoot", queueRoot).GetComponent<RectTransform>();
            SetRect(queueSlotsRoot, Vector2.zero, new Vector2(QueueWidth(), SlotHeight), TextAnchor.MiddleCenter);
            queueBlocksRoot = CreateRectObject("QueueBlocksRoot", queueRoot).GetComponent<RectTransform>();
            SetRect(queueBlocksRoot, Vector2.zero, new Vector2(QueueWidth(), SlotHeight), TextAnchor.MiddleCenter);

            startButton = CreateButton(panel.transform, "StartButton", font, "开始战斗");
            SetRect(startButton.GetComponent<RectTransform>(), new Vector2(595f, -382f), new Vector2(210f, 58f), TextAnchor.MiddleCenter);
            startButton.onClick.AddListener(OnStartClicked);
            cancelButton = CreateButton(panel.transform, "CancelButton", font, "取消");
            SetRect(cancelButton.GetComponent<RectTransform>(), new Vector2(365f, -382f), new Vector2(170f, 58f), TextAnchor.MiddleCenter);
            cancelButton.onClick.AddListener(OnCancelClicked);

            statusText = CreateText(panel.transform, "Status", font, 22, TextAnchor.MiddleLeft);
            statusText.color = new Color(0.94f, 0.84f, 0.56f, 1f);
            SetRect(statusText.rectTransform, new Vector2(-720f, -382f), new Vector2(980f, 42f), TextAnchor.MiddleLeft);

            BuildPoolButtons(font);
            BuildQueueSlots(font);
            isBuilt = true;
        }

        private void BuildPoolButtons(Font font)
        {
            ClearChildren(poolContent);
            poolButtons.Clear();
            poolLabels.Clear();
            poolImages.Clear();

            for (int i = 0; i < catalog.Count; i++)
            {
                int capturedIndex = i;
                Button button = CreateButton(poolContent, "PoolItem_" + i, font, string.Empty);
                button.onClick.AddListener(delegate { SelectPoolArtifact(capturedIndex); });
                PrototypePrepPoolDragItem dragItem = button.gameObject.AddComponent<PrototypePrepPoolDragItem>();
                dragItem.Initialize(this, capturedIndex);

                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 18;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                poolButtons.Add(button);
                poolLabels.Add(label);
                poolImages.Add(button.GetComponent<Image>());
            }
        }

        private void BuildQueueSlots(Font font)
        {
            ClearChildren(queueSlotsRoot);
            queueSlotLabels.Clear();
            queueSlotImages.Clear();

            for (int i = 0; i < QueueCapacity; i++)
            {
                int capturedIndex = i;
                Button button = CreateButton(queueSlotsRoot, "QueueSlot_" + i, font, string.Empty);
                RectTransform rect = button.GetComponent<RectTransform>();
                SetRect(rect, SlotCenter(i), new Vector2(SlotWidth, SlotHeight), TextAnchor.MiddleCenter);
                button.onClick.AddListener(delegate { SelectQueueSlot(capturedIndex); });
                PrototypePrepQueueSlotDropZone dropZone = button.gameObject.AddComponent<PrototypePrepQueueSlotDropZone>();
                dropZone.Initialize(this, capturedIndex);

                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 16;
                label.text = (i + 1).ToString("00");
                queueSlotLabels.Add(label);
                queueSlotImages.Add(button.GetComponent<Image>());
            }
        }

        private void RefreshAll()
        {
            RefreshPoolCards();
            RefreshQueueSlots();
            RefreshQueueBlocks();
            RefreshDetailPanel();
            RefreshButtons();
            RefreshCapacity();
        }

        private void RefreshDuringDrag()
        {
            RefreshPoolCards();
            RefreshQueueSlots();
            RefreshDetailPanel();
            RefreshButtons();
            RefreshCapacity();
        }

        private void RefreshPoolCards()
        {
            for (int i = 0; i < poolButtons.Count; i++)
            {
                ArtifactCombatProfile profile = catalog.GetAt(i);
                if (profile == null)
                {
                    poolButtons[i].interactable = false;
                    poolLabels[i].text = "空";
                    poolImages[i].color = DisabledColor;
                    continue;
                }

                bool isSelected = i == selectedPoolIndex;
                bool isEquipped = loadoutState != null && loadoutState.IsEquipped(profile.artifactId);
                poolLabels[i].text = BuildPoolText(profile, isEquipped);
                poolImages[i].color = isSelected ? SelectedColor : isEquipped ? new Color(0.15f, 0.19f, 0.16f, 0.98f) : SlotColor;
                poolButtons[i].interactable = true;
            }
        }

        private void RefreshQueueSlots()
        {
            for (int i = 0; i < queueSlotImages.Count; i++)
            {
                PrepLoadoutEntry entry = loadoutState.FindEntryAtSlot(catalog, i);
                queueSlotLabels[i].text = (i + 1).ToString("00");
                queueSlotImages[i].color = entry == null ? DisabledColor : new Color(0.105f, 0.098f, 0.088f, 0.98f);
            }
        }

        private void RefreshQueueBlocks()
        {
            ClearChildren(queueBlocksRoot);

            IReadOnlyList<PrepLoadoutEntry> entries = loadoutState.EquippedArtifacts;
            for (int i = 0; i < entries.Count; i++)
            {
                PrepLoadoutEntry entry = entries[i];
                ArtifactCombatProfile profile = catalog.FindById(entry.artifactId);
                if (profile == null)
                {
                    continue;
                }

                CreateEquippedBlock(entry, profile);
            }
        }

        private GameObject CreateEquippedBlock(PrepLoadoutEntry entry, ArtifactCombatProfile profile)
        {
            int size = Mathf.Max(1, profile.size);
            bool isSelected = string.Equals(loadoutState.SelectedArtifactId, profile.artifactId, StringComparison.Ordinal);
            float width = SlotWidth * size + SlotSpacing * Mathf.Max(0, size - 1);
            GameObject block = CreatePanel(queueBlocksRoot, "Equipped_" + profile.artifactId, new Vector2(width, SlotHeight), isSelected ? SelectedColor : SlotColor);
            RectTransform rect = block.GetComponent<RectTransform>();
            SetRect(rect, BlockCenter(entry.startSlotIndex, size), new Vector2(width, SlotHeight), TextAnchor.MiddleCenter);

            PrototypePrepQueueBlockDragItem dragItem = block.AddComponent<PrototypePrepQueueBlockDragItem>();
            dragItem.Initialize(this, profile.artifactId, entry.startSlotIndex);
            Button button = block.AddComponent<Button>();
            button.onClick.AddListener(delegate { SelectQueueArtifact(profile.artifactId); });

            ColorBlock colors = button.colors;
            colors.normalColor = isSelected ? SelectedColor : SlotColor;
            colors.highlightedColor = new Color(0.28f, 0.22f, 0.15f, 1f);
            colors.pressedColor = new Color(0.1f, 0.09f, 0.08f, 1f);
            colors.selectedColor = SelectedColor;
            button.colors = colors;

            Image image = block.GetComponent<Image>();
            image.raycastTarget = true;

            GameObject elementBar = CreateRectObject("ElementBar", block.transform, typeof(Image));
            Image barImage = elementBar.GetComponent<Image>();
            barImage.color = ElementColor(profile.element);
            RectTransform barRect = elementBar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(10f, 0f);
            barRect.anchoredPosition = Vector2.zero;

            Text label = CreateText(block.transform, "Label", CreateChineseFont(), 18, TextAnchor.MiddleCenter);
            label.text = profile.displayName + "\n容量 " + size;
            label.color = new Color(0.98f, 0.93f, 0.8f, 1f);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);
            return block;
        }

        private void RefreshDetailPanel()
        {
            ArtifactCombatProfile selectedQueue = GetSelectedQueueArtifact();
            ArtifactCombatProfile selectedPool = GetSelectedPoolArtifact();
            ArtifactCombatProfile profile = selectedQueue ?? selectedPool;
            detailText.text = profile != null ? BuildDetailText(profile) : "未选择法宝。";
        }

        private void RefreshButtons()
        {
            string invalidReason;
            bool isValid = loadoutState != null && loadoutState.IsValid(catalog, out invalidReason);
            startButton.interactable = loadoutState != null && loadoutState.EquippedCount > 0 && isValid;
        }

        private void RefreshCapacity()
        {
            int used = loadoutState != null ? loadoutState.UsedCapacity(catalog) : 0;
            int remaining = loadoutState != null ? loadoutState.RemainingCapacity(catalog) : QueueCapacity;
            capacityText.text = "容量 " + used + "/10　剩余 " + remaining;
        }

        private void SelectQueueSlot(int slotIndex)
        {
            PrepLoadoutEntry entry = loadoutState.FindEntryAtSlot(catalog, slotIndex);
            SelectQueueArtifact(entry != null ? entry.artifactId : string.Empty);
        }

        private void OnStartClicked()
        {
            if (!CanStart)
            {
                SetStatus("请先把法宝拖入器匣。");
                return;
            }

            SetStatus("准备进入战斗。");
            StartRequested?.Invoke();
        }

        private void OnCancelClicked()
        {
            SetStatus("已取消。");
            CancelRequested?.Invoke();
        }

        private ArtifactCombatProfile GetSelectedPoolArtifact()
        {
            return catalog != null && selectedPoolIndex >= 0 && selectedPoolIndex < catalog.Count ? catalog.GetAt(selectedPoolIndex) : null;
        }

        private ArtifactCombatProfile GetSelectedQueueArtifact()
        {
            return !string.IsNullOrEmpty(selectedQueueArtifactId) ? catalog.FindById(selectedQueueArtifactId) : null;
        }

        private void ClearDrag()
        {
            draggingArtifactId = string.Empty;
            draggingFromPool = false;
            HideDragPreview();
        }

        private void SetVisible(bool visible)
        {
            isVisible = visible;
            if (canvas != null)
            {
                canvas.enabled = visible;
            }

            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private static string BuildPoolText(ArtifactCombatProfile profile, bool isEquipped)
        {
            string mark = isEquipped ? "已携带" : "拖入器匣";
            return profile.displayName + "　" + mark
                + "\n" + BattleDisplayText.ElementName(profile.element) + "/" + BattleDisplayText.CategoryName(profile.category)
                + "　容量" + profile.size + "　冷却" + BattleDisplayText.FormatCooldownSeconds(profile.cooldown)
                + "\n选敌：" + BattleDisplayText.TargetSelectorName(profile.targetSelectorType);
        }

        private static string BuildDetailText(ArtifactCombatProfile profile)
        {
            return profile.displayName
                + "\n五行：" + BattleDisplayText.ElementName(profile.element)
                + "　类型：" + BattleDisplayText.CategoryName(profile.category)
                + "\n容量：" + profile.size
                + "　冷却：" + BattleDisplayText.FormatCooldownSeconds(profile.cooldown)
                + "\n目标：" + BattleDisplayText.TargetSelectorName(profile.targetSelectorType)
                + "\n供给：" + BattleDisplayText.SupplyDirectionName(profile.supplyDirection)
                + "　接收：" + BattleDisplayText.JoinedElements(profile.acceptedElements)
                + "\n相生：" + BattleDisplayText.FormatBuffSummary(profile.shengBuff);
        }

        private void ShowDragPreview(ArtifactCombatProfile profile)
        {
            HideDragPreview();
            if (profile == null || canvas == null)
            {
                return;
            }

            int size = Mathf.Max(1, profile.size);
            float width = SlotWidth * size + SlotSpacing * Mathf.Max(0, size - 1);
            dragPreview = CreatePanel(root.transform, "ArtifactDragPreview", new Vector2(width, SlotHeight), new Color(0.12f, 0.105f, 0.086f, 0.82f));
            dragPreviewRect = dragPreview.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = dragPreview.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            dragPreview.transform.SetAsLastSibling();

            dragPreviewImage = dragPreview.GetComponent<Image>();
            dragPreviewImage.raycastTarget = false;

            GameObject elementBar = CreateRectObject("ElementBar", dragPreview.transform, typeof(Image));
            Image barImage = elementBar.GetComponent<Image>();
            barImage.raycastTarget = false;
            barImage.color = ElementColor(profile.element);
            RectTransform barRect = elementBar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(10f, 0f);
            barRect.anchoredPosition = Vector2.zero;

            Text label = CreateText(dragPreview.transform, "Label", CreateChineseFont(), 18, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            label.text = profile.displayName + "\n容量 " + size;
            label.color = new Color(0.98f, 0.93f, 0.8f, 1f);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);
        }

        private void PreviewDropAtSlot(int targetSlotIndex, Vector2 queueLocalPosition)
        {
            ArtifactCombatProfile profile = catalog.FindById(draggingArtifactId);
            if (profile == null)
            {
                return;
            }

            PrepLoadoutEntry targetEntry = loadoutState.FindEntryAtSlot(catalog, targetSlotIndex);
            bool shouldInsert = !draggingFromPool
                && targetEntry != null
                && !string.Equals(targetEntry.artifactId, draggingArtifactId, StringComparison.Ordinal);
            bool insertAfterTarget = shouldInsert && IsPointerAfterEntry(targetEntry, queueLocalPosition);

            string reason;
            int previewStartSlot = targetSlotIndex;
            bool canDrop = draggingFromPool
                ? loadoutState.CanPlace(profile, catalog, targetSlotIndex, string.Empty, out reason)
                : shouldInsert
                    ? loadoutState.TryGetInsertMoveStartSlot(
                        draggingArtifactId,
                        catalog,
                        targetSlotIndex,
                        insertAfterTarget,
                        out previewStartSlot,
                        out reason)
                    : loadoutState.CanPlace(profile, catalog, targetSlotIndex, draggingArtifactId, out reason);

            int previewSnapSlot = Mathf.Clamp(previewStartSlot, 0, QueueCapacity - 1);
            int size = Mathf.Max(1, profile.size);
            dragPreviewRect.anchoredPosition = QueueToRootPosition(BlockCenter(previewSnapSlot, size));
            SetDragPreviewColor(canDrop
                ? shouldInsert ? new Color(0.16f, 0.24f, 0.13f, 0.9f) : new Color(0.13f, 0.18f, 0.24f, 0.9f)
                : new Color(0.34f, 0.11f, 0.08f, 0.86f));
        }

        private void SetDragPreviewColor(Color color)
        {
            if (dragPreviewImage != null)
            {
                dragPreviewImage.color = color;
            }
        }

        private bool TryScreenPointToQueueLocal(Vector2 screenPosition, out Vector2 localPosition)
        {
            localPosition = Vector2.zero;
            if (queueRoot == null)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                queueRoot,
                screenPosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out localPosition);
        }

        private bool IsPointerAfterEntry(PrepLoadoutEntry entry, Vector2 queueLocalPosition)
        {
            ArtifactCombatProfile profile = catalog.FindById(entry.artifactId);
            int size = profile != null ? Mathf.Max(1, profile.size) : 1;
            return queueLocalPosition.x >= BlockCenter(entry.startSlotIndex, size).x;
        }

        private bool IsInsideQueue(Vector2 queueLocalPosition)
        {
            float halfWidth = QueueWidth() * 0.5f;
            float halfHeight = SlotHeight * 0.5f;
            return queueLocalPosition.x >= -halfWidth
                && queueLocalPosition.x <= halfWidth
                && queueLocalPosition.y >= -halfHeight
                && queueLocalPosition.y <= halfHeight;
        }

        private int LocalPositionToSlotIndex(Vector2 queueLocalPosition)
        {
            float left = -QueueWidth() * 0.5f;
            float slotStep = SlotWidth + SlotSpacing;
            return Mathf.Clamp(Mathf.FloorToInt((queueLocalPosition.x - left) / slotStep), 0, QueueCapacity - 1);
        }

        private Vector2 QueueToRootPosition(Vector2 queuePosition)
        {
            Vector3 worldPosition = queueRoot.TransformPoint(queuePosition);
            return ((RectTransform)root.transform).InverseTransformPoint(worldPosition);
        }

        private void HideDragPreview()
        {
            if (dragPreview != null)
            {
                Destroy(dragPreview);
            }

            dragPreview = null;
            dragPreviewRect = null;
            dragPreviewImage = null;
        }

        private static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Metal:
                    return new Color(0.82f, 0.78f, 0.62f, 1f);
                case Element.Water:
                    return new Color(0.22f, 0.48f, 0.82f, 1f);
                case Element.Wood:
                    return new Color(0.28f, 0.62f, 0.34f, 1f);
                case Element.Fire:
                    return new Color(0.86f, 0.32f, 0.18f, 1f);
                case Element.Earth:
                    return new Color(0.66f, 0.52f, 0.28f, 1f);
                default:
                    return new Color(0.55f, 0.55f, 0.55f, 1f);
            }
        }

        private static float QueueWidth()
        {
            return SlotWidth * QueueCapacity + SlotSpacing * (QueueCapacity - 1);
        }

        private static Vector2 SlotCenter(int slotIndex)
        {
            float left = -QueueWidth() * 0.5f;
            return new Vector2(left + SlotWidth * 0.5f + slotIndex * (SlotWidth + SlotSpacing), 0f);
        }

        private static Vector2 BlockCenter(int startSlotIndex, int size)
        {
            float width = SlotWidth * size + SlotSpacing * Mathf.Max(0, size - 1);
            float left = -QueueWidth() * 0.5f + startSlotIndex * (SlotWidth + SlotSpacing);
            return new Vector2(left + width * 0.5f, 0f);
        }

        private static GameObject CreateRectObject(string name, Transform parent, params Type[] components)
        {
            Type[] allComponents = new Type[components.Length + 1];
            allComponents[0] = typeof(RectTransform);
            for (int i = 0; i < components.Length; i++)
            {
                allComponents[i + 1] = components[i];
            }

            GameObject gameObject = new GameObject(name, allComponents);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            GameObject panel = CreateRectObject(name, parent, typeof(Image));
            panel.GetComponent<RectTransform>().sizeDelta = size;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = CreateRectObject(name, parent, typeof(Text));
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Font font, string label)
        {
            GameObject buttonObject = CreateRectObject(name, parent, typeof(Image), typeof(Button));
            Image image = buttonObject.GetComponent<Image>();
            image.color = SlotColor;

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = SlotColor;
            colors.highlightedColor = new Color(0.25f, 0.2f, 0.14f, 1f);
            colors.pressedColor = new Color(0.1f, 0.09f, 0.08f, 1f);
            colors.selectedColor = SelectedColor;
            colors.disabledColor = DisabledColor;
            button.colors = colors;

            Text text = CreateText(buttonObject.transform, "Label", font, 20, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = new Color(0.96f, 0.91f, 0.8f, 1f);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 6f);
            textRect.offsetMax = new Vector2(-8f, -6f);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                children.Add(parent.GetChild(i).gameObject);
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] != null)
                {
                    UnityEngine.Object.Destroy(children[i]);
                }
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Vector2 pivot = new Vector2(0.5f, 0.5f);
            if (alignment == TextAnchor.UpperLeft || alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.LowerLeft)
            {
                pivot.x = 0f;
            }
            else if (alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight || alignment == TextAnchor.LowerRight)
            {
                pivot.x = 1f;
            }

            if (alignment == TextAnchor.UpperLeft || alignment == TextAnchor.UpperCenter || alignment == TextAnchor.UpperRight)
            {
                pivot.y = 1f;
            }
            else if (alignment == TextAnchor.LowerLeft || alignment == TextAnchor.LowerCenter || alignment == TextAnchor.LowerRight)
            {
                pivot.y = 0f;
            }

            rect.pivot = pivot;
        }

        private static Font CreateChineseFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "SimHei", "Microsoft YaHei UI", "Microsoft YaHei", "Arial" },
                24);

            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    public sealed class PrototypePrepPoolDragItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private PrototypePrepHud hud;
        private int catalogIndex;

        public void Initialize(PrototypePrepHud owner, int index)
        {
            hud = owner;
            catalogIndex = index;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            hud?.SelectPoolArtifact(catalogIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            hud?.BeginPoolDrag(catalogIndex, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            hud?.UpdateDragPreview(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            hud?.EndDrag();
        }
    }

    public sealed class PrototypePrepQueueBlockDragItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private PrototypePrepHud hud;
        private string artifactId;
        private int startSlotIndex;

        public void Initialize(PrototypePrepHud owner, string id, int slotIndex)
        {
            hud = owner;
            artifactId = id;
            startSlotIndex = slotIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            hud?.SelectQueueArtifact(artifactId);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            hud?.BeginQueueDrag(artifactId, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            hud?.UpdateDragPreview(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            hud?.EndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            hud?.DropOnQueueBlock(artifactId, eventData.position);
        }
    }

    public sealed class PrototypePrepQueueSlotDropZone : MonoBehaviour, IDropHandler
    {
        private PrototypePrepHud hud;
        private int slotIndex;

        public void Initialize(PrototypePrepHud owner, int index)
        {
            hud = owner;
            slotIndex = index;
        }

        public void OnDrop(PointerEventData eventData)
        {
            hud?.DropOnQueueSlot(slotIndex);
        }
    }

    public sealed class PrototypePrepPoolDropZone : MonoBehaviour, IDropHandler
    {
        private PrototypePrepHud hud;

        public void Initialize(PrototypePrepHud owner)
        {
            hud = owner;
        }

        public void OnDrop(PointerEventData eventData)
        {
            hud?.DropOnPool();
        }
    }
}
