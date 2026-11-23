// Modular Three.js part builders for Ironfield (Three.js -> .glb -> export_imsh.mjs -> units3d.bin).
// Usage:
//   node build_parts.mjs [out/parts.glb] [--only bodies,weapons,...]
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { triangles } from './lib.mjs';
import { buildBodies } from './parts/bodies.mjs';
import { buildLimbs } from './parts/limbs.mjs';
import { buildHeads } from './parts/heads.mjs';
import { buildWeapons } from './parts/weapons.mjs';
import { buildProps } from './parts/props.mjs';
import { buildMounts } from './parts/mounts.mjs';
import { buildBeasts } from './parts/beasts.mjs';

const MODULES = {
  bodies: buildBodies,
  limbs: buildLimbs,
  heads: buildHeads,
  weapons: buildWeapons,
  props: buildProps,
  mounts: buildMounts,
  beasts: buildBeasts,
};

let outArg = 'out/parts.glb';
let only = null;
for (let i = 2; i < process.argv.length; i++) {
  if (process.argv[i] === '--only') only = process.argv[++i].split(',').map(s => s.trim()).filter(Boolean);
  else outArg = process.argv[i];
}
const out = resolve(outArg);

const scene = new THREE.Scene();
const names = only ?? Object.keys(MODULES);
for (const name of names) {
  const fn = MODULES[name];
  if (!fn) throw new Error(`Unknown module: ${name}. Available: ${Object.keys(MODULES).join(', ')}`);
  fn(scene);
}

const glb = await new GLTFExporter().parseAsync(scene, { binary: true });
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, Buffer.from(glb));
console.log(`Wrote ${out} (${scene.children.length} parts, ${triangles(scene)} tris, ${(glb.byteLength / 1024).toFixed(1)} KB)`);
