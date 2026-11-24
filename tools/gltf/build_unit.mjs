// A man-at-arms of the Azure Crown built in three.js and written as glTF binary (.glb).
//   cd tools/gltf && npm install && node build_unit.mjs [out.glb]
// Metres, +Y up, the man faces +Z, feet at the origin; his right hand is on world -X (as Puppet3D's frame puts it).
// Shapes are lofts (rings of ellipses along a bone), lathes and bent extrusions, smooth shaded,
// with a baked height shade in the vertex colours. Materials carry extras.materialId (Mesh3D.cs ids)
// so team cloth (1, 2, 3) could be recoloured per side. Parts sit under named groups (legs, torso, head, arms, kit).
import * as THREE from 'three';
import { resolve } from 'node:path';
import { rgb, Palette, writeGlb, triangles } from './lib.mjs';

const out = resolve(process.argv[2] ?? 'out/man_at_arms.glb');
const V = (x, y, z) => new THREE.Vector3(x, y, z);

const mat = (name, id, metal = 0, rough = 0.85, extra = {}) => {
  const m = new THREE.MeshStandardMaterial({ name, color: rgb(...Palette[id]), metalness: metal, roughness: rough, vertexColors: true, ...extra });
  m.userData.materialId = id;
  return m;
};
const M = {
  cloth: mat('cloth', 1, 0, 0.9, { side: THREE.DoubleSide }),
  clothDark: mat('cloth_dark', 2),
  trim: mat('trim', 3, 0, 0.8, { side: THREE.DoubleSide }),
  steel: mat('steel', 4, 0.9, 0.32, { side: THREE.DoubleSide }),
  mail: mat('mail', 5, 0.6, 0.45, { side: THREE.DoubleSide }),
  leather: mat('leather', 7, 0, 0.7),
  wood: mat('wood', 8, 0, 0.75),
  skin: mat('skin', 9, 0, 0.7),
  hair: mat('hair', 10, 0, 0.9),
  dark: mat('sole', 13, 0, 0.9),
};

// ---------- shape helpers ----------

/** Rings of ellipses along the bone a -> b. Ring: [t (0..1 along), rx (across), rz (toward fwd), ox, oz].
 *  arc = [from, to] radians keeps part of each ring (a panel); otherwise the tube is closed and capped. */
function loft(a, b, rings, { segs = 12, fwd = V(0, 0, 1), arc = null, caps = true } = {}) {
  const d = b.clone().sub(a), len = d.length(), uy = d.clone().divideScalar(len);
  let ux = new THREE.Vector3().crossVectors(uy, fwd);
  if (ux.lengthSq() < 1e-6) ux.crossVectors(uy, V(1, 0, 0));
  ux.normalize();
  const uz = new THREE.Vector3().crossVectors(ux, uy).normalize();
  const open = !!arc, n = open ? segs + 1 : segs;
  const pos = [], idx = [];
  for (const [t, rx, rz, ox = 0, oz = 0] of rings) {
    const c = a.clone().addScaledVector(uy, t * len).addScaledVector(ux, ox).addScaledVector(uz, oz);
    for (let i = 0; i < n; i++) {
      const th = open ? arc[0] + (arc[1] - arc[0]) * i / segs : -Math.PI / 2 + 2 * Math.PI * i / segs;
      const p = c.clone().addScaledVector(ux, Math.cos(th) * rx).addScaledVector(uz, Math.sin(th) * rz);
      pos.push(p.x, p.y, p.z);
    }
  }
  for (let r = 0; r + 1 < rings.length; r++)
    for (let i = 0; i < (open ? segs : n); i++) {
      const i2 = open ? i + 1 : (i + 1) % n, A = r * n + i, B = r * n + i2, C = (r + 1) * n + i, D = (r + 1) * n + i2;
      idx.push(A, B, D, A, D, C);
    }
  if (caps && !open) for (const [r, flip] of [[0, true], [rings.length - 1, false]]) {
    const [t, , , ox = 0, oz = 0] = rings[r];
    const c = a.clone().addScaledVector(uy, t * len).addScaledVector(ux, ox).addScaledVector(uz, oz);
    const ci = pos.length / 3;
    pos.push(c.x, c.y, c.z);
    for (let i = 0; i < n; i++) {
      const p = r * n + i, q = r * n + (i + 1) % n;
      flip ? idx.push(ci, q, p) : idx.push(ci, p, q);
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setIndex(idx);
  g.computeVertexNormals();
  return g;
}
/** A loft standing on the Y axis with rings given by absolute height: [y, rx, rz, ox, oz]. */
const column = (rings, opts) => {
  const y0 = rings[0][0], y1 = rings[rings.length - 1][0];
  return loft(V(0, y0, 0), V(0, y1, 0), rings.map(([y, ...r]) => [(y - y0) / (y1 - y0), ...r]), opts);
};
/** Lathe of [r, y] points about Y. */
const lathe = (pts, segs = 20) => new THREE.LatheGeometry(pts.map(([r, y]) => new THREE.Vector2(r, y)), segs);
/** Bend a flat (XY) piece round a vertical cylinder of radius R so its face stays on a curved surface. */
function bend(g, R) {
  const p = g.attributes.position;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i), z = p.getZ(i), a = x / R, r = R + z;
    p.setXYZ(i, Math.sin(a) * r, p.getY(i), Math.cos(a) * r - R);
  }
  g.computeVertexNormals();
  return g;
}
/** Bend a flat piece onto the front of an ellipse (rx, rz) centred at depth cz. */
function wrap(g, rx, rz, cz) {
  const p = g.attributes.position;
  for (let i = 0; i < p.count; i++) {
    const x = p.getX(i), k = Math.max(0, 1 - (x / rx) ** 2);
    p.setZ(i, p.getZ(i) + cz + rz * Math.sqrt(k));
  }
  g.computeVertexNormals();
  return g;
}
/** Rotate a geometry so +Y points along dir. */
const along = (g, dir) => g.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(V(0, 1, 0), dir.clone().normalize()));
/** Two-bone arm: the elbow for a shoulder and hand, bent toward pole (Puppet3D.Elbow). */
function elbow(sh, hand, pole, up = 0.3, fore = 0.28) {
  const d = hand.clone().sub(sh), len = Math.min(d.length(), (up + fore) * 0.999), axis = d.normalize();
  const alongLen = (up * up - fore * fore + len * len) / (2 * len), h = Math.sqrt(Math.max(0, up * up - alongLen * alongLen));
  const b = pole.clone().addScaledVector(axis, -pole.dot(axis)).normalize();
  return sh.clone().addScaledVector(axis, alongLen).addScaledVector(b, h);
}

// ---------- assembling ----------

const unit = new THREE.Group();
unit.name = 'man_at_arms';
unit.userData = { troop: 'demo.man_at_arms', palette: 'Azure', units: 'metres', forward: '+Z', builder: 'three.js' };
const group = (name, parent = unit) => { const g = new THREE.Group(); g.name = name; parent.add(g); return g; };
function part(parent, name, geo, material, at = null) {
  if (at) geo.translate(at.x, at.y, at.z);
  const m = new THREE.Mesh(geo, material);
  m.name = name;
  parent.add(m);
  return m;
}

// Legs: hose, knees, boots with a foot and sole. Weight on the right leg, the left eased out.
const legs = group('legs');
for (const [side, s] of [['r', -1], ['l', 1]]) {
  const hip = V(s * 0.095, 0.94, 0), knee = V(s * (s > 0 ? 0.12 : 0.105), 0.51, s > 0 ? 0.03 : 0.015);
  const ankle = V(s * (s > 0 ? 0.14 : 0.11), 0.1, s > 0 ? 0.01 : -0.005);
  const kneeHi = knee.clone().add(V(0, 0.03, 0));
  part(legs, `thigh_${side}`, loft(hip, kneeHi, [[0, 0.1, 0.104], [0.45, 0.094, 0.098, 0, 0.008], [1, 0.07, 0.074]], { segs: 9 }), M.clothDark);
  part(legs, `shin_${side}`, loft(knee.clone().add(V(0, 0.04, 0)), knee.clone().lerp(ankle, 0.62), [[0, 0.072, 0.076], [0.15, 0.074, 0.08, 0, 0.004], [0.55, 0.064, 0.068, 0, -0.002], [1, 0.052, 0.054]], { segs: 9 }), M.clothDark);
  const boot0 = V(ankle.x, 0.05, ankle.z - 0.01), boot1 = V(ankle.x, 0.37, ankle.z);
  part(legs, `boot_${side}`, loft(boot0, boot1, [[0, 0.064, 0.074], [0.3, 0.06, 0.066], [0.84, 0.07, 0.074], [0.85, 0.082, 0.086], [1, 0.084, 0.088]], { segs: 8 }), M.leather);
  const heel = V(ankle.x, 0.048, ankle.z - 0.07), toe = V(ankle.x + s * 0.02, 0.038, ankle.z + 0.17);
  part(legs, `foot_${side}`, loft(heel, toe, [[0, 0.05, 0.048], [0.3, 0.06, 0.052], [0.75, 0.054, 0.036, 0, -0.008], [1, 0.03, 0.022, 0, -0.016]], { segs: 7, fwd: V(0, 1, 0) }), M.leather);
  part(legs, `sole_${side}`, new THREE.BoxGeometry(0.12, 0.014, 0.26), M.dark, V(ankle.x + s * 0.01, 0.007, ankle.z + 0.045));
}

// Torso: a mail hauberk, its skirt, the tabard over it front and back with a trimmed hem and the chevron, a belt.
const torso = group('torso');
part(torso, 'hauberk', column([[0.96, 0.172, 0.13], [1.08, 0.166, 0.125], [1.2, 0.18, 0.135, 0, 0.01], [1.32, 0.205, 0.142, 0, 0.014],
  [1.4, 0.215, 0.134], [1.45, 0.18, 0.11], [1.49, 0.075, 0.07]], { segs: 12 }), M.mail);
part(torso, 'hauberk_skirt', column([[1.0, 0.184, 0.14], [0.88, 0.21, 0.162], [0.74, 0.228, 0.176]], { segs: 14, caps: false }), M.mail);
const tabardRings = [[0.7, 0.232, 0.184], [0.86, 0.215, 0.17], [0.98, 0.188, 0.146], [1.06, 0.182, 0.14], [1.2, 0.194, 0.147, 0, 0.012],
  [1.32, 0.217, 0.154, 0, 0.016], [1.42, 0.225, 0.142], [1.46, 0.18, 0.114]];
const half = 1.0;
part(torso, 'tabard_front', column(tabardRings, { segs: 9, arc: [Math.PI / 2 - half, Math.PI / 2 + half] }), M.cloth);
part(torso, 'tabard_back', column(tabardRings, { segs: 9, arc: [-Math.PI / 2 - half, -Math.PI / 2 + half] }), M.cloth);
for (const [name, c] of [['front', Math.PI / 2], ['back', -Math.PI / 2]])
  part(torso, `tabard_hem_${name}`, column([[0.69, 0.236, 0.188], [0.725, 0.232, 0.184]], { segs: 9, arc: [c - half, c + half] }), M.trim);
const chev = new THREE.Shape();
chev.moveTo(-0.13, -0.02); chev.lineTo(0, 0.1); chev.lineTo(0.13, -0.02); chev.lineTo(0.13, -0.075);
chev.lineTo(0, 0.045); chev.lineTo(-0.13, -0.075); chev.closePath();
part(torso, 'chevron', wrap(new THREE.ExtrudeGeometry(chev, { depth: 0.004, bevelEnabled: false }), 0.2, 0.151, 0.016), M.trim, V(0, 1.24, 0.002));
part(torso, 'belt', column([[0.985, 0.198, 0.156], [1.035, 0.194, 0.152]], { segs: 14 }), M.leather);
part(torso, 'buckle', wrap(new THREE.BoxGeometry(0.05, 0.045, 0.012), 0.2, 0.158, 0.0), M.steel, V(0, 1.01, 0.004));
part(torso, 'pouch', loft(V(-0.17, 0.88, 0.09), V(-0.18, 1.0, 0.1), [[0, 0.03, 0.022], [0.3, 0.052, 0.034], [1, 0.05, 0.03]], { segs: 10 }), M.leather);

// Sword on the left hip: scabbard, chape, guard, grip and pommel.
const sword = group('sword', torso);
const top = V(0.2, 0.99, 0.08), tip = V(0.25, 0.2, -0.09), sd = tip.clone().sub(top).normalize();
part(sword, 'scabbard', loft(top, tip, [[0, 0.026, 0.012], [0.9, 0.022, 0.01], [1, 0.012, 0.008]], { segs: 8, fwd: V(1, 0, 0.2) }), M.leather);
part(sword, 'chape', loft(tip.clone().addScaledVector(sd, -0.07), tip.clone().addScaledVector(sd, 0.005), [[0, 0.024, 0.012], [1, 0.012, 0.009]], { segs: 8, fwd: V(1, 0, 0.2) }), M.steel);
const guardAt = top.clone().addScaledVector(sd, -0.015);
part(sword, 'guard', loft(guardAt.clone().add(V(-0.0, 0, -0.09)), guardAt.clone().add(V(0, 0, 0.09)), [[0, 0.008, 0.008], [0.5, 0.012, 0.012], [1, 0.008, 0.008]], { segs: 6, fwd: V(0, 1, 0) }), M.steel);
const pommel = top.clone().addScaledVector(sd, -0.2);
part(sword, 'grip', loft(guardAt, pommel, [[0, 0.014, 0.014], [0.5, 0.016, 0.016], [1, 0.013, 0.013]], { segs: 8 }), M.leather);
part(sword, 'pommel', new THREE.SphereGeometry(0.026, 6, 4).scale(1, 1, 0.6), M.steel, pommel);

// Head: neck, face, nose, eyes, brows, beard; a mail coif with the face open and a mantle on the shoulders; the kettle hat.
const head = group('head');
const hc = V(0, 1.665, 0.012), HS = 1.13;
part(head, 'neck', loft(V(0, 1.46, 0), V(0, 1.6, 0.01), [[0, 0.06, 0.058], [1, 0.05, 0.05]], { segs: 10 }), M.skin);
part(head, 'face', new THREE.SphereGeometry(0.1 * HS, 12, 9).scale(0.9, 1.1, 0.98), M.skin, hc);
part(head, 'jaw', new THREE.SphereGeometry(0.07 * HS, 10, 6).scale(1, 0.75, 0.9), M.skin, hc.clone().add(V(0, -0.066, 0.012)));
part(head, 'nose', along(new THREE.ConeGeometry(0.019, 0.055, 4), V(0, -0.45, 1)), M.skin, hc.clone().add(V(0, -0.004, 0.124)));
for (const s of [-1, 1]) {
  part(head, 'eye', new THREE.SphereGeometry(0.012, 6, 4).scale(1, 1.1, 0.5), M.dark, hc.clone().add(V(s * 0.037, 0.02, 0.112)));
  part(head, 'brow', new THREE.BoxGeometry(0.042, 0.013, 0.014).rotateZ(s * -0.18), M.hair, hc.clone().add(V(s * 0.038, 0.04, 0.114)));
}
part(head, 'mouth', wrap(new THREE.BoxGeometry(0.036, 0.006, 0.004), 0.09, 0.105, 0.0), M.dark, V(0, 1.618, hc.z));
part(head, 'coif', new THREE.SphereGeometry(0.136, 12, 8, Math.PI / 2 + 0.95, Math.PI * 2 - 1.9, 0, Math.PI * 0.8), M.mail, hc.clone().add(V(0, 0.008, -0.01)));
part(head, 'mantle', column([[1.355, 0.262, 0.18], [1.4, 0.254, 0.174], [1.46, 0.2, 0.146], [1.52, 0.138, 0.122], [1.58, 0.122, 0.118]], { segs: 14, caps: false }), M.mail);
const hat = group('kettle_hat', head);
const hatAt = V(0, 1.722, 0.008), HH = 1.12, HY = 1.3;
part(hat, 'hat_dome', lathe([[0.0001, 0.118], [0.06, 0.11], [0.1, 0.085], [0.12, 0.05], [0.126, 0.02],
  [0.15, 0.012], [0.2, -0.008], [0.224, -0.022], [0.218, -0.028], [0.15, 0.004], [0.118, 0.01]].map(([r, y]) => [r * HH, y * HH * (y > 0.015 ? HY : 1)]), 14), M.steel, hatAt);
part(hat, 'hat_band', new THREE.TorusGeometry(0.1265 * HH, 0.006, 3, 14).rotateX(Math.PI / 2), M.mail, hatAt.clone().add(V(0, 0.03 * HH, 0)));
part(hat, 'hat_comb', new THREE.BoxGeometry(0.008, 0.012, 0.2), M.steel, hatAt.clone().add(V(0, 0.118 * HH * HY, 0)));
for (let i = 0; i < 8; i++) {
  const a = (i + 0.5) / 8 * Math.PI * 2;
  part(hat, 'rivet', new THREE.OctahedronGeometry(0.0065), M.steel, hatAt.clone().add(V(Math.sin(a) * 0.128 * HH, 0.03 * HH, Math.cos(a) * 0.128 * HH)));
}

// Arms: mail sleeves to a cuffed glove. The right holds the spear upright beside him, the left carries the shield forward.
const shoulder = s => V(s * 0.228, 1.4, -0.005);
function arm(side, s, hand, pole, handDir) {
  const g = group(`arm_${side}`), sh = shoulder(s), el = elbow(sh, hand, pole);
  part(g, `shoulder_${side}`, new THREE.SphereGeometry(0.082, 8, 5), M.mail, sh);
  part(g, `upper_arm_${side}`, loft(sh, el, [[0, 0.08, 0.082], [0.5, 0.072, 0.074], [1, 0.062, 0.064]], { segs: 9 }), M.mail);
  part(g, `elbow_${side}`, new THREE.SphereGeometry(0.063, 7, 4), M.mail, el);
  const wrist = hand.clone().addScaledVector(handDir, -0.05);
  part(g, `forearm_${side}`, loft(el, wrist, [[0, 0.062, 0.064], [0.35, 0.064, 0.062], [1, 0.05, 0.048]], { segs: 9 }), M.mail);
  part(g, `cuff_${side}`, loft(wrist.clone().lerp(el, 0.28), wrist, [[0, 0.056, 0.054], [1, 0.068, 0.064]], { segs: 9, caps: false }), M.leather);
  part(g, `glove_${side}`, loft(wrist, hand.clone().addScaledVector(handDir, 0.05), [[0, 0.046, 0.036], [0.5, 0.054, 0.042], [1, 0.042, 0.036]], { segs: 7 }), M.leather);
  return g;
}
const handR = V(-0.29, 1.0, 0.17);
arm('r', -1, handR, V(-0.5, -0.4, -0.7), V(0, -0.3, 1).normalize());
const handL = V(0.2, 1.06, 0.31);
const armL = arm('l', 1, handL, V(0.7, -0.6, -0.3), V(-0.25, 0, 1).normalize());

// Spear: ash haft standing on the ground in the right hand, iron socket with langets, a leaf blade, a butt spike.
const spear = group('spear');
const sdir = V(0.012, 1, 0.025).normalize(), butt = handR.clone().addScaledVector(sdir, -(handR.y - 0.02) / sdir.y);
const at = t => butt.clone().addScaledVector(sdir, t);
part(spear, 'haft', along(new THREE.CylinderGeometry(0.017, 0.019, 2.3, 7, 1, true), sdir), M.wood, at(1.15));
part(spear, 'butt_spike', along(new THREE.CylinderGeometry(0.019, 0.006, 0.07, 7), sdir), M.mail, at(0.035));
part(spear, 'socket', along(lathe([[0.019, 0], [0.023, 0.01], [0.022, 0.09], [0.016, 0.13], [0.011, 0.14]], 7), sdir), M.mail, at(2.2));
for (const s of [-1, 1]) part(spear, 'langet', along(new THREE.BoxGeometry(0.008, 0.16, 0.004), sdir), M.mail, at(2.16).add(V(s * 0.018, 0, 0)));
const blade = lathe([[0.0001, 0], [0.024, 0.025], [0.036, 0.08], [0.032, 0.15], [0.018, 0.22], [0.0001, 0.29]], 10).scale(1, 1, 0.22);
part(spear, 'blade', along(blade, sdir), M.steel, at(2.33));
part(spear, 'blade_rib', along(lathe([[0.0001, 0], [0.008, 0.03], [0.006, 0.2], [0.0001, 0.28]], 6), sdir), M.steel, at(2.335));

// Heater shield on the left forearm: a curved board with an iron rim, the field in cloth, the chevron in trim, straps behind.
const heater = (w, h, k = 1) => {
  const s = new THREE.Shape();
  s.moveTo(-w, h * 0.42 * k); s.lineTo(w, h * 0.42 * k); s.lineTo(w, 0);
  s.quadraticCurveTo(w * 0.94, -h * 0.4, 0, -h * 0.58); s.quadraticCurveTo(-w * 0.94, -h * 0.4, -w, 0); s.closePath();
  return s;
};
const shield = group('shield', armL);
const R = 0.62;
part(shield, 'shield_board', bend(new THREE.ExtrudeGeometry(heater(0.27, 0.68), { depth: 0.02, bevelEnabled: true, bevelThickness: 0.004, bevelSize: 0.004, bevelSegments: 1, curveSegments: 6 }).translate(0, 0, -0.02), R), M.wood);
const rim = heater(0.274, 0.69); rim.holes.push(heater(0.246, 0.63));
part(shield, 'shield_rim', bend(new THREE.ExtrudeGeometry(rim, { depth: 0.008, bevelEnabled: false, curveSegments: 6 }), R), M.mail);
part(shield, 'shield_field', bend(new THREE.ExtrudeGeometry(heater(0.248, 0.632), { depth: 0.004, bevelEnabled: false, curveSegments: 6 }).translate(0, 0, 0.001), R), M.cloth);
const band = new THREE.Shape();
band.moveTo(-0.25, -0.06); band.lineTo(0, 0.16); band.lineTo(0.25, -0.06); band.lineTo(0.25, -0.15);
band.lineTo(0, 0.06); band.lineTo(-0.25, -0.15); band.closePath();
part(shield, 'shield_chevron', bend(new THREE.ExtrudeGeometry(band, { depth: 0.004, bevelEnabled: false }).translate(0, 0, 0.004), R), M.trim);
for (const y of [0.1, -0.08]) part(shield, 'enarme', new THREE.BoxGeometry(0.16, 0.03, 0.012), M.leather, V(0, y, -0.045));
// Face it forward and a little out, tilted back, its back on the forearm.
const n = V(0.35, 0.06, 1).normalize(), right = new THREE.Vector3().crossVectors(V(0, 1, 0), n).normalize(), up = new THREE.Vector3().crossVectors(n, right);
shield.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(right, up, n));
shield.position.copy(handL.clone().lerp(elbow(shoulder(1), handL, V(0.7, -0.6, -0.3)), 0.45)).addScaledVector(n, 0.12).add(V(0, -0.06, 0));

// Shade: darker low on the body and in the folds under the belt, as the Blender pack bakes.
unit.updateMatrixWorld(true);
unit.traverse(o => {
  if (!o.isMesh) return;
  const p = o.geometry.attributes.position, c = [], w = new THREE.Vector3();
  for (let i = 0; i < p.count; i++) {
    w.fromBufferAttribute(p, i).applyMatrix4(o.matrixWorld);
    const t = Math.min(1, Math.max(0, w.y / 1.7)), h = 0.68 + 0.32 * t * t * (3 - 2 * t);
    const fold = Math.abs(w.y - 0.98) < 0.06 && !o.name.startsWith('belt') ? 0.85 : 1;
    c.push(h * fold, h * fold, h * fold);
  }
  o.geometry.setAttribute('color', new THREE.Float32BufferAttribute(c, 3));
});

const bytes = await writeGlb(unit, out);
console.log(`${out}: ${triangles(unit)} triangles, ${Object.keys(M).length} materials, ${(bytes / 1024).toFixed(1)} KiB`);
