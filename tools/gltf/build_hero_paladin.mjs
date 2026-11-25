// High-detail Three.js hero model builder for Ironfield's Great-Helm Knight (paladins).
// Built without triangle limitations: smooth cel-shaded curves + sharp flat-beveled edges + baked vertex AO.
// Usage: node build_hero_paladin.mjs [out/hero-paladin.glb]
import * as THREE from 'three';
import './lib.mjs';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { V, M, shadeVerts, heightShade, flat, loft, pathLoft, column, lathe } from './geom.mjs';

const outArg = process.argv[2] || 'out/hero-paladin.glb';
const out = resolve(outArg);

// Helper: add a part group where sub-meshes keep smooth normals by default (use flat(g) for crisp edges).
function addHeroPart(root, name, builder) {
  const g = new THREE.Group();
  g.name = name;
  const mesh = (geo, matRef, pos = null, rot = null, scl = null, { crisp = false } = {}) => {
    const outGeo = crisp ? flat(geo) : geo;
    if (!outGeo.attributes.color) shadeVerts(outGeo, () => 0.94);
    const m = new THREE.Mesh(outGeo, matRef);
    if (pos) m.position.set(pos[0], pos[1], pos[2]);
    if (rot) m.rotation.set(rot[0], rot[1], rot[2]);
    if (scl) m.scale.set(scl[0], scl[1], scl[2]);
    g.add(m);
    return m;
  };
  builder(mesh, g);
  root.add(g);
}

// Multiply existing vertex colors (or initialize) with an AO/shading callback.
function mulShade(geo, fn, min = 0.25) {
  const p = geo.attributes.position;
  let c = geo.attributes.color;
  if (!c) {
    const arr = new Float32Array(p.count * 3).fill(1);
    c = new THREE.BufferAttribute(arr, 3);
    geo.setAttribute('color', c);
  }
  for (let i = 0; i < p.count; i++) {
    const s = Math.max(min, Math.min(1.0, fn(p.getX(i), p.getY(i), p.getZ(i), i)));
    c.setXYZ(i, c.getX(i) * s, c.getY(i) * s, c.getZ(i) * s);
  }
  return geo;
}

// Extruded beveled polygon helper (in XY, extruded along Z centered at 0).
function extrudePoly(pts, depth = 0.02, bevel = 0.004, bevelSegs = 2) {
  const s = new THREE.Shape();
  s.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i < pts.length; i++) s.lineTo(pts[i][0], pts[i][1]);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, {
    depth: Math.max(0.001, depth - bevel * 2),
    bevelEnabled: bevel > 0,
    bevelThickness: bevel,
    bevelSize: bevel,
    bevelSegments: bevelSegs,
    curveSegments: 8,
  });
  g.translate(0, 0, -(depth - bevel * 2) / 2);
  g.computeVertexNormals();
  return g;
}

// Dome rivet helper.
function rivet(mesh, matRef, pos, r = 0.009) {
  const g = new THREE.SphereGeometry(r, 10, 6);
  shadeVerts(g, (_x, y) => 0.88 + 0.12 * (y / r + 1) * 0.5);
  mesh(g, matRef, pos);
}

// Tapered cylinder/ring along Y from y0..y1.
function ringBand(y0, y1, rx0, rz0, rx1, rz1, segs = 28, caps = false) {
  return column([[y0, rx0, rz0], [y1, rx1, rz1]], { segs, caps });
}

// Smooth folded cloth sheet panel (with sinusoidal drapery folds & thickness).
function clothPanel({ wTop, wBot, yTop, yBot, zTop, zBot, thick = 0.012, back = false, folds = 4, foldAmp = 0.010, cols = 20, rows = 10 }) {
  const pos = [], idx = [];
  const stride = cols + 1;
  // Front & back surfaces of the panel so it has real physical thickness.
  for (let side = 0; side < 2; side++) {
    const zOff = (side === 0 ? 1 : -1) * (thick * 0.5) * (back ? -1 : 1);
    for (let r = 0; r <= rows; r++) {
      const v = r / rows;
      const y = yTop + (yBot - yTop) * v;
      const w = (wTop + (wBot - wTop) * v) * 0.5;
      const zBase = zTop + (zBot - zTop) * v;
      for (let c = 0; c <= cols; c++) {
        const u = c / cols;
        const x = -w + 2 * w * u;
        // Subtle cylindrical wrap + vertical drapery folds that deepen toward the hem.
        const wrap = -0.022 * (x * x) / Math.max(0.01, w * w);
        const fold = Math.sin(u * Math.PI * folds) * foldAmp * Math.pow(v, 0.85);
        pos.push(x, y, zBase + (back ? -wrap : wrap) + (back ? -fold : fold) + zOff);
      }
    }
  }
  const vPerSide = (rows + 1) * stride;
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      const a = r * stride + c, b = a + 1, d = (r + 1) * stride + c, e = d + 1;
      if (back) idx.push(a, b, d, b, e, d);
      else idx.push(a, d, b, b, d, e);
      const a2 = vPerSide + a, b2 = vPerSide + b, d2 = vPerSide + d, e2 = vPerSide + e;
      if (back) idx.push(a2, d2, b2, b2, d2, e2);
      else idx.push(a2, b2, d2, b2, e2, d2);
    }
  }
  // Stitch perimeter edges (top, bottom, left, right)
  for (let c = 0; c < cols; c++) {
    // bottom edge (r = rows)
    const f0 = rows * stride + c, f1 = f0 + 1, b0 = vPerSide + f0, b1 = vPerSide + f1;
    if (back) idx.push(f0, b0, f1, f1, b0, b1);
    else idx.push(f0, f1, b0, f1, b1, b0);
  }
  for (let r = 0; r < rows; r++) {
    // left edge (c = 0)
    const f0 = r * stride, f1 = (r + 1) * stride, b0 = vPerSide + f0, b1 = vPerSide + f1;
    if (back) idx.push(f0, f1, b0, f1, b1, b0);
    else idx.push(f0, b0, f1, f1, b0, b1);
    // right edge (c = cols)
    const rf0 = r * stride + cols, rf1 = (r + 1) * stride + cols, rb0 = vPerSide + rf0, rb1 = vPerSide + rf1;
    if (back) idx.push(rf0, rb0, rf1, rf1, rb0, rb1);
    else idx.push(rf0, rf1, rb0, rf1, rb1, rb0);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setIndex(idx);
  g.computeVertexNormals();
  // Shade: slightly brighter on ridges, darker in fold troughs and near the hem (weathered edge).
  return shadeVerts(g, (x, y, z) => {
    const v = Math.max(0, Math.min(1, (yTop - y) / Math.max(1e-4, yTop - yBot)));
    const hemAO = 1.0 - 0.16 * Math.pow(v, 2.2);
    return (0.86 + 0.14 * (1 - v)) * hemAO;
  }, 0.65);
}

function buildHeroPaladin(root) {
  // ============================================================================
  // 1. HELM: helm_greathelm (headJoint space: origin at (0, 1.53, 0.01))
  // ============================================================================
  addHeroPart(root, 'helm_greathelm', (mesh) => {
    // Mail aventail / coif draping below the helm rim to meet the gorget cleanly.
    const aventail = column([
      [-0.225, 0.168, 0.162, 0, -0.005],
      [-0.175, 0.156, 0.154, 0, -0.002],
      [-0.120, 0.148, 0.150, 0, 0],
    ], { segs: 28, caps: false });
    mesh(heightShade(aventail, -0.225, -0.120, 0.75, 0.96), M.mail);

    // Main barrel body with gentle entasis + subtly domed crown.
    const barrel = column([
      [-0.148, 0.164, 0.178, 0, 0.002],
      [-0.080, 0.172, 0.186, 0, 0.004],
      [0.040, 0.172, 0.186, 0, 0.004],
      [0.150, 0.168, 0.182, 0, 0.003],
      [0.195, 0.152, 0.166, 0, 0.002],
      [0.224, 0.112, 0.122, 0, 0.001],
      [0.236, 0.052, 0.056, 0, 0],
      [0.240, 0.004, 0.004, 0, 0],
    ], {
      segs: 32,
      profile: (th, t) => {
        // Subtle front prow ridge (+Z is sin(th)=1) for heroic angularity.
        const front = Math.max(0, Math.sin(th));
        return [1.0, 1.0 + 0.055 * front * (1 - Math.abs(t - 0.5))];
      },
    });
    mesh(heightShade(barrel, -0.148, 0.240, 0.80, 1.0), M.steel);

    // Sculpted V-prow faceplate (left & right angled plates meeting at central keel).
    for (const sx of [-1, 1]) {
      const prow = new THREE.BoxGeometry(0.155, 0.295, 0.024, 1, 4, 1);
      shadeVerts(prow, (_x, y) => 0.86 + 0.14 * ((y + 0.15) / 0.30));
      mesh(prow, M.steel, [sx * 0.072, 0.015, 0.184], [0, -sx * 0.16, 0], null, { crisp: true });
    }

    // Dark recessed sight slit (behind the brass cross & brow).
    mesh(shadeVerts(new THREE.BoxGeometry(0.242, 0.024, 0.016), () => 0.08), M.dark, [0, 0.056, 0.195], null, null, { crisp: true });
    // Lower edge bevel under sight slit.
    mesh(shadeVerts(new THREE.BoxGeometry(0.248, 0.012, 0.018), () => 0.96), M.steel, [0, 0.041, 0.197], null, null, { crisp: true });

    // Cruciform brass reinforce (horizontal brow bar + vertical nasal/cheek bar + flared terminals).
    const hBar = new THREE.BoxGeometry(0.270, 0.026, 0.016);
    mesh(shadeVerts(hBar, () => 0.98), M.brass, [0, 0.082, 0.201], null, null, { crisp: true });
    const vBar = new THREE.BoxGeometry(0.032, 0.296, 0.018);
    mesh(shadeVerts(vBar, () => 0.98), M.brass, [0, 0.018, 0.203], null, null, { crisp: true });
    // Flared cross-end medallions + center boss.
    for (const [rx, ry] of [[0, 0.082], [-0.132, 0.082], [0.132, 0.082], [0, 0.156], [0, -0.118]]) {
      const med = new THREE.CylinderGeometry(0.022, 0.022, 0.014, 12);
      med.rotateX(Math.PI / 2);
      mesh(shadeVerts(med, () => 1.0), M.brass, [rx, ry, 0.204], null, null, { crisp: true });
    }

    // Breathing vents: crisp symmetrical 2x3 vents on lower cheeks so both sides read cleanly!
    for (const sx of [-1, 1]) {
      for (let row = 0; row < 2; row++) {
        for (let col = 0; col < 3; col++) {
          const hx = sx * (0.046 + col * 0.028);
          const hy = -0.016 - row * 0.038;
          const hz = 0.203 - col * 0.006;
          const hole = new THREE.CylinderGeometry(0.0095, 0.0095, 0.014, 10);
          hole.rotateX(Math.PI / 2);
          mesh(shadeVerts(hole, () => 0.06), M.dark, [hx, hy, hz], [0, -sx * 0.16, 0]);
        }
      }
    }

    // Crown & lower rim brass reinforcing bands + 3D rivets around the perimeter.
    const lowerBand = column([[-0.152, 0.172, 0.186, 0, 0.002], [-0.116, 0.176, 0.190, 0, 0.003]], { segs: 32, caps: false });
    mesh(shadeVerts(lowerBand, () => 0.96), M.brass);
    const crownBand = column([[0.144, 0.174, 0.188, 0, 0.003], [0.180, 0.166, 0.180, 0, 0.003]], { segs: 32, caps: false });
    mesh(shadeVerts(crownBand, () => 0.98), M.brass);
    for (let i = 0; i < 12; i++) {
      const a = (i / 12) * Math.PI * 2;
      rivet(mesh, M.brass, [Math.cos(a) * 0.176, -0.134, Math.sin(a) * 0.190 + 0.002], 0.0075);
      rivet(mesh, M.brass, [Math.cos(a) * 0.172, 0.162, Math.sin(a) * 0.186 + 0.003], 0.0075);
    }
  });

  // ============================================================================
  // 2. PLUME: plume (attached at headPos + hU * 0.25 - hF * 0.01)
  // ============================================================================
  addHeroPart(root, 'plume', (mesh) => {
    // Brass crest socket at base.
    const socket = column([[0, 0.030, 0.036], [0.045, 0.024, 0.044]], { segs: 16 });
    mesh(shadeVerts(socket, () => 0.96), M.brass);
    // Main sweeping horsehair/feather crest (multi-layered curved ribbons).
    const main = pathLoft([
      { p: V(0, 0.040, 0.00), rx: 0.022, rz: 0.036 },
      { p: V(0, 0.145, -0.02), rx: 0.042, rz: 0.088 },
      { p: V(0, 0.230, -0.10), rx: 0.044, rz: 0.108 },
      { p: V(0, 0.205, -0.22), rx: 0.034, rz: 0.082 },
      { p: V(0, 0.110, -0.31), rx: 0.018, rz: 0.044 },
      { p: V(0, 0.010, -0.35), rx: 0.005, rz: 0.012 },
    ], { segs: 16, fwd: V(0, 0, 1) });
    mesh(heightShade(main, 0.01, 0.24, 0.72, 1.0), M.cloth);
    // Secondary inner ridge lock for layered fullness.
    const crest = pathLoft([
      { p: V(0, 0.050, 0.01), rx: 0.016, rz: 0.026 },
      { p: V(0, 0.175, -0.03), rx: 0.026, rz: 0.064 },
      { p: V(0, 0.255, -0.11), rx: 0.024, rz: 0.068 },
      { p: V(0, 0.220, -0.21), rx: 0.014, rz: 0.040 },
    ], { segs: 12, fwd: V(0, 0, 1) });
    mesh(heightShade(crest, 0.05, 0.26, 0.84, 1.0), M.trim);
  });

  // ============================================================================
  // 3. CHEST SURCOAT + GORGET: chest_surcoat (bodySpace y: 0.80..1.38)
  // ============================================================================
  addHeroPart(root, 'chest_surcoat', (mesh) => {
    // Inner mail hauberk collar & steel gorget visible at the neckline.
    const mailNeck = column([[1.280, 0.152, 0.132], [1.385, 0.116, 0.108]], { segs: 24, caps: false });
    mesh(heightShade(mailNeck, 1.28, 1.385, 0.78, 0.96), M.mail);

    const gorget = column([
      [1.240, 0.188, 0.158, 0, 0.004],
      [1.300, 0.164, 0.140, 0, 0.004],
      [1.345, 0.132, 0.120, 0, 0.002],
    ], { segs: 24, caps: false });
    mesh(heightShade(gorget, 1.24, 1.345, 0.85, 1.0), M.steel);
    // Brass gorget rim.
    mesh(ringBand(1.334, 1.352, 0.138, 0.124, 0.132, 0.120, 24), M.brass);

    // Sculpted cloth surcoat torso with tailored waist taper and natural drapery folds!
    // Front Z at y=1.03 is ~0.168 so the heraldic emblem at z=0.186 sits cleanly proud.
    const rings = [];
    const nRings = 12;
    for (let i = 0; i <= nRings; i++) {
      const t = i / nRings;
      const y = 0.800 + t * 0.545;
      // Hourglass-to-heroic chest curve: cinched at waist (t~0.1), broad at chest (t~0.75), tapering at collar.
      const waistPinch = 1.0 - 0.10 * Math.exp(-Math.pow((t - 0.08) / 0.22, 2));
      const chestBulge = 1.0 + 0.14 * Math.exp(-Math.pow((t - 0.72) / 0.28, 2));
      const neckTaper = t > 0.85 ? 1.0 - (t - 0.85) * 2.2 : 1.0;
      const rx = 0.198 * waistPinch * chestBulge * neckTaper;
      const rz = 0.152 * waistPinch * (1.0 + 0.09 * Math.exp(-Math.pow((t - 0.68) / 0.30, 2))) * neckTaper;
      rings.push([y, rx, rz, 0, 0.006]);
    }
    const surcoat = column(rings, {
      segs: 36,
      caps: true,
      profile: (th, t) => {
        // Subtle vertical cloth folds near the waist (t < 0.45), smooth over chest so emblem sits clean.
        const waistAmt = Math.max(0, 1 - t * 2.1);
        const fold = 1.0 + 0.032 * waistAmt * Math.cos(th * 6);
        return [fold, fold];
      },
    });
    mesh(shadeVerts(surcoat, (x, y, z) => {
      const h = 0.84 + 0.16 * Math.max(0, Math.min(1, (y - 0.80) / 0.54));
      const underarmAO = 1.0 - 0.14 * Math.exp(-Math.pow((y - 1.16) / 0.12, 2)) * Math.max(0, Math.abs(x) / 0.20 - 0.5);
      const waistAO = 1.0 - 0.12 * Math.exp(-Math.pow((y - 0.82) / 0.06, 2));
      return h * underarmAO * waistAO;
    }, 0.65), M.cloth);

    // Tailored armhole / shoulder straps of the surcoat + dark trim piping.
    for (const sx of [-1, 1]) {
      const strap = pathLoft([
        { p: V(sx * 0.185, 1.150, 0.115), rx: 0.032, rz: 0.016 },
        { p: V(sx * 0.195, 1.320, 0.030), rx: 0.036, rz: 0.020 },
        { p: V(sx * 0.185, 1.160, -0.105), rx: 0.032, rz: 0.016 },
      ], { segs: 12, fwd: V(sx, 0, 0) });
      mesh(shadeVerts(strap, () => 0.84), M.clothDark);
    }
  });

  // ============================================================================
  // 4. PAULDRONS: shld_pauldrons_steel (shoulders at ±0.255, 1.25, 0)
  // ============================================================================
  addHeroPart(root, 'shld_pauldrons_steel', (mesh) => {
    for (const s of [-1, 1]) {
      // Main sculpted upper pauldron dome with raised haute-piece neck guard ridge.
      const dome = new THREE.SphereGeometry(1, 24, 12, 0, Math.PI * 2, 0, Math.PI * 0.58);
      dome.scale(0.194, 0.120, 0.160);
      mesh(shadeVerts(dome, (_x, y) => 0.84 + 0.16 * Math.max(0, y)), M.steel, [s * 0.252, 1.250, 0], [0, 0, -s * 0.18]);

      // Brass edge trim & rivets along the main dome rim.
      const rim = new THREE.TorusGeometry(0.145, 0.010, 8, 24);
      rim.rotateX(Math.PI / 2);
      rim.scale(1.18, 1.0, 0.98);
      mesh(shadeVerts(rim, () => 0.98), M.brass, [s * 0.254, 1.224, 0], [0, 0, -s * 0.18]);

      // Three overlapping articulated steel lames stepping down the upper arm.
      for (let i = 0; i < 3; i++) {
        const y = 1.188 - i * 0.040;
        const r = 0.118 + i * 0.005;
        const lame = new THREE.CylinderGeometry(r * 0.92, r, 0.046, 20, 1, true);
        lame.scale(0.95, 1, 1.14);
        mesh(shadeVerts(lame, (_x, yy) => 0.84 + 0.12 * (yy + 0.025) / 0.05), M.steel, [s * (0.264 + i * 0.004), y, 0], [0, 0, -s * 0.08]);
        // Dark shadow lip under each lame.
        const gap = new THREE.CylinderGeometry(r * 0.90, r * 0.90, 0.008, 20, 1, true);
        gap.scale(0.95, 1, 1.14);
        mesh(shadeVerts(gap, () => 0.35), M.dark, [s * (0.264 + i * 0.004), y - 0.023, 0], [0, 0, -s * 0.08]);
      }

      // Circular besagew (rondel) with conical brass boss guarding the front armpit.
      const rondel = new THREE.CylinderGeometry(0.068, 0.072, 0.016, 20);
      rondel.rotateX(Math.PI / 2);
      mesh(shadeVerts(rondel, () => 0.96), M.steel, [s * 0.195, 1.215, 0.145], [0, s * 0.28, 0]);
      const boss = new THREE.ConeGeometry(0.022, 0.026, 12);
      boss.rotateX(Math.PI / 2);
      mesh(shadeVerts(boss, () => 1.0), M.brass, [s * 0.195, 1.215, 0.156], [0, s * 0.28, 0]);
    }
  });

  // ============================================================================
  // 5. BELT & SCABBARD: belt_plate (y: 0.78..0.88)
  // ============================================================================
  addHeroPart(root, 'belt_plate', (mesh) => {
    // Thick chamfered leather belt.
    const belt = column([
      [0.788, 0.204, 0.166, 0, 0.006],
      [0.800, 0.214, 0.174, 0, 0.006],
      [0.858, 0.214, 0.174, 0, 0.006],
      [0.870, 0.204, 0.166, 0, 0.006],
    ], { segs: 32, caps: false });
    mesh(heightShade(belt, 0.788, 0.870, 0.78, 0.98), M.leather);

    // Ornate rectangular brass buckle (4-bar frame + prong + dark backing).
    mesh(shadeVerts(new THREE.BoxGeometry(0.076, 0.074, 0.018), () => 0.45), M.dark, [0, 0.829, 0.178], null, null, { crisp: true });
    for (const [bx, by, bw, bh] of [
      [0, 0.861, 0.088, 0.015], [0, 0.797, 0.088, 0.015],
      [-0.036, 0.829, 0.016, 0.076], [0.036, 0.829, 0.016, 0.076],
      [0, 0.829, 0.052, 0.010],
    ]) {
      mesh(shadeVerts(new THREE.BoxGeometry(bw, bh, 0.022), () => 0.98), M.brass, [bx, by, 0.184], null, null, { crisp: true });
    }

    // Hanging leather belt tongue curving down front-left.
    const tongue = pathLoft([
      { p: V(-0.078, 0.822, 0.178), rx: 0.024, rz: 0.008 },
      { p: V(-0.095, 0.710, 0.194), rx: 0.024, rz: 0.008 },
      { p: V(-0.105, 0.590, 0.206), rx: 0.022, rz: 0.007 },
      { p: V(-0.108, 0.540, 0.208), rx: 0.006, rz: 0.004 },
    ], { segs: 10, fwd: V(0, 0, 1) });
    mesh(heightShade(tongue, 0.54, 0.82, 0.76, 0.96), M.leather);
    // Brass chape tip on belt tongue.
    mesh(shadeVerts(new THREE.BoxGeometry(0.036, 0.038, 0.016), () => 0.98), M.brass, [-0.106, 0.562, 0.207], null, null, { crisp: true });

    // Brass belt studs around the waist.
    for (let i = 1; i < 10; i++) {
      const a = -Math.PI / 2 + (i / 10) * Math.PI * 2;
      if (Math.abs(Math.cos(a)) < 0.35 && Math.sin(a) > 0) continue; // skip buckle area
      rivet(mesh, M.brass, [Math.cos(a) * 0.214, 0.829, Math.sin(a) * 0.174 + 0.006], 0.010);
    }

    // Slung leather sword scabbard on left hip (angled backward & down like reference art).
    const scabPts = [
      { p: V(-0.218, 0.775, 0.065), rx: 0.028, rz: 0.016 },
      { p: V(-0.238, 0.520, -0.115), rx: 0.025, rz: 0.014 },
      { p: V(-0.254, 0.260, -0.300), rx: 0.020, rz: 0.011 },
      { p: V(-0.264, 0.120, -0.400), rx: 0.008, rz: 0.006 },
    ];
    mesh(heightShade(pathLoft(scabPts, { segs: 12, fwd: V(1, 0, 0) }), 0.12, 0.78, 0.70, 0.94), M.leather);
    // Brass locket at scabbard mouth & brass chape at tip.
    const locket = pathLoft([
      { p: V(-0.214, 0.805, 0.086), rx: 0.033, rz: 0.020 },
      { p: V(-0.222, 0.735, 0.036), rx: 0.032, rz: 0.019 },
    ], { segs: 12, fwd: V(1, 0, 0) });
    mesh(shadeVerts(locket, () => 0.98), M.brass);
    const chape = pathLoft([
      { p: V(-0.256, 0.220, -0.328), rx: 0.021, rz: 0.012 },
      { p: V(-0.265, 0.110, -0.408), rx: 0.007, rz: 0.005 },
    ], { segs: 12, fwd: V(1, 0, 0) });
    mesh(shadeVerts(chape, () => 0.98), M.brass);
    // Suspension straps from belt to scabbard.
    for (const [bx, bz, sx, sy, sz] of [[-0.210, 0.055, -0.218, 0.765, 0.058], [-0.210, -0.020, -0.230, 0.630, -0.038]]) {
      const strap = pathLoft([
        { p: V(bx, 0.820, bz), rx: 0.010, rz: 0.016 },
        { p: V(sx, sy, sz), rx: 0.010, rz: 0.016 },
      ], { segs: 8, fwd: V(1, 0, 0) });
      mesh(shadeVerts(strap, () => 0.86), M.leather);
    }
  });

  // ============================================================================
  // 6. SKIRT & FAULD: skirt_fauld_steel (y: 0.45..0.83)
  // ============================================================================
  addHeroPart(root, 'skirt_fauld_steel', (mesh) => {
    // Mail hauberk skirt peeking out underneath the surcoat & fauld.
    const mailSkirt = column([
      [0.495, 0.236, 0.178, 0, 0.004],
      [0.640, 0.216, 0.164, 0, 0.004],
      [0.800, 0.194, 0.148, 0, 0.004],
    ], { segs: 28, caps: false });
    mesh(heightShade(mailSkirt, 0.495, 0.800, 0.72, 0.94), M.mail);

    // 2 articulated steel fauld hoops at the upper hip.
    for (let i = 0; i < 2; i++) {
      const yTop = 0.810 - i * 0.056;
      const yBot = yTop - 0.052;
      const rx = 0.208 + i * 0.010;
      const rz = 0.164 + i * 0.008;
      const hoop = ringBand(yBot, yTop, rx * 1.03, rz * 1.03, rx, rz, 28, false);
      mesh(heightShade(hoop, yBot, yTop, 0.84, 1.0), M.steel);
    }

    // Flowing split front surcoat tails (left & right with center leg slit!) + broad rear tail.
    for (const sx of [-1, 1]) {
      const frontTail = clothPanel({
        wTop: 0.156, wBot: 0.194, yTop: 0.800, yBot: 0.440,
        zTop: 0.166, zBot: 0.208, thick: 0.014, back: false, folds: 3, foldAmp: 0.012,
      });
      frontTail.translate(sx * 0.098, 0, 0);
      mesh(frontTail, M.cloth);
      // Trim border at bottom of front surcoat tails.
      const hem = clothPanel({
        wTop: 0.190, wBot: 0.196, yTop: 0.472, yBot: 0.436,
        zTop: 0.204, zBot: 0.210, thick: 0.016, back: false, folds: 3, foldAmp: 0.012, rows: 2,
      });
      hem.translate(sx * 0.098, 0, 0);
      mesh(hem, M.trim);
    }

    const backTail = clothPanel({
      wTop: 0.340, wBot: 0.420, yTop: 0.800, yBot: 0.430,
      zTop: -0.172, zBot: -0.222, thick: 0.014, back: true, folds: 5, foldAmp: 0.014,
    });
    mesh(backTail, M.cloth);
    const backHem = clothPanel({
      wTop: 0.412, wBot: 0.424, yTop: 0.464, yBot: 0.426,
      zTop: -0.217, zBot: -0.224, thick: 0.016, back: true, folds: 5, foldAmp: 0.014, rows: 2,
    });
    mesh(backHem, M.trim);
  });

  // ============================================================================
  // 7. UPPER ARM: uarm_plate (bone y: 0..1, length ~0.29m)
  // ============================================================================
  addHeroPart(root, 'uarm_plate', (mesh) => {
    // Mail sleeve under-layer at joints.
    mesh(heightShade(loft(V(0, -0.06, 0), V(0, 1.04, 0), [
      [0, 0.088, 0.092], [0.5, 0.076, 0.080], [1, 0.078, 0.082],
    ], { segs: 16 }), -0.06, 1.04, 0.72, 0.90), M.mail);

    // Sculpted rerebrace tube.
    const rerebrace = loft(V(0, 0.08, 0), V(0, 0.92, 0), [
      [0, 0.096, 0.102],
      [0.25, 0.102, 0.108],
      [0.70, 0.084, 0.088],
      [1, 0.080, 0.084],
    ], { segs: 20 });
    mesh(heightShade(rerebrace, 0.08, 0.92, 0.82, 1.0), M.steel);
    // Brass band above elbow.
    mesh(shadeVerts(ringBand(0.84, 0.90, 0.084, 0.088, 0.082, 0.086, 20), () => 0.98), M.brass);

    // Winged elbow couter (domed cup + lateral fan wing + brass rivet).
    const cup = new THREE.SphereGeometry(0.088, 18, 12);
    cup.scale(1.0, 1.15, 0.92);
    mesh(shadeVerts(cup, (_x, y) => 0.85 + 0.15 * (y + 0.1) / 0.2), M.steel, [0, 0.97, -0.040]);
    // Winged side fan.
    const fan = extrudePoly([
      [-0.11, -0.06], [0.11, -0.06], [0.13, 0.04], [0, 0.09], [-0.13, 0.04],
    ], 0.018, 0.004);
    fan.rotateY(Math.PI / 2);
    mesh(shadeVerts(fan, () => 0.96), M.steel, [0.075, 0.97, -0.015], null, null, { crisp: true });
    rivet(mesh, M.brass, [0, 0.97, -0.130], 0.013);
  });

  // ============================================================================
  // 8. FOREARM & GAUNTLET: farm_plate (bone y: 0..1, length ~0.28m)
  // ============================================================================
  addHeroPart(root, 'farm_plate', (mesh) => {
    // Anatomical vambrace with front keel ridge.
    const vambrace = loft(V(0, -0.04, 0), V(0, 0.72, 0), [
      [0, 0.080, 0.084],
      [0.35, 0.074, 0.078],
      [0.80, 0.062, 0.066],
      [1, 0.060, 0.064],
    ], {
      segs: 20,
      profile: (th) => [1.0, 1.0 + 0.08 * Math.max(0, Math.sin(th))],
    });
    mesh(heightShade(vambrace, -0.04, 0.72, 0.82, 1.0), M.steel);

    // Flared hourglass gauntlet cuff with brass border.
    const cuff = loft(V(0, 0.56, 0), V(0, 0.88, 0), [
      [0, 0.062, 0.066],
      [0.45, 0.068, 0.072],
      [1, 0.092, 0.094],
    ], { segs: 20 });
    mesh(heightShade(cuff, 0.56, 0.88, 0.85, 1.0), M.steel);
    mesh(shadeVerts(ringBand(0.56, 0.61, 0.065, 0.069, 0.067, 0.071, 20), () => 0.98), M.brass);

    // Leather inner glove.
    const palm = new THREE.BoxGeometry(0.088, 0.145, 0.074);
    mesh(shadeVerts(palm, () => 0.78), M.leather, [0, 0.98, 0.010], null, null, { crisp: true });

    // Metacarpal plate + knuckle ridge + 4 articulated armored fingers + thumb!
    const backPlate = extrudePoly([[-0.046, -0.045], [0.046, -0.045], [0.042, 0.045], [-0.042, 0.045]], 0.022, 0.005);
    mesh(shadeVerts(backPlate, () => 0.96), M.steel, [0, 0.935, 0.046], [-0.12, 0, 0], null, { crisp: true });
    // Knuckle guard bar.
    mesh(shadeVerts(new THREE.BoxGeometry(0.092, 0.022, 0.026), () => 1.0), M.brass, [0, 0.985, 0.048], null, null, { crisp: true });
    // 4 sculpted fingers curling forward.
    for (let f = 0; f < 4; f++) {
      const fx = -0.032 + f * 0.021;
      const finger = pathLoft([
        { p: V(fx, 0.990, 0.046), rx: 0.010, rz: 0.011 },
        { p: V(fx, 1.055, 0.038), rx: 0.009, rz: 0.010 },
        { p: V(fx, 1.085, -0.005), rx: 0.008, rz: 0.009 },
      ], { segs: 8, fwd: V(0, 0, 1) });
      mesh(shadeVerts(finger, () => 0.90), M.steel);
    }
    // Armored thumb.
    const thumb = pathLoft([
      { p: V(0.046, 0.930, 0.036), rx: 0.012, rz: 0.012 },
      { p: V(0.062, 0.995, 0.048), rx: 0.010, rz: 0.010 },
      { p: V(0.048, 1.035, 0.056), rx: 0.008, rz: 0.008 },
    ], { segs: 8, fwd: V(0, 0, 1) });
    mesh(shadeVerts(thumb, () => 0.92), M.steel);
  });

  // ============================================================================
  // 9. THIGH & KNEE POLEYN: thigh_plate (bone y: 0..1, length ~0.34m)
  // ============================================================================
  addHeroPart(root, 'thigh_plate', (mesh) => {
    // Mail under-thigh.
    mesh(heightShade(loft(V(0, -0.06, 0), V(0, 1.02, 0), [
      [0, 0.100, 0.106], [1, 0.080, 0.084],
    ], { segs: 16 }), -0.06, 1.02, 0.72, 0.88), M.mail);

    // Sculpted cuisse with front keel ridge.
    const cuisse = loft(V(0, 0.04, 0), V(0, 0.90, 0), [
      [0, 0.112, 0.118],
      [0.45, 0.102, 0.108, 0, 0.008],
      [1, 0.084, 0.090],
    ], {
      segs: 24,
      profile: (th) => [1.0, 1.0 + 0.09 * Math.max(0, Math.sin(th))],
    });
    mesh(heightShade(cuisse, 0.04, 0.90, 0.82, 1.0), M.steel);

    // 2 articulated knee lames + brass border.
    for (let i = 0; i < 2; i++) {
      const y0 = 0.84 + i * 0.07;
      mesh(shadeVerts(ringBand(y0, y0 + 0.072, 0.088, 0.094, 0.086, 0.092, 20), () => 0.92), M.steel);
    }
    mesh(shadeVerts(ringBand(0.83, 0.86, 0.090, 0.096, 0.089, 0.095, 20), () => 0.98), M.brass);

    // Winged knee poleyn (domed kneecap + outer fan wing + brass boss).
    const poleyn = new THREE.SphereGeometry(0.096, 20, 12);
    poleyn.scale(1.0, 1.15, 0.92);
    mesh(shadeVerts(poleyn, (_x, y) => 0.86 + 0.14 * (y + 0.1) / 0.2), M.steel, [0, 0.97, 0.048]);
    const wing = extrudePoly([[-0.08, -0.06], [0.08, -0.06], [0.10, 0.03], [0, 0.07], [-0.10, 0.03]], 0.016, 0.004);
    wing.rotateY(Math.PI / 2);
    mesh(shadeVerts(wing, () => 0.96), M.steel, [0.072, 0.97, 0.025], null, null, { crisp: true });
    rivet(mesh, M.brass, [0, 0.97, 0.136], 0.014);
  });

  // ============================================================================
  // 10. GREAVE, SABATON & SPUR: shin_plate (bone y: 0..1, length ~0.34m)
  // ============================================================================
  addHeroPart(root, 'shin_plate', (mesh) => {
    // Anatomical greave with calf curve and crisp front shin ridge.
    const greave = loft(V(0, -0.04, 0), V(0, 0.94, 0), [
      [0, 0.086, 0.094],
      [0.28, 0.086, 0.104, 0, -0.016],
      [0.62, 0.072, 0.078],
      [0.90, 0.064, 0.070],
      [1, 0.068, 0.074],
    ], {
      segs: 24,
      profile: (th) => [1.0, 1.0 + 0.10 * Math.max(0, Math.sin(th))],
    });
    mesh(heightShade(greave, -0.04, 0.94, 0.80, 1.0), M.steel);

    // Brass ankle collar.
    mesh(shadeVerts(ringBand(0.84, 0.89, 0.068, 0.074, 0.069, 0.075, 24), () => 0.98), M.brass);

    // Dark leather sole under the sabaton.
    const sole = extrudePoly([
      [-0.056, -0.064], [0.056, -0.064], [0.058, 0.130], [0.036, 0.210], [0, 0.232], [-0.036, 0.210], [-0.058, 0.130],
    ], 0.020, 0.003);
    sole.rotateX(-Math.PI / 2);
    mesh(shadeVerts(sole, () => 0.55), M.dark, [0, 1.005, 0.080], null, null, { crisp: true });

    // Articulated 5-lame steel sabaton tapering to an almond toe.
    const sabaton = pathLoft([
      { p: V(0, 0.945, -0.062), rx: 0.064, rz: 0.058 },
      { p: V(0, 0.935, 0.010), rx: 0.068, rz: 0.062 },
      { p: V(0, 0.938, 0.085), rx: 0.066, rz: 0.056 },
      { p: V(0, 0.952, 0.155), rx: 0.052, rz: 0.042 },
      { p: V(0, 0.970, 0.215), rx: 0.032, rz: 0.024 },
      { p: V(0, 0.985, 0.242), rx: 0.010, rz: 0.010 },
    ], { segs: 18, fwd: V(0, 1, 0) });
    mesh(heightShade(sabaton, 0.92, 1.0, 0.86, 1.0), M.steel);

    // Leather spur strap + brass rowel spur at the heel (just like ref-b!).
    mesh(shadeVerts(ringBand(0.925, 0.960, 0.068, 0.074, 0.068, 0.074, 18), () => 0.84), M.leather);
    const spurNeck = new THREE.CylinderGeometry(0.008, 0.010, 0.060, 8);
    spurNeck.rotateX(Math.PI / 2);
    mesh(shadeVerts(spurNeck, () => 0.98), M.brass, [0, 0.945, -0.092]);
    const rowel = new THREE.CylinderGeometry(0.022, 0.022, 0.006, 8);
    rowel.rotateZ(Math.PI / 2);
    mesh(shadeVerts(rowel, () => 1.0), M.brass, [0, 0.945, -0.124], null, null, { crisp: true });
  });

  // ============================================================================
  // 11. LONGSWORD: weapon_longsword (grip y=0, blade +Y, edge ±X)
  // ============================================================================
  addHeroPart(root, 'weapon_longsword', (mesh) => {
    // Faceted wheel pommel + peen block.
    const pommel = new THREE.CylinderGeometry(0.040, 0.040, 0.032, 16);
    pommel.rotateX(Math.PI / 2);
    mesh(shadeVerts(pommel, () => 0.98), M.brass, [0, -0.228, 0], null, null, { crisp: true });
    const boss = new THREE.SphereGeometry(0.024, 12, 8);
    boss.scale(1, 1, 0.65);
    mesh(shadeVerts(boss, () => 1.0), M.brass, [0, -0.228, 0]);
    mesh(shadeVerts(new THREE.OctahedronGeometry(0.014, 0), () => 1.0), M.brass, [0, -0.268, 0], null, null, { crisp: true });

    // Corded leather grip with central brass riser ring.
    const grip = column([
      [-0.195, 0.018, 0.015],
      [-0.105, 0.022, 0.018],
      [-0.015, 0.020, 0.016],
    ], { segs: 16 });
    mesh(heightShade(grip, -0.195, -0.015, 0.72, 0.92), M.leather);
    mesh(shadeVerts(ringBand(-0.112, -0.095, 0.024, 0.020, 0.024, 0.020, 16), () => 1.0), M.brass);

    // Swept steel & brass crossguard with flared quillon tips.
    const guard = extrudePoly([
      [-0.165, -0.008], [-0.140, -0.022], [-0.038, -0.016], [0, -0.026],
      [0.038, -0.016], [0.140, -0.022], [0.165, -0.008],
      [0.145, 0.016], [0.038, 0.012], [0, 0.022], [-0.038, 0.012], [-0.145, 0.016],
    ], 0.028, 0.004);
    mesh(shadeVerts(guard, () => 0.96), M.brass, [0, 0.002, 0], null, null, { crisp: true });

    // Diamond-section beveled steel blade (y: 0.016..1.16) built from 4 crisp facets per side.
    const segsY = 12;
    const pos = [], idx = [];
    for (let i = 0; i <= segsY; i++) {
      const t = i / segsY;
      const y = 0.016 + t * 1.144;
      const halfW = t < 0.80 ? 0.036 * (1 - t * 0.36) : 0.036 * 0.712 * (1 - (t - 0.80) / 0.20);
      const thick = t < 0.80 ? 0.011 * (1 - t * 0.32) : 0.011 * 0.744 * (1 - (t - 0.80) / 0.20);
      // 4 diamond vertices per ring: +X edge, +Z spine, -X edge, -Z spine
      pos.push(
        halfW, y, 0,
        0, y, thick,
        -halfW, y, 0,
        0, y, -thick
      );
    }
    for (let i = 0; i < segsY; i++) {
      const a = i * 4, b = (i + 1) * 4;
      for (let k = 0; k < 4; k++) {
        const k2 = (k + 1) % 4;
        idx.push(a + k, b + k, b + k2, a + k, b + k2, a + k2);
      }
    }
    const blade = new THREE.BufferGeometry();
    blade.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    blade.setIndex(idx);
    shadeVerts(blade, (x, y) => {
      const fuller = (y < 0.72 && Math.abs(x) < 0.008) ? 0.72 : 1.0;
      return (0.86 + 0.14 * Math.min(1, y / 1.16)) * fuller;
    }, 0.65);
    mesh(blade, M.steel, null, null, null, { crisp: true });
  });

  // ============================================================================
  // 12. HEATER SHIELD: shield_heater_quartered (curved +Z face)
  // ============================================================================
  addHeroPart(root, 'shield_heater', (mesh) => {
    const W = 0.32, H = 0.44, curve = 0.075;
    const cols = 16, rows = 14;
    const pos = [], idx = [], col = [];
    // Helper: shield outline width at v in [0..1] (0 = bottom point, 1 = flat top).
    const halfWidthAt = (v) => {
      if (v > 0.58) return W;
      const t = v / 0.58;
      return W * Math.pow(t, 0.58);
    };
    for (let side = 0; side < 2; side++) {
      const zSign = side === 0 ? 1 : -1;
      for (let r = 0; r <= rows; r++) {
        const v = r / rows;
        const y = -H * 0.52 + v * (H * 1.02);
        const hw = halfWidthAt(v);
        for (let c = 0; c <= cols; c++) {
          const u = (c / cols) * 2 - 1;
          const x = u * hw;
          const z = Math.cos(u * 0.92) * curve + (side === 0 ? 0.010 : -0.010);
          pos.push(x, y, z);
          const shade = 0.85 + 0.15 * v;
          col.push(shade, shade, shade);
        }
      }
    }
    const stride = cols + 1, vSide = (rows + 1) * stride;
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const a = r * stride + c, b = a + 1, d = (r + 1) * stride + c, e = d + 1;
        idx.push(a, b, d, b, e, d);
        const a2 = vSide + a, b2 = vSide + b, d2 = vSide + d, e2 = vSide + e;
        idx.push(a2, d2, b2, b2, d2, e2);
      }
    }
    // Front heraldry face (M.cloth) + wood back (separate mesh? let's build front & back cleanly!)
    const frontGeo = new THREE.BufferGeometry();
    const fPos = [], fIdx = [], fCol = [];
    for (let r = 0; r <= rows; r++) {
      const v = r / rows, y = -H * 0.52 + v * (H * 1.02), hw = halfWidthAt(v);
      for (let c = 0; c <= cols; c++) {
        const u = (c / cols) * 2 - 1;
        const x = u * hw;
        const z = Math.cos(u * 0.92) * curve + 0.011;
        fPos.push(x, y, z);
        const s = 0.84 + 0.16 * v;
        fCol.push(s, s, s);
      }
    }
    for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) {
      const a = r * stride + c, b = a + 1, d = (r + 1) * stride + c, e = d + 1;
      fIdx.push(a, b, d, b, e, d);
    }
    frontGeo.setAttribute('position', new THREE.Float32BufferAttribute(fPos, 3));
    frontGeo.setAttribute('color', new THREE.Float32BufferAttribute(fCol, 3));
    frontGeo.setIndex(fIdx);
    frontGeo.computeVertexNormals();
    mesh(frontGeo, M.cloth);

    // Back oak plank face (M.wood).
    const backGeo = frontGeo.clone();
    const bp = backGeo.attributes.position;
    for (let i = 0; i < bp.count; i++) bp.setZ(i, bp.getZ(i) - 0.022);
    const bi = backGeo.index;
    const bArr = [];
    for (let i = 0; i < bi.count; i += 3) bArr.push(bi.getX(i), bi.getX(i + 2), bi.getX(i + 1));
    backGeo.setIndex(bArr);
    backGeo.computeVertexNormals();
    mesh(backGeo, M.wood);

    // Raised brass rim along the perimeter of the shield!
    const rimPts = [];
    for (let c = 0; c <= cols; c++) {
      const u = (c / cols) * 2 - 1;
      rimPts.push(V(u * W, H * 0.50, Math.cos(u * 0.92) * curve + 0.005));
    }
    for (let r = rows - 1; r >= 0; r--) {
      const v = r / rows, y = -H * 0.52 + v * (H * 1.02), hw = halfWidthAt(v);
      rimPts.push(V(hw, y, Math.cos(0.92) * curve + 0.005));
    }
    for (let r = 1; r <= rows; r++) {
      const v = r / rows, y = -H * 0.52 + v * (H * 1.02), hw = halfWidthAt(v);
      rimPts.push(V(-hw, y, Math.cos(-0.92) * curve + 0.005));
    }
    for (let i = 0; i < rimPts.length; i++) {
      const p0 = rimPts[i], p1 = rimPts[(i + 1) % rimPts.length];
      if (p0.distanceTo(p1) < 0.003) continue;
      const seg = pathLoft([{ p: p0, rx: 0.012, rz: 0.016 }, { p: p1, rx: 0.012, rz: 0.016 }], { segs: 8, fwd: V(0, 0, 1) });
      mesh(shadeVerts(seg, () => 0.98), M.brass);
    }

    // Central brass shield boss + leather arm straps (enarmes) on the back.
    const shBoss = new THREE.SphereGeometry(0.056, 16, 10);
    shBoss.scale(1, 1, 0.55);
    mesh(shadeVerts(shBoss, () => 1.0), M.brass, [0, 0.05, curve + 0.014]);
    for (const sx of [-0.08, 0.08]) {
      const strap = new THREE.BoxGeometry(0.034, 0.220, 0.014);
      mesh(shadeVerts(strap, () => 0.80), M.leather, [sx, 0.04, curve * 0.85 - 0.020], null, null, { crisp: true });
    }
  });
}

const scene = new THREE.Scene();
buildHeroPaladin(scene);
const glb = await new GLTFExporter().parseAsync(scene, { binary: true });
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, Buffer.from(glb));
let tris = 0;
scene.traverse((o) => {
  if (o.isMesh) {
    const g = o.geometry;
    tris += g.index ? g.index.count / 3 : g.attributes.position.count / 3;
  }
});
console.log(`Wrote ${out} (${scene.children.length} parts, ${Math.round(tris)} tris, ${(glb.byteLength / 1024).toFixed(1)} KB)`);
