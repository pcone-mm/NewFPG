using System;
using FPG.Demo.Combat;
using FPG.Demo.Run;
using FPG.Demo.Player;
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
        [Header("Formal runtime")]
        [SerializeField] private FpgRoomEncounterDirector encounterDirector;
        [SerializeField] private FpgFormalPlayerTickDriver playerTickDriver;

        [Header("Player presentation")]
        [SerializeField] private FpgFormalPlayerHudPresenter playerHud;
        [SerializeField] private FpgFormalPlayerCameraFeedback cameraFeedback;

        [Header("Scene-owned camera")]
        [SerializeField] private Transform cameraRig;
        [SerializeField] private Camera targetCamera;

        private readonly FpgFormalPlayerActionEvent[] actionQueue =
            new FpgFormalPlayerActionEvent[ActionQueueCapacity];
        private FpgVitalsSnapshot[] vitalsBuffer =
            Array.Empty<FpgVitalsSnapshot>();

        private FpgPlayableCharacterSelection selection;
        private D0PlayerEntityView playerEntity;
        private Actor2DPresenter actorPresenter;
        private CombatTrace observedTrace;
        private FpgFormalCombatRuntimeBundle observedVitalsRuntime;
        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private long nextCombatEventOrdinal;
        private long vitalsCursor;
        private int actionHead;
        private int actionCount;
        private bool actionGap;
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
        public D0PlayerEntityView PlayerEntity => playerEntity;
        public FpgFormalPlayerPresentationSnapshot Snapshot => snapshot;
        public bool IsPrepared => prepared;
        public bool IsActive => active;
        public int VitalsGapCount { get; private set; }
        public int VitalsReadCapacity => vitalsBuffer.Length;

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
            bool primaryPresented = ConsumeCommittedActions();
            PresentationFrameFlags traceFlags = ConsumeCombatTrace();
            ApplySnapshotTransitions(
                previous,
                snapshot,
                primaryPresented,
                traceFlags);
            actorPresenter?.SetPaused(snapshot.IsPaused);
            cameraFeedback?.SetPaused(snapshot.IsPaused);
            playerHud?.Refresh(snapshot);
        }

        public bool TryPrepare(
            FpgPlayableCharacterSelection nextSelection,
            D0PlayerEntityView nextPlayerEntity,
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

            D0PlayerBarrierPresentationController barrier = nextPlayerEntity.Barrier;
            if (barrier == null
                || !barrier.TrySetThreeCProfile(nextSelection.ThreeCProfile, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal player presentation requires a configured barrier view.";
                }

                return false;
            }

            barrier.UnbindSceneServices();
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

            playerEntity.Controller?.CaptureInitialSpawn();
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
                        actorPresenter.PlayPrimaryAttack();
                        primaryPresented = true;
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeStarted:
                        actorPresenter.BeginSecondaryCharge();
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeCanceled:
                        actorPresenter.CancelSecondaryCharge();
                        break;
                    case FpgFormalPlayerActionType.SecondaryReleaseCommitted:
                        actorPresenter.PlaySecondaryRelease();
                        break;
                    case FpgFormalPlayerActionType.ReloadStarted:
                        actorPresenter.BeginReload();
                        break;
                    case FpgFormalPlayerActionType.ReloadCompleted:
                        actorPresenter.CompleteReload();
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
                    actorPresenter.PlayPrimaryAttack();
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
                    actorPresenter.BeginReload();
                }
                return;
            }

            if (value.WeaponState == WeaponState.AltCharging)
            {
                if (!actorPresenter.IsChargingSecondary)
                {
                    actorPresenter.BeginSecondaryCharge();
                }
                return;
            }

            if (actorPresenter.IsReloading)
            {
                actorPresenter.CompleteReload();
            }

            if (actorPresenter.IsChargingSecondary)
            {
                actorPresenter.CancelSecondaryCharge();
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
