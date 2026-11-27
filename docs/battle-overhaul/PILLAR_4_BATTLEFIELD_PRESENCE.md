# Pillar 4: Battlefield Presence, Formation Cohesion & Ride-Down Victory

**Vision:** Elevate the raw medieval spectacle and battlefield readability of *Chronicles: Medieval* (`00:03`, `01:02–01:07`, `02:45–02:48`, `03:24–03:33`, `04:56–05:07`, `07:17–07:23`) inside Ironfield's crisp ink-outlined 3D style — prominent waving company standards you can read from across the valley, rich cavalry hoof dust plumes, unmistakable braced-spear lean against charging horse, and a clean `Victory` banner that lets you ride down routing foes before leaving the field.

---

## Current State in Ironfield

- **3D Banners** (`BattleScreen.Draw.cs` lines 472–491, `Puppet3D.cs` `Banner()` line 1284, `BattleScreen.Effects.cs` `BannerFalls()`):
  - Each company already has a 3D banner bearer and falling-banner physics when the bearer dies.
  - Currently banners are relatively modest in height/cloth area, making them easy to lose in thick melee when HUD badges are minimal.
- **Dust & Ground Effects** (`BattleScreen.Draw.cs` `GroundSteps()` lines 369–416, `BattleScreen.Effects.cs` lines 110–135):
  - Emits trample marks and occasional dust puffs on charges and ground steps, but galloping cavalry formations don't kick up sustained, cohesive dust wakes across grass/dirt.
- **Braced Spears** (`BattleCombat.cs` `Braces()`, `Puppet3D.cs`):
  - Headless sim already tracks braced spearmen and deals heavy anti-cavalry damage, but visually braced ranks look almost identical to idle standing ranks.
- **Victory & End-of-Battle Flow** (`Battle.cs` `CheckEnd()` lines 643–655, `BattleScreen.cs` `ending` timer, `Hud.Overlays.cs` `Result()`):
  - Currently when `Winner >= 0` is set, `ending` zooms the camera up/back (`BattleScreen.Controls.cs` line 436) and opens the full `Result()` modal shortly after, cutting off the satisfying *Chronicles* moment of riding down routing enemies (`04:56–05:07`) under a clean `VICTORY` banner.

---

## Target Design

### A. Prominent Waving Heraldic Standards
Matches video `00:03`, `01:02–01:07`, `07:17–07:23`:
- Increase 3D company banner pole height (by ~`35%`) and cloth size (tall swallowtail/rectangular heraldic standards) with multi-segment sinusoidal wind wave driven by battle wind + bearer velocity (`stream`).
- Add a second smaller **pennon** on large companies (`> 50` men) and on the captain's personal retinue so formation flanks and depth read naturally without relying on UI.
- Keep `BannerFalls()` intact so seeing an enemy standard topple mid-clash is an unmistakable morale cue.

### B. Cavalry Hoof Dust Plumes & Charge Impact
Matches video `03:24–03:33`, `04:45–04:53`:
- When mounted units move above trot speed (`Speed > 5.5f`) on dry ground (grass, dirt, road, stony — not mud/water), emit warm, translucent, ground-hugging **dust puffs** behind hooves (pooled particle budget so 300+ cavalry stay >180 fps).
- On a cavalry-into-infantry impact (`BattleMove.Impact` / `BattleCombat.Lances`), burst a wider dust + grass-clod plume along the contact edge.

### C. Unmistakable Braced-Spear Posture
Matches video `02:45–02:48`, `07:42–07:45`:
- When an infantryman with a spear/polearm is braced (holding/shield-walling while enemy cavalry is approaching or active in `Braces`), render a distinct **low braced stance** in `Puppet3D.cs`:
  - Forward torso lean, front knee bent, shield angled tight, spear butt grounded with tip angled forward at `~30°` chest-height of a horse.
- Makes anti-cavalry counters instantly readable from horseback before you accidentally impale your own mount.

### D. Clean "Victory" Banner & Optional Pursuit / Ride-Down
Matches video `04:56–05:07`, `08:35–08:46`:
- When `Battle.Winner == 0` (Victory):
  - Show a clean, atmospheric top-center **`VICTORY`** banner (brass-accented serif/Pixelify Sans with subtle fade-in, no giant blocking modal yet!) and a quiet bottom-center prompt: `[Tab] / [Enter] Leave Field — or ride down the routing foe`.
  - Keep the player in **full horseback control** (do NOT force-lock camera zoom-out while pursuing!) so you can chase down fleeing troops for extra prisoners/XP/loot, and press `Tab` / `Enter` whenever you're ready to open the `BattleReport` / `Result()` summary.
  - On Defeat (`Winner == 1`) or when no routing enemies remain on the field, transition smoothly into the `Result()` summary as before (or after a short beat).

---

## Implementation Phases

### Phase P4.1 — Taller Waving Standards & Pennons
- `Puppet3D.cs` (`Banner()` line 1284):
  - Scale standard pole height & cloth dimensions; add richer multi-segment wind ripple and crisp heraldic charge contrast.
- `BattleScreen.Draw.cs` (lines 472–491):
  - Emit a secondary flank pennon for large companies (`s.Alive >= 50`) and ensure standards cast clean shadows without hurting instanced draw batching.

### Phase P4.2 — Pooled Cavalry Hoof Dust Plumes
- `BattleScreen.Effects.cs` & `BattleScreen.Draw.cs` (`GroundSteps`):
  - Add a fixed-size ring buffer of soft billboarded dust puffs emitted behind galloping horses (`u.Mounted && u.Vel.LengthSquared() > 30f`), throttled by camera distance and total active puffs so GPU cost stays negligible (`< 0.2 ms`).

### Phase P4.3 — Braced-Spear 3D Posture
- `Units.cs` / `BattleCombat.cs`:
  - Expose lightweight `Unit.Braced` bool set during `Braces()` / spear-hold vs. nearby cavalry (headless, deterministic).
- `Puppet3D.cs`:
  - Add `u.Braced` pose branch: forward torso lean, grounded spear butt, `~30°` forward-angled tip, tightened shield guard.

### Phase P4.4 — Ride-Down Victory State
- `BattleScreen.cs` & `BattleScreen.Controls.cs`:
  - Add `pursuing` state when `battle.Winner == 0` and routing enemies remain alive on the field; suppress forced camera pull-back in `UpdateCamera()` while `pursuing`.
  - Pressing `Enter` or `Tab` (or when all fleeing enemies exit the map / are caught) ends pursuit and opens `ShowResult = true`.
- `Hud.Overlays.cs`:
  - Render the clean non-blocking `VICTORY` banner + `[Enter] Finish Battle` prompt while `pursuing && !ShowResult`.

---

## Verification & Acceptance Criteria
- `tools/check.sh` ends with `ALL CHECKS PASSED`.
- Headless self-tests in `Tests/BattleTests.cs`:
  - `Unit.Braced` sets properly on spear infantry facing charging cavalry and clears when moving/routing.
  - Routing enemies caught after `Winner == 0` count cleanly in `Battle.Result()` without breaking `SelfTest.ReplayExact`.
- Visual & performance verification (`SDL_AUDIODRIVER=dummy ALSOFT_DRIVERS=null`, High quality):
  - Capture `IRONFIELD_SHOTS` (`IRONFIELD_SHOTS_FROM=clash`) and verify:
    - Taller heraldic standards clearly readable across the valley.
    - Visible cavalry dust plumes behind charging horse without frame drops (`--gpu-report` stays well above 150 fps on GTX 1080 Ti).
    - Braced spearmen visibly leaning with forward-angled spears.
    - Clean `VICTORY` banner with unobstructed pursuit view and zero text overflow in `overflow.txt`.
