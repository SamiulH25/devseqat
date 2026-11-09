# Melee and company tactics: plan

Two problems to fix, one feature to add:

1. **Companies do not fight as companies.** On a charge every man runs at the enemy nearest him, so a clash turns into
   one blob against another.
2. **They all go in at once.** The AI general charges every foot company when the first one gets within 16 m.
3. **New: companies think, and how well depends on their training.** Levies are a crowd that barely holds its shape.
   Veterans keep their ranks, face a threat to the flank, and look for a way round the enemy. Elites always work to
   surround the company they fight, and they win.

The blob is not banned. It becomes what happens when a company **loses its order** (levies quickly, elites rarely),
not what every company does the moment it charges.

---

## Where we start (as of `cbe0a5c`)

| What | Now | Where |
|---|---|---|
| A charging man | ignores his slot and heads for the nearest enemy (his target's nearest man while >3 m away, then anyone's) | `BattleAI.Think` L55-88 |
| A holding man | leaves his slot for any enemy within 6 m, comes back only past 14 m | `BattleAI.Think` L81 |
| Slots | renumbered every tick from the live men, so one death shuffles the whole company | `Battle.UpdateSquad` |
| Rear ranks | nothing stops them: they walk round the front rank's ends to find a foe | `BattleAI.Think` |
| Enemy bodies | only the soft push in `Separate` keeps them apart, so the lines soak into each other | `BattleGrid.Separate` |
| When the line charges | all foot together once any company is within 16 m (spears 10 m) | `BattleAI.General` L267 |
| Training | troop `Tier` 1-7 changes stats only; the campaign's company `Cohesion` (0-30) changes starting morale only | `Units.cs`, `Rules.cs` L279 |
| Flanks | flank hits do x1.2, rear x1.5, add `Squad.Pressure` (morale) | `BattleCombat.Hit` |

Divisions (`BattleDivision.cs`) and the general already decide *what* a company should do (hold here, attack that one).
This plan adds what is missing under them: *how* a company does it, and how well.

---

## The three layers

```
Commander          you / AI General / a division's Brain   ->  Squad.Order, Target, Anchor, Front   ("what")
Company wits (NEW) Battle.Tactics(squad), every 0.5-2 s     ->  Squad.Plan, Engage anchor, Bend      ("how")
The men            Think(u): fight from the ranks           ->  u.Wish, swings                       ("do")
```

- The commander's order is never overruled, except that rabble can **break ranks** (D4), the price of cheap troops.
- The company's wits decide the approach, the frontage and the reaction to what it meets.
- The men hold their place in the ranks as well as their **discipline** allows (Drill x Formed, below).

---

## D1. Drill: what training a company has (fixed for the battle)

`Squad.Drill`, 0-1, set once in `AddSquad`.

**From troop tier** (man-weighted average across a mixed company's stacks):

| Tier | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Drill | 0.10 | 0.22 | 0.38 | 0.50 | 0.64 | 0.78 | 0.92 |

**Modifiers** (then clamped 0-1):

| Source | Effect | Why |
|---|---|---|
| campaign `Cohesion` 0-30 | +0 to +0.10 | a company that has won together fights together; neglect costs it |
| officer trait `Veteran` / `Hothead` | +0.05 / -0.05 (and Hothead charges early) | traits already exist on `Company` |
| a companion Hero leads it | +0.05 | |
| in a division | + sergeant `Skill` x 0.01 | |
| troop data `drill` (optional new field) | replaces the tier value | creatures and specials (below) |

The campaign passes it in through a new `Company.Drill` (default -1 = work it out from tier). `Rules.Army` fills it,
so **no save change**.

**Grade**, the name the player sees. It sets which wits the company has (D5):

| Grade | Drill | Typically |
|---|---|---|
| Rabble | < 0.30 | tier 1-2 |
| Trained | 0.30-0.56 | tier 3-4 |
| Veteran | 0.56-0.85 | tier 5-6 |
| Elite | >= 0.85 | tier 7, or a well-kept tier 6 with a Veteran officer |

So a tier 6 company you look after can fight as Elite, and a neglected tier 7 one slips to Veteran. That ties
the battle to the campaign's legacy theme (docs/LEGACY.md).

**Creatures** keep their own behaviour (docs/CREATURES.md): `Swarm` companies (goblins, packs, trolls) get `drill` 0
and fight as a mob as they do now, and the pack's circling stays. Raised undead get 0.3 and never lose order
(they feel nothing). Skeleton warriors, if wanted, could take a data `drill` of 0.6: the dead in good order.

## D2. Formed: how well it holds its order right now (changes in battle)

`Squad.Formed`, 0-1, starts at 1. **Discipline = Drill x Formed** is the number the men read.

| Loses order | Amount (tune) |
|---|---|
| a man of the company falls | 0.6 / StartCount x (1.2 - Drill) |
| a flank or rear hit (alongside `Pressure`) | 0.01 / 0.02 x (1.2 - Drill) |
| running a charge over 20 m | 0.15 x (1 - Drill) |
| bad ground (`Terrain` order < 0.6: woods, water) | drifts down toward the ground's order value |
| its own men routing near by | 0.02 each, per second |
| rabble breaking ranks (D4) | set to 0.3 |

| Regains order | Rate |
|---|---|
| not in contact | Drill x 0.25 per second |
| in contact (Veteran and Elite only) | Drill x 0.05 per second |
| its sergeant or your banner within 10 m | x1.5 |

Below **Formed 0.4** the company's **ranks are broken**: its men fight as they all do today (anyone, anywhere), and
the HUD says so. A Veteran or Elite company that is broken and not pinned pulls back 6-10 m to re-form (D5).

## D3. Fighting from the ranks (the men; this fixes the blob)

All in `Think` and `UpdateSquad`. The blob behaviour stays as the fallback for broken ranks.

1. **Stable slots.** Each man keeps his `File` and `Rank` (new `Unit` fields) for the battle. When a man falls, the
   man behind him in the same file steps up; only the last rank is re-spread. No more whole-company shuffle.
2. **The engage anchor.** A charging company with a target moves its `Anchor` to the target's face: the nearest point
   of the target's bounding circle, less reach, with `Front` turned toward it. The men march in their slots right up
   to the enemy. The ranks come with them, instead of each man running.
3. **The front rank fights.** Men in the front rank (and the second rank with spears or pikes, `Reach > 2.2`) may step
   off their slot by up to **lunge** to strike: `lunge = 1 + 3 x (1 - discipline)` metres (an elite 1.3 m, a levy 3.7 m).
   They strike the nearest enemy within lunge + reach, **of the target company** unless someone of another company is
   hitting them (then they turn on him).
4. **The rear ranks wait.** Ranks behind the fighting ones hold their slot, face front, brace if they can, and step up
   when the man ahead falls. With low discipline some press forward anyway: a man leaves his slot when
   `noise(u) > discipline` (a fixed per-man draw from the battle seed). So levy rear ranks spill round the ends, which
   is the old blob, and elite rear ranks never do.
5. **A holding line stays a line.** On Hold the 6 m pull and 14 m leash become `lunge` and `lunge + 4`.
   An attacked holding company fights from its slots.
6. **A front where lines meet.** Two men of opposing sides facing each other within reach stop walking into each other:
   `Think` clamps a fighting man's forward wish to zero once he is at reach, and `Separate` pushes enemies apart twice
   as hard as friends. A **press** follows from that: where one line's men have more friends behind them (rank depth),
   the push between them drifts the front line their way, slowly. That gives a deep column a reason to exist and a thin
   line a reason to fear one.
7. **Strength in numbers, locally** (the reward for surrounding, in `Hit`):
   - a man struck while **two or more enemies** are within 2.5 m of him blocks less: x(1 - 0.15 per extra foe, at least 0.4);
   - a man **in his rank**, with a friend at each shoulder and his company's Formed above 0.6, blocks x(1 + 0.15 x Drill);
   - a man of a company whose ranks are broken blocks x0.85.

   With the flank and rear multipliers that `Hit` already has, a company that gets round its foe now wins clearly,
   and a company that keeps its ranks takes a frontal fight well.

## D4. When companies go in (the AI general; and rabble that won't wait)

- **Company by company.** `General` pairs each foot company with the enemy company opposite it (by lateral place
  along the front) and charges it when *its own* gap is under 16 m, not when the first company's is.
  The `charged[team]` flag stays for the horse, the reserve and the captain, which already key off it.
- **The wing goes for an end.** The outermost company already swings wide. Its target becomes the end company of the
  enemy line, with an approach from the side (D5 Veteran "hit a pinned enemy in the flank").
- **Rabble breaks ranks.** A Rabble company holding with an enemy company within 12 m, and no sergeant or banner within
  10 m of it, rolls `rng` each second against `(0.3 - Drill) x 2`. On a hit it charges on its own: Formed drops to 0.3,
  the feed says "The levies break ranks and charge!". This happens to **your** companies too. That's the cost of
  fielding levies, and the reason to stand your banner near them (D7).
- **Hothead** officers charge from 22 m, whatever their grade.

## D5. Company wits by grade (the thinking system)

New file `BattleTactics.cs`: `Tactics(Squad s)` runs per company on a staggered timer, keyed on the tick counter
(replays stay exact), and sets `Squad.Plan`:

`Plan { Advance, Pin, Wrap, Refuse, Reform, Rotate, Pursue, Hold }`

How often it looks and how far it sees depend on grade:

| Grade | Thinks every | Sees companies within | What it does (each grade does all the grades above it too) |
|---|---|---|---|
| **Rabble** | 2.0 s | 15 m | Goes straight at its target (or the nearest company). Never answers a flank threat. May break ranks (D4). Chases routers as far as they run and is hard to call back. |
| **Trained** | 1.0 s | 25 m | Keeps its ranks in contact (D3) and fights its target. **Refuses a flank**: an enemy company closing on its side while its front is free makes it wheel to face (`Refuse`). When its target breaks, it chases 15 m and then **re-forms**, instead of running off. |
| **Veteran** | 0.5 s | 40 m | **Hits a pinned enemy in the flank**: if its target is already fighting a friendly company front to front, it takes an engage anchor on the target's side, not its face. **Wraps an overlap**: if it is wider than its target, it bends its outer files round the target's end (`Squad.Bend`, below). **Re-forms**: broken and not pinned, it steps back 6-10 m, re-forms and goes in again. **Rotates**: a front-rank man whose stamina is under 30 swaps with the fresh man behind him. |
| **Elite** | 0.5 s | 60 m | **Always looks to surround.** With a friendly company near by it plans a pincer: whichever of the two is in contact first (or the bigger) `Pin`s, and the other takes the `Wrap` side, or the rear when it can get there. Alone against a narrower foe it bends both ends (a double envelopment). **Picks the decisive target** when not told one: a wavering company (low morale), one already pinned, a sergeant's or the captain's company, ahead of the nearest. **Rolls up the line**: when its target breaks it does not chase. It turns on the flank of the next enemy company over. **Does not throw itself away**: badly outnumbered and alone, it holds on good ground (`HoldsGround`) and waits for support. |

**Bend** (`Squad.Bend`, -1..1 per end): `Slot` curves the outer files forward round an arc, so the line wraps the end
of a narrower enemy. This one change gives the horns-of-the-bull look and puts flank hits where they belong.
`Squad.Slot` gets the bend; square and wedge ignore it.

**The ordered target is kept.** Wits change *how* the company reaches it (angle, bend, pin or wrap), not *what*.
A Veteran's or Elite's choice of the next target applies only once the ordered one has broken, or when it was told
"Charge" with no target (null = its pick).

**Your companies' wits work the same way.** A Charge order to your Elite company gets it wrapping. A Hold order keeps it
where you put it (it only wheels to face a flank threat). Your Rabble may break ranks.

## D6. Horse and bows by grade

**Horse** (`ThinkCav` already has Charge / Withdraw / Melee):

- Rabble: charges straight in and bogs down (Melee lasts longer), withdraws man by man.
- Trained: rides through and withdraws (as now).
- Veteran: **regroups as a body**. It withdraws to one rally point behind its own side and re-forms (Formed back
  over 0.7) before it charges again, so the second charge lands as a charge and not a trickle. It picks the flank or rear
  of a foot company that is already fighting.
- Elite: also picks a charge lane square to the target's open side. It never charges braced spears from the front,
  and it goes after archers and the enemy captain first when they are open.

**Bows:**

- Rabble: shoot the nearest company. When foot come within 7 m they draw blades where they stand.
- Trained: shoot their target (as now).
- Veteran: **fall back** behind their own foot when an enemy company comes within 20 m, rather than meet it with knives.
  Hold fire into a melee their own men are in.
- Elite: also focus on wavering companies and the enemy's archers. When there is room, they move a little to shoot
  along the enemy line (into the flank), not straight at its shields.

## D7. What the player sees

- **Company card and tooltip** (`Hud.Cards`, `SquadTooltip`): the grade (Rabble / Trained / Veteran / Elite) as a
  small mark, and the state in one word: *Formed*, *Wavering ranks*, *Ranks broken*, *Re-forming*.
- **Feed lines**, so the thinking shows: "The Iron Thanes wheel onto their flank!", "Your levies break ranks!",
  "The enemy pikes re-form.", "Huscarls roll up the line."
- **Order arrow** (`BattleScreen.Draw` order arrows): for a Veteran or Elite plan, a dashed hook shows the wrap.
- **Your banner** steadies Rabble (no breaking ranks within 10 m) and quickens re-forming (x1.5). That gives the banner
  and the last stand (`BattleBanner.cs`) one more job.
- **Muster roll** (`MapScreen.Muster.cs`): each company's grade beside its Cohesion bar, so the player sees that
  looking after cohesion makes a better battle company.
- README: one short section on drill, order and grades for players. F1 help: no new keys.

## D8. Balance, scale, determinism

- **Auto-resolve** (`Rules.AutoResolve`) has to keep up. Fought battles will now favour high tiers more than before.
  Measure with `--balance 8` (fought against auto-resolved outcome by tier gap), then raise the tier weight in
  `Rules.Strength` until the two agree. This is a `Balance` value in `Data/campaign.json`.
- **"Idle player loses in ~30 s"** (selftest gotcha) must still hold at every level.
- **Scale:** company wits are per squad on a 0.5-2 s timer, so they cost almost nothing. The per-man work (lunge
  target, rank checks) reuses `Near`/`Nearest`. Budget: `--bench 1500` no more than 10 % slower than now (~3.6 ms).
- **Determinism:** the break-ranks roll uses the battle `rng`, the rear-rank "noise" is a hash of the battle seed and
  `u.Index`, and every timer runs off `tick`. `ReplayExact` must pass after each step.
- Tune numbers are first guesses, marked *(tune)* in code comments, and checked with the tests below.

## D9. Tests (`Tests/BattleTests.cs`, plus helpers in `SelfTest.cs`)

Two measures, written once as helpers and printed by the tests:

- `RankError(squad)`: mean distance of its men from their slots.
- `Intermix(a, b)`: the share of a's men that have an enemy behind them (relative to a's front). That number is the blob.

| Test | Checks |
|---|---|
| `HoldsRanks` | two tier-4 companies clash for 20 s: rank error stays under 2 m and intermix under 15 %; with tier-1 levies both are clearly higher |
| `RearRanksWait` | in an elite company's first 5 s of contact, no rear-rank man has struck a blow |
| `StepsUp` | killing a front-rank man: the man behind him in his file takes his slot and nobody else moves more than 0.5 m |
| `FightsItsTarget` | two companies side by side charge two targets: over 90 % of blows land on their own targets |
| `PartsByCompany` | a 3-company line against a 3-company line: the companies charge at different times when their gaps differ |
| `LeviesBreakRanks` | a Rabble company holding 10 m from the enemy charges on its own within 15 s; with a sergeant beside it, it does not |
| `RefusesFlank` | a Trained company holding, enemy coming at its side: it turns to face before contact; Rabble does not |
| `VeteransWrap` | two Veteran companies against one engaged enemy: at least a third of their hits come from the flank or rear; two Rabble companies: far fewer |
| `ElitePincer` | two Elite companies against one: one pins, one wraps (Plan values), and they win faster than the same men as Trained |
| `Reforms` | a Veteran company broken (Formed 0.3), not pinned: it backs off and is over 0.7 within 10 s |
| `TierWins` | the same men, half Elite and half Rabble drill: Elite wins at even numbers and still wins at 1 : 1.3 |
| `ReplayExact` (existing) | still exact |

Plus visual checks: two new staged shots in `BattleScreen.Shots.cs`, `clash-levies.png` and `clash-elites.png`
(the same field 15 s into contact), so the difference can be seen in one look.

---

## Build order

Each step ends with `tools/check.sh` green, a commit, and before/after shots.

| Step | What | Main files | Done when |
|---|---|---|---|
| **M1** | Fighting from the ranks with one fixed discipline (0.6) for everyone: stable slots, engage anchor, front rank fights, rear ranks wait and step up, Hold leash, a front where lines meet. **Fixes the blob.** | `BattleAI.Think`, `Battle.UpdateSquad`, `Squad.Slot`, `BattleGrid.Separate`, `Units.cs` | `HoldsRanks` (tier 4 part), `RearRanksWait`, `StepsUp`, `FightsItsTarget`; shots look like two lines |
| **M2** | Drill and Formed: the tier table, modifiers, `Company.Drill`, gains and losses, broken ranks as the fallback, local numbers and in-rank support in `Hit` | `Squad.cs`, `Battle.AddSquad`, `BattleCombat.Hit`, `BattleMorale.cs`, `Troops.cs`, `Rules.Army` | `HoldsRanks` (levy part), `TierWins` |
| **M3** | Company by company: the general pairs companies and charges each on its own gap; the wing goes for an end; Rabble breaks ranks; Hothead | `BattleAI.General` | `PartsByCompany`, `LeviesBreakRanks` |
| **M4** | Company wits: `BattleTactics.cs`, `Plan`, cadence and sight by grade; Trained refuse and re-form; Veteran flank, bend, re-form, rotate; Elite pincer, target choice, roll-up, wait for support | new `BattleTactics.cs`, `Squad.Bend`/`Slot`, `BattleDivision.Engage` | `RefusesFlank`, `VeteransWrap`, `ElitePincer`, `Reforms` |
| **M5** | Horse and bows by grade | `BattleAI.ThinkCav`, `General` archers | horse regroup test, bows fall back test |
| **M6** | Player view: grade and state on cards and tooltips, feed lines, wrap arrow, banner effects, muster grade, README | `Hud.cs`, `BattleScreen.Draw.cs`, `BattleBanner.cs`, `MapScreen.Muster.cs` | shots, `overflow.txt` empty |
| **M7** | Balance: `--balance`, auto-resolve tier weight, bench, docs | `Rules.cs`, `Data/campaign.json`, `docs/DEVELOPING.md` 4.4/4.5, `CLAUDE.md` | fought and auto-resolved agree; bench within 10 % |

M1 alone fixes the look you raised. M2-M4 add the tiered thinking.

## Open questions

1. **Should your own levies break ranks without your order?** Proposed: yes (D4), with your banner or a sergeant near
   them stopping it. The alternative is enemy-only.
2. **Should your elite companies show initiative under a plain Hold order?** Proposed: no. Hold means hold, and they
   only wheel to face a flank threat. Wrapping happens on Charge (or in a division whose goal is to attack).
3. **Stamina rotation (Veteran+)**: worth having? It shows well, but it is the most fiddly piece. Proposed: last in M4,
   dropped if it costs too much.
