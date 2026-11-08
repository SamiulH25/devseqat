# Visuals and motion pass: plan

Status: **planned**. Each phase V1-V8 can ship on its own, and `tools/check.sh` must pass after each one.
When a phase ships, add its "As built" note at the end.

## 1. Goal

No new rules. Make the game **look and feel alive**: men who move like men, blows that land with weight, a world with
weather and time in it, and an interface that responds to the hand. The test for each item: *does a player notice it in
the first ten minutes, or does it make a battle easier to read?* If neither, it waits.

## 2. How this plan was made (2026-11-08)

- Took every staged shot at 1280x720 (`IRONFIELD_SHOTS`, `IRONFIELD_CAMPAIGN_SHOTS`) and looked at each.
- Read the animation code (`Puppet.PoseOf`, `Keys`, `Corpse`, `WolfBody`), the effects (`BattleScreen.Draw.cs` `Effects`,
  `Spray`, `GroundSteps`), the post pass (`Shaders/post.glsl`), the UI kit (`Ui.cs`, `UiKit.cs`) and `docs/DESIGN.md` 2-6.

**What is there now:**
- A man's pose is three key poses (idle, raised, struck) blended **linearly** by action progress. The walk is one sine on
  the legs. Arms, torso and head do not move with the walk. Nobody idles: a standing line is perfectly still.
- Death is one 0.5 s tip-over (`fall = k*k*80 deg`), then the parts are laid flat in the same heap for every man.
- Hit feedback is a red tint. Stagger is one fixed arm pose. There is no flinch, no knock-back, no hit pause.
- Particles: sparks, blood, dust, ground splashes (`Mote`, cap 1500). Nothing stays on the ground but corpses.
- Post: FXAA, grade, split tone, vignette. No bloom, no depth of field, no colour per weather or hour beyond `Sky.Set`.
- The UI has no motion at all: panels, tabs, tooltips and screens appear and vanish in one frame. Bars jump to their value.
  `docs/DESIGN.md` section 5 says so on purpose ("Nothing on the HUD bounces or slides").
- Screens cut hard: title -> map -> battle -> results with no fade.

**What the shots show:**

| Shot | What reads badly |
|---|---|
| `gallery-close*`, `gallery-side-close` | Close up, the ground is a flat smear of green; men stand rigid like cut-outs; no grass at their feet. |
| `field-view` | A line of men in identical poses; the captain's horse and rider are stiff. |
| `landscape`, `command-overview` | Squad flags pile on top of each other in a big battle; the label "RIGHT BATTLE HOLD" runs under the divisions panel. |
| `battle-result` | The VICTORY panel appears over a still frozen field: no moment of victory. |
| `campaign-map`, `campaign-close` | The map is dark and murky; trees are as big as towns and hide them; parties are static models. |
| every campaign panel shot | The welcome tip still draws over open panels and the event dialog. |
| `campaign-event` | The illustrated scene is a still picture (the moon and banners could live). |
| `overflow.txt` | Party labels at the screen edge ("SAUVETERRE DIGGINGS 140") are cut off instead of kept on screen. |

## 3. Rules for the whole pass

1. **The sim does not change.** All animation is in the view (`Puppet`, `BattleScreen`, `MapScreen`, `Ui`), reads sim state,
   and uses its own random (`BattleScreen.fx`, or a hash of `Unit.Id`). Never touch the battle `rng`, never add sim fields
   only for looks. Proof: `--selftest` prints **the same numbers** before and after each phase, and `ReplayExact` passes.
   If a look needs a fact the sim does not keep (who hit me, from where), add it as a plain field the sim already knows
   when it happens (like `DiedAt`), and check the selftest numbers do not move.
2. **3000 men must stay smooth.** Every per-man addition is a few float ops, no allocations. Far men (`Puppet.Detail > 0`)
   skip the extras. Each new effect sits under a `Quality` tier (High / Medium / Low) in `Settings`.
3. **Every new look has a staged shot**, so a change can be checked from the cloud with the Read tool.
4. **Motion has a job.** It shows cause and effect, draws the eye to what changed, or makes a moment land. No idle wobble on
   the HUD. `docs/DESIGN.md` section 5 is rewritten in V1 to say this (see V5).

## 4. Phases

### V1: Animation groundwork (small, do first)

1. **Easing curves.** Add `Ease` (static, headless-safe: `Out`, `InOut`, `Back`, `Spring(t, damping)`) next to `Style.cs`
   or in a new `Ease.cs`. Use it everywhere below instead of linear `Lerp`.
2. **Pose sheet shot.** A staged shot (`BattleScreen.Shots.cs`) that lays out one man of each class across a row, each frozen at
   a different moment of an action (idle, windup 50%, strike, recover, block, stagger, walk 0/25/50/75%, fall 25/50/100%).
   One still then shows every pose: the way to review animation without video. Same for horses (walk, trot, gallop, rear).
3. **Per-man phase.** A stable `float Phase` from `Unit.Id` (hash, view only) so walks, idles and banner waves are never in step.
4. Rewrite `docs/DESIGN.md` section 5 "Motion" to the rule in section 3.4, with the timings this plan uses.

Test: `Ease` endpoints (0 -> 0, 1 -> 1, monotone for `Out`/`InOut`) in a small headless test.

### V2: Men who move like men (`Puppet.cs`)

1. **Strikes with weight.** Windup eases in (slow start, held at the top for the last 15%), strike snaps (`Out` over the first
   30% of recover), follow-through overshoots then settles (`Back`). The torso leans into the swing (rotate the torso
   billboard a few degrees with the arm yaw); overheads dip the body.
2. **A real walk and run.** Arms swing opposite the legs, the body bobs twice per stride, the head lags a little. Speed picks
   walk or run (bigger stride, forward lean). Shield arm swings less than the sword arm.
3. **Idles.** Standing men breathe (tiny torso scale/bob) and, now and then by `Phase`, shift weight, look aside or rest the
   weapon. Spearmen in a braced line keep the brace pose. Archers nock, draw, hold, release with a string snap and recoil
   instead of one aim pose.
4. **Hit reactions.** On `HitFlash` start: a short flinch away from the blow (the torso and head kick back 0.12 s, `Spring`
   back). Stagger is a stumble with a step back, not a frozen arm. A block shows the shield jolt.
5. **Deaths that differ.** Fall direction from the killing blow (back if hit from the front, forward if from behind, to a side
   for a swing) or, without that, from `Phase`. Three fall curves (crumple, topple, spin). The corpse is laid out in the
   direction of the fall with a little random spread of the parts, so a field of dead stops looking stamped.
6. **Routing and victory.** Routers drop shield and weapon (a dropped-item corpse part), run with arms up. When `Winner` is set,
   the winning side's men raise weapons (a cheer pose with a random delay) while the losers flee.
7. **Horses.** A proper gait cycle by speed: walk (4-beat), trot (diagonal pairs), gallop (gathered/extended with body pitch).
   The head bobs with the stride. Rear on a hard stop or a charge impact; a falling horse tips and rolls instead of appearing
   dead. Riders rise in the stirrups at the gallop and lean into the lance.
8. **Creatures.** Trolls lumber (heavy sway, ground puff on each step), skeletons clatter (jittery joints), wolves already
   have a gait; give them a pounce arc on windup.

Shots: the pose sheet from V1 shows every item. Check `--bench 1500` frame cost of `BuildSprites` before and after (target:
no more than +10% at High, none at Low).

### V3: Blows that land (battle effects, `BattleScreen.Draw.cs`)

1. **Camera kick (field view only).** A tiny shake when your own blow lands or you are hit, a longer low rumble when a charge
   strikes near you. Scaled by a new Settings slider "Camera shake" (0 turns it off).
2. **Hit pause for the player.** When your strike lands, the *view* holds 40 ms (render the same frame; the sim steps on as
   normal, so replays stay exact). Off on Low.
3. **Arrows.** A faint streak behind each arrow in flight; arrows that miss stick in the ground (pooled, fade after a minute);
   volleys darken the sky for a moment from below.
4. **What stays on the field.** Blood and trampled-earth decals under fights (a pooled ring buffer of flat quads, a few
   hundred at High), dropped shields and weapons, and a trample map: grass where many men walked flattens and darkens
   (a small render target sampled by the grass shader).
5. **Bigger moments.** A charge throws a dust wave along its front; a broken squad leaves a cloud of dust as it runs; the gate
   breaking throws splinters and dust; a troll's sweep throws men and a ground ring.
6. **Banners.** Flags wave with real cloth: a cheap 3-segment wave driven by speed and wind, and they dip and fall when the
   bearer dies.

### V4: The world (terrain, light, weather)

**Battle field**
1. **Ground close up.** Add a detail noise (two octaves, world-space) and colour patches (clover, dry spots, flowers) to the
   terrain shader, faded in only within ~30 m, so the close-up stops being a smear (`gallery-close*`).
2. **Grass at their feet.** Denser, shorter tufts near the camera; men's feet sink into them.
3. **Cloud shadows** sliding across the field (noise in `world.glsl`, by the same wind as the grass).
4. **Weather that shows.** Rain darkens and glosses the ground and men, puddles appear on low ground, splashes on hits;
   fog thickens in valleys; snow sits on the ground, roofs and trees in winter; dawn and dusk tint everything warm with long
   shadows (`Sky.Set` already gives the hour).
5. **Light polish.** A soft bloom on bright sky and sun glints off steel (only highlights over a threshold, half-res), and a
   weather/hour grade in `post.glsl` (cold blue in rain, gold at dusk).

**Campaign map** (`MapScreen.cs`, `World.cs`)
6. **Brighter and clearer.** Lift the map's light and grade so it reads by day like the battle field; scale trees down
   against towns; fade foliage near the eye (Q3 noted it; check it is enough at close zoom).
7. **Parties that march.** The banner-bearer sprites walk with a bob and a dust trail when moving, stand when stopped;
   armies at war show a small fire/camp when resting; a besieged town has smoke.
8. **A living map.** Chimney smoke over towns, rivers that flow, birds over forests, day and night across the map with town
   lights at night, and season colours (spring green, autumn gold, winter white; `campaign-winter` already has snow).
9. **Labels.** Keep party and town labels on screen at the edges (fixes `overflow.txt`), fade small labels at far zoom.

### V5: The interface responds (`Ui.cs`, `UiKit.cs`)

1. **An animation clock in `Ui`.** `ui.Anim(id, target, speed)` returns an eased value that follows a target by the frame's
   real `dt` (not the sim's). One small dictionary keyed by widget id; nothing per frame allocates.
2. **Panels and modals** fade in and rise 6 px over 120 ms; close in 80 ms. The map or field behind dims smoothly.
3. **Buttons** brighten on hover over 80 ms and press down 1 px; the gold edge glints once when a button becomes available.
4. **Bars and numbers** slide to their new value (health, morale, XP, gold); gold and renown count up on the results screen;
   a bar that drops leaves a short red "lost" segment that drains after it (shows how much a hit took).
5. **Tabs** cross-fade their content; the tab underline slides to the new tab.
6. **Tooltips** wait 250 ms, then fade in; the event feed already fades (keep its timings).
7. **Toasts and beats** (rank up, battle honour, a named man rises) get an entrance: fade plus a gold shine across the ribbon.
8. **Fix: the tip over open panels.** The welcome tip stays drawn over every campaign panel in the shots. Hide an
   *already shown* tip while a panel or modal is open, not only new ones.

### V6: The big moments (front end and flow)

1. **Screen transitions.** A fade through black (or a quick iris on the banner) between title, map, battle and results; a short
   "loading" plate with the battle's name, ground and weather while the battle builds.
2. **Battle opening.** Before deployment, a 4-second camera flyover: along the enemy line with their banner and name, then
   back over your army; any key skips. Field layouts with a name (ridge, ford, village) show it.
3. **Battle ending.** When `Winner` is set: the view slows to half speed for 1.5 s (view only: the sim is already finished),
   the camera rises over the field while the winners cheer (V2.6), then the VICTORY / DEFEAT plate rises in.
4. **Title screen.** A slow camera drift over a generated battlefield or the campaign map at dusk behind the menu, with the
   logo plate; the menu buttons get the V5 states.
5. **Event pictures.** The illustrated scenes (`MapScreen.Event.cs`) gain light motion: banners wave, the moon glows,
   clouds drift, a slight parallax between layers, fire flickers.
6. **Campaign beats.** Rank up, a realm founded, a battle honour: a full-width banner sweep with a sound cue, not just a panel.

### V7: Big-battle readability

1. **Flag declutter.** In command view, flags that overlap on screen stack or shrink; far flags lose their letter; the hovered
   or selected squad's flag lifts to the front (`landscape`, `command-overview`).
2. **Division labels** (the "RIGHT BATTLE HOLD" text) stay off HUD panels and fade when they overlap each other.
3. **Selection and orders.** The selected squad's ground ring pulses once when selected; a given order draws its arrow from
   the squad and fades over 1 s, so you see that it took; couriers carry a small flag so you can watch the order travel.
4. **Squad state at a glance.** Wavering squads' flags tilt and shake slightly; routed squads' flags drop and grey.
5. **HUD weight at 1280x720.** The tip, the unit panel and the order panel crowd the bottom (`command-overview`): let the tip
   sit above the cards and the order panel collapse to icons when narrow.

### V8: Quality, speed and checks

1. Every new effect has a `Quality` switch; Low looks like today.
2. `--bench` gains a render-side figure (draw-call count and sprite count per frame from a shots run) so a heavy effect shows.
3. Re-take every shot; `overflow.txt` empty; the pose sheet and new shots looked at.
4. Update `docs/DESIGN.md` sections 2, 3 ("Feedback on units"), 5 and 6 to what was built, and `CLAUDE.md` for new files
   (`Ease.cs`, decals, trample map) and the new shots and Settings.

## 5. Order and size

| Phase | Size | Why this order |
|---|---|---|
| V1 | small | Tools for everything after: easing, the pose sheet, the phase. |
| V2 | large | The biggest change in feel; every battle shows it. |
| V5 | medium | Touches every screen; cheap per widget once `ui.Anim` exists. |
| V3 | medium | Builds on V2 (hit reactions, deaths) to make blows land. |
| V6 | medium | Needs V2.6 (cheers) and V5 (fades). |
| V4 | large | Shader work; most of it is independent and can be split. |
| V7 | small | Readability fixes; can go any time. |
| V8 | small | Closing pass. |

## 6. Out of scope

- New art direction: the faceted 3D world and pixel-puppet men stay. No imported art, models or textures.
- Skeletal or 3D men; sprite parts on the puppet stay the format.
- Gameplay or balance changes, even ones that look better (a dropped shield does not change armour).
- Music and sound beyond the cues a new moment needs (cheer, banner sweep, transition swoosh).
