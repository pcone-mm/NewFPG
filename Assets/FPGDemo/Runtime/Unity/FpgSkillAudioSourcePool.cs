using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity 2D audio pool for node-owned skill presentation.
    /// Global combat feedback remains routed through CombatAudioBank.
    /// </summary>
    public sealed class FpgSkillAudioSourcePool
    {
        private AudioSource[] sources = Array.Empty<AudioSource>();
        private GameObject generatedRoot;

        public int Capacity => sources.Length;
        public int RejectCount { get; private set; }
        public bool IsPrepared { get; private set; }

        public bool TryPrepare(
            Transform parent,
            int capacity,
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
                    nextSources[index] = source;
                }

                generatedRoot = nextRoot;
                sources = nextSources;
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
            if (!IsPrepared || clip == null || !IsFinite(volume)
                || volume < 0f || volume > 1f)
            {
                RejectCount++;
                return false;
            }

            for (int index = 0; index < sources.Length; index++)
            {
                AudioSource source = sources[index];
                if (source == null || source.isPlaying)
                {
                    continue;
                }

                ConfigureAsTwoDimensional(source);
                source.clip = clip;
                source.volume = volume;
                source.Play();
                return true;
            }

            RejectCount++;
            return false;
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

                source.Stop();
                source.clip = null;
                source.volume = 1f;
                ConfigureAsTwoDimensional(source);
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
