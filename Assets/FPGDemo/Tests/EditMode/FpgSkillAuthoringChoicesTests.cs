using System;
using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Editor.SkillAuthoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillAuthoringChoicesTests
    {
        private const string FeiPrefabPath =
            "Assets/FPGDemo/Presentation/Characters/Fei/Spine/"
            + "D0_Fei_30048_StraightAlpha.prefab";

        private static readonly HashSet<string> FormalFallbackAnimations =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "idle",
                "attack",
                "attack_play1",
                "attack_play2",
                "normal_skill1",
                "normal_skill2",
                "u1_buff_play",
                "u4_attack_ready",
                "u4_attack_end",
                "defense_play",
                "die&broken"
            };

        [Test]
        public void CueAndWarningChoicesDoNotOfferEmptyValues()
        {
            List<FpgSkillAuthoringChoice> cues =
                FpgSkillAuthoringChoices.BuildCueChoices(string.Empty);
            List<FpgSkillAuthoringChoice> warnings =
                FpgSkillAuthoringChoices.BuildWarningChoices(string.Empty);

            Assert.That(cues, Is.Not.Empty);
            Assert.That(warnings, Is.Not.Empty);
            Assert.That(
                cues.All(choice =>
                    !string.IsNullOrWhiteSpace(choice.Value)),
                Is.True);
            Assert.That(
                warnings.All(choice =>
                    !string.IsNullOrWhiteSpace(choice.Value)),
                Is.True);
        }

        [Test]
        public void PayloadChoiceLabelUsesDisplayNameAndKindWithoutStableId()
        {
            const string StableId = "payload.internal.primary-shot";
            List<FpgSkillPayloadRecord> payloads =
                new List<FpgSkillPayloadRecord>
                {
                    new FpgSkillPayloadRecord
                    {
                        Index = 0,
                        Id = StableId,
                        Name = "Primary Shot",
                        Kind = "PelletRay"
                    }
                };

            List<FpgSkillAuthoringChoice> choices =
                FpgSkillAuthoringChoices.BuildPayloadChoices(
                    payloads,
                    StableId);
            FpgSkillAuthoringChoice choice = choices.Single();

            Assert.That(choice.Value, Is.EqualTo(StableId));
            Assert.That(choice.Label, Is.EqualTo("Primary Shot · PelletRay"));
            Assert.That(choice.Label, Does.Not.Contain(StableId));
        }

        [Test]
        public void AnimationChoicesIncludeFormalFallbackAndMissingCurrentValue()
        {
            const string MissingAnimation = "animation.test.missing";
            List<FpgSkillAuthoringChoice> choices =
                FpgSkillAuthoringChoices.BuildAnimationChoices(
                    null,
                    new[] { MissingAnimation });

            Assert.That(
                choices.Any(choice =>
                    choice.Value == "attack_play1"),
                Is.True);
            Assert.That(
                choices.Any(choice =>
                    choice.Value == MissingAnimation),
                Is.True);
        }

        [Test]
        public void AnimationChoicesIncludeAnimationsReadFromFeiPrefab()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(FeiPrefabPath);
            Assert.That(prefab, Is.Not.Null, FeiPrefabPath);

            Spine.Unity.SkeletonAnimation skeleton =
                prefab.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
            Assert.That(skeleton, Is.Not.Null);
            Assert.That(skeleton.SkeletonDataAsset, Is.Not.Null);

            Spine.SkeletonData skeletonData =
                skeleton.SkeletonDataAsset.GetSkeletonData(true);
            Assert.That(skeletonData, Is.Not.Null);
            string dynamicAnimation = null;
            Spine.ExposedList<Spine.Animation> animations =
                skeletonData.Animations;
            for (int index = 0; index < animations.Count; index++)
            {
                string candidate = animations.Items[index].Name;
                if (!FormalFallbackAnimations.Contains(candidate))
                {
                    dynamicAnimation = candidate;
                    break;
                }
            }

            Assert.That(
                dynamicAnimation,
                Is.Not.Null.And.Not.Empty,
                "Fei 测试 Prefab 需要至少一个正式兜底表之外的动画。");

            List<FpgSkillAuthoringChoice> choices =
                FpgSkillAuthoringChoices.BuildAnimationChoices(
                    prefab,
                    Array.Empty<string>());
            Assert.That(
                choices.Any(choice =>
                    string.Equals(
                        choice.Value,
                        dynamicAnimation,
                        StringComparison.Ordinal)),
                Is.True,
                dynamicAnimation);
        }

        [Test]
        public void AnimationChoicesTreatFeiPrefabAsAuthority()
        {
            const string MissingAnimation = "normal_skill2";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(FeiPrefabPath);
            Assert.That(prefab, Is.Not.Null, FeiPrefabPath);

            Spine.Unity.SkeletonAnimation skeleton =
                prefab.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(
                    true);
            Assert.That(skeleton, Is.Not.Null);
            Assert.That(skeleton.SkeletonDataAsset, Is.Not.Null);

            Spine.SkeletonData skeletonData =
                skeleton.SkeletonDataAsset.GetSkeletonData(true);
            Assert.That(skeletonData, Is.Not.Null);
            Assert.That(
                skeletonData.FindAnimation(MissingAnimation),
                Is.Null,
                "测试前提变化：Fei Prefab 已包含 normal_skill2。");

            List<FpgSkillAuthoringChoice> normalChoices =
                FpgSkillAuthoringChoices.BuildAnimationChoices(
                    prefab,
                    Array.Empty<string>());
            Assert.That(
                normalChoices.Any(choice =>
                    string.Equals(
                        choice.Value,
                        MissingAnimation,
                        StringComparison.Ordinal)),
                Is.False,
                "Prefab 可读取时不应混入全局兜底动画。");

            List<FpgSkillAuthoringChoice> repairChoices =
                FpgSkillAuthoringChoices.BuildAnimationChoices(
                    prefab,
                    new[] { MissingAnimation });
            FpgSkillAuthoringChoice missingChoice = repairChoices.Single(
                choice => string.Equals(
                    choice.Value,
                    MissingAnimation,
                    StringComparison.Ordinal));
            Assert.That(missingChoice.Label, Does.Contain("当前动画"));
            Assert.That(
                missingChoice.Label,
                Does.Contain("Prefab 中不存在"));
            Assert.That(
                missingChoice.Label,
                Is.Not.EqualTo(MissingAnimation));
        }

    }
}
