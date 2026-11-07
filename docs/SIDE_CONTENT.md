# Side content, polish and a fuller world: plan

Status: **planned, not started.** This plan covers what a player does between the big wars: places to find on the map,
small quests, life on the roads and in towns, many more events, and a polish pass on the campaign screens. The main
loop (companies, career, fiefs, war, creatures) is built (`CAMPAIGN_OVERHAUL.md`, `CREATURES.md`) and is not changed here.
Numbers marked *(tune)* are starting values.

## Where the world stands (2026-11-07)

Found by reading the code and the staged campaign shots (`IRONFIELD_CAMPAIGN_SHOTS`):

- **Names repeat.** `Data/names.json` has 60 place names, and `WorldMap.Generate` hands them out with `nameAt % count` to
  about 60 provinces *and* about 180 settlements. Each name is used three or four times on one map. Lords draw from 26
  first names and 14 epithets (no culture split), bandits from 8 band names, sergeants from the lord list.
- **The land between towns is empty.** The map has provinces of four kinds (Meadow, Farmland, Forest, Hills), settlements,
  roads, two rivers and the mountain rim. Apart from parties there is nothing to ride to: no ruins, shrines, mines or
  old battlefields.
- **Villages have nothing to ask.** Quests come from the town steward's board (`hunt`, `clear`, `defend`, `scout`, `take`)
  and from friendly lords (`hunt` or `clear`). There are no escort, delivery, rescue or monster-hunt jobs, and no chains.
- **16 events** in `Data/events.json`. Most trigger on fiefs, lords or company state; none on the season, the ground,
  a place on the map or a companion.
- **Roads carry only soldiers.** Lords, bandits, garrisons and monsters move; no merchants, pilgrims or peasants.
- **Towns are menus.** Tabs (train, recruit, mercenaries, market, food, rest, captives, contract) work, but most are a
  short list on a large empty panel, and the town has no voice: no rumours, no tavern, no flavour line.
- **Map clutter.** At the default zoom, party and settlement labels overlap into an unreadable stack (see
  `campaign-map.png`).
- **Loose ends** listed as "Not yet" in `ROADMAP.md`: escort and deliver-ransom quests, AI realms offering you peace,
  AI alliances, the tier II village recruit at high prosperity, the Scout's wider vision. From `CREATURES.md` phase 6:
  a lone named troll, a troll bridge lair, spiders, a ruin battle ground.

## Goals

1. **Every ride passes something.** Between any two towns there is usually a place worth a detour.
2. **Small choices all the time.** Something to do in a slow week that pays a little gold, renown, opinion or a man.
3. **The world has a voice.** Unique names, rumours that point at real things on the map, events that fit the season and place.
4. **Readable.** The map and town screens show what matters at a glance.

### Out of scope
Trade goods and a market economy (caravans move prosperity, not goods), naval travel, marriage, new realms or cultures,
first-person exploration of sites (a site is a dialog and maybe a battle, not a level).

## Phases

Each phase is playable on its own, ends with `tools/check.sh` green, and is committed with its tests. Sizes: **S** about a
day, **M** a few days.

### S1. Names and identity (S)
- Grow `names.json`: per-culture place pools (Azure, Crimson, Northern, about 80 each) and per-culture lord names, plus
  more epithets and band names. Fall back to a generated name (prefix + suffix pools: "Ash" + "ford") when a pool runs out,
  so no name repeats on one map.
- Provinces get a name of their own kind ("the Greywater Hills", "Oakhollow Forest") from the main settlement.
- Bandit bands, goblin holdouts and sergeants draw from their own pools.
- **Done when:** a new map has no repeated settlement or province name (test), and lords of one realm sound alike.
- Save note: settlements and provinces are stored in the save, so old campaigns keep their names; only new maps change.

### S2. Places on the map: sites (M)
A **site** is a fixed point of interest generated from the seed with the map (like rivers), so it costs the save nothing
but its state (visited, emptied, cooldown).

| Site | Where | On a visit |
|---|---|---|
| Ruin | Hills, Forest | Search it: an event with a find (gold, a relic, an old banner), or goblins move in (a holdout) |
| Shrine | Meadow, Farmland, near roads | Pray: cohesion and morale for a week; once a season |
| Standing stones | Hills | Rest a night: an omen event (good or bad), more often on undead nights |
| Old battlefield | Anywhere a great battle was fought, plus a few at start | Graves that can rise (`Campaign/Undead.cs`); scavenge gear |
| Mine | Hills | Hire miners (sappers later), or clear it of a troll; its province's town gets income while it is safe |
| Hermit's hut | Forest | Advice: a skill point once, or a rumour |
| Watchtower | Borders | Climb it: see parties in a wide radius for a day (gives the Scout skill a use) |
| Spring / mill / ferry | Rivers and roads | Small heal, food, a named crossing |

- Code: `Site` record and generation in `Campaign/WorldMap.cs`; visit logic in a new `Campaign/Sites.cs`; state in
  `CampaignState.Sites` (id -> visited/emptied/until). Meshes in `World.cs` from existing parts (towers, walls, stones).
  Map label, tooltip and an enter dialog reusing the event panel (`MapScreen.Event.cs`).
- About 1.5 sites per province *(tune)*, placed off roads and away from settlements.
- **Done when:** sites appear in the map shot, each kind has a visit test, and the stand-in captain (`Pilot.cs`) visits
  them without breaking `--pacing` targets.

### S3. More quests (M)
New `Contract.Kind`s, reusing the contract machinery in `CampaignWar.cs`:
- **Village elders** (new, villages): *hunt the wolves* (a nearby pack), *drive off the goblins* (a raiding band),
  *bring grain* (food to the village before a date), *train our militia* (stay a day, lend a sergeant).
- **Town steward:** *escort a caravan* (needs S4), *deliver a ransom* (carry gold to an enemy town under truce),
  *rescue a captive* (a lord or merchant held by bandits).
- **Lords:** *escort*, *deliver ransom*, *take a castle for me*, and *recover my father's sword* from a ruin (S2).
- **Chains:** a contract may name a `Next` step, so a quest can run two or three steps (a rumour, a ruin, a lair).
- **Done when:** every kind can be offered, completed and failed in a test; the board shows two to four jobs most days.

### S4. Life on the roads (M)
New `PartyKind`s that are not soldiers:
- **Caravans** run between towns of a realm along roads; a town they reach gains prosperity, and a raided one loses it.
  Bandits and goblin bands hunt them; lords at war raid enemy caravans. You can escort (S3) or raid them (gold, but the
  realm's opinion falls and its lords hunt you).
- **Pilgrims** walk to shrines (S2); they are harmless and give rumours.
- **Peasants** go from villages to their town on market days; when a village is threatened they flee to the town.
- These parties are light and few *(tune: one caravan per two towns)*, so the hourly tick and `CampaignRun` stay fast.
- **Done when:** a 240-day headless run keeps the `CampaignRun` pressure line within its current range, and a caravan
  reaching a town moves its prosperity (test).

### S5. Town life (M)
- **Tavern tab:** the wanderer (moved here from the mercenaries tab), a mercenary captain's offer, and
  **rumours**: two or three lines that name real things on the map ("goblins in the Lindow hills", "a caravan leaves
  Netherby for Dunmere in two days", "the ruin at Coldharbour is said to hold a king's sword"). A rumour can mark the place
  on the map.
- **Tournaments as fights:** the `tournament` event becomes a short real battle (your captain and a few men against
  rival teams, blunt weapons: wounds, no deaths), with a purse and renown. Uses `Battle.Empty` plus a flag in `BattleOptions`.
- **A flavour line** per town from its culture, prosperity, unrest and owner, under the town header.
- **Empty states:** a tab with nothing to show says why and what to do ("No mercenaries this week. They come back on
  Monday.") instead of a blank panel (the mercenaries tab already does this; the others do not).
- **Done when:** the town shot shows the tavern with rumours, and a tournament can be played and won in a test.

### S6. Many more events (M)
- Grow `events.json` from 16 to about 50. New triggers in `Campaign.Find`: `season` (spring floods, harvest, first snow),
  `ground` (forest, hills, river crossing), `near_site`, `after_battle` (wounded, a looted standard, a deserter),
  `companion` (a companion's trait or opinion), `culture` (a village of another culture), `night`.
- New effect verbs where needed in `ApplyEffect`: `man` (a recruit joins), `rumour`, `site` (reveal or empty a site),
  `flag` / `if_flag` for simple chains (an event that follows up weeks later).
- Each event fits the voice in `docs/DESIGN.md`: short plain sentences, two or three choices with clear costs.
- **Done when:** `SelfTest.EventsAndQuests` validates all of them, and a 240-day run fires a varied spread (no event
  more than 15% of all fired).

### S7. Polish pass (M)
- **Map labels:** declutter by priority (you, your target, towns, lords, then bands and monsters), merge stacked labels,
  hide minor labels when zoomed out. Check with `campaign-map.png`.
- **Town and party panels:** fill or shrink empty space; consistent tooltips on every number.
- **Chronicle:** the realm tab's chronicle also records your deeds (sites found, quests, tournaments).
- **End of campaign:** a summary screen (days, battles, best company, deeds) on win or loss.
- **Loose ends** from `ROADMAP.md`: tier II village recruits at high prosperity, AI realms offering you peace, the Scout's
  wider vision (with the watchtower). AI alliances stay out unless play shows they are missed.
- **Done when:** the staged shots read clearly at 1280x720, and each loose end has a test or a staged shot.

### S8. Creatures in the world (later, optional)
From `CREATURES.md` phase 6, tied to sites: a lone named troll under a bridge (a ferry/bridge site), spiders in deep
forest ruins, a ruin battle ground (walls and rubble) for fights at ruins and holdouts.

## Recommended order
S1 first (small, fixes something wrong today). Then S2 sites, because S3 quests, S5 rumours and S6 events all point at
them. Then S3 and S4 together (escort needs caravans), S5, S6, and S7 last so the polish covers the new screens.

## Testing (added to `--selftest` step by step)
- **S1:** no repeated settlement or province name on three seeds.
- **S2:** sites never sit on a settlement or road; every kind can be visited; state round-trips through a save.
- **S3:** every quest kind can be offered, completed and failed; chains advance.
- **S4:** caravans move prosperity; `CampaignRun` stays in range; determinism holds.
- **S5:** rumours name things that exist; a tournament battle ends with no deaths.
- **S6:** every event parses and validates; spread check on a long run.
- Every step: save/load/save round trip, `ReplayExact`, and `--pacing` still near its targets.

## Open questions (proposals in brackets)
1. **Caravans and trade:** caravans move prosperity only [yes], or add trade goods and buying low, selling high?
2. **Raiding caravans:** allowed for the player [yes, with a realm opinion cost and lords who hunt you], or not at all?
3. **Tournaments:** a real small battle [yes], or keep the current gamble?
4. **Saves:** keep version 4 and add fields with defaults [yes; old saves load and simply have no sites visited].
5. **Size of the world's population:** how busy should roads feel? [about one non-soldier party for every two soldier
   parties, tuned for speed.]
