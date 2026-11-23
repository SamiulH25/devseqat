import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildBeasts(root) {
  // Beast material convention (UnitMatTable.FillBeast in Mesh3D.cs):
  //   M.coat (11) = body, M.coatDark (12) = accent, M.dark (13) = dark (eyes/claws/nose), M.trim (3) = fangs/bright accent

  // ---------- WOLF / HOUND / WARG (4 parts) ----------
  addPart(root, 'wolf_body', (mesh) => {
    // Lean muscular canine torso along Z (-0.56..+0.56)
    const body = loft(V(0, 0.04, -0.55), V(0, 0.06, 0.55), [
      [0.00, 0.09, 0.09],
      [0.18, 0.19, 0.19, 0, 0.02],
      [0.48, 0.16, 0.17, 0, -0.01],
      [0.78, 0.21, 0.23, 0, 0.04],
      [1.00, 0.11, 0.12, 0, 0.02],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.17, 0.27, 0.78, 1.0), M.coat);
    // Shaggy shoulder ruff
    const ruff = loft(V(0, 0.14, 0.16), V(0, 0.16, 0.52), [
      [0, 0.18, 0.12], [0.5, 0.21, 0.14], [1, 0.13, 0.10],
    ], { segs: 7, fwd: V(0, 1, 0) });
    mesh(ruff, M.coatDark);
  });

  addPart(root, 'wolf_head', (mesh) => {
    const skull = loft(V(0, 0.04, -0.06), V(0, 0.00, 0.51), [
      [0.00, 0.135, 0.125],
      [0.32, 0.145, 0.135],
      [0.64, 0.090, 0.088],
      [0.92, 0.064, 0.062],
      [1.00, 0.042, 0.042],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.10, 0.18, 0.82, 1.0), M.coat);
    // Tall pointed ears
    for (const sx of [-1, 1]) {
      const ear = shadeVerts(new THREE.ConeGeometry(0.042, 0.14, 5), () => 0.90);
      mesh(ear, M.coatDark, [sx * 0.085, 0.20, 0.05], [0.16, 0, -sx * 0.18]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.022, 5, 4), () => 1.0), M.trim, [sx * 0.095, 0.068, 0.22]);
      // Fangs
      const fang = shadeVerts(new THREE.ConeGeometry(0.012, 0.048, 4), () => 1.0);
      mesh(fang, M.neutral, [sx * 0.044, -0.055, 0.42], [Math.PI, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.032, 6, 5), () => 0.15), M.dark, [0, 0.01, 0.51]);
  });

  addPart(root, 'wolf_leg', (mesh) => {
    const leg = loft(V(0, -0.06, 0), V(0, 0.92, 0), [
      [0.0, 0.068, 0.078], [0.45, 0.052, 0.060], [1.0, 0.042, 0.048],
    ], { segs: 6 });
    mesh(heightShade(leg, -0.06, 0.92, 0.78, 1.0), M.coat);
    const paw = loft(V(0, 0.94, -0.04), V(0, 0.96, 0.105), [[0, 0.046, 0.038], [1, 0.032, 0.026]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(paw, M.dark);
  });

  addPart(root, 'wolf_tail', (mesh) => {
    const tail = loft(V(0, -0.04, 0), V(0, 1.04, 0), [
      [0.0, 0.042, 0.042], [0.35, 0.084, 0.088], [0.75, 0.074, 0.078], [1.0, 0.022, 0.022],
    ], { segs: 6 });
    mesh(heightShade(tail, -0.04, 1.04, 0.76, 1.0), M.coatDark);
  });

  // ---------- BEAR (3 parts) ----------
  addPart(root, 'bear_body', (mesh) => {
    // Massive hulking bear torso with high shoulder hump (Y up to 0.52, Z -0.65..+0.62)
    const body = loft(V(0, 0.05, -0.64), V(0, 0.08, 0.61), [
      [0.00, 0.16, 0.16],
      [0.18, 0.34, 0.32, 0, 0.04],
      [0.48, 0.36, 0.35, 0, 0.06],
      [0.76, 0.37, 0.41, 0, 0.12],
      [0.92, 0.30, 0.32, 0, 0.08],
      [1.00, 0.15, 0.16, 0, 0.04],
    ], { segs: 9, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.31, 0.52, 0.76, 1.0), M.coat);
    // Dark shaggy back ridge
    const ridge = loft(V(0, 0.34, -0.24), V(0, 0.40, 0.42), [
      [0, 0.14, 0.10], [0.6, 0.18, 0.13], [1, 0.12, 0.09],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(ridge, M.coatDark);
  });

  addPart(root, 'bear_head', (mesh) => {
    const skull = loft(V(0, 0.03, -0.07), V(0, -0.03, 0.56), [
      [0.00, 0.21, 0.20],
      [0.35, 0.225, 0.215],
      [0.68, 0.155, 0.145],
      [0.92, 0.115, 0.105],
      [1.00, 0.082, 0.075],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.20, 0.26, 0.80, 1.0), M.coat);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.068, 6, 5), () => 0.88), M.coatDark, [sx * 0.155, 0.215, 0.06]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.028, 5, 4), () => 0.18), M.dark, [sx * 0.142, 0.085, 0.28]);
      const fang = shadeVerts(new THREE.ConeGeometry(0.018, 0.062, 4), () => 1.0);
      mesh(fang, M.neutral, [sx * 0.065, -0.095, 0.44], [Math.PI, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.052, 6, 5), () => 0.15), M.dark, [0, -0.01, 0.56]);
  });

  addPart(root, 'bear_leg', (mesh) => {
    const leg = loft(V(0, -0.08, 0), V(0, 0.88, 0), [
      [0.0, 0.145, 0.145], [0.5, 0.125, 0.130], [1.0, 0.105, 0.115],
    ], { segs: 7 });
    mesh(heightShade(leg, -0.08, 0.88, 0.78, 1.0), M.coat);
    const paw = loft(V(0, 0.92, -0.08), V(0, 0.94, 0.20), [[0, 0.115, 0.078], [1, 0.085, 0.055]], { segs: 7, fwd: V(0, 1, 0) });
    mesh(paw, M.coatDark);
    for (const cx of [-0.055, 0, 0.055]) {
      const claw = shadeVerts(new THREE.ConeGeometry(0.014, 0.055, 4), () => 0.95);
      mesh(claw, M.dark, [cx, 0.95, 0.205], [Math.PI / 2, 0, 0]);
    }
  });

  // ---------- GIANT SPIDER (3 parts) ----------
  addPart(root, 'spider_abdomen', (mesh) => {
    // Bulbous glossy arachnid abdomen with accent stripes (X±0.32, Y -0.26..0.34, Z±0.42)
    const abd = loft(V(0, 0.04, -0.42), V(0, 0.04, 0.42), [
      [0.00, 0.06, 0.06],
      [0.18, 0.26, 0.24],
      [0.50, 0.32, 0.30],
      [0.82, 0.25, 0.23],
      [1.00, 0.08, 0.08],
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(abd, -0.26, 0.34, 0.76, 1.0), M.coat);
    // Dorsal hourglass / chevron stripes
    for (const z of [-0.14, 0.08]) {
      const band = shadeVerts(new THREE.TorusGeometry(0.285, 0.028, 5, 10), () => 0.96);
      mesh(band, M.coatDark, [0, 0.05, z]);
    }
  });

  addPart(root, 'spider_head', (mesh) => {
    // Cephalothorax (X±0.21, Y -0.18..0.20, Z -0.14..0.35)
    const ceph = shadeVerts(new THREE.SphereGeometry(0.195, 8, 6), () => 0.90);
    ceph.scale(1.05, 0.88, 1.18);
    mesh(ceph, M.coat, [0, 0.01, 0.04]);
    // 4 gleaming cluster eyes on front (+Z)
    for (const [ex, ey, er] of [[-0.065, 0.065, 0.030], [0.065, 0.065, 0.030], [-0.12, 0.035, 0.020], [0.12, 0.035, 0.020]]) {
      mesh(shadeVerts(new THREE.SphereGeometry(er, 5, 4), () => 1.0), M.coatDark, [ex, ey, 0.235]);
    }
    // Curved venomous chelicerae / fangs (+Z)
    for (const sx of [-1, 1]) {
      const fang = loft(V(sx * 0.075, -0.03, 0.20), V(sx * 0.045, -0.15, 0.345), [
        [0, 0.044, 0.044], [0.55, 0.032, 0.032], [1, 0.010, 0.010],
      ], { segs: 5 });
      mesh(fang, M.dark);
    }
  });

  addPart(root, 'spider_leg', (mesh) => {
    const seg = loft(V(0, -0.04, 0), V(0, 1.02, 0), [
      [0.0, 0.046, 0.046], [0.5, 0.036, 0.036], [1.0, 0.024, 0.024],
    ], { segs: 6 });
    mesh(heightShade(seg, -0.04, 1.02, 0.78, 1.0), M.dark);
  });

  // ---------- WYVERN (7 parts) ----------
  addPart(root, 'wyvern_body', (mesh) => {
    // Sleek armored drake torso (X±0.27, Y -0.25..0.42, Z -0.62..+0.58)
    const body = loft(V(0, 0.04, -0.61), V(0, 0.08, 0.57), [
      [0.00, 0.11, 0.12],
      [0.20, 0.21, 0.22],
      [0.52, 0.27, 0.29],
      [0.82, 0.24, 0.25],
      [1.00, 0.14, 0.15],
    ], { segs: 9, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.25, 0.36, 0.78, 1.0), M.coat);
    // Dorsal ridge spines along Y top
    for (const z of [-0.32, -0.08, 0.16, 0.38]) {
      const sp = shadeVerts(new THREE.ConeGeometry(0.036, 0.15, 4), () => 0.96);
      mesh(sp, M.coatDark, [0, 0.34, z], [-0.35, 0, 0]);
    }
  });

  addPart(root, 'wyvern_neck', (mesh) => {
    const neck = loft(V(0, -0.08, 0), V(0, 1.06, 0), [
      [0.0, 0.155, 0.155], [0.5, 0.130, 0.130], [1.0, 0.110, 0.110],
    ], { segs: 7 });
    mesh(heightShade(neck, -0.08, 1.06, 0.80, 1.0), M.coat);
  });

  addPart(root, 'wyvern_head', (mesh) => {
    // Reptilian horned dragon skull (snout +Z 0.54, swept horns -Z -0.22)
    const skull = loft(V(0, 0.05, -0.08), V(0, 0.01, 0.53), [
      [0.00, 0.155, 0.135],
      [0.32, 0.165, 0.145],
      [0.65, 0.110, 0.100],
      [0.92, 0.075, 0.068],
      [1.00, 0.048, 0.044],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.11, 0.22, 0.82, 1.0), M.coat);
    for (const sx of [-1, 1]) {
      // Swept-back drake horns (-Z, +Y)
      const horn = loft(V(sx * 0.095, 0.14, 0.02), V(sx * 0.18, 0.28, -0.22), [
        [0, 0.036, 0.036], [0.5, 0.024, 0.024], [1, 0.008, 0.008],
      ], { segs: 5 });
      mesh(horn, M.dark);
      // Glowing eye & fangs
      mesh(shadeVerts(new THREE.SphereGeometry(0.024, 5, 4), () => 1.0), M.trim, [sx * 0.12, 0.09, 0.20]);
      const fang = shadeVerts(new THREE.ConeGeometry(0.014, 0.055, 4), () => 1.0);
      mesh(fang, M.neutral, [sx * 0.052, -0.065, 0.44], [Math.PI, 0, 0]);
    }
  });

  addPart(root, 'wyvern_wing', (mesh) => {
    // Bat-like leathery wing extending along +X (0..1.02) with scalloped membrane trailing -Z (-0.62..0.14)
    const spar = loft(V(0.0, 0.02, 0.02), V(1.00, 0.04, 0.10), [
      [0.0, 0.045, 0.045], [0.45, 0.036, 0.036], [1.0, 0.016, 0.016],
    ], { segs: 5 });
    mesh(spar, M.coat);
    // 3 wing-finger ribs radiating back toward -Z
    const fingers = [
      [V(0.45, 0.03, 0.06), V(0.32, 0.0, -0.58)],
      [V(0.72, 0.04, 0.08), V(0.68, 0.0, -0.56)],
      [V(0.98, 0.04, 0.10), V(1.01, 0.0, -0.44)],
    ];
    for (const [a, b] of fingers) {
      mesh(loft(a, b, [[0, 0.018, 0.018], [1, 0.008, 0.008]], { segs: 4 }), M.coatDark);
    }
    // Thin wing membrane sheet (double-sided M.coatDark)
    const pts = [
      [0.0, 0.01, 0.02], [0.45, 0.02, 0.06], [0.72, 0.03, 0.08], [1.00, 0.03, 0.10],
      [0.0, -0.01, -0.48], [0.32, -0.01, -0.60], [0.68, -0.01, -0.56], [1.01, -0.01, -0.44],
    ];
    const pos = pts.flat();
    const idx = [
      0, 1, 5, 0, 5, 4,
      1, 2, 6, 1, 6, 5,
      2, 3, 7, 2, 7, 6,
    ];
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    mesh(shadeVerts(g, (x) => 0.82 + 0.16 * x), M.coatDark);
  });

  addPart(root, 'wyvern_leg', (mesh) => {
    const leg = loft(V(0, -0.06, 0), V(0, 0.90, 0), [
      [0.0, 0.080, 0.085], [0.5, 0.062, 0.068], [1.0, 0.048, 0.052],
    ], { segs: 6 });
    mesh(heightShade(leg, -0.06, 0.90, 0.78, 1.0), M.coat);
    for (const cx of [-0.042, 0, 0.042]) {
      const talon = loft(V(cx, 0.92, 0.0), V(cx * 1.3, 1.01, 0.155), [[0, 0.022, 0.022], [1, 0.006, 0.006]], { segs: 4 });
      mesh(talon, M.dark);
    }
  });

  addPart(root, 'wyvern_tail', (mesh) => {
    const seg = loft(V(0, -0.06, 0), V(0, 1.06, 0), [
      [0.0, 0.095, 0.095], [0.5, 0.075, 0.075], [1.0, 0.056, 0.056],
    ], { segs: 6 });
    mesh(heightShade(seg, -0.06, 1.06, 0.78, 1.0), M.coat);
  });

  addPart(root, 'wyvern_sting', (mesh) => {
    // Barbed scorpion/wyvern venom stinger tip (Y -0.05..1.05)
    const tip = lathe([
      [0.055, -0.05], [0.088, 0.35], [0.042, 0.72], [0.0, 1.05],
    ], 6);
    mesh(tip, M.dark);
    for (const sx of [-1, 1]) {
      const barb = shadeVerts(new THREE.ConeGeometry(0.026, 0.18, 4), () => 0.95);
      mesh(barb, M.coatDark, [sx * 0.065, 0.36, 0], [0, 0, sx * 2.55]);
    }
  });
}
