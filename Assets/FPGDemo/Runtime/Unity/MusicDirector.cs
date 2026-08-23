using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FPG.Demo.Unity
{
    public enum FpgMusicState
    {
        None = 0,
        Exploration,
        Combat,
        Victory,
        Defeat
    }

    /// <summary>
    /// Explicitly commanded music and ambience presenter. It never reads
    /// encounter state; a room composition bridge owns the commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour
    {
        private const int MaximumAmbiencePointVoiceCount = 4;

        [SerializeField] private FpgRoomAudioProfile profile;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private bool returnToExplorationAfterVictory = true;

        private AudioSource[] musicSources = Array.Empty<AudioSource>();
        private AudioSource ambienceSource;
        private AudioSource[] ambiencePointSources = Array.Empty<AudioSource>();
        private float[] ambiencePointVoiceRemaining = Array.Empty<float>();
        private int activeMusicSource = -1;
        private FpgMusicState state;
        private FpgMusicState pendingStinger;
        private float transitionRemaining;
        private float transitionDuration;
        private float stingerRemaining;
        private int[] lastMusicVariationIndices = Array.Empty<int>();
        private uint musicVariationRandomState;
        private int lastAmbienceVariationIndex = -1;
        private uint ambienceVariationRandomState;
        private int lastAmbiencePointVariationIndex = -1;
        private uint ambiencePointRandomState;
        private float ambiencePointDelayRemaining = float.PositiveInfinity;
        private bool prepared;
        private bool paused;

        public FpgRoomAudioProfile Profile => profile;
        public FpgMusicState State => state;
        public bool IsPrepared => prepared;
        public float TransitionRemaining => transitionRemaining;
        public bool IsPaused => paused;
        public AudioClip ActiveMusicClip =>
            activeMusicSource < 0 || activeMusicSource >= musicSources.Length
                ? null
                : musicSources[activeMusicSource].clip;
        public AudioClip ActiveAmbienceClip =>
            ambienceSource == null ? null : ambienceSource.clip;
        public AudioClip LastAmbiencePointClip { get; private set; }
        public Vector3 LastAmbiencePointLocalPosition { get; private set; }
        public float AmbiencePointDelayRemaining =>
            ambiencePointDelayRemaining;
        public int AmbiencePointPlayedCount { get; private set; }
        public int AmbiencePointRejectedCount { get; private set; }

        public void SetConfiguration(
            FpgRoomAudioProfile nextProfile,
            AudioMixerGroup nextMusicGroup,
            AudioMixerGroup nextAmbienceGroup)
        {
            if (prepared)
            {
                throw new InvalidOperationException(
                    "Music director configuration cannot change after preparation.");
            }

            profile = nextProfile;
            musicGroup = nextMusicGroup;
            ambienceGroup = nextAmbienceGroup;
        }

        public bool TryPrepare(out string error)
        {
            error = string.Empty;
            if (prepared)
            {
                return true;
            }

            if (profile == null || !profile.TryValidateMapping(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Music director requires a room audio profile.";
                }

                return false;
            }

            GameObject root = null;
            try
            {
                root = new GameObject("MusicSources");
                root.transform.SetParent(transform, false);
                musicVariationRandomState = unchecked(
                    (uint)(Environment.TickCount ^ GetInstanceID() ^ 0x51ED270Bu));
                lastMusicVariationIndices =
                    new int[(int)FpgMusicState.Defeat + 1];
                for (int index = 0;
                     index < lastMusicVariationIndices.Length;
                     index++)
                {
                    lastMusicVariationIndices[index] = -1;
                }

                musicSources = new AudioSource[2];
                for (int index = 0; index < musicSources.Length; index++)
                {
                    GameObject child = new GameObject(
                        "Music_" + index.ToString("0"));
                    child.transform.SetParent(root.transform, false);
                    AudioSource source = child.AddComponent<AudioSource>();
                    ConfigureMusicSource(source, musicGroup);
                    musicSources[index] = source;
                }

                GameObject ambienceObject = new GameObject("Ambience");
                ambienceObject.transform.SetParent(root.transform, false);
                ambienceSource = ambienceObject.AddComponent<AudioSource>();
                ConfigureMusicSource(ambienceSource, ambienceGroup);
                ambienceSource.loop = true;
                ambienceVariationRandomState = unchecked(
                    (uint)(Environment.TickCount ^ GetInstanceID()));
                lastAmbienceVariationIndex = -1;
                ambienceSource.clip = SelectAmbienceClip();
                ambienceSource.volume = ambienceSource.clip == null ? 0f : 1f;
                if (ambienceSource.clip != null)
                {
                    ambienceSource.Play();
                }

                PrepareAmbiencePointSources(root.transform);

                prepared = true;
                state = FpgMusicState.None;
                activeMusicSource = -1;
                transitionRemaining = 0f;
                pendingStinger = FpgMusicState.None;
                stingerRemaining = 0f;
                paused = false;
                error = string.Empty;
                generatedRoot = root;
                return true;
            }
            catch (Exception exception)
            {
                if (root != null)
                {
                    DestroyObject(root);
                }

                musicSources = Array.Empty<AudioSource>();
                ambienceSource = null;
                ambiencePointSources = Array.Empty<AudioSource>();
                ambiencePointVoiceRemaining = Array.Empty<float>();
                error = "Music director preparation failed: " + exception.Message;
                return false;
            }
        }

        public bool TrySetState(FpgMusicState nextState, bool immediate = false)
        {
            if (!prepared || !Enum.IsDefined(typeof(FpgMusicState), nextState)
                || nextState == FpgMusicState.None)
            {
                return false;
            }

            if (nextState == state && !immediate)
            {
                return true;
            }

            AudioClip clip = ResolveClip(nextState);
            if (clip == null)
            {
                state = nextState;
                pendingStinger = FpgMusicState.None;
                return false;
            }

            StartClip(clip, nextState, immediate);
            return true;
        }

        public void ClearRuntime()
        {
            for (int index = 0; index < musicSources.Length; index++)
            {
                if (musicSources[index] == null)
                {
                    continue;
                }

                musicSources[index].Stop();
                musicSources[index].clip = null;
                musicSources[index].volume = 0f;
            }

            if (ambienceSource != null)
            {
                ambienceSource.Stop();
            }

            ClearAmbiencePointVoices();
            ResetAmbiencePointState();

            activeMusicSource = -1;
            transitionRemaining = 0f;
            pendingStinger = FpgMusicState.None;
            stingerRemaining = 0f;
            state = FpgMusicState.None;
            paused = false;
        }

        public void SetPaused(bool value)
        {
            if (!prepared || paused == value)
            {
                return;
            }

            paused = value;
            for (int index = 0; index < musicSources.Length; index++)
            {
                if (musicSources[index] == null)
                {
                    continue;
                }

                if (paused)
                {
                    musicSources[index].Pause();
                }
                else
                {
                    musicSources[index].UnPause();
                }
            }

            if (ambienceSource != null)
            {
                if (paused)
                {
                    ambienceSource.Pause();
                }
                else
                {
                    ambienceSource.UnPause();
                }
            }


            for (int index = 0;
                 index < ambiencePointSources.Length;
                 index++)
            {
                AudioSource source = ambiencePointSources[index];
                if (source == null)
                {
                    continue;
                }

                if (paused)
                {
                    source.Pause();
                }
                else
                {
                    source.UnPause();
                }
            }
        }

        public void RestartAmbience()
        {
            if (!prepared || ambienceSource == null)
            {
                return;
            }

            ambienceSource.Stop();
            ambienceSource.clip = SelectAmbienceClip();
            ambienceSource.loop = true;
            ambienceSource.volume = ambienceSource.clip == null ? 0f : 1f;
            if (ambienceSource.clip != null)
            {
                ambienceSource.Play();
            }

            ClearAmbiencePointVoices();
            ScheduleNextAmbiencePoint();
        }

        public void Dispose()
        {
            ClearRuntime();
            if (generatedRoot != null)
            {
                DestroyObject(generatedRoot);
            }

            generatedRoot = null;
            musicSources = Array.Empty<AudioSource>();
            ambienceSource = null;
            ambiencePointSources = Array.Empty<AudioSource>();
            ambiencePointVoiceRemaining = Array.Empty<float>();
            lastMusicVariationIndices = Array.Empty<int>();
            musicVariationRandomState = 0u;
            lastAmbienceVariationIndex = -1;
            ambienceVariationRandomState = 0u;
            lastAmbiencePointVariationIndex = -1;
            ambiencePointRandomState = 0u;
            ambiencePointDelayRemaining = float.PositiveInfinity;
            LastAmbiencePointClip = null;
            LastAmbiencePointLocalPosition = Vector3.zero;
            AmbiencePointPlayedCount = 0;
            AmbiencePointRejectedCount = 0;
            prepared = false;
        }

        private GameObject generatedRoot;

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (!prepared || paused)
            {
                return;
            }

            float delta = Mathf.Max(0f, unscaledDeltaTime);
            if (transitionRemaining > 0f)
            {
                transitionRemaining = Mathf.Max(
                    0f,
                    transitionRemaining - delta);
                float t = transitionDuration <= 0f
                    ? 1f
                    : 1f - transitionRemaining / transitionDuration;
                int previous = 1 - activeMusicSource;
                if (previous >= 0 && previous < musicSources.Length)
                {
                    musicSources[previous].volume = 1f - t;
                }

                if (activeMusicSource >= 0)
                {
                    musicSources[activeMusicSource].volume = t;
                }

                if (transitionRemaining <= 0f
                    && previous >= 0
                    && previous < musicSources.Length)
                {
                    musicSources[previous].Stop();
                    musicSources[previous].clip = null;
                    musicSources[previous].volume = 0f;
                }
            }

            if (stingerRemaining > 0f)
            {
                stingerRemaining = Mathf.Max(0f, stingerRemaining - delta);
                if (stingerRemaining <= 0f
                    && pendingStinger == FpgMusicState.Victory
                    && returnToExplorationAfterVictory)
                {
                    pendingStinger = FpgMusicState.None;
                    TrySetState(FpgMusicState.Exploration);
                }
            }


            AdvanceAmbiencePoints(delta);
        }

        private void StartClip(
            AudioClip clip,
            FpgMusicState nextState,
            bool immediate)
        {
            int nextSource = activeMusicSource < 0
                ? 0
                : 1 - activeMusicSource;
            AudioSource source = musicSources[nextSource];
            source.Stop();
            source.clip = clip;
            source.loop = nextState == FpgMusicState.Exploration
                || nextState == FpgMusicState.Combat;
            source.volume = immediate || activeMusicSource < 0 ? 1f : 0f;
            source.Play();

            if (immediate || activeMusicSource < 0)
            {
                for (int index = 0; index < musicSources.Length; index++)
                {
                    if (index == nextSource)
                    {
                        continue;
                    }

                    musicSources[index].Stop();
                    musicSources[index].clip = null;
                    musicSources[index].volume = 0f;
                }

                transitionRemaining = 0f;
            }
            else
            {
                transitionDuration = profile == null
                    ? 0f
                    : profile.MusicFadeSeconds;
                transitionRemaining = transitionDuration;
            }

            activeMusicSource = nextSource;
            state = nextState;
            pendingStinger = nextState == FpgMusicState.Victory
                || nextState == FpgMusicState.Defeat
                    ? nextState
                    : FpgMusicState.None;
            stingerRemaining = pendingStinger == FpgMusicState.None
                ? 0f
                : clip.length;
        }

        private AudioClip ResolveClip(FpgMusicState nextState)
        {
            int stateIndex = (int)nextState;
            int clipCount = profile == null
                ? 0
                : profile.GetMusicClipCount(nextState);
            if (clipCount <= 0
                || stateIndex < 0
                || stateIndex >= lastMusicVariationIndices.Length)
            {
                return null;
            }

            int selectedIndex = FpgAudioVariationSelection.SelectIndex(
                clipCount,
                lastMusicVariationIndices[stateIndex],
                FpgAudioVariationSelection.Next(ref musicVariationRandomState));
            lastMusicVariationIndices[stateIndex] = selectedIndex;
            return profile.GetMusicClip(nextState, selectedIndex);
        }

        private AudioClip SelectAmbienceClip()
        {
            int clipCount = profile == null ? 0 : profile.AmbienceClipCount;
            if (clipCount <= 0)
            {
                return null;
            }

            int selectedIndex = FpgAudioVariationSelection.SelectIndex(
                clipCount,
                lastAmbienceVariationIndex,
                FpgAudioVariationSelection.Next(
                    ref ambienceVariationRandomState));
            lastAmbienceVariationIndex = selectedIndex;
            return profile.GetAmbienceClip(selectedIndex);
        }

        private void PrepareAmbiencePointSources(Transform parent)
        {
            ambiencePointRandomState = unchecked(
                (uint)(Environment.TickCount ^ GetInstanceID() ^ 0x6A09E667u));
            lastAmbiencePointVariationIndex = -1;
            LastAmbiencePointClip = null;
            LastAmbiencePointLocalPosition = Vector3.zero;
            AmbiencePointPlayedCount = 0;
            AmbiencePointRejectedCount = 0;

            int clipCount = profile == null
                ? 0
                : profile.AmbiencePointClipCount;
            if (clipCount <= 0)
            {
                ambiencePointSources = Array.Empty<AudioSource>();
                ambiencePointVoiceRemaining = Array.Empty<float>();
                ambiencePointDelayRemaining = float.PositiveInfinity;
                return;
            }

            int voiceCount = Mathf.Clamp(
                profile.AmbiencePointVoiceLimit,
                1,
                MaximumAmbiencePointVoiceCount);
            ambiencePointSources = new AudioSource[voiceCount];
            ambiencePointVoiceRemaining = new float[voiceCount];
            GameObject pointRoot = new GameObject("AmbiencePoints");
            pointRoot.transform.SetParent(parent, false);
            for (int index = 0; index < voiceCount; index++)
            {
                GameObject child = new GameObject(
                    "AmbiencePoint_" + index.ToString("00"));
                child.transform.SetParent(pointRoot.transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                ConfigureAmbiencePointSource(source, ambienceGroup, profile);
                ambiencePointSources[index] = source;
            }

            ScheduleNextAmbiencePoint();
        }

        private void AdvanceAmbiencePoints(float delta)
        {
            if (profile == null || profile.AmbiencePointClipCount <= 0)
            {
                return;
            }

            for (int index = 0;
                 index < ambiencePointVoiceRemaining.Length;
                 index++)
            {
                if (ambiencePointVoiceRemaining[index] <= 0f)
                {
                    continue;
                }

                ambiencePointVoiceRemaining[index] = Mathf.Max(
                    0f,
                    ambiencePointVoiceRemaining[index] - delta);
                if (ambiencePointVoiceRemaining[index] > 0f)
                {
                    continue;
                }

                AudioSource source = ambiencePointSources[index];
                source.Stop();
                source.clip = null;
            }

            ambiencePointDelayRemaining -= delta;
            if (ambiencePointDelayRemaining > 0f)
            {
                return;
            }

            TryPlayAmbiencePoint();
            ScheduleNextAmbiencePoint();
        }

        private bool TryPlayAmbiencePoint()
        {
            int sourceIndex = -1;
            for (int index = 0;
                 index < ambiencePointVoiceRemaining.Length;
                 index++)
            {
                if (ambiencePointVoiceRemaining[index] <= 0f)
                {
                    sourceIndex = index;
                    break;
                }
            }

            if (sourceIndex < 0)
            {
                AmbiencePointRejectedCount++;
                return false;
            }

            int clipCount = profile.AmbiencePointClipCount;
            int clipIndex = FpgAudioVariationSelection.SelectIndex(
                clipCount,
                lastAmbiencePointVariationIndex,
                FpgAudioVariationSelection.Next(ref ambiencePointRandomState));
            AudioClip clip = profile.GetAmbiencePointClip(clipIndex);
            if (clip == null)
            {
                AmbiencePointRejectedCount++;
                return false;
            }

            lastAmbiencePointVariationIndex = clipIndex;
            Vector3 localPosition = new Vector3(
                NextSignedAmbiencePointValue()
                    * profile.AmbiencePointHorizontalExtent,
                NextSignedAmbiencePointValue()
                    * profile.AmbiencePointVerticalExtent,
                0f);
            AudioSource source = ambiencePointSources[sourceIndex];
            source.Stop();
            source.transform.localPosition = localPosition;
            source.clip = clip;
            source.volume = profile.AmbiencePointVolume;
            source.Play();
            ambiencePointVoiceRemaining[sourceIndex] = clip.length;
            LastAmbiencePointClip = clip;
            LastAmbiencePointLocalPosition = localPosition;
            AmbiencePointPlayedCount++;
            return true;
        }

        private void ScheduleNextAmbiencePoint()
        {
            if (profile == null || profile.AmbiencePointClipCount <= 0)
            {
                ambiencePointDelayRemaining = float.PositiveInfinity;
                return;
            }

            float minimum = profile.AmbiencePointMinIntervalSeconds;
            float maximum = profile.AmbiencePointMaxIntervalSeconds;
            float normalized = NextAmbiencePointValue();
            ambiencePointDelayRemaining = Mathf.Lerp(
                minimum,
                maximum,
                normalized);
        }

        private void ClearAmbiencePointVoices()
        {
            for (int index = 0; index < ambiencePointSources.Length; index++)
            {
                AudioSource source = ambiencePointSources[index];
                if (source != null)
                {
                    source.Stop();
                    source.clip = null;
                }

                if (index < ambiencePointVoiceRemaining.Length)
                {
                    ambiencePointVoiceRemaining[index] = 0f;
                }
            }
        }

        private void ResetAmbiencePointState()
        {
            lastAmbiencePointVariationIndex = -1;
            LastAmbiencePointClip = null;
            LastAmbiencePointLocalPosition = Vector3.zero;
            AmbiencePointPlayedCount = 0;
            AmbiencePointRejectedCount = 0;
            ScheduleNextAmbiencePoint();
        }

        private float NextAmbiencePointValue()
        {
            uint value = FpgAudioVariationSelection.Next(
                ref ambiencePointRandomState);
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private float NextSignedAmbiencePointValue()
        {
            return NextAmbiencePointValue() * 2f - 1f;
        }

        private static void ConfigureMusicSource(
            AudioSource source,
            AudioMixerGroup group)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = group;
        }

        private static void ConfigureAmbiencePointSource(
            AudioSource source,
            AudioMixerGroup group,
            FpgRoomAudioProfile roomProfile)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = roomProfile.AmbiencePointMinDistance;
            source.maxDistance = roomProfile.AmbiencePointMaxDistance;
            source.outputAudioMixerGroup = group;
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
    }
}
