using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Forging
{
    [CreateAssetMenu(fileName = "ForgingMaterial", menuName = "NewFPG/Forging/Material Config")]
    public sealed class ForgingMaterialConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string materialId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int rarity = 1;
        [SerializeField, Min(0)] private int value;

        [Header("Grid Shape")]
        [SerializeField, Min(1)] private int shapeWidth = 1;
        [SerializeField, Min(1)] private int shapeHeight = 1;
        [SerializeField] private List<Vector2Int> cells = new List<Vector2Int> { Vector2Int.zero };

        [Header("Attributes")]
        [SerializeField] private ForgingElementAttributes attributes = new ForgingElementAttributes();
        [SerializeField] private List<ForgingNeighborRule> neighborRules = new List<ForgingNeighborRule>();
        [SerializeField] private List<ForgingWeaponBonus> weaponBonuses = new List<ForgingWeaponBonus>();

        [Header("Presentation")]
        [SerializeField] private Texture texture;
        [SerializeField] private string texturePath;
        [SerializeField] private ForgingUiSlot uiSlot = new ForgingUiSlot();
        [SerializeField, TextArea] private string description;

        public string MaterialId => materialId;

        public ForgingMaterialDefinition ToDefinition()
        {
            ForgingMaterialDefinition definition = new ForgingMaterialDefinition
            {
                materialId = materialId,
                displayName = displayName,
                rarity = Mathf.Max(1, rarity),
                value = Mathf.Max(0, value),
                shapeWidth = Mathf.Max(1, shapeWidth),
                shapeHeight = Mathf.Max(1, shapeHeight),
                cells = new List<Vector2Int>(cells ?? new List<Vector2Int> { Vector2Int.zero }),
                attributes = attributes ?? new ForgingElementAttributes(),
                neighborRules = new List<ForgingNeighborRule>(neighborRules ?? new List<ForgingNeighborRule>()),
                weaponBonuses = new List<ForgingWeaponBonus>(weaponBonuses ?? new List<ForgingWeaponBonus>()),
                texturePath = ForgingAssetPathUtility.GetAssetPath(texture, texturePath),
                uiSlot = uiSlot ?? new ForgingUiSlot(),
                description = description,
            };

            definition.neighborRule = definition.neighborRules.Count > 0
                ? definition.neighborRules[0]
                : new ForgingNeighborRule();
            definition.weaponBonus = definition.weaponBonuses.Count > 0
                ? definition.weaponBonuses[0]
                : new ForgingWeaponBonus();
            return definition;
        }

        public void ApplyDefinition(ForgingMaterialDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            materialId = definition.materialId;
            displayName = definition.displayName;
            rarity = Mathf.Max(1, definition.rarity);
            value = Mathf.Max(0, definition.value);
            shapeWidth = Mathf.Max(1, definition.shapeWidth);
            shapeHeight = Mathf.Max(1, definition.shapeHeight);
            cells = new List<Vector2Int>(definition.cells ?? new List<Vector2Int> { Vector2Int.zero });
            attributes = definition.attributes ?? new ForgingElementAttributes();
            neighborRules = new List<ForgingNeighborRule>(definition.neighborRules ?? new List<ForgingNeighborRule>());
            weaponBonuses = new List<ForgingWeaponBonus>(definition.weaponBonuses ?? new List<ForgingWeaponBonus>());
            texturePath = definition.texturePath;
            texture = ForgingAssetPathUtility.LoadAssetAtPath<Texture>(texturePath);
            uiSlot = definition.uiSlot ?? new ForgingUiSlot();
            description = definition.description;
            NormalizeShape();
        }

        private void OnValidate()
        {
            NormalizeShape();
        }

        private void NormalizeShape()
        {
            shapeWidth = Mathf.Max(1, shapeWidth);
            shapeHeight = Mathf.Max(1, shapeHeight);
            ForgingShapeUtility.NormalizeCells(cells, shapeWidth, shapeHeight, true);
        }
    }
}
