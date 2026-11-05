# Ironfield — battle prototype

A 2.5D (2D sprites in a 3D world) medieval battle game in C# + MonoGame (DesktopGL), inspired by the
"lead your army in person" genre. This is an original setting: no names, assets or factions from other games.

This prototype covers **combat only**: how the player fights as a soldier while commanding as a general.

## Build / run / test

```sh
dotnet build
dotnet run                 # play
dotnet run -- --selftest   # headless: formation math + 3 full battles must resolve
```

You need .NET SDK 10 (`net10.0`). On Ubuntu 24.04 `apt-get install dotnet-sdk-10.0` works (no Microsoft feed needed).
The game needs no art files: sprites, terrain and the font are all generated in code. Troops come from `Data/troops.json`.
**Design language:** see [`docs/DESIGN.md`](docs/DESIGN.md) (art direction, unit painting rules, heraldry, tiers, UI tokens).
To inspect the sprite atlas, run `IRONFIELD_DUMP_ATLAS=/path/atlas.png dotnet run`.
To render a few staged camera shots as PNGs and quit (works headless with `xvfb-run -a dotnet run`), set `IRONFIELD_SHOTS=/some/dir`.
`dotnet run -- --write-controls` writes `controls.json` with the default bindings.

## Controls

Press **F1** in game for the full list. Every key below is a default: rebind in `controls.json`
(`dotnet run -- --write-controls` writes a template with every action, plus `MouseSensitivity` and `InvertMouseY`).
Keys are MonoGame `Keys` names (`W`, `LeftShift`, `F1`...) or `LMB`/`RMB`/`MMB`/`MB4`/`MB5`, `SHIFT`, `CTRL`, `ALT`, `ESC`, digits.

**Field view** (you fight; third-person camera, mouse wheel zooms it). You start **on horseback**.
- Mounted: W/S rein (speed up / slow, back up), A/D turn the horse, Shift gallop, Q couch the lance (needs speed), E dismount (when nearly still).
  The mouse looks around freely: you strike where you look, not where the horse points.
- On foot: WASD move, Shift sprint, E mount a loose horse within reach (yours, a fallen rider's, or the enemy's). Prompts appear on screen.
- **Attack direction** comes from the way the mouse last moved: up = overhead, down = thrust, left/right = side cuts.
  The gold chevron at the crosshair shows it. LMB hold+release attacks (longer hold hits harder).
- RMB block (front only). A shield stops any direction at a stamina cost; blocking **in the direction the blow comes from**
  (red chevron) is a **parry**: free, and it staggers the attacker. RMB while winding up cancels the swing (feint).
- R rally. **Hold MMB (or Z) for the order wheel**: move the mouse toward an order, release to give it.

**Command view** (Tab; time x0.25, Space pauses):
- LMB select a squad, drag a box to select several (Shift adds). Click a unit card to select it, double-click to jump the camera to it.
- RMB click moves (keeps the current width), RMB drag draws the formation line (it faces the enemy) with a preview of every man's spot,
  RMB on an enemy squad attacks it (red line; hovered enemies are ringed).
- WASD or the screen edges pan, Q/E or middle-drag orbit (middle-drag also tilts), wheel zoom, click the minimap to jump there.
- The order buttons (bottom right) give the same orders as the hotkeys.

**Orders (both views):** 1-9 toggle a squad, 0 or ~ selects all (with nothing selected, orders go to every squad).
C charge, F follow me, G form a line on me, H hold position, T advance 15 m, B fall back 20 m, V loose/tight spacing,
X archers fire at will / hold fire. **Esc** opens the menu (resume, controls, new battle, quit); Enter starts a new battle after the result.

## Design: soldier vs general

The core tension: **you are strongest as a leader when you are in the fight, but you can only control the fight from above.**

- **Leadership aura:** your troops within 12 m gain morale (routing ones recover faster). The aura is OFF in command view.
- **Command view** slows time to x0.25 and gives RTS-style control, but your body stands idle and vulnerable.
- **Rally (R):** costs 30 stamina, 12 s cooldown. It gives +25 morale to allies within 15 m (+45 to routing ones, which usually brings them back).
- **Heroics:** your kills give nearby allies more morale than ordinary kills.
- **Leaders matter:** if you or the enemy captain dies, every unit on that side loses 25 morale. The enemy captain (red ring in command view) stays just behind his line, so killing him is a real tactical goal.
- **If you fall,** the battle continues and you command from afar at normal speed, without an aura.
- **Morale and routing:** below 15 morale a unit routs and flees off the map (radius 95). It rallies again above 45. A side loses when it has no active, non-routing units.
- **Melee:** windup, strike, recover. Hits interrupt windups. AI units block hits from the front by chance (Shieldmen 55%). A player block staggers the attacker; if stamina runs out, the guard breaks. Player swings cleave up to 3 targets.
- **Archers** shoot ballistic arrows that lead the target. They fire only when settled in formation, or on Charge. Loose spacing (V) reduces arrow hits, because hits are physical and not rolled.

## Design: cavalry

Cavalry comes in three tiers: **Outriders** (coursers), **Lancers** (warhorses) and **Knights** (caparisoned destriers);
the player rides a courser.
- **Momentum:** a horse has a heading and a speed. It accelerates, brakes and turns at limited rates (turning gets wider as it speeds up),
  so a charge needs room. AI riders steer toward a wished velocity (`Battle.Steer`); the player steers directly.
- **Speed bonus (Mount & Blade):** every melee blow gets `+6%` per m/s of closing speed above 2 m/s (shown under the crosshair as `+N% SPEED`).
- **Couched lance:** above 6 m/s a lance can be lowered (AI does it on a charge, the player presses Q). Anything that the point
  (1.0-2.7 m ahead of the horse) touches is hit for `14 + 6 x closing speed`, then the lance comes up. It drops again when the rider slows.
- **Trample:** a horse running into an enemy footman at over 4 m/s knocks him down (stagger) and hurts him; every body slows the horse,
  and tight shield walls slow it more. The shock costs nearby friends morale.
- **Bracing (Total War):** spearmen, and shieldmen holding a tight formation, stand still and set their points when horses come.
  A braced spear reaches further than a couched lance and hits the horse hard (`7 x` closing speed for spears), stopping the charge.
  Spearrunners also deal x1.8 damage to anything mounted. Arrows and spears often hit the horse instead of the rider.
- **Horses die:** a slain horse throws its rider (stagger, damage). Riders whose horse dies fight on foot. A killed rider leaves a loose horse.
- **Cycle charge (AI):** Charge → ride through the target → Withdraw (ride out ~3 s) → turn → Charge again. A rider that bogs down
  in a crowd (slow, near the target for 1.2 s) fights in place for 4 s and then pulls out. Cavalry on Hold counter-charge enemies within 15 m.
- **Enemy horse:** waits wide on the far flank, then hunts the softest squad (archers first) when the lines meet or after 35 s.

## Design: troops, tiers and armies (campaign-ready)

- **Troops are data** (`Data/troops.json`, loaded by `Troops.cs`): stats, class, tier (I-IV), the troops each one
  **upgrades to**, campaign cost and upgrade XP, its horse, and its **look** (head, body, legs, feet, hands, shield, weapon,
  tabard, plume, cape). The self-test checks the tree: links exist, every upgrade raises the tier and is a better soldier,
  and every troop can be reached from Levies.
- **The tree:** Levies branch into Shieldmen -> Men-at-Arms -> Ironguard, Spearrunners -> Pikemen (reach 3 m, x2.3 vs horse),
  Bowmen -> Longbowmen (range 42 m), and Outriders -> Lancers -> Knights.
- **Armies:** a battle takes two `ArmySpec`s, which are lists of `Company(troop, count, role)`. This is what the campaign map
  will hand over. Companies deploy by role (infantry front, archers behind, spears left, horse right; several companies of a
  role stand side by side), and the enemy general commands any mix.
- Until the campaign exists, each **new battle** rolls an army level (1-4) per side, and each line lands one tier up or down at random,
  as if companies had been upgraded unevenly. Elite companies are smaller. Override with `IRONFIELD_LEVELS=4,2`.
- Better troops look better (see the tier table in `docs/DESIGN.md`), and unit cards show the tier numeral.

## Design: terrain and the 3D world

The world is 3D; only the units (and horses) are 2D sprite puppets.
- **Terrain** (`Terrain.cs`, part of the headless sim): a 512 m heightmap on a 2.5 m grid. Gentle value-noise rolls (calmer in the middle),
  3-5 random hills, a rim of high ground framing the valley, and a dirt road winding between the armies. It is seeded per battle.
  `Height()` interpolates on the same triangles the mesh draws, so units stand exactly on the ground you see.
- **It matters in the fight:** uphill is slower and downhill quicker (`Battle.SlopeFactor`, x0.55-1.15); striking from higher ground deals
  up to +15% (less from below; the hit text says HIGH GROUND); arrows fly from and land on the real ground and stick where they hit it.
- **Foliage** (placed by `Terrain`, built into meshes by `World.cs`): woods of broadleaf trees or pines, two of them flanking the field,
  lone trees, bushes along wood edges, rocks and boulders, and ~14,000 3D grass tufts with a few flowers. All low-poly, flat-shaded and lit by one sun.
  Tree trunks and boulders are **solid**: units are pushed out of them and a horse that rides into one pulls up hard.
  The deployment ground (`Terrain.Core`) is kept clear of anything solid.
- **Sky:** a gradient dome; fog fades the world into the horizon colour.

## Design: interface

- Bottom centre: **unit cards** (portrait made from the unit's own sprites, count, strength and morale bars, order tag, state above:
  WAVERING / ROUTING / FLANKED). Bottom left: you (horse and speed, health, stamina, horse health, rally, kills).
- Top: **balance of power** (strength-weighted), battle clock, event feed (top left), **minimap** (top right; enemy at the top, camera wedge).
- Field view: directional crosshair chevrons, damage text (speed bonus, flank, rear, high ground, parry), context prompts.
- Command view: squad **banners** with morale bars, order buttons, box select, formation preview, attack lines.
- Overlays: order wheel, F1 controls, Esc menu, and a result screen with both armies' losses.

## Design: Total War morale and flanks

- **Flanks and rear:** blows from the side deal x1.2, from behind x1.5, and only frontal blows can be blocked.
  Each flank/rear hit adds to the squad's `Pressure`, which drains morale until it decays (HUD: FLANKED).
- **Fear:** horsemen galloping straight at an unbraced footman drain his morale. Nearby routing friends drain it too (rout spreads),
  and so does an army that has lost over half its men.
- HUD unit cards show STEADY / FLANKED / WAVERING (morale under 30) / ROUTING.

## Architecture

| File | Role |
|---|---|
| `Battle.cs` | Headless simulation: units, AI, melee, arrows, morale, enemy general, win check. No graphics. |
| `Squad.cs` | Squad orders (Hold/Follow/Charge, optional attack `Target`), formation slots (anchor = centre of the front rank), flank `Pressure`. |
| `Units.cs` | `UnitType` (a troop from the data), `Look`, `Mount`, the `Unit` state (incl. heading/speed when mounted), loose and dead horses. |
| `Troops.cs` | Loads and validates `Data/troops.json` (troops, upgrade trees, mounts); `ArmySpec`/`Company`; `Armies` builds armies by level. |
| `Style.cs` | Design tokens: side heraldry, materials, horse coats, sprite light and density, UI colours, grid, type scale. |
| `Terrain.cs` | Headless heightmap (noise, hills, rim, road), foliage placement and solid obstacles. |
| `BattleGame.cs` | MonoGame `Game`: input, the blended third-person/command camera, terrain picking, orders, the render passes. |
| `World.cs` | 3D world renderer: lit terrain mesh, procedural low-poly foliage and grass, sky dome, minimap image. |
| `Hud.cs` | The whole 2D interface, its layout and hit-testing (cards, buttons, minimap clicks, menus). |
| `Controls.cs` | Rebindable actions, defaults, `controls.json` loading and key names for the HUD. |
| `Puppet.cs` | Draws each unit as a puppet of part sprites on a 3D skeleton, with continuous poses from its action state; horses too. |
| `Painter.cs` | The sprite painter: materials (hue-shifted 5-tone ramps + surface patterns), lit shapes, selective outline. |
| `SpriteSheet.cs` | Paints every unit and horse part for every look, view and side, and packs a 1024x1024 atlas (headless). |
| `Art.cs` | Uploads the atlas; blob shadow; white pixel. `PixelFont.cs`: the 5x7 HUD font. |
| `SelfTest.cs` | `--selftest` checks. |

Ground positions are `Vector2` (X = world X, Y = world Z).

### Puppet sprites (how units are drawn)

Each unit is about 15 painted pixel-art sprites placed on a 3D skeleton (unit space: +X right hand, +Y up, +Z facing, metres; 1 sprite px = 0.03 m),
dressed from its troop's `Look` (`torso.mail.f`, `helm.greathelm.s`, `shield.heater`, ...):
- **Billboards** (head, helm/hood, torso, hands, boots, plume, cape, quiver) always face the camera. View parts swap between `.f`/`.s`/`.b`
  sprites by the angle between the unit's facing and the camera; side sprites face right and are mirrored.
  Layered parts get a small toward-camera `bias` (the helm sits over the head) so they never z-fight.
- **Axial** parts (legs, sleeves, sword, spear, bow, arrow) stretch between two joints and turn about that axis to face the camera.
- **Planes** (shields) are fixed world quads, so a shield really swings between the front and the side.
- **Long** parts (horse body, neck, head) span two joints like axial parts but keep their top edge up, so the saddle never flips under the belly.
  Small billboard caps (chest, rump, face) fill in the horse when it is seen end-on.
- **Riders** sit `Puppet.Saddle` metres up. The horse is drawn along `Unit.Heading` and the rider along `Unit.Facing`, so a rider can look and strike sideways.
- **Swing poses** are keyed per `AttackDir`: each key pose is (arm angle, weapon pitch, swing-plane yaw), so side cuts sweep across the body.
- **Corpses** lay the parts flat on the ground.

Poses are continuous: `Puppet.PoseOf` blends the key poses (idle/raise/strike, as arm angle plus weapon pitch) by action progress,
and the walk swing follows the stride. `Unit.ViewFacing` eases toward `Facing`, so turning sweeps smoothly.
Everything is one draw call (`AlphaTestEffect`, so no sorting) plus blob shadows and lines for arrows and markers.
Horse coat parts are neutral grey (`/n`) and tinted per horse; saddle cloths and caparisons are drawn over them in the side's colours.
For hand-made art later, put PNGs into the atlas under the same keys (`part.variant.view/palette`); the puppet does not care who painted them.

## Decisions / gotchas

- **Camera history:** the first build was third person, then a fixed world angle; the user then chose **third person for combat plus a free
  command camera**, with smooth blending between the views. The current code is the third/free version.
- **Sprite history:** side-only frames came first, then 16-direction box models pre-rendered at startup. These were replaced at the user's request by
  **puppets made of per-part pixel-art sprites** (the current version).
- Mouse look warps the cursor to the window centre (`Mouse.SetPosition`). If a platform refuses warps, switch to SDL relative mouse mode.
- Battle sim stays free of MonoGame graphics, so it runs headless in tests. Keep it that way (terrain and foliage placement included).
- The game asks for the `HiDef` profile (big static meshes). Multisampling is off: asking for it made context creation fail on Mesa/Xvfb.
- In field view, friendly soldiers within 6 m of the camera are not drawn, so your own ranks never fill the lens.
- `Battle.Empty` uses `Terrain.Flat()` (no hills, no trees), so mechanic tests are not affected by the map.
- Team 0 = player side and squads 1-3 in `Squads` order. `Squad.Alive` is refreshed each tick by `UpdateSquad`.
- Known shortcuts (search `shortcut:`): O(n²) separation (fine up to about 400 units).
- Scaling a MonoGame `Color` by a float also scales its alpha: `Mat` forces opaque colours, so use `Color.Lerp` for tints.
- `AttackDir` is named from the attacker's side; the defender must block `Dirs.Mirror(swing)`. The HUD shows incoming blows already mirrored.
- Separation is mass-weighted (horse 6, man 1) with radii 1.0 / 0.375 m; horse-vs-footman overlaps are where trample `Impact` happens.
- `Battle.Empty(seed)` builds a scenario field (two leaders far apart) for the self-test; `AddSquad`, `Hit` and `HitHorse` are reachable from it.
- With an idle player, the AI enemy (bigger than yours at the same level) wins the self-test battles in about 30 s. The balance assumes the player fights and commands.

## Next steps

1. Play-test the feel: horse turn rates, lance damage, parry timing (right now any matched block parries; a timing window would add skill).
2. Horse archers and a wedge formation for cavalry; AI riders remounting loose horses.
3. A battle-start deployment phase; woods that hide units and slow formations; rivers and fords.
4. The campaign map layer: companies gain XP from `Squad` kills and upgrade along `troops.json`.
5. A spatial grid for separation and target search, for bigger battles.
