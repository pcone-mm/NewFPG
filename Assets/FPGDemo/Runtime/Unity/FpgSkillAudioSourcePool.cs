using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FPG.Demo.Unity
{
    internal static class FpgAudioVariationSelection
    {
        public static int SelectIndex(
            int clipCount,
            int previousIndex,
            uint randomValue)
        {
            if (clipCount <= 1)
            {
                return 0;
            }

            if (previousIndex < 0 || previousIndex >= clipCount)
            {
                return (int)(randomValue % (uint)clipCount);
            }

            int selected = (int)(randomValue % (uint)(clipCount - 1));
            return selected >= previousIndex ? selected + 1 : selected;
        }

        public static uint Next(ref uint state)
        {
            if (state == 0u)
            {
                state = 0x9E3779B9u;
            }

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }

    /// <summary>
    /// Fixed-capacity audio pool for node-owned skill presentation.
    /// Global combat feedback remains routed through CombatAudioBank.
    /// </summary>
    public sealed class FpgSkillAudioSourcePool
    {
        private AudioSource[] sources = Array.Empty<AudioSource>();
        private bool[] heldSources = Array.Empty<bool>();
        private GameObject generatedRoot;
        private AudioMixerGroup outputGroup;

        public int Capacity => sources.Length;
        public int RejectCount { get; private set; }
        public bool IsPrepared { get; private set; }

        public bool TryPrepare(
            Transform parent,
            int capacity,
            out string error)
        {
            return TryPrepare(parent, capacity, null, out error);
        }

        public bool TryPrepare(
            Transform parent,
            int capacity,
            AudioMixerGroup nextOutputGroup,
            out string error)
        {
            if (IsPrepared)
            {
                error = capacity == sources.Length
                    ? string.Empty
                    : "Skill audio pool capacity changed after preparation.";
                return error.Length == 0;
            }

            if (parent == null || capacity <= 0)
            {
                error =
                    "Skill audio pool requires a parent and positive capacity.";
                return false;
            }

            AudioSource[] nextSources = new AudioSource[capacity];
            GameObject nextRoot = null;
            try
            {
                nextRoot = new GameObject("FpgSkillAudioPool");
                nextRoot.transform.SetParent(parent, false);
                for (int index = 0; index < capacity; index++)
                {
                    GameObject sourceObject =
                        new GameObject("SkillAudio_" + index.ToString("00"));
                    sourceObject.transform.SetParent(nextRoot.transform, false);
                    AudioSource source = sourceObject.AddComponent<AudioSource>();
                    ConfigureAsTwoDimensional(source);
                    source.outputAudioMixerGroup = nextOutputGroup;
                    nextSources[index] = source;
                }

                generatedRoot = nextRoot;
                sources = nextSources;
                heldSources = new bool[capacity];
                outputGroup = nextOutputGroup;
                IsPrepared = true;
                RejectCount = 0;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (nextRoot != null)
                {
                    DestroyObject(nextRoot);
                }

                sources = Array.Empty<AudioSource>();
                error = "Skill audio pool preparation failed: "
                    + exception.Message;
                return false;
            }
        }

        public bool TryPlay(AudioClip clip, float volume)
        {
            return TryPlay(
                clip,
                volume,
                FpgAudioPresentationSpace.TwoDimensional,
                Vector3.zero,
                1f,
                20f);
        }

        public bool TryPlay(
            AudioClip clip,
            float volume,
            FpgAudioPresentationSpace space,
            Vector3 position,
            float minDistance,
            float maxDistance)
        {
            if (!IsPrepared || clip == null || !IsFinite(volume)
                || volume < 0f || volume > 1f
                || !Enum.IsDefined(typeof(FpgAudioPresentationSpace), space)
                || !IsFinite(minDistance)
                || !IsFinite(maxDistance)
                || minDistance <= 0f
                || maxDistance < minDistance)
            {
                RejectCount++;
                return false;
            }

            for (int index = 0; index < sources.Length; index++)
            {
                AudioSource source = sources[index];
                if (source == null || heldSources[index] || source.isPlaying)
                {
                    continue;
                }

                ConfigureSource(
                    source,
                    space,
                    position,
                    minDistance,
                    maxDistance);
                source.clip = clip;
                source.volume = volume;
                source.Play();
                return true;
            }

            RejectCount++;
            return false;
        }

        public bool TryBorrowHeld(
            AudioClip clip,
            float volume,
            FpgAudioPresentationSpace space,
            Vector3 position,
            float minDistance,
            float maxDistance,
            out AudioSource instance)
        {
            instance = null;
            if (!IsValidRequest(
                    clip,
                    volume,
                    space,
                    minDistance,
                    maxDistance))
            {
                RejectCount++;
                return false;
            }

            for (int index = 0; index < sources.Length; index++)
            {
                AudioSource source = sources[index];
                if (source == null || heldSources[index] || source.isPlaying)
                {
                    continue;
                }

                ConfigureSource(
                    source,
                    space,
                    position,
                    minDistance,
                    maxDistance);
                source.clip = clip;
                source.volume = volume;
                source.loop = true;
                heldSources[index] = true;
                source.Play();
                instance = source;
                return true;
            }

            RejectCount++;
            return false;
        }

        public bool TryUpdateHeld(AudioSource instance, Vector3 position)
        {
            int index = FindSourceIndex(instance);
            if (index < 0 || !heldSources[index])
            {
                RejectCount++;
                return false;
            }

            if (instance.spatialBlend > 0f)
            {
                instance.transform.position = position;
            }

            return true;
        }

        public bool TryReleaseHeld(AudioSource instance)
        {
            int index = FindSourceIndex(instance);
            if (index < 0 || !heldSources[index])
            {
                RejectCount++;
                return false;
            }

            heldSources[index] = false;
            ResetSource(instance);
            return true;
        }

        public void Clear()
        {
            for (int index = 0; index < sources.Length; index++)
            {
                AudioSource source = sources[index];
                if (source == null)
                {
                    continue;
                }

                heldSources[index] = false;
                ResetSource(source);
            }
        }

        public void Dispose()
        {
            Clear();
            if (generatedRoot != null)
            {
                DestroyObject(generatedRoot);
            }

            generatedRoot = null;
            sources = Array.Empty<AudioSource>();
            heldSources = Array.Empty<bool>();
            outputGroup = null;
            IsPrepared = false;
            RejectCount = 0;
        }

        private static void ConfigureAsTwoDimensional(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

        private static void ConfigureSource(
            AudioSource source,
            FpgAudioPresentationSpace space,
            Vector3 position,
            float minDistance,
            float maxDistance)
        {
            if (space == FpgAudioPresentationSpace.WorldPositioned)
            {
                source.transform.position = position;
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                return;
            }

            ConfigureAsTwoDimensional(source);
        }

        private int FindSourceIndex(AudioSource instance)
        {
            if (instance == null)
            {
                return -1;
            }

            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] == instance)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool IsValidRequest(
            AudioClip clip,
            float volume,
            FpgAudioPresentationSpace space,
            float minDistance,
            float maxDistance)
        {
            return IsPrepared
                && clip != null
                && IsFinite(volume)
                && volume >= 0f
                && volume <= 1f
                && Enum.IsDefined(typeof(FpgAudioPresentationSpace), space)
                && IsFinite(minDistance)
                && IsFinite(maxDistance)
                && minDistance > 0f
                && maxDistance >= minDistance;
        }

        private void ResetSource(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.volume = 1f;
            ConfigureAsTwoDimensional(source);
            source.outputAudioMixerGroup = outputGroup;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
