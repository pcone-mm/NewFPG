using System;
using System.Collections.Generic;
using System.IO;
using FPG.Demo.Enemy;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgFormalSkillPresentationV3AssetTests
    {
        private const string PrimaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset";
        private const string ImmediateSecondaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Immediate.asset";
        private const string ChargeSecondaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Charge.asset";
        private const string ReloadPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Reload.asset";
        private const string BurstbugFastPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack.asset";
        private const string BurstbugVolleyPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_Volley.asset";
        private const string BurstbugHeavyPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_HeavyBreak.asset";
        private const string HudiePath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Attack.asset";
        private const string LuanPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Attack_Summon.asset";
        private const string FeiVfxRoot =
            "Assets/FPGDemo/Presentation/Characters/Players/Fei/VFX/";
        private const string FeiSecondaryChargePrefabPath =
            FeiVfxRoot + "PF_FPG_Fei_Secondary_Charge.prefab";
        private const string EnemyVfxRoot =
            "Assets/FPGDemo/Presentation/Characters/Enemies/Shared/VFX/";

        [Test]
        public void FormalSkillsArePureSchemaV3Assets()
        {
            FpgPlayerSkillDefinition[] playerSkills =
            {
                LoadRequired<FpgPlayerSkillDefinition>(PrimaryPath),
                LoadRequired<FpgPlayerSkillDefinition>(ImmediateSecondaryPath),
                LoadRequired<FpgPlayerSkillDefinition>(ChargeSecondaryPath),
                LoadRequired<FpgPlayerSkillDefinition>(ReloadPath)
            };
            FpgEnemyAttackDefinition[] enemySkills =
            {
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugFastPath),
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugVolleyPath),
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugHeavyPath),
                LoadRequired<FpgEnemyAttackDefinition>(HudiePath),
                LoadRequired<FpgEnemyAttackDefinition>(LuanPath)
            };

            for (int index = 0; index < playerSkills.Length; index++)
            {
                FpgPlayerSkillDefinition skill = playerSkills[index];
                AssertPureV3Authoring(skill);
                Assert.That(
                    skill.TryCompile(out _, out string error),
                    Is.True,
                    error);
            }

            for (int index = 0; index < enemySkills.Length; index++)
            {
                FpgEnemyAttackDefinition skill = enemySkills[index];
                AssertPureV3Authoring(skill);
                Assert.That(
                    skill.TryCompile(out _, out string error),
                    Is.True,
                    error);
            }
        }

        [Test]
        public void FeiPresentationLivesOnTypedTracksAndActionNodes()
        {
            FpgPlayerSkillDefinition primary =
                LoadRequired<FpgPlayerSkillDefinition>(PrimaryPath);
            FpgSkillSequenceDefinition primaryExecute =
                FindSequence(primary, FpgSkillSequenceKind.Execute);
            Assert.That(primaryExecute.ActivePresentationTracks.Count, Is.EqualTo(1));
            Assert.That(
                primaryExecute.ActivePresentationTracks[0].VfxEvents.Count,
                Is.EqualTo(1));
            FpgVfxPresentationEventDefinition primaryMuzzle =
                primaryExecute.ActivePresentationTracks[0].VfxEvents[0];
            Assert.That(primaryMuzzle.BoundGameplayEventId,
                Is.EqualTo("event.fei.primary.attack.0"));
            Assert.That(primaryMuzzle.Anchor,
                Is.EqualTo(FpgVfxPresentationAnchor.OwnerSocket));
            Assert.That(primaryMuzzle.OwnerSocketId,
                Is.EqualTo("weapon.primary.muzzle"));
            AssertAssetPath(
                primaryMuzzle.Presentation.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Primary_Muzzle.prefab");

            FpgSkillAttackEventDefinition primaryAttack =
                primaryExecute.AttackEvents[0];
            Assert.That(primaryAttack.TrajectoryPresentation, Is.Not.Null);
            Assert.That(primaryAttack.TrajectoryPresentation.Prefab, Is.Not.Null);
            Assert.That(
                primaryAttack.TrajectoryPresentation.Prefab
                    .GetComponent<FpgTrajectoryVfxView>(),
                Is.Not.Null);
            AssertAssetPath(
                primaryAttack.TrajectoryPresentation.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Primary_Trajectory.prefab");
            Assert.That(primaryAttack.ImpactPresentation, Is.Not.Null);
            Assert.That(primaryAttack.ImpactPresentation.BaseVfx, Is.Not.Null);
            AssertAssetPath(
                primaryAttack.ImpactPresentation.BaseVfx.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Primary_Hit.prefab");

            FpgPlayerSkillDefinition immediateSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ImmediateSecondaryPath);
            FpgPlayerSkillDefinition chargeSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ChargeSecondaryPath);
            FpgSkillSequenceDefinition chargeEnter =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.ChargeEnter);
            Assert.That(chargeEnter.ActivePresentationTracks.Count, Is.EqualTo(1));
            Assert.That(
                chargeEnter.ActivePresentationTracks[0].VfxEvents.Count,
                Is.EqualTo(1));
            Assert.That(
                chargeEnter.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.Empty);
            AssertAssetPath(
                chargeEnter.ActivePresentationTracks[0].VfxEvents[0]
                    .Presentation.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Secondary_Charge.prefab");
            Assert.That(
                chargeEnter.ActivePresentationTracks[0].VfxEvents[0]
                    .Presentation.Prefab
                    .GetComponent<ChargeProgressVfxDriver>(),
                Is.Not.Null);

            FpgSkillSequenceDefinition secondaryExecute =
                FindSequence(immediateSecondary, FpgSkillSequenceKind.Execute);
            Assert.That(
                secondaryExecute.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.execute.attack.0"));
            AssertAssetPath(
                secondaryExecute.ActivePresentationTracks[0].VfxEvents[0]
                    .Presentation.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Secondary_Muzzle.prefab");

            FpgSkillSequenceDefinition release =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Release);
            Assert.That(release.ActivePresentationTracks.Count, Is.EqualTo(1));
            Assert.That(
                release.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.release.attack.0"));
            AssertAssetPath(
                release.ActivePresentationTracks[0].VfxEvents[0]
                    .Presentation.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Secondary_Muzzle.prefab");
            FpgSkillProjectileEventDefinition projectile =
                release.ProjectileEvents[0];
            Assert.That(projectile.FlightVfx, Is.Not.Null);
            Assert.That(projectile.FlightVfx.Prefab, Is.Not.Null);
            AssertAssetPath(
                projectile.FlightVfx.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Secondary_Projectile.prefab");
            Assert.That(projectile.CollisionPresentation, Is.Not.Null);
            Assert.That(projectile.CollisionPresentation.BaseVfx, Is.Not.Null);
            AssertAssetPath(
                projectile.CollisionPresentation.BaseVfx.Prefab,
                FeiVfxRoot + "PF_FPG_Fei_Secondary_Hit.prefab");

            FpgPlayerSkillDefinition reload =
                LoadRequired<FpgPlayerSkillDefinition>(ReloadPath);
            FpgSkillReloadEventDefinition reloadCommit =
                FindSequence(reload, FpgSkillSequenceKind.Execute)
                    .ReloadEvents[0];
            Assert.That(reloadCommit.SuccessAnimationName,
                Is.EqualTo("u1_buff_ready"));
        }

        [Test]
        public void FeiSecondaryChargePrefabLoopsUntilExplicitRelease()
        {
            UnityEngine.GameObject prefab =
                LoadRequired<UnityEngine.GameObject>(FeiSecondaryChargePrefabPath);
            Assert.That(
                prefab.GetComponent<ChargeProgressVfxDriver>(),
                Is.Not.Null,
                FeiSecondaryChargePrefabPath);

            UnityEngine.ParticleSystem[] particleSystems =
                prefab.GetComponentsInChildren<UnityEngine.ParticleSystem>(true);
            Assert.That(
                particleSystems,
                Is.Not.Empty,
                FeiSecondaryChargePrefabPath);

            for (int index = 0; index < particleSystems.Length; index++)
            {
                UnityEngine.ParticleSystem particleSystem = particleSystems[index];
                Assert.That(
                    particleSystem.main.loop,
                    Is.True,
                    $"{FeiSecondaryChargePrefabPath}: ParticleSystem " +
                    $"'{GetHierarchyPath(particleSystem.transform, prefab.transform)}' " +
                    "must loop until the held presentation is released.");
            }
        }

        [Test]
        public void EnemyThreatLanguageAndNodePresentationAreExplicit()
        {
            AssertEnemyProjectile(
                BurstbugFastPath,
                FpgThreatPresentationKind.FastUninterceptable);
            AssertEnemyProjectile(
                BurstbugVolleyPath,
                FpgThreatPresentationKind.InterceptableVolley);
            AssertEnemyProjectile(
                HudiePath,
                FpgThreatPresentationKind.FastUninterceptable);

            FpgEnemyAttackDefinition heavy =
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugHeavyPath);
            FpgSkillAttackEventDefinition action =
                FindSequence(heavy, FpgSkillSequenceKind.Execute)
                    .AttackEvents[0];
            Assert.That(action.ThreatPresentationKind,
                Is.EqualTo(FpgThreatPresentationKind.HeavyWeakpoint));
            Assert.That(action.ImpactPresentation, Is.Not.Null);
            Assert.That(action.ImpactPresentation.BaseVfx, Is.Not.Null);
            AssertAssetPath(
                action.ImpactPresentation.BaseVfx.Prefab,
                EnemyVfxRoot + "PF_FPG_Enemy_Heavy_Impact.prefab");
            AssertWorldAudioGroup(
                action.ImpactPresentation.BaseAudio,
                4,
                "SFX_Burstbug_Heavy_Impact_");
            AssertWorldAudioGroup(
                action.ImpactPresentation.WeakpointAudioOverride,
                4,
                "SFX_Burstbug_Heavy_Weakpoint_");
        }

        [Test]
        public void HeavyWarningResolutionCarriesItsAuthoredCountdownWindow()
        {
            FpgEnemyAttackDefinition heavy =
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugHeavyPath);
            FpgSkillSequenceDefinition execute =
                FindSequence(heavy, FpgSkillSequenceKind.Execute);
            Assert.That(execute.Warnings.Count, Is.EqualTo(1));
            FpgSkillWarningDefinition warning = execute.Warnings[0];
            FpgCompiledSkillEvent compiledStart = new FpgCompiledSkillEvent(
                FpgSkillStableId.CompileEvent(warning.StartEventId),
                warning.StartTick,
                FpgSkillEventKind.WarningStarted,
                FpgSkillStableId.CompileWarning(warning.WarningId),
                socketId: FpgSkillStableId.CompileOptionalSocket(
                    warning.SocketId));

            Assert.That(
                FpgEnemySkillPresentationResolver.TryResolveWarning(
                    heavy,
                    FpgSkillSequenceKind.Execute,
                    compiledStart,
                    out FpgResolvedEnemySkillWarning resolved),
                Is.True);
            Assert.That(resolved.StartTick, Is.EqualTo(0));
            Assert.That(resolved.EndTick, Is.EqualTo(135));
            Assert.That(
                resolved.SocketName,
                Is.EqualTo("attack.default.origin"));
        }

        [Test]
        public void BurstbugFastProjectileAudioMatchesTheSpawnEvent()
        {
            FpgEnemyAttackDefinition skill =
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugFastPath);
            FpgSkillSequenceDefinition execute =
                FindSequence(skill, FpgSkillSequenceKind.Execute);

            Assert.That(execute.ActivePresentationTracks.Count, Is.EqualTo(1));
            FpgSkillActivePresentationTrackDefinition track =
                execute.ActivePresentationTracks[0];
            Assert.That(track.TrackId, Is.EqualTo("track.burstbug.fast.active"));
            Assert.That(track.AudioEvents.Count, Is.EqualTo(1));

            FpgAudioPresentationEventDefinition audio = track.AudioEvents[0];
            Assert.That(
                audio.EventId,
                Is.EqualTo("presentation.burstbug.fast.projectile.0"));
            Assert.That(audio.Tick, Is.EqualTo(36));
            Assert.That(audio.AuthoredOrdinal, Is.EqualTo(3));
            Assert.That(
                audio.BoundGameplayEventId,
                Is.EqualTo("event.burstbug-fast.attack.0"));
            Assert.That(audio.Presentation.ClipCount, Is.EqualTo(4));
            Assert.That(
                audio.Presentation.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                audio.Presentation.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerRoot));
            Assert.That(audio.Presentation.OwnerSocketId, Is.Empty);
            Assert.That(audio.Presentation.MinDistance, Is.EqualTo(1f));
            Assert.That(audio.Presentation.MaxDistance, Is.EqualTo(20f));
            for (int index = 0; index < audio.Presentation.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    "SFX_Burstbug_Fast_Projectile_",
                    audio.Presentation.GetClip(index).name);
            }
        }

        [Test]
        public void BurstbugFastHitAudioMatchesCollisionPresentation()
        {
            FpgEnemyAttackDefinition skill =
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugFastPath);
            FpgSkillProjectileEventDefinition projectile =
                FindSequence(skill, FpgSkillSequenceKind.Execute)
                    .ProjectileEvents[0];

            Assert.That(projectile.CollisionPresentation, Is.Not.Null);
            FpgAudioPresentationDefinition audio =
                projectile.CollisionPresentation.BaseAudio;
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.ClipCount, Is.EqualTo(6));
            Assert.That(
                audio.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                audio.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerRoot));
            Assert.That(audio.OwnerSocketId, Is.Empty);
            Assert.That(audio.MinDistance, Is.EqualTo(1f));
            Assert.That(audio.MaxDistance, Is.EqualTo(20f));
            for (int index = 0; index < audio.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    "SFX_Burstbug_Fast_Hit_",
                    audio.GetClip(index).name);
            }
        }

        [Test]
        public void BurstbugVolleyAudioSeparatesFlightImpactAndInterception()
        {
            FpgEnemyAttackDefinition skill =
                LoadRequired<FpgEnemyAttackDefinition>(BurstbugVolleyPath);
            FpgSkillProjectileEventDefinition projectile =
                FindSequence(skill, FpgSkillSequenceKind.Execute)
                    .ProjectileEvents[0];

            AssertWorldAudioGroup(
                projectile.FlightAudio,
                6,
                "SFX_Burstbug_Volley_Projectile_");
            Assert.That(projectile.CollisionPresentation, Is.Not.Null);
            AssertWorldAudioGroup(
                projectile.CollisionPresentation.BaseAudio,
                6,
                "SFX_Burstbug_Volley_Impact_");
            AssertWorldAudioGroup(
                projectile.CollisionPresentation.InterceptionAudioOverride,
                3,
                "SFX_Burstbug_Volley_Interception_");

            Assert.That(
                FpgSkillPresentationRegistry.TryResolveActionPresentation(
                    skill,
                    FpgSkillStableId.CompileEvent(
                        "event.burstbug-interceptable-volley.attack.0"),
                    out FpgCompiledSkillActionPresentation presentation),
                Is.True);
            Assert.That(presentation.FlightAudio.IsValid, Is.True);
            Assert.That(presentation.Collision.BaseAudio.IsValid, Is.True);
            Assert.That(
                presentation.Collision.InterceptionAudioOverride.IsValid,
                Is.True);
        }

        [Test]
        public void HudieProjectileAudioSeparatesLaunchFlightAndImpacts()
        {
            FpgEnemyAttackDefinition skill =
                LoadRequired<FpgEnemyAttackDefinition>(HudiePath);
            FpgSkillSequenceDefinition execute =
                FindSequence(skill, FpgSkillSequenceKind.Execute);
            FpgSkillProjectileEventDefinition projectile =
                execute.ProjectileEvents[0];

            Assert.That(execute.ActivePresentationTracks.Count, Is.EqualTo(1));
            FpgSkillActivePresentationTrackDefinition track =
                execute.ActivePresentationTracks[0];
            Assert.That(
                track.TrackId,
                Is.EqualTo("track.hudie-projectile.active"));
            Assert.That(track.AudioEvents.Count, Is.EqualTo(1));

            FpgAudioPresentationEventDefinition launch = track.AudioEvents[0];
            Assert.That(
                launch.EventId,
                Is.EqualTo("presentation.hudie-projectile.launch.0"));
            Assert.That(launch.Tick, Is.EqualTo(49));
            Assert.That(launch.AuthoredOrdinal, Is.EqualTo(3));
            Assert.That(
                launch.BoundGameplayEventId,
                Is.EqualTo("event.hudie-projectile.attack.0"));
            AssertWorldAudioGroup(
                launch.Presentation,
                4,
                "SFX_Hudie_Projectile_Launch_");
            AssertWorldAudioGroup(
                projectile.FlightAudio,
                5,
                "SFX_Hudie_Projectile_Flight_");
            Assert.That(projectile.CollisionPresentation, Is.Not.Null);
            AssertWorldAudioGroup(
                projectile.CollisionPresentation.BaseAudio,
                4,
                "SFX_Hudie_Projectile_Impact_");
            AssertWorldAudioGroup(
                projectile.CollisionPresentation.WeakpointAudioOverride,
                4,
                "SFX_Hudie_Projectile_Weakpoint_");

            Assert.That(
                FpgSkillPresentationRegistry.TryResolveActionPresentation(
                    skill,
                    FpgSkillStableId.CompileEvent(
                        "event.hudie-projectile.attack.0"),
                    out FpgCompiledSkillActionPresentation presentation),
                Is.True);
            Assert.That(presentation.FlightAudio.IsValid, Is.True);
            Assert.That(presentation.Collision.BaseAudio.IsValid, Is.True);
            Assert.That(
                presentation.Collision.WeakpointAudioOverride.IsValid,
                Is.True);
        }

        [Test]
        public void LuanSummonAudioSeparatesTelegraphAndAppearanceCommit()
        {
            FpgEnemyAttackDefinition skill =
                LoadRequired<FpgEnemyAttackDefinition>(LuanPath);
            FpgSkillSequenceDefinition execute =
                FindSequence(skill, FpgSkillSequenceKind.Execute);

            Assert.That(execute.ActivePresentationTracks.Count, Is.EqualTo(1));
            FpgSkillActivePresentationTrackDefinition track =
                execute.ActivePresentationTracks[0];
            Assert.That(track.TrackId, Is.EqualTo("track.luan-summon.active"));
            Assert.That(track.AudioEvents.Count, Is.EqualTo(3));

            FpgAudioPresentationEventDefinition telegraph =
                track.AudioEvents[0];
            Assert.That(
                telegraph.EventId,
                Is.EqualTo("presentation.luan-summon.telegraph.0"));
            Assert.That(telegraph.Tick, Is.Zero);
            Assert.That(telegraph.AuthoredOrdinal, Is.EqualTo(4));
            Assert.That(telegraph.BoundGameplayEventId, Is.Empty);
            AssertWorldAudioGroup(
                telegraph.Presentation,
                7,
                "SFX_Luan_Summon_Telegraph_");

            FpgAudioPresentationEventDefinition commit =
                track.AudioEvents[1];
            Assert.That(
                commit.EventId,
                Is.EqualTo("presentation.luan-summon.commit.0"));
            Assert.That(commit.Tick, Is.EqualTo(44));
            Assert.That(commit.AuthoredOrdinal, Is.EqualTo(5));
            Assert.That(
                commit.BoundGameplayEventId,
                Is.EqualTo("event.luan-summon.attack.0"));
            AssertWorldAudioGroup(
                commit.Presentation,
                7,
                "SFX_Luan_Summon_Commit_");

            FpgAudioPresentationEventDefinition selfDestruct =
                track.AudioEvents[2];
            Assert.That(
                selfDestruct.EventId,
                Is.EqualTo("presentation.luan-summon.self-destruct.1"));
            Assert.That(selfDestruct.Tick, Is.EqualTo(71));
            Assert.That(selfDestruct.AuthoredOrdinal, Is.EqualTo(6));
            Assert.That(
                selfDestruct.BoundGameplayEventId,
                Is.EqualTo("event.luan-summon.self-destruct.1"));
            AssertWorldAudioGroup(
                selfDestruct.Presentation,
                4,
                "SFX_Luan_SelfDestruct_");

            Assert.That(
                skill.TryCompile(
                    out FpgCompiledSkillDefinition compiled,
                    out string error),
                Is.True,
                error);
            FpgCompiledSkillSequence compiledExecute =
                FindSequence(compiled, FpgSkillSequenceKind.Execute);
            FpgCompiledSkillEvent compiledTelegraph = FindEvent(
                compiledExecute,
                "presentation.luan-summon.telegraph.0");
            FpgCompiledSkillEvent compiledCommit = FindEvent(
                compiledExecute,
                "presentation.luan-summon.commit.0");
            FpgCompiledSkillEvent compiledSelfDestruct = FindEvent(
                compiledExecute,
                "presentation.luan-summon.self-destruct.1");
            Assert.That(compiledTelegraph.PresentationHandle.IsValid, Is.True);
            Assert.That(compiledTelegraph.BoundGameplayEventId, Is.Zero);
            Assert.That(compiledCommit.PresentationHandle.IsValid, Is.True);
            Assert.That(
                compiledCommit.BoundGameplayEventId,
                Is.EqualTo(FpgSkillStableId.CompileEvent(
                    "event.luan-summon.attack.0")));
            Assert.That(
                compiledSelfDestruct.PresentationHandle.IsValid,
                Is.True);
            Assert.That(
                compiledSelfDestruct.BoundGameplayEventId,
                Is.EqualTo(FpgSkillStableId.CompileEvent(
                    "event.luan-summon.self-destruct.1")));
        }

        private static void AssertWorldAudioGroup(
            FpgAudioPresentationDefinition audio,
            int expectedCount,
            string expectedNamePrefix)
        {
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.ClipCount, Is.EqualTo(expectedCount));
            Assert.That(
                audio.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                audio.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerRoot));
            Assert.That(audio.OwnerSocketId, Is.Empty);
            Assert.That(audio.MinDistance, Is.EqualTo(1f));
            Assert.That(audio.MaxDistance, Is.EqualTo(20f));
            for (int index = 0; index < audio.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    expectedNamePrefix,
                    audio.GetClip(index).name);
            }
        }

        private static void AssertEnemyProjectile(
            string path,
            FpgThreatPresentationKind expectedKind)
        {
            FpgEnemyAttackDefinition skill =
                LoadRequired<FpgEnemyAttackDefinition>(path);
            FpgSkillProjectileEventDefinition action =
                FindSequence(skill, FpgSkillSequenceKind.Execute)
                    .ProjectileEvents[0];
            Assert.That(action.ThreatPresentationKind, Is.EqualTo(expectedKind));
            Assert.That(action.FlightVfx, Is.Not.Null);
            Assert.That(action.FlightVfx.Prefab, Is.Not.Null);
            AssertAssetPath(
                action.FlightVfx.Prefab,
                EnemyVfxRoot + "PF_FPG_Enemy_Projectile.prefab");
            Assert.That(action.CollisionPresentation, Is.Not.Null);
            Assert.That(action.CollisionPresentation.BaseVfx, Is.Not.Null);
            AssertAssetPath(
                action.CollisionPresentation.BaseVfx.Prefab,
                EnemyVfxRoot + "PF_FPG_Enemy_Impact.prefab");
        }

        private static void AssertAssetPath(
            UnityEngine.Object asset,
            string expectedPath)
        {
            Assert.That(
                AssetDatabase.GetAssetPath(asset),
                Is.EqualTo(expectedPath));
        }

        private static string GetHierarchyPath(
            UnityEngine.Transform transform,
            UnityEngine.Transform root)
        {
            string path = transform.name;
            while (transform != root && transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void AssertPureV3Authoring(
            FpgSkillTimelineDefinition skill)
        {
            Assert.That(
                skill.AuthoringSchemaVersion,
                Is.EqualTo(FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion),
                skill.name);

            SerializedObject serialized = new SerializedObject(skill);
            Assert.That(serialized.FindProperty("payloadSlots"), Is.Null);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            Assert.That(sequences, Is.Not.Null);
            for (int index = 0; index < sequences.arraySize; index++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(index);
                Assert.That(
                    sequence.FindPropertyRelative("logicEvents"),
                    Is.Null);
                Assert.That(
                    sequence.FindPropertyRelative("presentationCues"),
                    Is.Null);
            }

            string yaml = File.ReadAllText(AssetDatabase.GetAssetPath(skill));
            string[] legacyTokens =
            {
                "logicEvents:",
                "presentationCues:",
                "payloadSlots:",
                "shotPresentation:",
                "secondaryPresentation:",
                "reloadPresentation:",
                "hitPresentationKey:",
                "projectilePresentationKey:",
                "targetBurstVfxKey:",
                "target-burst"
            };
            for (int index = 0; index < legacyTokens.Length; index++)
            {
                Assert.That(
                    yaml,
                    Does.Not.Contain(legacyTokens[index]),
                    skill.name + " retains legacy token " + legacyTokens[index]);
            }
        }

        private static FpgSkillSequenceDefinition FindSequence(
            FpgSkillTimelineDefinition skill,
            FpgSkillSequenceKind kind)
        {
            IReadOnlyList<FpgSkillSequenceDefinition> sequences = skill.Sequences;
            for (int index = 0; index < sequences.Count; index++)
            {
                if (sequences[index].Kind == kind)
                {
                    return sequences[index];
                }
            }

            throw new InvalidOperationException(
                $"Skill '{skill.name}' does not contain sequence '{kind}'.");
        }

        private static FpgCompiledSkillSequence FindSequence(
            FpgCompiledSkillDefinition skill,
            FpgSkillSequenceKind kind)
        {
            for (int index = 0; index < skill.Sequences.Count; index++)
            {
                if (skill.Sequences[index].Kind == kind)
                {
                    return skill.Sequences[index];
                }
            }

            throw new InvalidOperationException(
                $"Compiled skill does not contain sequence '{kind}'.");
        }

        private static FpgCompiledSkillEvent FindEvent(
            FpgCompiledSkillSequence sequence,
            string eventId)
        {
            int compiledEventId = FpgSkillStableId.CompileEvent(eventId);
            for (int index = 0; index < sequence.Events.Count; index++)
            {
                if (sequence.Events[index].EventId == compiledEventId)
                {
                    return sequence.Events[index];
                }
            }

            throw new InvalidOperationException(
                $"Compiled sequence does not contain event '{eventId}'.");
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(value, Is.Not.Null, path);
            return value;
        }
    }
}
