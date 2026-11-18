# Scope audit (2026-11-18)

Status: **audit done, decisions pending.** This page lists every system added on top of the core loop, measures how often a
campaign meets it, and proposes for each one **finish**, **merge**, **cut** or **leave**. Nothing has changed in the code yet.
The owner decides each verdict. The decided work then becomes phases in this file, done one commit at a time with
`tools/check.sh` green after each.

Out of scope: the world map (another session is working on it). Map findings are noted below for that work, not acted on here.

## 1. The headline

- **The game is 70,600 lines of C#, not the ~17k CLAUDE.md says.** About 300 commits in 12 days (Nov 5-16), so nearly
  every plan phase shipped within hours of being written.
- **The core loop is solid and well tested**: battles fought from the ranks, companies with troop trees, lords at war,
  sieges, auto-resolve.
- **A year of play reaches a small fraction of the systems stacked on top of it.** In six one-year campaigns the
  stand-in captain never swore to an order, never chose a path, never got past aura tier 1, never made a form or founded
  an order, never marked a well, and never cast a spell of its own.
- **When a system went unused, the fix was usually another system.** docs/ARCANA.md opens by measuring that magic and aura
  were "a rare guest". The answer was to add wells, the Tide, doctrines, court mages, workings and three paths. Magic is
  still in 3-28% of battles and the player's own magic is still at zero.
- **Several plans stopped before their payoff.** COMPANY.md built faces, natures, ties and roll choices for named men
  (C1-C4), but not C5-C7: the camp, **named men in the fight**, and the book of the fallen. Meanwhile about 200 named men
  die a year, so a name means little.

## 2. How it was measured

A throwaway probe (not committed; it can become a `--census` command if wanted) ran the existing stand-in captain
(`Pilot`) for **360 days on 6 seeds**, plus **4 seeds as a "seeker"** that goes after aura and vigils. At the end it read
the save state of every system, and every 10 days it counted the parties on the map.

**Caveats.**
- The stand-in auto-resolves every battle and never fights by hand. A human earns aura XP faster from kills, parries and
  ripostes, so aura numbers here are a lower bound.
- The stand-in never uses divisions, duels, the smith, the spellbook, orders, courting or rites. A zero there means "no
  automation reaches it", not proof that a human wouldn't. That is a finding in itself (section 5).
- Even allowing generously for that, the gaps below are too big to be explained by the stand-in alone.

### Raw results, plain stand-in (6 seeds x 360 days)

| | Result |
|---|---|
| Career | MARSHAL by day 361 in 5 of 6; one seed stuck at Sworn Sword (11 of 40 won) |
| Battles a year | 130-170, nearly all against brigands |
| Aura | tier 1 (wall up) in 5 of 6, tier 0 in 1; one form (`focus`) |
| Path, order, own form, own order, aspects | never |
| Magic battles | 5-42 of ~150 (3-28%) |
| Spells in your own companies | 0 |
| Wells marked (anyone) | 0 |
| Provinces blighted by AI realms | 31-43 a year |
| Named men fallen | 166-205 a year (roll of 54-65 alive) |
| Gear pieces with marks / with a name | 2-6 / 0 |
| Sites visited | 0 |
| Tournaments, duels | 1-2 / 0 |
| Events fired | about 50 distinct ids over 6 years, of 98 in events.json |
| Peak parties on the map | bandits ~420 (+~60 robber knights), lords 140, goblin bands 45-109, caravans 30, order armies 14, spiders 8-13, trolls 8, wolves 6-10, bears 5-7, wyverns 4, undead 3 |

### The seeker (4 seeds)

- Reached aura tier 2 at best and kept 2-3 vigils a year.
- Took the Black Flame pact in **every** seed, because `flame_bargain` fires after every cracked core and the stand-in picks
  the first choice.
- Corruption reached 5. The flame's thresholds are 25, 50 and 75, so madness, the hunt, the purge and both endings were
  never reached.

## 3. System by system

Cost is lines of C#, not counting data, tests or shots. Reach is measured where possible.

| # | System | Cost | Reach (measured / from the gates) | Proposal |
|---|---|---|---|---|
| 1 | **Aura ladder**: tiers 0-5, walls, trials, vigils, 61 forms, three paths, own form and aspects, champions, battle moments | 1,350 | Tier 1 after a year. Tier 3 gates your own form, aspects and founding an order, so none of those are reached | **Merge** into one short ladder (sec. 4.1) |
| 2 | **Knight orders, player side**: standing, trial, oath, ranks, vows, penance, rival, elections, crusades, own order | 930 | Never engaged in 10 campaigns | **Cut** the player side. Keep orders as AI minor factions with signature troops |
| 3 | **Black Flame**: pact, corruption, burning a wall, madness, purge, two endings | 190 + events | Pact taken by accident; nothing past corruption 5 | **Cut** (or keep as one dark-bargain event with no track) |
| 4 | **Magic, battle side**: caster troops, abilities, Will, ordered casts, fields, great workings, spectacle | ~1,500 | 3-28% of battles, nearly always enemy mages | **Leave / finish**: this is what the player sees |
| 5 | **Magic, campaign side**: spellbook, circle master, doctrines, the Tide, wells, rites, Omen, relic, workings and blight, court mages, cunning folk | ~900 | Player use 0; AI blight 30-40 provinces a year, which the player probably never notices | **Cut** the Tide, wells, doctrines, workings, Omen and relic. **Merge** spellbook and master into "hire a circle, it casts" |
| 6 | **Captain progression overlap**: skills (10) and perks (50), aura, forms, path, order rank, rank rewards, career rank, renown and fame, gear marks | n/a | About ten parallel tracks | **Merge** down to three: career rank (renown), skills and perks, aura. Fame folds into renown |
| 7 | **Named men**: faces, natures, ties, roll choices, legend, banner honours, rescue | 810 | ~200 named deaths a year; the payoff phases C5-C7 were never built | **Finish**: name far fewer men (so a death matters), then build C6 (named men in the fight). Natures and ties stay only if C6 uses them |
| 8 | **Lords and houses**: 150 houses, ~600 people, kin, feuds, bonds, wants, promises, talk topics, courting and marriage, grudges, painted portraits | 1,970 | Talk is used (50-110 a year); the rest isn't measured | **Hold** for PT6 (the playtester asked for Crusader Kings depth here). Decide with PT6, not before |
| 9 | **Companions**: callings, talents, bonds, stories, students | 310 | not measured | **Leave** (small) |
| 10 | **Gear as things**: quality, marks, baggage, smith, spoils, piece names | 1,040 | 19-25 pieces carried, a few marked, none named | **Leave** (finished, has a screen). Maybe fold the smith into the armourer tab |
| 11 | **The enemy notices and hunts the captain** | 400 | Battle-only, passive | **Leave**; check it in play |
| 12 | **Monsters**: goblins, wolves, trolls, spiders, bears, wyverns, undead; graves and burial | 1,080 | Goblins peak at 45-109 bands. Undead and wyverns are rare | **Trim**: goblin numbers are a balance bug. Keep three or four kinds that each fight differently |
| 13 | **Siegecraft**: engines, walls that break, siege works, field train | 980 | not measured (the stand-in storms seats) | **Leave** (new, coherent) |
| 14 | **Sergeants and divisions**: command cap and reserve tray, horn plans, questions, voice range, natures, couriers | 1,660 | Battle-side, not measured | **Trim** after PT4. Horn plans and questions are the first candidates; the PT2 audit already counts about 12 HUD widgets |
| 15 | **Battle quality of life**: merge, relieve, split, queued and standing orders, alerts, remembered deployment | 460 | Battle-side | **Trim**: they are part of why there are 13 order buttons. Move to PT4's "more" fold; likely cut split and merge |
| 16 | **Side content**: sites, caravans, town life, tournaments, duels, goods, elders' jobs | 800 | Sites and duels 0 (stand-in), tournaments 1-2 a year | **Leave** (cheap, gives the map texture) |
| 17 | **Settlement tabs** | n/a | A town has 12 tabs (up to 15 with chapter and fief); 18 tab kinds in all | **Merge**: comes for free once rows 2, 5 and 10 lose their tabs (Chapter, Workshop) |

## 4. Proposed merges, in more detail

### 4.1 One aura ladder

Today the aura has 6 tiers, 5 walls, 3 kinds of trial, 61 forms, 3 paths, your own form with aspects, the Black Flame and
order ranks, all on one track that a year of play takes to tier 1.

Proposed:
- **Four tiers (0-3)**, earned by fighting.
- The first wall breaks on its own (as AR1 already does). Later walls break with **one** trial each: a duel or holding
  against the odds.
- **A path choice at tier 1**: Sword, Banner or Hearth (Hearth only if magic survives in some form).
- **About 3-4 forms per path** instead of 61. Tier 4-5 gifts fold into tier 3.
- **Cut**: your own form and aspects, the flame forms, the order forms.

### 4.2 Magic you can see

Keep:
- caster troops you can hire, and enemy lords who bring them;
- the Will pool, the P-key ordered cast, and the warning ring;
- the spectacle visuals.

Cut:
- the Tide (a second pool on top of Will);
- wells and their marks, doctrines and Unweave;
- map workings and blight;
- Omen, the relic and most rites.

That removes a resource, a map layer and a hidden AI process that nobody can see.

### 4.3 Reputation

Renown (career) and Fame (legend) are two numbers for one idea. Merge them into renown.

## 5. Problems in how the work was done (so it doesn't happen again)

1. **Nothing plays most systems.** The stand-in uses about 50 campaign actions and none of the meta systems, so `--balance`
   and `--pacing` can't tell whether any of them are reachable or balanced.
   Proposal: commit the probe as `--census` and add one line per system to it. A system no automation can reach needs a
   reason to exist.
2. **"Unused" was answered with more systems.** Rule for this work: when something is unused, first make it **cheaper to
   reach or more visible**. Only then add to it.
3. **Plans stop before their payoff.** COMPANY.md C5-C7 (above). NOBILITY.md N3-N5 is planned with N1-N2 built.
   PLAYTEST.md PT5 sits at 6/10. WORLD_LIFE.md WL7 is paused.
   Rule: a plan's payoff phase goes first, or the plan isn't started.
4. **The docs drift.**
   - COMPANY.md still says "nothing built yet".
   - CLAUDE.md says ~17k lines.
   - `a1e19a0` (PT4) left the build broken until `082d516`. This session's checkout was on `a1e19a0` and didn't build, so I
     moved the dev branch onto `main`.

## 6. Notes for the world-map work (not acted on here)

- At peak about **420 brigand bands (+~60 robber knights) against 140 lords**, and **45-109 goblin bands**. Brigands make
  up nearly all of the 150 battles a year. The world feels like pest control rather than a war between realms.
- The stand-in fights lords in only a small share of battles (AR5's own note: 7-13 of ~110).

## 7. Decisions needed from the owner

1. **Knight orders, player side** (row 2): cut, keeping the orders as AI factions?
2. **Black Flame** (row 3): cut whole, or keep as one event?
3. **Aura** (4.1): cut down to four tiers with one path choice and about a dozen forms?
4. **Campaign magic** (4.2): cut the Tide, wells, doctrines, workings, Omen and relic?
5. **Named men** (row 7): fewer named men, then build "named men in the fight" (C6)?
6. **Lords** (row 8): hold until PT6 is designed, or decide now?
7. **Saves**: may cuts bump `CampaignState.CurrentVersion`? That makes old saves refuse to load, but keeps the cuts clean.

Once these are answered, the work goes in this order: the cuts (cheap, and they shrink everything after), then the
merges, then finishing (named men, PT4/PT5), then the census command.
