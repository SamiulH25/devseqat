# World scale: a land four times as wide, with height and cliffs (WS0-WS8)

Status: **WS0 built 2026-11-24** (the 4x draft and its first relief, in the world page). WS1-WS8 open. Owner's brief 2026-11-24.
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

### WS2: Height in the game's land (headless)
`WorldDesign` reads `warp`, `uplands` and `cliffs`. The height function adds uplands (plateau: a flat top with a steep
skirt; massif: ridged peaks; hills: rolling noise) and cliffs as **a step, not a slope**: on the low side the ground stays
low right up to the line. Heights scale up with the land: map heights today stay under ~25 units. New map **layout 6**
(`WorldMap.CurrentLayout`); layout 5 keeps building from `Data/world.json` so saves on it keep their map. Test: a layout 6
map builds, its heights match the uplands, the step at a cliff is at least its height.

### WS3: Cliffs bar the way
Cliffs are barriers like the sea: `PlanRoute` and the AI's routes (`Campaign.SeaRoute` and friends) go round them or
through a pass; a party is never pushed up one. Roads only cross at passes (checked by the validator). Passes and bridges
become choke points the AI knows (`LordThink` blocks, holds, ambushes). Speed by slope and by land kind (high ground
slower, roads still fast). Test: a party ordered across a cliff takes the pass.

### WS4: The terrain at scale (drawing)
At 6,600 units with a 4-unit grid the map is about 2.7M height cells: too many for one mesh baked at load. Split the map's
terrain, foliage and places into chunks (e.g. 256 units), bake them on the worker thread, keep the near ones at full detail
and the far ones coarse, and stream as the camera moves (`World.Upload` per chunk). Cliffs get their own rock-face meshes
(strata, scree at the foot) along the line, so a cliff is crisp whatever the grid. `--world-bench` reports per chunk; frame
time at the widest zoom must not fall.

### WS5b: Things on the map at scale
One `MapScale` table (above) read by everything drawn on the map. Places: plans are laid in model space (today's sizes,
so the town plans, lots and works keep working) and the built mesh is scaled about the place's centre, its ground heights
sampled where the shrunk pieces stand; walls, gates and the doorstep follow. Parties: `FigureScale` 0.75, a column of
figures by party size and kind (lords' horse, levy foot, a caravan's wagons, a monster pack), and a banner marker that
takes over as the camera pulls back. Trees, dress and map life shrink with the places. Shots: `campaign-close`, `-road`,
`-holds`, `-village`, `place-*` at each step; the rider must stay readable at the default camera.

### WS5: The camera and the map at scale
Zoom bands: local (as now), regional (a province or two, places as models), strategic (the realm map M folded into the
main view: banners, borders, armies as counters). Labels, cards and the encounter ring keep their screen size. Sight
(`Vision`) and encounter radii stay in units (a man sees as far as he did), so the land feels larger, not emptier at once.

### WS6: Time and travel feel
`HourSeconds` to about 1.0 (2 real minutes town to town); speed-ups up to 8x or 16x; a march that auto-pauses on what
matters (a party in sight, arrival, an event, hunger). Forced march (faster, tiring), and camps at night as the C5 camp.
Long marches cost: food (forage by land: rich valleys, lean highlands), wear on horses, sickness in winter passes.

### WS7: The sim at scale
Party counts grow with area (`PartyScale`): 16x the land must not mean 16x the cost of a day (~76 ms now, `--campaign-bench`).
Parties far from the player think less often (an hourly brain every few hours), the encounter checks use the map index, and
news travels: the player hears of a siege or a war when word could have reached him (a delay by distance).

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
