# Pillar 1: Tactical HUD & On-Horseback Command Mode

**Vision:** Deliver the crisp, low-clutter battle UI and seamless mounted command flow of *Chronicles: Medieval* (`01:36–02:45`, `03:08–03:15`, `03:43–03:49`, `04:18–04:28`) translated into Ironfield's Forged Iron & Aged Brass aesthetic and Pixelify Sans typography — matching `couldbecool/ironfield-tactical-remake.jpg` and `couldbecool/ironfield-styled-battle.jpg`.

**Core shift:** Instead of forcing the player to toggle into a separate high-altitude RTS screen (`Tab`) to issue tactical orders, **you can command your entire army directly from horseback** via an expandable `Command Mode` (hold/toggle `Left Ctrl`), while keeping the normal combat HUD clean and unobstructed. Full high-altitude overview (`Tab`) remains available for macro-level awareness and pre-battle planning.

---

## Current State in Ironfield

- **Fight View HUD** (`Hud.Fight.cs`):
  - `FightTop()` draws a thin tug-of-war balance bar at top-center with headcounts.
  - `Vitals()` draws bottom-left player shield + HP/stamina/horse bars.
  - `ArmChips()` draws bottom-right arm group chips (`1 FOOT`, `2 BOWS`, `3 HORSE`, `4 MAGES`).
  - `OrderWheel` (MMB hold) issues quick orders without leaving field view.
- **Command View HUD** (`Hud.Deck.cs`, `Hud.Command.cs`, `Hud.Divisions.cs`):
  - Full-width row of company cards along the top, captain plate + order deck grid along the bottom.
- **World-Space Flags** (`Hud.cs` `Banners()` / `Banner()`, lines 625–760):
  - Projects per-squad cloth flags on poles with overlap stepping and morale droop.
- **Alerts** (`Hud.Alerts.cs`, `BattleAlerts.cs`):
  - Top-left strip currently filtered to `breaking` in fight view and `breaking`/`arrows`/`leaderless` in command view.
- **Order Previews** (`BattleScreen.Orders.cs` `PreviewSlots()`, `LineAt()`, `LineEnds()`):
  - Already computes exact per-man ghost positions for a dragged line/formation.

---

## Target Design

### A. Default Combat HUD (Clean Fighting State)
Matches `couldbecool/ironfield-styled-battle.jpg`:
1. **Top-Center Heraldic Balance Bar**:
   - Friendly heraldic shield on the left, enemy heraldic shield on the right, live headcounts flanking a crisp forged-brass-framed tug bar.
2. **Bottom-Left / Bottom-Center Vitals**:
   - Minimal player health + stamina bars (plus curved/compact horse stamina/health bar when mounted). No heavy box chrome during combat.
3. **Top-Right Morale Event Pills** (replaces noisy banners):
   - Compact dark-iron pills with unit-type icon + single crisp word: `Inspired`, `Wavering`, `Broken`, `Routing`, `Obliterated`. Fade cleanly after ~5 s, max 6 stacked.
4. **Clean World-Space Squad Badges**:
   - Replace bulky cloth-on-pole 2D sprites with crisp medieval shield/diamond badges showing **unit-type icon + alive count** (e.g. `[spear] 60`). Dim/desaturate when routing; highlight brass when selected or hovered.
5. **Minimal Control Hint** (subtle, hideable in Settings):
   - Small `[Left Ctrl] Command Mode` / `[Left Alt] Info Mode` tag on the mid-left edge until the player is comfortable.

### B. Expandable On-Horseback Command Mode (`Left Ctrl` hold or toggle)
Matches `couldbecool/ironfield-tactical-remake.jpg`:
1. **Left-Edge Vertical Company Roster**:
   - Compact stacked rows for each commanded company:
     - Number badge (`1..9`), role icon (spear, sword, bow, horse, mage), alive count, mini formation pips (e.g. grid/wedge/line), and live state word (`Idle`, `Moving`, `Skirmish`, `Engaging`, `Holding`, `Routing`).
   - Clicking a row selects/deselects; `Shift+Click` multi-selects; `1..9` selects directly while in Command Mode.
2. **Bottom-Left Selected Company Card**:
   - Portrait/troop crest, company name (e.g. `HOUSEHOLD POLEAXES`), morale/stance/action strip (`Confident | Defensive | Moving`), and quick **Formation** (`Line / Wall / Square / Wedge / Loose`) + **Stance** (`Defensive / Neutral / Offensive`) icon toggles.
3. **Bottom-Center / Bottom-Right 5–6 Button Order Bar**:
   - Crisp square forged-iron buttons with icon + hotkey label:
     - `Engage` (`Q`), `Charge` (`E`), `Hold` (`R`), `Advance` (`T`), `Fallback` (`F`), plus `Fire at Will` when ranged is selected.
4. **3D In-World Banner Cursor & Ground Footprints**:
   - While in Command Mode on horseback, mouse cursor unlocks (or projects from center reticle / free cursor) onto the terrain:
     - **Hovering ground**: tall translucent **blue heraldic banner** planted at the cursor + **translucent white ground footprint discs/silhouettes** (via `BattleScreen.Orders.cs` `PreviewSlots`) showing exact unit spacing and facing!
     - **Hovering an enemy company**: banner snaps to the enemy centroid and turns **crimson red with crossed-swords icon** — clicking immediately issues `Engage` / `Charge` on that exact target!
     - **Right-click drag** on ground draws formation width & facing live on the terrain, just like command view, without leaving your horse.

### C. Info Mode (`Left Alt` hold)
- Temporarily shows full enemy squad names, tier/weapon labels, morale bars, and friendly order lines across the field, then hides cleanly on release.

---

## Implementation Phases

### Phase P1.1 — Controls & Command Mode State
- `Controls.cs`: Add `Act.FieldCommand` (default `LeftControl`) and `Act.InfoMode` (default `LeftAlt`); add setting for hold vs. toggle Command Mode in `Settings.cs`.
- `BattleScreen.Controls.cs`:
  - Track `fieldCommand` and `infoMode` booleans on `BattleScreen` and pass to `Hud`.
  - When `fieldCommand` is active in Field View: unlock mouse cursor (while keeping horse WASD steering alive!), allow left-click squad selection (from roster or world badge), right-click / right-drag ground move & enemy target orders via existing `BattleScreen.Orders.cs` (`LineAt`, `Issue`, `Move`).
  - Keep `Tab` (high-altitude Command Overview) working seamlessly and sharing the same left-edge roster + selected card + bottom order bar so both modes share one unified visual language.

### Phase P1.2 — Unified Tactical HUD Layout
- `Hud.Fight.cs` & `Hud.cs`:
  - Upgrade `FightTop()` with left/right heraldic shields (`kit.Shield`) and forged-brass frame around `kit.TugBar`.
  - Replace `Banners()` with crisp shield/diamond world-space badges (`icon + count + subtle morale pip`).
  - Build `DrawRosterLeft()` (vertical company list on left edge when `FieldCommand || Command || Deploying`).
  - Build `DrawSelectedCard()` (bottom-left selected company card with morale/stance/action + formation toggles).
  - Build `DrawCompactOrderBar()` (bottom-center/right 5–6 hotkey icon buttons using `UiChrome.Plate` + `Pixelify Sans`).
- `Hud.Alerts.cs`:
  - Restyle top-right morale/state event pills (`Inspired`, `Wavering`, `Broken`, `Routing`, `Obliterated`) fed by `BattleAlerts.cs` / `BattleMorale.cs` state transitions.

### Phase P1.3 — 3D In-World Banner Cursor & Formation Footprints
- `BattleScreen.Draw.cs` & `BattleScreen.Orders.cs`:
  - Render a 3D translucent ground-projected **Command Banner** (blue on ground, crimson with crossed swords on enemy hover) when `fieldCommand` or `command` is active.
  - Render crisp translucent **white ground footprint markers** for every slot returned by `PreviewSlots()` when hovering/dragging a move order or hovering a selected squad's destination.

---

## Verification & Acceptance Criteria
- `tools/check.sh` ends with `ALL CHECKS PASSED`.
- Add headless self-test in `Tests/BattleTests.cs` verifying:
  - Morale state transition alerts (`Wavering`, `Broken`, `Routing`, `Inspired`) fire deterministically without duplicates.
  - Orders issued via `FieldCommand` path produce identical replay frames (`SelfTest.ReplayExact` passes).
- Visual verification (with `SDL_AUDIODRIVER=dummy ALSOFT_DRIVERS=null`, High quality only):
  - Capture `IRONFIELD_SHOTS` (`IRONFIELD_SHOTS_FROM=hud`) showing:
    1. Clean combat HUD (default fight view).
    2. Horseback `Command Mode` open with left roster, selected card, bottom order bar, and 3D banner cursor + ground footprints.
    3. Top-right morale pills during a clash.
  - Zero entries in `overflow.txt`.
