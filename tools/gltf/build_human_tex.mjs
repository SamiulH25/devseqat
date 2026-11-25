// Generate 128x128 PointClamp pixel-art equipment variety atlas (Data/meshes/human_tex.png).
// 4x4 grid of 32x32 tiles (patId 0..15):
//   0: Smooth Plate     1: Chainmail        2: Scale/Lamellar   3: Quilted Cloth
//   4: Brushed Steel    5: Fur Tufts        6: Fluted Plate     7: Riveted Plate
//   8: Battleworn Steel 9: Heavy Mail      10: Studded Scale   11: Herringbone Wool
//  12: Padded Aketon   13: Woven Brocade   14: Oak Wood Planks 15: Tooled Leather
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';

const out = resolve('../../Data/meshes/human_tex.png');
const W = 128, H = 128;
const rgba = new Uint8Array(W * H * 4);
function setTilePx(tid, x, y, r, g, b, a = 255) {
  const tx = (tid % 4) * 32, ty = Math.floor(tid / 4) * 32;
  const px = tx + (x & 31), py = ty + (y & 31);
  const o = (py * W + px) * 4;
  rgba[o] = Math.max(0, Math.min(255, r | 0));
  rgba[o + 1] = Math.max(0, Math.min(255, g | 0));
  rgba[o + 2] = Math.max(0, Math.min(255, b | 0));
  rgba[o + 3] = a;
}
function hash(x, y) {
  const n = Math.sin(x * 127.1 + y * 311.7) * 43758.5453;
  return n - Math.floor(n);
}

// 0: Smooth hammered plate + subtle corner rivets
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const h = hash(x >> 1, y >> 1);
  let v = 240 + (h > 0.72 ? 12 : h < 0.22 ? -12 : 0);
  if (x === 0 || y === 0) v = 205;
  if ((x === 3 && y === 3) || (x === 28 && y === 3) || (x === 3 && y === 28) || (x === 28 && y === 28)) v = 255;
  setTilePx(0, x, y, v, v, v);
}

// 1: Chainmail (4x4 interlocking rings)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const row = (y >> 2) & 1, lx = (x + row * 2) & 3, ly = y & 3;
  let v = 232;
  if ((lx === 1 || lx === 2) && (ly === 0 || ly === 3)) v = 255;
  else if ((lx === 0 || lx === 3) && (ly === 1 || ly === 2)) v = 248;
  else if ((lx === 1 || lx === 2) && (ly === 1 || ly === 2)) v = 165;
  else v = 195;
  setTilePx(1, x, y, v, v, v);
}

// 2: Scale / Lamellar (8x8 overlapping plates)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const row = (y >> 3) & 1, lx = (x + row * 4) & 7, ly = y & 7;
  let v = 235;
  if (ly === 0) v = 255;
  else if (ly === 7 || (ly >= 5 && (lx === 0 || lx === 7))) v = 168;
  else if (lx === 0 || lx === 7) v = 192;
  else if (ly === 2 && lx === 3) v = 252;
  setTilePx(2, x, y, v, v, v);
}

// 3: Quilted / Woven Cloth (diagonal gambeson stitch)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const d1 = (x + y) & 7, d2 = (x - y + 32) & 7;
  let v = 242;
  if (d1 === 0 || d2 === 0) v = 202;
  else if (d1 === 1 || d2 === 1) v = 253;
  setTilePx(3, x, y, v, v, v);
}

// 4: Brushed Steel / Wood Grain
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const s = hash(x >> 2, y);
  const v = 236 + (s > 0.7 ? 16 : s < 0.25 ? -18 : 0);
  setTilePx(4, x, y, v, v, v);
}

// 5: Fur Tufts
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const tuft = hash(x >> 1, y >> 2);
  const v = 232 + (tuft > 0.65 ? 20 : tuft < 0.25 ? -28 : 0);
  setTilePx(5, x, y, v, v, v);
}

// 6: Fluted Gothic Plate (crisp vertical flutes with specular ridges & shadow valleys)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const fx = x & 7;
  let v = 234;
  if (fx === 0 || fx === 7) v = 186;
  else if (fx === 3 || fx === 4) v = 255;
  else if (fx === 2 || fx === 5) v = 244;
  if (y === 0 || y === 16) v = Math.min(255, v - 24);
  setTilePx(6, x, y, v, v, v);
}

// 7: Riveted Plate / Brigandine (horizontal lame seams + bold rivet rows)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const ly = y & 7, lx = x & 7;
  let v = 238;
  if (ly === 0) v = 178;
  else if (ly === 1) v = 254;
  if ((ly === 3 || ly === 4) && (lx === 3 || lx === 4)) v = 255;
  if (ly === 5 && (lx === 3 || lx === 4)) v = 172;
  setTilePx(7, x, y, v, v, v);
}

// 8: Battleworn / Scratched Steel (diagonal nicks, edge wear, patina)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const h = hash(x, y);
  let v = 232 + (h > 0.8 ? 16 : h < 0.15 ? -26 : 0);
  if (((x * 2 + y) & 15) === 3 && (y & 7) < 5) v = 174;
  if (((x * 2 + y) & 15) === 4 && (y & 7) < 5) v = 254;
  setTilePx(8, x, y, v, v, v);
}

// 9: Heavy / Dark-Ringed Mail (6x6 bold hauberk links)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const row = Math.floor(y / 5) & 1;
  const lx = (x + row * 3) % 6, ly = y % 5;
  let v = 224;
  if (ly === 0 || lx === 0) v = 152;
  else if (ly === 1 && lx >= 2 && lx <= 4) v = 255;
  else if (lx === 2 && ly === 2) v = 248;
  setTilePx(9, x, y, v, v, v);
}

// 10: Studded / Ornate Scale (bright center stud on every scale)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const row = (y >> 3) & 1, lx = (x + row * 4) & 7, ly = y & 7;
  let v = 230;
  if (ly === 0) v = 255;
  else if (ly === 7 || lx === 0 || lx === 7) v = 162;
  if (ly >= 2 && ly <= 4 && lx >= 3 && lx <= 4) v = 255;
  setTilePx(10, x, y, v, v, v);
}

// 11: Herringbone Wool Surcoat
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const band = (x >> 2) & 1;
  const diag = ((band ? x + y : x - y + 32) & 3);
  let v = 238;
  if (diag === 0) v = 204;
  else if (diag === 1) v = 252;
  setTilePx(11, x, y, v, v, v);
}

// 12: Padded Vertical-Channel Aketon / Tabard
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const cx = x & 7;
  let v = 246;
  if (cx === 0) v = 186;
  else if (cx === 1 || cx === 7) v = 218;
  else if (cx === 4) v = 255;
  setTilePx(12, x, y, v, v, v);
}

// 13: Noble Woven Check / Brocade
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const sq = (((x >> 2) + (y >> 2)) & 1);
  let v = sq ? 248 : 218;
  if ((x & 3) === 0 && (y & 3) === 0) v = 255;
  setTilePx(13, x, y, v, v, v);
}

// 14: Oak Wood Planks (shields & hafts)
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const grain = hash(x, y >> 2);
  let v = 234 + (grain > 0.7 ? 16 : grain < 0.25 ? -20 : 0);
  if ((x & 7) === 0) v = 165; // plank seam
  if ((x & 7) === 1) v = 250; // plank bevel highlight
  setTilePx(14, x, y, v, v, v);
}

// 15: Tooled / Stitched Leather
for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
  const pebble = hash(x >> 1, y >> 1);
  let v = 234 + (pebble > 0.65 ? 14 : pebble < 0.25 ? -16 : 0);
  if ((y === 2 || y === 29) && (x & 3) < 2) v = 254; // saddle stitch
  setTilePx(15, x, y, v, v, v);
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
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), pngChunk('IHDR', ihdr), pngChunk('IDAT', deflateSync(raw)), pngChunk('IEND', Buffer.alloc(0))]));
console.log(`Wrote ${out} (${W}x${H}, 16 equipment tiles)`);
