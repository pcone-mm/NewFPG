# Heidemarie_30093 Unity import

Imported 534 Simplified-Chinese battle dependency records.

## What is usable now

- `SpineSource`: decoded PNG atlas pages, rewritten `.atlas.txt` files,
  decompressed private `.scsp1u.bytes`, and converted standard Spine 3.8 JSON
  kept side by side.
- `AncillarySource`: camera, camera-path and node SCSP1U data.
- `Configs`: JSON model/action data plus XML CFX/particle definitions.
- `Metadata/import-manifest.json`: source offsets, hashes and conversion notes.
- `Metadata/spine-json-conversion-report.json`: per-file conversion hashes,
  structure counts, timeline counts, and duplicate-property decisions.
- `Metadata/spine-cli-validation-report.md`: five representative Spine 3.8.75
  import/reload checks.

The offline converter produced **150/150** same-name `.json` files with zero
structural failures. Across the batch it recovered 288 animations, 11,749
attachments, and 67,941 timeline records. Notable model outputs are:

- `SpineSource/model/30093.json`: the main battle model with 46 animations;
- `SpineSource/model/30093_battle_ready.json`: the battle-ready model with 4
  animations, 12 IK constraints, and 7 transform constraints.

All 11,694 Region/Mesh attachment paths resolve against their adjacent atlas
files. The one skeleton without a same-name atlas is a camera-shake resource
with no slots or attachments.

## Inspect in Spine

SCSP1U itself still cannot be opened by Spine; use the converted JSON. For
example:

```powershell
& "F:\tool\Spinepro_3.8.75学习版\Spine pro 3.8.75\Spine.com" `
  -i "Assets\Imported\CZN\Heidemarie_30093\SpineSource\model\30093.json" `
  -o "$env:TEMP\Heidemarie_30093.spine" `
  -r Heidemarie_30093
```

The direct JSON is canonical. A Spine Editor save/re-export may normalize
weighted mesh/deform indices and omit nonessential mesh internal edges. The
adjacent `.atlas.txt` and PNG are intended for runtime rendering; to see
individual source images inside Spine Editor, unpack the atlas into the JSON's
`images` directory first.

## Unity status

The animation is now playable in Unity 6 through the locally assembled
official `spine-unity` 3.8 runtime. Unity generated 150 `SkeletonDataAsset`,
150 `SpineAtlasAsset`, and their materials. Every skeleton asset loads; the
runtime reports the expected 288 animations.

Ready-to-open assets:

- `Preview/Heidemarie_30093_Preview.unity`: isolated comparison scene;
- `Preview/Prefabs/Heidemarie_30093_Main.prefab`: main model, `idle` by default;
- `Preview/Prefabs/Heidemarie_30093_BattleReady.prefab`: close-up battle-ready
  model, `b_idle` by default;
- `Preview/Prefabs/Heidemarie_30093_U1_FrontFX.prefab`: representative U1
  foreground effect, `animation` by default;
- `Preview/Heidemarie_30093_Preview_Final.png`: verified Unity render.

To play another main-model animation at runtime:

```csharp
var skeleton = GetComponent<Spine.Unity.SkeletonAnimation>();
skeleton.AnimationState.SetAnimation(0, "u1_attack_play", false);
```

The CFX, particle, SRMD and ancillary camera/node data are now translated into
13 generated `CznSpineSkillSequence` assets and 13 Timeline assets under
`Preview/SkillCompositions`. Open
`Preview/Heidemarie_30093_SkillPreview.unity` to play and switch between the
two normal attacks, U1-U5, UG, UX, Fatal, enter and victory sequences.

The generated composition restores 25 actor cues, 156 Spine layers, 58
particle emitters and 15 camera/node transform cues. Fourteen shared
`particle/*.sct` textures are outside the character dependency set, so the
preview uses a generated soft particle texture. Masks, custom shaders,
post-processing, hit-stop and exact Bezier interpolation remain documented
approximation boundaries in `Metadata/skill-composition-report.md`.

The local runtime package and extracted character payload are excluded from
Git.

Spine Editor and Spine Runtimes are separately licensed. Use an official Spine
license before distributing a build or redistributing the runtime.

No EventData/EventTimeline sample occurs in this 150-file corpus. The converter
therefore refuses event-bearing SCSP1U rather than guessing its private layout.
