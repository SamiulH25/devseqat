import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildBodies(root) {
  // 1. chest_tunic: heroic tapered low-poly gambeson/tunic with bold shoulder breadth & cinched waist
  addPart(root, 'chest_tunic', (mesh) => {
    const torso = column([
      [0.79, 0.178, 0.136, 0, 0.004],
      [0.86, 0.172, 0.132, 0, 0.006],
      [1.00, 0.218, 0.154, 0, 0.012],
      [1.16, 0.256, 0.168, 0, 0.016],
      [1.28, 0.258, 0.156, 0, 0.008],
      [1.34, 0.195, 0.128, 0, 0.0],
      [1.38, 0.106, 0.100, 0, -0.002],
    ], { segs: 6 });
    mesh(heightShade(torso, 0.79, 1.38, 0.82, 1.0), M.cloth);

    const collar = column([
      [1.32, 0.124, 0.114, 0, -0.002],
      [1.36, 0.130, 0.120, 0, -0.002],
      [1.39, 0.106, 0.100, 0, -0.004],
    ], { segs: 6 });
    mesh(collar, M.clothDark);

    for (const s of [-1, 1]) {
      const cap = loft(V(s * 0.18, 1.29, 0.0), V(s * 0.285, 1.19, 0.0), [
        [0, 0.106, 0.110],
        [0.5, 0.102, 0.106],
        [1, 0.086, 0.090],
      ], { segs: 6, fwd: V(0, 0, 1) });
      mesh(shadeVerts(cap, () => 0.92), M.cloth);
    }
  });

  // 2. skirt_tunic: bold A-line flared low-poly tunic skirt with crisp hem band
  addPart(root, 'skirt_tunic', (mesh) => {
    const skirt = column([
      [0.46, 0.288, 0.205, 0, 0.008],
      [0.56, 0.256, 0.184, 0, 0.008],
      [0.70, 0.214, 0.156, 0, 0.006],
      [0.83, 0.182, 0.140, 0, 0.004],
    ], { segs: 6 });
    mesh(heightShade(skirt, 0.46, 0.83, 0.80, 0.98), M.cloth);
    const hem = column([
      [0.455, 0.294, 0.210, 0, 0.008],
      [0.495, 0.282, 0.202, 0, 0.008],
    ], { segs: 6, caps: false });
    mesh(hem, M.clothDark);
  });

  // 3. shld_collar_cloth: crisp flared medieval hood-capelet over shoulders
  addPart(root, 'shld_collar_cloth', (mesh) => {
    const mantle = column([
      [1.17, 0.295, 0.210, 0, -0.004],
      [1.26, 0.258, 0.182, 0, -0.002],
      [1.35, 0.146, 0.128, 0, -0.002],
      [1.39, 0.110, 0.104, 0, -0.004],
    ], { segs: 6 });
    mesh(heightShade(mantle, 1.17, 1.39, 0.82, 1.0), M.clothDark);
  });

  // 4. belt_leather: snug waist belt + crisp brass buckle + hip pouch
  addPart(root, 'belt_leather', (mesh) => {
    const strap = column([
      [0.790, 0.194, 0.150, 0, 0.005],
      [0.865, 0.188, 0.146, 0, 0.006],
    ], { segs: 6 });
    mesh(strap, M.leather);
    const buckle = shadeVerts(new THREE.BoxGeometry(0.068, 0.068, 0.028), () => 1.0);
    mesh(buckle, M.brass, [0, 0.828, 0.152]);
    const pouch = shadeVerts(new THREE.BoxGeometry(0.082, 0.105, 0.054), (_x, y) => 0.86 + y);
    mesh(pouch, M.leather, [-0.198, 0.770, 0.055], [0, -0.3, 0.08]);
  });

  // 5. chest_brigandine: broad heroic coat-of-plates / brigandine with clean planar facets
  addPart(root, 'chest_brigandine', (mesh) => {
    const body = column([
      [0.79, 0.188, 0.142, 0, 0.004],
      [0.87, 0.184, 0.140, 0, 0.006],
      [1.00, 0.228, 0.160, 0, 0.014],
      [1.16, 0.260, 0.174, 0, 0.018],
      [1.28, 0.258, 0.158, 0, 0.008],
      [1.34, 0.196, 0.130, 0, 0.0],
      [1.38, 0.112, 0.104, 0, -0.002],
    ], { segs: 6 });
    mesh(heightShade(body, 0.79, 1.38, 0.82, 1.0), M.cloth);

    const collar = column([
      [1.32, 0.138, 0.122, 0, -0.002],
      [1.36, 0.132, 0.118, 0, -0.002],
      [1.39, 0.108, 0.102, 0, -0.004],
    ], { segs: 6 });
    mesh(collar, M.steel);

    mesh(shadeVerts(new THREE.BoxGeometry(0.040, 0.48, 0.018), () => 0.90), M.clothDark, [0, 1.09, 0.176]);
  });

  // shld_pauldrons_steel: bold heroic wide 3-tier angular faceted steel pauldrons + gorget
  addPart(root, 'shld_pauldrons_steel', (mesh) => {
    const gorget = column([
      [1.28, 0.205, 0.162, 0, 0.004],
      [1.35, 0.156, 0.132, 0, 0.0],
      [1.40, 0.116, 0.108, 0, -0.002],
    ], { segs: 6 });
    mesh(heightShade(gorget, 1.28, 1.40, 0.84, 1.0), M.steel);

    for (const s of [-1, 1]) {
      // Wide angular top shoulder plate
      const dome = loft(V(s * 0.17, 1.37, 0.0), V(s * 0.365, 1.23, 0.0), [
        [0.0, 0.120, 0.140],
        [0.45, 0.134, 0.154],
        [1.0, 0.110, 0.126],
      ], { segs: 6, fwd: V(0, 0, 1) });
      mesh(shadeVerts(dome, (_x, y) => 0.86 + 0.14 * Math.max(0, (y - 1.18) / 0.20)), M.steel);

      // Two crisp stepped lower lames descending the upper arm
      for (const [k, yOff, xOff, w] of [[0, -0.095, 0.032, 0.110], [1, -0.175, 0.052, 0.098]]) {
        const lame = loft(V(s * (0.225 + xOff), 1.29 + yOff, 0), V(s * (0.350 + xOff), 1.21 + yOff, 0), [
          [0.0, w, w * 1.14],
          [1.0, w * 0.90, w * 1.04],
        ], { segs: 6, fwd: V(0, 0, 1) });
        mesh(shadeVerts(lame, () => 0.90 - k * 0.08), k === 0 ? M.steel : M.mail);
      }
    }
  });

  // ---------- SHARED TORSO HELPERS ----------
  // Heroic V-tapered 6-sided faceted torso: cinched waist -> broad athletic chest -> clean shoulder slope
  const torsoCol = (rxW, rzW, rxC, rzC, zC = 0.018, yBot = 0.80, yTop = 1.37, segs = 6) => column([
    [yBot, rxW, rzW, 0, 0],
    [yBot + 0.12, rxW * 1.05, rzW * 1.06, 0, zC * 0.5],
    [yBot + 0.34, rxC * 1.12, rzC * 1.08, 0, zC],
    [yTop - 0.08, rxC * 1.08, rzC * 0.92, 0, zC * 0.45],
    [yTop, rxC * 0.50, rzC * 0.60, 0, 0],
  ], { segs });

  const cuirassBase = (mesh, bodyMat, trimMat, {
    ridge = true, gilded = false, fluted = false, motif = null, darkTrim = false,
  } = {}) => {
    // Sculpted globose breastplate + backplate
    const cu = column([
      [0.80, 0.185, 0.132, 0, 0.0],
      [0.94, 0.210, 0.155, 0, 0.018],
      [1.14, 0.245, 0.185, 0, 0.036],
      [1.29, 0.232, 0.152, 0, 0.014],
      [1.37, 0.132, 0.118, 0, 0.0],
    ], { segs: 10 });
    mesh(heightShade(cu, 0.80, 1.37, 0.80, 1.0), bodyMat);
    // Waist flange & collar rim
    mesh(column([[0.79, 0.198, 0.144], [0.83, 0.194, 0.140]], { segs: 10 }), darkTrim ? M.mail : (gilded ? M.brass : trimMat));
    mesh(column([[1.33, 0.144, 0.128], [1.37, 0.134, 0.118]], { segs: 10 }), gilded ? M.brass : trimMat);
    // Center medial keel ridge on front (+Z)
    if (ridge) {
      const keel = loft(V(0, 0.84, 0.145), V(0, 1.32, 0.155), [
        [0, 0.014, 0.016], [0.55, 0.022, 0.034, 0, 0.060], [1, 0.012, 0.014],
      ], { segs: 4 });
      mesh(keel, gilded ? M.brass : bodyMat);
    }
    if (fluted) {
      for (const sx of [-1, 1]) {
        for (const ox of [0.065, 0.13]) {
          const rib = loft(V(sx * ox * 0.7, 0.86, 0.14), V(sx * ox, 1.26, 0.165), [
            [0, 0.008, 0.010], [0.5, 0.010, 0.018, 0, 0.035], [1, 0.008, 0.010],
          ], { segs: 4 });
          mesh(rib, bodyMat);
        }
      }
    }
    if (motif === 'lily' || motif === 'lantern' || motif === 'vespran') {
      const mMat = motif === 'vespran' ? M.trim : M.brass;
      const badge = shadeVerts(new THREE.OctahedronGeometry(0.052, 0), () => 1.0);
      badge.scale(0.85, 1.35, 0.35);
      mesh(badge, mMat, [0, 1.13, 0.215]);
    } else if (motif === 'tarn') {
      mesh(shadeVerts(new THREE.BoxGeometry(0.028, 0.34, 0.024), () => 0.94), M.steel, [0, 1.11, 0.21]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.18, 0.028, 0.024), () => 0.94), M.steel, [0, 1.17, 0.21]);
    } else if (motif === 'hollow') {
      for (const y of [0.96, 1.08, 1.20]) {
        const bar = shadeVerts(new THREE.BoxGeometry(0.28, 0.020, 0.022), () => 0.90);
        mesh(bar, M.brass, [0, y, 0.195]);
      }
    }
  };

  // ---------- CHESTS (24 remaining) ----------
  addPart(root, 'chest_surcoat', (mesh) => {
    // Keep front z <= 0.166 so the 3D heraldic emblem (z=0.186) sits completely proud with zero clipping
    mesh(heightShade(torsoCol(0.182, 0.136, 0.240, 0.144, 0.010, 0.80, 1.36, 6), 0.80, 1.36, 0.82, 1.0), M.cloth);
    mesh(column([[1.31, 0.148, 0.128], [1.37, 0.132, 0.118]], { segs: 6 }), M.steel);
  });

  addPart(root, 'chest_leather', (mesh) => {
    mesh(heightShade(torsoCol(0.185, 0.14, 0.238, 0.158, 0.012, 0.80, 1.36, 6), 0.80, 1.36, 0.80, 1.0), M.leather);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.045, 0.44, 0.024), () => 0.92), M.dark, [sx * 0.10, 1.09, 0.155]);
    }
    mesh(column([[1.32, 0.132, 0.120], [1.36, 0.124, 0.114]], { segs: 6 }), M.clothDark);
  });

  addPart(root, 'chest_mail', (mesh) => {
    mesh(heightShade(torsoCol(0.192, 0.145, 0.244, 0.162, 0.012, 0.80, 1.37, 6), 0.80, 1.37, 0.82, 1.0), M.steel);
    mesh(column([[1.32, 0.146, 0.132], [1.37, 0.136, 0.124]], { segs: 6 }), M.mail);
  });

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
    mesh(heightShade(torsoCol(0.188, 0.142, 0.236, 0.162, 0.010, 0.80, 1.36, 10), 0.80, 1.36, 0.82, 1.0), M.cloth);
    const front = column([
      [0.83, 0.195, 0.155, 0, 0.02],
      [1.12, 0.242, 0.195, 0, 0.032],
      [1.33, 0.205, 0.148, 0, 0.012],
    ], { segs: 8, arc: [Math.PI * 0.08, Math.PI * 0.92] });
    mesh(heightShade(front, 0.83, 1.33, 0.86, 1.0), M.steel);
  });

  const mirrorPlate = (mesh, bodyMat, discMat, ornate = false) => {
    mesh(heightShade(torsoCol(0.19, 0.145, 0.236, 0.165, 0.012, 0.80, 1.37, 10), 0.80, 1.37, 0.82, 1.0), bodyMat);
    for (const sz of [1, -1]) {
      const disc = shadeVerts(new THREE.CylinderGeometry(0.115, 0.122, 0.028, 10), () => 0.98);
      disc.rotateX(Math.PI / 2);
      mesh(disc, discMat, [0, 1.12, sz * 0.172]);
      if (ornate) {
        const ring = shadeVerts(new THREE.TorusGeometry(0.115, 0.012, 4, 10), () => 1.0);
        mesh(ring, M.brass, [0, 1.12, sz * 0.184]);
      }
    }
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.44, 0.016), () => 0.90), M.leather, [sx * 0.14, 1.10, 0.165]);
    }
  };
  addPart(root, 'chest_mirror', (mesh) => mirrorPlate(mesh, M.mail, M.steel, false));
  addPart(root, 'chest_mirror_qadir', (mesh) => mirrorPlate(mesh, M.cloth, M.brass, true));

  const lamellarChest = (mesh, mainMat, trimMat, ornate = false) => {
    mesh(heightShade(torsoCol(0.195, 0.15, 0.242, 0.172, 0.012, 0.80, 1.37, 10), 0.80, 1.37, 0.82, 1.0), mainMat);
    for (const y of [0.92, 1.04, 1.16, 1.28]) {
      const r = y < 1.0 ? 0.215 : 0.248;
      mesh(column([[y - 0.014, r, 0.180], [y + 0.014, r, 0.180]], { segs: 10 }), trimMat);
    }
    if (ornate) {
      const boss = shadeVerts(new THREE.SphereGeometry(0.055, 7, 5), () => 1.0);
      boss.scale(1, 1, 0.45);
      mesh(boss, M.brass, [0, 1.13, 0.195]);
    }
  };
  addPart(root, 'chest_lamellar', (mesh) => lamellarChest(mesh, M.leather, M.mail, false));
  addPart(root, 'chest_lamellar_khan', (mesh) => lamellarChest(mesh, M.steel, M.brass, true));
  addPart(root, 'chest_scale', (mesh) => lamellarChest(mesh, M.mail, M.steel, false));
  addPart(root, 'chest_jarl_scale', (mesh) => lamellarChest(mesh, M.steel, M.brass, true));

  addPart(root, 'chest_robe', (mesh) => {
    const robe = column([
      [0.18, 0.285, 0.225, 0, 0],
      [0.52, 0.245, 0.195, 0, 0],
      [0.82, 0.195, 0.155, 0, 0.01],
      [1.15, 0.235, 0.170, 0, 0.015],
      [1.36, 0.140, 0.125, 0, 0],
    ], { segs: 10 });
    mesh(heightShade(robe, 0.18, 1.36, 0.78, 1.0), M.cloth);
    mesh(shadeVerts(new THREE.BoxGeometry(0.055, 1.12, 0.016), () => 0.96), M.trim, [0, 0.76, 0.20]);
  });

  addPart(root, 'chest_jubbah', (mesh) => {
    mesh(heightShade(torsoCol(0.195, 0.148, 0.238, 0.168, 0.012, 0.80, 1.36, 10), 0.80, 1.36, 0.82, 1.0), M.cloth);
    mesh(shadeVerts(new THREE.BoxGeometry(0.06, 0.50, 0.016), () => 0.96), M.trim, [0, 1.08, 0.168]);
    mesh(column([[1.31, 0.145, 0.132], [1.36, 0.135, 0.124]], { segs: 8 }), M.trim);
  });

  addPart(root, 'chest_tarn_habit', (mesh) => {
    mesh(heightShade(torsoCol(0.19, 0.145, 0.238, 0.168, 0.012, 0.80, 1.37, 10), 0.80, 1.37, 0.80, 1.0), M.clothDark);
    mesh(shadeVerts(new THREE.BoxGeometry(0.14, 0.54, 0.022), () => 0.92), M.cloth, [0, 1.08, 0.172]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.026, 0.34, 0.026), () => 0.96), M.trim, [0, 1.10, 0.180]);
  });

  addPart(root, 'chest_buffcoat', (mesh) => {
    mesh(heightShade(torsoCol(0.195, 0.148, 0.240, 0.170, 0.012, 0.80, 1.38, 10), 0.80, 1.38, 0.82, 1.0), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.042, 0.52, 0.016), () => 0.92), M.trim, [0, 1.09, 0.172]);
    mesh(column([[1.32, 0.148, 0.135], [1.38, 0.136, 0.124]], { segs: 8 }), M.leather);
  });

  addPart(root, 'chest_bones', (mesh) => {
    // Spine column
    mesh(column([[0.68, 0.032, 0.032], [1.36, 0.028, 0.028]], { segs: 6 }), M.skin, [0, 0, -0.05]);
    // Pelvis
    const pelvis = column([[0.66, 0.16, 0.09], [0.80, 0.19, 0.10]], { segs: 8 });
    mesh(pelvis, M.skin);
    // 5 curved ribs + sternum
    for (let i = 0; i < 5; i++) {
      const y = 0.94 + i * 0.082;
      const rx = 0.16 + Math.sin((i / 4) * Math.PI) * 0.045;
      const rib = shadeVerts(new THREE.TorusGeometry(rx, 0.015, 4, 10), () => 0.92);
      rib.scale(1.0, 0.65, 1.0);
      mesh(rib, M.skin, [0, y, 0.02], [Math.PI / 2, 0, 0]);
    }
    mesh(shadeVerts(new THREE.BoxGeometry(0.038, 0.36, 0.022), () => 0.98), M.skin, [0, 1.12, 0.115]);
  });

  // ---------- SHOULDERS (15 remaining) ----------
  const buildPauldrons = (mesh, bodyMat, trimMat, {
    fluted = false, lance = false, hollow = false, yLow = 1.10,
  } = {}) => {
    // Gorget neck ring
    mesh(column([[1.28, 0.175, 0.145], [1.39, 0.135, 0.120]], { segs: 8 }), trimMat);
    for (const sx of [-1, 1]) {
      const extra = lance && sx === -1 ? 1.25 : 1.0;
      const cap = shadeVerts(new THREE.SphereGeometry(0.135 * extra, 8, 6, 0, Math.PI * 2, 0, Math.PI * 0.68), () => 0.95);
      cap.scale(1.0, 1.12, 0.95);
      mesh(cap, bodyMat, [sx * 0.245, 1.25, 0.01], [0, 0, -sx * 0.35]);
      // 2 articulated lames descending the upper arm
      for (let i = 0; i < 2; i++) {
        const lame = shadeVerts(new THREE.CylinderGeometry(0.095, 0.105, 0.055, 7, 1, false, -0.9, 1.8), () => 0.88 + i * 0.04);
        mesh(lame, i === 1 && trimMat !== bodyMat ? trimMat : bodyMat, [sx * 0.285, yLow + 0.08 - i * 0.055, 0.01], [0, sx > 0 ? Math.PI / 2 : -Math.PI / 2, -sx * 0.18]);
      }
      if (fluted) {
        const fin = shadeVerts(new THREE.BoxGeometry(0.022, 0.075, 0.14), () => 1.0);
        mesh(fin, trimMat, [sx * 0.21, 1.38, 0.01], [0, 0, -sx * 0.2]);
      }
      if (hollow) {
        const spike = shadeVerts(new THREE.ConeGeometry(0.022, 0.08, 4), () => 0.96);
        mesh(spike, trimMat, [sx * 0.29, 1.36, 0.01], [0, 0, -sx * 0.5]);
      }
    }
    if (lance) {
      // Left-side jousting grandguard / ecranche (+Z, -X)
      const guard = shadeVerts(new THREE.BoxGeometry(0.16, 0.24, 0.045), () => 0.98);
      mesh(guard, bodyMat, [-0.26, 1.28, 0.18], [0, 0.35, 0]);
    }
  };

  addPart(root, 'shld_pauldrons_gilded', (mesh) => buildPauldrons(mesh, M.steel, M.brass, { yLow: 1.08 }));
  addPart(root, 'shld_pauldrons_fluted', (mesh) => buildPauldrons(mesh, M.steel, M.steel, { fluted: true, yLow: 1.13 }));
  addPart(root, 'shld_pauldrons_dark', (mesh) => buildPauldrons(mesh, M.mail, M.dark, { yLow: 1.14 }));
  addPart(root, 'shld_pauldrons_lance', (mesh) => buildPauldrons(mesh, M.steel, M.brass, { lance: true, yLow: 1.06 }));
  addPart(root, 'shld_pauldrons_hollow', (mesh) => buildPauldrons(mesh, M.mail, M.brass, { hollow: true, yLow: 1.13 }));

  const buildMantle = (mesh, matMain, rxBot, rzBot, yBot, yTop = 1.39, segs = 10) => {
    const g = column([
      [yBot, rxBot, rzBot, 0, 0],
      [(yBot + yTop) * 0.5, rxBot * 0.88, rzBot * 0.88, 0, 0],
      [yTop, 0.145, 0.135, 0, 0],
    ], { segs });
    mesh(heightShade(g, yBot, yTop, 0.80, 1.0), matMain);
  };

  addPart(root, 'shld_mail_aventail', (mesh) => {
    buildMantle(mesh, M.mail, 0.285, 0.210, 1.20, 1.40, 6);
    // Bold 2-tier faceted steel pauldrons for footmen & greatswords (matches reference silhouette)
    for (const s of [-1, 1]) {
      const cap = loft(V(s * 0.175, 1.36, 0.0), V(s * 0.345, 1.22, 0.0), [
        [0.0, 0.112, 0.128],
        [0.48, 0.124, 0.142],
        [1.0, 0.102, 0.118],
      ], { segs: 6, fwd: V(0, 0, 1) });
      mesh(shadeVerts(cap, () => 0.94), M.steel);
      const lame = loft(V(s * 0.245, 1.21, 0.0), V(s * 0.360, 1.13, 0.0), [
        [0.0, 0.100, 0.112],
        [1.0, 0.088, 0.100],
      ], { segs: 6, fwd: V(0, 0, 1) });
      mesh(shadeVerts(lame, () => 0.86), M.steel);
    }
  });
  addPart(root, 'shld_gorget_steel', (mesh) => buildMantle(mesh, M.steel, 0.29, 0.175, 1.19, 1.40));
  addPart(root, 'shld_lamellar', (mesh) => buildMantle(mesh, M.leather, 0.32, 0.16, 1.13, 1.39));
  addPart(root, 'shld_scale', (mesh) => buildMantle(mesh, M.mail, 0.315, 0.145, 1.13, 1.39));
  addPart(root, 'shld_linen_shawl', (mesh) => buildMantle(mesh, M.trim, 0.27, 0.205, 1.22, 1.39));
  addPart(root, 'shld_cowl_cloth', (mesh) => buildMantle(mesh, M.clothDark, 0.28, 0.215, 1.14, 1.39));
  addPart(root, 'shld_tarn_mantle', (mesh) => buildMantle(mesh, M.clothDark, 0.29, 0.225, 1.18, 1.40));

  addPart(root, 'shld_fur_mantle', (mesh) => {
    // Wide angular low-poly draped fur shoulder pelt (6-sided faceted capelet, clearly wider than shoulders)
    const cape = column([
      [1.06, 0.390, 0.270, 0, -0.008],
      [1.20, 0.365, 0.252, 0, -0.006],
      [1.31, 0.280, 0.202, 0, -0.004],
      [1.39, 0.155, 0.142, 0, -0.002],
    ], { segs: 6 });
    mesh(heightShade(cape, 1.06, 1.39, 0.76, 1.0), M.hair);
    // Chunky angular pelt hem tabs around the bottom edge
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2 + Math.PI / 6;
      const tab = shadeVerts(new THREE.ConeGeometry(0.115, 0.15, 4), () => 0.84 + (i % 2) * 0.08);
      mesh(tab, M.hair, [Math.cos(a) * 0.31, 1.08, Math.sin(a) * 0.215 - 0.008], [Math.PI, a, 0]);
    }
    // Bold brass cloak clasp
    mesh(shadeVerts(new THREE.CylinderGeometry(0.042, 0.042, 0.026, 6), () => 1.0), M.brass, [0.12, 1.27, 0.222], [Math.PI / 2, 0, 0]);
  });
  addPart(root, 'shld_fur_collar', (mesh) => {
    buildMantle(mesh, M.hair, 0.25, 0.20, 1.12, 1.39, 8);
    mesh(shadeVerts(new THREE.BoxGeometry(0.08, 0.24, 0.045), () => 0.88), M.hair, [0, 1.02, 0.18]);
  });
  addPart(root, 'shld_wolf_mantle', (mesh) => {
    buildMantle(mesh, M.coat, 0.30, 0.23, 1.16, 1.40, 10);
    // Wolf-pelt clasp & head lump on left shoulder
    const pelt = shadeVerts(new THREE.SphereGeometry(0.075, 6, 5), () => 0.94);
    pelt.scale(1.0, 0.8, 1.35);
    mesh(pelt, M.coatDark, [-0.20, 1.33, 0.12]);
  });

  // ---------- BELTS (8 remaining) ----------
  const beltRing = (mesh, matMain, buckleMat, y0 = 0.79, y1 = 0.87, rx = 0.204, rz = 0.162) => {
    mesh(column([[y0, rx, rz], [y1, rx, rz]], { segs: 8 }), matMain);
    if (buckleMat) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.072, y1 - y0 + 0.016, 0.034), () => 1.0), buckleMat, [0, (y0 + y1) / 2, rz + 0.008]);
    }
  };

  addPart(root, 'belt_pouch', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.79, 0.87, 0.202, 0.160);
    mesh(shadeVerts(new THREE.BoxGeometry(0.11, 0.13, 0.075), () => 0.88), M.leather, [-0.18, 0.74, 0.08]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.09, 0.11, 0.065), () => 0.88), M.leather, [0.16, 0.75, 0.06]);
  });
  addPart(root, 'belt_gilded', (mesh) => beltRing(mesh, M.brass, M.trim, 0.79, 0.87, 0.208, 0.166));
  addPart(root, 'belt_sash_trim', (mesh) => beltRing(mesh, M.trim, M.brass, 0.79, 0.87, 0.208, 0.166));
  addPart(root, 'belt_sash_dark', (mesh) => beltRing(mesh, M.clothDark, M.trim, 0.79, 0.87, 0.208, 0.166));
  addPart(root, 'belt_plate', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.79, 0.87, 0.208, 0.166);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.064, 0.072, 0.026), () => 0.98), M.brass, [sx * 0.125, 0.83, 0.156]);
    }
  });
  addPart(root, 'belt_baldric', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.79, 0.86, 0.204, 0.162);
    // Diagonal shoulder strap hugging chest front & back
    const fStrap = loft(V(-0.18, 1.28, 0.165), V(0.17, 0.83, 0.162), [[0, 0.034, 0.014], [1, 0.034, 0.014]], { segs: 4 });
    const bStrap = loft(V(-0.18, 1.28, -0.155), V(0.17, 0.83, -0.155), [[0, 0.034, 0.014], [1, 0.034, 0.014]], { segs: 4 });
    mesh(fStrap, M.leather);
    mesh(bStrap, M.leather);
  });
  addPart(root, 'belt_cross_straps', (mesh) => {
    beltRing(mesh, M.leather, M.brass, 0.79, 0.86, 0.204, 0.162);
    for (const sx of [-1, 1]) {
      const f = loft(V(-sx * 0.17, 1.24, 0.165), V(sx * 0.17, 0.83, 0.162), [[0, 0.028, 0.014], [1, 0.028, 0.014]], { segs: 4 });
      const b = loft(V(-sx * 0.17, 1.24, -0.155), V(sx * 0.17, 0.83, -0.155), [[0, 0.028, 0.014], [1, 0.028, 0.014]], { segs: 4 });
      mesh(f, M.leather);
      mesh(b, M.leather);
    }
  });
  addPart(root, 'belt_stole', (mesh) => {
    beltRing(mesh, M.clothDark, M.brass, 0.80, 0.86, 0.22, 0.175);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.055, 0.62, 0.018), () => 0.96), M.trim, [sx * 0.09, 0.84, 0.18]);
    }
  });

  // ---------- SKIRTS / FAULDS (12 remaining) ----------
  const buildSkirt = (mesh, mainMat, yBot, yTop = 0.83, rxTop = 0.20, rxBot = 0.255, rzBot = 0.185, trimMat = null, hoops = 0) => {
    const g = column([
      [yBot, rxBot, rzBot, 0, 0],
      [(yBot + yTop) * 0.5, (rxTop + rxBot) * 0.52, rzBot * 0.92, 0, 0],
      [yTop, rxTop, rzBot * 0.78, 0, 0],
    ], { segs: 10 });
    mesh(heightShade(g, yBot, yTop, 0.78, 1.0), mainMat);
    if (trimMat) {
      mesh(column([[yBot, rxBot + 0.006, rzBot + 0.006], [yBot + 0.036, rxBot + 0.004, rzBot + 0.004]], { segs: 10 }), trimMat);
    }
    for (let i = 0; i < hoops; i++) {
      const y = yBot + ((i + 0.5) / hoops) * (yTop - yBot);
      const t = (y - yBot) / (yTop - yBot);
      const rx = rxBot * (1 - t) + rxTop * t + 0.006;
      const rz = rzBot * (1 - t * 0.2) + 0.006;
      mesh(column([[y - 0.010, rx, rz], [y + 0.010, rx, rz]], { segs: 10 }), trimMat ?? M.mail);
    }
  };

  addPart(root, 'skirt_mail', (mesh) => buildSkirt(mesh, M.mail, 0.48, 0.83, 0.195, 0.245, 0.182));
  addPart(root, 'skirt_fauld_steel', (mesh) => buildSkirt(mesh, M.steel, 0.52, 0.83, 0.200, 0.258, 0.178, M.mail, 0));
  addPart(root, 'skirt_fauld_gilded', (mesh) => buildSkirt(mesh, M.steel, 0.52, 0.83, 0.200, 0.258, 0.178, M.brass, 0));
  addPart(root, 'skirt_fauld_dark', (mesh) => buildSkirt(mesh, M.mail, 0.51, 0.83, 0.205, 0.258, 0.178, M.dark, 3));
  addPart(root, 'skirt_fauld_long', (mesh) => buildSkirt(mesh, M.steel, 0.43, 0.83, 0.205, 0.262, 0.182, M.mail, 4));
  addPart(root, 'skirt_fauld_hollow', (mesh) => buildSkirt(mesh, M.mail, 0.52, 0.80, 0.20, 0.24, 0.17, M.brass, 2));
  addPart(root, 'skirt_lamellar', (mesh) => buildSkirt(mesh, M.leather, 0.46, 0.83, 0.20, 0.25, 0.19, M.mail, 3));
  addPart(root, 'skirt_scale', (mesh) => buildSkirt(mesh, M.mail, 0.52, 0.83, 0.20, 0.25, 0.18, M.steel, 2));
  addPart(root, 'skirt_deel', (mesh) => buildSkirt(mesh, M.cloth, 0.40, 0.83, 0.20, 0.27, 0.21, M.trim, 0));
  addPart(root, 'skirt_kaftan', (mesh) => buildSkirt(mesh, M.cloth, 0.38, 0.81, 0.20, 0.27, 0.21, M.clothDark, 0));

  addPart(root, 'skirt_tabard', (mesh) => {
    for (const sz of [1, -1]) {
      mesh(heightShade(new THREE.BoxGeometry(0.19, 0.38, 0.018), -0.19, 0.19, 0.84, 1.0), M.cloth, [0, 0.63, sz * 0.195]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.19, 0.032, 0.022), () => 0.98), M.trim, [0, 0.455, sz * 0.195]);
    }
  });
  addPart(root, 'skirt_tarn_scapular', (mesh) => {
    buildSkirt(mesh, M.clothDark, 0.50, 0.82, 0.195, 0.23, 0.175);
    for (const sz of [1, -1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.16, 0.40, 0.020), () => 0.94), M.cloth, [0, 0.62, sz * 0.195]);
    }
  });
}
