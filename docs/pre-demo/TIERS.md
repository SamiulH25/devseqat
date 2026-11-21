# Tiers that matter (plan)

The owner's brief (2026-11-21): "upgrading from levies to swordsmen makes a visual change but it's not clear how much of a
gameplay change it makes. Why should I care that it's upgraded, why should the user care? Why would a player want to get high
tier soldiers?" This plan answers that from three sides: what a tier does, how the player sees it, and why it pays.

Phase prefix: **TI**. Nothing is built yet.

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
  - equal upkeep: the trade is even near tier II, and quality pulls ahead from IV
  - equal places: the higher tier wins
- The flat steps found here (levies to shieldmen first) are what TI1 fixes.

## TI1. A ladder of things each tier can do

Every line shares one ladder, so a player learns it once:

| Tier | Grade | What it can do (new at this tier) |
|---|---|---|
| I Levy | Rabble | Fights in a crowd: loose files, lunges out one by one, breaks early. **No shield wall, no brace**. Hold, charge and follow only |
| II | Trained | **Keeps formation:** line, shield wall, brace against horse; rallies once after a rout |
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

## Open questions for the owner

1. **The ladder:** is the TI1 ladder right, especially taking the shield wall and brace away from tier I levies?
2. **The trade:** at equal upkeep, should quality pull ahead from tier IV, or should cheap men stay an even trade all the way up?
3. **Losses:** how hard should losing an elite company hurt? Only gold and time, or fame and named men too?
4. **The fight preview:** is it wanted on the promotion screen, or is it too much like a calculator?
