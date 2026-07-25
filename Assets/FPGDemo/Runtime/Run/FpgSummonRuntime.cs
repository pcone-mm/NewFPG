using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Determines whether a summon adds another room-managed combatant or
    /// replaces its owner. Replacement summons bypass gameplay quotas because
    /// the owner is consumed after the child is accepted by the Spawn Queue.
    /// Fixed runtime capacities and stable request validation still apply.
    /// </summary>
    public enum FpgSummonOccupancyMode
    {
        AdditionalEntity = 0,
        ReplaceOwner = 1
    }

    /// <summary>
    /// Authored source for a summoned entity's initial world pose. The pure
    /// run layer carries only this intent; the Unity entity port captures the
    /// actual pose while the owner is still alive.
    /// </summary>
    public enum FpgSummonPlacementMode
    {
        EncounterSpawnPoint = 0,
        OwnerPosition = 1
    }

    public readonly struct FpgSummonRequest
    {
        public FpgSummonRequest(
            RuntimeId ownerRuntimeId,
            string enemyDefinitionId,
            int recursionDepth,
            long requestSequence,
            string summonActionId,
            int maxSummonsPerOwner = int.MaxValue,
            FpgSummonOccupancyMode occupancyMode = FpgSummonOccupancyMode.AdditionalEntity,
            FpgSummonPlacementMode placementMode = FpgSummonPlacementMode.EncounterSpawnPoint)
        {
            if (!ownerRuntimeId.IsValid || string.IsNullOrWhiteSpace(enemyDefinitionId)
                || string.IsNullOrWhiteSpace(summonActionId)
                || recursionDepth < 0 || requestSequence < 0 || maxSummonsPerOwner < 0
                || !Enum.IsDefined(typeof(FpgSummonOccupancyMode), occupancyMode)
                || !Enum.IsDefined(typeof(FpgSummonPlacementMode), placementMode)
                || (occupancyMode == FpgSummonOccupancyMode.AdditionalEntity
                    && maxSummonsPerOwner <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(ownerRuntimeId));
            }

            OwnerRuntimeId = ownerRuntimeId;
            EnemyDefinitionId = enemyDefinitionId;
            RecursionDepth = recursionDepth;
            RequestSequence = requestSequence;
            MaxSummonsPerOwner = maxSummonsPerOwner;
            OccupancyMode = occupancyMode;
            PlacementMode = placementMode;
            SummonActionId = summonActionId;
        }

        public RuntimeId OwnerRuntimeId { get; }
        public string EnemyDefinitionId { get; }
        public int RecursionDepth { get; }
        public long RequestSequence { get; }
        public int MaxSummonsPerOwner { get; }
        public FpgSummonOccupancyMode OccupancyMode { get; }
        public FpgSummonPlacementMode PlacementMode { get; }
        public string SummonActionId { get; }
        public bool IsValid => OwnerRuntimeId.IsValid
            && !string.IsNullOrWhiteSpace(EnemyDefinitionId)
            && !string.IsNullOrWhiteSpace(SummonActionId)
            && RecursionDepth >= 0
            && RequestSequence >= 0
            && MaxSummonsPerOwner >= 0
            && Enum.IsDefined(typeof(FpgSummonOccupancyMode), OccupancyMode)
            && Enum.IsDefined(typeof(FpgSummonPlacementMode), PlacementMode)
            && (OccupancyMode != FpgSummonOccupancyMode.AdditionalEntity
                || MaxSummonsPerOwner > 0);
    }

    /// <summary>
    /// Fixed summon ledger shared by every enemy skill. It prevents an enemy
    /// identity from acquiring a special summon path and makes recursive
    /// summon limits explicit before an entry reaches the spawn queue.
    /// </summary>
    public sealed class FpgSummonLedger
    {
        private readonly FpgSummonRequest[] requests;
        private readonly int maxTotalSummons;
        private readonly int maxRecursionDepth;
        private int count;

        public FpgSummonLedger(int capacity, int maxTotalSummons, int maxRecursionDepth)
        {
            if (capacity <= 0 || maxTotalSummons < 0 || maxRecursionDepth < 0
                || maxTotalSummons > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            requests = new FpgSummonRequest[capacity];
            this.maxTotalSummons = maxTotalSummons;
            this.maxRecursionDepth = maxRecursionDepth;
        }

        public int Capacity => requests.Length;
        public int Count => count;
        public int GameplayQuotaCount => CountByOccupancy(
            FpgSummonOccupancyMode.AdditionalEntity);
        public int MaxTotalSummons => maxTotalSummons;
        public int MaxRecursionDepth => maxRecursionDepth;

        public DomainResult TryReserve(FpgSummonRequest request)
        {
            if (!request.IsValid
                || request.RecursionDepth > maxRecursionDepth)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            // RequestSequence is the caller's stable edge identity. Check it
            // before capacities so duplicate replays have one deterministic
            // rejection reason regardless of current ledger fill.
            for (int index = 0; index < count; index++)
            {
                if (requests[index].RequestSequence == request.RequestSequence)
                {
                    return DomainResult.Rejected(RejectReason.DuplicateSequence);
                }
            }

            if (HasAcceptedReplacement(request.OwnerRuntimeId))
            {
                return DomainResult.Rejected(RejectReason.OwnerInterrupted);
            }

            bool enforceGameplayQuotas = request.OccupancyMode
                == FpgSummonOccupancyMode.AdditionalEntity;
            if (count >= requests.Length
                || (enforceGameplayQuotas
                    && (request.MaxSummonsPerOwner <= 0
                        || CountGameplayQuotaOwner(
                            request.OwnerRuntimeId,
                            request.SummonActionId)
                            >= request.MaxSummonsPerOwner
                        || GameplayQuotaCount >= maxTotalSummons)))
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            requests[count++] = request;
            return DomainResult.Success;
        }

        /// <summary>
        /// Rolls back an accepted request when spawn preparation fails. This
        /// never changes the order of the remaining accepted requests.
        /// </summary>
        public DomainResult TryRollback(FpgSummonRequest request)
        {
            for (int index = 0; index < count; index++)
            {
                FpgSummonRequest candidate = requests[index];
                if (candidate.OwnerRuntimeId != request.OwnerRuntimeId
                    || candidate.RequestSequence != request.RequestSequence)
                {
                    continue;
                }

                for (int move = index + 1; move < count; move++)
                {
                    requests[move - 1] = requests[move];
                }

                requests[--count] = default(FpgSummonRequest);
                return DomainResult.Success;
            }

            return DomainResult.Rejected(RejectReason.InvalidTarget);
        }

        public int CountOwner(RuntimeId ownerRuntimeId)
        {
            int ownerCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (requests[index].OwnerRuntimeId == ownerRuntimeId)
                {
                    ownerCount++;
                }
            }

            return ownerCount;
        }

        public int CountGameplayQuotaOwner(
            RuntimeId ownerRuntimeId,
            string summonActionId)
        {
            int ownerCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (requests[index].OccupancyMode
                        == FpgSummonOccupancyMode.AdditionalEntity
                    && requests[index].OwnerRuntimeId == ownerRuntimeId
                    && string.Equals(
                        requests[index].SummonActionId,
                        summonActionId,
                        StringComparison.Ordinal))
                {
                    ownerCount++;
                }
            }

            return ownerCount;
        }

        public int CountDefinition(string enemyDefinitionId)
        {
            int definitionCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (string.Equals(requests[index].EnemyDefinitionId, enemyDefinitionId, StringComparison.Ordinal))
                {
                    definitionCount++;
                }
            }

            return definitionCount;
        }

        public void Clear()
        {
            Array.Clear(requests, 0, requests.Length);
            count = 0;
        }

        private int CountByOccupancy(FpgSummonOccupancyMode occupancyMode)
        {
            int matchingCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (requests[index].OccupancyMode == occupancyMode)
                {
                    matchingCount++;
                }
            }

            return matchingCount;
        }

        private bool HasAcceptedReplacement(RuntimeId ownerRuntimeId)
        {
            for (int index = 0; index < count; index++)
            {
                if (requests[index].OwnerRuntimeId == ownerRuntimeId
                    && requests[index].OccupancyMode
                        == FpgSummonOccupancyMode.ReplaceOwner)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
