# SCSP1U reverse-engineering notes (`30093` + monster regression)

These notes describe the decompressed `model/30093.scsp1u.bytes` sample and the
layouts verified across all 150 Heidemarie SpineSource skeletons. They document
the audited corpus, not every variant that could exist in another client build.

## Confirmed status

- The data is not encrypted.
- It is not standard Spine JSON, Spine binary (`.skel`) or a `.spine` project.
- The embedded producer/version string is `3.8.79.scsp`.
- Spine cannot import SCSP1U directly, and changing the extension does not
  convert it.
- The game executable contains loader source-path strings such as
  `yuna2d/spinex/v3/SCSLoader_v3.cpp`, but no discovered exporter. Do not inject
  into or automate the running client; a static parser is the appropriate path.
- The offline converter now closes the declared body boundary for 150/150
  SpineSource files and emits standard Spine 3.8 JSON for all of them.
- A separate eight-monster regression closes 86/86 SCSP1U bodies. Six core
  models contain two EventData defaults (`hit` and `spell`); all 86 emit JSON.
- Spine 3.8.75 successfully imported and re-read five representative converted
  JSON files, including IK, transform, path, clipping, mesh, region, deform,
  and animation data.

## Top-level sample layout

For the decompressed `model/30093.scsp1u.bytes` sample:

```text
total length: 18,215,578 bytes

0x00  u32 bodyLength = 18,157,421
0x04  u32 stringPoolLength = 58,149
0x08  char[8] = "scsp1u\0\0"
0x10  serialized body
...   NUL-terminated string pool
```

The string pool begins at sample offset `18,157,429`, contains 6,216 strings,
and is referenced by `u32` byte offsets from body records.

## Confirmed sample sections

Offsets below are specific to this sample and should not be hard-coded into a
general parser; consume preceding counts and record sizes instead.

- At `0x6A`: bone count = 288.
- At `0x6C`: bone records, 43 bytes each:

```text
u16 index
u32 nameStringOffset
i16 parentIndex
f32 length
f32 x
f32 y
f32 rotation
f32 scaleX
f32 scaleY
f32 shearX
f32 shearY
u16 transformMode
u8  skinRequired
```

`transformMode` is the sequential spine-cpp 3.8 enum used by the Cocos client:
`0 normal`, `1 onlyTranslation`, `2 noRotationOrReflection`, `3 noScale`, and
`4 noScaleOrReflection`. It must not be interpreted as spine-csharp's internal
`[Flags]` values (`0/7/1/2/6`); standard JSON carries the mode by name and each
runtime maps that name to its own representation.

- At `0x30CC`: IK constraint count = 0.
- At `0x30CE`: slot count = 110.
- At `0x30D0`: slot records, 47 bytes each:

```text
u16 index
u32 nameStringOffset
u16 boneIndex
4 * f32 lightColor
4 * f32 darkColor
u8 hasDarkColor
u32 attachmentNameOffset (or 0xFFFFFFFF)
u16 blendMode
```

- At `0x4502`: transform constraint count = 7. A one-bone record is 57 bytes
  and includes name/order/skin flags, ten floats, relative/local flags, target,
  and constrained bone indices. The record size is `55 + 2 * boneCount`.
- At `0x4693`: path constraint count = 0.
- At `0x4695`: skin count = 1.
- At `0x4697`: default skin begins; the sample contains 297 attachment entries.
- At `0x4A061`: animation count = 46.
- At `0x4A063`: first animation (`attack_end`) begins with:

```text
u32 animationNameOffset
f32 duration = 0.333333...
u16 timelineCount = 395
```

The converter dynamically consumes all preceding counted sections; it does not
hard-code this animation offset.

## Audited attachment and timeline coverage

The 150-file corpus contains these attachments:

| Attachment | Count |
|---|---:|
| Region | 6,436 |
| Mesh | 5,258 |
| Path | 8 |
| Clipping | 47 |

Bounding-box, linked-mesh, and point attachments are valid Spine 3.8 types but
do not occur in this corpus, so their SCSP1U payload layouts are not asserted.

The timeline enum matches Spine 3.8 `TimelineType`:

| ID | Timeline | Corpus count |
|---:|---|---:|
| 0 | rotate | 21,062 |
| 1 | translate | 12,672 |
| 2 | scale | 9,185 |
| 3 | shear | 3,503 |
| 4 | attachment | 10,147 |
| 5 | color | 8,715 |
| 6 | deform | 2,531 |
| 7 | event | 0 |
| 8 | draw order | 50 |
| 9 | IK | 10 |
| 10 | transform | 2 |
| 11 | path position | 52 |
| 12 | path spacing | 0 |
| 13 | path mix | 12 |
| 14 | two-color | 0 |

Timeline types 12 and 14 are emitted according to the official Spine 3.8
layout, but no current file exercises them. EventData was absent from the
original 150-file corpus, but is now verified on six monster models. Each
record is 28 bytes in the following order:

```text
u32 nameStringOffset
i32 intDefault
f32 floatDefault
u32 stringDefaultOffset (or 0xFFFFFFFF)
u32 audioPathOffset (or 0xFFFFFFFF)
f32 volumeDefault
f32 balanceDefault
```

The verified models each declare `hit` and `spell` with zero/null defaults,
volume `1`, and balance `0`; they emit as top-level Spine 3.8 `events`. Timeline
type 7 remains unsupported because none of the 236 audited SCSP1U files uses an
EventTimeline, so its private frame layout is still not asserted.

The batch contains 288 animations and 67,941 timeline records. Expanded
SCSP1U curve samples are converted back to legacy Spine 3.8
`curve`/`c2`/`c3`/`c4` Bezier fields. Eleven duplicate attachment properties in
the main model are resolved by retaining the later runtime record; every such
replacement is listed in the conversion report.

## Validation results

- 150/150 bodies close at their declared SCSP1U boundary.
- All 150 files emit JSON; JSON output totals 288 animations.
- 149 same-name atlas files cover all renderable skeletons. The remaining
  camera-shake skeleton has no slots or attachments and needs no atlas.
- 11,694 Region/Mesh attachment paths resolve to atlas regions; zero are
  missing.
- All setup-pose slot attachment names resolve in the default skin.
- Spine 3.8.75 CLI imported and re-read the main model, battle-ready model,
  Region/Mesh skill effect, Path sample, and Transform-timeline sample with
  exit code 0.
- The monster regression converts 86/86 files with 191 animations and 12
  EventData records. All eight core model animation tables match their emitted
  JSON exactly; Spine 3.8.75 imports and re-reads the event-bearing/noScale
  Killer Fly model with 2 events and 15 animations.

## Unity integration status and remaining boundary

The emitted JSON is canonical. Importing into Spine Editor and saving a
`.spine` project is useful for inspection, but the editor may normalize
weighted mesh/deform indices and omit nonessential internal-edge metadata.

A local build of the official `spine-unity` 3.8 runtime is now connected to
this Unity 6.3 project. Unity created 150 `SkeletonDataAsset` and 150
`SpineAtlasAsset` objects; all skeleton assets load with zero runtime-data
errors. An edit-mode animation probe advanced `idle` to `u1_attack_play` and
confirmed that the generated mesh vertices changed, so the data is not merely
recognized as text: it is being evaluated by the Spine runtime.

The remaining work is choreography rather than skeleton conversion. The game
combines many independently imported effect skeletons with CFX, particle,
camera, mask, and timing configuration. Those source files are preserved, but
they have not all been reconstructed into one Unity Timeline or gameplay
ability prefab. EventTimeline-bearing SCSP1U remains outside the verified
format coverage.

The local runtime package and extracted payload are excluded from Git. Spine
Editor and Spine Runtimes have applicable license terms; use an official
license before distributing a build or redistributing the runtime.
