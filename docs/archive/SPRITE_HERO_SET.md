# Sprite overhaul: the hero set

Status: done. The hero set and a second pass shipped on the `sprite-improvements` branch (merged 2026-11-19, `02576fc`);
archived the same day. The 8-direction question is still open (docs/BACKLOG.md).

A brief for the agent doing the first pass of the sprite overhaul. Do this set first, judge it, and use it as the look every
other part is redrawn to match afterwards.

## Why these

The set covers 13 troops that between them wear the parts most of the roster uses. Once they are done, **180 of the 477
troops** (38%) are wholly redrawn, and each slot is mostly covered: bodies 69%, heads 65%, weapons 68%, shields 89%,
feet 89% of troops. Every class is in it (foot, spears, bows, crossbows, horse, horse archers, casters), and so is every
main culture. Counts are from `Data/troops.json` on 2026-11-18.

## The troops (`Data/troops.json` ids)

| Troop | Tier, culture, class | What it brings in |
|---|---|---|
| `levies` | I common, foot | tunic, cap, axe, round shield, boots, cloth legs, bare hands: the base man |
| `shieldmen` | II common, foot | gambeson, kettle hat, sword, kite shield |
| `bowmen` | II common, archer | hood, bow, quiver, arrow |
| `men_at_arms` | III azure, foot | mail (body and legs), nasal helm, heater shield |
| `crossbowmen` | III azure, archer | crossbow (all its poses: spanning, aimed), bolt |
| `greatswords` | III crimson, foot | sallet, greatsword (two-handed grip) |
| `huscarls` | III northern, foot | spangenhelm, dane axe (two-handed) |
| `ghazis` | III southern, foot | turban, scimitar, dhal shield |
| `steppe_bows` | III steppe, horse archer | deel, fur cap, steppe pony with steppe tack |
| `ironguard` | IV azure, foot | plate (body and legs), great helm, longsword, sabatons, gauntlets |
| `tumen_spears` | V steppe, spear | lamellar, steppe helm, spear |
| `banner_knights` | V azure, horse | destrier with caparison, pennon lance (couched and raised) |
| `magisters` | V azure, caster | robe, coif, staff |

## The parts, as files

All under `Data/sprites/` (`parts/` for team parts, painted in the Azure palette; `neutral/` for faces and horse coats;
`sides/<scheme>/<p>/` for per-palette copies whose charge differs by side: redo those too).

- **Bodies** (`torso.X.f/s/b` + `sleeve.X`): tunic, gambeson, mail, plate, lamellar, fur, deel, robe
- **Heads** (`helm.X.f/s/b`): cap, hood, kettle, nasal, greathelm, spangen, steppe, turban, furcap, sallet, coif
- **Faces** (`neutral/head.vN.f/s/b`): six of the 96, v0-v5, as the set the other faces follow
- **Legs, feet, hands**: `leg.cloth`, `leg.mail`, `leg.plate`; `boot.boots`, `boot.sabatons` (f/s/b); `hand.skin`, `hand.gauntlets`
- **Weapons**: axe, sword, longsword, spear, daneaxe, scimitar, greatsword, lance, pennonlance, bow, crossbow, staff; arrow, quiver
- **Shields**: round, kite, heater, dhal
- **Horses** (`hleg.up/low`, `hbody`, `hchest`, `hrump`, `hneck`, `hhead`, `hface`, `htail` and the coats in `neutral/`):
  the plain horse with `saddle`, `caparison` and `steppetack`
- **Kit**: tabard (f/s/b), cape, plume

Left out on purpose, for the second pass: creatures (goblins, trolls, the dead, beasts), gilded and fur variants
(`plate_gilded`, `mail_fur`...), people-specific helms in `SpriteSheet.Peoples.cs`, the rarer bardings, and the other
90 faces.

## Rules the parts must keep

- **Keep each PNG's size** unless you also change the rig: `Puppet.cs` reads metres from pixels (`Art.Px`). Helm brows
  sit by `Puppet.HelmTop`; a helm whose top changes needs its row there updated.
- **Team cloth stays in the master's Azure team colours.** Other sides are a colour swap by `Data/sprites/manifest.json`, so a
  new team shade must be added to the swaps or it won't recolour (check a red and a green side).
- Side views face right and are mirrored for the left. Front, side and back of one part must read as the same object.
- PNGs are the product. Change them by hand (`tools/sprite_tool.py show/write/lint/team`) or change the painting code in
  `SpriteSheet.cs` and run `--bake-sprites --force <part>`. If you hand-edit, `--bake-sprites --check` will list the part
  as differing from the code; that is expected.
- **Open question: 8 directions.** We are weighing two more views, a three-quarter front and a three-quarter back
  (`.q`/`.r`, say), for every viewed part (torso, helm, head, boots, tabard). The user has not decided yet. Paint
  front, side and back now, but draw them so a three-quarter view between them is obvious to paint later, and keep notes.

## Before you start

The `t3/polish-*` worktrees (`git worktree list`) already have hand pixel passes for helmets, faces, weapons, shields,
torsos and sleeves, boots and horses. Look at them first, take what is good, and don't redo it blind.

## Checking

1. `dotnet run -c Release --no-build -- --sprites /tmp/x.png torso.plate helm.greathelm ...` shows the chosen parts at 6x.
2. `IRONFIELD_TROOPS=/dir` saves each troop standing; `IRONFIELD_ANIM=/dir` (`IRONFIELD_ANIM_ONLY=`) saves the animation
   strips; `IRONFIELD_SHOTS=/dir` saves the battle shots (`gallery-*.png`, `pose-sheet*.png`, `pose-horses.png`). Render under
   `xvfb-run` with `ALSOFT_DRIVERS=null SDL_AUDIODRIVER=dummy`.
3. Have a visual judge score it (see CLAUDE.md). Move on only at 7/10 or better: close up, in the field view at normal
   distance, and in a wide shot of a thousand men.
4. `tools/check.sh` must end `ALL CHECKS PASSED` (`SpritesBaked` and `Sprites` cover the atlas).
