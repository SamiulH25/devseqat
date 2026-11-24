# The Wild: monsters as part of the game (plan)

The owner's brief (2026-11-21): monsters "just sort of exist". Give them a reason to exist, a reason to be fought and dealt with,
and a better roster. This plan comes from a brainstorm with the owner. Moved back to pre-demo on 2026-11-23.

**Status (2026-11-23):**
- **WI1 (Wildness)** built: `Province.Wildness` (0–100, saved), `Campaign/Wild.cs` (`WildnessDaily`, war/exhaustion multiplier, garrison & lord patrol calm, village prosperity drain & emptying, unrest in `Fiefs.cs`, caravan road closures in `Roads.cs`, seasonal wolf/wyvern shifts, immediate wildness cut on clearing), red wildness wash on the realm map (`MapScreen.Atlas.cs`), hover card line (`MapScreen.Hud.cs`), and tavern rumours (`Town.cs`).
- **WI2 (Growing lairs)** built: `Party.Stage` (saved), goblin `Holdout -> Warren -> Hold`, spider `Nest -> Brood -> Webbed Wood` (slows travel `* 0.7` in `Campaign.cs` and stops caravans), river-crossing `Bridge Trolls`, and persistent undead `Barrows` (`Undead.cs` -> `RaiseBarrow`).
- **WI3 (Spoils of the wild)** built: monster spoils (`troll_hide`, `wyvern_sting`, `spider_silk`, `bear_pelt`, `dire_fang`, `grave_dust`) in `Goods.cs` with cultural market premiums, 3 spoil-gated Smith marks (`sting_edged`, `troll_hide_lining`, `silk_padding`) in `Data/effects.json` & `Smith.cs`, `grave_dust` halving craft rite Omen in `Rites.cs`, village/lord gratitude, steppe wolf / northern bear cultural reactions, order standing on undead, deeds, and great-beast Aura Wall breakthroughs.
- **WI4–WI9** open next: monster contracts & hunts (WI4), new monsters (WI5–WI6), legendary apexes (WI7), world crisis endgame (WI8), and balance/shot verification (WI9).

## Where things stand

The fights are done and good (docs/archive/CREATURES.md, docs/archive/MONSTERS.md): goblins, wolves, trolls, the undead, spiders,
bears and wyverns, each with its own battle rules. What is missing is everything around the fights:

- Ignoring monsters costs almost nothing. Holdouts plunder a village now and then, and trolls and wyverns sometimes ravage one.
  None of it builds up, and none of it touches trade, recruits or the war.
- Killing them pays a small bounty (`BountyHoldout` 80 up to `BountyWyvern` 450) and nothing you can't also get from brigands.
- No one in the world cares. Lords hunt monsters only when they are much stronger, and no contract, people or order is about them.

## Decisions (owner, 2026-11-21)

- **In:** the wildness pressure, lairs that grow, war feeding the wild, seasons, every reward listed below, legendary monsters,
  and a **world crisis as the endgame**.
- **Out:** dragons, the Wild Hunt, and taming beasts.
- **Later, after a finished demo:** the monster hunter as a career of its own (`docs/post-demo/MONSTER_HUNTER.md`), and maybe
  playing as a goblin warlord or the lich.
- New monsters must make the player fight differently. A monster that is just a bigger man with more hp does not belong.

## WI1. Wildness

- Each province has a **wildness** from 0 to 100, kept in the save. Every lair, nest, roost and grave field in it raises
  wildness each day, more for bigger ones. Clearing one cuts wildness at once. Lords' patrols and a garrison bring it down slowly.
- **War feeds it:** a province whose lord is away on campaign, or whose realm is tired of war, gains wildness faster. A long war
  brings the monsters out of the woods, so peace has a value of its own.
- **What high wildness does:**
  - village prosperity falls and the village can empty
  - caravans and pilgrims take other roads or are lost (`Roads.cs`), so towns trade less and prices rise
  - fewer recruits and more unrest (`Fiefs.cs`)
  - more monster spawns: wildness feeds itself
- **Seen:** a red wash on the realm map (M) spreading in from the wilds, a line on the province card, and tavern rumours.
- **Seasons:** wolves come down out of taiga and tundra in winter, and wyverns move to new roosts in the dry season. A grave field
  after a great battle shows as a known risk.

## WI2. Lairs grow up

Each lair has stages, and a lair left alone moves up them:

- **Goblins:** a holdout becomes a warren, and the warren becomes a hold. A hold fields a real host and can take a village, or a
  weak castle, and keep it as a goblin place until someone storms it.
- **Spiders:** a nest becomes a brood and spreads until the whole forest province is webbed. Travel through it is slow and caravans
  stop.
- **Trolls:** a troll can claim a bridge or ford and close that road until it is killed.
- **Undead:** a grave field left long enough becomes a barrow (see the barrow king below), which keeps raising the dead.
- Every stage up shows on the map model and is told in the chronicle.

## WI3. Spoils of the wild

- **Trophies and reagents**, new goods taken after a monster battle (`Goods.cs`):

  | Taken from | Spoil |
  |---|---|
  | Troll | troll hide |
  | Wyvern | wyvern sting |
  | Spider | spider silk |
  | Bear | bear pelt |
  | Dire wolf | dire fang |
  | Undead | grave dust |
  | Basilisk | basilisk eye |
  | Harpy | harpy quills |

  They plug into what exists:
  - **Smith marks** (effects tagged `mark`): a sting-edged blade that poisons, troll-hide armour that heals a little, silk padding
    that is light.
  - **The circle:** reagents that make rites cheaper or stronger, or unlock spells.
  - **The market:** certain peoples pay well for certain spoils.
- **Hoards:** big lairs hold old loot: named gear pieces, a relic, or a house's lost banner you can return for its favour.
- **Fame:** titles (Troll-Bane, Wyrmslayer, Grave-Warden), chronicle entries for named monsters, and banner honours
  (`Legacy.BannerAfterBattle`).
- **Aura and orders:** slaying a great beast can count as a knight's trial or bring on a breakthrough. Some orders exist for this:
  one sworn against the dead, one that keeps the passes.
- **Land:** a cleared lair becomes a site: a reopened mine, a hunting lodge, a ruined castle you can claim, or the well of power the
  monster was feeding on (`Wells.cs`).
- **The people:** villages you saved remember it, with cheaper recruits, volunteers and warnings of ambush. Lords whose lands you
  cleared think better of you.
- **The peoples see monsters differently:** the steppe holds wolves sacred, so killing them angers the clans. The north has bear
  cults, and the order hates the dead.

## WI4. Contracts and the hunt

- **Monster contracts** from lords, elders and towns, each with the stake in plain words ("the road to Harrow is closed").
- **The hunt:** first learn what it is and where it lairs, from hunters, tracks and rumours. Then choose the ground and the hour:
  a troll by day, the dead in daylight, a wyvern on its roost at night. Knowing your quarry is an edge in the battle.

## WI5. New monsters I: they break your usual tactics

| Monster | Country | What it does | The answer it forces |
|---|---|---|---|
| Boars, and a great boar | Forest | A charging herd that bowls men over | Spears or stakes; a shield line is knocked down |
| Hill giant | Hills, Highlands | Throws boulders like a mangonel and stamps | Spread out and rush it |
| Goblin shaman | Hills (holdouts) | A goblin caster: curses, and rallies the swarm | Kill it first or the swarm keeps coming back |
| Harpies | Highlands | A flying swarm that picks off archers and the wounded | Bows must look up and foot must cover the archers |
| Griffon | Highlands | A lone flyer that seizes a man and carries him off | Bows; tight ranks so no man stands alone |

## WI6. New monsters II

| Monster | Country | What it does | The answer it forces |
|---|---|---|---|
| Sand wurm | Desert, Badlands | Bursts up under a formation | Keep moving and don't bunch |
| Basilisk | Desert, Oasis | Its gaze slows and freezes men who face it | Come at it from the flank or behind |
| Drowned dead | Coast | Come out of the sea at night onto shore villages | Fight at dawn; burn the wrecks they rise from |
| Sea serpent | The sea lanes | Strikes a voyage, costing men and cargo or forcing you back | Hunt it from the coast; a lane stays dangerous until then |
| Werewolf | Taiga, Tundra, Forest | Hides in a village: an investigation event, then a night fight | Silver or fire, or it heals |
| Wraiths | Old battlefields, Marsh | Only magic, aura or blessed weapons hurt them | Bring your circle or an order |
| Barrow king | Barrows (WI2) | An undead lord with a sworn guard who leads a host | Break the guard, then him |

Every family gets a full ladder from small fry to apex. Goblins and the undead already have one.

| Family | Small | Middle | Apex |
|---|---|---|---|
| Goblins | goblin | hobgoblin, shaman | warlord |
| Wolves | wolf | dire wolf | alpha |
| Boars | boar | tusker | great boar |
| Bears | bear | cave bear | bear-king |
| Spiders | spiderling | spider | brood mother |
| Giants | ogre | hill giant | giant chief |
| Undead | skeleton, ghoul | wight, wraith | barrow king, necromancer |
| Sky | harpy | griffon | wyvern |
| Desert | — | basilisk | sand wurm |

## WI7. Legendary monsters

One named apex per region, with its own story: for example "the Grey Widow of Hollin Wood", a brood mother. It hunts lords too, so
it changes the war around it. Killing it pays the best spoils in WI3, and its death is a chronicle entry and a banner honour.

## WI8. The world crisis (the endgame)

A threat no single realm can beat. It comes in the middle to late game, when wildness is high across the world, or after a set
number of days.

- **The Long Night:** the barrows wake together under a lich, and the dead march on the towns.
- **The Goblin Horde:** a Warlord unites the goblin holds and takes castles and towns.

How it plays:

- It starts with warnings (rumours, omens, fallen villages), then breaks out.
- The realms call truces and form a coalition host, and wars stop or slow.
- Leading against the crisis gives great standing: a seat on the council, a crown, an order.
- It ends when its head falls (the lich, the Warlord) in a set battle at its seat.
- If the world loses, the crisis takes realms. That is a loss, or a new world to fight back from.

Which crisis comes is the owner's call: one picked per campaign, or both.

## WI9. Check and tune

- Balance runs: `--realms` for whether the crisis breaks the realms, `--balance`, `--pacing`.
- A wildness trace per day, like `HoldoutTrace`.
- Shots of every new monster and every lair stage. The map's wildness wash at the widest zoom.

## After the demo

The monster hunter as a career of its own is drafted in `docs/post-demo/MONSTER_HUNTER.md`. Playing as a goblin warlord or the
lich is listed in `docs/post-demo/README.md`.
