using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Player
{
    public enum PlayerExposureState
    {
        Exposed = 0,
        Withdrawn,
        Disabled
    }

    public readonly struct ExposureRuntimeSnapshot
    {
        public ExposureRuntimeSnapshot(
            PlayerExposureState state,
            TickIndex exposureSinceTick,
            TickIndex withdrawnSinceTick)
        {
            State = state;
            ExposureSinceTick = exposureSinceTick;
            WithdrawnSinceTick = withdrawnSinceTick;
        }

        public PlayerExposureState State { get; }
        public TickIndex ExposureSinceTick { get; }
        public TickIndex WithdrawnSinceTick { get; }
    }

    public sealed class ExposureRuntime
    {
        public ExposureRuntime(PlayerExposureState initialState = PlayerExposureState.Exposed)
        {
            State = initialState;
            ExposureSinceTick = TickIndex.Invalid;
            WithdrawnSinceTick = TickIndex.Invalid;
        }

        public PlayerExposureState State { get; private set; }
        public TickIndex ExposureSinceTick { get; private set; }
        public TickIndex WithdrawnSinceTick { get; private set; }
        public bool IsExposed => State == PlayerExposureState.Exposed;

        public DomainResult ApplyAim(
            bool aimHeld,
            TickIndex currentTick,
            bool barrierLocked,
            out bool changed)
        {
            return ApplyCombatPosture(aimHeld, currentTick, barrierLocked, out changed);
        }

        /// <summary>
        /// Applies the player's authored combat posture. Aiming and either
        /// attack form request exposure, while an idle player withdraws behind
        /// the barrier. Keeping this intent separate from the physical input
        /// name prevents aim from becoming an accidental prerequisite for
        /// primary or secondary attacks.
        /// </summary>
        public DomainResult ApplyCombatPosture(
            bool shouldExpose,
            TickIndex currentTick,
            bool barrierLocked,
            out bool changed)
        {
            changed = false;
            if (State == PlayerExposureState.Disabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!shouldExpose && barrierLocked)
            {
                ForceExposed(currentTick, out changed);
                return DomainResult.Rejected(RejectReason.BarrierLocked);
            }

            PlayerExposureState requested = shouldExpose
                ? PlayerExposureState.Exposed
                : PlayerExposureState.Withdrawn;

            if (State == requested)
            {
                return DomainResult.Success;
            }

            State = requested;
            changed = true;
            if (requested == PlayerExposureState.Exposed)
            {
                ExposureSinceTick = currentTick;
            }
            else
            {
                WithdrawnSinceTick = currentTick;
            }

            return DomainResult.Success;
        }

        /// <summary>
        /// Reload always returns the player to cover. This posture is valid
        /// even while the barrier value is depleted; damage routing still
        /// decides whether an incoming hit reaches Barrier or Life.
        /// </summary>
        public DomainResult ApplyReloadPosture(TickIndex currentTick, out bool changed)
        {
            changed = false;
            if (State == PlayerExposureState.Disabled)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (State == PlayerExposureState.Withdrawn)
            {
                return DomainResult.Success;
            }

            State = PlayerExposureState.Withdrawn;
            WithdrawnSinceTick = currentTick;
            changed = true;
            return DomainResult.Success;
        }

        public void ForceExposed(TickIndex currentTick, out bool changed)
        {
            changed = State != PlayerExposureState.Exposed;
            State = PlayerExposureState.Exposed;
            if (changed || !ExposureSinceTick.IsValid)
            {
                ExposureSinceTick = currentTick;
            }
        }

        public void Disable()
        {
            State = PlayerExposureState.Disabled;
        }

        public ExposureRuntimeSnapshot CaptureRoomSnapshot()
        {
            return new ExposureRuntimeSnapshot(
                State,
                ExposureSinceTick,
                WithdrawnSinceTick);
        }

        public DomainResult RestoreRoomSnapshot(in ExposureRuntimeSnapshot snapshot)
        {
            if (!System.Enum.IsDefined(typeof(PlayerExposureState), snapshot.State))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            State = snapshot.State;
            ExposureSinceTick = snapshot.ExposureSinceTick;
            WithdrawnSinceTick = snapshot.WithdrawnSinceTick;
            return DomainResult.Success;
        }

        public DefenseSnapshot CreateDefenseSnapshot(
            TickDuration perfectWindow,
            int perfectBarrierMultiplierBasisPoints,
            TickDuration barrierLockDuration,
            int barrierRestoreBasisPoints)
        {
            ExposureMode exposure = State == PlayerExposureState.Withdrawn
                ? ExposureMode.Withdrawn
                : ExposureMode.Exposed;

            return new DefenseSnapshot(
                exposure,
                WithdrawnSinceTick,
                perfectWindow,
                perfectBarrierMultiplierBasisPoints,
                barrierLockDuration,
                barrierRestoreBasisPoints);
        }
    }
}


