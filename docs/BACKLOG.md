# Backlog: everything left to build

The one list of open work, gathered 2026-11-19 after the `sprite-improvements` merge (`02576fc`). Every plan that still
has open phases is linked; finished plans are in `docs/archive/` (index in `docs/archive/README.md`). When an item ships,
delete it here and write its "As built" note in its plan. When a plan's last phase ships, move the plan to `docs/archive/`.

State at the time of writing: `tools/check.sh --quick` passes and the full `--selftest` ends `selftest ok` (every test green).

"Judged N/10" in older plans meant a visual judge. The owner dropped the judge on 2026-11-19: render the shots and look at
them yourself (CLAUDE.md step 4); the scores in old notes are history only.

## Plans still open

| Plan | Open | Size | Notes |
|---|---|---|---|
| [`HOLDS.md`](HOLDS.md) | HD6 world size; HD7 docs and polish | decision + small | HD6 is the owner's call: (a) keep, (b) slow travel, (c) scale `DesignScale`. Run `--pacing` first. HD7: README, CAMPAIGN.md, tips, landing page numbers for castles, muster and holders |
| [`WORLD_DRESS.md`](WORLD_DRESS.md) | WD5 life in every land; WD6 air and weather; WD7 check and tune | medium | WD5: wild herds by land, gulls, waterfowl, storks, campfires. WD6: cloud shadows, wind on grass, dawn mist, dust, blown snow. WD7: every land per season, frame time at widest zoom |
| [`COMPANY.md`](COMPANY.md) | C5 the camp; C6 named men in the fight; C7 what is remembered; C8 polish | large | C5 is the big one (a screen, camp events with a `men` trigger, rites move there). C7 has the opt-in captain death (LEGACY L7) |
| [`NOBILITY.md`](NOBILITY.md) | N3 knighting a named man; N4 AI lords' noble retinues and UI badge; N5 balance | medium | Gap: northern, southern, steppe and league have no non-order noble T4 |
| [`PLAYTEST.md`](PLAYTEST.md) | PT5 weapons that read as weapons; PT6d CK systems | medium; design | PT5: re-look at `pose-grips`, `pose-stances` and the clash gallery after the sprite overhaul (it redrew most weapons) and close it or list what is still wrong. PT6d waits on the playtester |

## Left out of finished plans (pick up or drop)

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
- **Ships**: encounters at sea; AI parties never sail.
- **Saves** (`SAVES.md` "Not done"): cloud sync, import/export, a captain portrait on the card, a picture for the
  battle-start autosave.
- **Sprites** (`archive/SPRITE_HERO_SET.md`): the open question of 8 directions (three-quarter front and back views,
  `.q`/`.r`) is still the owner's call. Creatures, gilded and fur variants were left for a later pass.

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
