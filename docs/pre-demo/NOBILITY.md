# NOBILITY: common and noble troop trees

Status: N1 and N2 built (2026-11-15). N3-N5 planned.

**Rule:** you cannot drill militia into paladins. Troops are `stock` "common" (default) or "noble" (`Data/troops.json`).
XP promotion never crosses from common to noble. Nobles come from castles (hire) or by knighting one named man at a time.

## Decisions (user, 2026-11-15)
- Nobles come **only** from castle recruitment plus knighting. No mass cross-over.
- Commoner ceiling is T5 for melee infantry and horse. T6-T7 of those lines are noble. Archers, pole troops, mages and mounted bowmen stay common to T7 (they are professionals, not knights).
- Order troops (lily, rose, wolf, falcon, sky, lantern, hollow, sworn) stay on their order gate (`TrainsOrder`, rank); the order rank is their knighting.

## Design (revised from the first plan)
Edges in `upgradesTo` are **kept**, not cut. `Factions.Validate` needs each realm's seven-troop line to be an unbroken upgrade chain, and `Armies.LineTroop` builds AI armies from those lines. So the gate is a rule, not data surgery: `Campaign.CanEnterLine` refuses a common to noble upgrade ("NOBLE BIRTH ONLY"). Promote, `PromoteAll` (the AI) and the party screen all go through it. About 50 edges now cross common to noble (e.g. levies to crown_squires, men_at_arms to foot_knights, lancers to knights, blademasters to grand_blademasters); all are gated.

## Phases
- **N1 (built):** `stock` field, `Troops.Validate` rule, `CanEnterLine` gate (the player's men only; gotcha: gating AI lords too dropped their average tier below IV and starved court-mage circles, test `MagesInTheWorld`, so lords stay ungated until N4), test `NobleBirth`.
- **N2 (built):** castle noble recruits. Two new castle-root troops, `esquires` (T3 infantry) and `mounted_esquires` (T3 horse), culture common, stock noble, trained into the noble T4s (foot_knights, knights, demi_lancers and the orders' squires, which stay behind their order gate). Castles only (`Settlement.Nobles`, refilled every `NobleRefillDays` with no dice, cap `NobleRecruitsMax` x ArmyScale). `Campaign.RecruitNobles` in `Campaign/Nobility.cs`; you must serve that castle's realm or hold the castle, with renown `NobleRenown`; price `NobleMarkup` x the troop's cost. UI: the ESQUIRES tab on a castle (`MapScreen.Settlement.cs` `NoblesTab`, shot `campaign-esquires`). Sellswords step down to the best common troop of their line (`RefreshMercenaries`) and noble prisoners cannot be recruited (`RecruitPrisoners`). Test `NobleRecruits`.
- **N3:** knighting. An action on a named veteran of the roll (`Campaign/Roll.cs` style, like `RiseToSergeant`): deeds, renown, gold; one man becomes his noble upgrade. Bypasses the stock gate for that man only.
- **N4:** AI and UI. Lords' retinues noble, levy hosts common (`Armies.LineTroop`, `BranchRank`); party screen badge and "NOBLE BIRTH ONLY" text (`MapScreen.Party.cs`).
- **N5:** balance. Run `--pacing`, `--realms`, `--agree`, `--balance`; retune castle noble pool and prices.

## N2 decisions and gaps
- **T6 and some T4 nobles are reached by knighting, not by castle drill.** Nobles with a commoner parent and no noble one (e.g. grand_blademasters' siblings dread_cleavers, thane_champions, khan-line nobles, doge_guard, citadel_sentinels) keep that commoner edge: it is the knighting edge N3 will use. `NobleRecruits` proves every non-order noble is reachable from the castle roots or by knighting.
- **Gap cultures:** northern, southern, steppe and league have no non-order noble T4, so esquires of those cultures stay at T3 until they join an order or an azure/crimson kin line allows it. Their nobles come from knighting and orders. Adding a noble T4 entry per culture is open (needs new troops and sprites).
- **Mounted bowmen are common.** N1's marking caught mounted archers at T6-T7 (khan_guard, royal_dragoons, golden_horde_masters, sovereign_dragoons, kheshig, khagans_own, the turcopoles); N2 unmarked them, as decided. `crown_squires` is common too: it is the cheap horse root (levies to crown_squires to lancers); the gate now bites at lancers to knights.
- **Scripted grants still give nobles, left alone on purpose (list for the user):** the household guard (`Campaign/Household.cs` `GuardTroop` picks any troop of the tier, e.g. foot_knights at Lord rank), order brothers (`Campaign/Oath.cs`, behind the order rank), and origin or event `men:troop` grants (all common troops today).

## Noble troops (stock "noble")
- **azure**: foot_knights T4, paladins T5, citadel_sentinels T6, iron_legionaries T6, royal_crusaders T6, immortals T7, bastion_lords T7, sovereign_champions T7, marshal_guard T6, imperial_marshals T7, demi_lancers T4, banner_knights T5, paladin_knights T6, grail_knights T7, lily_squires T4, lily_knights T5, lily_wardens T6, crown_squires T2
- **crimson**: sword_brethren T5, grand_blademasters T6, dread_cleavers T6, sword_saints T7, archons T7, knights T4, cataphracts T5, iron_cataphracts T6, imperial_cataphracts T7, rose_blades T4, rose_swordmasters T5, thorn_knights T6
- **northern**: thane_champions T6, valhalla_reavers T6, iron_thanes T6, high_king_chosen T7, blood_jarls T7, allfather_guard T7, storm_riders T6, khan_guard T6, royal_dragoons T6, thunder_lords T7, golden_horde_masters T7, sovereign_dragoons T7, wolf_thegns T4, wolfsworn T5, winter_wolves T6
- **southern**: emir_mamluks T6, sultans_guard T7, emir_guard T6, lions_of_the_sun T7, falcon_riders T4, falcon_knights T5, sun_hawks T6, sultans_camels T6, sword_dancers T6, saints_of_the_sand T7
- **steppe**: kheshig T6, khagans_own T7, noyan_guard T6, khagans_axes T7, sky_riders T4, sky_archers T5, sky_sworn T6, khans_lancers T6, ironclad_khans T7
- **order**: sun_brothers T4, ashen_brothers T5, sun_templars T6, ashen_saints T7, knight_brothers T4, sun_knights T5, ashen_knights T6, grand_masters T7, hammer_brothers T4, iron_hammers T5, hammers_of_the_sun T6, ashen_hammers T7, veteran_turcopoles T4, chapter_turcopoles T5
- **league**: doge_guard T6, lion_guard T7, doge_lances T6, lion_knights T7, lantern_knights T6, admirals_guard T6
- **common**: hollow_squires T4, hollow_knights T5, hollow_lords T6, sworn_brothers T5, sworn_masters T6

Rule used to mark them: Infantry or Cavalry of tier 6 or more, the knightly troops (knights, banner knights, cataphracts, foot knights, paladins, sword brethren, squires and the order lines), and the `order` culture's tier 4 or more.
