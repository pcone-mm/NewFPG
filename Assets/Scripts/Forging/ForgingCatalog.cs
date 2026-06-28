using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace NewFPG.Forging
{
    [Serializable]
    public sealed class ForgingCatalog
    {
        public string version;
        public string source;
        public List<ForgingWeaponBlueprintDefinition> weaponBlueprints = new List<ForgingWeaponBlueprintDefinition>();
        public List<ForgingMaterialDefinition> materials = new List<ForgingMaterialDefinition>();

        public bool IsEmpty => (weaponBlueprints == null || weaponBlueprints.Count == 0)
            && (materials == null || materials.Count == 0);

        public static ForgingCatalog FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new ForgingCatalog();
            }

            ForgingCatalogDto dto = JsonConvert.DeserializeObject<ForgingCatalogDto>(json);
            ForgingCatalog catalog = dto != null ? dto.ToCatalog() : new ForgingCatalog();
            catalog.Normalize();
            return catalog;
        }

        public string ToJson(bool includeWeaponBlueprints = true, bool includeMaterials = true)
        {
            Normalize();
            ForgingCatalogDto dto = ForgingCatalogDto.FromCatalog(this, includeWeaponBlueprints, includeMaterials);
            return JsonConvert.SerializeObject(dto, Formatting.Indented);
        }

        public void Normalize()
        {
            if (weaponBlueprints == null)
            {
                weaponBlueprints = new List<ForgingWeaponBlueprintDefinition>();
            }

            if (materials == null)
            {
                materials = new List<ForgingMaterialDefinition>();
            }

            for (int i = 0; i < weaponBlueprints.Count; i++)
            {
                NormalizeBlueprint(weaponBlueprints[i]);
            }

            for (int i = 0; i < materials.Count; i++)
            {
                NormalizeMaterial(materials[i]);
            }
        }

        public ForgingWeaponBlueprintDefinition FindBlueprint(string blueprintId)
        {
            if (string.IsNullOrWhiteSpace(blueprintId) || weaponBlueprints == null)
            {
                return null;
            }

            for (int i = 0; i < weaponBlueprints.Count; i++)
            {
                ForgingWeaponBlueprintDefinition blueprint = weaponBlueprints[i];
                if (blueprint != null && blueprint.blueprintId == blueprintId)
                {
                    return blueprint;
                }
            }

            return null;
        }

        public ForgingMaterialDefinition FindMaterial(string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId) || materials == null)
            {
                return null;
            }

            for (int i = 0; i < materials.Count; i++)
            {
                ForgingMaterialDefinition material = materials[i];
                if (material != null && material.materialId == materialId)
                {
                    return material;
                }
            }

            return null;
        }

        private static void NormalizeBlueprint(ForgingWeaponBlueprintDefinition blueprint)
        {
            if (blueprint == null)
            {
                return;
            }

            blueprint.width = Mathf.Max(1, blueprint.width);
            blueprint.height = Mathf.Max(1, blueprint.height);
            if (blueprint.cells == null)
            {
                blueprint.cells = new List<Vector2Int>();
            }

            FitDimensionsToCells(ref blueprint.width, ref blueprint.height, blueprint.cells);

            if (blueprint.skillScalings == null)
            {
                blueprint.skillScalings = new List<ForgingSkillScaling>();
            }

            if (blueprint.runtime == null)
            {
                blueprint.runtime = new ForgingWeaponRuntimeBinding();
            }
        }

        private static void NormalizeMaterial(ForgingMaterialDefinition material)
        {
            if (material == null)
            {
                return;
            }

            material.rarity = Mathf.Max(1, material.rarity);
            material.value = Mathf.Max(0, material.value);
            material.shapeWidth = Mathf.Max(1, material.shapeWidth);
            material.shapeHeight = Mathf.Max(1, material.shapeHeight);
            if (material.cells == null || material.cells.Count == 0)
            {
                material.cells = new List<Vector2Int> { Vector2Int.zero };
            }

            FitDimensionsToCells(ref material.shapeWidth, ref material.shapeHeight, material.cells);

            if (material.attributes == null)
            {
                material.attributes = new ForgingElementAttributes();
            }

            if (material.neighborRules == null)
            {
                material.neighborRules = new List<ForgingNeighborRule>();
            }

            if (material.neighborRule == null)
            {
                material.neighborRule = material.neighborRules.Count > 0
                    ? material.neighborRules[0]
                    : new ForgingNeighborRule();
            }
            else if (material.neighborRules.Count == 0 && material.neighborRule.ruleType != ForgingNeighborRuleType.None)
            {
                material.neighborRules.Add(material.neighborRule);
            }

            if (material.weaponBonuses == null)
            {
                material.weaponBonuses = new List<ForgingWeaponBonus>();
            }

            if (material.weaponBonus == null)
            {
                material.weaponBonus = material.weaponBonuses.Count > 0
                    ? material.weaponBonuses[0]
                    : new ForgingWeaponBonus();
            }
            else if (material.weaponBonuses.Count == 0 && material.weaponBonus.bonusType != ForgingWeaponBonusType.None)
            {
                material.weaponBonuses.Add(material.weaponBonus);
            }

            if (material.uiSlot == null)
            {
                material.uiSlot = new ForgingUiSlot();
            }
        }

        private static void FitDimensionsToCells(ref int width, ref int height, IReadOnlyList<Vector2Int> cells)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (cells == null)
            {
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                width = Mathf.Max(width, cell.x + 1);
                height = Mathf.Max(height, cell.y + 1);
            }
        }
    }

    internal sealed class ForgingCatalogDto
    {
        public string version;
        public string source;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ForgingWeaponBlueprintDto> weaponBlueprints;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<ForgingMaterialDto> materials;

        public static ForgingCatalogDto FromCatalog(
            ForgingCatalog catalog,
            bool includeWeaponBlueprints,
            bool includeMaterials)
        {
            ForgingCatalogDto dto = new ForgingCatalogDto
            {
                version = catalog != null ? catalog.version : string.Empty,
                source = catalog != null ? catalog.source : string.Empty,
            };

            if (catalog == null)
            {
                return dto;
            }

            if (includeWeaponBlueprints && catalog.weaponBlueprints != null)
            {
                dto.weaponBlueprints = new List<ForgingWeaponBlueprintDto>();
                for (int i = 0; i < catalog.weaponBlueprints.Count; i++)
                {
                    ForgingWeaponBlueprintDto blueprintDto = ForgingWeaponBlueprintDto.FromDefinition(catalog.weaponBlueprints[i]);
                    if (blueprintDto != null)
                    {
                        dto.weaponBlueprints.Add(blueprintDto);
                    }
                }
            }

            if (includeMaterials && catalog.materials != null)
            {
                dto.materials = new List<ForgingMaterialDto>();
                for (int i = 0; i < catalog.materials.Count; i++)
                {
                    ForgingMaterialDto materialDto = ForgingMaterialDto.FromDefinition(catalog.materials[i]);
                    if (materialDto != null)
                    {
                        dto.materials.Add(materialDto);
                    }
                }
            }

            return dto;
        }

        public ForgingCatalog ToCatalog()
        {
            ForgingCatalog catalog = new ForgingCatalog
            {
                version = version,
                source = source,
                weaponBlueprints = new List<ForgingWeaponBlueprintDefinition>(),
                materials = new List<ForgingMaterialDefinition>(),
            };

            if (weaponBlueprints != null)
            {
                for (int i = 0; i < weaponBlueprints.Count; i++)
                {
                    ForgingWeaponBlueprintDefinition blueprint = weaponBlueprints[i]?.ToDefinition();
                    if (blueprint != null)
                    {
                        catalog.weaponBlueprints.Add(blueprint);
                    }
                }
            }

            if (materials != null)
            {
                for (int i = 0; i < materials.Count; i++)
                {
                    ForgingMaterialDefinition material = materials[i]?.ToDefinition();
                    if (material != null)
                    {
                        catalog.materials.Add(material);
                    }
                }
            }

            return catalog;
        }
    }

    internal sealed class ForgingWeaponBlueprintDto
    {
        public string blueprintId;
        public string displayName;
        public string skillLogicId;
        public int width;
        public int height;
        public List<ForgingCellDto> cells;
        public List<ForgingSkillScalingDto> skillScalings;
        public ForgingWeaponRuntimeBinding runtime;
        public string skillDescription;

        public static ForgingWeaponBlueprintDto FromDefinition(ForgingWeaponBlueprintDefinition blueprint)
        {
            if (blueprint == null)
            {
                return null;
            }

            return new ForgingWeaponBlueprintDto
            {
                blueprintId = blueprint.blueprintId,
                displayName = blueprint.displayName,
                skillLogicId = blueprint.skillLogicId,
                width = blueprint.width,
                height = blueprint.height,
                cells = ForgingCatalogDtoUtility.FromCells(blueprint.cells),
                skillScalings = FromSkillScalings(blueprint.skillScalings),
                runtime = blueprint.runtime ?? new ForgingWeaponRuntimeBinding(),
                skillDescription = blueprint.skillDescription,
            };
        }

        public ForgingWeaponBlueprintDefinition ToDefinition()
        {
            return new ForgingWeaponBlueprintDefinition
            {
                blueprintId = blueprintId,
                displayName = displayName,
                skillLogicId = skillLogicId,
                width = width,
                height = height,
                cells = ForgingCatalogDtoUtility.ToCells(cells),
                skillScalings = ToSkillScalings(skillScalings),
                runtime = runtime ?? new ForgingWeaponRuntimeBinding(),
                skillDescription = skillDescription,
            };
        }

        private static List<ForgingSkillScalingDto> FromSkillScalings(List<ForgingSkillScaling> scalings)
        {
            List<ForgingSkillScalingDto> dtos = new List<ForgingSkillScalingDto>();
            if (scalings == null)
            {
                return dtos;
            }

            for (int i = 0; i < scalings.Count; i++)
            {
                ForgingSkillScalingDto dto = ForgingSkillScalingDto.FromScaling(scalings[i]);
                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }

            return dtos;
        }

        private static List<ForgingSkillScaling> ToSkillScalings(List<ForgingSkillScalingDto> dtos)
        {
            List<ForgingSkillScaling> scalings = new List<ForgingSkillScaling>();
            if (dtos == null)
            {
                return scalings;
            }

            for (int i = 0; i < dtos.Count; i++)
            {
                ForgingSkillScalingDto dto = dtos[i];
                if (dto != null)
                {
                    scalings.Add(dto.ToScaling());
                }
            }

            return scalings;
        }
    }

    internal sealed class ForgingMaterialDto
    {
        public string materialId;
        public string displayName;
        public int rarity;
        public int value;
        public int shapeWidth;
        public int shapeHeight;
        public List<ForgingCellDto> cells;
        public ForgingElementAttributes attributes;
        public List<ForgingNeighborRuleDto> neighborRules;
        public List<ForgingWeaponBonusDto> weaponBonuses;
        public string texturePath;
        public ForgingUiSlotDto uiSlot;
        public string description;

        public static ForgingMaterialDto FromDefinition(ForgingMaterialDefinition material)
        {
            if (material == null)
            {
                return null;
            }

            return new ForgingMaterialDto
            {
                materialId = material.materialId,
                displayName = material.displayName,
                rarity = material.rarity,
                value = material.value,
                shapeWidth = Mathf.Max(1, material.shapeWidth),
                shapeHeight = Mathf.Max(1, material.shapeHeight),
                cells = ForgingCatalogDtoUtility.FromCells(material.cells),
                attributes = material.attributes ?? new ForgingElementAttributes(),
                neighborRules = FromNeighborRules(material.neighborRules),
                weaponBonuses = FromWeaponBonuses(material.weaponBonuses),
                texturePath = material.texturePath,
                uiSlot = ForgingUiSlotDto.FromUiSlot(material.uiSlot),
                description = material.description,
            };
        }

        public ForgingMaterialDefinition ToDefinition()
        {
            List<ForgingNeighborRule> convertedRules = new List<ForgingNeighborRule>();
            if (neighborRules != null)
            {
                for (int i = 0; i < neighborRules.Count; i++)
                {
                    ForgingNeighborRule rule = neighborRules[i]?.ToRule();
                    if (rule != null)
                    {
                        convertedRules.Add(rule);
                    }
                }
            }

            List<ForgingWeaponBonus> convertedBonuses = new List<ForgingWeaponBonus>();
            if (weaponBonuses != null)
            {
                for (int i = 0; i < weaponBonuses.Count; i++)
                {
                    ForgingWeaponBonus bonus = weaponBonuses[i]?.ToBonus();
                    if (bonus != null)
                    {
                        convertedBonuses.Add(bonus);
                    }
                }
            }

            return new ForgingMaterialDefinition
            {
                materialId = materialId,
                displayName = displayName,
                rarity = rarity,
                value = value,
                shapeWidth = shapeWidth,
                shapeHeight = shapeHeight,
                cells = ForgingCatalogDtoUtility.ToCells(cells),
                attributes = attributes ?? new ForgingElementAttributes(),
                neighborRules = convertedRules,
                neighborRule = convertedRules.Count > 0 ? convertedRules[0] : new ForgingNeighborRule(),
                weaponBonuses = convertedBonuses,
                weaponBonus = convertedBonuses.Count > 0 ? convertedBonuses[0] : new ForgingWeaponBonus(),
                texturePath = texturePath,
                uiSlot = uiSlot != null ? uiSlot.ToUiSlot() : new ForgingUiSlot(),
                description = description,
            };
        }

        private static List<ForgingNeighborRuleDto> FromNeighborRules(List<ForgingNeighborRule> rules)
        {
            List<ForgingNeighborRuleDto> dtos = new List<ForgingNeighborRuleDto>();
            if (rules == null)
            {
                return dtos;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                ForgingNeighborRuleDto dto = ForgingNeighborRuleDto.FromRule(rules[i]);
                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }

            return dtos;
        }

        private static List<ForgingWeaponBonusDto> FromWeaponBonuses(List<ForgingWeaponBonus> bonuses)
        {
            List<ForgingWeaponBonusDto> dtos = new List<ForgingWeaponBonusDto>();
            if (bonuses == null)
            {
                return dtos;
            }

            for (int i = 0; i < bonuses.Count; i++)
            {
                ForgingWeaponBonusDto dto = ForgingWeaponBonusDto.FromBonus(bonuses[i]);
                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }

            return dtos;
        }
    }

    internal sealed class ForgingSkillScalingDto
    {
        public string valueType;
        public string element;
        public float coefficient;

        public static ForgingSkillScalingDto FromScaling(ForgingSkillScaling scaling)
        {
            if (scaling == null)
            {
                return null;
            }

            return new ForgingSkillScalingDto
            {
                valueType = scaling.valueType.ToString(),
                element = scaling.element.ToString(),
                coefficient = Mathf.Max(0f, scaling.coefficient),
            };
        }

        public ForgingSkillScaling ToScaling()
        {
            return new ForgingSkillScaling(
                ForgingCatalogDtoUtility.ParseEnum(valueType, ForgingSkillValueType.Damage),
                ForgingCatalogDtoUtility.ParseEnum(element, ForgingElement.Wood),
                coefficient);
        }
    }

    internal sealed class ForgingNeighborRuleDto
    {
        public string ruleType;
        public string targetElement;
        public float multiplier = 1f;
        public bool includeDiagonals;
        public string description;

        public static ForgingNeighborRuleDto FromRule(ForgingNeighborRule rule)
        {
            if (rule == null)
            {
                return null;
            }

            return new ForgingNeighborRuleDto
            {
                ruleType = rule.ruleType.ToString(),
                targetElement = rule.targetElement.ToString(),
                multiplier = Mathf.Max(0f, rule.multiplier),
                includeDiagonals = rule.includeDiagonals,
                description = rule.description,
            };
        }

        public ForgingNeighborRule ToRule()
        {
            return new ForgingNeighborRule
            {
                ruleType = ForgingCatalogDtoUtility.ParseEnum(ruleType, ForgingNeighborRuleType.None),
                targetElement = ForgingCatalogDtoUtility.ParseEnum(targetElement, ForgingElement.Wood),
                multiplier = Mathf.Max(0f, multiplier),
                includeDiagonals = includeDiagonals,
                description = description,
            };
        }
    }

    internal sealed class ForgingWeaponBonusDto
    {
        public string bonusType;
        public string element;
        public float minPercent;
        public float maxPercent;
        public string description;

        public static ForgingWeaponBonusDto FromBonus(ForgingWeaponBonus bonus)
        {
            if (bonus == null)
            {
                return null;
            }

            return new ForgingWeaponBonusDto
            {
                bonusType = bonus.bonusType.ToString(),
                element = bonus.element.ToString(),
                minPercent = Mathf.Max(0f, bonus.minPercent),
                maxPercent = Mathf.Max(0f, bonus.maxPercent),
                description = bonus.description,
            };
        }

        public ForgingWeaponBonus ToBonus()
        {
            return new ForgingWeaponBonus
            {
                bonusType = ForgingCatalogDtoUtility.ParseEnum(bonusType, ForgingWeaponBonusType.None),
                element = ForgingCatalogDtoUtility.ParseEnum(element, ForgingElement.Metal),
                minPercent = Mathf.Max(0f, minPercent),
                maxPercent = Mathf.Max(0f, maxPercent),
                description = description,
            };
        }
    }

    internal sealed class ForgingCellDto
    {
        public int x;
        public int y;
    }

    internal sealed class ForgingVector2Dto
    {
        public float x;
        public float y;

        public static ForgingVector2Dto FromVector2(Vector2 value)
        {
            return new ForgingVector2Dto
            {
                x = value.x,
                y = value.y,
            };
        }

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }
    }

    internal sealed class ForgingUiSlotDto
    {
        public ForgingVector2Dto anchoredPosition;
        public ForgingVector2Dto size;

        public static ForgingUiSlotDto FromUiSlot(ForgingUiSlot uiSlot)
        {
            if (uiSlot == null)
            {
                return null;
            }

            return new ForgingUiSlotDto
            {
                anchoredPosition = ForgingVector2Dto.FromVector2(uiSlot.anchoredPosition),
                size = ForgingVector2Dto.FromVector2(uiSlot.size),
            };
        }

        public ForgingUiSlot ToUiSlot()
        {
            return new ForgingUiSlot
            {
                anchoredPosition = anchoredPosition != null ? anchoredPosition.ToVector2() : Vector2.zero,
                size = size != null ? size.ToVector2() : new Vector2(220f, 190f),
            };
        }
    }

    internal static class ForgingCatalogDtoUtility
    {
        public static List<Vector2Int> ToCells(List<ForgingCellDto> dtos)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (dtos == null)
            {
                return cells;
            }

            for (int i = 0; i < dtos.Count; i++)
            {
                ForgingCellDto dto = dtos[i];
                if (dto != null)
                {
                    cells.Add(new Vector2Int(dto.x, dto.y));
                }
            }

            return cells;
        }

        public static List<ForgingCellDto> FromCells(List<Vector2Int> cells)
        {
            List<ForgingCellDto> dtos = new List<ForgingCellDto>();
            if (cells == null)
            {
                return dtos;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                dtos.Add(new ForgingCellDto
                {
                    x = cell.x,
                    y = cell.y,
                });
            }

            return dtos;
        }

        public static T ParseEnum<T>(string raw, T fallback) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return Enum.TryParse(raw, true, out T parsed) ? parsed : fallback;
        }
    }
}
