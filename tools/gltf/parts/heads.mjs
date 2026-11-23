import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildHeads(root) {
  addPart(root, 'helm_cap', (mesh) => {
    const crown = column([
      [0.045, 0.154, 0.168, 0, -0.012],
      [0.105, 0.154, 0.168, 0, -0.014],
      [0.160, 0.132, 0.144, 0, -0.014],
      [0.205, 0.086, 0.094, 0, -0.012],
      [0.230, 0.024, 0.028, 0, -0.010],
    ], { segs: 8 });
    mesh(crown, M.cloth);
    const brim = column([
      [0.036, 0.154, 0.168, 0, -0.012],
      [0.062, 0.158, 0.172, 0, -0.012],
      [0.088, 0.150, 0.164, 0, -0.014],
    ], { segs: 8 });
    mesh(brim, M.clothDark);
    for (const s of [-1, 1]) {
      const flap = shadeVerts(new THREE.BoxGeometry(0.022, 0.090, 0.056), () => 0.88);
      mesh(flap, M.clothDark, [s * 0.142, -0.005, -0.048], [0, 0, s * -0.06]);
    }
  });

  // 6. helm_hood: medieval gugel/chaperon hood with open face (+Z), closed crown & draped liripipe tail
  addPart(root, 'helm_hood', (mesh) => {
    // Open-front cowl wrapping back and sides (-Z, +-X), leaving face (+Z) exposed
    const cowl = column([
      [-0.12, 0.158, 0.164, 0, -0.012],
      [-0.03, 0.154, 0.164, 0, -0.014],
      [0.06, 0.156, 0.168, 0, -0.016],
      [0.13, 0.140, 0.156, 0, -0.016],
    ], { segs: 8, arc: [-Math.PI * 1.24, Math.PI * 0.24] });
    mesh(cowl, M.cloth);
    // Closed brow & crown above eyes (y >= 0.085)
    const crown = column([
      [0.085, 0.162, 0.174, 0, -0.012],
      [0.145, 0.148, 0.162, 0, -0.014],
      [0.195, 0.100, 0.112, 0, -0.016],
      [0.225, 0.026, 0.030, 0, -0.016],
    ], { segs: 8 });
    mesh(crown, M.cloth);
    // Draped liripipe tail at back
    const tail = loft(V(0, 0.16, -0.11), V(0, 0.05, -0.32), [
      [0, 0.064, 0.060],
      [0.45, 0.044, 0.042],
      [0.85, 0.024, 0.024],
      [1, 0.010, 0.010],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(shadeVerts(tail, () => 0.88), M.clothDark);
    // Neck collar wrap
    const collar = column([
      [-0.155, 0.168, 0.168, 0, -0.012],
      [-0.105, 0.162, 0.164, 0, -0.012],
    ], { segs: 8 });
    mesh(collar, M.clothDark);
  });

  // 7. uarm_cloth: normalized bone [0..1] along +Y (shoulder -> elbow)

  addPart(root, 'helm_kettle', (mesh) => {
    const dome = column([
      [0.055, 0.156, 0.168, 0, -0.008],
      [0.120, 0.152, 0.164, 0, -0.010],
      [0.185, 0.122, 0.132, 0, -0.010],
      [0.240, 0.068, 0.074, 0, -0.008],
      [0.265, 0.014, 0.016, 0, -0.008],
    ], { segs: 8 });
    mesh(dome, M.steel);

    // Wide sloped kettle brim
    const brim = column([
      [0.005, 0.258, 0.274, 0, -0.006],
      [0.032, 0.240, 0.256, 0, -0.006],
      [0.068, 0.158, 0.170, 0, -0.008],
    ], { segs: 7 });
    mesh(brim, M.mail);

    // Subtle crest comb & brow band
    const comb = shadeVerts(new THREE.BoxGeometry(0.020, 0.042, 0.22), () => 0.96);
    mesh(comb, M.steel, [0, 0.245, -0.01]);
  });

  // helm_sallet: Gothic sallet with swept lobster-tail neck guard, brow visor & dark iron bevor
  addPart(root, 'helm_sallet', (mesh) => {
    // Upper skull dome
    const skull = column([
      [0.055, 0.154, 0.170, 0, -0.012],
      [0.125, 0.150, 0.166, 0, -0.014],
      [0.195, 0.120, 0.134, 0, -0.014],
      [0.245, 0.064, 0.074, 0, -0.012],
      [0.265, 0.018, 0.022, 0, -0.010],
    ], { segs: 8 });
    mesh(skull, M.steel);

    // Swept rear lobster tail (-Z)
    const tail = loft(V(0, 0.08, -0.10), V(0, -0.03, -0.34), [
      [0.0, 0.148, 0.065],
      [0.5, 0.132, 0.052],
      [1.0, 0.098, 0.036],
    ], { segs: 7, fwd: V(0, 1, 0) });
    mesh(shadeVerts(tail, () => 0.90), M.steel);

    // Visor brow ridge across front (+Z)
    const visor = column([
      [0.032, 0.166, 0.184, 0, 0.004],
      [0.072, 0.160, 0.178, 0, -0.002],
    ], { segs: 8, arc: [Math.PI * 0.10, Math.PI * 0.90] });
    mesh(visor, M.mail);

    // Dark iron bevor (lower chin/neck guard)
    const bevor = column([
      [-0.125, 0.134, 0.144, 0, 0.004],
      [-0.045, 0.146, 0.158, 0, 0.008],
      [-0.010, 0.144, 0.154, 0, 0.006],
    ], { segs: 7, arc: [Math.PI * 0.14, Math.PI * 0.86] });
    mesh(bevor, M.mail);
  });

  // ---------- HEADS (8 parts, centered at 0,0,0, face toward +Z) ----------
  const buildHumanHead = (mesh, { beard = 0, hair = 'short' } = {}) => {
    // Neck column (Y -0.19..-0.07)
    mesh(column([[-0.19, 0.068, 0.072], [-0.07, 0.072, 0.076]], { segs: 8 }), M.skin);
    // Cranium + jaw
    const skull = column([
      [-0.12, 0.078, 0.090, 0, 0.012],
      [-0.04, 0.125, 0.138, 0, 0.010],
      [0.04, 0.138, 0.150, 0, 0.004],
      [0.12, 0.122, 0.135, 0, -0.004],
      [0.175, 0.062, 0.072, 0, -0.006],
    ], { segs: 10 });
    mesh(heightShade(skull, -0.12, 0.175, 0.84, 1.0), M.skin);
    // Nose + dark eyes + brows
    mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.052, 0.042), () => 0.96), M.skin, [0, -0.005, 0.148]);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.026, 0.020, 0.020), () => 0.25), M.dark, [sx * 0.046, 0.022, 0.142]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.042, 0.016, 0.022), () => 0.82), M.hair, [sx * 0.048, 0.046, 0.144]);
    }
    // Hair cap
    if (hair !== 'bald') {
      const cap = column([
        [0.02, 0.145, 0.158, 0, -0.014],
        [0.11, 0.134, 0.146, 0, -0.012],
        [0.18, 0.068, 0.078, 0, -0.008],
      ], { segs: 9 });
      mesh(heightShade(cap, 0.02, 0.18, 0.82, 1.0), M.hair);
      if (hair === 'long') {
        const back = column([[-0.15, 0.132, 0.115, 0, -0.045], [0.04, 0.146, 0.135, 0, -0.030]], { segs: 8, arc: [Math.PI * 1.05, Math.PI * 1.95] });
        mesh(back, M.hair);
      }
    }
    // Beard styles
    if (beard === 1) {
      // Full beard
      const b = column([
        [-0.17, 0.088, 0.095, 0, 0.045],
        [-0.08, 0.130, 0.142, 0, 0.024],
        [-0.01, 0.134, 0.146, 0, 0.018],
      ], { segs: 8, arc: [Math.PI * 0.08, Math.PI * 0.92] });
      mesh(heightShade(b, -0.17, -0.01, 0.82, 1.0), M.hair);
    } else if (beard === 2) {
      // Mustache + goatee
      mesh(shadeVerts(new THREE.BoxGeometry(0.11, 0.024, 0.032), () => 0.88), M.hair, [0, -0.044, 0.148]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.062, 0.070, 0.040), () => 0.85), M.hair, [0, -0.115, 0.136]);
    }
  };

  addPart(root, 'head_0', (mesh) => buildHumanHead(mesh, { beard: 0, hair: 'short' }));
  addPart(root, 'head_1', (mesh) => buildHumanHead(mesh, { beard: 1, hair: 'short' }));
  addPart(root, 'head_2', (mesh) => buildHumanHead(mesh, { beard: 2, hair: 'short' }));
  addPart(root, 'head_3', (mesh) => buildHumanHead(mesh, { beard: 1, hair: 'long' }));

  addPart(root, 'head_goblin', (mesh) => {
    const cranium = shadeVerts(new THREE.SphereGeometry(0.125, 8, 6), () => 0.92);
    cranium.scale(1.05, 0.95, 1.08);
    mesh(cranium, M.skin, [0, 0.01, 0.0]);
    // Long hooked nose (+Z)
    const nose = loft(V(0, 0.0, 0.12), V(0, -0.04, 0.23), [[0, 0.028, 0.028], [1, 0.010, 0.010]], { segs: 5 });
    mesh(nose, M.skin);
    // Wide pointed ears (±X)
    for (const sx of [-1, 1]) {
      const ear = loft(V(sx * 0.12, 0.02, 0.0), V(sx * 0.32, 0.06, -0.04), [[0, 0.036, 0.018], [1, 0.008, 0.006]], { segs: 4 });
      mesh(ear, M.skin);
      mesh(shadeVerts(new THREE.SphereGeometry(0.024, 5, 4), () => 0.25), M.dark, [sx * 0.052, 0.035, 0.118]);
    }
  });

  addPart(root, 'head_troll', (mesh) => {
    const head = column([
      [-0.15, 0.125, 0.135, 0, 0.018],
      [-0.02, 0.155, 0.165, 0, 0.015],
      [0.10, 0.145, 0.152, 0, 0.008],
      [0.19, 0.092, 0.105, 0, 0.0],
    ], { segs: 8 });
    mesh(heightShade(head, -0.15, 0.19, 0.80, 1.0), M.skin);
    // Heavy brow ridge + tusks
    mesh(shadeVerts(new THREE.BoxGeometry(0.24, 0.048, 0.065), () => 0.85), M.skin, [0, 0.065, 0.155]);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.022, 0.022), () => 0.2), M.dark, [sx * 0.055, 0.032, 0.165]);
      const tusk = shadeVerts(new THREE.ConeGeometry(0.018, 0.075, 5), () => 0.98);
      mesh(tusk, M.trim, [sx * 0.068, -0.065, 0.175], [0.28, 0, -sx * 0.18]);
    }
  });

  addPart(root, 'head_skull', (mesh) => {
    const cranium = shadeVerts(new THREE.SphereGeometry(0.125, 8, 6), () => 0.96);
    cranium.scale(0.95, 1.05, 1.05);
    mesh(cranium, M.skin, [0, 0.045, -0.005]);
    // Gaunt jaw
    mesh(shadeVerts(new THREE.BoxGeometry(0.12, 0.095, 0.13), () => 0.88), M.skin, [0, -0.075, 0.025]);
    // Hollow dark eye sockets & nasal cavity
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.032, 6, 4), () => 0.15), M.dark, [sx * 0.044, 0.025, 0.112]);
    }
    mesh(shadeVerts(new THREE.ConeGeometry(0.018, 0.038, 4), () => 0.15), M.dark, [0, -0.022, 0.122]);
  });

  addPart(root, 'head_ghoul', (mesh) => {
    const head = column([
      [-0.14, 0.090, 0.098, 0, 0.012],
      [0.0, 0.124, 0.132, 0, 0.006],
      [0.12, 0.118, 0.126, 0, 0.0],
      [0.175, 0.064, 0.072, 0, 0.0],
    ], { segs: 8 });
    mesh(heightShade(head, -0.14, 0.175, 0.80, 1.0), M.skin);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.028, 5, 4), () => 0.18), M.dark, [sx * 0.044, 0.024, 0.122]);
    }
    mesh(shadeVerts(new THREE.BoxGeometry(0.075, 0.028, 0.024), () => 0.22), M.dark, [0, -0.065, 0.120]);
  });

  // ---------- HELM HELPERS ----------
  const mailCoifBase = (mesh, yTop = 0.04, r = 0.165) => {
    const coif = column([
      [-0.17, r * 1.06, r * 1.08, 0, -0.008],
      [-0.06, r * 0.98, r * 1.02, 0, -0.008],
      [yTop, r * 0.95, r * 1.00, 0, -0.006],
    ], { segs: 9, arc: [Math.PI * 0.88, Math.PI * 2.12] });
    mesh(heightShade(coif, -0.17, yTop, 0.80, 1.0), M.mail);
  };

  const domeBowl = (mesh, matMain, y0 = 0.04, yTop = 0.27, rx = 0.168, rz = 0.182, pointed = false) => {
    const g = column(pointed ? [
      [y0, rx, rz, 0, -0.006],
      [y0 + (yTop - y0) * 0.42, rx * 0.92, rz * 0.94, 0, -0.006],
      [y0 + (yTop - y0) * 0.78, rx * 0.52, rz * 0.56, 0, -0.004],
      [yTop, 0.016, 0.018, 0, -0.002],
    ] : [
      [y0, rx, rz, 0, -0.006],
      [y0 + (yTop - y0) * 0.45, rx * 0.96, rz * 0.98, 0, -0.006],
      [y0 + (yTop - y0) * 0.80, rx * 0.68, rz * 0.72, 0, -0.004],
      [yTop, 0.025, 0.028, 0, -0.002],
    ], { segs: 9 });
    mesh(heightShade(g, y0, yTop, 0.84, 1.0), matMain);
  };

  const addHorns = (mesh, yBase = 0.18, spread = 0.35, height = 0.46, matMain = M.trim) => {
    for (const sx of [-1, 1]) {
      const horn = loft(V(sx * 0.14, yBase, 0.0), V(sx * spread, height, 0.03), [
        [0.0, 0.042, 0.042],
        [0.45, 0.034, 0.034, sx * 0.04, -0.04],
        [0.80, 0.020, 0.020, sx * 0.02, -0.02],
        [1.0, 0.006, 0.006],
      ], { segs: 6 });
      mesh(horn, matMain);
    }
  };

  const buildGreathelm = (mesh, trimMat = M.brass) => {
    const barrel = column([
      [-0.16, 0.172, 0.188, 0, 0.006],
      [0.06, 0.176, 0.194, 0, 0.008],
      [0.24, 0.166, 0.182, 0, 0.004],
      [0.27, 0.115, 0.125, 0, 0.002],
    ], { segs: 10 });
    mesh(heightShade(barrel, -0.16, 0.27, 0.82, 1.0), M.steel);
    // Dark eye slit + brass cross reinforce on front (+Z)
    mesh(shadeVerts(new THREE.BoxGeometry(0.22, 0.024, 0.022), () => 0.12), M.dark, [0, 0.055, 0.194]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.036, 0.36, 0.024), () => 0.98), trimMat, [0, 0.055, 0.198]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.26, 0.030, 0.024), () => 0.98), trimMat, [0, 0.088, 0.196]);
    mesh(column([[0.21, 0.174, 0.190], [0.245, 0.170, 0.186]], { segs: 10 }), trimMat);
  };

  // ---------- HELMS (30 remaining) ----------
  addPart(root, 'helm_greathelm', (mesh) => buildGreathelm(mesh, M.brass));

  addPart(root, 'helm_greathelm_wings', (mesh) => {
    buildGreathelm(mesh, M.brass);
    for (const sx of [-1, 1]) {
      const wing = bladeExtrude([
        [0.0, 0.0], [0.06, 0.08], [0.04, 0.24], [-0.04, 0.36], [-0.09, 0.22], [-0.04, 0.06],
      ], 0.016, 0.003);
      mesh(wing, M.trim, [sx * 0.24, 0.17, -0.02], [0, 0, -sx * 0.58]);
    }
  });

  addPart(root, 'helm_greathelm_sun', (mesh) => {
    buildGreathelm(mesh, M.brass);
    const ray = shadeVerts(new THREE.ConeGeometry(0.022, 0.18, 4), () => 1.0);
    for (let i = -2; i <= 2; i++) {
      const a = i * 0.42;
      mesh(ray, M.brass, [Math.sin(a) * 0.14, 0.33 + Math.cos(a) * 0.06, 0.04], [0, 0, -a]);
    }
  });

  addPart(root, 'helm_greathelm_horns', (mesh) => {
    buildGreathelm(mesh, M.brass);
    addHorns(mesh, 0.18, 0.36, 0.48, M.trim);
  });

  addPart(root, 'helm_nasal', (mesh) => {
    mailCoifBase(mesh, 0.05);
    domeBowl(mesh, M.steel, 0.04, 0.30, 0.172, 0.185, true);
    mesh(column([[0.035, 0.180, 0.192], [0.068, 0.178, 0.190]], { segs: 9 }), M.mail);
    mesh(shadeVerts(new THREE.BoxGeometry(0.030, 0.125, 0.022), () => 0.96), M.steel, [0, -0.015, 0.188]);
  });

  addPart(root, 'helm_spangen', (mesh) => {
    mailCoifBase(mesh, 0.04);
    domeBowl(mesh, M.steel, 0.03, 0.32, 0.178, 0.190, true);
    mesh(column([[0.025, 0.188, 0.198], [0.065, 0.184, 0.195]], { segs: 9 }), M.brass);
    // Spectacle/nasal guard
    mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.13, 0.024), () => 0.96), M.brass, [0, -0.02, 0.194]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.18, 0.026, 0.022), () => 0.94), M.brass, [0, 0.03, 0.192]);
  });

  addPart(root, 'helm_spangen_horns', (mesh) => {
    mailCoifBase(mesh, 0.04);
    domeBowl(mesh, M.steel, 0.03, 0.30, 0.178, 0.190, true);
    mesh(column([[0.025, 0.188, 0.198], [0.065, 0.184, 0.195]], { segs: 9 }), M.brass);
    mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.13, 0.024), () => 0.96), M.steel, [0, -0.02, 0.194]);
    addHorns(mesh, 0.16, 0.36, 0.44, M.trim);
  });

  addPart(root, 'helm_coif', (mesh) => {
    mailCoifBase(mesh, 0.06, 0.168);
    domeBowl(mesh, M.mail, 0.04, 0.25, 0.166, 0.176, false);
  });

  addPart(root, 'helm_turban', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.165);
    const wrap = shadeVerts(new THREE.TorusGeometry(0.162, 0.068, 6, 11), () => 0.94);
    mesh(wrap, M.cloth, [0, 0.125, 0.01], [Math.PI / 2 - 0.12, 0, 0]);
    domeBowl(mesh, M.steel, 0.10, 0.34, 0.145, 0.155, true);
    mesh(shadeVerts(new THREE.SphereGeometry(0.032, 6, 5), () => 1.0), M.brass, [0, 0.14, 0.225]);
  });

  addPart(root, 'helm_spike_turban', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.168);
    const wrap = shadeVerts(new THREE.TorusGeometry(0.168, 0.068, 6, 11), () => 0.94);
    mesh(wrap, M.cloth, [0, 0.125, 0.01], [Math.PI / 2 - 0.10, 0, 0]);
    domeBowl(mesh, M.steel, 0.10, 0.32, 0.148, 0.158, true);
    mesh(lathe([[0.024, 0.30], [0.005, 0.45]], 6), M.brass);
  });

  addPart(root, 'helm_furcap', (mesh) => {
    const brim = shadeVerts(new THREE.TorusGeometry(0.172, 0.064, 6, 10), () => 0.88);
    mesh(brim, M.hair, [0, 0.08, 0], [Math.PI / 2, 0, 0]);
    domeBowl(mesh, M.cloth, 0.08, 0.27, 0.165, 0.175, false);
  });

  addPart(root, 'helm_bearhead', (mesh) => {
    mailCoifBase(mesh, 0.04, 0.175);
    const hood = domeBowl(mesh, M.hair, 0.02, 0.30, 0.195, 0.210, false);
    // Bear snout projecting forward (+Z) + 2 rounded ears
    const snout = loft(V(0, 0.14, 0.12), V(0, 0.11, 0.275), [[0, 0.085, 0.072], [1, 0.042, 0.038]], { segs: 7 });
    mesh(snout, M.hair);
    mesh(shadeVerts(new THREE.SphereGeometry(0.026, 5, 4), () => 0.2), M.dark, [0, 0.115, 0.275]);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.046, 6, 5), () => 0.85), M.hair, [sx * 0.135, 0.28, 0.02]);
    }
  });

  addPart(root, 'helm_morion', (mesh) => {
    domeBowl(mesh, M.steel, 0.06, 0.27, 0.165, 0.180, false);
    // Swept crescent boat-brim (pointed front & back)
    const brim = column([
      [0.045, 0.245, 0.335, 0, 0],
      [0.082, 0.172, 0.188, 0, 0],
    ], { segs: 10 });
    mesh(brim, M.steel);
    // High medial comb ridge
    const comb = bladeExtrude([
      [-0.22, 0.14], [-0.12, 0.32], [0.0, 0.34], [0.12, 0.32], [0.22, 0.14], [0.0, 0.18],
    ], 0.018, 0.004);
    mesh(comb, M.steel);
  });

  addPart(root, 'helm_barbute', (mesh) => {
    const shell = column([
      [-0.14, 0.175, 0.190, 0, -0.004],
      [0.05, 0.178, 0.194, 0, -0.004],
      [0.20, 0.150, 0.166, 0, -0.004],
      [0.30, 0.042, 0.048, 0, 0],
    ], { segs: 10, arc: [Math.PI * 0.62, Math.PI * 2.38] });
    mesh(heightShade(shell, -0.14, 0.30, 0.82, 1.0), M.steel);
    domeBowl(mesh, M.steel, 0.06, 0.30, 0.176, 0.192, false);
    mesh(shadeVerts(new THREE.BoxGeometry(0.028, 0.11, 0.024), () => 0.95), M.steel, [0, 0.0, 0.192]);
  });

  addPart(root, 'helm_steppe', (mesh) => {
    const neck = column([[-0.10, 0.215, 0.215], [0.05, 0.185, 0.190]], { segs: 9, arc: [Math.PI * 0.90, Math.PI * 2.10] });
    mesh(neck, M.leather);
    domeBowl(mesh, M.steel, 0.04, 0.33, 0.182, 0.192, true);
    mesh(column([[0.035, 0.194, 0.204], [0.072, 0.188, 0.198]], { segs: 9 }), M.brass);
    mesh(lathe([[0.022, 0.31], [0.005, 0.43]], 6), M.brass);
  });

  addPart(root, 'helm_tug', (mesh) => {
    const neck = column([[-0.10, 0.215, 0.215], [0.05, 0.185, 0.190]], { segs: 9, arc: [Math.PI * 0.90, Math.PI * 2.10] });
    mesh(neck, M.mail);
    domeBowl(mesh, M.steel, 0.04, 0.34, 0.184, 0.194, true);
    mesh(lathe([[0.024, 0.32], [0.006, 0.48]], 6), M.brass);
    // Flowing horsetail tug plume trailing -Z
    const tail = loft(V(0, 0.44, -0.02), V(0, 0.18, -0.38), [[0, 0.025, 0.025], [0.5, 0.060, 0.055], [1, 0.022, 0.022]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(tail, M.cloth);
  });

  addPart(root, 'helm_brigand_hat', (mesh) => {
    const brim = column([[0.055, 0.31, 0.31], [0.088, 0.175, 0.185]], { segs: 10 });
    mesh(brim, M.clothDark);
    const crown = column([[0.085, 0.170, 0.180], [0.24, 0.135, 0.145], [0.34, 0.035, 0.040]], { segs: 9 });
    mesh(heightShade(crown, 0.085, 0.34, 0.82, 1.0), M.clothDark);
    mesh(column([[0.085, 0.176, 0.186], [0.125, 0.172, 0.182]], { segs: 9 }), M.leather);
  });

  const buildVisoredHelm = (mesh, bodyMat, trimMat, snoutZ = 0.26, tailZ = -0.22) => {
    domeBowl(mesh, bodyMat, 0.04, 0.28, 0.170, 0.185, false);
    const gorget = column([[-0.16, 0.162, 0.172], [0.04, 0.172, 0.184]], { segs: 9 });
    mesh(heightShade(gorget, -0.16, 0.04, 0.82, 0.96), bodyMat);
    // Pointed visor (+Z)
    const visor = loft(V(0, 0.04, 0.11), V(0, 0.01, snoutZ), [[0, 0.145, 0.095], [0.6, 0.095, 0.068], [1, 0.022, 0.026]], { segs: 7, fwd: V(0, 1, 0) });
    mesh(visor, bodyMat);
    mesh(shadeVerts(new THREE.BoxGeometry(0.18, 0.020, 0.020), () => 0.12), M.dark, [0, 0.055, 0.195]);
    if (tailZ < -0.25) {
      const tail = loft(V(0, 0.06, -0.12), V(0, -0.04, tailZ), [[0, 0.15, 0.06], [1, 0.09, 0.03]], { segs: 6, fwd: V(0, 1, 0) });
      mesh(tail, bodyMat);
    }
    if (trimMat !== bodyMat) {
      mesh(column([[0.055, 0.176, 0.190], [0.085, 0.174, 0.188]], { segs: 9 }), trimMat);
    }
  };

  addPart(root, 'helm_armet', (mesh) => buildVisoredHelm(mesh, M.steel, M.steel, 0.26, -0.19));
  addPart(root, 'helm_bascinet', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.172);
    buildVisoredHelm(mesh, M.steel, M.brass, 0.295, -0.19);
  });
  addPart(root, 'helm_sallet_crest', (mesh) => {
    buildVisoredHelm(mesh, M.steel, M.brass, 0.20, -0.42);
    const comb = bladeExtrude([[-0.22, 0.16], [-0.08, 0.34], [0.08, 0.33], [0.16, 0.18]], 0.020, 0.004);
    mesh(comb, M.brass);
  });
  addPart(root, 'helm_dragon', (mesh) => {
    buildVisoredHelm(mesh, M.mail, M.brass, 0.28, -0.30);
    // Swept dragon fin crest
    const crest = bladeExtrude([
      [-0.28, 0.14], [-0.18, 0.44], [-0.02, 0.47], [0.14, 0.36], [0.22, 0.16], [0.0, 0.18],
    ], 0.022, 0.004);
    mesh(crest, M.brass);
  });
  addPart(root, 'helm_spike', (mesh) => {
    domeBowl(mesh, M.steel, 0.04, 0.27, 0.175, 0.185, false);
    mesh(column([[0.04, 0.21, 0.22], [0.075, 0.175, 0.185]], { segs: 9 }), M.mail);
    mesh(lathe([[0.026, 0.25], [0.005, 0.43]], 6), M.steel);
  });

  // Order & Faction Special Helms (8 parts)
  addPart(root, 'helm_lily_armet', (mesh) => {
    buildVisoredHelm(mesh, M.steel, M.brass, 0.24, -0.24);
    const fleur = bladeExtrude([[-0.08, 0.26], [-0.12, 0.40], [0.0, 0.44], [0.12, 0.40], [0.08, 0.26]], 0.018, 0.004);
    mesh(fleur, M.brass);
  });
  addPart(root, 'helm_rose_sallet', (mesh) => {
    buildVisoredHelm(mesh, M.steel, M.trim, 0.21, -0.43);
    const crest = loft(V(0, 0.24, 0.04), V(0, 0.42, -0.36), [[0, 0.04, 0.05], [0.5, 0.07, 0.08], [1, 0.02, 0.03]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(crest, M.cloth);
  });
  addPart(root, 'helm_tarn_cowl', (mesh) => {
    // Hooded iron-masked Order of the Tarn cowl
    const hood = column([
      [-0.17, 0.188, 0.20, 0, -0.02],
      [0.06, 0.192, 0.205, 0, -0.02],
      [0.24, 0.145, 0.165, 0, -0.03],
    ], { segs: 9 });
    mesh(heightShade(hood, -0.17, 0.24, 0.78, 1.0), M.clothDark);
    const tail = loft(V(0, 0.18, -0.14), V(0, 0.10, -0.32), [[0, 0.10, 0.09], [1, 0.02, 0.02]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(tail, M.clothDark);
    // Iron faceplate
    const mask = column([[-0.12, 0.142, 0.165], [0.12, 0.148, 0.170]], { segs: 7, arc: [Math.PI * 0.15, Math.PI * 0.85] });
    mesh(mask, M.mail, [0, 0, 0.032]);
  });
  addPart(root, 'helm_hollow_crown', (mesh) => {
    buildGreathelm(mesh, M.brass);
    // Crown of 6 tall dark-iron/brass spikes
    const sp = shadeVerts(new THREE.ConeGeometry(0.022, 0.18, 4), () => 0.96);
    for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2;
      mesh(sp, M.brass, [Math.cos(a) * 0.145, 0.35, Math.sin(a) * 0.155]);
    }
  });
  addPart(root, 'helm_wolfsworn', (mesh) => {
    mailCoifBase(mesh, 0.04, 0.175);
    domeBowl(mesh, M.mail, 0.03, 0.28, 0.178, 0.190, false);
    // Wolf skull/snout crest + pointed wolf ears
    const snout = loft(V(0, 0.15, 0.10), V(0, 0.12, 0.24), [[0, 0.075, 0.065], [1, 0.032, 0.032]], { segs: 6 });
    mesh(snout, M.coat);
    for (const sx of [-1, 1]) {
      const ear = shadeVerts(new THREE.ConeGeometry(0.038, 0.14, 5), () => 0.94);
      mesh(ear, M.coat, [sx * 0.11, 0.34, 0.01], [0, 0, -sx * 0.22]);
    }
  });
  addPart(root, 'helm_falcon_kulah', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.172);
    domeBowl(mesh, M.steel, 0.04, 0.34, 0.176, 0.188, true);
    mesh(lathe([[0.024, 0.32], [0.005, 0.44]], 6), M.brass);
    // Swept falcon cheek wings
    for (const sx of [-1, 1]) {
      const w = bladeExtrude([[0, 0], [0.05, 0.10], [-0.04, 0.18], [-0.05, 0.04]], 0.014, 0.002);
      mesh(w, M.brass, [sx * 0.19, 0.14, 0.04], [0, 0, -sx * 0.45]);
    }
  });
  addPart(root, 'helm_sky_mask', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.172);
    domeBowl(mesh, M.steel, 0.04, 0.35, 0.178, 0.190, true);
    mesh(lathe([[0.022, 0.33], [0.005, 0.45]], 6), M.brass);
    // Sculpted bronze face mask (+Z)
    const face = column([[-0.13, 0.148, 0.170], [0.10, 0.155, 0.176]], { segs: 8, arc: [Math.PI * 0.14, Math.PI * 0.86] });
    mesh(face, M.brass, [0, 0, 0.035]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.034, 0.060, 0.034), () => 1.0), M.brass, [0, -0.005, 0.205]);
  });
  addPart(root, 'helm_lantern_burgonet', (mesh) => {
    domeBowl(mesh, M.steel, 0.04, 0.30, 0.175, 0.190, false);
    const cheek = column([[-0.12, 0.168, 0.178], [0.05, 0.176, 0.188]], { segs: 9, arc: [Math.PI * 0.72, Math.PI * 2.28] });
    mesh(cheek, M.steel);
    // Peak brim (+Z) + high brass comb
    const peak = bladeExtrude([[0.12, 0.05], [0.24, 0.03], [0.16, 0.10]], 0.22, 0.003);
    mesh(peak, M.brass);
    const comb = bladeExtrude([[-0.18, 0.18], [-0.04, 0.35], [0.10, 0.33], [0.18, 0.18]], 0.022, 0.004);
    mesh(comb, M.brass);
  });
}
