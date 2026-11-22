#!/usr/bin/env python3
"""
High-fidelity offline SFZ orchestral/folk renderer and mastering engine for Ironfield.

Reads standard SFZ instrument patches (e.g. CC0 VSCO 2 Community Edition) with multi-velocity
layers, round-robins, stereo recordings, and CC11/CC1 dynamic expression curves; mixes sections
with orchestral stage depth (per-track pan + reverb send); convolves through a realistic stereo
hall impulse response with seamless loop tail wrap-around; masters and encodes to OGG Vorbis.
"""
from __future__ import annotations

import math
import os
import re
import subprocess
import tempfile
from dataclasses import dataclass, field
from typing import Optional

import numpy as np
import soundfile as sf
from scipy.signal import butter, fftconvolve, sosfilt

SR = 44100

NOTE_MAP = {"c": 0, "d": 2, "e": 4, "f": 5, "g": 7, "a": 9, "b": 11}
NOTE_RE = re.compile(r"^([a-gA-G])([#b]?)(-?\d+)$")


def parse_midi_note(val: str) -> int:
    val = val.strip()
    if val.lstrip("-").isdigit():
        return int(val)
    m = NOTE_RE.match(val)
    if not m:
        raise ValueError(f"Invalid MIDI note: {val!r}")
    base = NOTE_MAP[m.group(1).lower()]
    acc = 1 if m.group(2) == "#" else (-1 if m.group(2) == "b" else 0)
    octv = int(m.group(3))
    return (octv + 1) * 12 + base + acc


@dataclass
class SfzRegion:
    sample: str
    lokey: int = 0
    hikey: int = 127
    pitch_keycenter: int = 60
    lovel: int = 0
    hivel: int = 127
    seq_length: int = 1
    seq_position: int = 1
    lorand: float = 0.0
    hirand: float = 1.0
    volume_db: float = 0.0
    pan: float = 0.0
    tune_cents: float = 0.0
    transpose: int = 0
    offset: int = 0
    loop_mode: str = "no_loop"
    loop_start: int = 0
    loop_end: int = 0
    ampeg_attack: float = 0.015
    ampeg_release: float = 0.25


class SfzInstrument:
    """Loads an SFZ file and its referenced WAV samples on demand."""

    def __init__(self, sfz_path: str):
        self.sfz_path = os.path.abspath(sfz_path)
        self.base_dir = os.path.dirname(self.sfz_path)
        self.regions: list[SfzRegion] = []
        self._sample_cache: dict[str, tuple[np.ndarray, int]] = {}
        self._rr_counter: dict[int, int] = {}
        self._parse(self.sfz_path)

    def _parse(self, path: str) -> None:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            text = f.read()
        # Strip // line comments
        lines = []
        for line in text.splitlines():
            idx = line.find("//")
            if idx >= 0:
                line = line[:idx]
            lines.append(line)
        clean = "\n".join(lines)

        default_path = ""
        global_ops: dict[str, str] = {}
        group_ops: dict[str, str] = {}
        section = ""
        region_ops: dict[str, str] = {}

        def flush_region() -> None:
            nonlocal region_ops
            if not region_ops or "sample" not in region_ops:
                region_ops = {}
                return
            merged = {**global_ops, **group_ops, **region_ops}
            rel = merged["sample"].replace("\\", "/").strip()
            if default_path and not os.path.isabs(rel):
                rel = os.path.join(default_path.replace("\\", "/"), rel)
            full = os.path.normpath(os.path.join(self.base_dir, rel))
            if not os.path.isfile(full):
                # Case-insensitive fallback for Windows-authored SFZ paths on Linux
                full = self._resolve_ci(full) or full

            key_op = merged.get("key")
            lokey = parse_midi_note(merged["lokey"]) if "lokey" in merged else (parse_midi_note(key_op) if key_op else 0)
            hikey = parse_midi_note(merged["hikey"]) if "hikey" in merged else (parse_midi_note(key_op) if key_op else 127)
            center = parse_midi_note(merged["pitch_keycenter"]) if "pitch_keycenter" in merged else (
                parse_midi_note(key_op) if key_op else lokey
            )
            reg = SfzRegion(
                sample=full,
                lokey=lokey,
                hikey=hikey,
                pitch_keycenter=center,
                lovel=int(float(merged.get("lovel", 0))),
                hivel=int(float(merged.get("hivel", 127))),
                seq_length=max(1, int(float(merged.get("seq_length", 1)))),
                seq_position=max(1, int(float(merged.get("seq_position", 1)))),
                lorand=float(merged.get("lorand", 0.0)),
                hirand=float(merged.get("hirand", 1.0)),
                volume_db=float(merged.get("volume", 0.0)),
                pan=float(merged.get("pan", 0.0)) / 100.0,
                tune_cents=float(merged.get("tune", 0.0)),
                transpose=int(float(merged.get("transpose", 0))),
                offset=int(float(merged.get("offset", 0))),
                loop_mode=merged.get("loop_mode", "no_loop").strip().lower(),
                loop_start=int(float(merged.get("loop_start", 0))),
                loop_end=int(float(merged.get("loop_end", 0))),
                ampeg_attack=max(0.002, float(merged.get("ampeg_attack", 0.015))),
                ampeg_release=max(0.04, float(merged.get("ampeg_release", 0.28))),
            )
            if os.path.isfile(reg.sample):
                self.regions.append(reg)
            region_ops = {}

        # Tokenize headers (<...>) and opcode=value pairs (allowing spaces in sample=...)
        token_re = re.compile(r"<([a-zA-Z0-9_]+)>|([a-zA-Z0-9_]+)=([^\n<>]+)")
        for m in token_re.finditer(clean):
            if m.group(1):
                if section == "region":
                    flush_region()
                section = m.group(1).lower()
                if section == "group":
                    group_ops = {}
                elif section == "region":
                    region_ops = {}
            else:
                # A single line can contain multiple opcodes like `lokey=60 hikey=64` unless it's `sample=` or `default_path=`
                k = m.group(2).lower()
                rest = m.group(3).strip()
                if k in ("sample", "default_path"):
                    # Stop before the next opcode= if present on the same line
                    next_op = re.search(r"\s+([a-zA-Z0-9_]+)=", rest)
                    pairs = [(k, rest[:next_op.start()].strip())] if next_op else [(k, rest)]
                    if next_op:
                        tail = rest[next_op.start():]
                        for pm in re.finditer(r"([a-zA-Z0-9_]+)=([^\s]+)", tail):
                            pairs.append((pm.group(1).lower(), pm.group(2)))
                else:
                    pairs = [(k, rest.split()[0])]
                    tail = rest[len(pairs[0][1]):]
                    for pm in re.finditer(r"([a-zA-Z0-9_]+)=([^\s]+)", tail):
                        pairs.append((pm.group(1).lower(), pm.group(2)))

                for pk, pv in pairs:
                    if section == "control" and pk == "default_path":
                        default_path = pv
                    elif section == "global":
                        global_ops[pk] = pv
                    elif section == "group":
                        group_ops[pk] = pv
                    elif section == "region":
                        region_ops[pk] = pv
        if section == "region":
            flush_region()

    @staticmethod
    def _resolve_ci(path: str) -> Optional[str]:
        parts = os.path.normpath(path).split(os.sep)
        cur = os.sep if path.startswith(os.sep) else "."
        for p in parts:
            if not p:
                continue
            cand = os.path.join(cur, p)
            if os.path.exists(cand):
                cur = cand
                continue
            try:
                entries = os.listdir(cur)
            except OSError:
                return None
            match = next((e for e in entries if e.lower() == p.lower()), None)
            if match is None:
                return None
            cur = os.path.join(cur, match)
        return cur if os.path.isfile(cur) else None

    def _load_wav(self, path: str) -> tuple[np.ndarray, int]:
        if path not in self._sample_cache:
            data, sr = sf.read(path, dtype="float32", always_2d=True)
            if data.shape[1] == 1:
                data = np.repeat(data, 2, axis=1)
            elif data.shape[1] > 2:
                data = data[:, :2]
            self._sample_cache[path] = (data, sr)
        return self._sample_cache[path]

    def pick_region(self, midi: int, vel_127: int) -> Optional[SfzRegion]:
        cands = [r for r in self.regions if r.lokey <= midi <= r.hikey and r.lovel <= vel_127 <= r.hivel]
        if not cands:
            # Fallback: nearest key & velocity
            key_cands = [r for r in self.regions if r.lokey <= midi <= r.hikey]
            if not key_cands and self.regions:
                best_d = min(abs(midi - r.pitch_keycenter) for r in self.regions)
                key_cands = [r for r in self.regions if abs(midi - r.pitch_keycenter) == best_d]
            if not key_cands:
                return None
            cands = [min(key_cands, key=lambda r: abs(vel_127 - 0.5 * (r.lovel + r.hivel)))]

        # Round-robin selection
        max_seq = max(r.seq_length for r in cands)
        if max_seq > 1:
            rr = self._rr_counter.get(midi, 0) % max_seq
            self._rr_counter[midi] = rr + 1
            rr_cands = [r for r in cands if (r.seq_position - 1) % max_seq == rr]
            if rr_cands:
                cands = rr_cands
        elif len(cands) > 1:
            rr = self._rr_counter.get(midi, 0) % len(cands)
            self._rr_counter[midi] = rr + 1
            return cands[rr]
        return cands[0]

    def render_note(
        self,
        midi: int,
        dur_sec: float,
        vel: float,
        expr_curve: Optional[np.ndarray] = None,
        attack_override: Optional[float] = None,
        release_override: Optional[float] = None,
    ) -> Optional[np.ndarray]:
        vel_127 = max(1, min(127, int(round(vel * 127))))
        reg = self.pick_region(midi, vel_127)
        if reg is None:
            return None

        wav, src_sr = self._load_wav(reg.sample)
        if reg.offset > 0 and reg.offset < len(wav):
            wav = wav[reg.offset:]
        n_src = len(wav)
        if n_src < 8:
            return None

        semi = (midi - reg.pitch_keycenter) + reg.transpose + reg.tune_cents / 100.0
        rate_ratio = (2.0 ** (semi / 12.0)) * (src_sr / float(SR))

        atk = attack_override if attack_override is not None else reg.ampeg_attack
        rel = release_override if release_override is not None else reg.ampeg_release
        total_sec = dur_sec + rel
        out_frames = max(16, int(round(total_sec * SR)))
        hold_frames = max(1, int(round(dur_sec * SR)))

        # Compute source sample positions
        looped = reg.loop_mode in ("loop_continuous", "loop_sustain") and reg.loop_end > reg.loop_start + 32
        pos = np.arange(out_frames, dtype=np.float64) * rate_ratio

        if looped:
            ls = float(reg.loop_start)
            le = float(min(reg.loop_end, n_src - 2))
            span = max(1.0, le - ls)
            mask = pos >= ls
            pos[mask] = ls + np.mod(pos[mask] - ls, span)
        else:
            valid = pos < (n_src - 1.001)
            if not np.any(valid):
                return None
            out_frames = int(np.count_nonzero(valid))
            pos = pos[:out_frames]

        idx = pos.astype(np.int64)
        frac = (pos - idx).astype(np.float32)[:, None]
        out = wav[idx] * (1.0 - frac) + wav[np.minimum(idx + 1, n_src - 1)] * frac

        # Attack / release envelope
        env = np.ones(out_frames, dtype=np.float32)
        atk_n = min(out_frames, max(1, int(round(atk * SR))))
        if atk_n > 1:
            env[:atk_n] = np.linspace(0.0, 1.0, atk_n, dtype=np.float32)
        if out_frames > hold_frames:
            rel_n = out_frames - hold_frames
            env[hold_frames:] *= np.linspace(1.0, 0.0, rel_n, dtype=np.float32) ** 1.6

        # Velocity gain (gentle curve so pp isn't inaudible and ff has punch)
        vel_gain = (0.28 + 0.72 * (vel ** 1.25)) * (10.0 ** (reg.volume_db / 20.0))
        if expr_curve is not None:
            e = expr_curve[:out_frames]
            if len(e) < out_frames:
                e = np.pad(e, (0, out_frames - len(e)), mode="edge")
            env *= e.astype(np.float32)

        out *= (env * vel_gain)[:, None]

        # Subtle velocity/expression darkness filter for soft notes
        avg_dyn = float(vel * (np.mean(expr_curve[:out_frames]) if expr_curve is not None and len(expr_curve) else 1.0))
        if avg_dyn < 0.72 and out_frames > 64:
            cutoff = 1800.0 + avg_dyn * 7500.0
            sos = butter(1, min(cutoff, SR * 0.45) / (SR * 0.5), btype="low", output="sos")
            out[:, 0] = sosfilt(sos, out[:, 0])
            out[:, 1] = sosfilt(sos, out[:, 1])

        return out


@dataclass
class NoteEvent:
    beat: float
    midi: int
    dur_beats: float
    vel: float = 0.75
    attack: Optional[float] = None
    release: Optional[float] = None


@dataclass
class TrackSpec:
    name: str
    sfz: str
    pan: float = 0.0           # -1 (left) .. +1 (right)
    gain: float = 0.7
    reverb_send: float = 0.28  # 0.15 (close/dry) .. 0.48 (back of hall)
    stem: int = 0              # 0..3 for adaptive battle stems
    humanize_ms: float = 10.0
    notes: list[NoteEvent] = field(default_factory=list)
    expr_points: list[tuple[float, float]] = field(default_factory=list)  # [(beat, 0..1)]


def make_hall_ir(decay_sec: float = 2.1, predelay_ms: float = 18.0, seed: int = 42) -> np.ndarray:
    """Synthesize a warm, realistic stereo concert/stone hall impulse response (no metallic comb ringing)."""
    rng = np.random.default_rng(seed)
    n = int(round((decay_sec + 0.3) * SR))
    ir = np.zeros((n, 2), dtype=np.float32)
    t = np.arange(n, dtype=np.float32) / SR

    # Early reflections (distinct stereo hall taps, 12..85 ms)
    taps = [
        (0.014, -0.35, 0.34), (0.019, 0.40, 0.30), (0.027, -0.55, 0.25),
        (0.034, 0.25, 0.22),  (0.043, 0.60, 0.19), (0.052, -0.45, 0.16),
        (0.064, 0.30, 0.13),  (0.078, -0.25, 0.10),
    ]
    for sec, pan, amp in taps:
        idx = int(round(sec * SR))
        if idx < n:
            ang = (pan + 1.0) * math.pi * 0.25
            ir[idx, 0] += amp * math.cos(ang)
            ir[idx, 1] += amp * math.sin(ang)

    # Diffuse stereo tail with frequency-dependent decay (highs decay faster than lows)
    pre = int(round(predelay_ms * 0.001 * SR))
    tail_len = n - pre
    if tail_len > 64:
        noise = rng.standard_normal((tail_len, 2)).astype(np.float32)
        tt = np.arange(tail_len, dtype=np.float32) / SR
        fade_in = np.minimum(1.0, tt / 0.045)
        low_env = fade_in * np.exp(-6.9 * tt / decay_sec)
        high_env = fade_in * np.exp(-6.9 * tt / (decay_sec * 0.42))

        sos_lo = butter(2, 1400.0 / (SR * 0.5), btype="low", output="sos")
        sos_hi = butter(2, [1400.0 / (SR * 0.5), 6800.0 / (SR * 0.5)], btype="band", output="sos")
        lo = np.column_stack([sosfilt(sos_lo, noise[:, 0]), sosfilt(sos_lo, noise[:, 1])])
        hi = np.column_stack([sosfilt(sos_hi, noise[:, 0]), sosfilt(sos_hi, noise[:, 1])])
        diffuse = lo * low_env[:, None] * 0.55 + hi * high_env[:, None] * 0.28
        ir[pre:] += diffuse.astype(np.float32)

    # Normalize IR energy so wet=0.25..0.40 sits naturally
    rms = float(np.sqrt(np.mean(ir ** 2))) + 1e-9
    ir /= (rms * 18.0)
    return ir


def build_tempo_map(
    total_beats: float,
    base_bpm: float,
    rubato: Optional[list[tuple[float, float]]] = None,
) -> tuple[np.ndarray, np.ndarray]:
    """Build a (beats, seconds) lookup table supporting smooth rubato / ritardando."""
    steps = max(64, int(round(total_beats * 8)) + 1)
    beats = np.linspace(0.0, total_beats, steps)
    if not rubato:
        return beats, beats * (60.0 / base_bpm)
    rb = np.array([p[0] for p in rubato], dtype=np.float64)
    rm = np.array([p[1] for p in rubato], dtype=np.float64)
    bpm_curve = base_bpm * np.interp(beats, rb, rm)
    dt = np.diff(beats) * (60.0 / (0.5 * (bpm_curve[:-1] + bpm_curve[1:])))
    secs = np.concatenate([[0.0], np.cumsum(dt)])
    return beats, secs


def render_piece(
    tracks: list[TrackSpec],
    total_beats: float,
    base_bpm: float,
    loop: bool = True,
    rubato: Optional[list[tuple[float, float]]] = None,
    reverb_decay: float = 2.2,
    instrument_cache: Optional[dict[str, SfzInstrument]] = None,
    multi_stem: bool = False,
    seed: int = 101,
) -> dict[int, np.ndarray]:
    """
    Render tracks to stereo float32 buffers.
    Returns `{0: stereo}` if `multi_stem=False`, or `{0: s0, 1: s1, 2: s2, 3: s3}` if `multi_stem=True`.
    """
    cache = instrument_cache if instrument_cache is not None else {}
    beats_lut, secs_lut = build_tempo_map(total_beats, base_bpm, rubato)
    loop_sec = float(secs_lut[-1])
    tail_sec = 0.0 if loop else (reverb_decay + 1.2)
    loop_frames = int(round(loop_sec * SR))
    buf_frames = loop_frames + int(round((reverb_decay + 2.5) * SR))

    stems_used = sorted({t.stem for t in tracks}) if multi_stem else [0]
    dry_stems = {s: np.zeros((buf_frames, 2), dtype=np.float32) for s in stems_used}
    wet_stems = {s: np.zeros((buf_frames, 2), dtype=np.float32) for s in stems_used}

    rng = np.random.default_rng(seed)

    for track in tracks:
        if not track.notes:
            continue
        if track.sfz not in cache:
            cache[track.sfz] = SfzInstrument(track.sfz)
        inst = cache[track.sfz]

        # Precompute track expression curve in seconds if provided
        ex_s = ex_v = None
        if track.expr_points:
            ep = sorted(track.expr_points, key=lambda p: p[0])
            ex_s = np.interp([p[0] for p in ep], beats_lut, secs_lut)
            ex_v = np.array([p[1] for p in ep], dtype=np.float32)

        # Stereo seating: equal-power pan preserving natural stereo width of recordings
        pan = max(-1.0, min(1.0, track.pan))
        g_l = math.sqrt(0.5 * (1.0 - pan)) * track.gain
        g_r = math.sqrt(0.5 * (1.0 + pan)) * track.gain

        s_idx = track.stem if multi_stem else 0
        dry_dst = dry_stems[s_idx]
        wet_dst = wet_stems[s_idx]

        for n in track.notes:
            t0 = float(np.interp(n.beat, beats_lut, secs_lut))
            t1 = float(np.interp(n.beat + n.dur_beats, beats_lut, secs_lut))
            jitter = rng.uniform(-track.humanize_ms, track.humanize_ms) * 0.001 if track.humanize_ms > 0 else 0.0
            start_sec = max(0.0, t0 + jitter)
            dur_sec = max(0.04, t1 - t0)

            expr_arr = None
            if ex_s is not None and ex_v is not None:
                n_est = max(16, int(round((dur_sec + 1.5) * SR)))
                ts = start_sec + np.arange(n_est, dtype=np.float64) / SR
                expr_arr = np.interp(ts, ex_s, ex_v).astype(np.float32)

            vel_j = max(0.05, min(1.0, n.vel * float(rng.uniform(0.95, 1.05))))
            smp = inst.render_note(n.midi, dur_sec, vel_j, expr_arr, n.attack, n.release)
            if smp is None:
                continue

            i0 = int(round(start_sec * SR))
            i1 = min(buf_frames, i0 + len(smp))
            if i1 <= i0:
                continue
            seg = smp[: i1 - i0]
            # Mix preserving stereo image while steering center via pan
            mono_bias = 0.15
            l = (seg[:, 0] * (1.0 - mono_bias) + seg[:, 1] * mono_bias) * g_l
            r = (seg[:, 1] * (1.0 - mono_bias) + seg[:, 0] * mono_bias) * g_r

            dry_dst[i0:i1, 0] += l
            dry_dst[i0:i1, 1] += r
            wet_dst[i0:i1, 0] += l * track.reverb_send
            wet_dst[i0:i1, 1] += r * track.reverb_send

    ir = make_hall_ir(decay_sec=reverb_decay, seed=seed + 7)
    out_frames = loop_frames if loop else int(round((loop_sec + tail_sec) * SR))
    result: dict[int, np.ndarray] = {}

    for s in stems_used:
        wet_l = fftconvolve(wet_stems[s][:, 0], ir[:, 0], mode="full")[:buf_frames]
        wet_r = fftconvolve(wet_stems[s][:, 1], ir[:, 1], mode="full")[:buf_frames]
        mix = dry_stems[s] + np.column_stack([wet_l, wet_r]).astype(np.float32)

        if loop:
            # Wrap reverb/note tail past loop_frames seamlessly onto the start of the loop!
            final = mix[:loop_frames].copy()
            over = mix[loop_frames:]
            wrap_n = min(len(over), loop_frames)
            final[:wrap_n] += over[:wrap_n]
            result[s] = final
        else:
            final = mix[:out_frames].copy()
            fade_n = min(out_frames // 6, int(0.8 * SR))
            if fade_n > 1:
                final[-fade_n:] *= np.linspace(1.0, 0.0, fade_n, dtype=np.float32)[:, None]
            result[s] = final

    # Master: gentle high-pass, normalize across full sum so multi-stem tracks stay balanced
    sos_hp = butter(2, 28.0 / (SR * 0.5), btype="high", output="sos")
    for s in stems_used:
        result[s][:, 0] = sosfilt(sos_hp, result[s][:, 0])
        result[s][:, 1] = sosfilt(sos_hp, result[s][:, 1])

    combined = sum(result.values())
    rms = float(np.sqrt(np.mean(combined ** 2))) + 1e-9
    target_rms = 0.14  # ~ -17 dBFS orchestral sweet spot (dynamic, never squashed)
    scale = min(3.5, target_rms / rms)
    peak = float(np.max(np.abs(combined))) * scale
    if peak > 0.94:
        scale *= 0.94 / peak

    for s in stems_used:
        x = result[s] * scale
        # Gentle soft-clip safety ceiling
        over = np.abs(x) > 0.85
        if np.any(over):
            x[over] = np.sign(x[over]) * (0.85 + 0.13 * np.tanh((np.abs(x[over]) - 0.85) * 4.0))
        result[s] = x.astype(np.float32)

    return result


def export_ogg(stereo: np.ndarray, out_path: str, quality: float = 6.0) -> None:
    """Write a 44.1 kHz stereo float32 array to an OGG Vorbis file via ffmpeg."""
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tf:
        wav_path = tf.name
    try:
        sf.write(wav_path, stereo, SR, subtype="PCM_24")
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libvorbis", "-q:a", str(quality), out_path],
            check=True,
        )
    finally:
        if os.path.exists(wav_path):
            os.remove(wav_path)
