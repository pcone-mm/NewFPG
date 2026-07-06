using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Combat
{
    [CreateAssetMenu(fileName = "SO_HTC_Default", menuName = "NewFPG/Combat/Hit Tip Catalog")]
    public sealed class HitTipCatalog : ScriptableObject
    {
        [SerializeField] private HitTipAnimationConfig defaultAnimation;
        [SerializeField] private List<HitTipStyleConfig> styles = new List<HitTipStyleConfig>();

        public HitTipAnimationConfig DefaultAnimation => defaultAnimation;
        public IReadOnlyList<HitTipStyleConfig> Styles => styles;

        public HitTipStyleConfig GetStyle(HitTipStyleId styleId)
        {
            HitTipStyleConfig fallback = null;
            for (int i = 0; i < styles.Count; i++)
            {
                HitTipStyleConfig style = styles[i];
                if (style == null)
                {
                    continue;
                }

                if (style.StyleId == styleId)
                {
                    return style;
                }

                if (fallback == null && style.StyleId == HitTipStyleId.Normal)
                {
                    fallback = style;
                }
            }

            return fallback != null ? fallback : styles.Count > 0 ? styles[0] : null;
        }

        public HitTipAnimationConfig GetAnimation(HitTipStyleConfig style, HitTipAnimationConfig overrideAnimation)
        {
            if (overrideAnimation != null)
            {
                return overrideAnimation;
            }

            if (style != null && style.Animation != null)
            {
                return style.Animation;
            }

            return defaultAnimation;
        }

        public void SetDefaultAnimation(HitTipAnimationConfig animation)
        {
            defaultAnimation = animation;
        }

        public void SetStyles(IEnumerable<HitTipStyleConfig> nextStyles)
        {
            styles.Clear();
            if (nextStyles == null)
            {
                return;
            }

            styles.AddRange(nextStyles);
            NormalizeStyles();
        }

        private void OnValidate()
        {
            NormalizeStyles();
        }

        private void NormalizeStyles()
        {
            if (styles == null)
            {
                styles = new List<HitTipStyleConfig>();
                return;
            }

            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i] != null)
                {
                    styles[i].Normalize();
                }
            }
        }
    }
}
