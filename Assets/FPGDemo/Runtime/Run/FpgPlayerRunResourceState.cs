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
            int ammo)
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

            CharacterId = characterId;
            WeaponId = weaponId;
            Life = life;
            Barrier = barrier;
            Ammo = ammo;
        }

        public string CharacterId { get; }
        public string WeaponId { get; }
        public int Life { get; }
        public int Barrier { get; }
        public int Ammo { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(CharacterId)
            && !string.IsNullOrWhiteSpace(WeaponId)
            && Life > 0
            && Barrier >= 0
            && Ammo >= 0;
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
            out FpgPlayerRunResourceState state)
        {
            state = default(FpgPlayerRunResourceState);
            if (player == null || string.IsNullOrWhiteSpace(characterId)
                || string.IsNullOrWhiteSpace(weaponId) || player.Combatant.IsDead)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            CombatantResourceSnapshot combatant =
                player.Combatant.CaptureResources();
            try
            {
                state = new FpgPlayerRunResourceState(
                    characterId,
                    weaponId,
                    combatant.Life,
                    0,
                    player.Weapon.Magazine.Ammo);
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
                || state.Ammo > player.Weapon.Magazine.Capacity)
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            if (player.Weapon.State != WeaponState.Ready
                || player.Exposure.State != PlayerExposureState.Exposed)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            CombatantResourceSnapshot combatant =
                new CombatantResourceSnapshot(
                    player.RuntimeId,
                    state.Life,
                    0,
                    player.Combatant.MaxBreak);
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
