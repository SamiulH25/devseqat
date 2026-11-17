# Playtest notes: Andrew, round 1 (PT0-PT5)

Status: PT1 built (2026-11-16); PT2-PT4 and PT6a-c built (2026-11-16); PT5 in progress (judged 6/10, not yet passed); PT6d waits on the playtester. This page keeps the playtester's notes as given, says what each one means in
the code, and orders the work. Read it first when choosing what to build next. Update the status line and the "As built"
section at the end as phases land.

The theme of the notes is one line from the playtester: **"Less is more, more is less."** The game does a lot and shows all
of it at once. The work is mostly about showing less, sooner, and in pictures.

## The notes (verbatim)

1. Quests should have a dedicated menu.
2. Many of the menus are way way too cluttered with information.
3. Make it so you dont have to click on the center of the town.
4. The game feels extremely unintuitive. Less is more, more is less.
5. when attacking the troops shouldnt need to constantly reform when sent on a combat command.
6. Combat commands should have more visual commands.
7. Crusader kings for the lordship and ai systems in the campaign and battles.
8. To go with 7 commander personalities should effect how a commander does in the battle.
9. Look into if you can pause the rendering for all entities when paused in the campaign map.
10. The weapons look more like bats isntead of sharp and should be held in teh hand with proper perspective handling.
11. The incombat ui especially the combat ui should be more visually intuitive.

## Grouping and order

| Phase | Notes | What | Size |
|---|---|---|---|
| PT1 | 3, 5, 9 | Three contained fixes: the town click, troops that keep re-forming on a charge, a paused map that really stops | small |
| PT2 | 2, 4 | The clutter audit: one job per screen, progressive disclosure, the same rules everywhere | large, touches every panel |
| PT3 | 1 | A quests menu (needs PT2's rules first, or it becomes one more crowded panel) | medium |
| PT4 | 6, 11 | Combat orders and the battle HUD shown as pictures, not rows of text | large |
| PT5 | 10 | Weapon sprites: sharp blades, held in the hand, right in perspective | medium (art) |
| PT6 | 7, 8 | Lords, houses and AI after the Crusader Kings model; commander personality decides battles | large, design first |

Do PT1 first (each is a bug or a one-liner and the playtester will notice them at once). PT2 before PT3 and PT4, because
both add UI and need the rules PT2 sets. PT6 is a design pass before any code: most of the machinery exists (see below).

## PT1 The three contained fixes

### Note 3: you should not have to click the centre of a town

Now: `MapScreen.ClickWorld` takes the ground point under the mouse and calls `C.SettlementAt(g, 6)`, which finds a settlement
whose centre is within 6 world units (`Campaign.SettlementAt`, Campaign.cs). The same 6 is used for the hover tooltip
(`MapScreen.Hud.cs` `Tooltip`) and the cursor. A town is drawn much wider than that and the map zooms from 25 to 620, so
the hit area is tiny when zoomed out and the town's walls, label and banner are not clickable at all.

Plan:
- Pick in screen space, not world space: a settlement is hit if the mouse is within its drawn footprint or its label/banner
  rectangle, or within about 28 px of its screen position, whichever is larger. Parties get the same treatment (they are
  picked with a fixed 5 units now).
- Make the nearest candidate win when several overlap, and show it: a soft highlight ring under the mouse before the click
  and the gauntlet cursor (`CursorKind.Hand`, already set from `Tooltip`).
- One shared `PickAt(Point)` used by `ClickWorld`, `Tooltip` and the cursor, so the three cannot disagree again.
- Test: a headless pick at several zooms and offsets (project a settlement, pick at its edge, its label, 20 px off).

### Note 5: troops should not keep re-forming after a combat command

Now: a company sent to charge goes through the plan machinery in `BattleTactics.cs`. For veteran and better companies,
`Plan.Reform` fires when the company is `Broken` (ranks lost) and not in contact while its order is `Charge`: it steps back
6 m, re-forms to 70% formed, then goes in again (`BattleTactics.cs`, the `Plan.Reform` block). Ranks also close in
`BattleRanks.cs` (`Close`, `CloseFiles`). To the player this looks like the company halting and shuffling every time the
line breaks up on the way in.

Plan (reproduce first, with a recorded replay of a charge over rough ground, then fix):
- A charge the player ordered keeps going: no step back, no re-form while closing on the target. Re-form only after the
  fight, or on an explicit order, or when the company is `Routing`.
- Keep the AI's behaviour (it is part of the grade differences in docs/archive/MELEE.md D5) but gate it on "was this order
  given by the AI?". A flag on the order (`Squad.Directed` or reuse `Override`) is enough.
- Reduce how often a moving company counts as `Broken`: ranks scatter on the move because of terrain and separation; a
  tolerance while `Charge` is moving would remove most of the stutter at the source.
- Test: a player-ordered charge over 100 m of broken ground never enters `Plan.Reform`; an AI one still can. `SelfTest`
  `Reforms` and `HorseRegroup` must still pass unchanged.

### Note 9: pause the rendering of every entity when the map is paused

Now: `MapScreen.Update` always calls `UpdateLife(dt)` (`MapScreen.cs`), whatever `speed` is. Only the campaign tick is
gated on `speed > 0`. So birds, boats, villagers, caravans' wagons, chimney smoke and the water shader keep moving on a
paused map, which reads as "the game did not pause".

Plan:
- Feed `UpdateLife` a zero step while `speed == 0` (and the shader clock the world meshes use), so everything freezes.
  Camera, UI animation (`Ui.Now`) and cursor keep working.
- Decide what "paused" should still allow: the player's marching banner and tooltips yes; ambient life no.
- Side win: the async life task (`lifeTask`) can skip its work entirely while paused, which saves CPU. Measure with
  `--campaign-bench` and the frame profiler (`FrameProf`, "mapLife").
- Test: with speed 0, two frames' life state is identical (positions, smoke, boats).

## PT2 The clutter audit (notes 2 and 4)

The complaint is general, so the fix is a set of rules applied panel by panel. Do an audit first, then fix the worst.

**Audit:** for every campaign panel and the battle HUD, write down what is on screen, what the player is deciding there,
and what is not needed for that decision. The panels are in the file guide in CLAUDE.md (`MapScreen.Hud.cs`, `.Settlement.cs`,
`.Muster.cs`, `.Captain.cs`, `.Party.cs`, `.Realm.cs`, `.Orders.cs`, `.Talk.cs`, `.Atlas.cs`, `.Report.cs`, `.Event.cs`,
`.Dialogs.cs`, `.Inventory.cs`; `Hud*.cs` in battle). Take a screenshot of each with the shots tooling
(`IRONFIELD_CAMPAIGN_SHOTS`, `IRONFIELD_SHOTS`) and put them in `docs/images/playtest/` as the "before".

**Rules to apply (add them to docs/DESIGN.md "Voice" and "Layout"):**
1. One job per screen. A panel answers one question ("what can I do here", "who is in my party").
2. Three levels: what you need now (always shown), what you may want (one click), the rest (a detail view or tooltip).
3. At most about seven items in any group; past that, group or page them.
4. Numbers earn their place: show a bar or an icon where a number is only compared; show the number on hover.
5. No sentence where a word will do; no label where an icon is clear. Tips and long notes move to a help layer.
6. The first screen of a campaign shows almost nothing: the map, your party, one next step. Systems appear when they matter
   (first town, first lord, first siege), with the existing `Tips` system introducing each once.
7. Same thing, same place, same look in every panel (back, close, tabs, action buttons).

**Likely worst offenders** (verify in the audit): the settlement panel and its many tabs (market, recruits, nobles, elders,
tavern, hall, chapter, workshop), the party screen (`Muster.cs`), the captain sheet (`Captain.cs`, with its forms, paths,
order, flame and own-form blocks), the realm screen, the battle HUD's command tray and divisions.

### PT2 audit (2026-11-16)

Before shots: `docs/images/playtest/before/` (`campaign/`, `battle/`, `battle-sergeants/`; 1280x720, tips not dismissed, as
a new player sees them). `overflow.txt` is empty in all three, so the clutter is not text spilling out of panels: it is how much
is on screen and how alike it all looks.

**Problems on every screen**
- A. The map HUD stays drawn round and under every full panel: the news feed and the status box peek out behind
  P, K, R, L, V and the town, so a panel never has the screen to itself.
- B. The map HUD alone is seven widgets: feed (6 lines, cut with ".."), LOG toggle (sits on a map label, and its own tooltip
  draws over it at the top left), time bar, minimap, an 8-button nav column, the status box (7 lines, including a sentence
  of advice) and the company cards. The status box repeats the party screen.
- C. One font size, all capitals, one weight: rank is shown by colour alone. Enabled buttons in dim grey on dark blue look
  disabled (TALK topics, P card buttons), so the player cannot tell what is clickable.
- D. Text cut with ".." in places that matter: the feed, realm cards ("LORD BRAND..", "CROSSBOWS AND LANCES.."), perk names
  ("SER-GEANTS..").
- E. Tips are three-line paragraphs of key lists in the middle of the screen.

**Panels, worst first**

| Panel (shot) | What it is for | What is wrong | Fix |
|---|---|---|---|
| Town (`campaign-town`, `-armourer`, `-tavern`) | What can I do here? | 12 tabs in one row; tabs with nothing in them (MERCENARIES: one row, 80% empty); MARKET does three jobs (stables, selling goods, the armourer with 5 sub-tabs) | Four groups: MEN (train, recruit, hire, esquires), TRADE (sell, armourer, food), TOWN (tavern, hall, elders, chapter), CAMP (rest, captives, contract). Hide a tab with nothing to do. Split MARKET into SELL and ARMOURER |
| Realms (`campaign-realm`) | Who rules what, who fights whom | Each realm card holds 9 facts (style text, counts, ruler, three troops); a 7x7 matrix with 3-letter codes; a 13-line chronicle, all on one tab | Card: shield, name, war chip, land bar; the rest on hover or click. Relations: your realm's chips only, matrix behind a toggle. Chronicle to its own tab |
| Career (`campaign-realm-king`) | What next on the ladder; war and peace | 15 identical wide buttons (DECLARE WAR / ALLIANCE x6, march x3) | One row per realm: name, state chip, one ACT button that opens the choices for that realm |
| Captain K (`campaign-skills`) | Spend a perk | 10 skill rings and 50 perk nodes at once | Show the 10 rings; a ring with a perk to choose glows; click opens that skill's tree. Two levels instead of one wall |
| Lords L (`campaign-lords`) | Who is this lord to me | 20 rows x 5 facts each, 9 tabs, the opinion bar has no label | Row: face, name, opinion chip (warm/cold), one trait icon, men. Sort by opinion or strength. House, age, all traits in hover |
| Party P (`campaign-party`) | Who is in my company | Repeats the K skills list (10 numbers); 4 buttons on every card; the bottom half is 4 bars and empty space | Drop the skills list (a button to K is there). UP/SPLIT/MERGE/DISBAND move to one action bar for the selected card. Bottom: totals as one row |
| Divisions V (`campaign-divisions`) | Who leads which companies | Text only, with instructions as sentences; two thirds empty | Cards per sergeant (face, temper icon, companies as chips); assign by clicking a company chip then a sergeant |
| Talk (`campaign-talk`) | Ask a lord something | All topic buttons look disabled; the opinion breakdown is red and orange lines | Normal button contrast; disabled ones say why on hover. Opinion as one chip, the reasons on hover |
| Map HUD (`campaign-map`) | Where do I go | See B | Status box: crowns, men against the cap as a bar, food; the advice becomes one NEXT chip. Feed: last line only, opens on click; LOG goes in its header. Nav column: icons with key hints |
| Battle HUD (`command-overview`, `field-view`, `sergeants-*`) | Command, or fight | About 12 widgets at once: score bar, minimap, 3 sergeant cards (4 lines + 2 bars each), alert feed, selected stats, captain bars, 9 cards in codes ("IV TRN HLD 53/53"), 13 text order buttons, key hints, reserve tray, field labels that repeat the sergeant cards, ground text, tip box. In field view (you fighting) all of it stays | PT4 does the order palette and cards. PT2 quick wins: in field view fold the cards to a thin strip and hide orders and sergeants; drop field labels the sergeant cards already show; tips one line in a corner |

Fine as they are, small fixes only: the event dialog (`campaign-event`, the model for the rest: a picture, one sentence,
two choices with their cost), the encounter (`campaign-encounter-lord`: make ATTACK the primary button, show the odds larger),
the battle results (`campaign-results`), the roll (`campaign-roll`), kit and baggage (`campaign-inventory`), the tavern.

**How to fix: order of work**
1. **PT2a shell, all screens at once** (A, C, D, E): hide the map HUD (feed, status box, cards, nav) while a full panel is
   open; one contrast token pair for enabled and disabled buttons in `Style.cs`/`UiKit.cs`; a second, larger heading size
   for panel titles and section heads; tips docked bottom-left as one line plus a key chip, the full text on hover.
2. **PT2b map HUD** (B).
3. **PT2c town** (the most used screen).
4. **PT2d party and captain**, then **PT2e realm, career, lords**, then **PT2f divisions and talk**.
5. **PT2g battle quick wins**; the rest is PT4.
Each step: re-render the same shots into `docs/images/playtest/after/`, judge (global visual gate, 7/10 or more), and
keep `overflow.txt` empty.

**Done when:** each panel passes the rules on a judged render (judge step in the global instructions, 7/10 or higher), the
playtester finds the first hour usable without the F1 key list, and `overflow.txt` is empty on every panel shot.

## PT3 A dedicated quests menu (note 1)

Now: accepted work lives in `CampaignState.Contracts` (`Contract`, Campaign/CampaignState.cs), offered by towns and elders
(`Campaign/Quests.cs`, `CampaignWar.cs`), shown as the `Contracts()` strip on the main HUD (`MapScreen.Hud.cs`), in the
town's ELDERS and tavern tabs, and as quest steps (`Contract.Then`), the relic chain, lord quests and events (`Events.cs`).
There is no one place that answers "what am I supposed to be doing?".

Plan:
- A QUESTS screen on its own key (add `Act.Quests`, show it on the F1 help and as a button on the top bar), a new
  `PanelKind.Quests` in `MapScreen.cs`, drawn by `MapScreen.Quests.cs`.
- Three groups: ACTIVE (with the next step, where it is, a deadline, the reward), AVAILABLE NEARBY (offers in towns you
  have visited), DONE/FAILED (a short history, from the chronicle in `Campaign/Record.cs`).
- Each quest has a "show me" button that moves the camera and marks the place, and a pin that puts one quest's next step on
  the HUD. The HUD strip shows only the pinned quest.
- Contracts, elders' jobs, order contracts, lord requests, the relic chain and event follow-ups all need one common view
  type (title, giver, goal text, place, reward, deadline, state). Write `QuestView` and a `Campaign.QuestList()` that builds
  it from the existing sources; do not change how the quests themselves work.
- Test: the list contains one entry per source after a seeded run; a completed quest moves to DONE.

## PT4 Combat orders and HUD you can read at a glance (notes 6 and 11)

Now: orders are buttons and keys (`Hud.cs` command panel, `BattleScreen.Orders.cs`), plus order arrows and slot previews
(`BattleScreen.Draw.cs`, `OrderArrows`), fight marks (`Hud.Fights.cs`), alerts (`Hud.Alerts.cs`), the command tray and
divisions (`Hud.Command.cs`, `Hud.Divisions.cs`). Much of it is text and short key labels.

Plan, in order:
1. **Show the order on the field.** Every ordered company gets a ground marker in its order's picture: a pennant for move, a
   red cone with crossed swords toward its target for attack, a shield wall line for hold, a dotted retreat arrow, a fan
   for fire. The selected company's order is drawn bold, others faint. The cursor already shows what the next click does
   (docs and `Cursors.cs`); the markers show what the last click did.
2. **Make orders pictures.** Replace the order grid's text rows (CHARGE, HOLD, ADVANCE, FALL BACK, FOLLOW ME, SPACING,
   FORMATION, FIRE / HOLD, RELIEVE, SPLIT, STANDING) with an icon palette of about six big buttons for the common orders and
   a "more" fold for the rest. Keys stay as small hints.
3. **Company cards as symbols.** A card shows the troop's picture, a strength bar, a morale colour, and one state icon
   (marching, fighting, winning, losing, broken, out of arrows). The long stat strip moves into a hover card.
4. **Say what is happening.** Reuse the fight reader (`Battle.FightOf`, `Hud.Fights.cs`) for one plain banner chip per
   company: "winning", "holding", "flanked", "breaking". Sergeant questions and alerts become toasts at the screen edge
   that point at the company, not rows in a list.
5. **Drag to order.** Dragging with the right button already draws a line; add a drag preview showing the formation's
   shape and the slot positions as ghost men, and a one-click "attack that" from the enemy card.
6. **Sound and feedback.** A short cue and a flash on the company when an order is received, a second when it is carried
   out (the courier delay of `BattleCourier.cs` is invisible today; draw the rider on the way).

Done when: a new player can give move, attack, hold and fall back to a company in a quick battle with no tip text, judged
on a render, and the screen at the start of a battle has no more than a card row, one order palette, and the minimap.

## PT5 Weapons that look like weapons (note 10)

Now: weapon parts are small PNGs in `Data/sprites/parts/` (`sword`, `longsword`, `greatsword`, `axe`, `daneaxe`, `club`,
`mace`, `spear`, bows) painted in `SpriteSheet.cs` and baked (`--bake-sprites`). They are placed by the rig in `Puppet.cs`
and posed by grip in `PuppetPoses.cs` (`Grip`, `Look.GripOf`, two-bone arms with `Puppet.Arm`/`ArmTo`).

The reading of the note is that blades are too short, wide and blunt (they look like bats) and are not held convincingly:
the hand does not sit on the grip and the weapon does not turn with the wrist and the camera.

Plan:
- **Silhouette first.** Redraw each blade longer and narrower with a point, a visible edge highlight on one side, a
  crossguard and a pommel, and a grip that is clearly in the hand (the hand sprite overlaps the grip, not the blade). Use
  `tools/sprite_tool.py show` and the `--sprites out.png` dump to judge each at 6x.
- **Grip point.** Give every weapon a grip anchor (the pixel the hand holds) and a tip anchor in the sprite data, so the rig
  can place the grip on the hand and aim the tip along the forearm's direction instead of using one offset per weapon.
- **Perspective.** The camera looks down at a tilt, so a held sword should foreshorten: draw each weapon in two or three
  views (side, three-quarter, edge-on) and pick by swing angle and camera yaw, as the heads already do (`head.v*.f/b/s`).
  Blows should show the edge turning through the swing, not a flat bar sliding.
- **Weapon trails and hits** keep working from the same anchors (`BladeTrail`, `BladeLanded`).
- **Variety** by troop: kite or round shields, spear lengths, polearm heads; spears and pikes read as thin poles with
  steel heads.
- Sprites are PNG, so no code is needed for pure art changes; run `--check` and the `SpritesBaked` test.

Done when: the pose sheets (`pose-grips`, `pose-stances`) and the clash gallery judge at 7/10 or higher for "reads as a
weapon, held in the hand" with the judge step in the global instructions, and a side-by-side at 2x shows the difference.

## PT6 Lords, houses and commander personality (notes 7 and 8)

"Crusader kings" is read as: lords with traits, ambitions, families and grudges that drive the campaign, and commanders
whose temperament shows in how a battle is fought. A lot of this exists, so the first step is a design audit, not new code.

**Already built** (see CLAUDE.md file guide): houses, kin and heirs (`Campaign/Houses.cs`), bonds, feuds and rivalries
(`LordBonds.cs`), wants and promises (`Wants.cs`), talk and persuasion (`Talk.cs`), lord traits (`Characters.cs`
`LordTraits`: Honourable, Cruel, Cautious, Reckless, Greedy, Generous, Pious, Ambitious, Loyal, Fickle), diplomacy, vassals
and revolts (`Diplomacy.cs`, `Fiefs.cs`), realm temper (`Factions.cs`), and in battle a division sergeant's `Temper` (Bold,
Steady, Cautious) with `Hothead` and `Stubborn` traits (`BattleDivision.cs`, `BattleCommand.cs`) and company wits by grade.

**The gap to close:**
1. A lord's own character does not reach the battle. The enemy general (`BattleAI.General`) plays one way whoever leads;
   traits matter only through sergeants' `Temper`. The lord that comes at you should fight like that lord.
2. Campaign AI decisions are driven by realm temper and a few traits, but the player cannot see why a lord did something.
3. The CK feel (schemes, claims, succession crises, vassal contracts, opinion modifiers with reasons) is partly there and
   mostly invisible.

**Plan:**
- **PT6a design page** (`docs/LORDS2.md`, or a section here) that lists, per lord trait and temper, a campaign behaviour
  and a battle behaviour, in one table, before any code. Add a short list of "stories" it must produce (the reckless lord
  who charges alone and dies, the cautious one who will not leave the hill, the honourable one who accepts a duel, the
  cruel one who does not ransom, the greedy one who sells peace).
- **PT6b commander personality in battle.** Pass the leader's traits and temper into `BattleOptions` (a `LeaderTraits` and
  `LeaderTemper` next to `EnemyLeader`) and read them in `BattleAI.General`, `BattleHunt.cs`, `BattleDivision.Brain` and
  morale: when to commit the reserve, how long to wait for the player to attack, whether to chase a broken enemy, whether to
  fall back at 25% losses or fight to the last, whether to hunt the player's captain. Your own commanders (sergeants, and
  your captain's hired lords) read the same table.
- **PT6c show it.** The encounter panel line for a lord says how he fights ("RECKLESS: CHARGES AT ONCE") and the after-action
  report says what his nature did ("HIS RECKLESS CHARGE BROKE HIS OWN LINE"). Opinion and AI choices get a one-line reason
  in the lord screen.
- **PT6d CK systems** to consider after the first three, each its own small plan: claims and succession fights, vassal
  levies and contracts, schemes (a lord plots against a rival), marriage alliances that bind wars, councils. Decide the
  scope with the playtester before building; the Crusader Kings reference is large and the game is a battle game first.
- Tests: the same two armies with a Reckless then a Cautious enemy lord diverge measurably (time to contact, losses,
  reserve commit time); replays stay exact (`ReplayExact`); `--balance`, `--realms` and `--pacing` do not regress.

## Open questions for the playtester

- Note 4 ("unintuitive"): which part was worst, the campaign map, a town, or the first battle? Where did Andrew get stuck?
- Note 7: which Crusader Kings parts did Andrew want, the lord's personality and family, the politics and intrigue, or
  the way the AI behaves toward the player?
- Note 10: which weapons looked worst (swords, spears, axes)? Seen from where, in the field view or the command view?
- Note 11: is "combat UI" the order buttons, the company cards, or the whole HUD during a fight?

## Test and judge plan

- Each phase: update or add a `SelfTest`, run `tools/check.sh` (it must end `ALL CHECKS PASSED`), refresh
  `docs/CODEMAP.md` with `tools/codemap.sh`.
- UI and art phases (PT2-PT5): render shots, judge them through the project's visual quality gate, report each score, and
  go on only at 7/10 or higher. Keep the before and after images in `docs/images/playtest/`.
- Note `SaveMidRun` fails on `main` at the time of writing (found 2026-11-16); fix or record it before PT1 lands so a real
  regression is not missed.

## As built

### PT1 (2026-11-16)

- **Town click (note 3).** `MapScreen.PickAt(Point, includePlayer)` picks a party, a town or a ruin on screen: a thing counts
  when the mouse is within its drawn size, at least 22-28 px, and the one the mouse is nearest to (as a fraction of its
  size) wins. Parties beat towns beat ruins, as before. One pick per frame (`hover`, set in `Update` while no panel is open)
  is read by the click (`ClickWorld(MapPick)`, which re-picks without your own party), the tooltip, the cursor and a gold
  hover ring round the town or ruin the click would go to (`DrawLines`). Footprints in world units: village 6, castle 8,
  town 10. `Campaign.SettlementAt` is unchanged (the campaign still uses world distance). Checked by hovering a village at
  its edge in a rendered campaign. No headless test: the pick needs the camera.
- **Re-forming on a charge (note 5).** `BattleTactics.Wits`: veterans and better no longer step back to re-form (`Plan.Reform`)
  on a charge you ordered; `Battle.ObeysPlayer(s)` is your own company, not an allied lord's (`Squad.AiControlled`) and not
  one the AI plays for you (`Options.AutoPlayer`). The AI's companies still re-form, so the grade differences of
  docs/archive/MELEE.md D5 hold. Ranks still open on a long run (`UpdateFormed`), so a company you send far arrives more
  ragged; the cost of a long charge is left, only the halt is gone. Left alone: `Plan.Regroup` after the foe breaks and
  no enemy is near (a stop after a win), and the veteran horse rally (`BattleWings.cs`), which is a charge-and-return cycle,
  not a re-form. Test `Reforms` now covers both: AI veterans fall back, yours do not.
- **Pause (note 9).** `MapScreen.Update` feeds the shader clock (`time`: wind, water, sky) and `UpdateLife` zero while
  `speed == 0`, and `UpdateLife` does no work at all while paused with the camera still (it still runs when the camera moves, so
  herds and people appear and go as you pan). Two frames of a paused map differ in 22 of 921,600 pixels (the post pass's own
  film clock, `Shading.PostToScreen`, still uses the wall clock); a running map differs in 42,000.


### PT2a (2026-11-16)

- **A panel has the screen to itself.** `MapScreen.DrawUi` draws the map's labels (`Flags`), the clock (`TopBar`), the feed,
  the minimap (`Overview`), the captain's card, the company cards and the contracts only while no panel is open.
- **Tips are a narrow card at the right edge** (`Tips.Draw`: 300 px wide, solid, text re-wrapped, the `\n` in tip texts
  ignored). On the map it sits bottom right; in battle above the order panel and its key hints (`Tips.Draw(tipUi, U(224))`).
  Over an open panel it is still centred at the top (`DrawAt`), 420 px wide.
- **The feed wraps** an item to an indented second line instead of cutting it with "..", six lines at most. The LOG button
  lost its long note, which was drawn over the button itself at the top left.
- **Not done, from the audit's C:** the button colours were checked and are fine (the TALK topics in the shot were really
  disabled: the lord hates you). The larger heading size moves to the panel passes.
- Judged 6/10, then 8/10 after the opaque tip and the feed wrap (judge: Haiku, the fallback: the T3 judge was not
  connected in this session). Shots: `docs/images/playtest/after/`.
- `SaveMidRun` still fails, as before this change (checked with the change stashed); PT2a only draws.

### PT2b (2026-11-16)

- **The captain's card** (`MapScreen.CaptainPanel`, bottom left) is three rows instead of seven: rank and `RENOWN n / next`
  with a bar that matches those numbers (renown over the next unlock); the purse with a coin and the week's balance (grey at
  zero); one row of icons with numbers: men against the cap, days of food, spare horses, prisoners (when you hold any).
  Each names itself on hover; whom you serve, the next unlock and the company count are on hover over the top row. A row for
  a wound or a siege appears only when there is one. (The P screen and the battle report still draw their renown pips from
  the last rung, `RenownStep`.)
- **The next step** (`Campaign.NextStep`) is one gold chip under the clock (`NextChip`), no longer two lines in the card.
- **The feed** shows the three newest items of the last three days, four lines at most, on one dark plate; older news is
  in the realm's chronicle (R).
- **Map labels stay out from under the HUD** (`HudZones`, read in `Flags`): a name under the feed, clock, chip, minimap and
  rail, card or company cards is not drawn; your own party and your goal always are.
- The menu rail stays as words: the audit's idea of icons only would make it harder to read, not easier.
- Judged 6/10, then 7/10 (Haiku fallback). Fixes from the first round: words-sized icons (14 px), the renown numbers and
  bar, grey zero balance, one plate for the feed, spacing.

### PT2c (2026-11-16)

- **Town tabs in groups** (`MapScreen.SettlementPanel`, `TownTabs`, `TabGroups`): a place with more than seven tabs (a town)
  shows group tabs across the full width (MEN, TRADE, TOWN, CAMP, and YOUR FIEF on your own), and under them the open group's
  own tabs as words (`Kit.WordTabs`: the open one gold and underlined). Seven or fewer (villages, castles) keep one full-width
  row. A tab name set from outside (`Stage`, `NextStep`'s `Where`) still opens its tab; one the place lacks falls back to its
  first tab.
- **Empty tabs hide:** MERCENARIES with no company for hire, CAPTIVES with no prisoners.
- **MARKET split:** MARKET is horses (and your kennel) on the left and selling goods on the right ("THIS TOWN PAYS MOST FOR
  ..."); ARMOURER is its own tab, its gear kinds as words (WEAPONS ... YOUR HORSE, so it is not taken for the horses your men
  buy in the MARKET). The armourer tip now shows on ARMOURER. A new shot `campaign-market.png`.
- Judged 6/10, then 7/10 (Haiku fallback). Left for the panel passes: the fief tab's building rows, the elders' reward
  numbers, empty space under short lists.

### PT2d (2026-11-16)

- **The company screen (P, `MapScreen.Muster.cs`):** the captain column no longer lists the ten skills (they are on K); its
  button turns gold and says how many perks wait. The prisoners block shows only when you hold some. The cards lost their
  four buttons: a click on a card picks it (`musterPick`) and one bar at the bottom left (`MusterActions`) moves, splits,
  merges or disbands it (DISBAND red, set apart). One open slot shows where the next company goes, not a row of them. The
  summary under the cards is the role bar and one row of totals (purse and week are on the map's card); cards got the room.
- **The captain sheet (K, `MapScreen.Captain.cs` `SkillTree`):** ten skill rings in a grid, click one (`skillPick`, it opens
  on a skill with a perk waiting); that skill's perks are rows with the name, tier, what it does, and CLICK TO TAKE (gold
  frame), TAKEN (click twice to forget, in a town) or what it needs. The old branch of 50 small nodes and its hyphen-splitting
  name helpers (`NameLines`, `Breaks`) are gone.
- Judged 6/10, then 7/10 (Haiku fallback). Left: the empty lower part of K's portrait column, one icon for every perk of a
  skill, perk wording.

### PT2e (2026-11-16)

- **Realms (R, `MapScreen.Realm.cs`):** each realm card keeps shield, name, stance, land, ruler and three troops; its style of
  war, lords/towns/castles and war weariness are a hover tooltip (`hoverRealm`, drawn after all cards). Every card keeps two
  lines for the name and two for the ruler, so the rows line up across cards. The 7x7 relations grid is now a list, WARS AND
  ALLIANCES (`Relations`): one line a war or alliance, yours first and lit, peace left out ("THE REST ARE AT PEACE.").
- **Career as king (`KingDiplomacy`):** the wall of 15 buttons is one row per realm (name, standing, one button: SEEK PEACE
  in a war, DEALINGS in peace) that opens that realm's choices inline (`diploOpen`, one at a time).
- **Lords (L, `MapScreen.Party.cs`):** a row is face, name, men and task, a one-word regard chip (`Regard`; none when
  indifferent) and the first trait; house, all traits, age and memories are on hover. Sort words FRIENDLIEST FIRST /
  STRONGEST FIRST (`lordSort`).
- Judged 6/10, then 7/10 (Haiku fallback). Left: dead space under a short wars list, realm names and the AT WAR / ALLIED words
  sharing colours (a WAR/ALLY chip would fix it), the household vassal row's older layout.

### PT2f (2026-11-16)

- **Divisions (V, `MapScreen.Party.cs` `DivisionsPanel`, `DivisionCard`, `NewDivisionCard`):** the text rows, the sentence
  of instructions and the right-hand COMPANIES list whose button cycled a company through the divisions are gone. Now four
  cards a row: your retinue (companies with no sergeant), one per division (name, temper chip with icon and its meaning on
  hover, command pips with n/6, companies as blue chips, STARTS: goal and DISBAND), and a last card to make a new one. Click
  a chip (`divPick`, gold), then a card (gold frame), to move the company (`AssignDivision`). The panel is sized to its rows;
  one hint line under the cards says what a click does.
- **Talk (`MapScreen.Talk.cs` `TalkPanel`):** the opinion is one chip (`Regard`, as on the lords list) with his memories,
  family and bonds on hover; the lord's words are marked by a gold bar (the font has no quote glyph, which drew as a blank);
  when every topic is barred, one gold line says why. The lines under the portrait line up with it.
- The font has no semicolon either: UI text uses a full stop instead.
- Judged: divisions 6, 6, then 7/10; talk 7/10 (Haiku fallback). Two judge claims were wrong (the sergeant names are one
  colour; the new-division text already says companions). Left: the retinue card's empty lower half, greyed topic buttons
  are faint (the shared disabled style), small 7 px body text.

### PT2g (2026-11-16)

- **Field view** (you fight): the company cards fold to a thin strip (`Hud.StripCard`: number, men standing, strength and
  morale bars, the frame coloured by state; `Layout` sets the height from `!Command && !Deploying`), and the sergeants'
  cards are not laid out (`LayoutDivisions` returns early) until the command view.
- **Command view:** a division's NAME: GOAL tag over the field shows only for the division you picked or point at
  (`DivisionMarkers`); its card says it for the rest.
- Judged 7/10 (Haiku fallback). Left for PT4: the strip's bars have no legend, the field flags' letters mean little alone.

### PT3 (2026-11-16)

- **The quests screen** (J, a QUESTS button on the right-hand rail; `MapScreen.Quests.cs`): tabs YOURS, ON OFFER (the boards
  you have opened: towns' stewards and villages' elders, nearest first, eight at most) and DONE AND FAILED. A row: the task,
  who asked (and AT a place when the task does not name it), reward and days left, SHOW ME (looks at the place and marks it)
  and PIN. The panel is as tall as the open tab.
- **One view of every source** (`Campaign/QuestLog.cs`): `QuestView` and `QuestList()` read `S.Contracts` (stewards,
  elders, lords' quests, orders' charges, chain steps, bounties) and the boards; `PlaceOf` finds the quarry, town or site;
  `RecordQuest` keeps the last 30 finished and failed (save `QuestHistory`). How quests work is unchanged.
- **The map's corner** shows one quest (`Contracts` in MapScreen.Settlement.cs): the pinned one (`PinnedQuest`, it follows
  a chain to its next step and comes off when done), else the one due soonest, with "+N MORE"; a click opens the screen.
  The pinned quest's place has a gold ring on the ground and a gold QUEST plate (`QuestMark`).
- Test `QuestsListed`. Shots `campaign-quests.png`, `campaign-quest-map.png`. Judged 6/10, then 7/10 (Haiku fallback).
  Left: small row text, the ring drawn as dots.

### PT4 (2026-11-16)

- **The order palette** (`Hud.Layout`, `PaletteButton`): six big picture buttons, CHARGE, ADVANCE, HOLD, FOLLOW ME,
  FALL BACK, FIRE (`MainOrders`), each with an icon, a name and its key in the corner. The rest (ON ME, SPACING, SHAPE,
  RELIEVE, SPLIT, FOLD IN, RULE: `MoreOrdersList`, short names in `ShortOrder`) sit behind MORE ORDERS (`Cmd.More`,
  `Hud.MoreOrders`). Each small button shows its key on hover.
- **Cards say the order as a picture** (`OrderMark`): a shield for hold, a sword for attack (red when it has a target),
  a flag for follow. A struck-through arrow means it holds its fire. A gold dot means orders are queued or a rule is set.
  Tier, drill grade, formation and the rule moved to the hover card.
- **Flags carry the same picture**. A chosen company's attack draws a dashed red line with a dark edge to its foe, with
  a sword at the end. When the foe is off-screen, the line stops at the screen's edge.
- **A new order flashes** the card and its flag gold for half a second (`OrderFlash`: order, target, formation, place).
- Shot `command-orders.png` (two companies attack, bows hold fire, MORE open). Judged 6/10, then 7/10 (Haiku fallback).
  Left: the FALL BACK icon reads as an anvil, and the attack line can end under the top bar.

### PT4 rework plan: RTS Command View & Mount & Blade Combat View

Goal: split the battle HUD and controls cleanly into two distinct experiences instead of blending both on screen:
- **Command View = cursor-first RTS:** almost all work done with the mouse, minimal HUD clutter.
- **Combat View = Mount & Blade:** uncluttered melee screen with directional reticle, compact vitals, and quick F-key / radial ordering.

#### Step 1: Middle-mouse radial wheel in RTS mode (done)
- Holding Middle Mouse on the field opens the radial order/goal wheel at the cursor and slows time (`WheelTimeScale = 0.08f`); releasing executes the sector and restores the original speed. `Alt + Middle-drag` orbits.

#### Step 2–5: Implemented & visually verified (2026-11-16, rated 9/10)
- Gate RTS panels (`MinimapPanel`, `Cards`, `DivisionCards`, `Tray`, `AlertStrip`, `CommandPanel`) strictly on `Command`; hide them in Combat View.
- Remove the text `Feed()` log from the battlefield HUD (keep alerts as clickable icons that pan to the spot).
- Add clickable top-bar controls in Command View for Pause / Speed (`||`, `0.5x`, `1x`, `2x`, `3x`) and `FIGHT` / `COMMAND` mode toggle so you never need the keyboard.

#### Step 3: Cursor-first RTS controls & field readability (`BattleScreen.Controls.cs`, `BattleScreen.Draw.cs`, `Hud.cs`)
- **Minimap right-click:** right-clicking the minimap issues a move/attack order to selected squads at that world point (left-click/drag continues to pan).
- **Double-click field selection:** double-clicking a friendly squad on the field selects all friendly squads of the same class/type.
- **Cursor Attack-Move mode:** clicking the Attack-Move icon (or `A`) enters a one-shot target cursor; left-clicking ground/minimap advances and engages enemies along the path.
- **Zoom-to-cursor & ground order lines:** scroll wheel zooms toward `mouseGround`; selected squads draw crisp ground lines to their `Anchor` (blue/gold) or `Target` (red), plus a clear `FALL BACK` icon and clipped attack lines that never run under the top bar.
- **Contextual icon grid:** bottom-center action grid shows only valid icon buttons for the current selection (formations, fire-at-will, spells, split/merge/relieve).

#### Step 4: Mount & Blade Combat View (`Hud.Melee.cs`, `BattleScreen.Controls.cs`, `BattleScreen.Orders.cs`)
- **Minimal M&B HUD:** center 4-way directional swing/block reticle (`Aim`) with parry/hit flash, compact bottom-corner vitals (player HP, horse HP, stamina/ammo/shield), and punchy in-world `OpponentMark` / `BlowNumbers`.
- **M&B F-key group ordering:** `1` Infantry, `2` Archers, `3` Cavalry, `0` All + `F1` Hold/Follow/Charge (or hold `F1` to project a ground banner where you look, release to send), `F2` Formation cycle, `F3` Fire at Will / Hold Fire, with a tiny transient top-left prompt that fades out. Keep the radial wheel as an alternative.

#### Step 5: Visual review & verification
- Run `tools/check.sh` (full build + `--selftest`).
- Capture `IRONFIELD_SHOTS` for both Command View and Combat View and launch a Hydrogen Medium subagent to inspect and rate the visuals.

### PT5 (2026-11-16)

- Swords, longswords, greatswords and scimitars have points, crossguards and pommels (painted in `SpriteSheet.cs`, baked).
  The falchion has a point, an edge that swells toward the tip and a brass guard.
- The fist is drawn again over the grip after the weapon (`Puppet`, the weapon draw), so the hand shows on the weapon
  instead of under it.
- Every blade's point tapers over four rows to a single pixel. The falchion's back curves in to its point.
- Judged 5, 6, 6, 6 out of 10 (Haiku fallback): the judge says the blades read as sharp and held, but not yet at a ship
  bar. Not passed. Left: foreshortened views for thrusts toward the camera, blades that show at battle distance (a tip
  glint), and the falchion's top, which the judge still calls blunt although the 8x render shows a one-pixel point.

### PT6a-c (2026-11-16)

| Trait | In battle (`Nature.Of`, BattleNature.cs) | Panel line |
|---|---|---|
| Reckless | the line closes from 8 m further, the reserve and horse go in at half the wait, he leads the charge 3 s after the line, half the patience on good ground, retreats only at 6% of your strength | RECKLESS: HE CHARGES AT ONCE AND LEADS IT HIMSELF. |
| Cautious | the line closes from 5 m nearer, his reserve waits 1.7 times as long, he joins only after 40 s, 1.8 times the patience, retreats at 40% | CAUTIOUS: HE WAITS ON HIS GROUND AND KEEPS A RESERVE. |
| Ambitious | sends for your head at 0.6 of the wait, and joins by 8 s | AMBITIOUS: HE WILL COME FOR YOUR HEAD. |
| Cruel | fights on until 12% | CRUEL: HE FIGHTS ON LONG AFTER IT IS LOST. |
| Honourable | his bows never mark you | HONOURABLE: HIS BOWS WILL NOT MARK YOU. |

- `BattleOptions.LeaderTraits` is set from the lord's `Character.Traits` in `PrepareBattle`. It is empty for brigands,
  monsters and quick battles, so they fight as before. Your side plays as the plain general.
- The encounter panel shows the line when no grudge line takes the spot (`MapScreen.Dialogs.cs`). It is in parchment, not the
  ground note's hazard orange. Shot `campaign-encounter-lord.png`, judged 6/10, then 7/10 (Haiku fallback).
- Test `LordsFightAsTheyAre`: with the same armies, a reckless lord leads at 35 s, a plain one at 45 s, and a cautious
  one does not lead at all.
- Not done: the after-action line ("his reckless charge broke his own line"), reasons on the lord screen, and your own
  sergeants reading the table. PT6d (CK systems) waits on the playtester, as planned.
