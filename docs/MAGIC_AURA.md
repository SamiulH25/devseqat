# Magic and aura for the player: plan

Status: being built (2026-11-09). Done: MA1. See "As built" at the end. Builds on docs/COMPANIONS_MAGIC.md (effects, mages, companions: built
through M4 + P) and docs/KNIGHT_ORDERS.md (aura and orders: built K1-K8). Read docs/LEGACY.md first: your company is
your legacy, and every change here must serve that.

## 1. Why this pass

Both systems work, are tested and balanced. What they lack is **the player's hand on them** and **the look of power**:

- **Magic happens near the player, not through them.** Your circle of mages and your companions cast on their own by
  their `when` rules (COMPANIONS_MAGIC Q8). The only choice you make is to hire a circle and stand it somewhere. You
  cannot say "storm now, on those archers", you cannot choose which spells it prepares, and nothing about the circle
  grows except its tier.
- **Aura is felt in numbers, not seen.** The blade is two lines along your facing, aura armour and presence are rings
  (`BattleScreen.PlayerGlow`). A tier 4 Aura Burst and a tier 1 Focus look almost alike. A breakthrough is a line in
  the report.
- **Pieces of the plan never landed:** an unsworn captain's own form (KNIGHT_ORDERS 5.4); aura steadying the men
  near you from tier 2 and lords who want an Aura Master (5.7); mirage and mist that shortens sight (E3); no tips for
  the blade or the forms.
- **The aura pool rarely matters.** Out of a fight it refills at 2/s (full in under a minute), so Spent is a short
  pause, not a choice.
- **Aura and magic don't touch.** A blessing doesn't help your aura, an enemy curse can't reach you, and your aura can't
  answer the enemy's mages.

## 2. Pillars (unchanged rules first)

1. **Battles stay a fight of men.** One circle per captain, spells warn first and hit part of one company at most, the
   captain alone still loses to two tier III companies (`AuraBound`). Nothing here raises those caps.
2. **The player chooses, the sim decides.** New control is an *order* (with the order delay and a courier where
   divisions use one) or a *choice before battle*, never a twitch button per spell. Forms stay the only keys you press.
3. **Deterministic.** All new randomness from the battle's `rng` or the campaign's streams. `ReplayExact` holds.
4. **Higher tier is better, cost is visible.** Every new power shows its price (Will, aura, Omen, corruption) in the UI
   before you pay it.
5. **Headless sim, drawn in the screen.** All rules in `Battle*`/`Campaign/*`; visuals only in `BattleScreen.*`.
6. **Auto-resolve agrees.** `--agree` stays inside its bands after every phase.

## 3. Magic, for the player

### 3.1 Command the circle (battle)

A new order for a caster company: **CAST** (order wheel slot when a caster company is selected; key `Act.Cast`).

- Pick a spell the circle knows and is ready (a small fan of its spells, greyed with the reason: "NO WILL", "NEEDS
  RAIN", "IN 12"), then click a spot or a company. The order travels with the usual delay (`Direct`).
- The circle walks into range if it must, then casts with the normal warning ring. Will, cooldown and `needs` rules are
  the same as for its own casting; a commanded cast costs **+5 Will** (they break their rhythm for you).
- **Hold spells** toggle (like FIRE / HOLD for bows): the circle keeps its Will for your orders and casts nothing on its
  own. Default stays "cast at will", so a player who never touches it plays as today.
- The AI general gets nothing new: it already casts by `when`.
- Files: `BattleAbilities.cs` (`Commanded` cast queue, `HoldSpells`), `Squad` (two fields), `BattleScreen.Orders.cs`,
  `Hud.cs` (spell fan), `Controls.cs`, F1 help.
- Tests: `CastOnOrder` (the ordered spell fires at the spot, after the delay, and costs the extra Will), `HoldSpells`
  (a holding circle never casts on its own), `CastWarns` (an ordered attack still warns).

### 3.2 The circle's spellbook (map)

Today tier decides the spells (IV one, V two, VI three + the great working). Keep that as the number of **prepared**
spells, but let the circle **know more than it prepares**:

- A circle knows its line's three spells, plus up to two more learned on the map: from a site (shrine, standing stones,
  hermit), a relic, a companion of the same tradition, or a rite. Learned spells come from the same tradition only.
- Before a battle (the encounter panel, and the muster roll) you choose which known spells are prepared. Weather and
  ground already matter (lightning needs rain, wildfire needs dry), so this is a real choice: the panel shows the
  ground note next to the spells it helps or blocks.
- Save: `CompanyRecord.Spells` (known), `CompanyRecord.Prepared` (chosen; empty = the line's default, so old saves play
  as today). Into battle via `Company.Spells` -> `LearnSpells`.
- Files: `Campaign/Rites.cs` (learning), `Campaign/Sites.cs`, `MapScreen.Muster.cs`, `MapScreen.Dialogs.cs`.
- Tests: `SpellbookCarries` (prepared spells, and only those, reach the battle), `SpellbookByTier` (never more prepared
  than the tier allows).

### 3.3 The circle's master (legacy)

The circle gets a named head, on the roll like any named man (`Soldier`), with a tradition title (*Mother of the Hearth*,
*Stormcaller*). He gains a rank with battles (3 / 8 / 15), each rank giving the circle +10 Will cap or a cheaper great
working. If he falls the circle is **Leaderless** for 10 days (no great working), and the most senior caster rises. He can
become a companion (Priest or Cunning calling) at rank 3. That ties magic into the legacy pillar: the circle is people.

- Files: `Campaign/Roll.cs`, `Campaign/Callings.cs`, `BattleAbilities.cs` (cap and cost).
- Test: `CircleMaster` (rises, falls, the next rises; the bonus reaches the battle).

### 3.4 The captain and faith or craft (no new class)

The captain does not become a mage. Two small, player-pressed links instead:

- **Raise the relic** (if you carry it): a fifth slot on the form bar, once a battle, spends a relic charge. It casts a
  Blessing round you and gives a burst of aura (+30). Faith, no Omen.
- **Craft marks** (a craft rite, Omen +3): for the next battle your aura blade also leaves `fever` on a man it strikes.
  Pious companions dislike it (existing reactions in `Bonds.cs`).

### 3.5 Finish the spells left out

- **Mist shortens sight** (`BattleSight.Visible`: a company inside mist is seen only within 25 m) - this makes Call the
  mist an ambush tool, not only arrow cover.
- **Mirage** (Stars, tier VI): a false company for the enemy AI for 30 s (`Visible` returns it; `General` may turn a
  division to face it). Never drawn as real men for the player: a faint outline.
- **Counterspell** by rule: a Ward field cancels an enemy cast whose spot lies inside it (the ring breaks, Will is still
  spent). Faith answers craft.
- Tests: `MistHides`, `MirageTurnsAI`, `WardBreaksCast`.

### 3.6 Omen with teeth, and a way to spend it

Omen today only draws three events. Add:
- At 20+: enemy necromancers in the province raise one extra rank (`Undead.RaiseHost`) and grave fields stir sooner.
- At 30+: one village in three refuses you (as the flame's 50 does, milder and temporary).
- **Cleansing**: a priest companion or a faith circle can work *Cleanse* in a town with a chapel (100 crowns, a day):
  Omen -10. So faith and craft in one company balance each other.
- Test: `OmenTeeth`.

## 4. Aura, for the player

### 4.1 The pool matters

- Out of a fight aura comes back at 1/s (was 2), at 2/s while standing still with no foe within 30 m (catching your
  breath behind the line is a choice). In a fight unchanged.
- **Overflow**: at 100 aura, landing blows banks up to 20 **surge**, spent first by the next form. Rewards fighting
  well without spending; shown as a bright cap on the AURA bar.
- **Spent** gets a visible cost: below 20 your blows are -10% and stamina recovers 25% slower (an effect, `spent`),
  so emptying the pool is a decision.
- Check `AuraBound` still holds; retune `BladeDrain` if needed.

### 4.2 Your men see it (KNIGHT_ORDERS 5.7, not yet built)

- From tier 2, while you fight within 10 m of your own company, it gets `inspired` (+3 morale floor, +5% block). Tier 4
  presence already covers foes; this covers friends at every tier, smaller.
- A breakthrough mid-battle (trials can now land in battle, see 4.5) rallies every company within 30 m.
- Test: `MenSeeTheAura`.

### 4.3 Lords and the realm see it (5.7, not yet built)

- Aura Expert: +5 opinion with lords of your realm on meeting; Aura Master: +10, and your realm's council counts you as
  a rank higher for the marshal's baton.
- Bandits and small parties under a third of your Might flee an Aura Master on the map rather than fight.
- Renown from duels x1.5 at tier 3+.
- Files: `Campaign/Characters.cs`, `Career.cs`, `CampaignAI.BanditThink`. Test: `AuraIsKnown`.

### 4.4 Your own form (5.4, not yet built)

An **unsworn** captain at Aura Expert who has never sworn makes a form from how he fights. The battle counts it already
(`FormUses`, parries, ripostes, blows by weapon); add a running tally of style per battle:

| Most of your fighting | Your form | Kind |
|---|---|---|
| parries and ripostes | *Your Answer*: a counter with a longer window | counter |
| mounted charges, lance | *Your Charge*: a leap from the saddle (the only mounted leap) | leap |
| bow | *Your Mark*: a shot through more men | shot |
| heavy weapon blows | *Your Ruin*: a short burst round you | burst |
| sword and shield, holding | *Your Wall*: a guard that also shields the man beside you | guard |

The form takes your company's name (*The Tamsworth Answer*) and is what your own order teaches (K8 already reads
`OrderFormNow`). It is offered as an event after the battle in which you cross the threshold, with two of the five to
choose from (your top two styles).

- Save: `CaptainState.StyleTally`, the form as a generated `FormDef` saved in `CaptainState.OwnForm` (copied from the
  generic kind's numbers, +10%).
- Files: `Campaign/Aura.cs`, `BattleForms.cs`, `Forms.cs` (accept a saved form), `Data/events.json`.
- Test: `OwnFormFromStyle` (a parry-heavy tally gives a counter, a lance tally a mounted leap; it slots and works).

### 4.5 Forms that grow with choice

Ranks stay (15 uses a rank). At **rank 3** a form offers one of two **aspects**, chosen on the K sheet, permanent:

- Slash: *Wide* (radius +50%) or *Far* (range +40%).
- Guard: *Long* (+2 s) or *Riposte* (each blow stopped strikes back at half power).
- Leap: *Shock* (radius +1, more reel) or *Return* (half the aura back if the target falls).
- Buff/great: *Broad* (radius +30%) or *Lasting* (+50% duration).
- Shout, burst, shot, focus, edge, counter: two each in `Data/forms.json` (`aspects`).

Data-driven: `aspects: [{ "id", "name", "line", "mods": {...} }]` multiplying the form's own numbers. Save
`CaptainState.FormAspects`. Test: `AspectChanges` (each aspect moves its number and only that).

### 4.6 Trials in the field, a moment to remember

- When you stand at a wall, a battle can break it: kill an enemy champion of your tier or above, or hold against two to
  one until the enemy breaks. The breakthrough happens **on the spot**: a short slow-down (the V6 big-moment code), a
  flash, the pool refills, the new tier's gift is live for the rest of the battle, and companies near you rally (4.2).
- The campaign side applies the tier after the battle as today (`BreakThrough`); the battle flags it in `BattleResult`.
- Test: `BreakInBattle` (the tier rises mid-fight; the replay reproduces it).

### 4.7 Clash of champions

When you and an enemy aura champion are within 3 m and both blades are lit, a **clash** starts: other men give way (a
3 m ring nobody steps into for 8 s, `Separate`), the camera drifts in if you have not moved it, and blows between you
two land 20% harder. It ends when one of you falls, steps out, or 8 s pass. Pure presentation plus one number; the sim
stays deterministic. Test: `ClashRing` (men stay out, it ends).

### 4.8 Tier 5 deserves more

Transcendent today = great form twice. Add: your presence also reaches 20 m and blocks enemy `debuff` spells inside it
(the light that will not be cursed), and your aura blade cuts arrows from the air in front of you (arrows into a 60°
cone in front have a 50% chance to fall short while the blade burns). Rare, visible, legendary.

## 5. Aura and magic together

- **Blessing, War song** on your company also give you +10 aura if you stand in them.
- **Enemy curses can reach you**: Evil eye on the captain quenches your blade for 6 s; Fever drains your breath. Your aura
  armour (tier 3+) halves a curse's time; tier 5 blocks it (4.8). So a captain who rushes the enemy circle is a choice
  with a price, and killing their mages (horse, elite bows, you) matters.
- **Your blade cuts a spell**: a slash or a leap that lands on an enemy caster mid-cast breaks the cast (`Casts` removed,
  Will spent). A Master hunts mages.
- **Champions with circles**: an enemy order army with a mage company and a champion gets the champion's forms aimed at
  your circle first (`ChampionWants`).
- Tests: `BlessingFeedsAura`, `CurseOnCaptain`, `BladeBreaksCast`.

## 6. The look and the sound (V-style polish)

Magic and aura today are rings and lines. Raise them to the level of the V1-V8 pass, all in `BattleScreen.Effects.cs` /
`.Draw.cs`, drawn only, nothing in the sim:

| Thing | Now | Planned |
|---|---|---|
| Aura blade | two lines | a glowing ribbon trail on the swing arc, colour of your order (Lily silver, Rose red, Wolf frost...) or your banner; brighter with tier |
| Aura armour | ring | a shimmer on the puppet; a flash and sparks where a blow is soaked |
| Presence | ring | dust and leaves stirring outward, a faint pressure ripple every 2 s |
| Forms | rings/fans | per kind: slash = crescent sweep, leap = streak + landing dust ring, shout = ripple, burst = flash + motes, buff = rising motes on the companies hit |
| Breakthrough | report line | slow-down, a pillar of light, a gilded toast "AURA EXPERT" |
| Spells | rings | Smite a pillar of light, Lightning a jagged bolt + flash, Wildfire flame sprites and smoke, Mist/Smoke soft billboards, Storm local rain streaks, Blessing gold motes, Dread dark motes |
| Sound | `Cue.Spell`, `Cue.Shout` | a cue per tradition, the blade's hum while lit, a deep thrum for presence, a chord on breakthrough |
| HUD | AURA bar, form bar | surge cap on the bar; form aspect pips; the spell fan (3.1); a red "CURSED" chip on the captain when a curse lands |

Check every new look with `IRONFIELD_SHOTS` (new staged shots: `aura-blade`, `aura-forms`, `spells-faith`,
`spells-craft`, `breakthrough`) and keep `render.txt` counts reasonable (bench within 10%).

## 7. Teaching it

- Tips (`Tips.cs`): first time you reach Kindled ("press U for Focus"), Aura Knight ("hold ALT for the blade"), first
  circle hired ("select it and press C to cast"), first time Spent, first curse on you.
- F1 help lists CAST and the relic slot.
- K sheet: a short line under each form, the aspect choice at rank 3, the style tally toward your own form (unsworn).
- The UI lab gets a page for the spell fan and the new form bar (`UiLab.Pages.cs`).

## 8. Balance and checks

- `AuraBound` must still hold (tier 4 beats one tier III company of 40, loses to two). The surge and the tier 5 arrow
  cut are the risks: tune them first.
- `MagesHelp`, `SuperMages` must hold: commanded casting is not stronger than auto casting over a whole battle, only
  better timed (+5 Will each).
- `--agree`: commanded casting is not in auto-resolve (it assumes the player plays well, like AuraWeight).
  A circle's master rank adds to `Rules.MightAt` through the existing caster weight, small.
- `--bench 1500`: no new per-man work except the curse check on the player (one unit) and mist sight (per company).

## 9. Phases

Each phase ends with `tools/check.sh` green, `tools/codemap.sh` run, docs updated and a commit.

| Phase | What | Main files | Done when |
|---|---|---|---|
| **MA1** | Command the circle: CAST order, spell fan, hold spells | `BattleAbilities.cs`, `BattleScreen.Orders.cs`, `Hud.cs`, `Controls.cs` | `CastOnOrder`, `HoldSpells`, `CastWarns`, shot |
| **MA2** | The look: aura blade trail, soak sparks, form effects, spell visuals, sounds | `BattleScreen.Effects.cs`, `.Draw.cs`, `Audio.cs`, `.Shots.cs` | new shots read well, bench within 10% |
| **MA3** | Pool that matters + men see it: regen, surge, spent cost, `inspired` | `BattleAura.cs`, `Data/effects.json`, `Hud.cs` | `AuraBound` holds, `MenSeeTheAura` |
| **MA4** | Spellbook and the circle's master | `Campaign/Rites.cs`, `Roll.cs`, `Callings.cs`, `MapScreen.Muster.cs`, `.Dialogs.cs` | `SpellbookCarries`, `SpellbookByTier`, `CircleMaster` |
| **MA5** | Aura and magic together: curses on you, blade breaks casts, blessing feeds aura, relic slot, craft marks | `BattleAura.cs`, `BattleAbilities.cs`, `BattleForms.cs`, `BattleChampion.cs` | `CurseOnCaptain`, `BladeBreaksCast`, `BlessingFeedsAura` |
| **MA6** | Your own form and aspects | `Campaign/Aura.cs`, `Forms.cs`, `Data/forms.json`, `MapScreen.Captain.cs` | `OwnFormFromStyle`, `AspectChanges` |
| **MA7** | Moments: trials in battle, clash of champions, tier 5 gifts | `BattleAura.cs`, `BattleChampion.cs`, `BattleScreen.Effects.cs` | `BreakInBattle`, `ClashRing` |
| **MA8** | Finish the spells and Omen: mist sight, mirage, ward counterspell, Omen teeth, cleanse; aura and lords | `BattleSight.cs`, `BattleAbilities.cs`, `Campaign/Rites.cs`, `Characters.cs`, `Career.cs` | `MistHides`, `MirageTurnsAI`, `WardBreaksCast`, `OmenTeeth`, `AuraIsKnown` |
| **MA9** | Teaching and polish: tips, F1, K sheet lines, UI lab page, README, `--agree`, bench | `Tips.cs`, `Hud.Overlays.cs`, `UiLab.Pages.cs`, `README.md` | `overflow.txt` empty, `--agree` in band |

MA1 and MA2 first: they change how magic and aura feel at once with the least rules risk. MA3 next, since the pool
change touches every later number.

## 10. Open questions

1. **Should the player command spells at all?** COMPANIONS_MAGIC Q8 said no (mages cast on their own). This plan says
   yes, as an order with a delay and an extra Will cost, with "cast at will" as the default. If you would rather keep
   magic hands-off, drop MA1 and keep MA4 (choosing spells before battle) as the player's lever.
2. **Can the captain learn a spell?** Proposed: no; the relic slot and craft marks are the only links. A full
   "captain-mage" path would be a second power ladder beside aura.
3. **Surge (4.1)**: is a reward for not spending fun, or does it make players hoard? Try it behind a balance value.
4. **Clash of champions (4.7)**: should the camera move by itself? Proposed: only if you have not touched it for 3 s, and
   a setting turns it off.
5. **Aura colours**: by order, or by your banner? Proposed: order if sworn, banner if not.

## As built

- **MA1 (command the circle):** `Squad.HoldSpells`, `CastSpell` (-1 = none), `CastAt`, `CastSince`, recorded in replays
  like any order. `Battle.OrderedCast` runs first in `ThinkSpells`: it waits for the spell's cooldown and Will
  (`OrderedWill` 5 more), shifts the circle's anchor once to close within range, gives up a spell whose `needs` the spot
  does not meet, a spell it does not know, or an order older than `OrderLasts` (30 s), and picks an officer within 8 m of
  the spot for officer spells. An ordered cast is a normal `Cast` (it warns). `HoldSpells` stops casting by the `when`
  rules. Screen: `Act.Cast` (P) picks the next spell of a selected caster (a circle first, else a company whose hero has
  abilities) in command view; left click casts on the ground (`CastAt`, through `Send`, so a division company gets it
  late); right click cancels. X (FIRE / HOLD) toggles `HoldSpells` on selected circles. HUD `Hud.SpellFan` lists the
  spells with Will cost or why not (`CannotCast`); cards read HOLD. Test `CastOnOrder`.
