// Render .glb files to one PNG: a row a model, three views each (front three-quarter, side, back), same lights and
// camera for all, read back through GLTFLoader in headless Chrome (software GL): what any glTF reader would see.
//   node render_unit.mjs out.png a.glb[=Label] [b.glb=Label ...]
// Needs Chrome or Chromium; set CHROME=/path to pick one.
import puppeteer from 'puppeteer-core';
import { createServer } from 'node:http';
import { readFile, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { extname, join, resolve, relative, basename } from 'node:path';

const here = resolve('.');
const [outArg, ...models] = process.argv.slice(2);
if (!outArg || !models.length) { console.error('usage: node render_unit.mjs out.png a.glb[=Label] ...'); process.exit(1); }
const output = resolve(outArg);
const rows = models.map(m => { const [f, label] = m.split('='); return { file: resolve(f), label: label ?? basename(f, '.glb') }; });
const chrome = process.env.CHROME ?? ['/usr/bin/google-chrome-stable', '/usr/bin/chromium'].find(existsSync);
const W = 1500, RH = 640, H = RH * rows.length;

const page = `<!doctype html><html><body style="margin:0;background:#000;position:relative;font:600 20px sans-serif;color:#e8e2cc">
${rows.map((r, i) => `<div style="position:absolute;left:18px;top:${i * RH + 14}px;z-index:2">${r.label}<span id="t${i}" style="font-weight:400;opacity:.7"></span></div>`).join('')}
<script type="importmap">{"imports":{"three":"/node_modules/three/build/three.module.js","three/addons/":"/node_modules/three/examples/jsm/"}}</script>
<script type="module">
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
const W = ${W}, RH = ${RH}, N = ${rows.length}, views = [0.6, Math.PI / 2, Math.PI];
const r = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
r.setSize(W, RH * N); r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
r.outputColorSpace = THREE.SRGBColorSpace; r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 1.05;
r.setScissorTest(true);
document.body.appendChild(r.domElement);
const env = new THREE.PMREMGenerator(r).fromScene(new RoomEnvironment(), 0.04).texture;
const info = [];
for (let row = 0; row < N; row++) {
  const scene = new THREE.Scene();
  scene.environment = env; scene.environmentIntensity = 0.55;
  scene.add(new THREE.HemisphereLight(0xdfe8ff, 0x5a4a38, 0.9));
  const sun = new THREE.DirectionalLight(0xfff1d6, 2.6);
  sun.position.set(2.5, 5, 3.5); sun.castShadow = true; sun.shadow.mapSize.set(2048, 2048); sun.shadow.bias = -0.0004;
  Object.assign(sun.shadow.camera, { left: -2, right: 2, top: 3, bottom: -1 });
  scene.add(sun);
  const ground = new THREE.Mesh(new THREE.CircleGeometry(1.3, 48), new THREE.MeshStandardMaterial({ color: 0x6f7a4a, roughness: 1 }));
  ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; scene.add(ground);
  const unit = (await new GLTFLoader().loadAsync('/model/' + row)).scene;
  let tris = 0;
  unit.traverse(o => { if (o.isMesh) { o.castShadow = o.receiveShadow = true; tris += (o.geometry.index ? o.geometry.index.count : o.geometry.attributes.position.count) / 3; } });
  scene.add(unit);
  const h = new THREE.Box3().setFromObject(unit).max.y;
  document.getElementById('t' + row).textContent = '  ' + tris + ' triangles';
  info.push(tris);
  const w = W / views.length, y = (N - 1 - row) * RH;
  views.forEach((turn, i) => {
    const cam = new THREE.PerspectiveCamera(30, w / RH, 0.1, 50), d = 6.4;
    cam.position.set(Math.sin(turn) * d, 1.6, Math.cos(turn) * d);
    cam.lookAt(0, 1.12, 0);
    r.setViewport(i * w, y, w, RH); r.setScissor(i * w, y, w, RH);
    r.setClearColor(i % 2 ? 0x2b2f38 : 0x30343e);
    r.render(scene, cam);
  });
}
window.info = info; window.done = true;
</script></body></html>`;

const types = { '.js': 'text/javascript', '.glb': 'model/gltf-binary' };
const server = createServer(async (req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  try {
    let body, type = 'text/html';
    if (url === '/') body = page;
    else if (url.startsWith('/model/')) { body = await readFile(rows[+url.slice(7)].file); type = types['.glb']; }
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
  await tab.setViewport({ width: W, height: H });
  await tab.goto(`http://127.0.0.1:${server.address().port}/`);
  await tab.waitForFunction('window.done === true', { timeout: 120000 });
  const info = await tab.evaluate('window.info');
  await writeFile(output, await tab.screenshot({ type: 'png', clip: { x: 0, y: 0, width: W, height: H } }));
  console.log(`${output}: ${rows.map((r, i) => `${r.label} ${info[i]} tris`).join(', ')}`);
} finally {
  await browser.close();
  server.close();
}
