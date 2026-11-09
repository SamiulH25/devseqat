# CLAUDE.md: Ironfield project map

Read this first; it is written so a new session can start work without exploring the code.
For a member-level index (every type and method with its line number) read `docs/CODEMAP.md`.
For how each system works in depth (data model, tick order, damage path, campaign layers, data formats, worked examples,
troubleshooting) read `docs/DEVELOPING.md`.
Keep both current: see "Keeping this map current" at the end.

## Workflow for every change (follow in order)

1. Find the code: use the "File guide" table below, then `docs/CODEMAP.md` for the exact line of a method.
2. Make the smallest change that does the job. Match the style of the code around it. Do not reformat or move code you are not changing.
3. If you added behaviour, add or extend a test (see "Recipes").
4. Run `tools/check.sh`. It must end with `ALL CHECKS PASSED`. If it fails, fix the cause; never weaken or skip a test to get green.
5. If you added, renamed or moved a method or file: `git add` new files, run `tools/codemap.sh`, and update the tables here if a file's job changed.
6. Commit with a short message saying what changed and why.

## What this is

**Ironfield**: a 2.5D medieval battle + campaign game. C# / .NET 10 / MonoGame 3.8 DesktopGL. One project
(`Ironfield.csproj`, namespace `Ironfield`, ~17k lines). **No art or content pipeline**: sprites, terrain, font,
sound and music are all generated in code; shaders are plain GLSL compiled at runtime. The only data is `Data/*.json`.
`Content/Content.mgcb` has settings only, no content items.

## Commands

```bash
tools/check.sh                                           # build + rule checks + self-test; run before every commit
tools/check.sh --quick                                   # same without the self-test (while iterating)
dotnet build -c Release                                  # ~30 s cold, warnings are expected (CS0649 on JSON DTOs)
dotnet run -c Release --no-build -- --selftest           # THE test suite (headless, ~70 s); CI runs exactly this
dotnet run -c Release --no-build -- --selftest Parry     # one test by SelfTest method name
dotnet run -- --balance 4                                # N headless campaigns+battles -> balance/*.csv (gitignored)
dotnet run -c Release -- --pacing 8                      # stand-in captain only: career stage days vs targets (~10 min)
dotnet run -- --replay [file]                            # replay replays/last-battle.json, check same outcome
dotnet run -- --write-controls                           # write default controls.json
dotnet run -c Release --no-build -- --sprites out.png torso.plate helm   # headless: chosen sprite parts 6x on one PNG (pixel-art check)
dotnet run -c Release -- --bench 1500                    # headless 1500-a-side battle: ms per tick by stage (3000 men ~3.6 ms)
dotnet run -c Release -- --realms 8                      # snowball guard: N campaigns x 240 days, realm shares -> balance/realms.csv (~3 min)
dotnet run -c Release -- --grounds 24                    # same army both sides on each field layout, ground chosen by each in turn -> balance/grounds.csv (~2 min)
dotnet run -c Release -- --agree 4 5                     # fought vs auto-resolved: equal-Might fights by tier gap, at DrillWeight and at 5 (~2 min)
dotnet run -c Release -- --campaign-bench 30             # headless campaign for N days: ms a day by tick stage (~76 ms on the 115-province map)
```

Rendering without a display (works in the cloud container, software GL via Mesa llvmpipe):
```bash
IRONFIELD_SAVES=/tmp/ifs IRONFIELD_SHOTS=/tmp/shots xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
```
Saves ~30 staged battle PNGs (field-view.png, command-overview.png, gallery-*.png, pose-sheet*.png, pose-horses.png...) and quits, with `render.txt` (sprite triangles, lines and marks drawn per shot). `IRONFIELD_CAMPAIGN_SHOTS=/dir`
does the same for campaign screens, `IRONFIELD_LAB_SHOTS=/dir` for every UI lab page. Each run also writes `overflow.txt` (text that runs out of its panel; aim for none). ALSA/OpenAL "no audio" errors are harmless. Look at PNGs with the Read tool to check visual changes.

Other env vars: `IRONFIELD_SAVES` (save dir; settings go to its parent), `IRONFIELD_QUALITY=High|Medium|Low`,
`IRONFIELD_LEVELS=4,2` (quick-battle army levels), `IRONFIELD_MEN=750` (quick-battle men a side; default half the Battle size setting), `IRONFIELD_WEATHER=hour,rain,fog`, `IRONFIELD_GROUND=river` or `=Hills,river` (a `TerrainParams` preset), `IRONFIELD_LAYOUT=Ridge` (a `FieldLayout`),
`IRONFIELD_SHOTS_FROM=clash` (shots: only the melee clashes), `IRONFIELD_DUMP_ATLAS=/x.png`, `IRONFIELD_GPU_REPORT=1` (same as `--gpu-report`).
Default save/settings location: `$XDG_CONFIG_HOME|~/.config/Ironfield/` (ApplicationData). Set `IRONFIELD_SAVES` when running so you do not touch real saves.

## Environment

Cloud environment "devseqat" has a setup script that installs `dotnet-sdk-10.0` + `xvfb` from the Ubuntu 24.04 archive and
pre-builds. If `dotnet` is missing: `apt-get install -y dotnet-sdk-10.0 xvfb`. NuGet restore (MonoGame) needs package-registry access only.
CI: `.github/workflows/ci.yml` builds, runs `--selftest`, and on push publishes self-contained builds for win/linux/osx.

## Architecture in one screen

```
Program.cs  CLI switches (--selftest/--balance/--replay/--write-controls) else runs ScreenHost
ScreenHost  MonoGame Game. Mode {Title, Map, Battle, Lab}; owns Art, Audio, Settings, Controls (Ctl), Shading, Quality
  TitleScreen ── New/Continue/QuickBattle ──┐
  MapScreen (campaign UI + 3D map) ◄────────┤   SettingsScreen overlay on any mode
  BattleScreen (battle UI, camera, render) ◄┘

HEADLESS (no MonoGame graphics; must stay that way so --selftest runs):
  Battle (partial: Battle.cs, BattleAI, BattleCombat, BattleMove, BattleMorale, BattlePlayer, BattleSiege, BattleCaptain, BattleGrid,
          BattleDivision, BattleCourier, BattleCreatures, Replay)
     uses Squad, Unit/UnitType/Mount/Look (Units.cs), Troops (Data/troops.json), Terrain, BattleOptions/Weather
  Campaign (partial: Campaign/Campaign.cs, CampaignBattle, CampaignActions, CampaignAI, CampaignWar, CampaignSiege, Career, Characters, ...)
     state = CampaignState (all save data, plain fields -> JSON), Rules (static company math), WorldMap, Rng, Balance, Names
```
`Vector2` is used for both, but only `Microsoft.Xna.Framework` math types are allowed in headless code (no GraphicsDevice etc.).

### Campaign -> battle -> campaign hand-off
`MapScreen` encounter panel -> `Campaign.PrepareBattle(enemy)` (or `PrepareAssault(settlement)`) builds a `BattleSetup`
(`ArmySpec`s via `Rules.Army`, `BattleOptions` from perks/gear/weather/terrain, seed) -> `MapScreen.OnBattle` event ->
`ScreenHost.StartCampaignBattle` (autosaves) -> `BattleScreen.BeginCampaign` -> `new Battle(seed, player, enemy, options)` ->
deployment phase -> fight -> `Battle.Result()` = `BattleResult` -> `ScreenHost.FinishCampaignBattle` -> `Campaign.FinishBattle`
-> `Apply` (wounds/killed, XP, loot, ransom, prisoners, officers) -> `BattleReport` -> `MapScreen.ShowResults`.
Auto-resolve skips the battle: `Campaign.AutoResolveEncounter` -> `Rules.AutoResolve` -> same `Apply`.
`ArmySpec` = list of `Company(troop id, count, role, id, ...)`; `CompanyResult.Id` maps back to `CompanyRecord.Id`.

### Battle tick (`Battle.Update(dt)`, fixed 1/60 s in tests)
ApplyDirects -> RecordFrame -> BuildGrid -> centroids -> `General(team)` every 0.5 s (enemy AI, and own side if AutoPlayer/AI squads) ->
`UpdateDivisions` (couriers, arrived goals, `AiCommand`, each division's `Brain`) -> `UpdateSight` (every 0.5 s) ->
Batter (siege gate) -> Reinforce (1 s) -> called horse, Resupply -> `UpdateSquad` each (and `UpdatePath`) -> per unit: `UpdatePlayer` or `Think`
(`ThinkCav` for cavalry), `TickAction` (windup/strike/recover), `Tire` -> `Move` (steering, separation, terrain, wall) ->
`Braces`, `Lances` -> `UpdateArrows` -> `UpdateMorale` -> `CheckEnd` (sets `Winner`).
Files: tick/setup/result in `Battle.cs`; `Think`/`ThinkCav`/`General` in `BattleAI.cs`; `UpdatePlayer` in `BattlePlayer.cs`;
`TickAction`/`Strike`/`Hit`/`Kill`/arrows/`Braces`/`Lances` in `BattleCombat.cs`; `Move` in `BattleMove.cs`; `UpdateMorale` in `BattleMorale.cs`.
Damage path: `Strike` -> `Hit(attacker, target, ...)` (block/parry, flank via `SideOf`, speed bonus, slope) -> `Kill`. Horses: `HitHorse`.
Men think on a stagger (`ThinkNow`: near the enemy every tick, else every 2nd/4th) and morale every 2nd tick, keyed on the tick counter.

### Divisions (docs/COMBAT_OVERHAUL.md)
A `Division` (BattleDivision.cs) is a group of squads under a sergeant (`Unit.Sergeant`) that follows a `Goal` (Hold, Take, Attack,
Flank, Screen, Guard, Reserve, Harass, Pursue, FallBack, Ambush) through its `Brain`, not your orders. Division squads are excluded from
`Commanded` and the general's `Led()`. `IssueGoal` sets `Pending` + `PendingAt` (the order delay); a `Courier` rides there and can
be cut down (BattleCourier.cs). `Direct(squad, act)` delays a plain order and sets `Squad.Override`. Divisions come from
`ArmySpec.Divisions` + `Company.Division` (`BuildDivisions`); the campaign fills them from `CampaignState.Divisions`
(`Campaign.WithDivisions`, Campaign/Divisions.cs); `Armies.Divide` splits big AI armies. Battle UI: `Hud.Divisions.cs`, Y key.

Battle randomness uses its own seeded `rng` only: replays (`Replay.cs`) must reproduce exactly, so never use `Random.Shared`
or wall-clock time in the sim; `SelfTest.ReplayExact` checks it.

### Campaign tick (`Campaign.Tick(hours)`, 0.25 h steps, stops on `Encounter`/`Arrived`)
Ticks live in `Campaign.cs`; the AI they call (`LordThink`, `BanditThink`, `FactionAI`) in `CampaignAI.cs`.
`Hourly` (lord/bandit AI: `LordThink`, `BanditThink`, contracts, `SiegeHourly`) / `Daily` (healing, drill, food `EatDaily`,
`FactionAI`, sieges, truces) / `Weekly` (upkeep, income, garrisons, prisoners, desertion) -> `Move` -> `Encounters`.
Player actions (most in `CampaignActions.cs`) are public methods on `Campaign` returning `string?` (null = success, else the reason shown in the UI):
`Hire`, `RecruitLevies`, `BuyHorses`, `BuyGear`, `Upgrade`, `Split`, `Rest`, `SignContract`, `Besiege`, `Station`, `SellPrisoners`,
`SwearFealty`, `ProposeWar`, `SetObjective`, `DeclareIndependence`, `DeclareWar`, `OfferPeace`...
`Daily` ends with `FiefsDaily` then `CareerDaily` (rulers, councils due, rank beats, win/loss). Events the map must show once are plain fields:
`Encounter`, `Arrived`, `Report`, `Beat` (and the saved `S.Event`, an event waiting for a choice). Map screens on keys: P party, K skills, L lords, R realm, V divisions, M realm map, B siege, F1 key help (`Act` entries, table `MapScreen.Screens`; `OpenScreen`/`CameFrom` remember the party screen as the way back).

## File guide (where to look for what)

| Area | Files |
|---|---|
| Battle setup, tick, reinforcements, result | `Battle.cs` (`Update`, `Deploy`, `AddSquad`, `Reinforce`, `Result`, `CheckEnd`) |
| Unit AI, cavalry AI, the AI general | `BattleAI.cs` (`Think`, `ThinkCav`, `General`) |
| Melee, damage, kills, arrows, braced spears, lances / creatures (troll sweep, knockback, healing, sun; the necromancer raising the dead) | `BattleCombat.cs` (`Strike`, `Hit`, `Damage`, `HitHorse`, `Kill`, `Shoot`, `UpdateArrows`) / `BattleCreatures.cs` (`Sunlit`, `Sweep`, `Knock`, `Regen`, `BodyRadius`, `RaiseDead`, `MasterFalls`) |
| Unit movement and collisions / morale / your own unit | `BattleMove.cs` (`Move`, `Impact`) / `BattleMorale.cs` (`UpdateMorale`) / `BattlePlayer.cs` (`UpdatePlayer`) |
| Formations, orders, slots | `Squad.cs` (`Order`, `Formation`, `Slot`, `SetLine`) |
| Melee from the ranks (docs/MELEE.md): places kept for the battle, gaps filled from behind, a charge closing as a body, front rank fights while the rest wait | `BattleRanks.cs` (`PlaceMen`, `CloseFiles`, `Close`, `FightFromRanks`, `RankError`, `Intermix`) / drill by tier and order in battle (`Squad.Drill`, `Formed`, `Broken`, `Grade`), the guard a drilled man raises, numbers and shoulders in a blow | `BattleRanks.cs` (`Guard`, `Footing`, `Overwhelms`) / `BattleDrill.cs` (`DrillOf`, `GradeOf`, `UpdateFormed`, `RankGuard`, `RankEdge`); tests `Tests/MeleeTests.cs` |
| Company wits by grade (docs/MELEE.md D5): wheel to face a flank, take a pinned foe in the flank, wrap a narrower line (`Squad.Bend` in `Slot`), re-form when broken, rotate tired men, pincers, roll-up; a broken crowd strikes at 0.7 (`BattleCombat.BrokenBlow`) | `BattleTactics.cs` (`Plan`, `Tactics`, `Wits`, `FlankThreat`, `Pinner`, `FlankPoint`, `Wrap`, `Rotate`, `Choose`); the flank approach in `BattleRanks.Close` |
| Horse and bows by grade (docs/MELEE.md D6): rabble horse bog down, veteran horse rally as a body (`Squad.Rallying`) and charge a fighting company's flank, elite horse ride a lane round and hunt open archers; veteran bows fall back behind the foot and hold fire into a melee, elite bows focus wavering companies and archers | `BattleWings.cs` (`RallyPoint`, `RideToRally`, `HorseWits`, `HorsePrey`, `ChargeLane`, `HoldsFire`, `BowsWits`, `Focus`); hooks in `BattleAI.ThinkCav`, `Think`, `BattleCaptain.SoftTarget` |
| Sieges in battle / on the map | `BattleSiege.cs` (wall, ladders, gate `Via`) / `Campaign/CampaignSiege.cs` (garrisons, capture, fiefs) |
| Captain combat (bash, swap, horse call), fatigue, supply | `BattleCaptain.cs` |
| Personal goals in a battle (draw, live state, result) / your banner and the last stand | `BattleDeeds.cs` (`DrawGoals`, `GoalState`); HUD `Hud.GoalLines` / `BattleBanner.cs` (`UpdateBanner`, `LastStand`, `UpdateLastStand`) |
| Spatial grid (`Near`, `Separate`) | `BattleGrid.cs` |
| Sight: who has seen which company, crests and woods, ambushes (docs/TERRAIN.md T3) | `BattleSight.cs` (`UpdateSight`, `Visible`, `Sees`, `ClearLine`, `Ambush`); arrow cover `BattleCombat.Covered` |
| The AI reads the ground: the survey of places, holding a crest, dry anchors, horse going, the horse's flank, sergeants' snap, the AI's ambush wood | `BattleGround.cs` (`Places`, `Survey`, `BestPlace`, `SnapToGround`, `HoldsGround`, `Patience`, `DryAnchor`, `HorseGoing`, `HorseFlank`); `BattleDivision.AmbushWood` |
| Ground for the player: ground words, ground map (M), order warnings, stakes (K) | `Grounds.Describe`/`Name` (TerrainGround.cs); `Hud.Ground.cs` (`GroundPanel`, `GroundHints`); `World.GroundMap`, `World.KindColor` (World.Features.cs); `Battle.GroundWarning`, `PlantStakes`, `CannotStake` (BattleGround.cs); `FeatureKind.Stakes` (stops horse only: `Feature.Stops`); drawn in `BattleScreen.Stakes` |
| The campaign chooses the ground: field from the map cell, rain per province, who chooses, the panel line, auto-resolve edge | `WorldMap.BattleGround`; `Campaign/Ground.cs` (`RainDaily`, `GroundChooser`, `GroundFor`, `BattleSeed`, `GroundNote`, `GroundEdge`); `TerrainParams.Wet`/`Season`/`Favour` read in `TerrainLayouts` (`GroundOf`) |
| Pathing: flow fields round water, hedges, houses (only when the straight way is barred) | `BattlePath.cs` (`UpdatePath`, `WayBlocked`, `PathVia`, `Flow`, `FlowField`, `Route` for the order's dotted line); hooked in `BattleSiege.Via` |
| Divisions: goals, order delay, sergeant brain, AI goals / couriers | `BattleDivision.cs` (`IssueGoal`, `OrderDelay`, `Direct`, `Brain`, `AiCommand`) / `BattleCourier.cs` |
| Attack styles of tier V-VII troops (docs/COMPANIONS_MAGIC.md D2): crush, sweep, riposte, bash, thrust through, hold off, ride down, cut and wheel, plunging shot, quick loose, punch; all passive | `BattleStyles.cs` (`StyleFor`, `StyleText`, `StyleLanded`, `StyleGuard`, `Behind`); `UnitType.Style`/`StyleRank` set in `Troops.Load` (`attack` in troops.json overrides the weapon's); hooks in `Strike`, `Hit`, `StartSwing`, `Shoot`, `UpdateArrows`; reuses `Sweep`/`Knock` (BattleCreatures.cs); tests `Tests/StyleTests.cs` |
| Effects (docs/COMPANIONS_MAGIC.md Part A): one language for buffs, traits and auras; passive traits of tier V-VII troops and culture signatures (Zeal) | `Effects.cs` (`EffectDef`, `Mods`, loads `Data/effects.json`, unknown keys refused) / `BattleEffects.cs` (`PassivesFor`, `SignatureOf`, `PassiveWeight`, `TroopMods`, `UpdateEffects`, `AurasOn`); a man reads `Unit.Mods` (= `UnitType.Mods` + `Squad.Mods`) in `Hit`, `Damage`, `UpdateMorale`, `Move`, `Tire`, `Shoot`/`Loose`, `Braces`, `Lances`; auto-resolve `Rules.MightAt` (`Balance.TraitWeight`); tests `Tests/EffectTests.cs` |
| Magic and abilities (docs/COMPANIONS_MAGIC.md E1b, M1-M4): mage troops (`UnitClass.Caster`, `UnitType.Spells`), the ability engine (Will, `when` rules, casts with a warning ring, fields, timed effects, great workings), companion abilities on the same engine | `Abilities.cs` (`AbilityDef`, loads `Data/abilities.json`) / `BattleAbilities.cs` (`LearnSpells`, `UpdateAbilities`, `ThinkSpells`, `Target`, `Fire`, `Blast`, `UpdateFields`); `BattleEffects.RefreshMods`; drawn in `BattleScreen.Spells`; tests `Tests/MagicTests.cs` |
| Map buffs, camp rites, the relic, Omen | `Campaign/Buffs.cs` (`AddBuff`, `ActiveBuffs`, `BuffEdge`, `Feast`) / `Campaign/Rites.cs` (`RiteList`, `CanWork`, `Rite`, `RelicReady`); `dark_omens` events and `calm` verb (Events.cs); chips `MapScreen.BuffChips`; rite buttons in `MapScreen.Muster.cs` |
| Companion callings, XP, talents, champions / bonds, stories, students, leaving | `Campaign/Callings.cs` (`Data/companions.json`; `CallingFor`, `GainXp`, `WithHeroes`, `WithChampion`, `CompanionsAfterBattle`) / `Campaign/Bonds.cs` (`CompanionsReact`, `BondsAfterBattle`, `OpenStory`, `TakeStudent`, `StudentRises`, `CompanionsWeekly`); tests `Tests/CompanionTests.cs` |
| Unit/troop types, look, mounts | `Units.cs`, `Troops.cs` (+ `Armies` presets, `Large`, `Divide`), `Data/troops.json` |
| Battle inputs/outputs | `BattleTypes.cs` (`BattleOptions`, `Weather`, `BattleResult`, `CompanyResult`) |
| Terrain, rivers, foliage placement, siege wall geometry / the ground layer (kinds of ground, cover) / field layouts (ridge, two hills, broken, fen, river line, wooded flank, forest clearing, village, hedgerows, pass) / hedges, fences, bridges | `Terrain.cs` (headless) / `TerrainGround.cs` (`GroundKind`, `Grounds` <- `Data/ground.json`, `GroundAt`, `CoverAt`, `Paint`) / `TerrainLayouts.cs` (`FieldLayout`, `ShapeLayout`, `LayoutHeight`, `GroundOf`, `PlaceVillage`, `PlaceHedges`) / `TerrainFeatures.cs` (`Feature`, `AddFeature`, `GoRound`, `CrossesFeature`, `BridgeTheRoad`); drawn in `World.Features.cs`; docs/TERRAIN.md |
| Campaign data model + save/load/migration | `Campaign/CampaignState.cs` (also `Rng`, `Balance` <- `Data/campaign.json`, `Names`) |
| Campaign time, movement, encounters, hourly/daily/weekly ticks | `Campaign/Campaign.cs` |
| Battles from the campaign: prepare, finish, auto-resolve, apply results | `Campaign/CampaignBattle.cs` (`PrepareBattle`, `FinishBattle`, `Apply`) |
| Your town actions (recruit, hire, horses, split, contract, rest) | `Campaign/CampaignActions.cs` |
| The captain's gear: kit (`CaptainState.Kit`), buy/sell/wear, armourer stock, old-save tiers -> kit | `Gear.cs` (`GearDef`, loads `Data/gear.json`, `Validate`) / `Campaign/Armoury.cs` (`BuyGear`, `Equip`, `SellGear`, `ArmourerStock`, `MigrateGear`); `Rules.CaptainType(c, side)` builds the battle unit; UI `MapScreen.Armoury.cs` (MARKET tab) |
| Duels before a battle: who accepts, the lord's fighting form, the fight (`BattleSetup.Duel`, `BattleOptions.Duel`), what it does to the battle | `Campaign/Duels.cs` (`CannotChallenge`, `LordChampion`, `PrepareDuel`, `FinishDuel`); `Battle.SetupDuel`; CHALLENGE in `MapScreen.Dialogs.cs` `EncounterPanel` |
| Lord/bandit/realm AI, spawning | `Campaign/CampaignAI.cs` (`LordThink`, `BanditThink`, `FactionAI`) |
| Company math, auto-resolve (`Might`: strength with drill weighed in, `Balance.DrillWeight`), army building | `Campaign/Rules.cs` |
| World generation, provinces, rivers, roads / the look-up grid behind `ProvinceAt`, `RoadDist`, `RiverDist` | `Campaign/WorldMap.cs` / `Campaign/MapIndex.cs` (exact: must answer as the full scans do, `SelfTest.MapIndexMatches`) |
| Prisoners, officers, food, contracts | `Campaign/CampaignWar.cs` |
| Career ranks, fealty, realm council, founding a realm, win/loss (F5) | `Campaign/Career.cs` (`Rank`, `Council`, `DeclareIndependence`, `CheckEnd`) |
| Fief prosperity/unrest/taxes, buildings, revolts, vassals and orders, defection (F6) | `Campaign/Fiefs.cs` (`FiefsDaily`, `Build`, `RaiseVassal`, `OrderedProvince`) |
| War exhaustion, peace terms, war declarations, tribute, alliances, hosts, summons (F7) | `Campaign/Diplomacy.cs` (`DiplomacyDaily`, `MakePeace`, `OfferPeace`, `MusterHost`) |
| Events, lord quests, brigand kings, pretenders (F8) | `Campaign/Events.cs` + `Data/events.json` (`EventsDaily`, `Choose`, `ApplyEffect`, `ValidateEvents`) |
| Stacks/promotion, characters/opinion, skills (tiers, retraining), the captain's lasting wounds, earned traits and age, the household guard and rank rewards, the pilot | `Campaign/Companies.cs`, `Characters.cs`, `Skills.cs`, `Wounds.cs`, `Traits.cs`, `Household.cs`, `Pilot.cs` |
| Your divisions on the map (make, assign, goal, temper, command, into battle) | `Campaign/Divisions.cs` |
| The roll of named men (docs/LEGACY.md): naming, fates in battle, following promotion, moves, splits and merges | `Campaign/Roll.cs` (`NameMan`, `RollFates` (auto-resolve), `RollFought` (fought battles), `RollAfterBattle`, `SyncRoll`, `RollPromoted`, `MoveNamed`; the fallen in `CampaignState.Fallen` via `FallenOf`, report `BattleReport.Roll`; sergeants and companions from the ranks `RiseToSergeant`, `SergeantFell`, `CompanionFell`, `RoseFrom`); in battle `Company.Named` -> `Unit.SoldierId` (`AddSquad`) -> `CompanyResult.Named`; HUD `Hud.NamedMarks`; save `Soldier`, `CompanyRecord.Roll`/`Fallen` (own stream `CampaignState.RollRng`); UI `MapScreen.RollPanel` (Muster.cs) |
| The company's legend (docs/LEGACY.md L5-L8): battle names, banner honours won and lost, fame and what it draws, retire and hand over / pulling a downed named man out in battle | `Campaign/Legacy.cs` (`BattleName`, `BannerAfterBattle`, `Fame`, `FamePay`, `Retire`, `HandOver`); UI THE LEGEND tab `MapScreen.Realm.cs` `Legend` / `BattleRescue.cs` (`UpdateRescue`, `Rescuable`, `Ward`); burial event `bury_named` (trigger `fallen`) |
| Monsters on the map: goblin holdouts (spawn, grow, raid, plunder, cleared), wolf packs, rare trolls, `MonsterThink` | `Campaign/Monsters.cs` (`SpawnHoldout`, `MonstersDaily`, `SendRaiders`, `HoldoutCleared`, `WolfThink`, `SpawnTroll`, `TrollThink`); docs/CREATURES.md |
| The dead rise: grave fields after big battles, burial, undead hosts on the map | `Campaign/Undead.cs` (`AfterTheBattle`, `GravesDaily`, `RaiseHost`, `UndeadThink`); `bury_dead` in `Data/events.json` |
| Sites on the map (ruins, shrines, stones, old battlefields, mines, hermits, watchtowers, springs, mills): visits and their effects | `Campaign/Sites.cs` (`Site`, `ArriveAtSite`, `SiteEffect`, `Search`, `Spy`); placed by `WorldMap.PlaceSites`; events `site_*` in `Data/events.json`; models `World.SiteModel`; docs/SIDE_CONTENT.md |
| More quests: village elders' jobs (ELDERS tab), escort, ransom, rescue, the relic chain, quest steps (`Contract.Then`) | `Campaign/Quests.cs` (`ElderOffers`, `MoreTownOffers`, `QuestDone`, `QuestLost`, `QuestStepDone`, `RelicQuest`); contracts in `CampaignWar.cs` |
| Town life: tavern rumours, tournaments as small battles (`BattleSetup.Tournament`, `BattleOptions.Tournament`), town flavour line | `Campaign/Town.cs` (`Rumours`, `Follow`, `PrepareTournament`, `FinishTournament`, `AutoTournament`, `Flavour`); `MapScreen.Settlement.cs` `TavernTab`, `EldersTab` |
| Your record: deeds, the chronicle's memory, the tally, the end summary | `Campaign/Record.cs` (`Deed`, `Tally`, `ChronicleEntries`, `Summary`); sight `Campaign.PlayerSight`/`InSight` (Campaign.cs); peace offers `SuePeace` (Diplomacy.cs) |
| Life on the roads: caravans between towns, pilgrims to shrines, meeting them (buy, news, rob) | `Campaign/Roads.cs` (`RoadsDaily`, `SpawnCaravan`, `CaravanThink`, `Arrive`, `CaravanLost`, `Rob`); `PartyKind.Caravan`; `MapScreen.MeetingPanel` |
| Realms and cultures data: lines, heraldry palette, home on the map, town flavour, opening wars (`relations`) | `Factions.cs` + `Data/factions.json` (founded realms come from the save via `Factions.Bind`); `WorldMap.GrowRealms` lays them out; a realm's temper (aggression, garrison, siege, tribute, hunts) is read in `Diplomacy.cs` (`WarAppeal`, `AlliancesDaily`, `Leading`), `CampaignAI.LordThink` and `CampaignSiege` |
| Campaign map screen: 3D map, input, camera | `MapScreen.cs` + `Ui.cs` (immediate-mode kit: `Button`, `Panel`, `Text`) |
| Campaign panels | `MapScreen.Hud.cs` (top bar, cards, tooltips, `Modal`), `.Settlement.cs` (town header + one `XTab` method per tab), `.Muster.cs` (party P as a muster roll), `.Captain.cs` (skills K as the captain sheet), `.Party.cs` (characters, upgrade, divisions V), `.Realm.cs` (R: realms, relations, chronicle; career tab), `.Atlas.cs` (M: realm map, province colours and borders, legend), `.Report.cs` (battle results), `.Event.cs` (illustrated event dialog), `.Dialogs.cs` (encounter, siege, beats, game over, menu) |
| Battle UI, camera, input, orders, effects | `BattleScreen.cs` (update) + `.Controls.cs` (input, camera) + `.Orders.cs` (orders, goals, `Send`) + `.Draw.cs` (render, particles, culling, detail, order arrows) + `.Effects.cs` (camera kick, hit pause, ground marks, trample map, falling banners; docs/VISUALS.md) + `.Shots.cs` (staged shots, pose sheets) + `Hud.cs` (2D drawing + hit-testing) + `Hud.Overlays.cs` (menu, after-action result, controls) + `Hud.Divisions.cs` |
| Keys / rebinding | `Controls.cs` (`Act` enum + `Defaults` dict) |
| 3D meshes (terrain, trees, towns, grass) | `World.cs` |
| Shader pipeline / GLSL loader | `Shading.cs`, `GlslEffect.cs`, `Shaders/*.glsl`, `Sky.cs` (light by hour/weather) |
| Unit drawing (puppets of part sprites) | `Puppet.cs` (poses, skeleton), `SpriteSheet.cs` (paints parts; the plate torso is a hand-made pixel map, `PlateFront`/`PixelMap`), `Painter.cs` (materials, surface patterns), `Art.cs` (atlas), `SpriteDump.cs` (`--sprites`) |
| Per-man looks: two men of one troop look different (face = skin x hair x beard, painted once as `head.v{n}.f/n`; kin helms and one-handed weapons; height). Drawing only, seeded by Soldier id or place in the battle | `Variety.cs` (`Of`, `Stature`, `Seed`); read in `Puppet.Draw`; `Look.Visage`; shots `gallery-variety*.png` |
| Colours, heraldry, UI tokens | `Style.cs` |
| Motion: easing curves, per-man phase / UI animation (`Ui.Anim`, `AnimLag`, `Since`, `Count`; `Ui.Now`, `Ui.Instant` freezes it for shots) / soldier and horse poses (`PoseOf`, `Fallen`, `Banner`) | `Ease.cs` / `Ui.cs` / `Puppet.cs`; rules in docs/DESIGN.md section 5 |
| The interface kit: every widget (`Plate`, `PlateBody`, `Ribbon`, `Shield`, `Banner`, `Icon`, `Ring`, `Pips`, `Gauge`, `Chip`, `Tabs`, `Gilded`, `Toast`...; `Fit`/`TextFit` cut text to a width) | `UiKit.cs` (static class `Kit`, extension methods on `Ui`; `Ui.Panel`/`Button`/`Bar` and the battle HUD draw through it) |
| UI lab (F3 on the title): gallery of interface concepts with mock data | `UiLab.cs` (screen, pages list, shots) + `.Pages.cs` / `.Scenes.cs` (one method per page) |
| Sound + music synthesis | `Audio.cs` (`Cue` enum; battle emits cues via `Battle.Sounds`; ground footsteps and their splashes, mud and leaves come from `BattleScreen.GroundSteps`) |
| Settings, crash log | `Settings.cs`, `SettingsScreen.cs`; tips: `Tips.cs` |
| Tools | `Replay.cs`, `BalanceReport.cs` (`Run`, `Pacing`, `Realms`, `Grounds`), `GpuReport.cs`, `Bench.cs`; `Overflow.cs` (shots runs: text leaving its panel -> `overflow.txt`) |
| Tests | `SelfTest.cs` (runner: `Run`, helpers, `Check`) + `Tests/BattleTests.cs`, `Tests/MeleeTests.cs`, `Tests/CampaignTests.cs`, `Tests/CareerTests.cs`, `Tests/CreatureTests.cs`, `Tests/MapTests.cs`, `Tests/TierTests.cs` (higher tier is better: `TierFight`, `TierSurvey`), `Tests/StyleTests.cs`, `Tests/EffectTests.cs`, `Tests/MagicTests.cs`, `Tests/CompanionTests.cs` |
| Developer guide (systems in depth, worked examples) | `docs/DEVELOPING.md` |
| Design docs | `README.md` (player-facing rules + architecture + gotchas), `docs/LEGACY.md` (THE player fantasy: your company is your legacy; plan L0-L8, read first when choosing work), `docs/DESIGN.md` (art/UI language), `docs/CAMPAIGN.md` (campaign spec, "As built" at end), `docs/ROADMAP.md` (phases A-E all built; "As built" lists simplifications), `docs/COMBAT_OVERHAUL.md` (plan: sergeant divisions + battle scale), `docs/MELEE.md` (plan: companies fight from their ranks, drill and order by tier, company wits: pin, wrap, pincer), `docs/CREATURES.md` (plan: monsters, creature encounters and troops), `docs/SIDE_CONTENT.md` (plan: sites, side quests, caravans, town life, events, polish), `docs/PROGRESSION.md` (plan: captain gear, combat, skill tree, rank rewards, battle goals), `docs/WORLD.md` (plan: bigger world, seven realms), `docs/TERRAIN.md` (plan: ground kinds, battlefield layouts, sight and cover, pathing, terrain-aware AI), `docs/POLISH.md` (plan: polish pass Q1-Q7: bugs, tips, text that fits, map keys, story, continuity tests), `docs/VISUALS.md` (plan: visuals and motion pass V1-V8: animation, effects, world, UI motion, big moments), `docs/COMPANIONS_MAGIC.md` (plan: one effect system for buffs and auras, tier V-VII troop attack styles and passive traits, mage troops and spells (buffs and attacks), companion callings, talents and abilities, faith, craft, omens), `docs/KNIGHT_ORDERS.md` (plan K0-K8: knight orders as minor factions of a patron realm with signature troops, the captain's aura tiers, forms, trials, the Black Flame, founding your own order) |

## Recipes

- **Add a test:** write `static void Name()` in the matching `Tests/*Tests.cs` file using `Check(cond, "message")`, and call it from `Run()` in `SelfTest.cs`.
  Scenario fields: `Battle.Empty(seed)` (flat terrain, two leaders) + `AddSquad`/`Spawn`, then loop `b.Update(1/60f)`.
- **Add/change a troop:** edit `Data/troops.json` (stats, `upgradesTo`, `look`, mount). `Troops.Validate` + `SelfTest.TroopTree`
  enforce: links exist, upgrades raise tier and are better, every troop reachable from `levies`. New look parts need painting
  code in `SpriteSheet.cs` (keys `part.variant.view/palette`).
- **Add a count of men or a per-man price in the campaign:** armies are `Balance.ArmyScale` (3) times the base numbers. Write a count
  as `Scaled(n)` and anything paid per man or derived from `Rules.Strength` as `Rules.PerMan(x)` (docs/DEVELOPING.md 5.7).
  With ArmyScale 1 the campaign must play exactly as before; compare `--selftest CampaignRun`'s pressure line at 1 and 3.
- **Add a realm:** an entry in `Data/factions.json` (`palette` = a new `Style.Sides` entry in both `Standard()` and `ColorBlind()`,
  with its own `Emblem` charge; `home`; four seven-troop `lines` of its culture, common or kin troops), a culture (root, `kin`,
  `flavour`), its `relations`, and place/lord names in `Data/names.json`. New maps need a new `WorldMap.CurrentLayout`
  (existing layouts must keep their realm homes). `SelfTest.SevenRealms` checks heraldry and lines.
- **Add a piece of captain gear:** an entry in `Data/gear.json` (slot weapon/shield/body/helm/horse; look = an existing sprite part;
  horse = a mount id from troops.json). `Gear.Validate` (run by `SelfTest.Armoury`) checks it.
- **Add a campaign balance value:** add the field with its default to `Balance` (CampaignState.cs) AND to `Data/campaign.json` (they must match; `SelfTest.BalanceMatchesData` fails otherwise).
- **Add a field to the save:** add a public field with a default on the class in `CampaignState.cs` (System.Text.Json, `IncludeFields`).
  Old saves still load (missing fields keep their default). There is no migration: bumping `CampaignState.CurrentVersion` makes
  older saves be refused (`SelfTest.RefuseOldSave`), so bump it only when old saves can no longer be read correctly.
- **Add a battle option from the campaign:** field on `BattleOptions`, set it in `Campaign.PrepareBattle`, read it in `Battle`.
- **Add a key action:** add to `Act` enum and `Controls.Defaults`; read with `ctl.Pressed(Act.X)`; list it in the F1 help (`Hud.Help`).
- **Add a campaign panel/button:** a method in the matching `MapScreen.*.cs` file using `Ui` (`ui.Button(...)` returns true on click); call the
  `Campaign` action and put a returned error string in the `note` field (drawn by `Note(r)`).
- **Add a shader parameter:** declare it in the effect's uniform list in `Shading.cs` (see `GlslEffect.Uniform`), use the name in
  the GLSL; set with `effect.Parameters["Name"].SetValue(...)`. GLSL rules: README "Shaders" section.
- **Add an event:** an entry in `Data/events.json` using an existing trigger and effect verbs; a new trigger or verb is a case
  in `Campaign.Find` / `ApplyEffect` plus its name in `Triggers` / `Verbs` (`SelfTest.EventsAndQuests` validates the file).
  `"scene"` picks the picture; a follow-up weeks later is a `flag:name` effect plus an event with trigger `flag:name`.
- **Try a new UI look:** add a page method in `UiLab.Pages.cs`/`.Scenes.cs`, list it in the `pages` array in `UiLab.cs`, build widgets in `UiKit.cs` (shared with the real screens); check with `IRONFIELD_LAB_SHOTS`.
- **Add a sound:** add a `Cue`, synthesize it in `AudioEngine` ctor, emit from the sim with `Sound(Cue.X, pos)`.

## Invariants and gotchas (break these and things fail subtly)

- Headless code (Battle*, Squad, Units, Troops, Terrain, Campaign/*) must not use MonoGame graphics/audio/input.
- Ground positions are `Vector2(X = world X, Y = world Z)`; height comes from `Terrain.Height(pos)`.
- Faction ids index `CampaignState.Factions`; realms founded in play (F5) carry `FactionState.Founded` (ids past the save's
  `DataRealms`; never test `id >= Factions.All.Count`, the data may have grown since the save). A map's realm layout comes from
  `CampaignState.Layout` (0 = the old fixed bands, kept for old saves; 1 = realms grow from `home` in `factions.json`;
  2 = also climates, ridges, four rivers on the 620 map). A seed's map must never change for an existing layout: put new
  generation behind a new layout number. Party counts scale with `WorldMap.PartyScale` (land area past the 392 map). Use the instance
  `campaign.FactionNames[f]` (not a static list) and `Factions.Get(f)`, which sees founded realms once `Factions.Bind` ran (the `Campaign` constructor does it).
- The career rank is derived (`Campaign.Rank`), never stored; `CaptainState.BestRank` only remembers which beats were shown.
- Team 0 = player side, team 1 = enemy. Palette 3 = bandits (and, for now, monsters). `PartyKind.Caravan` parties (merchants,
  pilgrims) belong to a realm but are not soldiers: they never hunt, and `Party.Prey` (you robbed them) makes them hostile to you. `PartyKind.Monster` parties have no
  faction (`SideOf` = -3; `Campaign.Outlaw` covers brigands and monsters: no pay, no drill). Troops with culture `wild` are creatures:
  never recruited, outside the upgrade trees, not taken prisoner. `Squad.Alive` is refreshed every tick by `UpdateSquad`.
- `AttackDir` is named from the attacker's side; the defender must block `Dirs.Mirror(swing)`.
- MonoGame `Color * float` also scales alpha; use `Color.Lerp` for tints (`Mat` forces opaque).
- Render targets sample upside down on GL: `uv.y = 0.5 - 0.5*ndc.y`. Vertex shaders end with `FIXUP_POSITION`.
- No MSAA (breaks context creation on Mesa/Xvfb); FXAA in post. HiDef profile is required.
- Self-test battles: with an idle player the AI enemy wins in ~30 s at every level; that is expected (balance assumes the player fights).
- If you change sim behaviour, the `--selftest` printed numbers (battle times, arrow losses...) move; that is fine as long as it ends `selftest ok`.
- Code style: file-scoped namespace `Ironfield`, nullable on, terse expression-bodied members, `///` summary comments,
  British spelling in user-facing text ("colour", "armour"), UI text in short plain sentences (see docs/DESIGN.md "Voice").

## Keeping this map current

- After adding, removing, renaming or moving types or methods: run `tools/codemap.sh` and commit `docs/CODEMAP.md`
  (`tools/check.sh` fails if it is stale).
- Keep files focused: a class that grows past ~600 lines gets split into partial files by area (`Name.Area.cs` for screens,
  `NameArea.cs` for the sims), and a method past ~80 lines gets its natural steps pulled out into named methods.
- After changing architecture, data flow, commands, env vars or a recipe above: update this file in the same commit.
- Player-facing rules belong in `README.md`; campaign design in `docs/CAMPAIGN.md`. Keep this file about navigating the code.
