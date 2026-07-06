using System;
using System.Collections.Generic;
using NewFPG.Combat.SkillIndicators;
using NewFPG.Forging;
using Newtonsoft.Json;
using UnityEngine;

namespace NewFPG.Combat
{
    public enum WeaponModifierOperation
    {
        Add,
        Multiply,
        Override,
    }

    [Serializable]
    public sealed class WeaponModifier
    {
        public string modifierId;
        public WeaponModifierOperation operation = WeaponModifierOperation.Add;
        public bool modifyResourceCost;
        public float resourceCost;
        public bool modifyDamage;
        public float damage;
        public bool modifyCooldown;
        public float cooldown;
        public bool modifyRange;
        public float range;
        public bool modifyRadius;
        public float radius;
        public bool modifyWidth;
        public float width;
        public bool modifyLength;
        public float length;
        public bool modifyAngle;
        public float angle;
        public bool modifyShield;
        public float shield;
    }

    [Serializable]
    public sealed class WeaponInstanceData
    {
        public string instanceId;
        public string baseWeaponId;
        public string displayNameOverride;
        public ForgedWeaponRuntimeStats forgedStats;
        public List<WeaponModifier> permanentModifiers = new List<WeaponModifier>();

        public static WeaponInstanceData CreateForDefinition(WeaponDefinition definition)
        {
            return new WeaponInstanceData
            {
                instanceId = Guid.NewGuid().ToString("N"),
                baseWeaponId = definition != null ? definition.WeaponId : string.Empty,
            };
        }

        public static WeaponInstanceData CreateForged(
            WeaponDefinition definition,
            ForgedWeaponRuntimeStats stats,
            string displayName = null)
        {
            return new WeaponInstanceData
            {
                instanceId = Guid.NewGuid().ToString("N"),
                baseWeaponId = definition != null ? definition.WeaponId : string.Empty,
                displayNameOverride = displayName,
                forgedStats = stats,
            };
        }
    }

    [Serializable]
    public sealed class WeaponRuntimeStats
    {
        public string weaponId;
        public string instanceId;
        public string displayName;
        public float resourceCost;
        public float damage;
        public float bonusDamage;
        public float cooldown;
        public float range;
        public float radius;
        public SkillIndicatorShapeType shapeType;
        public float width;
        public float length;
        public float angle;
        public float height;
        public float groundOffset;
        public SkillIndicatorInputMode inputMode;
        public SkillIndicatorDefaultReleasePolicy tapPolicy;
        public SkillIndicatorDefaultReleasePolicy holdPolicy;
        public SkillIndicatorInvalidReleasePolicy invalidReleasePolicy;
        public SkillIndicatorAimSource aimSource;
        public bool requireSurfaceHit;
        public bool clampToRange;
        public SkillIndicatorPlacementMode placementMode;
        public LayerMask surfaceMask;
        public LayerMask collisionMask;
        public float tapMaxDuration;
        public float holdEnterDelay;
        public float castDelay;
        public float warningTime;
        public float duration;
        public float fadeOut;
        public string previewPrefabResourceId;
        public string validMaterialResourceId;
        public string invalidMaterialResourceId;
        public string confirmAudioResourceId;
        public string invalidAudioResourceId;
        public bool debugDraw;
        public float shield;
        public ForgingElementAttributes attributes;
        public List<ForgingWeaponBonusResult> bonuses = new List<ForgingWeaponBonusResult>();

        [JsonIgnore] public WeaponDefinition Definition;
        [JsonIgnore] public Sprite Icon;
        [JsonIgnore] public GameObject ReleaseEffectPrefab;
        [JsonIgnore] public GameObject HitEffectPrefab;

        public string WeaponId => weaponId;
        public string InstanceId => instanceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? weaponId : displayName;
        public float ResourceCost => resourceCost;
        public float Damage => damage;
        public float BonusDamage => bonusDamage;
        public float RuntimeTotalDamage => damage + bonusDamage;
        public float Cooldown => cooldown;
        public float Range => range;
        public float Radius => radius;
        public SkillIndicatorShapeType ShapeType => shapeType;
        public float Width => width;
        public float Length => length;
        public float Angle => angle;
        public float Height => height;
        public float GroundOffset => groundOffset;
        public SkillIndicatorInputMode InputMode => inputMode;
        public SkillIndicatorDefaultReleasePolicy TapPolicy => tapPolicy;
        public SkillIndicatorDefaultReleasePolicy HoldPolicy => holdPolicy;
        public SkillIndicatorInvalidReleasePolicy InvalidReleasePolicy => invalidReleasePolicy;
        public SkillIndicatorAimSource AimSource => aimSource;
        public bool RequireSurfaceHit => requireSurfaceHit;
        public bool ClampToRange => clampToRange;
        public SkillIndicatorPlacementMode PlacementMode => placementMode;
        public bool StickToGround => placementMode == SkillIndicatorPlacementMode.GroundSurface;
        public LayerMask SurfaceMask => surfaceMask;
        public LayerMask CollisionMask => collisionMask;
        public float TapMaxDuration => tapMaxDuration;
        public float HoldEnterDelay => holdEnterDelay;
        public float CastDelay => castDelay;
        public float WarningTime => warningTime;
        public float Duration => duration;
        public float FadeOut => fadeOut;
        public string PreviewPrefabResourceId => previewPrefabResourceId;
        public string ValidMaterialResourceId => validMaterialResourceId;
        public string InvalidMaterialResourceId => invalidMaterialResourceId;
        public string ConfirmAudioResourceId => confirmAudioResourceId;
        public string InvalidAudioResourceId => invalidAudioResourceId;
        public bool DebugDraw => debugDraw;
        public float Shield => shield;
        public ForgingElementAttributes Attributes => attributes;
        public IReadOnlyList<ForgingWeaponBonusResult> Bonuses => bonuses;
    }

    public static class WeaponRuntimeResolver
    {
        public static WeaponRuntimeStats Resolve(
            WeaponDefinition definition,
            WeaponInstanceData instance,
            IEnumerable<WeaponModifier> activeModifiers)
        {
            if (definition == null)
            {
                return null;
            }

            WeaponRuntimeStats stats = CreateBaseStats(definition, instance);
            ApplyForgedStats(stats, ResolveForgedStats(definition, instance));
            ApplyModifiers(stats, instance != null ? instance.permanentModifiers : null, activeModifiers);
            Normalize(stats);
            return stats;
        }

        public static WeaponRuntimeStats Resolve(WeaponDefinition definition)
        {
            return Resolve(definition, null, null);
        }

        private static WeaponRuntimeStats CreateBaseStats(
            WeaponDefinition definition,
            WeaponInstanceData instance)
        {
            string displayName = definition.DisplayName;
            if (instance != null && !string.IsNullOrWhiteSpace(instance.displayNameOverride))
            {
                displayName = instance.displayNameOverride;
            }

            return new WeaponRuntimeStats
            {
                weaponId = definition.WeaponId,
                instanceId = instance != null ? instance.instanceId : string.Empty,
                displayName = displayName,
                resourceCost = definition.ResourceCost,
                damage = definition.Damage,
                bonusDamage = 0f,
                cooldown = definition.Cooldown,
                range = definition.Range,
                radius = definition.Radius,
                shapeType = definition.ShapeType,
                width = definition.Width,
                length = definition.Length,
                angle = definition.Angle,
                height = definition.Height,
                groundOffset = definition.GroundOffset,
                inputMode = definition.InputMode,
                tapPolicy = definition.TapPolicy,
                holdPolicy = definition.HoldPolicy,
                invalidReleasePolicy = definition.InvalidReleasePolicy,
                aimSource = definition.AimSource,
                requireSurfaceHit = definition.RequireSurfaceHit,
                clampToRange = definition.ClampToRange,
                placementMode = definition.PlacementMode,
                surfaceMask = definition.SurfaceMask,
                collisionMask = definition.CollisionMask,
                tapMaxDuration = definition.TapMaxDuration,
                holdEnterDelay = definition.HoldEnterDelay,
                castDelay = definition.CastDelay,
                warningTime = definition.WarningTime,
                duration = definition.Duration,
                fadeOut = definition.FadeOut,
                previewPrefabResourceId = definition.PreviewPrefabResourceId,
                validMaterialResourceId = definition.ValidMaterialResourceId,
                invalidMaterialResourceId = definition.InvalidMaterialResourceId,
                confirmAudioResourceId = definition.ConfirmAudioResourceId,
                invalidAudioResourceId = definition.InvalidAudioResourceId,
                debugDraw = definition.DebugDraw,
                shield = 0f,
                attributes = null,
                bonuses = new List<ForgingWeaponBonusResult>(),
                Definition = definition,
                Icon = definition.Icon,
                ReleaseEffectPrefab = definition.ReleaseEffectPrefab,
                HitEffectPrefab = definition.HitEffectPrefab,
            };
        }

        private static ForgedWeaponRuntimeStats ResolveForgedStats(
            WeaponDefinition definition,
            WeaponInstanceData instance)
        {
            if (instance != null)
            {
                return instance.forgedStats;
            }

            return definition != null ? definition.ForgedRuntimeStats : null;
        }

        private static void ApplyForgedStats(WeaponRuntimeStats stats, ForgedWeaponRuntimeStats forgedStats)
        {
            if (stats == null || forgedStats == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(forgedStats.displayName))
            {
                stats.displayName = forgedStats.displayName;
            }

            if (forgedStats.HasDamageOverride)
            {
                stats.damage = forgedStats.damage;
            }

            stats.bonusDamage = forgedStats.BonusDamageAverage;
            stats.shield = Mathf.Max(0f, forgedStats.shield);
            stats.attributes = forgedStats.attributes != null
                ? forgedStats.attributes.Clone()
                : null;
            stats.bonuses = forgedStats.bonuses != null
                ? new List<ForgingWeaponBonusResult>(forgedStats.bonuses)
                : new List<ForgingWeaponBonusResult>();
        }

        private static void ApplyModifiers(
            WeaponRuntimeStats stats,
            IEnumerable<WeaponModifier> permanentModifiers,
            IEnumerable<WeaponModifier> activeModifiers)
        {
            if (stats == null)
            {
                return;
            }

            ApplyModifiers(stats, permanentModifiers, WeaponModifierOperation.Add);
            ApplyModifiers(stats, permanentModifiers, WeaponModifierOperation.Multiply);
            ApplyModifiers(stats, activeModifiers, WeaponModifierOperation.Add);
            ApplyModifiers(stats, activeModifiers, WeaponModifierOperation.Multiply);
            ApplyModifiers(stats, permanentModifiers, WeaponModifierOperation.Override);
            ApplyModifiers(stats, activeModifiers, WeaponModifierOperation.Override);
        }

        private static void ApplyModifiers(
            WeaponRuntimeStats stats,
            IEnumerable<WeaponModifier> modifiers,
            WeaponModifierOperation operation)
        {
            if (modifiers == null)
            {
                return;
            }

            foreach (WeaponModifier modifier in modifiers)
            {
                if (modifier == null || modifier.operation != operation)
                {
                    continue;
                }

                if (modifier.modifyResourceCost)
                {
                    stats.resourceCost = Apply(stats.resourceCost, modifier.resourceCost, operation);
                }

                if (modifier.modifyDamage)
                {
                    stats.damage = Apply(stats.damage, modifier.damage, operation);
                }

                if (modifier.modifyCooldown)
                {
                    stats.cooldown = Apply(stats.cooldown, modifier.cooldown, operation);
                }

                if (modifier.modifyRange)
                {
                    stats.range = Apply(stats.range, modifier.range, operation);
                }

                if (modifier.modifyRadius)
                {
                    stats.radius = Apply(stats.radius, modifier.radius, operation);
                }

                if (modifier.modifyWidth)
                {
                    stats.width = Apply(stats.width, modifier.width, operation);
                }

                if (modifier.modifyLength)
                {
                    stats.length = Apply(stats.length, modifier.length, operation);
                }

                if (modifier.modifyAngle)
                {
                    stats.angle = Apply(stats.angle, modifier.angle, operation);
                }

                if (modifier.modifyShield)
                {
                    stats.shield = Apply(stats.shield, modifier.shield, operation);
                }
            }
        }

        private static float Apply(float current, float value, WeaponModifierOperation operation)
        {
            switch (operation)
            {
                case WeaponModifierOperation.Multiply:
                    return current * value;
                case WeaponModifierOperation.Override:
                    return value;
                default:
                    return current + value;
            }
        }

        private static void Normalize(WeaponRuntimeStats stats)
        {
            stats.resourceCost = Mathf.Max(0f, stats.resourceCost);
            stats.damage = Mathf.Max(0f, stats.damage);
            stats.bonusDamage = Mathf.Max(0f, stats.bonusDamage);
            stats.cooldown = Mathf.Max(0.05f, stats.cooldown);
            stats.range = Mathf.Max(0.1f, stats.range);
            stats.radius = Mathf.Max(0.05f, stats.radius);
            stats.width = stats.width > 0f ? Mathf.Max(0.05f, stats.width) : stats.radius * 2f;
            stats.length = stats.length > 0f ? Mathf.Max(0.1f, stats.length) : stats.range;
            stats.angle = Mathf.Clamp(stats.angle, 1f, 360f);
            stats.height = Mathf.Max(0f, stats.height);
            stats.groundOffset = Mathf.Max(0f, stats.groundOffset);
            stats.tapMaxDuration = Mathf.Max(0f, stats.tapMaxDuration);
            stats.holdEnterDelay = Mathf.Max(0f, stats.holdEnterDelay);
            stats.castDelay = Mathf.Max(0f, stats.castDelay);
            stats.warningTime = Mathf.Max(0f, stats.warningTime);
            stats.duration = Mathf.Max(0f, stats.duration);
            stats.fadeOut = Mathf.Max(0f, stats.fadeOut);
            stats.shield = Mathf.Max(0f, stats.shield);

            if (string.IsNullOrWhiteSpace(stats.validMaterialResourceId))
            {
                stats.validMaterialResourceId = "M_IND_OwnerValid";
            }

            if (string.IsNullOrWhiteSpace(stats.invalidMaterialResourceId))
            {
                stats.invalidMaterialResourceId = "M_IND_Invalid";
            }

            if (string.IsNullOrWhiteSpace(stats.confirmAudioResourceId))
            {
                stats.confirmAudioResourceId = "S_IND_ConfirmRelease";
            }

            if (string.IsNullOrWhiteSpace(stats.invalidAudioResourceId))
            {
                stats.invalidAudioResourceId = "S_IND_Invalid";
            }
        }
    }

    [Serializable]
    public sealed class WeaponInventorySaveData
    {
        public List<WeaponInstanceData> weapons = new List<WeaponInstanceData>();

        public static WeaponInventorySaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new WeaponInventorySaveData();
            }

            WeaponInventorySaveData data = JsonConvert.DeserializeObject<WeaponInventorySaveData>(json);
            if (data == null)
            {
                return new WeaponInventorySaveData();
            }

            if (data.weapons == null)
            {
                data.weapons = new List<WeaponInstanceData>();
            }

            return data;
        }

        public string ToJson()
        {
            if (weapons == null)
            {
                weapons = new List<WeaponInstanceData>();
            }

            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public List<WeaponInstanceData> ResolveValidInstances(IReadOnlyList<WeaponDefinition> definitions)
        {
            List<WeaponInstanceData> resolved = new List<WeaponInstanceData>();
            if (weapons == null)
            {
                return resolved;
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInstanceData instance = weapons[i];
                if (instance == null)
                {
                    continue;
                }

                if (TryFindDefinition(definitions, instance.baseWeaponId, out _))
                {
                    resolved.Add(instance);
                    continue;
                }

                Debug.LogWarning("Skipped weapon instance with missing base weapon id: " + instance.baseWeaponId);
            }

            return resolved;
        }

        public static bool TryFindDefinition(
            IReadOnlyList<WeaponDefinition> definitions,
            string weaponId,
            out WeaponDefinition definition)
        {
            definition = null;
            if (definitions == null || string.IsNullOrWhiteSpace(weaponId))
            {
                return false;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                WeaponDefinition candidate = definitions[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.WeaponId == weaponId || candidate.name == weaponId)
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
