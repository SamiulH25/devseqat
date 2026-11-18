# Scope plan: cut, merge, finish (SC0-SC9)

Status: **planned** (2026-11-18). Nothing is built yet.

The evidence is in `docs/AUDIT.md`: what a year of play reaches, system by system. This plan turns that audit's verdicts
into phases.

Each phase that cuts or merges something is **gated on one owner decision** (AUDIT.md section 7, "D1"-"D7" below). The
plan is written with the recommended answer. If the owner answers differently, that phase is rewritten before it starts;
the other phases don't change.

Read this before choosing work. Until the plan is done, it takes priority over the other plans in `docs/`, except
`PLAYTEST.md` (PT4-PT6 are folded in below where they overlap).

## Rules for the whole plan

1. **One phase, one commit** (or a short run of commits).
   - `tools/check.sh` must end `ALL CHECKS PASSED` after each.
   - Run `tools/codemap.sh` after each.
   - Update the CLAUDE.md file guide row for every system touched, in the same commit.
2. **Removing a feature's tests along with the feature is fine. Weakening a test that still guards live code is not.**
   - Each phase lists the tests it expects to delete.
   - A test that breaks and is *not* on that list means a hidden dependency. Stop and fix the dependency, not the test.
3. **Cut whole.** A cut removes:
   - the code;
   - the data entries (`Data/*.json`);
   - the save fields;
   - the UI blocks, key bindings and tips;
   - the shots;
   - the events whose trigger it owned;
   - its rows in CLAUDE.md, README.md and docs.

   No dead flags left behind to "turn it back on". Git keeps the history; tag the commit before each cut
   (`git tag pre-SCn`) so a feature can be recovered.
4. **Old saves.** System.Text.Json skips members a class no longer has, so removing a save field does **not** break
   loading. Old saves load and silently lose that state.
   - Default (D7): no `CurrentVersion` bump.
   - Bump only if a cut leaves an old save in a state the new code can't read correctly; say so in that phase's
     "As built".
5. **Measure before and after.** Every cut or merge records the `--census` lines it touches (SC0) before and after, in
   its "As built".
6. **Don't answer "unused" with "add more".** If a kept system is still unused after its phase, make it cheaper to reach
   or easier to see first.
7. **Stay off the world map** while the other session works there: `WorldMap.cs`, `WorldRoads.cs`, `WorldLand.cs`,
   `World*.cs`, `MapLife*.cs`, and party spawn counts. Map findings go in the notes at the end for that work.

## Order and size

| Phase | What | Decision | Size | Net lines (estimate) |
|---|---|---|---|---|
| SC0 | `--census`: measure every system | | S | +200 |
| SC1 | Cut the Black Flame | D2 | M | -600 |
| SC2 | Cut knight orders, player side (and your own order) | D1 | L | -1,300 |
| SC3 | Cut your own form and form aspects | D3 | S | -350 |
| SC4 | One aura ladder: tiers 0-3, path at tier 1, about 12 forms | D3 | L | -800 |
| SC5 | Campaign magic down to "hire a circle, it casts" | D4 | L | -1,500 |
| SC6 | Named men: fewer and sturdier, then named men in the fight (COMPANY.md C6) | D5 | M | +300 |
| SC7 | Battle orders and HUD trim (with PT4) | needs a playtest | M | -400 |
| SC8 | Lords: decide with PT6 | D6 | design | n/a |
| SC9 | Docs and process: fix the drift, write the rules down, archive | | S | n/a |

Cuts first (SC1-SC3): they are cheap and shrink everything after them. SC1 before SC2 and SC3, because the Black Flame
reaches into orders, your own order and your own form. Then SC4 (the merge that the cuts make room for), then SC5. SC6
and SC7 don't depend on the cuts and can run in parallel with them.

---

## SC0: Census

**Why:** the stand-in captain (`Pilot`) runs about 50 campaign actions and none of the meta systems. So `--balance` and
`--pacing` can't tell whether a system is reachable. This phase makes reach a number that every later phase reports.

**Build:**
- `BalanceCensus.cs` (from the audit's throwaway probe). Add
  `--census [seeds] [days] [seek]` to `Program.cs` (default 6 seeds, 360 days).
- It plays the `Pilot` (and the `Seeker` with `seek`). It prints one block per seed and writes `balance/census.csv`
  (one row a seed, one column a system).
- Columns:
  - career stage;
  - battles fought and won, and the share against lords;
  - magic battles;
  - aura tier and the day of each tier;
  - path; forms known;
  - named men alive and fallen;
  - gear marked and named;
  - sites visited; tournaments; duels;
  - quests done;
  - distinct events fired;
  - peak parties by kind (these columns stay even while the world-map work is on, since they only read).
- One column per system still in the game. A phase that cuts a system removes its column; a phase that adds one adds a
  column.
- Add the command to CLAUDE.md "Commands".

**Test:** `CensusRuns`: 1 seed, 30 days, every column present, no exception.

**Done when:** `--census` reproduces the audit's numbers (AUDIT.md section 2), and those numbers are pasted here as the
baseline.

---

## SC1: Cut the Black Flame (D2: cut whole)

**What goes:**
- The pact and corruption.
- Burning a wall.
- `FlameDaily`, `MadDay`, the purge, both endings.
- Madness in battle.
- The hidden `hollow` order: its troops, forms (3) and orders.json entry.
- The flame look and sounds that nothing else uses.

**Files:**
- Delete `Campaign/BlackFlame.cs` and `BattleFlame.cs`.
- Remove the hooks in:
  - `Campaign/Aura.cs` (`TemptFlame` after a cracked core);
  - `Campaign/Events.cs` (the `flame_bargain` and `flame_end` triggers);
  - `Campaign/OwnOrder.cs`, `Campaign/Orders.cs`, `Campaign/OrderPolitics.cs`, `Campaign/Duels.cs`,
    `Campaign/CampaignBattle.cs`, `Campaign/CampaignActions.cs`, `Campaign/Campaign.cs`;
  - `BattleTypes.cs` (`Madness`), `Battle.cs`, `BattlePlayer.cs`, `BattleCombat.cs`, `BattleForms.cs`, `Forms.cs`,
    `Squad.cs`;
  - drawing: `BattleScreen.Ladder.cs`, `.Magic.cs`, `.Spectacle.cs`, `.Draw.cs`, `Hud.cs`;
  - `MapScreen.Captain.cs` (`FlameBlock`), `MapScreen.cs`, `ScreenHost.cs` (shots).
- Audio: drop flame-only cues from `AudioSynth*.cs`, only where nothing else plays them.

**Data:**
- `Data/events.json`: `flame_bargain`, `flame_end`, and any event gated on corruption.
- `Data/forms.json`: the `hollow` forms.
- `Data/orders.json`: `hollow`.
- `Data/troops.json`: `order: hollow` troops. Check `Troops.Validate`, then re-run `tools/troopmap.py --write`.

**Save:** `CaptainState.Corruption`, `Pact`, `Purged`, `FlameSeen`, `FlameEnd`.

**Kept:** the cracked core (`core` wound). It still costs a tier in battle; it just no longer opens the bargain.

**Tests to delete:** `BlackFlame`, `FlameTakesYou` (KnightTests). Check `Press` and `PathsDiffer` for flame references.

**Done when:**
- No identifier from the cut remains: `grep -rE "Corruption|Pact\b|Flame|hollow"` finds only unrelated words (`Flame` in
  spell names, if any).
- The seeker census no longer shows `flame`.

---

## SC2: Cut knight orders, player side (D1: cut, keep orders as AI factions)

**What goes:**
- Standing; the trial of entry; the oath; order ranks; vows and penance.
- The chapter's brothers; order contracts.
- The rival; grand master elections; crusades.
- Your own order: founding, creed, knighting named men, handing it on.
- The R screen's ORDERS tab and the town's CHAPTER tab.

**What stays:** the seven orders as minor factions of a patron realm.
- `Campaign/Orders.cs`: `KnightOrders`, `SyncOrders`, `SpawnOrderArmy`, `RefitOrder`, `OrdersDaily`, `KnightHeir`.
- Their armies on the map, their signature troops (troops.json `order`), and their champions in battle (with the forms
  SC4 leaves them).

**Files:**
- Delete `Campaign/Oath.cs`, `Campaign/OrderPolitics.cs`, `Campaign/OwnOrder.cs` and `MapScreen.Orders.cs`.
- Trim `Campaign/Orders.cs` to the AI side.
- Remove the hooks in:
  - `Campaign/CampaignBattle.cs` (`OrderAfterBattle`);
  - `Events.cs`, `CampaignWar.cs`, `Legacy.cs` (`KnightsAlive` in `Fame`), `Callings.cs`, `Circle.cs`, `Buffs.cs`,
    `CampaignAI.cs`, `Aura.cs`, `Rites.cs`, `Characters.cs`, `Roads.cs`, `Record.cs`;
  - `BattleAuraMagic.cs`, `BattleEffects.cs`, `BattleRelief.cs`, `Battle.cs`;
  - `MapScreen.Realm.cs` (drop the 4th tab), `MapScreen.Settlement.cs` (`ChapterAt`), `MapScreen.Captain.cs`,
    `MapScreen.cs`, `ScreenHost.cs`.

**Data:**
- `Data/troops.json`: `order: own` troops go. Other order troops stay.
- `Data/events.json`: order events.

**Save:**
- `CaptainState.Order`, `OrderRank`, `OrderDeeds`, `Penance`, `Forsworn`, `Rival`.
- `CampaignState.OwnOrder` (`FoundedOrder`), `Soldier.Knight`.
- In `OrderState`, keep only what the AI side reads.
- `OrderRng` stays: the AI armies roll it.

**Tests to delete:** `OrderOath`, `CreedBroken`, `OrderRankGate`, `OrderPolitics`, `OwnOrderFounded`.

**Tests to keep, possibly trimmed:** `OrdersOnMap`, `ChampionsUseAura`.

**Done when:**
- The orders still march and fight on the map (census peak `Lord:order` unchanged).
- The R screen has three tabs.
- No town has a CHAPTER tab.

---

## SC3: Cut your own form and form aspects (D3)

**What goes:**
- The style tally after each battle.
- The two forms offered.
- Making your own form.
- Aspects at rank III.

**Files:**
- Delete `Campaign/OwnForm.cs`.
- Trim:
  - `Forms.cs` (`Register`, `OwnId`, `AspectDef`);
  - `BattleForms.cs`, `BattleTypes.cs` (`OwnForm`, `FormAspects`);
  - `Campaign/Rules.cs`, `CampaignBattle.cs`, `Campaign.cs`;
  - `MapScreen.Captain.cs` (`OwnFormBlock`, the aspect buttons in `FormList`).

**Data:** the `aspects` block in `Data/forms.json`.

**Save:** `StyleTally`, `OwnFormOffer`, `OwnForm`, `FormAspects`.

**Tests to delete:** `OwnFormFromStyle`, `AspectChanges`.

---

## SC4: One aura ladder (D3: tiers 0-3, path at 1, about 12 forms)

**Now:**
- Six tiers (0-5), with walls at every tier.
- Three kinds of trial: duel, odds, vigil.
- 61 forms: 24 order forms, 9 path forms, the rest by kind.
- A year of play reaches tier 1 and one form.

**After:**
- **Four tiers: Unawakened, Kindled, Aura Knight, Master.**
  - XP needed: about 30 / 120 / 400 (tune with `--census` so a fighting player reaches tier 2 around day 120 and tier 3
    around day 300).
  - The first wall breaks by deeds, as now (`Seasoned`).
  - Walls 2 and 3 break with **one** trial each: a won duel, or holding against two to one.
  - Vigils go (sites keep their events).
- **Each tier gives one thing you feel:**
  - 1: breath back faster, and your path;
  - 2: the aura blade;
  - 3: aura armour, plus the presence that shakes nearby foes.
  - The old tier 4-5 gifts fold into 3. "Transcendent" and its light go.
- **A path at tier 1** (Sword, Banner, Hearth), chosen with a prompt when it wakes, not found on the K sheet.
  - Hearth stays only if SC5 keeps a circle you can stand beside. If D4 cuts circles too, Hearth goes and two paths
    remain.
- **About 12 forms:**
  - 3 general forms (one per form slot key you start with);
  - 3 per path, learned at tiers 1-3.
  - Order champions (AI) draw from the same pool by their order's style; the per-order forms go.
  - Keep 4 keys (U, I, O, L) only if there are 4 slots to fill; otherwise drop to 3 and free a key.
- **Moments:** keep the clash of champions and the breakthrough in the field. Cut tier 5 light and arrow cutting
  (`BattleMoments.InLight`, `CutsArrow`).

**Files:**
- `Campaign/Aura.cs` (tables, `BreakThrough`, `VigilAt`, `TrialsFor`, `NearestTrials`).
- `BattleAura.cs`, `BattleMoments.cs`, `BattleChampion.cs`, `BattlePaths.cs`, `BattleForms.cs`, `Forms.cs`,
  `BattleScreen.Ladder.cs`.
- `MapScreen.Captain.cs` (`AuraBlock`, `PathBlock`, `FormList`).
- `Data/forms.json`.
- `Balance.AuraWeight` (auto-resolve).

**Save:**
- `AuraTier` above 3 clamps to 3 on load (a one-line fix-up in `Load`, not a version bump).
- Forms no longer in the data are dropped from `Captain.Forms`.

**Tests:**
- Rework `AuraGrows`, `CoreAndTrials`, `FormsLearned`, `PathsDiffer` and `AwakeningByDeeds` to the new tiers.
- Delete `TranscendentLight`.
- Keep `AuraBlade`, `AuraArmour`, `ClashRing`, `BreakInBattle`.

**Done when:** the census shows a plain stand-in reaching tier 1 by day ~90 and choosing a path. Teach the stand-in to
choose one: the first path offered.

---

## SC5: Campaign magic down to "hire a circle, it casts" (D4)

**Keep** (the part players see):
- caster troops and their lines;
- the ability engine (Will, `when` rules, the warning ring, fields, great workings);
- casts you order (the P key, the spell fan);
- enemy lords who keep a circle, and court mages with a name (small, and they make the enemy's magic legible);
- mist, mirage and the ward counterspell (they are battle tactics);
- the spectacle visuals.

**Cut:**
- **The Tide.** One pool (Will) is enough.
  - Delete `BattleTide.cs`.
  - `BattleOptions.Tide`/`TideTradition`, `Campaign.TideEdge`, the Tide gauge in `Hud.cs`, the spell fan's TIDE column,
    `WouldOverreach`.
  - Overreach and backlash go with it.
- **Wells.**
  - Delete `Campaign/Wells.cs`.
  - The realm-map tide colours (`MapScreen.Atlas.cs`), the encounter tide line, the consecrate and bind rites.
  - Where circles are for hire goes back to a simple rule: a town with a chapel or shrine, plus tier V from day 120.
- **Doctrines and Unweave.**
  - `ChooseDoctrine`, `LearnRoom`, `Abilities.Doctrines`, `Squad.Doctrine`, `Battle.Unweave`, the `casting` rule.
- **Map workings.**
  - Delete `Campaign/Workings.cs` (blight, calm the dead; AI realms blighting 30-40 provinces a year that nobody sees).
- **Omen, the relic, most rites.**
  - `Campaign/Rites.cs` shrinks to at most two rites a circle can work in camp: a blessing before battle, and healing.
  - The `dark_omens` events, `OmenStirs`, `OmenShunned`, `cleanse`, `RelicReady`, `RaiseRelic`.
  - Check `Sky.cs`, `MusicDirector.cs` and the `Audio*` files for omen-driven looks and sounds; cut them unless they are
    shared.
- **Spellbook and the circle's master merge into the circle.** A circle casts its line's spells by tier, as it did before
  MA2.
  - Delete `Campaign/Circle.cs`'s learn and prepare side (`Known`, `PrepareSlots`, `Prepared`, `CannotLearn`,
    `LearnSpell`, `Prepare`) and `MapScreen.Muster.cs` `Spellbook`.
  - Keep `MasterOf` only if leaderless circles still matter in battle; otherwise cut it too.

**Also:** remove `BalanceArcana.cs` and `--arcana`; `--census` replaces it.

**Save:**
- `WellMarks`, `Blights`, `Calmed`, `Omen`, `RiteDay`, `RelicCharges`, `Relic`.
- `CompanyRecord.Spells`, `Prepared`, `Master`, `LeaderlessUntil` (if the master goes).

**Tests to delete:**
- `WellsFromSites`, `TideFeedsOffers`, `TideShared`, `Overreach`, `DoctrinesAndUnweave`, `WorkingsBite`,
  `WorkingsRemembered`, `OmenTeeth`, `RitesWork` (rework instead if any rite stays), `SpellbookCarries`,
  `SpellbookByTier`, `CircleMaster`, `ArcanaCensus`, `RelicRaised`, `BlessingFeedsAura` (if it read rites).

**Tests to keep:** `MagesInTheWorld`, `CourtMage`, `CastOnOrder`, `WardBreaksCast` and the battle spell tests.

**Done when:** census magic-battle share is unchanged or higher, and the player's circle shows its spells on the spell
fan with no map-side bookkeeping.

---

## SC6: Named men that matter (D5)

**Now:**
- Up to `RollSize` 6 named men a company, so 13 companies means about 65 names.
- About 200 named deaths a year: each roll turns over three times a year.
- The parts that would make a name pay off (COMPANY.md C5-C7) were never built.

**Part 1: fewer, sturdier names.**
- The roll becomes the company's, not each banner's: **about 12 named men for the whole company.** The best man in a
  company is named when a seat frees, so a new company doesn't name six strangers at once.
- `VeteranSurvival` goes up so a named man dies about a fifth as often as his stack. Target in the census: **about 20-30
  named deaths a year**.
- Natures and ties stay only where Part 2 reads them. A tie that no fight reads (only text on the roll) goes.

**Part 2: COMPANY.md C6, named men in the fight.**
- A named man is visible in battle (his marker already exists in `Hud.NamedMarks`) and does one thing his nature says:
  - he stands longer;
  - he leads a lunge;
  - he goes for the man who killed his tie (`Unit.Avenging` exists).
- His deeds come back in the report by name.

**Not now:** C5 (the camp) and C7 (the book of the fallen). Decide after the playtest of Part 2.

**Tests:**
- Rework `MenHaveTies`, `MenHaveNatures` and `RollChoices` to the shared roll.
- Add `NamedMenFight`: a named man with a nature behaves as that nature says, in a seeded duel of companies.

---

## SC7: Battle orders and HUD trim (with PT4; needs a playtest)

PT4 built the RTS command view and the field HUD split. This phase is the cut that PT4 left. **Don't start it until the
playtester has played a few battles on PT4,** and decide from their notes.

**Candidates:**
- **Order palette:** at most six big orders (move, attack, hold, fall back, fire/hold fire, follow me).
  - Formation and spacing move into a "more" fold, together with relieve, queued orders, standing rules, split and merge
    (`BattleRelief.cs`, `BattleQueue.cs`, `BattleMerge.cs`).
  - If the playtester never opens the fold, cut split and merge.
- **Sergeants:** keep the command cap, the reserve tray, call-ups and voice range.
  - Horn plans and sergeants' questions are the first to cut if the playtest says the HUD is still crowded.
  - `BattleCommand.cs` `SetHornPlan`, `SoundHorn`, `Questions`, `Answer`; keys `Act.Horn`, `AnswerA/B`.
- **Alerts:** keep, but in one corner; the pause-on-alerts setting stays.

**Done when:** the PT2/PT4 battle shots show no more than about 7 widgets in the field view and the playtester's notes
say the orders read at a glance.

---

## SC8: Lords (D6: hold for PT6)

Houses and kin, bonds and feuds, wants, promises, talk, courting and marriage come to about 2,000 lines. The playtester
asked for *more* of this, Crusader Kings style (PLAYTEST.md notes 7 and 8). So it is not cut now.

PT6's design pass decides, for each part, whether it serves "a lord's personality decides how he fights and how he
treats you".
- What serves that gets finished in PT6.
- What doesn't is cut in PT6 with the same rules as this plan.

Until then: no new lord features.

---

## SC9: Docs and process

**Fix the drift:**
- CLAUDE.md: "~17k lines" becomes the real figure; remove the rows of cut systems; add `--census`.
- `docs/COMPANY.md` status: C0-C4 and C9-C14 built; C5-C8 open (C6 in SC6).
- `docs/ARCANA.md`: mark the parts SC5 cut; move it to `docs/archive/`.
- `docs/archive/KNIGHT_ORDERS.md` and `docs/archive/MAGIC_AURA.md`: add a line at the top saying what this plan removed.
- `README.md`: player rules for the cut systems go.
- `docs/index.html`: drop features that no longer exist.

**Write down the rules** (in `docs/DEVELOPING.md`, a short "Adding a system" section):
1. A new system gets a `--census` column in the same commit. If no automation can reach it, say why it exists.
2. A plan builds its payoff phase first; a plan whose payoff isn't scheduled doesn't start.
3. "Nobody uses X" is answered by making X cheaper to reach or easier to see, before adding anything to it.
4. Before a plan starts, it says what it removes or replaces, not only what it adds.

**Archive:** when SC1-SC7 are done, move this file and `AUDIT.md` to `docs/archive/` with an "As built".

---

## Notes for the world-map work (not this plan)

- **Brigands and goblins:** at peak about 420 brigand bands (+~60 robber knights) and 45-109 goblin bands against 140
  lords, although `Balance.BanditsMax` is 80. Check how `PartyScale` and the spawners apply the cap. Brigands make up
  nearly all of the ~150 battles a year. More lord battles would also raise the magic share without new code.
- **Monster kinds:** seven kinds; undead (3 hosts) and wyverns (4) are rare. Once the world work settles, keep three or
  four kinds that each fight differently, and cut the rest with this plan's rules.

## As built

(Fill in per phase: what changed, census before and after, tests deleted, anything left out.)
