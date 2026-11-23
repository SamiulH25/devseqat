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
      [-0.58, 0.020, 0.024],
      [0.0, 0.022, 0.026],
      [1.36, 0.024, 0.028],
    ], { segs: 8 });
    mesh(haft, M.wood);
    const langets = shadeVerts(new THREE.BoxGeometry(0.052, 0.34, 0.062), () => 0.88);
    mesh(langets, M.mail, [0, 1.24, 0]);
    const blade = bladeExtrude([
      [0.025, 1.18],
      [0.22, 1.06],
      [0.27, 1.16],
      [0.26, 1.38],
      [0.20, 1.43],
      [0.025, 1.33],
    ], 0.018, 0.005);
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
    { name: 'weapon_sword', gripLo: -0.15, bladeHi: 0.85, guardW: 0.21, bladeW: 0.048, zweihander: false },
    { name: 'weapon_longsword', gripLo: -0.25, bladeHi: 1.13, guardW: 0.27, bladeW: 0.052, zweihander: false },
    { name: 'weapon_greatsword', gripLo: -0.34, bladeHi: 1.39, guardW: 0.35, bladeW: 0.062, zweihander: true },
  ];
  for (const sw of swords) {
    addPart(root, sw.name, (mesh) => {
      const pommel = lathe([
        [0.0, sw.gripLo - 0.045],
        [0.038, sw.gripLo - 0.022],
        [0.038, sw.gripLo],
        [0.0, sw.gripLo + 0.01],
      ], 10);
      mesh(pommel, M.brass);
      const grip = column([[sw.gripLo, 0.018, 0.015], [0.06, 0.020, 0.016]], { segs: 8 });
      mesh(grip, M.leather);
      const guard = shadeVerts(new THREE.BoxGeometry(sw.guardW, 0.024, 0.032), () => 0.94);
      mesh(guard, sw.zweihander ? M.steel : M.brass, [0, 0.072, 0]);
      if (sw.zweihander) {
        for (const s of [-1, 1]) {
          const lug = shadeVerts(new THREE.BoxGeometry(0.042, 0.020, 0.024), () => 0.92);
          mesh(lug, M.steel, [s * 0.055, 0.21, 0], [0, 0, s * -0.45]);
        }
      }
      const w0 = sw.bladeW * 0.5, w1 = sw.bladeW * 0.36;
      const blade = bladeExtrude([
        [-w0, 0.08],
        [-w1, sw.bladeHi - 0.14],
        [0, sw.bladeHi],
        [w1, sw.bladeHi - 0.14],
        [w0, 0.08],
      ], 0.016, 0.005);
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
    const limb = loft(V(0, -0.54, -0.18), V(0, 0.54, -0.18), [
      [0.00, 0.010, 0.010, 0, -0.02],
      [0.12, 0.014, 0.014, 0, 0.09],
      [0.32, 0.018, 0.016, 0, 0.16],
      [0.50, 0.020, 0.022, 0, 0.18],
      [0.68, 0.018, 0.016, 0, 0.16],
      [0.88, 0.014, 0.014, 0, 0.09],
      [1.00, 0.010, 0.010, 0, -0.02],
    ], { segs: 8 });
    mesh(shadeVerts(limb, () => 0.90), M.wood);
    const grip = column([[-0.07, 0.023, 0.025, 0, 0.0], [0.07, 0.023, 0.025, 0, 0.0]], { segs: 8 });
    mesh(grip, M.leather);
    const str = shadeVerts(new THREE.BoxGeometry(0.006, 1.08, 0.006), () => 0.95);
    mesh(str, M.trim, [0, 0, -0.19]);
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
}
