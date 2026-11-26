import { THREE, V, M, shadeVerts, heightShade, loft, pathLoft, drapeSheet, column, addPart } from '../geom.mjs';

export function buildMounts(root) {
  // Small indexed sheet helpers for the broad planar cloth, armour and fur shapes that carry the battle-distance read.
  const sheet = (rows, flip = false) => {
    const cols = rows[0].length, pos = [], idx = [];
    for (const row of rows) for (const p of row) pos.push(p.x, p.y, p.z);
    for (let r = 0; r + 1 < rows.length; r++) for (let c = 0; c + 1 < cols; c++) {
      const A = r * cols + c, B = A + 1, C = (r + 1) * cols + c, D = C + 1;
      if (flip) idx.push(A, B, D, A, D, C);
      else idx.push(A, D, B, A, C, D);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    return g;
  };
  const bandY = (pts, width, flip = false) => sheet([
    pts.map(p => V(p.x, p.y + width * 0.5, p.z)),
    pts.map(p => V(p.x, p.y - width * 0.5, p.z)),
  ], flip);
  const apronPanel = (out, yTop, yBot, zTop, zBot, wTop, wBot) => {
    const rows = [];
    for (let r = 0; r < 3; r++) {
      const k = r / 2, y = yTop + (yBot - yTop) * k, z = zTop + (zBot - zTop) * k, w = wTop + (wBot - wTop) * k;
      const row = [];
      for (let c = 0; c <= 6; c++) {
        const u = -1 + 2 * c / 6;
        // Slightly bellied centre and tucked corners keep the front/croup cloth from reading as a cardboard rectangle.
        row.push(V(u * w, y, z + out * 0.022 * (1 - u * u) * (1 - k * 0.35)));
      }
      rows.push(row);
    }
    return sheet(rows, out < 0);
  };
  const tri = (a, b, c) => {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute([a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z], 3));
    g.computeVertexNormals();
    return g;
  };

  // ---------- HORSES (15 parts) ----------

  // Faceted equine barrel along Z: broad deep chest and withers at +Z, a saddle hollow, tucked loin and powerful croup at -Z.
  const barrelProfile = (th, t) => {
    const sy = Math.sin(th); // +1 spine, -1 belly
    let mx = 1.0, mz = 1.0;
    if (sy > 0.30) {
      mx *= 0.82; // flatter spine planes
      if (t > 0.60 && t < 0.88) mz *= 1.10; // withers rise
    }
    if (sy < -0.20) {
      mx *= 0.88;
      if (t > 0.20 && t < 0.54) mz *= 0.78; // tucked flank
      else if (t >= 0.66) mz *= 1.14; // deep cavalry chest/brisket
    }
    return [mx, mz];
  };

  const horseBarrel = (mesh, bodyMat = M.coat) => {
    const body = loft(V(0, 0.03, -0.95), V(0, 0.08, 0.93), [
      [0.00, 0.12, 0.14, 0, -0.01], // tail dock
      [0.09, 0.28, 0.29, 0,  0.04], // rounded croup
      [0.23, 0.34, 0.32, 0,  0.04], // broad haunches
      [0.41, 0.295, 0.265, 0, -0.02], // tucked loin/flank
      [0.58, 0.305, 0.275, 0, -0.03], // saddle hollow and ribcage
      [0.76, 0.34, 0.325, 0,  0.05], // muscular shoulder/withers
      [0.91, 0.295, 0.295, 0,  0.05], // proud breast
      [1.00, 0.135, 0.155, 0,  0.00], // brisket front
    ], { segs: 8, fwd: V(0, 1, 0), profile: barrelProfile });
    mesh(heightShade(body, -0.30, 0.43, 0.72, 1.0), bodyMat);
  };

  const horseSaddle = (mesh, padMat = M.cloth, saddleMat = M.leather, withTack = true) => {
    // Cloth pad draped over the barrel, with a bright woven border on both hanging sides.
    const cloth = drapeSheet([
      { z: -0.45, w: 0.345, topY: 0.355, sideY: 0.16, hemY: -0.035, flare: 0.014 },
      { z: -0.22, w: 0.335, topY: 0.335, sideY: 0.14, hemY: -0.060, flare: 0.014 },
      { z:  0.03, w: 0.335, topY: 0.325, sideY: 0.13, hemY: -0.060, flare: 0.014 },
      { z:  0.26, w: 0.345, topY: 0.355, sideY: 0.15, hemY: -0.035, flare: 0.012 },
      { z:  0.40, w: 0.330, topY: 0.375, sideY: 0.18, hemY:  0.020, flare: 0.010 },
    ], { segs: 8 });
    mesh(heightShade(cloth, -0.07, 0.39, 0.80, 1.0), padMat);
    for (const sx of [-1, 1]) {
      const pts = [
        V(sx * 0.355, -0.015, -0.44), V(sx * 0.345, -0.040, -0.20), V(sx * 0.345, -0.040, 0.05),
        V(sx * 0.355, -0.015, 0.28), V(sx * 0.340, 0.040, 0.40),
      ];
      mesh(shadeVerts(bandY(pts, 0.034, sx > 0), () => 0.96), M.trim);
    }

    // Low but unmistakable medieval war saddle: padded seat, raised pommel and cantle rather than tall floating horns.
    const seat = drapeSheet([
      { z: -0.27, w: 0.245, topY: 0.382, sideY: 0.28, hemY: 0.18, flare: 0.014 },
      { z: -0.03, w: 0.235, topY: 0.360, sideY: 0.26, hemY: 0.15, flare: 0.014 },
      { z:  0.20, w: 0.238, topY: 0.382, sideY: 0.28, hemY: 0.18, flare: 0.014 },
    ], { segs: 7 });
    mesh(heightShade(seat, 0.14, 0.40, 0.78, 0.96), saddleMat);

    const pommel = loft(V(-0.205, 0.392, 0.205), V(0.205, 0.392, 0.205), [
      [0.00, 0.048, 0.024, 0, -0.004],
      [0.50, 0.074, 0.046, 0,  0.010],
      [1.00, 0.048, 0.024, 0, -0.004],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(heightShade(pommel, 0.35, 0.46, 0.80, 1.0), saddleMat);
    const cantle = loft(V(-0.215, 0.395, -0.265), V(0.215, 0.395, -0.265), [
      [0.00, 0.050, 0.026, 0, -0.004],
      [0.50, 0.078, 0.050, 0,  0.011],
      [1.00, 0.050, 0.026, 0, -0.004],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(heightShade(cantle, 0.35, 0.47, 0.80, 1.0), saddleMat);

    if (withTack) {
      // Broad girth, breastcollar and stirrups: large enough to read, no jewellery-scale tack.
      const girth = loft(V(0, 0.05, 0.09), V(0, 0.05, 0.18), [
        [0, 0.325, 0.285], [1, 0.325, 0.285],
      ], { segs: 8, fwd: V(0, 1, 0), caps: false });
      mesh(heightShade(girth, -0.26, 0.34, 0.72, 0.90), M.leather);

      const breast = pathLoft([
        { p: V(-0.31, 0.20, 0.31), rx: 0.021, rz: 0.032 },
        { p: V(-0.28, 0.12, 0.64), rx: 0.023, rz: 0.036 },
        { p: V( 0.00, 0.065, 0.92), rx: 0.026, rz: 0.042 },
        { p: V( 0.28, 0.12, 0.64), rx: 0.023, rz: 0.036 },
        { p: V( 0.31, 0.20, 0.31), rx: 0.021, rz: 0.032 },
      ], { segs: 4, fwd: V(0, 1, 0), caps: false });
      mesh(shadeVerts(breast, () => 0.84), M.leather);
      const boss = shadeVerts(new THREE.OctahedronGeometry(0.060, 0), () => 0.98);
      mesh(boss, M.brass, [0, 0.065, 0.935], null, [1.0, 0.72, 0.38]);

      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.024, 0.34, 0.034), () => 0.80), M.leather, [sx * 0.345, 0.015, 0.06]);
        mesh(shadeVerts(new THREE.TorusGeometry(0.046, 0.011, 3, 5), () => 0.92), M.steel, [sx * 0.352, -0.165, 0.06], [0, Math.PI / 2, 0]);
      }
    }
  };

  // Full caparison: broad team-coloured cloth planes following chest, ribcage and croup, with a shaped lifted hem and bold trim.
  const buildFullCaparison = (mesh, clothMat, trimMat, hemY = -0.40, pleat = 0.010) => {
    const st = (z, w, topY, sideY, dy, flare, pl = pleat) => ({ z, w, topY, sideY, hemY: hemY + dy, flare, pleat: pl });
    const stations = [
      st(-0.94, 0.17, 0.25, 0.055, 0.150, 0.018, pleat * 0.4),
      st(-0.76, 0.34, 0.385, 0.145, 0.065, 0.038),
      st(-0.43, 0.375, 0.390, 0.145, 0.000, 0.046),
      st( 0.00, 0.365, 0.360, 0.125, -0.010, 0.046),
      st( 0.43, 0.380, 0.410, 0.155, 0.000, 0.046),
      st( 0.76, 0.350, 0.410, 0.155, 0.060, 0.038),
      st( 0.94, 0.190, 0.290, 0.075, 0.145, 0.020, pleat * 0.4),
    ];
    const skirt = drapeSheet(stations, { segs: 8 });
    mesh(heightShade(skirt, hemY - 0.03, 0.43, 0.72, 1.0), clothMat);

    // Sloped cloth closes the chest and croup, so the silhouette tapers like a housed horse instead of ending in vertical box walls.
    mesh(heightShade(apronPanel( 1, 0.30, hemY + 0.13, 0.96, 0.75, 0.285, 0.165), hemY, 0.31, 0.74, 0.96), clothMat);
    mesh(heightShade(apronPanel(-1, 0.27, hemY + 0.13, -0.95, -0.73, 0.270, 0.155), hemY, 0.29, 0.72, 0.94), clothMat);

    // Wide flat woven trim follows the actual hemline on both sides and across both sloped ends.
    for (const sx of [-1, 1]) {
      const pts = stations.map(s => V(sx * (s.w + s.flare + 0.010), s.hemY + 0.035, s.z));
      mesh(shadeVerts(bandY(pts, 0.060, sx > 0), () => 0.96), trimMat);
    }
    const frontPts = [];
    const rearPts = [];
    for (let i = 0; i <= 6; i++) {
      const u = -1 + 2 * i / 6;
      frontPts.push(V(u * 0.165, hemY + 0.155 - 0.020 * (1 - u * u), 0.75 + 0.012 * (1 - u * u)));
      rearPts.push(V(u * 0.155, hemY + 0.155 - 0.020 * (1 - u * u), -0.73 - 0.012 * (1 - u * u)));
    }
    mesh(shadeVerts(bandY(frontPts, 0.055), () => 0.96), trimMat);
    mesh(shadeVerts(bandY(rearPts, 0.055, true), () => 0.94), trimMat);

    // Short vertical border welts complete the cloth outline where the side skirts meet the chest/croup aprons.
    for (const sx of [-1, 1]) {
      for (const front of [true, false]) {
        const out = front ? 1 : -1;
        const seam = pathLoft([
          { p: V(sx * (front ? 0.250 : 0.238), front ? 0.285 : 0.260, out * (front ? 0.920 : 0.910)), rx: 0.012, rz: 0.020 },
          { p: V(sx * (front ? 0.168 : 0.158), hemY + 0.155, out * (front ? 0.750 : 0.730)), rx: 0.012, rz: 0.020 },
        ], { segs: 4, fwd: V(0, 0, 1), caps: false });
        mesh(shadeVerts(seam, () => 0.96), trimMat);
      }
    }
  };

  addPart(root, 'horse_body', (mesh) => {
    horseBarrel(mesh, M.coat);
    horseSaddle(mesh, M.clothDark, M.leather, true);
  });

  addPart(root, 'horse_body_barded', (mesh) => {
    horseBarrel(mesh, M.coat);
    buildFullCaparison(mesh, M.cloth, M.trim, -0.405, 0.010);
    horseSaddle(mesh, M.clothDark, M.leather, false);
  });

  addPart(root, 'horse_body_half', (mesh) => {
    // Half-barding: a bold angular steel peytral in front, a team cloth crupper behind, and the ribcage left visibly bare.
    horseBarrel(mesh, M.coat);
    const peytral = drapeSheet([
      { z: 0.40, w: 0.365, topY: 0.405, sideY: 0.16, hemY: -0.10, flare: 0.018 },
      { z: 0.68, w: 0.350, topY: 0.390, sideY: 0.14, hemY: -0.17, flare: 0.024 },
      { z: 0.91, w: 0.225, topY: 0.290, sideY: 0.09, hemY: -0.13, flare: 0.018 },
    ], { segs: 8 });
    mesh(heightShade(peytral, -0.20, 0.42, 0.78, 1.0), M.steel);
    mesh(heightShade(apronPanel(1, 0.29, -0.14, 0.90, 0.82, 0.235, 0.185), -0.16, 0.30, 0.80, 1.0), M.steel);
    const boss = shadeVerts(new THREE.OctahedronGeometry(0.068, 0), () => 0.98);
    mesh(boss, M.brass, [0, 0.055, 0.955], null, [1.0, 0.75, 0.42]);

    const crupper = drapeSheet([
      { z: -0.93, w: 0.19, topY: 0.25, sideY: 0.06, hemY: -0.13, flare: 0.020 },
      { z: -0.74, w: 0.35, topY: 0.38, sideY: 0.15, hemY: -0.22, flare: 0.034, pleat: 0.006 },
      { z: -0.43, w: 0.36, topY: 0.37, sideY: 0.15, hemY: -0.18, flare: 0.026, pleat: 0.006 },
    ], { segs: 8 });
    mesh(heightShade(crupper, -0.25, 0.40, 0.76, 1.0), M.cloth);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(bandY([
        V(sx * 0.205, -0.105, -0.91), V(sx * 0.380, -0.195, -0.72), V(sx * 0.382, -0.155, -0.43),
      ], 0.050, sx > 0), () => 0.96), M.trim);
    }
    horseSaddle(mesh, M.cloth, M.leather, true);
  });

  addPart(root, 'horse_body_scale', (mesh) => {
    horseBarrel(mesh, M.coat);
    // Cataphract scale skirt: same strong housed silhouette, shorter hem so dark legs and hooves separate underneath.
    buildFullCaparison(mesh, M.mail, M.brass, -0.355, 0.004);
    const boss = shadeVerts(new THREE.OctahedronGeometry(0.074, 0), () => 0.98);
    mesh(boss, M.brass, [0, 0.055, 0.945], null, [1.0, 0.75, 0.42]);
    horseSaddle(mesh, M.clothDark, M.leather, false);
  });

  addPart(root, 'horse_body_fur', (mesh) => {
    horseBarrel(mesh, M.coat);
    // Steppe/northern hide caparison: a heavy hide sheet with a few large ragged locks, readable as fur without dense scalloping.
    const pelt = drapeSheet([
      { z: -0.82, w: 0.34, topY: 0.375, sideY: 0.15, hemY: -0.09, flare: 0.030, pleat: 0.012 },
      { z: -0.43, w: 0.365, topY: 0.375, sideY: 0.15, hemY: -0.15, flare: 0.036, pleat: 0.012 },
      { z:  0.00, w: 0.355, topY: 0.345, sideY: 0.13, hemY: -0.13, flare: 0.034, pleat: 0.012 },
      { z:  0.43, w: 0.365, topY: 0.405, sideY: 0.16, hemY: -0.13, flare: 0.036, pleat: 0.012 },
      { z:  0.76, w: 0.325, topY: 0.380, sideY: 0.15, hemY: -0.07, flare: 0.026, pleat: 0.008 },
    ], { segs: 8 });
    mesh(heightShade(pelt, -0.18, 0.42, 0.70, 0.96), M.leather);
    for (const sx of [-1, 1]) {
      const x = sx * 0.385;
      mesh(shadeVerts(bandY([
        V(x, -0.085, -0.78), V(x + sx * 0.010, -0.140, -0.40), V(x, -0.125, 0.00),
        V(x + sx * 0.010, -0.125, 0.40), V(x, -0.070, 0.74),
      ], 0.038, sx > 0), () => 0.72), M.coatDark);
      for (const [z, y, drop, len, swing] of [
        [-0.62, -0.105, 0.135, 0.20, -0.055], [-0.24, -0.145, 0.165, 0.18, 0.050],
        [ 0.14, -0.130, 0.120, 0.17, -0.040], [ 0.52, -0.085, 0.145, 0.19, 0.060],
      ]) {
        const a = V(x, y, z - len * 0.5), b = V(x, y, z + len * 0.5), c = V(x + sx * 0.038, y - drop, z + swing);
        mesh(sx > 0 ? tri(a, b, c) : tri(b, a, c), M.coatDark);
      }
    }
    horseSaddle(mesh, M.cloth, M.leather, true);
  });

  // Arched horse neck (normalized Y, +Z throat / -Z crest). Puppet3D stretches this along the neck bone.
  const buildHorseNeck = (mesh, neckMat, armorMat = null) => {
    const neckProfile = (th) => {
      const sz = Math.sin(th);
      return [sz < -0.15 ? 0.76 : 1.0, sz < 0 ? 1.14 : 0.92];
    };
    const neck = loft(V(0, -0.13, 0), V(0, 1.06, 0), [
      [0.00, 0.235, 0.315, 0,  0.015], // deep muscular base at chest/withers
      [0.25, 0.205, 0.275, 0, -0.035],
      [0.55, 0.165, 0.225, 0, -0.085], // high arched crest
      [0.82, 0.135, 0.185, 0, -0.060],
      [1.00, 0.115, 0.150, 0, -0.005], // poll socket
    ], { segs: 8, profile: neckProfile });
    mesh(heightShade(neck, -0.13, 1.06, 0.76, 1.0), neckMat);

    // Flattened dark crest mane: broad in side view, narrow across the neck, with an uneven but not fussy silhouette.
    const mane = pathLoft([
      { p: V(0, 0.00, -0.285), rx: 0.026, rz: 0.085 },
      { p: V(0, 0.28, -0.335), rx: 0.030, rz: 0.110 },
      { p: V(0, 0.58, -0.305), rx: 0.028, rz: 0.105 },
      { p: V(0, 0.85, -0.230), rx: 0.024, rz: 0.085 },
      { p: V(0, 1.04, -0.140), rx: 0.018, rz: 0.058 },
    ], { segs: 5, fwd: V(0, 0, 1) });
    mesh(heightShade(mane, 0.0, 1.04, 0.72, 0.96), M.coatDark);

    if (armorMat) {
      // Four broad overlapping crinet plates; their overlap reads as articulated armour, not zebra striping.
      const lames = [
        [0.17, 0.245, 0.305, -0.010, 0.14],
        [0.39, 0.220, 0.275, -0.045, 0.15],
        [0.62, 0.190, 0.235, -0.070, 0.14],
        [0.84, 0.155, 0.195, -0.050, 0.12],
      ];
      for (const [y, rx, rz, oz, half] of lames) {
        const lame = loft(V(0, y - half, oz), V(0, y + half, oz), [
          [0.0, rx * 1.02, rz * 1.02],
          [0.5, rx * 1.05, rz * 1.05],
          [1.0, rx * 0.96, rz * 0.96],
        ], { segs: 7, arc: [-Math.PI * 0.84, Math.PI * 0.84], caps: false });
        mesh(heightShade(lame, y - half, y + half, 0.82, 1.0), armorMat);
      }
    }
  };
  addPart(root, 'horse_neck', (mesh) => buildHorseNeck(mesh, M.coat));
  addPart(root, 'horse_neck_barded', (mesh) => buildHorseNeck(mesh, M.cloth, M.steel));
  addPart(root, 'horse_neck_scale', (mesh) => buildHorseNeck(mesh, M.mail, M.mail));

  // Angular equine head: broad brow/jowls, long flat nasal planes, tapered muzzle, pointed ears, dark eye/nostril accents.
  const buildHorseHead = (mesh, shaffronMat = null) => {
    const headProfile = (th, t) => {
      const sy = Math.sin(th); // +1 forehead/bridge, -1 cheek/jaw
      let mx = 1.0, mz = 1.0;
      if (t > 0.10 && t < 0.43 && sy < -0.10) {
        mx *= 1.10;
        mz *= 1.34; // big equine jowl/cheek plane
      }
      if (t > 0.36 && t < 0.88 && sy > 0.28) mx *= 0.78; // flat nasal bridge
      if (t >= 0.82 && Math.abs(Math.cos(th)) > 0.45) mx *= 1.14; // nostril flare
      return [mx, mz];
    };

    const head = loft(V(0, 0.045, -0.10), V(0, -0.025, 0.62), [
      [0.00, 0.112, 0.128, 0,  0.005], // poll/occiput
      [0.17, 0.148, 0.158, 0, -0.005], // broad brow and jowl root
      [0.35, 0.132, 0.142, 0, -0.015], // cheekbone taper
      [0.62, 0.086, 0.102, 0, -0.005], // flat nasal bridge
      [0.86, 0.078, 0.088, 0, -0.005], // flared nostrils/muzzle
      [1.00, 0.064, 0.070, 0, -0.012], // lips/chin
    ], { segs: 8, fwd: V(0, 1, 0), profile: headProfile });
    mesh(heightShade(head, -0.18, 0.21, 0.76, 1.0), M.coat);

    // Dark chin/lower lip accent and a compact forelock; both read at distance without adding fragile tack.
    mesh(shadeVerts(new THREE.BoxGeometry(0.092, 0.024, 0.105), () => 0.72), M.coatDark, [0, -0.078, 0.535]);
    const forelock = loft(V(0, 0.172, -0.025), V(0, 0.142, 0.145), [
      [0, 0.043, 0.030], [1, 0.012, 0.012],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(shadeVerts(forelock, () => 0.82), M.coatDark);

    for (const sx of [-1, 1]) {
      // Taller alert ears: triangular low-poly wedges rather than blobs.
      const ear = loft(V(0, 0, 0), V(0, 0.205, 0), [
        [0.0, 0.040, 0.044],
        [0.55, 0.027, 0.032],
        [1.0, 0.005, 0.007],
      ], { segs: 5 });
      mesh(heightShade(ear, 0, 0.205, 0.80, 0.96), M.coat, [sx * 0.080, 0.140, -0.010], [0.22, sx * 0.16, -sx * 0.20]);

      const eye = shadeVerts(new THREE.OctahedronGeometry(0.027, 0), () => 0.16, 0.12);
      mesh(eye, M.dark, [sx * 0.130, 0.064, 0.165], null, [1.0, 0.70, 0.62]);
      const nostril = shadeVerts(new THREE.OctahedronGeometry(0.020, 0), () => 0.20, 0.12);
      mesh(nostril, M.dark, [sx * 0.064, 0.000, 0.535], null, [1.0, 0.62, 0.70]);
    }

    // Simple bold bridle bands and cheek straps. Bit hardware is a readable boss, not tiny rings.
    const noseband = loft(V(0, -0.012, 0.425), V(0, -0.012, 0.465), [
      [0, 0.092, 0.104], [1, 0.092, 0.104],
    ], { segs: 8, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(noseband, () => 0.82), M.leather);
    const browband = loft(V(0, 0.022, 0.065), V(0, 0.022, 0.105), [
      [0, 0.150, 0.162], [1, 0.150, 0.162],
    ], { segs: 8, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(browband, () => 0.82), M.leather);
    for (const sx of [-1, 1]) {
      const cheek = pathLoft([
        { p: V(sx * 0.135, 0.040, 0.085), rx: 0.011, rz: 0.015 },
        { p: V(sx * 0.104, -0.008, 0.295), rx: 0.011, rz: 0.015 },
        { p: V(sx * 0.088, -0.040, 0.485), rx: 0.011, rz: 0.015 },
      ], { segs: 4, fwd: V(0, 1, 0), caps: false });
      mesh(shadeVerts(cheek, () => 0.82), M.leather);
      const bit = shadeVerts(new THREE.OctahedronGeometry(0.025, 0), () => 0.98);
      mesh(bit, M.brass, [sx * 0.092, -0.045, 0.495], null, [0.70, 1.0, 0.70]);
    }

    if (shaffronMat) {
      // Angular shaffron shell over brow and nasal bridge, ending before the bare soft muzzle.
      const shaf = loft(V(0, 0.064, 0.015), V(0, 0.012, 0.475), [
        [0.00, 0.153, 0.137, 0, 0.024],
        [0.34, 0.146, 0.130, 0, 0.022],
        [0.72, 0.097, 0.094, 0, 0.012],
        [1.00, 0.080, 0.074, 0, 0.008],
      ], { segs: 7, fwd: V(0, 1, 0), arc: [-Math.PI * 0.06, Math.PI * 1.06], caps: false });
      mesh(heightShade(shaf, -0.02, 0.22, 0.84, 1.0), shaffronMat);

      // Central reinforcing ridge and dark rectangular eye slits on the outside of the plate.
      const ridge = loft(V(0, 0.145, 0.055), V(0, 0.092, 0.455), [
        [0, 0.012, 0.014], [0.55, 0.013, 0.016], [1, 0.009, 0.010],
      ], { segs: 4, fwd: V(0, 1, 0) });
      mesh(shadeVerts(ridge, () => 0.98), M.brass);
      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.018, 0.020, 0.072), () => 0.16, 0.10), M.dark, [sx * 0.132, 0.062, 0.180]);
      }
    }
  };
  addPart(root, 'horse_head', (mesh) => buildHorseHead(mesh, null));
  addPart(root, 'horse_head_barded', (mesh) => buildHorseHead(mesh, M.steel));
  addPart(root, 'horse_head_scale', (mesh) => buildHorseHead(mesh, M.mail));

  // Foreleg upper: muscular forearm planes tapering to a crisp knee (normalized Y 0..1 down the bone).
  addPart(root, 'horse_leg_up', (mesh) => {
    const leg = loft(V(0, -0.10, 0), V(0, 1.06, 0), [
      [0.00, 0.135, 0.175, 0,  0.008],
      [0.34, 0.108, 0.138, 0,  0.000],
      [0.76, 0.073, 0.086, 0,  0.000],
      [1.00, 0.068, 0.080, 0,  0.000],
    ], { segs: 6 });
    mesh(heightShade(leg, -0.10, 1.06, 0.76, 1.0), M.coat);
  });

  // Hindleg upper: broad powerful thigh/croup mass narrowing through the gaskin into a sharp hock.
  addPart(root, 'horse_leg_hind_up', (mesh) => {
    const leg = loft(V(0, -0.14, 0), V(0, 1.06, 0), [
      [0.00, 0.165, 0.250, 0, -0.030],
      [0.30, 0.132, 0.198, 0, -0.020],
      [0.68, 0.086, 0.118, 0, -0.008],
      [1.00, 0.067, 0.088, 0, -0.006],
    ], { segs: 6 });
    mesh(heightShade(leg, -0.14, 1.06, 0.74, 1.0), M.coat);
  });

  // Lower leg: lean cannon, fetlock swelling, angled pastern and a broad dark flared hoof with a forward toe.
  addPart(root, 'horse_leg_low', (mesh) => {
    const cannon = loft(V(0, -0.08, 0), V(0, 0.85, 0), [
      [0.00, 0.064, 0.076, 0,  0.000],
      [0.24, 0.046, 0.054, 0,  0.000],
      [0.63, 0.044, 0.052, 0,  0.000],
      [0.78, 0.057, 0.068, 0, -0.010],
      [0.93, 0.047, 0.056, 0,  0.018],
      [1.00, 0.055, 0.066, 0,  0.030],
    ], { segs: 6 });
    mesh(heightShade(cannon, -0.08, 0.85, 0.74, 1.0), M.coat);

    const hoof = loft(V(0, 0.82, 0.026), V(0, 1.04, 0.052), [
      [0.00, 0.058, 0.068],
      [0.55, 0.071, 0.086],
      [1.00, 0.084, 0.104],
    ], { segs: 6 });
    mesh(shadeVerts(hoof, (_x, y) => 0.34 + 0.14 * (1.04 - y) / 0.22, 0.22), M.dark);
  });

  // Flattened flowing tail: narrow across the body, deep and expressive in side view, pointed feathered tip.
  addPart(root, 'horse_tail', (mesh) => {
    const tail = pathLoft([
      { p: V(0, -0.055,  0.000), rx: 0.026, rz: 0.044 },
      { p: V(0,  0.180, -0.050), rx: 0.034, rz: 0.074 },
      { p: V(0,  0.500, -0.095), rx: 0.042, rz: 0.105 },
      { p: V(0,  0.790, -0.070), rx: 0.034, rz: 0.085 },
      { p: V(0,  1.040,  0.010), rx: 0.012, rz: 0.026 },
    ], { segs: 5, fwd: V(0, 0, 1) });
    mesh(heightShade(tail, -0.06, 1.04, 0.68, 1.0), M.coatDark);
  });

  // ---------- CAMELS (3 parts; legs/tail are shared with horses in Puppet3D) ----------
  addPart(root, 'camel_body', (mesh) => {
    // Narrower, longer-bodied dromedary barrel with a high shoulder and sparse belly.
    const body = loft(V(0, 0.035, -0.88), V(0, 0.075, 0.86), [
      [0.00, 0.12, 0.135, 0, -0.010],
      [0.15, 0.255, 0.270, 0,  0.020],
      [0.43, 0.285, 0.285, 0, -0.015],
      [0.70, 0.275, 0.285, 0,  0.015],
      [0.90, 0.245, 0.250, 0,  0.020],
      [1.00, 0.120, 0.135, 0,  0.000],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.27, 0.39, 0.76, 1.0), M.coat);

    // One tall teardrop dromedary hump, broad front-to-back and clearly separate from the saddle cloth.
    const hump = column([
      [0.20, 0.270, 0.455, 0, 0.070],
      [0.38, 0.250, 0.395, 0, 0.082],
      [0.56, 0.190, 0.285, 0, 0.098],
      [0.70, 0.095, 0.145, 0, 0.112],
      [0.755, 0.038, 0.060, 0, 0.120],
    ], { segs: 7 });
    mesh(heightShade(hump, 0.20, 0.755, 0.80, 1.0), M.coat);

    // Bright woven desert blanket with a broad trim edge, then a low leather saddle frame nested behind/around the hump.
    const blanket = drapeSheet([
      { z: -0.46, w: 0.315, topY: 0.405, sideY: 0.19, hemY: 0.015, flare: 0.018 },
      { z: -0.12, w: 0.335, topY: 0.445, sideY: 0.21, hemY: -0.005, flare: 0.020 },
      { z:  0.22, w: 0.325, topY: 0.430, sideY: 0.20, hemY: 0.000, flare: 0.018 },
      { z:  0.42, w: 0.300, topY: 0.390, sideY: 0.18, hemY: 0.025, flare: 0.014 },
    ], { segs: 8 });
    mesh(heightShade(blanket, 0.0, 0.46, 0.82, 1.0), M.cloth);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(bandY([
        V(sx * 0.330, 0.035, -0.45), V(sx * 0.350, 0.015, -0.10), V(sx * 0.340, 0.020, 0.23), V(sx * 0.312, 0.045, 0.42),
      ], 0.045, sx > 0), () => 0.96), M.trim);
    }
    const seat = drapeSheet([
      { z: -0.36, w: 0.230, topY: 0.500, sideY: 0.42, hemY: 0.34, flare: 0.010 },
      { z: -0.13, w: 0.225, topY: 0.535, sideY: 0.45, hemY: 0.36, flare: 0.010 },
      { z:  0.08, w: 0.220, topY: 0.500, sideY: 0.42, hemY: 0.34, flare: 0.010 },
    ], { segs: 7 });
    mesh(heightShade(seat, 0.34, 0.55, 0.76, 0.94), M.leather);
  });

  addPart(root, 'camel_neck', (mesh) => {
    // Long narrow S-curve: throat reaches forward low down, then the poll draws back. Puppet3D supplies the extra length.
    const neck = loft(V(0, -0.08, 0), V(0, 1.06, 0), [
      [0.00, 0.152, 0.195, 0,  0.020],
      [0.20, 0.132, 0.165, 0,  0.092],
      [0.48, 0.113, 0.138, 0,  0.128],
      [0.74, 0.100, 0.123, 0,  0.070],
      [1.00, 0.088, 0.108, 0, -0.025],
    ], { segs: 7 });
    mesh(heightShade(neck, -0.08, 1.06, 0.78, 1.0), M.coat);
  });

  addPart(root, 'camel_head', (mesh) => {
    // Small high cranium tapering to a narrow drooping muzzle; unmistakably camel beside the longer horse head.
    const headProfile = (th, t) => {
      const sy = Math.sin(th);
      let mx = 1.0, mz = 1.0;
      if (t < 0.34 && sy > 0.2) mx *= 1.08; // high domed brow
      if (t > 0.55 && sy < -0.1) mz *= 1.12; // mobile upper lip/muzzle pad
      return [mx, mz];
    };
    const head = loft(V(0, 0.045, -0.08), V(0, -0.040, 0.48), [
      [0.00, 0.104, 0.116, 0,  0.012],
      [0.25, 0.130, 0.132, 0,  0.010],
      [0.56, 0.094, 0.102, 0, -0.002],
      [0.84, 0.086, 0.098, 0, -0.018],
      [1.00, 0.062, 0.068, 0, -0.030],
    ], { segs: 7, fwd: V(0, 1, 0), profile: headProfile });
    mesh(heightShade(head, -0.12, 0.18, 0.78, 1.0), M.coat);

    for (const sx of [-1, 1]) {
      // Small rounded ears folded back along the poll, high dark eyes and slit nostrils.
      const ear = loft(V(0, 0, 0), V(0, 0.075, 0), [
        [0, 0.030, 0.033], [0.65, 0.021, 0.024], [1, 0.006, 0.008],
      ], { segs: 5 });
      mesh(shadeVerts(ear, () => 0.90), M.coat, [sx * 0.080, 0.122, -0.040], [-0.44, sx * 0.24, -sx * 0.24]);
      const eye = shadeVerts(new THREE.OctahedronGeometry(0.023, 0), () => 0.16, 0.12);
      mesh(eye, M.dark, [sx * 0.110, 0.062, 0.132], null, [1.0, 0.68, 0.62]);
      const nostril = shadeVerts(new THREE.OctahedronGeometry(0.016, 0), () => 0.18, 0.12);
      mesh(nostril, M.dark, [sx * 0.046, -0.020, 0.402], null, [1.0, 0.55, 0.75]);
    }
    // Drooping dark lower-lip line plus a simple decorated desert noseband/browband.
    mesh(shadeVerts(new THREE.BoxGeometry(0.075, 0.018, 0.070), () => 0.68), M.coatDark, [0, -0.094, 0.410]);
    const noseband = loft(V(0, -0.018, 0.305), V(0, -0.018, 0.342), [
      [0, 0.086, 0.094], [1, 0.086, 0.094],
    ], { segs: 7, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(noseband, () => 0.88), M.cloth);
    const browband = loft(V(0, 0.030, 0.070), V(0, 0.030, 0.105), [
      [0, 0.123, 0.126], [1, 0.123, 0.126],
    ], { segs: 7, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(browband, () => 0.84), M.leather);
  });
}
