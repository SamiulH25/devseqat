import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildLimbs(root) {
  addPart(root, 'uarm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.06, 0), V(0, 1.04, 0), [
      [0, 0.068, 0.072],
      [0.2, 0.072, 0.076],
      [0.7, 0.062, 0.066],
      [1, 0.056, 0.060],
    ], { segs: 7 });
    mesh(heightShade(sleeve, -0.06, 1.04, 0.80, 1.0), M.cloth);
  });

  // 8. farm_cloth: normalized bone [0..1] along +Y (elbow -> wrist/hand): tunic sleeve + leather wrist wrap + skin hand!
  addPart(root, 'farm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.05, 0), V(0, 0.62, 0), [
      [0, 0.056, 0.060],
      [0.5, 0.052, 0.056],
      [1, 0.048, 0.052],
    ], { segs: 7 });
    mesh(shadeVerts(sleeve, () => 0.90), M.cloth);

    const wrap = loft(V(0, 0.58, 0), V(0, 0.84, 0), [
      [0, 0.050, 0.054],
      [0.5, 0.046, 0.050],
      [1, 0.042, 0.046],
    ], { segs: 9 });
    mesh(shadeVerts(wrap, () => 0.88), M.leather);

    const hand = loft(V(0, 0.82, 0), V(0, 1.05, 0), [
      [0, 0.038, 0.042],
      [0.5, 0.040, 0.046],
      [1, 0.028, 0.034],
    ], { segs: 8 });
    mesh(shadeVerts(hand, () => 0.96), M.skin);
  });

  // 9. thigh_cloth: normalized bone [0..1] along +Y (hip -> knee)
  addPart(root, 'thigh_cloth', (mesh) => {
    const thigh = loft(V(0, -0.06, 0), V(0, 1.05, 0), [
      [0, 0.088, 0.096],
      [0.45, 0.080, 0.088, 0, 0.004],
      [1, 0.064, 0.068],
    ], { segs: 7 });
    mesh(heightShade(thigh, -0.06, 1.05, 0.82, 1.0), M.clothDark);
  });

  // 10. shin_cloth: normalized bone [0..1] along +Y (knee -> ankle/boot, +Z toe forward!)
  addPart(root, 'shin_cloth', (mesh) => {
    // Upper hose + knee
    const hose = loft(V(0, -0.05, 0), V(0, 0.36, 0), [
      [0, 0.064, 0.068],
      [0.45, 0.062, 0.068, 0, 0.004],
      [1, 0.056, 0.060],
    ], { segs: 7 });
    mesh(shadeVerts(hose, () => 0.90), M.clothDark);

    // Rolled leather boot cuff & shaft
    const boot = loft(V(0, 0.34, 0), V(0, 0.94, 0), [
      [0, 0.066, 0.070],
      [0.14, 0.064, 0.068],
      [0.55, 0.054, 0.058],
      [1, 0.056, 0.064],
    ], { segs: 7 });
    mesh(shadeVerts(boot, (_x, y) => 0.78 + 0.22 * (1 - y)), M.leather);

    // Sculpted foot pointing +Z
    const foot = loft(V(0, 0.95, -0.055), V(0, 0.98, 0.165), [
      [0, 0.048, 0.044],
      [0.35, 0.056, 0.046],
      [0.78, 0.048, 0.032],
      [1, 0.026, 0.020],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(shadeVerts(foot, () => 0.88), M.leather);

    const sole = shadeVerts(new THREE.BoxGeometry(0.104, 0.024, 0.225), () => 0.80);
    mesh(sole, M.dark, [0, 1.015, 0.055]);
  });

  // 11. shield_round: planked round shield with iron rim & steel boss
}
