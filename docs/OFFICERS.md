# Officers and sergeants (OF0-OF7)

The player's gripe (2026-11-16): you can't tell what sergeants do, they act on their own, and they are fiddly to use.
This plan replaces the eleven division goals with two plain layers, the same for both sides:

- **Company sergeant** (every company): keeps his company in good order. Never changes its orders.
- **Officer at a post** (a few per army): you put him on the front line, a wing, the skirmishers or the reserve and give
  him a stance. He does what the stance says and nothing else. **His skill decides how well**: a poor officer on the
  enemy's line can lose it badly, and that is something to see and use.

Status: OF0-OF7 built (2026-11-17). Supersedes the goal layer of `docs/SERGEANTS.md` (SG0-SG7); the command cap,
reserve tray, call-ups and horn plans stay.

## Decisions
- No riders and no order delay for now (OF0). Orders land at once. Call-ups from the reserve still take their march.
- The AI uses the same posts and stances; its general picks stances, its officers carry them out with their own skill.
- Horn plans stay: a stance kept back until the horn.

## OF0 Orders land at once
- `Battle.OrderDelay` is 0: goals, direct orders and horn plans reach a division at once; no couriers ride.
- Sergeants no longer ask questions (SG3) or decide them for you; the card still reports what the division is doing.
- The AI's own goal changes stay until OF3 replaces them.

## OF1 Posts and stances (the sim)
- `Post` { Front, LeftWing, RightWing, Skirmish, Reserve } replaces `GoalKind` on a division (`Division.Post`, `Stance`).
- Stances by post:
  - Front: Hold (stand on the line), Advance (walk forward in order, stop at contact), Charge (go in).
  - Wing: Guard (cover the front line's flank), Swing (go round the enemy's flank, then in), Charge.
  - Skirmish: Loose (shoot, fall back from foot that comes close), Harass (keep at three-quarters range, pick soft targets),
    Screen (stand in front of the line and give way when it closes).
  - Reserve: Wait (behind the line), Plug (fill a gap or a breaking company in the line), Follow (stay by the captain).
- Where a post stands is worked out from the army, not set by hand: the front line's centre and width, wings beside its
  ends, skirmishers ahead, reserve behind.
- An officer never changes his stance himself: no charging because a foe came close, no committing the reserve on a
  timer. Men at blows still fight what is in front of them (the companies' own melee).

## OF2 Skill decides how well (both sides)
An officer's skill (0-10: a companion's Command, a sergeant's battles, an enemy lord's army quality) changes:
- **Reaction:** how often he looks again (about 2 s at skill 0, 0.4 s at 10).
- **Notice:** how far out he sees a threat to his flank or horse coming (Guard and Hold lock shields, Wing turns to meet it).
- **The line:** a good officer keeps his companies shoulder to shoulder and closes gaps; a poor one lets them drift apart
  and leaves gaps a flank can come through.
- **Together or piecemeal:** on Charge or Advance a good officer sends his companies in together; a poor one sends them
  in one after another.
- **Rotation and rallying:** only a good officer pulls a spent company back and puts a fresh one in.
- His company wits lift (SG4) stays. Natures (SG7) change how well, not what: Hothead is quicker to go in on Charge,
  Stubborn holds longer on Hold, Drillmaster keeps the line tighter.

## OF3 The AI on posts
- `Armies.Divide` lays the enemy army out on the same posts with officers whose skill comes from the army's quality.
- `AiCommand` picks stances by post and the state of the fight (Hold then Charge, wings Guard then Swing, reserve Plug).

## OF4 The command view
- In deployment each post is drawn on the field as a marked zone. Drag companies onto a post; AUTO-FILL fills them.
- An officer's card reads in one line: post, stance, what is happening ("LEFT WING · GUARD · enemy horse 60 m").
- Stances on number keys with the officer picked. Horn plans as before.

## OF5 Company sergeants
- Every company's sergeant (the named man from the roll, else its best man) keeps its order: dresses the ranks, re-forms
  after a charge, rallies wavering men, holds back men straining to break ranks.
- The card shows what he is doing now (dressing ranks, rallying, holding them back).
- When he falls the company fights a grade worse until another man steps up.

## OF6 The campaign
- `DivisionRecord` keeps a post and stance instead of a goal name; old saves map their goal to the nearest post.
- The party screen's lanes become the posts.

## OF7 Polish
- Shots and judging, the F1 help, README rules, tips; `--balance` and `--agree` within noise of before.

## As built
- OF0 (2026-11-16): `OrderDelay` returns 0; questions dropped.
- OF1 (2026-11-16): `Post`, `Stance` and `Goal` (the pair) in BattleDivision.cs; the officer's look by stance and `FrontLine`
  in BattlePosts.cs. Deviations: the AI keeps its old timing rules, put onto stances (Hold/Advance -> Charge, Guard -> Swing,
  Wait -> Plug), until OF3. Dropped with the old goals: Take (a spot you clicked), Attack on a company you clicked, Pursue,
  FallBack and the AI's ambush in a wood (`AmbushWood`, test `AmbushWaits`); the right button with an officer picked now
  gives his companies a direct order instead. The campaign still saves old goal names; `Goal.FromName` maps them (OF6
  replaces them). The command view only swapped the goal buttons for stance and post buttons; OF4 redoes it.
- OF2 (2026-11-16): `BattleOfficers.cs`. Charge stagger is quadratic in skill (0.08 x (10 - skill)^2 s), so skill 9 is
  together and skill 5 already loose. Rallying is resting: a good officer pulls a spent company back to get its nerve and
  sends a fresh one into its place; the morale system still decides who runs. `FrontLine` now faces the way the side
  deployed, not centroid to centroid (a new or wandering company swung every post). Drift is a sideways random walk on the
  battle's own rng, so replays still match.
- OF3 (2026-11-16): `Armies.Split` lays armies on posts, `Armies.OfficerSkill` from tier and level, `AiStance` reads the
  fight. Test `AiOnPosts`. Simplification: skirmishers stay on Loose all battle (no switch to Harass or Screen yet).
- OF4 (2026-11-17): `Hud.Posts.cs`, `Battle.Situation`, `Battle.AutoFill`. Deviation: the front post is not a drawn zone
  (its companies stand where you put them, and a box round a spread-out battle covered the field); it is a tag, and you drop
  onto its companies. Cards are 214 wide; the situation drops "ENEMY" ("HORSE 125 M"); the officer panel heads its stances and posts apart, gold marks his stance and post now, the horn's plan is the word HORN; a front officer's tag sits on his own company. Visual judge (Haiku fallback; T3 not available): 6, 6, 6, 6, then 7/10 after five rounds. Left as polish: the horn word in gold, AWAITS HORN beside HORN: SWING on a card.
- OF5 (2026-11-17): `BattleSergeants.cs`. He keeps order by numbers, not by moving: re-forming 25 % quicker, urge falls
  faster, a rally aura of 2.5 morale a second in 10 m. A grade worse is 0.25 drill (`SergeantLoss`), given back when another
  man steps up (8-24 s by drill). The card's plate across the portrait's foot also carries RESTLESS (it wins; it replaced the loose RST text). Visual judge (Haiku fallback): 5, then 7/10.
- OF6 (2026-11-17): `DivisionRecord.Goal` holds "Post:Stance"; the `Campaign` constructor maps an old save's goal name with
  `Goal.FromName` (no save version bump). Each party lane (and each V card) has a post button and a stance button
  (`PostButtons`) in place of the eight-goal cycle. Deviation: lanes stay one per officer, labelled with his post, rather
  than one per post (two officers may share a post). Visual judge (Haiku fallback): 6, then 7/10; the post button takes 4/7 of the row so RIGHT WING fits.
- OF7 (2026-11-17): README (officers at posts, company sergeants, the AI general), the F1 help's command rows, the divisions
  tip. `--agree 4 5` against the baseline (7fc4c1b), higher tier winning by gap: 1 42% (was 50% of 24), 2 45% (55% of 20),
  3 63% (69% of 16), 4 33% (33%), 5 13% (0% of 8), 6 0% (0%): one or two battles a gap, within the sample's noise.
  `--balance` was not run (the user asked for quick runs only). Visual judging used the Haiku fallback; the T3 Hydrogen judge
  was not available in these sessions. Left as polish: the horn word in gold, "AWAITS HORN" beside "HORN: SWING" on a card.
