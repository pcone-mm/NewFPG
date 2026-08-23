# Forest Audio Completion Queue

This queue covers the 23-event Forest baseline in `ForestAudioRequirements.csv`
and the current playable-content expansion identified by the coverage audit. The
CSV remains the authoritative status ledger for candidate and binding status;
new expansion rows are added before their Soundminer search begins.

Runtime trigger coverage and currently known gaps are tracked in
`ForestAudioCurrentContentCoverage.md`.

## Approval rule

Only one event group is open for approval at a time. A group moves through these states:

1. `PendingSoundminerExport`: search or deterministic editing is still required.
2. `PendingHumanApproval`: 3-8 traced 48 kHz / 24-bit WAV candidates exist outside Unity.
3. `ApprovedForImport`: the user explicitly retained all variants or selected specific variants.
4. `BoundAndValidated`: approved files are imported, bound, and covered by targeted Unity validation.
5. `ExcludedByDesign`: the user explicitly removed the event from the audible design; its stable runtime ID may remain, but it must have no imported clip or playback binding.

Unapproved candidates never enter `Assets/`. Runtime variation groups retain every approved sibling and avoid immediate repetition without consuming combat RNG.

## Order

1. Fei primary attack - complete, four variants retained.
2. Fei primary enemy hit - complete, four variants retained.
3. Fei primary weakpoint hit - complete, four reinforced variants retained.
4. Fei primary environment hit - complete, four variants retained and validated.
5. Burstbug Fast telegraph - complete, three variants retained and validated.
6. Burstbug Fast release - complete, four variants retained and validated.
7. Burstbug Fast projectile - complete, four variants retained and validated.
8. Burstbug Fast hit - complete, six magic pierce-burst variants retained and validated.
9. Player damaged - candidate 1 (`VO_Fei_Damaged_01.wav`) selected, imported, bound to `CombatAudioCue.PlayerDamaged` and EditMode validated; the other seven candidates remain outside Unity and all prior impact, male and KHRON pain candidates remain excluded.
10. Player barrier broken - complete, three Energy Shield Break variants retained, imported, bound to cue 15, and EditMode validated.
11. Enemy break - complete, three Slinky Laser Burst variants retained, imported, bound to cue 16, and EditMode validated.
12. Target lock - excluded by design per user decision; no candidates are imported and cue 19 remains an empty reference.
13. Room entered - complete; user selected candidate 03 only, imported as a single UI cue and EditMode validated.
14. Exit unlocked - complete; user selected candidate 01 only, imported as a single UI cue and EditMode validated.
15. Exit confirmed - complete; user selected candidate 01 only, imported as a single UI cue and EditMode validated.
16. Interaction focus - complete; candidate 01 selected, imported and bound to cue 25. Runtime caller wiring remains in the interaction-consumer pass.
17. Interaction confirm - complete; all three candidates retained, imported and bound to cue 26.
18. Interaction reject - complete; candidate 01 selected, imported and bound to cue 27.
19. Forest ambience - complete; all three bed variants retained, bound and validated.
20. Forest exploration music - complete; user selected candidate 01 only, imported as streaming stereo and EditMode validated.
21. Forest combat music - complete; user selected candidate 01 only, imported as streaming stereo and EditMode validated.
22. Forest victory stinger - complete; user selected candidate 01 only, imported as streaming stereo and EditMode validated.
23. Forest defeat stinger - complete; user selected candidate 01 only, imported as streaming stereo and EditMode validated.

## Active confirmation sequence

Only the first item below is presented to the user. The next item does not open
until the current group has been explicitly approved or excluded and its Unity
validation has passed.

1. B01 Fei reload - complete; all five sibling variants retained and bound.
2. B02 Fei immediate secondary launch - complete; all five KHRON Luminous Projectile siblings retained, bound at tick 0 to `weapon.secondary.muzzle`, and EditMode validated.
3. B03 Fei immediate secondary impacts - complete; candidate 01 and its paired reinforced weakpoint derivative are imported, bound and EditMode validated.
4. B04 Fei charged start/hold - complete; pair 01 selected, imported and bound with fixed-pool held-loop lifecycle coverage.
5. B05 Fei charged release/cancel - complete; all 7 release and 5 cancel variants retained, imported, bound and validated.
6. B06 Fei charged impacts - complete; candidate pair 02 is imported, bound and validated; pairs 01/03 remain outside Unity.
7. C01 Burstbug Volley warning/release - complete; all 3 telegraph and 5 release variants retained, bound to the existing global cue IDs and validated.
8. C02 Burstbug Volley projectile family - complete; all 6 projectile, 3 interception and 6 impact variants retained, bound and validated.
9. C03 Burstbug Heavy warning family - complete; all 4 telegraph, 3 danger-tick and 4 release variants retained, bound and validated with 65/65 related EditMode tests passing.
10. C04 Burstbug Heavy impact - complete; all four paired base/weakpoint variants retained, imported, bound and validated with 65/65 related EditMode tests passing.
11. C05 Hudie projectile launch/flight - complete; all four launch and five flight variants retained, imported, bound and validated with 66/66 related EditMode tests passing.
12. C06 Hudie impact - complete; all four base and four paired reinforced weakpoint variants retained, imported, bound and validated with 66/66 related EditMode tests passing.
13. C07 Luan summon - complete; all seven paired telegraph and appearance-commit siblings retained, imported, bound and validated with 67/67 related EditMode tests passing.
14. C08 Luan self-destruct - complete; all four Anime Game magical fire-burst siblings retained, imported, bound at tick 71 and validated with 67/67 related EditMode tests passing.
15. D01 Enemy lifecycle - complete; all four spawn variants and all six Roc creature-death vocal variants are retained, imported, bound and validated with 35/35 targeted EditMode tests passing. The rejected Retro Game death v1 remains outside Unity.

## Full sequential approval queue

The queue below is the working order for the complete current-content pass. Only
one approval unit is exposed at a time. A unit may contain tightly coupled
sub-events when they must share one sound identity, but every sub-event keeps its
own trigger, anchor and validation evidence in the requirements ledger.

| Queue | Approval unit | Included current content | State |
|---|---|---|---|
| A01 | Exit confirmed | Exit selection confirmation | Complete; candidate 01 bound and validated |
| A02 | Interaction focus | Boot and room-offer focus | Complete; candidate 01 imported and bound, caller wiring tracked under D03/D04 |
| A03 | Interaction confirm | Boot and room-offer confirmation | Complete; all three candidates imported and bound, caller wiring tracked under D03/D04 |
| A04 | Interaction reject | Rejected or unavailable interaction | Complete; candidate 01 imported, bound and validated |
| A05 | Forest ambience bed | Continuous stereo Forest room bed | Complete; all three variants imported, bound and validated |
| A06 | Forest ambience points | Sparse randomized local details | Complete; all eight retained, imported, spatially scheduled and EditMode validated |
| A07 | Forest exploration music | Exploration loop | Complete; candidate 01 imported, bound and EditMode validated |
| A08 | Forest combat music | Combat loop | Complete; candidate 01 imported, bound and EditMode validated |
| A09 | Forest victory | Victory transition punctuation | Complete; candidate 01 imported, bound and EditMode validated |
| A10 | Forest defeat | Defeat transition punctuation | Complete; candidate 01 imported, bound and EditMode validated |
| A11 | Player damaged | Fresh short Fei reaction | Complete; candidate 1 imported, bound and EditMode validated; candidates 2-8 remain outside Unity |
| B01 | Fei reload | Reload commit at the authored tick | Complete; five Fantasy Game 2 Mechanical Click siblings imported, bound at tick 40 and EditMode validated |
| B02 | Fei immediate secondary launch | Launch and optional bounded motion identity | Complete; five KHRON Luminous Projectile siblings imported, bound at tick 0 to `weapon.secondary.muzzle`, and EditMode validated |
| B03 | Fei immediate secondary impacts | Base and reinforced weakpoint impacts | Complete; selected KHRON Electrified Impact candidate 01 and its paired reinforced derivative are imported, bound and EditMode validated |
| B04 | Fei charged start/hold | Charge start and lifecycle-controlled hold pulse/loop | Complete; Fantasy Game 2 pair 01 imported at `ChargeEnter` tick 0, with one-shot start and fixed-pool held loop validated across pause/restart/disable cleanup |
| B05 | Fei charged release/cancel | Release and cancellation punctuation | Complete; all 7 release and 5 cancel variants retained, imported, bound and validated with 32/32 related EditMode tests passing |
| B06 | Fei charged impacts | Base and reinforced weakpoint impacts | Complete; selected Fantasy Game 2 Magic Ice pair 02 imported, bound and validated with 32/32 related EditMode tests passing; pairs 01/03 remain outside Unity |
| C01 | Burstbug Volley warning/release | Interceptable telegraph and release | Complete; all 3 telegraph and 5 release variants retained, bound to the existing global cue IDs and validated with 48/48 related EditMode tests passing |
| C02 | Burstbug Volley projectile family | Projectile, interception and impact | Complete; all 6 projectile, 3 interception and 6 impact variants retained, bound and validated with 63/63 related EditMode tests passing |
| C03 | Burstbug Heavy warning family | Telegraph, wired danger tick and release | Complete; all 11 variants retained, countdown wiring bound, and validated with 65/65 related EditMode tests passing |
| C04 | Burstbug Heavy impact | Heavy break impact identity | Complete; all 4 base and 4 paired reinforced weakpoint variants retained, bound and validated with 65/65 related EditMode tests passing |
| C05 | Hudie projectile | Launch and audible flight identity | Complete; all 4 launch and 5 flight variants retained, imported, bound and validated with 66/66 related EditMode tests passing |
| C06 | Hudie impact | Hudie impact identity | Complete; all 4 base and 4 paired reinforced weakpoint variants retained, imported, bound and validated with 66/66 related EditMode tests passing |
| C07 | Luan summon | Telegraph, summon commit and Hudie appearance | Complete; all 7 paired telegraph/commit siblings retained, imported, bound and validated with 67/67 related EditMode tests passing |
| C08 | Luan self-destruct | Owner self-destruction punctuation | Complete; all 4 Anime Game magical fire-burst siblings retained, imported, bound at tick 71 and validated with 67/67 related EditMode tests passing |
| D01 | Enemy lifecycle | Spawn and death feedback | Complete; all 4 spawn variants and all 6 Roc creature-death vocal variants are retained, imported, bound and validated with 35/35 targeted EditMode tests passing; rejected death v1 candidates remain outside Unity |
| D02 | Player defeat body feedback | Player-local defeat response separate from music | Trigger audit and Soundminer search required |
| D03 | Boot interactions | Wire approved focus/confirm/reject groups to character, mode and room choices | Wiring validation; reuse approved UI groups |
| D04 | Formal room interactions | Wire approved UI groups to exit offers and confirmation | Wiring validation; reuse approved UI groups |
| D05 | Cover transitions | Enter and leave cover | Trigger audit and Soundminer search required |
| D06 | Weapon rejection | Empty and rejected weapon actions | Trigger audit and Soundminer search required |
| E01 | Final discovery audit | Rescan all current scenes, skill assets and presentation consumers for uncovered events | Mandatory after D06; every new playable event becomes a new approval unit before E02 |
| E02 | Full runtime and mix pass | PlayMode sequence, pool pressure, cleanup, music/ambience transitions and intelligibility | Pending after E01 |

If E01 finds a playable consumer not represented above, it is inserted before
E02 and follows the same candidate approval gate. The pass is not complete merely
because this initial queue is exhausted.

For every approval handoff, provide 3-8 playable WAV files plus the event intent,
source library/path, edit summary and variation policy. Candidates remain outside
`Assets/` until the user confirms which files to retain. Approved siblings are
imported as one no-immediate-repeat random group unless the user requests a
specific subset.

## Current playable-content expansion

The original 23 rows are a Forest vertical slice, not complete coverage of the
current project. After the baseline sequence above, add and complete the following
groups in this order:

1. Fei weapon remainder: reload commit, immediate-secondary launch and impact, charged-secondary start/hold/release/cancel and impact.
2. Burstbug Interceptable Volley: telegraph, release, projectile, interception and impact. Telegraph/release use the existing stable global cue IDs; projectile/interception/impact remain skill presentation.
3. Burstbug Heavy Weakpoint/Break: telegraph, visible danger tick, release and impact. The unused heavy-countdown routing must be connected before approval can be marked complete.
4. Hudie: projectile launch/flight and impact. Its fast threat warning may reuse the approved global fast telegraph/release group, but the reuse must be verified in play rather than assumed from the cue name.
5. Luan: summon telegraph, summon commit/Hudie appearance and owner self-destruct. These use skill presentation events because the current global threat resolver does not classify summon-only timelines.
6. Enemy and player lifecycle punctuation: enemy spawn, enemy death, player defeat body feedback and any remaining committed damage/break state with no audible result.
7. Current interaction consumers: Boot character choice, secondary-mode choice, room entrance choice and FormalRoom exit offers. Wire focus/confirm/reject to actual consumers; public presenter methods without callers do not count as coverage.
8. Player control feedback: cover enter/leave, empty or rejected weapon action and any current committed movement transition whose animation is otherwise audibly silent.
9. Forest ambience details: sparse randomized point sounds in addition to the continuous ambience bed.

Before each expansion search, append stable event rows to
`ForestAudioRequirements.csv` with the actual asset event/tick, anchor and bus.
Do not invent a new `CombatAudioCue` when an existing skill presentation or stable
global cue already owns the event.

## Per-event completion gate

- Candidate count is 3-8 after path and `_AudioCRC` de-duplication.
- Source database is opened read-only and every source path/hash is recorded.
- Positional SFX is mono; ambience and music are stereo.
- Master format is 48 kHz / 24-bit PCM WAV with no clipping or unsafe DC offset.
- Loop candidates have a recorded seam check.
- Unity references use the required anchor, bus, priority, concurrency, and cooldown.
- Targeted EditMode tests pass before the next event is presented.
- PlayMode behavior is checked when the trigger requires runtime sequencing or cleanup.

## Final gate

The Forest audio pass is complete only when every baseline and expansion row is in
an accepted terminal state (`BoundAndValidated` or explicitly
`ExcludedByDesign`), every current trigger has an audited audible result or a
recorded exclusion, all imported resources pass playback validation, and audio
produces no combat hash, tick or result changes.
