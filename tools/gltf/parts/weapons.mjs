import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildWeapons(root) {
  addPart(root, 'shield_round', (mesh) => {
    const face = lathe([
      [0.0, 0.032],
      [0.29, 0.022],
      [0.315, 0.018],
      [0.315, 0.0],
      [0.0, 0.008],
    ], 16, 0.86, 1.0);
    face.rotateX(Math.PI / 2);
    mesh(face, M.cloth);

    const rim = new THREE.TorusGeometry(0.308, 0.016, 4, 10);
    mesh(shadeVerts(rim, () => 0.88), M.mail, [0, 0, 0.014]);

    const boss = lathe([
      [0.0, 0.102],
      [0.055, 0.088],
      [0.082, 0.034],
      [0.102, 0.028],
    ], 12, 0.88, 1.0);
    boss.rotateX(Math.PI / 2);
    mesh(boss, M.steel);
  });

  // 1. weapon_axe (edgeAlongZ = true)
  addPart(root, 'weapon_axe', (mesh) => {
    const haft = column([
      [-0.22, 0.018, 0.022],
      [0.0, 0.019, 0.023],
      [0.52, 0.020, 0.025],
      [0.79, 0.022, 0.028],
    ], { segs: 8 });
    mesh(haft, M.wood);
    const grip = column([[-0.10, 0.022, 0.026], [0.10, 0.022, 0.026]], { segs: 8 });
    mesh(grip, M.leather);
    const socket = shadeVerts(new THREE.BoxGeometry(0.046, 0.11, 0.068), () => 0.88);
    mesh(socket, M.mail, [0, 0.71, 0.008]);
    // Bearded axe blade extending toward +Z (extrude along X, rotated so shape X -> +Z)
    const blade = bladeExtrude([
      [0.03, 0.67],
      [0.15, 0.62],
      [0.185, 0.67],
      [0.175, 0.77],
      [0.145, 0.80],
      [0.03, 0.75],
    ], 0.018, 0.005);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // 2. weapon_daneaxe (edgeAlongZ = true)
  addPart(root, 'weapon_daneaxe', (mesh) => {
    const haft = column([
      [-0.58, 0.022, 0.026],
      [0.0, 0.024, 0.028],
      [1.38, 0.026, 0.030],
    ], { segs: 6 });
    mesh(haft, M.wood);
    const langets = shadeVerts(new THREE.BoxGeometry(0.056, 0.36, 0.068), () => 0.88);
    mesh(langets, M.mail, [0, 1.24, 0]);
    const blade = bladeExtrude([
      [0.028, 1.16],
      [0.25, 1.02],
      [0.31, 1.14],
      [0.30, 1.40],
      [0.22, 1.47],
      [0.028, 1.34],
    ], 0.022, 0.006);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // 3. weapon_spear & weapon_pike (edgeAlongZ = true)
  for (const [name, yMin, yMax] of [['weapon_spear', -0.68, 1.46], ['weapon_pike', -0.94, 1.98]]) {
    addPart(root, name, (mesh) => {
      const shaft = column([[yMin, 0.018, 0.018], [0, 0.020, 0.020], [yMax, 0.019, 0.019]], { segs: 8 });
      mesh(shaft, M.wood);
      const butt = lathe([[0.0, yMin - 0.08], [0.021, yMin]], 8);
      mesh(butt, M.mail);
      const socket = lathe([[0.024, yMax - 0.04], [0.020, yMax + 0.08], [0.026, yMax + 0.09]], 8);
      mesh(socket, M.mail);
      const head = bladeExtrude([
        [-0.036, yMax + 0.09],
        [-0.044, yMax + 0.16],
        [0.0, yMax + 0.33],
        [0.044, yMax + 0.16],
        [0.036, yMax + 0.09],
      ], 0.016, 0.005);
      mesh(head, M.steel);
    });
  }

  // 4. Straight swords: weapon_sword, weapon_longsword, weapon_greatsword (edgeAlongZ = false, edges +-X)
  const swords = [
    { name: 'weapon_sword', gripLo: -0.16, bladeHi: 0.88, guardW: 0.25, bladeW: 0.058, zweihander: false },
    { name: 'weapon_longsword', gripLo: -0.26, bladeHi: 1.16, guardW: 0.31, bladeW: 0.062, zweihander: false },
    { name: 'weapon_greatsword', gripLo: -0.35, bladeHi: 1.42, guardW: 0.40, bladeW: 0.074, zweihander: true },
  ];
  for (const sw of swords) {
    addPart(root, sw.name, (mesh) => {
      const pommel = lathe([
        [0.0, sw.gripLo - 0.052],
        [0.044, sw.gripLo - 0.026],
        [0.044, sw.gripLo],
        [0.0, sw.gripLo + 0.012],
      ], 8);
      mesh(pommel, sw.zweihander ? M.steel : M.brass);
      const grip = column([[sw.gripLo, 0.022, 0.018], [0.06, 0.024, 0.020]], { segs: 6 });
      mesh(grip, M.leather);
      const guard = shadeVerts(new THREE.BoxGeometry(sw.guardW, 0.032, 0.038), () => 0.96);
      mesh(guard, sw.zweihander ? M.steel : M.brass, [0, 0.074, 0]);
      if (sw.zweihander) {
        for (const s of [-1, 1]) {
          const lug = shadeVerts(new THREE.BoxGeometry(0.050, 0.024, 0.028), () => 0.94);
          mesh(lug, M.steel, [s * 0.062, 0.22, 0], [0, 0, s * -0.45]);
        }
      }
      const w0 = sw.bladeW * 0.5, w1 = sw.bladeW * 0.36;
      const blade = bladeExtrude([
        [-w0, 0.08],
        [-w1, sw.bladeHi - 0.15],
        [0, sw.bladeHi],
        [w1, sw.bladeHi - 0.15],
        [w0, 0.08],
      ], 0.020, 0.006);
      mesh(blade, M.steel);
    });
  }

  // 5. weapon_halberd (edgeAlongZ = true)
  addPart(root, 'weapon_halberd', (mesh) => {
    const shaft = column([[-0.62, 0.020, 0.022], [1.44, 0.022, 0.024]], { segs: 8 });
    mesh(shaft, M.wood);
    const butt = lathe([[0.0, -0.68], [0.022, -0.62]], 8);
    mesh(butt, M.mail);
    const langets = shadeVerts(new THREE.BoxGeometry(0.046, 0.36, 0.052), () => 0.88);
    mesh(langets, M.mail, [0, 1.32, 0]);
    const head = bladeExtrude([
      [-0.17, 1.42], // rear armor-piercing fluke (-Z)
      [-0.032, 1.36],
      [-0.030, 1.54],
      [0.0, 1.84],   // tall diamond thrusting spike
      [0.034, 1.54],
      [0.23, 1.52],  // sweeping crescent axe blade (+Z)
      [0.25, 1.35],
      [0.19, 1.24],
      [0.028, 1.32],
    ], 0.018, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 5b. weapon_billhook (edgeAlongZ = true)
  addPart(root, 'weapon_billhook', (mesh) => {
    const shaft = column([[-0.60, 0.020, 0.022], [1.28, 0.022, 0.024]], { segs: 8 });
    mesh(shaft, M.wood);
    const butt = lathe([[0.0, -0.66], [0.022, -0.60]], 8);
    mesh(butt, M.mail);
    const langets = shadeVerts(new THREE.BoxGeometry(0.046, 0.34, 0.052), () => 0.88);
    mesh(langets, M.mail, [0, 1.20, 0]);
    const head = bladeExtrude([
      [-0.15, 1.34], // rear hook/fluke (-Z)
      [-0.030, 1.29],
      [-0.026, 1.48],
      [0.0, 1.72],   // top thrusting spike
      [0.032, 1.52],
      [0.19, 1.54],  // forward-curling billhook beak (+Z)
      [0.21, 1.43],
      [0.14, 1.38],
      [0.12, 1.24],  // lower chopping edge
      [0.026, 1.20],
    ], 0.018, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 6. weapon_mace
  addPart(root, 'weapon_mace', (mesh) => {
    const haft = column([[-0.18, 0.017, 0.017], [0.64, 0.020, 0.020]], { segs: 8 });
    mesh(haft, M.wood);
    const grip = column([[-0.10, 0.021, 0.021], [0.10, 0.021, 0.021]], { segs: 8 });
    mesh(grip, M.leather);
    const core = lathe([[0.022, 0.56], [0.034, 0.68], [0.0, 0.79]], 8);
    mesh(core, M.mail);
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2;
      const flange = shadeVerts(new THREE.BoxGeometry(0.014, 0.14, 0.075), () => 0.94);
      mesh(flange, M.steel, [Math.sin(a) * 0.042, 0.64, Math.cos(a) * 0.042], [0, a, 0]);
    }
  });

  // 7. weapon_warhammer (edgeAlongZ = true)
  addPart(root, 'weapon_warhammer', (mesh) => {
    const haft = column([[-0.22, 0.018, 0.020], [0.68, 0.020, 0.022]], { segs: 8 });
    mesh(haft, M.wood);
    const grip = column([[-0.10, 0.022, 0.024], [0.10, 0.022, 0.024]], { segs: 8 });
    mesh(grip, M.leather);
    const head = bladeExtrude([
      [-0.21, 0.60], // armor-piercing beak (-Z)
      [-0.03, 0.62],
      [-0.022, 0.68],
      [0.0, 0.78],   // top spike
      [0.022, 0.68],
      [0.04, 0.66],
      [0.145, 0.68], // hammer face (+Z)
      [0.145, 0.59],
      [0.03, 0.60],
    ], 0.036, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 8. weapon_bow (curve toward -Z, string at -Z)
  addPart(root, 'weapon_bow', (mesh) => {
    const limb = loft(V(0, -0.58, -0.19), V(0, 0.58, -0.19), [
      [0.00, 0.014, 0.014, 0, -0.02],
      [0.12, 0.018, 0.018, 0, 0.10],
      [0.32, 0.022, 0.020, 0, 0.18],
      [0.50, 0.024, 0.026, 0, 0.20],
      [0.68, 0.022, 0.020, 0, 0.18],
      [0.88, 0.018, 0.018, 0, 0.10],
      [1.00, 0.014, 0.014, 0, -0.02],
    ], { segs: 6 });
    mesh(shadeVerts(limb, () => 0.90), M.wood);
    const grip = column([[-0.08, 0.028, 0.030, 0, 0.0], [0.08, 0.028, 0.030, 0, 0.0]], { segs: 6 });
    mesh(grip, M.leather);
    const str = shadeVerts(new THREE.BoxGeometry(0.008, 1.16, 0.008), () => 0.95);
    mesh(str, M.trim, [0, 0, -0.20]);
  });

  // 9. weapon_crossbow (prod +-X, tiller +Y): carved walnut stock, brass nut/trigger, heavy steel prod, stirrup & loaded quarrel
  addPart(root, 'weapon_crossbow', (mesh) => {
    // Tapered walnut tiller + flared buttstock
    const tiller = column([
      [-0.16, 0.018, 0.034],
      [-0.02, 0.022, 0.030],
      [0.18, 0.024, 0.030],
      [0.44, 0.020, 0.026],
    ], { segs: 8 });
    mesh(tiller, M.wood);
    // Brass lock-nut, flight-groove rail & trigger lever
    const nut = shadeVerts(new THREE.BoxGeometry(0.036, 0.044, 0.018), () => 0.96);
    mesh(nut, M.brass, [0, 0.14, 0.026]);
    const trigger = shadeVerts(new THREE.BoxGeometry(0.012, 0.095, 0.022), () => 0.92);
    mesh(trigger, M.brass, [0, 0.04, -0.036], [0.22, 0, 0]);
    // Heavy recurved steel prod + iron binding collar
    const prod = loft(V(-0.34, 0.41, 0.004), V(0.34, 0.41, 0.004), [
      [0.00, 0.014, 0.012, 0, -0.06],
      [0.25, 0.020, 0.016, 0, 0.02],
      [0.50, 0.026, 0.020, 0, 0.045],
      [0.75, 0.020, 0.016, 0, 0.02],
      [1.00, 0.014, 0.012, 0, -0.06],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(shadeVerts(prod, () => 0.92), M.steel);
    const collar = shadeVerts(new THREE.BoxGeometry(0.064, 0.056, 0.064), () => 0.88);
    mesh(collar, M.mail, [0, 0.42, 0.004]);
    // Forged iron foot-stirrup at front
    const stirrup = shadeVerts(new THREE.TorusGeometry(0.055, 0.010, 4, 8), () => 0.88);
    mesh(stirrup, M.mail, [0, 0.495, 0], [Math.PI / 2, 0, 0]);
    // Bowstring + loaded steel-tipped quarrel resting on top
    const str = shadeVerts(new THREE.BoxGeometry(0.64, 0.008, 0.008), () => 0.95);
    mesh(str, M.trim, [0, 0.34, 0.016]);
    const boltShaft = shadeVerts(new THREE.CylinderGeometry(0.007, 0.007, 0.34, 6), () => 0.92);
    mesh(boltShaft, M.wood, [0, 0.32, 0.030]);
    const boltHead = lathe([[0.0, 0.54], [0.018, 0.48], [0.0, 0.47]], 6);
    mesh(boltHead, M.steel, [0, 0, 0.030]);
  });

  // 14. weapon_flamberge (undulating two-handed greatsword, edge along X)
  addPart(root, 'weapon_flamberge', (mesh) => {
    const grip = column([[-0.30, 0.017, 0.015], [-0.02, 0.019, 0.016]], { segs: 8 });
    mesh(grip, M.leather);
    mesh(lathe([[0.0, -0.35], [0.032, -0.32], [0.026, -0.29], [0.014, -0.28]], 8), M.brass);
    // Swept crossguard + parrying rings
    const guard = shadeVerts(new THREE.BoxGeometry(0.35, 0.026, 0.030), () => 0.94);
    mesh(guard, M.brass, [0, 0.0, 0]);
    for (const sx of [-1, 1]) {
      const finial = shadeVerts(new THREE.SphereGeometry(0.020, 6, 5), () => 0.96);
      mesh(finial, M.brass, [sx * 0.175, 0.0, 0]);
    }
    // Parrying lugs (Parierhaken) above ricasso
    const lugs = shadeVerts(new THREE.BoxGeometry(0.14, 0.020, 0.018), () => 0.92);
    mesh(lugs, M.steel, [0, 0.24, 0]);
    // Wavy flame-bladed profile (edge along X, thin along Z)
    const pts = [];
    const steps = 12;
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      const y = 0.02 + t * 1.32;
      const baseW = 0.036 * (1 - 0.55 * t);
      const wave = i > 1 && i < steps ? 0.011 * Math.sin(i * Math.PI * 0.95) : 0;
      pts.push([baseW + wave, y]);
    }
    pts.push([0.0, 1.42]);
    for (let i = steps; i >= 0; i--) {
      const t = i / steps;
      const y = 0.02 + t * 1.32;
      const baseW = 0.036 * (1 - 0.55 * t);
      const wave = i > 1 && i < steps ? -0.011 * Math.sin(i * Math.PI * 0.95) : 0;
      pts.push([-(baseW + wave), y]);
    }
    const shape = new THREE.Shape(pts.map(([x, y]) => new THREE.Vector2(x, y)));
    const blade = new THREE.ExtrudeGeometry(shape, { depth: 0.012, bevelEnabled: true, bevelThickness: 0.004, bevelSize: 0.003, bevelSegments: 1 });
    blade.translate(0, 0, -0.006);
    blade.computeVertexNormals();
    mesh(heightShade(blade, 0.0, 1.42, 0.86, 1.0), M.steel);
  });

  // 15. weapon_scimitar (curved shamshir/kilij, WeaponEdgeZ = true -> edge toward +Z)
  addPart(root, 'weapon_scimitar', (mesh) => {
    const grip = column([[-0.14, 0.016, 0.018], [-0.01, 0.017, 0.020]], { segs: 8 });
    mesh(grip, M.leather);
    const pommel = shadeVerts(new THREE.SphereGeometry(0.024, 7, 5), () => 0.92);
    mesh(pommel, M.brass, [0, -0.155, -0.018]);
    const guard = shadeVerts(new THREE.BoxGeometry(0.030, 0.022, 0.16), () => 0.95);
    mesh(guard, M.brass, [0, 0.0, 0.01]);
    // Curved blade in YZ plane (bladeExtrude extrudes along X, so X->Z, Y->Y)
    const blade = bladeExtrude([
      [0.015, 0.01], [0.026, 0.25], [0.060, 0.52], [0.135, 0.76], [0.235, 0.91],
      [0.190, 0.90], [0.095, 0.76], [0.022, 0.52], [-0.012, 0.25], [-0.016, 0.01],
    ], 0.012, 0.004);
    mesh(heightShade(blade, 0.0, 0.92, 0.86, 1.0), M.steel);
  });

  // 16. weapon_falchion (broad heavy cleaving single-edge sword, WeaponEdgeZ = true)
  addPart(root, 'weapon_falchion', (mesh) => {
    const grip = column([[-0.15, 0.016, 0.018], [-0.01, 0.017, 0.019]], { segs: 8 });
    mesh(grip, M.leather);
    mesh(lathe([[0.0, -0.17], [0.028, -0.15], [0.016, -0.13]], 8), M.brass);
    const guard = shadeVerts(new THREE.BoxGeometry(0.028, 0.024, 0.15), () => 0.94);
    mesh(guard, M.brass, [0, 0.0, 0.015]);
    const blade = bladeExtrude([
      [0.024, 0.01], [0.042, 0.26], [0.072, 0.54], [0.105, 0.72], [0.042, 0.79],
      [-0.016, 0.73], [-0.020, 0.45], [-0.018, 0.01],
    ], 0.014, 0.004);
    mesh(heightShade(blade, 0.0, 0.79, 0.86, 1.0), M.steel);
  });

  // 17. weapon_morningstar (iron-banded haft + spiked steel sphere head)
  addPart(root, 'weapon_morningstar', (mesh) => {
    const haft = column([[-0.20, 0.018, 0.018], [0.0, 0.019, 0.019], [0.64, 0.022, 0.022]], { segs: 8 });
    mesh(haft, M.wood);
    mesh(column([[-0.10, 0.021, 0.021], [0.10, 0.021, 0.021]], { segs: 8 }), M.leather);
    mesh(column([[0.56, 0.026, 0.026], [0.66, 0.028, 0.028]], { segs: 8 }), M.mail);
    const ball = shadeVerts(new THREE.SphereGeometry(0.078, 8, 6), (x, y) => 0.85 + 0.15 * Math.max(0, y / 0.08));
    mesh(ball, M.mail, [0, 0.72, 0]);
    // Crown & equatorial spikes
    const spikeGeo = shadeVerts(new THREE.ConeGeometry(0.017, 0.072, 5), () => 0.98);
    mesh(spikeGeo, M.steel, [0, 0.825, 0]);
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2;
      mesh(spikeGeo, M.steel, [Math.cos(a) * 0.095, 0.72, Math.sin(a) * 0.095], [Math.sin(a) * Math.PI * 0.5, 0, -Math.cos(a) * Math.PI * 0.5]);
    }
    for (let i = 0; i < 4; i++) {
      const a = ((i + 0.5) / 4) * Math.PI * 2;
      mesh(spikeGeo, M.steel, [Math.cos(a) * 0.075, 0.775, Math.sin(a) * 0.075], [Math.sin(a) * 0.8, 0, -Math.cos(a) * 0.8]);
    }
  });

  // 18. weapon_club (heavy tapering iron-studded cudgel)
  addPart(root, 'weapon_club', (mesh) => {
    const body = column([
      [-0.18, 0.020, 0.020],
      [0.05, 0.022, 0.022],
      [0.36, 0.036, 0.036],
      [0.68, 0.068, 0.068],
      [0.86, 0.076, 0.076],
      [0.95, 0.044, 0.044],
    ], { segs: 8 });
    mesh(body, M.wood);
    mesh(column([[-0.12, 0.024, 0.024], [0.08, 0.024, 0.024]], { segs: 8 }), M.leather);
    // Two iron bands + studs
    for (const y of [0.64, 0.80]) {
      const r = y < 0.7 ? 0.069 : 0.078;
      mesh(column([[y - 0.018, r, r], [y + 0.018, r, r]], { segs: 8 }), M.mail);
      for (let i = 0; i < 4; i++) {
        const a = (i / 4) * Math.PI * 2 + (y > 0.7 ? Math.PI / 4 : 0);
        const stud = shadeVerts(new THREE.ConeGeometry(0.013, 0.028, 4), () => 0.94);
        mesh(stud, M.steel, [Math.cos(a) * (r + 0.008), y, Math.sin(a) * (r + 0.008)], [Math.sin(a) * Math.PI * 0.5, 0, -Math.cos(a) * Math.PI * 0.5]);
      }
    }
  });

  // 19. weapon_staff (mage/officer gnarled staff with brass ferrule & crowned finial)
  addPart(root, 'weapon_staff', (mesh) => {
    const shaft = column([
      [-0.70, 0.018, 0.018],
      [0.0, 0.020, 0.020],
      [0.60, 0.022, 0.022],
      [1.18, 0.025, 0.025],
      [1.30, 0.034, 0.034],
    ], { segs: 8 });
    mesh(shaft, M.wood);
    mesh(column([[-0.08, 0.023, 0.023], [0.12, 0.023, 0.023]], { segs: 8 }), M.leather);
    mesh(column([[1.22, 0.036, 0.036], [1.32, 0.044, 0.044]], { segs: 8 }), M.brass);
    // Brass crown prongs + inner crystal/orb
    const orb = shadeVerts(new THREE.SphereGeometry(0.046, 8, 6), () => 1.0);
    mesh(orb, M.trim, [0, 1.39, 0]);
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2;
      const prong = loft(V(Math.cos(a) * 0.034, 1.30, Math.sin(a) * 0.034), V(Math.cos(a) * 0.052, 1.45, Math.sin(a) * 0.052), [
        [0, 0.010, 0.010], [0.6, 0.008, 0.008], [1.0, 0.004, 0.004],
      ], { segs: 4 });
      mesh(prong, M.brass);
    }
  });

  // 20. weapon_lance (long tapered cavalry lance with conical vamplate & cloth swallowtail pennant)
  addPart(root, 'weapon_lance', (mesh) => {
    const shaft = column([
      [-0.75, 0.028, 0.028],
      [-0.18, 0.034, 0.034],
      [0.0, 0.022, 0.022],
      [0.14, 0.038, 0.038],
      [1.20, 0.032, 0.032],
      [2.32, 0.018, 0.018],
    ], { segs: 8 });
    mesh(shaft, M.wood);
    mesh(column([[-0.12, 0.024, 0.024], [0.08, 0.024, 0.024]], { segs: 8 }), M.leather);
    // Steel conical vamplate handguard ahead of grip
    const vamplate = lathe([
      [0.038, 0.08], [0.108, 0.14], [0.085, 0.24], [0.042, 0.34], [0.034, 0.36],
    ], 10);
    mesh(vamplate, M.steel);
    // Steel bodkin tip
    const tip = lathe([[0.020, 2.30], [0.028, 2.38], [0.0, 2.68]], 6);
    mesh(tip, M.steel);
    // Trailing side-colored pennant near tip (-Z flutter)
    const pennant = bladeExtrude([
      [-0.018, 1.88], [-0.24, 1.94], [-0.15, 2.04], [-0.24, 2.14], [-0.018, 2.22],
    ], 0.006, 0.0);
    mesh(pennant, M.cloth);
  });

  // 21. weapon_glaive (long polearm with sweeping single-edge blade, WeaponEdgeZ = true)
  addPart(root, 'weapon_glaive', (mesh) => {
    const shaft = column([[-0.68, 0.018, 0.018], [0.0, 0.019, 0.019], [1.46, 0.021, 0.021]], { segs: 8 });
    mesh(shaft, M.wood);
    mesh(column([[1.36, 0.024, 0.024], [1.50, 0.026, 0.026]], { segs: 8 }), M.mail);
    const blade = bladeExtrude([
      [0.022, 1.48], [0.068, 1.68], [0.105, 1.92], [0.072, 2.08], [0.012, 2.15],
      [-0.024, 1.95], [-0.022, 1.66], [-0.018, 1.48],
    ], 0.012, 0.003);
    mesh(heightShade(blade, 1.48, 2.15, 0.86, 1.0), M.steel);
    // Back-spike / lug on spine
    const lug = bladeExtrude([[-0.018, 1.66], [-0.066, 1.72], [-0.022, 1.76]], 0.010, 0.002);
    mesh(lug, M.mail);
  });

  // 22. weapon_sling (braided leather cords + cup pouch + stone)
  addPart(root, 'weapon_sling', (mesh) => {
    for (const sx of [-1, 1]) {
      const cord = loft(V(sx * 0.012, 0.0, 0.0), V(sx * 0.026, 0.56, 0.045), [
        [0, 0.006, 0.006], [0.5, 0.005, 0.005], [1, 0.006, 0.006],
      ], { segs: 5 });
      mesh(cord, M.leather);
    }
    const pouch = shadeVerts(new THREE.SphereGeometry(0.042, 7, 5), () => 0.88);
    pouch.scale(1.0, 1.2, 0.65);
    mesh(pouch, M.leather, [0, 0.58, 0.048]);
    const stone = shadeVerts(new THREE.SphereGeometry(0.028, 6, 5), () => 0.92);
    mesh(stone, M.mail, [0, 0.58, 0.062]);
  });

  // 23. weapon_arrow (fletched war arrow, Y 0..0.74)
  addPart(root, 'weapon_arrow', (mesh) => {
    const shaft = column([[0.0, 0.007, 0.007], [0.64, 0.007, 0.007]], { segs: 6 });
    mesh(shaft, M.wood);
    const head = lathe([[0.007, 0.63], [0.022, 0.66], [0.0, 0.74]], 6);
    mesh(head, M.steel);
    // 3-vane fletching near nock
    for (let i = 0; i < 3; i++) {
      const a = (i / 3) * Math.PI * 2;
      const vane = shadeVerts(new THREE.BoxGeometry(0.003, 0.11, 0.026), () => 0.96);
      mesh(vane, M.trim, [Math.sin(a) * 0.014, 0.075, Math.cos(a) * 0.014], [0, a, 0]);
    }
  });

  // 24. weapon_javelin (weighted throwing spear, Y -0.55..0.76)
  addPart(root, 'weapon_javelin', (mesh) => {
    const shaft = column([[-0.55, 0.012, 0.012], [0.0, 0.014, 0.014], [0.54, 0.013, 0.013]], { segs: 6 });
    mesh(shaft, M.wood);
    mesh(column([[-0.06, 0.017, 0.017], [0.06, 0.017, 0.017]], { segs: 6 }), M.leather);
    const head = lathe([[0.014, 0.52], [0.032, 0.60], [0.0, 0.76]], 6);
    mesh(head, M.steel);
  });

  // ---------- SHIELDS (face toward +Z, back/grip near Z = 0) ----------

  function buildCurvedShield(mesh, rows, curveZ = 0.045, thickness = 0.022, faceMat = M.cloth, rimMat = M.steel, backMat = M.wood) {
    // rows: [[y, halfW], ...] from bottom to top, curved around Y axis
    const cols = 6;
    const pos = [];
    const idx = [];
    const nR = rows.length;
    // Front face (z = +curve + thickness) and back face (z = +curve)
    const addSurface = (zOff, flip) => {
      const base = pos.length / 3;
      for (let r = 0; r < nR; r++) {
        const [y, hw] = rows[r];
        for (let c = 0; c <= cols; c++) {
          const u = (c / cols) * 2 - 1; // -1..1
          const x = u * hw;
          const z = zOff + curveZ * (1 - u * u);
          pos.push(x, y, z);
        }
      }
      const stride = cols + 1;
      for (let r = 0; r < nR - 1; r++) {
        for (let c = 0; c < cols; c++) {
          const a = base + r * stride + c;
          const b = a + 1;
          const d = a + stride;
          const e = d + 1;
          if (!flip) idx.push(a, b, e, a, e, d);
          else idx.push(a, e, b, a, d, e);
        }
      }
    };
    addSurface(0.015 + thickness, false);
    const frontGeo = new THREE.BufferGeometry();
    frontGeo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    frontGeo.setIndex(idx);
    frontGeo.computeVertexNormals();
    mesh(heightShade(frontGeo, rows[0][0], rows[nR - 1][0], 0.86, 1.0), faceMat);

    // Back face
    const bPos = [], bIdx = [];
    for (let r = 0; r < nR; r++) {
      const [y, hw] = rows[r];
      for (let c = 0; c <= cols; c++) {
        const u = (c / cols) * 2 - 1;
        bPos.push(u * hw, y, 0.015 + curveZ * (1 - u * u));
      }
    }
    const stride = cols + 1;
    for (let r = 0; r < nR - 1; r++) {
      for (let c = 0; c < cols; c++) {
        const a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
        bIdx.push(a, e, b, a, d, e);
      }
    }
    const backGeo = new THREE.BufferGeometry();
    backGeo.setAttribute('position', new THREE.Float32BufferAttribute(bPos, 3));
    backGeo.setIndex(bIdx);
    backGeo.computeVertexNormals();
    mesh(shadeVerts(backGeo, () => 0.78), backMat);

    // Perimeter rim connecting front & back
    const perim = [];
    for (let c = 0; c <= cols; c++) perim.push([0, c]);
    for (let r = 1; r < nR; r++) perim.push([r, cols]);
    for (let c = cols - 1; c >= 0; c--) perim.push([nR - 1, c]);
    for (let r = nR - 2; r >= 0; r--) perim.push([r, 0]);
    const rPos = [], rIdx = [];
    for (const [r, c] of perim) {
      const [y, hw] = rows[r];
      const u = (c / cols) * 2 - 1;
      const x = u * hw, z0 = 0.015 + curveZ * (1 - u * u);
      rPos.push(x, y, z0, x, y, z0 + thickness + 0.004);
    }
    for (let i = 0; i < perim.length - 1; i++) {
      const a = i * 2, b = a + 1, c2 = a + 2, d = a + 3;
      rIdx.push(a, b, d, a, d, c2);
    }
    const rimGeo = new THREE.BufferGeometry();
    rimGeo.setAttribute('position', new THREE.Float32BufferAttribute(rPos, 3));
    rimGeo.setIndex(rIdx);
    rimGeo.computeVertexNormals();
    mesh(shadeVerts(rimGeo, () => 0.94), rimMat);
  }

  // shield_heater (classic triangular curved knight shield; keep front z=0.077 so Puppet3D emblems sit proud)
  addPart(root, 'shield_heater', (mesh) => {
    buildCurvedShield(mesh, [
      [-0.36, 0.015], [-0.22, 0.155], [-0.02, 0.245], [0.18, 0.268], [0.35, 0.262],
    ], 0.042, 0.020, M.cloth, M.brass, M.wood);
    const bar = shadeVerts(new THREE.BoxGeometry(0.49, 0.030, 0.016), () => 0.96);
    mesh(bar, M.brass, [0, 0.32, 0.068]);
  });

  // shield_kite (tall Norman teardrop shield)
  addPart(root, 'shield_kite', (mesh) => {
    buildCurvedShield(mesh, [
      [-0.46, 0.01], [-0.28, 0.11], [-0.06, 0.195], [0.16, 0.23], [0.32, 0.21], [0.40, 0.11],
    ], 0.046, 0.020, M.cloth, M.steel, M.wood);
    const boss = shadeVerts(new THREE.SphereGeometry(0.052, 7, 5), () => 0.96);
    boss.scale(1, 1, 0.55);
    mesh(boss, M.steel, [0, 0.12, 0.082]);
  });

  // shield_pavise (large standing crossbowman pavise with central ridge)
  addPart(root, 'shield_pavise', (mesh) => {
    buildCurvedShield(mesh, [
      [-0.58, 0.26], [-0.20, 0.28], [0.22, 0.29], [0.46, 0.27],
    ], 0.036, 0.024, M.cloth, M.wood, M.wood);
    // Central vertical ridge spine
    const ridge = column([[-0.56, 0.040, 0.032], [0.44, 0.040, 0.032]], { segs: 6 });
    mesh(ridge, M.clothDark, [0, 0, 0.062]);
  });

  // shield_tower (tall legionary/heavy infantry scutum)
  addPart(root, 'shield_tower', (mesh) => {
    buildCurvedShield(mesh, [
      [-0.49, 0.25], [-0.15, 0.27], [0.18, 0.27], [0.45, 0.25],
    ], 0.072, 0.024, M.cloth, M.brass, M.wood);
    const boss = shadeVerts(new THREE.SphereGeometry(0.064, 8, 5), () => 0.98);
    boss.scale(1, 1.15, 0.55);
    mesh(boss, M.brass, [0, 0.0, 0.108]);
  });

  // shield_kalkan (eastern/steppe domed round shield with central boss + 4 rivets)
  addPart(root, 'shield_kalkan', (mesh) => {
    const dome = lathe([
      [0.31, 0.01], [0.31, 0.028], [0.24, 0.072], [0.14, 0.115], [0.06, 0.142], [0.0, 0.165],
    ], 12);
    dome.rotateX(Math.PI / 2);
    mesh(dome, M.cloth);
    const rim = shadeVerts(new THREE.TorusGeometry(0.305, 0.014, 5, 12), () => 0.94);
    mesh(rim, M.brass, [0, 0, 0.022]);
    const boss = shadeVerts(new THREE.SphereGeometry(0.056, 7, 5), () => 0.98);
    boss.scale(1, 1, 0.6);
    mesh(boss, M.brass, [0, 0, 0.145]);
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
      const stud = shadeVerts(new THREE.SphereGeometry(0.022, 5, 4), () => 0.96);
      mesh(stud, M.brass, [Math.cos(a) * 0.155, Math.sin(a) * 0.155, 0.108]);
    }
  });

  // shield_lantern_pavise (Order of the Lantern ornate pavise with brass sun-lantern crest)
  addPart(root, 'shield_lantern_pavise', (mesh) => {
    buildCurvedShield(mesh, [
      [-0.46, 0.24], [-0.10, 0.275], [0.26, 0.28], [0.50, 0.22],
    ], 0.052, 0.024, M.cloth, M.brass, M.wood);
    const ridge = column([[-0.44, 0.036, 0.028], [0.46, 0.036, 0.028]], { segs: 6 });
    mesh(ridge, M.brass, [0, 0, 0.078]);
    const lantern = shadeVerts(new THREE.OctahedronGeometry(0.068, 0), () => 1.0);
    lantern.scale(1, 1.35, 0.45);
    mesh(lantern, M.trim, [0, 0.10, 0.095]);
  });

  // shield_tarn_kite (Order of the Tarn dark iron-rimmed kite shield with vertical cross ridge)
  addPart(root, 'shield_tarn_kite', (mesh) => {
    buildCurvedShield(mesh, [
      [-0.46, 0.01], [-0.26, 0.115], [-0.04, 0.20], [0.18, 0.23], [0.34, 0.21], [0.40, 0.11],
    ], 0.048, 0.022, M.clothDark, M.mail, M.dark);
    const vBar = shadeVerts(new THREE.BoxGeometry(0.032, 0.72, 0.018), () => 0.95);
    mesh(vBar, M.steel, [0, -0.02, 0.078]);
    const hBar = shadeVerts(new THREE.BoxGeometry(0.38, 0.032, 0.018), () => 0.95);
    mesh(hBar, M.steel, [0, 0.15, 0.072]);
  });

  // shield_sun_kalkan (Sunward radiant domed kalkan with brass sun rays)
  addPart(root, 'shield_sun_kalkan', (mesh) => {
    const dome = lathe([
      [0.32, 0.01], [0.32, 0.028], [0.24, 0.068], [0.13, 0.105], [0.0, 0.135],
    ], 12);
    dome.rotateX(Math.PI / 2);
    mesh(dome, M.cloth);
    const rim = shadeVerts(new THREE.TorusGeometry(0.315, 0.016, 5, 12), () => 0.96);
    mesh(rim, M.brass, [0, 0, 0.024]);
    const sun = shadeVerts(new THREE.SphereGeometry(0.072, 8, 5), () => 1.0);
    sun.scale(1, 1, 0.55);
    mesh(sun, M.brass, [0, 0, 0.118]);
    const rayGeo = shadeVerts(new THREE.ConeGeometry(0.020, 0.105, 4), () => 0.98);
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * Math.PI * 2;
      mesh(rayGeo, M.brass, [Math.cos(a) * 0.155, Math.sin(a) * 0.155, 0.088], [0, 0, a - Math.PI / 2]);
    }
  });
}
