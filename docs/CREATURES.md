# Creatures and monsters: brainstorm and plan

Status: brainstorm (branch `creature-work`). Nothing here is built yet. Names are placeholders.

The world is a fantasy world. Today every enemy is a man: lords, bandits, garrisons. This plan adds creatures and
monsters as new enemies, new kinds of map encounter and, later, new troops for the player.

## Design goals

1. **Each creature family plays differently from men.** A monster that is just a man with more hit points adds nothing.
   Each family gets one or two battle rules of its own (see the roster).
2. **Use what the game already has.** Day and night, weather, map terrain kinds, leaders and sergeants, bounties,
   mercenaries, events, fiefs. Monsters should hook into these rather than sit beside them.
3. **The wilds are dangerous.** Forests and hills should feel different from farmland. Clearing them should matter
   to your fiefs and your purse.
4. **Headless and deterministic.** Creature AI and combat live in the headless sim, use the battle `rng` only, and
   must pass `ReplayExact`.

## Roster ideas (by family)

| Family | Examples | Battle rule that sets them apart | Where on the map |
|---|---|---|---|
| Beasts | wolves, dire wolves, wild boars, cave bears | No formation: they hunt in packs, circle and go for flanks and stragglers. The pack breaks if its alpha dies. Bears knock men down. | Forest, hills |
| Goblin-kin | goblins, hobgoblins, wolf riders | Swarm: many weak, fast, cowardly fighters. Ambush from cover; flee early but come back. Riders use a wolf instead of a horse. | Hills, forest edges |
| Trolls and giants | forest troll, hill giant, ogre | One huge body. A sweeping blow hits several men at once and throws them back. Trolls heal unless kept under attack; they turn slow and weak by day. | Hills, deep forest |
| Undead | skeletons, ghouls, a necromancer | Never rout; cause fear. Skeletons shrug off arrows. Kill the necromancer and the rest fall apart (like a leader or sergeant falling, but final). | Old battlefields, at night |
| Crawlers | giant spiders, swarms | Webs slow men in an area. A bite poisons (damage over time). | Deep forest |
| Fey and spirits | wisps, wild hunt riders | Come with fog. Hard to see and target; drain morale more than health. | Forest, in fog |
| Drakes (late) | wyvern, young dragon | Flies: swoops in like a cavalry charge from the air, then climbs away. Arrows matter. | Mountains (later) |

Shared creature traits, as data on the troop (so most of them need no new code once the trait exists):
`size` (scale and collision radius), `body` (human, beast, giant, swarm), `fear` (morale hit around it),
`fearless` (never routs), `pierceResist`, `regen`, `sweep` (hits several), `knockback`, `poison`, `daylight`
(weaker by day), `boundTo` (dies when its master dies). Monster-hunter troops can get a `vsMonster` bonus, the same way
`vsMounted` works now.

## New kinds of encounter

1. **Roaming packs.** A new `PartyKind.Monster` that roams the wilds like bandits do. Beasts hunt weak parties and
   avoid towns. They spawn by terrain kind (wolves in forest, goblins in hills).
2. **Lairs.** A fixed site on the map: a wolf den, goblin warren, troll bridge, spider nest. It slowly grows and sends
   out packs, and it hurts nearby villages (prosperity and unrest, via Fiefs). You attack it for loot, bounty and
   renown, and to stop the raids. The battle could be a small assault on a cave mouth or ruin.
3. **The dead rise.** After a big battle, the site can raise undead some nights later unless the dead are buried
   (an event choice). Undead walk only at night.
4. **Legendary monsters.** One named beast at a time per region ("the Grey Widow of Hollin Wood") with a big bounty
   and a unique reward. It roams and attacks lords too, so it changes the war around it.
5. **Ambushes.** Marching through deep forest at night can trigger an ambush: your army deploys badly and the enemy
   comes from the trees.
6. **Pleas and quests.** Events from `Data/events.json`: a village begs you to kill the beast; a lord asks you to
   clear a lair in his lands; a hunter sells you a trail.

## Creatures for the player (later)

- **War hounds:** a cheap fast company, raised from a kennel building in your fiefs.
- **Tamed beasts:** clear a lair and take its young (like taking prisoners), then train them over days.
- **Monster mercenaries:** an ogre or a goblin band sometimes offered on a town's mercenary board.
- **Monster hunters:** a human troop line with a `vsMonster` bonus, spears and nets.
- **Culture flavour:** northern troops can train bear-shirts or wolf riders.

## How it fits the code

| Need | Where | Notes |
|---|---|---|
| Creature stats and traits | `Data/troops.json`, `Units.cs` (`UnitType`), `Troops.cs` | New fields with defaults so existing troops are unchanged. A new `UnitClass.Beast` (or a `body` field) tells the AI and renderer what it is. `Troops.Validate` must allow creatures outside the `levies` tree. |
| Drawing | `Puppet.cs`, `SpriteSheet.cs`, `Art.cs` | Horses already use a four-legged rig, so beasts can likely reuse it with new part sprites. Giants scale the human rig. Swarms and flyers need new rigs. All sprites are painted in code. |
| Battle AI | `BattleAI.cs` | A `ThinkBeast` next to `Think`/`ThinkCav`: pack hunting, circling, flanking, fleeing when the alpha falls. The general handles a monster army simply (charge, or wait in ambush). |
| Combat | `BattleCombat.cs`, `BattleMove.cs` | Sweep (several targets in one `Strike`), knockback (reuse `Impact`), poison, regen, pierce resistance in `Damage`. Bigger radius in separation and the grid. |
| Morale | `BattleMorale.cs` | Fear around a monster; fearless units; a pack or undead host bound to its leader. |
| Map parties and lairs | `Campaign/CampaignState.cs`, `CampaignAI.cs` | `PartyKind.Monster` + `MonsterThink`; a `Lair` list on the save (new fields with defaults, no version bump). Spawn by `WorldMap` province kind. |
| Battles from the map | `Campaign/CampaignBattle.cs`, `Rules.cs` | `PrepareBattle` builds a monster army; `Rules.Strength` and `AutoResolve` need creature weights; no ransom or prisoners from beasts, but trophies. |
| Map screen | `MapScreen.*.cs`, `World.cs` | Monster party markers, lair models, encounter panel text. |
| Events | `Data/events.json`, `Campaign/Events.cs` | New triggers (near a lair, after a battle) and verbs (spawn a monster, raise the dead). |
| Tests | `Tests/BattleTests.cs`, `Tests/CampaignTests.cs` | Each trait gets a small scenario test. |

## Suggested phases

Each phase is playable on its own and ends with `tools/check.sh` green.

1. **One creature end to end: wolves.** Trait fields, a wolf and dire wolf in data, four-legged drawing,
   `ThinkBeast`, `PartyKind.Monster` packs in forests, auto-resolve, encounter text, tests. Proves the whole pipe.
2. **Big monsters: troll and ogre.** Size, sweep, knockback, fear, regen, daylight weakness.
3. **Goblin-kin.** Swarms, ambush deployment, wolf riders (a wolf as a mount).
4. **Lairs.** Map sites that grow, raid villages and can be assaulted; bounties and trophies.
5. **Undead.** Fearless, arrow resistance, necromancer bond, the dead rising after battles at night.
6. **Player creatures.** Hounds, tamed beasts, monster mercenaries, monster hunters.
7. **Later.** Spiders and webs, fey in fog, flyers, legendary monsters, a marsh terrain kind.

## Open questions

- Tone: grim low fantasy (rare, frightening monsters) or high fantasy (common, many kinds)?
- Are monsters only wild threats, or can some (goblins) hold land and act like a realm?
- Can the player recruit monsters, and how early?
- Which family first? The plan assumes wolves because they reuse the horse rig and are simple.
