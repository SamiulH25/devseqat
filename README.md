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

You need .NET SDK 10 (`net10.0`). The game needs no content files: sprites, terrain and the font are all generated in code (`Art.cs`).
To inspect the sprite atlas, run `IRONFIELD_DUMP_ATLAS=/path/atlas.png dotnet run`.

## Controls

**Field view** (you fight; third-person camera over the right shoulder):
WASD move, mouse look (you face where you look), Shift sprint,
LMB hold+release attack (a longer hold hits harder), RMB block (front only, costs stamina), R rally, Tab command view.

**Command view** (Tab):
LMB select a squad (Shift adds), RMB click moves (keeps the current width), RMB drag draws the formation line (it faces the enemy),
WASD pan, Q/E or middle-drag orbit (middle-drag also tilts), wheel zoom, G hold in place.
Tab eases the camera between the two views over 0.45 s (focus, yaw, pitch, distance and FOV are all blended).

**Both views:** 1-3 toggle a squad, 0 selects all (with nothing selected, orders go to every squad),
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

## Architecture

| File | Role |
|---|---|
| `Battle.cs` | Headless simulation: units, AI, melee, arrows, morale, enemy general, win check. No graphics. |
| `Squad.cs` | Squad orders (Hold/Follow/Charge) and formation slots (anchor = centre of the front rank). |
| `Units.cs` | `UnitType` stat table (in code for now) and the `Unit` state. |
| `BattleGame.cs` | MonoGame `Game`: input, the blended third-person/command camera, picking, the render passes, HUD. |
| `Puppet.cs` | Draws each unit as a puppet of part sprites on a 3D skeleton, with continuous poses from its action state. |
| `Art.cs` | Part sprites as ASCII pixel art (built per palette into a 256² atlas), props, grass, shadow, and the 5x7 `PixelFont`. |
| `SelfTest.cs` | `--selftest` checks. |

Ground positions are `Vector2` (X = world X, Y = world Z).

### Puppet sprites (how units are drawn)

Each unit is about 15 small pixel-art sprites placed on a 3D skeleton (unit space: +X right hand, +Y up, +Z facing, metres; 1 sprite px = 0.075 m):
- **Billboards** (head, helm/hood, torso, hands, boots, plume, cape, quiver) always face the camera. View parts swap between `.f`/`.s`/`.b`
  sprites by the angle between the unit's facing and the camera; side sprites face right and are mirrored.
  Layered parts get a small toward-camera `bias` (the helm sits over the head) so they never z-fight.
- **Axial** parts (legs, sleeves, sword, spear, bow, arrow) stretch between two joints and turn about that axis to face the camera.
- **Planes** (shields) are fixed world quads, so a shield really swings between the front and the side.
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
- With an idle player, the AI enemy (61 vs 53 units) wins the self-test battles in about 40 s. The balance assumes the player fights and commands.

## Next steps

1. Decide the player combat model (tap/hold attacks, parry, dodge and similar). It is still open.
2. Cavalry (mounted units, charge momentum).
3. Move unit stats into JSON, then build the campaign map layer.
4. A spatial grid for separation and target search, for bigger battles.
