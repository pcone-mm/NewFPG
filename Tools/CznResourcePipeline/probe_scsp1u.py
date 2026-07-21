#!/usr/bin/env python3
r"""Read-only structural validator/probe for CZN's Spine 3.8 SCSP1U files.

This is deliberately a parser, not a game-client hook.  It consumes already
decompressed ``*.scsp1u.bytes`` files and validates every record up to the
declared SCSP1U body boundary.  The layouts are the ones empirically verified
against Heidemarie/30093's 150 SpineSource files.

Usage:
    py probe_scsp1u.py D:\path\to\SpineSource
    py probe_scsp1u.py D:\path\to\30093.scsp1u.bytes --pretty
"""

from __future__ import annotations

import argparse
import collections
import json
import struct
from pathlib import Path
from typing import Any


ATTACHMENT_TYPES = {
    0: "region",
    1: "bounding_box",
    2: "mesh",
    3: "linked_mesh",
    4: "path",
    5: "point",
    6: "clipping",
}

TIMELINE_TYPES = {
    0: "rotate",
    1: "translate",
    2: "scale",
    3: "shear",
    4: "attachment",
    5: "color",
    6: "deform",
    7: "event",
    8: "draw_order",
    9: "ik_constraint",
    10: "transform_constraint",
    11: "path_position",
    12: "path_spacing",
    13: "path_mix",
    14: "two_color",
}

# All of these CurveTimeline subclasses serialize identically.  The frame
# vector's entry stride is implied by the type/class, not stored separately.
INDEXED_CURVE_TIMELINES = {0, 1, 2, 3, 5, 9, 10, 11, 12, 13, 14}


class ParseError(RuntimeError):
    pass


class Reader:
    def __init__(self, path: Path):
        self.path = path
        self.data = path.read_bytes()
        if len(self.data) < 16:
            raise ParseError("file is shorter than the SCSP1U header")
        self.body_length, self.pool_length = struct.unpack_from("<II", self.data, 0)
        if self.data[8:16] != b"scsp1u\0\0":
            raise ParseError("missing scsp1u marker")
        self.body_end = self.body_length + 8
        if self.body_end + self.pool_length != len(self.data):
            raise ParseError(
                f"declared lengths do not close: body_end=0x{self.body_end:x}, "
                f"pool={self.pool_length}, file={len(self.data)}"
            )
        self.pool = self.data[self.body_end :]
        self.o = 0x6A

    def need(self, count: int) -> None:
        if self.o + count > self.body_end:
            raise ParseError(
                f"read past body at 0x{self.o:x}: need {count}, end=0x{self.body_end:x}"
            )

    def u8(self) -> int:
        self.need(1)
        value = self.data[self.o]
        self.o += 1
        return value

    def u16(self) -> int:
        self.need(2)
        value = struct.unpack_from("<H", self.data, self.o)[0]
        self.o += 2
        return value

    def i32(self) -> int:
        self.need(4)
        value = struct.unpack_from("<i", self.data, self.o)[0]
        self.o += 4
        return value

    def u32(self) -> int:
        self.need(4)
        value = struct.unpack_from("<I", self.data, self.o)[0]
        self.o += 4
        return value

    def f32(self) -> float:
        self.need(4)
        value = struct.unpack_from("<f", self.data, self.o)[0]
        self.o += 4
        return value

    def f32s(self, count: int) -> list[float]:
        self.need(count * 4)
        values = list(struct.unpack_from(f"<{count}f", self.data, self.o)) if count else []
        self.o += count * 4
        return values

    def u16s(self, count: int) -> list[int]:
        self.need(count * 2)
        values = list(struct.unpack_from(f"<{count}H", self.data, self.o)) if count else []
        self.o += count * 2
        return values

    def i32s(self, count: int) -> list[int]:
        self.need(count * 4)
        values = list(struct.unpack_from(f"<{count}i", self.data, self.o)) if count else []
        self.o += count * 4
        return values

    def string(self, offset: int) -> str | None:
        if offset == 0xFFFFFFFF:
            return None
        if not 0 <= offset < len(self.pool):
            raise ParseError(f"string offset 0x{offset:x} outside pool")
        end = self.pool.find(b"\0", offset)
        if end < 0:
            raise ParseError(f"unterminated pool string at 0x{offset:x}")
        return self.pool[offset:end].decode("utf-8", errors="replace")

    def string_ref(self) -> dict[str, Any]:
        offset = self.u32()
        return {"offset": offset, "value": self.string(offset)}


def float_vector(r: Reader) -> list[float]:
    return r.f32s(r.u16())


def ushort_vector(r: Reader) -> list[int]:
    return r.u16s(r.u16())


def parse_bones(r: Reader) -> list[dict[str, Any]]:
    result = []
    for _ in range(r.u16()):
        result.append(
            {
                "index": r.u16(),
                "name": r.string_ref(),
                "parent_index": r.u16(),  # 0xffff means no parent.
                "length": r.f32(),
                "x": r.f32(),
                "y": r.f32(),
                "rotation": r.f32(),
                "scale_x": r.f32(),
                "scale_y": r.f32(),
                "shear_x": r.f32(),
                "shear_y": r.f32(),
                "transform_mode": r.u16(),
                "skin_required": r.u8(),
            }
        )
    return result


def parse_ik_constraints(r: Reader) -> list[dict[str, Any]]:
    result = []
    for _ in range(r.u16()):
        # Size is 28 + 2*bone_count.  This order is empirically confirmed by
        # constraints with one and two constrained bones.
        item = {
            "name": r.string_ref(),
            "order": r.i32(),
            "skin_required": r.u8(),
            "bend_direction": r.i32(),
            "compress": r.u8(),
            "mix": r.f32(),
            "softness": r.f32(),
            "stretch": r.u8(),
            "uniform": r.u8(),
            "target_bone_index": r.u16(),
        }
        item["bone_indices"] = r.u16s(r.u16())
        result.append(item)
    return result


def parse_slots(r: Reader) -> list[dict[str, Any]]:
    result = []
    for _ in range(r.u16()):
        result.append(
            {
                "index": r.u16(),
                "name": r.string_ref(),
                "bone_index": r.u16(),
                "light_rgba": r.f32s(4),
                "dark_rgba": r.f32s(4),
                "has_dark_color": r.u8(),
                "attachment_name": r.string_ref(),
                "blend_mode": r.u16(),
            }
        )
    return result


def parse_transform_constraints(r: Reader) -> list[dict[str, Any]]:
    result = []
    for _ in range(r.u16()):
        item = {
            "name": r.string_ref(),
            "order": r.i32(),
            "skin_required": r.u8(),
            # Field order matches the 3.8 TransformConstraintData member order.
            "rotate_mix": r.f32(),
            "translate_mix": r.f32(),
            "scale_mix": r.f32(),
            "shear_mix": r.f32(),
            "offset_rotation": r.f32(),
            "offset_x": r.f32(),
            "offset_y": r.f32(),
            "offset_scale_x": r.f32(),
            "offset_scale_y": r.f32(),
            "offset_shear_y": r.f32(),
            "relative": r.u8(),
            "local": r.u8(),
            "target_bone_index": r.u16(),
        }
        item["bone_indices"] = r.u16s(r.u16())
        result.append(item)
    return result


def parse_path_constraints(r: Reader) -> list[dict[str, Any]]:
    result = []
    for _ in range(r.u16()):
        item = {
            "name": r.string_ref(),
            "order": r.i32(),
            "skin_required": r.u8(),
            "position_mode": r.u16(),
            "spacing_mode": r.u16(),
            "rotate_mode": r.u16(),
            "offset_rotation": r.f32(),
            "position": r.f32(),
            "spacing": r.f32(),
            "rotate_mix": r.f32(),
            "translate_mix": r.f32(),
            "target_slot_index": r.u16(),
        }
        item["bone_indices"] = r.u16s(r.u16())
        result.append(item)
    return result


def parse_vertex_base(r: Reader) -> dict[str, Any]:
    return {
        "name": r.string_ref(),
        "bones": ushort_vector(r),
        "vertices": float_vector(r),
        "world_vertices_length": r.u16(),
        # A reference to the deform attachment.  It is self-referential in the
        # observed data (skin 0 + a duplicate attachment-name string).
        "deform_skin_index": r.u16(),
        "deform_attachment_name": r.string_ref(),
    }


def parse_region_attachment(r: Reader) -> dict[str, Any]:
    # Fixed payload excluding the two variable float vectors: 76 bytes.
    return {
        "name": r.string_ref(),
        "x": r.f32(),
        "y": r.f32(),
        "rotation": r.f32(),
        "scale_x": r.f32(),
        "scale_y": r.f32(),
        "width": r.f32(),
        "height": r.f32(),
        "region_offset_x": r.f32(),
        "region_offset_y": r.f32(),
        "region_width": r.f32(),
        "region_height": r.f32(),
        "region_original_width": r.f32(),
        "region_original_height": r.f32(),
        "vertex_offset": float_vector(r),
        "uvs": float_vector(r),
        "path": r.string_ref(),
        "color_rgba": r.f32s(4),
    }


def parse_mesh_attachment(r: Reader) -> dict[str, Any]:
    item = parse_vertex_base(r)
    item.update(
        {
            "region_offset_x": r.f32(),
            "region_offset_y": r.f32(),
            "region_width": r.f32(),
            "region_height": r.f32(),
            "region_original_width": r.f32(),
            "region_original_height": r.f32(),
            "region_uvs": float_vector(r),
            "uvs": float_vector(r),
            "triangles": ushort_vector(r),
            # Second mesh reference/name pair.  It is zero + a duplicate name
            # in all current samples; retain it rather than discarding it.
            "parent_skin_index": r.u16(),
            "parent_attachment_name": r.string_ref(),
            "region_uv_rect": r.f32s(4),
            "width": r.f32(),
            "height": r.f32(),
            "color_rgba": r.f32s(4),
            "hull_length": r.u16(),
            "edges": ushort_vector(r),
            "region_rotate": r.u8(),
            "region_degrees": r.i32(),
            "parent_ref": r.i32(),
            "deform_ref": r.i32(),
            "tail_flag": r.u8(),
        }
    )
    return item


def parse_path_attachment(r: Reader) -> dict[str, Any]:
    item = parse_vertex_base(r)
    item.update(
        {
            "lengths": float_vector(r),
            "closed": r.u8(),
            "constant_speed": r.u8(),
        }
    )
    return item


def parse_clipping_attachment(r: Reader) -> dict[str, Any]:
    item = parse_vertex_base(r)
    item["end_slot_index"] = r.u16()
    return item


def parse_attachment(r: Reader, type_code: int) -> dict[str, Any]:
    if type_code == 0:
        return parse_region_attachment(r)
    if type_code == 2:
        return parse_mesh_attachment(r)
    if type_code == 4:
        return parse_path_attachment(r)
    if type_code == 6:
        return parse_clipping_attachment(r)
    raise ParseError(
        f"attachment type {type_code} ({ATTACHMENT_TYPES.get(type_code, 'unknown')}) "
        f"is valid Spine 3.8 but absent from the verified 30093 corpus"
    )


def parse_skins(r: Reader) -> tuple[list[dict[str, Any]], collections.Counter[int]]:
    skins = []
    type_counts: collections.Counter[int] = collections.Counter()
    for _ in range(r.u16()):
        skin = {
            "name": r.string_ref(),
            "bone_indices": r.u16s(r.u16()),
            "constraint_indices": r.u16s(r.u16()),
            "attachments": [],
        }
        for _ in range(r.u16()):
            entry = {
                "slot_index": r.u16(),
                "key_name": r.string_ref(),
                "type": r.u16(),
            }
            type_counts[entry["type"]] += 1
            entry["type_name"] = ATTACHMENT_TYPES.get(entry["type"], "unknown")
            entry["payload"] = parse_attachment(r, entry["type"])
            skin["attachments"].append(entry)
        skins.append(skin)
    return skins, type_counts


def parse_event_data(r: Reader) -> list[dict[str, Any]]:
    """Parse Spine 3.8 EventData defaults stored before the animation table.

    The six audited monster models with EventData use the exact runtime field
    order: name, int, float, string, audio path, volume, and balance.  String
    values are pool references; the other fields use their native Spine types.
    """

    result: list[dict[str, Any]] = []
    for index in range(r.u16()):
        result.append(
            {
                "index": index,
                "name": r.string_ref(),
                "int": r.i32(),
                "float": r.f32(),
                "string": r.string_ref(),
                "audio": r.string_ref(),
                "volume": r.f32(),
                "balance": r.f32(),
            }
        )
    return result


def parse_curve_timeline(r: Reader, type_code: int) -> dict[str, Any]:
    return {
        "type": type_code,
        "type_name": TIMELINE_TYPES[type_code],
        "target_index": r.u16(),
        "frames": float_vector(r),
        "curves": float_vector(r),
    }


def parse_attachment_timeline(r: Reader) -> dict[str, Any]:
    return {
        "type": 4,
        "type_name": TIMELINE_TYPES[4],
        "slot_index": r.u16(),
        "frames": float_vector(r),
        "attachment_names": [r.string_ref() for _ in range(r.u16())],
    }


def parse_deform_timeline(r: Reader) -> dict[str, Any]:
    item = {
        "type": 6,
        "type_name": TIMELINE_TYPES[6],
        "slot_index": r.u16(),
        "frames": float_vector(r),
        "curves": float_vector(r),
        "frame_vertices": [],
    }
    for _ in range(r.u16()):
        item["frame_vertices"].append(float_vector(r))
    item["attachment_name"] = r.string_ref()
    item["skin_index"] = r.u16()
    return item


def parse_draw_order_timeline(r: Reader) -> dict[str, Any]:
    item = {
        "type": 8,
        "type_name": TIMELINE_TYPES[8],
        "frames": float_vector(r),
        "draw_orders": [],
    }
    for _ in range(r.u16()):
        item["draw_orders"].append(r.i32s(r.u16()))
    return item


def parse_timeline(r: Reader) -> dict[str, Any]:
    type_code = r.u16()
    if type_code in INDEXED_CURVE_TIMELINES:
        return parse_curve_timeline(r, type_code)
    if type_code == 4:
        return parse_attachment_timeline(r)
    if type_code == 6:
        return parse_deform_timeline(r)
    if type_code == 8:
        return parse_draw_order_timeline(r)
    if type_code == 7:
        raise ParseError("EventTimeline is valid but absent from all verified corpora")
    raise ParseError(f"unknown timeline type {type_code}")


def parse_animations(r: Reader) -> tuple[list[dict[str, Any]], collections.Counter[int]]:
    animations = []
    type_counts: collections.Counter[int] = collections.Counter()
    for _ in range(r.u16()):
        animation = {
            "name": r.string_ref(),
            "duration": r.f32(),
            "timelines": [],
        }
        for _ in range(r.u16()):
            timeline = parse_timeline(r)
            type_counts[timeline["type"]] += 1
            animation["timelines"].append(timeline)
        animations.append(animation)
    return animations, type_counts


def parse_file(path: Path, retain_records: bool = False) -> dict[str, Any]:
    r = Reader(path)
    bones = parse_bones(r)
    ik_constraints = parse_ik_constraints(r)
    slots = parse_slots(r)
    transform_constraints = parse_transform_constraints(r)
    path_constraints = parse_path_constraints(r)
    skins, attachment_counts = parse_skins(r)
    event_data = parse_event_data(r)
    animations, timeline_counts = parse_animations(r)
    if r.o != r.body_end:
        raise ParseError(f"body did not close: parser=0x{r.o:x}, declared=0x{r.body_end:x}")

    summary: dict[str, Any] = {
        "path": str(path),
        "file_size": len(r.data),
        "body_end": r.body_end,
        "string_pool_length": r.pool_length,
        "bone_count": len(bones),
        "ik_constraint_count": len(ik_constraints),
        "slot_count": len(slots),
        "transform_constraint_count": len(transform_constraints),
        "path_constraint_count": len(path_constraints),
        "skin_count": len(skins),
        "attachment_counts": {
            ATTACHMENT_TYPES.get(k, str(k)): v for k, v in sorted(attachment_counts.items())
        },
        "event_data_count": len(event_data),
        "event_data_names": [item["name"]["value"] for item in event_data],
        "animation_count": len(animations),
        "timeline_counts": {
            TIMELINE_TYPES.get(k, str(k)): v for k, v in sorted(timeline_counts.items())
        },
        "validated_body_end": True,
    }
    if retain_records:
        summary["records"] = {
            "bones": bones,
            "ik_constraints": ik_constraints,
            "slots": slots,
            "transform_constraints": transform_constraints,
            "path_constraints": path_constraints,
            "skins": skins,
            "event_data": event_data,
            "animations": animations,
        }
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("--pretty", action="store_true")
    parser.add_argument("--records", action="store_true", help="include full parsed records")
    args = parser.parse_args()

    paths = (
        sorted(args.input.rglob("*.scsp1u.bytes"))
        if args.input.is_dir()
        else [args.input]
    )
    if not paths:
        raise SystemExit("no *.scsp1u.bytes files found")

    results = []
    total_attachments: collections.Counter[str] = collections.Counter()
    total_timelines: collections.Counter[str] = collections.Counter()
    for path in paths:
        result = parse_file(path, retain_records=args.records)
        results.append(result)
        total_attachments.update(result["attachment_counts"])
        total_timelines.update(result["timeline_counts"])

    output = {
        "file_count": len(results),
        "all_body_ends_valid": True,
        "attachment_counts": dict(sorted(total_attachments.items())),
        "timeline_counts": dict(sorted(total_timelines.items())),
        "files": results,
    }
    print(json.dumps(output, ensure_ascii=False, indent=2 if args.pretty else None))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
