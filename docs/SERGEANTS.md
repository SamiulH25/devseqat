# Sergeants that matter (SG0-SG7)

The player's gripe: sergeants and their divisions felt loosely defined and hard to use, especially in a quick battle.
This plan makes the sergeant the place you plan a battle, not a way to lose control of companies.

Status: SG0-SG7 built (2026-11-12). Code: `BattleCommand.cs` (headless), `Hud.Command.cs` (drawing), hooks in
`BattleDivision.cs`, `BattleTactics.cs`, `Battle.cs`, `Replay.cs`, `BattleScreen.Orders.cs`, `BattleScreen.Controls.cs`.
Tests: `Tests/SergeantTests.cs`.

## SG0 The command cap and the longer approach (built first)
- Armies stand about 130 m apart (`Battle.Stand` 35).
- The captain leads at most `Armies.CommandCap` (10) companies himself. `Armies.Divide` fills sergeants' divisions
  (at most `Armies.DivisionCap`, 8, each) and gives the bows a sergeant when needed; anything still over the cap is held
  off the field (`reserves`, `Held`) and marches on when one of his companies is spent.

## SG1 The command tray: who leads whom, and the reserve
- In deployment you move companies between yourself and your sergeants (`Battle.Assign`, `CannotAssign`): select
  companies, then click ADD on a sergeant's card, or TO ME on the tray. Only before the battle starts.
- The tray under the company cards: `COMMAND n/10` and a chip for every company waiting off the field. Click a chip to
  call it up (`Battle.CallUp`, `CannotCallUp`): to you if you have a free place, or to the sergeant you have picked
  (Y or his card). Before the battle it is on the field at once; during it a rider goes back for it (`CallUpDelay`, 8 s).
- Replays: assignments and call-ups before the start go in `Replay.Setup` (applied before the start positions); later
  call-ups in `Frame.CallUps`.

## SG2 Plans and horns
- Each division can hold one horn plan (`Division.Horn`): a goal kept back until you sound the horn.
- Setting it: with a sergeant picked, toggle ON THE HORN in his goal panel, then pick a goal as usual. Before the battle
  it is set at once; during it, the plan rides out like any order (`Division.PendingHorn`, `HornAt`).
- Sounding it (Enter, `Act.Horn`, `Battle.SoundHorn`): every division with a plan takes it within half a second, no rider,
  at any distance. The plan is spent. Recorded as `Frame.Horn`; plans set in battle are goal orders with `Plan` set.

## SG3 Sergeants report and ask
- Each card shows what the division is doing (`Battle.Report`): engaged, under arrows, on the move, awaits the horn,
  asks for orders, sergeant down.
- At moments that matter a sergeant asks (`Division.Query`): their horse on our flank (stand fast / fall back), we are
  taking arrows (go at them / pull back), they break (run them down / re-form). Answer with F6 / F7 or click; the answer
  rides out as a goal. Unanswered after 12 s he decides himself by temper and traits. A sergeant asks at most every 25 s.
  Yours only, and not when the AI plays you.

## SG4 A sergeant lifts his companies' wits
- A company under a living sergeant thinks (wheel, wrap, re-form, rotate, pincer: `BattleTactics.Wits`) at the better of
  its own grade and his (`Battle.SergeantGrade`: skill 3+ Trained, 5+ Veteran, 8+ Elite; a Veteran sergeant +1 skill).
  Its fighting drill (ranks, guard) stays its own. `Battle.WitsGrade`.

## SG5 Command by being near
- Within `VoiceReach` (25 m) of a sergeant your goal is shouted: 0.6 s, no rider (`Battle.InVoice`). While you are in
  melee yourself, riders leave 3 s later. The card shows IN EARSHOT.

## SG6 Command view clarity
- Each of your divisions has its own colour (`Hud.DivisionColour`): its card stripe, a ring round each of its companies
  and its goal line. Riders' countdowns stay on the cards.

## SG7 Sergeants with a name and a nature
- A division takes the officer traits of its sergeant's company (`Company.Traits`), or the spec's (`DivisionSpec.Traits`).
  Hothead: charges from further and answers boldly. Stubborn: never falls back of his own accord. Drillmaster: drills his
  men twice as much. Veteran: one more skill for his grade. Quick-battle divisions get a nature each.
- Cards show the sergeant's name (a named man's, or a companion's), skill as pips and his nature.

## Deviations and gotchas
- Assigning is deployment only; mid-battle you still order a division's companies directly (they then ignore him).
- Empty divisions (all companies moved away) stay as cards so you can fill them again; they do not think.
