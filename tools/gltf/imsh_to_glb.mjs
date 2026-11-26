// Convert an IMSH pack back to a grouped GLB for inspection/preview.
// Usage: node imsh_to_glb.mjs input.bin output.glb [--filter regex]
// This is an inspection tool, not the production exporter: triangles are split into one child mesh per material slot.
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { readImsh, decodePart } from './imsh_lib.mjs';
import './lib.mjs'; // FileReader polyfill required by GLTFExporter in Node

const [inputArg, outputArg, ...rest] = process.argv.slice(2);
if (!inputArg || !outputArg) {
  console.error('usage: node imsh_to_glb.mjs input.bin output.glb [--filter regex]');
  process.exit(2);
}
let filter = null;
for (let i = 0; i < rest.length; i++) if (rest[i] === '--filter') filter = new RegExp(rest[++i]);
const input = resolve(inputArg), output = resolve(outputArg);
const parts = readImsh(input);
const scene = new THREE.Scene();
let written = 0, tris = 0;
for (const [name, buf] of parts) {
  if (filter && !filter.test(name)) continue;
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
    const mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.8 });
    mat.userData.materialId = matId;
    const mesh = new THREE.Mesh(g, mat);
    mesh.name = `${name}_m${matId}`;
    root.add(mesh);
    tris += list.length / 3;
  }
  scene.add(root); written++;
}
const glb = await new GLTFExporter().parseAsync(scene, { binary: true });
mkdirSync(dirname(output), { recursive: true });
writeFileSync(output, Buffer.from(glb));
console.log(`${output}: ${written} parts, ${tris} tris, ${(glb.byteLength / 1024).toFixed(1)} KB`);
