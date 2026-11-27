# Pillar 3: Mounted & Foot Combat Feel, Camera Framing & Live Progression

**Vision:** Capture the grounded, weighty third-person combat feel of *Chronicles: Medieval* (`02:53–03:07`, `03:34–04:17`, `04:28–04:35`, `08:14–08:16`) and `couldbecool/ironfield-styled-battle.jpg` — tight over-the-shoulder framing, dramatic upright-to-couched lance animations, uncluttered nearby-only enemy health bars, live in-battle skill-up toasts wired to our new `Skills.cs` system, and effortless mounting of loose horses on the field.

---

## Current State in Ironfield

- **Camera** (`BattleScreen.Controls.cs` `UpdateCamera()` lines 392–462):
  - `fieldDist = 3.8f`, `targetY = mounted ? 2.5f : 1.65f`, `targetShoulder = 0.68f`, `dist = baseDist + (mounted ? 1.4f : 0)` (= `5.2f` on horseback).
  - Critically damped height, dynamic FOV (`+5°` on gallop, `-4°` on charge), wall pull-in, and 360° delocked free-look on horseback (added in commit `58432420`).
- **Couched Lance** (`BattlePlayer.cs` lines 188–194, `Puppet3D.cs` lines 430, 846–875):
  - Player presses `Q` (`Act.Couch`) above speed `6` to toggle `p.Couched = true`.
  - `Puppet3D.cs` already has a `u.Couched` arm pose, but non-couched lances at rest/trot do not hold a tall, proud vertical/angled carry with a smooth lowering transition into the couch (or automatic lowering for AI cavalry charges).
- **Enemy Health Bars**:
  - Currently no clean contextual nearby-enemy health bar strip like *Chronicles* (`02:56–03:07`, `04:09–04:16`).
- **Skill Progression** (`Campaign/Skills.cs`, `Campaign/CampaignBattle.cs`):
  - Newly redesigned with Captain Level, XP, Skill Points, Focus Points, and battle skill training — currently applied *after* battle in `CampaignBattle.SkillsAfterBattle()`, so the player gets no real-time combat feedback when landing kills, parries, or lance hits.
- **Loose Horses** (`BattlePlayer.cs` `ToggleMount()` lines 243–275, `Battle.LooseHorses`):
  - Mechanics for dismounting and mounting the nearest `LooseHorse` via `E` (`Act.Interact`) already exist, but there is no clean on-screen proximity prompt (`Horse [E] Mount`).

---

## Target Design

### A. Grounded, Immediate Combat Camera
Matches `couldbecool/ironfield-styled-battle.jpg` and video `02:54–03:06`:
- Tighten default mounted camera slightly (`mountedBonus` `1.4f -> 0.85f`, `fieldDist` `3.8f -> 3.4f`, `targetY` `2.5f -> 2.35f`, slight right-shoulder offset `0.58f`) so your rider, horse head, and weapon swing fill the frame with real presence.
- Subtle speed-reactive pullback (`+0.0f` at walk/trot up to `+0.65f` at full gallop/charge) so high-speed charges feel expansive without losing close-quarters intimacy.
- Keep `F4` (`Act.CameraView`) cycling `OTS / Close Duel / First Person`.

### B. Upright → Couched Lance Stance & Smooth Blend
Matches video `03:34–04:06`, `05:36–05:39`:
- **At idle / walk / trot**: Lance riders (both player and AI cavalry!) hold their lances **tall and upright** (angled slightly forward ~`10°`), creating the iconic forest-of-lances silhouette from *Chronicles*.
- **Smooth Couch Blend** (`Unit.CouchBlend` `0..1`, interpolated in render/sim):
  - When `u.Couched` activates (player presses `Q` at gallop, or AI cavalry enters charge range within `~18 m` of impact), the lance smoothly sweeps down from vertical to horizontal couched position over `~0.28 s`, then lifts back upright after impact or slowdown.
- **Auto-couch option / UX**: Also allow optional auto-couch at gallop (or clear subtle bottom-right lance-ready indicator when `Speed > 6`).

### C. Contextual Nearby-Only Enemy Health Bars
Matches video `02:56–03:07`, `04:09–04:16`:
- In Field View, project slim, crisp dark-bordered **crimson HP bars** *only* above enemies that:
  1. Are within `~10 m` of the player **and**
  2. Have taken damage (`Hp < MaxHp`) **or** are within `~4.5 m` directly in front of the player's aim cone.
- Cap to the closest `6` enemies so thick melees stay readable and never turn into floating-bar soup.

### D. Live In-Battle Skill-Up & Level-Up Toasts
Matches video `04:33–04:34` (`★ Two-Handed: 1 / Skill Leveled Up`):
- Track live skill XP increments during a fought battle (headless counters / callbacks on `Battle` for `Arms`, `Bow`, `Riding`, `Tactics`, `Leadership`) using the captain's starting skill values passed in `BattleOptions`:
  - Landing melee kills / hits → `Arms`
  - Mounted kills / lance hits → `Riding` + `Arms`
  - Bow kills / hits → `Bow`
  - Flanking/routing enemy squads → `Tactics`
  - Rallying / inspiring nearby men → `Leadership`
- Whenever a skill crosses an integer threshold during the fight, push a lightweight `BattleToast` event rendered as a sleek **forged-brass & iron toast** on the mid-right edge (`★ ARMS 14 — Skill Increased`) that fades after `3.5 s` without interrupting combat.
- At battle end, reconcile cleanly with `CampaignBattle.SkillsAfterBattle()` so no XP is double-counted.

### E. Contextual World Interaction Prompts
Matches video `08:14–08:15` (`Horse [E] Mount`):
- When the player is on foot within mount reach of a `LooseHorse`, or near a usable siege engine (`EngineNear`), project a small clean prompt near bottom-center: `[E] Mount Horse` (with horse HP bar) or `[E] Man Engine`.

---

## Implementation Phases

### Phase P3.1 — Camera Framing & Speed-Reactive Pullback
- `BattleScreen.cs` & `BattleScreen.Controls.cs`:
  - Tune `fieldDist`, mounted offset, and add smoothly damped `speedPullback` in `UpdateCamera()` driven by `MathF.Abs(battle.Player.Speed)`.

### Phase P3.2 — Upright Lance Carry & Smooth Couch Transition
- `Units.cs` / `BattleAI.cs` / `BattleCombat.cs`:
  - Ensure AI lance cavalry sets `u.Couched = true` smoothly when charging at speed within `~20 m` of targets and clears it when slowed/stalled.
- `Puppet3D.cs`:
  - Add per-unit smoothed visual couch blend (or derive from speed/couched state) so lances stand tall and vertical at rest/trot and sweep cleanly down into the horizontal couch pose at charge.

### Phase P3.3 — Nearby Enemy HP Bars & Mount Prompt
- `Hud.Fight.cs`:
  - Add `DrawNearbyEnemyBars()` (strict distance + damage/cone filter, capped at 6).
  - Add `DrawInteractPrompt()` checking nearest `LooseHorse` within `ToggleMount` range (`~2.8 m`) or `Battle.EngineNear`.

### Phase P3.4 — Live In-Battle Skill-Up Toasts
- `BattleTypes.cs` / `Battle.cs` / `BattleCombat.cs`:
  - Pass captain's starting skill levels + focus multipliers into `BattleOptions` (optional, null in quick battles).
  - Emit deterministic `SkillToast(string Skill, int NewLevel, float Time)` records into `Battle.SkillToasts` on integer level crossings during combat.
- `Hud.Fight.cs`:
  - Render mid-right brass-accented toast card (`★ {SKILL} {LEVEL}` / `Skill Increased`) with smooth slide-in/fade-out.
- `Campaign/CampaignBattle.cs`:
  - Keep `SkillsAfterBattle()` as the authoritative campaign application (matching the same formula so in-battle toasts match post-battle report).

---

## Verification & Acceptance Criteria
- `tools/check.sh` ends with `ALL CHECKS PASSED`.
- Headless self-tests in `Tests/BattleTests.cs`:
  - AI lance cavalry couches on charge and uncrouches when stalled.
  - `SkillToasts` fire deterministically on combat kills/hits when captain skills are supplied, and `SelfTest.ReplayExact` stays green.
- Visual verification (`SDL_AUDIODRIVER=dummy ALSOFT_DRIVERS=null`, High quality):
  - Capture `IRONFIELD_SHOTS` (`IRONFIELD_SHOTS_FROM=clash`) and verify:
    - Closer mounted camera framing matches `couldbecool/ironfield-styled-battle.jpg`.
    - Upright lances at trot vs. couched lances in charge.
    - Nearby enemy HP bars appear cleanly in melee without clutter.
    - Loose-horse `[E] Mount` prompt and skill toast render with zero overflow.

---

## As built (2026-11-27)

- **P3.1:** Tightened default mounted OTS camera in `BattleScreen.cs` / `BattleScreen.Controls.cs` (`fieldDist` `3.8f -> 3.4f`, mounted focus height `2.5f -> 2.35f`, right-shoulder offset `0.68f -> 0.58f`, `mountedBonus` `1.4f -> 0.85f`) and added smoothly damped `camSpeedPull` (`0..+0.65f` driven by horse speed above walk/trot). `F4` (`Act.CameraView`) continues to cycle OTS / Close Duel / First Person.
- **P3.2:** Added `Unit.CouchBlend` (`0..1`) updated deterministically each tick in `Battle.Update` (`~0.28s` down, `~0.32s` back up). Updated `BattleAI.ThinkCav` so AI lance cavalry (`CanCouch`) couches within `20 m` at gallop and cleanly uncrouches when stalled (`< 4.5 m/s`) or out of range. Updated `Puppet3D.cs` so mounted lancers carry their lances tall and upright (`~10°` forward) at rest/trot and sweep smoothly down into the horizontal couched tuck as `CouchBlend` rises.
- **P3.3:** Added `Hud.DrawNearbyEnemyBars()` in `Hud.Fight.cs` (slim dark-bordered crimson HP bars above enemies within `10 m` that are wounded or within `4.5 m` in the player's aim cone, capped at the closest 6). Added `Hud.DrawInteractPrompt()` with Forged Iron & Brass 9-slice styling for loose horses (`[E] MOUNT HORSE` / `SEIZE HORSE` + horse HP bar), siege engines (`[E] MAN ...`), couching, dismounting, and look-orders, removing the old duplicate prompt block from `Hud.Crosshair`.
- **P3.4:** Added `SkillToast` (`BattleTypes.cs`), `BattleOptions.TrackSkills` / `SkillMult` (wired automatically in `Rules.WithCaptain`), and deterministic `Battle.AwardBattleSkill` / `Battle.SkillToasts` triggered by melee/mounted/bow kills, lance/arrow hits, parries/ripostes, rallying allies (`Leadership`), and routing enemy squads (`Tactics`). `Campaign.SkillsAfterBattle()` remains authoritative at battle end (no double-counting). `Hud.DrawSkillToasts()` renders mid-right Forged Iron & Brass toasts (`★ {SKILL} {LEVEL} / SKILL INCREASED`) with slide-in and 3.5 s fade-out.
- **Tests & Shots:** `Pillar3CombatFeel` in `Tests/BattleTests.cs` (registered in `SelfTest.cs`), `MovementMountedAndRanged`, and `ReplayExact`. Staged shots via `IRONFIELD_SHOTS_FROM=pillar3` (`p3-mounted-lances.png`, `p3-couched-charge.png`, `p3-melee-bars-toast.png`, `p3-mount-prompt.png`) with empty `overflow.txt`.
