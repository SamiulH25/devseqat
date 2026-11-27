# Pillar 2: Pre-Battle "Battle Planning" Phase

**Vision:** Turn Ironfield's pre-battle deployment into a clear, tactile *Chronicles: Medieval*-style **Battle Planning** phase (`01:42–01:53`, `05:08–05:35`): a high-angle free-cam over the field, a glowing physical deployment line painted onto the terrain, drag-and-rotate formation placement using the same left-edge roster from Pillar 1, a prominent top-center **Start Battle** header, and a smooth cinematic drop-in onto your mounted captain when battle begins.

---

## Current State in Ironfield

- **Deployment flow** (`BattleScreen.cs` `StartDeployment()` line 232, `BattleDeploy.cs`, `BattleScreen.Controls.cs`):
  - `StartDeployment()` enters `SetCommand(true)` with `deploying = true`.
  - `BattleDeploy.cs` handles `DeploySpot`, `DeploymentOf()`, `ApplyDeployment()` and remembers `CampaignState.LastDeploy`.
  - `BattleScreen.Controls.cs` already supports box-select, right-click drag to set line/facing, and `DeployAsLast` toggle.
  - `BattleScreen.Controls.cs` `UpdateCamera()` (line 429) has an `intro` sweep and `blend` smoothstep between `cmdFocus` (`command`) and `fieldFocus` (`fight`).
- **Missing today:**
  - No physical glowing deployment boundary line rendered on the terrain showing exact legal deployment bounds.
  - Deployment UI header/controls feel utilitarian compared to the clean `Battle Planning` + `[Start Battle (Enter)]` header and bottom control legend (`Move (Drag)`, `Q/E Rotate`, `Zoom`, `Space/Ctrl Move Vertical`).
  - Transitioning from deployment to battle snaps/blends generically instead of an intentional, dramatic camera drop-in onto the captain's saddle.

---

## Target Design

1. **Physical Terrain Deployment Line**:
   - A glowing translucent **steel-blue ribbon/trench line** projected directly onto the terrain mesh along the player's forward deployment boundary (with subtle animated shimmer/scroll and gentle fade at the field edges).
   - Clamps dragged squad placements smoothly to the legal side of the line and pulses softly if the player drags past it.
2. **Clean "Battle Planning" Header & Bottom Control Legend**:
   - Top center: forged-brass-accented `BATTLE PLANNING` title + clickable `START BATTLE [ENTER]` button (plus compact `Last Layout` / `Default` toggle pill).
   - Left edge: reuses Pillar 1's **vertical company roster** (click to focus/select, see unit counts & formations at a glance).
   - Bottom left: selected company card with **Formation** & **Stance** toggles.
   - Bottom center: compact keybind strip (`LMB Select`, `RMB Drag Place & Face`, `Q/E Rotate`, `WASD Pan`, `Scroll Zoom`).
   - Enemy companies visible on the far ridge (within initial scouting range) show crisp crimson world-space badges so you can plan counters before the horn blows.
3. **Drag-and-Rotate Ergonomics**:
   - Allow **LMB drag** on a selected company's ground banner (or **RMB drag** anywhere) to reposition smoothly with live translucent white formation footprints (`PreviewSlots`).
   - `Q` / `E` (or mouse-drag angle) rotates the selected company's facing in place.
4. **Cinematic Camera Drop-In on Start Battle**:
   - Pressing `Enter` or clicking `Start Battle`:
     - Sounds the battle horn (`Cue.Order` / warhorn).
     - Fades out the terrain deployment line over `0.45 s`.
     - Smoothly eases the camera over `~1.1 s` from the high-angle planning vantage straight down behind the captain's shoulder/saddle (`SetCommand(false)` with a dedicated drop-in curve in `UpdateCamera()`), giving full control as the lines begin to advance.

---

## Implementation Phases

### Phase P2.1 — Terrain-Projected Deployment Boundary Line
- `BattleDeploy.cs`: Expose the canonical deployment boundary predicate/line (`DeployBounds()`: min/max X, forward Z/Y limit, and clamp helper `ClampDeployPos(Vector2)`) so headless rules and 3D visuals share one source of truth.
- `BattleScreen.Draw.cs`:
  - Add `DrawDeployBoundary()` during `deploying` (and fading out during `deployFade > 0`): builds a terrain-hugging ribbon strip sampled every `2 m` via `battle.Terrain.Height(p) + 0.12f`, drawn with a clean additive/alpha blue glow and edge dashes.

### Phase P2.2 — Battle Planning HUD & Controls
- `Hud.cs` / `Hud.Deck.cs` (or dedicated `Hud.Planning.cs`):
  - Draw the top-center `BATTLE PLANNING` header + `START BATTLE [ENTER]` forged-iron button + compact `Last Deploy` toggle.
  - Reuse Pillar 1's left-edge roster and bottom-left selected card during `Deploying`.
  - Draw the clean bottom-center planning keybind bar.
- `BattleScreen.Controls.cs`:
  - Wire `Enter` to start battle while `deploying`.
  - Add `Q`/`E` in-place rotation of selected squad(s) during `deploying` (updating `Squad.Facing` and re-calling `SetLine`).
  - Clamp dragged deployment positions against `BattleDeploy.ClampDeployPos` with subtle visual feedback.

### Phase P2.3 — Smooth Drop-In Transition
- `BattleScreen.cs` & `BattleScreen.Controls.cs`:
  - Add `deployDrop` timer (`1.1f` s) triggered when exiting deployment into fight view.
  - In `UpdateCamera()`, ease `blend` with a custom ease-in-out curve that swoops down behind `battle.Player` and aligns `yaw` smoothly to `battle.Player.Heading` so the player faces the enemy line on arrival.

---

## Verification & Acceptance Criteria
- `tools/check.sh` ends with `ALL CHECKS PASSED`.
- Headless self-test in `Tests/BattleTests.cs`:
  - `DeployBounds` clamps out-of-zone positions consistently and `DeploymentMemory` still passes.
- Visual verification (`SDL_AUDIODRIVER=dummy ALSOFT_DRIVERS=null`, High quality):
  - Capture `IRONFIELD_SHOTS` deployment frame (`IRONFIELD_SHOTS_FROM=sergeants` or deployment stage) and verify:
    - Glowing terrain-hugging deployment line clearly visible across hills.
    - Clean `BATTLE PLANNING` header + `START BATTLE` button, zero text overflow in `overflow.txt`.
    - Left roster + world badges + formation footprints readable at 1280×720.
