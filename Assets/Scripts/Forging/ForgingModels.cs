using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace NewFPG.Forging
{
    public enum ForgingElement
    {
        Metal,
        Water,
        Wood,
        Fire,
        Earth,
    }

    public enum ForgingSkillValueType
    {
        Damage,
        Shield,
    }

    public enum ForgingNeighborRuleType
    {
        None,
        MultiplyElement,
    }

    public enum ForgingWeaponBonusType
    {
        None,
        ElementDamagePercentRange,
    }

    [Serializable]
    public sealed class ForgingElementAttributes
    {
        [Min(0f)] public float metal;
        [Min(0f)] public float water;
        [Min(0f)] public float wood;
        [Min(0f)] public float fire;
        [Min(0f)] public float earth;

        public ForgingElementAttributes()
        {
        }

        public ForgingElementAttributes(float metal, float water, float wood, float fire, float earth)
        {
            this.metal = Mathf.Max(0f, metal);
            this.water = Mathf.Max(0f, water);
            this.wood = Mathf.Max(0f, wood);
            this.fire = Mathf.Max(0f, fire);
            this.earth = Mathf.Max(0f, earth);
        }

        [JsonIgnore] public float Total => metal + water + wood + fire + earth;

        public float Get(ForgingElement element)
        {
            switch (element)
            {
                case ForgingElement.Metal:
                    return metal;
                case ForgingElement.Water:
                    return water;
                case ForgingElement.Wood:
                    return wood;
                case ForgingElement.Fire:
                    return fire;
                case ForgingElement.Earth:
                    return earth;
                default:
                    return 0f;
            }
        }

        public void Set(ForgingElement element, float value)
        {
            value = Mathf.Max(0f, value);
            switch (element)
            {
                case ForgingElement.Metal:
                    metal = value;
                    break;
                case ForgingElement.Water:
                    water = value;
                    break;
                case ForgingElement.Wood:
                    wood = value;
                    break;
                case ForgingElement.Fire:
                    fire = value;
                    break;
                case ForgingElement.Earth:
                    earth = value;
                    break;
            }
        }

        public void Add(ForgingElementAttributes other)
        {
            if (other == null)
            {
                return;
            }

            metal += other.metal;
            water += other.water;
            wood += other.wood;
            fire += other.fire;
            earth += other.earth;
        }

        public void Multiply(ForgingElement element, float multiplier)
        {
            Set(element, Get(element) * multiplier);
        }

        public ForgingElementAttributes Clone()
        {
            return new ForgingElementAttributes(metal, water, wood, fire, earth);
        }

        public override string ToString()
        {
            return $"Metal {metal:0.#}, Water {water:0.#}, Wood {wood:0.#}, Fire {fire:0.#}, Earth {earth:0.#}";
        }
    }

    [Serializable]
    public sealed class ForgingSkillScaling
    {
        public ForgingSkillValueType valueType = ForgingSkillValueType.Damage;
        public ForgingElement element = ForgingElement.Wood;
        [Min(0f)] public float coefficient = 1f;

        public ForgingSkillScaling()
        {
        }

        public ForgingSkillScaling(ForgingSkillValueType valueType, ForgingElement element, float coefficient)
        {
            this.valueType = valueType;
            this.element = element;
            this.coefficient = Mathf.Max(0f, coefficient);
        }
    }

    [Serializable]
    public sealed class ForgingNeighborRule
    {
        public ForgingNeighborRuleType ruleType = ForgingNeighborRuleType.None;
        public ForgingElement targetElement = ForgingElement.Wood;
        [Min(0f)] public float multiplier = 1f;
        public bool includeDiagonals;
        [TextArea] public string description;
    }

    [Serializable]
    public sealed class ForgingWeaponBonus
    {
        public ForgingWeaponBonusType bonusType = ForgingWeaponBonusType.None;
        public ForgingElement element = ForgingElement.Metal;
        [Min(0f)] public float minPercent;
        [Min(0f)] public float maxPercent;
        [TextArea] public string description;
    }

    [Serializable]
    public sealed class ForgingUiSlot
    {
        public Vector2 anchoredPosition;
        public Vector2 size = new Vector2(220f, 190f);
    }

    [Serializable]
    public sealed class ForgingMaterialDefinition
    {
        public string materialId;
        public string displayName;
        [Min(1)] public int rarity = 1;
        [Min(0)] public int value;
        [Min(1)] public int shapeWidth = 1;
        [Min(1)] public int shapeHeight = 1;
        public List<Vector2Int> cells = new List<Vector2Int> { Vector2Int.zero };
        public ForgingElementAttributes attributes = new ForgingElementAttributes();
        public ForgingNeighborRule neighborRule = new ForgingNeighborRule();
        public List<ForgingNeighborRule> neighborRules = new List<ForgingNeighborRule>();
        public ForgingWeaponBonus weaponBonus = new ForgingWeaponBonus();
        public List<ForgingWeaponBonus> weaponBonuses = new List<ForgingWeaponBonus>();
        public string texturePath;
        public ForgingUiSlot uiSlot = new ForgingUiSlot();
        [TextArea] public string description;

        public int CellCount => cells != null && cells.Count > 0 ? cells.Count : 1;
    }

    [Serializable]
    public sealed class ForgingWeaponRuntimeBinding
    {
        public string weaponDefinitionAssetPath;
        public string hudIconPath;
        public string indicatorConfigPath;
        public string releaseEffectPrefabPath;
        public string hitEffectPrefabPath;
        [Min(0f)] public float resourceCost;
        [Min(0f)] public float baseDamage;
        [Min(0.05f)] public float cooldown = 0.4f;
        [Min(0.1f)] public float range = 8f;
        [Min(0.05f)] public float radius = 0.55f;
    }

    [Serializable]
    public sealed class ForgingWeaponBlueprintDefinition
    {
        public string blueprintId;
        public string displayName;
        public int width = 3;
        public int height = 3;
        public List<Vector2Int> cells = new List<Vector2Int>();
        public List<ForgingSkillScaling> skillScalings = new List<ForgingSkillScaling>();
        public string skillLogicId;
        public ForgingWeaponRuntimeBinding runtime = new ForgingWeaponRuntimeBinding();
        [TextArea] public string skillDescription;

        public int CellCount => cells != null ? cells.Count : 0;
    }

    [Serializable]
    public sealed class ForgingPlacedMaterial
    {
        public ForgingMaterialDefinition material;
        public Vector2Int origin;
        public int rotationSteps;

        public ForgingPlacedMaterial()
        {
        }

        public ForgingPlacedMaterial(ForgingMaterialDefinition material, Vector2Int origin)
            : this(material, origin, 0)
        {
        }

        public ForgingPlacedMaterial(ForgingMaterialDefinition material, Vector2Int origin, int rotationSteps)
        {
            this.material = material;
            this.origin = origin;
            this.rotationSteps = rotationSteps;
        }
    }

    [Serializable]
    public sealed class ForgingMaterialContribution
    {
        public ForgingMaterialDefinition material;
        public Vector2Int origin;
        public int rotationSteps;
        public ForgingElementAttributes baseAttributes = new ForgingElementAttributes();
        public ForgingElementAttributes effectiveAttributes = new ForgingElementAttributes();
        public List<string> appliedRuleDescriptions = new List<string>();
    }

    [Serializable]
    public sealed class ForgingWeaponBonusResult
    {
        public string sourceMaterialId;
        public string sourceMaterialName;
        public ForgingWeaponBonusType bonusType;
        public ForgingElement element;
        public float minValue;
        public float maxValue;
        public string description;
    }

    [Serializable]
    public sealed class ForgedWeaponRuntimeStats
    {
        public string blueprintId;
        public string displayName;
        public string skillLogicId;
        public ForgingElementAttributes attributes = new ForgingElementAttributes();
        public float damage;
        public float shield;
        public List<ForgingWeaponBonusResult> bonuses = new List<ForgingWeaponBonusResult>();

        public bool HasDamageOverride => damage > 0f;
        public bool HasShield => shield > 0f;
        public float BonusDamageAverage
        {
            get
            {
                if (bonuses == null)
                {
                    return 0f;
                }

                float total = 0f;
                for (int i = 0; i < bonuses.Count; i++)
                {
                    ForgingWeaponBonusResult bonus = bonuses[i];
                    if (bonus != null && bonus.bonusType == ForgingWeaponBonusType.ElementDamagePercentRange)
                    {
                        total += (Mathf.Max(0f, bonus.minValue) + Mathf.Max(0f, bonus.maxValue)) * 0.5f;
                    }
                }

                return total;
            }
        }
    }

    [Serializable]
    public sealed class ForgingResult
    {
        public ForgingWeaponBlueprintDefinition blueprint;
        public bool isValid;
        public bool isComplete;
        public string invalidReason;
        public int filledCellCount;
        public int requiredCellCount;
        public ForgingElementAttributes finalAttributes = new ForgingElementAttributes();
        public float damage;
        public float shield;
        public List<ForgingMaterialContribution> contributions = new List<ForgingMaterialContribution>();
        public List<ForgingWeaponBonusResult> bonuses = new List<ForgingWeaponBonusResult>();

        public ForgedWeaponRuntimeStats ToRuntimeStats()
        {
            return new ForgedWeaponRuntimeStats
            {
                blueprintId = blueprint != null ? blueprint.blueprintId : string.Empty,
                displayName = blueprint != null ? blueprint.displayName : string.Empty,
                skillLogicId = blueprint != null ? blueprint.skillLogicId : string.Empty,
                attributes = finalAttributes != null ? finalAttributes.Clone() : new ForgingElementAttributes(),
                damage = damage,
                shield = shield,
                bonuses = new List<ForgingWeaponBonusResult>(bonuses ?? new List<ForgingWeaponBonusResult>()),
            };
        }
    }
}
