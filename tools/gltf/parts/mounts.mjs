import { THREE, V, M, shadeVerts, heightShade, loft, pathLoft, drapeSheet, column, addPart } from '../geom.mjs';

export function buildMounts(root) {
  // ---------- HORSES (15 parts) ----------

  // Anatomical equine barrel along Z (-0.94..+0.92):
  // Broad muscular chest (+Z), raised withers, gentle saddle dip, deep ribcage with tucked flank/belly, rounded croup (-Z).
  const barrelProfile = (th, t) => {
    const sy = Math.sin(th); // +1 = top (spine), -1 = bottom (belly)
    let mx = 1.0, mz = 1.0;
    // Slight ridge at the withers & spine (t ~ 0.65..0.85), narrower across the crest
    if (sy > 0.35) {
      mx *= 0.90;
      if (t > 0.58 && t < 0.88) mz *= 1.07; // withers rise
    }
    // Tucked-up flank/belly ahead of the hindquarters (t ~ 0.22..0.48)
    if (sy < -0.25) {
      mx *= 0.88;
      if (t > 0.18 && t < 0.52) mz *= 0.82;
      else if (t >= 0.65) mz *= 1.08; // deep muscular chest/brisket
    }
    return [mx, mz];
  };

  const horseBarrel = (mesh, bodyMat = M.coat) => {
    const body = loft(V(0, 0.04, -0.94), V(0, 0.08, 0.92), [
      [0.00, 0.13, 0.13, 0, -0.02], // tail dock
      [0.10, 0.29, 0.29, 0, 0.05],  // rounded croup / haunches
      [0.24, 0.34, 0.31, 0, 0.04],  // wide hindquarters
      [0.42, 0.29, 0.26, 0, -0.01], // tucked loin / flank
      [0.58, 0.31, 0.28, 0, -0.02], // deep barrel / saddle hollow
      [0.76, 0.33, 0.32, 0, 0.05],  // muscular shoulders & withers
      [0.91, 0.28, 0.27, 0, 0.04],  // proud breast / chest
      [1.00, 0.13, 0.14, 0, 0.01],  // brisket front
    ], { segs: 12, fwd: V(0, 1, 0), profile: barrelProfile });
    mesh(heightShade(body, -0.28, 0.42, 0.72, 1.0), bodyMat);
  };

  const horseSaddle = (mesh, padMat = M.cloth, saddleMat = M.leather, withTack = true) => {
    // Contoured draped saddle cloth hugging the curved barrel (no flat floating box!)
    const cloth = drapeSheet([
      { z: -0.44, w: 0.345, topY: 0.355, sideY: 0.16, hemY: -0.05, flare: 0.015 },
      { z: -0.22, w: 0.335, topY: 0.335, sideY: 0.14, hemY: -0.07, flare: 0.015 },
      { z:  0.02, w: 0.335, topY: 0.325, sideY: 0.13, hemY: -0.07, flare: 0.015 },
      { z:  0.24, w: 0.345, topY: 0.355, sideY: 0.15, hemY: -0.05, flare: 0.015 },
      { z:  0.38, w: 0.340, topY: 0.375, sideY: 0.18, hemY:  0.01, flare: 0.010 },
    ], { segs: 10 });
    mesh(heightShade(cloth, -0.08, 0.38, 0.80, 1.0), padMat);

    // Subtle trim edging along the saddle cloth bottom
    const padTrim = drapeSheet([
      { z: -0.45, w: 0.352, topY: 0.358, sideY: 0.16, hemY: -0.065, flare: 0.016 },
      { z: -0.41, w: 0.352, topY: 0.355, sideY: 0.16, hemY: -0.065, flare: 0.016 },
    ], { segs: 10 });
    mesh(shadeVerts(padTrim, () => 0.94), M.trim);

    // Sculpted medieval war saddle seat
    const seat = drapeSheet([
      { z: -0.28, w: 0.25, topY: 0.385, sideY: 0.28, hemY: 0.17, flare: 0.02 },
      { z: -0.04, w: 0.24, topY: 0.365, sideY: 0.26, hemY: 0.14, flare: 0.02 },
      { z:  0.20, w: 0.24, topY: 0.385, sideY: 0.28, hemY: 0.17, flare: 0.02 },
    ], { segs: 8 });
    mesh(heightShade(seat, 0.14, 0.40, 0.78, 0.96), saddleMat);

    // High arched medieval pommel (+Z) & wrap-around cantle (-Z)
    const pommel = loft(V(-0.21, 0.38, 0.21), V(0.21, 0.38, 0.21), [
      [0.00, 0.032, 0.055, 0, -0.06],
      [0.25, 0.040, 0.095, 0,  0.04],
      [0.50, 0.042, 0.110, 0,  0.07],
      [0.75, 0.040, 0.095, 0,  0.04],
      [1.00, 0.032, 0.055, 0, -0.06],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(heightShade(pommel, 0.30, 0.50, 0.80, 1.0), saddleMat);

    const cantle = loft(V(-0.23, 0.39, -0.27), V(0.23, 0.39, -0.27), [
      [0.00, 0.034, 0.065, 0, -0.05],
      [0.25, 0.044, 0.115, 0,  0.05],
      [0.50, 0.046, 0.130, 0,  0.08],
      [0.75, 0.044, 0.115, 0,  0.05],
      [1.00, 0.034, 0.065, 0, -0.05],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(heightShade(cantle, 0.30, 0.52, 0.80, 1.0), saddleMat);

    if (withTack) {
      // Leather girth strap wrapping underneath the belly
      const girth = loft(V(0, 0.05, 0.10), V(0, 0.05, 0.18), [
        [0, 0.325, 0.285], [1, 0.325, 0.285],
      ], { segs: 12, fwd: V(0, 1, 0), caps: false });
      mesh(heightShade(girth, -0.26, 0.34, 0.72, 0.90), M.leather);

      // Leather breastplate (peytral strap) curving across the chest + brass medallion
      const breast = pathLoft([
        { p: V(-0.31, 0.22, 0.28), rx: 0.022, rz: 0.032 },
        { p: V(-0.30, 0.14, 0.62), rx: 0.024, rz: 0.036 },
        { p: V(-0.18, 0.08, 0.86), rx: 0.024, rz: 0.038 },
        { p: V( 0.00, 0.06, 0.93), rx: 0.026, rz: 0.042 },
        { p: V( 0.18, 0.08, 0.86), rx: 0.024, rz: 0.038 },
        { p: V( 0.30, 0.14, 0.62), rx: 0.024, rz: 0.036 },
        { p: V( 0.31, 0.22, 0.28), rx: 0.022, rz: 0.032 },
      ], { segs: 5, fwd: V(0, 1, 0), caps: false });
      mesh(shadeVerts(breast, () => 0.84), M.leather);
      mesh(shadeVerts(new THREE.SphereGeometry(0.055, 6, 5), () => 0.96), M.brass, [0, 0.06, 0.94]);

      // Stirrup leathers + iron stirrups on both sides
      for (const sx of [-1, 1]) {
        mesh(shadeVerts(new THREE.BoxGeometry(0.022, 0.36, 0.032), () => 0.80), M.leather, [sx * 0.34, 0.02, 0.06]);
        mesh(shadeVerts(new THREE.TorusGeometry(0.045, 0.012, 4, 8), () => 0.92), M.steel, [sx * 0.35, -0.17, 0.06], [0, Math.PI / 2, 0]);
      }
    }
  };

  // Full contoured caparison / barding builder (zero floating boxes, zero barrel clipping!)
  const buildFullCaparison = (mesh, clothMat, trimMat, hemY = -0.44, pleat = 0.014) => {
    // Generous contoured drape enveloping chest, flanks, and croup
    const stations = [
      { z: -0.96, w: 0.18, topY: 0.24, sideY: 0.04, hemY: hemY + 0.08, flare: 0.03, pleat: pleat * 0.5 },
      { z: -0.78, w: 0.36, topY: 0.39, sideY: 0.14, hemY: hemY + 0.02, flare: 0.05, pleat },
      { z: -0.45, w: 0.38, topY: 0.39, sideY: 0.14, hemY: hemY,        flare: 0.05, pleat },
      { z:  0.00, w: 0.37, topY: 0.36, sideY: 0.12, hemY: hemY,        flare: 0.05, pleat },
      { z:  0.45, w: 0.38, topY: 0.42, sideY: 0.15, hemY: hemY,        flare: 0.05, pleat },
      { z:  0.80, w: 0.35, topY: 0.41, sideY: 0.15, hemY: hemY + 0.02, flare: 0.05, pleat },
      { z:  0.98, w: 0.20, topY: 0.28, sideY: 0.06, hemY: hemY + 0.08, flare: 0.03, pleat: pleat * 0.5 },
    ];
    const skirt = drapeSheet(stations, { segs: 12 });
    mesh(heightShade(skirt, hemY - 0.04, 0.44, 0.72, 1.0), clothMat);

    // Front chest apron closing the front of the caparison cleanly
    const chestApron = loft(V(0, hemY + 0.08, 0.97), V(0, 0.28, 0.94), [
      [0.00, 0.23, 0.05],
      [0.50, 0.29, 0.08],
      [1.00, 0.20, 0.07],
    ], { segs: 8, fwd: V(0, 0, 1) });
    mesh(heightShade(chestApron, hemY, 0.30, 0.74, 0.96), clothMat);

    // Rear croup apron closing the rear cleanly
    const rearApron = loft(V(0, hemY + 0.08, -0.95), V(0, 0.24, -0.92), [
      [0.00, 0.21, 0.05],
      [0.50, 0.28, 0.08],
      [1.00, 0.18, 0.07],
    ], { segs: 8, fwd: V(0, 0, -1) });
    mesh(heightShade(rearApron, hemY, 0.28, 0.72, 0.94), clothMat);

    // Contoured lower hem trim band matching the exact skirt silhouette (NO rectangular box!)
    const trimStations = stations.map(s => ({
      z: s.z,
      w: s.w + s.flare + 0.012,
      topY: s.hemY + 0.065,
      sideY: s.hemY + 0.035,
      hemY: s.hemY - 0.005,
      flare: 0.004,
      pleat: s.pleat,
    }));
    // Left & right trim ribbons along the lower hem
    for (const sx of [-1, 1]) {
      const bandPts = trimStations.map(s => ({
        p: V(sx * (s.w - 0.004), s.hemY + 0.03, s.z),
        rx: 0.016,
        rz: 0.038,
      }));
      const band = pathLoft(bandPts, { segs: 5, fwd: V(0, 1, 0), caps: true });
      mesh(shadeVerts(band, () => 0.96), trimMat);
    }
    // Front & rear trim edge
    const frontTrim = loft(V(-0.23, hemY + 0.10, 0.98), V(0.23, hemY + 0.10, 0.98), [
      [0, 0.025, 0.032], [0.5, 0.032, 0.038, 0, -0.02], [1, 0.025, 0.032],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(shadeVerts(frontTrim, () => 0.96), trimMat);
    const rearTrim = loft(V(-0.21, hemY + 0.10, -0.96), V(0.21, hemY + 0.10, -0.96), [
      [0, 0.025, 0.032], [0.5, 0.032, 0.038, 0, -0.02], [1, 0.025, 0.032],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(shadeVerts(rearTrim, () => 0.94), trimMat);
  };

  addPart(root, 'horse_body', (mesh) => {
    horseBarrel(mesh, M.coat);
    horseSaddle(mesh, M.clothDark, M.leather, true);
  });

  addPart(root, 'horse_body_barded', (mesh) => {
    horseBarrel(mesh, M.coat);
    buildFullCaparison(mesh, M.cloth, M.trim, -0.44, 0.016);
    horseSaddle(mesh, M.clothDark, M.leather, false);
  });

  addPart(root, 'horse_body_half', (mesh) => {
    // Half-barding: armored steel peytral (chest) + cloth/trimmed crupper (rump), flanks open!
    horseBarrel(mesh, M.coat);
    // Steel peytral chest armor wrapping the breast
    const peytral = drapeSheet([
      { z: 0.42, w: 0.365, topY: 0.40, sideY: 0.16, hemY: -0.14, flare: 0.02 },
      { z: 0.72, w: 0.355, topY: 0.39, sideY: 0.14, hemY: -0.22, flare: 0.03 },
      { z: 0.95, w: 0.210, topY: 0.28, sideY: 0.08, hemY: -0.20, flare: 0.02 },
    ], { segs: 10 });
    mesh(heightShade(peytral, -0.24, 0.42, 0.78, 1.0), M.steel);
    const chestPlate = loft(V(0, -0.20, 0.94), V(0, 0.28, 0.92), [
      [0, 0.22, 0.06], [0.5, 0.29, 0.09], [1, 0.20, 0.07],
    ], { segs: 8, fwd: V(0, 0, 1) });
    mesh(heightShade(chestPlate, -0.22, 0.30, 0.80, 1.0), M.steel);
    mesh(shadeVerts(new THREE.SphereGeometry(0.068, 7, 5), () => 0.98), M.brass, [0, 0.06, 0.98]);

    // Draped cloth crupper over the hindquarters
    const crupper = drapeSheet([
      { z: -0.95, w: 0.19, topY: 0.24, sideY: 0.05, hemY: -0.22, flare: 0.03 },
      { z: -0.74, w: 0.36, topY: 0.38, sideY: 0.15, hemY: -0.26, flare: 0.04, pleat: 0.01 },
      { z: -0.44, w: 0.36, topY: 0.37, sideY: 0.15, hemY: -0.22, flare: 0.03, pleat: 0.01 },
    ], { segs: 10 });
    mesh(heightShade(crupper, -0.28, 0.40, 0.76, 1.0), M.cloth);
    horseSaddle(mesh, M.cloth, M.leather, true);
  });

  addPart(root, 'horse_body_scale', (mesh) => {
    horseBarrel(mesh, M.coat);
    // Heavy cataphract/mail/scale barding with contoured brass rim (no floating box, no coat poking through!)
    buildFullCaparison(mesh, M.mail, M.brass, -0.40, 0.006);
    // Reinforced steel breastplate boss & croup ridge
    mesh(shadeVerts(new THREE.SphereGeometry(0.075, 7, 5), () => 0.98), M.brass, [0, 0.06, 0.99]);
    horseSaddle(mesh, M.cloth, M.leather, false);
  });

  addPart(root, 'horse_body_fur', (mesh) => {
    horseBarrel(mesh, M.coat);
    // Contoured shaggy steppe pelt draped over withers, back & haunches
    const pelt = drapeSheet([
      { z: -0.82, w: 0.35, topY: 0.38, sideY: 0.16, hemY: -0.14, flare: 0.035, pleat: 0.018 },
      { z: -0.42, w: 0.37, topY: 0.38, sideY: 0.16, hemY: -0.18, flare: 0.040, pleat: 0.018 },
      { z:  0.00, w: 0.36, topY: 0.35, sideY: 0.14, hemY: -0.16, flare: 0.035, pleat: 0.018 },
      { z:  0.46, w: 0.37, topY: 0.41, sideY: 0.17, hemY: -0.16, flare: 0.040, pleat: 0.018 },
      { z:  0.78, w: 0.33, topY: 0.38, sideY: 0.15, hemY: -0.10, flare: 0.025, pleat: 0.012 },
    ], { segs: 10 });
    mesh(heightShade(pelt, -0.20, 0.42, 0.74, 1.0), M.hair);
    horseSaddle(mesh, M.cloth, M.leather, true);
  });

  // Arched horse necks (normalized Y -0.12..1.06, +Z = throat, -Z = arched crest/mane)
  const buildHorseNeck = (mesh, neckMat, armorMat = null) => {
    const neckProfile = (th, t) => {
      const sz = Math.sin(th); // +1 = throat, -1 = crest
      // Narrower dorsal crest, broader muscular base at throat/windpipe
      return [sz < -0.2 ? 0.78 : 1.0, sz < 0 ? 1.12 : 0.92];
    };
    const neck = loft(V(0, -0.14, 0), V(0, 1.06, 0), [
      [0.00, 0.23, 0.31, 0, -0.02], // muscular base blending into chest/withers
      [0.28, 0.20, 0.27, 0, -0.06], // arched mid-lower neck
      [0.58, 0.16, 0.22, 0, -0.08], // proud equine crest arch
      [0.84, 0.13, 0.18, 0, -0.05], // tapering toward poll
      [1.00, 0.12, 0.15, 0,  0.00], // throatlatch / poll socket
    ], { segs: 10, profile: neckProfile });
    mesh(heightShade(neck, -0.14, 1.06, 0.76, 1.0), neckMat);

    // Flowing layered mane along the arched -Z crest
    const mane = pathLoft([
      { p: V(0, 0.02, -0.28), rx: 0.052, rz: 0.095 },
      { p: V(0, 0.28, -0.32), rx: 0.056, rz: 0.115 },
      { p: V(0, 0.58, -0.29), rx: 0.054, rz: 0.115 },
      { p: V(0, 0.85, -0.22), rx: 0.044, rz: 0.095 },
      { p: V(0, 1.04, -0.14), rx: 0.032, rz: 0.065 },
    ], { segs: 6, fwd: V(0, 0, 1) });
    mesh(heightShade(mane, 0.0, 1.04, 0.72, 0.96), M.coatDark);

    if (armorMat) {
      // Contoured overlapping crinet lames hugging the arched neck crest & sides
      const lames = [
        [0.14, 0.225, 0.295, -0.04],
        [0.36, 0.200, 0.265, -0.065],
        [0.58, 0.175, 0.230, -0.075],
        [0.78, 0.150, 0.195, -0.055],
      ];
      for (const [y, rx, rz, oz] of lames) {
        const lame = loft(V(0, y - 0.09, oz), V(0, y + 0.09, oz), [
          [0.0, rx * 1.02, rz * 1.02],
          [0.5, rx * 1.04, rz * 1.04],
          [1.0, rx * 0.94, rz * 0.94],
        ], { segs: 9, arc: [-Math.PI * 0.85, Math.PI * 0.85], caps: false });
        mesh(heightShade(lame, y - 0.09, y + 0.09, 0.82, 1.0), armorMat);
      }
    }
  };
  addPart(root, 'horse_neck', (mesh) => buildHorseNeck(mesh, M.coat));
  addPart(root, 'horse_neck_barded', (mesh) => buildHorseNeck(mesh, M.cloth, M.steel));
  addPart(root, 'horse_neck_scale', (mesh) => buildHorseNeck(mesh, M.coat, M.mail));

  // Sculpted equine head (poll at 0,0,0, muzzle toward +Z up to 0.60, +Y = forehead/bridge, -Y = jaw/jowl)
  const buildHorseHead = (mesh, shaffronMat = null) => {
    const headProfile = (th, t) => {
      const sy = Math.sin(th); // +1 = forehead/bridge, -1 = jowl/jaw
      let mx = 1.0, mz = 1.0;
      // Broad flat forehead & prominent rounded jowl/cheekbone near the back of the head (t ~ 0.15..0.42)
      if (t > 0.12 && t < 0.45 && sy < -0.15) {
        mx *= 1.08;
        mz *= 1.32; // iconic equine jowl / ganache!
      }
      // Flatter dorsal bridge along the nose
      if (t > 0.35 && t < 0.88 && sy > 0.35) {
        mx *= 0.82;
      }
      // Subtle nostril flare at the muzzle
      if (t >= 0.82 && Math.abs(Math.cos(th)) > 0.5) {
        mx *= 1.12;
      }
      return [mx, mz];
    };

    const head = loft(V(0, 0.04, -0.10), V(0, -0.02, 0.60), [
      [0.00, 0.115, 0.125, 0,  0.00], // poll / occiput
      [0.18, 0.145, 0.155, 0, -0.01], // brow & jowl base
      [0.34, 0.135, 0.145, 0, -0.02], // eye / cheekbone taper
      [0.62, 0.088, 0.102, 0,  0.00], // sleek nasal bridge
      [0.86, 0.078, 0.088, 0,  0.00], // muzzle / nostrils
      [1.00, 0.062, 0.068, 0, -0.01], // upper & lower lip
    ], { segs: 10, fwd: V(0, 1, 0), profile: headProfile });
    mesh(heightShade(head, -0.18, 0.20, 0.76, 1.0), M.coat);

    // Forelock tuft between the ears
    const forelock = loft(V(0, 0.17, -0.03), V(0, 0.14, 0.16), [
      [0, 0.045, 0.032], [0.5, 0.038, 0.026], [1, 0.012, 0.012],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(shadeVerts(forelock, () => 0.82), M.coatDark);

    // Alert pointed equine ears + expressive eyes + nostrils
    for (const sx of [-1, 1]) {
      const ear = loft(V(0, 0, 0), V(0, 0.18, 0), [
        [0.0, 0.036, 0.044],
        [0.4, 0.032, 0.040],
        [0.8, 0.020, 0.024],
        [1.0, 0.006, 0.008],
      ], { segs: 6 });
      mesh(heightShade(ear, 0, 0.18, 0.80, 0.96), M.coat, [sx * 0.082, 0.14, -0.01], [0.22, sx * 0.15, -sx * 0.18]);

      // Dark glossy eye (min=0.12 so it actually stays dark!)
      mesh(shadeVerts(new THREE.SphereGeometry(0.026, 6, 5), () => 0.16, 0.12), M.dark, [sx * 0.128, 0.065, 0.16]);
      // Subtle dark nostril indent
      mesh(shadeVerts(new THREE.SphereGeometry(0.020, 5, 4), () => 0.20, 0.12), M.dark, [sx * 0.068, 0.005, 0.52]);
    }

    // Crisp leather bridle: browband, noseband, cheekpieces, and brass bit rings
    const noseband = loft(V(0, -0.015, 0.43), V(0, -0.015, 0.47), [
      [0, 0.092, 0.104], [1, 0.092, 0.104],
    ], { segs: 10, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(noseband, () => 0.82), M.leather);

    const browband = loft(V(0, 0.02, 0.06), V(0, 0.02, 0.10), [
      [0, 0.152, 0.162], [1, 0.152, 0.162],
    ], { segs: 10, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(browband, () => 0.82), M.leather);

    for (const sx of [-1, 1]) {
      // Cheekpiece strap from browband to bit ring
      const cheek = pathLoft([
        { p: V(sx * 0.135, 0.04, 0.09), rx: 0.012, rz: 0.016 },
        { p: V(sx * 0.105, -0.01, 0.30), rx: 0.012, rz: 0.016 },
        { p: V(sx * 0.088, -0.04, 0.49), rx: 0.012, rz: 0.016 },
      ], { segs: 4, fwd: V(0, 1, 0), caps: false });
      mesh(shadeVerts(cheek, () => 0.82), M.leather);
      // Brass bit ring
      mesh(shadeVerts(new THREE.TorusGeometry(0.032, 0.009, 4, 8), () => 0.98), M.brass, [sx * 0.090, -0.045, 0.50], [0, Math.PI / 2, 0]);
    }

    if (shaffronMat) {
      // Contoured medieval shaffron plate hugging the brow, cheeks, and nasal bridge
      const shaf = loft(V(0, 0.06, 0.02), V(0, 0.01, 0.52), [
        [0.00, 0.152, 0.135, 0, 0.02],
        [0.32, 0.148, 0.132, 0, 0.02],
        [0.70, 0.096, 0.095, 0, 0.01],
        [1.00, 0.080, 0.075, 0, 0.01],
      ], { segs: 8, fwd: V(0, 1, 0), arc: [-Math.PI * 0.15, Math.PI * 1.15], caps: false });
      mesh(heightShade(shaf, -0.02, 0.22, 0.84, 1.0), shaffronMat);
      // Central brass ridge & brow spike
      mesh(shadeVerts(new THREE.ConeGeometry(0.024, 0.11, 6), () => 1.0), M.brass, [0, 0.19, 0.17], [0.85, 0, 0]);
    }
  };
  addPart(root, 'horse_head', (mesh) => buildHorseHead(mesh, null));
  addPart(root, 'horse_head_barded', (mesh) => buildHorseHead(mesh, M.steel));
  addPart(root, 'horse_head_scale', (mesh) => buildHorseHead(mesh, M.mail));

  // Foreleg upper (muscular shoulder/forearm -> neat knee joint, normalized Y 0..1)
  addPart(root, 'horse_leg_up', (mesh) => {
    const leg = loft(V(0, -0.10, 0), V(0, 1.06, 0), [
      [0.00, 0.125, 0.165, 0,  0.01], // muscular upper forearm
      [0.35, 0.105, 0.135, 0,  0.00], // tapered mid-forearm
      [0.78, 0.072, 0.086, 0,  0.00], // above knee
      [1.00, 0.066, 0.078, 0,  0.00], // knee (carpus)
    ], { segs: 8 });
    mesh(heightShade(leg, -0.10, 1.06, 0.76, 1.0), M.coat);
  });

  // Hindleg upper (broad powerful haunch/thigh -> sinewy gaskin -> hock, normalized Y 0..1)
  addPart(root, 'horse_leg_hind_up', (mesh) => {
    const leg = loft(V(0, -0.14, 0), V(0, 1.06, 0), [
      [0.00, 0.155, 0.240, 0, -0.03], // broad muscular thigh/croup
      [0.32, 0.132, 0.195, 0, -0.02], // stifle / upper gaskin
      [0.68, 0.088, 0.120, 0, -0.01], // lean gaskin
      [1.00, 0.066, 0.088, 0, -0.01], // crisp hock joint
    ], { segs: 8 });
    mesh(heightShade(leg, -0.14, 1.06, 0.74, 1.0), M.coat);
  });

  // Lower leg: slender cannon bone -> fetlock bulge -> angled pastern -> flared dark hoof (normalized Y 0..1)
  addPart(root, 'horse_leg_low', (mesh) => {
    const cannon = loft(V(0, -0.08, 0), V(0, 0.85, 0), [
      [0.00, 0.064, 0.076, 0,  0.000], // knee / hock socket
      [0.22, 0.048, 0.056, 0,  0.000], // slender cannon top
      [0.62, 0.046, 0.054, 0,  0.000], // slender cannon lower
      [0.76, 0.056, 0.068, 0, -0.012], // fetlock joint bulge
      [0.92, 0.046, 0.054, 0,  0.016], // angled pastern
      [1.00, 0.054, 0.064, 0,  0.030], // coronet band
    ], { segs: 8 });
    mesh(heightShade(cannon, -0.08, 0.85, 0.74, 1.0), M.coat);

    // Sculpted flared dark hoof (+Z toe)
    const hoof = loft(V(0, 0.83, 0.028), V(0, 1.03, 0.048), [
      [0.00, 0.056, 0.066],
      [0.55, 0.066, 0.080],
      [1.00, 0.074, 0.092],
    ], { segs: 8 });
    mesh(shadeVerts(hoof, (_x, y) => 0.34 + 0.14 * (1.03 - y) / 0.20, 0.22), M.dark);
  });

  // Flowing horse tail (normalized Y 0..1, slightly arched dock -> lush plume -> feathered tip)
  addPart(root, 'horse_tail', (mesh) => {
    const tail = loft(V(0, -0.06, 0), V(0, 1.04, 0), [
      [0.00, 0.048, 0.050, 0,  0.00], // dock root
      [0.18, 0.076, 0.084, 0, -0.04], // arched upper dock
      [0.48, 0.098, 0.106, 0, -0.05], // lush mid plume
      [0.78, 0.082, 0.088, 0, -0.02], // lower plume
      [1.00, 0.022, 0.024, 0,  0.02], // feathered tip
    ], { segs: 7 });
    mesh(heightShade(tail, -0.06, 1.04, 0.68, 1.0), M.coatDark);
  });

  // ---------- CAMELS (3 parts) ----------
  addPart(root, 'camel_body', (mesh) => {
    const body = loft(V(0, 0.04, -0.86), V(0, 0.08, 0.84), [
      [0.00, 0.13, 0.14],
      [0.16, 0.28, 0.28],
      [0.50, 0.32, 0.31],
      [0.84, 0.28, 0.27],
      [1.00, 0.13, 0.14],
    ], { segs: 10, fwd: V(0, 1, 0) });
    mesh(heightShade(body, -0.28, 0.38, 0.78, 1.0), M.coat);
    const hump = column([
      [0.24, 0.26, 0.42, 0, -0.02],
      [0.50, 0.21, 0.32, 0, -0.02],
      [0.74, 0.09, 0.14, 0, -0.02],
    ], { segs: 8 });
    mesh(heightShade(hump, 0.24, 0.74, 0.84, 1.0), M.coat);
    // Contoured woven desert blanket (no flat box!)
    const blanket = drapeSheet([
      { z: -0.40, w: 0.33, topY: 0.42, sideY: 0.20, hemY: 0.02, flare: 0.02 },
      { z:  0.00, w: 0.34, topY: 0.48, sideY: 0.22, hemY: 0.00, flare: 0.02 },
      { z:  0.36, w: 0.32, topY: 0.40, sideY: 0.18, hemY: 0.02, flare: 0.02 },
    ], { segs: 10 });
    mesh(heightShade(blanket, 0.0, 0.48, 0.82, 1.0), M.cloth);
  });

  addPart(root, 'camel_neck', (mesh) => {
    const neck = loft(V(0, -0.08, 0), V(0, 1.06, 0), [
      [0.00, 0.165, 0.20, 0, 0.03],
      [0.35, 0.140, 0.17, 0, 0.09],
      [0.70, 0.120, 0.145, 0, 0.04],
      [1.00, 0.110, 0.130, 0, -0.02],
    ], { segs: 8 });
    mesh(heightShade(neck, -0.08, 1.06, 0.78, 1.0), M.coat);
  });

  addPart(root, 'camel_head', (mesh) => {
    const head = loft(V(0, 0.04, -0.08), V(0, -0.02, 0.53), [
      [0.00, 0.125, 0.125],
      [0.30, 0.135, 0.135],
      [0.65, 0.095, 0.100],
      [1.00, 0.075, 0.082],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(heightShade(head, -0.11, 0.18, 0.80, 1.0), M.coat);
    for (const sx of [-1, 1]) {
      mesh(shadeVerts(new THREE.SphereGeometry(0.032, 5, 4), () => 0.88), M.coat, [sx * 0.095, 0.15, 0.02]);
      mesh(shadeVerts(new THREE.SphereGeometry(0.022, 5, 4), () => 0.16, 0.12), M.dark, [sx * 0.115, 0.055, 0.18]);
    }
    const nose = loft(V(0, -0.01, 0.40), V(0, -0.01, 0.44), [
      [0, 0.095, 0.10], [1, 0.095, 0.10],
    ], { segs: 8, fwd: V(0, 1, 0), caps: false });
    mesh(shadeVerts(nose, () => 0.90), M.cloth);
  });
}
