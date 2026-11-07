# Ironfield campaign overhaul (roadmap phase F)

Status: **in progress (F0-F5 built).** This spec replaces the campaign's play and progression layer: how companies are built and
grown, how the captain rises, how lords, fiefs and realms behave, and what ends a campaign. The world map itself (generation,
terrain, rendering) is out of scope. Where this text and [`CAMPAIGN.md`](CAMPAIGN.md) disagree, this text wins once the
matching F step is built; until then `CAMPAIGN.md` describes the game as it is. Numbers marked *(tune)* are starting values.

**Next step: F6** (`ROADMAP.md` phase F). F0 added pacing telemetry: `--balance` runs a scripted stand-in captain
(`Campaign/Pilot.cs`) through each campaign and writes `balance/pacing.csv` and `balance/pacing_weekly.csv`. F1 built troop stacks,
promotion by the man, mixed squads, company identity and save v4; F2 built cultures, `Data/factions.json` and regional recruitment; F3
characters, opinion, heirs and companions; F4 the captain's skill tree; F5 career ranks, the realm council,
founding a realm by rebellion, and winning and losing; see the as-built notes in `ROADMAP.md`. The section 12 open
questions are not yet confirmed by the user; build to the bracketed proposals unless told otherwise.

Decisions taken with the user (2026-11-05):
- **All eight pillars** are in: career arc, lords and relations, cultures and regional recruitment, fiefs, strategic war and
  diplomacy, a captain skill tree, events and quests, and a pacing retune. On top of that, the **company system is reworked**.
- **Endgame: your own realm.** The player can rise from free company to founding a kingdom, and a campaign can be won.
  This retires the old goal "you are a captain, not a king" (`CAMPAIGN.md` 1.3): you *start* as a captain.
- **Saves break.** The overhaul bumps saves to version 4 and refuses older ones with a clear message. No v3 → v4 migration.
- **Delivery:** phased, one F step at a time, each step playable and committed, with its tests in the self-test.

---

## 1. Goals

1. **Every man is a unit of progress.** Companies are no longer one troop type upgraded all at once. Men are promoted one at a
   time from an XP pool, so a company changes gradually and the player always has a small, affordable choice to make.
2. **A career, not a counter.** Renown stops being the only ladder. The captain climbs ranks (sworn sword, vassal, marshal,
   king), and each rank changes what you *can do*, not just how many men you can hold.
3. **The world has people in it.** Lords have names, traits, opinions of you and memories of what you did. Who you fought beside
   or beat matters when you ask for a fief, a vote or an alliance.
4. **Where you are matters.** Each realm has a culture, and its villages and towns raise their own troops. Elite lines need land
   and buildings, so conquest and fiefs change your army.
5. **The war has goals.** Realms pursue objectives with marshal-led hosts, and wars end in terms (land, gold, captives), not
   just a truce timer.
6. **A campaign ends.** There are clear win and loss states, and each career stage has a target length so pacing is measured.

### Out of scope
World map generation and rendering, naval travel, trade goods and caravans, marriage and heirs, multiplayer, and the player
leading more than one party on the map (allied lords and vassals do that; see 6.3).

---

## 2. Companies and troop stacks (step F1)

### 2.1 The model
A **company** is a banner: a name, a role, a history and an officer. It holds **troop stacks**, one stack per troop id.

```
CompanyRecord                            TroopStack
  Id, Name, Role (Line/Spear/Bow/Horse)    Troop    troop id in troops.json
  Banner, Origin (culture, home)           Men      fit men
  Battles, Kills, Cohesion                 Wounded  recovering men
  Officer, Traits                          Xp       the stack's XP pool (a total, not per man)
  Honours (veteran perks, 2.4)
  Stacks: List<TroopStack>
```

- **What may share a company:** any troops whose **role** matches the company's role, from **any tree** and any tier. A Spear
  company can hold Spearrunners, Pikemen and Northmarch Tempest Spears side by side. The role comes from the troop's class and
  `reach` (the same rule as `Armies.RoleOf`). Horse troops only join Horse companies, so a company is all-mounted or all-foot.
- **Size:** a company's men cap comes from its role and is shared by all its stacks. The old tier factor (elite companies are
  smaller) becomes a weight: each man counts as `TierWeight(tier)` places *(tune: I 0.8 … VII 1.6)*, so a company of elites
  fills up sooner.
- **Party limits:** company slots and the total men limit stay renown- and rank-driven (`Rules.CompanySlots`, `Rules.MenLimit`),
  extended by the Leadership skill (F4) and rank (F5).

### 2.2 XP and promotion
- **XP flows into the stack's pool.** Per battle, each stack earns participation (20 per surviving man, x1.5 on a victory) and
  its share of the company's kill XP, split by men. Drill (1 a day in camp, 2 in a friendly town) adds per man to each pool.
  New recruits add men, not XP, so refilling a veteran company no longer dilutes it.
- **Promotion is per man.** When `pool >= troop.upgradeXp`, one man can be promoted: the pool loses `upgradeXp`, one man moves
  from this stack to the target troop's stack (creating it if needed), and the gold cost is `next.cost - troop.cost` (plus a
  horse for a move into cavalry, `CAMPAIGN.md` 6.2). "Promote all" repeats this while the pool, gold and the cap allow.
- **Pool cap:** `upgradeXp x Men` of the stack, so a stack cannot bank more XP than its men could spend.
- **Branches:** each promotion picks one target from `upgradesTo`. A Levies stack can send three men to Shieldmen and two to
  Bowmen. A man who leaves the company's role (a Levy promoted to Bowman in a Line company) moves to a company of that role,
  or opens a new company if a slot is free; if neither is possible the promotion is refused with a reason.
- **Where:** promotion needs a town training yard, a camp with the Drillmaster perk, or a fief with a barracks (F6).
- **The AI uses the same rule:** lords promote per man when they refit, cheapest first within their culture's preferred branches.

### 2.3 Battle bridge
- A company still deploys as **one squad**. Each `Unit` already has its own `Type`, so a squad may contain several troop types
  of one role; its formation and order logic use the role, and its speed is set by its slowest man.
  *Verify first in F1:* every place `Squad` assumes a single type (`Battle.AddSquad`, `Cavalry`, reach, brace, ammo) must accept a mix.
- The battle `Company` record carries its stacks (troop, count) instead of one `Troop`; `Battle.Result()` reports casualties
  per (company id, troop), so wounds and deaths come off the right stack.
- **Auto-resolve** sums strength per stack (the `CAMPAIGN.md` 5.4 formula is already per man).
- **Unit cards** show the largest stack's portrait and a tier strip (men per tier).

### 2.4 Company identity
- **Origin:** the culture and home settlement a company was raised in. Recruits of the same culture keep cohesion; mixing in
  men of another culture costs cohesion for a week *(tune)*.
- **Banner:** a generated device (field colour from the realm, a charge from a small set), shown on the map sprite and in battle.
- **Honours (veteran perks):** unlocked by history, at most three per company, shown on the card:
  - *Unbroken* (10 battles without a rout): rout threshold -4.
  - *Horse-breakers* (40 cavalry kills): +10% damage against mounted.
  - *Old Hands* (25 battles): drill XP x1.5.
  - *Stormers* (3 siege assaults): faster on ladders, +morale on walls.
  - *Bloodied* (lost half its men in one battle and won): +5 starting morale.
  Honours are data (`Data/campaign.json` `Honours`), so new ones are a data change.
- **Officers** (B5) stay, and an officer may become a **companion** (3.3).
- **Ledger:** a company keeps its last battles (date, foe, losses, kills) for the party screen.

### 2.5 Interface
- **Party screen:** each company expands to its stacks (portrait, tier, men/wounded, pool bar with "N READY"). Drag a stack
  between companies of the same role, drag men off a stack to split it, and click a ready stack to open the promotion picker.
- **Promotion picker:** the troop's `upgradesTo` as cards with stats, cost and `upgradeXp`; a count stepper (1, 5, max) and the
  reason it is blocked ("NEEDS A TOWN", "NEEDS 3 HORSES", "NO BOW COMPANY FOR THESE MEN").
- **`NextStep()`** points at the stack with the most ready promotions.

---

## 3. Lords, companions and relations (step F3)

### 3.1 Characters
A **Character** record is shared by lords, companions and the captain:

```
Character
  Id, Name, Faction, Culture, Rank, Age
  Traits      2-3 of: Honourable, Cruel, Cautious, Reckless, Greedy, Generous, Pious, Ambitious, Loyal, Fickle
  Opinion     character id -> -100..100 (the player is just another id)
  Memories    short list of (day, about, what, weight): "you rescued me", "you razed my village"
  PartyId, Fiefs, Captive (held by whom)
```

- Lord parties reference a `Character` instead of carrying a bare name. A defeated lord is captured or escapes and **comes back**;
  a slain lord (rare, 5% when routed *(tune)*) is gone, and an heir (a new character) inherits his fiefs.
- **Traits drive the AI:** Reckless lords accept worse odds, Cautious ones flee earlier, Greedy ones raid more and demand more
  for peace, Loyal ones never defect.

### 3.2 Opinion
- Opinion moves on events, each with a weight and a decay *(tune)*: fighting together (+), winning a battle they joined (+),
  defeating them (-, less if you released them), ransoming them back (+), refusing their quest (-), razing or looting their
  land (--), sharing a liege (+1 a week), different culture (-), and trait clashes (Honourable against Cruel).
- Opinion gates fief requests, council votes (5.3), defection to your realm (6.3), and alliance and peace terms.
- **Interface:** a **Characters** screen (lords by realm, with traits, an opinion bar and the top three memories behind it), and
  an opinion line on every encounter panel and party card.

### 3.3 Companions
- An officer who survives 15 battles *(tune)*, or a wanderer met in a town tavern (1 town in 4 *(tune)*), can become a
  **companion**: a Character in your service with traits and one **role**: Quartermaster (food and upkeep), Surgeon (wounded),
  Scout (vision), Drillmaster (drill XP) or Steward (fief income).
- Companions fight as a hero unit leading their company. From rank Lord (F5) a companion can be **raised to a vassal**: given a
  fief and a party, they become an AI lord loyal to you (6.3).

---

## 4. Cultures and regional recruitment (step F2)

### 4.1 Cultures
- `Data/factions.json` (new) lists each realm: name, heraldry row, **culture**, starting lords and preferred branches.
  Proposed cultures: **Azure** (south-west: heavy foot, crossbows, lances), **Crimson** (south-east: halberds, longbows,
  heavy horse) and **Northern** (Northmarch: axes, round shields, light horse). Brigands keep their own mob list.
- `troops.json` gains a `culture` per troop: `"azure"`, `"crimson"`, `"northern"` or `"common"`. Today the tree is one web with
  two roots (`levies` for the south, `clansmen` for the north) and 47 nodes with more than one parent, so F2 starts by **tagging
  the existing 108 troops** by lineage and look and splitting the southern web into Azure and Crimson branches. Common troops
  stay reachable by everyone.
- **New looks** only where a culture has none of its own (DESIGN.md section 8 rules: painted by `SpriteSheet`, tier table updated).

### 4.2 Recruitment
- A village raises **its culture's T1 root** (and a T2 of that culture at high prosperity *(tune)*). Conquered villages keep
  their culture, so an Azure lord holding northern villages gets Northern recruits.
- A promotion may target a troop of another culture only if the stack's own troop already lists it in `upgradesTo` within the
  common core; culture lines are entered from that culture's recruits. You cannot turn Azure levies into Northern berserkers.
- **Elite lines (T5 and up)** need a barracks of the right culture in a fief you or your realm holds (F6). Mercenary companies
  are the other way to get them.
- The owner's opinion of you sets how many recruits a settlement offers you (-50: none, 0: half, +50: all).

---

## 5. The captain: skills and career (steps F4, F5)

### 5.1 Skills (F4)
The eight perks become a **skill tree**. Skills rise by use (0-100), and every 25 points unlocks a perk choice on that branch:

| Skill | Rises by | Governs | Example perks |
|---|---|---|---|
| Leadership | battles commanded, men led | company slots, men limit, morale | Bannerman, Iron Will, Born Captain (+1 slot) |
| Tactics | battles won outnumbered | auto-resolve, deployment intel | Tactician, Ambusher, Siegecraft |
| Logistics | days marched, food carried | speed, food, upkeep | Quartermaster, Horsemaster, Forager |
| Training | men promoted | drill, promotion cost | Drillmaster, Veteran Cadre, Cheap Steel |
| Medicine | wounded healed | wound chance, healing | Surgeon, Field Hospital |
| Stewardship | fief income, days ruling | fief yield, building cost, unrest | Tax Roll, Builder, Just Ruler |
| Charm | opinion gained, ransoms | opinion gains, recruits, prices | Silver Tongue, Ransom Broker |
| Arms | your kills in battle | your hp and damage in battle | War Cry, Duellist, Mounted Lord |

- Renown no longer buys perks. It still raises the men limit and gates rank (5.2).
- Equipment (armour, helm and horse tiers) stays; the higher tiers need Arms skill *(tune)*.

### 5.2 Career ranks (F5)
| Rank | How you get it | What it unlocks |
|---|---|---|
| **Free Company** | the start | mercenary contracts with any realm, brigand hunting |
| **Sworn Sword** | a contract with one realm for 30 days *(tune)* | the realm's contract board, wages, its recruits at half price |
| **Vassal** | swear fealty: renown >= 150 and liege opinion >= 10 | a fief grant (F6), a council vote, summons to hosts |
| **Lord** | hold 2 fiefs, renown >= 350 | raise companions to vassals, propose wars in council, culture barracks |
| **Marshal** | a council vote (5.3) or the liege's choice | lead the realm's hosts: set its war objective, summon lords |
| **King** | found a realm (5.4) | diplomacy, your own council and vassals, the win condition |

Each rank is a **career beat**: a short event panel when reached and a new line in `NextStep()`. The rank replaces the
renown title `Campaign.Stage`.

### 5.3 The realm council
- Each realm holds a council every 14 days *(tune)*. Lords vote, weighted by fiefs, on what is pending: who receives a captured
  settlement, the next war objective, war or peace, and a new marshal. The AI votes by opinion and traits; you vote as a vassal.
- A lord whose liege keeps deciding against him loses opinion of the liege, which drives defection (6.3).

### 5.4 Founding a realm
- **Rebellion:** as a Lord holding a town, declare independence. Your liege's realm goes to war with you, and lords with a high
  opinion of you and a low opinion of the liege may follow, bringing their fiefs.
- **Claim:** with no liege (a free captain, or after leaving a realm), take and hold a town for 14 days and declare a realm.
You choose a name and a heraldry row; your culture is your capital's. As King you have diplomacy (7.2), your own council and vassals.

### 5.5 Win and loss
- **Win as King:** your realm holds 60% of the provinces *(tune)*, or every other realm is destroyed or tributary to you (7.2).
- **Win as Marshal:** if you are your realm's marshal when it reaches the same share, the campaign also counts as won (the
  kingmaker ending), so rebelling is a choice, not a requirement.
- A win shows a campaign summary and offers to continue in sandbox.
- **Loss:** in Ironman when the captain dies, or when your own realm loses its last settlement and you have no party.

---

## 6. Fiefs (step F6)

### 6.1 Settlement state
Settlements gain `Prosperity` (0-100), `Unrest` (0-100), `Buildings` and a `Culture`:
- Prosperity rises with peace and buildings, and falls with raids, sieges and high taxes. It scales income and recruits.
- Unrest rises under a foreign-culture owner, high taxes, a long siege and nearby raids, and falls with a garrison and the
  Stewardship perks. Above 70 *(tune)* a revolt spawns a rebel party; at 100 the settlement defects to its culture's realm.

### 6.2 Buildings
Data in `Data/campaign.json` (`Buildings`): name, cost, build days, settlement kinds, effect.

| Building | Effect |
|---|---|
| Training yard (1-3) | promotion allowed; levels 2 and 3 cut promotion gold by 10% and 20% |
| Barracks (per culture) | unlocks that culture's T5-T7 promotions |
| Walls (1-3) | siege defence, garrison cap, supplies |
| Market | income +20%, food prices -20% |
| Granary | supplies under siege, winter food |
| Stables | horses for sale, horse cost -30% |
| Chapel | unrest -10, Pious lords' opinion + |
| Watchtower | vision, early warning of raids |

### 6.3 Vassals and the AI
- AI lords with fiefs build by a simple priority: walls on the border, otherwise market, otherwise barracks.
- **Your vassals** (raised companions or defecting lords) are AI lords whose liege is you. You give them a standing order
  (defend my lands, follow me, join the host) but do not steer their parties.
- **Defection:** at each council, a lord whose opinion of his liege is below -30 and of another ruler above +30 may switch
  realm, bringing his fiefs. Loyal lords never do.

---

## 7. War and diplomacy (step F7)

### 7.1 Strategic war
- Each realm at war keeps an **objective**: a target settlement chosen by value (prosperity, culture, distance from its own seats)
  and set by the marshal or the council.
- The marshal raises a **host**: summons nearby lords (they come if their opinion of the marshal is above -20 and their party is
  fit), marches on the objective and besieges it. Lords not summoned defend and raid.
- **You can be summoned** as a Vassal. Answering raises the marshal's opinion of you; ignoring a summons lowers it. As Marshal you
  pick the objective on the map and summon lords yourself.
- **War exhaustion** per realm rises with losses, lost settlements and the war's length, and pushes the AI to sue for peace.

### 7.2 Diplomacy (Kings, and the AI between realms)
- **Actions:** declare war, offer peace with terms, propose an alliance (a shared enemy, joint hosts), demand or pay tribute, and
  exchange or ransom captive lords.
- **Peace terms:** cede a settlement, pay gold over 4 weeks, release captives, or become a **tributary** (a vassal realm that pays
  and joins your wars, and counts toward the win).
- **AI acceptance** scores war score, exhaustion, relative strength and the rulers' opinion of each other.
- `War` records gain exhaustion and terms; today's war-score truce timer goes.

---

## 8. Events and quests (step F8)

- **Events** are data (`Data/events.json`): a trigger on world state (famine after a siege winter, a lord who hates his liege, a
  brigand band past a size, a company earning an honour), a text, and 2-3 choices with effects (gold, opinion, unrest, men, a
  spawned party, a quest). At most one event every few days *(tune)*, never during a battle.
- **Lord quests:** a lord with a positive opinion may ask for help (escort, deliver a ransom, hunt a rival, take a castle for him).
  Rewards are opinion first and gold second. They reuse the contract machinery (B4).
- **Named threats:** a brigand band can grow into a **brigand king** (a Character with a large band and a lair), and a revolt (6.1)
  can raise a pretender. These are the mid-game set pieces.

---

## 9. Pacing (steps F0 and F9)

- **Target lengths** *(tune)*: Free Company to Vassal in 3-5 hours of play, Vassal to Lord in 4-6, to King or Marshal in 6-10 more,
  a full win in 20-30 hours. `--balance` reports the in-game day an AI stand-in reaches each career stage.
- **XP curve:** retune `upgradeXp` for T1-T7 (the docs were written for four tiers) so that, with per-man promotion, a company has a
  promotion ready about every second battle and the first T5 men appear around the Vassal rank.
- **Renown curve:** renown per victory scales with the foe's strength and your rank; ranks, not renown, carry the unlocks.
- **Telemetry first:** F0 extends `--balance` with a per-stage CSV, so every later step is tuned against numbers.

---

## 10. Architecture

- New files in `Campaign/`: `Companies.cs` (stacks, promotion, honours), `Characters.cs` (characters, opinion, memories),
  `Career.cs` (ranks, council, founding), `Fiefs.cs` (prosperity, unrest, buildings), `Diplomacy.cs` (objectives, hosts,
  exhaustion, terms), `Events.cs`, and `Skills.cs` replacing the `Perk` enum. `Campaign` stays one partial class across them.
- New data: `Data/factions.json` and `Data/events.json`; `campaign.json` gains `Honours`, `Buildings`, `Skills` and `Ranks`.
- The campaign stays headless: no MonoGame graphics references outside the screens.
- **Saves:** version 4. `Deserialize` refuses versions below 4 with "this save is from before the campaign overhaul", and the
  v1-v3 migration steps are deleted with the v3 format.

## 11. Testing (added to `--selftest` step by step)
- **F1:** a promotion moves exactly one man and `upgradeXp` from the pool; pools never exceed their cap; a mixed company deploys as
  one squad and results come back per stack; the auto-resolve calibration still holds (75%).
- **F2:** every troop has a culture; every culture has a path from its T1 root to a T7; villages recruit their own culture.
- **F3:** opinion stays in range and decays; captured lords return; traits change AI choices in a fixed scenario.
- **F5:** a scripted AI stand-in captain reaches Vassal; founding a realm starts a war with the old liege.
- **F6 and F7:** a 240-day AI run ends with settlements changing hands, at least one peace with terms, and no realm stuck with zero
  lords or a negative treasury.
- **F8:** every event in `events.json` parses, and its triggers and effects reference valid ids.
- **Determinism and round trip** (save, load, save) keep passing at every step.

## 12. Open questions (proposals in brackets)
1. **Southern culture split:** split the existing southern web into Azure and Crimson by look and lineage [yes, with the shared
   T1-T2 core marked `common`], or paint new trees?
2. **Promotion out of role:** move the man to another company automatically [yes, to the first company of that role with room], or
   forbid promotions that change role?
3. **Win threshold:** 60% of provinces [proposed], or all capitals?
4. **Companions in battle:** a hero unit like the captain [proposed], or only a stat bonus to their company?
