# Ironfield roadmap: what comes next

Status: **proposal for review**. Written after the campaign landed (`docs/CAMPAIGN.md`, milestones M0-M5).
Sizes are relative: **S** about a day of focused work, **M** a few days, **L** one to two weeks. Each item has a "done when" so it can be checked.

## Where the game stands

- **Battles:** cavalry, directional combat, morale and flanks, real-time command, 3D terrain with shaders and sun shadows,
  painted tiered troops, deployment, reserves up to 200 men.
- **Campaign:** a seeded map, persistent companies with XP and upgrades, economy, lords and factions, encounters, saves.
- **Missing entirely:**
  - **sound** (the game is silent),
  - a **settings screen** (only F2 and `controls.json`),
  - **onboarding** (a new player gets no guidance),
  - **builds for players** (you need the .NET SDK to run it),
  - **CI** (the self-test only runs by hand).
- **Not yet felt in play:** controls, pacing and economy are tested headless and in screenshots, but nobody has played a full campaign.
- **Code health:** about 8,400 lines. `BattleGame.cs` hosts the title, map and battle modes at about 1,000 lines, which slows every UI feature that follows.

## Principles for choosing

1. **Get it into players' hands before adding scope.** Features that let people play it and tell us what is wrong come first.
2. **Make the war matter.** Today the war cannot change the map. Sieges and ownership are the biggest missing *feeling*.
3. **Deepen what exists before adding new kinds of things.** Formations and smarter AI before new troop classes.
4. **Keep the rules:** the sims stay headless and tested, art stays code-painted and in the design language, and data stays in `Data/`.

---

## Phase A: playable by testers (next)

| # | Feature | Size | Done when |
|---|---|---|---|
| A1 | **Screen split** (the CAMPAIGN.md 10.1 layout): `ScreenHost` plus Title, Map, Battle and Settings screens; `BattleGame` becomes `BattleScreen` with no behaviour change | M | Every mode is its own class; the self-test and all screenshot scripts pass unchanged |
| A2 | **CI and release builds:** a GitHub Actions workflow builds and runs `--selftest` on Linux, then publishes self-contained Windows, Linux and macOS builds as downloadable artifacts | S | A push to the branch produces three zip files that run without the .NET SDK |
| A3 | **Settings screen:** resolution and fullscreen, UI scale, graphics quality, FXAA, mouse sensitivity and invert, volumes, and a **key rebinding editor** that writes `controls.json` (including the map keys, closing an "As built" gap) | M | Every option persists between runs; rebinding a key works in battle and on the map |
| A4 | **Sound, procedural first:** synthesised sound effects (steel on steel, shield thuds, bow release and arrow hits, hooves at walk and gallop, shouts for charge and rally, rout horns), positional in battle, with UI clicks and a map ambience. No files, in keeping with code-painted art. Music arrives later as files (decision D2) | M | A battle is readable with eyes closed: you can hear a charge land, and when your flank breaks |
| A5 | **Onboarding:** a first-campaign tip track ("click to march", "a town's training yard upgrades companies"...) shown once each, and a short guided first battle against brigands | M | A new player reaches their first upgrade without reading the README |
| A6 | **Crash safety:** unhandled exceptions are written to a log next to the saves, the previous save is backed up before overwriting, and the game reports "your save was restored" if loading fails | S | Killing the game mid-save never loses a campaign |
| A7 | **Balance telemetry:** `--balance` runs N headless campaigns and battles and writes CSVs (economy per week, company progression, win rates by tier) to tune `Data/campaign.json` against | S | One command shows whether a change made the economy harder or easier |

## Phase B: the war matters

| # | Feature | Size | Done when |
|---|---|---|---|
| B1 | **Sieges** (a new battle mode): castle and town walls as 3D models with a gate and towers. Attackers bring ladders and a ram, defenders hold the walls (units standing on height, firing down). On the campaign map, besieging takes days, the defenders' supply runs down, and you can assault or wait | L | A town can be taken and held. The walls make defenders on the walls clearly stronger, and starving them out works |
| B2 | **Ownership and fiefs:** captured settlements change faction; your liege may grant you a castle or town (weekly income, a garrison you set, recruits of your culture). AI factions retake ground | M | The map's colours change over a campaign, and a fief changes your economy |
| B3 | **Prisoners:** keep captured men instead of instant ransom. Recruit them (low cohesion, risk of desertion), sell them in towns, or free them for renown. Captured lords are held for ransom and take part in peace terms | S | The "prisoners are ransomed at once" gap in CAMPAIGN.md is closed |
| B4 | **Contracts with variety:** a contract board (hunt a named lord, clear a camp, escort a supply column, defend a threatened village, scout a province) with deadlines and pay | M | Each week offers a choice of goals, not just roaming |
| B5 | **Officers:** each company can have a named sergeant with a trait (Stubborn: rout threshold −5; Drillmaster: +1 drill XP; Hothead: charges early). Officers gain traits from battles and can die | M | Losing a veteran officer is felt; traits show on cards and in battle |
| B6 | **A third faction with its own culture:** northern clans (axes, round shields, horn helms, fur cloaks), a new troop tree painted by `SpriteSheet` (new look parts), alliances among three factions | M | Its troops are recognisable at a glance and follow DESIGN.md; the self-test validates the new tree |
| B7 | **Seasons and supply:** winter slows the map and drains food; parties carry supplies, which are bought in towns or foraged from villages (angering their owner) | M | Long winter marches need planning, and starving armies desert |

Small "As built" gaps to fold in alongside: renaming companies (text input, S), allied companies under an optional AI general (S), and A* pathing once rivers exist (S).

## Phase C: deeper battles

| # | Feature | Size | Done when |
|---|---|---|---|
| C1 | **Formations:** shield wall (block bonus, slow, braces), square (stops cavalry, weak to arrows), wedge (cavalry impact bonus), skirmish line. All on the order wheel and command buttons | M | Each formation has a situation where it is clearly right |
| C2 | **Battle AI 2.0:** keeps a reserve, flanks with infantry as well as horse, focuses archers on the softest target, pulls back routed companies, and withdraws when it is clearly losing. Enemy lords fight as hero units like you | M | Headless AI-vs-AI battles show flanking and reserves, and the calibration test still holds |
| C3 | **Captain combat:** a parry timing window (the README's open item), shield bash and kick, chambering, swapping weapons on foot, and calling your horse | M | Skilled play beats AI soldiers of the same tier reliably; spamming the attack does not |
| C4 | **Time of day and weather:** the battle's sun follows the campaign hour (dawn, dusk, night with torches); rain (bows less accurate, mud slows horses) and fog (shorter vision) | M | The same battlefield plays differently at dawn in rain |
| C5 | **Rivers, fords and bridges:** water rendering, wading slows and disorders, fords as choke points; on the map as crossings | M | A ford battle is a real tactical puzzle |
| C6 | **New troop roles:** horse archers, crossbowmen (slow and armour-piercing), two-handed infantry (no shield, big damage) | M | Each gets its own line in `troops.json` and a counter |
| C7 | **Fatigue and ammo carts:** running and fighting tire men (slower, weaker); archers can resupply at a wagon | S | Long fights favour fresh reserves |

## Phase D: presentation

| # | Feature | Size | Done when |
|---|---|---|---|
| D1 | **Battle effects:** dust under galloping horses, blood and spark particles, arrows sticking in shields and the ground, company banners carried in the ranks, animated falls | M | A cavalry charge looks like one from the command camera |
| D2 | **Music:** a small set of original or licensed tracks for the map, deployment, battle and victory/defeat, with smooth transitions | S once files exist | Mood follows the game state |
| D3 | **Accessibility:** colour-blind-safe side colours as alternative heraldry palettes, UI scale override, text for barks, and holding instead of tapping for the order wheel (already in) | S | The game is playable with deuteranopia |
| D4 | **Map life:** day and night, animated water, parties leaving trails, seasonal ground colours, smoke over threatened villages | M | The map tells the story of the war at a glance |
| D5 | **Battle info:** hover tooltips for enemy companies (troop, tier, state), a company detail panel, a post-battle timeline | S | You never need the README to read the battlefield |

## Phase E: technical health (alongside the above)

| # | Item | Size | Done when |
|---|---|---|---|
| E1 | Spatial grid for separation and targeting, then raise the battle cap to 400 | S | 400 men at 60 ticks/s headless |
| E2 | Save migration: versioned upgrade steps instead of refusing old saves | S | A v1 save loads in v2 |
| E3 | Real-GPU shader matrix: Windows (NVIDIA, AMD, Intel), macOS (Metal-backed GL), Linux Mesa; fallback verified | S | Known-good list in the README |
| E4 | Map foliage cost: instancing or merged chunks with distance culling | S | The map draws at 60 fps on integrated graphics |
| E5 | Determinism guard: battle seed and replay recorder (inputs plus seed reproduce a battle) | M | Any reported battle bug can be replayed exactly |

---

## Recommended order

1. **A1 screen split → A2 CI and builds → A6 crash safety:** a solid base, and builds anyone can run.
2. **A3 settings → A4 sound → A5 onboarding → A7 telemetry:** first play-tests, then tuning.
3. **B3 prisoners, then B1 sieges and B2 ownership** (the next big feature), with C1 formations interleaved so siege defence has shield walls.
4. Then B4/B5 (contracts, officers) and C2/C3 (AI, captain combat), guided by play-test feedback.
5. B6, B7 and the rest of C and D as the game's identity firms up.

## Decisions wanted

1. **Next big feature after the tester build:** sieges and ownership (recommended), a third faction, or deeper battles first?
2. **Music:** commission or license tracks, or stay procedural (ambient drones and drums)?
3. **Platforms:** Windows, Linux and macOS (recommended, since CI can build all three), or Windows only at first?
4. **Difficulty:** add settings (AI aggression, economy multipliers, ironman) now with the settings screen, or after balancing?
