using Microsoft.Xna.Framework;

namespace Ironfield;

/// One earned chapter on the captain's illuminated Legend scroll (docs/pre-demo/LEGENDS.md LG1).
public sealed class LegendVerse
{
    public string Id = "", Pillar = "", Title = "", Line = "", Place = "", Icon = "star", Epithet = "", WitnessName = "";
    public int Day, Weight = 1, WitnessSeed;
    public Vector2 Pos;
}

/// Snapshot of a past Legend's signature gear, seeded as a Named Relic into future campaigns (LG3/LG4).
public sealed class LegendGear
{
    public string Def = "", Name = "", From = "";
    public ItemQuality Quality = ItemQuality.Named;
    public List<string> Marks = new();
}

/// A finished or retired captain sealed permanently into `legends.json` (LG3) and snapshotted into new worlds (LG4).
public sealed class SealedLegend
{
    public string Id = "", CaptainName = "", Epithet = "", CompanyName = "", Homeland = "", Fate = "Retired";
    public string AnchorPlace = "", Rank = "SPOKEN OF";
    public int Palette = 2, Arms, Days, Battles, Kills;
    public Vector2 AnchorPos;
    public Face? Portrait;
    public LegendGear? Relic;
    public List<LegendVerse> Verses = new();
    public bool ActiveInWorld = true;
    public DateTime SealedAt;

    public string FullName => Epithet != "" ? $"{CaptainName} {Epithet}" : CaptainName;
}

/// Definition of a Legend Verse and its shadowed "unwritten" prompt.
public sealed record VerseDef(string Id, string Pillar, string Title, string Epithet, string Icon, int Weight, string UnwrittenHint);

/// The Legends system: earning Verses across all pillars of play, computing Epithet & Legend Rank, surfacing Unwritten
/// Verses as diegetic pull, and sealing finished/retired captains into `LegendArchive` (docs/pre-demo/LEGENDS.md).
public sealed partial class Campaign
{
    /// Minimum Verses required to retire early and seal your Legend into the world.
    public const int MinSealVerses = 2;

    public static readonly VerseDef[] AllVerses =
    {
        // Battle & Command
        new("first_blood",    "battle",  "FIRST BLOODED",       "the Blooded",      "sword",  1, "WIN A NOTABLE BATTLE TO BEGIN YOUR CHRONICLE"),
        new("against_odds",   "battle",  "AGAINST THE ODDS",    "the Unyielding",   "shield", 3, "WIN A BATTLE OUTNUMBERED NEARLY TWO TO ONE"),
        new("unbroken_line",  "battle",  "UNBROKEN LINE",       "of the Iron Line", "flag",   3, "LEAD ONE COMPANY THROUGH FIFTEEN BATTLES"),
        new("duelist",        "battle",  "THE SINGLE BLADE",    "the Duellist",     "sword",  2, "MEET AND BEST AN ENEMY CHAMPION IN SINGLE COMBAT"),
        new("banner_renown",  "battle",  "BANNER OF RENOWN",    "the Renowned",     "crown",  3, "EARN TWO BATTLE HONOURS ON ONE COMPANY BANNER"),
        // The Wild
        new("beast_bane",     "wild",    "BEAST-BANE",          "Beast-Bane",       "skull",  2, "BRING DOWN A TROLL, WYVERN OR GREAT BEAST"),
        new("lair_burner",    "wild",    "TORCH OF THE WILDS",  "the Wilder",       "fire",   2, "BURN OUT A MONSTER LAIR OR GOBLIN HOLD"),
        new("apex_slayer",    "wild",    "SLAYER OF LEGENDS",   "Wyrmslayer",       "skull",  4, "HUNT AND SLAY A LEGENDARY APEX OF THE WILDS"),
        new("crisis_breaker", "wild",    "BREAKER OF THE DARK", "the Dawnbringer",  "sun",    5, "BREAK THE WORLD CRISIS AT ITS SEAT"),
        // Siege & Realm
        new("wall_breaker",   "realm",   "WALL-BREAKER",        "Wall-Breaker",     "hammer", 3, "STORM A WALLED CASTLE OR TOWN"),
        new("sworn_banner",   "realm",   "SWORN TO THE REALM",  "the Sworn",        "flag",   1, "EARN YOUR PLACE AS SWORN SWORD OR VASSAL"),
        new("landed_lord",    "realm",   "LORD OF THE KEEP",    "the Landed",       "crown",  3, "HOLD A FIEF IN YOUR OWN RIGHT"),
        new("crown_taken",    "realm",   "THE CROWN TAKEN",     "the Sovereign",    "crown",  5, "FOUND YOUR OWN REALM OR WIN THE WAR"),
        // Company & Nobility
        new("maker_knights",  "company", "MAKER OF KNIGHTS",    "the Noble",        "crown",  3, "KNIGHT A NAMED VETERAN FROM YOUR ROLL"),
        new("from_ranks",     "company", "FROM THE RANKS",      "the Captain",      "helm",   2, "SEE A NAMED SOLDIER RISE TO COMPANION"),
        new("remembered",     "company", "REMEMBERED DEAD",     "the Mourner",      "heart",  2, "HONOUR A TEN-BATTLE VETERAN WHO FELL UNDER YOU"),
        // Arcana & Orders
        new("awakened",       "arcana",  "THE AWAKENED",        "the Kindled",      "star",   2, "KINDLE YOUR AURA OR ENTER A KNIGHT ORDER"),
        new("order_founder",  "arcana",  "FOUNDER OF THE ORDER","the Grand Master", "sun",    4, "FOUND YOUR OWN KNIGHT ORDER OR FORGE A FORM"),
    };

    static readonly Dictionary<string, VerseDef> VerseById = AllVerses.ToDictionary(v => v.Id);

    public int LegendWeight => S.Verses.Sum(v => v.Weight);

    public string LegendRank => LegendWeight switch
    {
        >= 14 => "MYTH OF THE REALM",
        >= 9  => "LIVING LEGEND",
        >= 5  => "SUNG IN TAVERNS",
        >= 2  => "SPOKEN OF",
        _     => "NAMELESS",
    };

    /// The captain's earned epithet from their highest-weight Verse (newest breaks ties).
    public string LegendEpithet => S.Verses.Count == 0 ? ""
        : S.Verses.OrderByDescending(v => v.Weight).ThenByDescending(v => v.Day).First().Epithet;

    public string CaptainDisplayName => Me.Name.Equals("You", StringComparison.OrdinalIgnoreCase) ? "The Captain" : Me.Name;
    public string CaptainFullName => LegendEpithet != "" ? $"{CaptainDisplayName} {LegendEpithet}" : CaptainDisplayName;

    /// Record a Legend Verse once per captain. Idempotent: duplicate ids are ignored.
    public bool RecordVerse(string id, string place, Vector2 pos, string? customLine = null, string? witnessName = null, int witnessSeed = 0)
    {
        if (!VerseById.TryGetValue(id, out var def)) return false;
        if (S.Verses.Any(v => v.Id == id)) return false;
        string where = string.IsNullOrWhiteSpace(place) ? BattleName(pos) : place;
        string line = customLine ?? $"{def.Title} at {where}.";
        S.Verses.Add(new LegendVerse
        {
            Id = def.Id,
            Pillar = def.Pillar,
            Title = def.Title,
            Line = line,
            Place = where,
            Pos = pos,
            Day = Day,
            Icon = def.Icon,
            Epithet = def.Epithet,
            Weight = def.Weight,
            WitnessName = witnessName ?? "",
            WitnessSeed = witnessSeed,
        });
        LogYours($"A new verse on your Legend: {def.Title} - {line}");
        Deed($"Legend Verse: {def.Title} - {line}", "legend");
        return true;
    }

    /// Check continuous campaign state milestones so no eligible Verse is ever missed.
    public void SyncLegendVerses()
    {
        if (Player == null) return;
        string here = SettlementNear(Player.Pos)?.Name ?? BattleName(Player.Pos);
        if (Player.Companies.Any(c => c.Battles >= 15))
        {
            var c = Player.Companies.OrderByDescending(x => x.Battles).First();
            RecordVerse("unbroken_line", here, Player.Pos, $"{c.Name} stood unbroken through {c.Battles} battles.");
        }
        if (Player.Companies.Any(c => c.BattleHonours.Count >= 2))
        {
            var c = Player.Companies.OrderByDescending(x => x.BattleHonours.Count).First();
            RecordVerse("banner_renown", here, Player.Pos, $"{c.Name} carried {c.BattleHonours.Count} honours on their banner.");
        }
        if (Captain.Fealty || (Captain.Contract >= 0 && Captain.BestRank >= 1))
        {
            string realm = Captain.Contract >= 0 && Captain.Contract < FactionNames.Length ? FactionNames[Captain.Contract] : "the realm";
            RecordVerse("sworn_banner", here, Player.Pos, $"Swore the banner to {realm}.");
        }
        var fief = Fiefs.FirstOrDefault();
        if (fief != null)
            RecordVerse("landed_lord", fief.Name, fief.Pos, $"Held {fief.Name} in your own name.");
        if (S.Won || (Captain.Contract >= 0 && Captain.Contract < S.Factions.Count && S.Factions[Captain.Contract].Founded))
        {
            string realm = Captain.Contract >= 0 && Captain.Contract < FactionNames.Length ? FactionNames[Captain.Contract] : here;
            RecordVerse("crown_taken", here, Player.Pos, $"Raised the crown of {realm}.");
        }
        var risen = Companions.FirstOrDefault(k => k.Ranks != null);
        if (risen != null)
            RecordVerse("from_ranks", here, Player.Pos, $"{risen.Name} rose from the ranks to ride beside you.", risen.Name, risen.Id);
        var fallen = S.Fallen.FirstOrDefault(m => m.Battles >= 10);
        if (fallen != null)
            RecordVerse("remembered", here, Player.Pos, $"{fallen.Name} fell after {fallen.Battles} battles and was remembered.", fallen.Name, fallen.Id);
        if (Captain.AuraTier >= 1 || Captain.Order != "")
            RecordVerse("awakened", here, Player.Pos, Captain.Order != "" ? "Took the vows of a knightly order." : "Kindled the aura within.");
        if (S.OwnOrder != null || Captain.OwnForm != null)
            RecordVerse("order_founder", here, Player.Pos, S.OwnOrder != null ? $"Founded the {S.OwnOrder.Name}." : "Forged a battle form of your own.");
    }

    /// Up to `max` unearned Verses, prioritizing pillars the player has not yet touched.
    public List<VerseDef> UnwrittenVerses(int max = 4)
    {
        var have = S.Verses.Select(v => v.Id).ToHashSet();
        var pillarCounts = S.Verses.GroupBy(v => v.Pillar).ToDictionary(g => g.Key, g => g.Count());
        return AllVerses
            .Where(v => !have.Contains(v.Id))
            .OrderBy(v => pillarCounts.GetValueOrDefault(v.Pillar, 0))
            .ThenBy(v => v.Weight)
            .Take(max)
            .ToList();
    }

    /// Whether the player may retire early and seal their Legend into the world.
    public string? CannotSealLegend() => S.GameOver ? "THE WAR IS OVER"
        : Encounter != null ? "NOT WITH AN ENEMY IN FRONT OF YOU"
        : S.Verses.Count < MinSealVerses ? $"EARN AT LEAST {MinSealVerses} LEGEND VERSES FIRST ({S.Verses.Count}/{MinSealVerses})"
        : null;

    /// Retire and seal the current Legend into `legends.json` (available once `MinSealVerses` are earned).
    public string? SealAndRetire()
    {
        if (CannotSealLegend() is string why) return why;
        SyncLegendVerses();
        string fate = Captain.Contract >= 0 && Captain.Contract < S.Factions.Count && S.Factions[Captain.Contract].Founded ? "Crowned" : "Retired";
        SealCurrentLegend(fate);
        Deed($"{CaptainFullName} hung up the sword. Their Legend is sealed ({S.Verses.Count} verses).", "legend");
        S.Won = true;
        S.Retired = true;
        S.GameOver = true;
        S.EndReason = $"YOU SEAL YOUR LEGEND AS {CaptainFullName.ToUpperInvariant()}. FUTURE WORLDS WILL REMEMBER.";
        return null;
    }

    /// Snapshot and save the current captain's Legend to `LegendArchive` (idempotent per campaign unless `force` for HandOver).
    public SealedLegend? SealCurrentLegend(string fate, bool force = false)
    {
        if (S.LegendSealed && !force) return null;
        if (S.Verses.Count == 0 && !force) return null;
        SyncLegendVerses();
        var anchor = S.Verses.OrderByDescending(v => v.Weight).ThenByDescending(v => v.Day).FirstOrDefault();
        int pal = Captain.Contract >= 0 ? Style.PaletteOf(Captain.Contract) : (Captain.Homeland >= 0 ? Style.PaletteOf(Captain.Homeland) : 2);
        string homeland = Captain.Homeland >= 0 && Captain.Homeland < FactionNames.Length ? FactionNames[Captain.Homeland] : "the Free Marches";
        var sig = PickSignatureGear(anchor?.Place ?? homeland);
        var legend = new SealedLegend
        {
            Id = string.IsNullOrEmpty(S.CampaignId) ? $"leg-{S.Seed}-{Day}-{S.Generation}" : $"{S.CampaignId}-g{S.Generation}",
            CaptainName = CaptainDisplayName,
            Epithet = LegendEpithet,
            CompanyName = Player.Name,
            Homeland = homeland,
            Fate = fate,
            Rank = LegendRank,
            Palette = pal,
            Arms = Captain.Arms,
            Days = Day,
            Battles = S.Tally.GetValueOrDefault("battles", Player.Companies.Sum(c => c.Battles)),
            Kills = Player.Companies.Sum(c => c.Kills),
            AnchorPlace = anchor?.Place ?? (SettlementNear(Player.Pos)?.Name ?? "the borderlands"),
            AnchorPos = anchor != null && anchor.Pos != Vector2.Zero ? anchor.Pos : Player.Pos,
            Portrait = PortraitPainter.Of(this, Me),
            Relic = sig,
            Verses = S.Verses.Select(v => new LegendVerse
            {
                Id = v.Id, Pillar = v.Pillar, Title = v.Title, Line = v.Line, Place = v.Place,
                Icon = v.Icon, Epithet = v.Epithet, WitnessName = v.WitnessName,
                Day = v.Day, Weight = v.Weight, WitnessSeed = v.WitnessSeed, Pos = v.Pos
            }).ToList(),
            ActiveInWorld = true,
            SealedAt = DateTime.UtcNow,
        };
        LegendArchive.Add(legend);
        if (!force) S.LegendSealed = true;
        return legend;
    }

    LegendGear? PickSignatureGear(string anchorPlace)
    {
        // Prefer the captain's finest equipped weapon, helm, or armour.
        string[] candidates = { Captain.Kit.Weapon, Captain.Kit.Helm, Captain.Kit.Body, Captain.Kit.Shield };
        string? defId = candidates.FirstOrDefault(id => !string.IsNullOrEmpty(id) && id != "none" && Gear.Find(id) != null);
        if (defId == null) return null;
        var g = Gear.Find(defId)!;
        string first = CaptainDisplayName == "The Captain" ? "Captain" : CaptainDisplayName.Split(' ')[0];
        return new LegendGear
        {
            Def = g.Id,
            Name = $"{first}'s {g.Name}",
            From = $"Borne by {CaptainFullName} at {anchorPlace}.",
            Quality = ItemQuality.Named,
        };
    }
}
