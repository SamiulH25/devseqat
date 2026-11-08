# Ground that fights: a combat terrain overhaul

Plan and progress. Each phase T0-T8 can ship on its own, and `tools/check.sh` must pass after each one.
When a phase ships, add its "As built" note at the end.

## 1. Where we are

A battlefield (`Terrain.cs`) is a heightmap of ±256 m on 2.5 m vertices. It has rolling noise, 1-8 round hills,
a rim of high ground, an optional road, an optional river with two fords, and foliage. `TerrainParams` (Meadow,
Farmland, Forest, Hills, Marsh, Steppe, Badlands, Lists) changes only the numbers: how many hills, how high,
how many woods, trees and bushes. The campaign picks one with `WorldMap.BattleGround`.

What the ground does in the sim today:

| Effect | Where | Notes |
|---|---|---|
| Uphill slow, downhill quick (×0.55-1.15) | `BattleMove.SlopeFactor` | the same for foot and horse |
| High ground in melee, ±15% | `BattleCombat.Hit` (L173) | height difference ×5% a metre, melee only |
| Archers on a 2 m height reach 20% further | `BattleAI.Think` (L61) | an on/off step, not a curve |
| Water slows (ford ×0.8, river ×0.45 foot / ×0.55 horse), no bracing in water | `BattleMove.Wading`, `Battle.Braced` | |
| Trunks, boulders, houses, wagons block; a horse that hits one loses 40% speed | `Terrain.PushOut` | each is a circle; a "wood" is only many separate trunks |
| Rain makes mud and slows horses | `Weather.MudFactor` | the same everywhere on the field |
| Merlons cut arrow damage on the wall | `BattleCombat` (L104) | sieges only |

What it does **not** do:

- **No kinds of ground.** Grass, crops, scrub, stony ground, mud and the forest floor are all the same to the sim.
  Only the renderer (`World.GroundColor`) and the tint know about them.
- **Woods are not places.** Men in a wood get no cover from arrows, and they are not hidden. A wood slows nobody
  except by the trunks they bump into. Formations stay tidy, and horse can gallop between the trees.
- **No line of sight.** Every unit sees every other unit (only `Weather.Vision` limits range). A ridge hides nothing,
  so there are no ambushes and no reverse slopes.
- **No pathing.** Men steer straight at their slot or foe, and `PushOut` slides them round trunks. A company ordered
  across a river wades the deep water instead of going round to a ford. Nothing understands a wall, a hedge or a bridge.
- **The AI is blind to the ground.** `General` places the line from the centroids. `Deploy` uses fixed offsets.
  Division goals (`Hold`, `Take`) go to points the player or `AiCommand` picked, with no idea of high ground,
  woods or water.
- **The field does not scale.** `Terrain.Core` keeps ±50 × ±54 m clear, but `MapRadius` grows to 230 m for big battles
  (docs/COMBAT_OVERHAUL.md). A 3,000-man battle spills into random woods, and features never sit where the fight is.
- **The ground has no story.** Features are scattered at random. There is no "the ridge between us", no "the village
  in the middle", no "the bridge we must hold". A battle on Forest ground plays like one on Meadow ground with more trees.
- **The player cannot read it.** No tooltip, no overlay, and a minimap with no contours. Deployment does not show
  where the good ground is.

## 2. Goals

1. **Every kind of ground plays differently**, and you can tell what it does by looking at it. Woods, hills, water,
   mud, roads, crops, scrub and stony ground each change speed, formation, fatigue, cover or the charge.
2. **Features with a purpose.** Each field is built from a layout ("a ridge between the armies", "a river with
   a bridge", "a village in the centre", "a pass") scaled to the size of the battle. Then the ground is worth
   fighting over.
3. **Sight and cover.** Woods and crests hide men. Woods, hedges and walls stop arrows. Ambushes and reverse-slope
   defences become real tactics.
4. **Men go round what they cannot cross.** They use fords and bridges, file through gaps in hedges, and go round
   dense woods when they are in formation.
5. **The AI and the sergeants use the ground.** They hold crests, anchor a flank on a wood or a river, put archers
   behind cover, keep horse out of trees and bogs, and attack where the ground favours them.
6. **The campaign feeds it.** The map cell, its roads and rivers, the weather of recent days, scouting and the
   Tactician perk decide what field you get and who chooses where to stand. Auto-resolve learns the same lessons.
7. **No cost you can feel.** The `--bench` budget holds (3,000 men under 6 ms a tick), replays stay exact,
   and the headless sim stays headless.

## 3. The model: a ground layer

The core of the overhaul is a **headless ground layer** on the terrain's own 2.5 m vertex grid. It is stored as bytes,
so it is cheap to read. Every system reads this layer, not the foliage list.

```csharp
public enum GroundKind : byte { Grass, Road, Crops, Scrub, Stony, Mud, Wood, DenseWood, Ford, Water, Bridge, Street, Sand }
public readonly record struct Feature(FeatureKind Kind, Vector2 A, Vector2 B, float Width, int Id); // hedge, fence, wall, ditch, bridge, palisade, stakes
partial class Terrain {
    byte[] ground;                         // Ground per vertex cell
    byte[] cover;                          // 0-255: how much this cell hides men and stops arrows (woods, crops, hedges)
    public GroundKind GroundAt(Vector2 p);
    public float CoverAt(Vector2 p);       // 0..1
    public IReadOnlyList<Feature> Features;// line obstacles (segments, not circles)
}
```

The numbers live in a data table, `Data/ground.json`, not in code. `Ground.Validate` checks the file, the same way
`Gear.Validate` does. Each kind has these fields:

| Field | Meaning |
|---|---|
| `footSpeed`, `horseSpeed` | speed multipliers (stacked with slope and fatigue) |
| `fatigue` | how much faster men tire here (heavy armour more: × armour weight) |
| `order` | how well a formation keeps its shape (0 = falls apart, 1 = parade ground). Low order loosens slots and blocks bracing and the shield wall |
| `charge` | a multiplier on charge impact and lance damage (a gallop over stones or mud is no charge) |
| `stumble` | the chance per second that a galloping horse stumbles (a short stagger, no harm) |
| `cover` | default cover (arrows stopped, sight hidden) |
| `reach` | a penalty for long weapons (pikes and lances in a dense wood) |

A first pass at the values (to be tuned with `--balance`):

| Ground | Foot | Horse | Order | Charge | Cover | Notes |
|---|---|---|---|---|---|---|
| Grass | 1.0 | 1.0 | 1.0 | 1.0 | 0 | the baseline |
| Road | 1.1 | 1.15 | 1.0 | 1.0 | 0 | quick marches, flanking rides |
| Crops | 0.95 | 0.9 | 0.9 | 0.9 | 0.25 | tall wheat in summer and autumn only (season from the campaign) |
| Scrub | 0.85 | 0.75 | 0.75 | 0.7 | 0.2 | gorse and broom: light cover, bad for horse |
| Stony | 0.9 | 0.7 | 0.85 | 0.6 | 0 | badlands and hill tops; horses stumble |
| Mud | 0.7 | 0.55 | 0.7 | 0.5 | 0 | fen ground, and any low ground after rain |
| Wood | 0.8 | 0.5 | 0.55 | 0.3 | 0.55 | open woodland |
| DenseWood | 0.6 | 0.25 | 0.3 | 0.1 | 0.85 | horse almost useless; no formation |
| Ford | 0.8 | 0.85 | 0.6 | 0.6 | 0 | as now |
| Water | 0.45 | 0.55 | 0.2 | 0.2 | 0 | as now, plus fatigue |
| Bridge | 1.0 | 1.0 | 1.0 | 1.0 | 0 | narrow: a chokepoint |
| Street | 1.0 | 0.9 | 0.6 | 0.6 | 0.3 | between village houses |

Weather turns ground: rain moves Grass and Crops toward Mud in the low cells, by how wet the last days were
(see T7), and snow slows everyone. This replaces the field-wide `MudFactor`.

**Line features** (`Feature`) are segments, not circles. A hedge or a fence blocks movement except at its gaps.
It gives cover to men who stand right behind it, and it breaks a charge that hits it. A ditch slows and gives cover
to men in it. Stakes and a palisade (raised by archers, see T6) stop horse. A bridge is a strip of Bridge ground
over Water, and its rails block the sides. Features live in their own bucket grid, which `Move` reads.

## 4. Phases

### T0: groundwork and measurement (no visible change)
- Add `Ground`, `ground[]`, `cover[]`, `GroundAt` and `CoverAt` to `Terrain`, filled from what exists today:
  woods become Wood (cells within a wood's radius), the road becomes Road, the river becomes Water or Ford,
  and everything else stays Grass. Nothing reads them yet.
- Add `Data/ground.json` with every kind at the neutral 1.0 values, `Ground.Validate`, and `SelfTest.GroundData`.
- Add `Terrain.Version` and write it into `Replay` (`Replay.CurrentVersion` 2). A replay from an older terrain version
  says it cannot be replayed, instead of quietly coming out different.
- `--bench` prints `terrain` as its own stage. Record the baseline numbers in this doc.
- Add a self-test, `TerrainDeterministic`: the same seed and params give the same ground, cover and features.

### T1: ground that changes how men move and fight
- `Move`: speed is `GroundSpeed(u) × SlopeFactor × Wading`, with foot and horse read separately.
  `Wading` folds into the ground table (Ford and Water).
- Slope is split by class. Horse loses more uphill (×2 instead of ×1.5) and charges harder downhill.
  Heavily armoured men tire on a climb (`Tire` reads the slope and the ground's `fatigue`).
- Order: a squad's slot spacing grows and its facing loosens on low-order ground (`Squad.SlotSpread`).
  Bracing and the shield wall need order ≥ 0.6. Men in a wood fight as a loose crowd, as they should.
- Charge: `Impact` and couched lances are scaled by the ground's `charge` at the point of contact.
  Galloping on Stony, Scrub or Mud can stumble a horse (`Stagger`), using the seeded `rng` only.
- Reach: pikes and lances lose part of their reach bonus in Wood and DenseWood.
- High ground is reworked as one function, `HighGround(a, t)`: a smooth curve on the height difference and
  the slope between the two men, capped at ±15% as now, plus +10% to a charge that runs downhill into contact.
  The range bonus for archers becomes a smooth curve too (up to +25% at 6 m higher).
- Tests: `WoodSlowsHorse`, `MudTiresArmour`, `NoBraceInWood`, `DownhillCharge`, `StumbleIsSeeded`.

### T2: fields with a purpose
Replace "scatter features at random" with **layouts**. A layout is a small recipe that places named features
relative to the two armies' lines. The armies always meet along Z = 0, team 0 deploys at negative Z and team 1
at positive Z. Every layout scales with `MapRadius`, and the clear deployment zones scale with the size of each
army, which replaces the fixed `Core`.

| Layout | What is on the field | Used by |
|---|---|---|
| Open field | gentle swells, one flank wood (today's look) | Meadow, Steppe (no wood) |
| Ridge | a long crest across one army's front, and a reverse slope behind it | Hills, Meadow (sometimes) |
| Two hills | one knoll on each wing; whoever holds both rules the field | Hills, Badlands |
| River line | a river between the lines, one or two fords, and a bridge where the campaign road crosses | any cell near a river |
| Wooded flank | a large wood on one wing that hides an approach | Forest, Meadow |
| Forest clearing | a ring of dense wood with rides cut through it | Forest |
| Village | houses, gardens, hedges and fences in the centre (Street ground), crops around them | Farmland, near a village on the map |
| Hedgerows | fields cut up by hedges with gaps; bad for horse, good for archers | Farmland |
| Fen | Mud and Water channels, causeways of firm ground, reed beds that give cover | Marsh |
| Pass | high ground on both sides, and a narrow floor between them | a ridge cell on the campaign map |
| Broken ground | Stony knolls, gullies, scrub | Badlands |

- `TerrainParams` gains `Layouts` (a weighted list), plus `Season` (crops, snow) and `Wet` (0..1, see T7).
  Existing presets keep their names. The battle seed still picks everything.
- Add `Terrain.Features`: hedges, fences, ditches, bridge rails and village walls, as segments with gaps.
- Line features get collisions in `Move`: push a unit out of a segment, the way `PushOut` handles a circle.
  A horse that hits a hedge at speed stops, as it does at a trunk.
- Rendering (`World.cs`): new ground colours for Mud, Stony, Scrub and Crops (crops grow as tall grass cards
  in season). New meshes for hedges, fences and a timber bridge. Wood ground gets a darker floor and more undergrowth,
  so the edge of the wood shows where its effect starts.
- Siege fields stay as they are, with `SiegeWall`. The wall stays its own system and is not a Feature.
- Tests: `LayoutsScale` (the deployment zones are clear at every army size), `BridgeIsCrossable`, `HedgeBlocks`,
  and staged shots of each layout in the `IRONFIELD_SHOTS` gallery (`gallery-ground-*.png`).

### T3: sight and cover
- **Cover against arrows.** In `UpdateArrows`, an arrow that flies low through cells with cover can be stopped,
  as if it struck a tree or a hedge. The chance is `cover × the length of covered path`, from a cheap march along
  the cover grid, and it uses the seeded `rng`. A target standing in cover also has a smaller chance to be hit.
  Arrows lobbed high over the cover still drop on men in the open behind it. That way a wood is a refuge, and the
  open ground behind a hedge is not.
- **Line of sight**, per squad and not per man. Every 0.5 s (in step with `General`), each squad tests a few rays
  to each enemy squad against the heightmap (crests) and the cover grid (woods, crops, village). The result is
  `Squad.SeenBy[team]`, and it decays over a few seconds. A man inside a wood is seen only by enemies within about 25 m.
  Height lets you see further over woods and crests.
- What sight changes:
  - Archers shoot only at squads their side can see (or at a squad that is shooting at them).
  - The AI general and the sergeants plan only from what their side has seen, with a last-known position for the rest.
    A hidden company can charge from the wood: the first 4 s of contact count as an ambush (a morale shock to the
    victims, using `UpdateMorale`'s existing shock path).
  - The battle UI hides enemy squads that your side has not seen (banners at their last-known spot, faded).
    This builds on the deployment-phase hiding that `Options.Scouted` already does.
- Cost: about 40 squads a side, 4 rays each, every 0.5 s, on coarse 5 m steps, which is well under the T0 budget.
  `--bench` must show it.
- Tests: `WoodStopsArrows`, `ArcSkipsHedge`, `CrestHides`, `AmbushShock`, `SightDeterministic` (replay exact with sight on).

### T4: pathing
- A **coarse cost grid**: 5 m cells over the field, built once from the ground table (`footSpeed`/`horseSpeed`),
  Water (high cost; a ford or bridge is cheap), line features (blocked except at their gaps), and the siege wall
  (gate and ladders only).
- **Flow fields per goal**, not A* per man. When a squad gets a new anchor, a cached flow field to that cell is
  looked up or built (Dijkstra on the coarse grid, 2 classes: foot and horse). Men follow the flow until they are
  within ~12 m of their slot, and then steer straight as now. Keep about 64 fields in an LRU cache.
- A squad on the march narrows to a column through a gap (ford, bridge, hedge gap) and re-forms on the far side.
  This is a new `Formation.Column` (Squad.cs has Line, ShieldWall, Square, Wedge and Loose), used automatically on the march.
- Routing men flee along the flow away from the enemy and pile up at chokepoints. A routed army caught against a river
  is a disaster, as it should be.
- The player's own unit is not pathed. Orders drawn across water show a dotted path through the ford in the preview.
- Tests: `GoesToFord` (an order across the river ends with no man deeper than the ford), `FilesThroughGap`,
  `RoutJamsBridge`, `FlowCacheBound`.

### T5: an AI that reads the ground
- A **ground survey** at battle start, from the layout and the grids. It lists named places: crests, knolls,
  wood edges, fords, bridges, hedge lines, the village. Each place gets a score for each role
  (holding, shooting from, hiding in, charging over).
- **Deploy** uses the survey. A side that chooses its ground (T7) puts its line on the best crest or bank in its zone,
  anchors a flank on a wood or water, puts archers on a knoll or behind a hedge, and keeps horse on firm open ground.
  `Deploy` keeps today's offsets as a fallback when there is nothing to use.
- **`General`** learns three choices. *Hold good ground* means it does not charge off a crest or out of a wood edge
  against a weaker side; it waits, and lets its archers make the other side come. *Turn a flank through cover* means
  it sends the wing company or the horse round the far side of a wood. *Take the crossing* means that on a River line
  it fights for the fords and the bridge instead of wading. Horse never charges into Wood, Mud or uphill when it can avoid it.
- **Sergeants** (`Brain`, BattleDivision.cs): `Hold` and `Take` snap their point to the nearest good place in the
  survey within ~25 m. `Screen` and `Harass` use cover. `Guard` stands on a bridge or a ford. A new goal kind,
  **`Ambush`**, makes the division wait hidden in a wood until a target comes within 40 m, then charge.
  It is on the Y menu and in `AiCommand`.
- The temper of a realm (Factions.cs) colours the choice. A cautious realm holds ground, and an aggressive one
  attacks even uphill.
- Tests: `HoldsTheCrest` (an AI on a Ridge layout with equal armies does not leave the crest first),
  `HorseAvoidsWood`, `TakesTheFord`, `AmbushWaits`. The self-test idle-player battles must still end
  (see the invariants in CLAUDE.md).

### T6: tools for the player
- **Ground tooltip and overlay.** Hover the field (in command view, or with Alt in field view) to see the ground
  kind and what it does, in one short line: "Wood. Cover from arrows. Horse slowed. No formation."
  A new key, `Act.GroundView`, shades the field by ground kind and draws height contours. It is on by default
  during deployment.
- **Order preview** warns, in the voice of docs/DESIGN.md: "Horse in the wood: slowed, no charge."
  "Uphill charge." "Across deep water: by the ford."
- **Minimap**: contours, woods, water, roads, bridges and hedges, plus the last-known spots of enemies you
  cannot see (T3).
- **Field works.** Archers with the `Stakes` trait (data, in troops.json) can plant stakes in front of their line
  during deployment or when they are not under attack (an order, `Act.Stakes`). Stakes are a Feature that horse
  cannot cross. Siege engineers' pavises give cover. These make use of T2's features. Keep this small.
- **Captain perks** in the skill tree (docs/PROGRESSION.md), wired through `BattleOptions`: *Pathfinder*
  (your men lose less speed in woods and marsh), *Eye for Ground* (your side sees 25% further and sees through
  the edge of woods), *Ambusher* (it already exists on the map; it now also lets your Ambush goal hit harder).
- Tips (`Tips.cs`) for the first wood, first river, first ambush. Lines in the F1 help (`Hud.Help`).

### T7: the campaign chooses the ground
- **The field comes from the map.** `WorldMap.BattleGround` returns a layout and features from the map cell.
  A road plus a river gives a River line with a bridge. A village within a few units gives a Village layout.
  A ridge cell gives Ridge or Pass. A forest cell gives Forest clearing or Wooded flank. The season gives
  crops and snow.
- **Wet ground.** The campaign keeps a short memory of rain in each region (a saved `float` per province, decaying daily)
  and passes it as `TerrainParams.Wet`. A week of rain in the fens makes a bog, and horse should not fight there.
- **Who chooses the ground.** The side that is being attacked, or that has better scouting (`Scouted`, the Tactician
  perk, scouts in the party, a watchtower site), deploys first and may pick its zone's position on the layout
  (for example, "on the crest" or "behind the river"). The other side learns this only when it is seen.
  On the encounter panel, a new line says it: "You hold the high ground." / "They hold the ford."
- **Auto-resolve** (`Rules.AutoResolve`) takes a ground edge: a small factor from the layout and from who chose the ground,
  and a horse penalty on wet or wooded ground. `edge0` already exists for this.
- `--balance` gains columns for layout and ground, so tuning can see who wins on which field.
- Save: the new fields have defaults. Old saves load, and `CurrentVersion` is not bumped (see CLAUDE.md recipes).
- Tests: `RiverRoadGivesBridge`, `RainMakesMud`, `DefenderChoosesGround`, `AutoResolveGroundEdge`.

### T8: polish and balance
- Sound: footfalls in water and mud, branches in woods, a hollow beat on the bridge (cues through `Battle.Sounds`).
- Effects: mud spatter from horses, splashes at fords (they partly exist), leaves shaken from woods when men push through.
- Tune the ground table with `--balance` and `--bench` until each layout gives each side a fair chance to win
  when both use the ground well, and an edge to the side that uses it better.
- Document the rules for players in README.md, and the systems in docs/DEVELOPING.md ("Ground" section).
  Update CLAUDE.md's file guide and the tick-order line.

## 5. Code shape

- New files: `TerrainGround.cs` (ground layer, table, `GroundAt`, `CoverAt`), `TerrainLayouts.cs` (layouts and features),
  `BattleSight.cs` (line of sight, `SeenBy`), `BattlePath.cs` (cost grid, flow fields), `BattleSurvey.cs` (named places
  for the AI). `Terrain.cs` is 454 lines already, so the layouts must go in their own file, not into it.
- `World.cs` (812 lines) is already past the size limit. Split out `World.Ground.cs` (ground colours, the mesh)
  before adding ground kinds to it.
- All new sim code stays headless, uses only `Microsoft.Xna.Framework` math, and draws on the battle's own `rng`.
- Determinism: everything built from the seed, and nothing that depends on dictionary order or wall-clock time.
  `SelfTest.ReplayExact` runs on a River line and a Wooded flank too.

## 6. Risks

| Risk | What we do about it |
|---|---|
| Sight and pathing cost too much at 3,000 men | per squad, not per man; coarse grids; cached flow fields; `--bench` stage numbers after every phase |
| The AI sits on its hill forever and battles never end | a patience timer: after ~60 s with no fight the holding side advances or shoots; the self-test idle battles must end |
| Hidden enemies feel unfair | always show banners at last-known spots; scouts and perks reveal; the ambush shock is short |
| Woods make horse useless on Forest maps | Forest layouts always keep rides and clearings; the AI keeps horse on them |
| Replays from older builds break | `Terrain.Version` in the replay; old ones refuse with a clear message |
| Too many numbers to balance | one data table (`ground.json`), `--balance` columns per layout, change one phase at a time |

## 7. Order and size

T0 → T1 → T2 are the base, and each gives something playable: ground that matters, then fields with a shape.
T3 (sight) and T4 (pathing) are the heavy engineering, and can be built in either order. T5 needs T2-T4.
T6 and T7 can start once T2 lands. T8 is last.

Rough size: T0 small, T1 medium, T2 large (most of it is generation and art), T3 medium, T4 large, T5 large,
T6 medium, T7 medium, T8 small.

## As built

### T0 (done)
- `TerrainGround.cs`: `GroundKind` (named so, since `BattleOptions.Ground` is already the `TerrainParams` field), `GroundDef`,
  the `Grounds` table (loads `Data/ground.json`, `Validate`), and the `Terrain` partial with `GroundAt`, `CoverAt`, `GroundDefAt`.
  The layer is one byte per vertex cell (nearest vertex). `PaintGround` marks water and fords, then the road (within 2.5 m),
  then woods (the circles `PlaceFoliage` grows them in, now kept in `woods`). Everything else is Grass. Flat test fields and
  the campaign map have no layer and answer Grass.
- `Data/ground.json`: all thirteen kinds, neutral (multipliers 1, cover and stumble 0). Nothing in the sim reads it yet.
- `Terrain.Version` = 1, recorded in `Replay.TerrainVersion` (`Replay.CurrentVersion` 2). `--replay` refuses a replay made on
  other ground, with a message. Old replays read 0 and are refused.
- `--bench` first prints the terrain build time.
- Test `GroundLayer`: the data validates, the same seed gives the same ground (FNV hash, `Terrain.GroundHash`), and rivers,
  fords, the road and woods are marked.
- A note for T2: the battlefield road can cross the river with no bridge (the road cells there are Water). The River line
  layout must put a bridge where they cross.
- Baseline (`--bench 1500`, cloud container): terrain built in 63 ms; 3,013 men at 4.47 ms a tick mean, 6.52 p95
  (think 1.54, move 1.32, morale 1.17).

### T1 (done)
- Pace: `Move` scales each man's velocity (not only his step) by `SlopeFactor` and the ground's foot or horse speed, so
  charges, impacts and arrow leads see the real speed. The result is kept as `Unit.GroundPace`, with the signed slope in
  `Unit.Slope`. A horse's slope factor is ×2 a unit of rise (clamped 0.5-1.2); a man's is unchanged. `Wading` is gone:
  Ford and Water in `ground.json` carry its numbers.
- Fatigue: `Tire` measures effort before the ground slows a man (`Vel / (Speed × GroundPace)`), so slogging through mud at a
  run is running. `Strain` adds the ground's `fatigue` and climbing (×5 the rise), both × (1 + armour past 8 / 20). Level grass adds nothing.
- Order: `InOrder` (ground order ≥ 0.6) gates bracing (it replaces the water-depth check) and the shield wall's block bonus.
  The looser slot spacing on bad ground is **not built** yet. It moves to T2, where squads on mixed ground can be looked at.
- Charge: `Impact` and couched lances scale by the ground's `charge` where the blow lands. `Stumble`: a horse at 70% of gallop or
  more may check to 40% speed and raise its lance (seeded `rng`, only drawn on ground with a stumble chance).
- Reach: `ReachOf`: a weapon longer than 2 m loses `(reach - 2) × (1 - ground reach)` (pikes in a wood), for the AI's reach,
  the player's swings and braced points.
- `Battle.HighGround(above, slope, speed)` = 0.15·tanh(above / 3), plus up to +0.1 running (over 3 m/s) downhill.
  `Battle.HeightRange(above)` = 1 + above / 24, capped at +25%.
- AI fix found on the way: archers with nobody left to hold a line shoot what they carry and charge, with no wagon trips
  (a level-3 self-test battle stalled for ten minutes on this).
- Tests: `GroundMoves` (wood vs horse and foot, road, mud fatigue, horse on a slope), `GroundFights` (no bracing in a wood:
  the horse loses 17 hp, against 140 on grass; seeded stumbles; the high-ground and range curves).
- Bench after T1: 3,013 men at 4.6 ms a tick mean (think 1.62, move 1.38), the same as the T0 baseline within noise.

### T2a (done): the layout framework
- `TerrainLayouts.cs`: `FieldLayout` { Open, Ridge, TwoHills, Broken, Fen }. `TerrainParams.Layouts` lists the shapes a
  preset may take (Meadow: open, open, ridge; Hills: ridge, two hills; Badlands: broken, broken, two hills; Marsh: fen;
  the rest open). The layout is drawn from its own stream (`seed + 17`, features from `seed + 18`), so hills, woods and
  trees stay the same for a seed, and an open field at Scale 1 is the same map as before.
- Ridge: a crest 24-30 m in front of one army (`RidgeSide`, `RidgeZ`), 4-6.5 m high (more on Hills), steeper toward the enemy
  and gentle behind. Two hills: a knoll on each wing between the lines. Broken: four steep knolls; Stony on slopes over 0.22,
  scrub patches. Fen: Mud over about a third of the field and along the stream.
- Every field: thick wood (DenseWood) in the heart of woods over 18 m across, scrub in patches round wood edges, Stony on
  slopes over 0.45. Water and the road are never painted over.
- `TerrainParams.Scale` (from `Battle.FieldScale`, the same number `MapRadius` uses / 95): the clear deployment ground (`Core`,
  now an instance method), the flank woods and the spread of the hills grow with the battle. 1 up to 450 men, 2.4 at 2,600.
- `World.GroundColor` colours each kind (mud dark brown, stones grey, scrub olive, the forest floor darker, and so on), and so does the minimap.
- `IRONFIELD_GROUND=Hills[,river]` and `IRONFIELD_LAYOUT=Ridge` pick a quick battle's field (for shots and testing).
- `Terrain.Version` 2.
- Auto-resolve now disagrees with the sim more often on ridges (8/12 instead of 9/12 on mixed Meadow fields).
  `Calibration` runs on open ground until T7 gives auto-resolve its ground edge.
- Test `Layouts`: clear ground at scale 1 and 2.4 for five presets; the ridge, the two hills, stony broken ground,
  a muddy fen, thick wood in big woods.
- Still to come in T2: line features (hedges, fences, a bridge where the road crosses the river), the Village, Hedgerows,
  Wooded flank, Forest clearing, River line and Pass layouts, crops in season, and looser ranks on bad ground (from T1).

