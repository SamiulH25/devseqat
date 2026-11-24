import { THREE, V, M, shadeVerts, heightShade, loft, pathLoft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildBeasts(root) {
  // Beast material convention (UnitMatTable.FillBeast in Mesh3D.cs):
  //   M.coat (11) = body, M.coatDark (12) = accent/mane/scales, M.dark (13) = dark (eyes/claws/nose/beak),
  //   M.trim (3) = bright accent/eyes, M.neutral (0) = ivory/fangs/tusks/teeth/bones

  // =========================================================================
  // 1. WOLF / HOUND / DIRE WOLF / ALPHA WOLF / WARG (remade, sculpted)
  // =========================================================================
  addPart(root, 'wolf_body', (mesh) => {
    // Muscular canine torso with deep chest, tucked waist, and curved spine
    const body = pathLoft([
      { p: V(0, 0.02, -0.56), rx: 0.08, rz: 0.08 },
      { p: V(0, 0.06, -0.38), rx: 0.17, rz: 0.19 }, // haunches
      { p: V(0, 0.04, -0.10), rx: 0.13, rz: 0.15 }, // tucked waist
      { p: V(0, 0.08, 0.22), rx: 0.20, rz: 0.25 },  // deep ribcage
      { p: V(0, 0.13, 0.44), rx: 0.17, rz: 0.20 },  // shoulders
      { p: V(0, 0.16, 0.56), rx: 0.11, rz: 0.12 },  // neck base
    ], {
      segs: 12, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) > 0 ? 1.06 : 0.92], // sharper spine ridge, tucked belly
    });
    mesh(heightShade(body, -0.20, 0.36, 0.74, 1.0), M.coat);

    // Layered jagged shoulder ruff / hackles
    const ruff = pathLoft([
      { p: V(0, 0.16, 0.08), rx: 0.16, rz: 0.11 },
      { p: V(0, 0.21, 0.28), rx: 0.22, rz: 0.15 },
      { p: V(0, 0.23, 0.46), rx: 0.19, rz: 0.13 },
      { p: V(0, 0.19, 0.58), rx: 0.12, rz: 0.09 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(ruff, 0.05, 0.36, 0.78, 1.0), M.coatDark);
    for (const [z, h] of [[0.14, 0.12], [0.26, 0.15], [0.38, 0.14], [0.48, 0.11]]) {
      const tuft = shadeVerts(new THREE.ConeGeometry(0.055, h, 5), () => 0.88);
      mesh(tuft, M.coatDark, [0, 0.31, z], [-0.55, 0, 0]);
    }
  });

  addPart(root, 'wolf_head', (mesh) => {
    // Fierce snarling canine skull + distinct lower jaw
    const cranium = pathLoft([
      { p: V(0, 0.04, -0.10), rx: 0.13, rz: 0.13 },
      { p: V(0, 0.06, 0.08), rx: 0.145, rz: 0.14 },
      { p: V(0, 0.04, 0.24), rx: 0.10, rz: 0.09 },
      { p: V(0, 0.02, 0.44), rx: 0.068, rz: 0.058 },
      { p: V(0, 0.01, 0.54), rx: 0.042, rz: 0.036 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(cranium, -0.06, 0.20, 0.80, 1.0), M.coat);

    // Snarling open lower jaw
    const jaw = pathLoft([
      { p: V(0, -0.05, 0.08), rx: 0.09, rz: 0.05 },
      { p: V(0, -0.08, 0.30), rx: 0.062, rz: 0.038 },
      { p: V(0, -0.09, 0.48), rx: 0.042, rz: 0.026 },
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(jaw, M.coatDark);

    // Cheek ruffs, cupped pointed ears, brow ridges, glowing eyes, upper & lower fangs
    for (const sx of [-1, 1]) {
      const cheek = shadeVerts(new THREE.ConeGeometry(0.065, 0.16, 5), () => 0.86);
      mesh(cheek, M.coatDark, [sx * 0.13, 0.0, 0.06], [-0.4, 0, sx * 1.15]);
      const ear = shadeVerts(new THREE.ConeGeometry(0.048, 0.17, 6), () => 0.90);
      mesh(ear, M.coatDark, [sx * 0.09, 0.21, 0.02], [0.18, 0, -sx * 0.22]);
      const brow = shadeVerts(new THREE.BoxGeometry(0.055, 0.028, 0.08), () => 0.82);
      mesh(brow, M.coatDark, [sx * 0.088, 0.115, 0.20], [0.2, sx * 0.2, -sx * 0.2]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.024, 6, 5), () => 1.0), M.trim, [sx * 0.092, 0.082, 0.22]);
      // Upper & lower fangs
      mesh(shadeVerts(new THREE.ConeGeometry(0.014, 0.058, 4), () => 1.0), M.neutral, [sx * 0.044, -0.02, 0.44], [Math.PI, 0, 0]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.012, 0.044, 4), () => 1.0), M.neutral, [sx * 0.036, -0.065, 0.41], [0, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.036, 6, 5), () => 0.15), M.dark, [0, 0.02, 0.54]);
  });

  addPart(root, 'wolf_leg', (mesh) => {
    // Sculpted canine limb with muscular upper thigh, angled hock, and clawed paw
    const leg = pathLoft([
      { p: V(0, -0.06, 0.02), rx: 0.082, rz: 0.098 },
      { p: V(0, 0.30, 0.05), rx: 0.060, rz: 0.072 },
      { p: V(0, 0.58, -0.04), rx: 0.046, rz: 0.054 }, // hock
      { p: V(0, 0.90, 0.01), rx: 0.040, rz: 0.044 },
    ], { segs: 8 });
    mesh(heightShade(leg, -0.06, 0.92, 0.74, 1.0), M.coat);
    const paw = loft(V(0, 0.93, -0.04), V(0, 0.96, 0.13), [[0, 0.048, 0.038], [1, 0.034, 0.026]], { segs: 7, fwd: V(0, 1, 0) });
    mesh(paw, M.dark);
    for (const cx of [-0.024, 0, 0.024]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.008, 0.038, 4), () => 0.95), M.neutral, [cx, 0.97, 0.135], [Math.PI / 2, 0, 0]);
    }
  });

  addPart(root, 'wolf_tail', (mesh) => {
    const tail = pathLoft([
      { p: V(0, -0.04, 0), rx: 0.045, rz: 0.045 },
      { p: V(0, 0.32, 0.05), rx: 0.095, rz: 0.10 },
      { p: V(0, 0.72, 0.02), rx: 0.082, rz: 0.086 },
      { p: V(0, 1.05, -0.06), rx: 0.022, rz: 0.022 },
    ], { segs: 8 });
    mesh(heightShade(tail, -0.04, 1.05, 0.74, 1.0), M.coatDark);
  });

  // =========================================================================
  // 2. BEAR / CAVE BEAR / BEAR-KING (remade, sculpted)
  // =========================================================================
  addPart(root, 'bear_body', (mesh) => {
    // Massive hunched ursine torso with towering shoulder hump and heavy haunches
    const body = pathLoft([
      { p: V(0, 0.02, -0.66), rx: 0.15, rz: 0.16 },
      { p: V(0, 0.10, -0.44), rx: 0.35, rz: 0.34 },
      { p: V(0, 0.12, -0.10), rx: 0.36, rz: 0.36 },
      { p: V(0, 0.22, 0.24), rx: 0.39, rz: 0.44 }, // huge shoulder hump
      { p: V(0, 0.18, 0.48), rx: 0.31, rz: 0.34 },
      { p: V(0, 0.12, 0.62), rx: 0.16, rz: 0.18 },
    ], {
      segs: 12, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) > 0 ? 1.08 : 0.94],
    });
    mesh(heightShade(body, -0.32, 0.66, 0.72, 1.0), M.coat);

    // Shaggy dark shoulder cape & fur tufts along the spine
    const cape = pathLoft([
      { p: V(0, 0.34, -0.26), rx: 0.22, rz: 0.12 },
      { p: V(0, 0.44, 0.18), rx: 0.30, rz: 0.18 },
      { p: V(0, 0.36, 0.46), rx: 0.22, rz: 0.13 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(cape, 0.20, 0.60, 0.76, 1.0), M.coatDark);
    for (const [z, y, h] of [[-0.12, 0.46, 0.14], [0.08, 0.56, 0.18], [0.26, 0.58, 0.18], [0.40, 0.48, 0.14]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.08, h, 5), () => 0.85), M.coatDark, [0, y, z], [-0.45, 0, 0]);
    }
    // Stub tail
    mesh(shadeVerts(new THREE.SphereGeometry(0.085, 6, 5), () => 0.8), M.coatDark, [0, 0.08, -0.68]);
  });

  addPart(root, 'bear_head', (mesh) => {
    const cranium = pathLoft([
      { p: V(0, 0.04, -0.10), rx: 0.22, rz: 0.21 },
      { p: V(0, 0.06, 0.14), rx: 0.24, rz: 0.22 },
      { p: V(0, 0.02, 0.34), rx: 0.15, rz: 0.13 },
      { p: V(0, 0.00, 0.54), rx: 0.105, rz: 0.085 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(heightShade(cranium, -0.18, 0.28, 0.76, 1.0), M.coat);

    // Heavy roaring lower jaw
    const jaw = pathLoft([
      { p: V(0, -0.09, 0.10), rx: 0.14, rz: 0.08 },
      { p: V(0, -0.13, 0.34), rx: 0.095, rz: 0.058 },
      { p: V(0, -0.14, 0.50), rx: 0.072, rz: 0.042 },
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(jaw, M.coatDark);

    for (const sx of [-1, 1]) {
      // Cupped round bear ears
      const ear = shadeVerts(new THREE.SphereGeometry(0.078, 7, 6), () => 0.86);
      ear.scale(1.0, 1.0, 0.55);
      mesh(ear, M.coatDark, [sx * 0.17, 0.23, 0.06]);
      // Heavy brow + fierce eyes + 4 canines
      mesh(shadeVerts(new THREE.BoxGeometry(0.08, 0.036, 0.09), () => 0.8), M.coatDark, [sx * 0.12, 0.14, 0.24], [0.18, sx * 0.2, -sx * 0.18]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.030, 6, 5), () => 0.95), M.trim, [sx * 0.135, 0.10, 0.27]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.020, 0.072, 5), () => 1.0), M.neutral, [sx * 0.068, -0.06, 0.45], [Math.PI, 0, 0]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.017, 0.058, 5), () => 1.0), M.neutral, [sx * 0.058, -0.11, 0.42], [0, 0, 0]);
    }
    mesh(shadeVerts(new THREE.SphereGeometry(0.058, 7, 6), () => 0.14), M.dark, [0, 0.01, 0.55]);
  });

  addPart(root, 'bear_leg', (mesh) => {
    const leg = pathLoft([
      { p: V(0, -0.08, 0.02), rx: 0.165, rz: 0.175 },
      { p: V(0, 0.34, 0.05), rx: 0.135, rz: 0.145 },
      { p: V(0, 0.66, -0.02), rx: 0.112, rz: 0.122 },
      { p: V(0, 0.90, 0.02), rx: 0.105, rz: 0.110 },
    ], { segs: 9 });
    mesh(heightShade(leg, -0.08, 0.92, 0.74, 1.0), M.coat);
    const paw = loft(V(0, 0.92, -0.08), V(0, 0.95, 0.22), [[0, 0.125, 0.082], [1, 0.095, 0.058]], { segs: 8, fwd: V(0, 1, 0) });
    mesh(paw, M.coatDark);
    for (const cx of [-0.066, -0.022, 0.022, 0.066]) {
      const claw = loft(V(cx, 0.94, 0.19), V(cx * 1.15, 0.99, 0.29), [[0, 0.018, 0.018], [1, 0.005, 0.005]], { segs: 4 });
      mesh(claw, M.neutral);
    }
  });

  // =========================================================================
  // 3. GIANT SPIDER / BROOD MOTHER (remade, sculpted chitin)
  // =========================================================================
  addPart(root, 'spider_abdomen', (mesh) => {
    const abd = pathLoft([
      { p: V(0, 0.02, -0.48), rx: 0.07, rz: 0.06 },
      { p: V(0, 0.08, -0.26), rx: 0.30, rz: 0.27 },
      { p: V(0, 0.10, 0.02), rx: 0.36, rz: 0.32 },
      { p: V(0, 0.06, 0.28), rx: 0.26, rz: 0.24 },
      { p: V(0, 0.02, 0.44), rx: 0.09, rz: 0.09 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(heightShade(abd, -0.26, 0.42, 0.72, 1.0), M.coat);
    // Dorsal carapace ridge chevrons & rear spinnerets
    for (const [z, r] of [[-0.22, 0.27], [-0.04, 0.34], [0.14, 0.30]]) {
      const plate = shadeVerts(new THREE.TorusGeometry(r, 0.032, 6, 12, Math.PI * 1.2), () => 0.96);
      mesh(plate, M.coatDark, [0, 0.08, z], [0, 0, -Math.PI * 0.1]);
    }
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.032, 0.12, 5), () => 0.8), M.dark, [sx * 0.055, 0.02, -0.50], [-Math.PI / 2, 0, sx * 0.2]);
    }
  });

  addPart(root, 'spider_head', (mesh) => {
    const ceph = pathLoft([
      { p: V(0, 0.02, -0.18), rx: 0.18, rz: 0.14 },
      { p: V(0, 0.04, 0.04), rx: 0.23, rz: 0.18 },
      { p: V(0, 0.01, 0.24), rx: 0.17, rz: 0.14 },
      { p: V(0, -0.02, 0.32), rx: 0.10, rz: 0.09 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(ceph, -0.16, 0.22, 0.78, 1.0), M.coat);
    // 8 gleaming crimson eyes
    for (const [ex, ey, ez, er] of [
      [-0.058, 0.08, 0.26, 0.034], [0.058, 0.08, 0.26, 0.034],
      [-0.125, 0.06, 0.22, 0.024], [0.125, 0.06, 0.22, 0.024],
      [-0.032, 0.125, 0.23, 0.020], [0.032, 0.125, 0.23, 0.020],
      [-0.165, 0.03, 0.17, 0.018], [0.165, 0.03, 0.17, 0.018],
    ]) {
      mesh(shadeVerts(new THREE.SphereGeometry(er, 6, 5), () => 1.0), M.coatDark, [ex, ey, ez]);
    }
    // Curved serrated fangs + pedipalps
    for (const sx of [-1, 1]) {
      const fang = pathLoft([
        { p: V(sx * 0.08, -0.02, 0.24), rx: 0.048, rz: 0.048 },
        { p: V(sx * 0.09, -0.09, 0.36), rx: 0.032, rz: 0.032 },
        { p: V(sx * 0.03, -0.19, 0.38), rx: 0.008, rz: 0.008 },
      ], { segs: 6 });
      mesh(fang, M.dark);
      const palp = pathLoft([
        { p: V(sx * 0.16, -0.02, 0.20), rx: 0.028, rz: 0.028 },
        { p: V(sx * 0.24, 0.02, 0.34), rx: 0.022, rz: 0.022 },
        { p: V(sx * 0.18, -0.08, 0.42), rx: 0.014, rz: 0.014 },
      ], { segs: 5 });
      mesh(palp, M.coat);
    }
  });

  addPart(root, 'spider_leg', (mesh) => {
    const seg = pathLoft([
      { p: V(0, -0.04, 0), rx: 0.056, rz: 0.056 },
      { p: V(0, 0.15, 0), rx: 0.064, rz: 0.064 }, // armored joint bulb
      { p: V(0, 0.60, 0), rx: 0.042, rz: 0.042 },
      { p: V(0, 1.04, 0), rx: 0.016, rz: 0.016 }, // sharp tip
    ], { segs: 7 });
    mesh(heightShade(seg, -0.04, 1.04, 0.72, 1.0), M.dark);
    mesh(shadeVerts(new THREE.ConeGeometry(0.022, 0.09, 4), () => 0.9), M.coatDark, [0, 0.45, 0.045], [0.9, 0, 0]);
  });

  // =========================================================================
  // 4. WYVERN (remade — fierce draconic silhouette & huge arched wings)
  // =========================================================================
  addPart(root, 'wyvern_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, 0.02, -0.62), rx: 0.12, rz: 0.13 },
      { p: V(0, 0.06, -0.32), rx: 0.24, rz: 0.25 },
      { p: V(0, 0.10, 0.08), rx: 0.30, rz: 0.34 }, // keeled dragon chest
      { p: V(0, 0.12, 0.40), rx: 0.25, rz: 0.27 },
      { p: V(0, 0.10, 0.60), rx: 0.14, rz: 0.15 },
    ], {
      segs: 12, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) < -0.2 ? 1.12 : 1.0], // keeled breast
    });
    mesh(heightShade(body, -0.28, 0.44, 0.74, 1.0), M.coat);
    // Belly scutes + serrated dorsal fin spines
    for (const [z, h] of [[-0.42, 0.14], [-0.20, 0.18], [0.02, 0.22], [0.24, 0.20], [0.42, 0.15]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.042, h, 4), () => 0.95), M.coatDark, [0, 0.38, z], [-0.45, 0, 0]);
    }
  });

  addPart(root, 'wyvern_neck', (mesh) => {
    const neck = pathLoft([
      { p: V(0, -0.08, 0), rx: 0.165, rz: 0.175 },
      { p: V(0, 0.35, -0.04), rx: 0.140, rz: 0.145 },
      { p: V(0, 0.75, 0.03), rx: 0.120, rz: 0.125 },
      { p: V(0, 1.08, 0), rx: 0.105, rz: 0.110 },
    ], { segs: 10 });
    mesh(heightShade(neck, -0.08, 1.08, 0.76, 1.0), M.coat);
    for (const y of [0.20, 0.50, 0.80]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.032, 0.12, 4), () => 0.92), M.coatDark, [0, y, -0.14], [-1.3, 0, 0]);
    }
  });

  addPart(root, 'wyvern_head', (mesh) => {
    const cranium = pathLoft([
      { p: V(0, 0.06, -0.12), rx: 0.165, rz: 0.145 },
      { p: V(0, 0.08, 0.12), rx: 0.175, rz: 0.150 },
      { p: V(0, 0.04, 0.34), rx: 0.110, rz: 0.095 },
      { p: V(0, 0.02, 0.56), rx: 0.068, rz: 0.055 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(cranium, -0.08, 0.24, 0.78, 1.0), M.coat);
    // Open lower jaw
    const jaw = pathLoft([
      { p: V(0, -0.06, 0.08), rx: 0.11, rz: 0.055 },
      { p: V(0, -0.10, 0.34), rx: 0.075, rz: 0.042 },
      { p: V(0, -0.11, 0.52), rx: 0.048, rz: 0.028 },
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(jaw, M.coatDark);
    // Snout horn
    mesh(shadeVerts(new THREE.ConeGeometry(0.026, 0.11, 5), () => 0.95), M.dark, [0, 0.09, 0.48], [0.35, 0, 0]);
    for (const sx of [-1, 1]) {
      // Curved double drake horns
      const horn = pathLoft([
        { p: V(sx * 0.10, 0.14, 0.02), rx: 0.044, rz: 0.044 },
        { p: V(sx * 0.18, 0.26, -0.14), rx: 0.028, rz: 0.028 },
        { p: V(sx * 0.24, 0.34, -0.34), rx: 0.008, rz: 0.008 },
      ], { segs: 6 });
      mesh(horn, M.dark);
      const cheekHorn = loft(V(sx * 0.14, 0.02, 0.0), V(sx * 0.22, 0.0, -0.18), [[0, 0.028, 0.028], [1, 0.006, 0.006]], { segs: 5 });
      mesh(cheekHorn, M.dark);
      mesh(shadeVerts(new THREE.SphereGeometry(0.028, 6, 5), () => 1.0), M.trim, [sx * 0.125, 0.10, 0.22]);
      for (const zf of [0.32, 0.44]) {
        mesh(shadeVerts(new THREE.ConeGeometry(0.015, 0.062, 4), () => 1.0), M.neutral, [sx * 0.056, -0.02, zf], [Math.PI, 0, 0]);
      }
    }
  });

  addPart(root, 'wyvern_wing', (mesh) => {
    // Dramatic arched bat-dragon wing with muscular forearm, clawed thumb, and 4 sweeping fingers
    const arm = pathLoft([
      { p: V(0.0, 0.0, 0.0), rx: 0.058, rz: 0.058 },
      { p: V(0.34, 0.10, 0.12), rx: 0.048, rz: 0.048 }, // elbow/wrist arch
      { p: V(0.72, 0.16, 0.18), rx: 0.036, rz: 0.036 },
      { p: V(1.08, 0.06, 0.08), rx: 0.016, rz: 0.016 },
    ], { segs: 6 });
    mesh(arm, M.coat);
    // Wing-thumb claw
    mesh(loft(V(0.36, 0.12, 0.14), V(0.40, 0.24, 0.26), [[0, 0.022, 0.022], [1, 0.005, 0.005]], { segs: 4 }), M.dark);
    const fingers = [
      [V(0.36, 0.10, 0.12), V(0.24, -0.04, -0.68)],
      [V(0.56, 0.13, 0.15), V(0.50, -0.04, -0.74)],
      [V(0.78, 0.15, 0.17), V(0.80, -0.03, -0.66)],
      [V(1.06, 0.06, 0.08), V(1.12, -0.04, -0.46)],
    ];
    for (const [a, b] of fingers) {
      mesh(loft(a, b, [[0, 0.020, 0.020], [1, 0.008, 0.008]], { segs: 4 }), M.coatDark);
    }
    const pts = [
      [0.0, 0.0, 0.0], [0.36, 0.10, 0.12], [0.56, 0.13, 0.15], [0.78, 0.15, 0.17], [1.06, 0.06, 0.08],
      [0.0, -0.04, -0.54], [0.24, -0.04, -0.68], [0.50, -0.04, -0.74], [0.80, -0.03, -0.66], [1.12, -0.04, -0.46],
    ];
    const idx = [
      0, 1, 6, 0, 6, 5,
      1, 2, 7, 1, 7, 6,
      2, 3, 8, 2, 8, 7,
      3, 4, 9, 3, 9, 8,
    ];
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pts.flat(), 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    mesh(shadeVerts(g, (x) => 0.78 + 0.18 * Math.min(1, x)), M.coatDark);
  });

  addPart(root, 'wyvern_leg', (mesh) => {
    // Digitigrade avian-drake leg with muscular thigh, reverse-bent ankle, and 4 talons
    const leg = pathLoft([
      { p: V(0, -0.06, 0.04), rx: 0.115, rz: 0.125 },
      { p: V(0, 0.34, 0.12), rx: 0.075, rz: 0.082 },
      { p: V(0, 0.64, -0.08), rx: 0.056, rz: 0.062 }, // reverse ankle
      { p: V(0, 0.92, 0.02), rx: 0.050, rz: 0.054 },
    ], { segs: 8 });
    mesh(heightShade(leg, -0.06, 0.92, 0.74, 1.0), M.coat);
    for (const cx of [-0.052, 0, 0.052]) {
      mesh(loft(V(cx, 0.93, 0.02), V(cx * 1.4, 1.02, 0.21), [[0, 0.024, 0.024], [1, 0.006, 0.006]], { segs: 5 }), M.dark);
    }
    // Rear spur talon
    mesh(loft(V(0, 0.93, -0.02), V(0, 1.00, -0.14), [[0, 0.022, 0.022], [1, 0.005, 0.005]], { segs: 4 }), M.dark);
  });

  addPart(root, 'wyvern_tail', (mesh) => {
    const seg = loft(V(0, -0.06, 0), V(0, 1.06, 0), [
      [0.0, 0.105, 0.105], [0.5, 0.082, 0.082], [1.0, 0.060, 0.060],
    ], { segs: 8 });
    mesh(heightShade(seg, -0.06, 1.06, 0.76, 1.0), M.coat);
    mesh(shadeVerts(new THREE.ConeGeometry(0.028, 0.11, 4), () => 0.94), M.coatDark, [0, 0.5, -0.09], [-1.3, 0, 0]);
  });

  addPart(root, 'wyvern_sting', (mesh) => {
    const tip = lathe([
      [0.060, -0.05], [0.100, 0.35], [0.048, 0.72], [0.0, 1.08],
    ], 8);
    mesh(tip, M.dark);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.032, 0.22, 4), () => 0.96), M.coatDark, [sx * 0.075, 0.36, 0], [0, 0, sx * 2.55]);
    }
  });

  // =========================================================================
  // 5. BOAR / TUSKER / GREAT BOAR (NEW bespoke 3D model)
  // =========================================================================
  addPart(root, 'boar_body', (mesh) => {
    // Low, barrel-chested, wedge-shaped porcine body sloping down toward hindquarters
    const body = pathLoft([
      { p: V(0, 0.00, -0.54), rx: 0.12, rz: 0.13 },
      { p: V(0, 0.04, -0.34), rx: 0.25, rz: 0.25 },
      { p: V(0, 0.08, 0.00), rx: 0.29, rz: 0.31 },
      { p: V(0, 0.14, 0.32), rx: 0.31, rz: 0.36 }, // heavy sloped boar shoulders
      { p: V(0, 0.10, 0.52), rx: 0.22, rz: 0.24 },
    ], {
      segs: 12, fwd: V(0, 1, 0),
      profile: (th) => [1.0, Math.sin(th) > 0.3 ? 1.15 : 0.95], // ridged spine
    });
    mesh(heightShade(body, -0.26, 0.52, 0.72, 1.0), M.coat);
    // Bristling dorsal mohawk mane along the entire back
    for (const [z, y, h] of [[-0.24, 0.32, 0.14], [-0.04, 0.42, 0.20], [0.16, 0.48, 0.24], [0.34, 0.48, 0.22], [0.46, 0.38, 0.16]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.048, h, 5), () => 0.84), M.coatDark, [0, y, z], [-0.35, 0, 0]);
    }
    // Tufted boar tail
    const tail = pathLoft([
      { p: V(0, 0.04, -0.52), rx: 0.025, rz: 0.025 },
      { p: V(0, 0.02, -0.68), rx: 0.020, rz: 0.020 },
      { p: V(0, -0.06, -0.74), rx: 0.042, rz: 0.042 },
    ], { segs: 6 });
    mesh(tail, M.coatDark);
  });

  addPart(root, 'boar_head', (mesh) => {
    // Sloped wedge skull + upturned snout disc + HUGE curved ivory tusks!
    const skull = pathLoft([
      { p: V(0, 0.06, -0.08), rx: 0.21, rz: 0.22 },
      { p: V(0, 0.02, 0.16), rx: 0.18, rz: 0.17 },
      { p: V(0, -0.04, 0.38), rx: 0.11, rz: 0.10 },
      { p: V(0, -0.06, 0.54), rx: 0.082, rz: 0.078 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.16, 0.26, 0.76, 1.0), M.coat);
    // Flat upturned snout disc
    const snout = shadeVerts(new THREE.CylinderGeometry(0.085, 0.080, 0.045, 9), () => 0.85);
    mesh(snout, M.coatDark, [0, -0.055, 0.56], [Math.PI / 2 - 0.15, 0, 0]);
    for (const sx of [-1, 1]) {
      // Sweeping curved ivory tusks jutting up and out!
      const tusk = pathLoft([
        { p: V(sx * 0.07, -0.10, 0.36), rx: 0.036, rz: 0.036 },
        { p: V(sx * 0.18, -0.06, 0.48), rx: 0.028, rz: 0.028 },
        { p: V(sx * 0.25, 0.09, 0.54), rx: 0.018, rz: 0.018 },
        { p: V(sx * 0.22, 0.22, 0.50), rx: 0.005, rz: 0.005 },
      ], { segs: 6 });
      mesh(tusk, M.neutral);
      // Pointed bristled ears & red eyes
      mesh(shadeVerts(new THREE.ConeGeometry(0.052, 0.15, 5), () => 0.86), M.coatDark, [sx * 0.14, 0.22, 0.02], [0.25, 0, -sx * 0.45]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.024, 6, 5), () => 1.0), M.trim, [sx * 0.14, 0.06, 0.22]);
    }
  });

  addPart(root, 'boar_leg', (mesh) => {
    const leg = pathLoft([
      { p: V(0, -0.06, 0), rx: 0.105, rz: 0.115 },
      { p: V(0, 0.45, 0.02), rx: 0.072, rz: 0.078 },
      { p: V(0, 0.88, 0), rx: 0.058, rz: 0.062 },
    ], { segs: 8 });
    mesh(heightShade(leg, -0.06, 0.88, 0.75, 1.0), M.coat);
    const hoof = shadeVerts(new THREE.CylinderGeometry(0.058, 0.068, 0.11, 7), () => 0.22);
    mesh(hoof, M.dark, [0, 0.94, 0.02]);
  });

  // =========================================================================
  // 6. HARPY (NEW bespoke feathered avian predator)
  // =========================================================================
  addPart(root, 'harpy_body', (mesh) => {
    const body = pathLoft([
      { p: V(0, -0.32, -0.06), rx: 0.12, rz: 0.11 },
      { p: V(0, 0.0, 0.02), rx: 0.18, rz: 0.17 },
      { p: V(0, 0.26, 0.06), rx: 0.22, rz: 0.20 }, // feathered chest
      { p: V(0, 0.44, 0.04), rx: 0.12, rz: 0.12 },
    ], { segs: 10 });
    mesh(heightShade(body, -0.32, 0.44, 0.76, 1.0), M.coat);
    // Tail fan feathers
    for (const ang of [-0.35, -0.17, 0, 0.17, 0.35]) {
      const f = shadeVerts(new THREE.BoxGeometry(0.06, 0.018, 0.42), () => 0.88);
      mesh(f, M.coatDark, [Math.sin(ang) * 0.14, -0.30, -0.24], [0.25, ang, 0]);
    }
  });

  addPart(root, 'harpy_head', (mesh) => {
    const skull = shadeVerts(new THREE.SphereGeometry(0.145, 9, 8), () => 0.92);
    skull.scale(0.95, 1.05, 1.12);
    mesh(skull, M.coat, [0, 0.04, 0.0]);
    // Swept feather crest plume
    for (const [y, z, rx] of [[0.16, -0.04, -0.55], [0.12, -0.12, -0.85], [0.06, -0.16, -1.15]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.045, 0.22, 5), () => 0.88), M.coatDark, [0, y, z], [rx, 0, 0]);
    }
    // Fierce hooked raptor beak
    const beak = pathLoft([
      { p: V(0, 0.02, 0.12), rx: 0.055, rz: 0.058 },
      { p: V(0, 0.00, 0.26), rx: 0.032, rz: 0.038 },
      { p: V(0, -0.06, 0.31), rx: 0.006, rz: 0.008 },
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(beak, M.dark);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.026, 6, 5), () => 1.0), M.trim, [sx * 0.088, 0.075, 0.11]);
    }
  });

  addPart(root, 'harpy_wing', (mesh) => {
    // Layered feathered raptor wing extending +X (0..1.05)
    const leading = pathLoft([
      { p: V(0.0, 0.0, 0.0), rx: 0.055, rz: 0.065 },
      { p: V(0.42, 0.08, 0.08), rx: 0.045, rz: 0.055 },
      { p: V(0.88, 0.04, 0.04), rx: 0.028, rz: 0.035 },
    ], { segs: 6 });
    mesh(leading, M.coat);
    // 7 overlapping primary & secondary feather blades fanning back (-Z)
    for (let i = 0; i < 7; i++) {
      const t = i / 6;
      const x = 0.10 + t * 0.92;
      const len = 0.44 + Math.sin(t * Math.PI * 0.85) * 0.28;
      const feather = shadeVerts(new THREE.BoxGeometry(0.14, 0.022, len), () => 0.82 + 0.15 * t);
      mesh(feather, i % 2 === 0 ? M.coat : M.coatDark, [x, 0.02 + Math.sin(t * Math.PI) * 0.05, -len * 0.42 + 0.05], [0, -0.18 * t, 0]);
    }
  });

  // =========================================================================
  // 7. GRIFFON (NEW bespoke lion-body + eagle-head + massive feathered wings)
  // =========================================================================
  addPart(root, 'griffon_body', (mesh) => {
    // Noble muscular lion hindquarters + broad feathered eagle breast
    const body = pathLoft([
      { p: V(0, 0.02, -0.60), rx: 0.12, rz: 0.12 },
      { p: V(0, 0.08, -0.38), rx: 0.24, rz: 0.25 }, // lion haunch
      { p: V(0, 0.06, -0.06), rx: 0.18, rz: 0.19 }, // lean feline waist
      { p: V(0, 0.14, 0.28), rx: 0.29, rz: 0.34 },  // broad eagle chest
      { p: V(0, 0.22, 0.54), rx: 0.20, rz: 0.22 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.24, 0.48, 0.74, 1.0), M.coat);
    // Layered feathered neck ruff
    for (let i = 0; i < 6; i++) {
      const ang = (i / 6) * Math.PI * 2;
      const rf = shadeVerts(new THREE.ConeGeometry(0.08, 0.24, 5), () => 0.92);
      mesh(rf, M.coatDark, [Math.cos(ang) * 0.16, 0.22 + Math.sin(ang) * 0.14, 0.46], [-0.9, 0, -ang]);
    }
  });

  addPart(root, 'griffon_head', (mesh) => {
    // Regal hooked-beak eagle head with swept ear tufts
    const skull = pathLoft([
      { p: V(0, 0.04, -0.10), rx: 0.16, rz: 0.15 },
      { p: V(0, 0.06, 0.12), rx: 0.17, rz: 0.16 },
      { p: V(0, 0.03, 0.28), rx: 0.11, rz: 0.10 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.12, 0.22, 0.80, 1.0), M.coatDark);
    // Golden hooked eagle beak
    const beak = pathLoft([
      { p: V(0, 0.02, 0.24), rx: 0.085, rz: 0.095 },
      { p: V(0, 0.01, 0.44), rx: 0.055, rz: 0.068 },
      { p: V(0, -0.09, 0.52), rx: 0.010, rz: 0.012 },
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(beak, M.trim);
    for (const sx of [-1, 1]) {
      // Swept-back feathered ear-tuft crests
      mesh(loft(V(sx * 0.11, 0.16, 0.02), V(sx * 0.20, 0.30, -0.22), [[0, 0.048, 0.024], [1, 0.008, 0.008]], { segs: 5 }), M.coatDark);
      mesh(shadeVerts(new THREE.SphereGeometry(0.028, 6, 5), () => 1.0), M.trim, [sx * 0.125, 0.095, 0.19]);
    }
  });

  // =========================================================================
  // 8. BASILISK (NEW bespoke crowned venomous 6-legged reptile)
  // =========================================================================
  addPart(root, 'basilisk_body', (mesh) => {
    // Low, heavy, broad armored lizard torso with overlapping dorsal scale ridge
    const body = pathLoft([
      { p: V(0, 0.0, -0.64), rx: 0.14, rz: 0.11 },
      { p: V(0, 0.04, -0.34), rx: 0.30, rz: 0.21 },
      { p: V(0, 0.05, 0.04), rx: 0.34, rz: 0.23 },
      { p: V(0, 0.06, 0.40), rx: 0.28, rz: 0.20 },
      { p: V(0, 0.05, 0.62), rx: 0.17, rz: 0.14 },
    ], { segs: 12, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.18, 0.28, 0.74, 1.0), M.coat);
    // Broad armored dorsal scale plates + twin crest spikes
    for (const z of [-0.42, -0.20, 0.02, 0.24, 0.44]) {
      const plate = shadeVerts(new THREE.BoxGeometry(0.32, 0.06, 0.16), () => 0.90);
      mesh(plate, M.coatDark, [0, 0.24, z], [-0.15, 0, 0]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.036, 0.16, 4), () => 0.96), M.trim, [0, 0.32, z], [-0.4, 0, 0]);
    }
  });

  addPart(root, 'basilisk_head', (mesh) => {
    // Crowned venomous serpent-drake head with flared frill & petrifying glowing eyes
    const skull = pathLoft([
      { p: V(0, 0.03, -0.08), rx: 0.18, rz: 0.14 },
      { p: V(0, 0.04, 0.16), rx: 0.20, rz: 0.15 },
      { p: V(0, 0.01, 0.42), rx: 0.12, rz: 0.09 },
      { p: V(0, 0.00, 0.58), rx: 0.068, rz: 0.055 },
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(skull, -0.12, 0.20, 0.78, 1.0), M.coat);
    // Flared venomous neck frill / crown crest!
    for (let i = -3; i <= 3; i++) {
      const ang = i * 0.32;
      const spine = shadeVerts(new THREE.ConeGeometry(0.038, 0.26, 4), () => 0.95);
      mesh(spine, i % 2 === 0 ? M.coatDark : M.trim, [Math.sin(ang) * 0.20, 0.14 + Math.cos(ang) * 0.14, -0.02], [-0.5, 0, -ang]);
    }
    for (const sx of [-1, 1]) {
      // Glowing petrifying eyes
      mesh(shadeVerts(new THREE.SphereGeometry(0.038, 7, 6), () => 1.0), M.trim, [sx * 0.14, 0.095, 0.24]);
      mesh(shadeVerts(new THREE.ConeGeometry(0.016, 0.075, 4), () => 1.0), M.neutral, [sx * 0.065, -0.05, 0.48], [Math.PI, 0, 0]);
    }
  });

  // =========================================================================
  // 9. SAND WURM (NEW bespoke ringed desert colossus with toothed ring-maw)
  // =========================================================================
  addPart(root, 'wurm_head', (mesh) => {
    // Huge circular armored carapace head opening into a terrifying 3-ring toothed maw (+Z)
    const cowl = pathLoft([
      { p: V(0, 0, -0.35), rx: 0.36, rz: 0.36 },
      { p: V(0, 0, 0.05), rx: 0.44, rz: 0.44 },
      { p: V(0, 0, 0.36), rx: 0.40, rz: 0.40 },
    ], { segs: 14, fwd: V(0, 1, 0) });
    mesh(heightShade(cowl, -0.44, 0.44, 0.72, 1.0), M.coat);
    // Dark recessed gullet disc
    const gullet = shadeVerts(new THREE.CylinderGeometry(0.32, 0.26, 0.14, 12), () => 0.12);
    mesh(gullet, M.dark, [0, 0, 0.32], [Math.PI / 2, 0, 0]);
    // Outer armored carapace lip plates
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * Math.PI * 2;
      const plate = shadeVerts(new THREE.BoxGeometry(0.18, 0.10, 0.26), () => 0.90);
      mesh(plate, M.coatDark, [Math.cos(a) * 0.42, Math.sin(a) * 0.42, 0.24], [0, 0, a + Math.PI / 2]);
    }
    // 12 inward-curving razor ivory teeth ringing the maw!
    for (let i = 0; i < 12; i++) {
      const a = (i / 12) * Math.PI * 2;
      const tooth = shadeVerts(new THREE.ConeGeometry(0.036, 0.22, 4), () => 1.0);
      mesh(tooth, M.neutral, [Math.cos(a) * 0.28, Math.sin(a) * 0.28, 0.40], [Math.PI / 2 - Math.sin(a) * 0.45, Math.cos(a) * 0.45, a]);
    }
  });

  addPart(root, 'wurm_seg', (mesh) => {
    // Thick armored ringed carapace body segment (Y -0.05..1.05)
    const seg = lathe([
      [0.32, -0.06], [0.42, 0.20], [0.46, 0.50], [0.42, 0.80], [0.32, 1.06],
    ], 14);
    mesh(heightShade(seg, -0.06, 1.06, 0.74, 1.0), M.coat);
    // Armored ring band + dorsal fin ridges
    const band = shadeVerts(new THREE.TorusGeometry(0.45, 0.055, 6, 14), () => 0.88);
    mesh(band, M.coatDark, [0, 0.50, 0], [Math.PI / 2, 0, 0]);
    for (const sx of [-1, 0, 1]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.055, 0.18, 4), () => 0.94), M.coatDark, [sx * 0.22, 0.50, -0.44], [-1.4, 0, 0]);
    }
  });

  // =========================================================================
  // 10. WEREWOLF (NEW bespoke hunched bipedal lycanthrope)
  // =========================================================================
  addPart(root, 'werewolf_body', (mesh) => {
    // Hulking hunched bipedal torso: narrow feral waist flaring to massive hunched shoulders
    const torso = pathLoft([
      { p: V(0, 0.00, -0.04), rx: 0.18, rz: 0.15 }, // hips
      { p: V(0, 0.26, -0.02), rx: 0.16, rz: 0.14 }, // lean waist
      { p: V(0, 0.56, 0.06), rx: 0.30, rz: 0.24 },  // broad chest
      { p: V(0, 0.78, 0.14), rx: 0.32, rz: 0.24 },  // hunched shoulders
      { p: V(0, 0.92, 0.22), rx: 0.16, rz: 0.15 },  // neck
    ], { segs: 12 });
    mesh(heightShade(torso, 0.0, 0.94, 0.72, 1.0), M.coat);
    // Jagged dark fur mane along hunched upper back
    for (const [y, z, h] of [[0.52, -0.16, 0.16], [0.68, -0.10, 0.22], [0.82, 0.00, 0.22], [0.90, 0.10, 0.16]]) {
      mesh(shadeVerts(new THREE.ConeGeometry(0.075, h, 5), () => 0.84), M.coatDark, [0, y, z], [-1.1, 0, 0]);
    }
  });

  addPart(root, 'werewolf_arm', (mesh) => {
    // Long muscular clawed lycanthrope arm (0..1.0 along Y)
    const arm = pathLoft([
      { p: V(0, -0.05, 0), rx: 0.105, rz: 0.105 },
      { p: V(0, 0.42, 0.04), rx: 0.088, rz: 0.090 },
      { p: V(0, 0.86, 0.02), rx: 0.072, rz: 0.076 },
    ], { segs: 8 });
    mesh(heightShade(arm, -0.05, 0.90, 0.74, 1.0), M.coat);
    const hand = shadeVerts(new THREE.SphereGeometry(0.082, 7, 6), () => 0.82);
    mesh(hand, M.coatDark, [0, 0.92, 0.03]);
    for (const cx of [-0.048, -0.016, 0.016, 0.048]) {
      mesh(loft(V(cx, 0.94, 0.04), V(cx * 1.25, 1.12, 0.12), [[0, 0.016, 0.016], [1, 0.004, 0.004]], { segs: 4 }), M.neutral);
    }
  });
}
