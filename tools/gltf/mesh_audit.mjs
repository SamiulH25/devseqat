// Audit Ironfield's modular mesh packs against parts.manifest.json.
// Usage:
//   node mesh_audit.mjs <units3d.bin|parts.glb> [--manifest parts.manifest.json] [--partial] [--strict] [--json report.json]
// Errors are contract violations. Warnings are cost/quality targets; --strict makes warnings fail too.
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseGlb, extractPartsFromGlb, readImshBuffer, decodePart } from './imsh_lib.mjs';

const here = dirname(fileURLToPath(import.meta.url));
function usage() {
  console.error('usage: node mesh_audit.mjs <units3d.bin|parts.glb> [--manifest parts.manifest.json] [--partial] [--strict] [--json report.json]');
  process.exit(2);
}
const args = process.argv.slice(2);
if (!args.length) usage();
const input = resolve(args[0]);
let manifestPath = resolve(here, 'parts.manifest.json');
let partial = false, strict = false, jsonOut = null;
for (let i = 1; i < args.length; i++) {
  if (args[i] === '--manifest') manifestPath = resolve(args[++i]);
  else if (args[i] === '--partial') partial = true;
  else if (args[i] === '--strict') strict = true;
  else if (args[i] === '--json') jsonOut = resolve(args[++i]);
  else usage();
}
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
const errors = [], warnings = [], infos = [];
const glbIssues = [];
let parts, glb = null, sourceKind;

if (input.toLowerCase().endsWith('.glb')) {
  sourceKind = 'glb';
  glb = parseGlb(input);
  inspectGlbStructure(glb, glbIssues);
  parts = extractPartsFromGlb(glb);
  const extracted = new Set(parts.keys());
  const scene = glb.json.scenes?.[glb.json.scene ?? 0];
  for (const rootIdx of scene?.nodes ?? []) {
    const n = glb.json.nodes[rootIdx];
    if (n?.name && !extracted.has(n.name)) glbIssues.push(`root part '${n.name}' produced no triangles`);
  }
} else {
  sourceKind = 'imsh';
  const pack = readImshBuffer(readFileSync(input), input);
  parts = pack.parts;
  if (pack.trailing) warnings.push(`${input}: ${pack.trailing} trailing bytes after the last part`);
  const seen = new Set();
  for (const e of pack.entries) {
    if (seen.has(e.name)) errors.push(`duplicate part '${e.name}' in IMSH pack`);
    seen.add(e.name);
  }
}
for (const x of glbIssues) errors.push(x);

function inspectGlbStructure(glb, out) {
  const j = glb.json;
  const unsupported = (j.extensionsRequired ?? []).filter(x => x !== 'KHR_materials_unlit');
  if (unsupported.length) out.push(`unsupported required glTF extensions: ${unsupported.join(', ')} (no Draco/meshopt/quantization in this pipeline)`);
  if (j.buffers?.some(b => b.uri)) out.push('external glTF buffers are unsupported; write a self-contained binary GLB');
  if (j.images?.length || j.textures?.length) out.push('textures/images are unsupported: IMSH keeps only material slot ids and grayscale COLOR_0 AO');
  if (j.skins?.length) out.push('skins are unsupported: IMSH is rigid modular parts, not skinned meshes');
  if (j.animations?.length) out.push('animations are unsupported: articulation lives in Puppet3D.cs, not in mesh data');
  const scene = j.scenes?.[j.scene ?? 0];
  if (!scene?.nodes?.length) out.push('GLB has no scene root nodes; each root node must be one named part');
  const names = new Set();
  for (const rootIdx of scene?.nodes ?? []) {
    const root = j.nodes[rootIdx];
    if (!root?.name) {
      out.push(`unnamed scene root node ${rootIdx}; root names become IMSH part names`);
      continue;
    }
    if (names.has(root.name)) out.push(`duplicate scene root part name '${root.name}'`);
    names.add(root.name);
    if (!identityTransform(root)) out.push(`root part '${root.name}' has a non-identity transform; keep pivots in vertex data and root transforms identity`);
  }
  const accessorSparse = new Set();
  for (const mesh of j.meshes ?? []) {
    for (const prim of mesh.primitives ?? []) {
      if ((prim.mode ?? 4) !== 4) out.push(`primitive mode ${prim.mode} is unsupported; export triangles only`);
      for (const req of ['POSITION', 'NORMAL', 'COLOR_0']) {
        if (prim.attributes?.[req] === undefined) out.push(`mesh primitive lacks ${req}; the IMSH contract requires it`);
      }
      for (const accIdx of Object.values(prim.attributes ?? {})) {
        if (j.accessors?.[accIdx]?.sparse && !accessorSparse.has(accIdx)) {
          accessorSparse.add(accIdx);
          out.push(`sparse accessor ${accIdx} is unsupported`);
        }
      }
      if (prim.material !== undefined) {
        const mat = j.materials?.[prim.material];
        const id = mat?.extras?.materialId;
        if (!Number.isInteger(id) || id < 0 || id > 13) out.push(`material '${mat?.name ?? prim.material}' has extras.materialId ${id}; expected an integer 0..13`);
      }
    }
  }
}
function identityTransform(n) {
  if (n.matrix) return n.matrix.every((v, i) => Math.abs(v - [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1][i]) < 1e-7);
  const t = n.translation ?? [0,0,0], r = n.rotation ?? [0,0,0,1], s = n.scale ?? [1,1,1];
  return t.every(v => Math.abs(v) < 1e-7) && r.every((v, i) => Math.abs(v - (i === 3 ? 1 : 0)) < 1e-7) && s.every(v => Math.abs(v - 1) < 1e-7);
}

function analysePart(name, buf) {
  const verts = decodePart(buf);
  const st = {
    name, verts: verts.length, tris: verts.length / 3,
    min: [Infinity, Infinity, Infinity], max: [-Infinity, -Infinity, -Infinity],
    badFinite: 0, badNormal: 0, mixedMatTris: 0, nonGray: 0, badMat: 0, degenerate: 0,
    materials: new Set(),
  };
  if (verts.length % 3) errors.push(`${name}: vertex count ${verts.length} is not a multiple of 3`);
  for (const v of verts) {
    for (let k = 0; k < 3; k++) {
      if (!Number.isFinite(v.p[k]) || !Number.isFinite(v.n[k])) st.badFinite++;
      st.min[k] = Math.min(st.min[k], v.p[k]); st.max[k] = Math.max(st.max[k], v.p[k]);
    }
    const nl = Math.hypot(...v.n);
    if (!Number.isFinite(nl) || Math.abs(nl - 1) > 0.025) st.badNormal++;
    if (!Number.isInteger(Math.round(v.mat)) || Math.round(v.mat) < 0 || Math.round(v.mat) > 13 || Math.abs(v.mat - Math.round(v.mat)) > 0.01) st.badMat++;
    else st.materials.add(Math.round(v.mat));
    const [r, g, b] = v.rgba;
    if (!(r === g && g === b)) st.nonGray++;
  }
  for (let i = 0; i + 2 < verts.length; i += 3) {
    const ms = new Set([0,1,2].map(k => Math.round(verts[i+k].mat)));
    if (ms.size !== 1) st.mixedMatTris++;
    const a = verts[i].p, b = verts[i+1].p, c = verts[i+2].p;
    const ux = b[0]-a[0], uy = b[1]-a[1], uz = b[2]-a[2];
    const vx = c[0]-a[0], vy = c[1]-a[1], vz = c[2]-a[2];
    const cx = uy*vz-uz*vy, cy = uz*vx-ux*vz, cz = ux*vy-uy*vx;
    if (cx*cx+cy*cy+cz*cz < 1e-12) st.degenerate++;
  }
  return st;
}

const stats = new Map();
for (const [name, buf] of parts.entries()) stats.set(name, analysePart(name, buf));

// Inventory and per-part ratchet budgets.
const expected = manifest.parts ?? {};
for (const [name, meta] of Object.entries(expected)) {
  if (!stats.has(name) && !partial) errors.push(`missing required part '${name}' (${meta.module ?? 'unknown module'})`);
}
for (const [name, st] of stats.entries()) {
  const meta = expected[name];
  if (!meta) {
    errors.push(`unknown part '${name}': add a manifest entry with module, pivot and maxTris, or remove it`);
    continue;
  }
  if (meta.maxTris !== undefined && st.tris > meta.maxTris)
    errors.push(`${name}: ${st.tris} tris exceeds ratchet max ${meta.maxTris}; reduce it or deliberately reset the ratchet in parts.manifest.json`);
  if (meta.status === 'legacy-unused') infos.push(`${name}: marked ${meta.status}; do not spend new effort here without a runtime use`);
}

// Data integrity.
let degenerateTotal = 0;
const degenerateParts = [];
for (const st of stats.values()) {
  if (st.badFinite) errors.push(`${st.name}: ${st.badFinite} non-finite position/normal components`);
  if (st.badNormal) errors.push(`${st.name}: ${st.badNormal} normals are not unit length`);
  if (st.badMat) errors.push(`${st.name}: ${st.badMat} vertices have material ids outside integer 0..13`);
  if (st.mixedMatTris) errors.push(`${st.name}: ${st.mixedMatTris} triangles mix material ids inside one triangle`);
  if (st.nonGray) errors.push(`${st.name}: ${st.nonGray} COLOR_0 vertices are not grayscale; COLOR_0 is AO only, never albedo`);
  if (st.degenerate) {
    degenerateTotal += st.degenerate;
    degenerateParts.push([st.degenerate, st.name]);
  }
}
if (degenerateTotal) {
  degenerateParts.sort((a,b) => b[0]-a[0]);
  warnings.push(`${degenerateTotal} degenerate triangles waste vertices (worst: ${degenerateParts.slice(0,5).map(([n,p]) => `${p}:${n}`).join(', ')}); collapse zero-radius rings/caps where practical`);
}
const beastUnsafe = [];
for (const st of stats.values()) {
  if ((expected[st.name]?.module) !== 'beasts') continue;
  const bad = [...st.materials].filter(m => ![0, 11, 12, 13].includes(m)).sort((a,b) => a-b);
  if (bad.length) beastUnsafe.push(`${st.name}(${bad.join('/')})`);
}
if (beastUnsafe.length)
  warnings.push(`beast-module parts use material slots outside the safe beast contract 0/11/12/13: ${beastUnsafe.join(', ')}; UnitMatTable.FillBeast does not define human/team slots, so either remap these parts or deliberately extend FillBeast`);

// Pivot contract checks. These are deliberately broad: they catch wrong spaces without dictating sculpting.
function pivotError(st, why) { errors.push(`${st.name}: pivot ${manifest.parts[st.name]?.pivot} violation: ${why} (bounds x ${f(st.min[0])}..${f(st.max[0])}, y ${f(st.min[1])}..${f(st.max[1])}, z ${f(st.min[2])}..${f(st.max[2])})`); }
const f = x => x.toFixed(2);
for (const st of stats.values()) {
  const pivot = manifest.parts[st.name]?.pivot;
  if (!pivot) continue;
  const [minX,minY,minZ] = st.min, [maxX,maxY,maxZ] = st.max;
  if (pivot === 'unitBone') {
    if (!(minY >= -0.20 && minY <= 0.15 && maxY >= 0.85 && maxY <= 1.30 && Math.max(Math.abs(minX), Math.abs(maxX), Math.abs(minZ), Math.abs(maxZ)) <= 0.60))
      pivotError(st, 'unitBone parts must start at the joint near y=0, be length-normalised to y≈1, and stay within 0.60 m laterally');
  } else if (pivot === 'headJoint') {
    const originInsideX = minX <= 0.15 && maxX >= -0.15, originInsideZ = minZ <= 0.15 && maxZ >= -0.15;
    if (!(minY <= 0.06 && minY >= -0.55 && maxY <= 0.60 && originInsideX && originInsideZ && Math.max(Math.abs(minX), Math.abs(maxX), Math.abs(minZ), Math.abs(maxZ)) <= 0.70)) pivotError(st, 'head/helm parts must be authored around the neck/head joint within the beast-head envelope');
  } else if (pivot === 'grip') {
    if (!(minY <= 0.06 && minY >= -1.10 && maxY >= 0.25 && maxY <= 2.80 && Math.max(Math.abs(minX), Math.abs(maxX), Math.abs(minZ), Math.abs(maxZ)) <= 0.45)) pivotError(st, 'weapons must put the grip/rear end at y=0, extend along +Y, and stay inside the weapon envelope');
  } else if (pivot === 'shieldFace') {
    if (!(minZ <= 0.02 && minZ >= -0.02 && maxZ >= 0.04 && maxZ <= 0.25 && Math.max(Math.abs(minX), Math.abs(maxX)) <= 0.40 && Math.max(Math.abs(minY), Math.abs(maxY)) <= 0.65)) pivotError(st, 'shield decorated faces must point +Z from a central pivot inside the shield envelope');
  } else if (pivot === 'bodySpace') {
    if (!(minY >= 0.05 && maxY <= 1.65 && Math.max(Math.abs(minX), Math.abs(maxX)) <= 0.55 && minZ >= -0.55 && maxZ <= 0.45)) pivotError(st, 'humanoid armour layers live in final body heights above the feet origin inside the body envelope');
  } else if (pivot === 'groundFigure') {
    if (!(Math.abs(minY) <= 0.08 && maxY >= 1.4)) pivotError(st, 'far LOD figures start at ground y=0 and include the whole figure height');
  }
}

// Static runtime references. This is intentionally conservative: it catches direct kit.Get calls and the literal
// part names used by Mesh3D's mapping switches; dynamically composed limb names are covered by the inventory itself.
const runtimeRefs = new Map();
const repoRoot = resolve(here, '../..');
for (const rel of ['Mesh3D.cs', 'Puppet3D.cs', 'Puppet3D.Beasts.cs']) {
  let src;
  try { src = readFileSync(resolve(repoRoot, rel), 'utf8'); } catch { continue; }
  for (const m of src.matchAll(/kit\.Get\("([^"]+)"\)/g)) runtimeRefs.set(m[1], rel);
  if (rel === 'Mesh3D.cs')
    for (const m of src.matchAll(/"((?:chest|shld|belt|skirt|head|helm|uarm|farm|thigh|shin|weapon|shield|emblem|horse|camel|wolf|bear|spider|wyvern|boar|harpy|griffon|basilisk|wurm|werewolf|brute|lod|quad|plume|cape|quiver|web|banner)_[a-z0-9_]+)"/g)) runtimeRefs.set(m[1], rel);
}
for (const [part, rel] of runtimeRefs) {
  if (!stats.has(part) && !partial) errors.push(`runtime reference '${part}' in ${rel} has no mesh part`);
}

// Cost totals and assembled-rig budgets.
const totalTris = [...stats.values()].reduce((n, st) => n + st.tris, 0);
if (manifest.budgets?.total?.maxTris !== undefined && totalTris > manifest.budgets.total.maxTris)
  errors.push(`pack total ${totalTris} tris exceeds ratchet max ${manifest.budgets.total.maxTris}`);
if (manifest.budgets?.total?.targetTris !== undefined && totalTris > manifest.budgets.total.targetTris)
  warnings.push(`pack total ${totalTris} tris is above target ${manifest.budgets.total.targetTris} (ratchet max ${manifest.budgets.total.maxTris})`);
const kitRows = [];
for (const [kitName, kit] of Object.entries(manifest.kits ?? {})) {
  let cost = 0, missing = [];
  for (const [part, count] of Object.entries(kit.parts ?? {})) {
    const st = stats.get(part);
    if (!st) missing.push(part); else cost += st.tris * count;
  }
  if (missing.length) {
    if (!partial) warnings.push(`${kitName}: skipped rig cost check; missing ${missing.join(', ')}`);
    continue;
  }
  kitRows.push({ kit: kitName, tris: cost, target: kit.targetTris, max: kit.maxTris });
  if (cost > kit.maxTris) errors.push(`${kitName}: assembled rig costs ${cost} tris, above ratchet max ${kit.maxTris}`);
  else if (cost > kit.targetTris) warnings.push(`${kitName}: assembled rig costs ${cost} tris, above target ${kit.targetTris} (ratchet max ${kit.maxTris})`);
}

const top = [...stats.values()].sort((a,b) => b.tris - a.tris).slice(0, 12).map(st => ({ part: st.name, tris: st.tris, max: expected[st.name]?.maxTris }));
const report = { input, sourceKind, parts: stats.size, totalTris, errors, warnings, infos, kits: kitRows, top };
if (jsonOut) writeFileSync(jsonOut, JSON.stringify(report, null, 2) + '\n');

console.log(`mesh audit: ${input}`);
console.log(`  ${stats.size} parts, ${totalTris} tris, source ${sourceKind}${partial ? ' (partial inventory allowed)' : ''}`);
if (kitRows.length) {
  console.log('  rig costs (cost/target/ratchet max):');
  for (const r of kitRows) console.log(`    ${r.kit.padEnd(18)} ${String(r.tris).padStart(4)} / ${r.target} / ${r.max}`);
}
console.log('  costliest parts:');
for (const r of top) console.log(`    ${r.part.padEnd(28)} ${String(r.tris).padStart(4)} / max ${r.max ?? '?'}`);
for (const x of infos) console.log(`  info: ${x}`);
for (const x of warnings) console.log(`  warning: ${x}`);
for (const x of errors) console.error(`  error: ${x}`);
console.log(`  ${errors.length} errors, ${warnings.length} warnings`);
if (errors.length || (strict && warnings.length)) process.exit(1);
