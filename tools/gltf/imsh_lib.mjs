// Shared IMSH v1 / glTF plumbing for Ironfield's modular unit mesh pipeline.
// Keep this byte-for-byte compatible with export_imsh.mjs: export writes what Mesh3D.cs reads.
import * as THREE from 'three';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

export function readImsh(path) {
  const buf = readFileSync(path);
  return readImshBuffer(buf, path).parts;
}

export function readImshBuffer(buf, label = 'IMSH buffer') {
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  if (buf.toString('ascii', 0, 4) !== 'IMSH') throw new Error(`${label}: bad magic`);
  if (dv.getUint32(4, true) !== 1) throw new Error(`${label}: unsupported version`);
  const count = dv.getUint32(8, true);
  const parts = new Map();
  const entries = [];
  let o = 12;
  for (let p = 0; p < count; p++) {
    const nl = dv.getUint16(o, true); o += 2;
    const name = buf.toString('utf8', o, o + nl); o += nl;
    const vcount = dv.getUint32(o, true); o += 4;
    const bytes = vcount * 36;
    const data = buf.subarray(o, o + bytes);
    entries.push({ name, vcount, data });
    parts.set(name, data);
    o += bytes;
  }
  return { parts, entries, bytes: buf.length, trailing: buf.length - o };
}

export function decodePart(buf) {
  const count = Math.floor(buf.length / 36);
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  const out = new Array(count);
  for (let i = 0; i < count; i++) {
    const o = i * 36;
    out[i] = {
      p: [dv.getFloat32(o, true), dv.getFloat32(o + 4, true), dv.getFloat32(o + 8, true)],
      n: [dv.getFloat32(o + 12, true), dv.getFloat32(o + 16, true), dv.getFloat32(o + 20, true)],
      mat: dv.getFloat32(o + 24, true),
      aux: dv.getFloat32(o + 28, true),
      rgba: [buf[o + 32], buf[o + 33], buf[o + 34], buf[o + 35]],
    };
  }
  return out;
}

export function parseGlb(path) {
  const buf = readFileSync(path);
  return parseGlbBuffer(buf, path);
}

export function parseGlbBuffer(buf, label = 'GLB buffer') {
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
  if (dv.getUint32(0, true) !== 0x46546C67) throw new Error(`${label}: not a GLB`);
  let o = 12, json = null, bin = null;
  while (o < buf.length) {
    const len = dv.getUint32(o, true);
    const type = dv.getUint32(o + 4, true);
    const chunk = buf.subarray(o + 8, o + 8 + len);
    if (type === 0x4E4F534A) json = JSON.parse(chunk.toString('utf8'));
    else if (type === 0x004E4942) bin = chunk;
    o += 8 + len;
  }
  if (!json || !bin) throw new Error(`${label}: missing JSON or BIN chunk`);
  return { json, bin };
}

const COMP_BYTES = { 5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4 };
const TYPE_COMPS = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 };

export function readAccessor({ json, bin }, accIdx) {
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

export function nodeMatrix(node) {
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

export function extractPartsFromGlb(path) {
  const glb = typeof path === 'string' ? parseGlb(path) : path;
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

        const flipWind = worldMat.determinant() < 0;
        const getIdx = i => idxAcc ? idxAcc.data[i] : i;
        for (let i = 0; i + 2 < triCount; i += 3) {
          const i0 = getIdx(i);
          const i1 = getIdx(flipWind ? i + 2 : i + 1);
          const i2 = getIdx(flipWind ? i + 1 : i + 2);
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
    // Keep identity at root so each part stays in its own part-local pivot space.
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

export function writeImsh(path, partsMap) {
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
