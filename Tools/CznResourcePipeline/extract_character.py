#!/usr/bin/env python3
"""Extract an audited CZN character dependency set into a Unity project.

The script is intentionally read-only with respect to the installed client.
It consumes the record locations produced by the prior SSRA audit, verifies
every byte range, and writes only to the selected external/Unity destinations.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import re
import struct
import sys
import tempfile
import xml.etree.ElementTree as ET
import zlib
from collections import Counter
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


SCRIPT_VERSION = "1.4.0"
VALID_SOURCE_BRANCHES = {"main", "shadow"}
SCSP1U_MARKER = b"scsp1u\x00\x00"
SCSP3_MARKER = b"scsp\x03\x00\x00\x00"
SCT1_HEADER_SIZE = 17
SCT1_SUPPORTED_PIXEL_FORMATS = {
    4: ("RGB888", "RGB", 3),
}
SUPPORTED_PIXEL_FORMATS = {
    19: ("ETC2_RGBA8", 4, 4),
    40: ("ASTC_4x4", 4, 4),
    44: ("ASTC_6x6", 6, 6),
    47: ("ASTC_8x8", 8, 8),
}
JSON_EXTENSIONS = {".setting", ".srcs", ".srmd", ".srue", ".brmd"}
XML_EXTENSIONS = {".cfx", ".particle"}
COPY_EXTENSIONS = {".webp"}


class PipelineError(RuntimeError):
    pass


def bootstrap_dependencies(explicit_path: Path | None) -> tuple[Any, Any, Any, Any, Any]:
    candidates: list[Path] = []
    if explicit_path:
        candidates.append(explicit_path)
    env_path = os.environ.get("CZN_PIPELINE_PYTHONPATH")
    if env_path:
        candidates.append(Path(env_path))
    candidates.append(Path(tempfile.gettempdir()) / "codex-czn-zstd")

    for candidate in candidates:
        if candidate.is_dir():
            sys.path.insert(0, str(candidate))

    try:
        import lz4.block as lz4_block
        import texture2ddecoder
        import zstandard
        from PIL import Image, ImageDraw
    except ImportError as exc:
        raise PipelineError(
            "Missing Python dependency. Run `python -m pip install -r "
            "Tools/CznResourcePipeline/requirements.txt`, or pass "
            "--dependency-path."
        ) from exc

    return zstandard, lz4_block, texture2ddecoder, Image, ImageDraw


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def safe_relative_path(value: str) -> PurePosixPath:
    normalized = value.replace("\\", "/")
    path = PurePosixPath(normalized)
    if path.is_absolute() or not path.parts or any(part in {"", ".", ".."} for part in path.parts):
        raise PipelineError(f"Unsafe record path: {value!r}")
    return path


def native_path(root: Path, relative: PurePosixPath) -> Path:
    return root.joinpath(*relative.parts)


def atomic_write_bytes(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    os.replace(temporary, path)


def atomic_write_text(path: Path, text: str) -> None:
    atomic_write_bytes(path, text.encode("utf-8"))


def atomic_write_json(path: Path, value: Any) -> None:
    text = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    atomic_write_text(path, text)


def read_record_bytes(record: dict[str, Any], gameres_root: Path, zstandard: Any) -> bytes:
    branch = str(record["branch"])
    chunk_root = gameres_root / ("chunks" if branch == "main" else f"{branch}/chunks")
    chunk_path = chunk_root / str(record["chunk"])
    if not chunk_path.is_file():
        raise PipelineError(f"Missing chunk: {chunk_path}")

    offset = int(record["offset"])
    stored_size = int(record["stored"])
    original_size = int(record["original"])
    if offset < 0 or stored_size < 0 or original_size < 0:
        raise PipelineError(f"Negative record range for {record['path']}")
    if int(record.get("encryption0", 0)) or int(record.get("encryption1", 0)):
        raise PipelineError(f"Encrypted record is unsupported: {record['path']}")

    with chunk_path.open("rb") as stream:
        stream.seek(offset)
        stored = stream.read(stored_size)
    if len(stored) != stored_size:
        raise PipelineError(
            f"Short read for {record['path']}: expected {stored_size}, got {len(stored)}"
        )

    compression = int(record["compression"])
    if compression == 0:
        data = stored
    elif compression == 1:
        data = zstandard.ZstdDecompressor().decompress(
            stored, max_output_size=original_size
        )
    else:
        raise PipelineError(
            f"Unsupported outer compression {compression} for {record['path']}"
        )

    if len(data) != original_size:
        raise PipelineError(
            f"Outer size mismatch for {record['path']}: "
            f"expected {original_size}, got {len(data)}"
        )
    return data


def decode_sct1(
    data: bytes,
    lz4_block: Any,
    Image: Any,
    atlas_declared_size: tuple[int, int] | None = None,
) -> tuple[Any, dict[str, Any]]:
    """Decode the legacy 17-byte SCT1 RGB/LZ4 wrapper.

    The selected monster dependency sets contain an older texture container
    whose four-byte magic is ``SCT\x01`` rather than ``SCT2``.  Its payload is
    raw, top-down RGB888 compressed as one LZ4 block.  Keep the accepted format
    table deliberately narrow: an unknown code must fail instead of producing
    a plausible-looking placeholder or decoding with an assumed channel order.
    """

    if len(data) < SCT1_HEADER_SIZE:
        raise PipelineError(
            f"SCT1 wrapper is truncated: {len(data)} < {SCT1_HEADER_SIZE}"
        )

    magic, pixel_format, width, height, raw_size, lz4_size = struct.unpack_from(
        "<4sBHHII", data, 0
    )
    if magic != b"SCT\x01":
        raise PipelineError(f"Invalid SCT1 magic: {magic!r}")
    if pixel_format not in SCT1_SUPPORTED_PIXEL_FORMATS:
        raise PipelineError(f"Unsupported SCT1 pixel format: {pixel_format}")
    if not width or not height:
        raise PipelineError(f"Invalid SCT1 dimensions: {width}x{height}")

    format_name, channel_order, bytes_per_pixel = SCT1_SUPPORTED_PIXEL_FORMATS[
        pixel_format
    ]
    expected_raw_size = width * height * bytes_per_pixel
    if raw_size != expected_raw_size:
        raise PipelineError(
            f"SCT1 pixel size mismatch: expected {expected_raw_size}, header {raw_size}"
        )
    if SCT1_HEADER_SIZE + lz4_size != len(data):
        raise PipelineError(
            "SCT1 LZ4 payload length mismatch: "
            f"header={lz4_size}, available={len(data) - SCT1_HEADER_SIZE}"
        )

    try:
        pixels = lz4_block.decompress(
            data[SCT1_HEADER_SIZE:], uncompressed_size=raw_size
        )
    except Exception as exc:
        raise PipelineError("SCT1 LZ4 decompression failed") from exc
    if len(pixels) != raw_size:
        raise PipelineError(
            f"SCT1 LZ4 decompressed size mismatch: {len(pixels)} != {raw_size}"
        )

    image = Image.frombytes("RGB", (width, height), pixels, "raw", channel_order)
    if atlas_declared_size is not None and atlas_declared_size != (width, height):
        raise PipelineError(
            "SCT1 atlas size mismatch: "
            f"atlas={atlas_declared_size}, texture={(width, height)}"
        )
    metadata = {
        "container": "SCT1",
        "format_id": pixel_format,
        "format": format_name,
        "channel_order": channel_order,
        "orientation": "top-down",
        "encoded_width": width,
        "encoded_height": height,
        "logical_width": width,
        "logical_height": height,
        "output_width": width,
        "output_height": height,
        "atlas_declared_width": (
            atlas_declared_size[0] if atlas_declared_size is not None else None
        ),
        "atlas_declared_height": (
            atlas_declared_size[1] if atlas_declared_size is not None else None
        ),
        "cropped": False,
        "crop_box": None,
        "crop_reason": "dimensions_match",
        "header_size": SCT1_HEADER_SIZE,
        "version": 1,
        "storage": "lz4",
        "pixel_bytes": len(pixels),
        "lz4_bytes": lz4_size,
    }
    return image, metadata


def decode_sct2(
    data: bytes,
    lz4_block: Any,
    texture2ddecoder: Any,
    Image: Any,
    atlas_declared_size: tuple[int, int] | None = None,
) -> tuple[Any, dict[str, Any]]:
    if len(data) < 72 or data[:4] != b"SCT2":
        raise PipelineError("Invalid SCT2 header")

    total_size, header_crc, header_size, version, pixel_format = struct.unpack_from(
        "<5I", data, 4
    )
    width, height, logical_width, logical_height = struct.unpack_from(
        "<4H", data, 24
    )
    flags = struct.unpack_from("<I", data, 32)[0]
    if total_size != len(data):
        raise PipelineError(f"SCT2 total size mismatch: {total_size} != {len(data)}")
    if header_size < 32 or header_size > len(data):
        raise PipelineError(f"Invalid SCT2 header size: {header_size}")
    computed_crc = zlib.crc32(data[header_size:]) & 0xFFFFFFFF
    if computed_crc != header_crc:
        raise PipelineError(
            f"SCT2 body CRC mismatch: expected {header_crc:08x}, got {computed_crc:08x}"
        )
    if not width or not height:
        raise PipelineError(f"Invalid SCT2 dimensions: {width}x{height}")
    if not logical_width or not logical_height:
        raise PipelineError(
            f"Invalid SCT2 logical dimensions: {logical_width}x{logical_height}"
        )
    if logical_width > width or logical_height > height:
        raise PipelineError(
            "SCT2 logical dimensions exceed encoded dimensions: "
            f"logical={logical_width}x{logical_height}, encoded={width}x{height}"
        )
    if pixel_format not in SUPPORTED_PIXEL_FORMATS:
        raise PipelineError(f"Unsupported SCT2 pixel format: {pixel_format}")

    format_name, block_width, block_height = SUPPORTED_PIXEL_FORMATS[pixel_format]
    expected_gpu_size = (
        ((width + block_width - 1) // block_width)
        * ((height + block_height - 1) // block_height)
        * 16
    )
    remaining = len(data) - header_size

    if remaining == expected_gpu_size:
        storage = "raw"
        gpu_data = data[header_size:]
        lz4_size = None
    else:
        if remaining < 8:
            raise PipelineError("SCT2 LZ4 wrapper is truncated")
        raw_size, lz4_size = struct.unpack_from("<2I", data, header_size)
        if raw_size != expected_gpu_size:
            raise PipelineError(
                f"SCT2 GPU size mismatch: expected {expected_gpu_size}, header {raw_size}"
            )
        if header_size + 8 + lz4_size != len(data):
            raise PipelineError("SCT2 LZ4 payload length mismatch")
        storage = "lz4"
        gpu_data = lz4_block.decompress(
            data[header_size + 8 :], uncompressed_size=raw_size
        )
        if len(gpu_data) != raw_size:
            raise PipelineError("SCT2 LZ4 decompressed size mismatch")

    if pixel_format == 19:
        bgra = texture2ddecoder.decode_etc2a8(gpu_data, width, height)
    else:
        bgra = texture2ddecoder.decode_astc(
            gpu_data, width, height, block_width, block_height
        )
    expected_bgra_size = width * height * 4
    if len(bgra) != expected_bgra_size:
        raise PipelineError(
            f"Decoded pixel size mismatch: expected {expected_bgra_size}, got {len(bgra)}"
        )

    image = Image.frombytes("RGBA", (width, height), bgra, "raw", "BGRA")
    encoded_size = (width, height)
    logical_size = (logical_width, logical_height)
    if atlas_declared_size is not None:
        if atlas_declared_size == encoded_size:
            output_size = encoded_size
            crop_reason = "atlas_declares_encoded_size"
        elif atlas_declared_size == logical_size:
            output_size = logical_size
            crop_reason = "atlas_declares_logical_size"
        else:
            raise PipelineError(
                "SCT2 atlas size matches neither encoded nor logical dimensions: "
                f"atlas={atlas_declared_size}, encoded={encoded_size}, "
                f"logical={logical_size}"
            )
    else:
        output_size = logical_size
        crop_reason = "unpaired_texture_uses_header_logical_size"

    output_width, output_height = output_size
    cropped = output_size != encoded_size
    crop_box: list[int] | None = None
    if cropped:
        alpha = image.getchannel("A")
        if output_width < width:
            right_alpha = alpha.crop((output_width, 0, width, height))
            if right_alpha.getextrema()[1] != 0:
                raise PipelineError(
                    "SCT2 right padding is not transparent; refusing logical crop: "
                    f"encoded={encoded_size}, output={output_size}"
                )
        if output_height < height:
            bottom_alpha = alpha.crop((0, output_height, output_width, height))
            if bottom_alpha.getextrema()[1] != 0:
                raise PipelineError(
                    "SCT2 bottom padding is not transparent; refusing logical crop: "
                    f"encoded={encoded_size}, output={output_size}"
                )
        crop_box = [0, 0, output_width, output_height]
        image = image.crop(tuple(crop_box))

    metadata = {
        "container": "SCT2",
        "format_id": pixel_format,
        "format": format_name,
        "encoded_width": width,
        "encoded_height": height,
        "logical_width": logical_width,
        "logical_height": logical_height,
        "output_width": output_width,
        "output_height": output_height,
        "atlas_declared_width": (
            atlas_declared_size[0] if atlas_declared_size is not None else None
        ),
        "atlas_declared_height": (
            atlas_declared_size[1] if atlas_declared_size is not None else None
        ),
        "cropped": cropped,
        "crop_box": crop_box,
        "crop_reason": crop_reason,
        "header_size": header_size,
        "header_crc": f"{header_crc:08x}",
        "version": version,
        "flags": f"{flags:08x}",
        "storage": storage,
        "gpu_bytes": len(gpu_data),
        "lz4_bytes": lz4_size,
    }
    return image, metadata


def decode_sct(
    data: bytes,
    lz4_block: Any,
    texture2ddecoder: Any,
    Image: Any,
    atlas_declared_size: tuple[int, int] | None = None,
) -> tuple[Any, dict[str, Any]]:
    if data[:4] == b"SCT2":
        return decode_sct2(
            data,
            lz4_block,
            texture2ddecoder,
            Image,
            atlas_declared_size=atlas_declared_size,
        )
    if data[:4] == b"SCT\x01":
        return decode_sct1(
            data,
            lz4_block,
            Image,
            atlas_declared_size=atlas_declared_size,
        )
    if len(data) < 4:
        raise PipelineError(f"SCT wrapper is truncated: {len(data)} < 4")
    raise PipelineError(f"Unsupported SCT magic: {data[:4]!r}")


def image_to_png(image: Any) -> bytes:
    output = io.BytesIO()
    image.save(output, format="PNG", optimize=False, compress_level=6)
    return output.getvalue()


def unpack_scsp(data: bytes, lz4_block: Any) -> tuple[bytes, dict[str, Any]]:
    if len(data) < 8:
        raise PipelineError("SCSP wrapper is truncated")
    raw_size, lz4_size = struct.unpack_from("<2I", data, 0)
    if not raw_size or not lz4_size:
        raise PipelineError(
            f"SCSP wrapper has an empty size: raw={raw_size}, lz4={lz4_size}"
        )
    if 8 + lz4_size != len(data):
        raise PipelineError(
            f"SCSP wrapper size mismatch: payload={lz4_size}, total={len(data)}"
        )
    try:
        inner = lz4_block.decompress(data[8:], uncompressed_size=raw_size)
    except Exception as exc:
        raise PipelineError("SCSP LZ4 decompression failed") from exc
    if len(inner) != raw_size:
        raise PipelineError(
            f"SCSP inner size mismatch: expected {raw_size}, got {len(inner)}"
        )
    if len(inner) < 16:
        raise PipelineError(f"SCSP inner header is truncated: {len(inner)} < 16")

    marker = inner[8:16]
    common_metadata = {
        "wrapper": "<uint32 raw_size, uint32 lz4_size, raw_lz4_data>",
        "raw_bytes": raw_size,
        "lz4_bytes": lz4_size,
        "inner_marker_hex": marker.hex(),
        "inner_sha256": sha256_hex(inner),
        "preserved_unmodified": True,
        "playable": False,
    }
    if marker == SCSP1U_MARKER:
        spine_version = "3.8.79" if b"3.8.79" in inner else None
        metadata = {
            **common_metadata,
            "format_id": "SCSP1U",
            "inner_format": "SCSP1U-private",
            "spine_version_string": spine_version,
            "converter_eligible": True,
            "reason": (
                "Private runtime serialization; extract as .scsp1u.bytes and run "
                "the dedicated SCSP1U converter before Spine or Unity can play it."
            ),
        }
    elif marker == SCSP3_MARKER:
        metadata = {
            **common_metadata,
            "format_id": "SCSP3",
            "inner_format": "SCSP-v3-private",
            "private_format_version": 3,
            "spine_version_string": None,
            "converter_eligible": False,
            "reason": (
                "Legacy CZN private SCSP v3 serialization. It is not a standard "
                "Spine .skel file and its layout is not supported by the SCSP1U "
                "converter; preserve it under UnsupportedSource without renaming "
                "it to .scsp1u.bytes."
            ),
        }
    else:
        raise PipelineError(
            "Unsupported SCSP inner marker "
            f"{marker.hex()}; audited support is limited to SCSP1U and SCSP v3"
        )
    return inner, metadata


def parse_atlas_pages(data: bytes) -> tuple[list[str], list[dict[str, Any]]]:
    text = data.decode("utf-8-sig")
    lines = text.splitlines()
    pages: list[dict[str, Any]] = []
    for index, line in enumerate(lines[:-1]):
        if not line or line[0].isspace():
            continue
        if lines[index + 1].startswith("size:"):
            if not line.lower().endswith(".sct"):
                raise PipelineError(f"Unexpected atlas page suffix: {line}")
            size_match = re.fullmatch(
                r"size:\s*(\d+)\s*,\s*(\d+)", lines[index + 1]
            )
            if size_match is None:
                raise PipelineError(
                    f"Invalid atlas page size for {line!r}: {lines[index + 1]!r}"
                )
            replacement = re.sub(r"(?i)\.sct$", ".png", line)
            pages.append(
                {
                    "line_index": index,
                    "source": line,
                    "unity": replacement,
                    "declared_width": int(size_match.group(1)),
                    "declared_height": int(size_match.group(2)),
                }
            )
    if not pages:
        raise PipelineError("No atlas page was found")
    return lines, pages


def rewrite_atlas(data: bytes) -> tuple[str, dict[str, Any]]:
    lines, parsed_pages = parse_atlas_pages(data)
    pages: list[dict[str, Any]] = []
    for parsed_page in parsed_pages:
        lines[int(parsed_page["line_index"])] = str(parsed_page["unity"])
        pages.append(
            {
                key: value
                for key, value in parsed_page.items()
                if key != "line_index"
            }
        )
    return "\n".join(lines) + "\n", {"pages": pages, "page_count": len(pages)}


def build_atlas_page_declarations(
    records: list[dict[str, Any]],
    gameres_root: Path,
    zstandard: Any,
) -> dict[tuple[str, str], dict[str, Any]]:
    """Map each selected atlas page to its source-declared texture dimensions."""

    declarations: dict[tuple[str, str], dict[str, Any]] = {}
    for record in records:
        atlas_path = safe_relative_path(str(record["path"]))
        if atlas_path.suffix.lower() != ".atlas":
            continue
        branch = str(record["branch"])
        data = read_record_bytes(record, gameres_root, zstandard)
        _, pages = parse_atlas_pages(data)
        for page in pages:
            page_path = safe_relative_path(
                (atlas_path.parent / PurePosixPath(str(page["source"]))).as_posix()
            )
            key = (branch, page_path.as_posix())
            declared_size = (
                int(page["declared_width"]),
                int(page["declared_height"]),
            )
            previous = declarations.get(key)
            if previous is not None:
                if tuple(previous["declared_size"]) != declared_size:
                    raise PipelineError(
                        "Conflicting atlas declarations for "
                        f"{branch}:{page_path.as_posix()}: "
                        f"{previous['declared_size']} vs {declared_size}"
                    )
                previous["atlas_sources"].append(atlas_path.as_posix())
                continue
            declarations[key] = {
                "declared_size": declared_size,
                "atlas_sources": [atlas_path.as_posix()],
            }
    return declarations


def normalize_json(data: bytes) -> tuple[str, dict[str, Any]]:
    parsed = json.loads(data.decode("utf-8-sig"))
    return (
        json.dumps(parsed, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        {"root_type": type(parsed).__name__},
    )


def normalize_xml(data: bytes) -> tuple[str, dict[str, Any]]:
    text = data.decode("utf-8-sig")
    root = ET.fromstring(text)
    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    if not normalized.endswith("\n"):
        normalized += "\n"
    return normalized, {"root_tag": root.tag}


def changed_suffix(path: PurePosixPath, new_suffix: str) -> PurePosixPath:
    return path.with_name(path.stem + new_suffix)


def unity_target_for(source: PurePosixPath) -> tuple[str, PurePosixPath]:
    suffix = source.suffix.lower()
    if suffix == ".sct":
        return "texture_png", PurePosixPath("SpineSource") / source.with_suffix(".png")
    if suffix == ".atlas":
        return "spine_atlas_text", PurePosixPath("SpineSource") / PurePosixPath(
            str(source) + ".txt"
        )
    if suffix == ".scsp":
        root = "SpineSource" if source.parts[0] in {"model", "effect"} else "AncillarySource"
        # The actual SCSP inner variant is only known after LZ4 decompression.
        # Reserve the normal SCSP1U path for collision checks, then dispatch to
        # SpineSource/AncillarySource or UnsupportedSource during processing.
        return "scsp_inner_bytes", PurePosixPath(root) / changed_suffix(
            source, ".scsp1u.bytes"
        )
    if suffix in JSON_EXTENSIONS:
        return "json_config", PurePosixPath("Configs") / PurePosixPath(
            str(source) + ".json"
        )
    if suffix in XML_EXTENSIONS:
        return "xml_config", PurePosixPath("Configs") / PurePosixPath(
            str(source) + ".xml"
        )
    if suffix in COPY_EXTENSIONS:
        return "binary_copy", PurePosixPath("Reference") / source
    raise PipelineError(f"Unsupported resource extension: {source}")


def scsp_unity_target(
    source: PurePosixPath,
    conversion: dict[str, Any],
) -> tuple[str, PurePosixPath]:
    """Route only verified SCSP1U payloads into converter-scanned directories."""

    format_id = conversion.get("format_id")
    if format_id == "SCSP1U":
        root = "SpineSource" if source.parts[0] in {"model", "effect"} else "AncillarySource"
        return "scsp1u_bytes", PurePosixPath(root) / changed_suffix(
            source, ".scsp1u.bytes"
        )
    if format_id == "SCSP3":
        return "scsp3_bytes", PurePosixPath("UnsupportedSource") / changed_suffix(
            source, ".scsp3.bytes"
        )
    raise PipelineError(
        f"Internal error: unsupported SCSP classification {format_id!r} for {source}"
    )


def load_selected_records(
    records_path: Path,
    branch: str,
    limit: int | None = None,
) -> list[dict[str, Any]]:
    """Load one audited record list without silently choosing branch precedence."""

    all_records = json.loads(records_path.read_text(encoding="utf-8-sig"))
    if not isinstance(all_records, list):
        raise PipelineError("Record manifest root must be an array")

    if branch == "all":
        records = list(all_records)
    else:
        records = [record for record in all_records if record.get("branch") == branch]
    records.sort(key=lambda record: (str(record.get("branch", "")), str(record.get("path", ""))))
    if limit:
        records = records[:limit]
    if not records:
        raise PipelineError(f"No records found for branch selection {branch!r}")
    return records


def preflight_records(
    records: list[dict[str, Any]],
    gameres_root: Path,
) -> dict[str, Any]:
    """Validate every source range and output mapping without reading payload bytes."""

    seen_sources: set[tuple[str, str]] = set()
    unity_targets: dict[str, tuple[str, str]] = {}
    branch_counts: Counter[str] = Counter()
    kind_counts: Counter[str] = Counter()
    stored_bytes = 0
    original_bytes = 0

    for index, record in enumerate(records):
        if not isinstance(record, dict):
            raise PipelineError(f"Record #{index} is not an object")

        missing = [
            key
            for key in ("branch", "path", "chunk", "offset", "stored", "original", "compression")
            if key not in record
        ]
        if missing:
            raise PipelineError(f"Record #{index} is missing keys: {', '.join(missing)}")

        source_branch = str(record["branch"])
        if source_branch not in VALID_SOURCE_BRANCHES:
            raise PipelineError(
                f"Unsupported source branch {source_branch!r} for record {record.get('path')!r}"
            )

        source_path = safe_relative_path(str(record["path"]))
        source_key = (source_branch, source_path.as_posix())
        if source_key in seen_sources:
            raise PipelineError(
                f"Duplicate source record in {source_branch}: {source_path.as_posix()}"
            )
        seen_sources.add(source_key)

        kind, unity_relative = unity_target_for(source_path)
        unity_key = unity_relative.as_posix().casefold()
        previous = unity_targets.get(unity_key)
        if previous is not None:
            raise PipelineError(
                "Two audited records would overwrite the same Unity output: "
                f"{previous[0]}:{previous[1]} and {source_branch}:{source_path.as_posix()} "
                f"-> {unity_relative.as_posix()}. Keep only the selected branch record."
            )
        unity_targets[unity_key] = (source_branch, source_path.as_posix())

        chunk_relative = safe_relative_path(str(record["chunk"]))
        if len(chunk_relative.parts) != 1:
            raise PipelineError(
                f"Chunk must be a filename, not a path: {record['chunk']!r}"
            )
        chunk_root = gameres_root / (
            "chunks" if source_branch == "main" else f"{source_branch}/chunks"
        )
        chunk_path = native_path(chunk_root, chunk_relative)
        if not chunk_path.is_file():
            raise PipelineError(f"Missing chunk: {chunk_path}")

        try:
            offset = int(record["offset"])
            stored = int(record["stored"])
            original = int(record["original"])
            compression = int(record["compression"])
            encryption0 = int(record.get("encryption0", 0) or 0)
            encryption1 = int(record.get("encryption1", 0) or 0)
        except (TypeError, ValueError) as exc:
            raise PipelineError(
                f"Invalid numeric fields for {source_branch}:{source_path.as_posix()}"
            ) from exc
        if offset < 0 or stored < 0 or original < 0:
            raise PipelineError(
                f"Negative source range for {source_branch}:{source_path.as_posix()}"
            )
        if offset + stored > chunk_path.stat().st_size:
            raise PipelineError(
                f"Source range exceeds {chunk_path}: {source_path.as_posix()} "
                f"offset={offset} stored={stored}"
            )
        if compression not in {0, 1}:
            raise PipelineError(
                f"Unsupported outer compression {compression} for {source_path.as_posix()}"
            )
        if encryption0 or encryption1:
            raise PipelineError(f"Encrypted record is unsupported: {source_path.as_posix()}")

        branch_counts[source_branch] += 1
        kind_counts[kind] += 1
        stored_bytes += stored
        original_bytes += original

    return {
        "record_count": len(records),
        "source_branches": sorted(branch_counts),
        "branch_counts": dict(sorted(branch_counts.items())),
        "counts": dict(sorted(kind_counts.items())),
        "stored_bytes": stored_bytes,
        "original_bytes": original_bytes,
        "unity_output_count": len(unity_targets),
    }


def ensure_output_roots_are_safe(
    gameres_root: Path,
    external_root: Path,
    unity_root: Path,
) -> None:
    """Refuse output roots that could write into the installed game or each other."""

    gameres_root = gameres_root.resolve()
    external_root = external_root.resolve()
    unity_root = unity_root.resolve()
    for label, output_root in (("external", external_root), ("unity", unity_root)):
        if output_root == gameres_root or gameres_root in output_root.parents:
            raise PipelineError(
                f"The {label} output root is inside the read-only game directory: {output_root}"
            )
    if (
        external_root == unity_root
        or external_root in unity_root.parents
        or unity_root in external_root.parents
    ):
        raise PipelineError(
            "External and Unity output roots must be separate, non-overlapping directories"
        )


def create_core_contact_sheet(
    unity_root: Path,
    external_root: Path,
    character_id: str,
    Image: Any,
    ImageDraw: Any,
) -> str | None:
    candidates = [
        (
            f"{character_id} battle model",
            unity_root / f"SpineSource/model/{character_id}.png",
        ),
        (
            f"{character_id} battle-ready model",
            unity_root / f"SpineSource/model/{character_id}_battle_ready.png",
        ),
    ]
    if any(not path.is_file() for _, path in candidates):
        return None

    canvas_width = 1600
    panel_height = 360
    canvas = Image.new("RGBA", (canvas_width, panel_height * len(candidates)), (24, 24, 28, 255))
    draw = ImageDraw.Draw(canvas)
    for panel_index, (label, path) in enumerate(candidates):
        with Image.open(path) as source_image:
            image = source_image.convert("RGBA")
            image.thumbnail((canvas_width - 32, panel_height - 50), Image.Resampling.LANCZOS)
            x = (canvas_width - image.width) // 2
            y = panel_index * panel_height + 34 + (panel_height - 50 - image.height) // 2
            canvas.alpha_composite(image, (x, y))
        draw.text((12, panel_index * panel_height + 10), label, fill=(240, 240, 240, 255))

    output_path = external_root / "Reports/core-atlas-contact-sheet.png"
    atomic_write_bytes(output_path, image_to_png(canvas))
    return output_path.relative_to(external_root).as_posix()


def render_external_readme(label: str, branches: list[str], count: int) -> str:
    branch_text = ", ".join(f"`{branch}`" for branch in branches)
    raw_lines = "\n".join(
        f"- `Raw/{branch}` preserves the original {branch} resource names and wrappers."
        for branch in branches
    )
    return f"""# {label} extracted study data

This directory contains {count} exact, decompressed records from {branch_text}
copied from the local game client's SSRC chunks. The installed client was read
only; no client file was modified.

{raw_lines}
- `Metadata/records.<branch>.json` preserves each branch snapshot.
- `Metadata/records.merged.json` is the exact combined audit snapshot used.
- `Metadata/import-manifest.json` maps every source record to its Unity output.
- `Reports/core-atlas-contact-sheet.png` previews the two core model sheets.

These files are local study material. Do not redistribute them.
"""


def render_unity_readme(label: str, count: int) -> str:
    return f"""# {label} Unity import

Imported {count} Simplified-Chinese battle dependency records.

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
"""


def process_records(args: argparse.Namespace) -> dict[str, Any]:
    records_path: Path = args.records.resolve()
    gameres_root: Path = args.gameres_root.resolve()
    external_root: Path = args.external_root.resolve()
    unity_root: Path = args.unity_root.resolve()
    if not records_path.is_file():
        raise PipelineError(f"Record manifest does not exist: {records_path}")
    if not gameres_root.is_dir():
        raise PipelineError(f"Game resource root does not exist: {gameres_root}")
    ensure_output_roots_are_safe(gameres_root, external_root, unity_root)
    records = load_selected_records(records_path, args.branch, args.limit)
    preflight = preflight_records(records, gameres_root)
    source_branches = list(preflight["source_branches"])
    language_branch = source_branches[0] if len(source_branches) == 1 else "mixed"

    if getattr(args, "dry_run", False):
        summary = {
            "dry_run": True,
            "label": args.label,
            "character_id": args.character_id,
            "branch_selection": args.branch,
            "language_branch": language_branch,
            "records": str(records_path),
            "source_gameres_root": str(gameres_root),
            "external_root": str(external_root),
            "unity_root": str(unity_root),
            **preflight,
        }
        print(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True))
        return summary

    zstandard, lz4_block, texture2ddecoder, Image, ImageDraw = bootstrap_dependencies(
        args.dependency_path
    )
    atlas_page_declarations = build_atlas_page_declarations(
        records, gameres_root, zstandard
    )

    manifest_entries: list[dict[str, Any]] = []
    kind_counts: Counter[str] = Counter()
    format_counts: Counter[str] = Counter()
    scsp_format_counts: Counter[str] = Counter()
    total_raw_bytes = 0
    total_unity_bytes = 0

    for index, record in enumerate(records, start=1):
        source_branch = str(record["branch"])
        source_path = safe_relative_path(str(record["path"]))
        data = read_record_bytes(record, gameres_root, zstandard)
        total_raw_bytes += len(data)

        raw_relative = PurePosixPath("Raw") / source_branch / source_path
        raw_output = native_path(external_root, raw_relative)
        atomic_write_bytes(raw_output, data)

        kind, unity_relative = unity_target_for(source_path)
        unity_output = native_path(unity_root, unity_relative)
        conversion: dict[str, Any]

        if kind == "texture_png":
            atlas_declaration = atlas_page_declarations.get(
                (source_branch, source_path.as_posix())
            )
            atlas_declared_size = (
                tuple(atlas_declaration["declared_size"])
                if atlas_declaration is not None
                else None
            )
            image, conversion = decode_sct(
                data,
                lz4_block,
                texture2ddecoder,
                Image,
                atlas_declared_size=atlas_declared_size,
            )
            conversion["atlas_sources"] = (
                list(atlas_declaration["atlas_sources"])
                if atlas_declaration is not None
                else []
            )
            unity_data = image_to_png(image)
            format_counts[conversion["format"]] += 1
        elif kind == "spine_atlas_text":
            text, conversion = rewrite_atlas(data)
            unity_data = text.encode("utf-8")
        elif kind == "scsp_inner_bytes":
            unity_data, conversion = unpack_scsp(data, lz4_block)
            kind, unity_relative = scsp_unity_target(source_path, conversion)
            unity_output = native_path(unity_root, unity_relative)
            scsp_format_counts[str(conversion["format_id"])] += 1
        elif kind == "json_config":
            text, conversion = normalize_json(data)
            unity_data = text.encode("utf-8")
        elif kind == "xml_config":
            text, conversion = normalize_xml(data)
            unity_data = text.encode("utf-8")
        elif kind == "binary_copy":
            unity_data = data
            conversion = {"format": source_path.suffix.lower().lstrip(".")}
        else:
            raise AssertionError(kind)

        atomic_write_bytes(unity_output, unity_data)
        total_unity_bytes += len(unity_data)
        kind_counts[kind] += 1

        source_record = {
            key: record.get(key)
            for key in (
                "branch",
                "record_index",
                "path",
                "hash",
                "virtual_offset",
                "chunk",
                "chunk_index",
                "chunk_part",
                "chunk_group",
                "offset",
                "stored",
                "original",
                "compression",
                "encryption0",
                "encryption1",
                "category",
            )
        }
        manifest_entries.append(
            {
                "source": source_record,
                "raw_output": raw_relative.as_posix(),
                "raw_sha256": sha256_hex(data),
                "kind": kind,
                "unity_output": unity_relative.as_posix(),
                "unity_bytes": len(unity_data),
                "unity_sha256": sha256_hex(unity_data),
                "conversion": conversion,
            }
        )

        if args.progress_every and (index % args.progress_every == 0 or index == len(records)):
            print(
                f"[{index:>3}/{len(records)}] {source_branch}:{source_path}",
                flush=True,
            )

    contact_sheet = create_core_contact_sheet(
        unity_root, external_root, args.character_id, Image, ImageDraw
    )
    filtered_records = [entry["source"] for entry in manifest_entries]
    manifest = {
        "schema": "czn-character-import-manifest-v1",
        "pipeline_version": SCRIPT_VERSION,
        "label": args.label,
        "character_id": args.character_id,
        "language_branch": language_branch,
        "source_branches": source_branches,
        "branch_counts": preflight["branch_counts"],
        "source_gameres_root": str(gameres_root),
        "record_count": len(records),
        "counts": dict(sorted(kind_counts.items())),
        "texture_formats": dict(sorted(format_counts.items())),
        "scsp_formats": dict(sorted(scsp_format_counts.items())),
        "unsupported_scsp_count": sum(
            count
            for format_id, count in scsp_format_counts.items()
            if format_id != "SCSP1U"
        ),
        "raw_bytes": total_raw_bytes,
        "unity_bytes": total_unity_bytes,
        "playable": False,
        "playable_reason": (
            "The decompressed skeleton data is private SCSP1U, not standard "
            "Spine JSON or binary."
        ),
        "contact_sheet": contact_sheet,
        "entries": manifest_entries,
    }

    for source_branch in source_branches:
        atomic_write_json(
            external_root / f"Metadata/records.{source_branch}.json",
            [
                record
                for record in filtered_records
                if record.get("branch") == source_branch
            ],
        )
    atomic_write_json(
        external_root / "Metadata/records.merged.json", filtered_records
    )
    atomic_write_json(external_root / "Metadata/import-manifest.json", manifest)
    atomic_write_json(unity_root / "Metadata/import-manifest.json", manifest)
    atomic_write_text(
        external_root / "README.md",
        render_external_readme(args.label, source_branches, len(records)),
    )
    atomic_write_text(
        unity_root / "README.md", render_unity_readme(args.label, len(records))
    )

    summary = {
        key: manifest[key]
        for key in (
            "label",
            "character_id",
            "language_branch",
            "source_branches",
            "branch_counts",
            "record_count",
            "counts",
            "texture_formats",
            "scsp_formats",
            "unsupported_scsp_count",
            "raw_bytes",
            "unity_bytes",
            "playable",
            "playable_reason",
            "contact_sheet",
        )
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True))
    return manifest


def build_argument_parser() -> argparse.ArgumentParser:
    project_root = Path(__file__).resolve().parents[2]
    default_records = (
        Path(tempfile.gettempdir())
        / "codex-czn-30093-audit"
        / "complete_records.json"
    )
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--records", type=Path, default=default_records)
    parser.add_argument(
        "--gameres-root",
        type=Path,
        default=Path(
            r"F:\WeGameApps\rail_apps\czn(2002460)\bin\appdata\prod\gameres"
        ),
    )
    parser.add_argument(
        "--branch",
        choices=("main", "shadow", "all"),
        default="main",
        help=(
            "Select one source branch, or 'all' to consume the audited main/shadow "
            "records together without implicit precedence."
        ),
    )
    parser.add_argument("--label", default="Heidemarie_30093")
    parser.add_argument("--character-id", default="30093")
    parser.add_argument(
        "--external-root",
        type=Path,
        default=project_root / "External/CZN/Heidemarie_30093",
    )
    parser.add_argument(
        "--unity-root",
        type=Path,
        default=project_root / "Assets/Imported/CZN/Heidemarie_30093",
    )
    parser.add_argument("--dependency-path", type=Path)
    parser.add_argument("--limit", type=int, help="Process only the first N records for testing")
    parser.add_argument("--progress-every", type=int, default=25)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate record ranges and output collisions without reading or writing payloads.",
    )
    return parser


def main() -> int:
    parser = build_argument_parser()
    args = parser.parse_args()
    try:
        process_records(args)
    except (PipelineError, OSError, ValueError, json.JSONDecodeError, ET.ParseError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
