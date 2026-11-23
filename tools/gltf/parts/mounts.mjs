import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildMounts(root) {
  // ---------- HORSES (13 parts) ----------
  const horseBarrel = (mesh, bodyMat = M.coat) => {
    // Muscular equine barrel along Z (-0.94..+0.92), chest forward (+Z), rump rear (-Z)
    const body = loft(V(0, 0.04, -0.92), V(0, 0.08, 0.90), [
      [0.00, 0.14, 0.14, 0, -0.02],
      [0.14, 0.31, 0.29, 0, 0.04],
      [0.32, 0.33, 0.31, 0, 0.03],
      [0.52, 0.30, 0.28, 0, -0.01],
      [0.74, 0.33, 0.31, 0, 0.04],
      [0.92, 0.27, 0.26, 0, 0.06],
      [1.00, 0.13, 0.14, 0, 0.04],
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.28, 0.38, 0.78, 1.0), bodyMat);
  };

  const horseSaddle = (mesh, padMat = M.cloth, saddleMat = M.leather) => {
    // Saddle cloth + high medieval cantle & pommel (Y 0.24..0.46)
    const blanket = shadeVerts(new THREE.BoxGeometry(0.68, 0.14, 0.72), () => 0.92);
    mesh(blanket, padMat, [0, 0.27, -0.02]);
    const seat = shadeVerts(new THREE.BoxGeometry(0.46, 0.08, 0.52), () => 0.88);
    mesh(seat, saddleMat, [0, 0.34, -0.02]);
    // Pommel (+Z) & cantle (-Z)
    mesh(shadeVerts(new THREE.BoxGeometry(0.40, 0.14, 0.07), () => 0.94), saddleMat, [0, 0.40, 0.22]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.42, 0.16, 0.07), () => 0.94), saddleMat, [0, 0.41, -0.26]);
  };

  addPart(root, 'horse_body', (mesh) => {
    horseBarrel(mesh, M.coat);
    horseSaddle(mesh, M.clothDark, M.leather);
  });

  addPart(root, 'horse_body_barded', (mesh) => {
    horseBarrel(mesh, M.coat);
    // Draped caparison cloth hanging down to Y = -0.52 around chest, flanks, and croup
    const caparison = loft(V(0, -0.06, -0.94), V(0, -0.04, 0.96), [
      [0.00, 0.18, 0.36],
      [0.14, 0.37, 0.44],
      [0.50, 0.38, 0.44],
      [0.86, 0.37, 0.44],
      [1.00, 0.20, 0.36],
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(caparison, -0.53, 0.36, 0.76, 1.0), M.cloth);
    // Gold lower hem trim
    const hem = shadeVerts(new THREE.BoxGeometry(0.76, 0.055, 1.76), () => 0.98);
    mesh(hem, M.trim, [0, -0.46, 0.01]);
    horseSaddle(mesh, M.cloth, M.leather);
  });

  addPart(root, 'horse_body_scale', (mesh) => {
    horseBarrel(mesh, M.coat);
    const barding = loft(V(0, -0.06, -0.92), V(0, -0.04, 0.93), [
      [0.00, 0.18, 0.34],
      [0.15, 0.37, 0.43],
      [0.50, 0.38, 0.43],
      [0.85, 0.37, 0.43],
      [1.00, 0.20, 0.34],
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(barding, -0.52, 0.35, 0.78, 1.0), M.mail);
    const rim = shadeVerts(new THREE.BoxGeometry(0.75, 0.05, 1.70), () => 0.96);
    mesh(rim, M.brass, [0, -0.45, 0.01]);
    horseSaddle(mesh, M.cloth, M.leather);
  });

  addPart(root, 'horse_body_fur', (mesh) => {
    horseBarrel(mesh, M.coat);
    // Shaggy steppe pelt blanket across back & flanks
    const pelt = loft(V(0, 0.10, -0.78), V(0, 0.12, 0.76), [
      [0.0, 0.24, 0.26], [0.25, 0.36, 0.32], [0.75, 0.36, 0.32], [1.0, 0.24, 0.26],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(pelt, -0.22, 0.40, 0.80, 1.0), M.hair);
    horseSaddle(mesh, M.cloth, M.leather);
  });

  // Horse necks (normalized Y -0.10..1.06, mane toward -Z)
  const buildHorseNeck = (mesh, neckMat, armorMat = null) => {
    const neck = loft(V(0, -0.10, 0), V(0, 1.06, 0), [
      [0.00, 0.21, 0.27, 0, -0.02],
      [0.35, 0.18, 0.23, 0, -0.02],
      [0.75, 0.14, 0.18, 0, -0.01],
      [1.00, 0.12, 0.15, 0, 0.0],
    ], { segs: 8 });
    mesh(heightShade(neck, -0.10, 1.06, 0.80, 1.0), neckMat);
    // Flowing mane crest along -Z
    const mane = loft(V(0, 0.04, -0.24), V(0, 1.02, -0.16), [
      [0, 0.055, 0.09], [0.5, 0.050, 0.10], [1, 0.036, 0.07],
    ], { segs: 6 });
    mesh(mane, M.coatDark);
    if (armorMat) {
      // Crinet neck armor plates along +Z/sides
      for (let i = 0; i < 4; i++) {
        const y = 0.12 + i * 0.24;
        const rx = 0.20 - i * 0.02;
        const rz = 0.25 - i * 0.025;
        mesh(column([[y - 0.08, rx, rz], [y + 0.08, rx * 0.94, rz * 0.94]], { segs: 8 }), armorMat);
      }
    }
  };
  addPart(root, 'horse_neck', (mesh) => buildHorseNeck(mesh, M.coat));
  addPart(root, 'horse_neck_barded', (mesh) => buildHorseNeck(mesh, M.cloth, M.steel));
  addPart(root, 'horse_neck_scale', (mesh) => buildHorseNeck(mesh, M.coat, M.mail));

  // Horse heads (neck socket at 0,0,0, muzzle toward +Z up to 0.68, ears at Y 0.28..0.34)
  const buildHorseHead = (mesh, shaffronMat = null) => {
    // Cranium + tapering equine muzzle (+Z)
    const head = loft(V(0, 0.06, -0.08), V(0, -0.07, 0.67), [
      [0.00, 0.145, 0.155],
      [0.24, 0.155, 0.165],
      [0.58, 0.105, 0.115],
      [0.88, 0.082, 0.088],
      [1.00, 0.064, 0.066],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(head, -0.16, 0.24, 0.82, 1.0), M.coat);
    // Pointed ears
    for (const sx of [-1, 1]) {
      const ear = shadeVerts(new THREE.ConeGeometry(0.036, 0.15, 5), () => 0.90);
      mesh(ear, M.coat, [sx * 0.088, 0.26, 0.04], [0.18, 0, -sx * 0.22]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.024, 5, 4), () => 0.15), M.dark, [sx * 0.132, 0.075, 0.22]);
    }
    // Leather bridle + bit rings
    mesh(shadeVerts(new THREE.BoxGeometry(0.22, 0.024, 0.028), () => 0.85), M.leather, [0, -0.04, 0.52]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.28, 0.024, 0.026), () => 0.85), M.leather, [0, 0.12, 0.16]);
    if (shaffronMat) {
      // Armored shaffron plate along bridge of nose
      const shaf = loft(V(0, 0.17, 0.05), V(0, 0.02, 0.58), [
        [0, 0.135, 0.048], [0.4, 0.125, 0.045], [1, 0.072, 0.036],
      ], { segs: 6, fwd: V(0, 1, 0) });
      mesh(shaf, shaffronMat);
      mesh(shadeVerts(new THREE.ConeGeometry(0.020, 0.09, 5), () => 1.0), M.brass, [0, 0.18, 0.24], [0.9, 0, 0]);
    }
  };
  addPart(root, 'horse_head', (mesh) => buildHorseHead(mesh, null));
  addPart(root, 'horse_head_barded', (mesh) => buildHorseHead(mesh, M.steel));
  addPart(root, 'horse_head_scale', (mesh) => buildHorseHead(mesh, M.mail));

  // Horse legs & tail (normalized Y 0..1)
  addPart(root, 'horse_leg_up', (mesh) => {
    const leg = loft(V(0, -0.09, 0), V(0, 1.06, 0), [
      [0.0, 0.12, 0.145], [0.45, 0.095, 0.115], [1.0, 0.068, 0.078],
    ], { segs: 7 });
    mesh(heightShade(leg, -0.09, 1.06, 0.78, 1.0), M.coat);
  });

  addPart(root, 'horse_leg_low', (mesh) => {
    const cannon = loft(V(0, -0.07, 0), V(0, 0.84, 0), [
      [0.0, 0.066, 0.076], [0.5, 0.052, 0.060], [1.0, 0.056, 0.064],
    ], { segs: 7 });
    mesh(heightShade(cannon, -0.07, 0.84, 0.78, 1.0), M.coat);
    // Dark hoof (+Z)
    const hoof = loft(V(0, 0.82, 0.01), V(0, 1.04, 0.04), [
      [0.0, 0.058, 0.068], [1.0, 0.072, 0.088],
    ], { segs: 7 });
    mesh(shadeVerts(hoof, () => 0.70), M.dark);
  });

  addPart(root, 'horse_tail', (mesh) => {
    const tail = loft(V(0, -0.05, 0), V(0, 1.04, 0), [
      [0.0, 0.045, 0.045], [0.35, 0.088, 0.092], [0.78, 0.078, 0.082], [1.0, 0.025, 0.025],
    ], { segs: 6 });
    mesh(heightShade(tail, -0.05, 1.04, 0.74, 1.0), M.coatDark);
  });

  // ---------- CAMELS (3 parts) ----------
  addPart(root, 'camel_body', (mesh) => {
    // Leaner desert barrel + prominent dorsal hump (Y up to 0.76)
    const body = loft(V(0, 0.04, -0.86), V(0, 0.08, 0.84), [
      [0.00, 0.13, 0.14],
      [0.16, 0.28, 0.28],
      [0.50, 0.32, 0.31],
      [0.84, 0.28, 0.27],
      [1.00, 0.13, 0.14],
    ], { segs: 9, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.28, 0.38, 0.80, 1.0), M.coat);
    // Single tall dromedary hump
    const hump = column([
      [0.24, 0.26, 0.42, 0, -0.02],
      [0.50, 0.21, 0.32, 0, -0.02],
      [0.74, 0.09, 0.14, 0, -0.02],
    ], { segs: 8 });
    mesh(heightShade(hump, 0.24, 0.74, 0.86, 1.0), M.coat);
    // Woven desert saddle blanket & pack trim
    mesh(shadeVerts(new THREE.BoxGeometry(0.64, 0.16, 0.64), () => 0.94), M.cloth, [0, 0.36, -0.02]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.66, 0.04, 0.66), () => 0.98), M.trim, [0, 0.28, -0.02]);
  });

  addPart(root, 'camel_neck', (mesh) => {
    // Curved swan-like camel neck (Y -0.08..1.06)
    const neck = loft(V(0, -0.08, 0), V(0, 1.06, 0), [
      [0.00, 0.165, 0.20, 0, 0.03],
      [0.35, 0.140, 0.17, 0, 0.08],
      [0.70, 0.120, 0.145, 0, 0.03],
      [1.00, 0.110, 0.130, 0, -0.02],
    ], { segs: 8 });
    mesh(heightShade(neck, -0.08, 1.06, 0.80, 1.0), M.coat);
  });

  addPart(root, 'camel_head', (mesh) => {
    const head = loft(V(0, 0.04, -0.08), V(0, -0.02, 0.53), [
      [0.00, 0.125, 0.125],
      [0.30, 0.135, 0.135],
      [0.65, 0.095, 0.100],
      [1.00, 0.075, 0.082],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(head, -0.11, 0.18, 0.82, 1.0), M.coat);
    // Small rounded camel ears + bridle
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.032, 5, 4), () => 0.88), M.coat, [sx * 0.095, 0.15, 0.02]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.022, 5, 4), () => 0.15), M.dark, [sx * 0.115, 0.055, 0.18]);
    }
    mesh(shadeVerts(new THREE.BoxGeometry(0.20, 0.022, 0.024), () => 0.90), M.cloth, [0, -0.01, 0.42]);
  });
}
