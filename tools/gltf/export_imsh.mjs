// Compile one or more .glb files into Ironfield's IMSH v1 binary format (Data/meshes/units3d.bin).
// Usage:
//   node export_imsh.mjs --out ../../Data/meshes/units3d.bin [--merge ../../Data/meshes/units3d.bin] input.glb [...]
//
// Each root node in a .glb scene becomes one IMSH part (named by node.name).
// Child nodes under a root part have their local transforms baked into the part's vertices/normals.
// Material slot id is read from material.extras.materialId (default 0).
import { resolve } from 'node:path';
import { readImsh, extractPartsFromGlb, writeImsh } from './imsh_lib.mjs';

function parseArgs(argv) {
  let out = null, merge = null;
  const inputs = [];
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--out') out = argv[++i];
    else if (argv[i] === '--merge') merge = argv[++i];
    else inputs.push(argv[i]);
  }
  if (!out || !inputs.length) {
    console.error('Usage: node export_imsh.mjs --out <out.bin> [--merge <base.bin>] <in.glb> [...]');
    process.exit(1);
  }
  return { out: resolve(out), merge: merge ? resolve(merge) : null, inputs: inputs.map(p => resolve(p)) };
}

const { out, merge, inputs } = parseArgs(process.argv.slice(2));
const parts = merge ? readImsh(merge) : new Map();
let replaced = 0, added = 0;
for (const inp of inputs) {
  const extracted = extractPartsFromGlb(inp);
  for (const [name, vbuf] of extracted.entries()) {
    if (parts.has(name)) replaced++; else added++;
    parts.set(name, vbuf);
    console.log(`  ${name}: ${vbuf.length / 36 / 3} tris`);
  }
}
const stats = writeImsh(out, parts);
console.log(`Wrote ${out}: ${stats.parts} parts (${replaced} replaced, ${added} added), ${stats.tris} tris, ${stats.kb} KB`);
