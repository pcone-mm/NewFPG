using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgPlayerSkillAssetContractTests
    {
        private const string WeaponPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Weapon.asset";
        private const string PrimaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset";
        private const string SecondaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary.asset";
        private const string ReloadPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Reload.asset";
        private const string PresentationBridgePath =
            "Assets/FPGDemo/Runtime/Unity/FpgFormalPlayerPresentationBridge.cs";
        private const string TickDriverPath =
            "Assets/FPGDemo/Runtime/Unity/FpgFormalPlayerTickDriver.cs";
        private const string ActorPresenterPath =
            "Assets/FPGDemo/Runtime/Unity/Actor2DPresenter.cs";

        [Test]
        public void FeiFormalSkillsMatchTheAuthoredTickContract()
        {
            D0WeaponDefinition weapon = LoadRequired<D0WeaponDefinition>(WeaponPath);
            FpgPlayerSkillDefinition primary =
                LoadRequired<FpgPlayerSkillDefinition>(PrimaryPath);
            FpgPlayerSkillDefinition secondary =
                LoadRequired<FpgPlayerSkillDefinition>(SecondaryPath);
            FpgPlayerSkillDefinition reload =
                LoadRequired<FpgPlayerSkillDefinition>(ReloadPath);

            Assert.That(weapon.PrimarySkill, Is.SameAs(primary));
            Assert.That(weapon.SecondarySkill, Is.SameAs(secondary));
            Assert.That(weapon.ReloadSkill, Is.SameAs(reload));

            FpgSkillSequenceDefinition primaryExecute =
                FindSequence(primary, FpgSkillSequenceKind.Execute);
            Assert.That(
                primary.AuthoringSchemaVersion,
                Is.EqualTo(FpgSkillTimelineDefinition.CurrentAuthoringSchemaVersion));
            Assert.That(primaryExecute.DurationTicks, Is.EqualTo(40));
            Assert.That(primaryExecute.MainAnimation, Is.EqualTo("attack_play1"));
            Assert.That(
                primaryExecute.AlternateAnimations,
                Is.EqualTo(new[] { "attack_play2" }));
            Assert.That(primaryExecute.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(primaryExecute.AttackEvents[0].Tick, Is.Zero);
            Assert.That(primary.SequenceCooldownTicks, Is.EqualTo(12));

            Assert.That(
                secondary.Sequences.Select(value => value.Kind),
                Is.SupersetOf(new[]
                {
                    FpgSkillSequenceKind.Execute,
                    FpgSkillSequenceKind.ChargeEnter,
                    FpgSkillSequenceKind.ChargeLoop,
                    FpgSkillSequenceKind.Release,
                    FpgSkillSequenceKind.Cancel
                }));
            Assert.That(
                secondary.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ChargeRelease));
            FpgSkillSequenceDefinition release =
                FindSequence(secondary, FpgSkillSequenceKind.Release);
            Assert.That(release.DurationTicks, Is.EqualTo(60));
            Assert.That(release.ProjectileEvents.Count, Is.EqualTo(1));
            Assert.That(release.ProjectileEvents[0].Tick, Is.Zero);
            Assert.That(secondary.SequenceCooldownTicks, Is.EqualTo(30));

            FpgSkillSequenceDefinition reloadExecute =
                FindSequence(reload, FpgSkillSequenceKind.Execute);
            Assert.That(reloadExecute.DurationTicks, Is.EqualTo(84));
            Assert.That(reloadExecute.ReloadEvents.Count, Is.EqualTo(1));
            Assert.That(reloadExecute.ReloadEvents[0].Tick, Is.EqualTo(40));
        }

        [Test]
        public void FeiActivePresentationBindingsAndReloadSuccessLiveOnV3Nodes()
        {
            FpgPlayerSkillDefinition primary =
                LoadRequired<FpgPlayerSkillDefinition>(PrimaryPath);
            FpgPlayerSkillDefinition secondary =
                LoadRequired<FpgPlayerSkillDefinition>(SecondaryPath);
            FpgPlayerSkillDefinition reload =
                LoadRequired<FpgPlayerSkillDefinition>(ReloadPath);

            FpgSkillSequenceDefinition primaryExecute =
                FindSequence(primary, FpgSkillSequenceKind.Execute);
            Assert.That(
                primaryExecute.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                primaryExecute.ActivePresentationTracks[0].VfxEvents.Count,
                Is.EqualTo(1));
            Assert.That(
                primaryExecute.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.primary.attack.0"));

            FpgSkillSequenceDefinition chargeEnter =
                FindSequence(secondary, FpgSkillSequenceKind.ChargeEnter);
            Assert.That(
                chargeEnter.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                chargeEnter.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.Empty);

            FpgSkillSequenceDefinition release =
                FindSequence(secondary, FpgSkillSequenceKind.Release);
            Assert.That(
                release.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                release.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.release.attack.0"));

            FpgSkillSequenceDefinition reloadExecute =
                FindSequence(reload, FpgSkillSequenceKind.Execute);
            Assert.That(
                reloadExecute.ReloadEvents.Count,
                Is.EqualTo(1));
            Assert.That(
                reloadExecute.ReloadEvents[0].SuccessAnimationName,
                Is.EqualTo("u1_buff_ready"));
        }

        [Test]
        public void FeiFormalSkillsCompileIntoTheWeaponProjection()
        {
            D0WeaponDefinition weapon = LoadRequired<D0WeaponDefinition>(WeaponPath);

            Assert.That(
                weapon.TryCompileSkills(
                    out FpgCompiledPlayerSkillDefinition primary,
                    out FpgCompiledPlayerSkillDefinition secondary,
                    out FpgCompiledPlayerSkillDefinition reload,
                    out string compileError),
                Is.True,
                compileError);

            Assert.That(
                primary.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary primarySummary),
                Is.True);
            Assert.That(primarySummary.TotalAmmoCost, Is.EqualTo(1));
            Assert.That(primarySummary.LastAttackTick, Is.Zero);

            Assert.That(
                secondary.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Release,
                    out FpgCompiledPlayerSkillSequenceSummary secondarySummary),
                Is.True);
            Assert.That(secondarySummary.TotalAmmoCost, Is.EqualTo(2));
            Assert.That(secondarySummary.LastAttackTick, Is.Zero);
            Assert.That(secondary.ProjectileActionCount, Is.EqualTo(1));

            Assert.That(
                reload.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary reloadSummary),
                Is.True);
            Assert.That(reloadSummary.AttackEventCount, Is.Zero);
            Assert.That(reloadSummary.ReloadCommitEventCount, Is.EqualTo(1));

            Assert.That(
                weapon.TryCreate(out WeaponDefinition runtimeWeapon, out string error),
                Is.True,
                error);
            Assert.That(
                runtimeWeapon.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ChargeRelease));
            Assert.That(weapon.PrimaryIntervalTicks, Is.EqualTo(12));
            Assert.That(weapon.ReloadDurationTicks, Is.EqualTo(84));
        }

        [Test]
        public void FeiSecondaryProjectilePresentationKeepsOffsetsEditableAndImpactAlive()
        {
            const string projectilePath =
                "Assets/FPGDemo/Presentation/Characters/Fei/VFX/PF_FPG_Fei_Secondary_Projectile.prefab";
            const string impactPath =
                "Assets/FPGDemo/Presentation/Characters/Fei/VFX/PF_FPG_Fei_Secondary_Hit.prefab";
            FpgPlayerSkillDefinition secondary =
                LoadRequired<FpgPlayerSkillDefinition>(SecondaryPath);
            FpgSkillSequenceDefinition release =
                FindSequence(secondary, FpgSkillSequenceKind.Release);
            FpgSkillProjectileEventDefinition projectile =
                release.ProjectileEvents[0];
            FpgVfxPresentationDefinition flight = projectile.FlightVfx;
            FpgVfxPresentationDefinition impact =
                projectile.CollisionPresentation.BaseVfx;

            Assert.That(flight, Is.Not.Null);
            Assert.That(projectile.CollisionPresentation, Is.Not.Null);
            Assert.That(impact, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(flight.Prefab),
                Is.EqualTo(projectilePath));
            Assert.That(
                AssetDatabase.GetAssetPath(impact.Prefab),
                Is.EqualTo(impactPath));
            Assert.That(
                impact.DurationSeconds,
                Is.EqualTo(1.8f).Within(0.0001f));

            AssertFinite(flight.RotationOffsetEuler);
            AssertFinite(impact.RotationOffsetEuler);

            SerializedObject serialized = new SerializedObject(secondary);
            int releaseIndex = secondary.Sequences
                .Select((sequence, index) => new { sequence, index })
                .Single(value => value.sequence.Kind == FpgSkillSequenceKind.Release)
                .index;
            SerializedProperty serializedProjectile = serialized.FindProperty(
                $"sequences.Array.data[{releaseIndex}].projectileEvents.Array.data[0]");
            Assert.That(serializedProjectile, Is.Not.Null);
            SerializedProperty serializedFlight =
                serializedProjectile.FindPropertyRelative("flightVfx");
            SerializedProperty serializedCollision =
                serializedProjectile.FindPropertyRelative("collisionPresentation");
            Assert.That(
                serializedFlight.propertyType,
                Is.EqualTo(SerializedPropertyType.ManagedReference));
            Assert.That(
                serializedCollision.propertyType,
                Is.EqualTo(SerializedPropertyType.ManagedReference));
            Assert.That(
                serializedFlight.FindPropertyRelative("rotationOffsetEuler")
                    .propertyType,
                Is.EqualTo(SerializedPropertyType.Vector3));
            SerializedProperty serializedImpact =
                serializedCollision.FindPropertyRelative("baseVfx");
            Assert.That(
                serializedImpact.propertyType,
                Is.EqualTo(SerializedPropertyType.ManagedReference));
            Assert.That(
                serializedImpact.FindPropertyRelative("rotationOffsetEuler")
                    .propertyType,
                Is.EqualTo(SerializedPropertyType.Vector3));
        }

        [Test]
        public void FormalPlayerBridgeCannotInvokeLegacyWeaponSkillAnimations()
        {
            string bridgeSource = File.ReadAllText(PresentationBridgePath);
            string presenterSource = File.ReadAllText(ActorPresenterPath);

            Assert.That(
                bridgeSource,
                Does.Not.Contain("actorPresenter.PlayPrimaryAttack("));
            Assert.That(bridgeSource, Does.Not.Contain("actorPresenter.BeginReload("));
            Assert.That(bridgeSource, Does.Not.Contain("actorPresenter.CompleteReload("));
            Assert.That(
                bridgeSource,
                Does.Not.Contain("actorPresenter.BeginSecondaryCharge("));
            Assert.That(
                bridgeSource,
                Does.Not.Contain("actorPresenter.CancelSecondaryCharge("));
            Assert.That(
                bridgeSource,
                Does.Not.Contain("actorPresenter.PlaySecondaryRelease("));
            Assert.That(
                bridgeSource,
                Does.Contain("actorPresenter.NotifyPrimarySkillCommitted("));
            Assert.That(
                bridgeSource,
                Does.Contain("actorPresenter.NotifySecondaryReleaseCommitted("));

            string stateOnlyActions = SliceSource(
                presenterSource,
                "public void NotifyPrimarySkillCommitted()",
                "public void PlayHit()");
            Assert.That(stateOnlyActions, Does.Not.Contain("SetAnimation("));
            Assert.That(stateOnlyActions, Does.Not.Contain("AddAnimation("));
            Assert.That(stateOnlyActions, Does.Not.Contain("PlayOneShot"));
            Assert.That(stateOnlyActions, Does.Not.Contain("PlayLooping("));
            Assert.That(
                stateOnlyActions,
                Does.Not.Contain("runtimeWeaponDefinition."));
        }

        [Test]
        public void ExternalGameplayCommitPrecedesAttackAndShotIdCommit()
        {
            string source = File.ReadAllText(TickDriverPath);
            int hitMethod = source.IndexOf(
                "private DomainResult QueryAndSubmitHits",
                StringComparison.Ordinal);
            int roomMethod = source.IndexOf(
                "private DomainResult QueryAndCommitRoomInteraction",
                hitMethod,
                StringComparison.Ordinal);
            int postureMethod = source.IndexOf(
                "private static DomainResult ApplyPosture",
                roomMethod,
                StringComparison.Ordinal);

            AssertOrderedWithin(
                source,
                hitMethod,
                roomMethod,
                "runtime.CombatPort.TrySubmitPlayerHits",
                "CommitPreparedSkillRelease");
            AssertOrderedWithin(
                source,
                roomMethod,
                postureMethod,
                "encounterDirector.TrySelectExit",
                "CommitPreparedSkillRelease");
        }

        private static FpgSkillSequenceDefinition FindSequence(
            FpgPlayerSkillDefinition skill,
            FpgSkillSequenceKind kind)
        {
            for (int index = 0; index < skill.Sequences.Count; index++)
            {
                FpgSkillSequenceDefinition sequence = skill.Sequences[index];
                if (sequence.Kind == kind)
                {
                    return sequence;
                }
            }

            Assert.Fail(
                "Skill '" + skill.SkillId + "' is missing sequence " + kind + ".");
            return null;
        }

        private static void AssertOrderedWithin(
            string source,
            int start,
            int end,
            string first,
            string second)
        {
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            int firstIndex = source.IndexOf(
                first,
                start,
                end - start,
                StringComparison.Ordinal);
            int secondIndex = source.IndexOf(
                second,
                start,
                end - start,
                StringComparison.Ordinal);
            Assert.That(firstIndex, Is.GreaterThanOrEqualTo(start), first);
            Assert.That(secondIndex, Is.GreaterThan(firstIndex), second);
        }

        private static void AssertFinite(UnityEngine.Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }

        private static string SliceSource(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(
                startMarker,
                StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            int end = source.IndexOf(
                endMarker,
                start,
                StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
