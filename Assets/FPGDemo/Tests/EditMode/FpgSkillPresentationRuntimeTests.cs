using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillPresentationRuntimeTests
    {
        [Test]
        public void PresentationSourceContinuesAfterSubscriberFailure()
        {
            FpgFormalPlayerPresentationSource source =
                new FpgFormalPlayerPresentationSource();
            int deliveredSequence = 0;
            source.ActionCommitted += _ =>
                throw new InvalidOperationException("Expected test failure.");
            source.ActionCommitted += action =>
                deliveredSequence = (int)action.Sequence;

            Assert.DoesNotThrow(() => source.PublishAction(
                new TickIndex(1L),
                FpgFormalPlayerActionType.PrimaryReleaseCommitted,
                WeaponReleaseKind.Primary,
                new AttackId(2L),
                WeaponState.Ready,
                WeaponState.PrimaryRecovery,
                8,
                7));
            Assert.That(deliveredSequence, Is.EqualTo(1));
        }

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
        public void AudioPoolConfiguresWorldPositionedSourceWithLinearRolloff()
        {
            GameObject owner = new GameObject("SpatialSkillAudioPoolTest");
            AudioClip clip = AudioClip.Create("SpatialSkillAudio", 32, 1, 8000, false);
            FpgSkillAudioSourcePool pool = new FpgSkillAudioSourcePool();
            try
            {
                Assert.That(
                    pool.TryPrepare(owner.transform, 1, out string error),
                    Is.True,
                    error);
                Vector3 position = new Vector3(3f, 2f, -4f);
                Assert.That(
                    pool.TryPlay(
                        clip,
                        0.75f,
                        FpgAudioPresentationSpace.WorldPositioned,
                        position,
                        2f,
                        18f),
                    Is.True);

                AudioSource source =
                    owner.GetComponentInChildren<AudioSource>(true);
                Assert.That(source.spatialBlend, Is.EqualTo(1f));
                Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
                Assert.That(source.dopplerLevel, Is.Zero);
                Assert.That(source.minDistance, Is.EqualTo(2f));
                Assert.That(source.maxDistance, Is.EqualTo(18f));
                Assert.That(source.transform.position, Is.EqualTo(position));
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AudioPoolHeldLoopFollowsSourceAndReleasesForReuse()
        {
            GameObject owner = new GameObject("HeldSkillAudioPoolTest");
            AudioClip clip = AudioClip.Create(
                "HeldSkillAudio",
                128,
                1,
                8000,
                false);
            FpgSkillAudioSourcePool pool = new FpgSkillAudioSourcePool();
            try
            {
                Assert.That(
                    pool.TryPrepare(owner.transform, 1, out string error),
                    Is.True,
                    error);
                Vector3 initial = new Vector3(2f, 3f, -1f);
                Assert.That(
                    pool.TryBorrowHeld(
                        clip,
                        0.6f,
                        FpgAudioPresentationSpace.WorldPositioned,
                        initial,
                        1f,
                        12f,
                        out AudioSource held),
                    Is.True);
                Assert.That(held, Is.Not.Null);
                Assert.That(held.loop, Is.True);
                Assert.That(held.clip, Is.SameAs(clip));
                Assert.That(held.transform.position, Is.EqualTo(initial));

                Assert.That(
                    pool.TryBorrowHeld(
                        clip,
                        0.6f,
                        FpgAudioPresentationSpace.WorldPositioned,
                        initial,
                        1f,
                        12f,
                        out _),
                    Is.False,
                    "A held source must reserve its fixed-pool slot.");

                Vector3 moved = new Vector3(-4f, 1f, 6f);
                Assert.That(pool.TryUpdateHeld(held, moved), Is.True);
                Assert.That(held.transform.position, Is.EqualTo(moved));
                Assert.That(pool.TryReleaseHeld(held), Is.True);
                Assert.That(held.loop, Is.False);
                Assert.That(held.clip, Is.Null);

                Assert.That(
                    pool.TryBorrowHeld(
                        clip,
                        0.5f,
                        FpgAudioPresentationSpace.WorldPositioned,
                        initial,
                        1f,
                        12f,
                        out AudioSource reused),
                    Is.True);
                Assert.That(reused, Is.SameAs(held));
                pool.Clear();
                Assert.That(reused.loop, Is.False);
                Assert.That(reused.clip, Is.Null);
            }
            finally
            {
                pool.Dispose();
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AudioPresentationDefinitionValidatesSpatialAnchorAndHash()
        {
            AudioClip clip = AudioClip.Create("SpatialDefinition", 32, 1, 8000, false);
            FpgAudioPresentationDefinition definition =
                new FpgAudioPresentationDefinition();
            try
            {
                SetPrivateField(definition, "clip", clip);
                Assert.That(
                    definition.TryValidate(out string defaultError),
                    Is.True,
                    defaultError);
                Assert.That(
                    definition.PlaybackMode,
                    Is.EqualTo(FpgAudioPresentationPlaybackMode.OneShot));
                ulong twoDimensionalHash = definition.ComputeContentHash();

                SetPrivateField(
                    definition,
                    "space",
                    FpgAudioPresentationSpace.WorldPositioned);
                SetPrivateField(
                    definition,
                    "anchor",
                    FpgAudioPresentationAnchor.OwnerSocket);
                SetPrivateField(definition, "socketId", "weapon.primary.muzzle");
                SetPrivateField(definition, "minDistance", 2f);
                SetPrivateField(definition, "maxDistance", 18f);

                Assert.That(
                    definition.TryValidate(out string spatialError),
                    Is.True,
                    spatialError);
                Assert.That(
                    definition.ComputeContentHash(),
                    Is.Not.EqualTo(twoDimensionalHash));
                ulong spatialHash = definition.ComputeContentHash();

                SetPrivateField(
                    definition,
                    "playbackMode",
                    FpgAudioPresentationPlaybackMode.HeldLoop);
                Assert.That(
                    definition.TryValidate(out string heldError),
                    Is.True,
                    heldError);
                Assert.That(
                    definition.ComputeContentHash(),
                    Is.Not.EqualTo(spatialHash));

                SetPrivateField(
                    definition,
                    "space",
                    FpgAudioPresentationSpace.TwoDimensional);
                Assert.That(definition.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void AudioPresentationVariationsAffectHashAndRejectDuplicates()
        {
            AudioClip primary = AudioClip.Create(
                "AudioVariationPrimary",
                32,
                1,
                8000,
                false);
            AudioClip alternate = AudioClip.Create(
                "AudioVariationAlternate",
                32,
                1,
                8000,
                false);
            FpgAudioPresentationDefinition definition =
                new FpgAudioPresentationDefinition();
            try
            {
                SetPrivateField(definition, "clip", primary);
                Assert.That(definition.TryValidate(out _), Is.True);
                ulong singleClipHash = definition.ComputeContentHash();

                SetPrivateField(
                    definition,
                    "variations",
                    new[] { alternate });
                Assert.That(definition.TryValidate(out _), Is.True);
                Assert.That(definition.ClipCount, Is.EqualTo(2));
                Assert.That(definition.GetClip(0), Is.SameAs(primary));
                Assert.That(definition.GetClip(1), Is.SameAs(alternate));
                Assert.That(
                    definition.ComputeContentHash(),
                    Is.Not.EqualTo(singleClipHash));

                SetPrivateField(
                    definition,
                    "variations",
                    new[] { primary });
                Assert.That(definition.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(alternate);
                Object.DestroyImmediate(primary);
            }
        }

        [Test]
        public void AudioVariationSelectionNeverImmediatelyRepeats()
        {
            const int ClipCount = 4;
            for (int previous = 0; previous < ClipCount; previous++)
            {
                for (uint random = 0; random < 32u; random++)
                {
                    int selected = FpgAudioVariationSelection.SelectIndex(
                        ClipCount,
                        previous,
                        random);
                    Assert.That(selected, Is.InRange(0, ClipCount - 1));
                    Assert.That(selected, Is.Not.EqualTo(previous));
                }
            }

            Assert.That(
                FpgAudioVariationSelection.SelectIndex(1, 0, 123u),
                Is.Zero);
        }

        [Test]
        public void ImpactAudioResolutionUsesEnvironmentOverrideWithBaseFallback()
        {
            FpgPresentationHandle baseAudio = new FpgPresentationHandle(11);
            FpgPresentationHandle environmentAudio =
                new FpgPresentationHandle(12);
            FpgPresentationHandle weakpointAudio =
                new FpgPresentationHandle(13);
            FpgPresentationHandle interceptionAudio =
                new FpgPresentationHandle(14);
            FpgCompiledImpactPresentation presentation =
                new FpgCompiledImpactPresentation(
                    default(FpgPresentationHandle),
                    baseAudio,
                    environmentAudio,
                    interceptionAudio,
                    default(FpgPresentationHandle),
                    default(FpgPresentationHandle),
                    weakpointAudio,
                    default(FpgPresentationHandle),
                    1UL);

            Assert.That(
                presentation.ResolveAudio(false, false),
                Is.EqualTo(baseAudio));
            Assert.That(
                presentation.ResolveAudio(false, true),
                Is.EqualTo(environmentAudio));
            Assert.That(
                presentation.ResolveAudio(true, true),
                Is.EqualTo(weakpointAudio));
            Assert.That(
                presentation.ResolveInterceptionAudio(),
                Is.EqualTo(interceptionAudio));

            FpgCompiledImpactPresentation legacyPresentation =
                new FpgCompiledImpactPresentation(
                    default(FpgPresentationHandle),
                    baseAudio,
                    default(FpgPresentationHandle),
                    default(FpgPresentationHandle),
                    default(FpgPresentationHandle),
                    default(FpgPresentationHandle),
                    default(FpgPresentationHandle),
                    default(FpgPresentationHandle),
                    2UL);
            Assert.That(
                legacyPresentation.ResolveAudio(false, true),
                Is.EqualTo(baseAudio));
            Assert.That(
                legacyPresentation.ResolveInterceptionAudio(),
                Is.EqualTo(baseAudio));
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
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Immediate.asset",
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Charge.asset",
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
        public void PreparedWorldReusesOnlyTheSameSceneAndPreparationBindings()
        {
            const string skillPath =
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset";
            const string alternateSkillPath =
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Reload.asset";
            const string profilePath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset";
            GameObject worldOwner = new GameObject("RetainedSkillPresentationWorld");
            GameObject alternateOwner = new GameObject("AlternateSkillPresentationBindings");
            CombatPresentationProfile alternateProfile = null;
            FpgSkillTimelineDefinition mutableSkill = null;
            try
            {
                FpgSkillTimelineDefinition skill =
                    AssetDatabase.LoadAssetAtPath<FpgSkillTimelineDefinition>(
                        skillPath);
                FpgSkillTimelineDefinition alternateSkill =
                    AssetDatabase.LoadAssetAtPath<FpgSkillTimelineDefinition>(
                        alternateSkillPath);
                CombatPresentationProfile profile =
                    AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                        profilePath);
                Assert.That(skill, Is.Not.Null, skillPath);
                Assert.That(alternateSkill, Is.Not.Null, alternateSkillPath);
                Assert.That(profile, Is.Not.Null, profilePath);
                mutableSkill = Object.Instantiate(skill);

                D0CombatVfxWorld vfxWorld =
                    worldOwner.AddComponent<D0CombatVfxWorld>();
                FpgFormalPlayerCameraFeedback cameraFeedback =
                    worldOwner.AddComponent<FpgFormalPlayerCameraFeedback>();
                FpgSkillPresentationWorld world =
                    worldOwner.AddComponent<FpgSkillPresentationWorld>();
                Assert.That(
                    world.TryConfigure(
                        vfxWorld,
                        cameraFeedback,
                        out string configureError),
                    Is.True,
                    configureError);
                Assert.That(
                    world.TryPrepare(
                        new[] { mutableSkill },
                        profile,
                        out string prepareError),
                    Is.True,
                    prepareError);

                int registryCount = world.Registry.Count;
                int poolCount = vfxWorld.PoolCount;
                int prewarmedInstanceCount = vfxWorld.PrewarmedInstanceCount;
                int prepareInstantiateCount = vfxWorld.PrepareInstantiateCount;
                int audioSourceCount =
                    worldOwner.GetComponentsInChildren<AudioSource>(true).Length;
                Assert.That(registryCount, Is.GreaterThan(0));
                Assert.That(poolCount, Is.GreaterThan(0));
                Assert.That(prewarmedInstanceCount, Is.GreaterThan(0));
                Assert.That(audioSourceCount, Is.GreaterThan(0));

                world.ClearRuntimePresentation();
                Assert.That(world.IsPrepared, Is.True);
                Assert.That(
                    world.TryConfigure(
                        vfxWorld,
                        cameraFeedback,
                        out string retainedConfigureError),
                    Is.True,
                    retainedConfigureError);
                Assert.That(
                    world.TryPrepare(
                        new[] { mutableSkill },
                        profile,
                        out string retainedPrepareError),
                    Is.True,
                    retainedPrepareError);
                Assert.That(world.Registry.Count, Is.EqualTo(registryCount));
                Assert.That(vfxWorld.PoolCount, Is.EqualTo(poolCount));
                Assert.That(
                    vfxWorld.PrewarmedInstanceCount,
                    Is.EqualTo(prewarmedInstanceCount));
                Assert.That(
                    vfxWorld.PrepareInstantiateCount,
                    Is.EqualTo(prepareInstantiateCount));
                Assert.That(
                    worldOwner.GetComponentsInChildren<AudioSource>(true).Length,
                    Is.EqualTo(audioSourceCount));

                D0CombatVfxWorld alternateVfxWorld =
                    alternateOwner.AddComponent<D0CombatVfxWorld>();
                Assert.That(
                    world.TryConfigure(
                        alternateVfxWorld,
                        cameraFeedback,
                        out string vfxRebindError),
                    Is.False);
                StringAssert.Contains("cannot change", vfxRebindError);

                FpgFormalPlayerCameraFeedback alternateCameraFeedback =
                    alternateOwner.AddComponent<FpgFormalPlayerCameraFeedback>();
                Assert.That(
                    world.TryConfigure(
                        vfxWorld,
                        alternateCameraFeedback,
                        out string cameraRebindError),
                    Is.False);
                StringAssert.Contains("cannot change", cameraRebindError);
                Assert.That(world.VfxWorld, Is.SameAs(vfxWorld));
                Assert.That(
                    GetField(world, "cameraFeedback"),
                    Is.SameAs(cameraFeedback));
                Assert.That(alternateVfxWorld.IsPrepared, Is.False);

                alternateProfile = Object.Instantiate(profile);
                Assert.That(
                    world.TryPrepare(
                        new[] { mutableSkill },
                        alternateProfile,
                        out string profileChangeError),
                    Is.False);
                StringAssert.Contains(
                    "cannot change",
                    profileChangeError);
                Assert.That(
                    world.TryPrepare(
                        new[] { alternateSkill },
                        profile,
                        out string skillChangeError),
                    Is.False);
                StringAssert.Contains(
                    "cannot change",
                    skillChangeError);
                Assert.That(
                    world.TryPrepare(
                        null,
                        profile,
                        out string missingSkillsError),
                    Is.False);
                StringAssert.Contains(
                    "cannot change",
                    missingSkillsError);

                string originalSkillId = mutableSkill.SkillId;
                FieldInfo skillIdField =
                    typeof(FpgSkillTimelineDefinition).GetField(
                        "skillId",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(skillIdField, Is.Not.Null);
                skillIdField.SetValue(
                    mutableSkill,
                    originalSkillId + ".changed");
                Assert.That(
                    world.TryPrepare(
                        new[] { mutableSkill },
                        profile,
                        out string mutatedSkillError),
                    Is.False);
                StringAssert.Contains(
                    "cannot change",
                    mutatedSkillError);
                skillIdField.SetValue(mutableSkill, originalSkillId);

                Assert.That(world.Registry.Count, Is.EqualTo(registryCount));
                Assert.That(vfxWorld.PoolCount, Is.EqualTo(poolCount));
                Assert.That(
                    vfxWorld.PrepareInstantiateCount,
                    Is.EqualTo(prepareInstantiateCount));
            }
            finally
            {
                Object.DestroyImmediate(mutableSkill);
                Object.DestroyImmediate(alternateProfile);
                Object.DestroyImmediate(alternateOwner);
                Object.DestroyImmediate(worldOwner);
            }
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

        [Test]
        public void PostActionAnimationCancelKeepsCommittedFlightAndImpactAlive()
        {
            const string secondaryPath =
                "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Charge.asset";
            const string profilePath =
                "Assets/FPGDemo/Config/FormalEncounter/FPG_CombatPresentationProfile.asset";
            GameObject worldOwner = new GameObject("CommittedProjectileVfxWorld");
            GameObject bridgeOwner = new GameObject("CommittedProjectileBridge");
            GameObject actorOwner = new GameObject("CommittedProjectileActor");
            try
            {
                FpgPlayerSkillDefinition secondary =
                    AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                        secondaryPath);
                CombatPresentationProfile profile =
                    AssetDatabase.LoadAssetAtPath<CombatPresentationProfile>(
                        profilePath);
                Assert.That(secondary, Is.Not.Null, secondaryPath);
                Assert.That(profile, Is.Not.Null, profilePath);
                Assert.That(
                    secondary.TryCompile(
                        out FpgCompiledPlayerSkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                Assert.That(
                    compiled.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Release,
                        out FpgCompiledSkillSequence release),
                    Is.True);
                Assert.That(
                    compiled.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Cancel,
                        out FpgCompiledSkillSequence cancel),
                    Is.True);

                FpgCompiledSkillActionPresentation projectilePresentation =
                    default(FpgCompiledSkillActionPresentation);
                FpgCompiledSkillEvent projectileEvent =
                    default(FpgCompiledSkillEvent);
                for (int index = 0; index < release.ActionPresentations.Count;
                    index++)
                {
                    FpgCompiledSkillActionPresentation candidate =
                        release.ActionPresentations[index];
                    if (candidate.ActionKind
                        == FpgSkillActionKind.LaunchProjectile)
                    {
                        projectilePresentation = candidate;
                        break;
                    }
                }
                for (int index = 0; index < release.Events.Count; index++)
                {
                    FpgCompiledSkillEvent candidate = release.Events[index];
                    if (candidate.Kind == FpgSkillEventKind.GameplayAction
                        && candidate.ActionKind
                            == FpgSkillActionKind.LaunchProjectile)
                    {
                        projectileEvent = candidate;
                        break;
                    }
                }
                Assert.That(projectilePresentation.IsValid, Is.True);
                Assert.That(projectilePresentation.FlightVfx.IsValid, Is.True);
                Assert.That(projectilePresentation.Collision.HasAny, Is.True);
                Assert.That(projectileEvent.EventId, Is.GreaterThan(0));

                D0CombatVfxWorld vfxWorld =
                    worldOwner.AddComponent<D0CombatVfxWorld>();
                FpgSkillPresentationWorld presentationWorld =
                    worldOwner.AddComponent<FpgSkillPresentationWorld>();
                Assert.That(
                    presentationWorld.TryConfigure(
                        vfxWorld,
                        null,
                        out string configureError),
                    Is.True,
                    configureError);
                Assert.That(
                    presentationWorld.TryPrepare(
                        new FpgSkillTimelineDefinition[] { secondary },
                        profile,
                        out string prepareError),
                    Is.True,
                    prepareError);
                Assert.That(
                    presentationWorld.TryBorrowFlightVfx(
                        projectilePresentation.FlightVfx,
                        Vector3.zero,
                        Quaternion.identity,
                        out GameObject flightInstance),
                    Is.True);
                Assert.That(flightInstance, Is.Not.Null);
                Assert.That(flightInstance.activeSelf, Is.True);
                Assert.That(vfxWorld.ActiveInstanceCount, Is.EqualTo(1));

                FpgFormalPlayerPresentationBridge bridge =
                    bridgeOwner.AddComponent<FpgFormalPlayerPresentationBridge>();
                Actor2DPresenter actor =
                    actorOwner.AddComponent<Actor2DPresenter>();
                SetPrivateField(bridge, "actorPresenter", actor);
                SetPrivateField(bridge, "active", true);
                FieldInfo visualsField = typeof(FpgFormalPlayerPresentationBridge)
                    .GetField(
                        "playerProjectileVisuals",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(visualsField, Is.Not.Null);
                Type slotType = visualsField.FieldType.GetElementType();
                Assert.That(slotType, Is.Not.Null);
                Array visuals = Array.CreateInstance(slotType, 1);
                object slot = Activator.CreateInstance(slotType);
                SetField(slot, "IsUsed", true);
                SetField(slot, "Handle", projectilePresentation.FlightVfx);
                SetField(slot, "Instance", flightInstance);
                visuals.SetValue(slot, 0);
                visualsField.SetValue(bridge, visuals);

                FpgFormalPlayerSkillSequenceEvent canceledEnd =
                    CreateCanceledEndEvent(cancel);
                InvokePrivate(
                    bridge,
                    "HandleSkillSequenceAdvanced",
                    canceledEnd);
                InvokePrivate(bridge, "ConsumeSkillSequenceEvents");

                Array retainedVisuals = (Array)visualsField.GetValue(bridge);
                object retainedSlot = retainedVisuals.GetValue(0);
                Assert.That((bool)GetField(retainedSlot, "IsUsed"), Is.True);
                Assert.That(
                    GetField(retainedSlot, "Instance"),
                    Is.SameAs(flightInstance));
                Assert.That(flightInstance.activeSelf, Is.True);
                Assert.That(vfxWorld.ActiveInstanceCount, Is.EqualTo(1));

                FixedFpgSkillImpactPresentationStream impactStream =
                    new FixedFpgSkillImpactPresentationStream(4);
                FpgSkillImpactPresentationConsumer impactConsumer =
                    new FpgSkillImpactPresentationConsumer();
                Assert.That(
                    impactConsumer.TryPrepare(
                        impactStream,
                        presentationWorld,
                        1,
                        out string consumerError),
                    Is.True,
                    consumerError);
                FpgSkillImpactCorrelation correlation =
                    new FpgSkillImpactCorrelation(
                        new RuntimeId(1L),
                        new SkillExecutionId(42L),
                        projectileEvent.EventId);
                Assert.That(
                    impactConsumer.TryRegister(
                        correlation,
                        FpgSkillImpactPresentationGroupKind.Projectile,
                        projectilePresentation.Collision),
                    Is.True);
                Assert.That(
                    impactStream.TryRecordContact(
                        new FpgSkillImpactContact(
                            correlation,
                            FpgSkillImpactPresentationGroupKind.Projectile,
                            new TickIndex(60L),
                            new AttackId(700L),
                            new ProjectileId(701L),
                            new ImpactId(702L),
                            new RuntimeId(703L),
                            FpgSkillImpactContactKind.TargetImpact,
                            new SpatialVectorKey(1000, 2000, 0),
                            HitPart.Body,
                            0)),
                    Is.True);

                impactConsumer.Consume();

                Assert.That(impactConsumer.FaultCount, Is.Zero);
                Assert.That(vfxWorld.ActiveInstanceCount, Is.EqualTo(2));
                Assert.That(flightInstance.activeSelf, Is.True);
                Assert.That(
                    impactStream.TryRecordGroupCompletion(
                        new FpgSkillImpactGroupCompletion(
                            correlation,
                            FpgSkillImpactPresentationGroupKind.Projectile,
                            new TickIndex(60L),
                            new AttackId(700L))),
                    Is.True);
                impactConsumer.Consume();
                Assert.That(impactConsumer.FaultCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(bridgeOwner);
                Object.DestroyImmediate(actorOwner);
                Object.DestroyImmediate(worldOwner);
            }
        }

        private static FpgFormalPlayerSkillSequenceEvent
            CreateCanceledEndEvent(FpgCompiledSkillSequence cancel)
        {
            ConstructorInfo constructor = typeof(FpgPlayerSkillSequenceFrame)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(FpgPlayerSkillSlot),
                        typeof(FpgCompiledSkillSequence),
                        typeof(SkillExecutionId),
                        typeof(TickIndex),
                        typeof(TickIndex),
                        typeof(FpgSkillExecutionState)
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            FpgPlayerSkillSequenceFrame frame =
                (FpgPlayerSkillSequenceFrame)constructor.Invoke(
                    new object[]
                    {
                        FpgPlayerSkillSlot.Secondary,
                        cancel,
                        new SkillExecutionId(99L),
                        new TickIndex(53L),
                        new TickIndex(54L),
                        FpgSkillExecutionState.Canceled
                    });
            FpgFormalPlayerSkillSequenceEvent result =
                default(FpgFormalPlayerSkillSequenceEvent);
            FpgFormalPlayerPresentationSource source =
                new FpgFormalPlayerPresentationSource();
            source.SkillSequenceAdvanced += value => result = value;
            source.PublishSkillSequence(frame, "u4_attack_end");
            Assert.That(result.State, Is.EqualTo(FpgSkillExecutionState.Canceled));
            return result;
        }

        private static object InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(target);
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
