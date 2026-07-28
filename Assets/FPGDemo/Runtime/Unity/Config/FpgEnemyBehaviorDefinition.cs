using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public struct FpgAnimationRootMotionRule
    {
        [SerializeField]
        private string animationName;

        [SerializeField]
        private bool enabled;

        public FpgAnimationRootMotionRule(string animationName, bool enabled)
        {
            this.animationName = animationName ?? string.Empty;
            this.enabled = enabled;
        }

        public string AnimationName => animationName ?? string.Empty;
        public bool Enabled => enabled;
    }

    /// <summary>
    /// Formal behavior data. Navigation is intentionally outside this asset;
    /// the v1 room supplies authored positions and the runtime owns movement queries.
    /// This type owns the stable MonoScript GUID referenced by behavior assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FpgEnemyBehaviorDefinition",
        menuName = "FPG Demo/Formal Encounter/Enemy Behavior")]
    public sealed class FpgEnemyBehaviorDefinition : ScriptableObject
    {
        [D0PlannerSection("Identity")]
        [D0PlannerField("Behavior ID", "Stable behavior identity for diagnostics and replay logs.")]
        [SerializeField]
        private string behaviorId = "behavior";

        [D0PlannerField("Display Name", "Authoring-only display name.")]
        [SerializeField]
        private string displayName = "Behavior";

        [D0PlannerSection("Movement")]
        [D0PlannerField("Mode", "Movement policy selected by the formal runtime.")]
        [SerializeField]
        private FpgEnemyBehaviorMode mode = FpgEnemyBehaviorMode.FixedPosition;

        [D0PlannerField("Entry Speed", "World units per second used while entering the room.")]
        [SerializeField, Min(0f)]
        private float entrySpeed = 3f;

        [D0PlannerField("Move Speed", "World units per second used after activation.")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 1.5f;

        [D0PlannerField("Stop During Attack", "When enabled, attack windup and recovery hold the gameplay anchor.")]
        [SerializeField]
        private bool stopDuringAttack = true;

        [D0PlannerField("Entry Animation", "Presentation key played during the warning/entry phase.")]
        [SerializeField]
        private string entryAnimation = "enter";

        [D0PlannerField("Idle Animation", "Presentation key used while active and not attacking.")]
        [SerializeField]
        private string idleAnimation = "idle";

        [D0PlannerField("Death Animation", "Presentation key used after the combatant dies.")]
        [SerializeField]
        private string deathAnimation = "death";

        [D0PlannerSection("Animation Root Motion")]
        [D0PlannerField(
            "Animation Rules",
            "Root motion is disabled when an animation has no rule. Animation names must be unique.")]
        [SerializeField]
        private FpgAnimationRootMotionRule[] animationRootMotionRules =
            Array.Empty<FpgAnimationRootMotionRule>();

        public string BehaviorId => behaviorId;
        public string DisplayName => displayName;
        public FpgEnemyBehaviorMode Mode => mode;
        public float EntrySpeed => entrySpeed;
        public float MoveSpeed => moveSpeed;
        public bool StopDuringAttack => stopDuringAttack;
        public string EntryAnimation => entryAnimation;
        public string IdleAnimation => idleAnimation;
        public string DeathAnimation => deathAnimation;
        public IReadOnlyList<FpgAnimationRootMotionRule>
            AnimationRootMotionRules =>
                animationRootMotionRules
                    ?? Array.Empty<FpgAnimationRootMotionRule>();
        public int AnimationRootMotionRuleCount =>
            animationRootMotionRules == null
                ? 0
                : animationRootMotionRules.Length;

        public FpgAnimationRootMotionRule GetAnimationRootMotionRule(int index)
        {
            if (animationRootMotionRules == null
                || index < 0
                || index >= animationRootMotionRules.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return animationRootMotionRules[index];
        }

        public bool UsesAnimationRootMotion(string animationName)
        {
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return false;
            }

            FpgAnimationRootMotionRule[] rules =
                animationRootMotionRules
                    ?? Array.Empty<FpgAnimationRootMotionRule>();
            for (int index = 0; index < rules.Length; index++)
            {
                if (string.Equals(
                        rules[index].AnimationName,
                        animationName,
                        StringComparison.Ordinal))
                {
                    return rules[index].Enabled;
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(behaviorId)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(entryAnimation)
                || string.IsNullOrWhiteSpace(idleAnimation)
                || string.IsNullOrWhiteSpace(deathAnimation))
            {
                error = "Formal behavior requires identity and animation keys.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FpgEnemyBehaviorMode), mode)
                || !IsFiniteNonNegative(entrySpeed)
                || !IsFiniteNonNegative(moveSpeed))
            {
                error = "Formal behavior '" + behaviorId
                    + "' has invalid movement values.";
                return false;
            }

            HashSet<string> animationNames =
                new HashSet<string>(StringComparer.Ordinal);
            FpgAnimationRootMotionRule[] rules =
                animationRootMotionRules
                    ?? Array.Empty<FpgAnimationRootMotionRule>();
            for (int index = 0; index < rules.Length; index++)
            {
                string animationName = rules[index].AnimationName;
                if (string.IsNullOrWhiteSpace(animationName))
                {
                    error = "Formal behavior '" + behaviorId
                        + $"' root-motion rule {index} requires an animation name.";
                    return false;
                }

                if (!animationNames.Add(animationName))
                {
                    error = "Formal behavior '" + behaviorId
                        + "' repeats root-motion animation '"
                        + animationName + "'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
        }
    }
}
