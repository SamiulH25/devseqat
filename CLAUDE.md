# CLAUDE.md: Ironfield project map

Read this first; it is written so a new session can start work without exploring the code.
For a member-level index (every type and method with its line number) read `docs/CODEMAP.md`.
Keep both current: see "Keeping this map current" at the end.

## What this is

**Ironfield**: a 2.5D medieval battle + campaign game. C# / .NET 10 / MonoGame 3.8 DesktopGL. One project
(`Ironfield.csproj`, namespace `Ironfield`, ~13.7k lines). **No art or content pipeline**: sprites, terrain, font,
sound and music are all generated in code; shaders are plain GLSL compiled at runtime. The only data is `Data/*.json`.
`Content/Content.mgcb` has settings only, no content items.

## Commands

```bash
dotnet build -c Release                                  # ~30 s cold, warnings are expected (CS0649 on JSON DTOs)
dotnet run -c Release --no-build -- --selftest           # THE test suite (headless, ~20 s); CI runs exactly this
dotnet run -c Release --no-build -- --selftest Parry     # one test by SelfTest method name
dotnet run -- --balance 4                                # N headless campaigns+battles -> balance/*.csv (gitignored)
dotnet run -- --replay [file]                            # replay replays/last-battle.json, check same outcome
dotnet run -- --write-controls                           # write default controls.json
```

Rendering without a display (works in the cloud container, software GL via Mesa llvmpipe):
```bash
IRONFIELD_SAVES=/tmp/ifs IRONFIELD_SHOTS=/tmp/shots xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build
```
Saves ~20 staged battle PNGs (field-view.png, command-overview.png, gallery-*.png...) and quits. `IRONFIELD_CAMPAIGN_SHOTS=/dir`
does the same for campaign screens. ALSA/OpenAL "no audio" errors are harmless. Look at PNGs with the Read tool to check visual changes.

Other env vars: `IRONFIELD_SAVES` (save dir; settings go to its parent), `IRONFIELD_QUALITY=High|Medium|Low`,
`IRONFIELD_LEVELS=4,2` (quick-battle army levels), `IRONFIELD_WEATHER=hour,rain,fog`, `IRONFIELD_GROUND=river`,
`IRONFIELD_DUMP_ATLAS=/x.png`, `IRONFIELD_GPU_REPORT=1` (same as `--gpu-report`).
Default save/settings location: `$XDG_CONFIG_HOME|~/.config/Ironfield/` (ApplicationData). Set `IRONFIELD_SAVES` when running so you do not touch real saves.

## Environment

Cloud environment "devseqat" has a setup script that installs `dotnet-sdk-10.0` + `xvfb` from the Ubuntu 24.04 archive and
pre-builds. If `dotnet` is missing: `apt-get install -y dotnet-sdk-10.0 xvfb`. NuGet restore (MonoGame) needs package-registry access only.
CI: `.github/workflows/ci.yml` builds, runs `--selftest`, and on push publishes self-contained builds for win/linux/osx.
`.vscode/launch.json` still points at an old name (`Warband.csproj`): stale, ignore.

## Architecture in one screen

```
Program.cs  CLI switches (--selftest/--balance/--replay/--write-controls) else runs ScreenHost
ScreenHost  MonoGame Game. Mode {Title, Map, Battle}; owns Art, Audio, Settings, Controls (Ctl), Shading, Quality
  TitleScreen ── New/Continue/QuickBattle ──┐
  MapScreen (campaign UI + 3D map) ◄────────┤   SettingsScreen overlay on any mode
  BattleScreen (battle UI, camera, render) ◄┘

HEADLESS (no MonoGame graphics; must stay that way so --selftest runs):
  Battle (partial: Battle.cs, BattleSiege.cs, BattleCaptain.cs, BattleGrid.cs, Replay.cs)
     uses Squad, Unit/UnitType/Mount/Look (Units.cs), Troops (Data/troops.json), Terrain, BattleOptions/Weather
  Campaign (partial: Campaign/Campaign.cs, CampaignWar.cs, CampaignSiege.cs, Career.cs, Characters.cs, Companies.cs, Skills.cs, ...)
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
RecordFrame -> BuildGrid -> centroids -> `General(team)` every 0.5 s (enemy AI, and own side if AutoPlayer/AI squads) ->
Batter (siege gate) -> Reinforce (1 s) -> called horse, Resupply -> `UpdateSquad` each -> per unit: `UpdatePlayer` or `Think`
(`ThinkCav` for cavalry), `TickAction` (windup/strike/recover), `Tire` -> `Move` (steering, separation, terrain, wall) ->
`Braces`, `Lances` -> `UpdateArrows` -> `UpdateMorale` -> `CheckEnd` (sets `Winner`).
Damage path: `Strike` -> `Hit(attacker, target, ...)` (block/parry, flank via `SideOf`, speed bonus, slope) -> `Kill`. Horses: `HitHorse`.
Battle randomness uses its own seeded `rng` only: replays (`Replay.cs`) must reproduce exactly, so never use `Random.Shared`
or wall-clock time in the sim; `SelfTest.ReplayExact` checks it.

### Campaign tick (`Campaign.Tick(hours)`, 0.25 h steps, stops on `Encounter`/`Arrived`)
`Hourly` (lord/bandit AI: `LordThink`, `BanditThink`, contracts, `SiegeHourly`) / `Daily` (healing, drill, food `EatDaily`,
`FactionAI`, sieges, truces) / `Weekly` (upkeep, income, garrisons, prisoners, desertion) -> `Move` -> `Encounters`.
Player actions are public methods on `Campaign` returning `string?` (null = success, else the reason shown in the UI):
`Hire`, `RecruitLevies`, `BuyHorses`, `BuyGear`, `Upgrade`, `Split`, `Rest`, `SignContract`, `Besiege`, `Station`, `SellPrisoners`,
`SwearFealty`, `ProposeWar`, `SetObjective`, `DeclareIndependence`, `DeclareWar`, `OfferPeace`...
`Daily` ends with `FiefsDaily` then `CareerDaily` (rulers, councils due, rank beats, win/loss). Events the map must show once are plain fields:
`Encounter`, `Arrived`, `Report`, `Beat` (and the saved `S.Event`, an event waiting for a choice). Map screens on keys: P party, K skills, L lords, R realm (hard-coded in `MapScreen.Update`).

## File guide (where to look for what)

| Area | Files |
|---|---|
| Combat rules, AI, morale, cavalry, arrows | `Battle.cs` (`Think`, `ThinkCav`, `Hit`, `Strike`, `UpdateMorale`, `General` = AI general) |
| Formations, orders, slots | `Squad.cs` (`Order`, `Formation`, `Slot`, `SetLine`) |
| Sieges in battle / on the map | `BattleSiege.cs` (wall, ladders, gate `Via`) / `Campaign/CampaignSiege.cs` (garrisons, capture, fiefs) |
| Captain combat (bash, swap, horse call), fatigue, supply | `BattleCaptain.cs` |
| Spatial grid (`Near`, `Separate`) | `BattleGrid.cs` |
| Unit/troop types, look, mounts | `Units.cs`, `Troops.cs` (+ `Armies` presets), `Data/troops.json` |
| Battle inputs/outputs | `BattleTypes.cs` (`BattleOptions`, `Weather`, `BattleResult`, `CompanyResult`) |
| Terrain, rivers, foliage placement, siege wall geometry | `Terrain.cs` (headless) |
| Campaign data model + save/load/migration | `Campaign/CampaignState.cs` (also `Rng`, `Balance` <- `Data/campaign.json`, `Names`) |
| Company math, auto-resolve, army building | `Campaign/Rules.cs` |
| World generation, provinces, rivers, roads | `Campaign/WorldMap.cs` |
| Prisoners, officers, food, contracts | `Campaign/CampaignWar.cs` |
| Career ranks, fealty, realm council, founding a realm, win/loss (F5) | `Campaign/Career.cs` (`Rank`, `Council`, `DeclareIndependence`, `CheckEnd`) |
| Fief prosperity/unrest/taxes, buildings, revolts, vassals and orders, defection (F6) | `Campaign/Fiefs.cs` (`FiefsDaily`, `Build`, `RaiseVassal`, `OrderedProvince`) |
| War exhaustion, peace terms, war declarations, tribute, alliances, hosts, summons (F7) | `Campaign/Diplomacy.cs` (`DiplomacyDaily`, `MakePeace`, `OfferPeace`, `MusterHost`) |
| Events, lord quests, brigand kings, pretenders (F8) | `Campaign/Events.cs` + `Data/events.json` (`EventsDaily`, `Choose`, `ApplyEffect`, `ValidateEvents`) |
| Stacks/promotion, characters/opinion, skills, the pilot | `Campaign/Companies.cs`, `Characters.cs`, `Skills.cs`, `Pilot.cs` |
| Realms and cultures data | `Factions.cs` + `Data/factions.json` (founded realms come from the save via `Factions.Bind`) |
| Campaign UI (all panels: settlement, party, siege, upgrade, results) | `MapScreen.cs` + `Ui.cs` (immediate-mode kit: `Button`, `Panel`, `Text`) |
| Battle UI, camera, input, orders, effects | `BattleScreen.cs` (input) + `Hud.cs` (drawing + hit-testing) |
| Keys / rebinding | `Controls.cs` (`Act` enum + `Defaults` dict) |
| 3D meshes (terrain, trees, towns, grass) | `World.cs` |
| Shader pipeline / GLSL loader | `Shading.cs`, `GlslEffect.cs`, `Shaders/*.glsl`, `Sky.cs` (light by hour/weather) |
| Unit drawing (puppets of part sprites) | `Puppet.cs` (poses, skeleton), `SpriteSheet.cs` (paints parts), `Painter.cs` (materials), `Art.cs` (atlas) |
| Colours, heraldry, UI tokens | `Style.cs` |
| Sound + music synthesis | `Audio.cs` (`Cue` enum; battle emits cues via `Battle.Sounds`) |
| Settings, crash log | `Settings.cs`, `SettingsScreen.cs`; tips: `Tips.cs` |
| Tools | `Replay.cs`, `BalanceReport.cs`, `GpuReport.cs` |
| Tests | `SelfTest.cs` |
| Design docs | `README.md` (player-facing rules + architecture + gotchas), `docs/DESIGN.md` (art/UI language), `docs/CAMPAIGN.md` (campaign spec, "As built" at end), `docs/ROADMAP.md` (phases A-E all built; "As built" lists simplifications) |

## Recipes

- **Add a test:** write `static void Name()` in `SelfTest.cs` using `Check(cond, "message")`, call it from `Run()`.
  Scenario fields: `Battle.Empty(seed)` (flat terrain, two leaders) + `AddSquad`/`Spawn`, then loop `b.Update(1/60f)`.
- **Add/change a troop:** edit `Data/troops.json` (stats, `upgradesTo`, `look`, mount). `Troops.Validate` + `SelfTest.TroopTree`
  enforce: links exist, upgrades raise tier and are better, every troop reachable from `levies`. New look parts need painting
  code in `SpriteSheet.cs` (keys `part.variant.view/palette`).
- **Add a campaign balance value:** add the field with its default to `Balance` (CampaignState.cs) AND to `Data/campaign.json` (they must match).
- **Add a field to the save:** add a public field on the class in `CampaignState.cs` (System.Text.Json, `IncludeFields`).
  If old saves need conversion, bump `CampaignState.CurrentVersion` and handle it in `Migrate()`; `SelfTest.Migration` covers it.
- **Add a battle option from the campaign:** field on `BattleOptions`, set it in `Campaign.PrepareBattle`, read it in `Battle`.
- **Add a key action:** add to `Act` enum and `Controls.Defaults`; read with `ctl.Pressed(Act.X)`; list it in the F1 help (`Hud.Help`).
- **Add a campaign panel/button:** a method in `MapScreen.cs` using `Ui` (`ui.Button(...)` returns true on click); call the
  `Campaign` action and put a returned error string in the `note` field (drawn by `Note(r)`).
- **Add a shader parameter:** declare it in the effect's uniform list in `Shading.cs` (see `GlslEffect.Uniform`), use the name in
  the GLSL; set with `effect.Parameters["Name"].SetValue(...)`. GLSL rules: README "Shaders" section.
- **Add an event:** an entry in `Data/events.json` using an existing trigger and effect verbs; a new trigger or verb is a case
  in `Campaign.Find` / `ApplyEffect` plus its name in `Triggers` / `Verbs` (`SelfTest.EventsAndQuests` validates the file).
- **Add a sound:** add a `Cue`, synthesize it in `AudioEngine` ctor, emit from the sim with `Sound(Cue.X, pos)`.

## Invariants and gotchas (break these and things fail subtly)

- Headless code (Battle*, Squad, Units, Troops, Terrain, Campaign/*) must not use MonoGame graphics/audio/input.
- Ground positions are `Vector2(X = world X, Y = world Z)`; height comes from `Terrain.Height(pos)`.
- Faction ids index `CampaignState.Factions`; ids past `Data/factions.json` are realms founded in play (F5). Use the instance
  `campaign.FactionNames[f]` (not a static list) and `Factions.Get(f)`, which sees founded realms once `Factions.Bind` ran (the `Campaign` constructor does it).
- The career rank is derived (`Campaign.Rank`), never stored; `CaptainState.BestRank` only remembers which beats were shown.
- Team 0 = player side, team 1 = enemy. Palette 3 = bandits. `Squad.Alive` is refreshed every tick by `UpdateSquad`.
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
  (`tools/codemap.sh --check` tells you if it is stale).
- After changing architecture, data flow, commands, env vars or a recipe above: update this file in the same commit.
- Player-facing rules belong in `README.md`; campaign design in `docs/CAMPAIGN.md`. Keep this file about navigating the code.
