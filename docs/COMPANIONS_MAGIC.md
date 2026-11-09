# Companions, magic, abilities, buffs and auras (plan)

Status: plan, being built. Done: T0 (see the build order). Read docs/LEGACY.md first: the fantasy is that your company is your legacy.
Everything below must serve that. Companions are the named heart of the company. Mages are rare, costly and real: they
fight in battle, cast buffs on your companies and strike the enemy. The best troops (tiers V-VII) fight differently: their
blows sweep, knock back or thrust through, and they carry passive traits. Buffs, auras, spells and abilities are one system, so every bonus reads the same way everywhere.

("are systems" in the first request is read as **aura systems**. The second request asked for: mages who use magic in battle
and give buffs (Part E), and special abilities, buffs or attacks, for high-tier units (Part D). A third note made those passive, never activated by
the player, with attacks that differ: sweeping, knockback and so on.)

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
3. **Magic is rare, old and costly, but it hits.** Saints' relics, prayers, hedge-craft, weather-work, runes and curses.
   Casters are people who can be killed: companions, enemy champions, and small companies of mages (Part E). Spells buff
   your side and some strike the enemy (lightning in a storm, wildfire in dry grass, smiting the dead), but each costs Will,
   gives warning before it lands, and heavy use brings bad omens on the map. No spell wipes a company.
4. **The best troops fight like the best.** A tier V-VII company has its own attack style (sweep, crush, knockback,
   thrust through) and passive traits. All passive: the men fight this way on their own; you choose it by what you raise.
5. **A higher tier is always better.** Every upgrade beats the troop it came from: in its stats, its style, its traits
   and in a fight. A style or trait is added *on top of* better stats, never paid for with worse ones, and no malus may
   make a tier VII company lose to its own tier VI at equal numbers. Tests hold this (D0, Part G).
6. **Readable on the field.** An aura is a faint ring on the ground when its bearer's company is selected. A working shows
   as a colour (gold for faith, green-grey for craft, violet for the dead) and a feed line. Counterplay is always visible.
7. **The rules that keep the game honest still hold:** headless sim, seeded `rng` only, replays exact, `--bench` within
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

Each is an ability (B3) of a caster, built from Part A's effects. Companion casters and mage companies (Part E) draw
from **one spell list** in `Data/abilities.json`; the table below is the companion's share, E3 the full list.

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

## Part D. Troop traits and attack styles (tiers V-VII)

Today a tier VII company is a tier IV company with bigger numbers. Here the top three tiers of every line **fight
differently**: their blows land in their own way (a sweep, a knockback, a thrust through two men) and they carry passive
traits. **Nothing here is activated by the player and nothing has a cooldown**: it is how these men fight, every blow, on
their own. You see it on the field and read it in the troop tooltip; you choose it by what you recruit and upgrade.

### D0. Higher tier is better (the hard rule)

Everything in this part sits on one rule: **an upgrade is better than the troop it came from, and a tier beats the tier
below it.** Today `SelfTest.TroopTree` only checks `hp + armour x 5 + damage` rises along each upgrade. That stays, and grows:

- **Stats never go down.** Along every upgrade link that keeps its weapon: hp, damage, armour and block do not fall, and at
  least two of them rise; for bows, range and pierce do not fall. (`Troops.Validate`, so a bad edit to troops.json fails
  at once.) A link that changes role (levies to bowmen, shieldmen to greatswords: block falls by design) is judged on a
  role-aware power score and on the fight test below instead. Today 23 of 235 links have a stat that falls; nearly all
  are such role changes (22 of them are block), and T0 sorts out the few that are not.
- **Styles and traits only add.** An upgrade keeps its parent's style at the same or higher rank and keeps every passive.
  Nothing is traded: a troop never gets a style in place of stats.
- **Maluses are bounded.** A style or signature with a cost (Whirlwind blocks less; Testudo moves slower) is measured with
  the cost on: the troop must still be better than its parent with the malus counted.
- **They win the fight.** A new test fights each upgrade link: 30 men of the troop against 30 of its parent, same ground,
  several seeds, and the higher tier must win clearly (most seeds, more men left). Across lines, a tier N+1 company beats a
  tier N company of a like class from the same culture at equal numbers.
- **The gap is felt but not absurd.** A tier step should be worth roughly 1.3-1.6x the men; a tier VII company should beat
  a tier IV company of twice its size (horse: one and a half times, for now: in the open the many ride round the few, and
  20 Grail Knights against 40 Demi-Lancers is a coin flip; tighten after T1's charge styles). `--agree` reports `Might` by tier so auto-resolve tells the same story.
- **Same for mages and companions.** A tier VI mage company out-casts a tier V one (more Will, stronger spells, never fewer);
  a companion's talents only add.

### D1. The rule by tier

| Tier | What the company gets |
|---|---|
| V | its weapon's **attack style** (D2) and one **passive trait** (D3) |
| VI | the attack style stronger (`rank: 2`: wider arc, harder knock, more chance) and a second passive |
| VII | both at full strength, plus its culture's **signature** (D4) |

Data: two new fields in `Data/troops.json`, `attack` (one style id) and `passives` (trait ids), with the numbers per rank in a
new `Data/styles.json`. They sit beside the creature `traits` that already exist (`sweep`, `knockback`, `fearless`...),
and reuse their code. `Troops.Validate` checks the ids; `SelfTest.TroopTree` checks that an upgrade keeps or improves its
parent's style and passives.

### D2. Attack styles (by weapon)

The battle already has two of these for creatures: `BattleCreatures.Sweep` (a blow carries into the men beside) and
`Knock` (a man thrown back and staggered, his neighbours shaken). D2 makes them general, sized for men (`rank` sets reach,
how many, how far), and adds the rest in the same place, called from `Strike` -> `Hit`.

| Weapon (tier V+ troops) | Style | How a blow lands | Good against |
|---|---|---|---|
| Dane axe (10 troops) | **Crush** | an overhead blow: half the target's block is ignored, a hit staggers him (`Stagger`) and may break his shield | shield walls, heavy foot |
| Greatsword (5) | **Sweep** | a wide cut: carries into one man beside the target (two at VII) at 0.6 of the damage | dense ranks, rabble |
| Longsword and shield (17) | **Riposte** | after a block or parry the next blow comes at once (no windup) and gets +30 % | duels in the front rank, other elite foot |
| Sword or axe and shield (6) | **Shield bash** | every third blow is a bash: little damage, but the man is knocked back a step (`Knock`, small) and his guard is open | holding a line, pushing a gap open |
| Pike (12) | **Thrust through** | a thrust that kills or wounds carries on to the man behind (the second rank) at 0.5 | columns, charging horse |
| Spear (9) | **Hold off** | a hit on a man coming at them knocks him back to spear's length; horse struck while braced rear and throw riders | horse, men trying to close |
| Lance (17 horse) | **Ride down** | a lance hit knocks the man down (`Knock`, large) and the horse tramples one more behind; at VII the lance can go through two | foot in the open |
| Sword on horse (1) | **Cut and wheel** | riding past, strikes every man in reach once without slowing | routing men, archers |
| Longbow (10) | **Plunging shot** | at long range arrows drop from above: shields to the front do not stop them | static foot, shield walls |
| Bow (3 foot, 4 horse) | **Quick loose** | shorter reload; horse archers shoot while moving | skirmishing, wearing down |
| Crossbow (12 foot, 3 horse) | **Punch** | a bolt within 20 m knocks a man back (`Knock`, small) and unhorses a rider on a bad roll; pierces plate | knights, captains |

Readability: each style has its own swing pose in `Puppet.cs` (overhead chop, wide arc, thrust, bash), a sound, and a visible
result (men thrown back, a second man falling, a rider unhorsed). In a pose sheet (`gallery-styles.png`) each style is shown.

### D3. Passive traits

Always on; each is an effect (Part A) on the company, so it shows as an icon on the company card with a tooltip.

| Trait | Who (examples) | Effect |
|---|---|---|
| **Unshakeable** | guard foot, Order foot | morale loss from flank hits and nearby routs halved |
| **Iron skin** | heavy foot, cataphracts | arrows from the front do less (+armour against pierce) |
| **Tireless** | spears, steppe foot | stamina drains slower (`staminaPerSec`) |
| **Locked shields** | shield foot | standing still: the front rank blocks more and arrows from the front mostly stop |
| **Set spears** | pikes, spears | braces form faster (`Braces`), horse fear them more |
| **Fearsome** | Dane axes, greatswords | enemy companies they fight lose morale faster |
| **Steady aim** | longbows, crossbows | less spread, keep shooting calmly when charged (shorter panic) |
| **Pavise** | crossbows with a pavise | reloading men take less from arrows; when halted, plant pavises as cover (`CoverAt`) |
| **Swift** | light horse, horse archers | re-form faster after a charge (M5's rally) and faster on the move |
| **Heavy charge** | heavy horse | more knock-downs and morale shock on the first impact (`Lances`) |
| **Sure-footed** | highland, marsh and steppe troops | no slow from mud, slopes or fords (`GroundAt`) |

Each troop gets traits that fit its look and its culture; at VI the second one is chosen to make the line distinct from
its neighbours (the three Northern Dane-axe lines should not feel the same).

### D4. Culture signatures (tier VII, passive)

| Culture | Signature | Effect |
|---|---|---|
| Northern | **Battle fury** | the more of them fall, the harder the rest strike (+damage as the company shrinks, no morale loss from losses) |
| Azure | **Testudo** | under arrow fire, a halted company raises shields overhead on its own: arrows from any side mostly stop, half speed |
| Crimson | **Headsmen** | blows against officers, sergeants and heroes do double damage |
| Southern | **Whirlwind** | every blow is a sweep (D2) on top of the weapon's style; they block a little less, never below their tier VI |
| Steppe | **Ghost riders** | horse that are charged fall back out of reach on their own and shoot as they go; chasers lose order (`Formed`) |
| League | **Paid in full** | do not rout while their side holds the field (they rout only when the battle is lost) |
| Order | **Zeal** | holy (tag): fear nothing; companies within 20 m take heart; the undead take +50 % from them |

### D5. How it reaches the battle and the campaign

- At `AddSquad` each man's style and traits are read once into his `UnitType` (no lookups per blow). Styles hook into
  `Strike`/`Hit` (Crush, Riposte, Bash, Thrust through, Punch), `Lances` (Ride down), `UpdateArrows` (Plunging shot) and
  `Braces` (Hold off); traits are Part A effects on the company.
- The AI does not decide anything new: the men simply fight this way. The general may value it (send Dane axes at a
  shield wall, pikes at horse) through the existing matchup weights.
- Tooltips in the muster roll, at the recruiter and in the upgrade panel list the style and traits, so "upgrade to tier V"
  reads as "these men will now cut through two ranks".
- Auto-resolve: styles and traits add a weight per tier to `Rules.Might`; `--agree` checks fought and auto-resolved still match.

---

## Part E. Mages

Companions with the Priest or Cunning calling (Part B) are the *named* casters. Mages are the same magic in **companies**:
a handful of casters you can hire, drill and lose, as you do any troops.

### E1. Mage troops

- A new class `Caster` (in `Units.cs` with the four there are). A mage company is small (6-12 men: `size` in the data),
  fights poorly in melee, wears little armour, and stays behind the line like archers (`BattleWings` bows rules: fall back
  behind the foot).
- Each culture has one **mage line** of three troops, tiers IV-VI, branching from a new common tier II troop, **Acolytes**
  (from Levies), so every troop stays reachable from `levies` (`SelfTest.TroopTree`):

| Culture | Line (IV, V, VI) | Tradition | Flavour |
|---|---|---|---|
| Order | Chaplains, Battle Priests, Saints Militant | Faith | blessing, warding, smiting the dead |
| Azure | Lectors, Magisters, Grand Magisters | Faith (learned) | wards and light |
| Northern | Rune-singers, Skalds, Galdr-masters | Craft (runes) | war songs, storm-calling |
| Steppe | Drummers, Shamans, Sky-callers | Craft (spirits) | wind, fear, horse-spirits |
| Southern | Star-readers, Astrologers, Sages of the Sun | Craft (stars) | foresight, heat, mirage |
| Crimson | Hedge-witches, War-witches, Crimson Crones | Craft (curses) | fever, evil eye, wildfire |
| League | Apothecaries, Alchemists, Guild Alchemists | Craft (alchemy) | smoke, fire pots, salves |

- **Getting them:** not from villages. Acolytes are hired in towns with a shrine or temple building (`Fiefs.Build`) and at
  shrine and hermit sites (`Sites.cs`); upgrading needs XP *and* a visit to such a place (one line in `Companies.Upgrade`).
  Realm armies field mages only from army level 3 up, at most one mage company per lord (open question 4 for champions).
- Mages are counted in `Rules.Might` by a fixed weight per tier (not their poor melee stats), tuned with `--agree`.

### E2. Will, for a company

- A mage company has one **Will** pool (0-100, Part C2): each spell spends from it, it refills slowly in battle (faster
  standing still) and fully overnight. Losing men lowers the pool's cap (it is the men who cast).
- Under 20 Will the company is *Drained* (slower, cannot cast). Faith spells cost less Will; craft spells add Omen (C2).

### E3. Spells: buffs and attacks

Each spell is an ability (B3) built from Part A's effects. Tier IV knows the first, V the second, VI all three plus its
tradition's great working. Every attack spell shows a **warning** first (a glow on the ground or the target, 1.5-3 s), so
the target can move or the player can react, and never hits more than a part of one company.

| Spell | Who | Kind | Effect |
|---|---|---|---|
| **Blessing** | Faith | buff aura | 20 s: +morale, +block, fear immunity for your companies within 20 m |
| **Ward** | Faith | field | 12 m circle: arrows into it spread wide, the undead will not enter |
| **Smite** | Faith | attack | a pillar of light on one spot (3 m): heavy damage to undead and creatures, light to men, men there are shaken |
| **Mend** | Faith, Alchemy | buff | wounded men in 15 m stop bleeding and regain some health (`hpPerSec`); more of the fallen survive as wounded |
| **War song** | Runes | buff aura | 30 s: +damage and +stamina for companies within 25 m; louder with more singers alive |
| **Call the storm** | Runes, Spirits | field | rain over a 40 m area for 60 s (wet ground, bows weak); in rain already: **Lightning** strikes the tallest target (a rider, a banner) in the area every few seconds |
| **Fair wind** | Spirits, Stars | buff aura | your bows within 25 m shoot farther and straighter |
| **Dread drums** | Spirits | debuff aura | enemy companies within 25 m lose morale and rally slower |
| **Foresight** | Stars | buff | 20 s: a company sees blows coming (+parry, flank hits count as front) |
| **Mirage** | Stars | field | a false company shown to the enemy AI (`Visible`) for 30 s: it may turn to face it |
| **Evil eye** | Curses | debuff | one officer, sergeant or hero in clear line (`ClearLine`): his aura fails for 20 s |
| **Fever** | Curses | debuff | one enemy company tires fast (`staminaPerSec`) for 30 s |
| **Wildfire** | Curses, Alchemy | attack + field | sets dry grass, scrub or woods alight (`GroundKind`): a burning patch that spreads with the wind for 40 s; men in it burn, horses bolt. Does nothing on wet ground or in rain |
| **Fire pots** | Alchemy | attack | thrown like javelins (range 30 m): a small burst, then burning ground for 10 s |
| **Smoke** | Alchemy | field | a 25 m bank of smoke: no sight, no aimed arrows through it |

**Great workings** (tier VI only, once per battle, most of the pool): *Miracle* (Faith: every company of yours rallies),
*Tempest* (Runes/Spirits: a storm over the whole field for two minutes), *Plague of crows* (Curses: every enemy company
in sight loses morale and order), *Greek fire* (Alchemy: a 15 m line of fire). They leave the casters Drained for the
rest of the battle and add much Omen.

### E4. Counterplay (always visible)

- Mages are soft: horse that reaches them wins (`BattleWings.HorsePrey` already hunts open archers; mages count as prey).
- Elite bows `Focus` casters first; enemy AI does the same to yours.
- Attack spells warn; a company that moves off the glow takes nothing. Ground spells depend on ground and weather
  (wildfire is useless in rain, lightning needs it), so the field and the campaign's weather (`Campaign/Ground.cs`) decide
  which mages are worth bringing.
- A Ward or Blessing beats Dread and Evil eye; killing the caster ends his auras and fields.

### E5. Mages and the AI

- The enemy general (`General`) keeps its mages behind its foot, casts buffs as the lines close, and aims attack spells at
  bunched, slow or static companies (braced pikes, a shield wall: magic is the answer to turtles, horse the answer to
  magic). Its rules are the spell's `when` in the data.
- Your own mage companies cast on their own by the same rules, like any troops' traits (Part D): you do not trigger
  spells. You steer them the usual way, by where you place the company and what you order it to do.

---

## Part F. Balance, scale, determinism

- **Bench:** effects cost a rebuild per company every 0.5 s and one multiply per read. Budget: `--bench 1500` within
  10 % of now (5.07 ms). Field effects and auras are lists of a few dozen.
- **Auto-resolve:** standing effects feed `Rules.Might`; a companion adds his calling's weight to his company. Extend
  `--agree` with a "with companions" run so fought and auto-resolved battles still agree.
- **Determinism:** ability AI and workings use the battle `rng` and tick timers; `ReplayExact` must pass.
- **Saves:** all new state is plain fields with defaults (`Character.Calling`, `Talents`, `Xp`, `Will`; `CampaignState.Buffs`,
  `Omen`). Old saves load: a companion without a calling gets one from his role and traits on load.
- **Idle player still loses in ~30 s:** enemy champions and casters must not change that at level 1-2.
- **Troop styles, traits and mages in `Might`:** a tier V-VII company's style and traits add a fixed weight per tier; a mage company
  counts by tier, not its melee stats. `--agree` gets "with styles" and "with mages" runs; fought and auto-resolved
  must still agree within today's margins.
- **Attack spells are capped:** a spell hits at most a part of one company (a 3-6 m spot or the target's front), so the
  `--balance` casualty share by spells stays under ~10 % of a battle's dead. `BalanceReport` gains a "killed by" column.
- **Bench:** styles add a branch per blow (Sweep and Thrust through do one `Near` query only on a hit); an ability's `when` rule runs on the wits cadence (every 0.5 s, staggered by `Squad.Id`); burning ground is
  one more field list. Wildfire spread is ticked on a coarse grid (the trample map's), not per man.

## Part G. Tests

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
| `EffectsValidate` | every effect, talent, ability, spell and relic in the data is valid |
| `UpgradeNeverWorse` | along every upgrade link no stat falls, at least two rise, the style's rank and the passives only grow |
| `HigherTierWins` | 30 of each troop against 30 of its parent, several seeds: the upgrade wins clearly, malus included |
| `TierGap` | tier VII beats twice as many tier IV of a like class; `Might` rises with every tier |
| `StylesByTier` | every tier V-VII troop has an attack style and passives that fit its weapon; upgrades keep or improve them |
| `SweepHitsBeside` | a greatsword blow carries into the man beside; a plain sword blow does not |
| `KnockThrowsBack` | a bash, a lance hit and a close bolt throw a man back and stagger him; the men behind him are shaken |
| `ThrustThrough` | a pike kill carries on to the man behind |
| `CrushBeatsShields` | Dane axes kill shield foot faster than plain axes of the same stats |
| `PlungingShot` | longbow arrows at long range get past shields to the front; at short range they do not |
| `NoTroopActives` | no troop has an ability to trigger: the order wheel shows none for a plain company |
| `MageWill` | each spell spends Will; under 20 the company is Drained and cannot cast; it refills overnight |
| `SpellWarns` | an attack spell glows before it lands; a company that steps off the glow takes nothing |
| `WildfireNeedsDry` | wildfire burns and spreads in dry grass and does nothing on wet ground or in rain |
| `HorseHuntsMages` | elite horse and elite bows go for an open mage company first |
| `MageLineReachable` | every mage troop is reachable from levies through Acolytes; upgrading needs a shrine or temple |

Plus shots: `gallery-auras.png` (rings and glows), `gallery-workings.png` (mist, ward, mud), `gallery-spells.png`
(warning glows, smite, lightning, wildfire, smoke), `gallery-styles.png` (crush, sweep, bash, thrust, ride down, a man thrown back), the
mage troops in a pose sheet (`--sprites`), a companion sheet in
`IRONFIELD_CAMPAIGN_SHOTS`, and `overflow.txt` empty.

---

## Build order

Each step ends with `tools/check.sh` green and a commit, as the melee work did. T0 and T1 need nothing from E1, so they
are built first: the troops change at once, and every later step is held to "higher tier is better".

**As built, T0:** `Troops.WorseUpgrade` (run by `Validate`) refuses an upgrade that keeps class, weapon and shield but loses
a stat or gains in fewer than two. Three links failed and were fixed in the data (Valhalla Reavers and Blood Jarls lost
armour and block; Pavisiers' block was an outlier above the next tier's). The fight survey (`--selftest TierSurvey`, all
same-role links, three seeds, ~4 min) found every upgrade already beats its parent. Tests in `Tests/TierTests.cs`:
`UpgradeNeverWorse` (plus `Might` rises along every link), `HigherTierWins` (25 links, one per class and tier step, two
seeds), `TierGap`.

| Step | What | Main files | Done when |
|---|---|---|---|
| **E1** | Effect core: data, `Mods`, battle reads, auras by company; move the three auras and battle perks onto it | new `Effects.cs`, `BattleEffects.cs`, `Data/effects.json`; `BattleMorale`, `BattleDrill`, `BattleCombat` | `EffectsMatchOld`, `AuraReachesCompanies`, bench within 10 % |
| **E2** | Effects on the map and into battle; HUD icons and rings | `CampaignState.Buffs`, `Campaign/Sites.cs`, `Town.cs`, `Events.cs`, `PrepareBattle`, `Hud.cs`, `MapScreen.Hud.cs` | `StandingEffectsCarry`, shots |
| **T0** | The "higher tier is better" rule first: stricter `Troops.Validate`, `UpgradeNeverWorse`, `HigherTierWins`, `TierGap`; fix any troop in the data that fails today | `Troops.cs`, `Data/troops.json`, `Tests/BattleTests.cs` | the three tests, before any style exists |
| **T1** | Attack styles: `attack` in troops.json, `Data/styles.json`; Sweep and Knock made general from the creature code; Crush, Riposte, Bash, Thrust through, Ride down, Hold off, Plunging shot, Punch; swing poses | new `BattleStyles.cs`, `BattleCreatures.cs`, `BattleCombat.cs`, `Puppet.cs`, `Troops.cs` | `SweepHitsBeside`, `KnockThrowsBack`, `ThrustThrough`, `CrushBeatsShields`, `PlungingShot`, shots |
| **T2** | Passive traits and culture signatures as Part A effects; tooltips in muster, recruiter and upgrade panels; `Might` weights | `Data/troops.json`, `BattleEffects.cs`, `MapScreen.Muster.cs`, `Rules.cs` | `StylesByTier`, `NoTroopActives`, the T0 tests still pass, `--agree` with styles |
| **M1** | Magic core: the ability engine (cooldowns, `when` rules) for casters, Will, Omen, the `Caster` class, field effects (mist, smoke, mud, rain, burning ground), warning glows | new `BattleAbilities.cs`, `BattleSight.cs`, `TerrainGround.cs`, `CampaignState` | `MageWill`, `SpellWarns`, `MistCoversArrows`, `OmenDraws` |
| **M2** | Mage troops: Acolytes and the seven mage lines, sprites (robes, staves, drums, censers), hiring at shrines and temples, AI armies field them, `Might` | `Data/troops.json`, `SpriteSheet.cs`, `Campaign/Companies.cs`, `CampaignAI.cs`, `Rules.cs` | `MageLineReachable`, `--agree` with mages |
| **M3** | Spells: buffs first (Blessing, War song, Fair wind, Mend), then attacks (Smite, Lightning, Wildfire, Fire pots), then great workings; AI casting and counterplay | `Data/abilities.json`, `BattleAbilities.cs`, `BattleAI.General`, `BattleWings.cs` | `WildfireNeedsDry`, `HorseHuntsMages`, `WardStopsTheDead` |
| **C1** | Companion callings, XP, talents, the companion sheet | `Characters.cs`, `Data/companions.json`, `MapScreen.Party.cs` | `CompanionGrows` |
| **C2** | Companion abilities on the M1 engine, the order wheel slot and `Act.Ability` (Priest and Cunning callings use M3's spells), enemy champions | `BattleAbilities.cs`, `Campaign/CampaignBattle.cs` | `HoldTheLine`, `RallyToMe` |
| **C3** | Approval, quarrels, sworn brothers, personal quests, leaving, students | `Characters.cs`, `Data/events.json`, `Legacy.cs`, `Roll.cs` | `CompanionLeaves` |
| **M4** | Faith and craft on the map: relics in a charm slot, shrines refill, camp rituals, omen events; the necromancer on the same system | `Data/gear.json`, `Armoury.cs`, `BattleCreatures.cs`, `Data/events.json`, `MapScreen.Muster.cs` | rituals tested |
| **P** | Polish: glows and motes (docs/VISUALS.md), sounds, tooltips, README, `--agree` with companions, bench | `BattleScreen.Effects.cs`, `Audio.cs`, `README.md` | shots, `overflow.txt` empty, bench |

E1 alone pays for itself: it removes three hard-coded auras and gives every later step its language. T0-T2 come next (T0 first, so every later step is held to "higher tier is better")
because the troops already exist: every veteran army fights differently at once, with no new controls. Mages (M1-M3)
build the ability engine for casters; companions (C1-C3) reuse it: a Priest companion casts the same Blessing a Battle
Priest company does.

## Open questions

1. **Necromancy for the player?** Proposed: no. It stays the enemy's, which keeps the undead frightening. (An optional
   dark path through an event chain could come later.)
2. **How strong and how visible is magic?** Proposed: real but bounded. Mages buff and strike (Smite, Lightning,
   Wildfire, Fire pots), but every attack warns first, hits a part of one company at most, and costs Will and Omen.
   Glows, light, fire and storm; no beams or exploding men. If you want openly high magic (fireballs, many mages per army),
   this is the place to say so: it changes E3's caps and the `--agree` targets, not the structure.
3. **Can companions die for good?** Proposed: yes, but rarely. A fallen hero is wounded as now, and killed only on a
   second fall in the same battle, or a roll when his company is wiped out. The student (B5) softens the loss.
4. **Do enemy lords get champions from the start?** Proposed: from army level 3 up, one per lord, and more with rank, so
   early battles stay simple.
5. **Ability control.** Proposed: companions use their abilities on their own by default (most players will not micro
   six heroes), with a per-company toggle to keep them for your order.
6. **Mages as troops or only as companions?** Proposed: both. Companies of mages are what an army fields; a named Priest
   or Cunning companion is the one you grow and grieve. Mage companies are small and capped (one per lord) so the
   game stays a battle of men.
7. **Attack styles for tier IV?** Proposed: no. V-VII only, so reaching tier V is a moment. Tier IV could get a weak
   rank of its weapon's style later if the climb feels flat.
8. **Who presses buttons?** Troops: never (Part D is all passive). Mage companies: cast on their own by their rules (E5).
   Companions: on their own by default, with your order wheel slot as an option (B3, question 5).
