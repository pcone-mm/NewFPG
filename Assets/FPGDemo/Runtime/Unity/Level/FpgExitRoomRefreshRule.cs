using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public readonly struct FpgExitRefreshContext : IEquatable<FpgExitRefreshContext>
    {
        public FpgExitRefreshContext(
            FpgEncounterRunContext runContext,
            string sourceRoomId)
        {
            if (!runContext.IsValid)
            {
                throw new ArgumentException(
                    "Exit refresh requires a valid encounter run context.",
                    nameof(runContext));
            }

            if (string.IsNullOrWhiteSpace(sourceRoomId))
            {
                throw new ArgumentException(
                    "Exit refresh requires a stable source room ID.",
                    nameof(sourceRoomId));
            }

            RunContext = runContext;
            SourceRoomId = sourceRoomId;
        }

        public FpgEncounterRunContext RunContext { get; }
        public string SourceRoomId { get; }
        public bool IsValid => RunContext.IsValid
            && !string.IsNullOrWhiteSpace(SourceRoomId);

        public bool Equals(FpgExitRefreshContext other)
        {
            return RunContext.Equals(other.RunContext)
                && string.Equals(SourceRoomId, other.SourceRoomId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FpgExitRefreshContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RunContext.GetHashCode() * 397)
                    ^ StringComparer.Ordinal.GetHashCode(SourceRoomId ?? string.Empty);
            }
        }
    }

    public readonly struct FpgExitRouteDecision : IEquatable<FpgExitRouteDecision>
    {
        public FpgExitRouteDecision(
            string sourceRoomId,
            string exitId,
            string destinationRoomId,
            int roomVisitOrdinal)
        {
            if (string.IsNullOrWhiteSpace(sourceRoomId))
            {
                throw new ArgumentException(
                    "Route decision requires a source room ID.",
                    nameof(sourceRoomId));
            }

            if (string.IsNullOrWhiteSpace(exitId))
            {
                throw new ArgumentException(
                    "Route decision requires an exit ID.",
                    nameof(exitId));
            }

            if (string.IsNullOrWhiteSpace(destinationRoomId))
            {
                throw new ArgumentException(
                    "Route decision requires a destination room ID.",
                    nameof(destinationRoomId));
            }

            if (roomVisitOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomVisitOrdinal));
            }

            SourceRoomId = sourceRoomId;
            ExitId = exitId;
            DestinationRoomId = destinationRoomId;
            RoomVisitOrdinal = roomVisitOrdinal;
        }

        public string SourceRoomId { get; }
        public string ExitId { get; }
        public string DestinationRoomId { get; }
        public int RoomVisitOrdinal { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(SourceRoomId)
            && !string.IsNullOrWhiteSpace(ExitId)
            && !string.IsNullOrWhiteSpace(DestinationRoomId)
            && RoomVisitOrdinal >= 0;

        public bool Equals(FpgExitRouteDecision other)
        {
            return string.Equals(SourceRoomId, other.SourceRoomId, StringComparison.Ordinal)
                && string.Equals(ExitId, other.ExitId, StringComparison.Ordinal)
                && string.Equals(
                    DestinationRoomId,
                    other.DestinationRoomId,
                    StringComparison.Ordinal)
                && RoomVisitOrdinal == other.RoomVisitOrdinal;
        }

        public override bool Equals(object obj)
        {
            return obj is FpgExitRouteDecision other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(SourceRoomId ?? string.Empty);
                hash = (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(ExitId ?? string.Empty);
                hash = (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(DestinationRoomId ?? string.Empty);
                return (hash * 397) ^ RoomVisitOrdinal;
            }
        }
    }

    public sealed class FpgExitOffer
    {
        public FpgExitOffer(
            FpgExitRouteDecision decision,
            FpgRoomDefinition destinationRoom)
        {
            if (!decision.IsValid)
            {
                throw new ArgumentException(
                    "Exit offer requires a valid route decision.",
                    nameof(decision));
            }

            if (destinationRoom == null)
            {
                throw new ArgumentNullException(nameof(destinationRoom));
            }

            if (!string.Equals(
                    decision.DestinationRoomId,
                    destinationRoom.RoomId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Exit offer destination does not match the route decision.",
                    nameof(destinationRoom));
            }

            Decision = decision;
            DestinationRoom = destinationRoom;
        }

        public FpgExitRouteDecision Decision { get; }
        public FpgRoomDefinition DestinationRoom { get; }
        public string SourceRoomId => Decision.SourceRoomId;
        public string ExitId => Decision.ExitId;
        public string DestinationRoomId => Decision.DestinationRoomId;
        public string DestinationDisplayName => DestinationRoom.DisplayName;
        public int RoomVisitOrdinal => Decision.RoomVisitOrdinal;
        public bool IsValid => Decision.IsValid
            && DestinationRoom != null
            && string.Equals(
                DestinationRoomId,
                DestinationRoom.RoomId,
                StringComparison.Ordinal);
    }

    public static class FpgExitRouteSelector
    {
        private const ulong ExitRefreshDomain = 0x4650475F45584954UL;
        private const ulong ExitShuffleDomain = 0x524F5554455F5631UL;
        private const ulong StableIdDomain = 0x4650475F52494431UL;

        public static bool TrySelect(
            FpgExitRefreshContext context,
            IReadOnlyList<string> candidateRoomIds,
            IReadOnlyList<string> exitIds,
            out FpgExitRouteDecision[] decisions,
            out string error)
        {
            decisions = Array.Empty<FpgExitRouteDecision>();
            error = string.Empty;
            if (!context.IsValid)
            {
                error = "Exit route selection requires a valid refresh context.";
                return false;
            }

            if (!TryMaterializeStableIds(
                    candidateRoomIds,
                    "candidate room",
                    out string[] candidates,
                    out error))
            {
                return false;
            }

            if (Array.BinarySearch(
                    candidates,
                    context.SourceRoomId,
                    StringComparer.Ordinal) < 0)
            {
                error =
                    $"Source room '{context.SourceRoomId}' is not present in the candidate room pool.";
                return false;
            }

            if (!TryMaterializeStableIds(
                    exitIds,
                    "exit",
                    out string[] stableExitIds,
                    out error))
            {
                return false;
            }

            decisions = new FpgExitRouteDecision[stableExitIds.Length];
            string[] shuffledCandidates = new string[candidates.Length];
            int activeCycle = -1;
            for (int index = 0; index < stableExitIds.Length; index++)
            {
                int cycle = index / candidates.Length;
                if (cycle != activeCycle)
                {
                    Array.Copy(candidates, shuffledCandidates, candidates.Length);
                    Shuffle(context, cycle, shuffledCandidates);
                    activeCycle = cycle;
                }

                string destinationRoomId =
                    shuffledCandidates[index % shuffledCandidates.Length];
                decisions[index] = new FpgExitRouteDecision(
                    context.SourceRoomId,
                    stableExitIds[index],
                    destinationRoomId,
                    context.RunContext.RoomVisitOrdinal);
            }

            return true;
        }

        private static bool TryMaterializeStableIds(
            IReadOnlyList<string> source,
            string label,
            out string[] stableIds,
            out string error)
        {
            stableIds = Array.Empty<string>();
            error = string.Empty;
            if (source == null || source.Count == 0)
            {
                error = $"Exit route selection requires at least one {label} ID.";
                return false;
            }

            stableIds = new string[source.Count];
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string stableId = source[index];
                if (string.IsNullOrWhiteSpace(stableId))
                {
                    stableIds = Array.Empty<string>();
                    error = $"Exit route selection {label} ID at index {index} is empty.";
                    return false;
                }

                if (!unique.Add(stableId))
                {
                    stableIds = Array.Empty<string>();
                    error =
                        $"Exit route selection contains duplicate {label} ID '{stableId}'.";
                    return false;
                }

                stableIds[index] = stableId;
            }

            Array.Sort(stableIds, StringComparer.Ordinal);
            return true;
        }

        private static void Shuffle(
            FpgExitRefreshContext context,
            int cycle,
            string[] candidates)
        {
            ulong owner = StableStringHash(context.SourceRoomId);
            ulong routeSeed = StableHash.Combine(
                context.RunContext.RunSeed,
                ExitRefreshDomain,
                owner,
                unchecked((ulong)context.RunContext.RoomVisitOrdinal));
            ulong cycleSeed = StableHash.Combine(
                routeSeed,
                ExitShuffleDomain,
                owner,
                unchecked((ulong)cycle));
            ulong sampleOrdinal = 0UL;
            for (int index = candidates.Length - 1; index > 0; index--)
            {
                int selected = SampleRange(
                    cycleSeed,
                    unchecked((ulong)cycle),
                    ref sampleOrdinal,
                    index + 1);
                if (selected == index)
                {
                    continue;
                }

                string swap = candidates[index];
                candidates[index] = candidates[selected];
                candidates[selected] = swap;
            }
        }

        private static int SampleRange(
            ulong seed,
            ulong owner,
            ref ulong ordinal,
            int exclusiveMaximum)
        {
            ulong bound = unchecked((ulong)exclusiveMaximum);
            ulong rejectionThreshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                ulong sample = StableHash.Combine(
                    seed,
                    ExitShuffleDomain,
                    owner,
                    ordinal++);
                if (sample >= rejectionThreshold)
                {
                    return unchecked((int)(sample % bound));
                }
            }
        }

        private static ulong StableStringHash(string value)
        {
            ulong hash = StableHash.Mix(StableIdDomain);
            for (int index = 0; index < value.Length; index++)
            {
                hash = StableHash.Append(hash, value[index]);
            }

            return hash;
        }
    }

    [CreateAssetMenu(
        fileName = "FpgExitRoomRefreshRule",
        menuName = "FPG Demo/Config/Level/Exit Room Refresh Rule")]
    public sealed class FpgExitRoomRefreshRule : ScriptableObject
    {
        [SerializeField]
        [D0PlannerField(
            "房间目录",
            "出口在当前房间清空时，从该目录内所有房间等概率抽取目标；当前房间也参与抽取。一个房间有多个出口时，会先遍历完整候选池，再允许目标重复。")]
        private FpgRoomCatalog roomCatalog;

        public FpgRoomCatalog RoomCatalog => roomCatalog;

        public bool TryValidate(out string error)
        {
            if (roomCatalog == null)
            {
                error = "Exit room refresh rule requires a room catalog.";
                return false;
            }

            if (!roomCatalog.TryValidate(out error))
            {
                error = $"Exit room refresh rule has an invalid room catalog: {error}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCreateDecisions(
            FpgExitRefreshContext context,
            IReadOnlyList<string> exitIds,
            out FpgExitRouteDecision[] decisions,
            out string error)
        {
            decisions = Array.Empty<FpgExitRouteDecision>();
            if (!TryValidate(out error)
                || !roomCatalog.TryGetStableRoomIds(
                    out string[] candidateRoomIds,
                    out error))
            {
                return false;
            }

            return FpgExitRouteSelector.TrySelect(
                context,
                candidateRoomIds,
                exitIds,
                out decisions,
                out error);
        }

        public bool TryCreateOffers(
            FpgExitRefreshContext context,
            IReadOnlyList<string> exitIds,
            out FpgExitOffer[] offers,
            out string error)
        {
            offers = Array.Empty<FpgExitOffer>();
            if (!TryCreateDecisions(context, exitIds, out FpgExitRouteDecision[] decisions, out error))
            {
                return false;
            }

            offers = new FpgExitOffer[decisions.Length];
            for (int index = 0; index < decisions.Length; index++)
            {
                FpgExitRouteDecision decision = decisions[index];
                if (!roomCatalog.TryResolve(
                        decision.DestinationRoomId,
                        out FpgRoomDefinition destinationRoom,
                        out error))
                {
                    offers = Array.Empty<FpgExitOffer>();
                    return false;
                }

                offers[index] = new FpgExitOffer(decision, destinationRoom);
            }

            error = string.Empty;
            return true;
        }
    }
}
