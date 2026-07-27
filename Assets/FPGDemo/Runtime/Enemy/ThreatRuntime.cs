using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Enemy
{
    public enum EnemyControlState
    {
        Active = 0,
        Groggy,
        Dead
    }

    public enum ThreatState
    {
        Scheduled = 0,
        Telegraph,
        Windup,
        ReleaseCommitted,
        Recovery,
        Completed,
        Canceled
    }

    public readonly struct ThreatSnapshot
    {
        public ThreatSnapshot(
            RuntimeId runtimeId,
            int definitionId,
            ThreatState state,
            AttackId attackId,
            TickIndex stateUntilTick,
            bool hasReleased,
            bool isTerminal)
            : this(
                runtimeId,
                definitionId,
                state,
                attackId,
                stateUntilTick,
                hasReleased,
                isTerminal,
                ThreatPayloadKind.SweptProjectile,
                FpgThreatPresentationKind.FastUninterceptable,
                1,
                ThreatTargetPolicy.PlayerCombatant)
        {
        }

        public ThreatSnapshot(
            RuntimeId runtimeId,
            int definitionId,
            ThreatState state,
            AttackId attackId,
            TickIndex stateUntilTick,
            bool hasReleased,
            bool isTerminal,
            ThreatPayloadKind payloadKind,
            FpgThreatPresentationKind presentationKind,
            int presentationKey,
            ThreatTargetPolicy targetPolicy)
        {
            RuntimeId = runtimeId;
            DefinitionId = definitionId;
            State = state;
            AttackId = attackId;
            StateUntilTick = stateUntilTick;
            HasReleased = hasReleased;
            IsTerminal = isTerminal;
            PayloadKind = payloadKind;
            PresentationKind = presentationKind;
            PresentationKey = presentationKey;
            TargetPolicy = targetPolicy;
        }

        public RuntimeId RuntimeId { get; }
        public int DefinitionId { get; }
        public ThreatState State { get; }
        public AttackId AttackId { get; }
        public TickIndex StateUntilTick { get; }
        public bool HasReleased { get; }
        public bool IsTerminal { get; }
        public ThreatPayloadKind PayloadKind { get; }
        public FpgThreatPresentationKind PresentationKind { get; }
        public int PresentationKey { get; }
        public ThreatTargetPolicy TargetPolicy { get; }
    }

    public readonly struct ThreatDefinition
    {
        public ThreatDefinition(
            int definitionId,
            TickDuration telegraphDuration,
            TickDuration windupDuration,
            TickDuration recoveryDuration,
            ProjectileDefinition projectileDefinition,
            int payloadCount,
            FpgThreatPresentationKind presentationKind)
            : this(
                definitionId,
                telegraphDuration,
                windupDuration,
                recoveryDuration,
                ThreatPayloadDefinition.SweptProjectile(
                    projectileDefinition,
                    payloadCount,
                    presentationKind))
        {
        }

        public ThreatDefinition(
            int definitionId,
            TickDuration telegraphDuration,
            TickDuration windupDuration,
            TickDuration recoveryDuration,
            ThreatPayloadDefinition payload)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionId));
            }

            if (!payload.IsValid)
            {
                throw new ArgumentException("Threat payload must be valid.", nameof(payload));
            }

            DefinitionId = definitionId;
            TelegraphDuration = telegraphDuration;
            WindupDuration = windupDuration;
            RecoveryDuration = recoveryDuration;
            Payload = payload;
        }

        public int DefinitionId { get; }
        public TickDuration TelegraphDuration { get; }
        public TickDuration WindupDuration { get; }
        public TickDuration RecoveryDuration { get; }
        public ThreatPayloadDefinition Payload { get; }
        public ProjectileDefinition ProjectileDefinition => Payload.ProjectileDefinition;
        public int PayloadCount => Payload.PayloadCount;
        public int TotalBudgetUnits => Payload.TotalBudgetUnits;
    }

    public readonly struct ThreatRelease
    {
        public ThreatRelease(
            AttackId attackId,
            ThreatDefinition definition,
            ReservationToken reservationToken,
            TickIndex releaseTick)
        {
            AttackId = attackId;
            Definition = definition;
            ReservationToken = reservationToken;
            ReleaseTick = releaseTick;
        }

        public AttackId AttackId { get; }
        public ThreatDefinition Definition { get; }
        public ReservationToken ReservationToken { get; }
        public TickIndex ReleaseTick { get; }
    }

    public sealed class ThreatRuntime
    {
        private TickIndex stateUntilTick = TickIndex.Invalid;
        private ReservationToken reservationToken;

        public ThreatRuntime(ThreatDefinition definition)
        {
            Definition = definition;
            State = ThreatState.Scheduled;
        }

        public ThreatRuntime(ThreatDefinition definition, RuntimeId runtimeId)
            : this(definition)
        {
            if (!runtimeId.IsValid)
            {
                throw new ArgumentException("Threat RuntimeId must be valid.", nameof(runtimeId));
            }

            RuntimeId = runtimeId;
        }

        public RuntimeId RuntimeId { get; private set; } = RuntimeId.Invalid;
        public ThreatDefinition Definition { get; }
        public ThreatState State { get; private set; }
        public AttackId AttackId { get; private set; } = AttackId.Invalid;
        public ReservationToken ReservationToken => reservationToken;
        public TickIndex StateUntilTick => stateUntilTick;
        public bool IsTerminal => State == ThreatState.Completed || State == ThreatState.Canceled;
        public bool HasReleased => State == ThreatState.ReleaseCommitted
            || State == ThreatState.Recovery
            || State == ThreatState.Completed;

        public ThreatSnapshot GetSnapshot()
        {
            ThreatPayloadDefinition payload = Definition.Payload;
            return new ThreatSnapshot(
                RuntimeId,
                Definition.DefinitionId,
                State,
                AttackId,
                stateUntilTick,
                HasReleased,
                IsTerminal,
                payload.Kind,
                payload.PresentationKind,
                payload.PresentationKey,
                payload.TargetPolicy);
        }

        public DomainResult TryStart(
            TickIndex currentTick,
            EnemyControlState ownerState,
            ProjectileBudget budget,
            SessionIdAllocator idAllocator)
        {
            if (!currentTick.IsValid || budget == null || idAllocator == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (ownerState == EnemyControlState.Groggy)
            {
                return DomainResult.Rejected(RejectReason.OwnerGroggy);
            }

            if (ownerState == EnemyControlState.Dead)
            {
                return DomainResult.Rejected(RejectReason.OwnerInterrupted);
            }

            if (State != ThreatState.Scheduled)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            if (Definition.TotalBudgetUnits > 0)
            {
                DomainResult reserve = budget.TryReserve(Definition.TotalBudgetUnits, out reservationToken);
                if (!reserve.IsSuccess)
                {
                    return reserve;
                }
            }

            AttackId = idAllocator.NextAttackId();
            State = ThreatState.Telegraph;
            stateUntilTick = currentTick + Definition.TelegraphDuration;
            return DomainResult.Success;
        }

        public DomainResult AdvanceBeforeRelease(TickIndex currentTick)
        {
            if (!currentTick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (State == ThreatState.Telegraph && currentTick >= stateUntilTick)
            {
                State = ThreatState.Windup;
                stateUntilTick = currentTick + Definition.WindupDuration;
                return DomainResult.Success;
            }

            if (State == ThreatState.Recovery && currentTick >= stateUntilTick)
            {
                State = ThreatState.Completed;
                stateUntilTick = TickIndex.Invalid;
                return DomainResult.Success;
            }

            return DomainResult.Rejected(RejectReason.WrongTick);
        }

        public DomainResult TryCommitRelease(
            TickIndex currentTick,
            ProjectileBudget budget,
            out ThreatRelease release)
        {
            release = default(ThreatRelease);
            if (!currentTick.IsValid || budget == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (State != ThreatState.Windup)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            if (currentTick < stateUntilTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (reservationToken.IsValid)
            {
                DomainResult activation = budget.Activate(reservationToken);
                if (!activation.IsSuccess)
                {
                    return activation;
                }
            }

            State = ThreatState.ReleaseCommitted;
            stateUntilTick = TickIndex.Invalid;
            release = new ThreatRelease(AttackId, Definition, reservationToken, currentTick);
            return DomainResult.Success;
        }

        public DomainResult ConfirmPayloadsCreated(TickIndex currentTick)
        {
            if (!currentTick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (State != ThreatState.ReleaseCommitted)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            State = ThreatState.Recovery;
            stateUntilTick = currentTick + Definition.RecoveryDuration;
            return DomainResult.Success;
        }

        public DomainResult TryCancelBeforeRelease(ProjectileBudget budget)
        {
            if (HasReleased)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (State == ThreatState.Canceled || State == ThreatState.Completed)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (reservationToken.IsValid)
            {
                DomainResult release = budget.ReleaseReservation(reservationToken);
                if (!release.IsSuccess)
                {
                    return release;
                }
            }

            State = ThreatState.Canceled;
            stateUntilTick = TickIndex.Invalid;
            return DomainResult.Success;
        }

        public DomainResult TryCancelForSessionTermination(ProjectileBudget budget)
        {
            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (IsTerminal)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (!HasReleased && reservationToken.IsValid)
            {
                DomainResult released = budget.ReleaseReservation(reservationToken);
                if (!released.IsSuccess)
                {
                    return released;
                }
            }

            State = ThreatState.Canceled;
            stateUntilTick = TickIndex.Invalid;
            return DomainResult.Success;
        }
    }

    public sealed class GroggyRuntime
    {
        public TickIndex UntilTick { get; private set; } = TickIndex.Invalid;
        public bool IsActive => UntilTick.IsValid;

        public void Enter(TickIndex currentTick, TickDuration duration)
        {
            if (duration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            if (!currentTick.IsValid)
            {
                throw new ArgumentException("Groggy tick must be valid.", nameof(currentTick));
            }

            UntilTick = currentTick + duration;
        }

        public bool TryRecover(TickIndex currentTick)
        {
            if (!currentTick.IsValid)
            {
                return false;
            }

            if (!UntilTick.IsValid || currentTick < UntilTick)
            {
                return false;
            }

            UntilTick = TickIndex.Invalid;
            return true;
        }
    }
}
