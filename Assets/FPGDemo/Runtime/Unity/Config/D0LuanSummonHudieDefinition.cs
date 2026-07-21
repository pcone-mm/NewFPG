using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Planner-authored summon contract for Luan's Hudie companion. This is
    /// configuration only: spawning, lifetime, hitboxes and combat ownership
    /// remain the responsibility of the battle runtime.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0LuanSummonHudieDefinition",
        menuName = "FPG Demo/Config/D0 Luan Summon Hudie Definition")]
    public sealed class D0LuanSummonHudieDefinition : ScriptableObject
    {
        private const float TicksPerSecond = 60f;
        [D0PlannerSection("召唤目标")]
        [D0PlannerField("蝴蝶敌人定义", "陆鸾召唤的蝴蝶使用的独立敌人定义。其生命、韧性、角色表现与行为均由该资产维护；本资产不复制这些数据。")]
        [SerializeField]
        private D0EnemyDefinition hudieEnemy;

        [D0PlannerSection("召唤时序")]
        [D0PlannerField("召唤延迟（秒）", "战斗开始后到陆鸾开始召唤表现的时长。单位为秒；运行时应以战斗 Tick 换算，暂停期间不推进。")]
        [SerializeField, Min(0f)]
        private float summonDelaySeconds = 4f;

        [D0PlannerField("蝴蝶出现延迟（秒）", "从陆鸾开始召唤表现到蝴蝶出现的时长。单位为秒；它只定义召唤时序，不决定投射物释放或伤害。")]
        [SerializeField, Min(0f)]
        private float appearanceDelaySeconds = 0.7333f;

        [D0PlannerSection("出生动画位移")]
        [D0PlannerField("出生动画位移", "启用后，蝴蝶出生表现会采样指定动画与骨骼的美术位移，并叠加到实体位置。")]
        [SerializeField]
        private D0AnimationMotionSettings appearanceAnimationMotion =
            new(false, "appear", "gameplay_motion", true);

        [D0PlannerSection("Summon-owned presentation")]
        [D0PlannerField("Luan summon animation", "State animation played by Luan when summon starts.")]
        [SerializeField]
        private string summonAnimation = "die&broken";

        [D0PlannerField("Hudie appearance animation", "One-shot animation played when Hudie becomes visible.")]
        [SerializeField]
        private string appearanceAnimation = "appear";

        [D0PlannerField("Luan summon VFX key", "Stable key for summon-state VFX. Concrete views are prewarmed by CombatVfxWorld.")]
        [SerializeField]
        private string summonVfxKey = "luan.summon";

        [SerializeField]
        private GameObject summonVfxPrefab;

        [SerializeField, Min(1)]
        private int summonVfxPrewarmCapacity = 1;

        [SerializeField, Min(0.01f)]
        private float summonVfxDuration = 1f;

        [D0PlannerField("Hudie appearance VFX key", "Stable key for Hudie appearance VFX.")]
        [SerializeField]
        private string appearanceVfxKey = "hudie.appear";

        [SerializeField]
        private GameObject appearanceVfxPrefab;

        [SerializeField, Min(1)]
        private int appearanceVfxPrewarmCapacity = 1;

        [SerializeField, Min(0.01f)]
        private float appearanceVfxDuration = 1f;

        [D0PlannerField("Luan summon Socket", "Stable Entity Prefab socket used by the summon VFX. It must resolve on Luan's socket registry.")]
        [SerializeField]
        private string summonSocketId = D0ActorSocketRegistry.DefaultAttackOriginId;

        [D0PlannerField("Hudie appearance Socket", "Stable Entity Prefab socket used by the appearance VFX. It must resolve on Hudie's socket registry.")]
        [SerializeField]
        private string appearanceSocketId = D0ActorSocketRegistry.DefaultAttackOriginId;

        [D0PlannerField("Summon audio cue", "Executable cue played through CombatAudioPresenter's fixed voice pool.")]
        [SerializeField]
        private CombatAudioCue summonAudioCue = CombatAudioCue.EnemyBreak;

        [D0PlannerField("Appearance audio cue", "Executable cue played through CombatAudioPresenter's fixed voice pool.")]
        [SerializeField]
        private CombatAudioCue appearanceAudioCue =
            CombatAudioCue.EnemyFastThreatTelegraph;

        [SerializeField]
        private D0AnimationMotionSettings summonAnimationMotion =
            new(false, "die&broken", "gameplay_motion", true);

        public D0EnemyDefinition HudieEnemy => hudieEnemy;
        public float SummonDelaySeconds => summonDelaySeconds;
        public float AppearanceDelaySeconds => appearanceDelaySeconds;
        public int SummonTick => SecondsToTick(summonDelaySeconds);
        public int AppearanceTick => SecondsToTick(
            summonDelaySeconds + appearanceDelaySeconds);
        public D0AnimationMotionSettings AppearanceAnimationMotion => appearanceAnimationMotion;
        public D0AnimationMotionSettings SummonAnimationMotion => summonAnimationMotion;
        public string SummonAnimation => string.IsNullOrWhiteSpace(summonAnimation)
            ? "die&broken"
            : summonAnimation;
        public string AppearanceAnimation => string.IsNullOrWhiteSpace(appearanceAnimation)
            ? "appear"
            : appearanceAnimation;
        public string SummonVfxKey => string.IsNullOrWhiteSpace(summonVfxKey)
            ? "luan.summon"
            : summonVfxKey;
        public GameObject SummonVfxPrefab => summonVfxPrefab;
        public int SummonVfxPrewarmCapacity => Mathf.Max(1, summonVfxPrewarmCapacity);
        public float SummonVfxDuration => Mathf.Max(0.01f, summonVfxDuration);
        public string AppearanceVfxKey => string.IsNullOrWhiteSpace(appearanceVfxKey)
            ? "hudie.appear"
            : appearanceVfxKey;
        public GameObject AppearanceVfxPrefab => appearanceVfxPrefab;
        public int AppearanceVfxPrewarmCapacity => Mathf.Max(1, appearanceVfxPrewarmCapacity);
        public float AppearanceVfxDuration => Mathf.Max(0.01f, appearanceVfxDuration);
        public string SummonSocketId => string.IsNullOrWhiteSpace(summonSocketId)
            ? D0ActorSocketRegistry.DefaultAttackOriginId
            : summonSocketId;
        public string AppearanceSocketId => string.IsNullOrWhiteSpace(appearanceSocketId)
            ? D0ActorSocketRegistry.DefaultAttackOriginId
            : appearanceSocketId;
        public CombatAudioCue SummonAudioCue => summonAudioCue;
        public CombatAudioCue AppearanceAudioCue => appearanceAudioCue;

        public void CollectPresentationVfxReferences(
            List<D0CombatVfxAssetReference> target)
        {
            if (target == null)
            {
                return;
            }

            target.Add(new D0CombatVfxAssetReference(
                SummonVfxKey,
                SummonVfxPrefab,
                SummonVfxPrewarmCapacity,
                SummonVfxDuration,
                "animation",
                0,
                D0CombatVfxCategory.Summon));
            target.Add(new D0CombatVfxAssetReference(
                AppearanceVfxKey,
                AppearanceVfxPrefab,
                AppearanceVfxPrewarmCapacity,
                AppearanceVfxDuration,
                "animation",
                0,
                D0CombatVfxCategory.Summon));
        }

        public bool TryValidate(out string error)
        {
            if (hudieEnemy == null)
            {
                error = "Luan summon definition requires a Hudie enemy definition.";
                return false;
            }

            if (!IsFiniteNonNegative(summonDelaySeconds)
                || !IsFiniteNonNegative(appearanceDelaySeconds))
            {
                error = "Luan summon definition contains invalid timing values.";
                return false;
            }

            if (!appearanceAnimationMotion.TryValidate(out error))
            {
                error = "Luan summon appearance animation motion is invalid: " + error;
                return false;
            }

            if (!summonAnimationMotion.TryValidate(out error))
            {
                error = "Luan summon animation motion is invalid: " + error;
                return false;
            }

            if (string.IsNullOrWhiteSpace(SummonAnimation)
                || string.IsNullOrWhiteSpace(AppearanceAnimation)
                || string.IsNullOrWhiteSpace(SummonVfxKey)
                || string.IsNullOrWhiteSpace(AppearanceVfxKey)
                || string.Equals(SummonVfxKey, AppearanceVfxKey, System.StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(SummonSocketId)
                || string.IsNullOrWhiteSpace(AppearanceSocketId)
                || summonVfxPrefab == null
                || appearanceVfxPrefab == null
                || summonAudioCue <= CombatAudioCue.None
                || summonAudioCue >= CombatAudioCue.Count
                || appearanceAudioCue <= CombatAudioCue.None
                || appearanceAudioCue >= CombatAudioCue.Count
                || summonVfxPrewarmCapacity <= 0
                || appearanceVfxPrewarmCapacity <= 0
                || !IsFinitePositive(summonVfxDuration)
                || !IsFinitePositive(appearanceVfxDuration))
            {
                error = "Luan summon presentation requires animations, distinct concrete VFX pools, resolvable socket IDs, playable audio cues and finite pool values.";
                return false;
            }

            if (summonAnimationMotion.Enabled
                && !string.Equals(
                    summonAnimationMotion.AnimationName,
                    SummonAnimation,
                    System.StringComparison.Ordinal))
            {
                error = "Luan summon animation motion must sample the configured summon animation.";
                return false;
            }

            if (appearanceAnimationMotion.Enabled
                && !string.Equals(
                    appearanceAnimationMotion.AnimationName,
                    AppearanceAnimation,
                    System.StringComparison.Ordinal))
            {
                error = "Hudie appearance animation motion must sample the configured appearance animation.";
                return false;
            }

            if (!hudieEnemy.TryValidate(out error))
            {
                error = "Luan summon Hudie definition is invalid: " + error;
                return false;
            }

            if (hudieEnemy.EntityPrefab == null
                || !hudieEnemy.EntityPrefab.TryResolveSocket(
                    AppearanceSocketId,
                    out _))
            {
                error = $"Hudie entity prefab cannot resolve appearance socket '{AppearanceSocketId}'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int SecondsToTick(float seconds)
        {
            return Mathf.Max(0, Mathf.CeilToInt(seconds * TicksPerSecond));
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
