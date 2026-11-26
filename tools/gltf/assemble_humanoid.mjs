// Assemble a neutral-standing humanoid from modular IMSH/GLB parts for preview and cost checks.
// This is an inspection pose, not a runtime pose: it mirrors the neutral Puppet3D joint positions closely enough
// to catch pivot, proportion, clipping and readability problems before using IRONFIELD_TROOP_VIEWS.
// Usage:
//   node assemble_humanoid.mjs assembled.glb parts.bin|parts.glb [--render turnaround.png]
//   node assemble_humanoid.mjs assembled.glb parts.bin --set chest=chest_surcoat --set helm=helm_kettle --set shield=shield_heater
//   --set shoulders= (or weapon=/shield=/helm=) omits that layer.
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { writeFileSync, mkdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseGlb, extractPartsFromGlb, readImsh, decodePart } from './imsh_lib.mjs';
import './lib.mjs'; // FileReader polyfill for GLTFExporter in Node

const here = dirname(fileURLToPath(import.meta.url));
const [outArg, inputArg, ...rest] = process.argv.slice(2);
if (!outArg || !inputArg) {
  console.error('usage: node assemble_humanoid.mjs assembled.glb parts.bin|parts.glb [--render turnaround.png] [--set key=part]');
  process.exit(2);
}
const output = resolve(outArg), input = resolve(inputArg);
let renderOut = null;
const slots = {
  chest: 'chest_tunic', shoulders: '', belt: 'belt_leather', skirt: 'skirt_tunic',
  head: 'head_0', helm: 'helm_cap',
  uarm: 'uarm_cloth', farm: 'farm_cloth', thigh: 'thigh_cloth', shin: 'shin_cloth',
  weapon: 'weapon_axe', shield: 'shield_round',
};
for (let i = 0; i < rest.length; i++) {
  if (rest[i] === '--render') renderOut = resolve(rest[++i]);
  else if (rest[i] === '--set') {
    const kv = rest[++i], eq = kv.indexOf('=');
    if (eq < 1 || !(kv.slice(0, eq) in slots)) throw new Error(`bad --set ${kv}; keys: ${Object.keys(slots).join(', ')}`);
    slots[kv.slice(0, eq)] = kv.slice(eq + 1);
  } else throw new Error('unknown option ' + rest[i]);
}

const parts = input.toLowerCase().endsWith('.glb') ? extractPartsFromGlb(parseGlb(input)) : readImsh(input);
const palette = {
  0: [235,235,228], 1: [54,94,176], 2: [30,50,108], 3: [230,218,178], 4: [196,204,218], 5: [110,116,128],
  6: [214,172,70], 7: [128,86,52], 8: [146,102,60], 9: [226,180,140], 10: [96,64,40], 11: [132,104,72], 12: [92,64,52], 13: [60,46,38]
};
const materials = new Map();
function materialFor(id) {
  if (!materials.has(id)) {
    const rgb = palette[id] ?? palette[0];
    const color = new THREE.Color(rgb[0]/255, rgb[1]/255, rgb[2]/255).convertSRGBToLinear();
    const mat = new THREE.MeshStandardMaterial({ color, roughness: 0.78, metalness: id === 4 ? 0.65 : id === 6 ? 0.55 : id === 5 ? 0.35 : 0, vertexColors: true, side: THREE.DoubleSide });
    mat.userData.materialId = id;
    materials.set(id, mat);
  }
  return materials.get(id);
}
function partObject(name, buf) {
  const verts = decodePart(buf);
  const root = new THREE.Group(); root.name = name;
  const byMat = new Map();
  for (let i = 0; i + 2 < verts.length; i += 3) {
    const m = Math.round(verts[i].mat);
    if (!byMat.has(m)) byMat.set(m, []);
    byMat.get(m).push(verts[i], verts[i + 1], verts[i + 2]);
  }
  for (const [matId, list] of [...byMat.entries()].sort((a,b) => a[0]-b[0])) {
    const pos = new Float32Array(list.length * 3), norm = new Float32Array(list.length * 3), col = new Float32Array(list.length * 3);
    for (let i = 0; i < list.length; i++) {
      pos.set(list[i].p, i * 3); norm.set(list[i].n, i * 3); col.set(list[i].rgba.slice(0, 3).map(x => x / 255), i * 3);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('normal', new THREE.BufferAttribute(norm, 3));
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    const mesh = new THREE.Mesh(g, materialFor(matId));
    mesh.name = `${name}_m${matId}`;
    root.add(mesh);
  }
  return root;
}

const library = new Map();
for (const [name, buf] of parts) library.set(name, partObject(name, buf));
const scene = new THREE.Scene();
function inst(key) {
  const name = slots[key];
  if (!name) return null;
  const src = library.get(name);
  if (!src) throw new Error(`missing part '${name}' for slot ${key}; available: ${[...library.keys()].join(', ')}`);
  const o = src.clone(true);
  scene.add(o);
  return o;
}
const V = (x, y, z) => new THREE.Vector3(x, y, z);
function boneTransform(obj, a, b, fwdHint = V(0, 0, 1), radialScale = 1) {
  const d = b.clone().sub(a), len = d.length();
  const uy = d.clone().divideScalar(len);
  const rx = new THREE.Vector3().crossVectors(uy, fwdHint);
  if (rx.lengthSq() < 1e-6) rx.crossVectors(uy, V(1, 0, 0));
  rx.normalize();
  const fz = new THREE.Vector3().crossVectors(rx, uy).normalize();
  if (fz.dot(fwdHint) < 0) { rx.negate(); fz.negate(); }
  obj.position.copy(a);
  obj.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(rx, uy, fz));
  obj.scale.set(radialScale, len, radialScale);
}
function orientYF(obj, pos, dir, forward) {
  const uy = dir.clone().normalize();
  const fz = forward.clone().sub(uy.clone().multiplyScalar(forward.dot(uy))).normalize();
  const rx = new THREE.Vector3().crossVectors(uy, fz).normalize();
  obj.position.copy(pos);
  obj.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(rx, uy, fz));
}

// Body-space layers.
for (const key of ['chest', 'skirt', 'belt', 'shoulders']) inst(key);

// Neutral standing legs: Puppet3D hips near y=0.68 and two roughly 0.34 m bones to the ground.
for (const side of [-1, 1]) {
  const hip = V(side * 0.145, 0.68, 0.000);
  const knee = V(side * 0.157, 0.34, 0.008);
  const foot = V(side * 0.168, 0.00, 0.018);
  boneTransform(inst('thigh'), hip, knee, V(side * 0.05, 0, 1).normalize());
  boneTransform(inst('shin'), knee, foot, V(side * 0.04, 0, 1).normalize());
}

// Head/helm at the runtime neck joint.
for (const key of ['head', 'helm']) {
  const o = inst(key);
  if (o) o.position.set(0, 1.53, 0.01);
}

// Relaxed arms, using Puppet3D shoulder positions x +/-0.255, y=1.25.
const rSh = V(0.255, 1.25, 0.000), rEl = V(0.333, 0.982, 0.045), rHand = V(0.370, 0.720, 0.096);
boneTransform(inst('uarm'), rSh, rEl, V(0, 0, 1));
boneTransform(inst('farm'), rEl, rHand, V(0, 0, 1));
const lSh = V(-0.255, 1.25, 0.000), lEl = V(-0.340, 1.015, 0.070), lHand = V(-0.323, 0.800, 0.190);
boneTransform(inst('uarm'), lSh, lEl, V(0, 0, 1));
boneTransform(inst('farm'), lEl, lHand, V(0, 0, 1));

const weapon = inst('weapon');
if (weapon) orientYF(weapon, rHand, V(0.10, 1.0, 0.15), V(0.04, 0.12, 1));
const shield = inst('shield');
if (shield) {
  shield.position.set(-0.355, 0.925, 0.255);
  shield.rotation.set(THREE.MathUtils.degToRad(4), THREE.MathUtils.degToRad(-14), THREE.MathUtils.degToRad(-4), 'YXZ');
}

mkdirSync(dirname(output), { recursive: true });
const glb = await new GLTFExporter().parseAsync(scene, { binary: true });
writeFileSync(output, Buffer.from(glb));
const triOf = name => (parts.get(name)?.length ?? 0) / 36 / 3;
const weighted = ['chest','shoulders','belt','skirt','head','helm','weapon','shield'].reduce((n,k) => n + triOf(slots[k]), 0)
  + 2 * (triOf(slots.uarm) + triOf(slots.farm) + triOf(slots.thigh) + triOf(slots.shin));
console.log(`${output}: assembled weighted cost ${weighted} tris, ${(glb.byteLength / 1024).toFixed(1)} KB`);
if (renderOut) {
  const r = spawnSync(process.execPath, [resolve(here, 'render_unit.mjs'), renderOut, `${output}=assembled humanoid`], { stdio: 'inherit' });
  if (r.status !== 0) process.exit(r.status ?? 1);
}
