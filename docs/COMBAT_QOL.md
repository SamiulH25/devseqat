# Combat quality of life (Q1-Q6)

Sprint plan. Goal: fewer fiddly clicks in a battle, more of the player's attention on the decisions that matter.
Each phase stands alone and ships on its own. Q1 is built; Q2-Q6 are planned below in the order I would build them.

Rules for every phase:
- The sim stays headless and deterministic. Anything the player does that changes the sim must reach the replay
  (`Replay.Frame`), as stakes and folds do.
- A company in a division gets the order through `Send` (the rider's delay), like every other order.
- Orders that can fail answer with a reason (`CannotX` returning `string?`), shown in the battle log.
- Each phase adds a self-test and its key to the F1 help.

---

## Q1. Fold companies together (built)

**Player:** select two or more companies and press **,** (COMMA) or the FOLD IN button. The fullest company (or the one a
companion leads) takes in the others: their standing men walk over and fill its ranks from behind. A worn company of 12
folds into a nearly full one of 90 and you have one company of 102 instead of a fragile one that will rout.

**Rules:**
- Same command only: both your retinue, or both in the same division. Allies' companies keep their colours.
- Same kind only: foot with foot, horse with horse, archers with archers. Mage circles and creatures never fold.
- A company a companion leads can take men in but never folds into another.
- The company folding in must have lost men (an unhurt company has no gaps to bring), must not be at blows, and must be
  within 120 m. The company taking them in may be fighting: fresh men walking into a melee is the point.
- The joined company's drill and order are the weighted mix of the two, shaken a little by the newcomers.
- The fallen and the fled stay with their own company, so after the battle every man's fate (and every named man's deeds)
  goes back to the company he started in. The campaign needs no change.

**Code:** `BattleMerge.cs` (`CannotMerge`, `Merge`, `Fold`, `ReplayMerge`); `Squad.FoldedInto`; `Unit.Home` (the company a man
began in; `Battle.Result` counts men by it); `Battle.Commanded` leaves folded companies out; `Replay.Frame.Merges`;
UI `BattleScreen.MergeSelected`, `Cmd.Merge`, `Act.Merge`. Test `SelfTest.FoldCompanies`.

**Later, if wanted:** the AI general folds its own worn companies (`General`, once a company is under 30% and a like
company stands near); a sergeant does the same for his division (`Brain`).

---

## Q2. Relieve the line, and split a company

The other half of Q1. Folding fixes a worn company; relieving and splitting fix a tired or badly placed one.

**Player:**
- **Relieve (R in command view, or button):** select a company fighting in the front and a fresh one behind it. The fresh
  company passes through and takes the front company's exact line (anchor, front, width); the tired one falls back
  through it to where the fresh one stood. Men of the tired company stop fighting as the fresh ranks reach them, so the
  line is never empty. This is what veteran companies already do inside themselves (`BattleTactics.Rotate`), lifted to
  whole companies and put in the player's hands.
- **Split (Shift+,):** one selected company becomes two of half the men each, side by side on the old line. Lets you
  send a detachment to guard a flank or a gap without taking a whole company off the line.

**How:**
- Relieve: a short-lived pair state (`Squad.Relieving`, `Squad.RelievedBy`) in a new `BattleRelief.cs`. The fresh company
  is given the tired one's `Anchor/Front/Columns`; while the two overlap, the tired company's men switch to
  "pass back" (they ignore separation against the fresh company, as men already do inside their own ranks in
  `BattleRanks.CloseFiles`) and leave contact one by one as a fresh man takes their place. When the fresh company's front
  rank is formed (`Formed > 0.7`), the tired company gets the fresh one's old place as a hold order.
- Split: a new `Squad` added at runtime with the same `Company` (so `Result` keeps counting both halves under one
  company: `Unit.Home` from Q1 already makes this work if the new squad's men keep their `Home`). The new squad gets its
  own card. Rejoining is just Q1.
- `CannotRelieve`: same command, same kind, the fresh company within 60 m and not in contact itself, and it must not be
  smaller than half the tired one (a handful cannot hold a whole line).
- Replay: `Frame.Reliefs`, `Frame.Splits` (indices), as with folds.

**Test:** a tired company in contact is relieved: the front holds (no enemy man reaches the second line during the swap),
the tired company ends behind, its stamina recovers; a split company's two halves both report under one `CompanyResult`.

**Risk:** squads added mid-battle shift `Squads` indices; reinforcements already do this, so replays and `Squad.Id` cope,
but check every `Squads.IndexOf` user.

---

## Q3. Queued and standing orders

**Player:**
- **Queue (Shift + right-click):** each Shift order is added after the last one instead of replacing it: walk to the ford,
  then form a line here, then charge. The dotted route shows every leg (`Route`, the order arrows).
- **Standing orders on a company (order wheel, a second ring):** one rule each company keeps until you change it:
  - *Charge when they close*: hold, but charge any enemy that comes within 25 m (spears waiting for horse, a hidden reserve).
  - *Fall back when worn*: under 50% men (or wavering morale), fall back 30 m behind your nearest company and re-form.
    With Q1 this can offer "and fold in" as a checkbox.
  - *Skirmish* (archers and horse archers): keep 40 m from the nearest enemy, falling back as he comes on, firing as they go.
  - *Don't chase*: never follow routers past the line (horse that wins a fight and then rides off the field).
  The card shows the rule as a small tag (like the HOLD tag for arrows today).

**How:**
- `Squad.Queue` (a short list of order steps: move to a line, attack a company, hold, charge); `UpdateSquad` pops the next
  step when the current one is done (anchor reached and formed, target broken). Reuses the order lambdas in
  `BattleScreen.Orders.cs` by turning them into plain step records the sim understands, so they replay.
- `Squad.Standing` (an enum) read in `Think`/`ThinkCav` and `BattleWings` (skirmish distance), and in `UpdateSquad` (the
  fall-back trigger). Sergeants' companies keep their goal: standing orders only apply to companies you command directly
  or with `Override` set.
- Replay: the queue and standing rule go into `SquadOrder` (the order signature already diffs every order field).

**Test:** a queued march-then-hold arrives at the second point; a "charge when they close" company stays put until the
enemy is at 25 m and then charges; skirmishing archers lose fewer men to charging foot than holding archers.

---

## Q4. Battle speed and a planning pause

**Player:**
- **Slow (`[`) and fast (`]`):** 0.5x, 1x, 2x, 3x. Fast forward through the long approach; slow down for the clash.
  Shown on the HUD next to the clock. Fast is off while you fight in person (field view) unless you choose it.
- **Plan while paused:** today pause stops the battle; Q4 lets you give every order while paused (most already work) and
  shows all pending orders as ghosts (the preview slots `PreviewSlots` already draws while dragging), riders' delays as a
  number on each. Space resumes and they all go out at once.
- **Pause on alerts (setting):** pause automatically when a Q5 alert of your choice fires (a company breaking, the enemy
  reserve committed).

**How:** the sim already runs at a fixed 1/60 s step; speed is how many `Update(1/60f)` calls a frame makes
(`BattleScreen.Update`), so replays and the sim are untouched. Slow motion runs one step every other frame (with
interpolation in the puppet draw so it stays smooth: `Unit.ViewFacing` already eases; positions need the same).
Cap fast at whatever keeps the frame under 16 ms on the big battles (`--bench`: 3000 men is about 3.6 ms a tick, so 3x is
safe at 1500 a side). Settings: `Settings.BattleSpeedKeys`, `Settings.PauseOn`.

**Test:** a battle run at 3 steps a frame ends as the same battle at 1 (it is the same sim; a cheap guard against someone
later scaling `dt` instead).

---

## Q5. Alerts and a glass on the enemy

**Player:**
- **Alerts strip** above the cards: short lines for what needs you, each with a click to jump the camera there and select
  the company: *Spearmen wavering (flank)*, *Archers out of arrows*, *Horse rallying, ready to charge*, *Division
  leaderless*, *Enemy reserve committed*, *Enemy horse on your left*. Each one fires once until the state clears; old
  ones fade after 10 s. They replace digging through the log.
- **Enemy glass:** hover or click an enemy company you can see (`Battle.Visible`) for a card like your own: troop, tier,
  roughly how many ("about 60"), morale as a word, formation, its attack style and traits. What you have not seen stays
  "?", so sight (`BattleSight.cs`) matters more.
- **Company card hints:** the card already shows state; add arrows left (ammo), stamina, and a small mark when the company
  is being hit in the flank.

**How:** a headless `BattleAlerts.cs` builds `Battle.Alerts` (kind, squad, place, time) twice a second from state the sim
already keeps (`Pressure`, `Ammo`, `Rallying`, `Division.Leader`, `Squad.SeenAt`); drawing in a new `Hud.Alerts.cs`. Jump
reuses the double-click focus on a card. The enemy card reuses the `Hud` card code with a "seen" filter.

**Test:** an archer company that shoots its last arrow raises exactly one *out of arrows* alert; a company hit in the flank
raises *wavering (flank)*; an enemy company never seen shows "?" for its count.

---

## Q6. Control groups and remembered deployments

**Player:**
- **Control groups:** Ctrl+1..9 saves the selected companies as a group; 1..9 then selects the group (tap twice to jump the
  camera). Today 1..0 select single companies by card order, which breaks as soon as you have more than ten; groups keep
  1..0 for single cards when no group is set, so nothing changes for players who never use them.
- **Remembered deployment:** each campaign company remembers where it stood and in what formation relative to the army's
  centre in your last battle. Next deployment starts from that layout (mirrored to the ground's facing). A DEPLOY AS LAST
  TIME / DEFAULT toggle on the deployment banner. Groups are remembered too.
- **Named layouts (later):** save two or three layouts ("Shield wall centre", "Horse on the wings") and pick one when
  deploying.

**How:** groups live on the screen (`BattleScreen.groups`, by `Company.Id` so they survive between battles in the
campaign: save field `CampaignState.ControlGroups`). Deployment: after `Deploy`, `BattleScreen.BeginCampaign` applies
`CampaignState.LastDeploy` (company id -> offset, front, columns, formation), clamped to your third of the field; written
back in `FinishCampaignBattle` from the state at the moment deployment ended. Companies that are new or changed role fall
back to the default slot. Both save fields have defaults, so old saves load.

**Test:** a deployment saved and applied to a second battle puts each company within a pace of its saved offset; a
company missing from the save gets its default slot; a group saved by company id selects the same companies next battle.

---

## Order and size

| Phase | Size | Why this order |
|---|---|---|
| Q1 fold | small, built | the ask |
| Q2 relieve, split | medium | finishes the "manage worn companies" story Q1 starts |
| Q3 queued and standing orders | large | the biggest cut in clicks; fall-back-when-worn uses Q1 |
| Q4 speed and planning pause | small | screen-only, cheap, big feel |
| Q5 alerts and enemy glass | medium | tells you when Q1-Q3 are needed |
| Q6 groups and remembered deployment | medium | campaign save fields; best once the others settle |

Move this file to `docs/archive/` when Q6 ships.
