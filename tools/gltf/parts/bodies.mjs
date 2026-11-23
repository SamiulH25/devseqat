import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildBodies(root) {
  addPart(root, 'chest_tunic', (mesh) => {
    const torso = column([
      [0.79, 0.178, 0.132, 0, 0.004],
      [0.86, 0.168, 0.126, 0, 0.006],
      [0.98, 0.186, 0.136, 0, 0.010],
      [1.14, 0.208, 0.146, 0, 0.012],
      [1.26, 0.214, 0.138, 0, 0.006],
      [1.33, 0.172, 0.116, 0, -0.002],
      [1.37, 0.098, 0.092, 0, -0.004],
    ], { segs: 8 });
    mesh(torso, M.cloth);

    // Rolled collar band around neck
    const collar = column([
      [1.32, 0.106, 0.098, 0, -0.002],
      [1.355, 0.112, 0.104, 0, -0.002],
      [1.385, 0.096, 0.090, 0, -0.004],
    ], { segs: 8 });
    mesh(collar, M.clothDark);

    // Subtle V-neck lacing slit (dark cloth, no stark white block)
    const placket = shadeVerts(new THREE.BoxGeometry(0.032, 0.11, 0.018), () => 0.88);
    mesh(placket, M.clothDark, [0, 1.26, 0.132], [-0.16, 0, 0]);

    // Subtle shoulder cap sleeves (blends arm joint cleanly)
    for (const s of [-1, 1]) {
      const cap = loft(V(s * 0.16, 1.27, 0.0), V(s * 0.245, 1.20, 0.0), [
        [0, 0.088, 0.092],
        [0.5, 0.086, 0.090],
        [1, 0.072, 0.076],
      ], { segs: 7, fwd: V(0, 0, 1) });
      mesh(shadeVerts(cap, () => 0.92), M.cloth);
    }
  });

  // 2. skirt_tunic: flared knee-length tunic skirt with front & side overlap pleats + hem band
  addPart(root, 'skirt_tunic', (mesh) => {
    const skirt = column([
      [0.54, 0.228, 0.168, 0, 0.008],
      [0.60, 0.214, 0.158, 0, 0.008],
      [0.72, 0.194, 0.144, 0, 0.006],
      [0.83, 0.180, 0.134, 0, 0.004],
    ], { segs: 8 });
    mesh(skirt, M.cloth);
    const hem = column([
      [0.535, 0.232, 0.172, 0, 0.008],
      [0.575, 0.224, 0.166, 0, 0.008],
    ], { segs: 8, caps: false });
    mesh(hem, M.clothDark);
  });

  // 3. shld_collar_cloth: draped medieval linen/wool shoulder mantle
  addPart(root, 'shld_collar_cloth', (mesh) => {
    const mantle = column([
      [1.22, 0.248, 0.178, 0, -0.004],
      [1.28, 0.226, 0.162, 0, -0.002],
      [1.35, 0.132, 0.114, 0, -0.002],
      [1.39, 0.102, 0.096, 0, -0.004],
    ], { segs: 8 });
    mesh(mantle, M.clothDark);
  });

  // 4. belt_leather: snug waist belt (fits chest_tunic waist closely!) + brass buckle + hip pouch
  addPart(root, 'belt_leather', (mesh) => {
    const strap = column([
      [0.805, 0.182, 0.138, 0, 0.005],
      [0.855, 0.178, 0.135, 0, 0.006],
    ], { segs: 8 });
    mesh(strap, M.leather);
    const buckle = shadeVerts(new THREE.BoxGeometry(0.052, 0.054, 0.024), () => 1.0);
    mesh(buckle, M.brass, [0, 0.83, 0.140]);
    const pouch = shadeVerts(new THREE.BoxGeometry(0.068, 0.088, 0.044), (_x, y) => 0.85 + y);
    mesh(pouch, M.leather, [-0.178, 0.785, 0.055], [0, -0.3, 0.08]);
  });

  // 5. helm_cap: medieval padded arming cap / woollen cap sitting above the brow, with rolled brim & back ear flaps

  addPart(root, 'chest_brigandine', (mesh) => {
    const body = column([
      [0.79, 0.184, 0.136, 0, 0.004],
      [0.86, 0.174, 0.130, 0, 0.006],
      [0.98, 0.194, 0.142, 0, 0.010],
      [1.14, 0.214, 0.152, 0, 0.012],
      [1.26, 0.218, 0.142, 0, 0.006],
      [1.33, 0.176, 0.120, 0, -0.002],
      [1.37, 0.104, 0.096, 0, -0.004],
    ], { segs: 8 });
    mesh(body, M.cloth);

    // Subtle dark collar & chest seam
    const collar = column([
      [1.32, 0.112, 0.102, 0, -0.002],
      [1.36, 0.116, 0.106, 0, -0.002],
      [1.385, 0.098, 0.092, 0, -0.004],
    ], { segs: 8 });
    mesh(collar, M.leather);

    // Neat vertical brass rivet studs on front & sides
    for (const sx of [-0.11, -0.04, 0.04, 0.11]) {
      for (const y of [0.95, 1.05, 1.15, 1.24]) {
        const stud = shadeVerts(new THREE.BoxGeometry(0.016, 0.016, 0.016), () => 1.0);
        mesh(stud, M.brass, [sx, y, 0.142 - Math.abs(sx) * 0.18]);
      }
    }
  });

  // shld_pauldrons_steel: articulated 3-lame overlapping steel pauldrons + gorget
  addPart(root, 'shld_pauldrons_steel', (mesh) => {
    const gorget = column([
      [1.31, 0.154, 0.128, 0, -0.002],
      [1.36, 0.124, 0.108, 0, -0.002],
      [1.40, 0.102, 0.096, 0, -0.004],
    ], { segs: 8 });
    mesh(gorget, M.mail);

    for (const s of [-1, 1]) {
      // Main rounded shoulder dome
      const dome = loft(V(s * 0.17, 1.33, 0), V(s * 0.29, 1.24, 0), [
        [0.0, 0.085, 0.095],
        [0.5, 0.096, 0.106],
        [1.0, 0.082, 0.090],
      ], { segs: 7, fwd: V(0, 0, 1) });
      mesh(shadeVerts(dome, () => 0.94), M.steel);
      // Two overlapping lower lames
      for (const [k, yOff, xOff] of [[0, -0.07, 0.03], [1, -0.13, 0.05]]) {
        const lame = loft(V(s * (0.21 + xOff), 1.27 + yOff, 0), V(s * (0.29 + xOff), 1.22 + yOff, 0), [
          [0.0, 0.076, 0.084],
          [1.0, 0.068, 0.076],
        ], { segs: 7, fwd: V(0, 0, 1) });
        mesh(shadeVerts(lame, () => 0.88 - k * 0.04), k === 0 ? M.steel : M.mail);
      }
    }
  });

  // helm_kettle: classic chapel-de-fer kettle hat with wide sloped brim, ridged dome & chin-strap
}
