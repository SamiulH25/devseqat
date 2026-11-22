#!/usr/bin/env python3
"""
Proof-of-concept orchestral composition & rendering pipeline using CC0 VSCO 2 CE SFZ instruments
via tools/sfz_engine.py.

Renders:
  - Data/audio/music/title_ironfield.ogg           (symphonic medieval title theme)
  - Data/audio/music/battle_azure_s0..s3.ogg       (4 sample-locked adaptive battle stems)
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sfz_engine import NoteEvent, SfzInstrument, TrackSpec, export_ogg, render_piece

VSCO_DIR = os.path.expanduser("~/.cache/ironfield-audio/VSCO-2-CE")
OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Data", "audio", "music")


def sfz(name: str) -> str:
    p = os.path.join(VSCO_DIR, name)
    if not os.path.isfile(p):
        raise FileNotFoundError(f"Missing VSCO SFZ: {p}")
    return p


def phrase_notes(
    start_beat: float,
    spec: list[tuple[int, float, float]],  # (midi_or_0_for_rest, dur_beats, vel)
    legato: float = 0.98,
    attack: float | None = None,
    release: float | None = None,
) -> list[NoteEvent]:
    out: list[NoteEvent] = []
    b = start_beat
    for midi, dur, vel in spec:
        if midi > 0:
            out.append(NoteEvent(b, midi, dur * legato, vel, attack=attack, release=release))
        b += dur
    return out


def build_title_ironfield(cache: dict[str, SfzInstrument]) -> None:
    """
    "Banners Over Ironfield" — D Dorian, 74 BPM, 32 bars (4/4 = 128 beats, ~104s seamless loop).
    Structure:
      Bars  0- 7 (A1): Lone harp + contrabass pedal + distant bell, noble French horn introduces theme.
      Bars  8-15 (A2): Cellos & violas enter with warm counter-harmony; horn answers and rises.
      Bars 16-23 (B) : Climax — full strings (violins octave soar), trombones warm brass, timpani rolls.
      Bars 24-31 (A3): Reflective wind-down — solo violin / horn echo, harp settles home to D.
    """
    # 32-bar chord progression (voice-led root, 3rd, 5th, upper colour in D Dorian)
    # Each tuple: (bass_midi, [harp_voicing_midis], [pad_voicing_midis])
    chords = [
        # A1 (bars 0-7): Dm - C - Dm - Am | Bb - C - Dm - A(sus/5)
        (38, [50, 57, 62, 65], [50, 57, 62]),
        (36, [48, 55, 60, 64], [48, 55, 60]),
        (38, [50, 57, 62, 65], [50, 57, 62]),
        (33, [45, 52, 57, 60], [45, 52, 60]),
        (34, [46, 53, 58, 62], [46, 53, 62]),
        (36, [48, 55, 60, 64], [48, 55, 64]),
        (38, [50, 57, 62, 65], [50, 57, 65]),
        (33, [45, 52, 57, 62], [45, 52, 57]),
        # A2 (bars 8-15): Dm - F - C - Gm | Dm - C - Bb - A
        (38, [50, 57, 62, 65], [50, 57, 65]),
        (41, [53, 57, 60, 65], [53, 60, 65]),
        (36, [48, 55, 60, 64], [48, 55, 64]),
        (43, [50, 55, 58, 62], [50, 58, 62]),
        (38, [50, 57, 62, 65], [50, 57, 65]),
        (36, [48, 55, 60, 64], [48, 55, 60]),
        (34, [46, 53, 58, 62], [46, 53, 62]),
        (33, [45, 52, 57, 64], [45, 52, 60]),
        # B Climax (bars 16-23): F - C - Dm - Am | Bb - F - Gm - C
        (41, [53, 60, 65, 69], [53, 60, 69]),
        (36, [48, 55, 60, 67], [48, 55, 67]),
        (38, [50, 57, 62, 65], [50, 57, 65]),
        (33, [45, 52, 57, 64], [45, 52, 64]),
        (34, [46, 53, 58, 65], [46, 53, 65]),
        (41, [53, 57, 60, 65], [53, 60, 65]),
        (43, [50, 55, 58, 62], [50, 58, 62]),
        (36, [48, 55, 60, 64], [48, 55, 64]),
        # A3 Resolution (bars 24-31): Dm - C - Bb - C | Dm - Am - Dm - Dm
        (38, [50, 57, 62, 65], [50, 57, 65]),
        (36, [48, 55, 60, 64], [48, 55, 60]),
        (34, [46, 53, 58, 62], [46, 53, 62]),
        (36, [48, 55, 60, 64], [48, 55, 60]),
        (38, [50, 57, 62, 65], [50, 57, 62]),
        (33, [45, 52, 57, 60], [45, 52, 60]),
        (38, [50, 57, 62, 65], [50, 57, 62]),
        (38, [50, 57, 62, 69], [50, 57, 62]),
    ]

    # 1. Harp: flowing medieval arpeggios with gentle dynamic arc
    harp_notes: list[NoteEvent] = []
    for bar, (_, h, _) in enumerate(chords):
        b = bar * 4.0
        if bar == 31:
            # Final rolled chord
            for k, m in enumerate(h):
                harp_notes.append(NoteEvent(b + k * 0.08, m, 3.8, 0.58 + k * 0.03))
            continue
        # Varied 8th-note medieval harp pattern (never robotic)
        pat = [
            (0.0, h[0], 1.6, 0.68), (0.5, h[1], 1.2, 0.54), (1.0, h[2], 1.2, 0.60), (1.5, h[1], 0.9, 0.50),
            (2.0, h[3], 1.4, 0.64), (2.5, h[2], 1.0, 0.52), (3.0, h[1], 1.1, 0.56), (3.5, h[2], 0.8, 0.48),
        ]
        if bar % 4 == 3:
            pat = [
                (0.0, h[0], 1.8, 0.66), (0.5, h[1], 1.4, 0.54), (1.0, h[2], 1.4, 0.58),
                (2.0, h[3], 1.8, 0.62), (3.0, h[2], 1.0, 0.50),
            ]
        vel_mul = 1.08 if 16 <= bar < 24 else (0.88 if bar >= 28 else 1.0)
        for off, m, d, v in pat:
            harp_notes.append(NoteEvent(b + off, m, d, min(0.92, v * vel_mul)))

    # 2. Contrabass: deep sustained foundation
    cb_notes: list[NoteEvent] = []
    for bar, (bass, _, _) in enumerate(chords):
        vel = 0.72 if 16 <= bar < 24 else (0.52 if bar < 4 or bar >= 28 else 0.62)
        cb_notes.append(NoteEvent(bar * 4.0, bass, 3.92, vel, attack=0.14, release=0.45))

    # 3. Cello ensemble: enters bar 4 with rich warmth, swells in B
    cello_notes: list[NoteEvent] = []
    for bar in range(4, 32):
        bass, _, pad = chords[bar]
        vel = 0.78 if 16 <= bar < 24 else (0.55 if bar >= 28 else 0.65)
        cello_notes.append(NoteEvent(bar * 4.0, pad[0], 3.9, vel, attack=0.12, release=0.40))
        if 8 <= bar < 28:
            cello_notes.append(NoteEvent(bar * 4.0, pad[1], 3.9, vel * 0.82, attack=0.16, release=0.40))

    # 4. Viola ensemble: inner lush harmony (bars 8-27)
    viola_notes: list[NoteEvent] = []
    for bar in range(8, 28):
        _, _, pad = chords[bar]
        vel = 0.76 if 16 <= bar < 24 else 0.58
        viola_notes.append(NoteEvent(bar * 4.0, pad[2], 3.9, vel, attack=0.15, release=0.42))

    # 5. French Horn: noble main theme (A1 bars 2-7, A2 bars 8-15, B bars 16-23, quiet echo bars 26-29)
    horn_notes: list[NoteEvent] = []
    # A1 opening call (starts bar 2 = beat 8)
    horn_notes += phrase_notes(8.0, [
        (50, 2.0, 0.68), (57, 1.5, 0.76), (55, 0.5, 0.66),
        (53, 2.0, 0.72), (52, 1.0, 0.65), (50, 1.0, 0.62),
        (58, 2.5, 0.76), (57, 0.5, 0.68), (55, 1.0, 0.70),
        (57, 3.0, 0.74), (0, 1.0, 0.0),
        (50, 1.5, 0.70), (53, 0.5, 0.66), (55, 1.0, 0.72), (57, 1.0, 0.76),
        (52, 3.2, 0.70), (0, 0.8, 0.0),
    ], legato=0.97, attack=0.07, release=0.35)
    # A2 answer (bar 8 = beat 32)
    horn_notes += phrase_notes(32.0, [
        (62, 2.0, 0.78), (60, 1.0, 0.74), (57, 1.0, 0.72),
        (65, 2.5, 0.84), (64, 0.5, 0.76), (62, 1.0, 0.78),
        (60, 2.0, 0.80), (57, 1.5, 0.74), (55, 0.5, 0.68),
        (58, 3.0, 0.78), (0, 1.0, 0.0),
        (57, 1.5, 0.76), (55, 0.5, 0.70), (53, 2.0, 0.74),
        (55, 1.5, 0.72), (57, 0.5, 0.70), (53, 2.0, 0.72),
        (58, 2.0, 0.78), (57, 1.0, 0.74), (55, 1.0, 0.70),
        (57, 3.4, 0.80), (0, 0.6, 0.0),
    ], legato=0.97, attack=0.06, release=0.35)
    # B Climax (bar 16 = beat 64)
    horn_notes += phrase_notes(64.0, [
        (65, 2.0, 0.88), (64, 1.0, 0.84), (62, 1.0, 0.82),
        (60, 2.5, 0.86), (62, 0.5, 0.80), (64, 1.0, 0.84),
        (62, 2.0, 0.88), (57, 2.0, 0.82),
        (60, 3.0, 0.84), (0, 1.0, 0.0),
        (62, 1.5, 0.86), (65, 0.5, 0.84), (67, 2.0, 0.90),
        (65, 1.5, 0.86), (64, 0.5, 0.80), (62, 2.0, 0.84),
        (58, 2.0, 0.82), (60, 1.0, 0.80), (62, 1.0, 0.82),
        (60, 3.5, 0.86), (0, 0.5, 0.0),
    ], legato=0.98, attack=0.05, release=0.38)
    # A3 distant echo (bar 24 = beat 96)
    horn_notes += phrase_notes(96.0, [
        (62, 2.5, 0.66), (57, 1.5, 0.60),
        (55, 2.0, 0.58), (52, 2.0, 0.54),
        (53, 2.5, 0.58), (55, 1.5, 0.56),
        (52, 3.0, 0.54), (0, 1.0, 0.0),
        (50, 4.0, 0.56), (0, 4.0, 0.0),
        (50, 6.5, 0.50),
    ], legato=0.97, attack=0.10, release=0.50)

    # 6. Violin Ensemble: soaring high octave melody in B climax (bars 16-24)
    violin_notes: list[NoteEvent] = []
    violin_notes += phrase_notes(64.0, [
        (77, 2.0, 0.82), (76, 1.0, 0.78), (74, 1.0, 0.76),
        (72, 2.5, 0.80), (74, 0.5, 0.74), (76, 1.0, 0.78),
        (74, 2.0, 0.84), (69, 2.0, 0.76),
        (72, 3.2, 0.80), (0, 0.8, 0.0),
        (74, 1.5, 0.84), (77, 0.5, 0.82), (79, 2.0, 0.88),
        (77, 1.5, 0.84), (76, 0.5, 0.78), (74, 2.0, 0.80),
        (70, 2.0, 0.78), (72, 1.0, 0.76), (74, 1.0, 0.78),
        (72, 3.6, 0.82), (0, 0.4, 0.0),
        (74, 3.8, 0.64),
    ], legato=0.99, attack=0.09, release=0.45)

    # 7. Solo Violin: tender lyrical descant in A3 resolution (bars 24-30)
    sviolin_notes: list[NoteEvent] = []
    sviolin_notes += phrase_notes(104.0, [
        (69, 2.0, 0.64), (72, 1.0, 0.68), (74, 1.0, 0.70),
        (72, 2.0, 0.66), (69, 2.0, 0.62),
        (65, 2.0, 0.62), (64, 1.0, 0.58), (62, 1.0, 0.56),
        (62, 6.0, 0.54),
    ], legato=0.98, attack=0.08, release=0.45)

    # 8. Trombone: warm brass foundation in B climax (bars 16-23)
    tromb_notes: list[NoteEvent] = []
    for bar in range(16, 24):
        bass, _, _ = chords[bar]
        tromb_notes.append(NoteEvent(bar * 4.0, bass + 12, 3.85, 0.68, attack=0.12, release=0.40))

    # 9. Timpani & Rolls: solemn war heartbeat + crescendo swells
    timp_notes: list[NoteEvent] = []
    roll_notes: list[NoteEvent] = []
    for bar in range(0, 32):
        b = bar * 4.0
        root = 38 if chords[bar][0] not in (36, 41, 43) else chords[bar][0]
        if bar in (0, 8, 24, 30):
            timp_notes.append(NoteEvent(b, 38, 1.8, 0.62))
        elif 16 <= bar < 24:
            timp_notes.append(NoteEvent(b, root, 1.2, 0.76))
            timp_notes.append(NoteEvent(b + 2.0, 45, 0.8, 0.58))
            if bar % 2 == 1:
                timp_notes.append(NoteEvent(b + 3.5, 45, 0.5, 0.64))
        elif 8 <= bar < 16 and bar % 2 == 0:
            timp_notes.append(NoteEvent(b, root, 1.5, 0.58))
    # Swell rolls leading into B (bar 15) and A3 (bar 23)
    roll_notes.append(NoteEvent(60.0, 45, 3.9, 0.78, attack=0.8, release=0.5))
    roll_notes.append(NoteEvent(92.0, 38, 3.9, 0.76, attack=0.7, release=0.5))

    # 10. Tubular Bells: distant cathedral toll on major structural downbeats
    bell_notes = [
        NoteEvent(0.0, 62, 4.0, 0.55),
        NoteEvent(32.0, 62, 4.0, 0.58),
        NoteEvent(64.0, 65, 4.0, 0.66),
        NoteEvent(96.0, 62, 4.0, 0.56),
    ]

    # Global phrase expression curves (so sustained strings & brass truly breathe!)
    swell_arc = [
        (0.0, 0.55), (16.0, 0.72), (30.0, 0.62), (32.0, 0.75), (48.0, 0.82),
        (60.0, 0.78), (64.0, 0.96), (76.0, 1.00), (88.0, 0.94), (94.0, 0.82),
        (96.0, 0.68), (112.0, 0.60), (124.0, 0.52), (128.0, 0.55),
    ]

    tracks = [
        TrackSpec("Harp", sfz("Harp.sfz"), pan=-0.38, gain=0.62, reverb_send=0.26, humanize_ms=11, notes=harp_notes),
        TrackSpec("Contrabass", sfz("ContrabassSusVB.sfz"), pan=0.42, gain=0.68, reverb_send=0.28, humanize_ms=14, notes=cb_notes, expr_points=swell_arc),
        TrackSpec("Cellos", sfz("CelloEnsSusVib.sfz"), pan=0.28, gain=0.64, reverb_send=0.28, humanize_ms=13, notes=cello_notes, expr_points=swell_arc),
        TrackSpec("Violas", sfz("ViolaEnsSusVib.sfz"), pan=-0.08, gain=0.54, reverb_send=0.30, humanize_ms=14, notes=viola_notes, expr_points=swell_arc),
        TrackSpec("Violins", sfz("ViolinEnsSusVib.sfz"), pan=-0.46, gain=0.58, reverb_send=0.32, humanize_ms=12, notes=violin_notes, expr_points=swell_arc),
        TrackSpec("SoloViolin", sfz("SViolinVib.sfz"), pan=-0.25, gain=0.52, reverb_send=0.30, humanize_ms=10, notes=sviolin_notes),
        TrackSpec("FrenchHorn", sfz("FHornSus.sfz"), pan=0.14, gain=0.72, reverb_send=0.38, humanize_ms=11, notes=horn_notes, expr_points=swell_arc),
        TrackSpec("Trombone", sfz("TromboneSus.sfz"), pan=0.34, gain=0.46, reverb_send=0.40, humanize_ms=14, notes=tromb_notes, expr_points=swell_arc),
        TrackSpec("Timpani", sfz("Timpani.sfz"), pan=-0.18, gain=0.70, reverb_send=0.40, humanize_ms=8, notes=timp_notes),
        TrackSpec("TimpaniRolls", sfz("TimpaniRolls.sfz"), pan=-0.18, gain=0.66, reverb_send=0.42, humanize_ms=6, notes=roll_notes),
        TrackSpec("Bells", sfz("TubularBells.sfz"), pan=0.45, gain=0.38, reverb_send=0.48, humanize_ms=6, notes=bell_notes),
    ]

    # Subtle expressive rubato at major phrase cadences
    rubato = [
        (0.0, 1.00), (28.0, 1.00), (31.5, 0.95), (32.0, 1.00),
        (60.0, 1.01), (63.5, 0.96), (64.0, 1.02), (92.0, 1.02),
        (95.5, 0.94), (96.0, 0.98), (124.0, 0.96), (127.5, 0.93), (128.0, 1.00),
    ]

    print("Rendering title_ironfield...")
    stems = render_piece(
        tracks, total_beats=128.0, base_bpm=74.0, loop=True, rubato=rubato,
        reverb_decay=2.5, instrument_cache=cache, multi_stem=False, seed=101,
    )
    out_path = os.path.join(OUT_DIR, "title_ironfield.ogg")
    export_ogg(stems[0], out_path, quality=6.0)
    print(f"  wrote {out_path} ({os.path.getsize(out_path) / 1024:.0f} KB)")


def build_battle_azure(cache: dict[str, SfzInstrument]) -> None:
    """
    "Steel on the Field" (battle_azure) — D Minor/Dorian, 120 BPM, 24 bars (96 beats, 48s seamless loop).
    4 sample-locked adaptive stems:
      s0 (Deploy)  : Brooding contrabass drone, low cello spiccato pulse, distant timpani heartbeat, dark viola pads.
      s1 (Skirmish): Driving violin/viola spiccato ostinato (with harmonic motion & fills), crisp snare march.
      s2 (Melee)   : Heroic French horn & trombone battle theme, soaring cello/violin sustain counter-lines, war timpani.
      s3 (Crisis)  : Urgent violin/viola tremolos, trumpet staccato fanfares, tuba bass weight, timpani rolls.
    """
    # 24-bar progression: A (0-7), A' (8-15), B Climax (16-23)
    prog = [
        # A: Dm - Dm - Bb - C | Dm - F - Gm - A
        (38, [50, 57, 62]), (38, [50, 57, 62]), (34, [46, 53, 58]), (36, [48, 55, 60]),
        (38, [50, 57, 62]), (41, [53, 57, 60]), (43, [50, 55, 58]), (33, [45, 52, 57]),
        # A': Dm - C - Bb - C | Dm - Bb - Gm - A
        (38, [50, 57, 62]), (36, [48, 55, 60]), (34, [46, 53, 58]), (36, [48, 55, 60]),
        (38, [50, 57, 62]), (34, [46, 53, 58]), (43, [50, 55, 58]), (33, [45, 52, 57]),
        # B Climax: F - C - Dm - Bb | Gm - Dm - C - A
        (41, [53, 60, 65]), (36, [48, 55, 60]), (38, [50, 57, 62]), (34, [46, 53, 58]),
        (43, [50, 55, 58]), (38, [50, 57, 62]), (36, [48, 55, 60]), (33, [45, 52, 57]),
    ]

    # --- STEM 0: DEPLOY ---
    s0_cb, s0_cello_spic, s0_viola_pad, s0_timp = [], [], [], []
    for bar, (bass, triad) in enumerate(prog):
        b = bar * 4.0
        s0_cb.append(NoteEvent(b, bass, 3.9, 0.72, attack=0.10, release=0.32))
        if bar % 2 == 0:
            s0_viola_pad.append(NoteEvent(b, triad[2], 7.8, 0.52, attack=0.25, release=0.40))
        # Taut quarter/eighth cello pulse
        for off, v in [(0.0, 0.74), (1.0, 0.56), (2.0, 0.68), (2.5, 0.52), (3.0, 0.62)]:
            s0_cello_spic.append(NoteEvent(b + off, triad[0], 0.35, v))
        # Distant war-drum heartbeat
        s0_timp.append(NoteEvent(b, bass if bass <= 43 else 38, 0.9, 0.68))
        if bar % 4 == 3:
            s0_timp.append(NoteEvent(b + 3.0, 45, 0.5, 0.58))
            s0_timp.append(NoteEvent(b + 3.5, 45, 0.5, 0.66))

    # --- STEM 1: SKIRMISH ---
    s1_vln_spic, s1_vla_spic, s1_perc = [], [], []
    for bar, (bass, triad) in enumerate(prog):
        b = bar * 4.0
        # Driving 8th-note spiccato engine with accented downbeats & syncopation
        r0, r1, r2 = triad[0] + 12, triad[1] + 12, triad[2] + 12
        pat = [
            (0.0, r0, 0.80), (0.5, r0, 0.58), (1.0, r1, 0.70), (1.5, r0, 0.58),
            (2.0, r2, 0.78), (2.5, r1, 0.60), (3.0, r0, 0.72), (3.5, r1, 0.62),
        ]
        if bar % 4 == 3:
            # Turnaround ascent
            pat = [
                (0.0, r0, 0.82), (0.5, r1, 0.66), (1.0, r2, 0.76), (1.5, r1, 0.64),
                (2.0, r0, 0.80), (2.5, r1, 0.70), (3.0, r2, 0.82), (3.5, r2 + 2, 0.76),
            ]
        for off, m, v in pat:
            s1_vln_spic.append(NoteEvent(b + off, m, 0.30, v))
            s1_vla_spic.append(NoteEvent(b + off, m - 12, 0.32, v * 0.82))
        # Snare / percussion march pattern (GM-StylePerc: 38=snare, 36=bass drum)
        for off, m, v in [(0.0, 36, 0.72), (1.0, 38, 0.56), (2.0, 36, 0.66), (3.0, 38, 0.62), (3.5, 38, 0.48)]:
            s1_perc.append(NoteEvent(b + off, m, 0.25, v))
        if bar % 4 == 3:
            for off, v in [(3.0, 0.66), (3.25, 0.58), (3.5, 0.72), (3.75, 0.78)]:
                s1_perc.append(NoteEvent(b + off, 38, 0.20, v))

    # --- STEM 2: MELEE ---
    s2_horn = (
        # A theme (bars 0-7)
        phrase_notes(0.0, [
            (50, 1.5, 0.84), (53, 0.5, 0.76), (57, 2.0, 0.88),
            (55, 1.5, 0.82), (53, 0.5, 0.74), (50, 2.0, 0.82),
            (58, 2.0, 0.86), (57, 1.0, 0.80), (55, 1.0, 0.78),
            (57, 3.0, 0.86), (0, 1.0, 0.0),
            (62, 1.5, 0.88), (60, 0.5, 0.80), (57, 1.0, 0.84), (55, 1.0, 0.80),
            (53, 2.0, 0.84), (57, 2.0, 0.82),
            (58, 2.0, 0.84), (55, 1.0, 0.78), (53, 1.0, 0.76),
            (52, 3.2, 0.84), (0, 0.8, 0.0),
        ], legato=0.95, attack=0.04, release=0.28)
        # A' theme (bars 8-15)
        + phrase_notes(32.0, [
            (57, 1.5, 0.86), (60, 0.5, 0.80), (62, 2.0, 0.90),
            (60, 1.5, 0.84), (57, 0.5, 0.78), (55, 2.0, 0.82),
            (58, 2.0, 0.86), (60, 1.0, 0.82), (62, 1.0, 0.86),
            (60, 3.0, 0.88), (0, 1.0, 0.0),
            (62, 1.5, 0.90), (64, 0.5, 0.84), (65, 2.0, 0.92),
            (62, 1.5, 0.86), (60, 0.5, 0.80), (58, 2.0, 0.84),
            (55, 2.0, 0.82), (57, 1.0, 0.80), (58, 1.0, 0.82),
            (57, 3.2, 0.88), (0, 0.8, 0.0),
        ], legato=0.95, attack=0.04, release=0.28)
        # B Climax (bars 16-23)
        + phrase_notes(64.0, [
            (65, 2.0, 0.92), (64, 1.0, 0.86), (62, 1.0, 0.86),
            (60, 2.0, 0.90), (62, 1.0, 0.84), (64, 1.0, 0.88),
            (62, 2.5, 0.92), (60, 0.5, 0.84), (57, 1.0, 0.86),
            (58, 3.0, 0.88), (0, 1.0, 0.0),
            (58, 1.5, 0.86), (60, 0.5, 0.82), (62, 2.0, 0.90),
            (65, 2.0, 0.92), (62, 2.0, 0.88),
            (60, 2.0, 0.88), (58, 1.0, 0.84), (57, 1.0, 0.84),
            (57, 3.4, 0.90), (0, 0.6, 0.0),
        ], legato=0.96, attack=0.04, release=0.30)
    )
    s2_tromb, s2_cello_sus, s2_timp = [], [], []
    for bar, (bass, triad) in enumerate(prog):
        b = bar * 4.0
        s2_tromb.append(NoteEvent(b, bass + 12, 3.7, 0.74, attack=0.07, release=0.30))
        s2_cello_sus.append(NoteEvent(b, triad[1], 3.85, 0.72, attack=0.09, release=0.32))
        # Heavy battle percussion accent
        s2_timp.append(NoteEvent(b, bass if bass <= 43 else 38, 0.8, 0.84))
        s2_timp.append(NoteEvent(b + 1.5, 45, 0.5, 0.66))
        s2_timp.append(NoteEvent(b + 2.0, bass if bass <= 43 else 38, 0.8, 0.80))

    # --- STEM 3: CRISIS ---
    s3_vln_trem, s3_tpt, s3_tuba, s3_rolls = [], [], [], []
    for bar, (bass, triad) in enumerate(prog):
        b = bar * 4.0
        s3_vln_trem.append(NoteEvent(b, triad[2] + 12, 3.92, 0.76, attack=0.08, release=0.25))
        s3_tuba.append(NoteEvent(b, bass, 3.8, 0.74, attack=0.07, release=0.30))
        if bar % 4 == 3:
            s3_rolls.append(NoteEvent(b + 2.0, 45, 1.95, 0.84, attack=0.35, release=0.35))
    # Urgent trumpet calls on key bars
    s3_tpt += phrase_notes(12.0, [(69, 0.5, 0.84), (69, 0.5, 0.86), (72, 1.0, 0.90), (69, 1.5, 0.86)], legato=0.88)
    s3_tpt += phrase_notes(28.0, [(69, 0.5, 0.86), (72, 0.5, 0.88), (74, 1.5, 0.92), (76, 1.0, 0.90)], legato=0.88)
    s3_tpt += phrase_notes(64.0, [
        (77, 1.5, 0.90), (76, 0.5, 0.84), (74, 2.0, 0.90),
        (72, 2.0, 0.88), (74, 1.0, 0.86), (76, 1.0, 0.88),
        (74, 3.0, 0.92), (0, 1.0, 0.0),
    ], legato=0.92)
    s3_tpt += phrase_notes(88.0, [(72, 1.0, 0.88), (74, 1.0, 0.90), (76, 1.5, 0.92), (74, 0.5, 0.86), (76, 3.2, 0.92)], legato=0.92)

    tracks = [
        # Stem 0
        TrackSpec("S0_CB", sfz("ContrabassSusVB.sfz"), pan=0.38, gain=0.68, reverb_send=0.28, stem=0, humanize_ms=10, notes=s0_cb),
        TrackSpec("S0_CelloSpic", sfz("CelloEnsSpic.sfz"), pan=0.25, gain=0.62, reverb_send=0.24, stem=0, humanize_ms=8, notes=s0_cello_spic),
        TrackSpec("S0_ViolaPad", sfz("ViolaEnsSusVib.sfz"), pan=-0.12, gain=0.46, reverb_send=0.32, stem=0, humanize_ms=12, notes=s0_viola_pad),
        TrackSpec("S0_Timp", sfz("Timpani.sfz"), pan=-0.15, gain=0.66, reverb_send=0.38, stem=0, humanize_ms=6, notes=s0_timp),
        # Stem 1
        TrackSpec("S1_VlnSpic", sfz("ViolinEnsSpic.sfz"), pan=-0.42, gain=0.60, reverb_send=0.24, stem=1, humanize_ms=7, notes=s1_vln_spic),
        TrackSpec("S1_VlaSpic", sfz("ViolaEnsSpic.sfz"), pan=-0.10, gain=0.54, reverb_send=0.24, stem=1, humanize_ms=7, notes=s1_vla_spic),
        TrackSpec("S1_Perc", sfz("GM-StylePerc.sfz"), pan=0.10, gain=0.52, reverb_send=0.34, stem=1, humanize_ms=6, notes=s1_perc),
        # Stem 2
        TrackSpec("S2_Horn", sfz("FHornSus.sfz"), pan=0.16, gain=0.72, reverb_send=0.36, stem=2, humanize_ms=9, notes=s2_horn),
        TrackSpec("S2_Tromb", sfz("TromboneSus.sfz"), pan=0.34, gain=0.52, reverb_send=0.38, stem=2, humanize_ms=10, notes=s2_tromb),
        TrackSpec("S2_CelloSus", sfz("CelloEnsSusVib.sfz"), pan=0.26, gain=0.52, reverb_send=0.28, stem=2, humanize_ms=11, notes=s2_cello_sus),
        TrackSpec("S2_Timp", sfz("Timpani.sfz"), pan=-0.18, gain=0.68, reverb_send=0.38, stem=2, humanize_ms=6, notes=s2_timp),
        # Stem 3
        TrackSpec("S3_VlnTrem", sfz("ViolinEnsTrem.sfz"), pan=-0.46, gain=0.48, reverb_send=0.32, stem=3, humanize_ms=10, notes=s3_vln_trem),
        TrackSpec("S3_Trumpet", sfz("TrumpetStac.sfz"), pan=0.28, gain=0.56, reverb_send=0.40, stem=3, humanize_ms=8, notes=s3_tpt),
        TrackSpec("S3_Tuba", sfz("TubaSus.sfz"), pan=0.42, gain=0.54, reverb_send=0.38, stem=3, humanize_ms=10, notes=s3_tuba),
        TrackSpec("S3_Rolls", sfz("TimpaniRolls.sfz"), pan=-0.18, gain=0.62, reverb_send=0.42, stem=3, humanize_ms=5, notes=s3_rolls),
    ]

    print("Rendering battle_azure (4 stems)...")
    stems = render_piece(
        tracks, total_beats=96.0, base_bpm=120.0, loop=True,
        reverb_decay=2.0, instrument_cache=cache, multi_stem=True, seed=202,
    )
    for s in range(4):
        out_path = os.path.join(OUT_DIR, f"battle_azure_s{s}.ogg")
        export_ogg(stems[s], out_path, quality=6.0)
        print(f"  wrote {out_path} ({os.path.getsize(out_path) / 1024:.0f} KB)")


if __name__ == "__main__":
    cache: dict[str, SfzInstrument] = {}
    build_title_ironfield(cache)
    build_battle_azure(cache)
    print("Done.")
