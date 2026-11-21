#!/usr/bin/env python3
"""
Generates modular HD-2D 'Gilded Dusk' pixel-art sprite parts sized to fit Ironfield's
exact Puppet.cs rig (Art.Px = 0.03m/px):
  helm.greathelm_wings.{f,s,b}.png   (28x24)
  torso.plate_gilded.{f,b}.png       (22x25), .s.png (15x25)
  sleeve.plate_gilded.png            (10x22) [top=forearm, bottom=upper arm]
  leg.plate.png                      (10x22) [top=shin, bottom=thigh]
  boot.sabatons.{f,b}.png            (10x10), .s.png (12x10)
  hand.gauntlets.png                 (7x7)
  shield.kite.png                    (22x27)
  longsword_gilded.png               (13x44) [grip at 0.17 from bottom]
  cape.png                           (24x32)
  head.{f,s,b}.png                   (17x17) [dark mail coif under-head]
"""
import math
import os
import sys
import time
import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render_test_character import (
    M_STEEL, M_DARKSTEEL, M_GOLD, M_CRIMSON, M_LEATHER, M_MAIL, M_PLUME, M_GLOW, M_BLADE, M_SHIELD_BG,
    RAMPS, MAT_SPEC, BAYER2,
    smin, sd_sphere, sd_ellipsoid, sd_box, sd_capsule, rot_x, rot_y, rot_z, combine,
)

PARTS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Data", "sprites", "test_character", "parts")
PX = 0.03  # Ironfield Style.Px = 0.03 metres per pixel


def render_sdf_part(sdf_fn, W, H, yaw_deg=0.0, pitch_deg=8.0, center=(0.0, 0.0, 0.0),
                    world_px=PX, open_top=False, open_bottom=False, crease_thresh=0.10):
    """Raymarch a single modular 3D part into an exact WxH pixel-art sprite with 1px selective outline."""
    # Inner area is (W-2)x(H-2) so the 1px selective outline sits cleanly inside WxH
    iw, ih = W - 2, H - 2
    target = np.array(center, dtype=np.float32)
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    cam_dir = np.array([
        math.sin(yaw) * math.cos(pitch),
        math.sin(pitch),
        math.cos(yaw) * math.cos(pitch),
    ], dtype=np.float32)
    dist = 3.0
    cam_pos = target + cam_dir * dist
    fwd = -cam_dir
    world_up = np.array([0.0, 1.0, 0.0], dtype=np.float32)
    right = np.cross(fwd, world_up)
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)

    world_w = iw * world_px
    world_h = ih * world_px
    u = ((np.arange(iw, dtype=np.float32) + 0.5) / iw - 0.5) * world_w
    v = (0.5 - (np.arange(ih, dtype=np.float32) + 0.5) / ih) * world_h
    uu, vv = np.meshgrid(u, v)

    ro_x = cam_pos[0] + uu * right[0] + vv * up[0]
    ro_y = cam_pos[1] + uu * right[1] + vv * up[1]
    ro_z = cam_pos[2] + uu * right[2] + vv * up[2]
    rd_x = np.full_like(uu, fwd[0])
    rd_y = np.full_like(uu, fwd[1])
    rd_z = np.full_like(uu, fwd[2])

    t = np.full_like(uu, 1.2)
    hit = np.zeros(uu.shape, dtype=bool)
    active = np.ones(uu.shape, dtype=bool)

    for _ in range(64):
        px = ro_x + rd_x * t
        py = ro_y + rd_y * t
        pz = ro_z + rd_z * t
        d = sdf_fn(px, py, pz, False)
        newly_hit = active & (d < 0.0018)
        hit |= newly_hit
        active &= ~newly_hit
        t = np.where(active, t + np.maximum(d * 0.80, 0.003), t)
        active &= (t < 5.0)
        if not np.any(active):
            break

    hx = ro_x + rd_x * t
    hy = ro_y + rd_y * t
    hz = ro_z + rd_z * t
    _, mat = sdf_fn(hx, hy, hz, True)
    mat = np.where(hit, mat, 0)

    eps = 0.003
    nx = sdf_fn(hx + eps, hy, hz, False) - sdf_fn(hx - eps, hy, hz, False)
    ny = sdf_fn(hx, hy + eps, hz, False) - sdf_fn(hx, hy - eps, hz, False)
    nz = sdf_fn(hx, hy, hz + eps, False) - sdf_fn(hx, hy, hz - eps, False)
    nlen = np.sqrt(nx*nx + ny*ny + nz*nz + 1e-12)
    nx, ny, nz = nx / nlen, ny / nlen, nz / nlen

    ao = np.ones_like(t)
    for k, step in enumerate([0.025, 0.06, 0.12]):
        da = sdf_fn(hx + nx * step, hy + ny * step, hz + nz * step, False)
        ao -= (0.38 / (k + 1)) * np.clip(step - da, 0.0, step) / step
    ao = np.clip(ao, 0.36, 1.0)

    L_key = -0.45 * right + 0.70 * up - 0.55 * fwd
    L_key /= np.linalg.norm(L_key)
    L_rim = 0.65 * right + 0.32 * up + 0.68 * fwd
    L_rim /= np.linalg.norm(L_rim)
    V = -fwd

    ndotl = np.clip(nx * L_key[0] + ny * L_key[1] + nz * L_key[2], 0.0, 1.0)
    diff = (ndotl * 0.74 + 0.26) * ao
    H_vec = L_key + V
    H_vec /= np.linalg.norm(H_vec)
    ndoth = np.clip(nx * H_vec[0] + ny * H_vec[1] + nz * H_vec[2], 0.0, 1.0)
    ndotv = np.clip(nx * V[0] + ny * V[1] + nz * V[2], 0.0, 1.0)
    rim_align = np.clip(nx * L_rim[0] + ny * L_rim[1] + nz * L_rim[2] * 0.5 + 0.5, 0.0, 1.0)
    rim = ((1.0 - ndotv) ** 2.3) * (0.45 + 0.55 * rim_align)

    crease = np.zeros_like(hit, dtype=bool)
    for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        nx_s = np.roll(np.roll(nx, dy, axis=0), dx, axis=1)
        ny_s = np.roll(np.roll(ny, dy, axis=0), dx, axis=1)
        nz_s = np.roll(np.roll(nz, dy, axis=0), dx, axis=1)
        t_s  = np.roll(np.roll(t, dy, axis=0), dx, axis=1)
        h_s  = np.roll(np.roll(hit, dy, axis=0), dx, axis=1)
        dot_n = nx * nx_s + ny * ny_s + nz * nz_s
        crease |= hit & h_s & ((t_s - t > crease_thresh) | (dot_n < 0.25))

    by = np.arange(ih)[:, None] % 2
    bx = np.arange(iw)[None, :] % 2
    dither = BAYER2[by, bx] * 0.18

    inner = np.zeros((ih, iw, 4), dtype=np.uint8)
    for mid, ramp in RAMPS.items():
        mask = (mat == mid)
        if not np.any(mask):
            continue
        sp_pow, sp_str, rim_str = MAT_SPEC[mid]
        if mid == M_GLOW:
            shade = np.full_like(diff, 4.6)
        else:
            spec = (ndoth ** sp_pow) * sp_str
            val = diff * 0.90 + spec + rim * rim_str
            if mid == M_MAIL:
                tex = (((np.arange(ih)[:, None] + np.arange(iw)[None, :]) % 2) * 0.14 - 0.07)
                val = val + tex
            shade = np.clip((val - 0.16) / 0.98 * 5.0 + dither, 0.0, 5.0)
            shade = np.where(crease, np.maximum(0.0, shade - 0.9), shade)
        idx = np.clip(np.round(shade).astype(np.int32), 0, 5)
        inner[mask, :3] = ramp[idx][mask].astype(np.uint8)
        inner[mask, 3] = 255

    # Pad to W x H and apply selective 1px outline
    rgba = np.zeros((H, W, 4), dtype=np.uint8)
    rgba[1:H-1, 1:W-1] = inner
    hit_p = np.zeros((H, W), dtype=bool)
    hit_p[1:H-1, 1:W-1] = hit
    mat_p = np.zeros((H, W), dtype=np.int32)
    mat_p[1:H-1, 1:W-1] = mat

    out_mask = np.zeros((H, W), dtype=bool)
    out_rgb = np.zeros((H, W, 3), dtype=np.float32)
    best_lum = np.full((H, W), 1e9, dtype=np.float32)

    for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        h_n = np.roll(np.roll(hit_p, dy, axis=0), dx, axis=1)
        m_n = np.roll(np.roll(mat_p, dy, axis=0), dx, axis=1)
        if dy == -1: h_n[-1, :] = False
        if dy ==  1: h_n[0, :] = False
        if dx == -1: h_n[:, -1] = False
        if dx ==  1: h_n[:, 0] = False
        cand = (~hit_p) & h_n
        if open_top: cand[0, :] = False
        if open_bottom: cand[-1, :] = False
        if not np.any(cand):
            continue
        c_rgb = np.zeros((H, W, 3), dtype=np.float32)
        for mid, ramp in RAMPS.items():
            cm = cand & (m_n == mid)
            if np.any(cm):
                c_rgb[cm] = ramp[0] * 0.62
        lum = c_rgb[..., 0] * 0.3 + c_rgb[..., 1] * 0.59 + c_rgb[..., 2] * 0.11
        better = cand & (lum < best_lum)
        out_rgb[better] = c_rgb[better]
        best_lum[better] = lum[better]
        out_mask |= cand

    rgba[out_mask, :3] = np.clip(out_rgb[out_mask], 0, 255).astype(np.uint8)
    rgba[out_mask, 3] = 255
    return Image.fromarray(rgba, mode="RGBA")


# ============================================================
# INDIVIDUAL MODULAR PART SDFs (matching Ironfield Puppet.cs!)
# ============================================================

def sdf_helm(x, y, z, need_mat=True):
    """Winged Armet Helm + Glowing Amber Visor + Sweeping Crest Plume.
    Centered around y=0, total height ~0.64m (24px), width ~0.76m (28px).
    Note: in Puppet.cs, side sprites ('s') face RIGHT (+X in screen space -> camera at yaw=90 looks from +X toward -X?
    WAIT: in Puppet.cs, side sprites face RIGHT!
    When camera is at yaw=-90 (looking from -X toward +X), +Z (front of helm) points to the RIGHT of the image!
    """
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    # Exact 30x30 (0.84m inner) Ironfield helm.greathelm_wings alignment:
    # Center y=0 is row 15 (eye/visor line at 1.57m); dome top at y=+0.21 (row 8);
    # bevor/gorget base at y=-0.36 (row 27); swept wings & plume rise cleanly to y=+0.39 (row 2)!
    cy = -0.05
    d_dome = sd_ellipsoid(x, y, z, 0.0, cy + 0.05, -0.01, 0.205, 0.215, 0.220)
    d_snout = sd_ellipsoid(x, y, z, 0.0, cy - 0.02, 0.135, 0.150, 0.125, 0.155)
    d_bevor = sd_ellipsoid(x, y, z, 0.0, cy - 0.17, 0.035, 0.185, 0.145, 0.180)
    d_helm = smin(smin(d_dome, d_snout, 0.035), d_bevor, 0.04)
    add(d_helm, M_STEEL)

    # Dark recessed visor slit + glowing amber eyes right at row 15..16
    d_slit = sd_box(x, y, z, 0.0, cy + 0.025, 0.205, 0.125, 0.024, 0.055, 0.005)
    d_eyes = np.minimum(
        sd_ellipsoid(x, y, z,  0.054, cy + 0.025, 0.255, 0.040, 0.019, 0.028),
        sd_ellipsoid(x, y, z, -0.054, cy + 0.025, 0.255, 0.040, 0.019, 0.028),
    )
    add(d_slit, M_DARKSTEEL)
    add(d_eyes, M_GLOW)

    # Gold brow diadem, central keel, chin/gorget rim
    d_diadem = sd_ellipsoid(x, y, z, 0.0, cy + 0.085, 0.015, 0.212, 0.028, 0.232)
    d_keel = sd_box(x, y - (cy + 0.23), z + 0.01, 0.0, 0.0, 0.0, 0.024, 0.072, 0.165, 0.012)
    d_chin = sd_ellipsoid(x, y, z, 0.0, cy - 0.28, 0.03, 0.182, 0.030, 0.175)
    # Swept gold side-wings on temples (matching original 30x30 wing span)
    wxr, wyr, wzr = rot_x(x - 0.22, y - (cy + 0.17), z + 0.02, -24)
    wxr, wyr, wzr = rot_z(wxr, wyr, wzr, -30)
    d_wr = sd_box(wxr, wyr, wzr, 0.0, 0.065, -0.04, 0.020, 0.145, 0.078, 0.010)
    wxl, wyl, wzl = rot_x(x + 0.22, y - (cy + 0.17), z + 0.02, -24)
    wxl, wyl, wzl = rot_z(wxl, wyl, wzl, 30)
    d_wl = sd_box(wxl, wyl, wzl, 0.0, 0.065, -0.04, 0.020, 0.145, 0.078, 0.010)
    add(np.minimum(np.minimum(d_diadem, d_keel), np.minimum(d_chin, np.minimum(d_wr, d_wl))), M_GOLD)

    # Sweeping crimson-to-ivory crest plume arching back from the crown
    p0 = (0.0, cy + 0.28,  0.02)
    p1 = (0.0, cy + 0.39, -0.10)
    p2 = (0.0, cy + 0.30, -0.27)
    p3 = (0.0, cy + 0.06, -0.36)
    d_plume = sd_capsule(x, y, z, *p0, *p1, 0.046, 0.064)
    d_plume = smin(d_plume, sd_capsule(x, y, z, *p1, *p2, 0.064, 0.066), 0.03)
    d_plume = smin(d_plume, sd_capsule(x, y, z, *p2, *p3, 0.062, 0.034), 0.03)
    add(d_plume, M_PLUME)

    return (d, m) if need_mat else d


def sdf_head_coif(x, y, z, need_mat=True):
    """Dark armoured mail coif + gorget under-head (17x17) so no bare skin peeks under the closed armet."""
    d = sd_ellipsoid(x, y, z, 0.0, 0.0, 0.0, 0.165, 0.185, 0.175)
    m = np.full(x.shape, M_MAIL, dtype=np.int32) if need_mat else None
    return (d, m) if need_mat else d


def sdf_torso(x, y, z, need_mat=True):
    """Sculpted V-tapered cuirass, gorget, fluted shoulder pauldrons, gilded war-belt, tassets & crimson tabard.
    Size: 22x25 front/back (0.60m x 0.69m inner), 15x25 side (0.39m x 0.69m inner)."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    # Cuirass chest & waist (y in [-0.33, +0.33])
    d_waist = sd_ellipsoid(x, y, z, 0.0, -0.02, 0.0, 0.20, 0.16, 0.125)
    d_chest = sd_ellipsoid(x, y, z, 0.0,  0.14, 0.01, 0.265, 0.19, 0.155)
    d_keel  = sd_ellipsoid(x, y, z, 0.0,  0.13, 0.12, 0.145, 0.15, 0.075)
    add(smin(smin(d_waist, d_chest, 0.07), d_keel, 0.05), M_STEEL)

    # Gorget collar at top
    add(sd_ellipsoid(x, y, z, 0.0, 0.30, 0.0, 0.17, 0.055, 0.145), M_DARKSTEEL)
    add(sd_ellipsoid(x, y, z, 0.0, 0.315, 0.005, 0.178, 0.022, 0.152), M_GOLD)

    # Fluted shoulder pauldrons integrated cleanly at torso shoulders
    d_rp = sd_ellipsoid(x, y, z,  0.255, 0.22, 0.0, 0.105, 0.105, 0.135)
    d_lp = sd_ellipsoid(x, y, z, -0.255, 0.23, 0.0, 0.112, 0.115, 0.140)
    d_pr = np.minimum(
        sd_ellipsoid(x, y, z,  0.258, 0.245, 0.0, 0.110, 0.026, 0.142),
        sd_ellipsoid(x, y, z, -0.258, 0.255, 0.0, 0.118, 0.028, 0.146),
    )
    add(np.minimum(d_rp, d_lp), M_STEEL)
    add(d_pr, M_GOLD)

    # Gold sun-chevron on chest + sun boss on front AND back spine trim
    d_c1 = sd_capsule(x, y, z, 0.0, 0.07, 0.185,  0.15, 0.22, 0.14, 0.020)
    d_c2 = sd_capsule(x, y, z, 0.0, 0.07, 0.185, -0.15, 0.22, 0.14, 0.020)
    d_boss = sd_sphere(x, y, z, 0.0, 0.16, 0.195, 0.044)
    d_back_trim = sd_capsule(x, y, z, 0.0, 0.0, -0.165, 0.0, 0.27, -0.16, 0.022)
    add(np.minimum(np.minimum(d_c1, d_c2), np.minimum(d_boss, d_back_trim)), M_GOLD)

    # War-belt + large gilded buckle
    add(sd_ellipsoid(x, y, z, 0.0, -0.08, 0.0, 0.228, 0.052, 0.172), M_LEATHER)
    add(sd_box(x, y, z, 0.0, -0.08, 0.165, 0.055, 0.048, 0.022, 0.008), M_GOLD)

    # Mail skirt + flared armoured tassets + crimson tabard hanging down to y = -0.34
    add(sd_ellipsoid(x, y, z, 0.0, -0.18, 0.0, 0.225, 0.14, 0.165), M_MAIL)
    rx_t, ry_t, rz_t = rot_z(x - 0.195, y + 0.21, z, -12)
    lx_t, ly_t, lz_t = rot_z(x + 0.195, y + 0.21, z,  12)
    add(np.minimum(
        sd_box(rx_t, ry_t, rz_t, 0, 0, 0, 0.052, 0.115, 0.135, 0.020),
        sd_box(lx_t, ly_t, lz_t, 0, 0, 0, 0.052, 0.115, 0.135, 0.020),
    ), M_STEEL)
    add(np.minimum(
        sd_box(rx_t, ry_t, rz_t,  0.006, -0.105, 0, 0.055, 0.022, 0.138, 0.008),
        sd_box(lx_t, ly_t, lz_t, -0.006, -0.105, 0, 0.055, 0.022, 0.138, 0.008),
    ), M_GOLD)

    # Crimson front & back tabard panels with gold hem
    add(sd_box(x, y, z, 0.0, -0.22,  0.135, 0.105, 0.125, 0.022, 0.008), M_CRIMSON)
    add(sd_box(x, y, z, 0.0, -0.32,  0.138, 0.108, 0.024, 0.024, 0.006), M_GOLD)
    add(sd_box(x, y, z, 0.0, -0.22, -0.135, 0.105, 0.125, 0.022, 0.008), M_CRIMSON)
    add(sd_box(x, y, z, 0.0, -0.32, -0.138, 0.108, 0.024, 0.024, 0.006), M_GOLD)

    return (d, m) if need_mat else d


def sdf_sleeve(x, y, z, need_mat=True):
    """Armored sleeve (10x22): top half = forearm/vambrace (y>0), bottom half = upper arm (y<0), gold elbow cop in middle."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    add(sd_capsule(x, y, z, 0.0, -0.32, 0.0, 0.0, 0.32, 0.0, 0.098, 0.090), M_STEEL)
    # Dark steel joint shadow bands
    add(sd_ellipsoid(x, y, z, 0.0,  0.22, 0.0, 0.092, 0.055, 0.092), M_DARKSTEEL)
    add(sd_ellipsoid(x, y, z, 0.0, -0.22, 0.0, 0.100, 0.060, 0.100), M_DARKSTEEL)
    # Gold elbow cop / vambrace cuff trim
    add(sd_ellipsoid(x, y, z, 0.0,  0.00, 0.02, 0.102, 0.048, 0.102), M_GOLD)
    add(sd_ellipsoid(x, y, z, 0.0,  0.27, 0.0, 0.094, 0.024, 0.094), M_GOLD)
    return (d, m) if need_mat else d


def sdf_leg(x, y, z, need_mat=True):
    """Armored leg (10x22): blued-steel greaves & dark-steel cuisses with crisp gold knee poleyn & trim."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    add(sd_capsule(x, y, z, 0.0, -0.32, 0.0, 0.0, 0.0, 0.0, 0.106, 0.098), M_DARKSTEEL)
    add(sd_capsule(x, y, z, 0.0,  0.00, 0.0, 0.0, 0.32, 0.0, 0.098, 0.086), M_STEEL)
    # Subtle dark fluted plate bands + crisp gold knee poleyn
    add(sd_ellipsoid(x, y, z, 0.0, -0.14, 0.0, 0.102, 0.024, 0.102), M_STEEL)
    add(sd_ellipsoid(x, y, z, 0.0,  0.00, 0.03, 0.104, 0.044, 0.104), M_GOLD)
    add(sd_ellipsoid(x, y, z, 0.0,  0.26, 0.0, 0.090, 0.020, 0.090), M_GOLD)
    return (d, m) if need_mat else d


def sdf_boot(x, y, z, need_mat=True):
    """Articulated steel sabaton (10x10 front/back, 12x10 side) with gold ankle band."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    d_foot = sd_ellipsoid(x, y, z, 0.0, -0.04, 0.03, 0.092, 0.065, 0.145)
    d_foot = np.maximum(d_foot, -0.095 - y)
    d_ankle = sd_capsule(x, y, z, 0.0, -0.02, -0.02, 0.0, 0.09, -0.02, 0.082, 0.085)
    add(smin(d_foot, d_ankle, 0.02), M_DARKSTEEL)
    add(sd_ellipsoid(x, y, z, 0.0, 0.055, -0.01, 0.090, 0.024, 0.095), M_GOLD)
    add(sd_ellipsoid(x, y, z, 0.0, -0.035, 0.135, 0.062, 0.042, 0.045), M_STEEL)
    return (d, m) if need_mat else d


def sdf_hand(x, y, z, need_mat=True):
    """Armoured gauntlet fist (7x7)."""
    d = sd_ellipsoid(x, y, z, 0.0, 0.0, 0.0, 0.068, 0.068, 0.068)
    m = np.full(x.shape, M_STEEL, dtype=np.int32) if need_mat else None
    d_k = sd_box(x, y, z, 0.0, 0.015, 0.04, 0.052, 0.022, 0.032, 0.008)
    if need_mat:
        d, m = combine(d, m, d_k, M_GOLD)
    else:
        d = np.minimum(d, d_k)
    return (d, m) if need_mat else d


def sdf_shield(x, y, z, need_mat=True):
    """Curved Norman Kite Shield (22x27 = 0.60m x 0.75m inner) with gold border & embossed golden sunburst."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    # y in [-0.36, +0.36], x in [-0.28, +0.28]
    s_t = np.clip((0.33 - y) / 0.70, 0.0, 1.0)
    s_hw = np.where(y > 0.24,
                    0.275 * np.sqrt(np.maximum(0.0, 1.0 - ((y - 0.24) / 0.125)**2)),
                    0.275 * (1.0 - 0.88 * (s_t ** 1.35)))
    curve_z = -0.45 * (x * x)
    dz = np.abs(z - curve_z) - 0.025
    dx = np.abs(x) - np.maximum(s_hw, 0.001)
    dy = np.maximum(y - 0.365, -0.37 - y)
    d_body = np.maximum(dz, np.maximum(dx, dy))

    d_rim_box = np.maximum(np.abs(z - curve_z) - 0.030, np.maximum(np.abs(x) - np.maximum(s_hw + 0.005, 0.001), np.maximum(y - 0.37, -0.375 - y)))
    d_cutout = np.maximum(np.abs(x) - np.maximum(s_hw - 0.036, 0.001), np.maximum(y - 0.33, -0.32 - y))
    d_rim = np.maximum(d_rim_box, -d_cutout)

    add(d_body, M_SHIELD_BG)
    add(d_rim, M_GOLD)

    # Embossed golden sunburst emblem
    ex, ey, ez = x, y - 0.04, z - (curve_z + 0.026)
    d_boss = sd_ellipsoid(ex, ey, ez, 0.0, 0.0, 0.0, 0.068, 0.068, 0.028)
    d_rv = sd_box(ex, ey, ez, 0.0, 0.0, 0.0, 0.020, 0.185, 0.016, 0.005)
    d_rh = sd_box(ex, ey, ez, 0.0, 0.0, 0.0, 0.145, 0.020, 0.016, 0.005)
    rx1, ry1, rz1 = rot_z(ex, ey, ez, 45)
    d_rd1 = sd_box(rx1, ry1, rz1, 0.0, 0.0, 0.0, 0.016, 0.135, 0.014, 0.004)
    d_rd2 = sd_box(rx1, ry1, rz1, 0.0, 0.0, 0.0, 0.135, 0.016, 0.014, 0.004)
    add(np.minimum(d_boss, np.minimum(np.minimum(d_rv, d_rh), np.minimum(d_rd1, d_rd2))), M_GOLD)
    return (d, m) if need_mat else d


def sdf_longsword(x, y, z, need_mat=True):
    """Gilded Longsword (13x44 = 0.33m x 1.26m inner).
    Grip must sit at 0.17 from bottom -> y_bottom = -0.63, y_top = +0.63 -> grip center at y = -0.63 + 0.17*1.26 = -0.416."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    # Pommel at y = -0.57
    add(sd_ellipsoid(x, y, z, 0.0, -0.575, 0.0, 0.044, 0.048, 0.036), M_GOLD)
    # Leather grip from -0.54 to -0.32
    add(sd_capsule(x, y, z, 0.0, -0.54, 0.0, 0.0, -0.32, 0.0, 0.024, 0.026), M_LEATHER)
    # Swept gold crossguard at y = -0.30
    cg = 0.55 * np.minimum(x * x, 0.028)
    add(sd_box(x, y + 0.30 - cg, z, 0.0, 0.0, 0.0, 0.155, 0.024, 0.026, 0.008), M_GOLD)
    # Gold ricasso at base of blade
    add(sd_box(x, y, z, 0.0, -0.24, 0.0, 0.052, 0.042, 0.022, 0.005), M_GOLD)
    # Broad tapered diamond-section gleaming blade from -0.27 to +0.62
    b_t = np.clip((y + 0.27) / 0.89, 0.0, 1.0)
    b_hw = np.where(b_t < 0.82, 0.054 * (1.0 - 0.24 * b_t), 0.054 * 0.803 * np.maximum(0.08, 1.0 - (b_t - 0.82) / 0.18))
    b_ht = 0.022 * (1.0 - 0.45 * b_t)
    s_scale = np.maximum(np.minimum(b_hw, b_ht), 0.008)
    d_xs = (np.abs(x) / b_hw + np.abs(z) / b_ht - 1.0) * s_scale * 0.65
    d_blade = np.maximum(d_xs, np.maximum(-0.27 - y, y - 0.62))
    add(d_blade, M_BLADE)
    return (d, m) if need_mat else d


def sdf_cape(x, y, z, need_mat=True):
    """Sculpted Crimson Cape (24x32 = 0.66m x 0.90m inner) with 3 lush vertical folds, scalloped gold hem, and back sun emblem."""
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None
    def add(dn, mn):
        nonlocal d, m
        if need_mat: d, m = combine(d, m, dn, mn)
        else: d = np.minimum(d, dn)

    # y in [-0.44, +0.44]
    t = np.clip((0.44 - y) / 0.88, 0.0, 1.0)
    hw = 0.21 + 0.115 * (t ** 0.85)
    fold = 0.036 * np.cos(x * 20.0) * (0.35 + 0.65 * t) + 0.015 * np.cos(x * 40.0 + 1.0) * t
    dz = np.abs(z - fold) - 0.024
    dx = np.abs(x) - hw
    bottom_edge = (-0.42 + 0.022 * np.cos(x * 18.0)) - y
    dy = np.maximum(y - 0.43, bottom_edge)
    d_cape = np.sqrt(np.maximum(dz, 0)**2 + np.maximum(dx, 0)**2 + np.maximum(dy, 0)**2 + 1e-12) + np.minimum(np.maximum(dz, np.maximum(dx, dy)), 0.0) - 0.008
    add(d_cape, M_CRIMSON)

    # Scalloped gold hem along bottom
    d_hem = np.maximum(d_cape - 0.005, y - (-0.355 + 0.022 * np.cos(x * 18.0)))
    add(d_hem, M_GOLD)

    # Gold sun medallion in center of cape
    d_ring = np.abs(np.sqrt(x*x + (y - 0.02)**2 + 1e-12) - 0.078) - 0.015
    d_v = np.maximum(np.abs(x) - 0.015, np.abs(y - 0.02) - 0.16)
    d_h = np.maximum(np.abs(x) - 0.125, np.abs(y - 0.02) - 0.015)
    d_motif = np.minimum(d_ring, np.minimum(d_v, d_h))
    add(np.maximum(d_cape - 0.005, d_motif), M_GOLD)
    return (d, m) if need_mat else d


def main():
    t0 = time.time()
    os.makedirs(PARTS_DIR, exist_ok=True)

    # NOTE on Ironfield side-view ('s') convention:
    # In Puppet.cs, side sprites face RIGHT (+X on screen).
    # In our camera coords, yaw=0 looks at FRONT (+Z facing camera), yaw=180 looks at BACK (-Z facing camera),
    # and yaw=-90 puts the camera at -X looking toward +X, so +Z (front of character) points RIGHT on screen!
    parts = [
        ("helm.greathelm_wings.f", sdf_helm,      30, 30,   0.0,  6.0, False, False),
        ("helm.greathelm_wings.s", sdf_helm,      30, 30, -90.0,  6.0, False, False),
        ("helm.greathelm_wings.b", sdf_helm,      30, 30, 180.0,  6.0, False, False),

        ("head.f",                 sdf_head_coif, 17, 17,   0.0,  6.0, False, False),
        ("head.s",                 sdf_head_coif, 17, 17, -90.0,  6.0, False, False),
        ("head.b",                 sdf_head_coif, 17, 17, 180.0,  6.0, False, False),

        ("torso.plate_gilded.f",   sdf_torso,     22, 25,   0.0,  6.0, False, False),
        ("torso.plate_gilded.s",   sdf_torso,     15, 25, -90.0,  6.0, False, False),
        ("torso.plate_gilded.b",   sdf_torso,     22, 25, 180.0,  6.0, False, False),

        ("sleeve.plate_gilded",    sdf_sleeve,    10, 22,   0.0,  0.0, True,  True),
        ("leg.plate",              sdf_leg,       10, 22,   0.0,  0.0, True,  True),

        ("boot.sabatons.f",        sdf_boot,      10, 10,   0.0,  8.0, False, False),
        ("boot.sabatons.s",        sdf_boot,      12, 10, -90.0,  8.0, False, False),
        ("boot.sabatons.b",        sdf_boot,      10, 10, 180.0,  8.0, False, False),

        ("hand.gauntlets",         sdf_hand,       7,  7,   0.0,  0.0, False, False),
        ("shield.kite",            sdf_shield,    22, 27,   0.0,  0.0, False, False),
        ("longsword_gilded",       sdf_longsword, 13, 44,   0.0,  0.0, False, False),
        ("cape",                   sdf_cape,      24, 32,   0.0,  0.0, False, False),
    ]

    for name, fn, w, h, yaw, pitch, ot, ob in parts:
        img = render_sdf_part(fn, w, h, yaw_deg=yaw, pitch_deg=pitch, open_top=ot, open_bottom=ob)
        img.save(os.path.join(PARTS_DIR, f"{name}.png"))

    print(f"Baked {len(parts)} modular Puppet.cs part PNGs in {time.time() - t0:.2f}s -> {PARTS_DIR}")


if __name__ == "__main__":
    main()
