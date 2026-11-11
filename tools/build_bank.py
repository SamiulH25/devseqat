#!/usr/bin/env python3
"""
Builds Data/audio/instruments.bank from the MIT-licensed FluidR3_GM.sf2 SoundFont.
Renders 28 multi-zone acoustic & culturally sculpted instruments via fluidsynth in one pass,
computes phase-matched crossfaded sustain loops, delta-encodes 16-bit PCM, and GZip-compresses
into a deterministic ~2.5-3.8 MB binary read by AudioBank.cs (zero runtime NuGet dependencies).
"""
import argparse
import gzip
import io
import math
import os
import struct
import subprocess
import tempfile
import wave
import mido
import numpy as np

SF2_CANDIDATES = [
    os.path.expanduser("~/.local/share/soundfonts/FluidR3_GM.sf2"),
    "/usr/share/sounds/sf2/FluidR3_GM.sf2",
    "/usr/share/soundfonts/FluidR3_GM.sf2",
]

# Category: 0 = Plucked/Key, 1 = Bowed, 2 = Wind/Reed, 3 = Brass/Voice, 4 = Percussion/Bell
# Spec: (id, category, looped, rate, dur_sec, attack, release, vib_depth, vib_rate, roots, layers, sculpt)
# Each layer in `layers`: (is_drum, program_or_note, gain, transpose_semitones)
VOICES = [
    # --- Plucked & Keyboard (6) ---
    ("lute", 0, False, 24000, 0.95, 0.003, 0.22, 0.0015, 5.0, [45, 53, 61, 69, 77],
     [(False, 24, 0.82, 0), (False, 46, 0.26, 0)], "wood_warm"),
    ("harp", 0, False, 24000, 1.05, 0.003, 0.28, 0.0010, 4.8, [43, 52, 61, 70, 79],
     [(False, 46, 1.0, 0)], "air_bright"),
    ("oud", 0, False, 24000, 0.92, 0.003, 0.20, 0.0025, 5.2, [43, 51, 59, 67, 75],
     [(False, 24, 0.72, 0), (False, 105, 0.34, 0), (False, 104, 0.12, 0)], "oud_belly"),
    ("cittern", 0, False, 26000, 0.88, 0.002, 0.18, 0.0012, 5.4, [48, 57, 66, 75, 83],
     [(False, 25, 0.78, 0), (False, 6, 0.28, 0)], "wire_crisp"),
    ("harpsichord", 0, False, 26000, 0.88, 0.002, 0.16, 0.0, 0.0, [48, 57, 66, 75, 84],
     [(False, 6, 1.0, 0)], "wire_crisp"),
    ("qanun", 0, False, 26000, 0.95, 0.002, 0.22, 0.0018, 5.6, [50, 58, 66, 74, 82],
     [(False, 15, 0.65, 0), (False, 46, 0.45, 0)], "air_bright"),

    # --- Bowed Strings (6) ---
    ("fiddle", 1, True, 24000, 1.25, 0.045, 0.16, 0.0055, 5.8, [55, 62, 69, 76, 83],
     [(False, 110, 0.65, 0), (False, 40, 0.45, 0)], "bow_rosin"),
    ("nyckelharpa", 1, True, 24000, 1.25, 0.040, 0.18, 0.0045, 5.4, [50, 57, 64, 71, 78],
     [(False, 110, 0.62, 0), (False, 41, 0.42, 0), (False, 46, 0.14, 7)], "sympathetic"),
    ("morin_khuur", 1, True, 22050, 1.28, 0.055, 0.20, 0.0065, 5.0, [43, 50, 57, 64, 71],
     [(False, 42, 0.72, 0), (False, 41, 0.35, 0)], "horsehair_box"),
    ("viols", 1, True, 22050, 1.28, 0.075, 0.24, 0.0035, 5.0, [43, 51, 59, 67, 75],
     [(False, 48, 0.68, 0), (False, 42, 0.38, 0)], "wood_warm"),
    ("strings_spiccato", 1, False, 24000, 0.42, 0.006, 0.09, 0.0020, 5.5, [43, 51, 59, 67, 75],
     [(False, 45, 0.55, 0), (False, 48, 0.60, 0)], "spiccato_gate"),
    ("contrabass_drone", 1, True, 18000, 1.35, 0.120, 0.32, 0.0020, 4.2, [31, 38, 45, 52],
     [(False, 43, 0.78, 0), (False, 42, 0.32, 12)], "dark_sub"),

    # --- Winds & Reeds (5) ---
    ("recorder", 2, True, 24000, 1.20, 0.030, 0.12, 0.0035, 5.2, [60, 66, 72, 78, 84],
     [(False, 74, 0.90, 0)], "wood_warm"),
    ("ney", 2, True, 24000, 1.25, 0.050, 0.16, 0.0055, 4.9, [55, 62, 69, 76, 83],
     [(False, 75, 0.62, 0), (False, 77, 0.52, 0)], "breath_chiff"),
    ("shawm", 2, True, 24000, 1.20, 0.022, 0.12, 0.0045, 5.8, [55, 62, 69, 76],
     [(False, 111, 0.68, 0), (False, 68, 0.42, 0)], "reed_bite"),
    ("low_whistle", 2, True, 22050, 1.22, 0.038, 0.15, 0.0042, 5.0, [53, 60, 67, 74],
     [(False, 73, 0.65, 0), (False, 75, 0.42, 0)], "wood_warm"),
    ("pipes_drone", 2, True, 22050, 1.25, 0.045, 0.16, 0.0025, 5.5, [43, 50, 57, 64, 71],
     [(False, 109, 0.75, 0), (False, 69, 0.30, 0)], "wood_warm"),

    # --- Brass & Voices (5) ---
    ("war_horn", 3, True, 22050, 1.28, 0.065, 0.24, 0.0035, 4.6, [38, 45, 52, 59, 66],
     [(False, 60, 0.85, 0), (False, 57, 0.28, 0)], "brass_growl"),
    ("french_horn", 3, True, 22050, 1.25, 0.055, 0.22, 0.0030, 4.8, [43, 50, 57, 64, 71],
     [(False, 60, 0.82, 0), (False, 61, 0.25, 0)], "wood_warm"),
    ("low_brass", 3, True, 20000, 1.25, 0.045, 0.20, 0.0025, 4.5, [34, 41, 48, 55],
     [(False, 58, 0.68, 0), (False, 57, 0.48, 0)], "brass_growl"),
    ("choir_aah", 3, True, 22050, 1.30, 0.110, 0.30, 0.0035, 4.7, [45, 53, 61, 69, 77],
     [(False, 52, 0.78, 0), (False, 53, 0.34, 0)], "choir_halo"),
    ("throat_drone", 3, True, 20000, 1.35, 0.090, 0.28, 0.0025, 4.0, [36, 41, 46, 51],
     [(False, 53, 0.70, 0), (False, 42, 0.25, 0)], "throat_overtone"),

    # --- Percussion & Bells (6) ---
    ("war_drum", 4, False, 22050, 0.82, 0.002, 0.18, 0.0, 0.0, [36, 41, 46, 52],
     [(False, 47, 0.72, 0), (True, 36, 0.70, 0), (False, 116, 0.45, 0)], "drum_punch"),
    ("frame_drum", 4, False, 24000, 0.55, 0.002, 0.12, 0.0, 0.0, [43, 50, 57, 64],
     [(False, 116, 0.78, 0), (True, 61, 0.35, 0)], "wood_warm"),
    ("darbuka", 4, False, 26000, 0.46, 0.001, 0.10, 0.0, 0.0, [48, 56, 64, 72],
     [(False, 117, 0.62, 0), (True, 62, 0.55, 0), (True, 60, 0.35, 0)], "wire_crisp"),
    ("snare_march", 4, False, 26000, 0.40, 0.001, 0.09, 0.0, 0.0, [52, 60, 67],
     [(True, 38, 0.82, 0), (True, 40, 0.42, 0)], "wire_crisp"),
    ("tambourine_riq", 4, False, 28000, 0.36, 0.001, 0.08, 0.0, 0.0, [56, 64, 72],
     [(True, 54, 0.82, 0), (True, 70, 0.30, 0)], "air_bright"),
    ("tubular_bell", 4, False, 28000, 1.32, 0.002, 0.35, 0.0012, 3.8, [52, 60, 67, 74],
     [(False, 14, 1.0, 0)], "air_bright"),
]


def find_sf2(explicit: str | None) -> str:
    if explicit and os.path.isfile(explicit):
        return explicit
    for p in SF2_CANDIDATES:
        if os.path.isfile(p):
            return p
    raise FileNotFoundError("Could not find FluidR3_GM.sf2; pass --sf2 <path>.")


def resample_fft(x: np.ndarray, src_sr: int, dst_sr: int) -> np.ndarray:
    if src_sr == dst_sr:
        return x.copy()
    dst_len = max(64, int(round(len(x) * dst_sr / src_sr)))
    spec = np.fft.rfft(x)
    new_bins = dst_len // 2 + 1
    out_spec = np.zeros(new_bins, dtype=np.complex128)
    copy_bins = min(len(spec), new_bins)
    out_spec[:copy_bins] = spec[:copy_bins]
    # Gentle cosine roll-off near Nyquist to avoid Gibbs ringing
    roll = max(4, copy_bins // 20)
    taper = 0.5 * (1.0 + np.cos(np.linspace(0, math.pi, roll)))
    out_spec[copy_bins - roll:copy_bins] *= taper
    y = np.fft.irfft(out_spec, n=dst_len) * (dst_len / len(x))
    return y.astype(np.float64)


def biquad_peaking(x: np.ndarray, sr: int, freq: float, q: float, gain_db: float) -> np.ndarray:
    if freq >= sr * 0.46:
        return x
    A = 10.0 ** (gain_db / 40.0)
    w0 = 2.0 * math.pi * freq / sr
    alpha = math.sin(w0) / (2.0 * q)
    b0 = 1.0 + alpha * A
    b1 = -2.0 * math.cos(w0)
    b2 = 1.0 - alpha * A
    a0 = 1.0 + alpha / A
    a1 = -2.0 * math.cos(w0)
    a2 = 1.0 - alpha / A
    b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0
    y = np.empty_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        xi = float(x[i])
        yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2 = x1; x1 = xi; y2 = y1; y1 = yi
        y[i] = yi
    return y


def sculpt_audio(x: np.ndarray, sr: int, mode: str, root_midi: int, rng: np.random.Generator) -> np.ndarray:
    t = np.arange(len(x), dtype=np.float64) / sr
    f0 = 440.0 * (2.0 ** ((root_midi - 69.0) / 12.0))
    if mode == "wood_warm":
        x = biquad_peaking(x, sr, 260.0, 1.2, 2.5)
        x = biquad_peaking(x, sr, 4200.0, 1.0, -2.0)
    elif mode == "oud_belly":
        x = biquad_peaking(x, sr, 195.0, 1.4, 4.0)
        x = biquad_peaking(x, sr, 3400.0, 0.9, -3.5)
    elif mode == "wire_crisp":
        x = biquad_peaking(x, sr, 2800.0, 1.3, 3.0)
    elif mode == "air_bright":
        x = biquad_peaking(x, sr, 5200.0, 0.9, 2.5)
    elif mode == "bow_rosin":
        x = biquad_peaking(x, sr, 950.0, 1.4, 2.0)
        x = biquad_peaking(x, sr, 2900.0, 1.6, 2.2)
    elif mode == "sympathetic":
        x = biquad_peaking(x, sr, 720.0, 1.5, 2.5)
        fifth = f0 * 1.5
        if fifth < sr * 0.42:
            x = x + 0.045 * np.sin(2.0 * math.pi * fifth * t) * np.minimum(1.0, t * 5.0)
    elif mode == "horsehair_box":
        x = biquad_peaking(x, sr, 360.0, 2.0, 4.5)
        x = biquad_peaking(x, sr, 920.0, 1.8, 2.5)
        x = biquad_peaking(x, sr, 3800.0, 1.0, -3.0)
    elif mode == "spiccato_gate":
        env = np.exp(-np.maximum(0.0, t - 0.05) * 9.5)
        x = x * env
    elif mode == "dark_sub":
        x = x + 0.14 * np.sin(2.0 * math.pi * f0 * t) * np.minimum(1.0, t * 4.0)
        x = biquad_peaking(x, sr, 120.0, 1.1, 3.0)
    elif mode == "breath_chiff":
        chiff = rng.standard_normal(len(x)) * np.exp(-t * 28.0) * 0.08
        chiff = biquad_peaking(chiff, sr, min(sr * 0.38, f0 * 3.0), 3.0, 8.0)
        x = x + chiff
    elif mode == "reed_bite":
        x = np.tanh(x * 1.45) * 0.82
        x = biquad_peaking(x, sr, 1450.0, 1.6, 3.5)
    elif mode == "brass_growl":
        x = np.tanh(x * 1.35) * 0.85
        x = biquad_peaking(x, sr, 320.0, 1.2, 2.8)
    elif mode == "choir_halo":
        x = biquad_peaking(x, sr, 780.0, 1.5, 2.2)
        x = biquad_peaking(x, sr, 2600.0, 1.2, 1.8)
    elif mode == "throat_overtone":
        # Phase-locked harmonic overtones (6th, 8th, 10th) locked to f0 so loop period matches f0
        over = np.zeros_like(x)
        for h, amp in [(1, 0.22), (2, 0.15), (6, 0.11), (8, 0.09), (10, 0.05)]:
            if f0 * h < sr * 0.42:
                over += amp * np.sin(2.0 * math.pi * (f0 * h) * t)
        x = x * 0.72 + over * np.minimum(1.0, t * 6.0)
    elif mode == "drum_punch":
        sub = 0.28 * np.sin(2.0 * math.pi * (58.0 * np.exp(-t * 5.0) + 36.0) * t) * np.exp(-t * 6.5)
        x = x + sub
    return x


def make_loop(x: np.ndarray, sr: int, root_midi: int) -> tuple[np.ndarray, int, int]:
    """Find phase-aligned zero-crossing loop points and crossfade the seam cleanly."""
    n = len(x)
    start_target = int(0.36 * sr)
    end_target = int(min(n - 0.14 * sr, 1.02 * sr))
    fade_n = max(96, int(0.032 * sr))
    if end_target - start_target < fade_n * 3:
        start_target = int(0.22 * sr)
        end_target = int(n * 0.78)
        fade_n = max(64, (end_target - start_target) // 4)

    # Find upward zero crossing near start_target
    s0 = start_target
    for i in range(start_target, min(n - 2, start_target + sr // 15)):
        if x[i] <= 0.0 and x[i + 1] > 0.0:
            s0 = i
            break

    win = min(512, fade_n)
    ref = x[s0:s0 + win]
    ref_norm = float(np.linalg.norm(ref)) + 1e-9

    f0 = 440.0 * (2.0 ** ((root_midi - 69.0) / 12.0))
    search_span = max(int(sr / max(30.0, f0) * 6), int(0.08 * sr))
    lo = max(s0 + fade_n * 2, end_target - search_span)
    hi = min(n - fade_n - win - 2, end_target + search_span)

    best_e = end_target
    best_score = -2.0
    for i in range(lo, hi):
        if x[i] <= 0.0 and x[i + 1] > 0.0:
            cand = x[i:i + win]
            score = float(np.dot(ref, cand)) / (ref_norm * (float(np.linalg.norm(cand)) + 1e-9))
            if score > best_score:
                best_score = score
                best_e = i

    out = x[:best_e].copy()
    # Crossfade post-loop tail x[best_e : best_e + fade_n] into out[s0 : s0 + fade_n]
    # At k=0: alpha=0 -> out[s0] == x[best_e], matching out[best_e - 1] -> x[best_e] seamlessly!
    alpha = np.linspace(0.0, 1.0, fade_n, endpoint=True)
    tail = x[best_e:best_e + fade_n]
    out[s0:s0 + fade_n] = out[s0:s0 + fade_n] * alpha + tail * (1.0 - alpha)
    return out, s0, best_e


def build(sf2_path: str, out_path: str) -> None:
    slots: list[tuple[int, int]] = []
    for vi, v in enumerate(VOICES):
        for root in v[9]:
            slots.append((vi, root))

    # Build a single 120 BPM MIDI file (1 beat = 0.5s, 480 ticks/beat => 960 ticks/sec)
    ticks_per_beat = 480
    tps = 960
    slot_sec = 2.5
    note_hold_sec = 1.95

    mid = mido.MidiFile(ticks_per_beat=ticks_per_beat)
    trk = mido.MidiTrack()
    mid.tracks.append(trk)
    trk.append(mido.MetaMessage("set_tempo", tempo=500000, time=0))

    events: list[tuple[int, mido.Message]] = []
    for idx, (vi, root) in enumerate(slots):
        v = VOICES[vi]
        t0 = int(round(idx * slot_sec * tps))
        t_off = int(round((idx * slot_sec + note_hold_sec) * tps))
        t_cut = int(round(((idx + 1) * slot_sec - 0.05) * tps))

        mel_ch = 0
        for is_drum, prog_or_note, gain, transpose in v[10]:
            vel = max(20, min(127, int(round(108 * min(1.0, gain)))))
            if is_drum:
                ch = 9
                note = prog_or_note
                # Slightly vary drum velocity/note if pitched drum zone
                events.append((t0, mido.Message("control_change", channel=ch, control=7, value=120, time=0)))
                events.append((t0 + 1, mido.Message("note_on", channel=ch, note=note, velocity=vel, time=0)))
                events.append((t_off, mido.Message("note_off", channel=ch, note=note, velocity=0, time=0)))
            else:
                ch = mel_ch
                mel_ch += 1
                if mel_ch == 9:
                    mel_ch = 10
                note = max(12, min(108, root + transpose))
                vol = max(16, min(127, int(round(112 * gain))))
                events.append((t0, mido.Message("program_change", channel=ch, program=prog_or_note, time=0)))
                events.append((t0, mido.Message("control_change", channel=ch, control=7, value=vol, time=0)))
                events.append((t0, mido.Message("control_change", channel=ch, control=10, value=64, time=0)))
                events.append((t0 + 2, mido.Message("note_on", channel=ch, note=note, velocity=104, time=0)))
                events.append((t_off, mido.Message("note_off", channel=ch, note=note, velocity=0, time=0)))
        for ch_clear in [0, 1, 2, 9]:
            events.append((t_cut, mido.Message("control_change", channel=ch_clear, control=120, value=0, time=0)))
            events.append((t_cut, mido.Message("control_change", channel=ch_clear, control=123, value=0, time=0)))

    events.sort(key=lambda e: e[0])
    last_tick = 0
    for tick, msg in events:
        trk.append(msg.copy(time=max(0, tick - last_tick)))
        last_tick = tick
    trk.append(mido.MetaMessage("end_of_track", time=tps))

    with tempfile.TemporaryDirectory() as tmp:
        mid_path = os.path.join(tmp, "bank.mid")
        wav_path = os.path.join(tmp, "bank.wav")
        mid.save(mid_path)
        subprocess.run(
            ["fluidsynth", "-ni", "-C", "0", "-R", "0", "-g", "0.85", "-r", "44100", "-F", wav_path, sf2_path, mid_path],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        with wave.open(wav_path, "rb") as wf:
            sr = wf.getframerate()
            nch = wf.getnchannels()
            frames = wf.readframes(wf.getnframes())
        pcm = np.frombuffer(frames, dtype=np.int16).astype(np.float64) / 32768.0
        if nch == 2:
            pcm = 0.5 * (pcm[0::2] + pcm[1::2])

    rng = np.random.default_rng(20261111)
    by_voice: dict[int, list[tuple[int, int, int, int, int, int, np.ndarray]]] = {i: [] for i in range(len(VOICES))}

    for idx, (vi, root) in enumerate(slots):
        v = VOICES[vi]
        _, cat, looped, dst_sr, dur_sec, _, _, _, _, roots, _, sculpt = v
        s_start = int(round(idx * slot_sec * sr))
        s_end = min(len(pcm), s_start + int(round((slot_sec - 0.08) * sr)))
        seg = pcm[s_start:s_end].copy()

        # Trim leading silence while keeping 1.5 ms pre-attack
        above = np.flatnonzero(np.abs(seg) > 0.0025)
        if len(above) > 0:
            trim = max(0, int(above[0]) - int(0.0015 * sr))
            seg = seg[trim:]

        want_src = min(len(seg), int(round((dur_sec + (0.24 if looped else 0.02)) * sr)))
        seg = seg[:want_src]
        # For unpitched Ch 9 percussion voices (snare_march, tambourine_riq), pitch slightly by root around MIDI 60
        eff_dst_sr = dst_sr
        if cat == 4 and all(is_d for is_d, _, _, _ in v[10]):
            ratio = 2.0 ** ((root - 60.0) / 24.0)
            eff_dst_sr = max(12000, int(round(dst_sr / ratio)))
        seg = resample_fft(seg, sr, eff_dst_sr)
        seg = sculpt_audio(seg, dst_sr, sculpt, root, rng)

        # Remove DC offset
        seg -= float(np.mean(seg))

        if looped:
            seg, loop_start, loop_end = make_loop(seg, dst_sr, root)
            # Tiny 2 ms fade-in at index 0 to guarantee zero onset click
            fi = min(len(seg), max(16, int(0.002 * dst_sr)))
            seg[:fi] *= np.linspace(0.0, 1.0, fi)
        else:
            loop_start, loop_end = 0, 0
            fi = min(len(seg), max(8, int(0.001 * dst_sr)))
            seg[:fi] *= np.linspace(0.0, 1.0, fi)
            fo = min(len(seg) // 3, max(64, int(0.035 * dst_sr)))
            seg[-fo:] *= 0.5 * (1.0 + np.cos(np.linspace(0.0, math.pi, fo)))

        peak = float(np.max(np.abs(seg)))
        if peak > 1e-6:
            seg = seg * (0.88 / peak)

        # Quantize to 14-bit dynamic range (~84 dB SNR, completely transparent) so quiet tail noise compresses tightly
        q16 = (np.clip(np.round(seg * 8191.0), -8191, 8191).astype(np.int16) * 4).astype(np.int16)
        ri = roots.index(root)
        low_midi = 0 if ri == 0 else (roots[ri - 1] + root) // 2 + 1
        high_midi = 127 if ri == len(roots) - 1 else (root + roots[ri + 1]) // 2
        by_voice[vi].append((root, low_midi, high_midi, dst_sr, loop_start, loop_end, q16))

    buf = io.BytesIO()
    buf.write(b"IFBK")
    buf.write(struct.pack("<HH", 1, len(VOICES)))
    total_samples = 0
    for vi, v in enumerate(VOICES):
        vid, cat, looped, _, _, atk, rel, vib_d, vib_r, _, _, _ = v
        id_bytes = vid.encode("ascii")
        zones = by_voice[vi]
        buf.write(struct.pack("<B", len(id_bytes)))
        buf.write(id_bytes)
        buf.write(struct.pack("<BffffB", cat, atk, rel, vib_d, vib_r, len(zones)))
        for root, lo_m, hi_m, z_sr, ls, le, q16 in zones:
            total_samples += len(q16)
            delta = np.empty_like(q16)
            delta[0] = q16[0]
            # Wrapping int16 delta, stored byte-plane split (lo bytes then hi bytes) for tight GZip compression
            delta[1:] = (q16[1:].astype(np.int32) - q16[:-1].astype(np.int32)).astype(np.int16)
            raw_u16 = delta.view(np.uint8)
            buf.write(struct.pack("<BBBHiii", root, lo_m, hi_m, z_sr, ls, le, len(q16)))
            buf.write(raw_u16[0::2].tobytes())
            buf.write(raw_u16[1::2].tobytes())

    raw_bytes = buf.getvalue()
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    compressed = gzip.compress(raw_bytes, compresslevel=9, mtime=0)
    with open(out_path, "wb") as f:
        f.write(compressed)
    print(f"Built {out_path}: {len(VOICES)} voices, {len(slots)} zones, "
          f"{total_samples * 2 / 1048576:.2f} MB raw -> {len(compressed) / 1048576:.2f} MB gzip")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--sf2", default=None)
    ap.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "..", "Data", "audio", "instruments.bank"))
    args = ap.parse_args()
    build(find_sf2(args.sf2), os.path.abspath(args.out))
