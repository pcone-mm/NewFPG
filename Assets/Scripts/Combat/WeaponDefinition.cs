using System;
using UnityEngine;
using NewFPG.Combat.SkillIndicators;
using NewFPG.Forging;

namespace NewFPG.Combat
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "NewFPG/Combat/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string weaponId;
        [SerializeField] private string displayName = "Flying Sword";
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0f)] private float resourceCost = 3f;
        [SerializeField, Min(0f)] private float damage = 35f;
        [SerializeField, Min(0.05f)] private float cooldown = 0.4f;
        [SerializeField, Min(0.1f)] private float range = 8f;
        [SerializeField, Min(0.05f)] private float radius = 0.55f;

        [Header("Cast Shape")]
        [SerializeField] private SkillIndicatorShapeType shapeType = SkillIndicatorShapeType.GroundCircle;
        [SerializeField, Min(0.05f)] private float width = 1.1f;
        [SerializeField, Min(0.1f)] private float length = 8f;
        [SerializeField, Range(1f, 360f)] private float angle = 90f;
        [SerializeField, Min(0f)] private float height = 2f;
        [SerializeField, Min(0f)] private float groundOffset = 0.06f;

        [Header("Input And Aim")]
        [SerializeField] private SkillIndicatorInputMode inputMode = SkillIndicatorInputMode.HoldPreview;
        [SerializeField] private SkillIndicatorDefaultReleasePolicy tapPolicy = SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget;
        [SerializeField] private SkillIndicatorDefaultReleasePolicy holdPolicy = SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit;
        [SerializeField] private SkillIndicatorInvalidReleasePolicy invalidReleasePolicy = SkillIndicatorInvalidReleasePolicy.Cancel;
        [SerializeField] private SkillIndicatorAimSource aimSource = SkillIndicatorAimSource.CrosshairRay;
        [SerializeField] private bool requireSurfaceHit;
        [SerializeField] private bool clampToRange = true;
        [SerializeField] private SkillIndicatorPlacementMode placementMode = SkillIndicatorPlacementMode.GroundSurface;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Cast Timing")]
        [SerializeField, Min(0f)] private float tapMaxDuration = 0.16f;
        [SerializeField, Min(0f)] private float holdEnterDelay = 0.1f;
        [SerializeField, Min(0f)] private float castDelay;
        [SerializeField, Min(0f)] private float warningTime;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField, Min(0f)] private float fadeOut = 0.15f;

        [Header("Preview Resources")]
        [SerializeField] private string previewPrefabResourceId;
        [SerializeField] private string validMaterialResourceId = "M_IND_OwnerValid";
        [SerializeField] private string invalidMaterialResourceId = "M_IND_Invalid";
        [SerializeField] private string confirmAudioResourceId = "S_IND_ConfirmRelease";
        [SerializeField] private string invalidAudioResourceId = "S_IND_Invalid";
        [SerializeField] private bool debugDraw;

#pragma warning disable CS0618
        [SerializeField, HideInInspector] private SkillIndicatorConfig indicatorConfig;
#pragma warning restore CS0618
        [SerializeField] private GameObject releaseEffectPrefab;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private ForgedWeaponRuntimeStats forgedRuntimeStats;

        public string WeaponId => string.IsNullOrWhiteSpace(weaponId) ? name : weaponId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public float ResourceCost => resourceCost;
        public float Damage => damage;
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
#pragma warning disable CS0618
        [Obsolete("SkillIndicatorConfig is a legacy migration source. Runtime cast geometry now lives on WeaponDefinition.")]
        public SkillIndicatorConfig IndicatorConfig => indicatorConfig;
#pragma warning restore CS0618
        public GameObject ReleaseEffectPrefab => releaseEffectPrefab;
        public GameObject HitEffectPrefab => hitEffectPrefab;
        public ForgedWeaponRuntimeStats ForgedRuntimeStats => forgedRuntimeStats;
        public float RuntimeDamage => forgedRuntimeStats != null && forgedRuntimeStats.HasDamageOverride ? forgedRuntimeStats.damage : damage;
        public float RuntimeBonusDamage => forgedRuntimeStats != null ? forgedRuntimeStats.BonusDamageAverage : 0f;
        public float RuntimeTotalDamage => RuntimeDamage + RuntimeBonusDamage;
        public float RuntimeShield => forgedRuntimeStats != null ? forgedRuntimeStats.shield : 0f;
        public ForgingElementAttributes RuntimeAttributes => forgedRuntimeStats != null ? forgedRuntimeStats.attributes : null;

        public void SetForgedRuntimeStats(ForgedWeaponRuntimeStats stats)
        {
            forgedRuntimeStats = stats;
        }

        public void ApplyForgingRuntime(
            string nextDisplayName,
            Sprite nextIcon,
            ForgingWeaponRuntimeBinding runtime,
            GameObject nextReleaseEffectPrefab,
            GameObject nextHitEffectPrefab,
            ForgedWeaponRuntimeStats nextForgedStats)
        {
            if (runtime == null)
            {
                runtime = new ForgingWeaponRuntimeBinding();
            }

            if (!string.IsNullOrWhiteSpace(nextDisplayName))
            {
                displayName = nextDisplayName;
            }

            if (nextIcon != null)
            {
                icon = nextIcon;
            }

            resourceCost = Mathf.Max(0f, runtime.resourceCost);
            damage = Mathf.Max(0f, runtime.baseDamage);
            cooldown = Mathf.Max(0.05f, runtime.cooldown);
            range = Mathf.Max(0.1f, runtime.range);
            radius = Mathf.Max(0.05f, runtime.radius);
            shapeType = runtime.shapeType;
            width = runtime.width;
            length = runtime.length;
            angle = runtime.angle;
            height = runtime.height;
            groundOffset = runtime.groundOffset;
            inputMode = runtime.inputMode;
            tapPolicy = runtime.tapPolicy;
            holdPolicy = runtime.holdPolicy;
            invalidReleasePolicy = runtime.invalidReleasePolicy;
            aimSource = runtime.aimSource;
            requireSurfaceHit = runtime.requireSurfaceHit;
            clampToRange = runtime.clampToRange;
            placementMode = runtime.placementMode;
            surfaceMask = new LayerMask { value = runtime.surfaceMask };
            collisionMask = new LayerMask { value = runtime.collisionMask };
            tapMaxDuration = runtime.tapMaxDuration;
            holdEnterDelay = runtime.holdEnterDelay;
            castDelay = runtime.castDelay;
            warningTime = runtime.warningTime;
            duration = runtime.duration;
            fadeOut = runtime.fadeOut;
            previewPrefabResourceId = runtime.previewPrefabResourceId;
            validMaterialResourceId = runtime.validMaterialResourceId;
            invalidMaterialResourceId = runtime.invalidMaterialResourceId;
            confirmAudioResourceId = runtime.confirmAudioResourceId;
            invalidAudioResourceId = runtime.invalidAudioResourceId;
            debugDraw = runtime.debugDraw;
            releaseEffectPrefab = nextReleaseEffectPrefab;
            hitEffectPrefab = nextHitEffectPrefab;
            forgedRuntimeStats = nextForgedStats;
            NormalizeAuthoringValues();
        }

#pragma warning disable CS0618
        [Obsolete("Use ApplyForgingRuntime(string, Sprite, ForgingWeaponRuntimeBinding, GameObject, GameObject, ForgedWeaponRuntimeStats).")]
        public void ApplyForgingRuntime(
            string nextDisplayName,
            Sprite nextIcon,
            float nextResourceCost,
            float nextBaseDamage,
            float nextCooldown,
            float nextRange,
            float nextRadius,
            SkillIndicatorConfig nextIndicatorConfig,
            GameObject nextReleaseEffectPrefab,
            GameObject nextHitEffectPrefab,
            ForgedWeaponRuntimeStats nextForgedStats)
        {
            ForgingWeaponRuntimeBinding runtime = new ForgingWeaponRuntimeBinding
            {
                resourceCost = nextResourceCost,
                baseDamage = nextBaseDamage,
                cooldown = nextCooldown,
                range = nextRange,
                radius = nextRadius,
            };

            if (nextIndicatorConfig != null)
            {
                runtime.shapeType = nextIndicatorConfig.ShapeType;
                runtime.width = nextIndicatorConfig.Width > 0f ? nextIndicatorConfig.Width : nextRadius * 2f;
                runtime.length = nextIndicatorConfig.Length > 0f ? nextIndicatorConfig.Length : nextRange;
                runtime.angle = nextIndicatorConfig.Angle;
                runtime.height = nextIndicatorConfig.Height;
                runtime.groundOffset = nextIndicatorConfig.GroundOffset;
                runtime.inputMode = nextIndicatorConfig.InputMode;
                runtime.tapPolicy = nextIndicatorConfig.TapPolicy;
                runtime.holdPolicy = nextIndicatorConfig.HoldPolicy;
                runtime.invalidReleasePolicy = nextIndicatorConfig.InvalidReleasePolicy;
                runtime.aimSource = nextIndicatorConfig.AimSource;
                runtime.requireSurfaceHit = nextIndicatorConfig.RequireSurfaceHit;
                runtime.clampToRange = nextIndicatorConfig.ClampToRange;
                runtime.placementMode = nextIndicatorConfig.PlacementMode;
                runtime.surfaceMask = nextIndicatorConfig.SurfaceMask.value;
                runtime.collisionMask = nextIndicatorConfig.CollisionMask.value;
                runtime.tapMaxDuration = nextIndicatorConfig.TapMaxDuration;
                runtime.holdEnterDelay = nextIndicatorConfig.HoldEnterDelay;
                runtime.castDelay = nextIndicatorConfig.CastDelay;
                runtime.warningTime = nextIndicatorConfig.WarningTime;
                runtime.duration = nextIndicatorConfig.Duration;
                runtime.fadeOut = nextIndicatorConfig.FadeOut;
                runtime.previewPrefabResourceId = nextIndicatorConfig.PreviewPrefabResourceId;
                runtime.validMaterialResourceId = nextIndicatorConfig.ValidMaterialResourceId;
                runtime.invalidMaterialResourceId = nextIndicatorConfig.InvalidMaterialResourceId;
                runtime.confirmAudioResourceId = nextIndicatorConfig.ConfirmAudioResourceId;
                runtime.invalidAudioResourceId = nextIndicatorConfig.InvalidAudioResourceId;
                runtime.debugDraw = nextIndicatorConfig.DebugDraw;
            }

            ApplyForgingRuntime(
                nextDisplayName,
                nextIcon,
                runtime,
                nextReleaseEffectPrefab,
                nextHitEffectPrefab,
                nextForgedStats);
            indicatorConfig = nextIndicatorConfig;
        }
#pragma warning restore CS0618

        private void OnValidate()
        {
            NormalizeAuthoringValues();
        }

        private void NormalizeAuthoringValues()
        {
            resourceCost = Mathf.Max(0f, resourceCost);
            damage = Mathf.Max(0f, damage);
            cooldown = Mathf.Max(0.05f, cooldown);
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.05f, radius);
            width = width > 0f ? Mathf.Max(0.05f, width) : radius * 2f;
            length = length > 0f ? Mathf.Max(0.1f, length) : range;
            angle = Mathf.Clamp(angle, 1f, 360f);
            height = Mathf.Max(0f, height);
            groundOffset = Mathf.Max(0f, groundOffset);
            tapMaxDuration = Mathf.Max(0f, tapMaxDuration);
            holdEnterDelay = Mathf.Max(0f, holdEnterDelay);
            castDelay = Mathf.Max(0f, castDelay);
            warningTime = Mathf.Max(0f, warningTime);
            duration = Mathf.Max(0f, duration);
            fadeOut = Mathf.Max(0f, fadeOut);

            if (string.IsNullOrWhiteSpace(validMaterialResourceId))
            {
                validMaterialResourceId = "M_IND_OwnerValid";
            }

            if (string.IsNullOrWhiteSpace(invalidMaterialResourceId))
            {
                invalidMaterialResourceId = "M_IND_Invalid";
            }

            if (string.IsNullOrWhiteSpace(confirmAudioResourceId))
            {
                confirmAudioResourceId = "S_IND_ConfirmRelease";
            }

            if (string.IsNullOrWhiteSpace(invalidAudioResourceId))
            {
                invalidAudioResourceId = "S_IND_Invalid";
            }
        }
    }
}
