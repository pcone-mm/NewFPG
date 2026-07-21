using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Durable presentation state for the formal player. The struct contains
    /// only read-model values; no presentation component can mutate combat
    /// state through it.
    /// </summary>
    public readonly struct FpgFormalPlayerPresentationSnapshot
    {
        public FpgFormalPlayerPresentationSnapshot(
            TickIndex tick,
            RuntimeId playerRuntimeId,
            FpgEncounterPhase encounterPhase,
            bool paused,
            int life,
            int maxLife,
            int barrier,
            int maxBarrier,
            int ammo,
            int magazineCapacity,
            PlayerExposureState exposureState,
            WeaponState weaponState)
        {
            Tick = tick;
            PlayerRuntimeId = playerRuntimeId;
            EncounterPhase = encounterPhase;
            IsPaused = paused;
            Life = life;
            MaxLife = maxLife;
            Barrier = barrier;
            MaxBarrier = maxBarrier;
            Ammo = ammo;
            MagazineCapacity = magazineCapacity;
            ExposureState = exposureState;
            WeaponState = weaponState;
        }

        public static FpgFormalPlayerPresentationSnapshot Unavailable =>
            new FpgFormalPlayerPresentationSnapshot(
                TickIndex.Invalid,
                RuntimeId.Invalid,
                FpgEncounterPhase.None,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                PlayerExposureState.Withdrawn,
                WeaponState.Disabled);

        public TickIndex Tick { get; }
        public RuntimeId PlayerRuntimeId { get; }
        public FpgEncounterPhase EncounterPhase { get; }
        public bool IsPaused { get; }
        public int Life { get; }
        public int MaxLife { get; }
        public int Barrier { get; }
        public int MaxBarrier { get; }
        public int Ammo { get; }
        public int MagazineCapacity { get; }
        public PlayerExposureState ExposureState { get; }
        public WeaponState WeaponState { get; }

        public bool IsValid => PlayerRuntimeId.IsValid
            && MaxLife > 0
            && MaxBarrier > 0
            && MagazineCapacity > 0;

        public bool IsDead => IsValid && Life <= 0;

        public bool IsCombatActive => IsValid
            && !IsPaused
            && EncounterPhase != FpgEncounterPhase.None
            && EncounterPhase != FpgEncounterPhase.Preparing
            && EncounterPhase != FpgEncounterPhase.Cleared
            && EncounterPhase != FpgEncounterPhase.Failed
            && EncounterPhase != FpgEncounterPhase.Faulted
            && EncounterPhase != FpgEncounterPhase.Disposed;

        public FpgFormalPlayerPresentationState PresentationState
        {
            get
            {
                if (!IsValid)
                {
                    return FpgFormalPlayerPresentationState.Unavailable;
                }

                if (IsDead)
                {
                    return FpgFormalPlayerPresentationState.Defeat;
                }

                if (EncounterPhase == FpgEncounterPhase.Cleared)
                {
                    return FpgFormalPlayerPresentationState.Victory;
                }

                if (EncounterPhase == FpgEncounterPhase.Failed
                    || EncounterPhase == FpgEncounterPhase.Faulted
                    || EncounterPhase == FpgEncounterPhase.Disposed)
                {
                    return FpgFormalPlayerPresentationState.Faulted;
                }

                if (IsPaused || EncounterPhase == FpgEncounterPhase.Paused)
                {
                    return FpgFormalPlayerPresentationState.Paused;
                }

                if (EncounterPhase == FpgEncounterPhase.None
                    || EncounterPhase == FpgEncounterPhase.Preparing)
                {
                    return FpgFormalPlayerPresentationState.Preparing;
                }

                return FpgFormalPlayerPresentationState.Active;
            }
        }
    }

    public enum FpgFormalPlayerPresentationState
    {
        Unavailable = 0,
        Preparing,
        Active,
        Paused,
        Victory,
        Defeat,
        Faulted
    }

    public enum FpgFormalPlayerActionType
    {
        PrimaryReleaseCommitted = 0,
        SecondaryChargeStarted,
        SecondaryChargeCanceled,
        SecondaryReleaseCommitted,
        ReloadStarted,
        ReloadCompleted
    }

    /// <summary>
    /// A player action is raised only after WeaponRuntime.ProcessFrame has
    /// accepted the containing input frame. Presentation consumers may safely
    /// queue this event without feeding anything back into the simulation.
    /// </summary>
    public readonly struct FpgFormalPlayerActionEvent
    {
        public FpgFormalPlayerActionEvent(
            long sequence,
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter)
        {
            Sequence = sequence;
            Tick = tick;
            Type = type;
            ReleaseKind = releaseKind;
            AttackId = attackId;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            AmmoBefore = ammoBefore;
            AmmoAfter = ammoAfter;
        }

        public long Sequence { get; }
        public TickIndex Tick { get; }
        public FpgFormalPlayerActionType Type { get; }
        public WeaponReleaseKind ReleaseKind { get; }
        public AttackId AttackId { get; }
        public WeaponState StateBefore { get; }
        public WeaponState StateAfter { get; }
        public int AmmoBefore { get; }
        public int AmmoAfter { get; }
    }

    public interface IFpgFormalPlayerPresentationSource
    {
        bool TryGetPlayerPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot snapshot);
    }

    /// <summary>
    /// Runtime-only source shared by the formal tick driver and presentation
    /// bridge. It intentionally has no Unity references, so it can be replaced
    /// by a deterministic test source without scene lookups.
    /// </summary>
    public sealed class FpgFormalPlayerPresentationSource :
        IFpgFormalPlayerPresentationSource
    {
        private FpgFormalPlayerPresentationSnapshot snapshot =
            FpgFormalPlayerPresentationSnapshot.Unavailable;
        private long nextActionSequence;

        public event Action<FpgFormalPlayerActionEvent> ActionCommitted;

        public bool HasSnapshot => snapshot.IsValid;

        public bool TryGetPlayerPresentationSnapshot(
            out FpgFormalPlayerPresentationSnapshot result)
        {
            result = snapshot;
            return result.IsValid;
        }

        public void PublishSnapshot(in FpgFormalPlayerPresentationSnapshot next)
        {
            snapshot = next;
        }

        public void PublishAction(
            TickIndex tick,
            FpgFormalPlayerActionType type,
            WeaponReleaseKind releaseKind,
            AttackId attackId,
            WeaponState stateBefore,
            WeaponState stateAfter,
            int ammoBefore,
            int ammoAfter)
        {
            long sequence = nextActionSequence == long.MaxValue
                ? 1L
                : nextActionSequence + 1L;
            nextActionSequence = sequence;
            FpgFormalPlayerActionEvent action = new FpgFormalPlayerActionEvent(
                sequence,
                tick,
                type,
                releaseKind,
                attackId,
                stateBefore,
                stateAfter,
                ammoBefore,
                ammoAfter);

            // Presentation must never turn an animation/UI exception into a
            // deterministic combat failure.
            try
            {
                ActionCommitted?.Invoke(action);
            }
            catch (Exception)
            {
            }
        }

        public void Clear()
        {
            snapshot = FpgFormalPlayerPresentationSnapshot.Unavailable;
            nextActionSequence = 0L;
        }
    }
}


