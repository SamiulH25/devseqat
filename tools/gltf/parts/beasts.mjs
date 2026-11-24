import { THREE, V, M, shadeVerts, heightShade, loft, pathLoft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

// Helper: curved tapered feather blade along +X/+Z for organic feathered wings (harpy, griffon)
function featherBlade(origin, tip, width, camber = 0.04) {
  const dir = tip.clone().sub(origin);
  const len = dir.length();
  const mid = origin.clone().lerp(tip, 0.55);
  mid.y += camber;
  const side = new THREE.Vector3(-dir.z, 0, dir.x).normalize();
  return pathLoft([
    { p: origin, rx: width * 0.25, rz: 0.012 },
    { p: origin.clone().lerp(mid, 0.5), rx: width * 0.52, rz: 0.014 },
    { p: mid, rx: width * 0.50, rz: 0.013 },
    { p: mid.clone().lerp(tip, 0.65), rx: width * 0.34, rz: 0.010 },
    { p: tip, rx: 0.006, rz: 0.005 },
  ], { segs: 6, fwd: V(0, 1, 0) });
}

// Helper: 2-bone organic limb segment (muscular taper with joint bulge)
function limbSeg(rTop, rMid, rBot, segs = 12, midT = 0.42, midZ = 0.03) {
  return pathLoft([
    { p: V(0, -0.05, 0), rx: rTop * 0.92, rz: rTop * 0.96 },
    { p: V(0, 0.08, midZ * 0.4), rx: rTop, rz: rTop * 1.08 },
    { p: V(0, midT, midZ), rx: rMid, rz: rMid * 1.06 },
    { p: V(0, 0.78, midZ * 0.2), rx: rBot * 1.08, rz: rBot * 1.06 },
    { p: V(0, 1.02, 0), rx: rBot, rz: rBot },
  ], { segs });
}

export function buildBeasts(root) {
  // Beast material convention (UnitMatTable.FillBeast in Mesh3D.cs):
  //   M.coat (11) = primary hide/fur/scales
  //   M.coatDark (12) = secondary mane/plates/wing membrane/loincloth
  //   M.dark (13) = claws/hooves/nose/beak/horns/gullet
  //   M.trim (3) = glowing eyes/golden beak/crest highlights
  //   M.neutral (0) = ivory fangs/tusks/teeth/claws

  // =========================================================================
  // SHARED 2-BONE LIMB SEGMENTS (high-poly muscular thighs, shins, paws)
  // =========================================================================
  addPart(root, 'quad_thigh', (mesh) => {
    const g = limbSeg(0.115, 0.095, 0.058, 12, 0.38, 0.04);
    mesh(heightShade(g, -0.05, 1.02, 0.70, 1.0), M.coat);
  });

  addPart(root, 'quad_shin', (mesh) => {
    const g = limbSeg(0.058, 0.046, 0.040, 12, 0.48, -0.02);
    mesh(heightShade(g, -0.05, 1.02, 0.72, 1.0), M.coat);
    const paw = pathLoft([
      { p: V(0, 0.96, -0.04), rx: 0.052, rz: 0.040 },
      { p: V(0, 0.98, 0.08), rx: 0.056, rz: 0.036 },
      { p: V(0, 0.99, 0.16), rx: 0.038, rz: 0.024 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(paw, M.dark);
    for (const cx of [-0.028, 0, 0.028]) {
      mesh(loft(V(cx, 0.99, 0.14), V(cx * 1.15, 1.01, 0.21), [[0, 0.011, 0.011], [1, 0.003, 0.003]], { segs: 5 }), M.neutral);
    }
  });

  addPart(root, 'bear_thigh', (mesh) => {
    const g = limbSeg(0.195, 0.165, 0.125, 14, 0.40, 0.05);
    mesh(heightShade(g, -0.05, 1.02, 0.68, 1.0), M.coat);
  });

  addPart(root, 'bear_shin', (mesh) => {
    const g = limbSeg(0.125, 0.110, 0.105, 14, 0.45, -0.02);
    mesh(heightShade(g, -0.05, 1.02, 0.70, 1.0), M.coat);
    const paw = pathLoft([
      { p: V(0, 0.94, -0.08), rx: 0.125, rz: 0.075 },
      { p: V(0, 0.97, 0.10), rx: 0.132, rz: 0.068 },
      { p: V(0, 0.98, 0.24), rx: 0.098, rz: 0.048 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(paw, M.coatDark);
    for (const cx of [-0.072, -0.024, 0.024, 0.072]) {
      mesh(pathLoft([
        { p: V(cx, 0.97, 0.20), rx: 0.020, rz: 0.020 },
        { p: V(cx * 1.1, 0.98, 0.28), rx: 0.013, rz: 0.013 },
        { p: V(cx * 1.15, 1.02, 0.34), rx: 0.004, rz: 0.004 },
      ], { segs: 6 }), M.neutral);
    }
  });

  // =========================================================================
  // 1. WOLF / DIRE WOLF / ALPHA WOLF / HOUND / WARG (high-poly sculpted)
  // =========================================================================
  addPart(root, 'wolf_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, 0.00, -0.58), rx: 0.07, rz: 0.07 },
      { p: V(0, 0.06, -0.44), rx: 0.18, rz: 0.21 }, // sculpted haunch
      { p: V(0, 0.08, -0.26), rx: 0.15, rz: 0.17 },
      { p: V(0, 0.04, -0.06), rx: 0.125, rz: 0.145 }, // deep tucked waist
      { p: V(0, 0.07, 0.16), rx: 0.20, rz: 0.26 },   // deep muscular ribcage
      { p: V(0, 0.13, 0.38), rx: 0.21, rz: 0.25 },   // broad shoulders
      { p: V(0, 0.17, 0.54), rx: 0.14, rz: 0.16 },   // thick neck
      { p: V(0, 0.19, 0.62), rx: 0.11, rz: 0.12 },
    ], {
      segs: 16, fwd: V(0, 1, 0),
      profile: (th, t) => {
        const top = Math.sin(th);
        const ridge = top > 0.5 ? 1.08 : 1.0;
        const belly = top < -0.4 && t > 0.25 && t < 0.55 ? 0.84 : 1.0;
        return [1.0, ridge * belly];
      },
    });
    mesh(heightShade(body, -0.22, 0.42, 0.68, 1.0), M.coat);

    // Rich layered hackle ruff around shoulders & neck
    const ruff = pathLoft([
      { p: V(0, 0.15, 0.04), rx: 0.17, rz: 0.12 },
      { p: V(0, 0.22, 0.24), rx: 0.24, rz: 0.18 },
      { p: V(0, 0.24, 0.44), rx: 0.22, rz: 0.16 },
      { p: V(0, 0.20, 0.58), rx: 0.14, rz: 0.11 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(ruff, 0.04, 0.42, 0.72, 1.0), M.coatDark);
    for (const [z, y, h, w] of [[0.10, 0.31, 0.14, 0.06], [0.22, 0.36, 0.18, 0.07], [0.34, 0.37, 0.18, 0.07], [0.46, 0.33, 0.14, 0.06]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(w, h, 7), () => 0.85), M.coatDark, [0, y, z], [-0.6, 0, 0]);
    }
  });

  addPart(root, 'wolf_head', (mesh) => {
    const cranium = pathLoft([
      { p: V(0, 0.04, -0.14), rx: 0.135, rz: 0.135 },
      { p: V(0, 0.07, 0.04), rx: 0.155, rz: 0.148 },
      { p: V(0, 0.06, 0.18), rx: 0.125, rz: 0.115 },
      { p: V(0, 0.03, 0.34), rx: 0.082, rz: 0.068 },
      { p: V(0, 0.02, 0.50), rx: 0.058, rz: 0.048 },
      { p: V(0, 0.01, 0.58), rx: 0.038, rz: 0.032 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(cranium, -0.06, 0.22, 0.74, 1.0), M.coat);

    const jaw = pathLoft([
      { p: V(0, -0.05, 0.06), rx: 0.10, rz: 0.055 },
      { p: V(0, -0.09, 0.28), rx: 0.068, rz: 0.040 },
      { p: V(0, -0.11, 0.50), rx: 0.042, rz: 0.026 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(jaw, M.coatDark);

    for (const sx of [-1, 1]) {
      // Sculpted cheek fur tufts
      mesh(shadeVerts(new THREE.ConeGeometry(0.072, 0.20, 7), () => 0.84), M.coatDark, [sx * 0.14, -0.01, 0.04], [-0.45, 0, sx * 1.15]);
      // Tall cupped wolf ears
      const ear = pathLoft([
        { p: V(sx * 0.085, 0.15, 0.02), rx: 0.052, rz: 0.038 },
        { p: V(sx * 0.115, 0.25, 0.05), rx: 0.038, rz: 0.026 },
        { p: V(sx * 0.135, 0.34, 0.07), rx: 0.008, rz: 0.008 },
      ], { segs: 8 });
      mesh(ear, M.coatDark);
      mesh(shadeVerts(new THREE.BoxGeometry(0.062, 0.030, 0.09), () => 0.78), M.coatDark, [sx * 0.092, 0.125, 0.20], [0.22, sx * 0.22, -sx * 0.22]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.026, 8, 6), () => 1.0), M.trim, [sx * 0.098, 0.088, 0.22]);
      // Upper & lower curved fangs
      mesh(shadeVerts(new THREE.ConeGeometry(0.015, 0.068, 6), () => 1.0), M.neutral, [sx * 0.045, -0.025, 0.46], [Math.PI, 0, 0]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.012, 0.050, 6), () => 1.0), M.neutral, [sx * 0.038, -0.075, 0.43], [0, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.038, 8, 6), () => 0.14), M.dark, [0, 0.02, 0.58]);
  });

  addPart(root, 'wolf_leg', (mesh) => {
    const leg = limbSeg(0.082, 0.058, 0.040, 10, 0.45, 0.03);
    mesh(heightShade(leg, -0.05, 1.02, 0.72, 1.0), M.coat);
  });

  addPart(root, 'wolf_tail', (mesh) => {
    const tail = pathLoft([
      { p: V(0, -0.04, 0), rx: 0.048, rz: 0.048 },
      { p: V(0, 0.28, 0.06), rx: 0.105, rz: 0.112 },
      { p: V(0, 0.68, 0.02), rx: 0.092, rz: 0.096 },
      { p: V(0, 1.06, -0.08), rx: 0.020, rz: 0.020 },
    ], { segs: 12 });
    mesh(heightShade(tail, -0.04, 1.06, 0.72, 1.0), M.coatDark);
  });

  // =========================================================================
  // 2. BEAR / CAVE BEAR / BEAR-KING (high-poly sculpted)
  // =========================================================================
  addPart(root, 'bear_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, 0.00, -0.68), rx: 0.14, rz: 0.15 },
      { p: V(0, 0.10, -0.48), rx: 0.36, rz: 0.35 },
      { p: V(0, 0.12, -0.22), rx: 0.37, rz: 0.37 },
      { p: V(0, 0.16, 0.04), rx: 0.38, rz: 0.40 },
      { p: V(0, 0.26, 0.28), rx: 0.41, rz: 0.48 }, // massive ursine shoulder hump
      { p: V(0, 0.20, 0.50), rx: 0.32, rz: 0.35 },
      { p: V(0, 0.14, 0.64), rx: 0.18, rz: 0.20 },
    ], {
      segs: 18, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) > 0.2 ? 1.10 : 0.94],
    });
    mesh(heightShade(body, -0.34, 0.72, 0.68, 1.0), M.coat);

    // Shaggy dark shoulder cape & fur ridge
    const cape = pathLoft([
      { p: V(0, 0.36, -0.30), rx: 0.24, rz: 0.12 },
      { p: V(0, 0.48, 0.14), rx: 0.34, rz: 0.20 },
      { p: V(0, 0.46, 0.36), rx: 0.30, rz: 0.18 },
      { p: V(0, 0.34, 0.54), rx: 0.20, rz: 0.12 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(cape, 0.20, 0.68, 0.72, 1.0), M.coatDark);
    for (const [z, y, h] of [[-0.14, 0.48, 0.16], [0.06, 0.58, 0.20], [0.24, 0.64, 0.22], [0.40, 0.52, 0.16]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.095, h, 7), () => 0.82), M.coatDark, [0, y, z], [-0.5, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.095, 8, 6), () => 0.76), M.coatDark, [0, 0.08, -0.70]);
  });

  addPart(root, 'bear_head', (mesh) => {
    const cranium = pathLoft([
      { p: V(0, 0.04, -0.14), rx: 0.23, rz: 0.22 },
      { p: V(0, 0.07, 0.10), rx: 0.26, rz: 0.24 },
      { p: V(0, 0.04, 0.30), rx: 0.17, rz: 0.15 },
      { p: V(0, 0.01, 0.48), rx: 0.125, rz: 0.105 },
      { p: V(0, 0.00, 0.58), rx: 0.092, rz: 0.078 },
    ], { segs: 16, fwd: V(0, 1, 0) });
    mesh(heightShade(cranium, -0.18, 0.32, 0.72, 1.0), M.coat);

    const jaw = pathLoft([
      { p: V(0, -0.09, 0.08), rx: 0.15, rz: 0.085 },
      { p: V(0, -0.15, 0.34), rx: 0.105, rz: 0.062 },
      { p: V(0, -0.17, 0.52), rx: 0.076, rz: 0.044 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(jaw, M.coatDark);

    for (const sx of [-1, 1]) {
      const ear = shadeVerts(new THREE.SphereGeometry(0.085, 10, 8), () => 0.84);
      ear.scale(1.0, 1.0, 0.52);
      mesh(ear, M.coatDark, [sx * 0.185, 0.25, 0.05]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.085, 0.22, 7), () => 0.82), M.coatDark, [sx * 0.22, -0.02, 0.06], [-0.3, 0, sx * 1.2]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.088, 0.040, 0.10), () => 0.78), M.coatDark, [sx * 0.13, 0.15, 0.24], [0.2, sx * 0.2, -sx * 0.2]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.032, 8, 6), () => 0.96), M.trim, [sx * 0.145, 0.105, 0.28]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.022, 0.082, 6), () => 1.0), M.neutral, [sx * 0.072, -0.065, 0.46], [Math.PI, 0, 0]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.019, 0.066, 6), () => 1.0), M.neutral, [sx * 0.062, -0.125, 0.43], [0, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.062, 10, 8), () => 0.12), M.dark, [0, 0.01, 0.58]);
  });

  addPart(root, 'bear_leg', (mesh) => {
    const leg = limbSeg(0.16, 0.13, 0.10, 12, 0.45, 0.03);
    mesh(heightShade(leg, -0.05, 1.02, 0.72, 1.0), M.coat);
  });

  // =========================================================================
  // 3. GIANT SPIDER / BROOD MOTHER (high-poly armored chitin)
  // =========================================================================
  addPart(root, 'spider_abdomen', (mesh) => {
    const abd = pathLoft([
      { p: V(0, 0.02, -0.52), rx: 0.06, rz: 0.05 },
      { p: V(0, 0.09, -0.30), rx: 0.32, rz: 0.28 },
      { p: V(0, 0.12, 0.00), rx: 0.39, rz: 0.34 },
      { p: V(0, 0.08, 0.28), rx: 0.28, rz: 0.25 },
      { p: V(0, 0.02, 0.46), rx: 0.09, rz: 0.09 },
    ], { segs: 16, fwd: V(0, 1, 0) });
    mesh(heightShade(abd, -0.28, 0.46, 0.68, 1.0), M.coat);
    for (const [z, r] of [[-0.26, 0.29], [-0.06, 0.37], [0.14, 0.32]]) {
      const plate = shadeVerts(new THREE.TorusGeometry(r, 0.036, 8, 16, Math.PI * 1.2), () => 0.96);
      mesh(plate, M.coatDark, [0, 0.10, z], [0, 0, -Math.PI * 0.1]);
    }
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.036, 0.14, 6), () => 0.8), M.dark, [sx * 0.06, 0.02, -0.54], [-Math.PI / 2, 0, sx * 0.2]);
    }
  });

  addPart(root, 'spider_head', (mesh) => {
    const ceph = pathLoft([
      { p: V(0, 0.02, -0.20), rx: 0.19, rz: 0.15 },
      { p: V(0, 0.05, 0.04), rx: 0.25, rz: 0.19 },
      { p: V(0, 0.02, 0.24), rx: 0.18, rz: 0.15 },
      { p: V(0, -0.02, 0.34), rx: 0.10, rz: 0.09 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(ceph, -0.16, 0.24, 0.74, 1.0), M.coat);
    for (const [ex, ey, ez, er] of [
      [-0.062, 0.085, 0.27, 0.038], [0.062, 0.085, 0.27, 0.038],
      [-0.132, 0.065, 0.23, 0.026], [0.132, 0.065, 0.23, 0.026],
      [-0.034, 0.132, 0.24, 0.022], [0.034, 0.132, 0.24, 0.022],
      [-0.172, 0.035, 0.18, 0.020], [0.172, 0.035, 0.18, 0.020],
    ]) {
      mesh(shadeVerts(new THREE.SphereGeometry(er, 8, 6), () => 1.0), M.coatDark, [ex, ey, ez]);
    }
    for (const sx of [-1, 1]) {
      const fang = pathLoft([
        { p: V(sx * 0.085, -0.02, 0.25), rx: 0.052, rz: 0.052 },
        { p: V(sx * 0.095, -0.10, 0.38), rx: 0.036, rz: 0.036 },
        { p: V(sx * 0.03, -0.21, 0.40), rx: 0.008, rz: 0.008 },
      ], { segs: 8 });
      mesh(fang, M.dark);
      const palp = pathLoft([
        { p: V(sx * 0.17, -0.02, 0.21), rx: 0.032, rz: 0.032 },
        { p: V(sx * 0.26, 0.03, 0.36), rx: 0.024, rz: 0.024 },
        { p: V(sx * 0.20, -0.09, 0.45), rx: 0.015, rz: 0.015 },
      ], { segs: 7 });
      mesh(palp, M.coat);
    }
  });

  addPart(root, 'spider_leg', (mesh) => {
    const seg = pathLoft([
      { p: V(0, -0.04, 0), rx: 0.058, rz: 0.058 },
      { p: V(0, 0.14, 0), rx: 0.072, rz: 0.072 },
      { p: V(0, 0.58, 0), rx: 0.044, rz: 0.044 },
      { p: V(0, 1.04, 0), rx: 0.014, rz: 0.014 },
    ], { segs: 9 });
    mesh(heightShade(seg, -0.04, 1.04, 0.70, 1.0), M.dark);
    for (const y of [0.32, 0.62]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.022, 0.095, 5), () => 0.92), M.coatDark, [0, y, 0.048], [0.95, 0, 0]);
    }
  });

  // =========================================================================
  // 4. WYVERN (high-poly sculpted drake with huge sweeping 5-fingered wings)
  // =========================================================================
  addPart(root, 'wyvern_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, 0.00, -0.66), rx: 0.12, rz: 0.13 },
      { p: V(0, 0.05, -0.38), rx: 0.25, rz: 0.27 },
      { p: V(0, 0.10, -0.06), rx: 0.30, rz: 0.34 },
      { p: V(0, 0.14, 0.24), rx: 0.34, rz: 0.40 }, // deep armored dragon keel
      { p: V(0, 0.16, 0.48), rx: 0.26, rz: 0.28 },
      { p: V(0, 0.14, 0.64), rx: 0.16, rz: 0.17 },
    ], {
      segs: 16, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) < -0.2 ? 1.15 : 1.0],
    });
    mesh(heightShade(body, -0.32, 0.54, 0.68, 1.0), M.coat);
    // Armored belly scute plates
    for (const z of [-0.32, -0.10, 0.12, 0.34]) {
      const scute = shadeVerts(new THREE.BoxGeometry(0.32, 0.06, 0.18), () => 0.90);
      mesh(scute, M.coatDark, [0, -0.22, z], [0.1, 0, 0]);
    }
    // Tall swept dorsal crest spines
    for (const [z, y, h] of [[-0.44, 0.30, 0.16], [-0.22, 0.38, 0.22], [0.02, 0.44, 0.26], [0.24, 0.48, 0.26], [0.44, 0.42, 0.20]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.048, h, 5), () => 0.96), M.coatDark, [0, y, z], [-0.5, 0, 0]);
    }
  });

  addPart(root, 'wyvern_neck', (mesh) => {
    const neck = pathLoft([
      { p: V(0, -0.08, 0), rx: 0.18, rz: 0.19 },
      { p: V(0, 0.32, -0.07), rx: 0.15, rz: 0.16 },
      { p: V(0, 0.72, 0.05), rx: 0.13, rz: 0.14 },
      { p: V(0, 1.08, 0), rx: 0.115, rz: 0.12 },
    ], { segs: 14 });
    mesh(heightShade(neck, -0.08, 1.08, 0.72, 1.0), M.coat);
    for (const y of [0.16, 0.44, 0.72, 0.94]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.038, 0.15, 5), () => 0.94), M.coatDark, [0, y, -0.15], [-1.3, 0, 0]);
    }
  });

  addPart(root, 'wyvern_head', (mesh) => {
    const cranium = pathLoft([
      { p: V(0, 0.06, -0.14), rx: 0.175, rz: 0.155 },
      { p: V(0, 0.09, 0.10), rx: 0.195, rz: 0.165 },
      { p: V(0, 0.05, 0.34), rx: 0.125, rz: 0.105 },
      { p: V(0, 0.03, 0.56), rx: 0.080, rz: 0.065 },
      { p: V(0, 0.02, 0.64), rx: 0.052, rz: 0.044 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(cranium, -0.08, 0.26, 0.74, 1.0), M.coat);
    const jaw = pathLoft([
      { p: V(0, -0.07, 0.06), rx: 0.12, rz: 0.062 },
      { p: V(0, -0.12, 0.34), rx: 0.082, rz: 0.045 },
      { p: V(0, -0.13, 0.56), rx: 0.050, rz: 0.030 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(jaw, M.coatDark);
    mesh(shadeVerts(new THREE.ConeGeometry(0.030, 0.13, 6), () => 0.96), M.dark, [0, 0.10, 0.52], [0.35, 0, 0]);
    for (const sx of [-1, 1]) {
      const horn = pathLoft([
        { p: V(sx * 0.105, 0.15, 0.02), rx: 0.048, rz: 0.048 },
        { p: V(sx * 0.20, 0.28, -0.16), rx: 0.032, rz: 0.032 },
        { p: V(sx * 0.28, 0.38, -0.38), rx: 0.008, rz: 0.008 },
      ], { segs: 8 });
      mesh(horn, M.dark);
      const cheek = loft(V(sx * 0.15, 0.02, -0.02), V(sx * 0.25, -0.01, -0.22), [[0, 0.032, 0.032], [1, 0.006, 0.006]], { segs: 6 });
      mesh(cheek, M.dark);
      mesh(shadeVerts(new THREE.SphereGeometry(0.032, 8, 6), () => 1.0), M.trim, [sx * 0.135, 0.105, 0.23]);
      for (const zf of [0.28, 0.40, 0.50]) {
        mesh(shadeVerts(new THREE.ConeGeometry(0.016, 0.068, 5), () => 1.0), M.neutral, [sx * 0.060, -0.025, zf], [Math.PI, 0, 0]);
      }
    }
  });

  addPart(root, 'wyvern_wing', (mesh) => {
    // High-craft arched dragon wing with curved forearm, thumb claw, 4 curved finger spars, and smooth scalloped membrane
    const arm = pathLoft([
      { p: V(0.0, 0.0, 0.0), rx: 0.068, rz: 0.068 },
      { p: V(0.32, 0.16, 0.16), rx: 0.056, rz: 0.056 },
      { p: V(0.72, 0.24, 0.24), rx: 0.042, rz: 0.042 },
      { p: V(1.16, 0.10, 0.10), rx: 0.018, rz: 0.018 },
    ], { segs: 8 });
    mesh(arm, M.coat);
    mesh(pathLoft([
      { p: V(0.34, 0.18, 0.18), rx: 0.026, rz: 0.026 },
      { p: V(0.38, 0.30, 0.30), rx: 0.016, rz: 0.016 },
      { p: V(0.36, 0.38, 0.38), rx: 0.004, rz: 0.004 },
    ], { segs: 6 }), M.dark);
    const topPts = [
      V(0.0, 0.0, 0.0), V(0.32, 0.16, 0.16), V(0.56, 0.21, 0.21), V(0.84, 0.22, 0.21), V(1.16, 0.10, 0.10),
    ];
    const botPts = [
      V(0.0, -0.06, -0.60), V(0.26, -0.08, -0.78), V(0.54, -0.06, -0.84), V(0.88, -0.04, -0.74), V(1.22, -0.06, -0.48),
    ];
    for (let i = 1; i < topPts.length; i++) {
      const mid = topPts[i].clone().lerp(botPts[i], 0.5); mid.y += 0.08;
      mesh(pathLoft([
        { p: topPts[i], rx: 0.022, rz: 0.022 },
        { p: mid, rx: 0.015, rz: 0.015 },
        { p: botPts[i], rx: 0.007, rz: 0.007 },
      ], { segs: 5 }), M.coatDark);
    }
    // 2-row curved cambered membrane sheet
    const pos = [];
    const idx = [];
    for (let i = 0; i < 5; i++) {
      const a = topPts[i], b = botPts[i], m = a.clone().lerp(b, 0.5); m.y += 0.08;
      pos.push(a.x, a.y, a.z, m.x, m.y, m.z, b.x, b.y, b.z);
    }
    for (let i = 0; i < 4; i++) {
      const r0 = i * 3, r1 = (i + 1) * 3;
      idx.push(r0, r1, r1 + 1, r0, r1 + 1, r0 + 1);
      idx.push(r0 + 1, r1 + 1, r1 + 2, r0 + 1, r1 + 2, r0 + 2);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    mesh(shadeVerts(g, (x, y) => 0.74 + 0.16 * Math.min(1, x) + 0.10 * Math.max(0, y)), M.coatDark);
  });

  addPart(root, 'wyvern_leg', (mesh) => {
    const leg = pathLoft([
      { p: V(0, -0.06, 0.05), rx: 0.13, rz: 0.14 },
      { p: V(0, 0.34, 0.14), rx: 0.082, rz: 0.090 },
      { p: V(0, 0.66, -0.10), rx: 0.062, rz: 0.068 },
      { p: V(0, 0.94, 0.02), rx: 0.055, rz: 0.060 },
    ], { segs: 12 });
    mesh(heightShade(leg, -0.06, 0.94, 0.72, 1.0), M.coat);
    for (const cx of [-0.058, 0, 0.058]) {
      mesh(pathLoft([
        { p: V(cx, 0.94, 0.02), rx: 0.026, rz: 0.026 },
        { p: V(cx * 1.25, 0.98, 0.14), rx: 0.016, rz: 0.016 },
        { p: V(cx * 1.4, 1.04, 0.24), rx: 0.005, rz: 0.005 },
      ], { segs: 6 }), M.dark);
    }
    mesh(loft(V(0, 0.94, -0.02), V(0, 1.01, -0.16), [[0, 0.024, 0.024], [1, 0.005, 0.005]], { segs: 5 }), M.dark);
  });

  addPart(root, 'wyvern_tail', (mesh) => {
    const seg = loft(V(0, -0.06, 0), V(0, 1.06, 0), [
      [0.0, 0.115, 0.115], [0.5, 0.088, 0.088], [1.0, 0.064, 0.064],
    ], { segs: 12 });
    mesh(heightShade(seg, -0.06, 1.06, 0.72, 1.0), M.coat);
    for (const y of [0.25, 0.70]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.032, 0.13, 5), () => 0.94), M.coatDark, [0, y, -0.10], [-1.3, 0, 0]);
    }
  });

  addPart(root, 'wyvern_sting', (mesh) => {
    const tip = lathe([[0.066, -0.05], [0.112, 0.35], [0.052, 0.74], [0.0, 1.12]], 12);
    mesh(tip, M.dark);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.036, 0.26, 5), () => 0.96), M.coatDark, [sx * 0.082, 0.36, 0], [0, 0, sx * 2.55]);
    }
  });

  // =========================================================================
  // 5. BOAR / TUSKER / GREAT BOAR (high-poly sculpted)
  // =========================================================================
  addPart(root, 'boar_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, -0.02, -0.56), rx: 0.12, rz: 0.13 },
      { p: V(0, 0.04, -0.36), rx: 0.26, rz: 0.26 },
      { p: V(0, 0.08, -0.04), rx: 0.31, rz: 0.34 },
      { p: V(0, 0.16, 0.28), rx: 0.34, rz: 0.40 },
      { p: V(0, 0.12, 0.54), rx: 0.24, rz: 0.26 },
    ], {
      segs: 18, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) > 0.3 ? 1.18 : 0.95],
    });
    mesh(heightShade(body, -0.28, 0.56, 0.68, 1.0), M.coat);
    for (const [z, y, h] of [[-0.28, 0.34, 0.15], [-0.10, 0.44, 0.22], [0.08, 0.50, 0.26], [0.26, 0.54, 0.26], [0.42, 0.46, 0.20]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.056, h, 7), () => 0.80), M.coatDark, [0, y, z], [-0.4, 0, 0]);
    }
    const tail = pathLoft([
      { p: V(0, 0.04, -0.54), rx: 0.026, rz: 0.026 },
      { p: V(0, 0.02, -0.70), rx: 0.022, rz: 0.022 },
      { p: V(0, -0.08, -0.76), rx: 0.048, rz: 0.048 },
    ], { segs: 8 });
    mesh(tail, M.coatDark);
  });

  addPart(root, 'boar_head', (mesh) => {
    const skull = pathLoft([
      { p: V(0, 0.07, -0.10), rx: 0.22, rz: 0.24 },
      { p: V(0, 0.02, 0.16), rx: 0.19, rz: 0.18 },
      { p: V(0, -0.04, 0.38), rx: 0.12, rz: 0.11 },
      { p: V(0, -0.06, 0.56), rx: 0.088, rz: 0.082 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.16, 0.28, 0.72, 1.0), M.coat);
    const snout = shadeVerts(new THREE.CylinderGeometry(0.092, 0.086, 0.052, 12), () => 0.84);
    mesh(snout, M.coatDark, [0, -0.055, 0.58], [Math.PI / 2 - 0.15, 0, 0]);
    for (const sx of [-1, 1]) {
      const tusk = pathLoft([
        { p: V(sx * 0.075, -0.11, 0.36), rx: 0.040, rz: 0.040 },
        { p: V(sx * 0.20, -0.07, 0.50), rx: 0.032, rz: 0.032 },
        { p: V(sx * 0.28, 0.10, 0.56), rx: 0.020, rz: 0.020 },
        { p: V(sx * 0.24, 0.26, 0.52), rx: 0.005, rz: 0.005 },
      ], { segs: 8 });
      mesh(tusk, M.neutral);
      mesh(shadeVerts(new THREE.ConeGeometry(0.058, 0.17, 7), () => 0.84), M.coatDark, [sx * 0.15, 0.24, 0.02], [0.25, 0, -sx * 0.45]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.026, 8, 6), () => 1.0), M.trim, [sx * 0.145, 0.065, 0.23]);
    }
  });

  addPart(root, 'boar_leg', (mesh) => {
    const leg = limbSeg(0.095, 0.068, 0.055, 10, 0.45, 0.02);
    mesh(heightShade(leg, -0.05, 1.0, 0.72, 1.0), M.coat);
    const hoof = shadeVerts(new THREE.CylinderGeometry(0.058, 0.070, 0.12, 10), () => 0.20);
    mesh(hoof, M.dark, [0, 0.96, 0.02]);
  });

  // =========================================================================
  // 6. HARPY (high-poly feathered avian predator with curved tapered plumes)
  // =========================================================================
  addPart(root, 'harpy_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, -0.34, -0.08), rx: 0.12, rz: 0.11 },
      { p: V(0, 0.0, 0.02), rx: 0.19, rz: 0.18 },
      { p: V(0, 0.28, 0.08), rx: 0.24, rz: 0.22 },
      { p: V(0, 0.46, 0.05), rx: 0.13, rz: 0.13 },
    ], { segs: 14 });
    mesh(heightShade(body, -0.34, 0.46, 0.72, 1.0), M.coat);
    for (const ang of [-0.42, -0.21, 0, 0.21, 0.42]) {
      mesh(featherBlade(V(0, -0.28, -0.06), V(Math.sin(ang) * 0.34, -0.44, -0.52), 0.09, 0.03), M.coatDark);
    }
  });

  addPart(root, 'harpy_head', (mesh) => {
    const skull = shadeVerts(new THREE.SphereGeometry(0.155, 14, 10), () => 0.92);
    skull.scale(0.95, 1.06, 1.14);
    mesh(skull, M.coat, [0, 0.04, 0.0]);
    for (const [y, z, rx] of [[0.18, -0.04, -0.50], [0.13, -0.14, -0.82], [0.06, -0.18, -1.12]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.052, 0.26, 7), () => 0.86), M.coatDark, [0, y, z], [rx, 0, 0]);
    }
    const beak = pathLoft([
      { p: V(0, 0.02, 0.12), rx: 0.060, rz: 0.064 },
      { p: V(0, 0.00, 0.28), rx: 0.034, rz: 0.040 },
      { p: V(0, -0.07, 0.34), rx: 0.006, rz: 0.008 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(beak, M.dark);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.028, 8, 6), () => 1.0), M.trim, [sx * 0.092, 0.078, 0.12]);
    }
  });

  addPart(root, 'harpy_wing', (mesh) => {
    // Organic sculpted feathered wing: curved muscular arm + 9 overlapping curved tapered feather plumes!
    const arm = pathLoft([
      { p: V(0.0, 0.0, 0.0), rx: 0.065, rz: 0.078 },
      { p: V(0.38, 0.12, 0.10), rx: 0.052, rz: 0.065 },
      { p: V(0.84, 0.10, 0.06), rx: 0.032, rz: 0.042 },
      { p: V(1.08, 0.02, 0.00), rx: 0.015, rz: 0.018 },
    ], { segs: 8 });
    mesh(arm, M.coat);
    // Covert upper feather row + long primary/secondary flight feathers
    for (let i = 0; i < 9; i++) {
      const t = i / 8;
      const rootX = 0.08 + t * 0.96;
      const rootY = Math.sin(t * Math.PI) * 0.11;
      const rootZ = Math.sin(t * Math.PI) * 0.09;
      const len = 0.42 + Math.sin(t * Math.PI * 0.82) * 0.38;
      const tipX = rootX + t * 0.22;
      const tipZ = rootZ - len;
      mesh(featherBlade(V(rootX, rootY, rootZ), V(tipX, rootY - 0.06, tipZ), 0.13, 0.05), i % 2 === 0 ? M.coat : M.coatDark);
    }
  });

  // =========================================================================
  // 7. GRIFFON (high-poly sculpted lion + eagle)
  // =========================================================================
  addPart(root, 'griffon_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, 0.00, -0.62), rx: 0.12, rz: 0.12 },
      { p: V(0, 0.09, -0.40), rx: 0.26, rz: 0.27 },
      { p: V(0, 0.06, -0.08), rx: 0.18, rz: 0.20 },
      { p: V(0, 0.14, 0.26), rx: 0.31, rz: 0.36 },
      { p: V(0, 0.24, 0.54), rx: 0.22, rz: 0.24 },
    ], { segs: 16, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.26, 0.52, 0.70, 1.0), M.coat);
    for (let i = 0; i < 10; i++) {
      const ang = (i / 10) * Math.PI * 2;
      mesh(featherBlade(V(Math.cos(ang) * 0.16, 0.22 + Math.sin(ang) * 0.16, 0.52),
        V(Math.cos(ang) * 0.24, 0.16 + Math.sin(ang) * 0.22, 0.22), 0.11, 0.03), M.coatDark);
    }
  });

  addPart(root, 'griffon_head', (mesh) => {
    const skull = pathLoft([
      { p: V(0, 0.04, -0.12), rx: 0.17, rz: 0.16 },
      { p: V(0, 0.07, 0.10), rx: 0.185, rz: 0.17 },
      { p: V(0, 0.04, 0.28), rx: 0.12, rz: 0.11 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.12, 0.24, 0.76, 1.0), M.coatDark);
    const beak = pathLoft([
      { p: V(0, 0.03, 0.24), rx: 0.092, rz: 0.102 },
      { p: V(0, 0.01, 0.46), rx: 0.058, rz: 0.072 },
      { p: V(0, -0.10, 0.55), rx: 0.010, rz: 0.012 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(beak, M.trim);
    for (const sx of [-1, 1]) {
      mesh(featherBlade(V(sx * 0.11, 0.16, 0.04), V(sx * 0.24, 0.34, -0.26), 0.09, 0.03), M.coatDark);
      mesh(shadeVerts(new THREE.SphereGeometry(0.030, 8, 6), () => 1.0), M.trim, [sx * 0.132, 0.10, 0.20]);
    }
  });

  // =========================================================================
  // 8. BASILISK (high-poly armored 6-legged crowned reptile)
  // =========================================================================
  addPart(root, 'basilisk_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, 0.0, -0.66), rx: 0.15, rz: 0.12 },
      { p: V(0, 0.04, -0.36), rx: 0.32, rz: 0.22 },
      { p: V(0, 0.06, 0.04), rx: 0.37, rz: 0.25 },
      { p: V(0, 0.07, 0.40), rx: 0.30, rz: 0.22 },
      { p: V(0, 0.06, 0.64), rx: 0.18, rz: 0.15 },
    ], { segs: 16, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.20, 0.32, 0.70, 1.0), M.coat);
    for (const z of [-0.46, -0.24, -0.02, 0.20, 0.42]) {
      const plate = shadeVerts(new THREE.BoxGeometry(0.36, 0.065, 0.17), () => 0.88);
      mesh(plate, M.coatDark, [0, 0.26, z], [-0.15, 0, 0]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.042, 0.19, 6), () => 0.96), M.trim, [0, 0.35, z], [-0.42, 0, 0]);
    }
  });

  addPart(root, 'basilisk_head', (mesh) => {
    const skull = pathLoft([
      { p: V(0, 0.03, -0.10), rx: 0.19, rz: 0.15 },
      { p: V(0, 0.05, 0.16), rx: 0.22, rz: 0.16 },
      { p: V(0, 0.02, 0.44), rx: 0.13, rz: 0.095 },
      { p: V(0, 0.00, 0.60), rx: 0.072, rz: 0.058 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.12, 0.22, 0.74, 1.0), M.coat);
    for (let i = -4; i <= 4; i++) {
      const ang = i * 0.28;
      mesh(shadeVerts(new THREE.ConeGeometry(0.042, 0.30, 6), () => 0.96), i % 2 === 0 ? M.coatDark : M.trim,
        [Math.sin(ang) * 0.22, 0.15 + Math.cos(ang) * 0.16, -0.02], [-0.52, 0, -ang]);
    }
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.042, 10, 8), () => 1.0), M.trim, [sx * 0.148, 0.10, 0.25]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.018, 0.082, 5), () => 1.0), M.neutral, [sx * 0.068, -0.055, 0.50], [Math.PI, 0, 0]);
    }
  });

  // =========================================================================
  // 9. SAND WURM (high-poly dune colossus)
  // =========================================================================
  addPart(root, 'wurm_head', (mesh) => {
    const cowl = pathLoft([
      { p: V(0, 0, -0.38), rx: 0.38, rz: 0.38 },
      { p: V(0, 0, 0.05), rx: 0.48, rz: 0.48 },
      { p: V(0, 0, 0.38), rx: 0.44, rz: 0.44 },
    ], { segs: 20, fwd: V(0, 1, 0) });
    mesh(heightShade(cowl, -0.48, 0.48, 0.68, 1.0), M.coat);
    mesh(shadeVerts(new THREE.CylinderGeometry(0.35, 0.28, 0.16, 16), () => 0.10), M.dark, [0, 0, 0.34], [Math.PI / 2, 0, 0]);
    for (let i = 0; i < 10; i++) {
      const a = (i / 10) * Math.PI * 2;
      mesh(shadeVerts(new THREE.BoxGeometry(0.18, 0.11, 0.30), () => 0.88), M.coatDark, [Math.cos(a) * 0.45, Math.sin(a) * 0.45, 0.25], [0, 0, a + Math.PI / 2]);
    }
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * Math.PI * 2;
      mesh(shadeVerts(new THREE.ConeGeometry(0.036, 0.25, 5), () => 1.0), M.neutral, [Math.cos(a) * 0.30, Math.sin(a) * 0.30, 0.42], [Math.PI / 2 - Math.sin(a) * 0.45, Math.cos(a) * 0.45, a]);
    }
  });

  addPart(root, 'wurm_seg', (mesh) => {
    const seg = lathe([[0.34, -0.06], [0.44, 0.20], [0.49, 0.50], [0.44, 0.80], [0.34, 1.06]], 20);
    mesh(heightShade(seg, -0.06, 1.06, 0.70, 1.0), M.coat);
    mesh(shadeVerts(new THREE.TorusGeometry(0.48, 0.062, 8, 20), () => 0.86), M.coatDark, [0, 0.50, 0], [Math.PI / 2, 0, 0]);
    for (const sx of [-1, 0, 1]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.062, 0.21, 5), () => 0.94), M.coatDark, [sx * 0.24, 0.50, -0.47], [-1.4, 0, 0]);
    }
  });

  // =========================================================================
  // 10. WEREWOLF (high-poly sculpted hunched bipedal lycanthrope)
  // =========================================================================
  addPart(root, 'werewolf_body', (mesh) => {
    const torso = pathLoft([
      { p: V(0, -0.04, -0.04), rx: 0.20, rz: 0.16 },
      { p: V(0, 0.22, -0.02), rx: 0.17, rz: 0.15 }, // shredded feral waist
      { p: V(0, 0.50, 0.08), rx: 0.31, rz: 0.26 },  // broad muscular ribcage
      { p: V(0, 0.74, 0.18), rx: 0.36, rz: 0.28 },  // massive hunched shoulders
      { p: V(0, 0.90, 0.26), rx: 0.18, rz: 0.17 },
    ], { segs: 16 });
    mesh(heightShade(torso, -0.04, 0.94, 0.68, 1.0), M.coat);
    // Pectoral & shoulder deltoid bulges
    for (const sx of [-1, 1]) {
      const sh = shadeVerts(new THREE.SphereGeometry(0.165, 10, 8), () => 0.90);
      mesh(sh, M.coatDark, [sx * 0.29, 0.72, 0.16]);
    }
    for (const [y, z, h] of [[0.46, -0.18, 0.18], [0.64, -0.10, 0.26], [0.80, 0.02, 0.26], [0.90, 0.14, 0.18]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.088, h, 7), () => 0.82), M.coatDark, [0, y, z], [-1.1, 0, 0]);
    }
  });

  addPart(root, 'werewolf_arm', (mesh) => {
    const arm = limbSeg(0.115, 0.092, 0.075, 12, 0.44, 0.04);
    mesh(heightShade(arm, -0.05, 1.02, 0.70, 1.0), M.coat);
    mesh(shadeVerts(new THREE.SphereGeometry(0.088, 10, 8), () => 0.80), M.coatDark, [0, 0.98, 0.03]);
    for (const cx of [-0.052, -0.017, 0.017, 0.052]) {
      mesh(pathLoft([
        { p: V(cx, 1.00, 0.04), rx: 0.018, rz: 0.018 },
        { p: V(cx * 1.2, 1.12, 0.10), rx: 0.012, rz: 0.012 },
        { p: V(cx * 1.3, 1.22, 0.16), rx: 0.004, rz: 0.004 },
      ], { segs: 5 }), M.neutral);
    }
  });

  // =========================================================================
  // 11. BRUTE RIG: TROLL / OGRE / HILL GIANT / GIANT CHIEF (NO MORE BLUE TUNICS!)
  // =========================================================================
  addPart(root, 'brute_body', (mesh) => {
    // Hulking hunched brute torso: massive gut, broad sloping shoulders, fur/hide loincloth & shoulder pelt
    const torso = pathLoft([
      { p: V(0, -0.04, 0.00), rx: 0.30, rz: 0.26 }, // heavy hips
      { p: V(0, 0.24, 0.06), rx: 0.38, rz: 0.34 },  // heavy gut
      { p: V(0, 0.56, 0.08), rx: 0.42, rz: 0.34 },  // massive chest
      { p: V(0, 0.82, 0.14), rx: 0.44, rz: 0.32 },  // hunched brute shoulders
      { p: V(0, 0.98, 0.20), rx: 0.22, rz: 0.20 },  // thick bull neck
    ], { segs: 18 });
    mesh(heightShade(torso, -0.04, 1.02, 0.68, 1.0), M.coat);

    // Ragged hide/pelt loincloth & waist belt
    const loin = pathLoft([
      { p: V(0, -0.22, 0.02), rx: 0.34, rz: 0.30 },
      { p: V(0, 0.08, 0.03), rx: 0.33, rz: 0.29 },
    ], { segs: 14 });
    mesh(heightShade(loin, -0.22, 0.08, 0.72, 0.95), M.coatDark);
    const belt = shadeVerts(new THREE.TorusGeometry(0.34, 0.048, 8, 16), () => 0.78);
    mesh(belt, M.dark, [0, 0.06, 0.03], [Math.PI / 2, 0, 0]);

    // Asymmetric shaggy pelt/moss mantle over one shoulder
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.20, 12, 8), () => 0.88), M.coat, [sx * 0.38, 0.78, 0.12]);
    }
    const pelt = pathLoft([
      { p: V(-0.36, 0.92, 0.10), rx: 0.20, rz: 0.16 },
      { p: V(-0.08, 0.88, 0.02), rx: 0.24, rz: 0.18 },
      { p: V(0.22, 0.70, 0.06), rx: 0.20, rz: 0.15 },
    ], { segs: 10 });
    mesh(pelt, M.coatDark);
  });

  addPart(root, 'brute_head', (mesh) => {
    // Fierce troll/ogre/giant head: overhanging brow, protruding lower jaw, crooked tusks, long ears
    const skull = pathLoft([
      { p: V(0, -0.02, -0.04), rx: 0.20, rz: 0.20 },
      { p: V(0, 0.14, 0.02), rx: 0.22, rz: 0.22 },
      { p: V(0, 0.28, 0.00), rx: 0.18, rz: 0.18 },
      { p: V(0, 0.36, -0.02), rx: 0.10, rz: 0.10 },
    ], { segs: 14 });
    mesh(heightShade(skull, -0.02, 0.38, 0.74, 1.0), M.coat);
    // Heavy overhanging brow ridge + bulbous nose
    mesh(shadeVerts(new THREE.BoxGeometry(0.34, 0.08, 0.14), () => 0.76), M.coat, [0, 0.22, 0.16]);
    mesh(shadeVerts(new THREE.ConeGeometry(0.065, 0.16, 7), () => 0.86), M.coat, [0, 0.12, 0.22], [1.2, 0, 0]);
    // Shaggy beard/mane hair cap
    const hair = pathLoft([
      { p: V(0, 0.22, -0.08), rx: 0.22, rz: 0.20 },
      { p: V(0, 0.36, -0.04), rx: 0.16, rz: 0.16 },
    ], { segs: 12 });
    mesh(hair, M.coatDark);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.028, 8, 6), () => 1.0), M.trim, [sx * 0.095, 0.18, 0.20]);
      // Protruding crooked tusks!
      mesh(pathLoft([
        { p: V(sx * 0.08, 0.01, 0.18), rx: 0.026, rz: 0.026 },
        { p: V(sx * 0.10, 0.08, 0.23), rx: 0.016, rz: 0.016 },
        { p: V(sx * 0.09, 0.14, 0.24), rx: 0.004, rz: 0.004 },
      ], { segs: 6 }), M.neutral);
      // Pointed brute ears
      mesh(shadeVerts(new THREE.ConeGeometry(0.048, 0.16, 6), () => 0.84), M.coat, [sx * 0.22, 0.16, 0.0], [0, 0, -sx * 1.1]);
    }
  });

  addPart(root, 'brute_arm', (mesh) => {
    const g = limbSeg(0.16, 0.135, 0.115, 14, 0.45, 0.04);
    mesh(heightShade(g, -0.05, 1.02, 0.70, 1.0), M.coat);
    mesh(shadeVerts(new THREE.SphereGeometry(0.125, 10, 8), () => 0.84), M.coat, [0, 0.98, 0.02]);
  });

  addPart(root, 'brute_leg', (mesh) => {
    const g = limbSeg(0.185, 0.155, 0.125, 14, 0.45, 0.03);
    mesh(heightShade(g, -0.05, 1.02, 0.68, 1.0), M.coat);
    const foot = pathLoft([
      { p: V(0, 0.94, -0.08), rx: 0.135, rz: 0.075 },
      { p: V(0, 0.98, 0.18), rx: 0.125, rz: 0.058 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(foot, M.coatDark);
  });

  addPart(root, 'brute_club', (mesh) => {
    // Massive gnarled tree-trunk club with iron bands and bone spikes (0..1.45 along Y)
    const trunk = pathLoft([
      { p: V(0, -0.15, 0), rx: 0.065, rz: 0.065 },
      { p: V(0, 0.40, 0.02), rx: 0.095, rz: 0.095 },
      { p: V(0, 0.95, -0.03), rx: 0.165, rz: 0.155 },
      { p: V(0, 1.38, 0.02), rx: 0.195, rz: 0.185 },
      { p: V(0, 1.48, 0.02), rx: 0.095, rz: 0.095 },
    ], { segs: 12 });
    mesh(heightShade(trunk, -0.15, 1.48, 0.68, 1.0), M.coatDark);
    for (const y of [0.85, 1.20]) {
      mesh(shadeVerts(new THREE.TorusGeometry(0.175, 0.026, 6, 12), () => 0.72), M.dark, [0, y, 0], [Math.PI / 2, 0, 0]);
    }
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2;
      mesh(shadeVerts(new THREE.ConeGeometry(0.032, 0.15, 5), () => 0.95), M.neutral,
        [Math.cos(a) * 0.18, 1.05 + (i % 2) * 0.22, Math.sin(a) * 0.18], [Math.sin(a) * 1.4, 0, -Math.cos(a) * 1.4]);
    }
  });
}
