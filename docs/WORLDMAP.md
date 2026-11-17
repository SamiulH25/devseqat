# The designed world: plan

Status: **WM0-WM2 built** (2026-11-17); WM3-WM6 planned. New campaigns stay on layout 4 until WM4 (set `IRONFIELD_MAP_LAYOUT=5` to play the drawn world now). Brings the world drawn in `Data/world.json` (the Rift Continent, revision 11) into the
game as map layout 5, the map every new campaign plays on. Phases WM0-WM7; each ships on its own and `tools/check.sh`
must pass after each. When a phase ships, add its "As built" note at the end.

## Decisions (the owner, 2026-11-17)

- **The sea cannot be crossed** in this sprint. All the land is one piece (the south joins through the Order's isthmus),
  so nothing is cut off. Ports and the 27 sea lanes come later as ship travel (WM7, not in this sprint).
- **The Saltmarsh League** holds one province (Venaria, inside the Azure Crown) with 25 settlements crowded into it. The
  model puts a redraw forward as a note in the world file; the owner rules on it in the page before WM4 builds it.
- **Province names are invented** by the game's per-culture name lists, as settlements' are. The file's real-world names
  (Cairo, Kiev, Tuscany...) stay in the file as working labels only.
- **Subcultures recruit in full this sprint**: each subculture's tree is finished to tier VII and its settlements
  raise and train its men (WM5). The 17 cultures marked `waiting` in `Data/factions.json` lose the mark as they come in.

## What the file holds (revision 11)

| | Count | Note |
|---|---|---|
| Provinces | 162 | 80 land, 82 sea (`kind: "Sea"`, realm -1). Shapes: nearest centre after a noise warp (`provinceAt` in the page) |
| Settlements | 394 | 61 towns, 53 castles, 280 villages; each with realm, culture, subculture, 23 ports |
| Sites | 144 | the nine kinds the game has |
| Rivers, ridges, lakes | 5, 6, 12 | polylines with widths; lakes as circles |
| Roads | 117 | straight two-point links between settlements; many realms have none between them |
| Sea lanes | 27 | port to port (WM7) |
| Land kinds | 14 | the game's seven plus Coast, Taiga, Highlands, Tundra, Desert, Oasis, Sea |

The land is 43% of the 1240-wide square the current map fills. Realms by provinces: Khaganate 20, Emirates 18,
Northmarch 13, Azure 12, Crimson 12, Order 4, League 1.

## Phases

### WM0: One file, read by the game

- Move the file to `Data/world.json` (game data); `tools/world.py` and the page load and save it there. Fix
  `docs/world/README.md`, which still describes the old entry-and-thread book, to describe the map file (notes pinned on
  the map are the talk between the authors) and the rules for the model.
- `WorldDesign.cs` (headless): load and validate the file (the same faults `world.py --check` finds), and port `warp` and
  `provinceAt` exactly. A test checks the C# against points sampled from the page's own function.

### WM1: Layout 5 from the design

- `WorldMap.FromDesign`: provinces (land ones as `Province`, sea ones kept for `ProvinceAt` and drawing), settlements at
  their places with their province from `ProvinceAt`, rivers, ridges, lakes and sites as drawn, invented names. Roads:
  the drawn links routed over the land with the layout 4 router (`LayRoads`), plus links so every town can reach every
  other by road where the land joins.
- The same map for every seed: the seed still deals lords, weather and battles. Saves keep their layout; layout 4 and
  older generate exactly as before (`CampaignMap`, `MapIndexMatches` unchanged).
- Heights: the land rises from the shore; ridges as layout 4 draws them; Hills/Highlands get hills.

### WM2: The sea

- Sea ground drops below the water line with a shelf and a beach; the water shader draws it (as lakes are drawn).
- Nothing travels on the sea: route planning, `InLand`, AI movement and encounters keep to the land. A battle by the
  shore gets the coast field.

### WM3: The new lands

- Coast, Taiga, Highlands, Tundra, Desert, Oasis: map tint and ground materials, trees, `Province.Title`, battle field
  (`TerrainParams`), rain and season, site kinds, monster country (`Wild`), the tavern strip's country.

### WM4: Realms on the designed map

- Capitals, homes and lords from the drawn holdings (lords scale with a realm's towns and castles, not its provinces).
- The League redraw if the owner agrees it. Knight order chapters, wells, holdouts and nests placed on the new land.
- Guards: `--realms 8` (no realm snowballs), `--pacing 8` (career stages near their targets), `--campaign-bench` and
  `--world-bench` (no slower than layout 4).

### WM5: Subcultures recruit

- Each settlement's culture is its subculture; villages raise its recruits and its barracks train its men.
- Each of the 16 realm subcultures gets lines to tier VII (at least an infantry, archer and horse line at VII, as
  `Cultures` asks) built on its existing tier II-V chain; new looks painted and baked. `bandit` troops go to brigand
  bands. Each culture drops `waiting` as it passes `Cultures`; `docs/TROOPS.md` regenerated.
- Balance: `TierFight`, `--troop-balance`, `--balance 4`.

### WM6: Polish

- Campaign shots of the new map, judged (7/10 or better). The realm map (M) and minimap draw the coast. Docs, README
  and the landing page brought up to date.

### WM7 (later, not this sprint): ships

- Ports and sea lanes: embark, sail, land; encounters at sea.

## As built

- **WM0 (2026-11-17).** `Data/world.json` (moved from `docs/world/`; `tools/world.py` serves it to the page as
  `world.json`). `WorldDesign.cs` loads and validates it; `Warp` and `Hash2` copy JavaScript's number rules (`Math.imul`,
  a double sum, `>>>` as ToUint32 of the rounded double product), and `WorldDesignReads` checks twelve points against
  values from the page's own function run in node, to 1e-9. `tools/world.py` gained `save()`, which writes the page's
  layout, so a scripted edit diffs only what it changed. The League proposal is note 0 in the file (revision 12).
- **League redraw (2026-11-17, owner agreed note 0).** Lorraine and Peyrolles passed to the League; Venetia split into
  Venetia and Mestre; eleven settlements moved out of the crowded centre. League: 4 provinces, 37 settlements.
- **WM1 (2026-11-17).** `Campaign/WorldDesigned.cs`: `WorldMap.Generate` hands layout 5 to `FromDesign`. Deviations and
  gotchas:
  - **Not yet the default.** `CurrentLayout` stays 4 so the balance and campaign tests keep measuring the map they were
    tuned on; WM4 switches it once the realms are tuned. `Campaign.New(..., layout:)` or `IRONFIELD_MAP_LAYOUT=5`
    (campaign shots too) builds the drawn world.
  - The map's own noise uses `DesignSeed` (1105), not the campaign's seed: every campaign gets the same ground, names
    and ponds. The campaign's seed still deals everything else.
  - Provinces are the 81 land provinces only. `ProvinceAt` reads a 2-unit grid of the design's warped rule (built once
    a process, cached statically); a sea cell answers with the nearest land province, and `InSea` / `Landness` tell the
    sea apart. Neighbours are provinces whose land meets for three or more cells.
  - A province can hold several towns and castles: `SeatOf` a village is now the nearest seat in its province (the same
    answer as before on layouts 0-4, which have one).
  - Roads: the 117 drawn links, a village with none goes to the nearest seat over land, then the shortest land links
    between seats until every seat is joined; a seat whose every line crosses a bay (Crowle) is joined through villages.
    LayRoads routes them all; the router bars ground with `Landness` under 0.45.
  - Heights: the land is lifted `LandRise` (4.5) and sinks to `SeaDepth` (7) below across the shore; no mountain rim.
    (WM2 reshaped the shore and added the water.)
  - Rivers and ridges follow the drawn points with a meander between them; Hills and Highlands provinces get hills;
    the drawn lakes plus village ponds (no random big lakes).
  - Creating a campaign: about 1.1 s the first time in a process, 0.55 s after (road routing).
  - Seen in shots, left for later phases: the sea (WM2); the realm map (M) and minimap still paint provinces as plain
    nearest-centre cells over the whole square (WM6).
  - Test `DesignedMap`.
- **WM2 (2026-11-17).** The sea. Deviations and gotchas:
  - **Heights across the shore** (`DesignedHeight`, by blurred landness): sea floor (`-SeaDepth`), a shelf up to the
    shallows, a wide flat beach from `BeachLow` (-0.5) to `BeachHigh` (0.7) around the drawn coast, then a bank up to the
    land (lifted `LandRise`). The sea's surface is `SeaLevel` (0) and travels to the renderer as `Terrain.SeaLevel`.
  - **Drawing** (`World.Sea`, World.Water.cs): a still sheet over every 8 m cell that dips under the water, tiled and
    culled, shaded as lake water (x = 5 + nearness to the shore: paler shallows); past the map's edge open sea to the
    horizon over a flat sea-floor skirt (the mountain skirt is for maps without a sea). Sand on the beach in `Tint` by
    landness, so the band keeps its width whatever the slope (sand pale enough that `world.glsl` keeps its colour).
    Shader: still water got broad painted tone steps and a broken surf line at the shore (lakes too); the sun glint fades
    out past 50-160 m, where far ripples aliased into white speckle (rivers and lakes too).
  - **Nothing on the sea**: `InLand` puts a point at sea on the nearest land (each sea cell keeps its nearest land cell,
    `cellShore`); every party is put ashore each tick (spawns scatter); `PlanRoute` bars the sea; parties other than the
    marching player whose straight line crosses water follow a planned way round (`Campaign.SeaRoute`, not saved:
    planned again after a load).
  - **Left for WM3**: a battle by the shore still gets its province's field; the coast battlefield comes with the Coast
    land kind. **Left for WM6**: the minimap and realm map (M) paint no sea.
  - Judged with Haiku 5.5 (the T3 judge was not reachable from the session): 6 in four rounds; the owner looked at the shots and accepted the look (polish left for WM6).
- **The map scaled up a third (2026-11-17, the owner: towns stood too close).** `WorldMap.DesignScale` (1.33) spreads
  every drawn place, province centre, lake, ridge and road out from the middle; the province rule is read at
  `p / DesignScale`, so borders scale with it. Rivers keep their widths. The map is 825 wide each side of the middle
  instead of 620; parties scale with the land (`PartyScale`), and marches between towns take a third longer, which WM4's
  `--pacing` run will weigh. Campaign creation: about 1.5 s first, 0.9 s after.
