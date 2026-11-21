# Troops made of kit and skill (plan)

The owner's brief (2026-11-21): "the units' stats are determined by their base stats plus their skills plus their equipment
used. Equipment should matter to each troop." Phase prefix: **KT**. Nothing is built yet.

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
