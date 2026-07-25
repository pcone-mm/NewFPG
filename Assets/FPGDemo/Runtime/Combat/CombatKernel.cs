using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public enum ImpactPhasePriority
    {
        PlayerProjectileIntercept = 0,
        PlayerCombatantHit = 1,
        EnemyImpact = 2,
        Expiration = 3
    }

    public readonly struct QueuedImpact
    {
        public QueuedImpact(
            ImpactIntent intent,
            ImpactPhasePriority priority,
            RuntimeId stableOrderId,
            long skillExecutionId = 0L,
            int gameplayEventId = 0)
        {
            if (skillExecutionId < 0L
                || gameplayEventId < 0
                || (skillExecutionId > 0L) != (gameplayEventId > 0))
            {
                throw new ArgumentException(
                    "Queued impact skill correlation requires both IDs.");
            }

            Intent = intent;
            Priority = priority;
            StableOrderId = stableOrderId;
            SkillExecutionId = skillExecutionId;
            GameplayEventId = gameplayEventId;
        }

        public ImpactIntent Intent { get; }
        public ImpactPhasePriority Priority { get; }
        public RuntimeId StableOrderId { get; }
        public long SkillExecutionId { get; }
        public int GameplayEventId { get; }
        public bool HasSkillCorrelation => SkillExecutionId > 0L;
    }
    public sealed class ImpactQueue
    {
        private readonly QueuedImpact[] entries;
        private int count;

        public ImpactQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new QueuedImpact[capacity];
        }

        public int Count => count;

        public int Capacity => entries.Length;

        public DomainResult TryEnqueue(
            ImpactIntent intent,
            ImpactPhasePriority priority,
            RuntimeId stableOrderId,
            long skillExecutionId = 0L,
            int gameplayEventId = 0)
        {
            if (!stableOrderId.IsValid
                || skillExecutionId < 0L
                || gameplayEventId < 0
                || (skillExecutionId > 0L) != (gameplayEventId > 0))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (count >= entries.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            entries[count++] = new QueuedImpact(
                intent,
                priority,
                stableOrderId,
                skillExecutionId,
                gameplayEventId);
            return DomainResult.Success;
        }

        public int DrainDue(TickIndex currentTick, QueuedImpact[] output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int outputCount = 0;
            int remainingCount = 0;
            for (int index = 0; index < count; index++)
            {
                QueuedImpact entry = entries[index];
                if (entry.Intent.ImpactTick <= currentTick)
                {
                    if (outputCount >= output.Length)
                    {
                        throw new InvalidOperationException("Impact drain buffer is too small.");
                    }

                    output[outputCount++] = entry;
                }
                else
                {
                    entries[remainingCount++] = entry;
                }
            }

            count = remainingCount;
            InsertionSort(output, outputCount);
            return outputCount;
        }

        public void Clear()
        {
            count = 0;
        }

        private static void InsertionSort(QueuedImpact[] values, int length)
        {
            for (int index = 1; index < length; index++)
            {
                QueuedImpact candidate = values[index];
                int destination = index - 1;
                while (destination >= 0 && Compare(candidate, values[destination]) < 0)
                {
                    values[destination + 1] = values[destination];
                    destination--;
                }

                values[destination + 1] = candidate;
            }
        }

        private static int Compare(QueuedImpact left, QueuedImpact right)
        {
            int tickCompare = left.Intent.ImpactTick.CompareTo(right.Intent.ImpactTick);
            if (tickCompare != 0)
            {
                return tickCompare;
            }

            int priorityCompare = left.Priority.CompareTo(right.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            int runtimeCompare = left.StableOrderId.CompareTo(right.StableOrderId);
            if (runtimeCompare != 0)
            {
                return runtimeCompare;
            }

            return left.Intent.ImpactId.CompareTo(right.Intent.ImpactId);
        }
    }

    public sealed class CombatKernel : IDisposable
    {
        private bool disposed;

        public CombatKernel(
            int projectileBudgetCapacity,
            int impactCapacity = 256,
            int shotTargetCapacity = 256,
            int impactQueueCapacity = 128,
            int traceCapacity = CombatTrace.DefaultCapacity,
            int projectileReservationCapacity = 32)
        {
            ImpactLedger = new ImpactLedger(impactCapacity);
            ShotTargetLedger = new ShotTargetLedger(shotTargetCapacity);
            DamageResolver = new DamageResolver(ImpactLedger);
            ProjectileBudget = new ProjectileBudget(
                projectileBudgetCapacity,
                projectileReservationCapacity);
            ImpactQueue = new ImpactQueue(impactQueueCapacity);
            Trace = new CombatTrace(traceCapacity);
        }

        public ImpactLedger ImpactLedger { get; }
        public ShotTargetLedger ShotTargetLedger { get; }
        public DamageResolver DamageResolver { get; }
        public ProjectileBudget ProjectileBudget { get; }
        public ImpactQueue ImpactQueue { get; }
        public CombatTrace Trace { get; }
        public bool IsDisposed => disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            ImpactQueue.Clear();
            ImpactLedger.Clear();
            ShotTargetLedger.Clear();
            ProjectileBudget.CancelAll();
            disposed = true;
        }
    }
}
