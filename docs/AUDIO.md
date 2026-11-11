# Audio overhaul: combat, world and adaptive soundtrack (plan AU0–AU6)

Replaces the 22.05 kHz mono prototype synthesiser and 8 short tunes with a 44.1 kHz stereo audio engine:
- **28-voice micro-sample acoustic bank** (`Data/audio/instruments.bank`, ~3 MB compressed, built offline by `tools/build_bank.py` from MIT-licensed `FluidR3_GM.sf2` + spectral sculpting; zero new NuGet packages).
- **68 hand-authored scores** (`Data/music/*.json`) streamed in 0.25 s stereo chunks through a single `DynamicSoundEffectInstance` (`AudioStreamer.cs`), keeping RAM under 15 MB and locking all 4 battle stems (`Deploy`, `Skirmish`, `Melee`, `Crisis`) sample-accurately through one shared stereo hall reverb.
- **96 procedural SFX cues and 16 looping ambient beds** (`AudioSynth*.cs`) at 44.1 kHz covering weapon×armour hits, ranged weapons, siege engines, captain close-foley, monsters/beasts/undead, magic traditions, division/order horns, map biomes, settlements, deeds and the character creator.
- **3,000-man spatial mix**: raised `Battle.Sounds` buffer with player protection, listener-distance priority culling, family caps, `AmbienceVolume` setting, and sidechain ducking on horns, alerts and cinematic beats.

Simulation invariants: `Battle` sound emission never touches `Battle.rng` (`SelfTest.ReplayExact` is unchanged), headless files never reference `Microsoft.Xna.Framework.Audio`, and `BattleScreen.Effects` blood/camera-kick/trample triggers use cue-category helpers so split combat cues keep their visual effects.

## Phases

### AU0. Engine, instrument bank and stereo streamer
- `tools/build_bank.py` -> `Data/audio/instruments.bank` (28 multi-zone acoustic & sculpted voices, 16-bit delta PCM + GZip).
- `AudioBank.cs` (loader, Hermite-interpolated voice renderer with velocity tone, ornaments and procedural fallback).
- `MusicScore.cs` (score DTOs, JSON loader for `Data/music/*.json`, window/bar renderer, schema validator).
- `AudioStreamer.cs` (background 44.1 kHz stereo chunk renderer feeding one `DynamicSoundEffectInstance`: 4-stem battle mix, crossfades, layered stingers, stereo reverb, soft limiter).
- Upgrade `Synth.Rate` to `44100`; update `Audio.cs` with priority-sorted spatial playback, sidechain ducking, `MusicDirector` context API, and `Settings.AmbienceVolume` (`Settings.cs`, `SettingsScreen.cs`).

### AU1. Combat weapons, ranged, siege and captain foley
- Weapon×armour melee cues (`SlashLight`, `SlashHeavy`, `BluntFlesh`, `BluntPlate`, `PierceHit`, `LanceImpact`, `LanceShatter`), blocks/parries (`BlockWood`, `BlockMetal`, `ParrySteel`, `ShieldSplinter`, `BashThud`), captain close-mic (`SwingOne`, `SwingTwo`, `SwingPole`, `RiposteRing`, `WeaponSwap`, `LowHpPulse`), ranged split (`BowLoose`, `LongbowLoose`, `CrossbowSnap`, `CrossbowSpan`, `SlingCrack`, `JavelinCast`, `ArrowShield`, `ArrowPlate`, `ArrowFlesh`), siege (`RamSwing`, `LadderHook`, `FirePotShatter`).
- Wire `BattleCombat.cs`, `BattleSiege.cs`, `BattlePlayer.cs`, `BattleCaptain.cs`; raise `Battle.Sounds` cap to 256 (player cues never dropped); keep `BattleScreen.Effects.cs` blood/shake/trample intact via `CueInfo`.

### AU2. Creatures, beasts, undead, magic and signals
- Creature & undead cues (`TrollRoar`, `TrollSweep`, `WolfHowl`, `WolfSnarl`, `SpiderChitter`, `WebSpit`, `WebPin`, `BearRoar`, `BearMaul`, `WyvernScreech`, `WyvernFlap`, `GoblinShriek`, `BoneHit`, `BoneCollapse`, `GhoulRasp`, `RaiseDead`).
- Magic tradition & aura cues (`GatherMinor`, `CastFaith`, `CastGaldr`, `CastFire`, `CastHex`, `CastNecro`, `WardHum`, `WardShatter`, `CrowSwarm`, `GreekFire`, `AuraIgnite`, `AuraBreakthrough`, `ChampionClash`).
- Order & signal horns (`HornCharge`, `HornHold`, `HornRetreat`, `HornRally`, `CourierHorn`, `HorseWhistle`).
- Wire `BattleCreatures.cs`, `BattleMonsters.cs`, `BattleAbilities.cs`, `BattleMoments.cs`, `BattleScreen.Orders.cs`, `BattleDivision.cs`.

### AU3. World biome beds, campaign deeds, settlements and creator
- 7 new looping beds (`Surf`, `River`, `Forest`, `Blizzard`, `Tavern`, `Forge`, `OmenDrone`) + `SnowCrunch` and `GravelStep` in `BattleScreen.GroundSteps`.
- Campaign, settlement, site, UI and creator cues (`RecruitCheer`, `EquipBlade`, `EquipPlate`, `EquipMail`, `HorseTack`, `AnvilRing`, `WaxSeal`, `RiteDrum`, `FeastToast`, `ShrineBell`, `MinePick`, `StonesHum`, `ScrollUnroll`, `AtlasFold`, `AlertChime`, `SaluteSteel`).
- Wire `MapScreen.cs`, `MapScreen.*.cs`, `Campaign/*.cs`, `UiScroll.cs`, `CreatorScreen.cs`, `CreatorStage.cs`.

### AU4. Cultural overworld, front-end and places soundtrack (43 scores)
- Hand-authored JSON scores in `Data/music/`:
  - Front-end (3): `title_ironfield`, `creator_hearth` (with cultural lead stems), `chronicle_end`.
  - 7 cultural map suites × 4 states (`_day`, `_night`, `_peace`, `_host` = 28): `azure`, `crimson`, `north`, `sunward`, `steppe`, `league`, `order`.
  - Settlements & sites (8): `town_market`, `tavern_reel_1`, `tavern_reel_2`, `tavern_ballad`, `lords_hall`, `hall_feast`, `chapter_house`, `sacred_site`.
  - Seasons & dread (4): `winter_march`, `winter_night`, `omen_graves`, `black_flame`.
- Wire contextual `MusicDirector` across `ScreenHost`, `CreatorScreen`, and `MapScreen.Soundscape`.

### AU5. Adaptive 4-stem battle suites and stingers (25 scores)
- 13 four-stem battle suites (`Deploy`, `Skirmish`, `Melee`, `Crisis`): 7 cultural field suites (`battle_azure` .. `battle_order`) + 6 special suites (`battle_siege`, `battle_duel`, `battle_tournament`, `battle_beasts`, `battle_undead`, `battle_hollow`).
- 12 battle outcomes & campaign stingers (`victory_field`, `victory_heroic`, `victory_duel`, `defeat_field`, `defeat_fallen`, `sting_war`, `sting_peace`, `sting_rank`, `sting_marriage`, `sting_oath`, `sting_omen`, `sting_found_order`).
- Wire 4-stem battle intensity in `BattleScreen.Soundscape` and campaign stingers.

### AU6. Master mix, SoundDump filter, tests and docs
- Calibrate LUFS/RMS & peak targets across all 96 cues, 16 beds and 68 scores; extend `SoundDump.cs` (`--sounds <dir> [filter]`); archive `docs/AUDIO.md` -> `docs/archive/AUDIO.md` and update `CLAUDE.md`, `docs/DEVELOPING.md` and `docs/CODEMAP.md`; full `tools/check.sh` green.
