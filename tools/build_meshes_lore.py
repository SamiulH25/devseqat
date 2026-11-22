"""
Lore-grounded Roven 3D modular equipment (docs/LORE.md), imported by tools/build_meshes_3d.py:
  - 8 elite & Knight Order sculpted 3D helms
  - 11 sculpted 3D modular chest cuirasses/coats (8 elite/order + 3 mid-tier regional)
  - 4 matching 3D shoulder mantles/pauldrons + 3 sculpted 3D fauld/scapular skirts
  - 3 iconic 3D shields (Lantern Pavise, Tarn Ash-Iron Kite, Qadiri Sun-Mirror Kalkan)
"""
import math
from mathutils import Vector


def build_lore_all(PartBuilder):
    parts = []
    segs = 8

    def add_mail_coif(p, mat_id=5, shade=(0.96, 0.96, 1.0)):
        p.add_loft([
            (0, -0.17, -0.030, 0.176, 0.150, 8),
            (0, -0.06, -0.042, 0.160, 0.132, 8),
            (0, 0.06, -0.035, 0.166, 0.138, 8),
        ], mat_id=mat_id, shade=shade, cap_bot=False, cap_top=False, ao_y=(-0.17, 0.08, 0.78, 1.02))

    def build_cuirass_base(p, mat_id=4, hi=(1.06, 1.08, 1.12), lo=(0.90, 0.93, 0.98), seam_shade=(0.56, 0.60, 0.66)):
        rings_up = []
        for y, rx, rz, zoff in [
            (0.96, 0.218, 0.156, 0.012),
            (1.12, 0.248, 0.172, 0.016),
            (1.28, 0.236, 0.150, 0.000),
            (1.37, 0.122, 0.112, 0.000),
        ]:
            ring = []
            for i in range(segs):
                a = 2.0 * math.pi * i / segs
                sa = math.sin(a)
                x = math.cos(a) * rx
                z = sa * rz + zoff + (0.028 * (sa ** 2) if sa > 0 and 0.95 < y < 1.26 else 0.0)
                ring.append(Vector((x, y, z)))
            rings_up.append(ring)
        p.add_loft(rings_up, mat_id=mat_id, shade=hi, smooth=False, cap_bot=False, ao_y=(0.96, 1.37, 0.86, 1.10))
        p.add_loft([(0, 0.94, 0.010, 0.220, 0.160, segs), (0, 0.975, 0.012, 0.222, 0.162, segs)],
                   mat_id=5, shade=seam_shade, cap_bot=False, cap_top=False)
        rings_lo = []
        for y, rx, rz, zoff in [(0.80, 0.192, 0.146, 0.008), (0.95, 0.224, 0.162, 0.014)]:
            ring = []
            for i in range(segs):
                a = 2.0 * math.pi * i / segs
                sa = math.sin(a)
                x = math.cos(a) * rx
                z = sa * rz + zoff + (0.022 * (sa ** 2) if sa > 0 else 0.0)
                yy = y + (0.055 * (sa ** 2) if sa > 0 and y > 0.90 else 0.0)
                ring.append(Vector((x, yy, z)))
            rings_lo.append(ring)
        p.add_loft(rings_lo, mat_id=mat_id, shade=lo, smooth=False, cap_bot=False, ao_y=(0.80, 1.00, 0.82, 1.04))

    # ===================== 8 ELITE & KNIGHT ORDER 3D HELMS =====================

    # 1. Brevon / Silver Lily: silvered closed armet with raised fleur/lily crest & flowing rear mantling
    hl = PartBuilder("helm_lily_armet")
    hl.add_loft([
        (0, -0.15, -0.01, 0.152, 0.158, segs),
        (0, 0.02, 0.01, 0.184, 0.196, segs),
        (0, 0.14, 0.0, 0.182, 0.192, segs),
        (0, 0.245, 0.0, 0.120, 0.128, segs),
        (0, 0.282, 0.0, 0.036, 0.040, segs),
    ], mat_id=4, shade=(1.14, 1.16, 1.22), ao_y=(-0.15, 0.28, 0.82, 1.16))
    # Pointed visor prow + recessed dark ocular slit
    hl.add_cone((0, 0.04, 0.16), (0, 0.04, 0.245), 0.115, 0.022, segs=5, mat_id=4, shade=(1.16, 1.18, 1.24))
    hl.add_box((0, 0.088, 0.198), (0.21, 0.030, 0.028), mat_id=0, shade=(0.05, 0.05, 0.07))
    # Sculpted silver fleur-de-lis crest along the crown
    hl.add_box((0, 0.30, 0.01), (0.036, 0.14, 0.24), mat_id=4, shade=(1.20, 1.22, 1.28))
    hl.add_cone((0, 0.34, 0.04), (0, 0.44, 0.06), 0.034, 0.008, segs=4, mat_id=6)
    for sx in (-1, 1):
        hl.add_cone((sx * 0.02, 0.33, 0.04), (sx * 0.085, 0.40, 0.05), 0.022, 0.006, segs=4, mat_id=4, shade=(1.18, 1.20, 1.26))
    # Rear faction mantling tail
    hl.add_box((0, -0.04, -0.20), (0.14, 0.22, 0.045), mat_id=1, rot_euler=(0.35, 0, 0))
    parts.append(hl)

    # 2. Vespra / Red Rose: darkened gothic sallet with swept lobster tail, gilded bevor & high feather crest
    hr = PartBuilder("helm_rose_sallet")
    rings = []
    for y, rx, rz, zoff in [
        (-0.025, 0.186, 0.192, -0.055),
        (0.035, 0.194, 0.202, -0.022),
        (0.095, 0.192, 0.204, -0.008),
        (0.185, 0.174, 0.184, -0.004),
        (0.248, 0.112, 0.122, 0.000),
        (0.278, 0.032, 0.040, 0.000),
    ]:
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            sa = math.sin(a)
            x = math.cos(a) * rx
            z = sa * rz + zoff
            yy = y
            if sa < -0.20 and y < 0.10:
                z -= (0.10 - y) * 1.55 * (-sa)
            if sa > 0.35 and y < 0.04:
                yy += (0.04 - y) * 0.90 * sa
            if sa > 0.50 and 0.02 < y < 0.11:
                z += 0.020 * sa
            ring.append(Vector((x, yy, z)))
        rings.append(ring)
    hr.add_loft(rings, mat_id=4, shade=(0.68, 0.70, 0.76), ao_y=(-0.02, 0.28, 0.76, 1.10))
    # Dark steel bevor chin guard + brass brow trim & rose stud
    hr.add_loft([(0, -0.14, 0.02, 0.155, 0.155, segs), (0, 0.01, 0.025, 0.178, 0.182, segs)],
                mat_id=4, shade=(0.64, 0.66, 0.72), cap_bot=False)
    hr.add_box((0, 0.055, 0.198), (0.21, 0.028, 0.026), mat_id=0, shade=(0.05, 0.05, 0.07))
    hr.add_box((0, 0.088, 0.202), (0.23, 0.022, 0.022), mat_id=6)
    hr.add_box((0, -0.05, 0.192), (0.046, 0.046, 0.022), mat_id=6)  # rose stud
    # Sweeping team-coloured duellist feather crest
    hr.add_cone((0, 0.26, 0.02), (0, 0.44, -0.08), 0.062, 0.032, segs=5, mat_id=1)
    hr.add_cone((0, 0.42, -0.07), (0, 0.36, -0.26), 0.052, 0.014, segs=5, mat_id=1)
    parts.append(hr)

    # 3. Tarn / Knights of Tarn: austere ash-grey monastic cowl framing stark unpainted grey-iron face mask (zero gold!)
    ht = PartBuilder("helm_tarn_cowl")
    ash_cloth = (0.48, 0.50, 0.53)
    ash_iron = (0.56, 0.58, 0.62)
    add_mail_coif(ht, mat_id=0, shade=ash_cloth)
    # Heavy draped monastic hood/cowl
    ht.add_sphere((0, 0.07, -0.03), (0.208, 0.206, 0.196), segs=8, rings=5, mat_id=0, shade=ash_cloth, ao_y=(-0.12, 0.28, 0.78, 1.06))
    ht.add_cone((0, 0.12, -0.14), (0, 0.02, -0.32), 0.115, 0.032, segs=5, mat_id=0, shade=(0.42, 0.44, 0.47))
    # Stark hammered highland-iron face mask recessed inside the cowl
    ht.add_loft([
        (0, -0.11, 0.045, 0.132, 0.138, segs),
        (0, 0.04, 0.052, 0.148, 0.155, segs),
        (0, 0.16, 0.040, 0.132, 0.138, segs),
    ], mat_id=5, shade=ash_iron, smooth=False)
    for sx in (-1, 1):
        ht.add_box((sx * 0.052, 0.055, 0.198), (0.054, 0.026, 0.022), mat_id=0, shade=(0.05, 0.05, 0.06))
    ht.add_box((0, 0.04, 0.200), (0.022, 0.16, 0.022), mat_id=5, shade=(0.66, 0.68, 0.72))  # iron nasal ridge
    parts.append(ht)

    # 4. The Hollow Crown: gaunt blackened-iron greathelm ringed with jagged blackened crown & glowing ember eyes
    hc = PartBuilder("helm_hollow_crown")
    char_s = (0.24, 0.22, 0.26)
    rings = []
    for y, rx, rz, prow in [
        (-0.155, 0.168, 0.174, 0.020),
        (0.040, 0.178, 0.184, 0.028),
        (0.225, 0.166, 0.172, 0.016),
        (0.272, 0.130, 0.136, 0.006),
    ]:
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            sa = math.sin(a)
            x = math.cos(a) * rx
            z = sa * rz + (prow * (sa ** 2) if sa > 0 else 0.0)
            ring.append(Vector((x, y, z)))
        rings.append(ring)
    hc.add_loft(rings, mat_id=4, shade=char_s, smooth=False, ao_y=(-0.155, 0.275, 0.72, 1.05))
    # Jagged blackened iron crown circlet & 7 spikes
    hc.add_loft([(0, 0.21, 0, 0.176, 0.182, segs), (0, 0.26, 0, 0.184, 0.190, segs)], mat_id=5, shade=(0.30, 0.28, 0.34))
    for k in range(7):
        ang = 2.0 * math.pi * k / 7.0
        h = 0.44 if k == 0 else (0.39 if k in (1, 6) else 0.36)
        hc.add_cone((math.sin(ang) * 0.162, 0.25, math.cos(ang) * 0.162),
                    (math.sin(ang) * 0.185, h, math.cos(ang) * 0.185), 0.028, 0.005, segs=4, mat_id=5, shade=(0.34, 0.32, 0.38))
    # Dark visor slit with faint glowing ember eyes!
    hc.add_box((0, 0.062, 0.198), (0.245, 0.038, 0.030), mat_id=0, shade=(0.04, 0.03, 0.05))
    for sx in (-1, 1):
        hc.add_box((sx * 0.058, 0.062, 0.212), (0.034, 0.018, 0.014), mat_id=0, shade=(0.98, 0.42, 0.12))
    parts.append(hc)

    # 5. Skar / The Wolfsworn: riveted spectacle helm with sculpted iron wolf-muzzle crest & thick grey wolf-fur aventail
    hw = PartBuilder("helm_wolfsworn")
    wolf_fur = (0.58, 0.60, 0.64)
    add_mail_coif(hw, mat_id=7, shade=wolf_fur)
    hw.add_loft([
        (0, 0.05, 0.0, 0.184, 0.190, segs),
        (0, 0.16, 0.0, 0.162, 0.168, segs),
        (0, 0.265, 0.0, 0.068, 0.072, segs),
    ], mat_id=5, shade=(0.76, 0.78, 0.84), ao_y=(0.05, 0.27, 0.78, 1.08))
    # Silvered spectacle ocular brow & nasal guard
    hw.add_box((0, 0.055, 0.186), (0.23, 0.032, 0.026), mat_id=4, shade=(1.12, 1.14, 1.20))
    hw.add_box((0, -0.005, 0.186), (0.038, 0.12, 0.026), mat_id=4, shade=(1.12, 1.14, 1.20))
    # Sculpted wolf-head crest & pointed wolf ears crowning the helm
    hw.add_sphere((0, 0.24, -0.01), (0.145, 0.115, 0.165), segs=6, rings=4, mat_id=7, shade=wolf_fur)
    hw.add_box((0, 0.22, 0.165), (0.11, 0.082, 0.155), mat_id=5, shade=(0.82, 0.85, 0.90))  # iron wolf muzzle
    for sx in (-1, 1):
        hw.add_cone((sx * 0.095, 0.29, -0.02), (sx * 0.115, 0.41, 0.01), 0.042, 0.008, segs=4, mat_id=7, shade=wolf_fur)
    parts.append(hw)

    # 6. Qadir / Burning Falcon: fluted bronze-and-steel conical kulah with silk turban wrap & hawk-beak nasal
    hf = PartBuilder("helm_falcon_kulah")
    add_mail_coif(hf, mat_id=5, shade=(0.90, 0.90, 0.94))
    # Tall fluted bronze conical bowl
    hf.add_loft([
        (0, 0.11, 0.0, 0.178, 0.182, segs),
        (0, 0.23, 0.0, 0.132, 0.136, segs),
        (0, 0.34, 0.0, 0.038, 0.038, segs),
    ], mat_id=6, smooth=False, ao_y=(0.11, 0.35, 0.82, 1.14))
    hf.add_cone((0, 0.33, 0), (0, 0.44, 0), 0.036, 0.006, segs=5, mat_id=6)  # tall finial spike
    # Wrapped team-coloured silk turban band around the brow
    hf.add_loft([
        (0, 0.03, 0.0, 0.208, 0.214, segs),
        (0, 0.085, 0.0, 0.222, 0.228, segs),
        (0, 0.138, 0.0, 0.190, 0.194, segs),
    ], mat_id=1, ao_y=(0.03, 0.14, 0.82, 1.08))
    # Hawk-beak bronze nasal guard
    hf.add_box((0, -0.005, 0.195), (0.034, 0.15, 0.032), mat_id=6)
    hf.add_cone((0, 0.07, 0.20), (0, 0.11, 0.255), 0.028, 0.006, segs=4, mat_id=6)
    parts.append(hf)

    # 7. Naryn / Eternal Sky: domed steppe helm with hammered-steel war-mask, lamellar cheeks & tall horsehair tug plume
    hs = PartBuilder("helm_sky_mask")
    hs.add_loft([
        (0, 0.06, 0.0, 0.186, 0.192, segs),
        (0, 0.18, 0.0, 0.158, 0.164, segs),
        (0, 0.285, 0.0, 0.048, 0.048, segs),
    ], mat_id=4, shade=(0.94, 0.96, 1.02), ao_y=(0.06, 0.29, 0.80, 1.12))
    # Dark lacquered lamellar cheek & neck flaps
    hs.add_loft([(0, -0.15, -0.02, 0.192, 0.168, segs), (0, 0.06, -0.01, 0.186, 0.166, segs)],
                mat_id=7, shade=(0.50, 0.42, 0.40), cap_bot=False, cap_top=False)
    # Sculpted hammered-steel war-mask over face with eye slits & brass moustache ridge
    hs.add_loft([
        (0, -0.12, 0.04, 0.140, 0.145, segs),
        (0, 0.02, 0.05, 0.156, 0.162, segs),
        (0, 0.10, 0.04, 0.150, 0.155, segs),
    ], mat_id=4, shade=(1.10, 1.12, 1.18), smooth=False)
    for sx in (-1, 1):
        hs.add_box((sx * 0.052, 0.042, 0.205), (0.052, 0.024, 0.022), mat_id=0, shade=(0.05, 0.05, 0.06))
    hs.add_box((0, -0.035, 0.205), (0.125, 0.024, 0.022), mat_id=6)  # brass moustache ridge
    # Tall streaming horsehair tug plume from brass finial
    hs.add_cone((0, 0.28, 0), (0, 0.40, 0), 0.032, 0.016, segs=5, mat_id=6)
    hs.add_cone((0, 0.40, 0), (0, 0.34, -0.24), 0.058, 0.018, segs=5, mat_id=1)
    parts.append(hs)

    # 8. Brinemere / Lantern Company: swept combed arsenal burgonet with brass-trimmed peak & hinged cheek-pieces
    hb = PartBuilder("helm_lantern_burgonet")
    hb.add_loft([
        (0, 0.05, 0.0, 0.188, 0.196, segs),
        (0, 0.17, 0.0, 0.168, 0.176, segs),
        (0, 0.265, 0.0, 0.056, 0.064, segs),
    ], mat_id=4, shade=(1.04, 1.06, 1.10), ao_y=(0.05, 0.27, 0.80, 1.14))
    # Tall swept arsenal comb with gilded crest edge
    hb.add_box((0, 0.25, -0.01), (0.040, 0.17, 0.29), mat_id=4, shade=(1.12, 1.14, 1.18))
    hb.add_box((0, 0.335, -0.01), (0.044, 0.026, 0.27), mat_id=6)
    # Forward pointed brow peak & brass rim
    hb.add_box((0, 0.068, 0.185), (0.24, 0.028, 0.11), mat_id=4, rot_euler=(0.35, 0, 0))
    hb.add_box((0, 0.072, 0.215), (0.245, 0.020, 0.026), mat_id=6)
    # Hinged brass-rimmed cheek-pieces
    for sx in (-1, 1):
        hb.add_box((sx * 0.155, -0.035, 0.02), (0.038, 0.16, 0.115), mat_id=4, rot_euler=(0, sx * 0.32, -sx * 0.10))
        hb.add_box((sx * 0.165, -0.035, 0.065), (0.018, 0.15, 0.022), mat_id=6)
    parts.append(hb)

    # ===================== 11 SCULPTED 3D CHESTS =====================

    # 1. Silver Lily cuirass (silvered steel + clean quartered surcoat outside the keel + silver lily clasp)
    clily = PartBuilder("chest_plate_lily")
    build_cuirass_base(clily, mat_id=4, hi=(1.16, 1.18, 1.24), lo=(1.02, 1.04, 1.10))
    clily.add_box((-0.056, 1.06, 0.214), (0.112, 0.42, 0.022), mat_id=1)  # faction quarter (clears keel!)
    clily.add_box((0.056, 1.06, 0.214), (0.112, 0.42, 0.022), mat_id=0, shade=(0.95, 0.94, 0.90))  # white quarter
    clily.add_box((0, 1.15, 0.230), (0.068, 0.084, 0.018), mat_id=4, shade=(1.22, 1.24, 1.30))  # silver lily clasp
    parts.append(clily)

    # 2. Vespran duellist plate (darkened Milanese steel + diagonal faction silk sash clearing keel!)
    cvesp = PartBuilder("chest_plate_vespran")
    build_cuirass_base(cvesp, mat_id=4, hi=(0.72, 0.74, 0.80), lo=(0.60, 0.62, 0.68))
    cvesp.add_box((0, 1.08, 0.216), (0.064, 0.42, 0.022), mat_id=1, rot_euler=(0, 0, -0.62))
    cvesp.add_box((0, 1.08, 0.228), (0.020, 0.40, 0.014), mat_id=6, rot_euler=(0, 0, -0.62))
    parts.append(cvesp)

    # 3. Tarn ash-iron penitent plate (matte grey highland iron, zero gold, central ash-grey monastic scapular clearing keel!)
    ctarn = PartBuilder("chest_plate_tarn")
    build_cuirass_base(ctarn, mat_id=5, hi=ash_iron, lo=(0.48, 0.50, 0.54), seam_shade=(0.32, 0.34, 0.38))
    ctarn.add_box((0, 1.06, 0.216), (0.145, 0.46, 0.024), mat_id=0, shade=ash_cloth)  # front scapular
    ctarn.add_box((0, 1.06, -0.186), (0.145, 0.46, 0.024), mat_id=0, shade=ash_cloth)  # back scapular
    parts.append(ctarn)

    # 4. Hollow Crown charred plate (soot-blackened steel with glowing ember gorget & waist seams)
    choll = PartBuilder("chest_plate_hollow")
    build_cuirass_base(choll, mat_id=4, hi=(0.28, 0.26, 0.30), lo=(0.20, 0.18, 0.22), seam_shade=(0.94, 0.38, 0.10))
    choll.add_loft([(0, 1.32, 0, 0.172, 0.154, segs), (0, 1.365, 0, 0.148, 0.136, segs)],
                   mat_id=0, shade=(0.96, 0.40, 0.12), cap_bot=False, cap_top=False)  # ember gorget seam
    parts.append(choll)

    # 5. Skar Wolfsworn iron scale harness with twin hacksilver brooches
    cjarl = PartBuilder("chest_jarl_scale")
    cjarl.add_loft([
        (0, 0.80, 0.0, 0.198, 0.150, segs),
        (0, 1.12, 0.012, 0.246, 0.172, segs),
        (0, 1.28, 0.0, 0.234, 0.152, segs),
        (0, 1.37, 0.0, 0.124, 0.114, segs),
    ], mat_id=5, shade=(0.74, 0.78, 0.84), cap_bot=False, ao_y=(0.80, 1.37, 0.78, 1.08))
    for sx in (-1, 1):
        cjarl.add_sphere((sx * 0.11, 1.22, 0.198), (0.036, 0.036, 0.020), segs=6, rings=3, mat_id=4, shade=(1.18, 1.20, 1.26))
    parts.append(cjarl)

    # 6. Qadir sun-mirror jawshan (dyed silk & mail + gleaming sculpted bronze-and-steel char-aina sun disc)
    cqad = PartBuilder("chest_mirror_qadir")
    cqad.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.010, 0.242, 0.166, segs),
        (0, 1.28, 0.0, 0.228, 0.148, segs),
        (0, 1.37, 0.0, 0.122, 0.112, segs),
    ], mat_id=1, cap_bot=False, ao_y=(0.80, 1.37, 0.80, 1.08))
    cqad.add_sphere((0, 1.12, 0.188), (0.115, 0.115, 0.030), segs=8, rings=3, mat_id=6)
    cqad.add_sphere((0, 1.12, 0.208), (0.056, 0.056, 0.020), segs=6, rings=3, mat_id=4, shade=(1.18, 1.20, 1.26))
    cqad.add_sphere((0, 1.12, -0.172), (0.105, 0.105, 0.026), segs=8, rings=3, mat_id=6)
    parts.append(cqad)

    # 7. Naryn Noyan lacquered lamellar with silver sky-disc chest medallion
    ckhan = PartBuilder("chest_lamellar_khan")
    ckhan.add_loft([
        (0, 0.80, 0.0, 0.198, 0.150, segs),
        (0, 1.12, 0.012, 0.246, 0.172, segs),
        (0, 1.28, 0.0, 0.232, 0.152, segs),
        (0, 1.37, 0.0, 0.124, 0.114, segs),
    ], mat_id=7, shade=(0.52, 0.42, 0.40), cap_bot=False, ao_y=(0.80, 1.37, 0.78, 1.08))
    ckhan.add_sphere((0, 1.12, 0.192), (0.072, 0.072, 0.026), segs=8, rings=3, mat_id=4, shade=(1.18, 1.20, 1.26))
    parts.append(ckhan)

    # 8. Brinemere Lantern arsenal cuirass (burnished three-quarter plate + brass lantern badge clearing keel!)
    clant = PartBuilder("chest_plate_lantern")
    build_cuirass_base(clant, mat_id=4, hi=(1.08, 1.10, 1.14), lo=(0.94, 0.96, 1.00))
    clant.add_box((0, 1.14, 0.214), (0.074, 0.105, 0.024), mat_id=6)
    clant.add_box((0, 1.14, 0.228), (0.036, 0.056, 0.014), mat_id=0, shade=(0.96, 0.56, 0.18))
    parts.append(clant)

    # 9. Mid-tier Qadiri quilted jubbah
    cjub = PartBuilder("chest_jubbah")
    cjub.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.01, 0.238, 0.164, segs),
        (0, 1.28, 0.0, 0.226, 0.148, segs),
        (0, 1.36, 0.0, 0.122, 0.112, segs),
    ], mat_id=1, cap_bot=False, ao_y=(0.80, 1.36, 0.80, 1.06))
    cjub.add_box((0, 1.10, 0.168), (0.032, 0.34, 0.020), mat_id=6, rot_euler=(0, 0, -0.32))  # diagonal brass buttons
    parts.append(cjub)

    # 10. Mid-tier Tarn mantled mail habit
    cthab = PartBuilder("chest_tarn_habit")
    cthab.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.01, 0.240, 0.166, segs),
        (0, 1.28, 0.0, 0.228, 0.148, segs),
        (0, 1.37, 0.0, 0.122, 0.112, segs),
    ], mat_id=5, shade=ash_iron, cap_bot=False, ao_y=(0.80, 1.37, 0.80, 1.08))
    cthab.add_box((0, 1.06, 0.175), (0.165, 0.46, 0.024), mat_id=0, shade=ash_cloth)
    cthab.add_box((0, 1.06, -0.162), (0.165, 0.46, 0.024), mat_id=0, shade=ash_cloth)
    parts.append(cthab)

    # 11. Mid-tier Brinemere arsenal buff coat (oiled leather + steel gorget + brass buttons)
    cbuff = PartBuilder("chest_buffcoat")
    buff_s = (0.96, 0.72, 0.48)
    cbuff.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.01, 0.240, 0.166, segs),
        (0, 1.28, 0.0, 0.228, 0.148, segs),
        (0, 1.36, 0.0, 0.122, 0.112, segs),
    ], mat_id=7, shade=buff_s, cap_bot=False, ao_y=(0.80, 1.36, 0.78, 1.06))
    cbuff.add_loft([(0, 1.28, 0, 0.185, 0.155, segs), (0, 1.38, 0, 0.128, 0.118, segs)], mat_id=4, cap_bot=False)  # steel gorget
    for by in (0.96, 1.08, 1.20):
        cbuff.add_box((0, by, 0.172), (0.028, 0.028, 0.018), mat_id=6)
    parts.append(cbuff)

    # ===================== MATCHING 3D SHOULDERS & SKIRTS =====================

    # Hollow Crown jagged blackened pauldrons
    sph = PartBuilder("shld_pauldrons_hollow")
    for sx in (-1, 1):
        sph.add_sphere((sx * 0.235, 1.25, 0.005), (0.125, 0.115, 0.135), segs=6, rings=4, mat_id=4, shade=char_s)
        sph.add_cone((sx * 0.26, 1.30, 0.0), (sx * 0.34, 1.40, 0.0), 0.042, 0.008, segs=4, mat_id=5, shade=(0.32, 0.30, 0.36))
    parts.append(sph)

    # Tarn ash-grey monastic mantle over grey iron pauldrons (full clearance over plate keel!)
    spt = PartBuilder("shld_tarn_mantle")
    spt.add_loft([(0, 1.18, 0.008, 0.292, 0.228, segs), (0, 1.32, 0.004, 0.264, 0.202, segs), (0, 1.405, 0, 0.152, 0.142, segs)],
                 mat_id=0, shade=ash_cloth, cap_bot=False)
    parts.append(spt)

    # Wolfsworn heavy grey wolf-pelt shoulder mantle (full clearance over scale/plate!)
    spw = PartBuilder("shld_wolf_mantle")
    spw.add_loft([(0, 1.16, 0.004, 0.304, 0.232, segs), (0, 1.30, 0.002, 0.278, 0.214, segs), (0, 1.405, 0, 0.158, 0.146, segs)],
                 mat_id=7, shade=wolf_fur, cap_bot=False)
    parts.append(spw)

    # Tarn matte grey iron faulds + hanging ash-grey scapular (stops cleanly at waist belt!)
    skt = PartBuilder("skirt_tarn_scapular")
    skt.add_loft([(0, 0.54, 0, 0.234, 0.170, segs), (0, 0.81, 0, 0.194, 0.146, segs)], mat_id=5, shade=ash_iron, cap_top=False)
    skt.add_box((0, 0.62, 0.196), (0.145, 0.40, 0.022), mat_id=0, shade=ash_cloth)
    skt.add_box((0, 0.62, -0.184), (0.145, 0.40, 0.022), mat_id=0, shade=ash_cloth)
    parts.append(skt)

    # Hollow Crown charred jagged tassets
    skh = PartBuilder("skirt_fauld_hollow")
    skh.add_loft([(0, 0.52, 0, 0.232, 0.168, segs), (0, 0.80, 0, 0.198, 0.148, segs)], mat_id=4, shade=char_s, smooth=False, cap_top=False)
    for sx in (-1, 1):
        skh.add_box((sx * 0.16, 0.66, 0.10), (0.13, 0.24, 0.042), mat_id=4, shade=(0.22, 0.20, 0.25), rot_euler=(-0.10, sx * 0.40, sx * 0.10))
    parts.append(skh)

    # ===================== 3 ICONIC 3D SHIELDS =====================

    # 1. Brinemere Lantern Guild Pavise
    spv = PartBuilder("shield_lantern_pavise")
    spv.add_box((0, 0.02, 0.020), (0.56, 0.96, 0.032), mat_id=6)  # brass rim backing
    spv.add_box((0, 0.02, 0.044), (0.48, 0.88, 0.036), mat_id=1, ao_y=(-0.44, 0.48, 0.78, 1.08))
    spv.add_box((0, 0.02, 0.064), (0.10, 0.86, 0.024), mat_id=7, shade=buff_s)  # central ridge
    spv.add_box((0, 0.12, 0.076), (0.16, 0.24, 0.032), mat_id=6)  # brass lantern housing
    spv.add_box((0, 0.12, 0.090), (0.09, 0.14, 0.020), mat_id=0, shade=(0.98, 0.56, 0.18))  # lantern glow
    parts.append(spv)

    # 2. Tarn Ash-Iron Kite Shield (unadorned hammered grey highland iron, zero gold)
    stk = PartBuilder("shield_tarn_kite")
    stk.add_box((0, 0.08, 0.022), (0.46, 0.64, 0.030), mat_id=5, shade=(0.36, 0.38, 0.42))
    stk.add_box((0, 0.08, 0.044), (0.40, 0.58, 0.036), mat_id=5, shade=ash_iron, ao_y=(-0.36, 0.40, 0.78, 1.06))
    stk.add_box((0, -0.30, 0.020), (0.26, 0.31, 0.028), mat_id=5, shade=(0.36, 0.38, 0.42))
    stk.add_box((0, -0.29, 0.040), (0.21, 0.27, 0.034), mat_id=5, shade=ash_iron)
    stk.add_box((0, -0.02, 0.066), (0.075, 0.74, 0.022), mat_id=0, shade=ash_cloth)  # vertical ash cross-rib
    stk.add_box((0, 0.12, 0.066), (0.36, 0.075, 0.022), mat_id=0, shade=ash_cloth)   # horizontal ash cross-rib
    parts.append(stk)

    # 3. Qadir Sun-Mirror Kalkan (domed circular shield with radiating bronze sun-ribs & mirror boss)
    ssk = PartBuilder("shield_sun_kalkan")
    ssk.add_loft_z([(0, 0, 0.00, 0.325, 0.325, 8), (0, 0, 0.034, 0.325, 0.325, 8)], mat_id=6)  # bronze rim
    ssk.add_loft_z([(0, 0, 0.028, 0.290, 0.290, 8), (0, 0, 0.068, 0.250, 0.250, 8)], mat_id=1, ao_y=(-0.30, 0.30, 0.82, 1.08))
    for k in range(4):
        ang = k * math.pi / 4.0
        ssk.add_box((0, 0, 0.072), (0.52, 0.028, 0.020), mat_id=6, rot_euler=(0, 0, ang))
    ssk.add_sphere((0, 0, 0.088), (0.105, 0.105, 0.048), segs=6, rings=3, mat_id=6)
    ssk.add_sphere((0, 0, 0.118), (0.052, 0.052, 0.024), segs=6, rings=3, mat_id=4, shade=(1.18, 1.20, 1.26))
    parts.append(ssk)

    return parts
