using System;
using FPG.Demo.Core;

namespace FPG.Demo.Combat
{
    public sealed class ImpactLedger
    {
        private readonly long[] consumedIds;
        private int count;

        public ImpactLedger(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            consumedIds = new long[capacity];
        }

        public int Count => count;

        public int Capacity => consumedIds.Length;

        public int RemainingCapacity => consumedIds.Length - count;

        public DomainResult TryConsume(ImpactId impactId)
        {
            if (!impactId.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            for (int index = 0; index < count; index++)
            {
                if (consumedIds[index] == impactId.Value)
                {
                    return DomainResult.Rejected(RejectReason.DuplicateImpact);
                }
            }

            if (count >= consumedIds.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            consumedIds[count++] = impactId.Value;
            return DomainResult.Success;
        }

        public void Clear()
        {
            Array.Clear(consumedIds, 0, count);
            count = 0;
        }
    }

    public sealed class ShotTargetLedger
    {
        private readonly ShotTargetEntry[] entries;
        private int count;

        public ShotTargetLedger(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            entries = new ShotTargetEntry[capacity];
        }

        public int Count => count;

        public int Capacity => entries.Length;

        public int RemainingCapacity => entries.Length - count;

        public DomainResult TryMark(ShotId shotId, RuntimeId targetId)
        {
            DomainResult validation = ValidateCanMark(shotId, targetId);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            entries[count++] = new ShotTargetEntry(shotId, targetId);
            return DomainResult.Success;
        }

        public DomainResult ValidateCanMark(ShotId shotId, RuntimeId targetId)
        {
            if (!shotId.IsValid || !targetId.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            ShotTargetEntry candidate = new ShotTargetEntry(shotId, targetId);
            for (int index = 0; index < count; index++)
            {
                if (entries[index].Equals(candidate))
                {
                    return DomainResult.Rejected(RejectReason.DuplicateImpact);
                }
            }

            if (count >= entries.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            return DomainResult.Success;
        }

        public void Clear()
        {
            Array.Clear(entries, 0, count);
            count = 0;
        }

        private readonly struct ShotTargetEntry : IEquatable<ShotTargetEntry>
        {
            public ShotTargetEntry(ShotId shotId, RuntimeId targetId)
            {
                ShotId = shotId;
                TargetId = targetId;
            }

            public ShotId ShotId { get; }
            public RuntimeId TargetId { get; }

            public bool Equals(ShotTargetEntry other)
            {
                return ShotId == other.ShotId && TargetId == other.TargetId;
            }

            public override bool Equals(object obj)
            {
                return obj is ShotTargetEntry other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ShotId.GetHashCode() * 397) ^ TargetId.GetHashCode();
                }
            }
        }
    }

    public sealed class DamageResolver
    {
        private readonly ImpactLedger impactLedger;

        public DamageResolver(ImpactLedger impactLedger)
        {
            this.impactLedger = impactLedger ?? throw new ArgumentNullException(nameof(impactLedger));
        }

        public ImpactLedger ImpactLedger => impactLedger;

        public ImpactResolution ResolveCombatant(
            ImpactIntent intent,
            CombatantState target,
            DefenseSnapshot defense,
            bool breakDamageEnabled)
        {
            DomainResult ledgerResult = impactLedger.TryConsume(intent.ImpactId);
            if (!ledgerResult.IsSuccess)
            {
                return ImpactResolution.Rejected(ledgerResult.RejectReason);
            }

            if (!intent.AttackId.IsValid
                || !intent.SourceId.IsValid
                || !intent.TargetId.IsValid
                || !intent.ImpactTick.IsValid)
            {
                return ImpactResolution.Rejected(RejectReason.InvalidState);
            }

            if (target == null || target.RuntimeId != intent.TargetId || target.IsDead)
            {
                return ImpactResolution.Rejected(RejectReason.InvalidTarget);
            }

            int damage = intent.DamageSpec.BaseDamage;
            int breakDamage = intent.DamageSpec.BreakDamage;
            if (intent.HitPart == HitPart.Weakpoint)
            {
                damage = RoundBasisPoints(damage, intent.DamageSpec.WeakpointDamageMultiplierBasisPoints);
                breakDamage = RoundBasisPoints(breakDamage, intent.DamageSpec.WeakpointBreakMultiplierBasisPoints);
            }

            DamageChannel channel;
            bool perfectRetract = false;
            if (target.Kind == CombatantKind.Enemy)
            {
                channel = DamageChannel.Life;
            }
            else if (defense.Exposure == ExposureMode.Withdrawn && target.Barrier > 0)
            {
                channel = DamageChannel.Barrier;
                perfectRetract = IsPerfectRetract(intent.ImpactTick, defense);
                if (perfectRetract)
                {
                    damage = RoundBasisPoints(damage, defense.PerfectBarrierMultiplierBasisPoints);
                }
            }
            else
            {
                channel = DamageChannel.Life;
            }

            int valueBefore = channel == DamageChannel.Barrier ? target.Barrier : target.Life;
            bool wasAlive = !target.IsDead;
            bool hadBarrier = target.Barrier > 0;
            int breakBefore = target.Break;

            int appliedDamage = channel == DamageChannel.Barrier
                ? target.ApplyBarrierDamage(damage)
                : target.ApplyLifeDamage(damage);

            int appliedBreak = breakDamageEnabled && target.MaxBreak > 0
                ? target.ApplyBreakDamage(breakDamage)
                : 0;

            bool barrierBroken = channel == DamageChannel.Barrier && hadBarrier && target.Barrier == 0;
            bool death = wasAlive && target.IsDead;
            bool breakTriggered = !death && breakBefore > 0 && target.Break == 0;
            int valueAfter = channel == DamageChannel.Barrier ? target.Barrier : target.Life;

            DamagePacket packet = new DamagePacket(
                intent.ImpactId,
                channel,
                appliedDamage,
                appliedBreak,
                valueBefore,
                valueAfter);

            return ImpactResolution.Accepted(
                packet,
                perfectRetract,
                barrierBroken,
                breakTriggered,
                death,
                false);
        }

        public ImpactResolution ResolveProjectile(ImpactIntent intent, ProjectileRuntime projectile)
        {
            DomainResult ledgerResult = impactLedger.TryConsume(intent.ImpactId);
            if (!ledgerResult.IsSuccess)
            {
                return ImpactResolution.Rejected(ledgerResult.RejectReason);
            }

            if (!intent.AttackId.IsValid
                || !intent.SourceId.IsValid
                || !intent.TargetId.IsValid
                || !intent.ImpactTick.IsValid)
            {
                return ImpactResolution.Rejected(RejectReason.InvalidState);
            }

            if (projectile == null || projectile.RuntimeId != intent.TargetId || projectile.IsTerminal)
            {
                return ImpactResolution.Rejected(RejectReason.InvalidTarget);
            }

            if (!projectile.Definition.Interceptable)
            {
                return ImpactResolution.Rejected(RejectReason.InvalidTarget);
            }

            int damage = intent.DamageSpec.BaseDamage;
            int before = projectile.HitPoints;
            int applied = projectile.ApplyDamage(damage, intent.ImpactTick);
            bool destroyed = before > 0 && projectile.State == ProjectileState.Destroyed;

            DamagePacket packet = new DamagePacket(
                intent.ImpactId,
                DamageChannel.ProjectileHp,
                applied,
                0,
                before,
                projectile.HitPoints);

            return ImpactResolution.Accepted(packet, false, false, false, false, destroyed);
        }

        public static int RoundBasisPoints(int value, int basisPoints)
        {
            if (value <= 0 || basisPoints <= 0)
            {
                return 0;
            }

            long scaled = (long)value * basisPoints + DamageSpec.BasisPoints / 2L;
            long rounded = scaled / DamageSpec.BasisPoints;
            return rounded > int.MaxValue ? int.MaxValue : (int)rounded;
        }

        private static bool IsPerfectRetract(TickIndex impactTick, DefenseSnapshot defense)
        {
            if (!defense.WithdrawnSinceTick.IsValid || defense.PerfectWindow.Value <= 0)
            {
                return false;
            }

            TickIndex endExclusive = defense.WithdrawnSinceTick + defense.PerfectWindow;
            return impactTick >= defense.WithdrawnSinceTick && impactTick < endExclusive;
        }
    }
}
