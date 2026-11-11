#!/usr/bin/env python3
"""
Deterministic 4-stem battle score compiler for AU5 (13 hand-authored battle pieces):
  - Data/music/battle_field.json   (7 cultural field battle scores)
  - Data/music/battle_special.json (6 special encounter battle scores)

Every piece has 4 sample-locked stems (0=Deploy, 1=Skirmish, 2=Full Melee, 3=Crisis/Climax),
hand-authored modal phrases (A, A', B, Coda), conservative per-track gains (0.24..0.46) and
masterGain=0.72 so all 4 stems sum cleanly with headroom. Zero randomness.
"""
import json
import os

VALID_VOICES = {
    "lute", "harp", "oud", "cittern", "harpsichord", "qanun",
    "fiddle", "nyckelharpa", "morin_khuur", "viols", "strings_spiccato", "contrabass_drone",
    "recorder", "ney", "shawm", "low_whistle", "pipes_drone",
    "war_horn", "french_horn", "low_brass", "choir_aah", "throat_drone",
    "war_drum", "frame_drum", "darbuka", "snare_march", "tambourine_riq", "tubular_bell",
}
VALID_ORNAMENTS = {"grace", "mordent", "slide", "roll", "swell"}

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
}

ORN_MAP = {"g": "grace", "m": "mordent", "s": "slide", "r": "roll", "w": "swell"}


def deg_to_midi(root: int, scale: list[int], deg: int) -> int:
    n = len(scale)
    octv, idx = divmod(deg, n)
    return max(24, min(96, root + scale[idx] + 12 * octv))


def parse_phrase(root: int, scale: list[int], start_beat: float, tokens: str, vel_scale: float = 1.0) -> list[list]:
    out = []
    beat = start_beat
    for raw in tokens.replace("|", " ").split():
        head, dur_s = raw.split(":")
        orn = None
        if dur_s[-1].isalpha():
            orn = ORN_MAP[dur_s[-1]]
            dur = float(dur_s[:-1])
        else:
            dur = float(dur_s)
        if head != "R":
            deg = int(head)
            midi = deg_to_midi(root, scale, deg)
            vel = min(0.94, max(0.45, (0.74 + 0.035 * min(dur, 3.0) + 0.01 * max(-3, min(deg, 8))) * vel_scale))
            note_dur = dur if orn == "roll" else dur * 0.93
            item: list = [round(beat, 3), midi, round(note_dur, 3), round(vel, 3)]
            if orn:
                item.append(orn)
            out.append(item)
        beat += dur
    return out


def build_ostinato(root: int, scale: list[int], bpb: int, chords: list[int], bars: int,
                   pattern: list[tuple[float, int, float, float]]) -> list[list]:
    notes = []
    for bar in range(bars):
        c = chords[bar % len(chords)]
        base = bar * bpb
        for off, deg_off, dur, vel in pattern:
            if off < bpb:
                notes.append([round(base + off, 3), deg_to_midi(root, scale, c + deg_off), round(dur, 3), round(vel, 3)])
    return notes


def build_drums(bpb: int, bars: int, hits: list[tuple], bar_step: int = 1, bar_offset: int = 0) -> list[list]:
    notes = []
    for bar in range(bar_offset, bars, bar_step):
        base = bar * bpb
        for h in hits:
            off, midi, dur, vel = h[0], h[1], h[2], h[3]
            orn = h[4] if len(h) > 4 else None
            if off < bpb:
                item: list = [round(base + off, 3), midi, round(dur, 3), round(vel, 3)]
                if orn:
                    item.append(orn)
                notes.append(item)
    return notes


def build_harmony_pads(root: int, scale: list[int], bpb: int, chords: list[int], bars: int,
                       deg_shift: int, step_bars: int = 2, vel: float = 0.78) -> list[list]:
    notes = []
    for bar in range(0, bars, step_bars):
        span = min(step_bars, bars - bar)
        c = chords[bar % len(chords)]
        dur = round(span * bpb - 0.16, 3)
        notes.append([round(bar * bpb, 3), deg_to_midi(root, scale, c + deg_shift), dur, round(vel, 3), "swell"])
    return notes


# --- Ostinato patterns (beat_offset, scale_deg_offset, dur_beats, velocity) ---
OST_SPICCATO_4 = [
    (0.0, 0, 0.42, 0.84), (0.5, 0, 0.36, 0.68), (1.0, 4, 0.42, 0.76), (1.5, 0, 0.36, 0.66),
    (2.0, 2, 0.42, 0.80), (2.5, 4, 0.36, 0.68), (3.0, 6, 0.42, 0.78), (3.5, 4, 0.36, 0.70),
]
OST_MARCH_4 = [
    (0.0, 0, 0.85, 0.84), (1.0, 4, 0.42, 0.70), (1.5, 4, 0.40, 0.66),
    (2.0, 7, 0.85, 0.80), (3.0, 4, 0.42, 0.72), (3.5, 2, 0.40, 0.68),
]
OST_GALLOP_4 = [
    (0.0, 0, 0.45, 0.85), (0.5, 4, 0.24, 0.66), (0.75, 4, 0.24, 0.70),
    (1.0, 7, 0.45, 0.78), (1.5, 4, 0.24, 0.64), (1.75, 4, 0.24, 0.68),
    (2.0, 0, 0.45, 0.82), (2.5, 4, 0.24, 0.66), (2.75, 4, 0.24, 0.70),
    (3.0, 6, 0.45, 0.78), (3.5, 4, 0.40, 0.68),
]
OST_ARABESQUE_4 = [
    (0.0, 0, 0.65, 0.84), (0.75, 1, 0.35, 0.68), (1.0, 2, 0.45, 0.76), (1.5, 4, 0.42, 0.72),
    (2.0, 0, 0.65, 0.80), (2.75, 3, 0.35, 0.68), (3.0, 2, 0.45, 0.76), (3.5, 1, 0.42, 0.70),
]
OST_DUEL_3 = [
    (0.0, 0, 0.85, 0.85), (1.0, 4, 0.44, 0.72), (1.5, 7, 0.44, 0.76),
    (2.0, 6, 0.44, 0.74), (2.5, 4, 0.44, 0.70),
]
OST_HEAVY_4 = [
    (0.0, 0, 0.90, 0.86), (1.0, 0, 0.42, 0.70), (1.5, 3, 0.42, 0.74),
    (2.0, 4, 0.90, 0.84), (3.0, 1, 0.85, 0.78),
]

# --- Percussion grooves (beat_offset, midi, dur, vel[, ornament]) ---
GROOVE_SNARE_MARCH = [
    (0.0, 52, 0.32, 0.82), (0.75, 60, 0.20, 0.56), (1.0, 60, 0.28, 0.72),
    (2.0, 52, 0.32, 0.78), (2.5, 60, 0.22, 0.60), (3.0, 67, 0.30, 0.76), (3.5, 60, 0.45, 0.64, "roll"),
]
GROOVE_FRAME_DRIVE = [
    (0.0, 43, 0.42, 0.84), (0.5, 57, 0.22, 0.56), (1.0, 50, 0.35, 0.72), (1.5, 64, 0.22, 0.58),
    (2.0, 43, 0.42, 0.80), (2.75, 57, 0.22, 0.62), (3.0, 50, 0.35, 0.76), (3.5, 64, 0.24, 0.64),
]
GROOVE_DARBUKA_WAR = [
    (0.0, 48, 0.38, 0.86), (0.75, 64, 0.22, 0.66), (1.0, 56, 0.30, 0.74), (1.5, 72, 0.22, 0.68),
    (2.0, 48, 0.38, 0.82), (2.5, 64, 0.24, 0.66), (3.0, 56, 0.32, 0.78), (3.5, 72, 0.24, 0.72),
]
GROOVE_GALLOP_FRAME = [
    (0.0, 43, 0.40, 0.86), (0.5, 57, 0.20, 0.58), (0.75, 64, 0.20, 0.64),
    (1.0, 50, 0.35, 0.76), (1.5, 57, 0.20, 0.56), (1.75, 64, 0.20, 0.62),
    (2.0, 43, 0.40, 0.82), (2.5, 57, 0.20, 0.58), (2.75, 64, 0.20, 0.66),
    (3.0, 50, 0.36, 0.78), (3.5, 64, 0.24, 0.64),
]
GROOVE_TAMB_ACCENT = [
    (1.0, 64, 0.24, 0.64), (2.5, 72, 0.20, 0.56), (3.5, 64, 0.24, 0.62),
]
GROOVE_DUEL_3 = [
    (0.0, 48, 0.38, 0.84), (0.5, 64, 0.20, 0.56), (1.0, 56, 0.30, 0.72),
    (1.5, 72, 0.20, 0.62), (2.0, 48, 0.34, 0.78), (2.5, 64, 0.22, 0.68),
]


def make_battle_piece(
    pid: str, title: str, culture: str, bpm: float, bpb: int, bars: int,
    root: int, mode_name: str, chords: list[int],
    # Stem 0: Deploy / Tension anchor
    drone_voice: str, drone_midi: int,
    hb_voice: str, hb_hits: list[tuple],
    deploy_motif_voice: str, deploy_phrases: list[tuple[int, str]],
    # Stem 1: Skirmish
    ost_voice: str, ost_oct: int, ost_pattern: list[tuple[float, int, float, float]],
    ost2_voice: str | None, ost2_oct: int, ost2_pattern: list[tuple[float, int, float, float]] | None,
    skirm_drum_voice: str, skirm_hits: list[tuple],
    skirm_aux_voice: str | None, skirm_aux_hits: list[tuple] | None,
    # Stem 2: Full Melee
    lead_voice: str, lead_phrases: list[tuple[int, str]],
    lead2_voice: str | None, lead2_phrases: list[tuple[int, str]] | None,
    melee_drum_hits: list[tuple],
    # Stem 3: Crisis / Climax
    crisis_brass_voice: str, crisis_brass_deg: int,
    choir_deg: int,
    bell_bars: list[tuple[int, int]],
    wet: float = 0.26, decay: float = 1.75,
) -> dict:
    scale = MODES[mode_name]
    tracks: list[dict] = []

    # ---- STEM 0: Deploy / Tension anchor (drone + heartbeat drum + pre-battle motif) ----
    drone_notes = []
    for b in range(0, bars, 4):
        span = min(4, bars - b)
        drone_notes.append([round(b * bpb, 3), drone_midi, round(span * bpb - 0.12, 3), 0.84, "swell"])
    tracks.append({
        "voice": drone_voice, "pan": 0.0, "gain": 0.34, "stem": 0, "humanizeMs": 5, "notes": drone_notes,
    })

    tracks.append({
        "voice": hb_voice, "pan": -0.06, "gain": 0.35, "stem": 0, "humanizeMs": 7,
        "notes": build_drums(bpb, bars, hb_hits),
    })

    dep_notes = []
    for bar_idx, p_str in deploy_phrases:
        dep_notes.extend(parse_phrase(root, scale, bar_idx * bpb, p_str, vel_scale=0.92))
    tracks.append({
        "voice": deploy_motif_voice, "pan": 0.18, "gain": 0.32, "stem": 0, "humanizeMs": 11, "notes": dep_notes,
    })

    # ---- STEM 1: Skirmish (driving ostinato + percussion groove) ----
    tracks.append({
        "voice": ost_voice, "pan": -0.24, "gain": 0.36, "stem": 1, "humanizeMs": 8,
        "notes": build_ostinato(root + 12 * ost_oct, scale, bpb, chords, bars, ost_pattern),
    })
    if ost2_voice and ost2_pattern:
        tracks.append({
            "voice": ost2_voice, "pan": 0.22, "gain": 0.28, "stem": 1, "humanizeMs": 9,
            "notes": build_ostinato(root + 12 * ost2_oct, scale, bpb, chords, bars, ost2_pattern),
        })
    tracks.append({
        "voice": skirm_drum_voice, "pan": 0.10, "gain": 0.35, "stem": 1, "humanizeMs": 6,
        "notes": build_drums(bpb, bars, skirm_hits),
    })
    if skirm_aux_voice and skirm_aux_hits:
        tracks.append({
            "voice": skirm_aux_voice, "pan": -0.18, "gain": 0.25, "stem": 1, "humanizeMs": 7,
            "notes": build_drums(bpb, bars, skirm_aux_hits),
        })

    # ---- STEM 2: Full Melee (hand-authored A, A', B, Coda cultural lead + full war_drum drive) ----
    lead_notes = []
    for bar_idx, p_str in lead_phrases:
        lead_notes.extend(parse_phrase(root, scale, bar_idx * bpb, p_str, vel_scale=1.0))
    tracks.append({
        "voice": lead_voice, "pan": 0.14, "gain": 0.44, "stem": 2, "humanizeMs": 10, "notes": lead_notes,
    })
    if lead2_voice and lead2_phrases:
        lead2_notes = []
        for bar_idx, p_str in lead2_phrases:
            lead2_notes.extend(parse_phrase(root, scale, bar_idx * bpb, p_str, vel_scale=0.96))
        tracks.append({
            "voice": lead2_voice, "pan": -0.16, "gain": 0.38, "stem": 2, "humanizeMs": 11, "notes": lead2_notes,
        })
    tracks.append({
        "voice": "war_drum", "pan": 0.0, "gain": 0.40, "stem": 2, "humanizeMs": 6,
        "notes": build_drums(bpb, bars, melee_drum_hits),
    })

    # ---- STEM 3: Crisis / Climax (soaring choir_aah + french_horn/low_brass + drum rolls + tubular_bell) ----
    tracks.append({
        "voice": "choir_aah", "pan": -0.20, "gain": 0.32, "stem": 3, "humanizeMs": 14,
        "notes": build_harmony_pads(root, scale, bpb, chords, bars, choir_deg, step_bars=2, vel=0.80),
    })
    tracks.append({
        "voice": crisis_brass_voice, "pan": 0.20, "gain": 0.30, "stem": 3, "humanizeMs": 12,
        "notes": build_harmony_pads(root - 12, scale, bpb, chords, bars, crisis_brass_deg, step_bars=2, vel=0.78),
    })
    roll_notes = []
    for b in range(3, bars, 4):
        base = b * bpb
        roll_beat = base + (bpb - 1.5 if bpb >= 4 else bpb - 1.0)
        roll_dur = 1.45 if bpb >= 4 else 0.95
        roll_notes.append([round(roll_beat, 3), max(36, min(52, drone_midi + 5)), round(roll_dur, 3), 0.84, "roll"])
        next_b = (b + 1) % bars
        if next_b > 0:
            roll_notes.append([round(next_b * bpb, 3), max(36, min(52, drone_midi)), 0.75, 0.88])
    tracks.append({
        "voice": "war_drum", "pan": -0.08, "gain": 0.32, "stem": 3, "humanizeMs": 5, "notes": roll_notes,
    })
    b_notes = [[round(b * bpb, 3), deg_to_midi(root, scale, d), 3.0, 0.80] for b, d in bell_bars if b < bars]
    tracks.append({
        "voice": "tubular_bell", "pan": 0.26, "gain": 0.26, "stem": 3, "humanizeMs": 4, "notes": b_notes,
    })

    return {
        "id": pid, "title": title, "category": "battle", "culture": culture,
        "bpm": bpm, "beatsPerBar": bpb, "bars": bars, "loop": True,
        "reverbWet": wet, "reverbDecay": decay, "masterGain": 0.72,
        "tracks": tracks,
    }


def build_field() -> list[dict]:
    pieces = []

    # 1. AZURE CROWN — Heroic D Dorian chivalric charge (french_horn lead, 24 bars @ 118 BPM = 48.8s)
    pieces.append(make_battle_piece(
        "battle_azure", "Lances of the Azure Crown", "azure", 118, 4, 24,
        62, "dorian", [0, 0, 5, 6, 0, 3, 4, 6],
        "contrabass_drone", 38,
        "war_drum", [(0.0, 38, 0.75, 0.78), (2.5, 45, 0.45, 0.56)],
        "french_horn", [
            (0, "0:2.5w 4:1.5 | 3:3.5w R:0.5"),
            (6, "4:2.5w 3:1.5 | 0:3.5w R:0.5"),
            (12, "4:2w 7:2w | 6:3.5w R:0.5"),
            (18, "4:2.5w 2:1.5 | 0:3.5w R:0.5"),
        ],
        "strings_spiccato", -1, OST_SPICCATO_4,
        "lute", -1, OST_MARCH_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "tambourine_riq", GROOVE_TAMB_ACCENT,
        "french_horn", [
            # A (bars 0..5): Heroic rising fifth fanfare
            (0, "0:1 0:0.5 2:0.5 4:1.5m 3:0.5 | 2:1 4:1 7:2w | 6:1.5 5:0.5 4:1 2:1 | 3:1.5 4:0.5 4:1.5 R:0.5 | 4:1 5:1 6:1.5g 4:0.5 | 3:1.5 2:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Answering fanfare reaching the octave
            (6, "0:1 2:0.5 3:0.5 4:1.5 5:0.5 | 7:1.5g 8:0.5 7:2w | 6:1 4:1 5:1.5m 3:0.5 | 4:1 2:1 0:1.5 R:0.5 | 2:1 3:1 4:1.5 2:0.5 | 1:1.5 0:0.5 -1:1.5 R:0.5"),
            # B (bars 12..17): Soaring crest over VI - VII - i
            (12, "4:1.5 4:0.5 7:1.5w 6:0.5 | 8:1.5g 7:0.5 6:2 | 7:1 6:0.5 5:0.5 4:1 6:1 | 7:3.5w R:0.5 | 8:1 7:1 6:1 4:1 | 5:1.5m 4:0.5 3:1.5 R:0.5"),
            # Coda (bars 18..23): Resolute return to root for seamless loop
            (18, "7:1.5 6:0.5 4:1 2:1 | 3:1.5m 4:0.5 5:2 | 4:1 3:0.5 2:0.5 0:1 -1:1 | 0:1.5 2:0.5 4:2w | 3:1 2:1 1:1 -1:1 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 38, 0.55, 0.86), (1.5, 45, 0.35, 0.66), (2.0, 38, 0.50, 0.80), (3.0, 45, 0.40, 0.74), (3.5, 38, 0.35, 0.70)],
        "low_brass", 4,
        4,
        [(0, 0), (6, 4), (12, 7), (18, 4), (22, 0)],
        wet=0.27, decay=1.8,
    ))

    # 2. CRIMSON COMPACT — Fierce E Phrygian pike-march (shawm lead, 24 bars @ 122 BPM = 47.2s)
    pieces.append(make_battle_piece(
        "battle_crimson", "Pikes of the Red Compact", "crimson", 122, 4, 24,
        64, "phrygian", [0, 1, 0, 6, 3, 1, 6, 0],
        "contrabass_drone", 40,
        "frame_drum", [(0.0, 43, 0.65, 0.80), (2.0, 50, 0.45, 0.58)],
        "low_brass", [
            (0, "0:2w 1:2w | 0:3.5w R:0.5"),
            (6, "4:2w 3:2 | 1:3.5w R:0.5"),
            (12, "4:2.5w 1:1.5 | 0:3.5w R:0.5"),
            (18, "1:2w 0:2 | -1:1.5 0:2w R:0.5"),
        ],
        "strings_spiccato", -1, OST_HEAVY_4,
        "harpsichord", -1, OST_SPICCATO_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "frame_drum", [(1.5, 57, 0.25, 0.60), (3.0, 50, 0.30, 0.66)],
        "shawm", [
            # A (bars 0..5): Biting Phrygian b2 reed theme
            (0, "0:1 0:0.5 1:0.5 0:1 3:1 | 4:1.5m 3:0.5 1:2 | 0:1 1:0.5 3:0.5 4:1 3:1 | 1:3.5w R:0.5 | 3:1 4:1 6:1.5g 4:0.5 | 3:1.5 1:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Pressing higher with mordent flourishes
            (6, "0:1 1:0.5 3:0.5 4:1.5m 6:0.5 | 7:1.5g 6:0.5 4:2 | 5:1 4:1 3:1.5m 1:0.5 | 3:1.5 1:0.5 0:1.5 R:0.5 | 1:1 3:1 4:1 3:1 | 1:1.5 0:0.5 -1:1.5 R:0.5"),
            # B (bars 12..17): Relentless halberd advance in upper register
            (12, "4:1 4:0.5 6:0.5 7:2w | 8:1.5m 7:0.5 6:1 4:1 | 6:1 7:1 6:1.5g 4:0.5 | 4:3.5w R:0.5 | 7:1 6:0.5 4:0.5 3:1 4:1 | 3:1.5 1:0.5 0:1.5 R:0.5"),
            # Coda (bars 18..23): Crushing descent to Phrygian tonic
            (18, "6:1.5 4:0.5 3:1 1:1 | 3:1.5m 1:0.5 0:2 | 1:1 3:1 4:1.5 3:0.5 | 1:1.5 0:0.5 -1:2 | 0:1 1:1 3:1 1:1 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 40, 0.55, 0.88), (1.0, 47, 0.35, 0.66), (2.0, 40, 0.52, 0.82), (3.0, 47, 0.38, 0.74), (3.5, 41, 0.34, 0.72)],
        "french_horn", 4,
        4,
        [(0, 0), (6, 4), (12, 7), (18, 1), (22, 0)],
        wet=0.24, decay=1.65,
    ))

    # 3. NORTHMARCH — Grim D Aeolian shield-wall fury (nyckelharpa lead, 24 bars @ 116 BPM = 49.7s)
    pieces.append(make_battle_piece(
        "battle_north", "Shield-Wall of the Northmarch", "northern", 116, 4, 24,
        62, "aeolian", [0, 6, 5, 6, 0, 2, 5, 6],
        "pipes_drone", 50,
        "war_drum", [(0.0, 38, 0.75, 0.82), (2.0, 45, 0.50, 0.60)],
        "war_horn", [
            (0, "0:3w -1:1 | 0:3.5w R:0.5"),
            (6, "4:2.5w 2:1.5 | 0:3.5w R:0.5"),
            (12, "4:3w 6:1 | 4:3.5w R:0.5"),
            (18, "2:2.5w -1:1.5 | 0:3.5w R:0.5"),
        ],
        "lute", -1, OST_MARCH_4,
        "strings_spiccato", -1, OST_SPICCATO_4,
        "frame_drum", GROOVE_FRAME_DRIVE,
        None, None,
        "nyckelharpa", [
            # A (bars 0..5): Driving Nordic bowed double-feeling motif with slides
            (0, "0:1.5s 2:0.5 4:1 4:1 | 3:1 2:0.5 0:0.5 -1:2 | 0:1 2:0.5 3:0.5 4:1.5m 6:0.5 | 4:3.5w R:0.5 | 5:1 4:0.5 2:0.5 3:1 2:1 | 0:1.5 -1:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Dane-axe answer climbing to the minor seventh
            (6, "0:1 2:0.5 4:0.5 6:1.5s 7:0.5 | 6:1 4:1 2:2 | 3:1 4:0.5 5:0.5 4:1 2:1 | 0:3.5w R:0.5 | 2:1 4:1 5:1.5g 4:0.5 | 2:1.5 0:0.5 -1:1.5 R:0.5"),
            # B (bars 12..17): Wild high-string battle surge
            (12, "4:1 6:0.5 7:0.5 7:1.5w 8:0.5 | 7:1 6:1 4:2 | 5:1 6:0.5 7:0.5 6:1 4:1 | 6:3.5w R:0.5 | 7:1 6:0.5 4:0.5 5:1 3:1 | 4:1.5m 2:0.5 4:1.5 R:0.5"),
            # Coda (bars 18..23): Iron descent back to D
            (18, "7:1.5s 6:0.5 4:1 2:1 | 3:1 2:1 0:2 | -1:1 0:1 2:1.5m 0:0.5 | 4:2w 2:2 | 0:1.5 -1:0.5 -2:1 -1:1 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 38, 0.60, 0.88), (1.0, 45, 0.35, 0.64), (2.0, 38, 0.55, 0.82), (2.75, 45, 0.28, 0.66), (3.0, 43, 0.42, 0.78)],
        "low_brass", 4,
        4,
        [(0, 0), (6, 4), (12, 7), (18, 2), (22, 0)],
        wet=0.28, decay=1.9,
    ))

    # 4. SUNWARD EMIRATES — Whirling D Hijaz scimitar assault (oud + shawm leads, 24 bars @ 126 BPM = 45.7s)
    pieces.append(make_battle_piece(
        "battle_sunward", "Scimitars of the Sunward Waste", "southern", 126, 4, 24,
        62, "hijaz", [0, 1, 0, 6, 3, 1, 4, 0],
        "contrabass_drone", 38,
        "frame_drum", [(0.0, 43, 0.60, 0.80), (2.5, 50, 0.40, 0.62)],
        "ney", [
            (0, "0:2.5w 1:0.5 2:1 | 1:3.5w R:0.5"),
            (6, "4:2.5w 3:1.5 | 2:3.5w R:0.5"),
            (12, "4:2w 6:2s | 4:3.5w R:0.5"),
            (18, "2:2s 1:2 | 0:3.5w R:0.5"),
        ],
        "oud", -1, OST_ARABESQUE_4,
        "qanun", -1, OST_MARCH_4,
        "darbuka", GROOVE_DARBUKA_WAR,
        "tambourine_riq", GROOVE_TAMB_ACCENT,
        "shawm", [
            # A (bars 0..5): Blazing Hijaz augmented-second reed call
            (0, "0:1 1:0.5 2:0.5 4:1.5m 3:0.5 | 2:1 1:0.5 2:0.5 4:2w | 3:1 2:0.5 1:0.5 2:1 0:1 | 1:3.5w R:0.5 | 0:1 2:1 3:1.5g 2:0.5 | 1:1.5 0:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Mirage charge leaping to the sixth
            (6, "2:1 3:0.5 4:0.5 6:1.5m 5:0.5 | 4:1 3:1 2:2 | 3:1 4:0.5 5:0.5 4:1 2:1 | 4:3.5w R:0.5 | 3:1 2:0.5 1:0.5 2:1 1:1 | 0:1.5 -1:0.5 0:1.5 R:0.5"),
            # B (bars 12..17): High desert horn-and-reed climax
            (12, "4:1 6:0.5 7:0.5 7:2w | 8:1.5m 7:0.5 6:1 4:1 | 5:1 6:1 4:1.5s 3:0.5 | 4:3.5w R:0.5 | 6:1 5:0.5 4:0.5 3:1 2:1 | 3:1.5 2:0.5 1:1.5 R:0.5"),
            # Coda (bars 18..23): Whirling cadence to tonic
            (18, "4:1 3:0.5 2:0.5 1:1 2:1 | 3:1.5m 2:0.5 1:2 | 0:1 1:0.5 2:0.5 4:1 3:1 | 2:1.5 1:0.5 0:2 | -1:1 0:1 2:1.5m 1:0.5 | 0:3.5w R:0.5"),
        ],
        "oud", [
            # Interlocking ornamental oud unison/heterophony & fills
            (0, "0:0.5 1:0.5 2:1 4:1 3:1 | 2:1.5m 1:0.5 0:2 | R:4 | 1:1 2:0.5 3:0.5 2:1 0:1 | R:4 | 1:1 2:1 0:1.5 R:0.5"),
            (6, "R:4 | 4:0.5 5:0.5 4:1 3:1 2:1 | R:4 | 4:1 3:0.5 2:0.5 1:1.5 R:0.5 | R:4 | 2:1 1:1 0:1.5 R:0.5"),
            (12, "4:0.5 4:0.5 6:1 7:1.5m 6:0.5 | R:4 | 5:0.5 6:0.5 5:1 4:1 3:1 | 4:1 2:1 4:1.5 R:0.5 | R:4 | 3:1 2:1 1:1.5 R:0.5"),
            (18, "4:0.5 3:0.5 2:0.5 1:0.5 2:1 0:1 | R:4 | 0:0.5 1:0.5 2:1 4:1 3:1 | R:4 | -1:1 0:0.5 1:0.5 2:1 1:1 | 0:3.5 R:0.5"),
        ],
        [(0.0, 38, 0.50, 0.86), (1.5, 45, 0.32, 0.70), (2.0, 38, 0.48, 0.80), (3.0, 45, 0.35, 0.74), (3.5, 38, 0.30, 0.68)],
        "french_horn", 4,
        4,
        [(0, 0), (6, 4), (12, 7), (18, 4), (22, 0)],
        wet=0.26, decay=1.75,
    ))

    # 5. KHAGANATE OF THE GRASS — Thundering E Minor-Pentatonic horse-archer gallop (morin_khuur lead, 24 bars @ 132 BPM = 43.6s)
    pieces.append(make_battle_piece(
        "battle_steppe", "Arrows of the Great Grass", "steppe", 132, 4, 24,
        59, "penta_min", [0, 0, 3, 4, 0, 2, 3, 4],
        "throat_drone", 40,
        "war_drum", [(0.0, 40, 0.65, 0.82), (2.0, 47, 0.45, 0.62)],
        "war_horn", [
            (0, "0:3w 3:1 | 2:3.5w R:0.5"),
            (6, "3:2.5w 5:1.5 | 3:3.5w R:0.5"),
            (12, "5:3w 4:1 | 3:3.5w R:0.5"),
            (18, "2:2.5w -1:1.5 | 0:3.5w R:0.5"),
        ],
        "lute", -1, OST_GALLOP_4,
        "strings_spiccato", -1, OST_GALLOP_4,
        "frame_drum", GROOVE_GALLOP_FRAME,
        None, None,
        "morin_khuur", [
            # A (bars 0..5): Horsehair bow gallop with expressive steppe slides
            (0, "0:1.5s 2:0.5 3:1.5 2:0.5 | 3:1.5s 5:0.5 4:2w | 3:1 2:0.5 0:0.5 -1:1 0:1 | 2:3.5w R:0.5 | 0:1.5 2:0.5 3:1 2:1 | 0:3.5w R:0.5"),
            # A' (bars 6..11): Feigned-retreat wheel and whistling volley
            (6, "2:1 3:0.5 4:0.5 5:2w | 6:1.5s 5:0.5 3:2 | 4:1 3:0.5 2:0.5 0:1 2:1 | 3:3.5w R:0.5 | 4:1.5 3:0.5 2:1 0:1 | -1:1.5 0:0.5 0:1.5 R:0.5"),
            # B (bars 12..17): High-steppe charge under Tengri
            (12, "5:1.5s 6:0.5 7:2w | 8:1.5g 7:0.5 5:2 | 6:1 5:0.5 4:0.5 3:1 5:1 | 5:3.5w R:0.5 | 7:1 5:1 4:1.5s 3:0.5 | 4:1.5 3:0.5 2:1.5 R:0.5"),
            # Coda (bars 18..23): Encircling sweep back to the root
            (18, "5:1.5 4:0.5 3:1 2:1 | 3:1.5s 2:0.5 0:2 | -1:1 0:0.5 2:0.5 3:1 2:1 | 0:2w -1:2 | 0:1.5 2:0.5 3:1 2:1 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 40, 0.48, 0.88), (0.75, 47, 0.25, 0.64), (1.0, 45, 0.36, 0.74),
         (2.0, 40, 0.48, 0.84), (2.75, 47, 0.25, 0.66), (3.0, 45, 0.38, 0.78)],
        "french_horn", 3,
        5,
        [(0, 0), (6, 3), (12, 5), (18, 3), (22, 0)],
        wet=0.27, decay=1.85,
    ))

    # 6. SALTMARSH LEAGUE — Precision C Dorian mercenary crossbow & pike Clockwork (cittern + fiddle leads, 24 bars @ 124 BPM = 46.5s)
    pieces.append(make_battle_piece(
        "battle_league", "Crossbows of the Lagoon", "league", 124, 4, 24,
        60, "dorian", [0, 4, 6, 3, 0, 2, 5, 4],
        "contrabass_drone", 36,
        "frame_drum", [(0.0, 43, 0.55, 0.78), (2.0, 50, 0.40, 0.60)],
        "tubular_bell", [
            (0, "0:3.5 R:0.5"), (4, "4:3.5 R:0.5"), (8, "0:3.5 R:0.5"),
            (12, "7:3.5 R:0.5"), (16, "4:3.5 R:0.5"), (20, "0:3.5 R:0.5"),
        ],
        "harpsichord", -1, OST_SPICCATO_4,
        "strings_spiccato", -1, OST_MARCH_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "tambourine_riq", GROOVE_TAMB_ACCENT,
        "fiddle", [
            # A (bars 0..5): Crisp Venetian-mercenary agility
            (0, "0:1 2:0.5 3:0.5 4:1 6:1 | 5:1.5m 4:0.5 2:2 | 3:1 4:0.5 5:0.5 4:1 2:1 | 4:3.5w R:0.5 | 6:1 5:0.5 4:0.5 3:1 2:1 | 1:1.5 2:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Ranked pavise volley response
            (6, "0:0.5 2:0.5 3:0.5 4:0.5 6:1.5g 4:0.5 | 7:1.5 6:0.5 4:2 | 5:1 4:1 3:1.5m 2:0.5 | 0:3.5w R:0.5 | 2:1 3:0.5 4:0.5 5:1 3:1 | 2:1.5 0:0.5 -1:1.5 R:0.5"),
            # B (bars 12..17): Soaring fiddle cantabile over crisp pikes
            (12, "7:1.5w 8:0.5 9:1 7:1 | 6:1.5m 5:0.5 4:2 | 5:1 6:0.5 7:0.5 8:1 6:1 | 7:3.5w R:0.5 | 8:1 7:0.5 6:0.5 5:1 4:1 | 5:1.5g 4:0.5 2:1.5 R:0.5"),
            # Coda (bars 18..23): Tight disciplined cadence
            (18, "7:1 6:0.5 4:0.5 3:1 2:1 | 4:1.5m 2:0.5 0:2 | -1:1 0:0.5 2:0.5 4:1 3:1 | 2:1.5 0:0.5 -1:2 | 0:1 2:0.5 3:0.5 4:1 1:1 | 0:3.5w R:0.5"),
        ],
        "cittern", [
            # Counter-rhythmic plucked wire riffs trading bars with fiddle
            (0, "0:0.5 4:0.5 7:1 4:1 2:1 | R:4 | 3:0.5 5:0.5 7:1 5:1 4:1 | 4:1 2:1 0:1.5 R:0.5 | R:4 | 1:1 2:1 0:1.5 R:0.5"),
            (6, "R:4 | 7:0.5 6:0.5 4:1 2:1 4:1 | R:4 | 0:1 2:1 4:1.5 R:0.5 | 2:0.5 3:0.5 4:1 5:1 3:1 | R:4"),
            (12, "7:0.5 4:0.5 7:1 9:1 7:1 | R:4 | 5:0.5 6:0.5 7:1 6:1 4:1 | 7:1 4:1 7:1.5 R:0.5 | R:4 | 5:1 4:1 2:1.5 R:0.5"),
            (18, "R:4 | 4:0.5 3:0.5 2:1 0:1 2:1 | -1:0.5 0:0.5 2:1 4:1 3:1 | R:4 | 0:0.5 2:0.5 4:1 2:1 1:1 | 0:3.5 R:0.5"),
        ],
        [(0.0, 36, 0.50, 0.86), (1.0, 43, 0.32, 0.64), (2.0, 36, 0.48, 0.80), (3.0, 43, 0.36, 0.74), (3.5, 48, 0.30, 0.68)],
        "french_horn", 4,
        4,
        [(0, 0), (6, 4), (12, 7), (18, 4), (22, 0)],
        wet=0.25, decay=1.7,
    ))

    # 7. ORDER OF THE ASHEN SUN — Solemn, inexorable A Phrygian crusade (war_horn lead, 24 bars @ 106 BPM = 54.3s)
    pieces.append(make_battle_piece(
        "battle_order", "Judgment of the Ashen Sun", "order", 106, 4, 24,
        57, "phrygian", [0, 1, 4, 0, 5, 3, 1, 0],
        "contrabass_drone", 33,
        "war_drum", [(0.0, 45, 0.85, 0.84), (2.0, 38, 0.65, 0.68)],
        "tubular_bell", [
            (0, "7:3.5 R:0.5"), (3, "11:3.5 R:0.5"), (6, "7:3.5 R:0.5"), (9, "8:3.5 R:0.5"),
            (12, "11:3.5 R:0.5"), (15, "7:3.5 R:0.5"), (18, "8:3.5 R:0.5"), (21, "7:3.5 R:0.5"),
        ],
        "strings_spiccato", 0, OST_HEAVY_4,
        "viols", 0, OST_MARCH_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "frame_drum", [(0.0, 43, 0.50, 0.72), (2.0, 50, 0.42, 0.64)],
        "war_horn", [
            # A (bars 0..5): Cataclysmic brass decree
            (0, "0:2w 1:1 0:1 | 4:2.5w 3:1.5 | 2:1.5 1:0.5 0:2 | -1:2 0:1.5 R:0.5 | 0:1.5 1:0.5 3:2w | 1:2 0:1.5 R:0.5"),
            # A' (bars 6..11): Rising judgment
            (6, "0:1.5 3:0.5 4:2w | 5:1.5 4:0.5 3:2 | 4:1.5 3:0.5 1:1 0:1 | 1:3.5w R:0.5 | 3:1.5 2:0.5 1:1 0:1 | -1:1.5 0:0.5 0:1.5 R:0.5"),
            # B (bars 12..17): Fanatical zenith on the octave
            (12, "4:2w 7:2w | 8:1.5 7:0.5 6:2 | 7:1.5 6:0.5 4:2w | 5:2 4:1.5 R:0.5 | 6:1.5 5:0.5 4:1 3:1 | 4:2w 3:1.5 R:0.5"),
            # Coda (bars 18..23): Iron benediction
            (18, "4:1.5 3:0.5 1:2 | 2:1.5 1:0.5 0:2 | 1:1.5 3:0.5 4:2w | 3:1.5 1:0.5 0:2 | 1:1.5 0:0.5 -1:2 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 45, 0.65, 0.90), (1.5, 40, 0.35, 0.66), (2.0, 45, 0.60, 0.84), (3.0, 46, 0.45, 0.78)],
        "low_brass", 4,
        7,
        [(0, 7), (4, 11), (8, 7), (12, 14), (16, 11), (20, 8), (22, 7)],
        wet=0.31, decay=2.15,
    ))

    return pieces


def build_special() -> list[dict]:
    pieces = []

    # 1. SIEGE — Battering rams & burning pitch in C Aeolian (shawm lead, 24 bars @ 104 BPM = 55.4s)
    pieces.append(make_battle_piece(
        "battle_siege", "Breach in the Curtain-Wall", "common", 104, 4, 24,
        60, "aeolian", [0, 5, 3, 6, 0, 2, 5, 6],
        "contrabass_drone", 36,
        "war_drum", [(0.0, 36, 0.90, 0.86), (2.0, 43, 0.70, 0.72)],
        "war_horn", [
            (0, "0:3w -1:1 | -3:3.5w R:0.5"),
            (6, "0:2.5w 2:1.5 | 4:3.5w R:0.5"),
            (12, "4:3w 3:1 | 2:3.5w R:0.5"),
            (18, "0:2.5w -1:1.5 | 0:3.5w R:0.5"),
        ],
        "strings_spiccato", -1, OST_HEAVY_4,
        "harpsichord", -1, OST_MARCH_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "frame_drum", [(0.0, 43, 0.50, 0.76), (1.5, 50, 0.30, 0.60), (3.0, 43, 0.45, 0.72)],
        "shawm", [
            # A (bars 0..5): Siege-tower horn & reed theme
            (0, "0:1.5 0:0.5 2:1 3:1 | 4:2w 2:1 0:1 | -1:1.5 0:0.5 2:2 | 0:3.5w R:0.5 | 2:1 3:1 4:1.5m 2:0.5 | 3:1.5 1:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Scaling ladders under boiling oil
            (6, "0:1 2:1 4:1.5m 5:0.5 | 6:1.5g 5:0.5 4:2 | 3:1 4:0.5 5:0.5 4:1 2:1 | 4:3.5w R:0.5 | 5:1 4:1 2:1.5 0:0.5 | -1:1.5 0:0.5 0:1.5 R:0.5"),
            # B (bars 12..17): The gate splinters—storming the breach
            (12, "4:1.5 6:0.5 7:2w | 8:1.5m 7:0.5 5:2 | 6:1 7:1 5:1.5g 4:0.5 | 4:3.5w R:0.5 | 7:1 6:0.5 5:0.5 4:1 2:1 | 3:1.5 2:0.5 1:1.5 R:0.5"),
            # Coda (bars 18..23): Grinding courtyard melee back to C
            (18, "5:1.5 4:0.5 2:1 0:1 | 3:1.5m 2:0.5 0:2 | -1:1 0:1 2:1.5 3:0.5 | 4:2w 2:2 | 1:1 0:1 -1:1.5 0:0.5 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 36, 0.70, 0.90), (1.0, 36, 0.40, 0.70), (2.0, 43, 0.65, 0.86), (3.0, 36, 0.50, 0.80)],
        "low_brass", 4,
        4,
        [(0, 0), (4, 4), (8, 0), (12, 7), (16, 4), (20, 0)],
        wet=0.29, decay=2.0,
    ))

    # 2. DUEL — Razored single-combat in 3/4 D Nikriz/Dorian-sharp4 (fiddle + oud leads, 32 bars @ 136 BPM = 42.4s)
    pieces.append(make_battle_piece(
        "battle_duel", "Circle of Naked Steel", "common", 136, 3, 32,
        62, "nikriz", [0, 0, 4, 3, 0, 1, 4, 0],
        "contrabass_drone", 38,
        "frame_drum", [(0.0, 43, 0.55, 0.80), (2.0, 57, 0.32, 0.62)],
        "oud", [
            (0, "0:1.5m 3:1 4:0.5 | 3:2.5 R:0.5"),
            (8, "4:1.5 3:1 2:0.5 | 0:2.5 R:0.5"),
            (16, "4:1.5m 6:1 4:0.5 | 3:2.5 R:0.5"),
            (24, "3:1.5 2:1 1:0.5 | 0:2.5 R:0.5"),
        ],
        "cittern", -1, OST_DUEL_3,
        "strings_spiccato", -1, [(0.0, 0, 0.42, 0.82), (1.0, 4, 0.36, 0.68), (2.0, 3, 0.38, 0.74)],
        "darbuka", GROOVE_DUEL_3,
        "tambourine_riq", [(1.0, 64, 0.22, 0.58), (2.5, 72, 0.20, 0.62)],
        "fiddle", [
            # A (bars 0..7): Feint, lunge and riposte in 3/4
            (0, "0:1 2:0.5 3:0.5 4:1 | 3:1.5m 2:0.5 0:1 | 1:1 2:1 3:1 | 4:2.5w R:0.5 | 6:1 4:0.5 3:0.5 2:1 | 3:1.5g 2:0.5 1:1 | 0:1 -1:1 0:1 | 2:2.5w R:0.5"),
            # A' (bars 8..15): Circling blades climbing higher
            (8, "0:0.5 1:0.5 2:0.5 3:0.5 4:1 | 6:1.5m 4:0.5 3:1 | 4:1 6:1 7:1 | 6:2.5w R:0.5 | 7:1 6:0.5 4:0.5 3:1 | 4:1.5 3:0.5 2:1 | 1:1 2:0.5 1:0.5 -1:1 | 0:2.5w R:0.5"),
            # B (bars 16..23): Breathless steel flurry at the octave
            (16, "7:1.5w 8:0.5 7:1 | 6:1 4:1 3:1 | 4:1 6:0.5 7:0.5 9:1 | 7:2.5w R:0.5 | 8:1 7:0.5 6:0.5 4:1 | 6:1.5m 4:0.5 3:1 | 4:1 3:1 2:1 | 4:2.5w R:0.5"),
            # Coda (bars 24..31): Killing bind & clean return to guard
            (24, "7:1 6:0.5 4:0.5 3:1 | 4:1.5m 3:0.5 2:1 | 3:1 2:0.5 1:0.5 0:1 | -1:2.5 R:0.5 | 0:1 1:0.5 2:0.5 3:1 | 4:1.5g 3:0.5 1:1 | 2:1 1:0.5 -1:0.5 0:1 | 0:2.5w R:0.5"),
        ],
        "oud", [
            # Counter-parry phrases answering fiddle across the 3/4 measure
            (0, "0:1.5 4:1.5 | R:3 | 1:1 2:0.5 3:0.5 1:1 | 4:1 3:1 2:1 | R:3 | 3:1 2:1 1:1 | 0:1.5 -1:1.5 | 2:1 0:1.5 R:0.5"),
            (8, "R:3 | 6:1 4:1 3:1 | R:3 | 6:1 4:0.5 3:0.5 4:1 | R:3 | 4:1 3:1 2:1 | 1:1.5 -1:1.5 | 0:2.5 R:0.5"),
            (16, "4:1 4:1 7:1 | R:3 | 4:0.5 6:0.5 7:1 6:1 | 7:1 4:1.5 R:0.5 | R:3 | 6:1 4:1 3:1 | 4:1.5 2:1.5 | 4:2.5 R:0.5"),
            (24, "4:1 3:1 2:1 | R:3 | 3:0.5 2:0.5 1:1 0:1 | -1:1 0:1.5 R:0.5 | 0:1.5 3:1.5 | R:3 | 2:1 1:1 -1:1 | 0:2.5 R:0.5"),
        ],
        [(0.0, 38, 0.50, 0.84), (2.0, 45, 0.36, 0.72)],
        "french_horn", 4,
        4,
        [(0, 0), (8, 4), (16, 7), (24, 4), (30, 0)],
        wet=0.24, decay=1.55,
    ))

    # 3. TOURNAMENT — Pageantry, splintered lances & cheering lists in G Mixolydian (french_horn lead, 24 bars @ 126 BPM = 45.7s)
    pieces.append(make_battle_piece(
        "battle_tournament", "Tilts of the Painted Lists", "common", 126, 4, 24,
        60, "mixolydian", [0, 6, 3, 4, 0, 5, 6, 4],
        "pipes_drone", 48,
        "frame_drum", [(0.0, 43, 0.55, 0.78), (2.0, 50, 0.40, 0.62)],
        "french_horn", [
            (0, "0:1.5 4:0.5 4:2w | R:4"),
            (6, "4:1.5 7:0.5 6:2w | R:4"),
            (12, "7:2w 4:2 | R:4"),
            (18, "4:1.5 2:0.5 0:2w | R:4"),
        ],
        "lute", -1, OST_GALLOP_4,
        "cittern", -1, OST_MARCH_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "tambourine_riq", [(0.5, 64, 0.20, 0.58), (1.5, 72, 0.20, 0.62), (2.5, 64, 0.20, 0.58), (3.5, 72, 0.22, 0.66)],
        "french_horn", [
            # A (bars 0..5): Bright Mixolydian jousting fanfare
            (0, "0:1 0:0.5 2:0.5 4:1.5 6:0.5 | 7:1.5g 6:0.5 4:2 | 3:1 4:1 5:1.5m 3:0.5 | 4:3.5w R:0.5 | 6:1 5:0.5 4:0.5 2:1 4:1 | 3:1.5 2:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Second pass at the barrier
            (6, "0:0.5 2:0.5 4:1 6:1.5m 7:0.5 | 8:1.5 7:0.5 6:2 | 7:1 6:0.5 4:0.5 5:1 3:1 | 2:3.5w R:0.5 | 3:1 4:0.5 5:0.5 6:1 4:1 | 2:1.5 1:0.5 0:1.5 R:0.5"),
            # B (bars 12..17): Grand melee of the knights
            (12, "7:1.5 7:0.5 9:2w | 8:1.5g 7:0.5 6:2 | 4:1 6:1 7:1.5 8:0.5 | 7:3.5w R:0.5 | 6:1 7:0.5 6:0.5 4:1 2:1 | 3:1.5m 4:0.5 4:1.5 R:0.5"),
            # Coda (bars 18..23): Victor's salute at the pavilion
            (18, "7:1 6:0.5 5:0.5 4:1 2:1 | 3:1.5 4:0.5 6:2 | 5:1 4:1 2:1 0:1 | -1:1.5 0:0.5 2:2 | 4:1 2:1 1:1 -1:1 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 36, 0.50, 0.86), (1.0, 43, 0.34, 0.68), (2.0, 36, 0.48, 0.82), (3.0, 43, 0.38, 0.76), (3.5, 48, 0.28, 0.68)],
        "low_brass", 4,
        4,
        [(0, 7), (6, 11), (12, 14), (18, 11), (22, 7)],
        wet=0.25, decay=1.65,
    ))

    # 4. BEASTS — Feral trolls, wolves & wyverns in C Locrian (shawm lead, 24 bars @ 128 BPM = 45.0s)
    pieces.append(make_battle_piece(
        "battle_beasts", "Fangs in the Thicket", "common", 128, 4, 24,
        60, "locrian", [0, 1, 4, 0, 3, 4, 1, 0],
        "throat_drone", 36,
        "frame_drum", [(0.0, 43, 0.55, 0.82), (1.5, 50, 0.35, 0.62), (2.5, 43, 0.45, 0.68)],
        "war_horn", [
            (0, "0:2.5s 1:1.5 | 0:3.5w R:0.5"),
            (6, "4:2.5s 3:1.5 | 1:3.5w R:0.5"),
            (12, "4:3w 1:1 | 0:3.5w R:0.5"),
            (18, "1:2.5s 0:1.5 | -1:1.5 0:2w R:0.5"),
        ],
        "strings_spiccato", -1, OST_GALLOP_4,
        "oud", -1, OST_HEAVY_4,
        "frame_drum", GROOVE_GALLOP_FRAME,
        "darbuka", [(0.75, 64, 0.22, 0.66), (2.5, 56, 0.28, 0.72), (3.5, 72, 0.22, 0.70)],
        "shawm", [
            # A (bars 0..5): Snarling diminished-fifth Locrian reed cry
            (0, "0:1 1:0.5 0:0.5 4:1.5s 3:0.5 | 1:1 0:1 -1:2 | 0:0.5 1:0.5 3:1 4:1.5m 1:0.5 | 0:3.5w R:0.5 | 1:1 3:0.5 4:0.5 3:1 1:1 | 0:1.5 -1:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Pack encirclement from the treeline
            (6, "0:0.5 1:0.5 3:0.5 4:0.5 5:1.5s 4:0.5 | 3:1 1:1 0:2 | 1:1 4:1 3:1.5m 1:0.5 | 4:3.5w R:0.5 | 5:1 4:0.5 3:0.5 1:1 0:1 | 1:1.5 0:0.5 -1:1.5 R:0.5"),
            # B (bars 12..17): Wyvern dive & troll sweep in upper register
            (12, "4:1 7:0.5 8:0.5 7:1.5s 5:0.5 | 4:1.5m 3:0.5 1:2 | 4:1 5:0.5 7:0.5 6:1 4:1 | 7:3.5w R:0.5 | 8:1 7:0.5 5:0.5 4:1 3:1 | 4:1.5s 1:0.5 3:1.5 R:0.5"),
            # Coda (bars 18..23): Savage thrash back to C
            (18, "5:1 4:0.5 3:0.5 1:1 4:1 | 3:1.5m 1:0.5 0:2 | -1:1 0:0.5 1:0.5 4:1 3:1 | 1:1.5 0:0.5 -2:2 | -1:1 0:1 1:1.5m 0:0.5 | 0:3.5w R:0.5"),
        ],
        None, None,
        [(0.0, 36, 0.52, 0.90), (0.75, 43, 0.26, 0.68), (1.5, 41, 0.36, 0.76),
         (2.0, 36, 0.50, 0.86), (3.0, 42, 0.42, 0.80), (3.5, 36, 0.30, 0.74)],
        "low_brass", 4,
        4,
        [(0, 7), (6, 11), (12, 14), (18, 8), (22, 7)],
        wet=0.25, decay=1.7,
    ))

    # 5. UNDEAD — Risen legions & bone-rattle dread in D Aeolian/Phrygian-inflected (viols + tubular_bell leads, 24 bars @ 108 BPM = 53.3s)
    pieces.append(make_battle_piece(
        "battle_undead", "March of the Unburied", "common", 108, 4, 24,
        62, "aeolian", [0, 5, 1, 6, 0, 3, 5, 6],
        "contrabass_drone", 38,
        "war_drum", [(0.0, 38, 0.80, 0.80), (2.5, 45, 0.45, 0.58)],
        "tubular_bell", [
            (0, "0:3.5 R:0.5"), (4, "4:3.5 R:0.5"), (8, "1:3.5 R:0.5"),
            (12, "5:3.5 R:0.5"), (16, "4:3.5 R:0.5"), (20, "0:3.5 R:0.5"),
        ],
        "harpsichord", -1, OST_SPICCATO_4,
        "strings_spiccato", -1, OST_HEAVY_4,
        "snare_march", GROOVE_SNARE_MARCH,
        "frame_drum", [(0.0, 50, 0.40, 0.70), (2.0, 57, 0.35, 0.64)],
        "viols", [
            # A (bars 0..5): Haunting bowed danse-macabre
            (0, "0:1.5w 2:0.5 1:1 0:1 | 4:2w 3:1 1:1 | 0:1.5 -1:0.5 -2:2 | -1:3.5w R:0.5 | 0:1 2:1 3:1.5s 2:0.5 | 1:1.5 0:0.5 0:1.5 R:0.5"),
            # A' (bars 6..11): Cold necromantic swell
            (6, "2:1.5 3:0.5 4:1.5w 5:0.5 | 4:1.5 2:0.5 1:2 | 0:1 2:1 4:1.5s 3:0.5 | 2:3.5w R:0.5 | 3:1 2:0.5 1:0.5 0:1 -1:1 | 0:3.5w R:0.5"),
            # B (bars 12..17): Wailing upper-string requiem
            (12, "4:2w 7:2w | 6:1.5s 5:0.5 4:2 | 5:1.5 4:0.5 2:1 4:1 | 5:3.5w R:0.5 | 7:1.5 6:0.5 4:1 2:1 | 3:1.5 2:0.5 1:1.5 R:0.5"),
            # Coda (bars 18..23): Back into the barrow-mist
            (18, "4:1.5 3:0.5 2:1 0:1 | 1:1.5s 0:0.5 -1:2 | 0:1 2:1 4:1.5w 2:0.5 | 1:2 -1:2 | 0:1.5 1:0.5 2:1 -1:1 | 0:3.5w R:0.5"),
        ],
        "tubular_bell", [
            # Tolling death-knell counter-melody locking into phrase peaks
            (0, "0:3.5 R:0.5 | R:4 | 4:3.5 R:0.5 | R:4 | 3:3.5 R:0.5 | 0:3.5 R:0.5"),
            (6, "2:3.5 R:0.5 | R:4 | 4:3.5 R:0.5 | R:4 | 1:3.5 R:0.5 | 0:3.5 R:0.5"),
            (12, "4:2 7:2 | R:4 | 5:3.5 R:0.5 | R:4 | 7:2 4:2 | 1:3.5 R:0.5"),
            (18, "4:3.5 R:0.5 | -1:3.5 R:0.5 | 0:3.5 R:0.5 | R:4 | 2:2 -1:2 | 0:3.5 R:0.5"),
        ],
        [(0.0, 38, 0.62, 0.86), (2.0, 38, 0.55, 0.78), (3.0, 45, 0.42, 0.72)],
        "low_brass", 4,
        4,
        [(0, 0), (6, 4), (12, 7), (18, 5), (22, 0)],
        wet=0.33, decay=2.3,
    ))

    # 6. HOLLOW CROWN — Apocalyptic Black-Flame endgame in D Hollow [0,1,3,6,7,8,11] (low_brass + shawm leads, 24 bars @ 112 BPM = 51.4s)
    pieces.append(make_battle_piece(
        "battle_hollow", "The Black Flame Unbound", "common", 112, 4, 24,
        50, "hollow", [0, 1, 3, 4, 0, 5, 3, 6],
        "throat_drone", 38,
        "war_drum", [(0.0, 38, 0.85, 0.86), (2.0, 44, 0.60, 0.68)],
        "low_brass", [
            (0, "0:2.5w 3:1.5 | 4:3.5w R:0.5"),
            (6, "4:2.5w 3:1.5 | 1:3.5w R:0.5"),
            (12, "4:2w 7:2w | 3:3.5w R:0.5"),
            (18, "3:2.5w 1:1.5 | 0:3.5w R:0.5"),
        ],
        "strings_spiccato", 0, OST_HEAVY_4,
        "harpsichord", 0, OST_ARABESQUE_4,
        "darbuka", GROOVE_DARBUKA_WAR,
        "snare_march", GROOVE_SNARE_MARCH,
        "low_brass", [
            # A (bars 0..5): Abyssal tritone-and-leading-tone brass monolith (MIDI 49..62)
            (0, "0:1.5w 1:0.5 3:2w | 4:1.5 3:0.5 1:2 | 0:1 3:1 4:1.5w 5:0.5 | 4:3.5w R:0.5 | 3:1.5 1:0.5 0:1 -1:1 | 0:3.5w R:0.5"),
            # A' (bars 6..11): Reality-warping ascent
            (6, "0:1 1:1 3:1.5w 4:0.5 | 5:1.5 4:0.5 3:2 | 4:1 5:1 7:2w | 6:3.5w R:0.5 | 5:1.5 4:0.5 3:1 1:1 | 0:3.5w R:0.5"),
            # B (bars 12..17): Crown of Ash cataclysm
            (12, "4:2w 7:2w | 8:1.5 7:0.5 5:2 | 4:1.5 3:0.5 4:1 7:1 | 6:3.5w R:0.5 | 7:1.5 5:0.5 4:1 3:1 | 4:2w 3:1.5 R:0.5"),
            # Coda (bars 18..23): Collapsing tritone cadence into D
            (18, "5:1.5 4:0.5 3:1 1:1 | 3:1.5 1:0.5 0:2 | -1:1 0:1 3:2w | 4:2 1:2 | 3:1 1:1 -1:1 0:1 | 0:3.5w R:0.5"),
        ],
        "shawm", [
            # Searing upper-octave shawm incantation (degrees +7..+15 -> MIDI 62..75)
            (0, "7:1 8:0.5 10:0.5 11:2w | R:4 | 7:1 10:0.5 11:0.5 12:1.5m 11:0.5 | 11:3.5w R:0.5 | R:4 | 8:1.5 7:0.5 7:1.5 R:0.5"),
            (6, "R:4 | 12:1 11:0.5 10:0.5 8:1 7:1 | 11:1 12:0.5 13:0.5 14:2w | 13:3.5w R:0.5 | R:4 | 8:1 6:1 7:1.5 R:0.5"),
            (12, "11:1.5 14:0.5 14:2w | 15:1.5m 14:0.5 12:2 | R:4 | 13:1.5s 11:0.5 10:1.5 R:0.5 | 14:1 12:1 11:1 10:1 | 11:2w 10:1.5 R:0.5"),
            (18, "12:1 11:0.5 10:0.5 8:1 10:1 | R:4 | 6:1 7:1 10:1.5m 11:0.5 | 11:2 8:2 | 10:1 8:1 6:1 7:1 | 7:3.5w R:0.5"),
        ],
        [(0.0, 38, 0.65, 0.90), (1.0, 44, 0.36, 0.72), (2.0, 38, 0.60, 0.86), (3.0, 44, 0.45, 0.80), (3.5, 39, 0.34, 0.74)],
        "french_horn", 11,
        11,
        [(0, 7), (4, 11), (8, 10), (12, 14), (16, 11), (20, 10), (22, 7)],
        wet=0.34, decay=2.4,
    ))

    return pieces


def validate_packs(packs: dict[str, list[dict]]) -> None:
    seen_ids: set[str] = set()
    for fname, pieces in packs.items():
        for p in pieces:
            pid = p["id"]
            assert pid not in seen_ids, f"Duplicate ID: {pid}"
            seen_ids.add(pid)
            assert p["category"] == "battle", f"{pid}: category must be battle"
            assert p["loop"] is True, f"{pid}: loop must be True"
            assert p["bars"] in (24, 32), f"{pid}: bars must be 24 or 32"
            assert abs(p["masterGain"] - 0.72) < 1e-6, f"{pid}: masterGain must be 0.72"
            dur_sec = p["bars"] * p["beatsPerBar"] * (60.0 / p["bpm"])
            assert dur_sec > 25.0, f"{pid}: DurationSec {dur_sec:.2f}s <= 25s"
            max_beat = p["bars"] * p["beatsPerBar"]
            stems_seen: set[int] = set()
            for t in p["tracks"]:
                assert t["voice"] in VALID_VOICES, f"{pid}: invalid voice {t['voice']}"
                assert 0.24 <= t["gain"] <= 0.46, f"{pid}: track gain {t['gain']} outside [0.24, 0.46]"
                assert -1.0 <= t["pan"] <= 1.0, f"{pid}: invalid pan {t['pan']}"
                assert t["stem"] in (0, 1, 2, 3), f"{pid}: invalid stem {t['stem']}"
                stems_seen.add(t["stem"])
                assert len(t["notes"]) > 0, f"{pid}: empty track {t['voice']}"
                for n in t["notes"]:
                    beat, midi, dur, vel = n[0], n[1], n[2], n[3]
                    assert 0.0 <= beat < max_beat, f"{pid} ({t['voice']}): beat {beat} out of [0, {max_beat})"
                    assert 18 <= midi <= 108, f"{pid} ({t['voice']}): midi {midi} out of 18..108"
                    assert dur > 0.04 and 0.05 <= vel <= 1.0, f"{pid} ({t['voice']}): bad dur/vel {n}"
                    if len(n) >= 5:
                        assert n[4] in VALID_ORNAMENTS, f"{pid}: invalid ornament {n[4]}"
            assert stems_seen == {0, 1, 2, 3}, f"{pid}: missing stems, got {stems_seen}"


if __name__ == "__main__":
    out_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Data", "music"))
    os.makedirs(out_dir, exist_ok=True)
    packs = {
        "battle_field.json": build_field(),
        "battle_special.json": build_special(),
    }
    validate_packs(packs)
    total = 0
    for fname, pieces in packs.items():
        path = os.path.join(out_dir, fname)
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"pieces": pieces}, f, separators=(",", ":"))
        total += len(pieces)
        ids = ", ".join(p["id"] for p in pieces)
        print(f"Wrote {fname}: {len(pieces)} pieces ({os.path.getsize(path) / 1024:.1f} KB) [{ids}]")
    print(f"Total AU5 battle pieces written: {total}")

