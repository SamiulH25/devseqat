import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildLimbs(root) {
  // --- CLOTH LIMBS ---
  addPart(root, 'uarm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.06, 0), V(0, 1.04, 0), [
      [0, 0.078, 0.082],
      [0.25, 0.082, 0.086],
      [0.72, 0.070, 0.074],
      [1, 0.064, 0.068],
    ], { segs: 6 });
    mesh(heightShade(sleeve, -0.06, 1.04, 0.80, 1.0), M.cloth);
  });

  addPart(root, 'farm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.05, 0), V(0, 0.62, 0), [
      [0, 0.064, 0.068], [0.5, 0.058, 0.062], [1, 0.052, 0.056],
    ], { segs: 6 });
    mesh(shadeVerts(sleeve, () => 0.90), M.cloth);
    const wrap = loft(V(0, 0.56, 0), V(0, 0.84, 0), [
      [0, 0.058, 0.062], [0.5, 0.052, 0.056], [1, 0.048, 0.052],
    ], { segs: 6 });
    mesh(shadeVerts(wrap, () => 0.88), M.leather);
    const hand = loft(V(0, 0.82, 0), V(0, 1.06, 0), [
      [0, 0.044, 0.048], [0.5, 0.048, 0.052], [1, 0.032, 0.038],
    ], { segs: 6 });
    mesh(shadeVerts(hand, () => 0.96), M.skin);
  });

  addPart(root, 'thigh_cloth', (mesh) => {
    const thigh = loft(V(0, -0.06, 0), V(0, 1.05, 0), [
      [0, 0.098, 0.104], [0.45, 0.088, 0.094, 0, 0.004], [1, 0.072, 0.076],
    ], { segs: 6 });
    mesh(heightShade(thigh, -0.06, 1.05, 0.82, 1.0), M.clothDark);
  });

  addPart(root, 'shin_cloth', (mesh) => {
    const hose = loft(V(0, -0.05, 0), V(0, 0.34, 0), [
      [0, 0.072, 0.076], [0.5, 0.068, 0.074, 0, 0.004], [1, 0.062, 0.066],
    ], { segs: 6 });
    mesh(shadeVerts(hose, () => 0.90), M.clothDark);
    // Distinct flared medieval leather boot cuff + shaft
    const cuff = column([[0.28, 0.078, 0.084], [0.42, 0.072, 0.078]], { segs: 6 });
    mesh(shadeVerts(cuff, () => 0.94), M.leather);
    const boot = loft(V(0, 0.38, 0), V(0, 0.94, 0), [
      [0, 0.072, 0.078], [0.55, 0.062, 0.066], [1, 0.064, 0.072],
    ], { segs: 6 });
    mesh(shadeVerts(boot, (_x, y) => 0.78 + 0.22 * (1 - y)), M.leather);
    const foot = loft(V(0, 0.94, -0.060), V(0, 0.97, 0.190), [
      [0, 0.058, 0.050], [0.38, 0.066, 0.054], [0.80, 0.056, 0.038], [1, 0.032, 0.024],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(shadeVerts(foot, () => 0.88), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.122, 0.026, 0.255), () => 0.78), M.dark, [0, 1.015, 0.065]);
  });

  // --- MAIL LIMBS ---
  addPart(root, 'uarm_mail', (mesh) => {
    const sleeve = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.086, 0.090], [0.25, 0.092, 0.096], [0.75, 0.076, 0.080], [1, 0.070, 0.074],
    ], { segs: 6 });
    mesh(heightShade(sleeve, -0.07, 1.05, 0.82, 1.0), M.mail);
    const strap = column([[0.66, 0.082, 0.086], [0.76, 0.082, 0.086]], { segs: 6 });
    mesh(strap, M.leather);
  });

  addPart(root, 'farm_mail', (mesh) => {
    const mail = loft(V(0, -0.06, 0), V(0, 0.72, 0), [
      [0, 0.070, 0.074], [0.5, 0.064, 0.068], [1, 0.058, 0.062],
    ], { segs: 6 });
    mesh(shadeVerts(mail, () => 0.90), M.mail);
    const cuff = loft(V(0, 0.64, 0), V(0, 0.86, 0), [
      [0, 0.066, 0.070], [1, 0.056, 0.060],
    ], { segs: 6 });
    mesh(shadeVerts(cuff, () => 0.86), M.leather);
    const hand = loft(V(0, 0.84, 0), V(0, 1.05, 0), [
      [0, 0.048, 0.052], [0.5, 0.050, 0.054], [1, 0.034, 0.040],
    ], { segs: 6 });
    mesh(shadeVerts(hand, () => 0.90), M.leather);
  });

  addPart(root, 'thigh_mail', (mesh) => {
    const thigh = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.102, 0.108], [0.5, 0.092, 0.098], [1, 0.076, 0.080],
    ], { segs: 6 });
    mesh(heightShade(thigh, -0.07, 1.05, 0.82, 1.0), M.mail);
  });

  addPart(root, 'shin_mail', (mesh) => {
    const chausses = loft(V(0, -0.06, 0), V(0, 0.38, 0), [
      [0, 0.076, 0.080], [0.5, 0.072, 0.076], [1, 0.066, 0.070],
    ], { segs: 6 });
    mesh(shadeVerts(chausses, () => 0.90), M.mail);
    const cuff = column([[0.32, 0.082, 0.086], [0.46, 0.076, 0.080]], { segs: 6 });
    mesh(shadeVerts(cuff, () => 0.94), M.leather);
    const boot = loft(V(0, 0.42, 0), V(0, 0.94, 0), [
      [0, 0.076, 0.080], [0.5, 0.064, 0.068], [1, 0.066, 0.074],
    ], { segs: 6 });
    mesh(shadeVerts(boot, (_x, y) => 0.80 + 0.20 * (1 - y)), M.leather);
    const foot = loft(V(0, 0.94, -0.060), V(0, 0.97, 0.190), [
      [0, 0.058, 0.050], [0.4, 0.068, 0.054], [0.85, 0.054, 0.036], [1, 0.032, 0.024],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(shadeVerts(foot, () => 0.88), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.124, 0.026, 0.255), () => 0.78), M.dark, [0, 1.015, 0.065]);
  });

  // --- PLATE LIMBS ---
  addPart(root, 'uarm_plate', (mesh) => {
    const rerebrace = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.094, 0.100], [0.24, 0.102, 0.108], [0.74, 0.082, 0.086], [1, 0.076, 0.080],
    ], { segs: 6 });
    mesh(heightShade(rerebrace, -0.07, 1.05, 0.84, 1.0), M.steel);
    // Crisp faceted elbow couter guard
    const couter = shadeVerts(new THREE.OctahedronGeometry(0.092, 0), () => 0.98);
    couter.scale(1.05, 0.90, 1.15);
    mesh(couter, M.steel, [0, 0.98, -0.015]);
  });

  addPart(root, 'farm_plate', (mesh) => {
    const vambrace = loft(V(0, -0.05, 0), V(0, 0.78, 0), [
      [0, 0.076, 0.080], [0.45, 0.068, 0.072], [1, 0.060, 0.064],
    ], { segs: 6 });
    mesh(heightShade(vambrace, -0.05, 0.78, 0.84, 1.0), M.steel);
    // Flared faceted gauntlet cuff + steel mitten hand
    const cuff = column([[0.60, 0.074, 0.078], [0.78, 0.062, 0.066]], { segs: 6 });
    mesh(cuff, M.steel);
    const gauntlet = loft(V(0, 0.78, 0), V(0, 1.05, 0), [
      [0, 0.054, 0.058], [0.55, 0.056, 0.060], [1, 0.038, 0.042],
    ], { segs: 6 });
    mesh(shadeVerts(gauntlet, () => 0.96), M.steel);
  });

  addPart(root, 'thigh_plate', (mesh) => {
    const cuisse = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.106, 0.112], [0.45, 0.096, 0.102, 0, 0.008], [1, 0.080, 0.084],
    ], { segs: 6 });
    mesh(heightShade(cuisse, -0.07, 1.05, 0.84, 1.0), M.steel);
    // Crisp beveled faceted poleyn (knee cop)
    const poleyn = shadeVerts(new THREE.OctahedronGeometry(0.096, 0), () => 1.0);
    poleyn.scale(1.0, 0.95, 1.15);
    mesh(poleyn, M.steel, [0, 0.99, 0.032]);
  });

  addPart(root, 'shin_plate', (mesh) => {
    const greave = loft(V(0, -0.05, 0), V(0, 0.92, 0), [
      [0, 0.082, 0.088], [0.35, 0.080, 0.090, 0, 0.010], [0.75, 0.066, 0.074], [1, 0.066, 0.076],
    ], { segs: 6 });
    mesh(heightShade(greave, -0.05, 0.92, 0.82, 1.0), M.steel);
    // Bold faceted gothic steel sabaton (+Z)
    const sabaton = loft(V(0, 0.94, -0.065), V(0, 0.97, 0.210), [
      [0, 0.062, 0.054], [0.35, 0.072, 0.058], [0.75, 0.058, 0.040], [1, 0.026, 0.022],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(shadeVerts(sabaton, () => 0.96), M.steel);
  });

  // --- SKELETON / BONE LIMBS ---
  function buildBoneLimb(name, rShaft, rJoint) {
    addPart(root, name, (mesh) => {
      const bone = loft(V(0, 0.0, 0), V(0, 1.0, 0), [
        [0.0, rJoint, rJoint],
        [0.14, rShaft, rShaft],
        [0.50, rShaft * 0.88, rShaft * 0.88],
        [0.86, rShaft, rShaft],
        [1.0, rJoint, rJoint],
      ], { segs: 6 });
      mesh(heightShade(bone, 0.0, 1.0, 0.84, 1.0), M.skin);
    });
  }
  buildBoneLimb('uarm_bones', 0.026, 0.042);
  buildBoneLimb('farm_bones', 0.024, 0.038);
  buildBoneLimb('thigh_bones', 0.034, 0.054);
  buildBoneLimb('shin_bones', 0.030, 0.050);
}
