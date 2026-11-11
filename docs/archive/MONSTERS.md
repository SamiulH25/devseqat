# Monsters that threaten: plan

Goal: monster encounters that feel dangerous and unlike fighting men. Before this pass goblin holdouts went into battle behind
a human captain, there was no beast that was not a wolf or a troll, and the wilds were thin. Now every monster host is led by a
monster, four new kinds fight by rules of their own, and there are more of them, doing more harm.

## M1. No man leads a monster

A battle's enemy leader comes from the host (`Campaign.LeaderOf` -> `BattleOptions.EnemyLeader`, `MonsterLeader` in
Campaign/Beasts.cs): a holdout's Goblin Chieftain, a raiding band's hobgoblin, a pack's Alpha Wolf, an Elder Troll, a nest's
Brood Mother, the bear or wyvern itself, the dead's wight. The battle log names it ("The Goblin Chieftain is slain!"). The map
draws the party as its leader.

## M2. New beasts (Data/troops.json, culture `wild`)

| Troop | Kind | What it does |
|---|---|---|
| Goblin Chieftain | goblin, IV | boss and fear: the holdout's head |
| Alpha Wolf | wolf, V | leads a pack; white |
| Elder Troll | troll, VI | the troll set, bigger |
| Giant Spiders / Spiderlings | spider, III / I | pack; the bite poisons, the big ones spit webs |
| Brood Mother | spider, VI | huge, poison, webs, lays spiderlings as she fights |
| Cave Bears | bear, IV | knockback; rage below half hp |
| Wyverns | wyvern, VI | flying, sweep, poison sting, knockback, fear |

## M3. Their rules in battle (BattleMonsters.cs)

- **Poison**: 70% of a poisoned blow stays behind as poison, worked off at 9 hp a second; armour does nothing; it can kill.
- **Webs**: a spider spits at a foe 2.5-13 m off; men within 2.2 m (3.3 m from a brood mother) are held 3.5 s at a fifth of their
  pace and block at 0.4 of their chance. First spit after 1.5-3.5 s.
- **Brood**: two spiderlings every 4.5 s while foes are near, up to 30, in a company called The Brood.
- **Rage**: a wounded bear strikes 1.4 times as hard with a windup of 0.7.
- **Flight**: a wyvern cruises 7 m up; above 1.6 m no blade reaches it ("OUT OF REACH"). It dives, strikes, and climbs away for
  1.6-2.6 s. Arrows hit it at its height (`FlyerHit`). It does not collide with men while aloft.

## M4. On the map (Campaign/Beasts.cs, Campaign/Monsters.cs)

- **Spider nests** in the deep woods breed to 28 (scaled), send bands out at night once they hold twice their start, and lie
  still by day. Cleared like holdouts.
- **Bears** sleep by night and maul anyone weaker who comes near by day; they shamble off from stronger bands.
- **Wyverns** roost in high country by night and hunt by day: prey weaker than them, else they ravage a village. You cannot
  retreat from one. Lords weigh a wyvern double when choosing a hunt; brigands avoid wyverns and bears.
- **Ravaging**: trolls and wyverns may fall on villages (`Ravage`, `Balance.MonsterRavageChance`).
- **More of them**: more holdouts, packs, trolls (start 3, up to 8), the dead rise more often; each missing pack, bear, troll or
  wyvern has its own daily chance to return.
- Monsters judge a fight by `Rules.Might`, as auto-resolve does. Brigands and beasts let each other pass unless one hunts the other.

## As built

All of the above shipped in one pass. Tests: `Tests/MonsterTests.cs` (`MonsterLeaders`, `SpiderVenom`, `BearRage`,
`WyvernFlight`, `MonstersAbroad`; `BeastTrace` is a debug run, not in the list). Shots: `IRONFIELD_SHOTS_FROM=beasts`
(gallery-beasts, gallery-beasts-side, beasts-fight, beasts-fight-late). Simplifications: the beasts reuse the wolf's falls
and the troll's sweep and knockback; a wyvern on the map is a single strong party, not a moving flyer over terrain.
