#!/usr/bin/env python3
"""Audit an SSRA v4 manifest and emit candidate records for one CZN character.

The tool is read-only. It parses the unencrypted manifest tables, maps archive
group/part identifiers to the local SSRC filenames, and writes a deterministic
record snapshot. The initial candidate set is name-based; callers must expand
the dependency closure from SRMD/BRMD/CFX/particle contents before extraction.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import struct
from pathlib import Path
from typing import Any, Iterable


RECORD_SIZE = 40
ARCHIVE_SIZE = 32
ARCHIVE_PAYLOAD_SIZE = 28


class AuditError(RuntimeError):
    pass


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def u64(data: bytes, offset: int) -> int:
    return struct.unpack_from("<Q", data, offset)[0]


def c_string(data: bytes, offset: int) -> str:
    if offset < 0 or offset >= len(data):
        raise AuditError(f"String offset is outside the manifest: {offset}")
    end = data.find(b"\0", offset)
    if end < 0:
        raise AuditError(f"Unterminated manifest string at {offset}")
    return data[offset:end].decode("utf-8")


def parse_manifest(path: Path, branch: str) -> dict[str, Any]:
    data = path.read_bytes()
    if len(data) < 64 or data[:4] != b"SSRA":
        raise AuditError(f"Not an SSRA manifest: {path}")
    if u32(data, 4) != 4:
        raise AuditError(f"Unsupported SSRA version {u32(data, 4)}: {path}")

    archive_count = u32(data, 12)
    record_count = u32(data, 16)
    header_flags = u32(data, 20)
    string_table = u64(data, 24)
    string_bytes = u64(data, 32)
    archive_table = u64(data, 40)
    record_table = u64(data, 48)
    archive_record_size = u64(data, 56)

    if archive_record_size != ARCHIVE_PAYLOAD_SIZE:
        raise AuditError(f"Unexpected archive record size {archive_record_size}")
    if record_table + record_count * RECORD_SIZE > len(data):
        raise AuditError("SSRA record table exceeds the file")
    if archive_table + archive_count * ARCHIVE_SIZE > len(data):
        raise AuditError("SSRA archive table exceeds the file")
    if string_table + string_bytes > len(data):
        raise AuditError("SSRA string table exceeds the file")

    archives: list[dict[str, Any]] = []
    for index in range(archive_count):
        offset = archive_table + index * ARCHIVE_SIZE
        archives.append(
            {
                "archive_index": index,
                "part": u32(data, offset),
                "group": u32(data, offset + 4),
                "stored_size": u64(data, offset + 8),
                "original_size": u64(data, offset + 16),
                "hash": f"{u64(data, offset + 24):016x}",
            }
        )

    # Archive names are stored as consecutive C strings at the beginning of
    # the string table. File paths use independent offsets into that table.
    archive_name_offset = string_table
    virtual_starts: dict[int, int] = {}
    for archive in archives:
        name = c_string(data, archive_name_offset)
        archive_name_offset += len(name.encode("utf-8")) + 1
        archive["name"] = name
        group = int(archive["group"])
        archive["virtual_start"] = virtual_starts.get(group, 0)
        virtual_starts[group] = int(archive["virtual_start"]) + int(
            archive["original_size"]
        )

    archives_by_group: dict[int, list[dict[str, Any]]] = {}
    for archive in archives:
        archives_by_group.setdefault(int(archive["group"]), []).append(archive)

    records: list[dict[str, Any]] = []
    for index in range(record_count):
        offset = record_table + index * RECORD_SIZE
        virtual_offset = u64(data, offset + 8)
        stored = u32(data, offset + 16)
        original = u32(data, offset + 20)
        encryption0 = u32(data, offset + 24)
        name_offset = u32(data, offset + 28)
        compression = struct.unpack_from("<H", data, offset + 32)[0]
        archive_group = struct.unpack_from("<H", data, offset + 34)[0]
        encryption1 = u32(data, offset + 36)
        archive = next(
            (
                item
                for item in archives_by_group.get(archive_group, [])
                if int(item["virtual_start"])
                <= virtual_offset
                < int(item["virtual_start"]) + int(item["original_size"])
            ),
            None,
        )
        if archive is None:
            path_value = c_string(data, string_table + name_offset)
            raise AuditError(
                "No archive virtual range contains "
                f"{path_value!r} (group={archive_group}, offset={virtual_offset})"
            )
        records.append(
            {
                "branch": branch,
                "record_index": index,
                "path": c_string(data, string_table + name_offset),
                "hash": f"{u64(data, offset):016x}",
                "virtual_offset": virtual_offset,
                "stored": stored,
                "original": original,
                "compression": compression,
                "encryption0": encryption0,
                "encryption1": encryption1,
                "chunk_group": archive_group,
                "chunk_part": int(archive["part"]),
                "chunk_index": int(archive["archive_index"]),
                "chunk": str(archive["name"]),
                "offset": virtual_offset - int(archive["virtual_start"]),
            }
        )

    return {
        "path": str(path),
        "sha256": hashlib.sha256(data).hexdigest(),
        "header_flags": header_flags,
        "records": records,
        "archives": archives,
    }


def map_chunks(
    records: Iterable[dict[str, Any]],
    archives: Iterable[dict[str, Any]],
    chunk_root: Path,
) -> None:
    chunks = {item.name: item for item in chunk_root.glob("*.ssrc")}
    if not chunks:
        raise AuditError(f"No SSRC chunks found under {chunk_root}")

    archive_lookup = {str(item["name"]): item for item in archives}

    for record in records:
        offset = int(record["offset"])
        stored = int(record["stored"])
        chunk_name = str(record["chunk"])
        archive = archive_lookup.get(chunk_name)
        if archive is None:
            record["chunk"] = None
            continue

        chunk_path = chunks.get(chunk_name)
        expected_size = int(archive["stored_size"])
        if (
            chunk_path is None
            or chunk_path.stat().st_size != expected_size
            or offset + stored > expected_size
        ):
            record["chunk"] = None


def candidate_pattern(character_id: str, prefixes: list[str]) -> re.Pattern[str]:
    names = [re.escape(character_id), *(re.escape(item) for item in prefixes)]
    joined = "|".join(names)
    return re.compile(rf"(?:^|[/_])(?:{joined})(?:[/_.-]|$)", re.IGNORECASE)


def read_record_bytes(record: dict[str, Any], chunk_root: Path) -> bytes:
    chunk = record.get("chunk")
    if not chunk:
        raise AuditError(f"Record has no mapped chunk: {record['path']}")
    chunk_path = chunk_root / str(chunk)
    with chunk_path.open("rb") as stream:
        stream.seek(int(record["offset"]))
        stored = stream.read(int(record["stored"]))
    if len(stored) != int(record["stored"]):
        raise AuditError(f"Short read while auditing {record['path']}")
    compression = int(record["compression"])
    if compression == 0:
        data = stored
    elif compression == 1:
        try:
            import zstandard
        except ImportError as exc:
            raise AuditError(
                "zstandard is required for --expand-dependencies"
            ) from exc
        data = zstandard.ZstdDecompressor().decompress(
            stored, max_output_size=int(record["original"])
        )
    else:
        raise AuditError(
            f"Unsupported compression {compression} for {record['path']}"
        )
    if len(data) != int(record["original"]):
        raise AuditError(f"Size mismatch while auditing {record['path']}")
    return data


def collect_string_values(value: Any) -> set[str]:
    values: set[str] = set()
    if isinstance(value, dict):
        for child in value.values():
            values.update(collect_string_values(child))
    elif isinstance(value, list):
        for child in value:
            values.update(collect_string_values(child))
    elif isinstance(value, str) and value.strip():
        values.add(value.strip())
    return values


def dependency_stems_from_json(data: bytes) -> set[str]:
    parsed = json.loads(data.decode("utf-8-sig"))
    stems: set[str] = set()
    for value in collect_string_values(parsed):
        normalized = value.replace("\\", "/").rsplit("/", 1)[-1]
        if re.fullmatch(r"[A-Za-z0-9_./-]+", normalized):
            stem = normalized.rsplit(".", 1)[0]
            if "_" in stem and len(stem) >= 6:
                stems.add(stem)
    return stems


def dependency_stems_from_plist(data: bytes) -> set[str]:
    import plistlib

    parsed = plistlib.loads(data)
    stems: set[str] = set()
    for value in collect_string_values(parsed):
        normalized = value.replace("\\", "/").rsplit("/", 1)[-1]
        if re.fullmatch(r"[A-Za-z0-9_./-]+", normalized):
            stem = normalized.rsplit(".", 1)[0]
            if "_" in stem and len(stem) >= 6:
                stems.add(stem)
    return stems


def path_stem(path: str) -> str:
    return Path(path.replace("\\", "/")).stem.lower()


def expand_dependency_closure(
    records: list[dict[str, Any]],
    initial: list[dict[str, Any]],
    chunk_root: Path,
) -> tuple[list[dict[str, Any]], list[str]]:
    """Expand exact JSON/CFX references to matching battle resource records.

    This intentionally does not infer arbitrary resources from loose text. A
    reference stem must match a manifest basename exactly; unresolved stems
    remain visible in the summary for manual review.
    """

    by_stem: dict[str, list[dict[str, Any]]] = {}
    for record in records:
        by_stem.setdefault(path_stem(str(record["path"])), []).append(record)

    selected = {str(record["path"]): record for record in initial}
    pending = list(initial)
    inspected: set[str] = set()
    unresolved: set[str] = set()
    reference_like = re.compile(
        r"(?:^\d{5,}_|_\d{5,}(?:_|$)|(?:eff|effect|particle|shake|camera|node|"
        r"portrait|cutin|bg|screen|impact|flash|sword|fire|pat|crack|scroll))",
        re.IGNORECASE,
    )
    while pending:
        record = pending.pop()
        path = str(record["path"])
        if path in inspected:
            continue
        inspected.add(path)
        suffix = Path(path).suffix.lower()
        try:
            if suffix in {".srmd", ".brmd", ".srcs", ".srue", ".setting"}:
                stems = dependency_stems_from_json(read_record_bytes(record, chunk_root))
            elif suffix in {".cfx", ".particle"}:
                stems = dependency_stems_from_plist(read_record_bytes(record, chunk_root))
            else:
                continue
        except (json.JSONDecodeError, ValueError) as exc:
            raise AuditError(f"Cannot parse dependency source {path}: {exc}") from exc

        for stem in stems:
            matches = by_stem.get(stem.lower(), [])
            if not matches:
                if reference_like.search(stem):
                    unresolved.add(stem)
                continue
            for match in matches:
                match_path = str(match["path"])
                suffix = Path(match_path).suffix.lower()
                if suffix not in {
                    ".atlas",
                    ".scsp",
                    ".sct",
                    ".cfx",
                    ".particle",
                    ".json",
                }:
                    continue
                if match_path not in selected:
                    match["category"] = "referenced_dependency"
                    selected[match_path] = match
                    pending.append(match)

    output = sorted(selected.values(), key=lambda item: str(item["path"]))
    return output, sorted(unresolved)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def write_csv(path: Path, records: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "branch",
        "path",
        "chunk",
        "offset",
        "virtual_offset",
        "stored",
        "original",
        "compression",
        "encryption0",
        "encryption1",
        "record_index",
        "hash",
        "chunk_group",
        "chunk_part",
        "chunk_index",
    ]
    with path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(records)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--chunk-root", type=Path, required=True)
    parser.add_argument("--branch", choices=("main", "shadow"), required=True)
    parser.add_argument("--character-id", required=True)
    parser.add_argument("--prefix", action="append", default=[])
    parser.add_argument(
        "--expand-dependencies",
        action="store_true",
        help="Recursively include exact JSON/CFX/particle basename references.",
    )
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    try:
        parsed = parse_manifest(args.manifest.resolve(), args.branch)
        chunk_root = args.chunk_root.resolve()
        map_chunks(parsed["records"], parsed["archives"], chunk_root)
        pattern = candidate_pattern(args.character_id, args.prefix)
        # The import pipeline only consumes battle model/config/VFX records.
        # Restrict the initial name scan so unrelated UI, sound, card and
        # live-event assets sharing the numeric ID do not enter the payload.
        allowed_roots = {
            "camera",
            "camera_path",
            "cutin",
            "effect",
            "model",
            "model_data",
            "model_setting",
            "node",
            "particle",
        }
        candidates = []
        for item in parsed["records"]:
            path_value = str(item["path"])
            root = path_value.split("/", 1)[0]
            if root not in allowed_roots or not pattern.search(path_value):
                continue
            if root == "model" and not re.fullmatch(
                rf"model/(?:zhs/)?{re.escape(args.character_id)}"
                rf"(?:_battle_ready)?\.(?:atlas|scsp|sct)",
                path_value,
                re.IGNORECASE,
            ):
                continue
            if root in {"camera", "camera_path", "node"} and not (
                args.character_id.lower() in path_value.lower()
                or any(prefix.lower() in path_value.lower() for prefix in args.prefix)
            ):
                continue
            if root == "effect" and not (
                path_value.lower().startswith(f"effect/{args.character_id.lower()}_")
                or any(
                    path_value.lower().startswith(f"effect/{prefix.lower()}")
                    or path_value.lower().startswith(f"effect/zhs/{prefix.lower()}")
                    for prefix in args.prefix
                )
            ):
                continue
            candidates.append(item)
        for item in candidates:
            item["category"] = "name_candidate"
        unresolved_dependencies: list[str] = []
        if args.expand_dependencies:
            candidates, unresolved_dependencies = expand_dependency_closure(
                parsed["records"], candidates, chunk_root
            )
        candidates.sort(key=lambda item: str(item["path"]))
        write_json(args.output.resolve(), candidates)
        write_csv(args.output.resolve().with_suffix(".csv"), candidates)
        unresolved = [item["path"] for item in candidates if not item.get("chunk")]
        summary = {
            "manifest": parsed["path"],
            "manifest_sha256": parsed["sha256"],
            "branch": args.branch,
            "character_id": args.character_id,
            "prefixes": args.prefix,
            "candidate_count": len(candidates),
            "unresolved_chunks": unresolved,
            "unresolved_dependencies": unresolved_dependencies,
        }
        write_json(args.output.resolve().with_name("candidate-summary.json"), summary)
        print(json.dumps(summary, ensure_ascii=False, indent=2))
    except (AuditError, OSError, UnicodeDecodeError, struct.error) as exc:
        print(f"ERROR: {exc}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
