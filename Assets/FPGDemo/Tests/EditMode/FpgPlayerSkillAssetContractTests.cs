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
        private const string ImmediateSecondaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Immediate.asset";
        private const string ChargeSecondaryPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Charge.asset";
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
            FpgPlayerSkillDefinition immediateSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ImmediateSecondaryPath);
            FpgPlayerSkillDefinition chargeSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ChargeSecondaryPath);
            FpgPlayerSkillDefinition reload =
                LoadRequired<FpgPlayerSkillDefinition>(ReloadPath);

            Assert.That(weapon.PrimarySkill, Is.SameAs(primary));
            Assert.That(
                weapon.ImmediateSecondarySkill,
                Is.SameAs(immediateSecondary));
            Assert.That(
                weapon.ChargeSecondarySkill,
                Is.SameAs(chargeSecondary));
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
                immediateSecondary.Sequences.Select(value => value.Kind),
                Is.EquivalentTo(new[]
                {
                    FpgSkillSequenceKind.Execute
                }));
            Assert.That(
                chargeSecondary.Sequences.Select(value => value.Kind),
                Is.EquivalentTo(new[]
                {
                    FpgSkillSequenceKind.ChargeEnter,
                    FpgSkillSequenceKind.ChargeLoop,
                    FpgSkillSequenceKind.Release,
                    FpgSkillSequenceKind.Cancel
                }));
            Assert.That(
                immediateSecondary.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            Assert.That(
                chargeSecondary.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ChargeRelease));
            FpgSkillSequenceDefinition release =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Release);
            FpgSkillSequenceDefinition execute =
                FindSequence(immediateSecondary, FpgSkillSequenceKind.Execute);
            FpgSkillSequenceDefinition chargeEnter =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.ChargeEnter);
            FpgSkillSequenceDefinition chargeLoop =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.ChargeLoop);
            FpgSkillSequenceDefinition cancel =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Cancel);
            Assert.That(execute.DurationTicks, Is.EqualTo(60));
            Assert.That(execute.MainAnimation, Is.EqualTo("defense_play"));
            Assert.That(execute.ProjectileEvents.Count, Is.EqualTo(1));
            Assert.That(chargeEnter.DurationTicks, Is.EqualTo(28));
            Assert.That(chargeEnter.MainAnimation, Is.EqualTo("u4_attack_ready"));
            Assert.That(chargeLoop.DurationTicks, Is.Zero);
            Assert.That(chargeLoop.MainAnimation, Is.EqualTo("u4_attack_ready"));
            Assert.That(chargeLoop.Loop, Is.True);
            Assert.That(chargeLoop.HoldUntilCanceled, Is.True);
            Assert.That(release.DurationTicks, Is.EqualTo(52));
            Assert.That(release.MainAnimation, Is.EqualTo("u4_attack_play"));
            Assert.That(release.ProjectileEvents.Count, Is.EqualTo(1));
            Assert.That(release.ProjectileEvents[0].Tick, Is.Zero);
            Assert.That(cancel.DurationTicks, Is.EqualTo(28));
            Assert.That(cancel.MainAnimation, Is.EqualTo("u4_attack_end"));
            Assert.That(immediateSecondary.MinimumChargeTicks, Is.Zero);
            Assert.That(immediateSecondary.ChargeProgressTicks, Is.Zero);
            Assert.That(
                immediateSecondary.SequenceCooldownTicks,
                Is.EqualTo(30));
            Assert.That(chargeSecondary.MinimumChargeTicks, Is.EqualTo(30));
            Assert.That(chargeSecondary.ChargeProgressTicks, Is.EqualTo(30));
            Assert.That(chargeSecondary.SequenceCooldownTicks, Is.EqualTo(30));

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
            FpgPlayerSkillDefinition immediateSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ImmediateSecondaryPath);
            FpgPlayerSkillDefinition chargeSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ChargeSecondaryPath);
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
                FindSequence(chargeSecondary, FpgSkillSequenceKind.ChargeEnter);
            Assert.That(
                chargeEnter.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                chargeEnter.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.Empty);

            FpgSkillSequenceDefinition execute =
                FindSequence(immediateSecondary, FpgSkillSequenceKind.Execute);
            Assert.That(
                execute.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                execute.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.execute.attack.0"));

            FpgSkillSequenceDefinition release =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Release);
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
                    SecondaryTriggerMode.ChargeRelease,
                    out FpgCompiledPlayerSkillDefinition primary,
                    out FpgCompiledPlayerSkillDefinition chargeSecondary,
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
                chargeSecondary.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Release,
                    out FpgCompiledPlayerSkillSequenceSummary secondarySummary),
                Is.True);
            Assert.That(secondarySummary.TotalAmmoCost, Is.EqualTo(2));
            Assert.That(secondarySummary.LastAttackTick, Is.Zero);
            Assert.That(
                chargeSecondary.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Execute,
                    out _),
                Is.False);
            Assert.That(chargeSecondary.ProjectileActionCount, Is.EqualTo(1));

            Assert.That(
                weapon.TryCompileSkills(
                    SecondaryTriggerMode.ImmediateRepeatWhileHeld,
                    out _,
                    out FpgCompiledPlayerSkillDefinition immediateSecondary,
                    out _,
                    out string immediateCompileError),
                Is.True,
                immediateCompileError);
            Assert.That(
                immediateSecondary.TryGetSequenceSummary(
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledPlayerSkillSequenceSummary immediateSummary),
                Is.True);
            Assert.That(immediateSummary.TotalAmmoCost, Is.EqualTo(2));
            Assert.That(immediateSummary.LastAttackTick, Is.Zero);
            Assert.That(
                immediateSecondary.Timeline.TryGetSequence(
                    FpgSkillSequenceKind.Release,
                    out _),
                Is.False);
            Assert.That(immediateSecondary.ProjectileActionCount, Is.EqualTo(1));

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
            Assert.That(runtimeWeapon.SecondaryMinimumCharge.Value, Is.EqualTo(30));
            Assert.That(runtimeWeapon.SecondaryRecovery.Value, Is.EqualTo(30));
            Assert.That(
                weapon.TryCreate(
                    SecondaryTriggerMode.ImmediateRepeatWhileHeld,
                    out WeaponDefinition immediateWeapon,
                    out string immediateError),
                Is.True,
                immediateError);
            Assert.That(
                immediateWeapon.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ImmediateRepeatWhileHeld));
            Assert.That(immediateWeapon.SecondaryMinimumCharge.Value, Is.Zero);
            Assert.That(
                immediateWeapon.SecondaryAmmoCost,
                Is.EqualTo(runtimeWeapon.SecondaryAmmoCost));
            Assert.That(
                immediateWeapon.SecondaryDamage,
                Is.EqualTo(runtimeWeapon.SecondaryDamage));
            Assert.That(weapon.PrimaryIntervalTicks, Is.EqualTo(12));
            Assert.That(weapon.ReloadDurationTicks, Is.EqualTo(84));
        }

        [Test]
        public void FeiSecondaryModesShareOneProjectilePayloadContract()
        {
            FpgPlayerSkillDefinition immediateSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ImmediateSecondaryPath);
            FpgPlayerSkillDefinition chargeSecondary =
                LoadRequired<FpgPlayerSkillDefinition>(ChargeSecondaryPath);
            FpgSkillSequenceDefinition immediateSequence =
                FindSequence(immediateSecondary, FpgSkillSequenceKind.Execute);
            FpgSkillSequenceDefinition chargedSequence =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Release);
            FpgSkillProjectileEventDefinition immediate =
                immediateSequence.ProjectileEvents[0];
            FpgSkillProjectileEventDefinition charged =
                chargedSequence.ProjectileEvents[0];

            Assert.That(immediate.EventId, Is.Not.EqualTo(charged.EventId));
            AssertProjectileEventEquivalent(charged, immediate);
            AssertMuzzlePresentationEquivalent(
                chargedSequence,
                charged.EventId,
                immediateSequence,
                immediate.EventId);

            Assert.That(
                chargeSecondary.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiledChargeSkill,
                    out string chargeCompileError),
                Is.True,
                chargeCompileError);
            Assert.That(
                immediateSecondary.TryCompile(
                    out FpgCompiledPlayerSkillDefinition compiledImmediateSkill,
                    out string immediateCompileError),
                Is.True,
                immediateCompileError);
            FpgCompiledPlayerProjectileAction compiledCharged =
                ResolveCompiledProjectileAction(
                    compiledChargeSkill,
                    FpgSkillSequenceKind.Release,
                    out FpgCompiledSkillSequence compiledChargedSequence,
                    out FpgCompiledSkillEvent compiledChargedEvent);
            FpgCompiledPlayerProjectileAction compiledImmediate =
                ResolveCompiledProjectileAction(
                    compiledImmediateSkill,
                    FpgSkillSequenceKind.Execute,
                    out FpgCompiledSkillSequence compiledImmediateSequence,
                    out FpgCompiledSkillEvent compiledImmediateEvent);

            AssertCompiledGameplayEventEquivalent(
                compiledChargedEvent,
                compiledImmediateEvent);
            AssertCompiledProjectileActionEquivalent(
                compiledCharged,
                compiledImmediate);
            Assert.That(
                ResolveProjectilePresentationContentHash(
                    compiledImmediateSequence,
                    compiledImmediateEvent.ActionIndex),
                Is.EqualTo(ResolveProjectilePresentationContentHash(
                    compiledChargedSequence,
                    compiledChargedEvent.ActionIndex)));
        }

        [Test]
        public void FeiSecondaryProjectilePresentationKeepsOffsetsEditableAndImpactAlive()
        {
            const string projectilePath =
                "Assets/FPGDemo/Presentation/Characters/Fei/VFX/PF_FPG_Fei_Secondary_Projectile.prefab";
            const string impactPath =
                "Assets/FPGDemo/Presentation/Characters/Fei/VFX/PF_FPG_Fei_Secondary_Hit.prefab";
            FpgPlayerSkillDefinition secondary =
                LoadRequired<FpgPlayerSkillDefinition>(ChargeSecondaryPath);
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

        private static void AssertProjectileEventEquivalent(
            FpgSkillProjectileEventDefinition expected,
            FpgSkillProjectileEventDefinition actual)
        {
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick));
            Assert.That(
                actual.AuthoredOrdinal,
                Is.EqualTo(expected.AuthoredOrdinal));
            Assert.That(actual.SocketId, Is.EqualTo(expected.SocketId));
            Assert.That(
                actual.TargetSource,
                Is.EqualTo(expected.TargetSource));
            Assert.That(actual.TargetOffset, Is.EqualTo(expected.TargetOffset));
            Assert.That(
                actual.BoundGameplayEventId,
                Is.EqualTo(expected.BoundGameplayEventId));
            Assert.That(actual.ImpactMode, Is.EqualTo(expected.ImpactMode));
            Assert.That(actual.AmmoCost, Is.EqualTo(expected.AmmoCost));
            Assert.That(actual.BaseDamage, Is.EqualTo(expected.BaseDamage));
            Assert.That(actual.BreakDamage, Is.EqualTo(expected.BreakDamage));
            Assert.That(
                actual.WeakpointDamageMultiplierBasisPoints,
                Is.EqualTo(expected.WeakpointDamageMultiplierBasisPoints));
            Assert.That(
                actual.WeakpointBreakMultiplierBasisPoints,
                Is.EqualTo(expected.WeakpointBreakMultiplierBasisPoints));
            Assert.That(
                actual.ThreatDefinitionId,
                Is.EqualTo(expected.ThreatDefinitionId));
            Assert.That(
                actual.ProjectileDefinitionId,
                Is.EqualTo(expected.ProjectileDefinitionId));
            Assert.That(
                actual.ProjectileCount,
                Is.EqualTo(expected.ProjectileCount));
            Assert.That(
                actual.ProjectileFlightTicks,
                Is.EqualTo(expected.ProjectileFlightTicks));
            Assert.That(
                actual.ProjectileLifetimeTicks,
                Is.EqualTo(expected.ProjectileLifetimeTicks));
            Assert.That(
                actual.ProjectileMaxHitPoints,
                Is.EqualTo(expected.ProjectileMaxHitPoints));
            Assert.That(
                actual.ProjectileInterceptable,
                Is.EqualTo(expected.ProjectileInterceptable));
            Assert.That(
                actual.ProjectileBudgetUnits,
                Is.EqualTo(expected.ProjectileBudgetUnits));
            Assert.That(
                actual.ProjectileSweepRadiusKey,
                Is.EqualTo(expected.ProjectileSweepRadiusKey));
            Assert.That(
                actual.ThreatPresentationKind,
                Is.EqualTo(expected.ThreatPresentationKind));
            Assert.That(
                actual.AreaCombatantLimit,
                Is.EqualTo(expected.AreaCombatantLimit));
            Assert.That(
                actual.AreaProjectileLimit,
                Is.EqualTo(expected.AreaProjectileLimit));
            Assert.That(
                actual.AllowedTargetKinds,
                Is.EqualTo(expected.AllowedTargetKinds));
            Assert.That(
                actual.MaxImpactCount,
                Is.EqualTo(expected.MaxImpactCount));
            AssertVfxPresentationEquivalent(
                expected.FlightVfx,
                actual.FlightVfx,
                "projectile flight VFX");
            AssertImpactPresentationEquivalent(
                expected.CollisionPresentation,
                actual.CollisionPresentation,
                "projectile collision presentation");
        }

        private static void AssertMuzzlePresentationEquivalent(
            FpgSkillSequenceDefinition expectedSequence,
            string expectedGameplayEventId,
            FpgSkillSequenceDefinition actualSequence,
            string actualGameplayEventId)
        {
            Assert.That(
                actualSequence.ActivePresentationTracks.Count,
                Is.EqualTo(expectedSequence.ActivePresentationTracks.Count));
            Assert.That(
                actualSequence.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            FpgSkillActivePresentationTrackDefinition expectedTrack =
                expectedSequence.ActivePresentationTracks[0];
            FpgSkillActivePresentationTrackDefinition actualTrack =
                actualSequence.ActivePresentationTracks[0];
            Assert.That(actualTrack.TrackId, Is.Not.EqualTo(expectedTrack.TrackId));
            Assert.That(
                actualTrack.DisplayName,
                Is.EqualTo(expectedTrack.DisplayName));
            Assert.That(
                actualTrack.VfxEvents.Count,
                Is.EqualTo(expectedTrack.VfxEvents.Count));
            Assert.That(actualTrack.VfxEvents.Count, Is.EqualTo(1));
            Assert.That(
                actualTrack.AudioEvents.Count,
                Is.EqualTo(expectedTrack.AudioEvents.Count));
            Assert.That(
                actualTrack.CameraShakeEvents.Count,
                Is.EqualTo(expectedTrack.CameraShakeEvents.Count));

            FpgVfxPresentationEventDefinition expected =
                expectedTrack.VfxEvents[0];
            FpgVfxPresentationEventDefinition actual =
                actualTrack.VfxEvents[0];
            Assert.That(actual.EventId, Is.Not.EqualTo(expected.EventId));
            Assert.That(
                expected.BoundGameplayEventId,
                Is.EqualTo(expectedGameplayEventId));
            Assert.That(
                actual.BoundGameplayEventId,
                Is.EqualTo(actualGameplayEventId));
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick));
            Assert.That(
                actual.AuthoredOrdinal,
                Is.EqualTo(expected.AuthoredOrdinal));
            Assert.That(actual.Anchor, Is.EqualTo(expected.Anchor));
            Assert.That(
                actual.OwnerSocketId,
                Is.EqualTo(expected.OwnerSocketId));
            AssertVfxPresentationEquivalent(
                expected.Presentation,
                actual.Presentation,
                "secondary muzzle VFX");
        }

        private static FpgCompiledPlayerProjectileAction
            ResolveCompiledProjectileAction(
                FpgCompiledPlayerSkillDefinition skill,
                FpgSkillSequenceKind sequenceKind,
                out FpgCompiledSkillSequence sequence,
                out FpgCompiledSkillEvent skillEvent)
        {
            Assert.That(
                skill.Timeline.TryGetSequence(sequenceKind, out sequence),
                Is.True,
                sequenceKind.ToString());
            skillEvent = sequence.Events.Single(value =>
                value.Kind == FpgSkillEventKind.GameplayAction
                && value.ActionKind == FpgSkillActionKind.LaunchProjectile);
            Assert.That(
                skillEvent.ActionIndex,
                Is.InRange(0, skill.ProjectileActions.Count - 1));
            return skill.ProjectileActions[skillEvent.ActionIndex];
        }

        private static void AssertCompiledGameplayEventEquivalent(
            FpgCompiledSkillEvent expected,
            FpgCompiledSkillEvent actual)
        {
            Assert.That(actual.EventId, Is.Not.EqualTo(expected.EventId));
            Assert.That(actual.Tick, Is.EqualTo(expected.Tick));
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
            Assert.That(actual.ActionKind, Is.EqualTo(expected.ActionKind));
            Assert.That(actual.WarningId, Is.EqualTo(expected.WarningId));
            Assert.That(actual.SortOrder, Is.EqualTo(expected.SortOrder));
            Assert.That(actual.SocketId, Is.EqualTo(expected.SocketId));
            Assert.That(
                actual.TargetSource,
                Is.EqualTo(expected.TargetSource));
            Assert.That(actual.Offset, Is.EqualTo(expected.Offset));
            Assert.That(
                actual.BoundGameplayEventId,
                Is.EqualTo(expected.BoundGameplayEventId));
            Assert.That(
                actual.ActivePresentationKind,
                Is.EqualTo(expected.ActivePresentationKind));
            Assert.That(
                actual.PresentationHandle,
                Is.EqualTo(expected.PresentationHandle));
            Assert.That(
                actual.PresentationTrackId,
                Is.EqualTo(expected.PresentationTrackId));
            Assert.That(
                actual.PresentationContentHash,
                Is.EqualTo(expected.PresentationContentHash));
        }

        private static void AssertCompiledProjectileActionEquivalent(
            FpgCompiledPlayerProjectileAction expected,
            FpgCompiledPlayerProjectileAction actual)
        {
            Assert.That(actual.IsValid, Is.True);
            Assert.That(actual.ImpactMode, Is.EqualTo(expected.ImpactMode));
            Assert.That(
                actual.ThreatDefinitionId,
                Is.EqualTo(expected.ThreatDefinitionId));
            AssertCompiledPlayerPayloadEquivalent(
                expected.Payload,
                actual.Payload);
        }

        private static void AssertCompiledPlayerPayloadEquivalent(
            FpgCompiledPlayerSkillAction expected,
            FpgCompiledPlayerSkillAction actual)
        {
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
            Assert.That(actual.AmmoCost, Is.EqualTo(expected.AmmoCost));
            Assert.That(
                actual.Damage.BaseDamage,
                Is.EqualTo(expected.Damage.BaseDamage));
            Assert.That(
                actual.Damage.BreakDamage,
                Is.EqualTo(expected.Damage.BreakDamage));
            Assert.That(
                actual.Damage.WeakpointDamageMultiplierBasisPoints,
                Is.EqualTo(expected.Damage
                    .WeakpointDamageMultiplierBasisPoints));
            Assert.That(
                actual.Damage.WeakpointBreakMultiplierBasisPoints,
                Is.EqualTo(expected.Damage
                    .WeakpointBreakMultiplierBasisPoints));
            Assert.That(
                actual.QueryPolicy,
                Is.EqualTo(expected.QueryPolicy));
            Assert.That(actual.QueryMode, Is.EqualTo(expected.QueryMode));
            Assert.That(
                actual.PayloadCount,
                Is.EqualTo(expected.PayloadCount));
            Assert.That(
                actual.MaxImpactCount,
                Is.EqualTo(expected.MaxImpactCount));
            Assert.That(
                actual.AdditionalPenetrationCount,
                Is.EqualTo(expected.AdditionalPenetrationCount));
            Assert.That(
                actual.AreaCombatantLimit,
                Is.EqualTo(expected.AreaCombatantLimit));
            Assert.That(
                actual.AreaProjectileLimit,
                Is.EqualTo(expected.AreaProjectileLimit));
            Assert.That(
                actual.AllowedTargetKinds,
                Is.EqualTo(expected.AllowedTargetKinds));
            Assert.That(
                actual.ProjectileFlightTicks,
                Is.EqualTo(expected.ProjectileFlightTicks));
            Assert.That(
                actual.ProjectileSweepRadiusKey,
                Is.EqualTo(expected.ProjectileSweepRadiusKey));
            Assert.That(
                actual.ProjectileDefinitionId,
                Is.EqualTo(expected.ProjectileDefinitionId));
            Assert.That(
                actual.ProjectileCount,
                Is.EqualTo(expected.ProjectileCount));
            Assert.That(
                actual.ProjectileLifetimeTicks,
                Is.EqualTo(expected.ProjectileLifetimeTicks));
            Assert.That(
                actual.ProjectileMaxHitPoints,
                Is.EqualTo(expected.ProjectileMaxHitPoints));
            Assert.That(
                actual.ProjectileInterceptable,
                Is.EqualTo(expected.ProjectileInterceptable));
            Assert.That(
                actual.ProjectileBudgetUnits,
                Is.EqualTo(expected.ProjectileBudgetUnits));
        }

        private static ulong ResolveProjectilePresentationContentHash(
            FpgCompiledSkillSequence sequence,
            int actionIndex)
        {
            return sequence.ActionPresentations.Single(value =>
                value.ActionKind == FpgSkillActionKind.LaunchProjectile
                && value.ActionIndex == actionIndex).PresentationContentHash;
        }

        private static void AssertImpactPresentationEquivalent(
            FpgImpactPresentationBundleDefinition expected,
            FpgImpactPresentationBundleDefinition actual,
            string context)
        {
            Assert.That(
                actual == null,
                Is.EqualTo(expected == null),
                context + " presence");
            if (expected == null || actual == null)
            {
                return;
            }

            AssertVfxPresentationEquivalent(
                expected.BaseVfx,
                actual.BaseVfx,
                context + " base VFX");
            AssertAudioPresentationEquivalent(
                expected.BaseAudio,
                actual.BaseAudio,
                context + " base audio");
            AssertCameraShakePresentationEquivalent(
                expected.BaseCameraShake,
                actual.BaseCameraShake,
                context + " base camera shake");
            AssertVfxPresentationEquivalent(
                expected.WeakpointVfxOverride,
                actual.WeakpointVfxOverride,
                context + " weakpoint VFX");
            AssertAudioPresentationEquivalent(
                expected.WeakpointAudioOverride,
                actual.WeakpointAudioOverride,
                context + " weakpoint audio");
            AssertCameraShakePresentationEquivalent(
                expected.WeakpointCameraShakeOverride,
                actual.WeakpointCameraShakeOverride,
                context + " weakpoint camera shake");
        }

        private static void AssertVfxPresentationEquivalent(
            FpgVfxPresentationDefinition expected,
            FpgVfxPresentationDefinition actual,
            string context)
        {
            Assert.That(
                actual == null,
                Is.EqualTo(expected == null),
                context + " presence");
            if (expected == null || actual == null)
            {
                return;
            }

            Assert.That(
                actual.Prefab,
                Is.SameAs(expected.Prefab),
                context + " prefab");
            Assert.That(
                actual.DurationSeconds,
                Is.EqualTo(expected.DurationSeconds),
                context + " duration");
            Assert.That(
                actual.Scale,
                Is.EqualTo(expected.Scale),
                context + " scale");
            Assert.That(
                actual.RotationOffsetEuler,
                Is.EqualTo(expected.RotationOffsetEuler),
                context + " rotation offset");
        }

        private static void AssertAudioPresentationEquivalent(
            FpgAudioPresentationDefinition expected,
            FpgAudioPresentationDefinition actual,
            string context)
        {
            Assert.That(
                actual == null,
                Is.EqualTo(expected == null),
                context + " presence");
            if (expected == null || actual == null)
            {
                return;
            }

            Assert.That(
                actual.Clip,
                Is.SameAs(expected.Clip),
                context + " clip");
            Assert.That(
                actual.Volume,
                Is.EqualTo(expected.Volume),
                context + " volume");
        }

        private static void AssertCameraShakePresentationEquivalent(
            FpgCameraShakePresentationDefinition expected,
            FpgCameraShakePresentationDefinition actual,
            string context)
        {
            Assert.That(
                actual == null,
                Is.EqualTo(expected == null),
                context + " presence");
            if (expected == null || actual == null)
            {
                return;
            }

            Assert.That(
                actual.Strength,
                Is.EqualTo(expected.Strength),
                context + " strength");
            Assert.That(
                actual.DurationSeconds,
                Is.EqualTo(expected.DurationSeconds),
                context + " duration");
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
