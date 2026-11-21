#!/usr/bin/env python3
"""
Procedural 3D-to-HD2D pixel-art sprite generator for the Ashen Sun Vanguard test character.
Renders 8 true-3D turntable angles (0..315 deg) with custom hue-shifted pixel-art shading,
selective coloured outlines, crease ink, and a full presentation showcase sheet.
"""
import math
import os
import time
import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Data", "sprites", "test_character")

# Material IDs
M_STEEL     = 1
M_DARKSTEEL = 2
M_GOLD      = 3
M_CRIMSON   = 4
M_LEATHER   = 5
M_MAIL      = 6
M_PLUME     = 7
M_GLOW      = 8
M_BLADE     = 9
M_SHIELD_BG = 10

# Hand-tuned 6-tone hue-shifted ramps (deep shadow -> shadow -> mid-dark -> mid -> light -> specular)
RAMPS = {
    M_STEEL: np.array([
        [18, 22, 36],
        [34, 42, 62],
        [58, 70, 94],
        [94, 110, 138],
        [146, 166, 194],
        [222, 236, 250],
    ], dtype=np.float32),
    M_DARKSTEEL: np.array([
        [14, 16, 26],
        [24, 28, 42],
        [40, 46, 64],
        [62, 72, 94],
        [98, 112, 138],
        [168, 186, 212],
    ], dtype=np.float32),
    M_GOLD: np.array([
        [58, 32, 12],
        [104, 62, 18],
        [156, 102, 28],
        [208, 152, 46],
        [242, 204, 92],
        [255, 246, 192],
    ], dtype=np.float32),
    M_CRIMSON: np.array([
        [34, 10, 18],
        [68, 18, 30],
        [114, 28, 42],
        [162, 42, 54],
        [204, 72, 74],
        [236, 128, 116],
    ], dtype=np.float32),
    M_LEATHER: np.array([
        [24, 15, 12],
        [46, 28, 20],
        [74, 46, 32],
        [106, 68, 46],
        [142, 98, 68],
        [182, 138, 104],
    ], dtype=np.float32),
    M_MAIL: np.array([
        [20, 24, 34],
        [38, 44, 58],
        [60, 68, 86],
        [86, 96, 116],
        [124, 136, 156],
        [176, 188, 206],
    ], dtype=np.float32),
    M_PLUME: np.array([
        [72, 18, 28],
        [124, 34, 44],
        [178, 62, 62],
        [220, 116, 98],
        [244, 184, 154],
        [255, 238, 218],
    ], dtype=np.float32),
    M_GLOW: np.array([
        [180, 74, 16],
        [224, 116, 24],
        [248, 168, 42],
        [255, 212, 86],
        [255, 240, 158],
        [255, 252, 224],
    ], dtype=np.float32),
    M_BLADE: np.array([
        [32, 40, 58],
        [64, 78, 102],
        [108, 126, 154],
        [156, 176, 204],
        [206, 224, 244],
        [248, 252, 255],
    ], dtype=np.float32),
    M_SHIELD_BG: np.array([
        [22, 10, 22],
        [48, 16, 32],
        [84, 24, 44],
        [126, 36, 54],
        [168, 56, 68],
        [212, 98, 98],
    ], dtype=np.float32),
}

# Material specular parameters: (spec_power, spec_strength, rim_strength)
MAT_SPEC = {
    M_STEEL:     (28.0, 0.55, 0.40),
    M_DARKSTEEL: (24.0, 0.42, 0.36),
    M_GOLD:      (36.0, 0.72, 0.48),
    M_CRIMSON:   (8.0,  0.12, 0.34),
    M_LEATHER:   (12.0, 0.18, 0.24),
    M_MAIL:      (18.0, 0.32, 0.28),
    M_PLUME:     (10.0, 0.20, 0.42),
    M_GLOW:      (1.0,  0.00, 0.00),
    M_BLADE:     (42.0, 0.85, 0.50),
    M_SHIELD_BG: (14.0, 0.20, 0.28),
}

# ---- Vectorized SDF Primitives ----

def smin(a, b, k=0.03):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25

def sd_sphere(x, y, z, cx, cy, cz, r):
    return np.sqrt((x - cx)**2 + (y - cy)**2 + (z - cz)**2 + 1e-12) - r

def sd_ellipsoid(x, y, z, cx, cy, cz, rx, ry, rz):
    px = (x - cx) / rx
    py = (y - cy) / ry
    pz = (z - cz) / rz
    k0 = np.sqrt(px*px + py*py + pz*pz + 1e-12)
    px2 = px / rx
    py2 = py / ry
    pz2 = pz / rz
    k1 = np.sqrt(px2*px2 + py2*py2 + pz2*pz2 + 1e-12)
    return k0 * (k0 - 1.0) / k1

def sd_box(x, y, z, cx, cy, cz, bx, by, bz, r=0.0):
    qx = np.abs(x - cx) - bx
    qy = np.abs(y - cy) - by
    qz = np.abs(z - cz) - bz
    out = np.sqrt(np.maximum(qx, 0.0)**2 + np.maximum(qy, 0.0)**2 + np.maximum(qz, 0.0)**2 + 1e-12)
    inn = np.minimum(np.maximum(qx, np.maximum(qy, qz)), 0.0)
    return out + inn - r

def sd_capsule(x, y, z, ax, ay, az, bx, by, bz, ra, rb=None):
    if rb is None:
        rb = ra
    pax, pay, paz = x - ax, y - ay, z - az
    bax, bay, baz = bx - ax, by - ay, bz - az
    denom = bax*bax + bay*bay + baz*baz + 1e-12
    h = np.clip((pax*bax + pay*bay + paz*baz) / denom, 0.0, 1.0)
    dx = pax - bax * h
    dy = pay - bay * h
    dz = paz - baz * h
    r = ra + (rb - ra) * h
    return np.sqrt(dx*dx + dy*dy + dz*dz + 1e-12) - r

def rot_y(x, y, z, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return x * c + z * s, y, -x * s + z * c

def rot_x(x, y, z, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return x, y * c - z * s, y * s + z * c

def rot_z(x, y, z, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return x * c - y * s, x * s + y * c, z

def combine(d_cur, m_cur, d_new, m_new):
    mask = d_new < d_cur
    return np.where(mask, d_new, d_cur), np.where(mask, m_new, m_cur)


# ---- Character SDF (World units: height ~1.95m, feet at y=0, facing +Z) ----
def scene_sdf(x, y, z, need_mat=True):
    d = np.full_like(x, 1e4)
    m = np.zeros(x.shape, dtype=np.int32) if need_mat else None

    def add(dn, mn):
        nonlocal d, m
        if need_mat:
            d, m = combine(d, m, dn, mn)
        else:
            d = np.minimum(d, dn)

    # Slight heroic contrapposto stance:
    # Left foot slightly forward (+z), right foot braced slightly back (-z)

    # --- 1. BOOTS / SABATONS & GREAVES ---
    # Left leg (x < 0 is character's RIGHT? Convention: facing +Z, +X is character's LEFT, -X is character's RIGHT)
    # Wait: let's verify! Cross(Up=(0,1,0), Fwd=(0,0,1)) = (+1, 0, 0) = RIGHT!
    # So +X = character's RIGHT (sword hand), -X = character's LEFT (shield hand).
    # Right leg (+X, braced back slightly)
    d_rfoot = sd_ellipsoid(x, y, z, 0.145, 0.055, -0.02, 0.075, 0.055, 0.145)
    d_rfoot = np.maximum(d_rfoot, -y) # flat sole
    d_rshin = sd_capsule(x, y, z, 0.14, 0.08, -0.05, 0.13, 0.48, -0.01, 0.078, 0.086)
    d_rknee = sd_ellipsoid(x, y, z, 0.135, 0.50, 0.045, 0.078, 0.072, 0.078)
    d_rthigh = sd_capsule(x, y, z, 0.13, 0.50, -0.01, 0.115, 0.88, 0.0, 0.095, 0.115)

    # Left leg (-X, stepped slightly forward)
    d_lfoot = sd_ellipsoid(x, y, z, -0.145, 0.055, 0.07, 0.075, 0.055, 0.145)
    d_lfoot = np.maximum(d_lfoot, -y)
    d_lshin = sd_capsule(x, y, z, -0.14, 0.08, 0.04, -0.13, 0.48, 0.05, 0.078, 0.086)
    d_lknee = sd_ellipsoid(x, y, z, -0.135, 0.50, 0.105, 0.078, 0.072, 0.078)
    d_lthigh = sd_capsule(x, y, z, -0.13, 0.50, 0.05, -0.115, 0.88, 0.02, 0.095, 0.115)

    add(np.minimum(d_rfoot, d_lfoot), M_DARKSTEEL)
    add(np.minimum(d_rshin, d_lshin), M_STEEL)
    add(np.minimum(d_rknee, d_lknee), M_GOLD)
    add(np.minimum(d_rthigh, d_lthigh), M_DARKSTEEL)

    # Gold shin ridge / trim on greaves
    d_rtrim = sd_capsule(x, y, z, 0.14, 0.12, 0.028, 0.135, 0.44, 0.068, 0.018, 0.020)
    d_ltrim = sd_capsule(x, y, z, -0.14, 0.12, 0.118, -0.135, 0.44, 0.128, 0.018, 0.020)
    add(np.minimum(d_rtrim, d_ltrim), M_GOLD)

    # --- 2. MAIL SKIRT, TASSETS & CRIMSON TABARD ---
    # Mail hauberk skirt around hips
    d_mail_skirt = sd_ellipsoid(x, y, z, 0.0, 0.82, 0.0, 0.23, 0.18, 0.175)
    add(d_mail_skirt, M_MAIL)

    # Flared armoured tassets (left & right hip plates)
    rx_t, ry_t, rz_t = rot_z(x - 0.21, y - 0.78, z, -14)
    d_rtasset = sd_box(rx_t, ry_t, rz_t, 0, 0, 0, 0.045, 0.135, 0.135, 0.022)
    lx_t, ly_t, lz_t = rot_z(x + 0.21, y - 0.78, z, 14)
    d_ltasset = sd_box(lx_t, ly_t, lz_t, 0, 0, 0, 0.045, 0.135, 0.135, 0.022)
    add(np.minimum(d_rtasset, d_ltasset), M_STEEL)

    # Gold tasset borders (bottom edge)
    d_rt_gold = sd_box(rx_t, ry_t, rz_t, 0.008, -0.125, 0, 0.048, 0.022, 0.138, 0.01)
    d_lt_gold = sd_box(lx_t, ly_t, lz_t, -0.008, -0.125, 0, 0.048, 0.022, 0.138, 0.01)
    add(np.minimum(d_rt_gold, d_lt_gold), M_GOLD)

    # Front & Back Crimson Tabard Panels hanging between legs
    # Tapered slightly wider at bottom, gentle sway
    tab_y = np.clip((0.92 - y) / 0.56, 0.0, 1.0)
    tab_w = 0.095 + 0.045 * tab_y
    tab_zf = 0.145 + 0.045 * tab_y + 0.015 * np.sin(x * 24.0) * tab_y
    d_tab_f = sd_box(x, y, z - tab_zf, 0.0, 0.64, 0.0, tab_w, 0.27, 0.016, 0.008)
    # Gold hem on front tabard
    d_tab_fg = sd_box(x, y, z - tab_zf, 0.0, 0.39, 0.004, tab_w + 0.004, 0.026, 0.018, 0.006)
    add(d_tab_f, M_CRIMSON)
    add(d_tab_fg, M_GOLD)

    # --- 3. CUIRASS / CHESTPLATE & BELT ---
    # Sculpted heroic V-tapered breastplate + backplate
    d_waist = sd_ellipsoid(x, y, z, 0.0, 1.00, 0.0, 0.20, 0.16, 0.155)
    d_chest = sd_ellipsoid(x, y, z, 0.0, 1.24, 0.015, 0.265, 0.22, 0.185)
    # Prominent keeled breastplate ridge in front
    d_keel = sd_ellipsoid(x, y, z, 0.0, 1.22, 0.145, 0.14, 0.17, 0.085)
    d_torso = smin(smin(d_waist, d_chest, 0.08), d_keel, 0.06)
    add(d_torso, M_STEEL)

    # Gold breastplate sun-chevron / filigree on chest
    d_chev1 = sd_capsule(x, y, z, 0.0, 1.15, 0.205, 0.14, 1.32, 0.165, 0.018)
    d_chev2 = sd_capsule(x, y, z, 0.0, 1.15, 0.205, -0.14, 1.32, 0.165, 0.018)
    d_sun_chest = sd_sphere(x, y, z, 0.0, 1.25, 0.215, 0.042)
    add(np.minimum(np.minimum(d_chev1, d_chev2), d_sun_chest), M_GOLD)

    # Heavy leather war-belt with large gilded buckle
    d_belt = sd_ellipsoid(x, y, z, 0.0, 0.94, 0.0, 0.225, 0.052, 0.178)
    d_buckle = sd_box(x, y, z, 0.0, 0.94, 0.172, 0.052, 0.048, 0.022, 0.008)
    add(d_belt, M_LEATHER)
    add(d_buckle, M_GOLD)

    # Gorget (armoured collar protecting neck)
    d_gorget = sd_ellipsoid(x, y, z, 0.0, 1.44, 0.005, 0.165, 0.068, 0.155)
    d_gorget_rim = sd_ellipsoid(x, y, z, 0.0, 1.46, 0.01, 0.172, 0.024, 0.162)
    add(d_gorget, M_DARKSTEEL)
    add(d_gorget_rim, M_GOLD)

    # --- 4. FLUTED PAULDRONS (ASYMMETRICAL HEROIC SHOULDERS) ---
    # Right pauldron (+X, sword arm - layered sleeker)
    d_rp1 = sd_ellipsoid(x, y, z, 0.30, 1.37, 0.0, 0.135, 0.12, 0.145)
    d_rp2 = sd_ellipsoid(x, y, z, 0.33, 1.29, 0.0, 0.125, 0.085, 0.132)
    d_rp_rim = sd_ellipsoid(x, y, z, 0.305, 1.39, 0.0, 0.142, 0.032, 0.152)
    # Left pauldron (-X, shield arm - grander haute-piece ridge!)
    d_lp1 = sd_ellipsoid(x, y, z, -0.31, 1.39, 0.0, 0.148, 0.135, 0.158)
    d_lp2 = sd_ellipsoid(x, y, z, -0.34, 1.30, 0.0, 0.135, 0.092, 0.142)
    d_lp_fin = sd_box(x + 0.25, y - 1.49, z, 0.0, 0.0, 0.0, 0.028, 0.075, 0.115, 0.014)
    d_lp_rim = sd_ellipsoid(x, y, z, -0.315, 1.41, 0.0, 0.154, 0.034, 0.164)

    add(smin(d_rp1, d_rp2, 0.03), M_STEEL)
    add(smin(smin(d_lp1, d_lp2, 0.03), d_lp_fin, 0.02), M_STEEL)
    add(np.minimum(d_rp_rim, d_lp_rim), M_GOLD)

    # --- 5. ARMS & GAUNTLETS ---
    # Right arm (+X): held slightly outward in a commanding ready guard
    r_sh = (0.31, 1.32, 0.0)
    r_el = (0.43, 1.08, 0.02)
    r_ha = (0.45, 0.96, 0.22)
    d_rarm1 = sd_capsule(x, y, z, *r_sh, *r_el, 0.074, 0.066)
    d_rarm2 = sd_capsule(x, y, z, *r_el, *r_ha, 0.064, 0.056)
    d_relb = sd_sphere(x, y, z, *r_el, 0.072)
    d_rhand = sd_sphere(x, y, z, *r_ha, 0.062)
    add(np.minimum(d_rarm1, d_rarm2), M_DARKSTEEL)
    add(d_relb, M_GOLD)
    add(d_rhand, M_STEEL)

    # Left arm (-X): bent forward carrying the kite shield
    l_sh = (-0.31, 1.32, 0.0)
    l_el = (-0.37, 1.08, 0.04)
    l_ha = (-0.34, 1.05, 0.23)
    d_larm1 = sd_capsule(x, y, z, *l_sh, *l_el, 0.074, 0.066)
    d_larm2 = sd_capsule(x, y, z, *l_el, *l_ha, 0.064, 0.056)
    d_lelb = sd_sphere(x, y, z, *l_el, 0.072)
    add(np.minimum(d_larm1, d_larm2), M_DARKSTEEL)
    add(d_lelb, M_GOLD)

    # --- 6. WINGED ARMET HELM, GLOWING VISOR & FLOWING CREST PLUME ---
    # Cranium dome
    d_helm_dome = sd_ellipsoid(x, y, z, 0.0, 1.66, -0.01, 0.145, 0.165, 0.165)
    # Pointed houndskull/armet visor projecting forward (+Z)
    d_visor_snout = sd_ellipsoid(x, y, z, 0.0, 1.615, 0.095, 0.105, 0.092, 0.115)
    # Chin bevor
    d_bevor = sd_ellipsoid(x, y, z, 0.0, 1.54, 0.045, 0.115, 0.075, 0.115)
    d_helm = smin(smin(d_helm_dome, d_visor_snout, 0.03), d_bevor, 0.03)
    add(d_helm, M_STEEL)

    # Recessed dark visor slit + warm glowing amber eye-slit cleanly visible inside
    d_slit_cut = sd_box(x, y, z, 0.0, 1.652, 0.155, 0.090, 0.016, 0.036, 0.004)
    d_eyes = np.minimum(
        sd_ellipsoid(x, y, z, 0.038, 1.652, 0.186, 0.026, 0.011, 0.020),
        sd_ellipsoid(x, y, z, -0.038, 1.652, 0.186, 0.026, 0.011, 0.020),
    )
    add(d_slit_cut, M_DARKSTEEL)
    add(d_eyes, M_GLOW)

    # Gold brow diadem & central crest keel on helm
    d_diadem = sd_ellipsoid(x, y, z, 0.0, 1.69, 0.01, 0.150, 0.020, 0.172)
    d_crest_keel = sd_box(x, y - 1.79, z + 0.01, 0.0, 0.0, 0.0, 0.018, 0.055, 0.125, 0.010)
    # Swept gold side-wings on helm temples (iconic silhouette from front, 3/4, and back!)
    wx_r, wy_r, wz_r = rot_x(x - 0.145, y - 1.73, z + 0.02, -28)
    wx_r, wy_r, wz_r = rot_z(wx_r, wy_r, wz_r, -24)
    d_wing_r = sd_box(wx_r, wy_r, wz_r, 0.0, 0.04, -0.04, 0.014, 0.095, 0.055, 0.008)
    wx_l, wy_l, wz_l = rot_x(x + 0.145, y - 1.73, z + 0.02, -28)
    wx_l, wy_l, wz_l = rot_z(wx_l, wy_l, wz_l, 24)
    d_wing_l = sd_box(wx_l, wy_l, wz_l, 0.0, 0.04, -0.04, 0.014, 0.095, 0.055, 0.008)
    add(np.minimum(np.minimum(d_diadem, d_crest_keel), np.minimum(d_wing_r, d_wing_l)), M_GOLD)

    # Flowing sweeping crest plume arching from helm top back and down
    p0 = (0.0, 1.84, 0.01)
    p1 = (0.0, 1.91, -0.10)
    p2 = (0.0, 1.83, -0.24)
    p3 = (0.0, 1.66, -0.34)
    p4 = (0.0, 1.48, -0.39)
    d_plume = sd_capsule(x, y, z, *p0, *p1, 0.036, 0.052)
    d_plume = smin(d_plume, sd_capsule(x, y, z, *p1, *p2, 0.052, 0.056), 0.03)
    d_plume = smin(d_plume, sd_capsule(x, y, z, *p2, *p3, 0.054, 0.042), 0.03)
    d_plume = smin(d_plume, sd_capsule(x, y, z, *p3, *p4, 0.040, 0.018), 0.02)
    add(d_plume, M_PLUME)

    # --- 7. SCULPTED FLOWING CRIMSON CAPE (DRAMATIC FROM BACK, SIDE & 3/4) ---
    # Cape drapes from shoulders (y=1.42) down to calf (y=0.22), flaring wider & billowing backward
    cape_t = np.clip((1.42 - y) / 1.20, 0.0, 1.0)
    # Center z of cape curves gently back as it falls
    cape_z0 = -0.15 - 0.16 * (cape_t ** 1.15) - 0.04 * np.sin(cape_t * math.pi)
    # Sculpted vertical cloth folds (3 lush ridges)
    fold = 0.032 * np.cos(x * 16.0) * (0.35 + 0.65 * cape_t) + 0.016 * np.cos(x * 32.0 + 1.2) * cape_t
    # Gentle curve wrapping around shoulders at top
    wrap = 0.22 * (x * x) * (1.2 - 0.6 * cape_t)
    cape_z = cape_z0 + fold + wrap
    cape_half_w = 0.24 + 0.18 * (cape_t ** 0.9)
    # Distance to thin curved sheet
    dz_cape = np.abs(z - cape_z) - 0.022
    dx_cape = np.abs(x) - cape_half_w
    dy_cape = np.maximum(y - 1.43, 0.20 - y)
    # Scalloped/notched bottom edge for heroic flair
    bottom_cut = (0.22 + 0.025 * np.cos(x * 14.0)) - y
    dy_cape = np.maximum(y - 1.43, bottom_cut)
    d_cape = np.maximum(dz_cape, np.maximum(dx_cape, dy_cape))
    # Slightly round the cape edges
    d_cape = np.sqrt(np.maximum(dz_cape, 0)**2 + np.maximum(dx_cape, 0)**2 + np.maximum(dy_cape, 0)**2 + 1e-12) + np.minimum(np.maximum(dz_cape, np.maximum(dx_cape, dy_cape)), 0.0) - 0.008
    add(d_cape, M_CRIMSON)

    # Gold embroidered hem along bottom of cape + subtle golden sun-emblem on back
    d_cape_hem = np.maximum(d_cape - 0.004, y - (0.29 + 0.025 * np.cos(x * 14.0)))
    add(d_cape_hem, M_GOLD)
    # Back cape golden sunburst medallion (only on outer/back surface z < cape_z)
    cb_x, cb_y = x, y - 0.88
    d_cb_ring = np.abs(np.sqrt(cb_x*cb_x + cb_y*cb_y + 1e-12) - 0.085) - 0.013
    d_cb_v = np.maximum(np.abs(cb_x) - 0.014, np.abs(cb_y) - 0.175)
    d_cb_h = np.maximum(np.abs(cb_x) - 0.135, np.abs(cb_y) - 0.014)
    d_cb_motif = np.minimum(d_cb_ring, np.minimum(d_cb_v, d_cb_h))
    d_cape_back_gold = np.maximum(d_cape - 0.004, np.maximum(d_cb_motif, z - cape_z))
    add(d_cape_back_gold, M_GOLD)

    # Gold shoulder clasps holding cape
    add(sd_sphere(x, y, z, 0.16, 1.38, 0.145, 0.028), M_GOLD)
    add(sd_sphere(x, y, z, -0.16, 1.38, 0.145, 0.028), M_GOLD)

    # --- 8. CURVED KITE SHIELD (LEFT ARM, -X, ANGLED HEROICALLY) ---
    # Transform into shield local space: center at (-0.40, 1.00, 0.22), angled 38 deg outward
    sx, sy, sz = x + 0.40, y - 1.00, z - 0.22
    sx, sy, sz = rot_y(sx, sy, sz, -42)
    sx, sy, sz = rot_x(sx, sy, sz, 6)
    # Kite profile: wide rounded top (sy in [-0.36, 0.32]), tapering to a point at bottom (sy=-0.38)
    s_t = np.clip((0.30 - sy) / 0.68, 0.0, 1.0) # 0 at top, 1 at bottom tip
    s_hw = np.where(sy > 0.22,
                    0.22 * np.sqrt(np.maximum(0.0, 1.0 - ((sy - 0.22) / 0.12)**2)),
                    0.22 * (1.0 - 0.88 * (s_t ** 1.35)))
    # Slight cylindrical curve along local x
    curve_z = -0.35 * (sx * sx)
    dz_s = np.abs(sz - curve_z) - 0.020
    dx_s = np.abs(sx) - np.maximum(s_hw, 0.001)
    dy_s = np.maximum(sy - 0.34, -0.38 - sy)
    d_shield_body = np.maximum(dz_s, np.maximum(dx_s, dy_s))
    # Shield gold rim (crisp 0.024m border around midnight-crimson field)
    d_shield_rim_box = np.maximum(np.abs(sz - curve_z) - 0.024, np.maximum(np.abs(sx) - np.maximum(s_hw + 0.006, 0.001), np.maximum(sy - 0.346, -0.386 - sy)))
    d_shield_cutout = np.maximum(np.abs(sx) - np.maximum(s_hw - 0.026, 0.001), np.maximum(sy - 0.312, -0.338 - sy))
    d_shield_rim = np.maximum(d_shield_rim_box, -d_shield_cutout)

    add(d_shield_body, M_SHIELD_BG)
    add(d_shield_rim, M_GOLD)

    # Embossed golden sunburst emblem on front face of shield (sz > curve_z)
    eb_x, eb_y, eb_z = sx, sy - 0.04, sz - (curve_z + 0.022)
    d_boss = sd_ellipsoid(eb_x, eb_y, eb_z, 0.0, 0.0, 0.0, 0.052, 0.052, 0.020)
    # 4 cardinal + 4 diagonal sun rays
    d_ray_v = sd_box(eb_x, eb_y, eb_z, 0.0, 0.0, 0.0, 0.014, 0.145, 0.012, 0.004)
    d_ray_h = sd_box(eb_x, eb_y, eb_z, 0.0, 0.0, 0.0, 0.115, 0.014, 0.012, 0.004)
    rx1, ry1, rz1 = rot_z(eb_x, eb_y, eb_z, 45)
    d_ray_d1 = sd_box(rx1, ry1, rz1, 0.0, 0.0, 0.0, 0.011, 0.105, 0.010, 0.004)
    d_ray_d2 = sd_box(rx1, ry1, rz1, 0.0, 0.0, 0.0, 0.105, 0.011, 0.010, 0.004)
    d_emblem = np.minimum(d_boss, np.minimum(np.minimum(d_ray_v, d_ray_h), np.minimum(d_ray_d1, d_ray_d2)))
    add(d_emblem, M_GOLD)

    # --- 9. HEROIC GILDED LONGSWORD (RIGHT HAND, +X) ---
    # Local sword space centered at right hand, blade held boldly outward & up-forward!
    wx, wy, wz = x - r_ha[0], y - r_ha[1], z - r_ha[2]
    wx, wy, wz = rot_y(wx, wy, wz, -18)
    wx, wy, wz = rot_x(wx, wy, wz, 22)
    wx, wy, wz = rot_z(wx, wy, wz, 24)

    # Leather grip (wy in [-0.16, 0.07])
    d_grip = sd_capsule(wx, wy, wz, 0.0, -0.16, 0.0, 0.0, 0.07, 0.0, 0.022, 0.024)
    # Faceted gold pommel
    d_pommel = sd_ellipsoid(wx, wy, wz, 0.0, -0.195, 0.0, 0.040, 0.044, 0.034)
    # Swept gold crossguard (curved gently toward blade)
    cg_curve = 0.45 * np.minimum(wx * wx, 0.025)
    d_guard = sd_box(wx, wy - 0.090 - cg_curve, wz, 0.0, 0.0, 0.0, 0.155, 0.021, 0.024, 0.008)
    # Broad heroic tapered diamond-section steel blade (wy from 0.10 to 1.04)
    b_t = np.clip((wy - 0.10) / 0.94, 0.0, 1.0)
    b_hw = np.where(b_t < 0.82, 0.048 * (1.0 - 0.25 * b_t), 0.048 * 0.795 * np.maximum(0.08, 1.0 - (b_t - 0.82) / 0.18))
    b_ht = 0.020 * (1.0 - 0.45 * b_t)
    s_scale = np.maximum(np.minimum(b_hw, b_ht), 0.007)
    d_blade_xs = (np.abs(wx) / b_hw + np.abs(wz) / b_ht - 1.0) * s_scale * 0.65
    d_blade_y = np.maximum(0.098 - wy, wy - 1.04)
    d_blade = np.maximum(d_blade_xs, d_blade_y)
    # Gold ricasso accent at base of blade
    d_ricasso = sd_box(wx, wy, wz, 0.0, 0.13, 0.0, 0.042, 0.035, 0.019, 0.004)

    add(d_grip, M_LEATHER)
    add(d_pommel, M_GOLD)
    add(d_guard, M_GOLD)
    add(d_blade, M_BLADE)
    add(d_ricasso, M_GOLD)

    return (d, m) if need_mat else d


# ---- 2x2 Bayer Dither Matrix ----
BAYER2 = np.array([[0.0, 0.5], [0.75, 0.25]], dtype=np.float32) - 0.375


def render_angle(yaw_deg, W=76, H=88, pitch_deg=15.0):
    """Render one crisp pixel-art sprite at the given turntable yaw angle."""
    # Camera orbits around character center (0, 0.96, 0)
    target = np.array([0.0, 0.96, 0.0], dtype=np.float32)
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    # At yaw=0, camera is at +Z looking toward -Z (FRONT view of character facing +Z)
    cam_dir = np.array([
        math.sin(yaw) * math.cos(pitch),
        math.sin(pitch),
        math.cos(yaw) * math.cos(pitch),
    ], dtype=np.float32)
    dist = 5.2
    cam_pos = target + cam_dir * dist

    # Camera basis
    fwd = -cam_dir
    world_up = np.array([0.0, 1.0, 0.0], dtype=np.float32)
    right = np.cross(fwd, world_up)
    right /= np.linalg.norm(right)
    up = np.cross(right, fwd)

    # Orthographic-telephoto projection (clean pixel art proportions, zero distortion)
    # Framing: height covers 2.28m (from y=-0.10 to y=2.18), width proportional
    world_h = 2.38
    world_w = world_h * (W / H)

    # Pixel grid (leave 1px border for selective outline)
    u = ((np.arange(W, dtype=np.float32) + 0.5) / W - 0.5) * world_w
    v = (0.5 - (np.arange(H, dtype=np.float32) + 0.5) / H) * world_h
    uu, vv = np.meshgrid(u, v)

    # Slight perspective blend for subtle 3D depth while staying pixel-crisp
    ro_x = cam_pos[0] + uu * right[0] + vv * up[0]
    ro_y = cam_pos[1] + uu * right[1] + vv * up[1]
    ro_z = cam_pos[2] + uu * right[2] + vv * up[2]

    rd_x = np.full_like(uu, fwd[0])
    rd_y = np.full_like(uu, fwd[1])
    rd_z = np.full_like(uu, fwd[2])

    # Sphere-trace (raymarch)
    t = np.full_like(uu, 2.2) # start near bounding volume
    hit = np.zeros(uu.shape, dtype=bool)
    active = np.ones(uu.shape, dtype=bool)

    for _ in range(72):
        px = ro_x + rd_x * t
        py = ro_y + rd_y * t
        pz = ro_z + rd_z * t
        d = scene_sdf(px, py, pz, need_mat=False)
        newly_hit = active & (d < 0.0025)
        hit |= newly_hit
        active &= ~newly_hit
        t = np.where(active, t + np.maximum(d * 0.82, 0.004), t)
        active &= (t < 8.2)
        if not np.any(active):
            break

    # Evaluate material and normals at hit points
    hx = ro_x + rd_x * t
    hy = ro_y + rd_y * t
    hz = ro_z + rd_z * t
    _, mat = scene_sdf(hx, hy, hz, need_mat=True)
    mat = np.where(hit, mat, 0)

    eps = 0.004
    nx = scene_sdf(hx + eps, hy, hz, False) - scene_sdf(hx - eps, hy, hz, False)
    ny = scene_sdf(hx, hy + eps, hz, False) - scene_sdf(hx, hy - eps, hz, False)
    nz = scene_sdf(hx, hy, hz + eps, False) - scene_sdf(hx, hy, hz - eps, False)
    nlen = np.sqrt(nx*nx + ny*ny + nz*nz + 1e-12)
    nx, ny, nz = nx / nlen, ny / nlen, nz / nlen

    # Ambient occlusion (3 steps along normal)
    ao = np.ones_like(t)
    for k, step in enumerate([0.035, 0.085, 0.16]):
        da = scene_sdf(hx + nx * step, hy + ny * step, hz + nz * step, False)
        ao -= (0.42 / (k + 1)) * np.clip(step - da, 0.0, step) / step
    ao = np.clip(ao, 0.32, 1.0)

    # Lighting anchored relative to camera (classic top-left golden key + cool bottom-right rim)
    # so EVERY turntable angle has heroic, readable form lighting!
    L_key = -0.48 * right + 0.68 * up - 0.55 * fwd
    L_key /= np.linalg.norm(L_key)
    L_rim = 0.65 * right + 0.35 * up + 0.67 * fwd
    L_rim /= np.linalg.norm(L_rim)
    V = -fwd # view vector toward camera

    ndotl = np.clip(nx * L_key[0] + ny * L_key[1] + nz * L_key[2], 0.0, 1.0)
    # Half-Lambert wrap for lush painterly readability in shadows
    diff = (ndotl * 0.72 + 0.28) * ao

    # Blinn-Phong specular
    H_vec = L_key + V
    H_vec /= np.linalg.norm(H_vec)
    ndoth = np.clip(nx * H_vec[0] + ny * H_vec[1] + nz * H_vec[2], 0.0, 1.0)

    # Fresnel rim
    ndotv = np.clip(nx * V[0] + ny * V[1] + nz * V[2], 0.0, 1.0)
    rim_align = np.clip(nx * L_rim[0] + ny * L_rim[1] + nz * L_rim[2] * 0.5 + 0.5, 0.0, 1.0)
    rim = ((1.0 - ndotv) ** 2.4) * (0.45 + 0.55 * rim_align)

    # Internal crease detection (sharp normal change or depth jump -> crisp dark pixel accent)
    crease = np.zeros_like(hit, dtype=bool)
    for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        nx_s = np.roll(np.roll(nx, dy, axis=0), dx, axis=1)
        ny_s = np.roll(np.roll(ny, dy, axis=0), dx, axis=1)
        nz_s = np.roll(np.roll(nz, dy, axis=0), dx, axis=1)
        t_s  = np.roll(np.roll(t, dy, axis=0), dx, axis=1)
        h_s  = np.roll(np.roll(hit, dy, axis=0), dx, axis=1)
        # Depth edge where neighbor is further away -> darker foreground edge
        dot_n = nx * nx_s + ny * ny_s + nz * nz_s
        crease |= hit & h_s & ((t_s - t > 0.14) | (dot_n < 0.28))

    # Bayer dither grid
    by = np.arange(H)[:, None] % 2
    bx = np.arange(W)[None, :] % 2
    dither = BAYER2[by, bx] * 0.22

    # Compose RGBA pixel art
    rgba = np.zeros((H, W, 4), dtype=np.uint8)
    for mid, ramp in RAMPS.items():
        mask = (mat == mid)
        if not np.any(mask):
            continue
        sp_pow, sp_str, rim_str = MAT_SPEC[mid]
        if mid == M_GLOW:
            shade = np.full_like(diff, 4.4)
        else:
            spec = (ndoth ** sp_pow) * sp_str
            # Vertical subtle gradient (slightly brighter near shoulders/helm, grounded near boots)
            v_grad = np.clip((hy - 0.15) / 1.65, 0.0, 1.0) * 0.10
            val = diff * 0.88 + spec + rim * rim_str + v_grad
            # Subtle mail/cloth micro-texture
            if mid == M_MAIL:
                tex = (((np.arange(H)[:, None] + np.arange(W)[None, :]) % 2) * 0.12 - 0.06)
                val = val + tex
            # Map [0.18..1.18] -> [0..5] ramp index
            shade = np.clip((val - 0.18) / 1.00 * 5.0 + dither, 0.0, 5.0)
            # Crease darkens by 1.1 steps for crisp pixel readability
            shade = np.where(crease, np.maximum(0.0, shade - 1.05), shade)

        idx = np.clip(np.round(shade).astype(np.int32), 0, 5)
        col = ramp[idx]
        rgba[mask, :3] = col[mask].astype(np.uint8)
        rgba[mask, 3] = 255

    # Selective coloured 1px exterior outline (darkened tone of bordering material)
    outline_mask = np.zeros((H, W), dtype=bool)
    out_rgb = np.zeros((H, W, 3), dtype=np.float32)
    best_lum = np.full((H, W), 1e9, dtype=np.float32)

    for dy, dx in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        h_n = np.roll(np.roll(hit, dy, axis=0), dx, axis=1)
        m_n = np.roll(np.roll(mat, dy, axis=0), dx, axis=1)
        # Prevent wrap-around across borders
        if dy == -1: h_n[-1, :] = False
        if dy ==  1: h_n[0, :] = False
        if dx == -1: h_n[:, -1] = False
        if dx ==  1: h_n[:, 0] = False

        cand = (~hit) & h_n
        if not np.any(cand):
            continue
        # Look up ramp[0] darkened by 0.62
        c_rgb = np.zeros((H, W, 3), dtype=np.float32)
        for mid, ramp in RAMPS.items():
            cm = cand & (m_n == mid)
            if np.any(cm):
                c_rgb[cm] = ramp[0] * 0.62
        lum = c_rgb[..., 0] * 0.3 + c_rgb[..., 1] * 0.59 + c_rgb[..., 2] * 0.11
        better = cand & (lum < best_lum)
        out_rgb[better] = c_rgb[better]
        best_lum[better] = lum[better]
        outline_mask |= cand

    rgba[outline_mask, :3] = np.clip(out_rgb[outline_mask], 0, 255).astype(np.uint8)
    rgba[outline_mask, 3] = 255

    return Image.fromarray(rgba, mode="RGBA")


def draw_diorama_bg(w, h):
    """Create a rich, atmospheric dusk-meadow diorama backdrop."""
    arr = np.zeros((h, w, 3), dtype=np.float32)
    yy = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None]
    xx = np.linspace(0.0, 1.0, w, dtype=np.float32)[None, :]

    # Deep atmospheric slate-indigo top -> warm dusk amber horizon -> dark grounded studio base
    sky_top = np.array([18, 22, 34], dtype=np.float32)
    sky_mid = np.array([34, 40, 58], dtype=np.float32)
    sky_hor = np.array([92, 66, 54], dtype=np.float32)
    ground_top = np.array([36, 46, 36], dtype=np.float32)
    ground_bot = np.array([18, 22, 26], dtype=np.float32)

    horizon = 0.42
    t_sky = np.clip(yy / horizon, 0.0, 1.0)
    sky = np.where(
        t_sky[..., None] < 0.65,
        sky_top * (1 - t_sky[..., None] / 0.65) + sky_mid * (t_sky[..., None] / 0.65),
        sky_mid * (1 - (t_sky[..., None] - 0.65) / 0.35) + sky_hor * ((t_sky[..., None] - 0.65) / 0.35),
    )
    t_gnd = np.clip((yy - horizon) / (1.0 - horizon), 0.0, 1.0)
    gnd = ground_top * (1 - t_gnd[..., None]) + ground_bot * t_gnd[..., None]

    # Subtle radial warm spotlight behind hero panels
    glow = np.exp(-((xx - 0.5)**2 / 0.16 + (yy - 0.36)**2 / 0.08)) * 28.0
    bg = np.broadcast_to(np.where(yy[..., None] < horizon, sky, gnd), (h, w, 3)).copy()
    bg[..., 0] += glow * 1.15
    bg[..., 1] += glow * 0.85
    bg[..., 2] += glow * 0.45

    return Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8), mode="RGB")


def paste_with_shadow(canvas, sprite, cx, bottom_y, scale=1, shadow_alpha=125):
    sw, sh = sprite.size
    rw, rh = sw * scale, sh * scale
    scaled = sprite.resize((rw, rh), Image.Resampling.NEAREST)

    # Soft elliptical ground shadow
    sh_w = int(rw * 0.56)
    sh_h = max(4, int(rw * 0.16))
    sh_img = Image.new("RGBA", (sh_w, sh_h), (0, 0, 0, 0))
    sh_arr = np.zeros((sh_h, sh_w, 4), dtype=np.uint8)
    yy = (np.arange(sh_h)[:, None] + 0.5) / sh_h * 2.0 - 1.0
    xx = (np.arange(sh_w)[None, :] + 0.5) / sh_w * 2.0 - 1.0
    r2 = xx*xx + yy*yy
    a = np.clip(1.0 - r2, 0.0, 1.0) ** 1.4 * shadow_alpha
    sh_arr[..., 0] = 10
    sh_arr[..., 1] = 10
    sh_arr[..., 2] = 18
    sh_arr[..., 3] = a.astype(np.uint8)
    sh_img = Image.fromarray(sh_arr, mode="RGBA")

    canvas.alpha_composite(sh_img, (cx - sh_w // 2, bottom_y - sh_h // 2 - int(3 * scale)))
    canvas.alpha_composite(scaled, (cx - rw // 2, bottom_y - rh))


def build_showcase(sprites_by_angle):
    W, H = 1880, 1060
    bg = draw_diorama_bg(W, H).convert("RGBA")
    draw = ImageDraw.Draw(bg)

    try:
        font_title = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 24)
        font_sub = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Regular.ttf", 14)
        font_lbl = ImageFont.truetype("/usr/share/fonts/noto/NotoSans-Bold.ttf", 12)
    except Exception:
        font_title = font_sub = font_lbl = ImageFont.load_default()

    # Header bar
    draw.text((40, 24), "ASHEN SUN VANGUARD  -  TEST CHARACTER SPRITE SHEET", fill=(244, 214, 132, 255), font=font_title)
    draw.text((40, 58), "Custom 'Gilded Dusk' HD-2D Pixel Art  |  76x88 Native Sprites  |  True 3D 8-Angle Turntable  |  Selective Coloured Outlines", fill=(176, 186, 206, 255), font=font_sub)
    draw.line([(40, 84), (W - 40, 84)], fill=(208, 152, 46, 120), width=1)

    # Top hero showcase: 3 large 5x closeups (Front-Left 315°, Front-Right 45°, Back-Right 135°)
    hero_specs = [
        (315, "HERO 3/4 FRONT-LEFT (315°)", 360),
        (45,  "HERO 3/4 FRONT-RIGHT (45°)", 940),
        (135, "HERO 3/4 BACK-RIGHT (135°)", 1520),
    ]
    for ang, lbl, cx in hero_specs:
        # Subtle card frame
        draw.rounded_rectangle([cx - 245, 102, cx + 245, 578], radius=10, fill=(16, 20, 30, 110), outline=(208, 152, 46, 80), width=1)
        paste_with_shadow(bg, sprites_by_angle[ang], cx, 548, scale=5, shadow_alpha=140)
        bbox = draw.textbbox((0, 0), lbl, font=font_lbl)
        tw = bbox[2] - bbox[0]
        draw.text((cx - tw // 2, 554), lbl, fill=(224, 212, 188, 255), font=font_lbl)

    # Middle section: Full 8-angle 360° turntable at 3x scale
    draw.text((40, 594), "FULL 360° TURNTABLE (3x CRISP PIXEL SCALE)", fill=(244, 214, 132, 255), font=font_lbl)
    draw.rounded_rectangle([28, 616, W - 28, 916], radius=10, fill=(16, 20, 30, 135), outline=(146, 166, 194, 65), width=1)

    angles = [0, 45, 90, 135, 180, 225, 270, 315]
    names = ["0° FRONT", "45° FR-R", "90° RIGHT", "135° BK-R", "180° BACK", "225° BK-L", "270° LEFT", "315° FR-L"]
    cell_w = (W - 80) // 8
    for i, (ang, nm) in enumerate(zip(angles, names)):
        cx = 40 + cell_w * i + cell_w // 2
        paste_with_shadow(bg, sprites_by_angle[ang], cx, 886, scale=3, shadow_alpha=120)
        bbox = draw.textbbox((0, 0), nm, font=font_lbl)
        tw = bbox[2] - bbox[0]
        draw.text((cx - tw // 2, 892), nm, fill=(196, 206, 224, 255), font=font_lbl)

    # Bottom bar: 1x native sprites + palette swatches
    draw.text((40, 932), "1x NATIVE SPRITES (76x88 px)", fill=(176, 186, 206, 255), font=font_lbl)
    for i, ang in enumerate(angles):
        bg.alpha_composite(sprites_by_angle[ang], (36 + i * 82, 954))

    # Palette swatches on bottom right
    draw.text((760, 932), "SIGNATURE 'GILDED DUSK' 6-TONE MATERIAL RAMPS", fill=(176, 186, 206, 255), font=font_lbl)
    pal_items = [
        ("STEEL", M_STEEL), ("GOLD", M_GOLD), ("CRIMSON", M_CRIMSON),
        ("DARK", M_DARKSTEEL), ("LEATHER", M_LEATHER), ("PLUME", M_PLUME), ("BLADE", M_BLADE),
    ]
    px0 = 760
    for idx, (pname, mid) in enumerate(pal_items):
        bx = px0 + idx * 154
        draw.text((bx, 958), pname, fill=(196, 204, 218, 255), font=font_lbl)
        ramp = RAMPS[mid]
        for s in range(6):
            c = tuple(int(v) for v in ramp[s]) + (255,)
            draw.rectangle([bx + s * 22, 980, bx + s * 22 + 20, 1018], fill=c, outline=(10, 12, 18, 255))

    return bg.convert("RGB")


def main():
    t0 = time.time()
    os.makedirs(OUT_DIR, exist_ok=True)
    angles = [0, 45, 90, 135, 180, 225, 270, 315]
    sprites = {}
    for ang in angles:
        img = render_angle(ang, W=76, H=88, pitch_deg=15.0)
        sprites[ang] = img
        img.save(os.path.join(OUT_DIR, f"sun_vanguard_{ang:03d}.png"))

    # Save 1x horizontal sprite sheet
    sheet = Image.new("RGBA", (76 * len(angles), 88), (0, 0, 0, 0))
    for i, ang in enumerate(angles):
        sheet.paste(sprites[ang], (i * 76, 0))
    sheet.save(os.path.join(OUT_DIR, "sun_vanguard_sheet.png"))

    # Save presentation showcase
    showcase = build_showcase(sprites)
    showcase_path = os.path.join(OUT_DIR, "sun_vanguard_showcase.png")
    showcase.save(showcase_path)
    print(f"Rendered 8 angles + showcase in {time.time() - t0:.2f}s -> {showcase_path}")


if __name__ == "__main__":
    main()
