# The men of the company: plan

Status: **planned** (nothing built yet). Phases C1-C8; each ships on its own and `tools/check.sh` must pass after each.
When a phase ships, add its "As built" note at the end. Numbers marked *(tune)* are starting values.
Read `docs/archive/LEGACY.md` first: this plan is its second half.

## 1. Why this, and why now

The one fantasy (`docs/DESIGN.md` section 0):

> **You lead a free company in person. Every man you lose was someone you knew, and every honour on the banner is one
> you earned.**

Every plan so far is built. Looking at the game as it stands (the campaign and battle shots of 2026-11-12), the battle is
strong and the world is full. The weak place is the middle of the fantasy: **the men themselves.**

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

**Not in this plan:** new realms, new troops, new magic, new battle mechanics. Battle changes here are small and only about
named men (C6). A short polish track (C8) fixes what the shots showed.

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

## 5. Build order and why

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

## As built

(Nothing yet.)
