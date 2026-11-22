# Sieges: the overhaul (plan)

The owner's brief (2026-11-21): a complete overhaul of how every siege works, so a siege feels like a Total War siege and not an
"attack the wall simulator". This replaces how assaults are fought and how sieges run on the map. Engines, works and breaches
(docs/archive/SIEGECRAFT.md) and the people's building styles (docs/archive/SETTLEMENTS.md SM6) are kept and built on.

Phase prefix: **SE**. Core overhaul built (2026-11-21): SE1–SE8 built (fortress fields from `PlacePlan`, unified fortress flow-field pathing & horse in sieges, capture points & 60s heart hold & time limit, tower fire & gate oil & layered fallback, dynamic ladders, campaign encirclement/saps/ditch/sallies/terms/post-storm occupy-ransom-sack, auto-resolve `WallFactor`, fortress rendering & HUD). See "As built" at the end.

## Where things stand (2026-11-21)

**The battle** (`BattleSiege.cs`, `SiegeWall` in Terrain.cs):
- The wall is one straight line across the field at y = 11.5. There is one gate, 2 to 4 fixed ladders, and 20 sections that stones
  can breach. Behind the wall is open ground with a few houses for looks.
- Everyone fights on foot (horses are taken away), and the attacker never chooses where to put the ladders.
- The defence is fixed:
  - archers stand in one rank on the walkway and foot at the gate and the ladder tops
  - foot charge whoever gets near their post, and horse wait behind the gate
  - nothing falls back, and there is nothing further in to fall back to
- The only way to win is to kill or rout the other side, so every assault is the same fight at the same wall.

**The map** (`Campaign/CampaignSiege.cs`, `Campaign/SiegeWorks.cs`):
- You sit down, wait for ladders (12 h, 6 h with Siegecraft), optionally build works, then assault or wait for the garrison to
  starve.
- Castles sally against weak passing bands (`CastellansDaily`), but there is no sally against a siege camp.
- Lords ride to a holder's relief (LD2), but that is not a battle against the camp.
- There are no terms. The place falls, or the siege is lifted.

**What already exists to build on:**
- `TownPlans.LayTown` already lays each town's wall `Ring`, `Gates`, `Square` and `Lots` by people.
- `PlaceWorks.LayCastle` does the same for castles, and `Settlement.Rank`/`Size` tell a tower house from a great castle (HOLDS).
- `World.SiegeField.cs` draws walls, towers, gatehouses, houses, keeps and landmarks in each people's style.

## The target

A siege is a campaign that ends in one hard battle, with choices all along the way:

- On the map, you choose between starving the place, battering it, or storming early.
- The defender can sally, call for relief, or offer terms.
- The assault is fought over the real place: its wall ring with towers and gates, the streets behind it, and a heart (the square,
  the keep) that must be taken and held.
- The defenders give ground in layers: from the wall, to the streets, to the heart. Each layer is a different fight.
- The battle is won by holding the heart, not only by killing everyone.
- Every kind of place fights differently: a tower house, a castle with a keep, a concentric great castle, a walled town, a
  palisaded village, a port.

## SE1. The siege field is the place

- The assault field is built from the place's own plan, scaled to battle metres. A town takes `LayTown`'s ring, gates, square and
  lots; a castle takes `LayCastle`; a village its palisade if it has one.
- **Walls** become a closed ring of segments (`SiegeWall` becomes `Fortress`), each segment with its own hp. Things placed on them:
  - **towers** at the corners and along long runs
  - a **gatehouse** at each of the plan's gates, two or three for a town
  - for a great castle, an inner ring and the keep
- **Behind the walls** are streets and lots: the houses stand where the plan puts them, as solid blocks men cannot cross, so the
  streets are the ways through. The square or the keep is the heart.
- **Outside the walls:** the siege camp's ground (SM5 `LayCamp`) on the attacker's side, the approaches, and the ditch or moat
  where the walls level gives one.
- The look reuses SM6 (`FieldWall`, `FieldTower`, `FieldHouse`, `FieldKeep`, `FieldLandmark`), placed by the plan instead of on a
  straight line.
- **Size:** the field must fit the battle's man count and frame budget. A town takes about 300 x 300 m and a castle less.
  Measure `--bench` with a ring and streets before going further.

## SE2. Moving through a fortress

- **Pathing:** flow fields over the fortress (`BattlePath.cs`). The walls, houses and closed gates are solid; ladders, docked
  towers, breaches and open gates are the ways in.
- **Ladders anywhere:** ladders are carried by attacking companies and raised against any segment, not at fixed x values.
  - Raising one takes a few seconds, and men climb one at a time.
  - Defenders can push a ladder off before the first man is up.
- **Walkways:** men on the wall move along it in file, one or two abreast, at the wall's height. Towers are the doors between
  walkway runs and the stairs down into the town, so taking a tower cuts the wall in two.
- **Gates:** a gate opens to a battering ram, to stones, or from inside when the attackers take its gatehouse.
- **Horse in sieges:** horse come back. Attacking horse wait outside for an open gate or breach, then ride the streets, where they
  are cramped and weak against braced foot. Defending horse sally from a gate or charge in the square.
- The two straight-wall code paths (`Via`, `MayCross`, `EnforceWall`) become one fortress graph that both pathing and the wall
  rule read.

## SE3. Holding ground: points and victory

- **Capture points:**
  - every **gatehouse**: holding it opens or shuts its gate
  - every **tower**: holding it turns its shooters on the other side
  - the **heart**: the square, or the keep's door
  
  A point is taken by standing on it with more men than the other side for a time; it is drawn as a ring with a banner that
  changes colour.
- **How it is won:**
  - the attacker holds the heart for 60 s, or kills or routs every defender
  - the defender breaks the attack or holds out until the time runs out
  - the time is set by the place's size and supplies, from 20 to 35 minutes, and a starving garrison gets less
- **Morale by ground:** losing a gate shakes the wall's men, losing the wall shakes the town, and taking the heart breaks what is
  left. The garrison's supplies and the walls level set the defence's nerve at the start.
- The HUD shows the points as shields along the top (gold for yours, foe colours for theirs), the time left, and the state of each
  gate.

## SE4. The defence

- **Towers shoot:** each tower held by the defence looses arrows, or bolts from a springald at a higher walls level, at
  attackers in reach. A company that takes a tower makes it shoot for the attackers.
- **At the gate:** burning oil and dropped stones over the gatehouse. A ram under the gate takes losses, and its roof of hides
  matters.
- **On the walls:** stones dropped on the men below, ladders pushed off, and shield men holding the top of each ladder.
- **Fallback in layers:** the defence's AI holds the wall, then falls back to barricades at the street mouths, then to the
  heart. A company told to hold the wall gives it up when it is outnumbered there and no reserve is coming.
- **Reserves:** the defending general keeps a reserve by the square and sends it to the point under the most pressure, as the
  sergeant system's call-ups do (`BattleCommand.cs`).
- **Sally from a gate:** with an attack in trouble, defending horse sally out of a gate behind the attackers to burn their
  engines, then come back in.
- **Playing the defender:** you get all of this in a siege of your own fief. Post companies to the wall, to towers, to barricades
  and to the reserve in deployment, using the officer posts (OF4) adapted to the fortress.

## SE5. The assault

- **Siege equipment:** the camp's equipment comes to the field as things to carry or push:
  - ladders, one per ladder team, from the time sat down
  - a ram
  - siege towers
  - a sap: the wall section above a finished mine falls when the battle starts, or when you give the word
  
  In deployment you give each piece to a company, and that company carries or pushes it.
- **Plans:** the attacking AI picks a plan:
  - **feint and main assault:** ladders on one side to draw the reserve, the ram and towers on the other
  - **breach rush:** with a breach in the wall
  - **escalade everywhere:** against a thin garrison
  
  After the wall, it goes for the nearest gatehouse to let the horse in, then the heart.
- **The player's orders:** the usual orders, plus:
  - raise the ladder here, or carry the equipment to a segment
  - take a point
  - open the gate, given to a company in a gatehouse
- **Engines** keep their crews and marks (SIEGECRAFT A5). Stones can be aimed at a tower to silence it, or at a segment to
  breach it.

## SE6. The siege on the map

The siege becomes a short campaign of its own, shown on the siege panel (B) as days and a balance of supplies:

- **Encirclement:** a siege is only tight with enough men round the place, and a loose one lets supplies and messengers through.
  Lords at one siege share a camp, as `Comrades` do.
- **Attrition on both sides:**
  - the garrison eats its stores, then starves, losing men and morale
  - the camp eats its own food (`EatDaily`), and past a few weeks sickness takes men (`Balance.SiegeSickness`)
- **Works:** the existing works, plus:
  - ladders, as a count, not a timer
  - mining a sap under a segment (days, and Siegecraft halves it)
  - filling the ditch
  - mantlets, which shelter archers in the assault
- **Sallies:** the garrison can sally against the camp. That is a battle at your camp, where your works stand on the field and can
  be burned. Lords' garrisons sally when the odds suit them (`Balance.SallyOdds`), and you can sally when you hold a place.
- **Relief:** a relief army coming to the place is announced days ahead. If it arrives, the battle is fought at the camp with the
  garrison sallying at your back, three-sided as allies already are.
- **Terms:**
  - **The garrison can offer to yield:** march out with their arms, with honour (good opinion with the realm, no sack).
  - **You can offer terms:** an easier yield when they starve, harder when the walls are high and relief is close.
  - **After a storm, choose:** occupy, sack (gold and goods, unrest, bad opinion, the men like it), or ransom the town.
- **Lords** use the same rules: they encircle, mine, sally and send relief. The chronicle tells a long siege as a story.

## SE7. Every kind of place

| Place | Walls | The heart | What makes it different |
|---|---|---|---|
| Walled village | palisade, one gate, or none | the green | short and sharp: no towers, burnable gate |
| Tower house (rank 1) | one ring, small | the tower's door | a small garrison; the fight is the tower |
| Castle (rank 2) | curtain, corner towers, gatehouse | the keep | take the bailey, then the keep, a second short fight inside the first |
| Great castle (rank 3) | two rings (concentric) | the keep | the outer ward falls, then the inner wall from closer up |
| Town | long ring, two or three gates, many towers | the square | streets, barricades, many ways in, a long fight |
| Port | as the town, with a sea wall | the square | a harbour gate; ships later, not for the demo |

Each people builds them their own way (SM6). A steppe ordu on its mound has a stockade and a ditch and no stone, so it falls to
fire, not stones. A southern kasbah has high earthen walls that shrug off stones but soak.

## SE8. On paper

- `WallFactor` and `AutoResolveAssault` read the same things the battle does: the walls level, towers, breaches, the
  equipment, supplies on both sides and the garrison's nerve. They are checked against played assaults the way `--agree`
  checks field battles (a new `--sieges N` run that fights and auto-resolves the same assaults).
- Lords' assaults, sallies and reliefs go through the same numbers.

## SE9. Looks, sound and UI

- **Deployment:** an overview of the fortress with its segments, towers, gates and points; equipment chips that are dragged onto
  companies; the defence's posts (wall, towers, barricades, reserve) as zones (OF4).
- **HUD:** the points, the time, the gates, and the equipment in use.
- **Look:** ladders that are raised and pushed off, oil and stones over the gate, smoke from burning works, and banners changing
  on towers.
- **Sound:** horns when a point falls; the crowd in the streets.
- **Shots:** an `IRONFIELD_SHOTS_FROM=siege` run that saves the deployment, the wall fight, a gate falling, the streets and
  the heart for each kind of place, and the map's siege panel.

## SE10. Check and tune

- `--sieges N` balance against auto-resolve, and `--bench` at full battle size inside a town.
- `--realms` to confirm that sieges taking longer and terms do not stall the realms' wars.
- Replays stay exact (`ReplayExact` with a siege).
- Tests rewritten for the fortress:
  - the wall rules: `SectionsAndBreaches`, `RamAtGate`, `TowerDocks`
  - the new pieces: points, ladders raised and pushed, fallback, sallies, terms

## Order and size

SE1 and SE2 are the heart of it and the biggest risk: a fortress field that paths, fits the frame budget and replays exactly.
Build them first and look at the shots before anything else. SE3 to SE5 make the battle. SE6 is the map and can run beside them.
SE7 is mostly data on top of SE1 once the town and castle work. SE8 to SE10 close it.

Size: large, the biggest battle change since the combat overhaul.

## Decisions (owner, 2026-11-21)

1. **Time limit:** Yes — 20 to 35 minutes by place size and supplies; the defender wins by holding out until time runs out.
2. **Battle size:** Waves via `Reinforce` (as in field battles) so town assaults stay within the frame budget.
3. **Sack:** Yes — after a storm, choose occupy / sack / ransom; sacking gives gold & goods and boosts troop morale, at the cost of unrest and realm opinion.
4. **Ports:** Post-demo — ports fight as regular walled towns for the demo (sea wall and harbour gate wait for after the demo).

## As built (2026-11-21)

- **SE1 / SE7 (Fortress fields & kinds):** `Fortress.cs` (`FortressKind`: `WalledVillage`, `TowerHouse`, `Castle`, `GreatCastle`, `Town`; `FortressSpec.FromPlan` scales and rotates a campaign settlement's `PlacePlan` ring, gates, square, interior lots and siege camp into battle metres; `FortressSpec.Procedural` builds canonical layouts for standalone/headless runs; `GreatCastle` is concentric with two rings and gates). Drawn in `World.Fortress.cs` using each people's `Architecture` style (`FieldStyle`), banners, merlons, rubble/fallen-log breaches, gatehouses, ladders, interior houses/keep/landmark, camp tents/fires, and night wall torches. Checked visually on `place-azure_heart-town-field.png` and `place-azure_heart-castle-field.png`.
- **SE2 (Fortress pathing & horse):** `BattlePath.cs` unified 3-mode flow fields (attacker foot, defender foot, horse) with wall-segment edge blocking, open gates, breaches, docked ramps, raised ladders, solid interior building OBBs, and ditch cost; `InvalidatePath()` refreshes routes whenever `WallVersion` changes. Cavalry keep their horses in sieges, wait outside until a gate opens or wall breaches, then ride into the streets.
- **SE3 (Points, victory & HUD):** Capture points on every gatehouse (throws gate open/shut), tower (flips tower fire & banner), and the heart (60 s hold wins the assault); 20–35 min time limit scaled by fortress kind and supplies (`Battle.SiegeTimeLimit`); morale drops on lost gate/breach/heart; ground capture rings (`BattleScreen.Draw.cs` `FortressPointRings`) and siege HUD strip (`Hud.Deck.cs`).
- **SE4 / SE5 (Defence & assault):** Towers shoot arrows/springald bolts; closed defender gates pour burning oil / drop stones (rams' roofs shelter crew by 75%); defenders fall back in layers (wall -> street mouth -> heart) with a reserve dispatched to the hottest threat; dynamic `RaiseLadder` (can be pushed off by defenders before locking in).
- **SE6 / SE8 (Campaign sieges & auto-resolve):** Tight vs loose encirclement (`TightEncirclement`), camp sickness past 14 days, `MineSap` (+1 breach), `FillDitch` (clears `HasDitch`), AI garrison sallies, `OfferTerms` honourable surrender, and post-storm `StormChoice` (`Occupy` / `Ransom` / `Sack`, surfaced in `MapScreen.Report.cs` `ResultsPanel`). `WallFactor` weighs supplies, saps, and filled ditches.
- **Tests:** `SectionsAndBreaches`, `TrebuchetBreaches`, `RamAtGate`, `TowerDocks`, `WallEnginesShoot`, `SiegeCaptureAndTimeout`, `SiegeCampaignWorks`, `SiegeBattle`, `EngineBreaksGate`, `FieldsStyled`, `ReplayExact`.
