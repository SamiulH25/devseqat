# Holds: a land of castles, few towns (sprint plan)

Status: **in progress**. HD1-HD5 built 2026-11-19 (the owner asked the model to decide HD4); HD6 (world size) and HD7 (docs and polish) left. Planned 2026-11-18. The owner's direction, from the review of the drawn world (WM6):

> Towns and cities were rare in medieval times; castles and holds were far, far more common.

Owner's decisions (2026-11-18):
1. Troops can be promoted **outside a town up to tier III**.
2. Castles get **a function of their own** that cuts the captain's dependence on towns.
3. **Caravans stay mainly a town thing**; castles are **held together by their villages**.
4. Agreed from the review: towns become rare (2-3 a realm), castles and holds common (4-6 a province), castles take on
   some town services, and the world data changes go in as notes for the owner to agree (docs/world/README.md).

Still open: **world size** (see HD6).

## Where things stand (2026-11-18)

| | Towns | Castles | Villages | Provinces |
|---|---|---|---|---|
| Drawn world (`Data/world.json`, rev 13) | 61 | 53 | 280 | 81 land |

- More towns than castles. The settled land is about 1,050 x 940 km. A foot company crosses it in about 8 days on roads
  and 11 off them. The median distance from a town to the nearest town is 52 km, and from a town or castle to the
  nearest other one is 35 km.
- The campaign leans on towns. Code that tests `SettlementKind.Town` appears 66 times, against 16 for `Castle`:
  - Town only: promotion (`CanTrainHere`: a town, your own fief's yard, or the Drillmaster perk), mercenaries,
    tavern, market and goods, armourer and smith, workshop, tournaments, the steward's contract board and its quests,
    and caravans (`Roads.cs`: `CaravansWanted` = towns / 2, routes town to town).
  - Castle tabs today: HALL, ESQUIRES (noble recruits), REST, FOOD, CAPTIVES, CONTRACT. Village tabs: RECRUIT, FOOD,
    FORAGE, REST, ELDERS.
- A village is tied to a castle or town only through `SeatOf`: the nearest seat in its province gets its tax, and the
  seat's buildings reach it. Nothing else flows between them.
- Lords scale with a realm's towns and castles (docs/archive/WORLDMAP.md WM1). Tripling the holds would triple the lords unless
  that is rescaled (HD4).

## Target

- **Towns:** 2-3 a realm, about 15-20 in all: each capital and one or two great ports or crossroads. A town is a
  destination. It has trade, hiring, the tavern and tournaments, and caravans run between towns.
- **Castles and holds:** 4-6 a province of a realm, about 100-140 in all, in three sizes (HD7: tower house, castle,
  great castle).
- **Villages:** each bound to one castle (or town), 2-4 apiece. A castle lives off its villages, and they look to it.
- **The captain:** can drill raw men anywhere and keep a company going from castles for weeks. They still need a
  town to trade, to hire mercenaries, to buy fine gear, and to train veterans past tier III (outside a fief's yard).

## Phases

### HD1: Promotion in camp up to tier III (built 2026-11-19)
As built: `Campaign.CanTrainHere(p, to)` (Companies.cs) allows a target of tier `Balance.CampTier` (3) or below anywhere;
the reason is `TrainPlaceNeed` ("NEEDS A TOWN (TIER IV+)"). `NextStep` says PROMOTE THEM (P) when a ready stack can rise
where it stands. The pilot promotes every think and goes to a town only for promotions a town alone allows. Test `CampPromotion`.
`--pacing 8` against the same tree without HD1: average VASSAL day 51 -> 46, LORD 102 -> 98, MARSHAL 172 -> 160, first tier V
day 105 -> 69. The base was already early on several runs; no retune yet (revisit after HD4).
- `Campaign.CanTrainHere(p)` takes the target troop. A promotion **to tier I-III** is allowed anywhere. A promotion to
  tier IV or more still needs a town, a castle with a yard (HD2), your own fief's yard, or Drillmaster.
- Drillmaster (Skills.cs) keeps its worth: "promote men of any tier in camp".
- The reason text becomes `NEEDS A TOWN (TIER IV+)`. The upgrade screen (`MapScreen.Upgrade.cs`) and the party board
  promote from camp, and the TRAIN tab counts only what the place allows.
- Update the opening log line ("Levies upgrade after a few battles: visit a town's training yard") and the F1 help.
- Test: on a fresh campaign in the open, a levy stack with XP promotes to tier II. A tier III stack cannot reach IV
  until the party stands in a town.
- Balance: run `--pacing 8`. Career stages may come sooner, since raw men no longer wait for a town.

### HD2: The castle's own function, the MUSTER (built 2026-11-19)
As built: `Campaign/Muster.cs` (`Demesne`, `MusterRecruits`, `MusterPrice`, `CannotMuster`, `Muster`, `CastleYard`,
`GranaryDays`, `GranaryPrice`, `BuyFromGranary`, `CastleStablesWeekly`); tab `MapScreen.Castle.cs` `MusterTab`. Castle tabs:
MUSTER, TRAIN, HALL, ESQUIRES, REST, CAPTIVES, CONTRACT (FOOD went into the granary; a castle at war with you shows only HALL,
CAPTIVES, CONTRACT). Deviations: the muster recruits each village's own men at its own culture and price (+ `MusterMarkup`),
not all at the castle's culture. The granary is a cheaper store (`GranaryPrice` 0.8 of the market) that runs dry
(`GranaryReserve` days kept for the garrison, one store day feeds `GranaryMouths` 60 men); plain `BuyFood` is unchanged. Castles
start with half `CastleHorsesMax` (15) horses and breed `CastleHorsesWeek` (4) a week. The great castle's smith waits for hold
sizes (HD4/HD5). Test `CastleMuster`; shot `castle-muster.png`.
A castle is where its lord gathers men, horses and food from his villages. The MUSTER tab replaces town trips for
everything but trade:
- **Muster:** recruit from the castle's whole demesne in one place. Its bound villages' recruits pooled (HD3), at the
  castle's culture, without riding village to village. The price is a little above the village's own, the castellan's
  cut.
- **Stables:** buy remounts and draught horses (`Settlement.Horses` exists; castles breed more than villages).
- **Castle yard:** promotion to tier IV and V at any castle of a realm you serve or are at peace with (tier VI stays
  with towns and your own yards). This is the main lever against town dependence.
- **Granary:** buy food from the castle's stores (`Supplies`), filled by its villages (HD3).
- A **smith** for mending (`Smith.Mend`, C11) at great castles only; buying gear stays in towns.
- Data: the prices and the yard's tier cap go in `Balance` and `Data/campaign.json` (`CastleYardTier` = 5,
  `MusterMarkup`).
- UI: a MUSTER tab on the settlement screen (`MapScreen.Settlement.cs`, `TownTabs` for `Castle`), drawn from the
  existing `Card`, `Stat` and `JobCards` pieces, with a picture tile in `PlaceRail`.
- Tests: `CastleMuster` covers the pooled recruits, the yard's tier cap, and the stables and granary prices.

### HD3: Villages hold castles together (built 2026-11-19)
As built: `Campaign/Holders.cs` (`BindVillages`, `HeldShare`, `VillageFood`, `CastellansDaily`); `Settlement.Holder` (saved;
bound in the `Campaign` constructor, so old saves get one too), read by `SeatOf`. Holder = the nearest seat in the village's
province; 52 of the drawn map's 280 villages lie in provinces with no seat (before, they had no seat at all) and go to their
realm's nearest seat until HD4 gives every province one. A castle's weekly stores are 2 + `VillageFood` (2.5 a whole village
at prosperity 50, scaled by it) instead of a flat 7; its garrison refill and foals scale with `HeldShare`; whole villages take
0.1 a day off its unrest ("VILLAGES HELD"). The castellan rides out once a day at the nearest brigand band or beast pack near
his villages (`SallyReach` 14 km) that his garrison outweighs `SallyOdds` (1.5) to one (not trolls, wyverns, the dead, lairs or
named brigand kings). `CaravansWanted` = max(2, towns / 2, (towns + sea lanes) / 3): unchanged on today's map, 14 with 18
towns. Not built: the carts between a castle and its villages (drawing only; with HD5). Tests `VillagesFeedCastle`,
`CaravansTownToTown`.
- Each village gets one **holder**: a castle or town, chosen once when the map is built, as the nearest seat in its
  province, reachable by road, and kept in the save (`Settlement.Holder`; `SeatOf` reads it).
- **What villages give their castle:**
  - recruits to its muster pool (each village's `Recruits` refill as now, and the muster draws from all of them);
  - food to its granary (a castle's `Supplies` rise by its villages' prosperity, weekly);
  - horses to its stables.
- **What the castle gives back:**
  - its garrison guards the villages: raiders near a held village meet the castle's men sooner (`CampaignAI`: the
    castellan sallies against bands within a reach);
  - a castle that holds its villages keeps their unrest down.
- **Raiding a village hurts its castle.** Stripped or burnt villages cut the castle's muster, granary and garrison
  refill, so a siege can begin by raiding the villages round it. That is the medieval way, and it gives the AI and the
  player a reason to raid.
- Drawn on the map (drawing only): carts and folk on the lanes between a castle and its villages. These are not
  caravans: they cannot be met or robbed.
- **Caravans stay with towns:** they still run town to town. `CaravansWanted` is set from the sea lanes and the
  number of road links between towns, not the raw town count, so fewer towns do not empty the roads.
- Tests: `VillagesFeedCastle` (a castle's supplies and muster follow its villages; raiding one cuts them) and
  `CaravansTownToTown`.

### HD4: The world's settlements (data, by notes)
Built 2026-11-19. The owner asked the model to decide; the seven notes ("Holds: <realm>", at each capital) record the
proposal and are marked done. `Data/world.json` now has 19 towns (3 a realm, the Order 1), 123 castles (42 former towns, 28
new holds named in each realm's style) and every province a seat; `docs/world/holds-plan.json` is the plan it came from. 17 new
holds were then moved to stand at least 18 units from any village (the drawn castles stand a median 28 from theirs).
As built in code: lords were already shared out by each realm's share of seats (`LordsWantedOf`), so their number is unchanged.
A castle's income, garrison and walls scale with its size (`CampaignSiege.SizeFactor`: tower house 0.6, castle 1, great castle
1.4; a great castle keeps spears as a town does); realm income comes out about the same as before (13,500 against 13,900 base a
week). Towns on the drawn map offer 2-3 bands for hire instead of 1-2. `TryBesiege` is unchanged (a lord besieges only the first seat
listed in the province): letting him pick any enemy seat, towns first, snowballed (`--realms 8`: largest share 36-38%, the
Order of the Ashen Sun gone in 2-5 runs of 8, against 30% and none before HD4; unchanged it is 32% and none). A caravan sets out only from a town with a market at peace in reach. Contract
boards, tournaments and goods were left as they are (castles already have CONTRACT; tournaments and trade stay in towns). An old
save keeps its own settlements: the map draws only those (`Campaign` constructor). `DesignedMap` checks the ratio, a seat in
every province, every village bound in its own province, every realm a town, three castle sizes.
- Put the change to `Data/world.json` forward as notes, one per realm, pinned at its capital. Each lists the towns
  that stay, the towns that become castles, and where new holds go (frontiers, passes, river crossings, coasts).
  Nothing is edited until the owner agrees in the thread (docs/world/README.md, rule 2).
- Once agreed:
  - convert the towns;
  - add holds, with names in each subculture's style from `Data/names.json`;
  - bind villages (HD3), aiming for 2-4 villages a castle and a few more for a town;
  - keep every province with at least one seat;
  - give every realm a capital town.
- **Lords:** rescale "lords scale with towns and castles" so the count stays about the same. Not every hold needs a
  lord: a hold without one has a castellan (a garrison and a name) and belongs to the nearest lord's fief.
- **Systems that assumed many towns** (check each, and change it where the game would feel empty):
  - contract boards and quests: castles already offer CONTRACT, so give castles some of `MoreTownOffers`;
  - mercenary offers (`RefreshMercenaries`): fewer towns means fewer bands, so allow more per town;
  - tournaments: towns only, and rarer is fine;
  - goods and markets: towns only, which raises the value of trade runs;
  - the realm AI's targets (siege priorities, `ArmiesDaily`): towns are worth more, and castles are many small steps;
  - income: towns pay more and castles less, through `FiefFactor`;
  - the career win at 60% of provinces is unchanged.
- Balance runs: `--realms 8` (no realm snowballs), `--pacing 8`, `--balance 4`, `--campaign-bench 30` (more seats, same
  ms a day).
- Tests: `DesignedMap` gains the ratio (towns ≤ 25, castles ≥ 90, each village bound, each realm a capital).

### HD5: Seen on the map (built 2026-11-19)
As built: `"rank"` on castles in `Data/world.json` (1 tower house, 2 castle, 3 great castle; the page edits it as Size, and
`tools/world.py --check` refuses a bad one), `Settlement.Rank`/`Size`/`KindWord` (the UI's TOWN, VILLAGE, TOWER HOUSE, CASTLE,
GREAT CASTLE). Great castles are former towns that were ports or road hubs (17), tower houses the new holds (28), the rest
castles (78). Models `World.TowerHouse` (keep, corner turret, walled yard with a byre) and `World.GreatCastle` (curtain wall with
drum towers and a gatehouse round an inner ward and a great keep); the size rides in the castle foliage's `Dry` field (its
`Radius` would make it an obstacle). Labels (`MapScreen.Flags`): towns in large type to zoom 450, castles small, tower houses
gone past zoom 300, villages past 150 (the default view is 170). Shot `campaign-holds.png` frames the nearest tower house.
Not built: the carts between a castle and its villages (the roads' own ox carts already walk those lanes). Checked by eye, no
judge (the owner's rule from 2026-11-19).
- Three sizes of hold: a **tower house** (a keep and a wall), a **castle** (today's ring), and a **great castle** (a
  double ring and a big keep), from the hold's rank in the data.
- A label hierarchy: towns in large type, castles small, villages only close in.
- The minimap and the realm map (M) mark towns and castles apart, as they do now, with more castles.
- Render the campaign shots and judge them (7/10 gate). Check that labels and models do not crowd where holds cluster.

### HD6: World size (open: owner to decide)
At about 8 days across on foot, the 1,000 km land plays small. With roughly twice as many seats, there are three
options:
- **a)** Keep the size: the land feels busier, and travel time stays as now.
- **b)** Slow travel (`FootSpeed`, `HorseSpeed`): the same map feels bigger, with no data changes. It touches food,
  pacing and every balance number.
- **c)** Scale the drawn world up (`DesignScale`): more room between holds, and a world.json-wide change.

Recommendation: decide after HD4, from `--pacing` and a look at the density on the map.

### HD7: Docs and polish
- README (the player's rules: where you can promote, what castles give, villages and castles), docs/CAMPAIGN.md,
  docs/archive/WORLDMAP.md, CLAUDE.md file guide (the muster, holders), tips (`Tips.cs`), and the landing page's numbers.

## Order and size

HD1 is small and stands alone. HD2 and HD3 come before HD4, so that when the towns go, castles already do their new
work. HD4 waits on the owner's answers in the notes. HD5 follows the data. HD6 is a decision, not a build.
