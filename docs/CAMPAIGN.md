# Ironfield campaign specification

Status: **implemented (M0-M5)**; roadmap phase F will replace much of it (see [`CAMPAIGN_OVERHAUL.md`](archive/CAMPAIGN_OVERHAUL.md)), with the open questions in section 13 settled by the proposals; see "As built" at the end
for where the build differs from this text. This document specifies the campaign layer that wraps the battles: what the player does between
fights, how armies persist and improve, and how the map hands a battle over and takes the result back.
Look and interface follow [`DESIGN.md`](DESIGN.md). Numbers marked *(tune)* are starting values for play-testing, not promises.

---

## 1. Goals

1. **The campaign feeds the battles.** Every system exists to make the next battle matter: who you fight, with which companies,
   at what tier, on what ground. There is no system that the battles do not touch.
2. **Companies are characters.** A company has a name, a history and a tier, and it remembers its battles. Losing your veteran
   Men-at-Arms should hurt. Upgrading Levies to Ironguard over a campaign is the main long-term reward.
3. **You are a captain, not a king.** You ride with your army, as in the battles (the Mount & Blade half). Strategy happens at the
   scale of one warband among lords and their hosts. Owning provinces is late-game, not the core.
4. **The AI plays by your rules.** Enemy lords recruit, pay upkeep, gain XP and upgrade along the same trees with the same costs.
   An enemy army of Knights earned them.
5. **Short sessions, readable state.** At any moment the player can see what each company needs (men, pay, an upgrade, rest).
   A 20-minute session should hold at least one meaningful battle.

### Out of scope for the first campaign release
Sieges, diplomacy beyond war and peace, owning settlements, marriage and family, trade goods, naval travel, and multiplayer.
Section 12 sequences the work; section 13 lists the decisions still open.

---

## 2. The core loop

```
   travel the map ──► encounter ──► battle (or auto-resolve) ──► results
        ▲                                                            │
        │      upgrade / recruit / heal / get paid in towns          │
        └──────────────── camp & settlements ◄─────────────────────────┘
```

- **Travel:** move your party across the world map in real time (pausable), chasing contracts, enemy parties and recruits.
- **Encounter:** meeting another party opens a dialog: attack, auto-resolve, retreat or parley.
- **Battle:** the existing battle game, built from both parties' companies, on ground generated from the map location.
- **Results:** casualties (killed and wounded), XP, loot, renown and prisoners are applied to persistent companies.
- **Camp and towns:** spend gold and XP. Upgrade companies along `troops.json`, recruit, hire mercenaries, heal, take contracts.

---

## 3. World map

### 3.1 Structure
- A **province graph**: about 60 provinces *(tune)*. Each holds 1 town or castle and 2-3 villages, joined by roads. Province borders
  follow ridges and rivers. Provinces are packed by `WorldMap.ProvinceSpacing` (66 units, spread by `(extent/196)^0.3`), so the
  spacing decides the count and `WorldMap.ProvinceCount` records the expected result.
- **Factions own provinces.** At the start the Azure Crown and the Crimson Compact each hold about half, and a band of contested
  marches lies between them. Bandit camps spawn in wild provinces.
- **Generated from a seed**, like battle terrain: the same seed always gives the same map (useful for saves and bug reports).
  A hand-authored map can replace it later without code changes (it is data).
- **Size.** The provinces span +-`WorldMap.Extent` (620 units, ~930 km across, about 115 provinces; maps from before
  docs/archive/WORLD.md W2 keep 392); the rendered ground reaches +-`GroundHalf`, the
  difference being a rim of mountains that closes the world in. The ground is drawn on a 4 m grid (`WorldMap.Step`), which is
  coarser than a battlefield's 2.5 m because it covers a much larger area; the mesh uses 32-bit indices past 65k vertices.
  A save records the extent it was made with (`CampaignState.MapExtent`), so campaigns started on the original small map keep
  that geography after the map was enlarged.
- **Climates and ridges (layout 2, docs/archive/WORLD.md W2).** Province kinds follow climate bands: forest and hills in the north,
  Steppe in the east, Badlands in the south, Marsh (fens) in the south-west, highlands in the middle. Two to five mountain
  ridges cross the land (`WorldMap.Ridges`): slow going (x0.45) and Hills battlefields, except at the passes where roads cross.
  Four rivers instead of two.
- **Scale factors.** `WorldMap.PartyScale` scales how many parties the world raises: provinces / the original 26 up to the
  392 map, then land area beyond it (so a bigger map stays as crowded per league). `WorldMap.ProvinceScale` (provinces / 26) remains for the rest;
  `WorldMap.Spread` (spacing / the original) scales distances that mean "nearby" — a lord's patrol goal, a siege's muster
  radius, a contract's "at the village" check — so a bigger map does not mean longer rides to everything.

### 3.2 Terrain kinds
Each map cell has a terrain kind. The kind sets map travel speed and **parameterises the battle terrain** generated where a fight happens:

| Kind | Map speed | Battle terrain (`TerrainParams`) |
|---|---|---|
| Meadow | x1.0 | gentle rolls, few trees, often a road |
| Farmland | x1.0 | flat, hedges and bushes, village props (later) |
| Forest | x0.7 | woods close to the core, more lone trees, cavalry penalised |
| Hills | x0.8 | 2-3 big hills inside the field, high ground matters |
| Road (overlay) | x1.3 | the battle field contains the road |
| River ford (later) | x0.6 | a shallow river across the field with a ford (needs water rendering) |

`Terrain` gets a constructor `Terrain(int seed, TerrainParams p)` with fields for hill count and height, roll amplitude,
wood count and distance, and road on or off. Today's `Terrain(seed)` becomes `Meadow`.

### 3.3 Time
- **Continuous time with pause**, as in Mount & Blade. 1 in-game hour = 0.5 s at x1 *(tune)*, with speeds x1, x3 and pause (Space).
- **Days and weeks** drive the economy: upkeep and income are settled weekly, recruit pools and healing tick daily.
- No day/night cycle in the first release. The sun direction in battle may later follow the hour.

### 3.4 Rendering (per DESIGN.md section 6)
- The map uses the battle world's language: a low-poly 3D heightmap on a coarser grid, the same sun, fog and grade, with
  low-poly settlement models (town walls, keep, village huts) in the world palette.
- **Parties are 2D banner-bearer sprites**: a mounted puppet in the leader's look, carrying a flag in the side colours.
  The sprite's size grows a little with party size. Hovering a party shows its card (leader, companies, strength).
- The battle-space minimap becomes a **world overview** in the same top-right slot.

---

## 4. Parties and companies

### 4.1 Company (the persistent unit)

> **Superseded by F1** ([`CAMPAIGN_OVERHAUL.md`](archive/CAMPAIGN_OVERHAUL.md) section 2): a company now holds stacks of troops with an XP
> pool each, and men are promoted one at a time. The text below describes the pre-overhaul whole-company model.
The battle's `Company(Troop, Count, Role)` becomes the battle-time view of a persistent **CompanyRecord**:

| Field | Meaning |
|---|---|
| `Id` | stable integer id (also used to route battle results back) |
| `Name` | generated ("The Ashford Shields", "Grey Hollow Bows"), renamable |
| `Troop` | troop id in `troops.json` (the tier comes from the troop) |
| `Men` | fit men who will fight |
| `Wounded` | men recovering, who do not fight |
| `Xp` | average XP per fit man (see 6.1) |
| `Battles`, `Kills` | history, shown on the card ("12 battles, 87 kills") |
| `Cohesion` | 0-30: recent victories, pay and losses; adds to starting morale in battle |
| `Role` | infantry, archers, spears, horse (deployment; defaults from the class) |

- **Company size caps** come from the same table that `Armies.Count` uses today: line base size times a tier factor
  (tier I x1.25, II x1.0, III x0.85, IV x0.7, V x0.65, VI x0.6, VII x0.55). Elite companies are smaller.
- **Merge and split:** two companies of the same troop may merge (their XP is weighted by men) up to the cap. A company may split in two.
- **Disband:** dismissed men go home, and a company at 0 fit and 0 wounded men is gone (its history goes to the party ledger).

### 4.2 Party
A party is a leader plus companies:

- **Player party:** you (the Captain) plus up to **10** companies (`CompanySlotsBase = 10`, available from the start without renown gating).
  Total men ≤ 40 + renown / 5 + Leadership / 5 *(tune)* (renown raises your total troop count; perks come from skills since F4).
- **Lord parties (AI):** the same structure with a named lord as leader. A lord's personal tier (equipment) scales with renown.
- **Bandit parties:** a leader-less mob of Levies and a few Bowmen or Outriders, up to tier II, that roams wild provinces.
- **Travel speed** is set by the slowest company (foot 4 km/h, an all-mounted party 7 km/h *(tune)*), times terrain, times wounded load
  (−1% per 2% of men wounded, up to −20%).
- **Vision:** 1.5 provinces' worth of map *(tune)*; Outriders in the party extend it by 25%.

---

## 5. Encounters and battles

### 5.1 Encounter dialog
When parties touch, time pauses and a panel opens, showing the enemy party card (leader, companies with tiers, strength bar) and:
- **Attack:** play the battle.
- **Auto-resolve:** calculate the result (5.4). It is disabled if you are personally hunted by a lord with ambition (story hook).
- **Retreat:** escape at the cost of a rearguard. Your slowest company loses 10-25% of its men, chosen by speed difference.
  You cannot retreat if the enemy is faster and has horse.
- **Parley:** bandits may be paid off; lords may accept a truce while at peace.

Nearby parties within 1 map cell may **join**: allies on your side (AI-controlled companies), enemies on theirs.

### 5.2 Building the battle (map → battle)
`BattleSetup`:
- **Seed:** a hash of the campaign seed, the day and the two party ids (deterministic).
- **Terrain:** `TerrainParams` from the map cell (3.2).
- **Armies:** an `ArmySpec` per side, built from fit men. `Company` gains `Id` and `Name` so results route back to records.
- **Starting morale:** 70 + Cohesion − 15 if unpaid (morale is a battle `Unit` field already).
- **Your Captain:** if wounded, you start in command view and cannot take the field (you still command; the battle already supports
  this state).
- **Size cap:** at most **~200 units** on the field at once *(tune)*. Larger sides deploy their largest companies first and hold the rest as
  **reinforcements** that march in from their edge when a company breaks or leaves the field.
  This **requires** the spatial grid for separation and target search (the README's `shortcut:`), milestone M0.
- **Allied companies** on your side are controlled by a team-0 AI general (the enemy general generalised to either side). You can
  still give them orders when they are attached to you (later).

### 5.3 Deployment phase (new battle state)
Before the fight starts, the battle opens paused in command view with a **deployment zone** (your third of the field), and:
- You can drag companies, draw their lines (the existing RMB line drawing) and set spacing and fire orders.
- The enemy is visible only if you have Outriders (scouted). Otherwise you see banners only.
- **Start Battle** (button, or Space) unpauses. Auto-deploy uses today's `Battle.Deploy` layout.

### 5.4 Auto-resolve
- Strength per side = Σ over men of `(MaxHp + 5·Armor + 2·Damage) · classFactor · terrainFactor`, the same weighting family as the
  HUD's balance-of-power bar, plus 10% per tier difference in the leaders' favour and ±15% randomness *(tune)*.
- The outcome rolls on the strength ratio. Casualties are spread over companies in proportion to their strength, weighted toward
  the losing side and toward the lower tier.
- The result has exactly the shape of a played battle (5.5), so everything downstream is shared.
- **Calibration test:** on a fixed set of matchups, the auto-resolve winner must agree with the headless battle simulation
  (AI vs AI, as the self-test already runs) at least 75% of the time.

### 5.5 Results (battle → map)
`Battle.Result()` returns a `BattleResult`:

```
BattleResult
  Winner              team (0 or 1), or -1 if you withdrew
  Duration            seconds
  Companies[]         per company: Team, Id, Start, Standing, Fled, Fallen, Kills, KillsByTier[1..7], Routed (bool)
  Captain             Unhurt | Wounded | Captured   (the player body: never killed, see 7.3)
  EnemyLeader         Unhurt | Wounded | Slain | Captured
  HorsesTaken         loose enemy horses on the field at the end (for the winner)
```

The battle sim stays headless (an existing rule). `Squad` gains a `Company` reference, so kills and casualties map back by `Id`.

Applying the result (`Campaign.Apply`):
- **Fallen become killed or wounded:** P(wounded) = clamp(0.30 + Armor/40, 0.25, 0.70) *(tune)*. If you lost, each enemy-held wounded
  man is captured with 50% chance (and can be ransomed or rescued later).
- **Fled men** return to their company after 1-2 days if the party survives, else they are lost.
- **XP** (6.1), **loot** (7.1), **renown** (7.3), **prisoners** (the winner's captured wounded; ransom value = troop cost x 2).
- **Results screen** in the battle's result overlay style. It shows company cards with men lost, XP gained (bars) and an **UPGRADE READY**
  badge, and Continue returns to the map.

---

## 6. Progression: XP and upgrades

> **Superseded by F1** ([`CAMPAIGN_OVERHAUL.md`](archive/CAMPAIGN_OVERHAUL.md) section 2): a company now holds stacks of troops with an XP
> pool each, and men are promoted one at a time. The text below describes the pre-overhaul whole-company model.

### 6.1 Experience
Per battle, for each company that took the field:
- Participation: 20 XP per surviving man, x1.5 for a victory *(tune)*.
- Kills: `Σ(victim tier) × 12 / men` added to the average *(tune)*. A 24-man Shieldmen company killing 10 tier-II men gains +10 per man.
- New men (replacements) arrive with 0 XP, so they **dilute** the average. This is the cost of refilling a veteran company.

With these numbers, Levies (60 XP) are ready to upgrade after 1-2 battles, tier II companies (150-200 XP) after about 4, tier III
(250-320 XP) after about 6, tier IV (390-460 XP) after about 8, and tier V-VI (550-800 XP) after 10-14 each. Tier VII is the top of
every line. That sets a campaign's pace: a tier IV army takes about 25-30 battles to build, a tier VI one most of a long campaign.
(The overhaul, `CAMPAIGN_OVERHAUL.md` 2.2, replaces the average with a pool per troop stack.)

### 6.2 Upgrading
- When `Xp ≥ troop.upgradeXp`, the company card shows **UPGRADE READY**. Upgrading needs:
  - **gold** = (next.cost − troop.cost) × men, and
  - a **town** (training yard), or a camp with a Drillmaster perk (7.3).
- **Branches:** Levies choose a line (Shieldmen, Spearrunners, Bowmen or Outriders). The upgrade dialog shows the tree from
  `troops.json` with portraits, stats, cost and the next tier. Choosing a branch fixes the company's line.
- **The whole company upgrades together** (it is one banner). Wounded men upgrade when they return. XP resets to 0 after an upgrade.
- **Horses:** upgrading into cavalry (Outriders) requires buying horses (`mount` hp x 0.5 gold each *(tune)*) or using **captured horses**
  from battles (`HorsesTaken`).
- The AI uses the same rule. A lord's companies upgrade when they visit their towns with the gold.

### 6.3 Data
`troops.json` already carries `tier`, `upgradesTo`, `cost` and `upgradeXp`. Additions:
- `upkeep` (gold per man per week; default `cost × 0.1`),
- `horseCost` on mounts (for 6.2),
- `recruitAt` ("village", "town" or "mercenary") for the troops that can be hired directly.

---

## 7. Economy and the captain

### 7.1 Gold
Currency: **crowns**. Starting purse: 300 *(tune)*. Sources and sinks:

| Flow | Rule *(tune)* |
|---|---|
| Upkeep (sink) | Σ men × troop.upkeep, charged weekly. A level-2 standard army costs about 180 a week. |
| Contract pay (source) | While sworn to a faction: 60% of your upkeep weekly, plus bounties on lords and bandit camps. |
| Loot (source) | 25% of the recruit cost of enemies killed or routed, to the winner. |
| Ransom (source) | Captured wounded: troop cost x 2 each, paid at a town of their faction or by the next peace. |
| Recruits, mercenaries (sink) | Levies `cost` each; mercenary companies `cost × 1.5` per man, sold whole. |
| Upgrades, horses (sink) | 6.2 |

**Unpaid weeks:** if upkeep cannot be paid, each company loses 10 Cohesion and 5% of its men desert per week unpaid; the lowest tiers leave first.

### 7.2 Recruits and settlements
- **Villages:** a pool of Levies (4-8), regrowing 1 a day *(tune)*. Hostile villages refuse. Raiding a village (later) empties it.
- **Towns:** a market (horses, gear), a **training yard** (upgrades), mercenary companies for hire (tier II-III, 1-2 on offer,
  refreshed weekly), healing (wounded recover 3x faster), and contracts (the faction steward offers hunts and escorts).
- **Castles:** a garrison (an AI party that does not move). There are no sieges in the first release, so castles anchor ownership and AI behaviour.

### 7.3 The Captain (you)
- **Renown:** +5 per victory, + enemy strength / 50 *(tune)*. It raises the party caps (4.2) and the quality of mercenaries on offer.
- **Perks:** *(replaced in F4 by the skill tree, `CAMPAIGN_OVERHAUL.md` 5.1: these eight now sit on the Leadership, Tactics,
  Logistics, Training, Medicine and Arms branches, opened by skill, not renown.)* Each extends something the battle already has:
  - *Bannerman* (aura radius +4 m)
  - *War Cry* (rally +10 morale, −3 s cooldown)
  - *Tactician* (auto-resolve +10%, see the enemy at deployment)
  - *Quartermaster* (upkeep −10%)
  - *Surgeon* (wounded chance +10%)
  - *Drillmaster* (upgrade in camp)
  - *Horsemaster* (horses −30%, party speed +5%)
  - *Iron Will* (companies near you never rout below 30% strength)
- **Equipment:** your Look upgrades through the same parts (mail → plate, sallet → great helm), and your horse from Courser to
  Warhorse to Destrier. These are bought in towns and appear on the battlefield and your map sprite.
- **Wounds and capture, never death:** if you fall in battle you are **Wounded** (out of the fighting for 3-5 days; battles start in
  command view). If your army also loses, you are **Captured**: you lose 30% of your gold, and your party escapes under your best
  company's banner and waits at the nearest friendly town. There is no permadeath in the first release (13.3).

### 7.4 Cohesion and morale
- Cohesion (0-30) per company: +5 per victory, −5 per defeat, −10 per unpaid week, −1 per 10% of the company lost in one battle,
  and +1 a day while resting in a town, up to 20.
- Battle starting morale = 70 + Cohesion − 15 if unpaid (5.2). Morale rules inside battle are unchanged.

---

## 8. Factions and AI

### 8.1 Factions
- **Azure Crown** (palette 0, chevron) and **Crimson Compact** (palette 1, saltire) start at war (with seven realms, docs/archive/WORLD.md
  W3: the Crown fights the Northmarch and the Compact the Khaganate, their neighbours). You start as a free captain who
  signs a contract with the Azure Crown in the tutorial (13.1).
- **Bandits** (a new heraldry row: grey-brown cloth, no charge, a tattered banner) are hostile to all.
- Each faction lists its troop trees in `factions.json`. At first both use the shared tree; culture variants are a data change (new looks).

### 8.2 Lord AI (per party, ticks hourly)
A short utility list, evaluated in order of score:
1. **Flee** if a stronger enemy is within vision and faster.
2. **Defend** a threatened own village or town within 1 province.
3. **Hunt** a weaker enemy party (including you) within vision.
4. **Raid** an enemy village (later; for now, "march to and threaten", which scares recruits away for a week).
5. **Refit:** go to an own town to recruit, upgrade and heal when below 60% strength or with an upgrade ready and the gold.
6. **Patrol** the faction's border provinces.

Lords obey the same economy: they are paid by their faction treasury, they pay upkeep, and they upgrade by the 6.2 rules.

### 8.3 Faction AI (ticks daily)
- Keeps a **war front**: the provinces bordering the enemy, ranked by threat.
- Assigns idle lords to front provinces. When it has 3 or more lords near each other, it forms a **host** (they move together and
  join battles together).
- Recruits new lords when it has fewer than its target number (its province count / 2) and the treasury allows.
- Hands out **contracts** for the player: hunt a named lord, clear a bandit camp, escort a supply column (later).

### 8.4 War and peace
- **War score** from battles won (strength-weighted) and villages threatened.
- Above a threshold, the losing faction offers peace (a 30-day truce). War resumes when the truce expires and the AI's strength ratio favours it.

---

## 9. Interface (per DESIGN.md)

All screens use the existing components: iron panels, unit cards, bars, buttons, the 5x7 font in uppercase, gold for *you* only.

- **Map HUD:**
  - Top centre: date, time controls (pause, x1, x3) and a week progress bar to the next upkeep.
  - Top right: world overview.
  - Bottom left: the Captain panel (renown, wounds, gold, weekly balance as +/−).
  - **Bottom centre: your company cards, the same component as in battle**, plus an XP bar and an **UPGRADE READY** badge (gold).
- **Party screen (P):** companies in a list with all fields of 4.1, merge, split, rename, disband, and the reorder that sets the 1-9 numbering.
- **Upgrade dialog:** the tree from `troops.json` drawn left to right with card portraits; the affordable next steps glow gold,
  unaffordable ones are dimmed and say why ("NEEDS 240 XP", "NEEDS 1,150 CROWNS", "NEEDS A TOWN").
- **Settlement panel:** tabs for Recruit, Mercenaries, Market, Train (upgrades), Rest and Contracts.
- **Encounter panel:** both party cards with a strength comparison bar and the four options (5.1).
- **Results screen:** 5.5.
- **Keys:** P party, M world overview zoom, Space pause, 1/2/3 time speed (map only), plus the existing F1 controls screen extended
  with a map column. All new actions go through `Controls`, so they are rebindable.

---

## 10. Architecture

### 10.1 Code layout
```
Campaign/
  CampaignState.cs   all persistent data (plain classes, JSON-serialisable): day, gold, renown, perks, parties, companies,
                     settlements, factions, rng state
  Campaign.cs        the headless simulation: Tick(hours) moves parties, runs AI, settles the economy, raises encounters
  WorldMap.cs        generation (provinces, roads, terrain kinds, settlements), queries, A* pathing on the road and cell grid
  Companies.cs       CompanyRecord rules: XP, upgrades, merge/split, wounds, caps
  Economy.cs         upkeep, pay, loot, recruit pools, mercenary offers
  AutoResolve.cs     5.4
  LordAI.cs, FactionAI.cs
  BattleBridge.cs    BattleSetup (map -> ArmySpec, TerrainParams, morale) and Apply(BattleResult)
  Save.cs            JSON save/load with a version number
Screens/
  ScreenHost.cs      the MonoGame Game: owns the device, art, shading, controls; switches screens
  BattleScreen.cs    today's BattleGame logic, moved (no behaviour change)
  MapScreen.cs       map rendering, input, map HUD
  MenuScreen.cs      title, new campaign, load, quick battle (today's random battle)
```
The rule that the battle sim is headless extends to the campaign: `Campaign` never references MonoGame graphics, so the self-test can run
a whole campaign.

### 10.2 Data files
- `Data/troops.json`: extended as in 6.3.
- `Data/campaign.json`: every *(tune)* number in this document (time scale, pay rates, wound chances, caps), so balancing needs no code.
- `Data/factions.json`: names, heraldry (palette row), troop trees, starting lords.
- `Data/names.json`: places, lords and company name patterns.

### 10.3 Saves
- JSON in `saves/` (in the user's data folder in release builds). One file per slot, plus an autosave on entering a battle and on each new week.
- Saves include the campaign RNG state; battle seeds are derived (5.2), so reloading before a battle replays the same field and armies.
- Saves are versioned. A loader upgrades older versions or refuses them with a clear message.

---

## 11. Testing

Headless, in `--selftest` (and a slower `--campaigntest`):
- **Determinism:** the same seed and inputs give the same state after 60 simulated days.
- **Round trip:** save → load → save gives identical JSON.
- **Invariants after a 120-day AI-only run:** no negative men or gold, no company above its cap, every troop id valid,
  every upgrade along the tree, and every party reachable on the map.
- **Economy sanity:** a level-2 army on contract with average fortune stays solvent for 8 weeks *(tune target)*.
- **Auto-resolve calibration:** at least 75% agreement with headless battles (5.4).
- **Bridge:** a battle built from a party and resolved maps every casualty and kill back to the right `CompanyRecord`.
- **Battle at the cap:** a 200-unit battle runs headless at ≥ 60 ticks per simulated second on the CI machine (needs the spatial grid).

---

## 12. Milestones

Each milestone ends playable, with its tests in the self-test.

| # | Milestone | Contents | Done when |
|---|---|---|---|
| **M0** | Battle-side prep | Spatial grid for separation and targeting; `Company.Id`/`Name`; `Squad.Company`; `Battle.Result()`; `TerrainParams`; deployment phase; team-0 AI general for allies | 200-unit battles at speed; results map back by id |
| **M1** | **Company ledger** (no map yet) | `CampaignState` with persistent companies; a **chain of battles** against escalating random armies; camp screen with recruit, heal, upgrade (dialog), merge and split; XP, wounds, loot; save/load | Levies can be grown into Ironguard over a run; the loop is fun without a map |
| **M2** | The map | Map generation and rendering, party movement and pathing, settlements, bandits, encounters → battles → results | Ride from town to town, fight bandits, get back to the map |
| **M3** | Economy | Gold, weekly upkeep, contracts, recruit pools, mercenaries, horses, cohesion | A level-2 army stays solvent as tuned; unpaid troops desert |
| **M4** | Lords and war | Lord AI, faction AI, hosts, war score and truces, auto-resolve | The two factions fight a war without you; you can swing it |
| **M5** | The captain | Renown, perks, equipment, wounds and capture | A captain's choices change how battles play |
| M6 *(later)* | Beyond | Sieges, owning settlements, diplomacy, rivers and fords, culture troop trees, raiding | separate specs |

M1 comes before the map on purpose. The progression loop (companies that persist, bleed, gain XP and upgrade) is the heart of
the campaign, and it can be proven in a week with existing screens before any map work.

---

## 13. Open questions (decisions wanted)

1. **Start:** a free captain with a tutorial contract (proposed), or a sworn vassal from day one?
2. **Time:** real-time with pause (proposed, Mount & Blade feel) or turn-based (Total War feel)? Everything above assumes real-time;
   turn-based would change only 3.3, 8.2 (per-turn AI) and the map HUD.
3. **Death:** wounds and capture only (proposed), or an optional ironman mode where the Captain can die?
4. **Scale:** around 24 provinces and 200-unit battles (proposed) as the first target, or bigger from the start?
5. **Sieges:** confirm out of scope for the first campaign release (they need walls, ladders and a new battle mode).
6. **Factions:** stay with two plus bandits, or add a third faction with its own troop tree (new looks, painted by `SpriteSheet`) in M4?

---

## As built (differences from the spec above)

The campaign was built to this spec with the section 13 proposals: a free captain under contract, real-time with pause,
wounds and capture but no permadeath, about 24 provinces, 200-man battles, no sieges, and two factions plus brigands. Where the
build differs:

- **A far larger world** (after the campaign was built): about 60 provinces across +-392 units, a much wider bandit population,
  ~250 parties and a coarse-grid campaign terrain. See 3.1 and the brigand-pressure note below for the sizes and the scaling rules.
- **Allies** who join a battle fight on your side under a team-0 AI general (`Squad.AiControlled`); only your own companies take your orders.
- **Map movement** is straight-line steering with terrain and road speed factors. There is no A* yet, because nothing on the map is impassable.
- **Battle size:** the cap is 400 men, served by a spatial grid (roadmap E1).
- **Drill (new):** companies gain 1 XP a day in camp and 2 in a friendly town (`DrillCamp`, `DrillTown`). Without it the AI lords
  rarely grew past tier II.
- **Lord wages** are 1.3× upkeep plus a 2% share of the faction's war chest each week, so lords can afford upgrades.
- **Prisoners** are held (roadmap B3): sold in towns and castles at the ransom rate, recruited, or freed. **Fled men** return at once (all on a victory, 70% on a defeat).
- **Code layout:** `ScreenHost` hosts `TitleScreen`, `MapScreen`, `BattleScreen` and `SettingsScreen` (top level rather than `Screens/`).
  The campaign sim, rules, map and state live in `Campaign/`, as specified.
- **Renaming** works (click a company's name in the party screen). Merge and split, reordering, disbanding and the rest of section 9 work.
- **The realm map** (M, docs/archive/WORLD.md W6) shows every province in its realm's colour with the borders between realms and a legend.
- **Map keys** are rebindable like every other key (settings screen or `controls.json`).
- **Later additions** (roadmap phase B): sieges and fiefs, a third faction (the Northmarch) with per-pair wars, prisoners, contracts,
  officers, seasons and food. Saves are version 3 (version 2 added the third realm's wars, version 3 the map extent); older saves
  are migrated on load, keeping the small map they were played on. See `docs/archive/ROADMAP.md`, "As built".
- **Progression is surfaced, not inferred** (roadmap A5). `Campaign.NextStep()` (`Campaign/CampaignProgression.cs`) is the single
  answer to "what should I do now", in priority order: unpaid men, no food, a wounded captain, a free company slot with men to fill
  it, a perk to choose, a company with upgrade XP, no contract, too many wounded, and finally the next renown rung. The map HUD shows it
  under the captain panel, the results screen shows the renown ladder and how many companies are ready, and arriving in a town opens
  the tab the hint names (`TRAIN`, `RECRUIT`, `MARKET`, `FOOD`, `REST`, `CONTRACT`) instead of always the first one.
  `Rules.NextRenownUnlock` names the next thing renown buys (+1 men cap) and the renown that pays for it;
  the HUD and party screen draw the same bar from the last rung to the next, and `Campaign.Stage` gives the captain a title
  (Mercenary Band → Lord of War) that both panels show. (Since overhaul step F5 the title is the career rank, Free Company → King.)
- **Brigand pressure.** `BanditsMax` is 80 and `BanditSpawnsPerDay` is 5 (`BanditSpawnChance` 0.95 each) while below that cap, so
  the roads fill with bands quickly. Both counts scale with `WorldMap.ProvinceScale`: the numbers in `Data/campaign.json` are quoted
  against the original 26-province map, so on the 60-province map the world holds around 190 bands (`Campaign.BanditsWanted`).
  A band's strength still scales with the campaign day (`BanditBandMin/Max`, `BanditGrowPerDays`, `BanditTierPerDays`), so brigandry
  stays a threat instead of free loot. Bands are opportunists: `BanditThink` runs from anything within 1.2× its strength and only
  falls on a party below 0.9×, which keeps many small bands from grinding the lords to nothing.
- **Lords pick fights.** `LordAggression` (0.35 a day) has a lord with no enemy in sight march for the nearest enemy province
  (`EnemyProvinceNear`, up to two hops out) rather than patrolling home; lords on campaign accept even fights (1.0× strength,
  1.3× on enemy ground). `LordsPerFaction` is 8 per 26 provinces and scales with `ProvinceScale` (`Campaign.LordsWanted`), but is
  also capped by the seats a realm actually holds (`seats * 2 + 2`): a realm raises only the lords its provinces can pay for.
  AI parties close to `AiClashRadius` (5.5) rather than the player's `EncounterRadius` when neither is yours, and `Vision` is 55.
  `Campaign.Encounters` indexes parties into a 16 m grid, so ~250 parties on the big map do not cost an O(n²) sweep each tick.
- **The war chest backs refits.** A lord who has lost men goes home regardless of his own purse, and `Refit` may draw on the faction
  treasury — each visit may take up to half the chest divided between the realm's lords (capped at `250 * ProvinceScale`), so a run
  of refits can never drain the chest past what it needs to raise new lords (600 each). Without this the first battle a lord lost
  was permanent: beaten, broke, never refit, and ground to nothing by day 120.
- **Pressure telemetry.** `Campaign.AiBattles` counts fights resolved without you, and `--balance` writes it plus every faction's
  treasury to `balance/economy.csv`. `SelfTest.CampaignRun` fails if bandits stop appearing, AI parties stop fighting, lord companies
  shrink below 18 men, or a treasury cannot afford a lord.
- **Debug:** `IRONFIELD_CAMPAIGN_SHOTS=/dir` walks through the campaign screens and saves a PNG of each; `IRONFIELD_SAVES` moves the save folder.
- **Each realm's own recruits and branches** (docs/archive/ANIMATION_ROSTER.md R3-R4). A realm's archer and horse lines start
  with its own tier II troop rather than the shared Bowmen and Outriders, and the four younger realms carry two or three branch
  chains each (tiers III-VII) reached by `upgradesTo` from their line troops, as the Crown and the Northmarch already had. Goblin
  holdouts at full strength may be led by a warlord (tier VI), and a great host of the dead raises wights, with a death knight once
  the Omen stirs.
