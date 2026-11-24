# World scale: a land four times as wide, with height and cliffs (WS0-WS8)

Status: **layout 6 is the default for new campaigns since 2026-11-24** (the owner's call; the self-test still plays on
layout 5, `Campaign.TestLayout`, and the draft has its own tests). **WS0, WS1, WS2 and most of WS3 built 2026-11-24** (the 4x draft and its first relief in the world page; layout 6 builds it with its heights; cliffs bar the way; all headless), and a first WS7 pass (party counts, the two worst costs). WS1, the rest of WS3 (the AI and choke points), WS4-WS8 open. Owner's brief 2026-11-24.
This settles `archive/HOLDS.md` HD6 (world size): the land grows, travel slows.

> "A major world overhaul including elevation, cliffs, and a massive increase in scale of the world. There's no reason for
> the player to make it from town to town that fast." (owner, 2026-11-24)

## Owner's decisions (2026-11-24)

1. **Scale: 4x linear** (16x the area).
2. **Town to town takes about 2 real minutes at normal speed**: the player has the time speed-ups for the rest.
3. **Redraw the current continent** (Roven), do not start a new one: keep its realms, places and shape, and fill the new room.
4. **Lower settlement density**: the 4x spread does it (the same ~420 places on 16x the land); more places only where the
   owner draws or agrees to them.
5. **Things on the map shrink too, parties most** (later the same day): the land still grows 4x, and on it towns and
   castles are drawn smaller and parties smaller again (see "Smaller things" below).

## Where things stood (layout 5, `Data/world.json` revision 17)

| | Now (in game: drawn x `DesignScale` 1.33) | At 4x |
|---|---|---|
| Land across | 1,650 units | 6,600 units |
| Town to nearest town (median) | 217 units: 30 game hours on a road, **15 s** real | 870 units: ~121 game hours (5 days) |
| Any place to the nearest place | 36 units: 5 h, 2.5 s | 145 units: 20 h |
| Whole continent on foot | ~7 days, 85 s | ~4 weeks |

Speeds: foot 5.5 units an hour, horse 9, roads x1.3 (`Balance`); `HourSeconds` 0.5 real seconds a game hour at 1x.
**For 2 real minutes town to town at 4x, `HourSeconds` goes to about 1.0** (a game day = 24 s), speeds unchanged. Halving
the speeds instead would double the game days a trip takes and drag every daily system (food, wages, events) along; a
slower clock keeps a march at 5 days. Check with `--pacing` (career stage days will rise: march time is real game time now).

## Smaller things instead of (only) a longer land (owner's idea, 2026-11-24; looked into)

**What is oversized now.** Against the land (1 unit is about 0.6 km on a 1,000 km continent) everything on the map is a
token: a party's rider is drawn at 2.5x life (`MapScreen.FigureScale`) and stands ~6 units tall, half as wide as a town
(`PlacePlans.FootprintOf`: town 13, castle 4.2-9, village 5.5 units, i.e. a town ~8 km across); trees at 0.72
(`MapTreeScale`). A rider beside a town reads as a giant beside a hamlet, so the land reads small whatever the distances.

**Tried** (debug `IRONFIELD_MAP_FIGURE=x`, map shots `campaign-close.png` and `campaign-road.png` at 2.5, 1.0 and 0.6):
at 1.0 the rider is about a house's size and still reads at the default camera (the "YOUR COMPANY" plate does the rest);
at 0.6 he is a speck and needs a closer camera or a marker. Shrinking the riders alone makes the town look even bigger.

**What it does and does not do.**
- Smaller figures alone change only the look: the march still takes 15 s. Travel time is distance over speed, so the time
  goal is met only by more distance or a slower clock, whatever size the riders are.
- Shrinking *everything on the map* by k (riders, places, trees, roads and rivers, encounter and join radii, doorsteps,
  map life, the camera's zoom range) plays exactly like a world 1/k as large, **without the terrain growing**: the height
  grid keeps its cell count, which is the costly part of a 4x land (WS4: 2.7M cells to stream).
- The catch: the 4-unit height grid grows coarse against small things (a rider 2 units tall on 4-unit cells); hills
  stay fine, but close views need a finer grid near the camera (a local patch, far cheaper than streaming all the land),
  and cliffs stay crisp because they are their own meshes (WS4).

**Decided (owner, 2026-11-24): both.** The land still grows 4x (the draft as drawn, built at `DesignScale` 1.33 as now),
*and* everything on it shrinks, parties more than places. Starting numbers, to tune by shots:

| On the map | Now | Scale | Then |
|---|---|---|---|
| Party rider (`FigureScale`) | 2.5 (~6 units tall) | x0.3 | 0.75 (~1.8 units) |
| Town / great castle / castle / tower house / village footprint (`FootprintOf`) and its model | 13 / 9 / 6.2 / 4.2 / 5.5 | x0.5 | 6.5 / 4.5 / 3.1 / 2.1 / 2.75 |
| Trees and map dress (`MapTreeScale`) | 0.72 | x0.5 | 0.36 (a tree never outgrows a keep) |
| Road and river widths, doorsteps, the place click radius | as drawn | x0.5 | |
| Camera zoom range (25-620) and near plane | | close end x0.4 | 10-620 |

What that gives: town to town is 4x as far, 8x as far measured in towns, about 13x measured in riders. Encounter, join and
sight radii (3.5, 10, 55) are game rules, not looks: they are tuned in WS7 with the 4x land (contacts must not get rare),
not shrunk with the figures. Time is unchanged by this: `HourSeconds` ~1.0 for 2 real minutes town to town (WS6). The
terrain cost is still the 4x land's (WS4 streaming), plus a finer grid near the camera so 1.8-unit riders do not walk on
4-unit facets. Small riders need help to read: a party is a **column** (riders and foot, longer with more men) close up,
and a **banner marker** with its plate further out (WS5b).

## The draft (WS0, built)

- `Data/world6.json`: revision 17 of the drawn world with every point x4 (`extent` 2480), ridges 4x as wide, rivers 1.5x,
  lakes 4x. `"warp": 4` scales the province border warp so borders keep their shape (the page does `warp(p/4)*4`; the game
  port must do the same, bit-exact, at layout 6). The live `Data/world.json` and layout 5 are untouched; nothing in the game
  reads the draft yet.
- Open it: `tools/world.py --file Data/world6.json` (or `IRONFIELD_WORLD=Data/world6.json`); `--check`, `--summary`,
  `--inbox`, `--shot` take the same switch. The page's zoom thresholds (labels, villages, sites) read zoom as the 1x map would.
- **New kinds of thing** (page tools Cliff and Upland, validated by `tools/world.py`):
  - `uplands`: `{id, kind: plateau|massif|hills, height, points}`: a closed area of raised land, `height` in world units.
  - `cliffs`: `{id, height, points}`: a sheer drop; **the high ground is on the left** as the line runs (the page draws teeth
    pointing down the drop). Nothing climbs a cliff; a road through a gap in one is a pass.
- **First relief** (each pinned as a model note asking the owner to keep, move or drop it):
  - the **Tarn plateau**: the Chapter of Tarn on a table of rock, cliffs on its west and south rims, entered by the roads;
  - the **Skarl fjell**: the northern highlands as a high plateau, the west coast in sea cliffs;
  - the **Jebel**: the badlands as a mesa country with an escarpment over the western coast and sea cliffs to the south;
  - **hills**: Vespra's uplands with the **Dalmatian gorge** (the river between cliffs), the Krakow hills, the Castile downs;
  - **Brittany's** chalk sea cliffs.
  Made by a script that keeps each cliff clear of places and cuts it where a road crosses (that gap is the pass).

## Phases

### WS1: Settle the redraw (owner and model, in the page)
Answer the relief notes; then fill the new room: split big provinces where the gaps are wide (a province ~4x the old one
reads as empty), wild land between realms (forest, moor, waste: no settlement, monster country), waypoints about a day
apart on the roads (inns, ferries, toll bridges, watchtowers: as sites), and river crossings (fords and bridges as named
points). Every addition goes in as a note first (docs/world/README.md). Ends when the owner calls the draft settled.

**As built (2026-11-24): settled by the model.** The owner left the relief notes and the redraw to the model. All seven
relief notes are kept (built and tested). Revision 4 of `Data/world6.json`:
- **Provinces split**: every land province over ~60,000 square units (draft) is cut into pieces by k-means on its cells'
  warped points (the page's own warp, so borders keep their look), the old centre fixed. A piece is refused or pushed
  inland when it would move the coast by more than 8 cells of 8 units, or take a settlement from another province's
  family. 81 -> **167 land provinces**; the coast moved by 131 such cells in all (small nicks).
- **Wild land**: a new piece with no settlement turns to monster country: lowland to Forest (Marsh by a river), Steppe to
  Badlands, Oasis to Desert; 17 such.
- **Inns** (site kind `inn`, `Campaign.RoadSiteKinds`): one every ~150 draft units of drawn road (~200 game units, a long
  day on foot), not within 55 of a settlement, 90 of another site, 25 of a cliff or 12 of a river: **39**. Named by the
  people round them (Caravanserai of X in the south, Yam of X on the steppe, The Raven Hall in the north, The Red Lion
  elsewhere). **Toll bridges** (`toll`) where a drawn road crosses a drawn river, on the near bank: **3** (the draft has
  five rivers). Both are moved onto the routed road when the map is built (`WorldMap.Roadside`), with a model
  (`World.SiteModel`), an event (`site_inn`: news, beds, bread; `site_toll`: news, pay, talk your way across), a vignette
  in the event book and a tavern rumour. Shot `campaign-inn.png` (map-only shots, draft only). Test `RoadsideStops`.
- Not done: named fords and ferries beyond the tolls, and waypoints on the AI's or the player's off-road ways.

### WS2: Height in the game's land (headless)
`WorldDesign` reads `warp`, `uplands` and `cliffs`. The height function adds uplands (plateau: a flat top with a steep
skirt; massif: ridged peaks; hills: rolling noise) and cliffs as **a step, not a slope**: on the low side the ground stays
low right up to the line. Heights scale up with the land: map heights today stay under ~25 units. New map **layout 6**
(`WorldMap.CurrentLayout`); layout 5 keeps building from `Data/world.json` so saves on it keep their map. Test: a layout 6
map builds, its heights match the uplands, the step at a cliff is at least its height.

**As built (2026-11-24).** `WorldDesign` reads `warp`, `cliffs`, `uplands` (`WorldDesign.Draft()` reads `Data/world6.json`);
`WorldMap.Generate(seed, layout: WorldMap.DraftLayout)` (6) builds it via `FromDesign(d, realms, 6)` in **2.5 s** headless;
`CurrentLayout` stays 5 until WS4 can draw it. Relief in `Campaign/WorldRelief.cs` (`DesignRelief`, `Relief`, `CliffLift`,
`Ragged`), added in `DesignedHeight` after the shore so a sea cliff drops sheer to its beach. Uplands: plateau a flat top on a
30-unit skirt, massif peaks on 140, hills swells on 90. Cliffs: the full height on the high side up to the line (a step ~2
units wide), easing out over `CliffReach` (4 x height, at least 40) inland, tapering to nothing toward its ends (a pass is a
ramp). Both are read at a point moved by a smooth noise up to `RagAmp` (8) a side, so drawn straight edges come out ragged and
a cliff stays on its plateau's rim. `--relief out.png [layout] [pixels]` (`ReliefDump.cs`) draws any layout as shaded relief
with roads, cliffs and places: the way to look at heights until WS4 draws the land. Test `ReliefRaised`.
Found on the way:
- `MapIndex` took 20 s on the 4x map (every cell against every road bend): now built a block of 8x8 cells at a time in
  parallel, each block keeping only the roads within (its nearest + 2 half diagonals + 2h), and river lists cut where no
  bend can still be reached. Same answers (`MapIndexMatches`, the `--world-bench` checksum unchanged): 0.2 s.
- `River.Dist` skips bends more than 40 units off on both axes (kept for old seeds): the draft's ~100-wide ridges came out
  clipped square. `River.Skip` (default 40; layout 6 ridges their width + 8), read by `MapIndex` too.
- A river's berth round places and its bends grew with the map's span (150 units at 4x) and pushed rivers off their drawn
  lines through the gorge walls: on the draft they use layout 5's span, and rivers keep straight within 30-80 units of a
  cliff (`CliffCalm`).
- The draft's upland heights had been written 4x too high (fixed to 22, 26, 16, 10, 9, 7).
Left for WS1/WS8 tuning: the province hills (`hills`, radius by span) are 270-500 units wide domes at 4x and stack on the
Vespran hills into one white dome; scale them by span / warp when the redraw settles.

### WS3: Cliffs bar the way
Cliffs are barriers like the sea: `PlanRoute` and the AI's routes (`Campaign.SeaRoute` and friends) go round them or
through a pass; a party is never pushed up one. Roads only cross at passes (checked by the validator). Passes and bridges
become choke points the AI knows (`LordThink` blocks, holds, ambushes). Speed by slope and by land kind (high ground
slower, roads still fast). Test: a party ordered across a cliff takes the pass.

**As built (2026-11-24), the barrier and the climb.** `WorldMap.OnCliff(p, half)` (WorldRelief.cs): p within `half` of a
cliff's line, read at the ragged point as the heights are, where the step still stands at least `BarredFrom` (0.35) of its
height; toward a cliff's ends the step tapers below that, so a gap between two cliffs is a walkable ramp (a pass).
`CrossesCliff(a, b)` samples every 2 units against a 6-wide band. Wired in: the player's planner (`TravelCostAt`: a face is
impassable, band 6 wide so no diagonal step slips through the 4-unit grid), the AI's land grid (`PlanOver`, band 9 on its
6-unit grid, which also forbids slipping between two shut cells), `OverLand` (a straight way up a face is not over land, so
`Campaign.SeaRoute` sends AI parties round), and the road router (`Router.Barred`; a wider search when a cliff stands
between, and a leg keeps its cells' own way where smoothing would cut over a face). `DesignRelief` now runs before
`DesignRoads`, so the links the drawing left out never run up a cliff. Off the road, `SpeedFactor` multiplies by
`Climb(p)`: 1 on the flat (a plateau's top too), down to 0.45 on an upland's skirt or a scarp's back slope, from the
relief's slope only. Test `CliffsBar`: below to above the middle of the Tarn rim, 60 units straight, the player's way is
~1,120 and the AI's ~1,070 (round by a pass); no road leg climbs a face. Layout 5 unchanged (`--world-bench` checksum).
Still open in WS3: passes and bridges as places the AI knows (holds, blocks, ambushes), and battles fought on a cliff's
edge or in a pass taking their field from it (`BattleGround`).

### WS4: The terrain at scale (drawing)
At 6,600 units with a 4-unit grid the map is about 2.7M height cells: too many for one mesh baked at load. Split the map's
terrain, foliage and places into chunks (e.g. 256 units), bake them on the worker thread, keep the near ones at full detail
and the far ones coarse, and stream as the camera moves (`World.Upload` per chunk). Cliffs get their own rock-face meshes
(strata, scree at the foot) along the line, so a cliff is crisp whatever the grid. `--world-bench` reports per chunk; frame
time at the widest zoom must not fall.

**Measured (2026-11-24): layout 6 already draws.** `IRONFIELD_MAP_LAYOUT=6` with `IRONFIELD_CAMPAIGN_SHOTS` renders every map
shot: the map's terrain and world meshes are already in culled tiles (the overview draws ~47k of 5.6M terrain triangles).
So WS4 is not "build streaming" but **memory and load time**: `IRONFIELD_MAP_LAYOUT=6 --world-bench` gives a 1,678^2 terrain
field, 199k foliage items, **50M vertices in 15k tiles, 1,881 MB of buffers** (layout 5: 148 MB), 41M of them the foliage;
terrain 6.7 s and world meshes 5.1 s to build. Next in WS4: foliage by tile on demand (build a tile's trees and dress when it
first comes near the camera, drop it far away) and fewer, larger far trees; then the terrain's far tiles coarser.

**As built (2026-11-24), plants streamed.** `World.Plants.cs`: on a map with `World.StreamFrom` (40,000) foliage items or
more, plants and rocks (`Plant(kind)`) go into 96-unit tiles with a box sure to hold them, and are built on a worker the
first time the camera's view meets a tile (48 nearest a batch), sent to the tile's own buffer (1.5M vertices a frame), and
let go, longest unseen first, past `PlantBudget` (8M vertices, ~290 MB). Shots (`Ui.Instant`) build what they see at once.
Sites, places and roadside pieces still build at load (they carry chimneys). The drawn world is under the threshold and
builds exactly as before (checksum unchanged). Layout 6: buffers at load **1,881 -> 466 MB**, world meshes 5.4 -> 2.5 s,
the map shots run 121 -> 42 s and look the same. Left in WS4: the terrain (2.8M vertices, the 1,678^2 field) and its far
tiles coarser, cliff rock meshes, and the frame rate with the map in motion on real hardware.

### WS5b: Things on the map at scale
One `MapScale` table (above) read by everything drawn on the map. Places: plans are laid in model space (today's sizes,
so the town plans, lots and works keep working) and the built mesh is scaled about the place's centre, its ground heights
sampled where the shrunk pieces stand; walls, gates and the doorstep follow. Parties: `FigureScale` 0.75, a column of
figures by party size and kind (lords' horse, levy foot, a caravan's wagons, a monster pack), and a banner marker that
takes over as the camera pulls back. Trees, dress and map life shrink with the places. Shots: `campaign-close`, `-road`,
`-holds`, `-village`, `place-*` at each step; the rider must stay readable at the default camera.

**As built (2026-11-24), first pass.** `WorldMap.ThingScale` (0.5 on the draft, 1 elsewhere) rides on
`Terrain.ThingScale` into the drawing: trees and dress (`World.treeScale` x it), rocks, flowers, sites and roadside pieces
(`World.Item`), the places (built at their drawn size from their plans, then each shrunk about its middle onto the ground
where its pieces now stand, chimneys and fires too: `BuildPlaceTile`), road ribbons (`World.Water.cs`), and map life's
folk, carts, caravans and beasts (`MapLife.K`). Parties: `WorldMap.FigureShrink` 0.3 sets the puppets' scale in
`MapScreen.Open`. Only drawing changed: plans, footprints, `RoadDist` and the sim are as they were; the drawn world's
checksum is unchanged. Shots (`IRONFIELD_MAP_LAYOUT=6 IRONFIELD_MAP_ONLY=1`): the rider reads at the close camera; a
castle is about four riders across. Still open: the plans' footprints and the ground pad under a place (flattened at the
old size), the click radius and labels, rivers, boats, the camera's close end (WS5), and columns and banner markers.

### WS5: The camera and the map at scale
Zoom bands: local (as now), regional (a province or two, places as models), strategic (the realm map M folded into the
main view: banners, borders, armies as counters). Labels, cards and the encounter ring keep their screen size. Sight
(`Vision`) and encounter radii stay in units (a man sees as far as he did), so the land feels larger, not emptier at once.

**As built (2026-11-24), the close end.** `WorldMap.CloseZoom` (10 on the draft, 25 elsewhere) is the wheel's near
clamp in `MapScreen`; the near plane already follows the zoom. Shot `campaign-near.png` (map-only shots) is the camera at
its closest on your company. Still open: the regional and strategic bands, labels and ring at screen size.

### WS6: Time and travel feel
`HourSeconds` to about 1.0 (2 real minutes town to town); speed-ups up to 8x or 16x; a march that auto-pauses on what
matters (a party in sight, arrival, an event, hunger). Forced march (faster, tiring), and camps at night as the C5 camp.
Long marches cost: food (forage by land: rich valleys, lean highlands), wear on horses, sickness in winter passes.

**As built (2026-11-24), the clock.** `WorldMap.HourScale` (1.5 on the draft, 1 elsewhere) multiplies
`Balance.HourSeconds` in `MapScreen.Update`. Test `MarchTakesMinutes`: on foot from each town to its nearest town, along
the way the player's planner takes and at the map's speed there, the median is **123 s real at normal speed** (34-563 s,
19 towns; 15 s at 8x); mounted parties are ~40% quicker. Open in WS6: a 16x step, auto-pause on what matters beyond the
encounter and arrival stops there are, forced march, foraging by land, wear and winter sickness.

### WS7: The sim at scale
Party counts grow with area (`PartyScale`): 16x the land must not mean 16x the cost of a day (~76 ms now, `--campaign-bench`).
Parties far from the player think less often (an hourly brain every few hours), the encounter checks use the map index, and
news travels: the player hears of a siege or a war when word could have reached him (a delay by distance).

**First pass (2026-11-24).** `--campaign-bench` on layout 6 began at 3,362 parties (1,722 lords) and 14.8 s a game day
(layout 5: 262 parties, 117 lords, ~234 ms). Found and fixed:
- `Campaign.IsProvinceWebbed` swept every party for every party's speed every step (quadratic; 27% of the day): now the
  webbed provinces are found once a game hour (`webbed`, cleared when lairs grow).
- The AI's way round sea and cliffs searched a 6-unit grid (1.2M cells): `SeaCell` is 6 x the square root of the warp (12
  on the draft); steps beside a cliff (`cliffNear`) also check the face itself, so none slips past a cliff's end.
- Party counts: `WorldMap.LordScale` (lords, and their refit budget) is the drawn world's on the draft, since the realms
  and their seats are the same; `RoamScale` (brigands, pilgrims) grows with the land's width, not its area, so the wide land
  is emptier between places. Both equal `PartyScale` on every other map. **A starting value for the owner to judge.**
Now: 548 parties (117 lords), **~790 ms a day** (move 600). After the redraw (167 provinces) it rose to ~2 s a day: brigands' wander goals fell on
cliff faces, whose shut cells no step may enter, so each way round searched the whole land (~20 ms) and failed, again
every step. Now `PlanOver` aims at the nearest open cell that reaches such a goal without crossing (`OpenNear`), and
`Campaign.SeaRoute` remembers a goal it found no way to (`SeaWay = [goal]`) instead of searching again: **~580 ms a day**
(move 370; 588 parties, 118 lords). Layout 5 unchanged (~240 ms). Still to do: the AI's long ways (cache a way per goal, plan by
province or road graph, think less often far from the player), and the world's other per-area counts (lairs, nests,
holdouts, caravans) checked on the draft.

### WS8: Retune and docs
Pacing (`--pacing`), realms (`--realms`), events (`--events`) at the new scale: contract distances, quest timers, lord
travel, caravan counts, monster lairs per area, siege relief windows. README, docs/CAMPAIGN.md, tips and the landing page
numbers. The draft becomes `Data/world.json`'s successor for new campaigns; old saves stay on layout 5.

## Open questions for the owner

1. The relief notes in the draft: which to keep, move or drop?
2. Fill the new room with **more provinces** (smaller ones, same realms) or keep 81 big ones?
3. Speed-ups: is 8x enough at the top, or 16x?
4. Height: should a plateau also change what grows and who lives there (fewer villages, highland peoples), or only how
   armies move and fight?
