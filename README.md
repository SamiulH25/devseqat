# Ironfield

A 2.5D (2D sprites in a 3D world) medieval battle game in C# + MonoGame (DesktopGL), inspired by the
"lead your army in person" genre. This is an original setting: no names, assets or factions from other games.

You fight in person as a soldier while you command your army as a general, and between battles you lead a mercenary company
through a war of three realms on a campaign map: sieges, fiefs, prisoners, contracts, seasons and the rest of `docs/ROADMAP.md`.

## Build / run / test

```sh
dotnet build
dotnet run                 # play
dotnet run -- --selftest   # headless: mechanics, battles, sieges, campaigns, saves and replays
dotnet run -- --selftest SiegeBattle   # one test by name
dotnet run -- --balance 4  # telemetry: N headless campaigns (and a stand-in captain's career pacing) and battles -> balance/*.csv
dotnet run -- --replay     # play back your last battle (replays/last-battle.json) and check it ends the same way
dotnet run -- --gpu-report # draw a battle at each quality; writes gpu-report.txt (driver, shaders, fps)
dotnet run -c Release -- --bench 1500  # headless N-a-side battle: ms per tick for each stage of the simulation
```

You need .NET SDK 10 (`net10.0`). On Ubuntu 24.04 `apt-get install dotnet-sdk-10.0` works (no Microsoft feed needed).
The game needs no art files: sprites, terrain and the font are all generated in code. Troops come from `Data/troops.json`.
**Design language:** see [`docs/DESIGN.md`](docs/DESIGN.md) (art direction, unit painting rules, heraldry, tiers, UI tokens).
**Roadmap:** see [`docs/ROADMAP.md`](docs/ROADMAP.md) (what comes next, in what order, and why).
**Campaign:** see [`docs/CAMPAIGN.md`](docs/CAMPAIGN.md) (map, companies, XP and upgrades, economy, AI, battle hand-off; "As built" at the end).

The game opens on a **title screen**: New Campaign, Continue (latest save), Quick Battle (one random battle), Quit.
To inspect the sprite atlas, run `IRONFIELD_DUMP_ATLAS=/path/atlas.png dotnet run`.
To render a few staged camera shots as PNGs and quit (works headless with `xvfb-run -a dotnet run`), set `IRONFIELD_SHOTS=/some/dir`
(`IRONFIELD_CAMPAIGN_SHOTS` does the same for the campaign screens). `IRONFIELD_WEATHER=hour,rain,fog` and `IRONFIELD_GROUND=river` set a quick battle's
weather and ground; `IRONFIELD_MEN=n` sets its men a side (default: half the Battle size setting).
`dotnet run -- --write-controls` writes `controls.json` with the default bindings.
Graphics quality: **F2** cycles High (shaders, sun shadows, post) / Medium (no shadows) / Low (fixed-function); `IRONFIELD_QUALITY=Low` picks one at start.
UI lab: **F3** on the title screen opens a gallery of interface concepts (left/right to page, Esc back).
Shaders are plain GLSL in `Shaders/` and need **no shader compiler** (no MGFXC, no Wine): see "Shaders" below.

## The campaign

You are a free captain sworn to the Azure Crown. It is at war with the Crimson Compact, and the Northmarch clans hold the north under truces
that will not last. Brigands roam between them all.
- **Map:** click to march, click a town or village to enter it, and click a party to chase it. P opens the party screen, Space pauses,
  1-3 set the speed (x1, x3, x8), F follows you, F5 saves, F9 loads, and Esc opens the menu. The camera works as in command view (WASD, Q/E, wheel,
  middle-drag), and the overview (top right) jumps the camera. Hover anything for details.
- **Big armies.** Campaign armies are three times the old size (`ArmyScale` in `Data/campaign.json`): you start with about 120 men
  and can lead several hundred late on; lords field 100-200, and companies hold up to 72 men. Prices per man are a third of
  the troop price, so the economy works out as before. **Battle size** (Settings, GAME tab: 400 / 800 / 1,500 / 3,000) is how many
  men stand on the field at once; the rest wait as reinforcements. The field grows with the armies.
- **Divisions (V, or DIVISIONS on the party screen):** put companies under a companion or a company's sergeant. A division fights
  by a **goal** (hold, attack, flank left or right, reserve, screen, harass, guard) rather than your orders, in the leader's temper
  (bold, steady, cautious) and with his command skill, which grows with the battles he leads. Companies in no division are your
  retinue and obey you at once. At most 6 divisions; a division whose leader falls or leaves breaks up. Skilled leaders also help
  a little when you auto-resolve. Big enemy armies split into divisions too (a horse wing and two foot battles).
- **Companies persist.** A company is a banner of one role (foot, spears, bows or horse) holding **stacks** of troops, each with its own
  XP pool. Men are **promoted one at a time** along `troops.json` in a town's training yard: click a stack in the party screen and promote
  1, 5 or every ready man into any branch. A man promoted out of the company's role joins a company of his new role, or founds one.
  A mixed company fights as one squad. Companies keep an origin, a banner, a ledger of their last fights and up to three **honours**
  (Unbroken, Horse-breakers, Old Hands, Stormers, Bloodied) that change how they fight. Saves from before this (version 3 and older) are refused.
- **Cultures:** every troop is Azure, Crimson, Northern or common (`Data/troops.json`), and each realm has a culture (`Data/factions.json`).
  A village raises its own culture's men whoever holds it (clansmen in the north, levies in the south), and they form companies of their own
  people. Men train into common troops or their own culture's lines only, and a culture's elite lines (tier V and up) need one of its towns
  in your realm. Foreign mercenaries are the other way to field another culture's elites.
- **Lords and companions:** every lord is a character with traits (Honourable, Cruel, Cautious, Reckless, Greedy, ...) that drive what he
  does, and an opinion of you with the memories behind it ("you beat me in the field", "you set me free without ransom"); opinions fade
  with time. A lord who thinks ill of you keeps his villages' recruits from you. A lord can fall in battle and his heir takes up his banner
  and his grudges. Sergeants who live through 15 battles, and wanderers found in town taverns, join your household as **companions**
  (quartermaster, surgeon, scout, drillmaster or steward) and lead a company into battle as a hero. L opens the lords and companions screen.
- **Towns:** train, hire mercenaries, buy horses and food, kit yourself out at the armourer (MARKET tab: weapons from spear to crossbow, shields, armour, helms and horses; stock changes weekly, heavy pieces need the Arms skill, the best need a rank; worn gear can be swapped, spare gear sold back at 40%), rest to heal, sell prisoners
  and ransom lords, sign or end contracts, and take work from the steward's board (hunt a lord, clear brigands, guard a village, scout a town,
  take a castle). **Villages:** recruit (levies, or clansmen in the north), buy or forage food. **Castles:** rest, prisoners and contracts.
- **Sieges:** ride to an enemy town or castle and besiege it. After a night your men have ladders and a ram; assault the walls (an on-foot
  battle: climb at the ladders, batter the gate, defenders on the walkway) or wait while the garrison starves. A seat you capture as a sworn
  vassal becomes your **fief** (your first at once, later ones by the council's vote): weekly income, and a garrison you can strengthen
  with your own companies. Before you swear fealty the realm keeps it and pays you a captor's purse. Lords besiege and retake seats too.
- **Fiefs live:** each has **prosperity** (it scales income and recruits; raids and sieges hurt it) and **unrest** (foreign rule,
  high taxes, sieges and raids raise it; a garrison, a chapel and low taxes calm it). At 70 unrest rebels take the field; at 100 the place
  goes over to the realm of its own people. In your fief's FIEF tab set **taxes** and raise **buildings**: training yard, barracks (a
  culture's elite tiers need one), walls, market, granary, stables, chapel and watchtower. AI realms build too. As a lord with two fiefs
  you can give one to a companion and make him your **vassal**, with standing orders (defend my lands, follow me, join the host); lords who
  hate their liege may defect to a ruler they like.
- **War and peace:** wars wear realms down (losses, lost seats, time). A weary realm sues for peace on the winner's terms: a plain
  truce, gold over four weeks, a seat handed over, or **tribute** (a quarter of its income, and it fights its overlord's wars). Realms at
  rest pick new wars. The marshal **musters hosts** of the lords who respect him, and as a vassal you are **summoned**: come within four
  days to win his favour. As king you declare war, offer peace on terms (only as far as you are ahead), and ally against a shared enemy;
  your tributaries' land counts toward victory.
- **Events and quests:** every few days something happens that depends on your world: a petition from a restless fief, famine,
  a fair, a lord asking a favour (a quest that wins his friendship; refusing costs it), a plot against your liege, a feast for your
  veterans, a hard winter, a tournament. A big brigand band can crown a **brigand king** who burns villages, and a bad revolt can raise
  a **pretender** who will take the town if you do not beat him. Events are data in `Data/events.json`.
- **Between the towns:** ruins, shrines, standing stones, old battlefields, mines, hermits, watchtowers, springs and mills dot
  the map; ride to one to search, pray, rest or ask. Merchant caravans and pilgrims travel the roads: buy from them, ask their
  news, or rob them (their realm will not forget). Village elders ask for small jobs anyone may take (wolves, goblins, a
  stolen daughter, grain, militia drill); towns add caravan escorts and ransoms; lords may send you after a lost sword.
- **Sight:** you make out who a party is only within a day's sight of your company or your fiefs; further off you see
  banners, not names. A Scout companion sees a quarter further; the top of a watchtower doubles it for two days.
- **Your record:** your deeds stay in the realm tab's chronicle, and when the war ends a summary tells what you did.
  A weary enemy may send an envoy to sue a king for peace.
- **Town life:** the tavern has a wanderer now and then and rumours of real things nearby (ride there from the list).
  A tournament is a short fight in the lists with your captain and six of your best men: blunt weapons, no deaths, a purse.
- **Prisoners** follow you (they slow you and eat): sell, recruit or free them. Lords you beat may be captured and ransomed.
- **Officers:** sergeants with traits (Stubborn, Steady, Drillmaster, Hothead, Sharpshooter, Surgeon, Veteran) that show in battle; they can fall.
- **Seasons and food:** winter slows the march, slows healing and doubles rations; starving men lose cohesion and desert. Your liege's towns feed you.
- **Three realms:** wars and truces are per pair; a realm that grows too strong unites the others against it.
- **Encounters:** attack (the battle opens in a **deployment phase**: arrange your lines in your third of the field, then Space or START), auto-resolve,
  retreat (a rearguard pays), or pay brigands off. Esc in a campaign battle lets you **withdraw**.
- **Duels:** meeting a lord, **CHALLENGE** him to single combat between the armies (on foot, the two of you, a fall is a yield). A cautious lord refuses, and one whose army is half again as strong as yours sees no need unless he is reckless or honourable. Win: +10 renown and his men start the battle 15 morale down. Lose: you are wounded and lead the battle from the rear. One duel an encounter.
- **Results:** the fallen split into killed and wounded (heavier armour survives more often). Then come XP, loot, ransom, bounty, renown and captured horses, and sometimes a piece of the beaten leader's gear to take (sure if you cut down a lord; a tournament win may give a prize too).
  If you fall you are wounded, and command from the rear for a few days; if your army also loses, you are captured and ransomed.
- **The world runs without you.** Lords recruit, refit, upgrade (same rules and prices), patrol, hunt, flee and muster hosts, and
  march for the front rather than idling at home, so realms fight each other whether or not you are there. Brigands are very
  common — around 190 bands on the enlarged map, spawning in several a day — and grow with the campaign: a late band is a real
  fight, not loot. Villages get threatened, the war score moves,
  the losing side sues for a 30-day truce, and the war resumes when it ends. Upkeep is weekly, a contract pays 85% of it, and unpaid men desert.
- **A career, from hired sword to king.** Your **rank** rises: Free Company → Sworn Sword (a month in one realm's service; its recruits
  cost you half, and your liege pays a weekly retainer) → Vassal (swear fealty at 120 renown when your liege trusts you; hold land, vote in council) → Lord (two fiefs and 350
  renown; propose wars, declare independence) → Marshal (500 renown to stand, then elected by the council; name the realm's objective) → King (declare
  independence from a town you hold: your fiefs become a new realm at war with your old liege, and lords who like you more than him follow).
  Every realm's **council** sits each fortnight to grant captured seats, elect a marshal and vote on war. A king declares war and offers
  peace himself. **You win** when your realm holds 60% of the provinces while you are its king or marshal (play on after); a king whose
  realm loses its last seat has lost. R opens the realm screen: every realm, their relations and the chronicle, with your career and your acts on its second tab.
- **Progression is spelled out.** The captain panel shows your **rank** and one clear **next step**
  — promote your ready men, choose a perk, see a steward for work, buy food — and arriving in a town opens the tab that step
  names. You can field up to **10 companies** right away; **Renown** raises your total troop count (men limit); a bar under the captain
  panel shows the next rise and how far off it is, and the results screen repeats it after every battle.
- **Captain skills:** ten skills (Leadership, Tactics, Logistics, Training, Medicine, Stewardship, Charm, Arms, Riding, Bowcraft) rise by doing what
  they name: commanding, winning against the odds, marching, promoting men, healing them, ruling fiefs, winning goodwill and ransoms,
  your own kills and parries on foot, your kills and lance hits from the saddle, and your arrows. Every 25 points opens a perk on that branch, in tiers: tier I at 25, II at 50, III at 75, and at 100 a crowned capstone that needs two other perks of the branch (50 in all: Bannerman, Iron Will, Tactician, Siegecraft,
  Quartermaster, Drillmaster, Cheap Steel, Surgeon, Tax Roll, Silver Tongue, Ransom Broker, War Cry, Duellist, Lancer, Marksman, ... and the capstones Warlord, Master of War, Champion, Deadeye ...). Heavy
  gear needs Arms, great horses Riding, the war bow Bowcraft. K opens the skill tree; in a friendly town you can forget a taken perk (click it twice) for 150 crowns, 150 more each time. The battle report lists each skill that rose. Balance numbers live in `Data/campaign.json`; names in `Data/names.json`.

## Controls

Press **F1** in game for the full list. Every key below is a default: rebind in `controls.json`
(`dotnet run -- --write-controls` writes a template with every action, plus `MouseSensitivity` and `InvertMouseY`).
Keys are MonoGame `Keys` names (`W`, `LeftShift`, `F1`...) or `LMB`/`RMB`/`MMB`/`MB4`/`MB5`, `SHIFT`, `CTRL`, `ALT`, `ESC`, digits.

**Field view** (you fight; third-person camera, mouse wheel zooms it). You start **on horseback**.
- Mounted: W/S rein (speed up / slow, back up), A/D turn the horse, Shift gallop, Q couch the lance (needs speed), E dismount (when nearly still).
  The mouse looks around freely: you strike where you look, not where the horse points.
- On foot: WASD move, Shift sprint, E mount a loose horse within reach (yours, a fallen rider's, or the enemy's). Prompts appear on screen.
- **Attack direction** comes from the way the mouse last moved: up = overhead, down = thrust, left/right = side cuts.
  The gold chevron at the crosshair shows it. LMB hold+release attacks (longer hold hits harder).
- RMB block (front only). A shield stops any direction at a stamina cost; blocking **in the direction the blow comes from**
  (red chevron) is a **parry**: free, and it staggers the attacker. RMB while winding up cancels the swing (feint).
- R rally. **Hold MMB (or Z) for the order wheel**: move the mouse toward an order, release to give it.

**Command view** (Tab; time x0.25, Space pauses):
- LMB select a squad, drag a box to select several (Shift adds). Click a unit card to select it, double-click to jump the camera to it.
- RMB click moves (keeps the current width), RMB drag draws the formation line (it faces the enemy) with a preview of every man's spot,
  RMB on an enemy squad attacks it (red line; hovered enemies are ringed).
- WASD or the screen edges pan, Q/E or middle-drag orbit (middle-drag also tilts), wheel zoom, click the minimap to jump there.
- The order buttons (bottom right) give the same orders as the hotkeys.

**Orders (both views):** 1-9 toggle a squad, 0 or ~ selects all (with nothing selected, orders go to every squad).
C charge, F follow me, G form a line on me, H hold position, T advance 15 m, B fall back 20 m, V loose/tight spacing,
**J formation** (line, shield wall, square, wedge, loose), X archers fire at will / hold fire. Allied lords' companies follow their own AI general.

**Divisions (both views):** division cards sit top left; click one (or press **Y** to cycle) to give it goals. In command view the
order buttons become goal buttons, and RMB on the ground means *take this ground*, on an enemy *attack it*, on a friend *screen it*.
In field view the order wheel becomes a goal wheel. Goals do not arrive at once: a **rider** carries each one to the sergeant
(about 1.5 s plus 1 s per 14 m, longer if he is fighting, shorter with his skill and the Bannerman perk), and a newer goal overtakes
one still on the road. Riders show as gold dots in command view; an enemy who reaches the road cuts a rider down and the next one
rides out the long way round (cut down the enemy's red riders and his divisions wait). You can still select a division's companies
and order them directly: the order rides out the same way, and they then follow it until the division gets a new goal. If a
sergeant falls, his men keep their last goal, orders take twice as long, and the best man left takes charge after 10 s.

**Captain combat:** a parry needs the guard raised just as the blow comes (within 0.35 s, up to 0.45 s with Arms); attacking into a blow from the mirrored side
**chambers** it. A parry opens a **riposte** for 0.45 s: a swing started then winds up twice as fast, hits a quarter harder, costs half
the stamina and cannot be blocked by men of tier III or lower. Swings started hard on the heels of the last form a **chain**: each link
costs 5 more stamina, the enemy reads the rhythm and blocks more often, and past the second link your blows no longer interrupt his
swing. Breath comes back only between blows; a swing with too little stamina is weak and slow. Arms (15 to 100) quickens your
blows by up to 15% and cuts their stamina cost by up to a quarter. **Ctrl** shield bash (a kick with two hands), **N** swaps to your second weapon (the greatsword at first; any weapon you own; two-handed blades on foot only). With a bow or crossbow in hand, hold attack to draw (the aim steadies), release to loose at the foe nearest your facing; arrows refill at your wagon. **E** with no horse in reach
whistles for the nearest loose horse of your side. **Esc** opens the menu (resume, controls, new battle, quit); Enter starts a new battle after the result.

## Design: soldier vs general

The core tension: **you are strongest as a leader when you are in the fight, but you can only control the fight from above.**

- **Leadership aura:** your troops within 12 m gain morale (routing ones recover faster). The aura is OFF in command view, but a living
  general still heartens his whole army a little (so the enemy captain's aura behind his line is no longer a free permanent edge).
- **Command view** slows time to x0.25 and gives RTS-style control, but your body stands idle and vulnerable.
- **Rally (R):** costs 30 stamina, 12 s cooldown. It gives +25 morale to allies within 15 m (+45 to routing ones, which usually brings them back).
- **Heroics:** your kills give nearby allies more morale than ordinary kills.
- **Leaders matter:** if you or the enemy captain dies, every unit on that side loses 16 morale. The enemy captain (red ring in command view) stays just behind his line, so killing him is a real tactical goal.
- **If you fall,** the battle continues and you command from afar at normal speed, without an aura.
- **Morale and routing:** below 12 morale a unit routs and flees off the map (radius 95). It rallies again above 32, and a man who has
  broken twice stays broken. A side loses when it has no active, non-routing units.
- **Melee:** windup, strike, recover. Hits interrupt windups. AI units block hits from the front by chance (Shieldmen 55%). A player block staggers the attacker; if stamina runs out, the guard breaks. Player swings cleave up to 3 targets.
- **Archers** shoot ballistic arrows that lead the target. They fire only when settled in formation, or on Charge. Loose spacing (V) reduces arrow hits, because hits are physical and not rolled.

## Design: cavalry

Cavalry comes in three tiers: **Outriders** (coursers), **Lancers** (warhorses) and **Knights** (caparisoned destriers);
the player rides a courser.
- **Momentum:** a horse has a heading and a speed. It accelerates, brakes and turns at limited rates (turning gets wider as it speeds up),
  so a charge needs room. AI riders steer toward a wished velocity (`Battle.Steer`); the player steers directly.
- **Speed bonus (Mount & Blade):** every melee blow gets `+5%` per m/s of closing speed above 2 m/s (shown under the crosshair as `+N% SPEED`).
- **Couched lance:** above 6 m/s a lance can be lowered (AI does it on a charge, the player presses Q). Anything that the point
  (1.0-2.7 m ahead of the horse) touches is hit for `10 + 4.2 x closing speed`, then the lance comes up. It drops again when the rider slows.
  A rider's ordinary blow in the melee reaches only as far as a footman's blade (1.9 m), so a stalled charge is not a free win.
- **Trample:** a horse running into an enemy footman at over 4 m/s knocks him down (stagger) and hurts him; every body slows the horse,
  and tight shield walls slow it more. The shock costs nearby friends morale.
- **Bracing (Total War):** spearmen, and shieldmen holding a tight formation, stand still and set their points when horses come.
  A braced spear reaches further than a couched lance and hits the horse hard (`8 x` closing speed for spears), stopping the charge.
  Spearrunners also deal x1.8 damage to anything mounted. Arrows and spears often hit the horse instead of the rider.
- **Horses die:** a slain horse throws its rider (stagger, damage). Riders whose horse dies fight on foot. A killed rider leaves a loose horse.
- **Cycle charge (AI):** Charge → ride through the target → Withdraw (ride out ~4 s) → turn → Charge again. A rider that bogs down
  in a crowd (slow, near the target for 1.2 s) fights in place for 5 s and then pulls out. Cavalry on Hold counter-charge enemies within 15 m.
- **Enemy horse:** waits wide on the far flank, then hunts the softest squad (archers first) when the lines meet or after 35 s.

## Design: troops, tiers and armies (campaign-ready)

- **Troops are data** (`Data/troops.json`, loaded by `Troops.cs`): stats, class, tier (I-VII), the troops each one
  **upgrades to**, campaign cost and upgrade XP, its horse, and its **look** (head, body, legs, feet, hands, shield, weapon,
  tabard, plume, cape). The self-test checks the tree: links exist, every upgrade raises the tier and is a better soldier,
  and every troop can be reached from Levies or Clansmen.
- **The tree:** 107 troops (and the captain) across 7 tiers (I–VII) with multiple branching and cross-linking lines for Crown/Compact (`levies`) and Northmarch (`clansmen`) across heavy shield infantry, greatswords/executioners, sergeants, northern huscarls/berserkers/hearthguard, pikes/halberdiers/glaives, longbows/rangers/crossbows/arbalests, and shock knights/cataphracts/reaver cavalry/horse archers/dragoons.
- **Armies:** a battle takes two `ArmySpec`s, which are lists of `Company(troop, count, role)`. This is what the campaign map
  will hand over. Companies deploy by role (infantry front, archers behind, spears left, horse right; several companies of a
  role stand side by side), and the enemy general commands any mix.
- Until the campaign exists, each **new battle** rolls an army level (1-4) per side, and each line lands one tier up or down at random,
  as if companies had been upgraded unevenly. Elite companies are smaller. Override with `IRONFIELD_LEVELS=4,2`.
  The enemy's host is only 5% larger than yours at the same level, so a battle is won by what you do, not by the roster.
- Better troops look better (see the tier table in `docs/DESIGN.md`), and unit cards show the tier numeral.

## Design: terrain and the 3D world

The world is 3D; only the units (and horses) are 2D sprite puppets.
- **Terrain** (`Terrain.cs`, part of the headless sim): a 512 m heightmap on a 2.5 m grid for a battlefield, and a much larger one
  for the campaign map (+-448 m on a coarser 4 m grid; the grid size is per-instance, and the mesh switches to 32-bit indices past
  65k vertices). Gentle value-noise rolls (calmer in the middle),
  3-5 random hills, a rim of high ground framing the valley, and a dirt road winding between the armies. It is seeded per battle.
  `Height()` interpolates on the same triangles the mesh draws, so units stand exactly on the ground you see.
- **It matters in the fight:** uphill is slower and downhill quicker (`Battle.SlopeFactor`, x0.55-1.15); striking from higher ground deals
  up to +15% (less from below; the hit text says HIGH GROUND); arrows fly from and land on the real ground and stick where they hit it.
- **Foliage** (placed by `Terrain`, built into meshes by `World.cs`): woods of broadleaf trees or pines, two of them flanking the field,
  lone trees, bushes along wood edges, rocks and boulders, and ~14,000 3D grass tufts with a few flowers. All low-poly, flat-shaded and lit by one sun.
  Tree trunks and boulders are **solid**: units are pushed out of them and a horse that rides into one pulls up hard.
  The deployment ground (`Terrain.Core`) is kept clear of anything solid.
- **Sky:** a gradient dome; fog fades the world into the horizon colour.

## Shaders

The renderer runs on custom GLSL shaders (`Shaders/*.glsl`), turned into MonoGame effects **at runtime** by `GlslEffect.cs`.
MonoGame's own effect compiler (MGFXC) needs Wine on Linux and macOS. But a DesktopGL compiled effect (MGFX, profile 0) simply carries GLSL text,
so `GlslEffect` writes that binary itself (layout as read by MonoGame 3.8.6's `Effect`/`Shader`). The rules for a shader file:
- One file for both stages. The builder prepends `#version 120`, then `#define VERTEX` or `#define FRAGMENT`, the technique's own
  defines (e.g. `FOLIAGE`, `SHADOW`), one `uniform vec4 U[n]` holding all parameters, and a `#define` per parameter
  (matrices become `mat4(...)` columns, so write `pos * ViewProj` like HLSL). Samplers are fragment-only.
- Vertex shaders end with `FIXUP_POSITION` (MonoGame's GL `posFixup` y-flip for render targets, and D3D 0..1 depth to GL clip).
- Anything sampled from a render target is upside down on GL, so use `uv.y = 0.5 - 0.5 * ndc.y`.
- Parameters are set by name from C# (`effect.Parameters["SunDir"].SetValue(...)`). Sampler states travel with the effect.

The pipeline (`Shading.cs`, driven from `BattleGame.DrawShaded`):
1. **Sun shadow map:** a 2048² R32F target. An orthographic sun camera is aimed at what you look at (90 m in the field, more as the
   command camera zooms out) and snapped to texels so shadows do not shimmer. Terrain, foliage and units cast shadows. Units
   are rebuilt as puppets **facing the sun** for this pass, so a horse's shadow has legs.
2. **Scene into an off-screen target:** the sky (gradient, sun disc and glow, drifting fbm clouds), terrain and foliage (per-pixel
   sun and hemispheric sky light, 3x3 PCF shadows, wind on grass and leaves, backlit leaves glow, distance and height fog), contact blobs,
   lit and shadowed unit sprites, then arrows and markers.
3. **Post:** FXAA (the game runs without MSAA), a gentle colour grade with split toning (warm highlights, cool shadows), and a vignette.

If a driver rejects the GLSL, the game logs it and **falls back to the fixed-function path** (Low) instead of failing.

## Design: interface

- Bottom centre: **unit cards** (portrait made from the unit's own sprites, count, strength and morale bars, order tag, state above:
  WAVERING / ROUTING / FLANKED). Bottom left: you (horse and speed, health, stamina, horse health, rally, kills).
- Top: **balance of power** (strength-weighted), battle clock, event feed (top left), **minimap** (top right; enemy at the top, camera wedge).
- Field view: directional crosshair chevrons, damage text (speed bonus, flank, rear, high ground, parry), context prompts.
- Command view: squad **banners** with morale bars, order buttons, box select, formation preview, attack lines.
- Overlays: order wheel, F1 controls, Esc menu, and a result screen with both armies' losses.

## Design: divisions and scale

- **Why divisions:** at 1,500 men you cannot steer forty companies one by one. Divisions turn the general's job into picking goals
  and timing them, and the order delay means you must plan ahead: a flank ordered late arrives late.
- **Sergeants:** each division has a leader on the field (a gold pennant marks him); his aura steadies nearby men (+1.5 morale a
  second, +4 to the routing). Killing the enemy's sergeants is a real aim: their divisions freeze on their last goal.
- **The AI general** keeps his centre under his own hand and sends his divisions in when the lines close, the fight starts, or
  they have waited long enough for their temper; a broken wing that is not bold falls back.
- **Scale:** men near the enemy think every tick, others every 2nd or 4th tick, so 3,000 men cost about 3.6 ms a tick
  (`--bench`). Far men are drawn with fewer parts, and the roar of a big melee rises with the men in contact.

## Design: Total War morale and flanks

- **Flanks and rear:** blows from the side deal x1.2, from behind x1.5, and only frontal blows can be blocked.
  Each flank/rear hit adds to the squad's `Pressure`, which drains morale until it decays (HUD: FLANKED).
- **Fear:** horsemen galloping straight at an unbraced footman drain his morale. Nearby routing friends drain it too (rout spreads),
  and so does an army that has lost over half its men.
- HUD unit cards show STEADY / FLANKED / WAVERING (morale under 30) / ROUTING.

## Architecture

For a detailed developer guide (how each system works, data formats, worked examples) see `docs/DEVELOPING.md`.

| File | Role |
|---|---|
| `Battle.cs` | Headless simulation: setup, deployment, the tick, reinforcements, result and win check. No graphics. Split by area into `BattleAI.cs` (unit AI, cavalry, the AI general), `BattleCombat.cs` (strikes, hits, kills, arrows, braces, lances), `BattleMove.cs` (movement, collisions), `BattleMorale.cs`, `BattlePlayer.cs` (your own unit). |
| `Squad.cs` | Squad orders (Hold/Follow/Charge, optional attack `Target`), formation slots (anchor = centre of the front rank), flank `Pressure`. |
| `Units.cs` | `UnitType` (a troop from the data), `Look`, `Mount`, the `Unit` state (incl. heading/speed when mounted), loose and dead horses. |
| `Troops.cs` | Loads and validates `Data/troops.json` (troops, upgrade trees, mounts); `ArmySpec`/`Company`; `Armies` builds armies by level. |
| `Factions.cs` | Loads and validates `Data/factions.json`: the three realms (name, palette, culture, the troop each role fields per tier) and the cultures (each one's village recruit). |
| `Style.cs` | Design tokens: side heraldry, materials, horse coats, sprite light and density, UI colours, grid, type scale. |
| `Terrain.cs` | Headless heightmap (noise, hills, rim, road), foliage placement and solid obstacles. |
| `ScreenHost.cs` | The MonoGame `Game`: shared art, audio and settings; switches between `TitleScreen`, `MapScreen` and `BattleScreen`, with the `SettingsScreen` overlay. |
| `BattleScreen.cs` | A battle on screen: setup and the per-frame update; `BattleScreen.Controls.cs` (input, the blended third-person/command camera, terrain picking), `.Orders.cs` (squad orders), `.Draw.cs` (render passes, particles, rain), `.Shots.cs` (staged screenshots). |
| `BattleSiege.cs` | Assaults: deploying defenders on the wall, routing men through ladders and the gate (`Via`), the wall barrier, battering, siege generals. |
| `BattleCaptain.cs` | Captain combat (bash, weapon swap, calling a horse), fatigue, supply wagons, AI reserve and retreat helpers. |
| `BattleGrid.cs` | The spatial grid: near-neighbour queries for separation, targeting, morale and arrows. |
| `Replay.cs` | Battle recording and exact playback (`--replay`). `GpuReport.cs`: `--gpu-report`. `Sky.cs`: light by hour and weather. |
| `World.cs` | 3D world: terrain mesh, procedural low-poly foliage and grass (tagged with wind sway and leafiness), sky dome, minimap image; shaded and fixed-function draw paths. |
| `Campaign/` | The headless campaign: `CampaignState` (save data and its version check, RNG, balance, names), `WorldMap` (generation, rivers, terrain, settlements), `Rules` (companies, stacks, places, auto-resolve, battle bridge), `Companies` (stacks, promotion by the man, honours, ledger), `Campaign` (time, movement, encounters, the hourly/daily/weekly ticks), `CampaignBattle` (preparing and applying battles), `CampaignActions` (your actions in towns), `CampaignAI` (lords, bandits, realms), `CampaignWar` (prisoners, officers, seasons and food, contracts), `CampaignSiege` (garrisons, sieges, capture, fiefs), `CampaignProgression` (the next step), `Career` (ranks, fealty, the council, founding a realm, winning and losing), `Characters` (lords, companions, opinion and memories), `Skills` (the captain's skill tree and perks), `Pilot` (a scripted stand-in captain for headless pacing runs). |
| `MapScreen.cs` | The campaign map: 3D world, banner-bearer parties, input. Panels in `MapScreen.Hud.cs` (always-on map HUD), `.Settlement.cs` (one method per tab), `.Party.cs` (party, characters, skills, upgrade), `.Realm.cs`, `.Dialogs.cs` (encounter, siege, results, events, menu). `Ui.cs`: the immediate-mode panel kit. |
| `BattleTypes.cs` | `BattleOptions` (perks, gear, wounds, palettes, ground, size cap), `BattleResult` / `CompanyResult` for the campaign. |
| `Shading.cs` | The shader pipeline: effects, sun shadow map, scene target, post pass, light and grade values, quality levels. |
| `GlslEffect.cs` | Builds MonoGame effects from GLSL at runtime (writes the DesktopGL MGFX format), no shader compiler needed. |
| `Shaders/` | `world.glsl` (terrain, foliage), `sprite.glsl` (units), `sky.glsl`, `post.glsl`, `common.glsl` (shadow and fog helpers). |
| `Hud.cs` | The whole 2D interface, its layout and hit-testing (cards, buttons, minimap clicks, menus). |
| `Controls.cs` | Rebindable actions, defaults, `controls.json` loading and key names for the HUD. |
| `Puppet.cs` | Draws each unit as a puppet of part sprites on a 3D skeleton, with continuous poses from its action state; horses too. |
| `Painter.cs` | The sprite painter: materials (hue-shifted 5-tone ramps + surface patterns), lit shapes, selective outline. |
| `SpriteSheet.cs` | Paints every unit and horse part for every look, view and side, and packs a 1024x1024 atlas (headless). |
| `Art.cs` | Uploads the atlas; blob shadow; white pixel. `PixelFont.cs`: the 5x7 HUD font. |
| `SelfTest.cs` | `--selftest` runner and helpers; the tests are in `Tests/` (`BattleTests`, `CampaignTests`, `CareerTests`). `tools/check.sh` runs build + rules + self-test. |

Ground positions are `Vector2` (X = world X, Y = world Z).

### Puppet sprites (how units are drawn)

Each unit is about 15 painted pixel-art sprites placed on a 3D skeleton (unit space: +X right hand, +Y up, +Z facing, metres; 1 sprite px = 0.03 m),
dressed from its troop's `Look` (`torso.mail.f`, `helm.greathelm.s`, `shield.heater`, ...):
- **Billboards** (head, helm/hood, torso, hands, boots, plume, cape, quiver) always face the camera. View parts swap between `.f`/`.s`/`.b`
  sprites by the angle between the unit's facing and the camera; side sprites face right and are mirrored.
  Layered parts get a small toward-camera `bias` (the helm sits over the head) so they never z-fight.
- **Axial** parts (legs, sleeves, sword, spear, bow, arrow) stretch between two joints and turn about that axis to face the camera.
- **Planes** (shields) are fixed world quads, so a shield really swings between the front and the side.
- **Long** parts (horse body, neck, head) span two joints like axial parts but keep their top edge up, so the saddle never flips under the belly.
  Small billboard caps (chest, rump, face) fill in the horse when it is seen end-on.
- **Riders** sit `Puppet.Saddle` metres up. The horse is drawn along `Unit.Heading` and the rider along `Unit.Facing`, so a rider can look and strike sideways.
- **Swing poses** are keyed per `AttackDir`: each key pose is (arm angle, weapon pitch, swing-plane yaw), so side cuts sweep across the body.
- **Corpses** lay the parts flat on the ground.

Poses are continuous: `Puppet.PoseOf` blends the key poses (idle/raise/strike, as arm angle plus weapon pitch) by action progress,
and the walk swing follows the stride. `Unit.ViewFacing` eases toward `Facing`, so turning sweeps smoothly.
Everything is one draw call (`AlphaTestEffect`, so no sorting) plus blob shadows and lines for arrows and markers.
Horse coat parts are neutral grey (`/n`) and tinted per horse; saddle cloths and caparisons are drawn over them in the side's colours.
For hand-made art later, put PNGs into the atlas under the same keys (`part.variant.view/palette`); the puppet does not care who painted them.

## Decisions / gotchas

- **Camera history:** the first build was third person, then a fixed world angle; the user then chose **third person for combat plus a free
  command camera**, with smooth blending between the views. The current code is the third/free version.
- **Sprite history:** side-only frames came first, then 16-direction box models pre-rendered at startup. These were replaced at the user's request by
  **puppets made of per-part pixel-art sprites** (the current version).
- Mouse look warps the cursor to the window centre (`Mouse.SetPosition`). If a platform refuses warps, switch to SDL relative mouse mode.
- Battle sim stays free of MonoGame graphics, so it runs headless in tests. Keep it that way (terrain and foliage placement included).
- The game asks for the `HiDef` profile (big static meshes, float render targets). Multisampling is off: asking for it made context creation fail on
  Mesa/Xvfb, so FXAA in the post pass smooths the edges instead.
- In field view, friendly soldiers within 6 m of the camera are not drawn, so your own ranks never fill the lens.
- `Battle.Empty` uses `Terrain.Flat()` (no hills, no trees), so mechanic tests are not affected by the map.
- Team 0 = player side and squads 1-3 in `Squads` order. `Squad.Alive` is refreshed each tick by `UpdateSquad`.
- Separation, targeting, morale and arrows go through the spatial grid (`BattleGrid.cs`); a 400-man battle runs about 5× real time headless.
- Scaling a MonoGame `Color` by a float also scales its alpha: `Mat` forces opaque colours, so use `Color.Lerp` for tints.
- `AttackDir` is named from the attacker's side; the defender must block `Dirs.Mirror(swing)`. The HUD shows incoming blows already mirrored.
- Separation is mass-weighted (horse 6, man 1) with radii 1.0 / 0.375 m; horse-vs-footman overlaps are where trample `Impact` happens.
- `Battle.Empty(seed)` builds a scenario field (two leaders far apart) for the self-test; `AddSquad`, `Hit` and `HitHorse` are reachable from it.
- With an idle player, the AI enemy (slightly larger than yours at the same level) can still win the self-test battles, because the balance
  assumes you fight and command. With both sides on the AI (`--balance`), armies of the same level should come out near even.

## Graphics: verified setups

`dotnet run -- --gpu-report` writes `gpu-report.txt` (driver, shader status, frames per second at each quality). Please send it with any
graphics bug. Verified so far:

| Setup | Shaders | High / Medium / Low |
|---|---|---|
| Linux, Mesa llvmpipe (software, Xvfb), .NET 10 | compile and run | 12 / 31 / 46 fps at 1280x720 |

Real GPUs (Windows NVIDIA, AMD and Intel; macOS; Linux Mesa hardware drivers) are not verified yet. If the shaders fail on a driver,
the game falls back to Low by itself.

## Next steps

The roadmap (`docs/ROADMAP.md`) is built; its "As built" section lists what was simplified. Next: play-testing and balance
(`--balance` CSVs), real-GPU reports, animated water, torch light at night, and bridge models on the map.
