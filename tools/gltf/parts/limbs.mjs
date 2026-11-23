import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildLimbs(root) {
  // --- CLOTH LIMBS ---
  addPart(root, 'uarm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.06, 0), V(0, 1.04, 0), [
      [0, 0.068, 0.072],
      [0.2, 0.072, 0.076],
      [0.7, 0.062, 0.066],
      [1, 0.056, 0.060],
    ], { segs: 7 });
    mesh(heightShade(sleeve, -0.06, 1.04, 0.80, 1.0), M.cloth);
  });

  addPart(root, 'farm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.05, 0), V(0, 0.62, 0), [
      [0, 0.056, 0.060], [0.5, 0.052, 0.056], [1, 0.048, 0.052],
    ], { segs: 7 });
    mesh(shadeVerts(sleeve, () => 0.90), M.cloth);
    const wrap = loft(V(0, 0.58, 0), V(0, 0.84, 0), [
      [0, 0.050, 0.054], [0.5, 0.046, 0.050], [1, 0.042, 0.046],
    ], { segs: 8 });
    mesh(shadeVerts(wrap, () => 0.88), M.leather);
    const hand = loft(V(0, 0.82, 0), V(0, 1.05, 0), [
      [0, 0.038, 0.042], [0.5, 0.040, 0.046], [1, 0.028, 0.034],
    ], { segs: 7 });
    mesh(shadeVerts(hand, () => 0.96), M.skin);
  });

  addPart(root, 'thigh_cloth', (mesh) => {
    const thigh = loft(V(0, -0.06, 0), V(0, 1.05, 0), [
      [0, 0.088, 0.096], [0.45, 0.080, 0.088, 0, 0.004], [1, 0.064, 0.068],
    ], { segs: 7 });
    mesh(heightShade(thigh, -0.06, 1.05, 0.82, 1.0), M.clothDark);
  });

  addPart(root, 'shin_cloth', (mesh) => {
    const hose = loft(V(0, -0.05, 0), V(0, 0.36, 0), [
      [0, 0.064, 0.068], [0.45, 0.062, 0.068, 0, 0.004], [1, 0.056, 0.060],
    ], { segs: 7 });
    mesh(shadeVerts(hose, () => 0.90), M.clothDark);
    const boot = loft(V(0, 0.34, 0), V(0, 0.94, 0), [
      [0, 0.066, 0.070], [0.14, 0.064, 0.068], [0.55, 0.054, 0.058], [1, 0.056, 0.064],
    ], { segs: 7 });
    mesh(shadeVerts(boot, (_x, y) => 0.78 + 0.22 * (1 - y)), M.leather);
    const foot = loft(V(0, 0.95, -0.055), V(0, 0.98, 0.165), [
      [0, 0.048, 0.044], [0.35, 0.056, 0.046], [0.78, 0.048, 0.032], [1, 0.026, 0.020],
    ], { segs: 7, fwd: V(0, 1, 0) });
    mesh(shadeVerts(foot, () => 0.88), M.leather);
    mesh(shadeVerts(new THREE.BoxGeometry(0.104, 0.024, 0.225), () => 0.80), M.dark, [0, 1.015, 0.055]);
  });

  // --- MAIL LIMBS ---
  addPart(root, 'uarm_mail', (mesh) => {
    const sleeve = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.078, 0.082], [0.25, 0.084, 0.088], [0.75, 0.070, 0.074], [1, 0.064, 0.068],
    ], { segs: 7 });
    mesh(heightShade(sleeve, -0.07, 1.05, 0.82, 1.0), M.mail);
    const strap = column([[0.68, 0.074, 0.078], [0.76, 0.074, 0.078]], { segs: 7 });
    mesh(strap, M.leather);
  });

  addPart(root, 'farm_mail', (mesh) => {
    const mail = loft(V(0, -0.06, 0), V(0, 0.72, 0), [
      [0, 0.062, 0.066], [0.5, 0.058, 0.062], [1, 0.052, 0.056],
    ], { segs: 7 });
    mesh(shadeVerts(mail, () => 0.90), M.mail);
    const cuff = loft(V(0, 0.68, 0), V(0, 0.86, 0), [
      [0, 0.056, 0.060], [1, 0.048, 0.052],
    ], { segs: 7 });
    mesh(shadeVerts(cuff, () => 0.86), M.leather);
    const hand = loft(V(0, 0.84, 0), V(0, 1.04, 0), [
      [0, 0.042, 0.046], [0.5, 0.044, 0.048], [1, 0.030, 0.036],
    ], { segs: 7 });
    mesh(shadeVerts(hand, () => 0.90), M.leather);
  });

  addPart(root, 'thigh_mail', (mesh) => {
    const thigh = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.094, 0.100], [0.5, 0.086, 0.092], [1, 0.068, 0.072],
    ], { segs: 7 });
    mesh(heightShade(thigh, -0.07, 1.05, 0.82, 1.0), M.mail);
  });

  addPart(root, 'shin_mail', (mesh) => {
    const chausses = loft(V(0, -0.06, 0), V(0, 0.44, 0), [
      [0, 0.070, 0.074], [0.5, 0.066, 0.070], [1, 0.060, 0.064],
    ], { segs: 7 });
    mesh(shadeVerts(chausses, () => 0.90), M.mail);
    const boot = loft(V(0, 0.42, 0), V(0, 0.94, 0), [
      [0, 0.068, 0.072], [0.5, 0.058, 0.062], [1, 0.058, 0.066],
    ], { segs: 7 });
    mesh(shadeVerts(boot, (_x, y) => 0.80 + 0.20 * (1 - y)), M.leather);
    const foot = loft(V(0, 0.95, -0.055), V(0, 0.98, 0.16), [
      [0, 0.050, 0.044], [0.4, 0.058, 0.046], [0.85, 0.046, 0.030], [1, 0.026, 0.020],
    ], { segs: 7, fwd: V(0, 1, 0) });
    mesh(shadeVerts(foot, () => 0.88), M.leather);
  });

  // --- PLATE LIMBS ---
  addPart(root, 'uarm_plate', (mesh) => {
    const rerebrace = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.088, 0.094], [0.22, 0.096, 0.100], [0.72, 0.076, 0.080], [1, 0.070, 0.074],
    ], { segs: 8 });
    mesh(heightShade(rerebrace, -0.07, 1.05, 0.84, 1.0), M.steel);
    // Elbow couter guard near distal joint
    const couter = shadeVerts(new THREE.SphereGeometry(0.080, 7, 5), () => 0.96);
    couter.scale(1.05, 0.85, 1.12);
    mesh(couter, M.steel, [0, 0.98, -0.015]);
  });

  addPart(root, 'farm_plate', (mesh) => {
    const vambrace = loft(V(0, -0.05, 0), V(0, 0.78, 0), [
      [0, 0.068, 0.072], [0.45, 0.062, 0.066], [1, 0.054, 0.058],
    ], { segs: 8 });
    mesh(heightShade(vambrace, -0.05, 0.78, 0.84, 1.0), M.steel);
    // Flared gauntlet cuff + mittened steel hand
    const cuff = column([[0.64, 0.064, 0.068], [0.78, 0.056, 0.060]], { segs: 8 });
    mesh(cuff, M.steel);
    const gauntlet = loft(V(0, 0.78, 0), V(0, 1.04, 0), [
      [0, 0.048, 0.052], [0.55, 0.050, 0.054], [1, 0.034, 0.038],
    ], { segs: 7 });
    mesh(shadeVerts(gauntlet, () => 0.94), M.steel);
  });

  addPart(root, 'thigh_plate', (mesh) => {
    const cuisse = loft(V(0, -0.07, 0), V(0, 1.05, 0), [
      [0, 0.098, 0.104], [0.45, 0.090, 0.096, 0, 0.008], [1, 0.074, 0.078],
    ], { segs: 8 });
    mesh(heightShade(cuisse, -0.07, 1.05, 0.84, 1.0), M.steel);
    // Poleyn (knee cop) at distal end
    const poleyn = shadeVerts(new THREE.SphereGeometry(0.082, 7, 5), () => 0.98);
    poleyn.scale(1.0, 0.9, 1.1);
    mesh(poleyn, M.steel, [0, 0.99, 0.025]);
  });

  addPart(root, 'shin_plate', (mesh) => {
    const greave = loft(V(0, -0.05, 0), V(0, 0.92, 0), [
      [0, 0.074, 0.080], [0.35, 0.072, 0.082, 0, 0.008], [0.75, 0.060, 0.068], [1, 0.058, 0.068],
    ], { segs: 8 });
    mesh(heightShade(greave, -0.05, 0.92, 0.82, 1.0), M.steel);
    // Pointed gothic steel sabaton (+Z)
    const sabaton = loft(V(0, 0.94, -0.060), V(0, 0.98, 0.185), [
      [0, 0.054, 0.048], [0.35, 0.064, 0.052], [0.75, 0.050, 0.036], [1, 0.020, 0.018],
    ], { segs: 7, fwd: V(0, 1, 0) });
    mesh(shadeVerts(sabaton, () => 0.94), M.steel);
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
