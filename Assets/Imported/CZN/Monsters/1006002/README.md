# Monster_1006002 Unity import

Imported 46 Simplified-Chinese battle dependency records.

## What is usable now

- `SpineSource`: decoded PNG atlas pages, rewritten `.atlas.txt` files, and
  verified private `.scsp1u.bytes` data kept side by side.
- `AncillarySource`: camera, camera-path and node SCSP1U data.
- `UnsupportedSource`: preserved, explicitly recognized legacy SCSP v3 payloads.
  These files are deliberately outside the converter input roots and are not
  playable. Any unrecognized SCSP marker stops extraction for manual audit.
- `Configs`: JSON model/action data plus XML CFX/particle definitions.
- `Metadata/import-manifest.json`: source offsets, hashes and conversion notes.

The PNG and text/config assets can be inspected in Unity immediately. The
skeleton animation is **not playable yet**: SCSP1U is the game's private
runtime serialization, not standard Spine JSON or binary. Renaming it to
`.skel.bytes` does not convert it, and Spine 3.8.75 cannot open it.

The next required step is a real SCSP1U-to-standard-Spine converter. Only then
should a matching, officially licensed spine-unity runtime be added and an
isolated preview prefab/scene be created.
