# Knight orders and the captain's aura: plan

Status: **plan, nothing built.** Read docs/LEGACY.md first: the fantasy is that your company is your legacy. This plan adds
a second thread that feeds it: **the captain is a knight who awakens an aura, rises through a knight order, and can end
by founding an order of their own that outlives them.** Numbers marked *(tune)* are starting values.

The request, in short: knight orders as minor factions tied to a main realm, fielding a mix of their own signature troops
and the realm's regular troops; aura and dark power in the manner of medieval fantasy manhwa; and the player character
at the centre of it, not beside it.

## 1. The fantasy

- You start as a captain with no aura. A knight of an order sees something in you (or you see them cut through ten men
  and want it).
- You take an order's oath, or refuse every order and walk alone. Either way your **aura** grows through fighting,
  training and trials. Each tier is a wall you must break through, and breaking it is a story moment, not a number going up.
- At the top you are a figure other lords fear on the field: a Master whose blade glows, whose presence makes a
  company waver. But you still lead an army. **A Master breaks a company, not a battle.**
- The orders are people: grand masters, rivals, a knight-commander who trained you, a brother who fell to the black flame.
- The end of the road: your company becomes **your own order**, your named men become its sworn knights, and your form
  (the technique you made) is passed to whoever takes your banner.

There is a darker way up: the **Black Flame**, forbidden power that skips the walls and eats you from the inside.

## 2. Where we stand (2026-11-09)

Found by reading `Data/factions.json`, `Campaign/CampaignState.cs`, `Data/abilities.json`, `Data/effects.json`,
docs/PROGRESSION.md and docs/COMPANIONS_MAGIC.md:

- **Seven realms**, one of them already an order: the *Order of the Ashen Sun* (culture `order`: sworn brothers on foot and
  knight-brothers), a full realm at the centre of the map. There are no minor factions: every army belongs to a realm,
  brigands or monsters.
- **Troop lines** are seven tiers per role per realm (`lines` in factions.json). Tier V-VII troops already have attack
  styles and passive traits (COMPANIONS_MAGIC Part D), all passive.
- **The effect and ability engine exists** (`Effects.cs`, `Abilities.cs`, `BattleAbilities.cs`): effects with mods and
  icons, abilities with cooldowns, a Will pool, a warning ring, fields, timed effects and great workings. Today every
  ability casts itself by its `when` rule; **nothing is pressed by the player.**
- **The captain** (`CaptainState`) has eight skills with perks, a gear kit, lasting wounds, earned traits, a career rank
  (`Rank`: FreeCompany, SwornSword, Vassal, Lord, Marshal, King). Combat has timed parry, riposte, chains, feints, bash,
  lance, bow, and duels with lords (`Campaign/Duels.cs`). Arms skill sharpens the swing (`ArmsEdge`). **There is no power
  beyond a well-trained man in good armour.**
- **Omen** (`CampaignState.Omen`) already draws ill events from craft magic: a ready model for the Black Flame's cost.
- **Sites** (shrines, standing stones, hermits, old battlefields) and **events** (`Data/events.json`, `flag:` follow-ups)
  give places and chains for trials.

## 3. Pillars

1. **The captain is the hero of this system.** Orders exist to train, test, employ, oppose and finally be founded by you.
   Every phase must change what *you* do, not only what the AI does.
2. **Aura is earned and seen.** It grows from how you fight and the trials you pass. It shows: a glow on the blade, a
   shimmer at Expert, a visible pressure at Master. No hidden stat.
3. **The captain presses the buttons.** Troops stay passive (Part D's rule). Your forms (techniques) are yours to time.
   That keeps the action skill-based and the army a battle of men.
4. **Bounded power.** A Master wins a duel against almost anyone and wrecks a company, then runs dry. Aura is spent and
   comes back slowly. Arrows and pikes still matter. Auto-resolve weighs the captain's aura in, but an army still beats you.
5. **Orders are a mix, not a new realm.** An order army is a core of signature knights stiffened with the patron realm's
   regular troops. It fights for its patron's wars but keeps its own creed, rivals and enemies.
6. **Data first.** Orders, forms, aura tiers and creeds live in JSON on the existing effect and ability engine, like the
   rest of the game. Determinism holds: only the battle's seeded `rng`, and `SelfTest.ReplayExact` must still pass.

### Out of scope
Cultivation of the whole army (troops do not get aura tiers), flying or beams, player necromancy (COMPANIONS_MAGIC open
question 1 stands; the Black Flame is not necromancy), multiplayer.

## 4. The knight orders (minor factions)

### 4.1 What an order is

An order is a **minor faction**: it has no provinces of its own, but it has:

- a **patron realm** whose wars it fights (it shares the patron's enemies and peace, and its lords answer the patron's
  summons, `MusterHost`);
- a **chapter house**: one settlement of the patron where its knights gather, train and recruit (a building on that
  town, not a separate map object);
- a **grand master** and a few **knight-commanders**: named characters (`Characters.cs`) with aura tiers;
- a **creed**: what its knights must and must not do (see 4.4);
- **signature troops**: two or three troops of its own at tiers IV-VII, drawn on its patron's culture (`kin`);
- a **form**: the order's technique, taught to its knights and to you;
- **standing** with you (like a lord's opinion) and with every other order (rivals, allies).

The Ashen Sun stays a full realm: it is the mother order that grew into a state. The minor orders are younger and smaller,
and some of them resent it.

### 4.2 The orders *(names and flavour tune)*

| Order | Patron | Signature troops | Creed in a line | Form |
|---|---|---|---|---|
| **Order of the Silver Lily** | Azure Crown | Lily squires (IV), Lily knights (V, heavy foot), Knights of the Lily Ward (VI, heavy horse) | Never leave a wounded brother on the field | *Unbroken Wall*: a guard that turns aura into a shield |
| **Brotherhood of the Red Rose** | Crimson Compact | Rose blades (IV), Rose swordmasters (V), Thorn knights (VI) | A challenge is never refused | *Thorn Edge*: aura blade cuts that bleed through armour |
| **The Wolfsworn** | Northmarch | Wolf thegns (IV), Wolfsworn (V, axes), Winter knights (VI, light horse) | The pack eats together; no ransom for kin-killers | *Winter Howl*: a roar that shakes nearby foes |
| **Order of the Burning Falcon** | Sunward Emirates | Falcon riders (IV), Falcon knights (V, mamluk-like), Sun-hawks (VI) | Strike fast, never in the back | *Falcon Dive*: an aura-borne leap that closes distance |
| **Sworn of the Eternal Sky** | Khaganate of the Grass | Sky riders (IV), Sky archers (V), Sky-sworn (VI, lance and bow) | The oath binds beyond death | *Sky Arrow*: an aura-charged arrow that pierces a file |
| **The Lantern Company** | Saltmarsh League | Lantern pikes (IV), Lantern guards (V), Lantern knights (VI) | A contract is holy; a broken one is death | *Measured Blade*: a counter that turns the foe's strength back |
| **The Hollow Crown** (hidden) | none | Hollow brothers (V), Black Flame knights (VI-VII) | Power is the only truth | the Black Flame forms (section 7) |

Tier VII for each order is a single named **knight-commander's guard**, not recruitable until you rise high in it (4.5).

### 4.3 Order armies: the mix

An order party fields **40% signature troops, 60% the patron realm's regular line troops** *(tune)*, rising to 60/40 when
the grand master rides himself. The regulars are the patron's own `lines` at the party's level, so an order army looks like
its realm with a hard knightly core. In battle the order's companies carry its heraldry (the patron's palette with the
order's own charge on it, a new `Emblem`).

- Order parties are few: one or two per order, three during a crusade (4.6) *(tune)*.
- They obey their patron's marshal for the realm's objective, but **hunt their creed's enemies first** (the Lily rides to
  undead, the Wolfsworn to raiders on Northmarch land), like a realm's `hunts` temper.
- Signature troops are passive like every troop (Part D): a tier V order knight carries the order's passive trait (an
  effect, e.g. *Lily's ward*: holds ranks while a brother is down near him), and from tier VI a weak aura of their own
  (an effect: +armour and stamina, a faint glow). No order troop presses a button.

### 4.4 Creeds

A creed is a short list of **vows** read by the existing systems that already know when you did the thing:

- *never refuse a challenge* (`CannotChallenge`, encounter panel), *never rob a caravan* (`Rob`), *no fleeing a battle*
  (retreat), *ransom every prisoner* (`SellPrisoners`), *no craft magic* (`Omen` rising), *defend the patron's towns*.

Keep the creed and standing rises; break a vow and standing falls, and a sworn knight (you) gets a **trial of penance**
or is cast out (4.5). Creeds make orders feel different without new rules for each.

### 4.5 You and an order

This is the heart of the plan. Joining is optional; staying unsworn is a real path (section 5.4).

- **Meeting.** An order's knights are met on the map like lords. A knight-commander at a high enough standing offers a
  **trial of entry**: a duel (reuses `Duels.cs`), a deed (clear a goblin holdout, hold a bridge), or a vigil at a site.
- **The oath.** Swearing joins you to the order, not the realm. You can serve the patron as a sworn sword or vassal at the
  same time (the usual path), or serve another realm at peace with the patron (the order frowns: standing drifts down).
  An order's patron at war with your realm ends the oath, or makes you choose.
- **Rank in the order** (stored, unlike the career rank, because it is granted, not derived): *Squire*, *Knight-Brother*,
  *Knight-Captain*, *Knight-Commander*, *Grand Master*. Each needs standing, an aura tier and a deed.

| Order rank | Needs *(tune)* | Gives you |
|---|---|---|
| Squire | the trial of entry | training at the chapter house (aura grows faster resting there), the order's first form, its contracts |
| Knight-Brother | aura tier 1, standing 30 | the order's heraldry on your banner if you wish, recruit its tier IV troop at the chapter house |
| Knight-Captain | aura tier 2, standing 50, an order deed | its tier V troop, the order's second form, a knight-brother companion offered to you |
| Knight-Commander | aura tier 3, standing 70, beat a commander in the lists or win a crusade | its tier VI troop, a seat when the order chooses its next grand master |
| Grand Master | aura tier 4, the old grand master dead or beaten | the order rides with you: its parties follow your objective, its tier VII guard as your household |

- **Order contracts** (on `Contract` and `Quests.cs`): escort a relic, hunt a Black Flame knight, avenge a brother, hold a
  shrine. Done well they raise standing and aura (a deed).
- **Rivals.** Orders have standing with each other. Rising in the Rose makes the Lily cool; killing a Wolfsworn
  commander makes the Wolfsworn hunt you. A rival knight of your own order is a recurring character (6.2).

### 4.6 Orders on the map over time

- **Grand masters die and are chosen** (by the order's commanders; you get a vote at Knight-Commander, and can stand).
- **Crusades:** when the undead rise (`Undead.RaiseHost`) or a troll or brigand king grows, orders whose creed hunts them
  call a crusade: their parties gather, and you are summoned if sworn.
- **Orders outlive realms.** If the patron falls, an order seeks a new patron (the strongest realm of a kin culture), or
  goes free as a sellsword order you can hire, or dies out.
- **Orders fall to the Black Flame.** A grand master's corruption event can turn a whole order (rare): a mid-game crisis.

## 5. The captain's aura

### 5.1 The tiers

| Tier | Name | What you can do | How it looks |
|---|---|---|---|
| 0 | Unawakened | fight as now | |
| 1 | **Kindled** | aura pool appears; stamina comes back faster; your first form | a warm light in the eyes when a form is used |
| 2 | **Aura Knight** | **aura blade**: hold a key to sheathe your weapon in aura (pierces armour, cuts through a block, drains aura) | the blade glows in your colour |
| 3 | **Aura Expert** | **aura armour** (a hit's damage is paid partly from aura), **projected strike**: a short ranged cut (5-8 m) | a shimmer over you; the projected strike leaves a streak |
| 4 | **Aura Master** | **presence**: a field that steadies your men and shakes enemy companies near you; aura comes back in battle; the great form | a pressure ring on the ground; leaves and dust stir |
| 5 | **Transcendent** | the order's great form at its full; one at a time in the world *(tune)* | everything above, and men stop to look |

Tiers 1-3 are within reach of a normal campaign. Tier 4 is late game. Tier 5 is a legend: possible, never expected.

### 5.2 The pool and its cost (the bound)

- **Aura** is a pool 0-100 *(tune)* beside stamina and health on the HUD. Forms and the aura blade spend it. It refills
  slowly out of fights, quickly at rest on the map, faster at your order's chapter house.
- Below 20 you are **Spent** (an effect: slower, weaker, the glow dies). A Master who burns everything in the first minute
  is a tired man in heavy armour for the rest of the battle.
- **The rule, tested:** a tier 4 captain alone, played well, beats one tier III company of 40 *(tune)* and loses to two. A
  tier 2 captain beats a lord champion more often than not. Against arrows, aura armour soaks only a share.

### 5.3 How aura grows

`CaptainState.AuraXp` grows from what PROGRESSION P2 already notices, and from new deeds:

- fighting well: parries, ripostes, chains, kills of high-tier men (more for tier V+), duels won;
- training: rest days at a chapter house or with a knight companion (like drill, `Daily`);
- trials and deeds (5.5).

At each tier's threshold **you hit a wall**: XP stops filling, and the K screen (captain sheet) shows *"Your aura has
reached a wall. A trial will break it."* This is deliberate: the climb is gated by moments, not grinding.

### 5.4 Sworn or unsworn

- **Sworn:** your order's forms, its training, a clear trial path, and its creed and politics.
- **Unsworn:** you find your own way. Trials are harder to come by (hermits, old battlefields, duels with order champions),
  but at tier 3 you can **make your own form** from the moves you used most (the form takes its shape from your weapon
  and your most-used style: parry-heavy players get a counter, lance riders a charge). This form is what your own order
  teaches later (section 8).
- You may leave an order; it costs standing everywhere and some orders (the Wolfsworn, the Sky-sworn) never forgive it.

### 5.5 Breakthroughs (trials)

Each wall has a few trials, offered as events and map goals, chosen by your order and your path *(tune)*:

- **Duel a champion** of the next tier up and survive (win or lose well: a parry count, still standing at the end).
- **Hold against odds**: win a battle outnumbered two to one with yourself in the front.
- **Vigil**: a night at a shrine, standing stone or old battlefield (`Sites.cs`), then an event with choices
  (`Data/events.json`), some of which shape your form.
- **Grief**: the fall of a sworn brother or a companion who taught you (ties L3 "loss that is felt"); an event lets you
  break through in anger (fast, but a scar) or in mourning (slower, steadier).

A failed trial can **crack your core**: a new lasting wound (`Wounds.cs`, `core`) that caps your aura a tier lower until
healed by a long rest, a priest's rite (`Rites.cs`) or a quest. Never permanent unless you take the Black Flame to mend it.

### 5.6 Forms (the captain's techniques)

Forms are abilities on the existing engine (`Data/abilities.json`, `BattleAbilities`), with a new owner: **the player**.

- Two to four **form slots** on keys (new `Act` entries `Form1`..`Form4`, F1 help, `Controls.Defaults`). Forms cost aura
  and have cooldowns; the warning ring rules (E4) apply to forms that hit at range, so foes can still see them coming.
- Each order teaches two forms and one **great form** (once a battle). Unsworn captains get generic forms (*Focus*, *Aura
  Slash*) and later their own.
- Examples *(tune)*:
  - *Unbroken Wall* (Lily): 4 s of near-perfect block in front, reflecting stamina loss to the attacker.
  - *Thorn Edge* (Rose): the next three hits ignore most armour and leave a bleed (timed effect).
  - *Winter Howl* (Wolfsworn): an area shock that shakes enemy companies within 12 m.
  - *Falcon Dive* (Falcon): leap 10 m onto a target, a heavy strike on landing.
  - *Sky Arrow* (Sky-sworn): a charged shot that goes through a file of men.
  - *Measured Blade* (Lantern): a parry that turns the blow back at full power.
  - Great forms: *Lily's Bastion* (your company holds like a wall for 15 s), *Red Rose Bloom* (a whirl of cuts around you),
    and so on.
- Forms rank up with use and tier (a tier 4 *Thorn Edge* is not a tier 1 *Thorn Edge*), shown on the captain sheet.

### 5.7 Aura in the rest of the game

- **Duels:** aura tier decides most duels between equals in skill; a lower-tier captain wins only by timing.
- **Auto-resolve:** `Rules.MightAt` adds the captain's aura tier through a `Balance.AuraWeight` *(tune)*: worth a company,
  never an army. `--agree` must still pass.
- **Career:** an Aura Master is wanted: lords' opinion rises, councils listen, a realm offers you its marshal's baton
  sooner. Renown from duels and deeds grows faster.
- **Your men:** while you fight in their front at tier 2+, your aura steadies them (a small effect on companies near you,
  on top of the leadership aura). They love a captain who bleeds with them.

## 6. People

### 6.1 Order characters

Grand masters, knight-commanders and knight-brothers are `Character`s with an `Order`, an order rank and an aura tier.
They duel, give trials, die, rise, betray and remember (opinion and memories already exist).

### 6.2 The rival

When you join an order, a **rival** of the same rank is drawn: a knight-brother who wants what you want. They show up in
contracts, the lists and grand master elections. They may become a friend, an enemy, or fall to the Black Flame and be
the one you must kill. A short chain of `flag:` events, not a new system.

### 6.3 Companions and named men

- A **knight companion** (a new calling in `Data/companions.json`, *Sworn Knight*) trains your aura in camp, can hold an
  aura of their own (tier 1-3), and leads a company as a hero with a passive version of their order's form.
- **Named men** (L1) of your company can be **knighted** once you are a Knight-Captain or have your own order: a named
  sergeant becomes a knight, and later the core of your order (section 8).

## 7. The Black Flame (the demonic path)

Optional, found, never offered on the main road.

- **Where it comes from:** the Hollow Crown, a hidden order with no patron. You meet it through a cracked core with no
  cure, a vigil at the wrong stone, a fallen rival's blade, or a Hollow knight beaten in a duel offering a bargain.
- **What it gives:** it breaks a wall at once, no trial. Black Flame forms burn: damage over time, fear, aura drained from
  foes to you. At high corruption the black flame **heals you from kills**.
- **What it costs:** `CaptainState.Corruption` 0-100. It rises with each use and each breakthrough taken this way, and never
  falls by itself (a long chain of penance, a relic or a grand master's mercy brings it down).
  - 25: your men notice (morale events, a companion leaves, the pious first).
  - 50: orders hunt you: every order's standing collapses, Hollow Crown agents seek you out, villages bar their gates.
  - 75: **madness events**: in battle the flame can take you for a few seconds (you lose control and strike the nearest
    man, friend or foe); on the map choices you did not make.
  - 100: an ending: you become the Hollow Crown's new master, a villain the world unites against (a final war), or you
    burn out. Both close the campaign and go into the chronicle and the legend.
- It reuses Omen's model (thresholds that draw events) but is its own number: Omen is the world reacting to craft;
  Corruption is you changing.
- **A way back** exists and is hard: purging the flame drops you a tier, and is a story (an order taking you back, a
  priest companion, a relic quest). Players who want the dark path to the end can take it.

## 8. Your own order (the legacy)

The end of the knight thread meets the end of the legacy thread.

- **Founding:** at aura tier 3, Lord rank or fame enough (L6), and a seat of your own (a fief or a founded realm), you can
  **found an order** from your company. You name it, choose its charge on your banner and pick three vows for its creed.
- **Its knights:** knighted named men become its first knight-brothers. Your best company's troop line gains two
  **signature troops of your order**: tier V and VI versions of your own troops, carrying your order's passive trait.
- **Its form:** the form you made (5.4) or the great form you mastered is taught to your knights (as a passive trait on
  your signature troops) and to your successor (as forms).
- **When you go:** on retire or hand-over (`Legacy.Retire`, `HandOver`) the order goes on: your successor is its next grand
  master, starting a tier or two below you, with the order's forms. Your order appears in the legend tab and in the end
  summary. In a later generation it can be a minor faction of its own on the map, riding for your realm.

## 9. Hard rules (what must not break)

- **Determinism:** aura, forms and madness use the battle's seeded `rng` only. `SelfTest.ReplayExact` passes with forms used.
- **Headless:** all sim code (aura, forms, orders, creeds) stays in headless files; glows and rings only in drawing code.
- **Higher tier is better** (COMPANIONS_MAGIC D0): order signature troops follow it. `TierFight` and `TroopTree` stay green.
- **Scale:** order party sizes use `Scaled(n)`; prices per man `Rules.PerMan`. ArmyScale 1 plays as before.
- **Saves:** new fields only, with defaults. Old saves load as an unawakened, unsworn captain in a world whose orders are
  created on load. No version bump.
- **Maps:** orders sit on existing towns; no change to the world generator for any existing layout.

## 10. Phases

Each phase ships alone, ends with `tools/check.sh` passing, and adds self-tests. The order below is the build order.

### K0. This document (docs only)

### K1. The captain's aura (no orders yet)
- Save: `CaptainState.Aura` (pool), `AuraTier`, `AuraXp`, `AuraWall` (bool).
- Battle: `BattleOptions.AuraTier`; aura pool and Spent effect; tier 1 stamina, tier 2 **aura blade** (hold a key), tier 3
  aura armour. XP from P2's swing events. HUD aura bar; glow on the blade (draw only).
- Map: the wall message on the captain sheet; one generic trial (duel a stronger champion) to break tiers 1-2.
- Tests: `AuraBlade` (blade pierces, drains), `AuraBound` (tier 3 captain vs one company wins, vs two loses), replay exact.

### K2. Forms
- `Act.Form1..Form4`, form slots on the captain, abilities owned by the player (`owner: "captain"` in abilities.json),
  generic forms (*Focus*, *Aura Slash*), projected strike at tier 3. Shots: `gallery-aura*.png`.
- Tests: forms cost aura and cool down; a ranged form warns first.

### K3. Orders on the map
- `Data/orders.json` (id, name, patron, chapter, charge, creed vows, signature troops, forms, hunts) loaded and validated;
  `CampaignState.Orders` (`OrderState`: patron, grand master, commanders, standing with you and each order, strength).
- Signature troops in troops.json (tier IV-VI per order); `Troops.Validate` and `TroopTree` know order troops are reached
  from the patron line.
- Order parties with the mixed army (4.3), heraldry, hunts.
- Tests: an order army is about 40/60; order troops climb in tier; `SevenRealms` style check for order heraldry.

### K4. Joining, rising and creeds
- Meeting knights, the trial of entry, the oath, `CaptainState.Order` and `OrderRank`, creed vows checked, penance and
  casting out, chapter house training and recruiting, order contracts. UI: an ORDER tab on the realm screen (R) and a
  chapter tab on its town.
- Tests: `OrderOath`, `CreedBroken`, `OrderRankGate`.

### K5. Breakthroughs, trials and the master tier
- All trials of 5.5 as events and goals; cracked core wound and its cures; aura tier 4 (presence, aura back in battle,
  great forms); order forms and great forms.
- Tests: a failed trial cracks the core and caps the tier; presence shakes nearby enemy companies only.

### K6. Champions, rivals and the AI
- Order characters with aura tiers fight as champions (`LordChampion` reads aura); the enemy AI uses forms for its
  champions (by `when` rules, as mages do); the rival chain; grand master elections; crusades.
- Balance: `Balance.AuraWeight`, `--agree` and `--pacing` targets for aura tier by career stage.

### K7. The Black Flame
- The Hollow Crown, Corruption, its forms, the thresholds and madness, the way back, both endings.
- Tests: corruption only rises from its own sources; madness uses the seeded rng; the ending fires once.

### K8. Your own order
- Founding, knighting named men, your order's signature troops (made from your troop lines), its form, handing it on;
  the legend tab and summary.
- Tests: founding needs its gates; a successor inherits order and forms a tier lower.

**Why this order:** K1-K2 make the captain feel different in the first hour of play, before any faction work, and they are
what every later phase builds on. K3-K4 give the aura a world to grow in. K5-K6 give it peaks and enemies. K7 and K8 are
the two endings of the road and can be built in either order.

## 11. Open questions

1. **How high is the ceiling?** Proposed: a Master is a local storm (one company), not a battle-winner. If you want the
   manhwa extreme (one man routing an army), say so: it changes the K1 bound test and `AuraWeight`, not the structure.
2. **Can the captain belong to an order while serving a different realm?** Proposed: yes, if the two are at peace, with
   standing drifting down. War between them forces a choice.
3. **Unsworn power.** Proposed: unsworn captains reach the same tiers, a little slower, and get the only custom form.
   Is that the right reward for walking alone?
4. **The Ashen Sun.** Proposed: it stays a realm, and its knight-brothers can sponsor you like an order (it has the form
   *Ashen Dawn*). Or should it become the seventh minor order and its land go to its neighbours? (That would change maps.)
5. **Does the AI get aura on its lords?** Proposed: lords stay as now; only order characters and a few named champions
   (brigand kings, the Hollow Crown) carry aura, so a lord with aura is a known name, not a common sight.
6. **Names.** Order names, aura tier names and form names above are placeholders in the house voice.
