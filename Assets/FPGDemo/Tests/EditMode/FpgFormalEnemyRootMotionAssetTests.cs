using FPG.Demo.Core;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using Spine;
using Spine.Unity;
using Spine.Unity.AnimationTools;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgFormalEnemyRootMotionAssetTests
    {
        private const string HudieBehaviorPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Behavior.asset";
        private const string HudieDefinitionPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Enemy.asset";
        private const float PositionTolerance = 0.0001f;
        private const float RotationTolerance = 0.01f;

        private static readonly string[] FormalEnemyPrefabPaths =
        {
            "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_BurstbugEntity.prefab",
            "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_LuanEntity.prefab",
            "Assets/FPGDemo/Presentation/FormalEncounter/PF_FPG_HudieEntity.prefab"
        };

        [Test]
        public void BehaviorRejectsDuplicateRootMotionAnimationNames()
        {
            FpgEnemyBehaviorDefinition behavior =
                ScriptableObject.CreateInstance<FpgEnemyBehaviorDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(behavior);
                SerializedProperty rules = serialized.FindProperty(
                    "animationRootMotionRules");
                Assert.That(rules, Is.Not.Null);

                rules.arraySize = 2;
                SetRule(rules, 0, "appear", true);
                SetRule(rules, 1, "appear", false);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(behavior.TryValidate(out string error), Is.False);
                Assert.That(
                    error,
                    Does.Contain("repeats root-motion animation 'appear'"));
            }
            finally
            {
                Object.DestroyImmediate(behavior);
            }
        }

        [Test]
        public void FormalEnemyPrefabsUseRootBoneTrackZeroBridge()
        {
            for (int index = 0;
                index < FormalEnemyPrefabPaths.Length;
                index++)
            {
                string path = FormalEnemyPrefabPaths[index];
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);

                Transform visualRoot = prefab.transform.Find("VisualRoot");
                Assert.That(visualRoot, Is.Not.Null, path);
                Assert.That(
                    visualRoot.GetComponent<SkeletonAnimation>(),
                    Is.Not.Null,
                    path);

                FpgEntitySkeletonRootMotionBridge[] bridges =
                    visualRoot.GetComponents<
                        FpgEntitySkeletonRootMotionBridge>();
                Assert.That(bridges, Has.Length.EqualTo(1), path);

                FpgEntitySkeletonRootMotionBridge bridge = bridges[0];
                Assert.That(bridge.RootMotionBoneName, Is.EqualTo("root"), path);
                Assert.That(bridge.transformPositionX, Is.True, path);
                Assert.That(bridge.transformPositionY, Is.True, path);
                Assert.That(bridge.animationTrackFlags, Is.EqualTo(1), path);
                Assert.That(bridge.rigidBody, Is.Null, path);
                Assert.That(bridge.rigidBody2D, Is.Null, path);
            }
        }

        [Test]
        public void HudieEnablesOnlyAppearAndHasValidMotionTimeline()
        {
            FpgEnemyBehaviorDefinition behavior =
                AssetDatabase.LoadAssetAtPath<
                    FpgEnemyBehaviorDefinition>(HudieBehaviorPath);
            Assert.That(behavior, Is.Not.Null, HudieBehaviorPath);
            Assert.That(behavior.TryValidate(out string error), Is.True, error);
            Assert.That(behavior.AnimationRootMotionRuleCount, Is.EqualTo(1));
            Assert.That(behavior.UsesAnimationRootMotion("appear"), Is.True);
            Assert.That(behavior.UsesAnimationRootMotion("idle"), Is.False);
            Assert.That(behavior.UsesAnimationRootMotion("attack"), Is.False);
            Assert.That(behavior.UsesAnimationRootMotion("die"), Is.False);

            GameObject hudiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FormalEnemyPrefabPaths[2]);
            SkeletonAnimation skeleton = hudiePrefab
                .transform
                .Find("VisualRoot")
                .GetComponent<SkeletonAnimation>();
            SkeletonData data = skeleton.SkeletonDataAsset.GetSkeletonData(true);
            FpgEntitySkeletonRootMotionBridge bridge = skeleton.GetComponent<
                FpgEntitySkeletonRootMotionBridge>();

            Assert.That(data, Is.Not.Null);
            Assert.That(bridge, Is.Not.Null);
            Assert.That(
                bridge.TryValidateConfiguration(data, behavior, out error),
                Is.True,
                error);
        }

        [Test]
        public void BridgeRejectsEnabledAnimationAtOneTickDuration()
        {
            FpgEnemyBehaviorDefinition behavior =
                AssetDatabase.LoadAssetAtPath<FpgEnemyBehaviorDefinition>(
                    HudieBehaviorPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FormalEnemyPrefabPaths[2]);
            SkeletonAnimation skeletonAnimation = prefab.transform
                .Find("VisualRoot")
                .GetComponent<SkeletonAnimation>();
            SkeletonData data = skeletonAnimation.SkeletonDataAsset
                .GetSkeletonData(true);
            FpgEntitySkeletonRootMotionBridge bridge = skeletonAnimation
                .GetComponent<FpgEntitySkeletonRootMotionBridge>();
            Spine.Animation appear = data.FindAnimation(
                behavior.EntryAnimation);
            float authoredDuration = appear.Duration;
            try
            {
                appear.Duration = 1f / FpgSkillRuntimeConstants.TickRate;

                Assert.That(
                    bridge.TryValidateConfiguration(
                        data,
                        behavior,
                        out string error),
                    Is.False);
                Assert.That(error, Does.Contain("60 Hz"));
            }
            finally
            {
                appear.Duration = authoredDuration;
            }
        }

        [Test]
        public void HudieAppearMovesEntityAndUnbindResetsPresentation()
        {
            LoadHudieAssets(
                out FpgEnemyDefinition definition,
                out GameObject prefab);
            GameObject instance = InstantiateHudie(prefab);
            try
            {
                FpgEnemyEntityView view =
                    instance.GetComponent<FpgEnemyEntityView>();
                SkeletonAnimation skeletonAnimation = view.SkeletonAnimation;
                Transform visualRoot = skeletonAnimation.transform;
                FpgEntitySkeletonRootMotionBridge bridge =
                    visualRoot.GetComponent<FpgEntitySkeletonRootMotionBridge>();

                instance.transform.SetPositionAndRotation(
                    new Vector3(7f, -3f, 2f),
                    Quaternion.Euler(0f, 27f, 0f));
                Vector3 initialEntityPosition = instance.transform.position;
                Vector3 authoredVisualPosition = visualRoot.localPosition;
                Quaternion authoredVisualRotation = visualRoot.localRotation;
                Vector3 authoredVisualScale = visualRoot.localScale;
                float authoredTimeScale = skeletonAnimation.timeScale;
                Vector3 gameplayLocalPosition = instance.transform
                    .InverseTransformPoint(view.GameplayAnchor.position);
                Quaternion gameplayLocalRotation = RelativeRotation(
                    instance.transform,
                    view.GameplayAnchor);
                Vector3 projectileLocalPosition = instance.transform
                    .InverseTransformPoint(view.ProjectileAnchor.position);
                Quaternion projectileLocalRotation = RelativeRotation(
                    instance.transform,
                    view.ProjectileAnchor);

                Assert.That(
                    view.TryBindFormalRuntime(
                        new RuntimeId(101L),
                        0,
                        definition,
                        out string error),
                    Is.True,
                    error);

                Spine.Animation appear = skeletonAnimation.Skeleton.Data
                    .FindAnimation(definition.Behavior.EntryAnimation);
                TranslateTimeline timeline = appear
                    .FindTranslateTimelineForBone(
                        skeletonAnimation.Skeleton.Data.FindBoneIndex(
                            bridge.RootMotionBoneName));
                Vector3 bindMotion = ToWorldMotion(
                    timeline.Evaluate(0f) - timeline.Evaluate(-1f),
                    skeletonAnimation,
                    visualRoot);
                AssertVector3(
                    initialEntityPosition + bindMotion,
                    instance.transform.position,
                    "Binding must extract the authored t=0 displacement once.");

                int completionTick = Mathf.CeilToInt(
                    appear.Duration * FpgSkillRuntimeConstants.TickRate);
                for (int tick = 0; tick <= completionTick; tick++)
                {
                    Assert.That(
                        view.AdvanceFormalMotion(new TickIndex(tick)).IsSuccess,
                        Is.True,
                        view.LastRootMotionError);
                }

                Vector2 timelineMotion =
                    timeline.Evaluate(0f) - timeline.Evaluate(-1f)
                    + timeline.Evaluate(appear.Duration) - timeline.Evaluate(0f);
                Vector3 expectedWorldMotion = ToWorldMotion(
                    timelineMotion,
                    skeletonAnimation,
                    visualRoot);
                AssertVector3(
                    initialEntityPosition + expectedWorldMotion,
                    instance.transform.position,
                    "Entity must receive the complete official root motion.");
                AssertVector3(
                    authoredVisualPosition,
                    visualRoot.localPosition,
                    "VisualRoot local position must not drift.");
                AssertRelativePose(
                    instance.transform,
                    view.GameplayAnchor,
                    gameplayLocalPosition,
                    gameplayLocalRotation,
                    "GameplayAnchor");
                AssertRelativePose(
                    instance.transform,
                    view.ProjectileAnchor,
                    projectileLocalPosition,
                    projectileLocalRotation,
                    "ProjectileAnchor");

                Vector3 accumulatedEntityPosition = instance.transform.position;
                view.UnbindFormalRuntime();

                AssertVector3(
                    accumulatedEntityPosition,
                    instance.transform.position,
                    "Unbind must preserve accumulated Entity motion.");
                Assert.That(bridge.MotionEnabled, Is.False);
                Assert.That(bridge.animationTrackFlags, Is.Zero);
                Assert.That(
                    skeletonAnimation.timeScale,
                    Is.EqualTo(authoredTimeScale).Within(PositionTolerance));
                AssertVector3(authoredVisualPosition, visualRoot.localPosition);
                AssertQuaternion(authoredVisualRotation, visualRoot.localRotation);
                AssertVector3(authoredVisualScale, visualRoot.localScale);
                Assert.That(
                    skeletonAnimation.AnimationState.GetCurrent(0),
                    Is.Null,
                    "Unbind must clear the Spine track.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HudieAppearMotionIgnoresExtraRenderUpdatesBetweenTicks()
        {
            LoadHudieAssets(
                out FpgEnemyDefinition definition,
                out GameObject prefab);
            GameObject baseline = InstantiateHudie(prefab);
            GameObject renderUpdated = InstantiateHudie(prefab);
            try
            {
                FpgEnemyEntityView baselineView =
                    baseline.GetComponent<FpgEnemyEntityView>();
                FpgEnemyEntityView renderUpdatedView =
                    renderUpdated.GetComponent<FpgEnemyEntityView>();
                Assert.That(
                    baselineView.TryBindFormalRuntime(
                        new RuntimeId(201L),
                        0,
                        definition,
                        out string baselineError),
                    Is.True,
                    baselineError);
                Assert.That(
                    renderUpdatedView.TryBindFormalRuntime(
                        new RuntimeId(202L),
                        1,
                        definition,
                        out string renderUpdatedError),
                    Is.True,
                    renderUpdatedError);

                Spine.Animation appear = baselineView.SkeletonAnimation
                    .Skeleton.Data.FindAnimation(
                        definition.Behavior.EntryAnimation);
                int completionTick = Mathf.CeilToInt(
                    appear.Duration * FpgSkillRuntimeConstants.TickRate);
                for (int tick = 0; tick <= completionTick; tick++)
                {
                    Assert.That(
                        baselineView.AdvanceFormalMotion(
                            new TickIndex(tick)).IsSuccess,
                        Is.True,
                        baselineView.LastRootMotionError);
                    Assert.That(
                        renderUpdatedView.AdvanceFormalMotion(
                            new TickIndex(tick)).IsSuccess,
                        Is.True,
                        renderUpdatedView.LastRootMotionError);
                    AssertVector3(
                        baseline.transform.position,
                        renderUpdated.transform.position,
                        $"Tick {tick} diverged before render updates.");

                    for (int update = 0; update < 3; update++)
                    {
                        renderUpdatedView.SkeletonAnimation.Update(1f / 144f);
                    }

                    AssertVector3(
                        baseline.transform.position,
                        renderUpdated.transform.position,
                        $"Tick {tick} diverged after render updates.");
                }
            }
            finally
            {
                baseline.GetComponent<FpgEnemyEntityView>()
                    .UnbindFormalRuntime();
                renderUpdated.GetComponent<FpgEnemyEntityView>()
                    .UnbindFormalRuntime();
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(renderUpdated);
            }
        }

        [TestCase(
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Behavior.asset")]
        [TestCase(
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Behavior.asset")]
        public void OtherFormalBehaviorsLeaveRootMotionDisabled(string path)
        {
            FpgEnemyBehaviorDefinition behavior =
                AssetDatabase.LoadAssetAtPath<
                    FpgEnemyBehaviorDefinition>(path);
            Assert.That(behavior, Is.Not.Null, path);
            Assert.That(behavior.TryValidate(out string error), Is.True, error);
            Assert.That(behavior.AnimationRootMotionRuleCount, Is.Zero, path);
            Assert.That(behavior.UsesAnimationRootMotion("appear"), Is.False);
            Assert.That(behavior.UsesAnimationRootMotion("idle"), Is.False);
            Assert.That(behavior.UsesAnimationRootMotion("attack"), Is.False);
            Assert.That(behavior.UsesAnimationRootMotion("die"), Is.False);
        }

        private static void SetRule(
            SerializedProperty rules,
            int index,
            string animationName,
            bool enabled)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(index);
            rule.FindPropertyRelative("animationName").stringValue =
                animationName;
            rule.FindPropertyRelative("enabled").boolValue = enabled;
        }

        private static void LoadHudieAssets(
            out FpgEnemyDefinition definition,
            out GameObject prefab)
        {
            definition = AssetDatabase.LoadAssetAtPath<FpgEnemyDefinition>(
                HudieDefinitionPath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FormalEnemyPrefabPaths[2]);
            Assert.That(definition, Is.Not.Null, HudieDefinitionPath);
            Assert.That(prefab, Is.Not.Null, FormalEnemyPrefabPaths[2]);
            Assert.That(definition.EntityPrefab, Is.EqualTo(prefab));
        }

        private static GameObject InstantiateHudie(GameObject prefab)
        {
            GameObject instance = Object.Instantiate(prefab);
            Assert.That(instance, Is.Not.Null);
            Assert.That(
                instance.GetComponent<FpgEnemyEntityView>(),
                Is.Not.Null);
            return instance;
        }

        private static Vector3 ToWorldMotion(
            Vector2 timelineMotion,
            SkeletonAnimation skeletonAnimation,
            Transform visualRoot)
        {
            FpgEntitySkeletonRootMotionBridge bridge =
                visualRoot.GetComponent<FpgEntitySkeletonRootMotionBridge>();
            Spine.Skeleton skeleton = skeletonAnimation.Skeleton;
            Bone motionBone = skeleton.FindBone(bridge.RootMotionBoneName);
            Vector2 totalScale = new Vector2(
                skeleton.ScaleX,
                skeleton.ScaleY);
            for (Bone parent = motionBone.Parent;
                parent != null;
                parent = parent.Parent)
            {
                totalScale.x *= parent.ScaleX;
                totalScale.y *= parent.ScaleY;
            }

            timelineMotion.Scale(totalScale);
            Vector2 crossTranslation = new Vector2(
                bridge.rootMotionTranslateXPerY * timelineMotion.y,
                bridge.rootMotionTranslateYPerX * timelineMotion.x);
            timelineMotion.x = timelineMotion.x * bridge.rootMotionScaleX
                + crossTranslation.x;
            timelineMotion.y = timelineMotion.y * bridge.rootMotionScaleY
                + crossTranslation.y;
            if (!bridge.transformPositionX)
            {
                timelineMotion.x = 0f;
            }

            if (!bridge.transformPositionY)
            {
                timelineMotion.y = 0f;
            }

            return visualRoot.TransformVector(
                new Vector3(timelineMotion.x, timelineMotion.y, 0f));
        }

        private static Quaternion RelativeRotation(
            Transform entity,
            Transform anchor)
        {
            return Quaternion.Inverse(entity.rotation) * anchor.rotation;
        }

        private static void AssertRelativePose(
            Transform entity,
            Transform anchor,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string label)
        {
            AssertVector3(
                expectedPosition,
                entity.InverseTransformPoint(anchor.position),
                label + " relative position changed.");
            AssertQuaternion(
                expectedRotation,
                RelativeRotation(entity, anchor),
                label + " relative rotation changed.");
        }

        private static void AssertVector3(
            Vector3 expected,
            Vector3 actual,
            string message = null)
        {
            Assert.That(
                Vector3.Distance(expected, actual),
                Is.LessThanOrEqualTo(PositionTolerance),
                message);
        }

        private static void AssertQuaternion(
            Quaternion expected,
            Quaternion actual,
            string message = null)
        {
            Assert.That(
                Quaternion.Angle(expected, actual),
                Is.LessThanOrEqualTo(RotationTolerance),
                message);
        }
    }
}
