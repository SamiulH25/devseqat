# The men of the company, and their captain: plan

Status: **planned** (nothing built yet). It follows `docs/WORLD_LIFE.md` (WL0-WL6 built, WL7 paused). Phases C0-C14 (C9-C13, the captain's part, and C14, arrows, added 2026-11-12 at the owner's asking); each ships on its own and `tools/check.sh` must pass after each.
When a phase ships, add its "As built" note at the end. Numbers marked *(tune)* are starting values.
Read `docs/archive/LEGACY.md` first: this plan is its second half.

## 1. Why this, and why now

The one fantasy (`docs/DESIGN.md` section 0):

> **You lead a free company in person. Every man you lose was someone you knew, and every honour on the banner is one
> you earned.**

The battle is strong, the world is full, and the last two sprints gave it sound (`docs/archive/AUDIO.md`) and a lived-in
map (`docs/WORLD_LIFE.md`). Looking at the screens (campaign and battle shots rendered 2026-11-12 from the commit before
those two sprints; neither touched the roll, the report, the household or the legend), the weak place is the middle of the
fantasy: **the men themselves.**

- **A lord you will never meet is a richer person than a man who has fought beside you for sixty battles.** Lords have a
  painted face, a house, kin, bonds, a want, a temper, memories of you and a talk screen (`docs/archive/LORDS.md`). A named
  man of yours is one row of text: a name, a troop, two numbers and a deed line (`MapScreen.RollPanel`).
- **The dead are a line of red text.** The battle report's FALLEN block and THE FALLEN list give a name and a count. You
  cannot picture who it was.
- **Nothing happens between the men.** No two named men know each other. 91 events, and only two can name a man.
- **You never choose anything about them.** The senior man becomes sergeant on his own (`RiseToSergeant`). Deeds arrive;
  you cannot reward one.
- **There is no place where the company is.** Between battles your men are a card with a number. The party screen (P), the
  household tab (L) and THE LEGEND tab are mostly empty panel.

So the next sprint gives the men faces, natures, ties to each other, a camp to stand in, and a memory that outlasts them.
It passes the feature test twice: it makes the player care about *their* men, and it lets them see what they have built.

**The second part is the captain (C9-C13).** Two things the owner feels in play:
- **Your gear does not grow with you.** The army climbs seven tiers through 287 troops; the captain has 30 pieces in five
  slots (`Data/gear.json`: 13 weapons, 4 shields, 4 bodies, 4 helms, 5 horses), each one a plain id you own or do not
  (`CaptainKit.Owned`). Nothing is found, improved, named or carried; there is no inventory, only an armourer's list.
- **The enemy does not care that you are there.** A man picks the nearest foe (`BattleGrid.cs`: you are one more body in
  the distance test). Nobody seeks the gold captain, nobody shrinks from him, the enemy general never sends anyone for
  him, and enemy bows never mark him. Only enemy champions think of you at all (`BattleChampion.ChampionWants`).
Both are about the same thing as the first part: the company is led *in person*, and the person should matter.

**Not in this plan:** new realms, new troops, new magic. Battle changes are about named men (C6) and about how the enemy
treats you (C12, C13); the rules of a blow do not change. A short polish track (C8) fixes what the shots showed.

## 2. What already exists (build on it, do not rebuild)

| Have | Where | Use |
|---|---|---|
| Named men: `Soldier` (name, troop, battles, kills, wounds, joined, deeds, knight) | `CampaignState.cs`, `Campaign/Roll.cs` | add fields with defaults; no save bump |
| Painted portraits from a `Face` record | `PortraitPainter.cs` (`Of`, `Paint`), `Portraits.Of`, `Ui.Face` | a new `Of(Campaign, Soldier, CompanyRecord)` |
| A named man's look in battle is seeded by his id | `Variety.Seed(u)` = `SoldierId * 7919 + 1` | the portrait must use the same visage, so the face on the roll is the face in the line |
| Bonds, memories and opinion between characters | `Campaign/LordBonds.cs`, `Characters.cs` | the pattern, not the code: men's ties are simpler |
| Talk screen with topics and lines from data | `Campaign/Talk.cs`, `Data/talk.json`, `MapScreen.Talk.cs` | reuse the panel and `Line` picking for men |
| Living puppets in a render target | `CreatorStage.cs` | the camp scene |
| Events with holes and effect verbs | `Campaign/Events.cs`, `Data/events.json` (`{man}` exists for `bury_named`) | new triggers and verbs for men |
| Effects on a company in battle | `Effects.cs`, `Squad.Mods` | a named man's nature as a small effect |
| Alerts, rescue, the bring-him-home goal | `BattleAlerts.cs`, `BattleRescue.cs`, `BattleDeeds.cs` | C6 |
| The captain's kit, the armourer, spoils from a beaten leader | `Gear.cs`, `Data/gear.json`, `Campaign/Armoury.cs` (`MigrateGear`, `BattleSpoil`, `TakeSpoil`), `MapScreen.Armoury.cs`, `Rules.CaptainType` | C9-C11 grow these; ids become items |
| Goods from the field, sold by the lot | `Campaign/Goods.cs` | they share the baggage with gear (C9) and feed the smith (C11) |
| One language for stat changes | `Effects.cs` `Mods`, read by `Unit.Mods` in `Hit`, `Damage`, `Move`, `Tire` | gear qualities and trinkets are `Mods`, no new maths |
| Target choice, champions that seek you, the AI general, alerts | `BattleGrid.cs` (`Nearest`), `BattleChampion.cs`, `BattleAI.General`, `BattleCaptain.SoftTarget`, `BattleAlerts.cs` | C12-C13 |
| The household guard that follows you | `Campaign/Household.cs` | C13: they close round you when you are marked |
| Own dice for the roll | `CampaignState.RollRng` | every new draw about men goes here, so the main stream does not move |

## 3. Rules for the whole plan

- **Headless first.** Natures, ties, choices and memory live in `Campaign/`; faces and the camp are drawing only.
- **The main dice do not move.** New draws use `RollRng`. `--selftest CampaignRun`'s pressure line must print the same
  before and after C1-C5.
- **Old saves load.** New fields have defaults; a man from an old save gets his nature and age on first sync.
- **Auto-resolve pays the same price** (LEGACY risk a): anything a fought battle does to a man, auto-resolve does by odds.
- **Small numbers.** A man's nature is flavour with a light touch on his company, never a build to optimise.
- **Six men a company stays** (`RollSize`). Depth comes from knowing six men well, not from more names.

## 4. Phases

### C0: Green checks first
Two things are broken at the head of the branch today (2026-11-12), and every later phase needs both tools.
- `docs/CODEMAP.md` was stale after the world-life commit (six new files missing), so `tools/check.sh` failed.
  Regenerated with this plan.
- **The campaign shots run crashes** at the siege step: `ScreenHost.CampaignShots` case 12 calls `c.Besiege(castle)`,
  ignores a refusal, and case 13 passes a null `c.Besieging` to `PrepareAssault` (`NullReferenceException` in
  `GarrisonParty`, CampaignSiege.cs:42). Only 16 of about 48 screens are saved. Most likely the staged party no longer
  stands where `Besiege` accepts it on the layout 4 map. Find the refusal, fix the staging, and make the script stop with
  the reason when a staged action is refused.
- **Done when:** `tools/check.sh` ends `ALL CHECKS PASSED` and `IRONFIELD_CAMPAIGN_SHOTS` saves every screen.

### C1: Faces in the ranks
The cheapest phase and the biggest change to how the game reads.
- `PortraitPainter.Of(Campaign c, Soldier m, CompanyRecord co)`: visage from `Variety` with the same seed the battle uses;
  age from `Soldier.Age` (new field, drawn 17-30 on naming, rises with the year in `AgeDaily`); a scar per two `Wounds`;
  dress by his troop's tier (cap and cloth, mail, plate collar); the company's banner charge and your colours behind him;
  a knight of your order wears its mark. Mood from his company's cohesion (warm, even, cold).
- Draw it: each row of the roll (`RollPanel`), the report's FALLEN / HURT / NAMED lines (the fallen's face greyed),
  THE FALLEN list, the sergeant on the company card, the companion rows on the household tab.
- In battle: hovering or standing near a named man shows his small face beside his name (`Hud.NamedMarks`).
- `--portraits` gains a row of named men at tiers I, III, V, VII, fresh and scarred.
- **Tests:** `MenHaveFaces` (two men differ; one man's face is stable across save and load; wounds add a scar; the
  battle visage and the portrait visage are the same).
- **Check:** `campaign-roll.png`, `campaign-results-roll.png`.

### C2: A man of his own
- **Nature:** `Soldier.Nature`, one word, drawn on naming, weighted by the deed that named him *(tune)*: Steady, Bold,
  Grim, Merry, Pious, Sly, Proud, Gentle. Shown beside his name. Each does one small thing through his company
  (`Squad.Mods`, at most one nature counted per company, the senior man's): Steady +2 morale floor, Bold a quicker
  charge, Merry cohesion heals faster in camp, Grim less shaken by losses, Pious feeds blessings, Sly a better ambush,
  Proud fights harder beside the banner, Gentle more wounded live. Auto-resolve: `Balance.NatureWeight` *(tune: 0.01)*.
- **Home and age:** `Soldier.Home` (the village his company was raised in, or where he was recruited) and `Age`. Past 50
  *(tune)* he may ask to go home (C4).
- **His own deeds:** deeds from what the man did are kept as they are; add "his first battle", "wounded at X and came
  back", "last of the first six". The first deed still never rolls off.
- **Tests:** `MenHaveNatures` (drawn on `RollRng`; the main stream unchanged; one nature a company in battle; old saves
  fill in).

### C3: Ties between the men
- `Soldier.Ties`: at most two, each `{ Other (soldier id), Kind }`. Kinds: **Brothers** (named in the same battle, or
  five battles side by side in one company), **Kin** (same home village, 1 in 4 *(tune)*), **Rivals** (two Proud men, or
  two with near-equal kills), **Master and lad** (a man of 10+ battles and one newly named).
- What a tie does:
  - A brother or kin falls: the other gets the deed "saw X fall at Y" and for his next battle fights as *avenging*
    (a small damage rise, a greater risk). The company's cohesion loss for that death is half again.
  - Rivals: both earn names for kills a little faster; a camp event may set them at each other (C5).
  - Master and lad: the lad's survival odds use the master's; if the master falls the lad takes his nature.
- Ties follow men through splits, merges and promotion (`MoveNamed`, `SyncRoll`); a tie to a dead man becomes a memory
  line on the living one.
- The roll shows ties as a short line under the deed ("BROTHER TO LORIN BRIGHTBLADE").
- **Tests:** `MenHaveTies` (formed by the rules, survive a merge and a save, the fall of one marks the other).

### C4: Your hand on the roll
Choices, so the roll is something you do and not only something you read. All are `string?` actions on `Campaign`.
- **Choose the sergeant.** When a company needs one and two or more named men qualify, the map raises a choice (a `Beat`
  with the candidates' faces, natures and records) instead of taking the senior man. His nature becomes the officer
  trait where one fits (Steady -> Steady, Bold -> Hothead, Grim -> Stubborn...). Unanswered after a day, the senior
  man rises as now. `RiseToSergeant` keeps its draw.
- **Mention a man.** After a won battle the report lets you name one man of the day (from those with a deed in it): a
  purse *(tune: 20 crowns)*, +1 cohesion for his company, his deed marked with a star. Men mentioned three times are
  "of the banner": their name is read first on the roll and in the end summary.
- **Give him arms.** A piece of captain's gear or a lot of looted arms from `Goods` can go to a named man: he survives a
  little more often and the deed line says whose sword he carries. If he falls on a lost field, it is lost with him.
- **Send him home.** An old or thrice-wounded man can be pensioned (`Pension`, *(tune: 40 crowns)*): he leaves the roll
  alive, is written in the chronicle, and his village's recruits come a little readier (`Home`). Refusing a man who
  asks costs cohesion.
- **Move him.** `MoveNamed` already exists; give it a button on the roll (to a company of his role with room).
- **Tests:** `RollChoices` (each action's refusals and effects; the unanswered sergeant falls back; save round trip).

### C5: The camp
A place where the company is. New map screen on a free key (`Act.Camp`, default **C**), and the REST tab opens it.
- **The scene:** your men at rest, drawn like the creator's stage (`CreatorStage`): fires, the banner planted, tents,
  named men standing or sitting where you can see them, each under his own face mark. It follows the hour, the weather
  and the season, and the mood: after a rout the fires are few and men sit apart; after a named battle they cheer.
  Wounded named men lie by the surgeon's fire.
- **Click a man:** his page. The face large, nature, home, age, record, deeds in full, ties, whose sword he carries, and
  the C4 buttons. A short **talk** (reuse `MapScreen.Talk.cs`): ask how the men are (a true line about cohesion, pay,
  food, the last battle), ask where he is from, ask about the man he is tied to. Lines in `Data/talk.json` under new
  topics, picked by nature the way lords' lines are picked by temper.
- **Camp events:** a new trigger family `men` in `Events.Find` that picks one or two named men, with holes `{man}`,
  `{other}`, `{company}`, `{home}`. About twenty events *(tune)*, each grown from state that is true: two rivals come to
  blows; a man asks leave to bury his father at `{home}`; brothers ask to stand together in the front rank; a man is
  caught selling the company's bread; the lad wants to carry the banner; an old man tells the story of a battle on the
  banner (cohesion); a man wants to marry in the town you rest in; the men ask you to drink to a fallen man on the
  day he fell. New verbs: `man_deed`, `man_hurt`, `man_leaves`, `tie`, `mention`.
- **Rites move here.** The camp rites, the feast and the spellbook buttons get a home in the camp beside the men they
  belong to (the party screen keeps its links).
- **Tests:** `CampEvents` (`ValidateEvents` knows the trigger, holes and verbs; an event never names a dead man),
  `CampTalk` (`ValidateTalk`). **Check:** `campaign-camp.png`, `campaign-man.png`, and the lab page.

### C6: Named men in the fight
Small, and only what makes you look for them.
- **Hard pressed.** An alert when a named man of yours is under half health with a foe on him and you are within 60 m
  ("JEHAN THE GENTLE IS HARD PRESSED"); clicking it looks there (`BattleAlerts`). At most one such alert every 20 s.
- **Men speak.** A feed line with his face when a named man fells a captain or a horseman, when a brother falls, when
  he is pulled out ("GOT HIM. HE LIVES."). Lines by nature, from data.
- **The streamers on the banner.** `LEGACY.md` L5 left the battle honours off the 3D banner. Paint them: one streamer
  per battle honour (up to 3) and the fringe for 10+ battles, on `Puppet.Banner`, readable at 60 m.
- **Avenging and the lad** from C3 show in the HUD mark's colour.
- All drawing and logging; the only sim change is the avenging effect, on the battle `rng`.
- **Tests:** `NamedInTheFight` (the alert raises and is rate-limited; replay exact). **Check:** `IRONFIELD_SHOTS`.

### C7: What is remembered
- **The book of the fallen.** A full screen from THE LEGEND and from each company's roll: every dead man with his face,
  grouped under the battle he fell in, with the deed he was known for and who he left behind (ties). Sort by battle or
  by company.
- **Graves on the map.** A named battle where a named man of yours fell leaves a small cairn (a `Site`, kind `grave`,
  yours only). Ride there: an event names the men under it; the company that fought there gains cohesion once a season.
  The map keeps at most twelve *(tune)*, the oldest going back to grass, though the book never forgets.
- **The day he fell.** On the year's day of a battle that cost three or more named men, a line in the feed and a camp
  event.
- **The end summary** (`Record.Summary`, `Legacy.CompanyStory`) shows faces: the men of the banner, the longest-serving
  dead, who rose from the ranks.
- **The captain can die** (LEGACY L7, not built): a new-campaign option, off by default. On: a fall on a lost field may
  kill the captain *(tune: 1 in 6)*, and `HandOver` passes the company to the senior companion from the ranks at once.
  The dead captain goes first in the book.
- **Tests:** `BookOfTheFallen`, `GravesOnTheMap` (placed, capped, seeds' maps unchanged: graves are save state, not
  generation), `CaptainCanDie`.

### C8: Polish, pilot, docs
What the 2026-11-12 shots showed, and the tidy-up.
- **Overflow:** `campaign-tavern` "TAKE INTO SERVICE" leaves its button. The battle order buttons print raw key names
  that do not fit ("COMMA", "PERIOD", "SLASH", "SEMICOLON" is cut): show the glyph (`,` `.` `/` `;`).
- **Empty panels get the men:** the party screen's lower half shows the company's last battle and the men named in it;
  the household tab gives each companion a full card (face, calling, level, where he rose from, his bond with you);
  THE LEGEND lists every company with its honours as banners, not one text row.
- **Companion line bug:** the household row prints ". AGE 30" with nothing before the dot when a companion has no traits.
- **The pilot plays it:** the stand-in captain chooses sergeants, mentions men and pensions the old, so `--pacing` and
  `--balance` cover the new actions. Compare the auto-resolve rate before and after (LEGACY risk a).
- **Docs:** README (the roll, the camp), `docs/DEVELOPING.md` (5.6 layer row, 8.x data), `CLAUDE.md` file guide, a UI
  lab page for the man's page and the book; move this file to `docs/archive/`.

### C9: Gear that is a thing, and a baggage to carry it
The data model. Headless, no new screen yet (the armourer shows the new fields).
- **Items, not ids.** `Item { Id, Def (gear id), Quality, Marks (0-2 effect ids), Name, From, Kills, Battles }` in a new
  `CaptainState.Baggage` list; the worn slots hold item ids. `MigrateGear` turns an old save's `Owned` ids into plain
  items, so no version bump.
- **Quality:** Worn, Plain, Fine, Masterwork, and **Named** (one of a kind). Each step scales the piece's own numbers a
  little *(tune: 0.9 / 1 / 1.06 / 1.12)* and raises its price; Fine and up may carry a **mark**, a small `Mods` effect
  from `Data/effects.json` (Keen, Balanced, Well-tempered, Light, Sure-seated...). Marks are sideways choices, not only
  more.
- **More slots:** hands (gauntlets), legs (greaves, split out of `body`), cloak, horse harness (saddle cloth to full
  barding), and two **keepsakes** (a ring, a saint's knuckle, a wolf's tooth: no look, one small effect each). The Look
  already has the parts (`hands`, `legs`, `feet`, `cape`, caparison), so the captain's sprite shows every piece.
- **More gear, in grades.** About 90 pieces *(tune)* in five grades that mirror the troop tiers, so there is always a
  next piece to want: each of the seven cultures gets its own harness and arms (a steppe lamellar, a League brigandine,
  an Order white harness), sold in its own towns. Grades stay gated as now (skill, rank) plus renown for the top one.
- **Baggage:** gear and goods lots share a load, `Rules.BaggageRoom` (base, + pack horses you keep, + the Quartermaster
  perk). Over the limit the march slows; nothing is thrown away silently.
- **A piece has a story.** `From` ("taken from Lord Renaud Ironside at the Ford of Ashby, day 88"); it counts its own
  `Kills` and `Battles`. A weapon with 50 kills *(tune)* earns a name you may choose; a Named piece shows on your
  portrait's line and in the end summary.
- **Tests:** `GearItems` (migration from an old save, quality scales stats and price, marks apply through `Mods` in a
  battle, the load limit slows the march, save round trip). `Gear.Validate` checks the new slots and that every grade of
  every slot has a piece.

### C10: The inventory screen
- New map screen (`Act.Inventory`, default **I**): the captain large on the left, drawn as the living puppet wearing
  what is equipped (`CreatorStage`), a slot round him for each piece; the baggage on the right as a grid of pieces with
  their painted sprite part, quality colour and marks; goods lots below with the load gauge.
- Hover compares with what you wear, number by number, as the armourer does now (`GearStats`); click equips, right-click
  drops or sends to a companion. You can change kit anywhere outside a battle, not only at an armourer.
- **Companions and named men wear gear too.** A companion has weapon, body and helm slots filled from your baggage (his
  hero unit takes the look and numbers); C4's "give him arms" becomes handing a named man one item. What they wear comes
  back if they live, and is lost with them on a lost field.
- The battle's weapon swap (N) cycles every weapon you carry on you (at most three), chosen on this screen.
- The MARKET tab's armourer becomes buy and sell against the same grid; the paper doll on the K sheet links here.
- **Tests:** `InventoryActions` (equip, unequip, hand over, drop: refusals and effects). **Check:**
  `campaign-inventory.png`, a UI lab page, `overflow.txt` clean.

### C11: Where gear comes from
So that better kit is earned across the whole game, not bought in one visit.
- **Spoils are items.** `BattleSpoil` draws a quality by the beaten leader's tier and names where it came from. Lords
  drop their own culture's kit; champions and order knights drop Fine or better; a slain grand master or brigand king
  drops a Named piece.
- **Trophies.** Monster leaders give hides, teeth and bone that a smith works into gear only they can make: a wyvern-hide
  cloak, a troll-bone maul, a Brood Mother's silk gambeson, a wolf-alpha pelt. One recipe a monster kind, in `gear.json`.
- **The smith** (a town's MARKET, better with a prosperous town and its buildings): **mend**, **improve** one quality
  step for crowns and goods lots (arms, armour, silver plate), **re-mark** a piece, **work a trophy**. Masterwork needs a
  great town's smith and time (come back in some days).
- **Other sources:** tournament prizes by the lists' rank, ruins and old battlefields (`Search`), an order's gift at each
  rank (its white harness piece by piece), a liege's gift on a rank beat, a lord's reward for a want you met, a
  caravan's rare stock, a wedding gift.
- **Wear, lightly.** Only when you fall in a battle may one worn piece be **battered** (its quality counts one step lower
  until mended). No per-blow durability.
- **Sets.** Three or more pieces of one culture's or order's harness give a small named effect; a full Named set is the
  end of the road.
- **The pilot** buys, mends and improves, so `--pacing` shows gear spend against army spend; aim for about a fifth of
  income *(tune)*.
- **Tests:** `GearSources` (spoil quality by tier, trophy recipes valid, smith refusals and costs, battered and mended,
  set effect), `Calibration` and `TierFight` unchanged (the captain is one man).

### C12: The enemy sees you
Target choice, so that where you stand changes the fight round you. All on the battle `rng`; replays stay exact.
- **Notice.** In `Nearest`, an enemy weighs the captain as nearer than he is, by how much he is worth noticing: your
  renown title, aura tier, kills this battle, being mounted, the banner beside you *(tune: up to x0.5 distance)*. A man
  already at blows with someone keeps his man.
- **Places round you.** At most a set number of enemies may hold you as their target at once (`Battle.MarkedBy`,
  *(tune: 2 / 3 / 4 by difficulty; +1 against an Aura Expert or better)*); the rest fight your men. This is what stops
  "noticed" from meaning "buried".
- **By grade** (`Squad.Grade`): **rabble** go for you in a rush when you are alone and hang back when you have just
  killed (they give ground for a moment, guard up); **trained** men turn a file to face you when you come at their
  flank; **veterans and elites** send their best: the sergeant, or the man of the highest tier in reach, steps out
  to meet you, and the men beside him cover his sides instead of crowding.
- **Fear and nerve.** Your kills already hearten your men; now each kill in sight shakes the company you are cutting
  into, more with renown *(tune)*, and a company you have broken through twice wavers when you turn back at it. The
  other way: a company that brings you down takes heart for the rest of the battle.
- **You can see it.** A red mark at the screen's edge for each enemy that holds you as his target, a count by your
  health ("3 ON YOU"), and a different mark for a man stepping out to meet you.
- **Tests:** `EnemySeesCaptain` (a famed captain is picked over a nearer levy; the cap on places holds; a man at blows
  keeps his foe; rabble give ground after a kill; replay exact). The idle-player self-test battles will end sooner;
  that is expected.

### C13: The enemy hunts you
The general, the bows, the lord, and your own men's answer.
- **The general's order.** When you have done real harm (kills, a company broken, his horse stopped) or stand far
  forward, the AI general names you a target: a reserve company, or his horse, gets the order "the captain"
  (`General`, with a new squad target kind), once and with a cooldown. An alert tells you ("THEIR HORSE IS SENT FOR
  YOU"), and the order is cancelled if the company is needed to hold the line.
- **Bows mark you.** Elite bows may put a volley on a captain in the open and out of the melee: a warning ring first,
  as casts have, and never while you are among their own men. Trained bows do it only when you are mounted and alone.
- **Lords with a grudge.** A lord who remembers that you beat him, slew his kin or insulted him, or whose want is
  revenge on you, comes for you himself with his guard (bold and reckless at once, cautious when you are hurt). The
  encounter panel says so beforehand ("LORD RENAUD HAS SWORN TO HAVE YOUR HEAD"). A brigand king or monster leader
  with you as `Prey` does the same. A lord who respects you offers single combat in the field instead: his men hold
  back while it lasts, as in the clash of champions.
- **His guard.** The enemy captain's nearest men close round him when you come within 20 m, so reaching him is a fight
  and not a ride past.
- **Your men answer.** When two or more hold you as their target, the household guard and the nearest company of yours
  not at blows step in to cover you ("TO THE CAPTAIN!"), unless you have ordered them to hold. A named man who saves
  you earns the deed (C2).
- **It is worth it.** New personal goals ("the men sent for you: none get home", "stand when four come for you"), and
  the report's line for what the enemy threw at you.
- **Difficulty** scales the cap, the general's cooldown and the bows; Easy keeps the notice and the feedback but not the
  volleys.
- **Tests:** `EnemyHuntsCaptain` (the order goes out once, is recorded and replays; bows never mark you in a melee; a
  grudge lord seeks you; the guard closes; your men cover you), `Calibration` and `CavalryBalance` green; `--balance`
  win rate of even fights with the stand-in captain within noise of before.

### C14: Arrows you can see
Drawing only: `Battle.Arrows` and every hit stay as they are, so replays and the self-test numbers do not move.
Today an arrow in flight is one thin line with a faint streak behind it (`BattleScreen.Draw.cs`, the loop over
`battle.Arrows`), a javelin is the same line drawn twice, and a spent arrow is a shorter line. A volley of three hundred
reads as static, and you cannot tell a longbow shaft from a crossbow bolt or see where the arrows are falling.
- **A real arrow.** Each missile is a small sprite quad turned along its flight (`Puppet.Axial`, the "arrow" part the
  bowmen already hold): shaft, head and fletching, the fletching in the shooting side's cloth colour so you can tell
  whose arrows are in the air. Its own sprite by kind: arrow, short thick crossbow bolt, javelin, sling stone, fire pot.
  It pitches with its arc: nose up as it climbs, nose down as it falls.
- **A volley reads.** A company's volley leaves together and is seen as one dark flight against the sky; in command
  view far arrows thin to a light streak so three hundred do not turn to noise (by distance and Quality, as far men
  lose parts). A soft shadow of the flight crosses the ground under a big volley on High.
- **Where they land.** Arrows stand in the ground, at the angle they came down, and stay for the battle (capped, the
  oldest fading; they join the ground marks in `BattleScreen.Effects.cs`): a beaten zone you can read from above. They
  stand in shields (already drawn, `Unit.StuckArrows`), and now also in men who live, in horses, in the dead, in
  pavises, hedges, trees and house walls.
- **The hit.** A shaft that strikes a shield knocks it back a hand's width and throws splinters; on plate it glances
  away with a spark and spins off; in flesh the man jerks from the direction of flight (`Unit.HitFrom`). A miss kicks
  up a puff of dust, mud or water by the ground it lands on.
- **Being shot at.** In field view an arrow that passes close streaks past the camera with its whistle (the cue
  exists), and shafts thud into the ground round you. With C13, the warning ring before a volley marked on you is drawn
  as the shadow of the flight coming.
- **Your own shot.** When you loose, the camera follows your arrow a moment if you hold the key *(tune)*; it leaves a
  brighter trail than the rest so you can find where it fell, and a hit shows its range ("84 M").
- **Special shots show.** A plunging shot (longbows, tier V and up) climbs visibly higher; a punching bolt flies flat and
  fast with a shorter trail; steppe riders' arrows leave from the gallop; fire pots trail smoke; a Transcendent's blade
  cutting an arrow leaves two halves falling.
- **Budget:** at most 0.4 ms a frame for missiles at 3,000 men on High *(tune)*; Low keeps today's lines. Counted in the
  shots' `render.txt`.
- **Tests:** none in the sim (nothing changes there); `ReplayExact` and the printed arrow losses must be the same before
  and after. **Check:** new staged shots `arrows-volley.png` (command view, a volley in the air), `arrows-field.png`
  (field view under fire), `arrows-ground.png` (a beaten zone after two minutes), and the press shots.

## 5. Build order and why

0. **C0** before anything: without the shots no screen in this plan can be checked.
0. **C12, then C13** straight after, as the owner asked: they are battle-only, need nothing else in the plan, and
   change how every fight feels from the first battle.
0. **C14** any time: it is drawing only and touches nothing else. It goes well beside C13, whose marked volleys it shows.
0. **C9, C10, C11** next, in that order (data, screen, sources). C10's handing gear to men waits for C4 if C4 is not in
   yet; the rest stands alone.
1. **C1** first: a day's work on code that exists, and every later phase is better with faces on it.
2. **C2, C3**: who a man is and whom he stands with. Headless, well tested, no new screens (lines on the roll).
3. **C4**: choices. From here the roll is played, not read.
4. **C5**: the camp, the largest phase. It needs C1-C4 to have something to show. Split it if it grows: scene and man's
   page first, talk and events second.
5. **C6, C7**: the fight and the memory. Either order.
6. **C8** last, though its two overflow fixes can go in with C1.

## 6. Risks

- **Too much to track.** Sixty named men at ten companies. Guard: the camp shows the men, not a table; one nature and two
  ties at most; alerts and events are rate-limited; nothing here must be managed to play well.
- **Save-scumming and auto-resolve** to protect loved men. Guard: auto-resolve applies the same odds; pensions and gear
  give honest ways to protect a man; watch the auto-resolve rate in `--balance`.
- **Balance creep** from natures, arms and avenging. Guard: one nature a company, small weights, `TierFight`,
  `Calibration` and `--agree` unchanged within noise.
- **Replays.** C6's avenging effect goes in through `BattleOptions`/`Company.Named` and the battle `rng` only
  (`ReplayExact`).
- **Portrait cost.** Up to sixty more cached faces plus the dead. Guard: paint on first draw, cache by `Face`, as lords do.
- **Save growth.** Ties and a few fields per man; the fallen are still one record each. Check a 240-day `--realms` save.

- **Being hunted is not fun if it is unfair.** Guard: the cap on places round you, warnings before volleys and sent
  companies, difficulty scaling, your men's answer, and `--balance` on the stand-in captain's survival.
- **Gear power creep.** The captain is one man in three thousand, but duels and trials are him alone. Guard: qualities
  move numbers by a few per cent, marks are small `Mods`, and the duel and trial tests are run with the best and the
  worst kit.
- **Inventory as a chore.** Guard: one load number, no per-blow wear, a SELL ALL PLAIN button, and nothing that must be
  managed between every battle.
- **Old saves.** Kits migrate to items on load; a save from before C9 must open with the same numbers in battle
  (`GearItems` checks it).

## As built



**C0 (2026-11-12).** The siege step of the campaign shots topped each company to 20 men, but `CanBesiege` wants
`Scaled(20)` (60 at ArmyScale 3), so `Besiege` refused silently and `PrepareAssault` got a null target. The staging now
scales with `Balance.ArmyScale`, and a refused staged step stops the run with its reason (`ScreenHost.StagingRefused`).
All 53 campaign screens save; `overflow.txt` is empty.

**C12 (2026-11-12).** `BattleNotice.cs`. A foe's nearest target goes through `Notice` (in `Think`, after the router
check): the captain's distance is multiplied by `1 - 0.5 * Fame()` (renown full at 500 via the new `BattleOptions.Renown`,
aura tier, kills this battle, mounted, the banner within 8 m); a man within reach of someone else keeps him; leaders keep
their own logic (`ChampionWants`). `MarkCap` = 2 / 3 / 4 from `EnemyDamage`, +1 at aura tier 4; past it a foe takes your
nearest man within 10 m instead. Rabble: x0.7 when no man of yours is within 5 m of you, and give ground 1.5 s within 6 m
of your kill. Veteran and elite: one man (`MeetsCaptain`: highest tier, sergeant, nearest) steps out at x0.6 and does not
wait for a place; the rest keep their fight. Each kill shakes the victim's company within 25 m by `2 + 4 * Fame` (+10 at
the fifth and tenth man you cut out of it); the company that kills you gains 20 morale and a floor of 12. Simplified: the
"trained turn a file to face you" habit is left to the ranks' existing guard facing. HUD `Hud.Marked.cs`; staged shot
`captain-marked.png`.

**C13 (2026-11-12).** `BattleHunt.cs`, run from the end of `General(1)`. The general sends for you from 10 s in when you
have 6 kills (12 for a second time, and so on) or stand 30 m ahead of your line's centre: his nearest horse not at blows
and outside a division, else his reserve; cooldown 90 / 60 / 40 s by difficulty; the order lapses after 45 s, below 30%
of the company, or when you fall. The company fights free (not from its ranks) and goes for you unless already at blows.
Bows: elite, or trained against a lone rider, within range - 5 m, you visible and no man of theirs within 8 m of you;
warning 1.6 s (`Flashes` "mark", HUD ring and "ARROWS IN"), then every man with arrows looses at you in a 0.6 s window
unless their own men reach you; 25 s per company; none on Easy. The guard: the enemy captain's six nearest footmen take a
half ring 1.9 m in front of him while you are within 20 m. A grudge lord (`GrudgeOf`: his want is revenge on your house,
or his opinion of you is -40 or less) charges from the start and holds back below 35% health; the encounter panel shows
his line. Cover: with two or more on you (the sent company counts when within 25 m) your household guard on Follow and
your nearest idle foot company on Hold within 35 m go for the foes on you for 10 s. Goals `hunters` and `stand` are
added in the fight, not drawn. Not built (left for later): the respected lord's offer of single combat in the field,
brigand kings and monster leaders hunting by `Prey`, a named man earning a deed for saving you, the report's line, and a
`--balance` comparison (the self-test's `Calibration` and `CavalryBalance` stay green).

**C14 (2026-11-12).** `BattleScreen.Arrows.cs`, drawing only (`ReplayExact` prints the same before and after: 4845 ticks,
winner 1 at 80.8 s, 93 dead). Within 35 m of the eye a missile is a sprite quad along its flight (`Puppet.Missile`:
the bowmen's "arrow" part at half width, the bolt shorter and thicker, the javelin part) with a band of the shooter's
cloth over the fletching; farther it is a bright head and a light streak. Arrows that come down are kept for the battle
(1,500 at most, 3 per 0.7 m patch so a thick fall reads as a thicket), standing at least 55 degrees, leaning the way they
flew, the point sunk; a puff of water, mud or dust where each lands. Low quality keeps the old lines. Staged shots
`arrows-volley.png`, `arrows-field.png`, `arrows-ground.png`. Not built: arrows standing in living men, horses, the dead,
pavises and walls; the shield knock, plate glance and flinch; the close whistle past the camera; following your own arrow
and its range; plunging and flat shots drawn differently; fire pots; the flight's shadow; a volley that leaves together
(that is the sim's timing, out of a drawing-only phase); a frame budget measured at 3,000 men.

### C9 (gear as things), as built
- `GearItem { Def, Quality, Marks, Name, From, Kills, Battles }` in `CaptainState.Baggage` (Campaign/Items.cs). The kit's
  slots still hold gear ids (the look and old code read them); a slot's piece is `Campaign.ItemOf(c, def)`. One piece of
  each kind: a better make bought or taken replaces the one you hold. Free plain starting gear needs no item.
- `MigrateGear` turns an old save's `CaptainKit.Owned` into plain pieces and empties it; no version bump.
- Quality Worn / Plain / Fine / Masterwork / Named scales damage (weapon), block (shield), armour (body, helm) and a horse's
  health by 0.9 / 1 / 1.06 / 1.12 / 1.15, price by 0.6 / 1 / 1.6 / 2.5 / 4, plus a fifth per mark (`PriceOf`).
- Six marks in `Data/effects.json` tagged `mark` and the slots they fit: Keen, Balanced, Well-tempered, Light, Sure-seated,
  Sure-footed. `Rules.CaptainType` adds them to the captain's `UnitType.Mods`; a weapon's mark only while it is in hand
  (`SwapWeapon` resets `Unit.Mods`).
- The armourer offers some stock fine (a few masterwork in towns) with a mark, seeded by place and week (`StockMake`). Rows show
  the make (tinted green fine, blue masterwork, gold named), mark chips with their line on hover, the blade's kill tally, where a
  piece came from on hover, and a baggage line with a bar. Sell price moved to the SELL button's tooltip.
- Baggage: `BaggageRoom` = `Balance.BaggageBase` 20 + `BaggagePerHorse` 5 per spare horse (to six), +20 % with Quartermaster;
  `BaggageLoad` = gear not worn (armour 2, horses 0) + goods per ten lots. Over the room the march is 2 % slower a piece, to 25 %.
- Story: spoils and purchases set `From`; each fought battle adds to worn pieces' `Battles` and your kills to the main weapon's
  `Kills`. At 50 kills the armourer offers a name from `BladeNames` (NAME, OTHER: there is no keyboard entry yet); a named blade
  becomes Named quality and is a chronicle deed.
- Not built yet (left for a C9b): the new slots (hands, legs, cloak, harness, keepsakes), the ~90 graded culture pieces and the
  `Gear.Validate` checks for them, renown gating, the Named piece on the portrait line and the end summary.
- Test `GearItems` (Tests/GearTests.cs).

### C10 (the inventory screen), as built
- `MapScreen.Inventory.cs`, `Act.InventoryScreen` (I on the map; I stays Form2 in battle), also a KIT button over the captain
  card and a line in the F1 help. Left: the captain drawn in his kit (`ui.Portrait` of `Rules.CaptainType`, as on the K sheet;
  not the living `CreatorStage` puppet) with six slots round him (click one to take it off), his battle numbers, and a detail
  panel for the picked piece (make and what it adds, numbers, the change against what you wear, marks, where it came from).
  Right: every piece you can put on as a grid (make colour: green fine, blue masterwork, gold named) with WEAR / SECOND /
  DROP for the picked one; goods lots and the load gauge under it. Kit changes anywhere outside a battle.
- Actions in Campaign/Items.cs: `TakeOff(slot)` (the main hand refuses; the rest give way to the free plain piece),
  `DropItem` (refused when worn, named or free), `Wardrobe`. Test `InventoryActions`; shot `campaign-inventory.png`.
- Not built yet (C10b): companions and named men wearing gear from the baggage, N cycling up to three carried weapons, the
  armourer as buy/sell on the same grid, a link from the K sheet's doll, a UI lab page, the sprite part painted in each cell
  (an icon stands for it).
