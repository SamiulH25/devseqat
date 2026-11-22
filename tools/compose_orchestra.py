#!/usr/bin/env python3
"""
Full orchestral soundtrack compiler for Ironfield using CC0 VSCO 2 CE SFZ instruments.

Renders all 68 game soundtrack pieces (frontend, 13x4-stem adaptive battles, 28 cultural map
themes, 12 places/seasons/dread, 12 outcomes/stingers) to stereo OGG Vorbis in Data/audio/music/.

Usage:
  ~/.cache/ironfield-audio/venv/bin/python tools/compose_orchestra.py           # render all missing (or --force)
  ~/.cache/ironfield-audio/venv/bin/python tools/compose_orchestra.py --only battle_north
  ~/.cache/ironfield-audio/venv/bin/python tools/compose_orchestra.py --force   # re-render all
"""
from __future__ import annotations

import argparse
import concurrent.futures
import os
import sys
import time
from typing import Callable

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from orchestra_lib import (
    CULTURES,
    battle_percussion,
    cadence_rubato,
    chord_voicing,
    deg_midi,
    harp_arpeggios,
    make_melody,
    phrase_expression,
    sfz,
    spiccato_ostinato,
    string_pads,
)
from sfz_engine import NoteEvent, SfzInstrument, TrackSpec, export_ogg, render_piece

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO_ROOT, "Data", "audio", "music")


def stable_seed(name: str) -> int:
    h = 2166136261
    for c in name.encode("utf-8"):
        h = ((h ^ c) * 16777619) & 0xFFFFFFFF
    return h


# ---------------------------------------------------------------------------
# 1. FRONTEND (creator_hearth, chronicle_end; title_ironfield is in orchestra_poc.py)
# ---------------------------------------------------------------------------

def render_creator_hearth(cache: dict[str, SfzInstrument]) -> list[str]:
    """Warm 3/4 harp + flute + solo violin + cello hearthside waltz (4 stems or single)."""
    root, sc, bpb, bars, bpm = 57, "aeolian", 3, 24, 80.0
    prog = [0, 5, 2, 6, 0, 3, 5, 4]
    tot = float(bars * bpb)
    expr = phrase_expression(tot, 0.55, 0.60, 0.92)
    tracks = [
        TrackSpec("Harp", sfz("Harp.sfz"), -0.34, 0.66, 0.26, 0, 10, harp_arpeggios(root, sc, prog, bars, bpb, 0.64)),
        TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.28, 0.62, 0.28, 0, 12, string_pads(root, sc, prog, bars, bpb, 0, 50, 0.62), expr),
        TrackSpec("Viola", sfz("ViolaEnsSusVib.sfz"), -0.10, 0.50, 0.30, 0, 13, string_pads(root, sc, prog, bars, bpb, 2, 58, 0.54, 4, 24), expr),
        TrackSpec("Flute", sfz("FluteSusVib.sfz"), 0.14, 0.64, 0.32, 0, 10, make_melody(root + 12, sc, prog, bars, bpb, 1101, "lyrical", 0, 7, 0.72), expr),
        TrackSpec("SViolin", sfz("SViolinVib.sfz"), -0.22, 0.54, 0.30, 0, 11, make_melody(root, sc, prog, bars, bpb, 1102, "sparse", 2, 9, 0.64), expr),
    ]
    stems = render_piece(tracks, tot, bpm, True, cadence_rubato(tot, bpb, 0.05), 2.3, cache, False, 1101)
    p = os.path.join(OUT_DIR, "creator_hearth.ogg")
    export_ogg(stems[0], p, quality=4.8)
    return [p]


def render_chronicle_end(cache: dict[str, SfzInstrument]) -> list[str]:
    """Solemn, elegiac end-of-campaign chronicle (D Dorian, 66 BPM, 16 bars, non-loop)."""
    root, sc, bpb, bars, bpm = 62, "dorian", 4, 16, 66.0
    prog = [0, 6, 5, 4, 0, 2, 3, 0]
    tot = float(bars * bpb)
    expr = phrase_expression(tot, 0.52, 0.52, 0.90)
    tracks = [
        TrackSpec("Harp", sfz("Harp.sfz"), -0.32, 0.60, 0.28, 0, 12, harp_arpeggios(root, sc, prog, bars, bpb, 0.60, "sparse")),
        TrackSpec("CB", sfz("ContrabassSusVB.sfz"), 0.40, 0.64, 0.30, 0, 14, string_pads(root, sc, prog, bars, bpb, -1, 44, 0.60), expr),
        TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.24, 0.66, 0.30, 0, 12, string_pads(root, sc, prog, bars, bpb, 0, 52, 0.64), expr),
        TrackSpec("Violins", sfz("ViolinEnsSusVib.sfz"), -0.40, 0.58, 0.34, 0, 12, make_melody(root + 12, sc, prog, bars, bpb, 1201, "lyrical", 0, 7, 0.72), expr),
        TrackSpec("Horn", sfz("FHornSus.sfz"), 0.14, 0.66, 0.38, 0, 12, make_melody(root - 12, sc, prog, bars, bpb, 1202, "sparse", 0, 6, 0.74), expr),
        TrackSpec("Bells", sfz("TubularBells.sfz"), 0.42, 0.36, 0.48, 0, 6, [NoteEvent(0.0, 62, 4.0, 0.55), NoteEvent(32.0, 62, 4.0, 0.62), NoteEvent(60.0, 62, 4.0, 0.52)]),
    ]
    stems = render_piece(tracks, tot, bpm, False, cadence_rubato(tot, bpb, 0.07), 2.8, cache, False, 1201)
    p = os.path.join(OUT_DIR, "chronicle_end.ogg")
    export_ogg(stems[0], p, quality=4.8)
    return [p]


# ---------------------------------------------------------------------------
# 2. CULTURAL MAP SUITES (7 cultures x 4 moods = 28 pieces)
# ---------------------------------------------------------------------------

def render_map_piece(culture: str, mood: str, cache: dict[str, SfzInstrument]) -> list[str]:
    pal = CULTURES[culture]
    piece_id = f"{culture}_{mood}"
    seed = stable_seed(piece_id)
    bpb = 3 if (culture in ("azure", "north") and mood == "peace") else 4
    bars = 16 if mood == "night" else 20

    if mood == "day":
        bpm = {"azure": 86, "crimson": 92, "north": 82, "sunward": 90, "steppe": 100, "league": 94, "order": 72}[culture]
        prog = [0, 6, 2, 4, 0, 3, 5, 4]
        lead_sfz = pal.lead_day
        mel_style = "lyrical"
        reverb = 2.3
    elif mood == "peace":
        bpm = {"azure": 76, "crimson": 82, "north": 74, "sunward": 80, "steppe": 84, "league": 88, "order": 70}[culture]
        prog = [0, 3, 5, 4, 0, 2, 3, 0]
        lead_sfz = "FluteSusVib.sfz" if culture not in ("steppe", "order") else pal.lead_day
        mel_style = "lyrical"
        reverb = 2.4
    elif mood == "night":
        bpm = {"azure": 60, "crimson": 62, "north": 56, "sunward": 62, "steppe": 58, "league": 64, "order": 54}[culture]
        prog = [0, 5, 6, 0, 3, 5, 4, 0]
        lead_sfz = pal.lead_night
        mel_style = "sparse"
        reverb = 2.8
    else:  # host (enemy army near — tense march)
        bpm = {"azure": 98, "crimson": 104, "north": 96, "sunward": 104, "steppe": 112, "league": 100, "order": 92}[culture]
        prog = [0, 0, 5, 6, 0, 3, 5, 4]
        lead_sfz = pal.lead_battle
        mel_style = "martial"
        reverb = 2.1

    tot = float(bars * bpb)
    expr = phrase_expression(tot, 0.56, 0.54 if mood == "night" else 0.62, 0.88 if mood == "night" else 0.96)
    tracks: list[TrackSpec] = []

    # Low foundation
    tracks.append(TrackSpec(
        "Low", sfz(pal.low), 0.36, 0.62 if mood != "night" else 0.50, 0.28, 0, 12,
        string_pads(pal.root, pal.scale, prog, bars, bpb, -1, 44, 0.62 if mood != "night" else 0.48), expr,
    ))

    # Accompaniment texture
    if mood == "host":
        tracks.append(TrackSpec(
            "Ostinato", sfz("CelloEnsSpic.sfz"), 0.22, 0.60, 0.24, 0, 8,
            spiccato_ostinato(pal.root, pal.scale, prog, bars, bpb, -1, 0.72, pal.battle_pattern),
        ))
        timp, _, _ = battle_percussion(prog, pal.root, pal.scale, bars, bpb, pal.perc_style, 0.64)
        tracks.append(TrackSpec("Timp", sfz("Timpani.sfz"), -0.18, 0.60, 0.36, 0, 7, timp))
    elif pal.accompany == "Harp.sfz":
        tracks.append(TrackSpec(
            "Harp", sfz("Harp.sfz"), -0.34, 0.62 if mood != "night" else 0.54, 0.26, 0, 11,
            harp_arpeggios(pal.root, pal.scale, prog, bars, bpb, 0.60 if mood != "night" else 0.52, "sparse" if mood == "night" else "flowing"),
        ))
    elif pal.accompany == "OrganQuiet.sfz":
        tracks.append(TrackSpec(
            "Organ", sfz("OrganQuiet.sfz"), 0.0, 0.52, 0.38, 0, 10,
            string_pads(pal.root, pal.scale, prog, bars, bpb, 1, 56, 0.58), expr,
        ))
        tracks.append(TrackSpec(
            "Bells", sfz("TubularBells.sfz"), 0.42, 0.34, 0.46, 0, 6,
            [NoteEvent(bar * bpb, pal.root, 3.8, 0.52) for bar in range(0, bars, 8)],
        ))
    else:
        tracks.append(TrackSpec(
            "MidStr", sfz("CelloEnsSusVib.sfz"), 0.20, 0.58, 0.28, 0, 13,
            string_pads(pal.root, pal.scale, prog, bars, bpb, 0, 52, 0.58), expr,
        ))

    # Inner string warmth
    pad_sfz = "ViolaEnsSusVib-Quiet.sfz" if mood == "night" else "ViolaEnsSusVib.sfz"
    tracks.append(TrackSpec(
        "Viola", sfz(pad_sfz), -0.12, 0.50, 0.30, 0, 13,
        string_pads(pal.root, pal.scale, prog, bars, bpb, 2, 58, 0.56, start_bar=4 if mood != "host" else 0), expr,
    ))

    # Lead melody
    lead_root = pal.root if lead_sfz in ("FHornSus.sfz", "TromboneSus.sfz", "BassoonSus.sfz", "CelloEnsSusVib.sfz", "OrganQuiet.sfz") else pal.root
    if lead_sfz in ("FluteSusVib.sfz", "FluteExpVib.sfz", "FluteSusNV.sfz", "OboeSusVib.sfz", "SViolinVib.sfz", "ClarinetSus.sfz"):
        lead_root = pal.root + (12 if pal.root < 60 else 0)
    tracks.append(TrackSpec(
        "Lead", sfz(lead_sfz), 0.14 if lead_sfz.startswith("F") else -0.18, 0.68, 0.34, 0, 10,
        make_melody(lead_root, pal.scale, prog, bars, bpb, seed + 1, mel_style, 0, 8, 0.76 if mood != "night" else 0.64), expr,
    ))

    # Counter-voice in middle section for rich orchestration
    if mood in ("day", "peace"):
        cnt_sfz = "ViolinEnsSusVib.sfz" if mood == "day" else "CelloEnsSusVib.sfz"
        tracks.append(TrackSpec(
            "Counter", sfz(cnt_sfz), -0.38 if mood == "day" else 0.26, 0.48, 0.32, 0, 14,
            string_pads(pal.root, pal.scale, prog, bars, bpb, 1, 66 if mood == "day" else 52, 0.58, start_bar=8, end_bar=16), expr,
        ))

    stems = render_piece(tracks, tot, float(bpm), False, cadence_rubato(tot, bpb, 0.05 if mood != "host" else 0.02), reverb, cache, False, seed)
    p = os.path.join(OUT_DIR, f"{piece_id}.ogg")
    export_ogg(stems[0], p, quality=4.5)
    return [p]


# ---------------------------------------------------------------------------
# 3. ADAPTIVE 4-STEM BATTLE SUITES (12 remaining suites x 4 stems)
# ---------------------------------------------------------------------------

def render_battle_suite(suite_id: str, cache: dict[str, SfzInstrument]) -> list[str]:
    seed = stable_seed(suite_id)
    bpb = 4
    bars = 24

    # Configure each battle suite's distinctive key, tempo, instrumentation & groove
    cfg = {
        "battle_crimson":    (60, "aeolian",    122, "TromboneSus.sfz",  "TrumpetSus.sfz",  "TubaSus.sfz",        "march",  "heavy",  [0, 0, 5, 6, 0, 3, 5, 4]),
        "battle_north":      (57, "aeolian",    116, "FHornSus.sfz",     "SViolinSpic.sfz", "ContrabassSusVB.sfz","drive",  "heavy",  [0, 6, 5, 6, 0, 2, 5, 4]),
        "battle_sunward":    (62, "hijaz",      126, "OboeSusVib.sfz",   "TrumpetStac.sfz", "CelloEnsSpic.sfz",   "gallop", "gallop", [0, 1, 0, 6, 3, 1, 0, 4]),
        "battle_steppe":     (62, "penta_min",  130, "SViolinVib.sfz",   "FHornStac.sfz",   "ContrabassSusVB.sfz","gallop", "gallop", [0, 0, 3, 4, 0, 2, 3, 4]),
        "battle_league":     (67, "mixolydian", 124, "TrumpetSus.sfz",   "FluteStac.sfz",   "TubaStac.sfz",       "march",  "march",  [0, 6, 3, 4, 0, 5, 3, 4]),
        "battle_order":      (62, "phrygian",   106, "TromboneSus.sfz",  "OrganLoud.sfz",   "OrganLoudPedal.sfz", "march",  "heavy",  [0, 1, 0, 6, 0, 3, 1, 4]),
        "battle_siege":      (57, "phrygian",   104, "TromboneSus.sfz",  "TrumpetSus.sfz",  "TubaSus.sfz",        "march",  "heavy",  [0, 1, 0, 5, 0, 1, 6, 4]),
        "battle_duel":       (64, "harmonic",   134, "SViolinSpic.sfz",  "OboeStac.sfz",    "CelloEnsSpic.sfz",   "drive",  "march",  [0, 5, 3, 4, 0, 6, 5, 4]),
        "battle_tournament": (67, "ionian",     126, "TrumpetStac.sfz",  "FHornStac.sfz",   "TubaStac.sfz",       "march",  "march",  [0, 4, 5, 3, 0, 3, 4, 0]),
        "battle_beasts":     (64, "locrian",    128, "FHornMute.sfz",    "BassoonStac.sfz", "TubaStac.sfz",       "gallop", "heavy",  [0, 1, 4, 3, 0, 1, 5, 6]),
        "battle_undead":     (61, "phrygian",   108, "BassoonSus.sfz",   "OrganQuiet.sfz",  "OrganLoudPedal.sfz", "drive",  "heavy",  [0, 1, 5, 4, 0, 1, 6, 4]),
        "battle_hollow":     (62, "harmonic",   112, "OrganLoud.sfz",    "TrumpetSus.sfz",  "OrganLoudPedal.sfz", "march",  "heavy",  [0, 5, 4, 1, 0, 3, 5, 4]),
    }[suite_id]

    root, sc, bpm, lead_sfz, climax_sfz, low_sfz, pat, pstyle, prog = cfg
    tot = float(bars * bpb)
    timp, rolls, gm = battle_percussion(prog, root, sc, bars, bpb, pstyle, 0.78)

    tracks = [
        # Stem 0 (Deploy): brooding low foundation, cello pulse, sparse viola pad, heartbeat timpani
        TrackSpec("S0_Low", sfz(low_sfz), 0.38, 0.66, 0.28, 0, 10, string_pads(root, sc, prog, bars, bpb, -1, 44, 0.70, attack=0.10, release=0.30)),
        TrackSpec("S0_CelloSpic", sfz("CelloEnsSpic.sfz"), 0.24, 0.60, 0.24, 0, 8, spiccato_ostinato(root, sc, prog, bars, bpb, -1, 0.68, pat)),
        TrackSpec("S0_ViolaPad", sfz("ViolaEnsSusVib.sfz"), -0.14, 0.46, 0.32, 0, 12, string_pads(root, sc, prog, bars, bpb, 2, 58, 0.52)),
        TrackSpec("S0_Timp", sfz("Timpani.sfz"), -0.16, 0.62, 0.36, 0, 6, [n for n in timp if n.beat % 4.0 < 0.1]),

        # Stem 1 (Skirmish): driving violin/viola spiccato engine + crisp percussion march
        TrackSpec("S1_VlnSpic", sfz("ViolinEnsSpic.sfz"), -0.42, 0.58, 0.24, 1, 7, spiccato_ostinato(root, sc, prog, bars, bpb, 1, 0.76, pat)),
        TrackSpec("S1_VlaSpic", sfz("ViolaEnsSpic.sfz"), -0.10, 0.52, 0.24, 1, 7, spiccato_ostinato(root, sc, prog, bars, bpb, 0, 0.68, pat)),
        TrackSpec("S1_Perc", sfz("GM-StylePerc.sfz"), 0.10, 0.52, 0.34, 1, 6, gm),

        # Stem 2 (Melee): main heroic/fierce theme + warm trombone/cello harmony + full timpani
        TrackSpec("S2_Lead", sfz(lead_sfz), 0.14, 0.72, 0.36, 2, 9, make_melody(root, sc, prog, bars, bpb, seed + 10, "martial", 0, 8, 0.84)),
        TrackSpec("S2_Tromb", sfz("TromboneSus.sfz"), 0.32, 0.52, 0.38, 2, 10, string_pads(root, sc, prog, bars, bpb, 0, 50, 0.72, attack=0.07, release=0.28)),
        TrackSpec("S2_CelloSus", sfz("CelloEnsSusVib.sfz"), 0.26, 0.52, 0.28, 2, 11, string_pads(root, sc, prog, bars, bpb, 1, 56, 0.70, attack=0.09, release=0.30)),
        TrackSpec("S2_Timp", sfz("Timpani.sfz"), -0.18, 0.68, 0.38, 2, 6, timp),

        # Stem 3 (Crisis): urgent string tremolos + climax brass/organ counter-theme + timpani rolls
        TrackSpec("S3_VlnTrem", sfz("ViolinEnsTrem.sfz"), -0.46, 0.48, 0.32, 3, 10, string_pads(root, sc, prog, bars, bpb, 2, 72, 0.76, attack=0.08, release=0.24)),
        TrackSpec("S3_Climax", sfz(climax_sfz), 0.26, 0.58, 0.40, 3, 8, make_melody(root + (12 if climax_sfz.startswith(("Trumpet", "Flute", "Oboe")) else 0), sc, prog, bars, bpb, seed + 20, "martial", 2, 9, 0.86)),
        TrackSpec("S3_Rolls", sfz("TimpaniRolls.sfz"), -0.18, 0.62, 0.42, 3, 5, rolls),
    ]

    if suite_id in ("battle_order", "battle_undead", "battle_hollow", "battle_siege"):
        tracks.append(TrackSpec(
            "S3_Bells", sfz("TubularBells.sfz"), 0.44, 0.42, 0.46, 3, 5,
            [NoteEvent(bar * 4.0, root, 3.8, 0.72) for bar in range(0, bars, 4)],
        ))

    stems = render_piece(tracks, tot, float(bpm), True, None, 2.0, cache, True, seed)
    paths = []
    for s in range(4):
        p = os.path.join(OUT_DIR, f"{suite_id}_s{s}.ogg")
        export_ogg(stems[s], p, quality=4.5)
        paths.append(p)
    return paths


# ---------------------------------------------------------------------------
# 4. PLACES, SEASONS & DREAD (12 pieces)
# ---------------------------------------------------------------------------

def render_place_piece(piece_id: str, cache: dict[str, SfzInstrument]) -> list[str]:
    seed = stable_seed(piece_id)
    tracks: list[TrackSpec] = []

    if piece_id == "town_market":
        root, sc, bpb, bars, bpm, loop, rev = 67, "mixolydian", 4, 20, 102.0, True, 1.9
        prog = [0, 3, 4, 0, 0, 6, 3, 4]
        tracks = [
            TrackSpec("Harp", sfz("Harp.sfz"), -0.30, 0.62, 0.22, 0, 9, harp_arpeggios(root, sc, prog, bars, bpb, 0.64)),
            TrackSpec("Pizz", sfz("CelloEnsPizz.sfz"), 0.26, 0.64, 0.22, 0, 8, spiccato_ostinato(root, sc, prog, bars, bpb, -1, 0.72, "march")),
            TrackSpec("Flute", sfz("FluteStac.sfz"), 0.18, 0.62, 0.26, 0, 9, make_melody(root + 12, sc, prog, bars, bpb, seed + 1, "dance", 0, 7, 0.76)),
            TrackSpec("Clar", sfz("ClarinetStac.sfz"), -0.18, 0.54, 0.26, 0, 10, make_melody(root, sc, prog, bars, bpb, seed + 2, "lyrical", 0, 6, 0.68)),
            TrackSpec("Glock", sfz("Glockenspiel.sfz"), 0.36, 0.30, 0.34, 0, 8, [NoteEvent(b * 4.0, deg_midi(root + 12, sc, prog[b % len(prog)]), 1.5, 0.56) for b in range(0, bars, 2)]),
        ]
    elif piece_id in ("tavern_reel_1", "tavern_reel_2"):
        root = 62 if piece_id == "tavern_reel_1" else 67
        sc = "dorian" if piece_id == "tavern_reel_1" else "mixolydian"
        bpb, bars, bpm, loop, rev = (4, 20, 118.0, True, 1.6) if piece_id == "tavern_reel_1" else (3, 24, 114.0, True, 1.6)
        prog = [0, 6, 0, 4, 0, 3, 6, 4]
        tracks = [
            TrackSpec("Fiddle", sfz("SViolinSpic.sfz"), -0.20, 0.72, 0.22, 0, 8, make_melody(root + (12 if root < 64 else 0), sc, prog, bars, bpb, seed + 1, "dance", 0, 8, 0.82)),
            TrackSpec("Flute", sfz("FluteStac.sfz"), 0.22, 0.56, 0.24, 0, 9, make_melody(root + 12, sc, prog, bars, bpb, seed + 2, "dance", 0, 7, 0.68)),
            TrackSpec("Harp", sfz("Harp.sfz"), -0.34, 0.60, 0.20, 0, 9, harp_arpeggios(root, sc, prog, bars, bpb, 0.64)),
            TrackSpec("CelloPizz", sfz("CelloEnsPizz.sfz"), 0.28, 0.66, 0.20, 0, 8, spiccato_ostinato(root, sc, prog, bars, bpb, -1, 0.74, "march")),
            TrackSpec("Perc", sfz("GM-StylePerc.sfz"), 0.08, 0.44, 0.24, 0, 7, [NoteEvent(b * bpb + off, 54 if off % 1 else 36, 0.2, 0.64) for b in range(bars) for off in ([0.0, 1.0, 2.0, 3.0] if bpb == 4 else [0.0, 1.0, 2.0])]),
        ]
    elif piece_id == "tavern_ballad":
        root, sc, bpb, bars, bpm, loop, rev = 57, "aeolian", 3, 20, 74.0, True, 2.0
        prog = [0, 5, 2, 6, 0, 3, 5, 4]
        tot = float(bars * bpb)
        expr = phrase_expression(tot, 0.55, 0.58, 0.90)
        tracks = [
            TrackSpec("Harp", sfz("Harp.sfz"), -0.32, 0.66, 0.24, 0, 11, harp_arpeggios(root, sc, prog, bars, bpb, 0.62)),
            TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.26, 0.62, 0.26, 0, 12, string_pads(root, sc, prog, bars, bpb, 0, 50, 0.62), expr),
            TrackSpec("Flute", sfz("FluteSusVib.sfz"), 0.16, 0.64, 0.28, 0, 10, make_melody(root + 12, sc, prog, bars, bpb, seed + 1, "lyrical", 0, 7, 0.72), expr),
            TrackSpec("SViolin", sfz("SViolinVib.sfz"), -0.18, 0.54, 0.26, 0, 11, make_melody(root + 12, sc, prog, bars, bpb, seed + 2, "sparse", 0, 7, 0.64), expr),
        ]
    elif piece_id == "lords_hall":
        root, sc, bpb, bars, bpm, loop, rev = 62, "dorian", 4, 20, 74.0, True, 2.4
        prog = [0, 4, 5, 2, 3, 0, 6, 4]
        tot = float(bars * bpb)
        expr = phrase_expression(tot, 0.55, 0.58, 0.92)
        tracks = [
            TrackSpec("Harp", sfz("Harp.sfz"), -0.34, 0.60, 0.28, 0, 10, harp_arpeggios(root, sc, prog, bars, bpb, 0.60)),
            TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.26, 0.62, 0.30, 0, 12, string_pads(root, sc, prog, bars, bpb, 0, 52, 0.64), expr),
            TrackSpec("Viola", sfz("ViolaEnsSusVib.sfz"), -0.10, 0.54, 0.32, 0, 13, string_pads(root, sc, prog, bars, bpb, 2, 60, 0.58), expr),
            TrackSpec("Oboe", sfz("OboeSusVib.sfz"), 0.14, 0.62, 0.34, 0, 10, make_melody(root, sc, prog, bars, bpb, seed + 1, "lyrical", 0, 7, 0.74), expr),
            TrackSpec("Bassoon", sfz("BassoonSus.sfz"), 0.30, 0.54, 0.32, 0, 12, string_pads(root, sc, prog, bars, bpb, -1, 46, 0.62), expr),
        ]
    elif piece_id == "hall_feast":
        root, sc, bpb, bars, bpm, loop, rev = 67, "ionian", 4, 20, 114.0, True, 2.0
        prog = [0, 3, 4, 0, 0, 5, 3, 4]
        tracks = [
            TrackSpec("Trumpet", sfz("TrumpetStac.sfz"), 0.22, 0.56, 0.34, 0, 8, make_melody(root, sc, prog, bars, bpb, seed + 1, "dance", 0, 7, 0.78)),
            TrackSpec("Flute", sfz("FluteStac.sfz"), -0.22, 0.58, 0.28, 0, 8, make_melody(root + 12, sc, prog, bars, bpb, seed + 2, "dance", 0, 7, 0.72)),
            TrackSpec("Harp", sfz("Harp.sfz"), -0.34, 0.60, 0.24, 0, 9, harp_arpeggios(root, sc, prog, bars, bpb, 0.64)),
            TrackSpec("VlnPizz", sfz("ViolinEnsPizz.sfz"), -0.16, 0.56, 0.24, 0, 8, spiccato_ostinato(root, sc, prog, bars, bpb, 1, 0.72, "march")),
            TrackSpec("Timp", sfz("Timpani.sfz"), 0.10, 0.60, 0.34, 0, 7, [NoteEvent(b * 4.0, 43 if prog[b % len(prog)] == 0 else 38, 0.8, 0.70) for b in range(bars)]),
        ]
    elif piece_id == "chapter_house":
        root, sc, bpb, bars, bpm, loop, rev = 62, "dorian", 4, 16, 60.0, True, 3.0
        prog = [0, 6, 3, 0, 5, 2, 6, 0]
        tot = float(bars * bpb)
        expr = phrase_expression(tot, 0.50, 0.55, 0.88)
        tracks = [
            TrackSpec("OrgPed", sfz("OrganQuietPedal.sfz"), 0.0, 0.64, 0.42, 0, 10, string_pads(root, sc, prog, bars, bpb, -1, 44, 0.64), expr),
            TrackSpec("Org", sfz("OrganQuiet.sfz"), 0.0, 0.58, 0.44, 0, 10, string_pads(root, sc, prog, bars, bpb, 1, 58, 0.62), expr),
            TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.24, 0.54, 0.38, 0, 12, make_melody(root - 12, sc, prog, bars, bpb, seed + 1, "sparse", 0, 7, 0.68), expr),
            TrackSpec("Bells", sfz("TubularBells.sfz"), 0.38, 0.36, 0.50, 0, 6, [NoteEvent(b * 4.0, 62, 4.0, 0.54) for b in range(0, bars, 8)]),
        ]
    elif piece_id == "sacred_site":
        root, sc, bpb, bars, bpm, loop, rev = 64, "lydian", 4, 16, 58.0, True, 3.0
        prog = [0, 1, 0, 4, 5, 1, 0, 0]
        tot = float(bars * bpb)
        expr = phrase_expression(tot, 0.52, 0.50, 0.88)
        tracks = [
            TrackSpec("Harp", sfz("Harp.sfz"), -0.32, 0.60, 0.34, 0, 12, harp_arpeggios(root, sc, prog, bars, bpb, 0.56, "sparse")),
            TrackSpec("VlnQ", sfz("ViolinEnsSusVib-Quiet.sfz"), -0.36, 0.58, 0.40, 0, 14, string_pads(root, sc, prog, bars, bpb, 2, 68, 0.60), expr),
            TrackSpec("CelQ", sfz("CelloEnsSusVib-Quiet.sfz"), 0.28, 0.60, 0.38, 0, 14, string_pads(root, sc, prog, bars, bpb, 0, 52, 0.60), expr),
            TrackSpec("Flute", sfz("FluteExpVib.sfz"), 0.14, 0.60, 0.42, 0, 11, make_melody(root + 12, sc, prog, bars, bpb, seed + 1, "sparse", 0, 6, 0.66), expr),
            TrackSpec("Glock", sfz("Glockenspiel.sfz"), 0.36, 0.28, 0.48, 0, 8, [NoteEvent(b * 4.0 + 2.0, deg_midi(root + 12, sc, prog[b % len(prog)] + 2), 2.5, 0.48) for b in range(0, bars, 4)]),
        ]
    elif piece_id in ("winter_march", "winter_night"):
        is_night = piece_id == "winter_night"
        root, sc, bpb, bars, bpm, loop, rev = (57, "aeolian", 4, 16 if is_night else 18, 54.0 if is_night else 68.0, False, 2.8)
        prog = [0, 5, 6, 0, 3, 5, 4, 0]
        tot = float(bars * bpb)
        expr = phrase_expression(tot, 0.50, 0.48, 0.86)
        tracks = [
            TrackSpec("CelQ", sfz("CelloEnsSusVib-Quiet.sfz"), 0.26, 0.62, 0.34, 0, 13, string_pads(root, sc, prog, bars, bpb, 0, 50, 0.58), expr),
            TrackSpec("VlnQ", sfz("ViolinEnsSusVib-Quiet.sfz"), -0.38, 0.58, 0.38, 0, 14, string_pads(root, sc, prog, bars, bpb, 2, 66, 0.56), expr),
            TrackSpec("Harp", sfz("Harp.sfz"), -0.28, 0.56, 0.32, 0, 12, harp_arpeggios(root, sc, prog, bars, bpb, 0.52, "sparse")),
            TrackSpec("Flute", sfz("FluteSusNV.sfz"), 0.14, 0.58, 0.40, 0, 11, make_melody(root + 12, sc, prog, bars, bpb, seed + 1, "sparse" if is_night else "lyrical", 0, 7, 0.64), expr),
            TrackSpec("Glock", sfz("Glockenspiel.sfz"), 0.36, 0.28, 0.46, 0, 8, [NoteEvent(b * 4.0, deg_midi(root + 24, sc, prog[b % len(prog)]), 2.2, 0.46) for b in range(0, bars, 3)]),
        ]
    else:  # omen_graves, black_flame
        is_bf = piece_id == "black_flame"
        root, sc, bpb, bars, bpm, loop, rev = (62 if is_bf else 61, "harmonic" if is_bf else "locrian", 4, 16, 64.0 if is_bf else 58.0, is_bf, 3.0)
        prog = [0, 1, 5, 4, 0, 1, 6, 4]
        tot = float(bars * bpb)
        expr = phrase_expression(tot, 0.58, 0.50, 0.92)
        tracks = [
            TrackSpec("CBTrem", sfz("ContrabassTrem.sfz"), 0.38, 0.62, 0.36, 0, 12, string_pads(root, sc, prog, bars, bpb, -1, 42, 0.64), expr),
            TrackSpec("CelTrem", sfz("CelloEnsTrem.sfz"), 0.22, 0.56, 0.38, 0, 12, string_pads(root, sc, prog, bars, bpb, 0, 50, 0.60), expr),
            TrackSpec("Org", sfz("OrganQuietPedal.sfz" if not is_bf else "OrganLoudPedal.sfz"), 0.0, 0.58, 0.44, 0, 10, string_pads(root, sc, prog, bars, bpb, -1, 44, 0.60), expr),
            TrackSpec("Lead", sfz("FHornMute.sfz" if not is_bf else "TromboneSus.sfz"), -0.16, 0.62, 0.42, 0, 12, make_melody(root - 12, sc, prog, bars, bpb, seed + 1, "sparse", 0, 6, 0.72), expr),
            TrackSpec("Bells", sfz("TubularBells.sfz"), 0.42, 0.40, 0.50, 0, 6, [NoteEvent(b * 4.0, root, 4.0, 0.62) for b in range(0, bars, 4)]),
        ]

    tot = float(bars * bpb)
    stems = render_piece(tracks, tot, bpm, loop, cadence_rubato(tot, bpb, 0.04) if bpm < 90 else None, rev, cache, False, seed)
    p = os.path.join(OUT_DIR, f"{piece_id}.ogg")
    export_ogg(stems[0], p, quality=4.5)
    return [p]


# ---------------------------------------------------------------------------
# 5. OUTCOMES & STINGERS (12 short orchestral fanfares / elegies)
# ---------------------------------------------------------------------------

def render_stinger(piece_id: str, cache: dict[str, SfzInstrument]) -> list[str]:
    seed = stable_seed(piece_id)
    tracks: list[TrackSpec] = []

    if piece_id in ("victory_field", "victory_heroic", "victory_duel"):
        bpm = {"victory_field": 96.0, "victory_heroic": 92.0, "victory_duel": 104.0}[piece_id]
        bars = {"victory_field": 6, "victory_heroic": 8, "victory_duel": 5}[piece_id]
        root, sc = (62, "mixolydian") if piece_id != "victory_heroic" else (67, "ionian")
        prog = [0, 3, 4, 0, 3, 4, 0, 0][:bars]
        tot = float(bars * 4)
        tracks = [
            TrackSpec("Tpt", sfz("TrumpetSus.sfz"), 0.20, 0.70, 0.38, 0, 7, make_melody(root, sc, prog, bars, 4, seed + 1, "martial", 0, 7, 0.86)),
            TrackSpec("Horn", sfz("FHornSus.sfz"), -0.16, 0.68, 0.36, 0, 8, string_pads(root, sc, prog, bars, 4, 1, 56, 0.78, attack=0.05)),
            TrackSpec("Str", sfz("ViolinEnsSusVib.sfz"), -0.38, 0.60, 0.34, 0, 10, string_pads(root, sc, prog, bars, 4, 2, 68, 0.78, attack=0.08)),
            TrackSpec("Low", sfz("TromboneSus.sfz"), 0.32, 0.58, 0.38, 0, 9, string_pads(root, sc, prog, bars, 4, -1, 46, 0.76, attack=0.06)),
            TrackSpec("Timp", sfz("Timpani.sfz"), -0.14, 0.72, 0.38, 0, 5, [NoteEvent(b * 4.0, 38 if root == 62 else 43, 1.2, 0.84) for b in range(bars)]),
            TrackSpec("Roll", sfz("TimpaniRolls.sfz"), -0.14, 0.66, 0.40, 0, 4, [NoteEvent((bars - 2) * 4.0, 45 if root == 62 else 38, 3.8, 0.86, attack=0.5)]),
        ]
    elif piece_id in ("defeat_field", "defeat_fallen"):
        bpm = 58.0 if piece_id == "defeat_field" else 54.0
        bars = 5 if piece_id == "defeat_field" else 6
        root, sc = 62, "aeolian"
        prog = [0, 5, 3, 4, 0, 0][:bars]
        tot = float(bars * 4)
        tracks = [
            TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.24, 0.68, 0.34, 0, 12, string_pads(root, sc, prog, bars, 4, 0, 50, 0.68)),
            TrackSpec("Viola", sfz("ViolaEnsSusVib.sfz"), -0.12, 0.58, 0.36, 0, 12, string_pads(root, sc, prog, bars, 4, 2, 58, 0.62)),
            TrackSpec("Horn", sfz("FHornSus.sfz"), 0.14, 0.66, 0.40, 0, 11, make_melody(root - 12, sc, prog, bars, 4, seed + 1, "sparse", 0, 5, 0.70)),
            TrackSpec("Bells", sfz("TubularBells.sfz"), 0.40, 0.38, 0.48, 0, 6, [NoteEvent(0.0, 62, 4.0, 0.58), NoteEvent((bars - 1) * 4.0, 62, 4.0, 0.52)]),
        ]
    else:
        # Campaign stingers (2-3 bars)
        st_cfg = {
            "sting_war":         (108.0, 3, 62, "phrygian",   "TromboneSus.sfz", "TrumpetStac.sfz", True),
            "sting_peace":       (92.0,  3, 67, "ionian",     "FHornSus.sfz",    "Harp.sfz",        False),
            "sting_rank":        (110.0, 3, 62, "mixolydian", "TrumpetStac.sfz", "FHornSus.sfz",    True),
            "sting_marriage":    (94.0,  3, 67, "ionian",     "FluteSusVib.sfz", "Harp.sfz",        False),
            "sting_oath":        (80.0,  2, 62, "dorian",     "FHornSus.sfz",    "OrganQuiet.sfz",  False),
            "sting_omen":        (76.0,  3, 61, "locrian",    "FHornMute.sfz",   "TubularBells.sfz",True),
            "sting_found_order": (106.0, 3, 62, "dorian",     "TrumpetSus.sfz",  "OrganLoud.sfz",   True),
        }[piece_id]
        bpm, bars, root, sc, lead, supp, perc = st_cfg
        prog = [0, 4, 0][:bars]
        tot = float(bars * 4)
        tracks = [
            TrackSpec("Lead", sfz(lead), 0.12, 0.72, 0.38, 0, 7, make_melody(root, sc, prog, bars, 4, seed + 1, "martial" if perc else "lyrical", 0, 6, 0.84)),
            TrackSpec("Supp", sfz(supp), -0.22, 0.60, 0.38, 0, 8, string_pads(root, sc, prog, bars, 4, 1, 58, 0.72, attack=0.05)),
            TrackSpec("Cello", sfz("CelloEnsSusVib.sfz"), 0.24, 0.58, 0.32, 0, 10, string_pads(root, sc, prog, bars, 4, -1, 48, 0.70, attack=0.05)),
        ]
        if perc:
            tracks.append(TrackSpec("Timp", sfz("Timpani.sfz"), -0.15, 0.72, 0.38, 0, 5, [NoteEvent(0.0, 38, 1.2, 0.86), NoteEvent((bars - 1) * 4.0, 38, 1.8, 0.90)]))

    stems = render_piece(tracks, tot, bpm, False, None, 2.2, cache, False, seed)
    p = os.path.join(OUT_DIR, f"{piece_id}.ogg")
    export_ogg(stems[0], p, quality=4.5)
    return [p]


# ---------------------------------------------------------------------------
# Job registry & parallel runner
# ---------------------------------------------------------------------------

def build_jobs() -> list[tuple[str, list[str], Callable[[dict[str, SfzInstrument]], list[str]]]]:
    jobs: list[tuple[str, list[str], Callable[[dict[str, SfzInstrument]], list[str]]]] = []
    # Frontend (title_ironfield already rendered by orchestra_poc.py)
    jobs.append(("creator_hearth", [os.path.join(OUT_DIR, "creator_hearth.ogg")], render_creator_hearth))
    jobs.append(("chronicle_end", [os.path.join(OUT_DIR, "chronicle_end.ogg")], render_chronicle_end))
    # 12 remaining adaptive 4-stem battles (battle_azure already rendered by orchestra_poc.py)
    for b in [
        "battle_crimson", "battle_north", "battle_sunward", "battle_steppe", "battle_league", "battle_order",
        "battle_siege", "battle_duel", "battle_tournament", "battle_beasts", "battle_undead", "battle_hollow",
    ]:
        outs = [os.path.join(OUT_DIR, f"{b}_s{s}.ogg") for s in range(4)]
        jobs.append((b, outs, lambda c, bid=b: render_battle_suite(bid, c)))
    # 28 cultural map pieces
    for cult in ("azure", "crimson", "north", "sunward", "steppe", "league", "order"):
        for mood in ("day", "night", "peace", "host"):
            pid = f"{cult}_{mood}"
            jobs.append((pid, [os.path.join(OUT_DIR, f"{pid}.ogg")], lambda c, cu=cult, mo=mood: render_map_piece(cu, mo, c)))
    # 12 places, seasons & dread
    for pid in [
        "town_market", "tavern_reel_1", "tavern_reel_2", "tavern_ballad", "lords_hall", "hall_feast",
        "chapter_house", "sacred_site", "winter_march", "winter_night", "omen_graves", "black_flame",
    ]:
        jobs.append((pid, [os.path.join(OUT_DIR, f"{pid}.ogg")], lambda c, p=pid: render_place_piece(p, c)))
    # 12 outcomes & stingers
    for pid in [
        "victory_field", "victory_heroic", "victory_duel", "defeat_field", "defeat_fallen",
        "sting_war", "sting_peace", "sting_rank", "sting_marriage", "sting_oath", "sting_omen", "sting_found_order",
    ]:
        jobs.append((pid, [os.path.join(OUT_DIR, f"{pid}.ogg")], lambda c, p=pid: render_stinger(p, c)))
    return jobs


def _run_batch(batch_ids: list[str]) -> list[tuple[str, float, int]]:
    cache: dict[str, SfzInstrument] = {}
    job_map = {jid: fn for jid, _, fn in build_jobs()}
    res = []
    for jid in batch_ids:
        t0 = time.time()
        paths = job_map[jid](cache)
        kb = sum(os.path.getsize(p) for p in paths) // 1024
        res.append((jid, time.time() - t0, kb))
        print(f"  done {jid:20s} ({kb:4d} KB, {time.time() - t0:.1f}s)", flush=True)
    return res


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", help="Substring filter for piece IDs")
    ap.add_argument("--force", action="store_true", help="Re-render existing OGG files")
    ap.add_argument("--workers", type=int, default=min(4, os.cpu_count() or 2))
    args = ap.parse_args()

    os.makedirs(OUT_DIR, exist_ok=True)
    jobs = build_jobs()
    todo = []
    for jid, outs, _ in jobs:
        if args.only and args.only.lower() not in jid.lower():
            continue
        if not args.force and all(os.path.isfile(p) for p in outs):
            continue
        todo.append(jid)

    if not todo:
        print("All matching orchestral pieces already exist.")
        return

    print(f"Rendering {len(todo)} orchestral pieces across {args.workers} workers...", flush=True)
    t0 = time.time()
    # Partition across workers so each worker reuses its SFZ sample cache!
    buckets = [todo[i::args.workers] for i in range(args.workers) if todo[i::args.workers]]
    total_kb = 0
    with concurrent.futures.ProcessPoolExecutor(max_workers=len(buckets)) as ex:
        for batch_res in ex.map(_run_batch, buckets):
            for _, _, kb in batch_res:
                total_kb += kb

    print(f"Finished {len(todo)} pieces ({total_kb / 1024:.1f} MB) in {time.time() - t0:.1f}s")


if __name__ == "__main__":
    main()
