# Tiers that matter (plan)

The owner's brief (2026-11-21): "upgrading from levies to swordsmen makes a visual change but it's not clear how much of a
gameplay change it makes. Why should I care that it's upgraded, why should the user care? Why would a player want to get high
tier soldiers?" This plan answers that from three sides: what a tier does, how the player sees it, and why it pays.

Phase prefix: **TI**. TI0 and TI1 built (see "As built" at the end).

## Where things stand (2026-11-21)

A tier already does a lot, but almost all of it is hidden.

| Tier | What changes now | Where it lives |
|---|---|---|
| Every step | hp, damage, armour, block go up; an upgrade may never be worse (`Troops.WorseUpgrade`) | `Data/troops.json` |
| Every step | drill: 0.10, 0.10, 0.22, 0.38, 0.54, 0.72, 0.92 (tier 0 to VI). Drill keeps the ranks closed, refills gaps, raises the guard, holds the urge to lunge, and braces against horse (footing) | `BattleDrill.cs` `TierDrill`, `BattleRanks.cs`, `BattleUrge.cs`, `BattleGrid.BraceMass` |
| By grade | Rabble, Trained, Veteran or Elite from the drill sets the company's wits: wheel to a flank, wrap, rotate tired men, pincers; horse rally; bows fall back | `BattleTactics.cs`, `BattleWings.cs` |
| IV to VI | an attack style (crush, sweep, riposte...), passive traits, a culture's signature | `BattleStyles.cs`, `BattleEffects.cs` |
| Cost | more upkeep, more places in a company (1.3 at IV, 1.6 at VI), promotion to IV and up only in towns, V and up only in the people's towns | `Rules.TierWeight`, `CanTrainHere` |

The problems:

- **The first steps are thin.** Levies to shieldmen adds 30 hp, 6 damage, 5 armour and +0.2 block. Drill is 0.10 at both tier I
  and tier II, so levies and shieldmen are both Rabble and behave the same in the line. The new kit is real, but nothing on
  screen says so.
- **The player never sees the difference.** The promotion screen compares stats. In battle, a Rabble and a Veteran company look
  and act alike until you study them, and the grade is a word on a card.
- **There are no new things to do.** A new tier never unlocks an order or a behaviour the player can point to. Grades unlock
  wits, but silently.
- **The reason to want them is weak.** On the map, the only pay-off is a bigger auto-resolve number. Nothing in the campaign
  asks for good troops, and losing an elite company costs only gold.

## The target

- **Every tier is a step you feel.** At equal numbers, the next tier wins clearly. Each tier also adds something you can name:
  an order, a behaviour, a trait.
- **You can see it.** On the field, a drilled company looks drilled: tight files, in step, guards up, a ragged crowd beside it.
  On the map, the promotion screen says what the men will do that they can't now.
- **It pays.** Good troops win battles you could not win with numbers, cost less to feed per unit of strength, and open
  contracts, renown and fame. They also become the named veterans the company's legacy is built on (docs/archive/LEGACY.md).
- **It is a choice.** Many cheap men or few good ones is a real trade at every stage. Neither is always right.

## TI0. Measure

- A tier matrix run (`--tiers`, beside `TierSurvey`), for each line and each step. It fights three matchups:
  - the same number of men against each other
  - the same upkeep against each other
  - the same company places against each other
  
  It reports wins, men left and time to break. It also runs auto-resolve on the same matchups and compares the two.
- **Targets** (owner to agree):
  - equal men: the next tier wins nine times in ten, keeping about a third more men
  - equal upkeep: the trade is even near tier II, and quality pulls ahead from III (owner, 2026-11-21)
  - equal places: the higher tier wins
- The flat steps found here (levies to shieldmen first) are what TI1 fixes.

## TI1. A ladder of things each tier can do

Every line shares one ladder, so a player learns it once:

| Tier | Grade | What it can do (new at this tier) |
|---|---|---|
| I Levy | Rabble | Fights in a crowd: loose files, lunges out one by one, breaks early. **No shield wall, no formations**. Hold, charge, follow and brace only |
| II | Trained | **Keeps formation:** line, shield wall, a firmer brace; rallies once after a rout |
| III | Trained+ | **Answers orders quickly:** orders land a beat sooner, wheels to face a flank, relieves the line, can be split |
| IV | Veteran | **Its attack style** (crush, riposte, thrust through...); re-forms by itself after its ranks break; a square or wedge on order; pursues, then comes back |
| V | Veteran+ | **Passive traits**; shrugs off fear (monsters, the dead); rallies twice; fights from the ranks with the rear rank's spears or bows |
| VI | Elite | **The line's signature;** takes the initiative (pincers, rolling up a flank, focus fire); its banner steadies the companies around it |

- The drill numbers move so that tier II is Trained, not Rabble (`TierDrill`). The grade then follows the tier as shown.
- Orders a company can't yet obey are greyed out on its order deck, with the reason ("SHIELD WALL needs tier II"). The rabble
  learning the shield wall is then the moment you feel the upgrade.
- Stats keep rising, and the first steps get a bigger jump, set by TI0's numbers.

## TI2. You can see it in battle

- **Drill on the body:**
  - Rabble stand ragged, out of step, at odd angles and with shields low.
  - Trained men stand in files with shields up.
  - Veterans stride and swing in step and dress their ranks as they move.
  - Elites move as one.
  
  This is drawing only, read from `Squad.Drill` and `Formed` (`Puppet`, `PuppetPoses`).
- **Kit you can read:** each tier's look adds pieces in a fixed order: cloth, then a helm, then mail, then plate. A tier reads
  at battle distance by its silhouette, not by its colour (docs/DESIGN.md).
- **The card:** a grade plate on every company card (RABBLE, TRAINED, VETERAN, ELITE) with the tier's pips. Hovering it lists
  what the company can do, from TI1's ladder.
- **Moments that show the difference:**
  - a call when a drilled line holds a charge ("THE LINE HOLDS")
  - a veteran company re-forming after breaking ("RE-FORMED")
  - a rabble company breaking ranks ("BROKE RANKS")
  
  These reuse the alerts (`BattleAlerts.cs`) and the fight chips (`Hud.Fights.cs`).
- **The battle report:** losses and kills by tier ("your men-at-arms lost 4 and killed 31"), so a player sees which men earned
  their pay.

## TI3. You can see it on the map

- **The promotion screen** (`MapScreen.Upgrade.cs` `TroopDetail`) leads with what changes, as pictured cards:
  - "+ SHIELD WALL"
  - "+ HOLDS AGAINST HORSE"
  - "+ RALLIES ONCE"
  - "+ ATTACK STYLE: RIPOSTE"
  
  Stat bars come after, with the new numbers beside the old. The cost goes last: upkeep a week, places in the company, where
  the promotion can be done.
- **A fight preview:** a small "against your current men" strip showing how ten of the new troop fare against ten of the old,
  from TI0's matrix (precomputed, not a live sim).
- **The promotion itself:** a line in the report and a toast ("14 of the Black Company are now men-at-arms: they can form a
  shield wall"), and the company card's grade plate changes.
- **The party board** (`MapScreen.Board.cs`): each company tile shows its grade plate and a strength figure that weighs drill,
  so a small good company shows as strong.

## TI4. Why it pays: the campaign wants good troops

- **Strength per mouth:** food and wages are per man, but strength rises faster than cost from tier III up. A smaller, better
  army eats less, marches faster (rabble is already slow, `PaceOf`), and fits more strength into the command cap and company
  places.
- **Battles numbers can't win:**
  - against horse, only formed foot of tier II and up brace (TI1)
  - against monsters, fear doesn't reach tier V
  - in sieges, veterans climb ladders and hold points (SIEGES.md)
- **Work that asks for quality:** contracts with a minimum, such as "a company of veterans to hold the pass" or "elite horse for
  the king's guard", that pay well. Lords' respect and renown grow with the grade of the company you lead.
- **The legacy:** named men (docs/archive/LEGACY.md) rise from the companies that live long, and veterans live longer. Your
  sergeants, champions and the men on your roll come from them. An elite company with a name and banner honours is the thing
  the player builds.
- **Losses hurt:** losing an elite company should cost more than gold. It takes time and a town to replace, its named men fall,
  and the company's fame takes a knock. The player then fights to keep them alive, which is the care the owner asks for.
- **The trade stays real:** cheap levies still win by weight of numbers in auto-resolve, in sieges and against brigands. Many
  cheap men stays a good choice early on and a poor one later.

## TI5. Lines that feel different

- Branches at the same tier must have different jobs, not the same soldier with other stats:
  - **pikes** stop horse and are slow
  - **billmen** pull riders down and break shields
  - **greatswords** cut through a line but have no shield against arrows
  
  Write each branch's job in one line and check the data matches it (`tools/troopmap.py` gains a job column).
- An audit of the 387 troops for twins (the same stats under another name at the same tier) and for steps that go nowhere.
  Merge them or give them a job.
- Tier VI is each line's legend: a name worth knowing, a look that stands out, a signature only it has.

## TI6. Check and tune

- `--tiers` against the TI0 targets; `--balance`, `--agree` and `--realms` to check the AI's armies and auto-resolve still match
  played battles.
- Shots: a pose sheet of each grade standing, marching and fighting (`IRONFIELD_SHOTS_FROM=poses`), the promotion screen, and
  the party board.
- Tests:
  - `TierFight`/`TierSurvey` stay green
  - new tests for the order gates (no shield wall at tier I), the grade plate and the report's by-tier lines

## Order and size

TI0 first: the numbers decide how big each step must be. TI1 is the heart. TI2 and TI3 make it visible and can run beside each
other. TI4 needs TI1 and touches contracts and the legacy. TI5 is a data pass that can come at any time after TI1. TI6 closes it.

Size: large in total. TI0, TI1 and TI3 are medium on their own.

## The owner's answers (2026-11-21)

1. **The ladder:** as written, except **levies keep brace** against horse (a crowd of spears). They still get no shield wall
   and no formations: hold, charge, follow and brace only. Tier II brings the shield wall and formations.
2. **The trade:** at equal upkeep, quality pulls ahead **from tier III** (even near tier II). TI0's target changes to match.
3. **Losses:** losing an elite company costs gold, time (a town and weeks to replace), its named men and the company's fame.
4. **The fight preview:** yes, the strip on the promotion screen: ten of the new troop against ten of the old, wins and men
   left, from TI0's matrix (precomputed).

## As built

### TI0 (measure), as built (2026-11-21)
- `dotnet run -c Release --no-build -- --tiers [seeds] [filter]` (`BalanceTiers.cs`: `Tiers`, `TierLinks`, `ParentMen`, `TierBout`).
  Every upgrade link that keeps its class and shooting (370) fights its parent: 30 of the upgrade against 30 (men), against
  30 x cost ratio (upkeep; wages are per man from `UnitType.Cost`) and against 30 x `TierWeight` ratio (places), on flat ground,
  foot charging, bows holding and shooting, as `SelfTest.TierFight`. Bouts run in parallel (each its own `Battle` and dice, so the
  numbers do not depend on the core count): about 3 minutes for 3 seeds on 16 cores. Writes `balance/tiers.csv` (one row a link and
  matchup: wins, men kept each side, seconds, `Rules.Might` each side, whether auto-resolve calls the same winner) and prints a
  table by tier step and the flattest steps. A filter is an id fragment or a culture. Test `TierMatrix`.
- **What it found** (3 seeds, 2026-11-21):

| Step | Equal men: up wins, kept up/down | Equal upkeep: up wins (parent men x) | Equal places: up wins | Auto agrees at equal upkeep |
|---|---|---|---|---|
| I -> II | 100 %, 0.83 / 0.00 | 0 % (x2.4) | 100 % | 100 % |
| II -> III | 100 %, 0.71 / 0.00 | 4 % (x1.9) | 90 % | 73 % |
| III -> IV | 100 %, 0.79 / 0.01 | 52 % (x1.6) | 99 % | 56 % |
| IV -> V | 100 %, 0.71 / 0.00 | 35 % (x1.5) | 99 % | 34 % |
| V -> VI | 100 %, 0.70 / 0.00 | 59 % (x1.4) | 99 % | 61 % |

- **Reading it:**
  - *Equal men* is far past the target at every step: the upgrade wipes its parent and keeps about three in four. Levies to
    shieldmen is not thin in a fight; it is thin on screen and in behaviour (both are Rabble). TI1 and TI2 fix that, not the stats.
  - *Equal places* meets the target (the higher tier wins).
  - *Equal upkeep* misses badly: the wage steps are steeper than the strength steps. A tier II man costs 2.4 levies and a tier IV
    man 2.6 tier III men (average `cost` by tier: 10, 36, 52, 135, 192, 278), so the same wage bill buys a crowd that wins until
    tier IV, and IV -> V dips (bows 4 %, mages 0 %). The owner's target (even at II, ahead from III) needs the wage curve
    flattened or a tier's worth raised for its price; with equal-men fights already one-sided, the wage curve is the lever.
  - *Auto-resolve* calls the equal-upkeep fights wrong a third to two thirds of the time from tier IV up: `Rules.Might` rates
    tier V bows and mages above what they do in a fight.

### Wages by tier (TI4's lever, done with TI1, 2026-11-21)
- `Rules.WageCost(t)` = `WageAnchor` x (cost / `WageAnchor`) ^ `WageCurve`; `Rules.Wage` and `Rules.Upkeep` use it; hire, promotion,
  ransom and loot keep `cost`. `WageCurve` 0.6, `WageAnchor` 36 (Balance, Data/campaign.json). A levy's wage rises about two
  thirds, a tier VI man's falls by more than half.
- Tried 0.7 (equal upkeep: 18 / 33 / 76 / 64 / 80 % by step: crowds still win to III), 0.5 (77 / 54 / 89 / 88 / 93 %: quality wins
  everywhere), 0.6 (58 / 48 / 81 / 79 / 82 %).
- `--pacing 4` at 0.6 (with the C8 pilot): 4/4 seeds Marshal (days 106-162), 510 of 538 fights won, more renown than at 1.0.
  The campaign plays a little easier; the targets (Vassal day 40-67...) were already met early before.

### TI1 (a ladder of things each tier can do), as built (2026-11-21)
- **Drill** (`BattleDrill.TierDrill`): 0.10, 0.34, 0.46, 0.60, 0.72, 0.88 for tiers I-VI, so the grade follows the ladder: I Rabble,
  II-III Trained, IV-V Veteran, VI Elite (cohesion can lift a tier III company to Veteran). Tried 0.32/0.50 for II/III: no change
  in the II -> III step, which is the wage ratio's, not drill's.
- **Formations by tier** (`Squad.Tier`: the tier most of its men stand at; `FormationTier`, `CannotForm`, `Shapes` = the kit's,
  `Choices` = the learned ones): the shield wall at II, square and wedge at IV; line and open ranks always. The order deck shows
  the kit's shapes and greys the unlearned ones ("SHIELD WALL NEEDS TIER II. PROMOTE THE MEN TO TEACH IT."); `SetShape` leaves a
  company that has not learned it as it is. The AI's shield wall (`BattlePosts`) respects it. Levies keep the brace (owner).
- **Rallies by tier** (`Battle.RalliesOf`): levies never come back after breaking, II-IV once, V-VI twice (creatures once).
- **Fear** (`BattleMorale`): a monster's fear does not reach tier V and up. **Steadied:** a tier VI man within 8 m adds +0.4
  morale a second to lower-tier men of his side.
- **Relief and splitting** need tier III (`CannotRelieve`, `CannotSplit`).
- **Found on the way:** a man walking into a tree trunk stood against it for ever (`Terrain.PushOut` only pushed him back); he
  now edges round it the way he leans (`BattleMove.Move`). It stalled the level-4 battle of `ResolveBattles`.
- `--tiers 3` after TI1 (equal men / upkeep / places, up wins): I->II 100 / 73 / 100 %, II->III 100 / 40 / 90 %,
  III->IV 100 / 81 / 99 %, IV->V 100 / 83 / 99 %, V->VI 100 / 82 / 98 %. Auto-resolve now agrees 73-85 % of the time.
- **Not built:** "orders land a beat sooner" at III (orders already land at once since OF0), IV pursuing and coming back, V
  fighting from the ranks with the rear rank's spears or bows. **Open:** the II -> III step at equal upkeep (40 %) is still
  short of the owner's "ahead from III": a TI6 tuning item (tier III stats or its wage).
- `--pacing 4` after TI1 (wages and drill together): 4/4 seeds Marshal (day 162 each), 507 of 527 fights won; Vassal days 46-85,
  Lord days 111-134 (target 93-147, all four inside it; before TI1 three of four were early).
- Tests: `TierLadder` (gates, rallies, grades), `RelieveAndSplit` and `ElitePincer` set up for the new drill (tier III companies
  relieve; the pinned foe in the pincer is rabble).
