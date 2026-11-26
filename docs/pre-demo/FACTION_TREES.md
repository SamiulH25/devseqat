# Faction-Only Troop Trees, Subcultures, Kit & Art (plan)

Phase prefix: **FT**. Status: **Built (FT0–FT8 complete, 2026-11-26)**.

> [!NOTE]
> **As built (2026-11-26):**
> - Dropped all 10 universal `"common"` trunk troops (`levies`, `shieldmen`, `spearrunners`, `bowmen`, `outriders`, `lancers`, `billmen`, `acolytes`, `esquires`, `mounted_esquires`); **492 troops** remain across 7 realms, 16 subcultures, knight orders, bandits, and wild creatures.
> - Added per-culture `NobleFoot`, `NobleHorse`, and `MageRoot` resolvers (`Factions.cs`, `Campaign/Nobility.cs`, `Campaign/CampaignAI.cs`, `Troops.cs`, `Campaign/Camp.cs`, `MapScreen.Settlement.cs`) and `Factions.Open` subculture-kin awareness.
> - Built all 7 faction slices + 16 subculture trees (T1–T6), 19 new weapons, new helms/armors/shields, 9 new mounts, **283 modular 3D parts** (`Data/meshes/units3d.bin`, 54,448 tris), and baked 2D sprites (`Data/sprites/*.png`). Every faction (`FT1–FT7`) was visually audited by Hydrogen agents on 2D sprite sheets and 3D `IRONFIELD_TROOP_VIEWS` turnarounds with all reported fixes applied and verified.
> - Added legacy common-troop save migration in `CampaignState.Deserialize` (`Campaign/CampaignState.cs`), migrated `Data/origins.json`, `Origins.cs`, and 42 C# files, and updated `docs/CODEMAP.md`.
> - Verification: `tools/check.sh` passes all 5 static/build/codemap checks and **336+ of 351** self-tests (100% of troop-tree, culture, noble-recruit, sprite-bake, mage, tier-upgrade, stakes, and 120-day `CampaignRun` tests pass; the remaining ~14 failures in `combat-rework` are pre-existing combat-rework / drawn-map / bark tests from upstream `6c9d472`).

> [!IMPORTANT]
> **Verification & Visual Audit policy (owner decisions, 2026-11-26):**
> - `tools/check.sh` is **paused** until the entire `FT0–FT8` plan is complete. During faction slices (`FT1–FT7`), verify incrementally with `cd tools/gltf && npm run check`, `dotnet build -c Release`, `IRONFIELD_TROOP_VIEWS`, and `--sprites`. Run the full `tools/check.sh` + `--kit` / `--tiers 3` / `--agree` / `--realms` / `--pacing` / `--bench-3d` suite once in `FT8`.
> - **Hydrogen visual audit (owner exception to the no-visual-judge rule):** After each faction slice (`FT1–FT7`) generates its 2D sprite sheets (`--sprites`) and 3D in-game renders (`IRONFIELD_TROOP_VIEWS`), launch Hydrogen visual-auditing agents to critique silhouette, tier progression, faction identity, kit readability, and sprite/model parity before signing off on that faction.

## Goal & Owner Decisions (2026-11-26)

Drop the universal `"common"` recruitable troop tree (`levies`, `shieldmen`, `spearrunners`, `bowmen`, `outriders`, `lancers`, `billmen`, `acolytes`, `esquires`, `mounted_esquires`) so every realm and subculture has an unmistakable identity from Tier 1 through Tier 6.

1. **Subcultures (Option A):** Each of the 16 subcultures gets its own **dedicated 1–2 branch tree starting from its own T1/T2 subculture root** (no longer hanging off shared `levies`/`clansmen` or awkwardly jumping classes mid-chain).
2. **Cross-faction `kin` borrowing:** **Kept** where lore justifies it (e.g. slave/mercenary/shared-heritage links such as Qadir, Brinemere, Tarn, Naryn). Do not strip `kin` indiscriminately.
3. **Faction noble roots & lines:** Each of the 7 factions gets its own **named, visually distinct noble castle root(s)** and **full noble line(s)** (replacing generic `esquires` / `mounted_esquires` and closing the `NOBILITY.md` gap for Skar, Qadir, Naryn, and Brinemere).
4. **Faction mages:** Separate T2 mage initiate and progression line for each of the 7 factions (replacing common `acolytes`).
5. **New weapons within current mechanics:** All new weapons use existing `Grip` types (`One`, `Two`, `Pole`, `Lance`, `Bow`, `Crossbow`, `Staff`, `Thrown`, `Sling`) and `Data/gear.json` stats (`Fighter.Build`), backed by **new 3D models** (`tools/gltf/parts/*.mjs`) and **new 2D sprites** (`SpriteSheet*.cs`). Brand-new weapon combat mechanics are deferred post-demo.
6. **Faction-by-faction execution:** Deliver vertical slices one realm at a time (kit + 3D models + 2D sprites + troop tree + `factions.json`), then delete the old `common` trunk and run full validation in `FT8`.

### What stays non-faction (not removed)
- `captain` (T0 player/leader template)
- `hollow_squires` → `hollow_knights` → `hollow_lords` (The Hollow Crown, hidden factionless order)
- `sworn_brothers` → `sworn_masters` (Player's own founded knight order)
- `bandit` (`cutthroats` tree) and `wild` (creatures)
- `beast_hunters` (re-homed as a mercenary/camp/Holtmen specialist or kept as neutral specialist)

---

## Architecture & Pipeline Checklist (Per Faction Slice)

Every faction slice (`FT1–FT7`) touches the same five layers:

1. **Kit & Stats** — `Units.cs` (`Look.Heads`, `Bodies`, `Shields`, `Weapons`, `GripOf`) + `Data/gear.json` (stats on the material/skill ladder; `troop: true` where not sold to captain) + `Data/troops.json` (`mounts` if new mounts added).
2. **3D Models** — `tools/gltf/parts/{weapons,heads,bodies,mounts,props}.mjs` + `tools/gltf/parts.manifest.json` (triangle ratchet) + `Mesh3D.cs` / `Puppet3D.cs` mapping → verify with `cd tools/gltf && npm run check && npm run build-all`.
3. **2D Sprites** — `SpriteSheet*.cs` + `dotnet run -c Release -- --bake-sprites` → `Data/sprites/*.png`.
4. **Troop Trees & Realm Lines** — `Data/troops.json` (T1–T6 common, subculture, noble, mage) + `Data/factions.json` (`cultures` roots/noble roots/mage roots, `factions.lines`).
5. **Visual Check & Hydrogen Audit** — Capture 2D sprite sheets (`dotnet run -c Release --no-build -- --sprites /tmp/<faction>-sprites.png <parts...>`) and 3D in-game renders (`IRONFIELD_TROOP_VIEWS=<new_troops> IRONFIELD_SAVES=/tmp/ifs IRONFIELD_TROOPS=/tmp/troops xvfb-run -a -s "-screen 0 1280x720x24" dotnet run -c Release --no-build`), inspect PNGs directly, and launch Hydrogen auditing agent(s) on the rendered sheets to critique silhouette, faction identity, tier progression, and sprite/3D parity.

---

## Phases

### FT0 — Code & Data Decoupling (Infrastructure)
Prepare the codebase so factions can own their own roots, noble roots, and mage roots without breaking compilation while the old `common` troops still temporarily exist during `FT1–FT7`.
- **`Factions.cs` / `Data/factions.json`**: Add optional per-culture `NobleFoot`, `NobleHorse`, and `MageRoot` fields on `CultureDef` (falling back to existing defaults until each faction slice populates them). Support subculture `Root` troops directly in village recruitment.
- **`Campaign/Nobility.cs`**: `NobleTroop(settlement, mounted)` resolves from `Factions.Culture(s.Culture)` (falling back to parent realm culture) instead of hardcoded `"esquires"` / `"mounted_esquires"`.
- **`Campaign/Camp.cs` & `Campaign/Circle.cs` / `CourtMages.cs`**: Resolve camp/mage recruits from realm culture rather than `Culture == "common"` / `"acolytes"`.
- **`Units.cs` test shims**: Prepare to migrate `UnitType.Shieldman` / `Bowman` / `Spearrunner` / `Lancer` (kept aliased until `FT8` so intermediate builds stay clean).

---

### FT1 — Kingdom of Brevon (`azure`)
*Feudal monarchy, abbey steel, heavy foot & tourney lances.*
- **Main Roots (new)**:
  - Common T1: `brevon_fyrd` → T2 `manor_foot` (Inf), `shire_billmen` (Ski), `town_crossbows` (Arc, existing), `crown_squires` (Cav, existing)
  - Noble T3 Castle Roots: `brevon_esquires` (Inf → `foot_knights` / `lily_squires`), `tourney_squires` (Cav → `demi_lancers`)
  - Mage T2 Initiate: `vigil_novices` (Cas → `lectors` → `magisters` → `grand_magisters`)
- **Subcultures (dedicated T1 roots, 1–2 branches)**:
  - **Crownlanders (`azure_heart`)**: T1 `abbey_yeomen` → Branch 1 (Polearms: `royal_billmen` → `crown_halberdiers` → `crown_poleaxemen` T5 → `crown_vanguard` T6); Branch 2 (Heavy Foot: `vale_wardens` T4–T5).
  - **Marchers (`azure_march`)**: T1 `march_watch` → Branch 1 (Border Crossbows: `reiver_crossbows` → `march_arbalests` → `border_sharpshooters`); Branch 2 (Light Horse: `march_scouts` → `pass_reivers` → `march_riders`).
  - **Bayfolk (`azure_coast`)**: T1 `harbor_hands` → Branch 1 (Marines: `harbor_watch` → `bay_marines` → `harbour_guard` → `tide_wardens`); Branch 2 (Deck Crossbows: `bay_crossbows` → `deck_arbalests`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `poleaxe` (Pole, anti-armor knightly polearm), `goedendag` (Pole/Two, early militia), `boarding_axe` (One, Bayfolk).
  - *Helms/Bodies/Shields*: `hounskull` (visored bascinet), `kettle_bevor`, `oilskin_coat` (Bayfolk), `surcoat_mail`.
  - *Mounts*: `march_hobby` (agile border horse), `tourney_destrier` (full heraldic caparison + chamfron).

---

### FT2 — Vespra Compact (`crimson`)
*Patrician city-states, slashed-cloth duellists, halberds, longbows & Spine crag-clans.*
- **Main Roots (new)**:
  - Common T1: `civic_militia` → T2 `ward_swordsmen` (Inf), `guild_billmen` (Ski), `commons_bowmen` (Arc, existing), `hobilars` (Cav, existing)
  - Noble T3 Castle Roots: `signorial_cadets` (Inf → `sword_brethren` / `rose_blades`), `compagnia_squires` (Cav → `knights` → `cataphracts`)
  - Mage T2 Initiate: `hedge_apprentices` (Cas → `hedge_witches` → `war_witches` → `crimson_crones`)
- **Subcultures**:
  - **Vesprans (`crimson_patrician`)**: T1 `guild_levies` → Branch 1 (Civic Pikes/Halberds: `civic_pikemen` → `patrician_halberds` → `signoria_halberds`); Branch 2 (Duellists: `vespran_fencers` → `compact_duelists` → `forum_blades`).
  - **Cragfolk (`crimson_alpine`)**: T1 `crag_herdsmen` → Branch 1 (Mountain Skirmishers/Halberds: `crag_skirmishers` → `crag_halberdiers` → `summit_wardens`); Branch 2 (Highland Axes: `alpine_axemen` → `crag_cleavers` → `crag_axemen`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `estoc` (One/Two high-pierce duellist sword), `partisan` (Pole, winged civic polearm), `kriegsmesser` (Two, curved cleaver).
  - *Helms/Bodies/Shields*: `slashed_doublet` (puffed/slashed renaissance silhouette), `close_helm` (fluted), `crag_plaid` (alpine mantle), `rotella` (steel buckler/targe).
  - *Mounts*: `crag_cob` (sturdy highland mount).

---

### FT3 — The Skarlands (`northern`)
*Fjord raiders, round-shield walls, throwing axes, taiga hunters & bear-cults.*
- **Main Roots (new/refined)**:
  - Common T1: `clansmen` (now Skar-only!) → T2 `axemen`, `raiders`, `spear_karls` (replaces `spearrunners`), `hunters`, `fell_riders`
  - Noble T3 Castle Roots (fills `NOBILITY.md` gap!): `hearth_youths` (Inf → `hearth_thegns` T4 → `einherjar` / `thane_champions` / `wolf_thegns`), `jarl_outlaw_riders` / `storm_squires` (Cav → `thane_riders` T4 → `storm_riders` / `thunder_lords`)
  - Mage T2 Initiate: `rune_apprentices` (Cas → `rune_singers` → `skalds` → `galdr_masters`)
- **Subcultures**:
  - **Fjordmen (`northern_fjord`)**: T1 `fjord_oarsmen` → Branch 1 (Raiders: `longship_raiders` → `sea_huscarls` → `fjord_jarls` → `longship_huscarls`); Branch 2 (Shield-Breakers: `fjord_axemen` → `prow_guards`).
  - **Holtmen (`northern_taiga`)**: T1 `taiga_trappers` → Branch 1 (Frost Archers: `frost_hunters` → `snow_stalkers` → `taiga_rangers` → `snowfield_hunters`); Branch 2 (Bear-Cult / Boar-Spears: `bear_cultists` → `holt_berserkers`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `throwing_axe` (`Thrown` francisca skirmisher weapon), `atgeir` (Pole, Nordic hewing spear), `seax` (One, fast sidearm), `boar_spear` (Pole, `ThrustOnly` vs monsters/cav).
  - *Helms/Bodies/Shields*: `vendel_helm` (crested spectacle helm), `bear_pelt` (berserker mantle), `byrnie_fur`, `round_rimmed` (painted bossed Viking shield).
  - *Mounts*: `fjord_pony` (agile shaggy northern horse).

---

### FT4 — Emirates of Qadir (`southern`)
*Oasis dynasties, desert camels, curved blades, javelins, gilded Mamluks & Maren corsairs.*
- **Main Roots (new)**:
  - Common T1: `oasis_levy` → T2 `qadir_foot` (Inf), `qadir_spears` (Ski), `slingers` (Arc, existing), `camel_scouts` (Cav, existing)
  - Noble T3 Castle Roots (fills `NOBILITY.md` gap!): `ghulam_foot` (Inf → `qadir_champions` T4 → `dervish_blades` / `emir_guard`), `mamluk_pages` (Cav → `mamluks` T4 / `falcon_riders` → `sun_lancers` → `emir_mamluks`)
  - Mage T2 Initiate: `star_students` (Cas → `star_readers` → `astrologers` → `sages_of_the_sun`)
- **Subcultures**:
  - **Qadiri (`southern_oasis`)**: T1 `oasis_spearmen` → Branch 1 (Garden Guard: `river_guards` → `oasis_champions` → `canal_guard`); Branch 2 (Oasis Polearms: `palm_halberdiers` → `citadel_polearms`).
  - **Rimal (`southern_dune`)**: T1 `dune_nomads` → Branch 1 (Camel Skirmishers/Lancers: `dune_skirmishers` → `sand_raiders` → `dune_lancers` → `mirage_riders` → `dune_raiders`); Branch 2 (Desert Bows: `dune_archers`).
  - **Marenites (`southern_rift`)**: T1 `rift_sailors` → Branch 1 (Corsair Boarders: `rift_marines` → `corsair_captains` → `rift_boarders`); Branch 2 (Galley Bows: `galley_bowmen` → `corsair_archers`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `shamshir` (One, deep-curved sabre), `tabarzin` (One, crescent saddle axe), `cane_lance` (Lance, long light desert lance), `jarid` (`Thrown` light javelin bundle).
  - *Helms/Bodies/Shields*: `kulah_aventail`, `kazaghand` (quilted silk over mail), `corsair_vest`, `dhal_gilded`.
  - *Mounts*: `war_camel` (armored camel with barding!), `desert_arabian` (fastest gallop/turn rate).

---

### FT5 — Naryn Khaganate (`steppe`)
*Open-grass horse archers, wolf banners & Salt Sea lamellar cataphracts.*
- **Main Roots (new)**:
  - Common T1: `steppe_herdsmen` (splits off Skar's `clansmen`!) → T2 `yurt_foot` (Inf), `grass_skirmishers` (Ski), `herd_boys` (Arc, existing), `herd_riders` (Cav, existing)
  - Noble T3 Castle Roots (fills `NOBILITY.md` gap!): `noyan_foot_youths` (Inf → `torguud_nobles` T4 → `noyan_guard`), `kheshig_youths` (Cav → `kheshig_riders` T4 / `sky_riders` → `noyan_bows` / `khans_lancers` → `kheshig`)
  - Mage T2 Initiate: `sky_novices` (Cas → `drummers` → `wind_callers` → `sky_callers`)
- **Subcultures**:
  - **Free Banners (`steppe_horde`)**: T1 `banner_riders` → Branch 1 (Horse Archers: `banner_outriders` → `steppe_horse_bows` → `horde_champions`); Branch 2 (Steppe Lancers: `free_lancers` → `banner_lancers`).
  - **Saltmen (`steppe_salt`)**: T1 `salt_watch` → Branch 1 (Heavy Cataphracts: `salt_scouts` → `salt_lancers` → `salt_cataphracts` → `brine_cataphracts`); Branch 2 (Port Infantry/Skirmishers: `salt_skirmishers` → `salt_guards`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `steppe_sabre` (One, horseman sabre), `hooked_lance` (Lance, steppe kontos), `shestoper` (One, flanged mace), `steppe_glaive` (Pole, curved poleblade).
  - *Helms/Bodies/Shields*: `steppe_plumed_helm` (horsetail crest), `lacquered_lamellar`, `heavy_salt_lamellar`, `kalkan_bossed`.
  - *Mounts*: `salt_cataphract_charger` (heavy lamellar barding), `khan_steed`.

---

### FT6 — League of Brinemere (`league`)
*Merchant republic, pike phalanxes, standing-pavise arbalesters, condottieri & fen smugglers.*
- **Main Roots (new)**:
  - Common T1: `charter_watch` → T2 `canal_foot` (Inf), `arsenal_spears` (Ski), `militia_crossbows` (Arc, existing), `light_horse` (Cav, existing)
  - Noble T3 Castle Roots (fills `NOBILITY.md` gap!): `guild_ensigns` (Inf → `senate_cadets` T4 → `senate_guard` → `doge_guard`), `condottieri_squires` (Cav → `condottieri` T4 → `guild_lances` → `doge_lances`)
  - Mage T2 Initiate: `lab_apprentices` (Cas → `apothecaries` → `alchemists` → `guild_alchemists`)
- **Subcultures**:
  - **Brinemen (`league_lagoon`)**: T1 `lagoon_hands` → Branch 1 (Arsenal Pikes/Marines: `galley_pikemen` → `guild_marines` → `merchant_captains` → `arsenal_guard`); Branch 2 (Lagoon Crossbows: `lagoon_crossbows` → `arsenal_arbalesters`).
  - **Fenmen (`league_fen`)**: T1 `fen_skirmishers` → Branch 1 (Slingers & Pavise Bows: `fen_slingers` → `fen_pavisiers` → `reed_stalkers` → `pavise_bowmen`); Branch 2 (Marsh Spears: `marsh_spears` → `fen_wardens`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `schiavona` (One, basket-hilt Venetian broadsword), `marsh_fork` (Pole, Fenmen trident/fauchard), `stradiot_lance` (Lance, double-ended light lance), `windlass_arbalest` (Crossbow, heavy republic siege bow).
  - *Helms/Bodies/Shields*: `cabasset` (pear-stalk helmet), `reed_cloak` (Fenmen marsh mantle), `gilded_brigandine_cape`, `standing_pavise` (tall rectangular shield).
  - *Mounts*: `stradiot_courser` (agile light horse), `condottiere_charger`.

---

### FT7 — Chapter of Tarn (`order`)
*Austere grey-cloaked warrior-monks, anti-monster warhammers & fortified Pass-Wardens.*
- **Main Roots (new)**:
  - Common T1: `lay_brothers` → T2 `chapter_foot` (Inf), `pass_spears` (Ski), `pilgrim_bows` (Arc, existing), `pilgrim_riders` (Cav, existing)
  - Noble T3 Castle Roots: `vow_postulants` (Inf → `hammer_brothers` / `sun_brothers`), `squire_brothers` (Cav → `knight_brothers` → `sun_knights` → `ashen_knights`)
  - Mage T2 Initiate: `choir_novices` (Cas → `chaplains` → `battle_priests` → `saints_militant`)
- **Subcultures**:
  - **The Sworn (`order_chapter`)**: T1 `chapter_novices` → Branch 1 (Chapter Foot: `vow_sergeants` → `ashen_wardens` → `sun_paladins` → `chapter_sergeants`); Branch 2 (Hammer Brothers: `iron_penitents` → `crusader_hammers`).
  - **Pass-Wardens (`order_warden`)**: T1 `gate_militia` → Branch 1 (Pass Halberds: `pass_halberds` → `gate_champions` → `canyon_halberds`); Branch 2 (Canyon Crossbows: `canyon_crossbows` → `pass_arbalests`).
- **New Kit (3D + 2D + `gear.json`)**:
  - *Weapons*: `lucerne_hammer` (Pole, anti-armor polehammer), `great_maul` (Two, heavy crushing hammer), `iron_flail` / `heavy_mace` (One, anti-undead blunt).
  - *Helms/Bodies/Shields*: `monastic_cowl_mail` (grey hood over mail), `austere_greathelm` (unpainted iron, no gilt), `grey_plate_surcoat`.
  - *Mounts*: `chapter_iron_charger` (grey cloth + dark steel chanfron).

---

### FT8 — Remove `"common"` Trunk, Migrate References & Full Verification
Once all 7 realms stand completely on their own feet:
1. Delete the old common trunk troops (`levies`, `shieldmen`, `spearrunners`, `bowmen`, `outriders`, `lancers`, `billmen`, `acolytes`, `esquires`, `mounted_esquires`) from `Data/troops.json`.
2. Add a clean save-migration map in `Campaign/Saves.cs` (mapping old common troop IDs to the settlement/company's realm equivalent so existing saves still load).
3. Update `Units.cs` test shims (`UnitType.Shieldman` etc.), `Data/origins.json`, `Data/events.json`, `BattleScreen.Shots.cs`, `AnimSheet.cs`, `Campaign/TesterSaves.cs`, and `Tests/*`.
4. **Re-enable and run the full verification & balance suite**:
   - `tools/check.sh` (must end with `ALL CHECKS PASSED`)
   - `dotnet run -c Release --no-build -- --kit`
   - `dotnet run -c Release --no-build -- --tiers 3`
   - `dotnet run -c Release --no-build -- --agree 4 5`
   - `dotnet run -c Release --no-build -- --realms 8`
   - `dotnet run -c Release --no-build -- --pacing 8`
   - `dotnet run -c Release --no-build -- --bench-3d 1500`
   - `tools/codemap.sh` + update `CLAUDE.md` / `docs/CODEMAP.md` / `docs/TROOPS.md`.
