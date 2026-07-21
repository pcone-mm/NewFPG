#!/usr/bin/env python3
"""Validate a completed CZN Unity import against its generated manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
from typing import Any


SCSP1U_MARKER = b"scsp1u\x00\x00"
SCSP3_MARKER = b"scsp\x03\x00\x00\x00"


def bootstrap_pillow(explicit_path: Path | None) -> Any:
    candidates = [explicit_path]
    env_path = os.environ.get("CZN_PIPELINE_PYTHONPATH")
    if env_path:
        candidates.append(Path(env_path))
    candidates.append(Path(tempfile.gettempdir()) / "codex-czn-zstd")
    for candidate in candidates:
        if candidate and candidate.is_dir():
            sys.path.insert(0, str(candidate))
    from PIL import Image

    return Image


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def atlas_page_and_size(path: Path) -> tuple[str, tuple[int, int]]:
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    for index, line in enumerate(lines[:-1]):
        if not line or line[0].isspace() or not lines[index + 1].startswith("size:"):
            continue
        match = re.fullmatch(r"size:\s*(\d+)\s*,\s*(\d+)", lines[index + 1])
        if not match:
            raise ValueError(f"Invalid atlas size line in {path}: {lines[index + 1]}")
        return line, (int(match.group(1)), int(match.group(2)))
    raise ValueError(f"No atlas page found in {path}")


def validate(unity_root: Path, manifest_path: Path, dependency_path: Path | None) -> dict[str, Any]:
    Image = bootstrap_pillow(dependency_path)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    entries = manifest["entries"]
    counts: Counter[str] = Counter()
    atlas_pairs = 0

    for entry in entries:
        relative = Path(*entry["unity_output"].split("/"))
        output = unity_root / relative
        if not output.is_file():
            raise FileNotFoundError(output)
        actual_hash = sha256_file(output)
        if actual_hash != entry["unity_sha256"]:
            raise ValueError(f"SHA-256 mismatch: {output}")

        kind = entry["kind"]
        counts[kind] += 1
        if kind == "spine_atlas_text":
            page_name, declared_size = atlas_page_and_size(output)
            page_path = output.parent / page_name
            if not page_path.is_file():
                raise FileNotFoundError(page_path)
            with Image.open(page_path) as image:
                if image.size != declared_size:
                    raise ValueError(
                        f"Atlas/PNG size mismatch: {output} declares {declared_size}, "
                        f"PNG is {image.size}"
                    )
            atlas_pairs += 1
        elif kind in {"scsp1u_bytes", "scsp3_bytes"}:
            data = output.read_bytes()
            if len(data) < 16:
                raise ValueError(f"Truncated SCSP inner payload: {output}")
            marker = data[8:16]
            conversion = entry.get("conversion") or {}
            recorded_marker = conversion.get("inner_marker_hex")
            if recorded_marker != marker.hex():
                raise ValueError(
                    f"SCSP marker/manifest mismatch: {output} has {marker.hex()}, "
                    f"manifest has {recorded_marker!r}"
                )
            if kind == "scsp1u_bytes":
                if marker != SCSP1U_MARKER or not conversion.get("converter_eligible"):
                    raise ValueError(f"Invalid converter-eligible SCSP1U: {output}")
                if not output.name.endswith(".scsp1u.bytes"):
                    raise ValueError(f"SCSP1U has an unsafe output suffix: {output}")
            elif kind == "scsp3_bytes":
                if marker != SCSP3_MARKER or conversion.get("converter_eligible"):
                    raise ValueError(f"Invalid unsupported SCSP3 payload: {output}")
                if (
                    "UnsupportedSource" not in relative.parts
                    or not output.name.endswith(".scsp3.bytes")
                ):
                    raise ValueError(f"SCSP3 escaped UnsupportedSource: {output}")
        elif kind == "json_config":
            json.loads(output.read_text(encoding="utf-8-sig"))
        elif kind == "xml_config":
            ET.parse(output)

    expected_counts = manifest["counts"]
    if dict(counts) != expected_counts:
        raise ValueError(f"Count mismatch: actual={dict(counts)}, expected={expected_counts}")
    if atlas_pairs != counts["spine_atlas_text"]:
        raise ValueError("Not every atlas was paired with a PNG")

    return {
        "record_count": len(entries),
        "counts": dict(sorted(counts.items())),
        "atlas_png_pairs": atlas_pairs,
        "all_output_hashes_match": True,
        "all_scsp_markers_match": True,
        "all_configs_parse": True,
    }


def main() -> int:
    project_root = Path(__file__).resolve().parents[2]
    default_root = project_root / "Assets/Imported/CZN/Heidemarie_30093"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-root", type=Path, default=default_root)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--dependency-path", type=Path)
    args = parser.parse_args()
    unity_root = args.unity_root.resolve()
    manifest_path = (
        args.manifest.resolve()
        if args.manifest
        else unity_root / "Metadata/import-manifest.json"
    )
    try:
        result = validate(unity_root, manifest_path, args.dependency_path)
    except (OSError, ValueError, KeyError, json.JSONDecodeError, ET.ParseError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
