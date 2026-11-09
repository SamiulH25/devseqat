# Companions, magic, buffs and auras (plan)

Status: plan. Nothing here is built yet. Read docs/LEGACY.md first: the fantasy is that your company is your legacy.
Everything below must serve that. Companions are the named heart of the company. Magic is rare and old, and it costs you.
Buffs and auras are one system, so every bonus in the game reads the same way on the field and on the map.

("are systems" in the request is read as **aura systems**.)

## What exists today

- **Companions** (`Campaign/Characters.cs`, `Household.cs`, `Legacy.cs`): up to 6. Each has a camp role (Quartermaster,
  Surgeon, Scout, Drillmaster, Steward: one passive line each), traits, opinion and memories, and a Command skill for
  leading a division. A companion leads one company as its **Hero** unit (`Company.Hero`, `Unit.Hero`); if he falls he is
  wounded for a time (`HeroFell`). Some rose from the ranks (`Character.Ranks`), and one may succeed you (`Successor`).
  In battle a hero is one tougher man with +0.05 drill for his company. That is all.
- **Magic:** only for monsters. The necromancer raises the dead (`BattleCreatures.RaiseDead`) and his fall drops them
  (`MasterFalls`); trolls heal in the dark and not in the sun (`Regen`, `Sunlit`). Sites have flavour (shrines, hermits,
  springs, standing stones), and the relic quest chain exists (`Quests.RelicQuest`), but none of it is magic you can use.
- **Buffs:** spread everywhere, each coded by hand. Perks (`Skills.cs`), gear (`Gear.cs`), officer traits, earned traits
  (`Traits.cs`), lasting wounds (`Wounds.cs`), event effects (`Events.ApplyEffect`), food, weather and ground, honours.
  Each one is a special case in the code that reads it.
- **Auras:** three, each hard-coded. Your leadership aura (`BattleMorale`, `BattleOptions.AuraRadius`, wider with the
  Bannerman perk and a Lord's banner, gone while your banner is down), the sergeant's aura (+1.5 morale a second, +4 to
  routing men), and `Steadied` (sergeant or banner near: rabble hold, ranks re-form faster).

The problem: companions are a stat line, magic is something only monsters do, and every buff is its own little system
with its own UI (or none). The player cannot see why a company is fighting well or badly.

## Pillars

1. **One effect language.** Every bonus or malus, lasting or brief, personal or area, is an `Effect` with a name, an icon,
   numbers and a source. The HUD shows them all the same way. New content is data, not code.
2. **Companions are people with a calling.** A camp role for the road, a calling for the field, a talent tree that grows
   with their deeds, one or two battle abilities you can use, and opinions that can make them leave.
3. **Magic is low, old and costly.** No fireballs. Saints' relics, prayers, hedge-craft, weather-work and curses. A few
   casters, all of them people (companions or enemy champions) who can be killed. Each working costs the caster, and
   heavy use brings bad omens on the map.
4. **Readable on the field.** An aura is a faint ring on the ground when its bearer's company is selected. A working shows
   as a colour (gold for faith, green-grey for craft, violet for the dead) and a feed line. Counterplay is always visible.
5. **The rules that keep the game honest still hold:** headless sim, seeded `rng` only, replays exact, `--bench` within
   10 %, old saves load, auto-resolve agrees with fought battles (`--agree`).

---

## Part A. Effects: buffs and auras (the foundation)

### A1. The data

`Data/effects.json`, loaded by a new `Effects.cs` (headless) and validated like `Gear.Validate`:

```json
{ "id": "benediction", "name": "Benediction", "icon": "sun", "tags": ["holy"],
  "morale": 15, "moralePerSec": 1.0, "fear": -1.0, "block": 0.05, "duration": 20, "stack": "refresh" }
```

Stats an effect may change (all optional; multipliers default 1, flat values default 0):

| Group | Stats |
|---|---|
| Morale | `morale` (flat, on apply), `moralePerSec`, `fear` (scales horse fear, rout spread; -1 = immune), `routAt` |
| Fighting | `damage`, `block`, `armour`, `pierce`, `reach`, `swing` (windup and recover time) |
| Body | `speed`, `staminaPerSec`, `hpPerSec` (heal or bleed), `maxHp` |
| Ranks | `drill` (flat, battle only), `formedPerSec`, `breakRanks` (rabble's roll) |
| Bows | `spread`, `range`, `reload` |
| Campaign | `partySpeed`, `food`, `upkeep`, `healRate`, `sight`, `renown`, `goodwill`, `xp` |

`stack` is `refresh` (one copy, timer reset), `max` (the strongest counts), or `add` (up to `maxStacks`). Tags let
other things react: undead take holy damage, a ward stops `fear`, a curse is cleansed by `holy`.

### A2. Four kinds of source

| Kind | Where | Examples |
|---|---|---|
| **Standing** | the whole battle, from the campaign (`BattleOptions.Effects`) | perks, gear, traits, wounds, honours, a shrine's blessing, hunger |
| **Aura** | around a unit, `radius`, on the companies inside (own side, enemy, or both) | your leadership, a sergeant, your banner, a companion's calling, a priest |
| **Timed** | on a unit or a company, for `duration` seconds | an ability, a working, a war cry, being "Shaken" after a flank hit |
| **Field** | on an area of ground for `duration` | conjured mist, mud, a ward circle, consecrated ground |

### A3. How the battle reads them (cheap and exact)

- `Battle.Effects` (new partial, `BattleEffects.cs`). Each `Squad` keeps a small list of active effects; men read
  their **company's** totals (a man's own effects, for heroes and the captain, sit on the `Unit`). This keeps the cost per
  company, not per man.
- Totals are rebuilt every 0.5 s per company, staggered by `Squad.Id` like the wits cadence, into one `Mods` struct (a
  dozen floats). `Hit`, `Damage`, `Move`, `UpdateMorale`, `UpdateFormed` and `Shoot` multiply by the cached field: one
  read each, no lookups in hot loops.
- Auras test companies, not men: company centre within `radius + company radius` of the bearer. One pass over bearers
  (a few dozen) times companies (a few dozen) every 0.5 s.
- Field effects are a short list of circles; `GroundAt`/`Visible`/arrow cover ask it only when the list is not empty.
- Deterministic: timers count ticks; any roll uses the battle `rng`.

### A4. Moving today's rules onto it

Express the three auras and the scattered battle buffs as effects **with the same numbers**, then delete the special
cases: leadership aura (`BattleMorale`), sergeant aura, `Steadied`, Iron Will and Warlord, Rallying Cry, the troll's
sun and dark, the necromancer's fall. Test: every existing self-test passes unchanged, plus `EffectsMatchOld` (the same
battle with the old and new code paths gives the same `BattleResult`, before the old path is removed).

### A5. Buffs on the map

- `CampaignState.Buffs`: a list of `{ effect, untilHour, source }` on your party (and on lords' parties, so they can be
  blessed or cursed too). Campaign stats read the totals the same way (`partySpeed`, `food`, `healRate`...).
- New sources, all data: a shrine's blessing (3 days), a spring (wounded heal faster), a feast in a town's tavern (+morale
  into the next battle), a hermit's reading (sight), a good harvest event, hunger (when food runs out), sickness in camp
  (an event), a bad omen (Part C).
- Standing battle effects come from these, plus perks, gear, traits, wounds and honours, through `Campaign.PrepareBattle`
  into `BattleOptions.Effects`. Auto-resolve folds the standing effects into `Rules.Might`.

### A6. What the player sees

- Company card: up to three small effect icons under the gauges (the strongest first); tooltip: one line per effect with
  its source ("BENEDICTION (BROTHER OSWIN) 12 S").
- The captain panel and the map's party card: the same icons for party buffs, with days left.
- Select a company whose man bears an aura: a faint ring on the ground at the aura's edge, in the effect's colour.
- Feed lines when something big lands: "Brother Oswin blesses the Fallowby Shields."

---

## Part B. Companions 2.0

### B1. Role and calling

Keep the **camp role** (the road: Quartermaster, Surgeon, Scout, Drillmaster, Steward). Add a **calling** (the field),
chosen when he joins (from his past and traits) and fixed after:

| Calling | In battle | Aura (Part A) | First ability |
|---|---|---|---|
| **Sword** | a duellist; seeks the enemy's sergeants and champions | his company's front rank strikes harder | *Challenge*: call out the nearest enemy sergeant or hero to single combat; his fall shakes his company |
| **Shield** | stands in the front rank, hard to kill | his company blocks better | *Hold the line*: his company's ranks cannot break for 20 s, and re-form fast |
| **Banner** | carries a standard of his own | morale and steadiness, like a sergeant | *Rally to me*: routing men of your side within 25 m turn and rally |
| **Bow** | a marksman | his company's arrows spread less | *Mark*: one enemy company takes +20 % from all arrows for 30 s |
| **Lance** | leads horse | his horse re-form faster (M5's rally) | *Wheel*: his horse re-form and charge again at once |
| **Priest** | faith (Part C) | fear resistance | *Benediction* |
| **Cunning one** | hedge-craft (Part C) | none (people keep away) | *Call the mist* |

A calling also sets which gear suits him and which divisions he leads best (Banner and Shield steadier, Sword and Lance
bolder: feeds his `Temper`).

### B2. Growth

- Companions earn **XP from deeds**, the way the captain's skills rise: battles fought, enemies slain, abilities used
  well (an ability that turns a fight counts double), division battles led (`Command` stays as it is).
- Each level (up to 10) gives one **talent** from his calling's tree in `Data/companions.json`: three branches of four,
  the fourth needing two from its branch, like the captain's perks. A talent is an effect, a stronger aura, a shorter
  cooldown, or a second ability (at level 5).
- Lasting wounds (`Wounds.cs`) and earned traits (`Traits.cs`) work for companions as they do for you: a companion
  who survives ten battles may become *Grizzled*; one who loses an eye fights on with a malus.

### B3. Abilities in battle

- Each companion has one ability (two from level 5), with a cooldown (40-120 s) and, for casters, a cost in Will.
- **You** use them from the company's order wheel: one new slot that shows the hero's abilities when his company is
  selected (and a key, `Act.Ability`, listed in F1). Targeted ones use the order cursor.
- **The AI** uses them by rules in the same file: *Hold the line* when its company is flanked or below 0.6 Formed,
  *Rally to me* when a nearby company routs, *Challenge* when an enemy sergeant is within 15 m, and so on. Your companions
  use theirs by the same rules when you have not, unless you set a company to "abilities on my order".
- Enemy lords bring **champions**: named enemy heroes with callings and abilities, the same system on the other side.

### B4. People, not stat lines

- **Approval.** Each companion reacts to what you do, by his traits: robbing caravans, sacking a town, ransoming or
  freeing prisoners, sparing a beaten lord, burying the dead, using craft (the pious hate it). Approval is the existing
  `Opinion` of you, with `Memories` as the reasons, shown on his sheet.
- **Quarrels and bonds.** Two companions whose traits clash (the existing pairs) quarrel in events, and you settle it
  (one loses opinion). Two who fight near each other for five battles become **sworn brothers**: an aura when within
  20 m, and grief (a morale shock, a trait) if one falls.
- **Personal quests.** Each companion has one story in `Data/events.json` (flag chains, as the relic quest uses): find a
  lost brother, avenge a burnt village, take back a family's castle, atone for a deed. It pays out a talent, a relic or a
  trait. Ignore it too long and he may leave.
- **Leaving.** At very low opinion he warns you once (an event), then leaves, taking any relic he carries. A companion who
  leaves on bad terms may turn up later as an enemy champion.
- **Recruiting.** Wanderers in taverns stay. New ways in: a prisoner you free, a hermit met at a site, an enemy champion
  you beat in a duel and spare (`Duels.cs`), a named man promoted from the ranks (exists: `RiseToSergeant`, `RoseFrom`).

### B5. Legacy

- When a companion dies, his company keeps a memory (cohesion and a banner honour, as named men's deaths already do:
  docs/LEGACY.md L2-L5), and his gear and relic return to the armoury.
- A companion may take one named man of his company as **his student**. When the companion dies or retires, the student
  can rise as a new companion with the same calling and half his talents.

---

## Part C. Magic

### C1. Three traditions

| Tradition | Who | Flavour | Who dislikes it |
|---|---|---|---|
| **Faith** | priests (a companion calling), relics, shrines | prayer, blessing, warding the dead, healing | nobody, but a relic misused shames you |
| **Craft** | cunning men and wise women (a calling), hermits | weather-work, herbs, curses, bone-reading | pious lords and companions (`Pious` trait) |
| **Necromancy** | the enemy only (the necromancer exists) | raising, dread, decay | everyone |

Players do not get necromancy (see open question 1).

### C2. What it costs

- **Will** (casters only, 0-100): each working spends some; it comes back slowly in battle and fully overnight. A
  caster under 20 Will is *Drained* (an effect: slower, weaker) and cannot cast. This is the battle limit.
- **Omens** (the campaign limit). Every craft working adds to `CampaignState.Omen` (faith adds a little, and only when
  overused). Omen fades by a point a day. Past thresholds it draws events: crows over the camp (morale), a sick herd,
  a witch-hunter on your trail, villages that will not sell to you, grave fields stirring early (ties to
  `Undead.GravesDaily`). That keeps craft a spice, not a staple.
- **Relic charges:** a relic carried in the new **charm** gear slot holds a few uses of its blessing, refilled at a shrine.

### C3. Workings in battle

Each is an ability (B3) of a caster, built from Part A's effects:

| Working | Tradition | Kind | Effect | Counterplay |
|---|---|---|---|---|
| Benediction | Faith | aura burst | +morale, fear immunity, +block for 20 s on your companies within 20 m | kill or reach the priest |
| Ward | Faith | field | a 12 m circle the undead will not enter willingly; holy damage to those that do | fight round it, or wait it out |
| Last rites | Faith | timed | your fallen in 30 m cannot be raised by the necromancer | |
| Miracle | Faith | aura | once per battle and at great cost: every company of yours rallies and takes heart | a priest who calls one is drained for days |
| Call the mist | Craft | field | a 30 m bank of mist for 40 s: sight shortened, arrows through it spread wide (`Visible`, `Covered`) | close in, or move round |
| Bind the ground | Craft | field | a patch turns to mud for 60 s (a temporary `GroundKind`): horse slow, ranks loosen | do not charge over it |
| Evil eye | Craft | timed (enemy) | an enemy sergeant's or captain's aura fails for 20 s; needs a clear line (`ClearLine`) | kill the caster, keep him out of sight |
| Fair wind | Craft | aura | your bows within 25 m shoot farther and straighter for 30 s | |
| Fever | Craft | timed (enemy) | one enemy company loses stamina fast for 30 s | |
| Dread | Necromancy | aura | enemy morale drains near the necromancer | a priest's ward stops it |

Weather is already in the sim (`BattleOptions.Weather`, fog, rain, mud): field effects reuse it locally rather than
inventing new rules.

### C4. Magic on the map

Rituals are camp actions (new buttons on the party screen when a caster is in the company), each costing gold or herbs,
time, Will and Omen:

- *Bless the company* (priest): 3 days of +morale and fear resistance.
- *Read the bones* (cunning one): reveals parties within a wide radius for a day.
- *Call rain* (cunning one): wets the province (`RainDaily`), so the next battle there is heavy going for horse.
- *Lay the dead* (priest): buries a grave field (the existing `bury_dead` event, done at once).
- *Poultices* (either): the wounded heal faster for a week.

### C5. Creatures and magic

- Undead hate holy effects (tag `holy`): Ward, Benediction and Last rites matter most against them, so faith has a job.
- Trolls fear fire: a torch-bearer talent for Shield and Banner companions (a small aura: trolls will not stand in it).
- The necromancer becomes a proper caster on the same system (Will, Dread, Raise), so killing him first is still the
  answer, and a priest is the tool.

---

## Part D. Balance, scale, determinism

- **Bench:** effects cost a rebuild per company every 0.5 s and one multiply per read. Budget: `--bench 1500` within
  10 % of now (5.07 ms). Field effects and auras are lists of a few dozen.
- **Auto-resolve:** standing effects feed `Rules.Might`; a companion adds his calling's weight to his company. Extend
  `--agree` with a "with companions" run so fought and auto-resolved battles still agree.
- **Determinism:** ability AI and workings use the battle `rng` and tick timers; `ReplayExact` must pass.
- **Saves:** all new state is plain fields with defaults (`Character.Calling`, `Talents`, `Xp`, `Will`; `CampaignState.Buffs`,
  `Omen`). Old saves load: a companion without a calling gets one from his role and traits on load.
- **Idle player still loses in ~30 s:** enemy champions and casters must not change that at level 1-2.

## Part E. Tests

| Test | What it checks |
|---|---|
| `EffectsMatchOld` | the auras moved onto effects give the same result as before |
| `AuraReachesCompanies` | an aura reaches the companies within its radius and no others; it stops when its bearer falls |
| `StandingEffectsCarry` | a shrine blessing on the map is in the battle's options and in `Might` |
| `CompanionGrows` | deeds give XP, levels give talents, the fourth talent needs two of its branch |
| `HoldTheLine` | a Shield companion's company does not break while it lasts |
| `RallyToMe` | routing men in range rally; out of range they do not |
| `MistCoversArrows` | arrows through conjured mist spread wider; sight inside it is short |
| `WardStopsTheDead` | undead do not walk into a ward; a priest's fall ends it |
| `OmenDraws` | heavy craft use raises Omen and draws an omen event; it fades with days |
| `CompanionLeaves` | very low opinion gives a warning event, then he leaves with his relic |
| `EffectsValidate` | every effect, talent, ability and relic in the data is valid |

Plus shots: `gallery-auras.png` (rings and glows), `gallery-workings.png` (mist, ward, mud), a companion sheet in
`IRONFIELD_CAMPAIGN_SHOTS`, and `overflow.txt` empty.

---

## Build order

Each step ends with `tools/check.sh` green and a commit, as the melee work did.

| Step | What | Main files | Done when |
|---|---|---|---|
| **E1** | Effect core: data, `Mods`, battle reads, auras by company; move the three auras and battle perks onto it | new `Effects.cs`, `BattleEffects.cs`, `Data/effects.json`; `BattleMorale`, `BattleDrill`, `BattleCombat` | `EffectsMatchOld`, `AuraReachesCompanies`, bench within 10 % |
| **E2** | Effects on the map and into battle; HUD icons and rings | `CampaignState.Buffs`, `Campaign/Sites.cs`, `Town.cs`, `Events.cs`, `PrepareBattle`, `Hud.cs`, `MapScreen.Hud.cs` | `StandingEffectsCarry`, shots |
| **C1** | Companion callings, XP, talents, the companion sheet | `Characters.cs`, `Data/companions.json`, `MapScreen.Party.cs` | `CompanionGrows` |
| **C2** | Companion abilities: wheel slot and key, AI rules, enemy champions | new `BattleAbilities.cs`, `BattleScreen.Orders.cs`, `Controls.cs`, `Campaign/CampaignBattle.cs` | `HoldTheLine`, `RallyToMe` |
| **C3** | Approval, quarrels, sworn brothers, personal quests, leaving, students | `Characters.cs`, `Data/events.json`, `Legacy.cs`, `Roll.cs` | `CompanionLeaves` |
| **M1** | Magic core: Will, Omen, casters, field effects (mist, mud) | `BattleAbilities.cs`, `BattleSight.cs`, `TerrainGround.cs`, `CampaignState` | `MistCoversArrows`, `OmenDraws` |
| **M2** | Faith: priest workings, relics in a charm slot, shrines refill, undead interplay; the necromancer on the same system | `Data/gear.json`, `Armoury.cs`, `BattleCreatures.cs` | `WardStopsTheDead` |
| **M3** | Craft: workings and camp rituals, omen events | `Campaign/*`, `Data/events.json`, `MapScreen.Muster.cs` | rituals tested |
| **P** | Polish: glows and motes (docs/VISUALS.md), sounds, tooltips, README, `--agree` with companions, bench | `BattleScreen.Effects.cs`, `Audio.cs`, `README.md` | shots, `overflow.txt` empty, bench |

E1 alone pays for itself: it removes three hard-coded auras and gives every later step its language. C1-C2 are the
biggest win for the player. Magic comes after companions because its casters *are* companions.

## Open questions

1. **Necromancy for the player?** Proposed: no. It stays the enemy's, which keeps the undead frightening. (An optional
   dark path through an event chain could come later.)
2. **How visible is magic?** Proposed: subtle. Glows and mist, no beams or explosions; the feed tells you what happened.
3. **Can companions die for good?** Proposed: yes, but rarely. A fallen hero is wounded as now, and killed only on a
   second fall in the same battle, or a roll when his company is wiped out. The student (B5) softens the loss.
4. **Do enemy lords get champions from the start?** Proposed: from army level 3 up, one per lord, and more with rank, so
   early battles stay simple.
5. **Ability control.** Proposed: companions use their abilities on their own by default (most players will not micro
   six heroes), with a per-company toggle to keep them for your order.
