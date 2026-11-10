# Animation and roster pass (plan)

Two tracks that touch the same files (`Puppet.cs`, `SpriteSheet.cs`, `Data/troops.json`), so they are planned together.
Track A makes men move like the weapon in their hands. Track R fills the thin realms and gives the new kit to new troops.
Do A1-A3 first: several roster troops (halberdiers, javelin men, sword cavalry) only read right once the poses exist.

All animation is drawing only (docs/DESIGN.md section 5): the sim never reads a pose, so replays and `--selftest` numbers stay put.
The check for every A phase is the pose sheets (`pose-sheet*.png`, `pose-horses.png`, `gallery-*.png` from a shots run).

## Where things stand (survey, Nov 2026)

**Animation** (`Puppet.PoseOf`, `Puppet.Keys`): one key set per class and swing.
- Every melee weapon uses the one-handed keys. A greatsword, Dane axe or club swings in one hand; the off hand never joins the grip.
- `Keys` sends all Skirmisher and Cavalry men to the thrust keys, so halberds and glaives only poke, and mounted swordsmen
  (mamluks, khan guard) stab with a sword.
- Crossbows draw like bows: held upright in the off hand with a string pulled back. No shouldering, no spanning.
- Casters have no pose of their own. They stand and swing their staff like a club; the cast only shows in the effects
  (`BattleScreen.Gather`, `Sigil`).
- No formation stance: a shield wall, a braced pike line and a front rank of shooters all stand the same way.
- Legs are straight sticks swinging from the hip. No knee, so no crouch, brace, kneel or lunge.
- Idle is breathing plus a weight shift. Turning on the spot slides.
- Riders: no mounted cut, no rider falling from a dying horse.

**Roster** (247 troops; `Data/troops.json`, lines in `Data/factions.json`):

| Culture | Troops | Off-line branches |
|---|---|---|
| northern | 49 | 28 (raiders, berserkers, dragoons...) |
| azure | 40 | 21 (sergeants, foot knights, pavise chain...) |
| crimson | 29 | 11 |
| southern, steppe, league | 26 each | 6 (3 mages, 3 order troops) |
| order | 23 | 3 |
| wild | 13 | tops out at tier 5; undead are 3 troops |

- Southern, steppe, league and order have only their four lines: no branch troops to choose on promotion.
- Every archer and horse line repeats its tier 2 troop in slot 1 (`bowmen, bowmen`, `outriders, outriders`).
- Weapon looks: 13 in use. `mace` is painted but no troop carries it. There is no halberd, glaive, javelin, sling,
  curved sword or war hammer sprite: halberdiers carry `pike`, glaivemen and javelin men carry `spear`.
- Heads (11) and shields (5) are thin for seven realms; southern and steppe lean on `turban` and `steppe` for everything.

## Track A: animation

### A1. Grips: one hand, two hands, polearm
- Add `Look.Grip(weapon)` (Units.cs): `One`, `Two` (greatsword, daneaxe, club, new hammer), `Pole` (pike, spear, halberd, glaive),
  `Lance`, `Bow`, `Crossbow`, `Staff`.
- `Keys` keyed on grip and swing, not class. Two-handed: wider, slower arcs, off hand placed on the grip below the main hand
  (`Arm(-1, ...)` solved to reach a point on the weapon). Polearm: thrust plus an overhead chop for halberd and glaive.
  Mounted one-handers get a downward cut from the saddle.
- Body follows the weight: two-handed strikes lean and twist further (`LeanX` by swing side).
- Check: pose sheet row per grip; `--sprites` not needed (no new parts).

### A2. Legs with a knee
- Two-segment legs (hip, knee, foot) in `Puppet.Draw` and `Fallen`; the existing `leg.*` sprite split into thigh and shin
  (`SpriteSheet`: keep the old key as a fallback until every leg kind is painted).
- Add `Pose.Crouch` (0..1). Used by: braced pikes and spears (`Unit.BraceCd`/`Braces`), shield wall, front rank of shooters
  kneeling, the stagger, the crumple death. A lunge on thrusts.
- Feet step when a standing man turns more than ~40 degrees (a two-beat shuffle from `ViewFacing` change).

### A3. Crossbows and casters
- Crossbow: held level at the shoulder, bolt along it, a small kick on the shot; then the reload (`Unit.Cooldown` against
  `UnitType.Reload`): butt down, foot in the stirrup, both arms haul the string, bolt laid, raise. Pavise men reload behind
  the board.
- Caster: a gathering pose while a cast winds up (staff raised, off hand out toward the target, a lean back), a thrust of the
  staff on release; idle with the staff planted. Read the cast state the battle already keeps (`BattleAbilities`).
- Javelin and sling poses land here too once R1 adds the weapons (overarm throw; sling whirl and release).

### A4. Formations and lines
- Shield wall: shields up and overlapped, crouch 0.4, short steps. Square: outward facing already; shields up.
- Pike line: rear ranks hold pikes raised (pitch up) until the front closes; braced front rank crouched with the butt grounded.
- Archers in a line loose a little apart (phase), not as one.
- Check: a new staged shot `formations.png` in `BattleScreen.Shots.cs`.

### A5. Idle life and small reactions
- Idle set picked by phase and grip: lean on the weapon (two-handed, pole butt grounded), rest the sword on the shoulder,
  look round (head turn on the head billboard), roll the shoulders. Only when no foe within ~25 m.
- Hit reactions by where the blow came from (`DiedFrom`-style side), not one flinch backwards.
- Horses: shifting weight, a head toss, pawing when held.

### A6. Riders and falls
- Mounted cut and backhand for swords and axes (A1 keys), horse archers turning in the saddle to shoot sideways and behind.
- A dying horse goes down on its knees and the rider is thrown forward; a rider killed on a living horse slides off the side.
- More death variants by grip and blow: spun round by a side cut, folded over a thrust, flung back by a troll's sweep.

## Track R: roster

### R1. New kit to paint (`SpriteSheet.cs`, `Grip` table, `Look.Ranged`)
- Weapons: `halberd`, `glaive`, `javelin` (thrown, a bundle on the back), `sling`, `scimitar`, `falchion`, `warhammer`, `mace` (exists).
- Heads: southern `helm.spike` and `helm.mail_veil`; steppe `helm.fur_cap`, `helm.lamellar_dome`; league `helm.barbute`;
  order `helm.bucket_cross`. Shields: `buckler`, `targe` (round with a boss), `dhal` (small southern).
- Javelin and sling need sim support: `Look.Ranged` plus a thrown-missile kind in `Shoot`/`UpdateArrows` (short range,
  high damage, few shots). That is headless and moves selftest numbers; add a test in `Tests/BattleTests.cs`.
- Check: `--sprites out.png <parts>` at 6x for each new part.

### R2. Re-arm the existing lines
- Halberdiers and their chain -> `halberd`; glaivemen chain -> `glaive`; sun_javelins and dune_spears -> `javelin`;
  mamluks, emir guard, dervish blades -> `scimitar`; billmen -> `glaive`; give `mace` and `warhammer` to
  order and azure heavy foot where it fits. Stats unchanged unless the weapon kind changes how they fight.
- `TierSurvey`/`TierFight` must still pass: higher tier stays better.

### R3. Branch troops for the thin realms
Bring southern, steppe, league and order to ~36-40 troops each, the same shape azure and northern already have: two or three
off-line chains a realm reached by `upgradesTo` from its line troops, tiers 3-7, each with a job the lines lack.
- **Sunward:** slingers (T2-4 skirmish line from levies), camel riders (T3-6, `camel` barding exists), naffatun fire throwers
  (T4-6, javelin kind with a burn effect from `effects.json`), dervishes (T3-5, fast and lightly armoured).
- **Steppe:** dismounted archers' guard, lancer heavy horse (T4-7, lamellar barding), wolf-hide scouts (T2-4, fast light horse).
- **League:** marines (T3-6 boarding foot with buckler and falchion), a mixed pavise-and-spear company, condottiere foot
  (hired swords, high pay, low loyalty).
- **Order:** sergeant-brothers chain on foot, turcopoles (light horse archers), hospitaller infirmarians (T4-6 casters
  using existing healing abilities).
- Wild: a tier 6-7 top for goblins (warlord) and undead (wight, death knight) so late campaigns meet them at full strength.

### R4. The tier-1 slot in archer and horse lines
The lines repeat tier 2 because a tier 1 archer or horseman cannot be reached from the tier 1 root (`Troops.Validate`:
upgrades must raise tier). Options, to choose before building:
1. Leave it; it only shows in the line lists.
2. Culture tier-2 entry troops (hunters, herd boys, slingers) so each realm's first ranged and horse step looks its own,
   instead of the shared `bowmen`/`outriders`.
Recommendation: 2, since it also answers "every realm's recruits look the same".

### R5. Data checks and balance
- Each new troop: `Troops.Validate`, `SelfTest.TroopTree`, `SelfTest.SevenRealms`; new effects through `effects.json`
  (unknown keys refused).
- `--balance 4` and `--agree 4 5` after R2 and R3; auto-resolve `Might` must stay in step with fought results.
- Update `README.md` troop lists and `docs/CAMPAIGN.md` where realms are described.

## Order of work

| Step | Phase | Notes |
|---|---|---|
| 1 | A1 grips | biggest visible gain, no new sprites |
| 2 | R1 paint kit | halberd, glaive, scimitar, mace in use; javelin and sling sprites |
| 3 | A3 crossbow, caster, throws | needs R1 javelin/sling, and their sim kind |
| 4 | R2 re-arm lines | data only after 1-3 |
| 5 | A2 knees | sprite split; unlocks A4 |
| 6 | A4 formations | |
| 7 | R3 branches, R4 entry troops | the bulk of the data work; one realm a commit |
| 8 | A5 idles, A6 riders and falls | polish |
| 9 | R5 balance pass, docs | |

Each step ends with `tools/check.sh` passing and, for A steps, a look at the pose sheets.
Move this file to `docs/archive/` when the last phase ships.
