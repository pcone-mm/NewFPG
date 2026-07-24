# Heidemarie 30093 Unity skill composition report

Generated from `30093.srmd.json`, the referenced CFX files, particle plists, converted Spine 3.8 assets and ancillary camera/node JSON.

| Skill | Duration | Actor cues | Spine layers | Particle emitters | Transform cues | Markers | Unresolved |
|---|---:|---:|---:|---:|---:|---:|---:|
| `attack_play1` | 2.220s | 1 | 6 | 5 | 1 | 5 | 4 |
| `attack_play2` | 2.220s | 1 | 6 | 5 | 1 | 5 | 4 |
| `u1_attack` | 5.933s | 3 | 10 | 13 | 1 | 8 | 4 |
| `u2_buff` | 2.883s | 2 | 6 | 3 | 0 | 1 | 3 |
| `u3_attack` | 4.900s | 3 | 22 | 10 | 1 | 5 | 4 |
| `u4_buff` | 4.000s | 2 | 8 | 5 | 0 | 1 | 4 |
| `u5_attack_play1` | 2.220s | 1 | 7 | 3 | 1 | 5 | 3 |
| `u5_attack_play2` | 2.220s | 1 | 7 | 3 | 1 | 5 | 3 |
| `ug_attack` | 5.920s | 1 | 43 | 5 | 4 | 25 | 4 |
| `ux_attack` | 9.033s | 1 | 41 | 6 | 5 | 12 | 4 |
| `fatal` | 6.000s | 4 | 0 | 0 | 0 | 0 | 0 |
| `enter` | 1.667s | 3 | 0 | 0 | 0 | 0 | 0 |
| `victory` | 4.000s | 2 | 0 | 0 | 0 | 0 | 0 |

## Recovery boundary

- SRMD graph delays, command phases, CFX front/back ordering, anchors, offsets, scale and Spine animation names are data-derived.
- Camera/node SCSP1U files are converted to JSON and sampled as transform cues.
- Particle emitter motion/color/lifetime parameters are translated, but shared `particle/*.sct` textures are not in the character dependency set, so the preview uses a generated soft particle texture.
- Negative CFX scale values are retained as unresolved sentinel/mirroring semantics; the preview uses a positive fallback scale instead of guessing their engine-specific meaning.
- Ancillary camera/node keyframe values and durations are recovered, but non-stepped Spine Bezier curves are linearly interpolated in this preview.
- Multi-bone ancillary skeletons sample only the primary `cam`/`camera`/`node` bone; helper `node`/`pivot` bone motion remains in the source JSON but is not composed here.
- Original custom masks, shaders, radial RGB blur, speed blur, hit-stop and color-blend nodes are retained as Timeline diagnostic markers, not pixel-identical post-processing.
- The separate battle-ready UG graph/model remains available through `Heidemarie_30093_BattleReady.prefab`; this Timeline composer targets the main `30093` actor graph.

Preview scene: `Assets/Imported/CZN/Heidemarie_30093/Preview/Heidemarie_30093_SkillPreview.unity`.
