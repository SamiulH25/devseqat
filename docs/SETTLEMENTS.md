# Settlements: the town and castle models overhaul (SM1-SM7)

Status: **planned 2026-11-20**, SM1 and SM2 built 2026-11-20 (see their "As built"), SM3 next. Owner's brief (2026-11-20): overhaul the town and castle models.
Owner's answers:
1. **Scope:** towns, castles in all three sizes and villages on the campaign map, and the siege battlefield (the wall,
   towers, gate and houses you fight over) made to match.
2. **Variety:** by **subculture**, the 16 peoples of the drawn map (`Data/world.json` `subcultures`), each a variant of
   its realm's style.
3. **Live state:** a model shows its **owner's banners**, its **size and prosperity**, **siege and damage**, and the
   **buildings built** there.

## Where things stood (2026-11-20)

Every place was a few primitives in `World.cs`, the same in every land:
- **Town** (`Town`): a round 14-segment wall with cone-capped towers every third segment, 12 identical tile-roofed
  cottages at random angles, and a box hall with a cone spire. It ignores roads, rivers and the coast.
- **Castle** (`Castle`): four grey wall slabs, a cone-capped drum at each corner and a gabled box keep. No battlements,
  no gatehouse, nothing in the yard. **Great castle** (`GreatCastle`): the same, nested. **Tower house** (`TowerHouse`): a box
  under a gable with one turret and a low yard.
- **Village** (`Village`): 5-7 timber cottages in a ring.
- Nothing reads the place's culture, owner, prosperity, buildings or siege. Party sprites stand at a fixed spot "before the gate"
  (`MapScreen.UpdateBearers`: 9.5 south of a town or castle, 5.5 south of a village), wherever the gate really is.
- Cost: houses were built from boxes (a window alone was ~30 triangles), so 280 villages made up a large share of the
  2.56M foliage vertices (`--world-bench`).

The drawn map has 19 towns, 123 castles (28 tower houses, 78 castles, 17 great castles), 280 villages and 23 ports.

## Design

### Look
The world's rules hold (docs/DESIGN.md 2): low-poly, faceted, vertex colour only, one sun. What changes is the
**silhouette**. Each people must be recognisable from the map's working zoom (about 100) by the outline alone: tower
caps, roof pitch, wall material, and the landmark. Up close (zoom 25-60), the detail reads: merlons, timbering, windows
lamplit at night (the existing window tag), masonry (the existing masonry tag) and flags in the wind.

Detail is flat quads laid on surfaces, not boxes: a window is 2 triangles, a timber beam 2, a merlon 5. That lets a town
hold 60-120 houses for what 12 used to cost.

### The style table (`Architecture.cs`, headless)
A `PlaceStyle` for each subculture: wall and trim colours, roof colour and shape (gable, steep, hip, flat, dome, turf,
tent), house kind (timber frame, stone, whitewashed cube, log hall, chalet, tall canal house, stilt house, yurt, black
tent), defence (stone, brick, mud brick, timber palisade, earth bank), tower plan (round or square) and cap (cone,
pyramid, flat with merlons, dome, onion, timber hoard), and landmark. Realm cultures (older and generated maps) map to
their heartland subculture. The tavern's painted town (`MapScreen.LookOf`) reads its `Build` from the same table, so the
3D place and its picture agree. That also fixes the picture on the drawn map, where a settlement's culture is a
subculture id and every town was painted as timber.

| Subculture | Houses | Roofs | Defences | Towers | Landmark |
|---|---|---|---|---|---|
| azure_heart Crownlanders | timber frame, cream | blue slate, steep gable | stone curtain | round, tall slate cone | cathedral with spire |
| azure_march Border Reivers | rubble stone bastles | grey slate, low | rough stone | square peel towers, flat, merlons | beacon tower |
| azure_coast Bayfolk | tall timber frame | blue slate gable | stone | round, cone | harbour church, cranes |
| crimson_patrician High Compact | ochre stucco | red tile, low hip | brick | square, swallowtail merlons; tall tower houses | campanile and dome |
| crimson_alpine Cragfolk | chalet: white base, dark timber | steep shingle, wide eaves | stone | square, pyramid helm | spired church |
| northern_fjord Fjord-Reavers | plank longhalls | turf | earth bank and palisade | timber, pitched | stave hall |
| northern_taiga Wood-Clans | dark log cabins | steep plank | log wall | square timber, tent roof | wooden church, onion domes |
| southern_oasis River-Lords | whitewashed cubes, gardens | flat, domes | mud brick, white | square, flat, merlons | great dome and minaret |
| southern_dune Dune-Kin | ochre adobe; black tents | flat | mud brick kasbah | tapering square, flat | tall mud minaret |
| southern_rift Rift-Corsairs | white, blue doors | flat, domes | sea wall | round, flat | domed bazaar, light tower |
| steppe_horde Free Banners | felt yurts | yurt cones | earth bank and palisade | timber watch towers | great yurt, horse-tail standards |
| steppe_salt Salt-Riders | mud brick, yurts outside | flat | mud brick | round, blue dome | blue-domed tomb |
| league_lagoon Lagoon-Guilds | tall coloured stucco | red tile | brick | square, merlons | campanile, domed basilica |
| league_fen Fen-Folk | brick, stepped gables; stilt houses | reed thatch | brick, dykes | round brick, cone | windmill and brick church |
| order_chapter Vow-Keepers | grey ashlar | slate | concentric stone | square, massive, flat | chapter house and bell tower |
| order_warden Pass-Wardens | stone huts | slate | stone gate forts | square, pyramid | fortified gate |

### The plan of a place (`Campaign/PlacePlans.cs`, headless)
Derived at each map build (nothing saved), from the map: kind, size, style, seed, **road bearings** (where roads leave
it), the **water** round it (sea, river, lake: which way and how near), the port's quay, and the footprint. Gates face
roads, harbours face the water, a castle sits square to its approach. `Terrain.Places` carries the plans to the renderer.
`MapScreen.UpdateBearers` stands a party at the plan's **doorstep** (outside the gate it would use) instead of a fixed spot.

### Places are their own meshes, rebuilt when their state changes
Places leave the foliage tiles and get their own culled buffers (`World.Places.cs`), grouped by map tile so the overview
stays at tens of draw calls. Each place has a **state** (`PlaceState`: owner's colours, prosperity band, walls level,
buildings, battered band, besieged, raided). `MapScreen` compares the states each game hour and rebuilds only the tiles
whose places changed, on a worker thread (like map life), then uploads them. The first build runs in `MapScreen.Prepare`
with the campaign's state, so loading shows the right banners.

### Kit (`World.Kit.cs`)
Shared pieces, each taking a style: wall runs that follow the ground (with walkway and merlons, or palisade stakes),
towers (round or square, any cap), gatehouses (passage, portcullis, flanking towers), houses of each kind with their
roofs, windows and doors, keeps and halls, landmarks, flags (team-coloured, swaying in the wind with the leaf sway), and
small things (market stalls, wells, cranes, carts, stacks, gardens, fences).

## Phases

### SM1: The kit, the style table, the castles
- `Architecture.cs`: `PlaceStyle` and the 16 styles plus realm fallbacks; `Build` feeds `MapScreen.LookOf`.
- `PlacePlan` (road bearings, water, doorstep) built in `WorldMap.BuildTerrain`, carried on `Terrain.Places`.
- Places in their own tiled buffers (`World.Places.cs`), with a state hook (static for now), and a `places` stage in
  `--world-bench` for vertex counts.
- The kit (`World.Kit.cs`) and the castles in their three sizes for every style: the **tower house** (a tall tower and its
  walled yard; a ringfort hall in the north, a kasbah in the dune, a fortified camp on the steppe), the **castle** (a
  curtain with flanking towers and a gatehouse facing the road, a keep and hall, buildings against the walls), the
  **great castle** (concentric: outer bailey and inner ward, great keep). Each castle stands on its own ground: footings
  step down a slope instead of sinking into it.
- Shots: `IRONFIELD_PLACES=1` with `IRONFIELD_CAMPAIGN_SHOTS` saves `place-<subculture>-<kind>.png` for the nearest place of
  each style and kind, with labels and parties hidden (`IRONFIELD_PLACES=steppe` limits it to matching styles).
- Tests: `PlacesPlanned` (every place has a style and a plan; gates face roads; the doorstep is on dry land and outside the
  footprint), `PlaceStyles` (every subculture in world.json and every realm culture has a style).

**As built (2026-11-20).**
- One test, `PlacesPlanned` (Tests/MapTests.cs), covers both planned tests: every subculture and realm culture has a style,
  every settlement a plan, the gate faces the main road, the doorstep is dry and outside, nothing grows in a footprint, and
  flags go up and come down as the holder changes (the tile is rebuilt).
- Plans are made for every map layout (`BuildTerrain` calls `PlanPlaces`), so old saves on layouts 0-4 get the new castles
  too; a seed's terrain and foliage outside the footprints do not change. Towns and villages are still the old models
  (`World.Town`, `World.Village`), drawn through the place tiles; SM2 and SM3 replace them. `World.Castle`, `GreatCastle`
  and `TowerHouse` are no longer reached on the map (`World.Item` skips settlements when `Terrain.Places` is filled); SM7
  deletes them if the siege field (SM6) does not want them.
- State: `PlaceState` carries only the flags' palette (`MapScreen.PlaceStateOf`: the player's fiefs in gold, else the
  realm's colours, villages none). The map polls it every 15 frames (`World.UpdatePlaces`) and rebuilds changed 96-unit
  tiles on a worker task; `FlushPlaces` uploads them on the main thread.
- The kit lays finer courses than the battlefield's wall: masonry tag `(0,-1.25)` (`World.Courses`), read in world.glsl
  as 2.2x the pattern. Timber in walls and towers is weathered toward grey (`World.Weathered`) so palisades read from afar.
- Flags are drawn 1.35x tall and 1.6x long against the first cut: smaller ones did not read at the working zoom.
- Small holds by people: tower house (Crownlands, Compact, League, Orders, the Rift), ringfort (the north), kasbah (oasis,
  dune, salt sea), the khan's camp (the horde: a great yurt on a platform, no motte). Castles of palisade peoples are a
  motte and bailey (`MotteAndBailey`); the horde's is a camp with the great yurt.
- Budget (`--world-bench 11`): foliage fell from 2.56M to 1.65M verts (nothing grows in footprints, old settlement items
  moved out); places 1.29M verts, built in ~80 ms on the worker. Map terrain build +90 ms (`PlanPlaces`' road rays).
- Not done: per-castle variety within one style beyond size and seed (tower counts, keep placement); castles on steep
  slopes still stand on deep footings rather than terraces; the old towns still sit in the new footprint unchanged.

### SM2: Towns
- The footprint follows the land: stretched along the main road, cut by the river or shore, never in water. The wall
  circuit (polygon with towers at the corners) has gates where roads cross it. Streets run along the roads inside, houses
  stand in rows facing the street, and a market square with the landmark sits where the main roads meet.
- Ports: the wall opens on a quay with warehouses and cranes by the pier. League towns get canals, fen towns dykes and mills.
- Steppe towns: an earth bank and palisade round a timber core, yurts outside. Dune towns: a ksar, a block of adobe.
- Test: no house of a town stands in water or on a road.

**As built (2026-11-20).**
- The layout is headless and lives in the plan (`Campaign/TownPlans.cs`, `WorldMap.LayTown`): `PlacePlan.Ring` (the wall's
  corners, stretched along the main road, pulled in off water; corners still wet are dropped), `Gates` (where a drawn
  road's middle crosses an edge, and a water gate toward a quay within 6 of the wall), `Square` and `Lots` (`Lot`: middle,
  turn, frontage, depth, `LotUse`). `World.Towns.cs` only raises what the plan says, so the test checks the plan.
- Streets are the map's own road ribbons, narrowed to 0.62 of their width inside a town (`PlacePlan.StreetScale`, read
  by `World.Roads` and by the layout): at full width a town road (5.2) left no room for houses. Houses stand in rows along
  them (a row facing the street, one back to back, a lane, and on: 7 bands), then along lanes the map does not draw: out
  from the square between the roads, round 3.1 inside the wall, and a ring halfway out. Rows by people (`Rows`): terraced
  tall houses and stucco, attached cubes in the south (a ksar), detached halls, cabins and chalets.
- A town holds 9-59 houses (median ~30, seed 7): towns where seven roads meet or that stand out on the water hold fewest.
  The test asks for the fewest >= 8 and the median >= 25. Houses are drawn `lean` (bare side walls, plain backs).
- The landmark sits on the square on the side away from the roads (`World.Chapel` at 1.4, the cathedral at 1.65), a
  smaller one anywhere inside the wall when the square is crowded. Market stalls under striped awnings stand round it.
- Ports: a crane on the first dry ground in from the quay and up to three warehouses by it, wholly inside the wall or
  wholly outside; foliage is cleared under any lot past the footprint (`BuildTerrain`). The steppe pitches up to 12 yurts
  outside the bank and keeps a timber core (cabins) among the yurts inside; the fens' windmill stands out in the fields.
- Dropped: the lagoon's canals (both lagoon towns on the drawn map stand out on the water with no room for one, and a
  canal crowded out their quays) and the fens' dykes (no fen town on the drawn map). Revisit if the world gains them.
- Planning all places takes ~150 ms of the map build (towns laid out in parallel after every base plan is known).
- Working on layouts: `IRONFIELD_PLAN_SVG=/dir dotnet run -c Release --no-build -- --selftest TownsLaidOut` writes each
  town's plan from above (water, roads at full width, wall, gates, lots by use, doors) as `plan-<town>.svg`.

### SM3: Villages
- By style: a street village along the road, or houses round a green; the house kind of the people (cottages, longhalls,
  chalets, cubes, yurts and tents, stilt houses over the fen). A small church or shrine, a well, garden plots and fences.
- Budget: a village costs no more than it does today.

### SM4: The place's state: banners, prosperity, buildings
- **Banners:** flags on towers and keeps in the owner's realm colours (`Style.Sides`), changing when the place changes hands.
- **Prosperity:** rich towns grow suburbs along the roads past the gates and market stalls in the square; poor ones show
  empty plots and roofless houses. Villages gain or lose houses.
- **Buildings** (`Campaign/Fiefs.cs` `BuildingDefs`): walls level 1-3 raise the circuit (palisade, stone, towers with
  caps); market (stalls, a market hall); yard (a tiltyard and pells); barracks (a long range); granary (a tithe barn);
  stables (stable range and paddock); chapel (chapel); watchtower (a tall tower beyond the wall); kennel (kennels and a run).
- Test: two states that differ build different meshes; the same state builds the same mesh (checksum).

### SM5: Siege and damage
- **Battered** (`Settlement.Battered`): breaches in the curtain with rubble at the foot, broken merlons, fallen tower caps.
- **Besieged:** a siege camp round the place (tents in the besieger's colours, engines from `Party.SiegeWorks`), smoke and
  fires (the map's chimney smoke).
- **Raided** villages: burnt roofs and smoke for some days after a raid (needs a saved raid time on the village).

### SM6: The siege battlefield
- The assault field's wall, towers, gatehouse and the houses behind it in the defender's style (`BattleOptions.Style`, set
  in `Campaign.PrepareAssault`, carried to `Terrain`). Castles get a castle curtain, towns a town wall; a palisade people
  fight from timber. Breaches (A2) keep working.
- Village fields (`TerrainLayouts.PlaceVillage`) build houses in the province's style.
- Shots: `campaign-siege*.png` and the battle siege shots per style.

### SM7: Check and polish
- Every style by day, night and winter (snow on roofs), at overview, working and close zoom.
- `--world-bench`: places within budget (target: no more vertices than today's settlements plus 50%); frame time at the
  widest zoom.
- Docs: CLAUDE.md file guide, docs/DESIGN.md 6, README if the player sees anything new, the landing page shots.

## Order and size
SM1 is the biggest: the kit, the plumbing and the castles (most of the 422 places are castles and villages). SM2 and SM3
reuse the kit. SM4 and SM5 need the state plumbing from SM1. SM6 reuses the kit in battle. Each phase ends with its shots
looked at and `tools/check.sh` green.
