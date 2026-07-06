using UnityEngine;

namespace NewFPG.Combat
{
    public readonly struct HitTipRequest
    {
        private HitTipRequest(
            float amount,
            HitTipStyleId styleId,
            Vector3 worldPosition,
            Vector2 screenPosition,
            bool hasScreenPosition,
            bool hasColorOverride,
            Color colorOverride,
            HitTipAnimationConfig animationOverride)
        {
            Amount = Mathf.Max(0f, Mathf.Abs(amount));
            StyleId = styleId;
            WorldPosition = worldPosition;
            ScreenPosition = screenPosition;
            HasScreenPosition = hasScreenPosition;
            HasColorOverride = hasColorOverride;
            ColorOverride = colorOverride;
            AnimationOverride = animationOverride;
        }

        public HitTipRequest(float amount, Vector3 worldPosition, HitTipStyleId styleId = HitTipStyleId.Normal)
            : this(amount, styleId, worldPosition, Vector2.zero, false, false, Color.white, null)
        {
        }

        public float Amount { get; }
        public HitTipStyleId StyleId { get; }
        public Vector3 WorldPosition { get; }
        public Vector2 ScreenPosition { get; }
        public bool HasScreenPosition { get; }
        public bool HasColorOverride { get; }
        public Color ColorOverride { get; }
        public HitTipAnimationConfig AnimationOverride { get; }

        public static HitTipRequest FromScreen(
            float amount,
            Vector2 screenPosition,
            HitTipStyleId styleId = HitTipStyleId.Normal)
        {
            return new HitTipRequest(amount, styleId, Vector3.zero, screenPosition, true, false, Color.white, null);
        }

        public HitTipRequest WithColor(Color color)
        {
            return new HitTipRequest(
                Amount,
                StyleId,
                WorldPosition,
                ScreenPosition,
                HasScreenPosition,
                true,
                color,
                AnimationOverride);
        }

        public HitTipRequest WithAnimation(HitTipAnimationConfig animation)
        {
            return new HitTipRequest(
                Amount,
                StyleId,
                WorldPosition,
                ScreenPosition,
                HasScreenPosition,
                HasColorOverride,
                ColorOverride,
                animation);
        }
    }
}
