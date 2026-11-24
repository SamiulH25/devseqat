# Backlog: everything left to build

The one list of open work, gathered 2026-11-19 after the `sprite-improvements` merge (`02576fc`) and refreshed 2026-11-21
after the KIT work (KT0–KT4, KT6). Every plan that still has open phases is linked; open plans are in `docs/pre-demo/`, finished
plans in `docs/archive/` (index in `docs/archive/README.md`), and what waits for after the demo in `docs/post-demo/` (not listed
here). When an item ships, delete it here and write its "As built" note in its plan. When a plan's last phase ships, move the
plan to `docs/archive/`.

State at the time of writing: `main` builds, and the last full `--selftest` recorded in a plan ended `selftest ok` (KIT KT4:
322/322). Run `tools/check.sh` before starting work.

"Judged N/10" in older plans meant a visual judge. The owner dropped the judge on 2026-11-19: render the shots and look at
them yourself (CLAUDE.md step 4); the scores in old notes are history only.

> [!IMPORTANT]
> **Core design note (owner, 2026-11-23):** Right now there are a lot of systems in the game, **but there is no real reason for the player to ever interact with any of them**. New pre-demo work (`PROGRESSION_FEEL.md`, `CHARACTER_IMPACT.md`, `THE_WILD.md`, `EVENTS.md`, `SIEGES.md`) should actively pull players into those systems instead of adding isolated mechanics.

## Plans still open

| Plan | Open | Size | Notes |
|---|---|---|---|
| [`pre-demo/EVENTS.md`](pre-demo/EVENTS.md) | EV0-EV7: storylet format and cast, the director (moments, weights, pacing, forced queue), checks and outcome pages, the parchment book, port of code-raised events, vignettes, new content | large | Owner's brief 2026-11-22. Nothing built. Old `Data/events.json` set to be dropped. Three open questions at the end |
| [`pre-demo/SIEGES.md`](pre-demo/SIEGES.md) | SE1-SE10: the siege overhaul: the field built from the place's plan, a fortress to move through, capture points and the heart, layered defence, equipment and assault plans, sieges on the map (sallies, relief, terms, sack), every kind of place | large | Owner's brief 2026-11-21. Nothing built. Four open questions for the owner at the end of the plan |
| [`pre-demo/NOBILITY.md`](pre-demo/NOBILITY.md) | Done (N1-N5 shipped 2026-11-24) | — | Noble stock gate, castle esquires, knighting named veterans on the roll, AI lord noble retinues, TrainTab noble badge & copy |
| [`pre-demo/KIT.md`](pre-demo/KIT.md) | KT5 (price from kit) and KT7 (re-equip companies) only, both deferred until after the demo | — | **Not pre-demo work.** KT0–KT4 and KT6 built 2026-11-21 (`7cb10df`, `53b056e`, `471c168`); every man's numbers come from base + skills + worn kit |
| [`pre-demo/THE_WILD.md`](pre-demo/THE_WILD.md) | Done (WI1-WI9 shipped 2026-11-24) | — | Wildness, growing lairs, monster spoils & marks, contracts/hunts, 15 new monsters + high-poly Three.js models, legendary apexes, world crisis endgame |
| [`pre-demo/PROGRESSION_FEEL.md`](pre-demo/PROGRESSION_FEEL.md) | PF1-PF5: unified progression feel & character impact (Homeland != Sworn Sword, Total War scale, named men/camp/divisions in battle, arcane/order counters, origin on-ramps, tier jumps & sinks) | large | Owner's brief 2026-11-23 (merged `CHARACTER_IMPACT.md`). In progress |

## Left out of finished plans (pick up or drop)

- **Great beasts on the map** (2026-11-23, after auto-resolve learned to weigh them, `Rules.MightAgainst`; aim: 200-300 men beat
  six trolls, three wyverns or six ogres and lose a good part of themselves): AI lords alone almost never beat trolls (1 of 171
  clashes in `CampaignRun`) or wyverns (0 of 298), so those live on and keep mauling passing lords. Lords could hunt them by day
  in armies with bows, or beasts could tire and go to ground. The monster AI (`TrollThink`, `WyvernThink`, ...) still judges
  prey by the old `Might`.
- **The dressed map** (`archive/WORLD_DRESS.md` "Closed", 2026-11-21): WD5 (wild herds by land, gulls, waterfowl, storks, campfires), WD6 (cloud shadows, wind on grass, dawn mist, dust, blown snow), WD7 (every land per season, frame time at widest zoom).
- **Holds** (`archive/HOLDS.md` "Closed", 2026-11-21): HD6 world size (keep, slow travel, or scale `DesignScale`; run `--pacing` first) and HD7 (README, CAMPAIGN.md, tips and landing page numbers for castles, muster and holders).
- **Playtest fixes** (`archive/PLAYTEST.md` "Closed", 2026-11-21): PT5 (re-look at `pose-grips`, `pose-stances` and the clash gallery after the sprite overhaul) and PT6d (CK systems, waiting on the playtester).
- **Settlement models** (`archive/SETTLEMENTS.md` "Closed", 2026-11-21): SM7 (check and polish).
- **Magic and aura** (`archive/ARCANA.md` "As built"): a second shallow path and breakthroughs as choices (AR6); orders
  bound to paths; eight more doctrine spells (AR4); talking to court mages, rival circles with grudges (AR5); Omen per
  province, bless the walls (AR7); titles from workings and a master's staff as a relic (AR8). Pacing short of target:
  magic in 1 battle in 6 by day 120, Kindled by ~30 for a seeker, Aura Knight by ~90.
- **Siegecraft** (`archive/SIEGECRAFT.md`): engines never looked at on a real screen (tune sizes and colours); burning oil
  at the gate; an engineer troop line.
- **Officers** (`archive/OFFICERS.md`): the horn word in gold; "AWAITS HORN" beside "HORN: SWING" on a card.
- **World life** (`archive/WORLD_LIFE.md` WL7, paused, only if asked): rim light and shadow blob on men, tilt-shift and
  parchment edge on the map, thinner labels, palette per season.
- **Holds**: carts between a castle and its villages (drawing only).
- **The drawn world** (`archive/WORLDMAP.md`): peoples of one realm are hard to tell apart (horse troops most); top-tier foot
  close to the tier below; crossbows read as a blob. Re-check after the sprite overhaul.
- **The company** (`archive/COMPANY.md` "As built", closed 2026-11-21): C9b gear slots (hands, legs, cloak, harness,
  keepsakes), ~90 graded culture pieces and their `Gear.Validate` checks, renown gating, the Named piece on the portrait line and
  end summary; C10b companions and named men wearing baggage gear, N cycling up to three weapons, the armourer on the kit grid,
  a link from the K sheet's doll, sprite parts in the kit cells; C11b monster-leader trophies and recipes, culture lord drops,
  Named spoils from grand masters and kings, the smith's days for masterwork, gifts (ruins, order, liege, want, caravan,
  wedding), sets, the pilot's gear spend; C13 a lord's offer of single combat in the field, brigand kings and monster leaders
  hunting by `Prey`, a deed for a named man who saves you, the report's line; C3 rivals earning names faster, the lad's odds
  from his master's.
- **Ships**: encounters at sea; AI parties never sail.
- **Saves** (`SAVES.md` "Not done"): cloud sync, import/export, a captain portrait on the card, a picture for the
  battle-start autosave.
- **Sprites** (`archive/SPRITE_HERO_SET.md`): the open question of 8 directions (three-quarter front and back views,
  `.q`/`.r`) is still the owner's call. Creatures, gilded and fur variants were left for a later pass.

## Code health (shape, not defects)

[`CODE_HEALTH.md`](CODE_HEALTH.md) (2026-11-22): the codebase is 349 files and 97,933 lines of C# (11,797 of them the
selftest). Targets there, in order: delete the stale `.claude/worktrees/` copies (2.1 GB, and they make any line count
wrong); move the ~1,500 lines of shot staging out of `ScreenHost`/`MapScreen`/`BattleScreen` into a `Shots/` folder;
mechanical partial splits; name the steps inside `Puppet3D.TryDraw` (479 lines) and `CampaignBattle.Apply` (219);
`TroopIds` consts for the 1,776 troop-id literals; dispatch tables for the `switch (kind)` painters; parameter structs;
then break the town screens out of `MapScreen` (32 partials, 12,626 lines).

## Code debt (from the 2026-11-17 audit, `archive/ISSUES.md`)

Fixed 2026-11-19: couriers removed (5); the AI's skirmishers Screen and Harass (6, test `SkirmishersRead`); dead methods
deleted, companions can be moved between companies (click one on the household tab), the stand-in takes order trials
(`AutoTrial` in `Pilot.Visit`), `Aura.TrialsFor` deleted as `NearestTrials` already lists every trial (8). Closed with no
change: the trace helpers are debug runs (listed in DEVELOPING.md 9) (7); F1 help is already grouped by view (13); the
"pre-PT4 hooks" in `Hud.cs` are the live goal lines, form bar, spell fan and officer panel (14).

| # | Issue | Fix |
|---|---|---|
| 11 | Two landing pages: `docs/index.html` and `site/` (tracked, with 8 screenshots) | Keep `docs/index.html`, delete `site/` (owner to confirm) |
| 12 | `BlenderMCP/customer_uuid.txt` is tracked (a per-machine id) | `git rm --cached` it and ignore it |

## After the sprite merge

The `sprite-improvements` branch changed rules as well as art; these are now documented (README, DEVELOPING.md) but want a
look in play:

- **Top tier is VI** (`Troops.MaxTier = 6`, `Troops.Validate` refuses higher): every realm line is six troops.
  Passives now start at tier IV (0.7), V has one full and one part, VI all three with the culture's signature; drill and
  `Rules.TierWeight` re-spread over six tiers. Run `--balance`, `--pacing` and `--agree` to see what moved.
- **The captain's armour by material**: `body x quality + helm x quality` (`Rules.CaptainType`), no longer added on top of
  the base troop's armour. The captain starts in a gambeson, kettle hat and round shield, and gear progresses from there.
- **Castles sell gear up to 900 crowns** (villages 300, towns everything).
- **Death**: fallen men and horses rise as team-coloured wisps and are gone after ~4 s (no bodies left on the field).
  Check this against the crows over the dead (`BattleScreen.Life.cs` `Carrion`) and the arrows left standing.
- **Landing page** (`docs/index.html`): its tier strip and realm lines still show tier VII (the `TIERS` array and each
  realm's `lines`). Bring it to six tiers with the HD7 landing-page pass.
- **Untracked files** from that work: `Data/sprites/test_character/`, `tools/render_test_character*.py`,
  `tools/compose_puppet_showcase.py`. Commit them as tools or delete them.
