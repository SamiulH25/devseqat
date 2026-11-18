# Pixel map demo (art rework, option 3)

Status: **demo on branch `demo/pixel-map`**, not wired into the game. Started 2026-11-18.

## The direction

The art rework goes pixel-art first. The battle camera and fight view stay as they are. The units and the
campaign map change, and the art stays painted by code. For the campaign map the user chose **option 3, tilted 2.5D**:

- One fixed camera, looking north and tilted about 55° down. It is orthographic, so every art pixel has the same
  size. You can't turn it, and zoom moves in whole steps.
- The ground is drawn as pixel art straight from the map data (`world.json` → `WorldMap`): land kinds, beaches,
  sea depth, rivers, lakes, roads and field plots.
- Trees, towns, castles, villages, sites and parties stand up as sprites. Code paints each one at the exact pixel
  size it shows (never scaled, never turned), using the soldiers' painter (`Painter.cs`).

## Run it

```bash
dotnet run -c Release --no-build -- --pixel-map /dir [seed]
```

This is headless, with no GPU and no xvfb, and takes about 0.4 s a view. Each view is written as `name.png`
(1920x1080, 3x nearest) and `name.1x.png` (640x360, the true pixels). The views are `start` (where the company
starts), one town for each kind of country the world has (`coast`, `taiga`, `desert`, `highlands`, `forest`,
`farmland`, `tundra`, `steppe`), and `wide` (the start at half scale).

## How it works (`PixelMap.cs`, `PixelMapRamps.cs`, `PixelMapSprites.cs`)

- **Projection:** `screen x = (X - x0) * k`, `screen y = (Z * sin p - height * cos p) * k - oy`, where `k` is pixels
  per metre (3 close, 1.5 wide).
- **Ground:** the ground is drawn one screen column at a time, near to far. Each sample fills from its row up to the
  last row drawn (a y-buffer), like a heightfield renderer. Per pixel it keeps how near the ground is
  (`near`, used to hide sprites behind hills) and what the ground is (`Stuff`).
- **Colour:** land takes the map's own `WorldMap.Tint`, looked up through a small noise warp so the borders
  between kinds are ragged, not stair-stepped. It is then snapped to a coarse HSV grid and given a hue-shifted
  five-tone ramp (`Ramps.Land`), so the map holds a few dozen ramps instead of a gradient.
- **Light:** the slope's light against a fixed sun to the upper left (the sprites' light) is drawn as bands. A 4x4
  Bayer dither appears only in a narrow seam where two bands meet. Grass gets clumps and sparse tufts; water gets
  broken wave-crest rows.
- **Edges:** a bank where land meets water, foam on the water's edge, and darker verges beside roads.
- **Sprites:** sprites are sorted by their foot's screen y, with a drop shadow down and to the right. A sprite pixel
  is hidden where the ground drawn there is nearer than the sprite's foot.
- **Places:** walls, houses and roofs depend on the country and the people (`BuildOf`). Most lands get grey stone,
  plaster and red tile or brown thatch. The south gets ochre walls round white flat-roofed houses with a dome. The
  north and the taiga get log walls with shingle or turf. The steppe gets yurts. A banner in the realm's colours
  flies over each town's great house and each castle's keep.
- **Labels:** names use the game's pixel font (`PixelFont.Lit`) on a dark plate, with a line in the realm's UI
  colour under towns and castles. Labels that would overlap one already placed are skipped (towns first).

## Judging

The CLAUDE.md judge (Hydrogen via T3) was not reachable in this session, so the fallback judge (Haiku) was used:

| Round | Score | What it found |
|---|---|---|
| 1 | 5/10 | dither everywhere, sea specks read as stars, villages read as haystacks or boulders, labels on roofs, pale towns lost in the sand, parties on the gate |
| 2 | 6/10 | villages, labels, sea and dither fixed; mottled dark greens, yurts read as mushrooms, taiga roofs read as moss mounds, fields at random angles |
| 3 | 7/10 | coarser value steps, spaced yurts, shingle roofs, labels clear of towns, fields snapped to 0, 1:2, 1:1 and 2:1 |

Polish still open after round 3: the steppe and taiga ground is still a little speckled. Southern battlements merge
with bright sand. The rider beside a town gate touches the wall's footing. Taiga roofs need a ridge line and eaves.

## What a real build would need (not done)

- **In game:** bake the ground once per season into textures (tiles of the screen-space image, since the camera
  never turns). Re-bake only the sprites each frame. Draw it all into a 640x360 target scaled up by a whole number.
- **Time of day and weather:** palette shifts on the baked image (night ramps), not live lighting.
- **Life:** moving parties walk on the image, with sprites redrawn per frame. Smoke, birds and waves are small
  animated sprites.
- **Zoom:** 1.5, 3 and maybe 6 px/m, each baked at its own scale. Painted sprites are cached by size.
- **Picking:** clicks go back to world positions through the `near` buffer (screen pixel → world Z, X).
- **Not yet drawn:** province or realm borders, fords and bridges as their own sprites, sieges and burning villages,
  site sprites beyond the basic shapes.
