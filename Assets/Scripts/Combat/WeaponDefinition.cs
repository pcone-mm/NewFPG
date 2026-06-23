using UnityEngine;
using NewFPG.Combat.SkillIndicators;

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
    }
}
