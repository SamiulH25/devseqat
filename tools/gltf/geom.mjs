// Shared Three.js geometry primitives & Ironfield material table (UnitMatTable slots 0..13).
import * as THREE from 'three';
import { rgb, Palette } from './lib.mjs';

export { THREE };
export const V = (x, y, z) => new THREE.Vector3(x, y, z);

export const mat = (name, id, metal = 0, rough = 0.85, extra = {}) => {
  const m = new THREE.MeshStandardMaterial({
    name,
    color: rgb(...(Palette[id] ?? [255, 255, 255])),
    metalness: metal,
    roughness: rough,
    vertexColors: true,
    ...extra,
  });
  m.userData.materialId = id;
  return m;
};

export const M = {
  neutral: mat('neutral', 0, 0, 0.85, { side: THREE.DoubleSide }),
  cloth: mat('cloth', 1, 0, 0.9, { side: THREE.DoubleSide }),
  clothDark: mat('cloth_dark', 2, 0, 0.9, { side: THREE.DoubleSide }),
  trim: mat('trim', 3, 0, 0.8, { side: THREE.DoubleSide }),
  steel: mat('steel', 4, 0.9, 0.32, { side: THREE.DoubleSide }),
  mail: mat('mail', 5, 0.6, 0.45, { side: THREE.DoubleSide }),
  brass: mat('brass', 6, 0.85, 0.35, { side: THREE.DoubleSide }),
  leather: mat('leather', 7, 0, 0.72, { side: THREE.DoubleSide }),
  wood: mat('wood', 8, 0, 0.78, { side: THREE.DoubleSide }),
  skin: mat('skin', 9, 0, 0.7),
  hair: mat('hair', 10, 0, 0.9),
  coat: mat('coat', 11, 0, 0.82, { side: THREE.DoubleSide }),
  coatDark: mat('coat_dark', 12, 0, 0.88, { side: THREE.DoubleSide }),
  dark: mat('dark', 13, 0, 0.9, { side: THREE.DoubleSide }),
};

// ---------- geometry helpers ----------

export function shadeVerts(geo, fn) {
  const p = geo.attributes.position;
  const col = new Float32Array(p.count * 3);
  for (let i = 0; i < p.count; i++) {
    const s = Math.max(0.55, Math.min(1.0, fn(p.getX(i), p.getY(i), p.getZ(i))));
    col[i * 3] = s;
    col[i * 3 + 1] = s;
    col[i * 3 + 2] = s;
  }
  geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
  return geo;
}

export function heightShade(geo, y0, y1, lo = 0.86, hi = 1.0) {
  const span = Math.max(1e-4, y1 - y0);
  return shadeVerts(geo, (_x, y) => lo + (hi - lo) * Math.max(0, Math.min(1, (y - y0) / span)));
}

/** Rings of ellipses along a->b. Ring: [t, rx, rz, ox=0, oz=0, pinch=0]. */
export function loft(a, b, rings, { segs = 8, fwd = V(0, 0, 1), arc = null, caps = true } = {}) {
  const d = b.clone().sub(a), len = d.length(), uy = d.clone().divideScalar(len);
  let ux = new THREE.Vector3().crossVectors(uy, fwd);
  if (ux.lengthSq() < 1e-6) ux.crossVectors(uy, V(1, 0, 0));
  ux.normalize();
  const uz = new THREE.Vector3().crossVectors(ux, uy).normalize();
  const open = !!arc, n = open ? segs + 1 : segs;
  const pos = [], idx = [];
  for (const [t, rx, rz, ox = 0, oz = 0] of rings) {
    const c = a.clone().addScaledVector(uy, t * len).addScaledVector(ux, ox).addScaledVector(uz, oz);
    for (let i = 0; i < n; i++) {
      const th = open ? arc[0] + (arc[1] - arc[0]) * i / segs : -Math.PI / 2 + 2 * Math.PI * i / segs;
      const p = c.clone().addScaledVector(ux, Math.cos(th) * rx).addScaledVector(uz, Math.sin(th) * rz);
      pos.push(p.x, p.y, p.z);
    }
  }
  for (let r = 0; r + 1 < rings.length; r++)
    for (let i = 0; i < (open ? segs : n); i++) {
      const i2 = open ? i + 1 : (i + 1) % n, A = r * n + i, B = r * n + i2, C = (r + 1) * n + i, D = (r + 1) * n + i2;
      idx.push(A, D, B, A, C, D);
    }
  if (caps && !open) for (const [r, flip] of [[0, true], [rings.length - 1, false]]) {
    const [t, , , ox = 0, oz = 0] = rings[r];
    const c = a.clone().addScaledVector(uy, t * len).addScaledVector(ux, ox).addScaledVector(uz, oz);
    const ci = pos.length / 3;
    pos.push(c.x, c.y, c.z);
    for (let i = 0; i < n; i++) {
      const p = r * n + i, q = r * n + (i + 1) % n;
      flip ? idx.push(ci, p, q) : idx.push(ci, q, p);
    }
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setIndex(idx);
  g.computeVertexNormals();
  return g;
}

export const column = (rings, opts) => {
  const y0 = rings[0][0], y1 = rings[rings.length - 1][0];
  return heightShade(loft(V(0, y0, 0), V(0, y1, 0), rings.map(([y, ...r]) => [(y - y0) / (y1 - y0), ...r]), opts), y0, y1);
};

export const lathe = (pts, segs = 8, lo = 0.76, hi = 1.0) => {
  const g = new THREE.LatheGeometry(pts.map(([r, y]) => new THREE.Vector2(r, y)), segs);
  const ys = pts.map(p => p[1]);
  return heightShade(g, Math.min(...ys), Math.max(...ys), lo, hi);
};

export function bladeExtrude(shapePts, depth = 0.014, bevel = 0.004) {
  const s = new THREE.Shape();
  s.moveTo(shapePts[0][0], shapePts[0][1]);
  for (let i = 1; i < shapePts.length; i++) s.lineTo(shapePts[i][0], shapePts[i][1]);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, {
    depth: Math.max(0.002, depth - bevel * 2),
    bevelEnabled: bevel > 0,
    bevelThickness: bevel,
    bevelSize: bevel * 0.85,
    bevelSegments: 1,
    curveSegments: 2,
  });
  g.translate(0, 0, -(depth - bevel * 2) / 2);
  g.computeVertexNormals();
  return shadeVerts(g, (_x, y) => 0.82 + 0.18 * Math.max(0, Math.min(1, y)));
}

export function addPart(root, name, builder) {
  const g = new THREE.Group();
  g.name = name;
  const mesh = (geo, matRef, pos = null, rot = null) => {
    if (!geo.attributes.color) shadeVerts(geo, () => 0.92);
    const m = new THREE.Mesh(geo, matRef);
    if (pos) m.position.set(pos[0], pos[1], pos[2]);
    if (rot) m.rotation.set(rot[0], rot[1], rot[2]);
    g.add(m);
    return m;
  };
  builder(mesh, g);
  root.add(g);
}

