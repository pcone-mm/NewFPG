using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum D0CombatVfxCategory
    {
        EnemyAttack = 0,
        Summon = 1,
        ActorState = 2,
        SkillPresentation = 3
    }

    /// <summary>
    /// A logical VFX dependency discovered from a scenario definition.  A
    /// prefab is optional for procedural modules (for example the existing
    /// line/tracer renderer), but the key and capacity are always validated.
    /// </summary>
    [Serializable]
    public sealed class D0CombatVfxAssetReference
    {
        [SerializeField]
        private string key;

        [SerializeField]
        private GameObject prefab;

        [SerializeField, Min(1)]
        private int prewarmCapacity = 1;

        [SerializeField, Min(0.01f)]
        private float duration = 1f;

        [SerializeField]
        private string animationName = "animation";

        [SerializeField]
        private int sortingOrderOffset;

        [SerializeField]
        private D0CombatVfxCategory category = D0CombatVfxCategory.ActorState;

        public string Key => key;
        public GameObject Prefab => prefab;
        public int PrewarmCapacity => prewarmCapacity;
        public float Duration => duration;
        public string AnimationName => animationName;
        public int SortingOrderOffset => sortingOrderOffset;
        public D0CombatVfxCategory Category => category;

        public D0CombatVfxAssetReference()
        {
        }

        public D0CombatVfxAssetReference(
            string key,
            GameObject prefab,
            int prewarmCapacity,
            float duration,
            string animationName,
            int sortingOrderOffset,
            D0CombatVfxCategory category)
        {
            this.key = key;
            this.prefab = prefab;
            this.prewarmCapacity = prewarmCapacity;
            this.duration = duration;
            this.animationName = animationName;
            this.sortingOrderOffset = sortingOrderOffset;
            this.category = category;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(key)
                || prewarmCapacity <= 0
                || !IsFinitePositive(duration)
                || string.IsNullOrWhiteSpace(animationName))
            {
                error = "Combat VFX reference requires a stable key, positive capacity/duration and animation name.";
                return false;
            }

            if (!Enum.IsDefined(typeof(D0CombatVfxCategory), category))
            {
                error = "Combat VFX reference category is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
