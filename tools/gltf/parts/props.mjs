import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildProps(root) {
  // plume: bold sweeping helm crest feather plume (Y 0..0.28, Z -0.34..0.05)
  addPart(root, 'plume', (mesh) => {
    const socket = column([[0.0, 0.028, 0.028], [0.050, 0.036, 0.036]], { segs: 6 });
    mesh(socket, M.brass);
    const body = loft(V(0, 0.045, 0.02), V(0, 0.13, -0.33), [
      [0.0, 0.042, 0.042],
      [0.25, 0.082, 0.112, 0, 0.11],
      [0.58, 0.098, 0.122, 0, 0.12],
      [0.84, 0.072, 0.080, 0, 0.05],
      [1.0, 0.022, 0.026, 0, -0.04],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(heightShade(body, 0.04, 0.28, 0.80, 1.0), M.cloth);
  });

  // cape: bold wide draped faceted shoulder cloak (visible on both sides of legs like reference)
  addPart(root, 'cape', (mesh) => {
    const rows = [
      [1.28, 0.24, -0.11, 0.028],
      [1.00, 0.31, -0.19, 0.048],
      [0.64, 0.36, -0.27, 0.060],
      [0.20, 0.41, -0.36, 0.068],
    ];
    const cols = 6;
    const pos = [], idx = [];
    for (let r = 0; r < rows.length; r++) {
      const [y, hw, z0, bow] = rows[r];
      for (let c = 0; c <= cols; c++) {
        const u = (c / cols) * 2 - 1;
        const ripple = r > 0 ? Math.sin(u * Math.PI * 2) * 0.018 * (r / 3) : 0;
        pos.push(u * hw, y, z0 - bow * (1 - u * u) + ripple);
      }
    }
    const stride = cols + 1;
    for (let r = 0; r < rows.length - 1; r++) {
      for (let c = 0; c < cols; c++) {
        const a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
        idx.push(a, e, b, a, d, e);
      }
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setIndex(idx);
    g.computeVertexNormals();
    mesh(heightShade(g, 0.24, 1.28, 0.78, 1.0), M.cloth);
  });

  // quiver: angled leather quiver with brass mouth & fletched arrows (X -0.14..0.07, Y -0.21..0.35)
  addPart(root, 'quiver', (mesh) => {
    const tube = loft(V(-0.08, -0.21, 0), V(0.02, 0.22, 0), [
      [0.0, 0.036, 0.042], [0.5, 0.048, 0.054], [1.0, 0.056, 0.062],
    ], { segs: 8 });
    mesh(heightShade(tube, -0.21, 0.22, 0.80, 1.0), M.leather);
    const rim = loft(V(0.01, 0.19, 0), V(0.025, 0.23, 0), [
      [0, 0.060, 0.066], [1, 0.060, 0.066],
    ], { segs: 8 });
    mesh(rim, M.brass);
    // 4 visible arrow fletchings poking out the top
    const offsets = [[-0.018, -0.018], [0.018, -0.016], [-0.014, 0.020], [0.016, 0.018]];
    for (const [ox, oz] of offsets) {
      const sh = shadeVerts(new THREE.CylinderGeometry(0.006, 0.006, 0.14, 5), () => 0.90);
      mesh(sh, M.wood, [0.025 + ox, 0.27, oz], [0, 0, -0.22]);
      const fl = shadeVerts(new THREE.ConeGeometry(0.016, 0.055, 4), () => 0.98);
      mesh(fl, M.trim, [0.04 + ox, 0.325, oz], [Math.PI, 0, -0.22]);
    }
  });

  // web_snare: spider-silk bands wrapping a caught unit (X -0.33..0.36, Y 0.34..1.33, Z -0.33..0.33)
  addPart(root, 'web_snare', (mesh) => {
    for (let i = 0; i < 6; i++) {
      const y = 0.42 + i * 0.165;
      const r = 0.28 + (i % 2) * 0.045;
      const ring = shadeVerts(new THREE.TorusGeometry(r, 0.016, 4, 10), () => 0.96);
      mesh(ring, M.neutral, [0, y, 0], [Math.PI / 2 + (i % 2 ? 0.26 : -0.22), i * 0.4, 0]);
    }
    for (let i = 0; i < 4; i++) {
      const a = (i / 4) * Math.PI * 2 + 0.3;
      const strand = loft(V(Math.cos(a) * 0.26, 0.36, Math.sin(a) * 0.26), V(Math.cos(a + 0.5) * 0.28, 1.31, Math.sin(a + 0.5) * 0.28), [
        [0, 0.012, 0.012], [0.5, 0.014, 0.014], [1, 0.012, 0.012],
      ], { segs: 4 });
      mesh(strand, M.neutral);
    }
  });

  // banner_pole: 3.55m standard pole + crossbar + hanging cloth flag in +X
  addPart(root, 'banner_pole', (mesh) => {
    const pole = column([[0.0, 0.020, 0.020], [3.26, 0.018, 0.018]], { segs: 6 });
    mesh(pole, M.wood);
    const finial = lathe([[0.022, 3.24], [0.056, 3.34], [0.0, 3.55]], 6);
    mesh(finial, M.brass);
    const cross = shadeVerts(new THREE.CylinderGeometry(0.015, 0.015, 0.86, 6), () => 0.94);
    cross.rotateZ(Math.PI / 2);
    mesh(cross, M.brass, [0.42, 3.10, 0]);
    // Hanging undulating banner cloth (X 0.04..0.84, Y 2.15..3.08)
    const cols = 4, rows = 3;
    const pos = [], idx = [];
    for (let r = 0; r <= rows; r++) {
      const v = r / rows;
      const y = 3.08 - v * 0.92;
      for (let c = 0; c <= cols; c++) {
        const u = c / cols;
        const x = 0.04 + u * 0.80;
        const wave = Math.sin(u * Math.PI * 1.5 + v * 0.8) * 0.045 * u;
        pos.push(x, y, wave);
      }
    }
    const stride = cols + 1;
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
        idx.push(a, b, e, a, e, d);
      }
    }
    const flag = new THREE.BufferGeometry();
    flag.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    flag.setIndex(idx);
    flag.computeVertexNormals();
    mesh(heightShade(flag, 2.15, 3.08, 0.86, 1.0), M.cloth);
    // Gold trim border at bottom
    const trim = shadeVerts(new THREE.BoxGeometry(0.80, 0.055, 0.018), () => 0.98);
    mesh(trim, M.trim, [0.44, 2.16, 0]);
  });

  // ---------- HERALDIC EMBLEMS (flat embossed charges in XY, Z -0.01..0.01) ----------
  const emblemBox = (mesh, w, h, pos = [0, 0, 0], rot = [0, 0, 0]) => {
    mesh(shadeVerts(new THREE.BoxGeometry(w, h, 0.018), () => 0.98), M.trim, pos, rot);
  };

  addPart(root, 'emblem_chevron', (mesh) => {
    emblemBox(mesh, 0.24, 0.055, [-0.09, -0.02, 0], [0, 0, 0.56]);
    emblemBox(mesh, 0.24, 0.055, [0.09, -0.02, 0], [0, 0, -0.56]);
  });
  addPart(root, 'emblem_saltire', (mesh) => {
    emblemBox(mesh, 0.42, 0.058, [0, 0, 0], [0, 0, Math.PI / 4]);
    emblemBox(mesh, 0.42, 0.058, [0, 0, 0], [0, 0, -Math.PI / 4]);
  });
  addPart(root, 'emblem_lozenge', (mesh) => {
    emblemBox(mesh, 0.22, 0.22, [0, 0, 0], [0, 0, Math.PI / 4]);
  });
  addPart(root, 'emblem_bend', (mesh) => {
    emblemBox(mesh, 0.46, 0.068, [0, 0, 0], [0, 0, -Math.PI / 4]);
  });
  addPart(root, 'emblem_cross', (mesh) => {
    emblemBox(mesh, 0.062, 0.41, [0, 0, 0]);
    emblemBox(mesh, 0.33, 0.062, [0, 0.04, 0]);
  });
  addPart(root, 'emblem_pale', (mesh) => {
    emblemBox(mesh, 0.095, 0.43, [0, 0, 0]);
  });
  addPart(root, 'emblem_fess', (mesh) => {
    emblemBox(mesh, 0.35, 0.10, [0, 0.02, 0]);
  });
  addPart(root, 'emblem_sun', (mesh) => {
    const disc = shadeVerts(new THREE.CylinderGeometry(0.088, 0.088, 0.018, 10), () => 1.0);
    disc.rotateX(Math.PI / 2);
    mesh(disc, M.trim);
    const ray = shadeVerts(new THREE.ConeGeometry(0.022, 0.095, 4), () => 0.98);
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * Math.PI * 2;
      mesh(ray, M.trim, [Math.cos(a) * 0.125, Math.sin(a) * 0.125, 0], [0, 0, a - Math.PI / 2]);
    }
  });

  // ---------- DISTANCE LOD SILHOUETTES (keep ultra-low poly: ~44-56 tris!) ----------
  // lod_foot: X -0.31..0.24, Y 0.00..1.73, Z -0.15..0.32
  addPart(root, 'lod_foot', (mesh) => {
    // Legs block (dark cloth/boots)
    const legs = column([[0.0, 0.14, 0.10], [0.76, 0.16, 0.11]], { segs: 4 });
    mesh(legs, M.clothDark);
    // Torso block (side cloth)
    const torso = column([[0.74, 0.18, 0.12], [1.34, 0.22, 0.13]], { segs: 4 });
    mesh(torso, M.cloth);
    // Head / helm block (steel)
    const head = column([[1.36, 0.11, 0.12], [1.62, 0.12, 0.13], [1.73, 0.04, 0.04]], { segs: 4 });
    mesh(head, M.steel);
    // Shield silhouette on left (-X, +Z)
    mesh(shadeVerts(new THREE.BoxGeometry(0.04, 0.56, 0.36), () => 0.92), M.cloth, [-0.26, 1.02, 0.10]);
  });

  // lod_horse: sculpted low-poly horse + rider silhouette (~90 tris)
  addPart(root, 'lod_horse', (mesh) => {
    // Tapered front & hind leg pairs
    for (const sz of [0.48, -0.50]) {
      const legs = loft(V(0, 0.0, sz), V(0, 0.84, sz), [
        [0, 0.20, 0.09], [0.5, 0.22, 0.11], [1, 0.25, 0.15],
      ], { segs: 5 });
      mesh(heightShade(legs, 0, 0.84, 0.72, 0.88), M.coatDark);
    }
    // Tapered equine barrel
    const barrel = loft(V(0, 0.94, -0.86), V(0, 0.98, 0.86), [
      [0.0, 0.14, 0.14], [0.22, 0.30, 0.29], [0.78, 0.31, 0.29], [1.0, 0.15, 0.15],
    ], { segs: 6, fwd: V(0, 1, 0) });
    mesh(heightShade(barrel, 0.65, 1.30, 0.78, 1.0), M.coat);
    // Arched neck + head + tail
    const neck = loft(V(0, 1.04, 0.68), V(0, 1.66, 1.10), [
      [0, 0.18, 0.22], [1, 0.11, 0.14],
    ], { segs: 5 });
    mesh(heightShade(neck, 1.04, 1.66, 0.82, 1.0), M.coat);
    const head = loft(V(0, 1.64, 1.08), V(0, 1.36, 1.50), [
      [0, 0.12, 0.13], [1, 0.06, 0.07],
    ], { segs: 5, fwd: V(0, 1, 0) });
    mesh(shadeVerts(head, () => 0.92), M.coat);
    const tail = loft(V(0, 1.04, -0.84), V(0, 0.42, -1.06), [
      [0, 0.06, 0.06], [1, 0.03, 0.03],
    ], { segs: 4 });
    mesh(shadeVerts(tail, () => 0.76), M.coatDark);
    // Rider torso + helm
    const torso = loft(V(0, 1.24, 0.0), V(0, 1.84, 0.0), [
      [0, 0.20, 0.14], [0.65, 0.23, 0.16], [1, 0.18, 0.13],
    ], { segs: 6 });
    mesh(heightShade(torso, 1.24, 1.84, 0.84, 1.0), M.cloth);
    mesh(shadeVerts(new THREE.OctahedronGeometry(0.14, 0), () => 1.0), M.steel, [0, 1.98, 0.0]);
  });
}
