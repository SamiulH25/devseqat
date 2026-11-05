# Ironfield design language: "Iron and Heraldry"

This document is the reference for how everything in Ironfield looks, reads and behaves, from a footman's sleeve to the
campaign map. The tokens it names live in code in `Style.cs`. When this document and the code disagree, fix one of them.

## 1. Pillars

1. **Read the battle at a glance.** Side, troop type and state must be readable from the command camera at 60 m.
   Colour says *whose*, silhouette says *what*, and the HUD says *how they are doing*.
2. **Honest materials.** Cloth, quilting, mail, plate, leather and wood each have one look everywhere.
   A man's worth is visible: better troops wear more metal and brighter heraldry.
3. **Heraldry is identity.** A side is its colours and its charge (the emblem on shields, tabards and barding). The UI uses
   the same colours as the cloth.
4. **The world is solid; the soldiers are drawn.** The land, trees and rocks are low-poly 3D, flat-shaded and lit by one sun.
   People and horses are crisp pixel-art puppets. Never mix the two (no 2D trees, no 3D soldiers).
5. **The interface is a field kit.** Dark iron plates with a hairline edge, gold only for what matters to *you*,
   short uppercase words. The UI frames the battle; it never competes with it.

## 2. The world (3D)

- **Shape:** low-poly and faceted. Spheres are once-subdivided icosahedra with jittered radii, trunks are 6-sided prisms,
  pines are stacked 9-sided cones. There are no textures on props: colour comes from vertex colour and lighting only.
- **Light:** one warm sun (`World.SunDir`, high at about 53 degrees, ahead and to the right of your starting view) plus a cool sky and a
  warm ground bounce (hemispheric ambient). Unit sprites carry their own painted top-left shading (`Style.SpriteLight`). In game,
  the scene's sun and sky light set their overall brightness, so a man walking into a wood's shadow darkens.
- **Shadows:** everything casts real sun shadows (terrain, trees, soldiers and horses), softened with a 3x3 filter.
  Soft blob shadows stay under every unit as contact darkening.
- **Palette:** muted and natural. Meadow greens, drier yellow-greens on high ground, earth browns on slopes and the road,
  and stone greys. Saturated colour is reserved for heraldry (units, banners) so the armies pop against the land.
- **Atmosphere:** a gradient sky dome (pale horizon to deep zenith) with a sun disc, a soft glow and slow drifting clouds; fog that
  fades distance into the horizon colour, thinner on high ground and warmer toward the sun.
- **Life:** grass and leaves sway in one wind (waves across the field plus slow gusts); leaves glow when lit from behind.
  Nothing else in the world moves.
- **Grade:** a gentle final grade. Saturation stays just under neutral so heraldry outshouts the land, the contrast is slightly lifted,
  highlights are split-toned warm and shadows cool (the same rule as the sprite ramps), with a light vignette. Values live in `Shading.cs`.
- **Readable ground:** the deployment ground is kept open. Woods frame the flanks and cue cavalry routes. The road is a
  strong line that orients you.

## 3. The soldiers (2D pixel puppets)

### Format
- **Density:** `Style.Px` = 0.03 m per pixel, so a man is about 58 px tall (2.5 times the first prototype's resolution).
  A part's metric size is fixed by the skeleton, so pixel sizes follow from it (a torso is 20x23 px, a sword 8x33).
- **Puppets:** about 15 parts per man on a 3D skeleton (`Puppet.cs`). Billboards (head, helmet, torso, tabard, boots) come in
  **front, side (facing right, mirrored for left) and back** views; limbs and weapons are axial strips; shields are world planes.
- **Proportions:** chunky and heroic. The head is about a quarter of the torso height and helmets are slightly oversized for
  readability. Weapons are true length (a pike is 2.7 m).

### Painting rules (`Painter.cs`)
- **Painted by code, not by hand:** every part is built from lit shapes (ellipse, tapered body, columns) so that all
  loadouts, tiers and sides come from one description. This keeps the style uniform as the troop list grows.
- **Light:** top-left, slightly from the front. Shading is **banded, not dithered**, in five tones per material:
  deep shadow, shadow, base, light, highlight.
- **Ramps are hue-shifted:** shadows lean cool (blue-violet), lights lean warm (cream). Never shade by darkening alone.
- **Outline:** a 1 px *selective* outline. Each edge pixel takes the darkest tone of the material it touches, darkened further,
  so a red tabard has a deep red edge and a steel helm a blue-grey one. No pure-black outlines.
- **Surface patterns** (applied in tone steps, so they always sit inside the ramp):

| Material | Pattern | Used for |
|---|---|---|
| Cloth | vertical fold lines every 6 px | tunics, hose, hoods, cloaks, caparisons |
| Quilt | diamond quilting every 4 px | gambesons (dyed in the side's colour) |
| Mail | 1 px checker | hauberks, chausses, aventails |
| Plate | contrast stretched one step (crisp highlights) | breastplates, helms, greaves, chanfrons |
| Wood | grain ticks | shafts, bows, round shields |
| Hair | diagonal strands | hair, manes, tails, plumes |
| Leather, brass, iron, skin | none | belts, saddles, fittings, faces |

### Sides and heraldry (`Style.Sides`)

| Palette | Cloth | Dark cloth | Trim | Charge | UI colour |
|---|---|---|---|---|---|
| 0 Azure (your company) | deep blue | navy | cream | chevron | bright blue |
| 1 Crimson (the enemy host) | crimson | oxblood | ochre | saltire | coral red |
| 2 Gold (you) | gold | umber | near-black | lozenge | gold |

- The **charge** is painted on kite and heater shields, tabards, caparisons and the horse's chest.
- **You are always gold**, so you can be found in any crowd.
- Future factions add a row: three cloth colours, a charge, and a UI colour that reads on the dark panels.

### Tiers you can see
Upgrading a company (in the campaign) must be **visible on the field**. Each step up the tree adds metal, then heraldry, then
full protection:

| Tier | Head | Body | Shield | Heraldry | Example |
|---|---|---|---|---|---|
| I (levy) | linen cap | dyed tunic | round wooden | painted quarters | Levies |
| II (regular) | kettle hat or hood | tunic or quilted gambeson | kite | charge on shield | Shieldmen, Spearrunners, Bowmen, Outriders |
| III (veteran) | nasal helm or kettle | mail | heater | tabard with charge | Men-at-Arms, Pikemen, Longbowmen, Lancers |
| IV (elite) | sallet, spangenhelm or great helm | mail, plate on the heaviest | heater, or none | tabard, often a cape; caparisoned destrier | Ironguard, Knights |
| V (champion) | sallet or great helm | plate (mail for some archers) | heater, or none | tabard, cape; plume on the heavy lines | Paladins, Banner Knights, Master Longbowmen |
| VI (household) | great helm or sallet | plate | heater, or none | tabard, cape and plume | Iron Legionaries, Paladin Knights, Royal Yeomen |
| VII (legend) | great helm | plate | heater, or none | tabard, cape and plume on every man | Immortals, Grail Knights, Sky Piercers |

Silhouettes also separate the lines: a **shield** means a line infantryman, a **long pole** a spearman (a longer one a pikeman),
a **bow** an archer, a **horse** cavalry, and a **plume** an officer, lancer or knight. Tier numerals (I-VII) appear on unit cards. Tiers V-VII use the same parts as tier IV and add
heraldry (tabard, then cape, then plume), so a high tier reads from across the field without new sprites. The Northmarch lines
keep fur, spangenhelms and round shields at every tier.

### Horses
- Coat parts are painted once in neutral grey and **tinted per horse** (bay, chestnut, black, grey, dun: `Style.Coats`),
  so a squadron never looks cloned.
- Gear is painted in the side's colours over the coat: a **saddle cloth** (coursers, warhorses) or a full **caparison**
  with crinet and a steel chanfron (destriers).

### Feedback on units
- Hit: a brief red tint (0.15 s). Routing: desaturated grey. Dead: the parts laid flat with blood.
- The player's own troops never fill the camera lens (friendly sprites within 6 m of the field camera are hidden).

## 4. Interface

### Tokens (`Style.cs`)
- **Panel:** slate (`Style.Panel`, 82% opacity), a 1 px edge (`PanelEdge`) and a 1 px top light (`PanelLight`): the
  "iron plate". There is one panel style; emphasis comes from the edge colour and weight, not from new shapes.
- **Colour roles:**
  - **Gold** (`Accent`) means *you*: your body, your selection, the highlighted choice, parries.
  - **Side colours** (`Friendly`, `Foe`) mean allegiance only.
  - **State colours:** `Ok` (steady, full), `Warn` (wavering, held fire), `Orange` (flanked), `Bad` (routing, wounds, defeat).
  - **Text and dim:** `Text` for content, `Dim` for labels and key hints.
- **Grid:** 4 px (`Style.Grid`) times the UI scale (1 at 720p, 2 at 1080p and up). Every margin and gap is a multiple.
- **Type:** one 5x7 pixel font in uppercase only, at integer scales: caption x1 (labels, key hints), body x2,
  heading x3, title x5. No other fonts and no mixed case.

### Components
- **Unit card:** index badge (gold when selected), tier numeral, order tag (HLD / CHG / ATK / FLW), a portrait built from the
  troop's own sprites, its name, men left, a strength bar (side colour) and a morale bar (state colour). A state word sits
  above the card when it is not steady.
- **Bars:** 4 px high on cards, 8 px in the player panel, always on a dark trough. Fill colours follow the colour roles.
- **Buttons:** a label on the left and its key on the right (dim). On hover: a gold edge and a gold wash.
- **Banners** (command view): a pole and a flag in the side colour with the company number (yours) or initial (theirs),
  and a morale strip beneath.
- **Order wheel:** eight sectors clockwise from the top: Charge, Advance, Form on me, Spacing, Fall back, Fire/Hold, Hold,
  Follow me. The chosen sector is gold.
- **Crosshair:** four chevrons for the attack directions. Gold is your chosen direction, red is an incoming blow.
- **Overlays** (menu, controls, result) dim the battle to 45% and centre one panel.

### Voice
- Orders and events are short and in the imperative or reporting voice: "HOLD POSITION!", "ENEMY HORSE ON THE FLANK!".
- Use numbers rather than adjectives where possible ("12/24", "+45% SPEED").
- A key hint always shows the *current* binding (from `controls.json`), never a hard-coded letter.

## 5. Motion

- Camera transitions ease (smoothstep, 0.45 s). Facing changes ease too, so turning is a sweep.
- Text in the event feed holds for 4.5 s and fades over 1.5 s. Hit text holds for 1.2 s.
- Nothing on the HUD bounces or slides. Emphasis is colour, not movement.

## 6. Campaign map (direction)

The campaign uses the same world language: low-poly 3D land under the same sun and fog, with armies shown as
**2D banner-bearer sprites** and company flags. A company's card on the campaign map is the same component as the battle
card, with an **upgrade** affordance that shows the next troops in the tree (portraits and tier numerals) and the XP and cost from
`troops.json`. Settlements are simple faceted 3D models in the world palette.

## 7. Balance rules (battle)

These are the working numbers that keep a battle winnable and cavalry from being an auto-win. They live as named constants in
`Battle.cs` (`RoutAt`, `RallyAt`, `MountedMeleeReach`) so the sim, the HUD and the tests cannot drift apart.

- **Routs are recoverable, not terminal.** A company breaks at 12 morale and rallies above 32, and a routing man gains morale
  once he is clear of the enemy (26 m). A man who has broken twice stays broken, so battles still resolve. The HUD's WAVERING
  warning uses `RallyAt`, so a company is never shown as steady while it is about to run.
- **A general heartens his whole army.** The captain who stands behind his line kept a permanent aura the player lost entirely
  in command view. A living general now gives every man +1.5 morale/s (+3 routing); being inside the aura adds more.
- **Reach is the counter to horses.** A troop's `reach` is its lance or pike. A rider's ordinary melee blow is capped at
  `MountedMeleeReach` (1.9 m), about a footman's, so a stalled charge is not free damage. Spears brace in any tight formation
  (`CanBrace`), not only on Hold, which the AI abandons the moment the lines meet.
- **The charge is a moment, not a state.** A couched lance is `10 + 4.2 x closing speed`, tramples `2.6 x closing`, horses
  lose more speed pushing through formed bodies, and the AI's cycle charge spends longer withdrawn. Arrows take the horse 55%
  of the time, where a footman's blade does 18%.
- **Fair rosters.** `Armies` builds the enemy host only 5% larger than yours at the same level, and `--balance` reports the
  win rate of even fights, so a systematic bias shows up as a number instead of a feeling.

## 8. Adding content

- **A troop:** add an entry to `Data/troops.json` (stats, tier, `upgradesTo`, look). The self-test checks the tree and that
  every look part has sprites.
- **A look part** (a new helmet, for example): add its name to the matching list in `Look`, paint its views in
  `SpriteSheet` from the shared materials (keep the metric size of the part it replaces), and add it to the tier table above.
- **A faction:** add a `Heraldry` row and a charge painter, and check that its UI colour reads on the slate panels.
- **A UI element:** build it from Panel, Bar, Text and Button, place it on the 4 px grid, and give it a colour role from
  section 4. Add no new colours without adding a token.
