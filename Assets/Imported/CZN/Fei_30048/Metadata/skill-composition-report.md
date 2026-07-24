# 绯（30048）Unity skill composition report

Generated from `30048.srmd.json`, the referenced CFX files, particle plists, converted Spine 3.8 assets and ancillary camera/node JSON.

| Skill | Duration | Actor cues | Spine layers | Particle emitters | Transform cues | Camera zoom | Markers | Unresolved |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `attack_play1` | 1.250s | 1 | 5 | 3 | 1 | 0 | 4 | 0 |
| `attack_play2` | 1.250s | 1 | 7 | 3 | 1 | 0 | 4 | 0 |
| `u1_buff` | 2.416s | 2 | 4 | 4 | 0 | 0 | 1 | 0 |
| `u2_buff` | 2.867s | 2 | 4 | 13 | 0 | 0 | 1 | 0 |
| `u3_buff` | 2.417s | 2 | 9 | 10 | 0 | 0 | 1 | 0 |
| `u4_attack` | 2.916s | 3 | 10 | 8 | 2 | 0 | 6 | 1 |
| `u5_buff` | 3.117s | 2 | 8 | 9 | 0 | 0 | 1 | 0 |
| `ug_attack` | 5.333s | 1 | 28 | 0 | 3 | 0 | 19 | 2 |
| `ux_buff` | 16.466s | 2 | 38 | 3 | 1 | 3 | 11 | 1 |
| `fatal` | 6.000s | 4 | 0 | 0 | 0 | 0 | 0 | 0 |
| `enter` | 1.133s | 3 | 0 | 0 | 0 | 0 | 0 | 0 |
| `victory` | 2.667s | 2 | 0 | 0 | 0 | 0 | 0 | 0 |

## Recovery boundary

- SRMD graph delays, command phases, CFX front/back ordering, anchors, offsets, scale and Spine animation names are data-derived.
- Camera/node SCSP1U files are converted to JSON and sampled as transform cues.
- Particle emitter motion/color/lifetime parameters are translated. Four exact config-referenced `particle/*.sct` textures are decoded and bound; additive plist emitters use the project's additive particle shader, and any unresolved texture uses a generated soft fallback.
- Negative CFX scale values are retained as unresolved sentinel/mirroring semantics; the preview uses a positive fallback scale instead of guessing their engine-specific meaning.
- Ancillary camera/node keyframe values and durations are recovered; camera scale is combined with SRMD zoom as orthographic zoom, while non-stepped Spine Bezier curves are linearly interpolated in this preview.
- Multi-bone ancillary skeletons sample only the primary `cam`/`camera`/`node` bone; helper `node`/`pivot` bone motion remains in the source JSON but is not composed here.
- Original custom masks, shaders, radial RGB blur, speed blur, hit-stop and color-blend nodes are retained as Timeline diagnostic markers, not pixel-identical post-processing.
- UG composes the BattleReady standby actor and its BRMD node transform. The preview scale/placement is a study approximation based on BRMD standby coordinates.
- `FRONT` CFX anchors are mapped to the screen foreground because the Unity preview has no native XCent FRONT layer.
- UX `CUTIN` is retained as a diagnostic marker: the source node has neither `id` nor `file_name`, so no name-only candidate is bound.

Preview scene: `Assets/Imported/CZN/Fei_30048/Preview/Fei_30048_SkillPreview.unity`.
