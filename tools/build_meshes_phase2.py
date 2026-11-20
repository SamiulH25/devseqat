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

    def add_mail_coif(p, mat_id=5, shade=(0.96, 0.96, 1.0)):
        p.add_loft([
            (0, -0.17, -0.045, 0.175, 0.145, 7),
            (0, 0.06, -0.045, 0.172, 0.140, 7),
        ], mat_id=mat_id, shade=shade, cap_bot=False, cap_top=False, ao_y=(-0.17, 0.08, 0.76, 1.02))

    def greathelm_base(name):
        p = PartBuilder(name)
        p.add_loft([
            (0, -0.155, 0.00, 0.172, 0.178, segs),
            (0, 0.040, 0.008, 0.182, 0.188, segs),
            (0, 0.230, 0.000, 0.168, 0.174, segs),
            (0, 0.275, 0.000, 0.135, 0.140, segs),
        ], mat_id=4, ao_y=(-0.155, 0.275, 0.76, 1.10))
        p.add_box((0, 0.062, 0.192), (0.255, 0.042, 0.034), mat_id=0, shade=(0.05, 0.05, 0.07))
        p.add_box((0, 0.035, 0.205), (0.044, 0.32, 0.026), mat_id=6)
        p.add_box((0, 0.095, 0.203), (0.275, 0.036, 0.026), mat_id=6)
        p.add_loft([(0, 0.215, 0, 0.174, 0.180, segs), (0, 0.242, 0, 0.172, 0.178, segs)], mat_id=6, cap_bot=False, cap_top=False)
        return p

    # 1. Winged greathelm (swept 3-tiered fanned feathered wings!)
    gw = greathelm_base("helm_greathelm_wings")
    for sx in (-1, 1):
        gw.add_box((sx * 0.21, 0.22, -0.01), (0.11, 0.22, 0.050), mat_id=6, rot_euler=(0.0, sx * 0.22, -sx * 0.26))
        gw.add_box((sx * 0.27, 0.33, -0.04), (0.13, 0.24, 0.044), mat_id=3, rot_euler=(0.0, sx * 0.28, -sx * 0.38), ao_y=(0.18, 0.48, 0.84, 1.12))
        gw.add_box((sx * 0.34, 0.43, -0.08), (0.10, 0.18, 0.036), mat_id=6, rot_euler=(0.0, sx * 0.34, -sx * 0.52))
    parts.append(gw)

    # 2. Sunburst greathelm (Order of the Ashen Sun regal crown + 7 radiant golden rays!)
    gs = greathelm_base("helm_greathelm_sun")
    gs.add_loft([(0, 0.22, 0, 0.178, 0.184, segs), (0, 0.285, 0, 0.192, 0.198, segs)], mat_id=6)
    for k in range(7):
        ang = (k - 3) * 0.42
        h = 0.47 if k == 3 else (0.43 if abs(k - 3) == 1 else 0.39)
        gs.add_cone((math.sin(ang) * 0.145, 0.27, math.cos(ang) * 0.145),
                    (math.sin(ang) * 0.24, h, math.cos(ang) * 0.24), 0.036, 0.006, segs=4, mat_id=6)
    parts.append(gs)

    # 3. Horned greathelm (brass sockets + thick double-curved ivory horns!)
    gh = greathelm_base("helm_greathelm_horns")
    for sx in (-1, 1):
        gh.add_cone((sx * 0.14, 0.18, 0.0), (sx * 0.22, 0.23, 0.01), 0.058, 0.048, segs=5, mat_id=6)
        gh.add_cone((sx * 0.21, 0.22, 0.01), (sx * 0.35, 0.32, 0.03), 0.046, 0.028, segs=5, mat_id=3)
        gh.add_cone((sx * 0.35, 0.32, 0.03), (sx * 0.27, 0.49, 0.08), 0.028, 0.007, segs=5, mat_id=3)
    parts.append(gh)

    # 4. Horned northern spangenhelm (brass ribs, spectacle guard, mail aventail & sweeping Viking horns!)
    sh = PartBuilder("helm_spangen_horns")
    add_mail_coif(sh, mat_id=5)
    sh.add_loft([(0, 0.048, 0, 0.192, 0.196, segs), (0, 0.095, 0, 0.190, 0.194, segs)], mat_id=6)
    sh.add_loft([(0, 0.085, 0, 0.182, 0.188, segs), (0, 0.185, 0, 0.148, 0.154, segs), (0, 0.295, 0, 0.028, 0.028, segs)], mat_id=4, ao_y=(0.08, 0.30, 0.78, 1.10))
    sh.add_box((0, 0.195, 0.0), (0.030, 0.20, 0.31), mat_id=6)
    sh.add_box((0, -0.005, 0.192), (0.044, 0.12, 0.028), mat_id=5)
    sh.add_box((0, 0.048, 0.190), (0.18, 0.030, 0.028), mat_id=6)
    for sx in (-1, 1):
        sh.add_cone((sx * 0.14, 0.14, 0.0), (sx * 0.22, 0.19, 0.02), 0.054, 0.044, segs=5, mat_id=6)
        sh.add_cone((sx * 0.21, 0.18, 0.02), (sx * 0.35, 0.28, 0.04), 0.042, 0.024, segs=5, mat_id=3)
        sh.add_cone((sx * 0.35, 0.28, 0.04), (sx * 0.28, 0.45, 0.09), 0.024, 0.007, segs=5, mat_id=3)
    parts.append(sh)

    # 5. Bear-head pelt helm (open fanged bear jaw crest framing warrior face + thick pelt cheeks!)
    bh = PartBuilder("helm_bearhead")
    add_mail_coif(bh, mat_id=7, shade=(0.76, 0.66, 0.58))
    bh.add_sphere((0, 0.11, -0.02), (0.218, 0.198, 0.218), segs=7, rings=5, mat_id=7, shade=(0.82, 0.72, 0.64), ao_y=(-0.06, 0.30, 0.76, 1.08))
    bh.add_box((0, 0.14, 0.18), (0.15, 0.105, 0.155), mat_id=7, shade=(1.06, 0.96, 0.86))  # sculpted bear snout
    bh.add_box((0, 0.15, 0.26), (0.058, 0.048, 0.032), mat_id=0, shade=(0.10, 0.09, 0.09))  # bear nose
    for sx in (-1, 1):
        bh.add_cone((sx * 0.052, 0.10, 0.22), (sx * 0.052, 0.045, 0.225), 0.018, 0.004, segs=4, mat_id=0, shade=(0.96, 0.94, 0.88))  # white bear fangs!
        bh.add_sphere((sx * 0.165, 0.275, 0.02), (0.062, 0.062, 0.048), segs=5, rings=3, mat_id=7, shade=(0.74, 0.64, 0.56))
    parts.append(bh)

    # 6. Morion (Saltmarsh League swept crescent boat-brim + bold tall fluted comb + brass rivet band!)
    mr = PartBuilder("helm_morion")
    rings = []
    for y, rx, rz, front_up in [(0.035, 0.245, 0.34, 0.095), (0.088, 0.192, 0.205, 0.0), (0.185, 0.172, 0.185, 0.0), (0.268, 0.048, 0.058, 0.0)]:
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            x = math.cos(a) * rx
            z = math.sin(a) * rz
            yy = y + front_up * abs(math.sin(a)) ** 1.4
            ring.append(Vector((x, yy, z)))
        rings.append(ring)
    mr.add_loft(rings, mat_id=4, ao_y=(0.03, 0.29, 0.78, 1.14))
    mr.add_loft([(0, 0.082, 0, 0.198, 0.210, segs), (0, 0.112, 0, 0.194, 0.206, segs)], mat_id=6)  # brass rivet band
    mr.add_box((0, 0.255, 0.0), (0.038, 0.175, 0.30), mat_id=4, shade=(1.10, 1.10, 1.14))  # tall dramatic morion comb
    parts.append(mr)

    # 7. Barbute (TRUE open-T/Y-faced Italian warhelm with sculpted forward cheek-guards + brass crest!)
    bb = PartBuilder("helm_barbute")
    # Back shell & steel crown (leaves face cleanly open!)
    bb.add_loft([
        (0, -0.14, -0.055, 0.188, 0.155, segs),
        (0, 0.06, -0.035, 0.192, 0.175, segs),
        (0, 0.18, 0.00, 0.178, 0.185, segs),
        (0, 0.268, 0.00, 0.058, 0.064, segs),
    ], mat_id=4, ao_y=(-0.14, 0.27, 0.78, 1.12))
    # Prominent brow arch + twin swept cheek-guards framing the open T-slit!
    bb.add_box((0, 0.11, 0.172), (0.28, 0.065, 0.055), mat_id=4, shade=(1.02, 1.04, 1.08))
    for sx in (-1, 1):
        bb.add_box((sx * 0.128, -0.035, 0.135), (0.088, 0.21, 0.095), mat_id=4, rot_euler=(0.0, sx * 0.35, -sx * 0.08), ao_y=(-0.14, 0.10, 0.80, 1.06))
    bb.add_box((0, 0.255, -0.01), (0.036, 0.12, 0.25), mat_id=6)  # gilded comb crest
    parts.append(bb)

    # 8. Steppe helm (Khaganate lush fur-trimmed brim + ear-flaps + tall ribbed conical steel helm + spire!)
    st = PartBuilder("helm_steppe")
    st.add_loft([(0, 0.02, 0, 0.228, 0.232, segs), (0, 0.08, 0, 0.238, 0.242, segs), (0, 0.12, 0, 0.210, 0.215, segs)], mat_id=7, shade=(0.82, 0.74, 0.64), ao_y=(0.02, 0.12, 0.80, 1.08))
    for sx in (-1, 1):
        st.add_box((sx * 0.175, -0.035, 0.01), (0.052, 0.13, 0.11), mat_id=7, shade=(0.78, 0.70, 0.60))  # fur cheek ear-flaps
    st.add_loft([(0, 0.11, 0, 0.178, 0.184, segs), (0, 0.24, 0, 0.124, 0.130, segs), (0, 0.36, 0, 0.024, 0.024, segs)], mat_id=4, ao_y=(0.11, 0.36, 0.80, 1.14))
    st.add_cone((0, 0.34, 0), (0, 0.43, 0), 0.032, 0.008, segs=5, mat_id=6)  # brass finial
    parts.append(st)

    # 9. Tug khan helm (gilded conical steppe helm + fur brim + arched streaming dark horsehair tug!)
    tg = PartBuilder("helm_tug")
    tg.add_loft([(0, 0.02, 0, 0.232, 0.236, segs), (0, 0.08, 0, 0.242, 0.246, segs), (0, 0.12, 0, 0.214, 0.218, segs)], mat_id=7, shade=(0.78, 0.70, 0.60))
    for sx in (-1, 1):
        tg.add_box((sx * 0.175, -0.035, 0.01), (0.052, 0.13, 0.11), mat_id=6)  # gilded cheek guards
    tg.add_loft([(0, 0.11, 0, 0.180, 0.186, segs), (0, 0.25, 0, 0.122, 0.128, segs), (0, 0.38, 0, 0.026, 0.026, segs)], mat_id=4, ao_y=(0.11, 0.38, 0.80, 1.14))
    tg.add_loft([(0, 0.11, 0, 0.186, 0.192, segs), (0, 0.145, 0, 0.182, 0.188, segs)], mat_id=6)
    tg.add_cone((0, 0.36, 0), (0, 0.46, -0.03), 0.036, 0.018, segs=5, mat_id=6)
    tg.add_cone((0, 0.45, -0.03), (0, 0.38, -0.22), 0.048, 0.038, segs=5, mat_id=12)
    tg.add_cone((0, 0.38, -0.22), (0, 0.16, -0.38), 0.038, 0.014, segs=5, mat_id=12)
    parts.append(tg)

    # 10. Spiked desert turban (Sunward Emirates tall gilded-ribbed kulah khud + folded white turban + neck cloth!)
    su = PartBuilder("helm_spike_turban")
    add_mail_coif(su, mat_id=5)  # iron mail aventail under turban
    su.add_loft([(0, 0.13, 0, 0.182, 0.186, segs), (0, 0.25, 0, 0.138, 0.142, segs), (0, 0.33, 0, 0.042, 0.042, segs)], mat_id=4, ao_y=(0.13, 0.34, 0.82, 1.14))
    su.add_cone((0, 0.31, 0), (0, 0.45, 0), 0.046, 0.008, segs=5, mat_id=6)  # tall gleaming brass spire
    su.add_loft([(0, 0.025, 0, 0.220, 0.226, segs), (0, 0.092, 0, 0.238, 0.242, segs), (0, 0.155, 0, 0.196, 0.200, segs)], mat_id=0, shade=(0.96, 0.94, 0.88), ao_y=(0.02, 0.16, 0.80, 1.06))
    su.add_box((0, 0.098, 0.205), (0.25, 0.055, 0.072), mat_id=0, shade=(0.98, 0.96, 0.92), rot_euler=(0, 0, 0.24))
    su.add_box((0, 0.098, 0.208), (0.25, 0.055, 0.072), mat_id=0, shade=(0.91, 0.89, 0.84), rot_euler=(0, 0, -0.24))
    su.add_box((0, 0.110, 0.242), (0.062, 0.062, 0.028), mat_id=6)  # front gilded jewel clasp
    parts.append(su)

    # 11. Wide-brimmed brigand / ranger hat with contrast band & jaunty side feather!
    br = PartBuilder("helm_brigand_hat")
    br.add_loft([(0, 0.045, 0, 0.32, 0.32, segs), (0, 0.085, 0, 0.185, 0.185, segs), (0, 0.215, 0, 0.152, 0.152, segs), (0, 0.245, 0, 0.082, 0.082, segs)], mat_id=7, ao_y=(0.04, 0.25, 0.78, 1.08))
    br.add_loft([(0, 0.082, 0, 0.192, 0.192, segs), (0, 0.120, 0, 0.188, 0.188, segs)], mat_id=1)  # faction hatband
    br.add_cone((-0.17, 0.10, -0.04), (-0.28, 0.34, -0.18), 0.036, 0.012, segs=4, mat_id=3)  # jaunty feather!
    parts.append(br)

    # 12. Sallet with brass crest & gothic bevor chin-guard!
    sc = PartBuilder("helm_sallet_crest")
    sc.add_loft([(0, 0.01, -0.06, 0.190, 0.235, segs), (0, 0.08, -0.02, 0.195, 0.212, segs), (0, 0.185, -0.01, 0.180, 0.190, segs), (0, 0.27, 0, 0.045, 0.052, segs)], mat_id=4, ao_y=(0.01, 0.28, 0.78, 1.12))
    sc.add_box((0, 0.068, 0.212), (0.21, 0.028, 0.026), mat_id=0, shade=(0.06, 0.06, 0.08))
    sc.add_box((0, -0.065, 0.135), (0.22, 0.125, 0.11), mat_id=4, shade=(0.94, 0.96, 1.0))  # steel bevor chin-guard
    sc.add_box((0, 0.265, -0.02), (0.038, 0.14, 0.26), mat_id=6)
    parts.append(sc)

    # 13. Dragon-crested warhelm (sculpted gilded wyrm head, jaws, horns & spine ridge!)
    dr = greathelm_base("helm_dragon")
    dr.add_box((0, 0.315, -0.02), (0.062, 0.145, 0.28), mat_id=6)
    dr.add_cone((0, 0.35, 0.10), (0, 0.39, 0.31), 0.058, 0.020, segs=4, mat_id=6)  # dragon snout
    for sx in (-1, 1):
        dr.add_cone((sx * 0.04, 0.37, 0.02), (sx * 0.13, 0.48, -0.14), 0.026, 0.006, segs=4, mat_id=6)  # swept dragon horns
    dr.add_cone((0, 0.32, -0.14), (0, 0.24, -0.32), 0.045, 0.010, segs=4, mat_id=6)  # dragon crest tail
    parts.append(dr)

    # 14. Armet (NEW — sleek rounded late-medieval closed knight helm with pointed bellows visor!)
    am = PartBuilder("helm_armet")
    am.add_loft([
        (0, -0.16, -0.01, 0.162, 0.168, segs),
        (0, -0.04, 0.01, 0.175, 0.192, segs),
        (0, 0.08, 0.01, 0.182, 0.198, segs),
        (0, 0.20, 0.00, 0.166, 0.176, segs),
        (0, 0.275, 0.00, 0.052, 0.058, segs),
    ], mat_id=4, ao_y=(-0.16, 0.28, 0.78, 1.12))
    # Angular protruding pointed visor prow + crisp dark eye-slit + high crown comb
    am.add_cone((0, 0.015, 0.15), (0, 0.035, 0.275), 0.115, 0.024, segs=4, mat_id=4, smooth=False, shade=(1.04, 1.06, 1.10))
    am.add_box((0, 0.080, 0.206), (0.22, 0.032, 0.030), mat_id=0, shade=(0.05, 0.05, 0.07))
    am.add_box((0, 0.235, -0.01), (0.034, 0.11, 0.24), mat_id=4, shade=(1.10, 1.10, 1.14))
    am.add_box((0, 0.080, 0.214), (0.032, 0.15, 0.020), mat_id=6)  # subtle gilt visor trim
    parts.append(am)

    # 15. Houndskull Bascinet (NEW — tall pointed skull + sharp pig-face visor snout + mail aventail!)
    bs = PartBuilder("helm_bascinet")
    add_mail_coif(bs, mat_id=5)
    bs.add_loft([
        (0, -0.08, -0.01, 0.176, 0.184, segs),
        (0, 0.08, 0.00, 0.180, 0.190, segs),
        (0, 0.21, -0.01, 0.145, 0.155, segs),
        (0, 0.315, -0.02, 0.032, 0.035, segs),  # iconic pointed bascinet apex
    ], mat_id=4, ao_y=(-0.08, 0.32, 0.78, 1.12))
    bs.add_cone((0, 0.01, 0.14), (0, 0.02, 0.305), 0.110, 0.022, segs=4, mat_id=4, smooth=False, shade=(1.05, 1.07, 1.12))
    bs.add_box((0, 0.076, 0.205), (0.20, 0.030, 0.028), mat_id=0, shade=(0.05, 0.05, 0.07))
    parts.append(bs)

    # 16. Spiked Iron Skullcap (NEW — 16 spike troops: domed iron helm + brass socket + tall top spike!)
    sp = PartBuilder("helm_spike")
    sp.add_loft([
        (0, 0.035, -0.02, 0.215, 0.225, segs),
        (0, 0.090, 0.0, 0.186, 0.192, segs),
        (0, 0.195, 0.0, 0.155, 0.160, segs),
        (0, 0.265, 0.0, 0.055, 0.055, segs),
    ], mat_id=5, ao_y=(0.03, 0.27, 0.78, 1.10))
    sp.add_loft([(0, 0.075, 0, 0.194, 0.198, segs), (0, 0.108, 0, 0.190, 0.195, segs)], mat_id=6)
    sp.add_cone((0, 0.255, 0), (0, 0.425, 0), 0.044, 0.008, segs=5, mat_id=4, shade=(1.08, 1.10, 1.14))  # bold iron spike!
    parts.append(sp)

    # 17. Chainmail Coif (NEW — snug riveted iron mail hood framing face!)
    cf = PartBuilder("helm_coif")
    add_mail_coif(cf, mat_id=5)
    cf.add_sphere((0, 0.06, -0.025), (0.180, 0.182, 0.172), segs=7, rings=5, mat_id=5, ao_y=(-0.10, 0.24, 0.78, 1.08))
    parts.append(cf)

    return parts


def build_phase2_bodies_and_limbs(PartBuilder):
    parts = []
    segs = 8

    # 1. Gilded knightly plate (sculpted steel cuirass, gilded gorget, gilded V-trim, pauldrons, tassets & surcoat!)
    pg = PartBuilder("torso_plate_gilded")
    pg.add_loft([
        (0, 0.66, 0.01, 0.228, 0.166, segs),
        (0, 0.86, 0.01, 0.192, 0.146, segs),
        (0, 1.12, 0.016, 0.252, 0.176, segs),
        (0, 1.28, 0.00, 0.242, 0.154, segs),
        (0, 1.35, 0.00, 0.152, 0.130, segs),
    ], mat_id=4, ao_y=(0.66, 1.36, 0.80, 1.08))
    pg.add_loft([(0, 1.33, 0, 0.156, 0.134, segs), (0, 1.41, 0, 0.120, 0.112, segs)], mat_id=6)  # gilded gorget!
    pg.add_loft([(0, 0.54, 0, 0.232, 0.170, segs), (0, 0.67, 0, 0.218, 0.158, segs)], mat_id=5)  # mail skirt hem
    for sx in (-1, 1):
        pg.add_box((sx * 0.230, 1.26, 0.015), (0.115, 0.12, 0.205), mat_id=6, rot_euler=(0.0, 0.0, -sx * 0.24))  # gilded shoulder ridges
        pg.add_box((sx * 0.170, 0.69, 0.095), (0.14, 0.21, 0.050), mat_id=6, rot_euler=(-0.10, sx * 0.42, sx * 0.10))  # gilded tassets
    pg.add_box((0, 0.82, 0.170), (0.22, 0.48, 0.026), mat_id=1)
    pg.add_box((0, 0.82, -0.158), (0.22, 0.48, 0.026), mat_id=1)
    pg.add_box((0, 1.16, 0.180), (0.26, 0.055, 0.024), mat_id=6)  # gilded chest bar
    pg.add_box((0, 0.85, 0.006), (0.43, 0.065, 0.35), mat_id=6)   # gilded belt
    parts.append(pg)

    # 2. Brigandine / Coat of Plates (NEW — ref-c soldier #2: faction studded doublet + steel spaulders + chest straps + mail skirt!)
    bg = PartBuilder("torso_brigandine")
    bg.add_loft([(0, 0.46, 0, 0.238, 0.176, segs), (0, 0.60, 0, 0.226, 0.168, segs)], mat_id=5, ao_y=(0.46, 0.60, 0.72, 0.96))  # mail hem
    bg.add_loft([
        (0, 0.58, 0.0, 0.238, 0.176, segs),
        (0, 0.84, 0.0, 0.194, 0.148, segs),
        (0, 1.12, 0.012, 0.244, 0.170, segs),
        (0, 1.28, 0.0, 0.234, 0.152, segs),
        (0, 1.38, 0.0, 0.128, 0.116, segs),
    ], mat_id=1, ao_y=(0.58, 1.38, 0.76, 1.06))
    # Steel gorget + layered steel shoulder spaulders + central chest buckle straps!
    bg.add_loft([(0, 1.32, 0, 0.155, 0.136, segs), (0, 1.40, 0, 0.122, 0.112, segs)], mat_id=4)
    for sx in (-1, 1):
        bg.add_box((sx * 0.225, 1.25, 0.01), (0.115, 0.115, 0.195), mat_id=4, rot_euler=(0, 0, -sx * 0.26), shade=(1.04, 1.06, 1.10))
    for cy in (1.02, 1.14, 1.24):
        bg.add_box((0, cy, 0.172), (0.14, 0.032, 0.024), mat_id=4, shade=(0.92, 0.94, 0.98))
    bg.add_box((0, 0.84, 0.0), (0.42, 0.062, 0.335), mat_id=7)
    bg.add_box((0, 0.84, 0.174), (0.072, 0.064, 0.024), mat_id=6)
    parts.append(bg)

    # 3. Breastplate over Gambeson (NEW — gleaming steel cuirass + gorget + spaulders over faction gambeson skirt!)
    bp = PartBuilder("torso_breastplate")
    bp.add_loft([(0, 0.52, 0, 0.242, 0.178, segs), (0, 0.84, 0, 0.198, 0.150, segs)], mat_id=1, ao_y=(0.52, 0.84, 0.74, 1.02))
    bp.add_loft([
        (0, 0.82, 0.008, 0.200, 0.154, segs),
        (0, 1.11, 0.018, 0.250, 0.176, segs),
        (0, 1.28, 0.000, 0.238, 0.152, segs),
        (0, 1.39, 0.000, 0.120, 0.110, segs),
    ], mat_id=4, ao_y=(0.82, 1.39, 0.80, 1.10))
    for sx in (-1, 1):
        bp.add_box((sx * 0.228, 1.25, 0.01), (0.11, 0.11, 0.19), mat_id=4, rot_euler=(0, 0, -sx * 0.24))
    bp.add_box((0, 0.84, 0.005), (0.425, 0.062, 0.34), mat_id=7)
    bp.add_box((0, 0.84, 0.178), (0.072, 0.064, 0.024), mat_id=6)
    parts.append(bp)

    # 4. Lamellar armor (Ref-c soldier #3: white linen scarf collar + tiered golden-brass cuirass, spaulders & hip tassets over faction skirt!)
    lm = PartBuilder("torso_lamellar")
    lm.add_loft([
        (0, 0.46, 0, 0.252, 0.190, segs),
        (0, 0.76, 0, 0.214, 0.164, segs),
    ], mat_id=1, ao_y=(0.46, 0.78, 0.74, 1.04))  # rich faction tunic skirt below lamellar
    # Tiered brass lamellar hip tassets flaring cleanly over waist!
    lm.add_loft([
        (0, 0.62, 0, 0.246, 0.184, segs),
        (0, 0.84, 0, 0.212, 0.162, segs),
    ], mat_id=6, ao_y=(0.62, 0.84, 0.76, 1.04))
    # Stepped brass lamellar chest cuirass + flared lamellar shoulder guards
    lm.add_loft([
        (0, 0.82, 0, 0.215, 0.164, segs),
        (0, 1.12, 0.012, 0.246, 0.174, segs),
        (0, 1.28, 0, 0.234, 0.154, segs),
    ], mat_id=6, ao_y=(0.82, 1.30, 0.80, 1.10))
    for sx in (-1, 1):
        lm.add_box((sx * 0.235, 1.22, 0.01), (0.12, 0.15, 0.19), mat_id=6, rot_euler=(0, 0, -sx * 0.28), ao_y=(1.12, 1.32, 0.82, 1.08))
    # Crisp white desert linen collar scarf!
    lm.add_loft([(0, 1.28, 0, 0.178, 0.154, segs), (0, 1.39, 0, 0.132, 0.122, segs)], mat_id=0, shade=(0.95, 0.93, 0.88))
    lm.add_box((0, 0.84, 0), (0.44, 0.068, 0.35), mat_id=2)   # dark sash
    lm.add_box((0, 0.84, 0.182), (0.075, 0.072, 0.026), mat_id=6)  # brass medallion clasp
    parts.append(lm)

    # 5. Scale hauberk (iron scales + layered scale shoulder guards + diagonal faction baldric + brass hem!)
    sc = PartBuilder("torso_scale")
    sc.add_loft([
        (0, 0.54, 0, 0.242, 0.178, segs),
        (0, 0.84, 0, 0.202, 0.154, segs),
        (0, 1.12, 0.012, 0.244, 0.172, segs),
        (0, 1.28, 0, 0.234, 0.152, segs),
        (0, 1.38, 0, 0.122, 0.110, segs),
    ], mat_id=5, ao_y=(0.54, 1.38, 0.78, 1.10))
    for sx in (-1, 1):
        sc.add_box((sx * 0.232, 1.23, 0.01), (0.115, 0.14, 0.19), mat_id=5, rot_euler=(0, 0, -sx * 0.28))
    sc.add_box((0, 1.08, 0.168), (0.085, 0.44, 0.024), mat_id=1, rot_euler=(0, 0, 0.55))  # diagonal faction baldric
    sc.add_box((0, 0.84, 0), (0.425, 0.064, 0.335), mat_id=1)
    sc.add_box((0, 0.55, 0), (0.465, 0.052, 0.345), mat_id=6)
    parts.append(sc)

    # 6. Fur-mantled northern warlord/clansman torso (shaggy asymmetric pelt shoulders, crossed chest straps & iron bosses!)
    fr = PartBuilder("torso_fur")
    fr.add_loft([
        (0, 0.56, 0, 0.234, 0.174, segs),
        (0, 0.84, 0, 0.198, 0.150, segs),
        (0, 1.12, 0.01, 0.240, 0.166, segs),
        (0, 1.28, 0, 0.228, 0.148, segs),
    ], mat_id=1, ao_y=(0.56, 1.28, 0.76, 1.06))
    # Heavy sculpted shaggy fur pelt mantle draped over shoulders & collar!
    fr.add_loft([
        (0, 1.10, -0.025, 0.295, 0.208, segs),
        (0, 1.26, -0.015, 0.318, 0.222, segs),
        (0, 1.38, 0.000, 0.198, 0.162, segs),
    ], mat_id=7, shade=(0.76, 0.68, 0.58), ao_y=(1.10, 1.38, 0.80, 1.12))
    for sx in (-1, 1):
        fr.add_sphere((sx * 0.26, 1.22, -0.02), (0.11, 0.13, 0.14), segs=5, rings=3, mat_id=7, shade=(0.70, 0.62, 0.52))
    # Crossed leather chest baldric + iron boss & heavy fur war-belt
    fr.add_box((0, 1.04, 0.168), (0.070, 0.42, 0.024), mat_id=7, shade=(0.68, 0.54, 0.42), rot_euler=(0, 0, 0.52))
    fr.add_box((0, 1.04, 0.170), (0.070, 0.42, 0.024), mat_id=7, shade=(0.68, 0.54, 0.42), rot_euler=(0, 0, -0.52))
    fr.add_box((0, 1.04, 0.184), (0.072, 0.072, 0.026), mat_id=4)  # iron chest boss
    fr.add_box((0, 0.84, 0), (0.42, 0.068, 0.33), mat_id=7)
    fr.add_box((0, 0.84, 0.172), (0.075, 0.068, 0.024), mat_id=6)
    parts.append(fr)

    # 7. Steppe deel (Ref-c rider #4: flared riding coat with plush fur collar, diagonal fur V-lapel, fur hem & bright sash!)
    dl = PartBuilder("torso_deel")
    dl.add_loft([
        (0, 0.42, 0, 0.268, 0.204, segs),
        (0, 0.82, 0, 0.200, 0.156, segs),
        (0, 1.12, 0.01, 0.238, 0.166, segs),
        (0, 1.29, 0, 0.224, 0.148, segs),
    ], mat_id=1, ao_y=(0.42, 1.30, 0.74, 1.06))
    # Plush fur collar + diagonal overlapping fur lapel trim + fur hem!
    dl.add_loft([(0, 1.25, 0, 0.222, 0.170, segs), (0, 1.39, 0, 0.152, 0.138, segs)], mat_id=7, shade=(0.86, 0.78, 0.66), ao_y=(1.25, 1.39, 0.82, 1.10))
    dl.add_box((0.03, 1.08, 0.166), (0.068, 0.40, 0.028), mat_id=7, shade=(0.86, 0.78, 0.66), rot_euler=(0, 0, 0.34))  # diagonal fur lapel!
    dl.add_loft([(0, 0.40, 0, 0.274, 0.210, segs), (0, 0.46, 0, 0.268, 0.205, segs)], mat_id=7, shade=(0.84, 0.76, 0.64))
    dl.add_box((0, 0.84, 0), (0.425, 0.080, 0.335), mat_id=3)  # bright steppe sash
    dl.add_box((0, 0.84, 0.176), (0.070, 0.065, 0.024), mat_id=6)
    parts.append(dl)

    # 8. Desert kaftan / burnous (Sunward flowing open-front layered coat over inner tunic + scarf collar + sash!)
    kf = PartBuilder("torso_kaftan")
    kf.add_loft([
        (0, 0.38, 0, 0.272, 0.212, segs),
        (0, 0.82, 0, 0.200, 0.156, segs),
        (0, 1.14, 0.01, 0.236, 0.164, segs),
        (0, 1.34, 0, 0.158, 0.128, segs),
    ], mat_id=1, ao_y=(0.38, 1.34, 0.76, 1.08))
    # Crisp white linen inner tunic panel & shoulder shawl!
    kf.add_box((0, 0.90, 0.164), (0.14, 0.64, 0.026), mat_id=0, shade=(0.95, 0.93, 0.88))
    kf.add_loft([(0, 1.24, 0, 0.238, 0.175, segs), (0, 1.39, 0, 0.142, 0.128, segs)], mat_id=0, shade=(0.94, 0.92, 0.86))
    kf.add_box((0, 0.84, 0), (0.425, 0.075, 0.335), mat_id=3)  # golden/trim waist sash
    kf.add_box((0, 0.84, 0.178), (0.068, 0.066, 0.024), mat_id=6)
    parts.append(kf)

    # 9. Skeleton ribcage + collarbones + spine + pelvis (bleached bone)
    bn = PartBuilder("torso_bones")
    bn.add_cone((0, 0.70, -0.02), (0, 1.36, -0.02), 0.048, 0.044, segs=5, mat_id=9, shade=(0.82, 0.80, 0.75))
    bn.add_box((0, 0.72, 0.0), (0.31, 0.12, 0.18), mat_id=9, shade=(0.86, 0.84, 0.78))
    bn.add_box((0, 1.12, 0.11), (0.065, 0.28, 0.040), mat_id=9, shade=(1.02, 1.00, 0.96))  # sternum
    bn.add_box((0, 1.31, 0.05), (0.42, 0.048, 0.065), mat_id=9, shade=(1.02, 1.00, 0.96))  # clavicles
    for ry in (0.94, 1.06, 1.18, 1.26):
        w = 0.36 if ry > 1.0 else 0.30
        bn.add_box((0, ry, 0.02), (w, 0.052, 0.20), mat_id=9, shade=(0.98, 0.96, 0.92))
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
    da.add_cone((0, -0.58, 0), (0, 1.38, 0), 0.034, 0.034, segs=5, mat_id=8)
    da.add_box((0, 1.24, 0.14), (0.042, 0.34, 0.24), mat_id=4, shade=(1.06, 1.08, 1.12))
    parts.append(da)

    # Glaive
    gl = PartBuilder("weapon_glaive")
    gl.add_cone((0, -0.68, 0), (0, 1.48, 0), 0.034, 0.032, segs=5, mat_id=8)
    gl.add_loft([(0, 1.46, 0, 0.022, 0.068, 4), (0, 1.88, 0.04, 0.018, 0.082, 4), (0, 2.15, 0.10, 0.005, 0.010, 4)], mat_id=4, smooth=False, shade=(1.08, 1.08, 1.12))
    parts.append(gl)

    # Billhook
    bh = PartBuilder("weapon_billhook")
    bh.add_cone((0, -0.68, 0), (0, 1.56, 0), 0.034, 0.032, segs=5, mat_id=8)
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

    # Pavise (tall crossbowman wall shield, clean heraldic base with dark iron border)
    pv = PartBuilder("shield_pavise")
    pv.add_box((0, -0.06, 0.026), (0.58, 1.04, 0.034), mat_id=5, shade=(0.86, 0.88, 0.92))
    pv.add_box((0, -0.06, 0.050), (0.51, 0.96, 0.042), mat_id=1, ao_y=(-0.58, 0.46, 0.78, 1.08))
    parts.append(pv)

    # Tower shield (clean heraldic base with brass boss & rim)
    tw = PartBuilder("shield_tower")
    tw.add_box((0, -0.02, 0.026), (0.54, 0.94, 0.036), mat_id=6, shade=(0.88, 0.86, 0.82))
    tw.add_box((0, -0.02, 0.052), (0.47, 0.86, 0.044), mat_id=1, ao_y=(-0.48, 0.44, 0.80, 1.08))
    tw.add_sphere((0, -0.02, 0.082), (0.075, 0.075, 0.035), segs=6, rings=3, mat_id=6)
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


def build_phase3_emblems_and_props(PartBuilder):
    """Phase 3: all 8 heraldic relief emblems (Style.Emblem) + quiver, arrow, javelin, web snare, 3D banner pole."""
    parts = []
    t = 0.018  # crisp relief depth along +Z

    # 1. Chevron (^ — Azure)
    em = PartBuilder("emblem_chevron")
    em.add_box((-0.090, -0.02, 0), (0.21, 0.075, t), rot_euler=(0, 0, math.radians(34)), mat_id=3)
    em.add_box(( 0.090, -0.02, 0), (0.21, 0.075, t), rot_euler=(0, 0, math.radians(-34)), mat_id=3)
    parts.append(em)

    # 2. Saltire (X — Crimson)
    em = PartBuilder("emblem_saltire")
    em.add_box((0, 0, 0), (0.40, 0.075, t), rot_euler=(0, 0, math.radians(48)), mat_id=3)
    em.add_box((0, 0, 0), (0.40, 0.075, t), rot_euler=(0, 0, math.radians(-48)), mat_id=3)
    parts.append(em)

    # 3. Lozenge (diamond — Gold / Player!)
    em = PartBuilder("emblem_lozenge")
    em.add_box((0, 0, 0), (0.22, 0.22, t), rot_euler=(0, 0, math.radians(45)), mat_id=3)
    parts.append(em)

    # 4. Bend (diagonal sash — Northmarch)
    em = PartBuilder("emblem_bend")
    em.add_box((0, 0, 0), (0.42, 0.095, t), rot_euler=(0, 0, math.radians(-48)), mat_id=3)
    parts.append(em)

    # 5. Cross (+ — Order of the Ashen Sun)
    em = PartBuilder("emblem_cross")
    em.add_box((0, 0.00, 0), (0.088, 0.42, t), mat_id=3)
    em.add_box((0, 0.05, 0), (0.34, 0.088, t), mat_id=3)
    parts.append(em)

    # 6. Pale (vertical stripe — Saltmarsh)
    em = PartBuilder("emblem_pale")
    em.add_box((0, 0, 0), (0.11, 0.44, t), mat_id=3)
    parts.append(em)

    # 7. Fess (horizontal bar — Khaganate)
    em = PartBuilder("emblem_fess")
    em.add_box((0, 0.02, 0), (0.36, 0.11, t), mat_id=3)
    parts.append(em)

    # 8. Sun (radiant burst — Sunward Sultanate)
    em = PartBuilder("emblem_sun")
    em.add_loft_z([(0, 0, -t * 0.5, 0.105, 0.105, 8), (0, 0, t * 0.5, 0.105, 0.105, 8)], mat_id=3)
    for k in range(4):
        ang = k * (math.pi / 4.0)
        em.add_box((0, 0, 0), (0.34, 0.042, t * 0.9), rot_euler=(0, 0, ang), mat_id=3)
    parts.append(em)

    # Quiver (worn on archer/crossbowman back, leather tube + 3 feathered arrow nocks)
    qv = PartBuilder("quiver")
    qv.add_cone((0.02, -0.20, 0.0), (-0.06, 0.22, 0.0), 0.055, 0.072, segs=6, mat_id=7, ao_y=(-0.20, 0.22, 0.78, 1.06))
    qv.add_box((-0.02, 0.02, 0.0), (0.14, 0.05, 0.14), mat_id=3)
    for dx, dz in [(-0.03, 0.02), (0.02, -0.02), (0.0, 0.03)]:
        qv.add_cone((-0.06 + dx, 0.18, dz), (-0.08 + dx * 1.4, 0.34, dz * 1.4), 0.012, 0.010, segs=3, mat_id=8)
        qv.add_box((-0.08 + dx * 1.4, 0.32, dz * 1.4), (0.042, 0.065, 0.018), mat_id=1)
    parts.append(qv)

    # 3D Arrow / Bolt (sleek shaft 0.0 -> 0.74m along +Y, narrow faction fletching at base, crisp steel bodkin tip)
    ar = PartBuilder("weapon_arrow")
    ar.add_cone((0, 0.0, 0), (0, 0.62, 0), 0.015, 0.013, segs=4, mat_id=8)
    ar.add_cone((0, 0.60, 0), (0, 0.74, 0), 0.025, 0.004, segs=4, mat_id=4, smooth=False)
    ar.add_box((0, 0.06, 0), (0.046, 0.10, 0.010), mat_id=1)
    ar.add_box((0, 0.06, 0), (0.010, 0.10, 0.046), mat_id=3)
    parts.append(ar)

    # 3D Javelin (length 1.30m along +Y: -0.55..+0.75)
    jv = PartBuilder("weapon_javelin")
    jv.add_cone((0, -0.55, 0), (0, 0.54, 0), 0.020, 0.018, segs=4, mat_id=8)
    jv.add_cone((0, 0.52, 0), (0, 0.76, 0), 0.040, 0.005, segs=4, mat_id=4, smooth=False)
    jv.add_box((0, -0.02, 0), (0.046, 0.16, 0.046), mat_id=7)
    parts.append(jv)

    # Spider web snare (criss-cross sticky pale silk cords wrapping caught soldiers)
    wb = PartBuilder("web_snare")
    for yy in (0.45, 0.85, 1.20):
        wb.add_loft([(0, yy - 0.03, 0, 0.36, 0.34, 7), (0, yy + 0.03, 0, 0.36, 0.34, 7)], mat_id=0, shade=(0.94, 0.95, 0.92), cap_bot=False, cap_top=False)
    for ang in (0.4, 2.2, 4.1):
        cx, cz = math.cos(ang) * 0.34, math.sin(ang) * 0.34
        wb.add_cone((cx, 0.35, cz), (-cx, 1.32, -cz), 0.024, 0.024, segs=3, mat_id=0, shade=(0.92, 0.94, 0.90))
    parts.append(wb)

    # 3D Company Banner Pole (height 3.35m along +Y, brass spearhead finial + crossbar)
    bp = PartBuilder("banner_pole")
    bp.add_cone((0, 0.0, 0), (0, 3.25, 0), 0.036, 0.028, segs=5, mat_id=8, ao_y=(0.0, 3.25, 0.80, 1.06))
    bp.add_cone((0, 3.22, 0), (0, 3.55, 0), 0.068, 0.008, segs=4, mat_id=6, smooth=False)
    bp.add_box((0.42, 3.16, 0), (0.86, 0.042, 0.042), mat_id=6)
    parts.append(bp)

    return parts


def build_all(PartBuilder):
    parts = []
    parts.extend(build_phase2_helms(PartBuilder))
    parts.extend(build_phase2_bodies_and_limbs(PartBuilder))
    parts.extend(build_phase2_weapons_and_shields(PartBuilder))
    parts.extend(build_phase2_mounts(PartBuilder))
    parts.extend(build_phase2_race_heads(PartBuilder))
    parts.extend(build_phase2_beasts(PartBuilder))
    parts.extend(build_phase3_emblems_and_props(PartBuilder))
    return parts
