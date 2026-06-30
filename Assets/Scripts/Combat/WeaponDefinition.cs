using UnityEngine;
using NewFPG.Combat.SkillIndicators;
using NewFPG.Forging;

namespace NewFPG.Combat
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "NewFPG/Combat/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Flying Sword";
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0f)] private float resourceCost = 3f;
        [SerializeField, Min(0f)] private float damage = 35f;
        [SerializeField, Min(0.05f)] private float cooldown = 0.4f;
        [SerializeField, Min(0.1f)] private float range = 8f;
        [SerializeField, Min(0.05f)] private float radius = 0.55f;
        [SerializeField] private SkillIndicatorConfig indicatorConfig;
        [SerializeField] private GameObject releaseEffectPrefab;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private ForgedWeaponRuntimeStats forgedRuntimeStats;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public float ResourceCost => resourceCost;
        public float Damage => damage;
        public float Cooldown => cooldown;
        public float Range => range;
        public float Radius => radius;
        public SkillIndicatorConfig IndicatorConfig => indicatorConfig;
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
            if (!string.IsNullOrWhiteSpace(nextDisplayName))
            {
                displayName = nextDisplayName;
            }

            if (nextIcon != null)
            {
                icon = nextIcon;
            }

            resourceCost = Mathf.Max(0f, nextResourceCost);
            damage = Mathf.Max(0f, nextBaseDamage);
            cooldown = Mathf.Max(0.05f, nextCooldown);
            range = Mathf.Max(0.1f, nextRange);
            radius = Mathf.Max(0.05f, nextRadius);
            indicatorConfig = nextIndicatorConfig;
            releaseEffectPrefab = nextReleaseEffectPrefab;
            hitEffectPrefab = nextHitEffectPrefab;
            forgedRuntimeStats = nextForgedStats;
        }
    }
}
