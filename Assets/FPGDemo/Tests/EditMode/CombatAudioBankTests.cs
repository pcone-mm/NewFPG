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
                CombatAudioCue.PlayerPrimaryShot,
                CombatAudioCue.PlayerSecondaryCharge,
                CombatAudioCue.PlayerSecondaryRelease,
                CombatAudioCue.PlayerBodyHit,
                CombatAudioCue.PlayerWeakpointHit,
                CombatAudioCue.ProjectileIntercept,
                CombatAudioCue.PlayerReload,
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
                CombatAudioCue.PlayerConfirmRelease
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
            }
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
                    .intValue = (int)CombatAudioCue.PlayerPrimaryShot;
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
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
