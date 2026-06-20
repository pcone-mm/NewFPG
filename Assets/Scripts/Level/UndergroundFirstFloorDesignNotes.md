# Underground First Floor Level Flow

## Hades II Structure Mapping

- Route: a run path with its own region order, reward economy, enemy ecology, and pacing.
- Region: a themed segment with unique encounter rules, enemies, rewards, NPCs, and a boss endpoint.
- Chamber: the smallest scheduled unit. A room owns pre-combat choice, encounter, reward, and exits.
- Door: the next-room preview. It exposes reward pool and risk before the player commits.
- Encounter: the room content. It can be combat, blessing, story event, shop, elite fight, boss, or post-clear add-on.

## Reward Pool Rules

- MajorFind: run-power choices such as blessings, weapon changes, stat growth, and build upgrades.
- MinorFind: long-term resources and lower-risk economic rewards.
- SpecialDoor: shop, NPC, elite, boss, rest, cost/risk doors. These are structural beats rather than ordinary rerolls.
- PostClearAddOn: optional content after clearing, such as timed chests, wells, gathering, or cost gates.

## First Floor Rule

The first floor does not spawn enemies immediately on room entry. The flow is:

1. Enter a room.
2. Show a blessing, event, risk, or reward choice.
3. Apply the selected choice.
4. Switch to battle camera.
5. Spawn enemies.
6. Player clicks the on-screen weapon to fire projectiles.
7. Enemies take hit feedback and die.
8. Switch back to explore camera.
9. Show next-room door choices.

## Current Prototype Rooms

- b1_entry_combat: fixed opening blessing choice, then one fish enemy.
- b1_blessing: MajorFind blessing choice, then one fish enemy.
- b1_story_event: SpecialDoor event choice, then one fish enemy.
- b1_cross_combat: MinorFind choice, then two fish enemies.
- b1_elite_combat: risk/reward choice, then one higher-HP fish enemy.
- b1_rest: rest endpoint placeholder.

## Script Responsibilities

- LevelContracts: serializable route, room, door, choice, reward pool, and flow state contracts.
- LevelFlowDirector: room state machine, first-floor pre-combat choice rule, camera switching, enemy spawning, and next door selection.
- LevelFlowHud: temporary runtime Canvas for status, blessing/event choices, and door choices.
- LevelWeaponProjectileShooter: bridges PrototypeFirstPersonWeaponView click attacks into projectile fire.
- LevelProjectile: simple homing/aim projectile that damages LevelCombatant targets.
- LevelCombatant: health, death, hit feedback, optional Animator Hit trigger, and fish hit sprite fallback.
- LevelFlowSceneInstaller: editor menu to install this prototype into a scene.

## Next Expansion Points

- Move room data from code into ScriptableObject route assets.
- Replace temporary Canvas HUD with the production UI layer.
- Add true blessing effect objects instead of direct damage/gold fields.
- Add post-clear add-ons such as wells, timed chests, gathering, and cost doors.
- Add per-region enemy tables, terrain modifiers, elite modifiers, and boss room definitions.
