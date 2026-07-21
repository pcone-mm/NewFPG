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
        [D0PlannerSection("基础圆环")]
        [D0PlannerField("常态颜色", "玩家未探身瞄准时的圆环颜色。只改变 UI 表现；Alpha 建议低于瞄准颜色以保留状态层级。")]
        [SerializeField]
        private Color restingColor = new Color(0.48f, 0.82f, 0.92f, 0.56f);

        [D0PlannerField("瞄准颜色", "战斗已接受瞄准／探身状态时的圆环颜色。不会改变准星位置、射线或命中判定。")]
        [SerializeField]
        private Color aimingColor = new Color(0.76f, 0.96f, 1f, 0.96f);

        [D0PlannerField("射击闪光颜色", "射击事务成功提交时，放大圆环过渡到的峰值颜色。空弹、换弹锁定或查询失败不会触发。")]
        [SerializeField]
        private Color shotColor = Color.white;

        [D0PlannerField("命中提示颜色", "攻击真实命中战斗目标时，圆环外围四段提示使用的颜色。当前 Fei 规范为红色。")]
        [SerializeField]
        private Color hitColor = new Color(1f, 0.13f, 0.10f, 1f);

        [D0PlannerField("圆环半径（像素）", "常态与瞄准状态下圆环的半径，单位为 UI 像素。必须大于 0。")]
        [SerializeField, Min(1f)]
        private float baseRadius = 15f;

        [D0PlannerField("圆环线宽（像素）", "基础圆环的线宽，单位为 UI 像素。瞄准态会在其外增加低透明光晕。")]
        [SerializeField, Min(0.5f)]
        private float ringThickness = 2f;

        [D0PlannerField("瞄准光晕强度", "战斗已接受瞄准／探身状态时的圆环光晕 Alpha，范围 0–1；0 表示关闭光晕。")]
        [SerializeField, Range(0f, 1f)]
        private float aimingGlowAlpha = 0.22f;

        [D0PlannerSection("射击反馈")]
        [D0PlannerField("射击峰值半径（像素）", "射击成功提交时圆环短暂扩张到的半径，单位为 UI 像素；必须大于基础半径。")]
        [SerializeField, Min(1f)]
        private float shotRadius = 23f;

        [D0PlannerField("射击脉冲时长（秒）", "圆环从基础大小扩张并回落的总时长，使用非缩放时间；暂停时冻结。")]
        [SerializeField, Min(0.01f)]
        private float shotDuration = 0.16f;

        [D0PlannerSection("命中反馈")]
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

        public Color RestingColor => restingColor;
        public Color AimingColor => aimingColor;
        public Color ShotColor => shotColor;
        public Color HitColor => hitColor;
        public float BaseRadius => baseRadius;
        public float RingThickness => ringThickness;
        public float AimingGlowAlpha => aimingGlowAlpha;
        public float ShotRadius => shotRadius;
        public float ShotDuration => shotDuration;
        public float HitMarkerRadius => hitMarkerRadius;
        public float HitMarkerThickness => hitMarkerThickness;
        public float HitMarkerArcDegrees => hitMarkerArcDegrees;
        public float HitExpansion => hitExpansion;
        public float HitDuration => hitDuration;

        public bool TryValidate(out string error)
        {
            if (!IsVisible(restingColor) || !IsVisible(aimingColor)
                || !IsVisible(shotColor) || !IsVisible(hitColor))
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

            if (!IsFinite(aimingGlowAlpha)
                || aimingGlowAlpha < 0f || aimingGlowAlpha > 1f)
            {
                error = "Aim indicator aiming glow alpha must be between zero and one.";
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
