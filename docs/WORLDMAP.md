# The designed world: plan

Status: **WM0-WM6 built** (2026-11-18); WM7 (ships) later. This sprint is done; move it to `docs/archive/` when WM7 gets its own plan. New campaigns play the drawn world (layout 5); `IRONFIELD_MAP_LAYOUT=4` makes the last seeded map. Brings the world drawn in `Data/world.json` (the Rift Continent, revision 11) into the
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
- **WM3 (2026-11-17).** The new lands. One table, `Lands.All` (`Campaign/Lands.cs`), holds every kind of country: its
  title, map tint and `Wash` (a pull toward a colour of its own: sand, frost; a multiply cannot make them), woods and
  whether they are pines, map speed, rain, monster country (`Wild`), site kinds, the battlefield (`TerrainParams`) and a
  climate (`cold`, `hot`). `Province.Title`, `WorldMap.Tint`/`KindTint`/`SpeedFactor`/`BattleGround`/`SiteKindsFor`,
  the map's woods (`BuildTerrain`, `GrowStands`), `Campaign.RainDaily` and `Monsters.Wild` read it. The first seven rows
  keep the values the code had, so layouts 0-4 generate and play as before (`CampaignMap`, `MapIndexMatches` unchanged).
  Deviations and gotchas:
  - **Battlefields**: presets `Coast`, `Taiga`, `Highlands`, `Tundra`, `Desert`, `Oasis` (Terrain.cs; `IRONFIELD_GROUND`
    knows them). `TerrainParams.Look` (`GroundLook`: Moss, Frost, Sand, Dry) colours the open ground (`World.GroundColor`)
    and sets its ground layer (`Terrain.LookGround`: dunes of Sand in the desert and round an oasis's edge, Stony patches
    in the tundra and on highland slopes); `Conifer` makes every tree a pine. The desert has no flank woods.
  - **The shore** is a new `FieldLayout.Shore`: the sea along one flank (`Terrain.ShoreSide`), from just past the
    deployment ground (`50 * Scale + 34..48` m), a sand beach, the rim falling away into it. `Terrain.SeaLevel` (-2.5 on a
    battlefield) makes `WaterDepth` read the sea and `World.Sea` draw it; the open sea past the field's edge is drawn
    only off an edge that is under water, so the other three sides keep their rim. Inland, low ground is lifted clear of
    the sea's surface. `WorldMap.BattleGround` gives a Shore field wherever open water lies within 8 units of the fight
    (`NearSea`), whatever the province. A Coast province away from the water fights on open ground or a village.
  - **Weather**: `Weather.For` takes the country's rain and climate (`Campaign.WeatherFor` reads the player's province):
    the sands never snow, the cold lands snow outside summer. This also makes battles in the fens rainier and on the steppe
    drier on every layout (the same dice, other thresholds).
  - **Map**: winter does not whiten the sands. A land's wash amount is also how much of its own colour the map shader keeps
    through its grassland look (`WorldMap.OwnColour` -> `Terrain.Own`, sent as terrain UV x = 1000 + the amount, which plain
    `p / 4` UVs never reach; world.glsl). The shader used to keep only very pale ground's colour, so the sands came out olive
    and the frost as fog-white blotches; the washes are natural colours now: sand (0.86, 0.74, 0.52), the tundra lichen and
    grey stone (0.66, 0.67, 0.58), heath brown in the highlands.
  - **Fields and woods on the drawn map** (2026-11-18): sands and barrens keep 40 % of a village's plots, bare (fallow and
    ploughed: `LandDef.Fields`, `Barren`); taiga 60 %; no orchards in the sands or the cold lands. Woods kept 9 x span (38
    units at span 4.2) clear of every place, which left the crowded drawn map nearly bare; there it is 9 x sqrt(span), stands
    try twice as often (the drawn provinces are ragged), and ridge pines shrink by a town as stand trees do. Layouts 0-4 are
    untouched (`--world-bench` checksum unchanged).
  - **Monsters**: wolves and spider nests also in the taiga; bears in the taiga and highlands; wyverns in the highlands and
    desert. **Tavern strip**: taiga is drawn as forest, highlands as moors under a range, tundra as flat barrens with snow
    outside summer, desert and oasis as dunes (hot), coast as the sea to the horizon. **Names**: battles take Strand,
    Sands, Barrow, Wells. **Sound**: the coast has the surf bed, the taiga the forest bed.
  - **Shots**: campaign shots on the drawn map add `campaign-lands-Desert/Tundra/Taiga/Highlands.png` (a province of each,
    at midday); a battle shots run on a Shore field aims `landscape.png` at the sea. Test `NewLands`.
- **The map scaled up a third (2026-11-17, the owner: towns stood too close).** `WorldMap.DesignScale` (1.33) spreads
  every drawn place, province centre, lake, ridge and road out from the middle; the province rule is read at
  `p / DesignScale`, so borders scale with it. Rivers keep their widths. The map is 825 wide each side of the middle
  instead of 620; parties scale with the land (`PartyScale`), and marches between towns take a third longer, which WM4's
  `--pacing` run will weigh. Campaign creation: about 1.5 s first, 0.9 s after.
- **WM4 (2026-11-18).** Realms on the drawn map, and the drawn map for every new campaign. Deviations and gotchas:
  - **The default.** `WorldMap.CurrentLayout` is 5; `GeneratedLayout` (4) is what `Generate(seed)` makes when no layout is
    named, so the generator's tests still test the seeded maps. Saves keep their layout.
  - **Lords by holdings**: `Campaign.LordsWantedOf(f)` shares the same world total by the towns and castles each realm holds
    now (two at least): the Order with six seats raises 6, the Khaganate with 27 raises 25. Other layouts: `LordsWanted`.
  - **Capitals and homes** come from the drawing as WM1 made it (no change needed); knight-order chapters, wells, holdouts,
    nests and packs land on the new land by the rules they had (checked in a 60-day run).
  - **A province with several seats** (`CampaignSiege` capture): a taken seat brings the villages it is the seat of
    (`SeatOf`), and the province changes hands once no seat in it holds out for the old realm. It used to hand over every
    village and the province at the first seat; with one seat a province (every seeded map) nothing changes.
  - **Speed** (`--campaign-bench 30`: 118.6 ms a day, layout 4 118.5). The drawn map ran 640 ms a day: (1) AI parties going
    round the sea planned with the player's planner, which priced roads and woods on a 4-unit grid and cleared two
    170 000-cell arrays a plan; they use `WorldMap.PlanAroundSea` now (a 6-unit land grid, arrays kept, straight legs over
    land). (2) The road look-up grid was first built on the first campaign tick (0.8 s), since the drawn sites are placed
    without asking it; `FromDesign` builds it. Also `MapIndex.NearRoad` (exact: a cell's lower bound, then the first road in
    reach) for `SpeedFactor`.
  - **Saved ways round the sea**: `Party.SeaWay`/`SeaGoal` replace WM2's unsaved table; a way planned again after a load
    differed, and a loaded game drifted (`SaveMidRun`).
  - **Guards**: `--realms 8`: largest share 32 %, 0 flags (layout 4: 28 %, 0); the Khaganate starts at a quarter of the
    provinces as drawn, the Order keeps 1-6 % and survives every run. `--campaign-bench` as above. `--pacing 8` on both maps
    with the same build: the stand-in captain runs ahead of the targets on each (Marshal by day 106-162 on both; vassal
    from day 31), so the drawn map paces as layout 4 did; on it one run of eight never got past Sworn Sword and one was slow
    (Lord on day 165). The fast pacing is older than this sprint and is left for a balance pass.
  - **Tests moved to the drawn map** where they had read the old map's land names (`Wolves`: forest or taiga;
    `RainMakesMud`: the rainy lands against the dry by `Lands.Rain`, the drawn map has no fens), the one-seat province
    (`CampaignWar`), or a quiet first town or largest host (`CoreAndTrials` rests in a town of your realm, `OrderPolitics`
    makes its host the greatest, `RealmTempers` leaves every realm a seat). `MagesInTheWorld` keeps the seeded map: on the
    drawn map the two arcana-3 realms (the Compact, the Order) lose their early wars and field no lord of tier IV in 22
    weeks. **Left for balance**: those two realms' lords stay at tiers I-III through the first half year.
- **WM5 (2026-11-18).** Subcultures recruit. Deviations and gotchas:
  - **Places are their people**: a drawn settlement's culture is its subculture (`FromDesign`), once that culture is not
    `waiting`; its villages raise the same root (levies or clansmen) and its men climb their own lines. Twelve League
    places are drawn as Bayfolk (`azure_coast`), Azure people under League rule; kept as drawn.
  - **66 new troops** (`Data/troops.json`, made by a script from the parent realm's lines): each subculture's own chain runs
    on to tier VII (tiers VI-VII; the Bayfolk also V), and it gets a tier VII of its own in each other role (infantry,
    archers, horse), reached from its parent realm's tier VI of that role, so its men climb there through kin troops.
    Stats: a chain step takes the stronger of its last troop (raised 8 %) and the parent's troop at that tier; a role VII has
    its parent realm's tier VII stats. Names from each people's own words (King's Wardens, Sea-King's Chosen, Pine Wardens,
    Gate Wardens...). **Looks use existing painted parts** (the parent realm's armour at that tier, the people's own helm
    for the role VIIs, no plume on a soft hat or hood); no new parts were painted. All 16 pass `Cultures` and drop
    `waiting`; `bandit` keeps it. Judged with Haiku from the troop sheets (`IRONFIELD_TROOPS`): 5.5/10. Infantry VIIs read as
    a step up; **left for later**: peoples of one realm are hard to tell apart (horse VIIs above all, and steppe_horde from
    steppe_salt, southern_dune from southern_rift), which needs parts painted per people (helms, horse coats and harness).
  - **Parts painted per people** (`SpriteSheet.Peoples.cs`): 15 helms (frogmouth, galea, ibex, dragon, bear and white-bear
    hoods, spiked turban, litham, corsair, tug, faceplate, lion, chapel, sun greathelm, winged sallet) and a `_gilt` tier VII
    copy of 13 of them (`HelmBase` strips it), six bodies (muscle cuirass, kaftan, mirror mail, jupon, dark lamellar,
    burnous) and eight bardings (half, mail, fur, lamellar, parti, cross, tasselled, chequy; `SpriteSheet.Bardings`,
    drawn through `PalSprites.Barding`). Each people climbs its own helm V -> VI -> gilt VII; 17 new mounts copy an
    existing mount's stats and change only the barding, and no troop gained or lost a shield, so the sim is unchanged.
    Judged with Sonnet from the troop sheets: 6.5/10. Left: tier VII foot still close to VI, crossbows read as a blob.
  - **A people's realm** (`Factions.PeopleOf`): a culture a realm holds is its own; a subculture belongs to the first of its
    kin a realm holds (a realm culture's kin are only lines it borrows: the League's include the Azure). Used by
    `HasBarracks` (a barracks in a town of a culture's peoples trains that culture's lines: on the drawn map every town is
    a subculture's, so a realm's own elite lines had no barracks) and `KinRealm` (a subculture's restless seat goes over
    to its parent realm; before, no drawn seat could).
  - **Found on the way**: `WandererAt` offered dead wanderers (hired, never came); lords' refits now leave 150 in the
    realm's chest (`RefitReserve`: a beaten small realm's refits halved its chest toward nothing, and with an empty chest it
    raises no lord; the Order of the Ashen Sun ran down to 50).
  - **Left out: brigand bands of the outlaws' own troops.** Tried (the `bandit` culture's four lines, same dice): the seeker
    in `--arcana` kindled its aura by day 90 in 2 of 5 seeds instead of 4, from fewer fights; a balance call for later.
    Bands still field realm lines.
  - Tests moved to subcultures: `Cultures` (a village is its realm's people or its drawn people; lords' own troops count
    their peoples'), `UpgradeTreeVisibility`, `FiefLife` (a Crimson-people castle), `SiegeCamp` (the wall mends each
    unbesieged day: another lord's siege in between left the end state battered).
- **WM6 (2026-11-18).** The minimap paints the sea below `Terrain.SeaLevel` (teal shallows to dark deep water, a pale surf
  line at the shore; `World.BuildMinimap`). The realm map (M) leaves texels with `Landness` under 0.5 unpainted, so the
  minimap's sea shows through, and draws a dark coastline (`MapScreen.Atlas.cs`). Night on the map was lit like a late
  afternoon: its `nightFloor` (0.7) capped the light's colour as well as its brightness. Now `Sky.Moonlit` turns the
  sun and sky to night colour by the true darkness (up to 80%), at the floor's brightness; the map's floor is 0.6; and
  the night grade (`Sky.Grade`, post tint) is a strong cool (0.62, 0.8, 1.2) that takes the grass's green out. Battles
  at night (floor 0.42) get the same cooler moonlight. README, CAMPAIGN.md and the landing page describe the drawn world (81 provinces, about
  1,200 km, ringed by sea). Judged with Haiku 5.5 (the T3 judge was not reachable): 6, 6.5, 6.5, then 7/10 once night
  read as night. Wrong claims checked against the pixels: the close minimap had no sea, the Tundra shot was a battle,
  then that it showed the desert (it is Skane; the Sunward tooltip in it is a stale hover from the Desert shot before).
- **Map polish 1 (2026-11-18).** Night drains colour as well as cooling it (`Sky.NightHue` lowers the post saturation in
  `Shading.PostToScreen`; the night grade eased to (0.74, 0.86, 1.08)), so brown and green no longer turn mauve. Roads
  carry grit (three octaves of noise on `trodden` in world.glsl): they read as a smooth glossy tube without it. `Stage`
  marks the view stale (`viewReady`), so a shot no longer hovers what lay under the mouse through the previous shot's
  camera (the Sunward card in the Tundra shot). Judged 7/10 (Haiku). Still open: the roads' three lengthwise colour bands
  (verge, rut, crown) read as corrugation; the Tundra map tint (`Lands`, 0.66, 0.67, 0.58) passes for sand in spring;
  fields at 04:00 go grey-purple; Khaganate teal is close to Azure blue on the realm map; the realm map's sea has no
  shallows. Checked and fine: winter field contrast, label stacking (placement keeps labels apart; two touch at most).
- **Farmland on the drawn map (2026-11-18, the owner's notes).** Pens and fields overlapped (plots were only kept off
  other villages, never off each other's plots) and ran onto the beach: on the drawn map a plot now keeps 2.5 m from
  every other plot (`FieldPlot.Overlaps`, a separating-axis test) and off the shore (`Landness` 0.97); test
  `FieldsApart`. Hedges were single random sides with gaps, standing outside a crop that faded before its edge, so
  they read as dark slabs scattered on the grass: a plot is now hedged all round or not at all, hedges stand on the
  edge with a flat, sunlit top of leaves and rarely break, and a hedged crop runs up to its hedge. Plots sit 0.26 m
  over the ground (was 0.16) and the beasts that live on them stand on that. Orchards were a strict 3.3 m grid of
  touching crowns (the owner: "trees should not be like a grid"): now loose rows, 4.4 m apart, jittered, a fifth gone,
  smaller crowns. Penned sheep fleeing a party ran into the fence and were shoved to the middle every frame (the
  jitter): a fleeing goal is kept inside the pen, and a beast at the fence stops and picks a new spot. Layout 4 keeps
  its fields (the plot rules are behind `Designed`; the hedge gap and lift apply to both). Judged 7/10 (Haiku); its
  claims of a pen overlapping a field and of hedges on no plot were wrong. Still open: an unhedged plot's edge fades
  over many metres, and green crops have no texture beside the furrowed ones. In shots, a pasture's herd sometimes has
  not spawned yet (the shot can come before the herd search runs).
