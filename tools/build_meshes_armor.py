"""
Modular composable 3D body armor system for Ironfield (imported by tools/build_meshes_3d.py).
Divides body armor into 4 independent interlocking layers so all 29+ body armor IDs and per-soldier
regiment variety combine cleanly with per-layer procedural surface patterns (mail, scale, quilt, brushed steel):
  Layer 1 — Chest / Cuirass (y = 0.80 .. 1.34): chest_*
  Layer 2 — Shoulders / Collars / Mantles (y = 1.12 .. 1.41): shld_*
  Layer 3 — Belts / Sashes / Baldrics / Chest Harnesses (y = 0.74 .. 1.24): belt_*
  Layer 4 — Skirts / Faulds / Tassets / Tabards (y = 0.38 .. 0.86): skirt_*
"""
import math
from mathutils import Vector


def build_modular_armors(PartBuilder):
    parts = []
    segs = 8

    # ===================== LAYER 1: CHESTS / CUIRASSES =====================

    # 1. Quilted cloth tunic / gambeson chest with V-laced chest placket
    ct = PartBuilder("chest_tunic")
    ct.add_loft([
        (0, 0.80, 0.0, 0.194, 0.146, segs),
        (0, 1.12, 0.01, 0.236, 0.162, segs),
        (0, 1.28, 0.0, 0.226, 0.146, segs),
        (0, 1.36, 0.0, 0.122, 0.112, segs),
    ], mat_id=1, cap_bot=False, ao_y=(0.80, 1.36, 0.80, 1.06))
    ct.add_box((0, 1.19, 0.158), (0.072, 0.19, 0.022), mat_id=3)
    parts.append(ct)

    # 2. Faction surcoat chest (clean heraldic cloth chest worn over mail or plate!)
    cs = PartBuilder("chest_surcoat")
    cs.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.01, 0.240, 0.165, segs),
        (0, 1.28, 0.0, 0.228, 0.148, segs),
        (0, 1.35, 0.0, 0.125, 0.114, segs),
    ], mat_id=1, cap_bot=False, ao_y=(0.80, 1.36, 0.80, 1.06))
    parts.append(cs)

    # 3. Stitched leather jerkin / cuirass (dark warm leather with reinforced trim!)
    cl = PartBuilder("chest_leather")
    cl.add_loft([
        (0, 0.80, 0.0, 0.194, 0.146, segs),
        (0, 1.12, 0.01, 0.236, 0.162, segs),
        (0, 1.28, 0.0, 0.224, 0.145, segs),
        (0, 1.36, 0.0, 0.118, 0.108, segs),
    ], mat_id=7, shade=(0.92, 0.86, 0.80), cap_bot=False, ao_y=(0.80, 1.36, 0.78, 1.06))
    cl.add_box((0, 1.12, 0.160), (0.045, 0.34, 0.020), mat_id=8)  # center leather lacing seam
    parts.append(cl)

    # 4. Exposed gleaming chainmail hauberk chest (mat_id=5 -> PatMail rings!)
    cm = PartBuilder("chest_mail")
    cm.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.01, 0.240, 0.165, segs),
        (0, 1.28, 0.0, 0.228, 0.148, segs),
        (0, 1.37, 0.0, 0.120, 0.110, segs),
    ], mat_id=5, cap_bot=False, ao_y=(0.80, 1.37, 0.80, 1.08))
    parts.append(cm)

    # 5. Brigandine / coat-of-plates (faction riveted doublet with 3 horizontal steel buckle straps!)
    cb = PartBuilder("chest_brigandine")
    cb.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.012, 0.244, 0.170, segs),
        (0, 1.28, 0.0, 0.232, 0.150, segs),
        (0, 1.37, 0.0, 0.122, 0.112, segs),
    ], mat_id=1, cap_bot=False, ao_y=(0.80, 1.37, 0.78, 1.06))
    for cy in (0.99, 1.11, 1.23):
        cb.add_box((0, cy, 0.172), (0.145, 0.032, 0.024), mat_id=4, shade=(0.94, 0.96, 1.00))
    parts.append(cb)

    # 6. Sculpted steel gothic breastplate with proud central keel ridge!
    cp = PartBuilder("chest_plate")
    rings = []
    for y, rx, rz, zoff in [
        (0.80, 0.190, 0.144, 0.008),
        (1.10, 0.246, 0.170, 0.014),
        (1.28, 0.236, 0.150, 0.000),
        (1.37, 0.120, 0.110, 0.000),
    ]:
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            x = math.cos(a) * rx
            z = math.sin(a) * rz + zoff
            if math.sin(a) > 0.72 and 0.92 < y < 1.24:
                z += 0.024  # sculpted keel ridge
            ring.append(Vector((x, y, z)))
        rings.append(ring)
    cp.add_loft(rings, mat_id=4, cap_bot=False, ao_y=(0.80, 1.37, 0.80, 1.10))
    parts.append(cp)

    # 7. Mirror armor (Eastern / Persian char-aina: gleaming circular steel mirror disc strapped over faction mail!)
    cmr = PartBuilder("chest_mirror")
    cmr.add_loft([
        (0, 0.80, 0.0, 0.196, 0.148, segs),
        (0, 1.12, 0.01, 0.240, 0.165, segs),
        (0, 1.28, 0.0, 0.228, 0.148, segs),
        (0, 1.36, 0.0, 0.120, 0.110, segs),
    ], mat_id=1, cap_bot=False, ao_y=(0.80, 1.36, 0.78, 1.06))
    # Gleaming round mirror-plate front & back + gilded boss rim + crossing leather straps!
    cmr.add_loft_z([(0, 1.09, 0.155, 0.145, 0.145, 8), (0, 1.09, 0.186, 0.130, 0.130, 8)], mat_id=4, shade=(1.08, 1.10, 1.14))
    cmr.add_loft_z([(0, 1.09, 0.182, 0.055, 0.055, 6), (0, 1.09, 0.196, 0.042, 0.042, 6)], mat_id=6)
    cmr.add_loft_z([(0, 1.09, -0.175, 0.125, 0.125, 6), (0, 1.09, -0.148, 0.138, 0.138, 6)], mat_id=4)
    parts.append(cmr)

    # 8. Golden-brass lamellar cuirass chest (mat_id=6 -> PatScale shingles!)
    cla = PartBuilder("chest_lamellar")
    cla.add_loft([
        (0, 0.80, 0.0, 0.212, 0.162, segs),
        (0, 1.12, 0.012, 0.246, 0.174, segs),
        (0, 1.28, 0.0, 0.234, 0.154, segs),
        (0, 1.36, 0.0, 0.122, 0.112, segs),
    ], mat_id=6, cap_bot=False, ao_y=(0.80, 1.36, 0.80, 1.10))
    parts.append(cla)

    # 9. Iron scale hauberk chest (mat_id=5 -> PatScale shingles!)
    csc = PartBuilder("chest_scale")
    csc.add_loft([
        (0, 0.80, 0.0, 0.202, 0.154, segs),
        (0, 1.12, 0.012, 0.244, 0.172, segs),
        (0, 1.28, 0.0, 0.234, 0.152, segs),
        (0, 1.37, 0.0, 0.122, 0.110, segs),
    ], mat_id=5, cap_bot=False, ao_y=(0.80, 1.37, 0.80, 1.10))
    parts.append(csc)

    # 10. Full-length caster / monk robe (standalone chest+skirt silhouette, pairs with any shoulder/belt!)
    crb = PartBuilder("chest_robe")
    crb.add_loft([
        (0, 0.18, 0.0, 0.295, 0.235, segs),
        (0, 0.54, 0.0, 0.242, 0.182, segs),
        (0, 0.85, 0.0, 0.194, 0.148, segs),
        (0, 1.15, 0.01, 0.230, 0.158, segs),
        (0, 1.36, 0.0, 0.130, 0.115, segs),
    ], mat_id=2, ao_y=(0.18, 1.36, 0.74, 1.06))
    parts.append(crb)

    # 11. Skeleton ribcage + spine + sternum + clavicles + pelvis
    cbn = PartBuilder("chest_bones")
    cbn.add_cone((0, 0.70, -0.02), (0, 1.36, -0.02), 0.048, 0.044, segs=5, mat_id=9, shade=(0.82, 0.80, 0.75))
    cbn.add_box((0, 0.72, 0.0), (0.31, 0.12, 0.18), mat_id=9, shade=(0.86, 0.84, 0.78))
    cbn.add_box((0, 1.12, 0.11), (0.065, 0.28, 0.040), mat_id=9, shade=(1.02, 1.00, 0.96))
    cbn.add_box((0, 1.31, 0.05), (0.42, 0.048, 0.065), mat_id=9, shade=(1.02, 1.00, 0.96))
    for ry in (0.94, 1.06, 1.18, 1.26):
        w = 0.36 if ry > 1.0 else 0.30
        cbn.add_box((0, ry, 0.02), (w, 0.052, 0.20), mat_id=9, shade=(0.98, 0.96, 0.92))
    parts.append(cbn)

    # ===================== LAYER 2: SHOULDERS, COLLARS & MANTLES =====================

    # 1. Padded standing gambeson collar
    s_cc = PartBuilder("shld_collar_cloth")
    s_cc.add_loft([(0, 1.30, 0, 0.162, 0.144, segs), (0, 1.40, 0, 0.132, 0.120, segs)], mat_id=2, cap_bot=False, ao_y=(1.30, 1.40, 0.84, 1.08))
    parts.append(s_cc)

    # 2. Exposed gleaming chainmail aventail yoke & collar mantle!
    s_ma = PartBuilder("shld_mail_aventail")
    s_ma.add_loft([(0, 1.19, 0.0, 0.252, 0.174, segs), (0, 1.40, 0.0, 0.128, 0.118, segs)], mat_id=5, cap_bot=False, ao_y=(1.19, 1.40, 0.82, 1.12))
    parts.append(s_ma)

    # 3. Beveled steel gorget + compact steel spaulder flanges (foot soldiers / breastplates!)
    s_gs = PartBuilder("shld_gorget_steel")
    s_gs.add_loft([(0, 1.30, 0, 0.162, 0.142, segs), (0, 1.40, 0, 0.124, 0.114, segs)], mat_id=4, cap_bot=False, ao_y=(1.30, 1.40, 0.84, 1.10))
    for sx in (-1, 1):
        s_gs.add_box((sx * 0.225, 1.25, 0.01), (0.105, 0.105, 0.185), mat_id=4, rot_euler=(0, 0, -sx * 0.26), shade=(1.04, 1.06, 1.10))
    parts.append(s_gs)

    # 4. Bold rounded knightly steel pauldrons + steel gorget!
    s_ps = PartBuilder("shld_pauldrons_steel")
    s_ps.add_loft([(0, 1.30, 0, 0.164, 0.144, segs), (0, 1.40, 0, 0.124, 0.114, segs)], mat_id=4, cap_bot=False)
    for sx in (-1, 1):
        s_ps.add_sphere((sx * 0.255, 1.25, 0.005), (0.125, 0.115, 0.135), segs=6, rings=3, mat_id=4, shade=(1.05, 1.07, 1.10), ao_y=(1.14, 1.36, 0.84, 1.12))
        s_ps.add_box((sx * 0.275, 1.17, 0.005), (0.095, 0.085, 0.185), mat_id=4, rot_euler=(0, 0, -sx * 0.22), shade=(0.94, 0.96, 1.00))
    parts.append(s_ps)

    # 5. Ornate gilded brass pauldrons + gilded gorget!
    s_pg = PartBuilder("shld_pauldrons_gilded")
    s_pg.add_loft([(0, 1.30, 0, 0.166, 0.146, segs), (0, 1.41, 0, 0.124, 0.114, segs)], mat_id=6, cap_bot=False)
    for sx in (-1, 1):
        s_pg.add_sphere((sx * 0.258, 1.255, 0.005), (0.130, 0.120, 0.140), segs=6, rings=3, mat_id=6, ao_y=(1.14, 1.38, 0.86, 1.14))
        s_pg.add_box((sx * 0.278, 1.17, 0.005), (0.095, 0.085, 0.190), mat_id=4, rot_euler=(0, 0, -sx * 0.22))
    parts.append(s_pg)

    # 6. High-guard fluted gothic tournament pauldrons (raised neck-guard wings!)
    s_pf = PartBuilder("shld_pauldrons_fluted")
    s_pf.add_loft([(0, 1.30, 0, 0.166, 0.146, segs), (0, 1.40, 0, 0.124, 0.114, segs)], mat_id=4, cap_bot=False)
    for sx in (-1, 1):
        s_pf.add_sphere((sx * 0.255, 1.25, 0.005), (0.125, 0.115, 0.135), segs=6, rings=3, mat_id=4, shade=(1.08, 1.10, 1.14))
        s_pf.add_box((sx * 0.200, 1.35, 0.005), (0.042, 0.135, 0.195), mat_id=4, rot_euler=(0, 0, sx * 0.18), shade=(1.10, 1.12, 1.16))  # haute-piece neck guards!
        s_pf.add_box((sx * 0.260, 1.28, 0.005), (0.115, 0.032, 0.215), mat_id=6)  # brass fluting ridge
    parts.append(s_pf)

    # 7. Flared golden-brass lamellar shoulder spaulders + crisp white linen scarf collar!
    s_la = PartBuilder("shld_lamellar")
    s_la.add_loft([(0, 1.28, 0, 0.180, 0.156, segs), (0, 1.39, 0, 0.132, 0.122, segs)], mat_id=0, shade=(0.95, 0.93, 0.88), cap_bot=False)
    for sx in (-1, 1):
        s_la.add_box((sx * 0.242, 1.22, 0.008), (0.125, 0.16, 0.195), mat_id=6, rot_euler=(0, 0, -sx * 0.30), ao_y=(1.12, 1.34, 0.82, 1.10))
    parts.append(s_la)

    # 8. Flared layered iron scale shoulder guards + brass neck border!
    s_sc = PartBuilder("shld_scale")
    s_sc.add_loft([(0, 1.32, 0, 0.160, 0.142, segs), (0, 1.39, 0, 0.130, 0.118, segs)], mat_id=6, cap_bot=False)
    for sx in (-1, 1):
        s_sc.add_box((sx * 0.238, 1.22, 0.008), (0.12, 0.15, 0.195), mat_id=5, rot_euler=(0, 0, -sx * 0.28))
    parts.append(s_sc)

    # 9. Heavy shaggy Northern fur pelt mantle over both shoulders & back!
    s_fm = PartBuilder("shld_fur_mantle")
    s_fm.add_loft([
        (0, 1.12, -0.025, 0.295, 0.208, segs),
        (0, 1.27, -0.015, 0.316, 0.220, segs),
        (0, 1.39, 0.000, 0.194, 0.158, segs),
    ], mat_id=7, shade=(0.76, 0.68, 0.58), cap_bot=False, ao_y=(1.12, 1.39, 0.80, 1.12))
    for sx in (-1, 1):
        s_fm.add_sphere((sx * 0.26, 1.22, -0.02), (0.105, 0.125, 0.135), segs=5, rings=3, mat_id=7, shade=(0.70, 0.62, 0.52))
    parts.append(s_fm)

    # 10. Steppe plush fur collar + diagonal fur V-lapel across chest!
    s_fc = PartBuilder("shld_fur_collar")
    s_fc.add_loft([(0, 1.25, 0, 0.222, 0.170, segs), (0, 1.39, 0, 0.152, 0.138, segs)], mat_id=7, shade=(0.86, 0.78, 0.66), cap_bot=False, ao_y=(1.25, 1.39, 0.82, 1.10))
    s_fc.add_box((0.03, 1.08, 0.168), (0.068, 0.40, 0.028), mat_id=7, shade=(0.86, 0.78, 0.66), rot_euler=(0, 0, 0.34))
    parts.append(s_fc)

    # 11. Flowing desert linen shawl / caster cowl mantle!
    s_ls = PartBuilder("shld_linen_shawl")
    s_ls.add_loft([(0, 1.22, -0.005, 0.252, 0.180, segs), (0, 1.39, 0.0, 0.142, 0.128, segs)], mat_id=0, shade=(0.95, 0.93, 0.88), cap_bot=False, ao_y=(1.22, 1.39, 0.84, 1.08))
    parts.append(s_ls)

    # 12. Caster cloth cowl mantle (faction primary cloth + brass clasp!)
    s_cm = PartBuilder("shld_cowl_cloth")
    s_cm.add_loft([(0, 1.14, -0.01, 0.268, 0.188, segs), (0, 1.28, -0.01, 0.256, 0.178, segs), (0, 1.39, 0, 0.145, 0.130, segs)], mat_id=1, cap_bot=False, ao_y=(1.14, 1.39, 0.80, 1.08))
    s_cm.add_box((0, 1.24, 0.168), (0.074, 0.058, 0.026), mat_id=6)
    parts.append(s_cm)

    # ===================== LAYER 3: BELTS, SASHES & CHEST HARNESSES =====================

    # 1. Classic leather sword-belt + brass buckle
    b_l = PartBuilder("belt_leather")
    b_l.add_box((0, 0.84, 0.004), (0.422, 0.062, 0.342), mat_id=7)
    b_l.add_box((0, 0.84, 0.180), (0.074, 0.066, 0.024), mat_id=6)
    parts.append(b_l)

    # 2. Leather belt + hip pouch + small belt dagger/satchel
    b_p = PartBuilder("belt_pouch")
    b_p.add_box((0, 0.84, 0.004), (0.422, 0.062, 0.342), mat_id=7)
    b_p.add_box((0, 0.84, 0.180), (0.072, 0.064, 0.024), mat_id=6)
    b_p.add_box((-0.195, 0.76, 0.065), (0.076, 0.115, 0.095), mat_id=7, shade=(0.84, 0.80, 0.76))
    b_p.add_box((0.185, 0.75, 0.040), (0.050, 0.14, 0.055), mat_id=13)  # hip scabbard/pouch
    parts.append(b_p)

    # 3. Ornate gilded knightly belt + gilded chest trim bar!
    b_g = PartBuilder("belt_gilded")
    b_g.add_box((0, 0.845, 0.005), (0.432, 0.068, 0.352), mat_id=6)
    b_g.add_box((0, 1.15, 0.184), (0.25, 0.052, 0.022), mat_id=6)
    parts.append(b_g)

    # 4. Wide bright faction-trim waist sash + brass buckle!
    b_st = PartBuilder("belt_sash_trim")
    b_st.add_box((0, 0.84, 0.004), (0.430, 0.080, 0.345), mat_id=3)
    b_st.add_box((0, 0.84, 0.182), (0.074, 0.070, 0.026), mat_id=6)
    parts.append(b_st)

    # 5. Wide dark contrast waist sash + gleaming brass medallion clasp!
    b_sd = PartBuilder("belt_sash_dark")
    b_sd.add_box((0, 0.84, 0.004), (0.440, 0.072, 0.352), mat_id=2)
    b_sd.add_box((0, 0.84, 0.184), (0.078, 0.074, 0.026), mat_id=6)
    parts.append(b_sd)

    # 6. Diagonal cross-chest leather baldric strap + iron/brass buckle + sword-belt!
    b_bd = PartBuilder("belt_baldric")
    b_bd.add_box((0, 0.84, 0.004), (0.425, 0.062, 0.342), mat_id=7)
    b_bd.add_box((0, 0.84, 0.180), (0.072, 0.064, 0.024), mat_id=6)
    b_bd.add_box((0, 1.07, 0.184), (0.076, 0.45, 0.028), mat_id=7, shade=(0.74, 0.62, 0.50), rot_euler=(0, 0, 0.54))
    b_bd.add_box((0, 1.07, -0.162), (0.076, 0.45, 0.028), mat_id=7, shade=(0.74, 0.62, 0.50), rot_euler=(0, 0, -0.54))
    b_bd.add_box((-0.05, 1.13, 0.198), (0.064, 0.056, 0.020), mat_id=6)
    parts.append(b_bd)

    # 7. Crossed Northern X-harness chest straps + iron chest boss + war-belt!
    b_cx = PartBuilder("belt_cross_straps")
    b_cx.add_box((0, 0.84, 0.004), (0.425, 0.068, 0.342), mat_id=7)
    b_cx.add_box((0, 0.84, 0.180), (0.074, 0.066, 0.024), mat_id=6)
    b_cx.add_box((0, 1.05, 0.184), (0.068, 0.42, 0.028), mat_id=7, shade=(0.68, 0.54, 0.42), rot_euler=(0, 0, 0.52))
    b_cx.add_box((0, 1.05, 0.186), (0.068, 0.42, 0.028), mat_id=7, shade=(0.68, 0.54, 0.42), rot_euler=(0, 0, -0.52))
    b_cx.add_box((0, 1.05, 0.200), (0.074, 0.074, 0.026), mat_id=4)
    parts.append(b_cx)

    # 8. Ornate caster/order stole + cinched trim waist cord!
    b_sl = PartBuilder("belt_stole")
    b_sl.add_box((0, 0.85, 0.004), (0.420, 0.062, 0.330), mat_id=3)
    b_sl.add_box((0, 0.88, 0.162), (0.095, 0.68, 0.024), mat_id=3)
    parts.append(b_sl)

    # ===================== LAYER 4: SKIRTS, FAULDS, TASSETS & TABARDS =====================

    # 1. Quilted faction gambeson/tunic skirt with darker hem trim
    k_tu = PartBuilder("skirt_tunic")
    k_tu.add_loft([(0, 0.54, 0, 0.236, 0.174, segs), (0, 0.83, 0, 0.194, 0.146, segs)], mat_id=1, cap_top=False, ao_y=(0.54, 0.83, 0.76, 1.02))
    k_tu.add_loft([(0, 0.53, 0, 0.242, 0.179, segs), (0, 0.58, 0, 0.238, 0.176, segs)], mat_id=2, cap_bot=False, cap_top=False)
    parts.append(k_tu)

    # 2. Chainmail hauberk skirt peeking down to upper thighs (mat_id=5 -> PatMail rings!)
    k_ma = PartBuilder("skirt_mail")
    k_ma.add_loft([(0, 0.48, 0, 0.238, 0.176, segs), (0, 0.83, 0, 0.196, 0.148, segs)], mat_id=5, cap_top=False, ao_y=(0.48, 0.83, 0.74, 1.00))
    parts.append(k_ma)

    # 3. Layered steel fauld + angled steel hip tassets over dark mail hem!
    k_fs = PartBuilder("skirt_fauld_steel")
    k_fs.add_loft([(0, 0.53, 0, 0.230, 0.168, segs), (0, 0.66, 0, 0.214, 0.156, segs)], mat_id=5, cap_top=False, ao_y=(0.53, 0.66, 0.72, 0.94))
    k_fs.add_loft([(0, 0.65, 0.008, 0.224, 0.164, segs), (0, 0.83, 0.008, 0.190, 0.144, segs)], mat_id=4, cap_top=False, ao_y=(0.65, 0.83, 0.80, 1.06))
    for sx in (-1, 1):
        k_fs.add_box((sx * 0.165, 0.68, 0.095), (0.135, 0.20, 0.048), mat_id=4, rot_euler=(-0.10, sx * 0.42, sx * 0.10), ao_y=(0.56, 0.80, 0.80, 1.06))
    parts.append(k_fs)

    # 4. Ornate gilded brass tassets + steel fauld over dark mail hem!
    k_fg = PartBuilder("skirt_fauld_gilded")
    k_fg.add_loft([(0, 0.53, 0, 0.232, 0.170, segs), (0, 0.66, 0, 0.216, 0.158, segs)], mat_id=5, cap_top=False)
    k_fg.add_loft([(0, 0.65, 0.008, 0.226, 0.166, segs), (0, 0.83, 0.008, 0.192, 0.146, segs)], mat_id=4, cap_top=False)
    for sx in (-1, 1):
        k_fg.add_box((sx * 0.168, 0.68, 0.095), (0.140, 0.205, 0.050), mat_id=6, rot_euler=(-0.10, sx * 0.42, sx * 0.10))
    parts.append(k_fg)

    # 5. Flowing faction surcoat tabard panels (front & back, layers cleanly over ANY skirt/fauld!)
    k_tb = PartBuilder("skirt_tabard")
    k_tb.add_box((0, 0.80, 0.170), (0.220, 0.52, 0.026), mat_id=1, ao_y=(0.54, 1.08, 0.78, 1.06))
    k_tb.add_box((0, 0.80, -0.156), (0.220, 0.52, 0.026), mat_id=1, ao_y=(0.54, 1.08, 0.74, 1.02))
    parts.append(k_tb)

    # 6. Tiered golden-brass lamellar hip tassets over flowing faction under-skirt!
    k_la = PartBuilder("skirt_lamellar")
    k_la.add_loft([(0, 0.46, 0, 0.252, 0.190, segs), (0, 0.74, 0, 0.216, 0.164, segs)], mat_id=1, cap_top=False, ao_y=(0.46, 0.74, 0.74, 1.02))
    k_la.add_loft([(0, 0.61, 0, 0.246, 0.184, segs), (0, 0.83, 0, 0.212, 0.162, segs)], mat_id=6, cap_top=False, ao_y=(0.61, 0.83, 0.78, 1.06))
    parts.append(k_la)

    # 7. Scale skirt with brass border hem!
    k_sc = PartBuilder("skirt_scale")
    k_sc.add_loft([(0, 0.53, 0, 0.242, 0.178, segs), (0, 0.83, 0, 0.202, 0.154, segs)], mat_id=5, cap_top=False, ao_y=(0.53, 0.83, 0.76, 1.04))
    k_sc.add_loft([(0, 0.52, 0, 0.248, 0.184, segs), (0, 0.57, 0, 0.244, 0.180, segs)], mat_id=6, cap_bot=False, cap_top=False)
    parts.append(k_sc)

    # 8. Long flared Steppe deel riding coat skirt with plush fur hem!
    k_dl = PartBuilder("skirt_deel")
    k_dl.add_loft([(0, 0.41, 0, 0.268, 0.204, segs), (0, 0.83, 0, 0.200, 0.156, segs)], mat_id=1, cap_top=False, ao_y=(0.41, 0.83, 0.74, 1.04))
    k_dl.add_loft([(0, 0.40, 0, 0.274, 0.210, segs), (0, 0.46, 0, 0.268, 0.205, segs)], mat_id=7, shade=(0.84, 0.76, 0.64), cap_bot=False, cap_top=False)
    parts.append(k_dl)

    # 9. Flowing desert kaftan skirt + crisp white linen inner front panel!
    k_kf = PartBuilder("skirt_kaftan")
    k_kf.add_loft([(0, 0.38, 0, 0.272, 0.212, segs), (0, 0.83, 0, 0.200, 0.156, segs)], mat_id=1, cap_top=False, ao_y=(0.38, 0.83, 0.76, 1.06))
    k_kf.add_box((0, 0.88, 0.166), (0.135, 0.66, 0.026), mat_id=0, shade=(0.95, 0.93, 0.88))
    parts.append(k_kf)

    return parts
