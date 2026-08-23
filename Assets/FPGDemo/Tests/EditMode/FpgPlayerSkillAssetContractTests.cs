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
        private const string ReloadClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Reload_";
        private const string ImmediateSecondaryLaunchClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Immediate_Launch_";
        private const string ImmediateSecondaryHitClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Immediate_Hit_01.wav";
        private const string ImmediateSecondaryWeakpointClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Immediate_Weakpoint_01.wav";
        private const string ChargeSecondaryStartClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Start_01.wav";
        private const string ChargeSecondaryHoldClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Hold_01.wav";
        private const string ChargeSecondaryReleaseClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Release_";
        private const string ChargeSecondaryCancelClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Cancel_";
        private const string ChargeSecondaryHitClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Hit_02.wav";
        private const string ChargeSecondaryWeakpointClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Weakpoint_02.wav";
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
            Assert.That(primary.UsesSecondaryTriggerMode, Is.False);

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
            Assert.That(immediateSecondary.UsesSecondaryTriggerMode, Is.True);
            Assert.That(
                chargeSecondary.SecondaryTriggerMode,
                Is.EqualTo(SecondaryTriggerMode.ChargeRelease));
            Assert.That(chargeSecondary.UsesSecondaryTriggerMode, Is.True);
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
            Assert.That(reload.UsesSecondaryTriggerMode, Is.False);
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
            Assert.That(
                primaryExecute.ActivePresentationTracks[0].AudioEvents.Count,
                Is.EqualTo(1));
            FpgAudioPresentationEventDefinition primaryAudio =
                primaryExecute.ActivePresentationTracks[0].AudioEvents[0];
            Assert.That(
                primaryAudio.EventId,
                Is.EqualTo("presentation.fei.primary.audio.0"));
            Assert.That(primaryAudio.Tick, Is.EqualTo(0));
            Assert.That(
                primaryAudio.BoundGameplayEventId,
                Is.EqualTo("event.fei.primary.attack.0"));
            Assert.That(primaryAudio.Presentation.ClipCount, Is.EqualTo(4));
            Assert.That(
                primaryAudio.Presentation.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                primaryAudio.Presentation.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerSocket));
            Assert.That(
                primaryAudio.Presentation.OwnerSocketId,
                Is.EqualTo("weapon.primary.muzzle"));
            FpgImpactPresentationBundleDefinition primaryImpact =
                primaryExecute.AttackEvents[0].ImpactPresentation;
            Assert.That(primaryImpact, Is.Not.Null);
            Assert.That(
                primaryImpact.EnvironmentAudioOverride,
                Is.Not.Null);
            Assert.That(
                primaryImpact.EnvironmentAudioOverride.ClipCount,
                Is.EqualTo(4));
            Assert.That(
                primaryImpact.EnvironmentAudioOverride.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                primaryImpact.EnvironmentAudioOverride.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerRoot));
            Assert.That(
                primaryImpact.EnvironmentAudioOverride.OwnerSocketId,
                Is.Empty);
            StringAssert.StartsWith(
                "SFX_Fei_Primary_EnvironmentHit_",
                primaryImpact.EnvironmentAudioOverride.Clip.name);

            FpgSkillSequenceDefinition chargeEnter =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.ChargeEnter);
            Assert.That(
                chargeEnter.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                chargeEnter.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.Empty);
            FpgSkillActivePresentationTrackDefinition chargeTrack =
                chargeEnter.ActivePresentationTracks[0];
            Assert.That(chargeTrack.AudioEvents.Count, Is.EqualTo(2));
            FpgAudioPresentationEventDefinition chargeStart =
                chargeTrack.AudioEvents.Single(value =>
                    value.EventId
                        == "presentation.fei.secondary.charge.audio.0");
            FpgAudioPresentationEventDefinition chargeHold =
                chargeTrack.AudioEvents.Single(value =>
                    value.EventId
                        == "presentation.fei.secondary.charge.hold.0");
            AssertChargeAudio(
                chargeStart,
                ChargeSecondaryStartClipPath,
                "SFX_Fei_Secondary_Charge_Start_01",
                1,
                FpgAudioPresentationPlaybackMode.OneShot);
            AssertChargeAudio(
                chargeHold,
                ChargeSecondaryHoldClipPath,
                "SFX_Fei_Secondary_Charge_Hold_01",
                2,
                FpgAudioPresentationPlaybackMode.HeldLoop);

            FpgSkillSequenceDefinition execute =
                FindSequence(immediateSecondary, FpgSkillSequenceKind.Execute);
            Assert.That(
                execute.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                execute.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.execute.attack.0"));
            FpgSkillActivePresentationTrackDefinition immediateTrack =
                execute.ActivePresentationTracks[0];
            Assert.That(
                immediateTrack.TrackId,
                Is.EqualTo("track.fei.secondary.execute.active"));
            Assert.That(immediateTrack.AudioEvents.Count, Is.EqualTo(1));
            FpgAudioPresentationEventDefinition immediateAudio =
                immediateTrack.AudioEvents[0];
            Assert.That(
                immediateAudio.EventId,
                Is.EqualTo("presentation.fei.secondary.execute.audio.0"));
            Assert.That(immediateAudio.Tick, Is.Zero);
            Assert.That(
                immediateAudio.BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.execute.attack.0"));
            Assert.That(immediateAudio.Presentation.ClipCount, Is.EqualTo(5));
            Assert.That(
                immediateAudio.Presentation.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                immediateAudio.Presentation.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerSocket));
            Assert.That(
                immediateAudio.Presentation.OwnerSocketId,
                Is.EqualTo("weapon.secondary.muzzle"));

            for (int index = 0;
                index < immediateAudio.Presentation.ClipCount;
                index++)
            {
                UnityEngine.AudioClip clip =
                    immediateAudio.Presentation.GetClip(index);
                Assert.That(clip, Is.Not.Null);
                StringAssert.StartsWith(
                    "SFX_Fei_Secondary_Immediate_Launch_",
                    clip.name);
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(48000));

                string clipPath = AssetDatabase.GetAssetPath(clip);
                Assert.That(
                    clipPath,
                    Is.EqualTo(ImmediateSecondaryLaunchClipRoot
                        + (index + 1).ToString("00")
                        + ".wav"));
                UnityEditor.AudioImporter importer =
                    AssetImporter.GetAtPath(clipPath)
                    as UnityEditor.AudioImporter;
                Assert.That(importer, Is.Not.Null, clipPath);
                Assert.That(importer.forceToMono, Is.True);
                Assert.That(importer.loadInBackground, Is.False);
                UnityEditor.AudioImporterSampleSettings settings =
                    importer.defaultSampleSettings;
                Assert.That(
                    settings.loadType,
                    Is.EqualTo(
                        UnityEngine.AudioClipLoadType.DecompressOnLoad));
                Assert.That(
                    settings.compressionFormat,
                    Is.EqualTo(
                        UnityEngine.AudioCompressionFormat.PCM));
                Assert.That(
                    settings.sampleRateSetting,
                    Is.EqualTo(
                        UnityEditor.AudioSampleRateSetting.PreserveSampleRate));
                Assert.That(settings.preloadAudioData, Is.True);
            }

            FpgSkillProjectileEventDefinition immediateProjectile =
                execute.ProjectileEvents[0];
            Assert.That(immediateProjectile.CollisionPresentation, Is.Not.Null);
            AssertImpactAudio(
                immediateProjectile.CollisionPresentation.BaseAudio,
                ImmediateSecondaryHitClipPath,
                "SFX_Fei_Secondary_Immediate_Hit_01");
            AssertImpactAudio(
                immediateProjectile.CollisionPresentation
                    .WeakpointAudioOverride,
                ImmediateSecondaryWeakpointClipPath,
                "SFX_Fei_Secondary_Immediate_Weakpoint_01");

            FpgSkillSequenceDefinition release =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Release);
            FpgSkillSequenceDefinition cancel =
                FindSequence(chargeSecondary, FpgSkillSequenceKind.Cancel);
            Assert.That(release.ProjectileEvents.Count, Is.EqualTo(1));
            FpgImpactPresentationBundleDefinition chargeImpact =
                release.ProjectileEvents[0].CollisionPresentation;
            Assert.That(chargeImpact, Is.Not.Null);
            AssertImpactAudio(
                chargeImpact.BaseAudio,
                ChargeSecondaryHitClipPath,
                "SFX_Fei_Secondary_Charge_Hit_02");
            AssertImpactAudio(
                chargeImpact.WeakpointAudioOverride,
                ChargeSecondaryWeakpointClipPath,
                "SFX_Fei_Secondary_Charge_Weakpoint_02");
            Assert.That(
                release.ActivePresentationTracks.Count,
                Is.EqualTo(1));
            Assert.That(
                release.ActivePresentationTracks[0].VfxEvents[0]
                    .BoundGameplayEventId,
                Is.EqualTo("event.fei.secondary.release.attack.0"));
            FpgSkillActivePresentationTrackDefinition releaseTrack =
                release.ActivePresentationTracks[0];
            Assert.That(
                releaseTrack.TrackId,
                Is.EqualTo("track.fei.secondary.release.active"));
            Assert.That(releaseTrack.AudioEvents.Count, Is.EqualTo(1));
            AssertChargeAudioGroup(
                releaseTrack.AudioEvents[0],
                "presentation.fei.secondary.release.audio.0",
                "event.fei.secondary.release.attack.0",
                ChargeSecondaryReleaseClipRoot,
                7,
                2,
                FpgAudioPresentationAnchor.OwnerSocket,
                "weapon.secondary.muzzle");

            Assert.That(cancel.ActivePresentationTracks.Count, Is.EqualTo(1));
            FpgSkillActivePresentationTrackDefinition cancelTrack =
                cancel.ActivePresentationTracks[0];
            Assert.That(
                cancelTrack.TrackId,
                Is.EqualTo("track.fei.secondary.cancel.active"));
            Assert.That(cancelTrack.VfxEvents.Count, Is.Zero);
            Assert.That(cancelTrack.CameraShakeEvents.Count, Is.Zero);
            Assert.That(cancelTrack.AudioEvents.Count, Is.EqualTo(1));
            AssertChargeAudioGroup(
                cancelTrack.AudioEvents[0],
                "presentation.fei.secondary.cancel.audio.0",
                string.Empty,
                ChargeSecondaryCancelClipRoot,
                5,
                0,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            FpgSkillSequenceDefinition reloadExecute =
                FindSequence(reload, FpgSkillSequenceKind.Execute);
            Assert.That(
                reloadExecute.ReloadEvents.Count,
                Is.EqualTo(1));
            Assert.That(
                reloadExecute.ReloadEvents[0].SuccessAnimationName,
                Is.EqualTo("u1_buff_ready"));

            Assert.That(reloadExecute.ActivePresentationTracks.Count, Is.EqualTo(1));
            FpgSkillActivePresentationTrackDefinition reloadTrack =
                reloadExecute.ActivePresentationTracks[0];
            Assert.That(reloadTrack.TrackId, Is.EqualTo("track.fei.reload.active"));
            Assert.That(reloadTrack.VfxEvents.Count, Is.Zero);
            Assert.That(reloadTrack.CameraShakeEvents.Count, Is.Zero);
            Assert.That(reloadTrack.AudioEvents.Count, Is.EqualTo(1));

            FpgAudioPresentationEventDefinition reloadAudio =
                reloadTrack.AudioEvents[0];
            Assert.That(
                reloadAudio.EventId,
                Is.EqualTo("presentation.fei.reload.commit.audio.0"));
            Assert.That(reloadAudio.Tick, Is.EqualTo(40));
            Assert.That(
                reloadAudio.BoundGameplayEventId,
                Is.EqualTo("event.fei.reload.commit.0"));
            Assert.That(reloadAudio.Presentation.ClipCount, Is.EqualTo(5));
            Assert.That(
                reloadAudio.Presentation.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                reloadAudio.Presentation.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerRoot));
            Assert.That(reloadAudio.Presentation.OwnerSocketId, Is.Empty);

            for (int index = 0; index < reloadAudio.Presentation.ClipCount; index++)
            {
                UnityEngine.AudioClip clip =
                    reloadAudio.Presentation.GetClip(index);
                Assert.That(clip, Is.Not.Null);
                StringAssert.StartsWith(
                    "SFX_Fei_Reload_",
                    clip.name);
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(48000));

                string clipPath = AssetDatabase.GetAssetPath(clip);
                Assert.That(
                    clipPath,
                    Is.EqualTo(ReloadClipRoot
                        + (index + 1).ToString("00")
                        + ".wav"));
                UnityEditor.AudioImporter importer =
                    AssetImporter.GetAtPath(clipPath) as UnityEditor.AudioImporter;
                Assert.That(importer, Is.Not.Null, clipPath);
                Assert.That(importer.forceToMono, Is.True);
                Assert.That(importer.loadInBackground, Is.False);
                UnityEditor.AudioImporterSampleSettings settings =
                    importer.defaultSampleSettings;
                Assert.That(
                    settings.loadType,
                    Is.EqualTo(UnityEngine.AudioClipLoadType.DecompressOnLoad));
                Assert.That(
                    settings.compressionFormat,
                    Is.EqualTo(UnityEngine.AudioCompressionFormat.PCM));
                Assert.That(
                    settings.sampleRateSetting,
                    Is.EqualTo(UnityEditor.AudioSampleRateSetting.PreserveSampleRate));
                Assert.That(settings.preloadAudioData, Is.True);
            }
        }

        private static void AssertImpactAudio(
            FpgAudioPresentationDefinition audio,
            string expectedPath,
            string expectedName)
        {
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.ClipCount, Is.EqualTo(1));
            Assert.That(
                audio.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                audio.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerRoot));
            Assert.That(audio.OwnerSocketId, Is.Empty);
            Assert.That(audio.MinDistance, Is.EqualTo(1f));
            Assert.That(audio.MaxDistance, Is.EqualTo(20f));
            Assert.That(audio.Clip.name, Is.EqualTo(expectedName));
            Assert.That(AssetDatabase.GetAssetPath(audio.Clip), Is.EqualTo(expectedPath));
            Assert.That(audio.Clip.channels, Is.EqualTo(1));
            Assert.That(audio.Clip.frequency, Is.EqualTo(48000));

            AudioImporter importer =
                AssetImporter.GetAtPath(expectedPath) as AudioImporter;
            Assert.That(importer, Is.Not.Null, expectedPath);
            Assert.That(importer.forceToMono, Is.True);
            Assert.That(importer.loadInBackground, Is.False);
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            Assert.That(
                settings.loadType,
                Is.EqualTo(UnityEngine.AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                settings.compressionFormat,
                Is.EqualTo(UnityEngine.AudioCompressionFormat.PCM));
            Assert.That(
                settings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(settings.preloadAudioData, Is.True);
        }

        private static void AssertChargeAudio(
            FpgAudioPresentationEventDefinition audioEvent,
            string expectedPath,
            string expectedName,
            int expectedOrdinal,
            FpgAudioPresentationPlaybackMode expectedPlaybackMode)
        {
            Assert.That(audioEvent.Tick, Is.Zero);
            Assert.That(audioEvent.AuthoredOrdinal, Is.EqualTo(expectedOrdinal));
            Assert.That(audioEvent.BoundGameplayEventId, Is.Empty);
            FpgAudioPresentationDefinition audio = audioEvent.Presentation;
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.ClipCount, Is.EqualTo(1));
            Assert.That(audio.Clip.name, Is.EqualTo(expectedName));
            Assert.That(
                AssetDatabase.GetAssetPath(audio.Clip),
                Is.EqualTo(expectedPath));
            Assert.That(
                audio.PlaybackMode,
                Is.EqualTo(expectedPlaybackMode));
            Assert.That(
                audio.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(
                audio.Anchor,
                Is.EqualTo(FpgAudioPresentationAnchor.OwnerSocket));
            Assert.That(
                audio.OwnerSocketId,
                Is.EqualTo("weapon.secondary.muzzle"));
            Assert.That(audio.Clip.channels, Is.EqualTo(1));
            Assert.That(audio.Clip.frequency, Is.EqualTo(48000));

            AudioImporter importer =
                AssetImporter.GetAtPath(expectedPath) as AudioImporter;
            Assert.That(importer, Is.Not.Null, expectedPath);
            Assert.That(importer.forceToMono, Is.True);
            Assert.That(importer.loadInBackground, Is.False);
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            Assert.That(
                settings.loadType,
                Is.EqualTo(UnityEngine.AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                settings.compressionFormat,
                Is.EqualTo(UnityEngine.AudioCompressionFormat.PCM));
            Assert.That(
                settings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(settings.preloadAudioData, Is.True);
        }

        private static void AssertChargeAudioGroup(
            FpgAudioPresentationEventDefinition audioEvent,
            string expectedEventId,
            string expectedBoundGameplayEventId,
            string expectedClipRoot,
            int expectedClipCount,
            int expectedOrdinal,
            FpgAudioPresentationAnchor expectedAnchor,
            string expectedSocketId)
        {
            Assert.That(audioEvent.EventId, Is.EqualTo(expectedEventId));
            Assert.That(audioEvent.Tick, Is.Zero);
            Assert.That(audioEvent.AuthoredOrdinal, Is.EqualTo(expectedOrdinal));
            Assert.That(
                audioEvent.BoundGameplayEventId,
                Is.EqualTo(expectedBoundGameplayEventId));
            FpgAudioPresentationDefinition audio = audioEvent.Presentation;
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.ClipCount, Is.EqualTo(expectedClipCount));
            Assert.That(
                audio.PlaybackMode,
                Is.EqualTo(FpgAudioPresentationPlaybackMode.OneShot));
            Assert.That(
                audio.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(audio.Anchor, Is.EqualTo(expectedAnchor));
            Assert.That(audio.OwnerSocketId, Is.EqualTo(expectedSocketId));

            for (int index = 0; index < expectedClipCount; index++)
            {
                UnityEngine.AudioClip clip = audio.GetClip(index);
                Assert.That(clip, Is.Not.Null);
                string expectedPath = expectedClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                Assert.That(
                    AssetDatabase.GetAssetPath(clip),
                    Is.EqualTo(expectedPath));
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(48000));

                AudioImporter importer =
                    AssetImporter.GetAtPath(expectedPath) as AudioImporter;
                Assert.That(importer, Is.Not.Null, expectedPath);
                Assert.That(importer.forceToMono, Is.True);
                Assert.That(importer.loadInBackground, Is.False);
                AudioImporterSampleSettings settings =
                    importer.defaultSampleSettings;
                Assert.That(
                    settings.loadType,
                    Is.EqualTo(UnityEngine.AudioClipLoadType.DecompressOnLoad));
                Assert.That(
                    settings.compressionFormat,
                    Is.EqualTo(UnityEngine.AudioCompressionFormat.PCM));
                Assert.That(
                    settings.sampleRateSetting,
                    Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
                Assert.That(settings.preloadAudioData, Is.True);
            }
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
            AssertMuzzleVfxPresentationEquivalent(
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
            ulong immediatePresentationHash =
                ResolveProjectilePresentationContentHash(
                    compiledImmediateSequence,
                    compiledImmediateEvent.ActionIndex);
            ulong chargedPresentationHash =
                ResolveProjectilePresentationContentHash(
                    compiledChargedSequence,
                    compiledChargedEvent.ActionIndex);
            Assert.That(immediatePresentationHash, Is.Not.Zero);
            Assert.That(chargedPresentationHash, Is.Not.Zero);
            Assert.That(
                immediatePresentationHash,
                Is.Not.EqualTo(chargedPresentationHash),
                "Secondary modes share gameplay payloads but own independent presentation content.");
        }

        [Test]
        public void FeiSecondaryProjectilePresentationKeepsOffsetsEditableAndImpactAlive()
        {
            const string projectilePath =
                "Assets/FPGDemo/Presentation/Characters/Players/Fei/VFX/PF_FPG_Fei_Secondary_Projectile.prefab";
            const string impactPath =
                "Assets/FPGDemo/Presentation/Characters/Players/Fei/VFX/PF_FPG_Fei_Secondary_Hit.prefab";
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
            AssertImpactVisualPresentationEquivalent(
                expected.CollisionPresentation,
                actual.CollisionPresentation,
                "projectile collision presentation");
        }

        private static void AssertMuzzleVfxPresentationEquivalent(
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

        private static void AssertImpactVisualPresentationEquivalent(
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
            AssertCameraShakePresentationEquivalent(
                expected.BaseCameraShake,
                actual.BaseCameraShake,
                context + " base camera shake");
            AssertVfxPresentationEquivalent(
                expected.WeakpointVfxOverride,
                actual.WeakpointVfxOverride,
                context + " weakpoint VFX");
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
