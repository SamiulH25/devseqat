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
The game needs no content files: sprites, terrain and the font are all generated in code (`Art.cs`).
To inspect the sprite atlas, run `IRONFIELD_DUMP_ATLAS=/path/atlas.png dotnet run`.
To render a few staged camera shots as PNGs and quit (works headless with `xvfb-run -a dotnet run`), set `IRONFIELD_SHOTS=/some/dir`.

## Controls

**Field view** (you fight; third-person camera over the right shoulder). You start **on horseback**.
- Mounted: W/S rein (speed up / slow, back up), A/D turn the horse, Shift gallop, Q couch the lance (needs speed), H dismount (when nearly still).
  The mouse looks around freely: you strike where you look, not where the horse points.
- On foot: WASD move, Shift sprint, H mount a loose horse within reach (yours, a fallen rider's, or the enemy's).
- **Attack direction** comes from the way the mouse last moved: up = overhead, down = thrust, left/right = side cuts.
  Gold marks around the crosshair show it. LMB hold+release attacks (longer hold hits harder).
- RMB block (front only). A shield stops any direction at a stamina cost; blocking **in the direction the blow comes from**
  (red mark at the crosshair) is a **parry**: free, and it staggers the attacker. RMB while winding up cancels the swing (feint).
- R rally, Tab command view.

**Command view** (Tab):
LMB select a squad (Shift adds), RMB click moves (keeps the current width), RMB drag draws the formation line (it faces the enemy),
RMB click **on an enemy squad** attacks it (red line; hovered enemies are ringed),
WASD pan, Q/E or middle-drag orbit (middle-drag also tilts), wheel zoom, G hold in place.
Tab eases the camera between the two views over 0.45 s (focus, yaw, pitch, distance and FOV are all blended).

**Both views:** 1-4 toggle a squad, 0 selects all (with nothing selected, orders go to every squad),
F follow me, C charge, V loose/tight spacing, G form up ahead of you (field view). Enter starts a new battle after the result. Esc quits.

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

Each side has 10 **Lancers** (`UnitType.Lancer`, on a `Mount.Warhorse`); the player rides a `Mount.Courser`.
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
| `Units.cs` | `UnitType` stat table and `Mount` horses (in code for now), the `Unit` state (incl. heading/speed when mounted), `LooseHorse`. |
| `BattleGame.cs` | MonoGame `Game`: input, the blended third-person/command camera, picking, the render passes, HUD. |
| `Puppet.cs` | Draws each unit as a puppet of part sprites on a 3D skeleton, with continuous poses from its action state; horses too. |
| `Art.cs` | Part sprites as ASCII pixel art (built per palette into a 512x256 atlas), props, grass, shadow, and the 5x7 `PixelFont`. |
| `SelfTest.cs` | `--selftest` checks. |

Ground positions are `Vector2` (X = world X, Y = world Z).

### Puppet sprites (how units are drawn)

Each unit is about 15 small pixel-art sprites placed on a 3D skeleton (unit space: +X right hand, +Y up, +Z facing, metres; 1 sprite px = 0.075 m):
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
For real art, replace the ASCII strings in `Art.Sprites` with PNGs that keep the same names (`name.view` + palette).

## Decisions / gotchas

- **Camera history:** the first build was third person, then a fixed world angle; the user then chose **third person for combat plus a free
  command camera**, with smooth blending between the views. The current code is the third/free version.
- **Sprite history:** side-only frames came first, then 16-direction box models pre-rendered at startup. These were replaced at the user's request by
  **puppets made of per-part pixel-art sprites** (the current version).
- Mouse look warps the cursor to the window centre (`Mouse.SetPosition`). If a platform refuses warps, switch to SDL relative mouse mode.
- Battle sim stays free of MonoGame graphics, so it runs headless in tests. Keep it that way.
- Team 0 = player side and squads 1-3 in `Squads` order. `Squad.Alive` is refreshed each tick by `UpdateSquad`.
- Known shortcuts (search `shortcut:`): O(n²) separation (fine up to about 400 units) and unit stats in code instead of data files.
- `AttackDir` is named from the attacker's side; the defender must block `Dirs.Mirror(swing)`. The HUD shows incoming blows already mirrored.
- Separation is mass-weighted (horse 6, man 1) with radii 1.0 / 0.375 m; horse-vs-footman overlaps are where trample `Impact` happens.
- `Battle.Empty(seed)` builds a scenario field (two leaders far apart) for the self-test; `AddSquad`, `Hit` and `HitHorse` are reachable from it.
- With an idle player, the AI enemy (71 vs 63 units) wins the self-test battles in about 30 s. The balance assumes the player fights and commands.

## Next steps

1. Play-test the feel: horse turn rates, lance damage, parry timing (right now any matched block parries; a timing window would add skill).
2. Horse archers and a wedge formation for cavalry; AI riders remounting loose horses.
3. Unit cards you can click in the HUD, and a battle-start deployment phase.
4. Move unit stats into JSON, then build the campaign map layer.
5. A spatial grid for separation and target search, for bigger battles.
