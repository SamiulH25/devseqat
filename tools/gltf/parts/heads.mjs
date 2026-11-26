import { THREE, V, M, shadeVerts, heightShade, loft, pathLoft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildHeads(root) {
  // Faceted pentagonal visor wedge: broad shield-shaped back silhouette tapering toward the front,
  // so it reads as a visor both face-on and in profile. All dimensions are local head space (+Z face).
  function visorWedge({ width = 0.126, top = 0.078, mid = -0.012, bottom = -0.108, y = 0, z0 = 0.16, z1 = 0.22, sx = 0.60, sy = 0.72, dy = -0.006 } = {}) {
    const back = [[-width, top], [width, top], [width * 0.84, mid], [0, bottom], [-width * 0.84, mid]];
    const pos = [];
    for (const [x, yy] of back) pos.push(x, yy + y, z0);
    for (const [x, yy] of back) pos.push(x * sx, yy * sy + dy + y, z1);
    const idx = [
      0, 1, 2, 0, 2, 3, 0, 3, 4,       // back cap (-Z)
      5, 9, 8, 5, 8, 7, 5, 7, 6,       // front cap (+Z)
    ];
    for (let i = 0; i < 5; i++) {
      const j = (i + 1) % 5, bi = i, bj = j, fi = i + 5, fj = j + 5;
      idx.push(bi, fi, fj, bi, fj, bj);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    return shadeVerts(g, (_x, yy) => 0.84 + Math.max(0, Math.min(0.14, yy + 0.08)), 0.78);
  }

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

  // 6. helm_hood: medieval gugel/chaperon hood with open face (+Z), closed crown & flared shoulder capelet
  addPart(root, 'helm_hood', (mesh) => {
    // Open-front cowl wrapping back and sides (-Z, +-X), leaving face (+Z) exposed
    const cowl = column([
      [-0.13, 0.168, 0.174, 0, -0.010],
      [-0.03, 0.164, 0.174, 0, -0.012],
      [0.06, 0.166, 0.178, 0, -0.014],
      [0.14, 0.148, 0.162, 0, -0.014],
    ], { segs: 8, arc: [Math.PI * 0.82, Math.PI * 2.18] });
    mesh(heightShade(cowl, -0.13, 0.14, 0.84, 1.0), M.cloth);
    // Closed brow & crown above eyes (y >= 0.080)
    const crown = column([
      [0.080, 0.170, 0.182, 0, -0.010],
      [0.150, 0.154, 0.166, 0, -0.012],
      [0.205, 0.104, 0.114, 0, -0.014],
      [0.235, 0.028, 0.032, 0, -0.014],
    ], { segs: 8 });
    mesh(heightShade(crown, 0.08, 0.235, 0.88, 1.0), M.cloth);
    // Draped liripipe tail at back
    const tail = loft(V(0, 0.16, -0.12), V(0, 0.04, -0.30), [
      [0, 0.066, 0.060],
      [0.5, 0.044, 0.042],
      [1, 0.014, 0.014],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(shadeVerts(tail, () => 0.86), M.clothDark);
    // Flared medieval hood capelet draping over the collar/shoulders (matches reference Bowman)
    const capelet = column([
      [-0.255, 0.265, 0.205, 0, -0.008],
      [-0.190, 0.215, 0.185, 0, -0.010],
      [-0.120, 0.170, 0.168, 0, -0.010],
    ], { segs: 8 });
    mesh(heightShade(capelet, -0.255, -0.120, 0.82, 0.98), M.clothDark);
  });

  // 7. helm_kettle: classic chapel-de-fer kettle hat with tailored sloped brim & crisp faceted dome
  addPart(root, 'helm_kettle', (mesh) => {
    const dome = column([
      [0.065, 0.158, 0.170, 0, -0.006],
      [0.135, 0.152, 0.164, 0, -0.008],
      [0.202, 0.120, 0.130, 0, -0.008],
      [0.252, 0.062, 0.068, 0, -0.006],
      [0.274, 0.016, 0.018, 0, -0.006],
    ], { segs: 8 });
    mesh(heightShade(dome, 0.065, 0.274, 0.84, 1.0), M.steel);

    // Crisp sloped chapel-de-fer brim: clear silhouette without UFO overhang, keeps eyes readable.
    const brim = column([
      [0.014, 0.236, 0.248, 0, -0.004],
      [0.044, 0.212, 0.224, 0, -0.004],
      [0.080, 0.160, 0.172, 0, -0.006],
    ], { segs: 8 });
    mesh(heightShade(brim, 0.014, 0.080, 0.82, 0.96), M.steel);

    // Low rolled median rib only; a tall fin would read as a morion rather than a chapel-de-fer.
    const comb = shadeVerts(new THREE.BoxGeometry(0.020, 0.022, 0.20), () => 0.98);
    mesh(comb, M.steel, [0, 0.262, -0.008]);
  });

  // helm_sallet: sleek Gothic skull, narrow visor slit and a close swept tail (not a huge rear fin).
  addPart(root, 'helm_sallet', (mesh) => {
    const skull = column([
      [0.040, 0.166, 0.180, 0, -0.008],
      [0.120, 0.164, 0.178, 0, -0.010],
      [0.198, 0.126, 0.140, 0, -0.010],
      [0.252, 0.064, 0.074, 0, -0.008],
      [0.274, 0.018, 0.022, 0, -0.006],
    ], { segs: 8 });
    mesh(heightShade(skull, 0.040, 0.274, 0.84, 1.0), M.steel);

    // Lobster-tail neck guard hugs the nape and sweeps back horizontally.
    const tail = loft(V(0, 0.064, -0.105), V(0, -0.040, -0.335), [
      [0.0, 0.148, 0.040],
      [0.55, 0.132, 0.030],
      [1.0, 0.096, 0.018],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(shadeVerts(tail, () => 0.88), M.steel);
    mesh(shadeVerts(new THREE.BoxGeometry(0.022, 0.018, 0.190), () => 0.96), M.steel, [0, 0.020, -0.205], [0.18, 0, 0]);

    // Short pentagonal visor wedge and a single unbroken black sight line.
    mesh(visorWedge({ width: 0.142, top: 0.076, mid: -0.010, bottom: -0.098, y: 0.018, z0: 0.164, z1: 0.226, sx: 0.62, sy: 0.74 }), M.steel);
    mesh(shadeVerts(new THREE.BoxGeometry(0.160, 0.018, 0.006), () => 0.08), M.dark, [0, 0.070, 0.230]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.178, 0.015, 0.009), () => 0.98), M.steel, [0, 0.092, 0.224]);

    // Bevor wraps the jaw and chin, leaving no floating visor or bare throat gap.
    const bevor = column([
      [-0.145, 0.146, 0.162, 0, 0.010],
      [-0.055, 0.166, 0.186, 0, 0.018],
      [0.012, 0.164, 0.184, 0, 0.014],
    ], { segs: 8, arc: [Math.PI * 0.08, Math.PI * 0.92] });
    mesh(heightShade(bevor, -0.145, 0.012, 0.82, 0.96), M.steel);
  });

  // ---------- MODULAR HUMAN HEADS, FACES, HAIR & BEARDS (portrait-aligned, centered at 0,0,0, face +Z) ----------
  // All crowns and skulls share segs: 10 angular alignment so hair strictly encloses the skull with zero z-fighting.
  // Dynamic material slots used on human faces (restored/populated per-unit in Puppet3D.cs):
  //   M.neutral (0) = eye sclera, M.coat (11) = eye iris (PortraitPainter.EyeColours), M.coatDark (12) = lip/scar tone.

  const buildHeadBase = (mesh, jaw = 1) => {
    const jw = jaw === 0 ? 1.12 : jaw === 2 ? 0.86 : 1.0;
    const cw = jaw === 0 ? 1.05 : jaw === 2 ? 0.94 : 1.0;
    // Neck column (shaded darker beneath the chin)
    const neck = column([
      [-0.190, 0.068 * cw, 0.070, 0, -0.004],
      [-0.080, 0.072 * cw, 0.074, 0, -0.002],
    ], { segs: 8 });
    mesh(heightShade(neck, -0.190, -0.080, 0.90, 0.78), M.skin);

    // Sculpted cranium & jaw (segs: 10, symmetric front +Z spine)
    const skull = column([
      [-0.125, 0.066 * jw, 0.082, 0, 0.016],  // chin
      [-0.065, 0.112 * jw, 0.128, 0, 0.012],  // jaw & mouth
      [ 0.010, 0.132 * cw, 0.144, 0, 0.004],  // cheekbones & eyes
      [ 0.085, 0.132,      0.146, 0, -0.002], // brow & temples
      [ 0.145, 0.106,      0.120, 0, -0.006], // upper cranium
      [ 0.180, 0.048,      0.056, 0, -0.008], // crown top
    ], { segs: 10 });
    mesh(heightShade(skull, -0.125, 0.180, 0.84, 1.0), M.skin);

    // Ears
    for (const sx of [-1, 1]) {
      const ear = shadeVerts(new THREE.BoxGeometry(0.022, 0.056, 0.034), (_x, y) => 0.84 + Math.max(0, y + 0.02) * 1.4, 0.78);
      mesh(ear, M.skin, [sx * (0.128 * cw), 0.004, -0.006], [0, -sx * 0.22, -sx * 0.06]);
    }

    // Eyes: crisp balanced sclera, colored iris (M.coat -> EyeColours), dark pupil & upper lash
    for (const sx of [-1, 1]) {
      const ry = sx * 0.22;
      mesh(shadeVerts(new THREE.BoxGeometry(0.026, 0.016, 0.008), () => 0.90), M.neutral, [sx * 0.044, 0.018, 0.132], [0, ry, 0]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.017, 0.016, 0.010), () => 0.96), M.coat, [sx * 0.044, 0.018, 0.136], [0, ry, 0]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.011, 0.012, 0.010), () => 0.14), M.dark, [sx * 0.044, 0.018, 0.139], [0, ry, 0]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.030, 0.006, 0.010), () => 0.26), M.dark, [sx * 0.044, 0.028, 0.137], [0, ry, 0]);
    }

    // Subtle mouth/lip line (M.coatDark -> portrait lip shade)
    mesh(shadeVerts(new THREE.BoxGeometry(0.038, 0.009, 0.010), () => 0.92), M.coatDark, [0, -0.056, 0.131]);
  };

  const buildNose = (mesh, style = 1) => {
    // Sculpted tapered wedge prism from bridge between brows down to tip
    const yTop = style === 0 ? 0.026 : style === 2 ? 0.036 : 0.030;
    const yTip = style === 0 ? -0.018 : style === 2 ? -0.034 : -0.026;
    const zTip = style === 0 ? 0.166 : style === 2 ? 0.184 : 0.175;
    const wTip = style === 0 ? 0.022 : style === 2 ? 0.018 : 0.020;
    const nose = loft(V(0, yTop, 0.138), V(0, yTip, zTip), [
      [0.0, 0.011, 0.010],
      [0.65, wTip * 0.85, 0.016],
      [1.0, wTip, 0.014],
    ], { segs: 5, fwd: V(0, 0, 1) });
    mesh(shadeVerts(nose, (_x, y) => y < yTip + 0.012 ? 0.80 : 0.98, 0.76), M.skin);
  };

  const buildBrows = (mesh, style = 1) => {
    const w = style === 0 ? 0.040 : style === 2 ? 0.054 : 0.046;
    const h = style === 0 ? 0.010 : style === 2 ? 0.022 : 0.015;
    const d = style === 0 ? 0.016 : style === 2 ? 0.026 : 0.020;
    const y = style === 0 ? 0.048 : style === 2 ? 0.045 : 0.047;
    for (const sx of [-1, 1]) {
      const brow = shadeVerts(new THREE.BoxGeometry(w, h, d), () => 0.80);
      mesh(brow, M.hair, [sx * 0.046, y, 0.140], [0, sx * 0.22, -sx * 0.10]);
    }
  };

  const buildScar = (mesh, style = 1) => {
    if (style === 1) {
      // Brow scar: vertical seam above and below the right eye (skipping the eye itself)
      mesh(shadeVerts(new THREE.BoxGeometry(0.010, 0.034, 0.010), () => 0.96), M.coatDark, [0.052, 0.056, 0.140], [0, 0.22, -0.14]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.010, 0.034, 0.010), () => 0.94), M.coatDark, [0.046, -0.008, 0.140], [0, 0.22, -0.14]);
    } else if (style === 2) {
      // Cheek scar: diagonal seam across the left cheek
      mesh(shadeVerts(new THREE.BoxGeometry(0.052, 0.010, 0.010), () => 0.95), M.coatDark, [-0.068, -0.018, 0.126], [0, -0.45, 0.48]);
    }
  };

  const buildHair = (mesh, cut = 0) => {
    if (cut === 0) {
      // Short: full crown (segs: 10, strictly outside skull), high swept hairline, nape coverage, sideburns
      const cap = column([
        [0.088, 0.148, 0.160, 0, -0.004],
        [0.136, 0.144, 0.156, 0, -0.005],
        [0.178, 0.104, 0.116, 0, -0.006],
        [0.202, 0.048, 0.054, 0, -0.006],
      ], { segs: 10 });
      mesh(heightShade(cap, 0.088, 0.202, 0.84, 1.0), M.hair);
      // Swept medieval forelock sitting cleanly above the forehead
      mesh(shadeVerts(new THREE.BoxGeometry(0.144, 0.026, 0.042), () => 0.94), M.hair, [-0.008, 0.098, 0.136], [-0.14, 0, 0.09]);
      // Back nape coverage
      const nape = column([
        [-0.065, 0.128, 0.136, 0, -0.012],
        [ 0.090, 0.148, 0.160, 0, -0.004],
      ], { segs: 8, arc: [Math.PI * 0.90, Math.PI * 2.10] });
      mesh(heightShade(nape, -0.065, 0.090, 0.78, 0.92), M.hair);
      // Sideburns framing temples
      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.022, 0.072, 0.042), () => 0.86), M.hair, [sx * 0.134, 0.048, 0.038]);
      }
    } else if (cut === 1) {
      // Long: fuller parted crown (clean forehead) + shoulder-length side curtains + flowing back mane
      const cap = column([
        [0.086, 0.154, 0.166, 0, -0.004],
        [0.138, 0.150, 0.162, 0, -0.005],
        [0.182, 0.108, 0.120, 0, -0.006],
        [0.206, 0.052, 0.058, 0, -0.006],
      ], { segs: 10 });
      mesh(heightShade(cap, 0.086, 0.206, 0.84, 1.0), M.hair);
      buildHairLongLower(mesh, 0.090);
    } else if (cut === 2) {
      // Tied: sleek drawn-back crown + prominent warrior topknot with trim cord
      const cap = column([
        [0.086, 0.144, 0.156, 0, -0.005],
        [0.134, 0.140, 0.152, 0, -0.006],
        [0.176, 0.098, 0.110, 0, -0.007],
        [0.196, 0.044, 0.050, 0, -0.007],
      ], { segs: 10 });
      mesh(heightShade(cap, 0.086, 0.196, 0.84, 1.0), M.hair);
      const nape = column([
        [-0.035, 0.124, 0.132, 0, -0.012],
        [ 0.088, 0.144, 0.156, 0, -0.005],
      ], { segs: 8, arc: [Math.PI * 0.94, Math.PI * 2.06] });
      mesh(heightShade(nape, -0.035, 0.088, 0.78, 0.90), M.hair);
      // Trim hair cord around topknot base
      const cord = column([
        [0.178, 0.046, 0.046, 0, -0.045],
        [0.198, 0.044, 0.044, 0, -0.048],
      ], { segs: 8 });
      mesh(shadeVerts(cord, () => 0.96), M.trim);
      // Topknot / bun bulb
      const bun = column([
        [0.194, 0.042, 0.042, 0, -0.048],
        [0.232, 0.068, 0.068, 0, -0.058],
        [0.268, 0.050, 0.050, 0, -0.062],
        [0.284, 0.020, 0.020, 0, -0.062],
      ], { segs: 8 });
      mesh(heightShade(bun, 0.194, 0.284, 0.82, 1.0), M.hair);
    } else if (cut === 3) {
      // Cropped: close-cropped buzz cut hugging skull cleanly with high hairline
      const cap = column([
        [0.076, 0.144, 0.158, 0, -0.004],
        [0.132, 0.138, 0.152, 0, -0.005],
        [0.172, 0.096, 0.108, 0, -0.007],
        [0.190, 0.044, 0.050, 0, -0.007],
      ], { segs: 10 });
      mesh(heightShade(cap, 0.076, 0.190, 0.80, 0.96), M.hair);
      const nape = column([
        [-0.015, 0.124, 0.134, 0, -0.010],
        [ 0.078, 0.144, 0.158, 0, -0.004],
      ], { segs: 8, arc: [Math.PI * 0.96, Math.PI * 2.04] });
      mesh(heightShade(nape, -0.015, 0.078, 0.76, 0.88), M.hair);
    } else if (cut === 4) {
      // Bald fringe: horseshoe band around sides & back, completely bare crown
      const fringe = column([
        [-0.045, 0.126, 0.136, 0, -0.010],
        [ 0.015, 0.144, 0.156, 0, -0.006],
        [ 0.058, 0.140, 0.152, 0, -0.008],
      ], { segs: 8, arc: [Math.PI * 0.92, Math.PI * 2.08] });
      mesh(heightShade(fringe, -0.045, 0.058, 0.78, 0.92), M.hair);
    }
  };

  const buildHairLongLower = (mesh, topY = 0.048) => {
    // Back mane draping down to the shoulders
    const back = column([
      [-0.220, 0.162, 0.132, 0, -0.042],
      [-0.090, 0.156, 0.142, 0, -0.030],
      [ topY,  0.152, 0.158, 0, -0.008],
    ], { segs: 8, arc: [Math.PI * 0.90, Math.PI * 2.10] });
    mesh(heightShade(back, -0.220, topY, 0.76, 0.94), M.hair);
    // Side curtains framing cheeks & ears down past the jaw
    for (const sx of [-1, 1]) {
      const curtain = loft(V(sx * 0.136, topY, 0.042), V(sx * 0.148, -0.195, 0.018), [
        [0.0, 0.034, 0.056],
        [0.5, 0.036, 0.054],
        [1.0, 0.024, 0.042],
      ], { segs: 6, fwd: V(0, 0, 1) });
      mesh(heightShade(curtain, -0.195, topY, 0.78, 0.94), M.hair);
    }
  };

  const buildBeard = (mesh, style = 1) => {
    if (style === 1) {
      // Full beard: chin & lower jaw mass below the mouth, side jawbands framing bright upper cheeks, and moustache
      const chin = column([
        [-0.195, 0.072, 0.086, 0, 0.038],
        [-0.125, 0.120, 0.134, 0, 0.020],
        [-0.070, 0.132, 0.136, 0, 0.010],
      ], { segs: 8, arc: [Math.PI * 0.10, Math.PI * 0.90] });
      mesh(heightShade(chin, -0.195, -0.070, 0.78, 0.96), M.hair);
      // Side jaw cheeks connecting chin beard up to sideburns without covering upper cheekbones
      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.030, 0.072, 0.064), () => 0.88), M.hair, [sx * 0.122, -0.044, 0.066], [0, sx * 0.25, 0]);
      }
      // Moustache above lip (leaves clean mouth slit visible between moustache and chin beard)
      mesh(shadeVerts(new THREE.BoxGeometry(0.104, 0.020, 0.030), () => 0.94), M.hair, [0, -0.040, 0.142]);
    } else if (style === 2) {
      // Moustache only (no goatee, matching PortraitBust.Beard == 2)
      mesh(shadeVerts(new THREE.BoxGeometry(0.106, 0.022, 0.032), () => 0.92), M.hair, [0, -0.042, 0.144]);
      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.028, 0.036, 0.026), () => 0.86), M.hair, [sx * 0.054, -0.058, 0.136], [0, sx * 0.25, -sx * 0.22]);
      }
    } else if (style === 3) {
      // Forked beard: bold moustache + twin-pointed chin prongs (matching PortraitBust.Beard == 3)
      mesh(shadeVerts(new THREE.BoxGeometry(0.112, 0.024, 0.034), () => 0.92), M.hair, [0, -0.042, 0.144]);
      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.028, 0.036, 0.026), () => 0.86), M.hair, [sx * 0.056, -0.058, 0.136], [0, sx * 0.25, -sx * 0.22]);
      }
      // Chin anchor
      mesh(shadeVerts(new THREE.BoxGeometry(0.096, 0.056, 0.052), () => 0.86), M.hair, [0, -0.100, 0.124]);
      // Twin tapered fork prongs
      for (const sx of [-1, 1]) {
        const prong = loft(V(sx * 0.028, -0.115, 0.126), V(sx * 0.044, -0.225, 0.116), [
          [0.0, 0.026, 0.026],
          [0.55, 0.020, 0.020],
          [1.0, 0.008, 0.008],
        ], { segs: 5, fwd: V(0, 0, 1) });
        mesh(heightShade(prong, -0.225, -0.115, 0.76, 0.92), M.hair);
      }
    }
  };

  // Modular parts used at runtime by Puppet3D
  addPart(root, 'head_broad', (mesh) => buildHeadBase(mesh, 0));
  addPart(root, 'head_plain', (mesh) => buildHeadBase(mesh, 1));
  addPart(root, 'head_narrow', (mesh) => buildHeadBase(mesh, 2));

  addPart(root, 'nose_short', (mesh) => buildNose(mesh, 0));
  addPart(root, 'nose_plain', (mesh) => buildNose(mesh, 1));
  addPart(root, 'nose_long', (mesh) => buildNose(mesh, 2));

  addPart(root, 'brow_fine', (mesh) => buildBrows(mesh, 0));
  addPart(root, 'brow_plain', (mesh) => buildBrows(mesh, 1));
  addPart(root, 'brow_heavy', (mesh) => buildBrows(mesh, 2));

  addPart(root, 'scar_brow', (mesh) => buildScar(mesh, 1));
  addPart(root, 'scar_cheek', (mesh) => buildScar(mesh, 2));

  addPart(root, 'hair_short', (mesh) => buildHair(mesh, 0));
  addPart(root, 'hair_long', (mesh) => buildHair(mesh, 1));
  addPart(root, 'hair_long_under', (mesh) => buildHairLongLower(mesh, 0.048));
  addPart(root, 'hair_tied', (mesh) => buildHair(mesh, 2));
  addPart(root, 'hair_cropped', (mesh) => buildHair(mesh, 3));
  addPart(root, 'hair_bald_fringe', (mesh) => buildHair(mesh, 4));

  addPart(root, 'beard_full', (mesh) => buildBeard(mesh, 1));
  addPart(root, 'beard_moustache', (mesh) => buildBeard(mesh, 2));
  addPart(root, 'beard_forked', (mesh) => buildBeard(mesh, 3));

  // Legacy composed head_0..3 kept for reference/tooling compatibility (now clean & z-fight free)
  addPart(root, 'head_0', (mesh) => { buildHeadBase(mesh, 1); buildNose(mesh, 1); buildBrows(mesh, 1); buildHair(mesh, 0); });
  addPart(root, 'head_1', (mesh) => { buildHeadBase(mesh, 1); buildNose(mesh, 1); buildBrows(mesh, 1); buildHair(mesh, 0); buildBeard(mesh, 1); });
  addPart(root, 'head_2', (mesh) => { buildHeadBase(mesh, 1); buildNose(mesh, 1); buildBrows(mesh, 1); buildHair(mesh, 0); buildBeard(mesh, 2); });
  addPart(root, 'head_3', (mesh) => { buildHeadBase(mesh, 1); buildNose(mesh, 1); buildBrows(mesh, 1); buildHair(mesh, 4); buildBeard(mesh, 1); });

  addPart(root, 'head_goblin', (mesh) => {
    // Angular hungry little skull: narrow chin, high cheekbones, hooked nose.
    const cranium = column([
      [-0.105, 0.082, 0.092, 0, 0.020],
      [-0.020, 0.122, 0.132, 0, 0.006],
      [0.095, 0.112, 0.122, 0, -0.006],
      [0.165, 0.052, 0.062, 0, -0.012],
    ], { segs: 8 });
    mesh(heightShade(cranium, -0.105, 0.165, 0.82, 1.0), M.skin);
    // Long nose bends down at the tip like the reference goblin silhouette.
    const noseA = loft(V(0, 0.012, 0.105), V(0, -0.012, 0.190), [[0, 0.030, 0.025], [1, 0.018, 0.016]], { segs: 5, fwd: V(0, 1, 0) });
    mesh(noseA, M.skin);
    const noseB = loft(V(0, -0.012, 0.190), V(0, -0.066, 0.176), [[0, 0.018, 0.016], [1, 0.006, 0.006]], { segs: 5, fwd: V(0, 1, 0) });
    mesh(noseB, M.skin);
    // Broad leaf-shaped ears, not thin spikes; they read in one pixel cluster from the front.
    for (const sx of [-1, 1]) {
      const ear = bladeExtrude([[0.0, 0.0], [sx * 0.190, 0.072], [sx * 0.046, -0.058]], 0.024, 0.003);
      mesh(ear, M.skin, [sx * 0.090, 0.032, -0.012], [0, sx * 0.10, 0]);
      mesh(shadeVerts(new THREE.BoxGeometry(0.030, 0.024, 0.016), () => 0.20), M.dark, [sx * 0.045, 0.030, 0.124]);
    }
    mesh(shadeVerts(new THREE.BoxGeometry(0.072, 0.014, 0.014), () => 0.20), M.dark, [0, -0.067, 0.105]);
  });

  addPart(root, 'head_troll', (mesh) => {
    // Massive square brow and jaw, tiny deep-set eyes, blunt upturned tusks.
    const head = column([
      [-0.155, 0.142, 0.142, 0, 0.024],
      [-0.035, 0.170, 0.172, 0, 0.018],
      [0.095, 0.154, 0.158, 0, 0.006],
      [0.190, 0.094, 0.106, 0, -0.002],
    ], { segs: 8 });
    mesh(heightShade(head, -0.155, 0.190, 0.78, 1.0), M.skin);
    mesh(shadeVerts(new THREE.BoxGeometry(0.185, 0.090, 0.090), () => 0.84), M.skin, [0, -0.105, 0.092]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.255, 0.058, 0.068), () => 0.76), M.skin, [0, 0.066, 0.154]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.060, 0.055, 0.055), () => 0.83), M.skin, [0, 0.005, 0.165]);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.018, 0.014), () => 0.16), M.dark, [sx * 0.057, 0.036, 0.188]);
      const tusk = shadeVerts(new THREE.ConeGeometry(0.020, 0.090, 5), () => 0.98);
      mesh(tusk, M.trim, [sx * 0.073, -0.070, 0.145], [0.10, 0, -sx * 0.18]);
    }
  });

  addPart(root, 'head_skull', (mesh) => {
    const cranium = shadeVerts(new THREE.SphereGeometry(0.125, 8, 6), () => 0.96);
    cranium.scale(0.95, 1.05, 1.05);
    mesh(cranium, M.skin, [0, 0.045, -0.005]);
    // Gaunt blocky jaw and cheekbones keep the skeleton angular rather than balloon-like.
    mesh(shadeVerts(new THREE.BoxGeometry(0.118, 0.092, 0.128), () => 0.88), M.skin, [0, -0.075, 0.025]);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.BoxGeometry(0.054, 0.030, 0.035), () => 0.91), M.skin, [sx * 0.060, -0.018, 0.095], [0, 0, sx * 0.15]);
      // Flat deep sockets read as holes; round beads read as eyes.
      mesh(shadeVerts(new THREE.BoxGeometry(0.044, 0.038, 0.012), () => 0.12), M.dark, [sx * 0.044, 0.028, 0.121]);
    }
    mesh(shadeVerts(new THREE.BoxGeometry(0.026, 0.042, 0.012), () => 0.12), M.dark, [0, -0.021, 0.124]);
    // A dark mouth shadow with four large square teeth (readable, not dense denture noise).
    mesh(shadeVerts(new THREE.BoxGeometry(0.090, 0.020, 0.012), () => 0.15), M.dark, [0, -0.078, 0.092]);
    for (const x of [-0.033, -0.011, 0.011, 0.033])
      mesh(shadeVerts(new THREE.BoxGeometry(0.015, 0.034, 0.010), () => 0.96), M.skin, [x, -0.096, 0.096]);
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
      mesh(shadeVerts(new THREE.BoxGeometry(0.040, 0.032, 0.012), () => 0.14), M.dark, [sx * 0.044, 0.026, 0.126]);
    }
    mesh(shadeVerts(new THREE.BoxGeometry(0.075, 0.028, 0.018), () => 0.20), M.dark, [0, -0.065, 0.124]);
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

  const addHorns = (mesh, yBase = 0.18, spread = 0.30, height = 0.40, matMain = M.trim) => {
    for (const sx of [-1, 1]) {
      // Compact heraldic horns: thick at the cheek, curving up rather than sweeping far sideways.
      const horn = pathLoft([
        { p: V(sx * 0.135, yBase, 0.005), rx: 0.040, rz: 0.036 },
        { p: V(sx * (spread * 0.92), yBase + (height - yBase) * 0.46, -0.005), rx: 0.030, rz: 0.027 },
        { p: V(sx * spread, height, 0.018), rx: 0.006, rz: 0.006 },
      ], { segs: 6, fwd: V(0, 0, 1) });
      mesh(horn, matMain);
    }
  };

  const buildGreathelm = (mesh, trimMat = M.brass) => {
    // Heroic flat-topped great helm: octagonal barrel behind a broad, almost flat faceplate.
    const barrel = column([
      [-0.150, 0.166, 0.180, 0, 0.002],
      [-0.060, 0.174, 0.188, 0, 0.004],
      [0.160, 0.168, 0.184, 0, 0.004],
      [0.228, 0.132, 0.144, 0, 0.002],
    ], { segs: 8 });
    mesh(heightShade(barrel, -0.150, 0.228, 0.82, 1.0), M.steel);
    // Trapezoid faceplate gives the front one readable flat plane instead of a barrel curve.
    const plate = bladeExtrude([[-0.152, -0.158], [0.152, -0.158], [0.136, 0.155], [-0.136, 0.155]], 0.026, 0.003);
    mesh(plate, M.steel, [0, 0.026, 0.184]);
    // Cruciform brass reinforcing over a single dark horizontal sight slit.
    mesh(shadeVerts(new THREE.BoxGeometry(0.224, 0.026, 0.012), () => 0.10), M.dark, [0, 0.057, 0.202]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.248, 0.030, 0.014), () => 0.98), trimMat, [0, 0.091, 0.205]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.036, 0.292, 0.014), () => 0.98), trimMat, [0, 0.022, 0.206]);
    // Bold lower and crown bands frame the plate without rivet noise.
    mesh(column([[-0.150, 0.174, 0.188], [-0.108, 0.176, 0.190]], { segs: 8 }), trimMat);
    mesh(column([[0.150, 0.170, 0.186], [0.190, 0.162, 0.178]], { segs: 8 }), trimMat);
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
    addHorns(mesh, 0.18, 0.30, 0.42, M.trim);
  });

  addPart(root, 'helm_nasal', (mesh) => {
    mailCoifBase(mesh, 0.05);
    domeBowl(mesh, M.steel, 0.04, 0.30, 0.172, 0.185, true);
    mesh(column([[0.035, 0.180, 0.192], [0.068, 0.178, 0.190]], { segs: 9 }), M.mail);
    // Broad shield-shaped nasal: readable as a single bright vertical stroke over the nose.
    const nasal = bladeExtrude([[-0.026, 0.064], [0.026, 0.064], [0.020, -0.040], [0.0, -0.082], [-0.020, -0.040]], 0.020, 0.002);
    mesh(nasal, M.steel, [0, -0.006, 0.194]);
  });

  addPart(root, 'helm_spangen', (mesh) => {
    mailCoifBase(mesh, 0.04, 0.168);
    domeBowl(mesh, M.steel, 0.03, 0.31, 0.178, 0.190, true);
    // Bold brass brow band + 4 vertical spangen ribs + spectacle/nasal guard
    mesh(column([[0.025, 0.188, 0.198], [0.068, 0.184, 0.195]], { segs: 8 }), M.brass);
    const ribF = loft(V(0, 0.05, 0.190), V(0, 0.315, 0.012), [[0, 0.020, 0.012], [0.5, 0.018, 0.012], [1, 0.012, 0.010]], { segs: 4 });
    const ribB = loft(V(0, 0.05, -0.192), V(0, 0.315, -0.012), [[0, 0.020, 0.012], [0.5, 0.018, 0.012], [1, 0.012, 0.010]], { segs: 4 });
    mesh(ribF, M.brass);
    mesh(ribB, M.brass);
    const guard = bladeExtrude([[-0.028, 0.066], [0.028, 0.066], [0.022, -0.040], [0.0, -0.084], [-0.022, -0.040]], 0.022, 0.002);
    mesh(guard, M.brass, [0, -0.006, 0.198]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.205, 0.030, 0.020), () => 0.96), M.brass, [0, 0.040, 0.196]);
  });

  addPart(root, 'helm_spangen_horns', (mesh) => {
    mailCoifBase(mesh, 0.04);
    domeBowl(mesh, M.steel, 0.03, 0.30, 0.178, 0.190, true);
    mesh(column([[0.025, 0.188, 0.198], [0.065, 0.184, 0.195]], { segs: 9 }), M.brass);
    mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.13, 0.024), () => 0.96), M.steel, [0, -0.02, 0.194]);
    addHorns(mesh, 0.16, 0.29, 0.38, M.trim);
  });

  addPart(root, 'helm_coif', (mesh) => {
    mailCoifBase(mesh, 0.06, 0.168);
    domeBowl(mesh, M.mail, 0.04, 0.25, 0.166, 0.176, false);
  });

  addPart(root, 'helm_turban', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.165);
    // Rounded wrapped band hugging the skull; two large cloth rolls, no brim-like disc.
    const wrap = column([
      [0.040, 0.178, 0.190, 0, -0.002],
      [0.082, 0.190, 0.200, 0, -0.002],
      [0.126, 0.184, 0.194, 0, -0.002],
      [0.158, 0.160, 0.172, 0, -0.002],
    ], { segs: 9 });
    mesh(heightShade(wrap, 0.040, 0.158, 0.84, 1.0), M.cloth);
    mesh(column([[0.082, 0.192, 0.202], [0.106, 0.190, 0.200]], { segs: 9 }), M.clothDark);
    domeBowl(mesh, M.steel, 0.13, 0.34, 0.138, 0.150, true);
    mesh(shadeVerts(new THREE.SphereGeometry(0.034, 6, 5), () => 1.0), M.brass, [0, 0.115, 0.202]);
  });

  addPart(root, 'helm_spike_turban', (mesh) => {
    mailCoifBase(mesh, 0.03, 0.168);
    const wrap = column([
      [0.040, 0.182, 0.192, 0, -0.002],
      [0.086, 0.194, 0.204, 0, -0.002],
      [0.132, 0.184, 0.194, 0, -0.002],
      [0.160, 0.160, 0.172, 0, -0.002],
    ], { segs: 9 });
    mesh(heightShade(wrap, 0.040, 0.160, 0.84, 1.0), M.cloth);
    mesh(column([[0.084, 0.196, 0.206], [0.108, 0.192, 0.202]], { segs: 9 }), M.clothDark);
    domeBowl(mesh, M.steel, 0.13, 0.32, 0.140, 0.152, true);
    mesh(lathe([[0.024, 0.30], [0.005, 0.45]], 6), M.brass);
  });

  addPart(root, 'helm_furcap', (mesh) => {
    // Thick cylindrical fur brim with a softer cloth crown, matching the northern/fur-hat silhouette.
    const brim = column([
      [0.025, 0.188, 0.198, 0, -0.002],
      [0.070, 0.196, 0.206, 0, -0.002],
      [0.118, 0.180, 0.190, 0, -0.002],
    ], { segs: 9 });
    mesh(heightShade(brim, 0.025, 0.118, 0.78, 0.94), M.hair);
    domeBowl(mesh, M.cloth, 0.10, 0.27, 0.160, 0.170, false);
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
    // bladeExtrude shapes lie in XY; rotate the median comb into the sagittal ZY plane.
    mesh(comb, M.steel, [0, 0, 0], [0, -Math.PI / 2, 0]);
  });

  addPart(root, 'helm_barbute', (mesh) => {
    // Deep cheek/neck shell open only at the front, so the wearer's face makes the T-opening read.
    const shell = column([
      [-0.145, 0.174, 0.188, 0, -0.004],
      [0.020, 0.180, 0.194, 0, -0.004],
      [0.115, 0.172, 0.186, 0, -0.004],
    ], { segs: 10, arc: [Math.PI * 0.78, Math.PI * 2.22] });
    mesh(heightShade(shell, -0.145, 0.115, 0.82, 1.0), M.steel);
    // Closed rounded crown above the brow; the lower front remains open.
    domeBowl(mesh, M.steel, 0.090, 0.300, 0.174, 0.190, false);
    // Crisp brow and central nasal stem form the metal upright of the barbute T.
    mesh(shadeVerts(new THREE.BoxGeometry(0.214, 0.026, 0.024), () => 0.96), M.steel, [0, 0.060, 0.184]);
    const stem = bladeExtrude([[-0.023, 0.048], [0.023, 0.048], [0.018, -0.072], [0.0, -0.096], [-0.018, -0.072]], 0.020, 0.002);
    mesh(stem, M.steel, [0, -0.002, 0.190]);
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
    // Flowing horsetail tug plume trailing -Z; narrow dark hair, not a cloth sail.
    const tail = loft(V(0, 0.44, -0.02), V(0, 0.17, -0.36), [[0, 0.020, 0.020], [0.45, 0.034, 0.030], [1, 0.012, 0.012]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(tail, M.hair);
  });

  addPart(root, 'helm_brigand_hat', (mesh) => {
    const brim = column([[0.055, 0.31, 0.31], [0.088, 0.175, 0.185]], { segs: 10 });
    mesh(brim, M.clothDark);
    const crown = column([[0.085, 0.170, 0.180], [0.24, 0.135, 0.145], [0.34, 0.035, 0.040]], { segs: 9 });
    mesh(heightShade(crown, 0.085, 0.34, 0.82, 1.0), M.clothDark);
    mesh(column([[0.085, 0.176, 0.186], [0.125, 0.172, 0.182]], { segs: 9 }), M.leather);
  });

  const buildVisoredHelm = (mesh, bodyMat, trimMat, snoutZ = 0.26, tailZ = -0.22) => {
    domeBowl(mesh, bodyMat, 0.035, 0.280, 0.170, 0.184, false);
    // Full lower shell: these are enclosed helms, so no skin head is emitted underneath.
    const gorget = column([[-0.160, 0.158, 0.168], [-0.055, 0.168, 0.180], [0.038, 0.170, 0.182]], { segs: 9 });
    mesh(heightShade(gorget, -0.160, 0.038, 0.82, 0.96), bodyMat);

    // Pentagonal visor wedge: broad brow, cheek facets and one downward point, tapering forward in profile.
    const depth = 0.060 + Math.max(0, snoutZ - 0.22) * 0.45;
    mesh(visorWedge({ width: 0.126, top: 0.078, mid: -0.012, bottom: -0.108, y: 0.006, z0: snoutZ - depth, z1: snoutZ, sx: 0.60, sy: 0.72 }), bodyMat);
    mesh(shadeVerts(new THREE.BoxGeometry(0.142, 0.018, 0.006), () => 0.08), M.dark, [0, 0.060, snoutZ + 0.004]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.158, 0.014, 0.008), () => 0.98), trimMat, [0, 0.084, snoutZ + 0.002]);

    // Sallet tails are close horizontal laminations, not upright fins.
    if (tailZ < -0.25) {
      const tail = loft(V(0, 0.055, -0.115), V(0, -0.042, tailZ), [
        [0.0, 0.142, 0.036],
        [0.62, 0.120, 0.026],
        [1.0, 0.086, 0.016],
      ], { segs: 6, fwd: V(0, 1, 0) });
      mesh(shadeVerts(tail, () => 0.88), bodyMat);
    }
    if (trimMat !== bodyMat) {
      mesh(column([[0.052, 0.176, 0.190], [0.082, 0.174, 0.188]], { segs: 9 }), trimMat);
    }
  };

  addPart(root, 'helm_armet', (mesh) => buildVisoredHelm(mesh, M.steel, M.steel, 0.24, -0.19));
  addPart(root, 'helm_bascinet', (mesh) => {
    // Hounskull/pig-face bascinet: pointed visor over a steel face shell, mail aventail behind.
    mailCoifBase(mesh, 0.025, 0.176);
    domeBowl(mesh, M.steel, 0.040, 0.295, 0.176, 0.190, true);
    const face = column([
      [-0.135, 0.148, 0.162, 0, 0.012],
      [-0.040, 0.164, 0.180, 0, 0.018],
      [0.046, 0.166, 0.182, 0, 0.014],
    ], { segs: 8, arc: [Math.PI * 0.07, Math.PI * 0.93] });
    mesh(heightShade(face, -0.135, 0.046, 0.82, 0.96), M.steel);
    mesh(visorWedge({ width: 0.130, top: 0.082, mid: -0.010, bottom: -0.126, y: 0.002, z0: 0.232, z1: 0.315, sx: 0.56, sy: 0.70, dy: -0.010 }), M.steel);
    mesh(shadeVerts(new THREE.BoxGeometry(0.142, 0.018, 0.006), () => 0.08), M.dark, [0, 0.064, 0.319]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.158, 0.015, 0.009), () => 0.98), M.steel, [0, 0.090, 0.313]);
  });
  addPart(root, 'helm_sallet_crest', (mesh) => {
    buildVisoredHelm(mesh, M.steel, M.brass, 0.20, -0.42);
    const comb = bladeExtrude([[-0.22, 0.16], [-0.08, 0.34], [0.08, 0.33], [0.16, 0.18]], 0.020, 0.004);
    mesh(comb, M.brass, [0, 0, 0], [0, -Math.PI / 2, 0]);
  });
  addPart(root, 'helm_dragon', (mesh) => {
    buildVisoredHelm(mesh, M.mail, M.brass, 0.28, -0.30);
    // Swept dragon fin crest
    const crest = bladeExtrude([
      [-0.28, 0.14], [-0.18, 0.44], [-0.02, 0.47], [0.14, 0.36], [0.22, 0.16], [0.0, 0.18],
    ], 0.022, 0.004);
    mesh(crest, M.brass, [0, 0, 0], [0, -Math.PI / 2, 0]);
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
    const crest = loft(V(0, 0.24, 0.04), V(0, 0.42, -0.36), [[0, 0.026, 0.030], [0.5, 0.040, 0.044], [1, 0.012, 0.016]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(crest, M.cloth);
  });
  addPart(root, 'helm_tarn_cowl', (mesh) => {
    // Deep hood with a recessed angular iron mask, matching the dark order specialist silhouette.
    const cowl = column([
      [-0.185, 0.196, 0.204, 0, -0.018],
      [-0.060, 0.194, 0.206, 0, -0.020],
      [0.075, 0.184, 0.196, 0, -0.020],
    ], { segs: 9, arc: [Math.PI * 0.80, Math.PI * 2.20] });
    mesh(heightShade(cowl, -0.185, 0.075, 0.76, 0.96), M.clothDark);
    const crown = column([
      [0.060, 0.188, 0.200, 0, -0.018],
      [0.170, 0.158, 0.172, 0, -0.026],
      [0.245, 0.072, 0.084, 0, -0.034],
    ], { segs: 9 });
    mesh(heightShade(crown, 0.060, 0.245, 0.80, 1.0), M.clothDark);
    const tail = loft(V(0, 0.17, -0.14), V(0, 0.09, -0.31), [[0, 0.075, 0.064], [1, 0.014, 0.014]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(tail, M.clothDark);
    // Black recess, then one bold trapezoidal mask plate with a single eye slit.
    mesh(shadeVerts(new THREE.BoxGeometry(0.190, 0.230, 0.018), () => 0.12), M.dark, [0, -0.010, 0.145]);
    const mask = bladeExtrude([[-0.082, -0.112], [0.082, -0.112], [0.092, 0.070], [0.060, 0.120], [-0.060, 0.120], [-0.092, 0.070]], 0.024, 0.003);
    mesh(mask, M.mail, [0, -0.006, 0.164]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.112, 0.018, 0.012), () => 0.08), M.dark, [0, 0.040, 0.181]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.020, 0.142, 0.012), () => 0.92), M.mail, [0, -0.030, 0.182]);
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
    const snout = loft(V(0, 0.15, 0.10), V(0, 0.12, 0.245), [[0, 0.072, 0.046], [1, 0.030, 0.024]], { segs: 6, fwd: V(0, 1, 0) });
    mesh(snout, M.coat);
    mesh(shadeVerts(new THREE.BoxGeometry(0.046, 0.030, 0.018), () => 0.14), M.dark, [0, 0.118, 0.246]);
    for (const sx of [-1, 1]) {
      const ear = shadeVerts(new THREE.ConeGeometry(0.046, 0.17, 5), () => 0.94);
      mesh(ear, M.coat, [sx * 0.112, 0.355, 0.005], [0, 0, -sx * 0.20]);
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
    for (const sx of [-1, 1]) mesh(shadeVerts(new THREE.BoxGeometry(0.042, 0.014, 0.010), () => 0.10), M.dark, [sx * 0.044, 0.030, 0.213]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.034, 0.060, 0.034), () => 1.0), M.brass, [0, -0.005, 0.212]);
  });
  addPart(root, 'helm_lantern_burgonet', (mesh) => {
    domeBowl(mesh, M.steel, 0.04, 0.30, 0.175, 0.190, false);
    const cheek = column([[-0.12, 0.168, 0.178], [0.05, 0.176, 0.188]], { segs: 9, arc: [Math.PI * 0.72, Math.PI * 2.28] });
    mesh(cheek, M.steel);
    // Horizontal forward peak (+Z) with a brass lip, plus a sagittal brass comb.
    mesh(shadeVerts(new THREE.BoxGeometry(0.238, 0.018, 0.118), () => 0.94), M.steel, [0, 0.067, 0.204], [-0.10, 0, 0]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.250, 0.014, 0.020), () => 0.98), M.brass, [0, 0.064, 0.260]);
    const comb = bladeExtrude([[-0.18, 0.18], [-0.04, 0.35], [0.10, 0.33], [0.18, 0.18]], 0.022, 0.004);
    mesh(comb, M.brass, [0, 0, 0], [0, -Math.PI / 2, 0]);
  });

  // helm_cabasset: Pear-stalk Renaissance steel helm with swept stalk & proud brass brim band
  addPart(root, 'helm_cabasset', (mesh) => {
    const dome = column([
      [0.055, 0.162, 0.174], [0.155, 0.146, 0.158], [0.245, 0.088, 0.096, 0, -0.016], [0.310, 0.026, 0.028, 0, -0.028], [0.355, 0.014, 0.014, 0, -0.036],
    ], { segs: 8 });
    mesh(heightShade(dome, 0.055, 0.355, 0.84, 1.0), M.steel);
    mesh(column([[0.045, 0.200, 0.212], [0.068, 0.178, 0.190]], { segs: 8 }), M.steel);
    mesh(column([[0.064, 0.194, 0.206], [0.092, 0.184, 0.196]], { segs: 8 }), M.brass);
  });

  // helm_austere_greathelm: Plain dark-iron flat-top Tarn greathelm (compact height, flush eye slit & nasal bar)
  addPart(root, 'helm_austere_greathelm', (mesh) => {
    const can = column([
      [-0.13, 0.170, 0.180], [0.04, 0.172, 0.182], [0.20, 0.164, 0.174], [0.235, 0.138, 0.148], [0.245, 0.01, 0.01],
    ], { segs: 8 });
    mesh(heightShade(can, -0.13, 0.245, 0.78, 0.96), M.mail);
    mesh(shadeVerts(new THREE.BoxGeometry(0.175, 0.030, 0.016), () => 0.06), M.dark, [0, 0.052, 0.174]); // flush eye slit
    mesh(shadeVerts(new THREE.BoxGeometry(0.032, 0.23, 0.018), () => 0.94), M.steel, [0, 0.035, 0.178]); // flush iron nasal bar
  });

  // helm_steppe_plumed: Conical steppe steel helm with brown fur brim, brass socket & tilted horsetail plume
  addPart(root, 'helm_steppe_plumed', (mesh) => {
    mailCoifBase(mesh, 0.02, 0.166);
    const cone = column([
      [0.06, 0.168, 0.178], [0.16, 0.132, 0.140], [0.28, 0.064, 0.068], [0.36, 0.018, 0.018],
    ], { segs: 8 });
    mesh(heightShade(cone, 0.06, 0.36, 0.84, 1.0), M.mail);
    mesh(column([[0.02, 0.184, 0.194], [0.08, 0.178, 0.188]], { segs: 8 }), M.hair); // brown fur brim
    mesh(column([[0.33, 0.030, 0.030], [0.38, 0.026, 0.026]], { segs: 6 }), M.brass); // brass plume socket
    mesh(lathe([[0.022, 0.36], [0.048, 0.46], [0.028, 0.58], [0.0, 0.60]], 6), M.cloth, [0, 0, -0.02], [-0.18, 0, 0]);
  });

  // helm_vendel: Crested Nordic Vendel spectacle helm with brass brow-band and nasal-mask
  addPart(root, 'helm_vendel', (mesh) => {
    mailCoifBase(mesh, 0.02, 0.168);
    domeBowl(mesh, M.mail, 0.04, 0.30, 0.174, 0.186, false);
    const comb = bladeExtrude([[-0.17, 0.16], [-0.02, 0.34], [0.14, 0.30], [0.18, 0.14]], 0.024, 0.004);
    mesh(comb, M.brass, [0, 0, 0], [0, -Math.PI / 2, 0]);
    mesh(column([[0.035, 0.182, 0.194], [0.070, 0.182, 0.194]], { segs: 8 }), M.brass);
    // Spectacle guard + nasal (+Z)
    mesh(shadeVerts(new THREE.BoxGeometry(0.16, 0.028, 0.024), () => 0.94), M.steel, [0, 0.015, 0.185]);
    mesh(shadeVerts(new THREE.BoxGeometry(0.030, 0.11, 0.026), () => 0.96), M.steel, [0, -0.01, 0.192]);
  });

  // helm_kettle_bevor: Brevon chapel-de-fer kettle hat with fitted chin/throat steel bevor & mail neck
  addPart(root, 'helm_kettle_bevor', (mesh) => {
    mailCoifBase(mesh, 0.02, 0.165);
    const dome = column([
      [0.065, 0.158, 0.170, 0, -0.006],
      [0.135, 0.152, 0.164, 0, -0.008],
      [0.205, 0.118, 0.128, 0, -0.008],
      [0.268, 0.020, 0.022, 0, -0.006],
    ], { segs: 8 });
    mesh(heightShade(dome, 0.065, 0.268, 0.84, 1.0), M.steel);
    const brim = column([
      [0.016, 0.236, 0.248, 0, -0.004],
      [0.048, 0.210, 0.222, 0, -0.004],
      [0.080, 0.160, 0.172, 0, -0.006],
    ], { segs: 8 });
    mesh(heightShade(brim, 0.016, 0.080, 0.82, 0.96), M.steel);
    // Fitted steel chin/throat bevor (+Z) wrapping to cheeks with no floating gap
    const bevor = column([[-0.15, 0.145, 0.155], [-0.01, 0.162, 0.172]], { segs: 8, arc: [0, Math.PI] });
    mesh(bevor, M.steel, [0, 0, 0.012]);
  });
}
