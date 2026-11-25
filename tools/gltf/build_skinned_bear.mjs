// Continuous GPU-skinned Bear + 128x128 PointClamp pixel-art texture (SKN1 + PNG).
// Usage: node build_skinned_bear.mjs
import * as THREE from 'three';
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { V, pathLoft } from './geom.mjs';

const skinOut = resolve('../../Data/meshes/bear_skinned.skin');
const texOut = resolve('../../Data/meshes/bear_tex.png');

// 8 Bones (bind pivots in bear standing space: y=0 ground, +Z forward):
// 0 Pelvis, 1 Chest, 2 Head, 3 Jaw, 4 FrontL, 5 FrontR, 6 HindL, 7 HindR
const BONES = [
  [0, 0.72, -0.32],
  [0, 0.80,  0.26],
  [0, 0.87,  0.52],
  [0, 0.78,  0.62],
  [-0.26, 0.66,  0.36],
  [ 0.26, 0.66,  0.36],
  [-0.26, 0.66, -0.42],
  [ 0.26, 0.66, -0.42],
];

// ---------- 1. Generate 128x128 crisp pixel-art texture ----------
const W = 128, H = 128;
const rgba = new Uint8Array(W * H * 4);
function setPx(x, y, r, g, b, a = 255) {
  x = Math.max(0, Math.min(W - 1, x | 0));
  y = Math.max(0, Math.min(H - 1, y | 0));
  const o = (y * W + x) * 4;
  rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = a;
}
function hash(x, y) {
  const n = Math.sin(x * 127.1 + y * 311.7) * 43758.5453;
  return n - Math.floor(n);
}

// Quadrant (0..63, 0..127): Body/Limb fur with tufted pixel clusters, darker dorsal cape, paler underbelly
for (let y = 0; y < 128; y++) {
  for (let x = 0; x < 64; x++) {
    // u in 0..0.5 maps around circumference: x=0..16 & 48..64 = dorsal/sides, x=16..48 = belly
    const dorsal = Math.exp(-Math.pow(x / 14, 2));
    const belly = Math.exp(-Math.pow((63 - x) / 14, 2));
    const tuft = hash(x >> 1, y >> 2);
    let r = 126, g = 86, b = 56;
    if (dorsal > 0.55) { r = 82; g = 54; b = 36; }
    else if (belly > 0.55) { r = 164; g = 124; b = 88; }
    if (tuft > 0.72) { r = Math.min(255, r + 16); g = Math.min(255, g + 12); b = Math.min(255, b + 8); }
    else if (tuft < 0.22) { r = Math.max(0, r - 18); g = Math.max(0, g - 14); b = Math.max(0, b - 10); }
    setPx(x, y, r, g, b);
  }
}

// Quadrant (64..127, 0..63): Head, Snout, Amber Eyes, Wet Nose
for (let y = 0; y < 64; y++) {
  for (let x = 64; x < 128; x++) {
    const u = (x - 64) / 64, v = y / 64;
    const tuft = hash(x >> 1, y >> 1);
    let r = 122, g = 84, b = 54;
    if (tuft < 0.25) { r -= 14; g -= 10; b -= 8; }
    // Pale muzzle band (v > 0.55)
    if (v > 0.55) { r = 172; g = 136; b = 98; }
    // Wet black nose (v > 0.88, centered at tip)
    if (v > 0.88 && Math.abs(u - 0.5) < 0.16) { r = 24; g = 20; b = 22; }
    // Dark brow & fierce amber eyes (at u ~ 0.28 and 0.72, v ~ 0.36)
    for (const ex of [0.26, 0.74]) {
      const dx = (u - ex) * 64, dy = (v - 0.36) * 64;
      const d2 = dx * dx + dy * dy;
      if (d2 < 22 && d2 >= 11) { r = 42; g = 28; b = 22; } // dark eye socket
      if (d2 < 11) { r = 244; g = 182; b = 44; } // glowing amber iris
      if (d2 < 3.5) { r = 24; g = 18; b = 16; } // pupil
      if (Math.abs(dx + 1) < 1.1 && Math.abs(dy + 1) < 1.1) { r = 255; g = 245; b = 220; } // catchlight
    }
    setPx(x, y, r, g, b);
  }
}

// Quadrant (64..127, 64..127): Ivory claws & fangs (top) + dark paw pads/inner mouth (bottom)
for (let y = 64; y < 128; y++) {
  for (let x = 64; x < 128; x++) {
    if (y < 96) setPx(x, y, 226, 214, 186); // ivory
    else setPx(x, y, 42, 32, 30); // dark pad / inner mouth
  }
}

// Minimal CRC32 + PNG writer
const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++) {
  let c = n;
  for (let k = 0; k < 8; k++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
  crcTable[n] = c >>> 0;
}
function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function pngChunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length, 0);
  const t = Buffer.from(type, 'ascii');
  const crcBuf = Buffer.concat([t, data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(crcBuf), 0);
  return Buffer.concat([len, t, data, crc]);
}
function writePng(path, w, h, rgbaBytes) {
  const raw = Buffer.alloc(h * (1 + w * 4));
  for (let y = 0; y < h; y++) {
    raw[y * (1 + w * 4)] = 0;
    Buffer.from(rgbaBytes.buffer, y * w * 4, w * 4).copy(raw, y * (1 + w * 4) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6; // 8-bit RGBA
  const png = Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', deflateSync(raw)),
    pngChunk('IEND', Buffer.alloc(0)),
  ]);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, png);
}
writePng(texOut, W, H, rgba);

// ---------- 2. Build Continuous Skinned Bear Geometry ----------
const verts = []; // { p, n, u, v, c:[r,g,b], bones:[b0,b1,b2,b3], weights:[w0,w1,w2,w3] }
const indices = [];

function addGeo(geo, uvFn, weightFn, shadeFn = () => 1.0) {
  const base = verts.length;
  geo.computeVertexNormals();
  const pos = geo.attributes.position;
  const norm = geo.attributes.normal;
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
    const nx = norm.getX(i), ny = norm.getY(i), nz = norm.getZ(i);
    const [u, v] = uvFn(x, y, z, i);
    const [bones, weights] = weightFn(x, y, z);
    const s = Math.max(0.45, Math.min(1.0, shadeFn(x, y, z)));
    const sb = Math.round(s * 255);
    verts.push({ p: [x, y, z], n: [nx, ny, nz], u, v, c: [sb, sb, sb], bones, weights });
  }
  const idx = geo.index;
  if (idx) {
    for (let i = 0; i < idx.count; i++) indices.push(base + idx.getX(i));
  } else {
    for (let i = 0; i < pos.count; i++) indices.push(base + i);
  }
}

function spineWeight(z) {
  // Smooth blend along Z: Pelvis(0) at z=-0.32 -> Chest(1) at z=0.26 -> Head(2) at z=0.52+
  if (z <= -0.32) return [[0, 0, 0, 0], [1, 0, 0, 0]];
  if (z < 0.26) {
    const t = (z - (-0.32)) / 0.58;
    const s = t * t * (3 - 2 * t);
    return [[0, 1, 0, 0], [1 - s, s, 0, 0]];
  }
  if (z < 0.52) {
    const t = (z - 0.26) / 0.26;
    const s = t * t * (3 - 2 * t);
    return [[1, 2, 0, 0], [1 - s, s, 0, 0]];
  }
  return [[2, 0, 0, 0], [1, 0, 0, 0]];
}

// A. Continuous Torso -> Neck -> Head -> Snout!
const body = pathLoft([
  { p: V(0, 0.70, -0.72), rx: 0.14, rz: 0.14 },
  { p: V(0, 0.76, -0.52), rx: 0.39, rz: 0.33 },
  { p: V(0, 0.78, -0.24), rx: 0.42, rz: 0.36 },
  { p: V(0, 0.82,  0.04), rx: 0.43, rz: 0.38 },
  { p: V(0, 0.90,  0.28), rx: 0.44, rz: 0.43 }, // massive shoulder hump
  { p: V(0, 0.89,  0.48), rx: 0.31, rz: 0.30 }, // thick neck
  { p: V(0, 0.91,  0.66), rx: 0.26, rz: 0.24 }, // cranium
  { p: V(0, 0.88,  0.84), rx: 0.16, rz: 0.14 }, // brow/snout bridge
  { p: V(0, 0.85,  1.00), rx: 0.10, rz: 0.085 }, // nose tip
  { p: V(0, 0.84,  1.04), rx: 0.04, rz: 0.035 },
], {
  segs: 24, fwd: V(0, 1, 0),
  profile: (th) => [1.0, Math.sin(th) > 0.2 ? 1.08 : 0.92],
});
addGeo(body,
  (x, y, z) => {
    if (z > 0.56) {
      // Map head/snout into quadrant (0.5..1.0, 0..0.5)
      const u = 0.75 + Math.max(-0.24, Math.min(0.24, x * 0.9));
      const v = Math.max(0.02, Math.min(0.48, ((z - 0.56) / 0.48) * 0.46));
      return [u, v];
    }
    const ang = Math.atan2(x, y - 0.78);
    const u = (Math.abs(ang) / Math.PI) * 0.46 + 0.01;
    const v = Math.max(0.01, Math.min(0.99, (z + 0.72) / 1.30));
    return [u, v];
  },
  (_x, _y, z) => spineWeight(z),
  (_x, y) => 0.76 + 0.24 * Math.max(0, Math.min(1, (y - 0.45) / 0.75))
);

// B. Lower Jaw (Bone 3)
const jaw = pathLoft([
  { p: V(0, 0.78, 0.56), rx: 0.14, rz: 0.08 },
  { p: V(0, 0.73, 0.80), rx: 0.10, rz: 0.06 },
  { p: V(0, 0.71, 0.97), rx: 0.07, rz: 0.04 },
], { segs: 14, fwd: V(0, 1, 0) });
addGeo(jaw,
  (x, _y, z) => [0.75 + x * 0.6, 0.35 + ((z - 0.56) / 0.42) * 0.12],
  () => [[3, 0, 0, 0], [1, 0, 0, 0]],
  () => 0.85
);

// C. Ears (Bone 2) & Fangs (Bone 2 / Bone 3)
for (const sx of [-1, 1]) {
  const ear = new THREE.SphereGeometry(0.085, 12, 8);
  ear.scale(1.0, 1.0, 0.52);
  ear.translate(sx * 0.19, 1.12, 0.63);
  addGeo(ear, () => [0.55, 0.05], () => [[2, 0, 0, 0], [1, 0, 0, 0]], () => 0.84);

  const upperFang = new THREE.ConeGeometry(0.020, 0.072, 8);
  upperFang.rotateX(Math.PI);
  upperFang.translate(sx * 0.068, 0.785, 0.91);
  addGeo(upperFang, () => [0.75, 0.60], () => [[2, 0, 0, 0], [1, 0, 0, 0]], () => 1.0);

  const lowerFang = new THREE.ConeGeometry(0.017, 0.058, 8);
  lowerFang.translate(sx * 0.058, 0.745, 0.89);
  addGeo(lowerFang, () => [0.75, 0.60], () => [[3, 0, 0, 0], [1, 0, 0, 0]], () => 1.0);
}

// D. 4 Continuous Muscular Legs + Paws + Claws (smoothly blended to Torso at shoulder/hip!)
const legSpecs = [
  { bone: 4, torsoBone: 1, sx: -1, z:  0.36, rTop: 0.175 },
  { bone: 5, torsoBone: 1, sx:  1, z:  0.36, rTop: 0.175 },
  { bone: 6, torsoBone: 0, sx: -1, z: -0.42, rTop: 0.185 },
  { bone: 7, torsoBone: 0, sx:  1, z: -0.42, rTop: 0.185 },
];
for (const leg of legSpecs) {
  const x0 = leg.sx * 0.25;
  const g = pathLoft([
    { p: V(x0 * 0.85, 0.76, leg.z),        rx: leg.rTop * 1.08, rz: leg.rTop * 1.18 },
    { p: V(x0,        0.56, leg.z - 0.02), rx: leg.rTop * 0.95, rz: leg.rTop * 1.05 },
    { p: V(x0,        0.34, leg.z - 0.07), rx: 0.115, rz: 0.125 }, // muscular elbow/hock
    { p: V(x0,        0.14, leg.z + 0.01), rx: 0.095, rz: 0.105 },
    { p: V(x0,        0.04, leg.z + 0.07), rx: 0.118, rz: 0.145 }, // broad paw
    { p: V(x0,        0.00, leg.z + 0.07), rx: 0.085, rz: 0.105 },
  ], { segs: 16 });
  addGeo(g,
    (x, y) => [0.15 + Math.abs(x) * 0.3, Math.max(0.05, Math.min(0.95, 1 - y / 0.76))],
    (_x, y) => {
      if (y > 0.66) {
        const t = Math.min(1, (y - 0.66) / 0.14);
        return [[leg.bone, leg.torsoBone, 0, 0], [1 - t * 0.6, t * 0.6, 0, 0]];
      }
      return [[leg.bone, 0, 0, 0], [1, 0, 0, 0]];
    },
    (_x, y) => 0.74 + 0.26 * Math.min(1, y / 0.76)
  );
  // 4 curved claws per paw
  for (let c = 0; c < 4; c++) {
    const cx = x0 + (c - 1.5) * 0.042;
    const claw = new THREE.ConeGeometry(0.014, 0.068, 6);
    claw.rotateX(Math.PI * 0.42);
    claw.translate(cx, 0.024, leg.z + 0.21);
    addGeo(claw, () => [0.75, 0.60], () => [[leg.bone, 0, 0, 0], [1, 0, 0, 0]], () => 1.0);
  }
}

// ---------- 3. Write SKN1 Binary ----------
const bufSize = 4 + 4 + BONES.length * 12 + 4 + 4 + verts.length * 56 + indices.length * 2;
const out = Buffer.alloc(bufSize);
let o = 0;
out.write('SKN1', o, 'ascii'); o += 4;
out.writeInt32LE(BONES.length, o); o += 4;
for (const [bx, by, bz] of BONES) {
  out.writeFloatLE(bx, o); out.writeFloatLE(by, o + 4); out.writeFloatLE(bz, o + 8); o += 12;
}
out.writeInt32LE(verts.length, o); o += 4;
out.writeInt32LE(indices.length, o); o += 4;
for (const vt of verts) {
  out.writeFloatLE(vt.p[0], o); out.writeFloatLE(vt.p[1], o + 4); out.writeFloatLE(vt.p[2], o + 8); o += 12;
  out.writeFloatLE(vt.n[0], o); out.writeFloatLE(vt.n[1], o + 4); out.writeFloatLE(vt.n[2], o + 8); o += 12;
  out.writeFloatLE(vt.u, o); out.writeFloatLE(vt.v, o + 4); o += 8;
  out[o++] = vt.c[0]; out[o++] = vt.c[1]; out[o++] = vt.c[2]; out[o++] = 255;
  out[o++] = vt.bones[0]; out[o++] = vt.bones[1]; out[o++] = vt.bones[2]; out[o++] = vt.bones[3];
  out.writeFloatLE(vt.weights[0], o); out.writeFloatLE(vt.weights[1], o + 4);
  out.writeFloatLE(vt.weights[2], o + 8); out.writeFloatLE(vt.weights[3], o + 12); o += 16;
}
for (const ix of indices) { out.writeUInt16LE(ix, o); o += 2; }
mkdirSync(dirname(skinOut), { recursive: true });
writeFileSync(skinOut, out.subarray(0, o));
console.log(`Wrote ${skinOut}: ${verts.length} verts, ${indices.length / 3} tris (${(o / 1024).toFixed(1)} KB) + ${texOut}`);
