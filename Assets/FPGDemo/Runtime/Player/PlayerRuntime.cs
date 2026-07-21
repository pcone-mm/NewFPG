using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;

namespace FPG.Demo.Player
{
    public sealed class PlayerRuntime
    {
        public PlayerRuntime(
            CombatantState combatant,
            ExposureRuntime exposure,
            WeaponRuntime weapon)
        {
            Combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            Exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));

            if (combatant.Kind != CombatantKind.Player)
            {
                throw new ArgumentException("PlayerRuntime requires a player CombatantState.", nameof(combatant));
            }
        }

        public RuntimeId RuntimeId => Combatant.RuntimeId;
        public CombatantState Combatant { get; }
        public ExposureRuntime Exposure { get; }
        public WeaponRuntime Weapon { get; }

        public void Disable()
        {
            Exposure.Disable();
            Weapon.Disable();
        }
    }
}
