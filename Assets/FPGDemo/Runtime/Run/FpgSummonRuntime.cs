using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public readonly struct FpgSummonRequest
    {
        public FpgSummonRequest(
            RuntimeId ownerRuntimeId,
            string enemyDefinitionId,
            int recursionDepth,
            long requestSequence,
            int maxSummonsPerOwner = int.MaxValue)
        {
            if (!ownerRuntimeId.IsValid || string.IsNullOrWhiteSpace(enemyDefinitionId)
                || recursionDepth < 0 || requestSequence < 0 || maxSummonsPerOwner < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerRuntimeId));
            }

            OwnerRuntimeId = ownerRuntimeId;
            EnemyDefinitionId = enemyDefinitionId;
            RecursionDepth = recursionDepth;
            RequestSequence = requestSequence;
            MaxSummonsPerOwner = maxSummonsPerOwner;
        }

        public RuntimeId OwnerRuntimeId { get; }
        public string EnemyDefinitionId { get; }
        public int RecursionDepth { get; }
        public long RequestSequence { get; }
        public int MaxSummonsPerOwner { get; }
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
        public int MaxTotalSummons => maxTotalSummons;
        public int MaxRecursionDepth => maxRecursionDepth;

        public DomainResult TryReserve(FpgSummonRequest request)
        {
            if (!request.OwnerRuntimeId.IsValid
                || string.IsNullOrWhiteSpace(request.EnemyDefinitionId)
                || request.RequestSequence < 0
                || request.RecursionDepth < 0
                || request.MaxSummonsPerOwner < 0
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

            if (request.MaxSummonsPerOwner <= 0
                || CountOwner(request.OwnerRuntimeId) >= request.MaxSummonsPerOwner
                || count >= maxTotalSummons
                || count >= requests.Length)
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
    }
}

