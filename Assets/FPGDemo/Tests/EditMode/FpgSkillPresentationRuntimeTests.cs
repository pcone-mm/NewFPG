using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillPresentationRuntimeTests
    {
        [Test]
        public void CommitCacheRetainsSuccessUntilExecutionRelease()
        {
            FpgSkillPresentationCommitCache cache =
                new FpgSkillPresentationCommitCache(2);
            SkillExecutionId first = new SkillExecutionId(11L);
            SkillExecutionId second = new SkillExecutionId(12L);

            Assert.That(cache.TryRecordSuccess(first, 101), Is.True);
            Assert.That(cache.TryRecordSuccess(first, 101), Is.True);
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.WasSuccessful(first, 101), Is.True);
            Assert.That(cache.WasSuccessful(first, 102), Is.False);
            Assert.That(cache.TryRecordSuccess(second, 201), Is.True);
            Assert.That(
                cache.TryRecordSuccess(new SkillExecutionId(13L), 301),
                Is.False);

            cache.ReleaseExecution(first);

            Assert.That(cache.WasSuccessful(first, 101), Is.False);
            Assert.That(cache.WasSuccessful(second, 201), Is.True);
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(
                cache.TryRecordSuccess(new SkillExecutionId(13L), 301),
                Is.True);
        }

        [Test]
        public void AudioPoolPrewarmsFixedTwoDimensionalSources()
        {
            GameObject owner = new GameObject("SkillAudioPoolTest");
            AudioClip clip = AudioClip.Create("SkillAudio", 32, 1, 8000, false);
            FpgSkillAudioSourcePool pool = new FpgSkillAudioSourcePool();
            try
            {
                Assert.That(
                    pool.TryPrepare(owner.transform, 3, out string error),
                    Is.True,
                    error);
                Assert.That(pool.Capacity, Is.EqualTo(3));

                AudioSource[] sources = owner.GetComponentsInChildren<AudioSource>(true);
                Assert.That(sources, Has.Length.EqualTo(3));
                for (int index = 0; index < sources.Length; index++)
                {
                    Assert.That(sources[index].spatialBlend, Is.Zero);
                    Assert.That(sources[index].playOnAwake, Is.False);
                }

                Assert.That(pool.TryPlay(clip, 0.5f), Is.True);
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TrajectoryViewUsesExactEndpointsAndRejectsMissingContract()
        {
            GameObject root = new GameObject("TrajectoryViewTest");
            GameObject invalidRoot = new GameObject("InvalidTrajectoryViewTest");
            try
            {
                LineRenderer line = root.AddComponent<LineRenderer>();
                FpgTrajectoryVfxView view =
                    root.AddComponent<FpgTrajectoryVfxView>();
                SetPrivateField(view, "lineRenderer", line);

                Vector3 start = new Vector3(1f, 2f, 3f);
                Vector3 end = new Vector3(7f, 5f, -2f);
                Assert.That(
                    view.TryActivate(
                        start,
                        end,
                        0.2f,
                        Vector3.one,
                        Vector3.zero,
                        out string error),
                    Is.True,
                    error);
                Assert.That(line.GetPosition(0), Is.EqualTo(start));
                Assert.That(line.GetPosition(1), Is.EqualTo(end));

                FpgTrajectoryVfxView invalid =
                    invalidRoot.AddComponent<FpgTrajectoryVfxView>();
                Assert.That(invalid.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(invalidRoot);
            }
        }

        [Test]
        public void SkillValidationRejectsTrajectoryPrefabWithoutValidRootView()
        {
            const string PrimaryPath =
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset";
            FpgPlayerSkillDefinition source =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    PrimaryPath);
            Assert.That(source, Is.Not.Null, PrimaryPath);
            FpgPlayerSkillDefinition clone = Object.Instantiate(source);
            GameObject invalidRoot = new GameObject("InvalidTrajectoryPrefab");
            try
            {
                FpgSkillSequenceDefinition execute = null;
                for (int index = 0; index < clone.Sequences.Count; index++)
                {
                    if (clone.Sequences[index].Kind
                        == FpgSkillSequenceKind.Execute)
                    {
                        execute = clone.Sequences[index];
                        break;
                    }
                }

                Assert.That(execute, Is.Not.Null);
                Assert.That(execute.AttackEvents, Is.Not.Empty);
                FpgVfxPresentationDefinition trajectory =
                    execute.AttackEvents[0].TrajectoryPresentation;
                Assert.That(trajectory, Is.Not.Null);
                SetPrivateField(trajectory, "prefab", invalidRoot);

                Assert.That(clone.TryValidate(out string missingViewError),
                    Is.False);
                StringAssert.Contains(
                    nameof(FpgTrajectoryVfxView),
                    missingViewError);

                invalidRoot.AddComponent<FpgTrajectoryVfxView>();
                Assert.That(clone.TryValidate(out string invalidViewError),
                    Is.False);
                StringAssert.Contains("invalid trajectory Prefab",
                    invalidViewError);
            }
            finally
            {
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(invalidRoot);
            }
        }

        [Test]
        public void RegistryDistributesOneGlobalVfxBudgetAcrossHandles()
        {
            string[] paths =
            {
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset",
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary.asset",
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Reload.asset",
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack.asset",
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_Volley.asset",
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_HeavyBreak.asset",
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Attack.asset",
                "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Attack_Summon.asset"
            };
            FpgSkillPresentationRegistry registry =
                new FpgSkillPresentationRegistry();
            for (int index = 0; index < paths.Length; index++)
            {
                FpgSkillTimelineDefinition skill =
                    AssetDatabase.LoadAssetAtPath<FpgSkillTimelineDefinition>(
                        paths[index]);
                Assert.That(skill, Is.Not.Null, paths[index]);
                Assert.That(
                    registry.TryRegister(skill, out string error),
                    Is.True,
                    error);
            }

            const int globalCapacity = 48;
            List<D0CombatVfxAssetReference> references =
                new List<D0CombatVfxAssetReference>();
            Assert.That(registry.TryCollectVfxReferences(
                references,
                globalCapacity,
                out string collectError), Is.True, collectError);
            Assert.That(references.Count, Is.GreaterThan(0));
            int total = 0;
            for (int index = 0; index < references.Count; index++)
            {
                Assert.That(references[index].PrewarmCapacity,
                    Is.GreaterThan(0));
                total += references[index].PrewarmCapacity;
            }

            Assert.That(total, Is.EqualTo(globalCapacity));

            int vfxHandleCount = references.Count;
            references.Clear();
            Assert.That(registry.TryCollectVfxReferences(
                references,
                vfxHandleCount - 1,
                out _), Is.False);
        }

        [Test]
        public void ImpactConsumerGapReleasesStaleCorrelationBindings()
        {
            GameObject owner = new GameObject("ImpactConsumerGapTest");
            try
            {
                FpgSkillPresentationWorld world =
                    owner.AddComponent<FpgSkillPresentationWorld>();
                SetPrivateField(world, "prepared", true);
                FixedFpgSkillImpactPresentationStream stream =
                    new FixedFpgSkillImpactPresentationStream(1);
                FpgSkillImpactPresentationConsumer consumer =
                    new FpgSkillImpactPresentationConsumer();
                Assert.That(consumer.TryPrepare(
                    stream,
                    world,
                    1,
                    out string prepareError), Is.True, prepareError);

                FpgSkillImpactCorrelation first =
                    new FpgSkillImpactCorrelation(
                        new RuntimeId(1L),
                        new SkillExecutionId(1L),
                        101);
                FpgSkillImpactCorrelation second =
                    new FpgSkillImpactCorrelation(
                        new RuntimeId(2L),
                        new SkillExecutionId(2L),
                        202);
                FpgCompiledImpactPresentation presentation =
                    new FpgCompiledImpactPresentation(
                        default(FpgPresentationHandle),
                        new FpgPresentationHandle(1),
                        default(FpgPresentationHandle),
                        default(FpgPresentationHandle),
                        default(FpgPresentationHandle),
                        default(FpgPresentationHandle),
                        1UL);
                Assert.That(consumer.TryRegister(
                    first,
                    FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                    presentation), Is.True);

                Assert.That(stream.TryRecordGroupCompletion(
                    new FpgSkillImpactGroupCompletion(
                        first,
                        FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                        new TickIndex(1L),
                        new AttackId(1L))), Is.True);
                Assert.That(stream.TryRecordGroupCompletion(
                    new FpgSkillImpactGroupCompletion(
                        first,
                        FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                        new TickIndex(2L),
                        new AttackId(1L))), Is.True);

                consumer.Consume();

                Assert.That(consumer.GapCount, Is.EqualTo(1));
                Assert.That(consumer.TryRegister(
                    second,
                    FpgSkillImpactPresentationGroupKind.ImmediateAttack,
                    presentation), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void SetPrivateField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
