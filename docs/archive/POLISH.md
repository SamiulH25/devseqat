# Polish pass: plan

Status: **planned**. Each phase Q1-Q7 can ship on its own, and `tools/check.sh` must pass after each one.
When a phase ships, add its "As built" note at the end.

## 1. Goal

No new systems. Make what is already built **work together, read cleanly and fit on screen**, so a new player can find
every system and a long campaign never shows a broken seam. The test for each item: *does a player see it, or would a
long campaign hit it?* If neither, it waits.

## 2. How this plan was made (2026-11-08)

- Read the hand-offs: `BattleOptions` fields set by the campaign vs read by the battle, `BattleResult`/`CompanyResult`
  vs `Campaign.Apply`, `Balance` fields vs their readers, `Data/events.json` triggers and verbs vs `Campaign/Events.cs`,
  `Act` keys vs `Controls.Defaults` and the F1 help, `Cue` sounds vs their emitters.
- Scripted scan for public fields nothing reads (only `Balance.CompanySlotRenown`, `Balance.PeaceScore`,
  `Style.Grid`, `Style.PanelLight` and `World.FoliageTotal` are never read; all `BattleOptions` connect both ways).
- Took every staged screenshot at 1280x720 (`IRONFIELD_CAMPAIGN_SHOTS`, `IRONFIELD_SHOTS`) and looked at each.

**What is sound:** the campaign-to-battle bridge connects in both directions. Every event trigger has an event. Save,
load and save again gives the same file. Every key action has a binding. Most of what is wrong is on the surface: text
that does not fit, tips on top of buttons, and keys that are hard-coded or not shown.

## 3. Out of scope

The "Not built" lists in the other plans (Column formation, pavises, a captain who can die, creatures at sites, AI
alliances...) are features, not polish. They stay where they are.

## 4. Phases

### Q1: Bugs and broken seams (small, fix first)

1. **Panel keys fire while you type a name.** `MapScreen.Update` (MapScreen.cs ~165-174) checks L/K/R/V/M even when
   `renaming != null`. From the party screen, typing a company name with a K or V in it opens Skills or Divisions in the
   middle of the word. Skip the panel keys while renaming.
2. **"BACK (V)" does not go back.** On the divisions panel the button says `BACK (V)` and goes to the party screen
   (MapScreen.Party.cs:216), but pressing V closes everything (MapScreen.cs:172). Make V do what the label says.
3. **Screenshot folders must exist.** `ScreenHost.Shot` (ScreenHost.cs:218) and the battle shot script crash with
   `DirectoryNotFoundException` if the folder is missing. Call `Directory.CreateDirectory` first.
4. **Tips that lie.** The "slots" tip says "the maximum of 10 companies", but `Rules.CompanySlots` grows (11 as king).
   Use the real number. Tips and the map hint line spell keys out ("P opens your company", "K", "Tab"); write them with
   `ctl.Name(Act.X)` so rebinding does not leave them wrong.
5. **Fields nothing reads:** `Balance.CompanySlotRenown`, `Balance.PeaceScore` (also in `Data/campaign.json`),
   `Style.Grid`, `Style.PanelLight`, `World.FoliageTotal`. Wire them up if they were meant to do something, else remove
   them (and drop the two Balance values from the JSON, so `BalanceMatchesData` still passes).
6. **Event verbs with no event:** `gamble` and `revolt` are coded in `ApplyEffect` but no event uses them. Give each one
   an event (a dice game in the tavern, `in_town`; a bad answer to the `fief_unrest` petition that ends in revolt).
   Then every verb is in use, and `EventsAndQuests` can check that it stays that way.

Test: extend `EventsAndQuests` so every verb in `Verbs` is used by at least one event. No test is needed for the shot
folder.

### Q2: Tips that help and never block

1. **Tips draw on top of modals.** At 1280x720 the tip box covers the battle's VICTORY buttons (NEW BATTLE / QUIT TO
   TITLE), runs over the F1 controls overlay, and sits behind the campaign encounter panel. Hold the tip queue while a modal or
   overlay is open (`Hud.ShowResult`, `MenuOpen`, `HelpOpen`; any `MapScreen` panel other than `None`). Then draw it
   last, so it is always on top when it does show.
2. **The newer systems have no tips.** There are 15 tips, and all of them cover the early game. Add one-time tips the first
   time each of these comes up: a man gets a name (the roll, P), the first fallen named man, the divisions panel (V), the
   first site, a duel offer, the armourer (MARKET), your first fief (FIEF tab), fame drawing recruits, a downed named man
   in battle (rescue), and retiring the captain.
3. Keep each tip to three short lines, as the style guide asks (docs/DESIGN.md "Voice").

### Q3: Text that fits at 1280x720

From the shots (all at 1280x720, the smallest size we support):

| Screen | Problem | Where |
|---|---|---|
| Map | The two key-hint lines overlap the captain card. The card title "CAPTAIN  FREE COMPANY" runs into "RENOWN 50". | `MapScreen.Hud.cs` `Hints` (~305), captain card (~227) |
| Map | The message log has no backing, so map labels show through it, and lines are cut with "..". | `MapScreen.Hud.cs` log |
| Map, close zoom | Trees right by the camera fill the screen and hide the town. | `MapScreen` draw: fade or skip foliage near the eye |
| Realms (R) | Culture lines, ruler names and long realm names ("ORDER OF THE ASHEN SUN") overflow the cards. Relation labels are cut ("NORTHMAR"). | `MapScreen.Realm.cs` |
| Lords (L) | The realm tab strip, with 7+ realms and the household, runs off both sides of the panel. | `MapScreen.Party.cs` `CharactersPanel` (~27) |
| Skills (K) | Perk names are cut to 8 letters ("BANNERMA", "QUARTERM"). | `MapScreen.Captain.cs:125` |
| Fief tab | Every building you cannot start says "BUILDING WALLS". The tax help text runs past the panel edge. | `MapScreen.Settlement.cs` ~418-431 |
| Report | A long fallen line is cut mid-word. | `MapScreen.Report.cs` roll lines |
| Battle F1 | The controls list runs off the bottom of the overlay. | `Hud.Overlays.cs` `Help` |
| Battle command view | Company card names are cut ("MEN-AT-ARM", "YEOMAN ARC"). The hint line and the division labels sit under the cards. | `Hud.cs` |

Approach:
1. Add one helper to the kit, `Kit.Fit(text, width)`. It steps down to the small font, then cuts at a word with "..",
   and the hover tooltip shows the full text. Use it everywhere a name or line goes into a fixed box, instead of fixed
   character counts.
2. **A guard so it stays fixed:** in shots mode (`IRONFIELD_*_SHOTS`), `Ui` records any text that runs past the panel
   it is drawn in and writes the list to `overflow.txt` next to the PNGs. The aim is an empty file at 1280x720.
3. Fix the "BUILDING WALLS" case by saying "BUSY: WALLS, 12 DAYS" once above the list. Each button then shows its
   cost, or its own reason.

### Q4: Keys and help on the map

1. The map's panel keys (L, K, R, V, M, B) are hard-coded (`Pressed(Keys.L)`...), unlike everything else. Make them
   `Act` entries (`Lords`, `Skills`, `Realm`, `Divisions`, `Atlas`, `Siege`) with defaults in `Controls.Defaults`. That way they can be
   rebound and they go into `controls.json`.
2. Make the toggles consistent. Each key opens its panel from the map *or from any other panel* and closes it again. Today
   K and V work from the party screen, but L, R and M do not.
3. Add an F1 help overlay on the map, listing the map keys in the same layout as the battle one. Replace the two hint
   lines with a single "F1 CONTROLS" line, which also clears the overlap from Q3.
4. Add small clickable buttons for these panels (a row under the top bar or by the captain card). That way a mouse-only
   player can find the realm, lords, atlas and divisions screens.

### Q5: Telling the story the systems already have

1. **Name the battle on the report.** `FinishBattle` already works out `BattleName` (CampaignBattle.cs:26) for honours
   and the chronicle, but the report says only "AGAINST EMBER CREW". Put the name on `BattleReport` and show
   "VICTORY AT ASHBY FORD". Show the same name in the battle's opening message line.
2. **Show honours in the report.** A battle honour won, lost or won back goes to `rollNews` and the log, but the report
   shows it only as a roll line. Give it its own row with the banner icon.
3. **Fill the empty space in panels.** The results, encounter, divisions, lords and town tabs have large blank areas at
   720p (the encounter panel has one row of troops in a 300 px well). Use it for what is already known: on the encounter panel, the field and who
   chose it (`GroundNote`) and the odds. On a short town tab, the next step (`C.NextStep`).
4. **Few events for fiefs.** `fief_poor`, `fief_rich`, `fief_unrest`, `veterans` and `unpaid` have one event each, so the
   same one keeps repeating. Add a second event for each, using existing verbs only.

### Q6: Continuity checks (tests)

1. **Save in the middle of a run.** Today's test (`CampaignTests`, ~line 43) runs 60 days and checks that save, load and
   save give the same file. Add a test that runs the pilot for ~200 days, saves, loads and goes on for 60 more days. It
   must end in the same state as a run that never saved. This catches state kept only in memory, such as caches, RNG streams
   or indexes that are not rebuilt. By 200 days the roll, the fallen, honours, fiefs, sites and the undead are all in play.
2. **The report matches the record.** After a fought battle and after an auto-resolve, check that the report's
   fell/hurt/lost per company equal the change in `CompanyRecord` men. Also check that every named man in `rep.Roll`
   is on the roll or in `Fallen`.
3. **A long pilot run without errors.** A `--pacing`-style run of 365 days must end with no exceptions, no empty
   company left on the roster, no party with negative men, and no realm with no land that still has lords.

### Q7: Code tidy (only where a phase already touches the code)

CLAUDE.md asks for methods under ~80 lines and files under ~600. Split these when a phase is already editing them, not
as separate work: `Campaign.Apply` (191 lines), `MapScreen.Update` (87, and Q4 changes it), `MapScreen.MusterCard` (91),
`MapScreen.Hud.Flags` (81). `ScreenHost.CampaignShots` (281) can become a table of named shots. Files over 600 lines:
`CampaignState.cs`, `Hud.cs`, `Career.cs` (plus `World.cs` and `SpriteSheet.cs`, which are drawing code and fine as they are).

## 5. Build order and why

Q1 first: small and safe, and it clears the bugs. Q2 next, because the tips problem hides buttons on every new player's
first battle. Q3 and Q4 together: the hint-line overlap is fixed by Q4's F1 overlay, and both touch `MapScreen.Hud.cs`.
Then Q5, which builds on the layout helper from Q3. Q6 can run alongside any phase and should land before more content is
added. Q7 rides along with the others.

Each phase: retake the campaign and battle shots at 1280x720 and look at every changed screen. From Q3 on, also check
that `overflow.txt` is empty.

## As built

- **Q1.** The map screens have their own key actions (`Act.LordsScreen`, `SkillsScreen`, `RealmScreen`, `DivisionsScreen`,
  `AtlasScreen`, `SiegeScreen`; table `MapScreen.Screens`), so they can be rebound and the tips name the bound key
  (`ctl.First`). A screen opened from the party screen closes back to it (`OpenScreen`/`CameFrom`). The shots runs make
  their folders. Unused fields and styles were removed (`CompanySlotRenown`, `PeaceScore`, `Style.PanelLight`/`Grid`,
  `World.FoliageTotal`), and `ValidateEvents` reports effect verbs that no event uses.
- **Q2.** In battle, a tip is not drawn and cannot be dismissed while a menu or overlay is open (`BattleScreen.TipsHeld`).
  On the map a tip sits at the top while a panel is open (`Tips.DrawAt`), so it never covers a panel's buttons. New tips
  in `MapScreen.LaterTips` cover the roll, the fallen, divisions, sites, duels, the market, fiefs, fame and the legend.
- **Q3.** `Kit.Fit`/`FitScale`/`TextFit`/`TextFitC` (UiKit.cs) cut text at a word and end it with "..". Battle cards,
  toasts, lords tabs, realm cards, perk labels, the captain card, the event feed and the fief tab now fit their boxes.
  `Overflow.cs` writes text that leaves its panel to `overflow.txt` in shots runs. What is left there is map labels near
  the screen's right edge.
- **Q4.** F1 on the map opens a help panel (`MapScreen.HelpPanel`). The old hint line became a row of buttons with their
  keys (`Hints`). The battle F1 overlay fits at 720 px high.
- **Q5.** A battle has a name (`BattleSetup.Field`, `BattleReport.Field`). It shows in the battle log, the encounter panel
  ("THE FIELD") and the report heading. Honours won and lost have their own section in the report (`BattleReport.Honours`).
  The encounter panel shows your own companies and the odds in words. There are six new events (rabble, dice,
  fief_sickness, guild_offer, veteran_banner, unpaid_books), and `gamble` is now a dice roll weighted by Charm.
- **Q6.** `SelfTest.SaveMidRun`: the pilot plays 200 days, the game is saved and loaded, and both copies play 60 more days
  under the same driver. They must give the same save, and neither may leave empty companies, negative men or lords of a
  realm with no land (`CheckSound`). It runs 260 days rather than 365 to keep the suite short. `SelfTest.ReportMatchesRecord`:
  in auto-resolved and fought battles, each company's losses add up and match its record, and every named man the report
  names is on a roll or among the fallen. The new events shifted a test's dice and showed a bug: a named man who fell and
  lived could come back fit because his stack's rounding counted him killed. He is now always counted among the wounded.
- **Q7.** The per-stack part of `Campaign.Apply` is now `StackLosses`. The other splits were left, since no phase changed
  those methods much.
