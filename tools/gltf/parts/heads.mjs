import { THREE, V, M, shadeVerts, heightShade, loft, column, lathe, bladeExtrude, addPart } from '../geom.mjs';

export function buildHeads(root) {
  addPart(root, 'helm_cap', (mesh) => {
    const crown = column([
      [0.045, 0.154, 0.168, 0, -0.012],
      [0.105, 0.154, 0.168, 0, -0.014],
      [0.160, 0.132, 0.144, 0, -0.014],
      [0.205, 0.086, 0.094, 0, -0.012],
      [0.230, 0.024, 0.028, 0, -0.010],
    ], { segs: 8 });
    mesh(crown, M.cloth);
    const brim = column([
      [0.036, 0.154, 0.168, 0, -0.012],
      [0.062, 0.158, 0.172, 0, -0.012],
      [0.088, 0.150, 0.164, 0, -0.014],
    ], { segs: 8 });
    mesh(brim, M.clothDark);
    for (const s of [-1, 1]) {
      const flap = shadeVerts(new THREE.BoxGeometry(0.022, 0.090, 0.056), () => 0.88);
      mesh(flap, M.clothDark, [s * 0.142, -0.005, -0.048], [0, 0, s * -0.06]);
    }
  });

  // 6. helm_hood: medieval gugel/chaperon hood with open face (+Z), closed crown & draped liripipe tail
  addPart(root, 'helm_hood', (mesh) => {
    // Open-front cowl wrapping back and sides (-Z, +-X), leaving face (+Z) exposed
    const cowl = column([
      [-0.12, 0.158, 0.164, 0, -0.012],
      [-0.03, 0.154, 0.164, 0, -0.014],
      [0.06, 0.156, 0.168, 0, -0.016],
      [0.13, 0.140, 0.156, 0, -0.016],
    ], { segs: 8, arc: [-Math.PI * 1.24, Math.PI * 0.24] });
    mesh(cowl, M.cloth);
    // Closed brow & crown above eyes (y >= 0.085)
    const crown = column([
      [0.085, 0.162, 0.174, 0, -0.012],
      [0.145, 0.148, 0.162, 0, -0.014],
      [0.195, 0.100, 0.112, 0, -0.016],
      [0.225, 0.026, 0.030, 0, -0.016],
    ], { segs: 8 });
    mesh(crown, M.cloth);
    // Draped liripipe tail at back
    const tail = loft(V(0, 0.16, -0.11), V(0, 0.05, -0.32), [
      [0, 0.064, 0.060],
      [0.45, 0.044, 0.042],
      [0.85, 0.024, 0.024],
      [1, 0.010, 0.010],
    ], { segs: 8, fwd: V(0, 1, 0) });
    mesh(shadeVerts(tail, () => 0.88), M.clothDark);
    // Neck collar wrap
    const collar = column([
      [-0.155, 0.168, 0.168, 0, -0.012],
      [-0.105, 0.162, 0.164, 0, -0.012],
    ], { segs: 8 });
    mesh(collar, M.clothDark);
  });

  // 7. uarm_cloth: normalized bone [0..1] along +Y (shoulder -> elbow)

  addPart(root, 'helm_kettle', (mesh) => {
    const dome = column([
      [0.055, 0.156, 0.168, 0, -0.008],
      [0.120, 0.152, 0.164, 0, -0.010],
      [0.185, 0.122, 0.132, 0, -0.010],
      [0.240, 0.068, 0.074, 0, -0.008],
      [0.265, 0.014, 0.016, 0, -0.008],
    ], { segs: 8 });
    mesh(dome, M.steel);

    // Wide sloped kettle brim
    const brim = column([
      [0.005, 0.258, 0.274, 0, -0.006],
      [0.032, 0.240, 0.256, 0, -0.006],
      [0.068, 0.158, 0.170, 0, -0.008],
    ], { segs: 7 });
    mesh(brim, M.mail);

    // Subtle crest comb & brow band
    const comb = shadeVerts(new THREE.BoxGeometry(0.020, 0.042, 0.22), () => 0.96);
    mesh(comb, M.steel, [0, 0.245, -0.01]);
  });

  // helm_sallet: Gothic sallet with swept lobster-tail neck guard, brow visor & dark iron bevor
  addPart(root, 'helm_sallet', (mesh) => {
    // Upper skull dome
    const skull = column([
      [0.055, 0.154, 0.170, 0, -0.012],
      [0.125, 0.150, 0.166, 0, -0.014],
      [0.195, 0.120, 0.134, 0, -0.014],
      [0.245, 0.064, 0.074, 0, -0.012],
      [0.265, 0.018, 0.022, 0, -0.010],
    ], { segs: 8 });
    mesh(skull, M.steel);

    // Swept rear lobster tail (-Z)
    const tail = loft(V(0, 0.08, -0.10), V(0, -0.03, -0.34), [
      [0.0, 0.148, 0.065],
      [0.5, 0.132, 0.052],
      [1.0, 0.098, 0.036],
    ], { segs: 7, fwd: V(0, 1, 0) });
    mesh(shadeVerts(tail, () => 0.90), M.steel);

    // Visor brow ridge across front (+Z)
    const visor = column([
      [0.032, 0.166, 0.184, 0, 0.004],
      [0.072, 0.160, 0.178, 0, -0.002],
    ], { segs: 8, arc: [Math.PI * 0.10, Math.PI * 0.90] });
    mesh(visor, M.mail);

    // Dark iron bevor (lower chin/neck guard)
    const bevor = column([
      [-0.125, 0.134, 0.144, 0, 0.004],
      [-0.045, 0.146, 0.158, 0, 0.008],
      [-0.010, 0.144, 0.154, 0, 0.006],
    ], { segs: 7, arc: [Math.PI * 0.14, Math.PI * 0.86] });
    mesh(bevor, M.mail);
  });
}
