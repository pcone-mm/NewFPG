using System.Reflection;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    /// <summary>
    /// Guards the D0 production-line contract: the standard sample must resolve
    /// one 3C profile, one behavior profile and three reusable attack assets.
    /// </summary>
    public sealed class D0ProductionLineProfileTests
    {
        private const string ScenarioPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/CombatLab/D0_CombatLab_FeiVsBurstbug.asset";

        private const string ThreeCPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Fei/D0_Fei_3C.asset";

        private const string BehaviorPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Behavior.asset";
        private const string LuanEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Luan/Prefabs/PF_D0_LuanEntity.prefab";

        private const string HudieEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Hudie/Prefabs/PF_D0_HudieEntity.prefab";

        private const string FastPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Fast.asset";

        private const string VolleyPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_Volley.asset";

        private const string HeavyPath =
            "Assets/FPGDemo/Config/D0Slice/Definitions/Burstbug/D0_Burstbug_Attack_HeavyBreak.asset";

        [Test]
        public void FeiThreeCProfileDefinesFixed2Point5DCompositionAndAimContract()
        {
            D0ThreeCProfile profile = LoadRequired<D0ThreeCProfile>(ThreeCPath);

            Assert.That(profile.TryValidate(out string error), Is.True, error);
            Assert.That(profile.FixedPlayerViewportAnchor.y, Is.LessThan(0.5f));
            Assert.That(profile.ReticleSafeViewport.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(profile.ReticleSafeViewport.xMax, Is.LessThanOrEqualTo(1f));
            Assert.That(profile.MaximumAimDistance, Is.EqualTo(50f));
            Assert.That(profile.InputBufferTicks, Is.EqualTo(4));

            Vector2 clamped = CombatAimViewportMath.ClampToSafeArea(
                new Vector2(-5f, 8f),
                profile.ReticleSafeViewport);
            Assert.That(clamped.x, Is.EqualTo(profile.ReticleSafeViewport.xMin));
            Assert.That(clamped.y, Is.EqualTo(profile.ReticleSafeViewport.yMax));
        }

        [Test]
        public void ThreeCProfileValidatesNonDefaultCameraInstallationValues()
        {
            D0ThreeCProfile clone = Object.Instantiate(
                LoadRequired<D0ThreeCProfile>(ThreeCPath));
            try
            {
                Vector3 expectedPivotPosition = new Vector3(1.25f, 3.4f, -7.2f);
                Vector3 expectedPivotEuler = new Vector3(-8.5f, 16f, 0f);
                Vector3 expectedCameraPosition = new Vector3(0.15f, -0.2f, 0.35f);
                Vector3 expectedCameraEuler = new Vector3(2f, -3f, 1f);
                const float expectedFieldOfView = 61f;
                const float expectedNearClipPlane = 0.35f;
                const float expectedFarClipPlane = 132.7f;

                SerializedObject serialized = new SerializedObject(clone);
                RequireProperty(serialized, "cameraPivotLocalPosition").vector3Value =
                    expectedPivotPosition;
                RequireProperty(serialized, "cameraPivotLocalEulerAngles").vector3Value =
                    expectedPivotEuler;
                RequireProperty(serialized, "cameraLocalPosition").vector3Value =
                    expectedCameraPosition;
                RequireProperty(serialized, "cameraLocalEulerAngles").vector3Value =
                    expectedCameraEuler;
                RequireProperty(serialized, "cameraFieldOfView").floatValue = expectedFieldOfView;
                RequireProperty(serialized, "cameraNearClipPlane").floatValue = expectedNearClipPlane;
                RequireProperty(serialized, "cameraFarClipPlane").floatValue = expectedFarClipPlane;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string validError), Is.True, validError);
                Assert.That(clone.CameraPivotLocalPosition, Is.EqualTo(expectedPivotPosition));
                Assert.That(clone.CameraPivotLocalEulerAngles, Is.EqualTo(expectedPivotEuler));
                Assert.That(clone.CameraLocalPosition, Is.EqualTo(expectedCameraPosition));
                Assert.That(clone.CameraLocalEulerAngles, Is.EqualTo(expectedCameraEuler));
                Assert.That(clone.CameraFieldOfView, Is.EqualTo(expectedFieldOfView).Within(0.001f));
                Assert.That(clone.CameraNearClipPlane, Is.EqualTo(expectedNearClipPlane).Within(0.001f));
                Assert.That(clone.CameraFarClipPlane, Is.EqualTo(expectedFarClipPlane).Within(0.001f));

                RequireProperty(serialized, "cameraFarClipPlane").floatValue = expectedNearClipPlane;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(clone.TryValidate(out string invalidError), Is.False);
                Assert.That(invalidError, Does.Contain("camera"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void BurstbugBehaviorProfileDefinesEntryPatrolAttackStopAndDeathExit()
        {
            D0EnemyBehaviorProfile profile = LoadRequired<D0EnemyBehaviorProfile>(BehaviorPath);

            Assert.That(profile.TryValidate(out string error), Is.True, error);
            Assert.That(profile.EntryOffset.x, Is.GreaterThan(profile.PatrolRightOffset.x));
            Assert.That(profile.PatrolLeftOffset.x, Is.LessThan(profile.PatrolRightOffset.x));
            Assert.That(profile.StopDuringThreat, Is.True);
            Assert.That(profile.ResumePatrolAfterRecovery, Is.True);
            Assert.That(profile.DeathExitOffset.x, Is.GreaterThan(profile.PatrolRightOffset.x));
        }

        [Test]
        public void PatrolStopsForEveryCommittedAttackPhaseIncludingRecovery()
        {
            Assert.That(D0EnemyBehaviorController.IsThreatBlockingPatrol(ThreatState.Scheduled), Is.False);
            Assert.That(D0EnemyBehaviorController.IsThreatBlockingPatrol(ThreatState.Telegraph), Is.True);
            Assert.That(D0EnemyBehaviorController.IsThreatBlockingPatrol(ThreatState.Windup), Is.True);
            Assert.That(D0EnemyBehaviorController.IsThreatBlockingPatrol(ThreatState.ReleaseCommitted), Is.True);
            Assert.That(D0EnemyBehaviorController.IsThreatBlockingPatrol(ThreatState.Recovery), Is.True);
            Assert.That(D0EnemyBehaviorController.IsThreatBlockingPatrol(ThreatState.Completed), Is.False);

            Vector3 entered = D0EnemyBehaviorController.MoveOffsetForTicks(
                new Vector3(7.5f, 0f, 0f),
                new Vector3(-1.5f, 0f, 0f),
                5f,
                60L);
            Assert.That(entered.x, Is.EqualTo(2.5f).Within(0.0001f));

            Vector3 unchangedDuringAttack = D0EnemyBehaviorController.MoveOffsetForTicks(
                entered,
                new Vector3(1.5f, 0f, 0f),
                1.4f,
                0L);
            Assert.That(unchangedDuringAttack, Is.EqualTo(entered));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CanceledAttackAnimationMotionFinalizesOnceWithoutChangingProgramMotion(
            bool persistEndOffset)
        {
            GameObject root = new GameObject("Canceled attack animation motion");
            try
            {
                D0EnemyBehaviorController controller =
                    root.AddComponent<D0EnemyBehaviorController>();
                RuntimeId activeThreatId = new RuntimeId(9001L);
                Vector3 programOffset = new Vector3(2f, -1f, 0f);
                Vector3 programSkillOffset = new Vector3(0.5f, 3f, 0f);
                Vector3 committedOffset = new Vector3(-4f, 2f, 0f);
                Vector3 activeOffset = new Vector3(7f, -5f, 0f);

                SetPrivateField(controller, "programMotionOffset", programOffset);
                SetPrivateField(controller, "programSkillMotionOffset", programSkillOffset);
                SetPrivateField(controller, "committedAnimationMotionOffset", committedOffset);
                SetPrivateField(controller, "activeAnimationMotionOffset", activeOffset);
                SetPrivateField(
                    controller,
                    "activeAnimationMotionSettings",
                    new D0AnimationMotionSettings(
                        true,
                        "attack",
                        "gameplay_motion",
                        persistEndOffset));
                SetPrivateField(controller, "activeAnimationMotionThreatId", activeThreatId);
                SetPrivateField(controller, "activeAnimationMotionStartTick", 120L);

                Assert.That(
                    InvokeCanceledAnimationMotionFinalizer(
                        controller,
                        CreateThreatSnapshot(new RuntimeId(9002L), ThreatState.Canceled)),
                    Is.False);
                Assert.That(
                    InvokeCanceledAnimationMotionFinalizer(
                        controller,
                        CreateThreatSnapshot(activeThreatId, ThreatState.Recovery)),
                    Is.False);
                Assert.That(controller.HasActiveAnimationMotion, Is.True);

                Assert.That(
                    InvokeCanceledAnimationMotionFinalizer(
                        controller,
                        CreateThreatSnapshot(activeThreatId, ThreatState.Canceled)),
                    Is.True);

                Vector3 expectedCommitted = persistEndOffset
                    ? committedOffset + activeOffset
                    : committedOffset;
                Assert.That(controller.HasActiveAnimationMotion, Is.False);
                Assert.That(controller.ProgramMotionOffset, Is.EqualTo(programOffset));
                Assert.That(controller.ProgramSkillMotionOffset, Is.EqualTo(programSkillOffset));
                Assert.That(controller.ActiveAnimationMotionOffset, Is.EqualTo(Vector3.zero));
                Assert.That(controller.CommittedAnimationMotionOffset, Is.EqualTo(expectedCommitted));
                Assert.That(
                    controller.CombinedMotionOffset,
                    Is.EqualTo(programOffset + programSkillOffset + expectedCommitted));

                Assert.That(
                    InvokeCanceledAnimationMotionFinalizer(
                        controller,
                        CreateThreatSnapshot(activeThreatId, ThreatState.Canceled)),
                    Is.False);
                Assert.That(controller.CommittedAnimationMotionOffset, Is.EqualTo(expectedCommitted));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(LuanEntityPrefabPath)]
        [TestCase(HudieEntityPrefabPath)]
        public void EntityPrefabRestoresItsOwnAuthoredVisualCalibration(string prefabPath)
        {
            GameObject instance = Object.Instantiate(LoadRequired<GameObject>(prefabPath));
            try
            {
                D0EnemyEntityView entity = instance.GetComponent<D0EnemyEntityView>();
                Assert.That(entity, Is.Not.Null);
                Assert.That(entity.VisualRoot, Is.Not.Null);

                Vector3 visualPosition = entity.VisualRoot.localPosition;
                Quaternion visualRotation = entity.VisualRoot.localRotation;
                Vector3 visualScale = entity.VisualRoot.localScale;
                entity.CaptureAuthoredLocalPose();

                entity.VisualRoot.localPosition += new Vector3(8f, -4f, 2f);
                entity.VisualRoot.localRotation = Quaternion.Euler(17f, 41f, 9f);
                entity.VisualRoot.localScale = Vector3.one * 3.7f;

                Assert.That(entity.RestoreAuthoredLocalPose(), Is.True);
                Assert.That(entity.VisualRoot.localPosition, Is.EqualTo(visualPosition));
                Assert.That(
                    Quaternion.Angle(entity.VisualRoot.localRotation, visualRotation),
                    Is.LessThan(0.001f));
                Assert.That(entity.VisualRoot.localScale, Is.EqualTo(visualScale));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void EnemyBehaviorRejectsAncestorLinkedVisualAndGameplayAnchors()
        {
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioPath);
            D0EnemyBehaviorProfile profile =
                LoadRequired<D0EnemyBehaviorProfile>(BehaviorPath);
            GameObject root = new GameObject("D0 enemy behavior hierarchy contract");
            GameObject visual = new GameObject("VisualRoot");
            GameObject gameplay = new GameObject("GameplayAnchor");

            try
            {
                visual.transform.SetParent(root.transform, false);
                gameplay.transform.SetParent(root.transform, false);
                BattleSessionHost host = root.AddComponent<BattleSessionHost>();
                D0EnemyBehaviorController controller =
                    root.AddComponent<D0EnemyBehaviorController>();
                controller.Configure(
                    host,
                    profile,
                    scenario.Encounter,
                    visual.transform,
                    gameplay.transform);

                Assert.That(controller.TryValidate(out string siblingError), Is.True, siblingError);

                visual.transform.SetParent(gameplay.transform, false);
                Assert.That(controller.TryValidate(out string visualChildError), Is.False);
                Assert.That(visualChildError, Does.Contain("independent transform branches"));

                visual.transform.SetParent(root.transform, false);
                gameplay.transform.SetParent(visual.transform, false);
                Assert.That(controller.TryValidate(out string gameplayChildError), Is.False);
                Assert.That(gameplayChildError, Does.Contain("independent transform branches"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StandardEncounterReusesThreeCompleteAttackDefinitions()
        {
            D0CombatScenarioDefinition scenario =
                LoadRequired<D0CombatScenarioDefinition>(ScenarioPath);
            D0EnemyAttackDefinition fast = LoadRequired<D0EnemyAttackDefinition>(FastPath);
            D0EnemyAttackDefinition volley = LoadRequired<D0EnemyAttackDefinition>(VolleyPath);
            D0EnemyAttackDefinition heavy = LoadRequired<D0EnemyAttackDefinition>(HeavyPath);

            Assert.That(fast.TryValidate(out string fastError), Is.True, fastError);
            Assert.That(volley.TryValidate(out string volleyError), Is.True, volleyError);
            Assert.That(heavy.TryValidate(out string heavyError), Is.True, heavyError);
            Assert.That(fast.AttackLanguage, Is.EqualTo(D0EnemyAttackLanguage.FastAttack));
            Assert.That(volley.AttackLanguage, Is.EqualTo(D0EnemyAttackLanguage.InterceptableVolley));
            Assert.That(heavy.AttackLanguage, Is.EqualTo(D0EnemyAttackLanguage.HeavyWeakpointBreak));

            D0EncounterDefinition encounter = scenario.Encounter;
            Assert.That(encounter.UsesReusableAttackDefinitions, Is.True);
            Assert.That(encounter.AttackScheduleCount, Is.EqualTo(6));
            Assert.That(encounter.GetAttackScheduleEntry(0).Attack, Is.SameAs(fast));
            Assert.That(encounter.GetAttackScheduleEntry(3).Attack, Is.SameAs(fast));
            Assert.That(encounter.GetAttackScheduleEntry(1).Attack, Is.SameAs(volley));
            Assert.That(encounter.GetAttackScheduleEntry(4).Attack, Is.SameAs(volley));
            Assert.That(encounter.GetAttackScheduleEntry(2).Attack, Is.SameAs(heavy));
            Assert.That(encounter.GetAttackScheduleEntry(5).Attack, Is.SameAs(heavy));

            Assert.That(fast.TryCreateScheduleEntry(1L, 120, out ThreatScheduleEntry fastEntry, out string fastEntryError),
                Is.True,
                fastEntryError);
            Assert.That(fastEntry.Payload.Kind, Is.EqualTo(ThreatPayloadKind.SweptProjectile));
            Assert.That(fastEntry.DefinitionId, Is.EqualTo(201));

            Assert.That(volley.TryCreateScheduleEntry(2L, 300, out ThreatScheduleEntry volleyEntry, out string volleyEntryError),
                Is.True,
                volleyEntryError);
            Assert.That(volleyEntry.Payload.ProjectileDefinition.Interceptable, Is.True);
            Assert.That(volleyEntry.Payload.PayloadCount, Is.EqualTo(3));

            Assert.That(heavy.TryCreateScheduleEntry(3L, 540, out ThreatScheduleEntry heavyEntry, out string heavyEntryError),
                Is.True,
                heavyEntryError);
            Assert.That(heavyEntry.Payload.Kind, Is.EqualTo(ThreatPayloadKind.TimedImpact));
            Assert.That(heavyEntry.Payload.TimedImpactDamage.BaseDamage, Is.EqualTo(120));
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"Required D0 production asset is missing: {path}");
            return asset;
        }

        private static ThreatSnapshot CreateThreatSnapshot(
            RuntimeId runtimeId,
            ThreatState state)
        {
            return new ThreatSnapshot(
                runtimeId,
                401,
                state,
                AttackId.Invalid,
                TickIndex.Invalid,
                false,
                state == ThreatState.Canceled || state == ThreatState.Completed);
        }

        private static bool InvokeCanceledAnimationMotionFinalizer(
            D0EnemyBehaviorController controller,
            ThreatSnapshot snapshot)
        {
            MethodInfo method = typeof(D0EnemyBehaviorController).GetMethod(
                "TryFinalizeCanceledAttackAnimationMotion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(controller, new object[] { snapshot });
        }

        private static void SetPrivateField<T>(
            object target,
            string fieldName,
            T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string propertyPath)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            Assert.That(property, Is.Not.Null, $"Missing serialized property '{propertyPath}'.");
            return property;
        }
    }
}
