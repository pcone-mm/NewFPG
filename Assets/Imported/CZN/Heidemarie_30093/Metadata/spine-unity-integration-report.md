# Heidemarie 30093 spine-unity integration

Validation date: 2026-07-11 (Asia/Hong_Kong)

## Runtime

- Unity: 6000.3.15f1.
- Package: `com.esotericsoftware.spine.spine-unity@3.8.0`.
- Source: official `EsotericSoftware/spine-runtimes` branch `3.8`, commit
  `8b4844bd4b193ba9e54487ed397a777993cbad56`.
- Local package: `External/CZN/SpineRuntime-3.8`.
- Manifest reference: `file:../External/CZN/SpineRuntime-3.8`.

The upstream package layout was assembled with its matching `spine-csharp`
sources. A clean isolated Unity 6.3 compile completed with zero errors and 30
legacy API warnings. The warnings concern obsolete editor APIs such as old
sprite metadata, instance IDs, build-target defines, and `Rigidbody2D`
properties; they do not block import or runtime playback.

## Unity import validation

- `SkeletonDataAsset`: 150.
- `SpineAtlasAsset`: 150.
- Converted animations exposed by the runtime: 288.
- Skeleton assets returning null or throwing during `GetSkeletonData(true)`: 0.
- Unity console after import: 0 errors, 0 warnings.
- Main-model edit-mode probe: 1,235 generated vertices in both frames.
- Vertex checksum changed from `18567.992` (`idle`) to `16308.767`
  (`u1_attack_play` at 0.4 seconds), confirming animation evaluation.

## Preview assets

- `Preview/Heidemarie_30093_Preview.unity`.
- `Preview/Prefabs/Heidemarie_30093_Main.prefab`.
- `Preview/Prefabs/Heidemarie_30093_BattleReady.prefab`.
- `Preview/Prefabs/Heidemarie_30093_U1_FrontFX.prefab`.
- `Preview/Heidemarie_30093_Preview_Final.png`.

The preview scene was created additively, saved, and closed again. The user's
existing `Shulin_L0` scene remained the active scene and was not modified by
the preview setup.

## Boundary

All individual Spine effect layers are imported. Reproducing an entire skill
exactly still requires translating the accompanying CFX, particle, mask,
camera, and timing configuration into Unity orchestration (for example a
Timeline or an ability-specific controller).

The extracted assets and local runtime package are excluded from Git. Spine
Editor and Spine Runtimes have applicable license terms; use an official
license before distributing a build or redistributing the runtime.
