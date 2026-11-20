# Issues that need fixing

Archived 2026-11-19: the items still open (5, 6, 7 trace helpers, 8, 11, 13, 14) are carried in docs/BACKLOG.md; 9 is
COMPANY.md C5; 10 was fixed.

Audit of HEAD `c0e186c` (2026-11-17). Every line reference was checked against that tree; every claim was reproduced
by running the thing, not read off a doc. Ordered by what costs the most if left alone.

Fix them roughly in this order: 1 and 2 first (they gate everything else being trustworthy), then 3, then 4–6.
7–12 are cheap and can go in any time. 13–14 are polish.

## Progress (2026-11-17, second session)

- **Done:** 1 (seven weapon names in `StyleFor`/`PassivesFor`; a two-handed morningstar crushes), 2 (`RunNames`: every test
  runs, failures print as `FAILED name: why`, the summary names them), 3 (see the correction under 3), the two OF5
  regressions in 7 (see the note under 7), `UpgradeTreeVisibility` registered, `Tests/Dbg.cs` deleted, 10, 12.
- **Decided:** 4 is option (c), by the owner: the troops stay and come in with the world book's map. The 17 cultures carry
  `"waiting": true` in `Data/factions.json`; `Cultures` skips them, `Factions.Validate` refuses a waiting culture a realm
  holds, and `docs/TROOPS.md` lists them.
- **Still red, needs a call:** `PassivesOnlyAdd` (see the note under 7).
- **Open:** 5, 6, 8, 9, 11, 13, 14; the trace helpers in 7.

| # | Issue | Severity | Size |
|---|---|---|---|
| 1 | 32 elite troops lost their attack style, and get the wrong passives | **P1** | small |
| 2 | A failing test kills its shard; a full run executes 46% of the suite | **P1** | small |
| 3 | A distant war starves an army parked in a town | **P1** | small |
| 4 | 80 troops in 17 cultures that no faction, castle or band ever fields | **P1** | design call |
| 5 | Couriers are built, switched off, and still drawn | **P1** | small |
| 6 | `Harass` and `Screen` cannot be chosen by anyone | **P1** | small |
| 7 | 8 tests red, 10 written but never registered | P2 | small |
| 8 | Ten public methods with no callers; three are missing features | P2 | small |
| 9 | C5 (the camp) has no screen | P2 | large |
| 10 | `docs/COMPANY.md` status line is false | P2 | trivial |
| 11 | Two landing pages have diverged | P2 | trivial |
| 12 | Junk in the repo root | P2 | trivial |
| 13 | 63 keys, no way to learn them in the game | P3 | small |
| 14 | `Hud.cs` still carries the pre-PT4 hooks | P3 | small |

---

## 1. 32 elite troops lost their attack style, and get the wrong passives

**P1 — silently changes every elite fight**

`Data/troops.json` gained seven weapon names in commit `902dce7`:

`flamberge` (4 troops), `longsword_gilded` (6), `pennonlance` (7), `arbalest` (8), `bardiche` (5), `recurve` (6),
`morningstar` (5) — 41 men in all.

Neither switch that reads weapon names knows them:

- `BattleStyles.cs:13-29` `StyleFor` — no case, so the troop falls to `AttackStyle.None`.
- `BattleEffects.cs:13-33` `PassivesFor` — falls to the catch-all arm, so the troop gets *wrong* passives, not none.

What breaks, per archetype:

| Weapon | Troops at T5+ | Should be | Actually |
|---|---|---|---|
| flamberge | 3: blademasters, grand_blademasters, sword_saints | Sweep + fearsome/tireless | no style, wrong passives |
| longsword_gilded | 6: royal_crusaders, immortals, sovereign_champions, ashen_saints, lion_guard, sun_paladins | Riposte + fearsome/tireless | no style, wrong passives |
| pennonlance | 7: banner_knights, paladin_knights, grail_knights, grand_masters, doge_lances, lion_knights, storm_lancers | RideDown + heavy_charge/iron_skin | no style, `swift`/`fearsome` |
| arbalest | 6: heavy_arbalests, fortress_bolters, grand_arbalests, iron_citadel_arbalests, doge_arbalesters, sun_arbalests | Punch + steady_aim | no style, wrong passives |
| bardiche | 5: iron_thanes, noyan_guard, khagans_axes, huscarl_thanes, summit_wardens | Crush | no style, wrong passives |
| recurve | 4: kheshig, sky_wolf_archers, eternal_sky_archers, sultans_bows | Plunge/QuickLoose + steady_aim | no style, wrong passives |
| morningstar | 1: iron_hammers | Bash | no style, wrong passives |

32 of the 178 tier V–VII troops are affected: **18% of the elite roster.**

**Fix:** seven extra arms in `StyleFor`, and the same seven in `PassivesFor` routing to the family's existing
entry (flamberge → greatsword, longsword_gilded → longsword, pennonlance → lance, arbalest → crossbow,
bardiche → daneaxe, recurve → bow, morningstar → mace). One line each.

**Then:** the three red style tests should go green, and `PassivesOnlyAdd` should too.

**Risk if skipped:** every elite-vs-elite balance number quoted since 2026-11-16 is wrong for those 32 troops, and
`--troop-balance` output is misleading.

---

## 2. A failing test kills its shard; a full run executes 46% of the suite

**P1 — makes the whole test suite untrustworthy**

`SelfTestShards.cs:55-63` `RunShard` has no try/catch:

```csharp
foreach (var name in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
{
    var clock = Stopwatch.StartNew();
    byName[name]();          // throws out of the loop
    Console.WriteLine(TimeMark + name + " " + ...);
}
```

The first failure throws out of `RunShard`, the shard process dies, and every test after it in that shard is never
run and never reported.

**Measured, not inferred** — two independent 8-shard runs, identical both times:

```
shard 0/35 ran   a sweep carries into the man beside
shard 35/35 ran  OK
shard 28/35 ran  and trade better (2.5 kills a loss against 2.7)
shard 36/36 ran  OK
shard 4/35 ran   every tier V-VII troop has an attack style
shard 1/38 ran   a lance rides a man down
shard 25/36 ran  azure_heart men can climb from levies to tier VII
shard 3/36 ran   passives do not make Grail Knights fight worse
TOTAL 132 of 286 executed; 154 never run; 571 s
```

Minimal repro:

```bash
dotnet run -c Release --no-build -- --selftest-shard Slots,Charge,SweepHitsBeside,Parry,Javelins
# runs 3 of 5; says nothing about Parry or Javelins
```

Running each test in its own process is the only way to see them all — that takes 26 minutes instead of 160 seconds.

**Fix:** try/catch inside the loop. Record the failure, print it as `<name>: <message>`, carry on with the rest of the
shard, exit non-zero at the end. About ten lines. `check.sh` should then report how many ran and how many failed
rather than a bare "selftest failed".

**Risk if skipped:** a regression landing in the hidden 54% ships with a green-looking run.

---

## 3. A distant war starves an army parked in a town

**P1 — real player-facing loss**

`Tests/CampaignTests.cs:121-130` `Economy` fails: after eight weeks parked in a town with a contracted level-2 army,
52 men remain where the test wants 60.

Cause, traced by instrumenting the run:

```
week 0..6:  food 10→6→5,  men 60,  at Nogent, faction 0
week 7:     food 5.0,     men 60,  at Nogent, faction 5   <- the town changed hands
            h1319: men 60 -> 56   "The assault on Ebbmouth is thrown back."
            h1343: men 56 -> 52   "Your men are starving."
```

`Campaign/CampaignWar.cs:184-207` `EatDaily` only resupplies for free when the town belongs to your contract's realm:

```csharp
if (here != null && Captain.Contract >= 0 && here.Faction == Captain.Contract && here.Kind != SettlementKind.Village)
    Player.Food = MathF.Max(Player.Food, 7);
else if (Player.Food < 4 && here != null && !Refuses(here) && S.Gold >= FoodPrice(7))
    BuyFood(7);   // this branch never fired
```

The purchase branch did not fire because `FoodPrice(7)` exceeded the player's gold at that moment. So the player was
standing in a town, not at war with it, with an army in the field — and starved.

**Correction (2026-11-17):** the gold gate was not the cause (314 crowns, a week costs 189). Nogent was taken by realm 5,
at war with your liege, while the army stood in it; the test clears every encounter, so the army never answered the
assault, and an enemy town will not sell food (`Refuses`). In play you get the encounter and can march away. The test now
keeps the liege at peace; `EatDaily` is unchanged. Open question: should a town taken round you throw you out?

**Fix, two parts:**

- `EatDaily`: a town or castle you are not at war with should let the quartermaster buy food. The condition is already
  there; the failing part is the gold gate combined with a free top-up that a *different realm's* town does not give.
  Either let a non-hostile town top you up to a couple of days for free, or drop the price gate to "buy what you can
  afford" instead of all-or-nothing at seven days.
- The test: it is asserting on a simulation whose town ownership is decided by a war elsewhere on the map. Pin the
  town's owner in the setup, or run the eight weeks somewhere the war cannot reach, so the test measures economy and
  not the AI's war.

**Risk if skipped:** a player can lose a company to a political event they never saw, while standing still in a town.

---

## 4. 80 troops in 17 cultures that no faction, castle or band ever fields

**P1 — design decision needed**

Commit `902dce7` added 17 cultures (`azure_heart`, `azure_march`, `azure_coast`, `crimson_patrician`,
`crimson_alpine`, `northern_fjord`, `northern_taiga`, `southern_oasis`, `southern_dune`, `southern_rift`,
`steppe_horde`, `steppe_salt`, `league_lagoon`, `league_fen`, `order_chapter`, `order_warden`, `bandit`) holding
80 troops: 63 in the 16 realm subcultures, 17 in `bandit`.

What the data says:

- **0** of those troops appear in any faction's `lines` in `Data/factions.json`. AI armies, mercenary offers and lord
  retinues all build from `lines` (`Factions.Line`, `Armies.LineTroop`), so no AI will ever field one.
- **Every subculture chain roots at tier 2 and stops at tier 5** — `crown_yeomen`→…→`vale_wardens`, and the same shape
  fifteen times. None reaches tier VI or VII, and all 15 chain-ends are dead ends.
- **The tier V troops are unreachable, permanently.** `Companies.cs:152` needs a barracks of the troop's culture for
  any promotion to tier 5+, and `WorldMap.cs:148,156` sets every settlement's culture to
  `Factions.CultureOf(faction)` — one of the seven realm cultures. No settlement can ever have culture `azure_heart`,
  so no `azure_heart` barracks can exist, so `vale_wardens` can never be trained.
- The tier 2–4 rungs are only reachable by a company whose own `Culture` is `common` or empty, because
  `CanEnterLine` (`Companies.cs:149-150`) lets a common-culture man train into anything. In practice that means a
  company you never gave a home.
- The 17 bandit-culture troops are a different story: `SpawnBandits` (`CampaignAI.cs:48-75`) builds bands from
  `Armies.LineTroop(role, tier, rng.Next(3))` — the three original realms — so bandit battles never field them. They are
  not wholly unreachable, though: a captured prisoner of that troop keeps its id, so `CampaignWar.RecruitPrisoners`
  could hand you one. Nothing in the world generates such a prisoner today, so in practice they are inert too, but they
  sit on a live code path rather than a dead one.

`Tests/CampaignTests.cs:748-766` `Cultures` fails on the first victim (`azure_heart`: no tier VII line).

**Fix — pick one:**

- **(a) Delete.** Remove the 17 cultures and their 80 troops. The roster drops from 411 to 331, and `Cultures` passes.
  Cost: the subculture variety the world book asked for is gone.
- **(b) Wire them up.** Give each subculture a `home`, a faction to belong to, and lines in that faction's `lines`;
  then make `WorldMap` assign subculture culture to provinces by region instead of by faction. That is a world-gen
  change, a save-layout change (new `Layout` number, existing seeds must not move) and a design pass against
  `docs/world/world.json`.
- **(c) Keep the data, drop the claim.** Leave the troops as flavour for later, remove them from the roster checks,
  and note in `docs/TROOPS.md` that they are unreachable today.

(b) is the only one that gives you the variety. (a) is the only one that leaves the repo honest today.

**Risk if skipped:** the roster advertises 411 troops; ~80 of them cannot be met in play.

---

## 5. Couriers are built, switched off, and still drawn

**P1 — dead weight plus a lie on screen**

`BattleCourier.cs` is a complete system: riders that travel, can be cut down, re-route after being intercepted, and
delay an order.

It cannot run:

- `BattleDivision.cs:189` — `public const bool Riders = false;`
- `BattleDivision.cs:194` — `OrderDelay` returns 0 on its first line, so the whole body (lines 196–208) is
  unreachable, wrapped in `#pragma warning disable CS0162`.
- `BattleOptions.Couriers` is only ever set to `false` (duels, trials, tournaments), never true.

And yet the HUD draws them: `Hud.Divisions.cs:320` iterates `B.Couriers` and draws a flag-on-a-staff rider for each —
over an always-empty list.

**Fix — pick one:**

- **Turn it on** for normal field battles (`Riders = true`). Duels/tournaments already opt out via
  `BattleOptions.Couriers = false`. The behaviour is tested and `IssueGoal`/`Direct` already handle the delay path.
  `ReplayExact` will re-record, which is expected.
- **Or delete it:** `BattleCourier.cs`, the `OrderDelay` body, the `Riders` constant, and the HUD loop. Keeps
  `OrderDelay` as `=> 0` or removes it entirely.

Either is fine; leaving it as-is is the only wrong answer.

---

## 6. `Harass` and `Screen` cannot be chosen by anyone

**P1 — 11 stances advertised, 7 reachable**

`BattlePosts.cs:293-312` `AiStance` returns only `Hold`, `Advance`, `Charge`, `Guard`, `Swing`, `Plug`. It never
returns `Harass`, `Screen` or `Wait`. Of the 11 stances on the enum (`BattleDivision.cs:10`):

| Stance | Who can pick it |
|---|---|
| Hold, Advance, Charge, Guard, Swing, Plug | AI general and player |
| `Loose` | the player, and the AI never leaves it |
| `Harass`, `Screen` | player only |
| `Wait` | player only, and only as a state that flips to `Plug` |

The doc note in `docs/archive/OFFICERS.md` "As built" OF3 already says this ("skirmishers stay on Loose all battle (no switch
to Harass or Screen yet)"), so it is a known deferral rather than a bug — but it means a whole post's vocabulary is
half-inert, and the player's skirmish orders have no AI counterpart to read.

**Fix:** add two rules to `AiStance`:

- Skirmish + enemy foot within ~60 m → `Harass`.
- Skirmish + enemy horse inside the front line's span → `Screen`.

Or delete `Harass`/`Screen`/`Wait` and keep skirmishers on `Loose` honestly. Test `AiOnPosts` should cover whichever
you pick.

---

## 7. 8 tests red, 10 written but never registered

**P2 — but see issue 2 first, it hides this**

Red at HEAD `c0e186c` (all eight confirmed by running each alone):

| Test | File | Cause |
|---|---|---|
| `StylesByTier` | `Tests/StyleTests.cs:29` | issue 1 |
| `SweepHitsBeside` | `Tests/StyleTests.cs:45` | issue 1 |
| `KnockThrowsBack` | `Tests/StyleTests.cs:57` | issue 1 |
| `PassivesOnlyAdd` | `Tests/EffectTests.cs:156` | issue 1 |
| `Cultures` | `Tests/CampaignTests.cs:748` | issue 4 |
| `Economy` | `Tests/CampaignTests.cs:121` | issue 3 |
| `VeteransWrap` | `Tests/MeleeTests.cs:242` | officer work: "and trade better (2.5 kills a loss against 2.7)" |
| `Trolls` | `Tests/CreatureTests.cs:243` | officer work: night/day troll balance moved |

The last two are regressions from OF4–OF6, which shipped with their own balance guards red. They pass at `3ea9b0d`.

**Update (2026-11-17):** `VeteransWrap` and `Trolls` broke at OF5 (`3a48e10`, company sergeants), bisected. Trolls: the
sergeant's rally (2.5 morale a second) cancelled a troll's fear (2.5) exactly; a man under a `fear` creature is no longer
rallied. VeteransWrap: seed 52 is an unlucky draw (veterans trade better on 8 of 12 seeds); the trade is now averaged over
six. `PassivesOnlyAdd` was hiding a second failure behind Grail Knights: **Tempest Spears** (20) against 30 Storm Riders is
a near wipeout every time (0-7 men standing), and on 12 seeds the spears score 39 with their passives against 60 without.
`set_spears` is most of it (braced points kill the horse, the rider fights on afoot), but even `tireless` alone scores
lower. It passed before 902dce7 by luck (the same probe there: 54 against 31). Needs a call: a fairer matchup for this
pair (20 against 15 is steady: 360 against 372, `set_spears` still costs about a man), or a look at what bracing skirmishers do.

Test methods that exist but are not in the `Tests` array in `SelfTest.cs`, so never run in any mode:

- `UpgradeTreeVisibility` (`Tests/CampaignTests.cs:734`) — passes when run by hand. **Register it.**
- `TierSurvey` (`Tests/TierTests.cs:91`) — a survey that prints, not an assertion. Keep as a tool
  (`--selftest TierSurvey`), do not register.
- `HoldoutTrace`, `WolfTrace`, `TrollTrace`, `UndeadTrace`, `GraveTrace` (`Tests/CreatureTests.cs`),
  `BeastTrace` (`Tests/MonsterTests.cs:236`), `PilotTrace` (`Tests/CareerTests.cs:8`) — debug helpers. Delete or move
  behind a debug flag.
- `DbgArc` (`Tests/Dbg.cs:5`) — scratch file. **Delete `Tests/Dbg.cs`.**

---

## 8. Ten public methods with no callers; three of them are missing features

**P2**

A whole-tree call-site scan (declarations plus every `.cs`) finds ten methods whose only mention is their own
declaration.

**Dead — delete:**

| Method | File | Note |
|---|---|---|
| `MusicLibrary.ByCategory` | `MusicScore.cs:366` | `MusicDirector.Pool` replaced it |
| `AudioEngine.Intensity` | `Audio.cs:212` | callers use `SetStemWeights` directly |
| `AudioEngine.RequestPiece` | `Audio.cs:228` | directors call `StartPiece` |
| `AudioStreamer.SetVolume` | `AudioStreamer.cs:61` | `Pump` writes the volume itself |
| `Hud.GradeTag` | `Hud.cs:204` | cards stopped printing RAB/TRN/VET |
| `Campaign.CultureName` | `Campaign/Companies.cs:69` | one-line wrapper on `Factions.CultureOf` |

**Unwired — these are features that exist but no screen calls:**

| Method | File | What is missing |
|---|---|---|
| `Campaign.AssignCompanion` | `Campaign/Characters.cs:326` | nothing in the UI moves a companion to another company. Companions can never be reassigned once placed. |
| `Campaign.AutoTrial` | `Campaign/Oath.cs:88` | the stand-in captain's way to pass an order trial without a fight. Nothing calls it, so `Pilot` can never break an aura wall by trial — which distorts every `--pacing` and `--arcana` number the docs quote. |
| `Campaign.Aura.TrialsFor` | `Campaign/Aura.cs:144` | `KNIGHT_ORDERS.md` 5.5 says the K sheet lists the trials open at your tier. The sheet draws `NearestTrials()` instead, which is a different thing (where to go, not what to do for the next tier). |

**Fix:** delete the six; wire `AssignCompanion` to the household card; call `AutoTrial` from `Pilot` alongside the
existing `AutoTournament` call; decide whether the K sheet wants `TrialsFor` or `NearestTrials` and remove the loser.

---

## 9. C5 (the camp) has no screen

**P2 — largest remaining gap against the game's own fantasy**

`docs/COMPANY.md` C5 plans a screen where the company exists between battles: men at rest, fires, the banner planted,
faces you can click, camp events. Everything it depends on already shipped:

- `CreatorStage` — the living puppet drawn into a render target.
- `Campaign/Talk.cs` + `Data/talk.json` — lines picked by temper.
- The `men` event family verbs (`man_deed`, `man_hurt`, `man_leaves`, `tie`, `mention`).
- `PortraitPainter.Of(Soldier, CompanyRecord)` — faces seeded the same way the battle seeds them.
- Natures, ties, the roll, the captain's choices.

There is no `Act.Camp` and no `PanelKind.Camp` (grep: zero hits). Rites and the feast live in the party screen.

**Fix:** C5 only, no new systems. One screen, the stage, the men as clickable faces, the existing talk panel for a
man's page. Nothing else in that phase needs to land with it.

---

## 10. `docs/COMPANY.md` status line is false

**P2 — trivial, and it misleads every fresh session**

`docs/COMPANY.md:3` says:

> Status: **planned** (nothing built yet).

Nine phases have shipped with their own "As built" notes at the end of the same file: C0, C1, C2, C3, C4, C9, C10,
C11, C12, C13, C14. Only C5–C8 remain open.

Fix the status line to name what is built and what is left.

**Correction to an earlier draft of this report:** `docs/NOBILITY.md:3` saying "N3 planned" is *accurate* and must not
be changed. Own-order knighting (`Campaign/OwnOrder.cs` `Knight`) sets `Soldier.Knight` — it joins your knight order.
N3 is a different thing: promoting one named man's `Troop` to his noble upgrade. `Campaign/Roll.cs:211` is the only
place a man's troop changes, and it is promotion, not knighting. N3 is genuinely not built.

`docs/CAMPAIGN.md:3` points at `CAMPAIGN_OVERHAUL.md` as "roadmap phase F will replace much of it"; that overhaul is
built (F0–F9) and archived. Reword to past tense.

---

## 11. Two landing pages have diverged

**P2 — trivial**

`docs/index.html` (86 kB, Nov 14) and `site/index.html` (95 kB, Nov 11) are two hand-written self-contained pages for
the same game. They already differ in title, meta description, OG tags, the font link and image paths (8 image
references in `site/`, 0 in `docs/`).

**Fix:** keep `docs/index.html` (it is the tracked, newer one and referenced from the docs), delete `site/`, or make
`site/` a build step that copies `docs/`.

---

## 12. Junk in the repo root

**P2 — trivial**

| Path | State | What it is |
|---|---|---|
| `Untitled` | untracked, 9 bytes | `#cd Iron`, an editor accident |
| `BlenderMCP/customer_uuid.txt` | **tracked**, 36 bytes | a per-machine id; should never be in git |
| `dist/` | untracked, 40 MB | a built AppImage; already in `.gitignore` |
| `.midas/` | untracked | tool state |

**Fix:** `git rm --cached BlenderMCP/customer_uuid.txt` (and add to `.gitignore`), delete `Untitled`, add `.midas/` to
`.gitignore`.

---

## 13. 63 keys, no way to learn them in the game

**P3**

`Controls.cs` defines 63 `Act` members and all 63 are bound. The F1 help lists them by area, but there is no way for a
new player to find out which of the 63 apply to the screen they are looking at.

**Fix:** group the help and the settings screen by view (map, command view, fight view) — the code already comments the
enum in exactly those groups, so this is presenting what already exists rather than new structure.

---

## 14. `Hud.cs` still carries the pre-PT4 hooks

**P3**

Two HUD rebuilds left the old widgets in place:

- `Hud.cs:341` `GoalLines` — personal battle goals under the balance bar.
- `Hud.cs:400` `FormBar` — the captain's forms.
- `Hud.cs:439` `SpellFan` — the spell fan.
- `Hud.Divisions.cs:130` `GoalPanel` — the pre-deck officer goals panel.

All four are still called from `Draw()` and `DrawCommand()`, and each early-returns when its state is unset, so nothing
draws wrongly — but a layout change now reads as archaeology.

**Fix:** one delete pass once PT4/OF7 settles. Not urgent; do it when the next HUD change happens anyway.

---

## How this was checked

- Every file:line above verified in the working tree at `c0e186c`.
- Test state: all 286 tests run one per process (26 min), 278 pass, 8 fail — the eight listed in issue 7. Then two
  independent 8-shard runs to measure the 132/286 figure in issue 2.
- Troop data: `Data/troops.json` and `Data/factions.json` parsed and cross-checked for line membership, upgrade
  reachability, culture assignment and weapon names.
- Call-site scan for issue 8: every declaration, then every mention across all `.cs` files.
- Not checked: renderer, shader and puppet internals beyond their call sites; audio synth internals; UI kit internals;
  visual quality (needs the render passes and a judge — PT5 last judged 6/10 and is still open).
