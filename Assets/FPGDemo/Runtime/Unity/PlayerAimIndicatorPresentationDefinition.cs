using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Character-owned, presentation-only tuning for the layered aim indicator.
    /// It deliberately contains no input, targeting, spread or damage values.
    /// </summary>
    [Serializable]
    public sealed class PlayerAimIndicatorPresentationDefinition
    {
        // Legacy ring-only values are retained for asset compatibility.
        [SerializeField, HideInInspector]
        private Color restingColor = new Color(0.48f, 0.82f, 0.92f, 0.56f);

        [SerializeField, HideInInspector]
        private Color aimingColor = new Color(0.76f, 0.96f, 1f, 0.96f);

        [D0PlannerSection("射击与命中反馈")]
        [D0PlannerField("射击闪光颜色", "射击事务成功提交时，放大圆环过渡到的峰值颜色。空弹、换弹锁定或查询失败不会触发。")]
        [SerializeField]
        private Color shotColor = Color.white;

        [D0PlannerField("命中提示颜色", "攻击真实命中战斗目标时，圆环外围四段提示使用的颜色。当前 Fei 规范为红色。")]
        [SerializeField]
        private Color hitColor = new Color(1f, 0.13f, 0.10f, 1f);

        [D0PlannerField("射击脉冲起始半径（像素）", "射击成功提交时扩张圆环的起始半径。只影响 Shot 动效，不改变正常准星尺寸。")]
        [SerializeField, Min(1f)]
        private float baseRadius = 15f;

        [D0PlannerField("射击脉冲线宽（像素）", "射击成功提交时扩张圆环的线宽，也作为命中提示内外缘校验基准。")]
        [SerializeField, Min(0.5f)]
        private float ringThickness = 2f;

        [SerializeField, HideInInspector]
        private float aimingGlowAlpha = 0.22f;

        [D0PlannerField("射击峰值半径（像素）", "射击成功提交时圆环短暂扩张到的半径，单位为 UI 像素；必须大于基础半径。")]
        [SerializeField, Min(1f)]
        private float shotRadius = 23f;

        [D0PlannerField("射击脉冲时长（秒）", "圆环从基础大小扩张并回落的总时长，使用非缩放时间；暂停时冻结。")]
        [SerializeField, Min(0.01f)]
        private float shotDuration = 0.16f;

        [D0PlannerField("命中提示半径（像素）", "红色外围分段提示的起始半径，单位为 UI 像素；必须在基础圆环之外。")]
        [SerializeField, Min(1f)]
        private float hitMarkerRadius = 27f;

        [D0PlannerField("命中提示线宽（像素）", "红色外围分段提示的线宽，单位为 UI 像素。")]
        [SerializeField, Min(0.5f)]
        private float hitMarkerThickness = 2.6f;

        [D0PlannerField("命中分段角度（度）", "外围四段提示各自覆盖的圆弧角度，单位为度，范围 4–60。")]
        [SerializeField, Range(4f, 60f)]
        private float hitMarkerArcDegrees = 24f;

        [D0PlannerField("命中外扩距离（像素）", "命中提示淡出期间继续向外移动的距离，单位为 UI 像素。")]
        [SerializeField, Min(0f)]
        private float hitExpansion = 4f;

        [D0PlannerField("命中提示时长（秒）", "红色外围提示从出现到淡出的总时长，使用非缩放时间；暂停时冻结。")]
        [SerializeField, Min(0.01f)]
        private float hitDuration = 0.20f;

        [D0PlannerSection("分层准星基础状态")]
        [D0PlannerField("正常准星颜色", "瞄准有效但未指向敌人时使用的细准星颜色。Fei 默认使用白色。")]
        [SerializeField]
        private Color normalColor = Color.white;

        [D0PlannerField("敌人准星颜色", "权威中心射线指向敌方 Combatant 时使用的准星颜色；投射物和普通环境不会触发。")]
        [SerializeField]
        private Color enemyColor = new Color(0.22f, 0.68f, 1f, 1f);

        [D0PlannerField("不可攻击颜色", "玩家或武器状态暂时不允许攻击、但不是换弹和当前掩体阻挡时使用的准星颜色。")]
        [SerializeField]
        private Color unavailableColor = new Color(0.65f, 0.68f, 0.72f, 0.72f);

        [D0PlannerField("当前掩体阻挡颜色", "枪口到意图点被当前依附掩体阻挡时，禁止射击符号使用的颜色。")]
        [SerializeField]
        private Color currentCoverBlockedColor = new Color(1f, 0.24f, 0.18f, 1f);

        [D0PlannerField("换弹进度颜色", "换弹状态下权威进度环与循环动效使用的颜色。")]
        [SerializeField]
        private Color reloadColor = Color.white;

        [D0PlannerField("准星中心间隙（像素）", "四条准星臂与屏幕瞄准点之间的距离。数值越小，准星越紧凑。")]
        [SerializeField, Min(0f)]
        private float crosshairGap = 4f;

        [D0PlannerField("准星臂长（像素）", "正常、敌人和不可攻击状态下每条准星臂的长度。")]
        [SerializeField, Min(0.5f)]
        private float crosshairArmLength = 7f;

        [D0PlannerField("准星线宽（像素）", "正常、敌人和不可攻击状态下准星臂的线宽。")]
        [SerializeField, Min(0.5f)]
        private float crosshairThickness = 1.5f;

        [D0PlannerField("禁止符号半径（像素）", "当前掩体阻挡时显示的禁止射击圆圈半径。")]
        [SerializeField, Min(1f)]
        private float prohibitedRadius = 14f;

        [D0PlannerField("禁止符号线宽（像素）", "当前掩体阻挡时禁止射击圆圈和斜线的线宽。")]
        [SerializeField, Min(0.5f)]
        private float prohibitedThickness = 2f;

        [D0PlannerSection("散布与范围圈")]
        [D0PlannerField("主射散布圈颜色", "主射时覆盖真实 pellet 弹道锥的屏幕散布圈颜色。建议保持低透明度，避免遮挡目标。")]
        [SerializeField]
        private Color primarySpreadColor = new Color(1f, 1f, 1f, 0.34f);

        [D0PlannerField("主射散布圈线宽（像素）", "主射真实散布圈的屏幕线宽。只改变显示，不改变弹道。")]
        [SerializeField, Min(0.25f)]
        private float primarySpreadThickness = 1f;

        [D0PlannerField("副射范围圈颜色", "副射蓄力期间固定屏幕范围圈的颜色。该圆圈只表达大致落点范围。")]
        [SerializeField]
        private Color secondaryRangeColor = new Color(0.22f, 0.72f, 1f, 0.52f);

        [D0PlannerField("副射范围圈线宽（像素）", "副射固定屏幕范围圈的线宽。只改变显示，不改变范围伤害。")]
        [SerializeField, Min(0.25f)]
        private float secondaryRangeThickness = 1.5f;

        [D0PlannerField("副射范围参考距离（米）", "把副射世界范围换算为固定屏幕圆圈时使用的标定距离。默认按 20 米表达大致范围，不是世界空间投影。")]
        [SerializeField, Min(0.01f)]
        private float secondaryReferenceDistance = 20f;

        [D0PlannerSection("换弹进度环")]
        [D0PlannerField("换弹环半径（像素）", "换弹时围绕瞄准点显示的权威进度环半径。")]
        [SerializeField, Min(1f)]
        private float reloadRadius = 21f;

        [D0PlannerField("换弹环线宽（像素）", "换弹权威进度环的屏幕线宽。")]
        [SerializeField, Min(0.5f)]
        private float reloadThickness = 2f;

        [D0PlannerField("换弹环旋转速度（度/秒）", "换弹环轻微循环动效的旋转速度；设为 0 可关闭旋转，但仍显示权威进度。")]
        [SerializeField, Min(0f)]
        private float reloadSpinDegreesPerSecond = 90f;

        [Obsolete("The layered reticle no longer renders a resting ring.")]
        public Color RestingColor => restingColor;
        [Obsolete("The layered reticle no longer renders an aiming ring.")]
        public Color AimingColor => aimingColor;
        public Color ShotColor => shotColor;
        public Color HitColor => hitColor;
        public float BaseRadius => baseRadius;
        public float RingThickness => ringThickness;
        [Obsolete("The layered reticle no longer renders an aiming glow.")]
        public float AimingGlowAlpha => aimingGlowAlpha;
        public float ShotRadius => shotRadius;
        public float ShotDuration => shotDuration;
        public float HitMarkerRadius => hitMarkerRadius;
        public float HitMarkerThickness => hitMarkerThickness;
        public float HitMarkerArcDegrees => hitMarkerArcDegrees;
        public float HitExpansion => hitExpansion;
        public float HitDuration => hitDuration;
        public Color NormalColor => normalColor;
        public Color EnemyColor => enemyColor;
        public Color UnavailableColor => unavailableColor;
        public Color CurrentCoverBlockedColor => currentCoverBlockedColor;
        public Color ReloadColor => reloadColor;
        public float CrosshairGap => crosshairGap;
        public float CrosshairArmLength => crosshairArmLength;
        public float CrosshairThickness => crosshairThickness;
        public float ProhibitedRadius => prohibitedRadius;
        public float ProhibitedThickness => prohibitedThickness;
        public Color PrimarySpreadColor => primarySpreadColor;
        public float PrimarySpreadThickness => primarySpreadThickness;
        public Color SecondaryRangeColor => secondaryRangeColor;
        public float SecondaryRangeThickness => secondaryRangeThickness;
        public float SecondaryReferenceDistance => secondaryReferenceDistance;
        public float ReloadRadius => reloadRadius;
        public float ReloadThickness => reloadThickness;
        public float ReloadSpinDegreesPerSecond => reloadSpinDegreesPerSecond;

        public bool TryValidate(out string error)
        {
            if (!IsVisible(shotColor) || !IsVisible(hitColor))
            {
                error = "Aim indicator colors must have visible alpha values.";
                return false;
            }

            if (!IsFinite(baseRadius) || baseRadius < 1f
                || !IsFinite(ringThickness) || ringThickness < 0.5f
                || ringThickness >= baseRadius * 2f
                || !IsFinite(shotRadius) || shotRadius <= baseRadius
                || !IsFinite(shotDuration) || shotDuration < 0.01f)
            {
                error =
                    "Aim indicator base ring and shot pulse must respect their radius, line-width and duration limits.";
                return false;
            }

            float shotOuterEdge = shotRadius + ringThickness * 0.5f;
            float hitInnerEdge = hitMarkerRadius - hitMarkerThickness * 0.5f;
            float expandedHitOuterEdge =
                hitMarkerRadius + hitExpansion + hitMarkerThickness * 0.5f;
            if (!IsFinite(hitMarkerRadius) || hitMarkerRadius < 1f
                || !IsFinite(hitMarkerThickness) || hitMarkerThickness < 0.5f
                || hitMarkerThickness >= hitMarkerRadius * 2f
                || !IsFinite(shotOuterEdge)
                || !IsFinite(hitInnerEdge)
                || hitInnerEdge <= shotOuterEdge
                || !IsFinite(expandedHitOuterEdge)
                || !IsFinite(hitMarkerArcDegrees)
                || hitMarkerArcDegrees < 4f || hitMarkerArcDegrees > 60f
                || !IsFinite(hitExpansion) || hitExpansion < 0f
                || !IsFinite(hitDuration) || hitDuration < 0.01f)
            {
                error =
                    "Aim indicator hit arcs must remain outside the shot ring and respect their geometry and duration limits.";
                return false;
            }

            if (!IsVisible(normalColor) || !IsVisible(enemyColor)
                || !IsVisible(unavailableColor)
                || !IsVisible(currentCoverBlockedColor)
                || !IsVisible(reloadColor)
                || !IsVisible(primarySpreadColor)
                || !IsVisible(secondaryRangeColor))
            {
                error = "Layered aim-indicator colors must have visible alpha values.";
                return false;
            }

            float crosshairOuterRadius = crosshairGap + crosshairArmLength;
            if (!IsFinite(crosshairGap) || crosshairGap < 0f
                || !IsFinitePositive(crosshairArmLength)
                || !IsFinitePositive(crosshairThickness)
                || crosshairThickness >= crosshairOuterRadius * 2f
                || !IsFinitePositive(prohibitedRadius)
                || !IsFinitePositive(prohibitedThickness)
                || prohibitedThickness >= prohibitedRadius * 2f
                || !IsFinitePositive(primarySpreadThickness)
                || !IsFinitePositive(secondaryRangeThickness)
                || !IsFinitePositive(secondaryReferenceDistance)
                || !IsFinitePositive(reloadRadius)
                || !IsFinitePositive(reloadThickness)
                || reloadThickness >= reloadRadius * 2f
                || !IsFinite(reloadSpinDegreesPerSecond)
                || reloadSpinDegreesPerSecond < 0f)
            {
                error = "Layered aim-indicator geometry and timing values are invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsVisible(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b)
                && IsFinite(value.a) && value.a > 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
