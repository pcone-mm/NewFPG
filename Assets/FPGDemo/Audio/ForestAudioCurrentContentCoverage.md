# Forest Current-Content Audio Coverage

This audit complements `ForestAudioRequirements.csv`. The requirements CSV owns
candidate/import status; this file proves whether every currently playable
consumer has a real presentation trigger. A clip in a bank with no caller does
not count as coverage.

## Status meanings

- `Covered`: approved variations are bound and targeted Unity validation passes.
- `CandidateApproval`: traced candidates exist outside Unity and are waiting for the single active user approval gate.
- `NeedsSound`: the gameplay/presentation event exists, but its audio definition is empty.
- `NeedsTrigger`: a sound group or routing API exists, but no current consumer invokes it.
- `ExcludedByDesign`: the user explicitly requested silence for the event.

## Current playable inventory

| Area | Authoritative trigger or asset | Current evidence | Status | Required completion work |
|---|---|---|---|---|
| Fei primary fire | `FPG_Fei_Primary.asset`, `event.fei.primary.attack.0`, tick 0 | Four muzzle-socket variations bound | Covered | Keep no-immediate-repeat variation and PlayMode tick/socket coverage |
| Fei primary impacts | Primary impact bundle | Base, weakpoint and environment groups bound | Covered | Preserve hit-point positioning and A/B distinction |
| Fei reload | `FPG_Fei_Reload.asset`, `event.fei.reload.commit.0`, tick 40 | `track.fei.reload.active` contains five mono mechanical-click variations bound at tick 40 to the authored reload commit | Covered | Preserve five approved sibling variations and verify no-immediate-repeat playback during PlayMode |
| Fei immediate secondary | `FPG_Fei_Secondary_Immediate.asset`, projectile tick 0 | Five approved KHRON Luminous Projectile launch variants are bound at tick 0 to `weapon.secondary.muzzle`; selected KHRON Electrified Impact candidate 01 and its reinforced derivative are bound to base and weakpoint collision audio | Covered | Launch keeps no-immediate-repeat randomization; the selected impact pair is world-positioned at the committed hit |
| Fei charged secondary | `FPG_Fei_Secondary_Charge.asset`, charge enter, release and cancel sequences | Approved Magic Ice Charging pair 01 is bound at `ChargeEnter` tick 0; all 7 release variants are bound to the committed release at `weapon.secondary.muzzle`; all 5 quieter cancel variants are bound at `OwnerRoot`; selected Magic Ice charged-impact pair 02 is bound as base and reinforced weakpoint collision audio | Covered | Preserve release randomization, hit-point positioning, base/weakpoint distinction and pause/restart/disable held-loop cleanup |
| Burstbug Fast | `FPG_Burstbug_Attack.asset` | Telegraph 3, release 4, projectile 4 and impact 6 variations bound | Covered | Verify Hudie reuse does not create an incorrect Burstbug-only identity |
| Burstbug Interceptable Volley | `FPG_Burstbug_Attack_Volley.asset`, warning ticks 0-66 and projectile tick 66 | Global cues contain 3 telegraph and 5 release variations; skill presentation contains 6 projectile, 3 interception and 6 impact variations, with intercepted contacts suppressing the normal impact group | Covered | Preserve no-immediate-repeat randomization, actual spawn/contact positioning and verify three-projectile concurrency in the final PlayMode pass |
| Burstbug Heavy Weakpoint/Break | `FPG_Burstbug_Attack_HeavyBreak.asset`, warning ticks 0-135 and impact tick 135 | Four telegraph, three danger-tick and four release variations are wired through the existing Heavy cue route; four base and four reinforced weakpoint impact variations are bound to the skill impact presentation | Covered | Prove the warning and danger ticks remain audible under continuous fire in the final PlayMode mix pass |
| Hudie projectile | `FPG_Hudie_Attack.asset`, warning ticks 0-39 and projectile tick 49 | Shared Fast threat routing remains available; four launch, five flight, four base-impact and four reinforced weakpoint variations are bound to the Hudie skill presentation | Covered | Verify shared warning identity, flight cleanup and impact concurrency in the final PlayMode pass |
| Luan summon | `FPG_Luan_Attack_Summon.asset`, warning 0-44, summon 44, self-destruct 71 | One active presentation track contains seven telegraph variations at tick 0, seven appearance-commit variations at tick 44 and four self-destruct variations at tick 71, all world-positioned at OwnerRoot | Covered | Verify all three events play once, follow the owner correctly and clean up across restart/room teardown in the final PlayMode pass |
| Player damaged | Committed `DamageApplied` trace targeting the player | `VO_Fei_Damaged_01.wav` (BOOM Close Combat recid 6831) is imported and bound to `PlayerDamaged` as a mono world-positioned SFX; candidates 2-8 remain outside Unity and previous impact, male and long KHRON vocal candidates remain rejected | Covered | Preserve the short female reaction as a separate player-local layer; add approved sibling variations only through a later explicit approval |
| Player barrier broken | Committed `BarrierBroken` trace | Three 2D shield-break variations bound | Covered | Preserve player-centric 2D routing and cooldown |
| Enemy break | Committed `BreakTriggered` trace | Three world-positioned variations bound | Covered | Preserve hit-position routing and cooldown |
| Enemy spawn/death | Formal enemy lifecycle | No dedicated stable audio consumer found | NeedsTrigger | Add presentation-only spawn and death hooks, then search one coherent enemy lifecycle family |
| Target lock | Hittable reticle transition | User explicitly removed this event; cue 19 remains empty | ExcludedByDesign | Regression test must keep the Forest bank reference empty |
| Room entered | `Preparing` and `Restarted` lifecycle events | User selected candidate 03; one 2D UI clip is imported, bound and covered by `CombatAudioBankTests` and `CombatAudioPresenterTests` | Covered | Add exactly-once lifecycle PlayMode coverage during the final room-transition pass |
| Exit unlocked | `ExitUnlocked` lifecycle event | User selected candidate 01; one 2D UI clip is imported, bound and covered by `CombatAudioBankTests` and `ForestRoomLifecycleCuesUseApprovedClipsAndCooldowns` | Covered | Add exactly-once lifecycle PlayMode coverage during the final room-transition pass |
| Exit confirmed | `ExitSelected` event | User selected candidate 01; one 2D UI clip is imported, bound to cue 24 and covered by `CombatAudioBankTests` and `ForestRoomLifecycleCuesUseApprovedClipsAndCooldowns` | Covered | Add exactly-once lifecycle PlayMode coverage during the final room-transition pass |
| Interaction focus/confirm/reject | Public methods on `FpgFormalAudioCoordinator` | Focus 01, all three Confirm variants and Reject 01 are approved, imported and bound to cues 25/26/27; no current caller invokes these methods; Boot runs before the FormalRoom audio root exists | NeedsTrigger | Bind approved groups to Boot character/mode/room choices and FormalRoom exit offers through a presentation-only UI audio owner |
| Forest ambience | `FpgRoomAudioProfile` / `MusicDirector` | Three approved stereo bed variations loop without mid-loop switching; all eight mono point sounds are bound to an independent no-repeat 8-18 second scheduler with a four-voice spatial pool and linear attenuation | Covered | Verify non-fatiguing density and spatial placement alongside the ambience bed during the final PlayMode mix pass |
| Exploration/combat/victory/defeat music | Explicit `MusicDirector` state transitions | Exploration, combat, victory and defeat candidate 01 are approved, imported as streaming stereo and bound; the profile/director support state-specific no-immediate-repeat variation groups; targeted EditMode validation passes | Covered | Validate full runtime crossfades and no duplicate combat-bank stingers during the final PlayMode transition pass |
| Cover enter/leave and rejected weapon actions | Current player control/presentation flow | No stable audio hooks documented in the baseline matrix | NeedsTrigger | Audit committed cover transitions and empty/rejected fire paths, add presentation-only IDs, then search concise feedback groups |

## Completion proof

Before the audio goal can be marked complete:

1. Every row above must be `Covered` or explicitly `ExcludedByDesign`.
2. Every `NeedsTrigger` row must have a concrete caller covered by EditMode or PlayMode tests.
3. Every approved sound group must be represented in `ForestAudioRequirements.csv` with source path/hash and final Unity resource.
4. Every repeated event must use an approved variation group with no immediate repeat.
5. A final runtime pass must verify Boot, room lifecycle, all Fei skills, every current enemy attack, player damage/break states, ambience and all music transitions.
