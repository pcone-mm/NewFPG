using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using Spine;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Durable presentation state for the formal player. The struct contains
    /// only read-model values; no presentation component can mutate combat
    /// state through it.
    /// </summary>
    public readonly struct FpgFormalPlayerPresentationSnapshot
    {
        public FpgFormalPlayerPresentationSnapshot(
            TickIndex tick,
            RuntimeId playerRuntimeId,
            FpgEncounterPhase encounterPhase,
            bool paused,
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            int ammo,
            int magazineCapacity,
            PlayerExposureState exposureState,
            WeaponState weaponState)
            : this(
                tick,
                playerRuntimeId,
                encounterPhase,
                paused,
                life,
                maxLife,
                barrier,
                maxBarrier,
                ammo,
                magazineCapacity,
                exposureState,
                weaponState,
                false,
                0f,
                TickIndex.Invalid)
        {
        }

        public FpgFormalPlayerPresentationSnapshot(
            TickIndex tick,
            RuntimeId playerRuntimeId,
            FpgEncounterPhase encounterPhase,
            bool paused,
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            int ammo,
            int magazineCapacity,
            PlayerExposureState exposureState,
            WeaponState weaponState,
            bool isSecondaryCharging,
            float secondaryChargeProgress,
            TickIndex secondaryChargeStartedTick)
            : this(
                tick,
                playerRuntimeId,
                encounterPhase,
                paused,
                life,
                maxLife,
                barrier,
                maxBarrier,
                ammo,
                magazineCapacity,
                exposureState,
                weaponState,
                isSecondaryCharging,
                secondaryChargeProgress,
                secondaryChargeStartedTick,
                false,
                TickIndex.Invalid,
                false)
        {
        }

        public FpgFormalPlayerPresentationSnapshot(
            TickIndex tick,
            RuntimeId playerRuntimeId,
            FpgEncounterPhase encounterPhase,
            bool paused,
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            int ammo,
            int magazineCapacity,
            PlayerExposureState exposureState,
            WeaponState weaponState,
            bool isSecondaryCharging,
            float secondaryChargeProgress,
            TickIndex secondaryChargeStartedTick,
            bool isCoverPeekRequested,
            TickIndex coverPeekStartedTick,
            bool isBarrierLocked)
        {
            Tick = tick;
            PlayerRuntimeId = playerRuntimeId;
            EncounterPhase = encounterPhase;
            IsPaused = paused;
            Life = life;
            MaxLife = maxLife;
            Barrier = barrier;
            MaxBarrier = maxBarrier;
            Ammo = ammo;
            MagazineCapacity = magazineCapacity;
            ExposureState = exposureState;
            WeaponState = weaponState;
            IsSecondaryCharging = isSecondaryCharging;
            SecondaryChargeProgress = isSecondaryCharging
                && !float.IsNaN(secondaryChargeProgress)
                && !float.IsInfinity(secondaryChargeProgress)
                ? Mathf.Clamp01(secondaryChargeProgress)
                : 0f;
            SecondaryChargeStartedTick = isSecondaryCharging
                ? secondaryChargeStartedTick
                : TickIndex.Invalid;
            IsCoverPeekRequested = isCoverPeekRequested;
            CoverPeekStartedTick = isCoverPeekRequested
                ? coverPeekStartedTick
                : TickIndex.Invalid;
            IsBarrierLocked = isBarrierLocked;
        }

        public static FpgFormalPlayerPresentationSnapshot Unavailable =>
            new FpgFormalPlayerPresentationSnapshot(
                TickIndex.Invalid,
                RuntimeId.Invalid,
                FpgEncounterPhase.None,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                PlayerExposureState.Withdrawn,
                WeaponState.Disabled);

        public TickIndex Tick { get; }
        public RuntimeId PlayerRuntimeId { get; }
        public FpgEncounterPhase EncounterPhase { get; }
        public bool IsPaused { get; }
        public int Life { get; }
        public int MaxLife { get; }
        public int Barrier { get; }
        public int MaxBarrier { get; }
        public int Ammo { get; }
        public int MagazineCapacity { get; }
        public PlayerExposureState ExposureState { get; }
        public WeaponState WeaponState { get; }
        public bool IsSecondaryCharging { get; }
        public float SecondaryChargeProgress { get; }
        public TickIndex SecondaryChargeStartedTick { get; }
        public bool IsCoverPeekRequested { get; }
        public TickIndex CoverPeekStartedTick { get; }
        public bool IsBarrierLocked { get; }

        public bool IsValid => PlayerRuntimeId.IsValid
            && MaxLife > 0
            && MaxBarrier > 0
            && MagazineCapacity > 0;

        public bool IsDead => IsValid && Life <= 0;

        public bool IsCombatActive => IsValid
            && !IsDead
            && !IsPaused
            && EncounterPhase != FpgEncounterPhase.None
            && EncounterPhase != FpgEncounterPhase.Preparing
            && EncounterPhase != FpgEncounterPhase.Cleared
            && EncounterPhase != FpgEncounterPhase.Defeated
            && EncounterPhase != FpgEncounterPhase.Failed
            && EncounterPhase != FpgEncounterPhase.Faulted
            && EncounterPhase != FpgEncounterPhase.Disposed;

        public FpgFormalPlayerPresentationState PresentationState
        {
            get
            {
                if (!IsValid)
                {
                    return FpgFormalPlayerPresentationState.Unavailable;
                }

                if (IsDead || EncounterPhase == FpgEncounterPhase.Defeated)
                {
                    return FpgFormalPlayerPresentationState.Defeat;
                }

                if (EncounterPhase == FpgEncounterPhase.Cleared)
                {
                    return FpgFormalPlayerPresentationState.Victory;
                }

                if (EncounterPhase == FpgEncounterPhase.Failed
                    || EncounterPhase == FpgEncounterPhase.Faulted
                    || EncounterPhase == FpgEncounterPhase.Disposed)
                {
                    return FpgFormalPlayerPresentationState.Faulted;
                }

                if (IsPaused || EncounterPhase == FpgEncounterPhase.Paused)
                {
                    return FpgFormalPlayerPresentationState.Paused;
                }

                if (EncounterPhase == FpgEncounterPhase.None
                    || EncounterPhase == FpgEncounterPhase.Preparing)
                {
                    return FpgFormalPlayerPresentationState.Preparing;
                }

                return FpgFormalPlayerPresentationState.Active;
            }
        }
    }

    public enum FpgFormalPlayerPresentationState
    {
        Unavailable = 0,
        Preparing,
        Active,
        Paused,
        Victory,
        Defeat,
        Faulted
    }

    public enum FpgFormalPlayerActionType
    {
        PrimaryReleaseCommitted = 0,
        SecondaryChargeStarted,
        SecondaryChargeCanceled,
        SecondaryReleaseCommitted,
        ReloadStarted,
        ReloadCompleted
    }

    /// <summary>
    /// A player action is raised only after WeaponRuntime.ProcessFrame has
    /// accepted the containing input frame. Presentation consumers may safely
    /// queue this event without feeding anything back into the simulation.
    /// </summary>
    public readonly struct FpgFormalPlayerActionEvent
    {
        public FpgFormalPlayerActionEvent(
            long sequence,
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter)
            : this(
                sequence,
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter,
                SkillExecutionId.Invalid,
                0)
        {
        }

        public FpgFormalPlayerActionEvent(
            long sequence,
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            if (gameplayEventId < 0
                || skillExecutionId.IsValid != (gameplayEventId > 0))
            {
                throw new ArgumentException(
                    "Formal player action correlation requires both a valid skill execution and positive gameplay event ID.",
                    nameof(gameplayEventId));
            }

            Sequence = sequence;
            Tick = tick;
            Type = type;
            ReleaseKind = releaseKind;
            AttackId = attackId;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            AmmoBefore = ammoBefore;
            AmmoAfter = ammoAfter;
            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
        }

        public long Sequence { get; }
        public TickIndex Tick { get; }
        public FpgFormalPlayerActionType Type { get; }
        public WeaponReleaseKind ReleaseKind { get; }
        public AttackId AttackId { get; }
        public WeaponState StateBefore { get; }
        public WeaponState StateAfter { get; }
        public int AmmoBefore { get; }
        public int AmmoAfter { get; }
        public SkillExecutionId SkillExecutionId { get; }
        public int GameplayEventId { get; }
        public bool HasSkillCorrelation => SkillExecutionId.IsValid;
    }

    public readonly struct FpgFormalPlayerSkillSequenceEvent
    {
        internal FpgFormalPlayerSkillSequenceEvent(
            long sequence,
            in FpgPlayerSkillSequenceFrame frame,
            string animationName)
        {
            Sequence = sequence;
            Slot = frame.Slot;
            CompiledSequence = frame.Sequence;
            ExecutionId = frame.ExecutionId;
            StartTick = frame.StartTick;
            Tick = frame.Tick;
            RelativeTick = frame.RelativeTick;
            State = frame.State;
            ResolvedAnimationId = frame.ResolvedAnimationId;
            AnimationName = animationName ?? string.Empty;
        }

        public long Sequence { get; }
        public FpgPlayerSkillSlot Slot { get; }
        public FpgCompiledSkillSequence CompiledSequence { get; }
        public FpgSkillSequenceKind SequenceKind => CompiledSequence.Kind;
        public SkillExecutionId ExecutionId { get; }
        public TickIndex StartTick { get; }
        public TickIndex Tick { get; }
        public int RelativeTick { get; }
        public FpgSkillExecutionState State { get; }
        public int ResolvedAnimationId { get; }
        public string AnimationName { get; }
        public bool IsTerminal => State == FpgSkillExecutionState.Completed
            || State == FpgSkillExecutionState.Canceled;
    }

    public readonly struct FpgFormalPlayerActivePresentationEvent
    {
        internal FpgFormalPlayerActivePresentationEvent(
            long sequence,
            in FpgPlayerSkillExecutionEvent skillEvent)
        {
            if (sequence <= 0L
                || skillEvent.Outcome != FpgSkillEventOutcome.Triggered
                || skillEvent.Event.Kind
                    != FpgSkillEventKind.ActivePresentation)
            {
                throw new ArgumentException(
                    "Active skill presentation event is invalid.");
            }

            Sequence = sequence;
            Slot = skillEvent.Slot;
            ExecutionId = skillEvent.RuntimeEvent.ExecutionId;
            SequenceKind = skillEvent.RuntimeEvent.SequenceKind;
            EventId = skillEvent.Event.EventId;
            Kind = skillEvent.Event.ActivePresentationKind;
            Handle = skillEvent.Event.PresentationHandle;
            SocketId = skillEvent.Event.SocketId;
            BoundGameplayEventId =
                skillEvent.Event.BoundGameplayEventId;
            ScheduledTick = skillEvent.RuntimeEvent.ScheduledTick;
            Tick = skillEvent.RuntimeEvent.Tick;
        }

        public long Sequence { get; }
        public FpgPlayerSkillSlot Slot { get; }
        public SkillExecutionId ExecutionId { get; }
        public FpgSkillSequenceKind SequenceKind { get; }
        public int EventId { get; }
        public FpgActivePresentationKind Kind { get; }
        public FpgPresentationHandle Handle { get; }
        public int SocketId { get; }
        public int BoundGameplayEventId { get; }
        public TickIndex ScheduledTick { get; }
        public TickIndex Tick { get; }
        public bool RequiresGameplayCommit => BoundGameplayEventId > 0;
    }

    public static class FpgPlayerSkillPresentationResolver
    {
        public static bool TryResolveAnimationName(
            FpgPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            int animationId,
            out string animationName)
        {
            animationName = string.Empty;
            if (definition == null || animationId <= 0)
            {
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < definition.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    definition.Sequences[sequenceIndex];
                if (sequence == null || sequence.Kind != sequenceKind)
                {
                    continue;
                }

                if (FpgSkillStableId.CompileAnimation(sequence.MainAnimation)
                    == animationId)
                {
                    animationName = sequence.MainAnimation;
                    return true;
                }

                for (int variantIndex = 0;
                    variantIndex < sequence.AlternateAnimations.Count;
                    variantIndex++)
                {
                    string variant = sequence.AlternateAnimations[variantIndex];
                    if (FpgSkillStableId.CompileAnimation(variant)
                        == animationId)
                    {
                        animationName = variant;
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        public static bool TryValidatePrefabBindings(
            FpgPlayerEntityView entityPrefab,
            FpgPlayerSkillDefinition primarySkill,
            FpgPlayerSkillDefinition secondarySkill,
            FpgPlayerSkillDefinition reloadSkill,
            out string error)
        {
            if (entityPrefab == null)
            {
                error =
                    "Formal player skill preflight requires an entity prefab.";
                return false;
            }

            if (entityPrefab.SkeletonAnimation == null
                || entityPrefab.SkeletonAnimation.SkeletonDataAsset == null)
            {
                error =
                    "Formal player skill preflight requires prefab Spine skeleton data.";
                return false;
            }

            SkeletonData skeletonData;
            try
            {
                skeletonData = entityPrefab.SkeletonAnimation
                    .SkeletonDataAsset.GetSkeletonData(true);
            }
            catch (Exception exception)
            {
                error =
                    "Formal player prefab Spine skeleton data could not load: "
                    + exception.Message;
                return false;
            }

            if (skeletonData == null)
            {
                error =
                    "Formal player prefab Spine skeleton data could not load.";
                return false;
            }

            D0ActorSocketRegistry socketRegistry = entityPrefab.SocketRegistry;
            if (socketRegistry == null)
            {
                error =
                    "Formal player skill preflight requires a prefab socket registry.";
                return false;
            }

            return TryValidateSkillBindings(
                    primarySkill,
                    "primary",
                    skeletonData,
                    socketRegistry,
                    out error)
                && TryValidateSkillBindings(
                    secondarySkill,
                    "secondary",
                    skeletonData,
                    socketRegistry,
                    out error)
                && TryValidateSkillBindings(
                    reloadSkill,
                    "reload",
                    skeletonData,
                    socketRegistry,
                    out error);
        }

        public static bool TryResolvePresentationSource(
            FpgPlayerEntityView entity,
            string socketName,
            out Transform source)
        {
            source = null;
            if (entity == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(socketName))
            {
                D0ActorSocketRegistry registry = entity.SocketRegistry;
                return registry != null
                    && registry.TryResolve(socketName, out source);
            }

            source = entity.AimAnchor != null
                ? entity.AimAnchor
                : entity.transform;
            return source != null;
        }

        private static bool TryValidateSkillBindings(
            FpgPlayerSkillDefinition definition,
            string slotName,
            SkeletonData skeletonData,
            D0ActorSocketRegistry socketRegistry,
            out string error)
        {
            if (definition == null)
            {
                error =
                    "Formal player skill preflight requires the "
                    + slotName + " skill.";
                return false;
            }

            for (int sequenceIndex = 0;
                sequenceIndex < definition.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    definition.Sequences[sequenceIndex];
                if (sequence == null)
                {
                    error =
                        "Formal player skill '" + definition.SkillId
                        + "' contains a missing sequence.";
                    return false;
                }

                if (!TryValidateAnimation(
                        definition,
                        sequence,
                        sequence.MainAnimation,
                        skeletonData,
                        out error))
                {
                    return false;
                }

                for (int animationIndex = 0;
                    animationIndex < sequence.AlternateAnimations.Count;
                    animationIndex++)
                {
                    if (!TryValidateAnimation(
                            definition,
                            sequence,
                            sequence.AlternateAnimations[animationIndex],
                            skeletonData,
                            out error))
                    {
                        return false;
                    }
                }

                if (!TryValidateActionSockets(
                        definition,
                        sequence.AttackEvents,
                        socketRegistry,
                        out error)
                    || !TryValidateActionSockets(
                        definition,
                        sequence.ProjectileEvents,
                        socketRegistry,
                        out error)
                    || !TryValidateActionSockets(
                        definition,
                        sequence.ReloadEvents,
                        socketRegistry,
                        out error)
                    || !TryValidateActionSockets(
                        definition,
                        sequence.SummonEvents,
                        socketRegistry,
                        out error))
                {
                    return false;
                }

                for (int warningIndex = 0;
                    warningIndex < sequence.Warnings.Count;
                    warningIndex++)
                {
                    FpgSkillWarningDefinition warning =
                        sequence.Warnings[warningIndex];
                    if (warning == null)
                    {
                        error =
                            "Formal player skill '" + definition.SkillId
                            + "' contains a missing warning.";
                        return false;
                    }

                    if (!TryValidateSocket(
                            definition,
                            warning.EventId,
                            warning.SocketId,
                            socketRegistry,
                            out error))
                    {
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateActionSockets<TAction>(
            FpgPlayerSkillDefinition definition,
            IReadOnlyList<TAction> actions,
            D0ActorSocketRegistry socketRegistry,
            out string error)
            where TAction : FpgSkillGameplayActionDefinition
        {
            for (int actionIndex = 0;
                actionIndex < actions.Count;
                actionIndex++)
            {
                TAction action = actions[actionIndex];
                if (action == null)
                {
                    error =
                        "Formal player skill '" + definition.SkillId
                        + "' contains a missing gameplay action.";
                    return false;
                }

                if (!TryValidateSocket(
                        definition,
                        action.EventId,
                        action.SocketId,
                        socketRegistry,
                        out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateAnimation(
            FpgPlayerSkillDefinition definition,
            FpgSkillSequenceDefinition sequence,
            string animationName,
            SkeletonData skeletonData,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(animationName)
                || skeletonData.FindAnimation(animationName) == null)
            {
                error =
                    "Formal player skill '" + definition.SkillId
                    + "' sequence " + sequence.Kind
                    + " cannot resolve prefab Spine animation '"
                    + animationName + "'.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateSocket(
            FpgPlayerSkillDefinition definition,
            string eventName,
            string socketName,
            D0ActorSocketRegistry socketRegistry,
            out string error)
        {
            if (string.IsNullOrEmpty(socketName)
                || socketRegistry.TryResolve(socketName, out _))
            {
                error = string.Empty;
                return true;
            }

            error =
                "Formal player skill '" + definition.SkillId
                + "' event '" + eventName
                + "' cannot resolve prefab socket '" + socketName + "'.";
            return false;
        }
    }

    public static class FpgPlayerSkillGameplayEventResolver
    {
        public static bool TryResolveSocketName(
            FpgPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            in FpgCompiledSkillEvent compiledEvent,
            out string socketName)
        {
            socketName = string.Empty;
            if (definition == null
                || compiledEvent.Kind != FpgSkillEventKind.GameplayAction)
            {
                return false;
            }

            return TryResolveTypedSocketName(
                definition,
                sequenceKind,
                compiledEvent,
                out socketName);
        }

        private static bool TryResolveTypedSocketName(
            FpgPlayerSkillDefinition definition,
            FpgSkillSequenceKind sequenceKind,
            in FpgCompiledSkillEvent compiledEvent,
            out string socketName)
        {
            socketName = string.Empty;
            int actionOffset = 0;
            for (int sequenceIndex = 0;
                sequenceIndex < definition.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    definition.Sequences[sequenceIndex];
                if (sequence == null)
                {
                    return false;
                }

                int actionCount = GetActionCount(
                    sequence,
                    compiledEvent.ActionKind);
                if (sequence.Kind == sequenceKind)
                {
                    int localIndex = compiledEvent.ActionIndex - actionOffset;
                    if (!TryGetAction(
                            sequence,
                            compiledEvent.ActionKind,
                            localIndex,
                            out FpgSkillGameplayActionDefinition action)
                        || !Matches(action, compiledEvent))
                    {
                        return false;
                    }

                    socketName = action.SocketId;
                    return true;
                }

                actionOffset = checked(actionOffset + actionCount);
            }

            return false;
        }

        private static int GetActionCount(
            FpgSkillSequenceDefinition sequence,
            FpgSkillActionKind actionKind)
        {
            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                    return sequence.AttackEvents.Count;
                case FpgSkillActionKind.LaunchProjectile:
                    return sequence.ProjectileEvents.Count;
                case FpgSkillActionKind.CommitReload:
                    return sequence.ReloadEvents.Count;
                case FpgSkillActionKind.SummonActors:
                    return sequence.SummonEvents.Count;
                default:
                    return 0;
            }
        }

        private static bool TryGetAction(
            FpgSkillSequenceDefinition sequence,
            FpgSkillActionKind actionKind,
            int index,
            out FpgSkillGameplayActionDefinition action)
        {
            action = null;
            if (index < 0)
            {
                return false;
            }

            switch (actionKind)
            {
                case FpgSkillActionKind.Attack:
                    if (index < sequence.AttackEvents.Count)
                    {
                        action = sequence.AttackEvents[index];
                    }

                    break;
                case FpgSkillActionKind.LaunchProjectile:
                    if (index < sequence.ProjectileEvents.Count)
                    {
                        action = sequence.ProjectileEvents[index];
                    }

                    break;
                case FpgSkillActionKind.CommitReload:
                    if (index < sequence.ReloadEvents.Count)
                    {
                        action = sequence.ReloadEvents[index];
                    }

                    break;
                case FpgSkillActionKind.SummonActors:
                    if (index < sequence.SummonEvents.Count)
                    {
                        action = sequence.SummonEvents[index];
                    }

                    break;
            }

            return action != null;
        }

        private static bool Matches(
            FpgSkillGameplayActionDefinition action,
            in FpgCompiledSkillEvent compiledEvent)
        {
            return action.Tick == compiledEvent.Tick
                && action.AuthoredOrdinal == compiledEvent.SortOrder
                && FpgSkillStableId.CompileEvent(action.EventId)
                    == compiledEvent.EventId
                && FpgSkillStableId.CompileOptionalSocket(action.SocketId)
                    == compiledEvent.SocketId
                && action.TargetSource == compiledEvent.TargetSource
                && action.OffsetXMillimeters
                    == compiledEvent.Offset.XMillimeters
                && action.OffsetYMillimeters
                    == compiledEvent.Offset.YMillimeters
                && action.OffsetZMillimeters
                    == compiledEvent.Offset.ZMillimeters;
        }
    }

    public static class FpgFormalPlayerSkillAnimationClock
    {
        private const double MaximumInterpolation = 0.999999999d;

        public static double ResolveInterpolation(
            double renderTime,
            double fixedTime,
            double fixedDeltaTime)
        {
            if (double.IsNaN(renderTime)
                || double.IsInfinity(renderTime)
                || double.IsNaN(fixedTime)
                || double.IsInfinity(fixedTime)
                || double.IsNaN(fixedDeltaTime)
                || double.IsInfinity(fixedDeltaTime)
                || fixedDeltaTime <= 0d)
            {
                return 0d;
            }

            return Math.Min(
                MaximumInterpolation,
                Math.Max(0d, (renderTime - fixedTime) / fixedDeltaTime));
        }
    }

    public interface IFpgFormalPlayerPresentationSource
    {
        bool TryGetPlayerPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot snapshot);
    }

    /// <summary>
    /// Runtime-only source shared by the formal tick driver and presentation
    /// bridge. It intentionally has no Unity references, so it can be replaced
    /// by a deterministic test source without scene lookups.
    /// </summary>
    public sealed class FpgFormalPlayerPresentationSource :
        IFpgFormalPlayerPresentationSource
    {
        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private long nextActionSequence;
        private long nextSkillPresentationSequence;

        public event Action<FpgFormalPlayerActionEvent> ActionCommitted;
        public event Action<FpgFormalPlayerSkillSequenceEvent> SkillSequenceAdvanced;
        public event Action<FpgFormalPlayerActivePresentationEvent>
            ActivePresentationCommitted;

        public bool HasSnapshot => snapshot.IsValid;

        public bool TryGetPlayerPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot result)
        {
            result = snapshot;
            return result.IsValid;
        }

        public void PublishSnapshot(in FpgFormalPlayerPresentationSnapshot next)
        {
            snapshot = next;
        }

        public void PublishAction(
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter)
        {
            PublishAction(
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter,
                SkillExecutionId.Invalid,
                0);
        }

        public void PublishAction(
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter,
            SkillExecutionId skillExecutionId,
            int gameplayEventId)
        {
            long sequence = nextActionSequence == long.MaxValue
                ? 1L
                : nextActionSequence + 1L;
            nextActionSequence = sequence;
            FpgFormalPlayerActionEvent action = new FpgFormalPlayerActionEvent(
                sequence,
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter,
                skillExecutionId,
                gameplayEventId);

            // Presentation must never turn an animation/UI exception into a
            // deterministic combat failure.
            try
            {
                ActionCommitted?.Invoke(action);
            }
            catch (Exception)
            {
            }
        }

        public void PublishSkillSequence(
            in FpgPlayerSkillSequenceFrame frame,
            string animationName)
        {
            long sequence = NextSkillPresentationSequence();
            FpgFormalPlayerSkillSequenceEvent sequenceEvent =
                new FpgFormalPlayerSkillSequenceEvent(
                    sequence,
                    frame,
                    animationName);
            try
            {
                SkillSequenceAdvanced?.Invoke(sequenceEvent);
            }
            catch (Exception)
            {
            }
        }

        public void PublishActivePresentation(
            in FpgPlayerSkillExecutionEvent skillEvent)
        {
            long sequence = NextSkillPresentationSequence();
            FpgFormalPlayerActivePresentationEvent presentationEvent =
                new FpgFormalPlayerActivePresentationEvent(
                    sequence,
                    skillEvent);
            try
            {
                ActivePresentationCommitted?.Invoke(presentationEvent);
            }
            catch (Exception)
            {
            }
        }

        public void Clear()
        {
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            nextActionSequence = 0L;
            nextSkillPresentationSequence = 0L;
        }

        private long NextSkillPresentationSequence()
        {
            long sequence = nextSkillPresentationSequence == long.MaxValue
                ? 1L
                : nextSkillPresentationSequence + 1L;
            nextSkillPresentationSequence = sequence;
            return sequence;
        }
    }
}

