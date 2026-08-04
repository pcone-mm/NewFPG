---
name: czn-character-spine-unity-import
description: Recover a CZN (Chaos Zero Nightmare / 卡厄斯梦境) character's Spine 3.8 battle models, layered skill VFX, CFX/particle/camera timing, and import them into this Unity project with prefabs, SkillSequence assets, Timelines, preview scenes, reports, and replay validation. Use when the user asks to locate, extract, convert, import, preview, combine, repair, or repeat the workflow for another CZN character or character ID.
---

# CZN Character Spine / Unity Import

## Required context

1. Read `CLAUDE.md`, `Docs/CLAUDE.md`, and `references/WORKFLOW.zh-CN.md` completely.
2. Confirm or discover: display name, numeric character ID, game `gameres` root, main/shadow branch, Unity root, and Spine 3.8 executable.
3. Treat the installed client as read-only. Work only from copied SSRC records and generated project folders.
4. Never commit or redistribute extracted game payloads or the locally assembled Spine runtime without authorization.

## Workflow

1. Audit before extraction.
   - Resolve the character ID and produce an audited `complete_records.json` containing exact path, chunk, offset, size, branch, and hash for all model/config/effect dependencies.
   - Include `model`, `model_data`, `model_setting`, referenced CFX/particle resources, camera/camera_path/node data, cut-ins, shared effects, and battle-ready graphs.
   - Do not guess missing records from names alone; record unresolved dependencies.

2. Extract and validate.
   - Run `Tools/CznResourcePipeline/extract_character.py` with explicit `--label`, `--character-id`, `--external-root`, and `--unity-root`; its defaults are Heidemarie-specific.
   - Run `validate_import.py` with explicit paths. Preserve `import-manifest.json`, record snapshots, source hashes, wrappers, and `*.scsp1u.bytes`.

3. Convert Spine data.
   - Run `scsp1u_to_spine.py` over `SpineSource` and `AncillarySource` as applicable, writing per-batch reports.
   - Keep converter JSON as canonical. Never rename SCSP1U to `.skel`, and never overwrite canonical JSON with a Spine Editor re-export.
   - Audit animation counts, atlas bindings, unsupported record types, deform, attachment, draw-order, IK/transform/path constraints, and timeline closure.

4. Import into Unity.
   - Verify the local Spine 3.8 runtime reference before opening/importing assets.
   - Let spine-unity generate atlas, material, and `SkeletonDataAsset` objects; verify every skeleton loads and its animation count matches conversion reports.
   - Build isolated main-model and battle-ready prefabs plus a simple comparison scene before composing skills.
   - Put each serialized custom `TrackAsset` subclass in its own same-named `.cs` file. After saving and reloading every generated Timeline, assert `GetRootTracks()` and clip sequence references are still present; YAML that records the track with `m_Script: {fileID: 0}` is broken even if the in-memory Timeline initially worked.

5. Recover and compose skills.
   - Parse SRMD/BRMD command graphs and referenced CFX/particle plists. Preserve phase timing, layer delay, animation name, front/back sorting, SELF/TARGET/CENTER/SCREEN anchors, scale/offset/rotation/opacity, particle alpha-vs-additive blend, camera/node translate-rotate-scale transforms, and event markers.
   - Resolve standby action duration from its referenced BRMD command/node animation so `STANDBY_OFF` follows the real action window. Hold a non-looping actor animation at its terminal pose until the next actor cue or an explicit `IDLE` marker instead of falling back to idle during graph gaps.
   - Reuse runtime types in `Assets/Scripts/CZN`. Adapt or create a character-specific Editor composer under `Assets/Editor/CZN`; the existing `HeidemarieSkillComposer` is a reference, not a generic command.
   - Generate idempotently: SkillSequence assets, Timeline assets, preview prefabs/scenes, metadata reports, and unresolved-resource notes.

6. Validate deterministically.
   - Compile with zero errors, inspect the Unity console, and sample every skill at start, cue boundaries, visible middle frames, and end.
   - Default battle-skill previews to one-shot playback: let the Timeline stop, clear runtime Spine/particle/standby/camera state, and return the main actor to looping `b_idle`. Only use automatic Timeline wrap when the source behavior or the user explicitly requires a loop.
   - Test switch, pause/resume without cleanup, immediate replay, replay after terminal frames, natural completion to `b_idle`, and at least three manual hard replays. If loop mode is intentionally enabled, additionally validate at least three automatic wraps.
   - Spine hard replay order is `ClearTracks` -> `SetToSetupPose` -> `SetAnimation` -> apply/update. The project player uses `SkeletonAnimation.ClearState()` for the first two operations. Never reuse a terminal attachment/alpha pose.
   - Assert runtime layer counts do not grow, required attachments/mesh vertices return after replay, and camera/particles reset.
   - Sample one frame before and after every cue boundary, including marker and camera-zoom boundaries. For replay regressions, compare deterministic slot/attachment/type/mesh-vertex signatures at a representative visible frame, not only aggregate attachment counts.

7. Hand off.
   - Update `Assets/Imported/CZN/<Label>/README.md` from `references/CHARACTER-HANDOFF-TEMPLATE.zh-CN.md`; keep detailed evidence in the adjacent `Metadata/` directory instead of creating a parallel project-level workflow guide.
   - Report exact scene, prefab, Spine project, SkillSequence/Timeline, metadata, validation, approximation, licensing, and replay-test locations.

## Existing references

- Detailed reusable procedure: `references/WORKFLOW.zh-CN.md`
- Deliverable template: `references/CHARACTER-HANDOFF-TEMPLATE.zh-CN.md`
- Proven sample: `Assets/Imported/CZN/Heidemarie_30093/README.md` and its adjacent `Metadata/` reports
- Extract/convert tools: `Tools/CznResourcePipeline`
- SCSP1U format limits: `Tools/CznResourcePipeline/SCSP1U_NOTES.md`

Do not claim the pipeline is fully one-click: extraction/conversion are parameterized, while dependency-audit generation and skill-graph composition still require character-specific analysis and validation.
