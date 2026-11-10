# Lords: faces, houses, bonds and talk (sprint plan LD0-LD7)

The lords are the world's people, and today they are names on banners. This plan gives each lord a face, a house and
kin, real feelings about the other lords, a life of his own (wants, feuds, ambitions), and lets you sit down and talk with
any of them. It builds on what exists; nothing here replaces a system.

## What exists (the starting point)

- `Character` (Campaign/Characters.cs): name, culture, age, 2-3 of ten traits, `Opinion` by character id, up to eight
  `Memory` entries (what, weight, day) that fade 5% a week. Opinion of you is shown with its reasons in the L screen.
- Between lords: only `BaseOpinion` (foreign culture -10, clashing traits -15). Lords never remember anything about each
  other, so the web of feeling is flat. It is read by armies (a lord who hates the leader stays home), the council, defection.
- A slain lord's heir appears from nowhere with his father's grudges halved (`SlayLord`). No houses, no kin, no women.
- Faces: none for lords. `Hud.Portrait` draws a troop sprite; the captain has a `Visage` (skin x hair x beard, `Variety.cs`).
- Talking: none. You meet a lord only when he is hostile (the encounter panel: attack, auto-resolve, retreat, parley/pay,
  challenge). Friendly lords cannot be met. Lord quests arrive as events.

## Goals

1. **Every lord has a face** you learn to recognise, which ages, scars, and shows his mood towards you.
2. **Lords belong to houses**: fathers, sons, brothers, wives and daughters; heirs come from the house; a house remembers.
3. **Lords feel things about each other**: friends, rivals, feuds, kin; and it changes what they do (who rides in whose
   army, who backs whom in council, who defects, who comes to whose relief).
4. **You can talk to anyone**: ride up to any lord (or find him in his hall) and open a conversation with real choices:
   news, asking about others, favours, persuasion, threats, gifts, marriage.
5. **Lords want things**: a fief, revenge, peace, a crown; you can learn it, help or thwart it, and they act on it themselves.

## Phases

### LD0: houses and kin (data, headless)

- `House` in the save: id, name ("House Vael"), realm, arms (realm palette + its own charge and field, `Style.Emblem`),
  seat (a settlement or none), founder day, `Prestige`.
- `Character` gains `House`, `Father`, `Mother`, `Spouse`, `Children`, `Gender` ("m"/"f"), `Born` (campaign day; age is
  derived from then on, so people grow old), and `Title` ("Lord", "Lady", "Ser", "King", "Queen").
- World start: each lord gets a house with a family seeded around him: a spouse (often), 0-4 children of varied age, maybe
  a brother. Family members who are not leading a party are `Kind = "kin"`: they live at the house seat.
- Heirs: `SlayLord` takes the eldest grown son (then brother, then daughter) from the house; a house with nobody left dies
  out and its seat goes back to the ruler. Children come of age at 16 and may be given a party when the realm raises lords.
- Ladies: daughters and widows can lead a house (and a party) when no man is left; some realms' cultures allow women lords
  from the start (a culture flag in factions.json). Names: female name lists per culture in names.json.
- Tests: heirs come from the house, ages advance, houses die out cleanly, old saves load (lords without a house get one on load).
- **As built:** `Female`/`First` instead of `Gender`/`Title`, and `Age` still counts up each year (`HousesYearly`) rather than
  deriving from a `Born` day. A lord whose given name is in his culture's `ladies` list (names.json) is a lady, so women lead
  houses from the start in every realm (no culture flag). The house's family name is the founder's epithet ("the Grim"); the
  seat is derived (`SeatOf`: what the head's party holds). A house with no heir gives the party's banner to a new lord of a new
  house (or, once the campaign runs, a grown younger son of the realm's houses: `SpareKin`); the fief stays with the party.

### LD1: portraits (drawing)

- A painted **bust portrait** in code, in the game's pixel style: 48x60 px per face, drawn 3-6x. `PortraitPainter.cs`
  (drawing only) paints from a `Face` record on the character: skin, hair colour and cut, beard, eye colour, brow, nose,
  jaw width, plus age (grey streaks, lines, balding), scars (from battles and the Cruel/Reckless traits), and dress by culture
  and rank (circlet for a ruler, order tabard for a knight, mail for a warlord). Background: his house arms.
- Expression by mood: the mouth and brows shift with his opinion of you (warm, neutral, cold, hostile), so a face reads at a glance.
- Cached per character in the atlas or a small render-target sheet; repainted only when age band, scars or rank change.
- Used in: the encounter panel, the L screen (portrait cards instead of text rows), the new talk screen, events about a lord,
  the council, the battle report (the enemy commander), the map tooltip.
- `--portraits out.png` headless sheet (like `--sprites`) to check variety; a UI lab page; campaign shots.
- The captain's own portrait from the creator's face, so you sit across the table from them.

### LD2: bonds between lords (headless)

- Lords remember things about each other, using the same `Remember`: fought side by side, rescued in battle, lost a fief
  to him, he got the fief I wanted, slew my kin, married into my house, insulted me, backed me in council, deserted the army.
- Named **bonds** on top of opinion, each with a rule to form and to break: **Friend** (opinion > 50 and shared deeds),
  **Rival** (both want the same thing: a fief, the marshal's staff, a woman's hand), **Feud** (kin slain; lasts for
  generations, passed to heirs), **Kin** (same house, or by marriage), **Liege/Vassal** (as now), **Sworn** (oaths).
- What bonds do: armies (friends join each other's armies, rivals refuse, feuding lords may even fight across realms),
  relief (a friend rides to relieve a besieged friend first), council votes, defection (a lord with a feud with his king's
  favourite leaves), duels (rivals challenge each other), events ("Lord X and Lord Y have come to blows at the feast").
- The **web** view: in the L screen, a lord's card shows his bonds; a small graph of his house and his friends/rivals/feuds.
- Bonds with you are the same bonds: you can become a lord's friend, rival or blood enemy.

### LD3: talking (the core)

- **Meeting anyone**: right-click a lord on the map, or ride up to him, and you meet him (like meeting a caravan now). A
  hostile lord's encounter panel gains TALK beside ATTACK. In a town or castle, a **HALL** tab lists who is there (the
  holder, his kin, lords resting there) and you can talk to each.
- **The talk screen**: his portrait large, his house arms, his opinion of you with its reasons, what you know of him; a line
  of greeting that fits him (traits, opinion, culture, whether his realm is at war with you, what happened last time).
  Then **topics**, each a button that shows its odds and cost before you press it:
  - *Ask*: news of the war; where a lord is; about another lord (reveals that lord's opinions and bonds); about his wants.
  - *Request*: work (a quest from him, on the contract machinery); ride together (join his army, or ask him to follow
    you for a few days); a loan; release of a prisoner; passage; recruits from his villages.
  - *Persuade*: back you in council; support your claim; make peace with you or with another realm; leave his king and
    swear to yours (defection); spare a village. A **persuasion check**: Charm skill + his opinion + trait fit
    (an argument that suits him: gold for the Greedy, honour for the Honourable, glory for the Ambitious) against the size
    of the ask. Up to three arguments; each can raise or lower the odds; failing costs opinion and he remembers it.
  - *Threaten / demand*: surrender, tribute, give up a prisoner; odds from strength, your renown and his traits.
  - *Gift*: gold, a horse, a piece of gear, a prisoner of his enemies; what pleases depends on his traits.
  - *Insult / challenge*: start a rivalry, provoke a duel (uses the existing duel).
  - *Marriage* (LD6).
- **Data-driven lines**: `Data/talk.json`: topics, lines keyed by temper (from traits), opinion band, culture voice and
  context, with conditions and effects in the event verb language (`Campaign.ApplyEffect`), so writing more talk is data
  work. Validated like events (`SelfTest`).
- **Memory of talk**: asking again too soon annoys; a broken promise ("I will come to your relief") is remembered.
- Headless API on `Campaign` (`TalkTo`, `Topics`, `Say`, `Persuade` returning odds and outcome) so the pilot and tests can talk.

### LD4: lords' wants and ambitions (headless AI)

- Each lord has one or two **wants**, from traits and situation: a fief (landless, Ambitious), revenge on X (feud), peace
  (weary, Cautious), the marshal's staff, a crown (pretender), wealth (Greedy), a good match for his daughter, the death of a monster.
- Wants drive his AI: he asks for the fief in council, picks fights with his feud, lobbies the king for peace, joins a
  claimant's revolt. They drive his quests for you and the arguments that persuade him.
- You learn a want by talking (Ask about his wants) or from tavern rumours; helping a want is the strongest way to win a
  lord; thwarting one makes a rival.

### LD5: halls and courts

- Lords not in the field rest at their seat; their kin live there. The HALL tab shows a small court scene (portraits in a
  row, the holder seated), with who is present, and feasts and tournaments gather lords from around the realm.
- Holding a **feast** in your own hall (cost scales with guests) draws lords; talking to several at once; rivals at the
  same table may quarrel (an event).

### LD6: marriage

- Courting: a lord's unwed daughter or sister (or son/brother for a female captain): talk, gifts, deeds, then ask her
  father/head of house. A match ties you to the house (kin bond with all its members), brings a dowry, opinion with the
  realm, and an heir for your company's legend (`HandOver` / `Retire` read it).
- AI houses marry among themselves too, which shifts the web over the years (alliances between houses).

### LD7: AI pilot, balance, polish, docs

- The pilot (`Pilot.cs`) talks: asks for work, persuades lords to join it, makes friends; `--pacing` must stay on target.
- Tests per phase; a long run checks the web stays alive (friends, rivals and feuds form and end; houses do not all die out).
- README rules, CLAUDE.md file guide, DEVELOPING.md section on talk data; move this file to `docs/archive/` when done.

## Order and size

LD0 and LD1 first (they are what every later phase shows), then LD3 (talking: the biggest piece of play), then LD2 and
LD4 (they give talking something to be about), then LD5, LD6, LD7. LD3 can ship with a small topic set and grow through data.

## Decisions to make before starting

1. Portraits: a new painted bust (recommended) or the battle sprite's head drawn larger (cheaper, less character).
2. Talk: topic menu with persuasion checks and data lines (recommended), or longer hand-written branching conversations.
3. Women lords and ladies: in from LD0 (recommended: houses need wives and daughters for heirs and marriage anyway).
4. Marriage for the captain (LD6): in scope or later.
