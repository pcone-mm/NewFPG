# Fei_30048 Unity import

Imported 411 Simplified-Chinese battle dependency records.

## Ready-to-use outputs

- `SpineSource`: decoded PNG atlas pages, rewritten `.atlas.txt` files, and
  decompressed private `.scsp1u.bytes` data kept side by side.
- `AncillarySource`: camera, camera-path and node SCSP1U data.
- `Configs`: JSON model/action data plus XML CFX/particle definitions.
- `Metadata/import-manifest.json`: source offsets, hashes and conversion notes.
- `SpineSource/**/*.json`: canonical Spine 3.8 JSON converted from SCSP1U.
- `Preview/Prefabs`: main model, BattleReady model and full skill composer.
- `Preview/SkillCompositions`: generated SkillSequence and Timeline assets.
- `Preview/Fei_30048_Preview.unity`: side-by-side model preview.
- `Preview/Fei_30048_SkillPreview.unity`: 12-skill composition preview.

Use `Tools > CZN > Fei 30048 > Build Complete Import` to rebuild the Unity
prefabs, SkillSequence assets, Timelines and preview scenes idempotently.

The separate Spine Editor projects are under
`External/CZN/Fei_30048/SpineProjects`. The converted JSON is canonical;
do not overwrite it with a Spine Editor re-export. These extracted payloads
are local study material and must not be redistributed without authorization.
