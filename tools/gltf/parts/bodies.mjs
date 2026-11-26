import { THREE, V, M, shadeVerts, heightShade, column, bladeExtrude, addPart } from '../geom.mjs';

export function buildBodies(root) {
  // ---------- SHARED LOW-POLY TORSO HELPERS ----------
  // Flat-fronted octagonal prism rings. Unlike a radial column these give a broad chest plane,
  // planar sides and a readable low-poly silhouette instead of a barrel or a pointed ellipsoid.
  const octagon = (rx, rz) => {
    const fx = rx * 0.62, sz = rz * 0.56;
    return [
      [-fx, rz], [fx, rz],
      [rx, sz], [rx, -sz],
      [fx, -rz], [-fx, -rz],
      [-rx, -sz], [-rx, sz],
    ];
  };

  function prismColumn(rings, { caps = true } = {}) {
    const n = 8, pos = [], idx = [];
    for (const [y, rx, rz, ox = 0, oz = 0] of rings) {
      for (const [x, z] of octagon(rx, rz)) pos.push(x + ox, y, z + oz);
    }
    for (let r = 0; r + 1 < rings.length; r++) {
      for (let i = 0; i < n; i++) {
        const i2 = (i + 1) % n;
        const A = r * n + i, B = r * n + i2, C = (r + 1) * n + i, D = (r + 1) * n + i2;
        idx.push(A, B, C, B, D, C);
      }
    }
    if (caps) for (const [r, flip] of [[0, true], [rings.length - 1, false]]) {
      const y = rings[r][0], ox = rings[r][3] ?? 0, oz = rings[r][4] ?? 0;
      const ci = pos.length / 3;
      pos.push(ox, y, oz);
      for (let i = 0; i < n; i++) {
        const p = r * n + i, q = r * n + (i + 1) % n;
        flip ? idx.push(ci, q, p) : idx.push(ci, p, q);
      }
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    return g;
  }

  // Heroic squat torso block: narrow waist, broad planar chest, fast slope into the neck.
  const torsoShell = (rxW, rzW, rxC, rzC, zC = 0.012, yBot = 0.80, yTop = 1.37) => prismColumn([
    [yBot, rxW, rzW, 0, 0],
    [yBot + 0.11, rxW * 1.05, rzW * 1.05, 0, zC * 0.35],
    [yBot + 0.34, rxC, rzC, 0, zC],
    [yTop - 0.08, rxC * 0.96, rzC * 0.92, 0, zC * 0.45],
    [yTop, 0.112, 0.102, 0, 0],
  ]);

  const trap = (wTop, wBot, h) => [
    [-wBot / 2, -h / 2], [wBot / 2, -h / 2], [wTop / 2, h / 2], [-wTop / 2, h / 2],
  ];

  const shapePlate = (pts, depth = 0.018, bevel = 0) => bladeExtrude(pts, depth, bevel);

  const box = (w, h, d, shade = 0.92) => shadeVerts(new THREE.BoxGeometry(w, h, d), () => shade);

  function beamBetween(mesh, a, b, width, thick, matRef, shade = 0.92) {
    const dir = b.clone().sub(a), len = dir.length();
    const g = new THREE.BoxGeometry(width, len, thick);
    g.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(V(0, 1, 0), dir.normalize()));
    mesh(shadeVerts(g, () => shade), matRef, [(a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2]);
  }

  // A light cloth/leather sheet with a shallow centre fold. Front sheets face +Z; back sheets reverse winding.
  function sheetPanel({ wTop, wBot, yTop, yBot, zTop, zBot, back = false, fold = 0.012, cols = 2, rows = 2 }) {
    const pos = [], idx = [];
    const sgn = back ? -1 : 1;
    for (let r = 0; r <= rows; r++) {
      const t = r / rows, y = yTop + (yBot - yTop) * t;
      const w = wTop + (wBot - wTop) * t, z = zTop + (zBot - zTop) * t;
      for (let c = 0; c <= cols; c++) {
        const u = -1 + 2 * c / cols;
        pos.push(u * w / 2, y, z + sgn * fold * (1 - Math.abs(u)));
      }
    }
    const stride = cols + 1;
    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
      const a = r * stride + c, b = a + 1, d = (r + 1) * stride + c, e = d + 1;
      if (back) idx.push(a, b, d, b, e, d);
      else idx.push(a, d, b, b, d, e);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    return heightShade(g, yBot, yTop, 0.82, 1.0);
  }

  function addBuckle(mesh, matRef, y0, y1, z, scale = 1) {
    const h = (y1 - y0) * 0.86 * scale, w = 0.074 * scale;
    mesh(box(w, h, 0.022, 1.0), matRef, [0, (y0 + y1) / 2, z]);
    mesh(box(w * 0.48, h * 0.43, 0.010, 0.72), M.dark, [0, (y0 + y1) / 2, z + 0.010]);
  }

  const beltRing = (mesh, matMain, buckleMat, y0 = 0.79, y1 = 0.87, rx = 0.205, rz = 0.160) => {
    mesh(prismColumn([[y0, rx, rz], [y1, rx * 1.01, rz * 1.01]], { caps: false }), matMain);
    if (buckleMat) addBuckle(mesh, buckleMat, y0, y1, rz + 0.012);
  };

  // ---------- SEGMENTED PAULDRONS ----------
  function pauldronPair(mesh, bodyMat, trimMat, { yLow = 1.10, fluted = false, hollow = false } = {}) {
    for (const s of [-1, 1]) {
      // Low faceted dome cupping the shoulder. The broad faces point up/outward rather than
      // forward, so it reads as armour round the arm instead of a flat shield glued to the side.
      const dome = new THREE.SphereGeometry(1, 6, 3, 0, Math.PI * 2, 0, Math.PI * 0.56);
      dome.scale(0.168, 0.098, 0.138);
      mesh(shadeVerts(dome, (_x, y) => 0.86 + y * 0.16), bodyMat, [s * 0.246, 1.235, 0], [0, 0, -s * 0.16]);

      // Two open cylindrical lames wrap the upper arm and step downward. Full rings avoid
      // foreground/background gaps while their flattened elliptical scale keeps the profile lean.
      for (let i = 0; i < 2; i++) {
        const r = 0.112 + i * 0.006;
        const lame = new THREE.CylinderGeometry(r * 0.92, r, 0.056, 6, 1, true);
        lame.scale(1.17, 1, 0.82);
        const y = yLow + 0.096 - i * 0.073;
        mesh(shadeVerts(lame, () => 0.90 - i * 0.05), i === 1 && trimMat !== bodyMat ? trimMat : bodyMat,
          [s * (0.254 + i * 0.012), y, 0], [0, 0, -s * 0.16]);
      }

      if (fluted) {
        // Narrow upright comb on the crown of the cop.
        mesh(box(0.020, 0.082, 0.128, 1.0), trimMat, [s * 0.215, 1.365, 0], [0, 0, -s * 0.18]);
      }
      if (hollow) {
        const spike = shadeVerts(new THREE.ConeGeometry(0.025, 0.090, 4), () => 0.96);
        mesh(spike, trimMat, [s * 0.305, 1.355, 0.0], [0, 0, -s * 0.55]);
      }
    }
  }

  function buildPauldrons(mesh, bodyMat, trimMat, {
    fluted = false, lance = false, hollow = false, yLow = 1.10,
  } = {}) {
    // Faceted gorget collar under the shoulder cops.
    mesh(heightShade(prismColumn([
      [1.275, 0.178, 0.140, 0, 0.002],
      [1.345, 0.150, 0.126, 0, 0],
      [1.395, 0.112, 0.104, 0, -0.002],
    ]), 1.275, 1.395, 0.84, 1.0), trimMat);
    pauldronPair(mesh, bodyMat, trimMat, { yLow, fluted, hollow });
    if (lance) {
      // Left-breast grandguard, a large planar jousting plate rather than a thin floating box.
      const guard = shapePlate([
        [-0.345, 1.145], [-0.155, 1.145], [-0.145, 1.310], [-0.245, 1.390], [-0.350, 1.310],
      ], 0.034, 0.003);
      mesh(guard, bodyMat, [0, 0, 0.176], [0, 0.10, 0]);
    }
  }

  // ---------- 1. CLOTH / LIGHT TORSOS ----------
  addPart(root, 'chest_tunic', (mesh) => {
    const torso = torsoShell(0.178, 0.132, 0.248, 0.154, 0.012, 0.79, 1.38);
    mesh(heightShade(torso, 0.79, 1.38, 0.82, 1.0), M.cloth);
    mesh(column([
      [1.315, 0.126, 0.112, 0, -0.002],
      [1.365, 0.128, 0.116, 0, -0.002],
      [1.395, 0.104, 0.098, 0, -0.004],
    ], { segs: 6 }), M.clothDark);
    // Darkened side gussets make the waist-to-chest taper legible under the arms.
    for (const s of [-1, 1]) mesh(box(0.018, 0.310, 0.115, 0.78), M.clothDark, [s * 0.218, 1.065, 0.002]);
  });

  addPart(root, 'skirt_tunic', (mesh) => {
    const shell = prismColumn([
      [0.460, 0.286, 0.204, 0, 0.006],
      [0.620, 0.246, 0.178, 0, 0.006],
      [0.830, 0.188, 0.140, 0, 0.004],
    ], { caps: false });
    mesh(heightShade(shell, 0.46, 0.83, 0.80, 0.98), M.cloth);
    // Proud front/back apron faces with a centre fold: the eye reads a garment, not a cone.
    for (const sz of [1, -1]) {
      mesh(sheetPanel({
        wTop: 0.295, wBot: 0.405, yTop: 0.805, yBot: 0.485,
        zTop: sz * 0.145, zBot: sz * 0.207, back: sz < 0, fold: 0.012,
      }), M.cloth);
      mesh(box(0.360, 0.034, 0.018, 0.82), M.clothDark, [0, 0.478, sz * 0.211]);
    }
  });

  addPart(root, 'shld_collar_cloth', (mesh) => {
    const mantle = prismColumn([
      [1.155, 0.315, 0.214, 0, -0.006],
      [1.245, 0.282, 0.192, 0, -0.004],
      [1.345, 0.154, 0.132, 0, -0.002],
      [1.395, 0.110, 0.102, 0, -0.004],
    ], { caps: false });
    mesh(heightShade(mantle, 1.155, 1.395, 0.80, 1.0), M.clothDark);
    // Small front opening shadow keeps the hood/capelet from reading as a solid funnel.
    mesh(box(0.026, 0.170, 0.012, 0.64), M.dark, [0, 1.245, 0.202]);
  });

  addPart(root, 'belt_leather', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.790, 0.868, 0.202, 0.158);
    mesh(box(0.086, 0.108, 0.052, 0.86), M.leather, [-0.202, 0.765, 0.064], [0, -0.25, 0.06]);
    mesh(box(0.070, 0.036, 0.054, 0.78), M.dark, [-0.202, 0.810, 0.066], [0, -0.25, 0.06]);
  });

  addPart(root, 'chest_brigandine', (mesh) => {
    const body = torsoShell(0.184, 0.136, 0.250, 0.154, 0.012, 0.79, 1.38);
    mesh(heightShade(body, 0.79, 1.38, 0.82, 1.0), M.cloth);
    mesh(column([[1.320, 0.134, 0.120], [1.370, 0.126, 0.114], [1.395, 0.106, 0.100]], { segs: 6 }), M.mail);

    // Large riveted leather/plate rows: deliberately few, broad and readable rather than dense rivet noise.
    for (const sz of [1, -1]) {
      for (const [y, w] of [[0.955, 0.310], [1.075, 0.350], [1.195, 0.360]]) {
        mesh(box(w, 0.090, 0.020, 0.86), M.leather, [0, y, sz * (0.153 + (y - 0.95) * 0.015)]);
      }
    }
    mesh(box(0.030, 0.415, 0.016, 0.70), M.clothDark, [0, 1.090, 0.171]);
    for (const y of [0.965, 1.085, 1.205]) {
      for (const sx of [-1, 1]) {
        const rivet = shadeVerts(new THREE.OctahedronGeometry(0.013, 0), () => 1.0);
        mesh(rivet, M.brass, [sx * 0.072, y, 0.177]);
      }
    }
  });

  addPart(root, 'shld_pauldrons_steel', (mesh) => buildPauldrons(mesh, M.steel, M.mail, { yLow: 1.100 }));

  // ---------- 2. CHESTS ----------
  addPart(root, 'chest_surcoat', (mesh) => {
    // Kept shallow at the front (max z about 0.158) so the separate heraldic emblem remains fully proud.
    mesh(heightShade(torsoShell(0.178, 0.124, 0.238, 0.132, 0.008, 0.80, 1.36), 0.80, 1.36, 0.82, 1.0), M.cloth);
    mesh(column([[1.310, 0.146, 0.126], [1.365, 0.132, 0.116]], { segs: 6 }), M.steel);
    for (const s of [-1, 1]) mesh(box(0.018, 0.380, 0.098, 0.78), M.clothDark, [s * 0.209, 1.070, 0.002]);
  });

  addPart(root, 'chest_leather', (mesh) => {
    mesh(heightShade(torsoShell(0.184, 0.136, 0.238, 0.154, 0.012, 0.80, 1.36), 0.80, 1.36, 0.80, 1.0), M.leather);
    // Dark edge bindings and two bold chest straps replace the old pair of floating black stripes.
    mesh(box(0.030, 0.430, 0.014, 0.68), M.dark, [0, 1.085, 0.162]);
    for (const sx of [-1, 1]) mesh(box(0.040, 0.430, 0.014, 0.82), M.clothDark, [sx * 0.105, 1.085, 0.158]);
    mesh(column([[1.320, 0.132, 0.118], [1.365, 0.122, 0.110]], { segs: 6 }), M.clothDark);
  });

  addPart(root, 'chest_mail', (mesh) => {
    mesh(heightShade(torsoShell(0.190, 0.142, 0.242, 0.158, 0.012, 0.80, 1.37), 0.80, 1.37, 0.82, 1.0), M.mail);
    mesh(column([[1.315, 0.146, 0.130], [1.370, 0.134, 0.120]], { segs: 8 }), M.mail);
    // Slight flared sleeve-head mass under the arms, so a hauberk reads loose rather than skin-tight.
    for (const s of [-1, 1]) mesh(box(0.052, 0.180, 0.130, 0.86), M.mail, [s * 0.224, 1.235, 0.0], [0, 0, s * 0.12]);
  });

  // Plate cuirass construction: planar breast/back plates, a proud lower plackart, then optional keel/motifs.
  const upperCuirass = [
    [-0.160, -0.200], [0.160, -0.200], [0.205, -0.090], [0.215, 0.070],
    [0.172, 0.158], [0.102, 0.208], [0, 0.224], [-0.102, 0.208], [-0.172, 0.158], [-0.215, 0.070], [-0.205, -0.090],
  ];
  const plackart = [
    [-0.170, -0.110], [0.170, -0.110], [0.198, 0.055], [0.180, 0.108], [-0.180, 0.108], [-0.198, 0.055],
  ];

  const cuirassBase = (mesh, bodyMat, trimMat, {
    ridge = true, gilded = false, fluted = false, motif = null, darkTrim = false,
  } = {}) => {
    const shell = prismColumn([
      [0.790, 0.184, 0.126, 0, 0],
      [0.930, 0.204, 0.140, 0, 0.012],
      [1.140, 0.242, 0.154, 0, 0.024],
      [1.290, 0.228, 0.142, 0, 0.010],
      [1.370, 0.116, 0.104, 0, 0],
    ]);
    mesh(heightShade(shell, 0.79, 1.37, 0.80, 1.0), bodyMat);

    const upper = shapePlate(upperCuirass, 0.032, 0);
    mesh(upper, bodyMat, [0, 1.166, 0.154]);
    mesh(shapePlate(upperCuirass, 0.032, 0), bodyMat, [0, 1.166, -0.154]);
    const lower = shapePlate(plackart, 0.028, 0);
    mesh(lower, bodyMat, [0, 0.915, 0.164]);
    mesh(shapePlate(plackart, 0.028, 0), bodyMat, [0, 0.915, -0.164]);

    // Dark/trimmed horizontal seat line makes the plackart overlap legible even when all steel.
    mesh(box(0.355, 0.018, 0.012, 0.78), darkTrim ? M.dark : trimMat, [0, 1.015, 0.181]);

    // Crisp waist flange and neck roll; both sit outside the shell but under belts/faulds.
    mesh(prismColumn([[0.785, 0.198, 0.140], [0.825, 0.198, 0.140]], { caps: false }), darkTrim ? M.mail : (gilded ? M.brass : trimMat));
    mesh(column([[1.325, 0.142, 0.124], [1.375, 0.130, 0.114]], { segs: 8 }), gilded ? M.brass : trimMat);

    if (ridge) {
      const keelPts = [[-0.012, -0.165], [0.012, -0.165], [0.020, 0.085], [0, 0.165], [-0.020, 0.085]];
      mesh(shapePlate(keelPts, 0.014, 0), gilded ? M.brass : bodyMat, [0, 1.165, 0.176]);
    }
    if (fluted) {
      for (const sx of [-1, 1]) for (const ox of [0.072, 0.132]) {
        mesh(box(0.011, 0.280, 0.010, 0.98), bodyMat, [sx * ox, 1.170, 0.177], [0, 0, -sx * 0.07]);
      }
    }
    if (motif === 'lily' || motif === 'lantern' || motif === 'vespran') {
      const mMat = motif === 'vespran' ? M.trim : M.brass;
      const badge = shadeVerts(new THREE.OctahedronGeometry(0.052, 0), () => 1.0);
      badge.scale(0.85, 1.35, 0.35);
      mesh(badge, mMat, [0, 1.150, 0.198]);
    } else if (motif === 'tarn') {
      mesh(box(0.028, 0.340, 0.018, 0.94), M.steel, [0, 1.115, 0.190]);
      mesh(box(0.180, 0.028, 0.018, 0.94), M.steel, [0, 1.175, 0.190]);
    } else if (motif === 'hollow') {
      for (const y of [1.015, 1.115, 1.215]) mesh(box(0.280, 0.020, 0.016, 0.90), M.brass, [0, y, 0.188]);
    }
  };

  addPart(root, 'chest_plate', (mesh) => cuirassBase(mesh, M.steel, M.mail, { ridge: true }));
  addPart(root, 'chest_plate_fluted', (mesh) => cuirassBase(mesh, M.steel, M.steel, { ridge: true, fluted: true }));
  addPart(root, 'chest_plate_dark', (mesh) => cuirassBase(mesh, M.mail, M.dark, { ridge: true, darkTrim: true }));
  addPart(root, 'chest_plate_gilded', (mesh) => cuirassBase(mesh, M.steel, M.brass, { ridge: true, gilded: true }));
  addPart(root, 'chest_plate_lily', (mesh) => cuirassBase(mesh, M.steel, M.brass, { ridge: true, gilded: true, motif: 'lily' }));
  addPart(root, 'chest_plate_vespran', (mesh) => cuirassBase(mesh, M.steel, M.trim, { ridge: true, motif: 'vespran' }));
  addPart(root, 'chest_plate_tarn', (mesh) => cuirassBase(mesh, M.mail, M.steel, { ridge: false, motif: 'tarn', darkTrim: true }));
  addPart(root, 'chest_plate_hollow', (mesh) => cuirassBase(mesh, M.mail, M.brass, { ridge: false, motif: 'hollow' }));
  addPart(root, 'chest_plate_lantern', (mesh) => cuirassBase(mesh, M.steel, M.brass, { ridge: true, gilded: true, motif: 'lantern' }));

  addPart(root, 'chest_breastplate', (mesh) => {
    mesh(heightShade(torsoShell(0.184, 0.132, 0.236, 0.148, 0.010, 0.80, 1.36), 0.80, 1.36, 0.82, 1.0), M.cloth);
    mesh(shapePlate(upperCuirass, 0.026, 0), M.steel, [0, 1.155, 0.148]);
    mesh(shapePlate(plackart, 0.024, 0), M.steel, [0, 0.915, 0.156]);
    // Leather shoulder straps visible at the upper corners of the cuirass.
    for (const sx of [-1, 1]) beamBetween(mesh, V(sx * 0.155, 1.345, 0.148), V(sx * 0.205, 1.185, 0.155), 0.034, 0.012, M.leather, 0.86);
  });

  const mirrorPlate = (mesh, bodyMat, discMat, ornate = false) => {
    mesh(heightShade(torsoShell(0.188, 0.140, 0.238, 0.156, 0.012, 0.80, 1.37), 0.80, 1.37, 0.82, 1.0), bodyMat);
    for (const sz of [1, -1]) {
      const disc = shadeVerts(new THREE.CylinderGeometry(0.112, 0.120, 0.026, 8), () => 0.98);
      disc.rotateX(Math.PI / 2);
      mesh(disc, discMat, [0, 1.120, sz * 0.166]);
      if (ornate) {
        const ring = shadeVerts(new THREE.TorusGeometry(0.113, 0.011, 3, 8), () => 1.0);
        mesh(ring, M.brass, [0, 1.120, sz * 0.181]);
      }
    }
    for (const sx of [-1, 1]) mesh(box(0.034, 0.440, 0.014, 0.86), M.leather, [sx * 0.145, 1.095, 0.156]);
  };
  addPart(root, 'chest_mirror', (mesh) => mirrorPlate(mesh, M.mail, M.steel, false));
  addPart(root, 'chest_mirror_qadir', (mesh) => mirrorPlate(mesh, M.cloth, M.brass, true));

  const lamellarChest = (mesh, mainMat, trimMat, ornate = false) => {
    mesh(heightShade(torsoShell(0.192, 0.144, 0.240, 0.160, 0.012, 0.80, 1.37), 0.80, 1.37, 0.82, 1.0), mainMat);
    // Four thick overlapping horizontal rows. The flattened prism faces make each row read at distance.
    for (const [y, rx, rz] of [
      [0.925, 0.208, 0.154], [1.035, 0.226, 0.166], [1.145, 0.242, 0.174], [1.255, 0.236, 0.162],
    ]) {
      mesh(prismColumn([[y - 0.018, rx, rz], [y + 0.018, rx, rz]], { caps: false }), trimMat);
    }
    mesh(box(0.026, 0.420, 0.014, 0.76), ornate ? M.brass : M.leather, [0, 1.085, 0.181]);
    if (ornate) {
      const boss = shadeVerts(new THREE.OctahedronGeometry(0.058, 0), () => 1.0);
      boss.scale(1, 1.15, 0.45);
      mesh(boss, M.brass, [0, 1.140, 0.198]);
    }
  };
  addPart(root, 'chest_lamellar', (mesh) => lamellarChest(mesh, M.leather, M.mail, false));
  addPart(root, 'chest_lamellar_khan', (mesh) => lamellarChest(mesh, M.steel, M.brass, true));
  addPart(root, 'chest_scale', (mesh) => lamellarChest(mesh, M.mail, M.steel, false));
  addPart(root, 'chest_jarl_scale', (mesh) => lamellarChest(mesh, M.steel, M.brass, true));

  addPart(root, 'chest_robe', (mesh) => {
    const robe = prismColumn([
      [0.180, 0.292, 0.218, 0, 0],
      [0.520, 0.252, 0.190, 0, 0],
      [0.820, 0.198, 0.150, 0, 0.008],
      [1.150, 0.236, 0.162, 0, 0.012],
      [1.360, 0.122, 0.110, 0, 0],
    ]);
    mesh(heightShade(robe, 0.18, 1.36, 0.78, 1.0), M.cloth);
    // Twin pale stole strips and a dark centre opening match the reference priest/mage silhouette.
    for (const sx of [-1, 1]) beamBetween(mesh, V(sx * 0.070, 0.245, 0.220), V(sx * 0.060, 1.235, 0.166), 0.046, 0.012, M.trim, 0.96);
    beamBetween(mesh, V(0, 0.260, 0.218), V(0, 1.155, 0.170), 0.018, 0.010, M.clothDark, 0.72);
    mesh(box(0.440, 0.034, 0.018, 0.78), M.clothDark, [0, 0.195, 0.205]);
  });

  addPart(root, 'chest_jubbah', (mesh) => {
    mesh(heightShade(torsoShell(0.190, 0.140, 0.238, 0.158, 0.012, 0.80, 1.36), 0.80, 1.36, 0.82, 1.0), M.cloth);
    for (const sx of [-1, 1]) mesh(box(0.038, 0.500, 0.014, 0.96), M.trim, [sx * 0.048, 1.075, 0.164]);
    mesh(column([[1.310, 0.144, 0.128], [1.360, 0.132, 0.118]], { segs: 8 }), M.trim);
  });

  addPart(root, 'chest_tarn_habit', (mesh) => {
    mesh(heightShade(torsoShell(0.188, 0.140, 0.238, 0.158, 0.012, 0.80, 1.37), 0.80, 1.37, 0.80, 1.0), M.clothDark);
    mesh(shapePlate(trap(0.150, 0.180, 0.540), 0.016, 0), M.cloth, [0, 1.080, 0.163]);
    mesh(box(0.026, 0.340, 0.018, 0.96), M.trim, [0, 1.100, 0.176]);
  });

  addPart(root, 'chest_buffcoat', (mesh) => {
    mesh(heightShade(torsoShell(0.192, 0.142, 0.240, 0.160, 0.012, 0.80, 1.38), 0.80, 1.38, 0.82, 1.0), M.leather);
    mesh(box(0.042, 0.520, 0.014, 0.92), M.trim, [0, 1.090, 0.166]);
    for (const sx of [-1, 1]) mesh(box(0.030, 0.410, 0.012, 0.80), M.dark, [sx * 0.115, 1.075, 0.160]);
    mesh(column([[1.320, 0.146, 0.130], [1.380, 0.132, 0.118]], { segs: 8 }), M.leather);
  });

  // chest_oilskin: Bayfolk waxed mariner coat (M.leather) with dark blue storm collar & brass clasps (matches 2D sprite)
  addPart(root, 'chest_oilskin', (mesh) => {
    mesh(heightShade(torsoShell(0.194, 0.144, 0.242, 0.162, 0.012, 0.80, 1.38), 0.80, 1.38, 0.80, 1.0), M.leather);
    mesh(column([[1.310, 0.162, 0.145], [1.410, 0.178, 0.158]], { segs: 8 }), M.clothDark);
    for (const y of [0.96, 1.10, 1.24]) mesh(box(0.048, 0.022, 0.018, 0.98), M.brass, [0, y, 0.168]);
  });

  // chest_slashed: Vespran renaissance puffed-and-slashed cloth doublet with proud trim slashes
  addPart(root, 'chest_slashed', (mesh) => {
    mesh(heightShade(torsoShell(0.194, 0.144, 0.264, 0.172, 0.014, 0.80, 1.38), 0.80, 1.38, 0.84, 1.0), M.cloth);
    for (const sx of [-0.095, 0, 0.095]) mesh(box(0.026, 0.340, 0.016, 0.98), M.trim, [sx, 1.07, 0.188]);
    mesh(column([[1.320, 0.156, 0.140], [1.400, 0.148, 0.132]], { segs: 8 }), M.clothDark);
  });

  // chest_bear_pelt: Holtmen bear-cult berserker fur torso (M.hair matching bearhead) with leather cross-harness
  addPart(root, 'chest_bear_pelt', (mesh) => {
    mesh(heightShade(torsoShell(0.200, 0.150, 0.256, 0.172, 0.014, 0.80, 1.38), 0.80, 1.38, 0.78, 0.98), M.hair);
    mesh(column([[0.80, 0.202, 0.152], [0.98, 0.206, 0.156]], { segs: 8 }), M.leather);
    beamBetween(mesh, V(-0.17, 1.34, 0.184), V(0.16, 0.84, 0.186), 0.044, 0.018, M.leather, 0.92);
    beamBetween(mesh, V(0.17, 1.34, 0.184), V(-0.16, 0.84, 0.186), 0.044, 0.018, M.leather, 0.92);
  });

  // chest_reed_cloak: Fenmen woven reed-and-oilcloth marsh smock with clean wide shoulder mantle
  addPart(root, 'chest_reed_cloak', (mesh) => {
    mesh(heightShade(torsoShell(0.194, 0.144, 0.238, 0.160, 0.012, 0.80, 1.38), 0.80, 1.38, 0.80, 0.98), M.leather);
    mesh(column([[1.16, 0.315, 0.228, 0, 0.012], [1.39, 0.235, 0.180, 0, 0.012]], { segs: 8 }), M.clothDark);
  });

  // chest_grey_plate: Austere Tarn dark-iron plate cuirass under grey monastic scapular strip
  addPart(root, 'chest_grey_plate', (mesh) => {
    mesh(heightShade(torsoShell(0.196, 0.146, 0.250, 0.168, 0.014, 0.80, 1.38), 0.80, 1.38, 0.80, 0.98), M.mail);
    for (const sz of [1, -1]) mesh(box(0.155, 0.54, 0.020, 0.92), M.clothDark, [0, 1.09, sz * 0.180]);
  });

  // chest_lacquered_lamellar: Naryn dark lacquered lamellar cuirass with proud octagonal brass bands
  addPart(root, 'chest_lacquered_lamellar', (mesh) => {
    mesh(heightShade(torsoShell(0.198, 0.148, 0.248, 0.168, 0.012, 0.80, 1.38), 0.80, 1.38, 0.80, 0.98), M.clothDark);
    for (const [y, rx, rz] of [[0.92, 0.242, 0.182], [1.06, 0.258, 0.192], [1.20, 0.264, 0.192]]) {
      mesh(column([[y, rx, rz, 0, 0.014], [y + 0.036, rx, rz, 0, 0.014]], { segs: 8 }), M.brass);
    }
  });

  // chest_kazaghand: Qadiri silk-covered mail coat with gilded collar & wide brass clasps
  addPart(root, 'chest_kazaghand', (mesh) => {
    mesh(heightShade(torsoShell(0.196, 0.146, 0.246, 0.166, 0.012, 0.80, 1.38), 0.80, 1.38, 0.84, 1.0), M.cloth);
    mesh(column([[1.31, 0.156, 0.140], [1.39, 0.150, 0.134]], { segs: 8 }), M.brass);
    for (const y of [0.96, 1.09, 1.22]) mesh(box(0.084, 0.026, 0.020, 0.98), M.brass, [0, y, 0.180]);
  });

  // chest_corsair_vest: Marenite open embroidered sea-vest (proud dark vest panels over cream shirt)
  addPart(root, 'chest_corsair_vest', (mesh) => {
    mesh(heightShade(torsoShell(0.186, 0.136, 0.232, 0.154, 0.012, 0.80, 1.38), 0.80, 1.38, 0.88, 1.0), M.trim);
    for (const sx of [-1, 1]) mesh(box(0.125, 0.50, 0.345, 0.84), M.clothDark, [sx * 0.135, 1.09, 0.012]);
  });

  // chest_crag_plaid: Cragfolk alpine checked-cloth tunic (M.cloth -> PatClothCheck) with bold hem band
  addPart(root, 'chest_crag_plaid', (mesh) => {
    mesh(heightShade(torsoShell(0.196, 0.146, 0.246, 0.166, 0.012, 0.80, 1.38), 0.80, 1.38, 0.84, 1.0), M.cloth);
    mesh(column([[0.80, 0.200, 0.150], [0.88, 0.202, 0.152]], { segs: 8 }), M.clothDark);
  });

  addPart(root, 'chest_bones', (mesh) => {
    mesh(column([[0.68, 0.032, 0.032], [1.36, 0.028, 0.028]], { segs: 6 }), M.skin, [0, 0, -0.05]);
    const pelvis = column([[0.66, 0.16, 0.09], [0.80, 0.19, 0.10]], { segs: 8 });
    mesh(pelvis, M.skin);
    for (let i = 0; i < 5; i++) {
      const y = 0.94 + i * 0.082;
      const rx = 0.16 + Math.sin((i / 4) * Math.PI) * 0.045;
      const rib = shadeVerts(new THREE.TorusGeometry(rx, 0.015, 4, 8), () => 0.92);
      rib.scale(1.0, 0.65, 1.0);
      mesh(rib, M.skin, [0, y, 0.02], [Math.PI / 2, 0, 0]);
    }
    mesh(box(0.038, 0.36, 0.022, 0.98), M.skin, [0, 1.12, 0.115]);
  });

  // ---------- 3. SHOULDERS ----------
  addPart(root, 'shld_pauldrons_gilded', (mesh) => buildPauldrons(mesh, M.steel, M.brass, { yLow: 1.080 }));
  addPart(root, 'shld_pauldrons_fluted', (mesh) => buildPauldrons(mesh, M.steel, M.steel, { fluted: true, yLow: 1.130 }));
  addPart(root, 'shld_pauldrons_dark', (mesh) => buildPauldrons(mesh, M.mail, M.dark, { yLow: 1.140 }));
  addPart(root, 'shld_pauldrons_lance', (mesh) => buildPauldrons(mesh, M.steel, M.brass, { lance: true, yLow: 1.060 }));
  addPart(root, 'shld_pauldrons_hollow', (mesh) => buildPauldrons(mesh, M.mail, M.brass, { hollow: true, yLow: 1.130 }));

  const buildMantle = (mesh, matMain, rxBot, rzBot, yBot, yTop = 1.39) => {
    const g = prismColumn([
      [yBot, rxBot, rzBot, 0, -0.004],
      [(yBot + yTop) * 0.5, rxBot * 0.86, rzBot * 0.86, 0, -0.002],
      [yTop, 0.142, 0.126, 0, -0.002],
    ], { caps: false });
    mesh(heightShade(g, yBot, yTop, 0.80, 1.0), matMain);
  };

  addPart(root, 'shld_mail_aventail', (mesh) => {
    buildMantle(mesh, M.mail, 0.292, 0.205, 1.195, 1.405);
    // Faceted two-lame shoulder defense over the mail, matching the foot-knight reference silhouette.
    pauldronPair(mesh, M.steel, M.steel, { yLow: 1.125 });
  });

  addPart(root, 'shld_gorget_steel', (mesh) => {
    buildMantle(mesh, M.steel, 0.285, 0.170, 1.190, 1.400);
    mesh(box(0.030, 0.150, 0.014, 0.82), M.mail, [0, 1.245, 0.166]);
  });

  addPart(root, 'shld_lamellar', (mesh) => {
    buildMantle(mesh, M.leather, 0.320, 0.165, 1.125, 1.390);
    for (const y of [1.155, 1.235]) {
      mesh(prismColumn([[y - 0.014, 0.312 - (y - 1.155) * 0.18, 0.164], [y + 0.014, 0.312 - (y - 1.155) * 0.18, 0.164]], { caps: false }), M.mail);
    }
  });

  addPart(root, 'shld_scale', (mesh) => {
    buildMantle(mesh, M.mail, 0.315, 0.150, 1.125, 1.390);
    for (const y of [1.155, 1.235]) {
      mesh(prismColumn([[y - 0.013, 0.306 - (y - 1.155) * 0.16, 0.150], [y + 0.013, 0.306 - (y - 1.155) * 0.16, 0.150]], { caps: false }), M.steel);
    }
  });

  addPart(root, 'shld_linen_shawl', (mesh) => {
    buildMantle(mesh, M.trim, 0.282, 0.205, 1.205, 1.390);
    // Two hanging shawl ends make the cultural silhouette legible from the front.
    for (const sx of [-1, 1]) mesh(box(0.052, 0.300, 0.014, 0.94), M.trim, [sx * 0.075, 1.105, 0.166], [0, 0, sx * 0.04]);
  });

  addPart(root, 'shld_cowl_cloth', (mesh) => {
    buildMantle(mesh, M.clothDark, 0.292, 0.215, 1.135, 1.395);
    mesh(box(0.024, 0.170, 0.012, 0.64), M.dark, [0, 1.245, 0.205]);
  });

  addPart(root, 'shld_tarn_mantle', (mesh) => {
    buildMantle(mesh, M.clothDark, 0.300, 0.224, 1.170, 1.400);
    mesh(box(0.070, 0.270, 0.016, 0.90), M.cloth, [0, 1.125, 0.185]);
  });

  addPart(root, 'shld_fur_mantle', (mesh) => {
    // Very broad angular pelt capelet with chunky triangular hanging tufts.
    const cape = prismColumn([
      [1.055, 0.392, 0.268, 0, -0.010],
      [1.205, 0.360, 0.246, 0, -0.007],
      [1.315, 0.272, 0.196, 0, -0.004],
      [1.395, 0.148, 0.132, 0, -0.002],
    ], { caps: false });
    mesh(heightShade(cape, 1.055, 1.395, 0.76, 1.0), M.hair);
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2 + Math.PI / 6;
      const tab = shadeVerts(new THREE.ConeGeometry(0.112, 0.145, 4), () => 0.84 + (i % 2) * 0.08);
      mesh(tab, M.hair, [Math.cos(a) * 0.305, 1.075, Math.sin(a) * 0.210 - 0.008], [Math.PI, a, 0]);
    }
    const clasp = shadeVerts(new THREE.CylinderGeometry(0.042, 0.042, 0.024, 6), () => 1.0);
    clasp.rotateX(Math.PI / 2);
    mesh(clasp, M.brass, [0.120, 1.270, 0.218]);
  });

  addPart(root, 'shld_fur_collar', (mesh) => {
    buildMantle(mesh, M.hair, 0.255, 0.198, 1.120, 1.390);
    mesh(box(0.082, 0.240, 0.042, 0.86), M.hair, [0, 1.020, 0.174]);
  });

  addPart(root, 'shld_wolf_mantle', (mesh) => {
    buildMantle(mesh, M.coat, 0.305, 0.225, 1.155, 1.400);
    const pelt = shadeVerts(new THREE.SphereGeometry(0.075, 6, 4), () => 0.94);
    pelt.scale(1.0, 0.8, 1.35);
    mesh(pelt, M.coatDark, [-0.200, 1.330, 0.120]);
    mesh(box(0.050, 0.160, 0.030, 0.82), M.coatDark, [-0.155, 1.210, 0.165], [0, 0, -0.12]);
  });

  // ---------- 4. BELTS / STRAPS ----------
  addPart(root, 'belt_pouch', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.790, 0.870, 0.202, 0.160);
    mesh(box(0.108, 0.128, 0.070, 0.86), M.leather, [-0.185, 0.742, 0.082], [0, -0.18, 0.04]);
    mesh(box(0.090, 0.034, 0.072, 0.76), M.dark, [-0.185, 0.790, 0.084], [0, -0.18, 0.04]);
    mesh(box(0.086, 0.106, 0.060, 0.86), M.leather, [0.165, 0.752, 0.066], [0, 0.15, -0.03]);
  });

  addPart(root, 'belt_gilded', (mesh) => beltRing(mesh, M.brass, M.trim, 0.790, 0.870, 0.212, 0.172));
  addPart(root, 'belt_sash_trim', (mesh) => beltRing(mesh, M.trim, M.brass, 0.790, 0.870, 0.208, 0.166));
  addPart(root, 'belt_sash_dark', (mesh) => beltRing(mesh, M.clothDark, M.trim, 0.790, 0.870, 0.208, 0.166));

  addPart(root, 'belt_plate', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.790, 0.870, 0.214, 0.174);
    for (const sx of [-1, 1]) mesh(box(0.064, 0.070, 0.020, 0.98), M.brass, [sx * 0.126, 0.830, 0.170]);
  });

  addPart(root, 'belt_baldric', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.790, 0.862, 0.204, 0.162);
    // Broad flat baldrics, front and back, rather than narrow round cords.
    beamBetween(mesh, V(-0.180, 1.285, 0.170), V(0.170, 0.835, 0.168), 0.046, 0.012, M.leather, 0.88);
    beamBetween(mesh, V(-0.180, 1.285, -0.154), V(0.170, 0.835, -0.154), 0.046, 0.012, M.leather, 0.82);
  });

  addPart(root, 'belt_cross_straps', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.790, 0.862, 0.204, 0.162);
    for (const sx of [-1, 1]) {
      beamBetween(mesh, V(-sx * 0.170, 1.245, 0.170), V(sx * 0.170, 0.835, 0.168), 0.038, 0.012, M.leather, 0.88);
      beamBetween(mesh, V(-sx * 0.170, 1.245, -0.154), V(sx * 0.170, 0.835, -0.154), 0.038, 0.012, M.leather, 0.82);
    }
  });

  addPart(root, 'belt_stole', (mesh) => {
    beltRing(mesh, M.clothDark, M.brass, 0.800, 0.862, 0.218, 0.172);
    for (const sx of [-1, 1]) mesh(box(0.054, 0.620, 0.014, 0.96), M.trim, [sx * 0.086, 0.835, 0.180]);
  });

  // ---------- 5. SKIRTS / FAULDS ----------
  const skirtShell = (mesh, mainMat, yBot, yTop, rxTop, rxBot, rzTop, rzBot) => {
    const g = prismColumn([
      [yBot, rxBot, rzBot, 0, 0.004],
      [(yBot + yTop) * 0.5, (rxTop + rxBot) * 0.52, (rzTop + rzBot) * 0.52, 0, 0.004],
      [yTop, rxTop, rzTop, 0, 0.002],
    ], { caps: false });
    mesh(heightShade(g, yBot, yTop, 0.78, 1.0), mainMat);
  };

  const hoop = (mesh, y, rx, rz, h, matRef) => {
    mesh(prismColumn([[y - h / 2, rx, rz], [y + h / 2, rx, rz]], { caps: false }), matRef);
  };

  function buildClothSkirt(mesh, mainMat, trimMat, yBot, yTop, rxTop, rxBot, rzTop, rzBot, { panels = true, centreTrim = false } = {}) {
    skirtShell(mesh, mainMat, yBot, yTop, rxTop, rxBot, rzTop, rzBot);
    if (panels) for (const sz of [1, -1]) {
      mesh(sheetPanel({
        wTop: rxTop * 1.30, wBot: rxBot * 1.34, yTop: yTop - 0.025, yBot: yBot + 0.025,
        zTop: sz * (rzTop + 0.006), zBot: sz * (rzBot + 0.008), back: sz < 0, fold: 0.012,
      }), mainMat);
      mesh(box(rxBot * 1.25, 0.032, 0.018, 0.82), trimMat, [0, yBot + 0.018, sz * (rzBot + 0.012)]);
      if (centreTrim && sz > 0) {
        beamBetween(mesh, V(-0.045, yTop - 0.040, rzTop + 0.014), V(-0.065, yBot + 0.045, rzBot + 0.020), 0.026, 0.010, trimMat, 0.96);
        beamBetween(mesh, V(0.045, yTop - 0.040, rzTop + 0.014), V(0.065, yBot + 0.045, rzBot + 0.020), 0.026, 0.010, trimMat, 0.96);
      }
    }
  }

  function buildFauld(mesh, mainMat, trimMat, yBot, yTop, rx, rz, rows = 2) {
    // A continuous flared armoured skirt preserves the side silhouette; overlapping hoops and
    // broad aprons supply the construction. This avoids the cage of separated sticks that
    // isolated tassets produce in profile.
    skirtShell(mesh, mainMat, yBot, yTop, rx * 0.96, rx * 1.20, rz * 0.94, rz * 1.18);
    for (let i = 0; i < rows; i++) {
      const t = rows === 1 ? 0 : i / (rows - 1);
      const y = yTop - 0.025 - i * 0.055;
      hoop(mesh, y, rx + 0.010 + t * 0.012, rz + 0.008 + t * 0.010, 0.038, i === rows - 1 ? trimMat : mainMat);
    }

    const panelTop = yTop - 0.030;
    const apron = (x, wTop, wBot, zSign) => {
      const g = sheetPanel({
        wTop, wBot, yTop: panelTop, yBot,
        zTop: zSign * (rz + 0.009), zBot: zSign * (rz * 1.18 + 0.012),
        back: zSign < 0, fold: 0.006,
      });
      g.translate(x, 0, 0);
      mesh(g, mainMat);
    };
    // Split front pair and one broad rear apron. The centre front gap reads as the leg slit.
    for (const sx of [-1, 1]) apron(sx * 0.086, 0.150, 0.172, 1);
    apron(0, 0.290, 0.340, -1);
    if (trimMat !== mainMat) for (const sx of [-1, 1]) {
      mesh(box(0.148, 0.022, 0.014, 0.96), trimMat, [sx * 0.086, yBot + 0.010, rz * 1.18 + 0.022]);
    }
  }

  addPart(root, 'skirt_mail', (mesh) => {
    skirtShell(mesh, M.mail, 0.480, 0.830, 0.195, 0.248, 0.140, 0.184);
    // Front slit shadow keeps the hauberk skirt from reading as a solid dress.
    mesh(box(0.022, 0.200, 0.012, 0.58), M.dark, [0, 0.570, 0.190]);
  });

  addPart(root, 'skirt_fauld_steel', (mesh) => buildFauld(mesh, M.steel, M.mail, 0.520, 0.830, 0.202, 0.148, 2));
  addPart(root, 'skirt_fauld_gilded', (mesh) => buildFauld(mesh, M.steel, M.brass, 0.520, 0.830, 0.202, 0.148, 2));
  addPart(root, 'skirt_fauld_dark', (mesh) => buildFauld(mesh, M.mail, M.dark, 0.510, 0.830, 0.205, 0.150, 3));
  addPart(root, 'skirt_fauld_long', (mesh) => buildFauld(mesh, M.steel, M.mail, 0.430, 0.830, 0.205, 0.150, 3));
  addPart(root, 'skirt_fauld_hollow', (mesh) => buildFauld(mesh, M.mail, M.brass, 0.520, 0.800, 0.198, 0.145, 2));

  function buildLamellarSkirt(mesh, mainMat, trimMat, yBot, rows) {
    skirtShell(mesh, mainMat, yBot, 0.830, 0.198, 0.250, 0.140, 0.184);
    for (let i = 0; i < rows; i++) {
      const t = (i + 0.5) / rows, y = yBot + t * (0.810 - yBot);
      const rx = 0.246 + (0.198 - 0.246) * t + 0.010;
      const rz = 0.181 + (0.140 - 0.181) * t + 0.008;
      hoop(mesh, y, rx, rz, 0.026, trimMat);
    }
    // Four broad hanging scale strips on the front reinforce construction without dense lacing.
    for (const sx of [-0.135, -0.045, 0.045, 0.135]) {
      mesh(box(0.070, 0.220, 0.014, 0.88), mainMat, [sx, (yBot + 0.755) / 2, 0.190]);
    }
  }
  addPart(root, 'skirt_lamellar', (mesh) => buildLamellarSkirt(mesh, M.leather, M.mail, 0.460, 3));
  addPart(root, 'skirt_scale', (mesh) => buildLamellarSkirt(mesh, M.mail, M.steel, 0.520, 2));

  addPart(root, 'skirt_deel', (mesh) => {
    buildClothSkirt(mesh, M.cloth, M.trim, 0.400, 0.830, 0.198, 0.270, 0.142, 0.205, { centreTrim: true });
    // Steppe coat overlap: one diagonal pale edge across the front skirt.
    beamBetween(mesh, V(-0.105, 0.790, 0.157), V(0.090, 0.445, 0.218), 0.030, 0.010, M.trim, 0.96);
  });

  addPart(root, 'skirt_kaftan', (mesh) => {
    buildClothSkirt(mesh, M.cloth, M.clothDark, 0.380, 0.810, 0.198, 0.272, 0.142, 0.206, { centreTrim: true });
  });

  addPart(root, 'skirt_tabard', (mesh) => {
    for (const sz of [1, -1]) {
      // Tapered apron panels with a pale hem, front and back; sides remain open for leg movement.
      mesh(shapePlate(trap(0.215, 0.292, 0.380), 0.014, 0), M.cloth, [0, 0.630, sz * 0.198]);
      mesh(box(0.286, 0.032, 0.018, 0.98), M.trim, [0, 0.455, sz * 0.202]);
    }
  });

  addPart(root, 'skirt_tarn_scapular', (mesh) => {
    skirtShell(mesh, M.clothDark, 0.500, 0.820, 0.195, 0.232, 0.138, 0.174);
    for (const sz of [1, -1]) mesh(shapePlate(trap(0.150, 0.176, 0.400), 0.016, 0), M.cloth, [0, 0.620, sz * 0.190]);
  });
}
