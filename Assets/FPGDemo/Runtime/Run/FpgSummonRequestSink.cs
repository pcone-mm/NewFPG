using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public enum FpgSummonQueueDisposition
    {
        Queued = 0,
        RetryNextTick,
        StaticLimitReached,
        Rejected
    }

    /// <summary>
    /// Synchronous acknowledgement from the encounter-owned Spawn Queue. The
    /// combat port only consumes its attack after Queued or StaticLimitReached;
    /// retry keeps the same stable summon request pending for the next tick.
    /// </summary>
    public readonly struct FpgSummonQueueAck
    {
        private FpgSummonQueueAck(
            FpgSummonQueueDisposition disposition,
            DomainResult result)
        {
            Disposition = disposition;
            Result = result;
        }

        public FpgSummonQueueDisposition Disposition { get; }
        public DomainResult Result { get; }

        public static FpgSummonQueueAck Queued => new FpgSummonQueueAck(
            FpgSummonQueueDisposition.Queued,
            DomainResult.Success);

        public static FpgSummonQueueAck Retry(RejectReason reason)
        {
            return new FpgSummonQueueAck(
                FpgSummonQueueDisposition.RetryNextTick,
                DomainResult.Rejected(reason));
        }

        public static FpgSummonQueueAck LimitReached => new FpgSummonQueueAck(
            FpgSummonQueueDisposition.StaticLimitReached,
            DomainResult.Rejected(RejectReason.BufferCapacity));

        public static FpgSummonQueueAck Rejected(RejectReason reason)
        {
            return new FpgSummonQueueAck(
                FpgSummonQueueDisposition.Rejected,
                DomainResult.Rejected(reason));
        }
    }

    public interface IFpgSummonRequestSink
    {
        FpgSummonQueueAck TryQueueSummon(FpgSummonRequest request, TickIndex tick);
    }

    /// <summary>
    /// Adapter that makes FpgEncounterRuntime and its authoritative summon
    /// ledger the only request history and the only writer to the shared Spawn
    /// Queue. Gameplay quota checks apply only to additional-entity summons.
    /// </summary>
    public sealed class FpgEncounterRuntimeSummonSink : IFpgSummonRequestSink
    {
        private readonly FpgEncounterRuntime runtime;

        public FpgEncounterRuntimeSummonSink(FpgEncounterRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public FpgSummonQueueAck TryQueueSummon(FpgSummonRequest request, TickIndex tick)
        {
            FpgSummonLedger ledger = runtime.SummonLedger;
            if (ledger == null)
            {
                return FpgSummonQueueAck.Rejected(RejectReason.InvalidState);
            }

            bool enforceGameplayQuotas = request.OccupancyMode
                == FpgSummonOccupancyMode.AdditionalEntity;
            if (enforceGameplayQuotas
                && (request.MaxSummonsPerOwner <= 0
                    || ledger.CountGameplayQuotaOwner(
                        request.OwnerRuntimeId,
                        request.SummonActionId)
                        >= request.MaxSummonsPerOwner
                    || ledger.GameplayQuotaCount >= ledger.MaxTotalSummons))
            {
                return FpgSummonQueueAck.LimitReached;
            }

            DomainResult queued = runtime.TryQueueSummon(request, tick);
            if (queued.IsSuccess)
            {
                return FpgSummonQueueAck.Queued;
            }

            if (enforceGameplayQuotas
                && (queued.RejectReason == RejectReason.BudgetExceeded
                    || queued.RejectReason == RejectReason.InvalidTarget))
            {
                return FpgSummonQueueAck.Retry(queued.RejectReason);
            }

            return FpgSummonQueueAck.Rejected(queued.RejectReason);
        }
    }
}
