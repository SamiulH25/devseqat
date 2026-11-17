# The troop trees

Every soldier in Ironfield, from the levy a village raises to the tier-VII company a realm fields at
the end of a campaign. Generated from `Data/troops.json`, `Data/factions.json` and `Data/orders.json`
by `tools/troopmap.py`; change the data and re-run it (`python3 tools/troopmap.py --write`) rather
than editing this page.

**411 troops, 473 upgrade links.** 387 of them are men and 24 are wild
things. Tiers run I–VII among the men, plus the tier-0 captain, who belongs to no tree. The wild
roster has no links at all. The 30 roots are `levies`, `clansmen`, `acolytes`, `esquires`,
`mounted_esquires`, the captain, and one for each wild troop.

**Not in the world yet:** 80 troops in 17 cultures marked `waiting` in `Data/factions.json`
(`azure_heart`, `azure_march`, `azure_coast`, `crimson_patrician`, `crimson_alpine`, `northern_fjord`, `northern_taiga`, `southern_oasis`, `southern_dune`, `southern_rift`, `steppe_horde`, `steppe_salt`, `league_lagoon`, `league_fen`, `order_chapter`, `order_warden`, `bandit`). They come from the world book
(`docs/world/world.json`) and are kept for when that world is brought into the game; until then no realm,
settlement or band fields them, and their trees stop short of tier VII.

Marks used below: **†** noble stock, raised at a castle rather than a village; **‡** a knight
order's signature troop; **bold** in *From* means the parent is outside the culture.

## How the trees are shaped

- **Tier is the rank.** Every upgrade raises the tier and must be a better soldier — `Troops.WorseUpgrade`
  allows no loss of hp, damage, armour or block (nor range and pierce, for a bow) when a troop keeps its
  class, weapon and shield, and asks for a gain in two of them. Tier I is the levy, VII the sovereign elite.
- **Two recruit roots.** `levies` (tier I, common) feed the five levy realms; `clansmen` (tier I, northern)
  feed the northern and steppe lines. Every troop but the two noble roots, the mages and the captain
  can be reached by upgrading one of those two.
- **Mages are hired, not drilled.** Every caster rises from `acolytes` (tier II), and no recruit upgrade
  leads into a caster line: a circle is hired, never grown from the ranks.
- **Wild things do not upgrade.** The monsters and beasts are a flat roster; their tier is only what an
  army of them is worth on the field.

The trunk every realm shares:

```
levies I (common) ─┬─ shieldmen II ───────── heavy foot of azure, crimson, order, league, southern
                   ├─ spearrunners II ───── every spear line
                   ├─ bowmen II ─────────── every bow and crossbow line
                   ├─ outriders II ──────── every horse line
                   └─ ten culture entry troops at tier II (crossbows, squires, slingers, hobilars...)
clansmen I (northern) ─┬─ axemen II ──────── northern and steppe foot
                       ├─ raiders II ────── northern hearthguard
                       ├─ hunters II ────── northern bows
                       ├─ fell riders II ── northern horse
                       ├─ herd boys II ──── steppe bows
                       ├─ herd riders II ── steppe horse
                       └─ spearrunners II, bowmen II, outriders II (shared with levies)
```

## How a company walks it

The data graph is a DAG; the campaign puts gates on its edges (`Companies.CanEnterLine`):

- **Common troops are open to all.** A stack of a culture's men may only train into its own culture's
  troops or its kin's (Sunward may borrow crimson and northern; the League azure; the Ashen Sun azure
  and crimson; the Steppe northern).
- **Tier V and up need a barracks** of that culture standing in your realm — or of the men's own culture,
  when they are training along a borrowed kin line.
- **Noble birth, or a knighthood.** A company of commoners cannot be promoted into a † troop. Esquires
  are bought at castles (renown 60, 1.6× their listed price) or a named man is knighted.
- **A knight order trains only its own.** Its three signature troops (‡) open when you join or found it.

## Cultures at a glance

| Culture | Realm | Troops | Tier VII | Line | Kin |
|---|---|---|---|---|---|
| Azure | Azure Crown | 42 | 8 | heavy foot, crossbows and lances | — |
| Crimson | Crimson Compact | 32 | 5 | greatswords, halberds, longbows and heavy horse | — |
| Northern | Northmarch | 55 | 8 | axes, round shields, glaives and light horse | — |
| Sunward | Sunward Emirates | 44 | 5 | spears and javelins, light horse and mamluks | Crimson, Northern |
| Steppe | Khaganate of the Grass | 42 | 5 | horse archers first, bows on foot, axes and spears | Northern |
| League | Saltmarsh League | 43 | 5 | pikes and pavise crossbows, hired steel | Azure |
| Ashen Sun | Order of the Ashen Sun | 32 | 5 | sworn brothers on foot and knight-brothers | Azure, Crimson |
| Wild | — | 24 | 0 | beasts and monsters, no upgrades | — |

Men per tier: T0 (the captain) 1, I 3, II 41, III 58, IV 92, V 90, VI 61, VII 41.

## The shared trunk

| Troop | Tier | Class | Cost | Upgrades into |
|---|---|---|---|---|
| **Levies** | I | Infantry | 10 | Bowmen (common), Camel Scouts (southern), Chapter Novices (order_chapter), Commons Bowmen (crimson), Crag Skirmishers (crimson_alpine), Crown Squires (azure), Crown Yeomen (azure_heart), Dune Skirmishers (southern_dune), Fen Slingers (league_fen), Galley Pikemen (league_lagoon), Gate Militia (order_warden), Guild Levies (crimson_patrician), Harbor Watch (azure_coast), Hobilars (crimson), Light Horse (league), March Scouts (azure_march), Militia Crossbows (league), Oasis Spearmen (southern_oasis), Outriders (common), Pilgrim Crossbows (order), Pilgrim Riders (order), Rift Sailors (southern_rift), Shieldmen (common), Slingers (southern), Spearrunners (common), Town Crossbows (azure) |
| **Clansmen** | I | Infantry | 10 | Axemen (northern), Banner Outriders (steppe_horde), Bowmen (common), Fell Riders (northern), Fjord Oarsmen (northern_fjord), Herd Boys (steppe), Herd Riders (steppe), Hunters (northern), Outriders (common), Raiders (northern), Salt Scouts (steppe_salt), Spearrunners (common), Taiga Trappers (northern_taiga) |
| **Shieldmen** | II | Infantry | 24 | Canal Guards (league), Ghazis (southern), Greatswords (crimson), Marines (league), Men-at-Arms (azure), Order Sergeants (order), Sergeants (azure) |
| **Spearrunners** | II | Skirmisher | 22 | Beast Hunters (common), Billmen (common), Grass Spears (steppe), League Pikes (league), Order Spears (order), Pikemen (azure), Sun Javelins (southern) |
| **Bowmen** | II | Archer | 26 | Crossbowmen (azure), League Crossbows (league), Longbowmen (crimson), Order Crossbows (order), Rangers (northern), Sand Archers (southern), Steppe Archers (steppe) |
| **Outriders** | II | Cavalry | 40 | Desert Riders (southern), Horse Archers (northern), Lancers (common), Mounted Yeomen (northern), Order Riders (order), Steppe Bows (steppe), Steppe Lancers (steppe), Stradiots (league) |
| **Acolytes** | II | Caster | 320 | Apothecaries (league), Chaplains (order), Drummers (steppe), Hedge-witches (crimson), Lectors (azure), Rune-singers (northern), Star-readers (southern) |

## Azure — Azure Crown

*Heavy foot, crossbows and lances.* Root: `levies`. No kin.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Levies | Shieldmen | Men-at-Arms | Ironguard | Paladins | Iron Legionaries | Immortals |
| Archers | Town Crossbows | Town Crossbows | Crossbowmen | Arbalests | Heavy Arbalests | Guild Sharpshooters | Grand Arbalests |
| Spears | Levies | Spearrunners | Pikemen | Phalanx Pikemen | Silver Pikes | Grand Phalanx | Imperial Pikemen |
| Horse | Crown Squires | Crown Squires | Lancers | Demi-Lancers | Banner Knights | Paladin Knights | Grail Knights |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| II | Town Crossbows | Archer | 26 | **Levies** | Crossbowmen |
| II | Crown Squires | Cavalry | 40 | **Levies** | Lancers |
| III | Sergeants | Infantry | 44 | **Shieldmen** | Halberdiers, Veteran Sergeants |
| III | Men-at-Arms | Infantry | 45 | **Shieldmen** | Foot Knights, Ironguard, Lily Squires |
| III | Pikemen | Skirmisher | 44 | **Spearrunners** | Halberdiers, Phalanx Pikemen |
| III | Crossbowmen | Archer | 48 | **Bowmen**, Town Crossbows | Arbalests, Pavise Crossbowmen |
| IV | Veteran Sergeants | Infantry | 72 | Sergeants | Aegis Wardens, Banner Sergeants, Sworn Brothers |
| IV | Ironguard | Infantry | 75 | Men-at-Arms | Aegis Wardens, Paladins, Sworn Brothers |
| IV | Foot Knights † | Infantry | 78 | **Esquires**, Men-at-Arms | Paladins, Sword Brethren, Sworn Brothers |
| IV | Lily Squires † ‡ | Infantry | 86 | **Esquires**, Men-at-Arms | Lily Knights |
| IV | Phalanx Pikemen | Skirmisher | 72 | Pikemen | Heavy Halberdiers, Silver Pikes |
| IV | Arbalests | Archer | 76 | Crossbowmen | Heavy Arbalests, Siege Crossbowmen |
| IV | Pavise Crossbowmen | Archer | 78 | Crossbowmen | Siege Crossbowmen |
| IV | Demi-Lancers † | Cavalry | 96 | **Lancers**, **Mounted Esquires**, **Mounted Yeomen** | Banner Knights, Valkyrie Lancers |
| IV | Lectors | Caster | 750 | **Acolytes** | Magisters |
| V | Banner Sergeants | Infantry | 108 | Veteran Sergeants | Marshal Guard |
| V | Aegis Wardens | Infantry | 110 | Ironguard, Veteran Sergeants | Citadel Sentinels, Iron Legionaries |
| V | Paladins † | Infantry | 115 | Foot Knights, Ironguard | Iron Legionaries, Royal Crusaders |
| V | Lily Knights † ‡ | Infantry | 150 | Lily Squires | Wardens of the Lily |
| V | Silver Pikes | Skirmisher | 108 | Phalanx Pikemen | Grand Phalanx, Royal Halberdiers |
| V | Heavy Arbalests | Archer | 112 | Arbalests | Fortress Bolters, Guild Sharpshooters |
| V | Siege Crossbowmen | Archer | 114 | Arbalests, Pavise Crossbowmen | Fortress Bolters |
| V | Banner Knights † | Cavalry | 145 | Demi-Lancers, **Knights** | Iron Cataphracts, Paladin Knights |
| V | Magisters | Caster | 1000 | Lectors | Grand Magisters |
| VI | Citadel Sentinels † | Infantry | 155 | Aegis Wardens | Bastion Lords, Immortals |
| VI | Marshal Guard † | Infantry | 155 | Banner Sergeants | Imperial Marshals |
| VI | Iron Legionaries † | Infantry | 160 | Aegis Wardens, Paladins | Immortals, Sovereign Champions |
| VI | Royal Crusaders † | Infantry | 165 | Paladins, **Sword Brethren** | Sovereign Champions |
| VI | Wardens of the Lily † ‡ | Infantry | 208 | Lily Knights | — |
| VI | Grand Phalanx † | Skirmisher | 172 | Silver Pikes | Imperial Pikemen, Sovereign Halberdiers |
| VI | Guild Sharpshooters † | Archer | 156 | Heavy Arbalests | Grand Arbalests |
| VI | Fortress Bolters † | Archer | 158 | Heavy Arbalests, Siege Crossbowmen | Grand Arbalests, Iron Citadel Arbalests |
| VI | Paladin Knights † | Cavalry | 195 | Banner Knights, **Valkyrie Lancers** | Grail Knights, Imperial Cataphracts |
| VI | Grand Magisters | Caster | 1350 | Magisters | — |
| VII | Imperial Marshals † | Infantry | 218 | Marshal Guard | — |
| VII | Sovereign Champions † | Infantry | 225 | Iron Legionaries, Royal Crusaders | — |
| VII | Bastion Lords † | Infantry | 238 | Citadel Sentinels | — |
| VII | Immortals † | Infantry | 244 | Citadel Sentinels, Iron Legionaries | — |
| VII | Imperial Pikemen † | Skirmisher | 210 | Grand Phalanx | — |
| VII | Grand Arbalests † | Archer | 218 | Fortress Bolters, Guild Sharpshooters | — |
| VII | Iron Citadel Arbalests † | Archer | 222 | Fortress Bolters | — |
| VII | Grail Knights † | Cavalry | 260 | Paladin Knights, **Storm Riders** | — |

**Stops short of tier VII:** Grand Magisters (VI), Wardens of the Lily (VI).

## Crimson — Crimson Compact

*Greatswords, halberds, longbows and heavy horse.* Root: `levies`. No kin.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Levies | Shieldmen | Greatswords | Zweihanders | Blademasters | Grand Blademasters | Sword Saints |
| Archers | Commons Bowmen | Commons Bowmen | Longbowmen | Yeoman Archers | Master Longbowmen | Royal Yeomen | Sovereign Longbowmen |
| Spears | Levies | Spearrunners | Billmen | Halberdiers | Heavy Halberdiers | Royal Halberdiers | Sovereign Halberdiers |
| Horse | Hobilars | Hobilars | Lancers | Knights | Cataphracts | Iron Cataphracts | Imperial Cataphracts |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| II | Commons Bowmen | Archer | 26 | **Levies** | Longbowmen |
| II | Hobilars | Cavalry | 40 | **Levies** | Lancers |
| III | Greatswords | Infantry | 48 | **Shieldmen** | Rose Blades, Sword Brethren, Zweihanders |
| III | Longbowmen | Archer | 48 | **Bowmen**, Commons Bowmen | Composite Bowmen, Yeoman Archers |
| IV | Zweihanders | Infantry | 78 | Greatswords | Blademasters, Executioners, Sworn Brothers |
| IV | Compact Knights † | Infantry | 82 | **Esquires** | Sword Brethren |
| IV | Rose Blades † ‡ | Infantry | 101 | **Esquires**, Greatswords | Rose Swordmasters |
| IV | Halberdiers | Skirmisher | 74 | **Billmen**, **Pikemen**, **Sergeants** | Heavy Halberdiers, Vanguard Glaives |
| IV | Yeoman Archers | Archer | 74 | Longbowmen | Master Longbowmen, Ward Archers |
| IV | Knights † | Cavalry | 105 | **Lancers**, **Mounted Esquires** | Banner Knights, Cataphracts |
| IV | Hedge-witches | Caster | 750 | **Acolytes** | War-witches |
| V | Sword Brethren † | Infantry | 112 | Compact Knights, **Foot Knights**, Greatswords | Grand Blademasters, Royal Crusaders |
| V | Executioners | Infantry | 114 | Zweihanders | Dread Cleavers |
| V | Blademasters | Infantry | 115 | Zweihanders | Dread Cleavers, Grand Blademasters |
| V | Rose Swordmasters † ‡ | Infantry | 150 | Rose Blades | Thorn Knights |
| V | Heavy Halberdiers | Skirmisher | 110 | Halberdiers, **Phalanx Pikemen** | Royal Halberdiers |
| V | Ward Archers | Archer | 106 | **Composite Bowmen**, Yeoman Archers | Royal Yeomen |
| V | Master Longbowmen | Archer | 108 | Yeoman Archers | Highland Marksmen, Royal Yeomen |
| V | Cataphracts † | Cavalry | 150 | Knights | Iron Cataphracts |
| V | War-witches | Caster | 1000 | Hedge-witches | Crimson Crones |
| VI | Dread Cleavers † | Infantry | 160 | Blademasters, Executioners | Archons |
| VI | Grand Blademasters † | Infantry | 162 | Blademasters, Sword Brethren | Archons, Sword Saints |
| VI | Thorn Knights † ‡ | Infantry | 174 | Rose Swordmasters | — |
| VI | Royal Halberdiers † | Skirmisher | 156 | Heavy Halberdiers, **Silver Pikes**, **Vanguard Glaives** | Sovereign Halberdiers |
| VI | Royal Yeomen † | Archer | 152 | Master Longbowmen, Ward Archers | Sky Piercers, Sovereign Longbowmen |
| VI | Iron Cataphracts † | Cavalry | 182 | **Banner Knights**, Cataphracts | Imperial Cataphracts |
| VI | Crimson Crones | Caster | 1350 | War-witches | — |
| VII | Archons † | Infantry | 218 | Dread Cleavers, Grand Blademasters | — |
| VII | Sword Saints † | Infantry | 225 | Grand Blademasters | — |
| VII | Sovereign Halberdiers † | Skirmisher | 214 | **Grand Phalanx**, Royal Halberdiers | — |
| VII | Sovereign Longbowmen † | Archer | 210 | Royal Yeomen | — |
| VII | Imperial Cataphracts † | Cavalry | 265 | Iron Cataphracts, **Paladin Knights** | — |

**Stops short of tier VII:** Crimson Crones (VI), Thorn Knights (VI).

## Northern — Northmarch

*Axes, round shields, glaives and light horse.* Root: `clansmen`. No kin.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Clansmen | Axemen | Huscarls | Varangians | Einherjar | Thane Champions | High King's Chosen |
| Archers | Hunters | Hunters | Rangers | Woodland Stalkers | Shadow Hunters | Highland Marksmen | Sky Piercers |
| Spears | Clansmen | Spearrunners | Billmen | Glaivemen | Vanguard Glaives | Dragon Glaives | Tempest Spears |
| Horse | Fell Riders | Fell Riders | Horse Archers | Nomad Bows | Steppe Marksmen | Khan's Guard | Golden Horde Masters |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| I | Clansmen | Infantry | 10 | **root** | Axemen, Banner Outriders, Bowmen, Fell Riders, Fjord Oarsmen, Herd Boys, Herd Riders, Hunters, Outriders, Raiders, Salt Scouts, Spearrunners, Taiga Trappers |
| II | Raiders | Infantry | 24 | Clansmen | Hearthguard, Huscarls |
| II | Axemen | Infantry | 26 | Clansmen | Berserkers, Huscarls, Yurt Guards |
| II | Hunters | Archer | 26 | Clansmen | Rangers |
| II | Fell Riders | Cavalry | 40 | Clansmen | Horse Archers |
| III | Hearthguard | Infantry | 46 | Raiders | Fjord Veterans, Varangians |
| III | Berserkers | Infantry | 48 | Axemen | Shieldbreakers, Ulfhednar |
| III | Huscarls | Infantry | 50 | Axemen, Raiders | Shieldbreakers, Varangians, Wolf Thegns |
| III | Rangers | Archer | 46 | **Bowmen**, Hunters | Composite Bowmen, Woodland Stalkers |
| III | Horse Archers | Cavalry | 64 | Fell Riders, **Outriders** | Mounted Crossbows, Nomad Bows |
| III | Mounted Yeomen | Cavalry | 64 | **Outriders** | Demi-Lancers, Reaver Horsemen |
| IV | Fjord Veterans | Infantry | 74 | Hearthguard | Jarl Guard, Sworn Brothers |
| IV | Ulfhednar | Infantry | 76 | Berserkers | Doomcleavers, Sworn Brothers |
| IV | Shieldbreakers | Infantry | 78 | Berserkers, Huscarls | Doomcleavers, Einherjar, Sworn Brothers |
| IV | Varangians | Infantry | 80 | Hearthguard, Huscarls | Einherjar, Jarl Guard, Sworn Brothers |
| IV | Thane Hearthguard † | Infantry | 82 | **Esquires** | Huscarl Thanes |
| IV | Wolf Thegns † ‡ | Infantry | 104 | **Esquires**, Huscarls | Wolfsworn |
| IV | Glaivemen | Skirmisher | 70 | **Billmen** | Vanguard Glaives |
| IV | Composite Bowmen | Archer | 72 | **Longbowmen**, Rangers | Shadow Hunters, Ward Archers |
| IV | Woodland Stalkers | Archer | 72 | Rangers | Shadow Hunters |
| IV | Nomad Bows | Cavalry | 92 | Horse Archers | Carabiniers, Steppe Marksmen |
| IV | Mounted Crossbows | Cavalry | 94 | Horse Archers | Carabiniers |
| IV | Reaver Horsemen | Cavalry | 94 | Mounted Yeomen | Valkyrie Lancers |
| IV | Thane Riders † | Cavalry | 98 | **Mounted Esquires** | Storm Lancers |
| IV | Rune-singers | Caster | 750 | **Acolytes** | Skalds |
| V | Doomcleavers | Infantry | 112 | Shieldbreakers, Ulfhednar | Valhalla Reavers |
| V | Jarl Guard | Infantry | 114 | Fjord Veterans, Varangians | Iron Thanes, Thane Champions |
| V | Einherjar | Infantry | 116 | Shieldbreakers, Varangians | Thane Champions, Valhalla Reavers |
| V | Huscarl Thanes † | Infantry | 118 | Thane Hearthguard | Iron Thanes, Thane Champions |
| V | Wolfsworn † ‡ | Infantry | 151 | Wolf Thegns | Winter Wolves |
| V | Vanguard Glaives | Skirmisher | 106 | Glaivemen, **Halberdiers** | Dragon Glaives, Royal Halberdiers |
| V | Shadow Hunters | Archer | 105 | Composite Bowmen, Woodland Stalkers | Highland Marksmen |
| V | Steppe Marksmen | Cavalry | 130 | Nomad Bows | Khan's Guard, Royal Dragoons |
| V | Carabiniers | Cavalry | 134 | Mounted Crossbows, Nomad Bows | Royal Dragoons |
| V | Valkyrie Lancers | Cavalry | 138 | **Demi-Lancers**, Reaver Horsemen | Paladin Knights, Storm Riders |
| V | Storm Lancers † | Cavalry | 142 | Thane Riders | Storm Riders |
| V | Skalds | Caster | 1000 | Rune-singers | Galdr-masters |
| VI | Iron Thanes † | Infantry | 152 | Huscarl Thanes, Jarl Guard | Allfather's Guard, High King's Chosen |
| VI | Thane Champions † | Infantry | 164 | Einherjar, Huscarl Thanes, Jarl Guard | Blood Jarls, High King's Chosen |
| VI | Valhalla Reavers † | Infantry | 178 | Doomcleavers, Einherjar | Blood Jarls |
| VI | Winter Wolves † ‡ | Infantry | 213 | Wolfsworn | — |
| VI | Dragon Glaives † | Skirmisher | 150 | Vanguard Glaives | Tempest Spears |
| VI | Highland Marksmen † | Archer | 148 | **Master Longbowmen**, Shadow Hunters | Sky Piercers |
| VI | Khan's Guard † | Cavalry | 180 | Steppe Marksmen | Golden Horde Masters, Sovereign Dragoons |
| VI | Royal Dragoons † | Cavalry | 184 | Carabiniers, Steppe Marksmen | Sovereign Dragoons |
| VI | Storm Riders † | Cavalry | 185 | Storm Lancers, Valkyrie Lancers | Grail Knights, Thunder Lords |
| VI | Galdr-masters | Caster | 1350 | Skalds | — |
| VII | Blood Jarls † | Infantry | 222 | Thane Champions, Valhalla Reavers | — |
| VII | Allfather's Guard † | Infantry | 224 | Iron Thanes | — |
| VII | High King's Chosen † | Infantry | 228 | Iron Thanes, Thane Champions | — |
| VII | Tempest Spears † | Skirmisher | 212 | Dragon Glaives | — |
| VII | Sky Piercers † | Archer | 206 | Highland Marksmen, **Royal Yeomen** | — |
| VII | Sovereign Dragoons † | Cavalry | 218 | Khan's Guard, Royal Dragoons | — |
| VII | Golden Horde Masters † | Cavalry | 245 | Khan's Guard | — |
| VII | Thunder Lords † | Cavalry | 255 | Storm Riders | — |

**Stops short of tier VII:** Galdr-masters (VI), Winter Wolves (VI).

## Sunward — Sunward Emirates

*Spears and javelins, light horse and mamluks.* Root: `levies`. Kin: Crimson, Northern.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Levies | Shieldmen | Ghazis | Jandars | Dervish Blades | Emir's Guard | Lions of the Sun |
| Archers | Slingers | Slingers | Sand Archers | Oasis Bows | Emir's Archers | Falcon Eyes | Sultan's Bows |
| Spears | Levies | Spearrunners | Sun Javelins | Dune Spears | Sun Guard | Emir's Spears | Golden Spears |
| Horse | Camel Scouts | Camel Scouts | Desert Riders | Mamluks | Sun Lancers | Emir's Mamluks | Sultan's Guard |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| II | Slingers | Archer | 21 | **Levies** | Desert Slingers, Sand Archers |
| II | Camel Scouts | Cavalry | 40 | **Levies** | Desert Riders |
| III | Ghazis | Infantry | 45 | **Shieldmen** | Dervishes, Jandars |
| III | Sun Javelins | Skirmisher | 44 | **Spearrunners** | Dune Spears, Naffatun |
| III | Desert Slingers | Archer | 41 | Slingers | Storm Slingers |
| III | Sand Archers | Archer | 46 | **Bowmen**, Slingers | Oasis Bows |
| III | Desert Riders | Cavalry | 68 | Camel Scouts, **Outriders** | Camel Lancers, Falcon Riders, Mamluks |
| IV | Jandars | Infantry | 75 | Ghazis | Dervish Blades, Sworn Brothers |
| IV | Dervishes | Infantry | 78 | Ghazis | Whirling Dervishes |
| IV | Faris Guard † | Infantry | 80 | **Esquires** | Mamluk Footmen |
| IV | Dune Spears | Skirmisher | 72 | Sun Javelins | Sun Guard |
| IV | Naffatun | Skirmisher | 72 | Sun Javelins | Flame Bearers |
| IV | Storm Slingers | Archer | 63 | Desert Slingers | — |
| IV | Oasis Bows | Archer | 72 | Sand Archers | Emir's Archers |
| IV | Faris Lancers † | Cavalry | 100 | **Mounted Esquires** | Mamluk Knights |
| IV | Camel Lancers | Cavalry | 105 | Desert Riders | Camel Guard |
| IV | Mamluks | Cavalry | 105 | Desert Riders | Sun Lancers |
| IV | Falcon Riders † ‡ | Cavalry | 136 | Desert Riders, **Mounted Esquires** | Falcon Knights |
| IV | Star-readers | Caster | 750 | **Acolytes** | Astrologers |
| V | Dervish Blades | Infantry | 115 | Jandars | Emir's Guard |
| V | Whirling Dervishes | Infantry | 115 | Dervishes | Sword Dancers |
| V | Mamluk Footmen † | Infantry | 116 | Faris Guard | Emir's Guard |
| V | Flame Bearers | Skirmisher | 108 | Naffatun | Sun-fire Throwers |
| V | Sun Guard | Skirmisher | 108 | Dune Spears | Emir's Spears |
| V | Emir's Archers | Archer | 105 | Oasis Bows | Falcon Eyes |
| V | Mamluk Knights † | Cavalry | 148 | Faris Lancers | Emir's Mamluks |
| V | Camel Guard | Cavalry | 150 | Camel Lancers | Sultan's Camels |
| V | Sun Lancers | Cavalry | 150 | Mamluks | Emir's Mamluks |
| V | Falcon Knights † ‡ | Cavalry | 156 | Falcon Riders | Sun Hawks |
| V | Astrologers | Caster | 1000 | Star-readers | Sages of the Sun |
| VI | Emir's Guard † | Infantry | 160 | Dervish Blades, Mamluk Footmen | Lions of the Sun |
| VI | Sword Dancers † | Infantry | 163 | Whirling Dervishes | Saints of the Sand |
| VI | Emir's Spears † | Skirmisher | 152 | Sun Guard | Golden Spears |
| VI | Sun-fire Throwers † | Skirmisher | 178 | Flame Bearers | — |
| VI | Falcon Eyes † | Archer | 148 | Emir's Archers | Sultan's Bows |
| VI | Sultan's Camels † | Cavalry | 182 | Camel Guard | — |
| VI | Emir's Mamluks † | Cavalry | 200 | Mamluk Knights, Sun Lancers | Sultan's Guard |
| VI | Sun Hawks † ‡ | Cavalry | 212 | Falcon Knights | — |
| VI | Sages of the Sun | Caster | 1350 | Astrologers | — |
| VII | Lions of the Sun † | Infantry | 220 | Emir's Guard | — |
| VII | Saints of the Sand † | Infantry | 224 | Sword Dancers | — |
| VII | Golden Spears † | Skirmisher | 210 | Emir's Spears | — |
| VII | Sultan's Bows † | Archer | 204 | Falcon Eyes | — |
| VII | Sultan's Guard † | Cavalry | 265 | Emir's Mamluks | — |

**Stops short of tier VII:** Storm Slingers (IV), Sages of the Sun (VI), Sultan's Camels (VI), Sun Hawks (VI), Sun-fire Throwers (VI).

## Steppe — Khaganate of the Grass

*Horse archers first, bows on foot, axes and spears.* Root: `clansmen`. Kin: Northern.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Clansmen | Axemen | Yurt Guards | Torguud | Wolf Axes | Noyan's Guard | Khagan's Axes |
| Archers | Herd Boys | Herd Boys | Steppe Archers | Yurt Archers | Tengri Bows | Sky Wolf Archers | Eternal Sky Archers |
| Spears | Clansmen | Spearrunners | Grass Spears | Horde Spears | Tumen Spears | Sky Spears | Eternal Spears |
| Horse | Herd Riders | Herd Riders | Steppe Bows | Kheshig Riders | Noyan Bows | Kheshig | Khagan's Own |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| II | Herd Boys | Archer | 26 | **Clansmen** | Steppe Archers |
| II | Herd Riders | Cavalry | 40 | **Clansmen** | Steppe Bows, Wolf Scouts |
| III | Yurt Guards | Infantry | 50 | **Axemen** | Torguud, Tug Bearers |
| III | Grass Spears | Skirmisher | 42 | **Spearrunners** | Horde Spears |
| III | Steppe Archers | Archer | 46 | **Bowmen**, Herd Boys | Yurt Archers |
| III | Steppe Bows | Cavalry | 64 | Herd Riders, **Outriders** | Kheshig Riders, Sky Riders |
| III | Wolf Scouts | Cavalry | 64 | Herd Riders | Wolf Runners |
| III | Steppe Lancers | Cavalry | 68 | **Outriders** | Torguud Lancers |
| IV | Torguud | Infantry | 80 | Yurt Guards | Sworn Brothers, Wolf Axes |
| IV | Noyan Footmen † | Infantry | 82 | **Esquires** | Steppe Champions |
| IV | Horde Spears | Skirmisher | 70 | Grass Spears | Tumen Spears |
| IV | Tug Bearers | Skirmisher | 72 | Yurt Guards | Tug Guard |
| IV | Yurt Archers | Archer | 72 | Steppe Archers | Tengri Bows |
| IV | Kheshig Riders | Cavalry | 92 | Steppe Bows | Noyan Bows |
| IV | Wolf Runners | Cavalry | 93 | Wolf Scouts | — |
| IV | Noyan Riders † | Cavalry | 100 | **Mounted Esquires** | Baghatur |
| IV | Torguud Lancers | Cavalry | 105 | Steppe Lancers | Iron Lancers |
| IV | Sky Riders † ‡ | Cavalry | 120 | **Mounted Esquires**, Steppe Bows | Sky Archers |
| IV | Drummers | Caster | 750 | **Acolytes** | Shamans |
| V | Wolf Axes | Infantry | 116 | Torguud | Noyan's Guard |
| V | Steppe Champions † | Infantry | 118 | Noyan Footmen | Noyan's Guard |
| V | Tumen Spears | Skirmisher | 106 | Horde Spears | Sky Spears |
| V | Tug Guard | Skirmisher | 108 | Tug Bearers | Tug Lords |
| V | Tengri Bows | Archer | 105 | Yurt Archers | Sky Wolf Archers |
| V | Noyan Bows | Cavalry | 130 | Kheshig Riders | Kheshig |
| V | Baghatur † | Cavalry | 148 | Noyan Riders | Khan's Lancers |
| V | Iron Lancers | Cavalry | 150 | Torguud Lancers | Khan's Lancers |
| V | Sky Archers † ‡ | Cavalry | 169 | Sky Riders | Sky-Sworn |
| V | Shamans | Caster | 1000 | Drummers | Sky-callers |
| VI | Noyan's Guard † | Infantry | 164 | Steppe Champions, Wolf Axes | Khagan's Axes |
| VI | Tug Lords † | Skirmisher | 152 | Tug Guard | — |
| VI | Sky Spears † | Skirmisher | 170 | Tumen Spears | Eternal Spears |
| VI | Sky Wolf Archers † | Archer | 148 | Tengri Bows | Eternal Sky Archers |
| VI | Kheshig † | Cavalry | 180 | Noyan Bows | Khagan's Own |
| VI | Khan's Lancers † | Cavalry | 182 | Baghatur, Iron Lancers | Ironclad Khans |
| VI | Sky-Sworn † ‡ | Cavalry | 234 | Sky Archers | — |
| VI | Sky-callers | Caster | 1350 | Shamans | — |
| VII | Khagan's Axes † | Infantry | 228 | Noyan's Guard | — |
| VII | Eternal Spears † | Skirmisher | 212 | Sky Spears | — |
| VII | Eternal Sky Archers † | Archer | 206 | Sky Wolf Archers | — |
| VII | Khagan's Own † | Cavalry | 245 | Kheshig | — |
| VII | Ironclad Khans † | Cavalry | 262 | Khan's Lancers | — |

**Stops short of tier VII:** Wolf Runners (IV), Sky-Sworn (VI), Sky-callers (VI), Tug Lords (VI).

## League — Saltmarsh League

*Pikes and pavise crossbows, hired steel.* Root: `levies`. Kin: Azure.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Levies | Shieldmen | Canal Guards | Guild Swords | Brigandiers | Doge's Guard | Lion Guard |
| Archers | Militia Crossbows | Militia Crossbows | League Crossbows | Pavisiers | Guild Arbalesters | Doge's Arbalesters | Lion Crossbows |
| Spears | Levies | Spearrunners | League Pikes | Guild Pikes | Marsh Wardens | Doge's Pikes | Lagoon Guard |
| Horse | Light Horse | Light Horse | Stradiots | Condottieri | Guild Lances | Doge's Lances | Lion Knights |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| II | Militia Crossbows | Archer | 26 | **Levies** | League Crossbows |
| II | Light Horse | Cavalry | 40 | **Levies** | Stradiots |
| III | Canal Guards | Infantry | 45 | **Shieldmen** | Free Companions, Guild Swords |
| III | Marines | Infantry | 47 | **Shieldmen** | Galley Marines |
| III | League Pikes | Skirmisher | 44 | **Spearrunners** | Guild Pikes, Lantern Pikes, Pavise Spears |
| III | League Crossbows | Archer | 48 | **Bowmen**, Militia Crossbows | Pavisiers |
| III | Stradiots | Cavalry | 68 | Light Horse, **Outriders** | Condottieri |
| IV | Guild Swords | Infantry | 75 | Canal Guards | Brigandiers, Sworn Brothers |
| IV | Galley Marines | Infantry | 78 | Marines | Sea Wardens |
| IV | Patrician Guard † | Infantry | 82 | **Esquires** | Senate Guard |
| IV | Guild Pikes | Skirmisher | 72 | League Pikes | Marsh Wardens |
| IV | Pavise Spears | Skirmisher | 72 | League Pikes | Wall Spears |
| IV | Free Companions | Skirmisher | 79 | Canal Guards | Condottiere Foot |
| IV | Lantern Pikes ‡ | Skirmisher | 94 | League Pikes | Lantern Guards |
| IV | Pavisiers | Archer | 78 | League Crossbows | Guild Arbalesters |
| IV | Condottieri | Cavalry | 96 | Stradiots | Guild Lances |
| IV | Signoria Lancers † | Cavalry | 98 | **Mounted Esquires** | Guild Knights |
| IV | Apothecaries | Caster | 750 | **Acolytes** | Alchemists |
| V | Brigandiers | Infantry | 115 | Guild Swords | Doge's Guard |
| V | Sea Wardens | Infantry | 115 | **Bay Marines**, Galley Marines | Admiral's Guard |
| V | Senate Guard † | Infantry | 118 | Patrician Guard | Doge's Guard |
| V | Marsh Wardens | Skirmisher | 108 | Guild Pikes | Doge's Pikes |
| V | Wall Spears | Skirmisher | 108 | Pavise Spears | Lagoon Wall |
| V | Condottiere Foot | Skirmisher | 119 | Free Companions | Captains of Fortune |
| V | Lantern Guards ‡ | Skirmisher | 140 | Lantern Pikes | Lantern Knights |
| V | Guild Arbalesters | Archer | 112 | Pavisiers | Doge's Arbalesters |
| V | Guild Knights † | Cavalry | 145 | Signoria Lancers | Doge's Lances |
| V | Guild Lances | Cavalry | 145 | Condottieri | Doge's Lances |
| V | Alchemists | Caster | 1000 | Apothecaries | Guild Alchemists |
| VI | Doge's Guard † | Infantry | 160 | Brigandiers, Senate Guard | Lion Guard |
| VI | Admiral's Guard † | Infantry | 163 | Sea Wardens | — |
| VI | Doge's Pikes † | Skirmisher | 152 | Marsh Wardens | Lagoon Guard |
| VI | Lagoon Wall † | Skirmisher | 152 | Wall Spears | — |
| VI | Captains of Fortune † | Skirmisher | 167 | Condottiere Foot | Lords of Fortune |
| VI | Lantern Knights † ‡ | Skirmisher | 198 | Lantern Guards | — |
| VI | Doge's Arbalesters † | Archer | 156 | Guild Arbalesters | Lion Crossbows |
| VI | Doge's Lances † | Cavalry | 195 | Guild Knights, Guild Lances | Lion Knights |
| VI | Guild Alchemists | Caster | 1350 | Alchemists | — |
| VII | Lion Guard † | Infantry | 220 | Doge's Guard | — |
| VII | Lagoon Guard † | Skirmisher | 210 | Doge's Pikes | — |
| VII | Lords of Fortune † | Skirmisher | 252 | Captains of Fortune | — |
| VII | Lion Crossbows † | Archer | 218 | Doge's Arbalesters | — |
| VII | Lion Knights † | Cavalry | 242 | Doge's Lances | — |

**Stops short of tier VII:** Admiral's Guard (VI), Guild Alchemists (VI), Lagoon Wall (VI), Lantern Knights (VI).

## Ashen Sun — Order of the Ashen Sun

*Sworn brothers on foot and knight-brothers.* Root: `levies`. Kin: Azure, Crimson.

**The realm's line** — what its lords promote along (`factions.json`). A name repeated across two
tiers means the line has no distinct troop at the lower one yet, so its companies start a step up:

| Role | I | II | III | IV | V | VI | VII |
|---|---|---|---|---|---|---|---|
| Infantry | Levies | Shieldmen | Order Sergeants | Sun Brothers | Ashen Brothers | Sun Templars | Ashen Saints |
| Archers | Pilgrim Crossbows | Pilgrim Crossbows | Order Crossbows | Chapter Arbalests | Ashen Arbalests | Sun Arbalests | Dawn Watch |
| Spears | Levies | Spearrunners | Order Spears | Chapter Pikes | Ashen Pikes | Pilgrim Guard | Dawn Pikes |
| Horse | Pilgrim Riders | Pilgrim Riders | Order Riders | Knight Brothers | Sun Knights | Ashen Knights | Grand Masters |

**Every troop of the culture**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| II | Pilgrim Crossbows | Archer | 26 | **Levies** | Order Crossbows |
| II | Pilgrim Riders | Cavalry | 40 | **Levies** | Order Riders, Turcopoles |
| III | Order Sergeants | Infantry | 50 | **Shieldmen** | Hammer Brothers, Sun Brothers |
| III | Order Spears | Skirmisher | 48 | **Spearrunners** | Chapter Pikes |
| III | Order Crossbows | Archer | 53 | **Bowmen**, Pilgrim Crossbows | Chapter Arbalests |
| III | Turcopoles | Cavalry | 64 | Pilgrim Riders | Veteran Turcopoles |
| III | Order Riders | Cavalry | 75 | **Outriders**, Pilgrim Riders | Knight Brothers |
| IV | Hammer Brothers † | Infantry | 78 | Order Sergeants | Iron Hammers |
| IV | Sun Brothers † ‡ | Infantry | 96 | Order Sergeants | Ashen Brothers |
| IV | Chapter Pikes | Skirmisher | 79 | Order Spears | Ashen Pikes |
| IV | Chapter Arbalests | Archer | 84 | Order Crossbows | Ashen Arbalests |
| IV | Veteran Turcopoles | Cavalry | 93 | Turcopoles | Chapter Turcopoles |
| IV | Knight Brothers † | Cavalry | 106 | Order Riders | Sun Knights |
| IV | Chaplains | Caster | 750 | **Acolytes** | Battle Priests |
| V | Iron Hammers † | Infantry | 115 | Hammer Brothers | Hammers of the Sun |
| V | Ashen Brothers † ‡ | Infantry | 127 | Sun Brothers | Sun Templars |
| V | Ashen Pikes | Skirmisher | 119 | Chapter Pikes | Pilgrim Guard |
| V | Ashen Arbalests | Archer | 123 | Chapter Arbalests | Sun Arbalests |
| V | Chapter Turcopoles | Cavalry | 132 | Veteran Turcopoles | — |
| V | Sun Knights † | Cavalry | 160 | Knight Brothers | Ashen Knights |
| V | Battle Priests | Caster | 1000 | Chaplains | Saints Militant |
| VI | Hammers of the Sun † | Infantry | 163 | Iron Hammers | Ashen Hammers |
| VI | Sun Templars † ‡ | Infantry | 176 | Ashen Brothers | Ashen Saints |
| VI | Pilgrim Guard † | Skirmisher | 167 | Ashen Pikes | Dawn Pikes |
| VI | Sun Arbalests † | Archer | 172 | Ashen Arbalests | Dawn Watch |
| VI | Ashen Knights † | Cavalry | 215 | Sun Knights | Grand Masters |
| VI | Saints Militant | Caster | 1350 | Battle Priests | — |
| VII | Ashen Hammers † | Infantry | 214 | Hammers of the Sun | — |
| VII | Ashen Saints † | Infantry | 242 | Sun Templars | — |
| VII | Dawn Pikes † | Skirmisher | 200 | Pilgrim Guard | — |
| VII | Dawn Watch † | Archer | 240 | Sun Arbalests | — |
| VII | Grand Masters † | Cavalry | 258 | Ashen Knights | — |

**Stops short of tier VII:** Chapter Turcopoles (V), Saints Militant (VI).

## Common — the trunk, the noble roots and your own order

The troops of no realm: what every culture shares (`levies`, `shieldmen`, `bowmen`, `outriders` and
their kin), the noble stock that hangs off castles (`esquires`, `mounted_esquires`), the player's own
knight order (`sworn_brothers`, `sworn_masters`), the few odd branches that lead nowhere else
(`billmen`, `beast_hunters`, the Hollow Crown's line), and the captain himself at tier 0.

**Every common troop**, by tier:

| Tier | Troop | Class | Cost | From | To |
|---|---|---|---|---|---|
| 0 | Captain | Captain | — | **root** | — |
| I | Levies | Infantry | 10 | **root** | Bowmen, Camel Scouts, Chapter Novices, Commons Bowmen, Crag Skirmishers, Crown Squires, Crown Yeomen, Dune Skirmishers, Fen Slingers, Galley Pikemen, Gate Militia, Guild Levies, Harbor Watch, Hobilars, Light Horse, March Scouts, Militia Crossbows, Oasis Spearmen, Outriders, Pilgrim Crossbows, Pilgrim Riders, Rift Sailors, Shieldmen, Slingers, Spearrunners, Town Crossbows |
| II | Shieldmen | Infantry | 24 | Levies | Canal Guards, Ghazis, Greatswords, Marines, Men-at-Arms, Order Sergeants, Sergeants |
| II | Spearrunners | Skirmisher | 22 | **Clansmen**, Levies | Beast Hunters, Billmen, Grass Spears, League Pikes, Order Spears, Pikemen, Sun Javelins |
| II | Bowmen | Archer | 26 | **Clansmen**, Levies | Crossbowmen, League Crossbows, Longbowmen, Order Crossbows, Rangers, Sand Archers, Steppe Archers |
| II | Outriders | Cavalry | 40 | **Clansmen**, Levies | Desert Riders, Horse Archers, Lancers, Mounted Yeomen, Order Riders, Steppe Bows, Steppe Lancers, Stradiots |
| II | Acolytes | Caster | 320 | **root** | Apothecaries, Chaplains, Drummers, Hedge-witches, Lectors, Rune-singers, Star-readers |
| III | Esquires † | Infantry | 60 | **root** | Compact Knights, Faris Guard, Foot Knights, Hollow Squires, Lily Squires, Noyan Footmen, Patrician Guard, Rose Blades, Thane Hearthguard, Wolf Thegns |
| III | Beast Hunters | Skirmisher | 40 | Spearrunners | — |
| III | Billmen | Skirmisher | 42 | Spearrunners | Glaivemen, Halberdiers, Hollow Squires |
| III | Mounted Esquires † | Cavalry | 66 | **root** | Demi-Lancers, Falcon Riders, Faris Lancers, Knights, Noyan Riders, Signoria Lancers, Sky Riders, Thane Riders |
| III | Lancers | Cavalry | 68 | **Crown Squires**, **Hobilars**, Outriders | Demi-Lancers, Knights |
| IV | Hollow Squires † ‡ | Infantry | 95 | Billmen, Esquires | Hollow Knights |
| V | Hollow Knights † ‡ | Infantry | 150 | Hollow Squires | Lords of the Hollow Crown |
| V | Sworn Brothers † | Infantry | 155 | **Fjord Veterans**, **Foot Knights**, **Guild Swords**, **Ironguard**, **Jandars**, **Shieldbreakers**, **Torguud**, **Ulfhednar**, **Varangians**, **Veteran Sergeants**, **Zweihanders** | Masters of the Order |
| VI | Masters of the Order † | Infantry | 162 | Sworn Brothers | — |
| VI | Lords of the Hollow Crown † ‡ | Infantry | 172 | Hollow Knights | — |

**Stops short of tier VII:** Beast Hunters (III), Lords of the Hollow Crown (VI), Masters of the Order (VI).

## Bridges between cultures

Most upgrades stay inside one culture, but a few are the roads between them — a Crimson greatsword
becomes an Azure Royal Crusader, a northern glaive a Crimson Royal Halberdier. These are the links that
make a conquered realm's veterans useful, provided the men may serve that culture (they must be kin):

- Axemen (Northern) → Yurt Guards (Steppe)
- Banner Knights (Azure) → Iron Cataphracts (Crimson)
- Bay Marines (Bayfolk) → Sea Wardens (League)
- Clansmen (Northern) → Herd Boys (Steppe)
- Clansmen (Northern) → Herd Riders (Steppe)
- Clansmen (Northern) → Fjord Oarsmen (Fjord-Reavers)
- Clansmen (Northern) → Taiga Trappers (Wood-Clans)
- Clansmen (Northern) → Banner Outriders (Free Banners)
- Clansmen (Northern) → Salt Scouts (Salt-Riders)
- Composite Bowmen (Northern) → Ward Archers (Crimson)
- Demi-Lancers (Azure) → Valkyrie Lancers (Northern)
- Foot Knights (Azure) → Sword Brethren (Crimson)
- Grand Phalanx (Azure) → Sovereign Halberdiers (Crimson)
- Halberdiers (Crimson) → Vanguard Glaives (Northern)
- Knights (Crimson) → Banner Knights (Azure)
- Longbowmen (Crimson) → Composite Bowmen (Northern)
- Master Longbowmen (Crimson) → Highland Marksmen (Northern)
- Mounted Yeomen (Northern) → Demi-Lancers (Azure)
- Paladin Knights (Azure) → Imperial Cataphracts (Crimson)
- Phalanx Pikemen (Azure) → Heavy Halberdiers (Crimson)
- Pikemen (Azure) → Halberdiers (Crimson)
- Royal Yeomen (Crimson) → Sky Piercers (Northern)
- Sergeants (Azure) → Halberdiers (Crimson)
- Silver Pikes (Azure) → Royal Halberdiers (Crimson)
- Storm Riders (Northern) → Grail Knights (Azure)
- Sword Brethren (Crimson) → Royal Crusaders (Azure)
- Valkyrie Lancers (Northern) → Paladin Knights (Azure)
- Vanguard Glaives (Northern) → Royal Halberdiers (Crimson)

The hubs, where many lines meet:

- **Sworn Brothers** (V, Infantry) ← Fjord Veterans, Foot Knights, Guild Swords, Ironguard, Jandars, Shieldbreakers, Torguud, Ulfhednar, Varangians, Veteran Sergeants, Zweihanders → Masters of the Order
- **Esquires** (III, Infantry) ← root → Compact Knights, Faris Guard, Foot Knights, Hollow Squires, Lily Squires, Noyan Footmen, Patrician Guard, Rose Blades, Thane Hearthguard, Wolf Thegns
- **Mounted Esquires** (III, Cavalry) ← root → Demi-Lancers, Falcon Riders, Faris Lancers, Knights, Noyan Riders, Signoria Lancers, Sky Riders, Thane Riders
- **Lancers** (III, Cavalry) ← Crown Squires, Hobilars, Outriders → Demi-Lancers, Knights
- **Halberdiers** (IV, Skirmisher) ← Billmen, Pikemen, Sergeants → Heavy Halberdiers, Vanguard Glaives
- **Longbowmen** (III, Archer) ← Bowmen, Commons Bowmen → Composite Bowmen, Yeoman Archers
- **Valkyrie Lancers** (V, Cavalry) ← Demi-Lancers, Reaver Horsemen → Paladin Knights, Storm Riders
- **Sword Brethren** (V, Infantry) ← Compact Knights, Foot Knights, Greatswords → Grand Blademasters, Royal Crusaders

`sworn_brothers` is the exception that proves the rule: eleven different lines feed it, because it is
the player's own knight order — every realm's veteran can become one once you have founded it.

## Knight orders

Eight orders in `Data/orders.json`, each with three signature troops (‡ in the tables above), plus the
order you found yourself. A troop's `order` field keeps outsiders from training into it; the Hollow
Crown is a hidden order you can uncover, and `own` is the order you found.

| Order | Patron | Signature troops |
|---|---|---|
| Order of the Silver Lily | Azure Crown | Lily Squires, Lily Knights, Wardens of the Lily |
| Brotherhood of the Red Rose | Crimson Compact | Rose Blades, Rose Swordmasters, Thorn Knights |
| Sworn of the Eternal Sky | Khaganate of the Grass | Sky Riders, Sky Archers, Sky-Sworn |
| The Wolfsworn | Northmarch | Wolf Thegns, Wolfsworn, Winter Wolves |
| Knights of the Ashen Sun | Order of the Ashen Sun | Sun Brothers, Ashen Brothers, Sun Templars |
| The Lantern Company | Saltmarsh League | Lantern Pikes, Lantern Guards, Lantern Knights |
| Order of the Burning Falcon | Sunward Emirates | Falcon Riders, Falcon Knights, Sun Hawks |
| The Hollow Crown | — (hidden) | Hollow Squires, Hollow Knights, Lords of the Hollow Crown |
| Your own order | you | Sworn Brothers, Masters of the Order |

## Mages

One caster line per culture, hired as `acolytes` (II) and rising at IV, V and VI. A circle costs 750,
1000 and 1350 gold a company, far above any soldier's price, and it is the one line no recruit can enter.

| Culture | IV | V | VI |
|---|---|---|---|
| Azure | Lectors (750) | Magisters (1000) | Grand Magisters (1350) |
| Crimson | Hedge-witches (750) | War-witches (1000) | Crimson Crones (1350) |
| Northern | Rune-singers (750) | Skalds (1000) | Galdr-masters (1350) |
| Sunward | Star-readers (750) | Astrologers (1000) | Sages of the Sun (1350) |
| Steppe | Drummers (750) | Shamans (1000) | Sky-callers (1350) |
| League | Apothecaries (750) | Alchemists (1000) | Guild Alchemists (1350) |
| Ashen Sun | Chaplains (750) | Battle Priests (1000) | Saints Militant (1350) |

## The wild

Beasts and monsters, fielded by the campaign's wild parties and by the Ashen Sun's hunts. No upgrades,
no culture but their breed; tier says what a man of them is worth next to a soldier. A band is led by
a stronger beast of its own breed (`Campaign.MonsterLeader`): a goblin holdout by its chieftain and a
hobgoblin warband by a hobgoblin, a wolf pack by an alpha, a nest by its brood mother. Beyond that,
`goblin_warlord` is placed in every goblin holdout, `necromancer` captains a host of the risen dead and
raises the fallen, and `death_knight` joins such a host when the Omen runs high.

| Tier | Troop | Class | Breed | Cost |
|---|---|---|---|---|
| I | Goblin Archers | Archer | goblin | 8 |
| I | Goblins | Infantry | goblin | 6 |
| I | Spiderlings | Infantry | spider | 6 |
| II | Skeletons | Infantry | undead | 30 |
| II | War Hounds | Infantry | hound | 22 |
| II | Wolf Riders | Cavalry | goblin | 40 |
| II | Wolves | Infantry | wolf | 14 |
| III | Ghouls | Infantry | undead | 45 |
| III | Giant Spiders | Infantry | spider | 40 |
| III | Hobgoblins | Infantry | goblin | 30 |
| IV | Cave Bears | Infantry | bear | 110 |
| IV | Dire Wolf | Infantry | wolf | 60 |
| IV | Goblin Boss | Infantry | goblin | 60 |
| IV | Goblin Chieftain | Infantry | goblin | 90 |
| IV | Ogres | Infantry | ogre | 130 |
| V | Alpha Wolf | Infantry | wolf | 120 |
| V | Necromancer | Infantry | undead | 400 |
| V | Trolls | Infantry | troll | 300 |
| V | Wights | Infantry | undead | 110 |
| VI | Brood Mother | Infantry | spider | 400 |
| VI | Death Knight | Infantry | undead | 260 |
| VI | Elder Troll | Infantry | troll | 500 |
| VI | Goblin Warlord | Infantry | goblin | 180 |
| VI | Wyverns | Infantry | wyvern | 450 |

## Where the tree stops short

- 41 troops reach tier VII. 43 lines end below it: a branch the campaign can climb
  but never crown. A realm fields only its own four lines, so a side branch ending at VI is a dead end
  unless a bridge carries it into a neighbour's tier VII.
- The tree stops being a tree where lines converge: Sworn Brothers (11 ways in), Sword Brethren (3 ways in), Thane Champions (3 ways in), Halberdiers (3 ways in), Royal Halberdiers (3 ways in), Lancers (3 ways in).
- Branches that end early, culture by culture:
  Beast Hunters (III, common), Storm Slingers (IV, southern), Wolf Runners (IV, steppe), Vale Wardens (V, azure_heart), Pass Reivers (V, azure_march), Bandit Chieftains (V, bandit), Corsair Captains (V, bandit), Master Poachers (V, bandit), Outlaw Captains (V, bandit), Summit Wardens (V, crimson_alpine), Compact Duelists (V, crimson_patrician), Reed Stalkers (V, league_fen), Merchant Captains (V, league_lagoon), Fjord Jarls (V, northern_fjord), Taiga Rangers (V, northern_taiga), Chapter Turcopoles (V, order), Sun Paladins (V, order_chapter), Gate Champions (V, order_warden), Mirage Riders (V, southern_dune), Oasis Champions (V, southern_oasis), Rift Captains (V, southern_rift), Horde Champions (V, steppe_horde), Salt Cataphracts (V, steppe_salt), Grand Magisters (VI, azure), Wardens of the Lily (VI, azure), Lords of the Hollow Crown (VI, common), Masters of the Order (VI, common), Crimson Crones (VI, crimson), Thorn Knights (VI, crimson), Admiral's Guard (VI, league), Guild Alchemists (VI, league), Lagoon Wall (VI, league), Lantern Knights (VI, league), Galdr-masters (VI, northern), Winter Wolves (VI, northern), Saints Militant (VI, order), Sages of the Sun (VI, southern), Sultan's Camels (VI, southern), Sun Hawks (VI, southern), Sun-fire Throwers (VI, southern), Sky-Sworn (VI, steppe), Sky-callers (VI, steppe), Tug Lords (VI, steppe)
- `captain` (tier 0) is the player's own man and belongs to no tree, and `beast_hunters` (III) is a
  single tier the campaign fields as it stands, with nothing above or below it.

