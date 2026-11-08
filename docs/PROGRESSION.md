# Captain progression and gameplay: plan

Status: **P1-P6 built; P7 next** (built steps are listed under "As built" at the end). This plan covers the captain as a person: the skills and perks they grow, the
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
- P2 (step 1, skill in the swing): `BattleCaptain.ReleaseSwing` decides each of your swings. A parry (`ParryTime`: 0.35 s
  + 0.1 s at Arms 100) sets `RiposteUntil` (0.45 s); a swing started in it is a riposte (windup x0.5, power x1.25, half
  the stamina, and `Hit` gives a tier I-III defender no block). A swing started within `ChainGap` (0.4 s) of the last
  recovery extends `Chain`: stamina 8 + 5 a link, AI block chance +8% a link (up to 4), and from the second link your hits
  no longer interrupt his windup (`Damage`), which is what ends the old stun-lock where constant swings kept a man from
  ever striking. Stamina no longer refills while you wind up, recover or hold a swing; a swing you cannot pay for is weak
  (x0.6) and slow (x1.4). `BattleOptions.Arms` (the campaign passes your skill; 15 by default) gives `ArmsEdge` 0-1:
  windup -15%, stamina a swing -25%, parry window +0.1 s. `SelfTest.SkillBouts`: against two men-at-arms a scripted
  parrying captain wins 10/10, a captain who only swings 0/10 (against one he still wins, at about half his health).
  Mounted swings were already in (any direction from the saddle, with the speed bonus).
- P2 (step 2, duels): `Campaign/Duels.cs`. CHALLENGE on a lord's encounter panel (`CannotChallenge`: lords only; not
  while wounded; one an encounter; Cautious never; a lord 1.5x stronger than you only if Reckless or Honourable).
  `PrepareDuel` builds a setup with empty armies and `BattleOptions.Duel` on the lists ground; the enemy leader is
  `LordChampion(party)` (the captain's type, +12 health, +2 damage, +1 armour a tier past II, plate from tier IV).
  In battle `SetupDuel` puts both on foot 10 m apart, the AI general just sends him at you, and `Result` reports a fall as
  Wounded on both sides (no capture, no slaying); the battle screen skips deployment. `FinishDuel` (from `FinishBattle`)
  keeps the encounter open: a win gives +10 renown, Arms 2.5, a deed, and the next battle against that party gets
  `BattleOptions.EnemyMorale` -15 (`DuelMoraleFor`; auto-resolve x1.1, a loss x0.95); a loss wounds you 3 days, so the
  battle has `PlayerWounded`. The state (`duelWith`, `duelWon`) is not saved and clears after the battle or a retreat.
  `SelfTest.DuelFight` (a parrying captain beats a tier IV lord 6/6) and `SelfTest.Duels` (acceptance, effects).
  Not done: duels in towns or as events, the AI lord challenging you.
- P3 (step 1, Riding and Bowcraft): two new skills in `Skills.Defaults` (10 now). Riding: horse health +1% per 4 points
  (`CaptainMount`), and `RidingEdge` (0-1 from 5 to 100) gives mounted turning +25%, saddle blows +10% and the couched lance
  +20%; perks Mounted Lord (moved from Arms; same id), Lancer (lance +25%), Saddle-Fast (gallop +10%), Horse Breaker (vs
  riders +25%). Bowcraft: `BowEdge` gives draw time -25% and scatter -30%; perks Fletcher (+50% arrows), Steady Aim (scatter
  -30%), Horse Archer (no saddle penalty), Marksman (arrows +20%). Arms trades Mounted Lord for Iron Grip (blocking costs 30%
  less stamina). `GearDef.Skill` names the skill `needs` counts: the war bow Bowcraft 20, the destrier Riding 30, the barded
  destrier Riding 40. `Rules.WithCaptain(options, captain)` fills every battle's captain part (skills, `Perks`, kit, horse)
  for field battles, tournaments and duels. The battle counts your deeds (`SaddleKills`, `ArrowKills`, `LanceHits`,
  `ArrowHits`, `Parries`, `Ripostes` in `BattleResult`), and `SkillsAfterBattle` splits the rise: Arms for kills on foot and
  parries/ripostes, Riding for saddle kills and lance hits, Bowcraft for arrows; an auto-resolved battle gives Arms (or
  Bowcraft with a bow in hand) and a little Riding. Old saves: `MigrateSkills` sets both at half the captain's Arms (min 5).
- P3 (step 2, tiers and capstones): `Skills.TierOf` gives each perk a tier by its place on the branch (the first two tier I,
  then II, III, and the fifth perk the capstone, tier IV; `PerkDef.Tier` overrides). `Skills.Locked` says why a perk cannot
  be taken: its tier needs the skill at 25 x tier (I 25, II 50, III 75, IV 100), a capstone needs two other perks of the
  branch, and a choice must be open (one per 25 points). The ten capstones: Warlord (near you, no company's morale falls
  below breaking while you stand), Master of War (auto-resolve +10%, order delay halved), Forced March (party speed +10%),
  Academy (drill XP +50%), Field Surgeon (once a battle you get back up at half health), Builder (buildings half price),
  Kingmaker (every lord +10 opinion of you), Champion (riposte window x1.5 and no one can block it), Knight of the Charge
  (braced spears below tier IV cannot stop your charge), Deadeye (no scatter at full draw). The K sheet draws each branch
  as two rows of tier I-III perks and the crowned capstone centred below; the tooltip shows tier and the lock reason.
  `SelfTest.PerkTiers`. Simplifications against the plan: capstones are milder (Forced March is +10% always, not a
  double-speed day; Academy speeds drill instead of a free promotion; Kingmaker is +10 opinion, not +20 votes); there is no
  per-perk `Needs` chain.
- P3 (step 3, a legible rise and retraining): `BattleReport.Rises` holds a line per skill that rose a whole point
  (`Skills.Rises`: `ARMS 42 -> 44`, plus `. PERK` when a 25 mark was crossed); field battles, duels and tournaments fill
  it and the report's YOUR CAREER well lists them (gilt when a perk opened). More ways to rise: a win that lost under 10%
  of your men adds a point of Tactics (`CheapWin`); parries and ripostes in duels and tournaments feed Arms; a day the
  company ends fed adds 0.03 Logistics. Retraining: on the K sheet, in a friendly town, click a taken perk twice to forget
  it (`ForgetPerk`; `CannotForget` says why not: not in a town, a capstone still standing on it, gold). It costs 150
  crowns, 150 more each time (`CaptainState.Retrained`), and the choice opens again. `SelfTest.SkillRises`.
  Not done: holding a flag point (no flag points until P6); the plan's "training ground" building (any town will do).
- P4 (step 1, lasting wounds): `Campaign/Wounds.cs`. When the captain falls in a field battle (wounded or taken; not in
  duels or tournaments) there is a 15% chance (`LastingWoundOdds`: Medicine down to half at 100, a Surgeon companion
  halves it again) of a wound he does not carry yet (`CaptainState.Wounds`): Scarred (+5 renown when earned, harmless),
  Bad Knee (speed on foot -10%), Broken Hand (blows -10%; both in `Rules.CaptainType`), Lost Eye (parry window 0.05 s
  shorter; `BattleOptions.Wounds`). The battle report says so; the captain card (K) shows them as red chips with a
  tooltip. A healing spring's rest mends one (event verb `mend`, `MendWound`); scars stay. `SelfTest.LastingWounds`.
  Simplification: the surgeon lowers the odds rather than healing a wound.
- P4 (step 2, earned traits and age): `Campaign/Traits.cs`. Each week `TraitsWeekly` works the captain's traits out again
  from the tally (`TraitsEarned`), so they come and go: Lionheart (2 duels won or 8 battles with 5+ of your own kills,
  tally `frontline`), Steadfast (4 battles won at 1.5x odds or worse, `outnumbered`), Merciful (3 lords or prisoner bands
  freed, `freed`) or Ruthless (3 caravans robbed), whichever leads by two, and Coward (4 retreats from a weaker foe, `fled`, and more than
  half as many as victories). They sit on the captain's `Character` and colour lords' opinion (`RegardOfTraits`: e.g.
  Lionheart +5 to all and +5 more from the Reckless, Ruthless -15 from the Honourable, Coward -10 to all), and each is an
  event trigger `trait:Name` (five events in `Data/events.json`). Age: a year is 120 days (`DaysPerYear`); past 45
  (`OldAge`) each new season costs a point of Arms and Leadership and Tactics rise 25% faster (`AgeEdge` in `Practise`).
  `SelfTest.CaptainTraits`. A campaign rarely runs the 15 years to 45; a new captain's age (30) is unchanged.
- P5 (step 1, rank rewards): `Campaign/Household.cs`. From Vassal the captain has a **household guard** (`GuardWeekly`,
  by `CaptainState.BestRank`, so it stays once earned): 15 men of tier III of your culture, a Lord's 25 of tier IV, a
  Marshal's ride (tier IV horse), a King's 40 tier V horse (`GuardFor`, `GuardTroop`). It is a company with the trait
  `Household`: one extra company slot and its men added to the men limit (`Rules.CompanySlots`, `MenLimit`), it never
  deserts (`Desert`), refills 3 men a week, takes the new troop when your rank rises, and in battle starts on Follow
  (`AddSquad`). Sworn Sword gives one free perk choice (`CaptainState.FreePerks`; the perk goes in `BonusPerks` so it does
  not eat a branch's next mark, and forgetting it gives the choice back). A Lord's banner adds 4 m to the captain's
  aura (`LordBanner`). A King's first rally of each battle is the King's Charge (`BattleOptions.KingsCharge`): every man
  of yours, anywhere, to at least rally morale +10, broken companies given another chance. The ladder and the rank beats
  name the rewards. `SelfTest.HouseholdGuard`, `SelfTest.RankRewards`.
  Not done: the culture's gear at Sworn Sword (gear has no culture), a crown look, a free second sergeant at Marshal.
- P5 (step 2, the road you came by): a new campaign from the title opens with the event `start_road` (trigger `start`,
  forced by `OfferRoad`; `ScreenHost.StartCampaign` calls it, not `Campaign.New`, so tests and tools start as before).
  Its choices call `ChooseRoad` (Armoury.cs) through the verbs `road_sword` (Arms 20, kite shield), `road_lance`
  (Riding 20, a warhorse, the boar spear as second weapon) and `road_bow` (Bowcraft 20, the hunting bow in hand, the sword
  at the side). `SelfTest.StartingRoad`.
- P5 (step 3, the free road to a crown and titles): `ClaimCrown` (Career.cs). An unsworn captain under contract with 400
  renown (`CrownRenown`) who storms a town (`AfterAssault`) is asked by the forced event `free_crown` (trigger `crown`)
  whether to hand it over or keep it; the `crown` verb gives the town and its villages to the captain and founds a realm
  on them through `FoundRealm`, the body `DeclareIndependence` now shares: King at once, at war with the old paymaster.
  Titles: past 300 renown (`TitleRenown`) the captain is called by a name (`Campaign.Title`): a trait first (the Craven,
  the Lionheart, the Cruel, the Merciful, the Steadfast), else deeds (the Champion for 3 duels, of the Lists for 3
  tournaments, Goblin-Bane for 3 holdouts, the Hammer for 20 victories), else the Bold. Shown under the name on the
  captain card and in the end summary. `SelfTest.FreeCrown`. The pilot always hands the town over (first choice).
- P6 (step 1, personal goals): `BattleDeeds.cs` (headless). With `BattleOptions.Goals` (the campaign's field battles and
  assaults) the battle draws two goals from `GoalKinds` with its own seeded generator, so the sim's random numbers do not
  move: slay their captain, fell five with your own hand, break their horse (only if they have any), no company of yours
  routs, win within three minutes (not in sieges). `GoalState` is live (open, met, lost) and the HUD lists them under the
  balance bar (`Hud.GoalLines`); `BattleResult.Goals`/`GoalsMet` hand them back, and each met pays 5 renown and 1.5 of its
  skill (`GoalRenown`, `GoalPractice`; Arms, Tactics or Leadership). The report lists them. `SelfTest.BattleGoals`.
  Not done: hold the hill (no marked high ground), take the gate first.
- Pacing check after P5/P6a (`--pacing 4`; the report now prints each run's traits, title, retreats, wounds and perks):
  the stand-in captain dodges many encounters, so counting every retreat made it a Coward (-10 with every lord) and lords
  refused its fealty by one point; `fled` now counts only retreats from a weaker foe. After the fix 2 of 4 seeds reach
  Vassal (days 81 and 193) and one reaches Marshal (day 218); the end-of-P3 build gave 2 of 3 (days 51, 91). The misses
  stall at 99-119 renown of the 120 fealty needs. Retune in P8.
- P6 (step 2, the banner and the last stand): `BattleBanner.cs` (headless). With `BattleOptions.Banner` (campaign
  battles) the first man of your household guard, else of the company nearest you, is `Bearer`. If he falls the banner
  is down where he fell: every man of yours loses 10 morale and your aura stops working (`BannerUp` in `UpdateMorale`)
  until you reach it (2.5 m) or, after 3 s, the nearest man of yours within 4 m raises it (+5 morale). The goal
  "keep your banner flying" (Leadership) is drawn only with a bearer. The HUD shows a gold pennant over the bearer and the
  fallen banner on the ground (`Hud.BannerMark`). With `BattleOptions.LastStand` your first fall (after Field Surgeon,
  which still gets you up free) puts you down for 4 s (`DownSince`, staggered at 1 health): rally (R) to fight on at a
  quarter of your health (`FoughtOn`, which still counts as a fall in the result), or be carried off when the moment
  passes; a blow while down ends it. `SelfTest.BannerAndLastStand`; `ReplayExact` now also replays a battle with all three.
  Simplifications: the bearer stays in his company's ranks rather than at your side; no "a horse is near" condition.
