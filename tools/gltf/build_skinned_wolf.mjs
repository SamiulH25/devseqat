// Continuous GPU-skinned Wolf + 128x128 PointClamp pixel-art texture (SKN1 + PNG).
import * as THREE from 'three';
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { V, pathLoft } from './geom.mjs';

const skinOut = resolve('../../Data/meshes/wolf_skinned.skin');
const texOut = resolve('../../Data/meshes/wolf_tex.png');

const BONES = [
  [0, 0.62, -0.32],
  [0, 0.64,  0.24],
  [0, 0.76,  0.44],
  [-0.15, 0.58,  0.34],
  [ 0.15, 0.58,  0.34],
  [-0.15, 0.58, -0.38],
  [ 0.15, 0.58, -0.38],
];

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

// Body/Limb fur (0..63, 0..127): silver-grey wolf with dark dorsal mane & pale belly
for (let y = 0; y < 128; y++) for (let x = 0; x < 64; x++) {
  const dorsal = Math.exp(-Math.pow(x / 13, 2));
  const belly = Math.exp(-Math.pow((63 - x) / 14, 2));
  const tuft = hash(x >> 1, y >> 2);
  let r = 158, g = 162, b = 170;
  if (dorsal > 0.55) { r = 92; g = 96; b = 104; }
  else if (belly > 0.55) { r = 216; g = 218; b = 224; }
  if (tuft > 0.72) { r = Math.min(255, r + 16); g = Math.min(255, g + 16); b = Math.min(255, b + 16); }
  else if (tuft < 0.22) { r = Math.max(0, r - 18); g = Math.max(0, g - 18); b = Math.max(0, b - 18); }
  setPx(x, y, r, g, b);
}

// Head/Snout/Amber Eyes (64..127, 0..63)
for (let y = 0; y < 64; y++) for (let x = 64; x < 128; x++) {
  const u = (x - 64) / 64, v = y / 64;
  const tuft = hash(x >> 1, y >> 1);
  let r = 162, g = 166, b = 174;
  if (tuft < 0.25) { r -= 14; g -= 14; b -= 14; }
  if (v > 0.58) { r = 212; g = 214; b = 220; }
  if (v > 0.88 && Math.abs(u - 0.5) < 0.16) { r = 22; g = 20; b = 24; }
  for (const ex of [0.26, 0.74]) {
    const dx = (u - ex) * 64, dy = (v - 0.36) * 64, d2 = dx * dx + dy * dy;
    if (d2 < 20 && d2 >= 10) { r = 44; g = 42; b = 48; }
    if (d2 < 10) { r = 244; g = 182; b = 44; }
    if (d2 < 3.2) { r = 20; g = 18; b = 20; }
    if (Math.abs(dx + 1) < 1.1 && Math.abs(dy + 1) < 1.1) { r = 255; g = 248; b = 225; }
  }
  setPx(x, y, r, g, b);
}

// Claws/Fangs (64..127, 64..127)
for (let y = 64; y < 128; y++) for (let x = 64; x < 128; x++) {
  if (y < 96) setPx(x, y, 232, 224, 202);
  else setPx(x, y, 38, 34, 36);
}

const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1); crcTable[n] = c >>> 0; }
function crc32(buf) { let c = 0xffffffff; for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; }
function pngChunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length, 0);
  const t = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(Buffer.concat([t, data])), 0);
  return Buffer.concat([len, t, data, crc]);
}
const raw = Buffer.alloc(H * (1 + W * 4));
for (let y = 0; y < H; y++) { raw[y * (1 + W * 4)] = 0; Buffer.from(rgba.buffer, y * W * 4, W * 4).copy(raw, y * (1 + W * 4) + 1); }
const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4); ihdr[8] = 8; ihdr[9] = 6;
mkdirSync(dirname(texOut), { recursive: true });
writeFileSync(texOut, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), pngChunk('IHDR', ihdr), pngChunk('IDAT', deflateSync(raw)), pngChunk('IEND', Buffer.alloc(0))]));

const verts = [], indices = [];
function addGeo(geo, uvFn, weightFn, shadeFn = () => 1.0) {
  const base = verts.length;
  geo.computeVertexNormals();
  const pos = geo.attributes.position, norm = geo.attributes.normal;
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
    const [u, v] = uvFn(x, y, z, i);
    const [bones, weights] = weightFn(x, y, z);
    const sb = Math.round(Math.max(0.45, Math.min(1.0, shadeFn(x, y, z))) * 255);
    verts.push({ p: [x, y, z], n: [norm.getX(i), norm.getY(i), norm.getZ(i)], u, v, c: [sb, sb, sb], bones, weights });
  }
  const idx = geo.index;
  if (idx) for (let i = 0; i < idx.count; i++) indices.push(base + idx.getX(i));
  else for (let i = 0; i < pos.count; i++) indices.push(base + i);
}
function spineWeight(z) {
  if (z <= -0.32) return [[0, 0, 0, 0], [1, 0, 0, 0]];
  if (z < 0.24) { const t = (z + 0.32) / 0.56, s = t * t * (3 - 2 * t); return [[0, 1, 0, 0], [1 - s, s, 0, 0]]; }
  if (z < 0.44) { const t = (z - 0.24) / 0.20, s = t * t * (3 - 2 * t); return [[1, 2, 0, 0], [1 - s, s, 0, 0]]; }
  return [[2, 0, 0, 0], [1, 0, 0, 0]];
}

// Continuous Wolf Body -> Neck -> Head -> Snout + Tail
const body = pathLoft([
  { p: V(0, 0.46, -0.92), rx: 0.04, rz: 0.05 }, // tail tip
  { p: V(0, 0.58, -0.68), rx: 0.08, rz: 0.09 }, // tail base
  { p: V(0, 0.62, -0.44), rx: 0.20, rz: 0.19 }, // hips
  { p: V(0, 0.60, -0.10), rx: 0.19, rz: 0.20 }, // waist
  { p: V(0, 0.65,  0.24), rx: 0.24, rz: 0.26 }, // broad chest & ruff
  { p: V(0, 0.72,  0.42), rx: 0.19, rz: 0.20 }, // neck
  { p: V(0, 0.78,  0.56), rx: 0.16, rz: 0.15 }, // cranium
  { p: V(0, 0.75,  0.74), rx: 0.09, rz: 0.08 }, // snout
  { p: V(0, 0.74,  0.88), rx: 0.05, rz: 0.045 }, // nose
], { segs: 18, fwd: V(0, 1, 0) });
addGeo(body,
  (x, y, z) => z > 0.48
    ? [0.75 + Math.max(-0.24, Math.min(0.24, x * 1.4)), Math.max(0.02, Math.min(0.48, ((z - 0.48) / 0.40) * 0.46))]
    : [(Math.abs(Math.atan2(x, y - 0.62)) / Math.PI) * 0.46 + 0.01, Math.max(0.01, Math.min(0.99, (z + 0.92) / 1.40))],
  (_x, _y, z) => spineWeight(z),
  (_x, y) => 0.78 + 0.22 * Math.max(0, Math.min(1, (y - 0.40) / 0.50))
);

// Ears & Fangs
for (const sx of [-1, 1]) {
  const ear = new THREE.ConeGeometry(0.052, 0.13, 8);
  ear.translate(sx * 0.10, 0.94, 0.52);
  addGeo(ear, () => [0.10, 0.10], () => [[2, 0, 0, 0], [1, 0, 0, 0]], () => 0.85);
  const fang = new THREE.ConeGeometry(0.013, 0.046, 6);
  fang.rotateX(Math.PI); fang.translate(sx * 0.042, 0.68, 0.80);
  addGeo(fang, () => [0.75, 0.60], () => [[2, 0, 0, 0], [1, 0, 0, 0]], () => 1.0);
}

// 4 Continuous Wolf Legs
for (const leg of [
  { bone: 3, tb: 1, sx: -1, z:  0.32, hock: -0.06 },
  { bone: 4, tb: 1, sx:  1, z:  0.32, hock: -0.06 },
  { bone: 5, tb: 0, sx: -1, z: -0.38, hock: -0.10 },
  { bone: 6, tb: 0, sx:  1, z: -0.38, hock: -0.10 },
]) {
  const x0 = leg.sx * 0.145;
  const g = pathLoft([
    { p: V(x0 * 0.85, 0.62, leg.z),              rx: 0.095, rz: 0.105 },
    { p: V(x0,        0.44, leg.z + leg.hock*0.4), rx: 0.078, rz: 0.088 },
    { p: V(x0,        0.26, leg.z + leg.hock),    rx: 0.056, rz: 0.064 },
    { p: V(x0,        0.05, leg.z + 0.02),        rx: 0.052, rz: 0.068 },
    { p: V(x0,        0.00, leg.z + 0.04),        rx: 0.062, rz: 0.082 },
  ], { segs: 12 });
  addGeo(g,
    (x, y) => [0.15 + Math.abs(x) * 0.4, Math.max(0.05, Math.min(0.95, 1 - y / 0.62))],
    (_x, y) => y > 0.52 ? [[leg.bone, leg.tb, 0, 0], [0.5, 0.5, 0, 0]] : [[leg.bone, 0, 0, 0], [1, 0, 0, 0]],
    (_x, y) => 0.76 + 0.24 * Math.min(1, y / 0.62)
  );
}

const bufSize = 4 + 4 + BONES.length * 12 + 4 + 4 + verts.length * 56 + indices.length * 2;
const out = Buffer.alloc(bufSize);
let o = 0;
out.write('SKN1', o, 'ascii'); o += 4;
out.writeInt32LE(BONES.length, o); o += 4;
for (const [bx, by, bz] of BONES) { out.writeFloatLE(bx, o); out.writeFloatLE(by, o + 4); out.writeFloatLE(bz, o + 8); o += 12; }
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
