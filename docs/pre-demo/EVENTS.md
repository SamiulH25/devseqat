# Events: the rework (EV0-EV7)

Owner's brief, 2026-11-22: the events system is legacy and gets a complete overhaul: how events are triggered, how they look,
and what each choice does. Owner's calls on direction:

- **Triggers:** storylets with a pacing director (several conditions and a weight per event; a director spaces them and
  prefers what fits the moment; forced events queue instead of being dropped).
- **Choices:** checks with an outcome page (a choice may test a skill, trait, companion or named man, odds shown; the outcome
  is a second page with what happened and the real effects; some consequences hinted, not spelled out; choices gated by more
  than gold).
- **Look:** a parchment book over the dimmed map (the `UiScroll` scroll), an illustrated vignette at the head, prose in mixed case.
- **Content:** rewritten from scratch. The 119 events in `Data/events.json` go; a new, smaller, stronger set is written in the
  Roven voice (`docs/LORE.md`).

## What is there now (and why it goes)

| Part | Now | Problem |
|---|---|---|
| Trigger | `EventsDaily`: every 3-6 days (`EventGapMin/Max`), one event drawn at even odds from all whose **one** trigger string holds (`Find`) | No weights, no combined conditions, no sense of the moment; the same few broad triggers (`forest`, `night`, `after_battle`) crowd out the rest |
| Forced events | `Force(id, pe)` | Dropped if another is waiting (a pretender or peace offer can be lost) |
| Chains | `flag:name` effect, the `flag:name` event a fixed 7 days later | One fixed delay, no branching on what you chose, no memory of choices |
| Choice | a flat list of effect strings, all shown as chips, applied exactly | A sum to compare, not a decision; only `need: gold:N` gates; only `gamble` has chance |
| Result | one line in the log | You never see what happened |
| Subjects | `PendingEvent` holds ids for a fixed set of holes | Hard to add a cast; holes resolved by hand in `Fill` |
| Look | a small modal (`MapScreen.Event.cs`): silhouette scene, all-caps text, chips | Reads as a pop-up, not a story |

Code that raises events by id (all must keep working through the rework, then move to the new ids): `Force` in
`Campaign/Events.cs` (`brigand_king`, `pretender`, `free_crown`), `Diplomacy.cs` (`peace_offer`), `Undead.cs` (`bury_dead`),
`BlackFlame.cs` (`flame_bargain`, `flame_end`), `Aura.cs` (`aura_vigil`, `aura_grief`), `Sites.cs` (`site_*`), `Remembered.cs`
(`cairn`, `men_day_fell`), `CampEvents.cs` (`men_*`, `MenEventsDaily`), `Roll.cs` (`bury_named`), origins (`origin:<id>`),
`start_road`, the rival chain (`flag:rival_meet`, `flag:rival_fallen`). Readers: `Pilot.cs` (`EventOf0`), `Playtest.cs`,
`BalanceReport.cs`, `ScreenHost` shots, and about 100 test lines across `Tests/*` that name event ids.

## The new model

### A storylet (data)

One file per theme in `Data/events/*.json` (road, camp, court, town, fief, war, faith, sites, flame, origins, men...), loaded
together. A storylet:

```jsonc
{
  "id": "ford_toll",
  "moment": ["road"],                 // when it may come up (EV1)
  "when": ["near:river", "!at_war:local", "gold>=40", "season!=winter"],   // all must hold
  "cast": {                           // who and what it is about, bound when it fires
    "lord": { "pick": "lord", "where": ["near", "opinion<20"] },
    "man":  { "pick": "named", "where": ["nature:bold"], "optional": true }
  },
  "weight": 3, "cooldown": 40, "once": false,
  "scene": "ford",                    // the vignette (EV4)
  "title": "The Toll at the Ford",
  "text": "{lord}'s men have chained a boat across the ford and want a crown a head.",
  "choices": [
    { "label": "Pay the toll", "cost": ["gold:men*1"], "show": ["gold"],
      "outcome": { "text": "You pay. {lord}'s reeve counts every coin twice.", "do": ["opinion:lord:+3"] } },
    { "label": "Wade the shallows upstream", "check": { "skill": "Logistics", "against": 40 },
      "pass": { "text": "...", "do": ["hours:+6"] },
      "fail": { "text": "...", "do": ["horses:-2", "sick:3"], "hint": "The water is high." } },
    { "label": "{man} offers to cut the chain", "needs": ["cast:man"],
      "check": { "man": "man", "trait": "bold", "against": 50 },
      "pass": { "text": "...", "do": ["man_deed", "opinion:lord:-10"], "then": { "id": "ford_reprisal", "days": [10, 20] } },
      "fail": { "text": "...", "do": ["man_hurt"] } }
  ]
}
```

- **Conditions** (`when`, `where`, `needs`) are a small typed language: `key`, `!key`, `key:arg`, `key<op>number`. Each key is
  one C# predicate in a table (`EventConditions`), validated at load. They cover place (province kind, near river/site/sea, in
  a settlement and its kind and culture), time (season, hour, day), party (gold, food, men, wounded, horses, cohesion, unpaid,
  prisoners, omen), captain (traits, skills, rank, renown, path, order, corruption, origin), world (at war, exhaustion, fiefs and
  their state), memory (`flag:x`, `chose:event.choice`, `seen:event`, `days_since:event`).
- **Cast** binds roles (lord, companion, named man, settlement, site, band, company, realm) by filter; one role per hole in the
  text. Replaces `PendingEvent`'s fixed ids and `Fill`'s hand-written holes with a role -> id map.
- **Choices** may have `needs` (shown greyed with the reason), `cost` (paid on choosing, shown), and either one `outcome` or a
  `check` with `pass`/`fail` (and optionally `great`/`awful` at the extremes). An outcome has text, effects (`do`), a `hint`
  shown before choosing, and `then`: a follow-up storylet after a day range, optionally only if a condition holds.
- **Effects** (`do`) are typed verbs aimed at a role (`opinion:lord:+3`, `man_hurt:man`): the old verbs carried over
  where they still make sense, plus new ones: item (gain/lose/batter a piece), buff (an effect from effects.json for N days),
  wound on a named man, relation between two lords, quest (open a contract), chain (`then`), remember (`flag`, with a value).
  Each verb is one entry in a table (`EventVerbs`) with its validator and its line for the outcome page.

### The director (when one comes up)

- **Moments:** the code calls `EventMoment(kind)` when something happens: `road` (an hour of travel, rolled), `camp` (you make
  camp or rest at night), `arrive` (you reach a settlement or site), `after_battle`, `province` (you cross into a new one),
  `town` (you linger in a settlement), `week` (the weekly tick, for the fief and court). A storylet lists the moments it may
  come at.
- **Picking:** among storylets that fit the moment and whose `when` and cast hold, odds = weight x freshness (lower for
  storylets and themes seen recently) x fit (higher for the more specific: more conditions met counts as a better fit).
- **Pacing:** a budget, not a fixed gap: a quiet stretch after each event (longer after a heavy one), a weekly cap, and a
  rising pull when nothing has happened for a while. Numbers in `Balance` (`Data/campaign.json`).
- **Forced events** go into a queue with a priority and an expiry (the pretender waits; a peace offer lapses when the war ends).
  The queue drains before anything is drawn. Code that raised by id now calls `QueueEvent(id, cast, priority, expiresIn)`.
- **Memory:** what you chose in each event is kept (`S.EventHistory`: id, choice, outcome, day), read by `chose:` and `seen:`.

### The book (how it looks)

- Opens over the dimmed map as the `UiScroll` parchment unrolling; the map pauses.
- **Head:** a painted vignette (wider and richer than today's scenes: the camp, the road, a ford, a hall, a village, a burning
  village, a site per kind, a cairn, the Flame...), with the cast's faces in frames on it (lords via `PortraitPainter`, named
  men via `OfMan`).
- **Body:** title as a ruled heading, prose in mixed case in the ink colour, holes filled with names in a stronger ink.
- **Choices:** inked lines with a numeral. Beside each: its cost (crowns, food, men), and for a check its odds as a word and a
  bar (Sure / Likely / Even / Unlikely / Desperate) with what it tests (`ARMS`, `Gib the Stern`, `Pious`). Hints in italic
  under it. A blocked choice is struck through with the reason.
- **The outcome page:** choosing turns the page (the scroll rolls up and unrolls again): the outcome text, then what changed as
  small pictured lines (icon, words, signed number), then CONTINUE. A follow-up is noted ("This is not the end of it.").
- Keys: 1-3 pick, Enter continues; mouse throughout.
- The chronicle (`Record.cs`) gets the event's title and the outcome's first sentence.

## Phases

| Phase | What | Done when |
|---|---|---|
| **EV0** | The storylet format and loader (`Data/events/*.json`), condition and verb tables, cast binding, the validator; the new engine runs alongside the old one, nothing in data yet but test storylets | `--selftest EventsValidate` checks the format; test storylets bind casts and apply effects in a test |
| **EV1** | The director: moments hooked into the campaign tick and actions, weights, freshness, pacing budget, the forced queue, event history in the save; the old `EventsDaily` lottery switched off | A test runs 200 campaign days and checks spacing, the weekly cap, queue drain, no event starved; `--events N` report (how often each fired, which never could) |
| **EV2** | Choices: needs, costs, checks (odds from skill, trait, man's nature and drill, companion's calling), pass/fail/great/awful, hints, `then` chains with day ranges and conditions | Tests for odds (the shown odds match the rolled rate over 10k rolls), chains, needs and costs |
| **EV3** | The book: the scroll, vignette and faces, choice lines with odds and costs, the outcome page, keys; shots `campaign-event-*.png` (opening, a check, an outcome, a blocked choice, a camp event with two faces) | Shots checked; `overflow.txt` empty for event shots |
| **EV4** | Port every code-raised event to the queue and the new format: origins, start road, sites, cairns, burial, Black Flame, aura vigil and grief, the rival chain, peace offer, brigand king, pretender, crown, the camp's men events (`MenEventsDaily` becomes the `camp` moment); tests updated to the new ids; the stand-in captain (`Pilot`) and the playtest harness choose through the new API | All those systems' tests pass on the new engine; old `Events.cs` engine and `Data/events.json` deleted |
| **EV5** | Vignettes: the painted scenes the content needs (ford, road, hall, market, shrine and the faiths, ship, siege camp, the Unkept), drawing only | Shots per scene checked |
| **EV6** | Content: the new set, written from scratch in the Roven voice (`docs/LORE.md`, docs/DESIGN.md "Voice"; the `llm-writing` skill before writing). Target ~80 storylets at the start: per moment, per realm and faith, per party state, several chains of 3-5 steps, each with at least one check; every verb used | `--events 20` shows every storylet reachable and none dominant; a read-through of the set |
| **EV7** | Balance and polish: rewards against `Balance` scales (`Rules.PerMan`, `Scaled`), pacing numbers tuned from `--events` and the playtest fleet, docs (CLAUDE.md map, DEVELOPING.md, README rules, recipe "Add an event") | `tools/check.sh` green; plan archived |

Order: EV0 -> EV1 -> EV2 -> EV3 can run beside EV1-EV2 once the format is fixed -> EV4 (the switch) -> EV5 and EV6 together -> EV7.
Between EV1 and EV4 both engines exist; nothing new is written for the old one.

## Decisions and gotchas

- **Saves:** `S.Event` (a `PendingEvent`) becomes the new pending storylet (id, cast, page); `S.EventsFired`, `S.NextEvent`,
  `S.Flags` stay readable. No version bump: an old save's pending event with an id the new set lacks is dropped on load.
- **Dice:** checks roll on the campaign `rng`; the director on its own stream (`Rng.Of(seed, "events")`) so adding a storylet
  does not shift battle seeds more than it must. Never `HashCode.Combine`.
- **Headless:** the engine, director and checks live in `Campaign/` and stay free of MonoGame graphics; the book is `MapScreen.Event.cs`.
- **Odds shown are the odds rolled.** The word and the bar come from the same number the roll uses.
- **Hidden is not random:** a fail's effects may be hinted rather than shown, but never contradict the hint.
- **The camp's events** (C5) keep their men, deeds and ties; they become storylets with a `man`/`other` cast at the `camp` moment.

## Open questions for the owner

1. Should the map keep running under a non-urgent event (a toast you open when you like), or always pause? (Plan: always pause.)
2. Roughly how often should an event come in a normal week of play? (Plan: about two, fewer on long marches through empty land.)
3. Any events from the old set worth keeping in spirit (the start road, the burial, the cairn visit, the rival chain)?

Answered by default (2026-11-22, not yet confirmed by the owner): always pause; about two a week; the start road, burials, the
cairn, the day they fell and the rival chain kept in spirit, rewritten.

## As built (2026-11-22)

All phases EV0-EV7 are in; the old engine (`EventsDaily`, `Find`, triggers, `MenEventsDaily`) and `Data/events.json` are gone.

- **Data:** 117 storylets in 14 theme files under `Data/events/` (road, camp, court, fief, war, faith, wild, town, sites, origins,
  traits, aura, fallen, world). 28 have a check. Every verb in `Campaign.Verbs` is used; `EventsValidate` holds it to that.
- **Engine:** `Events.cs` (format, loader, validator, `Choose` -> `EventOutcome`), `EventConditions.cs` (conditions, cast
  conditions, odds), `EventVerbs.cs` (verbs, page lines, effects), `EventDirector.cs` (moments, draw, queue, follow-ups, cast
  binding). Brigand kings and pretenders moved to `Threats.cs` unchanged. Events roll on their own dice (`Rng.Events`,
  `CampaignState.EventRng`), not the campaign `rng` as the plan said: checks and draws then never shift battle seeds.
- **Pacing:** the weekly cap (`EventWeekCap`) counts only drawn events; events the world raises (burials, graves, envoys, kings,
  sites, follow-ups) do not use it up, or a company that fights often would see nothing else. A named man's burial comes at most
  once a week and only when nothing else waits (it used to come after every won fight with named dead).
- **Numbers** (`--events 4`, the stand-in captain, 240 days): about 3 events a week, 2 of them drawn; the most frequent are the
  burials (17% and 13%: the stand-in fights far more than a player) and the day they fell (7%); 39 checks passed and 9 failed. 46
  of 98 drawn storylets never came up for it: they wait on states it never reaches (an origin from the creator, a site visited,
  an earned trait, omen from the craft, a poor or restless fief). Pacing: quiet 1.5-3 days, road 0.06, camp 0.5 an evening.
- **Checks:** a skill scores 20 + the skill (`SkillScoreBase`), so a new captain's 5-15 makes a check against 35 UNLIKELY to EVEN and
  training moves it up; a named man 25 + 4 a battle (to 12) + 5 a mention - 8 a wound + 20 for a matching nature; a companion
  40 + 5 a level. `EventOddsMatch` rolls 4000 times and checks the rate against the odds shown.
- **The book:** `MapScreen.Event.cs`. The scroll unrolls anew for each page; the vignette (new scenes: road, ford, forest, hills,
  sea, village, field, the Flame) with the cast's faces (named men, lords, companions); prose in mixed case through a lowercase
  set added to the pixel font (`PixelFont.Lower`, `DrawMixed`, `Ui.InkProse`; nothing else in the game writes lowercase); choices
  with the cost at the right, TESTS <skill> with an odds bar and word, the hint, or the reason struck through. Keys 1-9 choose,
  Enter or Space continues. Shots: `IRONFIELD_EVENTS_ONLY=1`.
- **Deviation:** names in the prose are not set in a stronger ink (the font has one weight).
- **Harness and stand-in:** `Playtest.cs` `event` lists costs, checks with odds and hints; `choose` prints the outcome page.
  `Pilot` answers with the first choice it can take and clears the page.
- **Old saves:** a waiting event whose id the new set lacks is dropped when the save loads.
