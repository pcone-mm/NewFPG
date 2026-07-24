# Heidemarie 30093 Spine CLI validation

Validation date: 2026-07-11 (Asia/Hong_Kong)

## Result

Five representative converted JSON files were imported into fresh `.spine`
projects with Spine CLI 3.8.75 and then re-opened with the CLI project-info
command. Every import and reload returned exit code 0.

This covers the setup and animation features used by the recovered battle
assets: Region, Mesh, Path and Clipping attachments; weighted vertices and
deforms; IK, Transform and Path constraints; and bone, slot, draw-order,
constraint, and deform timelines.

The SCSP1U producer string is `3.8.79.scsp`. Spine 3.8.75 accepted the emitted
Spine 3.8 JSON and saved 3.8.75 `.spine` projects without a version rejection.

## Environment and command

CLI executable used:

```text
F:\tool\Spinepro_3.8.75学习版\Spine pro 3.8.75\Spine.com
```

Import and reload pattern:

```powershell
& $spine -i $json -o $temporaryProject -r $skeletonName
& $spine -i $temporaryProject
```

The generated `.spine` projects were temporary validation artifacts and are
not committed under `Assets`. Their sizes and SHA-256 hashes are recorded below
to identify the exact successful runs.

## Validation matrix

| Case | Canonical JSON | Recovered structure | Import | Reload |
|---|---|---|---:|---:|
| Main battle model | `SpineSource/model/30093.json` | 288 bones, 110 slots, 7 Transform constraints, 296 Mesh + 1 Clipping attachment, 46 animations | 0 | 0 |
| Battle-ready model | `SpineSource/model/30093_battle_ready.json` | 326 bones, 113 slots, 12 IK + 7 Transform constraints, 122 Mesh attachments, 4 animations | 0 | 0 |
| U1 Region/Mesh effect | `SpineSource/effect/heidemarie_30093_eff_u1_attack_play_b.json` | 57 bones, 88 slots, 48 Region + 60 Mesh attachments, 2 animations, 382 timelines | 0 | 0 |
| U4 Path effect | `SpineSource/effect/heidemarie_30093_eff_u4_buff_self_b.json` | 156 bones, 266 slots, 10 Path constraints, 3 Path + 1 Clipping + 130 Region + 131 Mesh attachments, 2 animations; 18 Path-position + 6 Path-mix timelines | 0 | 0 |
| UX Transform sample | `SpineSource/effect/heidemarie_30093_ux_cutin_02_1.json` | 201 bones, 122 slots, 9 Transform constraints, 127 Mesh attachments, 2 animations; 2 Transform timelines | 0 | 0 |

## Canonical JSON identity

| JSON | Bytes | SHA-256 |
|---|---:|---|
| `model/30093.json` | 11,772,089 | `01B1BAD6FBD89F2E0A006E62D016649C0C74F40BF63CE369FF76E4FDE52BF57B` |
| `model/30093_battle_ready.json` | 1,789,037 | `8BF3710E3DFBB49A79957DFF4C6CAF56263AD4AA87D7D935748714C75126470D` |
| `effect/heidemarie_30093_eff_u1_attack_play_b.json` | 89,077 | `F3CA87A28D1C46641CAB849B7F1A9ABB6922384A6D7C008A62F859C7397412C5` |
| `effect/heidemarie_30093_eff_u4_buff_self_b.json` | 336,775 | `4895E380572A71FF24CF0019FBE62F10ACD9384F9A12395518FC38795080C9CC` |
| `effect/heidemarie_30093_ux_cutin_02_1.json` | 837,221 | `915A6E3DB6138E886F8EC567924B0E32D03AE9B5EA0EAE7D434BA0B4F049B3A8` |

## Temporary Spine project identity

| Project | Bytes | SHA-256 |
|---|---:|---|
| `canonical-30093.spine` | 1,927,857 | `9AA2594EFEC3E1205B80D1F2D508BDE6AD94426659DCD80682E8ABC82F0B3058` |
| `canonical-30093_battle_ready.spine` | 411,866 | `1806B1052572D55D3B30490293F6FEF622124F1CC1074EDFD799E449A28EF01C` |
| `canonical-u1.spine` | 16,165 | `839FAC0B32C9092EBCCA9FCA714D2EF4DACDCC7515B57F9FCA6A64FBB9DAE0F5` |
| `canonical-u4-path.spine` | 55,668 | `DBAD88DDBAE379A6D2328DD09E7760933311E4ADFD5C2046FFA47ED2041B991B` |
| `canonical-ux-transform.spine` | 173,689 | `36220E08AB01371FB06D98DEB9B408D82CB7C4EFCC52C753D4851F1E0CA9CFD4` |

## Batch and atlas checks

- SCSP1U structural closure: 150/150.
- JSON conversion: 150/150, zero failures.
- Total recovered animations: 288.
- Total recovered timelines: 67,941.
- Atlas-backed Region/Mesh attachments checked: 11,694.
- Missing atlas regions: 0.
- Missing default-skin setup attachments: 0.
- Same-name atlas files: 149. The sole atlas-less camera-shake skeleton has no
  slots or attachments.

The full per-file evidence is in `spine-json-conversion-report.json`.

## Expected warnings and limits

The CLI emitted non-fatal Java Preferences registry warnings and a welcome-data
HTTP 410 warning. Neither changed the exit code or project data. It also warned
that nonessential mesh internal edges are unavailable. SCSP1U does not retain
all Spine Editor authoring metadata, so this is expected.

The generated JSON is the canonical runtime-oriented output. Saving or
re-exporting through Spine Editor may normalize weighted influences and deform
indices; a round-tripped JSON should not replace the direct converter output.

No EventData or EventTimeline occurs in this corpus. Event-bearing SCSP1U is
therefore intentionally rejected until its private layout is verified.

A local build of the official `spine-unity` 3.8 runtime is now connected to the
Unity 6.3 project. All 150 generated `SkeletonDataAsset` objects load with zero
data errors, and an edit-mode probe confirmed that advancing
`u1_attack_play` changes the generated mesh. See
`spine-unity-integration-report.md` for the package and preview evidence.

Spine Editor and Spine Runtimes have applicable license terms. Use an official
license before distributing a build or redistributing the runtime.
