# Captain progression and gameplay: plan

Status: **P1 built; P2 next** (built steps are listed under "As built" at the end). This plan covers the captain as a person: the skills and perks they grow, the
gear they carry, how they fight with their own hands, and what each step of the career gives *them* (not only the realm).
The campaign loop (companies, career, fiefs, war, creatures, side content) is built and is not changed here, except
where a phase says so. Numbers marked *(tune)* are starting values.

## Where the captain stands (2026-11-07)

Found by reading `Campaign/Skills.cs`, `Rules.CaptainType`, `BattleCaptain.cs`, `BattlePlayer.cs`, `Campaign/Career.cs`:

- **Skills are flat.** Eight skills (Leadership, Tactics, Logistics, Training, Medicine, Stewardship, Charm, Arms), four
  perks each, all 32 wired. A choice opens every 25 points and any perk on the branch can be taken: no order, no
  prerequisite, no capstone. At 100 a branch is simply full.
- **Only three skills rise in battle.** Leadership (men led), Tactics (winning outnumbered) and Arms (your own kills,
  0.45 a kill). How you fought (parries, a lance charge, a duel won, the banner held) earns nothing.
- **One body of gear.** `CaptainState` has `ArmorTier`, `HelmTier`, `HorseTier`: four one-time buys (plate, great helm,
  warhorse, destrier). Once bought there is nothing more to want. Loot never touches the captain.
- **One weapon.** The captain fights with sword and shield; N swaps to a greatsword that is a clone of the same type
  (`Damage * 1.35`, `Reach + 0.5`, no shield). No spear, axe, bow or crossbow for the captain, though troops use all of them.
- **Combat has its pieces but no depth curve.** Timed parry (`ParryWindow` 0.35 s), feint (cancel into a block), held
  swings scaling power, shield bash/kick, couched lance, rally, calling a horse. None of it improves with skill: a
  captain at Arms 90 swings and parries like one at Arms 15, only with more health.
- **The career is political.** Each rank opens realm powers (fiefs, council vote, war proposals, the objective, a crown).
  Nothing about the captain changes with rank: no household guard, no title in the field, no better armourer.
  Renown past the ranks only raises the men cap (`Rules.NextRenownUnlock`).
- **Loose end:** a free captain cannot claim a realm (`ROADMAP.md` F5 "Not yet").

## Goals

1. **Every battle grows the captain a little, and how you fought decides what grows.**
2. **Something to want at every stage:** a perk, a piece of gear, a rank reward, always one in sight.
3. **Skill shows in the fight:** a high-Arms captain fights visibly better, not just longer.
4. **Two captains end different:** a mounted lancer, a shield-wall sergeant, a bowman lord and a schemer play differently.

### Out of scope
A full RPG inventory (no item grid, no durability), character creation with backgrounds beyond a starting kit choice,
first-person view, magic, multiplayer. Companions keep their current roles (only P4 touches them).

## Phases

Each phase is shippable alone, ends with `tools/check.sh` passing, and adds self-tests. Order is the build order.

### P1. The armoury: weapons and gear tiers

The captain's kit becomes a small loadout instead of four flags.

- **Save:** `CaptainState.Kit` (a small class): `Main` (weapon id), `Side` (second weapon id or ""), `Shield`, `Body`,
  `Helm`, `Horse` as string ids into a new `Data/gear.json` (`id, slot, name, cost, needs: {skill, level}, rank, stats, look`).
  Old saves: `ArmorTier/HelmTier/HorseTier` map to the matching ids on load (the fields stay, read once, then ignored), so no
  version bump.
- **Weapons** *(tune)*: arming sword, longsword, axe, mace (armour-piercing, short), spear (reach, thrust only), greatsword,
  dane axe, lance (mounted), hunting bow, war bow, crossbow (slow, strong). Each has damage, reach, windup, recover, a
  stamina cost per swing and a list of allowed swing directions (`AttackDir`). Ranged weapons give the captain `Ammo`.
- **Armour** in four tiers per slot: padded, mail, brigandine/plate, and a gilded lord's harness (rank Lord, see P5).
  Heavier armour: more `Armor`, less speed and stamina regen *(tune)*. Shields: round, heater, kite, pavise (pavise only on foot, huge block arc).
- **Horses:** courser, warhorse, destrier, and a barded destrier (rank Vassal).
- **Shop:** the town Market's gear list becomes an armourer tab, stocked by town size and culture (a few items, refreshed weekly).
  Selling back pays 40%. `Rules.CaptainType` and `CaptainMount` build the unit from the kit; `SwapWeapon` swaps `Main`/`Side`
  instead of cloning the greatsword.
- **Loot:** beating an enemy leader (`BattleResult` foe leader down) offers his weapon or body armour in the report
  (one item, take or leave). Tournaments (`Town.FinishTournament`) can prize a piece of gear.
- **Done when:** a captain can buy, wear and swap any weapon; each weapon fights differently in `--selftest` duels
  (spear outranges sword, mace beats plate, bow kills at 40 m); the battle shots show the new looks.

### P2. Captain combat that rewards skill

Arms (and the new skills of P3) shape *how* you fight, so practice shows on the field.

- **Riposte:** a parry inside the window opens a 0.4 s riposte; a swing started in it winds up 50% faster and cannot be
  parried by AI of tier I-III *(tune)*.
- **Chained blows:** a swing released within 0.25 s of the last recover chains into the next direction with a shorter windup;
  each link costs more stamina, so spamming empties you. This is the C3 promise: skilled play beats spam.
- **Skill in the numbers:** Arms scales windup/recover (-0.15% per point), parry window (+0.002 s per point) and the
  stamina cost of swings. Riding (P3) scales mounted turn rate and lance damage; Bowcraft scales draw time and sway.
- **Captain archery:** with a bow or crossbow, hold attack to draw, the aim reticle tightens over the draw, release to
  loose (an arrow through the existing `Shoot`/`UpdateArrows`). Arrows from your wagon refill your quiver.
- **Mounted swings:** swing left/right/overhead from the saddle with the speed bonus already in `Hit`.
- **Duels:** before a field battle against a lord, the encounter panel may offer *Challenge their captain*. A duel is a
  small battle on `Battle.Empty` ground (like a tournament bout): win and the enemy starts the battle at -15 morale and you
  gain renown; lose and you start wounded. The AI lord accepts by trait (Reckless/Honourable yes, Cautious no).
- **Done when:** a scripted test captain that parries and ripostes beats a tier III swordsman more than 80% of bouts; a
  spamming one loses more than half; duels play and apply their effects; replays stay exact (`ReplayExact`).

### P3. A deeper skill tree

- **Three new skills split off Arms:** Arms (melee on foot), Riding (mounted: rises with lance hits and kills from the saddle)
  and Bowcraft (rises with your arrow hits). Arms keeps its health bonus; heavy gear needs Arms, barded horses need Riding,
  war bows need Bowcraft. Existing saves copy Arms into Riding and Bowcraft at half.
- **Tiers on each branch:** perks get a `Tier` (1-4) and `Needs` (another perk on the branch, optional). Tier 1 at 25,
  tier 2 at 50, tier 3 at 75, and a **capstone** at 100 that needs two of the branch's earlier perks. Each branch keeps its
  current four perks spread over tiers 1-3 and adds one capstone *(names tune)*:
  Leadership *Warlord* (companies under your banner never rout before you do), Tactics *Master of War* (see the enemy's
  plan arrows during deployment), Logistics *Forced March* (one day a week at double speed), Training *Academy* (one man a
  week promoted free), Medicine *Field Surgeon* (the captain gets up once per battle), Stewardship *Builder* (buildings
  half price), Charm *Kingmaker* (council votes go your way at +20), Arms *Champion* (riposte always on after a parry),
  Riding *Knight of the Charge* (couched lance cannot be stopped by braced spears below tier IV), Bowcraft *Deadeye* (no sway).
- **More ways to rise:** parries, ripostes and duels won count for Arms; holding a flag point, a lance kill, an arrow kill
  for their skills; a battle won with few losses for Tactics; days with full food for Logistics. All through `Practise`.
- **A legible rise:** the battle report gets a line per skill that rose (`ARMS 42 -> 44`), and crossing a mark says which
  perks just opened.
- **Retraining:** at a town with a training ground, forget one perk for gold (rising each time) and choose again.
- **Done when:** perks can only be taken in tier order with their needs met; capstones work; the K sheet shows tiers as
  rows; the F4 test (two captains end different) extends to the three new skills.

### P4. The captain's body and name

- **Lasting wounds:** when the captain falls, a small chance *(tune 15%, Medicine lowers it)* of a lasting wound from a short
  list (a scar: +5 renown in towns, harmless; a bad knee: -10% speed on foot; a broken hand: -10% damage; a lost eye: -5% parry
  window). A surgeon companion or a shrine site (`Sites.cs`) can heal some. Listed on the captain card.
- **Earned traits:** the captain gains traits from what they did, through the deeds already kept in `Record.cs`: *Lionheart*
  (duels won, battles led from the front), *Merciful* (prisoners freed), *Ruthless* (prisoners executed, caravans robbed),
  *Steadfast* (battles held outnumbered), *Coward* (battles fled). Traits move lords' opinion through the existing
  `Characters` opinion rules and are triggers for events in `Data/events.json`.
- **Age:** past 45 the captain loses 1 Arms a season but gains Leadership/Tactics faster *(tune)*; an heir chain is out of scope.
- **Done when:** a wound and a trait can each be earned and lost in a test; an event triggers on a trait.

### P5. What each rank gives the captain

The career ladder (`Career.Ladder`) gets a personal reward per rank, shown in the ladder and as the rank beat.

| Rank | New for the captain *(tune)* |
|---|---|
| Free Company | Choose a starting kit: swordsman, lancer or bowman (P1 gear, P3 starting skills) |
| Sworn Sword | The realm's armourers sell you their culture's gear; a +1 perk choice once |
| Vassal | A **household guard**: one company of 15 tier III men in your colours that never deserts and stays at your side in battle; barded horses |
| Lord | The gilded harness; your banner in battle gives +5 morale in its aura; guard to 25 men, tier IV |
| Marshal | A second division sergeant is free; the guard rides as knights |
| King | A crown look; the royal guard (40 men, tier V); a once-per-battle *King's Charge* rally that brings routed companies back |

- **Free road to a crown:** close the F5 loose end. A Free Company with 400 renown *(tune)* may besiege a holding of a realm
  it is at war with under a contract and, on taking it, declare it its own (Rank King of a new realm through the existing
  `DeclareIndependence` path).
- **Renown past the ranks:** after the men cap rung, renown also unlocks named titles (*the Bold*, *the Hammer*...) earned
  from the deeds and traits of P4, shown by your name.
- **Done when:** each rank's reward appears in the ladder and works; `--pacing 8` days for each stage stay inside the F9
  targets (rerun and retune if not); a free-company crown works in `--selftest PilotCareer`.

### P6. Gameplay goals in battle

Give the captain something to do in each fight beyond "win".

- **Battle deeds:** before a battle, up to two personal goals are drawn from the situation: *slay their captain*, *hold the
  hill until the horn*, *break their cavalry*, *no company routs*, *take the gate first* (sieges). Each shown on the HUD
  and paying renown and skill XP in the report. Lives in a new `BattleDeeds.cs` (headless, deterministic, seeded `rng`).
- **The banner:** your banner bearer is a unit; if he falls the aura goes and companies take a morale hit; you or a man can
  pick it up. Holding it all battle is a deed.
- **Last stand:** when the captain falls, a short choice (if a horse is near): be carried off (battle goes on, you watch from
  command view) or fight on wounded at half health (Medicine capstone: free).
- **Done when:** deeds appear, track and pay out in tests; the banner falls and is picked up in a scripted battle; replays stay exact.

### P7. The screens

- **Captain sheet (K):** a paper doll tab for the kit (P1), tiered perk rows (P3), wounds and traits on the card (P4), the
  rank rewards on the ladder (P5).
- **Armourer tab** in the town screen, with the item's stats against what you wear (`+3 ARMOUR, -5% SPEED`).
- **Battle HUD:** weapon and ammo in the corner, riposte flash, chain counter, deeds list (P6).
- **Battle report:** skill rises, the loot offer, deeds done.
- **F1 help and `README.md`** updated for the new keys and rules; a UI lab page for each new screen (`IRONFIELD_LAB_SHOTS`).
- **Done when:** the campaign and lab shots show every new screen readable at 1280x720.

### P8. Balance and the stand-in captain

- `Pilot.cs` picks a kit, buys gear when it can afford it and takes tiered perks, so `--pacing` and `--balance` still play
  a whole career.
- Rerun `--pacing 8` and `--balance 4` after each phase; numbers in `Data/campaign.json` (every new value also in `Balance`,
  `SelfTest.BalanceMatchesData`).

## Suggested order and size

| Phase | Size | Depends on |
|---|---|---|
| P1 armoury | L | - |
| P2 combat | M | P1 (weapons) |
| P3 skill tree | M | P1/P2 (new skills read gear and combat) |
| P4 body and name | S | - |
| P5 rank rewards | M | P1 (gear), P3 (kits) |
| P6 battle goals | M | P2 |
| P7 screens | M | built alongside each phase, polish pass at the end |
| P8 balance | S | after each phase |

## Open questions

1. Starting kit choice (P5 Free Company): a pick on New Campaign, or always the swordsman with the others bought later?
2. Duels: only before battles, or also as an event/tavern challenge in towns?
3. Lasting wounds in non-Ironman games: keep, or make them heal in a season?

## As built

- P1 (armoury, step 1): `Data/gear.json` (10 weapons, 4 shields, 4 bodies, 4 helms, 5 horses) loaded by `Gear.cs`;
  `CaptainState.Kit` (`CaptainKit`: worn ids per slot, `Side` weapon, `Owned`); free gear (cost 0) is always yours.
  `Rules.CaptainType(c, side)` builds the captain from the kit: the weapon sets damage, reach, windup, recover, pierce,
  ammo and range; two hands drop the shield; body and helm add armour and speed; the shield sets the block chance.
  Old saves: `Campaign.MigrateGear` (constructor) turns `ArmorTier/HelmTier/HorseTier` into plate, great helm and
  warhorse/destrier, then clears them (no version bump). The armourer is the right half of the MARKET tab
  (`MapScreen.Armoury.cs`): tabs per slot, this week's stock (`ArmourerStock`, seeded by place and week; towns 70%, castles
  45%, villages 20% and only cheap pieces), buy (worn at once), wear, carry second, drop, sell at 40%. Buying needs the
  Arms skill (`needs`), the career rank (`rank`: the lord's harness, the barded destrier) and gold.
  Simplifications: no lance item (the couched lance works with any weapon from the saddle); no pavise (no sprite); the
  lord's harness uses the plate look. The old `Balance` gear costs are gone (prices live in gear.json).
- P1 (step 2, the kit in battle): `BattleOptions.PlayerSide` is the second weapon (`Rules.CaptainType(c, side: true)`); a
  quick battle keeps the old greatsword clone. `SwapWeapon` (N) swaps the two, each keeping its own arrows (`sideAmmo`);
  two-handed melee weapons only on foot, bows also in the saddle. The boar spear only thrusts (`UnitType.ThrustOnly`). Your
  windup and recover scale with the weapon (`Windup / 0.4`, `Recover / 0.5`; the arming sword plays as before), and your
  melee blows use the weapon's pierce (the mace and axes). Captain archery (`PlayerBow`): hold attack to draw
  (`Windup * 1.75 / Reload` seconds; a crossbow aims fast and reloads slowly), release past 30% to loose at the enemy
  nearest your facing within 0.2 rad and range (`AimTarget`), damage and spread by draw; arrows refill at your wagon
  (`Resupply` now serves you too). `Shoot` and the player share `Loose`. The HUD shows shots left.
- P1 (step 3, spoils): `BattleReport.Spoil` (a gear id). After a win over a lord or brigands, `BattleSpoil` offers a piece
  the captain lacks (no rank pieces; up to 900 crowns from a lord, 300 from brigands): sure from a lord whose leader fell,
  else 35%; 60% / 15% for brigands. A tournament win gives a prize half the time (`TournamentPrizeChance`). The report's
  SPOILS well shows it with TAKE (`TakeSpoil`: owned, wear it at any armourer); closing the report leaves it. The pilot
  takes every spoil and buys body armour, helms and horses with three reserves to spare (`Pilot.Kit`).
