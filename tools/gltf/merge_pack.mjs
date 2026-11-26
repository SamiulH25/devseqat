// Merge an experimental partial IMSH/GLB pack over the installed stock pack for real-renderer preview.
// Usage: node merge_pack.mjs overlay.bin|overlay.glb merged.bin [--base ../../Data/meshes/units3d.bin] [--audit]
// IRONFIELD_MESH_PACK replaces the whole pack, so experimental previews should normally use this merged output.
import { spawnSync } from 'node:child_process';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readImsh, extractPartsFromGlb, writeImsh } from './imsh_lib.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const [overlayArg, outArg, ...rest] = process.argv.slice(2);
if (!overlayArg || !outArg) {
  console.error('usage: node merge_pack.mjs overlay.bin|overlay.glb merged.bin [--base stock-units3d.bin] [--audit]');
  process.exit(2);
}
let base = resolve(here, '../../Data/meshes/units3d.bin'), audit = false;
for (let i = 0; i < rest.length; i++) {
  if (rest[i] === '--base') base = resolve(rest[++i]);
  else if (rest[i] === '--audit') audit = true;
  else { console.error('unknown option ' + rest[i]); process.exit(2); }
}
const overlayPath = resolve(overlayArg), out = resolve(outArg);
const stock = readImsh(base);
const overlay = overlayPath.toLowerCase().endsWith('.glb') ? extractPartsFromGlb(overlayPath) : readImsh(overlayPath);
let replaced = 0, added = 0;
for (const [name, buf] of overlay) {
  if (stock.has(name)) replaced++; else added++;
  stock.set(name, buf);
}
const stats = writeImsh(out, stock);
console.log(`${out}: ${stats.parts} parts (${replaced} replaced, ${added} added), ${stats.tris} tris, ${stats.kb} KB`);
if (audit) {
  const r = spawnSync(process.execPath, [resolve(here, 'mesh_audit.mjs'), out], { stdio: 'inherit' });
  if (r.status !== 0) process.exit(r.status ?? 1);
}
