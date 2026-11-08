# A bigger world: more land, seven realms

Plan and progress: each phase's "As built" note says what shipped. Phases W0-W6 can each ship on their own, and `tools/check.sh` must pass after each.

## 1. Where we are

- **Size.** Provinces live within +-392 units (`WorldMap.Extent`), about 590 km across, ~60 provinces, ~60 towns and castles,
  ~150 villages, 2 rivers, a mountain rim. Crossing it on foot takes about 5-6 days.
- **Three realms**, placed by fixed rules in `WorldMap.Generate`: the north 30% is the Northmarch, the south is cut in half by X
  (Azure Crown west, Crimson Compact east). Capitals are "the outermost town". Every province is one of four kinds
  (Meadow, Farmland, Forest, Hills), picked at random, so the land looks the same everywhere.
- **"Three" is written into the code** in a few places:
  - `Campaign.New` sets up the wars by hand (0-1 at war, 2 on truces).
  - `Factions.Validate` asks for at least three realms. `Troops.Validate` lists the cultures by name.
    `Town.Flavour` has a switch on culture.
  - `Style.Standard()` has five palettes (0 azure, 1 crimson, 2 you, 3 brigands, 4 northmarch). Founded realms borrow palette 2.
  - Tests loop `f < 3` or name realm 0/1/2.
- **Realm ids**: realms founded in play take ids after the last `factions.json` entry. Adding realms to the file would shift
  those ids in old saves (see 6).
- **Scaling traps** that a bigger map hits at once:
  - `LordsWanted = LordsPerFaction * ProvinceScale` counts lords per realm from the size of the *whole* map. Today that is
    18 a realm. At 120 provinces with 7 realms it would be 37 a realm, about 260 lords.
  - `ProvinceAt` and `RoadDist` scan every province and road. They run for each party each step, and for each ground vertex
    when the terrain is built.
  - The sprite atlas is 1024x1024 and is painted once per palette, so each new palette adds a full set of parts.

## 2. Goals

1. **A world about 2.5x the land area**: half-side ~392 -> ~620 (about 930 km across), **~120 provinces**, crossing it in
   about 8-9 days on foot. Riding should still find a fight within a day, so "nearby" keeps scaling with `Spread`, as it does now.
2. **Seven realms**, each with its own country, culture, troops, heraldry and play style. None is a copy of another.
3. **Geography that matters**: climates (cold north, dry south, open steppe east), inner mountain ranges with passes,
   more rivers, lakes and marsh. The map should have places worth fighting over (passes, fords, river towns).
4. **Realms come from data**: home region, culture, starting wars and alliances all live in `factions.json`, with no
   counts in the code. An eighth realm should need data and art, not code.
5. Performance holds: a campaign tick and the map's frame time no worse than today on the bigger world (measured, see W0).

## 3. The seven realms (proposal)

The first three keep their troops and lands. The four new ones fill the new ground.

| Realm | Home | Country | Culture: how they fight | Their style on the map |
|---|---|---|---|---|
| **Azure Crown** | west | farmland, meadow | heavy foot, crossbows, lances (as now) | the steady kingdom: many castles, strong garrisons |
| **Crimson Compact** | east-centre | meadow, hills | greatswords, halberds, longbows, heavy horse (as now) | aggressive: high `LordAggression`, wars often |
| **Northmarch** | north | forest, cold hills | axes, round shields, glaives, light horse (as now) | raiders: hit villages, few sieges |
| **Sunward Emirates** (new, `southern`) | south | dry hills, badlands, oases | spears and javelins, curved swords, camel and light horse, mamluk heavy horse | rich towns, short of men: hires mercenaries, pays well for contracts |
| **Khaganate of the Grass** (new, `steppe`) | far east | steppe | horse archers first, lancers, little foot (takes over the Northmarch's horse-archer line, which then becomes northern light horse) | fast hosts that strike and ride off; weak at sieges |
| **Saltmarsh League** (new, `league`) | south-west | marsh, river delta | pikes, pavise crossbows, hired swords | merchant cities: many towns, few castles, buys peace and pays tribute rather than fighting |
| **Order of the Ashen Sun** (new, `order`) | the central highlands | mountain passes | knight-brothers, sergeants-at-arms, warrior priests | small and elite. Fights the undead and goblins first and lords second. Its lords are brothers with no heirs, so they never defect |

There are no new races. These are human realms, in the same fantasy-medieval tone as the existing three.

## 4. Phases

### W0: make it scale first (no visible change)
- **A spatial index on the map.** A bucket grid (~32 units a cell) over provinces, roads, rivers and settlements.
  `ProvinceAt`, `RoadDist` and `RiverDist` then look only at nearby cells, with the same results as the full scan
  (a test compares the two on 10k random points).
- **Lords per realm by the realm's land**: `LordsPerFaction * (realm's provinces / (LegacyProvinces / 3))`, so the world
  total follows the map and not the number of realms. At today's map this must give today's counts (`CampaignRun` pressure line unchanged).
- **`--campaign-bench N`**: a headless campaign for N days that prints ms per `Tick` by stage (hourly AI, moves,
  encounters). This is the baseline for every later phase. If `Encounters` checks every party against every other, give it the grid too.
- **Atlas check**: dump the atlas (`IRONFIELD_DUMP_ATLAS`) and measure how full it is. If 9 palettes do not fit, either grow it
  to 2048x2048 or paint only the dyed parts (cloth, shields, banners) per palette and share the rest.

**As built (W0).** `Campaign/MapIndex.cs`: a 16-unit grid whose cells keep every province, road and river bend that could
answer for a point inside them, so the answers are bit-for-bit the full scans' (`SelfTest.MapIndexMatches`, 60k points;
sites placed on an old seed do not move). `--campaign-bench 30` on seed 11 (60 provinces, 163 parties): 80 -> 45 ms a game
day, movement 52 -> 18 ms. `LordsWanted` now divides by the number of data realms over three (same counts today).
The atlas is 30% full with 5 palettes (the Sprites test prints it), so 9 palettes fit as it is. Encounters already had a grid.

### W1: realms from data
- `factions.json` per realm: `home` (a point on a unit square, e.g. `[0.5, 0.85]` = south centre), `weight` (how much
  land it gets), `country` (the province kinds it favours), `capital` rule. A top-level `relations` list gives the
  starting wars, truces and alliances by realm name.
- `WorldMap.Generate`: grow realms outward from their home points. Use a weighted flood fill over the province neighbour
  graph, so borders follow rivers and ridges, not straight lines. The capital is the town nearest the home point.
- `Campaign.New` reads the wars from `relations`. The hard-coded 0-1 war goes.
- Remove the counts and culture lists in code: `Troops.Validate` takes the cultures from `factions.json`, `Town.Flavour`
  gets a `flavour` line per culture in data, and the tests loop over `Factions.All.Count`.
- Old maps keep generating the old way (see 6). Still three realms at the end of W1.

**As built (W1).** `factions.json`: realms have `home` and `reach`, cultures a `flavour` line, and a top-level `relations` list
opens the wars and truces (`Campaign.New`). `WorldMap.GrowRealms` (layout 1) grows realms from their homes over the province
links, cheapest claim first, so each realm is one piece; the capital is the realm's province nearest its home. Saves record
`Layout` (missing = 0, the old bands, unchanged) and `DataRealms` (missing = 3); `Factions.Get` knows founded realms by
`FactionState.Founded`, so old founded realms keep their look when the data grows. `Troops.Validate` reads the cultures from
the data. Test: `RealmsFromData` (sizes on seeds 11-13: 19/24/17, 21/19/22, 18/24/19). The "Quests" test helper now ticks
whole game hours (a meeting cut its ticks short on the new map).

### W2: a bigger, varied land
- `Extent` 392 -> ~620. `ProvinceSpacing` stays, so the `(extent/196)^0.3` spread gives about 120 provinces.
- **Climate**: province kind is chosen from position plus noise, no longer at random everywhere. Cold north, dry south,
  steppe east, wet south-west, highlands in the middle.
- **New province kinds**, each with a map speed, a tint, foliage and battle `TerrainParams`:
  - Marsh: slow; battles have pools and soft ground.
  - Steppe: fast; open, almost no trees.
  - Badlands: dry and rocky; gullies.
  - Highland: hills with snow in winter.
  - Mountains: cannot be crossed except through passes.
- **Inner mountain ranges** (2-3 ridges) with **passes** where roads cross. A pass makes a natural border and a battle site.
- **Rivers**: from 2 to about 5. One great river can only be crossed at bridges (the bridges become towns' wealth and targets).
  Add a few **lakes**, which are no-go areas the roads go around.
- Sites (`PlaceSites`) get kinds for the new country: oasis, steppe kurgan, marsh shrine, pass fort.
- Terrain build: the ground grows from ~50k to ~150k vertices. Check build time and frame time on Low/Medium/High,
  and cull trees by distance if needed (`World.cs`).

**As built (W2).** `WorldMap.Extent` 620 (about 112-119 provinces); `CampaignState.MapExtent` defaults to 392 for saves
without it, and `Layout` 2 turns on the rest (layouts 0-1 are unchanged). `WorldMap.Climate` picks province kinds by
region with noisy edges; new kinds Marsh (speed 0.6, always a stream on the battlefield), Steppe (1.1, open), Badlands
(0.85, tall knolls), each with `TerrainParams`, a tint, a tree count, site kinds and a title ("the X Fens/Steppe/Wastes");
`Campaign.Wild` counts fens and badlands as wild. `RaiseRidges`: 2-3 wandering crests, kept 9.5 x span from settlements
(they break where they cannot be), slopes 8 x span, passes wherever a road crosses (low, and the road's pace); stored as
`River` records in `WorldMap.Ridges` with their own `MapIndex`. Ridge slopes are rock-tinted with pines. Four rivers.
`WorldMap.PartyScale` follows land area past the 392 map (provinces spread out as the map grows, so parties counted by
province grew sparse: the stand-in captain fought 8-10 battles in 120 days instead of 15-19). Bench: 76 ms a game day
with ~300 parties. Lakes and route-finding through passes are not built (parties still walk straight, slowly over ridges).
Tests: `ClimatesAndRidges`; the siege test rests the whole night (more parties pass the camp). Each missing troll now has its own daily chance to wake (big bandit bands kill them more often on the crowded map).

### W3: four new realms (first pass)
- Add the four realms to `factions.json`, with names (`names.json` places and people per culture) and heraldry: four more
  palettes, plus new charges (`Emblem`: Cross, Pale, Fess, Quarterly) so seven realms tell apart even in colour-blind mode,
  where charge and colour both matter.
- **Troops at first**: each new culture gets 6-8 signature troops (its T1 root and the key line ends). The other line steps
  borrow from `common` or a named kin culture (a new `kin` field in a culture, which `Factions.Validate` allows).
  So the realms play differently from day one without 100 new troops.
- **Realm character as data**: per-realm overrides for `LordAggression`, garrison size, siege appetite, tribute
  willingness, and what it hunts first (the Order hunts monsters and undead). `FactionAI`, `LordThink` and
  `Diplomacy` read these values.

**As built (W3).** `factions.json` has seven realms and seven cultures. New cultures: `southern` (Sunward Emirates, south;
own spears and horse: javelins to Golden Spears, Desert Riders to the Sultan's Guard), `steppe` (Khaganate, east; own horse
archers and foot bows), `league` (Saltmarsh League, south-west; own pikes and pavise crossbows), `order` (Order of the Ashen
Sun, the middle, small: reach -25; own brothers on foot and knight-brothers). 39 new troops (`Data/troops.json`), made
from the troop each replaces in the borrowed line with a culture twist, and built only from existing sprite parts.
`CultureDef.Kin` + `Factions.Open` let a culture's lines and men use its kin's troops (`Companies.CanEnterLine`: a kin line's
elite tiers train in the men's own barracks too). Heraldry: palettes 5-8 (Sunward, Khaganate, Saltmarsh, Ashen Sun) in both
colour sets, new charges `Cross`, `Pale`, `Fess`, `Sun` (sprites and UI shields); the atlas is 51% full with 9 palettes.
70 places and 30 lord names per new culture. Opening relations: two border wars (Azure-Northmarch, Crimson-Khaganate; Azure and Crimson share no border on any seed), truces elsewhere. Layout 3 puts
the realms at their data homes (Azure west, Crimson south-east, Northmarch north); layouts 1-2 keep the three old homes in
code (`WorldMap.ThreeRealmHomes`), and `Generate` takes the save's `DataRealms`. Realm sizes on seed 11-13: 8 to 24
provinces. The stand-in captain (`Pilot`) now counts a den's or the night's edge (`Campaign.Edge`) before it picks a fight, as a player would. Test: `SevenRealms`. Not yet: the realms' own behaviour (aggression, tribute, monster hunting) is W5.

### W4: full troop trees and looks
- Fill each new culture's four lines to seven tiers with their own troops (~15-20 new troops a culture), with `look` parts:
  turbans and lamellar, steppe caps and deel coats, marsh-league pavises and brigandines, Order white surcoats with a sun charge.
- New parts are painted in `SpriteSheet.cs`, and mounts go in `troops.json` (camel, steppe pony).
- `SelfTest.TroopTree` and `Factions.Validate` already enforce that the trees join up.

### W5: seven realms that behave
- **Neighbour-aware wars**: a realm wants war mostly with realms it borders. `ProposeWar`/council motions weigh
  border length, the target's strength, and wars it already fights. This stops seven realms all fighting everyone.
- **Alliances (F7)** get real use: two realms both at war with a strong third realm tend to ally against it.
- **Snowball guard**: check with `--balance` and `--pacing` that over 2 years in-game no realm is gone before
  year 1 and no realm holds more than ~40% of the map, across 8 seeds. Tune with data, not special cases.
- **Choose your start**: the new-campaign screen lets you pick the realm you start in (its capital, its levies), or
  start as a free captain on the border.

### W6: screens and docs
- Realm panel (R), relations (a 7x7 table becomes a list of your realm's wars/truces/alliances plus a small matrix),
  map legend and flags, title-screen save card. Check every page with `IRONFIELD_LAB_SHOTS` and `IRONFIELD_CAMPAIGN_SHOTS`.
- A map overview toggle (M) that shows realm colours and borders, because a world 930 km across needs one.
- Update `README.md` (realms, cultures), `docs/CAMPAIGN.md` 3.1-3.2 "As built", `CLAUDE.md` file guide, `CODEMAP.md`.

## 5. Tests to add

- `WorldScale`: on 3 seeds, ~110-130 provinces. Each of the 7 realms has a capital town, at least 10 provinces, and
  connected land (no island provinces cut off from the realm).
- `MapIndexMatches`: the grid look-ups give the same answers as the full scan.
- `PassesConnect`: every settlement can reach every other by road. Mountains never cut the road graph.
- `RealmsFromData`: the starting wars match `relations`. Lord counts follow each realm's land.
- `SevenRealmsTwoYears` (long, `--balance`-style): no realm is gone in year 1, none holds over 40%.
- Existing tests that name realms 0/1/2 keep working (the first three stay in place).

## 6. Old saves

A save stores the seed and `MapExtent`, so the map can be regenerated. Two problems:
- **Founded realm ids**: an old save's founded realm 3 would now read as the Sunward Emirates. Fix: the save records how
  many data realms it was made with (`CampaignState.DataRealms`, default 3 when missing). `Factions.Get` treats ids past that
  number as founded.
- **Map layout**: old saves (`MapExtent` 392 or 196) keep the old three-realm generator, unchanged, behind a layout version.
  A new campaign gets the new world.

The option is keeping old saves playable on the old map (more code, kept forever) or bumping `CurrentVersion` so old saves
are refused (simple). Recommendation: keep them for W1-W2 using the two fixes above, and decide again before W3.

## 7. Risks

- **Performance** (more parties, bigger mesh): W0 measures first, and every phase reports `--campaign-bench` and `--bench`.
- **Atlas space** for 9 palettes: W0 decides grow vs share.
- **Pacing**: the career stages are tuned on a smaller world. `--pacing 8` after W2 and W5, then tune `Balance` values.
- **Map AI**: route-finding has to respect mountains. Parties today walk straight lines toward goals, so they need to go
  by road through passes. That is a road-graph path (A* over settlements), which also makes travel look right.
- **Art load** in W4 is the biggest single job. The W3 borrow-lines step means the realms are playable before it is done.

## 8. Open questions

1. Size: ~2.5x the land (half-side 620) as proposed, or bigger (2x across, ~4x the land, ~200 provinces)?
2. Seven realms as above, or a different count or set? Any of the four new ones to swap out?
3. Keep old saves playable, or allow a clean break?
4. Should the steppe realm take the Northmarch's horse-archer line (the Northmarch then gets northern light horse), or
   keep both?
