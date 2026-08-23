using System.Collections.Generic;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatAudioBankTests
    {

        [Test]
        public void RequiredCueSetLocksEveryD0CombatFeedbackEvent()
        {
            CombatAudioCue[] expected =
            {
                CombatAudioCue.EnemyFastThreatTelegraph,
                CombatAudioCue.EnemyFastThreatRelease,
                CombatAudioCue.EnemyInterceptableThreatTelegraph,
                CombatAudioCue.EnemyInterceptableThreatRelease,
                CombatAudioCue.EnemyHeavyThreatTelegraph,
                CombatAudioCue.EnemyHeavyThreatRelease,
                CombatAudioCue.PlayerDamaged,
                CombatAudioCue.PlayerBarrierBroken,
                CombatAudioCue.EnemyBreak,
                CombatAudioCue.Victory,
                CombatAudioCue.Defeat,
                CombatAudioCue.ReticleTargetLock,
                CombatAudioCue.EnemyDangerTick,
                CombatAudioCue.RoomEntered,
                CombatAudioCue.ExitUnlocked,
                CombatAudioCue.ExitConfirmed,
                CombatAudioCue.InteractionFocus,
                CombatAudioCue.InteractionConfirm,
                CombatAudioCue.InteractionReject,
                CombatAudioCue.EnemySpawn,
                CombatAudioCue.EnemyDeath
            };

            Assert.That(CombatAudioBank.RequiredCueCount, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(CombatAudioBank.GetRequiredCue(index), Is.EqualTo(expected[index]));
            }
        }

        [Test]
        public void RequiredCuePoliciesAreFiniteUniqueAndFitTheFixedVoicePool()
        {
            HashSet<CombatAudioCue> seen = new HashSet<CombatAudioCue>();
            for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
            {
                CombatAudioCuePolicy policy = CombatAudioBank.GetRequiredCuePolicy(index);
                Assert.That(policy.Cue, Is.EqualTo(CombatAudioBank.GetRequiredCue(index)));
                Assert.That(seen.Add(policy.Cue), Is.True);
                Assert.That(policy.Priority, Is.InRange(0, 256));
                Assert.That(float.IsNaN(policy.Volume), Is.False);
                Assert.That(float.IsInfinity(policy.Volume), Is.False);
                Assert.That(policy.Volume, Is.InRange(0f, 1f));
                Assert.That(float.IsNaN(policy.CooldownSeconds), Is.False);
                Assert.That(float.IsInfinity(policy.CooldownSeconds), Is.False);
                Assert.That(policy.CooldownSeconds, Is.GreaterThanOrEqualTo(0f));
                Assert.That(policy.MaxConcurrentVoices, Is.InRange(
                    1,
                    CombatAudioBank.DefaultConcurrentVoiceLimit));
                Assert.That(
                    System.Enum.IsDefined(typeof(CombatAudioBus), policy.Bus),
                    Is.True);
                Assert.That(
                    System.Enum.IsDefined(
                        typeof(FpgAudioPresentationSpace),
                        policy.Space),
                    Is.True);
                Assert.That(policy.MinDistance, Is.GreaterThan(0f));
                Assert.That(
                    policy.MaxDistance,
                    Is.GreaterThanOrEqualTo(policy.MinDistance));
            }


            CombatAudioCuePolicy dangerTick = FindRequiredPolicy(
                CombatAudioCue.EnemyDangerTick);
            Assert.That(dangerTick.Priority, Is.EqualTo(85));
            Assert.That(
                dangerTick.CooldownSeconds,
                Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(
                dangerTick.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));

            CombatAudioCuePolicy heavyTelegraph = FindRequiredPolicy(
                CombatAudioCue.EnemyHeavyThreatTelegraph);
            Assert.That(heavyTelegraph.Priority, Is.EqualTo(80));
            CombatAudioCuePolicy heavyRelease = FindRequiredPolicy(
                CombatAudioCue.EnemyHeavyThreatRelease);
            Assert.That(heavyRelease.Priority, Is.EqualTo(65));

            CombatAudioCuePolicy enemySpawn = FindRequiredPolicy(
                CombatAudioCue.EnemySpawn);
            Assert.That(enemySpawn.Priority, Is.EqualTo(30));
            Assert.That(enemySpawn.MaxConcurrentVoices, Is.EqualTo(3));
            Assert.That(
                enemySpawn.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));

            CombatAudioCuePolicy enemyDeath = FindRequiredPolicy(
                CombatAudioCue.EnemyDeath);
            Assert.That(enemyDeath.Priority, Is.EqualTo(45));
            Assert.That(enemyDeath.MaxConcurrentVoices, Is.EqualTo(3));
            Assert.That(
                enemyDeath.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
        }

        [Test]
        public void ForestBankAssetIsCompleteAndMappingReady()
        {
            const string Path =
                "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset";
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(Path);

            Assert.That(bank, Is.Not.Null, Path);
            Assert.That(
                bank.TryValidateMapping(out string error),
                Is.True,
                error);
            Assert.That(
                bank.CueEntryCount,
                Is.EqualTo(CombatAudioBank.RequiredCueCount));
            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.EnemyFastThreatTelegraph,
                    out CombatAudioCueEntry telegraph),
                Is.True);
            Assert.That(telegraph.ClipCount, Is.EqualTo(3));
            Assert.That(
                telegraph.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(telegraph.Priority, Is.EqualTo(60));
            Assert.That(
                telegraph.CooldownSeconds,
                Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(telegraph.MaxConcurrentVoices, Is.EqualTo(1));
            for (int index = 0; index < telegraph.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    "SFX_Burstbug_Fast_Telegraph_",
                    telegraph.GetClip(index).name);
            }

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.EnemyFastThreatRelease,
                    out CombatAudioCueEntry release),
                Is.True);
            Assert.That(release.ClipCount, Is.EqualTo(4));
            Assert.That(
                release.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(release.Priority, Is.EqualTo(50));
            Assert.That(
                release.CooldownSeconds,
                Is.EqualTo(0.10f).Within(0.0001f));
            Assert.That(release.MaxConcurrentVoices, Is.EqualTo(1));
            for (int index = 0; index < release.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    "SFX_Burstbug_Fast_Release_",
                    release.GetClip(index).name);
            }

            AssertWorldCueGroup(
                bank,
                CombatAudioCue.EnemyHeavyThreatTelegraph,
                4,
                "SFX_Burstbug_Heavy_Telegraph_",
                80,
                0.20f);
            AssertWorldCueGroup(
                bank,
                CombatAudioCue.EnemyHeavyThreatRelease,
                4,
                "SFX_Burstbug_Heavy_Release_",
                65,
                0.12f);
            AssertWorldCueGroup(
                bank,
                CombatAudioCue.EnemyDangerTick,
                3,
                "SFX_Burstbug_Heavy_DangerTick_",
                85,
                0.12f);
            AssertWorldCueGroup(
                bank,
                CombatAudioCue.EnemySpawn,
                4,
                "SFX_Enemy_Spawn_",
                30,
                0.10f,
                3);
            AssertWorldCueGroup(
                bank,
                CombatAudioCue.EnemyDeath,
                6,
                "SFX_Enemy_Death_",
                45,
                0.10f,
                3);

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.PlayerBarrierBroken,
                    out CombatAudioCueEntry barrierBroken),
                Is.True);
            Assert.That(barrierBroken.ClipCount, Is.EqualTo(3));
            Assert.That(
                barrierBroken.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(barrierBroken.Bus, Is.EqualTo(CombatAudioBus.Sfx));
            Assert.That(barrierBroken.Priority, Is.EqualTo(15));
            Assert.That(
                barrierBroken.CooldownSeconds,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(barrierBroken.MaxConcurrentVoices, Is.EqualTo(1));
            for (int index = 0; index < barrierBroken.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    "SFX_Player_BarrierBreak_",
                    barrierBroken.GetClip(index).name);
            }

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.EnemyBreak,
                    out CombatAudioCueEntry enemyBreak),
                Is.True);
            Assert.That(enemyBreak.ClipCount, Is.EqualTo(3));
            Assert.That(
                enemyBreak.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(enemyBreak.Bus, Is.EqualTo(CombatAudioBus.Sfx));
            Assert.That(enemyBreak.Priority, Is.EqualTo(20));
            Assert.That(
                enemyBreak.CooldownSeconds,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(enemyBreak.MaxConcurrentVoices, Is.EqualTo(1));
            for (int index = 0; index < enemyBreak.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    "SFX_Enemy_Break_",
                    enemyBreak.GetClip(index).name);
            }

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.RoomEntered,
                    out CombatAudioCueEntry roomEntered),
                Is.True);
            Assert.That(roomEntered.ClipCount, Is.EqualTo(1));
            Assert.That(roomEntered.Clip.name, Is.EqualTo("UI_Room_Entered_03"));
            Assert.That(
                roomEntered.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(roomEntered.Bus, Is.EqualTo(CombatAudioBus.Ui));
            Assert.That(roomEntered.Priority, Is.EqualTo(120));
            Assert.That(
                roomEntered.CooldownSeconds,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(roomEntered.MaxConcurrentVoices, Is.EqualTo(1));

            string roomEnteredPath =
                AssetDatabase.GetAssetPath(roomEntered.Clip);
            AudioImporter importer =
                AssetImporter.GetAtPath(roomEnteredPath) as AudioImporter;
            Assert.That(importer, Is.Not.Null, roomEnteredPath);
            Assert.That(importer.forceToMono, Is.True);
            Assert.That(importer.loadInBackground, Is.False);
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            Assert.That(
                settings.loadType,
                Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                settings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(
                settings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(settings.preloadAudioData, Is.True);

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.ExitUnlocked,
                    out CombatAudioCueEntry exitUnlocked),
                Is.True);
            Assert.That(exitUnlocked.ClipCount, Is.EqualTo(1));
            Assert.That(
                exitUnlocked.Clip.name,
                Is.EqualTo("UI_Exit_Unlocked_01"));
            Assert.That(
                exitUnlocked.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(exitUnlocked.Bus, Is.EqualTo(CombatAudioBus.Ui));
            Assert.That(exitUnlocked.Priority, Is.EqualTo(120));
            Assert.That(
                exitUnlocked.CooldownSeconds,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(exitUnlocked.MaxConcurrentVoices, Is.EqualTo(1));

            string exitUnlockedPath =
                AssetDatabase.GetAssetPath(exitUnlocked.Clip);
            AudioImporter exitUnlockedImporter =
                AssetImporter.GetAtPath(exitUnlockedPath) as AudioImporter;
            Assert.That(
                exitUnlockedImporter,
                Is.Not.Null,
                exitUnlockedPath);
            Assert.That(exitUnlockedImporter.forceToMono, Is.True);
            Assert.That(exitUnlockedImporter.loadInBackground, Is.False);
            AudioImporterSampleSettings exitUnlockedSettings =
                exitUnlockedImporter.defaultSampleSettings;
            Assert.That(
                exitUnlockedSettings.loadType,
                Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                exitUnlockedSettings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(
                exitUnlockedSettings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(exitUnlockedSettings.preloadAudioData, Is.True);

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.ExitConfirmed,
                    out CombatAudioCueEntry exitConfirmed),
                Is.True);
            Assert.That(exitConfirmed.ClipCount, Is.EqualTo(1));
            Assert.That(
                exitConfirmed.Clip.name,
                Is.EqualTo("UI_Exit_Confirmed_01"));
            Assert.That(
                exitConfirmed.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(exitConfirmed.Bus, Is.EqualTo(CombatAudioBus.Ui));
            Assert.That(exitConfirmed.Priority, Is.EqualTo(110));
            Assert.That(
                exitConfirmed.CooldownSeconds,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(exitConfirmed.MaxConcurrentVoices, Is.EqualTo(1));

            string exitConfirmedPath =
                AssetDatabase.GetAssetPath(exitConfirmed.Clip);
            AudioImporter exitConfirmedImporter =
                AssetImporter.GetAtPath(exitConfirmedPath) as AudioImporter;
            Assert.That(
                exitConfirmedImporter,
                Is.Not.Null,
                exitConfirmedPath);
            Assert.That(exitConfirmedImporter.forceToMono, Is.True);
            Assert.That(exitConfirmedImporter.loadInBackground, Is.False);
            AudioImporterSampleSettings exitConfirmedSettings =
                exitConfirmedImporter.defaultSampleSettings;
            Assert.That(
                exitConfirmedSettings.loadType,
                Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                exitConfirmedSettings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(
                exitConfirmedSettings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(exitConfirmedSettings.preloadAudioData, Is.True);

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.InteractionFocus,
                    out CombatAudioCueEntry interactionFocus),
                Is.True);
            Assert.That(interactionFocus.ClipCount, Is.EqualTo(1));
            Assert.That(
                interactionFocus.Clip.name,
                Is.EqualTo("UI_Interaction_Focus_01"));
            Assert.That(
                interactionFocus.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(interactionFocus.Bus, Is.EqualTo(CombatAudioBus.Ui));
            Assert.That(interactionFocus.Priority, Is.EqualTo(130));
            Assert.That(
                interactionFocus.CooldownSeconds,
                Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(interactionFocus.MaxConcurrentVoices, Is.EqualTo(1));

            string interactionFocusPath =
                AssetDatabase.GetAssetPath(interactionFocus.Clip);
            AudioImporter interactionFocusImporter =
                AssetImporter.GetAtPath(interactionFocusPath) as AudioImporter;
            Assert.That(
                interactionFocusImporter,
                Is.Not.Null,
                interactionFocusPath);
            Assert.That(interactionFocusImporter.forceToMono, Is.True);
            Assert.That(interactionFocusImporter.loadInBackground, Is.False);
            AudioImporterSampleSettings interactionFocusSettings =
                interactionFocusImporter.defaultSampleSettings;
            Assert.That(
                interactionFocusSettings.loadType,
                Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                interactionFocusSettings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(
                interactionFocusSettings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(interactionFocusSettings.preloadAudioData, Is.True);

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.InteractionConfirm,
                    out CombatAudioCueEntry interactionConfirm),
                Is.True);
            Assert.That(interactionConfirm.ClipCount, Is.EqualTo(3));
            Assert.That(
                interactionConfirm.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(interactionConfirm.Bus, Is.EqualTo(CombatAudioBus.Ui));
            Assert.That(interactionConfirm.Priority, Is.EqualTo(125));
            Assert.That(
                interactionConfirm.CooldownSeconds,
                Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(interactionConfirm.MaxConcurrentVoices, Is.EqualTo(1));
            for (int index = 0; index < interactionConfirm.ClipCount; index++)
            {
                AudioClip confirmClip = interactionConfirm.GetClip(index);
                Assert.That(
                    confirmClip.name,
                    Is.EqualTo(
                        "UI_Interaction_Confirm_"
                        + (index + 1).ToString("00")));

                string confirmPath = AssetDatabase.GetAssetPath(confirmClip);
                AudioImporter confirmImporter =
                    AssetImporter.GetAtPath(confirmPath) as AudioImporter;
                Assert.That(confirmImporter, Is.Not.Null, confirmPath);
                Assert.That(confirmImporter.forceToMono, Is.True);
                Assert.That(confirmImporter.loadInBackground, Is.False);
                AudioImporterSampleSettings confirmSettings =
                    confirmImporter.defaultSampleSettings;
                Assert.That(
                    confirmSettings.loadType,
                    Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
                Assert.That(
                    confirmSettings.compressionFormat,
                    Is.EqualTo(AudioCompressionFormat.PCM));
                Assert.That(
                    confirmSettings.sampleRateSetting,
                    Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
                Assert.That(confirmSettings.preloadAudioData, Is.True);
            }

            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.InteractionReject,
                    out CombatAudioCueEntry interactionReject),
                Is.True);
            Assert.That(interactionReject.ClipCount, Is.EqualTo(1));
            Assert.That(
                interactionReject.Clip.name,
                Is.EqualTo("UI_Interaction_Reject_01"));
            Assert.That(
                interactionReject.Space,
                Is.EqualTo(FpgAudioPresentationSpace.TwoDimensional));
            Assert.That(interactionReject.Bus, Is.EqualTo(CombatAudioBus.Ui));
            Assert.That(interactionReject.Priority, Is.EqualTo(125));
            Assert.That(
                interactionReject.CooldownSeconds,
                Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(interactionReject.MaxConcurrentVoices, Is.EqualTo(1));

            string interactionRejectPath =
                AssetDatabase.GetAssetPath(interactionReject.Clip);
            AudioImporter interactionRejectImporter =
                AssetImporter.GetAtPath(interactionRejectPath) as AudioImporter;
            Assert.That(
                interactionRejectImporter,
                Is.Not.Null,
                interactionRejectPath);
            Assert.That(interactionRejectImporter.forceToMono, Is.True);
            Assert.That(interactionRejectImporter.loadInBackground, Is.False);
            AudioImporterSampleSettings interactionRejectSettings =
                interactionRejectImporter.defaultSampleSettings;
            Assert.That(
                interactionRejectSettings.loadType,
                Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(
                interactionRejectSettings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.PCM));
            Assert.That(
                interactionRejectSettings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(interactionRejectSettings.preloadAudioData, Is.True);
        }

        [Test]
        public void ForestBankLeavesTargetLockCueEmptyWhenExcludedByDesign()
        {
            const string Path =
                "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset";
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(Path);

            Assert.That(bank, Is.Not.Null, Path);
            Assert.That(
                bank.TryGetCueEntry(
                    CombatAudioCue.ReticleTargetLock,
                    out CombatAudioCueEntry targetLock),
                Is.True);
            Assert.That(targetLock.ClipCount, Is.EqualTo(0));
        }

        [Test]
        public void CompleteBankMapsEveryRequiredCueWithStaticPlaybackPolicy()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip clip = AudioClip.Create("CombatAudioBankTest", 128, 1, 44100, false);
            try
            {
                ConfigureCompleteBank(bank, clip, CombatAudioBank.DefaultConcurrentVoiceLimit);

                Assert.That(bank.TryValidate(out string error), Is.True, error);
                Assert.That(
                    bank.ConcurrentVoiceLimit,
                    Is.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));
                Assert.That(bank.CueEntryCount, Is.EqualTo(CombatAudioBank.RequiredCueCount));

                for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
                {
                    CombatAudioCue cue = CombatAudioBank.GetRequiredCue(index);
                    Assert.That(bank.TryGetCueEntry(cue, out CombatAudioCueEntry entry), Is.True);
                    Assert.That(entry.Clip, Is.SameAs(clip));
                    Assert.That(entry.Priority, Is.EqualTo(32 + index));
                    Assert.That(entry.Volume, Is.EqualTo(0.25f + index * 0.01f).Within(0.0001f));
                    Assert.That(entry.CooldownSeconds, Is.EqualTo(index * 0.01f).Within(0.0001f));
                    Assert.That(entry.MaxConcurrentVoices, Is.EqualTo(1 + index % 2));
                }

                Assert.That(bank.TryGetCueEntry(CombatAudioCue.None, out _), Is.False);
                Assert.That(bank.TryGetCueEntry(CombatAudioCue.Count, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void CueEntryDefaultsToSafeFullVolume()
        {
            CombatAudioCueEntry entry = new CombatAudioCueEntry();

            Assert.That(entry.Volume, Is.EqualTo(1f));
        }

        [Test]
        public void CueEntryAcceptsUniqueVariationsAndRejectsDuplicates()
        {
            CombatAudioBank bank =
                ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip primary = AudioClip.Create(
                "CombatAudioPrimary",
                128,
                1,
                44100,
                false);
            AudioClip alternate = AudioClip.Create(
                "CombatAudioAlternate",
                128,
                1,
                44100,
                false);
            try
            {
                ConfigureCompleteBank(
                    bank,
                    primary,
                    CombatAudioBank.DefaultConcurrentVoiceLimit);
                SerializedObject serialized = new SerializedObject(bank);
                SerializedProperty firstEntry = serialized
                    .FindProperty("cueEntries")
                    .GetArrayElementAtIndex(0);
                SerializedProperty variations =
                    firstEntry.FindPropertyRelative("variations");
                variations.arraySize = 1;
                variations.GetArrayElementAtIndex(0).objectReferenceValue =
                    alternate;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(bank.TryValidate(out _), Is.True);
                Assert.That(
                    bank.TryGetCueEntry(
                        CombatAudioBank.GetRequiredCue(0),
                        out CombatAudioCueEntry entry),
                    Is.True);
                Assert.That(entry.ClipCount, Is.EqualTo(2));
                Assert.That(entry.GetClip(1), Is.SameAs(alternate));

                variations.GetArrayElementAtIndex(0).objectReferenceValue =
                    primary;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(bank.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("duplicate clip variation"));
            }
            finally
            {
                Object.DestroyImmediate(alternate);
                Object.DestroyImmediate(primary);
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void BankRejectsCueVolumesOutsideTheSafeUnityRange()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip clip = AudioClip.Create("CombatAudioBankVolume", 128, 1, 44100, false);
            try
            {
                ConfigureCompleteBank(bank, clip, CombatAudioBank.DefaultConcurrentVoiceLimit);
                SerializedObject serialized = new SerializedObject(bank);
                SerializedProperty volume = serialized
                    .FindProperty("cueEntries")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("volume");

                volume.floatValue = -0.01f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(bank.TryValidate(out string belowRangeError), Is.False);
                Assert.That(belowRangeError, Does.Contain("volume"));

                volume.floatValue = 1.01f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(bank.TryValidate(out string aboveRangeError), Is.False);
                Assert.That(aboveRangeError, Does.Contain("volume"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void G0MappingAllowsMissingClipsButG3PlaybackValidationRejectsThem()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            try
            {
                ConfigureCompleteBank(
                    bank,
                    null,
                    CombatAudioBank.DefaultConcurrentVoiceLimit);

                Assert.That(bank.TryValidateMapping(out string mappingError), Is.True, mappingError);
                Assert.That(bank.TryValidatePlayback(out string playbackError), Is.False);
                Assert.That(playbackError, Does.Contain("must reference an AudioClip"));
                Assert.That(bank.TryValidate(out string strictError), Is.False);
                Assert.That(strictError, Does.Contain("must reference an AudioClip"));
            }
            finally
            {
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void BankRejectsARequiredCueThatIsNotMapped()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip clip = AudioClip.Create("CombatAudioBankMissingCue", 128, 1, 44100, false);
            try
            {
                ConfigureCompleteBank(
                    bank,
                    clip,
                    CombatAudioBank.DefaultConcurrentVoiceLimit,
                    CombatAudioBank.RequiredCueCount - 1);

                Assert.That(bank.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("does not map the required cue"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void BankRejectsPerCueConcurrencyAboveItsFixedSourceLimit()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip clip = AudioClip.Create("CombatAudioBankConcurrency", 128, 1, 44100, false);
            try
            {
                ConfigureCompleteBank(bank, clip, 2);
                SerializedObject serialized = new SerializedObject(bank);
                SerializedProperty firstEntry = serialized
                    .FindProperty("cueEntries")
                    .GetArrayElementAtIndex(0);
                firstEntry.FindPropertyRelative("maxConcurrentVoices").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(bank.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("max concurrent voices cannot exceed the bank limit"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void BankRejectsDuplicateCueMappings()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip clip = AudioClip.Create("CombatAudioBankDuplicateCue", 128, 1, 44100, false);
            try
            {
                ConfigureCompleteBank(bank, clip, CombatAudioBank.DefaultConcurrentVoiceLimit);
                SerializedObject serialized = new SerializedObject(bank);
                SerializedProperty entries = serialized.FindProperty("cueEntries");
                entries.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("cue")
                    .intValue = (int)CombatAudioCue.EnemyFastThreatTelegraph;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(bank.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("appears more than once"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(bank);
            }
        }



        private static void ConfigureCompleteBank(
            CombatAudioBank bank,
            AudioClip clip,
            int concurrentVoiceLimit,
            int entryCount = -1)
        {
            int resolvedEntryCount = entryCount < 0
                ? CombatAudioBank.RequiredCueCount
                : entryCount;
            SerializedObject serialized = new SerializedObject(bank);
            serialized.FindProperty("concurrentVoiceLimit").intValue = concurrentVoiceLimit;
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            entries.arraySize = resolvedEntryCount;
            for (int index = 0; index < resolvedEntryCount; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("cue").intValue =
                    (int)CombatAudioBank.GetRequiredCue(index);
                entry.FindPropertyRelative("clip").objectReferenceValue = clip;
                entry.FindPropertyRelative("priority").intValue = 32 + index;
                entry.FindPropertyRelative("volume").floatValue = 0.25f + index * 0.01f;
                entry.FindPropertyRelative("cooldownSeconds").floatValue = index * 0.01f;
                entry.FindPropertyRelative("maxConcurrentVoices").intValue = 1 + index % 2;
                CombatAudioCuePolicy policy =
                    CombatAudioBank.GetRequiredCuePolicy(index);
                entry.FindPropertyRelative("bus").intValue = (int)policy.Bus;
                entry.FindPropertyRelative("space").intValue = (int)policy.Space;
                entry.FindPropertyRelative("minDistance").floatValue =
                    policy.MinDistance;
                entry.FindPropertyRelative("maxDistance").floatValue =
                    policy.MaxDistance;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CombatAudioCuePolicy FindRequiredPolicy(
            CombatAudioCue cue)
        {
            for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
            {
                CombatAudioCuePolicy policy =
                    CombatAudioBank.GetRequiredCuePolicy(index);
                if (policy.Cue == cue)
                {
                    return policy;
                }
            }

            Assert.Fail("Missing required cue policy: " + cue);
            return default(CombatAudioCuePolicy);
        }

        private static void AssertWorldCueGroup(
            CombatAudioBank bank,
            CombatAudioCue cue,
            int expectedCount,
            string expectedPrefix,
            int expectedPriority,
            float expectedCooldown,
            int expectedMaxConcurrentVoices = 1)
        {
            Assert.That(
                bank.TryGetCueEntry(cue, out CombatAudioCueEntry entry),
                Is.True,
                cue.ToString());
            Assert.That(entry.ClipCount, Is.EqualTo(expectedCount), cue.ToString());
            Assert.That(
                entry.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned),
                cue.ToString());
            Assert.That(entry.Bus, Is.EqualTo(CombatAudioBus.Sfx), cue.ToString());
            Assert.That(entry.Priority, Is.EqualTo(expectedPriority), cue.ToString());
            Assert.That(
                entry.CooldownSeconds,
                Is.EqualTo(expectedCooldown).Within(0.0001f),
                cue.ToString());
            Assert.That(
                entry.MaxConcurrentVoices,
                Is.EqualTo(expectedMaxConcurrentVoices),
                cue.ToString());
            for (int index = 0; index < entry.ClipCount; index++)
            {
                StringAssert.StartsWith(
                    expectedPrefix,
                    entry.GetClip(index).name,
                    cue + " variation " + index);
            }
        }
    }
}
