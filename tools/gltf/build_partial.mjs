// Build one parts module, merge it over the installed pack, audit the result, and only then install it.
// Usage: node build_partial.mjs <bodies|limbs|heads|weapons|props|mounts|beasts> [--check]
import { spawnSync } from 'node:child_process';
import { copyFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const modules = process.argv.slice(2).filter(x => !x.startsWith('--'));
const checkOnly = process.argv.includes('--check');
if (modules.length !== 1) {
  console.error('usage: node build_partial.mjs <bodies|limbs|heads|weapons|props|mounts|beasts> [--check]');
  process.exit(2);
}
const mod = modules[0];
const node = process.execPath;
const run = (script, args) => {
  const r = spawnSync(node, [resolve(here, script), ...args], { cwd: here, stdio: 'inherit' });
  if (r.error) throw r.error;
  if (r.status !== 0) process.exit(r.status ?? 1);
};
const glb = `out/${mod}.glb`, merged = `out/units3d.${mod}.bin`, base = resolve(here, '../../Data/meshes/units3d.bin');
run('build_parts.mjs', [glb, '--only', mod]);
run('export_imsh.mjs', ['--out', merged, '--merge', base, glb]);
run('mesh_audit.mjs', [merged]);
if (!checkOnly) {
  copyFileSync(resolve(here, merged), base);
  console.log(`Installed audited ${mod} merge -> ../../Data/meshes/units3d.bin`);
}
