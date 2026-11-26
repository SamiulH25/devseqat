import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildWeapons(root) {
  addPart(root, 'shield_round', (mesh) => {
    // Flat-faced plank disc, bold iron rim, high domed boss (reference: flat round shield, strong rim + boss)
    const face = lathe([
      [0.0, 0.030],
      [0.24, 0.026],
      [0.29, 0.022],
      [0.315, 0.016],
      [0.315, 0.0],
      [0.0, 0.006],
    ], 14, 0.86, 1.0);
    face.rotateX(Math.PI / 2);
    mesh(face, M.cloth);

    const rim = new THREE.TorusGeometry(0.305, 0.020, 4, 12);
    mesh(shadeVerts(rim, () => 0.88), M.mail, [0, 0, 0.012]);

    const boss = lathe([
      [0.0, 0.118],
      [0.052, 0.104],
      [0.085, 0.052],
      [0.108, 0.024],
    ], 10, 0.88, 1.0);
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
    // Broad bearded axe blade sweeping toward +Z (extrude along X, rotated so shape X -> +Z):
    // horned top, round belly edge, deep drooping beard below the eye
    const blade = bladeExtrude([
      [0.028, 0.60],
      [0.155, 0.50],  // beard tip drops well below the eye
      [0.225, 0.60],  // lower edge belly
      [0.235, 0.70],  // edge mid
      [0.195, 0.80],  // upper horn
      [0.11, 0.83],
      [0.028, 0.78],
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
    // Great sweeping crescent bit: deep beard below the eye, wide round belly, horned top (shape X -> +Z)
    const blade = bladeExtrude([
      [0.028, 1.14],
      [0.26, 0.98],   // beard tip
      [0.37, 1.12],   // lower belly
      [0.40, 1.28],   // edge mid
      [0.33, 1.44],   // upper horn
      [0.20, 1.50],
      [0.028, 1.36],
    ], 0.020, 0.006);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // 3. weapon_spear & weapon_pike: long leaf-shaped heads that read at distance (edges +-X)
  for (const [name, yMin, yMax, hw, len] of [['weapon_spear', -0.68, 1.46, 0.052, 0.40], ['weapon_pike', -0.94, 1.98, 0.040, 0.34]]) {
    addPart(root, name, (mesh) => {
      const shaft = column([[yMin, 0.018, 0.018], [0, 0.020, 0.020], [yMax, 0.019, 0.019]], { segs: 8 });
      mesh(shaft, M.wood);
      const butt = lathe([[0.0, yMin - 0.08], [0.021, yMin]], 8);
      mesh(butt, M.mail);
      const socket = lathe([[0.024, yMax - 0.04], [0.020, yMax + 0.08], [0.026, yMax + 0.09]], 8);
      mesh(socket, M.mail);
      // Broad leaf blade: full-width shoulder, swelling belly, long taper to the point
      const head = bladeExtrude([
        [-hw * 0.62, yMax + 0.08],
        [-hw, yMax + 0.08 + len * 0.30],
        [-hw * 0.55, yMax + 0.08 + len * 0.66],
        [0.0, yMax + 0.08 + len],
        [hw * 0.55, yMax + 0.08 + len * 0.66],
        [hw, yMax + 0.08 + len * 0.30],
        [hw * 0.62, yMax + 0.08],
      ], 0.016, 0.005);
      mesh(head, M.steel);
    });
  }

  // 4. Straight swords: weapon_sword, weapon_longsword, weapon_greatsword (edgeAlongZ = false, edges +-X)
  // Long slender knightly blades: diamond cross-section lofts (strong facets, readable edge-on), tapered quillon guards.
  const swords = [
    { name: 'weapon_sword', gripLo: -0.16, bladeHi: 0.88, guardW: 0.26, bladeW: 0.056, zweihander: false },
    { name: 'weapon_longsword', gripLo: -0.26, bladeHi: 1.16, guardW: 0.32, bladeW: 0.058, zweihander: false },
    { name: 'weapon_greatsword', gripLo: -0.35, bladeHi: 1.44, guardW: 0.42, bladeW: 0.074, zweihander: true },
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
      // Tapered quillons: slim diamond-bar swelling at the center block, pinching toward the tips
      const g2 = sw.guardW / 2;
      const guard = loft(V(-g2, 0.075, 0), V(g2, 0.075, 0), [
        [0.0, 0.009, 0.011],
        [0.30, 0.013, 0.016],
        [0.50, 0.020, 0.024],
        [0.70, 0.013, 0.016],
        [1.0, 0.009, 0.011],
      ], { segs: 4, fwd: V(0, 1, 0) });
      mesh(shadeVerts(guard, () => 0.96), sw.zweihander ? M.steel : M.brass);
      if (sw.zweihander) {
        for (const s of [-1, 1]) {
          const lug = shadeVerts(new THREE.BoxGeometry(0.050, 0.024, 0.028), () => 0.94);
          mesh(lug, M.steel, [s * 0.062, 0.22, 0], [0, 0, s * -0.45]);
        }
      }
      // Diamond cross-section blade: wide flat at the shoulder, long slender taper to a needle point
      const w0 = sw.bladeW * 0.5;
      const blade = loft(V(0, 0.08, 0), V(0, sw.bladeHi, 0), [
        [0.0, w0, w0 * 0.30],
        [0.45, w0 * 0.86, w0 * 0.26],
        [0.78, w0 * 0.52, w0 * 0.17],
        [1.0, 0.0035, 0.0022],
      ], { segs: 4 });
      mesh(heightShade(blade, 0.08, sw.bladeHi, 0.84, 1.0), M.steel);
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
      [-0.24, 1.44], // long rear armor-piercing fluke (-Z)
      [-0.032, 1.34],
      [-0.030, 1.54],
      [0.0, 1.90],   // tall diamond thrusting spike
      [0.036, 1.54],
      [0.30, 1.56],  // wide sweeping crescent axe blade (+Z)
      [0.33, 1.42],
      [0.24, 1.24],  // deep drooping beard
      [0.028, 1.32],
    ], 0.018, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 5t. weapon_lucerne_hammer (edgeAlongZ = true: austere Tarn polehammer with chunky 3D hammer block, thick beak & spike)
  addPart(root, 'weapon_lucerne_hammer', (mesh) => {
    mesh(column([[-0.56, 0.025, 0.027], [1.26, 0.027, 0.029]], { segs: 6 }), M.wood);
    mesh(shadeVerts(new THREE.BoxGeometry(0.048, 0.34, 0.052), () => 0.88), M.mail, [0, 1.18, 0]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.085, 0.115, 0.145), () => 0.90), M.mail, [0, 1.38, 0.085]); // 3D hammer block (+Z)
    const beak = bladeExtrude([[-0.22, 1.38], [-0.028, 1.32], [-0.036, 1.48], [0.0, 1.78], [0.036, 1.48], [0.030, 1.32]], 0.032, 0.008);
    beak.rotateY(-Math.PI / 2);
    mesh(beak, M.steel);
  });

  // 5u. weapon_great_maul (proportional two-handed iron crushing maul)
  addPart(root, 'weapon_great_maul', (mesh) => {
    mesh(column([[-0.32, 0.024, 0.026], [1.14, 0.026, 0.028]], { segs: 6 }), M.wood);
    mesh(column([[-0.08, 0.028, 0.030], [0.30, 0.028, 0.030]], { segs: 6 }), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.11, 0.13, 0.24), () => 0.88), M.mail, [0, 1.06, 0]);
  });

  // 5q. weapon_schiavona (Venetian basket-hilt broadsword)
  addPart(root, 'weapon_schiavona', (mesh) => {
    mesh(column([[-0.16, 0.015, 0.018], [0.06, 0.017, 0.020]], { segs: 6 }), M.leather);
    const basket = shadeVerts(new THREE.SphereGeometry(0.068, 6, 4), () => 0.96);
    basket.scale(1.0, 0.85, 1.15);
    mesh(basket, M.brass, [0, 0.03, 0.015]);
    const blade = loft(V(0, 0.08, 0), V(0, 0.96, 0), [[0.0, 0.032, 0.010], [0.75, 0.024, 0.008], [1.0, 0.004, 0.003]], { segs: 4 });
    mesh(heightShade(blade, 0.08, 0.96, 0.86, 1.0), M.steel);
  });

  // 5r. weapon_marsh_fork (Fenmen three-pronged iron marsh trident with sturdy readable tines)
  addPart(root, 'weapon_marsh_fork', (mesh) => {
    mesh(column([[-0.56, 0.025, 0.027], [1.24, 0.027, 0.029]], { segs: 6 }), M.wood);
    mesh(shadeVerts(new THREE.BoxGeometry(0.26, 0.038, 0.038), () => 0.90), M.mail, [0, 1.26, 0]);
    for (const [sx, top] of [[-0.105, 1.72], [0, 1.84], [0.105, 1.72]]) {
      mesh(lathe([[0.026, 1.26], [0.020, top - 0.12], [0.008, top]], 5), M.steel, [sx, 0, 0]);
    }
  });

  // 5s. weapon_stradiot_lance (double-ended Stradiot light cavalry lance with thick shaft & visible butt spike)
  addPart(root, 'weapon_stradiot_lance', (mesh) => {
    mesh(column([[-0.50, 0.032, 0.032], [2.04, 0.030, 0.030]], { segs: 6 }), M.wood);
    mesh(column([[-0.12, 0.036, 0.036], [0.12, 0.036, 0.036]], { segs: 6 }), M.leather);
    mesh(lathe([[0.040, 2.04], [0.028, 2.26], [0.008, 2.50]], 6), M.steel);
    mesh(lathe([[0.008, -0.74], [0.028, -0.58], [0.036, -0.50]], 6), M.mail);
  });

  // 5n. weapon_steppe_sabre (edgeAlongZ = true: swept steppe horseman sabre, convex +Z edge)
  addPart(root, 'weapon_steppe_sabre', (mesh) => {
    mesh(column([[-0.16, 0.015, 0.020], [0.06, 0.017, 0.022]], { segs: 6 }), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.028, 0.024, 0.16), () => 0.96), M.brass, [0, 0.07, 0]);
    const blade = bladeExtrude([
      [-0.020, 0.08], [-0.018, 0.50], [-0.036, 0.80], [-0.070, 0.98],
      [0.030, 0.78], [0.042, 0.48], [0.032, 0.08],
    ], 0.014, 0.004);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // 5o. weapon_hooked_lance (edgeAlongZ = true: steppe kontos with saddle-hook & solid shaft)
  addPart(root, 'weapon_hooked_lance', (mesh) => {
    mesh(column([[-0.65, 0.032, 0.032], [2.06, 0.030, 0.030]], { segs: 6 }), M.wood);
    const head = bladeExtrude([
      [-0.036, 2.02], [0.0, 2.52], [0.036, 2.02], [0.18, 1.94], [0.16, 1.82], [0.032, 1.92],
    ], 0.022, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 5p. weapon_shestoper (six-flanged brass-and-steel steppe mace)
  addPart(root, 'weapon_shestoper', (mesh) => {
    mesh(column([[-0.18, 0.017, 0.017], [0.62, 0.019, 0.019]], { segs: 6 }), M.wood);
    mesh(column([[-0.12, 0.021, 0.021], [0.08, 0.021, 0.021]], { segs: 6 }), M.leather);
    mesh(lathe([[0.022, 0.52], [0.036, 0.64], [0.0, 0.78]], 6), M.steel);
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2;
      mesh(shadeVerts(new THREE.BoxGeometry(0.016, 0.16, 0.085), () => 0.96), M.brass, [Math.sin(a) * 0.044, 0.62, Math.cos(a) * 0.044], [0, a, 0]);
    }
  });

  // 5k. weapon_tabarzin (edgeAlongZ = true: Qadiri crescent saddle axe with brass collar & spike)
  addPart(root, 'weapon_tabarzin', (mesh) => {
    mesh(column([[-0.18, 0.018, 0.020], [0.64, 0.020, 0.022]], { segs: 6 }), M.wood);
    mesh(column([[-0.12, 0.022, 0.024], [0.08, 0.022, 0.024]], { segs: 6 }), M.leather);
    mesh(column([[0.48, 0.026, 0.028], [0.62, 0.026, 0.028]], { segs: 6 }), M.brass);
    mesh(lathe([[0.026, 0.64], [0.0, 0.80]], 6), M.brass);
    const blade = bladeExtrude([
      [0.022, 0.60], [0.14, 0.66], [0.22, 0.70], [0.25, 0.56], [0.21, 0.42], [0.13, 0.46], [0.022, 0.50],
    ], 0.018, 0.004);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // 5l. weapon_cane_lance (long light Qadiri desert lance with thick readable shaft, leather grip, brass collar & full tassel)
  addPart(root, 'weapon_cane_lance', (mesh) => {
    mesh(column([[-0.65, 0.032, 0.032], [2.04, 0.029, 0.029]], { segs: 6 }), M.wood);
    mesh(column([[-0.14, 0.036, 0.036], [0.12, 0.036, 0.036]], { segs: 6 }), M.leather);
    mesh(column([[1.80, 0.062, 0.062], [2.02, 0.036, 0.036]], { segs: 6 }), M.cloth); // bold horsehair tassel
    mesh(column([[2.00, 0.040, 0.040], [2.08, 0.040, 0.040]], { segs: 6 }), M.brass);
    mesh(lathe([[0.038, 2.08], [0.026, 2.28], [0.0, 2.52]], 6), M.steel);
  });

  // 5m. weapon_jarid (compact Qadiri throwing dart with leaf head & leather grip, clears eye level)
  addPart(root, 'weapon_jarid', (mesh) => {
    mesh(column([[-0.48, 0.019, 0.019], [0.58, 0.020, 0.020]], { segs: 6 }), M.wood);
    mesh(column([[-0.08, 0.023, 0.023], [0.08, 0.023, 0.023]], { segs: 6 }), M.leather);
    mesh(column([[0.54, 0.026, 0.026], [0.60, 0.026, 0.026]], { segs: 6 }), M.brass);
    const tip = bladeExtrude([[0.0, 0.84], [0.038, 0.66], [0.022, 0.58], [-0.022, 0.58], [-0.038, 0.66]], 0.014, 0.004);
    mesh(tip, M.steel);
  });

  // 5h. weapon_throwing_axe (edgeAlongZ = true: Nordic francisca throwing axe)
  addPart(root, 'weapon_throwing_axe', (mesh) => {
    mesh(column([[-0.16, 0.015, 0.017], [0.46, 0.017, 0.019]], { segs: 6 }), M.wood);
    const head = bladeExtrude([
      [-0.04, 0.44], [-0.04, 0.38], [0.18, 0.35], [0.22, 0.48], [0.14, 0.54], [-0.02, 0.47],
    ], 0.016, 0.004);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 5i. weapon_atgeir (edgeAlongZ = true: Nordic hewing spear with bold cleaver belly)
  addPart(root, 'weapon_atgeir', (mesh) => {
    mesh(column([[-0.56, 0.024, 0.026], [1.18, 0.026, 0.028]], { segs: 6 }), M.wood);
    mesh(column([[1.10, 0.036, 0.036], [1.20, 0.038, 0.038]], { segs: 6 }), M.brass);
    const blade = bladeExtrude([
      [-0.028, 1.18], [-0.024, 1.60], [0.0, 1.84], [0.16, 1.66], [0.19, 1.34], [0.036, 1.18],
    ], 0.018, 0.005);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // 5j. weapon_boar_spear (winged Holtmen hunting spear with cross-lugs)
  addPart(root, 'weapon_boar_spear', (mesh) => {
    mesh(column([[-0.56, 0.019, 0.021], [1.30, 0.021, 0.023]], { segs: 6 }), M.wood);
    mesh(shadeVerts(new THREE.BoxGeometry(0.24, 0.030, 0.036), () => 0.90), M.mail, [0, 1.30, 0]);
    const blade = bladeExtrude([
      [0.0, 1.82], [0.068, 1.48], [0.030, 1.30], [-0.030, 1.30], [-0.068, 1.48],
    ], 0.016, 0.004);
    mesh(blade, M.steel);
  });

  // 5f. weapon_partisan (broad winged Vespran civic spear)
  addPart(root, 'weapon_partisan', (mesh) => {
    const shaft = column([[-0.56, 0.024, 0.026], [1.20, 0.026, 0.028]], { segs: 6 });
    mesh(shaft, M.wood);
    mesh(lathe([[0.0, -0.62], [0.026, -0.56]], 6), M.mail);
    mesh(column([[1.12, 0.038, 0.038], [1.22, 0.042, 0.042]], { segs: 6 }), M.brass);
    const blade = bladeExtrude([
      [0.0, 1.84], [0.062, 1.48], [0.21, 1.40], [0.16, 1.24], [0.038, 1.22],
      [-0.038, 1.22], [-0.16, 1.24], [-0.21, 1.40], [-0.062, 1.48],
    ], 0.020, 0.005);
    mesh(blade, M.steel);
  });

  // 5g. weapon_kriegsmesser (edgeAlongZ = true: heavy two-handed curved Vespran war-knife, convex +Z edge, Z-aligned guard + nagel)
  addPart(root, 'weapon_kriegsmesser', (mesh) => {
    mesh(column([[-0.28, 0.017, 0.023], [0.06, 0.019, 0.025]], { segs: 6 }), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.028, 0.22), () => 0.96), M.brass, [0, 0.07, 0]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.044, 0.028, 0.028), () => 0.98), M.brass, [0.032, 0.07, 0.01]); // brass nagel
    mesh(shadeVerts(new THREE.BoxGeometry(0.036, 0.044, 0.036), () => 0.94), M.brass, [0, -0.30, 0]);
    const blade = bladeExtrude([
      [-0.024, 0.08], [-0.022, 0.65], [-0.038, 1.08], [-0.068, 1.34], // swept-back spine (-Z)
      [0.040, 1.10], [0.052, 0.66], [0.040, 0.08],                     // convex cutting edge (+Z)
    ], 0.018, 0.004);
    blade.rotateY(-Math.PI / 2);
    mesh(blade, M.steel);
  });

  // shield_rotella (domed circular Vespran duellist steel shield with gilt boss/rim and closed back)
  addPart(root, 'shield_rotella', (mesh) => {
    const dome = lathe([
      [0.0, 0.004], [0.29, 0.008], [0.29, 0.026], [0.21, 0.062], [0.10, 0.095], [0.0, 0.115],
    ], 10);
    dome.rotateX(Math.PI / 2);
    mesh(dome, M.steel);
    const rim = shadeVerts(new THREE.TorusGeometry(0.285, 0.015, 4, 10), () => 0.96);
    mesh(rim, M.brass, [0, 0, 0.022]);
    const boss = shadeVerts(new THREE.SphereGeometry(0.062, 6, 4), () => 1.0);
    boss.scale(1, 1, 0.6);
    mesh(boss, M.brass, [0, 0, 0.105]);
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
      [-0.20, 1.36], // rear hook/fluke (-Z)
      [-0.030, 1.28],
      [-0.026, 1.48],
      [0.0, 1.76],   // top thrusting spike
      [0.034, 1.52],
      [0.25, 1.58],  // bold forward-curling billhook beak (+Z)
      [0.27, 1.44],
      [0.17, 1.37],
      [0.15, 1.22],  // lower chopping edge
      [0.026, 1.20],
    ], 0.018, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 5c. weapon_poleaxe (edgeAlongZ = true: knightly anti-armor axe + hammer + top spike)
  addPart(root, 'weapon_poleaxe', (mesh) => {
    const shaft = column([[-0.56, 0.020, 0.022], [1.34, 0.022, 0.024]], { segs: 6 });
    mesh(shaft, M.wood);
    mesh(lathe([[0.0, -0.62], [0.022, -0.56]], 6), M.mail);
    mesh(shadeVerts(new THREE.BoxGeometry(0.044, 0.36, 0.048), () => 0.88), M.mail, [0, 1.22, 0]);
    const head = bladeExtrude([
      [-0.20, 1.44], [-0.20, 1.30], [-0.030, 1.30], // bold square hammer face (-Z)
      [-0.024, 1.50], [0.0, 1.86], [0.028, 1.50],   // long diamond thrusting spike
      [0.27, 1.54], [0.29, 1.36], [0.22, 1.22], [0.028, 1.28], // wide curved axe blade (+Z)
    ], 0.022, 0.005);
    head.rotateY(-Math.PI / 2);
    mesh(head, M.steel);
  });

  // 5d. weapon_goedendag (iron-banded militia club-spear)
  addPart(root, 'weapon_goedendag', (mesh) => {
    const shaft = column([[-0.45, 0.020, 0.020], [0.75, 0.026, 0.026], [1.26, 0.056, 0.056]], { segs: 6 });
    mesh(shaft, M.wood);
    const band = column([[1.14, 0.062, 0.062], [1.28, 0.064, 0.064]], { segs: 6 });
    mesh(band, M.mail);
    const spike = lathe([[0.034, 1.26], [0.020, 1.48], [0.0, 1.68]], 6);
    mesh(spike, M.steel);
  });

  // 5e. weapon_boarding_axe (edgeAlongZ = true: naval axe with rear grappling hook)
  addPart(root, 'weapon_boarding_axe', (mesh) => {
    const haft = column([[-0.20, 0.017, 0.019], [0.64, 0.019, 0.021]], { segs: 6 });
    mesh(haft, M.wood);
    mesh(column([[-0.12, 0.021, 0.023], [0.10, 0.021, 0.023]], { segs: 6 }), M.leather);
    const head = bladeExtrude([
      [-0.19, 0.56], [-0.15, 0.49], [-0.026, 0.54], // curved rear boarding hook (-Z)
      [-0.024, 0.65], [0.024, 0.65],
      [0.21, 0.67], [0.23, 0.54], [0.16, 0.43], [0.024, 0.50], // bearded axe edge (+Z)
    ], 0.018, 0.004);
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

  // 8. weapon_bow (tall man-height longbow: deep D-curve toward +Z, string at -Z)
  addPart(root, 'weapon_bow', (mesh) => {
    const limb = loft(V(0, -0.70, -0.17), V(0, 0.70, -0.17), [
      [0.00, 0.012, 0.012, 0, -0.03],
      [0.12, 0.017, 0.017, 0, 0.09],
      [0.32, 0.021, 0.019, 0, 0.175],
      [0.50, 0.023, 0.025, 0, 0.20],
      [0.68, 0.021, 0.019, 0, 0.175],
      [0.88, 0.017, 0.017, 0, 0.09],
      [1.00, 0.012, 0.012, 0, -0.03],
    ], { segs: 6 });
    mesh(shadeVerts(limb, () => 0.90), M.wood);
    const grip = column([[-0.08, 0.028, 0.030, 0, 0.0], [0.08, 0.028, 0.030, 0, 0.0]], { segs: 6 });
    mesh(grip, M.leather);
    const str = shadeVerts(new THREE.BoxGeometry(0.008, 1.40, 0.008), () => 0.95);
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
    // Bowstring drawn back from both prod tips to the lock-nut + loaded steel-tipped quarrel resting on top
    for (const s of [-1, 1]) {
      const len = Math.hypot(0.33, 0.26);
      const str = shadeVerts(new THREE.BoxGeometry(len, 0.007, 0.007), () => 0.95);
      mesh(str, M.trim, [s * 0.165, 0.27, 0.016], [0, 0, s * Math.atan2(0.26, 0.33)]);
    }
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
    const steps = 10;
    for (let i = 0; i <= steps; i++) {
      const t = i / steps;
      const y = 0.02 + t * 1.32;
      const baseW = 0.036 * (1 - 0.55 * t);
      const wave = i > 1 && i < steps ? 0.016 * Math.sin(i * Math.PI * 0.95) : 0;
      pts.push([baseW + wave, y]);
    }
    pts.push([0.0, 1.42]);
    for (let i = steps; i >= 0; i--) {
      const t = i / steps;
      const y = 0.02 + t * 1.32;
      const baseW = 0.036 * (1 - 0.55 * t);
      const wave = i > 1 && i < steps ? -0.016 * Math.sin(i * Math.PI * 0.95) : 0;
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
    // Deep shamshir curve flaring wider toward the tip (kilij yelman) so the sweep reads at distance
    const blade = bladeExtrude([
      [0.015, 0.01], [0.028, 0.25], [0.064, 0.52], [0.140, 0.76], [0.235, 0.91],
      [0.172, 0.90], [0.080, 0.77], [0.020, 0.52], [-0.012, 0.25], [-0.016, 0.01],
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
    // Broad heavy cleaver: narrow at the hilt, swelling to maximum width near the clipped point
    const blade = bladeExtrude([
      [0.022, 0.02], [0.042, 0.28], [0.070, 0.52], [0.098, 0.68], [0.052, 0.80],
      [0.004, 0.78], [-0.018, 0.58], [-0.020, 0.30], [-0.016, 0.02],
    ], 0.014, 0.004);
    mesh(heightShade(blade, 0.0, 0.80, 0.86, 1.0), M.steel);
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

  // 18. weapon_club (heavy crude ogre cudgel: long tapering knotty shaft swelling to a huge knot, iron studs)
  addPart(root, 'weapon_club', (mesh) => {
    const body = column([
      [-0.18, 0.019, 0.019],
      [0.05, 0.022, 0.021],
      [0.34, 0.038, 0.034],
      [0.62, 0.072, 0.064],
      [0.82, 0.092, 0.080],
      [0.94, 0.096, 0.086],
      [1.02, 0.048, 0.046],
    ], { segs: 7 });
    mesh(body, M.wood);
    mesh(column([[-0.12, 0.024, 0.024], [0.08, 0.024, 0.024]], { segs: 7 }), M.leather);
    // One crude hammered iron band + big hand-forged studs around the knot
    const r = 0.094;
    mesh(column([[0.76, r, r * 0.92], [0.82, r, r * 0.92]], { segs: 7 }), M.mail);
    for (let i = 0; i < 5; i++) {
      const a = (i / 5) * Math.PI * 2;
      const stud = shadeVerts(new THREE.ConeGeometry(0.020, 0.045, 4), () => 0.94);
      mesh(stud, M.steel, [Math.cos(a) * (r + 0.006), 0.88, Math.sin(a) * (r * 0.92 + 0.006)], [Math.sin(a) * Math.PI * 0.5, 0, -Math.cos(a) * Math.PI * 0.5]);
    }
    for (let i = 0; i < 3; i++) {
      const a = (i / 3) * Math.PI * 2 + 0.5;
      const stud = shadeVerts(new THREE.ConeGeometry(0.016, 0.036, 4), () => 0.94);
      mesh(stud, M.steel, [Math.cos(a) * 0.078, 0.68, Math.sin(a) * 0.070], [Math.sin(a) * Math.PI * 0.5, 0, -Math.cos(a) * Math.PI * 0.5]);
    }
  });

  // 19. weapon_staff (mage gnarled dark-wood staff, big faceted crystal shard cradled in a forked wooden claw)
  addPart(root, 'weapon_staff', (mesh) => {
    // Gnarled shaft: uneven rings & slight wobble in the upper third
    const shaft = column([
      [-0.70, 0.016, 0.017],
      [0.0, 0.019, 0.020],
      [0.55, 0.021, 0.022],
      [0.95, 0.024, 0.023],
      [1.16, 0.028, 0.027],
      [1.28, 0.036, 0.034],
    ], { segs: 7 });
    mesh(shaft, M.wood);
    mesh(column([[-0.08, 0.023, 0.023], [0.12, 0.023, 0.023]], { segs: 7 }), M.leather);
    // Knotty collar where the claw splits
    mesh(column([[1.22, 0.038, 0.036], [1.32, 0.046, 0.042]], { segs: 7 }), M.wood);
    // Large elongated hexagonal crystal shard with a pointed crown (bright trim material)
    const crystalBody = shadeVerts(new THREE.CylinderGeometry(0.040, 0.056, 0.19, 6), () => 1.0);
    mesh(crystalBody, M.trim, [0, 1.475, 0]);
    const crystalTip = shadeVerts(new THREE.ConeGeometry(0.040, 0.11, 6), () => 1.0);
    mesh(crystalTip, M.trim, [0, 1.625, 0]);
    const crystalBase = shadeVerts(new THREE.ConeGeometry(0.056, 0.08, 6), () => 0.92);
    mesh(crystalBase, M.trim, [0, 1.345, 0], [Math.PI, 0, 0]);
    // Four gnarled wooden talons curving in to clutch the shard
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + Math.PI / 4;
      const prong = loft(V(Math.cos(a) * 0.030, 1.26, Math.sin(a) * 0.030), V(Math.cos(a) * 0.043, 1.47, Math.sin(a) * 0.043), [
        [0, 0.013, 0.013], [0.45, 0.011, 0.011, Math.cos(a) * 0.012, Math.sin(a) * 0.012], [0.8, 0.008, 0.008, Math.cos(a) * 0.006, Math.sin(a) * 0.006], [1.0, 0.004, 0.004],
      ], { segs: 4 });
      mesh(prong, M.wood);
    }
    // Small brass binding ring at the collar throat
    mesh(column([[1.18, 0.033, 0.032], [1.24, 0.035, 0.034]], { segs: 7 }), M.brass);
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
      [0.024, 1.48], [0.082, 1.66], [0.125, 1.90], [0.088, 2.10], [0.014, 2.20],
      [-0.030, 1.98], [-0.026, 1.66], [-0.018, 1.48],
    ], 0.012, 0.003);
    mesh(heightShade(blade, 1.48, 2.20, 0.86, 1.0), M.steel);
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
