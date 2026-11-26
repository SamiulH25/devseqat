# Ironfield troop visual style bible

Read this together with `MODELING.md`. `MODELING.md` says what the runtime can load; this file says what makes a troop look like Ironfield rather than a generic low-poly medieval asset. A clean audit is only permission to keep iterating. It is never visual acceptance.

The reference sheets in `tools/gltf/reference/` are part of this bible. Open them while modelling:

- `game-tier-progression.png` — real game-rendered infantry progression across several families.
- `game-turnarounds.png` — six-view game renders of levies, clansmen, shieldmen, men-at-arms and foot knights.
- `game-front-reads.png` — low-tier/front-read comparison at game scale.
- `parts-bodies.png`, `parts-heads-helms.png`, `parts-limbs.png`, `parts-weapons-shields.png` — the installed modular-part vocabulary, isolated with material-slot colours.
- `anti-example-first-pass.png` — the first compliant-but-generic experiment. Do not copy its weakened focal shapes.

## 1. The core look: designed simplicity

Ironfield is low-poly, but it is **not primitive**. The models are built from a small number of large, deliberate shapes that describe how the equipment is made and worn. Every major shape should answer one of these questions:

- What garment or armour layer is this?
- How is it fastened, folded, layered, patched, or reinforced?
- What is its battlefield purpose?
- What makes this troop readable from the command camera?

If a shape is only "a cylinder because limbs are cylinders" or "a box because torsos are boxes", it is not finished. Ironfield simplicity comes from compression and selection, not from omitting design.

Avoid three failure modes:

1. **Asset-store medievalism** — smooth generic peasants/knights with no specific construction.
2. **Toy geometry** — capsule limbs, ball heads, plain hemispherical caps, wedge weapons, disc shields.
3. **Noise** — many tiny protrusions, wrinkles, rivets, or patches that disappear at game scale while costing triangles.

## 2. The three reads

Design for these in order.

### First read: command-camera silhouette

At distance the player must read:

- team colour
- headwear silhouette
- weapon type and reach
- shield presence and shape
- armour weight / tier
- broad body posture and mass

If the troop does not read here, no interior detail can save it.

### Second read: equipment construction

At troop-card or close battle range the player should read:

- cloth versus leather versus mail versus plate
- whether clothing is wrapped, belted, layered, padded, fur-lined, or improvised
- shield rim/face/boss/emblem construction
- weapon head, collar, shaft and grip construction
- boot/cuff/glove interruptions on otherwise simple limbs

### Third read: craft accents

Only after the first two work:

- buckles, pouches, small trim strips, rivet rows, patches, seams, scarf ends, hood tails
- one or two personal/improvised details
- restrained heraldry

Do not invert this order. Small accents cannot substitute for missing silhouette or missing construction.

## 3. Figure and silhouette language

Ironfield humanoids are compact and weighty rather than tall fashion figures.

- Put visual mass in the **headwear, shoulders/chest, belt/hip layer, shield and weapon**.
- Limbs are subordinate connectors. They may be simple, but not completely featureless: interrupt them with cuffs, wrapped forearms, gloves, boot cuffs, flared boot feet, or armour terminals.
- Clothing should have a controlled outline: shoulder width, sleeve volume, taper at the waist, skirt/hem flare, and a broken lower edge. Avoid one straight tube from shoulder to knee.
- Headwear is one of the strongest unit identifiers. A cap, hood, kettle, spangenhelm, turban, or brim must change the silhouette. A plain hemisphere is not enough.
- Weapons are slightly exaggerated for readability. The head must have a distinct silhouette and a believable junction to the shaft.
- Shields are identity objects, not circles stuck to arms. Build rim thickness, face curvature/planking, boss, rear thickness and a large simple emblem or construction pattern.
- Break perfect bilateral symmetry at least once: a pouch, diagonal strap, scarf end, patched shoulder, hanging hood tail, wrapped wrist, uneven hem, or asymmetric cap flop. Do not overdo it.

## 4. Construction vocabulary by component

### Cloth body layers

Cloth is modelled as a shell with volume and construction, not a painted body.

Use some combination of:

- overlapping chest and skirt shells
- flared or split hems
- sleeve cuffs and shoulder seams
- quilt/padding bands broad enough to survive pixelation
- diagonal chest straps, cross belts, belts with buckles/pouches
- patched panels that are raised/recessed enough not to z-fight
- trim only where it clarifies an edge

Poor troops are not blank troops. A levy's gear should look improvised, handed down, repaired or work-worn, but still intentionally designed.

### Leather and armour layers

Leather and armour need construction logic:

- leather reads through harness straps, layered chest pieces, thick belts, pouches and dark edges
- mail is normally a simple shell plus the shader pattern; spend geometry on aventails, collars and cuffs rather than modelling links
- plate reads through large overlapping plates, fluting/ridges, trim and articulated terminals
- brigandine/scale/lamellar need rows or plates large enough to read as construction, not noise

### Heads and helmets

The head/helmet assembly is a focal point. It deserves real triangle investment.

- Keep a readable skin/hair/face zone unless the helm is fully enclosing.
- Helmet brims, nasals, cheek plates, hood tails, cap flops, crests, turban wraps and rivet bands are all valid silhouette devices.
- Do not use a smooth ball cap or featureless dome as the primary design.
- Even a peasant cap needs a plan: floppy point, folded brim, seam, patch, hood tail, or asymmetric sag.

### Weapons

Weapons are read by silhouette before realism.

- The head should be large enough to identify at game scale.
- Separate blade/head, collar/socket, shaft and grip materials.
- Axes need a shaped blade, not a rectangular wedge: beard, toe, heel, hammer/back-spike, or asymmetrical work-axe profile.
- Spears/polearms need a shaped point and collar, not a cone on a stick.
- Swords need guard/pommel construction and a blade taper.
- A levy weapon can be improvised or tool-like, but it still needs a designed cutting edge and mounting.

### Shields

Shields are heraldic construction objects.

- The silhouette must be deliberate: round, heater, kite, pavise, tower, asymmetric plank, hide-covered frame, etc.
- Build thickness: rim/edge thickness, face plane, boss, rear shadow.
- Emblems should be large, simple and geometric: chevron, animal head, bar, sun, split field, nailed planks, hide stitching. Tiny icons vanish.
- A plain disc plus a small boss is a failure for a new hero/part design unless intentionally hidden behind stronger equipment.

### Limbs and boots

Limbs can remain cheap, but they frame the silhouette.

- Cloth arms/legs may be simple faceted columns.
- Add interruption where the eye expects it: sleeve cuff, bare forearm, wrist wrap, knee overlap, boot cuff, boot foot/toe.
- Boots should not be painted-on column endings. The foot needs a forward volume and enough shape to read from the side.
- Do not spend large budgets on fingers, muscle anatomy, knee anatomy or cloth wrinkles.

## 5. Colour and material hierarchy

Team colour says allegiance; material contrast says equipment.

- Team cloth is usually the largest field, but it must be interrupted by skin, leather, wood, steel, dark cloth or trim.
- Leather/wood should be warm and structural: belts, harness, grips, shafts, boots, shield backing.
- Steel/metal indicates tier, professionalism or scavenged protection. Even one helm or shoulder plate can change the read.
- Team trim/heraldry is a sharp accent, not a second main colour everywhere.
- Dark cloth/leather is for recesses, edging, gloves, boots and shadowed construction.
- Do not make the whole body a single blue mass with small brown boots. That is the generic failure mode.
- Material slots are not paint-by-number. Use them to construct the object: rim versus face, blade versus shaft, cap brim versus crown, harness versus cloth.

## 6. Where triangles go

Do not spread triangles evenly. Spend them where the player looks.

The installed levy baseline with axe is approximately **1,826 assembled triangles** before any optimisation:

| group | current levy tris | share |
|---|---:|---:|
| head + helm | 398 | 22% |
| chest + skirt + belt | 300 | 16% |
| limbs, doubled | 696 | 38% |
| weapon + shield | 432 | 24% |

The first failed experiment spent only **865 assembled triangles**. It roughly preserved these proportions but halved the absolute investment in every focal shape, producing exactly the generic look we do not want.

For new infantry work:

- Treat the manifest ratchet maximum as the hard ceiling.
- Treat lower target numbers as an optimisation direction, **not** permission to erase identity.
- Do not chase the lowest possible count. A visually strong 1,400-triangle levy is better than a generic 850-triangle levy.
- For a cloth levy/militia redesign, a sensible quality target is roughly **1,300–1,700 assembled triangles**, with 1,806 as the hard ceiling for the reference infantry rig unless the manifest is deliberately changed.
- If the result is below roughly 1,200, justify how the helmet, torso construction, weapon and shield still carry the design.
- Limbs should be efficient, but focal parts must not be starved just because they are optional in a naive budget average.

## 7. Creativity rules

Creative does not mean random. Every design needs a small, coherent fiction.

Before modelling, write this brief and keep it with the report:

```text
Role:
Who carries this equipment, and what work or social class does it come from?
Silhouette signature:
The one shape that identifies the troop at command-camera distance.
Construction story:
Three visible ways the gear is made, fastened, repaired, layered or improvised.
Asymmetry:
One deliberate asymmetrical detail.
Material hierarchy:
What dominates, what interrupts, what is the rare/prestige material?
Game-scale read:
What must remain readable after pixelation/outline?
Deliberate omissions:
What detail is intentionally not modelled?
```

Good levy/militia invention examples — **illustrative, not a shopping list. Do not copy this exact set; invent a different coherent construction story for each new troop:**

- a work cap with a folded brim and one sagging point
- a patched quilted chest layer with broad reinforcement bands
- a diagonal rope/leather chest harness carrying a pouch
- a split apron-like skirt with a pale repaired hem
- a woodcutter axe with a bearded blade and visible socket
- a plank-and-hide round shield with thick rim, visible nail heads and a large simple white mark

Bad invention examples:

- random spikes, rivets or patches without construction logic
- copying the exact current levy part-for-part
- fantasy ornament that contradicts a poor militia role
- perfectly symmetrical clean factory-made gear
- details so thin they become pixel noise

## 8. Required visual process

For a new troop or redesign, follow this order:

1. **Study the references.** Open the game renders first, then the isolated part sheets.
2. **Write the creative brief.** Do this before geometry.
3. **Block the silhouette.** Headwear, body mass, shield and weapon first.
4. **Build construction.** Add hems, cuffs, straps, layers, rims, collars, boots and material separation.
5. **Assemble and render in the real game renderer.** Smooth external renders are diagnostic only.
6. **Compare side by side with current troops at the same scale.** At minimum compare a levy/militia design against `levies`, `clansmen`, `shieldmen` and one richer reference such as `men_at_arms`.
7. **Iterate the design, not just the audit.** If it reads as a generic medieval NPC, redesign the focal shapes.

For experimental IMSH packs, use the real-renderer override instead of overwriting game data. `IRONFIELD_MESH_PACK` replaces the **whole** pack, so first merge a partial experiment over the stock pack; otherwise heraldry, arrows, mounts and support meshes are missing:

```bash
cd tools/gltf
node merge_pack.mjs /path/to/experimental-parts.bin /tmp/experimental-full.bin --audit
cd ../..
IRONFIELD_MESH_PACK=/tmp/experimental-full.bin \
IRONFIELD_TROOPS=/path/to/out \
IRONFIELD_TROOP_VIEWS=levies \
IRONFIELD_SAVES=/tmp/ifs \
xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
```

Use the exact runtime part names for the pieces being previewed. The merged pack preserves stock heraldry/support meshes, so the preview matches final integration much more closely than a sparse partial file.

## 9. Visual rejection checklist

Reject or rework the design if any of these are true:

- The cap/helmet is a plain hemisphere or blob with no constructed silhouette.
- The torso is a smooth tube/box with no hem, sleeve, belt, strap, layering or construction read.
- The shield is only a flat disc plus boss, or its emblem is too small to read.
- The weapon head is an undirected wedge/blob with no blade profile, collar or grip logic.
- The body is one uninterrupted team-colour mass.
- There is no deliberate asymmetry or equipment story.
- The troop could plausibly be a generic asset-pack peasant/knight with no Ironfield-specific construction.
- The result was judged only in a smooth external renderer and never in the game renderer.
- The triangle savings came primarily from weakening the headwear, shield, weapon or torso construction.

The final judge is the game-rendered image at game scale, checked by a human/authoring agent. No metric can replace that.
