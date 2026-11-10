# Spectacle pass: plan

Plan for the next visual pass after docs/VISUALS.md (V1-V8) and docs/MAGIC_AURA.md 6 (MA2). Those made blows land and gave
magic a look; this pass makes the *big* things feel big. Magic and aura come first: they are meant to be rare and awe
inspiring, and today a Smite and a Mend differ mostly in colour. When a phase ships, add its "As built" note at the end.

## 1. What we have to build on (read before starting)

| Hook | Where | What it gives |
|---|---|---|
| `Battle.Flashes` | `Battle.cs:40`, filled by `Flash(...)` in `BattleAbilities`, `BattleForms`, `BattleAura`, `BattleMoments` | moments the sim marks (`cast`, `spell:<kind>`, `bolt`, `form:<kind>`, `land:<kind>`, `blade`, `soak`, `breakthrough`, `clash`, `broken`) with place, radius, team, tag (tradition or order) |
| `Battle.Casts` | `BattleAbilities.cs:33` (`Cast.Started`, `FireAt`, `A.Great`, `At`, `A.Radius`) | every working **before** it lands: warnings are 1.5-2.5 s for the big ones (smite 2, tempest 2, crows 2.5, greek fire 2.5) - room for a build-up |
| Great workings | `AbilityDef.Great` (`miracle`, `tempest`, `crows`, `greek_fire` in `Data/abilities.json`) | once a battle, tier VI casters only: the natural "rare" set |
| Aura state | `BattleAura.cs` (`AuraTier` 0-5, `AuraBlade`, `Spent`, `Surge`, `Presence`), `BattleMoments.cs` (`InLight`, `KeepRing`, `ClashAt`), `BattleChampion.cs` (`FoeTier`) | the ladder and its moments |
| The look of magic | `BattleScreen.Magic.cs` (`FlashFx`, `FieldFx`, `BuffFx`, `AuraFx`, `Ripple`, `Beam`, `Rise`, `OrderColour`, `SpellColor`) | the switch every new look plugs into |
| Time scale | `BattleScreen.cs:311-314` (`scale`: `Ending` 0.5, `hitPause` 0, `CommandTimeScale`) | slow motion is already safe: `Replay.Frame.Dt` records each tick's step, so a slowed view replays exactly |
| Camera and marks | `BattleScreen.Effects.cs` (`Kick`, `trauma`, `AddMark` ring buffer, trample map, falling banners) | shake, ground decals, flattened grass |
| Light | `Sky.cs` (sun, ambient, `Grade`, `Night`), `Shaders/common.glsl` `TorchLight` (one fixed row) | no moving point lights yet |
| Post | `Shaders/post.glsl` (FXAA, `Tint` grade + bloom, saturation/contrast/vignette from `Grade`) | one full-screen pass to extend |
| Poses | `Puppet.PoseOf(u, now, phase, winner)` | every man's pose from his state: the place for men reacting |

Rules carried over from V1-V8: **drawing only**. Nothing changes who wins, `SelfTest.ReplayExact` stays green, the self-test
numbers do not move. New `Flash` calls are fine (they are view hints the sim appends and the screen empties). Every effect
has a `Quality` switch and Low looks like today. No imported art.

## 2. The rules of awe

1. **Contrast.** Ordinary battle stays earthy and grim. Common troops never glow; tier I-III aura is barely visible. The
   spectacle budget goes to rare things.
2. **Build-up, then release.** Every great working and every aura peak has three beats: *gathering* (from `Cast.Started`
   to `FireAt`), *the strike*, *the aftermath*.
3. **The world answers.** Light, sky, wind, grass and sound bend to it; men near it react.
4. **It leaves a scar.** Big workings leave marks that last the whole battle (not the evicting ring buffer).
5. **Seen across the field.** A great working must read from the command camera 300 m away: tall light, sky change,
   a sound with a long tail.
6. **A budget.** At most **one cinematic beat** (slow view, letterbox, camera nudge) per battle, plus the closing beat.
   First come first served, priority: breakthrough > clash of champions > your great working > the foe's great working.
   Everything else gets the build-up and strike but no cinematic.

## 3. Phases

### S1: Groundwork (small, do first)

Tools every later phase needs. New file `BattleScreen.Spectacle.cs` (partial, view only).

1. **The director.** `Beat(kind, at, length)` asks for the battle's one cinematic beat (rule 6); returns false if spent or
   a higher one is running. While a beat runs: view time scale eases to 0.35 (same path as `Ending`), letterbox bars slide in
   (HUD layer, 8% of height), the field camera turns toward `at` (command camera: a slow pull-in), and the ambient bed ducks
   by 60% (`Audio`: a duck value the music and crowd loops read). `Settings.Cinematics` (on/off) beside Camera shake.
2. **Moving light.** Up to 4 point lights per frame: `Uniform.Vec4("Glow0".."Glow3")` (xyz world, w strength) +
   `Vec4("GlowCol0..3")` in `Shading.cs`; `GlowLight(vec3 w)` in `common.glsl` beside `TorchLight`, added in `world.glsl`
   and `sprite.glsl`. Negative strength *takes* light (for the Black Flame, S5). The screen picks the 4 strongest live
   sources (beams, bolts, fires, the tier 5 captain). Off on Low; 2 lights on Medium.
3. **Sky override.** `Sky.Omen(dark, tint, swirl)` blended over the hour/weather values for a few seconds and eased back:
   darkens sun and ambient, tints the grade. `sky.glsl` gets a `Swirl` uniform (centre in screen + strength) that winds the
   cloud noise into a slow vortex (Tempest).
4. **Post uniforms.** `Awe` (vec4): xy = a screen point, z = desaturate everything outside a radius round it (breakthrough,
   Dread), w = heat shimmer strength (a small noise offset of the UV near the bottom of fire; Wildfire, Greek fire).
5. **Scars.** `AddScar(at, radius, kind)` with kinds `Glass` (smite: pale, glinting), `Scorch` (fire, lightning: black with
   ember specks for 20 s), `Frost` (Wolf order), `Bloom` (Miracle: green and flower specks), `Crack` (breakthrough). Own small
   pool (64), never evicted by blood marks. Scorch and Bloom also stamp the trample map so grass flattens or stands.
6. **Men react.** A view-side list of awe sources (place, radius, strength, until). `Puppet.PoseOf` gets an optional
   `awe` term: within the radius men lean back, raise the off arm against the light, look up (head pitch) for ~0.8 s,
   staggered by their phase. Fighting men keep fighting; only idle and waiting men react fully.
7. **Shots.** `IRONFIELD_SHOTS` gains a frozen-time staging helper: `StageCast(id, team, at, t)` puts a `Cast` mid-gather
   at fraction t so a build-up can be shot (`Ui.Instant` keeps the rest still).

Done when: a dummy beat in a shots run shows letterbox + dimmed sky + one glow lighting men; `ReplayExact` green.

### S2: The gathering (every cast with a warning)

Read `battle.Casts` each frame in a new `GatherFx(dt)` called from `MagicFx`. Fraction `g = (Time - Started) / (FireAt - Started)`.

- **Sigil circle.** Replace the plain warning ring: a ring of tradition marks drawn as short line strokes (faith: rays and
  crosses; craft: knots and runes; arms: none). It draws itself round the circle as g rises, then spins up in the last 20%.
  Radius = the working's radius, so the foe can read where it lands (the warning stays honest).
- **Pull.** Motes and dust drift *inward* to the caster; the grass leans toward him (world shader: a `Pull` vec4 point +
  strength, one at a time, the strongest cast).
- **The caster.** His cloak and banner lift; a column of faint light rises with g; his glow is a moving light (S1.2).
- **Great workings only:** from g = 0.3 the sky darkens (`Sky.Omen`), the wind stills (grass and banner wind fade), the
  ambient ducks, a low swelling tone per tradition (`Cue.GatherFaith`, `Cue.GatherCraft`: synthesized swells in `Audio`).
  Your own great working asks the director for the beat at g = 0.6 so the release lands in slow time.

### S3: The great workings and the strong spells

Each entry: gathering (S2) + strike + aftermath. All in `FlashFx` / `FieldFx`; new flash names where the sim has no moment yet.

| Working | Strike | Aftermath |
|---|---|---|
| **Smite** (faith attack) | clouds part over the spot (Omen light hole), a shaft descends slowly over 0.4 s with dust in it, then the hit: white flash, shock ring, men thrown in the pose | `Glass` scar that glints in sun; motes rising for 3 s |
| **Miracle** (great) | dawn light falls on the field round the caster whatever the hour (sun colour and grade pulled warm for 6 s), a choir chord (`Cue.Choir`), gold petals fall | `Bloom` scar, routed men turning (already in the sim) get a gold glint as they rally |
| **Lightning** | a forked bolt (3 branches, not one jag), the whole field flashes (grade spike, one frame), men drawn as silhouettes for 2 frames, thunder after a delay by distance | `Scorch` scar, smoke wisp |
| **Tempest** (great) | the sky spirals (`Swirl`), rain turns sideways, banners stand straight, men lean into the wind, two lightning strikes in the field | heavy rain stays for the field's duration (already a storm field), puddles grow |
| **Wildfire** | the fire spreads as a visible front across the field's radius over 2 s, heat shimmer (`Awe.w`), ember storm, a smoke column tall enough to read from anywhere | `Scorch` scars where it burned; fire is a moving light at night |
| **Greek fire** (great) | pots arc in (drawn arcs from the caster), splash into liquid flame that runs downhill a little, roaring sound | `Scorch`, burning for the field's life |
| **Plague of crows** (great) | a real flock (a few hundred bird sprites on a cheap boid: circle, funnel, dive) rising from the caster and falling on the target | birds circling over the cursed company for its duration |
| **Mist** | rolls out from the caster as a tide (soft billboards moving outward), men inside fade to grey shapes | thins at its edges |
| **Mirage** | a ghost copy of the target company shimmering, with a heat edge | breaks into sparkles when the foe sees through it (`MirageFor` ending) |
| **Dread / Dread drums** | colour drains in a ring round the target (`Awe.z`), shadows stretch toward it | a faint dark vignette on that company |
| **Ward** | invisible until hit: arrows that strike it flash a hex of light on a faint dome | the dome flickers when broken (`broken` flash) |

Only one great working per side per battle exists in the sim, so the full treatment never repeats much. The common spells
(Mend, Blessing, War song, Fair wind) keep their light MA2 look on purpose (rule 1).

### S4: The aura as a rarity ladder

In `AuraFx` and `BattleScreen.PlayerGlow`; colours from `OrderColour` (your order) or `Style.Aura`. Each tier must read as
clearly greater than the last, and an enemy champion's (`FoeTier`) uses the same ladder in the foe's cold tint.

| Tier | Look |
|---|---|
| 1 Kindled | a glint along the blade's edge when lit; nothing else |
| 2 | the lit blade glows; faint footprints of light that fade in 1 s |
| 3 | air ripple round the captain (heat shimmer at his feet), dust lifts as he moves |
| 4 Master | grass flattens outward in a ring (trample map stamp), pebbles and leaves float, nearby foes get the awe pose (they hesitate: drawn only) |
| 5 Transcendent | a column of light over him seen from anywhere (`InLight`), the day brightens round him (moving light, grade), arrows cut (`CutsArrow`) burst into sparks |

- **High-tier swings (4-5):** a crescent of light that hangs in the air 0.3 s along the arc (extends `bladeTrail`), and a
  cut line drawn through dust on the ground.
- **Order motifs:** Lily silver petals, Rose red petals, Wolf frost breath and `Frost` scars where he stands still, Falcon
  feathers of light, Lantern a warm lamp glow, Ashen embers, Hollow black fire (S5).
- **A foe champion from afar:** his glow shows at 2x the distance you can make out his men, so you see him coming.

### S5: The aura's moments

- **Breakthrough in the field** (`breakthrough` flash, MA7): asks for the beat. Colour drains from everything but a circle
  round the captain (`Awe.z`), a heartbeat cue, time at 0.25; after 1.2 s a white shock ring, the colour floods back from
  him outward, a `Crack` scar, his men raise weapons (cheer pose). Gilded toast "AURA EXPERT" (exists) lands after.
- **Clash of champions** (`clash`, `KeepRing`): the field round the ring dims, each blade meeting throws a flash that goes up
  to the sky (a short beam), and the two colours push at the ring's middle (two half rings, the stronger one wider). Asks
  for the beat on the first meeting only.
- **The Black Flame** (`BattleFlame`, `Madness`): the captain's aura in purple-black, flames that *take* light (negative
  glow, S1.2), the screen edge warps slightly while `Mad` (post shimmer at the edges only), crows of shadow round him.

### S6: Mundane spectacle that frames the magic

So the field around the magic is alive too (keep each small):

- **The charge in slow time.** When a cavalry squad charging (`Order.Charge`, speed > 8) will reach a formed foe in
  under 1 s and the player is within 60 m or it is selected, ask for the beat: lances lower together (pose), the ground
  trembles (rising `Kick`), impact throws dust, lance shards (sprays) and the hit men into the awe pose's throw-back.
- **Storms.** Weather rain at high strength gets lightning now and then (view only: flash + delayed thunder, no damage),
  gusts as visible waves crossing the grass (wind uniform pulse), sunbeams after rain clears.
- **Fire at night.** Fire pots and burning fields light men (moving lights, S1.2).
- **Instant replay (later, optional).** `Replay` stores inputs, not positions, and re-simulating to a moment costs too much in
  a long battle. A replay needs a view-side ring of snapshots (pos, dir, action, hp per man at 15 Hz for 5 s: ~4 MB at 3000
  men). Build it only if the slow-time beats feel short.

### S7: The aftermath

- **The battle report** (`MapScreen.Report.cs`): a great working or a breakthrough you saw gets a gilded line with its icon
  ("Your circle called down the Tempest"). Needs `BattleResult.Wonders` (a list of strings): a result field, not sim logic.
- **On the map (later):** a great working leaves a mark in the province for a season (a scorched or blooming patch drawn by
  `World` at the battle cell). Needs a save field (`CampaignState.Scars`: cell, kind, until) and a site hook in
  `Campaign/Sites.cs`; plan it with docs/SIDE_CONTENT.md, not in this pass.

### S8: Quality, speed and checks

1. Quality: Low = today; Medium = 2 lights, no shimmer, birds halved; High = all.
2. `--bench 1500` unchanged (no sim work). `render.txt`: a great-working shot stays within 1.5x the busiest V-pass shot.
3. New shots in `BattleScreen.Shots.cs`: `gather-smite` (g = 0.7), `smite-strike`, `miracle`, `tempest`, `wildfire`,
   `crows`, `aura-ladder` (five captains, tiers 1-5 side by side), `breakthrough-beat`, `clash-beat`, `black-flame`.
   Look at each with the Read tool; `overflow.txt` empty.
4. Tests: `SelfTest.ReplayExact` with a slowed beat in the recording; a `Spectacle` test that the director grants one beat
   per battle and in priority order (pure logic, headless: keep the director's choice in a small class the test can call).
5. Docs: docs/DESIGN.md (feedback section), CLAUDE.md file guide row for `BattleScreen.Spectacle.cs`, `tools/codemap.sh`.

## 4. Order and size

| Phase | Size | Why this order |
|---|---|---|
| S1 | medium | Lights, sky, post, scars, the director: everything after uses them |
| S2 | small | One method over `Casts`; makes every big spell feel big at once |
| S4 | medium | The aura ladder is seen every battle the captain has aura |
| S3 | large | One look per working; can be split (faith, craft-storm, craft-fire, crows) |
| S5 | medium | Needs S1 beat + post, S4 ladder |
| S6 | small-medium | Charge beat and storms; replay only if wanted |
| S7 | small | Report line now; map scars later |
| S8 | small | Closing pass |

## 5. Out of scope

- Any change to what spells or aura *do*. If a look suggests a rule ("men really should rout from the light"), write it in
  docs/MAGIC_AURA.md as a question instead.
- New art direction: faceted world and pixel puppets stay; birds and petals are painted parts in `SpriteSheet.cs`.
- A general particle system rewrite: motes and `Spray` stay; S1 only adds lights, sky, post and scars.
