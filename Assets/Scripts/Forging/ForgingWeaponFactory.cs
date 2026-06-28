using NewFPG.Combat;
using NewFPG.Combat.SkillIndicators;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Forging
{
    public static class ForgingWeaponFactory
    {
        public static WeaponDefinition CreateRuntimeWeapon(ForgingWeaponBlueprintDefinition blueprint, ForgingResult result)
        {
            if (blueprint == null)
            {
                return null;
            }

            WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = string.IsNullOrWhiteSpace(blueprint.blueprintId)
                ? "ForgedWeapon"
                : "Forged_" + blueprint.blueprintId;
            ApplyToWeaponDefinition(weapon, blueprint, result);
            return weapon;
        }

        public static void ApplyToWeaponDefinition(
            WeaponDefinition weapon,
            ForgingWeaponBlueprintDefinition blueprint,
            ForgingResult result)
        {
            if (weapon == null || blueprint == null)
            {
                return;
            }

            ForgingWeaponRuntimeBinding runtime = blueprint.runtime ?? new ForgingWeaponRuntimeBinding();
            ForgedWeaponRuntimeStats stats = result != null && result.isValid
                ? result.ToRuntimeStats()
                : new ForgedWeaponRuntimeStats
                {
                    blueprintId = blueprint.blueprintId,
                    displayName = blueprint.displayName,
                    skillLogicId = blueprint.skillLogicId,
                };

            weapon.ApplyForgingRuntime(
                blueprint.displayName,
                LoadAsset<Sprite>(runtime.hudIconPath),
                runtime.resourceCost,
                runtime.baseDamage,
                runtime.cooldown,
                runtime.range,
                runtime.radius,
                LoadAsset<SkillIndicatorConfig>(runtime.indicatorConfigPath),
                LoadAsset<GameObject>(runtime.releaseEffectPrefabPath),
                LoadAsset<GameObject>(runtime.hitEffectPrefabPath),
                stats);
        }

        private static T LoadAsset<T>(string projectPath) where T : Object
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return null;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(projectPath);
#else
            return null;
#endif
        }
    }
}
