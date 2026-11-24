// The same man-at-arms from the Blender pipeline's pack (Data/meshes/units3d.bin, tools/build_meshes_3d.py),
// assembled standing as Puppet3D.cs does it (EmitLocal / EmitBone / EmitWeapon / EmitShield, frame right = -X)
// and coloured as UnitMatTable.Fill does, then written as .glb so both pipelines render through one viewer.
//   node blender_unit.mjs [units3d.bin] [out.glb]
// Not shown here: the game's shader adds procedural mail, quilt and brushed patterns on top of these colours.
import * as THREE from 'three';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { rgb, Palette, writeGlb, triangles } from './lib.mjs';

const binPath = resolve(process.argv[2] ?? '../../Data/meshes/units3d.bin');
const out = resolve(process.argv[3] ?? 'out/blender_man_at_arms.glb');

// Read the pack: IMSH, version, count; per part a name and triangle-list vertices (pos, normal, mat, aux, rgba).
const buf = readFileSync(binPath), dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
if (buf.toString('ascii', 0, 4) !== 'IMSH') throw new Error('not a mesh pack');
const parts = {};
let o = 12;
for (let p = 0, n = dv.getUint32(8, true); p < n; p++) {
  const nl = dv.getUint16(o, true); o += 2;
  const name = buf.toString('utf8', o, o + nl); o += nl;
  const count = dv.getUint32(o, true); o += 4;
  const v = [];
  for (let i = 0; i < count; i++, o += 36) {
    const f = k => dv.getFloat32(o + k * 4, true);
    v.push({ p: [f(0), f(1), f(2)], n: [f(3), f(4), f(5)], m: Math.round(f(6)), c: [buf[o + 32], buf[o + 33], buf[o + 34]] });
  }
  parts[name] = v;
}

// Colours by material id, as UnitMatTable.Fill packs them (with its boosts), and metalness from UnitMatTable.Metals.
const boost = { 1: 1.16, 2: 1.18, 3: 1.10 };
const colour = m => (Palette[m] ?? [255, 255, 255]).map(x => Math.min(1, x * (boost[m] ?? 1) / 255));
const metal = { 4: 0.95, 5: 0.55, 6: 0.9 };

// One mesh per emitted part, its vertices placed in the unit's frame (local x -> world -x, Puppet3D.Frame facing +Z).
const unit = new THREE.Group();
unit.name = 'man_at_arms';
unit.userData = { troop: 'demo.man_at_arms', palette: 'Azure', units: 'metres', forward: '+Z', builder: 'blender (units3d.bin)' };
const W = v => new THREE.Vector3(-v.x, v.y, v.z);
const mats = {};
function emit(name, key, pos, rx, uy, fz, sx = 1, sy = 1, sz = 1) {
  const src = parts[key];
  if (!src) throw new Error(`no part ${key}`);
  const groups = {};
  for (const v of src) (groups[v.m] ??= []).push(v);
  for (const [m, vs] of Object.entries(groups)) {
    const P = [], N = [], C = [];
    for (const v of vs) {
      const lp = pos.clone().addScaledVector(rx, sx * v.p[0]).addScaledVector(uy, sy * v.p[1]).addScaledVector(fz, sz * v.p[2]);
      const ln = new THREE.Vector3().addScaledVector(rx, v.n[0]).addScaledVector(uy, v.n[1]).addScaledVector(fz, v.n[2]).normalize();
      const wp = W(lp), wn = W(ln), col = colour(+m);
      P.push(wp.x, wp.y, wp.z); N.push(wn.x, wn.y, wn.z);
      C.push(...col.map((k, i) => new THREE.Color(k * v.c[i] / 255, 0, 0).convertSRGBToLinear().r));
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(P, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(N, 3));
    g.setAttribute('color', new THREE.Float32BufferAttribute(C, 3));
    mats[m] ??= Object.assign(new THREE.MeshStandardMaterial({
      name: `mat_${m}`, vertexColors: true, metalness: metal[m] ?? 0, roughness: metal[m] ? 0.4 : 0.85, side: THREE.DoubleSide,
    }), { userData: { materialId: +m } });
    const mesh = new THREE.Mesh(g, mats[m]);
    mesh.name = `${name}${Object.keys(groups).length > 1 ? '_' + m : ''}`;
    unit.add(mesh);
  }
}
const X = new THREE.Vector3(1, 0, 0), Y = new THREE.Vector3(0, 1, 0), Z = new THREE.Vector3(0, 0, 1);
const L = (x, y, z) => new THREE.Vector3(x, y, z);
function bone(name, key, a, b, hint = Z) {
  const d = b.clone().sub(a), len = d.length(), uy = d.divideScalar(len);
  let rx = new THREE.Vector3().crossVectors(uy, hint);
  if (rx.lengthSq() < 1e-4) rx.crossVectors(uy, X);
  rx.normalize();
  let fz = new THREE.Vector3().crossVectors(rx, uy).normalize();
  if (fz.dot(hint) < 0) { rx.negate(); fz.negate(); }
  emit(name, key, a, rx, uy, fz, 1, len, 1);
}
const rotY = (v, deg) => { const r = deg * Math.PI / 180, c = Math.cos(r), s = Math.sin(r); return L(v.x * c + v.z * s, v.y, -v.x * s + v.z * c); };
const rotX = (v, deg) => { const r = deg * Math.PI / 180, c = Math.cos(r), s = Math.sin(r); return L(v.x, v.y * c - v.z * s, v.y * s + v.z * c); };
function elbow(sh, hand, pole, up = 0.29, fore = 0.28) {
  const d = hand.clone().sub(sh), len = Math.min(d.length(), (up + fore) * 0.999), axis = d.normalize();
  const al = (up * up - fore * fore + len * len) / (2 * len), h = Math.sqrt(Math.max(0, up * up - al * al));
  const b = pole.clone().addScaledVector(axis, -pole.dot(axis)).normalize();
  return sh.clone().addScaledVector(axis, al).addScaledVector(b, h);
}

// The look: body "mail" (surcoat, aventail, pouch belt, mail skirt) with a tabard, mail arms, cloth legs,
// head_0 under a kettle helm, spear, heater shield, the Azure chevron.
for (const s of [-1, 1]) {
  const hip = L(s * 0.125, 0.68, 0), knee = L(s * 0.125, 0.34, 0.01), foot = L(s * 0.125, 0, 0);
  bone(`thigh_${s}`, 'thigh_cloth', hip, knee);
  bone(`shin_${s}`, 'shin_cloth', knee, foot);
}
emit('chest', 'chest_surcoat', L(0, 0, 0), X, Y, Z);
emit('skirt', 'skirt_mail', L(0, 0, 0), X, Y, Z);
emit('shoulders', 'shld_mail_aventail', L(0, 0, 0), X, Y, Z);
emit('belt', 'belt_pouch', L(0, 0, 0), X, Y, Z);
emit('tabard_charge', 'emblem_chevron', L(0, 1.03, 0.186), X, Y, Z, 0.56, 0.56, 0.7);
emit('head', 'head_0', L(0, 1.53, 0.01), X, Y, Z);
emit('helm', 'helm_kettle', L(0, 1.53, 0.01), X, Y, Z);

// Arms as the puppet lays them (Shoulder, Elbow), hands where the three.js man holds his (mirrored into local x).
const shoulder = s => L(s * 0.23, 1.24, 0);
const handW = L(0.25, 0.86, 0.16), handS = L(-0.19, 0.9, 0.28);
for (const [s, hand, pole] of [[1, handW, L(0.5, -0.5, -0.7)], [-1, handS, L(-0.7, -0.6, -0.3)]]) {
  const el = elbow(shoulder(s), hand, pole);
  bone(`uarm_${s}`, 'uarm_mail', shoulder(s), el);
  bone(`farm_${s}`, 'farm_mail', el, hand);
}
// Spear: EmitWeapon with a straight edge (edge along local x), upright in the weapon hand.
const sdir = L(-0.012, 1, 0.025).normalize();
let e = L(1, 0, 0).addScaledVector(sdir, -sdir.x).normalize();
emit('spear', 'weapon_spear', handW, e, sdir, new THREE.Vector3().crossVectors(e, sdir).normalize());
// Shield: EmitShield on the off hand, yawed to face forward and out like the three.js man's.
const yaw = 20, sR = rotY(X, yaw), sU = rotY(Y, yaw), sF = rotY(Z, yaw), sp = handS.clone().add(L(-0.07, 0.02, 0.06));
emit('shield', 'shield_heater', sp, sR, sU, sF);
emit('shield_charge', 'emblem_chevron', sp.clone().addScaledVector(sU, 0.02).addScaledVector(sF, 0.07), sR, sU, sF, 0.94, 0.94, 1);

// Put his feet on the ground and his spear butt beside them.
const box = new THREE.Box3().setFromObject(unit);
unit.position.y = -box.min.y;
const bytes = await writeGlb(unit, out);
console.log(`${out}: ${triangles(unit)} triangles, ${Object.keys(mats).length} materials, ${(bytes / 1024).toFixed(1)} KiB`);
