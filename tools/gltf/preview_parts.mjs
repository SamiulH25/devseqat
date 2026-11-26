// Render each root part of a parts.glb into a labelled contact sheet, with the origin/axes visible.
// Usage: node preview_parts.mjs out.png parts.glb [--filter regex] [--cols 8] [--cell 176]
// Needs Chrome or Chromium; set CHROME=/path to pick one.
import puppeteer from 'puppeteer-core';
import { createServer } from 'node:http';
import { readFile, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { extname, join, resolve, relative } from 'node:path';

const [outArg, glbArg, ...rest] = process.argv.slice(2);
if (!outArg || !glbArg) {
  console.error('usage: node preview_parts.mjs out.png parts.glb [--filter regex] [--cols 8] [--cell 176]');
  process.exit(2);
}
let filter = '', cols = 8, cell = 176;
for (let i = 0; i < rest.length; i++) {
  if (rest[i] === '--filter') filter = rest[++i];
  else if (rest[i] === '--cols') cols = Math.max(1, +rest[++i] || cols);
  else if (rest[i] === '--cell') cell = Math.max(96, +rest[++i] || cell);
  else { console.error('unknown option ' + rest[i]); process.exit(2); }
}
const output = resolve(outArg), model = resolve(glbArg), here = resolve('.');
const chrome = process.env.CHROME ?? ['/usr/bin/google-chrome-stable', '/usr/bin/chromium'].find(existsSync);
if (!chrome) throw new Error('Chrome/Chromium not found; set CHROME=/path');

const page = `<!doctype html><html><body style="margin:0;background:#171a20;font:600 11px sans-serif;color:#e8e2cc">
<canvas id="c"></canvas><div id="labels"></div>
<script type="importmap">{"imports":{"three":"/node_modules/three/build/three.module.js","three/addons/":"/node_modules/three/examples/jsm/"}}</script>
<script type="module">
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
const palette = {
  0: [235,235,228], 1: [54,94,176], 2: [30,50,108], 3: [230,218,178], 4: [196,204,218], 5: [110,116,128],
  6: [214,172,70], 7: [128,86,52], 8: [146,102,60], 9: [226,180,140], 10: [96,64,40], 11: [132,104,72], 12: [92,64,52], 13: [60,46,38]
};
const filterText = ${JSON.stringify(filter)}, cols = ${cols}, cell = ${cell};
const loaded = (await new GLTFLoader().loadAsync('/model.glb')).scene;
let parts = loaded.children.filter(x => x.name);
if (filterText) { const re = new RegExp(filterText); parts = parts.filter(x => re.test(x.name)); }
if (!parts.length) throw new Error('no parts match');
const rows = Math.ceil(parts.length / cols), W = cols * cell, H = rows * cell;
const canvas = document.getElementById('c'); canvas.width = W; canvas.height = H; canvas.style.width = W + 'px'; canvas.style.height = H + 'px';
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
renderer.setSize(W, H, false); renderer.setScissorTest(true); renderer.outputColorSpace = THREE.SRGBColorSpace; renderer.toneMapping = THREE.ACESFilmicToneMapping;
const scene = new THREE.Scene();
scene.add(new THREE.HemisphereLight(0xdfe8ff, 0x4a4036, 1.6));
const sun = new THREE.DirectionalLight(0xfff1d6, 2.2); sun.position.set(2.5, 5, 3.5); scene.add(sun);
scene.add(loaded);
const mats = new Map();
for (const [id, rgb] of Object.entries(palette)) {
  const c = new THREE.Color(rgb[0]/255, rgb[1]/255, rgb[2]/255).convertSRGBToLinear();
  mats.set(+id, new THREE.MeshStandardMaterial({ color: c, metalness: +id === 4 ? 0.75 : +id === 6 ? 0.65 : +id === 5 ? 0.45 : 0, roughness: 0.72, vertexColors: true, side: THREE.DoubleSide }));
}
loaded.traverse(o => {
  if (!o.isMesh) return;
  const id = o.material?.userData?.materialId ?? o.material?.extras?.materialId ?? 0;
  o.material = mats.get(id) ?? mats.get(0);
});
const info = [];
for (const p of parts) {
  let tris = 0; const box = new THREE.Box3();
  p.traverse(o => { if (o.isMesh) { tris += (o.geometry.index ? o.geometry.index.count : o.geometry.attributes.position.count) / 3; } });
  p.updateWorldMatrix(true, true); box.setFromObject(p);
  info.push({ name: p.name, tris, min: box.min.toArray(), max: box.max.toArray() });
}
document.getElementById('labels').innerHTML = info.map((p, i) => {
  const x = (i % cols) * cell + 5, y = Math.floor(i / cols) * cell + 4;
  return '<div style="position:absolute;left:' + x + 'px;top:' + y + 'px;text-shadow:0 1px 2px #000;pointer-events:none">' + p.name + '<span style="opacity:.68;font-weight:400"> ' + p.tris + 'tri</span></div>';
}).join('');
const axisMat = new THREE.MeshBasicMaterial({ color: 0xff5848 });
const originDot = new THREE.Mesh(new THREE.SphereGeometry(0.025, 8, 6), axisMat);
const grid = new THREE.GridHelper(2, 8, 0x545d68, 0x30363f);
const axes = new THREE.AxesHelper(0.55);
scene.add(grid, axes, originDot);
for (let i = 0; i < parts.length; i++) {
  for (const p of loaded.children) p.visible = p === parts[i];
  const p = parts[i];
  const box = new THREE.Box3().setFromObject(p);
  box.expandByPoint(new THREE.Vector3(0, 0, 0));
  const centre = box.getCenter(new THREE.Vector3()), size = box.getSize(new THREE.Vector3());
  const radius = Math.max(0.24, size.length() * 0.5);
  grid.scale.setScalar(Math.max(0.5, radius)); axes.scale.setScalar(Math.max(0.35, radius * 0.8)); originDot.scale.setScalar(Math.max(0.7, radius * 0.7));
  const cam = new THREE.PerspectiveCamera(34, 1, 0.02, 100);
  cam.position.copy(centre).add(new THREE.Vector3(radius * 2.30, radius * 1.65, radius * 2.80));
  cam.lookAt(centre);
  const x = (i % cols) * cell, y = H - (Math.floor(i / cols) + 1) * cell;
  renderer.setViewport(x, y, cell, cell); renderer.setScissor(x, y, cell, cell); renderer.setClearColor((i + Math.floor(i / cols)) % 2 ? 0x20242c : 0x242932);
  renderer.render(scene, cam);
}
window.info = info; window.count = parts.length; window.width = W; window.height = H; window.done = true;
</script></body></html>`;

const types = { '.js': 'text/javascript', '.glb': 'model/gltf-binary' };
const server = createServer(async (req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  try {
    let body, type = 'text/html';
    if (url === '/') body = page;
    else if (url === '/model.glb') { body = await readFile(model); type = types['.glb']; }
    else {
      const file = join(here, url);
      if (relative(here, file).startsWith('..')) throw new Error('outside');
      body = await readFile(file); type = types[extname(file)] ?? 'application/octet-stream';
    }
    res.writeHead(200, { 'content-type': type }); res.end(body);
  } catch { res.writeHead(404); res.end(); }
}).listen(0, '127.0.0.1');
await new Promise(ok => server.once('listening', ok));
const browser = await puppeteer.launch({ executablePath: chrome, headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
try {
  const tab = await browser.newPage();
  tab.on('pageerror', e => console.error('page:', e.message));
  await tab.setViewport({ width: 900, height: 700 });
  await tab.goto(`http://127.0.0.1:${server.address().port}/`);
  await tab.waitForFunction('window.done === true', { timeout: 120000 });
  const meta = await tab.evaluate('({count: window.count, width: window.width, height: window.height})');
  await tab.setViewport({ width: meta.width, height: meta.height });
  await writeFile(output, await tab.screenshot({ type: 'png', clip: { x: 0, y: 0, width: meta.width, height: meta.height } }));
  console.log(`${output}: ${meta.count} parts, ${meta.width}x${meta.height}`);
} finally {
  await browser.close();
  server.close();
}
