#!/usr/bin/env python3
"""Emit Spine 3.8 JSON animations from probe_scsp1u.py retained records.

This module deliberately consumes the canonical parser's records rather than
seeking to a model-specific animation offset.  That makes animation location
dynamic: it follows bones, constraints, slots, skins, and EventData in the
source file, exactly as ``probe_scsp1u.parse_file(..., retain_records=True)``
does.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import math
from collections import Counter
from pathlib import Path
from typing import Any


HERE = Path(__file__).resolve().parent
PROBE_PATH = HERE / "probe_scsp1u.py"

FRAME_WIDTHS = {
    0: 2,   # rotate: time, degrees
    1: 3,   # translate: time, x, y
    2: 3,   # scale: time, x, y
    3: 3,   # shear: time, x, y
    5: 5,   # color: time, r, g, b, a
    9: 6,   # IK: time, mix, softness, bend, compress, stretch
    10: 5,  # transform: time, rotateMix, translateMix, scaleMix, shearMix
    11: 2,  # path position: time, value
    12: 2,  # path spacing: time, value
    13: 3,  # path mix: time, rotateMix, translateMix
    14: 8,  # two color: time, light RGBA, dark RGB
}

TYPE_NAMES = {
    0: "rotate",
    1: "translate",
    2: "scale",
    3: "shear",
    4: "attachment",
    5: "color",
    6: "deform",
    7: "event",
    8: "drawOrder",
    9: "ik",
    10: "transform",
    11: "pathPosition",
    12: "pathSpacing",
    13: "pathMix",
    14: "twoColor",
}


def ref(value: Any) -> str | None:
    if isinstance(value, dict) and "value" in value:
        return value["value"]
    if value is None or isinstance(value, str):
        return value
    raise TypeError(f"expected string reference, got {value!r}")


def clean_number(value: float) -> int | float:
    if not math.isfinite(value):
        raise ValueError(f"non-finite value {value}")
    if abs(value) < 5e-7:
        return 0
    rounded = round(value, 6)
    return int(rounded) if float(rounded).is_integer() else rounded


def add_time(frame: dict[str, Any], value: float) -> None:
    value = clean_number(value)
    if value != 0:
        frame["time"] = value


def color_hex(values: list[float], channels: int) -> str:
    if len(values) != channels:
        raise ValueError(f"color expected {channels} channels, got {len(values)}")
    result = [max(0, min(255, int(value * 255 + 0.5))) for value in values]
    return "".join(f"{value:02x}" for value in result)


def recover_control_pair(v1: float, v2: float) -> tuple[float, float]:
    # CurveTimeline::setCurve stores samples at t=.1 and t=.2. Invert the
    # cubic with endpoints (0,0) and (1,1) to recover c1/c2 for one axis.
    a1, b1, t1 = 0.243, 0.027, 0.1
    a2, b2, t2 = 0.384, 0.096, 0.2
    r1, r2 = v1 - t1**3, v2 - t2**3
    determinant = a1 * b2 - b1 * a2
    return (
        (r1 * b2 - b1 * r2) / determinant,
        (a1 * r2 - r1 * a2) / determinant,
    )


def add_curve(frame: dict[str, Any], curves: list[float], frame_index: int) -> str:
    start = frame_index * 19
    block = curves[start : start + 19]
    if len(block) != 19:
        raise ValueError(f"truncated expanded curve block {frame_index}")
    kind = block[0]
    if abs(kind) < 1e-6:
        return "linear"
    if abs(kind - 1) < 1e-6:
        frame["curve"] = "stepped"
        return "stepped"
    if abs(kind - 2) > 1e-6:
        raise ValueError(f"invalid expanded curve type {kind}")
    cx1, cx2 = recover_control_pair(block[1], block[3])
    cy1, cy2 = recover_control_pair(block[2], block[4])
    # Spine 3.8 uses legacy scalar fields, not `curve: [c1,c2,c3,c4]`.
    frame["curve"] = clean_number(cx1)
    frame["c2"] = clean_number(cy1)
    frame["c3"] = clean_number(cx2)
    frame["c4"] = clean_number(cy2)
    return "bezier"


def split_frames(
    timeline: dict[str, Any], width: int
) -> tuple[list[list[float]], list[float]]:
    values = timeline["frames"]
    curves = timeline["curves"]
    if not values or len(values) % width:
        raise ValueError(
            f"{timeline['type_name']}: {len(values)} frame floats not divisible by {width}"
        )
    frames = [values[index : index + width] for index in range(0, len(values), width)]
    if len(curves) != (len(frames) - 1) * 19:
        raise ValueError(
            f"{timeline['type_name']}: {len(curves)} curve floats for {len(frames)} frames"
        )
    if any(left[0] > right[0] + 1e-6 for left, right in zip(frames, frames[1:])):
        raise ValueError(f"{timeline['type_name']}: non-monotonic times")
    return frames, curves


def insert_timeline(
    owner: dict[str, Any],
    name: str,
    frames: list[dict[str, Any]],
    context: str,
    *,
    replace_attachment: bool = False,
) -> bool:
    if name not in owner:
        owner[name] = frames
        return False
    if replace_attachment and name == "attachment":
        # A small number of source animations contain duplicate property IDs.
        # Runtime application order makes the later AttachmentTimeline fully
        # override the earlier one; standard JSON permits only one.
        owner[name] = frames
        return True
    raise ValueError(f"duplicate {name} timeline for {context}")


def attachment_metadata(
    records: dict[str, Any],
) -> dict[tuple[int, int, str], dict[str, Any]]:
    result: dict[tuple[int, int, str], dict[str, Any]] = {}
    for skin_index, skin in enumerate(records["skins"]):
        skin_name = ref(skin["name"])
        if skin_name is None:
            raise ValueError(f"skin {skin_index} has a null name")
        for entry in skin["attachments"]:
            slot_index = entry["slot_index"]
            key_name = ref(entry["key_name"])
            payload = entry["payload"]
            aliases = {
                key_name,
                ref(payload.get("name")),
                ref(payload.get("deform_attachment_name")),
            }
            item = {
                "skin_index": skin_index,
                "skin_name": skin_name,
                "slot_index": slot_index,
                "key_name": key_name,
                "weighted": bool(payload.get("bones")),
                "setup_vertices": payload.get("vertices", []),
                "world_vertices_length": payload.get("world_vertices_length", 0),
            }
            for alias in aliases:
                if alias is None:
                    continue
                lookup = (skin_index, slot_index, alias)
                previous = result.get(lookup)
                if previous is not None and previous["key_name"] != key_name:
                    raise ValueError(f"ambiguous deform attachment alias {lookup}")
                result[lookup] = item
    return result


def deform_values(runtime: list[float], metadata: dict[str, Any]) -> list[int | float]:
    setup = metadata["setup_vertices"]
    if metadata["weighted"]:
        if len(setup) % 3:
            raise ValueError("weighted setup vertex float count is not divisible by 3")
        expected = len(setup) // 3 * 2
        if len(runtime) != expected:
            raise ValueError(f"weighted deform length {len(runtime)} != {expected}")
        values = runtime
    else:
        if len(runtime) != len(setup):
            raise ValueError(f"unweighted deform length {len(runtime)} != {len(setup)}")
        values = [value - base for value, base in zip(runtime, setup)]
    return [clean_number(value) for value in values]


def curve_frames(
    timeline: dict[str, Any], report: dict[str, Any]
) -> list[dict[str, Any]]:
    type_code = timeline["type"]
    values, curves = split_frames(timeline, FRAME_WIDTHS[type_code])
    result: list[dict[str, Any]] = []
    for index, row in enumerate(values):
        frame: dict[str, Any] = {}
        add_time(frame, row[0])
        if type_code == 0:
            frame["angle"] = clean_number(row[1])
        elif type_code in (1, 2, 3):
            frame["x"] = clean_number(row[1])
            frame["y"] = clean_number(row[2])
        elif type_code == 5:
            frame["color"] = color_hex(row[1:5], 4)
        elif type_code == 9:
            frame.update(
                {
                    "mix": clean_number(row[1]),
                    "softness": clean_number(row[2]),
                    "bendPositive": row[3] > 0,
                    "compress": bool(row[4]),
                    "stretch": bool(row[5]),
                }
            )
        elif type_code == 10:
            frame.update(
                {
                    "rotateMix": clean_number(row[1]),
                    "translateMix": clean_number(row[2]),
                    "scaleMix": clean_number(row[3]),
                    "shearMix": clean_number(row[4]),
                }
            )
        elif type_code == 11:
            frame["position"] = clean_number(row[1])
        elif type_code == 12:
            frame["spacing"] = clean_number(row[1])
        elif type_code == 13:
            frame["rotateMix"] = clean_number(row[1])
            frame["translateMix"] = clean_number(row[2])
        elif type_code == 14:
            frame["light"] = color_hex(row[1:5], 4)
            frame["dark"] = color_hex(row[5:8], 3)
        else:
            raise NotImplementedError(f"curve timeline type {type_code}")
        if index < len(values) - 1:
            report["curve_counts"][add_curve(frame, curves, index)] += 1
        result.append(frame)
    return result


def emit_animations(records: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any]]:
    bones = records["bones"]
    slots = records["slots"]
    ik_constraints = records["ik_constraints"]
    transform_constraints = records["transform_constraints"]
    path_constraints = records["path_constraints"]
    deform_lookup = attachment_metadata(records)
    animations: dict[str, Any] = {}
    report: dict[str, Any] = {
        "animation_count": len(records["animations"]),
        "timeline_counts": Counter(),
        "curve_counts": Counter(),
        "duplicate_attachment_replacements": [],
    }

    for source_animation in records["animations"]:
        animation_name = ref(source_animation["name"])
        if animation_name is None or animation_name in animations:
            raise ValueError(f"invalid/duplicate animation name {animation_name!r}")
        animation: dict[str, Any] = {}

        for timeline in source_animation["timelines"]:
            kind = timeline["type"]
            report["timeline_counts"][TYPE_NAMES[kind]] += 1

            if kind in (0, 1, 2, 3):
                target = timeline["target_index"]
                bone_name = ref(bones[target]["name"])
                owner = animation.setdefault("bones", {}).setdefault(bone_name, {})
                insert_timeline(
                    owner,
                    TYPE_NAMES[kind],
                    curve_frames(timeline, report),
                    f"{animation_name}/bone/{bone_name}",
                )

            elif kind == 4:
                slot_index = timeline["slot_index"]
                slot_name = ref(slots[slot_index]["name"])
                times = timeline["frames"]
                names = timeline["attachment_names"]
                if len(times) != len(names):
                    raise ValueError(f"{animation_name}: attachment names/times differ")
                frames = []
                for time, name_ref in zip(times, names):
                    frame: dict[str, Any] = {"name": ref(name_ref) or None}
                    add_time(frame, time)
                    frames.append(frame)
                owner = animation.setdefault("slots", {}).setdefault(slot_name, {})
                replaced = insert_timeline(
                    owner,
                    "attachment",
                    frames,
                    f"{animation_name}/slot/{slot_name}",
                    replace_attachment=True,
                )
                if replaced:
                    report["duplicate_attachment_replacements"].append(
                        {"animation": animation_name, "slot": slot_name}
                    )

            elif kind in (5, 14):
                slot_index = timeline["target_index"]
                slot_name = ref(slots[slot_index]["name"])
                owner = animation.setdefault("slots", {}).setdefault(slot_name, {})
                insert_timeline(
                    owner,
                    TYPE_NAMES[kind],
                    curve_frames(timeline, report),
                    f"{animation_name}/slot/{slot_name}",
                )

            elif kind == 6:
                slot_index = timeline["slot_index"]
                skin_index = timeline["skin_index"]
                attachment_name = ref(timeline["attachment_name"])
                lookup_key = (skin_index, slot_index, attachment_name)
                try:
                    metadata = deform_lookup[lookup_key]
                except KeyError as exc:
                    raise ValueError(f"missing deform attachment {lookup_key}") from exc
                times = timeline["frames"]
                curves = timeline["curves"]
                vertices = timeline["frame_vertices"]
                if len(times) != len(vertices) or len(curves) != (len(times) - 1) * 19:
                    raise ValueError(f"{animation_name}: malformed deform timeline")
                frames = []
                for index, (time, runtime) in enumerate(zip(times, vertices)):
                    frame: dict[str, Any] = {"vertices": deform_values(runtime, metadata)}
                    add_time(frame, time)
                    if index < len(times) - 1:
                        report["curve_counts"][add_curve(frame, curves, index)] += 1
                    frames.append(frame)
                slot_name = ref(slots[slot_index]["name"])
                skin_owner = animation.setdefault("deform", {}).setdefault(
                    metadata["skin_name"], {}
                )
                slot_owner = skin_owner.setdefault(slot_name, {})
                insert_timeline(
                    slot_owner,
                    metadata["key_name"],
                    frames,
                    f"{animation_name}/deform/{metadata['key_name']}",
                )

            elif kind == 8:
                times = timeline["frames"]
                orders = timeline["draw_orders"]
                if len(times) != len(orders) or "drawOrder" in animation:
                    raise ValueError(f"{animation_name}: malformed/duplicate drawOrder")
                frames = []
                for time, order in zip(times, orders):
                    frame: dict[str, Any] = {}
                    add_time(frame, time)
                    if order:
                        if len(order) != len(slots) or sorted(order) != list(range(len(slots))):
                            raise ValueError(f"{animation_name}: invalid drawOrder permutation")
                        desired = [0] * len(order)
                        for position, setup_index in enumerate(order):
                            desired[setup_index] = position
                        frame["offsets"] = [
                            {
                                "slot": ref(slots[index]["name"]),
                                "offset": desired[index] - index,
                            }
                            for index in range(len(slots))
                        ]
                    frames.append(frame)
                animation["drawOrder"] = frames

            elif kind == 9:
                target = timeline["target_index"]
                name = ref(ik_constraints[target]["name"])
                owner = animation.setdefault("ik", {})
                insert_timeline(
                    owner, name, curve_frames(timeline, report), f"{animation_name}/ik/{name}"
                )

            elif kind == 10:
                target = timeline["target_index"]
                name = ref(transform_constraints[target]["name"])
                owner = animation.setdefault("transform", {})
                insert_timeline(
                    owner,
                    name,
                    curve_frames(timeline, report),
                    f"{animation_name}/transform/{name}",
                )

            elif kind in (11, 12, 13):
                target = timeline["target_index"]
                name = ref(path_constraints[target]["name"])
                owner = animation.setdefault("path", {}).setdefault(name, {})
                timeline_name = {11: "position", 12: "spacing", 13: "mix"}[kind]
                insert_timeline(
                    owner,
                    timeline_name,
                    curve_frames(timeline, report),
                    f"{animation_name}/path/{name}",
                )

            elif kind == 7:
                raise NotImplementedError(
                    "EventTimeline is not emitted: the audited corpus has no type 7"
                )
            else:
                raise NotImplementedError(f"timeline type {kind}")

        animations[animation_name] = animation

    report["timeline_counts"] = dict(report["timeline_counts"])
    report["curve_counts"] = dict(report["curve_counts"])
    return animations, report


def load_probe():
    spec = importlib.util.spec_from_file_location("czn_probe_scsp1u", PROBE_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot import {PROBE_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--pretty", action="store_true")
    args = parser.parse_args()
    probe = load_probe()
    parsed = probe.parse_file(args.input, retain_records=True)
    animations, report = emit_animations(parsed["records"])
    output = {"animations": animations}
    text = json.dumps(
        output,
        ensure_ascii=False,
        indent=2 if args.pretty else None,
        separators=None if args.pretty else (",", ":"),
    )
    if args.output:
        args.output.write_text(text, encoding="utf-8")
        report["output"] = str(args.output)
        report["output_bytes"] = args.output.stat().st_size
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
