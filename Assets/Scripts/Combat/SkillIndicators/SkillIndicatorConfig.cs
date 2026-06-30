using System;
using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    public enum SkillIndicatorOwnerType
    {
        [InspectorName("玩家")]
        Player,
        [InspectorName("怪物")]
        Monster,
        [InspectorName("中立")]
        Neutral,
    }

    public enum SkillIndicatorInputMode
    {
        [InspectorName("仅点击")]
        TapOnly,
        [InspectorName("长按预览")]
        HoldPreview,
        [InspectorName("自动预警")]
        AutoTelegraph,
    }

    public enum SkillIndicatorShapeType
    {
        [InspectorName("无")]
        None,
        [InspectorName("目标准星")]
        TargetReticle,
        [InspectorName("地面圆形")]
        GroundCircle,
        [InspectorName("直线")]
        Line,
        [InspectorName("矩形")]
        Rectangle,
        [InspectorName("扇形")]
        Cone,
        [InspectorName("抛物线轨迹")]
        ArcTrajectory,
        [InspectorName("放置占位")]
        Footprint,
        [InspectorName("连线")]
        Tether,
        [InspectorName("倒计时危险区")]
        CountdownDanger,
        [InspectorName("持续区域")]
        PersistentZone,
    }

    public enum SkillIndicatorAimSource
    {
        [InspectorName("准星射线")]
        CrosshairRay,
        [InspectorName("屏幕光标射线")]
        ScreenCursorRay,
        [InspectorName("施法者朝向")]
        OwnerForward,
        [InspectorName("自身位置")]
        Self,
    }

    public enum SkillIndicatorDefaultReleasePolicy
    {
        [InspectorName("朝前方最远射程释放")]
        CastForwardMaxRange,
        [InspectorName("在准星命中点释放")]
        CastAtCrosshairHit,
        [InspectorName("在准星下方地面释放")]
        CastAtGroundUnderCrosshair,
        [InspectorName("自动选择最佳目标")]
        AutoSelectBestTarget,
        [InspectorName("对自身释放")]
        CastOnSelf,
        [InspectorName("对当前锁定目标释放")]
        CastAtCurrentLock,
    }

    public enum SkillIndicatorInvalidReleasePolicy
    {
        [InspectorName("取消")]
        Cancel,
        [InspectorName("回退到默认释放")]
        FallbackToDefault,
    }

    public enum SkillIndicatorPlacementMode
    {
        [InspectorName("贴地表面")]
        GroundSurface,
        [InspectorName("世界空间")]
        WorldSpace,
        [InspectorName("挂到施法点")]
        AttachToCastOrigin,
    }

    public enum SkillIndicatorValidationReason
    {
        [InspectorName("无")]
        None,
        [InspectorName("已禁用")]
        Disabled,
        [InspectorName("冷却中")]
        Cooldown,
        [InspectorName("资源不足")]
        Resource,
        [InspectorName("未命中表面")]
        NoSurface,
        [InspectorName("超出射程")]
        OutOfRange,
        [InspectorName("被阻挡")]
        Blocked,
        [InspectorName("配置无效")]
        InvalidConfig,
        [InspectorName("No target")]
        NoTarget,
    }

    [CreateAssetMenu(fileName = "SkillIndicatorConfig", menuName = "NewFPG/战斗/技能指示器/技能指示器配置")]
    public sealed class SkillIndicatorConfig : ScriptableObject
    {
        [Header("身份")]
        [InspectorName("技能 ID"), Tooltip("技能或武器的逻辑 ID。留空时运行时会回退到 WeaponDefinition 名称。")]
        [SerializeField] private string abilityId;
        [InspectorName("指示器 ID"), Tooltip("这个指示器配置的资源 ID，方便调试和检索。")]
        [SerializeField] private string indicatorId;
        [InspectorName("归属方"), Tooltip("指示器所属阵营，用于区分玩家、怪物或中立来源。")]
        [SerializeField] private SkillIndicatorOwnerType ownerType = SkillIndicatorOwnerType.Player;
        [InspectorName("输入模式"), Tooltip("技能如何触发指示器：仅点击、长按预览或自动预警。")]
        [SerializeField] private SkillIndicatorInputMode inputMode = SkillIndicatorInputMode.HoldPreview;

        [Header("释放")]
        [InspectorName("点击释放策略"), Tooltip("短按或点击时使用的默认施法落点规则。")]
        [SerializeField] private SkillIndicatorDefaultReleasePolicy tapPolicy = SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget;
        [InspectorName("长按释放策略"), Tooltip("长按预览结束并松手时使用的施法落点规则。")]
        [SerializeField] private SkillIndicatorDefaultReleasePolicy holdPolicy = SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit;
        [InspectorName("无效释放策略"), Tooltip("当前预览位置无效时，松手后是取消还是回退到默认释放。")]
        [SerializeField] private SkillIndicatorInvalidReleasePolicy invalidReleasePolicy = SkillIndicatorInvalidReleasePolicy.Cancel;

        [Header("瞄准")]
        [InspectorName("瞄准来源"), Tooltip("决定指示器从哪里取瞄准方向或目标点。")]
        [SerializeField] private SkillIndicatorAimSource aimSource = SkillIndicatorAimSource.CrosshairRay;
        [InspectorName("必须命中表面"), Tooltip("开启后，射线没有命中合法表面时本次预览会判定为无效。")]
        [SerializeField] private bool requireSurfaceHit;
        [InspectorName("限制到射程"), Tooltip("开启后，目标点会被限制在射程范围内。")]
        [SerializeField] private bool clampToRange = true;
        [InspectorName("放置方式"), Tooltip("决定预览对象贴地、留在世界空间，还是挂到施法点上。")]
        [SerializeField] private SkillIndicatorPlacementMode placementMode = SkillIndicatorPlacementMode.GroundSurface;
        [InspectorName("可命中表面层"), Tooltip("瞄准射线允许命中的场景表面 Layer。")]
        [SerializeField] private LayerMask surfaceMask = ~0;
        [InspectorName("阻挡检测层"), Tooltip("用于检查路径或落点是否被遮挡的 Layer。")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("形状")]
        [InspectorName("指示器形状"), Tooltip("预览区域的几何类型，会影响默认预制体和缩放方式。")]
        [SerializeField] private SkillIndicatorShapeType shapeType = SkillIndicatorShapeType.GroundCircle;
        [InspectorName("射程"), Tooltip("技能最大可释放距离。为 0 时会优先使用武器配置的射程。")]
        [SerializeField, Min(0f)] private float range;
        [InspectorName("半径"), Tooltip("圆形、爆炸、光环等区域的半径。为 0 时会优先使用武器配置的半径。")]
        [SerializeField, Min(0f)] private float radius;
        [InspectorName("宽度"), Tooltip("直线、矩形或墙体类指示器的宽度。")]
        [SerializeField, Min(0f)] private float width;
        [InspectorName("长度"), Tooltip("直线、矩形、冲刺路径或射线类指示器的长度。")]
        [SerializeField, Min(0f)] private float length;
        [InspectorName("角度"), Tooltip("扇形或锥形指示器的张角，单位为度。")]
        [SerializeField, Range(1f, 360f)] private float angle = 90f;
        [InspectorName("高度"), Tooltip("世界空间或体积类指示器的参考高度。")]
        [SerializeField, Min(0f)] private float height = 2f;
        [InspectorName("离地偏移"), Tooltip("贴地预览抬离表面的高度，用于避免闪烁或穿插。")]
        [SerializeField, Min(0f)] private float groundOffset = 0.06f;

        [Header("时序")]
        [InspectorName("点击最长时长"), Tooltip("按下时间不超过这个值时，会按点击处理。")]
        [SerializeField, Min(0f)] private float tapMaxDuration = 0.16f;
        [InspectorName("长按预览延迟"), Tooltip("按住超过这个时间后进入预览状态。")]
        [SerializeField, Min(0f)] private float holdEnterDelay = 0.1f;
        [InspectorName("施法延迟"), Tooltip("确认释放后到实际生效前的延迟。")]
        [SerializeField, Min(0f)] private float castDelay;
        [InspectorName("预警时间"), Tooltip("怪物攻击或延迟技能正式生效前的提示时长。")]
        [SerializeField, Min(0f)] private float warningTime;
        [InspectorName("持续时间"), Tooltip("持续区域或持久提示存在的时长。")]
        [SerializeField, Min(0f)] private float duration;
        [InspectorName("淡出时间"), Tooltip("指示器消失时的淡出时长。")]
        [SerializeField, Min(0f)] private float fadeOut = 0.15f;

        [Header("美术资源")]
        [InspectorName("预览预制体资源 ID"), Tooltip("指定临时美术索引里的预览预制体 ID。留空时会按形状自动选择。")]
        [SerializeField] private string previewPrefabResourceId;
        [InspectorName("有效材质资源 ID"), Tooltip("预览位置有效时使用的材质资源 ID。")]
        [SerializeField] private string validMaterialResourceId = "M_IND_OwnerValid";
        [InspectorName("无效材质资源 ID"), Tooltip("预览位置无效时使用的材质资源 ID。")]
        [SerializeField] private string invalidMaterialResourceId = "M_IND_Invalid";
        [InspectorName("确认音效资源 ID"), Tooltip("成功确认释放时播放的音效资源 ID。")]
        [SerializeField] private string confirmAudioResourceId = "S_IND_ConfirmRelease";
        [InspectorName("无效音效资源 ID"), Tooltip("释放无效或取消时播放的音效资源 ID。")]
        [SerializeField] private string invalidAudioResourceId = "S_IND_Invalid";

        [Header("调试")]
        [InspectorName("绘制调试信息"), Tooltip("开启后绘制瞄准、落点和范围相关调试信息。")]
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
