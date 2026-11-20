#!/usr/bin/env python3
"""
Blender 5.x low-poly modular medieval 3D mesh kit for Ironfield's Real-Time 3D Pixel Art pipeline (Phase 1 optimized).
Run via:
  blender --background --python tools/build_meshes_3d.py

Outputs:
  Data/meshes/units3d.bin   (compact binary mesh pack loaded directly by Mesh3D.cs)
  Data/meshes/units3d.blend (editable Blender scene of all modular parts)

Phase 1 Triangle Budgets:
  - Full armored knight (Detail 0): ~380-460 tris (down from 2,284 tris: ~5x lighter!)
  - Full barded warhorse (Detail 0): ~420-490 tris (down from 2,116 tris: ~4.5x lighter!)
  - Far LOD footman (Detail 2): ~52 tris
  - Far LOD horse (Detail 2): ~58 tris

Coordinate convention (Ironfield unit-local space):
  +X = right, +Y = up, +Z = forward.

Material IDs (resolved per unit in Mesh3D.cs):
  0 = Fixed baked color (shadeColor is literal RGB)
  1 = Faction primary cloth (Style.Sides[pal].Cloth)
  2 = Faction dark cloth (Style.Sides[pal].ClothDark)
  3 = Faction trim / heraldry (Style.Sides[pal].Trim)
  4 = Steel / polished plate
  5 = Dark iron / mail
  6 = Brass / gold trim
  7 = Leather (warm saddle brown)
  8 = Wood (haft / bow / shield back)
  9 = Skin (Variety.Skins)
 10 = Hair / beard (Variety.Hairs)
 11 = Horse coat (Style.Coats)
 12 = Horse mane / tail
 13 = Horse hoof / dark bridle
"""
import math
import os
import struct
import bpy
import bmesh
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Data", "meshes")
os.makedirs(OUT_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


class PartBuilder:
    """Builds one modular low-poly 3D mesh part using Blender bmesh, storing per-triangle vertex data + material IDs."""

    def __init__(self, name):
        self.name = name
        self.tris = []

    def _emit_bm(self, bm, mat_id, shade=(1.0, 1.0, 1.0), smooth=True, ao_y=None):
        bm.normal_update()
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.verts.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
        for f in bm.faces:
            fn = f.normal.normalized()
            for v in f.verts:
                p = v.co
                n = v.normal.normalized() if smooth else fn
                ao = 1.0
                if ao_y is not None:
                    y0, y1, lo, hi = ao_y
                    t = max(0.0, min(1.0, (p.y - y0) / max(1e-5, y1 - y0)))
                    ao = lo + (hi - lo) * t
                r = max(0, min(255, int(round(shade[0] * ao * 255))))
                g = max(0, min(255, int(round(shade[1] * ao * 255))))
                b = max(0, min(255, int(round(shade[2] * ao * 255))))
                self.tris.append((
                    (float(p.x), float(p.y), float(p.z)),
                    (float(n.x), float(n.y), float(n.z)),
                    (r, g, b, 255),
                    float(mat_id),
                    0.0,
                ))
        bm.free()

    def add_loft(self, rings, mat_id, shade=(1.0, 1.0, 1.0), cap_bot=True, cap_top=True, smooth=True, ao_y=None):
        bm = bmesh.new()
        vert_rings = []
        for ring in rings:
            if isinstance(ring, tuple) and len(ring) == 6:
                cx, cy, cz, rx, rz, segs = ring
                rv = []
                for i in range(segs):
                    a = 2.0 * math.pi * i / segs
                    rv.append(bm.verts.new((cx + math.cos(a) * rx, cy, cz + math.sin(a) * rz)))
                vert_rings.append(rv)
            else:
                vert_rings.append([bm.verts.new(tuple(pt)) for pt in ring])

        n_segs = len(vert_rings[0])
        for r in range(len(vert_rings) - 1):
            a, b = vert_rings[r], vert_rings[r + 1]
            for i in range(n_segs):
                j = (i + 1) % n_segs
                try:
                    bm.faces.new((a[i], a[j], b[j], b[i]))
                except ValueError:
                    pass
        if cap_bot and len(vert_rings[0]) >= 3:
            try:
                bm.faces.new(list(reversed(vert_rings[0])))
            except ValueError:
                pass
        if cap_top and len(vert_rings[-1]) >= 3:
            try:
                bm.faces.new(vert_rings[-1])
            except ValueError:
                pass
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self._emit_bm(bm, mat_id, shade, smooth, ao_y)

    def add_loft_z(self, rings_z, mat_id, shade=(1.0, 1.0, 1.0), cap_back=True, cap_front=True, smooth=True, ao_y=None):
        pts_rings = []
        for cx, cy, cz, rx, ry, segs in rings_z:
            ring = []
            for i in range(segs):
                a = 2.0 * math.pi * i / segs
                ring.append(Vector((cx + math.cos(a) * rx, cy + math.sin(a) * ry, cz)))
            pts_rings.append(ring)
        self.add_loft(pts_rings, mat_id, shade, cap_bot=cap_back, cap_top=cap_front, smooth=smooth, ao_y=ao_y)

    def add_sphere(self, center, radius_xyz, segs=6, rings=4, mat_id=4, shade=(1.0, 1.0, 1.0), smooth=True, ao_y=None):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
        cx, cy, cz = center
        rx, ry, rz = radius_xyz if isinstance(radius_xyz, tuple) else (radius_xyz, radius_xyz, radius_xyz)
        for v in bm.verts:
            v.co = Vector((cx + v.co.x * rx, cy + v.co.z * ry, cz + v.co.y * rz))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self._emit_bm(bm, mat_id, shade, smooth, ao_y)

    def add_box(self, center, size_xyz, mat_id=4, shade=(1.0, 1.0, 1.0), rot_euler=(0, 0, 0), smooth=False, ao_y=None):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        sx, sy, sz = size_xyz
        for v in bm.verts:
            v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
        mat = Matrix.Translation(center) @ Matrix.Rotation(rot_euler[1], 4, 'Y') @ Matrix.Rotation(rot_euler[0], 4, 'X') @ Matrix.Rotation(rot_euler[2], 4, 'Z')
        bmesh.ops.transform(bm, matrix=mat, verts=bm.verts[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self._emit_bm(bm, mat_id, shade, smooth, ao_y)

    def add_cone(self, p0, p1, r0, r1, segs=5, mat_id=4, shade=(1.0, 1.0, 1.0), smooth=True, ao_y=None):
        a, b = Vector(p0), Vector(p1)
        axis = b - a
        if axis.length < 1e-5:
            return
        up = axis.normalized()
        ref = Vector((1, 0, 0)) if abs(up.x) < 0.8 else Vector((0, 0, 1))
        right = up.cross(ref).normalized()
        fwd = right.cross(up).normalized()
        r_pts0, r_pts1 = [], []
        for i in range(segs):
            ang = 2.0 * math.pi * i / segs
            d = right * math.cos(ang) + fwd * math.sin(ang)
            r_pts0.append(a + d * max(0.002, r0))
            r_pts1.append(b + d * max(0.002, r1))
        self.add_loft([r_pts0, r_pts1], mat_id, shade, cap_bot=r0 > 0.003, cap_top=r1 > 0.003, smooth=smooth, ao_y=ao_y)


# ===================== HUMANOID PARTS (CRISP LOW-POLY) =====================

def build_torso(kind):
    """High-craft stout heroic medieval torsos with real layered armor silhouettes (gorgets, pauldrons, faulds, coifs, belts)."""
    p = PartBuilder(f"torso_{kind}")
    segs = 8

    if kind == "plate":
        # Sculpted steel gothic cuirass with proud keel chest ridge, steel gorget, layered fauld/tassets & faction surcoat!
        rings = []
        profile = [
            (0.66, 0.222, 0.162, 0.008),  # fauld skirt hem
            (0.86, 0.188, 0.142, 0.008),  # crisp waist cinch
            (1.10, 0.246, 0.170, 0.014),  # heroic chest
            (1.28, 0.236, 0.150, 0.000),  # broad shoulders
            (1.39, 0.118, 0.108, 0.000),  # steel gorget rim
        ]
        for y, rx, rz, zoff in profile:
            ring = []
            for i in range(segs):
                a = 2.0 * math.pi * i / segs
                x = math.cos(a) * rx
                z = math.sin(a) * rz + zoff
                if math.sin(a) > 0.72 and 0.92 < y < 1.24:
                    z += 0.022  # sculpted breastplate keel
                ring.append(Vector((x, y, z)))
            rings.append(ring)
        p.add_loft(rings, mat_id=4, ao_y=(0.66, 1.40, 0.78, 1.08))
        # Dark chainmail skirt peeking under steel faulds
        p.add_loft([(0, 0.54, 0.0, 0.228, 0.166, segs), (0, 0.67, 0.0, 0.212, 0.154, segs)], mat_id=5, cap_top=False, ao_y=(0.54, 0.67, 0.72, 0.94))
        # Sculpted layered steel tasset plates over the hips
        for sx in (-1, 1):
            p.add_box((sx * 0.165, 0.69, 0.095), (0.135, 0.21, 0.048), mat_id=4, rot_euler=(-0.10, sx * 0.42, sx * 0.10), ao_y=(0.56, 0.82, 0.80, 1.06))
            # Shoulder gorget flanges / besagew ridges
            p.add_box((sx * 0.225, 1.25, 0.015), (0.105, 0.11, 0.195), mat_id=4, rot_euler=(0.0, 0.0, -sx * 0.24), shade=(1.04, 1.05, 1.08))
        # Flowing faction surcoat tabard panels front & back
        p.add_box((0, 0.82, 0.168), (0.215, 0.48, 0.026), mat_id=1, ao_y=(0.56, 1.08, 0.78, 1.06))
        p.add_box((0, 0.82, -0.154), (0.215, 0.48, 0.026), mat_id=1, ao_y=(0.56, 1.08, 0.74, 1.02))
        # Bold sword-belt + gilded buckle
        p.add_box((0, 0.85, 0.006), (0.42, 0.062, 0.34), mat_id=7)
        p.add_box((0, 0.85, 0.184), (0.075, 0.068, 0.024), mat_id=6)

    elif kind == "mail":
        # Ref-c style: gleaming chainmail aventail yoke & mail hauberk skirt peeking out under a belted faction surcoat!
        p.add_loft([
            (0, 0.48, 0.0, 0.236, 0.174, segs),  # mail hauberk lower hem
            (0, 0.62, 0.0, 0.224, 0.166, segs),
        ], mat_id=5, cap_top=False, ao_y=(0.48, 0.62, 0.74, 0.98))
        # Faction cloth surcoat over the mail hauberk
        p.add_loft([
            (0, 0.58, 0.0, 0.234, 0.172, segs),
            (0, 0.84, 0.0, 0.194, 0.146, segs),
            (0, 1.12, 0.01, 0.238, 0.164, segs),
            (0, 1.26, 0.0, 0.228, 0.148, segs),
        ], mat_id=1, ao_y=(0.58, 1.26, 0.76, 1.06))
        # Exposed riveted chainmail aventail collar & shoulder mantle (reads unmistakably as mail!)
        p.add_loft([
            (0, 1.21, 0.0, 0.244, 0.168, segs),
            (0, 1.40, 0.0, 0.125, 0.115, segs),
        ], mat_id=5, cap_bot=False, ao_y=(1.20, 1.40, 0.80, 1.10))
        # Leather belt, brass buckle, and side hip pouch
        p.add_box((0, 0.84, 0.0), (0.415, 0.060, 0.325), mat_id=7)
        p.add_box((0, 0.84, 0.170), (0.070, 0.064, 0.024), mat_id=6)
        p.add_box((-0.19, 0.76, 0.06), (0.075, 0.11, 0.095), mat_id=7, shade=(0.86, 0.82, 0.78))

    elif kind == "robe":
        # Flowing caster/monk robe with draped shoulder cowl mantle, ornate stole & cinched sash
        p.add_loft([
            (0, 0.18, 0.0, 0.295, 0.235, segs),
            (0, 0.54, 0.0, 0.242, 0.182, segs),
            (0, 0.85, 0.0, 0.194, 0.148, segs),
            (0, 1.15, 0.01, 0.230, 0.158, segs),
            (0, 1.36, 0.0, 0.130, 0.115, segs),
        ], mat_id=2, ao_y=(0.18, 1.36, 0.74, 1.06))
        # Draped shoulder mantle / cowl collar
        p.add_loft([
            (0, 1.12, -0.01, 0.270, 0.190, segs),
            (0, 1.28, -0.01, 0.258, 0.180, segs),
            (0, 1.39, 0.00, 0.145, 0.130, segs),
        ], mat_id=1, ao_y=(1.12, 1.39, 0.80, 1.08))
        p.add_box((0, 0.88, 0.158), (0.095, 0.68, 0.024), mat_id=3)  # long ornate stole
        p.add_box((0, 0.85, 0.0), (0.415, 0.062, 0.325), mat_id=3)   # sash
        p.add_box((0, 1.24, 0.165), (0.075, 0.060, 0.026), mat_id=6) # brass mantle clasp

    else:  # tunic / padded gambeson
        # Quilted gambeson with padded standing collar, V-laced placket, broad shoulder caps, belt & pouch
        p.add_loft([
            (0, 0.54, 0.0, 0.235, 0.174, segs),
            (0, 0.84, 0.0, 0.192, 0.145, segs),
            (0, 1.12, 0.01, 0.236, 0.162, segs),
            (0, 1.28, 0.0, 0.228, 0.146, segs),
            (0, 1.38, 0.0, 0.128, 0.116, segs),
        ], mat_id=1, ao_y=(0.54, 1.38, 0.76, 1.06))
        # Padded standing collar roll + laced V-placket + lower trim hem
        p.add_loft([(0, 1.32, 0, 0.155, 0.138, segs), (0, 1.40, 0, 0.132, 0.120, segs)], mat_id=2, cap_bot=False, ao_y=(1.32, 1.40, 0.84, 1.06))
        p.add_box((0, 1.20, 0.158), (0.075, 0.20, 0.022), mat_id=3)
        p.add_loft([(0, 0.53, 0, 0.240, 0.178, segs), (0, 0.58, 0, 0.236, 0.175, segs)], mat_id=2, cap_bot=False, cap_top=False)
        p.add_box((0, 0.84, 0.0), (0.405, 0.058, 0.315), mat_id=7)
        p.add_box((0, 0.84, 0.164), (0.065, 0.060, 0.022), mat_id=6)
        p.add_box((0.18, 0.76, 0.06), (0.072, 0.105, 0.090), mat_id=7, shade=(0.84, 0.80, 0.76))

    return p


def build_head(style):
    """Crisp low-poly 3D medieval head (~65-95 tris) centered at (0,0,0)."""
    p = PartBuilder(f"head_{style}")
    # Neck + skull
    p.add_cone((0, -0.19, -0.01), (0, -0.08, 0.0), 0.075, 0.080, segs=6, mat_id=9, shade=(0.86, 0.82, 0.80))
    p.add_sphere((0, 0.01, 0.0), (0.145, 0.165, 0.150), segs=7, rings=5, mat_id=9, ao_y=(-0.15, 0.17, 0.85, 1.06))
    # Sculpted nose & dark recessed eyes
    p.add_box((0, -0.005, 0.146), (0.040, 0.060, 0.044), mat_id=9, shade=(1.04, 1.01, 0.98))
    for sx in (-1, 1):
        p.add_box((sx * 0.052, 0.028, 0.138), (0.028, 0.036, 0.024), mat_id=0, shade=(0.11, 0.09, 0.09))

    bald = (style == 3)
    beard = style in (1, 3)
    stache = (style == 2)

    if not bald:
        p.add_sphere((0, 0.055, -0.022), (0.156, 0.138, 0.156), segs=7, rings=4, mat_id=10, ao_y=(-0.06, 0.20, 0.78, 1.08))
        p.add_box((0, -0.02, -0.095), (0.23, 0.15, 0.10), mat_id=10, ao_y=(-0.10, 0.10, 0.76, 1.0))
    else:
        p.add_box((0, -0.01, -0.095), (0.23, 0.11, 0.095), mat_id=10)

    if beard:
        p.add_sphere((0, -0.092, 0.058), (0.148, 0.118, 0.128), segs=6, rings=4, mat_id=10, ao_y=(-0.20, 0.02, 0.74, 1.05))
    elif stache:
        p.add_box((0, -0.048, 0.145), (0.12, 0.030, 0.032), mat_id=10)
    else:
        p.add_box((0, -0.062, 0.136), (0.050, 0.014, 0.016), mat_id=9, shade=(0.68, 0.56, 0.52))

    return p


def build_helm(kind):
    """High-craft sculpted medieval helmets with crisp 2x silhouettes, coifs, visors, ridges & trim."""
    p = PartBuilder(f"helm_{kind}")
    segs = 8

    def add_mail_coif(mat_id=5, shade=(0.96, 0.96, 1.0)):
        # Open-faced chainmail/linen aventail wrapping cheeks, jaw & back of neck cleanly down to gorget!
        p.add_loft([
            (0, -0.17, -0.045, 0.175, 0.145, 7),
            (0, 0.06, -0.045, 0.172, 0.140, 7),
        ], mat_id=mat_id, shade=shade, cap_bot=False, cap_top=False, ao_y=(-0.17, 0.08, 0.76, 1.02))

    if kind == "kettle":
        # Ref-c style: wide angled riveted steel brim, domed ribbed skull + snug chainmail coif framing face!
        add_mail_coif(mat_id=5)
        p.add_loft([
            (0, 0.035, 0.0, 0.295, 0.305, segs),  # bold sloped kettle brim
            (0, 0.088, 0.0, 0.198, 0.206, segs),  # brim crease
            (0, 0.185, 0.0, 0.176, 0.182, segs),  # steel dome
            (0, 0.275, 0.0, 0.042, 0.042, segs),  # peaked crown
        ], mat_id=4, ao_y=(0.03, 0.28, 0.78, 1.12))
        p.add_loft([(0, 0.085, 0, 0.203, 0.210, segs), (0, 0.120, 0, 0.198, 0.205, segs)], mat_id=5, cap_bot=False, cap_top=False)  # riveted iron brow band
        p.add_box((0, 0.215, 0.0), (0.028, 0.125, 0.24), mat_id=4, shade=(1.08, 1.08, 1.12))  # subtle crown ridge

    elif kind == "sallet":
        # Ref-c style: gothic swept lobster-tail neck guard, brow visor with dark eye-slit & crisp crown comb!
        rings = []
        levels = [
            (0.01, 0.185, 0.185, -0.065),
            (0.075, 0.196, 0.210, -0.015),
            (0.185, 0.180, 0.190, -0.008),
            (0.272, 0.042, 0.052, 0.000),
        ]
        for y, rx, rz, zoff in levels:
            ring = []
            for i in range(segs):
                a = 2.0 * math.pi * i / segs
                x = math.cos(a) * rx
                z = math.sin(a) * rz + zoff
                if math.sin(a) < -0.25 and y < 0.11:
                    z -= (0.11 - y) * 1.65 * (-math.sin(a))  # iconic gothic lobster tail!
                if math.sin(a) > 0.55 and 0.04 < y < 0.12:
                    z += 0.028  # projecting visor brow!
                ring.append(Vector((x, y, z)))
            rings.append(ring)
        p.add_loft(rings, mat_id=4, ao_y=(0.01, 0.28, 0.78, 1.12))
        # Dark horizontal visor eye-slit + steel lower visor lip
        p.add_box((0, 0.065, 0.212), (0.21, 0.028, 0.026), mat_id=0, shade=(0.06, 0.06, 0.08))
        p.add_box((0, 0.036, 0.198), (0.22, 0.034, 0.032), mat_id=4, shade=(0.94, 0.96, 1.0))
        p.add_box((0, 0.225, -0.01), (0.032, 0.105, 0.23), mat_id=4, shade=(1.10, 1.10, 1.14))  # raised gothic comb

    elif kind == "greathelm":
        # Ref-c style: bold flat-topped crusader greathelm with PROUD brass T-cross & unmistakable dark eye-slit!
        p.add_loft([
            (0, -0.155, 0.00, 0.172, 0.178, segs),
            (0, 0.040, 0.008, 0.182, 0.188, segs),
            (0, 0.230, 0.000, 0.168, 0.174, segs),
            (0, 0.275, 0.000, 0.135, 0.140, segs),  # authentic flat-topped crusader crown
        ], mat_id=4, ao_y=(-0.155, 0.275, 0.76, 1.10))
        # Proud brass brow-band + dark recessed eye-slit clearly protruding outside the steel cylinder!
        p.add_box((0, 0.062, 0.192), (0.255, 0.042, 0.034), mat_id=0, shade=(0.05, 0.05, 0.07))  # deep black ocular slit
        p.add_box((0, 0.035, 0.205), (0.044, 0.32, 0.026), mat_id=6)   # proud vertical gilded cross bar
        p.add_box((0, 0.095, 0.203), (0.275, 0.036, 0.026), mat_id=6)  # proud upper brow cross bar
        p.add_box((0, -0.045, 0.184), (0.16, 0.030, 0.026), mat_id=5, shade=(0.55, 0.56, 0.60))  # breath-plate shadow band
        p.add_loft([(0, 0.215, 0, 0.174, 0.180, segs), (0, 0.242, 0, 0.172, 0.178, segs)], mat_id=6, cap_bot=False, cap_top=False)  # crown rim band

    elif kind == "nasal":
        # Norman conical nasal helm with riveted brow rim, proud nasal bar & snug chainmail coif!
        add_mail_coif(mat_id=5)
        p.add_loft([
            (0, 0.05, 0.0, 0.186, 0.192, segs),
            (0, 0.17, 0.0, 0.156, 0.162, segs),
            (0, 0.31, 0.0, 0.026, 0.026, segs),
        ], mat_id=4, ao_y=(0.05, 0.31, 0.80, 1.12))
        p.add_loft([(0, 0.048, 0, 0.194, 0.198, segs), (0, 0.088, 0, 0.190, 0.195, segs)], mat_id=5, cap_bot=False, cap_top=False)
        p.add_box((0, -0.005, 0.192), (0.046, 0.145, 0.032), mat_id=4, shade=(1.04, 1.06, 1.10))

    elif kind == "spangen":
        # Nordic spangenhelm: brass brow band, crossed over-crown ribs, spectacle eye-arch + cheek plates & mail aventail!
        add_mail_coif(mat_id=5)
        p.add_loft([(0, 0.048, 0.0, 0.192, 0.196, segs), (0, 0.095, 0.0, 0.190, 0.194, segs)], mat_id=6, cap_bot=False, cap_top=False)
        p.add_loft([
            (0, 0.085, 0.0, 0.182, 0.188, segs),
            (0, 0.185, 0.0, 0.148, 0.154, segs),
            (0, 0.295, 0.0, 0.028, 0.028, segs),
        ], mat_id=4, ao_y=(0.08, 0.30, 0.78, 1.10))
        # Crossed spangen crown ribs + spectacle nasal guard + hinged cheek plates
        p.add_box((0, 0.195, 0.0), (0.030, 0.20, 0.31), mat_id=6)
        p.add_box((0, 0.195, 0.0), (0.31, 0.20, 0.030), mat_id=6)
        p.add_box((0, -0.005, 0.192), (0.044, 0.12, 0.028), mat_id=5)
        p.add_box((0, 0.048, 0.190), (0.18, 0.030, 0.028), mat_id=6)  # spectacle brow bar
        for sx in (-1, 1):
            p.add_box((sx * 0.165, -0.025, 0.055), (0.042, 0.14, 0.115), mat_id=4, rot_euler=(0, sx * 0.25, -sx * 0.12))

    elif kind == "turban":
        # Ref-c style: authentic low-wrapped folded linen turban around a gleaming domed steel helm + brass finial + neck cloth!
        add_mail_coif(mat_id=0, shade=(0.90, 0.88, 0.82))  # draped desert neck-cloth framing bearded face
        # Sculpted steel helmet bowl rising cleanly above the turban!
        p.add_loft([
            (0, 0.13, 0.0, 0.172, 0.176, segs),
            (0, 0.22, 0.0, 0.142, 0.146, segs),
            (0, 0.29, 0.0, 0.042, 0.042, segs),
        ], mat_id=4, ao_y=(0.13, 0.30, 0.82, 1.14))
        p.add_cone((0, 0.27, 0), (0, 0.36, 0), 0.044, 0.008, segs=5, mat_id=6)  # brass finial
        # Thick crisp white folded turban wrap sitting LOW around the brow (not a floating mushroom!)
        p.add_loft([
            (0, 0.025, 0.0, 0.212, 0.218, segs),
            (0, 0.090, 0.0, 0.228, 0.232, segs),
            (0, 0.152, 0.0, 0.188, 0.192, segs),
        ], mat_id=0, shade=(0.95, 0.93, 0.88), ao_y=(0.02, 0.16, 0.80, 1.06))
        # Diagonal criss-cross turban cloth folds + brass front clasp
        p.add_box((0, 0.095, 0.195), (0.24, 0.052, 0.070), mat_id=0, shade=(0.98, 0.96, 0.92), rot_euler=(0, 0, 0.24))
        p.add_box((0, 0.095, 0.198), (0.24, 0.052, 0.070), mat_id=0, shade=(0.91, 0.89, 0.84), rot_euler=(0, 0, -0.24))
        p.add_box((0, 0.105, 0.232), (0.048, 0.048, 0.024), mat_id=6)

    elif kind == "hood":
        # Draped medieval hood framing face with folded shoulder cowl & drooping liripipe tail
        add_mail_coif(mat_id=1, shade=(0.94, 0.94, 0.96))
        p.add_sphere((0, 0.05, -0.042), (0.192, 0.192, 0.172), segs=7, rings=5, mat_id=1, ao_y=(-0.14, 0.24, 0.78, 1.08))
        p.add_cone((0, 0.11, -0.14), (0, 0.01, -0.34), 0.115, 0.028, segs=5, mat_id=1, shade=(0.88, 0.88, 0.92))
        p.add_loft([(0, -0.17, -0.02, 0.245, 0.195, segs), (0, -0.11, -0.03, 0.190, 0.165, segs)], mat_id=2)

    elif kind == "furcap":
        # Ref-c style: plush thick rounded fur brim wrapping low over brow & ears + peaked faction cloth crown!
        p.add_loft([
            (0, 0.015, 0.0, 0.228, 0.232, segs),
            (0, 0.075, 0.0, 0.244, 0.248, segs),
            (0, 0.135, 0.0, 0.215, 0.220, segs),
        ], mat_id=7, shade=(0.86, 0.78, 0.68), ao_y=(0.01, 0.14, 0.78, 1.10))
        p.add_loft([
            (0, 0.125, 0.0, 0.175, 0.180, segs),
            (0, 0.220, 0.0, 0.130, 0.135, segs),
            (0, 0.285, 0.0, 0.035, 0.035, segs),
        ], mat_id=1, ao_y=(0.12, 0.29, 0.82, 1.08))

    else:  # cap (padded arming cap with rolled brow & ear flaps)
        p.add_loft([
            (0, 0.040, 0.0, 0.194, 0.198, segs),
            (0, 0.082, 0.0, 0.196, 0.200, segs),
        ], mat_id=2)
        p.add_loft([
            (0, 0.075, 0.0, 0.186, 0.190, segs),
            (0, 0.160, 0.0, 0.164, 0.168, segs),
            (0, 0.232, 0.0, 0.052, 0.052, segs),
        ], mat_id=1, ao_y=(0.07, 0.24, 0.80, 1.08))
        for sx in (-1, 1):
            p.add_box((sx * 0.155, -0.01, -0.01), (0.040, 0.11, 0.095), mat_id=2)

    return p


def build_limbs():
    """Low-poly smooth 5-sided limb segments with open internal joint caps (~20-32 tris each!)."""
    parts = []
    segs = 5
    for mat_tag, mat_id, is_plate in [("cloth", 1, False), ("mail", 5, False), ("plate", 4, True)]:
        ua = PartBuilder(f"uarm_{mat_tag}")
        ua.add_loft([
            (0, -0.08, 0, 0.098 if is_plate else 0.085, 0.098 if is_plate else 0.085, segs),
            (0, 0.18, 0, 0.132 if is_plate else 0.098, 0.128 if is_plate else 0.098, segs),
            (0, 0.42, 0, 0.092 if is_plate else 0.084, 0.092 if is_plate else 0.084, segs),
            (0, 1.06, 0, 0.072, 0.072, segs),
        ], mat_id=mat_id, cap_bot=False, cap_top=False, ao_y=(0.0, 1.0, 1.06, 0.84))
        if is_plate:
            # Sculpted layered steel pauldron bell capping the shoulder!
            ua.add_sphere((0, 0.10, 0.0), (0.145, 0.185, 0.142), segs=5, rings=3, mat_id=4, shade=(1.04, 1.05, 1.08), ao_y=(-0.08, 0.28, 1.08, 0.84))
        parts.append(ua)

        fa = PartBuilder(f"farm_{mat_tag}")
        fa.add_loft([
            (0, -0.06, 0, 0.078 if is_plate else 0.074, 0.078 if is_plate else 0.074, segs),
            (0, 0.65, 0, 0.066, 0.066, segs),
            (0, 0.82, 0, 0.082 if is_plate else 0.074, 0.082 if is_plate else 0.074, segs),  # flared gauntlet / glove cuff
            (0, 1.04, 0, 0.070, 0.070, segs),
        ], mat_id=mat_id if is_plate else (7 if mat_tag == "cloth" else mat_id), cap_bot=False, cap_top=True, ao_y=(0.0, 1.0, 1.00, 0.84))
        parts.append(fa)

        th = PartBuilder(f"thigh_{mat_tag}")
        th.add_loft([
            (0, -0.08, 0, 0.104, 0.108, segs),
            (0, 0.45, 0, 0.100, 0.104, segs),
            (0, 1.06, 0, 0.086, 0.092, segs),
        ], mat_id=2 if mat_tag == "cloth" else mat_id, cap_bot=False, cap_top=False, ao_y=(0.0, 1.0, 0.96, 0.84))
        parts.append(th)

        sh = PartBuilder(f"shin_{mat_tag}")
        sh.add_loft([
            (0, -0.06, 0, 0.084, 0.088, segs),
            (0, 0.55, 0, 0.080, 0.084, segs),
            (0, 0.98, 0, 0.072, 0.076, segs),
        ], mat_id=7 if mat_tag == "cloth" else mat_id, cap_bot=False, cap_top=False, ao_y=(0.0, 1.0, 0.95, 0.78))
        sh.add_box((0, 0.96, 0.055), (0.125, 0.14, 0.21), mat_id=4 if is_plate else 7, shade=(0.90, 0.88, 0.86))
        parts.append(sh)

    # Plume & Draped Tapered Cape
    pl = PartBuilder("plume")
    pl.add_loft([
        (0, 0.00, 0.00, 0.045, 0.045, 6),
        (0, 0.16, -0.05, 0.092, 0.112, 6),
        (0, 0.26, -0.17, 0.068, 0.096, 6),
        (0, 0.18, -0.28, 0.025, 0.045, 6),
    ], mat_id=1, ao_y=(0.0, 0.28, 0.88, 1.15))
    parts.append(pl)

    cp = PartBuilder("cape")
    cape_rings = []
    for y, hw, zc, curve in [
        (0.28, 0.28, -0.32, 0.05),
        (0.75, 0.24, -0.24, 0.035),
        (1.28, 0.20, -0.16, 0.02),
    ]:
        cape_rings.append([
            Vector((-hw, y, zc + curve)),
            Vector((0.0, y, zc - curve * 0.6)),
            Vector((hw, y, zc + curve)),
            Vector((hw, y, zc + curve + 0.035)),
            Vector((0.0, y, zc - curve * 0.6 + 0.035)),
            Vector((-hw, y, zc + curve + 0.035)),
        ])
    cp.add_loft(cape_rings, mat_id=1, smooth=True, ao_y=(0.28, 1.28, 0.74, 1.06))
    parts.append(cp)
    return parts


# ===================== WEAPONS & SHIELDS (CRISP LOW-POLY) =====================

def build_weapons_and_shields():
    parts = []

    def sword(name, blade_len, blade_w, grip_len, guard_w, gilt=False):
        p = PartBuilder(name)
        p.add_box((0, -grip_len, 0), (0.065, 0.065, 0.05), mat_id=6)
        p.add_cone((0, -grip_len + 0.02, 0), (0, 0.04, 0), 0.022, 0.024, segs=4, mat_id=7)
        p.add_box((0, 0.05, 0), (guard_w, 0.038, 0.042), mat_id=6 if gilt else 5)
        b_rings = [
            [Vector((-blade_w, 0.07, 0)), Vector((0, 0.07, 0.024)), Vector((blade_w, 0.07, 0)), Vector((0, 0.07, -0.024))],
            [Vector((-blade_w * 0.82, 0.07 + blade_len * 0.78, 0)), Vector((0, 0.07 + blade_len * 0.78, 0.020)),
             Vector((blade_w * 0.82, 0.07 + blade_len * 0.78, 0)), Vector((0, 0.07 + blade_len * 0.78, -0.020))],
            [Vector((-0.004, 0.07 + blade_len, 0)), Vector((0, 0.07 + blade_len, 0.004)),
             Vector((0.004, 0.07 + blade_len, 0)), Vector((0, 0.07 + blade_len, -0.004))],
        ]
        p.add_loft(b_rings, mat_id=4, smooth=False, shade=(1.06, 1.07, 1.10))
        return p

    parts.append(sword("weapon_sword", 0.78, 0.048, 0.16, 0.22, gilt=True))
    parts.append(sword("weapon_longsword", 1.06, 0.054, 0.26, 0.28, gilt=True))
    parts.append(sword("weapon_greatsword", 1.32, 0.062, 0.34, 0.34, gilt=True))

    ax = PartBuilder("weapon_axe")
    ax.add_cone((0, -0.22, 0), (0, 0.78, 0), 0.024, 0.026, segs=5, mat_id=8)
    ax.add_box((0, 0.68, 0.09), (0.042, 0.22, 0.18), mat_id=4, shade=(1.02, 1.04, 1.08))
    parts.append(ax)

    mc = PartBuilder("weapon_mace")
    mc.add_cone((0, -0.18, 0), (0, 0.62, 0), 0.022, 0.024, segs=5, mat_id=8)
    mc.add_sphere((0, 0.68, 0), (0.085, 0.10, 0.085), segs=5, rings=4, mat_id=5, smooth=False)
    parts.append(mc)

    for sname, slen in [("weapon_spear", 2.15), ("weapon_pike", 2.95)]:
        sp = PartBuilder(sname)
        sp.add_cone((0, -slen * 0.32, 0), (0, slen * 0.68, 0), 0.035, 0.032, segs=5, mat_id=8)
        sp.add_cone((0, slen * 0.66, 0), (0, slen * 0.66 + 0.34, 0), 0.052, 0.006, segs=4, mat_id=4, smooth=False, shade=(1.08, 1.08, 1.12))
        parts.append(sp)

    ln = PartBuilder("weapon_lance")
    ln.add_cone((0, -0.75, 0), (0, 2.35, 0), 0.044, 0.030, segs=6, mat_id=8)
    ln.add_cone((0, -0.05, 0), (0, 0.24, 0), 0.125, 0.040, segs=6, mat_id=4)
    ln.add_cone((0, 2.32, 0), (0, 2.68, 0), 0.042, 0.006, segs=4, mat_id=4, smooth=False)
    ln.add_box((0, 2.02, -0.14), (0.026, 0.36, 0.24), mat_id=1)
    parts.append(ln)

    hb = PartBuilder("weapon_halberd")
    hb.add_cone((0, -0.68, 0), (0, 1.68, 0), 0.035, 0.034, segs=5, mat_id=8)
    hb.add_cone((0, 1.65, 0), (0, 2.08, 0), 0.038, 0.004, segs=4, mat_id=4, smooth=False)
    hb.add_box((0, 1.54, 0.12), (0.036, 0.28, 0.22), mat_id=4)
    hb.add_box((0, 1.56, -0.09), (0.030, 0.08, 0.14), mat_id=5)
    parts.append(hb)

    bw = PartBuilder("weapon_bow")
    for k in range(-2, 2):
        y0, y1 = k * 0.27, (k + 1) * 0.27
        z0 = -0.18 * (y0 / 0.54) ** 2
        z1 = -0.18 * (y1 / 0.54) ** 2
        bw.add_cone((0, y0, z0), (0, y1, z1), 0.022, 0.020, segs=4, mat_id=8)
    bw.add_cone((0, -0.54, -0.18), (0, 0.54, -0.18), 0.008, 0.008, segs=3, mat_id=3)
    parts.append(bw)

    cb = PartBuilder("weapon_crossbow")
    cb.add_box((0, 0.18, 0), (0.05, 0.62, 0.06), mat_id=8)
    cb.add_box((0, 0.44, 0), (0.64, 0.04, 0.04), mat_id=5)
    parts.append(cb)

    st = PartBuilder("weapon_staff")
    st.add_cone((0, -0.70, 0), (0, 1.28, 0), 0.026, 0.028, segs=5, mat_id=8)
    st.add_sphere((0, 1.36, 0), (0.075, 0.095, 0.075), segs=6, rings=4, mat_id=6)
    parts.append(st)

    # Shields (~36-52 tris each, clean medieval heater/kite/round bases; per-realm heraldic emblems emit dynamically!)
    hs = PartBuilder("shield_heater")
    hs.add_box((0, 0.04, 0.022), (0.50, 0.60, 0.030), mat_id=5, shade=(0.88, 0.90, 0.94))  # iron rim backing
    hs.add_box((0, 0.04, 0.044), (0.44, 0.54, 0.036), mat_id=1, ao_y=(-0.26, 0.34, 0.80, 1.06))
    hs.add_box((0, -0.26, 0.020), (0.30, 0.17, 0.028), mat_id=5, shade=(0.84, 0.86, 0.90))
    hs.add_box((0, -0.25, 0.040), (0.25, 0.14, 0.034), mat_id=1, shade=(0.88, 0.88, 0.90))
    parts.append(hs)

    ks = PartBuilder("shield_kite")
    ks.add_box((0, 0.08, 0.022), (0.46, 0.64, 0.030), mat_id=5, shade=(0.88, 0.90, 0.94))
    ks.add_box((0, 0.08, 0.044), (0.40, 0.58, 0.036), mat_id=1, ao_y=(-0.36, 0.40, 0.78, 1.06))
    ks.add_box((0, -0.30, 0.020), (0.26, 0.31, 0.028), mat_id=5, shade=(0.84, 0.86, 0.90))
    ks.add_box((0, -0.29, 0.040), (0.21, 0.27, 0.034), mat_id=1, shade=(0.86, 0.86, 0.88))
    parts.append(ks)

    rs = PartBuilder("shield_round")
    rs.add_loft_z([(0, 0, 0.00, 0.315, 0.315, 8), (0, 0, 0.032, 0.315, 0.315, 8)], mat_id=5, shade=(0.88, 0.90, 0.94))
    rs.add_loft_z([(0, 0, 0.026, 0.285, 0.285, 8), (0, 0, 0.060, 0.285, 0.285, 8)], mat_id=1, ao_y=(-0.30, 0.30, 0.82, 1.08))
    rs.add_sphere((0, 0, 0.078), (0.085, 0.085, 0.042), segs=6, rings=3, mat_id=4)
    parts.append(rs)

    return parts


# ===================== WARHORSE PARTS (LOW-POLY ~440 TRIS TOTAL!) =====================

def build_horse():
    parts = []
    segs = 8

    for barded in (False, True):
        hb = PartBuilder("horse_body_barded" if barded else "horse_body")
        hb.add_loft_z([
            (0, 0.04, -0.94, 0.14, 0.14, segs),
            (0, 0.08, -0.74, 0.32, 0.31, segs),
            (0, 0.03, -0.05, 0.30, 0.30, segs),
            (0, 0.07, 0.50, 0.34, 0.35, segs),
            (0, 0.02, 0.92, 0.15, 0.16, segs),
        ], mat_id=11, ao_y=(-0.35, 0.42, 0.76, 1.08))

        if barded:
            hb.add_loft_z([
                (0, -0.05, -0.90, 0.25, 0.25, segs),
                (0, -0.09, -0.58, 0.37, 0.42, segs),
                (0, -0.09, 0.48, 0.38, 0.44, segs),
                (0, -0.03, 0.94, 0.17, 0.19, segs),
            ], mat_id=1, ao_y=(-0.50, 0.35, 0.76, 1.06))
            hb.add_sphere((0, 0.06, 0.78), (0.35, 0.27, 0.20), segs=7, rings=4, mat_id=4, shade=(1.02, 1.04, 1.08))
            hb.add_box((0, -0.48, 0.0), (0.72, 0.055, 1.50), mat_id=3)

        hb.add_box((0, 0.26, -0.02), (0.62, 0.12, 0.54), mat_id=1)
        hb.add_box((0, 0.32, -0.02), (0.48, 0.09, 0.42), mat_id=7)
        hb.add_box((0, 0.40, -0.21), (0.42, 0.12, 0.09), mat_id=7)
        parts.append(hb)

    for barded in (False, True):
        hn = PartBuilder("horse_neck_barded" if barded else "horse_neck")
        hn.add_loft([
            (0, -0.10, 0.00, 0.24, 0.30, 7),
            (0, 0.48, 0.02, 0.19, 0.23, 7),
            (0, 1.06, 0.00, 0.12, 0.14, 7),
        ], mat_id=11, ao_y=(0.0, 1.0, 0.84, 1.06))
        hn.add_loft([
            (0, 0.02, -0.26, 0.07, 0.09, 5),
            (0, 0.55, -0.23, 0.08, 0.10, 5),
            (0, 1.04, -0.16, 0.06, 0.07, 5),
        ], mat_id=12, ao_y=(0.0, 1.0, 0.80, 1.05))
        if barded:
            hn.add_loft([
                (0, 0.08, -0.05, 0.23, 0.24, 7),
                (0, 0.94, -0.03, 0.16, 0.16, 7),
            ], mat_id=4, ao_y=(0.0, 1.0, 0.85, 1.08))
        parts.append(hn)

    for barded in (False, True):
        hh = PartBuilder("horse_head_barded" if barded else "horse_head")
        hh.add_loft_z([
            (0, 0.02, -0.10, 0.12, 0.13, 7),
            (0, 0.01, 0.10, 0.168, 0.195, 7),
            (0, 0.01, 0.42, 0.112, 0.128, 7),
            (0, -0.01, 0.66, 0.084, 0.094, 7),
        ], mat_id=11, ao_y=(-0.22, 0.22, 0.80, 1.08))
        hh.add_box((0, -0.01, 0.62), (0.19, 0.19, 0.12), mat_id=12)
        for sx in (-1, 1):
            hh.add_cone((sx * 0.105, 0.14, 0.00), (sx * 0.135, 0.34, 0.04), 0.052, 0.010, segs=4, mat_id=11, shade=(1.04, 1.02, 1.00))
            hh.add_box((sx * 0.146, 0.065, 0.21), (0.042, 0.052, 0.052), mat_id=0, shade=(0.09, 0.07, 0.07))
        hh.add_cone((0, 0.17, 0.02), (0, 0.15, 0.22), 0.062, 0.015, segs=4, mat_id=12)
        hh.add_box((0, 0.03, 0.13), (0.33, 0.37, 0.042), mat_id=13)
        hh.add_box((0, 0.00, 0.49), (0.23, 0.26, 0.042), mat_id=13)
        if barded:
            hh.add_loft_z([
                (0, 0.08, 0.06, 0.145, 0.135, 6),
                (0, 0.06, 0.52, 0.095, 0.085, 6),
            ], mat_id=4, shade=(1.04, 1.06, 1.10))
            hh.add_box((0, 0.20, 0.18), (0.075, 0.05, 0.075), mat_id=6)
        parts.append(hh)

    # Horse legs: clean 5-sided smooth lofts with open internal joint caps (~18 tris each!)
    hl_up = PartBuilder("horse_leg_up")
    hl_up.add_loft([
        (0, -0.10, 0, 0.14, 0.16, 5),
        (0, 0.45, 0, 0.115, 0.125, 5),
        (0, 1.08, 0, 0.085, 0.09, 5),
    ], mat_id=11, cap_bot=False, cap_top=False, ao_y=(0.0, 1.0, 0.96, 0.84))
    parts.append(hl_up)

    hl_low = PartBuilder("horse_leg_low")
    hl_low.add_loft([
        (0, -0.08, 0, 0.085, 0.088, 5),
        (0, 0.78, 0, 0.072, 0.078, 5),
    ], mat_id=11, cap_bot=False, cap_top=False, ao_y=(0.0, 1.0, 0.94, 0.80))
    hl_low.add_loft([
        (0, 0.78, 0.00, 0.078, 0.084, 5),
        (0, 1.04, 0.04, 0.092, 0.106, 5),
    ], mat_id=13, cap_bot=False, cap_top=True, ao_y=(0.78, 1.04, 0.95, 0.72))
    parts.append(hl_low)

    ht = PartBuilder("horse_tail")
    ht.add_loft([
        (0, -0.05, 0, 0.07, 0.07, 5),
        (0, 0.55, 0, 0.105, 0.105, 5),
        (0, 1.04, 0, 0.025, 0.025, 5),
    ], mat_id=12, ao_y=(0.0, 1.0, 1.02, 0.75))
    parts.append(ht)

    return parts


# ===================== FAR LOD SILHOUETTES (Detail == 2 / Far Shadows) =====================

def build_lods():
    """Ultra-lightweight 3D silhouettes (~52 tris/footman, ~58 tris/horse) for distant armies > 95m."""
    parts = []

    lf = PartBuilder("lod_foot")
    # Single tapered column for legs + torso (faction cloth) + steel helm cap + raised shield/weapon hint!
    lf.add_loft([
        (0, 0.00, 0.0, 0.18, 0.14, 5),
        (0, 0.68, 0.0, 0.21, 0.16, 5),
        (0, 1.32, 0.0, 0.24, 0.16, 5),
    ], mat_id=1, ao_y=(0.0, 1.35, 0.72, 1.06))
    lf.add_box((0, 1.56, 0.0), (0.30, 0.34, 0.30), mat_id=4)  # head+helm block
    lf.add_box((-0.24, 0.98, 0.14), (0.14, 0.52, 0.36), mat_id=3)  # shield silhouette
    parts.append(lf)

    lh = PartBuilder("lod_horse")
    lh.add_box((0, 1.25, 0.0), (0.64, 0.62, 1.75), mat_id=11, ao_y=(0.9, 1.6, 0.76, 1.08))  # barrel
    lh.add_box((0, 1.78, 0.96), (0.34, 0.64, 0.44), mat_id=11)  # neck+head
    lh.add_box((0, 0.48, 0.56), (0.56, 0.96, 0.28), mat_id=11, shade=(0.82, 0.82, 0.82))  # forelegs pair
    lh.add_box((0, 0.48, -0.58), (0.56, 0.96, 0.28), mat_id=11, shade=(0.80, 0.80, 0.80))  # hindlegs pair
    parts.append(lh)

    return parts


def save_blend(all_parts):
    clear_scene()
    for idx, part in enumerate(all_parts):
        mesh = bpy.data.meshes.new(part.name)
        verts, faces = [], []
        for i in range(0, len(part.tris), 3):
            base = len(verts)
            for k in range(3):
                p = part.tris[i + k][0]
                verts.append((p[0], -p[2], p[1]))
            faces.append((base, base + 1, base + 2))
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(part.name, mesh)
        obj.location = ((idx % 8) * 2.4, (idx // 8) * 2.8, 0)
        bpy.context.collection.objects.link(obj)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "units3d.blend"))


def export_bin(all_parts):
    path = os.path.join(OUT_DIR, "units3d.bin")
    total_tris = 0
    counts = {}
    with open(path, "wb") as f:
        f.write(b"IMSH")
        f.write(struct.pack("<II", 1, len(all_parts)))
        for part in all_parts:
            nb = part.name.encode("utf-8")
            f.write(struct.pack("<H", len(nb)))
            f.write(nb)
            f.write(struct.pack("<I", len(part.tris)))
            t = len(part.tris) // 3
            total_tris += t
            counts[part.name] = t
            for pos, norm, rgba, mat_id, aux in part.tris:
                f.write(struct.pack(
                    "<ffffffffBBBB",
                    pos[0], pos[1], pos[2],
                    norm[0], norm[1], norm[2],
                    mat_id, aux,
                    rgba[0], rgba[1], rgba[2], rgba[3],
                ))
    size_kb = os.path.getsize(path) / 1024.0
    knight = sum(counts[k] for k in ["torso_plate", "head_0", "helm_sallet", "uarm_plate", "uarm_plate", "farm_plate", "farm_plate", "thigh_plate", "thigh_plate", "shin_plate", "shin_plate", "weapon_sword", "shield_heater"])
    horse = sum(counts[k] for k in ["horse_body_barded", "horse_neck_barded", "horse_head_barded", "horse_tail", "horse_leg_up", "horse_leg_up", "horse_leg_up", "horse_leg_up", "horse_leg_low", "horse_leg_low", "horse_leg_low", "horse_leg_low"])
    print(f"Exported {len(all_parts)} modular 3D parts ({total_tris} tris total, {size_kb:.1f} KB) -> {path}")
    print(f"  Detail 0 Knight: {knight} tris | Detail 0 Barded Horse: {horse} tris | Detail 2 LOD Foot: {counts['lod_foot']} tris | LOD Horse: {counts['lod_horse']} tris")


def main():
    clear_scene()
    all_parts = []
    for k in ("tunic", "mail", "plate", "robe"):
        all_parts.append(build_torso(k))
    for st in range(4):
        all_parts.append(build_head(st))
    for hk in ("kettle", "sallet", "greathelm", "nasal", "spangen", "turban", "hood", "cap", "furcap"):
        all_parts.append(build_helm(hk))
    all_parts.extend(build_limbs())
    all_parts.extend(build_weapons_and_shields())
    all_parts.extend(build_horse())
    all_parts.extend(build_lods())
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import build_meshes_phase2
    all_parts.extend(build_meshes_phase2.build_all(PartBuilder))

    export_bin(all_parts)
    save_blend(all_parts)


if __name__ == "__main__":
    main()
