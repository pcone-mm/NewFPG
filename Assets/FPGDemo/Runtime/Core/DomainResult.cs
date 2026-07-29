namespace FPG.Demo.Core
{
    public enum RejectReason
    {
        None = 0,
        InvalidState,
        InvalidDefinition,
        WrongTick,
        DuplicateSequence,
        ExpiredSequence,
        NotEnoughAmmo,
        NotExposed,
        BarrierDepleted,
        ActionLocked,
        Cooldown,
        AlreadyTerminal,
        InvalidTarget,
        DuplicateImpact,
        BudgetExceeded,
        BufferCapacity,
        OwnerInterrupted,
        OwnerGroggy,
        RestartRequired,
        Disposed,
        InvariantFault
    }

    public readonly struct DomainResult
    {
        private DomainResult(bool isSuccess, RejectReason rejectReason)
        {
            IsSuccess = isSuccess;
            RejectReason = rejectReason;
        }

        public bool IsSuccess { get; }

        public RejectReason RejectReason { get; }

        public static DomainResult Success => new DomainResult(true, RejectReason.None);

        public static DomainResult Rejected(RejectReason reason)
        {
            return new DomainResult(false, reason);
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : RejectReason.ToString();
        }
    }
}
