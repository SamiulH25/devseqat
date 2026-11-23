// Compile one or more .glb files into Ironfield's IMSH v1 binary format (Data/meshes/units3d.bin).
// Usage:
//   node export_imsh.mjs --out ../../Data/meshes/units3d.bin [--merge ../../Data/meshes/units3d.bin] input.glb [...]
//
// Each root node in a .glb scene becomes one IMSH part (named by node.name).
// Child nodes under a root part have their local transforms baked into the part's vertices/normals.
// Material slot id is read from material.extras.materialId (default 0).
import * as THREE from 'three';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

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

function readImsh(path) {
  const buf = readFileSync(path);
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  if (buf.toString('ascii', 0, 4) !== 'IMSH') throw new Error(`${path}: bad magic`);
  if (dv.getUint32(4, true) !== 1) throw new Error(`${path}: unsupported version`);
  const count = dv.getUint32(8, true);
  const parts = new Map();
  let o = 12;
  for (let p = 0; p < count; p++) {
    const nl = dv.getUint16(o, true); o += 2;
    const name = buf.toString('utf8', o, o + nl); o += nl;
    const vcount = dv.getUint32(o, true); o += 4;
    const bytes = vcount * 36;
    parts.set(name, buf.subarray(o, o + bytes));
    o += bytes;
  }
  return parts;
}

function parseGlb(path) {
  const buf = readFileSync(path);
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  if (dv.getUint32(0, true) !== 0x46546C67) throw new Error(`${path}: not a GLB`);
  let o = 12, json = null, bin = null;
  while (o < buf.length) {
    const len = dv.getUint32(o, true);
    const type = dv.getUint32(o + 4, true);
    const chunk = buf.subarray(o + 8, o + 8 + len);
    if (type === 0x4E4F534A) json = JSON.parse(chunk.toString('utf8'));
    else if (type === 0x004E4942) bin = chunk;
    o += 8 + len;
  }
  if (!json || !bin) throw new Error(`${path}: missing JSON or BIN chunk`);
  return { json, bin };
}

const COMP_BYTES = { 5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4 };
const TYPE_COMPS = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 };

function readAccessor({ json, bin }, accIdx) {
  const acc = json.accessors[accIdx];
  const bv = json.bufferViews[acc.bufferView];
  const nComp = TYPE_COMPS[acc.type];
  const cBytes = COMP_BYTES[acc.componentType];
  const stride = bv.byteStride || (nComp * cBytes);
  const base = (bv.byteOffset || 0) + (acc.byteOffset || 0);
  const dv = new DataView(bin.buffer, bin.byteOffset, bin.byteLength);
  const out = new Float32Array(acc.count * nComp);
  const norm = !!acc.normalized;
  for (let i = 0; i < acc.count; i++) {
    const off = base + i * stride;
    for (let k = 0; k < nComp; k++) {
      const p = off + k * cBytes;
      let v;
      switch (acc.componentType) {
        case 5126: v = dv.getFloat32(p, true); break;
        case 5125: v = dv.getUint32(p, true); break;
        case 5123: v = dv.getUint16(p, true); if (norm) v /= 65535; break;
        case 5121: v = dv.getUint8(p); if (norm) v /= 255; break;
        case 5122: v = dv.getInt16(p, true); if (norm) v = Math.max(-1, v / 32767); break;
        case 5120: v = dv.getInt8(p); if (norm) v = Math.max(-1, v / 127); break;
        default: throw new Error(`Unsupported componentType ${acc.componentType}`);
      }
      out[i * nComp + k] = v;
    }
  }
  return { data: out, nComp, count: acc.count };
}

function nodeMatrix(node) {
  const m = new THREE.Matrix4();
  if (node.matrix) {
    m.fromArray(node.matrix);
  } else {
    const t = node.translation ? new THREE.Vector3(...node.translation) : new THREE.Vector3();
    const r = node.rotation ? new THREE.Quaternion(...node.rotation) : new THREE.Quaternion();
    const s = node.scale ? new THREE.Vector3(...node.scale) : new THREE.Vector3(1, 1, 1);
    m.compose(t, r, s);
  }
  return m;
}

function extractPartsFromGlb(path) {
  const glb = parseGlb(path);
  const { json } = glb;
  const scene = json.scenes?.[json.scene ?? 0];
  if (!scene?.nodes) return new Map();
  const out = new Map();

  function collectNode(nodeIdx, parentMat, verts) {
    const node = json.nodes[nodeIdx];
    const worldMat = parentMat.clone().multiply(nodeMatrix(node));
    const normMat = new THREE.Matrix3().getNormalMatrix(worldMat);

    if (node.mesh !== undefined) {
      const mesh = json.meshes[node.mesh];
      for (const prim of mesh.primitives) {
        const posAcc = readAccessor(glb, prim.attributes.POSITION);
        const normAcc = prim.attributes.NORMAL !== undefined ? readAccessor(glb, prim.attributes.NORMAL) : null;
        const colAcc = prim.attributes.COLOR_0 !== undefined ? readAccessor(glb, prim.attributes.COLOR_0) : null;
        const idxAcc = prim.indices !== undefined ? readAccessor(glb, prim.indices) : null;
        const mat = prim.material !== undefined ? json.materials?.[prim.material] : null;
        const matId = mat?.extras?.materialId ?? 0;

        const triCount = idxAcc ? idxAcc.count : posAcc.count;
        const vTmp = new THREE.Vector3();
        const nTmp = new THREE.Vector3();

        const getIdx = i => idxAcc ? idxAcc.data[i] : i;
        for (let i = 0; i + 2 < triCount; i += 3) {
          const i0 = getIdx(i), i1 = getIdx(i + 1), i2 = getIdx(i + 2);
          // Fallback face normal if NORMAL missing
          let fn = null;
          if (!normAcc) {
            const p0 = new THREE.Vector3(posAcc.data[i0*3], posAcc.data[i0*3+1], posAcc.data[i0*3+2]).applyMatrix4(worldMat);
            const p1 = new THREE.Vector3(posAcc.data[i1*3], posAcc.data[i1*3+1], posAcc.data[i1*3+2]).applyMatrix4(worldMat);
            const p2 = new THREE.Vector3(posAcc.data[i2*3], posAcc.data[i2*3+1], posAcc.data[i2*3+2]).applyMatrix4(worldMat);
            fn = p1.sub(p0).cross(p2.sub(p0)).normalize();
          }
          for (const vi of [i0, i1, i2]) {
            vTmp.set(posAcc.data[vi*3], posAcc.data[vi*3+1], posAcc.data[vi*3+2]).applyMatrix4(worldMat);
            if (normAcc) {
              nTmp.set(normAcc.data[vi*3], normAcc.data[vi*3+1], normAcc.data[vi*3+2]).applyMatrix3(normMat).normalize();
            } else {
              nTmp.copy(fn);
            }
            let r = 255, g = 255, b = 255;
            if (colAcc) {
              const nc = colAcc.nComp;
              r = Math.max(0, Math.min(255, Math.round(colAcc.data[vi * nc] * 255)));
              g = Math.max(0, Math.min(255, Math.round(colAcc.data[vi * nc + 1] * 255)));
              b = Math.max(0, Math.min(255, Math.round(colAcc.data[vi * nc + 2] * 255)));
            }
            verts.push([vTmp.x, vTmp.y, vTmp.z, nTmp.x, nTmp.y, nTmp.z, matId, r, g, b]);
          }
        }
      }
    }
    if (node.children) {
      for (const c of node.children) collectNode(c, worldMat, verts);
    }
  }

  for (const rootIdx of scene.nodes) {
    const root = json.nodes[rootIdx];
    const partName = root.name;
    if (!partName) continue;
    const verts = [];
    // Note: root node's own transform is ignored if it's just a layout offset, OR baked?
    // Keep identity at root so each part stays in its own part-local pivot space!
    const idMat = new THREE.Matrix4();
    if (root.mesh !== undefined) {
      collectNode(rootIdx, idMat, verts);
    } else if (root.children) {
      for (const c of root.children) collectNode(c, idMat, verts);
    }
    if (verts.length > 0) {
      const buf = Buffer.allocUnsafe(verts.length * 36);
      const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
      for (let i = 0; i < verts.length; i++) {
        const [px, py, pz, nx, ny, nz, matId, r, g, b] = verts[i];
        const o = i * 36;
        dv.setFloat32(o, px, true);
        dv.setFloat32(o + 4, py, true);
        dv.setFloat32(o + 8, pz, true);
        dv.setFloat32(o + 12, nx, true);
        dv.setFloat32(o + 16, ny, true);
        dv.setFloat32(o + 20, nz, true);
        dv.setFloat32(o + 24, matId, true);
        dv.setFloat32(o + 28, 0, true);
        buf[o + 32] = r;
        buf[o + 33] = g;
        buf[o + 34] = b;
        buf[o + 35] = 255;
      }
      out.set(partName, buf);
    }
  }
  return out;
}

function writeImsh(path, partsMap) {
  const chunks = [];
  const hdr = Buffer.allocUnsafe(12);
  hdr.write('IMSH', 0, 'ascii');
  hdr.writeUInt32LE(1, 4);
  hdr.writeUInt32LE(partsMap.size, 8);
  chunks.push(hdr);

  let totalVerts = 0;
  for (const [name, vbuf] of partsMap.entries()) {
    const nb = Buffer.from(name, 'utf8');
    const ph = Buffer.allocUnsafe(2 + nb.length + 4);
    ph.writeUInt16LE(nb.length, 0);
    nb.copy(ph, 2);
    const vcount = vbuf.length / 36;
    ph.writeUInt32LE(vcount, 2 + nb.length);
    chunks.push(ph, vbuf);
    totalVerts += vcount;
  }
  mkdirSync(dirname(path), { recursive: true });
  const finalBuf = Buffer.concat(chunks);
  writeFileSync(path, finalBuf);
  return { parts: partsMap.size, tris: totalVerts / 3, kb: (finalBuf.length / 1024).toFixed(1) };
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
