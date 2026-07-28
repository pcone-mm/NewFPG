using System;
using System.Globalization;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgReticleTargetState
    {
        Idle = 0,
        Hittable,
        Blocked
    }

    public enum FpgReticlePulseState
    {
        None = 0,
        Shot,
        Hit
    }

    public enum FpgHudResourceKind
    {
        Life = 0,
        Barrier,
        Ammo
    }

    [Serializable]
    public sealed class FpgHudResourcePresentation
    {
        [SerializeField]
        [InspectorName("资源类型")]
        [Tooltip("绑定正式玩家 HUD 的权威资源。生命、护盾和弹药各配置一次，不允许重复。")]
        private FpgHudResourceKind kind;

        [SerializeField]
        [InspectorName("显示标签")]
        [Tooltip("显示在 current/max 数值前的文本标签，由正式玩家 HUD 直接读取。")]
        private string label = "RESOURCE";

        [SerializeField]
        [InspectorName("条形颜色")]
        [Tooltip("正式玩家 HUD 对应资源条的填充颜色；不改变数值文字和战斗数据。")]
        private Color color = Color.white;

        [SerializeField]
        [InspectorName("显示顺序")]
        [Tooltip("三个资源条的排列顺序，数值越小越靠上；运行时映射到场景已有的三个 Y 槽位。")]
        private int order;

        [SerializeField]
        [InspectorName("数值格式")]
        [Tooltip("current/max 数字格式。必须同时包含 {0}（当前值）和 {1}（上限），由正式玩家 HUD 使用固定区域格式化。")]
        private string valueFormat = "{0}/{1}";

        [SerializeField, Min(0.01f)]
        [InspectorName("条形缓动时长（秒）")]
        [Tooltip("资源条从当前视觉比例缓动到权威比例所需秒数，必须大于 0；数值文字仍在同帧立即更新，暂停时缓动冻结。")]
        private float barEaseDuration = 0.14f;

        public FpgHudResourcePresentation()
        {
        }

        public FpgHudResourcePresentation(
            FpgHudResourceKind kind,
            string label,
            Color color,
            int order,
            string valueFormat,
            float barEaseDuration)
        {
            this.kind = kind;
            this.label = label;
            this.color = color;
            this.order = order;
            this.valueFormat = valueFormat;
            this.barEaseDuration = barEaseDuration;
        }

        public FpgHudResourceKind Kind => kind;
        public string Label => label;
        public Color Color => color;
        public int Order => order;
        public string ValueFormat => valueFormat;
        public float BarEaseDuration => barEaseDuration;

        internal bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(FpgHudResourceKind), kind)
                || string.IsNullOrWhiteSpace(label)
                || string.IsNullOrWhiteSpace(valueFormat)
                || !valueFormat.Contains("{0}")
                || !valueFormat.Contains("{1}")
                || !IsVisible(color)
                || !IsFinitePositive(barEaseDuration))
            {
                error = "Formal HUD resource presentation is invalid.";
                return false;
            }

            try
            {
                string.Format(
                    CultureInfo.InvariantCulture,
                    valueFormat,
                    0,
                    0);
            }
            catch (FormatException)
            {
                error = "Formal HUD resource value format is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static bool IsVisible(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g)
                && IsFinite(value.b) && IsFinite(value.a) && value.a > 0f;
        }

        internal static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        internal static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class FpgDamagePopupSpriteStyle
    {
        [SerializeField]
        [InspectorName("命中表现类型")]
        [Tooltip("此套跳字 Sprite 对应的正式命中类型。普通命中、弱点命中和弹体拦截必须各配置一次，且不允许重复。")]
        private CombatHitPresentationKind kind;

        [SerializeField]
        [InspectorName("背景图片")]
        [Tooltip("显示在伤害数字后方的背景 Sprite，不能为空。")]
        private Sprite backgroundSprite;

        [SerializeField]
        [InspectorName("数字图片（0-9）")]
        [Tooltip("按索引 0 到 9 依次配置十张数字 Sprite。数组必须恰好包含十项，且每项都不能为空。")]
        private Sprite[] digitSprites = new Sprite[10];

        [SerializeField, Min(0.01f)]
        [InspectorName("数字高度（参考像素）")]
        [Tooltip("每个数字 Sprite 在 HUD Canvas 参考分辨率下的显示高度，必须大于 0。宽度按 Sprite 原始宽高比计算。")]
        private float digitHeight = 60f;

        [SerializeField]
        [InspectorName("数字间距（参考像素）")]
        [Tooltip("相邻数字之间的水平间距，可使用负值让数字适度重叠；数值必须为有限值。")]
        private float digitSpacing = -2f;

        [SerializeField, Min(0f)]
        [InspectorName("背景水平留白（参考像素）")]
        [Tooltip("数字整体左右两侧各自保留的背景宽度，必须不小于 0。")]
        private float backgroundHorizontalPadding = 34f;

        [SerializeField]
        [InspectorName("背景最小尺寸（参考像素）")]
        [Tooltip("跳字背景在 HUD Canvas 参考分辨率下允许的最小宽高，两个分量都必须大于 0。")]
        private Vector2 backgroundMinSize = new Vector2(133f, 50f);

        public CombatHitPresentationKind Kind => kind;
        public Sprite BackgroundSprite => backgroundSprite;
        public float DigitHeight => digitHeight;
        public float DigitSpacing => digitSpacing;
        public float BackgroundHorizontalPadding => backgroundHorizontalPadding;
        public Vector2 BackgroundMinSize => backgroundMinSize;

        public Sprite GetDigitSprite(int digit)
        {
            return digitSprites != null
                && digit >= 0
                && digit < digitSprites.Length
                    ? digitSprites[digit]
                    : null;
        }

        internal bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(CombatHitPresentationKind), kind))
            {
                error = "Formal damage-popup Sprite style has an invalid hit kind.";
                return false;
            }

            if (backgroundSprite == null)
            {
                error = "Formal damage-popup Sprite style requires a background Sprite.";
                return false;
            }

            if (digitSprites == null || digitSprites.Length != 10)
            {
                error = "Formal damage-popup Sprite style requires exactly ten digit Sprites.";
                return false;
            }

            for (int digit = 0; digit < digitSprites.Length; digit++)
            {
                if (digitSprites[digit] == null)
                {
                    error = $"Formal damage-popup Sprite style is missing digit Sprite {digit}.";
                    return false;
                }
            }

            if (!FpgHudResourcePresentation.IsFinitePositive(digitHeight)
                || float.IsNaN(digitSpacing)
                || float.IsInfinity(digitSpacing)
                || !FpgHudResourcePresentation.IsFiniteNonNegative(backgroundHorizontalPadding)
                || !FpgHudResourcePresentation.IsFinitePositive(backgroundMinSize.x)
                || !FpgHudResourcePresentation.IsFinitePositive(backgroundMinSize.y))
            {
                error = "Formal damage-popup Sprite style layout is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FpgDamagePopupPresentation
    {
        [SerializeField]
        [InspectorName("跳字 Sprite 样式")]
        [Tooltip("正式跳字使用的 Sprite 样式。普通命中、弱点命中和弹体拦截必须各有且仅有一项完整配置。")]
        private FpgDamagePopupSpriteStyle[] spriteStyles =
            Array.Empty<FpgDamagePopupSpriteStyle>();

        [SerializeField, Min(0f)]
        [InspectorName("命中点上移（参考像素）")]
        [Tooltip("命中世界坐标投影到 HUD 后向上偏移的参考像素数，必须不小于 0。")]
        private float screenVerticalOffset = 24f;

        [SerializeField, Min(0f)]
        [InspectorName("同帧邻近判定距离（参考像素）")]
        [Tooltip("同一帧内两个跳字投影位置小于等于此参考像素距离时进行垂直错位；只影响表现，不聚合伤害。")]
        private float nearbyDistance = 42f;

        [SerializeField, Min(0f)]
        [InspectorName("邻近跳字垂直步长（参考像素）")]
        [Tooltip("同一帧邻近跳字每增加一个时向上追加的参考像素数，必须不小于 0。")]
        private float nearbyVerticalStep = 20f;

        public float ScreenVerticalOffset => screenVerticalOffset;
        public float NearbyDistance => nearbyDistance;
        public float NearbyVerticalStep => nearbyVerticalStep;

        public bool TryGetSpriteStyle(
            CombatHitPresentationKind kind,
            out FpgDamagePopupSpriteStyle style)
        {
            style = null;
            if (spriteStyles == null)
            {
                return false;
            }

            for (int i = 0; i < spriteStyles.Length; i++)
            {
                FpgDamagePopupSpriteStyle candidate = spriteStyles[i];
                if (candidate != null && candidate.Kind == kind)
                {
                    style = candidate;
                    return true;
                }
            }

            return false;
        }

        internal bool TryValidate(out string error)
        {
            if (!FpgHudResourcePresentation.IsFiniteNonNegative(screenVerticalOffset)
                || !FpgHudResourcePresentation.IsFiniteNonNegative(nearbyDistance)
                || !FpgHudResourcePresentation.IsFiniteNonNegative(nearbyVerticalStep))
            {
                error = "Formal damage-popup presentation is invalid.";
                return false;
            }

            if (spriteStyles == null || spriteStyles.Length != 3)
            {
                error = "Formal damage-popup presentation requires exactly three Sprite styles.";
                return false;
            }

            bool hasBody = false;
            bool hasWeakpoint = false;
            bool hasIntercept = false;
            for (int i = 0; i < spriteStyles.Length; i++)
            {
                FpgDamagePopupSpriteStyle style = spriteStyles[i];
                if (style == null)
                {
                    error = "Formal damage-popup presentation contains a null Sprite style.";
                    return false;
                }
                if (!style.TryValidate(out error))
                {
                    return false;
                }

                switch (style.Kind)
                {
                    case CombatHitPresentationKind.Body:
                        if (hasBody)
                        {
                            error = "Formal damage-popup presentation contains duplicate Body Sprite styles.";
                            return false;
                        }

                        hasBody = true;
                        break;
                    case CombatHitPresentationKind.Weakpoint:
                        if (hasWeakpoint)
                        {
                            error = "Formal damage-popup presentation contains duplicate Weakpoint Sprite styles.";
                            return false;
                        }

                        hasWeakpoint = true;
                        break;
                    case CombatHitPresentationKind.Intercept:
                        if (hasIntercept)
                        {
                            error = "Formal damage-popup presentation contains duplicate Intercept Sprite styles.";
                            return false;
                        }

                        hasIntercept = true;
                        break;
                    default:
                        error = "Formal damage-popup presentation contains an unsupported Sprite style.";
                        return false;
                }
            }

            if (!hasBody || !hasWeakpoint || !hasIntercept)
            {
                error = "Formal damage-popup presentation is missing a required Sprite style.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class FpgReticlePresentation
    {
        [SerializeField]
        [InspectorName("空闲颜色")]
        [Tooltip("正式准星当前没有合法目标且未被掩体阻挡时的颜色。")]
        private Color idleColor = Color.white;

        [SerializeField]
        [InspectorName("可命中颜色")]
        [Tooltip("正式 Aim Solution 判定当前射线存在合法可命中目标时的准星颜色。")]
        private Color hittableColor = new Color(0.3f, 1f, 0.55f, 1f);

        [SerializeField]
        [InspectorName("掩体阻挡颜色")]
        [Tooltip("正式 Aim Solution 判定射线先被掩体截停时的准星颜色。")]
        private Color blockedColor = new Color(1f, 0.45f, 0.16f, 1f);

        [SerializeField]
        [InspectorName("射击脉冲颜色")]
        [Tooltip("正式玩家攻击成功提交后，射击脉冲持续期间覆盖目标状态的准星颜色。")]
        private Color shotColor = new Color(0.4f, 0.9f, 1f, 1f);

        [SerializeField]
        [InspectorName("命中脉冲颜色")]
        [Tooltip("正式伤害 Impact 成功结算后，命中脉冲持续期间覆盖目标状态的准星颜色。")]
        private Color hitColor = new Color(1f, 0.9f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Color of the secondary-charge radial progress ring.")]
        private Color chargeRingColor = new Color(0.25f, 0.9f, 1f, 1f);

        [SerializeField, Min(1f)]
        [InspectorName("空闲尺寸（参考像素）")]
        [Tooltip("空闲状态下准星两条 Graphic 笔画的主轴长度，按 HUD Canvas 参考分辨率计算，必须至少为 1。")]
        private float idleSize = 18f;

        [SerializeField, Min(1f)]
        [InspectorName("可命中尺寸（参考像素）")]
        [Tooltip("可命中状态下准星两条 Graphic 笔画的主轴长度，按 HUD Canvas 参考分辨率计算，必须至少为 1。")]
        private float hittableSize = 20f;

        [SerializeField, Min(1f)]
        [InspectorName("掩体阻挡尺寸（参考像素）")]
        [Tooltip("掩体阻挡状态下准星两条 Graphic 笔画的主轴长度，按 HUD Canvas 参考分辨率计算，必须至少为 1。")]
        private float blockedSize = 22f;

        [SerializeField, Min(1f)]
        [InspectorName("射击脉冲尺寸（参考像素）")]
        [Tooltip("射击脉冲期间准星两条 Graphic 笔画的主轴长度，按 HUD Canvas 参考分辨率计算，必须至少为 1。")]
        private float shotPulseSize = 28f;

        [SerializeField, Min(1f)]
        [InspectorName("命中脉冲尺寸（参考像素）")]
        [Tooltip("命中脉冲期间准星两条 Graphic 笔画的主轴长度，按 HUD Canvas 参考分辨率计算，必须至少为 1。")]
        private float hitPulseSize = 30f;

        [SerializeField, Min(1f)]
        [Tooltip("Reference-pixel size of the secondary-charge radial progress ring.")]
        private float chargeRingSize = 36f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Opacity multiplier applied only to the secondary-charge ring.")]
        private float chargeRingAlpha = 0.9f;

        [SerializeField, Min(0.01f)]
        [InspectorName("射击脉冲时长（秒）")]
        [Tooltip("成功提交攻击后射击脉冲保持的未缩放秒数，必须大于 0；正式战斗暂停时计时冻结。")]
        private float shotPulseDuration = 0.08f;

        [SerializeField, Min(0.01f)]
        [InspectorName("命中脉冲时长（秒）")]
        [Tooltip("成功结算 Impact 后命中脉冲保持的未缩放秒数，必须大于 0；正式战斗暂停时计时冻结。")]
        private float hitPulseDuration = 0.14f;

        public Color IdleColor => idleColor;
        public Color HittableColor => hittableColor;
        public Color BlockedColor => blockedColor;
        public Color ShotColor => shotColor;
        public Color HitColor => hitColor;
        public Color ChargeRingColor => chargeRingColor;
        public float IdleSize => idleSize;
        public float HittableSize => hittableSize;
        public float BlockedSize => blockedSize;
        public float ShotPulseSize => shotPulseSize;
        public float HitPulseSize => hitPulseSize;
        public float ChargeRingSize => chargeRingSize;
        public float ChargeRingAlpha => chargeRingAlpha;
        public float ShotPulseDuration => shotPulseDuration;
        public float HitPulseDuration => hitPulseDuration;

        internal bool TryValidate(out string error)
        {
            if (!FpgHudResourcePresentation.IsVisible(idleColor)
                || !FpgHudResourcePresentation.IsVisible(hittableColor)
                || !FpgHudResourcePresentation.IsVisible(blockedColor)
                || !FpgHudResourcePresentation.IsVisible(shotColor)
                || !FpgHudResourcePresentation.IsVisible(hitColor)
                || !FpgHudResourcePresentation.IsVisible(chargeRingColor)
                || !FpgHudResourcePresentation.IsFinitePositive(idleSize)
                || !FpgHudResourcePresentation.IsFinitePositive(hittableSize)
                || !FpgHudResourcePresentation.IsFinitePositive(blockedSize)
                || !FpgHudResourcePresentation.IsFinitePositive(shotPulseSize)
                || !FpgHudResourcePresentation.IsFinitePositive(hitPulseSize)
                || !FpgHudResourcePresentation.IsFinitePositive(chargeRingSize)
                || !FpgHudResourcePresentation.IsFinitePositive(chargeRingAlpha)
                || chargeRingAlpha > 1f
                || !FpgHudResourcePresentation.IsFinitePositive(shotPulseDuration)
                || !FpgHudResourcePresentation.IsFinitePositive(hitPulseDuration))
            {
                error = "Formal reticle presentation is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
