using System;
using System.Collections.Generic;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// Owns the authored G0 mapping for the D0 audio bank. It writes only
    /// static presentation policy and preserves any clips assigned by G3, so
    /// rerunning the D0 installer never discards approved replacement audio.
    /// </summary>
    public static class FpgDemoD0AudioBankAuthoring
    {
        public const string AudioBankPath =
            "Assets/FPGDemo/Config/D0Slice/CombatAudioBank.asset";

        [MenuItem("FPG Demo/D0 2.5D/Prepare Audio Cue Mapping")]
        public static void PrepareAudioCueMapping()
        {
            CombatAudioBank bank = AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                AudioBankPath);
            if (bank == null)
            {
                throw new InvalidOperationException(
                    "D0 CombatAudioBank is missing. Run the D0 slice installer before preparing audio mapping.");
            }

            EnsureCueMappings(bank);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Idempotently writes all D0 cue policies. G0 deliberately keeps
        /// clips nullable; G3 is responsible for generating and binding WAVs.
        /// </summary>
        public static void EnsureCueMappings(CombatAudioBank bank)
        {
            if (bank == null)
            {
                throw new ArgumentNullException(nameof(bank));
            }

            Dictionary<CombatAudioCue, AudioClip> preservedClips =
                CaptureExistingClips(bank);
            SerializedObject serializedBank = new SerializedObject(bank);
            SerializedProperty voiceLimit = serializedBank.FindProperty("concurrentVoiceLimit");
            SerializedProperty entries = serializedBank.FindProperty("cueEntries");
            if (voiceLimit == null || entries == null)
            {
                throw new InvalidOperationException(
                    "CombatAudioBank no longer exposes its required serialized mapping fields.");
            }

            voiceLimit.intValue = CombatAudioBank.DefaultConcurrentVoiceLimit;
            entries.arraySize = CombatAudioBank.RequiredCueCount;
            for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
            {
                CombatAudioCuePolicy policy = CombatAudioBank.GetRequiredCuePolicy(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                SerializedProperty cue = entry.FindPropertyRelative("cue");
                SerializedProperty clip = entry.FindPropertyRelative("clip");
                SerializedProperty priority = entry.FindPropertyRelative("priority");
                SerializedProperty volume = entry.FindPropertyRelative("volume");
                SerializedProperty cooldown = entry.FindPropertyRelative("cooldownSeconds");
                SerializedProperty maximumVoices = entry.FindPropertyRelative("maxConcurrentVoices");
                if (cue == null || clip == null || priority == null || volume == null
                    || cooldown == null || maximumVoices == null)
                {
                    throw new InvalidOperationException(
                        "CombatAudioCueEntry no longer exposes its required serialized policy fields.");
                }

                cue.intValue = (int)policy.Cue;
                clip.objectReferenceValue = preservedClips.TryGetValue(
                    policy.Cue,
                    out AudioClip preservedClip)
                    ? preservedClip
                    : null;
                priority.intValue = policy.Priority;
                volume.floatValue = policy.Volume;
                cooldown.floatValue = policy.CooldownSeconds;
                maximumVoices.intValue = policy.MaxConcurrentVoices;
            }

            serializedBank.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);
            if (!bank.TryValidateMapping(out string error))
            {
                throw new InvalidOperationException(
                    $"D0 CombatAudioBank mapping is invalid after authoring: {error}");
            }
        }

        private static Dictionary<CombatAudioCue, AudioClip> CaptureExistingClips(
            CombatAudioBank bank)
        {
            Dictionary<CombatAudioCue, AudioClip> clips =
                new Dictionary<CombatAudioCue, AudioClip>();
            for (int index = 0; index < bank.CueEntryCount; index++)
            {
                CombatAudioCueEntry entry = bank.GetCueEntry(index);
                if (entry == null || !IsPlayableCue(entry.Cue) || entry.Clip == null)
                {
                    continue;
                }

                // First occurrence wins so a malformed old mapping cannot
                // make a rerun select an arbitrary duplicate clip.
                if (!clips.ContainsKey(entry.Cue))
                {
                    clips.Add(entry.Cue, entry.Clip);
                }
            }

            return clips;
        }

        private static bool IsPlayableCue(CombatAudioCue cue)
        {
            return cue > CombatAudioCue.None && cue < CombatAudioCue.Count;
        }
    }
}
