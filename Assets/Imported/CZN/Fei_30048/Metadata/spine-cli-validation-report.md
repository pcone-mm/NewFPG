# 绯（30048）Spine 3.8 CLI validation

Spine 3.8.75 CLI imported the canonical converter JSON after unpacking each
matching atlas into a local `images` directory. All three commands completed
with exit code `0`.

| Project | Project bytes | Unpacked images | Status |
|---|---:|---:|---|
| `External/CZN/Fei_30048/SpineProjects/Main/Fei_30048_Main.spine` | 1,577,588 | 206 | passed |
| `External/CZN/Fei_30048/SpineProjects/BattleReady/Fei_30048_BattleReady.spine` | 198,096 | 104 | passed |
| `External/CZN/Fei_30048/SpineProjects/U4AttackFX/Fei_30048_U4AttackFX.spine` | 9,515 | 35 | passed |

The converter JSON under `Assets/Imported/CZN/Fei_30048/SpineSource` remains
canonical. Do not overwrite it with a Spine Editor re-export, because the
editor may normalize mesh/deform data and nonessential topology.
