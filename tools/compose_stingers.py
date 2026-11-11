#!/usr/bin/env python3
"""
Deterministic score compiler for AU5 battle outcomes (5 pieces) and campaign stingers (7 pieces).
Generates Data/music/stingers.json with 12 hand-authored non-looped ("loop": false) scores
matching MusicScore.cs and AudioBank.cs voice IDs, note tuples, and ornaments.
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
    "locrian":    [0, 1, 3, 5, 6, 8, 10],
}

ORN = {"g": "grace", "m": "mordent", "s": "slide", "r": "roll", "w": "swell"}


def deg_to_midi(root: int, scale: list[int], deg: int) -> int:
    n = len(scale)
    octv, idx = divmod(deg, n)
    return root + scale[idx] + 12 * octv


def parse_phrase(root: int, scale: list[int], start_beat: float, tokens: str, vel_scale: float = 1.0) -> list[list]:
    """
    Tokens like '0:1.5m 2:0.5 4:2w R:1 -1:1g' or chromatic '+6:1.0w' (exact semitone offset from root).
      deg:dur[orn], +semi:dur[orn], or R:dur (rest).
      ornaments: g=grace, m=mordent, s=slide, r=roll, w=swell
    """
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
            if head.startswith("+"):
                semi = int(head[1:])
                midi = root + semi
                deg_est = semi // 2
            else:
                deg = int(head)
                midi = deg_to_midi(root, scale, deg)
                deg_est = deg
            vel = min(0.95, max(0.44, (0.74 + 0.04 * min(dur, 3.0) + 0.01 * max(-3, min(deg_est, 8))) * vel_scale))
            item = [round(beat, 3), midi, round(dur * 0.95, 3), round(vel, 3)]
            if orn:
                item.append(orn)
            out.append(item)
        beat += dur
    return out


def track(voice: str, pan: float, gain: float, notes: list[list], humanize_ms: float = 8.0, stem: int = 0) -> dict:
    notes_sorted = sorted(notes, key=lambda x: (x[0], x[1]))
    return {
        "voice": voice,
        "pan": round(pan, 3),
        "gain": round(gain, 3),
        "stem": stem,
        "humanizeMs": humanize_ms,
        "notes": notes_sorted,
    }


def score(
    pid: str, title: str, category: str, culture: str,
    bpm: float, bpb: int, bars: int,
    wet: float, decay: float, master: float,
    tracks: list[dict],
) -> dict:
    return {
        "id": pid,
        "title": title,
        "category": category,
        "culture": culture,
        "bpm": bpm,
        "beatsPerBar": bpb,
        "bars": bars,
        "loop": False,
        "reverbWet": wet,
        "reverbDecay": decay,
        "masterGain": master,
        "tracks": tracks,
    }


def build_outcomes() -> list[dict]:
    pieces = []

    # 1. victory_field — "The Field Is Ours"
    # D Mixolydian (D4=62), 96 BPM, 6 bars, 4/4 (24 beats = 15.0s)
    # snare/war_drum roll into triumphant french_horn + viols + tubular_bell
    sc_dmix = MODES["mixolydian"]
    vf_snare = [
        [0.0, 60, 1.85, 0.80, "roll"], [2.0, 60, 0.4, 0.72], [2.5, 67, 0.35, 0.62],
        [3.0, 60, 0.4, 0.78], [3.5, 60, 0.4, 0.84],
        [4.0, 60, 0.45, 0.82], [5.5, 67, 0.3, 0.56], [6.0, 60, 0.4, 0.70], [7.0, 60, 0.9, 0.72, "roll"],
        [8.0, 60, 0.45, 0.80], [9.5, 67, 0.3, 0.56], [10.0, 60, 0.4, 0.72], [11.5, 60, 0.35, 0.68],
        [12.0, 60, 0.45, 0.82], [13.5, 67, 0.3, 0.58], [14.0, 60, 0.4, 0.74], [15.0, 60, 0.9, 0.76, "roll"],
        [16.0, 60, 0.45, 0.84], [17.5, 67, 0.3, 0.60], [18.0, 60, 1.9, 0.84, "roll"],
        [20.0, 60, 0.6, 0.90], [20.75, 67, 0.35, 0.66], [21.0, 60, 0.6, 0.86],
    ]
    vf_drum = [
        [0.0, 38, 1.9, 0.78, "roll"], [2.0, 45, 0.9, 0.76], [3.0, 38, 0.9, 0.86],
        [4.0, 38, 1.0, 0.88], [6.0, 45, 0.8, 0.72], [7.5, 38, 0.45, 0.74],
        [8.0, 36, 1.0, 0.84], [10.0, 43, 0.8, 0.74], [11.5, 45, 0.45, 0.70],
        [12.0, 38, 1.0, 0.86], [14.0, 36, 0.9, 0.78], [15.0, 45, 0.9, 0.80, "roll"],
        [16.0, 38, 1.0, 0.88], [18.0, 45, 1.9, 0.84, "roll"],
        [20.0, 38, 2.8, 0.92],
    ]
    vf_horn = parse_phrase(
        62, sc_dmix, 1.0,
        "-3:1.0 0:1.0 4:1.0 | "
        "7:1.5g 6:0.5 4:1.0 2:1.0 | 6:1.5 5:0.5 4:2.0w | "
        "3:1.0 4:1.0 6:1.5m 7:0.5 | 8:1.5 7:0.5 4:2.0w | "
        "6:1.0 4:1.0 2:1.0 4:1.0 | 7:3.6w",
        vel_scale=1.04,
    )
    vf_viols = (
        parse_phrase(50, sc_dmix, 0.0, "0:2.0w 4:2.0 | 0:2.0 4:2.0 | 6:2.0 3:2.0 | 3:2.0 4:2.0 | 0:2.0 6:2.0 | 6:2.0 4:2.0 | 0:3.8w", 0.92)
        + parse_phrase(50, sc_dmix, 4.0, "9:2.0w 7:2.0 | 6:2.0 7:2.0 | 7:2.0 9:2.0 | 9:2.0 6:2.0 | 6:2.0 9:2.0 | 9:3.8w", 0.86)[0:10]
    )
    # Clean explicit viols two-voice accompaniment across 24 beats
    vf_viols = (
        parse_phrase(50, sc_dmix, 0.0, "0:2.0w 4:2.0 | 0:2.0 4:2.0 | 6:2.0 3:2.0 | 3:2.0 4:2.0 | 0:2.0 6:2.0 | 0:3.8w", 0.92)
        + parse_phrase(57, sc_dmix, 0.0, "0:2.0w 2:2.0 | 4:2.0 2:2.0 | 3:2.0 2:2.0 | 0:2.0 2:2.0 | 4:2.0 3:2.0 | 4:3.8w", 0.84)
    )
    vf_bell = [
        [4.0, 74, 3.4, 0.78], [8.0, 72, 3.2, 0.72], [12.0, 74, 3.4, 0.76],
        [16.0, 69, 3.2, 0.74], [20.0, 74, 3.8, 0.86],
    ]
    pieces.append(score(
        "victory_field", "The Field Is Ours", "outcome", "common", 96, 4, 6, 0.30, 2.1, 0.86,
        [
            track("french_horn", 0.14, 0.58, vf_horn, 10),
            track("viols", -0.20, 0.40, vf_viols, 12),
            track("snare_march", -0.10, 0.38, vf_snare, 6),
            track("war_drum", 0.06, 0.48, vf_drum, 7),
            track("tubular_bell", 0.26, 0.28, vf_bell, 5),
        ],
    ))

    # 2. victory_heroic — "A Day for the Chronicle"
    # D Ionian/Lydian (D4=62, with +6=G#4 and +18=G#5 Lydian inflection), 92 BPM, 8 bars, 4/4 (32 beats = 20.87s)
    # choir_aah + french_horn + low_brass + war_drum + tubular_bell
    sc_dion = MODES["ionian"]
    vh_horn = parse_phrase(
        62, sc_dion, 0.0,
        "0:1.5 4:0.5 7:2.0w | 6:1.5 5:0.5 4:2.0 | "
        "2:1.0 +6:1.0 4:1.5g 7:0.5 | +18:1.5 7:0.5 9:2.0w | "
        "9:1.5 8:0.5 7:1.0 4:1.0 | 5:1.5 6:0.5 7:2.0w | "
        "9:1.5 7:0.5 +18:1.0 9:1.0 | 7:3.8w",
        vel_scale=1.05,
    )
    vh_choir = (
        parse_phrase(62, sc_dion, 0.0, "2:4.0w | 1:4.0w | +6:4.0w | 4:4.0w | 5:4.0w | 4:4.0w | 5:2.0 6:2.0 | 7:3.8w", 0.90)
        + parse_phrase(62, sc_dion, 0.0, "7:4.0w | 4:4.0w | 9:4.0w | 7:4.0w | 9:4.0w | 7:4.0w | 9:2.0 9:2.0 | 9:3.8w", 0.82)
    )
    vh_brass = parse_phrase(
        38, sc_dion, 0.0,
        "0:2.0w 4:2.0 | 5:2.0 3:2.0 | 1:2.0 4:2.0 | 0:2.0 4:2.0 | "
        "3:2.0 4:2.0 | 5:2.0 3:2.0 | 3:2.0 4:2.0 | 0:3.8w",
        vel_scale=0.95,
    )
    vh_drum = [
        [0.0, 38, 1.2, 0.84], [3.0, 45, 0.9, 0.68, "roll"],
        [4.0, 38, 1.0, 0.78], [6.0, 43, 0.8, 0.66],
        [8.0, 40, 1.2, 0.80], [11.0, 45, 0.9, 0.72, "roll"],
        [12.0, 38, 1.2, 0.84], [14.0, 45, 1.9, 0.82, "roll"],
        [16.0, 43, 1.2, 0.86], [18.0, 45, 1.0, 0.78],
        [20.0, 38, 1.0, 0.82], [22.0, 43, 1.0, 0.76],
        [24.0, 43, 1.0, 0.84], [26.0, 45, 1.9, 0.88, "roll"],
        [28.0, 38, 3.2, 0.92],
    ]
    vh_bell = [
        [0.0, 74, 3.6, 0.78], [8.0, 76, 3.6, 0.76], [16.0, 74, 3.6, 0.82],
        [24.0, 69, 3.4, 0.78], [28.0, 74, 3.8, 0.88],
    ]
    pieces.append(score(
        "victory_heroic", "A Day for the Chronicle", "outcome", "common", 92, 4, 8, 0.36, 2.6, 0.86,
        [
            track("french_horn", 0.15, 0.56, vh_horn, 10),
            track("choir_aah", -0.16, 0.40, vh_choir, 14),
            track("low_brass", 0.04, 0.44, vh_brass, 9),
            track("war_drum", 0.0, 0.46, vh_drum, 7),
            track("tubular_bell", 0.26, 0.28, vh_bell, 5),
        ],
    ))

    # 3. victory_duel — "First Blood and Honour"
    # G Mixolydian (G3=55, G4=67), 104 BPM, 5 bars, 4/4 (20 beats = 11.54s)
    # french_horn + fiddle + lute + frame_drum
    sc_gmix = MODES["mixolydian"]
    vd_fiddle = parse_phrase(
        67, sc_gmix, 0.0,
        "0:0.5 2:0.5 4:1.0m 5:0.5 4:0.5 2:1.0 | "
        "6:1.0g 5:0.5 4:0.5 3:1.0 4:1.0 | "
        "7:1.5m 6:0.5 4:1.0 2:1.0 | "
        "3:0.5 4:0.5 5:1.0g 4:1.0 2:1.0 | "
        "7:3.6w",
        vel_scale=1.02,
    )
    vd_horn = parse_phrase(
        55, sc_gmix, 0.0,
        "0:1.5 4:0.5 7:2.0w | "
        "6:2.0 3:2.0 | "
        "7:2.0w 6:1.0 4:1.0 | "
        "3:1.5 4:0.5 6:2.0 | "
        "7:3.6w",
        vel_scale=0.98,
    )
    vd_lute = []
    duel_chords = [0, 6, 0, 3, 0]
    arp_pat = [(0.0, 0, 1.1, 0.80), (0.5, 4, 0.8, 0.62), (1.0, 7, 1.0, 0.72), (1.5, 4, 0.8, 0.58),
               (2.0, 2, 1.1, 0.74), (2.5, 4, 0.8, 0.60), (3.0, 7, 1.0, 0.70), (3.5, 4, 0.8, 0.60)]
    for bar, ch in enumerate(duel_chords):
        base = bar * 4.0
        if bar < 4:
            for off, doff, dur, vel in arp_pat:
                vd_lute.append([round(base + off, 3), deg_to_midi(43, sc_gmix, ch + doff), dur, vel])
        else:
            vd_lute.extend([
                [16.0, 43, 3.4, 0.84], [16.08, 50, 3.3, 0.78], [16.16, 59, 3.2, 0.80], [16.24, 67, 3.1, 0.84],
            ])
    vd_drum = []
    for bar in range(4):
        b = bar * 4.0
        vd_drum.extend([
            [b + 0.0, 43, 0.45, 0.82], [b + 1.0, 57, 0.3, 0.58], [b + 1.5, 64, 0.25, 0.52],
            [b + 2.0, 50, 0.4, 0.74], [b + 3.0, 57, 0.3, 0.62], [b + 3.5, 64, 0.25, 0.58],
        ])
    vd_drum.extend([[16.0, 43, 0.8, 0.88], [16.5, 57, 0.35, 0.66], [17.0, 43, 1.2, 0.84]])
    pieces.append(score(
        "victory_duel", "First Blood and Honour", "outcome", "common", 104, 4, 5, 0.26, 1.7, 0.86,
        [
            track("fiddle", 0.20, 0.52, vd_fiddle, 9),
            track("french_horn", -0.16, 0.50, vd_horn, 10),
            track("lute", -0.24, 0.46, vd_lute, 8),
            track("frame_drum", 0.08, 0.44, vd_drum, 6),
        ],
    ))

    # 4. defeat_field — "Banners in the Mud"
    # D Aeolian (D3=50, D4=62), 60 BPM, 5 bars, 4/4 (20 beats = 20.0s)
    # falling war_horn + viols + slow war_drum
    sc_daeo = MODES["aeolian"]
    df_horn = parse_phrase(
        62, sc_daeo, 0.0,
        "4:2.5w 2:1.5 | 3:2.0s 2:1.0 0:1.0 | "
        "5:2.5w 4:1.5 | 2:1.5 1:0.5 0:1.0 -2:1.0 | "
        "-7:3.8w",
        vel_scale=0.94,
    )
    df_viols = (
        parse_phrase(50, sc_daeo, 0.0, "0:4.0w | -2:2.0 0:2.0 | -4:2.0 -3:2.0 | -2:2.0 -1:2.0 | 0:3.8w", 0.88)
        + parse_phrase(50, sc_daeo, 0.0, "2:4.0w | 2:2.0 4:2.0 | 3:2.0 4:2.0 | 2:2.0 1:2.0 | 4:3.8w", 0.80)
    )
    df_drum = [
        [0.0, 36, 1.6, 0.78], [2.5, 41, 0.9, 0.50],
        [4.0, 36, 1.6, 0.74], [6.5, 41, 0.9, 0.48],
        [8.0, 36, 1.6, 0.76], [10.5, 41, 0.9, 0.48],
        [12.0, 36, 1.6, 0.72], [14.5, 41, 0.9, 0.44],
        [16.0, 36, 2.4, 0.74],
    ]
    pieces.append(score(
        "defeat_field", "Banners in the Mud", "outcome", "common", 60, 4, 5, 0.38, 2.7, 0.84,
        [
            track("war_horn", 0.12, 0.54, df_horn, 12),
            track("viols", -0.18, 0.44, df_viols, 14),
            track("war_drum", 0.04, 0.46, df_drum, 8),
        ],
    ))

    # 5. defeat_fallen — "Ashes and Crows"
    # D Phrygian (D3=50, D4=62), 56 BPM, 6 bars, 4/4 (24 beats = 25.71s)
    # choir_aah dirge + contrabass_drone + tubular_bell + war_drum
    sc_dphr = MODES["phrygian"]
    dfl_choir_hi = parse_phrase(
        62, sc_dphr, 0.0,
        "4:2.5w 5:1.5 | 4:2.0 2:2.0 | "
        "7:2.5w 5:1.5 | 4:2.0 3:1.0 2:1.0 | "
        "1:2.5w 2:1.5 | 0:3.8w",
        vel_scale=0.92,
    )
    dfl_choir_lo = parse_phrase(
        50, sc_dphr, 0.0,
        "7:4.0w | 5:2.0 7:2.0 | "
        "9:2.0w 7:2.0 | 5:2.0 4:2.0 | "
        "5:2.0 4:2.0 | 4:3.8w",
        vel_scale=0.84,
    )
    dfl_drone = [
        [0.0, 38, 7.85, 0.80, "swell"],
        [8.0, 34, 3.85, 0.78, "swell"],
        [12.0, 39, 3.85, 0.80, "swell"],
        [16.0, 38, 7.85, 0.82, "swell"],
    ]
    dfl_bell = [
        [0.0, 62, 3.8, 0.74], [8.0, 69, 3.8, 0.72],
        [16.0, 63, 3.8, 0.74], [20.0, 62, 3.8, 0.78],
    ]
    dfl_drum = [
        [0.0, 36, 1.8, 0.72], [4.0, 36, 1.6, 0.66],
        [8.0, 36, 1.8, 0.74], [12.0, 36, 1.6, 0.68], [14.5, 41, 0.9, 0.46],
        [16.0, 36, 1.8, 0.72], [20.0, 36, 2.5, 0.76],
    ]
    pieces.append(score(
        "defeat_fallen", "Ashes and Crows", "outcome", "common", 56, 4, 6, 0.42, 3.0, 0.84,
        [
            track("choir_aah", 0.16, 0.48, dfl_choir_hi, 14),
            track("choir_aah", -0.18, 0.38, dfl_choir_lo, 15),
            track("contrabass_drone", 0.0, 0.38, dfl_drone, 6),
            track("tubular_bell", 0.26, 0.28, dfl_bell, 5),
            track("war_drum", -0.06, 0.42, dfl_drum, 8),
        ],
    ))

    return pieces


def build_stingers() -> list[dict]:
    pieces = []

    # 1. sting_war — D Phrygian, war_horn + low_brass + war_drum + tubular_bell
    # 108 BPM, 3 bars, 4/4 -> 12 beats = 6.67s
    sc_dphr = MODES["phrygian"]
    sw_horn = parse_phrase(
        62, sc_dphr, 0.0,
        "0:1.5 0:0.5 4:2.0w | 5:1.5 4:0.5 1:2.0s | 0:3.6w",
        vel_scale=1.06,
    )
    sw_brass = (
        parse_phrase(38, sc_dphr, 0.0, "0:2.0 1:2.0 | -2:2.0 1:2.0 | 0:3.6w", 0.98)
        + parse_phrase(50, sc_dphr, 0.0, "4:2.0 5:2.0 | 5:2.0 4:2.0 | 4:3.6w", 0.86)
    )
    sw_drum = [
        [0.0, 38, 0.9, 0.88], [1.5, 43, 0.4, 0.68], [2.0, 36, 0.9, 0.82], [3.0, 45, 0.95, 0.82, "roll"],
        [4.0, 38, 0.9, 0.88], [5.0, 43, 0.5, 0.70], [6.0, 39, 0.9, 0.84], [7.0, 45, 0.95, 0.86, "roll"],
        [8.0, 38, 0.6, 0.92], [8.5, 36, 2.6, 0.90],
    ]
    sw_bell = [
        [0.0, 62, 3.4, 0.80], [4.0, 63, 3.2, 0.78], [8.0, 62, 3.6, 0.86],
    ]
    pieces.append(score(
        "sting_war", "Iron Drawn", "stinger", "common", 108, 4, 3, 0.32, 2.2, 0.88,
        [
            track("war_horn", 0.14, 0.58, sw_horn, 8),
            track("low_brass", -0.16, 0.48, sw_brass, 8),
            track("war_drum", 0.0, 0.52, sw_drum, 6),
            track("tubular_bell", 0.26, 0.30, sw_bell, 5),
        ],
    ))

    # 2. sting_peace — G Ionian, harp + french_horn + recorder + tubular_bell
    # 96 BPM, 3 bars, 3/4 -> 9 beats = 5.625s
    sc_gion = MODES["ionian"]
    sp_harp = [
        [0.0, 43, 1.6, 0.80], [0.5, 50, 1.4, 0.68], [1.0, 55, 1.2, 0.72], [1.5, 59, 1.0, 0.68], [2.0, 62, 1.0, 0.74],
        [3.0, 48, 1.6, 0.78], [3.5, 55, 1.2, 0.66], [4.0, 60, 1.2, 0.72], [4.5, 50, 1.2, 0.70], [5.0, 57, 1.0, 0.68], [5.5, 66, 0.9, 0.72],
        [6.0, 43, 2.8, 0.82], [6.1, 50, 2.7, 0.74], [6.2, 59, 2.6, 0.78], [6.3, 67, 2.5, 0.82],
    ]
    sp_horn = parse_phrase(55, sc_gion, 0.0, "0:2.0w 2:1.0 | 3:1.5 4:1.5 | 4:2.8w", 0.94)
    sp_rec = parse_phrase(67, sc_gion, 0.0, "4:1.5g 2:0.5 0:1.0 | 3:1.5m 2:0.5 1:1.0 | 2:2.8w", 0.96)
    sp_bell = [[0.0, 67, 3.0, 0.72], [6.0, 79, 2.8, 0.78]]
    pieces.append(score(
        "sting_peace", "Olive and Wax", "stinger", "common", 96, 3, 3, 0.36, 2.4, 0.85,
        [
            track("harp", -0.22, 0.50, sp_harp, 8),
            track("french_horn", -0.10, 0.46, sp_horn, 10),
            track("recorder", 0.18, 0.48, sp_rec, 10),
            track("tubular_bell", 0.28, 0.26, sp_bell, 5),
        ],
    ))

    # 3. sting_rank — D Mixolydian, french_horn + viols + war_drum
    # 112 BPM, 3 bars, 4/4 -> 12 beats = 6.43s
    sc_dmix = MODES["mixolydian"]
    sr_horn = parse_phrase(
        62, sc_dmix, 0.0,
        "-3:1.0 0:1.0 2:1.0 4:1.0 | 6:1.5g 5:0.5 4:1.0 6:1.0 | 7:3.6w",
        vel_scale=1.04,
    )
    sr_viols = (
        parse_phrase(50, sc_dmix, 0.0, "0:2.0w 4:2.0 | 6:2.0 4:2.0 | 0:3.6w", 0.92)
        + parse_phrase(50, sc_dmix, 0.0, "4:2.0w 7:2.0 | 9:2.0 9:2.0 | 9:3.6w", 0.84)
    )
    sr_drum = [
        [0.0, 38, 0.8, 0.84], [0.75, 45, 0.35, 0.62], [1.0, 45, 0.5, 0.70], [2.0, 38, 0.8, 0.78], [3.0, 45, 0.95, 0.76, "roll"],
        [4.0, 36, 0.8, 0.82], [6.0, 45, 0.8, 0.76], [7.0, 45, 0.95, 0.84, "roll"],
        [8.0, 38, 2.6, 0.90],
    ]
    pieces.append(score(
        "sting_rank", "Spurs and Banner", "stinger", "common", 112, 4, 3, 0.30, 2.0, 0.86,
        [
            track("french_horn", 0.14, 0.58, sr_horn, 9),
            track("viols", -0.18, 0.44, sr_viols, 11),
            track("war_drum", 0.04, 0.48, sr_drum, 6),
        ],
    ))

    # 4. sting_marriage — F Lydian (F4=65), harp + lute + recorder + tubular_bell
    # 96 BPM, 3 bars, 3/4 -> 9 beats = 5.625s
    sc_flyd = MODES["lydian"]
    sm_harp = [
        [0.0, 53, 1.6, 0.80], [0.5, 60, 1.3, 0.68], [1.0, 65, 1.2, 0.74], [2.0, 69, 1.0, 0.70],
        [3.0, 55, 1.6, 0.78], [3.5, 62, 1.3, 0.68], [4.0, 71, 1.2, 0.76], [5.0, 67, 1.0, 0.70],
        [6.0, 53, 2.8, 0.82], [6.1, 60, 2.7, 0.74], [6.2, 69, 2.6, 0.78], [6.3, 77, 2.5, 0.82],
    ]
    sm_lute = [
        [0.0, 41, 1.5, 0.76], [1.0, 53, 1.0, 0.68], [1.5, 57, 0.8, 0.64], [2.0, 60, 1.0, 0.70],
        [3.0, 43, 1.5, 0.76], [4.0, 55, 1.0, 0.68], [4.5, 59, 0.8, 0.68], [5.0, 60, 1.0, 0.72],
        [6.0, 41, 2.6, 0.80], [6.12, 53, 2.5, 0.74], [6.24, 65, 2.4, 0.78],
    ]
    sm_rec = parse_phrase(65, sc_flyd, 0.0, "0:1.0 2:1.0 3:1.0g | 4:1.5m 3:0.5 2:1.0 | 4:2.8w", 0.96)
    sm_bell = [[0.0, 77, 2.8, 0.70], [3.0, 72, 2.6, 0.66], [6.0, 77, 2.8, 0.76]]
    pieces.append(score(
        "sting_marriage", "Ring and Garland", "stinger", "common", 96, 3, 3, 0.34, 2.3, 0.85,
        [
            track("harp", -0.24, 0.48, sm_harp, 8),
            track("lute", 0.22, 0.44, sm_lute, 8),
            track("recorder", 0.08, 0.48, sm_rec, 10),
            track("tubular_bell", -0.12, 0.24, sm_bell, 5),
        ],
    ))

    # 5. sting_oath — E Dorian (E3=52, E4=64), choir_aah + french_horn + tubular_bell
    # 80 BPM, 2 bars, 4/4 -> 8 beats = 6.00s
    sc_edor = MODES["dorian"]
    so_choir = (
        parse_phrase(64, sc_edor, 0.0, "0:2.0w -2:2.0 | -1:1.5 0:2.4w", 0.92)
        + parse_phrase(64, sc_edor, 0.0, "4:2.0w 5:2.0 | 3:1.5 4:2.4w", 0.84)
    )
    so_horn = parse_phrase(52, sc_edor, 0.0, "0:1.5 4:0.5 5:1.0 6:1.0 | 7:1.5g 8:0.5 7:1.9w", 1.02)
    so_bell = [[0.0, 64, 3.6, 0.78], [4.0, 71, 2.4, 0.72], [6.0, 64, 2.2, 0.82]]
    pieces.append(score(
        "sting_oath", "Sworn Upon Steel", "stinger", "common", 80, 4, 2, 0.40, 2.8, 0.86,
        [
            track("choir_aah", -0.16, 0.46, so_choir, 13),
            track("french_horn", 0.14, 0.54, so_horn, 10),
            track("tubular_bell", 0.24, 0.30, so_bell, 5),
        ],
    ))

    # 6. sting_omen — C Locrian (C3=48, Gb3=54, C4=60, Gb4=66), tubular_bell tritone + throat_drone + contrabass_drone
    # 76 BPM, 2 bars, 4/4 -> 8 beats = 6.32s
    som_bell = [
        [0.0, 60, 3.6, 0.84], [1.5, 66, 3.6, 0.82],
        [3.5, 73, 2.8, 0.76], [4.0, 66, 3.4, 0.80],
        [5.5, 60, 2.6, 0.84],
    ]
    som_throat = [
        [0.0, 48, 3.6, 0.82, "swell"],
        [3.5, 49, 2.2, 0.84, "slide"],
        [5.5, 42, 2.4, 0.80, "swell"],
    ]
    som_bass = [
        [0.0, 36, 7.8, 0.86, "swell"],
        [2.0, 42, 5.8, 0.74, "swell"],
    ]
    pieces.append(score(
        "sting_omen", "Star of Ash", "stinger", "common", 76, 4, 2, 0.44, 3.2, 0.86,
        [
            track("tubular_bell", 0.18, 0.44, som_bell, 6),
            track("throat_drone", -0.16, 0.48, som_throat, 8),
            track("contrabass_drone", 0.0, 0.46, som_bass, 6),
        ],
    ))

    # 7. sting_found_order — D Ionian (D4=62), choir_aah + french_horn + harp + war_drum
    # 108 BPM, 3 bars, 4/4 -> 12 beats = 6.67s
    sc_dion = MODES["ionian"]
    sfo_horn = parse_phrase(
        62, sc_dion, 0.0,
        "0:1.0 2:1.0 4:1.5g 7:0.5 | 5:1.0 7:1.0 8:1.0 6:1.0 | 7:3.6w",
        vel_scale=1.04,
    )
    sfo_choir = (
        parse_phrase(62, sc_dion, 0.0, "2:4.0w | 3:2.0 4:2.0 | 2:3.6w", 0.90)
        + parse_phrase(62, sc_dion, 0.0, "4:4.0w | 5:2.0 6:2.0 | 7:3.6w", 0.84)
    )
    sfo_harp = [
        [0.0, 50, 1.5, 0.78], [0.5, 57, 1.2, 0.66], [1.0, 62, 1.2, 0.72], [1.5, 66, 1.0, 0.68],
        [2.0, 50, 1.5, 0.76], [2.5, 57, 1.2, 0.66], [3.0, 69, 1.0, 0.72], [3.5, 66, 0.9, 0.66],
        [4.0, 43, 1.5, 0.78], [4.5, 50, 1.2, 0.68], [5.0, 59, 1.0, 0.72], [5.5, 62, 0.9, 0.68],
        [6.0, 45, 1.5, 0.80], [6.5, 52, 1.2, 0.68], [7.0, 61, 1.0, 0.74], [7.5, 64, 0.9, 0.70],
        [8.0, 50, 3.6, 0.84], [8.1, 57, 3.5, 0.76], [8.2, 66, 3.4, 0.80], [8.3, 74, 3.3, 0.84],
    ]
    sfo_drum = [
        [0.0, 38, 0.9, 0.82], [2.0, 45, 0.8, 0.68],
        [4.0, 43, 0.9, 0.78], [6.0, 45, 0.8, 0.74], [7.0, 45, 0.95, 0.84, "roll"],
        [8.0, 38, 2.8, 0.90],
    ]
    pieces.append(score(
        "sting_found_order", "The Chapter Founded", "stinger", "common", 108, 4, 3, 0.36, 2.5, 0.86,
        [
            track("french_horn", 0.14, 0.56, sfo_horn, 9),
            track("choir_aah", -0.16, 0.42, sfo_choir, 13),
            track("harp", -0.24, 0.44, sfo_harp, 8),
            track("war_drum", 0.04, 0.46, sfo_drum, 6),
        ],
    ))

    return pieces


def main() -> None:
    out_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Data", "music"))
    os.makedirs(out_dir, exist_ok=True)
    pieces = build_outcomes() + build_stingers()
    path = os.path.join(out_dir, "stingers.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"pieces": pieces}, f, separators=(",", ":"))
    print(f"Wrote stingers.json: {len(pieces)} pieces ({os.path.getsize(path) / 1024:.1f} KB)")
    for p in pieces:
        dur = p["bars"] * p["beatsPerBar"] * 60.0 / p["bpm"]
        voices = ",".join(dict.fromkeys(t["voice"] for t in p["tracks"]))
        print(f"  {p['id']:18s} [{p['category']:7s}] {dur:5.2f}s ({p['bars']} bars @ {p['bpm']} BPM, {p['beatsPerBar']}/4) -> {voices}")


if __name__ == "__main__":
    main()
