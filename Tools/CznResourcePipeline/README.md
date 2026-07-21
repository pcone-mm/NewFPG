# CZN resource extraction and Spine conversion pipeline

This local study tool extracts an already-audited character dependency list
from the CZN `SSRC` chunk files and converts decompressed SCSP1U skeletons to
inspectable Spine 3.8 JSON. It never writes to the installed game and does not
start, hook, or inject into the client.

For Heidemarie (`30093`), the pipeline performs:

```text
SSRC record -> Zstandard -> original wrapper/resource
SCT1 -> 17-byte header + raw LZ4 RGB888 pixels -> PNG
SCT2 -> raw LZ4 or raw GPU blocks -> PNG
SCSP wrapper -> raw LZ4 -> private SCSP1U bytes
atlas -> atlas.txt with the page suffix changed from .sct to .png
SCSP1U -> standard Spine 3.8 JSON
JSON/XML configs -> Unity-readable TextAssets
```

Install the Python dependencies:

```powershell
python -m pip install -r Tools/CznResourcePipeline/requirements.txt
```

Create the initial name-based SSRA audit snapshot for a character before
expanding SRMD/BRMD/CFX dependencies:

```powershell
py Tools/CznResourcePipeline/audit_ssra_character.py `
  --manifest "F:\path\to\gameres\manifest.ssra" `
  --chunk-root "F:\path\to\gameres\chunks" `
  --branch main `
  --character-id 30048 `
  --prefix fei_30048 `
  --output "$env:TEMP\codex-czn-30048-audit\candidate-records.json"
```

The resulting candidates are not automatically a complete dependency closure.
Extract the model SRMD/BRMD and referenced CFX/particle configs, then add any
shared camera/node/effect triplets before treating the list as
`complete_records.json`.

Run from the Unity project root:

```powershell
python Tools/CznResourcePipeline/extract_character.py `
  --records "$env:TEMP\codex-czn-30093-audit\complete_records.json" `
  --gameres-root "F:\WeGameApps\rail_apps\czn(2002460)\bin\appdata\prod\gameres" `
  --branch main `
  --label Heidemarie_30093
```

Outputs:

- `External/CZN/Heidemarie_30093/Raw/main`: exact decompressed resource
  entries, including the original SCT/SCSP wrappers.
- `Assets/Imported/CZN/Heidemarie_30093`: Unity-readable PNG, atlas text,
  JSON/XML and decompressed `*.scsp1u.bytes` files.

The extractor recognizes two verified texture containers. `SCT2` carries the
existing ETC2/ASTC block formats. Legacy `SCT\x01` uses the packed header
`<magic[4], format_u8, width_u16, height_u16, raw_size_u32, lz4_size_u32>`;
the currently verified format code `4` is top-down RGB888. The decoder checks
the exact 17-byte header, `width * height * 3`, compressed payload length, and
LZ4 output length before writing a PNG. Unknown SCT magic or legacy format
codes are hard errors and are never replaced by placeholder images.

SCT2 may store block-aligned encoded dimensions that are slightly larger than
its logical dimensions. For atlas pages, the source atlas `size:` declaration
is authoritative: a page that declares the encoded size keeps the padding,
while a page that declares the smaller header-logical size is cropped from the
right and bottom. An SCT2 texture with no selected atlas page uses its logical
size. Every crop requires the discarded alpha pixels to be fully transparent;
otherwise extraction stops. Import metadata preserves encoded, logical,
atlas-declared, and final output dimensions plus the crop reason.

## Mixed main/shadow records and monster batches

An audited record array may deliberately combine a complete model triplet from
`shadow` with model configuration from `main`. Select `--branch all` so the
extractor consumes each record's explicit branch and emits one merged import
manifest. It refuses duplicate logical outputs instead of silently choosing a
branch:

```powershell
py Tools/CznResourcePipeline/extract_character.py `
  --records "output/CZNMonsterImportAudit/1004020/complete_records.json" `
  --branch all `
  --label Monster_1004020 `
  --character-id 1004020 `
  --external-root "External/CZN/Monsters/1004020" `
  --unity-root "Assets/Imported/CZN/Monsters/1004020" `
  --dry-run
```

For multiple monsters, preflight every record range and model triplet first,
then stage and publish the whole batch with `extract_monsters.py`:

```powershell
py Tools/CznResourcePipeline/extract_monsters.py `
  --records-root "output/CZNMonsterImportAudit" `
  --ids 1001005 1001023 1001016 1001003 1004002 1004020 1006002 1006018 `
  --dry-run
```

The project audit layout intentionally keeps two scopes per monster:

- `complete_records.json`: the six core records (one same-branch model
  `.atlas/.scsp/.sct` triplet plus main `setting/.srmd/.srcs`) used by `--ids`;
- `full_records.json`: the audited stance/VFX/camera/particle dependency set,
  supplied explicitly with repeated `--monster ID=.../full_records.json` when
  that larger scope is required.

Remove `--dry-run` only after reviewing the merged plan. The batch command
never writes under `gameres`, refuses existing per-ID output directories, and
publishes no monster directory until every extraction has completed in its
staging directory.

`SCSP1U` is a private runtime serialization, not a standard Spine `.skel`
file. Never rename it to `.skel.bytes`.

## Convert SCSP1U to Spine JSON

Run the offline converter from the Unity project root:

```powershell
py Tools/CznResourcePipeline/scsp1u_to_spine.py `
  Assets/Imported/CZN/Heidemarie_30093/SpineSource `
  --report Assets/Imported/CZN/Heidemarie_30093/Metadata/spine-json-conversion-report.json
```

For `foo.scsp1u.bytes`, the converter writes `foo.json` beside the source. It
preserves the original SCSP1U, `.atlas.txt`, and PNG files. The converter uses:

- `probe_scsp1u.py` for counted-record parsing and body-boundary validation;
- `emit_spine_animations.py` for Spine 3.8 animation timelines and curves;
- `scsp1u_to_spine.py` for setup pose, constraints, attachments, JSON output,
  and batch reporting.

The Heidemarie batch currently validates and converts **150/150** skeletons
with zero structural failures. It emits **288 animations**, including:

- `model/30093.json`: 46 animations;
- `model/30093_battle_ready.json`: 4 animations, 12 IK constraints, and 7
  transform constraints.

The batch report is stored at
`Assets/Imported/CZN/Heidemarie_30093/Metadata/spine-json-conversion-report.json`.
Five representative JSON files were also imported and re-read by Spine 3.8.75;
see `Metadata/spine-cli-validation-report.md` under the character import.

## Validation

Validate the extracted hashes, atlas/PNG pairs, SCSP1U markers, and config
files:

```powershell
py Tools/CznResourcePipeline/validate_import.py
```

The full JSON batch additionally passed an atlas binding audit: 11,694
renderable attachments resolved to an atlas region, with zero missing paths.
One camera-shake skeleton has no atlas because it has no slots or attachments.

The generated JSON is the canonical runtime-oriented output. Spine Editor can
import it, but saving or re-exporting a `.spine` project may normalize weighted
mesh/deform data and discard nonessential mesh internal edges.

## Unity runtime preview

The project now references a local `spine-unity` 3.8 package at
`External/CZN/SpineRuntime-3.8`. It was assembled from the official
`EsotericSoftware/spine-runtimes` `3.8` branch at commit
`8b4844bd4b193ba9e54487ed397a777993cbad56`, including the matching
`spine-csharp` sources. Unity 6.3.15f1 compiles it with zero errors; the
remaining compiler messages are obsolete-API warnings from the legacy editor
integration.

After import, Unity has 150 `SkeletonDataAsset` and 150 `SpineAtlasAsset`
objects. All 150 skeleton assets load successfully and expose the same 288
animations counted by the converter. Reusable preview assets are under
`Assets/Imported/CZN/Heidemarie_30093/Preview`.

The character import now also includes a data-driven Unity skill composer. It
reads `30093.srmd.json`, the referenced CFX/particle plists and converted
camera/node JSON, then generates 13 `CznSpineSkillSequence` assets plus 13
Timeline assets. The ready-to-run scene is
`Preview/Heidemarie_30093_SkillPreview.unity`; rebuild it with
`Tools > CZN > Heidemarie 30093 > Build Skill Compositions`.

Both the extracted payload and the assembled runtime package are intentionally
ignored by Git. `Packages/manifest.json` contains a local file reference, so a
fresh checkout must recreate that package before Unity resolves the project.
Spine Editor and Spine Runtimes have applicable license terms; use an official
license before distributing a build or redistributing the runtime.

Current structural findings and converter limits are documented in
[`SCSP1U_NOTES.md`](SCSP1U_NOTES.md).
