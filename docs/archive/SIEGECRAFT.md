# Siegecraft: engines of war

Plan for siege and field artillery. Status as of 2026-11-16: **A1-A6 done** (A6 not yet seen on a screen: only built and tested headless).
Technology goes as far as the counterweight trebuchet; there is no gunpowder.

## A1 Engines in the battle sim (done)

- **Data:** `Data/engines.json` (`EngineDef`, registry `Engines.cs`, checked by `Engines.Validate`): scorpion and springald
  (bolts), mangonel and trebuchet (stones). Fields: range and least range, damage, armour pierce, men a bolt runs through
  (`pierce`), stone `splash`, reload with a full crew, crew, hp, shot speed, spread per metre, morale `shock`, `wallDamage`.
- **On the field:** `BattleEngines.cs`. `Battle.AddEngine(team, id, at, front, crew)` sets one up worked by a company
  (`Squad.Works`); the crew forms up behind it and holds, and the AI general (field and siege) leaves it alone.
  `UpdateEngines` runs after `UpdateArrows` each tick.
  - Crew within `CrewReach` (5 m) wind it, at a rate of crew present / crew needed; none and it stands silent.
  - Foes within `WreckReach` (2.5 m) hack at it; a stone landing next to an enemy engine hits it too. At 0 hp it is wrecked
    and the crew becomes a company again.
  - Aim (`EngineAim`): a closed gate first, for an attacking engine that breaks walls (`wallDamage` 20 or more). If not, the company
    the crew is told to target (`Squad.Target`). If not, the best visible company within range: the nearest for bolts, the
    thickest body of foot for stones. It leads the mark by its pace and is scattered by distance x spread x weather.
  - **Bolts** fly flat and run through up to `pierce` foes, losing a quarter of their force with each. They never hit their own side.
  - **Stones** fall on everyone within `splash`, their own side too, harder near the middle. They throw men down, shake men within
    twice the splash, and take `wallDamage` off a gate they land on (`GateGivesWay`, shared with `Batter`).
  - Kills go to the crewman who loosed (`Kill` credits only foes); `Engine.Shots`, `Kills`, `GateDamage` count for reports.
- **Screen:** a stone's landing flashes `"stone"` (earth spray, a dent, camera kick in `BattleScreen.Magic.cs`). The engines
  themselves and shots in flight (`Battle.Engines`, `Battle.EngineShots`) are not drawn yet (A6).
- **Tests** (`Tests/EngineTests.cs`): `EnginesValidate`, `EngineShoots`, `EngineNeedsCrew`, `StoneSplashes`, `EngineBreaksGate`.
- **Not yet:** engines only come from `AddEngine`. Nothing in the campaign or deployment makes them (A3/A4), so replays do not
  record them yet. Once `BattleOptions` carries engines, set them up in `Deploy` so a replay rebuilds them.

## A2 Walls that break (done)

- **Sections:** `SiegeWall` (Terrain.cs) splits the face from x -100 to 100 into 20 sections of 10 m (`SectionHp`, `SectionAt`,
  `SectionX`). The two by the gatehouse are not sections. Each starts with `BattleOptions.WallHp` (0: half again the gate's);
  the campaign sets 900 for a town and 700 for a castle, plus 250 for each level of walls (`PrepareAssault`).
  A section at 0 hp is a **breach** (`Breached`, `AtBreach`, `Breaches`), 8 m wide in the middle of the section.
  The bank behind it stands, so the ground does not change: men on foot of either side climb the rubble, and riders cannot.
- **Ways in:** `Via` and `MayCross` (BattleSiege.cs) take the gate, ladders, tower ramps (`SiegeWall.Ramps`, attackers on foot)
  and breaches (anyone on foot). Once a breach or a ramp exists, a closed gate's cost in `Via` grows with its hp
  (25 + 0.1 x hp), so men leave a strong gate for the new way in.
- **Stones:** a stone landing within 2.5 m of the face hits the gate or the section under it (`Land`, `Engine.WallDamage`).
  An attacking stone engine aims at the section in front of it if it is a great engine (`wallDamage` 100 or more, the trebuchet)
  or the gate is down. A lesser engine aims at a closed gate. Once there is a breach, stones turn back to men.
- **A breach** (`WallBreached`): log, crash and horn, defenders lose 6 morale, and the nearest defending foot company takes a post
  behind the gap (`SiegeDefend` sends it at whoever comes through).
- **Rolling engines** (`Engine.Goal`, `Roll`): the `ram` (100 gate hp a second with a full crew) rolls to the gate, and the `tower`
  rolls to the face in its lane and docks (`Engine.Docked`, adds a ramp). Their crews push at a pace set by how many are there.
  Men of their side within 3 m take a quarter of arrow damage (`Sheltered`, in `Hit`). A wrecked tower takes its ramp away.
- **Defenders' engines:** `BattleOptions.WallEngines` springalds on the walkway (`MountWallEngines`, from `DeployDefenders`),
  one for each level of walls, each worked by townsmen (levies, no campaign company, so no losses on the roll).
- **Screen:** `Battle.WallVersion` counts wall changes. The battle screen rebuilds the foliage meshes when it moves, which replaces
  the old `GateBroke` flag that a multi-step `Advance` could miss. A breached section is drawn as rubble (`World.Item`).
  The HUD gate bar says THE WALL IS BREACHED.
- **Tests** (`Tests/WallTests.cs`): `SectionsAndBreaches`, `TrebuchetBreaches`, `RamAtGate`, `TowerDocks`, `WallEnginesShoot`.
- **Not yet:** the ram and tower are invisible (A6). Burning oil at the gate is left for later. Attackers only get engines through
  `AddEngine`, which comes in A3.

## A3 The siege camp (done)

- **Works** (`Campaign/SiegeWorks.cs`): siege works are the engines in `Data/engines.json` with a `build` time (mangonel 24 h, trebuchet
  48 h, ram 12 h, tower 36 h) and a `cost` in crowns. `BuildWork` pays and queues one at your siege; each starts when the one
  before it is done, and takes half the time with Siegecraft. The camp holds at most `MaxWorks` (4).
  Works live on the party (`Party.SiegeWorks`, `WorksAt`) and count only while it still besieges that place (`WorksOf`,
  `ReadyWorks`), so lifting a siege or ending an assault abandons them.
- **Battering** (`BatterDaily`, at the start of `SiegesDaily`): each day the ready stone engines of every camp at a place raise
  `Settlement.Battered` (mangonel 0.08, trebuchet 0.2, divided by 1 + 0.5 x walls level, at most 1). A place no longer besieged
  mends 0.1 a day. A wall battered to 1 is breached.
- **Lords** queue a mangonel when they sit down to a siege (and a trebuchet with 210 men or more, `Scaled(70)`), free.
- **Assault:** `PrepareAssault` scales `WallHp` by 1 - 0.7 x battered, sets `Breaches` (1 for a breached wall: the section at
  x -35 starts down), and adds your ready works and train to `BattleOptions.Engines`. Stone engines stand before the wall at
  0.7 of their range (at most 100 m), the ram before the gate, and towers at x ±55 (`DeployEngines`).
- **On paper:** `WallFactor` replaces the fixed wall factors in `AutoResolveAssault` and in the lords' assaults. It starts at
  1.4 with ladders (1.7 without) + 0.1 x walls, then subtracts 0.3 x battered, 0.08 for a ram, 0.08 for each tower (up to 2)
  and 0.04 for each stone engine (up to 3). It never goes below 1.05.
- **Siege panel** (`MapScreen.Dialogs.cs` `SiegeWorksRows`): how battered the walls are, the camp's works and when each is ready,
  and a BUILD row for each work.
- **Tests:** `SiegeCamp`.

## A4 Field artillery (done)

- **The train:** `Party.Train` holds the light engines bought in a town's WORKSHOP tab (`BuyEngine`, `SellEngine` for half back;
  scorpion 400, springald 700; at most `MaxTrain` 4). Each slows the march by `TrainDrag` (4%), and a lost battle loses
  the whole train (`TrainLost` in `FinishBattle`). A lord brings a scorpion from 120 men and a springald too from 210
  (`TrainOf`, worked out from his size, not stored).
- **In battle:** `EnginesFor` puts every party's train into `BattleOptions.Engines` (an ally's is marked `Ally`).
  `DeployEngines` (BattleEngines.cs, after `BuildDivisions`) sets up each one 4 m behind its side's rear line. Its crew is split
  off (`SplitNow` with a count) from the lowest-tier foot company that can spare them, preferring men without missiles:
  yours for your engines, an ally's for his. The crew reports with that company and leaves its division. In a field battle
  only bolt-throwers come; siege works stay in camp. Replays rebuild engines from the options.
- **Horse:** horse with no target rides for an enemy engine's crew it can see within 90 m (`EnginePrey`, in `General`).
- **On paper:** `TrainEdge` scales your auto-resolve edge by 1 + 0.03 per engine on each side (up to 4).
- **Tests:** `FieldEngines` (crews, placement, shooting, replay), `HorseRidesForEngines`, `CompanyTrain`.
- **Balance check:** `--realms 4` before and after: the largest share is 30% both times, with no flags. `MagesInTheWorld` now sums
  circles over 22 weeks of two worlds: in seed 2001 alone the changed war left the arcana-3 realms with no tier-IV lords.
- **Not done:** the plan's engineer troop line. Crews come from your own foot companies instead, which keeps the troop data
  (287 troops, their sprites and trees) as it is. Run `--agree` and `--grounds` once engines are drawn and tuned.

## A5 The player (done)

- **Command view:** pick an engine's crew (its company) and right-click. An enemy company becomes the mark (`Squad.Target`); a spot of
  ground, or a place within 8 m of the wall face (snapped to the face, so stones go at the wall), becomes a standing mark
  (`Engine.Mark`, `Battle.AimEngine`). A spot out of reach (range, or inside the least range) is refused and says so. Hold position
  clears the mark. `EngineAim` shoots at the mark first, then the chosen company, then its own choice. Orders reach the crew through
  `Send`, like any other. An engine's crew take no other orders: `Targets()` leaves out `Works` companies.
- **Field view:** stand within `ManReach` (3.5 m) of one of your own non-rolling engines and press the use key (`Battle.Man`). The crew
  keep winding but the engine holds fire. Attack looses it where the crosshair meets the ground (`PlayerInput.EngineAim`, held to its range,
  refused inside the least range), with you as the shooter; you count as one more crewman. The use key again, command view, mounting, a
  wrecked engine or walking 4.5 m away lets go.
- **HUD:** a reload bar over each of your engines (gold when ready); the ones you picked or work show their reach and least range as dashed
  rings, the mark or company as a line and a ring, and for one you work the landing ring and the arc the shot would fly. A prompt and a
  reload bar under the crosshair when you stand by or work one (`Hud.cs`).
- **Tests:** `EngineOrders`, `MannedEngine`.

## A6 Looks and sound (done)

- **Meshes** (`BattleScreen.Engines.cs`, `EngineLines` from `DrawLines`): each engine is built in code from timbers (camera-facing ribbons through
  `BeamQuad`, never thinner than a few pixels). Scorpion and springald: cart, stock, bow arms and a string that is drawn back and loaded.
  Mangonel and trebuchet: frame, arm on an axle (cocked back, flung in 0.4 s, wound back over the reload: `ArmAngle`), sling and stone,
  the trebuchet's counterweight. Ram: hide roof, a trunk that swings at the gate. Tower: clad timber frame, a bridge raised until it docks.
  Wrecks are heaps of timber. Hides and pennants take the crew's side colour.
- **Shots in flight:** bolts as a timber with an iron head and a streak, stones as a dark ball with a streak.
- **Sound** (`AudioSynth.Combat.cs`): `EngineWinch` (ratchet and creaking rope, once a winding is under way), `EngineRelease` (the arm's slap
  and a deep thump: stone engines and springalds; the scorpion keeps its snap) and `WallFall` (a breach or the gate giving way).
- **Not yet:** looked at on a real screen (tune sizes and colours); burning oil at the gate; the engineer troop line.
