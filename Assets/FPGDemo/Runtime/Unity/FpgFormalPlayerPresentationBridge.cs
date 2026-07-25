using System;
using FPG.Demo.Combat;
using FPG.Demo.Run;
using FPG.Demo.Player;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only formal player boundary. Composition injects the
    /// selected definition/entity/profile; this bridge observes committed
    /// formal runtime state and never writes combat decisions.
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerPresentationBridge : MonoBehaviour,
        IFpgFormalPlayerPresentationSource
    {
        private const int ActionQueueCapacity = 32;
        private const int SkillPresentationQueueCapacity = 64;
        private const string AnimationCuePrefix = "animation.";
        [Header("Formal runtime")]
        [SerializeField] private FpgRoomEncounterDirector encounterDirector;
        [SerializeField] private FpgFormalPlayerTickDriver playerTickDriver;

        [Header("Player presentation")]
        [SerializeField] private FpgFormalPlayerHudPresenter playerHud;
        [SerializeField] private FpgFormalPlayerCameraFeedback cameraFeedback;
        [SerializeField] private D0CombatVfxWorld skillVfxWorld;

        [Header("Scene-owned camera")]
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Camera targetCamera;

        private readonly FpgFormalPlayerActionEvent[] actionQueue =
            new FpgFormalPlayerActionEvent[ActionQueueCapacity];
        private readonly FpgFormalPlayerSkillSequenceEvent[] skillSequenceQueue =
            new FpgFormalPlayerSkillSequenceEvent[
                SkillPresentationQueueCapacity];
        private readonly FpgFormalPlayerSkillCueEvent[] skillCueQueue =
            new FpgFormalPlayerSkillCueEvent[
                SkillPresentationQueueCapacity];
        private FpgVitalsSnapshot[] vitalsBuffer =
            Array.Empty<FpgVitalsSnapshot>();

        private FpgPlayableCharacterSelection selection;
        private FpgPlayerEntityView playerEntity;
        private Actor2DPresenter actorPresenter;
        private CombatTrace observedTrace;
        private FpgFormalCombatRuntimeBundle observedVitalsRuntime;
        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private long nextCombatEventOrdinal;
        private long vitalsCursor;
        private int actionHead;
        private int actionCount;
        private int skillSequenceHead;
        private int skillSequenceCount;
        private int skillCueHead;
        private int skillCueCount;
        private bool actionGap;
        private bool skillSequenceGap;
        private bool hasActiveSkillSequence;
        private FpgFormalPlayerSkillSequenceEvent activeSkillSequence;
        private bool prepared;
        private bool active;
        private bool subscribed;

        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public FpgFormalPlayerTickDriver PlayerTickDriver => playerTickDriver;
        public FpgFormalPlayerHudPresenter PlayerHud => playerHud;
        public FpgFormalPlayerCameraFeedback CameraFeedback => cameraFeedback;
        public Transform CameraRig => cameraRig;
        public Camera TargetCamera => targetCamera;
        public FpgPlayableCharacterSelection Selection => selection;
        public FpgPlayerEntityView PlayerEntity => playerEntity;
        public FpgFormalPlayerPresentationSnapshot Snapshot => snapshot;
        public bool IsPrepared => prepared;
        public bool IsActive => active;
        public int VitalsGapCount { get; private set; }
        public int VitalsReadCapacity => vitalsBuffer.Length;
        public int SkillSequenceGapCount { get; private set; }
        public int SkillCueGapCount { get; private set; }
        public int SkillPresentationFaultCount { get; private set; }

        public event Action<FpgFormalPlayerSkillCueEvent> SkillCuePresented;

        private void LateUpdate()
        {
            if (!active || encounterDirector == null || playerTickDriver == null)
            {
                return;
            }

            if (!playerTickDriver.TryRefreshPresentationSnapshot(
                    out FpgFormalPlayerPresentationSnapshot nextSnapshot))
            {
                return;
            }

            nextSnapshot = ApplyVitalsChanges(nextSnapshot);

            FpgFormalPlayerPresentationSnapshot previous = snapshot;
            snapshot = nextSnapshot;
            ConsumeSkillSequenceEvents();
            bool primaryPresented = ConsumeCommittedActions();
            PresentationFrameFlags traceFlags = ConsumeCombatTrace();
            ApplySnapshotTransitions(
                previous,
                snapshot,
                primaryPresented,
                traceFlags);
            actorPresenter?.SetPaused(snapshot.IsPaused);
            EvaluateActiveSkillAnimation();
            ConsumeSkillCues();
            cameraFeedback?.SetPaused(snapshot.IsPaused);
            playerHud?.Refresh(snapshot);
        }

        public bool TryPrepare(
            FpgPlayableCharacterSelection nextSelection,
            FpgPlayerEntityView nextPlayerEntity,
            out string error)
        {
            if (prepared)
            {
                error = "Formal player presentation supports one preparation per scene lifetime.";
                return false;
            }

            if (!TryValidateAuthoring(out error)
                || !nextSelection.TryValidate(out error))
            {
                return false;
            }

            if (nextPlayerEntity == null || !nextPlayerEntity.TryValidate(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal player presentation requires a valid scene entity.";
                }

                return false;
            }

            D0CharacterDefinition definition = nextSelection.CharacterDefinition;
            if (!playerTickDriver.IsPlayerConfigured
                || playerTickDriver.PlayerDefinition != definition
                || playerTickDriver.PlayerEntity != nextPlayerEntity
                || playerTickDriver.ThreeCProfile != nextSelection.ThreeCProfile)
            {
                error = "Formal player presentation and tick driver must share the selected player binding.";
                return false;
            }

            Actor2DPresenter nextActorPresenter = nextPlayerEntity.ActorPresenter;
            if (nextActorPresenter == null || !nextActorPresenter.IsInitialized
                || nextActorPresenter.RuntimePresentationOverride
                    != definition.ActorPresentation
                || nextActorPresenter.RuntimeWeaponDefinition != definition.Weapon)
            {
                error = "Formal player Actor2DPresenter must be initialized from the selected definition.";
                return false;
            }

            if (!playerHud.TryPrepare(out error)
                || !cameraFeedback.TryPrepare(
                    nextSelection.ThreeCProfile,
                    targetCamera,
                    cameraRig,
                    out error)
                || !playerTickDriver.TryBindCameraFeedback(cameraFeedback, out error))
            {
                return false;
            }

            FpgPlayerBarrierPresentationController barrier = nextPlayerEntity.Barrier;
            if (barrier == null
                || !barrier.TrySetThreeCProfile(nextSelection.ThreeCProfile, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal player presentation requires a configured barrier view.";
                }

                return false;
            }
            if (!barrier.TryBindFormalSource(this, out error))
            {
                return false;
            }

            selection = nextSelection;
            playerEntity = nextPlayerEntity;
            actorPresenter = nextActorPresenter;
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            ResetEventCursors();
            prepared = true;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Called after the director has placed the player at the room entry.
        /// This is the only point that applies the scene-owned camera rig.
        /// </summary>
        public bool TryActivate(out string error)
        {
            if (active)
            {
                error = string.Empty;
                return true;
            }

            if (!prepared || playerEntity == null || actorPresenter == null)
            {
                error = "Formal player presentation must be prepared before activation.";
                return false;
            }

            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (runtime == null || runtime.IsDisposed
                || runtime.Player == null
                || runtime.Player.RuntimeId.IsValid == false)
            {
                error = "Formal player presentation requires a prepared combat runtime.";
                return false;
            }

            int vitalsReadCapacity = runtime.CombatPort.Vitals.EventCapacity;
            if (vitalsReadCapacity <= 0)
            {
                error = "Formal player presentation requires a positive Vitals event capacity.";
                return false;
            }
            vitalsBuffer = new FpgVitalsSnapshot[vitalsReadCapacity];

            playerEntity.gameObject.SetActive(true);
            if (playerEntity.VisualRoot != null)
            {
                playerEntity.VisualRoot.gameObject.SetActive(true);
            }
            if (!cameraFeedback.TryApplyFixedSceneRig(playerEntity.transform, out error))
            {
                return false;
            }

            Subscribe();
            observedTrace = runtime.CombatKernel.Trace;
            nextCombatEventOrdinal = observedTrace.TotalEventCount;
            active = true;
            if (playerTickDriver.TryRefreshPresentationSnapshot(out snapshot))
            {
                ApplyDurableActorState(snapshot);
                actorPresenter.SetPaused(snapshot.IsPaused);
                cameraFeedback.SetPaused(snapshot.IsPaused);
                playerHud.Refresh(snapshot);
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetPlayerPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot result)
        {
            result = snapshot;
            return active && result.IsValid;
        }

        public bool TryValidateAuthoring(out string error)
        {
            if (encounterDirector == null || playerTickDriver == null
                || playerHud == null || cameraFeedback == null
                || cameraRig == null || targetCamera == null)
            {
                error = "Formal player presentation requires director, driver, HUD, camera feedback, rig and camera references.";
                return false;
            }

            if (playerTickDriver.EncounterDirector != encounterDirector)
            {
                error = "Formal player presentation driver must target its encounter director.";
                return false;
            }

            if (!playerHud.TryValidate(out error)
                || !cameraFeedback.TryValidate(out error))
            {
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Clear()
        {
            Unsubscribe();
            if (playerEntity != null && playerEntity.Barrier != null)
            {
                playerEntity.Barrier.UnbindFormalSource();
            }

            actorPresenter?.SetPaused(false);
            actorPresenter?.ClearAndReturnToIdle();
            playerHud?.Clear();
            cameraFeedback?.Clear();
            selection = default(FpgPlayableCharacterSelection);
            playerEntity = null;
            actorPresenter = null;
            observedTrace = null;
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            VitalsGapCount = 0;
            SkillSequenceGapCount = 0;
            SkillCueGapCount = 0;
            SkillPresentationFaultCount = 0;
            prepared = false;
            active = false;
            ResetEventCursors();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            playerTickDriver.ActionCommitted += HandleActionCommitted;
            playerTickDriver.SkillSequenceAdvanced +=
                HandleSkillSequenceAdvanced;
            playerTickDriver.SkillCueCommitted += HandleSkillCueCommitted;
            encounterDirector.LifecycleEvent += HandleEncounterLifecycle;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (playerTickDriver != null)
            {
                playerTickDriver.ActionCommitted -= HandleActionCommitted;
                playerTickDriver.SkillSequenceAdvanced -=
                    HandleSkillSequenceAdvanced;
                playerTickDriver.SkillCueCommitted -= HandleSkillCueCommitted;
            }

            if (encounterDirector != null)
            {
                encounterDirector.LifecycleEvent -= HandleEncounterLifecycle;
            }

            subscribed = false;
        }

        private void HandleActionCommitted(FpgFormalPlayerActionEvent action)
        {
            if (!active)
            {
                return;
            }

            if (actionCount == actionQueue.Length)
            {
                actionHead = (actionHead + 1) % actionQueue.Length;
                actionCount--;
                actionGap = true;
            }

            int writeIndex = (actionHead + actionCount) % actionQueue.Length;
            actionQueue[writeIndex] = action;
            actionCount++;
        }

        private void HandleSkillSequenceAdvanced(
            FpgFormalPlayerSkillSequenceEvent sequenceEvent)
        {
            if (!active)
            {
                return;
            }

            if (skillSequenceCount == skillSequenceQueue.Length)
            {
                skillSequenceQueue[skillSequenceHead] =
                    default(FpgFormalPlayerSkillSequenceEvent);
                skillSequenceHead =
                    (skillSequenceHead + 1) % skillSequenceQueue.Length;
                skillSequenceCount--;
                skillSequenceGap = true;
                SkillSequenceGapCount++;
            }

            int writeIndex = (skillSequenceHead + skillSequenceCount)
                % skillSequenceQueue.Length;
            skillSequenceQueue[writeIndex] = sequenceEvent;
            skillSequenceCount++;
        }

        private void HandleSkillCueCommitted(
            FpgFormalPlayerSkillCueEvent cueEvent)
        {
            if (!active)
            {
                return;
            }

            if (skillCueCount == skillCueQueue.Length)
            {
                skillCueQueue[skillCueHead] =
                    default(FpgFormalPlayerSkillCueEvent);
                skillCueHead = (skillCueHead + 1) % skillCueQueue.Length;
                skillCueCount--;
                SkillCueGapCount++;
            }

            int writeIndex = (skillCueHead + skillCueCount)
                % skillCueQueue.Length;
            skillCueQueue[writeIndex] = cueEvent;
            skillCueCount++;
        }

        private void ConsumeSkillSequenceEvents()
        {
            if (skillSequenceGap)
            {
                if (hasActiveSkillSequence)
                {
                    actorPresenter.CancelSkillAnimation(
                        activeSkillSequence.ExecutionId);
                }

                hasActiveSkillSequence = false;
                activeSkillSequence =
                    default(FpgFormalPlayerSkillSequenceEvent);
                skillSequenceGap = false;
            }

            while (skillSequenceCount > 0)
            {
                FpgFormalPlayerSkillSequenceEvent sequenceEvent =
                    skillSequenceQueue[skillSequenceHead];
                skillSequenceQueue[skillSequenceHead] =
                    default(FpgFormalPlayerSkillSequenceEvent);
                skillSequenceHead =
                    (skillSequenceHead + 1) % skillSequenceQueue.Length;
                skillSequenceCount--;

                if (sequenceEvent.State == FpgSkillExecutionState.Canceled)
                {
                    actorPresenter.CancelSkillAnimation(
                        sequenceEvent.ExecutionId);
                    if (hasActiveSkillSequence
                        && activeSkillSequence.ExecutionId
                            == sequenceEvent.ExecutionId)
                    {
                        hasActiveSkillSequence = false;
                        activeSkillSequence =
                            default(FpgFormalPlayerSkillSequenceEvent);
                    }

                    continue;
                }

                if (hasActiveSkillSequence
                    && activeSkillSequence.ExecutionId
                        != sequenceEvent.ExecutionId)
                {
                    actorPresenter.CancelSkillAnimation(
                        activeSkillSequence.ExecutionId);
                }

                activeSkillSequence = sequenceEvent;
                hasActiveSkillSequence = true;
            }
        }

        private void EvaluateActiveSkillAnimation()
        {
            if (!hasActiveSkillSequence || actorPresenter == null
                || snapshot.IsPaused)
            {
                return;
            }

            FpgFormalPlayerSkillSequenceEvent sequenceEvent =
                activeSkillSequence;
            long relativeValue = sequenceEvent.RelativeTick;
            if (snapshot.Tick.IsValid && sequenceEvent.StartTick.IsValid)
            {
                relativeValue = snapshot.Tick.Value
                    - sequenceEvent.StartTick.Value;
            }

            int relativeTick = (int)Math.Max(
                0L,
                Math.Min(
                    sequenceEvent.CompiledSequence.DurationTicks,
                    relativeValue));
            double interpolation = sequenceEvent.IsTerminal
                ? 0d
                : FpgFormalPlayerSkillAnimationClock.ResolveInterpolation(
                    Time.timeAsDouble,
                    Time.fixedTimeAsDouble,
                    Time.fixedDeltaTime);
            if (!actorPresenter.TryEvaluateSkillAnimation(
                    sequenceEvent.ExecutionId,
                    sequenceEvent.AnimationName,
                    sequenceEvent.CompiledSequence,
                    relativeTick,
                    interpolation,
                    out _))
            {
                SkillPresentationFaultCount++;
                actorPresenter.CancelSkillAnimation(
                    sequenceEvent.ExecutionId);
                hasActiveSkillSequence = false;
                activeSkillSequence =
                    default(FpgFormalPlayerSkillSequenceEvent);
                return;
            }

            if (sequenceEvent.State == FpgSkillExecutionState.Completed)
            {
                actorPresenter.CompleteSkillAnimation(
                    sequenceEvent.ExecutionId);
                hasActiveSkillSequence = false;
                activeSkillSequence =
                    default(FpgFormalPlayerSkillSequenceEvent);
            }
        }

        private void ConsumeSkillCues()
        {
            if (snapshot.IsPaused)
            {
                return;
            }

            while (skillCueCount > 0)
            {
                FpgFormalPlayerSkillCueEvent cueEvent =
                    skillCueQueue[skillCueHead];
                skillCueQueue[skillCueHead] =
                    default(FpgFormalPlayerSkillCueEvent);
                skillCueHead = (skillCueHead + 1) % skillCueQueue.Length;
                skillCueCount--;
                PresentSkillCue(cueEvent);
            }
        }

        private void PresentSkillCue(
            in FpgFormalPlayerSkillCueEvent cueEvent)
        {
            if (cueEvent.CueName.StartsWith(
                    AnimationCuePrefix,
                    StringComparison.Ordinal))
            {
                string animationName = cueEvent.CueName.Substring(
                    AnimationCuePrefix.Length);
                if (!actorPresenter.TryPlaySkillCueAnimation(
                        animationName,
                        true,
                        out _))
                {
                    SkillPresentationFaultCount++;
                }
            }
            else if (skillVfxWorld != null)
            {
                if (!FpgPlayerSkillPresentationResolver.TryResolveCueSource(
                        playerEntity,
                        cueEvent.SocketName,
                        out Transform source)
                    || !skillVfxWorld.TryPresent(
                        cueEvent.CueName,
                        source,
                        out _))
                {
                    SkillPresentationFaultCount++;
                }
            }

            try
            {
                SkillCuePresented?.Invoke(cueEvent);
            }
            catch (Exception)
            {
                SkillPresentationFaultCount++;
            }
        }

        private bool ConsumeCommittedActions()
        {
            bool primaryPresented = false;
            while (actionCount > 0)
            {
                FpgFormalPlayerActionEvent action = actionQueue[actionHead];
                actionQueue[actionHead] = default(FpgFormalPlayerActionEvent);
                actionHead = (actionHead + 1) % actionQueue.Length;
                actionCount--;

                switch (action.Type)
                {
                    case FpgFormalPlayerActionType.PrimaryReleaseCommitted:
                        actorPresenter.NotifyPrimarySkillCommitted();
                        primaryPresented = true;
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeStarted:
                        actorPresenter.NotifySecondaryChargeStarted();
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeCanceled:
                        actorPresenter.NotifySecondaryChargeCanceled();
                        break;
                    case FpgFormalPlayerActionType.SecondaryReleaseCommitted:
                        actorPresenter.NotifySecondaryReleaseCommitted();
                        break;
                    case FpgFormalPlayerActionType.ReloadStarted:
                        actorPresenter.NotifyReloadStarted();
                        break;
                    case FpgFormalPlayerActionType.ReloadCompleted:
                        actorPresenter.NotifyReloadCompleted();
                        break;
                }

                cameraFeedback.PresentCommittedAction(action);
            }

            return primaryPresented;
        }

        private PresentationFrameFlags ConsumeCombatTrace()
        {
            PresentationFrameFlags flags = default(PresentationFrameFlags);
            FpgFormalCombatRuntimeBundle runtime = encounterDirector.CombatRuntime;
            CombatTrace trace = runtime == null || runtime.IsDisposed
                ? null
                : runtime.CombatKernel.Trace;
            if (!ReferenceEquals(trace, observedTrace))
            {
                observedTrace = trace;
                nextCombatEventOrdinal = trace == null ? 0L : trace.TotalEventCount;
                return flags;
            }

            if (trace == null)
            {
                return flags;
            }

            long total = trace.TotalEventCount;
            long oldest = total - trace.Count;
            if (nextCombatEventOrdinal < oldest || nextCombatEventOrdinal > total)
            {
                nextCombatEventOrdinal = total;
                flags.HasGap = true;
                return flags;
            }

            for (long ordinal = nextCombatEventOrdinal; ordinal < total; ordinal++)
            {
                CombatEvent combatEvent = trace.GetOldest((int)(ordinal - oldest));
                if (combatEvent.TargetId != snapshot.PlayerRuntimeId)
                {
                    continue;
                }

                switch (combatEvent.EventType)
                {
                    case CombatEventType.DamageApplied:
                        if (combatEvent.DamageChannel == DamageChannel.Life)
                        {
                            actorPresenter.PlayHit();
                            flags.PlayerHit = true;
                        }
                        break;
                    case CombatEventType.BarrierBroken:
                        actorPresenter.PlayGroggy();
                        flags.BarrierBroken = true;
                        break;
                    case CombatEventType.Death:
                        actorPresenter.PlayDefeat();
                        flags.Defeat = true;
                        break;
                }
            }

            nextCombatEventOrdinal = total;
            return flags;
        }

        private void ApplySnapshotTransitions(
            in FpgFormalPlayerPresentationSnapshot previous,
            in FpgFormalPlayerPresentationSnapshot current,
            bool primaryPresented,
            in PresentationFrameFlags traceFlags)
        {
            if (!current.IsValid)
            {
                return;
            }

            if (current.PresentationState == FpgFormalPlayerPresentationState.Victory
                && previous.PresentationState
                    != FpgFormalPlayerPresentationState.Victory)
            {
                actorPresenter.PlayVictory();
                return;
            }

            if (current.PresentationState == FpgFormalPlayerPresentationState.Defeat
                && previous.PresentationState
                    != FpgFormalPlayerPresentationState.Defeat
                && !traceFlags.Defeat)
            {
                actorPresenter.PlayDefeat();
                return;
            }

            if (previous.IsValid)
            {
                if (current.Life < previous.Life && !traceFlags.PlayerHit)
                {
                    actorPresenter.PlayHit();
                }

                if (previous.Barrier > 0 && current.Barrier <= 0
                    && !traceFlags.BarrierBroken)
                {
                    actorPresenter.PlayGroggy();
                }

                if (!primaryPresented
                    && (actionGap || traceFlags.HasGap)
                    && previous.WeaponState != WeaponState.PrimaryRecovery
                    && current.WeaponState == WeaponState.PrimaryRecovery)
                {
                    actorPresenter.NotifyPrimarySkillCommitted();
                }
            }

            ApplyDurableActorState(current);
            actionGap = false;
        }

        private void ApplyDurableActorState(
            in FpgFormalPlayerPresentationSnapshot value)
        {
            if (!value.IsValid
                || value.PresentationState == FpgFormalPlayerPresentationState.Victory
                || value.PresentationState == FpgFormalPlayerPresentationState.Defeat)
            {
                return;
            }

            if (value.WeaponState == WeaponState.Reloading)
            {
                if (!actorPresenter.IsReloading)
                {
                    actorPresenter.NotifyReloadStarted();
                }
                return;
            }

            if (value.WeaponState == WeaponState.AltCharging)
            {
                if (!actorPresenter.IsChargingSecondary)
                {
                    actorPresenter.NotifySecondaryChargeStarted();
                }
                return;
            }

            if (actorPresenter.IsReloading)
            {
                actorPresenter.NotifyReloadCompleted();
                actorPresenter.ReturnToIdle();
            }

            if (actorPresenter.IsChargingSecondary)
            {
                actorPresenter.NotifySecondaryChargeCanceled();
                actorPresenter.ReturnToIdle();
            }
        }

        private FpgFormalPlayerPresentationSnapshot ApplyVitalsChanges(
            in FpgFormalPlayerPresentationSnapshot fallback)
        {
            try
            {
                return ApplyVitalsChangesCore(fallback);
            }
            catch (Exception)
            {
                VitalsGapCount++;
                observedVitalsRuntime = null;
                vitalsCursor = 0L;
                if (vitalsBuffer != null && vitalsBuffer.Length > 0)
                {
                    Array.Clear(vitalsBuffer, 0, vitalsBuffer.Length);
                }
                return fallback;
            }
        }

        private FpgFormalPlayerPresentationSnapshot ApplyVitalsChangesCore(
            in FpgFormalPlayerPresentationSnapshot fallback)
        {
            FpgFormalCombatRuntimeBundle runtime = encounterDirector == null
                ? null
                : encounterDirector.CombatRuntime;
            if (!ReferenceEquals(runtime, observedVitalsRuntime))
            {
                observedVitalsRuntime = runtime;
                vitalsCursor = 0L;
            }

            if (runtime == null || runtime.IsDisposed
                || !fallback.PlayerRuntimeId.IsValid)
            {
                return fallback;
            }

            IFpgVitalsView view = runtime.CombatPort.Vitals;
            FpgVitalsSnapshot latest = default(FpgVitalsSnapshot);
            bool hasLatest = false;
            if (view.LastSequence < vitalsCursor)
            {
                VitalsGapCount++;
                vitalsCursor = 0L;
            }

            if (view.EventCapacity > vitalsBuffer.Length)
            {
                if (vitalsCursor != view.LastSequence)
                {
                    VitalsGapCount++;
                }
                vitalsCursor = view.LastSequence;
                hasLatest = view.TryGetLatest(
                    fallback.PlayerRuntimeId,
                    out latest);
            }
            else
            {
                try
                {
                    int count = view.CopyChangesAfter(
                        vitalsCursor,
                        vitalsBuffer,
                        out bool hasGap);
                    if (hasGap)
                    {
                        VitalsGapCount++;
                        hasLatest = view.TryGetLatest(
                            fallback.PlayerRuntimeId,
                            out latest);
                    }
                    else
                    {
                        for (int index = 0; index < count; index++)
                        {
                            FpgVitalsSnapshot candidate = vitalsBuffer[index];
                            vitalsCursor = Math.Max(
                                vitalsCursor,
                                candidate.Sequence);
                            if (candidate.RuntimeId == fallback.PlayerRuntimeId)
                            {
                                latest = candidate;
                                hasLatest = true;
                            }
                            vitalsBuffer[index] = default(FpgVitalsSnapshot);
                        }
                    }

                    if (hasGap)
                    {
                        vitalsCursor = view.LastSequence;
                    }
                }
                catch (Exception)
                {
                    VitalsGapCount++;
                    vitalsCursor = view.LastSequence;
                    hasLatest = view.TryGetLatest(
                        fallback.PlayerRuntimeId,
                        out latest);
                }
            }

            if (!hasLatest)
            {
                return fallback;
            }

            return new FpgFormalPlayerPresentationSnapshot(
                fallback.Tick,
                fallback.PlayerRuntimeId,
                fallback.EncounterPhase,
                fallback.IsPaused,
                latest.Life,
                latest.MaxLife,
                latest.Barrier,
                latest.MaxBarrier,
                fallback.Ammo,
                fallback.MagazineCapacity,
                fallback.ExposureState,
                fallback.WeaponState);
        }

        private void ResetEventCursors()
        {
            for (int index = 0; index < actionQueue.Length; index++)
            {
                actionQueue[index] = default(FpgFormalPlayerActionEvent);
            }
            actionHead = 0;
            actionCount = 0;
            actionGap = false;
            if (hasActiveSkillSequence && actorPresenter != null)
            {
                actorPresenter.CancelSkillAnimation(
                    activeSkillSequence.ExecutionId);
            }
            Array.Clear(
                skillSequenceQueue,
                0,
                skillSequenceQueue.Length);
            Array.Clear(skillCueQueue, 0, skillCueQueue.Length);
            skillSequenceHead = 0;
            skillSequenceCount = 0;
            skillCueHead = 0;
            skillCueCount = 0;
            skillSequenceGap = false;
            hasActiveSkillSequence = false;
            activeSkillSequence =
                default(FpgFormalPlayerSkillSequenceEvent);
            nextCombatEventOrdinal = 0L;
            observedVitalsRuntime = null;
            vitalsCursor = 0L;
            if (vitalsBuffer.Length > 0)
            {
                Array.Clear(vitalsBuffer, 0, vitalsBuffer.Length);
            }
        }

        private void OnEnable()
        {
            if (!active)
            {
                return;
            }

            Subscribe();
            actorPresenter?.ClearAndReturnToIdle();
            cameraFeedback?.ResetRuntimeFeedback();
            playerHud?.Clear();
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            ResetEventCursors();
            observedTrace = encounterDirector == null
                || encounterDirector.CombatRuntime == null
                || encounterDirector.CombatRuntime.IsDisposed
                ? null
                : encounterDirector.CombatRuntime.CombatKernel.Trace;
            nextCombatEventOrdinal =
                observedTrace == null ? 0L : observedTrace.TotalEventCount;
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private struct PresentationFrameFlags
        {
            public bool PlayerHit;
            public bool BarrierBroken;
            public bool Defeat;
            public bool HasGap;
        }

        private void HandleEncounterLifecycle(
            FpgEncounterLifecycleEvent lifecycle)
        {
            if (lifecycle.Type != FpgEncounterLifecycleEventType.Restarted)
            {
                return;
            }

            actorPresenter?.SetPaused(false);
            actorPresenter?.ClearAndReturnToIdle();
            cameraFeedback?.ResetRuntimeFeedback();
            playerHud?.Clear();
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            observedTrace = encounterDirector == null
                || encounterDirector.CombatRuntime == null
                || encounterDirector.CombatRuntime.IsDisposed
                ? null
                : encounterDirector.CombatRuntime.CombatKernel.Trace;
            ResetEventCursors();
        }
}
}
