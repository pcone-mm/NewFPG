using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only fixed voice pool for committed combat cues. It owns
    /// no combat state; callers provide an already-routed stable cue and an
    /// optional world position.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAudioPresenter : MonoBehaviour
    {
        [SerializeField]
        private CombatAudioBank bank;

        [SerializeField]
        private AudioMixerGroup sfxGroup;

        [SerializeField]
        private AudioMixerGroup uiGroup;

        [SerializeField, Min(0)]
        private int poolCapacityOverride;

        private VoiceSlot[] voices = Array.Empty<VoiceSlot>();
        private double[] lastPlayedAt = Array.Empty<double>();
        private int[] lastVariationIndices = Array.Empty<int>();
        private uint variationRandomState;
        private GameObject generatedRoot;
        private bool prepared;

        public CombatAudioBank Bank => bank;
        public int Capacity => voices.Length;
        public bool IsPrepared => prepared;
        public int PlayedCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int MissingClipCount { get; private set; }
        public int PreemptedCount { get; private set; }
        public int ActiveVoiceCount
        {
            get
            {
                int count = 0;
                double now = GetNow();
                for (int index = 0; index < voices.Length; index++)
                {
                    if (IsActive(voices[index], now))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void SetConfiguration(
            CombatAudioBank nextBank,
            AudioMixerGroup nextSfxGroup,
            AudioMixerGroup nextUiGroup)
        {
            if (prepared)
            {
                throw new InvalidOperationException(
                    "Combat audio presenter configuration cannot change after preparation.");
            }

            bank = nextBank;
            sfxGroup = nextSfxGroup;
            uiGroup = nextUiGroup;
        }

        public bool TryPrepare(out string error)
        {
            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            if (bank == null)
            {
                error = "Combat audio presenter requires a CombatAudioBank.";
                return false;
            }

            if (!bank.TryValidateMapping(out error))
            {
                return false;
            }

            int capacity = poolCapacityOverride > 0
                ? poolCapacityOverride
                : bank.ConcurrentVoiceLimit;
            if (capacity <= 0)
            {
                error = "Combat audio presenter requires positive pool capacity.";
                return false;
            }

            GameObject nextRoot = null;
            try
            {
                nextRoot = new GameObject("CombatAudioVoices");
                nextRoot.transform.SetParent(transform, false);
                voices = new VoiceSlot[capacity];
                for (int index = 0; index < capacity; index++)
                {
                    GameObject sourceObject = new GameObject(
                        "CombatAudio_" + index.ToString("00"));
                    sourceObject.transform.SetParent(nextRoot.transform, false);
                    AudioSource source = sourceObject.AddComponent<AudioSource>();
                    ConfigureAsTwoDimensional(source);
                    voices[index] = new VoiceSlot(source);
                }

                lastPlayedAt = new double[(int)CombatAudioCue.Count];
                lastVariationIndices = new int[(int)CombatAudioCue.Count];
                ResetCooldowns();
                ResetVariations();
                variationRandomState = unchecked(
                    (uint)(Environment.TickCount ^ GetInstanceID()));
                generatedRoot = nextRoot;
                prepared = true;
                PlayedCount = 0;
                RejectedCount = 0;
                MissingClipCount = 0;
                PreemptedCount = 0;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (nextRoot != null)
                {
                    DestroyObject(nextRoot);
                }

                voices = Array.Empty<VoiceSlot>();
                lastPlayedAt = Array.Empty<double>();
                lastVariationIndices = Array.Empty<int>();
                error = "Combat audio presenter preparation failed: "
                    + exception.Message;
                return false;
            }
        }

        public bool TryPresent(CombatAudioCue cue)
        {
            return TryPresentInternal(cue, Vector3.zero, false, GetNow());
        }

        public bool TryPresentAt(CombatAudioCue cue, Vector3 worldPosition)
        {
            return TryPresentInternal(cue, worldPosition, true, GetNow());
        }

        public bool TryPresentAt(
            CombatAudioCue cue,
            Vector3 worldPosition,
            double nowSeconds)
        {
            return TryPresentInternal(cue, worldPosition, true, nowSeconds);
        }

        public bool TryPresentAt(CombatAudioCue cue, double nowSeconds)
        {
            return TryPresentInternal(cue, Vector3.zero, false, nowSeconds);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!prepared)
            {
                return;
            }

            double now = GetNow();
            for (int index = 0; index < voices.Length; index++)
            {
                if (!IsActive(voices[index], now))
                {
                    ClearSlot(index);
                }
            }
        }

        public void ClearRuntime()
        {
            for (int index = 0; index < voices.Length; index++)
            {
                ClearSlot(index);
            }

            ResetCooldowns();
            ResetVariations();
        }

        public void Dispose()
        {
            ClearRuntime();
            if (generatedRoot != null)
            {
                DestroyObject(generatedRoot);
            }

            generatedRoot = null;
            voices = Array.Empty<VoiceSlot>();
            lastPlayedAt = Array.Empty<double>();
            lastVariationIndices = Array.Empty<int>();
            prepared = false;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        private bool TryPresentInternal(
            CombatAudioCue cue,
            Vector3 worldPosition,
            bool hasWorldPosition,
            double nowSeconds)
        {
            if (!prepared
                || bank == null
                || !bank.TryGetCueEntry(cue, out CombatAudioCueEntry entry)
                || entry == null)
            {
                RejectedCount++;
                return false;
            }

            if (entry.ClipCount <= 0)
            {
                MissingClipCount++;
                RejectedCount++;
                return false;
            }

            int cueIndex = (int)cue;
            if (cueIndex < 0 || cueIndex >= lastPlayedAt.Length
                || nowSeconds - lastPlayedAt[cueIndex]
                    < entry.CooldownSeconds)
            {
                RejectedCount++;
                return false;
            }

            int activeForCue = 0;
            int freeIndex = -1;
            int worstIndex = -1;
            int worstPriority = int.MinValue;
            for (int index = 0; index < voices.Length; index++)
            {
                VoiceSlot voice = voices[index];
                if (!IsActive(voice, nowSeconds))
                {
                    if (freeIndex < 0)
                    {
                        freeIndex = index;
                    }

                    continue;
                }

                if (voice.Cue == cue)
                {
                    activeForCue++;
                }

                if (voice.Priority > worstPriority)
                {
                    worstPriority = voice.Priority;
                    worstIndex = index;
                }
            }

            if (activeForCue >= entry.MaxConcurrentVoices)
            {
                RejectedCount++;
                return false;
            }

            int slotIndex = freeIndex;
            if (slotIndex < 0)
            {
                if (worstIndex < 0 || entry.Priority >= worstPriority)
                {
                    RejectedCount++;
                    return false;
                }

                slotIndex = worstIndex;
                ClearSlot(slotIndex);
                PreemptedCount++;
            }

            AudioSource source = voices[slotIndex].Source;
            if (source == null)
            {
                RejectedCount++;
                return false;
            }

            int variationIndex = FpgAudioVariationSelection.SelectIndex(
                entry.ClipCount,
                lastVariationIndices[cueIndex],
                FpgAudioVariationSelection.Next(ref variationRandomState));
            AudioClip selectedClip = entry.GetClip(variationIndex);
            if (selectedClip == null)
            {
                MissingClipCount++;
                RejectedCount++;
                return false;
            }

            bool worldPositioned = entry.Space
                == FpgAudioPresentationSpace.WorldPositioned
                && hasWorldPosition;
            if (worldPositioned)
            {
                source.transform.position = worldPosition;
                ConfigureAsWorldPositioned(
                    source,
                    entry.MinDistance,
                    entry.MaxDistance);
            }
            else
            {
                ConfigureAsTwoDimensional(source);
            }

            source.outputAudioMixerGroup = entry.Bus == CombatAudioBus.Ui
                ? uiGroup
                : sfxGroup;
            source.clip = selectedClip;
            source.volume = entry.Volume;
            source.priority = entry.Priority;
            source.Play();

            voices[slotIndex] = new VoiceSlot(
                source,
                cue,
                entry.Priority,
                nowSeconds,
                nowSeconds + Math.Max(0.001f, selectedClip.length));
            lastPlayedAt[cueIndex] = nowSeconds;
            lastVariationIndices[cueIndex] = variationIndex;
            PlayedCount++;
            return true;
        }

        private void ClearSlot(int index)
        {
            if (index < 0 || index >= voices.Length)
            {
                return;
            }

            VoiceSlot voice = voices[index];
            if (voice.Source != null)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.volume = 1f;
                ConfigureAsTwoDimensional(voice.Source);
            }

            voices[index] = voice.Source == null
                ? default(VoiceSlot)
                : new VoiceSlot(voice.Source);
        }

        private static bool IsActive(VoiceSlot voice, double now)
        {
            return voice.Source != null
                && voice.Cue != CombatAudioCue.None
                && now < voice.EndTime;
        }

        private void ResetCooldowns()
        {
            for (int index = 0; index < lastPlayedAt.Length; index++)
            {
                lastPlayedAt[index] = double.NegativeInfinity;
            }
        }

        private void ResetVariations()
        {
            for (int index = 0; index < lastVariationIndices.Length; index++)
            {
                lastVariationIndices[index] = -1;
            }
        }

        private static double GetNow()
        {
            return Time.unscaledTime;
        }

        private static void ConfigureAsTwoDimensional(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

        private static void ConfigureAsWorldPositioned(
            AudioSource source,
            float minDistance,
            float maxDistance)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private readonly struct VoiceSlot
        {
            public VoiceSlot(AudioSource source)
                : this(source, CombatAudioCue.None, 256, 0d, 0d)
            {
            }

            public VoiceSlot(
                AudioSource source,
                CombatAudioCue cue,
                int priority,
                double startTime,
                double endTime)
            {
                Source = source;
                Cue = cue;
                Priority = priority;
                StartTime = startTime;
                EndTime = endTime;
            }

            public AudioSource Source { get; }
            public CombatAudioCue Cue { get; }
            public int Priority { get; }
            public double StartTime { get; }
            public double EndTime { get; }
        }
    }
}
