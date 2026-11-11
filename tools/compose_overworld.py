#!/usr/bin/env python3
"""
Deterministic score compiler for AU4 (40 hand-authored overworld, cultural, settlement and season/dread pieces).
Each piece specifies its own composed melody phrases (A, A', B, Coda), modal scale, chord progression,
contrary/harmonic counterline, bassline and cultural percussion rhythm—zero random-walk generation.
"""
import json
import os

MODES = {
    "dorian":     [0, 2, 3, 5, 7, 9, 10],
    "aeolian":    [0, 2, 3, 5, 7, 8, 10],
    "mixolydian": [0, 2, 4, 5, 7, 9, 10],
    "ionian":     [0, 2, 4, 5, 7, 9, 11],
    "lydian":     [0, 2, 4, 6, 7, 9, 11],
    "phrygian":   [0, 1, 3, 5, 7, 8, 10],
    "hijaz":      [0, 1, 4, 5, 7, 8, 10],
    "nikriz":     [0, 2, 3, 6, 7, 9, 10],
    "locrian":    [0, 1, 3, 5, 6, 8, 10],
    "hollow":     [0, 1, 3, 6, 7, 8, 11],
    "penta_min":  [0, 3, 5, 7, 10],
    "penta_maj":  [0, 2, 4, 7, 9],
}


def deg_to_midi(root: int, scale: list[int], deg: int) -> int:
    n = len(scale)
    octv, idx = divmod(deg, n)
    return root + scale[idx] + 12 * octv


def parse_phrase(root: int, scale: list[int], start_beat: float, tokens: str, vel_scale: float = 1.0) -> list[list]:
    """
    Tokens like '0:1.5m 2:0.5 4:2w R:1 -1:1g' where:
      deg:dur[orn]  or  R:dur (rest).
      ornaments: g=grace, m=mordent, s=slide, r=roll, w=swell
    """
    ORN = {"g": "grace", "m": "mordent", "s": "slide", "r": "roll", "w": "swell"}
    out = []
    beat = start_beat
    for raw in tokens.replace("|", " ").split():
        head, dur_s = raw.split(":")
        orn = None
        if dur_s[-1].isalpha():
            orn = ORN[dur_s[-1]]
            dur = float(dur_s[:-1])
        else:
            dur = float(dur_s)
        if head != "R":
            deg = int(head)
            midi = deg_to_midi(root, scale, deg)
            # Phrase-arch dynamics: slightly stronger on longer/higher notes
            vel = min(0.95, max(0.42, (0.72 + 0.04 * min(dur, 3.0) + 0.01 * max(-3, min(deg, 8))) * vel_scale))
            item = [round(beat, 3), midi, round(dur * 0.94, 3), round(vel, 3)]
            if orn:
                item.append(orn)
            out.append(item)
        beat += dur
    return out


def build_counter(root: int, scale: list[int], bpb: int, chords: list[int], start_bar: int, bars: int, interval: int = 2, vel: float = 0.64) -> list[list]:
    notes = []
    for b in range(bars):
        bar = start_bar + b
        c = chords[b % len(chords)]
        beat = bar * bpb
        m1 = deg_to_midi(root, scale, c + interval)
        m2 = deg_to_midi(root, scale, c + interval + 2)
        if bpb == 4:
            notes.append([round(beat, 3), m1, 2.2, vel, "swell"])
            notes.append([round(beat + 2.0, 3), m2, 1.8, round(vel * 0.92, 3)])
        else:
            notes.append([round(beat, 3), m1, round(bpb * 0.92, 3), vel, "swell"])
    return notes


def build_arp(root: int, scale: list[int], bpb: int, chords: list[int], total_bars: int, pattern: list[tuple[float, int, float, float]]) -> list[list]:
    notes = []
    for bar in range(total_bars):
        c = chords[bar % len(chords)]
        base = bar * bpb
        for off, deg_off, dur, vel in pattern:
            if off < bpb:
                notes.append([round(base + off, 3), deg_to_midi(root, scale, c + deg_off), dur, vel])
    return notes


def build_drums(bpb: int, total_bars: int, hits: list[tuple[float, int, float, float]], start_bar: int = 0, end_bar: int | None = None) -> list[list]:
    end_b = total_bars if end_bar is None else end_bar
    notes = []
    for bar in range(start_bar, end_b):
        base = bar * bpb
        for off, midi, dur, vel in hits:
            if off < bpb:
                notes.append([round(base + off, 3), midi, dur, vel])
    return notes


ARP_4_WALK = [(0.0, 0, 1.4, 0.78), (1.0, 4, 1.0, 0.62), (2.0, 7, 1.2, 0.70), (3.0, 4, 0.9, 0.58)]
ARP_4_LUTE = [(0.0, 0, 1.2, 0.78), (0.5, 4, 0.9, 0.58), (1.0, 7, 1.0, 0.68), (1.5, 4, 0.8, 0.55),
              (2.0, 2, 1.2, 0.72), (2.5, 4, 0.8, 0.56), (3.0, 6, 1.0, 0.66), (3.5, 4, 0.8, 0.56)]
ARP_3_WALTZ = [(0.0, 0, 1.5, 0.80), (1.0, 4, 1.0, 0.62), (1.5, 7, 0.8, 0.58), (2.0, 9, 1.0, 0.64)]
ARP_4_SPARSE = [(0.0, 0, 2.2, 0.74), (2.0, 4, 1.8, 0.62)]
ARP_3_SPARSE = [(0.0, 0, 2.6, 0.74), (1.5, 4, 1.3, 0.58)]
ARP_STEPPE_GALLOP = [(0.0, 0, 0.5, 0.80), (0.5, 4, 0.35, 0.60), (0.75, 4, 0.35, 0.64),
                     (1.0, 7, 0.5, 0.74), (1.5, 4, 0.35, 0.58), (1.75, 4, 0.35, 0.62),
                     (2.0, 0, 0.5, 0.78), (2.5, 4, 0.35, 0.60), (2.75, 4, 0.35, 0.64),
                     (3.0, 7, 0.6, 0.72), (3.5, 4, 0.4, 0.60)]

DRUM_MAQSUM = [(0.0, 48, 0.4, 0.82), (1.0, 64, 0.3, 0.68), (2.5, 64, 0.3, 0.64), (3.0, 48, 0.4, 0.76), (3.5, 72, 0.25, 0.58)]
DRUM_MALFUF = [(0.0, 48, 0.4, 0.86), (1.5, 64, 0.3, 0.72), (2.0, 48, 0.4, 0.80), (3.0, 64, 0.3, 0.70), (3.5, 72, 0.25, 0.66)]
DRUM_GALLOP = [(0.0, 38, 0.45, 0.84), (0.75, 46, 0.25, 0.58), (1.0, 41, 0.35, 0.68),
               (2.0, 38, 0.45, 0.80), (2.75, 46, 0.25, 0.58), (3.0, 41, 0.35, 0.70)]
DRUM_MARCH  = [(0.0, 38, 0.5, 0.84), (1.0, 46, 0.3, 0.55), (2.0, 41, 0.45, 0.74), (3.0, 46, 0.3, 0.58), (3.5, 46, 0.25, 0.52)]
DRUM_BODHRAN_4 = [(0.0, 43, 0.45, 0.78), (1.5, 57, 0.25, 0.52), (2.0, 50, 0.4, 0.68), (3.0, 57, 0.3, 0.54), (3.5, 64, 0.25, 0.48)]
DRUM_BODHRAN_3 = [(0.0, 43, 0.5, 0.76), (1.0, 57, 0.3, 0.48), (2.0, 50, 0.4, 0.60), (2.5, 64, 0.25, 0.46)]
DRUM_REEL = [(0.0, 43, 0.4, 0.82), (0.5, 64, 0.2, 0.48), (1.0, 57, 0.3, 0.66), (1.5, 64, 0.2, 0.50),
             (2.0, 43, 0.4, 0.78), (2.5, 64, 0.2, 0.48), (3.0, 57, 0.3, 0.68), (3.5, 64, 0.25, 0.56)]


def make_piece(
    pid: str, title: str, cat: str, culture: str, bpm: float, bpb: int, bars: int, loop: bool,
    root: int, mode_name: str, chords: list[int],
    lead_voice: str, lead_phrases: list[tuple[int, str]],
    acc_voice: str, acc_pattern: list[tuple[float, int, float, float]],
    cnt_voice: str | None = None, cnt_start_bar: int = 4, cnt_bars: int = 12, cnt_oct: int = 0,
    drone_voice: str | None = None, drone_midi: int | None = None,
    drum_voice: str | None = None, drum_hits: list[tuple[float, int, float, float]] | None = None, drum_bars: tuple[int, int] | None = None,
    bell_bars: list[tuple[int, int]] | None = None,
    wet: float = 0.30, decay: float = 2.0, master: float = 0.84,
) -> dict:
    scale = MODES[mode_name]
    tracks = []

    if drone_voice:
        dm = drone_midi if drone_midi is not None else (root - 12)
        d_notes = []
        step_bars = 4
        for b in range(0, bars, step_bars):
            dur_b = min(step_bars, bars - b) * bpb - 0.15
            d_notes.append([round(b * bpb, 3), dm, round(dur_b, 3), 0.72, "swell"])
        tracks.append({"voice": drone_voice, "pan": 0.0, "gain": 0.30, "stem": 0, "humanizeMs": 6, "notes": d_notes})

    acc_notes = build_arp(root - 12, scale, bpb, chords, bars, acc_pattern)
    tracks.append({"voice": acc_voice, "pan": -0.22, "gain": 0.44, "stem": 0, "humanizeMs": 9, "notes": acc_notes})

    lead_notes = []
    for bar_idx, phrase_str in lead_phrases:
        lead_notes.extend(parse_phrase(root, scale, bar_idx * bpb, phrase_str))
    tracks.append({"voice": lead_voice, "pan": 0.16, "gain": 0.54, "stem": 0, "humanizeMs": 12, "notes": lead_notes})

    if cnt_voice:
        c_notes = build_counter(root + cnt_oct * 12, scale, bpb, chords, cnt_start_bar, min(cnt_bars, bars - cnt_start_bar))
        tracks.append({"voice": cnt_voice, "pan": -0.14, "gain": 0.36, "stem": 0, "humanizeMs": 14, "notes": c_notes})

    if drum_voice and drum_hits:
        sb, eb = drum_bars if drum_bars else (0, bars)
        d_notes = build_drums(bpb, bars, drum_hits, sb, eb)
        tracks.append({"voice": drum_voice, "pan": 0.06, "gain": 0.40, "stem": 0, "humanizeMs": 7, "notes": d_notes})

    if bell_bars:
        b_notes = [[round(b * bpb, 3), deg_to_midi(root, scale, d), 3.2, 0.68] for b, d in bell_bars if b < bars]
        tracks.append({"voice": "tubular_bell", "pan": 0.26, "gain": 0.25, "stem": 0, "humanizeMs": 5, "notes": b_notes})

    return {
        "id": pid, "title": title, "category": cat, "culture": culture,
        "bpm": bpm, "beatsPerBar": bpb, "bars": bars, "loop": loop,
        "reverbWet": wet, "reverbDecay": decay, "masterGain": master,
        "tracks": tracks,
    }


def build_all() -> dict[str, list[dict]]:
    west, east, places = [], [], []

    # ==================== 1. AZURE CROWN (West) ====================
    west.append(make_piece(
        "azure_day", "High Road to Valen", "map", "azure", 88, 4, 20, False, 62, "dorian", [0, 3, 5, 4, 0, 2, 6, 0],
        "recorder", [
            (2, "0:1.5 2:0.5 4:1.5m 3:0.5 | 2:1 0:1 -1:2 | 0:1 2:1 4:1.5g 5:0.5 | 4:3.5w R:0.5"),
            (6, "4:1.5 5:0.5 7:2w | 6:1 4:1 2:2 | 3:1.5m 2:0.5 0:1 2:1 | 0:3.5w R:0.5"),
            (10, "7:2w 6:1 4:1 | 5:1.5g 4:0.5 2:2 | 4:1 5:1 7:1.5 8:0.5 | 7:3.5w R:0.5"),
            (14, "6:1.5 4:0.5 2:1 3:1 | 4:1.5m 2:0.5 0:2 | -1:1 0:1 2:1.5 1:0.5 | 0:4w"),
        ],
        "lute", ARP_4_LUTE, "viols", 6, 12, -1, "contrabass_drone", 38, None, None, None, [(0, 0), (10, 4), (18, 0)], 0.30, 2.0))

    west.append(make_piece(
        "azure_night", "Watchfires on the Weir", "map", "azure", 60, 3, 20, False, 65, "lydian", [0, 1, 4, 0, 5, 3, 4, 0],
        "french_horn", [
            (2, "0:2w 2:1 | 4:2.5w 2:0.5 | 1:2 0:1 | -3:3w"),
            (6, "0:2 4:1 | 6:2w 4:1 | 5:2g 4:1 | 4:3w"),
            (12, "7:2w 6:1 | 4:2 2:1 | 1:2 2:1 | 0:3w"),
            (16, "2:2w 1:1 | 0:2 -1:1 | 0:5.5w"),
        ],
        "harp", ARP_3_WALTZ, "viols", 4, 14, -1, None, None, None, None, None, [(0, 4), (12, 0)], 0.38, 2.6))

    west.append(make_piece(
        "azure_peace", "Bells of the Blue Gables", "map", "azure", 76, 3, 20, False, 67, "ionian", [0, 4, 5, 3, 0, 3, 4, 0],
        "lute", [
            (1, "0:1 2:1 4:1 | 4:1.5m 5:0.5 4:1 | 2:1 3:1 1:1 | 0:2.5 R:0.5"),
            (5, "4:1 5:1 7:1 | 6:1.5g 5:0.5 4:1 | 3:1 2:1 1:1 | 4:2.5 R:0.5"),
            (10, "7:1.5 6:0.5 4:1 | 5:1.5 4:0.5 2:1 | 3:1 4:1 2:1 | 1:2.5 R:0.5"),
            (14, "0:1 2:1 4:1 | 5:1.5g 4:0.5 2:1 | 1:1.5 2:0.5 -1:1 | 0:5.5w"),
        ],
        "harp", ARP_3_WALTZ, "recorder", 6, 12, 0, None, None, None, None, None, [(0, 0), (8, 4), (16, 0)], 0.34, 2.3))

    west.append(make_piece(
        "azure_host", "Lances of the Crown", "map", "azure", 98, 4, 20, False, 62, "dorian", [0, 6, 3, 4, 0, 5, 6, 0],
        "french_horn", [
            (2, "0:1 0:0.5 0:0.5 4:2w | 3:1 2:1 0:2 | 4:1.5 5:0.5 7:2w | 6:1.5 5:0.5 4:2"),
            (6, "7:1.5 6:0.5 4:1 2:1 | 3:1.5 4:0.5 5:2 | 4:1 2:1 0:1 -1:1 | 0:3.5w R:0.5"),
            (12, "4:1 4:0.5 5:0.5 7:2w | 8:1.5 7:0.5 6:2 | 7:1.5 5:0.5 4:1 3:1 | 4:3.5w R:0.5"),
            (16, "7:1 6:1 4:1 2:1 | 3:1.5 2:0.5 0:2 | -1:1 0:1 2:1 4:1 | 0:4w"),
        ],
        "strings_spiccato", ARP_4_WALK, "low_brass", 4, 14, -1, None, None, "snare_march", DRUM_MARCH, (0, 20), [(0, 0), (12, 4)], 0.26, 1.7))

    # ==================== 2. CRIMSON COMPACT (West) ====================
    west.append(make_piece(
        "crimson_day", "Red Banners on the Causeway", "map", "crimson", 94, 4, 20, False, 67, "mixolydian", [0, 6, 3, 0, 0, 4, 6, 0],
        "shawm", [
            (2, "0:1.5m 1:0.5 2:1 4:1 | 6:1.5g 5:0.5 4:2 | 3:1 4:1 2:1 0:1 | -1:2 0:2"),
            (6, "4:1.5 5:0.5 6:1 7:1 | 6:1.5m 4:0.5 2:2 | 3:1 2:1 0:1 -1:1 | 0:3.5w R:0.5"),
            (12, "7:1.5 6:0.5 7:1 9:1 | 8:1.5g 7:0.5 6:2 | 4:1 6:1 5:1 3:1 | 4:3.5w R:0.5"),
            (16, "6:1.5 5:0.5 4:1 2:1 | 3:1 1:1 0:2 | -1:1 0:1 2:1 1:1 | 0:4w"),
        ],
        "viols", ARP_4_WALK, "french_horn", 6, 12, -1, None, None, "frame_drum", DRUM_BODHRAN_4, (2, 20), None, 0.26, 1.8))

    west.append(make_piece(
        "crimson_night", "Embers in the Pavilion", "map", "crimson", 62, 4, 18, False, 64, "aeolian", [0, 5, 2, 6, 0, 3, 4, 0],
        "viols", [
            (1, "0:2w 2:2 | 1:2.5 -1:1.5 | 0:2 3:2w | 2:3.5w R:0.5"),
            (5, "4:2w 5:2 | 4:2 2:2 | 1:2 0:2 | -1:3.5w R:0.5"),
            (10, "7:2.5w 6:1.5 | 4:2 5:2 | 3:2 2:2 | 1:3.5w R:0.5"),
            (14, "2:2 1:2 | 0:2 -1:2 | 0:6w"),
        ],
        "harpsichord", ARP_4_SPARSE, "recorder", 8, 8, 0, "contrabass_drone", 40, None, None, None, None, 0.36, 2.5))

    west.append(make_piece(
        "crimson_peace", "The Compact's Wine", "map", "crimson", 86, 3, 20, False, 60, "mixolydian", [0, 3, 6, 0, 5, 3, 6, 0],
        "recorder", [
            (2, "4:1 5:0.5 4:0.5 2:1 | 0:1.5 2:0.5 4:1 | 6:1.5m 5:0.5 4:1 | 4:2.5 R:0.5"),
            (6, "7:1 6:1 4:1 | 5:1.5g 4:0.5 2:1 | 3:1 2:1 1:1 | 0:2.5 R:0.5"),
            (12, "4:1 6:1 7:1 | 8:1.5 7:0.5 6:1 | 5:1 4:1 2:1 | 4:2.5 R:0.5"),
            (16, "7:1 6:0.5 5:0.5 4:1 | 3:1 2:1 1:1 | 0:1.5 -1:0.5 0:1 | 0:3w"),
        ],
        "harpsichord", ARP_3_WALTZ, "viols", 4, 14, 0, None, None, "tambourine_riq", [(0.0, 64, 0.3, 0.50), (2.0, 72, 0.25, 0.42)], (2, 18), None, 0.28, 1.9))

    west.append(make_piece(
        "crimson_host", "Pike and Great-Sword", "map", "crimson", 104, 4, 20, False, 64, "phrygian", [0, 1, 6, 0, 3, 1, 6, 0],
        "shawm", [
            (2, "0:1 0:1 1:1.5m 0:0.5 | 4:2w 3:1 1:1 | 0:1 1:1 3:1 4:1 | 4:3.5w R:0.5"),
            (6, "7:1.5 6:0.5 4:1 3:1 | 4:1.5m 3:0.5 1:2 | 0:1 1:1 3:1 1:1 | 0:3.5w R:0.5"),
            (12, "4:1 6:1 7:2w | 8:1.5 7:0.5 6:2 | 7:1 6:1 4:1 3:1 | 4:3.5w R:0.5"),
            (16, "6:1 4:1 3:1 1:1 | 2:1.5 1:0.5 0:2 | 1:1 0:1 -1:1 0:1 | 0:4w"),
        ],
        "strings_spiccato", ARP_4_WALK, "low_brass", 2, 16, -1, None, None, "war_drum", DRUM_MARCH, (0, 20), None, 0.25, 1.7))

    # ==================== 3. NORTHMARCH (West) ====================
    west.append(make_piece(
        "north_day", "Fell-Wind Over Pine", "map", "northern", 84, 4, 20, False, 57, "dorian", [0, 6, 2, 0, 3, 6, 4, 0],
        "nyckelharpa", [
            (2, "0:1.5s 2:0.5 4:2w | 3:1 2:1 0:1 -1:1 | 0:1.5 3:0.5 4:1 6:1 | 4:3.5w R:0.5"),
            (6, "7:1.5g 6:0.5 4:2 | 3:1.5 2:0.5 0:2 | 2:1 3:1 2:1 0:1 | 0:3.5w R:0.5"),
            (12, "4:1.5 6:0.5 7:2w | 9:1.5g 7:0.5 6:2 | 7:1 6:1 4:1 2:1 | 4:3.5w R:0.5"),
            (16, "6:1.5 4:0.5 3:1 2:1 | 0:1.5 -1:0.5 -3:2 | -1:1 0:1 2:1.5 0:0.5 | 0:4w"),
        ],
        "lute", ARP_4_WALK, "low_whistle", 8, 10, 1, "pipes_drone", 45, "frame_drum", DRUM_BODHRAN_4, (2, 18), None, 0.32, 2.1))

    west.append(make_piece(
        "north_night", "Aurora Above the Hall", "map", "northern", 56, 3, 18, False, 62, "aeolian", [0, 5, 2, 6, 0, 3, 6, 0],
        "low_whistle", [
            (2, "4:2w 2:1 | 0:2.5s -1:0.5 | 0:2 2:1 | 4:3w"),
            (6, "7:2w 6:1 | 4:2 2:1 | 1:2g 0:1 | 0:3w"),
            (11, "5:2w 4:1 | 2:2 0:1 | -1:2 0:1 | 2:3w"),
            (15, "4:2 2:1 | 0:2 -1:1 | 0:3.5w"),
        ],
        "harp", ARP_3_SPARSE, "nyckelharpa", 6, 10, 0, "contrabass_drone", 38, None, None, None, None, 0.40, 2.8))

    west.append(make_piece(
        "north_peace", "Smoke from the Longhouse", "map", "northern", 78, 3, 20, False, 67, "dorian", [0, 3, 6, 0, 2, 5, 6, 0],
        "fiddle", [
            (2, "0:1.5s 2:0.5 4:1 | 3:1.5m 2:0.5 0:1 | -1:1.5 0:0.5 2:1 | 0:2.5 R:0.5"),
            (6, "4:1.5 6:0.5 7:1 | 6:1.5g 4:0.5 2:1 | 3:1 2:1 0:1 | 4:2.5 R:0.5"),
            (12, "7:1.5 8:0.5 7:1 | 6:1.5 4:0.5 3:1 | 4:1 2:1 0:1 | -1:2.5 R:0.5"),
            (16, "0:1.5 2:0.5 4:1 | 6:1g 4:1 2:1 | 0:1.5 -1:0.5 0:1 | 0:3w"),
        ],
        "lute", ARP_3_WALTZ, "harp", 6, 12, 0, None, None, "frame_drum", DRUM_BODHRAN_3, (4, 18), None, 0.30, 2.0))

    west.append(make_piece(
        "north_host", "Oars and Dane-Axes", "map", "northern", 100, 4, 20, False, 62, "aeolian", [0, 6, 5, 0, 2, 6, 5, 0],
        "war_horn", [
            (2, "0:2w 4:2w | 3:1.5 2:0.5 0:2 | -1:2 0:1 2:1 | 4:3.5w R:0.5"),
            (6, "7:2w 6:1 4:1 | 5:2 4:2 | 3:1.5 2:0.5 0:1 -1:1 | 0:3.5w R:0.5"),
            (12, "4:1.5 4:0.5 7:2w | 6:1.5 5:0.5 4:2 | 5:1.5 4:0.5 2:1 0:1 | -1:3.5w R:0.5"),
            (16, "0:1.5 2:0.5 4:2 | 6:1.5 4:0.5 2:2 | 0:1 -1:1 0:2 | 0:4w"),
        ],
        "nyckelharpa", ARP_4_WALK, "low_brass", 4, 14, -1, "contrabass_drone", 38, "war_drum", DRUM_MARCH, (0, 20), None, 0.28, 1.9))

    # ==================== 4. SALTMARSH LEAGUE (West) ====================
    west.append(make_piece(
        "league_day", "Cranes on the Lagoon", "map", "league", 92, 4, 20, False, 60, "dorian", [0, 4, 6, 0, 2, 5, 4, 0],
        "cittern", [
            (2, "0:1 2:0.5 3:0.5 4:1.5m 2:0.5 | 4:1 6:1 4:2 | 3:1 2:0.5 0:0.5 -1:1 0:1 | 2:3.5 R:0.5"),
            (6, "4:1 6:0.5 7:0.5 8:1.5g 7:0.5 | 6:1 4:1 2:2 | 3:1 4:1 2:1 -1:1 | 0:3.5 R:0.5"),
            (12, "7:1.5m 6:0.5 4:1 6:1 | 7:1 9:1 7:2 | 6:1 4:1 3:1 2:1 | 4:3.5 R:0.5"),
            (16, "7:1 6:0.5 4:0.5 3:1 2:1 | 4:1 2:1 0:2 | -1:1 0:1 2:1 1:1 | 0:4"),
        ],
        "harpsichord", ARP_4_LUTE, "recorder", 6, 12, 1, None, None, None, None, None, [(0, 0), (10, 4), (18, 0)], 0.28, 1.9))

    west.append(make_piece(
        "league_night", "Lanterns on Black Water", "map", "league", 64, 3, 20, False, 67, "aeolian", [0, 4, 5, 2, 3, 0, 6, 0],
        "fiddle", [
            (2, "0:2w 2:1 | 4:2s 3:1 | 2:2 0:1 | -1:3w"),
            (6, "2:2 4:1 | 5:2g 4:1 | 2:2 1:1 | 0:3w"),
            (12, "7:2w 6:1 | 5:2 4:1 | 2:2 3:1 | 4:3w"),
            (16, "4:2 2:1 | 1:2 -1:1 | 0:5.5w"),
        ],
        "harpsichord", ARP_3_WALTZ, "viols", 4, 14, -1, None, None, None, None, None, [(0, 0), (10, 4)], 0.36, 2.5))

    west.append(make_piece(
        "league_peace", "The Doge's Counting-House", "map", "league", 88, 3, 20, False, 65, "mixolydian", [0, 3, 4, 0, 5, 2, 6, 0],
        "recorder", [
            (2, "4:1 2:0.5 4:0.5 5:1 | 7:1.5m 6:0.5 4:1 | 3:1 2:1 0:1 | 2:2.5 R:0.5"),
            (6, "4:1 5:1 7:1 | 6:1.5g 4:0.5 2:1 | 1:1 2:1 0:1 | 0:2.5 R:0.5"),
            (12, "7:1.5 8:0.5 7:1 | 6:1 4:1 5:1 | 4:1 2:1 3:1 | 4:2.5 R:0.5"),
            (16, "7:1 5:1 4:1 | 3:1.5 2:0.5 0:1 | -1:1 0:1 1:1 | 0:3w"),
        ],
        "cittern", ARP_3_WALTZ, "harp", 6, 12, 0, None, None, None, None, None, [(0, 4)], 0.28, 1.9))

    west.append(make_piece(
        "league_host", "Hired Pikes on the Quay", "map", "league", 102, 4, 20, False, 60, "aeolian", [0, 6, 5, 4, 0, 3, 6, 0],
        "cittern", [
            (2, "0:0.5 0:0.5 2:1 4:1.5m 3:0.5 | 2:1 0:1 -1:2 | 0:1 2:1 3:1 5:1 | 4:3.5 R:0.5"),
            (6, "7:1 6:0.5 5:0.5 4:1 2:1 | 3:1.5 2:0.5 0:2 | -1:1 0:1 2:1 -1:1 | 0:3.5 R:0.5"),
            (12, "4:1 5:1 7:1.5 8:0.5 | 7:1 5:1 4:2 | 3:1 5:1 4:1 2:1 | 4:3.5 R:0.5"),
            (16, "7:1 5:1 4:1 2:1 | 3:1 2:1 0:2 | -1:1 0:1 2:1 -1:1 | 0:4"),
        ],
        "strings_spiccato", ARP_4_WALK, "low_brass", 4, 14, -1, None, None, "snare_march", DRUM_MARCH, (0, 20), [(0, 0), (16, 0)], 0.25, 1.7))

    # ==================== 5. SUNWARD EMIRATES (East) ====================
    east.append(make_piece(
        "sunward_day", "Caravan Through the Palms", "map", "southern", 90, 4, 20, False, 62, "hijaz", [0, 1, 3, 0, 4, 3, 1, 0],
        "oud", [
            (2, "0:1.5m 1:0.5 2:1.5s 3:0.5 | 4:1.5m 3:0.5 2:2 | 1:1 2:0.5 1:0.5 0:1 -1:1 | 0:3.5 R:0.5"),
            (6, "4:1.5 5:0.5 6:1.5g 5:0.5 | 4:1 3:1 2:2 | 3:1.5m 2:0.5 1:1 0:1 | 0:3.5 R:0.5"),
            (12, "4:1 6:1 7:1.5m 6:0.5 | 5:1 4:1 3:2 | 4:1.5g 3:0.5 2:1 1:1 | 2:3.5 R:0.5"),
            (16, "4:1.5 3:0.5 2:1 1:1 | 2:1.5m 1:0.5 0:2 | -1:1 0:1 1:1.5 0:0.5 | 0:4"),
        ],
        "qanun", ARP_4_LUTE, "ney", 6, 12, 0, None, None, "darbuka", DRUM_MAQSUM, (2, 20), None, 0.30, 2.0))

    east.append(make_piece(
        "sunward_night", "Stars Over the Courtyard", "map", "southern", 62, 4, 18, False, 67, "nikriz", [0, 3, 4, 0, 1, 4, 3, 0],
        "ney", [
            (1, "0:2.5w 1:0.5 2:1 | 3:2.5s 4:1.5 | 3:2 2:1 1:1 | 0:3.5w R:0.5"),
            (5, "4:2.5w 5:1.5 | 4:2 3:1 2:1 | 1:2g 0:2 | -1:3.5w R:0.5"),
            (10, "7:2.5w 6:1.5 | 4:2 3:2 | 2:2s 1:2 | 3:3.5w R:0.5"),
            (14, "4:2 3:1 2:1 | 1:2 0:2 | 0:6w"),
        ],
        "oud", ARP_4_SPARSE, "qanun", 6, 10, 0, None, None, None, None, None, None, 0.38, 2.6))

    east.append(make_piece(
        "sunward_peace", "Fountains of Al-Qasr", "map", "southern", 80, 3, 20, False, 65, "mixolydian", [0, 1, 4, 0, 3, 4, 6, 0],
        "qanun", [
            (2, "0:1 2:0.5 3:0.5 4:1 | 6:1.5m 5:0.5 4:1 | 3:1 2:1 1:1 | 2:2.5 R:0.5"),
            (6, "4:1 5:1 7:1 | 6:1.5g 4:0.5 3:1 | 2:1 1:1 0:1 | 0:2.5 R:0.5"),
            (12, "7:1.5m 6:0.5 4:1 | 5:1 4:1 2:1 | 3:1.5 2:0.5 1:1 | 4:2.5 R:0.5"),
            (16, "6:1 4:1 3:1 | 2:1.5g 1:0.5 0:1 | -1:1 0:1 1:1 | 0:3"),
        ],
        "oud", ARP_3_WALTZ, "ney", 6, 12, 0, None, None, "tambourine_riq", [(0.0, 56, 0.3, 0.45), (2.0, 72, 0.25, 0.38)], (2, 18), None, 0.32, 2.2))

    east.append(make_piece(
        "sunward_host", "Banners of the Emir", "map", "southern", 106, 4, 20, False, 64, "hijaz", [0, 1, 0, 6, 3, 1, 6, 0],
        "shawm", [
            (2, "0:1 0:0.5 1:0.5 2:1.5m 3:0.5 | 4:2w 3:1 2:1 | 1:1 2:1 3:1 1:1 | 0:3.5w R:0.5"),
            (6, "4:1 4:0.5 5:0.5 6:2w | 5:1 4:1 3:1 2:1 | 1:1.5 2:0.5 1:1 0:1 | 0:3.5w R:0.5"),
            (12, "7:1.5g 6:0.5 4:1 5:1 | 6:1.5 5:0.5 4:2 | 3:1 4:1 2:1 1:1 | 2:3.5w R:0.5"),
            (16, "4:1 3:1 2:1 1:1 | 2:1.5 1:0.5 0:2 | -1:1 0:1 1:1 0:1 | 0:4w"),
        ],
        "oud", ARP_4_WALK, "viols", 4, 14, 0, None, None, "darbuka", DRUM_MALFUF, (0, 20), None, 0.26, 1.8))

    # ==================== 6. KHAGANATE OF THE GRASS (East) ====================
    east.append(make_piece(
        "steppe_day", "Ten Thousand Hooves", "map", "steppe", 104, 4, 20, False, 64, "penta_min", [0, 0, 3, 4, 0, 2, 3, 0],
        "morin_khuur", [
            (2, "0:2s 2:1 3:1 | 4:2.5w 3:1.5 | 2:1.5 0:0.5 -1:2 | 0:3.5w R:0.5"),
            (6, "3:1.5s 4:0.5 5:2w | 4:1.5 3:0.5 2:2 | 0:1.5 2:0.5 0:1 -1:1 | 0:3.5w R:0.5"),
            (12, "5:2.5w 7:1.5 | 6:2s 5:2 | 4:1.5 3:0.5 2:2 | 3:3.5w R:0.5"),
            (16, "5:1.5 4:0.5 3:1 2:1 | 3:1.5 2:0.5 0:2 | -1:1.5 0:0.5 2:1 0:1 | 0:4w"),
        ],
        "lute", ARP_STEPPE_GALLOP, None, 0, 0, 0, "throat_drone", 40, "frame_drum", DRUM_GALLOP, (2, 20), None, 0.30, 2.0))

    east.append(make_piece(
        "steppe_night", "Felt Tents Under Tengri", "map", "steppe", 58, 4, 18, False, 59, "penta_min", [0, 3, 2, 0, 4, 2, 3, 0],
        "morin_khuur", [
            (1, "0:3w 2:1 | 3:3s 2:1 | 0:2 -1:2 | 0:3.5w R:0.5"),
            (5, "3:2.5w 5:1.5 | 4:3w 3:1 | 2:2 0:2 | -1:3.5w R:0.5"),
            (10, "5:3w 4:1 | 3:2s 2:2 | 0:2 2:2 | 3:3.5w R:0.5"),
            (14, "2:2 0:2 | -1:2 0:2 | 0:6w"),
        ],
        "lute", ARP_4_SPARSE, "ney", 6, 10, 1, "throat_drone", 35, None, None, None, None, 0.38, 2.7))

    east.append(make_piece(
        "steppe_peace", "Mares by the River", "map", "steppe", 82, 3, 20, False, 62, "penta_maj", [0, 2, 3, 0, 4, 2, 3, 0],
        "low_whistle", [
            (2, "0:1.5 1:0.5 2:1 | 4:2w 2:1 | 1:1.5g 0:0.5 -1:1 | 0:2.5 R:0.5"),
            (6, "2:1 4:1 5:1 | 4:2w 2:1 | 1:1 2:1 0:1 | 0:2.5 R:0.5"),
            (12, "5:2w 4:1 | 2:1.5 1:0.5 0:1 | 1:1 2:1 4:1 | 2:2.5 R:0.5"),
            (16, "4:1.5 2:0.5 1:1 | 0:1.5 -1:0.5 0:1 | 1:1 0:2 | 0:3w"),
        ],
        "lute", ARP_3_WALTZ, "morin_khuur", 4, 14, -1, None, None, None, None, None, None, 0.32, 2.2))

    east.append(make_piece(
        "steppe_host", "The Noyan's Horn", "map", "steppe", 114, 4, 20, False, 64, "dorian", [0, 0, 6, 0, 3, 6, 5, 0],
        "war_horn", [
            (2, "0:1.5 0:0.5 4:2w | 6:1.5 4:0.5 3:2 | 2:1 0:1 -1:1 0:1 | 4:3.5w R:0.5"),
            (6, "7:2w 6:1 4:1 | 5:1.5 4:0.5 2:2 | 0:1 2:1 3:1 2:1 | 0:3.5w R:0.5"),
            (12, "4:1 4:1 7:2w | 9:1.5 7:0.5 6:2 | 4:1 6:1 4:1 2:1 | 4:3.5w R:0.5"),
            (16, "7:1.5 6:0.5 4:1 2:1 | 3:1 2:1 0:2 | -1:1 0:1 2:1 0:1 | 0:4w"),
        ],
        "lute", ARP_STEPPE_GALLOP, "morin_khuur", 2, 16, 0, "throat_drone", 40, "war_drum", DRUM_GALLOP, (0, 20), None, 0.26, 1.8))

    # ==================== 7. ORDER OF THE ASHEN SUN (East) ====================
    east.append(make_piece(
        "order_day", "Pilgrims of the Grey Road", "map", "order", 70, 4, 20, False, 64, "phrygian", [0, 4, 5, 0, 2, 6, 4, 0],
        "choir_aah", [
            (2, "0:2w 1:2 | 2:2 4:2w | 3:2 2:2 | 1:3.5w R:0.5"),
            (6, "4:2w 5:2 | 4:2 2:2 | 1:2 0:2 | 0:3.5w R:0.5"),
            (12, "4:2w 7:2w | 6:2 4:2 | 5:2 3:2 | 4:3.5w R:0.5"),
            (16, "4:2 2:2 | 1:2 0:2 | 1:2 -1:2 | 0:4w"),
        ],
        "harp", ARP_4_SPARSE, "viols", 2, 16, -1, "contrabass_drone", 40, None, None, None, [(0, 0), (8, 4), (16, 0)], 0.38, 2.7))

    east.append(make_piece(
        "order_night", "Compline in the Cloister", "map", "order", 54, 4, 16, False, 62, "dorian", [0, 3, 6, 0, 4, 2, 6, 0],
        "choir_aah", [
            (1, "0:3w 2:1 | 4:3w 3:1 | 2:2 0:2 | -1:3.5w R:0.5"),
            (5, "2:2w 4:2 | 6:3w 4:1 | 3:2 2:2 | 0:3.5w R:0.5"),
            (9, "7:3w 6:1 | 4:2 2:2 | 3:2 4:2 | 4:3.5w R:0.5"),
            (13, "2:2 0:2 | -1:2 0:2 | 0:4w"),
        ],
        "harp", ARP_4_SPARSE, "viols", 1, 14, -1, "contrabass_drone", 38, None, None, None, [(0, 0), (12, 0)], 0.42, 3.0))

    east.append(make_piece(
        "order_peace", "Ashen Sun Rising", "map", "order", 74, 3, 20, False, 69, "dorian", [0, 2, 3, 4, 0, 5, 6, 0],
        "french_horn", [
            (2, "0:2w 2:1 | 4:2w 6:1 | 5:2 4:1 | 2:2.5 R:0.5"),
            (6, "4:2 7:1 | 6:2 4:1 | 2:2 1:1 | 0:2.5 R:0.5"),
            (12, "7:2w 6:1 | 4:2 5:1 | 6:2 4:1 | 4:2.5 R:0.5"),
            (16, "4:2 2:1 | 0:2 -1:1 | 0:5.5w"),
        ],
        "harp", ARP_3_WALTZ, "choir_aah", 4, 14, 0, None, None, None, None, None, [(0, 0), (12, 4)], 0.36, 2.5))

    east.append(make_piece(
        "order_host", "Vow of Iron and Ash", "map", "order", 94, 4, 20, False, 62, "phrygian", [0, 1, 4, 0, 6, 1, 4, 0],
        "war_horn", [
            (2, "0:2w 1:1 0:1 | 4:2w 3:2 | 2:1.5 1:0.5 0:2 | 4:3.5w R:0.5"),
            (6, "7:2w 6:1 4:1 | 5:2 4:2 | 3:1 2:1 1:2 | 0:3.5w R:0.5"),
            (12, "4:1.5 4:0.5 7:2w | 8:2 7:2 | 6:1 4:1 3:2 | 4:3.5w R:0.5"),
            (16, "6:1 4:1 3:1 1:1 | 2:2 0:2 | 1:2 -1:2 | 0:4w"),
        ],
        "strings_spiccato", ARP_4_WALK, "choir_aah", 2, 16, 0, None, None, "war_drum", DRUM_MARCH, (0, 20), [(0, 0), (8, 4), (16, 0)], 0.30, 2.1))

    # ==================== 8. SETTLEMENTS, HALLS & SITES (8 looped pieces) ====================
    places.append(make_piece(
        "town_market", "Cobbles and Coin", "place", "common", 102, 4, 20, True, 67, "mixolydian", [0, 3, 6, 0, 4, 3, 6, 0],
        "recorder", [
            (0, "0:1 2:0.5 4:0.5 4:1 6:1 | 5:1.5m 4:0.5 2:2 | 3:1 2:0.5 0:0.5 -1:1 0:1 | 2:3.5 R:0.5"),
            (4, "0:1 2:0.5 4:0.5 7:1.5g 6:0.5 | 5:1 4:1 2:2 | 3:1 4:1 2:1 -1:1 | 0:3.5 R:0.5"),
            (10, "7:1 6:0.5 4:0.5 5:1 7:1 | 6:1.5 4:0.5 2:2 | 3:1 4:1 5:1 6:1 | 4:3.5 R:0.5"),
            (14, "7:1 6:1 4:1 2:1 | 3:1.5m 2:0.5 0:2 | -1:1 0:1 2:1 1:1 | 0:3.5 R:0.5"),
        ],
        "cittern", ARP_4_LUTE, "lute", 8, 12, 0, None, None, "tambourine_riq",
        [(0.0, 56, 0.3, 0.55), (1.5, 72, 0.2, 0.42), (2.0, 64, 0.3, 0.50), (3.5, 72, 0.2, 0.44)], (0, 20), None, 0.26, 1.6))

    places.append(make_piece(
        "tavern_reel_1", "The Foaming Jack", "place", "common", 120, 4, 20, True, 62, "mixolydian", [0, 0, 6, 3, 0, 4, 6, 0],
        "fiddle", [
            (0, "0:0.5 2:0.5 4:1 4:0.5 6:0.5 4:1 | 3:0.5 2:0.5 0:1 -1:1 0:1 | 0:0.5 2:0.5 4:1 7:1 6:1 | 4:3.5m R:0.5"),
            (4, "7:0.5 6:0.5 4:1 5:0.5 4:0.5 2:1 | 3:0.5 2:0.5 0:1 2:1 4:1 | 3:1 2:0.5 0:0.5 -1:1 0:1 | 0:3.5 R:0.5"),
            (10, "4:0.5 5:0.5 7:1 7:0.5 8:0.5 7:1 | 6:0.5 5:0.5 4:1 2:2 | 4:0.5 5:0.5 7:1 6:1 4:1 | 4:3.5 R:0.5"),
            (14, "7:0.5 6:0.5 4:1 2:0.5 3:0.5 4:1 | 3:0.5 2:0.5 0:1 -1:2 | 0:0.5 2:0.5 4:1 2:1 1:1 | 0:3.5 R:0.5"),
        ],
        "cittern", ARP_4_LUTE, "lute", 4, 16, 0, "pipes_drone", 50, "frame_drum", DRUM_REEL, (0, 20), None, 0.24, 1.4))

    places.append(make_piece(
        "tavern_reel_2", "Saddle-Sore Jig", "place", "common", 116, 3, 24, True, 64, "dorian", [0, 6, 3, 4, 0, 2, 6, 0],
        "fiddle", [
            (0, "0:1 2:0.5 3:0.5 4:1 | 6:1m 4:1 2:1 | 3:1 2:0.5 0:0.5 -1:1 | 0:2.5 R:0.5"),
            (4, "4:1 6:0.5 7:0.5 7:1 | 6:1g 4:1 2:1 | 3:1 4:1 2:1 | 0:2.5 R:0.5"),
            (12, "7:1 8:0.5 7:0.5 6:1 | 4:1 6:1 7:1 | 6:1 4:0.5 2:0.5 3:1 | 4:2.5 R:0.5"),
            (16, "7:1 6:0.5 4:0.5 2:1 | 3:1 2:0.5 0:0.5 -1:1 | 0:1 2:1 1:1 | 0:2.5 R:0.5"),
        ],
        "lute", ARP_3_WALTZ, "recorder", 8, 16, 1, "pipes_drone", 52, "frame_drum", DRUM_BODHRAN_3, (0, 24), None, 0.24, 1.4))

    places.append(make_piece(
        "tavern_ballad", "Last Winter's Pay", "place", "common", 76, 3, 20, True, 69, "aeolian", [0, 5, 2, 6, 0, 3, 4, 0],
        "low_whistle", [
            (0, "0:2w 2:1 | 4:2s 3:1 | 2:1.5 0:0.5 -1:1 | 0:2.5 R:0.5"),
            (4, "4:2 6:1 | 7:2w 6:1 | 4:1.5g 2:0.5 3:1 | 4:2.5 R:0.5"),
            (10, "7:2w 6:1 | 5:2 4:1 | 2:1.5 3:0.5 1:1 | 0:2.5 R:0.5"),
            (14, "2:2 4:1 | 2:1.5 0:0.5 -1:1 | 0:2.5 R:0.5"),
        ],
        "lute", ARP_3_WALTZ, "harp", 4, 16, -1, None, None, None, None, None, None, 0.30, 1.9))

    places.append(make_piece(
        "lords_hall", "Tapestry and Stone", "place", "common", 74, 4, 20, True, 62, "dorian", [0, 3, 4, 0, 5, 2, 6, 0],
        "viols", [
            (0, "0:2w 4:2 | 3:1.5m 2:0.5 0:2 | 2:2 5:2w | 4:3.5w R:0.5"),
            (4, "7:2w 6:1 4:1 | 5:2 3:2 | 2:1.5 0:0.5 -1:2 | 0:3.5w R:0.5"),
            (10, "4:2 6:2w | 7:2 4:2 | 5:1.5 4:0.5 2:2 | 4:3.5w R:0.5"),
            (14, "6:2 4:1 2:1 | 3:2 0:2 | -1:1.5 0:0.5 2:2 | 0:3.5w R:0.5"),
        ],
        "harp", ARP_4_WALK, "french_horn", 8, 12, -1, None, None, None, None, None, None, 0.36, 2.4))

    places.append(make_piece(
        "hall_feast", "Tables Groaning", "place", "common", 114, 4, 20, True, 67, "mixolydian", [0, 3, 6, 0, 4, 3, 6, 0],
        "shawm", [
            (0, "0:1 0:0.5 2:0.5 4:1 4:1 | 6:1.5m 5:0.5 4:2 | 3:1 4:1 2:1 0:1 | 2:3.5 R:0.5"),
            (4, "4:1 5:0.5 6:0.5 7:1.5g 6:0.5 | 5:1 4:1 2:2 | 3:1 2:1 0:1 -1:1 | 0:3.5 R:0.5"),
            (10, "7:1 7:0.5 8:0.5 7:1 6:1 | 5:1 6:1 4:2 | 4:1 6:1 5:1 3:1 | 4:3.5 R:0.5"),
            (14, "7:1 6:0.5 5:0.5 4:1 2:1 | 3:1 2:1 0:2 | -1:1 0:1 2:1 1:1 | 0:3.5 R:0.5"),
        ],
        "lute", ARP_4_LUTE, "fiddle", 4, 16, 0, None, None, "frame_drum", DRUM_REEL, (0, 20), None, 0.28, 1.8))

    places.append(make_piece(
        "chapter_house", "Beneath the Crossed Vault", "place", "order", 60, 4, 16, True, 64, "phrygian", [0, 4, 2, 0, 5, 3, 6, 0],
        "choir_aah", [
            (0, "0:3w 1:1 | 2:2 4:2w | 3:2 1:2 | 0:3.5w R:0.5"),
            (4, "4:3w 5:1 | 4:2 2:2 | 1:2 0:2 | -1:3.5w R:0.5"),
            (8, "4:2w 7:2w | 6:2 4:2 | 3:2 2:2 | 4:3.5w R:0.5"),
            (12, "4:2 2:2 | 1:2 0:2 | 1:2 -1:2 | 0:3.5w R:0.5"),
        ],
        "harp", ARP_4_SPARSE, "viols", 0, 16, -1, "contrabass_drone", 40, None, None, None, [(0, 0), (8, 4)], 0.42, 3.0))

    places.append(make_piece(
        "sacred_site", "Stones Older Than Kings", "place", "common", 58, 4, 16, True, 65, "lydian", [0, 1, 4, 0, 2, 4, 6, 0],
        "ney", [
            (0, "4:3w 3:1 | 4:2 6:2w | 4:3w 2:1 | 1:3.5w R:0.5"),
            (4, "0:2w 2:2 | 4:3w 7:1 | 6:2 4:2 | 4:3.5w R:0.5"),
            (8, "7:3w 6:1 | 4:2 2:2 | 3:2 4:2 | 2:3.5w R:0.5"),
            (12, "4:2 2:2 | 1:2 0:2 | -1:2 0:2 | 0:3.5w R:0.5"),
        ],
        "harp", ARP_4_SPARSE, "choir_aah", 4, 12, 0, None, None, None, None, None, [(0, 4), (8, 0)], 0.42, 3.0))

    # ==================== 9. SEASONS & DREAD (4 pieces) ====================
    places.append(make_piece(
        "winter_march", "Rime on the Mail", "map", "common", 68, 4, 18, False, 69, "aeolian", [0, 5, 3, 6, 0, 2, 4, 0],
        "low_whistle", [
            (2, "0:2w 2:1 3:1 | 2:2.5 0:1.5 | -1:2 0:2 | 2:3.5w R:0.5"),
            (6, "4:2.5w 5:1.5 | 4:2 2:2 | 1:2 0:2 | 0:3.5w R:0.5"),
            (11, "7:2.5w 6:1.5 | 4:2 2:2 | 3:2 1:2 | 2:3.5w R:0.5"),
            (15, "2:2 0:2 | -1:2 0:2 | 0:4w"),
        ],
        "harp", ARP_4_SPARSE, "viols", 4, 12, -1, "contrabass_drone", 45, None, None, None, [(0, 4), (10, 0)], 0.38, 2.7))

    places.append(make_piece(
        "winter_night", "The White Stillness", "map", "common", 54, 3, 16, False, 64, "phrygian", [0, 1, 5, 0, 3, 1, 6, 0],
        "recorder", [
            (2, "4:2.5w 3:0.5 | 1:2 0:1 | -1:2 0:1 | 1:3w"),
            (6, "4:2w 6:1 | 5:2 4:1 | 1:2 0:1 | 0:3w"),
            (11, "3:2w 1:1 | 0:2 -1:1 | 0:5.5w"),
        ],
        "harp", ARP_3_SPARSE, "choir_aah", 4, 10, 0, "contrabass_drone", 40, None, None, None, [(0, 0)], 0.42, 3.0))

    places.append(make_piece(
        "omen_graves", "Crows Over the Cairn", "map", "common", 58, 4, 16, False, 60, "locrian", [0, 1, 4, 0, 5, 4, 1, 0],
        "ney", [
            (1, "0:2.5w 1:1.5 | 4:3s 3:1 | 1:2 0:2 | -1:3.5w R:0.5"),
            (5, "4:2.5w 5:1.5 | 4:2 1:2 | 0:2 -2:2 | 0:3.5w R:0.5"),
            (10, "7:2.5w 4:1.5 | 3:2 1:2 | 0:2 1:2 | 0:6w"),
        ],
        "harp", ARP_4_SPARSE, "choir_aah", 3, 12, 0, "contrabass_drone", 36, None, None, None, [(0, 4), (8, 0)], 0.42, 3.0))

    places.append(make_piece(
        "black_flame", "Whisper of the Hollow Crown", "place", "common", 64, 4, 16, True, 62, "hollow", [0, 1, 3, 0, 4, 3, 1, 0],
        "choir_aah", [
            (0, "0:3w 1:1 | 3:3w 2:1 | 1:2 0:2 | -1:3.5w R:0.5"),
            (4, "4:3w 3:1 | 2:2 1:2 | 3:2 0:2 | 0:3.5w R:0.5"),
            (8, "7:3w 6:1 | 4:2 3:2 | 2:2 1:2 | 3:3.5w R:0.5"),
            (12, "3:2 1:2 | 0:2 -1:2 | 1:2 0:2 | 0:3.5w R:0.5"),
        ],
        "lute", ARP_4_SPARSE, "low_brass", 4, 12, -1, "throat_drone", 38, "frame_drum",
        [(0.0, 43, 0.5, 0.68), (2.5, 50, 0.35, 0.48)], (0, 16), [(0, 3), (8, 0)], 0.40, 2.8))

    return {"map_west.json": west, "map_east.json": east, "places.json": places}


if __name__ == "__main__":
    out_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Data", "music"))
    os.makedirs(out_dir, exist_ok=True)
    packs = build_all()
    total = 0
    for fname, pieces in packs.items():
        path = os.path.join(out_dir, fname)
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"pieces": pieces}, f, separators=(",", ":"))
        total += len(pieces)
        print(f"Wrote {fname}: {len(pieces)} pieces ({os.path.getsize(path) / 1024:.1f} KB)")
    print(f"Total AU4 pieces written: {total}")
