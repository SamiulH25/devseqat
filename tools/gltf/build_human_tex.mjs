// Generate 128x128 PointClamp pixel-art detail atlas for humanoid troops (Data/meshes/human_tex.png).
// 4x2 grid of 32x32 tiles:
//   (0,0) Smooth/Plate   (1,0) Chainmail    (2,0) Scale/Lamellar   (3,0) Quilted/Cloth
//   (0,1) Brushed/Wood   (1,1) Human Face   (2,1) Fur                (3,1) White/Neutral
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';

const out = resolve('../../Data/meshes/human_tex.png');
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

// Fill neutral white by default
for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) setPx(x, y, 245, 245, 245);

// Tile (0,0): Plate / Smooth (subtle hammered sheen + crisp plate border & corner rivets)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const h = hash(x >> 1, y >> 1);
  let v = 238 + (h > 0.72 ? 14 : h < 0.22 ? -14 : 0);
  if (x === 0 || y === 0) v = 200;
  if ((x === 3 && y === 3) || (x === 28 && y === 3) || (x === 3 && y === 28) || (x === 28 && y === 28)) v = 255;
  setPx(x, y, v, v, v);
}

// Tile (1,0): Chainmail (crisp 4x4 interlocking pixel-art rings)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const row = (y >> 2) & 1;
  const lx = (x + row * 2) & 3;
  const ly = y & 3;
  let v = 232;
  if ((lx === 1 || lx === 2) && (ly === 0 || ly === 3)) v = 255;
  else if ((lx === 0 || lx === 3) && (ly === 1 || ly === 2)) v = 248;
  else if ((lx === 1 || lx === 2) && (ly === 1 || ly === 2)) v = 165;
  else v = 195;
  setPx(32 + x, y, v, v, v);
}

// Tile (2,0): Scale / Lamellar (8x8 overlapping pixel-art scales)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const row = (y >> 3) & 1;
  const lx = (x + row * 4) & 7;
  const ly = y & 7;
  let v = 235;
  if (ly === 0) v = 255;
  else if (ly === 7 || (ly >= 5 && (lx === 0 || lx === 7))) v = 168;
  else if (lx === 0 || lx === 7) v = 192;
  else if (ly === 2 && lx === 3) v = 252; // tiny lacing/rivet highlight
  setPx(64 + x, y, v, v, v);
}

// Tile (3,0): Quilted / Woven Cloth (crisp diagonal pixel-art gambeson stitch)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const d1 = (x + y) & 7;
  const d2 = (x - y + 32) & 7;
  let v = 242;
  if (d1 === 0 || d2 === 0) v = 204;
  else if (d1 === 1 || d2 === 1) v = 252;
  setPx(96 + x, y, v, v, v);
}

// Tile (0,1): Brushed Steel / Wood Grain
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const s = hash(x >> 2, y);
  const v = 236 + (s > 0.7 ? 16 : s < 0.25 ? -18 : 0);
  setPx(x, 32 + y, v, v, v);
}

// Tile (1,1): Human Face (32x32 centered at x=48, y=48; u=0.25..0.50, v=0.25..0.50)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  let r = 245, g = 240, b = 238;
  // Subtle cheek blush & nose bridge
  if (Math.abs(x - 15.5) < 1.5 && y >= 13 && y <= 20) { r = 232; g = 216; b = 210; }
  // Eyebrows (y = 10..11)
  if ((y === 10 || y === 11) && ((x >= 8 && x <= 12) || (x >= 19 && x <= 23))) {
    r = 95; g = 72; b = 56;
  }
  // Crisp pixel eyes (y = 13..15): white sclera + dark pupil
  if (y >= 13 && y <= 15) {
    if (x === 9 || x === 22) { r = 255; g = 255; b = 255; } // sclera
    if (x === 10 || x === 11 || x === 20 || x === 21) { r = 38; g = 34; b = 42; } // iris/pupil
    if (y === 13 && (x === 10 || x === 20)) { r = 210; g = 225; b = 240; } // catchlight
  }
  // Mouth / mustache shadow (y = 23)
  if (y === 23 && x >= 12 && x <= 19) { r = 156; g = 118; b = 106; }
  setPx(32 + x, 32 + y, r, g, b);
}

// PNG writer
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
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(Buffer.concat([t, data])), 0);
  return Buffer.concat([len, t, data, crc]);
}
const raw = Buffer.alloc(H * (1 + W * 4));
for (let y = 0; y < H; y++) {
  raw[y * (1 + W * 4)] = 0;
  Buffer.from(rgba.buffer, y * W * 4, W * 4).copy(raw, y * (1 + W * 4) + 1);
}
const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4); ihdr[8] = 8; ihdr[9] = 6;
const png = Buffer.concat([
  Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
  pngChunk('IHDR', ihdr),
  pngChunk('IDAT', deflateSync(raw)),
  pngChunk('IEND', Buffer.alloc(0)),
]);
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, png);
console.log(`Wrote ${out} (${W}x${H})`);
