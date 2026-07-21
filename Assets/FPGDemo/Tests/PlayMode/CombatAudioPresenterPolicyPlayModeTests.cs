using System.Collections;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    /// <summary>
    /// Exercises runtime audio policy decisions against a real fixed
    /// AudioSource pool.  It does not use the authored clips, so generated SFX
    /// length cannot make concurrency assertions flaky.
    /// </summary>
    public sealed class CombatAudioPresenterPolicyPlayModeTests
    {
        private static readonly FieldInfo CueEntriesField = typeof(CombatAudioBank)
            .GetField("cueEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ConcurrentVoiceLimitField = typeof(CombatAudioBank)
            .GetField("concurrentVoiceLimit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CueField = typeof(CombatAudioCueEntry)
            .GetField("cue", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ClipField = typeof(CombatAudioCueEntry)
            .GetField("clip", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PriorityField = typeof(CombatAudioCueEntry)
            .GetField("priority", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CooldownField = typeof(CombatAudioCueEntry)
            .GetField("cooldownSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MaxConcurrentVoicesField = typeof(CombatAudioCueEntry)
            .GetField("maxConcurrentVoices", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SessionHostField = typeof(CombatAudioPresenter)
            .GetField("sessionHost", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AudioBankField = typeof(CombatAudioPresenter)
            .GetField("audioBank", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PresentationProfileField = typeof(CombatAudioPresenter)
            .GetField("presentationProfile", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AudioSourceRootField = typeof(CombatAudioPresenter)
            .GetField("audioSourceRoot", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SourcePoolCapacityField = typeof(CombatAudioPresenter)
            .GetField("sourcePoolCapacity", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTest]
        public IEnumerator FixedAudioPoolHonorsCooldownConcurrencyAndPriorityWithoutGrowing()
        {
            AssertRequiredReflectionFields();

            GameObject hostObject = new GameObject("D0AudioPolicyHost");
            GameObject audioRootObject = new GameObject("D0AudioPolicyRoot");
            GameObject presenterObject = new GameObject("D0AudioPolicyPresenter");
            GameObject listenerObject = new GameObject("D0AudioPolicyListener");
            listenerObject.AddComponent<AudioListener>();
            AudioClip clip = AudioClip.Create("D0AudioPolicyClip", 88200, 1, 44100, false);
            CombatAudioBank bank = ScriptableObject.CreateInstance<CombatAudioBank>();
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();

            try
            {
                BuildLongClipBank(bank, clip);
                BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
                CombatAudioPresenter presenter = presenterObject.AddComponent<CombatAudioPresenter>();
                Bind(presenter, host, bank, profile, audioRootObject.transform);

                Assert.That(presenter.TryPrepare(out string error), Is.True, error);
                AudioSource[] prewarmedSources =
                    audioRootObject.GetComponentsInChildren<AudioSource>(true);
                Assert.That(prewarmedSources,
                    Has.Length.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));

                // Fill every voice with a higher-priority cue.  The low-priority
                // primary cue must be rejected instead of allocating another
                // source or evicting a more important cue.
                int highPriorityCueCount = 0;
                for (int index = 0; index < CombatAudioBank.RequiredCueCount
                     && highPriorityCueCount < CombatAudioBank.DefaultConcurrentVoiceLimit;
                     index++)
                {
                    CombatAudioCue cue = CombatAudioBank.GetRequiredCue(index);
                    if (cue == CombatAudioCue.PlayerPrimaryShot)
                    {
                        continue;
                    }

                    Assert.That(presenter.TryPlayPresentationCue(cue), Is.True, cue.ToString());
                    highPriorityCueCount++;
                    // Let AudioSource.isPlaying become observable before the
                    // next acquisition. The synthetic clip is two seconds
                    // long, so all sixteen voices remain active.
                    yield return null;
                }

                Assert.That(highPriorityCueCount,
                    Is.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));
                Assert.That(presenter.ActiveVoiceCount,
                    Is.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));

                int priorityRejectsBefore = presenter.PriorityRejectedCount;
                Assert.That(presenter.TryPlayPresentationCue(CombatAudioCue.PlayerPrimaryShot), Is.False);
                Assert.That(presenter.PriorityRejectedCount, Is.EqualTo(priorityRejectsBefore + 1));
                Assert.That(presenter.CreatedSourceCount,
                    Is.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));
                AssertSourceIdentityIsStable(prewarmedSources, audioRootObject.transform);

                presenter.ClearPresentation();
                ConfigureCue(
                    bank,
                    CombatAudioCue.PlayerPrimaryShot,
                    priority: 10,
                    cooldownSeconds: 0.5f,
                    maxConcurrentVoices: 4);
                int cooldownRejectsBefore = presenter.CooldownRejectedCount;
                Assert.That(presenter.TryPlayPresentationCue(CombatAudioCue.PlayerPrimaryShot), Is.True);
                Assert.That(presenter.TryPlayPresentationCue(CombatAudioCue.PlayerPrimaryShot), Is.False);
                Assert.That(presenter.CooldownRejectedCount, Is.EqualTo(cooldownRejectsBefore + 1));

                presenter.ClearPresentation();
                ConfigureCue(
                    bank,
                    CombatAudioCue.PlayerPrimaryShot,
                    priority: 10,
                    cooldownSeconds: 0f,
                    maxConcurrentVoices: 4);
                for (int index = 0; index < 4; index++)
                {
                    Assert.That(presenter.TryPlayPresentationCue(CombatAudioCue.PlayerPrimaryShot), Is.True);
                    yield return null;
                }

                int concurrencyRejectsBefore = presenter.ConcurrencyRejectedCount;
                Assert.That(presenter.TryPlayPresentationCue(CombatAudioCue.PlayerPrimaryShot), Is.False);
                Assert.That(presenter.ConcurrencyRejectedCount,
                    Is.EqualTo(concurrencyRejectsBefore + 1));
                Assert.That(presenter.ActiveVoiceCount, Is.EqualTo(4));
                Assert.That(presenter.CreatedSourceCount,
                    Is.EqualTo(CombatAudioBank.DefaultConcurrentVoiceLimit));
                AssertSourceIdentityIsStable(prewarmedSources, audioRootObject.transform);
            }
            finally
            {
                Object.Destroy(presenterObject);
                Object.Destroy(audioRootObject);
                Object.Destroy(hostObject);
                Object.Destroy(listenerObject);
                Object.Destroy(bank);
                Object.Destroy(profile);
                Object.Destroy(clip);
            }
        }

        private static void BuildLongClipBank(CombatAudioBank bank, AudioClip clip)
        {
            ConcurrentVoiceLimitField.SetValue(bank, CombatAudioBank.DefaultConcurrentVoiceLimit);
            CombatAudioCueEntry[] entries = new CombatAudioCueEntry[CombatAudioBank.RequiredCueCount];
            for (int index = 0; index < entries.Length; index++)
            {
                CombatAudioCue cue = CombatAudioBank.GetRequiredCue(index);
                CombatAudioCueEntry entry = new CombatAudioCueEntry();
                CueField.SetValue(entry, cue);
                ClipField.SetValue(entry, clip);
                PriorityField.SetValue(
                    entry,
                    cue == CombatAudioCue.PlayerPrimaryShot ? 200 : 10);
                CooldownField.SetValue(entry, 0f);
                MaxConcurrentVoicesField.SetValue(entry, CombatAudioBank.DefaultConcurrentVoiceLimit);
                entries[index] = entry;
            }

            CueEntriesField.SetValue(bank, entries);
            Assert.That(bank.TryValidatePlayback(out string error), Is.True, error);
        }

        private static void ConfigureCue(
            CombatAudioBank bank,
            CombatAudioCue cue,
            int priority,
            float cooldownSeconds,
            int maxConcurrentVoices)
        {
            for (int index = 0; index < bank.CueEntryCount; index++)
            {
                CombatAudioCueEntry entry = bank.GetCueEntry(index);
                if (entry == null || entry.Cue != cue)
                {
                    continue;
                }

                PriorityField.SetValue(entry, priority);
                CooldownField.SetValue(entry, cooldownSeconds);
                MaxConcurrentVoicesField.SetValue(entry, maxConcurrentVoices);
                Assert.That(bank.TryValidatePlayback(out string error), Is.True, error);
                return;
            }

            Assert.Fail("The requested test cue is missing from the generated bank.");
        }

        private static void Bind(
            CombatAudioPresenter presenter,
            BattleSessionHost host,
            CombatAudioBank bank,
            CombatPresentationProfile profile,
            Transform audioRoot)
        {
            SessionHostField.SetValue(presenter, host);
            AudioBankField.SetValue(presenter, bank);
            PresentationProfileField.SetValue(presenter, profile);
            AudioSourceRootField.SetValue(presenter, audioRoot);
            SourcePoolCapacityField.SetValue(presenter, CombatAudioBank.DefaultConcurrentVoiceLimit);
        }

        private static void AssertSourceIdentityIsStable(
            AudioSource[] expected,
            Transform audioRoot)
        {
            AudioSource[] actual = audioRoot.GetComponentsInChildren<AudioSource>(true);
            Assert.That(actual, Has.Length.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index], Is.SameAs(expected[index]));
            }
        }

        private static void AssertRequiredReflectionFields()
        {
            Assert.That(CueEntriesField, Is.Not.Null);
            Assert.That(ConcurrentVoiceLimitField, Is.Not.Null);
            Assert.That(CueField, Is.Not.Null);
            Assert.That(ClipField, Is.Not.Null);
            Assert.That(PriorityField, Is.Not.Null);
            Assert.That(CooldownField, Is.Not.Null);
            Assert.That(MaxConcurrentVoicesField, Is.Not.Null);
            Assert.That(SessionHostField, Is.Not.Null);
            Assert.That(AudioBankField, Is.Not.Null);
            Assert.That(PresentationProfileField, Is.Not.Null);
            Assert.That(AudioSourceRootField, Is.Not.Null);
            Assert.That(SourcePoolCapacityField, Is.Not.Null);
        }
    }
}
