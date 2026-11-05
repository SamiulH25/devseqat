# Ironfield roadmap: what comes next

Status: phases A-E are **built**; phase F (the campaign overhaul, [`CAMPAIGN_OVERHAUL.md`](CAMPAIGN_OVERHAUL.md)) is **planned**; "As built" at the end lists where the build differs from the plan.
Written after the campaign landed (`docs/CAMPAIGN.md`, milestones M0-M5).
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

## Phase F: campaign overhaul (in progress)

The campaign's play and progression layer is rebuilt; the world map itself is untouched. Full spec:
[`CAMPAIGN_OVERHAUL.md`](CAMPAIGN_OVERHAUL.md). Saves break at F1 (version 4, no migration). Build in this order; each step
ends playable and committed, with its tests in the self-test.

| # | Feature | Size | Done when |
|---|---|---|---|
| F0 | **Groundwork:** commit the pending work, fix the stale four-tier text in DESIGN.md and CAMPAIGN.md, extend `--balance` with per-stage pacing telemetry | S | `--balance` writes a pacing CSV; the docs describe tiers I-VII |
| F1 | **Troop stacks and per-man promotion:** companies of one role holding stacks of many troop types, an XP pool per stack, promotion one man at a time, mixed squads in battle, company identity (origin, banner, honours, ledger), party screen and promotion picker, save v4 | L | Five Levies of one company can go to Shieldmen and two to Bowmen, and the mixed company fights as one squad with casualties per stack |
| F2 | **Cultures and regional recruitment:** `culture` on every troop, `Data/factions.json`, the southern tree split into Azure and Crimson, villages raise their culture, elite lines gated by barracks | M | Each realm's army is recognisable by its troops, and holding foreign villages gives foreign recruits |
| F3 | **Characters and relations:** `Character` for lords and companions, traits that drive the AI, opinion with memories, returning lords and heirs, companions with roles, a Characters screen | M | A lord you beat and released treats you differently from one you beat and ransomed |
| F4 | **Captain skill tree:** eight skills that rise by use, a perk choice every 25 points, replacing the `Perk` enum | M | Two captains played differently for 20 battles end with different skills and perks |
| F5 | **Career ranks and the council:** Free Company to King, career beats, the realm council (fiefs, objectives, war, marshal), founding a realm by rebellion or claim, win and loss | L | A player can swear fealty, get a fief, become marshal or rebel, found a realm, and win |
| F6 | **Fiefs:** prosperity, unrest and revolts, buildings, vassals with standing orders, defection | M | A fief's choices change its income and recruits, and a neglected one revolts |
| F7 | **Strategic war and diplomacy:** realm objectives, marshal-led hosts with summons, war exhaustion, peace terms (land, gold, captives, tributary), alliances | L | A 240-day AI run ends wars with terms, and settlements change hands through planned campaigns |
| F8 | **Events and quests:** data-driven events with choices, lord quests that reward opinion, brigand kings and pretenders | M | A long campaign shows events that depend on what happened in it |
| F9 | **Pacing retune:** `upgradeXp` for T1-T7, renown and rank thresholds, against the F0 telemetry | S | Career stages land within the spec's target lengths in `--balance` runs |

---

## Recommended order

1. **A1 screen split → A2 CI and builds → A6 crash safety:** a solid base, and builds anyone can run.
2. **A3 settings → A4 sound → A5 onboarding → A7 telemetry:** first play-tests, then tuning.
3. **B3 prisoners, then B1 sieges and B2 ownership** (the next big feature), with C1 formations interleaved so siege defence has shield walls.
4. Then B4/B5 (contracts, officers) and C2/C3 (AI, captain combat), guided by play-test feedback.
5. B6, B7 and the rest of C and D as the game's identity firms up.

## Decisions taken

1. **Next big feature:** sieges and ownership, then the third faction and deeper battles.
2. **Music:** procedural (drones and drums synthesised in `Audio.cs`), no files.
3. **Platforms:** Windows, Linux and macOS (Intel and Apple silicon) builds from CI.
4. **Difficulty:** in the settings screen now (Easy, Normal, Hard: enemy damage and contract pay), with Ironman for new campaigns.

## As built

All of A-E is in. Where the build differs from the table above:

**Phase A**
- A1: `ScreenHost` hosts `TitleScreen`, `MapScreen`, `BattleScreen` and the `SettingsScreen` overlay (one folder level, not `Screens/`).
- A3: mouse sensitivity and invert live in `controls.json`, as before, rather than on the settings screen.
- A4/D2: sound and music are both synthesised; there are no audio files.

**Phase B**
- B1 sieges: the wall runs across the whole field with a gate near the middle and two ladders (four with 90+ men; none if you
  assault before the night's work is done, so only the gate). Attackers on foot climb at the ladders or batter the gate (`Battle.Batter`);
  everyone fights on foot. Defenders' archers stand in one rank on the walkway, behind merlons (ranged hits from below do 60%), and
  elevated archers shoot 20% further. On the map: besiege (12 hours to build ladders), assault, auto-resolve (walls count 1.4×, 1.7× without
  ladders), wait, or lift. The garrison eats its supplies, then starves. Lords besiege when their host outmatches a garrison 1.6:1 and
  assault after two days.
- B2: a captured seat takes its province and villages. Your liege grants it to you as a fief: its income (and its villages') comes to you
  weekly, you can station companies in its garrison, and you hand fiefs back if you change sides. Garrisons are refilled from the faction's
  treasury. "Recruits of your culture": every village raises its own culture's recruits (clansmen in the north).
- B3: prisoners slow you and eat; sell them in towns and castles, recruit them (cohesion 0), or free them for renown. Captured lords are
  ransomed or freed; they do not take part in peace terms.
- B4: the steward's board offers hunt, clear, defend (stay near a village 24 hours), scout and take contracts, at most three at a time.
  Escorting a supply column was left out (there are no supply columns on the map).
- B5: officers are named sergeants with up to three traits from Stubborn, Steady, Drillmaster, Hothead, Sharpshooter, Surgeon and Veteran.
  Hothead adds 10% damage rather than charging early.
- B6: the Northmarch holds the north. Wars and truces are per pair of factions (`CampaignState.Wars`), so "alliances" are expressed as
  shared enemies. When one realm holds 45% of the towns and castles, the others make peace with each other and go to war with it.
- B7: four 30-day seasons; winter slows the map by a quarter, doubles the time to heal and makes men eat half again as much. Your liege's towns
  and castles feed you; elsewhere the quartermaster buys a week's food in friendly settlements, or you forage (renown cost at home,
  a raid abroad).
- Small gaps: companies can be renamed (click the name in the party screen), allied lords' companies fight under an AI general
  (`Squad.AiControlled`), and **A\* was skipped**: rivers are crossable everywhere, only slowly, so straight-line steering still works.

**Phase C**
- C1: shield wall, square, wedge and loose, cycled with J (and the FORMATION button); not on the order wheel, which keeps its eight orders.
- C2: an AI general keeps an infantry reserve, swings its outermost company wide, shoots the least armoured company, resupplies its archers,
  and sounds the retreat when it is down to a fifth of the enemy's strength. The enemy captain joins the melee once the lines are engaged.
  The auto-resolve calibration still holds (9 of 12).
- C3: parries need the guard raised within 0.35 s of the blow; striking into a blow from the mirrored side chambers it; Ctrl bashes with the
  shield (a kick with two hands); N swaps to a two-handed greatsword on foot (only parries stop blows from the wrong side); E with no horse in
  reach whistles for the nearest loose horse of your side.
- C4: the sun follows the hour (`Sky.cs`); nights are moonlit rather than torch-lit, because the shaders have no point lights. Rain and snow
  fall as streaks or flakes, scatter arrows and slow horses; fog and night shorten archers' sight.
- C5: rivers with two fords cross a third of quick battles and any campaign battle near a map river; the map's rivers have bridges where roads
  cross. There are no bridge models: on the map a crossing is shallower water.
- C6: crossbowmen (reload 2×, bolts ignore 60% of armour), horse archers (skirmish from the saddle), greatswords; and the Northmarch line
  (clansmen, axemen, huscarls).
- C7: the rank and file tire running and fighting (to 70% speed and damage); each side has a supply wagon of 300 arrows.

**Phase D**
- D1: particles are opaque pixel squares (sparks, blood, dust). Banners ride over the first man of each company. Falls topple over half a second.
  Arrows stick in shields (up to four).
- D3: colour-blind heraldry (Okabe-Ito blue, vermilion and purple) applies on restart. UI scale was already in A3.
- D4: day and night, seasonal ground, party tracks and smoke over threatened villages. **Water is not animated.**
- D5: hover any company in command view; select one for its detail panel; the results screen has a timeline.

**Phase E**
- E1: a 4 m grid (`BattleGrid.cs`) serves separation, targeting, morale, arrows and death shock. The cap is now 400 men: 351 fielded run at
  about 310 ticks a second headless.
- E2: saves are version 3 (version 2 added per-pair wars, version 3 the campaign map's extent);
  `CampaignState.Migrate` upgrades older saves and keeps them on the map they were played on.
- E3: a GPU matrix cannot be run from CI. Instead `dotnet run -- --gpu-report` draws a battle at each quality and writes `gpu-report.txt`
  (driver, shader status, frames per second); the README lists what has been verified.
- E4: foliage is built in 48 m tiles with bounding boxes, and each pass (camera and sun) draws only the tiles in its frustum.
- E5: every battle you fight is recorded (seed, armies, options, starting positions, each tick's input and your orders) to
  `replays/last-battle.json`; `dotnet run -- --replay [file]` plays it back headless and checks that it ends the same way.

**Phase F** (in progress; one entry per step as it lands)
- F0: the pending work built and passed the self-test unchanged. `Campaign/Pilot.cs` is a scripted stand-in captain (it signs with the
  Crown, hunts weaker parties and brigand contracts, recruits, promotes, buys food, and runs for a safe seat when outmatched); `--balance`
  runs it for 30 weeks per seed and writes `balance/pacing.csv` and `balance/pacing_weekly.csv`. First numbers (3 seeds): Sworn Company
  on days 18-32, Battle-Hardened on day 84 in one seed of three, 15-42 battles in 211 days. A company wiped out away from battle now
  regroups ten men at the nearest friendly town (before, it was left with no companies). `--selftest PilotTrace` prints a pilot's run.
- F1: `CompanyRecord` holds `Stacks` (troop, men, wounded, XP pool capped at `upgradeXp x men`); a company's places come from its role
  (foot 24, bows 16, spears 12, horse 10) and each man takes his tier's weight (I 0.8 ... VII 1.6). `Campaign/Companies.cs` promotes by
  the man: each spends `upgradeXp` from his stack's pool and the difference in troop price (plus a horse into cavalry, bought on the spot
  when the party has none) and moves to his new troop's stack, in the same company if it fits and has room, else the first company of
  his role with room, else a new company. The AI uses the same rule (`PromoteAll`). Battles get a `Mix` per company and spawn one squad
  of mixed men (largest stack first, marching at its slowest man); results come back per stack (`StackResult`) and wounds, captures,
  fled men and pool XP are applied per stack. Companies carry a culture, home, banner charge (a baked flag per side and charge, carried
  in battle), a ledger of the last 8 fights and up to 3 honours from `Data/campaign.json` `Honours` (defaults built in). The party screen
  shows stacks as chips (tier, men, pool bar, N READY); a chip opens the promotion picker (1 / 5 / all per branch, with cost and the
  reason when it cannot) with MOVE and SPLIT for the stack. Saves are version 4; older ones are refused with a message on the title
  screen and on quickload. Deviations from the spec: raw tier I recruits fit any foot company (so levies can drill in a spear or bow
  company until promoted); Stormers gives assault morale only (ladder speed is not modelled); mixing cultures costs 5 cohesion at once
  rather than over a week. Balance: AI recruits now fill only green (tier I) companies, a lord's promotions come from his own purse unless
  he came home beaten, and a war chest keeps a 300-crown floor when paying lords (without these the 120-day run bled one realm's chest
  to zero). Pacing (3 seeds, 211 days): Sworn Company on days 9-200, Battle-Hardened on day 78 in one seed; the pilot still bleeds men to
  unpaid upkeep between fights in two seeds, as before F1 (to fix with the pacing work).
- F2: every troop in `troops.json` has a `culture`. The common core is levies, shieldmen, spearrunners, bowmen and outriders, plus two
  tier III hubs where the web has no other way into a culture's line: lancers (Azure demi-lancers, Crimson knights) and billmen (Crimson
  halberds, Northern glaives). Azure (34 troops) keeps heavy foot, sergeants, pikes, crossbows and lances; Crimson (23) greatswords,
  halberds, longbows and heavy horse; Northern (43) axes, glaives, rangers, horse archers and light horse. `Data/factions.json` (loaded by
  `Factions.cs`, validated: seven troops per line, each an upgrade of the last, of the realm's culture or common) holds the realms and
  cultures and replaces the hard-coded lines in `Armies`; `Campaign.FactionNames` and `Style.PaletteOf` read it. Settlements carry a
  `Culture` from the realm that founded them and keep it when taken; villages raise their culture's root, and your recruits join a
  company of their own people (else a new one, and only with no free slot a foreign company). A man's culture is his troop's, or his
  company's for common troops; he may train into common troops or his own culture's. Tier V+ culture troops need a town of that
  culture in your realm (or held by you) as a stand-in for the F6 barracks. Mercenaries are hired from any realm's lines. AI lords
  now try every branch (realm line first): before, a refused first branch stopped a stack's promotions, which left the Crimson lords
  without a single promotion. Not yet: the T2 village recruit at high prosperity (F6) and recruits scaled by the owner's opinion (F3).
