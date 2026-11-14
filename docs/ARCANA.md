# ARCANA: magic and aura that shape the campaign

Status: **in progress** (2026-11-13): AR0-AR3, AR5 and AR6 built (see "As built" at the end); AR4, AR7-AR9 planned. Builds on docs/archive/COMPANIONS_MAGIC.md (effects, mages,
spells), docs/archive/KNIGHT_ORDERS.md (aura, orders, forms) and docs/archive/MAGIC_AURA.md (the player's hand on both).
Read docs/archive/LEGACY.md first: your company is your legacy, and every change here must serve that.

## 1. Why this pass

The three earlier plans built the parts well: 22 caster troops, 27 spells, a Will pool, warnings, fields, great workings,
a spellbook, a circle master, rites and Omen; five aura tiers, walls and trials, 27 forms, orders, your own form, aspects,
the Black Flame. The trouble is not the parts but **where they sit**: off to one side of a campaign that rarely goes there.

### 1.1 What a campaign actually holds (measured)

A census of 4 pilot campaigns x 240 days (~110 fought or auto-resolved battles each), sampled monthly, before the fixes
in commit 3e46a93:

| | Seen |
|---|---|
| Lord armies with a mage company | **0 of 154**, every sample |
| Towns with a circle for hire | **0 of 63**, every week for 20 weeks |
| Your aura | stops at the first wall (30 XP) by day ~60, **tier 0 at day 240** |
| Lords with aura | none; only the 14 knight-order armies' knights |

AR0's census (`--arcana 2`, seeds 4001-4002, after the fixes): by day 120 magic in **0/26 and 1/43** battles (a seeker),
**0/48 and 1/48** (plain); lords with a circle 2-6 of ~120; a seeker reached Kindled on day **141 and 68** (one vigil each,
no duels), a plain captain **never**; the stand-in never hired a circle.

Two of those were bugs and are fixed (towns' shrine radius was not scaled by `Map.Spread`; lords got a circle only when
raised at tier 5+, which never happens). After the fixes (`MagesInTheWorld`): 70 offers in 22 weeks, at most **4 of 154**
lords with a circle. Magic now exists in the world, but it is still a rare guest.

### 1.2 What is underdesigned (from the code)

1. **Magic happens near the player, not through him.** Circles cast by their `when` rules. The P-key cast order exists
   but nothing asks you to plan around spells: there is no shared resource, no rival casting, no reason to pick a moment.
2. **One circle, one fixed line.** A culture has one caster line; its spells come by tier (2-5, every line has Mend).
   The spellbook adds two spells of the same tradition. Choosing a circle is choosing a culture, not a way of war.
3. **The world has no arcane geography.** Shrines, stones, hermits and springs are visits with an event. They do not
   change where or how magic is strong, so magic never pulls the campaign anywhere.
4. **The enemy barely uses it.** No court mages, no realm that leans on sorcery, no circle that answers yours. Your Ward,
   your blade breaking casts and your horse hunting mages (all built) have almost nothing to answer.
5. **Aura is a hidden track behind walls.** Tier 0 does nothing. The first wall needs a won duel or a vigil, and the game
   does not lead you to either. A player who never duels never sees aura, and so never sees 27 forms.
6. **Generic aura is thin.** 3 generic forms; the rest belong to orders. An unsworn captain gets Focus, Aura Slash,
   Aura Burst and (at Expert, after 40 style points) his own form. Breakthroughs give numbers, not choices.
7. **Aura and magic touch only at the edges** (`BattleAuraMagic.cs`: a blessing feeds +10, curses on you, the blade
   breaks a cast, craft marks, the relic). There is no build where the captain and his circle are one instrument.
8. **Magic on the map is seven rites.** Bless, poultices, mend the core, read the bones, call rain, craft marks, cleanse.
   No rite touches another lord, a province, a siege or a war.

## 2. Pillars (unchanged rules first)

1. **Battles stay a fight of men.** Spells warn and hit part of a company; the captain alone still loses to two tier III
   companies (`AuraBound`). New power comes with a visible cost and an answer, not bigger numbers.
2. **The player chooses, the sim decides.** New control is an order (with the order delay) or a choice before battle or
   on the map. Forms stay the only keys pressed for power.
3. **Deterministic.** Battle randomness from the battle's `rng`; campaign magic on its own stream (`ArcRng`, like
   `KinRng`) so existing campaigns' draws do not move. `ReplayExact` holds.
4. **Auto-resolve agrees.** `--agree` stays inside its bands after each phase; magic and aura are weighed in `Rules.Might`.
5. **Old saves play.** Every new save field has a default that plays as today. Map changes go behind a new layout number.
6. **Measured.** Every phase states a target in the census (AR0) and ends with it met.

## 3. The shape of the redesign

Three ideas carry it:

- **Wells (the arcane geography).** Places of power on the map, each with a tradition and a strength. They decide where
  mages come from, how strong magic is in a battle fought near them, and what the map's rites can do. Magic becomes a
  reason to go somewhere and to hold something.
- **The Tide (one contested pool per battle).** Each battle has an arcane Tide set by the nearest well, the weather and
  the hour. Every caster on both sides draws on it as well as on his company's Will. Casting is now a contest for a shared
  resource: an enemy circle that casts first leaves less for yours, and pushing past the Tide is possible at a price
  (overreach). This is what makes the cast order matter.
- **Paths (the captain's arcane identity).** Aura grows along a chosen path from the first wall: the **Sword** (forms,
  the duelist; orders sit here), the **Banner** (presence: aura spent on your men), or the **Hearth** (resonance: aura
  that feeds and steers your circle). One path deep, a second shallow. This is where aura and magic become one build.

## 4. Phases

### AR0. Measure (tooling first)

- `--arcana N` in `BalanceReport.cs` (`Arcana`): N pilot campaigns x 240 days; per month: lords with a circle, circle
  offers, battles with magic on either side, enemy casts per battle, your aura tier and path, forms known, Omen,
  wells held. Writes `balance/arcana.csv`.
- `Pilot` learns to seek trials (duel a lord who accepts, a vigil when at a wall and near a site), hire a circle when one
  is offered and affordable, and work a rite when it has a teacher. Without this the census only measures neglect.
- Baseline written into section 1.1. Targets (to be met by AR9):
  - by day 120, magic (either side) in **1 battle in 6**; against great lords, **1 in 3**;
  - a player who seeks it reaches Kindled by day **~30**, Aura Knight by **~90**, Aura Expert by **~180**;
  - a player who never seeks it still reaches Kindled by day **~90** (AR1's awakening).
- Test: `ArcanaCensus` (short: 1 campaign x 60 days, numbers printed, sanity checks only).

### AR1. Aura you can find

- **The awakening.** Tier 0 is not nothing: aura XP fills a visible ember on the AURA row. The first wall breaks by any
  of: a won duel (as now), a vigil (as now), **a battle won against the odds where you fought at the front** (kills and
  ratio, read in `AuraAfterBattle`), or **a tournament won** (`FinishTournament`). So every captain who fights awakens.
- **The wall as a quest.** At a wall the K sheet shows a TRIAL card: the trials that break this wall, and for each the
  nearest way (the nearest shrine or stones for a vigil, a lord who would accept a duel, the next tournament). A beat
  when you first reach a wall, a tip, a chronicle line when it breaks.
- **Breakthroughs are choices.** Each new tier offers two gifts (one per path at Kindled: AR6 opens paths); the numbers
  that today come for free become the floor.
- Files: `Campaign/Aura.cs` (`TrialsFor`, `AuraAfterBattle`, new `NearestTrials`), `Campaign/Town.cs`,
  `MapScreen.Captain.cs` (TRIAL card), `Hud.cs` (ember), `Tips.cs`.
- Tests: `AwakeningByDeeds` (a won battle at odds with front kills breaks the first wall; a lost or rear-line one does
  not), `TrialCardShowsWays`.

### AR2. Wells: the arcane map

- **Wells** are sites with a tradition (faith: shrine, spring, hermit; craft: stones, ruin, old battlefield) and a
  strength 1-3. Existing sites become wells (no new map generation: placing is already done, so old maps keep theirs).
  A province's **Tide** is the strength of its strongest well, plus its neighbours' at half. Shown on the realm map (M)
  as a layer, and in the encounter panel ("A strong craft well: storms come easy here").
- **Holding a well:** a realm holds the wells in its provinces. The player can **consecrate** a well (faith, costs crowns,
  a priest or faith circle) or **bind** it (craft, costs Omen) when it is in a province you hold or your liege holds.
  A bound or consecrated well strengthens its tradition there and weakens the other.
- **Wells feed recruitment:** a town's circle offer reads its province's Tide (replacing the shrine radius rule), so
  mages come from where magic is strong; a culture's mages come from its own wells, foreign ones from foreign wells.
- Save: `Site.Tradition`, `Site.Strength`, `Site.Bound` (-1 none, else the binder's faction), defaults derived from kind.
- Files: `Campaign/Sites.cs`, new `Campaign/Wells.cs` (`TideAt`, `Consecrate`, `Bind`, `WellsDaily`), `WorldMap.cs`
  (read only), `MapScreen.Atlas.cs`, `MapScreen.Dialogs.cs`, `CampaignAI.RefreshMercenaries`.
- Tests: `WellsFromSites` (every shrine/stones is a well; a seed's wells never change), `TideFeedsOffers`.

### AR3. The Tide in battle

- `BattleOptions.Tide` (0-3, and its tradition) from the campaign; quick battles from the ground preset. Each battle
  holds a **Tide pool** (`Battle.Tide`: 60 x (1 + Tide), refilled slowly; faster at night for craft, by day for faith).
- A cast costs its Will **and** draws its Will cost from the Tide. With the Tide empty a circle may still cast by
  **overreaching**: double Will, and a backlash roll (the battle's `rng`): casters wounded, a `shaken` effect on the
  circle, Omen +1 for craft. Shown on the spell fan ("OVERREACH: 2x WILL, RISK").
- The AI general reads the Tide too: an enemy circle with the Tide high casts freely; with it low it saves for its great
  working. So **casting first, or holding**, is a decision, and the P-key order and HOLD (built in MA1) finally matter.
- A HUD band shows the Tide (one bar, both sides' draws in their colours), only when either side has casters.
- Headless rules in a new `BattleTide.cs` (`TideTick`, `DrawTide`, `Overreach`); hooks in `BattleAbilities.Fire` /
  `ThinkSpells`; `Rules.Might` reads Tide for casters (`Balance.TideWeight`).
- Tests: `TideShared` (an enemy cast lowers what yours can draw), `Overreach` (casts past an empty Tide cost double and
  may backlash; seeded), `TideAgree` (`--agree` within bands).

### AR4. Doctrines: what kind of circle you keep

- A circle chooses a **doctrine** within its tradition when hired or at its master's rank 2. Faith: **Mercy** (heals,
  wards, rallies), **Wrath** (smite, judgement fields), **Vigil** (counterspell, warding, sight). Craft: **Storm**
  (weather, lightning), **Stars** (foresight, mist, mirage), **Curse** (evil eye, fever, dread), **Fire** (pots, smoke,
  wildfire). Each doctrine has 4-5 spells; cultures' lines keep their flavour by preferring one doctrine.
- Prepared slots stay by tier; known spells come from the doctrine first, learned ones (up to 2) from anywhere in the
  tradition. A **second circle** is allowed once `Campaign.Fame` reaches `Balance.SecondCircleFame` (about 100), of the other tradition only.
- New spells to round doctrines out (about 10): Vigil's *Unweave* (breaks an enemy cast in range, a real counterspell),
  *Sanctuary* (no curses in a field); Mercy's *Last rites* (the dying fight on 5 s); Wrath's *Judgement* (a field that
  hurts routers); Stars' *Omen of defeat* (an enemy division's sergeant hesitates: order delay x2); Curse's *Wither*
  (stamina drain); Fire's *Firebreak* (a line of fire that horse will not cross); Storm's *Gale* (arrows into it fall short).
- Data: `Data/abilities.json` gains `doctrine`; new `Data/doctrines.json` (id, tradition, spells, colour, line text);
  `Abilities.Validate` checks every doctrine has a heal or ward and a way to answer the other tradition.
- Files: `Abilities.cs`, `Campaign/Circle.cs` (`Doctrine`, `ChooseDoctrine`, `SecondCircle`), `BattleAbilities.cs`
  (new kinds: `unweave`, `delay`), `MapScreen.Muster.cs` (spellbook by doctrine).
- Tests: `DoctrinesValidate`, `UnweaveBreaks`, `SecondCircleOtherTradition`.

### AR5. The enemy's arcana

- **Court mages.** A realm's temper gains `arcana` (0-3, `Data/factions.json`): how much it invests in circles. Its great
  lords (tier IV+) raise circles by temper and Tide at home (replacing the one-in-five rule of 3e46a93); a realm's council
  has a **court mage** (a `Character` of kind `mage`, with a doctrine and a rank) who leads the realm's strongest circle
  and can be met, talked to (`Talk.cs`: ask, threaten) and slain.
- **Rival circles** remember you: a court mage whose circle you broke may come back with a grudge (as `GrudgeOf`).
- Enemy circles answer yours: Vigil and Wrath circles prefer targets near your casters; the general sends horse at a
  circle that overreaches (`HorseHuntsMages` built).
- Auto-resolve weighs circles through `Rules.Might` (built) and the Tide.
- Files: `Factions.cs`, `Data/factions.json`, `Campaign/CampaignAI.cs`, `Campaign/Characters.cs`, `Campaign/Talk.cs`,
  `BattleAI.cs`.
- Tests: `RealmsRaiseCircles` (arcana-3 realms field circles in 1 great lord in 2 by day 120; arcana-0 in none),
  `CourtMage` (named, leads the circle, can fall).

### AR6. Paths: the captain's arcane identity

At Kindled the captain chooses a **path**; at Aura Expert he may open a second, shallow (tier gifts up to Kindled's).

- **Sword** (today's aura, sharpened): forms, the blade, duels. Order forms sit here; an unsworn Sword captain gets a
  generic set of 6 forms (up from 3), two offered at each breakthrough, one taken.
- **Banner** (presence first): `Inspire` from tier 1 and wider by tier; new aura spends on your men (*Hold!*: a company
  within 15 m will not rout for 10 s; *Rally to me*: routers within 30 m turn; *Second wind*: stamina back to a company).
  These are forms (pressed keys), of a new kind `banner`.
- **Hearth** (resonance with the circle): your circle's casts near you (25 m) cost less Tide; a form *Lend aura* gives
  your aura to the circle's Will; *Anchor* lets the circle overreach without backlash for 15 s while you stand still;
  at tier 4 your blade carries the circle's doctrine (Mercy heals near blows, Fire burns, Curse withers). A Hearth captain
  without a circle has a weak path: the plan says so in the choice.
- Orders map to paths (Lily, Lantern: Banner; Rose, Wolf, Falcon, Sky: Sword; Ashen: Hearth (faith); Hollow: Hearth
  (craft, the Black Flame)), so an oath is also a path.
- Save: `CaptainState.Path`, `SecondPath`; old saves: Sword (as today).
- Files: `Campaign/Aura.cs` (`ChoosePath`, gifts by path), `Data/forms.json` (path, new forms), `BattleForms.cs`
  (kinds `banner`, `lend`, `anchor`), `BattleAura.cs` (Inspire by path), `BattleTide.cs`, `MapScreen.Captain.cs`.
- Tests: `PathsDiffer` (same captain, three paths, different battle results through the right systems), `HearthResonance`,
  `BannerHolds`, and `AuraBound` per path.

### AR7. Campaign arcana: rites with reach

- Rites become **workings on the map**, with a tradition, a cost (crowns, Omen, a well's strength) and a target:
  - *Scry* (stars): see a lord's army and its circle for 3 days; *Bless the walls* (faith): a siege you defend starts with
    a ward; *Blight* (curse): a province's harvest fails (unrest, food), Omen +5, the lord's opinion of you falls if he
    learns; *Calm the dead* (faith): grave fields in a province cannot rise for 30 days; *Storm the roads* (storm): a
    province's roads go to mud for 3 days (slows armies); *Sanctify* / *Bind* a well (AR2).
- AI realms with high `arcana` work them too (`FactionAI`), shown in rumours and the chronicle; the player can answer
  (*Unblight*, a Vigil working).
- **Omen per province** (save `Province.Omen`; the global `S.Omen` stays as yours) so dark workings mark places.
- Files: `Campaign/Rites.cs` (data-driven: new `Data/rites.json`), `Campaign/Wells.cs`, `Campaign/Diplomacy.cs`,
  `Campaign/Town.cs` (rumours), `MapScreen.Muster.cs` and a WORKINGS tab.
- Tests: `WorkingsValidate`, `BlightBites`, `AIWorks`.

### AR8. Legacy: magic remembered

- Named casters on the roll (built: the master) gain **titles from great workings** ("Ylva Stormcaller of Greyford").
  A great working names the battle (`Legacy.BattleName`). A fallen master's staff becomes a relic item (docs/COMPANY.md C9
  items) that teaches a spell.
- Wells you consecrated or bound appear in THE LEGEND; a court mage you slew is a deed.
- Files: `Campaign/Legacy.cs`, `Campaign/Roll.cs`, `Campaign/Items.cs`, `MapScreen.Realm.cs`.
- Test: `WorkingsRemembered`.

### AR9. Look, sound, tips, balance, archive

- The Tide band, well markers and auras of wells on the map (`World.SiteModel` glows by tradition), path colours on the
  K sheet, doctrine colours on spells (`SpellColor` by doctrine). Cues for overreach and backlash (`Cue.Backlash`).
- Tips for the Tide, overreach, paths and wells; F1 lines for new keys.
- Balance: `--arcana` targets (AR0), `--agree`, `--realms` (wells must not snowball a realm), `--pacing`.
- Archive this plan to `docs/archive/` with an "As built".

## 5. Order and size

| Phase | Depends on | Size | Player sees |
|---|---|---|---|
| AR0 Measure | | S | nothing (tooling) |
| AR1 Aura you can find | AR0 | M | aura for everyone who fights; trials with directions |
| AR2 Wells | AR0 | M | places of power; mages from them |
| AR3 The Tide | AR2 | M | a contested pool; casting first matters; overreach |
| AR4 Doctrines | AR3 | L | choosing what kind of circle you keep; counterspells |
| AR5 Enemy arcana | AR3, AR4 | M | court mages; enemy circles that answer yours |
| AR6 Paths | AR1, AR3 | L | Sword, Banner or Hearth captain |
| AR7 Campaign arcana | AR2, AR5 | M | workings on the map, from you and the realms |
| AR8 Legacy | AR4, AR6 | S | titles, relics, deeds |
| AR9 Polish, balance | all | M | |

AR1 and AR2 can go in parallel. AR1 alone already fixes "most players never see aura"; AR2+AR3 alone already make magic
something the map and the battle are about.

## 6. Decisions for you (recommendation first)

1. **Should the captain cast?** Recommended: **no**; the Hearth path makes him the circle's anchor instead (pillar 1,
   and it keeps the circle the people of your legacy). Alternative: a minor tradition of his own at Hearth tier 3.
2. **One Tide pool for both sides, or one each?** Recommended: **shared** (a real contest). One each is simpler to read.
3. **How common should enemy magic be?** Recommended targets in AR0 (1 battle in 6 by day 120). Higher makes every
   battle a magic battle; lower keeps it a set piece.
4. **A second circle?** Recommended: once Fame reaches about 100, other tradition only. Alternative: never (keep "mages are super units").
5. **Wells on old maps?** Recommended: derive from existing sites (no layout bump, old saves get wells). Alternative:
   new layout 5 with wells placed on purpose (better geography, only new campaigns).

## As built

- **AR0 (measure):** `BalanceArcana.cs`: `--arcana N` (`Arcana`, `ArcanaRun`, `ArcanaSample`) plays seeds 4001.. twice for
  `ArcanaDays` (240), seeking and plain, and writes `balance/arcana.csv`. `Pilot.Seeker` rides to the nearest ready vigil
  site (`Arcane`) when its aura stands at a wall a vigil breaks, shuns a site it had to flee from for 5 days (it once
  dithered between the stones and a refuge for ten days), and blesses its company / mends a cracked core in town when it
  keeps a circle. Telemetry `Pilot.MagicBattles`, `EnemyMagic`, `Vigils`, `Rites`, `AuraDay`. The stand-in does not duel or
  fight by hand, so its aura days are a lower bound: a battle you fight yourself gives kills, parries and ripostes too.
  Test `ArcanaCensus`.
- **AR1 (aura you can find):** `Campaign/Aura.cs`. `Seasoned`: while the first wall stands, a won battle adds 1 to
  `CaptainState.WallWins` (2 if fought); at `SeasonWins` (12) it breaks ("Battle after battle, it wakes."). Only the first
  wall. A won tournament (`FinishTournament`) breaks walls below `DuelBreaksBelow`, as a duel does. `NearestTrials` is the
  trial card on the K sheet (`AuraBlock`): each way through this wall with where it is now (the nearest *ready* vigil site
  in days and a bearing; a hostile lord in sight who would accept a duel). `TrialsFor` names the new trials. The census
  (2 seeds): a plain captain wakes on day 81-87 (target ~90); a seeking one on day 68-107 (it reaches the wall's 30 XP
  only around day 45-55 from auto-resolved fights). Breakthroughs as choices wait for AR6 (paths). Test `AwakeningByDeeds`.
- **Found on the way:** the self-test's `ResolveBattles` (level 4) stalled because pathing ignored men within 12 m of
  their goal: the last enemy bows stood pressed to a village fence with their places 4 m beyond. `BattlePath.AnyBlocked`
  and `PathVia` now route near goals when a fence or hedge crosses the line (test `RoundTheNearFence`).
- **AR2 (wells):** `Campaign/Wells.cs`. A site is a well by kind (`WellTradition`: shrine, spring, hermit = faith; stones,
  ruin, battlefield = craft) with a fixed strength from its id (`WellBase`, 1-3; seed 61: 51 faint, 51 steady, 13 strong).
  No map change: every map and old save has wells. Marks are the only save: `CampaignState.WellMarks` (`WellMark`:
  tradition, faction, day). `Well(site)` gives tradition and strength (a mark turns it and adds a step). `TideAt(pos)`: the
  strongest well within `WellReach` (25) x `Map.Spread` at full strength, within twice that a step less (so a faint well
  reaches only close by); half the towns of seed 2001 lie where a well reaches. Rites `consecrate` (faith, 120 crowns) and
  `bind` (craft, Omen +5) at a well (`WellHere`) in a province of your realm or one of your fiefs (`CannotMarkWell`,
  `MarkWell`, a deed). Circles for hire: a town with a chapel or a tide (`RefreshMercenaries`), tier V from day 120 at a
  strong tide; this replaces 3e46a93's shrine radius. UI: wells on the realm map (M; a mark sized by strength, pale blue
  faith, violet craft, edged white when marked; legend lines), the encounter panel's tide line (`TideNote`). Tests
  `WellsFromSites`, `TideFeedsOffers`. Province neighbours and wells feeding a realm's own culture's mages are left out.
- **AR3 (the Tide in battle):** `BattleTide.cs`. `BattleOptions.Tide` (0-3; a quick battle 1, the campaign the field's
  `TideAt`, 0 where no well reaches) and `TideTradition`. `Battle.Tide` / `TideCap` = `TideBase` (60) x (1 + Tide), refilled
  over `TideRefill` (120 s) while any company can cast (`Arcane`). `PayForCast`: a mage company's cast pays its Will and
  draws the same from the Tide (`TideDraw`: half for the well's own tradition); short of it, an **overreach** pays twice the
  Will, empties the Tide and lashes back at `OverreachRisk` (0.35, battle dice): one caster struck to 1 hp (never killed,
  so nobody's kill is counted), the circle -12 morale, Omen +1 for your craft; counted in `Overreaches`/`Backlashes`.
  Casting by rule waits for the Tide except a great working, which may overreach; your ordered casts overreach when they
  must (`OrderedCast`; `CannotCast` says NO TIDE only when even that is out of reach). Heroes' abilities draw nothing.
  HUD: a thin violet Tide gauge under the tug bar while any company can cast (hover: the Tide and its well); the spell fan
  reads WILL and TIDE, and a spell that would overreach shows its doubled cost in red (`WouldOverreach`). Auto-resolve:
  `Campaign.TideEdge` (`Balance.TideWeight` 0.5) by each side's mage share. `--agree` unchanged (its fights have no
  mages). The general sending horse at an overreaching circle is left for AR5. Tests `TideShared`, `Overreach`.
- **AR5, first step (realms that lean on sorcery):** `FactionDef.Arcana` (0-3, default 1; `Data/factions.json`: Crimson and
  the Ashen Sun 3, Northmarch, Sunward and the Khaganate 2, Azure and the League 1; `Factions.Validate`). `KeepsCircle`: of
  four great lords, as many as the realm's arcana (one more where a steady well reaches his home), by id; it replaces the
  one-in-five rule at spawn and at refit (`RaiseCircle`). Census (2 seeds, 240 days): lords with a circle 10-19 of ~125 (was
  3-8). The stand-in fights lords in only 7-13 of ~110 battles (brigands the rest, who never cast), so "magic in 1 battle in
  6" cannot be met by lords alone: against lords, enemy magic in 5 of 39 battles (target 1 in 3). Open question for the
  next step: cunning folk among the bigger brigand bands, or more circles among lords. `Pilot.LordBattles`/`LordMagic`.
  Court mages, rival circles and the AI answering your circle are still to come. Test: `MagesInTheWorld` checks arcana 3
  realms keep more circles a lord than arcana 1.
- **AR5, second step (court mages, cunning folk):** `Campaign/CourtMages.cs`. `NameCourtMage`: a lord's new circle (at spawn
  or refit) gets a named head, a `Character` of kind `mage` (`PartyId`, `CompanyId`; named from the lord's culture by id, no
  dice; titled by tradition, `MageTitle`), and the circle is called after him. `CourtMageOf(party)`; the encounter panel
  shows "LED BY ..." on the circle's card. `CourtMagesAfterBattle` (from `Apply`): a mage whose circle is cut down falls (a
  deed when you did it). `CunningFolk`: from day 90 one brigand band in five of tier III+ brings a few hedge witches (by id).
  The enemy already answers circles (horse hunt mages first, elite bows focus them: MELEE D6/E4). Talking to a court mage and
  rival circles with grudges are left for later. Census after this step (2 seeds, deterministic now): magic in 1 battle in
  14-26 over 240 days for three runs, 1 in 2.6 for a seeker that kept its own circle. Test `CourtMage`.
- **Found on the way:** campaign weather, battle seeds, armourers' stock and more were mixed with `HashCode.Combine`, which
  .NET seeds per process; the same save went another way in every run. Now `Rng.Mix`/`Rng.Of` (commit "the same save plays
  the same in every process"); `tools/check.sh` refuses `HashCode` in campaign and sim code.
- **AR6 (paths):** `CaptainState.Path` ("" until chosen; old saves choose when they next open K), `Campaign.ChoosePath` /
  `CannotChoosePath` / `PathLine` (Aura.cs), once, from Kindled; `LearnForms` learns the path's forms by tier
  (`Forms.OfPath`; `FormDef.Path`, `Act`; `Forms.Generic` leaves them out). Into battle as `BattleOptions.Path` (`WithCaptain`,
  only with a woken aura). `BattlePaths.cs`: **Sword** `FormCost` x`SwordThrift` (0.85; HUD form bar and K sheet show it);
  **Banner** `InspireFrom` tier 1 and `InspireReach` 16 m (was tier 2, 10 m), forms Hold Fast (I, `held`), Steady Them (III,
  new effect `steadied`), The Unbroken Banner (IV, great), all `buff` forms (which also turn routers back); **Hearth**
  `Hearthside` (your caster company within `HearthReach` 25 m) draws `HearthDraw` (0.5) from the Tide (`TideDraw` takes the
  squad), forms of the new kind `hearth` with an `act`: Lend Aura (I: your aura x1.2 into the nearest circle's Will), Anchor
  (III: `Anchored` circles near you overreach without backlash for 15 s), Kindle the Tide (IV, great: Tide and Will full);
  aspects `hearth_deep`, `hearth_long`. K sheet `PathBlock`: three buttons with tooltips, then the path's line. Sworn orders
  are not yet bound to a path, the second shallow path and breakthrough choices are left out. Test `PathsDiffer`.

