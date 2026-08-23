#!/usr/bin/env python3
"""Extract and analyze selected Enter the Gungeon Wwise media for reference."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
import struct
import wave
from pathlib import Path

import numpy as np

from forest_audio_wav import analyze, read_wav


DEFAULT_GAME_ROOT = Path(r"E:\Steam\steamapps\common\Enter the Gungeon")
DEFAULT_BANK_ROOT = DEFAULT_GAME_ROOT / "EtG_Data" / "StreamingAssets" / "Audio" / "GeneratedSoundBanks" / "Windows"
DEFAULT_OUTPUT = Path(r"D:\Unity\NewFPG_AudioWork\Forest\references\gungeon")

IMA_INDEX_TABLE = (-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8)
IMA_STEP_TABLE = (
    7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
    34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130,
    143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449,
    494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411,
    1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026,
    4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442,
    11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623,
    27086, 29794, 32767,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bank", type=Path, default=DEFAULT_BANK_ROOT / "SFX.bnk")
    parser.add_argument("--metadata", type=Path, default=DEFAULT_BANK_ROOT / "SFX.txt")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--media-id", type=int, action="append", required=True)
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_bank(path: Path) -> tuple[bytes, dict[int, tuple[int, int]], int]:
    blob = path.read_bytes()
    position = 0
    didx = b""
    data_offset = -1
    while position + 8 <= len(blob):
        tag = blob[position : position + 4]
        size = struct.unpack_from("<I", blob, position + 4)[0]
        start = position + 8
        if tag == b"DIDX":
            didx = blob[start : start + size]
        elif tag == b"DATA":
            data_offset = start
        position = start + size
    if not didx or data_offset < 0:
        raise ValueError(f"Wwise DIDX/DATA chunks were not found in {path}")
    entries = {
        media_id: (offset, size)
        for media_id, offset, size in struct.iter_unpack("<III", didx)
    }
    return blob, entries, data_offset


def parse_media_metadata(path: Path) -> dict[int, dict[str, object]]:
    records: dict[int, dict[str, object]] = {}
    with path.open("r", encoding="utf-8-sig", errors="replace") as stream:
        for line in stream:
            columns = [column.strip() for column in line.rstrip("\r\n").split("\t") if column.strip()]
            if len(columns) < 4 or not columns[0].isdigit():
                continue
            wem_path = next((column for column in columns if column.lower().endswith(".wem")), "")
            if not wem_path:
                continue
            object_path = next((column for column in columns if column.startswith("\\Actor-Mixer")), "")
            records[int(columns[0])] = {
                "mediaId": int(columns[0]),
                "name": columns[1],
                "wwiseCachePath": wem_path,
                "objectPath": object_path,
                "encodedBytes": int(columns[-1]) if columns[-1].isdigit() else 0,
            }
    return records


def riff_chunks(blob: bytes) -> dict[bytes, bytes]:
    if len(blob) < 12 or blob[:4] != b"RIFF" or blob[8:12] != b"WAVE":
        raise ValueError("Embedded media is not a RIFF WAVE file")
    chunks: dict[bytes, bytes] = {}
    position = 12
    while position + 8 <= len(blob):
        tag = blob[position : position + 4]
        size = struct.unpack_from("<I", blob, position + 4)[0]
        start = position + 8
        chunks[tag] = blob[start : start + size]
        position = start + size + (size & 1)
    return chunks


def clamp_pcm16(value: int) -> int:
    return max(-32768, min(32767, value))


def decode_wwise_adpcm(blob: bytes) -> tuple[int, int, np.ndarray]:
    chunks = riff_chunks(blob)
    fmt = chunks[b"fmt "]
    format_tag, channels, sample_rate, _, block_align, bits_per_sample = struct.unpack_from("<HHIIHH", fmt, 0)
    if format_tag != 2 or bits_per_sample != 4 or channels not in (1, 2):
        raise ValueError(f"Unsupported Wwise WAV format tag={format_tag}, bits={bits_per_sample}, channels={channels}")
    encoded = chunks[b"data"]
    decoded_blocks: list[np.ndarray] = []
    if block_align % channels != 0:
        raise ValueError("Wwise ADPCM block alignment is not divisible by its channel count")
    channel_block_size = block_align // channels
    for block_start in range(0, len(encoded), block_align):
        block = encoded[block_start : block_start + block_align]
        if len(block) < block_align:
            continue
        channel_samples: list[list[int]] = []
        for channel in range(channels):
            start = channel * channel_block_size
            channel_block = block[start : start + channel_block_size]
            predictor, step_index, _ = struct.unpack_from("<hBB", channel_block, 0)
            if step_index >= len(IMA_STEP_TABLE):
                raise ValueError("Wwise ADPCM step index is out of range")
            samples = [predictor]
            for packed in channel_block[4:]:
                for nibble in (packed & 0x0F, packed >> 4):
                    step = IMA_STEP_TABLE[step_index]
                    difference = step >> 3
                    if nibble & 1:
                        difference += step >> 2
                    if nibble & 2:
                        difference += step >> 1
                    if nibble & 4:
                        difference += step
                    predictor = clamp_pcm16(predictor - difference if nibble & 8 else predictor + difference)
                    step_index = max(0, min(88, step_index + IMA_INDEX_TABLE[nibble]))
                    samples.append(predictor)
                    if len(samples) == 64:
                        break
                if len(samples) == 64:
                    break
            channel_samples.append(samples)

        frame_count = min(len(channel) for channel in channel_samples)
        decoded_blocks.append(
            np.column_stack([np.asarray(samples[:frame_count], dtype=np.int16) for samples in channel_samples])
        )

    decoded = np.concatenate(decoded_blocks, axis=0) if decoded_blocks else np.empty((0, channels), dtype=np.int16)
    return sample_rate, channels, decoded


def write_pcm16(path: Path, sample_rate: int, channels: int, samples: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(channels)
        stream.setsampwidth(2)
        stream.setframerate(sample_rate)
        stream.writeframes(samples.astype("<i2", copy=False).tobytes())


def spectral_metrics(path: Path) -> dict[str, float]:
    sample_rate, data, _ = read_wav(path)
    mono = np.mean(data, axis=1)
    magnitude = np.abs(mono)
    peak = float(np.max(magnitude)) if len(magnitude) else 0.0
    peak_index = int(np.argmax(magnitude)) if len(magnitude) else 0
    active = np.flatnonzero(magnitude >= peak * 0.01) if peak > 0 else np.empty(0, dtype=int)
    effective_ms = 0.0 if len(active) == 0 else (active[-1] - active[0] + 1) * 1000.0 / sample_rate

    if len(mono) < 2:
        return {"peakTimeMs": 0.0, "effectiveDurationMs": effective_ms, "spectralCentroidHz": 0.0, "rolloff85Hz": 0.0}
    spectrum = np.abs(np.fft.rfft(mono * np.hanning(len(mono))))
    frequencies = np.fft.rfftfreq(len(mono), 1.0 / sample_rate)
    total = float(np.sum(spectrum))
    centroid = float(np.sum(frequencies * spectrum) / total) if total > 0 else 0.0
    cumulative = np.cumsum(spectrum)
    rolloff_index = int(np.searchsorted(cumulative, cumulative[-1] * 0.85)) if total > 0 else 0
    rolloff = float(frequencies[min(rolloff_index, len(frequencies) - 1)])
    return {
        "peakTimeMs": round(peak_index * 1000.0 / sample_rate, 3),
        "effectiveDurationMs": round(effective_ms, 3),
        "spectralCentroidHz": round(centroid, 3),
        "rolloff85Hz": round(rolloff, 3),
    }


def safe_filename(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", name).strip("_")


def main() -> int:
    args = parse_args()
    metadata = parse_media_metadata(args.metadata)
    bank, entries, data_offset = read_bank(args.bank)
    args.output.mkdir(parents=True, exist_ok=True)
    rows: list[dict[str, object]] = []

    for media_id in args.media_id:
        if media_id not in metadata:
            raise KeyError(f"Media ID {media_id} is absent from {args.metadata}")
        if media_id not in entries:
            raise KeyError(f"Media ID {media_id} is absent from {args.bank}")
        offset, size = entries[media_id]
        embedded = bank[data_offset + offset : data_offset + offset + size]
        record = metadata[media_id]
        base_name = f"{media_id}_{safe_filename(str(record['name']))}"
        wem_path = args.output / f"{base_name}.wem"
        wav_path = args.output / f"{base_name}.wav"
        wem_path.write_bytes(embedded)
        sample_rate, channels, samples = decode_wwise_adpcm(embedded)
        write_pcm16(wav_path, sample_rate, channels, samples)
        row = {
            **record,
            "referenceOnly": True,
            "notForProjectImport": True,
            "wemPath": str(wem_path),
            "wavPath": str(wav_path),
            "wemSha256": sha256_file(wem_path),
            **analyze(wav_path),
            **spectral_metrics(wav_path),
        }
        rows.append(row)

    report = {
        "sourceBank": str(args.bank),
        "sourceBankSha256": sha256_file(args.bank),
        "referenceOnly": True,
        "notForProjectImport": True,
        "media": rows,
    }
    report_path = args.output / "gungeon_reference_analysis.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    csv_path = args.output / "gungeon_reference_analysis.csv"
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    print(json.dumps({"report": str(report_path), "mediaCount": len(rows)}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
