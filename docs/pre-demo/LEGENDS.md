# Legends: Living Chronicle & Permanent World Legacy (LG1-LG4)

> **Core purpose:** Give the player a compelling, diegetic reason to engage with every system in Ironfield (battles, duels, the wild, sieges, nobility, camp, arcana, politics), celebrate their story in an illuminated parchment scroll, and make every finished or retired captain a permanent, playable part of future worlds.

## Decisions (owner, 2026-11-24)

1. **Sealing rules (A + B):** A Legend seals automatically on **Victory, Ironman Death, or Hand-Over**, AND the player may **Retire & Seal Legend** early once they have earned at least **2 Verses** (so both full campaigns and shorter demo runs leave a permanent mark on the world).
2. **World impact (A — Atmospheric + Light Mechanics):** Past sealed Legends persist into future campaigns as **Map Monuments / Cairns** (visitable for custom Event Book storylets & blessings), **Discoverable Named Relics** (their signature gear), and **Folk Memory** (bard songs in taverns, lord greetings, and event references).
3. **Storage:** Shared `legends.json` in the saves root (`Saves.Root`, respecting `IRONFIELD_SAVES` so tests and screenshots stay isolated).

---

## LG1. Legend Verses, Epithets & Live Pull

**Files:** `Campaign/Legends.cs`, `Campaign/CampaignState.cs`, hooks in `CampaignBattle.cs`, `Campaign/Wild.cs`, `Campaign/Crisis.cs`, `Campaign/Nobility.cs`, `Campaign/CampaignSiege.cs`, `Campaign/OwnOrder.cs`, `Campaign/OwnForm.cs`, `Campaign/Duels.cs`, `Campaign/Legacy.cs`, `Campaign/Career.cs`.

- **Data model (`LegendVerse` in `CampaignState.Verses`):**
  - `Id`, `Pillar` (`"battle"`, `"wild"`, `"realm"`, `"company"`, `"arcana"`), `Title`, `Line` (poetic 1-line chronicle), `Day`, `Place`, `Pos`, `Icon`, `Epithet`, `Weight` (1–5), `WitnessName`, `WitnessFaceSeed`.
- **Curated Verses across all 5 pillars (16 Verses):**
  - *Battle & Command:* First Blooded (first notable win), Against the Odds (win outnumbered 1.6x+), Unbroken Line (a company reaches 15+ battles), Duelist (win a pre-battle single combat), Banner of Renown (a company earns 2+ Battle Honours).
  - *The Wild:* Beast-Bane (slay a troll/wyvern/major beast), Torch of the Wilds (burn a growing lair), Apex-Slayer (slay a Legendary named apex), Breaker of the Crisis (defeat the World Crisis seat).
  - *Siege & Realm:* Wall-Breaker (storm a castle or town), Landed Lord (hold a fief), Sworn Banner (earn Sworn Sword / Vassal), Crown Taken (found an independent realm or win the war).
  - *Company & Nobility:* Maker of Knights (knight a named veteran), From the Ranks (a veteran rises to companion), Remembered Dead (a 10-battle veteran falls and is honoured).
  - *Arcana & Orders:* The Awakened (reach Kindled / Aura Knight), Founder of the Order (found Own Order or Own Form).
- **Dynamic Epithet & Legend Rank:**
  - Highest-weight Verse grants the captain's live **Epithet** (e.g., *Aldric Wyrmslayer*, *Aldric Wall-Breaker*, *Aldric the Unbroken*).
  - **Legend Rank** scales by total Verse weight: *Nameless (0)* → *Spoken Of (2+)* → *Sung in Taverns (5+)* → *Living Legend (9+)* → *Myth of the Realm (14+)*.
- **Unwritten Verses (Diegetic Pull):**
  - `Campaign.UnwrittenVerses(int max = 4)` returns shadowed parchment hints for pillars the player hasn't earned yet (e.g., *"No great beast has fallen to your hand"*, *"No man of yours has been knighted"*, *"No wall has broken before your engines"*).
- **Live World Reaction:**
  - Reaching *Sung in Taverns+* gives a modest tavern cohesion/recruit bonus and unlocks a bard line referencing your latest Verse.

---

## LG2. The Illuminated Legend Scroll & Title Screen Hall of Legends

**Files:** `MapScreen.Legend.cs`, `LegendScreen.cs`, `TitleScreen.cs`, `MapScreen.Hud.cs`, `Controls.cs`.

- **In-Run Illuminated Scroll (`MapScreen.Legend.cs`):**
  - Reuses the overhauled parchment scroll renderer (`ui.Scroll`, `ui.Ink`, `ui.InkProse`, `ui.FaceHead`, `ui.Shield`, `ui.Clip`).
  - **Header:** Captain's painted portrait, Company Heraldry shield, Full Name + Earned Epithet, Legend Rank, Days/Battles/Kills, and a coloured **Wax Seal** (Amber = Living, Gold = Retired/Victorious, Crimson = Fallen, Azure = Crowned).
  - **Chronicle Body:** Scrollable list of earned Verses — each rendered as a gilded parchment block with icon, Verse title, poetic chronicle line, day & place, and witness face (when present).
  - **Footer:** 3–4 faint sepia **Unwritten Verses** pulling the player toward untouched systems, plus **"RETIRE & SEAL LEGEND"** (enabled once `Verses.Count >= 2`, with a two-click confirmation).
  - Accessible anytime via a dedicated Campaign HUD button / hotkey (and linked from Camp's Legend view).
- **Title Screen Hall of Legends (`LegendScreen.cs`):**
  - New `HALL OF LEGENDS` button on `TitleScreen.cs` (enabled whenever `legends.json` has at least 1 sealed Legend).
  - Left column: list of past sealed Legends (heraldry shield, captain name + epithet, fate badge, active-in-world toggle).
  - Right pane: full Illuminated Scroll preview of the selected Legend, plus `ACTIVE IN NEW WORLDS: ON/OFF` toggle and `DELETE`.

---

## LG3. Sealing & Persistent World Chronicle (`legends.json`)

**Files:** `Campaign/LegendArchive.cs`, `Campaign/Legacy.cs`, `MapScreen.Dialogs.cs`.

- **`SealedLegend` record** persisted to `Path.Combine(Saves.Root, "legends.json")`:
  - `Id`, `CaptainName`, `Epithet`, `CompanyName`, `Palette`, `Arms`, `Face` (recipe), `Fate` (`Retired` / `Victorious` / `Crowned` / `Fallen` / `HandedOver`), `Days`, `Battles`, `Kills`, `Homeland`, `AnchorPos`, `AnchorPlace`, `SignatureGear` (name, slot, base gear id, bonus stat, lore line), `Verses`, `ActiveInWorld`, `SealedAt`.
- **Sealing Triggers:**
  - `Campaign.SealAndRetire()` — player clicks **Retire & Seal Legend** (available at `Verses.Count >= 2`, or full retire via `Legacy.cs`).
  - Auto-seal on **War Won**, **Crisis Broken**, **Ironman Death / Game Over**, and **Hand-Over** (outgoing captain seals their scroll; successor continues with a fresh scroll).
  - Guarded by `CampaignState.LegendSealed` so a campaign never double-seals if reloaded.

---

## LG4. Permanent World Impact (Monuments, Relics & Folk Memory)

**Files:** `Campaign/LegendWorld.cs`, `Campaign/Campaign.cs` (`New`), `Campaign/Sites.cs`, `Campaign/EventDirector.cs`, `Campaign/Talk.cs`, `MapScreen.Town.cs`.

- **1. Permanent Map Monuments / Cairns:**
  - On `Campaign.New`, up to **4 active Sealed Legends** spawn permanent **Legend Monuments** (`Site.LegendId`) near their defining `AnchorPos` (clamped to valid land) — named e.g. *"Aldric's Stand"*, *"Cairn of the Red Lances"*.
  - Visiting a Legend Monument opens a custom **Event Book** page displaying the past captain's portrait, fate, and defining Verse, with choices (e.g., *Pay respects* → company cohesion + morale; *Swear by their deed* → renown + small local volunteer muster; *Search the cairn* → chance to claim their relic if unfound).
- **2. Discoverable Named Relics:**
  - Up to **2 active Sealed Legends'** `SignatureGear` items are seeded into the new world as **Named Relics** (e.g., *Aldric's Sallet — "Worn at the walls of Lowmoor"*): either resting at their Monument, guarded in a ruin, or available as a rare heirloom at their old homeland town's Armourer.
- **3. Folk Memory (Taverns & Greetings):**
  - Town Tavern tab displays a *"SUNG BY THE HEARTH"* bard verse celebrating a random active past Legend.
  - `Talk.Greeting` occasionally references an active past Legend of the same homeland/realm (*"You march out of Brevon — men still sing of Aldric Wyrmslayer"*).
- **Determinism & Testing (`Tests/LegendTests.cs`):**
  - `Campaign.New` snapshots active legends into `CampaignState.WorldLegends` at campaign creation so saves and replays remain 100% deterministic even if `legends.json` changes later!
  - Tests cover: Verse earning across all 5 pillars, Epithet/Rank updates, early Retire & Seal at 2 Verses, `legends.json` persistence under isolated `IRONFIELD_SAVES`, and Monument + Relic + Folk Memory injection in a new campaign.
