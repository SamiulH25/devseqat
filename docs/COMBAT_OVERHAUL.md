# Combat overhaul: plan

Goal: battles that feel massive. Two main features, each with the work that has to come with it:

1. **Sergeant divisions**: small armies inside your army, led by a companion, that pursue a *goal* rather than obey a
   command, and hear your orders only after a delay.
2. **Battle scale**: many times more men on the field, on bigger ground, with campaign armies to match.

Scale comes first in the build order: it is the riskiest (performance, determinism), and divisions are what make a
2,000-man army controllable.

## Where we start (as of branch point `1ac965a`)

| Thing | Now | Where |
|---|---|---|
| Men on the field at once | 400 total (`BattleOptions.Cap`); the rest wait as reserves | `BattleTypes.cs`, `Battle.Deploy` |
| Party size | `MenBase` 40 + renown + Leadership | `Data/campaign.json`, `Rules.MenLimit` |
| Company size | 10-24 places by role (`Rules.Cap`) | `Campaign/Rules.cs` |
| Field size | `MapRadius` 95 m | `Battle.cs` |
| Companions | up to 3 (`MaxCompanions`); each fights as a hero at the head of one company | `Characters.cs`, `Battle.AddSquad` |
| AI-led friendly companies | `Squad.AiControlled` (allied lords), led by `General(0)` | `Battle.cs`, `BattleAI.cs` |
| Orders | instant, to selected squads or all (`BattleScreen.Orders.cs`) | `Issue`, `Command` |
| Unit drawing | full puppet per unit, every unit, every frame | `BattleScreen.Draw.cs`, `Puppet.cs` |
| Per-tick whole-army scans | ~19 `Units.Count/Where/Any` in the sim | `Battle*.cs` |

---

## Feature 1: Sergeant divisions

### 1.1 What the player sees

- Before battle, in the party screen (P), you group companies into **divisions** and put a companion at the head of each.
  Companies not in a division are your **retinue**: you command them directly and instantly, as now.
- In battle you give a division a **goal**, not an order. The sergeant decides the formations, charges and timing.
- Your goals reach him after a **delay**: a horn call or a rider has to get there. A sergeant far away, or in the thick of
  it, hears late. The HUD shows the order on its way ("Riding to Sgt Aldric, 6 s").
- Each sergeant has a temperament and skill, so two divisions given the same goal fight it differently.

### 1.2 Goals

| Goal | What the sergeant does |
|---|---|
| Hold ground (point) | forms on the point, shield wall or square against horse, does not chase beyond a leash |
| Take ground (point / hill) | advances, clears the point, then holds it |
| Attack (enemy division/squad) | closes and engages that target; picks the formation and when to charge |
| Flank (left / right) | swings wide around the enemy line and hits its side or rear |
| Screen (friendly squad, e.g. archers) | stays between the enemy and that squad; engages only what comes close |
| Guard the captain | follows you and fights what threatens you |
| Reserve (commit when...) | waits behind the line; commits itself when a line company breaks, or when you send the word |
| Harass (horse / skirmishers) | shoot and withdraw, never stand in melee |
| Pursue | runs down routers, rallies when they are gone |
| Fall back (point) | withdraws in order, rearguard facing the enemy |

A goal is data: `Goal { Kind, Point?, TargetSquad?, Trigger? }`. The sergeant turns it into the existing squad orders
(`Order`, `Formation`, `Anchor`, `Target`). The unit AI (`Think`, `ThinkCav`) stays as it is.

### 1.3 The sergeant brain (headless sim)

- New file `BattleDivision.cs` (partial `Battle`) and a `Division` class: `Team`, `Leader` (the companion's hero unit),
  `Squads`, `Goal`, `PendingGoals` (a queue with arrival times), `Temperament`, `Skill`.
- `Divisions(team)` runs every 0.5 s next to `General`. It is a cut-down general working on the division's squads only,
  steered by the goal. Pull the reusable pieces out of `General` (line forming, flank swing, charge range, soft target)
  into helpers that take a list of squads, so both use them.
- **Temperament** (from the companion): *Bold* charges early and pursues far; *Steady* keeps formation and waits for
  contact; *Cautious* keeps a wider leash and falls back sooner. **Skill** (a new companion skill, "Command", trained by
  battles led) speeds up his own reactions and widens what he notices (threat radius, flank awareness).
- **Sergeant falls**: the division keeps its last goal but stops adapting, takes a morale shock, and its delay doubles
  until you name a new leader (the best officer in the division takes over after 10 s).

### 1.4 Order delay

- On issue: `arrive = now + Base + Distance(captain, sergeant) / CourierSpeed + Busy`, where `Busy` is added when the
  sergeant is in melee. Skill, a `Horns` perk and a banner-bearer shorten it.
- A newer goal sent before an older one arrives replaces it ("countermanded").
- **Messenger riders (option)**: the order is a real rider who rides to the sergeant and can be cut down. Good for feel,
  more risky to build; make it a second step after plain delays work.
- Determinism: the issue is recorded in the replay as an input; the arrival is computed by the sim, so replays stay exact.
  Add `SelfTest.ReplayExact` coverage for a battle with delayed goals.

### 1.5 Campaign side

- Save: `CompanyRecord.Division` (int, -1 = retinue) and `Character.Temperament`. Old saves load with everything in the
  retinue (no version bump).
- Divisions available = companions present (healthy) in the party. Raise `MaxCompanions` (3 -> 6) so a large army can
  field several divisions; promoted sergeants (`OfficerToCompanion`) are the main source.
- New companion skill "Command" (`Skills.cs`), gained by battles led as a sergeant.
- Party screen: a Divisions tab: drag companies under a companion, set a **starting goal** for deployment.
- `Rules.Army` passes the division per company through `Company` (new field) into the battle.
- Auto-resolve: a small bonus for divisions led by skilled sergeants, so the system is not only cosmetic off the field.

### 1.6 Enemy divisions

- Enemy lords split large armies into divisions too: the lord himself plus vassal/officer sergeants. This uses the same
  `Division` code, so the enemy line moves as several bodies with their own timing, not as one brain.

### 1.7 Battle UI

- Division cards in the HUD (grouped company cards with the sergeant's portrait, goal icon, delay countdown, strength).
- A goal wheel or radial on a key; click the field or an enemy to place the goal's point or target.
- Command overview (the existing top-down view) shows goal arrows and pending orders.
- Selecting a division's company and giving it a plain order is refused with a note ("Sgt Aldric leads them: give him a goal").
- New keys: `Act.GoalWheel`, `Act.NextDivision` in `Controls.cs` and the F1 help.

### 1.8 Tests

- A Hold goal keeps the division within its leash for 60 s against an enemy that retreats.
- A Flank goal reaches the enemy's side (hits arrive from `Flank`/`Rear`).
- Delay: the goal is not applied before its arrival time, and is applied after; a countermanded goal never applies.
- Sergeant killed: the division keeps fighting, the new leader is named, the delay goes back to normal.
- Replay exact with divisions and delayed orders.
- Save round-trip keeps division assignments; an old save loads with everything in the retinue.

---

## Feature 2: Massive battle scale

### 2.1 Targets (to confirm)

| | Now | Target |
|---|---|---|
| Men on the field | 400 | 1,500 default, up to 3,000 ("Battle size" setting) |
| Your party at the start / late game | 40 / ~150 | 120 / ~1,000 |
| Lord armies, hosts | (current) | scaled about x5 |
| Field | 95 m radius | scales with army size, up to ~300 m |
| Frame budget at 1,500 men | | sim <= 6 ms/tick, draw holds 60 fps on Medium |

### 2.2 Step 0: measure first

- `--bench N` CLI switch: headless N-vs-N battle, prints ms/tick (mean, p95) for each tick stage. Run it for 400, 1,500 and 3,000.
- `IRONFIELD_BENCH_DRAW=1` with xvfb: frame time with N men on screen.
- Every scale step below is checked against these numbers before and after.

### 2.3 Simulation at scale (headless)

- **Cached counts**: per-team / per-squad active, routing and alive counts kept up to date in `UpdateSquad`/`Kill`, instead
  of the ~19 whole-army LINQ scans (`Reinforce`, `CheckEnd`, morale, general).
- **Staggered thinking**: units out of contact (no enemy within ~20 m) think every 4th tick, split by unit index, so it is
  deterministic. Units in contact think every tick.
- **Squad-level target search**: a squad picks the enemy squads it faces; men search for the nearest enemy only among those,
  using the grid. This avoids each man searching the whole enemy army.
- **Grid**: check cell size and query radius at 3,000 men; make it a flat array reused every tick, with no reallocation.
- **Arrows**: a pooled array; volleys from one company fired as a batch.
- **Morale**: squad-level terms (strength, flank pressure, leader nearby) computed once per squad, not per man.
- **Allocation**: no LINQ or `new` in the per-unit hot path (the GC pauses show at this size).
- **Bigger field**: `MapRadius` from the armies' size; deployment spots spread with the width of the line; terrain
  resolution and foliage density checked on large maps (`Terrain.cs`).
- **Reinforcements at scale**: waves from the edge of the field by role, at rally points, not one company per second.
- **Determinism**: all of the above must leave `ReplayExact` passing.

### 2.4 Rendering at scale

- **LOD**:
  - near: full puppet, as now;
  - middle: fewer parts (no small kit, simpler poses);
  - far: a one-quad impostor, prebaked per troop look, facing and a few poses into the atlas.
- **Batching**: draw crowds as one big dynamic vertex buffer per LOD band, not part by part; frustum and distance culling.
- **Shadows**: blob shadows batched in one draw; real shadows only for the near band.
- **Bodies**: past a limit, the oldest bodies turn into ground decals.
- **Readability**: a floating banner per company and per division over the crowd, so you can read the battle from afar.
- **Camera**: higher pull-out on the command view; fog and haze tuned so distant masses read as an army.
- Tie density to `Quality` (how many units get full puppets), never to how many men are simulated.

### 2.5 Sound at scale

- Crowd ambience from counts in contact (a clash layer, a march layer, distant shouting) rather than one cue per blow.
- Limit concurrent one-shot cues, nearest first.

### 2.6 Campaign scale and balance

- Raise `MenBase`, `Rules.Cap` per role (larger companies) and company slots in `Data/campaign.json` / `Rules.cs`.
- Scale lord parties, garrisons, hosts and bandit bands in the same proportion (`CampaignAI.cs`, `CampaignSiege.cs`, `Diplomacy.cs`).
- Economy: wages, food and upkeep per man go down so a 1,000-man army is affordable late, not early.
- Auto-resolve (`Rules.AutoResolve`): check that results still match fought battles at the new sizes (`--balance`).
- Re-run `--pacing` and adjust career stage targets.
- Add the new "Battle size" setting (`Settings.cs`) into `BattleOptions.Cap`; it is stored in the replay.

### 2.7 Tests

- Bench guard in the self-test: a 1,500-man battle finishes and stays under a generous time limit (catches 10x regressions).
- Cached counts equal the LINQ counts every tick in a debug self-test.
- Staggered thinking: a 400-man battle ends with the same winner and similar duration as before.
- `ReplayExact` on a 1,500-man battle.
- Campaign: `BalanceMatchesData` with the new values; a scaled lord army auto-resolves without errors.

---

## Build order

| Phase | Work | Done when |
|---|---|---|
| 0 | `--bench`, baseline numbers | numbers written in this file |
| 1 | Sim at scale (2.3) | 1,500 men <= 6 ms/tick, selftest + replay pass |
| 2 | Render at scale (2.4) + sound (2.5) | 1,500 men at 60 fps on Medium under xvfb, screenshots readable |
| 3 | Divisions sim + delay (1.2-1.4), tests (1.8) | goals and delays tested headless |
| 4 | Divisions UI + campaign (1.5, 1.7) | playable from the party screen into battle |
| 5 | Enemy divisions (1.6) | enemy lines move as several bodies |
| 6 | Campaign scale + balance (2.6) | `--balance` and `--pacing` within targets |
| 7 | Messenger riders, polish, docs (README rules, DEVELOPING, CLAUDE.md) | |

## Open questions (answered)

1. Battle size: 1,500 by default, 3,000 at most (also 400 and 800). **Yes.**
2. You can still order a division's companies directly; the order rides out with the same delay. **Yes.**
3. Plain delays first, real riders after. **Done in that order.**
4. Promoted sergeants can lead divisions as well as companions. **Yes.**

## As built

All phases 0-7 are built on branch `combat-overhaul`. What differs from the plan, and the numbers:

**Scale (phases 0-2).**
- `--bench N`: 3,000 men went from 20.5 ms a tick to about 3.6 ms (1,500 men about 1.9 ms), under the 6 ms budget.
  The wins: staggered thinking (`ThinkNow`), morale every 2nd tick, squad bounding circles for `GapTo` and the far nearest-enemy
  search (`NearestFar`), and a 128x128 grid (4 m cells, +-256 m). No cached alive counts were needed.
- Field radius `95 * sqrt(men / 450)`, 95-230 m (not 300 m: 230 m already holds 3,000 men with room to manoeuvre).
  Deployment lays companies out in rows that wrap past 110 m, with 2-6 ranks by size; flanks sit outside the widest line.
- Reinforcements come in from the rear line in columns; no rally points or waves.
- Rendering: frustum culling (also for the sun pass) and three puppet detail levels by distance (fewer parts far away).
  Software GL (llvmpipe) holds about 32 fps with 3,000 men on Medium, against 40 with 70 men; real GPUs are far above that.
  No impostor sprites or instancing were needed.
- Sound: a synthesised crowd roar rises with the men in contact (`sqrt(men / 600)`); one-shot cues are capped at 64 a tick.

**Divisions (phases 3-5).**
- Goals as planned plus Take (hold a chosen point) and Pursue; Flank keeps an axis fixed when the goal starts.
- Delay `1.5 s + distance / 14 m/s`, +2 s if the sergeant is fighting, x2 with no sergeant, -5% per skill point, x0.7 with
  the Bannerman perk ("Horns"). A fallen sergeant costs his men 8 morale; after 10 s the best man left takes charge.
- Couriers (phase 7) are not units: a rider follows the line from the general so he arrives on time, and an enemy within
  2.5 m cuts him down. Each replacement swings 30 m further round. This keeps replays exact and costs nothing at scale;
  the rider is drawn as a dot in command view, not as a horseman.
- Campaign: the V panel (also DIVISIONS on the party screen) makes up to 6 divisions under companions or sergeants, assigns
  companies and sets a starting goal. Companions now have a Command skill (2-10, +1 every 2 battles led) and a temper;
  sergeants' command comes from their battles (at most 6). `MaxCompanions` went from 3 to 6.
- `Armies.Divide` splits armies of 240+ men and 6+ companies into a horse wing and two foot battles; enemy lords and quick
  battles use it. The AI general sends divisions in when the lines close, the fight starts, or after 15/30/45 s by temper.
- Auto-resolve: each division leader past command 3 adds 1%, at most 8%.

**Campaign scale (phase 6).**
- One knob, `Balance.ArmyScale` = 3, instead of separate raises: company places, the men limit, bands, garrisons, recruits and
  horses for sale are x3 (`Scaled`), and everything paid per man or derived from strength is /3 (`Rules.PerMan`): wages, levies,
  loot, ransom, promotion, food, bounties, parley prices, contract rewards, lord battle gold, war score, skill gains.
  You start with about 120 men (target 120) and late games reach several hundred (target ~1,000 is not reached by default:
  10 companies of 72 is the ceiling; raise `ArmyScale` for more).
- Check: with `ArmyScale` 1 the campaign self-test prints exactly the old numbers. At 3: AI battles 1,918 (1,880 before), lords
  average 73 men (21 x 3 = 63 before), war chests healthy.
- `--pacing 6`: 5 of 6 stand-in careers reach Marshal (2 of 6 before), 6 stages off target (9 before). Vassal day 31-56, Lord
  day 127-163, Marshal day 218-274.
- The economy per man is unchanged rather than cheaper: a big army is affordable late because income from fiefs grows, as before.
