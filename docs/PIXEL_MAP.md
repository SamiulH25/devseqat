# Pixel map demo (art rework)

Status: **demo on branch `demo/pixel-map`**, not wired into the game. Started 2026-11-18.

## The direction

The art rework goes pixel-art first. The battle camera and fight view stay as they are; the units and the campaign
map change; art stays painted by code. The first demo was a tilted 2.5D height field (option 3). On 2026-11-18 the user
rejected it: parties stood in the sea, things overlapped everywhere, and the sprites did not work. They asked for
**the world entirely pixel art, at an angle, with minimal elevation changes**. This is that version:

- **One tile grid.** A tile is 4 m, drawn 12 pixels across. The ground is seen from straight above. Things on it
  (trees, houses, walls, men) stand up in a three-quarter view, as in classic pixel-art maps. The camera never turns,
  and every art pixel is the same size.
- **No overlap by rule.** Every thing claims whole tiles, and a tile holds one thing: a town takes 8x6 tiles, a
  castle 6x5, a village house 2x2, a site 2x2, a field block 2x3, a tree 1, and a party 2-4 by 2. A party also keeps
  one tile clear of walls, houses, sites and other parties. No tree stands just south of a field, where its crown
  would rise over it. Only a sprite's top rises into the tile behind it, as 3/4 art always does.
- **Minimal relief.** The ground is cut into terraces 2.2 m high. A step up shows a three-pixel earth face on its south
  side, and a one-pixel shade on its east and west sides. Higher terraces are a shade lighter. There is no height
  field and no occlusion.
- **Hand-ruled sprites.** Every sprite is placed pixel by pixel in code from five-tone hue-shifted ramps, lit from the
  upper left, with a selective outline. Sprites are never scaled shapes. A man is 13 px tall, a rider 20 px long, a
  house 16-22 px wide.

## Run it

```bash
dotnet run -c Release --no-build -- --pixel-map /dir [seed]
```

This is headless, with no GPU and no xvfb, and takes about 0.5 s a view. Each view is written as `name.png`
(1920x1080, 3x nearest) and `name.1x.png` (640x360, the true pixels). The views are `start` (where the company starts)
and one town for each kind of country (`coast`, `taiga`, `desert`, `highlands`, `forest`, `farmland`, `tundra`, `steppe`).

## How it works

- `PixelMap.cs`: the run, the views, and the ground. Each pixel samples the world at its tile-aligned point: sea by
  depth (banded), rivers and lakes, a place's yard, a field, a road, or land coloured by the map's `WorldMap.Tint`
  (warped so the borders between kinds are ragged, then snapped to a few dozen ramps). Then come `Terraces` (the step
  faces) and `Edges` (foam on water under land, verges beside roads).
- `PixelMapPlan.cs`: the plan of the tiles (`Use`), claimed in order. Each step takes only the tiles still free:
  1. water, steep tiles and roads (`Base`)
  2. towns and castles; two places the map sets close together are pushed to the nearest clear footprint (`Places`)
  3. village houses on free 2x2 plots round the village
  4. sites
  5. fields (`Fields`): 2x3-tile blocks worked by the nearest place within its reach, with lanes on uneven columns and
     rows
  6. trees (`Trees`): the map's own trees, plus stands grown from the land kind's tree count through a patchy noise,
     kept a tile clear of houses
  7. parties (`Parties`): the nearest free land or road block to where each party is. A party at a place stands just
     east of it, facing it, and only one party shows per place.

  The same file also draws: shadows (an oval at the foot, or a strip along a place's east wall), sprites sorted by
  foot, and labels. Labels are tried above a place, below it, then further out, and are never laid over another place,
  a party or another name.
- `PixelMapPix.cs`: `Pix`, the pixel canvas (`Set`, `Rect`, `Oval` with a per-pixel colour, `Put`, `Outline`). Also
  `Tones` (ramps), `Ink` (outline shade), the sprite cache, and `Charge` (a realm's emblem in pixels).
- `PixelMapArt.cs`: trees, pines and bushes; `House` (tile, thatch or shingle roof, plaster with timber framing, log
  walls, or the south's flat roofs and dome); yurts; wall runs, wall sides and towers (round with a cone, square and
  flat, or timber); `Town`, `Castle` and `Site`. `Build` picks the look by people and country: grey stone and red
  tile, ochre and white flat roofs in the south, logs and shingle in the north, a palisade and yurts on the steppe.
- `PixelMapFolk.cs`: men (kettle hat, hood or bare; spear, bow or axe; a round shield with rim and boss), riders
  (a horse with mane, a low head and a tail; a knight's caparison in the realm's dark cloth with its charge; a
  swallow-tailed banner), wolves, a bear, an ox, spiders, a wyvern and a covered wagon. `Party` lays them out as one
  sprite and mirrors it when the party faces left.
- `PixelMapRamps.cs`: the ground's ramps (sea, fresh water, road, hedge, fields, land snapped in HSV).

## Judging

The CLAUDE.md judge (Hydrogen via T3) was not reachable in this session, so the fallback judge (Haiku) was used.

| Round | Score | What it found |
|---|---|---|
| 1-3 | 5, 6, 7 | the first, tilted height-field demo (commit d93ef60) |
| 4-6 | 6, 6, 7 | strip fields and parties as scenes on that demo (commit 8d63b69) |
| 7 | 7/10 | this tile-grid redesign: coherent, consistent scale, no party on water; a party touching a town tower and trees rising over fields (both fixed after) |

Still open after round 7:
- Some labels lie over a field or a tree.
- Every desert hamlet is the same white box.
- A lone watch tower on the steppe reads poorly.

## What a real build would need (not done)

- **In game:** draw the ground once per season into tile-aligned chunks and redraw only sprites each frame. Render into
  a 640x360 target and scale it up by a whole number.
- **Zoom:** whole steps only (12 px tiles at 1x, 2x and 3x). A far zoom would need its own small sprites.
- **Time of day and weather:** palette shifts, such as night ramps.
- **Life:** parties move tile to tile on the plan. Smoke, birds and waves are small animated sprites.
- **Picking:** a click goes to its tile (screen pixel / 12), then to what claimed the tile.
- **Not yet drawn:** borders, bridges and fords as sprites, sieges, burning villages, and a far zoom.
