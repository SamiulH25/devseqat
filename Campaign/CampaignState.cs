using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

// Persistent campaign data: plain classes with public fields, serialised as JSON (docs/CAMPAIGN.md 10.3).
// Nothing here references MonoGame graphics: the self-test runs whole campaigns headless.

public enum SettlementKind { Village, Town, Castle }
public enum PartyKind { Player, Lord, Bandit, Garrison }
public enum Perk { Bannerman, WarCry, Tactician, Quartermaster, Surgeon, Drillmaster, Horsemaster, IronWill }

/// A persistent company (docs/CAMPAIGN.md 4.1). Xp is the average per fit man.
public sealed class CompanyRecord
{
    public int Id, Men, Wounded, Battles, Kills;
    public string Name = "", Troop = "", Role = "";
    public float Xp, Cohesion = 10;
    /// The company's sergeant (B5): a name and up to three traits; "" = none yet.
    public string Officer = "";
    public List<string> Traits = new();

    public UnitType Type => Troops.Get(Troop);
}

/// Captured men held by a party (B3).
public sealed class PrisonerGroup
{
    public string Troop = "";
    public int Count;
}

/// A contract from a faction's steward (B4). Kind: hunt (a lord), clear (brigands), defend (a village), scout (a town), take (a castle or town).
public sealed class Contract
{
    public int Id, Target, Faction;
    public string Kind = "", Text = "";
    public float Deadline, Reward, Progress;
}

public sealed class Party
{
    public int Id, Faction = -1, HomeId = -1, ChaseId = -1, GoalSettlement = -1, PatrolProvince = -1, Horses, Tier = 1;
    public string Name = "", Task = "patrol";
    public PartyKind Kind;
    public Vector2 Pos, Goal;
    public bool Moving, Down, Resting;
    public float Gold, Cooldown, DownUntil, TaskUntil, Facing;
    public List<CompanyRecord> Companies = new();
    public List<PrisonerGroup> Prisoners = new();
    public List<int> Captives = new();   // lords you hold for ransom
    public bool Captive;                 // a lord held by you
    public float Food = 10;              // days of food (yours; lords are fed by their faction)
    public int SiegeTarget = -1;         // the settlement this party is besieging

    public int Men => Companies.Sum(c => c.Men);
    public int Wounded => Companies.Sum(c => c.Wounded);
}

public sealed class Settlement
{
    public int Id, Province, Faction, Recruits, Horses;
    public string Name = "";
    public SettlementKind Kind;
    public Vector2 Pos;
    public float ThreatUntil;
    public List<CompanyRecord> Mercenaries = new();
    public List<CompanyRecord> Garrison = new();   // towns and castles (B1)
    public List<Contract> Offers = new();          // the steward's contract board (towns)
    public float Supplies = 30;                    // days a garrison can hold out under siege
    public int Owner = -1;                         // party id of the holder of the fief (-1: the faction)
    public bool Besieged;
    public float SiegeSince;                       // when the siege began (ladders take 12 hours)
}

public sealed class Province
{
    public int Id, Faction;
    public string Name = "", Kind = "Meadow";
    public Vector2 Center;
    public List<int> Neighbors = new();
}

public sealed class FactionState
{
    public int Id;
    public string Name = "";
    public float Treasury, WarScore;
}

/// The state between two factions (B6): at war, or in truce until a time. Score: what each side has won from the other.
public sealed class War
{
    public int A, B;
    public bool Active = true;
    public float TruceUntil, ScoreA, ScoreB;
}

public sealed class CaptainState
{
    public float Renown = 50, WoundedUntil;
    public int ArmorTier, HelmTier, HorseTier, Contract = 0; // Contract: faction you serve, -1 = none
    public List<Perk> Perks = new();
}

public sealed class LogEntry
{
    public double At;
    public string Text = "";
}

public sealed class CampaignState
{
    public const int CurrentVersion = 3;
    public int Version = CurrentVersion, Seed, NextId = 1, UnpaidWeeks;
    /// How far the provinces of this save's map reach (WorldMap.Extent for new campaigns; older saves have their own).
    public float MapExtent = WorldMap.LegacyExtent;
    public double Hours;
    public ulong Rng;
    public float Gold, LastWeekBalance, TruceUntil;
    public bool Ironman, GameOver;
    public bool? AtWar;                         // version 1 only (one war); read by Migrate
    public List<War> Wars = new();
    public CaptainState Captain = new();
    public List<Party> Parties = new();
    public List<Settlement> Settlements = new();
    public List<Province> Provinces = new();
    public List<FactionState> Factions = new();
    public List<LogEntry> Log = new();
    public List<Contract> Contracts = new();   // the contracts you have accepted
    public int LastBattleFoe = -1;             // the last party you defeated (for hunt contracts)

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static CampaignState Deserialize(string json)
    {
        var s = JsonSerializer.Deserialize<CampaignState>(json, Json) ?? throw new InvalidDataException("empty save");
        if (s.Version > CurrentVersion || s.Version < 1) throw new InvalidDataException($"save version {s.Version} is not supported (this build reads 1-{CurrentVersion})");
        s.Migrate();
        return s;
    }

    /// Bring an older save up to this version (roadmap E2). Each step upgrades one version.
    public void Migrate()
    {
        if (Version == 1)
        {
            // v1 had one war between the first two factions; v2 keeps a war per pair (and the Northmarch only in new campaigns).
            Wars = new List<War> { new() { A = 0, B = 1, Active = AtWar ?? true, TruceUntil = TruceUntil, ScoreA = Factions.ElementAtOrDefault(0)?.WarScore ?? 0, ScoreB = Factions.ElementAtOrDefault(1)?.WarScore ?? 0 } };
            AtWar = null;
            Version = 2;
        }
        if (Version == 2)
        {
            // v3 doubled the campaign map. A v2 campaign was played on the small one, so it keeps that geography
            // (its provinces and settlements are in the save anyway; this keeps the ground under them the same).
            MapExtent = WorldMap.LegacyExtent;
            Version = 3;
        }
    }

    public static string SaveDir => Environment.GetEnvironmentVariable("IRONFIELD_SAVES") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ironfield", "saves");

    /// Write to a temporary file first and keep the previous save as .bak, so a crash mid-save never loses a campaign.
    public void Save(string slot)
    {
        Directory.CreateDirectory(SaveDir);
        string p = Path.Combine(SaveDir, slot + ".json"), tmp = p + ".tmp";
        File.WriteAllText(tmp, Serialize());
        if (File.Exists(p)) File.Copy(p, p + ".bak", true);
        File.Move(tmp, p, true);
    }

    /// True after Load had to fall back to the .bak file.
    public static bool RestoredFromBackup;

    public static CampaignState? Load(string slot)
    {
        var p = Path.Combine(SaveDir, slot + ".json");
        RestoredFromBackup = false;
        if (!File.Exists(p)) return null;
        try { return Deserialize(File.ReadAllText(p)); }
        catch (Exception e) when (File.Exists(p + ".bak"))
        {
            Console.WriteLine($"save {slot} unreadable ({e.Message}); restoring the backup");
            RestoredFromBackup = true;
            return Deserialize(File.ReadAllText(p + ".bak"));
        }
    }

    public static bool Exists(string slot) => File.Exists(Path.Combine(SaveDir, slot + ".json"));
}

/// Deterministic random numbers whose state lives in the save (System.Random cannot be saved). xorshift64*.
public sealed class Rng
{
    readonly CampaignState s;
    public Rng(CampaignState state) { s = state; if (s.Rng == 0) s.Rng = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)s.Seed; }

    public ulong NextU()
    {
        ulong x = s.Rng;
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        s.Rng = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    public float Float() => (NextU() >> 40) / (float)(1UL << 24);
    public int Next(int max) => max <= 0 ? 0 : (int)(NextU() % (ulong)max);
    public int Next(int min, int max) => min + Next(max - min);
    public float Range(float a, float b) => a + (b - a) * Float();
    public bool Chance(float p) => Float() < p;
    public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];
}

/// Campaign balance numbers (Data/campaign.json overrides these defaults).
public sealed class Balance
{
    public float HourSeconds = 0.5f, FootSpeed = 5.5f, HorseSpeed = 9, RoadBonus = 1.3f, Vision = 55, EncounterRadius = 3.5f, JoinRadius = 10;
    /// How much closer two AI parties must be before they fight (the player's own encounters use EncounterRadius).
    public float AiClashRadius = 5.5f;
    public float StartGold = 300, StartRenown = 50;
    public float UpkeepRate = 0.1f, ContractPay = 0.85f, LootRate = 0.25f, RansomRate = 1, MercMarkup = 1.5f;
    public float WoundBase = 0.3f, WoundArmor = 40, WoundMin = 0.25f, WoundMax = 0.7f, CaptureChance = 0.5f, FledReturnOnDefeat = 0.7f;
    public float XpParticipation = 20, XpVictory = 1.5f, XpPerKillTier = 12;
    public float RenownPerVictory = 5, RenownStrengthDivisor = 50, PerkEvery = 100;
    public float CompanySlotsBase = 10, CompanySlotRenown = 0, MenBase = 40, MenRenownDivisor = 5;
    public int VillageRecruitsMax = 8, LordsPerFaction = 8, BanditsMax = 80;
    /// A bandit band appears this many times a day while below BanditsMax (0-1) — the roll for each attempt.
    public float BanditSpawnChance = 0.95f;
    /// How many bands the world tries to raise each day (while under BanditsMax), so a big map fills up quickly.
    /// It scales with the map's area, so a bigger world stays populated: Campaign.BanditSpawnsWanted.
    public int BanditSpawnsPerDay = 5;
    /// Lords per faction and bandit bands are quoted per the original 26-province map and multiply by how much world there
    /// is (Campaign.LordsWanted, Campaign.BanditsWanted), so a realm has as many parties as its ground needs.
    /// Bandit bands are rolled 1-3 strong, and gain a man for every this many days of campaign (they grow as you do).
    public int BanditBandMin = 10, BanditBandMax = 20;
    public float BanditGrowPerDays = 9;
    public float BanditTierPerDays = 45;
    /// Daily chance (0-1) that a lord who sees no enemy province to raid marches to a neighbouring faction's front instead:
    /// how often the AI picks a fight on its own.
    public float LordAggression = 0.35f;
    public float HealField = 0.08f, HealTown = 0.25f, DrillCamp = 1, DrillTown = 2;
    public float TownIncome = 150, CastleIncome = 90, VillageIncome = 40, LordWage = 1.3f;
    public float TruceDays = 30, PeaceScore = 1500, BountyBandits = 50, BountyLord = 120;
    public float HorseCostRate = 0.5f, ArmorCost = 600, HelmCost = 300, WarhorseCost = 400, DestrierCost = 900;

    static Balance? loaded;
    public static Balance Get()
    {
        if (loaded != null) return loaded;
        loaded = new Balance();
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = Path.Combine(dir, "Data", "campaign.json");
            if (!File.Exists(p)) continue;
            loaded = JsonSerializer.Deserialize<Balance>(File.ReadAllText(p), new JsonSerializerOptions { IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip })!;
            break;
        }
        return loaded;
    }
}

/// Name lists (Data/names.json).
public sealed class Names
{
    public List<string> Places = new(), Lords = new(), Epithets = new(), Bandits = new();
    public Dictionary<string, List<string>> Nouns = new();

    static Names? loaded;
    public static Names Get()
    {
        if (loaded != null) return loaded;
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = Path.Combine(dir, "Data", "names.json");
            if (!File.Exists(p)) continue;
            return loaded = JsonSerializer.Deserialize<Names>(File.ReadAllText(p), new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true })!;
        }
        throw new FileNotFoundException("Data/names.json not found");
    }
}
