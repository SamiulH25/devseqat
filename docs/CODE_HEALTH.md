# Code health: how big the game is, and what to clean up

Measured 2026-11-22 on `main` (working tree dirty; the numbers are line counts, which is what this doc is for).

This is an audit of **shape**, not of defects. The last defect audit was `docs/archive/ISSUES.md` (2026-11-17); its open
items live in `docs/BACKLOG.md` under "Code debt". Nothing here is broken. This is about the cost of the next change.

## 1. Size

**349 C# files, 97,933 lines** (excluding `bin/`, `obj/`, and the two stale git worktrees under `.claude/worktrees/`).

| Area | Files | Lines | Share |
|---|---:|---:|---:|
| repo root (sims, screens, rendering, audio) | 231 | 67,194 | 69% |
| `Campaign/` (headless campaign sim + data model) | 93 | 19,017 | 19% |
| `Tests/` (the `--selftest` suite) | 25 | 11,797 | 12% |

Around the code, for scale: `Data/*.json` 18,512 lines, `docs/**/*.md` 15,176, `tools/*.py|*.sh` 9,981,
plus the drawn world (`Data/world.json`, ~4.5k lines) that the game reads rather than computes.

A naive `find . -name '*.cs' | wc -l` on the repo says **247,615 lines and 929 files**, because
`.claude/worktrees/wm6` (1.7 GB) and `pixel-map` (430 MB) hold full stale copies of the tree. They are excluded only via
`.git/info/exclude`, so they are invisible to `git status` but wreck any tool that walks the filesystem, including
`rg`-based scripts and codemap tooling. **Delete both**; they give back 2.1 GB and a truthful line count.

### What the size is made of

Roughly a third of the code draws things, and that part *should* be big: it is hand-written pixel and mesh geometry, not
logic that a pattern can shrink.

| Kind | Lines | Where |
|---|---:|---|
| Painted sprites (`SpriteSheet*`: parts, peoples, lore, items) | 3,479 | root |
| World meshes and map dressing (`World*` 16 files) | 4,719 | root |
| Painted towns and streets (`MapScreen.Street*`, `MapScreen.Townscape*`) | 4,544 | root |
| Procedural sound and music (`AudioSynth*` 4 files, `AudioBank`, `MusicScore`) | 3,084 | root |
| Battle rendering (`BattleScreen*` 18 files) | 6,150 | root |
| Campaign UI (`MapScreen*` 32 files) | 12,626 | root |

The real logic — `Battle*` sim, `Campaign/` sim, `Rules`, `Terrain` — is a much smaller slice than the file list suggests.
So "make it smaller" mostly means "stop one class owning too much", not "delete code".

## 2. Cleanup targets, ranked

Ranked by (lines reclaimed × risk of not touching it). Every entry was measured, not guessed.

### T1. `MapScreen`: 32 partials, 12,626 lines, one class

This is the god object. It owns: the 3D map camera and input, every panel and tab, the settlement street painter, the
shop/armoury/inventory/muster/party board screens, save/load dialogs, event dialogs, and the shot staging.

Two things make it worse than the number suggests:

- `MapScreen.Settlement.cs` (764) plus `Inventory.cs` (600), `Camp.cs` (597), `Board.cs`, `Party.cs`, `Muster.cs`,
  `Upgrade.cs` (489) are a **second screen** — the town interior — living inside the map screen's class.
- `MapScreen.Draw` (155 lines) and `MapScreen.Update` (101 lines, cx 55) have no seams left to hang another panel on.

**Refactor:** give the settlement views their own class (they already take the street painter, the campaign and a `Ui`);
keep `MapScreen` for the map, the camera, the top bar and routing. Move `Stage` (168 lines, `MapScreen.cs:904`) into a
`MapScreen.Shots.cs` alongside the other dev-only code (T2). Nothing else in this doc buys as much.

### T2. Dev-only shot code inside production classes (~1,500 lines)

Roughly 4,000 lines exist only to produce screenshots and test saves, and about 1,500 of those sit in classes the game
runs every frame.

| What | Where | Lines |
|---|---|---:|
| `CampaignShots` | `ScreenHost.cs:676` | 459 |
| `PlaceShots`, `SaveShots`, `CampShots`, `EventShots` | `ScreenHost.cs:531-676` | ~145 |
| `ShotScript` | `BattleScreen.Shots.cs:12` | 286 |
| `Stage` | `MapScreen.cs:904` | 168 |
| `CommandControls` | `BattleScreen.Controls.cs:103` | 193 |

`ScreenHost.cs` is 1,136 lines and more than half of it is shot staging.

**Refactor:** a `Shots/` folder with one `ShotDirector` (they all share `CaptureShot`, `pendingShot` and the refusal
path already). Screens keep a single `PrepareShot(string id)` hook. This is the cleanest win available: no behaviour
changes, no risk to the sim, and it makes the screens readable again.

### T3. Long methods with numbered phases and no names (180 methods over 60 lines, 31 over 80)

The worst offenders already contain the extraction plan in their comments.

| Method | Lines | cx | Natural steps already in the code |
|---|---:|---:|---|
| `Puppet3D.TryDraw` (`Puppet3D.cs:297`) | 479 | 85 | its own comments read "Far LOD", "Phase 3.5 locomotion", "1. Legs", "2. twist", "3. Head", "4. Arms", "Crossbow", "Bow", "Couched lance", "Sling" |
| `CampaignBattle.Apply` (`CampaignBattle.cs:11`) | 219 | 76 | "Aftermath for each party", "Personal goals met", plus wounds/killed/XP/loot/ransom/prisoners/officers |
| `SpriteSheet.Helm` (`SpriteSheet.cs:324`) | 360 | — | one branch per helm kind, 30+ kinds |
| `SpriteSheet.Peoples.PeopleHelm` (`.Peoples.cs:37`) | 268 | — | same shape, per people |
| `SpriteSheet.Lore.LoreHelm` (`.Lore.cs:19`) | 200 | — | same shape |
| `World.Kit.Dwelling` (`World.Kit.cs:192`) | 214 | — | one branch per house kind |
| `SpriteSheet.Weapon` (`SpriteSheet.cs:1228`) | 211 | — | one branch per weapon |
| `BattleScreen.Draw.BuildSprites` (`.Draw.cs:364`) | 191 | — | one block per sprite family |
| `SettingsScreen.Draw` (`SettingsScreen.cs:50`) | 150 | 54 | one block per settings group |
| `BattleCombat.Hit` (`BattleCombat.cs:128`) | 142 | 53 | block, parry, flank, speed, slope, armour |
| `PuppetPoses.PoseOf` (`PuppetPoses.cs:111`) | 140 | 37 | idle / walk / strike / brace / mount |
| `Puppet3D.Banner` (`Puppet3D.cs:1011`) | 135 | — | see T4 |
| `Puppet.Draw` (`Puppet.cs:218`) | 133 | — | the 2D mirror of `TryDraw` |

**Refactor:** cut `TryDraw` into `DrawFoot`, `DrawMounted`, `DrawBeast3D`, `DrawFarLod` and per-limb helpers, in that
order. Same for `Apply` (one method per aftermath step). The `switch (kind)` painters are better expressed as a table
(T5) than as five 200-line methods.

### T4. Two renderers for one skeleton

`Puppet.cs` (1,102) and `Puppet3D.cs` (1,395) both turn a `Unit` + `Look` + `Pose` into a drawn man. They share the
vocabulary (`Sink`, `Knee`, `Elbow`, `RotY`, `HorseScale`, `Banner`) but reimplement the body of it, and their `Banner`
signatures are identical ten-parameter lists.

**Refactor:** extract the shared skeleton maths (joint angles from a `Pose`, horse geometry, banner wave) into a
`Skeleton`/`PoseSolve` type both renderers consume. That is where the drift between 2D and 3D will bite later, and it
also removes two of the ten-parameter methods.

### T5. Magic strings for ids and kinds (1,776 troop-id literals outside `Tests/`)

`Troops.Get(id)` throws `KeyNotFoundException` at runtime on a typo, and the ids are written as bare literals in the sim,
the AI, the campaign and the UI.

| Literal | Occurrences |
|---|---:|
| `"levies"` | 435 |
| `"infantry"` | 388 |
| `"men_at_arms"` | 307 |
| `"horse"` | 286 |
| `"shieldmen"` | 264 |
| `"wolf"` | 149 |

Across the codebase there are 129 `switch` statements and 621 `case "..."` labels, i.e. dispatch on a string in place of
an enum or a table.

**Refactor, in this order:**

1. A `TroopIds` static class of `const string` for the ~40 ids used in code, and `Troops.Get(TroopIds.Levies)`. Typos
   become compile errors and rename becomes a solution-wide edit. No behaviour change.
2. Convert the `switch (kind)` dispatchers that have a fixed, closed set of cases to lookup tables
   (`static readonly Dictionary<string, Func<Canvas>> Helms`), which also kills the five long methods in T3 at once.
3. Leave genuinely open, data-driven strings alone (`Data/*.json` keys, event ids, effect verbs): they are validated by
   `Troops.Validate`, `Gear.Validate`, `Factions.Validate`, `Effects` and `ValidateEvents`, which is the right place for
   the check.

### T6. Long parameter lists (46 methods over 6 parameters)

| Method | Params |
|---|---:|
| `AudioSynth.Voices` | 11 |
| `Puppet3D.Banner` / `Puppet.Banner` | 10 each |
| `Shading.PixelPostTo` | 10 |
| `World.Dress.Blades` | 10 |
| `World.Frustum` | 8 |
| `World.Kit.Merlon`, `GableRoof`, `Drum`, `House` | 8 each |
| `Companies.Promotable` | 9 (two `out`) |

**Refactor:** `BannerStyle`, `VoiceSpec`, `Placement` (position, size, yaw) as small readonly structs. The mesh helpers
are the same argument set every time and will pay for themselves the first time one more is added.

### T7. Files past the project's own limit

`CLAUDE.md` says a class past ~600 lines gets split and a method past ~80 gets named steps pulled out. Twenty-four files
break the file rule; 31 methods break the method rule. Worst: `SpriteSheet.cs` 2,222 (13 members, so long local
functions), `Puppet3D.cs` 1,270, `World.cs` 1,258, `ScreenHost.cs` 1,091, `BattleScreen.Draw.cs` 1,053, `Puppet.cs`
1,025, `MapScreen.cs` 1,013, `Fortress.cs` 928, `AudioSynth.cs` 864, `BattleScreen.Shots.cs` 840 (non-blank lines; the
`wc -l` figures are a little higher).

`SpriteSheet.cs` splits cleanly by subject with no code change: `.Helms.cs`, `.Weapons.cs`, `.Torsos.cs`, `.Beasts.cs`,
`.Barding.cs` (`.Peoples`, `.Lore`, `.Items` already show the pattern).

### T8. Very large test files

`Tests/BattleTests.cs` 2,193 and `Tests/CampaignTests.cs` 2,100 are single files longer than most subsystems. They shard
across processes fine (`SelfTestShards`), so this is navigation cost only. Split along the plans they cover (melee,
ranks, orders, divisions; campaign, career, fiefs, diplomacy).

## 3. Order of work

| Step | Why first | Risk |
|---|---|---|
| 1. Delete `.claude/worktrees/*` | truthful tooling and 2.1 GB | none |
| 2. `Shots/` folder (T2) | biggest readability win per hour; behaviour-preserving | low |
| 3. Mechanical partial splits (T7 SpriteSheet, `MapScreen.Stage`→`.Shots.cs`) | no logic moves at all | none |
| 4. Named-step extraction in `TryDraw` and `Apply` (T3) | safest long methods, comments already name the steps | low |
| 5. `TroopIds` (T5.1) | compile-time safety across the whole tree | low |
| 6. Dispatch tables replacing `switch (kind)` painters (T5.2) | collapses five long methods | medium (rendering output must be byte-identical; check with `--sprites`, `--bake-sprites --check`, `IRONFIELD_TROOPS`) |
| 7. Parameter structs (T6) | small, local | low |
| 8. Split the settlement screens out of `MapScreen` (T1) | largest structural change, do it when the demo's UI is settled | medium |
| 9. Shared skeleton for the two renderers (T4) | the deepest change; worth it only once T3 landed | medium |

Steps 2–5 have no expected effect on `--selftest` output. From step 6 on, run `tools/check.sh` and compare the printed
selftest numbers, plus the sprite checks named above.

## 4. Keeping it clean

The codebase is not rotten: there are **zero** `TODO`/`FIXME`/`HACK` comments, and 17,261 `///` doc lines against
~98k of code. The cost is concentration, not neglect. Two cheap guards would stop it drifting back:

- A `tools/check.sh` rule that fails on any `.cs` over 900 code lines (the current 24 are listed in T7, so raise the
  ceiling to today's worst and ratchet it down as they split).
- The same for methods over 120 lines, with the same ratchet.

Neither needs new tooling: `tools/codemap.sh` already walks every declaration.
