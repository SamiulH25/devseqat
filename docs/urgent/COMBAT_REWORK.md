# Combat Rework: directional melee, troop combat & camera (plan)

The owner's brief (2026-11-25): the current melee combat is far too basic — treat it as non-sacred. Rebuild combat around **Direction A** (Mount & Blade / Chivalry / Mordhau-style directional melee) with **better combat camera handling**, and unify the new melee mechanics across **both the player and AI troops**.

Phase prefix: **CR** (`CR0`–`CR7`).
Branch: `combat-rework` (branched from `demo-sprint` @ `abd3e09`).

---

## Where things stand (2026-11-25)

**Player & troop melee (`BattleCombat.cs`, `BattlePlayer.cs`, `BattleCaptain.cs`):**
- Melee is a single-tick distance check, not a physical swing. Releasing LMB runs a `~0.15s` `Windup`, fires one frame of `InArc(reach, minDot)` in `Strike()`, then locks into `0.3s` of `Recover`. There is no `Active` swing phase, no angular sweep, no accels/drags, no weapon-on-weapon clash/chambering, and no forward attack lunge.
- `BattlePlayer.cs` slows player movement to `0.3x` during `Windup`/`Recover` and `0.5x` while charging or blocking — feet feel glued to mud mid-fight.
- There is no sidestep/dodge, no input buffering (inputs during late `Recover` are dropped), and `Chain` actively *punishes* consecutive swings by raising enemy RNG block chance (`+8%` per swing, up to 95%) and draining extra stamina.
- Shields auto-block all 4 directions while holding RMB; directional blocking only matters for two-handed weapons or timed parries.
- Weapon classes (sword, greatsword, axe, mace, polearm) all run the same 1-tick `InArc` loop with slightly different `Reach`/`Damage`/`Recover` numbers.

**Troop AI & blocking (`BattleAI.cs`, `BattleCombat.cs`):**
- In `BattleAI.Think`, melee fighters walk in a straight line to `reach` and spam `StartSwing` on cooldown. There is no spacing, strafing, circling, feinting, or 1vX attack-slot coordination (in `1v2`/`1v3`, enemies stack into one blob and swing on top of each other).
- Enemy blocking is pure RNG (`rng.NextSingle() < chance`) rolled on the exact frame a strike lands. Enemies do not hold a readable directional guard, so player attack directions barely matter on offense.

**Combat camera & input (`BattleScreen.Controls.cs`):**
- `UpdateCamera()` snaps rigidly to `battle.Player.Pos` every tick with zero spring/damping (terrain ripples and collision bumps jitter the view), has no wall/building pull-in (clips inside fortress masonry), and has no soft target lock-on or close-duel framing.
- `aimAcc = aimAcc * 0.8f + d` updates `aimDir` from every raw mouse delta, so panning the camera to track a flanking enemy constantly flips your guard/attack direction.

**Visual & HUD feedback (`BattleScreen.Effects.cs`, `Puppet3D.cs`, `Hud.Melee.cs`):**
- `hitPause` is only `0.045s` on `DMG` (none on parries, guard-breaks, or kills); there are no steel clash sparks or directional knockback steps.
- `Hud.Melee.cs` prints plain text (`SWINGS: BLOCK LEFT`) instead of a directional combat reticle.

**What already exists to build on:**
- **Selectable Duel Arena** (`QuickBattleConfig.cs`, `QuickBattleScreen.cs`, `Battle.cs`, `IRONFIELD_DUEL=1v3`) — instant `1v1` to `5v6` foot/mounted combat testbed committed at `abd3e09`.
- `BattleRanks.cs` (rank cohesion & frontage), `BattleStyles.cs` (weapon style hooks), `Puppet3D.cs` (3D humanoid rig + `EmitSlashArc`), and `BattleCaptain.cs` (parry/riposte/bash/weapon-swap hooks).

---

## The target

One unified, physical directional melee system shared by both the player and AI troops, paired with a responsive combat camera:

- **Swings are physical sweeps over time** (`Windup → Active → Recover`) — turning into a swing accelerates it, dragging delays it, cleaves hit targets sequentially across the arc, and colliding blades trigger **clashes / chambers**.
- **Guards are real and readable, not RNG dice rolls** — every fighter holds an explicit `GuardDir` visible in their 3D pose; feints, morphs, kicks/bashes, and guard-crush weapons break turtles cleanly.
- **Footwork feels alive** — forward lunge on swings, `Space` sidestep dodge, 150ms input buffering, and smooth opposite-side combo chains.
- **Every weapon class has a distinct role** — Sword & Shield, 2H Greatsword, Guard-Crush Axes/Maces, Sweet-Spot Spears/Polearms, Bows, and Mounted Lances.
- **1vX duels are tactical** — enemies space, strafe, circle to flanks, and pace attack slots instead of clipping into one blob.
- **Large battles stay fast & deterministic** — headless sim, seeded `rng` only (`ReplayExact` passes), and proximity-tiered AI fidelity so `--bench 1500` stays ≤ 5 ms/tick.

---

## Architecture: One Combat System, Two Fidelity Tiers

| Layer | Mass Battle Ranks (`> 18m` from player, non-duel) | Near Player & Duel Arena (`<= 18m` or `Options.Duel`) |
|---|---|---|
| **Swing & Hit** | Same 3-phase `Windup → Active → Recover` state machine (simplified single arc check) | Full per-tick angular sweep, accels/drags, sequential cleave, blade clashes/chambers |
| **Guard & Posture** | Explicit `GuardDir` updated by tier/drill & threat; stamina guard-break | Active directional guard reading incoming swings, feint reactions, timed parries |
| **Footwork & AI** | Rank cohesion (`BattleRanks.cs`) | Dueling brain (`BattleDuelAI.cs`): spacing, strafing, 1vX flank/slot pacing, lunges |

---

## CR0. Baseline & Duel Arena harness (Done — `abd3e09`)

- Selectable Duel Arena (`1v1`, `1v2`, `1v3`, `1v4`, `2v2`, `2v3`, `3v3`, custom `1..5` vs `1..6`, foot/mounted, custom fighter picker) in Battle Lab and via `IRONFIELD_DUEL=1v3`.
- Before starting CR1, record baseline numbers:
  - `dotnet run -c Release -- --bench 1500` (target: keep ≤ 5 ms/tick)
  - `dotnet run -c Release -- --tiers 3` and `--agree 4 5` (sanity check after CR3/CR4)

---

## CR1. Combat camera & responsive controls

**Files:** `BattleScreen.Controls.cs`, `BattleScreen.cs`, `Controls.cs`, `Settings.cs`

1. **Damped over-the-shoulder combat camera:**
   - Critically damped smoothing on camera focus height and lateral shoulder offset (zero terrain-ripple jitter, 1:1 mouse-look responsiveness).
   - Closer default combat distance (`fieldDist` ~`3.8m` on foot, `5.2m` mounted; scroll `2.4m`–`10m`) plus an optional close-duel / first-person toggle.
   - Dynamic FOV (`+5°` on sprint/gallop, slight tighten on charged swings) and **directional camera kick** aligned to swing/impact vectors.
   - **Wall/building pull-in:** segment test from `focus` to `eye` against `Fortress` walls & village houses so the camera never clips into geometry.
2. **Optional Soft Lock-On (`MMB` / `Tab`):**
   - Locks camera framing onto the active melee opponent; flick/wheel/key cycles targets in `1v2`/`1v3`/`2v2`.
   - While locked on, mouse movement is dedicated cleanly to 4-way attack/guard direction without fighting camera yaw.
3. **Decoupled directional input (Free-Look):**
   - Fix `aimAcc` jitter with a clean deadzone, idle decay, and **lock-on-press** behavior so casual camera panning does not flip your guard.
   - Add an optional setting for **Movement-key direction** alongside **Mouse-flick direction**.
4. **Input buffering (150ms):**
   - Queue Attack, Block, Dodge, and Kick/Bash pressed during late `Active`/`Recover` so follow-ups trigger on the first available frame.

**Verify (CR1):** Playtest `IRONFIELD_DUEL=1v3` and `2v2` — verify zero camera jitter on slopes, no wall clipping in `TOWN SIEGE`, clean lock-on target switching, and zero dropped inputs.

---

## CR2. Active swing physics, clashes, feints, footwork & combos

**Files:** `Units.cs`, `BattleTypes.cs`, `BattlePlayer.cs`, `BattleCaptain.cs`, `BattleCombat.cs`

1. **3-phase melee state machine (`Windup → Active → Recover`):**
   - Add `UnitAction.Active` (duration `0.12s`–`0.20s` by weapon weight) and per-unit `SwingProgress` (`0 → 1`) + `HitMask` (or small fixed hit list) so each target is struck at most once per swing.
   - During `Active`, sweep the weapon ray/arc from `-arc/2` to `+arc/2` (horizontal cuts), top-down (overhead), or forward extension (thrust).
   - **Accels & Drags:** rotating into the swing hits earlier; rotating away delays contact to bait early blocks.
   - **Cleave-stop:** heavy 2H blades cleave through light targets (tapering damage per target); 1H weapons & axes stop on heavy armor/shields unless fully charged.
2. **Blade clashes & chambering:**
   - When two fighters' swings intersect in `Active`/`Windup` from mirrored directions, trigger a **Blade Clash / Chamber** (steel ring, spark burst, brief recoil pushback, fast counter-windup).
3. **Feints & morphs:**
   - **Feint:** RMB (or `Q`) during the first 60% of `Windup` cancels cleanly into block/neutral for a small stamina cost.
   - **Morph:** changing direction during early `Windup` smoothly transitions e.g. Overhead → Thrust.
4. **Footwork, lunge, dodge & bash:**
   - Remove the `0.3x` mud-glue crawl during swings; add a **forward lunge impulse** on swing release (scaled by weapon & movement input).
   - Add **Sidestep / Dodge** (`Space`, `~0.22s` directional burst, `22` stamina, brief cooldown, no i-frame cheese — real spatial evasion).
   - Rework **Kick / Shield Bash** (`F`): fast short-range posture breaker that staggers a turtling shield or interrupts a close windup.
5. **Combo flow (replace punishing `Chain`):**
   - Buffering an opposite-side swing near the end of `Active` skips half of `Recover` into a fluid combo (`Left → Right → Overhead`) instead of artificially inflating enemy RNG block chance.

**Verify (CR2):** Add unit tests in `Tests/BattleTests.cs` for `Active` sweep hit timing, sequential cleave, chamber clash, feint cancel, dodge impulse, and combo cancel. Verify `ReplayExact`.

---

## CR3. Unified directional guard & non-RNG troop combat

**Files:** `Units.cs`, `BattleCombat.cs`, `BattleAI.cs`, `BattleRanks.cs`

1. **Explicit `GuardDir` replaces RNG blocking:**
   - Every unit tracks `GuardDir` (`Left`, `Right`, `Overhead`, `Thrust`) and `BlockStart`.
   - **Unshielded / 2H fighters** must match `Dirs.Mirror(incoming.Swing)` to block cleanly; wrong-side guards take heavy bleed-through.
   - **Shields** cover a generous front cone at stamina cost (vulnerable to flanks, kicks/bashes, and heavy axe/mace guard-crush), while **directional matching** triggers a free **Timed Parry / Riposte**.
2. **Universal stamina & guard-break (Player + Troops):**
   - Blocking drains stamina proportional to weapon impact (`Crush` weapons deal `1.6x` posture damage).
   - Zero stamina on block triggers **Guard Break** (`0.9s` stagger, weapon/shield flung wide, open for a punishing blow).
3. **Troop integration & mass-battle performance:**
   - All troops run the same `Windup → Active → Recover` and `GuardDir` rules.
   - Troop tier & `Squad.Drill` govern guard-switch reaction speed, stamina recovery, and riposte rate (levies `T1–T2` hold static guards and break easily; elites `T5–T6` shift guards fast and punish reckless swings).
   - Troops `> 18m` from the player in mass battles use a fast simplified arc check during `Active` to keep `--bench 1500` within budget.

**Verify (CR3):** Run `--selftest`, `--bench 1500`, `--tiers 3`, and `--agree 4 5`; tune base damage/stamina constants so tier progression and battle lethality remain balanced.

---

## CR4. Weapon class identities & move sets

**Files:** `BattleCaptain.cs`, `BattleCombat.cs`, `BattleStyles.cs`, `Data/troops.json`

| Weapon Class | Distinct Mechanics |
|---|---|
| **Sword & Shield** | Fast `Windup`/`Recover`, forgiving shield cone, fastest riposte speed, shield bash (`F`) staggers foes; lower armor pen. |
| **Greatsword / Longsword (2H)** | Wide cleave arc, strong combo chaining, high-commitment overhead with brief poise/hyper-armor, directional blade parries. |
| **Axes & Maces / Warhammers** | Heavy **guard-crush** (`1.6x` stamina damage vs shields/blocks) and high armor penetration; shorter reach, heavier whiff recovery. |
| **Spears & Polearms** | Dominant **Thrust** reach with a **tip sweet-spot** (bonus damage at optimal range, reduced damage when crowded face-to-face); sweep/overhead pushes foes back to reset spacing. |
| **Bows / Crossbows** | Draw-tension sweet spot, clean aim stability, fast sidearm swap when rushed. |
| **Mounted & Lances** | Speed-scaled side cuts, couched lance impact impulse & shield-shatter, horse turn-in momentum. |

**Verify (CR4):** Test every weapon kit in Duel Arena (`1v1` & `1v3`) and add a weapon-identity self-test verifying guard-crush, polearm sweet-spot, and greatsword cleave.

---

## CR5. Smart dueling & 1vX AI

**Files:** `BattleAI.cs`, `BattleDuelAI.cs` (new partial)

For all fighters in `Options.Duel` and enemies engaging near the player (`<= 14m`):
1. **Spacing & footwork:** Maintain preferred weapon distance (`~0.85 * reach`), strafe/circle laterally instead of nose-clipping, and step back briefly after a whiff or when low on stamina.
2. **1vX flank circling & attack-slot pacing:** When 2–6 enemies fight one target (`1v2`, `1v3`, `1v4`), assign **1–2 active attackers** while remaining foes **circle to flanks/rear** at `3.0m–4.5m` and step in to punish if you turn your back or stall.
3. **Tier-scaled dueling brain:**
   - `T1–T2` (Levies/Bandits): Predictable swings, slow guard shifts, rarely feint.
   - `T3–T4` (Men-at-Arms/Veterans): Shift `GuardDir` toward player windups, punish spam, occasionally feint or kick a turtling shield.
   - `T5–T6` (Knights/Champions/Lords): Fast guard reads, feint-into-thrust, riposte after parrying you, disciplined spacing.

**Verify (CR5):** Playtest `IRONFIELD_DUEL=1v1`, `1v2`, `1v3`, `2v2`, `3v3` across tiers `T1`–`T6`. Add `DuelAISpacing` self-test verifying 1v3 enemies spread around flanks rather than stacking on one point.

---

## CR6. 3D combat telegraphs, impact feel & directional reticle HUD

**Files:** `Puppet3D.cs`, `BattleScreen.Effects.cs`, `Hud.Melee.cs`

1. **Readable 3D telegraphs (`Puppet3D.cs`):**
   - Exaggerate the 4 directional windups (`Left`, `Right`, `Overhead`, `Thrust`) and held `GuardDir` poses on 3D humanoids so opponents can be read purely by watching their weapon/body.
   - Drive `EmitSlashArc` directly from `UnitAction.Active` `SwingProgress` (`0 → 1`).
   - Add clear body poses for **Guard-Break**, **Blade Clash**, **Kick/Bash**, and **Sidestep Dodge**.
2. **Weighty impact feedback (`BattleScreen.Effects.cs`):**
   - Tiered **Hitstop**: light hit (`35ms`), heavy/kill (`65ms`), timed parry/chamber (`55ms`), guard-break (`70ms`).
   - **Steel Clash Sparks** on parries, chambers, and shield blocks; directional blood/dust bursts aligned to swing arc; subtle physical knockback step on heavy blows.
3. **Directional combat reticle (`Hud.Melee.cs`):**
   - Replace plain `SWINGS: BLOCK LEFT` text with a crisp **4-chevron combat reticle** around center screen:
     - Gilt chevron = your active attack/guard direction.
     - Flashing crimson chevron = incoming enemy swing side to block/parry.
     - Bright gold pulse = active **Riposte** or **Chamber** window.
   - Compact **Stamina / Posture bar** right beneath the reticle during melee.

**Verify (CR6):** Capture and inspect Duel & Battle HUD screenshots (`IRONFIELD_SHOTS`, `IRONFIELD_SHOTS_FROM=hud`, `overflow.txt` clean).

---

## CR7. Balance, determinism, perf & full verification

- Run full `tools/check.sh` (`ALL CHECKS PASSED`, including full `--selftest` & `ReplayExact`).
- Run `dotnet run -c Release --no-build -- --bench 1500` (verify ≤ 5 ms/tick).
- Run `dotnet run -c Release --no-build -- --tiers 3` and `--agree 4 5` (verify troop tier ladder & auto-resolve agreement still hold).
- Update `docs/CODEMAP.md` and `CLAUDE.md` for any new files/methods, and record "As built" notes below as each phase ships.

---

## Open design calls (resolved)

1. **Direction input default:** Mouse-flick (with deadzone + idle decay + lock-on-press) as default, with Movement-key direction (`Settings.MoveDirControl`) as an option, plus `MMB` tap / `Tab` (in Duel) soft lock-on. *(Adopted.)*
2. **Shields:** Omni-directional block at stamina cost (vulnerable to flanks, kicks/bashes & `1.6x` axe/mace guard-crush) + directional matching for a free Timed Parry/Riposte. *(Adopted.)*
3. **Dodge binding:** `Space` (`Act.Dodge`) for sidestep dodge on foot (keeping `Shift` for sprint, `CTRL` for kick/bash, and `Space` for Pause in command view/deployment). *(Adopted.)*

---

## As built (2026-11-25)

- **CR0 Baseline:** `--bench 1500` recorded at **7.52 ms/tick** mean (p95 8.49 ms, 2982 men).
- **CR1 (Camera & Controls):** Critically damped focus height and lateral shoulder offset (`BattleScreen.Controls.cs`), closer default `fieldDist = 3.8m` (scroll `2.4m–10m`), `F4` (`Act.CameraView`) OTS / Close Duel / First-Person toggle, dynamic FOV (`+5°` sprint, `-4°` charged swing), spring-damped directional camera kick (`KickDir` in `BattleScreen.Effects.cs`), Fortress wall/tower/building camera pull-in (`PullCameraIn`), `MMB` tap / `Tab` (Duel) soft lock-on with mouse-wheel target cycling, decoupled mouse-flick with deadzone/decay/lock-on-press + `Settings.MoveDirControl` toggle in Settings, and deterministic 150ms input buffer in `BattlePlayer.cs`.
- **CR2 (Active Swings, Clashes, Feints, Footwork & Combos):** 3-phase `Windup → Active → Recover` (`UnitAction.Active`, `SwingProgress`, fixed 3-hit tracking with zero per-unit allocations), live-facing angular sweeps (`SweepActive`) supporting accels/drags, sequential cleave & cleave-stop, mirrored-swing **Blade Clashes & Chambers** (`TriggerClash`), early-windup **Feint** (RMB/Q) & **Morph**, forward swing lunge impulse, `Space` **Sidestep Dodge** (`0.22s`, `22` stamina, real spatial evasion), reworked posture-breaking **Kick / Shield Bash**, responsive combat footwork (removed `0.3x` mud crawl), and opposite-side combo cancels replacing punishing `Chain` block-rate inflation. Tested by `CombatReworkCR2`.
- **CR3 (Unified Directional Guard & Non-RNG Troop Combat):** Explicit `Unit.GuardDir` + tier/drill-scaled `UpdateGuardDir` replacing pure RNG melee blocking for AI troops; directional timed parries, shield stamina drain & `0.9s` **Guard Break** (`GuardBroken`) for both player and troops; mass-battle fast path (`> 18m` from player, non-duel) keeps `--bench 1500` at **7.40 ms/tick** mean (faster than CR0 baseline).
- **CR4 (Weapon Class Identities):** Sword & Shield fastest riposte (`0.42x`), 2H Greatsword wider cleave (`±1.28 rad`) + combo speed + overhead hyper-armor/poise, Axes/Maces `1.6x` guard-crush + `+0.15` pierce + `1.15x` whiff recovery, Polearms thrust tip sweet-spot (`+22%` at `>= 0.62*reach`, `-25%` crowded `< 0.42*reach`) + sweep spacing pushback, Bow draw-tension sweet spot (`0.78–0.96` charge) + fast sidearm swap (`0.18s`), Mounted wider side cuts & couched lance `1.6x` shield-shatter. Tested by `CombatReworkCR3CR4`.
- **CR5 (Smart Dueling & 1vX AI):** New `BattleDuelAI.cs` for `Options.Duel` and foot melee engaging near the player (`<= 14m`): preferred reach spacing (`0.85 * reach`), lateral strafing, stamina/whiff backstep, deterministic 1vX attack-slot pacing (`DuelSlot`) with flank/rear circling at `3.5m` and punish-on-turned-back, and tier-scaled guard reads, feints, shield-kicks, and ripostes. Tested by `DuelAISpacing`.
- **CR6 (3D Telegraphs, VFX & Reticle HUD):** Exaggerated directional windups, `Active` `SwingProgress`-driven sweeps & `EmitSlashArc`, visible directional `GuardDir` poses, and dedicated **Guard-Break**, **Blade Clash**, **Kick/Bash**, and **Sidestep Dodge** poses (`PuppetPoses.cs`, `Puppet3D.cs`); tiered hitstop (`35/55/65/70 ms`) and steel clash spark flashes (`BattleScreen.Effects.cs`); 4-chevron directional combat reticle with pulsing crimson threat chevron, gold riposte/chamber pulse, and melee stamina/posture bar (`Hud.Melee.cs`, `Hud.cs`).
