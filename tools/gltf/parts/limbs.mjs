import { THREE, V, M, shadeVerts, heightShade, loft, pathLoft, column, addPart } from '../geom.mjs';

// Limb parts are posed by Puppet3D.EmitBone: local +Y runs joint->joint scaled by the bone length
// (thigh/shin 0.34, upper arm 0.29, forearm 0.28) while local X/Z stay in world units. So anything
// vertical (a boot foot's thickness!) needs roughly 3x the local measure to read right in the world.
// Shin and forearm parts are double-duty: at mid LOD the bone stretches over the whole leg (hip->foot)
// or arm (shoulder->hand), so each reads as a complete limb on its own: hose/chausses up top, boot below.

// Snap every vertex below yPlane up to it: gives boots and sabatons a crisp flat sole edge.
function flattenSole(geo, yPlane) {
  const p = geo.attributes.position;
  for (let i = 0; i < p.count; i++) if (p.getY(i) > yPlane) p.setY(i, yPlane);
  return geo;
}

/**
 * A boot/sabaton foot along +Z, ankle joint at local y=1 (the bone end, ground level).
 * Rings are [z, rx(width, world units), rz(half height, compressed local units), cy] sweeping the toe.
 * The sole is flattened at ySole so the foot plants flat; the heel cups behind the ankle at z<0.
 */
function footShape(rings, { segs = 7, ySole = 1.004, shade = 0.9 } = {}) {
  const stations = rings.map(([z, rx, rz, cy]) => ({ p: V(0, cy, z), rx, rz }));
  const g = pathLoft(stations, { segs, fwd: V(0, 1, 0) });
  flattenSole(g, ySole);
  return shadeVerts(g, () => shade);
}

// The medieval leather boot: flared cuff fold, shaped shaft, ankle, flat-soled foot with a blunt toe.
// Returns the sole slab depth so callers can drop the dark sole under it.
function leatherBoot(mesh, { cuffShade = 0.94, bootShade = 0.86, toe = 0.19, wide = 0 } = {}) {
  // Folded cuff: a short flared funnel sitting proud of the shaft.
  const cuff = column([[0.26, 0.086 + wide, 0.092 + wide], [0.33, 0.078 + wide, 0.084 + wide], [0.42, 0.072 + wide, 0.078 + wide]], { segs: 7 });
  mesh(shadeVerts(cuff, () => cuffShade), M.leather);
  // Shaft: slim at the calf, nipping in at the ankle, opening into the foot.
  const shaft = loft(V(0, 0.36, 0), V(0, 1.0, 0), [
    [0, 0.072 + wide, 0.078 + wide],
    [0.35, 0.066 + wide, 0.070 + wide],
    [0.72, 0.062 + wide, 0.066 + wide],
    [1, 0.066 + wide, 0.070 + wide],
  ], { segs: 7 });
  mesh(shadeVerts(shaft, (_x, y) => 0.80 + 0.20 * (1 - y)), M.leather);
  // Foot: heel cup behind the ankle, instep rising, broad ball, blunt slightly upturned toe.
  const foot = footShape([
    [-0.058, 0.052, 0.094, 0.910],
    [-0.008, 0.058, 0.108, 0.894],
    [0.060, 0.062, 0.110, 0.890],
    [0.125, 0.056, 0.098, 0.900],
    [toe, 0.032, 0.068, 0.936],
  ], { shade: bootShade });
  mesh(foot, M.leather);
  // Dark sole slab, a touch wider than the foot so the welt reads at distance.
  const sole = new THREE.BoxGeometry(0.122 + wide, 0.030, 0.235);
  mesh(shadeVerts(sole, () => 0.78), M.dark, [0, 1.0, 0.062]);
}

// A glove/gauntlet hand past the wrist (y=1): a flattened slightly tapering mitten with a rounded end.
function handShape(mat, shade = 0.94, wide = 0) {
  return shadeVerts(loft(V(0, 0.84, 0), V(0, 1.09, 0), [
    [0, 0.048 + wide, 0.042 + wide * 0.6],
    [0.45, 0.054 + wide, 0.044 + wide * 0.6],
    [0.8, 0.048 + wide, 0.040 + wide * 0.6],
    [1, 0.034 + wide * 0.6, 0.030 + wide * 0.5],
  ], { segs: 7 }), () => shade);
}

// A faceted cop shell for knees and elbows. EmitBone squashes local Y by the bone length, so the shell
// is built prolate (tall in local Y) to come out round in the world; its cap then wraps the joint.
function copShell(r, boneLen) {
  const cop = shadeVerts(new THREE.SphereGeometry(r, 7, 5), () => 1.0);
  cop.scale(1.04, 0.95 / boneLen, 1.10);
  return cop;
}

export function buildLimbs(root) {
  // --- CLOTH LIMBS ---
  addPart(root, 'uarm_cloth', (mesh) => {
    // Loose wool sleeve, a touch of padding fullness high on the arm.
    const sleeve = loft(V(0, -0.06, 0), V(0, 1.05, 0), [
      [0, 0.080, 0.084],
      [0.28, 0.086, 0.090],
      [0.62, 0.074, 0.078],
      [1, 0.066, 0.070],
    ], { segs: 7 });
    mesh(heightShade(sleeve, -0.06, 1.05, 0.80, 1.0), M.cloth);
  });

  addPart(root, 'farm_cloth', (mesh) => {
    const sleeve = loft(V(0, -0.05, 0), V(0, 0.64, 0), [
      [0, 0.066, 0.070], [0.5, 0.060, 0.064], [1, 0.054, 0.058],
    ], { segs: 7 });
    mesh(shadeVerts(sleeve, () => 0.90), M.cloth);
    // Leather bracer over the forearm, slightly barrelled with a flare toward the wrist.
    const bracer = loft(V(0, 0.54, 0), V(0, 0.87, 0), [
      [0, 0.062, 0.066], [0.45, 0.058, 0.062], [1, 0.054, 0.058],
    ], { segs: 7 });
    mesh(shadeVerts(bracer, () => 0.88), M.leather);
    mesh(handShape(M.skin, 0.96), M.skin);
  });

  addPart(root, 'thigh_cloth', (mesh) => {
    // Close hose with a hint of thigh fullness and a soft knee.
    const thigh = loft(V(0, -0.06, 0), V(0, 1.06, 0), [
      [0, 0.100, 0.106],
      [0.42, 0.090, 0.096, 0, 0.004],
      [0.86, 0.078, 0.084, 0, 0.006],
      [1, 0.074, 0.080],
    ], { segs: 7 });
    mesh(heightShade(thigh, -0.06, 1.06, 0.82, 1.0), M.clothDark);
  });

  addPart(root, 'shin_cloth', (mesh) => {
    const hose = loft(V(0, -0.05, 0), V(0, 0.36, 0), [
      [0, 0.074, 0.078], [0.5, 0.070, 0.076, 0, 0.004], [1, 0.064, 0.068],
    ], { segs: 7 });
    mesh(shadeVerts(hose, () => 0.90), M.clothDark);
    leatherBoot(mesh);
  });

  // --- MAIL LIMBS ---
  addPart(root, 'uarm_mail', (mesh) => {
    // Loose hauberk sleeve, a leather strap buckled below the elbow.
    const sleeve = loft(V(0, -0.07, 0), V(0, 1.06, 0), [
      [0, 0.088, 0.092],
      [0.26, 0.094, 0.098],
      [0.72, 0.078, 0.082],
      [1, 0.072, 0.076],
    ], { segs: 7 });
    mesh(heightShade(sleeve, -0.07, 1.06, 0.82, 1.0), M.mail);
    const strap = column([[0.64, 0.084, 0.088], [0.75, 0.082, 0.086]], { segs: 7 });
    mesh(strap, M.leather);
  });

  addPart(root, 'farm_mail', (mesh) => {
    const mail = loft(V(0, -0.06, 0), V(0, 0.72, 0), [
      [0, 0.072, 0.076], [0.5, 0.066, 0.070], [1, 0.060, 0.064],
    ], { segs: 7 });
    mesh(shadeVerts(mail, () => 0.90), M.mail);
    // Leather cuff and a heavy leather glove.
    const cuff = loft(V(0, 0.62, 0), V(0, 0.88, 0), [
      [0, 0.068, 0.072], [0.6, 0.060, 0.064], [1, 0.056, 0.060],
    ], { segs: 7 });
    mesh(shadeVerts(cuff, () => 0.86), M.leather);
    mesh(handShape(M.leather, 0.90), M.leather);
  });

  addPart(root, 'thigh_mail', (mesh) => {
    const thigh = loft(V(0, -0.07, 0), V(0, 1.06, 0), [
      [0, 0.104, 0.110],
      [0.45, 0.094, 0.100],
      [0.86, 0.082, 0.088, 0, 0.004],
      [1, 0.078, 0.084],
    ], { segs: 7 });
    mesh(heightShade(thigh, -0.07, 1.06, 0.82, 1.0), M.mail);
  });

  addPart(root, 'shin_mail', (mesh) => {
    const chausses = loft(V(0, -0.06, 0), V(0, 0.38, 0), [
      [0, 0.078, 0.082], [0.5, 0.074, 0.078], [1, 0.068, 0.072],
    ], { segs: 7 });
    mesh(shadeVerts(chausses, () => 0.90), M.mail);
    leatherBoot(mesh, { wide: 0.004 });
  });

  // --- PLATE LIMBS ---
  addPart(root, 'uarm_plate', (mesh) => {
    // Rerebrace: a crisp steel tube flaring at the pauldron and pinching before the elbow.
    const rerebrace = loft(V(0, -0.07, 0), V(0, 1.04, 0), [
      [0, 0.096, 0.102],
      [0.22, 0.104, 0.110],
      [0.68, 0.084, 0.088],
      [0.92, 0.078, 0.082],
      [1, 0.084, 0.088],
    ], { segs: 7 });
    mesh(heightShade(rerebrace, -0.07, 1.04, 0.84, 1.0), M.steel);
    // Couter: a bright faceted round cop cupping the elbow point (upper arm bone is 0.29 long).
    mesh(copShell(0.096, 0.29), M.steel, [0, 0.96, -0.046]);
  });

  addPart(root, 'farm_plate', (mesh) => {
    // Vambrace tapering to the wrist.
    const vambrace = loft(V(0, -0.05, 0), V(0, 0.74, 0), [
      [0, 0.078, 0.082], [0.45, 0.070, 0.074], [1, 0.062, 0.066],
    ], { segs: 7 });
    mesh(heightShade(vambrace, -0.05, 0.74, 0.84, 1.0), M.steel);
    // Bold flared gauntlet cuff: the bell mouth reads at any distance.
    const cuff = loft(V(0, 0.60, 0), V(0, 0.90, 0), [
      [0, 0.064, 0.068], [0.55, 0.070, 0.074], [1, 0.088, 0.092],
    ], { segs: 7 });
    mesh(cuff, M.steel);
    // Steel mitten: broad knuckle plate, tapered fingers.
    mesh(handShape(M.steel, 0.96, 0.006), M.steel);
  });

  addPart(root, 'thigh_plate', (mesh) => {
    // Cuisse with two articulating lames stepping down toward the knee.
    const cuisse = loft(V(0, -0.07, 0), V(0, 0.92, 0), [
      [0, 0.108, 0.114], [0.5, 0.098, 0.104, 0, 0.008], [1, 0.084, 0.090],
    ], { segs: 7 });
    mesh(heightShade(cuisse, -0.07, 0.92, 0.84, 1.0), M.steel);
    const lame1 = column([[0.84, 0.086, 0.092], [0.93, 0.084, 0.090]], { segs: 7 });
    mesh(lame1, M.steel);
    const lame2 = column([[0.92, 0.087, 0.093], [1.0, 0.085, 0.091]], { segs: 7 });
    mesh(lame2, M.steel);
    // Poleyn: a round shell capping the kneecap, proud of the lames (thigh bone is 0.34 long).
    mesh(copShell(0.092, 0.34), M.steel, [0, 0.97, 0.048]);
  });

  addPart(root, 'shin_plate', (mesh) => {
    // Greave: calf bulge high at the back, clean ridge line, closing on the ankle.
    const greave = loft(V(0, -0.05, 0), V(0, 0.96, 0), [
      [0, 0.086, 0.094],
      [0.3, 0.084, 0.100, 0, -0.014],
      [0.65, 0.070, 0.076],
      [0.92, 0.064, 0.070],
      [1, 0.068, 0.074],
    ], { segs: 7 });
    mesh(heightShade(greave, -0.05, 0.96, 0.82, 1.0), M.steel);
    // Sabaton: a bright ridged steel foot, narrower than the leather boot, running to a moderate point.
    const sabaton = footShape([
      [-0.060, 0.050, 0.096, 0.912],
      [-0.005, 0.056, 0.110, 0.894],
      [0.065, 0.058, 0.112, 0.890],
      [0.135, 0.050, 0.100, 0.900],
      [0.200, 0.024, 0.060, 0.940],
    ], { shade: 0.96 });
    mesh(sabaton, M.steel);
    // One lame seam across the instep where the greave meets the foot.
    const lame = column([[0.85, 0.066, 0.072], [0.94, 0.068, 0.074]], { segs: 7 });
    mesh(lame, M.steel);
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
