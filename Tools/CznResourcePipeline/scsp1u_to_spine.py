#!/usr/bin/env python3
"""Convert decompressed CZN SCSP1U skeletons to standard Spine 3.8 JSON.

The converter is intentionally offline: it reads only already-extracted
``*.scsp1u.bytes`` files.  It does not start, hook, or modify the game client.

Examples from the Unity project root::

    py Tools/CznResourcePipeline/scsp1u_to_spine.py \
      Assets/Imported/CZN/Heidemarie_30093/SpineSource \
      --report Assets/Imported/CZN/Heidemarie_30093/Metadata/spine-json-conversion-report.json

    py Tools/CznResourcePipeline/scsp1u_to_spine.py \
      Assets/Imported/CZN/Heidemarie_30093/SpineSource/model/30093.scsp1u.bytes

For a source named ``foo.scsp1u.bytes``, the default output is ``foo.json``
beside the source.  The original SCSP1U, atlas text, and PNG are never changed.
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
from typing import Any

from emit_spine_animations import emit_animations
from probe_scsp1u import ParseError, Reader, parse_file


SCSP_SUFFIX = ".scsp1u.bytes"
CONVERTER_VERSION = 2

# SCSP1U comes from the game's spine-cpp runtime, whose TransformMode enum is
# sequential.  spine-csharp 3.8 represents the same modes with different
# internal [Flags] values (0, 7, 1, 2, 6); those values must not be applied to
# the serialized C++ ordinal.  Standard Spine JSON uses the names below, so
# spine-unity maps them to its own runtime representation when importing.
TRANSFORM_MODES = {
    0: "normal",
    1: "onlyTranslation",
    2: "noRotationOrReflection",
    3: "noScale",
    4: "noScaleOrReflection",
}
BLEND_MODES = {0: "normal", 1: "additive", 2: "multiply", 3: "screen"}
POSITION_MODES = {0: "fixed", 1: "percent"}
SPACING_MODES = {0: "length", 1: "fixed", 2: "percent"}
ROTATE_MODES = {0: "tangent", 1: "chain", 2: "chainScale"}


def string_value(value: Any) -> str | None:
    if isinstance(value, dict) and "value" in value:
        return value["value"]
    if value is None or isinstance(value, str):
        return value
    raise TypeError(f"expected string reference, got {value!r}")


def clean_number(value: float) -> int | float:
    if not math.isfinite(value):
        raise ValueError(f"non-finite number {value}")
    if abs(value) < 5e-7:
        return 0
    rounded = round(value, 6)
    return int(rounded) if float(rounded).is_integer() else rounded


def differs(value: float, default: float) -> bool:
    return abs(value - default) >= 5e-7


def color_hex(values: list[float], channels: int) -> str:
    if len(values) != channels:
        raise ValueError(f"color expected {channels} channels, got {len(values)}")
    quantized = [max(0, min(255, int(value * 255 + 0.5))) for value in values]
    return "".join(f"{value:02x}" for value in quantized)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def output_for(source: Path, input_root: Path, output_root: Path | None) -> Path:
    if not source.name.endswith(SCSP_SUFFIX):
        raise ValueError(f"source name does not end with {SCSP_SUFFIX}: {source}")
    name = source.name[: -len(SCSP_SUFFIX)] + ".json"
    if output_root is None:
        return source.with_name(name)
    if input_root.is_file():
        if output_root.suffix.lower() == ".json":
            return output_root
        return output_root / name
    return output_root / source.relative_to(input_root).with_name(name)


def skeleton_metadata(source: Path) -> dict[str, Any]:
    reader = Reader(source)
    version_offset = struct.unpack_from("<I", reader.data, 0x56)[0]
    version = reader.string(version_offset) or "3.8.79"
    if version.endswith(".scsp"):
        version = version[: -len(".scsp")]
    width, height = struct.unpack_from("<ff", reader.data, 0x16)
    fps = struct.unpack_from("<f", reader.data, 0x2E)[0]
    result: dict[str, Any] = {
        "hash": reader.string(0) or "",
        "spine": version,
        "images": "./images/",
        "audio": "",
    }
    if differs(width, 0):
        result["width"] = clean_number(width)
    if differs(height, 0):
        result["height"] = clean_number(height)
    if differs(fps, 30):
        result["fps"] = clean_number(fps)
    return result


def emit_bones(records: dict[str, Any]) -> list[dict[str, Any]]:
    source = records["bones"]
    result: list[dict[str, Any]] = []
    for expected_index, item in enumerate(source):
        if item["index"] != expected_index:
            raise ValueError(f"bone index {item['index']} != {expected_index}")
        name = string_value(item["name"])
        if name is None:
            raise ValueError(f"bone {expected_index} has no name")
        bone: dict[str, Any] = {"name": name}
        parent_index = item["parent_index"]
        if parent_index != 0xFFFF:
            if parent_index >= expected_index:
                raise ValueError(f"invalid parent {parent_index} for bone {expected_index}")
            bone["parent"] = string_value(source[parent_index]["name"])
        for key, source_key, default in (
            ("length", "length", 0.0),
            ("x", "x", 0.0),
            ("y", "y", 0.0),
            ("rotation", "rotation", 0.0),
            ("scaleX", "scale_x", 1.0),
            ("scaleY", "scale_y", 1.0),
            ("shearX", "shear_x", 0.0),
            ("shearY", "shear_y", 0.0),
        ):
            value = item[source_key]
            if differs(value, default):
                bone[key] = clean_number(value)
        mode = item["transform_mode"]
        if mode:
            try:
                bone["transform"] = TRANSFORM_MODES[mode]
            except KeyError as exc:
                raise ValueError(f"unknown transform mode {mode} on bone {name}") from exc
        if item["skin_required"]:
            bone["skin"] = True
        result.append(bone)
    return result


def emit_slots(records: dict[str, Any], bones: list[dict[str, Any]]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for expected_index, item in enumerate(records["slots"]):
        if item["index"] != expected_index:
            raise ValueError(f"slot index {item['index']} != {expected_index}")
        name = string_value(item["name"])
        bone_index = item["bone_index"]
        if name is None or not 0 <= bone_index < len(bones):
            raise ValueError(f"invalid slot {expected_index}")
        slot: dict[str, Any] = {"name": name, "bone": bones[bone_index]["name"]}
        light = color_hex(item["light_rgba"], 4)
        if light != "ffffffff":
            slot["color"] = light
        if item["has_dark_color"]:
            slot["dark"] = color_hex(item["dark_rgba"][:3], 3)
        attachment = string_value(item["attachment_name"])
        if attachment is not None:
            slot["attachment"] = attachment
        blend = item["blend_mode"]
        if blend:
            try:
                slot["blend"] = BLEND_MODES[blend]
            except KeyError as exc:
                raise ValueError(f"unknown blend mode {blend} on slot {name}") from exc
        result.append(slot)
    return result


def emit_ik_constraints(
    records: dict[str, Any], bones: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for item in records["ik_constraints"]:
        name = string_value(item["name"])
        target = item["target_bone_index"]
        if name is None or not 0 <= target < len(bones):
            raise ValueError(f"invalid IK constraint {name!r}")
        constraint: dict[str, Any] = {
            "name": name,
            "bones": [bones[index]["name"] for index in item["bone_indices"]],
            "target": bones[target]["name"],
        }
        if item["order"]:
            constraint["order"] = item["order"]
        if item["skin_required"]:
            constraint["skin"] = True
        if differs(item["mix"], 1):
            constraint["mix"] = clean_number(item["mix"])
        if differs(item["softness"], 0):
            constraint["softness"] = clean_number(item["softness"])
        if item["bend_direction"] <= 0:
            constraint["bendPositive"] = False
        for key in ("compress", "stretch", "uniform"):
            if item[key]:
                constraint[key] = True
        result.append(constraint)
    return result


def emit_transform_constraints(
    records: dict[str, Any], bones: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for item in records["transform_constraints"]:
        name = string_value(item["name"])
        target = item["target_bone_index"]
        if name is None or not 0 <= target < len(bones):
            raise ValueError(f"invalid transform constraint {name!r}")
        constraint: dict[str, Any] = {
            "name": name,
            "bones": [bones[index]["name"] for index in item["bone_indices"]],
            "target": bones[target]["name"],
        }
        if item["order"]:
            constraint["order"] = item["order"]
        if item["skin_required"]:
            constraint["skin"] = True
        if item["local"]:
            constraint["local"] = True
        if item["relative"]:
            constraint["relative"] = True
        for key, source_key, default in (
            ("rotation", "offset_rotation", 0.0),
            ("x", "offset_x", 0.0),
            ("y", "offset_y", 0.0),
            ("scaleX", "offset_scale_x", 0.0),
            ("scaleY", "offset_scale_y", 0.0),
            ("shearY", "offset_shear_y", 0.0),
            ("rotateMix", "rotate_mix", 1.0),
            ("translateMix", "translate_mix", 1.0),
            ("scaleMix", "scale_mix", 1.0),
            ("shearMix", "shear_mix", 1.0),
        ):
            value = item[source_key]
            if differs(value, default):
                constraint[key] = clean_number(value)
        result.append(constraint)
    return result


def emit_path_constraints(
    records: dict[str, Any], bones: list[dict[str, Any]], slots: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for item in records["path_constraints"]:
        name = string_value(item["name"])
        target = item["target_slot_index"]
        if name is None or not 0 <= target < len(slots):
            raise ValueError(f"invalid path constraint {name!r}")
        constraint: dict[str, Any] = {
            "name": name,
            "bones": [bones[index]["name"] for index in item["bone_indices"]],
            "target": slots[target]["name"],
        }
        if item["order"]:
            constraint["order"] = item["order"]
        if item["skin_required"]:
            constraint["skin"] = True
        try:
            position_mode = POSITION_MODES[item["position_mode"]]
            spacing_mode = SPACING_MODES[item["spacing_mode"]]
            rotate_mode = ROTATE_MODES[item["rotate_mode"]]
        except KeyError as exc:
            raise ValueError(f"unknown path mode on constraint {name}") from exc
        if position_mode != "percent":
            constraint["positionMode"] = position_mode
        if spacing_mode != "length":
            constraint["spacingMode"] = spacing_mode
        if rotate_mode != "tangent":
            constraint["rotateMode"] = rotate_mode
        for key, source_key, default in (
            ("rotation", "offset_rotation", 0.0),
            ("position", "position", 0.0),
            ("spacing", "spacing", 0.0),
            ("rotateMix", "rotate_mix", 1.0),
            ("translateMix", "translate_mix", 1.0),
        ):
            value = item[source_key]
            if differs(value, default):
                constraint[key] = clean_number(value)
        result.append(constraint)
    return result


def json_vertices(
    bone_values: list[int],
    vertex_values: list[float],
    world_vertices_length: int,
    skeleton_bone_count: int,
) -> list[int | float]:
    if world_vertices_length % 2:
        raise ValueError(f"odd world vertices length {world_vertices_length}")
    if not bone_values:
        if len(vertex_values) != world_vertices_length:
            raise ValueError(
                f"unweighted vertex length {len(vertex_values)} != {world_vertices_length}"
            )
        return [clean_number(value) for value in vertex_values]

    result: list[int | float] = []
    bone_cursor = 0
    vertex_cursor = 0
    vertex_count = 0
    while bone_cursor < len(bone_values):
        influence_count = bone_values[bone_cursor]
        bone_cursor += 1
        result.append(influence_count)
        for _ in range(influence_count):
            if bone_cursor >= len(bone_values) or vertex_cursor + 3 > len(vertex_values):
                raise ValueError("truncated weighted vertex data")
            bone_index = bone_values[bone_cursor]
            bone_cursor += 1
            if not 0 <= bone_index < skeleton_bone_count:
                raise ValueError(f"weighted vertex refers to bone {bone_index}")
            result.append(bone_index)
            result.extend(
                clean_number(value) for value in vertex_values[vertex_cursor : vertex_cursor + 3]
            )
            vertex_cursor += 3
        vertex_count += 1
    if vertex_cursor != len(vertex_values):
        raise ValueError(f"unused weighted values: {len(vertex_values) - vertex_cursor}")
    if vertex_count * 2 != world_vertices_length:
        raise ValueError(
            f"weighted vertex count {vertex_count} != world length {world_vertices_length}"
        )
    return result


def add_attachment_name(target: dict[str, Any], key: str, payload: dict[str, Any]) -> str:
    internal = string_value(payload["name"])
    if internal is None:
        raise ValueError(f"attachment {key!r} has no internal name")
    if internal != key:
        target["name"] = internal
    return internal


def emit_region_attachment(key: str, payload: dict[str, Any]) -> dict[str, Any]:
    result: dict[str, Any] = {"type": "region"}
    internal = add_attachment_name(result, key, payload)
    path = string_value(payload["path"])
    if path is not None and path != internal:
        result["path"] = path
    for key_name, source_key, default in (
        ("x", "x", 0.0),
        ("y", "y", 0.0),
        ("rotation", "rotation", 0.0),
        ("scaleX", "scale_x", 1.0),
        ("scaleY", "scale_y", 1.0),
    ):
        value = payload[source_key]
        if differs(value, default):
            result[key_name] = clean_number(value)
    # RegionAttachment defaults to 32x32, so explicit zero is significant.
    result["width"] = clean_number(payload["width"])
    result["height"] = clean_number(payload["height"])
    tint = color_hex(payload["color_rgba"], 4)
    if tint != "ffffffff":
        result["color"] = tint
    return result


def emit_mesh_attachment(
    key: str, payload: dict[str, Any], skeleton_bone_count: int
) -> dict[str, Any]:
    result: dict[str, Any] = {"type": "mesh"}
    internal = add_attachment_name(result, key, payload)
    path = string_value(payload["parent_attachment_name"])
    if path is not None and path != internal:
        result["path"] = path
    if payload["parent_ref"] != -1 or payload["deform_ref"] != 0 or payload["tail_flag"]:
        raise NotImplementedError(f"linked mesh metadata found on {key!r}")
    if payload["deform_skin_index"] != 0 or payload["parent_skin_index"] != 0:
        raise NotImplementedError(f"non-default mesh skin reference found on {key!r}")
    deform_name = string_value(payload["deform_attachment_name"])
    if deform_name != internal:
        raise ValueError(f"mesh {key!r} deform name {deform_name!r} != {internal!r}")
    if len(payload["uvs"]) != payload["world_vertices_length"]:
        raise ValueError(f"mesh {key!r} UV/world vertex lengths differ")
    result["uvs"] = [clean_number(value) for value in payload["uvs"]]
    result["triangles"] = payload["triangles"]
    result["vertices"] = json_vertices(
        payload["bones"],
        payload["vertices"],
        payload["world_vertices_length"],
        skeleton_bone_count,
    )
    if payload["hull_length"]:
        result["hull"] = payload["hull_length"]
    if payload["edges"]:
        result["edges"] = payload["edges"]
    if differs(payload["width"], 0):
        result["width"] = clean_number(payload["width"])
    if differs(payload["height"], 0):
        result["height"] = clean_number(payload["height"])
    tint = color_hex(payload["color_rgba"], 4)
    if tint != "ffffffff":
        result["color"] = tint
    return result


def emit_path_attachment(
    key: str, payload: dict[str, Any], skeleton_bone_count: int
) -> dict[str, Any]:
    result: dict[str, Any] = {"type": "path"}
    add_attachment_name(result, key, payload)
    world_length = payload["world_vertices_length"]
    result["vertexCount"] = world_length // 2
    result["vertices"] = json_vertices(
        payload["bones"], payload["vertices"], world_length, skeleton_bone_count
    )
    result["lengths"] = [clean_number(value) for value in payload["lengths"]]
    if payload["closed"]:
        result["closed"] = True
    if not payload["constant_speed"]:
        result["constantSpeed"] = False
    return result


def emit_clipping_attachment(
    key: str,
    payload: dict[str, Any],
    skeleton_bone_count: int,
    slots: list[dict[str, Any]],
) -> dict[str, Any]:
    result: dict[str, Any] = {"type": "clipping"}
    internal = add_attachment_name(result, key, payload)
    deform_name = string_value(payload["deform_attachment_name"])
    if deform_name != internal:
        raise ValueError(f"clip {key!r} deform name {deform_name!r} != {internal!r}")
    end = payload["end_slot_index"]
    if not 0 <= end < len(slots):
        raise ValueError(f"clip {key!r} has invalid end slot {end}")
    world_length = payload["world_vertices_length"]
    result["end"] = slots[end]["name"]
    result["vertexCount"] = world_length // 2
    result["vertices"] = json_vertices(
        payload["bones"], payload["vertices"], world_length, skeleton_bone_count
    )
    return result


def emit_skins(
    records: dict[str, Any], bones: list[dict[str, Any]], slots: list[dict[str, Any]]
) -> tuple[list[dict[str, Any]], dict[str, int]]:
    result: list[dict[str, Any]] = []
    counts: collections.Counter[str] = collections.Counter()
    for skin_index, source_skin in enumerate(records["skins"]):
        name = string_value(source_skin["name"])
        if name is None:
            raise ValueError(f"skin {skin_index} has no name")
        skin: dict[str, Any] = {"name": name}
        if source_skin["bone_indices"]:
            skin["bones"] = [bones[index]["name"] for index in source_skin["bone_indices"]]
        if source_skin["constraint_indices"]:
            # The current 30093 corpus has no skin-owned constraints.  SCSP1U
            # stores one untyped index vector, while standard JSON separates
            # IK/transform/path names; refuse to guess if a future file uses it.
            raise NotImplementedError(f"skin-owned constraints found in skin {name!r}")
        attachments: dict[str, dict[str, Any]] = {}
        for entry in source_skin["attachments"]:
            slot_index = entry["slot_index"]
            key = string_value(entry["key_name"])
            if key is None or not 0 <= slot_index < len(slots):
                raise ValueError(f"invalid attachment in skin {name!r}")
            slot_name = slots[slot_index]["name"]
            slot_attachments = attachments.setdefault(slot_name, {})
            if key in slot_attachments:
                raise ValueError(f"duplicate attachment {name}/{slot_name}/{key}")
            payload = entry["payload"]
            kind = entry["type"]
            if kind == 0:
                attachment = emit_region_attachment(key, payload)
                counts["region"] += 1
            elif kind == 2:
                attachment = emit_mesh_attachment(key, payload, len(bones))
                counts["mesh"] += 1
            elif kind == 4:
                attachment = emit_path_attachment(key, payload, len(bones))
                counts["path"] += 1
            elif kind == 6:
                attachment = emit_clipping_attachment(key, payload, len(bones), slots)
                counts["clipping"] += 1
            else:
                raise NotImplementedError(f"attachment type {kind} on {key!r}")
            slot_attachments[key] = attachment
        skin["attachments"] = attachments
        result.append(skin)
    return result, dict(sorted(counts.items()))


def emit_event_data(records: dict[str, Any]) -> dict[str, dict[str, Any]]:
    """Emit SCSP1U EventData defaults as the Spine 3.8 root `events` map."""

    result: dict[str, dict[str, Any]] = {}
    for expected_index, item in enumerate(records.get("event_data", [])):
        if item["index"] != expected_index:
            raise ValueError(f"event data index {item['index']} != {expected_index}")
        name = string_value(item["name"])
        if not name or name in result:
            raise ValueError(f"invalid/duplicate event data name {name!r}")

        event: dict[str, Any] = {}
        if item["int"]:
            event["int"] = item["int"]
        if differs(item["float"], 0.0):
            event["float"] = clean_number(item["float"])
        string_default = string_value(item["string"])
        if string_default is not None:
            event["string"] = string_default

        audio = string_value(item["audio"])
        if audio is not None:
            event["audio"] = audio
            if differs(item["volume"], 1.0):
                event["volume"] = clean_number(item["volume"])
            if differs(item["balance"], 0.0):
                event["balance"] = clean_number(item["balance"])
        elif differs(item["volume"], 1.0) or differs(item["balance"], 0.0):
            raise ValueError(
                f"event data {name!r} has audio-only defaults without an audio path"
            )
        result[name] = event
    return result


def emit_spine_json(source: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    parsed = parse_file(source, retain_records=True)
    records = parsed["records"]
    bones = emit_bones(records)
    slots = emit_slots(records, bones)
    ik = emit_ik_constraints(records, bones)
    transforms = emit_transform_constraints(records, bones)
    paths = emit_path_constraints(records, bones, slots)
    skins, attachment_counts = emit_skins(records, bones, slots)
    event_data = emit_event_data(records)
    animations, animation_report = emit_animations(records)

    root: dict[str, Any] = {
        "skeleton": skeleton_metadata(source),
        "bones": bones,
        "slots": slots,
    }
    if ik:
        root["ik"] = ik
    if transforms:
        root["transform"] = transforms
    if paths:
        root["path"] = paths
    root["skins"] = skins
    if event_data:
        root["events"] = event_data
    root["animations"] = animations

    report = {
        "body_end_valid": parsed["validated_body_end"],
        "bones": len(bones),
        "slots": len(slots),
        "ik_constraints": len(ik),
        "transform_constraints": len(transforms),
        "path_constraints": len(paths),
        "skins": len(skins),
        "event_data": len(event_data),
        "event_data_names": list(event_data),
        "attachments": attachment_counts,
        "animations": animation_report["animation_count"],
        "timeline_counts": animation_report["timeline_counts"],
        "curve_counts": animation_report["curve_counts"],
        "duplicate_attachment_replacements": animation_report[
            "duplicate_attachment_replacements"
        ],
    }
    return root, report


def write_json_atomic(path: Path, value: Any, pretty: bool) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(
        json.dumps(
            value,
            ensure_ascii=False,
            indent=2 if pretty else None,
            separators=None if pretty else (",", ":"),
        )
        + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def display_path(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return str(path)


def convert_one(
    source: Path, output: Path, report_root: Path, pretty: bool
) -> dict[str, Any]:
    root, report = emit_spine_json(source)
    write_json_atomic(output, root, pretty)
    return {
        "source": display_path(source, report_root),
        "source_bytes": source.stat().st_size,
        "source_sha256": sha256_file(source),
        "output": display_path(output, report_root),
        "output_bytes": output.stat().st_size,
        "output_sha256": sha256_file(output),
        **report,
    }


def run_batch(
    input_path: Path,
    output_path: Path | None,
    report_path: Path | None,
    pretty: bool,
) -> tuple[dict[str, Any], int]:
    input_path = input_path.resolve()
    output_path = output_path.resolve() if output_path else None
    if input_path.is_dir():
        sources = sorted(input_path.rglob(f"*{SCSP_SUFFIX}"))
        report_root = input_path
    elif input_path.is_file():
        sources = [input_path]
        report_root = input_path.parent
    else:
        raise FileNotFoundError(input_path)
    if not sources:
        raise FileNotFoundError(f"no *{SCSP_SUFFIX} files under {input_path}")

    started = dt.datetime.now(dt.timezone.utc)
    files: list[dict[str, Any]] = []
    failures: list[dict[str, str]] = []
    total_attachments: collections.Counter[str] = collections.Counter()
    total_timelines: collections.Counter[str] = collections.Counter()
    total_curves: collections.Counter[str] = collections.Counter()
    total_animations = 0
    total_event_data = 0
    for source in sources:
        output = output_for(source, input_path, output_path)
        try:
            entry = convert_one(source, output, report_root, pretty)
            files.append(entry)
            total_attachments.update(entry["attachments"])
            total_timelines.update(entry["timeline_counts"])
            total_curves.update(entry["curve_counts"])
            total_animations += entry["animations"]
            total_event_data += entry["event_data"]
            print(f"OK {display_path(source, report_root)} -> {display_path(output, report_root)}")
        except (OSError, ValueError, KeyError, IndexError, ParseError, NotImplementedError) as exc:
            failure = {
                "source": display_path(source, report_root),
                "error_type": type(exc).__name__,
                "error": str(exc),
            }
            failures.append(failure)
            print(f"ERROR {failure['source']}: {failure['error_type']}: {exc}", file=sys.stderr)

    finished = dt.datetime.now(dt.timezone.utc)
    report: dict[str, Any] = {
        "converter": "CZN SCSP1U to Spine 3.8 JSON",
        "converter_version": CONVERTER_VERSION,
        "started_utc": started.isoformat(),
        "finished_utc": finished.isoformat(),
        "elapsed_seconds": round((finished - started).total_seconds(), 3),
        "input": str(input_path),
        "requested_files": len(sources),
        "successful": len(files),
        "failed": len(failures),
        "all_body_ends_valid": not failures and all(item["body_end_valid"] for item in files),
        "totals": {
            "animations": total_animations,
            "event_data": total_event_data,
            "attachments": dict(sorted(total_attachments.items())),
            "timelines": dict(sorted(total_timelines.items())),
            "curves": dict(sorted(total_curves.items())),
        },
        "known_limits": [
            "EventData defaults are emitted; EventTimeline type 7 remains unsupported because no audited file exercises its private layout.",
            "Attachment types bounding box, linked mesh, and point are valid Spine types but absent from the audited corpus.",
            "CLI round-trips may normalize weighted mesh/deform data; generated JSON is canonical.",
        ],
        "failures": failures,
        "files": files,
    }
    if report_path:
        write_json_atomic(report_path.resolve(), report, True)
    return report, 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="SCSP1U file or directory tree")
    parser.add_argument(
        "--output",
        type=Path,
        help="JSON file for one input, or output root for a directory (default: beside source)",
    )
    parser.add_argument("--report", type=Path, help="write a batch conversion report")
    parser.add_argument("--pretty", action="store_true", help="indent generated skeleton JSON")
    args = parser.parse_args()
    try:
        report, exit_code = run_batch(args.input, args.output, args.report, args.pretty)
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    print(
        json.dumps(
            {
                "requested_files": report["requested_files"],
                "successful": report["successful"],
                "failed": report["failed"],
                "totals": report["totals"],
                "report": str(args.report.resolve()) if args.report else None,
            },
            ensure_ascii=False,
            indent=2,
        )
    )
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
