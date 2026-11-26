// Build the full modular parts pack, audit it, and only then replace Data/meshes/units3d.bin.
//   node build_release.mjs          build + audit + install into Data/meshes
//   node build_release.mjs --check  build + audit in out/ without touching Data/meshes
import { spawnSync } from 'node:child_process';
import { copyFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const checkOnly = process.argv.includes('--check');
const node = process.execPath;
const run = (script, args) => {
  const r = spawnSync(node, [resolve(here, script), ...args], { cwd: here, stdio: 'inherit' });
  if (r.error) throw r.error;
  if (r.status !== 0) process.exit(r.status ?? 1);
};

run('build_parts.mjs', ['out/parts.glb']);
run('export_imsh.mjs', ['--out', 'out/units3d.bin', 'out/parts.glb']);
run('mesh_audit.mjs', ['out/units3d.bin']);
if (!checkOnly) {
  copyFileSync(resolve(here, 'out/units3d.bin'), resolve(here, '../../Data/meshes/units3d.bin'));
  console.log('Installed out/units3d.bin -> ../../Data/meshes/units3d.bin');
}
