using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Enemy
{
    public sealed class EnemyRuntime
    {
        private readonly ThreatRuntime[] threats;
        private readonly TickDuration groggyDuration;
        private int threatCount;

        public EnemyRuntime(
            CombatantState combatant,
            TickDuration groggyDuration,
            int threatCapacity = 8)
        {
            Combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            if (combatant.Kind != CombatantKind.Enemy)
            {
                throw new ArgumentException("EnemyRuntime requires an enemy CombatantState.", nameof(combatant));
            }

            if (groggyDuration.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groggyDuration));
            }

            if (threatCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(threatCapacity));
            }

            this.groggyDuration = groggyDuration;
            threats = new ThreatRuntime[threatCapacity];
            Groggy = new GroggyRuntime();
            ControlState = EnemyControlState.Active;
        }

        public RuntimeId RuntimeId => Combatant.RuntimeId;
        public CombatantState Combatant { get; }
        public EnemyControlState ControlState { get; private set; }
        public GroggyRuntime Groggy { get; }
        public int ThreatCount => threatCount;

        public DomainResult TryAddThreat(ThreatRuntime threat)
        {
            return TryAddThreat(threat, out int ignoredIndex);
        }

        public DomainResult TryAddThreat(ThreatRuntime threat, out int threatIndex)
        {
            threatIndex = -1;
            if (threat == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            DomainResult capacity = ValidateCanAddThreat();
            if (!capacity.IsSuccess)
            {
                return capacity;
            }

            for (int index = 0; index < threatCount; index++)
            {
                ThreatRuntime existing = threats[index];
                if (existing == null || existing.IsTerminal)
                {
                    threats[index] = threat;
                    threatIndex = index;
                    return DomainResult.Success;
                }
            }

            if (threatCount >= threats.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            threatIndex = threatCount;
            threats[threatCount++] = threat;
            return DomainResult.Success;
        }

        public DomainResult ValidateCanAddThreat()
        {
            if (ControlState != EnemyControlState.Active)
            {
                return DomainResult.Rejected(
                    ControlState == EnemyControlState.Dead
                        ? RejectReason.OwnerInterrupted
                        : RejectReason.OwnerGroggy);
            }

            for (int index = 0; index < threatCount; index++)
            {
                ThreatRuntime existing = threats[index];
                if (existing == null || existing.IsTerminal)
                {
                    return DomainResult.Success;
                }
            }

            return threatCount < threats.Length
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.BufferCapacity);
        }

        public ThreatRuntime GetThreat(int index)
        {
            if (index < 0 || index >= threatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return threats[index];
        }

        public int EnterGroggy(TickIndex currentTick, ProjectileBudget budget)
        {
            if (budget == null || !currentTick.IsValid)
            {
                return 0;
            }

            if (ControlState == EnemyControlState.Dead)
            {
                return 0;
            }

            ControlState = EnemyControlState.Groggy;
            Groggy.Enter(currentTick, groggyDuration);

            int canceled = 0;
            bool failed = false;
            for (int index = 0; index < threatCount; index++)
            {
                ThreatRuntime threat = threats[index];
                if (threat == null || threat.HasReleased || threat.IsTerminal)
                {
                    continue;
                }

                DomainResult result = threat.TryCancelBeforeRelease(budget);
                if (result.IsSuccess)
                {
                    canceled++;
                }
                else
                {
                    failed = true;
                }
            }

            return failed ? -1 : canceled;
        }

        public bool AdvanceStartOfTick(TickIndex currentTick)
        {
            if (ControlState != EnemyControlState.Groggy || !Groggy.TryRecover(currentTick))
            {
                return false;
            }

            Combatant.RestoreBreakFull();
            ControlState = EnemyControlState.Active;
            return true;
        }

        public DomainResult MarkDead(ProjectileBudget budget)
        {
            if (ControlState == EnemyControlState.Dead)
            {
                return DomainResult.Success;
            }

            ControlState = EnemyControlState.Dead;
            DomainResult firstFailure = DomainResult.Success;
            for (int index = 0; index < threatCount; index++)
            {
                ThreatRuntime threat = threats[index];
                if (threat == null || threat.HasReleased || threat.IsTerminal)
                {
                    continue;
                }

                DomainResult canceled = threat.TryCancelBeforeRelease(budget);
                if (!canceled.IsSuccess && firstFailure.IsSuccess)
                {
                    firstFailure = canceled;
                }
            }

            return firstFailure;
        }
    }
}
