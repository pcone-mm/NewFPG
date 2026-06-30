using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Forging
{
    public static class ForgingCalculator
    {
        public static ForgingResult Evaluate(
            ForgingWeaponBlueprintDefinition blueprint,
            IReadOnlyList<ForgingPlacedMaterial> placements)
        {
            ForgingResult result = new ForgingResult
            {
                blueprint = blueprint,
                requiredCellCount = blueprint != null ? blueprint.CellCount : 0,
            };

            if (blueprint == null)
            {
                result.invalidReason = "No weapon blueprint selected.";
                return result;
            }

            HashSet<Vector2Int> blueprintCells = BuildCellSet(blueprint.cells);
            if (blueprintCells.Count == 0)
            {
                result.invalidReason = "Weapon blueprint has no cells.";
                return result;
            }

            Dictionary<Vector2Int, int> occupied = new Dictionary<Vector2Int, int>();
            if (placements != null)
            {
                for (int i = 0; i < placements.Count; i++)
                {
                    ForgingPlacedMaterial placement = placements[i];
                    if (placement == null || placement.material == null)
                    {
                        continue;
                    }

                    List<Vector2Int> cells = CellsForPlacement(placement);
                    for (int j = 0; j < cells.Count; j++)
                    {
                        Vector2Int cell = cells[j];
                        if (!blueprintCells.Contains(cell))
                        {
                            result.invalidReason = placement.material.displayName + " is outside the weapon blueprint.";
                            return result;
                        }

                        if (occupied.ContainsKey(cell))
                        {
                            result.invalidReason = placement.material.displayName + " overlaps another material.";
                            return result;
                        }

                        occupied[cell] = result.contributions.Count;
                    }

                    result.contributions.Add(new ForgingMaterialContribution
                    {
                        material = placement.material,
                        origin = placement.origin,
                        rotationSteps = ForgingShapeUtility.NormalizeRotationSteps(placement.rotationSteps),
                        baseAttributes = placement.material.attributes != null
                            ? placement.material.attributes.Clone()
                            : new ForgingElementAttributes(),
                        effectiveAttributes = placement.material.attributes != null
                            ? placement.material.attributes.Clone()
                            : new ForgingElementAttributes(),
                    });
                }
            }

            result.filledCellCount = occupied.Count;
            result.isComplete = occupied.Count == blueprintCells.Count;

            ApplyNeighborRules(result.contributions);

            for (int i = 0; i < result.contributions.Count; i++)
            {
                ForgingMaterialContribution contribution = result.contributions[i];
                result.finalAttributes.Add(contribution.effectiveAttributes);
            }

            for (int i = 0; i < result.contributions.Count; i++)
            {
                AddWeaponBonusResults(result.bonuses, result.contributions[i], result.finalAttributes);
            }

            ApplySkillScaling(blueprint, result);
            result.isValid = true;
            return result;
        }

        public static bool TryPlaceMaterial(
            ForgingWeaponBlueprintDefinition blueprint,
            IReadOnlyList<ForgingPlacedMaterial> existingPlacements,
            ForgingMaterialDefinition material,
            Vector2Int origin,
            out string reason)
        {
            return TryPlaceMaterial(blueprint, existingPlacements, material, origin, 0, out reason);
        }

        public static bool TryPlaceMaterial(
            ForgingWeaponBlueprintDefinition blueprint,
            IReadOnlyList<ForgingPlacedMaterial> existingPlacements,
            ForgingMaterialDefinition material,
            Vector2Int origin,
            int rotationSteps,
            out string reason)
        {
            reason = string.Empty;
            if (blueprint == null)
            {
                reason = "Select a weapon blueprint first.";
                return false;
            }

            if (material == null)
            {
                reason = "Select a material first.";
                return false;
            }

            List<ForgingPlacedMaterial> placements = new List<ForgingPlacedMaterial>();
            if (existingPlacements != null)
            {
                placements.AddRange(existingPlacements);
            }

            placements.Add(new ForgingPlacedMaterial(material, origin, rotationSteps));
            ForgingResult result = Evaluate(blueprint, placements);
            reason = result.invalidReason;
            return result.isValid;
        }

        public static List<Vector2Int> CellsForPlacement(ForgingPlacedMaterial placement)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (placement == null || placement.material == null)
            {
                return cells;
            }

            IReadOnlyList<Vector2Int> materialCells = placement.material.cells;
            if (materialCells == null || materialCells.Count == 0)
            {
                cells.Add(placement.origin);
                return cells;
            }

            List<Vector2Int> rotatedCells = ForgingShapeUtility.RotatedCells(placement.material, placement.rotationSteps);
            for (int i = 0; i < rotatedCells.Count; i++)
            {
                cells.Add(placement.origin + rotatedCells[i]);
            }

            return cells;
        }

        public static string FormatElementName(ForgingElement element)
        {
            switch (element)
            {
                case ForgingElement.Metal:
                    return "Metal";
                case ForgingElement.Water:
                    return "Water";
                case ForgingElement.Wood:
                    return "Wood";
                case ForgingElement.Fire:
                    return "Fire";
                case ForgingElement.Earth:
                    return "Earth";
                default:
                    return "None";
            }
        }

        private static void ApplyNeighborRules(IReadOnlyList<ForgingMaterialContribution> contributions)
        {
            if (contributions == null)
            {
                return;
            }

            for (int sourceIndex = 0; sourceIndex < contributions.Count; sourceIndex++)
            {
                ForgingMaterialContribution source = contributions[sourceIndex];
                if (source == null || source.material == null || source.material.neighborRule == null)
                {
                    continue;
                }

                List<ForgingNeighborRule> rules = GetNeighborRules(source.material);
                for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    ForgingNeighborRule rule = rules[ruleIndex];
                    if (rule == null || rule.ruleType != ForgingNeighborRuleType.MultiplyElement)
                    {
                        continue;
                    }

                    for (int targetIndex = 0; targetIndex < contributions.Count; targetIndex++)
                    {
                        if (targetIndex == sourceIndex)
                        {
                            continue;
                        }

                        ForgingMaterialContribution target = contributions[targetIndex];
                        if (!AreAdjacent(source, target, rule.includeDiagonals))
                        {
                            continue;
                        }

                        target.effectiveAttributes.Multiply(rule.targetElement, rule.multiplier);
                        string description = !string.IsNullOrWhiteSpace(rule.description)
                            ? rule.description
                            : source.material.displayName + " modifies adjacent " + FormatElementName(rule.targetElement);
                        target.appliedRuleDescriptions.Add(description);
                    }
                }
            }
        }

        private static bool AreAdjacent(
            ForgingMaterialContribution source,
            ForgingMaterialContribution target,
            bool includeDiagonals)
        {
            List<Vector2Int> sourceCells = CellsForPlacement(new ForgingPlacedMaterial(source.material, source.origin, source.rotationSteps));
            List<Vector2Int> targetCells = CellsForPlacement(new ForgingPlacedMaterial(target.material, target.origin, target.rotationSteps));
            for (int i = 0; i < sourceCells.Count; i++)
            {
                for (int j = 0; j < targetCells.Count; j++)
                {
                    Vector2Int delta = sourceCells[i] - targetCells[j];
                    int absX = Mathf.Abs(delta.x);
                    int absY = Mathf.Abs(delta.y);
                    if (absX + absY == 1)
                    {
                        return true;
                    }

                    if (includeDiagonals && absX == 1 && absY == 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddWeaponBonusResults(
            ICollection<ForgingWeaponBonusResult> bonusResults,
            ForgingMaterialContribution contribution,
            ForgingElementAttributes finalAttributes)
        {
            if (bonusResults == null || contribution == null || contribution.material == null)
            {
                return;
            }

            List<ForgingWeaponBonus> bonuses = GetWeaponBonuses(contribution.material);
            for (int i = 0; i < bonuses.Count; i++)
            {
                ForgingWeaponBonus bonus = bonuses[i];
                if (bonus == null || bonus.bonusType == ForgingWeaponBonusType.None)
                {
                    continue;
                }

                float sourceValue = finalAttributes != null ? finalAttributes.Get(bonus.element) : 0f;
                bonusResults.Add(new ForgingWeaponBonusResult
                {
                    sourceMaterialId = contribution.material.materialId,
                    sourceMaterialName = contribution.material.displayName,
                    bonusType = bonus.bonusType,
                    element = bonus.element,
                    minValue = sourceValue * Mathf.Max(0f, bonus.minPercent),
                    maxValue = sourceValue * Mathf.Max(0f, bonus.maxPercent),
                    description = bonus.description,
                });
            }
        }

        private static void ApplySkillScaling(ForgingWeaponBlueprintDefinition blueprint, ForgingResult result)
        {
            if (blueprint.skillScalings == null)
            {
                return;
            }

            for (int i = 0; i < blueprint.skillScalings.Count; i++)
            {
                ForgingSkillScaling scaling = blueprint.skillScalings[i];
                if (scaling == null)
                {
                    continue;
                }

                float value = result.finalAttributes.Get(scaling.element) * scaling.coefficient;
                switch (scaling.valueType)
                {
                    case ForgingSkillValueType.Damage:
                        result.damage += value;
                        break;
                    case ForgingSkillValueType.Shield:
                        result.shield += value;
                        break;
                }
            }
        }

        private static HashSet<Vector2Int> BuildCellSet(IReadOnlyList<Vector2Int> cells)
        {
            HashSet<Vector2Int> set = new HashSet<Vector2Int>();
            if (cells == null)
            {
                return set;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                set.Add(cells[i]);
            }

            return set;
        }

        private static List<ForgingNeighborRule> GetNeighborRules(ForgingMaterialDefinition material)
        {
            List<ForgingNeighborRule> rules = new List<ForgingNeighborRule>();
            if (material == null)
            {
                return rules;
            }

            if (material.neighborRules != null && material.neighborRules.Count > 0)
            {
                rules.AddRange(material.neighborRules);
            }
            else if (material.neighborRule != null && material.neighborRule.ruleType != ForgingNeighborRuleType.None)
            {
                rules.Add(material.neighborRule);
            }

            return rules;
        }

        private static List<ForgingWeaponBonus> GetWeaponBonuses(ForgingMaterialDefinition material)
        {
            List<ForgingWeaponBonus> bonuses = new List<ForgingWeaponBonus>();
            if (material == null)
            {
                return bonuses;
            }

            if (material.weaponBonuses != null && material.weaponBonuses.Count > 0)
            {
                bonuses.AddRange(material.weaponBonuses);
            }
            else if (material.weaponBonus != null && material.weaponBonus.bonusType != ForgingWeaponBonusType.None)
            {
                bonuses.Add(material.weaponBonus);
            }

            return bonuses;
        }
    }
}
