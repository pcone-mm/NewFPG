using System;
using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    public enum SkillIndicatorOwnerType
    {
        Player,
        Monster,
        Neutral,
    }

    public enum SkillIndicatorInputMode
    {
        TapOnly,
        HoldPreview,
        AutoTelegraph,
    }

    public enum SkillIndicatorShapeType
    {
        None,
        TargetReticle,
        GroundCircle,
        Line,
        Rectangle,
        Cone,
        ArcTrajectory,
        Footprint,
        Tether,
        CountdownDanger,
        PersistentZone,
    }

    public enum SkillIndicatorAimSource
    {
        CrosshairRay,
        ScreenCursorRay,
        OwnerForward,
        Self,
    }

    public enum SkillIndicatorDefaultReleasePolicy
    {
        CastForwardMaxRange,
        CastAtCrosshairHit,
        CastAtGroundUnderCrosshair,
        AutoSelectBestTarget,
        CastOnSelf,
        CastAtCurrentLock,
    }

    public enum SkillIndicatorInvalidReleasePolicy
    {
        Cancel,
        FallbackToDefault,
    }

    public enum SkillIndicatorPlacementMode
    {
        GroundSurface,
        WorldSpace,
        AttachToCastOrigin,
    }

    public enum SkillIndicatorValidationReason
    {
        None,
        Disabled,
        Cooldown,
        Resource,
        NoSurface,
        OutOfRange,
        Blocked,
        InvalidConfig,
    }

    [CreateAssetMenu(fileName = "SkillIndicatorConfig", menuName = "NewFPG/Combat/Skill Indicators/Skill Indicator Config")]
    public sealed class SkillIndicatorConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string abilityId;
        [SerializeField] private string indicatorId;
        [SerializeField] private SkillIndicatorOwnerType ownerType = SkillIndicatorOwnerType.Player;
        [SerializeField] private SkillIndicatorInputMode inputMode = SkillIndicatorInputMode.HoldPreview;

        [Header("Release")]
        [SerializeField] private SkillIndicatorDefaultReleasePolicy tapPolicy = SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget;
        [SerializeField] private SkillIndicatorDefaultReleasePolicy holdPolicy = SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit;
        [SerializeField] private SkillIndicatorInvalidReleasePolicy invalidReleasePolicy = SkillIndicatorInvalidReleasePolicy.Cancel;

        [Header("Aim")]
        [SerializeField] private SkillIndicatorAimSource aimSource = SkillIndicatorAimSource.CrosshairRay;
        [SerializeField] private bool requireSurfaceHit;
        [SerializeField] private bool clampToRange = true;
        [SerializeField] private SkillIndicatorPlacementMode placementMode = SkillIndicatorPlacementMode.GroundSurface;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Geometry")]
        [SerializeField] private SkillIndicatorShapeType shapeType = SkillIndicatorShapeType.GroundCircle;
        [SerializeField, Min(0f)] private float range;
        [SerializeField, Min(0f)] private float radius;
        [SerializeField, Min(0f)] private float width;
        [SerializeField, Min(0f)] private float length;
        [SerializeField, Range(1f, 360f)] private float angle = 90f;
        [SerializeField, Min(0f)] private float height = 2f;
        [SerializeField, Min(0f)] private float groundOffset = 0.06f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float tapMaxDuration = 0.16f;
        [SerializeField, Min(0f)] private float holdEnterDelay = 0.1f;
        [SerializeField, Min(0f)] private float castDelay;
        [SerializeField, Min(0f)] private float warningTime;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField, Min(0f)] private float fadeOut = 0.15f;

        [Header("Art")]
        [SerializeField] private string previewPrefabResourceId;
        [SerializeField] private string validMaterialResourceId = "M_IND_OwnerValid";
        [SerializeField] private string invalidMaterialResourceId = "M_IND_Invalid";
        [SerializeField] private string confirmAudioResourceId = "S_IND_ConfirmRelease";
        [SerializeField] private string invalidAudioResourceId = "S_IND_Invalid";

        [Header("Debug")]
        [SerializeField] private bool debugDraw;

        public string AbilityId => abilityId;
        public string IndicatorId => indicatorId;
        public SkillIndicatorOwnerType OwnerType => ownerType;
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
        public SkillIndicatorShapeType ShapeType => shapeType;
        public float Range => range;
        public float Radius => radius;
        public float Width => width;
        public float Length => length;
        public float Angle => angle;
        public float Height => height;
        public float GroundOffset => groundOffset;
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

        private void OnValidate()
        {
            range = Mathf.Max(0f, range);
            radius = Mathf.Max(0f, radius);
            width = Mathf.Max(0f, width);
            length = Mathf.Max(0f, length);
            height = Mathf.Max(0f, height);
            groundOffset = Mathf.Max(0f, groundOffset);
            tapMaxDuration = Mathf.Max(0f, tapMaxDuration);
            holdEnterDelay = Mathf.Max(0f, holdEnterDelay);
            castDelay = Mathf.Max(0f, castDelay);
            warningTime = Mathf.Max(0f, warningTime);
            duration = Mathf.Max(0f, duration);
            fadeOut = Mathf.Max(0f, fadeOut);
            angle = Mathf.Clamp(angle, 1f, 360f);
        }
    }

    [Serializable]
    public struct SkillIndicatorResolvedConfig
    {
        public string abilityId;
        public SkillIndicatorInputMode inputMode;
        public SkillIndicatorDefaultReleasePolicy tapPolicy;
        public SkillIndicatorDefaultReleasePolicy holdPolicy;
        public SkillIndicatorInvalidReleasePolicy invalidReleasePolicy;
        public SkillIndicatorAimSource aimSource;
        public SkillIndicatorPlacementMode placementMode;
        public SkillIndicatorShapeType shapeType;
        public bool requireSurfaceHit;
        public bool clampToRange;
        public LayerMask surfaceMask;
        public LayerMask collisionMask;
        public float range;
        public float radius;
        public float width;
        public float length;
        public float angle;
        public float height;
        public float groundOffset;
        public float tapMaxDuration;
        public float holdEnterDelay;
        public string previewPrefabResourceId;
        public string validMaterialResourceId;
        public string invalidMaterialResourceId;

        public static SkillIndicatorResolvedConfig From(SkillIndicatorConfig config, WeaponDefinition weapon)
        {
            float fallbackRange = weapon != null ? Mathf.Max(0.1f, weapon.Range) : 6f;
            float fallbackRadius = weapon != null ? Mathf.Max(0.05f, weapon.Radius) : 1f;

            SkillIndicatorShapeType shape = config != null ? config.ShapeType : SkillIndicatorShapeType.GroundCircle;
            SkillIndicatorResolvedConfig resolved = new SkillIndicatorResolvedConfig
            {
                abilityId = ResolveAbilityId(config, weapon),
                inputMode = config != null ? config.InputMode : SkillIndicatorInputMode.HoldPreview,
                tapPolicy = config != null ? config.TapPolicy : SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget,
                holdPolicy = config != null ? config.HoldPolicy : SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit,
                invalidReleasePolicy = config != null ? config.InvalidReleasePolicy : SkillIndicatorInvalidReleasePolicy.Cancel,
                aimSource = config != null ? config.AimSource : SkillIndicatorAimSource.CrosshairRay,
                placementMode = config != null ? config.PlacementMode : SkillIndicatorPlacementMode.GroundSurface,
                shapeType = shape,
                requireSurfaceHit = config != null && config.RequireSurfaceHit,
                clampToRange = config == null || config.ClampToRange,
                surfaceMask = config != null ? config.SurfaceMask : ~0,
                collisionMask = config != null ? config.CollisionMask : ~0,
                range = config != null && config.Range > 0f ? config.Range : fallbackRange,
                radius = config != null && config.Radius > 0f ? config.Radius : fallbackRadius,
                width = config != null && config.Width > 0f ? config.Width : fallbackRadius * 2f,
                length = config != null && config.Length > 0f ? config.Length : fallbackRange,
                angle = config != null ? Mathf.Clamp(config.Angle, 1f, 360f) : 90f,
                height = config != null && config.Height > 0f ? config.Height : 2f,
                groundOffset = config != null ? Mathf.Max(0f, config.GroundOffset) : 0.06f,
                tapMaxDuration = config != null ? config.TapMaxDuration : 0.16f,
                holdEnterDelay = config != null ? config.HoldEnterDelay : 0.1f,
                previewPrefabResourceId = ResolvePrefabResourceId(config, shape),
                validMaterialResourceId = config != null && !string.IsNullOrWhiteSpace(config.ValidMaterialResourceId) ? config.ValidMaterialResourceId : "M_IND_OwnerValid",
                invalidMaterialResourceId = config != null && !string.IsNullOrWhiteSpace(config.InvalidMaterialResourceId) ? config.InvalidMaterialResourceId : "M_IND_Invalid",
            };

            return resolved;
        }

        private static string ResolveAbilityId(SkillIndicatorConfig config, WeaponDefinition weapon)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.AbilityId))
            {
                return config.AbilityId;
            }

            return weapon != null ? weapon.name : string.Empty;
        }

        private static string ResolvePrefabResourceId(SkillIndicatorConfig config, SkillIndicatorShapeType shape)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.PreviewPrefabResourceId))
            {
                return config.PreviewPrefabResourceId;
            }

            switch (shape)
            {
                case SkillIndicatorShapeType.TargetReticle:
                    return "PF_IND_TargetReticle";
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                    return "PF_IND_LineRect";
                case SkillIndicatorShapeType.Cone:
                    return "PF_IND_Cone";
                case SkillIndicatorShapeType.ArcTrajectory:
                    return "PF_IND_ArcTrajectory";
                case SkillIndicatorShapeType.Footprint:
                    return "PF_IND_Footprint";
                case SkillIndicatorShapeType.Tether:
                    return "PF_IND_TetherLine";
                case SkillIndicatorShapeType.CountdownDanger:
                    return "PF_IND_CountdownDanger";
                case SkillIndicatorShapeType.PersistentZone:
                    return "PF_IND_PersistentZone";
                default:
                    return "PF_IND_GroundCircle";
            }
        }

        public bool SticksToGround()
        {
            return placementMode == SkillIndicatorPlacementMode.GroundSurface;
        }
    }
}
