#!/usr/bin/env python3
"""Composites the real MonoGame Puppet.cs turntable + combat poses + modular part sprites into puppet_showcase.png."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Data", "sprites", "test_character")
RENDERS = os.path.join(BASE, "puppet_renders")
PARTS = os.path.join(BASE, "parts")


def draw_bg(w, h):
    yy = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None]
    xx = np.linspace(0.0, 1.0, w, dtype=np.float32)[None, :]
    sky_top = np.array([18, 22, 34], dtype=np.float32)
    sky_mid = np.array([34, 42, 60], dtype=np.float32)
    sky_hor = np.array([94, 68, 54], dtype=np.float32)
    gnd_top = np.array([40, 52, 38], dtype=np.float32)
    gnd_bot = np.array([18, 22, 26], dtype=np.float32)
    hor = 0.40
    t_sky = np.clip(yy / hor, 0.0, 1.0)
    sky = np.where(
        t_sky[..., None] < 0.65,
        sky_top * (1 - t_sky[..., None] / 0.65) + sky_mid * (t_sky[..., None] / 0.65),
        sky_mid * (1 - (t_sky[..., None] - 0.65) / 0.35) + sky_hor * ((t_sky[..., None] - 0.65) / 0.35),
    )
    t_gnd = np.clip((yy - hor) / (1.0 - hor), 0.0, 1.0)
    gnd = gnd_top * (1 - t_gnd[..., None]) + gnd_bot * t_gnd[..., None]
    glow = np.exp(-((xx - 0.5)**2 / 0.22 + (yy - 0.32)**2 / 0.09)) * 24.0
    bg = np.broadcast_to(np.where(yy[..., None] < hor, sky, gnd), (h, w, 3)).copy()
    bg[..., 0] += glow * 1.15
    bg[..., 1] += glow * 0.85
    bg[..., 2] += glow * 0.45
    return Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8), mode="RGB").convert("RGBA")


def paste_shadow(canvas, img, cx, bottom_y, scale=1, shadow_alpha=125):
    w, h = img.size
    rw, rh = int(w * scale), int(h * scale)
    im = img.resize((rw, rh), Image.Resampling.NEAREST) if scale != 1 else img
    sh_w, sh_h = int(rw * 0.42), max(6, int(rw * 0.11))
    yy = (np.arange(sh_h)[:, None] + 0.5) / sh_h * 2.0 - 1.0
    xx = (np.arange(sh_w)[None, :] + 0.5) / sh_w * 2.0 - 1.0
    a = np.clip(1.0 - (xx*xx + yy*yy), 0.0, 1.0) ** 1.4 * shadow_alpha
    sh = np.zeros((sh_h, sh_w, 4), dtype=np.uint8)
    sh[..., 0], sh[..., 1], sh[..., 2], sh[..., 3] = 10, 10, 16, a.astype(np.uint8)
    canvas.alpha_composite(Image.fromarray(sh, mode="RGBA"), (cx - sh_w // 2, bottom_y - sh_h // 2 - int(18 * scale)))
    canvas.alpha_composite(im, (cx - rw // 2, bottom_y - rh))


def main():
    W, H = 1920, 1140
    bg = draw_bg(W, H)
    draw = ImageDraw.Draw(bg)

    font_title = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 25)
    font_sub = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Regular.ttf", 14)
    font_lbl = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 12)
    font_sm = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Regular.ttf", 11)

    draw.text((36, 22), "ASHEN SUN VANGUARD  -  LIVE IRONFIELD PUPPET.CS RENDER", fill=(244, 214, 132, 255), font=font_title)
    draw.text((36, 56), "18 Modular Sprites (Exact Art.Px = 0.03m/px Rig Sizes)  |  Rendered Directly Through Ironfield's MonoGame Puppet.cs 3D Skeleton", fill=(182, 192, 212, 255), font=font_sub)
    draw.line([(36, 82), (W - 36, 82)], fill=(208, 152, 46, 120), width=1)

    # Top row: 5 live Puppet.cs poses at 1.4x crisp scale (220x260 -> 308x364)
    poses = [
        ("puppet_045.png",  "READY STANCE (45°)"),
        ("pose_walk.png",   "MARCH STRIDE"),
        ("pose_swing.png",  "SWORD WINDUP"),
        ("pose_strike.png", "OVERHEAD STRIKE"),
        ("pose_block.png",  "SHIELD GUARD"),
    ]
    card_w = 350
    gap = 24
    start_x = (W - (5 * card_w + 4 * gap)) // 2
    for i, (fname, lbl) in enumerate(poses):
        x0 = start_x + i * (card_w + gap)
        cx = x0 + card_w // 2
        draw.rounded_rectangle([x0, 98, x0 + card_w, 488], radius=10, fill=(16, 20, 30, 125), outline=(208, 152, 46, 85), width=1)
        im = Image.open(os.path.join(RENDERS, fname)).convert("RGBA")
        paste_shadow(bg, im, cx, 462, scale=1.35, shadow_alpha=135)
        bb = draw.textbbox((0, 0), lbl, font=font_lbl)
        draw.text((cx - (bb[2] - bb[0]) // 2, 464), lbl, fill=(230, 216, 190, 255), font=font_lbl)

    # Middle row: Full 8-Angle 360° Puppet.cs Turntable
    draw.text((36, 504), "FULL 360° LIVE PUPPET.CS TURNTABLE (8 CAMERA ANGLES)", fill=(244, 214, 132, 255), font=font_lbl)
    draw.rounded_rectangle([28, 526, W - 28, 842], radius=10, fill=(16, 20, 30, 140), outline=(146, 166, 194, 70), width=1)
    angles = [0, 45, 90, 135, 180, 225, 270, 315]
    names = ["0° FRONT", "45° FR-R", "90° RIGHT", "135° BK-R", "180° BACK", "225° BK-L", "270° LEFT", "315° FR-L"]
    cell_w = (W - 72) // 8
    for i, (deg, nm) in enumerate(zip(angles, names)):
        cx = 36 + i * cell_w + cell_w // 2
        im = Image.open(os.path.join(RENDERS, f"puppet_{deg:03d}.png")).convert("RGBA")
        paste_shadow(bg, im, cx, 814, scale=1.08, shadow_alpha=120)
        bb = draw.textbbox((0, 0), nm, font=font_lbl)
        draw.text((cx - (bb[2] - bb[0]) // 2, 818), nm, fill=(200, 210, 226, 255), font=font_lbl)

    # Bottom row: All 18 modular Puppet.cs parts at 3x crisp scale
    draw.text((36, 860), "MODULAR PUPPET.CS SPRITE PARTS (3x SCALE  -  DROP-IN COMPATIBLE WITH Data/sprites/parts/)", fill=(244, 214, 132, 255), font=font_lbl)
    draw.rounded_rectangle([28, 882, W - 28, 1116], radius=10, fill=(16, 20, 30, 145), outline=(146, 166, 194, 65), width=1)

    part_list = [
        ("helm.greathelm_wings.f", "helm.f"),
        ("helm.greathelm_wings.s", "helm.s"),
        ("helm.greathelm_wings.b", "helm.b"),
        ("torso.plate_gilded.f",   "torso.f"),
        ("torso.plate_gilded.s",   "torso.s"),
        ("torso.plate_gilded.b",   "torso.b"),
        ("sleeve.plate_gilded",    "sleeve"),
        ("leg.plate",              "leg"),
        ("boot.sabatons.f",        "boot.f"),
        ("boot.sabatons.s",        "boot.s"),
        ("boot.sabatons.b",        "boot.b"),
        ("hand.gauntlets",         "hand"),
        ("shield.kite",            "shield"),
        ("longsword_gilded",       "sword"),
        ("cape",                   "cape"),
    ]
    px = 52
    for key, short_lbl in part_list:
        im = Image.open(os.path.join(PARTS, key + ".png")).convert("RGBA")
        w, h = im.size
        sc = 3
        sw, sh = w * sc, h * sc
        scaled = im.resize((sw, sh), Image.Resampling.NEAREST)
        cy = 995
        bg.alpha_composite(scaled, (px, cy - sh // 2))
        bb = draw.textbbox((0, 0), short_lbl, font=font_lbl)
        tw = bb[2] - bb[0]
        cx = px + sw // 2
        draw.text((cx - tw // 2, 1072), short_lbl, fill=(214, 222, 236, 255), font=font_lbl)
        sz_lbl = f"{w}x{h}"
        bb2 = draw.textbbox((0, 0), sz_lbl, font=font_sm)
        draw.text((cx - (bb2[2] - bb2[0]) // 2, 1089), sz_lbl, fill=(140, 152, 174, 255), font=font_sm)
        px += max(sw + 32, 92)

    out_path = os.path.join(BASE, "puppet_showcase.png")
    bg.convert("RGB").save(out_path)
    print("Saved ->", out_path)


if __name__ == "__main__":
    main()
