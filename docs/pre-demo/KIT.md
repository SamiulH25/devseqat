# Troops made of kit and skill (plan)

The owner's brief (2026-11-21): "the units' stats are determined by their base stats plus their skills plus their equipment
used. Equipment should matter to each troop." Phase prefix: **KT**. KT0–KT4 and KT6 built (KT5 deferred post-demo; see "As built" at the end).

## Why it is not like this today

- Every troop's numbers (hp, damage, armour, reach, windup, recover, speed, block, pierce, range, reload, vsMounted,
  vsMonster) are typed by hand in `Data/troops.json`. They came first, from the battle prototype.
- The troop's look (head, body, legs, feet, hands, shield, weapon) came later with the sprites and is drawing only. Only a few
  rules read it: a shield blocks arrows and makes the shield wall, the weapon picks the attack style (IV+) and the passives.
  A man in plate can carry a tunic's armour if that is what was typed.
- Equipment with numbers exists only for the captain (`Data/gear.json`, P1; quality and marks C9-C11). `Rules.CaptainType`
  already builds him as *base man + skills (Arms, Riding, Bowcraft) + kit*. Troops were never moved onto it.
- Troops wear 109 kinds of parts; 24 of them have numbers in `gear.json` (weapons 13 of 28, shields 4 of 12, bodies 3 of 26,
  helms 4 of 43).

## The owner's decisions (2026-11-21)

1. **Skills are the captain's skills:** Arms, Riding and Bowcraft, one system for the captain and his men.
2. **Rebalance from scratch:** the kit and the skills decide; today's numbers are not the target. `--tiers` and the tests
   catch what breaks.
3. **Re-equipping companies comes later** as its own phase (KT7). First, a troop's kit is what it visibly wears, and it is real.

## The model

A man's battle numbers = **the base man** + **his skills** + **his kit**, built by one function for the captain and for every
troop (`Fighter.Build`, headless; `Rules.CaptainType` and `Troops.Load` both call it).

| Part | Gives | Where |
|---|---|---|
| Base man | hp, speed, a bare-handed block, stamina | one entry, `Data/gear.json` `base` |
| Arms | hp (+2% a 10 points, as the captain today), melee damage, block, swing speed | troop `skills` in troops.json, else by tier and class |
| Bowcraft | missile damage, reload, aim | " |
| Riding | horse hp, mounted damage and reach, the charge | " |
| Weapon | damage, reach, windup, recover, pierce, vs horse, range, reload, ammo | the look's `weapon` -> a gear piece |
| Shield | block (and arrow block), speed | the look's `shield` |
| Body, helm, legs, hands | armour, speed | the look's `body`, `head`, `legs`, `hands` |
| Horse | the mount | troop `mount` (as now) |

- Every part a troop wears has one gear piece (`look` = the sprite part). Pieces sit on material ladders so a reader can
  guess them: cloth < padded < leather < mail < scale/lamellar < brigandine < coat of plates < plate for bodies; hood < cap <
  nasal < kettle/spangen < barbute/bascinet < great helm for heads; buckler < round < kite/heater < tower for shields.
- **Troop-only pieces** carry `"troop": true`: no armourer sells them to the captain (a hood, a robe). The rest are in the
  captain's shop as now, so the captain gets the new pieces too (more equipment to find, the owner's earlier wish).
- **Creatures** (`culture: wild`) keep their typed numbers: a troll wears nothing.
- `UnitType` keeps the fields the sim reads; only where they come from changes. Data validation: every part a man wears has a
  piece; an upgrade is never worse on the built numbers (`Troops.WorseUpgrade`, as now).

## Phases

### KT0. Baseline
- Dump every troop's typed numbers and `--tiers 3` to `balance/kit-before.csv`; the comparison table for KT4.

### KT1. The kit table
- A gear piece for each of the 85 parts that lack one, on the ladders above (numbers from the captain's existing pieces,
  extended down for poor kit and up for the best). Legs and hands get small armour values (mail chausses, gauntlets).
- `Gear.Validate`: every troop's look parts have a piece; `--sprites`-style check that the piece's look exists.

### KT2. Skills for troops
- `skills: { "arms": n, "riding": n, "bowcraft": n }` in troops.json, optional. Default from tier and class: Arms
  10 + 15 per tier above I for foot and horse, Bowcraft the same for bows and skirmishers, Riding for horse; a line may lean
  (a culture's horse archers: Riding and Bowcraft both).
- The skill effects move into one place (`Fighter`), used for the captain too (his Arms, Riding, Bowcraft today act in
  `Rules.CaptainType` and `BattleCaptain`'s edges).

### KT3. The switch
- `Troops.Load` builds each man's numbers with `Fighter.Build`; the typed numbers leave troops.json for men (kept for
  creatures). `Rules.CaptainType` uses the same builder.
- Fix the data where an upgrade comes out worse (better kit or more skill for the upgrade, never a typed patch).
- Tests that count on a troop's exact numbers are set up again with the built numbers; none are weakened.

### KT4. Rebalance and measure
- `--tiers` (every step at equal men, upkeep and places, TI0 targets), `--agree` (fought vs auto-resolved), `--grounds`,
  `--pacing`, `--realms`, and the battle self-tests. Tune piece numbers and skill effects, not individual troops.

### KT5. Price from kit
- A troop's hire `cost` = the base man + its kit's worth + its skills' worth (one formula), so a better-armed troop costs
  more and wages (`Rules.WageCost`) follow. Promotion cost follows from it.

### KT6. See it
- The promotion screen and troop details list the kit piece by piece (name, what it gives) and the three skills, and say
  what changes on promotion ("+ MAIL HAUBERK: ARMOUR +8"). Hovering a company card shows its kit. Ties into TIERS TI3.

### KT7. Re-equip companies (later, its own phase)
- An armourer re-equips a company with better pieces or better-made ones (the captain's quality grades and smith's marks,
  C9-C11), for crowns and looted arms and armour; kit battered in hard fights, mended in towns; drawn with trim and gilt.

## Order and size

KT0-KT3 in that order (KT3 needs both tables). KT4 straight after. KT5 and KT6 can follow in either order. KT7 after the
demo's core is stable. Size: large; KT1 and KT3 are the bulk.

## Risks

- **Everything moves at once:** auto-resolve, AI armies, sieges and the career pace all read troop numbers. KT4 measures them
  all before the switch lands.
- **Upgrades that change only the look** (same kit, new colours) would now be no better: they need more skill or a better
  piece. `WorseUpgrade` finds them.
- **The captain's numbers** must not drift when his builder is shared: `CaptainCombat`, `CaptainKit` and `SkillBouts` stay green.

## As built

### KT4 (rebalance and measure), as built (2026-11-21)
- **Tuned bases and pieces** (no individual non-wild troop overrides; captain base untouched):
  - `Data/gear.json` `man` base anchored around tier III (Arms 40) so tier I is softer and tier II–VI scale cleanly: `hp: 52, hpPerArms: 0.0325`, `damage: 0.30, damagePerArms: 0.0125`, `block: 0.51, blockPerArms: 0.006`, `swing: 1.18, swingPerArms: -0.004`, `speed: 3.55`.
  - `mage` base: `hp: 100, hpPerArms: 0.05` (bringing tier II `acolytes` from 210 back near 163 HP while tier IV–VI stay 238–313), matching `man` speed/damage/block/swing.
  - Weapon polish: `flamberge` (`troop: true`) damage `46 -> 41` and `dane_axe` `windup/recover 0.55/0.65 -> 0.54/0.62` so `zweihanders -> executioners` wins 3/3 (0.41/0.00) at equal men.
  - Wild creatures (`Data/troops.json`, typed stats): `troll` armor `16 -> 11` (beatable by 30 shieldmen by day, still routs them at night) and `wolf` damage `18 -> 16` (pack tears through bowmen and breaks on a shield line).
  - `Campaign/CampaignState.cs`: synced `Balance.WageCurve` default to `0.6f` to match `Data/campaign.json`.
  - Test setup fixes (none weakened): `NamedInTheFight` keeps `foe.Hp = foe.Type.MaxHp` during the 5 s `Press()` alert-cooldown loop so `hob` does not kill the enemy leader early; `WarAndPeace` checks for peace-on-terms during the 240-day loop instead of only the 20-entry tail of `S.Treaties`.
- **Measurements after KT4:**
  - `--tiers 3` (370 links x 3 matchups x 3 seeds, up wins / auto agrees):
    - `I -> II`: men 100% (0.84/0.00), upkeep **67%** (auto 73%), places 100% (was 15% upkeep before KT4)
    - `II -> III`: men 100% (0.78/0.00), upkeep **63%** (auto 66%), places 100% (was 50% after KT3, 40% in TI1)
    - `III -> IV`: men 100% (0.80/0.01), upkeep **80%** (auto 81%), places 99%
    - `IV -> V`: men 99% (0.69/0.00), upkeep **71%** (auto 71%), places 95%
    - `V -> VI`: men 100% (0.68/0.01), upkeep **83%** (auto 83%), places 94%
  - `--agree 4 3.5`: at `DrillWeight = 5.0`, higher tier wins gap 1: 50%, gap 2: 75%, gap 3: 58%, gap 4: 50%, gap 5: 0% (4 fights).
  - `--grounds 16`: 0 unfinished across all 12 layouts, 84–120 s a battle, chooser wins 15–23/32.
  - `--realms 8`: largest share 33%, 0 flags.
  - `--pacing 4`: 4/4 Marshal (days 106–162), 549/562 battles won, 4/4 Vassal in target window (days 41–56, target 40–67).
  - `tools/check.sh`: `ALL CHECKS PASSED` (322/322 self-tests, including `CaptainCombat`, `CaptainKit`, `SkillBouts`).

### KT6 (see it), as built (2026-11-21)
- **Headless kit & skill display helpers** (`Fighter.cs`): `PieceEffect`, `KitList` (weapon, shield if carried, body, helm if worn, plus armoured legs/feet/hands), `KitLine` (one-line summary with mount for hover cards), `SkillsLine` (Arms, Riding, Bowcraft), and `KitChanges` (slot-by-slot promotion diffs such as `+ GAMBESON: ARM +3.5`, `+ KITE SHIELD: BLK +16%`).
- **Promotion screen and troop details** (`MapScreen.Upgrade.cs` `TroopDetail`): shows the three skills (`ARMS`, `RIDING`, `BOWCRAFT`) with promotion gains under the portrait, plus a dedicated `KIT CHANGES` + `KIT` column listing what changes on promotion and every worn piece with what it gives beside the stat bars.
- **Company card hover** (`Hud.cs` `SquadTooltip`, `EnemyGlass`; `MapScreen.Board.cs` `CompanyTile`, `CompanyDetail`): hovering a company card in battle command view shows its `KIT:` and skills (and visible `KIT:` for seen enemy companies); hovering a company tile or troop stack on the party board shows its `KIT:` (and skills on troop rows).
- **Verification**: added `TroopKitDisplay` in `Tests/GearTests.cs`; captured and checked `upgrade-tree.png`, `party-board.png`, and battle command HUD shots (`overflow.txt` empty).
