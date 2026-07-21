#!/usr/bin/env python3
"""Safely batch-extract audited CZN monster model records from mixed branches.

Each monster must have its own complete_records.json. The record list may mix
main configuration records with a complete shadow model triplet; no implicit
branch precedence is applied. The installed game is read-only. All jobs are
preflighted first, then extracted into staging directories and published only
after every job succeeds.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace
from typing import Any

from extract_character import (
    PipelineError,
    atomic_write_json,
    ensure_output_roots_are_safe,
    load_selected_records,
    preflight_records,
    process_records,
)


SCRIPT_VERSION = "1.1.0"
MONSTER_ID_PATTERN = re.compile(r"\d{7}")


class BatchExtractionError(RuntimeError):
    pass


@dataclass(frozen=True)
class MonsterJob:
    monster_id: str
    label: str
    records: Path
    external_output: Path
    unity_output: Path


@dataclass(frozen=True)
class StagedJob:
    job: MonsterJob
    external_stage: Path
    unity_stage: Path
    result: dict[str, Any]


def normalize_monster_id(value: Any) -> str:
    monster_id = str(value).strip()
    if not MONSTER_ID_PATTERN.fullmatch(monster_id):
        raise BatchExtractionError(
            f"Monster ID must be exactly seven decimal digits: {value!r}"
        )
    return monster_id


def records_path_for(records_root: Path, monster_id: str) -> Path:
    candidates = (
        records_root / monster_id / "complete_records.json",
        records_root / f"{monster_id}.complete_records.json",
        records_root / f"{monster_id}.json",
    )
    matches = [path.resolve() for path in candidates if path.is_file()]
    if not matches:
        rendered = "\n  - ".join(str(path) for path in candidates)
        raise BatchExtractionError(
            f"No audited record list found for {monster_id}. Checked:\n  - {rendered}"
        )
    if len(matches) > 1:
        raise BatchExtractionError(
            f"Multiple audited record lists found for {monster_id}: "
            + ", ".join(str(path) for path in matches)
        )
    return matches[0]


def parse_explicit_monster(value: str) -> tuple[str, Path]:
    if "=" not in value:
        raise BatchExtractionError(
            f"--monster expects ID=RECORDS_JSON, got {value!r}"
        )
    monster_id, records = value.split("=", 1)
    return normalize_monster_id(monster_id), Path(records).expanduser().resolve()


def load_plan(path: Path) -> list[dict[str, Any]]:
    plan_path = path.resolve()
    if not plan_path.is_file():
        raise BatchExtractionError(f"Batch plan does not exist: {plan_path}")
    payload = json.loads(plan_path.read_text(encoding="utf-8-sig"))
    if isinstance(payload, dict):
        entries = payload.get("monsters")
    else:
        entries = payload
    if not isinstance(entries, list):
        raise BatchExtractionError(
            "Batch plan must be an array or an object with a 'monsters' array"
        )

    output: list[dict[str, Any]] = []
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            raise BatchExtractionError(f"Plan monster #{index} is not an object")
        if "id" not in entry or "records" not in entry:
            raise BatchExtractionError(
                f"Plan monster #{index} requires both 'id' and 'records'"
            )
        records = Path(str(entry["records"])).expanduser()
        if not records.is_absolute():
            records = plan_path.parent / records
        output.append(
            {
                "id": normalize_monster_id(entry["id"]),
                "label": str(entry.get("label") or f"Monster_{entry['id']}").strip(),
                "records": records.resolve(),
            }
        )
    return output


def build_jobs(args: argparse.Namespace) -> list[MonsterJob]:
    input_modes = sum(bool(value) for value in (args.plan, args.monster, args.ids))
    if input_modes != 1:
        raise BatchExtractionError(
            "Choose exactly one input mode: --plan, repeated --monster, or --ids"
        )

    entries: list[dict[str, Any]] = []
    if args.plan:
        entries = load_plan(args.plan)
    elif args.monster:
        for value in args.monster:
            monster_id, records = parse_explicit_monster(value)
            entries.append(
                {
                    "id": monster_id,
                    "label": f"Monster_{monster_id}",
                    "records": records,
                }
            )
    else:
        records_root = args.records_root.resolve()
        for value in args.ids:
            monster_id = normalize_monster_id(value)
            entries.append(
                {
                    "id": monster_id,
                    "label": f"Monster_{monster_id}",
                    "records": records_path_for(records_root, monster_id),
                }
            )

    seen_ids: set[str] = set()
    jobs: list[MonsterJob] = []
    external_root = args.external_root.resolve()
    unity_root = args.unity_root.resolve()
    for entry in entries:
        monster_id = normalize_monster_id(entry["id"])
        if monster_id in seen_ids:
            raise BatchExtractionError(f"Duplicate monster ID in batch: {monster_id}")
        seen_ids.add(monster_id)
        label = str(entry["label"]).strip()
        if not label:
            raise BatchExtractionError(f"Empty label for monster {monster_id}")
        jobs.append(
            MonsterJob(
                monster_id=monster_id,
                label=label,
                records=Path(entry["records"]).resolve(),
                external_output=external_root / monster_id,
                unity_output=unity_root / monster_id,
            )
        )
    if not jobs:
        raise BatchExtractionError("The batch contains no monsters")
    return jobs


def verify_core_monster_records(
    monster_id: str,
    records: list[dict[str, Any]],
) -> dict[str, Any]:
    """Require one unambiguous same-branch/root model triplet plus core configs."""

    triplets: dict[tuple[str, str], set[str]] = {}
    model_pattern = re.compile(
        rf"^(model(?:/zhs)?)/{re.escape(monster_id)}\.(atlas|scsp|sct)$",
        re.IGNORECASE,
    )
    normalized_paths: set[str] = set()
    for record in records:
        path = str(record["path"]).replace("\\", "/")
        normalized_paths.add(path.casefold())
        match = model_pattern.fullmatch(path)
        if match:
            key = (str(record["branch"]), match.group(1).casefold())
            triplets.setdefault(key, set()).add(match.group(2).casefold())

    complete = [
        (branch, root)
        for (branch, root), extensions in triplets.items()
        if extensions == {"atlas", "scsp", "sct"}
    ]
    if not complete:
        observed = {
            f"{branch}:{root}": sorted(extensions)
            for (branch, root), extensions in sorted(triplets.items())
        }
        raise BatchExtractionError(
            f"{monster_id} has no complete same-branch/root model triplet: {observed}"
        )
    if len(complete) != 1:
        raise BatchExtractionError(
            f"{monster_id} has multiple complete model triplets {complete}; "
            "the audited list must keep only the chosen source."
        )

    required_configs = (
        f"model_setting/{monster_id}.setting",
        f"model_data/{monster_id}.srmd",
        f"model_data/{monster_id}.srcs",
    )
    missing_configs = [
        path for path in required_configs if path.casefold() not in normalized_paths
    ]
    if missing_configs:
        raise BatchExtractionError(
            f"{monster_id} audited list is missing core configs: "
            + ", ".join(missing_configs)
        )

    model_branch, model_root = complete[0]
    config_branches = sorted(
        {
            str(record["branch"])
            for record in records
            if str(record["path"]).replace("\\", "/").casefold()
            in {path.casefold() for path in required_configs}
        }
    )
    return {
        "model_branch": model_branch,
        "model_root": model_root,
        "config_branches": config_branches,
    }


def preflight_job(job: MonsterJob, gameres_root: Path) -> dict[str, Any]:
    if not job.records.is_file():
        raise BatchExtractionError(
            f"Audited record list does not exist for {job.monster_id}: {job.records}"
        )
    if job.external_output.exists():
        raise BatchExtractionError(
            f"External output already exists; refusing to overwrite it: {job.external_output}"
        )
    if job.unity_output.exists():
        raise BatchExtractionError(
            f"Unity output already exists; refusing to overwrite it: {job.unity_output}"
        )

    records = load_selected_records(job.records, "all")
    source_summary = preflight_records(records, gameres_root)
    core_summary = verify_core_monster_records(job.monster_id, records)
    return {
        "monster_id": job.monster_id,
        "label": job.label,
        "records": str(job.records),
        "external_output": str(job.external_output),
        "unity_output": str(job.unity_output),
        **source_summary,
        **core_summary,
    }


def safe_cleanup_stage(path: Path, parent: Path) -> None:
    if not path.exists():
        return
    resolved = path.resolve()
    parent = parent.resolve()
    if parent not in resolved.parents or not resolved.name.startswith(".czn-stage-"):
        raise BatchExtractionError(f"Refusing to clean unexpected staging path: {resolved}")
    shutil.rmtree(resolved)


def extract_to_stage(
    job: MonsterJob,
    gameres_root: Path,
    external_root: Path,
    unity_root: Path,
    args: argparse.Namespace,
) -> StagedJob:
    token = uuid.uuid4().hex
    external_stage = external_root / f".czn-stage-{job.monster_id}-{token}"
    unity_stage = unity_root / f".czn-stage-{job.monster_id}-{token}"
    namespace = SimpleNamespace(
        records=job.records,
        gameres_root=gameres_root,
        branch="all",
        label=job.label,
        character_id=job.monster_id,
        external_root=external_stage,
        unity_root=unity_stage,
        dependency_path=args.dependency_path,
        limit=None,
        progress_every=args.progress_every,
        dry_run=False,
    )
    try:
        result = process_records(namespace)
    except Exception:
        safe_cleanup_stage(external_stage, external_root)
        safe_cleanup_stage(unity_stage, unity_root)
        raise
    return StagedJob(job, external_stage, unity_stage, result)


def publish_staged_jobs(
    staged_jobs: list[StagedJob],
    external_root: Path,
    unity_root: Path,
) -> None:
    published: list[StagedJob] = []
    try:
        for staged in staged_jobs:
            staged.external_stage.rename(staged.job.external_output)
            try:
                staged.unity_stage.rename(staged.job.unity_output)
            except Exception:
                staged.job.external_output.rename(staged.external_stage)
                raise
            published.append(staged)
    except Exception:
        for staged in reversed(published):
            if staged.job.unity_output.exists():
                staged.job.unity_output.rename(staged.unity_stage)
            if staged.job.external_output.exists():
                staged.job.external_output.rename(staged.external_stage)
        raise
    finally:
        for staged in staged_jobs:
            safe_cleanup_stage(staged.external_stage, external_root)
            safe_cleanup_stage(staged.unity_stage, unity_root)


def build_argument_parser() -> argparse.ArgumentParser:
    project_root = Path(__file__).resolve().parents[2]
    default_gameres = Path(
        r"F:\WeGameApps\rail_apps\czn(2002460)\bin\appdata\prod\gameres"
    )
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--gameres-root", type=Path, default=default_gameres)
    parser.add_argument(
        "--records-root",
        type=Path,
        default=project_root / "output/CZNMonsterImportAudit",
        help=(
            "For --ids, search <root>/<ID>/complete_records.json first, then "
            "<ID>.complete_records.json and <ID>.json."
        ),
    )
    parser.add_argument("--ids", nargs="+", help="Monster IDs resolved under --records-root")
    parser.add_argument(
        "--monster",
        action="append",
        help="Explicit ID=RECORDS_JSON entry; repeat for multiple monsters.",
    )
    parser.add_argument(
        "--plan",
        type=Path,
        help="JSON array (or {'monsters': [...]}) with id, records and optional label.",
    )
    parser.add_argument(
        "--external-root",
        type=Path,
        default=project_root / "External/CZN/Monsters",
    )
    parser.add_argument(
        "--unity-root",
        type=Path,
        default=project_root / "Assets/Imported/CZN/Monsters",
    )
    parser.add_argument("--dependency-path", type=Path)
    parser.add_argument("--progress-every", type=int, default=25)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Preflight all jobs and print the merged plan without writing any output.",
    )
    return parser


def main() -> int:
    args = build_argument_parser().parse_args()
    try:
        gameres_root = args.gameres_root.resolve()
        external_root = args.external_root.resolve()
        unity_root = args.unity_root.resolve()
        if not gameres_root.is_dir():
            raise BatchExtractionError(f"Game resource root does not exist: {gameres_root}")
        ensure_output_roots_are_safe(gameres_root, external_root, unity_root)
        jobs = build_jobs(args)

        preflight_summaries: list[dict[str, Any]] = []
        for index, job in enumerate(jobs, start=1):
            print(
                f"[PREFLIGHT {index}/{len(jobs)}] {job.monster_id} <- {job.records}",
                flush=True,
            )
            preflight_summaries.append(preflight_job(job, gameres_root))

        batch_summary: dict[str, Any] = {
            "schema": "czn-monster-batch-extraction-v1",
            "pipeline_version": SCRIPT_VERSION,
            "dry_run": bool(args.dry_run),
            "source_gameres_root": str(gameres_root),
            "external_root": str(external_root),
            "unity_root": str(unity_root),
            "monster_count": len(jobs),
            "record_count": sum(item["record_count"] for item in preflight_summaries),
            "monsters": preflight_summaries,
        }
        if args.dry_run:
            print(json.dumps(batch_summary, ensure_ascii=False, indent=2, sort_keys=True))
            return 0

        external_root.mkdir(parents=True, exist_ok=True)
        unity_root.mkdir(parents=True, exist_ok=True)
        staged_jobs: list[StagedJob] = []
        try:
            for index, job in enumerate(jobs, start=1):
                print(f"[STAGE {index}/{len(jobs)}] {job.monster_id}", flush=True)
                staged_jobs.append(
                    extract_to_stage(job, gameres_root, external_root, unity_root, args)
                )
        except Exception:
            for staged in staged_jobs:
                safe_cleanup_stage(staged.external_stage, external_root)
                safe_cleanup_stage(staged.unity_stage, unity_root)
            raise

        print(f"[PUBLISH] Publishing {len(staged_jobs)} completed monsters", flush=True)
        publish_staged_jobs(staged_jobs, external_root, unity_root)
        batch_summary["dry_run"] = False
        batch_summary["status"] = "complete"
        atomic_write_json(external_root / "batch-import-manifest.json", batch_summary)
        print(json.dumps(batch_summary, ensure_ascii=False, indent=2, sort_keys=True))
    except (
        BatchExtractionError,
        PipelineError,
        OSError,
        ValueError,
        KeyError,
        json.JSONDecodeError,
        ET.ParseError,
    ) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
