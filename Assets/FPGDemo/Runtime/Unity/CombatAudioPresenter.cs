using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only D0 audio bridge. It consumes committed feeds and
    /// snapshots, then plays replaceable clips through a fixed AudioSource pool.
    /// It never issues combat commands, performs combat queries, or mutates a
    /// BattleSession.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class CombatAudioPresenter : MonoBehaviour
    {
        private const int DefaultTraceBufferCapacity = CombatTrace.DefaultCapacity;
        private const int DefaultShotBufferCapacity =
            FixedPlayerShotPresentationFeed.DefaultEventCapacity;
        private const int DefaultThreatBufferCapacity = 8;

        [Header("Required scene references")]
        [SerializeField]
        private BattleSessionHost sessionHost;

        [SerializeField]
        private CombatAudioBank audioBank;

        [SerializeField]
        private CombatPresentationProfile presentationProfile;

        [SerializeField]
        private Transform audioSourceRoot;

        [Header("Fixed presentation capacity")]
        [SerializeField, Min(1)]
        private int sourcePoolCapacity = CombatAudioBank.DefaultConcurrentVoiceLimit;

        private readonly CombatTraceCursor traceCursor = new CombatTraceCursor();
        private readonly PlayerShotPresentationCursor shotCursor =
            new PlayerShotPresentationCursor();

        private AudioSource[] sources;
        private CombatAudioCue[] activeCues;
        private float[] voiceStartTimes;
        private bool[] voicePaused;
        private float[] lastCueTimes;
        private CombatAudioCueEntry[] entriesByCue;
        private CombatEvent[] traceBuffer;
        private PlayerShotPresentationEvent[] shotBuffer;
        private ThreatSnapshot[] threatSnapshots;
        private RuntimeId[] cachedThreatRuntimeIds;
        private int[] cachedThreatPresentationKeys;
        private int cachedThreatCount;
        private Transform generatedVoiceRoot;
        private BattleSession boundSession;
        private IPlayerShotPresentationFeed boundShotFeed;
        private bool initialized;
        private bool sourcesPaused;
        private bool skipRetainedEventsOnNextBind;
        private float presentationTime;

        public BattleSessionHost SessionHost => sessionHost;
        public CombatAudioBank AudioBank => audioBank;
        public CombatPresentationProfile PresentationProfile => presentationProfile;
        public Transform AudioSourceRoot => audioSourceRoot;
        public int SourcePoolCapacity => sourcePoolCapacity;
        public bool IsInitialized => initialized;
        public bool IsSourcesPaused => sourcesPaused;
        public int CreatedSourceCount => sources == null ? 0 : sources.Length;
        public int ActiveVoiceCount => CountActiveSources();
        public int PeakActiveVoiceCount { get; private set; }
        public int PlayedCueCount { get; private set; }
        public int CooldownRejectedCount { get; private set; }
        public int ConcurrencyRejectedCount { get; private set; }
        public int PriorityRejectedCount { get; private set; }
        public int PresentationFaultCount { get; private set; }
        public int TraceGapCount => traceCursor.GapCount;
        public int ShotFeedGapCount => shotCursor.GapCount;

        /// <summary>
        /// Checks all static D0 audio dependencies before a pool is allocated.
        /// The full Bank validation is intentionally fail-closed: an installed
        /// G3 presenter must never silently run with missing SFX.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (sessionHost == null)
            {
                error = "CombatAudioPresenter requires a BattleSessionHost.";
                return false;
            }

            if (audioBank == null)
            {
                error = "CombatAudioPresenter requires a CombatAudioBank.";
                return false;
            }

            if (!audioBank.TryValidatePlayback(out error))
            {
                error = $"CombatAudioPresenter audio bank is invalid: {error}";
                return false;
            }

            if (presentationProfile == null)
            {
                error = "CombatAudioPresenter requires a CombatPresentationProfile.";
                return false;
            }

            if (!presentationProfile.TryValidateStatic(out error))
            {
                error = $"CombatAudioPresenter presentation profile is invalid: {error}";
                return false;
            }

            if (audioSourceRoot == null)
            {
                error = "CombatAudioPresenter requires a D0Audio source root.";
                return false;
            }

            if (sourcePoolCapacity != CombatAudioBank.DefaultConcurrentVoiceLimit
                || sourcePoolCapacity != audioBank.ConcurrentVoiceLimit
                || sourcePoolCapacity != presentationProfile.PoolCapacities.AudioSourceCapacity)
            {
                error = "CombatAudioPresenter source pool capacity must match the fixed D0 16-voice contract.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Builds all fixed buffers and all AudioSources outside the playback
        /// hot path. It is safe to call repeatedly after a successful prepare.
        /// </summary>
        public bool TryPrepare(out string error)
        {
            if (initialized)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            Transform nextGeneratedVoiceRoot = null;
            try
            {
                entriesByCue = new CombatAudioCueEntry[(int)CombatAudioCue.Count];
                lastCueTimes = new float[(int)CombatAudioCue.Count];
                for (int index = 0; index < lastCueTimes.Length; index++)
                {
                    lastCueTimes[index] = float.NegativeInfinity;
                }

                for (int index = 0; index < audioBank.CueEntryCount; index++)
                {
                    CombatAudioCueEntry entry = audioBank.GetCueEntry(index);
                    if (entry != null)
                    {
                        entriesByCue[(int)entry.Cue] = entry;
                    }
                }

                int threatCapacity = Mathf.Max(
                    DefaultThreatBufferCapacity,
                    presentationProfile.PoolCapacities.ThreatTelegraphCapacity);
                traceBuffer = new CombatEvent[DefaultTraceBufferCapacity];
                shotBuffer = new PlayerShotPresentationEvent[DefaultShotBufferCapacity];
                threatSnapshots = new ThreatSnapshot[threatCapacity];
                cachedThreatRuntimeIds = new RuntimeId[threatCapacity];
                cachedThreatPresentationKeys = new int[threatCapacity];
                sources = new AudioSource[sourcePoolCapacity];
                activeCues = new CombatAudioCue[sourcePoolCapacity];
                voiceStartTimes = new float[sourcePoolCapacity];
                voicePaused = new bool[sourcePoolCapacity];

                GameObject rootObject = new GameObject("D0CombatAudioVoices");
                nextGeneratedVoiceRoot = rootObject.transform;
                nextGeneratedVoiceRoot.SetParent(audioSourceRoot, false);
                for (int index = 0; index < sourcePoolCapacity; index++)
                {
                    GameObject sourceObject = new GameObject($"AudioVoice_{index:00}");
                    sourceObject.transform.SetParent(nextGeneratedVoiceRoot, false);
                    AudioSource source = sourceObject.AddComponent<AudioSource>();
                    ConfigurePooledSource(source);
                    sources[index] = source;
                    activeCues[index] = CombatAudioCue.None;
                }

                generatedVoiceRoot = nextGeneratedVoiceRoot;
                initialized = true;
                ClearPresentation();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                DestroyObject(nextGeneratedVoiceRoot == null
                    ? null
                    : nextGeneratedVoiceRoot.gameObject);
                DisposeBuffers();
                error = $"CombatAudioPresenter could not prepare its fixed pool: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// Plays a cue derived by another D0 presentation component, such as a
        /// free-reticle lock transition or a threat countdown tick. It remains
        /// presentation-only and accepts no combat payload.
        /// </summary>
        public bool TryPlayPresentationCue(CombatAudioCue cue)
        {
            if (!initialized || sourcesPaused)
            {
                return false;
            }

            BattleSession session = sessionHost == null ? null : sessionHost.Session;
            if (session != null && session.State != BattleSessionState.Running)
            {
                return false;
            }

            return TryPlayCue(cue);
        }

        /// <summary>
        /// Stops every pooled source and clears all transient routing state. It
        /// is used by restart and disable paths, so F5 cannot retain a delayed
        /// or paused sound from the prior battle.
        /// </summary>
        public void ClearPresentation()
        {
            if (sources != null)
            {
                for (int index = 0; index < sources.Length; index++)
                {
                    AudioSource source = sources[index];
                    if (source != null)
                    {
                        source.Stop();
                    }

                    if (activeCues != null)
                    {
                        activeCues[index] = CombatAudioCue.None;
                    }

                    if (voiceStartTimes != null)
                    {
                        voiceStartTimes[index] = 0f;
                    }

                    if (voicePaused != null)
                    {
                        voicePaused[index] = false;
                    }
                }
            }

            if (lastCueTimes != null)
            {
                for (int index = 0; index < lastCueTimes.Length; index++)
                {
                    lastCueTimes[index] = float.NegativeInfinity;
                }
            }

            presentationTime = 0f;
            sourcesPaused = false;
            cachedThreatCount = 0;
        }

        private void Start()
        {
            if (!TryPrepare(out string error))
            {
                Debug.LogError($"[{nameof(CombatAudioPresenter)}] {error}", this);
            }
        }

        private void OnEnable()
        {
            SubscribeToHostRestart();
        }

        private void OnDisable()
        {
            UnsubscribeFromHostRestart();
            ClearPresentation();
            ResetBindings();
            skipRetainedEventsOnNextBind = initialized;
        }

        private void OnDestroy()
        {
            UnsubscribeFromHostRestart();
            ClearPresentation();
            ResetBindings();
            DisposeBuffers();
            DestroyObject(generatedVoiceRoot == null ? null : generatedVoiceRoot.gameObject);
            generatedVoiceRoot = null;
            initialized = false;
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            try
            {
                BattleSession session = sessionHost == null ? null : sessionHost.Session;
                IPlayerShotPresentationFeed shotFeed = sessionHost == null
                    ? null
                    : sessionHost.PlayerShotPresentationFeed;
                RefreshBinding(session, shotFeed);
                if (session == null)
                {
                    return;
                }

                if (session.State == BattleSessionState.Paused)
                {
                    SetSourcesPaused(true);
                    return;
                }

                SetSourcesPaused(false);
                if (session.State == BattleSessionState.Running)
                {
                    presentationTime += Mathf.Max(0f, Time.unscaledDeltaTime);
                    RefreshThreatCache(session);
                    ConsumeCommittedShots();
                    ConsumeCombatTrace(session);
                }
                else if (session.State == BattleSessionState.Completed)
                {
                    // The terminal trace entry is the only source for Victory or
                    // Defeat. Consume it once, then keep its stinger playing.
                    RefreshThreatCache(session);
                    ConsumeCombatTrace(session);
                }
            }
            catch (Exception)
            {
                PresentationFaultCount++;
            }
        }

        private void RefreshBinding(
            BattleSession session,
            IPlayerShotPresentationFeed shotFeed)
        {
            if (ReferenceEquals(boundSession, session)
                && ReferenceEquals(boundShotFeed, shotFeed))
            {
                return;
            }

            ClearPresentation();
            traceCursor.Reset();
            shotCursor.Reset();
            boundSession = session;
            boundShotFeed = shotFeed;
            cachedThreatCount = 0;

            if (boundSession == null)
            {
                return;
            }

            ValidateBoundCapacities(boundSession, boundShotFeed);
            RefreshThreatCache(boundSession);
            if (skipRetainedEventsOnNextBind)
            {
                SetTraceBaseline(boundSession.Trace);
                if (boundShotFeed != null)
                {
                    shotCursor.SetBaseline(boundShotFeed);
                }
            }

            skipRetainedEventsOnNextBind = false;
        }

        private void ValidateBoundCapacities(
            BattleSession session,
            IPlayerShotPresentationFeed shotFeed)
        {
            if (traceBuffer == null || traceBuffer.Length < session.Trace.Capacity
                || threatSnapshots == null || threatSnapshots.Length < session.ThreatCount
                || (shotFeed != null && (shotBuffer == null
                    || shotBuffer.Length < shotFeed.EventCapacity)))
            {
                throw new InvalidOperationException(
                    "CombatAudioPresenter fixed buffers do not match the bound feed capacity.");
            }
        }

        private void ConsumeCommittedShots()
        {
            if (boundShotFeed == null)
            {
                return;
            }

            int eventCount = shotCursor.CopyUnread(boundShotFeed, shotBuffer, out bool hasGap);
            if (hasGap)
            {
                ClearPresentation();
                shotCursor.ResolveGap(boundShotFeed);
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                PlayerShotPresentationEvent shotEvent = shotBuffer[index];
                PlayerShotPresentationSnapshot snapshot = shotEvent.Snapshot;
                if (CombatAudioCueRouting.TryGetShotReleaseCue(snapshot, out CombatAudioCue releaseCue))
                {
                    TryPlayCue(releaseCue);
                    if (releaseCue == CombatAudioCue.PlayerSecondaryRelease)
                    {
                        // D0SecondaryChargeView treats its Release call as the
                        // audited visual HIT boundary. This confirmation layer is
                        // intentionally audio-only and cannot affect the frozen
                        // shot result.
                        TryPlayCue(CombatAudioCue.PlayerConfirmRelease);
                    }
                }

                if (CombatAudioCueRouting.TryGetShotHitCue(snapshot, out CombatAudioCue hitCue))
                {
                    TryPlayCue(hitCue);
                }

                shotCursor.Commit(shotEvent);
            }
        }

        private void ConsumeCombatTrace(BattleSession session)
        {
            int eventCount = traceCursor.CopyUnread(session.Trace, traceBuffer, out bool hasGap);
            if (hasGap)
            {
                ClearPresentation();
                traceCursor.ResolveGap(session.Trace);
                return;
            }

            for (int index = 0; index < eventCount; index++)
            {
                CombatEvent combatEvent = traceBuffer[index];
                if (combatEvent.EventType == CombatEventType.ThreatStateChanged)
                {
                    ThreatState currentState =
                        (ThreatState)combatEvent.ValueAfter;
                    if (currentState != ThreatState.ReleaseCommitted
                        && TryGetCachedThreatPresentationKey(
                            combatEvent.TargetId,
                            out int presentationKey)
                        && CombatAudioCueRouting.TryGetThreatTransitionCue(
                            presentationKey,
                            (ThreatState)combatEvent.ValueBefore,
                            currentState,
                            out CombatAudioCue threatCue))
                    {
                        TryPlayCue(threatCue);
                    }
                }
                else if (CombatAudioCueRouting.TryGetTraceCue(
                             combatEvent,
                             session.PlayerRuntimeId,
                             session.EnemyRuntimeId,
                             out CombatAudioCue cue))
                {
                    TryPlayCue(cue);
                }

                traceCursor.Commit(combatEvent);
            }
        }

        private void RefreshThreatCache(BattleSession session)
        {
            if (session == null || threatSnapshots == null)
            {
                cachedThreatCount = 0;
                return;
            }

            DomainResult copy = session.CopyThreatSnapshots(threatSnapshots, out int count);
            if (!copy.IsSuccess)
            {
                throw new InvalidOperationException(
                    "CombatAudioPresenter could not copy threat snapshots.");
            }

            cachedThreatCount = count;
            for (int index = 0; index < count; index++)
            {
                ThreatSnapshot snapshot = threatSnapshots[index];
                cachedThreatRuntimeIds[index] = snapshot.RuntimeId;
                cachedThreatPresentationKeys[index] = snapshot.PresentationKey;
            }
        }

        private bool TryGetCachedThreatPresentationKey(
            RuntimeId runtimeId,
            out int presentationKey)
        {
            for (int index = 0; index < cachedThreatCount; index++)
            {
                if (cachedThreatRuntimeIds[index] == runtimeId)
                {
                    presentationKey = cachedThreatPresentationKeys[index];
                    return presentationKey > 0;
                }
            }

            presentationKey = 0;
            return false;
        }

        private bool TryPlayCue(CombatAudioCue cue)
        {
            int cueIndex = (int)cue;
            if (cueIndex <= (int)CombatAudioCue.None
                || cueIndex >= (int)CombatAudioCue.Count
                || entriesByCue == null
                || lastCueTimes == null)
            {
                return false;
            }

            CombatAudioCueEntry entry = entriesByCue[cueIndex];
            if (entry == null || entry.Clip == null)
            {
                return false;
            }

            if (presentationTime < lastCueTimes[cueIndex] + entry.CooldownSeconds)
            {
                CooldownRejectedCount++;
                return false;
            }

            int activeForCue = 0;
            int freeVoice = -1;
            for (int index = 0; index < sources.Length; index++)
            {
                if (IsVoiceActive(index))
                {
                    if (activeCues[index] == cue)
                    {
                        activeForCue++;
                    }
                }
                else if (freeVoice < 0)
                {
                    freeVoice = index;
                }
            }

            if (activeForCue >= entry.MaxConcurrentVoices)
            {
                ConcurrencyRejectedCount++;
                return false;
            }

            int selectedVoice = freeVoice >= 0
                ? freeVoice
                : FindStealableVoice(entry.Priority);
            if (selectedVoice < 0)
            {
                PriorityRejectedCount++;
                return false;
            }

            AudioSource source = sources[selectedVoice];
            source.Stop();
            source.clip = entry.Clip;
            source.priority = entry.Priority;
            source.volume = entry.Volume;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.Play();
            activeCues[selectedVoice] = cue;
            voiceStartTimes[selectedVoice] = presentationTime;
            voicePaused[selectedVoice] = false;
            lastCueTimes[cueIndex] = presentationTime;
            PlayedCueCount++;
            int activeCount = CountActiveSources();
            if (activeCount > PeakActiveVoiceCount)
            {
                PeakActiveVoiceCount = activeCount;
            }

            return true;
        }

        private int FindStealableVoice(int incomingPriority)
        {
            int selected = -1;
            int lowestPriority = int.MinValue;
            float oldestStartTime = float.PositiveInfinity;
            for (int index = 0; index < sources.Length; index++)
            {
                if (!IsVoiceActive(index))
                {
                    continue;
                }

                AudioSource source = sources[index];
                int currentPriority = source.priority;
                if (currentPriority <= incomingPriority)
                {
                    continue;
                }

                if (currentPriority > lowestPriority
                    || (currentPriority == lowestPriority
                        && voiceStartTimes[index] < oldestStartTime))
                {
                    selected = index;
                    lowestPriority = currentPriority;
                    oldestStartTime = voiceStartTimes[index];
                }
            }

            return selected;
        }

        private int CountActiveSources()
        {
            if (sources == null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < sources.Length; index++)
            {
                if (IsVoiceActive(index))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsVoiceActive(int index)
        {
            return sources != null
                && index >= 0
                && index < sources.Length
                && sources[index] != null
                && (voicePaused[index] || sources[index].isPlaying);
        }

        private void SetSourcesPaused(bool shouldPause)
        {
            if (sourcesPaused == shouldPause || sources == null)
            {
                return;
            }

            for (int index = 0; index < sources.Length; index++)
            {
                AudioSource source = sources[index];
                if (source == null)
                {
                    continue;
                }

                if (shouldPause)
                {
                    if (source.isPlaying)
                    {
                        source.Pause();
                        voicePaused[index] = true;
                    }
                }
                else if (voicePaused[index])
                {
                    source.UnPause();
                    voicePaused[index] = false;
                }
            }

            sourcesPaused = shouldPause;
        }

        private void SetTraceBaseline(ICombatTraceView trace)
        {
            if (trace != null && trace.Count > 0)
            {
                traceCursor.Commit(trace.GetOldest(trace.Count - 1));
            }
        }

        private void HandleSessionRestarted(BattleSessionHost restartedHost)
        {
            if (restartedHost != sessionHost)
            {
                return;
            }

            ClearPresentation();
            ResetBindings();
            // The replacement session is clean. Its initial bookkeeping events
            // are harmless, while retaining this false prevents a valid first
            // shot immediately after F5 from being silently discarded.
            skipRetainedEventsOnNextBind = false;
        }

        private void SubscribeToHostRestart()
        {
            if (sessionHost != null)
            {
                sessionHost.SessionRestarted -= HandleSessionRestarted;
                sessionHost.SessionRestarted += HandleSessionRestarted;
            }
        }

        private void UnsubscribeFromHostRestart()
        {
            if (sessionHost != null)
            {
                sessionHost.SessionRestarted -= HandleSessionRestarted;
            }
        }

        private void ResetBindings()
        {
            boundSession = null;
            boundShotFeed = null;
            traceCursor.Reset();
            shotCursor.Reset();
            cachedThreatCount = 0;
        }

        private void DisposeBuffers()
        {
            sources = null;
            activeCues = null;
            voiceStartTimes = null;
            voicePaused = null;
            lastCueTimes = null;
            entriesByCue = null;
            traceBuffer = null;
            shotBuffer = null;
            threatSnapshots = null;
            cachedThreatRuntimeIds = null;
            cachedThreatPresentationKeys = null;
            cachedThreatCount = 0;
        }

        private static void ConfigurePooledSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.priority = 256;
            source.volume = 1f;
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
