using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "ForestAudioProfile",
        menuName = "FPG Demo/Audio/Room Audio Profile")]
    public sealed class FpgRoomAudioProfile : ScriptableObject
    {
        [Header("Forest music")]
        [SerializeField] private AudioClip explorationMusic;
        [SerializeField] private AudioClip[] explorationMusicVariations =
            Array.Empty<AudioClip>();
        [SerializeField] private AudioClip combatMusic;
        [SerializeField] private AudioClip[] combatMusicVariations =
            Array.Empty<AudioClip>();
        [SerializeField] private AudioClip victoryStinger;
        [SerializeField] private AudioClip[] victoryStingerVariations =
            Array.Empty<AudioClip>();
        [SerializeField] private AudioClip defeatStinger;
        [SerializeField] private AudioClip[] defeatStingerVariations =
            Array.Empty<AudioClip>();

        [Header("Forest ambience")]
        [SerializeField] private AudioClip ambienceLoop;
        [SerializeField] private AudioClip[] ambienceVariations =
            Array.Empty<AudioClip>();

        [Header("Forest ambience points")]
        [SerializeField] private AudioClip[] ambiencePointClips =
            Array.Empty<AudioClip>();
        [SerializeField, Min(0f)]
        private float ambiencePointMinIntervalSeconds = 8f;
        [SerializeField, Min(0f)]
        private float ambiencePointMaxIntervalSeconds = 18f;
        [SerializeField, Range(0f, 1f)]
        private float ambiencePointVolume = 0.6f;
        [SerializeField, Min(0f)]
        private float ambiencePointHorizontalExtent = 10f;
        [SerializeField, Min(0f)]
        private float ambiencePointVerticalExtent = 4f;
        [SerializeField, Min(0.01f)]
        private float ambiencePointMinDistance = 2f;
        [SerializeField, Min(0.01f)]
        private float ambiencePointMaxDistance = 24f;
        [SerializeField, Range(1, 4)]
        private int ambiencePointVoiceLimit = 4;

        [Header("Crossfade")]
        [SerializeField, Min(0f)] private float musicFadeSeconds = 1.25f;
        [SerializeField, Min(0f)] private float ambienceFadeSeconds = 0.75f;

        public AudioClip ExplorationMusic => explorationMusic;
        public AudioClip CombatMusic => combatMusic;
        public AudioClip VictoryStinger => victoryStinger;
        public AudioClip DefeatStinger => defeatStinger;
        public AudioClip AmbienceLoop => ambienceLoop;
        public int AmbienceClipCount => (ambienceLoop == null ? 0 : 1)
            + (ambienceVariations == null ? 0 : ambienceVariations.Length);
        public int AmbiencePointClipCount => ambiencePointClips == null
            ? 0
            : ambiencePointClips.Length;
        public float AmbiencePointMinIntervalSeconds =>
            ambiencePointMinIntervalSeconds;
        public float AmbiencePointMaxIntervalSeconds =>
            ambiencePointMaxIntervalSeconds;
        public float AmbiencePointVolume => ambiencePointVolume;
        public float AmbiencePointHorizontalExtent =>
            ambiencePointHorizontalExtent;
        public float AmbiencePointVerticalExtent =>
            ambiencePointVerticalExtent;
        public float AmbiencePointMinDistance => ambiencePointMinDistance;
        public float AmbiencePointMaxDistance => ambiencePointMaxDistance;
        public int AmbiencePointVoiceLimit => ambiencePointVoiceLimit;
        public float MusicFadeSeconds => musicFadeSeconds;
        public float AmbienceFadeSeconds => ambienceFadeSeconds;

        public int GetMusicClipCount(FpgMusicState state)
        {
            if (!TryGetMusicGroup(
                    state,
                    out AudioClip primary,
                    out AudioClip[] variations))
            {
                return 0;
            }

            return (primary == null ? 0 : 1)
                + (variations == null ? 0 : variations.Length);
        }

        public AudioClip GetMusicClip(FpgMusicState state, int index)
        {
            if (!TryGetMusicGroup(
                    state,
                    out AudioClip primary,
                    out AudioClip[] variations))
            {
                return null;
            }

            int clipCount = (primary == null ? 0 : 1)
                + (variations == null ? 0 : variations.Length);
            if (index < 0 || index >= clipCount)
            {
                return null;
            }

            int primaryCount = primary == null ? 0 : 1;
            return index < primaryCount
                ? primary
                : variations[index - primaryCount];
        }

        public AudioClip GetAmbienceClip(int index)
        {
            int primaryCount = ambienceLoop == null ? 0 : 1;
            if (index < 0 || index >= AmbienceClipCount)
            {
                return null;
            }

            return index < primaryCount
                ? ambienceLoop
                : ambienceVariations[index - primaryCount];
        }

        public AudioClip GetAmbiencePointClip(int index)
        {
            return index < 0 || index >= AmbiencePointClipCount
                ? null
                : ambiencePointClips[index];
        }

        public bool TryValidateMapping(out string error)
        {
            if (!IsFiniteNonNegative(musicFadeSeconds)
                || !IsFiniteNonNegative(ambienceFadeSeconds))
            {
                error = "Room audio profile fade times must be finite and non-negative.";
                return false;
            }

            if (HasInvalidOrDuplicateAmbienceVariations())
            {
                error =
                    "Room audio profile contains a missing or duplicate ambience variation.";
                return false;
            }

            if (HasInvalidOrDuplicateAmbiencePointClips())
            {
                error =
                    "Room audio profile contains a missing or duplicate ambience point clip.";
                return false;
            }

            if (!IsFiniteNonNegative(ambiencePointMinIntervalSeconds)
                || !IsFiniteNonNegative(ambiencePointMaxIntervalSeconds)
                || ambiencePointMaxIntervalSeconds
                    < ambiencePointMinIntervalSeconds)
            {
                error =
                    "Room audio profile ambience point intervals must be finite, non-negative and ordered.";
                return false;
            }

            if (!IsFiniteRange01(ambiencePointVolume)
                || !IsFiniteNonNegative(ambiencePointHorizontalExtent)
                || !IsFiniteNonNegative(ambiencePointVerticalExtent)
                || !IsFinitePositive(ambiencePointMinDistance)
                || !IsFinitePositive(ambiencePointMaxDistance)
                || ambiencePointMaxDistance < ambiencePointMinDistance
                || ambiencePointVoiceLimit < 1
                || ambiencePointVoiceLimit > 4)
            {
                error =
                    "Room audio profile ambience point spatial parameters are invalid.";
                return false;
            }

            for (FpgMusicState state = FpgMusicState.Exploration;
                 state <= FpgMusicState.Defeat;
                 state++)
            {
                if (!HasInvalidOrDuplicateMusicVariations(state))
                {
                    continue;
                }

                error = "Room audio profile contains a missing or duplicate "
                    + state
                    + " music variation.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidatePlayback(out string error)
        {
            if (!TryValidateMapping(out error))
            {
                return false;
            }

            if (GetMusicClipCount(FpgMusicState.Exploration) == 0
                || GetMusicClipCount(FpgMusicState.Combat) == 0
                || AmbienceClipCount == 0)
            {
                error = "Room audio profile requires exploration, combat and ambience clips for playback.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool HasInvalidOrDuplicateAmbienceVariations()
        {
            for (int index = 0; index < AmbienceClipCount; index++)
            {
                AudioClip candidate = GetAmbienceClip(index);
                if (candidate == null)
                {
                    return true;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (GetAmbienceClip(previous) == candidate)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasInvalidOrDuplicateAmbiencePointClips()
        {
            for (int index = 0; index < AmbiencePointClipCount; index++)
            {
                AudioClip candidate = GetAmbiencePointClip(index);
                if (candidate == null)
                {
                    return true;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (GetAmbiencePointClip(previous) == candidate)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasInvalidOrDuplicateMusicVariations(
            FpgMusicState state)
        {
            int clipCount = GetMusicClipCount(state);
            for (int index = 0; index < clipCount; index++)
            {
                AudioClip candidate = GetMusicClip(state, index);
                if (candidate == null)
                {
                    return true;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (GetMusicClip(state, previous) == candidate)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetMusicGroup(
            FpgMusicState state,
            out AudioClip primary,
            out AudioClip[] variations)
        {
            switch (state)
            {
                case FpgMusicState.Exploration:
                    primary = explorationMusic;
                    variations = explorationMusicVariations;
                    return true;
                case FpgMusicState.Combat:
                    primary = combatMusic;
                    variations = combatMusicVariations;
                    return true;
                case FpgMusicState.Victory:
                    primary = victoryStinger;
                    variations = victoryStingerVariations;
                    return true;
                case FpgMusicState.Defeat:
                    primary = defeatStinger;
                    variations = defeatStingerVariations;
                    return true;
                default:
                    primary = null;
                    variations = null;
                    return false;
            }
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value > 0f;
        }

        private static bool IsFiniteRange01(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f
                && value <= 1f;
        }
    }
}
