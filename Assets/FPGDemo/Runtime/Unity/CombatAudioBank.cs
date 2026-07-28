using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Stable presentation-only audio cue identifiers for the D0 combat slice.
    /// These values are serialized in <see cref="CombatAudioBank"/> assets, so
    /// do not reorder existing values.
    /// </summary>
    public enum CombatAudioCue
    {
        None = 0,
        EnemyFastThreatTelegraph = 8,
        EnemyFastThreatRelease = 9,
        EnemyInterceptableThreatTelegraph = 10,
        EnemyInterceptableThreatRelease = 11,
        EnemyHeavyThreatTelegraph = 12,
        EnemyHeavyThreatRelease = 13,
        PlayerDamaged = 14,
        PlayerBarrierBroken = 15,
        EnemyBreak = 16,
        Victory = 17,
        Defeat = 18,
        ReticleTargetLock = 19,
        EnemyDangerTick = 20,
        Count = 22
    }

    /// <summary>
    /// Stable authored defaults for one D0 audio cue. The bank asset owns the
    /// serialized mapping; this value provides a deterministic policy while the
    /// clips remain independently replaceable.
    /// </summary>
    public readonly struct CombatAudioCuePolicy
    {
        public CombatAudioCuePolicy(
            CombatAudioCue cue,
            int priority,
            float cooldownSeconds,
            int maxConcurrentVoices,
            float volume = 1f)
        {
            Cue = cue;
            Priority = priority;
            CooldownSeconds = cooldownSeconds;
            MaxConcurrentVoices = maxConcurrentVoices;
            Volume = volume;
        }

        public CombatAudioCue Cue { get; }

        public int Priority { get; }

        public float CooldownSeconds { get; }

        public int MaxConcurrentVoices { get; }

        /// <summary>
        /// Linear per-cue gain applied by the fixed AudioSource pool. D0 keeps
        /// this in the safe Unity range of zero through one so replacing a clip
        /// never requires a code change to rebalance it.
        /// </summary>
        public float Volume { get; }
    }

    /// <summary>
    /// Static playback policy for one visual presentation cue. A future
    /// presenter owns the actual AudioSource pool; this type deliberately
    /// contains no runtime playback state or combat dependencies.
    /// </summary>
    [Serializable]
    public sealed class CombatAudioCueEntry
    {
        [SerializeField]
        private CombatAudioCue cue = CombatAudioCue.PlayerDamaged;

        [SerializeField]
        private AudioClip clip;

        // Unity AudioSource priority uses 0 as highest and 256 as lowest.
        [SerializeField, Range(0, 256)]
        private int priority = 128;

        // Unity AudioSource volume is a linear scalar in the inclusive 0..1
        // range. Keep this authored per cue instead of hard-coding it on the
        // pooled voice, so later clip replacement can be balanced in data.
        [SerializeField, Range(0f, 1f)]
        private float volume = 1f;

        [SerializeField, Min(0f)]
        private float cooldownSeconds;

        [SerializeField, Min(1)]
        private int maxConcurrentVoices = 1;

        public CombatAudioCue Cue => cue;
        public AudioClip Clip => clip;
        public int Priority => priority;
        public float Volume => volume;
        public float CooldownSeconds => cooldownSeconds;
        public int MaxConcurrentVoices => maxConcurrentVoices;

        public bool TryValidate(out string error)
        {
            return TryValidate(requireClip: true, out error);
        }

        internal bool TryValidate(bool requireClip, out string error)
        {
            if (!CombatAudioBank.IsPlayableCue(cue))
            {
                error = "Combat audio cue entries require a playable cue identifier.";
                return false;
            }

            if (requireClip && clip == null)
            {
                error = $"Combat audio cue {cue} must reference an AudioClip.";
                return false;
            }

            if (priority < 0 || priority > 256)
            {
                error = $"Combat audio cue {cue} priority must be between 0 and 256.";
                return false;
            }

            if (float.IsNaN(volume)
                || float.IsInfinity(volume)
                || volume < 0f
                || volume > 1f)
            {
                error = $"Combat audio cue {cue} volume must be finite and between 0 and 1.";
                return false;
            }

            if (float.IsNaN(cooldownSeconds)
                || float.IsInfinity(cooldownSeconds)
                || cooldownSeconds < 0f)
            {
                error = $"Combat audio cue {cue} cooldown must be finite and non-negative.";
                return false;
            }

            if (maxConcurrentVoices <= 0)
            {
                error = $"Combat audio cue {cue} must allow at least one concurrent voice.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Inspector-authored, replaceable audio mapping for the D0 combat slice.
    /// It is intentionally a presentation configuration asset: it reads no
    /// battle state, owns no AudioSource, and has no effect on deterministic
    /// combat results.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatAudioBank", menuName = "FPG Demo/Combat Audio Bank")]
    public sealed class CombatAudioBank : ScriptableObject
    {
        public const int DefaultConcurrentVoiceLimit = 16;

        private static readonly CombatAudioCuePolicy[] RequiredCuePolicies =
        {
            new CombatAudioCuePolicy(CombatAudioCue.EnemyFastThreatTelegraph, 60, 0.20f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyFastThreatRelease, 50, 0.10f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyInterceptableThreatTelegraph, 75, 0.20f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyInterceptableThreatRelease, 65, 0.08f, 3),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyHeavyThreatTelegraph, 30, 0.20f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyHeavyThreatRelease, 25, 0.12f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.PlayerDamaged, 20, 0.15f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.PlayerBarrierBroken, 15, 0.25f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyBreak, 20, 0.25f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.Victory, 10, 0.50f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.Defeat, 5, 0.50f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.ReticleTargetLock, 130, 0.08f, 1),
            new CombatAudioCuePolicy(CombatAudioCue.EnemyDangerTick, 65, 0.18f, 1)
        };

        [Header("Fixed presentation capacity")]
        [SerializeField, Min(1)]
        private int concurrentVoiceLimit = DefaultConcurrentVoiceLimit;

        [Header("Cue mapping")]
        [SerializeField]
        private CombatAudioCueEntry[] cueEntries = Array.Empty<CombatAudioCueEntry>();

        /// <summary>
        /// Maximum pooled AudioSources a future <c>CombatAudioPresenter</c>
        /// may use concurrently. It remains data-only in G0.
        /// </summary>
        public int ConcurrentVoiceLimit => concurrentVoiceLimit;
        public int CueEntryCount => cueEntries == null ? 0 : cueEntries.Length;
        public static int RequiredCueCount => RequiredCuePolicies.Length;

        public CombatAudioCueEntry GetCueEntry(int index)
        {
            if (index < 0 || index >= CueEntryCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return cueEntries[index];
        }

        public static CombatAudioCue GetRequiredCue(int index)
        {
            return GetRequiredCuePolicy(index).Cue;
        }

        public static CombatAudioCuePolicy GetRequiredCuePolicy(int index)
        {
            if (index < 0 || index >= RequiredCuePolicies.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return RequiredCuePolicies[index];
        }

        public bool TryGetCueEntry(
            CombatAudioCue cue,
            out CombatAudioCueEntry entry)
        {
            if (!IsPlayableCue(cue))
            {
                entry = null;
                return false;
            }

            CombatAudioCueEntry[] entries = cueEntries
                ?? Array.Empty<CombatAudioCueEntry>();
            for (int index = 0; index < entries.Length; index++)
            {
                CombatAudioCueEntry candidate = entries[index];
                if (candidate != null && candidate.Cue == cue)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Validates the G3 playback-ready bank. Every required cue must map
        /// to an AudioClip before an AudioSource presenter can bind it.
        /// </summary>
        public bool TryValidate(out string error)
        {
            return TryValidatePlayback(out error);
        }

        public bool TryValidatePlayback(out string error)
        {
            return TryValidateInternal(requireClips: true, out error);
        }

        /// <summary>
        /// Validates G0's complete event-to-policy mapping before offline WAV
        /// generation. It intentionally permits missing clips, but keeps all
        /// cue, priority, cooldown and voice-limit constraints fail-closed.
        /// </summary>
        public bool TryValidateMapping(out string error)
        {
            return TryValidateInternal(requireClips: false, out error);
        }

        private bool TryValidateInternal(bool requireClips, out string error)
        {
            if (concurrentVoiceLimit <= 0)
            {
                error = "Combat audio concurrent voice limit must be positive.";
                return false;
            }

            CombatAudioCueEntry[] entries = cueEntries
                ?? Array.Empty<CombatAudioCueEntry>();
            bool[] mappedCues = new bool[(int)CombatAudioCue.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                CombatAudioCueEntry entry = entries[index];
                if (entry == null)
                {
                    error = $"Combat audio cue entry {index} is missing.";
                    return false;
                }

                if (!entry.TryValidate(requireClips, out string entryError))
                {
                    error = $"Combat audio cue entry {index} is invalid: {entryError}";
                    return false;
                }

                int cueIndex = (int)entry.Cue;
                if (mappedCues[cueIndex])
                {
                    error = $"Combat audio cue {entry.Cue} appears more than once.";
                    return false;
                }

                if (entry.MaxConcurrentVoices > concurrentVoiceLimit)
                {
                    error = $"Combat audio cue {entry.Cue} max concurrent voices cannot exceed the bank limit ({concurrentVoiceLimit}).";
                    return false;
                }

                mappedCues[cueIndex] = true;
            }

            for (int index = 0; index < RequiredCuePolicies.Length; index++)
            {
                CombatAudioCue requiredCue = RequiredCuePolicies[index].Cue;
                if (!mappedCues[(int)requiredCue])
                {
                    error = $"Combat audio bank does not map the required cue {requiredCue}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        internal static bool IsPlayableCue(CombatAudioCue cue)
        {
            return cue > CombatAudioCue.None
                && cue < CombatAudioCue.Count
                && Enum.IsDefined(typeof(CombatAudioCue), cue);
        }
    }
}
