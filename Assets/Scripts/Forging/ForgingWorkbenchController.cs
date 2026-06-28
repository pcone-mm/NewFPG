using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Forging
{
    [DisallowMultipleComponent]
    public sealed class ForgingWorkbenchController : MonoBehaviour
    {
        private const float CellSize = 126f;
        private const float CellGap = 12f;
        private const float DrawerWidth = 680f;
        private const float DrawerHeight = 980f;
        private const string RuntimeRootName = "ForgingRuntimeUI";

        private static readonly Vector2 ForgeCenter = new Vector2(872f, 24f);
        private static readonly Color EmptyCellColor = new Color(0.055f, 0.042f, 0.028f, 0.58f);
        private static readonly Color DisabledCellColor = new Color(0.02f, 0.017f, 0.014f, 0.04f);
        private static readonly Color FilledCellColor = new Color(0.72f, 0.48f, 0.18f, 0.86f);
        private static readonly Color DashColor = new Color(1f, 0.78f, 0.38f, 0.96f);
        private static readonly Color InkPanelColor = new Color(0.024f, 0.02f, 0.016f, 0.94f);
        private static readonly Color ButtonColor = new Color(0.13f, 0.092f, 0.058f, 0.96f);
        private static readonly Color ButtonSelectedColor = new Color(0.56f, 0.34f, 0.12f, 0.98f);
        private static readonly Color MaterialSlotColor = new Color(0.02f, 0.018f, 0.014f, 0.05f);
        private static readonly Color MaterialSlotSelectedColor = new Color(0.42f, 0.27f, 0.08f, 0.5f);
        private static readonly Color DragPreviewValidColor = new Color(0.95f, 0.7f, 0.26f, 0.5f);
        private static readonly Color DragPreviewInvalidColor = new Color(0.95f, 0.18f, 0.14f, 0.42f);
        private static readonly Color DragGhostColor = new Color(1f, 0.88f, 0.48f, 0.78f);
        private static readonly Color TextGold = new Color(0.96f, 0.82f, 0.52f, 1f);
        private static readonly Color TextPale = new Color(0.92f, 0.86f, 0.73f, 1f);
        private static TMP_FontAsset runtimeFontAsset;

        [SerializeField] private string weaponBlueprintsPath = ForgingCatalogLoader.DefaultWeaponBlueprintsPath;
        [SerializeField] private string materialsPath = ForgingCatalogLoader.DefaultMaterialsPath;
        [SerializeField, HideInInspector] private string catalogPath = ForgingCatalogLoader.LegacyCatalogPath;
        [SerializeField] private Texture materialTexture;
        [SerializeField] private bool showDrawerOnStart = true;
        [SerializeField] private ForgingUILayoutPreset layoutPreset;

        private Canvas canvas;
        private RectTransform root;
        private RectTransform boardFrame;
        private RectTransform boardRoot;
        private RectTransform previewRoot;
        private RectTransform dragGhostRoot;
        private RectTransform drawer;
        private RectTransform drawerList;
        private RectTransform materialHudRoot;
        private RectTransform resultPanel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI resultText;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI selectedMaterialText;
        private TextMeshProUGUI drawerToggleLabel;
        private Button forgeButton;
        private Button clearButton;

        private readonly List<ForgingWeaponBlueprintDefinition> blueprints = new List<ForgingWeaponBlueprintDefinition>();
        private readonly List<ForgingMaterialDefinition> materials = new List<ForgingMaterialDefinition>();
        private readonly List<ForgingPlacedMaterial> placements = new List<ForgingPlacedMaterial>();
        private readonly Dictionary<Vector2Int, ForgingCellView> cellViews = new Dictionary<Vector2Int, ForgingCellView>();
        private readonly Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>();
        private readonly List<Button> materialButtons = new List<Button>();
        private readonly List<Button> blueprintButtons = new List<Button>();

        private ForgingWeaponBlueprintDefinition selectedBlueprint;
        private ForgingMaterialDefinition selectedMaterial;
        private ForgingMaterialDefinition draggedMaterial;
        private ForgingResult currentResult;
        private bool drawerOpen;
        private bool isDraggingMaterial;
        private int dragRotationSteps;
        private Vector2Int dragHoverCell;
        private bool hasDragHoverCell;
        private bool dragPlacementValid;

        public ForgingResult CurrentResult => currentResult;
        public ForgedWeaponRuntimeStats CurrentRuntimeStats => currentResult != null && currentResult.isComplete
            ? currentResult.ToRuntimeStats()
            : null;
        public ForgingUILayoutPreset LayoutPreset => layoutPreset;
        public RectTransform RuntimeRoot => root;
        public bool DrawerOpen => drawerOpen;

        private void Awake()
        {
            EnsureData();
            EnsureCanvas();
            RebuildUi();
            SetDrawerOpen(showDrawerOnStart, true);

            if (selectedMaterial == null && materials.Count > 0)
            {
                SelectMaterial(materials[0]);
            }

            Evaluate();
            RefreshAll();
        }

        private void Update()
        {
            bool rotatePressed = WasRotatePressed();
            float scrollDeltaY = ReadScrollDeltaY();

            if (isDraggingMaterial && (rotatePressed || scrollDeltaY > 0.1f))
            {
                RotateDraggedMaterial(1);
            }
            else if (isDraggingMaterial && scrollDeltaY < -0.1f)
            {
                RotateDraggedMaterial(-1);
            }
            else if (!isDraggingMaterial && selectedMaterial != null && rotatePressed)
            {
                dragRotationSteps = ForgingShapeUtility.NormalizeRotationSteps(dragRotationSteps + 1);
                SetStatus(selectedMaterial.displayName + " rotation " + (dragRotationSteps * 90) + " deg");
            }
        }

        public void SelectBlueprint(ForgingWeaponBlueprintDefinition blueprint)
        {
            selectedBlueprint = blueprint;
            placements.Clear();
            BuildBoard();
            SetDrawerOpen(false, true);
            SetStatus(blueprint != null ? "已选择器图：" + blueprint.displayName : "请选择器图");
            RefreshAll();
        }

        public void SelectMaterial(ForgingMaterialDefinition material)
        {
            selectedMaterial = material;
            SetStatus(material != null ? "已选材料：" + material.displayName : "请选择材料");
            RefreshAll();
        }

        public void ToggleDrawer()
        {
            SetDrawerOpen(!drawerOpen, true);
        }

        public void ClearMaterials()
        {
            placements.Clear();
            SetStatus("已清空图纸。");
            RefreshAll();
        }

        public void RebuildRuntimeUiPreview()
        {
            EnsureData();
            EnsureCanvas();
            RebuildUi();
            SetDrawerOpen(showDrawerOnStart, true);

            if (selectedMaterial == null && materials.Count > 0)
            {
                SelectMaterial(materials[0]);
            }

            Evaluate();
            RefreshAll();
        }

        public bool SaveCurrentRuntimeLayoutToPreset(
            bool includeGeneratedContent,
            ForgingLayoutDrawerStateCapture drawerStateCapture)
        {
            if (layoutPreset == null || root == null)
            {
                return false;
            }

            RectTransform drawerToggle = drawerToggleLabel != null
                ? drawerToggleLabel.transform.parent as RectTransform
                : null;
            layoutPreset.CaptureFrom(root, includeGeneratedContent);
            layoutPreset.CaptureDrawerState(drawerStateCapture, drawer, drawerToggle, drawerOpen);
#if UNITY_EDITOR
            EditorUtility.SetDirty(layoutPreset);
            AssetDatabase.SaveAssets();
#endif
            return true;
        }

        public void Forge()
        {
            Evaluate();
            if (currentResult == null || !currentResult.isValid)
            {
                SetStatus(currentResult != null ? currentResult.invalidReason : "还不能炼制。");
                return;
            }

            if (!currentResult.isComplete)
            {
                SetStatus("需要填满全部格子。");
                return;
            }

            SetStatus("炼制完成：" + selectedBlueprint.displayName + "，属性已可写入运行时武器。");
        }

        private void TryPlaceAt(Vector2Int cell)
        {
            if (selectedBlueprint == null)
            {
                SetStatus("先从右侧选择器图。");
                SetDrawerOpen(true, true);
                return;
            }

            if (selectedMaterial == null)
            {
                SetStatus("先选择左侧材料。");
                return;
            }

            if (!TryPlaceMaterial(selectedMaterial, cell, dragRotationSteps, out string reason))
            {
                SetStatus(reason);
                return;
            }

            placements.Add(new ForgingPlacedMaterial(selectedMaterial, cell, dragRotationSteps));
            SetStatus(selectedMaterial.displayName + " 已放入。");
            RefreshAll();
        }

        private void BeginMaterialDrag(ForgingMaterialDefinition material, PointerEventData eventData)
        {
            if (material == null)
            {
                return;
            }

            SelectMaterial(material);
            draggedMaterial = material;
            isDraggingMaterial = true;
            hasDragHoverCell = false;
            dragPlacementValid = false;
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                RotateDraggedMaterial(1);
            }

            EnsureDragGhost(material);
            UpdateMaterialDrag(eventData);
        }

        private void UpdateMaterialDrag(PointerEventData eventData)
        {
            if (!isDraggingMaterial || draggedMaterial == null)
            {
                return;
            }

            Vector2 screenPosition = eventData != null ? eventData.position : ReadPointerScreenPosition();
            MoveDragGhost(screenPosition);
            hasDragHoverCell = TryGetBoardCell(screenPosition, out dragHoverCell);
            dragPlacementValid = false;
            string reason = string.Empty;
            if (hasDragHoverCell)
            {
                dragPlacementValid = TryPlaceMaterial(draggedMaterial, dragHoverCell, dragRotationSteps, out reason);
            }

            BuildDragPreview();
        }

        private void EndMaterialDrag(PointerEventData eventData)
        {
            if (!isDraggingMaterial)
            {
                return;
            }

            UpdateMaterialDrag(eventData);
            if (draggedMaterial != null && hasDragHoverCell && dragPlacementValid)
            {
                placements.Add(new ForgingPlacedMaterial(draggedMaterial, dragHoverCell, dragRotationSteps));
                SetStatus(draggedMaterial.displayName + " placed.");
                RefreshAll();
            }
            else if (draggedMaterial != null)
            {
                SetStatus("Cannot place " + draggedMaterial.displayName + ".");
            }

            isDraggingMaterial = false;
            draggedMaterial = null;
            hasDragHoverCell = false;
            ClearDragPreview();
            ClearDragGhost();
        }

        private void RotateDraggedMaterial(int delta)
        {
            dragRotationSteps = ForgingShapeUtility.NormalizeRotationSteps(dragRotationSteps + delta);
            if (draggedMaterial != null)
            {
                SetStatus(draggedMaterial.displayName + " rotation " + (dragRotationSteps * 90) + " deg");
                EnsureDragGhost(draggedMaterial);
                MoveDragGhost(ReadPointerScreenPosition());
            }

            BuildDragPreview();
        }

        private bool TryPlaceMaterial(
            ForgingMaterialDefinition material,
            Vector2Int origin,
            int rotationSteps,
            out string reason)
        {
            reason = string.Empty;
            if (selectedBlueprint == null)
            {
                reason = "Select a weapon blueprint first.";
                return false;
            }

            if (material == null)
            {
                reason = "Select a material first.";
                return false;
            }

            List<ForgingPlacedMaterial> nextPlacements = new List<ForgingPlacedMaterial>(placements)
            {
                new ForgingPlacedMaterial(material, origin, rotationSteps)
            };
            ForgingResult result = ForgingCalculator.Evaluate(selectedBlueprint, nextPlacements);
            reason = result.invalidReason;
            return result.isValid;
        }

        private void EnsureData()
        {
            blueprints.Clear();
            materials.Clear();

            ForgingCatalog catalog = ForgingCatalogLoader.LoadFromProjectPaths(weaponBlueprintsPath, materialsPath);
#if UNITY_EDITOR
            if (catalog.IsEmpty)
            {
                string absolutePath = System.IO.Path.GetFullPath(catalogPath);
                if (System.IO.File.Exists(absolutePath))
                {
                    catalog = ForgingCatalog.FromJson(System.IO.File.ReadAllText(absolutePath));
                }
            }
#endif
            if (catalog.weaponBlueprints != null)
            {
                blueprints.AddRange(catalog.weaponBlueprints);
            }

            if (catalog.materials != null)
            {
                materials.AddRange(catalog.materials);
            }

            if (blueprints.Count == 0 || materials.Count == 0)
            {
                Debug.LogWarning("Forging workbench loaded incomplete catalog. WeaponBlueprints=" + weaponBlueprintsPath
                    + ", Materials=" + materialsPath + ", Legacy=" + catalogPath
                    + ", Blueprints=" + blueprints.Count + ", Materials=" + materials.Count);
            }

            if (materialTexture == null)
            {
                materialTexture = LoadTexture("Assets/Art/UI/ForgingPSDImport/Layers/03_yellow_earth.png");
            }
        }

        private void EnsureCanvas()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("ForgingUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(3840f, 2160f);
                scaler.matchWidthOrHeight = 0.5f;
                transform.SetParent(canvas.transform, false);
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler existingScaler = canvas.GetComponent<CanvasScaler>();
            if (existingScaler != null)
            {
                existingScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                existingScaler.referenceResolution = new Vector2(3840f, 2160f);
                existingScaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void RebuildUi()
        {
            Font font = CreateFont();
            DestroyRuntimeRoot();

            root = CreateRectObject(RuntimeRootName, transform).GetComponent<RectTransform>();
            Stretch(root);

            materialHudRoot = CreateRectObject("MaterialHotspots", root).GetComponent<RectTransform>();
            Stretch(materialHudRoot);
            BuildMaterialButtons(font);

            boardFrame = CreatePanel(root, "ForgeBoardFrame", new Vector2(760f, 760f), Color.clear).GetComponent<RectTransform>();
            SetRect(boardFrame, ForgeCenter + new Vector2(0f, -18f), new Vector2(760f, 760f));
            boardFrame.GetComponent<Image>().raycastTarget = false;

            titleText = CreateText(boardFrame, "BlueprintTitle", font, 64, TextAnchor.MiddleCenter);
            titleText.color = TextGold;
            titleText.fontStyle = FontStyles.Bold;
            SetRect(titleText.rectTransform, new Vector2(0f, 300f), new Vector2(720f, 90f));
            AddTextShadow(titleText, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));

            boardRoot = CreateRectObject("BlueprintGrid", boardFrame).GetComponent<RectTransform>();
            SetRect(boardRoot, new Vector2(0f, -28f), new Vector2(640f, 640f));
            previewRoot = CreateRectObject("PlacementPreview", boardRoot).GetComponent<RectTransform>();
            Stretch(previewRoot);
            previewRoot.SetAsLastSibling();

            resultPanel = CreatePanel(root, "ResultPanel", new Vector2(1100f, 230f), InkPanelColor).GetComponent<RectTransform>();
            SetRect(resultPanel, ForgeCenter + new Vector2(0f, -710f), new Vector2(1100f, 230f));

            resultText = CreateText(resultPanel, "ResultText", font, 42, TextAnchor.MiddleLeft);
            resultText.color = TextPale;
            resultText.lineSpacing = 1.05f;
            Stretch(resultText.rectTransform, new Vector2(42f, 28f), new Vector2(-310f, -28f));

            statusText = CreateText(root, "Status", font, 34, TextAnchor.MiddleCenter);
            statusText.color = TextGold;
            AddTextShadow(statusText, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));
            SetRect(statusText.rectTransform, ForgeCenter + new Vector2(0f, -860f), new Vector2(1100f, 58f));

            forgeButton = CreateButton(resultPanel, "ForgeButton", font, "炼制");
            SetRect(forgeButton.GetComponent<RectTransform>(), new Vector2(390f, 48f), new Vector2(230f, 82f));
            forgeButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 46;
            forgeButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
            forgeButton.onClick.AddListener(Forge);

            clearButton = CreateButton(resultPanel, "ClearButton", font, "清空");
            SetRect(clearButton.GetComponent<RectTransform>(), new Vector2(390f, -54f), new Vector2(230f, 74f));
            clearButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 38;
            clearButton.onClick.AddListener(ClearMaterials);

            BuildDrawer(font);
            BuildBoard();
            ApplyLayoutPreset();
        }

        private void BuildMaterialButtons(Font font)
        {
            materialButtons.Clear();
            for (int i = 0; i < materials.Count; i++)
            {
                ForgingMaterialDefinition material = materials[i];
                Vector2 slotSize = material.uiSlot.size + new Vector2(58f, 58f);
                RectTransform slot = CreatePanel(materialHudRoot, "MaterialSlot_" + material.materialId, slotSize, MaterialSlotColor).GetComponent<RectTransform>();
                SetRect(slot, material.uiSlot.anchoredPosition + new Vector2(0f, -6f), slotSize);

                RawImage image = CreateRectObject("Icon", slot, typeof(RawImage)).GetComponent<RawImage>();
                image.raycastTarget = false;
                image.texture = LoadTexture(material.texturePath) ?? materialTexture;
                image.color = new Color(1f, 1f, 1f, 0.98f);
                SetRect(image.rectTransform, new Vector2(0f, 18f), material.uiSlot.size * 0.72f);

                selectedMaterialText = CreateText(slot, "Label", font, 30, TextAnchor.MiddleCenter);
                selectedMaterialText.color = TextPale;
                selectedMaterialText.text = material.displayName;
                AddTextShadow(selectedMaterialText, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));
                SetRect(selectedMaterialText.rectTransform, new Vector2(0f, -slotSize.y * 0.5f + 30f), new Vector2(slotSize.x + 48f, 54f));

                Button button = slot.gameObject.AddComponent<Button>();
                ConfigureButtonColors(button, MaterialSlotColor, MaterialSlotSelectedColor);
                ForgingMaterialDefinition captured = material;
                button.onClick.AddListener(delegate { SelectMaterial(captured); });
                MaterialDragSource dragSource = slot.gameObject.AddComponent<MaterialDragSource>();
                dragSource.Initialize(this, captured);
                materialButtons.Add(button);
            }
        }

        private void BuildDrawer(Font font)
        {
            drawer = CreatePanel(root, "BlueprintDrawer", new Vector2(DrawerWidth, DrawerHeight), InkPanelColor).GetComponent<RectTransform>();
            drawer.anchorMin = new Vector2(1f, 0.5f);
            drawer.anchorMax = new Vector2(1f, 0.5f);
            drawer.pivot = new Vector2(1f, 0.5f);
            drawer.sizeDelta = new Vector2(DrawerWidth, DrawerHeight);

            TextMeshProUGUI drawerTitle = CreateText(drawer, "DrawerTitle", font, 58, TextAlignmentOptions.Center);
            drawerTitle.color = TextGold;
            drawerTitle.fontStyle = FontStyles.Bold;
            drawerTitle.text = "器图";
            AddTextShadow(drawerTitle, new Color(0f, 0f, 0f, 0.9f), new Vector2(3f, -3f));
            SetRect(drawerTitle.rectTransform, new Vector2(0f, 398f), new Vector2(540f, 80f));

            TextMeshProUGUI drawerHint = CreateText(drawer, "DrawerHint", font, 34, TextAlignmentOptions.Center);
            drawerHint.color = new Color(TextPale.r, TextPale.g, TextPale.b, 0.82f);
            drawerHint.text = "选择武器蓝图";
            SetRect(drawerHint.rectTransform, new Vector2(0f, 332f), new Vector2(540f, 48f));

            drawerList = CreateRectObject("BlueprintList", drawer).GetComponent<RectTransform>();
            SetRect(drawerList, new Vector2(0f, -56f), new Vector2(540f, 560f));

            BuildBlueprintButtons(font);

            Button drawerToggle = CreateButton(root, "BlueprintDrawerToggle", font, string.Empty);
            RectTransform toggleRect = drawerToggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.sizeDelta = new Vector2(112f, 280f);
            drawerToggleLabel = drawerToggle.GetComponentInChildren<TextMeshProUGUI>();
            drawerToggleLabel.fontSize = 38;
            drawerToggleLabel.fontStyle = FontStyles.Bold;
            drawerToggleLabel.lineSpacing = 0.9f;
            AddTextShadow(drawerToggleLabel, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));
            drawerToggle.onClick.AddListener(ToggleDrawer);
        }

        private void BuildBlueprintButtons(Font font)
        {
            blueprintButtons.Clear();
            ClearChildren(drawerList);
            for (int i = 0; i < blueprints.Count; i++)
            {
                ForgingWeaponBlueprintDefinition blueprint = blueprints[i];
                Button button = CreateButton(drawerList, "Blueprint_" + blueprint.blueprintId, font, string.Empty);
                RectTransform rect = button.GetComponent<RectTransform>();
                SetRect(rect, new Vector2(0f, 190f - i * 150f), new Vector2(540f, 120f));

                RectTransform preview = CreateRectObject("ShapePreview", button.transform).GetComponent<RectTransform>();
                SetRect(preview, new Vector2(-178f, 0f), new Vector2(88f, 88f));
                BuildBlueprintPreview(preview, blueprint);

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.fontSize = 32;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.lineSpacing = 1.08f;
                label.text = blueprint.displayName + "\n" + blueprint.CellCount + " 格";
                Stretch(label.rectTransform, new Vector2(190f, 20f), new Vector2(-28f, -20f));

                ForgingWeaponBlueprintDefinition captured = blueprint;
                button.onClick.AddListener(delegate { SelectBlueprint(captured); });
                blueprintButtons.Add(button);
            }
        }

        private void BuildBoard()
        {
            ClearChildren(boardRoot);
            cellViews.Clear();
            if (selectedBlueprint == null)
            {
                return;
            }

            Vector2 boardSize = BoardSize(selectedBlueprint);
            boardRoot.sizeDelta = boardSize;
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>(selectedBlueprint.cells);
            for (int y = 0; y < selectedBlueprint.height; y++)
            {
                for (int x = 0; x < selectedBlueprint.width; x++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    bool active = cells.Contains(coord);
                    GameObject cellObject = CreatePanel(boardRoot, "Cell_" + x + "_" + y, new Vector2(CellSize, CellSize), active ? EmptyCellColor : DisabledCellColor);
                    SetRect(cellObject.GetComponent<RectTransform>(), CellPosition(selectedBlueprint, coord), new Vector2(CellSize, CellSize));
                    cellObject.GetComponent<Image>().raycastTarget = active;

                    ForgingCellView view = cellObject.AddComponent<ForgingCellView>();
                    view.Initialize(this, coord, active);
                    cellViews[coord] = view;

                    if (active)
                    {
                        AddDashedBorder(cellObject.transform, CellSize, CellSize);
                    }
                }
            }

            previewRoot = CreateRectObject("PlacementPreview", boardRoot).GetComponent<RectTransform>();
            Stretch(previewRoot);
            previewRoot.SetAsLastSibling();
        }

        private void RefreshAll()
        {
            Evaluate();
            RefreshCells();
            RefreshTexts();
            RefreshButtons();
        }

        private void Evaluate()
        {
            currentResult = ForgingCalculator.Evaluate(selectedBlueprint, placements);
        }

        private void RefreshCells()
        {
            foreach (KeyValuePair<Vector2Int, ForgingCellView> pair in cellViews)
            {
                pair.Value.SetFilled(null, null);
            }

            for (int i = 0; i < placements.Count; i++)
            {
                ForgingPlacedMaterial placement = placements[i];
                Texture texture = LoadTexture(placement.material.texturePath) ?? materialTexture;
                List<Vector2Int> cells = ForgingCalculator.CellsForPlacement(placement);
                for (int j = 0; j < cells.Count; j++)
                {
                    if (cellViews.TryGetValue(cells[j], out ForgingCellView view))
                    {
                        view.SetFilled(placement.material, texture);
                    }
                }
            }
        }

        private void RefreshTexts()
        {
            if (titleText != null)
            {
                titleText.text = selectedBlueprint != null ? selectedBlueprint.displayName : string.Empty;
            }

            if (resultText == null)
            {
                return;
            }

            if (selectedBlueprint == null)
            {
                resultText.text = "选择器图\n放入材料";
                return;
            }

            if (currentResult == null || !currentResult.isValid)
            {
                resultText.text = currentResult != null ? currentResult.invalidReason : "未计算";
                return;
            }

            resultText.text =
                "格子 " + currentResult.filledCellCount + "/" + currentResult.requiredCellCount
                + "\n" + FormatAttributes(currentResult.finalAttributes)
                + "\n伤害 " + currentResult.damage.ToString("0.#")
                + (currentResult.shield > 0f ? "   护盾 " + currentResult.shield.ToString("0.#") : string.Empty)
                + "   词条 " + FormatBonuses(currentResult.bonuses);
        }

        private void RefreshButtons()
        {
            if (forgeButton != null)
            {
                forgeButton.interactable = currentResult != null && currentResult.isValid && currentResult.isComplete;
            }

            for (int i = 0; i < materialButtons.Count && i < materials.Count; i++)
            {
                Transform labelTransform = materialButtons[i].transform.Find("Label");
                TextMeshProUGUI label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
                if (label != null)
                {
                    label.color = materials[i] == selectedMaterial ? TextGold : TextPale;
                }

                Image image = materialButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = materials[i] == selectedMaterial ? MaterialSlotSelectedColor : MaterialSlotColor;
                }
            }

            for (int i = 0; i < blueprintButtons.Count && i < blueprints.Count; i++)
            {
                ColorBlock colors = blueprintButtons[i].colors;
                colors.normalColor = blueprints[i] == selectedBlueprint ? ButtonSelectedColor : ButtonColor;
                blueprintButtons[i].colors = colors;
            }
        }

        private void BuildDragPreview()
        {
            if (previewRoot == null)
            {
                return;
            }

            ClearChildren(previewRoot);
            if (!isDraggingMaterial || draggedMaterial == null || selectedBlueprint == null || !hasDragHoverCell)
            {
                return;
            }

            List<Vector2Int> rotatedCells = ForgingShapeUtility.RotatedCells(draggedMaterial, dragRotationSteps);
            Color color = dragPlacementValid ? DragPreviewValidColor : DragPreviewInvalidColor;
            Texture texture = LoadTexture(draggedMaterial.texturePath) ?? materialTexture;
            for (int i = 0; i < rotatedCells.Count; i++)
            {
                Vector2Int boardCell = dragHoverCell + rotatedCells[i];
                RawImage preview = CreateRectObject("PreviewCell_" + i, previewRoot, typeof(RawImage)).GetComponent<RawImage>();
                preview.texture = texture;
                preview.color = color;
                preview.raycastTarget = false;
                SetRect(preview.rectTransform, CellPosition(selectedBlueprint, boardCell), new Vector2(CellSize, CellSize));
            }
        }

        private void ClearDragPreview()
        {
            if (previewRoot != null)
            {
                ClearChildren(previewRoot);
            }
        }

        private bool TryGetBoardCell(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = Vector2Int.zero;
            if (selectedBlueprint == null || boardRoot == null)
            {
                return false;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRoot, screenPosition, eventCamera, out Vector2 localPoint))
            {
                return false;
            }

            Vector2 boardSize = BoardSize(selectedBlueprint);
            float pitch = CellSize + CellGap;
            int x = Mathf.RoundToInt((localPoint.x + boardSize.x * 0.5f - CellSize * 0.5f) / pitch);
            int y = Mathf.RoundToInt((boardSize.y * 0.5f - CellSize * 0.5f - localPoint.y) / pitch);
            if (x < 0 || y < 0 || x >= selectedBlueprint.width || y >= selectedBlueprint.height)
            {
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
        }

        private void EnsureDragGhost(ForgingMaterialDefinition material)
        {
            ClearDragGhost();
            if (root == null || material == null)
            {
                return;
            }

            dragGhostRoot = CreateRectObject("MaterialDragGhost", root).GetComponent<RectTransform>();
            dragGhostRoot.SetAsLastSibling();
            Vector2Int rotatedSize = ForgingShapeUtility.RotatedSize(material, dragRotationSteps);
            SetRect(dragGhostRoot, Vector2.zero, new Vector2(
                rotatedSize.x * CellSize + Mathf.Max(0, rotatedSize.x - 1) * CellGap,
                rotatedSize.y * CellSize + Mathf.Max(0, rotatedSize.y - 1) * CellGap));

            Texture texture = LoadTexture(material.texturePath) ?? materialTexture;
            List<Vector2Int> rotatedCells = ForgingShapeUtility.RotatedCells(material, dragRotationSteps);
            Vector2 ghostSize = dragGhostRoot.sizeDelta;
            for (int i = 0; i < rotatedCells.Count; i++)
            {
                Vector2Int cell = rotatedCells[i];
                RawImage image = CreateRectObject("GhostCell_" + i, dragGhostRoot, typeof(RawImage)).GetComponent<RawImage>();
                image.texture = texture;
                image.color = DragGhostColor;
                image.raycastTarget = false;
                Vector2 position = new Vector2(
                    -ghostSize.x * 0.5f + CellSize * 0.5f + cell.x * (CellSize + CellGap),
                    ghostSize.y * 0.5f - CellSize * 0.5f - cell.y * (CellSize + CellGap));
                SetRect(image.rectTransform, position, new Vector2(CellSize * 0.88f, CellSize * 0.88f));
            }
        }

        private void MoveDragGhost(Vector2 screenPosition)
        {
            if (dragGhostRoot == null || root == null)
            {
                return;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, eventCamera, out Vector2 localPoint))
            {
                dragGhostRoot.anchoredPosition = localPoint + new Vector2(32f, -32f);
            }
        }

        private void ClearDragGhost()
        {
            if (dragGhostRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(dragGhostRoot.gameObject);
            }
            else
            {
                DestroyImmediate(dragGhostRoot.gameObject);
            }

            dragGhostRoot = null;
        }

        private void SetDrawerOpen(bool open, bool immediate)
        {
            drawerOpen = open;
            if (drawer != null)
            {
                drawer.anchoredPosition = layoutPreset != null
                    ? open ? layoutPreset.DrawerOpenAnchoredPosition : layoutPreset.DrawerClosedAnchoredPosition
                    : open ? new Vector2(-36f, 0f) : new Vector2(DrawerWidth + 42f, 0f);
            }

            if (drawerToggleLabel != null)
            {
                drawerToggleLabel.text = open ? "收\n起" : "器\n图";
                RectTransform toggleRect = drawerToggleLabel.transform.parent.GetComponent<RectTransform>();
                toggleRect.anchoredPosition = layoutPreset != null
                    ? open ? layoutPreset.DrawerToggleOpenAnchoredPosition : layoutPreset.DrawerToggleClosedAnchoredPosition
                    : open ? new Vector2(-DrawerWidth - 36f, -4f) : new Vector2(-28f, -4f);
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = string.IsNullOrWhiteSpace(message) ? " " : message;
            }
        }

        private void ApplyLayoutPreset()
        {
            if (layoutPreset == null || root == null)
            {
                return;
            }

            layoutPreset.ApplyTo(root);
        }

        private void AddDashedBorder(Transform parent, float width, float height)
        {
            float halfWidth = width * 0.5f - 6f;
            float halfHeight = height * 0.5f - 6f;
            for (int i = 0; i < 6; i++)
            {
                float tx = Mathf.Lerp(-halfWidth + 8f, halfWidth - 8f, i / 5f);
                CreateDash(parent, "DashTop_" + i, new Vector2(tx, halfHeight), new Vector2(13f, 2f));
                CreateDash(parent, "DashBottom_" + i, new Vector2(tx, -halfHeight), new Vector2(13f, 2f));
                float ty = Mathf.Lerp(-halfHeight + 8f, halfHeight - 8f, i / 5f);
                CreateDash(parent, "DashLeft_" + i, new Vector2(-halfWidth, ty), new Vector2(2f, 13f));
                CreateDash(parent, "DashRight_" + i, new Vector2(halfWidth, ty), new Vector2(2f, 13f));
            }
        }

        private void CreateDash(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Image dash = CreateRectObject(name, parent, typeof(Image)).GetComponent<Image>();
            dash.color = DashColor;
            dash.raycastTarget = false;
            SetRect(dash.rectTransform, position, size);
        }

        private void BuildBlueprintPreview(RectTransform parent, ForgingWeaponBlueprintDefinition blueprint)
        {
            if (blueprint == null)
            {
                return;
            }

            AddBorder(parent, "PreviewFrame", parent.sizeDelta, new Color(0.85f, 0.62f, 0.28f, 0.2f), 2f);

            float previewBounds = Mathf.Max(48f, Mathf.Min(parent.sizeDelta.x, parent.sizeDelta.y) - 14f);
            const float previewGap = 4f;
            float maxDimension = Mathf.Max(blueprint.width, blueprint.height);
            float previewCellSize = (previewBounds - Mathf.Max(0f, maxDimension - 1f) * previewGap) / Mathf.Max(1f, maxDimension);
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>(blueprint.cells);
            Vector2 totalSize = new Vector2(
                blueprint.width * previewCellSize + Mathf.Max(0, blueprint.width - 1) * previewGap,
                blueprint.height * previewCellSize + Mathf.Max(0, blueprint.height - 1) * previewGap);

            for (int y = 0; y < blueprint.height; y++)
            {
                for (int x = 0; x < blueprint.width; x++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    bool active = cells.Contains(coord);
                    Image cell = CreateRectObject("PreviewCell_" + x + "_" + y, parent, typeof(Image)).GetComponent<Image>();
                    cell.color = active ? new Color(0.94f, 0.67f, 0.28f, 0.84f) : new Color(0.18f, 0.13f, 0.08f, 0.32f);
                    cell.raycastTarget = false;
                    Vector2 position = new Vector2(
                        -totalSize.x * 0.5f + previewCellSize * 0.5f + x * (previewCellSize + previewGap),
                        totalSize.y * 0.5f - previewCellSize * 0.5f - y * (previewCellSize + previewGap));
                    SetRect(cell.rectTransform, position, new Vector2(previewCellSize, previewCellSize));
                }
            }
        }

        private Texture LoadTexture(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return null;
            }

            if (textureCache.TryGetValue(projectPath, out Texture cached))
            {
                return cached;
            }

            Texture texture = null;
#if UNITY_EDITOR
            texture = AssetDatabase.LoadAssetAtPath<Texture>(projectPath);
#endif
            textureCache[projectPath] = texture;
            return texture;
        }

        private static string ShortSkillText(ForgingWeaponBlueprintDefinition blueprint)
        {
            if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.skillDescription))
            {
                return string.Empty;
            }

            string text = blueprint.skillDescription.Replace("\n", string.Empty);
            return text.Length > 24 ? text.Substring(0, 24) + "..." : text;
        }

        private static string FormatAttributes(ForgingElementAttributes attributes)
        {
            if (attributes == null)
            {
                return "金0 水0 木0 火0 土0";
            }

            return "金" + attributes.metal.ToString("0.#")
                + " 水" + attributes.water.ToString("0.#")
                + " 木" + attributes.wood.ToString("0.#")
                + " 火" + attributes.fire.ToString("0.#")
                + " 土" + attributes.earth.ToString("0.#");
        }

        private static string FormatBonuses(IReadOnlyList<ForgingWeaponBonusResult> bonuses)
        {
            if (bonuses == null || bonuses.Count == 0)
            {
                return "无";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < bonuses.Count; i++)
            {
                ForgingWeaponBonusResult bonus = bonuses[i];
                parts.Add(bonus.sourceMaterialName + " +" + bonus.minValue.ToString("0.##") + "-" + bonus.maxValue.ToString("0.##"));
            }

            return string.Join("；", parts);
        }

        private static Vector2 BoardSize(ForgingWeaponBlueprintDefinition blueprint)
        {
            return new Vector2(
                blueprint.width * CellSize + Mathf.Max(0, blueprint.width - 1) * CellGap,
                blueprint.height * CellSize + Mathf.Max(0, blueprint.height - 1) * CellGap);
        }

        private static Vector2 CellPosition(ForgingWeaponBlueprintDefinition blueprint, Vector2Int cell)
        {
            Vector2 size = BoardSize(blueprint);
            return new Vector2(
                -size.x * 0.5f + CellSize * 0.5f + cell.x * (CellSize + CellGap),
                size.y * 0.5f - CellSize * 0.5f - cell.y * (CellSize + CellGap));
        }

        private static Font CreateFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void DestroyRuntimeRoot()
        {
            Transform existing = transform.Find(RuntimeRootName);
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        private static GameObject CreateRectObject(string objectName, Transform parent, params Type[] components)
        {
            List<Type> types = new List<Type> { typeof(RectTransform) };
            if (components != null)
            {
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != typeof(RectTransform))
                    {
                        types.Add(components[i]);
                    }
                }
            }

            GameObject gameObject = new GameObject(objectName, types.ToArray());
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static GameObject CreatePanel(Transform parent, string objectName, Vector2 size, Color color)
        {
            GameObject panel = CreateRectObject(objectName, parent, typeof(Image));
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            panel.GetComponent<RectTransform>().sizeDelta = size;
            return panel;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, Font font, int fontSize, TextAnchor alignment)
        {
            return CreateText(parent, objectName, font, fontSize, ToTmpAlignment(alignment));
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, Font font, int fontSize, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = CreateRectObject(objectName, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            TMP_FontAsset tmpFont = GetRuntimeTmpFont();
            if (tmpFont != null)
            {
                text.font = tmpFont;
            }

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_FontAsset GetRuntimeTmpFont()
        {
            if (runtimeFontAsset != null)
            {
                return runtimeFontAsset;
            }

            string[] families = { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "Arial Unicode MS" };
            for (int i = 0; i < families.Length; i++)
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(families[i], "Regular", 90);
                if (asset != null)
                {
                    asset.name = "ForgingRuntime_" + families[i] + "_TMP";
                    asset.atlasPopulationMode = AtlasPopulationMode.DynamicOS;
                    runtimeFontAsset = asset;
                    return runtimeFontAsset;
                }
            }

            return null;
        }

        private static Button CreateButton(Transform parent, string objectName, Font font, string label)
        {
            GameObject buttonObject = CreatePanel(parent, objectName, new Vector2(180f, 52f), ButtonColor);
            Button button = buttonObject.AddComponent<Button>();
            ConfigureButtonColors(button);

            TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", font, 19, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = TextPale;
            Stretch(text.rectTransform, new Vector2(14f, 6f), new Vector2(-14f, -6f));
            return button;
        }

        private static void ConfigureButtonColors(Button button)
        {
            ConfigureButtonColors(button, ButtonColor, ButtonSelectedColor);
        }

        private static void ConfigureButtonColors(Button button, Color normalColor, Color selectedColor)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, selectedColor, 0.48f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.42f);
            colors.selectedColor = selectedColor;
            colors.disabledColor = new Color(0.08f, 0.075f, 0.07f, 0.52f);
            button.colors = colors;
        }

        private static void AddTextShadow(TextMeshProUGUI text, Color color, Vector2 distance)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static bool WasRotatePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        private static float ReadScrollDeltaY()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.scroll.ReadValue().y;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        private static Vector2 ReadPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        private static void AddBorder(Transform parent, string namePrefix, Vector2 size, Color color, float thickness)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            CreateLine(parent, namePrefix + "_Top", new Vector2(0f, halfHeight), new Vector2(size.x, thickness), color);
            CreateLine(parent, namePrefix + "_Bottom", new Vector2(0f, -halfHeight), new Vector2(size.x, thickness), color);
            CreateLine(parent, namePrefix + "_Left", new Vector2(-halfWidth, 0f), new Vector2(thickness, size.y), color);
            CreateLine(parent, namePrefix + "_Right", new Vector2(halfWidth, 0f), new Vector2(thickness, size.y), color);
        }

        private static void AddCornerBrackets(Transform parent, Vector2 size, Color color, float length, float thickness)
        {
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            CreateLine(parent, "Corner_TL_H", new Vector2(-x + length * 0.5f, y), new Vector2(length, thickness), color);
            CreateLine(parent, "Corner_TL_V", new Vector2(-x, y - length * 0.5f), new Vector2(thickness, length), color);
            CreateLine(parent, "Corner_TR_H", new Vector2(x - length * 0.5f, y), new Vector2(length, thickness), color);
            CreateLine(parent, "Corner_TR_V", new Vector2(x, y - length * 0.5f), new Vector2(thickness, length), color);
            CreateLine(parent, "Corner_BL_H", new Vector2(-x + length * 0.5f, -y), new Vector2(length, thickness), color);
            CreateLine(parent, "Corner_BL_V", new Vector2(-x, -y + length * 0.5f), new Vector2(thickness, length), color);
            CreateLine(parent, "Corner_BR_H", new Vector2(x - length * 0.5f, -y), new Vector2(length, thickness), color);
            CreateLine(parent, "Corner_BR_V", new Vector2(x, -y + length * 0.5f), new Vector2(thickness, length), color);
        }

        private static void CreateLine(Transform parent, string objectName, Vector2 position, Vector2 size, Color color)
        {
            Image line = CreateRectObject(objectName, parent, typeof(Image)).GetComponent<Image>();
            line.color = color;
            line.raycastTarget = false;
            SetRect(line.rectTransform, position, size);
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private sealed class ForgingCellView : MonoBehaviour, IPointerClickHandler
        {
            private ForgingWorkbenchController controller;
            private Vector2Int coordinate;
            private bool active;
            private Image background;
            private RawImage materialImage;

            public void Initialize(ForgingWorkbenchController owner, Vector2Int cell, bool isActiveCell)
            {
                controller = owner;
                coordinate = cell;
                active = isActiveCell;
                background = GetComponent<Image>();
                materialImage = CreateRectObject("MaterialImage", transform, typeof(RawImage)).GetComponent<RawImage>();
                materialImage.raycastTarget = false;
                materialImage.color = Color.clear;
                SetRect(materialImage.rectTransform, Vector2.zero, new Vector2(CellSize * 0.72f, CellSize * 0.72f));
                SetFilled(null, null);
            }

            public void SetFilled(ForgingMaterialDefinition material, Texture texture)
            {
                if (background != null)
                {
                    background.color = !active ? DisabledCellColor : material != null ? FilledCellColor : EmptyCellColor;
                }

                if (materialImage != null)
                {
                    materialImage.texture = texture;
                    materialImage.color = material != null ? Color.white : Color.clear;
                }
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (active && controller != null)
                {
                    controller.TryPlaceAt(coordinate);
                }
            }
        }

        private sealed class MaterialDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private ForgingWorkbenchController controller;
            private ForgingMaterialDefinition material;

            public void Initialize(ForgingWorkbenchController owner, ForgingMaterialDefinition sourceMaterial)
            {
                controller = owner;
                material = sourceMaterial;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (controller != null)
                {
                    controller.BeginMaterialDrag(material, eventData);
                }
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (controller != null)
                {
                    controller.UpdateMaterialDrag(eventData);
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (controller != null)
                {
                    controller.EndMaterialDrag(eventData);
                }
            }
        }
    }
}
