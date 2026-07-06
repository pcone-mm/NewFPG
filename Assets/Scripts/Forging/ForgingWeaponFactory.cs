using NewFPG.Combat;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Forging
{
    public static class ForgingWeaponFactory
    {
        public static WeaponInstanceData CreateWeaponInstance(
            WeaponDefinition baseDefinition,
            ForgingWeaponBlueprintDefinition blueprint,
            ForgingResult result)
        {
            if (baseDefinition == null || blueprint == null)
            {
                return null;
            }

            ForgedWeaponRuntimeStats stats = CreateRuntimeStats(blueprint, result);
            return WeaponInstanceData.CreateForged(baseDefinition, stats, blueprint.displayName);
        }

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
            runtime.Normalize();
            ForgedWeaponRuntimeStats stats = CreateRuntimeStats(blueprint, result);

            weapon.ApplyForgingRuntime(
                blueprint.displayName,
                LoadAsset<Sprite>(runtime.hudIconPath),
                runtime,
                LoadAsset<GameObject>(runtime.releaseEffectPrefabPath),
                LoadAsset<GameObject>(runtime.hitEffectPrefabPath),
                stats);
        }

        private static ForgedWeaponRuntimeStats CreateRuntimeStats(
            ForgingWeaponBlueprintDefinition blueprint,
            ForgingResult result)
        {
            if (result != null && result.isValid)
            {
                return result.ToRuntimeStats();
            }

            return new ForgedWeaponRuntimeStats
            {
                blueprintId = blueprint != null ? blueprint.blueprintId : string.Empty,
                displayName = blueprint != null ? blueprint.displayName : string.Empty,
                skillLogicId = blueprint != null ? blueprint.skillLogicId : string.Empty,
            };
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
