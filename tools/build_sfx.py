#!/usr/bin/env python3
"""
Reproducible open-source (CC0) SFX & ambient bed builder for Ironfield.

Reads raw CC0 recordings from ~/.cache/ironfield-audio/sfx-raw/ (and CC0 VSCO 2 CE brass/percussion
for war horns & rite drums), trims leading/trailing silence, normalizes loudness to clean target peaks,
creates seamless crossfaded bed loops, and encodes to compact 44.1 kHz mono OGG Vorbis in:
  - Data/audio/sfx/<Cue>_<idx>.ogg
  - Data/audio/beds/<Bed>.ogg
Also writes Data/audio/CREDITS_AUDIO.md documenting all CC0 sources.
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile

import numpy as np
import soundfile as sf

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sfz_engine import NoteEvent, SfzInstrument, TrackSpec, render_piece

SR = 44100
RAW_DIR = os.path.expanduser("~/.cache/ironfield-audio/sfx-raw")
VSCO_DIR = os.path.expanduser("~/.cache/ironfield-audio/VSCO-2-CE")
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SFX_OUT = os.path.join(REPO_ROOT, "Data", "audio", "sfx")
BED_OUT = os.path.join(REPO_ROOT, "Data", "audio", "beds")
CREDITS_OUT = os.path.join(REPO_ROOT, "Data", "audio", "CREDITS_AUDIO.md")


def load_mono(path: str) -> np.ndarray:
    """Decode any audio file (wav/ogg/flac/mp3) to 44.1 kHz mono float32 via ffmpeg."""
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tf:
        wav_path = tf.name
    try:
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-ac", "1", "-ar", str(SR), "-sample_fmt", "s16", wav_path],
            check=True,
        )
        data, _ = sf.read(wav_path, dtype="float32")
        return data
    finally:
        if os.path.exists(wav_path):
            os.remove(wav_path)


def trim_and_norm(x: np.ndarray, peak: float = 0.85, max_sec: float = 2.5, trim_db: float = -42.0) -> np.ndarray:
    if len(x) == 0:
        return x
    mx = float(np.max(np.abs(x)))
    if mx < 1e-6:
        return x
    thresh = mx * (10.0 ** (trim_db / 20.0))
    idx = np.flatnonzero(np.abs(x) >= thresh)
    if len(idx) > 0:
        i0 = max(0, int(idx[0]) - int(0.006 * SR))
        i1 = min(len(x), int(idx[-1]) + int(0.04 * SR))
        x = x[i0:i1]
    max_n = int(round(max_sec * SR))
    if len(x) > max_n:
        x = x[:max_n].copy()
        fade = min(len(x) // 4, int(0.08 * SR))
        x[-fade:] *= np.linspace(1.0, 0.0, fade, dtype=np.float32)
    # Gentle 3ms de-click edges
    edge = min(len(x) // 8, int(0.003 * SR))
    if edge > 1:
        x[:edge] *= np.linspace(0.0, 1.0, edge, dtype=np.float32)
        x[-edge:] *= np.linspace(1.0, 0.0, edge, dtype=np.float32)
    mx = float(np.max(np.abs(x)))
    if mx > 1e-6:
        x = x * (peak / mx)
    return x.astype(np.float32)


def make_seamless_loop(x: np.ndarray, target_sec: float = 12.0, xfade_sec: float = 1.2, rms_target: float = 0.14) -> np.ndarray:
    """Create a click-free crossfaded mono loop of roughly target_sec."""
    xf = int(round(xfade_sec * SR))
    want = int(round(target_sec * SR))
    if len(x) < xf * 3:
        # Repeat if short
        reps = max(2, (want + xf) // max(1, len(x)) + 1)
        x = np.tile(x, reps)
    if len(x) > want + xf:
        x = x[: want + xf]
    body = x[:-xf].copy()
    tail = x[-xf:]
    alpha = np.linspace(0.0, 1.0, xf, dtype=np.float32)
    body[:xf] = body[:xf] * alpha + tail * (1.0 - alpha)
    rms = float(np.sqrt(np.mean(body ** 2))) + 1e-9
    body *= min(4.0, rms_target / rms)
    mx = float(np.max(np.abs(body)))
    if mx > 0.92:
        body *= 0.92 / mx
    return body.astype(np.float32)


def save_ogg_mono(x: np.ndarray, out_path: str, quality: float = 4.5) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tf:
        wav_path = tf.name
    try:
        sf.write(wav_path, x, SR, subtype="PCM_16")
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, "-c:a", "libvorbis", "-q:a", str(quality), out_path],
            check=True,
        )
    finally:
        if os.path.exists(wav_path):
            os.remove(wav_path)


def raw(rel: str) -> str:
    return os.path.join(RAW_DIR, rel)


# Cue -> (peak, max_sec, [relative paths in RAW_DIR])
CUE_MAP: dict[str, tuple[float, float, list[str]]] = {
    # --- Melee steel, slashes, blocks & parries ---
    "Clash": (0.85, 1.1, [
        "swords/sword_clash.1.ogg", "swords/sword_clash.2.ogg",
        "swords/sword_clash.3.ogg", "swords/sword_clash.4.ogg",
    ]),
    "ParrySteel": (0.88, 1.2, [
        "swords/sword_clash.5.ogg", "swords/sword_clash.6.ogg",
        "swords/sword_clash.7.ogg", "swords/sword_clash.8.ogg",
    ]),
    "BlockMetal": (0.86, 1.0, [
        "swords/sword_clash.9.ogg", "swords/sword_clash.10.ogg",
        "rpg_sound_pack/RPG Sound Pack/inventory/metal-ringing.wav",
    ]),
    "SlashLight": (0.84, 0.9, [
        "swords/sword - StarNinjas/sword.1.ogg", "swords/sword - StarNinjas/sword.2.ogg",
        "swords/sword - StarNinjas/sword.3.ogg", "kenney_rpg/OGG/knifeSlice.ogg",
    ]),
    "SlashHeavy": (0.90, 1.1, [
        "swords/sword - StarNinjas/sword.4.ogg", "swords/sword - StarNinjas/sword.5.ogg",
        "swords/sword - StarNinjas/sword.6.ogg", "kenney_rpg/OGG/chop.ogg",
    ]),
    "PierceHit": (0.85, 0.9, [
        "swords/sword - StarNinjas/sword.7.ogg", "swords/sword - StarNinjas/sword.8.ogg",
        "kenney_rpg/OGG/knifeSlice2.ogg",
    ]),
    "SwingOne": (0.76, 0.7, [
        "rpg_sound_pack/RPG Sound Pack/battle/swing.wav",
        "rpg_sound_pack/RPG Sound Pack/battle/swing2.wav",
        "rpg_sound_pack/RPG Sound Pack/battle/swing3.wav",
    ]),
    "SwingTwo": (0.80, 0.8, [
        "swords/sword - StarNinjas/sword.9.ogg", "swords/sword - StarNinjas/sword.10.ogg",
        "rpg_sound_pack/RPG Sound Pack/battle/swing3.wav",
    ]),
    "WeaponSwap": (0.80, 0.9, [
        "rpg_sound_pack/RPG Sound Pack/battle/sword-unsheathe.wav",
        "rpg_sound_pack/RPG Sound Pack/battle/sword-unsheathe2.wav",
        "rpg_sound_pack/RPG Sound Pack/battle/sword-unsheathe3.wav",
        "kenney_rpg/OGG/drawKnife1.ogg",
    ]),
    "Hit": (0.86, 0.8, [
        "hits_punches/hits/hit01.mp3.flac", "hits_punches/hits/hit02.mp3.flac",
        "hits_punches/hits/hit03.mp3.flac", "hits_punches/hits/hit04.mp3.flac",
    ]),
    "BluntFlesh": (0.88, 0.8, [
        "hits_punches/hits/hit05.mp3.flac", "hits_punches/hits/hit06.mp3.flac",
        "hits_punches/hits/hit07.mp3.flac", "hits_punches/hits/hit08.mp3.flac",
    ]),
    "BluntPlate": (0.90, 0.9, [
        "hits_punches/hits/hit10.mp3.flac", "hits_punches/hits/hit11.mp3.flac",
        "kenney_rpg/OGG/metalPot1.ogg", "kenney_rpg/OGG/metalPot2.ogg",
    ]),
    "BlockWood": (0.84, 0.8, [
        "hits_punches/hits/hit15.mp3.flac", "hits_punches/hits/hit16.mp3.flac",
        "rpg_sound_pack/RPG Sound Pack/inventory/wood-small.wav",
        "kenney_rpg/OGG/doorClose_1.ogg",
    ]),
    "Block": (0.84, 0.8, [
        "hits_punches/hits/hit18.mp3.flac", "hits_punches/hits/hit19.mp3.flac",
        "hits_punches/hits/hit20.mp3.flac",
    ]),
    "BashThud": (0.92, 0.9, [
        "hits_punches/hits/hit25.mp3.flac", "hits_punches/hits/hit26.mp3.flac",
        "hits_punches/hits/hit27.mp3.flac",
    ]),
    "Impact": (0.92, 1.0, [
        "hits_punches/hits/hit30.mp3.flac", "hits_punches/hits/hit31.mp3.flac",
        "hits_punches/hits/hit32.mp3.flac",
    ]),
    "LanceImpact": (0.94, 1.1, [
        "hits_punches/hits/hit33.mp3.flac", "hits_punches/hits/hit34.mp3.flac",
        "hits_punches/hits/hit35.mp3.flac",
    ]),
    "Ram": (0.94, 1.3, [
        "hits_punches/hits/hit36.mp3.flac", "hits_punches/hits/hit37.mp3.flac",
        "kenney_rpg/OGG/doorClose_3.ogg",
    ]),
    "ArrowFlesh": (0.82, 0.6, [
        "hits_punches/hits/hit12.mp3.flac", "hits_punches/hits/hit13.mp3.flac",
        "hits_punches/hits/hit14.mp3.flac",
    ]),
    "ArrowShield": (0.84, 0.7, [
        "hits_punches/hits/hit21.mp3.flac", "hits_punches/hits/hit22.mp3.flac",
        "rpg_sound_pack/RPG Sound Pack/inventory/wood-small.wav",
    ]),
    "ArrowPlate": (0.84, 0.7, [
        "kenney_rpg/OGG/metalClick.ogg", "kenney_rpg/OGG/metalLatch.ogg",
        "rpg_sound_pack/RPG Sound Pack/inventory/metal-small1.wav",
    ]),

    # --- UI, Armoury, Scrolls, Coins, Steps ---
    "Click": (0.72, 0.4, [
        "rpg_sound_pack/RPG Sound Pack/interface/interface1.wav",
        "kenney_rpg/OGG/metalClick.ogg",
    ]),
    "Hover": (0.45, 0.25, [
        "kenney_rpg/OGG/cloth1.ogg", "kenney_rpg/OGG/cloth2.ogg",
    ]),
    "Page": (0.75, 0.7, [
        "kenney_rpg/OGG/bookFlip1.ogg", "kenney_rpg/OGG/bookFlip2.ogg", "kenney_rpg/OGG/bookFlip3.ogg",
    ]),
    "ScrollUnroll": (0.78, 0.9, [
        "kenney_rpg/OGG/bookOpen.ogg", "kenney_rpg/OGG/bookPlace1.ogg", "kenney_rpg/OGG/bookPlace2.ogg",
    ]),
    "Coin": (0.80, 0.8, [
        "rpg_sound_pack/RPG Sound Pack/inventory/coin.wav",
        "rpg_sound_pack/RPG Sound Pack/inventory/coin2.wav",
        "rpg_sound_pack/RPG Sound Pack/inventory/coin3.wav",
        "kenney_rpg/OGG/handleCoins.ogg",
    ]),
    "EquipBlade": (0.84, 0.9, [
        "rpg_sound_pack/RPG Sound Pack/battle/sword-unsheathe4.wav",
        "rpg_sound_pack/RPG Sound Pack/battle/sword-unsheathe5.wav",
        "kenney_rpg/OGG/drawKnife2.ogg", "kenney_rpg/OGG/drawKnife3.ogg",
    ]),
    "EquipArmor": (0.84, 1.0, [
        "rpg_sound_pack/RPG Sound Pack/inventory/chainmail1.wav",
        "rpg_sound_pack/RPG Sound Pack/inventory/chainmail2.wav",
        "rpg_sound_pack/RPG Sound Pack/inventory/armor-light.wav",
    ]),
    "HorseTack": (0.80, 0.9, [
        "kenney_rpg/OGG/beltHandle1.ogg", "kenney_rpg/OGG/beltHandle2.ogg",
        "kenney_rpg/OGG/handleSmallLeather.ogg", "kenney_rpg/OGG/dropLeather.ogg",
    ]),
    "AnvilRing": (0.86, 1.4, [
        "rpg_sound_pack/RPG Sound Pack/inventory/metal-ringing.wav",
        "kenney_rpg/OGG/metalPot3.ogg",
    ]),
    "GravelStep": (0.68, 0.45, [
        "kenney_rpg/OGG/footstep00.ogg", "kenney_rpg/OGG/footstep01.ogg",
        "kenney_rpg/OGG/footstep02.ogg", "kenney_rpg/OGG/footstep03.ogg",
    ]),
    "SnowCrunch": (0.66, 0.45, [
        "kenney_rpg/OGG/footstep05.ogg", "kenney_rpg/OGG/footstep06.ogg",
        "kenney_rpg/OGG/footstep07.ogg", "kenney_rpg/OGG/footstep08.ogg",
    ]),
    "Rustle": (0.65, 0.7, [
        "kenney_rpg/OGG/cloth3.ogg", "kenney_rpg/OGG/cloth4.ogg",
        "rpg_sound_pack/RPG Sound Pack/inventory/cloth-heavy.wav",
    ]),
    "EngineWinch": (0.82, 1.1, [
        "kenney_rpg/OGG/creak1.ogg", "kenney_rpg/OGG/creak2.ogg", "kenney_rpg/OGG/creak3.ogg",
    ]),

    # --- Monsters, Beasts & Magic ---
    "TrollRoar": (0.90, 1.8, [
        "rpg_sound_pack/RPG Sound Pack/NPC/giant/giant1.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/giant/giant2.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/giant/giant3.wav",
    ]),
    "BearRoar": (0.90, 1.6, [
        "rpg_sound_pack/RPG Sound Pack/NPC/ogre/ogre1.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/ogre/ogre2.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/ogre/ogre3.wav",
    ]),
    "WolfSnarl": (0.84, 1.2, [
        "rpg_sound_pack/RPG Sound Pack/NPC/misc/wolfman.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/gutteral beast/mnstr1.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/gutteral beast/mnstr2.wav",
    ]),
    "SpiderChitter": (0.82, 0.9, [
        "rpg_sound_pack/RPG Sound Pack/NPC/beetle/bite-small.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/beetle/bite-small2.wav",
        "rpg_sound_pack/RPG Sound Pack/NPC/beetle/bite-small3.wav",
    ]),
    "Spell": (0.84, 1.5, [
        "rpg_sound_pack/RPG Sound Pack/battle/magic1.wav",
        "rpg_sound_pack/RPG Sound Pack/battle/spell.wav",
    ]),
}


def build_sfz_horns() -> None:
    """Render authentic medieval brass war horns & rite drums from CC0 VSCO 2 CE."""
    if not os.path.isdir(VSCO_DIR):
        return
    cache: dict[str, SfzInstrument] = {}
    fhorn = os.path.join(VSCO_DIR, "FHornSus.sfz")
    tuba = os.path.join(VSCO_DIR, "TubaSus.sfz")
    timp = os.path.join(VSCO_DIR, "Timpani.sfz")

    calls = {
        "Horn": ([NoteEvent(0.0, 50, 1.4, 0.88), NoteEvent(0.0, 38, 1.4, 0.72)], 2.2),
        "HornCharge": ([NoteEvent(0.0, 50, 0.45, 0.86), NoteEvent(0.5, 57, 0.45, 0.90), NoteEvent(1.0, 62, 1.1, 0.95)], 2.6),
        "HornHold": ([NoteEvent(0.0, 57, 0.7, 0.86), NoteEvent(0.75, 50, 1.2, 0.88)], 2.4),
        "HornRetreat": ([NoteEvent(0.0, 62, 0.5, 0.86), NoteEvent(0.55, 57, 0.5, 0.82), NoteEvent(1.1, 50, 1.0, 0.80)], 2.6),
    }
    for cue, (notes, beats) in calls.items():
        tracks = [
            TrackSpec("Horn", fhorn, pan=0.0, gain=0.85, reverb_send=0.35, humanize_ms=0, notes=[n for n in notes if n.midi >= 45]),
            TrackSpec("Low", tuba, pan=0.0, gain=0.60, reverb_send=0.38, humanize_ms=0, notes=[n for n in notes if n.midi < 45] or [NoteEvent(0.0, 38, beats * 0.6, 0.55)]),
        ]
        stems = render_piece(tracks, total_beats=beats, base_bpm=90.0, loop=False, reverb_decay=1.6, instrument_cache=cache, seed=301)
        mono = trim_and_norm(0.5 * (stems[0][:, 0] + stems[0][:, 1]), peak=0.90, max_sec=2.8)
        save_ogg_mono(mono, os.path.join(SFX_OUT, f"{cue}_0.ogg"), quality=5.0)
        print(f"  horn {cue}_0.ogg")

    # RiteDrum from real CC0 Timpani
    drum_tracks = [TrackSpec("Timp", timp, pan=0.0, gain=0.95, reverb_send=0.35, humanize_ms=0,
                             notes=[NoteEvent(0.0, 38, 1.2, 0.92), NoteEvent(0.6, 38, 1.2, 0.78)])]
    stems = render_piece(drum_tracks, total_beats=2.0, base_bpm=100.0, loop=False, reverb_decay=1.5, instrument_cache=cache, seed=302)
    mono = trim_and_norm(0.5 * (stems[0][:, 0] + stems[0][:, 1]), peak=0.90, max_sec=2.0)
    save_ogg_mono(mono, os.path.join(SFX_OUT, "RiteDrum_0.ogg"), quality=5.0)
    print("  drum RiteDrum_0.ogg")


def build_beds() -> None:
    amb_dir = raw("ambience")
    bed_sources = {
        "Rain": ("amb-rain-loop-1", 12.0, 0.14),
        "Wind": ("mild-wind-background-noise", 14.0, 0.14),
        "Gallop": ("horse-trotting", 8.0, 0.18),
    }
    if not os.path.isdir(amb_dir):
        return
    files = os.listdir(amb_dir)
    for bed, (prefix, sec, rms) in bed_sources.items():
        match = next((f for f in files if f.startswith(prefix)), None)
        if not match:
            continue
        x = load_mono(os.path.join(amb_dir, match))
        loop = make_seamless_loop(x, target_sec=sec, xfade_sec=1.0, rms_target=rms)
        save_ogg_mono(loop, os.path.join(BED_OUT, f"{bed}.ogg"), quality=4.5)
        print(f"  bed {bed}.ogg ({len(loop) / SR:.1f}s)")


def write_credits() -> None:
    os.makedirs(os.path.dirname(CREDITS_OUT), exist_ok=True)
    with open(CREDITS_OUT, "w", encoding="utf-8") as f:
        f.write("""# Open-Source Audio Credits (All CC0 1.0 Public Domain)

All external audio assets used in Ironfield's SFX, ambient beds, and offline orchestral sample rendering are **CC0 1.0 Universal (Public Domain)**:

- **VSCO 2 Community Edition (SFZ)** — Versilian Studios (`sgossner/VSCO-2-CE`), CC0. Used offline by `tools/sfz_engine.py` to render orchestral & folk music and war horns.
- **RPG Sound Pack** — ArtisticDude (OpenGameArt), CC0. Sword swings, unsheathe, chainmail/armour, coins, metal/wood, magic, monster vocals, UI.
- **50 RPG Sound Effects** — Kenney.nl (OpenGameArt), CC0. Parchment/book flips, footsteps, leather/cloth, knife slices, coins, creaks, metal latches.
- **20 Sword Sound Effects (Attacks and Clashes)** — StarNinjas (OpenGameArt), CC0. Steel clashes, parries, and sword slashes.
- **37 Hits/Punches** — Independent.nu (OpenGameArt), CC0. Melee flesh/wood/plate/lance/ram impacts.
- **Rain, Wind, and Horse Trotting Ambience** — OpenGameArt CC0 submissions (`amb-rain-loop-1`, `mild-wind-background-noise`, `horse-trotting`).
""")


def main() -> None:
    os.makedirs(SFX_OUT, exist_ok=True)
    os.makedirs(BED_OUT, exist_ok=True)
    built = 0
    for cue, (peak, max_sec, rels) in CUE_MAP.items():
        idx = 0
        for rel in rels:
            p = raw(rel)
            if not os.path.isfile(p):
                print(f"  [skip missing] {p}")
                continue
            x = trim_and_norm(load_mono(p), peak=peak, max_sec=max_sec)
            save_ogg_mono(x, os.path.join(SFX_OUT, f"{cue}_{idx}.ogg"), quality=4.5)
            idx += 1
        if idx > 0:
            built += 1
    print(f"Built {built} CC0 SFX cues into {SFX_OUT}")
    build_sfz_horns()
    build_beds()
    write_credits()
    print("Done.")


if __name__ == "__main__":
    main()
