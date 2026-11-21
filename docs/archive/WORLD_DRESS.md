# The dressed map (plan WD1-WD7)

Status: **WD1-WD4 built** (2026-11-19). WD5-WD7 planned. Follows `docs/archive/WORLD_LIFE.md` (roads, water, fields, beasts,
folk) and `docs/archive/WORLDMAP.md` (the drawn world). Move to `docs/archive/` when the last phase ships.

## The problem (measured, 2026-11-19)

The drawn map (layout 5) is about 1.17 million m2 of land and held **1,073 trees and bushes**: one plant to 1,100 m2.
Everything else that grew was a village's field plots. Between a wood's stand and the next village the ground was bare
shaded grass, sand or frost, so the map read as a painted board with models set on it. Three causes:

1. **Nothing small.** The only plants were full trees and big bushes, in stands. No shrubs, grass, stones or flowers:
   nothing between "tree" and "bare ground", so nothing to give the ground scale.
2. **Lands differ only by tint.** Desert, tundra, steppe and coast (38 of the 81 provinces) grow almost no trees, so they
   were empty; an oasis had no palms, a highland no heather.
3. **Woods are specks.** Stands keep 18 m off each of the 394 places, which are some 55 m apart, so a "forest" province
   was a few stands in a lawn.

Life (40 herds, 5 flocks near the camera) and weather are thin too, but they sit on top of the ground: they come after.

## Rules

- **Drawing only**, as `docs/archive/WORLD_LIFE.md` has it: nothing here is read by `Campaign` or saved; own dice, placed after
  everything else in `BuildTerrain`, so no other part of the map moves.
- **The drawn map only** (`WorldMap.Designed`). Layouts 0-4 keep their maps as they were.
- **Budget.** The map's vertex buffers were 119 MB (3.1 M vertices). The whole plan may add 60 MB; small cover is built
  from 20-face lumps and single blades, never the 80-face `Blob`. Check with `--world-bench` (it lists the plants by
  kind) and the shots' `render.txt`.
- **Shots.** `IRONFIELD_MAP_ONLY=1` with `IRONFIELD_CAMPAIGN_SHOTS` saves only the map (18 s): the overview, the land's
  life, and `campaign-lands-<Kind>.png` for eleven kinds of country. Judge each phase from them.

## Phases

### WD1 Small cover (built)
A try in every 7 m cell of open land that nothing stands in: shrubs, tussocks of long grass, stones, lone trees. Cover
gathers in drifts (a noise field) and leaves lawns between. It comes up to a place's houses (11 m of a village, 14 m of
a town or castle); the stands' 18 m ring stays for trees only.

### WD2 Each land its own plants (built)
One table (`WorldMap.Covers`): per kind of country, how readily it grows woods, how full its open ground is, how
parched its plants are, and what stands there by weight. New pieces: thorn scrub and straw tussocks where it is dry,
heather (purple in summer and autumn) in the highlands, reeds in the fens and oases, dead trees, palms, drifts of wild
flowers in spring and summer (one colour to a drift), warm sandstone in the dry lands instead of grey rock.

### WD3 Woods that are woods (built)
Where a second noise field runs high the cell grows a tree instead: a closed canopy at the heart (crowns meet, even by
a town), smaller trees and shrubs at the rim, glades between. Roads and streams keep a ride cut through (4.5 m, 4 m),
so they stay in sight. Forest and taiga are wood with glades; meadow, hills and coast get copses; steppe a rare one;
desert, tundra and badlands none.

### WD4 The water's edge and the roadside (built)
- Shore: stones and drift wood on the beach, marram tussocks on the bank behind it, reeds round lake shores, rocks
  standing in the surf. (Cover stops at `Landness` 0.95 today, so the beach is bare.)
- Ports (23 drawn): a jetty and moored boats; fishing boats off the coast (drawing only, as river boats are).
- Roads: trees and hedges along a road near a town, milestones, a wayside shrine or well at crossroads (deferred from WL1).

### WD5 Life in every land
- Wild herds by land, on `MapLife`'s herd code: horses on the steppe, goats in the highlands, camels in the sands,
  reindeer on the tundra, boar in the forest. Homes from the cover's drifts, as deer take wood edges.
- Birds: gulls on the coast, waterfowl on lakes, storks on village roofs. More flocks at wide zoom.
- Night: campfires by parties at rest, fireflies in the meadows in summer.

### WD6 Air and weather over the land
- Cloud shadows drifting over the map (the shader has `CloudAt`; give it broad slow cells on the map).
- Wind waves running over grass and crops; mist in low ground at dawn; dust on the steppe, blown snow in winter.

### WD7 Judge and tune
Shots of every land in each season, judged 8/10 or better; frame time at the widest zoom measured on High and Medium;
thin the cover by `Quality` if Medium needs it.

## As built

- **WD1-WD3 (2026-11-19).** `Campaign/WorldDress.cs` (`WorldMap.Dress`, called at the end of `BuildTerrain` for the
  drawn map; dice `Seed * 17 + 5`) and `World.Dress.cs` (the pieces: `Shrub`, `Tussock`, `ReedClump`, `Heather`,
  `DeadTree`, `Palm`, `Flowers`; `Mesh.Lump` is the 20-face blob). `FoliageKind` gained the seven kinds and `Foliage`
  a `Dry` field (0-1: green toward straw; rocks toward sandstone). `BuildTerrain`'s `Clear` is now `Open` (roads,
  water, fields, sites, shore) plus the ring round places, so cover can use `Open` alone.
  - Numbers (`--world-bench`, seed 11): 1,611 -> 7,163 items; trees 1,073 -> 2,141, small cover 0 -> 4,402; buffers
    119 -> 154 MB; map terrain 229 -> 316 ms, world meshes 551 -> 604 ms. The overview shot draws 162 k world triangles
    (was 128 k), 183 k in the shadow pass (was 142 k).
  - The land is crowded: of 22,300 cells only 55 % are open (roads, fields, places and the stands take the rest), so a
    forest province cannot be one mass; it is woods with rides and glades. In a wood's heart only every other cell
    grows a tree, at 0.8 size or more: the crowns still meet, at half the triangles of small trees in every cell.
  - A tussock is a low mound with blades: blades alone are a pixel wide from the overview and shimmer without MSAA.
  - Leafy pieces carry leaf tag 1, so `world.glsl`'s map canopy shading (clumps, dark undersides, snow on top) takes
    them with no shader change.
  - Shots: seven more `campaign-lands-*` (Steppe, Coast, Hills, Meadow, Oasis, Forest, Badlands); `IRONFIELD_MAP_ONLY`.
  - Test `MapDressed` (Tests/MapTests.cs): cover on every land, none in fields, on roads, in water or among houses,
    palms only in oases, heather only in the high and cold lands, the same cover twice.
  - Not done: judged by eye from the shots only (no judge model was reachable from the session). Cover does not thin
    with `Quality`. Layout 4 has none.
- **WD4 (2026-11-19).** `Campaign/WorldEdges.cs` (`DressEdges`, called in `BuildTerrain` just before `Dress`; dice
  `Seed * 19 + 11`): `Shore` (a try every 4 m across landness 0.27-0.95: rocks in the surf off rocky stretches (a 40 m
  noise), stones and `Driftwood` on the dry beach, marram (a `Tussock` bleached to dry 0.45+) on the bank; none on the
  roads, by a pier (`ByQuay`) or among houses), `Boats` (`Rowboat`: three by each pier, one or two below each village
  within 34 m of the sea, on the dry beach at landness 0.6-0.75), `Roadside` (the last 50 m into a town or castle: a
  tree every 9.6 m on one side, shrunk by the walls as the stands' are, and a shrub hedge on the other; a `Milestone`
  every 45 m of the seats' roads; halfway along each seats' road over 90 m a `Wayside` shrine or a `Well`). Pieces in
  `World.Dress.cs`. `BuildTerrain` split `Open` into `Ground` (no site, river, lake or field) plus land and road.
  - Fishing boats (drawing only, `MapLife.People.cs` `SeaBoat`): up to two (four on High) by day, drifting over the
    shelf 1.2-3.5 m deep and turning back from the beach and the deep.
  - The small cover now rolls its own dice a cell (`Rng.Mix(Seed, 17, x, z)`): before, anything placed ahead of it
    reshuffled every wood on the map. Woods keep 8 m off a town's or castle's houses (3 m off a village's): a wood tree
    at a gate stood in front of the town in the close view.
  - Lake reeds were already drawn (`World.Lake`); nothing added there. Ports' piers and moored cogs came with WM7.
  - Crossroads in the open are rare on the drawn map (its roads run place to place), so the wayside shrines and wells
    stand halfway along the long roads instead.
  - Numbers (`--world-bench`, seed 11): 9,624 items (140 driftwood, 128 boats, 59 milestones, 24 shrines and wells,
    some 1,060 shore pieces); buffers 154 -> 162 MB.
  - Marram is its own kind (`Marram`: a fan of stiff pale blades, no mound); the beach's sand reaches up the bank to
    landness 0.98 (`WorldMap.Tint`), so the stones, driftwood and marram stand on sand, not lawn.
  - Judged with the Haiku fallback (the T3 judge was not reachable): round 1 scored 6 (driftwood read as a stick,
    milestones as stakes, round marram, no sand under the shore pieces); fixed and re-judged.
  - Test: `MapDressed` grew the shore, marram, boats, avenue, milestone and wayside checks.

## Closed

Closed by the owner on 2026-11-21 as finished. Not built: WD5 (wild herds by land, gulls, waterfowl, storks, campfires), WD6 (cloud shadows, wind on grass, dawn mist, dust, blown snow), WD7 (every land per season, frame time at widest zoom).
