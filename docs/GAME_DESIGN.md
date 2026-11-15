# Ironfield — Game Design Document

A handoff doc for building this game (or one like it) from scratch.
No engine, no code, no tools — just the game: what it is, how it plays,
and what is still left to build.

---

## 1. High concept

**Ironfield is a medieval soldier-general game.** You fight *in* the battle
as a mounted captain — lance, sword, shield — while also commanding your
army RTS-style from a slowed-time overhead view. Between battles you lead
a mercenary company across a living campaign map: three realms at war,
brigands on the roads, towns to recruit and train in, castles to besiege,
lords who fight their own war whether you show up or not.

Two genre halves, one fantasy:

- **The battle half** (Mount & Blade's "lead in person" + Total War's morale,
  flanks and braced spears): directional melee, couched lances, trample,
  routing and rallying, terrain that matters.
- **The campaign half** (Mount & Blade's warband life): persistent companies
  that gain XP, bleed, upgrade through troop trees and remember their history;
  an economy of upkeep, contracts, loot and ransom; AI lords playing by your
  rules.

The core tension, stated once because everything hangs off it:

> **You are strongest as a leader when you are in the fight, but you can
> only control the fight from above.**

---

## 2. Pillars

1. **Read the battle at a glance.** At command distance: colour says *whose*,
   silhouette says *what* (shield = line infantry, long pole = spear,
   longer pole = pike, bow = archer, horse = cavalry, plume = officer),
   and the HUD says *how they are doing*.
2. **The campaign feeds the battles.** Every map system exists to make the
   next battle matter: who you fight, with which companies, at what tier,
   on what ground. No system the battles don't touch.
3. **Companies are characters.** A company has a name, a history and a tier,
   and it remembers its battles. Losing veteran Men-at-Arms should hurt.
   Growing Levies into an elite guard over a campaign is the long-term reward.
4. **You start as a captain, not a king.** You ride with your army at the
   scale of one warband among lords and their hosts. (The endgame — rising
   to found your own realm — is specified but not yet built; see section 10.)
5. **The AI plays by your rules.** Enemy lords recruit, pay upkeep, gain XP
   and upgrade along the same trees with the same costs. An enemy army of
   knights *earned* them.
6. **Short sessions, readable state.** At any moment the player can see what
   each company needs (men, pay, an upgrade, rest). A 20-minute session
   should hold at least one meaningful battle.

---

## 3. Player fantasy and session shape

You are a **free captain** sworn by contract to a realm at war. In one
session you: march across the map, chase down a brigand band or an enemy
lord, fight the battle yourself (or auto-resolve the trivial ones), spend
the loot and XP in the next town (upgrade a company, hire recruits, buy
horses and food), check the contract board, and ride out again.

A full campaign today is a sandbox of roughly **25–30 battles** to grow a
tier-IV army. The overhaul (section 10) turns it into a career with a win
state in roughly **20–30 hours**.

---

## 4. Core loop

```
travel the map ──► encounter ──► battle (or auto-resolve) ──► results
      ▲                                                              │
      │        upgrade / recruit / heal / get paid in towns          │
      └────────────────── camp & settlements ◄───────────────────────┘
```

- **Travel:** real-time with pause across the world map, chasing contracts,
  enemy parties and recruits.
- **Encounter:** meeting a party opens a dialog: attack, auto-resolve,
  retreat, or parley.
- **Battle:** the battle game, built from both parties' companies, on ground
  generated from the map location (or a siege assault at a settlement).
- **Results:** casualties (killed vs wounded), XP, loot, renown, prisoners
  and captured horses, applied to persistent companies.
- **Camp & towns:** spend gold and XP. Upgrade companies, recruit, hire
  mercenaries, heal, take contracts, buy food and gear.

---

## 5. Battles (the heart of the game)

### 5.1 Soldier vs general

- **Field view** (third person): you fight. Your **leadership aura** (12 m)
  gives nearby troops morale (routing ones recover faster). Your kills give
  nearby allies extra morale ("heroics"). **Rally** costs stamina, has a
  cooldown, and gives a burst of morale in a radius — usually enough to
  bring routing men back.
- **Command view** (overhead, time slowed to ~quarter speed, pausable):
  RTS-style control — select squads, draw formation lines, give orders. But
  your body stands idle and vulnerable, and your aura is off. A living
  general still heartens the whole army slightly, so neither side gets a
  free permanent edge from an unreachable captain.
- **If you fall**, the battle continues: you command from afar at normal
  speed with no aura. If either side's captain dies, every unit on that
  side loses a chunk of morale — killing the enemy captain (who waits just
  behind his line) is a genuine tactical goal.
- A side loses when it has no active, non-routing units left.

### 5.2 Field combat (you, the captain)

- **Directional attacks:** the mouse's last movement sets the swing —
  up = overhead, down = thrust, left/right = side cuts. Hold and release to
  strike; longer holds hit harder. Swings can cleave up to 3 targets.
- **Blocking:** front only. A shield stops any direction at a stamina cost;
  blocking *in the direction the blow comes from* (shown on the crosshair)
  is a **parry**: free, and it staggers the attacker. Guards raised within a
  tight window (~0.35 s) of the blow count; striking into a blow from the
  mirrored side **chambers** it. Winding up can be cancelled (feint).
  Empty stamina = guard break.
- **Extras:** shield bash (a kick when fighting two-handed), swapping to a
  two-handed greatsword on foot (only parries stop blows from the wrong
  side), mounting any loose horse in reach, whistling for the nearest loose
  horse of your side when none is near.
- **Mounted:** rein up/down, turn (turning widens with speed), gallop. Above
  a speed threshold the lance can be **couched**; anything the point touches
  takes heavy damage scaled by closing speed, then the lance comes up until
  you slow and re-couch. The mouse looks (and strikes) freely, independent
  of where the horse points.

### 5.3 Command (the general)

- Select squads (click, box-drag, number keys, unit cards); orders go to the
  selection, or the whole army if nothing is selected.
- **Orders:** Charge, Advance, Fall back, Hold position, Follow me,
  Form line on me, Loose/tight spacing, Archers fire at will / hold fire —
  plus an order wheel with the same set for field view.
- **Formations:** line, shield wall (block bonus, slow, braces), square
  (stops cavalry, weak to arrows), wedge (cavalry impact bonus), loose
  (spreads against arrows, which are physical — spacing genuinely reduces
  hits). Cycled per squad.
- Click to move (keeps width); drag to draw the formation line (faces the
  enemy, with a per-man preview); click an enemy squad to attack it.
- Allied lords' companies in your battle fight under their own AI general;
  only your companies take your orders.

### 5.4 Melee, morale, flanks

- Melee is staged: windup → strike → recover. Hits interrupt windups.
  AI soldiers block frontal hits by chance (shield troops often); a player
  block staggers the attacker.
- **Flanks and rear:** side blows deal ×1.2, rear blows ×1.5, and only
  frontal blows can be blocked. Flank/rear hits build squad **Pressure**,
  which drains morale until it decays.
- **Morale and routing:** below ~12 morale a unit routs and flees the field;
  it rallies above ~32 (routing men recover morale once clear of the enemy).
  A man who has broken twice stays broken, so battles resolve. Routing
  spreads (nearby routing friends drain morale), as do cavalry bearing down
  on unbraced foot and an army that has lost over half its men.
- Unit states read STEADY / FLANKED / WAVERING / ROUTING. Routs are
  recoverable, not terminal — rallying a broken flank back into the fight
  is a core skill.

### 5.5 Cavalry (the signature system)

Three tiers — light outriders on coursers, lancers on warhorses, knights on
armoured destriers — governed by momentum:

- Horses have heading and speed with limited acceleration, braking and turn
  rate. A charge needs room.
- **Speed bonus:** every melee blow gains ~+5% damage per m/s of closing
  speed above a threshold (shown live under the crosshair).
- **Couched lance:** the point ahead of a fast horse hits for a base plus a
  large multiple of closing speed.
- **Trample:** a fast horse knocks down and hurts footmen it runs into —
  but every body slows it, and tight shield walls slow it much more.
- **The counters (Total War bracing):** spearmen, and shieldmen holding
  tight, set their points against horses. A braced spear out-reaches a
  couched lance and hits the horse extremely hard. Spears deal bonus damage
  to anything mounted. A rider's *ordinary* melee reach is capped near a
  footman's, so a stalled charge is not free damage.
- **Horses die:** a slain horse throws its rider (stagger + damage); riders
  fight on foot; a killed rider leaves a loose horse anyone can take.
- **AI cycle charge:** charge → ride through → withdraw → turn → charge
  again; riders bogged down in a crowd fight in place briefly, then pull
  out. Cavalry on Hold counter-charges enemies that come close. The enemy's
  horse waits wide on a flank, then hunts the softest squad (archers first)
  once the lines meet.
- Archers' arrows catch the horse more than half the time; blades mostly
  find the rider.

### 5.6 Archers, fatigue, supply

- Archers fire ballistic arrows that lead the target, only when settled in
  formation or charging. Elevated archers shoot ~20% further. Rain/snow
  scatter arrows; fog and night shorten archers' sight.
- Men tire from running and fighting (down to ~70% speed and damage), so
  fresh reserves matter in long fights. Each side has a supply wagon with a
  limited arrow stock; archers resupply at it.

### 5.7 Terrain, weather, ground

- Rolling fields with hills, woods on the flanks (cavalry routes), lone
  trees, rocks, and a road that orients the player. Deployment ground is
  kept open; trunks and boulders are solid (horses pull up hard on them).
- **Ground matters:** uphill is slower, downhill quicker; striking from
  higher ground deals up to +15% (less from below). Arrows fly from and
  land on the real ground.
- Map terrain parameterises battle terrain: forests bring close woods,
  hills bring big high ground, farmland brings hedges, roads bring the road.
- **Time of day and weather:** the sun follows the campaign hour (dawn,
  dusk, moonlit night); rain/snow and fog change play.
- **Rivers and fords:** some fields are crossed by a river with fordable
  crossings; wading slows and disorders — ford battles are choke-point
  puzzles.

### 5.8 Deployment, reserves, victory

- Battles open **paused** in command view with a deployment zone (your third
  of the field): arrange companies, draw lines, set spacing and fire orders,
  then start. Scouts in your army reveal the enemy deployment; otherwise you
  see banners only.
- **Reserves:** the field caps at ~400 men. Larger sides deploy their
  biggest companies first and march the rest in from their edge as companies
  break or leave.
- **Siege assaults** (see 6.5) are a dedicated on-foot battle mode.

### 5.9 Auto-resolve

Trivial fights can be skipped: per-man strength (health, armour, damage,
class and terrain factors), plus leadership and a luck band, rolled on the
strength ratio. Casualties spread over companies toward the losing side and
lower tiers. It must agree with a played AI-vs-AI battle ~3 times in 4, so
skipping never feels rigged — nor ever quite safe.

### 5.10 Battle results

Per company: started, standing, fled, fallen, kills (by victim tier).
The fallen split into **killed vs wounded** (heavier armour survives more
often); the defeated side's wounded may be captured. Then XP, loot, renown
and captured horses. Fled men trickle back if the party survives. A results
screen shows company cards with losses, XP bars and upgrade badges, plus a
battle timeline.

---

## 6. Troops and armies

### 6.1 Roles and counters

- **Line infantry** (shieldmen → men-at-arms → elite guard): the shield wall,
  holds ground, braces in tight order.
- **Spear infantry** (runners → pikes → halberds/glaives): the cavalry
  answer; braced points stop a charge cold.
- **Heavy infantry variants:** two-handed greatswords (no shield, huge
  damage), sergeants, northern axemen/huscarls.
- **Archers** (bowmen → longbows/rangers → crossbows/arbalests): ballistic
  fire; crossbows reload slowly but largely ignore armour.
- **Cavalry** (outriders → lancers → knights/cataphracts): shock and
  pursuit; plus **horse archers** that skirmish from the saddle.
- **Northern culture line:** clansmen → axemen → huscarls/berserkers/
  hearthguard, with light reaver cavalry.

Every role has a counter: spears beat horse, horse beats archers, archers
punish loose elites and squares, shields hold the line, crossbows and
greatswords crack armour.

### 6.2 The troop trees

~108 troops across **7 tiers** in branching, cross-linked trees per culture:
a southern tree (shared root: levies) and a northern tree (root: clansmen).
Every troop defines stats, class, tier, upgrade targets, recruit cost,
upkeep, upgrade XP, its horse if mounted, and its visual look. Rules the
trees must obey: every upgrade raises the tier and is a better soldier;
every troop is reachable from a root.

### 6.3 Tiers you can see

Upgrading must be **visible on the field**. Each step adds metal, then
heraldry, then full protection:

| Tier | Head | Body | Shield | Heraldry |
|---|---|---|---|---|
| I (levy) | linen cap | dyed tunic | round wooden | painted quarters |
| II (regular) | kettle hat / hood | tunic / quilted gambeson | kite | charge on shield |
| III (veteran) | nasal helm / kettle | mail | heater | tabard with charge |
| IV+ (elite) | great helm | plate | heater | tabard, plume; armoured destrier |

(Tiers V–VII continue the elite range: heavier plate, richer heraldry.)

### 6.4 Heraldry is identity

Each side is its colours plus a **charge** (emblem) painted on shields,
tabards, horse cloth and banners — and the UI uses the same colours.
The three realms: a blue/chevron south-western crown, a crimson/saltire
south-eastern compact, and a northern clan culture; brigands fly tattered
grey-brown with no charge. **The player is always gold**, findable in any
crowd.

---

## 7. Campaign

### 7.1 World map

- A **province graph** (~60 provinces): each holds one town or castle plus
  2–3 villages, joined by roads; borders follow ridges and rivers. A rim of
  mountains closes the world. Generated from a seed (same seed = same map).
- **Terrain kinds** per cell set map speed and the battlefields fought
  there: meadow, farmland, forest (slow, close woods, bad for cavalry),
  hills (big high ground), roads (fast overlay), rivers with ford/bridge
  crossings.
- **Continuous time with pause** (Mount & Blade style): speeds ×1/×3/×8.
  Days drive healing, recruits and drill; weeks drive upkeep and income.
  Four 30-day seasons; **winter** slows the march, slows healing and raises
  food consumption.
- The map is alive in one glance: day/night, seasonal ground, party tracks,
  smoke over threatened villages. Parties render as banner-bearers in their
  leader's colours, sized slightly by strength; hovering shows their card.

### 7.2 Parties and companies

- A **party** is a leader plus companies. The player fields up to **10
  companies**; total men scale with renown (~40 + renown/5). Lords mirror
  this; brigand bands are leaderless mobs capped near tier II.
- A **company** (the persistent unit) has: name (generated, renamable),
  troop type, fit men, wounded men, average XP, battles/kills history,
  cohesion (0–30, feeds battle morale), role (line/spear/bow/horse),
  and a named sergeant **officer** with traits. Elite companies are smaller.
- **Travel speed** follows the slowest company (all-mounted parties are
  fast), modified by terrain, roads, and wounded load. Scouts extend vision.

### 7.3 Encounters

When parties touch, time pauses and a panel shows both cards with a strength
comparison:

- **Attack** (opens deployment, then battle), **Auto-resolve**, **Retreat**
  (a rearguard pays in men; impossible if the enemy is faster and horsed),
  **Parley** (pay off brigands; observe truces with lords).
- Nearby parties may **join** as allies (AI-led) or enemy reinforcements.
- Withdrawing mid-battle is allowed (counts as a defeat for results).

### 7.4 Progression: XP and upgrades

- **XP per battle** per company: participation per surviving man (×1.5 on
  victory) plus kill XP scaled by victim tier over company size. **Drill**
  adds a little daily (more in friendly towns). New recruits arrive at 0 XP
  and dilute the average — refilling veterans has a real cost.
- Pace: tier I upgrades after 1–2 battles, tier II after ~4, high tiers
  after many more; a full elite army takes ~25–30 battles.
- **Upgrading** needs the XP threshold, gold (cost difference × men), and a
  town training yard (or a Drillmaster perk for camp training). The whole
  company upgrades together; wounded upgrade on return; XP resets after.
  **Branches:** levies choose a line (shield/spear/bow/horse); cavalry needs
  bought or captured horses. AI lords upgrade by identical rules.

### 7.5 Economy

Currency: **crowns**. Start: ~300.

| Flow | Rule (starting values) |
|---|---|
| Upkeep (sink) | ~10% of recruit cost per man, weekly |
| Contract pay (source) | ~85% of upkeep while sworn, plus bounties |
| Loot (source) | ~25% of beaten enemies' recruit cost, to the winner |
| Ransom (source) | captured men sold near their faction's towns |
| Recruits/mercenaries (sink) | levies at cost; mercenary companies whole at a markup |
| Upgrades, horses, gear, food (sink) | as priced |
| Fief income (source) | weekly from held seats and their villages |

Unpaid weeks bleed cohesion and desert the lowest tiers first. A mid-tier
army on contract with average fortune should stay roughly solvent — the
economy is tuned so war pays for war, barely.

### 7.6 Settlements

- **Villages:** recruit levies/clansmen from a regrowing pool (hostiles
  refuse); buy or forage food (foraging at home costs renown; abroad it is
  a raid).
- **Towns:** training yard (upgrades), mercenary offers (tier II–III,
  refreshed weekly), market (horses, food, captain's gear: armour → plate,
  helm → great helm, courser → warhorse → destrier), fast healing, prisoner
  sales and lord ransoms, contracts, and the steward's **contract board**.
- **Castles:** garrisons (stationary AI parties), rest, prisoners, contracts.
- **Contracts board** (up to 3 at a time): hunt a named lord, clear a
  brigand band, guard a threatened village, scout a settlement, take a seat.
  Deadlines and pay.

### 7.7 Sieges and fiefs

- Ride to an enemy town/castle and **besiege**: after a night your men have
  ladders and a ram. Then **assault** (an on-foot battle — climb ladders,
  batter the gate, defenders on the walkway behind cover with longer
  archer range), **wait** (the garrison eats its supplies, then starves),
  auto-resolve (walls multiply defence, much more without ladders), or lift
  the siege. Lords besiege and retake seats by the same logic.
- A captured seat takes its province and villages and becomes your **fief**:
  weekly income plus a garrison you can reinforce with your own companies.
  Hand fiefs back if you change sides. Garrison strength and supplies decide
  later sieges.

### 7.8 Prisoners, officers, food

- **Prisoners** march with you (they slow you and eat): sell them, recruit
  them (no cohesion, desertion risk), or free them for renown. Captured
  lords are ransomed or freed.
- **Officers:** named sergeants with up to three traits (stubborn, steady,
  drillmaster, hothead, sharpshooter, surgeon, veteran) that show in battle.
  They can fall. Losing a veteran officer should sting.
- **Food:** parties carry supplies, bought in towns or foraged. Starving men
  lose cohesion and desert. Winter bites hard on long marches.

### 7.9 Factions and the living war

- Three realms plus brigands. Wars and truces are **per pair of realms**;
  the loser sues for a ~30-day truce past a war-score threshold; war
  resumes when it lapses. A realm holding ~45% of the seats unites the
  others against it (anti-snowball).
- **Lord AI** (hourly desires): flee stronger faster enemies → defend
  threatened seats → hunt weaker parties (including you) → threaten enemy
  villages → refit at home (recruit/upgrade/heal, drawing on the faction
  war chest) → march to the front. Lords on campaign accept even fights.
- **Faction AI** (daily): holds a war front, assigns lords, musters hosts
  that move and join battles together, raises new lords from its treasury
  (capped by the seats it can pay for), posts contracts.
- **Brigands** (~190 bands on the full map, several spawning daily, growing
  in size and tier with campaign day) are opportunists: they flee strength
  and fall on weakness — pressure on the roads, not lord-grinders.

### 7.10 The captain (you)

- **Renown:** per victory plus a foe-strength share. Raises the men limit
  and, each 100, buys a **perk**: aura radius, rally strength, auto-resolve
  and scouting, cheaper upkeep, better wound odds, camp training, cheaper
  horses and faster marching, unbreakable nearby companies. Each changes
  battles.
- **Stage titles** (Mercenary Band → Lord of War) and a **next-step hint**
  ("upgrade that ready company", "buy food", "see a steward") shown under
  the captain panel, on the results screen, and used to open the right town
  tab on arrival. A new player should reach their first upgrade unaided.
- **Gear:** buy armour, helm and horse tiers in towns; they appear on the
  battlefield and the map sprite.
- **Wounds and capture, never death:** fall in battle and you're **wounded**
  (days out; battles start in command view). Lose the army too and you're
  **captured**: lose gold, your party escapes to the nearest friendly town.
  No permadeath yet (an ironman mode is planned with the career overhaul).

---

## 8. Look, interface and sound (game-facing)

- **The world is solid; the soldiers are drawn.** Low-poly flat-shaded 3D
  land, trees and rocks under one warm sun with real shadows, gradient sky
  with sun and drifting clouds, distance fog, swaying grass and backlit
  leaves. People and horses are crisp pixel-art puppets (~15 parts on a
  skeleton, front/side/back views, five-tone hue-shifted shading, selective
  outlines, per-material surface patterns: folds, quilting, mail checker,
  plate crispness, wood grain). Never mix the two.
- **Muted land, loud heraldry.** Saturated colour belongs to armies and
  banners so they pop; a gentle grade (warm highlights, cool shadows, light
  vignette) finishes the frame. Hit flash, routing grey, toppled corpses,
  dust under hooves, blood and spark flecks, arrows stuck in shields and
  ground, company banners riding in the ranks.
- **The interface is a field kit.** Dark iron plates with hairline edges,
  gold reserved for *you* (selection, your body, parries, ready upgrades),
  side colours for allegiance, state colours for condition (steady /
  wavering / flanked / routing). One uppercase pixel font at integer sizes;
  short imperative voice ("HOLD POSITION!", "12/24"). Nothing bounces or
  slides — emphasis is colour, not movement.
- **Campaign map** speaks the same language: same land and light, armies as
  banner-bearer sprites, same company cards with upgrade affordances.
- **Sound:** fully synthesised, positional in battle — steel on steel,
  shield thuds, bows and arrow strikes, hooves at walk and gallop, charge
  shouts and rally cries, rout horns, UI clicks, map ambience, procedural
  drums-and-drones music that follows game state (map / deployment /
  battle / victory / defeat). A battle should be readable with eyes closed.

---

## 9. Scale (current targets)

- **Battle:** up to ~400 men on a ~500 m field; 20-minute session ≥ 1 battle.
- **Map:** ~60 provinces × (1 town/castle + 2–3 villages); ~250 parties;
  ~190 brigand bands at pressure; 8 lords per realm per 26 provinces
  (scaled, seat-capped).
- **Party:** 10 company slots; ~40 + renown/5 men; elite companies smaller.
- **Pace:** T1 upgrade in 1–2 battles; elite army in ~25–30 battles.
- **Balance bar:** same-tier AI-vs-AI battles come out near even; only
  player skill (fighting *and* commanding) tilts even fights.

---

## 10. What remains to be built

Everything above **exists and plays**. What follows is the planned second
half of the game — specified in full, not yet started. Build it in this
order; each step ends playable.

### F1. Troop stacks and per-man promotion (large)

Today a company is one troop type upgraded all at once. Rework: a company
is a **banner of one role** (line/spear/bow/horse) holding **stacks** of any
troops of that role, any tree, any tier.

- Each stack has its own men/wounded and a shared XP **pool**. When the pool
  covers one man's `upgradeXp`, **one man** promotes (pool and gold paid per
  man; "promote all" repeats while affordable). Refilling veterans no longer
  dilutes them.
- Each promotion picks a branch: five levies to shieldmen, two to bowmen —
  men leaving the company's role transfer to (or found) a company of that
  role, or the promotion is refused with a reason.
- Mixed companies deploy as **one squad** of mixed troop types (speed of the
  slowest man); casualties come off the right stacks; cards show the largest
  stack plus a tier strip.
- Company identity deepens: **origin** (culture + home settlement; mixing
  cultures costs cohesion), generated **banner**, up to three **honours**
  (veteran perks unlocked by history: unbroken streaks, cavalry kills,
  sieges stormed…), and a battle **ledger**.
- Interface: a party screen with expandable stacks (pool bars, "N READY"),
  drag stacks between same-role companies, and a promotion picker (branch
  cards, count stepper, plain-spoken block reasons: "NEEDS A TOWN",
  "NEEDS 3 HORSES").

### F2. Cultures and regional recruitment (medium)

- Every troop gains a **culture** (two southern cultures + northern, plus a
  shared common core at low tiers). The southern tree splits into two
  recognisable branches by lineage and look.
- **Villages raise their own culture's troops** — conquered villages keep
  theirs, so holding foreign land means foreign recruits. Culture lines are
  entered from that culture's recruits (no turning southern levies into
  northern berserkers). **Elite lines (T5+) need a matching-culture barracks**
  in a held fief — or mercenaries.
- Settlement owners' opinion of you scales how many recruits you're offered.

### F3. Characters and relations (medium)

- A shared **character** record for lords, companions and the captain:
  name, faction, culture, rank, age, 2–3 **traits** (honourable, cruel,
  cautious, reckless, greedy, generous, pious, ambitious, loyal, fickle),
  **opinion** of others (−100…100), and short **memories** ("you rescued
  me", "you razed my village").
- Defeated lords are captured or escape and **come back**; the rare slain
  lord is succeeded by an heir. Traits drive AI (reckless takes worse odds,
  cautious flees earlier, greedy raids, loyal never defects).
- Opinion moves on events with weights and decay, and gates fief requests,
  council votes, defections and peace terms. A **characters screen** shows
  lords by realm with traits, opinion bars and the memories behind them.
- **Companions:** veteran officers (or tavern wanderers) join as characters
  with a **role** (quartermaster, surgeon, scout, drillmaster, steward),
  fight as hero units, and can later be raised to vassals with their own
  parties and fiefs.

### F4. Captain skill tree (medium)

The eight perks become **eight skills that rise by use** (0–100), each with
a perk choice every 25 points: Leadership (command), Tactics (outnumbered
wins), Logistics (marching), Training (promotions), Medicine (wounded),
Stewardship (ruling), Charm (opinion), Arms (your own kills). Two captains
played differently for 20 battles should end meaningfully different.
Renown keeps raising the men limit but stops buying perks.

### F5. Career ranks and the council (large) — the endgame

- **Ranks:** Free Company → Sworn Sword (single-realm contract) → Vassal
  (swear fealty; fief grant, council vote, host summons) → Lord (2 fiefs;
  raise vassals, propose wars) → **Marshal** (lead the realm's hosts, set
  objectives, summon lords) → **King** (found a realm).
- Each rank is a **career beat** (event panel + new guidance); each unlocks
  capabilities, not just headcount.
- **Realm councils** (fortnightly): lords vote, weighted by fiefs, on fief
  grants, war objectives, war/peace and the marshal. Repeatedly overruled
  lords sour on their liege — which drives defection.
- **Founding a realm** by rebellion (as a lord holding a town; sympathetic
  lords may follow with their fiefs) or by claim (hold a town 14 days
  liegeless, then declare). You name it and choose its heraldry.
- **Win:** as King, hold ~60% of the provinces (or leave every other realm
  destroyed or tributary); as Marshal, win the "kingmaker" ending if your
  realm gets there. Victory shows a campaign summary, then sandbox freely.
  **Loss:** ironman captain death, or losing your last settlement and party.

### F6. Fiefs (medium)

- Settlements gain **prosperity** (peace/buildings up; raids/sieges/taxes
  down; scales income and recruits), **unrest** (foreign owner, taxes,
  sieges, raids up; garrisons and stewardship down — high unrest revolts or
  defects), **culture**, and **buildings**: training yards, culture
  barracks, walls, market, granary, stables, chapel, watchtower.
- AI lords build to a simple priority (border walls → market → barracks).
  Your vassals take **standing orders** (defend, follow, join the host) but
  steer themselves. Miserable lords defect with their fiefs (the loyal never
  do).

### F7. Strategic war and diplomacy (large)

- Realms at war hold an **objective** (a target settlement); the marshal
  raises a **host** (summoned lords come if able and willing), marches and
  besieges; others defend and raid. You can be summoned (answer or snub at
  an opinion cost); as Marshal you pick the objective and summon lords.
- **War exhaustion** (losses, lost seats, time) pushes toward peace.
- **Diplomacy** (kings, and AI between realms): declare war, offer peace
  **with terms** (cede land, pay gold over weeks, release captives, become a
  **tributary** realm), propose alliances, exchange captive lords.
  Acceptance weighs war score, exhaustion, strength and rulers' opinion.
  The truce timer goes away, replaced by terms.

### F8. Events and quests (medium)

- **Data-driven events** with triggers on world state (post-siege famine, a
  lord who hates his liege, an oversized brigand band, a company earning an
  honour), narrative text, and 2–3 choices with real effects. At most one
  every few days, never mid-battle.
- **Lord quests** (escort, deliver ransom, hunt a rival, take a castle)
  paying opinion first, gold second. **Named threats:** brigand bands can
  grow a **brigand king** with a lair; revolts can raise a **pretender** —
  the mid-game set pieces.

### F9. Pacing retune (small, last)

Retune XP/renown/rank curves against telemetry so career stages land on
target: vassal in ~3–5 hours, lord in ~4–6 more, king/marshal in ~6–10 more,
a full win in ~20–30 hours. A company should have a promotion ready about
every second battle; first T5 men around the vassal rank.

### Smaller gaps (fold in alongside)

- Escort-a-supply-column contracts (needs supply columns on the map).
- Captured lords taking part in peace terms (arrives with F7).
- Animated water, torch-lit nights, bridge models on the map.
- Full-campaign play-testing and economy tuning against telemetry; broader
  machine/GPU verification.

---

## 11. Open questions (current proposals in brackets)

1. Southern culture split: divide the existing southern tree by look and
   lineage [yes, with a shared T1–T2 common core], or paint new trees?
2. Promotion out of role: auto-transfer the man [yes, to the first fitting
   company with room], or forbid cross-role promotion?
3. Win threshold: ~60% of provinces [proposed], or holding all capitals?
4. Companions in battle: hero units like the captain [proposed], or a stat
   bonus to their company?

---

## 12. Suggested build order (for a fresh implementation)

Proven once already; each step ends playable:

1. **Battle core first:** melee stages, directional captain combat,
   blocking/parry, morale/routing, flanks, leadership aura and rally,
   basic AI that advances and fights. Playable skirmish.
2. **Cavalry + counters:** momentum, couched lance, trample, bracing,
   horse death and loose horses, cycle-charge AI. Then archers, fatigue,
   terrain effects, weather, deployment, reserves.
3. **Siege assaults** as a battle mode (walls, ladders, gate, walkway).
4. **Company ledger before any map:** persistent companies in a *chain of
   battles* with camp screens (recruit/heal/upgrade/merge/split), XP,
   wounds, loot, saves. Prove the progression loop in isolation — it is
   the heart of the campaign.
5. **The map:** generation, movement, settlements, brigands,
   encounters → battles → results.
6. **Economy:** gold, upkeep, contracts, recruits, mercenaries, food.
7. **Lords and war:** lord AI, faction AI, hosts, war score, truces,
   auto-resolve.
8. **The captain:** renown, perks/skills, gear, wounds/capture, onboarding
   hints.
9. **Then section 10**, in order: F1 → F9.

Non-negotiable engineering rules that kept this game shippable (stated as
game-team rules, not tech choices): the battle and campaign simulations
must run **without any graphics** so whole battles and campaigns can be
auto-played for balance; all troop/economy/naming content lives in
**data**, never code; art follows one style document with no exceptions;
every tuning number from this doc starts as a **starting value for
play-testing**, not a promise.

---

*Companion docs: art/interface direction, the as-built campaign spec, the
full overhaul spec with data shapes, and the roadmap history live alongside
this file. This document describes the game; those describe the build.*
