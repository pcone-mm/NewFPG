using System;
using UnityEngine;

namespace NewFPG.Combat
{
    [Serializable]
    public sealed class HitTipStyleConfig
    {
        [SerializeField] private HitTipStyleId styleId;
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];
        [SerializeField] private HitTipAnimationConfig animation;
        [SerializeField] private float digitSpacing = -2f;
        [SerializeField, Min(0f)] private float backgroundHorizontalPadding = 34f;
        [SerializeField] private Vector2 backgroundMinSize = new Vector2(133f, 50f);
        [SerializeField, Min(1f)] private float digitHeight = 60f;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Color highlightColor = new Color(1f, 0.95f, 0.65f, 1f);

        public HitTipStyleConfig()
        {
            EnsureDigitArray();
        }

        public HitTipStyleConfig(HitTipStyleId styleId, Sprite backgroundSprite, Sprite[] digitSprites)
        {
            this.styleId = styleId;
            this.backgroundSprite = backgroundSprite;
            this.digitSprites = digitSprites;
            EnsureDigitArray();
        }

        public HitTipStyleId StyleId => styleId;
        public Sprite BackgroundSprite => backgroundSprite;
        public HitTipAnimationConfig Animation => animation;
        public float DigitSpacing => digitSpacing;
        public float BackgroundHorizontalPadding => Mathf.Max(0f, backgroundHorizontalPadding);
        public Vector2 BackgroundMinSize => new Vector2(Mathf.Max(1f, backgroundMinSize.x), Mathf.Max(1f, backgroundMinSize.y));
        public float DigitHeight => Mathf.Max(1f, digitHeight);
        public Color BaseColor => baseColor;
        public Color HighlightColor => highlightColor;

        public bool IsValid
        {
            get
            {
                if (backgroundSprite == null || digitSprites == null || digitSprites.Length < 10)
                {
                    return false;
                }

                for (int i = 0; i < 10; i++)
                {
                    if (digitSprites[i] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Sprite GetDigitSprite(int digit)
        {
            EnsureDigitArray();
            return digit >= 0 && digit < digitSprites.Length ? digitSprites[digit] : null;
        }

        public void Configure(
            HitTipStyleId nextStyleId,
            Sprite nextBackgroundSprite,
            Sprite[] nextDigitSprites,
            HitTipAnimationConfig nextAnimation,
            float nextDigitSpacing,
            float nextBackgroundHorizontalPadding,
            Vector2 nextBackgroundMinSize,
            float nextDigitHeight,
            Color nextBaseColor,
            Color nextHighlightColor)
        {
            styleId = nextStyleId;
            backgroundSprite = nextBackgroundSprite;
            digitSprites = nextDigitSprites;
            animation = nextAnimation;
            digitSpacing = nextDigitSpacing;
            backgroundHorizontalPadding = Mathf.Max(0f, nextBackgroundHorizontalPadding);
            backgroundMinSize = new Vector2(Mathf.Max(1f, nextBackgroundMinSize.x), Mathf.Max(1f, nextBackgroundMinSize.y));
            digitHeight = Mathf.Max(1f, nextDigitHeight);
            baseColor = nextBaseColor;
            highlightColor = nextHighlightColor;
            EnsureDigitArray();
        }

        public void Normalize()
        {
            EnsureDigitArray();
            backgroundHorizontalPadding = Mathf.Max(0f, backgroundHorizontalPadding);
            backgroundMinSize = new Vector2(Mathf.Max(1f, backgroundMinSize.x), Mathf.Max(1f, backgroundMinSize.y));
            digitHeight = Mathf.Max(1f, digitHeight);
        }

        private void EnsureDigitArray()
        {
            if (digitSprites == null)
            {
                digitSprites = new Sprite[10];
                return;
            }

            if (digitSprites.Length == 10)
            {
                return;
            }

            Array.Resize(ref digitSprites, 10);
        }
    }
}
