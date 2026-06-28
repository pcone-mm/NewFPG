using System.Collections.Generic;
using NewFPG.Combat;
using NewFPG.Combat.SkillIndicators;
using UnityEngine;

namespace NewFPG.Forging
{
    [CreateAssetMenu(fileName = "ForgingWeaponBlueprint", menuName = "NewFPG/Forging/Weapon Blueprint Config")]
    public sealed class ForgingWeaponBlueprintConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string blueprintId;
        [SerializeField] private string displayName;
        [SerializeField] private string skillLogicId;

        [Header("Grid Shape")]
        [SerializeField, Min(1)] private int width = 3;
        [SerializeField, Min(1)] private int height = 3;
        [SerializeField] private List<Vector2Int> cells = new List<Vector2Int>();

        [Header("Skill Scaling")]
        [SerializeField] private List<ForgingSkillScaling> skillScalings = new List<ForgingSkillScaling>();
        [SerializeField, TextArea] private string skillDescription;

        [Header("Runtime Binding")]
        [SerializeField] private WeaponDefinition weaponDefinitionAsset;
        [SerializeField] private string weaponDefinitionAssetPath;
        [SerializeField] private Sprite hudIcon;
        [SerializeField] private string hudIconPath;
        [SerializeField] private SkillIndicatorConfig indicatorConfig;
        [SerializeField] private string indicatorConfigPath;
        [SerializeField] private GameObject releaseEffectPrefab;
        [SerializeField] private string releaseEffectPrefabPath;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private string hitEffectPrefabPath;
        [SerializeField, Min(0f)] private float resourceCost;
        [SerializeField, Min(0f)] private float baseDamage;
        [SerializeField, Min(0.05f)] private float cooldown = 0.4f;
        [SerializeField, Min(0.1f)] private float range = 8f;
        [SerializeField, Min(0.05f)] private float radius = 0.55f;

        public string BlueprintId => blueprintId;

        public ForgingWeaponBlueprintDefinition ToDefinition()
        {
            return new ForgingWeaponBlueprintDefinition
            {
                blueprintId = blueprintId,
                displayName = displayName,
                skillLogicId = skillLogicId,
                width = Mathf.Max(1, width),
                height = Mathf.Max(1, height),
                cells = new List<Vector2Int>(cells ?? new List<Vector2Int>()),
                skillScalings = new List<ForgingSkillScaling>(skillScalings ?? new List<ForgingSkillScaling>()),
                runtime = new ForgingWeaponRuntimeBinding
                {
                    weaponDefinitionAssetPath = ForgingAssetPathUtility.GetAssetPath(weaponDefinitionAsset, weaponDefinitionAssetPath),
                    hudIconPath = ForgingAssetPathUtility.GetAssetPath(hudIcon, hudIconPath),
                    indicatorConfigPath = ForgingAssetPathUtility.GetAssetPath(indicatorConfig, indicatorConfigPath),
                    releaseEffectPrefabPath = ForgingAssetPathUtility.GetAssetPath(releaseEffectPrefab, releaseEffectPrefabPath),
                    hitEffectPrefabPath = ForgingAssetPathUtility.GetAssetPath(hitEffectPrefab, hitEffectPrefabPath),
                    resourceCost = Mathf.Max(0f, resourceCost),
                    baseDamage = Mathf.Max(0f, baseDamage),
                    cooldown = Mathf.Max(0.05f, cooldown),
                    range = Mathf.Max(0.1f, range),
                    radius = Mathf.Max(0.05f, radius),
                },
                skillDescription = skillDescription,
            };
        }

        public void ApplyDefinition(ForgingWeaponBlueprintDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            blueprintId = definition.blueprintId;
            displayName = definition.displayName;
            skillLogicId = definition.skillLogicId;
            width = Mathf.Max(1, definition.width);
            height = Mathf.Max(1, definition.height);
            cells = new List<Vector2Int>(definition.cells ?? new List<Vector2Int>());
            skillScalings = new List<ForgingSkillScaling>(definition.skillScalings ?? new List<ForgingSkillScaling>());
            skillDescription = definition.skillDescription;

            ForgingWeaponRuntimeBinding runtime = definition.runtime ?? new ForgingWeaponRuntimeBinding();
            weaponDefinitionAssetPath = runtime.weaponDefinitionAssetPath;
            hudIconPath = runtime.hudIconPath;
            indicatorConfigPath = runtime.indicatorConfigPath;
            releaseEffectPrefabPath = runtime.releaseEffectPrefabPath;
            hitEffectPrefabPath = runtime.hitEffectPrefabPath;
            weaponDefinitionAsset = ForgingAssetPathUtility.LoadAssetAtPath<WeaponDefinition>(weaponDefinitionAssetPath);
            hudIcon = ForgingAssetPathUtility.LoadAssetAtPath<Sprite>(hudIconPath);
            indicatorConfig = ForgingAssetPathUtility.LoadAssetAtPath<SkillIndicatorConfig>(indicatorConfigPath);
            releaseEffectPrefab = ForgingAssetPathUtility.LoadAssetAtPath<GameObject>(releaseEffectPrefabPath);
            hitEffectPrefab = ForgingAssetPathUtility.LoadAssetAtPath<GameObject>(hitEffectPrefabPath);
            resourceCost = Mathf.Max(0f, runtime.resourceCost);
            baseDamage = Mathf.Max(0f, runtime.baseDamage);
            cooldown = Mathf.Max(0.05f, runtime.cooldown);
            range = Mathf.Max(0.1f, runtime.range);
            radius = Mathf.Max(0.05f, runtime.radius);
            NormalizeShape();
        }

        private void OnValidate()
        {
            NormalizeShape();
        }

        private void NormalizeShape()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            ForgingShapeUtility.NormalizeCells(cells, width, height, false);
        }
    }
}
