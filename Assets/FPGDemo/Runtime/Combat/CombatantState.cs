using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public readonly struct CombatantResourceSnapshot
    {
        public CombatantResourceSnapshot(
            RuntimeId runtimeId,
            int life,
            int barrier,
            int breakValue,
            TickIndex barrierLockUntilTick,
            int barrierRestoreBasisPoints)
        {
            RuntimeId = runtimeId;
            Life = life;
            Barrier = barrier;
            Break = breakValue;
            BarrierLockUntilTick = barrierLockUntilTick;
            BarrierRestoreBasisPoints = barrierRestoreBasisPoints;
        }

        public RuntimeId RuntimeId { get; }
        public int Life { get; }
        public int Barrier { get; }
        public int Break { get; }
        public TickIndex BarrierLockUntilTick { get; }
        public int BarrierRestoreBasisPoints { get; }
    }

    public sealed class CombatantState
    {
        private TickIndex barrierLockUntilTick = TickIndex.Invalid;
        private int barrierRestoreBasisPoints = DamageSpec.BasisPoints;

        public CombatantState(
            RuntimeId runtimeId,
            CombatantKind kind,
            int maxLife,
            int maxBarrier,
            int maxBreak)
        {
            if (!runtimeId.IsValid)
            {
                throw new ArgumentException("RuntimeId must be valid.", nameof(runtimeId));
            }

            if (maxLife <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLife));
            }

            if (maxBarrier < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBarrier));
            }

            if (maxBreak < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBreak));
            }

            RuntimeId = runtimeId;
            Kind = kind;
            MaxLife = maxLife;
            MaxBarrier = maxBarrier;
            MaxBreak = maxBreak;
            Life = maxLife;
            Barrier = maxBarrier;
            Break = maxBreak;
        }

        public RuntimeId RuntimeId { get; }
        public CombatantKind Kind { get; }
        public int MaxLife { get; }
        public int MaxBarrier { get; }
        public int MaxBreak { get; }
        public int Life { get; private set; }
        public int Barrier { get; private set; }
        public int Break { get; private set; }
        public bool IsDead => Life <= 0;
        public TickIndex BarrierLockUntilTick => barrierLockUntilTick;

        public bool IsBarrierLocked(TickIndex currentTick)
        {
            return barrierLockUntilTick.IsValid && currentTick < barrierLockUntilTick;
        }

        public bool TryRestoreBarrier(TickIndex currentTick)
        {
            if (!barrierLockUntilTick.IsValid || currentTick < barrierLockUntilTick)
            {
                return false;
            }

            Barrier = Math.Min(
                MaxBarrier,
                DamageResolver.RoundBasisPoints(MaxBarrier, barrierRestoreBasisPoints));
            barrierLockUntilTick = TickIndex.Invalid;
            return true;
        }

        public void RestoreBreakFull()
        {
            Break = MaxBreak;
        }

        internal int ApplyLifeDamage(int amount)
        {
            int applied = Math.Min(Math.Max(amount, 0), Life);
            Life -= applied;
            return applied;
        }

        /// <summary>
        /// Ends this combatant's lifetime without pretending that the damage
        /// came from a player attack. Encounter lifecycle transitions (for
        /// example an egg hatching into a butterfly) use this operation so the
        /// old runtime can emit a real Death event while a separate runtime is
        /// spawned in the same deterministic tick.
        /// </summary>
        public int ForceDeath()
        {
            int previousLife = Life;
            Life = 0;
            return previousLife;
        }

        internal int ApplyBarrierDamage(int amount)
        {
            int applied = Math.Min(Math.Max(amount, 0), Barrier);
            Barrier -= applied;
            return applied;
        }

        internal int ApplyBreakDamage(int amount)
        {
            int applied = Math.Min(Math.Max(amount, 0), Break);
            Break -= applied;
            return applied;
        }

        public CombatantResourceSnapshot CaptureResources()
        {
            return new CombatantResourceSnapshot(
                RuntimeId,
                Life,
                Barrier,
                Break,
                barrierLockUntilTick,
                barrierRestoreBasisPoints);
        }

        public DomainResult RestoreResources(in CombatantResourceSnapshot snapshot)
        {
            if (snapshot.RuntimeId != RuntimeId
                || snapshot.Life < 0 || snapshot.Life > MaxLife
                || snapshot.Barrier < 0 || snapshot.Barrier > MaxBarrier
                || snapshot.Break < 0 || snapshot.Break > MaxBreak
                || snapshot.BarrierRestoreBasisPoints < 0)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            Life = snapshot.Life;
            Barrier = snapshot.Barrier;
            Break = snapshot.Break;
            barrierLockUntilTick = snapshot.BarrierLockUntilTick;
            barrierRestoreBasisPoints = snapshot.BarrierRestoreBasisPoints;
            return DomainResult.Success;
        }

        internal void BeginBarrierLock(
            TickIndex currentTick,
            TickDuration lockDuration,
            int restoreBasisPoints)
        {
            Barrier = 0;
            barrierLockUntilTick = currentTick + lockDuration;
            barrierRestoreBasisPoints = Math.Max(0, restoreBasisPoints);
        }
    }
}


