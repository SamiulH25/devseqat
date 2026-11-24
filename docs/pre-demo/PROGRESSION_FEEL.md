# Progression Feel & Character Impact (unified plan)

Owner's brief (2026-11-23):
1. **Make players want and need to interact with the already-existing systems** — no new mechanics for their own sake. Right now Camp, Named Men/Roll/Natures/Sergeants/Cairns, Arcane Camps/Rites/Circles/Wells/Workings, Knight Orders/Aura/Black Flame, Training Yards/Barracks, Smithing/Kit, Duels, Divisions, Contracts, and Fiefs all exist, but a player can ignore almost all of them and still win.
2. **Total War scale, not Mount & Blade caps:** Keep generous army/company scale (`CompanySlots`, `MenLimit`). Progression and friction come from **economy/upkeep, recruitment & training infrastructure, army cohesion/command, and battlefield counters** — not artificial tiny party-size caps.
3. **Homeland != Sworn Sword:** Choosing a faction in the character creator makes you **born of and favoured in that Homeland** (culture, warm lord/village standing, starting province), **not** immediately on an 85% realm payroll on Day 1.
4. **Named men affect battles:** Soldiers earning names, Natures, chosen Sergeants, Armed veterans, and Mentioned heroes ("of the banner") must tangibly swing battles and company steadiness.

> Merged `CHARACTER_IMPACT.md` into this plan on 2026-11-23 so Homeland, Birth, Past, and Road act as direct on-ramps into the game's existing systems.

---

## Status (2026-11-23)

- **PF1** built: `CaptainState.Homeland` & `HomelandRegard` (+12 standing lord opinion, 15% homeland village recruit discount); creator starts independent (`Contract = -1`, homeland palette except `deserter`); `FreeContractPay = 0.35` until earned `SwornSword` (`SwornDays = 14` + `SwornDeeds = 3`); scaled `Ditch Wolves`; `CampTier = 2`, `CastleYardTier = 4`, `NobleRenown = 140`.
- **PF2** built: recurring origin hooks across `Rites`, `Oath`, `Smith`, `Goods`, `Ships`, `Fiefs`, `Nobility`, `Campaign.Daily/Speed`, `CampaignBattle`, `Duels`, `Talk.Greeting`, plus saved `CaptainState.Road` -> `BattleOptions.CaptainRoad` combat signatures.
- **PF3** built: `NamedSpec` carries `Armed`, `Banner`, `Battles` into battle (scaling HP/damage, Armed +4 armour/+15% dmg/+12% HP, company morale & `BattleUrge` steadiness); stronger `nature_*` effects & `NatureWeight = 0.05`; `SergeantPickDays = 5`; lower free town cohesion cap (`12`, `18` with Merry); active Division officers boost wing combat (+5%), discipline (`TemperOf -0.12`), and morale reach.
- **PF4** built: stronger `bless_rite`, higher `TideWeight = 0.75`, stronger pre-battle `Duels` (`DuelMorale = 20`, `DuelRenown = 15`, `DuelEdge = 1.15`) and `tourney`/`hedge_knight` duel leverage; `cloister`/`pilgrim` Day-1 Faith Camp Rites.
- **PF5** partial: `TrainTab` now surfaces stat/role promotion jumps and `Inventory` shows discounted Smith prices; full `--tiers` / `--pacing` / `--realms` balance pass remains next.

### Handoff Notes (leaving off 2026-11-23)

**Design philosophy locked in with owner:**
- **Zero new mechanics for their own sake** — every change couples existing systems (Camp, Named Men/Roll/Natures/Sergeants/Cairns, Arcane Rites/Circles/Wells, Knight Orders/Aura/Black Flame, Training Yards/Barracks, Smithing, Duels, Divisions, Homeland/Origins) into the progression loop so players have a concrete reason to engage with them.
- **Total War scale, NOT Mount & Blade caps** — keep generous `CompanySlots` and `MenLimit`. Friction comes from economy/upkeep (`FreeContractPay` until earned `SwornSword`), recruitment & training infrastructure (`CampTier = 2`, `CastleYardTier = 4`, `NobleRenown = 140`), company cohesion/command, and tactical counters.
- **Homeland != Sworn Sword** — picking a faction in the creator sets `Captain.Homeland` (warm lord standing + 15% village recruit discount + homeland palette) while starting independent (`Contract = -1`, no Day-1 85% payroll).
- **Named men directly affect battles** — veterancy, `Armed` gear, `OfBanner` mentions, `nature_*` effects, and Division officers all tangibly move HP, damage, armour, morale, and `BattleUrge` steadiness.

**What to do next on the other device:**
1. **Finish PF5 balance & verification:**
   - Run `dotnet run -c Release --no-build -- --tiers 3`, `--pacing 4`, `--balance 4`, and `--realms 4` to check opening climb (Days 1–20 without levies), tier jump clarity across Tiers I–VI, and mid/late gold sinks (upkeep, horses, Smith marks, Camp Rites/Feasts, Fief works).
   - Check `MapScreen.Town` / `MapScreen.Party` / `MapScreen.Camp` UI shots (`IRONFIELD_TOWN_ONLY=1`, `IRONFIELD_CAMP_ONLY=1`) to verify the new `TrainTab` stat-jump lines and `Inventory` Smith discount labels don't overflow.
2. **Optional polish on PF2/PF4:**
   - Expand single `day>=14` origin events into 2–3 beat personal arcs once `EVENTS.md` storylets are underway.
   - Audit enemy Court Mages / Order Champions in mid/late battles to ensure `bless_rite`, Wells (`TideWeight = 0.75`), Circle counterplay, and Duels feel decisive without making mundane generals unviable.
3. **Note on pre-existing `demo-sprint` test failures (unrelated to PF1–PF4):**
   - Full `--selftest` had 7 pre-existing failures from earlier dirty changes on `demo-sprint`: `TroopTree` (longbowmen/yurt_guards), `UrgeLunges`, `Sites`, `Roads`, `FormsWork`, `SaveMidRun` (`NextItemId` mismatch on load), and `LordArmies`. All PF1–PF4 targeted tests pass.

## Phases

### PF1. Homeland, Free Company Opening & Earned Progression Spine
- **Homeland vs. Contract:** Add `CaptainState.Homeland` (saved). Character-creator campaigns start **independent** (`Contract = -1`, true Free Company) in their Homeland with warm homeland favour (lord opinion bonus, homeland village recruit discount, warm tavern/steward welcome — flipped to suspicion for `deserter`/`outlaw`).
- **No free Day-1 85% subsidy:** Signing a realm contract as a `Free Company` pays a modest retainer (`0.35` of upkeep); earning **`Sworn Sword`** unlocks the full `ContractPay` (`0.85`) subsidy and half-price realm recruits.
- **Earned `Sworn Sword`:** Reaching `Sworn Sword` requires real service (days in service **and** completed contracts or battles won for the realm), not passively waiting 30 idle days.
- **Opening without levies:** Scale the opening `Ditch Wolves` band to your starting force so a 0–6 specialist start isn't ambushed by 27 levies on Day 1, and tune starting purse/recruiting so week 1 builds smoothly into a real Total War band while upkeep pushes you into contracts and fights.
- **Infrastructure & Noble gates:** Lower `CampTier` (`3 -> 2`) and `CastleYardTier` (`5 -> 4`) so **Town Training Yards** and **Fief Barracks** are required for Tier III–VI promotions. Raise `NobleRenown` (`60 -> 140`, or Vassal rank) so noble troops are a real mid-game unlock.

### PF2. Origin Leverage & Day-to-Day Character Impact (Homeland, Birth, Past, Road)
- **System On-Ramps per Birth & Past:** Give every Birth and Past a recurring day-to-day mechanical hook on an existing system:
  - `cloister` / `pilgrim`: Faith Camp Rites (`bless`, `poultices`) usable early + faster shrine/Order standing.
  - `forge`: free Smith `Mend` and discounted `Improve` / `Remark`.
  - `guild` / `steward` / `shore`: better `Goods` trade margins (`guild`), cheaper Fief works & lower upkeep drag (`steward`), cheaper sea-lane fares (`shore`).
  - `drillmaster` / `camp` / `sellsword`: faster named-man rising, stronger camp drill & cohesion recovery.
  - `surgeon`: faster field wound recovery and higher fallen survival.
  - `poacher` / `marcher` / `outlaw` / `deserter`: woods/marches foraging & scouting, better loot/fences, ambush edges.
  - `highborn` / `disinherited`: earlier noble recruit access and house opinion memory.
  - `tourney` / `hedge_knight`: stronger pre-battle Duel morale/renown impact and earlier lordly duel acceptance.
- **World & NPC Reactions:** Lords, stewards, tavern keepers, and companions acknowledge your Homeland, Birth, Past, and Road.
- **Road Identity (`sword`, `lance`, `bow`, `great`, `pole`):** Distinct captain battle/duel identity beyond starting weapon.

### PF3. Named Men, Sergeants, Natures, Camp & Divisions in Battle
- **Named Men & Sergeants directly affect battles:**
  - Raise `NatureWeight` and make the senior/sergeant's **Nature** (`Steady`, `Bold`, `Grim`, `Merry`, `Pious`, `Sly`, `Proud`, `Gentle`) produce real, felt combat and campaign effects (not 1%).
  - Companies with a **named Sergeant / named veterans** hold formation better (lower `Urge` lunge/break-ranks risk, higher starting morale, better `Drill`/Guard); raw unled companies are noticeably more brittle.
  - **Armed** named men (`RollChoices.cs`) and **Mentioned** heroes (`"of the banner"`) visibly strengthen their company's combat edge, survival, and Cohesion.
  - Give `ChooseSergeant` a real decision window (don't silently auto-pick after 1 day before the player opens Camp).
- **Camp (`C`) & Real Attrition:** Reduce free passive town cohesion auto-recovery and passive field healing slightly so **Camp**, **Feasts**, **Arming named men from looted `Goods`**, **Mentioning**, **Pensioning veterans**, and **Honouring Cairns** (`Remembered.cs`) are active campaign-layer tools.
- **Divisions (`Divisions.cs`):** Reward grouping companies under a **Companion or "of the banner" Sergeant** with real division-wide morale, drill, and order responsiveness in big battles.

### PF4. Arcane Camps, Circles, Wells, Aura, Knight Orders, Smithing & Duels
- **Camp Rites (`Rites.cs`) & Workings (`Workings.cs`):** Surface and tune `poultices`, `bless`, `mend_core`, `Calm the dead`, and `Blight` so they directly answer real campaign attrition, undead grave fields, and hard sieges.
- **Arcane Counterplay (mundane generals remain viable):** Mid/late rival lords' **Court Mages** (`CourtMages.cs`), **Order Champions**, and **Monsters/Undead** genuinely punish an unprepared army — countered either by fielding a **Circle + Wells (`TideEdge`)** OR by **Faith `bless`/warding, Knight Orders, Aura + Smith-marked gear, and winning pre-battle Duels (`Duels.cs`)**.
- **Aura Walls & Black Flame:** Make breaking through Aura Walls (via Order Trials, Duels, or the Black Flame pact) a felt mid/late power spike.

### PF5. Tangible Tier Jumps, Mid/Late Gold Sinks & Verification
- Audit `--tiers` and `--kit` across Tiers I–VI so promotions deliver clear battlefield role shifts; surface promotion gains clearly on Party/Camp screens.
- Balance mid/late sinks (elite upkeep, replacement horses, Smith masterwork/marks, Camp Rites/Feasts, Fief works) so wealth flows into army capability.
- Verify with `tools/check.sh`, `--pacing`, `--balance`, `--tiers`, and `--realms`.
