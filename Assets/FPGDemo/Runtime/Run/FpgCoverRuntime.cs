using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public interface IFpgCoverGeometryResolver
    {
        bool TryResolveCoverId(GeometryId geometryId, out string coverId);
    }

    public enum FpgCoverMoveDirection
    {
        None = 0,
        Left = -1,
        Right = 1
    }

    public readonly struct FpgCoverNodeDefinition
    {
        public FpgCoverNodeDefinition(
            string coverId,
            int lateralPositionKey,
            int maxDurability,
            bool isStartingCover)
        {
            if (string.IsNullOrWhiteSpace(coverId))
            {
                throw new ArgumentException("Cover id is required.", nameof(coverId));
            }

            if (maxDurability <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDurability));
            }

            CoverId = coverId;
            LateralPositionKey = lateralPositionKey;
            MaxDurability = maxDurability;
            IsStartingCover = isStartingCover;
        }

        public string CoverId { get; }
        public int LateralPositionKey { get; }
        public int MaxDurability { get; }
        public bool IsStartingCover { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(CoverId)
            && MaxDurability > 0;
    }

    public readonly struct FpgCoverSnapshot
    {
        public FpgCoverSnapshot(
            string coverId,
            int orderedIndex,
            int durability,
            int maxDurability,
            bool isCurrent,
            bool isMoveTarget,
            bool isMoving)
        {
            CoverId = coverId ?? string.Empty;
            OrderedIndex = orderedIndex;
            Durability = durability;
            MaxDurability = maxDurability;
            IsCurrent = isCurrent;
            IsMoveTarget = isMoveTarget;
            IsMoving = isMoving;
        }

        public string CoverId { get; }
        public int OrderedIndex { get; }
        public int Durability { get; }
        public int MaxDurability { get; }
        public bool IsCurrent { get; }
        public bool IsMoveTarget { get; }
        public bool IsMoving { get; }
        public bool IsDestroyed => MaxDurability > 0 && Durability <= 0;
        public bool IsValid => !string.IsNullOrWhiteSpace(CoverId)
            && OrderedIndex >= 0
            && Durability >= 0
            && MaxDurability > 0
            && Durability <= MaxDurability;
    }

    /// <summary>
    /// Room-scoped authoritative cover state. Definitions and durability storage
    /// are allocated once at room preparation and never resize during combat.
    /// </summary>
    public sealed class FpgCoverRuntime
    {
        public const int MaximumCoverCount = 32;

        private readonly FpgCoverNodeDefinition[] definitions;
        private readonly CombatantState[] durabilityStates;
        private readonly TickDuration traversalDuration;
        private int startingIndex;
        private int currentIndex;
        private int targetIndex = -1;
        private TickIndex traversalStartedTick = TickIndex.Invalid;
        private TickIndex traversalEndsTick = TickIndex.Invalid;

        public FpgCoverRuntime(
            RuntimeId playerRuntimeId,
            FpgCoverNodeDefinition[] source,
            TickDuration traversalDuration)
        {
            if (!playerRuntimeId.IsValid)
            {
                throw new ArgumentException(
                    "Cover runtime requires the player runtime id.",
                    nameof(playerRuntimeId));
            }

            if (source == null || source.Length == 0
                || source.Length > MaximumCoverCount)
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }

            if (traversalDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traversalDuration));
            }

            this.traversalDuration = traversalDuration;
            definitions = new FpgCoverNodeDefinition[source.Length];
            durabilityStates = new CombatantState[source.Length];
            Array.Copy(source, definitions, source.Length);
            SortDefinitions(definitions);

            int startCount = 0;
            for (int index = 0; index < definitions.Length; index++)
            {
                FpgCoverNodeDefinition definition = definitions[index];
                if (!definition.IsValid)
                {
                    throw new ArgumentException(
                        "Every cover definition must be valid.",
                        nameof(source));
                }

                for (int otherIndex = 0; otherIndex < index; otherIndex++)
                {
                    if (string.Equals(
                            definition.CoverId,
                            definitions[otherIndex].CoverId,
                            StringComparison.Ordinal)
                        || definition.LateralPositionKey
                            == definitions[otherIndex].LateralPositionKey)
                    {
                        throw new ArgumentException(
                            "Cover ids and lateral positions must be unique.",
                            nameof(source));
                    }
                }

                if (definition.IsStartingCover)
                {
                    startingIndex = index;
                    startCount++;
                }

                durabilityStates[index] = new CombatantState(
                    playerRuntimeId,
                    CombatantKind.Player,
                    1,
                    definition.MaxDurability,
                    0);
            }

            if (startCount != 1)
            {
                throw new ArgumentException(
                    "A room must define exactly one starting cover.",
                    nameof(source));
            }

            currentIndex = startingIndex;
        }

        public int Count => definitions.Length;
        public TickDuration TraversalDuration => traversalDuration;
        public bool IsTraversing => targetIndex >= 0;
        public TickIndex TraversalStartedTick => traversalStartedTick;
        public TickIndex TraversalEndsTick => traversalEndsTick;
        public bool CanBeTargeted => !IsTraversing;
        public bool CurrentCoverIsDestroyed =>
            durabilityStates[currentIndex].Barrier <= 0;
        public CombatantState CurrentDefenseState =>
            CurrentCoverIsDestroyed || IsTraversing
                ? null
                : durabilityStates[currentIndex];
        public FpgCoverSnapshot CurrentSnapshot => CreateSnapshot(currentIndex);
        public FpgCoverSnapshot TargetSnapshot => targetIndex < 0
            ? default(FpgCoverSnapshot)
            : CreateSnapshot(targetIndex);

        public FpgCoverSnapshot GetSnapshot(int orderedIndex)
        {
            if (orderedIndex < 0 || orderedIndex >= definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(orderedIndex));
            }

            return CreateSnapshot(orderedIndex);
        }

        public bool TryGetSnapshot(string coverId, out FpgCoverSnapshot snapshot)
        {
            int index = FindById(coverId);
            if (index < 0)
            {
                snapshot = default(FpgCoverSnapshot);
                return false;
            }

            snapshot = CreateSnapshot(index);
            return true;
        }

        public bool TryGetIntactDefenseState(
            string coverId,
            out CombatantState defenseState)
        {
            int index = FindById(coverId);
            if (index < 0 || durabilityStates[index].Barrier <= 0)
            {
                defenseState = null;
                return false;
            }

            defenseState = durabilityStates[index];
            return true;
        }

        public bool IsCurrentCover(string coverId)
        {
            return currentIndex >= 0
                && currentIndex < definitions.Length
                && string.Equals(
                    definitions[currentIndex].CoverId,
                    coverId,
                    StringComparison.Ordinal);
        }

        public DomainResult TryBeginMove(
            FpgCoverMoveDirection direction,
            TickIndex tick,
            out FpgCoverSnapshot target)
        {
            target = default(FpgCoverSnapshot);
            if (!tick.IsValid
                || direction != FpgCoverMoveDirection.Left
                    && direction != FpgCoverMoveDirection.Right)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (IsTraversing)
            {
                return DomainResult.Rejected(RejectReason.ActionLocked);
            }

            int nextIndex = currentIndex + (int)direction;
            if (nextIndex < 0 || nextIndex >= definitions.Length)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (tick.Value > long.MaxValue - traversalDuration.Value)
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            targetIndex = nextIndex;
            traversalStartedTick = tick;
            traversalEndsTick = new TickIndex(tick.Value + traversalDuration.Value);
            target = CreateSnapshot(targetIndex);
            return DomainResult.Success;
        }

        public DomainResult Advance(TickIndex tick, out bool completed)
        {
            completed = false;
            if (!tick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (!IsTraversing || tick < traversalEndsTick)
            {
                return DomainResult.Success;
            }

            currentIndex = targetIndex;
            targetIndex = -1;
            traversalStartedTick = TickIndex.Invalid;
            traversalEndsTick = TickIndex.Invalid;
            completed = true;
            return DomainResult.Success;
        }

        public void CancelTraversal()
        {
            targetIndex = -1;
            traversalStartedTick = TickIndex.Invalid;
            traversalEndsTick = TickIndex.Invalid;
        }

        public void Reset()
        {
            for (int index = 0; index < durabilityStates.Length; index++)
            {
                CombatantState state = durabilityStates[index];
                state.RestoreResources(new CombatantResourceSnapshot(
                    state.RuntimeId,
                    state.MaxLife,
                    state.MaxBarrier,
                    state.MaxBreak));
            }

            currentIndex = startingIndex;
            targetIndex = -1;
            traversalStartedTick = TickIndex.Invalid;
            traversalEndsTick = TickIndex.Invalid;
        }

        private FpgCoverSnapshot CreateSnapshot(int index)
        {
            FpgCoverNodeDefinition definition = definitions[index];
            CombatantState state = durabilityStates[index];
            return new FpgCoverSnapshot(
                definition.CoverId,
                index,
                state.Barrier,
                state.MaxBarrier,
                index == currentIndex,
                index == targetIndex,
                IsTraversing);
        }

        private int FindById(string coverId)
        {
            if (string.IsNullOrWhiteSpace(coverId))
            {
                return -1;
            }

            for (int index = 0; index < definitions.Length; index++)
            {
                if (string.Equals(
                        definitions[index].CoverId,
                        coverId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static void SortDefinitions(FpgCoverNodeDefinition[] values)
        {
            for (int index = 1; index < values.Length; index++)
            {
                FpgCoverNodeDefinition value = values[index];
                int insertion = index - 1;
                while (insertion >= 0 && Compare(values[insertion], value) > 0)
                {
                    values[insertion + 1] = values[insertion];
                    insertion--;
                }

                values[insertion + 1] = value;
            }
        }

        private static int Compare(
            FpgCoverNodeDefinition left,
            FpgCoverNodeDefinition right)
        {
            int lateral = left.LateralPositionKey.CompareTo(
                right.LateralPositionKey);
            return lateral != 0
                ? lateral
                : string.CompareOrdinal(left.CoverId, right.CoverId);
        }
    }
}
