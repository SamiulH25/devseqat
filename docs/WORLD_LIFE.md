# World life and the diorama look (plan WL0-WL7)

Status: WL0 built (Muted chosen for battles; the map later moved to the grounded look, WL4b). WL1-WL6 built (see each "As built"; some items deferred). WL7 paused until asked for; move this file to `docs/archive/` once WL7 is dropped or built.

## The goal

The world looks plain and empty. Ironfield should look like a **painted miniature diorama**: flat-shaded low-poly land in
calm, hand-picked colours, warm light with coloured (not black) shadows, the pixel-art men standing on it like painted
figures, and a land that is **lived in**: birds in the air, beasts in the fields, villagers at work, wagons on real roads,
rivers and lakes that look like water.

Decided (talk with the owner, 2026-11-11):
- **Stay low-poly and stylised, not realistic.** Realistic ground beside 15-pixel men makes them look pasted on, needs an
  art pipeline the project does not have, and turns a 3,000-man field into noise. Everything stays generated in code.
- **The main complaint is that the world is plain.** Ambient life, real roads and real water come before polish.
- **The diorama finish (WL7) is paused and optional**: it is done only if asked for once WL0-WL6 are in.

## Rules for every phase

- **Drawing only.** Ambient life (birds, beasts, villagers, carts) lives in the screens, never in `Campaign`, `Battle` or
  the save. It uses its own seeded `Random`, so it can never move a replay or a campaign (`SelfTest.ReplayExact` stays green).
- **A seed's map never changes for an existing layout.** New map generation (routed roads, lakes, field plots) goes behind
  `WorldMap` layout 4; layouts 0-3 keep their maps. Drawing-only additions (birds, herds) may appear on any layout.
- **Budgets.** Map ambience at most 1.5 ms a frame on High; battle ambience at most 0.5 ms. Everything thins out with
  zoom and with the Quality setting (Low: no ambience). Check with `--bench` and the shots' `render.txt`.
- **Shots.** Each phase adds or updates staged shots (`IRONFIELD_CAMPAIGN_SHOTS`, `IRONFIELD_SHOTS`) so the change can be judged by eye.

## Phases

### WL0 Look test (choose the palette by eye)
Two or three light-and-colour presets switchable at run time (`IRONFIELD_LOOK=current|diorama|muted`, and a debug key):
sun and ambient colour, shadow tint and depth, saturation and grade in post, fog colour, terrain palette. Shots of the
same map view and battle under each. The owner picks one; it becomes the default and the others go.
Files: `Sky.cs` (light by hour/weather), `Shaders/*.glsl` (shadow tint, grade), `World.cs` (terrain colours).
As built: `Looks.cs` holds the three presets (`current`, `diorama`, `muted`); F7 cycles them on the map and in battle (a
note names the one now on), `IRONFIELD_LOOK` picks one, `Settings.Look` remembers it. The land's palette is applied in
`world.glsl` (`Palette`: saturation, lightness, warmth), so it switches at once without rebuilding meshes. Comparison
shots: campaign shots with `IRONFIELD_MAP_HOUR=11` (new campaigns start at night) and `IRONFIELD_SHOTS` (seed 7, clear).
Gotcha: with `SDL_VIDEODRIVER=offscreen` the window stays 800x480, so set Width/Height 800x480 in settings.json or the
PNGs show only a corner. The diorama's first cut (saturation 1.2, warmth 0.6) turned the grass acid lime; it is now 1.08 / 0.25.
Decision: the owner chose **Muted**. `Looks.cs` now holds only its values (constants); F7, `IRONFIELD_LOOK` and
`Settings.Look` were removed with the other presets.

### WL1 Real roads (layout 4)
Today roads are straight lines between province centres, painted into the ground colour (`WorldMap.Generate`, `World`
terrain tint), so they read as smears.
- Route each road over the land: a cheap path search on the height map that prefers flat ground and valleys, bends round
  hills, meets rivers at bridges or fords. Stored as polylines (more segments in `WorldMap.Roads`; `MapIndex` already handles that).
- Draw them as their own ribbon mesh on the ground: packed earth with wheel ruts, grass verges, a paler crown; wider
  near towns, narrower to villages. Bridges as models where a road crosses a river.
- By the roads: milestones, wayside shrines, a well or inn sign near crossroads, fences near villages.
- Old layouts get the ribbon drawn along their straight roads (no map change).
As built: `Campaign/WorldRoads.cs` (`WorldMap` is now partial). Layout 4 (`CurrentLayout`) routes each road with A* on a
4 m grid over `LandHeight` (cost rises with slope; lakes and rivers barred except at the straight line's own crossings and
ridge passes, which are kept as straight approaches), then simplifies and Chaikin-smooths it. The drawn line is
`RoadLine` (half width 2.6 m between towns, 1.7 m out to a village); the gameplay `Roads` become a coarse copy
(`Router.Simplify(points, 1.2)`) so `RoadDist` stays cheap (~2,900 segments; campaign day ~147 ms, setup ~3 s against ~1 s).
Each crossing of a river becomes a `MapBridge` (stone between towns, planks to a village). Drawn by `World.Roads`
(World.Water.cs): a seven-column ribbon (verge, earth, rut, crown, rut, earth, verge) 0.32 m above the ground, in 40 m
culled tiles, tinted 10% by the season's ground colour, with dark edges so it reads against grass; `GroundColor` wears
the verges within ~2.6-6 m of a road. Roads carry the UV tag `Trodden` (0, -0.25): `world.glsl` lets little snow lie on
them and darkens them to slush, so they stay visible in winter.
Gotchas: the ribbon must sit ~0.3 m up or the terrain mesh's triangles poke through between samples; after changing
`Roads`, rivers or heights set `index = null` (the `MapIndex` grid is lazy).
Deferred: ribbons along old layouts' straight roads, roads on battlefields, milestones, shrines, wells, fences.

### WL2 Rivers and water
Today a river is a strip of flat quads per path segment in one deep or shallow colour (`World.Water`), with no motion,
no banks and no lakes or sea.
- Rivers that read as water: a ribbon mesh that widens downstream, flow drawn in the shader (scrolling ripples along the
  river's direction, foam where it narrows or meets a ford), a shallow lighter edge, the sky's colour and the sun's glint
  on the surface; muddy or sandy banks cut a little into the ground, reeds and rushes in slow stretches.
- Still water (layout 4): ponds near villages, a lake or two in low ground, marsh pools in the fens; wind ripples, lily
  pads, a jetty or a punt. Where the map meets the sea: a shoreline with surf and a pale beach.
- Works on the water: fords that look shallow (stones, a churned bed), bridges by kind (plank, stone arch, a town's
  bridge with houses), mills with turning waterwheels at the `mill` sites, a ferry where a road meets a wide river
  without a bridge, fishing boats, a weir.
- Life on it: ducks and swans, a heron at the edge, a fish rising now and then (drawing only, WL4's rules).
- Winter: rivers edged with ice, ponds frozen over. Rain: the surface pocked and darker.
- The battlefield gets the same water (`Terrain` rivers and fords in battle): flow, banks, reeds, a splash where men wade.
Files: `World.cs` (`Water`), `Shaders/world.glsl` (a water tag like `MasonryTag`/`FlameTag`), `Campaign/WorldMap.cs` (lakes behind layout 4), `Campaign/Sites.cs` (mills).
As built: lakes and village ponds on layout 4 (`WorldRoads.PlaceLakes`, own dice `seed*31+7` so the rest of the map
draws as on layout 3; `Lake` in Terrain.cs carves the height, `InLake` slows parties to 0.35 and keeps trees off).
`World.Water` (World.Water.cs) draws rivers as ribbons whose UVs carry the flow (x = across -1..1, y = -10 - metres along;
still water x = 5), muddy banks, reeds and cattails in the quiet stretches, stepping stones at fords no bridge covers,
lakes as discs with a shore ring and reeds, and the map bridges. `world.glsl` treats `vUV.y < -9.5` as water: ripples
running downstream, pale shallows and foam at the banks, fresnel sky, sun glint, rain pocks, ice in winter. The same
code draws battlefield rivers. Layout 4 map rivers are 4.6 m wide and 1.5 deep (2.6 / 1.1 before) so they read at map
zoom; banks have a dark wet rim; a pale streak runs down the current; lakes carry their radius in UV x (5 + share out to
the shore) for a darker rim; ice is a cracked cold blue, darker at the rim, so it does not vanish into the snow.
These came from a screenshot critique (mountain wall, field colours, label overlap and panel size were also raised: WL3 / UI). The old "flat, blue, uv 0" water test in the shader is gone.
Deferred: sea and coast (the map has none), turning waterwheels, ferries, boats, weirs, lily pads, jetties, ducks and
swans (WL4), splashes where men wade (WL6).
Side fix found by the new map: weekly garrison refills (`GarrisonsWeekly`) could spend a realm's chest to 0 (the only
drain without a floor); they now keep the same 300-crown floor as lords' wages (`SelfTest.CampaignRun` war chest check).

### WL3 Land with meaning
- Farmland round villages and towns: a patchwork of field plots (wheat, green, ploughed, fallow) with hedges or fences,
  haystacks in summer, bare in winter.
- Pasture with fences; orchards near towns.
- Forests as stands with edges and clearings, not even scatter; two or three tree shapes per climate.
- Towns and castles a little larger as map pieces; the party figures a little smaller, so the scale stops fighting.
As built: `Campaign/WorldLand.cs` (layout 4, own dice, drawing only: the campaign never reads it). `LayFields` lays a
rotated grid of strips in a ring round each village and town, kept off roads, water, ridges, sites and other settlements
(a cell a road crosses keeps whichever half is clear); kinds `FieldKind` Grain, Green, Ploughed, Fallow, Pasture (one in
five), Orchard (a town's nearest one or two). Stored on the renderable `Terrain.Fields` (`FieldPlot`) with
`Terrain.MapSeason`. `World.Land.cs` draws each plot as a patch over the ground (colour by kind and season: young corn,
ripe wheat, stubble...), hedges on about 60% of sides (browner in autumn and winter), post-and-rail fences round
pastures, haystacks on hay meadows in summer and stubble in autumn; orchards are rows of small trees (foliage). Rows
and furrows are drawn in `world.glsl` from the UV (x = 100 + metres across, y = strength; the vertex shader keeps
x >= 50 from swaying), fading out by ~190 m before they could shimmer. Woods on layout 4 grow as stands
(`GrowStands`): dense hearts, smaller trees and bushes at the edge, clearings where a noise field dips, and a third tree
shape, the birch (`FoliageKind.Birch`, golden in autumn). Trees keep out of fields. Towns and castles draw at 2.5 (was
2.1), villages at 1.8 with bigger cottages round a green; the map's party figures at 2.5x (was 3x).
Also from the screenshot critique: the mountain rim is graded (`WorldMap.Tint`): scrubby foothills, warm brown to warm
grey to cool blue-grey rock with height, strata bands, steep faces darker, snow sliding off the steep faces.
The map now passes `Snow` in its winter weather (`MapScreen.Draw`), so the shader whitens fields, hedges, tree tops and
roofs and freezes the water; before that the map's winter lived only in the baked ground tint.
Deferred: climate-specific tree sets (only the birch was added).
Revised after the owner judged the first cut ugly (a screenshot critique named the causes): hedges were near-black 1 m
ridges on 60% of edges, netting every village in black; fields were flat equal rectangles laid on the grass like a
quilt; stands were stamped blobs; beasts were twice a fence post high; the rim was a wall. Now: 5-7 plots a village
(9 a town) in a ragged ring, sizes and angles varied, the crop colour strongest in the middle and fading into the
meadow toward the plot's edge and corners (`Plot`), rows only within ~110 m and hidden by snow; hedges on a third of
sides, 0.6 m wide and 0.55-0.85 m tall in the trees' green, gaps every few metres and open corners, snow-pale in
winter; stands thickest at the heart and thinning to their edge, three sizes, birch 1 in 8, trees by towns smaller;
beasts about one fence post high; the rim rises through 7 m of rolling foothills before the range (layout 4,
`LandHeight`), and the far ring of peaks (`World.Skirt`) fades toward the haze.
Shots: set every tip key in the shot run's `settings.json` `TipsSeen` or tip boxes cover the frames.

### WL4 Beasts and birds on the map
- Birds: flocks wheeling over woods and fields, crows over grave fields and battle sites, gulls on the coast, a hawk
  circling alone. Drawn as small sprites with two-frame flaps, flocks moving as a group (simple boids).
- Beasts: deer at forest edges, sheep and cattle in pastures, a few rabbits; they scatter from an army that comes near.
- Spawned round the camera, kept within a radius, culled with zoom.
As built: `MapLife.cs` (drawing only, own dice; created in `MapScreen.Open`, moved in `MapScreen.UpdateLife`, drawn
through `World.Extra`/`ExtraTris` in the shaded passes, so beasts and birds are lit, shadowed and fogged; Low quality has
none). Herds: sheep or cattle in each pasture (kept inside the fence), deer at wood edges (empty 8 m cells beside trees,
one in nine), rabbits in one field in three (not in winter, only within zoom 140). They graze (head down), amble, and
run from any party within 10-24 m (deer bolt, rabbits dash and vanish). Birds by day: wandering flocks (boids-lite:
each bird holds a drifting place in its flock), crows circling grave fields and old battlefields, one hawk gliding in
wide circles. Counts: 40 herds / 5 flocks on High, 20 / 3 on Medium; herds drop beyond zoom 300, birds beyond 520.
Sizes: beasts about life size against fences and cottages (1.1-1.4x), birds 3.6x life so they read from the air. Shots: `campaign-farm.png` (a village's
fields), `campaign-life.png` (a pasture close up), staged in `ScreenHost.CampaignShots` before the year turns.
Gotcha: a GUI run's stdout goes nowhere; log to a file to debug. Deferred: gulls (no coast), the frame-time budget
was not measured with `--bench` (the map has no bench for drawing).

### WL4b The grounded look (map only; look test, built)

After WL3/WL4 the stylised map still read as plain, so the overworld moved to a grounded, realistic look (chosen over
3D pixel art, an illustrated map and a painted diorama). Battles keep the muted chronicle.
- `World.Materials.cs` `BuildMaterials`: on the map (`!t.Grass`) the Detail slot holds a 256x256 tileable texture made
  in code: R grass, G earth and pebbles, B rock strata, A broad variation. Battles keep the grey `BuildDetail`.
- `world.glsl` TERRAIN map branch (`Field.y < 0.5`): `MapLand` (olive greens, hue drifting over a few hundred metres,
  lush and dry patches tens of metres across, grass texture), earth on slopes 0.16-0.3, rock past 0.3, pale ground
  (snow) keeps its colour, and shade in hollows from `World.Cavity` stored in the terrain vertex alpha.
- Field plots run through `MapLand` at their feathered edge and keep their crop colour in the middle (crop share in
  the field vertex alpha), so plots meet the meadow without a seam.
- Map canopies: `Mesh.Blob` gives leaf blobs (Tag.Y >= 0.5) normals from the crown's centre, so crowns shade round;
  the shader adds leaf clumps and darker undersides (map only).
- `Looks.Grounded` (set by `MapScreen` before `Sky.Set`, cleared by `BattleScreen`) switches light and grade to
  natural: sun 1.0, ambient 0.95, cool shadow tint, saturation 0.78, grade (1.0, 1.06, 0.2).
- Not done yet: the map's rim still reads as a wall in the overview, and hedges are small dark dashes from far off.

### WL5 People and traffic
- Villagers in the fields (working, walking home at dusk), a few figures in town streets, smoke from chimneys, market stalls.
- Walkers, riders and carts on the roads between nearby settlements (drawing only, following WL1's polylines);
  boats on the wider rivers (WL2).
- The real caravans and pilgrims (`PartyKind.Caravan`, `Campaign/Roads.cs`) drawn as wagon trains with oxen and outriders,
  pilgrims as a file of walkers, instead of soldier figures.
- Night: lit windows, torches at gates, fewer people about.

As built (`MapLife.People.cs`, drawing only):
- Folk: villages and towns (from the map's Village/Town foliage) keep 2-6 people by day on the green or round the edge;
  1-3 hands work every other non-pasture plot from 06:30 to 18:00 outside winter, stooped to a hoe, and walk to their
  village at dusk, then vanish indoors. Drawn as boxes `Folk` (1.7) times life size.
- Roads: walkers, riders and ox carts on `Terrain.RoadLines` in view (14 by day on High, 7 on Medium, 2 riders at
  night), keeping to one side; boats poled downstream on rivers 4 m wide or more (not at fords or bridges).
- Caravans and pilgrims: `MapScreen.UpdateLife` passes their parties (`SetCaravans`); each keeps a breadcrumb trail and
  draws 2-3 canvas-covered wagons (merchants) or a file of six walkers (pilgrims) behind the leader's puppet.
- Smoke: `World.Chimney` puts a stack on every other cottage (every fourth town house) and records it in
  `World.Chimneys`; the 16-40 nearest smoke, five puffs each, rising and drifting.
- Night: `World.Windows` puts small windows on every house's long walls, UV tag y = -3; `world.glsl` shows them dark
  by day and lamplit (70% of them) as `Look.z` (night) rises. The masonry and flame branches now test their own tag
  bands. Torches at gates were not added. The map's night floor (0.7) keeps it fairly light, so the lamps are subtle.
- Shots: `campaign-village.png`, `campaign-road.png`, `campaign-night.png` (22:30, the clock restored afterwards).

### WL6 Life on the battlefield
- Birds lift from the woods when the armies close; crows circle once the dead lie on the field.
- Field edges that say where the battle is: hedgerows, a distant farmstead, a cart left by the road, grazing beasts
  that run off at first contact.
- Ground detail: denser grass tufts, earth and trampled patches (builds on `BattleScreen.Effects` trample map).

As built (drawing only, the sim and replays untouched):
- `MapLife` has a battle mode (`MapLife.Field.cs`, made by `BattleScreen.Life.cs` `NewFieldLife` with each new world):
  2-3 herds of sheep or cattle graze 110-170 m out and run from men within ~11 m; deer at wood edges; up to ten roosts
  (8 m cells of three trees or more) whose birds lift in a climbing flock, away from the men, once any man comes within
  35 m; crows circle up to three 16 m cells holding a dozen dead or more (`Carrion`, every 2 s). Stepped with the
  sim's time (`FieldLife(dt * scale)`), so pausing stills it. Birds are drawn at 1.4x life, not the map's 3.6x.
- `World.Outskirts` (World.Land.cs): on open battlefields (not sieges) a farmstead 175-215 m out, clear of water and
  trees: house with chimney, barn, a paddock fence with a gate gap, three haystacks, a cart.
- `world.glsl`: worn earth patches and faint sheep tracks on the battle ground at any distance (`Field.y > 0.5`).
- Not done: denser grass tufts (the grass layer is unchanged).

### WL7 Diorama finish (paused, optional)
Only if asked for after WL6.
- Men pop off the ground: a slight outline or rim light on soldier sprites, a soft shadow blob under each.
- Optional tilt-shift blur at the screen edges on the map; parchment edge at the map's border.
- Labels thinned: minor party names only when zoomed in or hovered.
- Final tuning of the chosen palette per season and climate.

## Order

WL0 first (it decides the colours everything else is painted in), then WL1 roads and WL2 water (together: they cross
at bridges and fords), then WL4 beasts and birds, WL3 land, WL5 people, WL6 the battlefield. WL7 waits until asked for.
Move this file to `docs/archive/` when WL6 ships (and WL7, if it is taken up).
