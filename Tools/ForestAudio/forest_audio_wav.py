#!/usr/bin/env python3
"""Analyze and non-destructively render Forest audio WAV masters."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import wave
from fractions import Fraction
from pathlib import Path

import numpy as np
from scipy.io import wavfile
from scipy.signal import resample_poly


def db_to_gain(value: float) -> float:
    return 10.0 ** (value / 20.0)


def amplitude_to_db(value: float) -> float:
    return -120.0 if value <= 0.0 else 20.0 * math.log10(value)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_wav(path: Path) -> tuple[int, np.ndarray, int]:
    sample_rate, raw = wavfile.read(path)
    bits = raw.dtype.itemsize * 8
    try:
        with wave.open(str(path), "rb") as stream:
            bits = stream.getsampwidth() * 8
    except wave.Error:
        # IEEE-float WAV files are supported by scipy even when the stdlib
        # wave module cannot parse their format tag.
        pass
    if np.issubdtype(raw.dtype, np.floating):
        data = raw.astype(np.float64)
    elif raw.dtype == np.uint8:
        data = (raw.astype(np.float64) - 128.0) / 128.0
    else:
        info = np.iinfo(raw.dtype)
        data = raw.astype(np.float64) / float(max(abs(info.min), info.max))
    if data.ndim == 1:
        data = data[:, np.newaxis]
    return int(sample_rate), data, bits


def resample(data: np.ndarray, source_rate: int, target_rate: int) -> np.ndarray:
    if source_rate == target_rate:
        return data
    divisor = math.gcd(source_rate, target_rate)
    return resample_poly(data, target_rate // divisor, source_rate // divisor, axis=0)


def set_channels(data: np.ndarray, mode: str) -> np.ndarray:
    if mode == "preserve":
        return data
    if mode == "mono":
        return np.mean(data, axis=1, keepdims=True)
    if mode == "stereo":
        if data.shape[1] == 1:
            return np.repeat(data, 2, axis=1)
        if data.shape[1] == 2:
            return data
        return data[:, :2]
    raise ValueError(f"Unsupported channel mode: {mode}")


def shift_pitch_by_resampling(data: np.ndarray, semitones: float) -> np.ndarray:
    """Shift pitch and playback length together, matching tape-style game SFX layering."""
    if abs(semitones) < 1e-6:
        return data
    playback_ratio = 2.0 ** (semitones / 12.0)
    ratio = Fraction(playback_ratio).limit_denominator(1000)
    return resample_poly(data, ratio.denominator, ratio.numerator, axis=0)


def apply_transient_boost(
    data: np.ndarray,
    sample_rate: int,
    boost_db: float,
    window_ms: float,
) -> None:
    if boost_db <= 0.0 or window_ms <= 0.0 or len(data) == 0:
        return
    frames = min(len(data), max(1, round(sample_rate * window_ms / 1000.0)))
    start_gain = db_to_gain(boost_db)
    envelope = np.linspace(start_gain, 1.0, frames, endpoint=True)[:, np.newaxis]
    data[:frames] *= envelope


def apply_soft_saturation(data: np.ndarray, drive: float) -> None:
    if drive <= 0.0:
        return
    data[:] = np.tanh(data * drive) / math.tanh(drive)


def remove_dc_offset(data: np.ndarray) -> None:
    if len(data) == 0:
        return
    data -= np.mean(data, axis=0, keepdims=True)


def apply_fades(data: np.ndarray, sample_rate: int, fade_in_ms: float, fade_out_ms: float) -> None:
    fade_in = min(len(data), round(sample_rate * fade_in_ms / 1000.0))
    fade_out = min(len(data), round(sample_rate * fade_out_ms / 1000.0))
    envelope = np.ones(len(data), dtype=np.float64)
    if fade_in > 0:
        envelope[:fade_in] *= np.linspace(0.0, 1.0, fade_in, endpoint=True)
    if fade_out > 0:
        envelope[-fade_out:] *= np.linspace(1.0, 0.0, fade_out, endpoint=True)

    envelope_sum = float(np.sum(envelope))
    if envelope_sum > 0.0:
        weighted_mean = np.sum(data * envelope[:, np.newaxis], axis=0) / envelope_sum
        data -= weighted_mean[np.newaxis, :]
    data *= envelope[:, np.newaxis]


def apply_loop_crossfade(data: np.ndarray, sample_rate: int, milliseconds: float) -> np.ndarray:
    overlap = round(sample_rate * milliseconds / 1000.0)
    if overlap <= 0:
        return data
    if overlap * 2 >= len(data):
        raise ValueError("Loop crossfade must be shorter than half the rendered clip")
    ramp = np.linspace(0.0, 1.0, overlap, endpoint=True)[:, np.newaxis]
    blended = data[-overlap:] * (1.0 - ramp) + data[:overlap] * ramp
    return np.concatenate((blended, data[overlap:-overlap]), axis=0)


def write_pcm24(path: Path, sample_rate: int, data: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    clipped = np.clip(data, -1.0, 1.0 - 1.0 / 8388608.0)
    integers = np.rint(clipped * 8388607.0).astype(np.int32).reshape(-1)
    packed = np.empty((integers.size, 3), dtype=np.uint8)
    packed[:, 0] = integers & 0xFF
    packed[:, 1] = (integers >> 8) & 0xFF
    packed[:, 2] = (integers >> 16) & 0xFF
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(data.shape[1])
        stream.setsampwidth(3)
        stream.setframerate(sample_rate)
        stream.writeframes(packed.tobytes())


def analyze(path: Path) -> dict[str, object]:
    sample_rate, data, bits = read_wav(path)
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    frame_peak = np.max(np.abs(data), axis=1) if len(data) else np.empty(0)
    peak_frame = int(np.argmax(frame_peak)) if len(frame_peak) else 0
    active_frames = np.flatnonzero(frame_peak >= peak * 0.01) if peak > 0.0 else np.empty(0, dtype=int)
    active_start = int(active_frames[0]) if len(active_frames) else 0
    active_end = int(active_frames[-1]) + 1 if len(active_frames) else 0
    rms = float(np.sqrt(np.mean(np.square(data)))) if data.size else 0.0
    dc = float(np.max(np.abs(np.mean(data, axis=0)))) if data.size else 0.0
    seam_delta = float(np.max(np.abs(data[0] - data[-1]))) if len(data) > 1 else 0.0
    window = min(len(data) // 2, max(1, round(sample_rate * 0.05)))
    seam_rms = 0.0
    if window > 0:
        seam_rms = float(np.sqrt(np.mean(np.square(data[:window] - data[-window:]))))
    return {
        "path": str(path),
        "sha256": sha256_file(path),
        "sampleRate": sample_rate,
        "bitDepth": bits,
        "channels": data.shape[1],
        "samples": len(data),
        "durationSeconds": len(data) / sample_rate if sample_rate else 0.0,
        "peakTimeMs": round(peak_frame * 1000.0 / sample_rate, 3) if sample_rate else 0.0,
        "activeStartMs": round(active_start * 1000.0 / sample_rate, 3) if sample_rate else 0.0,
        "activeDurationMs": round((active_end - active_start) * 1000.0 / sample_rate, 3) if sample_rate else 0.0,
        "peakDbfs": round(amplitude_to_db(peak), 3),
        "rmsDbfs": round(amplitude_to_db(rms), 3),
        "dcOffset": dc,
        "loopSeamDelta": seam_delta,
        "loopSeamRms50ms": seam_rms,
        "clippedSamples": int(np.count_nonzero(np.abs(data) >= 1.0)),
    }


def render(args: argparse.Namespace) -> dict[str, object]:
    gains = args.input_gain_db or []
    if gains and len(gains) != len(args.input):
        raise ValueError("--input-gain-db count must match --input count")
    if not gains:
        gains = [0.0] * len(args.input)
    input_trim_starts = getattr(args, "input_trim_start", None) or []
    if input_trim_starts and len(input_trim_starts) != len(args.input):
        raise ValueError("--input-trim-start count must match --input count")
    if not input_trim_starts:
        input_trim_starts = [0.0] * len(args.input)
    input_pitch_semitones = getattr(args, "input_pitch_semitones", None) or []
    if input_pitch_semitones and len(input_pitch_semitones) != len(args.input):
        raise ValueError("--input-pitch-semitones count must match --input count")
    if not input_pitch_semitones:
        input_pitch_semitones = [0.0] * len(args.input)
    input_delay_ms = getattr(args, "input_delay_ms", None) or []
    if input_delay_ms and len(input_delay_ms) != len(args.input):
        raise ValueError("--input-delay-ms count must match --input count")
    if not input_delay_ms:
        input_delay_ms = [0.0] * len(args.input)

    layers: list[np.ndarray] = []
    for path, gain_db, input_trim_start, pitch_semitones, delay_ms in zip(
        args.input,
        gains,
        input_trim_starts,
        input_pitch_semitones,
        input_delay_ms,
    ):
        source_rate, data, _ = read_wav(path)
        data = resample(data, source_rate, args.sample_rate)
        data = set_channels(data, args.channels)
        input_start = max(0, round(input_trim_start * args.sample_rate))
        data = data[input_start:]
        if len(data) == 0:
            raise ValueError(f"Input trim removes the entire source: {path}")
        data = shift_pitch_by_resampling(data, pitch_semitones)
        delay_frames = max(0, round(delay_ms * args.sample_rate / 1000.0))
        if delay_frames > 0:
            silence = np.zeros((delay_frames, data.shape[1]), dtype=np.float64)
            data = np.concatenate((silence, data), axis=0)
        layers.append(data * db_to_gain(gain_db))

    channel_count = layers[0].shape[1]
    if any(layer.shape[1] != channel_count for layer in layers):
        raise ValueError("All input layers must resolve to the same channel count")
    length = max(len(layer) for layer in layers)
    mixed = np.zeros((length, channel_count), dtype=np.float64)
    for layer in layers:
        mixed[: len(layer)] += layer

    start = max(0, round(args.trim_start * args.sample_rate))
    end = len(mixed)
    if args.duration is not None:
        end = min(end, start + round(args.duration * args.sample_rate))
    mixed = mixed[start:end].copy()
    if len(mixed) == 0:
        raise ValueError("Rendered selection is empty")

    if args.loop_crossfade_ms > 0:
        mixed = apply_loop_crossfade(mixed, args.sample_rate, args.loop_crossfade_ms)
    remove_dc_offset(mixed)
    transient_boost_db = getattr(args, "transient_boost_db", 0.0)
    transient_window_ms = getattr(args, "transient_window_ms", 0.0)
    saturation_drive = getattr(args, "saturation_drive", 0.0)
    apply_transient_boost(mixed, args.sample_rate, transient_boost_db, transient_window_ms)
    apply_soft_saturation(mixed, saturation_drive)
    apply_fades(mixed, args.sample_rate, args.fade_in_ms, args.fade_out_ms)

    peak = float(np.max(np.abs(mixed)))
    target_peak = db_to_gain(args.peak_dbfs)
    if peak > 0.0:
        mixed *= target_peak / peak
    write_pcm24(args.output, args.sample_rate, mixed)

    report = {
        "output": analyze(args.output),
        "inputs": [
            {
                "path": str(path),
                "gainDb": gain_db,
                "trimStart": trim_start,
                "pitchSemitones": pitch_semitones,
                "delayMs": delay_ms,
                "sha256": sha256_file(path),
            }
            for path, gain_db, trim_start, pitch_semitones, delay_ms in zip(
                args.input,
                gains,
                input_trim_starts,
                input_pitch_semitones,
                input_delay_ms,
            )
        ],
        "settings": {
            "sampleRate": args.sample_rate,
            "channels": args.channels,
            "trimStart": args.trim_start,
            "duration": args.duration,
            "fadeInMs": args.fade_in_ms,
            "fadeOutMs": args.fade_out_ms,
            "loopCrossfadeMs": args.loop_crossfade_ms,
            "peakDbfs": args.peak_dbfs,
            "dcOffsetRemoved": True,
            "transientBoostDb": transient_boost_db,
            "transientWindowMs": transient_window_ms,
            "saturationDrive": saturation_drive,
        },
    }
    report_path = args.report or args.output.with_suffix(".render.json")
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return report


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    analyze_parser = subparsers.add_parser("analyze")
    analyze_parser.add_argument("input", type=Path, nargs="+")
    analyze_parser.add_argument("--output", type=Path)

    render_parser = subparsers.add_parser("render")
    render_parser.add_argument("--input", type=Path, action="append", required=True)
    render_parser.add_argument("--input-gain-db", type=float, action="append")
    render_parser.add_argument("--input-trim-start", type=float, action="append")
    render_parser.add_argument("--input-pitch-semitones", type=float, action="append")
    render_parser.add_argument("--input-delay-ms", type=float, action="append")
    render_parser.add_argument("--output", type=Path, required=True)
    render_parser.add_argument("--report", type=Path)
    render_parser.add_argument("--sample-rate", type=int, default=48000)
    render_parser.add_argument("--channels", choices=("mono", "stereo", "preserve"), default="mono")
    render_parser.add_argument("--trim-start", type=float, default=0.0)
    render_parser.add_argument("--duration", type=float)
    render_parser.add_argument("--fade-in-ms", type=float, default=2.0)
    render_parser.add_argument("--fade-out-ms", type=float, default=10.0)
    render_parser.add_argument("--loop-crossfade-ms", type=float, default=0.0)
    render_parser.add_argument("--peak-dbfs", type=float, default=-1.0)
    render_parser.add_argument("--transient-boost-db", type=float, default=0.0)
    render_parser.add_argument("--transient-window-ms", type=float, default=0.0)
    render_parser.add_argument("--saturation-drive", type=float, default=0.0)
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.command == "render":
        print(json.dumps(render(args), ensure_ascii=False, indent=2))
        return 0

    rows = [analyze(path) for path in args.input]
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with args.output.open("w", encoding="utf-8-sig", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)
    print(json.dumps(rows, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
