# Developer guide

A detailed walk through the Ironfield code: how each system works, what its data looks like, and how to change it safely.

- **New here?** Read sections 1 to 3, then the section for the area you are changing.
- **Short on time?** `CLAUDE.md` is the one-page map and `docs/CODEMAP.md` lists every method with its line number.
- **Player-facing rules** (what the game does) are in `README.md`; the campaign design is in `docs/CAMPAIGN.md`.

Contents

1. [Quick start](#1-quick-start)
2. [Rules that must not be broken](#2-rules-that-must-not-be-broken)
3. [Project layout](#3-project-layout)
4. [The battle simulation](#4-the-battle-simulation)
5. [The campaign simulation](#5-the-campaign-simulation)
6. [Screens and interface](#6-screens-and-interface)
7. [Rendering and sound](#7-rendering-and-sound)
8. [Data files](#8-data-files)
9. [Testing](#9-testing)
10. [Worked examples](#10-worked-examples)
11. [Troubleshooting](#11-troubleshooting)
12. [Glossary](#12-glossary)

---

## 1. Quick start

```bash
dotnet build -c Release            # build (~30 s cold). CS0649 warnings on JSON classes are expected.
tools/check.sh                     # build + rule checks + full self-test. Must end "ALL CHECKS PASSED".
tools/check.sh --quick             # the same without the self-test, while you iterate
dotnet run -c Release --no-build -- --selftest Parry   # run one test by its method name
dotnet run -c Release --no-build   # play (needs a display)
```

To see the game without a display (works in a cloud container):

```bash
IRONFIELD_SAVES=/tmp/ifs IRONFIELD_SHOTS=/tmp/shots xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
IRONFIELD_SAVES=/tmp/ifs IRONFIELD_CAMPAIGN_SHOTS=/tmp/cshots xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
```

The first saves staged battle screenshots (`field-view.png`, `command-overview.png`, ...); the second saves every campaign
screen (`campaign-town.png`, `campaign-realm.png`, ...). Both quit when done. "No audio" errors from ALSA/OpenAL are harmless.
Always set `IRONFIELD_SAVES` so you never touch real save files.

**The change loop:** find the code (section 3 / `docs/CODEMAP.md`) -> make the smallest change -> add a test if you added
behaviour -> `tools/check.sh` -> if you added/moved methods or files, `git add` them and run `tools/codemap.sh` -> commit.

---

## 2. Rules that must not be broken

`tools/check.sh` enforces the first three automatically. The rest fail quietly, so read them.

| # | Rule | Why | Enforced by |
|---|---|---|---|
| 1 | Headless code (the battle and campaign sims, `Tests/`, `SelfTest.cs`, `Replay.cs`, `BalanceReport.cs`) never uses MonoGame graphics, audio or input. Only math types (`Vector2`, `MathHelper`...) from `Microsoft.Xna.Framework`. | `--selftest` and CI run with no window. | `tools/check.sh` |
| 2 | The battle sim uses only its own seeded `rng`. Never `Random.Shared`, `new Random()` without a seed, `DateTime`, `Stopwatch`. | Replays must reproduce a battle exactly. | `tools/check.sh` + `ReplayExact` test |
| 3 | `docs/CODEMAP.md` is regenerated after adding, renaming or moving methods/files. | Other sessions navigate by it. | `tools/check.sh` |
| 4 | Every value in `Balance` (`Campaign/CampaignState.cs`) has the same default as in `Data/campaign.json`. | The file wins at runtime; the default is what readers believe. | `BalanceMatchesData` test |
| 5 | Ground positions are `Vector2(X = world X, Y = world Z)`. Height comes from `Terrain.Height(pos)`. | Mixing Y and Z puts things in the sky. | - |
| 6 | Team 0 is the player's side, team 1 the enemy. Palette 3 is bandits. | Hard-coded in many places. | - |
| 7 | `AttackDir` is named from the attacker's side; a defender must block `Dirs.Mirror(swing)`. | Left/right flip between the two men. | Parry tests |
| 8 | Campaign faction names come from the instance `campaign.FactionNames[f]` and `Factions.Get(f)`, never a static list. | Realms founded in play get ids past `Data/factions.json`. | - |
| 9 | The career rank is derived (`Campaign.Rank`), never stored. | It is read from the world's state. | - |
| 10 | MonoGame `Color * float` also scales alpha. Use `Color.Lerp` for tints. | Tints turn transparent. | - |
| 11 | User-facing text uses British spelling ("armour", "colour") and short plain sentences; UI text is UPPER CASE. | House style (`docs/DESIGN.md` "Voice"). | - |
| 12 | Never skip, weaken or delete a test to get green. If a sim change moves the printed numbers, that is fine; if a `Check` fails, fix the cause. | Tests are the only safety net. | review |

---

## 3. Project layout

One project, `Ironfield.csproj`, namespace `Ironfield`, file-scoped namespaces. Big classes are `partial` and split across
files by area. All `*.cs` files in the folder tree are compiled (SDK globbing), so a new file needs no project edit.

```
Program.cs              command-line switches (--selftest, --balance, --pacing, --replay, --bench, --write-controls), else start the game
ScreenHost.cs           the MonoGame Game: owns art, audio, settings, controls; switches Title / Map / Battle

── Battle simulation (headless) ── class Battle, partial ──────────────────────────────────────────────
Battle.cs               setup, deployment, the tick (Update), squads, reinforcements, Result, CheckEnd
BattleAI.cs             Think (infantry/archers), ThinkCav (cavalry), steering helpers, General (the AI commander)
BattleCombat.cs         TickAction, Strike, Hit, Damage, HitHorse, Kill, Shoot, UpdateArrows, Braces, Lances
BattleMove.cs           Move (steering, separation, terrain, wading, slope), Impact (collisions)
BattleMorale.cs         UpdateMorale (wavering, routing, rallying)
BattlePlayer.cs         UpdatePlayer (your own unit), ToggleMount, Rally
BattleCaptain.cs        captain moves (bash, weapon swap, calling a horse), fatigue, supply wagons, AI retreat
BattleSiege.cs          assaults: defenders on the wall, ladders and gate routing (Via), battering
BattleGrid.cs           spatial grid for neighbour queries (Near, Separate, NearestFar, NearestRouter)
BattleDivision.cs       divisions: Goal, Temper, IssueGoal and OrderDelay, Direct, the sergeant's Brain, AiCommand
BattleCourier.cs        riders carrying orders to divisions; cut down by enemies on the road
BattleTypes.cs          BattleOptions, Weather, BattleResult, CompanyResult, StackResult
Squad.cs                a company on the field: Order, Formation, slots
Units.cs                UnitType (a troop), Unit (one soldier), Mount, Look, enums
Troops.cs               loads Data/troops.json; Company / ArmySpec / DivisionSpec records; Armies (presets, Large, Divide)
Terrain.cs              heightmap, roads, rivers, foliage placement, siege wall geometry
Replay.cs               record and replay battles exactly

── Campaign simulation (headless) ── class Campaign, partial, in Campaign/ ────────────────────────────
Campaign.cs             construction, New(seed), relations, Tick, movement, encounters, Hourly/Daily/Weekly
CampaignBattle.cs       PrepareBattle, FinishBattle, AutoResolveEncounter, Retreat, Parley, Apply (results -> state)
CampaignActions.cs      your actions: recruit, hire, horses, gear, merge/split, contracts, rest
CampaignAI.cs           spawning lords and bandits, LordThink, BanditThink, refits, FactionAI
CampaignState.cs        ALL save data (plain classes), save/load, Rng, Balance (<- Data/campaign.json), Names
CampaignWar.cs          prisoners, officers, food and seasons, contracts board
CampaignSiege.cs        garrisons, besieging, assaults, capture
CampaignProgression.cs  "what to do next" hints
Career.cs               ranks, fealty, the realm council, founding a realm, winning and losing
Characters.cs           lords, companions, wanderers, opinion and memories
Companies.cs            troop stacks, promotion, honours, ledgers; Scaled(n) (army scale)
Divisions.cs            your divisions: make, assign, goals, leader temper and command, WithDivisions (into battle)
Diplomacy.cs            war exhaustion, peace terms, alliances, tribute, hosts, summons
Events.cs               events from Data/events.json, lord quests, brigand kings, pretenders
Fiefs.cs                prosperity, unrest, taxes, buildings, revolts, vassals, defection
Skills.cs               the captain's skill tree and perks
Rules.cs                static company math, Strength, AutoResolve, Army (campaign -> battle armies)
WorldMap.cs             world generation: provinces, rivers, roads, settlements
Pilot.cs                a scripted stand-in captain for headless pacing runs

── Screens (MonoGame) ─────────────────────────────────────────────────────────────────────────────────
TitleScreen.cs, SettingsScreen.cs, Tips.cs
MapScreen.cs            campaign map: 3D world, parties, camera, input, panel switch
MapScreen.Hud.cs        always-on map interface, tooltips, Modal(), Note()
MapScreen.Settlement.cs settlement panel, one method per tab (RecruitTab, MarketTab, ... FiefTab)
MapScreen.Party.cs      party, characters, skills, upgrade and divisions (V) panels
MapScreen.Realm.cs      realm panel (R): RankLadder, RealmColumn, KingDiplomacy; Vassals
MapScreen.Dialogs.cs    encounter, siege, results, event, beat, game over, menu panels
BattleScreen.cs         battle screen: setup, per-frame update, sounds, HUD sync
BattleScreen.Controls.cs field-view and command-view input, picking, camera
BattleScreen.Orders.cs  squad orders and drawn lines
BattleScreen.Draw.cs    render passes, sprites, particles, rain, order lines
BattleScreen.Shots.cs   staged screenshots (IRONFIELD_SHOTS)
Hud.cs                  battle 2D interface: layout, drawing and hit-testing
Hud.Divisions.cs        division cards, goal buttons, goal wheel, goal lines, riders, sergeant pennants
Ui.cs                   immediate-mode panel kit used by the campaign screens
Controls.cs             rebindable actions (Act enum), defaults, controls.json

── Rendering and sound ────────────────────────────────────────────────────────────────────────────────
World.cs (3D meshes), Shading.cs + GlslEffect.cs + Shaders/*.glsl (shader pipeline), Sky.cs (light by hour/weather),
Puppet.cs (units as part-sprite puppets), SpriteSheet.cs + Painter.cs (paint the sprite atlas in code), Art.cs (atlas upload),
PixelFont.cs (the HUD font), Style.cs (colours, heraldry, UI tokens), Verts.cs (growable vertex array), Audio.cs (synthesised sound)

── Tests and tools ────────────────────────────────────────────────────────────────────────────────────
SelfTest.cs             test runner (Run, RunOne), shared helpers, Check
Tests/BattleTests.cs    battle mechanics, troops, sprites, full battles, replays
Tests/CampaignTests.cs  map, saves, economy, sieges, war, stacks, cultures, characters, skills, balance data
Tests/CareerTests.cs    pilot, ranks, fiefs, diplomacy, events
BalanceReport.cs        --balance and --pacing reports; GpuReport.cs: --gpu-report; Bench.cs: --bench
tools/check.sh          the pre-commit check; tools/codemap.sh regenerates docs/CODEMAP.md
Data/*.json             troops, factions, campaign balance, events, names
```

---

## 4. The battle simulation

### 4.1 Data model

| Type | File | What it is |
|---|---|---|
| `UnitType` | `Units.cs` | A troop from `Data/troops.json`: `Class` (Infantry, Archer, Skirmisher, Captain, Cavalry), `Tier`, `MaxHp`, `Damage`, `Armor`, `Reach`, `Windup`, `Recover`, `Speed`, `BlockChance`, `Range`, `Ammo`, `VsMounted`, `Pierce`, `Mount`, `Look`, `UpgradesTo`. Shared, never mutated in battle. Get one with `Troops.Get("shieldmen")`. |
| `Unit` | `Units.cs` | One soldier: `Type`, `Team`, `Squad`, `Pos`/`Vel`/`Wish` (ground plane), `Slot` (where formation wants him), `Hp`, `Morale` (starts 70), `Stamina`, `Action` (None, Windup, Recover, Block, Stagger), `Swing` (an `AttackDir`), `Routing`, `Fled`, `Dead`, `Active`. Riders also have `Horse`, `Speed`, `Heading`, `Couched`, `Cav` (a `CavState`). |
| `Squad` | `Squad.cs` | A company on the field: `Units`, `Order` (Hold, Follow, Charge), `Target` (a squad to charge, or null for nearest), `Anchor` (centre of the front rank), `Front`, `Columns`, `Formation` (Line, ShieldWall, Square, Wedge, Loose), `FireAtWill`, `Pressure` (recent flank hits), `Kills`, `Company` (the campaign link). `Slot(i, n)` gives a man's place. |
| `Company` / `ArmySpec` | `Troops.cs` | What the campaign sends in: `ArmySpec(Name, Level, Companies)`; each `Company(Troop, Count, Role, Id, Name, Morale, Traits, Allied, Mix, ...)`. `Mix` lists stacks when a company holds several troop types. |
| `BattleOptions` | `BattleTypes.cs` | Everything that is not the armies: `Cap` (most men on the field at once, default 400), perks (`IronWill`), `AutoPlayer` (AI also commands your side), `Ground` (terrain), `Palette`, `EnemyDamage` (difficulty), `Siege`, `Ladders`, `GateHp`, `Weather`. |
| `Weather` | `BattleTypes.cs` | `Hour`, `Rain`, `Fog`, `Snow`; derived `Spread` (arrow scatter), `MudFactor` (horse speed), `Vision` (archer range limit). |
| `BattleResult` | `BattleTypes.cs` | What goes back: `Winner` (0 you, 1 enemy), `Duration`, one `CompanyResult` per company (`Standing`, `Fled`, `Fallen`, `Kills`, `KillsByTier`, per-stack `Stacks`), the captain's and enemy leader's `LeaderFate`, `HorsesTaken`. |

### 4.2 Building a battle

- `new Battle(seed, playerArmy, enemyArmy, options)` is a normal battle. `Battle.Empty(seed)` is flat ground with just the
  two leaders: tests build scenarios on it with `AddSquad(team, type, n, anchor, columns, front)` and `Spawn(...)`.
- `Deploy(team, army, budget)`: the biggest companies take the field until the budget (half of `Options.Cap`) is used; the rest
  wait in `reserves`. Companies are grouped by role (`Armies.RoleOf`: infantry, spears, archers, horse) and placed by role.
  In a siege, team 1 goes on the wall (`DeployDefenders`).
- `Reinforce()` (every second) brings a reserve company on when the field has thinned to 60% of what started.

### 4.3 The tick: `Battle.Update(dt)`

Tests run it at a fixed `1/60` s. Each step, with the file it lives in:

| Step | Method | File |
|---|---|---|
| delayed direct orders to division companies that are due | `ApplyDirects` | `BattleDivision.cs` |
| record the frame for replays | `RecordFrame` | `Replay.cs` |
| rebuild the neighbour grid | `BuildGrid` | `BattleGrid.cs` |
| team centroids and squad bounding circles | `UpdateCentroids` | `Battle.cs` |
| AI generals, every 0.5 s (team 1 always; team 0 when `AutoPlayer` or it has AI squads) | `General` | `BattleAI.cs` |
| couriers, goals that arrive, the AI's goals, each division's brain | `UpdateDivisions` | `BattleDivision.cs`, `BattleCourier.cs` |
| siege gate battering | `Batter` | `BattleSiege.cs` |
| reinforcements, every 1 s | `Reinforce` | `Battle.cs` |
| called horse, supply wagons | `UpdateCalledHorse`, `Resupply` | `BattleCaptain.cs` |
| per squad: pressure decay, alive count, slots | `UpdateSquad` | `Battle.cs` |
| per unit: decide (`UpdatePlayer` for you, `Think` for others), act, tire | `UpdatePlayer` / `Think` / `TickAction` / `Tire` | `BattlePlayer.cs` / `BattleAI.cs` / `BattleCombat.cs` / `BattleCaptain.cs` |
| move everyone | `Move` | `BattleMove.cs` |
| braced spears vs riders, couched lances | `Braces`, `Lances` | `BattleCombat.cs` |
| arrows in flight | `UpdateArrows` | `BattleCombat.cs` |
| morale | `UpdateMorale` | `BattleMorale.cs` |
| has a side lost? | `CheckEnd` (sets `Winner`) | `Battle.cs` |

**Staggered thinking (scale):** `ThinkNow(u)` lets a man within 6 m of an enemy, mid-action or routing think every tick,
one within 25 m (and every rider) every 2nd tick, and the rest every 4th, offset by `u.Index` so the work spreads evenly.
Morale runs every 2nd tick with `dt * 2`. Both are keyed on the tick counter, so replays stay exact. `--bench N` prints
ms per tick for each stage (through the `Probe` callback; the timing lives in `Bench.cs` because the sim may not read a clock).

Units do not move themselves in `Think`: they set `u.Wish` (desired velocity). `Move` turns wishes into motion with
separation, terrain and walls. Keep that split when adding behaviour.

### 4.4 Unit AI (`BattleAI.cs`)

`Think(u, dt)`:
1. A routing man runs away from the enemy centroid; past `MapRadius` he has fled.
2. A man mid-swing stands still (a rider keeps riding).
3. Find the nearest enemy (`Nearest`). Riders go to `ThinkCav`.
4. Foot: archers shoot from formation at the squad's target; otherwise hold the slot, or close and fight when charging.

`ThinkCav(u, s, enemy, d, dt)` is a small state machine on `u.Cav`:
- passing strikes at anything in reach; horse archers shoot from the saddle and skirmish when charging;
- not charging: hold the slot, but counter-charge anything within 15 m;
- `Charge`: aim 6 m past the target to ride through, couch the lance at speed; once through, `Withdraw`;
- `Withdraw`: ride away from the enemy mass for 4 s, then `Charge` again;
- `Melee`: bogged down (slow and close for 1.2 s) -> stand and hack for 5 s, then withdraw.

### 4.5 The AI general (`General(team)` in `BattleAI.cs`)

Runs every 0.5 s. In order: sound the retreat if beaten (`SoundRetreat`); sieges use `SiegeDefend`/`SiegeAttack` instead.
Otherwise:
- keep the smallest of three or more infantry companies back as a reserve, committing it 18 s after the charge or when a line
  company falls under half strength;
- form the infantry line facing the enemy; the outermost company swings wide for a flank; spears go wide on one flank;
- charge when any infantry company is within 16 m of the enemy (or spears within 10 m);
- archers advance into range, shoot the softest company (`SoftTarget`), resupply from the wagon (`ArcherSupply`), and join the
  melee when the enemy is within 7 m or arrows are gone;
- horse waits on the other flank, then charges when the line does, after 35 s, or when the enemy is within 20 m, aiming
  at archers first, then spears, infantry, horse;
- the enemy captain leads from behind his line and joins the fight 12 s after the charge.

### 4.6 The damage path (`BattleCombat.cs`)

`TickAction` runs a swing: `Windup` -> `Strike` -> `Recover`. `Strike` finds targets in an arc (`InArc`) and calls
`Hit(attacker, target, dmg, ranged, lance)`. `Hit`, in order:

1. Enemy blows are scaled by `Options.EnemyDamage`. Archers shooting up at a wall lose 40%.
2. A mounted target: arrows and skirmishers hit the horse 55% of the time, others 18% -> `HitHorse`.
3. Side of the hit: `SideOf` gives Front, Flank or Rear. A square has no flank. Loose ranks let 30% of arrows through.
4. **You, blocking:** a block in the mirrored direction within `ParryWindow` (0.35 s) is a parry (attacker staggers). Without a
   shield, a block from the wrong side halves the blow. With a shield, any block works at a stamina cost; at 0 stamina the guard breaks.
5. **AI, facing:** a block roll on `BlockChance`, adjusted for shield, swing direction, lance, arrows, shield wall.
6. **Chambering:** you striking into the enemy blow from the mirrored side turns it aside.
7. Multipliers: speed bonus (charges), high ground (+-15%), fatigue, officer traits, square vs arrows (+25%), wedge on charge
   (+15%), rear (x1.5) / flank (x1.2), `VsMounted` against riders. Flank and rear hits add `Squad.Pressure`.
8. `Damage` applies armour, then hp, morale shock and interrupts; at 0 hp, `Kill`.

### 4.7 Morale (`BattleMorale.cs`)

Every unit except leaders, each tick: near a living leader (`Options.AuraRadius`, default 12 m) morale rises; a living
general helps everyone a little. Squads under 40% strength lose morale; men out of contact recover. Routing men recover
once 26 m clear of the enemy (a man who breaks twice keeps running). Constants `RoutAt = 12` and `RallyAt = 32` are in `Battle.cs`.

### 4.8 End of battle

`CheckEnd` sets `Winner` when a side has no active, non-routing men (and no reserves that could still come). `Withdraw()` is
the player giving up: a loss, your men count as fled. `Result()` builds the `BattleResult`; companies still in reserve
come back as `Deployed: false` with no losses.

### 4.9 Divisions (`BattleDivision.cs`, `BattleCourier.cs`)

A `Division` groups squads of one team under a leader (`Unit.Sergeant`), with a `Temper` (Bold, Steady, Cautious), a
`Skill` (1-10) and a `Goal` (`GoalKind` + point / target squad / flank side). Division squads are left out of
`Commanded` (your orders) and of the AI general's `Led()`; the division's `Brain` gives their orders instead, every
0.5-1 s by skill. Goals: Hold and Take (stand at a point, engage what comes within reach, leash back by temper), Attack
(form toward the target, then engage), Flank (an axis fixed when the goal starts; swing wide, come alongside, hit), Screen
(stand in front of a friend), Guard (stay by the general), Reserve, Harass (ranged), Pursue, FallBack.

- **Orders arrive late:** `IssueGoal(d, goal)` sets `d.Pending` and `d.PendingAt = Time + OrderDelay(d)`; `UpdateDivisions`
  swaps it in when due. A newer goal replaces a pending one. `Direct(squad, act)` delays a plain order to one company the
  same way; when it lands the squad gets `Override = true` and its sergeant leaves it alone until the next goal.
- **Couriers:** each goal (and each batch of direct orders) sends a `Courier` that rides the line from the general so that it
  arrives at `PendingAt`. An enemy within 2.5 m cuts it down (after a 1.5 s grace): the order's time moves back by a fresh
  delay, and the next rider bends round the danger. `BattleOptions.Couriers = false` keeps plain delays.
- **Leaders:** `LeaderCheck` notices a dead sergeant (morale -8 for his men, orders x2 slower) and promotes the best man
  left after 10 s. His aura is in `UpdateMorale`.
- **Where divisions come from:** `ArmySpec.Divisions` (a list of `DivisionSpec`) plus `Company.Division`, read by
  `BuildDivisions` in the constructor (not in sieges). The campaign fills them from `CampaignState.Divisions`
  (`Campaign.WithDivisions`); `Armies.Divide` splits any big army into a horse wing and two foot battles (enemy lords and
  quick battles). The AI's goals come from `AiCommand(team)`.
- **Replays:** goals you give are recorded (`Frame.Goals`) and re-issued on playback; the AI's are recomputed. Direct orders
  are recorded when they land, as ordinary order changes.

### 4.10 Ground (docs/TERRAIN.md)

The field has three layers on top of the height map. Each 2.5 m cell has a `GroundKind` (`TerrainGround.cs`, rules per
kind in `Data/ground.json`: pace for foot and horse, tiring, formation order, charge, reach, cover, stumbling). Line features
(`TerrainFeatures.cs`: hedges, fences, rails, planted stakes) block movement and give cover; `Feature.Stops(mounted)` says
who they stop. A field's shape comes from a `FieldLayout` (`TerrainLayouts.cs`) the seed (or the campaign) picks.

- **Moving and fighting:** `Move` reads the cell's pace and order; `Hit` reads slope (high ground) and the kind's reach and
  charge; `Tire` reads its fatigue. Arrows look along their flight for cover (`Covered`).
- **Sight** (`BattleSight.cs`): every 0.5 s each side works out which enemy companies it sees. Crests and woods hide;
  `Visible(s, team)` is what the AI and the HUD may use.
- **Pathing** (`BattlePath.cs`): only when the straight way is barred (`WayBlocked`), a company follows a cached flow field.
  `Via` -> `PathVia` steers each man. The screen's dotted order line (`Route`) builds its own field and never touches the
  battle's cache, so looking cannot change what happens.
- **The AI on the ground** (`BattleGround.cs`): `Survey` names the places worth holding (crest, knoll, wood edge, ford,
  bridge, hedge, village); the general holds good ground for a while (`HoldsGround`), keeps anchors dry (`DryAnchor`),
  picks firm going for horse (`HorseGoing`, `HorseFlank`), plants stakes; divisions snap to good ground and lie in ambush.
- **From the campaign** (`Campaign/Ground.cs`): `WorldMap.BattleGround` picks the layout from the map cell, `Province.Wet`
  (daily rain) and the season set `TerrainParams.Wet`/`Season`, and `Favour` puts the side that chose the ground on it.
- **On screen:** the ground line and order warnings (`Hud.Ground.cs`), the ground map (M), ground effects and sounds
  (`BattleScreen.GroundSteps`: splashes, mud, falling leaves; Splash, Squelch, Rustle and Hollow cues, played near the
  camera, so the sim stays silent about footsteps).
- **Tuning:** `--grounds n` fights the same army on both sides on each layout, twice a seed with the ground chosen by each
  side in turn. Layouts the chooser cannot change come out exactly even; a Ridge gives the chooser most fights.

### 4.11 Determinism and replays

`Replay.cs` records the seed, armies, options, start positions and, per frame, `dt`, the player's input and any orders
that changed. `Replay.Play()` reruns it; `--replay` checks the outcome matches. Anything in the sim that is not driven by
the seed and those inputs breaks this. Use `rng` (the battle's `Random(seed)`) for every roll, and do not iterate a
`Dictionary`/`HashSet` in a way whose order matters.

---

## 5. The campaign simulation

### 5.1 State and saving (`Campaign/CampaignState.cs`)

`CampaignState` (`campaign.S`) holds everything saved: `Hours`, `Gold`, `Parties`, `Settlements`, `Provinces`,
`Factions`, `Wars`, `Characters`, `Contracts`, `Pacts`, `Log`, the pending `Event`, `Captain` (your `CaptainState`), and
the `Rng` state. All plain public fields, serialised with System.Text.Json (`IncludeFields`). Anything not in
`CampaignState` is lost on save. Properties marked `[JsonIgnore]` are derived.

Key classes:
- `Party`: a band on the map (`Kind`: Player, Lord, Bandit, Garrison), `Pos`, `Goal`, `Companies`, `Prisoners`, `Food`, `Faction`.
- `CompanyRecord`: a company: `Name`, `Role`, `Cohesion`, `Stacks` (list of `TroopStack`), `Officer`, `Hero`, `Honours`, `Ledger`.
  `Men`, `Main` (largest stack) and `Type` are derived.
- `TroopStack`: `Troop` id, `Men`, `Wounded`, `Xp` pool. `Ready` = men the pool can promote.
- `Settlement`: `Kind` (Village, Town, Castle), `Faction`, `Owner` (party id or -1), `Recruits`, `Garrison`, `Mercenaries`,
  `Prosperity`, `Unrest`, `Buildings`, `Tax`.
- `FactionState`: `Ruler`, `Marshal`, `Objective`, council data, `Treasury`.

**Save versions:** `CampaignState.CurrentVersion` (now 4). Older saves are refused with a clear message, not migrated
(`RefuseOldSave` test). If you change the save shape in a way old saves cannot load: bump the version (old saves will then
be refused). Adding a new field with a sensible default does **not** need a bump: missing JSON fields keep their default.

`Save(slot)` writes a temp file first and keeps a `.bak`. Saves go to `CampaignState.SaveDir` (`IRONFIELD_SAVES` if set).

### 5.2 Randomness

The campaign uses `Rng` (in `CampaignState.cs`; its state is saved in `S.Rng`) through `campaign.rng`: `Float()`,
`Next(n)`, `Range(a, b)`, `Chance(p)`, `Pick(list)`. Use it, not `Random`, so a loaded save continues the same way.

### 5.3 Time (`Campaign.cs`)

`Tick(hours)` advances in 0.25 h steps and **stops early** when `Encounter` (a hostile party caught you or you it) or
`Arrived` (you reached a settlement) is set; the map screen shows that and clears it. Each step:

- `Hourly()` when the hour changes: AI thinking (`LordThink`, `BanditThink` in `CampaignAI.cs`), contracts, sieges, summons.
- `Daily()` when the day changes: healing, drill, food (`EatDaily`), `FactionAI`, sieges, diplomacy, events, then
  `FiefsDaily` and `CareerDaily` (rulers, councils, rank beats, win/loss).
- `Weekly()`: upkeep, income, garrisons, prisoners, desertion, relations.
- `Move(h)`: parties move toward their goal or chase target, at `Speed(p)` times road/terrain factor.
- `Encounters()`: a grid finds nearby pairs; `Clash(a, b)` makes AI fights resolve at once (auto-resolve) and a fight with
  you set `Encounter`.

Fields the map must show once and then clear: `Encounter`, `Arrived`, `Report`, `Beat`; and the saved `S.Event` (an event
waiting for your choice).

### 5.4 Battles from the campaign (`CampaignBattle.cs`, `Rules.cs`)

1. `PrepareBattle(enemy)` (or `PrepareAssault(settlement)` in `CampaignSiege.cs`) builds a `BattleSetup`: both armies via
   `Rules.Army(...)` (each `CompanyRecord` -> a battle `Company`, morale from cohesion, traits, honours), `BattleOptions`
   from your perks, gear, the weather (`Weather.For`), the ground (`WorldMap.BattleGround`), and a seed.
2. The screen fights it; `FinishBattle(result, setup)` -> `Apply(result, side0, side1, ...)`.
3. Or `AutoResolveEncounter()` -> `Rules.AutoResolve(...)`: compares `Strength` with +-15% luck and a tier edge, spreads
   losses toward lower tiers, and produces the same `BattleResult` shape -> the same `Apply`.
4. `Apply` turns the result into state: fallen men become wounded or killed (by armour), XP into stack pools, loot, ransom,
   prisoners, renown, officers, honours, skills; returns a `BattleReport` for the results panel.

`CompanyResult.Id` == `CompanyRecord.Id`: that is how results find their company.

### 5.5 Player actions (`CampaignActions.cs` and the layer files)

Every action the UI can take is a public method on `Campaign` that **returns `string?`: null on success, otherwise the
reason, in UPPER CASE, shown to the player**. Check every precondition first and change nothing if one fails:

```csharp
public string? BuyHorses(Settlement s, int n)
{
    n = Math.Min(n, s.Horses);
    if (n <= 0) return "NO HORSES FOR SALE";
    if (S.Gold < n * HorsePrice) return $"NEEDS {n * HorsePrice} CROWNS";
    S.Gold -= n * HorsePrice;
    s.Horses -= n;
    Player.Horses += n;
    return null;
}
```

The UI calls it as `note = C.BuyHorses(s, 1) ?? "BOUGHT";`.

### 5.6 The layers

The campaign was built in steps F1-F9 (see `docs/CAMPAIGN_OVERHAUL.md`); each layer is one file:

| Layer | File | Core ideas |
|---|---|---|
| Companies and stacks (F1) | `Companies.cs` | A company holds stacks of troop types; XP pools promote men one by one along upgrade lines; honours from history. |
| Cultures (F2) | `Factions.cs`, `Data/factions.json` | A realm's culture decides village recruits and promotion lines. |
| Characters (F3) | `Characters.cs` | Lords, companions, wanderers. Opinion = base (culture, traits) + memories that fade. `OpinionOf(a, b)`, `Remember(...)`. |
| Skills (F4) | `Skills.cs` | Skills rise by doing; a perk per 25 points. `Has("Perk")`, `Skill("Name")`, `Practise(...)`. |
| Career (F5) | `Career.cs` | Ranks FreeCompany -> SwornSword -> Vassal -> Lord -> Marshal -> King, derived from the world. Thresholds are constants at the top of the file. The council every 14 days grants fiefs, elects a marshal, sets the objective, votes on war. |
| Fiefs (F6) | `Fiefs.cs` | Prosperity and unrest, taxes, buildings, revolts at unrest 70+, defection at 100; vassals with standing orders. |
| Diplomacy (F7) | `Diplomacy.cs` | War exhaustion, peace terms (`Terms`), alliances, tribute, hosts and summons. |
| Events (F8) | `Events.cs`, `Data/events.json` | Triggers find a subject; choices apply effect verbs. See 8.3. |
| Pacing (F9) | `Pilot.cs`, `BalanceReport.cs` | A scripted captain plays headless; `--pacing` reports days per career stage. |

### 5.7 Balance values

Tunable numbers live in `Balance` (`CampaignState.cs`), read with `Balance.Get()` (in `Campaign` there is the shortcut `B`).
`Data/campaign.json` overrides them at runtime. Keep both identical (rule 4). Career thresholds (renown for ranks, council
days) are `const` in `Career.cs`, not in `Balance`.

**Army scale.** `Balance.ArmyScale` (3) multiplies every count of men in the campaign: company places (`Rules.Cap`), your men
limit, bands, garrisons, recruits, horses for sale. Base numbers in the code and in `campaign.json` stay at the old size; when you
write a new one:
- a count of men: `Scaled(n)` (in `Campaign`), e.g. `NewCompany(troop, "infantry", Scaled(20), 0)`;
- a price, reward or skill gain paid **per man** (wages, levies, loot, ransom, promotion, food), or worked out from
  `Rules.Strength` (which grows with men): wrap it in `Rules.PerMan(x)`, so a 3x army costs and earns what the old one did.
With `ArmyScale` set to 1 the campaign plays exactly as it did before the overhaul; compare `CampaignRun`'s printed pressure
line at 1 and 3 to find a value that was missed.

---

## 6. Screens and interface

### 6.1 `ScreenHost`

The MonoGame `Game`. `Mode` is Title, Map or Battle; `Update`/`Draw` call the current screen. It owns shared things:
`Art` (sprite atlas), `Font`, `Shading` (null if shaders failed), `Ctl` (controls), `Audio`, `Quality`, and the
`SettingsPanel` overlay. If the shader pipeline throws on the map, it falls back to `Quality.Low`.
Campaign battles go `MapScreen.OnBattle` -> `StartCampaignBattle` (autosaves) -> `BattleScreen.BeginCampaign` ->
... -> `FinishCampaignBattle` -> `Campaign.FinishBattle` -> `MapScreen.ShowResults`.

### 6.2 The `Ui` kit (campaign screens)

Immediate mode: every frame, panels are drawn and clicks tested in one pass. There is no retained widget tree.

| Call | Does |
|---|---|
| `ui.U(v)` | scale a design size by the UI scale (always wrap pixel sizes in it) |
| `ui.Panel(r)`, `ui.Rect(r, c)`, `ui.Frame(r, c, t)` | backgrounds and borders |
| `ui.Text(s, x, y, colour, scale)`, `TextC` (centred), `TextR` (right-aligned), `TW` (width) | pixel-font text; scale 2 normal, 1 small |
| `ui.Button(r, label, enabled, note, active)` | draws a button, returns true on the frame it is clicked |
| `ui.Hover(r)`, `ui.Clicked(r)` | hit tests |
| `ui.Bar(r, frac, colour)`, `ui.Portrait(type, box)` | bars and troop portraits |

In `MapScreen`, `Modal(w, h, title)` opens a centred dimmed panel and returns its rectangle; `note` is a string field that
`Note(r)` draws at the bottom of the panel (set it to an action's error, or a confirmation). `panel` (a `PanelKind`) says
which panel is open; set it to `PanelKind.None` to close. Colours come from `Style` (`Style.Text`, `Style.Dim`,
`Style.Accent`, `Style.Ok`, `Style.Warn`, `Style.Bad`, `Style.Foe`).

### 6.3 `MapScreen`

- `Update` handles keys (P party, K skills, L lords, R realm, ...), the camera, clicks on the world, and advances time.
- `Draw` renders the 3D map and parties, then `DrawUi()` (in `MapScreen.Hud.cs`) draws the HUD and whichever panel is open.
- The settlement panel picks its tabs by settlement kind, then calls one method per tab: `RecruitTab(s, body)`,
  `MercenariesTab`, `MarketTab`, `TrainTab`, `RestTab`, `FoodTab`, `ForageTab`, `CaptivesTab`, `GarrisonTab`, `FiefTab`,
  `ContractTab`. To add a tab, add its name to the right array in `SettlementPanel` and a `case` calling your method.

### 6.4 `BattleScreen` and `Hud`

`BattleScreen` wraps a `Battle`: two views blended by an eased camera, **field** (third person, you fight) and **command**
(orbit camera, slowed time, you direct squads). Input in `BattleScreen.Controls.cs` (`FieldControls`, `CommandControls`),
orders in `BattleScreen.Orders.cs` (`Command(Cmd)`, drawn lines), rendering in `BattleScreen.Draw.cs`. `Hud.cs` draws the
battle's 2D interface (cards, minimap, menus, crosshair, order wheel) and answers hit tests.

### 6.5 Controls

`Controls.cs`: the `Act` enum lists every bindable action; `Controls.Defaults` maps them to keys or mouse buttons; players
can rebind in `controls.json`. Read input with `ctl.Pressed(Act.X)`, `ctl.Down(Act.X)`. New actions should also appear
in the F1 help (`Hud.Help`). Some map keys (P, K, L, R) are hard-coded in `MapScreen.Update`.

---

## 7. Rendering and sound

There are no image, model or sound files: everything is generated at startup.

- **Sprites:** `SpriteSheet.cs` paints every body part, for every look, view (front/side/back) and side colour, into a
  1024x1024 atlas, using `Painter.cs` materials. Keys look like `helm.kettle.f`. A new `Look` part needs painting code here.
  `Puppet.cs` draws each unit as part sprites on a 3D skeleton, posed from its action state.
- **World:** `World.cs` builds the terrain mesh, foliage, towns, water and the minimap from `Terrain`.
- **Shaders:** plain GLSL in `Shaders/`, compiled at runtime by `GlslEffect.cs`. `Shading.cs` runs the pipeline (shadow map,
  scene target, post pass with FXAA). Quality High = shadows + post, Medium = no shadows, Low = fixed-function. GLSL rules
  are in the README "Shaders" section: render targets sample upside down on GL, vertex shaders end with `FIXUP_POSITION`,
  no MSAA (breaks Mesa/Xvfb).
- **Light:** `Sky.cs` sets sun, ambient and fog from the hour and weather.
- **Sound:** `Audio.cs` synthesises every `Cue` (Clash, Block, Hit, Bow, ArrowHit, Impact, Shout, Horn, Rout, Click, Coin; the ground's Splash, Squelch, Rustle, Hollow)
  and the music. The sim emits cues with `Sound(Cue.X, pos)` into `Battle.Sounds`; the screen plays them. No audio device
  means silence, not a crash.

Check visual changes with the screenshot commands in section 1 and look at the PNGs.

---

## 8. Data files

All in `Data/`. `troops.json`, `factions.json` and `events.json` carry `//` comments that explain their format; `campaign.json`
uses a `_comment` key instead. Keys match C# field names case-insensitively.

### 8.1 `troops.json`

`{ "mounts": [...], "troops": [...] }`. A troop:

```json
{ "id": "levies", "name": "Levies", "class": "Infantry", "culture": "common", "tier": 1,
  "upgradesTo": ["shieldmen", "spearrunners", "bowmen", "outriders"], "cost": 10, "upgradeXp": 60,
  "hp": 70, "damage": 16, "armor": 1, "reach": 1.6, "windup": 0.5, "recover": 0.7, "speed": 3.6, "block": 0.35,
  "look": { "head": "cap", "body": "tunic", "legs": "cloth", "feet": "boots", "hands": "skin", "shield": "round", "weapon": "axe" } }
```

Optional: `range`, `ammo`, `pierce`, `reload`, `vsMounted`, `mount` (a mount id), `leader`. Look values must be from the
lists in `Look` (`Units.cs`). `Troops.Validate` refuses broken data at load (missing links, upgrades that do not raise the
tier, unknown look parts, cavalry without a horse) and `TroopTree` checks every troop is reachable from `levies`.

### 8.2 `factions.json`

`cultures` (id, name, root troop, text) and the realms: name, palette, culture, and `lines` (the troop each role fields at
tiers I-VII; each must be an upgrade of the one before, or the same).

### 8.3 `events.json`

```json
{ "id": "famine", "trigger": "fief_poor", "cooldown": 30, "title": "HUNGER IN {settlement}",
  "text": "...", "choices": [
    { "label": "OPEN YOUR STORES", "need": "gold:200", "effects": ["gold:-200", "prosperity:12", "unrest:-10"] },
    { "label": "LET THEM FEND", "effects": ["unrest:12", "prosperity:-3"] } ] }
```

- **Triggers** (`Campaign.Triggers` in `Events.cs`): `fief_unrest`, `fief_poor`, `fief_rich`, `lord_friend`,
  `lord_hates_liege`, `big_band`, `veterans`, `hungry_winter`, `unpaid`, `prisoners`, `war_weary`, `in_town`, and the forced
  `brigand_king`, `pretender`. Each is a `case` in `Find(trigger)` that picks the subject or returns null.
- **Effect verbs** (`Verbs`): `gold`, `renown`, `food`, `horses`, `opinion`, `opinion_liege`, `opinion_realm`, `unrest`,
  `prosperity`, `tax`, `cohesion`, `xp`, `exhaustion`, `gamble`, and without a number: `quest`, `bounty`, `revolt`,
  `prisoners_free`, `prisoners_recruit`, `disband`. Each is a `case` in `ApplyEffect`.
- **Holes** in text: `{settlement}`, `{lord}`, `{band}`, `{company}`, `{realm}`, `{liege}`.
- `ValidateEvents` (run by the `EventsAndQuests` test) rejects unknown triggers, verbs and holes.

### 8.4 `campaign.json`

Overrides for every `Balance` field (section 5.7), plus optional `Buildings`, `Honours` and `Skills` lists (built-in
defaults are used when empty). `BalanceMatchesData` fails if a number here differs from its C# default.

### 8.5 `names.json`

Name lists for lords, companies and places (`Names.Get()`).

---

## 9. Testing

`dotnet run -c Release --no-build -- --selftest` runs everything (about 70 s); CI runs exactly this. Tests are
`static void` methods on the partial class `SelfTest`, spread over `Tests/*.cs`, and called in order from
`SelfTest.Run()`. A failing `Check(cond, "message")` throws `selftest failed: message` and stops the run.
`--selftest Name` runs one test by method name (it does not need to be in `Run()`, but it must be to run in CI).

The run prints a few numbers (battle times, losses). Those move whenever sim behaviour changes; that is expected. Only the
final `selftest ok` matters.

**Helpers in `SelfTest.cs`:**
- `Run(b, seconds)`: step a battle at 1/60 s.
- `Lancer(b)`: one galloping lancer for charge tests.
- `Scaled(army, k, idBase)`, `Ids(army, idBase)`: resize armies / give companies ids.
- `Days(c, n)`: let a campaign run n days, brushing aside encounters.
- `MakeWanderer(c, town)`: put a wanderer in a town.

**A battle test:**

```csharp
static void SpearsStopHorse()
{
    var b = Battle.Empty(7);
    var spears = b.AddSquad(1, Troops.Get("spearrunners"), 12, new(0, 0), 6, -Vector2.UnitY);
    var l = Lancer(b);                       // team 0, galloping at the spears
    Run(b, 6);
    Check(l.Dead || l.Hp < l.Type.MaxHp, "braced spears hurt a charging lancer");
}
```

**A campaign test:**

```csharp
static void HorsesCostGold()
{
    var c = Campaign.New(3);
    var town = c.S.Settlements.First(s => s.Kind == SettlementKind.Town && s.Horses > 0);
    float gold = c.S.Gold;
    Check(c.BuyHorses(town, 1) == null, "can buy a horse");
    Check(c.S.Gold == gold - c.HorsePrice, "a horse costs HorsePrice");
}
```

Both are real tests (`SpearsStopHorse` in `Tests/BattleTests.cs`, `HorsesCostGold` in `Tests/CampaignTests.cs`): copy them as a starting point.

Put battle tests in `Tests/BattleTests.cs`, campaign tests in `Tests/CampaignTests.cs`, career/realm/event tests in
`Tests/CareerTests.cs`, and add the call to `SelfTest.Run()`. Use fixed seeds so results are repeatable.

Battle-scale tests: `ScaleBattle` (600 a side, divided, deploys without overlaps, under 12 ms a tick, replays exactly),
`DivisionGoals` (delay, countermand, direct orders, leash, flank, a fallen sergeant, replay) and `Couriers`; campaign:
`CampaignDivisions` (make, assign, save, into battle, old saves).

Other tools: `--balance N` writes CSVs of N headless campaigns and battles to `balance/` (gitignored); `--pacing N` runs the
stand-in captain and reports career stage days against targets (slow: ~10 min for 8).

---

## 10. Worked examples

### 10.1 Change a troop's stats
1. Edit the troop in `Data/troops.json`.
2. `tools/check.sh`. `TroopTree` fails if an upgrade is no longer better than what it upgrades from; fix the data, not the test.

### 10.2 Add a new troop
1. Add it to `Data/troops.json` with a higher `tier` than the troop it upgrades from, and add its id to that troop's `upgradesTo`.
2. If it belongs to a realm's line, add it to that realm's `lines` in `Data/factions.json`.
3. Use only existing look parts, or paint new ones in `SpriteSheet.cs` and add them to the arrays in `Look`.
4. `tools/check.sh`; render battle screenshots to see it.

### 10.3 Add a campaign action with a button
1. In `Campaign/CampaignActions.cs` (or the layer file it belongs to), add `public string? DoThing(Settlement s)`: check
   preconditions and return a reason string, then change state and return null.
2. In the right `MapScreen.*.cs` file, inside the panel or tab method:
   `if (ui.Button(new Rectangle(body.X, body.Y + ui.U(60), ui.U(200), ui.U(28)), "DO THING", canDo)) note = C.DoThing(s) ?? "DONE";`
3. Add a campaign test that calls `DoThing` and checks the state change and the failure reasons.
4. `tools/check.sh`, then campaign screenshots to see the button.

### 10.4 Add a balance value
1. Add the field with its default to `Balance` in `Campaign/CampaignState.cs`, with a `///` comment.
2. Add the same key and value to `Data/campaign.json`.
3. Read it as `B.MyValue` in `Campaign`, or `Balance.Get().MyValue` elsewhere.

### 10.5 Add a field to the save
1. Add a public field with a default on the right class in `CampaignState.cs` (e.g. `Party`, `Settlement`, `CaptainState`).
2. Existing saves load fine (the field keeps its default). Bump `CurrentVersion` only if old saves can no longer be read correctly; they will then be refused.
3. Extend `CampaignPersistence` if the field must survive a save/load round trip in a non-obvious way.

### 10.6 Pass something from the campaign into a battle
1. Add a field to `BattleOptions` (`BattleTypes.cs`).
2. Set it in `Campaign.PrepareBattle` (`CampaignBattle.cs`), and in `PrepareAssault` (`CampaignSiege.cs`) if it applies to sieges.
3. Read `Options.MyField` in the battle code. Replays store `Options`, so they keep working.

### 10.7 Add an event
1. Add an entry to `Data/events.json` using existing triggers and verbs (8.3).
2. For a new trigger: a `case` in `Find` returning a `PendingEvent` (or null) and its name in `Triggers`.
   For a new verb: a `case` in `ApplyEffect` and its name in `Verbs` (and `NoNumber` if it takes no number).
3. `tools/check.sh` (`EventsAndQuests` validates the file).

### 10.8 Change battle AI or combat
1. Find the step in the tick table (4.3); AI in `BattleAI.cs`, damage in `BattleCombat.cs`, morale in `BattleMorale.cs`.
2. Use `rng` for any randomness; set `u.Wish` instead of moving units directly.
3. Add or adjust a scenario test on `Battle.Empty(seed)`.
4. `tools/check.sh`. The printed battle times will move; `selftest ok` must remain. `CavalryBalance` and `Calibration`
   guard the overall balance: if they fail, the change shifted balance too far.

### 10.9 Add a key action
1. Add a value to `Act` in `Controls.cs` and a default binding in `Controls.Defaults`.
2. Read it with `ctl.Pressed(Act.MyAction)` in the screen.
3. List it in the F1 help (`Hud.Help`).

### 10.10 Add a sound
1. Add a value to `Cue` in `Audio.cs` and synthesise it in the `AudioEngine` constructor (see how others use `Make`).
2. Emit it from the sim with `Sound(Cue.MyCue, pos)`.

---

## 11. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `CHECK FAILED: headless files above use MonoGame graphics` | sim or test code references `GraphicsDevice`, `Texture2D`, `Keyboard`, ... | move the drawing/input part into a screen or `Hud` file; the sim exposes data, the screen reads it |
| `CHECK FAILED: battle sim above uses unseeded randomness` | `Random.Shared`, `new Random()`, `DateTime` in battle code | use the battle's `rng` |
| `CHECK FAILED: docs/CODEMAP.md is stale` | methods or files added/moved | `git add` new files, run `tools/codemap.sh`, commit the result |
| `selftest failed: replay reproduces...` (`ReplayExact`) | non-determinism in the sim | look for unordered collections, the clock, or rolls outside `rng` |
| `selftest failed: campaign.json and Balance defaults differ: X` | a balance value changed in one place | make `Data/campaign.json` and the `Balance` default the same |
| `troops.json: ...` exception at startup or in tests | `Troops.Validate` found broken troop data | read the message; it names the troop and the problem |
| a `Check` in `Calibration` or `CavalryBalance` fails | balance moved too far | revisit the change; do not loosen the check without the user's agreement |
| the build passes but a panel looks wrong | layout numbers | render campaign screenshots and look at the PNG |
| `no test named X` | `--selftest X` with a wrong method name | names are case-sensitive, see `docs/CODEMAP.md` under `Tests/` |
| `dotnet: command not found` | SDK missing | `apt-get install -y dotnet-sdk-10.0 xvfb` |
| shader errors on the map | GLSL problem | the game falls back to Low quality; fix the shader following the README "Shaders" rules |

---

## 12. Glossary

| Term | Meaning |
|---|---|
| captain | you, the player character (in battle `Battle.Player`; on the map `Campaign.Player` / `S.Captain`) |
| company | a unit of men under one name: `CompanyRecord` on the map, `Company` going into battle, `Squad` on the field |
| stack | the men of one troop type inside a company (`TroopStack`) |
| troop | a soldier type from `troops.json` (`UnitType`) |
| tier | a troop's rank, I-VII; upgrades always raise it |
| line | a culture's sequence of troops for one role across tiers (`factions.json`) |
| role | infantry, spears, archers or horse: decides where a company deploys |
| cohesion | a company's campaign-side steadiness (0-30); becomes battle morale |
| renown | your fame; gates ranks and men limit |
| realm / faction | a kingdom; id indexes `S.Factions` |
| fief | a settlement you hold (`Settlement.Owner == Player.Id`) |
| seat | a town or castle (a fief that is not a village) |
| encounter | a hostile party has met yours (`Campaign.Encounter`) |
| beat | a one-time career message for the map (`Campaign.Beat`) |
| pilot | the scripted stand-in captain used in headless runs |
| headless | runs with no window: the sims, tests and tools |
