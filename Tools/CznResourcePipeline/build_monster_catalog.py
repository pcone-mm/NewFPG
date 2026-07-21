#!/usr/bin/env python3
"""Build a local, searchable CZN monster ID/art catalog.

The installed client is read-only. This script reads SSRA/SSRC records and
game logs, then writes decoded preview PNGs plus a static HTML catalog under
the requested output directory. It does not start or modify the game client.
"""

from __future__ import annotations

import argparse
import csv
import html
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

from audit_ssra_character import (
    map_chunks,
    parse_manifest,
    read_record_bytes,
)
from extract_character import bootstrap_dependencies, decode_sct


SPOT_TYPES = (
    "SPOT_TYPE_BATTLE",
    "SPOT_TYPE_ELITE",
    "SPOT_TYPE_BOSS",
)
GENERIC_PREFIXES = (
    "cm_shake",
    "hit_",
    "fatal_",
    "boss_death",
    "screen_fx",
    "monster_",
)
ACTION_MARKERS = {
    "action",
    "active",
    "attack",
    "buff",
    "casting",
    "debuff",
    "death",
    "enter",
    "fatal",
    "idle",
    "job",
    "m",
    "normal",
    "run",
    "s",
    "skill",
    "special",
    "stance",
    "strong",
    "unique",
}


class CatalogError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    project_root = Path(__file__).resolve().parents[2]
    default_game = Path(
        r"F:\WeGameApps\rail_apps\czn(2002460)\bin\appdata\prod\gameres"
    )
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gameres-root", type=Path, default=default_game)
    parser.add_argument(
        "--logs-root",
        type=Path,
        default=default_game.parent / "logs",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=project_root / "output/CZNMonsterCatalog/Browser",
    )
    parser.add_argument(
        "--scope",
        choices=("battle", "all-observed"),
        default="battle",
        help=(
            "battle includes every ID observed in SPOT_TYPE_BATTLE and marks "
            "ELITE/BOSS overlap; all-observed also includes elite/boss-only IDs."
        ),
    )
    return parser.parse_args()


def find_battle_dicts(value: Any) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        spot_type = value.get("spot_type")
        if spot_type in SPOT_TYPES and isinstance(value.get("monsters"), list):
            yield value
        for child in value.values():
            yield from find_battle_dicts(child)
    elif isinstance(value, list):
        for child in value:
            yield from find_battle_dicts(child)


def scan_logs(logs_root: Path) -> dict[str, dict[str, dict[str, Any]]]:
    if not logs_root.is_dir():
        raise CatalogError(f"Log directory does not exist: {logs_root}")

    observed: dict[str, dict[str, dict[str, Any]]] = {
        spot_type: {} for spot_type in SPOT_TYPES
    }
    for log_path in sorted(logs_root.glob("game_*.log")):
        with log_path.open("r", encoding="utf-8", errors="replace") as stream:
            for line_number, line in enumerate(stream, start=1):
                if '"spot_type":"SPOT_TYPE_' not in line or '"monsters"' not in line:
                    continue
                json_start = line.find("{")
                if json_start < 0:
                    continue
                try:
                    payload = json.loads(line[json_start:])
                except json.JSONDecodeError:
                    continue
                for battle in find_battle_dicts(payload):
                    spot_type = str(battle["spot_type"])
                    for monster in battle["monsters"]:
                        if not isinstance(monster, dict):
                            continue
                        res_id = str(monster.get("res_id", ""))
                        match = re.fullmatch(r"(\d{7})_\d+", res_id)
                        if not match:
                            continue
                        monster_id = match.group(1)
                        entry = observed[spot_type].setdefault(
                            monster_id,
                            {
                                "count": 0,
                                "files": set(),
                                "examples": [],
                            },
                        )
                        entry["count"] += 1
                        entry["files"].add(log_path.name)
                        if len(entry["examples"]) < 3:
                            entry["examples"].append(
                                f"{log_path.name}:{line_number}:{res_id}"
                            )
    return observed


def load_branches(gameres_root: Path) -> dict[str, dict[str, Any]]:
    definitions = (
        (
            "main",
            gameres_root / "manifest.ssra",
            gameres_root / "chunks",
        ),
        (
            "shadow",
            gameres_root / "shadow/manifest.ssra",
            gameres_root / "shadow/chunks",
        ),
    )
    output: dict[str, dict[str, Any]] = {}
    for branch, manifest_path, chunk_root in definitions:
        parsed = parse_manifest(manifest_path, branch)
        map_chunks(parsed["records"], parsed["archives"], chunk_root)
        output[branch] = {
            "manifest": manifest_path,
            "chunks": chunk_root,
            "records": parsed["records"],
            "by_path": {str(item["path"]): item for item in parsed["records"]},
        }
    return output


def decode_json_record(
    record: dict[str, Any] | None,
    branch_data: dict[str, Any],
) -> dict[str, Any]:
    if not record:
        return {}
    try:
        raw = read_record_bytes(record, branch_data["chunks"])
        parsed = json.loads(raw.decode("utf-8-sig"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError):
        return {}
    return parsed if isinstance(parsed, dict) else {}


def collect_file_names(value: Any) -> list[str]:
    output: list[str] = []
    if isinstance(value, dict):
        for key, child in value.items():
            if isinstance(child, str) and key in {
                "file_name",
                "effect_name",
                "death_effect_name",
            }:
                normalized = child.strip()
                if normalized:
                    output.append(normalized)
            output.extend(collect_file_names(child))
    elif isinstance(value, list):
        for child in value:
            output.extend(collect_file_names(child))
    return output


def alias_prefix(value: str) -> str | None:
    lowered = value.lower()
    if not lowered or lowered.startswith(GENERIC_PREFIXES):
        return None
    tokens = [token for token in re.split(r"[_\-]+", lowered) if token]
    if not tokens or tokens[0].isdigit():
        return None
    kept: list[str] = []
    for token in tokens:
        if (
            token in ACTION_MARKERS
            or re.fullmatch(r"\d+", token)
            or re.fullmatch(
                r"(?:action|attack|buff|cam|camera|death|debuff|eff|effect|"
                r"end|enter|fatal|idle|node|normal|play|ready|run|self|skill|"
                r"stance|target|unique)\d*",
                token,
            )
        ):
            break
        kept.append(token)
    if not kept:
        return None
    return "_".join(kept[:4])


def choose_alias(srmd: dict[str, Any]) -> str:
    candidates = [
        prefix
        for value in collect_file_names(srmd)
        if (prefix := alias_prefix(value))
    ]
    if not candidates:
        return ""
    counts = Counter(candidates)
    return sorted(counts, key=lambda item: (-counts[item], -len(item), item))[0]


def complete_triplet(
    by_path: dict[str, dict[str, Any]],
    root: str,
    monster_id: str,
) -> dict[str, dict[str, Any]] | None:
    triplet: dict[str, dict[str, Any]] = {}
    for extension in ("atlas", "scsp", "sct"):
        path = f"{root}/{monster_id}.{extension}"
        record = by_path.get(path)
        if not record or not record.get("chunk"):
            return None
        triplet[extension] = record
    return triplet


def choose_model_triplet(
    branches: dict[str, dict[str, Any]],
    monster_id: str,
) -> tuple[str, dict[str, dict[str, Any]]]:
    choices = (
        ("shadow", "model"),
        ("main", "model/zhs"),
        ("main", "model"),
    )
    for branch, root in choices:
        triplet = complete_triplet(branches[branch]["by_path"], root, monster_id)
        if triplet:
            return branch, triplet
    raise CatalogError(f"No complete model triplet for observed monster {monster_id}")


def choose_portrait(
    branches: dict[str, dict[str, Any]],
    monster_id: str,
) -> tuple[str, dict[str, Any]] | None:
    choices = (
        ("shadow", f"face/mob/portrait_boss_{monster_id}.sct"),
        ("main", f"face/mob/zhs/portrait_boss_{monster_id}.sct"),
        ("main", f"face/mob/portrait_boss_{monster_id}.sct"),
        ("shadow", f"face/mob/portrait_boss_crop_{monster_id}.sct"),
        ("main", f"face/mob/zhs/portrait_boss_crop_{monster_id}.sct"),
        ("main", f"face/mob/portrait_boss_crop_{monster_id}.sct"),
        ("shadow", f"face/mob/face/face_mob_{monster_id}.sct"),
        ("main", f"face/mob/face/zhs/face_mob_{monster_id}.sct"),
        ("main", f"face/mob/face/face_mob_{monster_id}.sct"),
        ("shadow", f"face/mob/face_mob_{monster_id}.sct"),
        ("main", f"face/mob/face_mob_{monster_id}.sct"),
    )
    for branch, path in choices:
        record = branches[branch]["by_path"].get(path)
        if record and record.get("chunk"):
            return branch, record
    return None


def record_snapshot(record: dict[str, Any]) -> dict[str, Any]:
    return {
        key: record.get(key)
        for key in (
            "branch",
            "path",
            "chunk",
            "offset",
            "stored",
            "original",
            "compression",
            "record_index",
            "hash",
        )
    }


def save_sct_png(
    record: dict[str, Any],
    branch_data: dict[str, Any],
    output_path: Path,
    decoder: tuple[Any, Any, Any, Any, Any],
) -> dict[str, Any]:
    _, lz4_block, texture2ddecoder, image_type, _ = decoder
    raw = read_record_bytes(record, branch_data["chunks"])
    image, metadata = decode_sct(raw, lz4_block, texture2ddecoder, image_type)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(output_path, format="PNG")
    metadata["width"] = image.width
    metadata["height"] = image.height
    return metadata


def build_entry(
    monster_id: str,
    observed: dict[str, dict[str, dict[str, Any]]],
    branches: dict[str, dict[str, Any]],
    output_root: Path,
    decoder: tuple[Any, Any, Any, Any, Any],
) -> dict[str, Any]:
    main_by_path = branches["main"]["by_path"]
    setting_record = main_by_path.get(f"model_setting/{monster_id}.setting")
    srmd_record = main_by_path.get(f"model_data/{monster_id}.srmd")
    srcs_record = main_by_path.get(f"model_data/{monster_id}.srcs")
    setting = decode_json_record(setting_record, branches["main"])
    srmd = decode_json_record(srmd_record, branches["main"])

    model_branch, triplet = choose_model_triplet(branches, monster_id)
    portrait_choice = choose_portrait(branches, monster_id)

    portrait_relative = Path("portraits") / f"{monster_id}.png"
    portrait_metadata: dict[str, Any] | None = None
    portrait_record: dict[str, Any] | None = None
    portrait_branch = ""
    if portrait_choice:
        portrait_branch, portrait_record = portrait_choice
        portrait_metadata = save_sct_png(
            portrait_record,
            branches[portrait_branch],
            output_root / portrait_relative,
            decoder,
        )

    atlas_relative = Path("model-atlases") / f"{monster_id}.png"
    atlas_metadata = save_sct_png(
        triplet["sct"],
        branches[model_branch],
        output_root / atlas_relative,
        decoder,
    )

    types = [spot_type for spot_type in SPOT_TYPES if monster_id in observed[spot_type]]
    type_counts = {
        spot_type: int(observed[spot_type].get(monster_id, {}).get("count", 0))
        for spot_type in SPOT_TYPES
    }
    type_files = {
        spot_type: len(observed[spot_type].get(monster_id, {}).get("files", set()))
        for spot_type in SPOT_TYPES
    }
    examples = {
        spot_type: observed[spot_type].get(monster_id, {}).get("examples", [])
        for spot_type in SPOT_TYPES
    }
    animations = setting.get("animations")
    if not isinstance(animations, list):
        animations = []
    commands = srmd.get("command")
    if not isinstance(commands, dict):
        commands = {}

    return {
        "id": monster_id,
        "internal_alias": choose_alias(srmd),
        "types": types,
        "type_counts": type_counts,
        "type_files": type_files,
        "log_examples": examples,
        "ordinary_only": types == ["SPOT_TYPE_BATTLE"],
        "scale_grade": setting.get("scale_grade", srmd.get("scale_grade", "")),
        "render_width": setting.get("render_width", srmd.get("render_width")),
        "render_height": setting.get("render_height", srmd.get("render_height")),
        "animation_count": len(animations) if animations else len(commands),
        "animations": animations,
        "command_count": len(commands),
        "model_type": srmd.get("model_type", ""),
        "model_branch": model_branch,
        "model": {extension: record_snapshot(record) for extension, record in triplet.items()},
        "setting": record_snapshot(setting_record) if setting_record else None,
        "srmd": record_snapshot(srmd_record) if srmd_record else None,
        "srcs": record_snapshot(srcs_record) if srcs_record else None,
        "portrait_branch": portrait_branch,
        "portrait": record_snapshot(portrait_record) if portrait_record else None,
        "portrait_png": portrait_relative.as_posix() if portrait_record else "",
        "portrait_metadata": portrait_metadata,
        "model_atlas_png": atlas_relative.as_posix(),
        "model_atlas_metadata": atlas_metadata,
    }


def render_badges(entry: dict[str, Any]) -> str:
    labels = {
        "SPOT_TYPE_BATTLE": ("普通战斗", "battle"),
        "SPOT_TYPE_ELITE": ("也用于精英", "elite"),
        "SPOT_TYPE_BOSS": ("也用于 Boss", "boss"),
    }
    badges = "".join(
        f'<span class="badge {css}">{html.escape(label)}</span>'
        for spot_type, (label, css) in labels.items()
        if spot_type in entry["types"]
    )
    if not entry["model_type"]:
        badges += '<span class="badge uncertain">类型未声明</span>'
    return badges


def render_card(entry: dict[str, Any]) -> str:
    monster_id = html.escape(entry["id"])
    alias = html.escape(entry["internal_alias"] or "未解析内部代号")
    portrait = html.escape(entry["portrait_png"] or entry["model_atlas_png"])
    atlas = html.escape(entry["model_atlas_png"])
    scale = html.escape(str(entry["scale_grade"] or "UNKNOWN"))
    model_scsp = entry["model"]["scsp"]
    model_path = html.escape(str(model_scsp["path"]))
    chunk = html.escape(str(model_scsp["chunk"]))
    branch = html.escape(str(entry["model_branch"]))
    portrait_path = html.escape(
        str(entry["portrait"]["path"]) if entry["portrait"] else "无独立识别图，使用模型图集"
    )
    search_text = html.escape(
        " ".join(
            (
                entry["id"],
                entry["internal_alias"],
                str(entry["scale_grade"]),
                str(model_scsp["path"]),
            )
        ).lower()
    )
    types = " ".join(entry["types"])
    return f"""
      <article class="card" data-search="{search_text}" data-types="{types}"
        data-ordinary="{str(entry['ordinary_only']).lower()}"
        data-modeltype="{html.escape(str(entry['model_type']).lower())}"
        data-scale="{scale}" data-id="{monster_id}"
        data-animations="{entry['animation_count']}"
        data-count="{entry['type_counts']['SPOT_TYPE_BATTLE']}">
        <a class="preview" href="{portrait}" target="_blank" title="打开识别图原图">
          <img loading="lazy" src="{portrait}" alt="怪物 {monster_id} 识别图">
        </a>
        <div class="body">
          <div class="title-row"><h2>{monster_id}</h2><span class="alias">{alias}</span></div>
          <div class="badges">{render_badges(entry)}</div>
          <p class="stats">{scale} · {entry['animation_count']} 个动画 · 普通战斗记录 {entry['type_counts']['SPOT_TYPE_BATTLE']} 次</p>
          <details>
            <summary>查看真实模型图集与资源路径</summary>
            <a href="{atlas}" target="_blank"><img class="atlas" loading="lazy" src="{atlas}" alt="{monster_id} 模型图集"></a>
            <dl>
              <dt>SCSP</dt><dd><code>{model_path}</code></dd>
              <dt>分支</dt><dd><code>{branch}</code></dd>
              <dt>分块</dt><dd><code>{chunk}</code></dd>
              <dt>识别图</dt><dd><code>{portrait_path}</code></dd>
            </dl>
          </details>
        </div>
      </article>"""


def render_html(entries: list[dict[str, Any]]) -> str:
    cards = "\n".join(render_card(entry) for entry in entries)
    pure_count = sum(bool(entry["ordinary_only"]) for entry in entries)
    uncertain_count = sum(
        bool(entry["ordinary_only"] and not entry["model_type"])
        for entry in entries
    )
    return f"""<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>CZN 怪物 ID / 模型图鉴</title>
  <style>
    :root {{ color-scheme: dark; --bg:#11141a; --panel:#1a1f28; --line:#323b49; --text:#eef2f8; --muted:#aab6c8; --blue:#6eb6ff; }}
    * {{ box-sizing:border-box; }}
    body {{ margin:0; background:var(--bg); color:var(--text); font:15px/1.5 "Microsoft YaHei",system-ui,sans-serif; }}
    header {{ position:sticky; top:0; z-index:5; padding:18px 24px; background:rgba(17,20,26,.96); border-bottom:1px solid var(--line); backdrop-filter:blur(12px); }}
    h1 {{ margin:0 0 5px; font-size:24px; }}
    header p {{ margin:0 0 14px; color:var(--muted); }}
    .controls {{ display:grid; grid-template-columns:minmax(220px,1fr) repeat(3,minmax(145px,220px)); gap:10px; }}
    input,select {{ width:100%; border:1px solid var(--line); border-radius:8px; background:#0d1015; color:var(--text); padding:10px 12px; }}
    main {{ padding:20px 24px 40px; }}
    #result {{ margin:0 0 12px; color:var(--muted); }}
    .grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(300px,1fr)); gap:14px; }}
    .card {{ overflow:hidden; border:1px solid var(--line); border-radius:12px; background:var(--panel); box-shadow:0 8px 28px rgba(0,0,0,.18); }}
    .card.hidden {{ display:none; }}
    .preview {{ display:flex; height:230px; align-items:center; justify-content:center; background:#101319; border-bottom:1px solid var(--line); }}
    .preview img {{ max-width:96%; max-height:96%; object-fit:contain; }}
    .body {{ padding:13px 14px 15px; }}
    .title-row {{ display:flex; align-items:baseline; gap:10px; flex-wrap:wrap; }}
    h2 {{ margin:0; font-size:21px; }}
    .alias {{ color:var(--blue); font-size:13px; word-break:break-word; }}
    .badges {{ display:flex; gap:6px; flex-wrap:wrap; margin:8px 0; }}
    .badge {{ padding:2px 8px; border-radius:999px; font-size:12px; font-weight:700; }}
    .badge.battle {{ background:#153f31; color:#86e6bb; }}
    .badge.elite {{ background:#4a3512; color:#ffd886; }}
    .badge.boss {{ background:#4c1d2a; color:#ff9db4; }}
    .badge.uncertain {{ background:#303744; color:#d3dae6; }}
    .stats {{ margin:6px 0 10px; color:var(--muted); }}
    details {{ border-top:1px solid var(--line); padding-top:9px; }}
    summary {{ cursor:pointer; color:#d9e8ff; }}
    .atlas {{ display:block; width:100%; max-height:320px; object-fit:contain; margin:10px 0; background:#0d1015; border-radius:8px; }}
    dl {{ display:grid; grid-template-columns:58px 1fr; gap:4px 8px; margin:8px 0 0; }}
    dt {{ color:var(--muted); }} dd {{ margin:0; min-width:0; }} code {{ white-space:normal; word-break:break-all; font-size:12px; color:#cbe0ff; }}
    .note {{ margin-top:18px; color:var(--muted); }}
    @media (max-width:850px) {{ .controls {{ grid-template-columns:1fr 1fr; }} }}
    @media (max-width:520px) {{ header,main {{ padding-left:14px; padding-right:14px; }} .controls {{ grid-template-columns:1fr; }} }}
  </style>
</head>
<body>
  <header>
    <h1>CZN 怪物 ID / 模型图鉴</h1>
    <p>共 {len(entries)} 个曾出现在普通战斗日志中的模型，其中 {pure_count} 个只见于普通战斗；这组里有 {uncertain_count} 个 SRMD 未声明 model_type，已单独标注，可能是 NPC 或特殊单位。识别图用于选外形；展开卡片可查看该 ID 的真实 Spine 模型图集与 SSRC 路径。</p>
    <div class="controls">
      <input id="search" type="search" placeholder="搜索 ID、内部代号或模型路径">
      <select id="type">
        <option value="all">全部普通战斗模型</option>
        <option value="ordinary" selected>仅普通战斗（不含精英/Boss）</option>
        <option value="declared">仅明确声明 monster</option>
        <option value="uncertain">仅类型未声明</option>
        <option value="elite">同时用于精英</option>
        <option value="boss">同时用于 Boss</option>
      </select>
      <select id="scale">
        <option value="all">全部体型</option>
        <option value="SMALL">SMALL</option><option value="MIDDLE">MIDDLE</option><option value="LARGE">LARGE</option>
      </select>
      <select id="sort">
        <option value="count">按日志出现次数</option>
        <option value="id">按 ID</option>
        <option value="animations">按动画数量</option>
      </select>
    </div>
  </header>
  <main>
    <p id="result"></p>
    <section id="grid" class="grid">{cards}</section>
    <p class="note">说明：<code>model-atlases</code> 是游戏实际使用的拆件图集，不是组装后的静态立绘。选定 ID 后，需要解包 SCSP 并在 Spine/Unity 中播放 idle 才能看到完整战斗模型。</p>
  </main>
  <script>
    const search=document.querySelector('#search'), type=document.querySelector('#type'), scale=document.querySelector('#scale'), sort=document.querySelector('#sort');
    const grid=document.querySelector('#grid'), result=document.querySelector('#result');
    const cards=[...document.querySelectorAll('.card')];
    function apply() {{
      const q=search.value.trim().toLowerCase(), t=type.value, s=scale.value;
      for (const card of cards) {{
        const types=card.dataset.types;
        const typeOk=t==='all' || (t==='ordinary' && card.dataset.ordinary==='true') || (t==='declared' && card.dataset.ordinary==='true' && card.dataset.modeltype==='monster') || (t==='uncertain' && card.dataset.ordinary==='true' && !card.dataset.modeltype) || (t==='elite' && types.includes('SPOT_TYPE_ELITE')) || (t==='boss' && types.includes('SPOT_TYPE_BOSS'));
        const ok=typeOk && (s==='all' || card.dataset.scale===s) && (!q || card.dataset.search.includes(q));
        card.classList.toggle('hidden',!ok);
      }}
      const visible=cards.filter(c=>!c.classList.contains('hidden'));
      visible.sort((a,b)=>sort.value==='id' ? a.dataset.id.localeCompare(b.dataset.id) : sort.value==='animations' ? Number(b.dataset.animations)-Number(a.dataset.animations) : Number(b.dataset.count)-Number(a.dataset.count));
      for (const card of visible) grid.appendChild(card);
      result.textContent=`当前显示 ${{visible.length}} / ${{cards.length}} 个模型`;
    }}
    for (const control of [search,type,scale,sort]) control.addEventListener('input',apply);
    apply();
  </script>
</body>
</html>
"""


def write_catalog(output_root: Path, entries: list[dict[str, Any]]) -> None:
    output_root.mkdir(parents=True, exist_ok=True)
    (output_root / "catalog-data.json").write_text(
        json.dumps(entries, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    fields = (
        "id",
        "internal_alias",
        "ordinary_only",
        "types",
        "battle_count",
        "elite_count",
        "boss_count",
        "scale_grade",
        "animation_count",
        "model_branch",
        "model_path",
        "model_chunk",
        "portrait_path",
    )
    with (output_root / "catalog.csv").open(
        "w", newline="", encoding="utf-8-sig"
    ) as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for entry in entries:
            writer.writerow(
                {
                    "id": entry["id"],
                    "internal_alias": entry["internal_alias"],
                    "ordinary_only": entry["ordinary_only"],
                    "types": ",".join(entry["types"]),
                    "battle_count": entry["type_counts"]["SPOT_TYPE_BATTLE"],
                    "elite_count": entry["type_counts"]["SPOT_TYPE_ELITE"],
                    "boss_count": entry["type_counts"]["SPOT_TYPE_BOSS"],
                    "scale_grade": entry["scale_grade"],
                    "animation_count": entry["animation_count"],
                    "model_branch": entry["model_branch"],
                    "model_path": entry["model"]["scsp"]["path"],
                    "model_chunk": entry["model"]["scsp"]["chunk"],
                    "portrait_path": entry["portrait"]["path"] if entry["portrait"] else "",
                }
            )
    (output_root / "index.html").write_text(
        render_html(entries),
        encoding="utf-8",
    )


def main() -> int:
    args = parse_args()
    try:
        gameres_root = args.gameres_root.resolve()
        output_root = args.output.resolve()
        observed = scan_logs(args.logs_root.resolve())
        branches = load_branches(gameres_root)
        decoder = bootstrap_dependencies(None)
        selected = set(observed["SPOT_TYPE_BATTLE"])
        if args.scope == "all-observed":
            for spot_type in SPOT_TYPES:
                selected.update(observed[spot_type])

        entries: list[dict[str, Any]] = []
        for index, monster_id in enumerate(sorted(selected, key=int), start=1):
            entry = build_entry(
                monster_id,
                observed,
                branches,
                output_root,
                decoder,
            )
            entries.append(entry)
            print(
                f"[{index:>3}/{len(selected)}] {monster_id} "
                f"{entry['internal_alias'] or '-'}",
                flush=True,
            )

        entries.sort(
            key=lambda item: (
                -item["type_counts"]["SPOT_TYPE_BATTLE"],
                int(item["id"]),
            )
        )
        write_catalog(output_root, entries)
        summary = {
            "schema": "czn-monster-catalog-v1",
            "scope": args.scope,
            "entry_count": len(entries),
            "ordinary_only_count": sum(
                bool(entry["ordinary_only"]) for entry in entries
            ),
            "elite_overlap_count": sum(
                "SPOT_TYPE_ELITE" in entry["types"] for entry in entries
            ),
            "boss_overlap_count": sum(
                "SPOT_TYPE_BOSS" in entry["types"] for entry in entries
            ),
            "output": str(output_root),
            "index": str(output_root / "index.html"),
        }
        (output_root / "summary.json").write_text(
            json.dumps(summary, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(json.dumps(summary, ensure_ascii=False, indent=2))
    except (CatalogError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
