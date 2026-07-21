using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public enum ProjectileState
    {
        Scheduled = 0,
        Travelling,
        Hit,
        Destroyed,
        Expired,
        Canceled
    }

    public enum ProjectileTerminalReason
    {
        None = 0,
        TargetImpact,
        EnvironmentBlocked,
        Intercepted,
        Missed,
        LifetimeExpired,
        OwnerCanceled,
        SessionEnded
    }

    public readonly struct ProjectileSnapshot
    {
        public ProjectileSnapshot(
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            Team team,
            ProjectileState state,
            int hitPoints,
            TickIndex impactTick,
            TickIndex expireTick,
            TickIndex spawnTick,
            int definitionId,
            int presentationKey,
            ProjectileTerminalReason terminalReason,
            TickIndex terminalTick)
        {
            ProjectileId = projectileId;
            RuntimeId = runtimeId;
            AttackId = attackId;
            OwnerId = ownerId;
            Team = team;
            State = state;
            HitPoints = hitPoints;
            ImpactTick = impactTick;
            ExpireTick = expireTick;
            SpawnTick = spawnTick;
            DefinitionId = definitionId;
            PresentationKey = presentationKey;
            TerminalReason = terminalReason;
            TerminalTick = terminalTick;
        }

        public ProjectileId ProjectileId { get; }
        public RuntimeId RuntimeId { get; }
        public AttackId AttackId { get; }
        public RuntimeId OwnerId { get; }
        public Team Team { get; }
        public ProjectileState State { get; }
        public int HitPoints { get; }
        public TickIndex ImpactTick { get; }
        public TickIndex ExpireTick { get; }
        public TickIndex SpawnTick { get; }
        public int DefinitionId { get; }
        public int PresentationKey { get; }
        public ProjectileTerminalReason TerminalReason { get; }
        public TickIndex TerminalTick { get; }
        public bool IsTerminal => State == ProjectileState.Hit
            || State == ProjectileState.Destroyed
            || State == ProjectileState.Expired
            || State == ProjectileState.Canceled;
    }

    public readonly struct ProjectileDefinition
    {
        public ProjectileDefinition(
            int definitionId,
            TickDuration flightDuration,
            TickDuration expireDuration,
            DamageSpec damageSpec,
            int maxHitPoints,
            bool interceptable,
            int budgetUnits,
            int presentationKey = 1,
            int sweepRadiusKey = 1)
        {
            if (definitionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionId));
            }

            if (flightDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(flightDuration));
            }

            if (expireDuration.Value < flightDuration.Value)
            {
                throw new ArgumentOutOfRangeException(nameof(expireDuration));
            }

            if (maxHitPoints < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            }

            if (interceptable && maxHitPoints == 0)
            {
                throw new ArgumentException("Interceptable projectiles require positive hit points.", nameof(maxHitPoints));
            }

            if (budgetUnits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(budgetUnits));
            }

            if (presentationKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(presentationKey));
            }

            if (sweepRadiusKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sweepRadiusKey));
            }

            DefinitionId = definitionId;
            FlightDuration = flightDuration;
            ExpireDuration = expireDuration;
            DamageSpec = damageSpec;
            MaxHitPoints = maxHitPoints;
            Interceptable = interceptable;
            BudgetUnits = budgetUnits;
            PresentationKey = presentationKey;
            SweepRadiusKey = sweepRadiusKey;
        }

        public int DefinitionId { get; }
        public TickDuration FlightDuration { get; }
        public TickDuration ExpireDuration { get; }
        public DamageSpec DamageSpec { get; }
        public int MaxHitPoints { get; }
        public bool Interceptable { get; }
        public int BudgetUnits { get; }
        public int PresentationKey { get; }
        public int SweepRadiusKey { get; }
    }

    public sealed class ProjectileRuntime
    {
        public ProjectileRuntime(
            ProjectileId projectileId,
            RuntimeId runtimeId,
            AttackId attackId,
            RuntimeId ownerId,
            Team team,
            ProjectileDefinition definition,
            TickIndex spawnTick,
            ReservationToken reservationToken)
        {
            if (!projectileId.IsValid || !runtimeId.IsValid || !attackId.IsValid || !ownerId.IsValid)
            {
                throw new ArgumentException("Projectile identifiers must be valid.");
            }

            if (!Enum.IsDefined(typeof(Team), team) || team == Team.Neutral)
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            if (definition.DefinitionId <= 0)
            {
                throw new ArgumentException("Projectile definition must be initialized.", nameof(definition));
            }

            if (!spawnTick.IsValid)
            {
                throw new ArgumentException("Projectile spawn tick must be valid.", nameof(spawnTick));
            }

            ProjectileId = projectileId;
            RuntimeId = runtimeId;
            AttackId = attackId;
            OwnerId = ownerId;
            Team = team;
            Definition = definition;
            SpawnTick = spawnTick;
            ImpactTick = spawnTick + definition.FlightDuration;
            ExpireTick = spawnTick + definition.ExpireDuration;
            HitPoints = definition.MaxHitPoints;
            ReservationToken = reservationToken;
            State = ProjectileState.Scheduled;
            TerminalReason = ProjectileTerminalReason.None;
            TerminalTick = TickIndex.Invalid;
        }

        public ProjectileId ProjectileId { get; }
        public RuntimeId RuntimeId { get; }
        public AttackId AttackId { get; }
        public RuntimeId OwnerId { get; }
        public Team Team { get; }
        public ProjectileDefinition Definition { get; }
        public TickIndex SpawnTick { get; }
        public TickIndex ImpactTick { get; }
        public TickIndex ExpireTick { get; }
        public int HitPoints { get; private set; }
        public ProjectileState State { get; private set; }
        public ReservationToken ReservationToken { get; }
        public ProjectileTerminalReason TerminalReason { get; private set; }
        public TickIndex TerminalTick { get; private set; }
        public bool IsTerminal => State == ProjectileState.Hit
            || State == ProjectileState.Destroyed
            || State == ProjectileState.Expired
            || State == ProjectileState.Canceled;

        public ProjectileSnapshot GetSnapshot()
        {
            return new ProjectileSnapshot(
                ProjectileId,
                RuntimeId,
                AttackId,
                OwnerId,
                Team,
                State,
                HitPoints,
                ImpactTick,
                ExpireTick,
                SpawnTick,
                Definition.DefinitionId,
                Definition.PresentationKey,
                TerminalReason,
                TerminalTick);
        }

        public DomainResult StartTravelling()
        {
            if (State != ProjectileState.Scheduled)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            State = ProjectileState.Travelling;
            return DomainResult.Success;
        }

        public DomainResult TryHit()
        {
            return TryHit(ImpactTick);
        }

        public DomainResult TryHit(TickIndex currentTick)
        {
            if (State != ProjectileState.Travelling)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            if (!currentTick.IsValid || currentTick < SpawnTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            State = ProjectileState.Hit;
            TerminalReason = ProjectileTerminalReason.TargetImpact;
            TerminalTick = currentTick;
            return DomainResult.Success;
        }

        public DomainResult TryExpire(TickIndex currentTick)
        {
            if (State != ProjectileState.Travelling)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            if (!currentTick.IsValid || currentTick < SpawnTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (currentTick < ExpireTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            State = ProjectileState.Expired;
            TerminalReason = ProjectileTerminalReason.LifetimeExpired;
            TerminalTick = currentTick;
            return DomainResult.Success;
        }

        public DomainResult TryMiss(TickIndex currentTick)
        {
            if (State != ProjectileState.Travelling)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            if (!currentTick.IsValid || currentTick < ImpactTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            State = ProjectileState.Expired;
            TerminalReason = ProjectileTerminalReason.Missed;
            TerminalTick = currentTick;
            return DomainResult.Success;
        }

        public DomainResult TryBlock(TickIndex currentTick)
        {
            if (State != ProjectileState.Travelling)
            {
                return DomainResult.Rejected(IsTerminal ? RejectReason.AlreadyTerminal : RejectReason.InvalidState);
            }

            if (!currentTick.IsValid || currentTick < SpawnTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            State = ProjectileState.Canceled;
            TerminalReason = ProjectileTerminalReason.EnvironmentBlocked;
            TerminalTick = currentTick;
            return DomainResult.Success;
        }

        public DomainResult TryCancel(
            TickIndex currentTick,
            ProjectileTerminalReason reason = ProjectileTerminalReason.OwnerCanceled)
        {
            if (IsTerminal)
            {
                return DomainResult.Rejected(RejectReason.AlreadyTerminal);
            }

            if (!currentTick.IsValid || currentTick < SpawnTick)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (reason != ProjectileTerminalReason.OwnerCanceled && reason != ProjectileTerminalReason.SessionEnded)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            State = ProjectileState.Canceled;
            TerminalReason = reason;
            TerminalTick = currentTick;
            return DomainResult.Success;
        }

        internal int ApplyDamage(int amount, TickIndex currentTick)
        {
            if (State != ProjectileState.Travelling
                || !currentTick.IsValid
                || currentTick < SpawnTick
                || !Definition.Interceptable
                || HitPoints <= 0)
            {
                return 0;
            }

            int applied = Math.Min(Math.Max(amount, 0), HitPoints);
            HitPoints -= applied;
            if (HitPoints == 0)
            {
                State = ProjectileState.Destroyed;
                TerminalReason = ProjectileTerminalReason.Intercepted;
                TerminalTick = currentTick;
            }

            return applied;
        }
    }

    public readonly struct ReservationToken : IEquatable<ReservationToken>
    {
        internal ReservationToken(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0L;
        public bool Equals(ReservationToken other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ReservationToken other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public static bool operator ==(ReservationToken left, ReservationToken right) => left.Equals(right);
        public static bool operator !=(ReservationToken left, ReservationToken right) => !left.Equals(right);
    }

    public sealed class ProjectileBudget
    {
        private readonly ReservationEntry[] entries;
        private long nextTokenValue = 1L;

        public ProjectileBudget(int capacity, int maxReservations = 32)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (maxReservations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReservations));
            }

            Capacity = capacity;
            entries = new ReservationEntry[maxReservations];
        }

        public int Capacity { get; }
        public int ReservedUnits { get; private set; }
        public int ActiveUnits { get; private set; }

        public DomainResult CanReserve(int units)
        {
            if (units <= 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if ((long)ReservedUnits + ActiveUnits + units > Capacity)
            {
                return DomainResult.Rejected(RejectReason.BudgetExceeded);
            }

            return FindFreeEntry() < 0
                ? DomainResult.Rejected(RejectReason.BufferCapacity)
                : DomainResult.Success;
        }

        public DomainResult TryReserve(int units, out ReservationToken token)
        {
            token = default(ReservationToken);
            DomainResult canReserve = CanReserve(units);
            if (!canReserve.IsSuccess)
            {
                return canReserve;
            }

            int freeIndex = FindFreeEntry();
            token = new ReservationToken(nextTokenValue++);
            entries[freeIndex] = new ReservationEntry(token, units, ReservationState.Reserved);
            ReservedUnits += units;
            return DomainResult.Success;
        }

        public DomainResult Activate(ReservationToken token)
        {
            int index = FindEntry(token);
            if (index < 0 || entries[index].State != ReservationState.Reserved)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            ReservationEntry entry = entries[index];
            ReservedUnits -= entry.RemainingUnits;
            ActiveUnits += entry.RemainingUnits;
            entry.State = ReservationState.Active;
            entries[index] = entry;
            return DomainResult.Success;
        }

        public DomainResult ReleaseReservation(ReservationToken token)
        {
            int index = FindEntry(token);
            if (index < 0 || entries[index].State != ReservationState.Reserved)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            ReservedUnits -= entries[index].RemainingUnits;
            entries[index] = default(ReservationEntry);
            return DomainResult.Success;
        }

        public DomainResult ReleaseActive(ReservationToken token, int units)
        {
            int index = FindEntry(token);
            if (index < 0 || entries[index].State != ReservationState.Active || units <= 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            ReservationEntry entry = entries[index];
            if (units > entry.RemainingUnits)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            entry.RemainingUnits -= units;
            ActiveUnits -= units;
            entries[index] = entry.RemainingUnits == 0 ? default(ReservationEntry) : entry;
            return DomainResult.Success;
        }

        public void CancelAll()
        {
            for (int index = 0; index < entries.Length; index++)
            {
                entries[index] = default(ReservationEntry);
            }

            ReservedUnits = 0;
            ActiveUnits = 0;
        }

        private int FindFreeEntry()
        {
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].State == ReservationState.None)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindEntry(ReservationToken token)
        {
            if (!token.IsValid)
            {
                return -1;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].Token == token)
                {
                    return index;
                }
            }

            return -1;
        }

        private enum ReservationState
        {
            None = 0,
            Reserved,
            Active
        }

        private struct ReservationEntry
        {
            public ReservationEntry(ReservationToken token, int remainingUnits, ReservationState state)
            {
                Token = token;
                RemainingUnits = remainingUnits;
                State = state;
            }

            public ReservationToken Token;
            public int RemainingUnits;
            public ReservationState State;
        }
    }

    public readonly struct TimedImpact
    {
        public TimedImpact(TickIndex dueTick, ImpactIntent intent)
        {
            DueTick = dueTick;
            Intent = intent;
        }

        public TickIndex DueTick { get; }
        public ImpactIntent Intent { get; }
    }
}
