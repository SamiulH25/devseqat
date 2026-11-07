using System.Text.Json;

namespace Ironfield;

/// The troop registry, loaded once from Data/troops.json (copied next to the game).
/// Troops form upgrade trees (UpgradesTo, rising Tier); the campaign layer will upgrade companies along them.
public static class Troops
{
    public const string FileName = "Data/troops.json";
    static Dictionary<string, UnitType>? troops;
    static Dictionary<string, Mount>? mounts;

    public static IReadOnlyCollection<UnitType> All { get { Load(); return troops!.Values; } }
    public static UnitType Get(string id) { Load(); return troops!.TryGetValue(id, out var t) ? t : throw new KeyNotFoundException("no troop " + id); }
    public static Mount Mount(string id) { Load(); return mounts!.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException("no mount " + id); }

    // Data shapes of troops.json (public fields; names match the JSON keys, case-insensitively).
    sealed class MountDto
    {
        public string Id = "", Name = "", Barding = "saddle";
        public float Hp, Trot, Gallop, Accel, TurnRate;
    }

    sealed class LookDto
    {
        public string Head = "none", Body = "tunic", Legs = "cloth", Feet = "boots", Hands = "skin", Shield = "none", Weapon = "sword";
        public bool Tabard, Plume, Cape;
    }

    sealed class TroopDto
    {
        public string Id = "", Name = "", Class = "Infantry", Culture = "common";
        public string? Mount;
        public int Tier, Cost, UpgradeXp, Ammo;
        public string[] UpgradesTo = Array.Empty<string>();
        public float Hp, Damage, Armor, Reach, Windup, Recover, Speed, Block, Range, VsMounted = 1, Pierce, Reload = 1;
        public bool Leader;
        public LookDto Look = new();
    }

    sealed class FileDto
    {
        public List<MountDto> Mounts = new();
        public List<TroopDto> Troops = new();
    }

    static string Path()
    {
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = System.IO.Path.Combine(dir, FileName);
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("troop data not found", FileName);
    }

    static void Load()
    {
        if (troops != null) return;
        var opts = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(Path()), opts)!;
        mounts = file.Mounts.ToDictionary(m => m.Id, m => new Mount(m.Id, m.Name, m.Hp, m.Trot, m.Gallop, m.Accel, m.TurnRate, m.Barding));
        troops = new Dictionary<string, UnitType>();
        foreach (var d in file.Troops)
        {
            var l = d.Look;
            troops[d.Id] = new UnitType
            {
                Id = d.Id, Name = d.Name, Culture = d.Culture, Class = Enum.Parse<UnitClass>(d.Class, true), Tier = d.Tier, Cost = d.Cost, UpgradeXp = d.UpgradeXp,
                UpgradesTo = d.UpgradesTo, MaxHp = d.Hp, Damage = d.Damage, Armor = d.Armor, Reach = d.Reach, Windup = d.Windup,
                Recover = d.Recover, Speed = d.Speed, BlockChance = d.Block, Range = d.Range, Ammo = d.Ammo, VsMounted = d.VsMounted, Pierce = d.Pierce, Reload = d.Reload,
                Leader = d.Leader, Mount = d.Mount == null ? null : mounts[d.Mount],
                Look = new Look(l.Head, l.Body, l.Legs, l.Feet, l.Hands, l.Shield, l.Weapon, l.Tabard, l.Plume, l.Cape),
            };
        }
        var problems = Validate().ToList();
        if (problems.Count > 0) throw new InvalidDataException("troops.json: " + string.Join("; ", problems));
    }

    /// Data checks: links exist, upgrades raise the tier, every look part has sprites, cavalry has horses.
    public static IEnumerable<string> Validate()
    {
        foreach (var t in troops!.Values)
        {
            foreach (var u in t.UpgradesTo)
                if (!troops.TryGetValue(u, out var next)) yield return $"{t.Id} upgrades to unknown {u}";
                else if (next.Tier <= t.Tier) yield return $"{t.Id} -> {u} does not raise the tier";
            if ((t.Class == UnitClass.Cavalry) != (t.Mount != null)) yield return $"{t.Id}: cavalry needs a mount and only cavalry has one";
            if (t.Culture is not ("common" or "azure" or "crimson" or "northern")) yield return $"{t.Id}: unknown culture {t.Culture}";
            if (t.Class == UnitClass.Archer && (t.Ammo <= 0 || t.Range <= 0)) yield return $"{t.Id}: archers need ammo and range";
            if (Look.Ranged(t.Look.Weapon) != t.Ammo > 0) yield return $"{t.Id}: a bow needs ammo, and only bows have it";
            var l = t.Look;
            if (!Look.Heads.Contains(l.Head)) yield return $"{t.Id}: unknown head {l.Head}";
            if (!Look.Bodies.Contains(l.Body)) yield return $"{t.Id}: unknown body {l.Body}";
            if (!Look.LegsKinds.Contains(l.Legs)) yield return $"{t.Id}: unknown legs {l.Legs}";
            if (!Look.FeetKinds.Contains(l.Feet)) yield return $"{t.Id}: unknown feet {l.Feet}";
            if (!Look.HandsKinds.Contains(l.Hands)) yield return $"{t.Id}: unknown hands {l.Hands}";
            if (!Look.Shields.Contains(l.Shield)) yield return $"{t.Id}: unknown shield {l.Shield}";
            if (!Look.Weapons.Contains(l.Weapon)) yield return $"{t.Id}: unknown weapon {l.Weapon}";
        }
        foreach (var m in mounts!.Values)
            if (m.Barding is not ("saddle" or "caparison")) yield return $"{m.Id}: unknown barding {m.Barding}";
    }

    /// Every troop reachable from `id` by upgrading (including itself).
    public static IEnumerable<UnitType> Tree(string id)
    {
        var t = Get(id);
        yield return t;
        foreach (var u in t.UpgradesTo)
            foreach (var d in Tree(u)) yield return d;
    }
}

/// A company in an army: what the campaign map hands to a battle. Role ("infantry", "archers", "spears", "horse")
/// decides where it deploys and how the AI uses it; empty = from the troop class.
/// Id and Name tie it to the campaign's CompanyRecord (0 = a one-off battle); Morale is its starting morale.
/// Troop is the company's face (its largest stack) and Count its total; Mix lists every stack (troop, men) when the company
/// holds more than one troop type (CAMPAIGN_OVERHAUL.md 2.3). RoutBonus and VsMounted come from its honours.
public sealed record Company(string Troop, int Count, string Role = "", int Id = 0, string Name = "", float Morale = 70, string[]? Traits = null, bool Allied = false,
    StackSpec[]? Mix = null, float RoutBonus = 0, float VsMounted = 1, int Banner = -1, string Hero = "", int Division = -1)
{
    public bool Has(string trait) => Traits?.Contains(trait) == true;
    /// The men to spawn, largest stack first (so a squad's first man is its majority troop).
    public IEnumerable<StackSpec> Stacks => Mix is { Length: > 0 } m ? m.Where(x => x.Count > 0).OrderByDescending(x => x.Count) : new[] { new StackSpec(Troop, Count) };
}

/// One troop type's men in a battle company.
public sealed record StackSpec(string Troop, int Count);

public sealed record ArmySpec(string Name, int Level, IReadOnlyList<Company> Companies, IReadOnlyList<DivisionSpec>? Divisions = null);

/// A division (docs/COMBAT_OVERHAUL.md 1): companies whose Company.Division is Id, led by a sergeant who pursues a goal.
/// Leader names the companion fighting as a company's Hero, if one leads it; Temper is "Bold", "Steady" or "Cautious";
/// Skill (0-10) is his Command skill; Goal is the goal he starts the battle with (a GoalKind name); LeadCompany, when
/// Leader is empty, is the company his best man is picked from (a sergeant leading from his own company).
public sealed record DivisionSpec(int Id, string Name, string Temper = "Steady", int Skill = 3, string Goal = "Hold", string Leader = "", int LeadCompany = 0);

/// Army builder. A company belongs to a line (infantry, spears, archers, horse); its tier picks the troop on that line,
/// standing in for companies the campaign has upgraded. Elite companies are smaller.
public static class Armies
{
    /// The troop a realm's line fields at a tier (1-7), from Data/factions.json.
    public static string LineTroop(string role, int tier, int faction) => Factions.Line(faction, role, tier);

    static readonly (string Role, string[] ByTier, int Base)[] Lines =
    {
        ("infantry", new[] { "levies", "shieldmen", "men_at_arms", "ironguard", "paladins", "iron_legionaries", "immortals" }, 24),
        ("archers", new[] { "bowmen", "bowmen", "longbowmen", "yeoman_archers", "master_longbowmen", "royal_yeomen", "sovereign_longbowmen" }, 16),
        ("spears", new[] { "levies", "spearrunners", "pikemen", "phalanx_pikemen", "silver_pikes", "grand_phalanx", "imperial_pikemen" }, 12),
        ("horse", new[] { "outriders", "lancers", "lancers", "knights", "banner_knights", "paladin_knights", "grail_knights" }, 10),
    };

    static int Count(int baseCount, string troop, bool enemy)
    {
        var t = Troops.Get(troop);
        float k = t.Tier switch { 1 => 1.25f, 2 => 1f, 3 => 0.85f, 4 => 0.7f, 5 => 0.65f, 6 => 0.6f, _ => 0.55f };
        return Math.Max(4, (int)MathF.Round(baseCount * k * (enemy ? 1.05f : 1)));
    }

    /// The prototype's standard army (level 2: Shieldmen, Bowmen, Spearrunners, Lancers).
    public static ArmySpec Standard(int team) => Make(team == 1 ? "Crimson host" : "Azure company", 2, new[] { 2, 2, 2, 2 }, team == 1);

    /// A campaign-like army: each line's tier is the level, give or take one (companies are upgraded unevenly).
    public static ArmySpec Random(Random rng, int level, int team)
    {
        var tiers = Lines.Select(_ => Math.Clamp(level + (rng.NextSingle() < 0.35f ? (rng.Next(2) == 0 ? -1 : 1) : 0), 1, 7)).ToArray();
        return Make(team == 1 ? "Crimson host" : "Azure company", level, tiers, team == 1);
    }

    /// A big campaign-like army of about `men` men: several companies per line (the bench and scale tests).
    public static ArmySpec Large(Random rng, int level, int team, int men)
    {
        var list = new List<Company>();
        int id = 1;
        while (list.Sum(c => c.Count) < men)
            foreach (var c in Random(rng, level, team).Companies) list.Add(c with { Id = team * 1000 + id++ });
        return new ArmySpec(team == 1 ? "Crimson host" : "Azure company", level, list);
    }

    /// Split a big army into divisions under sergeants (docs/COMBAT_OVERHAUL.md 1.6): the horse rides a wing, the foot
    /// is cut into a left and a right battle, and the middle of the foot, the bows and the allies stay with the captain.
    /// Armies under 240 men or 6 companies are left whole; an army that already has divisions is not touched.
    public static ArmySpec Divide(ArmySpec army)
    {
        if (army.Divisions is { Count: > 0 } || army.Companies.Count < 6 || army.Companies.Sum(c => c.Count) < 240) return army;
        var comps = army.Companies.ToList();
        var specs = new List<DivisionSpec>();
        void Make(string name, List<int> idx, string goal, string temper)
        {
            if (idx.Count == 0) return;
            int id = specs.Count;
            foreach (var i in idx) comps[i] = comps[i] with { Division = id };
            var lead = idx.OrderByDescending(i => Troops.Get(comps[i].Troop).Tier).ThenBy(i => i).First();
            specs.Add(new DivisionSpec(id, name, temper, 3 + army.Level / 2, goal, "", comps[lead].Id));
        }
        var own = Enumerable.Range(0, comps.Count).Where(i => !comps[i].Allied).ToList();
        var horse = own.Where(i => RoleOf(comps[i]) == "horse").ToList();
        var foot = own.Where(i => RoleOf(comps[i]) is "infantry" or "spears").ToList();
        if (horse.Sum(i => comps[i].Count) >= 30) Make("The horse", horse, "FlankRight", "Bold");
        if (foot.Count >= 4)
        {
            int third = foot.Count / 3;
            Make("Left battle", foot.Take(third).ToList(), "Hold", "Steady");
            Make("Right battle", foot.Skip(foot.Count - third).ToList(), "Hold", "Steady");
        }
        return army with { Companies = comps, Divisions = specs.Count > 0 ? specs : null };
    }

    static ArmySpec Make(string name, int level, int[] tiers, bool enemy) =>
        new(name, level, Lines.Select((l, i) => new Company(l.ByTier[tiers[i] - 1], Count(l.Base, l.ByTier[tiers[i] - 1], enemy), l.Role)).ToList());

    /// The troop a line fields at a tier (1-7).
    public static string LineTroop(string role, int tier) => Lines.First(l => l.Role == role).ByTier[Math.Clamp(tier, 1, 7) - 1];

    public static string RoleOf(Company c) => c.Role is { Length: > 0 } r ? r : Troops.Get(c.Troop).Class switch
    {
        UnitClass.Archer => "archers", UnitClass.Skirmisher => "spears", UnitClass.Cavalry => "horse", _ => "infantry",
    };
}
