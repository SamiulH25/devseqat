"""
Phase 2 modular 3D mesh builders for Ironfield (imported by tools/build_meshes_3d.py):
  - 13 additional iconic medieval/realm helms (covering all 42 helm IDs)
  - 7 additional body armor silhouettes + skeleton bone limbs (covering all 29 body IDs)
  - 10 additional weapons + 3 additional shields (covering all 29 weapons & 12 shields)
  - Camel mount + scale/fur horse barding variants
  - 4 non-human humanoid heads (goblin, troll, skull, ghoul)
  - 4 complete 3D beast kits (wolf/warg, cave bear, giant spider, wyvern)
"""
import math
from mathutils import Vector


def build_phase2_helms(PartBuilder):
    parts = []
    segs = 8

    def greathelm_base(name):
        p = PartBuilder(name)
        p.add_loft([
            (0, -0.15, 0.00, 0.175, 0.185, segs),
            (0, 0.04, 0.01, 0.185, 0.195, segs),
            (0, 0.22, 0.00, 0.168, 0.178, segs),
            (0, 0.27, 0.00, 0.090, 0.095, segs),
        ], mat_id=4, ao_y=(-0.15, 0.27, 0.78, 1.08))
        p.add_box((0, 0.055, 0.182), (0.24, 0.032, 0.034), mat_id=0, shade=(0.08, 0.08, 0.10))
        p.add_box((0, 0.04, 0.194), (0.040, 0.29, 0.020), mat_id=6)
        p.add_box((0, 0.055, 0.194), (0.26, 0.034, 0.020), mat_id=6)
        return p

    # 1. Winged greathelm (twin swept gilded/faction wings!)
    gw = greathelm_base("helm_greathelm_wings")
    for sx in (-1, 1):
        gw.add_box((sx * 0.24, 0.26, -0.02), (0.14, 0.30, 0.055), mat_id=6, rot_euler=(0.0, sx * 0.25, -sx * 0.32), ao_y=(0.12, 0.44, 0.80, 1.12))
        gw.add_box((sx * 0.31, 0.38, -0.05), (0.12, 0.18, 0.042), mat_id=3, rot_euler=(0.0, sx * 0.30, -sx * 0.45))
    parts.append(gw)

    # 2. Sunburst greathelm (Order of the Ashen Sun radiant crown!)
    gs = greathelm_base("helm_greathelm_sun")
    gs.add_loft([(0, 0.21, 0, 0.175, 0.182, segs), (0, 0.27, 0, 0.182, 0.190, segs)], mat_id=6)
    for k in range(5):
        ang = (k - 2) * 0.55
        gs.add_cone((math.sin(ang) * 0.14, 0.26, math.cos(ang) * 0.14),
                    (math.sin(ang) * 0.24, 0.44, math.cos(ang) * 0.24), 0.036, 0.006, segs=4, mat_id=6)
    parts.append(gs)

    # 3. Horned greathelm
    gh = greathelm_base("helm_greathelm_horns")
    for sx in (-1, 1):
        gh.add_cone((sx * 0.15, 0.18, 0.0), (sx * 0.32, 0.30, 0.02), 0.052, 0.028, segs=5, mat_id=3)
        gh.add_cone((sx * 0.32, 0.30, 0.02), (sx * 0.26, 0.46, 0.06), 0.028, 0.008, segs=5, mat_id=3)
    parts.append(gh)

    # 4. Horned northern spangenhelm
    sh = PartBuilder("helm_spangen_horns")
    sh.add_loft([(0, 0.05, 0, 0.188, 0.192, segs), (0, 0.09, 0, 0.186, 0.190, segs)], mat_id=6)
    sh.add_loft([(0, 0.08, 0, 0.180, 0.185, segs), (0, 0.18, 0, 0.145, 0.150, segs), (0, 0.29, 0, 0.025, 0.025, segs)], mat_id=4, ao_y=(0.08, 0.29, 0.80, 1.08))
    sh.add_box((0, 0.01, 0.185), (0.042, 0.10, 0.024), mat_id=5)
    for sx in (-1, 1):
        sh.add_cone((sx * 0.15, 0.14, 0.0), (sx * 0.33, 0.24, 0.03), 0.048, 0.024, segs=5, mat_id=3)
        sh.add_cone((sx * 0.33, 0.24, 0.03), (sx * 0.28, 0.42, 0.08), 0.024, 0.007, segs=5, mat_id=3)
    parts.append(sh)

    # 5. Bear-head pelt helm (Northmarch berserkers!)
    bh = PartBuilder("helm_bearhead")
    bh.add_sphere((0, 0.10, -0.02), (0.215, 0.195, 0.215), segs=7, rings=5, mat_id=7, shade=(0.82, 0.72, 0.64), ao_y=(-0.06, 0.30, 0.76, 1.08))
    bh.add_box((0, 0.13, 0.18), (0.14, 0.10, 0.14), mat_id=7, shade=(1.08, 0.98, 0.88))  # bear muzzle crest
    bh.add_box((0, 0.14, 0.25), (0.055, 0.045, 0.03), mat_id=0, shade=(0.12, 0.10, 0.10))  # bear nose
    for sx in (-1, 1):
        bh.add_sphere((sx * 0.16, 0.27, 0.02), (0.058, 0.058, 0.045), segs=5, rings=3, mat_id=7, shade=(0.76, 0.66, 0.58))
    parts.append(bh)

    # 6. Morion (Saltmarsh League swept-brim combed Renaissance helmet!)
    mr = PartBuilder("helm_morion")
    rings = []
    for y, rx, rz, front_up in [(0.045, 0.24, 0.32, 0.075), (0.085, 0.19, 0.20, 0.0), (0.18, 0.17, 0.18, 0.0), (0.26, 0.05, 0.06, 0.0)]:
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            x = math.cos(a) * rx
            z = math.sin(a) * rz
            yy = y + front_up * abs(math.sin(a))  # iconic upturned front & back brim!
            ring.append(Vector((x, yy, z)))
        rings.append(ring)
    mr.add_loft(rings, mat_id=4, ao_y=(0.04, 0.28, 0.78, 1.12))
    mr.add_box((0, 0.24, 0.0), (0.036, 0.15, 0.28), mat_id=4, shade=(1.10, 1.10, 1.14))  # tall morion comb
    parts.append(mr)

    # 7. Barbute (Italian open-T cheek-guard warhelm with brass crest)
    bb = PartBuilder("helm_barbute")
    bb.add_loft([
        (0, -0.10, -0.02, 0.185, 0.185, segs),
        (0, 0.06, 0.00, 0.190, 0.195, segs),
        (0, 0.18, 0.00, 0.175, 0.180, segs),
        (0, 0.26, 0.00, 0.060, 0.065, segs),
    ], mat_id=4, ao_y=(-0.10, 0.27, 0.78, 1.10))
    bb.add_box((0, 0.25, -0.01), (0.034, 0.11, 0.24), mat_id=6)
    parts.append(bb)

    # 8. Steppe helm (Khaganate tall conical steel helm + fur brim)
    st = PartBuilder("helm_steppe")
    st.add_loft([(0, 0.04, 0, 0.215, 0.220, segs), (0, 0.10, 0, 0.210, 0.215, segs)], mat_id=7, shade=(0.76, 0.68, 0.60))
    st.add_loft([(0, 0.09, 0, 0.175, 0.180, segs), (0, 0.22, 0, 0.125, 0.130, segs), (0, 0.36, 0, 0.018, 0.018, segs)], mat_id=4, ao_y=(0.09, 0.36, 0.80, 1.12))
    parts.append(st)

    # 9. Tug khan helm (tall gilded steppe cone + streaming horsehair tail!)
    tg = PartBuilder("helm_tug")
    tg.add_loft([(0, 0.04, 0, 0.220, 0.225, segs), (0, 0.10, 0, 0.215, 0.220, segs)], mat_id=7, shade=(0.74, 0.66, 0.58))
    tg.add_loft([(0, 0.09, 0, 0.178, 0.182, segs), (0, 0.24, 0, 0.120, 0.125, segs), (0, 0.38, 0, 0.022, 0.022, segs)], mat_id=4, ao_y=(0.09, 0.38, 0.80, 1.12))
    tg.add_cone((0, 0.36, 0), (0, 0.45, -0.04), 0.032, 0.018, segs=5, mat_id=6)
    tg.add_cone((0, 0.44, -0.04), (0, 0.18, -0.34), 0.045, 0.018, segs=5, mat_id=12)  # streaming dark horsehair tug
    parts.append(tg)

    # 10. Spiked desert turban (Sunward Emirates tall spike + thick linen wrap)
    su = PartBuilder("helm_spike_turban")
    su.add_loft([(0, 0.04, 0, 0.225, 0.230, segs), (0, 0.12, 0, 0.242, 0.246, segs), (0, 0.20, 0, 0.175, 0.180, segs)], mat_id=3, ao_y=(0.04, 0.21, 0.82, 1.08))
    su.add_cone((0, 0.17, 0), (0, 0.41, 0), 0.115, 0.012, segs=6, mat_id=4)
    su.add_box((0, 0.13, 0.235), (0.055, 0.055, 0.028), mat_id=6)  # front jewel clasp
    parts.append(su)

    # 11. Wide-brimmed brigand / ranger hat
    br = PartBuilder("helm_brigand_hat")
    br.add_loft([(0, 0.05, 0, 0.31, 0.31, segs), (0, 0.08, 0, 0.18, 0.18, segs), (0, 0.21, 0, 0.15, 0.15, segs), (0, 0.24, 0, 0.08, 0.08, segs)], mat_id=7, ao_y=(0.05, 0.25, 0.78, 1.08))
    br.add_loft([(0, 0.08, 0, 0.186, 0.186, segs), (0, 0.115, 0, 0.182, 0.182, segs)], mat_id=3)
    parts.append(br)

    # 12. Sallet with brass crest
    sc = PartBuilder("helm_sallet_crest")
    sc.add_loft([(0, 0.04, -0.04, 0.19, 0.22, segs), (0, 0.16, -0.01, 0.18, 0.19, segs), (0, 0.26, 0, 0.045, 0.05, segs)], mat_id=4, ao_y=(0.04, 0.27, 0.80, 1.10))
    sc.add_box((0, 0.26, -0.02), (0.036, 0.13, 0.25), mat_id=6)
    parts.append(sc)

    # 13. Dragon-crested warhelm
    dr = greathelm_base("helm_dragon")
    dr.add_box((0, 0.31, -0.01), (0.055, 0.14, 0.26), mat_id=6)
    dr.add_cone((0, 0.34, 0.10), (0, 0.38, 0.28), 0.052, 0.018, segs=4, mat_id=6)
    parts.append(dr)

    return parts


def build_phase2_bodies_and_limbs(PartBuilder):
    parts = []
    segs = 8

    # 1. Gilded knightly plate (brass shoulder ridges, trim & heraldic surcoat)
    pg = PartBuilder("torso_plate_gilded")
    pg.add_loft([
        (0, 0.64, 0.01, 0.230, 0.170, segs),
        (0, 0.84, 0.01, 0.195, 0.148, segs),
        (0, 1.12, 0.02, 0.252, 0.180, segs),
        (0, 1.29, 0.00, 0.242, 0.155, segs),
        (0, 1.38, 0.00, 0.120, 0.110, segs),
    ], mat_id=4, ao_y=(0.64, 1.38, 0.80, 1.08))
    pg.add_box((0, 0.82, 0.168), (0.20, 0.44, 0.028), mat_id=1)
    pg.add_box((0, 0.82, -0.158), (0.20, 0.44, 0.028), mat_id=1)
    pg.add_box((0, 1.12, 0.182), (0.24, 0.06, 0.026), mat_id=6)  # gilded chest bar
    pg.add_box((0, 0.84, 0.005), (0.43, 0.06, 0.35), mat_id=6)   # gilded belt
    parts.append(pg)

    # 2. Lamellar armor (Sunward/Khaganate golden-brass horizontal lames + cloth skirt)
    lm = PartBuilder("torso_lamellar")
    lm.add_loft([
        (0, 0.54, 0, 0.245, 0.185, segs),
        (0, 0.80, 0, 0.210, 0.160, segs),
    ], mat_id=1, ao_y=(0.54, 0.82, 0.76, 1.04))
    lm.add_loft([
        (0, 0.78, 0, 0.215, 0.162, segs),
        (0, 1.12, 0.01, 0.242, 0.172, segs),
        (0, 1.28, 0, 0.232, 0.152, segs),
        (0, 1.37, 0, 0.120, 0.105, segs),
    ], mat_id=6, ao_y=(0.78, 1.37, 0.78, 1.08))
    lm.add_box((0, 0.84, 0), (0.43, 0.06, 0.34), mat_id=2)
    parts.append(lm)

    # 3. Scale hauberk (iron/steel scales + brass hem & sash)
    sc = PartBuilder("torso_scale")
    sc.add_loft([
        (0, 0.56, 0, 0.238, 0.175, segs),
        (0, 0.84, 0, 0.202, 0.154, segs),
        (0, 1.12, 0.01, 0.242, 0.170, segs),
        (0, 1.28, 0, 0.230, 0.150, segs),
        (0, 1.37, 0, 0.118, 0.105, segs),
    ], mat_id=5, ao_y=(0.56, 1.37, 0.78, 1.08))
    sc.add_box((0, 0.84, 0), (0.42, 0.06, 0.33), mat_id=1)
    sc.add_box((0, 0.58, 0), (0.46, 0.05, 0.34), mat_id=6)
    parts.append(sc)

    # 4. Fur-mantled northern warlord/clansman torso
    fr = PartBuilder("torso_fur")
    fr.add_loft([
        (0, 0.58, 0, 0.230, 0.170, segs),
        (0, 0.84, 0, 0.198, 0.150, segs),
        (0, 1.12, 0.01, 0.238, 0.165, segs),
        (0, 1.28, 0, 0.228, 0.148, segs),
    ], mat_id=1, ao_y=(0.58, 1.28, 0.76, 1.05))
    # Heavy thick fur pelt mantle over shoulders!
    fr.add_loft([
        (0, 1.10, -0.02, 0.295, 0.205, segs),
        (0, 1.26, -0.01, 0.310, 0.215, segs),
        (0, 1.36, 0.00, 0.195, 0.155, segs),
    ], mat_id=7, shade=(0.76, 0.68, 0.60), ao_y=(1.10, 1.36, 0.82, 1.10))
    fr.add_box((0, 0.84, 0), (0.41, 0.06, 0.32), mat_id=7)
    parts.append(fr)

    # 5. Steppe deel (Khaganate long wrapped coat with fur collar & wide sash)
    dl = PartBuilder("torso_deel")
    dl.add_loft([
        (0, 0.42, 0, 0.265, 0.200, segs),
        (0, 0.82, 0, 0.200, 0.155, segs),
        (0, 1.12, 0.01, 0.235, 0.162, segs),
        (0, 1.29, 0, 0.220, 0.145, segs),
    ], mat_id=1, ao_y=(0.42, 1.30, 0.74, 1.06))
    dl.add_loft([(0, 1.26, 0, 0.210, 0.160, segs), (0, 1.38, 0, 0.145, 0.130, segs)], mat_id=7, shade=(0.76, 0.68, 0.58))
    dl.add_box((0, 0.84, 0), (0.42, 0.075, 0.33), mat_id=3)  # bright steppe sash
    parts.append(dl)

    # 6. Desert kaftan / burnous (Sunward light flowing coat + linen trim)
    kf = PartBuilder("torso_kaftan")
    kf.add_loft([
        (0, 0.38, 0, 0.270, 0.210, segs),
        (0, 0.82, 0, 0.200, 0.155, segs),
        (0, 1.14, 0.01, 0.235, 0.162, segs),
        (0, 1.34, 0, 0.155, 0.125, segs),
    ], mat_id=3, ao_y=(0.38, 1.34, 0.76, 1.08))
    kf.add_box((0, 0.84, 0), (0.42, 0.07, 0.33), mat_id=1)  # faction sash
    kf.add_box((0, 0.96, 0.16), (0.14, 0.48, 0.025), mat_id=1)
    parts.append(kf)

    # 7. Skeleton ribcage + spine + pelvis (bleached bone, mat_id=9 which UnitMatTable sets to bone for skull face!)
    bn = PartBuilder("torso_bones")
    bn.add_cone((0, 0.70, -0.02), (0, 1.36, -0.02), 0.048, 0.044, segs=5, mat_id=9, shade=(0.82, 0.80, 0.75))  # spine
    bn.add_box((0, 0.72, 0.0), (0.30, 0.12, 0.18), mat_id=9, shade=(0.86, 0.84, 0.78))  # pelvis
    for ry in (0.94, 1.06, 1.18, 1.28):
        w = 0.36 if ry > 1.0 else 0.30
        bn.add_box((0, ry, 0.02), (w, 0.055, 0.20), mat_id=9, shade=(0.98, 0.96, 0.92))  # ribs
    parts.append(bn)

    # Thin skeletal bone limbs!
    for bname in ("uarm_bones", "farm_bones", "thigh_bones", "shin_bones"):
        bl = PartBuilder(bname)
        r = 0.044 if "arm" in bname else 0.052
        bl.add_loft([(0, 0.0, 0, r * 1.25, r * 1.25, 5), (0, 0.5, 0, r * 0.78, r * 0.78, 5), (0, 1.0, 0, r * 1.2, r * 1.2, 5)], mat_id=9, ao_y=(0.0, 1.0, 0.96, 0.82))
        parts.append(bl)

    return parts


def build_phase2_weapons_and_shields(PartBuilder):
    parts = []

    # Scimitar (curved eastern blade)
    sc = PartBuilder("weapon_scimitar")
    sc.add_cone((0, -0.16, 0), (0, 0.05, 0), 0.022, 0.024, segs=4, mat_id=7)
    sc.add_box((0, 0.06, 0), (0.20, 0.036, 0.04), mat_id=6)
    sc.add_loft([
        (0, 0.08, 0.00, 0.022, 0.052, 4),
        (0, 0.44, 0.04, 0.020, 0.062, 4),
        (0, 0.76, 0.14, 0.016, 0.058, 4),
        (0, 0.92, 0.24, 0.005, 0.012, 4),
    ], mat_id=4, smooth=False, shade=(1.08, 1.08, 1.12))
    parts.append(sc)

    # Falchion (heavy cleaver-tip blade)
    fl = PartBuilder("weapon_falchion")
    fl.add_cone((0, -0.16, 0), (0, 0.05, 0), 0.022, 0.024, segs=4, mat_id=7)
    fl.add_box((0, 0.06, 0), (0.20, 0.036, 0.04), mat_id=5)
    fl.add_box((0, 0.36, 0.02), (0.032, 0.58, 0.095), mat_id=4, shade=(1.04, 1.06, 1.10))
    fl.add_box((0, 0.70, 0.04), (0.028, 0.18, 0.135), mat_id=4, shade=(1.08, 1.10, 1.14))
    parts.append(fl)

    # Flamberge (tall wavy greatsword)
    fm = PartBuilder("weapon_flamberge")
    fm.add_cone((0, -0.34, 0), (0, 0.06, 0), 0.024, 0.026, segs=4, mat_id=7)
    fm.add_box((0, 0.07, 0), (0.36, 0.042, 0.045), mat_id=6)
    fm.add_loft([
        (0, 0.09, 0.0, 0.065, 0.024, 4),
        (0.025, 0.45, 0.0, 0.062, 0.022, 4),
        (-0.025, 0.85, 0.0, 0.055, 0.020, 4),
        (0.02, 1.18, 0.0, 0.042, 0.016, 4),
        (0, 1.42, 0.0, 0.006, 0.006, 4),
    ], mat_id=4, smooth=False, shade=(1.08, 1.08, 1.12))
    parts.append(fm)

    # Warhammer
    wh = PartBuilder("weapon_warhammer")
    wh.add_cone((0, -0.22, 0), (0, 0.78, 0), 0.024, 0.024, segs=5, mat_id=8)
    wh.add_box((0, 0.72, 0.08), (0.09, 0.11, 0.14), mat_id=4)
    wh.add_cone((0, 0.72, -0.02), (0, 0.72, -0.22), 0.045, 0.006, segs=4, mat_id=5)
    parts.append(wh)

    # Morningstar
    ms = PartBuilder("weapon_morningstar")
    ms.add_cone((0, -0.20, 0), (0, 0.66, 0), 0.022, 0.024, segs=5, mat_id=8)
    ms.add_sphere((0, 0.72, 0), (0.095, 0.095, 0.095), segs=5, rings=4, mat_id=5, smooth=False)
    for dx, dy, dz in [(0.14, 0, 0), (-0.14, 0, 0), (0, 0.14, 0), (0, 0, 0.14), (0, 0, -0.14)]:
        ms.add_cone((0, 0.72, 0), (dx, 0.72 + dy, dz), 0.025, 0.004, segs=3, mat_id=4)
    parts.append(ms)

    # Daneaxe (long two-handed northern battleaxe)
    da = PartBuilder("weapon_daneaxe")
    da.add_cone((0, -0.58, 0), (0, 1.38, 0), 0.026, 0.026, segs=5, mat_id=8)
    da.add_box((0, 1.24, 0.14), (0.042, 0.34, 0.24), mat_id=4, shade=(1.06, 1.08, 1.12))
    parts.append(da)

    # Glaive
    gl = PartBuilder("weapon_glaive")
    gl.add_cone((0, -0.68, 0), (0, 1.48, 0), 0.025, 0.025, segs=5, mat_id=8)
    gl.add_loft([(0, 1.46, 0, 0.022, 0.068, 4), (0, 1.88, 0.04, 0.018, 0.082, 4), (0, 2.15, 0.10, 0.005, 0.010, 4)], mat_id=4, smooth=False, shade=(1.08, 1.08, 1.12))
    parts.append(gl)

    # Billhook
    bh = PartBuilder("weapon_billhook")
    bh.add_cone((0, -0.68, 0), (0, 1.56, 0), 0.025, 0.025, segs=5, mat_id=8)
    bh.add_box((0, 1.72, 0.06), (0.032, 0.36, 0.14), mat_id=5)
    bh.add_box((0, 1.88, 0.16), (0.030, 0.10, 0.12), mat_id=4)
    parts.append(bh)

    # Club (troll / goblin knotted warclub)
    cl = PartBuilder("weapon_club")
    cl.add_loft([(0, -0.18, 0, 0.030, 0.030, 5), (0, 0.40, 0, 0.052, 0.052, 5), (0, 0.85, 0, 0.095, 0.095, 5), (0, 0.96, 0, 0.065, 0.065, 5)], mat_id=8, ao_y=(-0.18, 0.96, 0.75, 1.08))
    parts.append(cl)

    # Sling
    sl = PartBuilder("weapon_sling")
    sl.add_cone((0, 0.0, 0), (0, 0.56, 0.04), 0.014, 0.012, segs=4, mat_id=7)
    sl.add_sphere((0, 0.58, 0.04), (0.055, 0.055, 0.055), segs=5, rings=3, mat_id=5)
    parts.append(sl)

    # Pavise (tall crossbowman wall shield)
    pv = PartBuilder("shield_pavise")
    pv.add_box((0, -0.06, 0.05), (0.56, 1.02, 0.055), mat_id=1, ao_y=(-0.58, 0.46, 0.76, 1.08))
    pv.add_box((0, -0.06, 0.082), (0.12, 0.92, 0.018), mat_id=3)
    pv.add_box((0, 0.14, 0.082), (0.48, 0.12, 0.018), mat_id=3)
    parts.append(pv)

    # Tower shield
    tw = PartBuilder("shield_tower")
    tw.add_box((0, -0.02, 0.05), (0.52, 0.92, 0.06), mat_id=1, ao_y=(-0.48, 0.44, 0.78, 1.08))
    tw.add_box((0, -0.02, 0.085), (0.46, 0.08, 0.02), mat_id=6)
    tw.add_sphere((0, -0.02, 0.09), (0.09, 0.09, 0.04), segs=6, rings=3, mat_id=6)
    parts.append(tw)

    # Kalkan (domed conical eastern shield)
    kk = PartBuilder("shield_kalkan")
    kk.add_loft_z([(0, 0, 0.0, 0.31, 0.31, 8), (0, 0, 0.08, 0.18, 0.18, 8), (0, 0, 0.14, 0.04, 0.04, 8)], mat_id=1, ao_y=(-0.31, 0.31, 0.80, 1.10))
    kk.add_sphere((0, 0, 0.12), (0.09, 0.09, 0.055), segs=6, rings=3, mat_id=6)
    parts.append(kk)

    return parts


def build_phase2_mounts(PartBuilder):
    parts = []
    segs = 8

    # Scale-barded horse body/neck/head (golden-brass scale armor for Sunward/Khaganate heavy cav)
    hs = PartBuilder("horse_body_scale")
    hs.add_loft_z([
        (0, -0.04, -0.92, 0.24, 0.24, segs),
        (0, -0.08, -0.58, 0.37, 0.42, segs),
        (0, -0.08, 0.48, 0.38, 0.44, segs),
        (0, -0.02, 0.94, 0.17, 0.19, segs),
    ], mat_id=6, ao_y=(-0.50, 0.35, 0.78, 1.10))
    hs.add_box((0, 0.26, -0.02), (0.62, 0.12, 0.54), mat_id=1)
    hs.add_box((0, 0.32, -0.02), (0.48, 0.09, 0.42), mat_id=7)
    parts.append(hs)

    hns = PartBuilder("horse_neck_scale")
    hns.add_loft([(0, -0.10, 0, 0.24, 0.30, 7), (0, 0.48, 0.02, 0.20, 0.24, 7), (0, 1.06, 0, 0.14, 0.16, 7)], mat_id=6, ao_y=(0.0, 1.0, 0.82, 1.10))
    hns.add_loft([(0, 0.02, -0.26, 0.07, 0.09, 5), (0, 1.04, -0.16, 0.06, 0.07, 5)], mat_id=12)
    parts.append(hns)

    hhs = PartBuilder("horse_head_scale")
    hhs.add_loft_z([(0, 0.02, -0.10, 0.12, 0.13, 7), (0, 0.01, 0.10, 0.168, 0.195, 7), (0, 0.01, 0.42, 0.112, 0.128, 7), (0, -0.01, 0.66, 0.084, 0.094, 7)], mat_id=11)
    hhs.add_loft_z([(0, 0.08, 0.06, 0.148, 0.138, 6), (0, 0.06, 0.54, 0.095, 0.085, 6)], mat_id=6, shade=(1.06, 1.06, 1.08))
    for sx in (-1, 1):
        hhs.add_cone((sx * 0.105, 0.14, 0.0), (sx * 0.135, 0.34, 0.04), 0.052, 0.010, segs=4, mat_id=11)
    parts.append(hhs)

    # Fur-barded northern warhorse
    hf = PartBuilder("horse_body_fur")
    hf.add_loft_z([
        (0, 0.04, -0.94, 0.15, 0.15, segs),
        (0, 0.08, -0.72, 0.33, 0.32, segs),
        (0, 0.03, -0.05, 0.31, 0.31, segs),
        (0, 0.07, 0.52, 0.35, 0.36, segs),
        (0, 0.02, 0.92, 0.16, 0.17, segs),
    ], mat_id=11, ao_y=(-0.35, 0.42, 0.76, 1.08))
    hf.add_box((0, 0.14, 0.02), (0.72, 0.36, 1.25), mat_id=7, shade=(0.74, 0.66, 0.58))  # wolf-pelt drape
    hf.add_box((0, 0.32, -0.02), (0.48, 0.10, 0.42), mat_id=1)
    parts.append(hf)

    # Camel (sculpted desert dromedary with prominent hump, low-dipping U-neck & distinct camel head!)
    cb = PartBuilder("camel_body")
    cb.add_loft_z([
        (0, 0.04, -0.88, 0.14, 0.14, segs),
        (0, 0.08, -0.62, 0.29, 0.30, segs),
        (0, 0.06, 0.00, 0.32, 0.34, segs),
        (0, 0.06, 0.56, 0.30, 0.32, segs),
        (0, 0.02, 0.86, 0.14, 0.15, segs),
    ], mat_id=11, ao_y=(-0.32, 0.45, 0.78, 1.08))
    # Prominent camel hump + ornate desert saddlecloth!
    cb.add_sphere((0, 0.44, -0.04), (0.28, 0.32, 0.42), segs=7, rings=5, mat_id=11, shade=(1.04, 1.02, 0.98))
    cb.add_box((0, 0.32, -0.04), (0.64, 0.24, 0.62), mat_id=1)
    cb.add_box((0, 0.20, -0.04), (0.66, 0.06, 0.64), mat_id=3)
    parts.append(cb)

    cn = PartBuilder("camel_neck")
    cn.add_loft([(0, -0.08, 0, 0.18, 0.22, 6), (0, 0.48, 0.10, 0.14, 0.17, 6), (0, 1.06, 0, 0.11, 0.13, 6)], mat_id=11, ao_y=(0.0, 1.0, 0.84, 1.08))
    parts.append(cn)

    ch = PartBuilder("camel_head")
    ch.add_loft_z([(0, 0.02, -0.08, 0.11, 0.12, 6), (0, 0.02, 0.18, 0.135, 0.145, 6), (0, 0.00, 0.54, 0.105, 0.115, 6)], mat_id=11)
    for sx in (-1, 1):
        ch.add_sphere((sx * 0.11, 0.14, 0.04), (0.036, 0.045, 0.032), segs=4, rings=3, mat_id=11)
        ch.add_box((sx * 0.125, 0.055, 0.22), (0.036, 0.042, 0.042), mat_id=0, shade=(0.10, 0.08, 0.08))
    ch.add_box((0, 0.01, 0.42), (0.22, 0.24, 0.04), mat_id=1)  # colorful desert bridle
    parts.append(ch)

    return parts


def build_phase2_race_heads(PartBuilder):
    """4 non-human humanoid heads: goblin, troll, skull (skeleton), ghoul."""
    parts = []

    # 1. Goblin: fierce green head, wide pointed ears, glowing yellow eyes, toothy grin!
    gb = PartBuilder("head_goblin")
    gb.add_sphere((0, 0.01, 0.0), (0.145, 0.145, 0.150), segs=7, rings=5, mat_id=9, ao_y=(-0.14, 0.16, 0.80, 1.08))
    gb.add_cone((0, 0.00, 0.12), (0, -0.03, 0.25), 0.055, 0.015, segs=4, mat_id=9, shade=(1.06, 1.08, 0.96))  # hooked snout
    for sx in (-1, 1):
        gb.add_cone((sx * 0.13, 0.02, 0.0), (sx * 0.34, 0.08, -0.04), 0.058, 0.010, segs=4, mat_id=9)  # long goblin ears!
        gb.add_box((sx * 0.058, 0.04, 0.138), (0.042, 0.038, 0.026), mat_id=0, shade=(0.98, 0.88, 0.18))  # bright yellow eyes!
    gb.add_box((0, -0.065, 0.135), (0.14, 0.034, 0.026), mat_id=0, shade=(0.92, 0.90, 0.80))  # jagged teeth
    parts.append(gb)

    # 2. Troll: heavy brutish brow, jutting ivory tusks, small fiery eyes!
    tr = PartBuilder("head_troll")
    tr.add_sphere((0, 0.02, 0.0), (0.175, 0.185, 0.175), segs=7, rings=5, mat_id=9, ao_y=(-0.16, 0.20, 0.78, 1.06))
    tr.add_box((0, 0.08, 0.13), (0.26, 0.065, 0.08), mat_id=9, shade=(0.82, 0.84, 0.80))  # heavy brow ridge
    for sx in (-1, 1):
        tr.add_box((sx * 0.068, 0.035, 0.155), (0.035, 0.028, 0.026), mat_id=0, shade=(0.95, 0.55, 0.12))  # amber eyes
        tr.add_cone((sx * 0.085, -0.09, 0.13), (sx * 0.10, 0.02, 0.21), 0.030, 0.008, segs=4, mat_id=0, shade=(0.92, 0.88, 0.76))  # tusks!
    parts.append(tr)

    # 3. Skeleton skull: bleached bone cranium, dark eye sockets with eerie green flame pupils, skeletal jaw!
    sk = PartBuilder("head_skull")
    sk.add_sphere((0, 0.04, -0.01), (0.142, 0.148, 0.148), segs=7, rings=5, mat_id=9, ao_y=(-0.10, 0.18, 0.82, 1.08))
    sk.add_box((0, -0.085, 0.055), (0.15, 0.095, 0.12), mat_id=9, shade=(0.88, 0.86, 0.80))  # jaw & teeth
    for sx in (-1, 1):
        sk.add_box((sx * 0.052, 0.03, 0.135), (0.048, 0.048, 0.028), mat_id=0, shade=(0.06, 0.06, 0.08))  # dark socket
        sk.add_box((sx * 0.052, 0.03, 0.148), (0.022, 0.022, 0.016), mat_id=0, shade=(0.30, 0.98, 0.58))  # glowing green eye!
    sk.add_box((0, -0.02, 0.142), (0.028, 0.036, 0.020), mat_id=0, shade=(0.08, 0.08, 0.10))  # nasal cavity
    parts.append(sk)

    # 4. Ghoul: gaunt sunken corpse face with hollow dark cheeks & pale sickly eyes
    gh = PartBuilder("head_ghoul")
    gh.add_sphere((0, 0.01, 0.0), (0.135, 0.165, 0.145), segs=7, rings=5, mat_id=9, ao_y=(-0.15, 0.17, 0.76, 1.04))
    for sx in (-1, 1):
        gh.add_box((sx * 0.050, 0.032, 0.135), (0.042, 0.040, 0.026), mat_id=0, shade=(0.12, 0.10, 0.12))
        gh.add_box((sx * 0.050, 0.032, 0.146), (0.020, 0.020, 0.015), mat_id=0, shade=(0.84, 0.94, 0.50))
    gh.add_box((0, -0.065, 0.132), (0.11, 0.038, 0.024), mat_id=0, shade=(0.16, 0.08, 0.10))  # gaping dark maw
    parts.append(gh)

    return parts


def build_phase2_beasts(PartBuilder):
    """4 complete 3D beast mesh kits: Wolf/Hound/Warg, Cave Bear, Giant Spider, Wyvern!"""
    parts = []

    # ---- 1. WOLF / WAR HOUND / DIRE WOLF / WARG ----
    wb = PartBuilder("wolf_body")
    wb.add_loft_z([
        (0, 0.02, -0.56, 0.12, 0.12, 7),
        (0, 0.04, -0.32, 0.19, 0.19, 7),
        (0, 0.02, 0.02, 0.17, 0.18, 7),
        (0, 0.05, 0.34, 0.22, 0.23, 7),  # muscular ruff / chest
        (0, 0.04, 0.56, 0.14, 0.15, 7),
    ], mat_id=11, ao_y=(-0.22, 0.28, 0.76, 1.08))
    parts.append(wb)

    wh = PartBuilder("wolf_head")
    wh.add_loft_z([
        (0, 0.02, -0.06, 0.12, 0.12, 6),
        (0, 0.03, 0.12, 0.15, 0.15, 6),
        (0, 0.00, 0.34, 0.095, 0.095, 6),
        (0, -0.01, 0.50, 0.065, 0.062, 6),
    ], mat_id=11, ao_y=(-0.14, 0.18, 0.80, 1.08))
    wh.add_box((0, 0.01, 0.50), (0.07, 0.06, 0.05), mat_id=0, shade=(0.10, 0.09, 0.09))  # black nose
    for sx in (-1, 1):
        wh.add_cone((sx * 0.095, 0.11, 0.06), (sx * 0.12, 0.26, 0.09), 0.052, 0.010, segs=4, mat_id=11)  # pointed ears
        wh.add_box((sx * 0.11, 0.06, 0.22), (0.034, 0.034, 0.034), mat_id=0, shade=(0.98, 0.82, 0.20))  # fierce golden eyes
    parts.append(wh)

    wl = PartBuilder("wolf_leg")
    wl.add_loft([(0, -0.06, 0, 0.08, 0.085, 5), (0, 0.50, 0, 0.06, 0.065, 5), (0, 1.02, 0.03, 0.068, 0.082, 5)], mat_id=11, cap_bot=False, ao_y=(0.0, 1.0, 0.96, 0.78))
    parts.append(wl)

    wt = PartBuilder("wolf_tail")
    wt.add_loft([(0, -0.04, 0, 0.06, 0.06, 5), (0, 0.55, 0, 0.095, 0.095, 5), (0, 1.04, 0, 0.022, 0.022, 5)], mat_id=12, ao_y=(0.0, 1.0, 1.0, 0.76))
    parts.append(wt)

    # ---- 2. CAVE BEAR ----
    bb = PartBuilder("bear_body")
    bb.add_loft_z([
        (0, 0.00, -0.66, 0.20, 0.22, 7),
        (0, 0.04, -0.35, 0.34, 0.36, 7),
        (0, 0.12, 0.22, 0.38, 0.42, 7),  # massive humped bear shoulders!
        (0, 0.06, 0.62, 0.26, 0.28, 7),
    ], mat_id=11, ao_y=(-0.36, 0.52, 0.74, 1.08))
    parts.append(bb)

    bh = PartBuilder("bear_head")
    bh.add_sphere((0, 0.02, 0.14), (0.24, 0.23, 0.24), segs=7, rings=5, mat_id=11, ao_y=(-0.20, 0.26, 0.80, 1.08))
    bh.add_loft_z([(0, -0.02, 0.30, 0.14, 0.13, 6), (0, -0.04, 0.54, 0.10, 0.095, 6)], mat_id=11, shade=(1.38, 1.32, 1.24))  # pale snout
    bh.add_box((0, -0.01, 0.55), (0.09, 0.075, 0.05), mat_id=0, shade=(0.10, 0.08, 0.08))  # dark nose
    for sx in (-1, 1):
        bh.add_sphere((sx * 0.17, 0.21, 0.08), (0.075, 0.075, 0.055), segs=5, rings=3, mat_id=11)  # round bear ears
        bh.add_box((sx * 0.14, 0.08, 0.32), (0.042, 0.042, 0.042), mat_id=0, shade=(0.10, 0.08, 0.08))
    parts.append(bh)

    bl = PartBuilder("bear_leg")
    bl.add_loft([(0, -0.08, 0, 0.15, 0.16, 6), (0, 0.65, 0, 0.125, 0.135, 6), (0, 1.02, 0.04, 0.14, 0.16, 6)], mat_id=11, cap_bot=False, ao_y=(0.0, 1.0, 0.96, 0.76))
    bl.add_box((0, 0.98, 0.16), (0.14, 0.07, 0.10), mat_id=0, shade=(0.86, 0.82, 0.74))  # claws!
    parts.append(bl)

    # ---- 3. GIANT SPIDER / BROOD MOTHER / SPIDERLING ----
    sa = PartBuilder("spider_abdomen")
    sa.add_sphere((0, 0.04, 0.0), (0.34, 0.30, 0.44), segs=8, rings=5, mat_id=11, ao_y=(-0.26, 0.34, 0.72, 1.06))
    # Crimson widow chevrons along top of abdomen!
    for cz in (-0.18, 0.0, 0.18):
        sa.add_box((0, 0.31, cz), (0.24, 0.055, 0.085), mat_id=0, shade=(0.76, 0.18, 0.14))
    parts.append(sa)

    sh = PartBuilder("spider_head")
    sh.add_sphere((0, 0.02, 0.08), (0.22, 0.18, 0.24), segs=7, rings=4, mat_id=11, ao_y=(-0.16, 0.20, 0.76, 1.06))
    # Glowing crimson eye cluster + white fangs!
    for ex, ey, ez in [(-0.08, 0.08, 0.28), (0.08, 0.08, 0.28), (-0.03, 0.11, 0.30), (0.03, 0.11, 0.30)]:
        sh.add_box((ex, ey, ez), (0.045, 0.045, 0.035), mat_id=0, shade=(1.0, 0.24, 0.18))
    for sx in (-1, 1):
        sh.add_cone((sx * 0.075, -0.02, 0.28), (sx * 0.055, -0.18, 0.34), 0.042, 0.008, segs=4, mat_id=0, shade=(0.90, 0.86, 0.74))
    parts.append(sh)

    sl = PartBuilder("spider_leg")
    sl.add_loft([(0, -0.04, 0, 0.052, 0.052, 5), (0, 0.50, 0, 0.042, 0.042, 5), (0, 1.02, 0, 0.022, 0.022, 5)], mat_id=11, cap_bot=False, ao_y=(0.0, 1.0, 1.0, 0.76))
    parts.append(sl)

    # ---- 4. WYVERN (WINGED DRAKE ALOFT!) ----
    yb = PartBuilder("wyvern_body")
    yb.add_loft_z([
        (0, 0.00, -0.62, 0.16, 0.16, 7),
        (0, 0.04, -0.18, 0.26, 0.24, 7),
        (0, 0.06, 0.32, 0.28, 0.26, 7),
        (0, 0.04, 0.58, 0.18, 0.17, 7),
    ], mat_id=11, ao_y=(-0.22, 0.32, 0.74, 1.10))
    # Pale armored belly keel + dorsal spine ridges
    yb.add_box((0, -0.20, 0.05), (0.28, 0.10, 0.92), mat_id=12)
    for sz in (-0.30, -0.05, 0.20):
        yb.add_cone((0, 0.26, sz), (0, 0.42, sz - 0.06), 0.055, 0.008, segs=4, mat_id=13)
    parts.append(yb)

    yn = PartBuilder("wyvern_neck")
    yn.add_loft([(0, -0.08, 0, 0.16, 0.18, 6), (0, 0.50, 0, 0.13, 0.15, 6), (0, 1.06, 0, 0.11, 0.12, 6)], mat_id=11, cap_bot=False, cap_top=False, ao_y=(0.0, 1.0, 0.84, 1.08))
    parts.append(yn)

    yh = PartBuilder("wyvern_head")
    yh.add_loft_z([(0, 0.02, -0.08, 0.13, 0.13, 6), (0, 0.02, 0.16, 0.16, 0.15, 6), (0, -0.02, 0.54, 0.09, 0.09, 6)], mat_id=11, ao_y=(-0.12, 0.18, 0.80, 1.10))
    for sx in (-1, 1):
        yh.add_cone((sx * 0.10, 0.10, 0.04), (sx * 0.18, 0.28, -0.22), 0.048, 0.008, segs=4, mat_id=13)  # swept drake horns!
        yh.add_box((sx * 0.13, 0.06, 0.22), (0.042, 0.040, 0.042), mat_id=0, shade=(1.0, 0.78, 0.15))  # fierce drake eyes
    parts.append(yh)

    # Wyvern wing: sculpted swept bat/dragon wing from root (x=0) -> sharp wingtip (x=1.0), leading edge +Z=0, trailing edge -Z!
    yw = PartBuilder("wyvern_wing")
    # Leading wing arm bone (shoulder -> elbow bend -> sharp wingtip)
    yw.add_cone((0.0, 0.02, 0.00), (0.48, 0.06, 0.10), 0.055, 0.036, segs=4, mat_id=11)
    yw.add_cone((0.48, 0.06, 0.10), (1.02, 0.02, -0.14), 0.036, 0.012, segs=4, mat_id=11)
    # Sculpted swept tapered membrane loft (inner panel + outer tapered tip!)
    yw.add_loft([
        [Vector((0.00, 0.015, 0.00)), Vector((0.00, 0.015, -0.52)), Vector((0.00, -0.015, -0.52)), Vector((0.00, -0.015, 0.00))],
        [Vector((0.48, 0.050, 0.09)), Vector((0.44, 0.015, -0.62)), Vector((0.44, -0.015, -0.62)), Vector((0.48, 0.020, 0.09))],
        [Vector((0.82, 0.035, -0.02)), Vector((0.76, 0.010, -0.44)), Vector((0.76, -0.010, -0.44)), Vector((0.82, 0.010, -0.02))],
        [Vector((1.02, 0.020, -0.14)), Vector((0.96, 0.005, -0.22)), Vector((0.96, -0.005, -0.22)), Vector((1.02, 0.005, -0.14))],
    ], mat_id=12, smooth=False, shade=(0.94, 0.80, 0.74))
    # Wing finger ribs
    yw.add_cone((0.48, 0.05, 0.09), (0.44, 0.01, -0.62), 0.022, 0.008, segs=3, mat_id=13)
    yw.add_cone((0.48, 0.05, 0.09), (0.76, 0.01, -0.44), 0.020, 0.008, segs=3, mat_id=13)
    parts.append(yw)

    yl = PartBuilder("wyvern_leg")
    yl.add_loft([(0, -0.06, 0, 0.095, 0.10, 5), (0, 0.65, 0, 0.065, 0.07, 5), (0, 1.02, 0.05, 0.09, 0.12, 5)], mat_id=11, cap_bot=False)
    parts.append(yl)

    yt = PartBuilder("wyvern_tail")
    yt.add_loft([(0, -0.06, 0, 0.11, 0.11, 5), (0, 1.06, 0, 0.075, 0.075, 5)], mat_id=11, cap_bot=False, cap_top=False)
    parts.append(yt)

    ys = PartBuilder("wyvern_sting")
    ys.add_cone((0, -0.05, 0), (0, 1.05, 0), 0.095, 0.010, segs=5, mat_id=13, shade=(0.95, 0.90, 0.82))
    parts.append(ys)

    return parts


def build_all(PartBuilder):
    parts = []
    parts.extend(build_phase2_helms(PartBuilder))
    parts.extend(build_phase2_bodies_and_limbs(PartBuilder))
    parts.extend(build_phase2_weapons_and_shields(PartBuilder))
    parts.extend(build_phase2_mounts(PartBuilder))
    parts.extend(build_phase2_race_heads(PartBuilder))
    parts.extend(build_phase2_beasts(PartBuilder))
    return parts
