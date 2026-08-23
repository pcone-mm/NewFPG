using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class CombatAudioPresenterTests
    {
        [Test]
        public void ForestAudioAssetsExposeTheRequiredMixerBuses()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(
                "Assets/FPGDemo/Audio/FPG_AudioMixer.mixer");
            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(
                    "Assets/FPGDemo/Audio/ForestAudioProfile.asset");

            Assert.That(mixer, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryValidateMapping(out string profileError), Is.True, profileError);

            string[] requiredGroups =
                { "Master", "Music", "Ambience", "SFX", "UI", "Voice" };
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(string.Empty);
            for (int index = 0; index < requiredGroups.Length; index++)
            {
                Assert.That(
                    System.Array.Exists(groups, group =>
                        group != null && group.name == requiredGroups[index]),
                    Is.True,
                    requiredGroups[index]);
            }
        }

        [Test]
        public void ForestBurstbugVolleyCuesUseApprovedVariationGroups()
        {
            const string BankPath =
                "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset";
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(BankPath);

            Assert.That(bank, Is.Not.Null, BankPath);
            AssertApprovedVolleyCue(
                bank,
                CombatAudioCue.EnemyInterceptableThreatTelegraph,
                "SFX_Burstbug_Volley_Telegraph_",
                3);
            AssertApprovedVolleyCue(
                bank,
                CombatAudioCue.EnemyInterceptableThreatRelease,
                "SFX_Burstbug_Volley_Release_",
                5);
        }

        [Test]
        public void ForestProfileUsesApprovedStreamingAmbienceVariations()
        {
            const string ProfilePath =
                "Assets/FPGDemo/Audio/ForestAudioProfile.asset";
            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(ProfilePath);

            Assert.That(profile, Is.Not.Null, ProfilePath);
            Assert.That(profile.AmbienceClipCount, Is.EqualTo(3));
            Assert.That(
                profile.TryValidateMapping(out string error),
                Is.True,
                error);
            for (int index = 0; index < profile.AmbienceClipCount; index++)
            {
                AudioClip clip = profile.GetAmbienceClip(index);
                Assert.That(clip, Is.Not.Null);
                Assert.That(
                    clip.name,
                    Is.EqualTo("AMB_Forest_Bed_" + (index + 1).ToString("00")));
                Assert.That(clip.channels, Is.EqualTo(2));
                Assert.That(clip.frequency, Is.EqualTo(48000));

                string clipPath = AssetDatabase.GetAssetPath(clip);
                AudioImporter importer =
                    AssetImporter.GetAtPath(clipPath) as AudioImporter;
                Assert.That(importer, Is.Not.Null, clipPath);
                Assert.That(importer.forceToMono, Is.False);
                Assert.That(importer.loadInBackground, Is.True);
                AudioImporterSampleSettings settings =
                    importer.defaultSampleSettings;
                Assert.That(
                    settings.loadType,
                    Is.EqualTo(AudioClipLoadType.Streaming));
                Assert.That(
                    settings.compressionFormat,
                    Is.EqualTo(AudioCompressionFormat.Vorbis));
                Assert.That(
                    settings.sampleRateSetting,
                    Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
                Assert.That(settings.preloadAudioData, Is.False);
                Assert.That(settings.quality, Is.EqualTo(0.7f).Within(0.001f));
            }
        }

        [Test]
        public void ForestProfileUsesApprovedSpatialAmbiencePoints()
        {
            const string ProfilePath =
                "Assets/FPGDemo/Audio/ForestAudioProfile.asset";
            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(ProfilePath);

            Assert.That(profile, Is.Not.Null, ProfilePath);
            Assert.That(profile.AmbiencePointClipCount, Is.EqualTo(8));
            Assert.That(profile.AmbiencePointVoiceLimit, Is.EqualTo(4));
            Assert.That(
                profile.AmbiencePointMinIntervalSeconds,
                Is.EqualTo(8f));
            Assert.That(
                profile.AmbiencePointMaxIntervalSeconds,
                Is.EqualTo(18f));
            Assert.That(profile.AmbiencePointMinDistance, Is.EqualTo(2f));
            Assert.That(profile.AmbiencePointMaxDistance, Is.EqualTo(24f));
            Assert.That(
                profile.TryValidateMapping(out string error),
                Is.True,
                error);

            for (int index = 0;
                 index < profile.AmbiencePointClipCount;
                 index++)
            {
                AudioClip clip = profile.GetAmbiencePointClip(index);
                string clipPath =
                    "Assets/FPGDemo/Audio/Forest/Ambience/"
                    + "AMB_Forest_Point_"
                    + (index + 1).ToString("00")
                    + ".wav";
                Assert.That(clip, Is.Not.Null, clipPath);
                Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(clipPath));
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(48000));

                AudioImporter importer =
                    AssetImporter.GetAtPath(clipPath) as AudioImporter;
                Assert.That(importer, Is.Not.Null, clipPath);
                Assert.That(importer.forceToMono, Is.False);
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
            }
        }

        [Test]
        public void ForestProfileUsesApprovedStreamingExplorationMusic()
        {
            const string ProfilePath =
                "Assets/FPGDemo/Audio/ForestAudioProfile.asset";
            const string ClipPath =
                "Assets/FPGDemo/Audio/Forest/Music/MUS_Forest_Exploration_01.wav";
            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(ProfilePath);

            Assert.That(profile, Is.Not.Null, ProfilePath);
            Assert.That(
                profile.GetMusicClipCount(FpgMusicState.Exploration),
                Is.EqualTo(1));
            AudioClip clip =
                profile.GetMusicClip(FpgMusicState.Exploration, 0);
            Assert.That(clip, Is.Not.Null, ClipPath);
            Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(ClipPath));
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.frequency, Is.EqualTo(48000));

            AudioImporter importer =
                AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            Assert.That(importer, Is.Not.Null, ClipPath);
            Assert.That(importer.forceToMono, Is.False);
            Assert.That(importer.loadInBackground, Is.True);
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            Assert.That(
                settings.loadType,
                Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(
                settings.compressionFormat,
                Is.EqualTo(AudioCompressionFormat.Vorbis));
            Assert.That(
                settings.sampleRateSetting,
                Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            Assert.That(settings.preloadAudioData, Is.False);
            Assert.That(settings.quality, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(
                profile.TryValidateMapping(out string error),
                Is.True,
                error);
        }

        [Test]
        public void PresenterEnforcesCooldownAndPreemptsLowerPriorityVoice()
        {
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            AudioClip clip = AudioClip.Create("CombatAudioPresenter", 44100, 1, 44100, false);
            GameObject root = new GameObject("CombatAudioPresenterTest");
            try
            {
                ConfigureBank(bank, clip, 1);
                CombatAudioPresenter presenter =
                    root.AddComponent<CombatAudioPresenter>();
                presenter.SetConfiguration(bank, null, null);
                Assert.That(
                    presenter.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);

                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.EnemyFastThreatTelegraph,
                        0d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.EnemyFastThreatTelegraph,
                        0.01d),
                    Is.False,
                    "The per-cue cooldown must reject a repeated telegraph.");

                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.EnemyFastThreatRelease,
                        0.02d),
                    Is.True,
                    "The release cue has higher priority and may preempt the telegraph.");
                Assert.That(presenter.PreemptedCount, Is.EqualTo(1));
                Assert.That(presenter.PlayedCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(bank);
            }
        }

        [Test]
        public void ForestRoomLifecycleCuesUseApprovedClipsAndCooldowns()
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset");
            GameObject root = new GameObject("ForestLifecycleAudioTest");
            try
            {
                Assert.That(bank, Is.Not.Null);
                CombatAudioPresenter presenter =
                    root.AddComponent<CombatAudioPresenter>();
                presenter.SetConfiguration(bank, null, null);
                Assert.That(
                    presenter.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);

                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.RoomEntered,
                        0d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.RoomEntered,
                        0.1d),
                    Is.False,
                    "Room entry must respect its per-cue cooldown.");
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.ExitUnlocked,
                        1d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.ExitConfirmed,
                        2d),
                    Is.True);
                Assert.That(presenter.PlayedCount, Is.EqualTo(3));
                Assert.That(presenter.RejectedCount, Is.EqualTo(1));
                Assert.That(presenter.MissingClipCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ForestPlayerDamagedUsesApprovedFemaleReaction()
        {
            const string ClipPath =
                "Assets/FPGDemo/Audio/Forest/SFX/VO_Fei_Damaged_01.wav";
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset");
            GameObject root = new GameObject("ForestPlayerDamagedAudioTest");
            try
            {
                Assert.That(bank, Is.Not.Null);
                Assert.That(
                    bank.TryGetCueEntry(
                        CombatAudioCue.PlayerDamaged,
                        out CombatAudioCueEntry entry),
                    Is.True);
                Assert.That(entry.ClipCount, Is.EqualTo(1));
                Assert.That(entry.Clip, Is.Not.Null, ClipPath);
                Assert.That(
                    AssetDatabase.GetAssetPath(entry.Clip),
                    Is.EqualTo(ClipPath));
                Assert.That(entry.Clip.channels, Is.EqualTo(1));
                Assert.That(entry.Clip.frequency, Is.EqualTo(48000));
                Assert.That(entry.Priority, Is.EqualTo(20));
                Assert.That(entry.CooldownSeconds, Is.EqualTo(0.15f));
                Assert.That(entry.Bus, Is.EqualTo(CombatAudioBus.Sfx));
                Assert.That(
                    entry.Space,
                    Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));

                AudioImporter importer =
                    AssetImporter.GetAtPath(ClipPath) as AudioImporter;
                Assert.That(importer, Is.Not.Null, ClipPath);
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

                CombatAudioPresenter presenter =
                    root.AddComponent<CombatAudioPresenter>();
                presenter.SetConfiguration(bank, null, null);
                Assert.That(
                    presenter.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.PlayerDamaged,
                        Vector3.zero,
                        0d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.PlayerDamaged,
                        Vector3.zero,
                        0.1d),
                    Is.False,
                    "Player damage must respect its cooldown.");
                Assert.That(presenter.MissingClipCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ForestInteractionFocusUsesApprovedClipAndCooldown()
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset");
            GameObject root = new GameObject("ForestInteractionFocusAudioTest");
            try
            {
                Assert.That(bank, Is.Not.Null);
                CombatAudioPresenter presenter =
                    root.AddComponent<CombatAudioPresenter>();
                presenter.SetConfiguration(bank, null, null);
                Assert.That(
                    presenter.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);

                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionFocus,
                        0d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionFocus,
                        0.04d),
                    Is.False,
                    "Interaction focus must respect its short cooldown.");
                Assert.That(presenter.PlayedCount, Is.EqualTo(1));
                Assert.That(presenter.RejectedCount, Is.EqualTo(1));
                Assert.That(presenter.MissingClipCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ForestInteractionConfirmUsesApprovedVariationsAndCooldown()
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset");
            GameObject root = new GameObject("ForestInteractionConfirmAudioTest");
            try
            {
                Assert.That(bank, Is.Not.Null);
                Assert.That(
                    bank.TryGetCueEntry(
                        CombatAudioCue.InteractionConfirm,
                        out CombatAudioCueEntry entry),
                    Is.True);
                Assert.That(entry.ClipCount, Is.EqualTo(3));

                CombatAudioPresenter presenter =
                    root.AddComponent<CombatAudioPresenter>();
                presenter.SetConfiguration(bank, null, null);
                Assert.That(
                    presenter.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);

                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionConfirm,
                        0d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionConfirm,
                        0.06d),
                    Is.False,
                    "Interaction confirm must respect its cooldown.");
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionConfirm,
                        0.13d),
                    Is.True);
                Assert.That(presenter.PlayedCount, Is.EqualTo(2));
                Assert.That(presenter.RejectedCount, Is.EqualTo(1));
                Assert.That(presenter.MissingClipCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ForestInteractionRejectUsesApprovedClipAndCooldown()
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset");
            GameObject root = new GameObject("ForestInteractionRejectAudioTest");
            try
            {
                Assert.That(bank, Is.Not.Null);
                Assert.That(
                    bank.TryGetCueEntry(
                        CombatAudioCue.InteractionReject,
                        out CombatAudioCueEntry entry),
                    Is.True);
                Assert.That(entry.ClipCount, Is.EqualTo(1));

                CombatAudioPresenter presenter =
                    root.AddComponent<CombatAudioPresenter>();
                presenter.SetConfiguration(bank, null, null);
                Assert.That(
                    presenter.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);

                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionReject,
                        0d),
                    Is.True);
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionReject,
                        0.06d),
                    Is.False,
                    "Interaction reject must respect its cooldown.");
                Assert.That(
                    presenter.TryPresentAt(
                        CombatAudioCue.InteractionReject,
                        0.20d),
                    Is.True);
                Assert.That(presenter.PlayedCount, Is.EqualTo(2));
                Assert.That(presenter.RejectedCount, Is.EqualTo(1));
                Assert.That(presenter.MissingClipCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MusicDirectorCrossfadesAndReturnsFromVictoryToExploration()
        {
            FpgRoomAudioProfile profile =
                ScriptableObject.CreateInstance<FpgRoomAudioProfile>();
            AudioClip exploration =
                AudioClip.Create("Exploration", 8000, 2, 8000, false);
            AudioClip combat =
                AudioClip.Create("Combat", 8000, 2, 8000, false);
            AudioClip victory =
                AudioClip.Create("Victory", 800, 2, 8000, false);
            AudioClip ambience =
                AudioClip.Create("Ambience", 8000, 2, 8000, false);
            GameObject root = new GameObject("MusicDirectorTest");
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty("explorationMusic").objectReferenceValue =
                    exploration;
                serialized.FindProperty("combatMusic").objectReferenceValue =
                    combat;
                serialized.FindProperty("victoryStinger").objectReferenceValue =
                    victory;
                serialized.FindProperty("ambienceLoop").objectReferenceValue =
                    ambience;
                serialized.FindProperty("musicFadeSeconds").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MusicDirector director = root.AddComponent<MusicDirector>();
                director.SetConfiguration(profile, null, null);
                Assert.That(
                    director.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);
                Assert.That(
                    root.GetComponentsInChildren<AudioSource>(true),
                    Has.Length.EqualTo(3));

                Assert.That(
                    director.TrySetState(
                        FpgMusicState.Exploration,
                        immediate: true),
                    Is.True);
                Assert.That(director.State, Is.EqualTo(FpgMusicState.Exploration));
                Assert.That(
                    director.TrySetState(FpgMusicState.Combat),
                    Is.True);
                Assert.That(director.TransitionRemaining, Is.EqualTo(0.5f));

                director.Advance(0.5f);
                Assert.That(director.TransitionRemaining, Is.Zero);
                Assert.That(director.State, Is.EqualTo(FpgMusicState.Combat));

                Assert.That(
                    director.TrySetState(FpgMusicState.Victory),
                    Is.True);
                director.Advance(victory.length);
                Assert.That(
                    director.State,
                    Is.EqualTo(FpgMusicState.Exploration));

                director.ClearRuntime();
                Assert.That(director.State, Is.EqualTo(FpgMusicState.None));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(ambience);
                Object.DestroyImmediate(victory);
                Object.DestroyImmediate(combat);
                Object.DestroyImmediate(exploration);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MusicDirectorSelectsOneAmbiencePerRestartWithoutImmediateRepeat()
        {
            FpgRoomAudioProfile profile =
                ScriptableObject.CreateInstance<FpgRoomAudioProfile>();
            AudioClip[] ambience =
            {
                AudioClip.Create("Ambience_01", 8000, 2, 8000, false),
                AudioClip.Create("Ambience_02", 8000, 2, 8000, false),
                AudioClip.Create("Ambience_03", 8000, 2, 8000, false)
            };
            GameObject root = new GameObject("AmbienceVariationTest");
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty("ambienceLoop").objectReferenceValue =
                    ambience[0];
                SerializedProperty variations =
                    serialized.FindProperty("ambienceVariations");
                variations.arraySize = 2;
                variations.GetArrayElementAtIndex(0).objectReferenceValue =
                    ambience[1];
                variations.GetArrayElementAtIndex(1).objectReferenceValue =
                    ambience[2];
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MusicDirector director = root.AddComponent<MusicDirector>();
                director.SetConfiguration(profile, null, null);
                Assert.That(
                    director.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);
                AudioClip first = director.ActiveAmbienceClip;
                Assert.That(first, Is.Not.Null);

                director.RestartAmbience();
                AudioClip second = director.ActiveAmbienceClip;
                Assert.That(second, Is.Not.Null);
                Assert.That(second, Is.Not.SameAs(first));

                AudioSource ambienceSource =
                    System.Array.Find(
                        root.GetComponentsInChildren<AudioSource>(true),
                        source => source.gameObject.name == "Ambience");
                Assert.That(ambienceSource, Is.Not.Null);
                Assert.That(ambienceSource.loop, Is.True);
                Assert.That(ambienceSource.spatialBlend, Is.Zero);
                Assert.That(ambienceSource.dopplerLevel, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                for (int index = 0; index < ambience.Length; index++)
                {
                    Object.DestroyImmediate(ambience[index]);
                }

                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MusicDirectorSchedulesBoundedSpatialAmbiencePoints()
        {
            FpgRoomAudioProfile profile =
                ScriptableObject.CreateInstance<FpgRoomAudioProfile>();
            AudioClip[] points =
            {
                AudioClip.Create("Point_01", 12000, 1, 48000, false),
                AudioClip.Create("Point_02", 12000, 1, 48000, false)
            };
            GameObject root = new GameObject("AmbiencePointTest");
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                SerializedProperty pointClips =
                    serialized.FindProperty("ambiencePointClips");
                pointClips.arraySize = points.Length;
                for (int index = 0; index < points.Length; index++)
                {
                    pointClips.GetArrayElementAtIndex(index)
                        .objectReferenceValue = points[index];
                }

                serialized.FindProperty("ambiencePointMinIntervalSeconds")
                    .floatValue = 1f;
                serialized.FindProperty("ambiencePointMaxIntervalSeconds")
                    .floatValue = 1f;
                serialized.FindProperty("ambiencePointHorizontalExtent")
                    .floatValue = 3f;
                serialized.FindProperty("ambiencePointVerticalExtent")
                    .floatValue = 2f;
                serialized.FindProperty("ambiencePointMinDistance")
                    .floatValue = 1f;
                serialized.FindProperty("ambiencePointMaxDistance")
                    .floatValue = 12f;
                serialized.FindProperty("ambiencePointVoiceLimit")
                    .intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MusicDirector director = root.AddComponent<MusicDirector>();
                director.SetConfiguration(profile, null, null);
                Assert.That(
                    director.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);
                AudioSource[] pointSources = System.Array.FindAll(
                    root.GetComponentsInChildren<AudioSource>(true),
                    source => source.gameObject.name.StartsWith(
                        "AmbiencePoint_"));
                Assert.That(pointSources, Has.Length.EqualTo(2));
                for (int index = 0; index < pointSources.Length; index++)
                {
                    Assert.That(pointSources[index].loop, Is.False);
                    Assert.That(pointSources[index].spatialBlend, Is.EqualTo(1f));
                    Assert.That(pointSources[index].dopplerLevel, Is.Zero);
                    Assert.That(
                        pointSources[index].rolloffMode,
                        Is.EqualTo(AudioRolloffMode.Linear));
                    Assert.That(pointSources[index].minDistance, Is.EqualTo(1f));
                    Assert.That(pointSources[index].maxDistance, Is.EqualTo(12f));
                }

                Assert.That(director.AmbiencePointDelayRemaining, Is.EqualTo(1f));
                director.Advance(1f);
                AudioClip first = director.LastAmbiencePointClip;
                Assert.That(first, Is.Not.Null);
                Assert.That(director.AmbiencePointPlayedCount, Is.EqualTo(1));
                Assert.That(
                    Mathf.Abs(director.LastAmbiencePointLocalPosition.x),
                    Is.LessThanOrEqualTo(3f));
                Assert.That(
                    Mathf.Abs(director.LastAmbiencePointLocalPosition.y),
                    Is.LessThanOrEqualTo(2f));

                director.Advance(0.25f);
                director.Advance(0.75f);
                AudioClip second = director.LastAmbiencePointClip;
                Assert.That(second, Is.Not.Null);
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(director.AmbiencePointPlayedCount, Is.EqualTo(2));
                Assert.That(director.AmbiencePointRejectedCount, Is.Zero);

                director.SetPaused(true);
                director.Advance(5f);
                Assert.That(director.AmbiencePointPlayedCount, Is.EqualTo(2));
                director.SetPaused(false);
                director.Advance(1f);
                Assert.That(director.AmbiencePointPlayedCount, Is.EqualTo(3));
                Assert.That(
                    director.LastAmbiencePointClip,
                    Is.Not.SameAs(second));

                director.ClearRuntime();
                Assert.That(director.AmbiencePointPlayedCount, Is.Zero);
                Assert.That(director.LastAmbiencePointClip, Is.Null);
                Assert.That(
                    System.Array.TrueForAll(
                        pointSources,
                        source => source.clip == null),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                for (int index = 0; index < points.Length; index++)
                {
                    Object.DestroyImmediate(points[index]);
                }

                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MusicDirectorSelectsStateVariationsWithoutImmediateRepeat()
        {
            FpgRoomAudioProfile profile =
                ScriptableObject.CreateInstance<FpgRoomAudioProfile>();
            AudioClip[] exploration =
            {
                AudioClip.Create("Exploration_01", 8000, 2, 8000, false),
                AudioClip.Create("Exploration_02", 8000, 2, 8000, false),
                AudioClip.Create("Exploration_03", 8000, 2, 8000, false)
            };
            AudioClip ambience =
                AudioClip.Create("Ambience", 8000, 2, 8000, false);
            GameObject root = new GameObject("MusicVariationTest");
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty("explorationMusic").objectReferenceValue =
                    exploration[0];
                SerializedProperty variations =
                    serialized.FindProperty("explorationMusicVariations");
                variations.arraySize = 2;
                variations.GetArrayElementAtIndex(0).objectReferenceValue =
                    exploration[1];
                variations.GetArrayElementAtIndex(1).objectReferenceValue =
                    exploration[2];
                serialized.FindProperty("ambienceLoop").objectReferenceValue =
                    ambience;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    profile.GetMusicClipCount(FpgMusicState.Exploration),
                    Is.EqualTo(3));
                Assert.That(
                    profile.TryValidateMapping(out string mappingError),
                    Is.True,
                    mappingError);

                MusicDirector director = root.AddComponent<MusicDirector>();
                director.SetConfiguration(profile, null, null);
                Assert.That(
                    director.TryPrepare(out string prepareError),
                    Is.True,
                    prepareError);
                Assert.That(
                    director.TrySetState(
                        FpgMusicState.Exploration,
                        immediate: true),
                    Is.True);
                AudioClip first = director.ActiveMusicClip;
                Assert.That(first, Is.Not.Null);

                Assert.That(
                    director.TrySetState(
                        FpgMusicState.Exploration,
                        immediate: true),
                    Is.True);
                AudioClip second = director.ActiveMusicClip;
                Assert.That(second, Is.Not.Null);
                Assert.That(second, Is.Not.SameAs(first));

                Assert.That(
                    director.TrySetState(
                        FpgMusicState.Exploration,
                        immediate: true),
                    Is.True);
                Assert.That(director.ActiveMusicClip, Is.Not.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(ambience);
                for (int index = 0; index < exploration.Length; index++)
                {
                    Object.DestroyImmediate(exploration[index]);
                }

                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RoomAudioProfileRejectsDuplicateMusicVariations()
        {
            FpgRoomAudioProfile profile =
                ScriptableObject.CreateInstance<FpgRoomAudioProfile>();
            AudioClip duplicate =
                AudioClip.Create("DuplicateMusic", 8000, 2, 8000, false);
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.FindProperty("explorationMusic").objectReferenceValue =
                    duplicate;
                SerializedProperty variations =
                    serialized.FindProperty("explorationMusicVariations");
                variations.arraySize = 1;
                variations.GetArrayElementAtIndex(0).objectReferenceValue =
                    duplicate;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    profile.TryValidateMapping(out string error),
                    Is.False);
                Assert.That(error, Does.Contain("Exploration"));
                Assert.That(error, Does.Contain("duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void RoomAudioProfileRejectsDuplicateAmbiencePointClips()
        {
            FpgRoomAudioProfile profile =
                ScriptableObject.CreateInstance<FpgRoomAudioProfile>();
            AudioClip duplicate =
                AudioClip.Create("DuplicatePoint", 8000, 1, 8000, false);
            try
            {
                SerializedObject serialized = new SerializedObject(profile);
                SerializedProperty points =
                    serialized.FindProperty("ambiencePointClips");
                points.arraySize = 2;
                points.GetArrayElementAtIndex(0).objectReferenceValue =
                    duplicate;
                points.GetArrayElementAtIndex(1).objectReferenceValue =
                    duplicate;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    profile.TryValidateMapping(out string error),
                    Is.False);
                Assert.That(error, Does.Contain("ambience point"));
                Assert.That(error, Does.Contain("duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
                Object.DestroyImmediate(profile);
            }
        }

        private static void ConfigureBank(
            CombatAudioBank bank,
            AudioClip clip,
            int capacity)
        {
            SerializedObject serialized = new SerializedObject(bank);
            serialized.FindProperty("concurrentVoiceLimit").intValue = capacity;
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            entries.arraySize = CombatAudioBank.RequiredCueCount;
            for (int index = 0; index < entries.arraySize; index++)
            {
                CombatAudioCuePolicy policy =
                    CombatAudioBank.GetRequiredCuePolicy(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("cue").intValue = (int)policy.Cue;
                entry.FindPropertyRelative("clip").objectReferenceValue = clip;
                entry.FindPropertyRelative("priority").intValue = policy.Priority;
                entry.FindPropertyRelative("volume").floatValue = policy.Volume;
                entry.FindPropertyRelative("cooldownSeconds").floatValue =
                    policy.CooldownSeconds;
                entry.FindPropertyRelative("maxConcurrentVoices").intValue =
                    1;
                entry.FindPropertyRelative("bus").intValue = (int)policy.Bus;
                entry.FindPropertyRelative("space").intValue = (int)policy.Space;
                entry.FindPropertyRelative("minDistance").floatValue =
                    policy.MinDistance;
                entry.FindPropertyRelative("maxDistance").floatValue =
                    policy.MaxDistance;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertApprovedVolleyCue(
            CombatAudioBank bank,
            CombatAudioCue cue,
            string clipPrefix,
            int expectedClipCount)
        {
            Assert.That(
                bank.TryGetCueEntry(cue, out CombatAudioCueEntry entry),
                Is.True,
                cue.ToString());
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.ClipCount, Is.EqualTo(expectedClipCount));
            Assert.That(entry.TryValidate(out string error), Is.True, error);

            CombatAudioCuePolicy policy = default(CombatAudioCuePolicy);
            bool policyFound = false;
            for (int index = 0;
                 index < CombatAudioBank.RequiredCueCount;
                 index++)
            {
                CombatAudioCuePolicy candidate =
                    CombatAudioBank.GetRequiredCuePolicy(index);
                if (candidate.Cue != cue)
                {
                    continue;
                }

                policy = candidate;
                policyFound = true;
                break;
            }

            Assert.That(policyFound, Is.True, cue.ToString());
            Assert.That(entry.Priority, Is.EqualTo(policy.Priority));
            Assert.That(entry.Volume, Is.EqualTo(policy.Volume));
            Assert.That(
                entry.CooldownSeconds,
                Is.EqualTo(policy.CooldownSeconds));
            Assert.That(
                entry.MaxConcurrentVoices,
                Is.EqualTo(policy.MaxConcurrentVoices));
            Assert.That(entry.Bus, Is.EqualTo(CombatAudioBus.Sfx));
            Assert.That(
                entry.Space,
                Is.EqualTo(FpgAudioPresentationSpace.WorldPositioned));
            Assert.That(entry.MinDistance, Is.EqualTo(1f));
            Assert.That(entry.MaxDistance, Is.EqualTo(20f));

            for (int index = 0; index < entry.ClipCount; index++)
            {
                AudioClip clip = entry.GetClip(index);
                Assert.That(clip, Is.Not.Null);
                Assert.That(
                    clip.name,
                    Is.EqualTo(clipPrefix + (index + 1).ToString("00")));
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.frequency, Is.EqualTo(48000));

                string clipPath = AssetDatabase.GetAssetPath(clip);
                AudioImporter importer =
                    AssetImporter.GetAtPath(clipPath) as AudioImporter;
                Assert.That(importer, Is.Not.Null, clipPath);
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
            }
        }
    }
}
