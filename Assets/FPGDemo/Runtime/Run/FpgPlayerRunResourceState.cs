using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Portable player resources captured at a room boundary. Runtime ids and
    /// transient weapon/exposure state deliberately do not cross that boundary.
    /// </summary>
    public readonly struct FpgPlayerRunResourceState
    {
        public FpgPlayerRunResourceState(
            string characterId,
            string weaponId,
            int life,
            int barrier,
            int ammo,
            int remainingBarrierLockTicks,
            int barrierRestoreBasisPoints)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                throw new ArgumentException(
                    "Character id is required.",
                    nameof(characterId));
            }

            if (string.IsNullOrWhiteSpace(weaponId))
            {
                throw new ArgumentException(
                    "Weapon id is required.",
                    nameof(weaponId));
            }

            if (life <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(life));
            }

            if (barrier < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(barrier));
            }

            if (ammo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammo));
            }

            if (remainingBarrierLockTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingBarrierLockTicks));
            }

            if (barrierRestoreBasisPoints < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(barrierRestoreBasisPoints));
            }

            if (remainingBarrierLockTicks > 0 && barrier != 0)
            {
                throw new ArgumentException(
                    "A barrier lock can only be carried while barrier is depleted.",
                    nameof(barrier));
            }

            CharacterId = characterId;
            WeaponId = weaponId;
            Life = life;
            Barrier = barrier;
            Ammo = ammo;
            RemainingBarrierLockTicks = remainingBarrierLockTicks;
            BarrierRestoreBasisPoints = barrierRestoreBasisPoints;
        }

        public string CharacterId { get; }
        public string WeaponId { get; }
        public int Life { get; }
        public int Barrier { get; }
        public int Ammo { get; }
        public int RemainingBarrierLockTicks { get; }
        public int BarrierRestoreBasisPoints { get; }
        public bool HasBarrierLock => RemainingBarrierLockTicks > 0;

        public bool IsValid => !string.IsNullOrWhiteSpace(CharacterId)
            && !string.IsNullOrWhiteSpace(WeaponId)
            && Life > 0
            && Barrier >= 0
            && Ammo >= 0
            && RemainingBarrierLockTicks >= 0
            && BarrierRestoreBasisPoints >= 0
            && (!HasBarrierLock || Barrier == 0);
    }

    /// <summary>
    /// Converts between one live player runtime and its run-scoped room-boundary
    /// resources. Import is intentionally valid only for a fresh player runtime.
    /// </summary>
    public static class FpgPlayerRunResourceTransfer
    {
        public static DomainResult TryCapture(
            PlayerRuntime player,
            string characterId,
            string weaponId,
            TickIndex currentTick,
            out FpgPlayerRunResourceState state)
        {
            state = default(FpgPlayerRunResourceState);
            if (player == null || string.IsNullOrWhiteSpace(characterId)
                || string.IsNullOrWhiteSpace(weaponId) || !currentTick.IsValid
                || player.Combatant.IsDead)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            CombatantResourceSnapshot combatant =
                player.Combatant.CaptureResources();
            int barrier = combatant.Barrier;
            int remainingBarrierLockTicks = 0;
            if (combatant.BarrierLockUntilTick.IsValid)
            {
                long remaining = combatant.BarrierLockUntilTick - currentTick;
                if (remaining > int.MaxValue)
                {
                    return DomainResult.Rejected(RejectReason.BufferCapacity);
                }

                if (remaining > 0L)
                {
                    remainingBarrierLockTicks = (int)remaining;
                }
                else
                {
                    barrier = Math.Min(
                        player.Combatant.MaxBarrier,
                        DamageResolver.RoundBasisPoints(
                            player.Combatant.MaxBarrier,
                            combatant.BarrierRestoreBasisPoints));
                }
            }

            try
            {
                state = new FpgPlayerRunResourceState(
                    characterId,
                    weaponId,
                    combatant.Life,
                    barrier,
                    player.Weapon.Magazine.Ammo,
                    remainingBarrierLockTicks,
                    combatant.BarrierRestoreBasisPoints);
                return DomainResult.Success;
            }
            catch (ArgumentException)
            {
                state = default(FpgPlayerRunResourceState);
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }
        }

        public static DomainResult TryRestoreRoomEntry(
            PlayerRuntime player,
            string expectedCharacterId,
            string expectedWeaponId,
            in FpgPlayerRunResourceState state)
        {
            if (player == null || !state.IsValid
                || !string.Equals(
                    state.CharacterId,
                    expectedCharacterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.WeaponId,
                    expectedWeaponId,
                    StringComparison.Ordinal))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (state.Life > player.Combatant.MaxLife
                || state.Barrier > player.Combatant.MaxBarrier
                || state.Ammo > player.Weapon.Magazine.Capacity)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (player.Weapon.State != WeaponState.Ready
                || player.Exposure.State != PlayerExposureState.Exposed)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            TickIndex barrierLockUntilTick = state.HasBarrierLock
                ? new TickIndex(state.RemainingBarrierLockTicks)
                : TickIndex.Invalid;
            CombatantResourceSnapshot combatant =
                new CombatantResourceSnapshot(
                    player.RuntimeId,
                    state.Life,
                    state.Barrier,
                    player.Combatant.MaxBreak,
                    barrierLockUntilTick,
                    state.BarrierRestoreBasisPoints);
            DomainResult restored = player.Combatant.RestoreResources(combatant);
            if (!restored.IsSuccess)
            {
                return restored;
            }

            DomainResult ammo = player.Weapon.Magazine.RestoreAmmo(state.Ammo);
            if (!ammo.IsSuccess)
            {
                return ammo;
            }

            player.Exposure.ForceExposed(new TickIndex(0L), out _);
            return DomainResult.Success;
        }
    }
}
