# The company is the legacy: plan

Status: **L0-L2 built** (see "As built"). Each phase L0-L8 can ship on its own, and `tools/check.sh` must pass after each one.
When a phase ships, add its "As built" note at the end. Numbers marked *(tune)* are starting values.

## 1. The fantasy

> **You lead a free company in person. Every man you lose was someone you knew, and every honour on the banner is one
> you earned.**

This is Ironfield's one main player fantasy. Every other system serves it:

- **The battle** is where the company is made and unmade: how you fight and command decides who comes home.
- **The campaign** is how you feed, grow, shelter and reward the company: pay, recruits, contracts, a home.
- **The career** (rank, fiefs, council, the crown) is a way to give the company land and standing. It stays in the game
  as a possible ending, but it is no longer the spine.

**The test for any feature:** does it make the player care more about *their* men, or let them see what they've built?
If neither, it is not core work. It can still ship, but it waits behind work that passes the test.

## 2. Where we stand (2026-11-08)

Found by reading `CompanyRecord` (CampaignState.cs), `Campaign/Companies.cs`, `Campaign/Characters.cs`,
`Campaign/Career.cs`, `BattleBanner.cs`.

**Already strong (keep and build on):**
- Companies persist: a name, role, origin (`Culture`, `Home`), a banner charge, cohesion, an 8-battle `Ledger`, up to three
  `Honours` with real effects (`Honours.Of`), and kill and battle counts.
- Each company has a sergeant (`Officer`, with traits). One who lives through 15 battles becomes a companion (F3), and
  companions lead divisions in battle (`DivisionRecord`), with a temper and a command skill.
- Men are promoted one at a time along the troop tree, so a veteran stack is visibly hard-won.
- Your banner and the last stand (`BattleBanner.cs`), duels, battle goals (`BattleDeeds.cs`), and the chronicle and end
  summary (`Campaign/Record.cs`).

**The gaps the fantasy shows:**
1. **Men are numbers.** `TroopStack` is `Troop, Men, Wounded, Xp`. Nobody below the sergeant has a name, so "every man
   you lose was someone you knew" isn't true yet.
2. **Loss is a number too.** The battle report gives "lost 14", nothing more. Nothing is remembered once the 8-battle
   ledger rolls over.
3. **Sergeants come from nowhere.** `WithOfficer` makes up a new one. They should rise from the company's own veterans.
4. **The banner shows nothing.** Honours are three words on a panel and stars on the muster roll. In battle a famous
   company looks the same as a new one.
5. **The campaign ends on politics.** `CheckEnd` wins on realm share as marshal or king. The end summary names a
   "best company" but doesn't tell its story.
6. **Auto-resolve dodges the cost.** If auto-resolved fights kill no one you know, players will auto-resolve to protect
   their men. Every phase below must apply to auto-resolve as well.

## 3. Out of scope

Naming all 72 men in every company, permadeath of the captain by default (it is offered only as an option, L7), a full
character sheet per soldier, morale meters per man, voiced lines.

## 4. Phases

### L0: Write the fantasy down (docs only)
- Put the pitch and the feature test in `docs/DESIGN.md` as pillar 0, and one line in `README.md` under "The campaign".
- **Done when:** both say the same thing as section 1 here.

### L1: Names in the ranks
Not every man, only the ones who have earned it. A company keeps a small **roll** of named men.
- New save class `Soldier { Id, Name, Troop, Battles, Kills, Wounds, Joined (day), Deeds (list of short strings), Dead,
  DiedDay, DiedAt }` and `CompanyRecord.Roll` (`List<Soldier>`). Up to `RollSize` named men a company *(tune: 6)*.
- **How a man earns a name:** after a battle a company may name one man for a deed: most kills, survived a rout, held at a
  wall, killed a lord or a horseman, the first man of a new troop tier. Each new company also starts with one named man.
  This is campaign rng only. Names come from `Names`; today `Data/names.json` has one shared `sergeants` list, so L1 adds
  men's names by culture so a man's name fits his home.
- A named man is a man of his stack: promoting the stack can promote him, and when the stack loses men he can be among them.
- Show the roll on the muster roll (P): name, troop, battles, kills, and one deed line.
- **Tests:** `SelfTest.RollEarnsNames` (names arrive after battles, the cap holds, a name fits the culture, old saves load
  with empty rolls).

### L2: Named men in battle
- `Company` (ArmySpec) carries its roll. `Battle.AddSquad` places named men as real units (`Unit.SoldierId`), at the front
  rank for foot and the first file for horse.
- The sim tracks what they do: kills, lord or horse kills, falling, being wounded or killed (using the existing
  wounded/killed split in `Kill`). This uses the battle's seeded rng only, so replays stay exact (`SelfTest.ReplayExact`).
- `CompanyResult` returns each named man's fate and his kills, and `Campaign.Apply` writes them back.
- **Auto-resolve:** `Rules.AutoResolve` gives each named man a death chance of company loss fraction × `VeteranSurvival`
  *(tune: 0.6; veterans live a little more often)*, and a wound chance the same way.
- HUD: a small pennant over a named man when you're within 25 m, and his name when hovered in command view.
- **Tests:** `SelfTest.NamedMenFight` (a named man is placed, his kills count, his death comes back in the result, and the
  replay matches), `SelfTest.AutoResolveNamedMen`.

### L3: Loss that is felt
- **The report names the fallen.** `MapScreen.Report.cs` gets a FALLEN block first: name, troop, battles served, and the
  deed he was known for. Then the wounded.
- **The roll of the dead.** Dead men leave the roll and go into `CompanyRecord.Fallen` (all of them, never trimmed; one
  short line each). It is shown in a new section of the company panel and read by the end summary.
- **Losing a veteran hurts the company.** When a named man dies, the company loses cohesion (`FallenCohesion`, *(tune: 1.5)*)
  and, for one battle, a little morale. A short burial choice after the battle (a new event, like `bury_dead`, which today only fires on big grave fields) names
  him and gives some of it back.
- The chronicle (`Record.cs`) gets a line when a man with 10+ battles falls.
- **Tests:** `SelfTest.FallenRemembered` (the dead move to Fallen, the cohesion loss applies, and the report lists them).

### L4: Rising from the ranks
- **Sergeants come from the roll.** When a company needs a sergeant, the named man with the most battles takes the post
  (his name is kept, and his traits come from his deeds). `WithOfficer` only makes one up when the roll is empty.
- This joins up the chain the game already has: **named man -> sergeant -> companion (15 battles, F3) -> division
  leader**. A companion's record keeps the line "rose from the ranks of the X company, day N".
- When a companion falls, he goes into his old company's Fallen list.
- The division panel (V) shows which company each leader came from.
- **Tests:** `SelfTest.RisesFromRanks` (the sergeant is the senior named man, then a companion keeps his history).

### L5: The banner shows its history
- **Named battles.** A battle gets a name from the nearest settlement or feature ("the Red Ford", "Hollin Hill"). It is
  generated in `Campaign/Ground.cs` and stored in `LedgerEntry.Name`, and the report and chronicle use it.
- **Battle honours on the banner.** Notable wins (outnumbered, a lord killed, a wall taken) add the battle's name to the
  company's `BattleHonours` (kept forever). The banner sprite gets one streamer per honour (up to 3) and a fringe for 10+
  battles. It's drawn in `SpriteSheet.cs` and `Puppet.cs`, and visible at 60 m (DESIGN.md pillar 1).
- **Losing the banner.** If a company's bearer falls and the enemy holds the ground at the end, the company loses its
  newest battle honour, and you can win it back by beating that enemy's realm in a later battle. Use with care: test that
  it feels fair *(tune)*.
- **Tests:** `SelfTest.BattleNames`, `SelfTest.BannerHonours`, and a visual check with `IRONFIELD_SHOTS`.

### L6: Fame brings men and work
- **Company fame** = battles + honours + battle honours, worked out from the record (not stored).
- Fame changes three things: recruits ("men come to serve with a famous company": better starting XP and more volunteers
  at towns of the company's home culture), contracts (famous companies get offered better ones), and how lords treat you
  (opinion).
- The career ladder stays, and each rank's reward text is reworded around the company ("land for your men",
  "a hall for the company").
- **Tests:** `SelfTest.FameDraws` (a famous company gets better recruits and offers than a new one with the same gold).

### L7: How the story ends
- **A new ending that counts as a win: the company's legend.** Once the company is old enough *(tune: 3 battle honours and
  a company with 40+ battles)*, you can **retire** (the captain hangs up the sword). This writes the company's chronicle as
  the end summary.
- **Succession (optional).** When retiring, or if "the captain can die" is turned on, the senior companion who rose from
  the ranks takes command, and the campaign goes on with the same company. Off by default, and the existing wounded/
  captured rules apply when it's off.
- **The end summary is rewritten around the company.** It covers the companies, their battle honours, the roll of the
  dead (the count, plus the five longest-serving dead by name), and who rose from the ranks. Realm share comes last.
- The existing marshal/king win stays, and its text says what it gave the company.
- **Tests:** `SelfTest.RetireAndSucceed` (the summary lists the company's dead, the successor keeps the company, and the
  save round-trips).

### L8: Fighting for your men (battle polish)
- **Battle goals about your men** (`BattleDeeds.cs`): "Bring Aldric home", "Hold the line for the Grey Company", next to
  the existing ones.
- **Reach a fallen man.** A named man who goes down wounded near you can be pulled back (stand over him for 2 s, the same
  idea as the last stand). This turns a death into a wound. It's limited to men within reach, so it stays a choice.
- **Named men speak.** A short toast when a veteran falls or does a deed ("Aldric is down!").
- **Tests:** `SelfTest.PullBack` (the fallen man is saved as wounded, and the replay matches).

## 5. Build order and why

1. **L0, L1, L2, L3** first: these make the fantasy true. Without names and felt loss the rest is decoration.
2. **L4** next: cheap, and it ties the existing sergeant, companion and division chain into the story.
3. **L5, L6**: make the history visible and useful.
4. **L7, L8**: endings and polish, once playtesting shows the roll matters to players.

Risks: (a) the roll may make players save-scum or auto-resolve. L2's auto-resolve rule and the L3 burial event soften this,
so watch `--balance` for an auto-resolve rate change. (b) Replays: named men must be placed and tracked with the battle rng
only. (c) Save growth: Fallen is never trimmed but is one short line a man, so check the save size after a 240-day
`--realms` run.

## 6. What the fantasy does to existing systems

| System | Effect |
|---|---|
| Companies, honours, ledger, promotion | **Core.** Extended by L1-L5. |
| Sergeants, companions, divisions | **Core.** L4 makes them the company's own veterans. |
| Captain combat, banner, last stand, duels | **Core.** You fight in the line beside your men, and L8 adds fighting *for* them. |
| Terrain, sight, couriers, AI general | **Support.** Good ground is how you keep men alive. Keep, no new work. |
| Career ranks, fiefs, council, crown | **Support.** Reworded around the company (L6, L7). No new political depth for now. |
| Diplomacy, realm AI, snowball guard | **Background.** It keeps the war going around you. Keep stable, no new features. |
| Side content (sites, caravans, tournaments, monsters, undead) | **Optional.** Good where it gives the company a story (a monster hunt earns a battle honour). No new work unless it does. |

## As built

- **L0:** the pitch and test are `docs/DESIGN.md` section 0 and a line at the top of the README's campaign section.
- **L1:** `Campaign/Roll.cs`. Only your own companies keep a roll (AI parties do not). A man's name is a first name of the
  company's culture (`men` per culture in `Data/names.json`) and an epithet, or "of" the company's home. The roll draws on
  its own stream (`CampaignState.RollRng`) and ids (`NextSoldier`), so the campaign's main draws and ids are unchanged.
  In every battle, fought or auto-resolved, each fit named man shares his stack's odds (killed and lost scaled by
  `VeteranSurvival`), until L2 gives fought battles real fates. Deeds come from what the company did (wall taken, horse
  killed, bloodied, came home from a loss, front rank), not yet from the man himself (L2). "First of the company made X"
  names a man on a promotion to a new top tier. `SyncRoll` (after each battle and daily) keeps the roll true to its stacks:
  surplus hurt men heal, and surplus fit men (desertion, hunger) leave for the fallen as "left". Merged companies keep
  every name even past the cap. A disbanded company's roll goes with it.
- **L2:** `Rules.Army` hands each company's fit named men to the battle (`Company.Named`, `NamedSpec`); `Battle.AddSquad`
  marks the first unit of each one's troop (`Unit.SoldierId`), so he stands in its front rank. Every unit now counts its own
  `Kills` and `FelledLeader` (in `Battle.Kill`). `Battle.Result` returns `CompanyResult.Named` (each man's kills, fell,
  fled) and the best unnamed man (`BestKills`, `BestTroop`). `Campaign.RollFought` applies them: one who fell is wounded,
  not killed, more often than his stack (killed odds times `VeteranSurvival`), and may be taken on a lost field; one who
  fled from a lost field may be missing. 5+ kills or a captain cut down is a deed. The company's best man earns a name for
  4+ kills or a captain. Auto-resolve hands back no `Named`, so the L1 shared odds apply. The HUD (`Hud.NamedMarks`) puts
  a small gold mark over named men within 25 m (all of them in command view) and shows the name within 10 m or under the
  cursor. Tests: `NamedMenFight` (placing, fates, kills, replay), `AutoResolveNamedMen`.
