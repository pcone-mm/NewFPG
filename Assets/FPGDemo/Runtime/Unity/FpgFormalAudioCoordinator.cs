using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Formal-room composition bridge for audio. It observes committed trace,
    /// lifecycle and presentation events, then issues presentation commands.
    /// </summary>
    [DefaultExecutionOrder(950)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalAudioCoordinator : MonoBehaviour
    {
        [Header("Formal presentation sources")]
        [SerializeField] private FpgFormalEncounterHost encounterHost;
        [SerializeField] private FpgRoomEncounterDirector encounterDirector;
        [SerializeField] private FpgFormalCombatFeedbackBridge combatFeedback;
        [SerializeField] private CombatAimReticle aimReticle;

        [Header("Audio presenters")]
        [SerializeField] private CombatAudioPresenter combatAudio;
        [SerializeField] private MusicDirector musicDirector;

        private CombatTrace observedTrace;
        private long nextCombatEventOrdinal;
        private bool reticleStateInitialized;
        private bool previousReticleLocked;
        private bool prepared;
        private bool subscribed;
        private ActiveHeavyWarning[] activeHeavyWarnings =
            Array.Empty<ActiveHeavyWarning>();
        private int activeHeavyWarningCount;

        public bool IsPrepared => prepared;
        public int TraceGapCount { get; private set; }
        public int RoutingFaultCount { get; private set; }
        public int ActiveHeavyWarningCount => activeHeavyWarningCount;
        public int DroppedHeavyWarningCount { get; private set; }

        private void Awake()
        {
            TryPrepare(out _);
        }

        private void OnEnable()
        {
            if (!prepared)
            {
                TryPrepare(out _);
            }

            Subscribe();
        }

        private void LateUpdate()
        {
            if (!prepared)
            {
                return;
            }

            ConsumeCombatTrace();
            ConsumeHeavyCountdowns();
            ConsumeReticleTransition();
        }

        public bool TryPrepare(out string error)
        {
            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            if (encounterHost == null
                || encounterDirector == null
                || combatFeedback == null
                || aimReticle == null
                || combatAudio == null
                || musicDirector == null)
            {
                error = "Formal audio coordinator references are incomplete.";
                return false;
            }

            if (!combatAudio.TryPrepare(out error)
                || !musicDirector.TryPrepare(out error))
            {
                return false;
            }

            observedTrace = null;
            nextCombatEventOrdinal = 0L;
            reticleStateInitialized = false;
            previousReticleLocked = false;
            TraceGapCount = 0;
            RoutingFaultCount = 0;
            int heavyWarningCapacity = combatFeedback.EnemySkillWarningCapacity;
            if (heavyWarningCapacity <= 0)
            {
                error = "Formal audio coordinator needs positive threat telegraph capacity.";
                return false;
            }

            activeHeavyWarnings = new ActiveHeavyWarning[heavyWarningCapacity];
            activeHeavyWarningCount = 0;
            DroppedHeavyWarningCount = 0;
            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TryPresentInteractionFocus()
        {
            return combatAudio != null
                && combatAudio.TryPresent(CombatAudioCue.InteractionFocus);
        }

        public bool TryPresentInteractionConfirm()
        {
            return combatAudio != null
                && combatAudio.TryPresent(CombatAudioCue.InteractionConfirm);
        }

        public bool TryPresentInteractionReject()
        {
            return combatAudio != null
                && combatAudio.TryPresent(CombatAudioCue.InteractionReject);
        }

        private void Subscribe()
        {
            if (!prepared || subscribed)
            {
                return;
            }

            encounterDirector.LifecycleEvent += HandleLifecycle;
            encounterDirector.ExitSelected += HandleExitSelected;
            combatFeedback.EnemySkillWarningChanged += HandleEnemySkillWarning;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (encounterDirector != null)
            {
                encounterDirector.LifecycleEvent -= HandleLifecycle;
                encounterDirector.ExitSelected -= HandleExitSelected;
            }

            if (combatFeedback != null)
            {
                combatFeedback.EnemySkillWarningChanged -=
                    HandleEnemySkillWarning;
            }

            subscribed = false;
        }

        private void HandleLifecycle(FpgEncounterLifecycleEvent lifecycle)
        {
            switch (lifecycle.Type)
            {
                case FpgEncounterLifecycleEventType.Preparing:
                    ClearHeavyWarnings();
                    musicDirector.RestartAmbience();
                    musicDirector.TrySetState(
                        FpgMusicState.Exploration,
                        immediate: true);
                    combatAudio.TryPresent(CombatAudioCue.RoomEntered);
                    break;

                case FpgEncounterLifecycleEventType.Started:
                    musicDirector.TrySetState(FpgMusicState.Combat);
                    break;

                case FpgEncounterLifecycleEventType.EnemyActivated:
                    musicDirector.TrySetState(FpgMusicState.Combat);
                    PresentEnemyLifecycleCue(lifecycle);
                    break;

                case FpgEncounterLifecycleEventType.EnemyDied:
                    PresentEnemyLifecycleCue(lifecycle);
                    break;

                case FpgEncounterLifecycleEventType.ExitUnlocked:
                    combatAudio.TryPresent(CombatAudioCue.ExitUnlocked);
                    break;

                case FpgEncounterLifecycleEventType.RoomCleared:
                    musicDirector.TrySetState(FpgMusicState.Victory);
                    break;

                case FpgEncounterLifecycleEventType.Defeated:
                case FpgEncounterLifecycleEventType.Failed:
                case FpgEncounterLifecycleEventType.Faulted:
                    ClearHeavyWarnings();
                    musicDirector.TrySetState(FpgMusicState.Defeat);
                    break;

                case FpgEncounterLifecycleEventType.Paused:
                    ClearHeavyWarnings();
                    combatAudio.ClearRuntime();
                    musicDirector.SetPaused(true);
                    break;

                case FpgEncounterLifecycleEventType.Resumed:
                    musicDirector.SetPaused(false);
                    break;

                case FpgEncounterLifecycleEventType.Restarted:
                    observedTrace = null;
                    nextCombatEventOrdinal = 0L;
                    reticleStateInitialized = false;
                    ClearHeavyWarnings();
                    combatAudio.ClearRuntime();
                    musicDirector.ClearRuntime();
                    musicDirector.RestartAmbience();
                    musicDirector.TrySetState(
                        FpgMusicState.Exploration,
                        immediate: true);
                    combatAudio.TryPresent(CombatAudioCue.RoomEntered);
                    break;

                case FpgEncounterLifecycleEventType.Disposed:
                    ClearHeavyWarnings();
                    combatAudio.ClearRuntime();
                    musicDirector.ClearRuntime();
                    break;
            }
        }

        private void HandleExitSelected(string exitId)
        {
            combatAudio.TryPresent(CombatAudioCue.ExitConfirmed);
        }

        private void PresentEnemyLifecycleCue(
            in FpgEncounterLifecycleEvent lifecycle)
        {
            if (!CombatAudioCueRouting.TryGetEnemyLifecycleCue(
                    lifecycle.Type,
                    out CombatAudioCue cue))
            {
                return;
            }

            FpgCombatantAnchorMap anchors = encounterHost.CombatantAnchorMap;
            if (anchors != null
                && anchors.TryGet(
                    lifecycle.RuntimeId,
                    out FpgCombatantAnchorSnapshot snapshot))
            {
                Vector3 position = snapshot.GameplayAnchor != null
                    ? snapshot.GameplayAnchor.position
                    : snapshot.LastPose.position;
                combatAudio.TryPresentAt(cue, position);
                return;
            }

            combatAudio.TryPresent(cue);
        }

        private void HandleEnemySkillWarning(
            FpgFormalEnemySkillWarningPresentationEvent warningEvent)
        {
            FpgFormalEnemySkillTimelineEvent timeline =
                warningEvent.TimelineEvent;
            if (!TryResolveThreatPresentationKind(
                    timeline.Definition,
                    out FpgThreatPresentationKind presentationKind))
            {
                RoutingFaultCount++;
                return;
            }

            ThreatState previous = warningEvent.IsActive
                ? ThreatState.Scheduled
                : ThreatState.Telegraph;
            ThreatState current = warningEvent.IsActive
                ? ThreatState.Telegraph
                : ThreatState.ReleaseCommitted;
            if (!CombatAudioCueRouting.TryGetThreatTransitionCue(
                    presentationKind,
                    previous,
                    current,
                    out CombatAudioCue cue))
            {
                return;
            }

            bool hasPosition = encounterDirector.TryResolveEnemyPresentationSource(
                    timeline.OwnerRuntimeId,
                    timeline.SpawnSequence,
                    warningEvent.Resolved.SocketName,
                    out Transform source)
                && source != null;
            Vector3 position = hasPosition ? source.position : Vector3.zero;
            if (hasPosition)
            {
                combatAudio.TryPresentAt(cue, position);
            }
            else
            {
                combatAudio.TryPresent(cue);
            }

            if (presentationKind == FpgThreatPresentationKind.HeavyWeakpoint)
            {
                if (warningEvent.IsActive)
                {
                    TrackHeavyWarning(warningEvent, hasPosition, position);
                }
                else
                {
                    RemoveHeavyWarning(
                        timeline.RuntimeEvent.ExecutionId,
                        timeline.Event.WarningId);
                }
            }
        }

        private void TrackHeavyWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent,
            bool hasPosition,
            Vector3 position)
        {
            FpgFormalEnemySkillTimelineEvent timeline =
                warningEvent.TimelineEvent;
            SkillExecutionId executionId = timeline.RuntimeEvent.ExecutionId;
            TickIndex scheduledStart = timeline.RuntimeEvent.ScheduledTick;
            int authoredDuration = warningEvent.Resolved.EndTick
                - warningEvent.Resolved.StartTick;
            if (!executionId.IsValid
                || !scheduledStart.IsValid
                || authoredDuration <= 0
                || scheduledStart.Value > long.MaxValue - authoredDuration)
            {
                RoutingFaultCount++;
                return;
            }

            int warningId = timeline.Event.WarningId;
            int freeIndex = -1;
            for (int index = 0; index < activeHeavyWarnings.Length; index++)
            {
                if (activeHeavyWarnings[index].IsActive)
                {
                    if (activeHeavyWarnings[index].ExecutionId == executionId
                        && activeHeavyWarnings[index].WarningId == warningId)
                    {
                        freeIndex = index;
                        break;
                    }

                    continue;
                }

                if (freeIndex < 0)
                {
                    freeIndex = index;
                }
            }

            if (freeIndex < 0)
            {
                DroppedHeavyWarningCount++;
                return;
            }

            bool replacing = activeHeavyWarnings[freeIndex].IsActive;
            activeHeavyWarnings[freeIndex] = new ActiveHeavyWarning
            {
                IsActive = true,
                ExecutionId = executionId,
                WarningId = warningId,
                EndTick = new TickIndex(
                    scheduledStart.Value + authoredDuration),
                PreviousDisplayedSeconds = -1,
                HasPosition = hasPosition,
                Position = position,
            };
            if (!replacing)
            {
                activeHeavyWarningCount++;
            }
        }

        private void ConsumeHeavyCountdowns()
        {
            TickIndex currentTick = encounterDirector == null
                ? TickIndex.Invalid
                : encounterDirector.CurrentTick;
            if (!currentTick.IsValid)
            {
                return;
            }

            for (int index = 0; index < activeHeavyWarnings.Length; index++)
            {
                ActiveHeavyWarning warning = activeHeavyWarnings[index];
                if (!warning.IsActive)
                {
                    continue;
                }

                long remainingTicks = warning.EndTick.Value - currentTick.Value;
                if (remainingTicks <= 0L)
                {
                    ClearHeavyWarningAt(index);
                    continue;
                }

                int displayedSeconds = CombatAudioCueRouting
                    .GetHeavyDisplayedSeconds(remainingTicks);
                if (CombatAudioCueRouting.TryGetHeavyCountdownCue(
                        warning.PreviousDisplayedSeconds,
                        displayedSeconds,
                        out CombatAudioCue cue))
                {
                    if (warning.HasPosition)
                    {
                        combatAudio.TryPresentAt(cue, warning.Position);
                    }
                    else
                    {
                        combatAudio.TryPresent(cue);
                    }
                }

                warning.PreviousDisplayedSeconds = displayedSeconds;
                activeHeavyWarnings[index] = warning;
            }
        }

        private void RemoveHeavyWarning(
            SkillExecutionId executionId,
            int warningId)
        {
            for (int index = 0; index < activeHeavyWarnings.Length; index++)
            {
                ActiveHeavyWarning warning = activeHeavyWarnings[index];
                if (warning.IsActive
                    && warning.ExecutionId == executionId
                    && warning.WarningId == warningId)
                {
                    ClearHeavyWarningAt(index);
                    return;
                }
            }
        }

        private void ClearHeavyWarningAt(int index)
        {
            if (!activeHeavyWarnings[index].IsActive)
            {
                return;
            }

            activeHeavyWarnings[index] = default(ActiveHeavyWarning);
            activeHeavyWarningCount--;
        }

        private void ClearHeavyWarnings()
        {
            Array.Clear(
                activeHeavyWarnings,
                0,
                activeHeavyWarnings.Length);
            activeHeavyWarningCount = 0;
        }

        private void ConsumeCombatTrace()
        {
            FpgFormalCombatRuntimeBundle runtime = encounterHost.CombatRuntime;
            CombatTrace trace = runtime == null || runtime.IsDisposed
                || runtime.CombatKernel == null
                    ? null
                    : runtime.CombatKernel.Trace;
            if (!ReferenceEquals(trace, observedTrace))
            {
                observedTrace = trace;
                nextCombatEventOrdinal = trace == null
                    ? 0L
                    : trace.TotalEventCount;
                return;
            }

            if (trace == null || runtime == null)
            {
                return;
            }

            long total = trace.TotalEventCount;
            long oldest = total - trace.Count;
            if (nextCombatEventOrdinal < oldest
                || nextCombatEventOrdinal > total)
            {
                nextCombatEventOrdinal = total;
                TraceGapCount++;
                return;
            }

            RuntimeId playerRuntimeId = runtime.Player.RuntimeId;
            for (long ordinal = nextCombatEventOrdinal;
                ordinal < total;
                ordinal++)
            {
                CombatEvent combatEvent = trace.GetOldest(
                    (int)(ordinal - oldest));
                if (!CombatAudioCueRouting.TryGetTraceCue(
                        combatEvent,
                        playerRuntimeId,
                        combatEvent.TargetId,
                        out CombatAudioCue cue))
                {
                    continue;
                }

                if (TryResolveCombatEventPosition(
                        runtime,
                        combatEvent,
                        out Vector3 position))
                {
                    combatAudio.TryPresentAt(cue, position);
                }
                else
                {
                    combatAudio.TryPresent(cue);
                }
            }

            nextCombatEventOrdinal = total;
        }

        private void ConsumeReticleTransition()
        {
            bool currentLocked = aimReticle.TargetState
                == FpgReticleTargetState.Hittable;
            if (!reticleStateInitialized)
            {
                reticleStateInitialized = true;
                previousReticleLocked = currentLocked;
                return;
            }

            if (CombatAudioCueRouting.TryGetReticleLockCue(
                    previousReticleLocked,
                    currentLocked,
                    out CombatAudioCue cue))
            {
                combatAudio.TryPresent(cue);
            }

            previousReticleLocked = currentLocked;
        }

        private bool TryResolveCombatEventPosition(
            FpgFormalCombatRuntimeBundle runtime,
            in CombatEvent combatEvent,
            out Vector3 position)
        {
            if (combatEvent.TargetId == runtime.Player.RuntimeId
                && encounterHost.ActivePlayerEntity != null)
            {
                position = encounterHost.ActivePlayerEntity.transform.position;
                return true;
            }

            FpgCombatantAnchorMap anchors = encounterHost.CombatantAnchorMap;
            if (anchors != null
                && anchors.TryGet(
                    combatEvent.TargetId,
                    out FpgCombatantAnchorSnapshot snapshot))
            {
                position = snapshot.GameplayAnchor != null
                    ? snapshot.GameplayAnchor.position
                    : snapshot.LastPose.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private static bool TryResolveThreatPresentationKind(
            FpgEnemyAttackDefinition definition,
            out FpgThreatPresentationKind kind)
        {
            if (definition != null
                && definition.TryCompile(
                    out FpgCompiledEnemySkillDefinition compiled,
                    out _))
            {
                for (int index = 0;
                    index < compiled.GameplayActionCount;
                    index++)
                {
                    FpgCompiledEnemySkillAction action =
                        compiled.GetGameplayAction(index);
                    if (action.Kind == FpgEnemySkillActionKind.Projectile
                        || action.Kind == FpgEnemySkillActionKind.TimedImpact)
                    {
                        kind = action.ThreatPayload.PresentationKind;
                        return true;
                    }
                }
            }

            kind = default(FpgThreatPresentationKind);
            return false;
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearHeavyWarnings();
            combatAudio?.ClearRuntime();
            musicDirector?.ClearRuntime();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private struct ActiveHeavyWarning
        {
            public bool IsActive;
            public SkillExecutionId ExecutionId;
            public int WarningId;
            public TickIndex EndTick;
            public int PreviousDisplayedSeconds;
            public bool HasPosition;
            public Vector3 Position;
        }
    }
}
