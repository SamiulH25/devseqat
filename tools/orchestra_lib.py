#!/usr/bin/env python3
"""
Musical composition & orchestration helpers for Ironfield's VSCO 2 CE orchestral soundtrack.
Provides modal scales, voice-led chord progressions, expressive melodic phrasing, cultural
instrument palettes, orchestral accompaniment patterns, and phrase-level expression curves.
"""
from __future__ import annotations

import os
import random
from dataclasses import dataclass
from typing import Optional

from sfz_engine import NoteEvent, TrackSpec

VSCO_DIR = os.path.expanduser("~/.cache/ironfield-audio/VSCO-2-CE")


def sfz(name: str) -> str:
    p = os.path.join(VSCO_DIR, name)
    if not os.path.isfile(p):
        raise FileNotFoundError(f"Missing VSCO SFZ: {p}")
    return p


SCALES: dict[str, list[int]] = {
    "dorian":     [0, 2, 3, 5, 7, 9, 10],
    "aeolian":    [0, 2, 3, 5, 7, 8, 10],
    "mixolydian": [0, 2, 4, 5, 7, 9, 10],
    "ionian":     [0, 2, 4, 5, 7, 9, 11],
    "lydian":     [0, 2, 4, 6, 7, 9, 11],
    "phrygian":   [0, 1, 3, 5, 7, 8, 10],
    "hijaz":      [0, 1, 4, 5, 7, 8, 10],
    "harmonic":   [0, 2, 3, 5, 7, 8, 11],
    "locrian":    [0, 1, 3, 5, 6, 8, 10],
    "penta_min":  [0, 3, 5, 7, 10],
}


def deg_midi(root: int, scale_name: str, deg: int) -> int:
    sc = SCALES[scale_name]
    octv, idx = divmod(deg, len(sc))
    return max(24, min(96, root + sc[idx] + 12 * octv))


def chord_voicing(root: int, scale_name: str, deg: int, center_midi: int = 56, spread: int = 3) -> tuple[int, list[int]]:
    """
    Build a smooth, voice-led triad/tetrad around `center_midi` for scale degree `deg`.
    Returns `(bass_midi, [voiced_midis_sorted])`.
    """
    sc_len = len(SCALES[scale_name])
    bass = deg_midi(root - 12, scale_name, deg)
    while bass < 33:
        bass += 12
    while bass > 47:
        bass -= 12

    # Pick 1st, 3rd, 5th (and optional 7th/octave) in scale degrees
    offsets = [0, 2, 4] if spread == 3 else [0, 2, 4, 6]
    if sc_len == 5:  # pentatonic: open 4th/5th/octave voicings
        offsets = [0, 2, 3] if spread == 3 else [0, 2, 3, 5]

    raw = [deg_midi(root, scale_name, deg + o) for o in offsets]
    voiced = []
    for m in raw:
        while m < center_midi - 7:
            m += 12
        while m > center_midi + 9:
            m -= 12
        voiced.append(m)
    voiced.sort()
    # Ensure distinct pitches
    for i in range(1, len(voiced)):
        if voiced[i] <= voiced[i - 1]:
            voiced[i] = voiced[i - 1] + 12
    return bass, voiced


def make_melody(
    root: int,
    scale_name: str,
    prog_degs: list[int],
    bars: int,
    bpb: int,
    seed: int,
    style: str = "lyrical",
    low_deg: int = -1,
    high_deg: int = 9,
    vel_base: float = 0.76,
) -> list[NoteEvent]:
    """
    Compose a structured A - A' - B - A'' melody with real rests, motif repetition,
    chord-tone landing on downbeats, and expressive velocity phrasing.
    """
    rng = random.Random(seed)
    sc_len = len(SCALES[scale_name])

    # Rhythm vocabularies per bar (sum == bpb)
    if bpb == 3:
        rhythms = {
            "lyrical": [[1.5, 0.5, 1.0], [2.0, 1.0], [1.0, 1.0, 1.0], [2.5, 0.5]],
            "dance":   [[1.0, 0.5, 0.5, 1.0], [0.5, 0.5, 1.0, 1.0], [1.5, 0.5, 0.5, 0.5]],
            "martial": [[1.5, 0.5, 1.0], [1.0, 0.5, 0.5, 1.0], [2.0, 1.0]],
            "sparse":  [[2.0, 1.0], [3.0], [1.5, 1.5]],
        }
    else:
        rhythms = {
            "lyrical": [
                [2.0, 1.0, 1.0], [1.5, 0.5, 2.0], [1.5, 0.5, 1.0, 1.0],
                [2.5, 0.5, 1.0], [1.0, 1.0, 2.0], [3.0, 1.0],
            ],
            "martial": [
                [1.5, 0.5, 1.0, 1.0], [1.0, 0.5, 0.5, 2.0], [2.0, 1.0, 1.0],
                [0.5, 0.5, 1.0, 2.0], [1.5, 0.5, 1.5, 0.5],
            ],
            "dance": [
                [0.5, 0.5, 1.0, 0.5, 0.5, 1.0], [1.0, 0.5, 0.5, 1.0, 1.0],
                [0.5, 0.5, 0.5, 0.5, 1.0, 1.0], [1.5, 0.5, 0.5, 0.5, 1.0],
            ],
            "sparse": [
                [3.0, 1.0], [2.0, 2.0], [2.5, 1.5], [4.0],
            ],
        }
    pool = rhythms.get(style, rhythms["lyrical"])

    # Create a 4-bar A motif (list of (bar_in_phrase, beat_off, deg_rel, dur))
    def gen_4bar(start_deg: int, lift: int = 0) -> list[tuple[int, float, int, float]]:
        motif: list[tuple[int, float, int, float]] = []
        cur = start_deg + lift
        for bi in range(4):
            if bi == 3:
                # Phrase cadence bar: held note + breath rest
                hold = float(bpb - 1.0) if style != "dance" else float(bpb - 0.5)
                motif.append((bi, 0.0, cur, hold))
                continue
            rh = rng.choice(pool)
            off = 0.0
            for dur in rh:
                step = rng.choices([-2, -1, 0, 1, 2, 3, -3], weights=[12, 26, 10, 28, 14, 6, 4])[0]
                cur = max(low_deg + lift, min(high_deg + lift, cur + step))
                motif.append((bi, off, cur, dur))
                off += dur
        return motif

    motif_a = gen_4bar(rng.choice([0, 2, 4]), lift=0)
    motif_b = gen_4bar(rng.choice([4, 5, 6]), lift=2)

    notes: list[NoteEvent] = []
    n_phrases = max(1, bars // 4)
    for pi in range(n_phrases):
        bar0 = pi * 4
        if bar0 >= bars:
            break
        # Leave intro breathing room on slow/lyrical pieces when bars >= 16
        if pi == 0 and bars >= 16 and style == "sparse":
            continue
        is_b = (pi == n_phrases // 2) or (n_phrases >= 5 and pi == 2)
        src = motif_b if is_b else motif_a
        phrase_vel = vel_base * (1.10 if is_b else (0.92 if pi == n_phrases - 1 else 1.0))

        for bi, off, deg, dur in src:
            abs_bar = bar0 + bi
            if abs_bar >= bars:
                continue
            c_deg = prog_degs[abs_bar % len(prog_degs)]
            # Anchor strong downbeats onto chord tones for clean harmony
            if off == 0.0:
                chord_tones = [c_deg, c_deg + 2, c_deg + 4, c_deg + sc_len, c_deg + sc_len + 2]
                deg = min(chord_tones, key=lambda ct: abs(ct - deg))
            # Final cadence resolves home to tonic
            if pi == n_phrases - 1 and bi == 3:
                deg = 0 if deg <= 3 else sc_len

            midi = deg_midi(root, scale_name, deg)
            # Expressive phrasing: arch velocity across the 4-bar phrase
            arch = 0.90 + 0.14 * (1.0 - abs((bi + off / bpb) - 2.0) / 2.2)
            v = max(0.35, min(0.95, phrase_vel * arch * (1.04 if off == 0.0 else 0.94)))
            leg = 0.96 if style in ("lyrical", "sparse") else (0.86 if style == "dance" else 0.92)
            notes.append(NoteEvent(abs_bar * bpb + off, midi, dur * leg, v))

    return notes


def phrase_expression(total_beats: float, climax_frac: float = 0.58, lo: float = 0.56, hi: float = 0.96) -> list[tuple[float, float]]:
    """Generate a smooth phrase-breathing CC11 expression curve across a piece."""
    pts: list[tuple[float, float]] = []
    steps = max(8, int(total_beats // 8))
    for i in range(steps + 1):
        frac = i / steps
        b = frac * total_beats
        # Global arch peaking at climax_frac + local 2-bar breathing
        dist = abs(frac - climax_frac)
        arch = hi - (hi - lo) * min(1.0, dist / max(0.35, climax_frac))
        breath = 0.06 * (1.0 if i % 2 == 1 else -1.0)
        pts.append((b, max(0.38, min(1.0, arch + breath))))
    return pts


def cadence_rubato(total_beats: float, bpb: int, amount: float = 0.05) -> list[tuple[float, float]]:
    """Subtle expressive ritardando at major 8-bar cadences."""
    pts: list[tuple[float, float]] = [(0.0, 1.0)]
    step = bpb * 8
    b = float(step)
    while b <= total_beats:
        pts.append((b - bpb, 1.0))
        pts.append((b - 0.5, 1.0 - amount))
        pts.append((b, 1.0))
        b += step
    if pts[-1][0] < total_beats:
        pts.append((total_beats, 1.0))
    return pts


def harp_arpeggios(root: int, scale_name: str, prog: list[int], bars: int, bpb: int, vel: float = 0.64, style: str = "flowing") -> list[NoteEvent]:
    notes: list[NoteEvent] = []
    for bar in range(bars):
        _, v = chord_voicing(root, scale_name, prog[bar % len(prog)], center_midi=58, spread=4)
        b = bar * bpb
        if bpb == 3:
            pat = [(0.0, v[0], 1.4, 1.0), (0.5, v[1], 1.1, 0.8), (1.0, v[2], 1.2, 0.88),
                   (1.5, v[3], 1.1, 0.84), (2.0, v[2], 1.0, 0.82), (2.5, v[1], 0.8, 0.74)]
        elif style == "sparse":
            pat = [(0.0, v[0], 2.2, 1.0), (0.12, v[1], 2.0, 0.84), (0.24, v[2], 1.8, 0.88), (2.0, v[3], 1.8, 0.78)]
        else:
            pat = [(0.0, v[0], 1.4, 1.0), (0.5, v[1], 1.1, 0.80), (1.0, v[2], 1.2, 0.88), (1.5, v[1], 0.9, 0.76),
                   (2.0, v[3], 1.3, 0.92), (2.5, v[2], 1.0, 0.78), (3.0, v[1], 1.0, 0.82), (3.5, v[2], 0.8, 0.74)]
            if bar % 4 == 3:
                pat = [(0.0, v[0], 1.8, 0.96), (0.5, v[1], 1.4, 0.80), (1.0, v[2], 1.4, 0.86), (2.0, v[3], 1.8, 0.90)]
        for off, m, d, vm in pat:
            if off < bpb:
                notes.append(NoteEvent(b + off, m, d, min(0.92, vel * vm)))
    return notes


def string_pads(root: int, scale_name: str, prog: list[int], bars: int, bpb: int, voice_idx: int,
                center: int = 56, vel: float = 0.64, start_bar: int = 0, end_bar: Optional[int] = None,
                attack: float = 0.14, release: float = 0.42) -> list[NoteEvent]:
    notes: list[NoteEvent] = []
    eb = end_bar if end_bar is not None else bars
    for bar in range(start_bar, min(bars, eb)):
        bass, v = chord_voicing(root, scale_name, prog[bar % len(prog)], center_midi=center, spread=3)
        midi = bass if voice_idx < 0 else v[min(voice_idx, len(v) - 1)]
        notes.append(NoteEvent(bar * bpb, midi, bpb - 0.08, vel, attack=attack, release=release))
    return notes


def spiccato_ostinato(root: int, scale_name: str, prog: list[int], bars: int, bpb: int,
                      octave_shift: int = 0, vel: float = 0.74, pattern: str = "drive") -> list[NoteEvent]:
    notes: list[NoteEvent] = []
    for bar in range(bars):
        _, v = chord_voicing(root, scale_name, prog[bar % len(prog)], center_midi=56 + octave_shift * 12, spread=3)
        b = bar * bpb
        if pattern == "gallop":
            pat = [(0.0, 0, 0.35, 1.0), (0.5, 0, 0.22, 0.72), (0.75, 1, 0.22, 0.78),
                   (1.0, 2, 0.35, 0.90), (1.5, 1, 0.22, 0.72), (1.75, 0, 0.22, 0.76),
                   (2.0, 0, 0.35, 0.96), (2.5, 1, 0.22, 0.74), (2.75, 2, 0.22, 0.80),
                   (3.0, 1, 0.35, 0.88), (3.5, 0, 0.28, 0.76)]
        elif pattern == "march":
            pat = [(0.0, 0, 0.45, 1.0), (1.0, 1, 0.32, 0.80), (1.5, 1, 0.28, 0.74),
                   (2.0, 2, 0.45, 0.92), (3.0, 1, 0.32, 0.82), (3.5, 0, 0.28, 0.76)]
        else:  # drive
            pat = [(0.0, 0, 0.32, 1.0), (0.5, 0, 0.26, 0.72), (1.0, 1, 0.32, 0.86), (1.5, 0, 0.26, 0.72),
                   (2.0, 2, 0.32, 0.94), (2.5, 1, 0.26, 0.74), (3.0, 0, 0.32, 0.86), (3.5, 1, 0.26, 0.78)]
            if bar % 4 == 3:
                pat = [(0.0, 0, 0.32, 1.0), (0.5, 1, 0.28, 0.82), (1.0, 2, 0.32, 0.92), (1.5, 1, 0.28, 0.80),
                       (2.0, 0, 0.32, 0.96), (2.5, 1, 0.28, 0.86), (3.0, 2, 0.35, 0.98), (3.5, 2, 0.30, 0.90)]
        for off, vi, dur, vm in pat:
            if off < bpb:
                notes.append(NoteEvent(b + off, v[vi], dur, min(0.95, vel * vm)))
    return notes


def battle_percussion(prog: list[int], root: int, scale_name: str, bars: int, bpb: int,
                      style: str = "march", vel: float = 0.76) -> tuple[list[NoteEvent], list[NoteEvent], list[NoteEvent]]:
    """Returns (timpani_notes, timpani_rolls, gm_perc_notes)."""
    timp, rolls, gm = [], [], []
    for bar in range(bars):
        bass, _ = chord_voicing(root, scale_name, prog[bar % len(prog)], center_midi=48, spread=3)
        t_midi = max(36, min(48, bass))
        b = bar * bpb
        if style == "gallop":
            timp += [NoteEvent(b, t_midi, 0.5, vel), NoteEvent(b + 0.75, t_midi + 5, 0.35, vel * 0.72),
                     NoteEvent(b + 2.0, t_midi, 0.5, vel * 0.92), NoteEvent(b + 2.75, t_midi + 5, 0.35, vel * 0.75)]
            gm += [NoteEvent(b, 36, 0.25, vel * 0.86), NoteEvent(b + 1.0, 38, 0.2, vel * 0.64),
                   NoteEvent(b + 2.0, 36, 0.25, vel * 0.80), NoteEvent(b + 3.0, 38, 0.2, vel * 0.70)]
        elif style == "heavy":
            timp += [NoteEvent(b, t_midi, 0.9, vel), NoteEvent(b + 2.0, t_midi, 0.8, vel * 0.90)]
            gm += [NoteEvent(b, 36, 0.3, vel * 0.95), NoteEvent(b + 2.0, 36, 0.3, vel * 0.85),
                   NoteEvent(b + 3.0, 38, 0.25, vel * 0.68)]
        else:
            timp += [NoteEvent(b, t_midi, 0.7, vel), NoteEvent(b + 1.5, t_midi + 5, 0.4, vel * 0.70),
                     NoteEvent(b + 2.0, t_midi, 0.6, vel * 0.88)]
            gm += [NoteEvent(b, 36, 0.25, vel * 0.84), NoteEvent(b + 1.0, 38, 0.2, vel * 0.66),
                   NoteEvent(b + 2.0, 36, 0.25, vel * 0.78), NoteEvent(b + 3.0, 38, 0.2, vel * 0.72)]
        if bar % 4 == 3:
            rolls.append(NoteEvent(b + max(1.0, bpb - 2.0), t_midi, 1.9, vel * 0.90, attack=0.35, release=0.35))
            for off, vm in [(3.0, 0.74), (3.25, 0.66), (3.5, 0.80), (3.75, 0.86)]:
                if off < bpb:
                    gm.append(NoteEvent(b + off, 38, 0.18, vel * vm))
    return timp, rolls, gm


@dataclass
class CulturePalette:
    name: str
    root: int
    scale: str
    lead_day: str
    lead_night: str
    lead_battle: str
    accompany: str
    brass: str
    low: str
    battle_pattern: str = "drive"
    perc_style: str = "march"


CULTURES: dict[str, CulturePalette] = {
    "azure": CulturePalette(
        "azure", 62, "dorian",
        lead_day="FHornSus.sfz", lead_night="FluteSusVib.sfz", lead_battle="FHornSus.sfz",
        accompany="Harp.sfz", brass="TromboneSus.sfz", low="ContrabassSusVB.sfz",
        battle_pattern="drive", perc_style="march",
    ),
    "crimson": CulturePalette(
        "crimson", 60, "aeolian",
        lead_day="TrumpetSus.sfz", lead_night="CelloEnsSusVib.sfz", lead_battle="TromboneSus.sfz",
        accompany="ViolaEnsSpic.sfz", brass="TrumpetSus.sfz", low="TubaSus.sfz",
        battle_pattern="march", perc_style="heavy",
    ),
    "north": CulturePalette(
        "north", 57, "aeolian",
        lead_day="SViolinVib.sfz", lead_night="BassoonSus.sfz", lead_battle="FHornSus.sfz",
        accompany="CelloEnsSusVib.sfz", brass="TromboneSus.sfz", low="ContrabassSusVB.sfz",
        battle_pattern="drive", perc_style="heavy",
    ),
    "sunward": CulturePalette(
        "sunward", 62, "hijaz",
        lead_day="OboeSusVib.sfz", lead_night="FluteExpVib.sfz", lead_battle="OboeSusVib.sfz",
        accompany="Harp.sfz", brass="FHornSus.sfz", low="CelloEnsPizz.sfz",
        battle_pattern="gallop", perc_style="gallop",
    ),
    "steppe": CulturePalette(
        "steppe", 62, "penta_min",
        lead_day="SViolinVib.sfz", lead_night="FluteSusNV.sfz", lead_battle="SViolinVib.sfz",
        accompany="CelloEnsSpic.sfz", brass="FHornSus.sfz", low="ContrabassSusVB.sfz",
        battle_pattern="gallop", perc_style="gallop",
    ),
    "league": CulturePalette(
        "league", 67, "mixolydian",
        lead_day="ClarinetSus.sfz", lead_night="FluteSusVib.sfz", lead_battle="TrumpetStac.sfz",
        accompany="Harp.sfz", brass="FHornStac.sfz", low="CelloEnsPizz.sfz",
        battle_pattern="march", perc_style="march",
    ),
    "order": CulturePalette(
        "order", 62, "phrygian",
        lead_day="FHornSus.sfz", lead_night="OrganQuiet.sfz", lead_battle="TromboneSus.sfz",
        accompany="OrganQuiet.sfz", brass="OrganLoud.sfz", low="OrganLoudPedal.sfz",
        battle_pattern="march", perc_style="heavy",
    ),
}
