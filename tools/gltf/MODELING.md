# Ironfield 3D unit modelling contract

This is the authoring contract for `Data/meshes/units3d.bin`. Read it before touching `tools/gltf/parts/*.mjs`, and read `STYLE.md` before designing or judging the visuals.
The pipeline is **not** "make a model in Three.js". It is a rigid modular mesh kit consumed by `Puppet3D.cs`; most mechanical mistakes are pivot, material-slot, or triangle-budget mistakes, while most visual failures come from ignoring the construction and silhouette language in `STYLE.md`.

## The loop

```bash
cd tools/gltf
npm ci                         # first time only
npm run check                  # build all parts to out/, audit the IMSH pack; does not touch Data/meshes
npm run preview                # writes out/parts-contact.png (after a build); inspect it yourself
npm run build-all              # build + audit, then install out/units3d.bin into Data/meshes
cd ../..
dotnet build -c Release        # copies Data into bin/Release
# or copy Data/meshes/units3d.bin into bin/Release/net10.0/Data/meshes/ when using --no-build
```

Authoritative visual checks use the game renderer, not the Three.js preview:

```bash
IRONFIELD_SAVES=/tmp/ifs IRONFIELD_TROOPS=/tmp/troops xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
IRONFIELD_TROOP_VIEWS=men_at_arms,knights IRONFIELD_SAVES=/tmp/ifs IRONFIELD_TROOPS=/tmp/troops xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
# preview an experimental partial pack in the real renderer without overwriting Data/meshes:
cd tools/gltf
node merge_pack.mjs /tmp/experimental-parts.bin /tmp/experimental-full.bin --audit
cd ../..
IRONFIELD_MESH_PACK=/tmp/experimental-full.bin IRONFIELD_TROOP_VIEWS=levies IRONFIELD_SAVES=/tmp/ifs IRONFIELD_TROOPS=/tmp/troops xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
dotnet run -c Release --no-build -- --bench-3d 1500
```

`npm run preview` is a fast geometry/pivot contact sheet. `IRONFIELD_TROOPS` is the truth: it uses `Puppet3D`, the real material table, the unit shader, pixel post and the game's camera scale. `--bench-3d` is the truth for cost. Look at the PNGs yourself before committing visual work.

`build_unit.mjs`, `blender_unit.mjs` and `render_unit.mjs` are comparison/legacy harnesses. They are **not** the production authoring path.

## Hard runtime constraints

1. **Rigid parts, not models.** Each scene root in `parts.glb` is one named IMSH part. The runtime positions whole parts; it does not preserve a hierarchy, skeleton or scene layout.
2. **Root transforms must be identity.** Put the pivot in the vertices. Children may have transforms; they are baked at export.
3. **Part names are the contract.** Names must exactly match `parts.manifest.json` and the names `Mesh3D.cs` resolves. A renamed or missing part silently becomes empty in game unless the audit catches it.
4. **No skinning or animation.** Articulation is C# code in `Puppet3D*.cs`. Anything that bends must be split at the joint.
5. **No textures, UV-painted detail, materials colours, lights, cameras or external files.** Only POSITION, NORMAL, grayscale COLOR_0 and `material.extras.materialId` survive export. UVs are discarded and object-space procedural UVs are computed at load.
6. **No compressed/extended glTF.** No Draco, meshopt, sparse accessors, external buffers, non-triangle primitive modes, skins or animations. Self-contained GLB only.
7. **Metres, +Y up, +Z forward.** Do not rescale to fit a viewer. The game expects human figures roughly 1.75 m tall at unit scale.
8. **Normals matter.** Faces are not culled (`CullNone`), but wrong winding/normals invert the cel lighting. Export outward CCW geometry with unit normals.
9. **COLOR_0 is ambient occlusion only.** Keep it grayscale, normally 0.55-1.0. It multiplies the runtime material colour; it is not albedo.
10. **Material IDs are palette slots, not materials.** Use integer slots 0..13 through `geom.mjs`'s `M` materials. A triangle must not mix slots.
11. **Triangle cost is multiplied.** IMSH is triangle soup, CPU-transformed again for every visible soldier. The camera pass and the shadow pass both pay it. A 100-tri limb used eight times costs 800 tris on one beast/horse, before the rider.
12. **No per-part LOD.** Detail 0 and Detail 1 reuse the same parts; Detail 1 only omits some props/subsegments. Detail 2 uses only `lod_foot`/`lod_horse`. If close geometry is expensive, the budget is wrong.
13. **Small geometry is wasted.** The game pixelates and outlines the render. Details thinner than roughly 1.5-2 cm usually become noise or disappear, but still cost vertices in both passes.
14. **The budget is a ratchet.** `parts.manifest.json` records the committed maximum for every part and assembled reference rig. New work may reduce cost, but may not increase it without deliberately editing the manifest. Targets are lower than the ratchet because the current pack is too heavy.

## Pivot spaces

The manifest assigns one pivot class to every part:

- **`unitBone`**: origin at the parent joint; the bone extends along +Y and is length-normalised to `y ~= 1`. `EmitBone` scales Y to the runtime joint distance. Toes, claws and forward detail point +Z. Examples: arms, legs, horse legs/tail/neck, spider legs, wyvern neck/tail.
- **`bodySpace`**: humanoid body coordinates with the unit origin at the feet. Chests, belts, skirts, shoulder layers, capes and web snares are authored at final body heights.
- **`headJoint`**: origin at the neck/head joint; face/muzzle points +Z. Human heads, helms, plumes and beast heads use this.
- **`grip`**: origin at the hand grip or rear end; shaft/blade runs +Y. Straight sword edges are ±X. Axes, curved blades and polearm cutting bits are ±Z (`WeaponEdgeZ` in `Mesh3D.cs`).
- **`shieldFace`**: origin at shield centre; decorated face points +Z; local +Y is up. Heraldry relief is emitted slightly in front of +Z.
- **`bodyCenter`**, **`wingRoot`**, **`emblemPlane`**, **`groundFigure`**, **`prop`**: see `parts.manifest.json`; keep the existing pivot stable unless the runtime emitter changes with it.

Outer envelopes enforced by the audit (larger means wrong scale/space, not heroically larger):

| pivot | hard envelope |
|---|---|
| `unitBone` | joint start y -0.20..0.15, tip y 0.85..1.30, lateral x/z within ±0.60 m |
| `bodySpace` | y 0.05..1.65 m, x within ±0.55 m, z -0.55..+0.45 m |
| `headJoint` | y -0.55..+0.60 m, x/z within ±0.70 m, origin inside the head volume |
| `grip` | rear/grip y -1.10..+0.06, tip y 0.25..2.80, lateral x/z within ±0.45 m |
| `shieldFace` | x within ±0.40 m, y within ±0.65 m, thickness/front z -0.02..+0.25 m |

These are outer cages, not target proportions. Match nearby existing parts first; use the reference sheets and game renders for the final silhouette.

## Material slots

| id | meaning in game |
|---:|---|
| 0 | neutral white/tint, bone, teeth, claws |
| 1 | team cloth |
| 2 | team dark cloth |
| 3 | team trim / heraldry |
| 4 | bright steel |
| 5 | mail / dark steel |
| 6 | brass / gold |
| 7 | leather |
| 8 | wood |
| 9 | skin |
| 10 | hair |
| 11 | horse coat or beast body |
| 12 | dark coat or beast accent |
| 13 | dark leather / shadow |

For beast rigs, slots **0 and 11-13 are the safe contract** because `UnitMatTable.FillBeast` defines those explicitly. Do not use human clothing/metal/skin slots in new beast parts unless `FillBeast` is extended deliberately; otherwise colours depend on whatever unit was drawn before. Reusing human helmets/weapons on beasts needs the same review.

## Budgets and art direction

Read the battle first: **colour says whose, silhouette says what**. Interior detail comes third.

- Flat, broad facets; chunky heroic proportions; large shapes that survive a 60 m command camera.
- Sharp silhouette cues: helmet brim/crest, shield shape, weapon head, ears/snout/tail, shoulder mass. Not wrinkles, fur hairs, fingers or jewellery-scale buckles.
- Use procedural shader patterns for mail, scale, quilting and brushing. Do not model those textures.
- Humanoid/mass-use parts: normally 4-8 radial segments. Organic hero forms may use more where the silhouette needs it, but every extra segment needs a reason.
- Prefer remodelling one large plane over adding three small floating detail pieces. Avoid coplanar shells and z-fighting.
- Radially symmetrical caps often make degenerate triangles at zero-radius rings; collapse or omit them when easy.

Reference rigs in the manifest (cost / target / ratchet max):

| rig | cost / target / max |
|---|---:|
| light infantry | 1806 / 1400 / 1806 |
| veteran infantry | 1790 / 950 / 1790 |
| plate infantry | 1842 / 1050 / 1842 |
| crossbowman | 1946 / 1100 / 1946 |
| plain horse | 2392 / 1400 / 2392 |
| barded horse | 2752 / 1700 / 2752 |
| wolf | 2624 / 1000 / 2624 |
| bear | 3396 / 1600 / 3396 |
| spider | 3356 / 1200 / 3356 |
| wyvern | 2454 / 1600 / 2454 |
| brute | 3278 / 1800 / 3278 |

The light-infantry target was revised from 900 to 1400 after a controlled authoring experiment: an 865-triangle levy was mechanically valid but visually generic because the focal shapes were underbuilt. Cost targets must leave enough budget for the visual floor in `STYLE.md`.

These targets mean the current pack is already too dense; do not treat "same cost as the old part" as automatically good. But they are an optimisation direction, not permission to erase the troop's identity. Never meet a target by starving the helmet, torso construction, weapon or shield until the model becomes generic; `STYLE.md` gives the required visual floor. When remaking a part, reduce waste and invisible density first. When editing several parts, judge the rig total, not each part in isolation.

## Special tooling

- `parts.manifest.json` — machine-readable inventory, pivot class, per-part ratchet and assembled-rig budgets. Add an entry whenever adding a part.
- `npm run check` — full build plus `mesh_audit.mjs`, without installing into `Data/meshes`.
- `npm run audit` — audit the installed `Data/meshes/units3d.bin` and print rig costs. For an experimental subset pack outside the full inventory, run `node mesh_audit.mjs your-pack.bin --partial`; this skips full-inventory/runtime-reference completeness but still checks format, pivots, materials and budgets.
- `npm run preview` — per-part contact sheet with origin axes/grid, material-slot colours and triangle counts. Use `--filter '^(wolf_|bear_)'` when iterating. Example direct command: `node preview_parts.mjs out/wolf.png out/parts.glb --filter '^(wolf_|quad_)' --cols 4`.
- `npm run build-all` — audited full build, then installs `Data/meshes/units3d.bin`.
- `npm run build-module -- <module> [--check]` / `npm run build-mounts` — audited partial merge for one module. Never hand-run `export_imsh.mjs` straight into `Data/meshes`; that bypasses the budget and pivot gates.
- `IRONFIELD_TROOPS` / `IRONFIELD_TROOP_VIEWS` — actual game-rendered troop PNGs. Always use these before committing a visual model change. `IRONFIELD_MESH_PACK=/path/pack.bin` previews an experimental IMSH pack in the real renderer without overwriting `Data/meshes`; because it replaces the whole pack, use `node merge_pack.mjs overlay.bin merged.bin --audit` for partial experiments so stock emblems/support parts remain present.

- `node assemble_humanoid.mjs assembled.glb your-pack.bin --render turnaround.png` — canonical neutral-standing humanoid assembly and three-view render. It mirrors the main `Puppet3D` joint positions closely enough to catch pivot/proportion/clipping problems and applies the Azure material-slot palette for inspection. It is not a substitute for `IRONFIELD_TROOP_VIEWS`; use `--set key=part` to choose chest/helm/limb/weapon/shield parts.
- `--bench-3d 1500` — actual CPU vertex-emission cost and triangle count by LOD/pass.

## Adding or changing a part

1. Decide the runtime user first: which `Look`, troop, beast or emitter path uses it? A part with no runtime use is dead weight.
2. Add/edit the builder in the right `parts/*.mjs` module using `geom.mjs` primitives. Keep the exact snake_case name.
3. Add or update its entry in `parts.manifest.json` (`module`, `pivot`, `maxTris`). New parts must fit the rig budget; update the manifest only intentionally.
4. Run `npm run check`. Fix every error. Treat target warnings as design feedback, not noise.
5. Run `npm run preview` and inspect the pivot, silhouette, material slots and density.
6. Install with `npm run build-all`, rebuild/copy the binary, then inspect the relevant `IRONFIELD_TROOP_VIEWS` PNGs in game.
7. Run `--bench-3d 1500` and compare before/after ms and triangles.
8. If the runtime mapping changed, update `Mesh3D.cs` in the same change and add/extend validation or a self-test where practical.

The reference PNGs in `tools/gltf/reference/` are art-direction targets for silhouette, construction and restraint, not permission to copy every small shape or raise density. When the references and this contract disagree, this contract wins; when a technically valid model still reads as generic, `STYLE.md` wins over the audit.
