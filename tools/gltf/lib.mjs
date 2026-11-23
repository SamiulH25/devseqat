// Shared bits for the glTF trial: the Azure palette, materials, and writing a scene as .glb from Node.
import * as THREE from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

// GLTFExporter reads its binary chunks with FileReader, which Node lacks.
globalThis.FileReader ??= class {
  readAsArrayBuffer(blob) { blob.arrayBuffer().then(b => { this.result = b; this.onloadend?.(); }); }
  readAsDataURL(blob) {
    blob.arrayBuffer().then(b => {
      this.result = `data:${blob.type || 'application/octet-stream'};base64,` + Buffer.from(b).toString('base64');
      this.onloadend?.();
    });
  }
};

/** An sRGB byte colour as a linear three.js colour. */
export const rgb = (r, g, b) => new THREE.Color(r / 255, g / 255, b / 255).convertSRGBToLinear();

/** The game's colours (Style.Standard Azure, Style.cs and UnitMatTable.Fill in Mesh3D.cs), by Mesh3D material id. */
export const Palette = {
  1: [54, 94, 176], 2: [30, 50, 108], 3: [230, 218, 178], 4: [196, 204, 218], 5: [110, 116, 128],
  6: [214, 172, 70], 7: [128, 86, 52], 8: [146, 102, 60], 9: [226, 180, 140], 10: [96, 64, 40], 13: [60, 46, 38],
};

export async function writeGlb(root, path) {
  const scene = new THREE.Scene();
  scene.add(root);
  const glb = await new GLTFExporter().parseAsync(scene, { binary: true });
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, Buffer.from(glb));
  return glb.byteLength;
}

export function triangles(root) {
  let n = 0;
  root.traverse(o => { if (o.isMesh) n += (o.geometry.index ? o.geometry.index.count : o.geometry.attributes.position.count) / 3; });
  return n;
}
