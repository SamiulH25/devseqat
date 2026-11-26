using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle mode for the Quick Battle / Battle Lab dev tool.
public enum QuickBattleMode { Field, Siege, Duel }

/// One side's army configuration in the Battle Lab.
public sealed class QuickSideConfig
{
    /// 0..Factions.All.Count-1 = realm, -1 = Bandits, -2 = Monsters/Wild.
    public int Faction;
    /// When true, uses `Companies` verbatim instead of generating a faction host by Men/Tier.
    public bool Custom;
    public int Men = 180;
    public int Tier = 3;
    /// Force-split companies into sergeant divisions (so sergeants/posts/stances can be tested at any army size).
    public bool Divisions;
    public int OfficerSkill = 5;
    /// Enemy AI general personality ("", "Reckless", "Cautious", "Ambitious", "Honourable").
    public string Trait = "";
    public List<(string Troop, int Count)> Companies = new();

    public QuickSideConfig Clone() => new()
    {
        Faction = Faction, Custom = Custom, Men = Men, Tier = Tier,
        Divisions = Divisions, OfficerSkill = OfficerSkill, Trait = Trait,
        Companies = Companies.ToList(),
    };
}

/// Complete headless configuration for a Quick Battle / Battle Lab test run.
public sealed class QuickBattleConfig
{
    public string Preset = "OPEN FIELD";
    public QuickBattleMode Mode = QuickBattleMode.Field;
    public QuickSideConfig Player = new() { Faction = 0, Men = 180, Tier = 3 };
    public QuickSideConfig Enemy = new() { Faction = 1, Men = 180, Tier = 3 };

    public string Ground = "Meadow";
    /// Null = Auto (chosen by seed from TerrainParams.Layouts); otherwise forced layout.
    public FieldLayout? Layout = FieldLayout.Open;
    public bool River;

    // Siege settings
    public FortressKind FortressKind = FortressKind.Town;
    public int WallsLevel = 2;
    public int Ladders = 2;
    public int Breaches;
    public int WallEngines = 2;
    public int Rams = 1;
    public int Towers = 1;
    public int Mangonels;
    public int Trebuchets;
    public int Springalds;

    // Duel Arena settings (used when Mode == QuickBattleMode.Duel)
    /// Total fighters on your side including the player (1 = solo, 2 = player + 1 ally, etc.).
    public int DuelAllies = 1;
    /// Total fighters on the enemy side (1 = 1v1, 2 = 1v2, 3 = 1v3, etc.).
    public int DuelEnemies = 1;
    /// Optional explicit troop id for allied duelists ("" = auto melee troop for Player.Faction & Player.Tier).
    public string DuelAllyTroop = "";
    /// Optional explicit troop id for enemy duelists ("" = auto melee troop for Enemy.Faction & Enemy.Tier).
    public string DuelEnemyTroop = "";
    /// Fight on horseback instead of on foot.
    public bool DuelMounted;

    public static readonly (string Label, int Allies, int Enemies)[] DuelMatchups =
    {
        ("1V1", 1, 1), ("1V2", 1, 2), ("1V3", 1, 3), ("1V4", 1, 4),
        ("2V2", 2, 2), ("2V3", 2, 3), ("3V3", 3, 3),
    };

    // Weather & time
    public float Hour = 13f;
    /// 0 Clear, 1 Rain, 2 Storm, 3 Fog, 4 Snow.
    public int WeatherKind;

    // Captain & testing options
    public string Road = "sword";
    public int AuraTier;
    public int Tide = 1;
    public bool AutoPlayer;
    public bool Scouted = true;
    public bool SkipDeploy;
    public int Seed = 777;

    public static readonly string[] Grounds = { "Meadow", "Farmland", "Forest", "Hills", "Marsh", "Steppe", "Badlands", "Coast", "Taiga", "Highlands", "Tundra", "Desert", "Oasis", "Lists" };
    public static readonly FieldLayout?[] Layouts = new FieldLayout?[] { null }.Concat(Enum.GetValues<FieldLayout>().Cast<FieldLayout?>()).ToArray();
    public static readonly FortressKind[] Fortresses = Enum.GetValues<FortressKind>();
    public static readonly float[] Hours = { 6f, 10f, 13f, 19f, 23f };
    public static readonly string[] HourNames = { "DAWN (6H)", "MORNING (10H)", "NOON (13H)", "DUSK (19H)", "NIGHT (23H)" };
    public static readonly string[] WeatherNames = { "CLEAR", "RAIN", "STORM", "FOG", "SNOW" };
    public static readonly string[] Roads = { "sword", "lance", "bow", "great", "pole" };
    public static readonly string[] Traits = { "", "Reckless", "Cautious", "Ambitious", "Honourable" };

    public static readonly string[] PresetNames =
    {
        "OPEN FIELD", "VILLAGE", "RIVER", "TOWN SIEGE", "CASTLE SIEGE",
        "GREAT CASTLE", "SERGEANTS", "CAVALRY", "ARCANA", "MONSTERS", "DUEL",
    };

    public static QuickBattleConfig FromPreset(string name, int seed = 777)
    {
        var c = new QuickBattleConfig { Preset = name, Seed = seed };
        switch (name)
        {
            case "VILLAGE":
                c.Ground = "Farmland"; c.Layout = FieldLayout.Village;
                c.Player.Faction = 0; c.Enemy.Faction = 2; c.Player.Men = 140; c.Enemy.Men = 140;
                break;
            case "RIVER":
                c.Ground = "Meadow"; c.Layout = FieldLayout.RiverLine; c.River = true;
                c.Player.Faction = 0; c.Enemy.Faction = 5; c.Player.Men = 200; c.Enemy.Men = 200;
                break;
            case "TOWN SIEGE":
                c.Mode = QuickBattleMode.Siege; c.FortressKind = FortressKind.Town; c.WallsLevel = 2;
                c.Ladders = 3; c.Rams = 1; c.Towers = 1; c.Mangonels = 1; c.WallEngines = 2;
                c.Player.Faction = 0; c.Enemy.Faction = 1; c.Player.Men = 240; c.Enemy.Men = 180;
                break;
            case "CASTLE SIEGE":
                c.Mode = QuickBattleMode.Siege; c.FortressKind = FortressKind.Castle; c.WallsLevel = 2;
                c.Ladders = 3; c.Rams = 1; c.Towers = 1; c.Trebuchets = 1; c.WallEngines = 3;
                c.Player.Faction = 1; c.Enemy.Faction = 6; c.Player.Men = 260; c.Enemy.Men = 200;
                break;
            case "GREAT CASTLE":
                c.Mode = QuickBattleMode.Siege; c.FortressKind = FortressKind.GreatCastle; c.WallsLevel = 3;
                c.Ladders = 4; c.Rams = 1; c.Towers = 2; c.Trebuchets = 2; c.Breaches = 1; c.WallEngines = 4;
                c.Player.Faction = 0; c.Enemy.Faction = 6; c.Player.Men = 320; c.Enemy.Men = 250;
                c.Player.Divisions = true; c.Enemy.Divisions = true;
                break;
            case "SERGEANTS":
                c.Ground = "Meadow"; c.Layout = FieldLayout.Ridge;
                c.Player.Faction = 0; c.Enemy.Faction = 1;
                c.Player.Men = 340; c.Enemy.Men = 340; c.Player.Tier = 3; c.Enemy.Tier = 3;
                c.Player.Divisions = true; c.Enemy.Divisions = true;
                c.Player.OfficerSkill = 6; c.Enemy.OfficerSkill = 6;
                break;
            case "CAVALRY":
                c.Ground = "Steppe"; c.Layout = FieldLayout.Open; c.Road = "lance";
                c.Player.Faction = 4; c.Enemy.Faction = 3; c.Player.Men = 160; c.Enemy.Men = 160;
                c.Player.Tier = 4; c.Enemy.Tier = 4;
                break;
            case "ARCANA":
                c.Ground = "Highlands"; c.Layout = FieldLayout.TwoHills; c.Hour = 19f;
                c.AuraTier = 4; c.Tide = 3;
                c.Player.Faction = 0; c.Enemy.Faction = 1;
                c.Player.Custom = true; c.Enemy.Custom = true;
                c.Player.Companies = BuildFactionCompanies(0, 140, 3, new Random(seed));
                c.Enemy.Companies = BuildFactionCompanies(1, 140, 3, new Random(seed + 1));
                if (Troops.Has("galdr_masters")) c.Player.Companies.Add(("galdr_masters", 14));
                else if (Troops.Has("vigil_novices")) c.Player.Companies.Add(("vigil_novices", 16));
                if (Troops.Has("crimson_crones")) c.Enemy.Companies.Add(("crimson_crones", 14));
                else if (Troops.Has("vigil_novices")) c.Enemy.Companies.Add(("vigil_novices", 16));
                break;
            case "MONSTERS":
                c.Ground = "Forest"; c.Layout = FieldLayout.ForestClearing; c.Hour = 19f;
                c.Player.Faction = 2; c.Player.Men = 160; c.Player.Tier = 3;
                c.Enemy.Faction = -2; c.Enemy.Men = 120; c.Enemy.Tier = 3;
                break;
            case "DUEL":
                c.Mode = QuickBattleMode.Duel; c.Ground = "Lists"; c.Layout = FieldLayout.Open;
                c.Player.Faction = 0; c.Enemy.Faction = 1; c.Player.Tier = 3; c.Enemy.Tier = 3;
                c.DuelAllies = 1; c.DuelEnemies = 1;
                break;
            default:
                c.Ground = "Meadow"; c.Layout = FieldLayout.Open;
                c.Player.Faction = 0; c.Enemy.Faction = 1; c.Player.Men = 180; c.Enemy.Men = 180;
                break;
        }
        return c;
    }

    /// Build a Duel Arena config for an explicit matchup (e.g. 1v1, 1v2, 1v3, 2v2).
    public static QuickBattleConfig FromDuel(int allies, int enemies, bool mounted = false, int seed = 777)
    {
        var c = FromPreset("DUEL", seed);
        c.DuelAllies = Math.Clamp(allies, 1, 5);
        c.DuelEnemies = Math.Clamp(enemies, 1, 6);
        c.DuelMounted = mounted;
        if (mounted) c.Road = "lance";
        return c;
    }

    /// Parse IRONFIELD_DUEL env spec: "1v3", "2v2", "1v2:mounted", etc. Returns null for "setup" or invalid.
    public static QuickBattleConfig? ParseDuelEnv(string spec)
    {
        var parts = spec.Trim().ToLowerInvariant().Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0] == "setup") return null;
        var nums = parts[0].Split('v');
        if (nums.Length != 2 || !int.TryParse(nums[0], out int a) || !int.TryParse(nums[1], out int e)) return null;
        bool mounted = parts.Any(p => p is "mounted" or "horse");
        var cfg = FromDuel(a, e, mounted);
        foreach (var p in parts.Skip(1))
            if (Troops.Has(p)) cfg.DuelEnemyTroop = p;
            else if (Roads.Contains(p)) cfg.Road = p;
        return cfg;
    }

    /// Pick a default melee fighter troop id for a faction and tier in Duel Arena.
    public static string PickDuelTroop(int faction, int tier)
    {
        tier = Math.Clamp(tier, 1, Troops.MaxTier);
        if (faction == -2)
        {
            var pool = Troops.All.Where(t => (t.Wild || t.Culture == "wild") && t.Id != "captain")
                .OrderBy(t => Math.Abs(t.Tier - tier)).ToList();
            return pool.Count > 0 ? pool[0].Id : "troll";
        }
        if (faction == -1)
        {
            var band = Troops.All.Where(t => t.Culture == "bandit" && t.Class != UnitClass.Archer)
                .OrderBy(t => Math.Abs(t.Tier - tier)).ToList();
            return band.Count > 0 ? band[0].Id : "brevon_fyrd";
        }
        int f = Math.Clamp(faction, 0, Factions.All.Count - 1);
        return Factions.Line(f, "infantry", tier);
    }

    public static string FactionName(int faction) => faction switch
    {
        -1 => "BANDITS",
        -2 => "MONSTERS AND BEASTS",
        _ => Factions.All[Math.Clamp(faction, 0, Factions.All.Count - 1)].Name.ToUpperInvariant(),
    };

    public static int PaletteOf(int faction) => faction switch
    {
        -1 or -2 => 3,
        _ => Factions.All[Math.Clamp(faction, 0, Factions.All.Count - 1)].Palette,
    };

    public static string CultureOf(int faction) => faction switch
    {
        -1 => "bandit",
        -2 => "wild",
        _ => Factions.All[Math.Clamp(faction, 0, Factions.All.Count - 1)].Culture,
    };

    /// Generate a balanced list of (troopId, count) companies for any realm, bandits, or monsters.
    public static List<(string Troop, int Count)> BuildFactionCompanies(int faction, int men, int tier, Random rng)
    {
        tier = Math.Clamp(tier, 1, 6);
        men = Math.Clamp(men, 10, 800);
        var list = new List<(string Troop, int Count)>();

        if (faction == -2)
        {
            var pool = Troops.All.Where(t => t.Wild || t.Culture == "wild").ToList();
            if (pool.Count == 0) { list.Add(("brevon_fyrd", men)); return list; }
            string[] preferred = { "troll", "wyvern", "cave_bear", "giant_spider", "dire_wolf", "goblin_raiders", "goblin_shooters", "skeleton_warriors" };
            var chosen = preferred.Where(Troops.Has).ToList();
            if (chosen.Count == 0) chosen = pool.Take(5).Select(t => t.Id).ToList();
            int left = men;
            int idx = 0;
            while (left > 0 && idx < 10)
            {
                string id = chosen[idx % chosen.Count];
                var t = Troops.Get(id);
                int n = t.Huge ? Math.Clamp(left / 25, 1, 4) : Math.Min(left, Math.Max(8, (int)(32 / Rules.TierWeight(t.Tier))));
                list.Add((id, n));
                left -= t.Huge ? n * 18 : n;
                idx++;
            }
            return list;
        }

        if (faction == -1)
        {
            var band = Troops.All.Where(t => t.Culture == "bandit").OrderBy(t => Math.Abs(t.Tier - tier)).ToList();
            if (band.Count == 0) { list.Add(("brevon_fyrd", men)); return list; }
            int left = men, idx = 0;
            while (left > 0 && idx < 12)
            {
                var t = band[idx % Math.Min(4, band.Count)];
                int n = Math.Min(left, Math.Max(10, (int)(36 / Rules.TierWeight(t.Tier))));
                list.Add((t.Id, n));
                left -= n;
                idx++;
            }
            return list;
        }

        int f = Math.Clamp(faction, 0, Factions.All.Count - 1);
        (string Role, float Share)[] mix = { ("infantry", 0.42f), ("spears", 0.22f), ("archers", 0.20f), ("horse", 0.16f) };
        foreach (var (role, share) in mix)
        {
            int want = Math.Max(6, (int)MathF.Round(men * share));
            int tTier = Math.Clamp(tier + (rng.NextSingle() < 0.25f ? (rng.Next(2) == 0 ? -1 : 1) : 0), 1, Troops.MaxTier);
            string id = Factions.Line(f, role, tTier);
            int per = Math.Max(8, (int)MathF.Round((role == "horse" ? 36 : 54) / Rules.TierWeight(Troops.Get(id).Tier)));
            while (want > 0)
            {
                int n = want - per < 8 ? want : Math.Min(want, per);
                list.Add((id, n));
                want -= n;
            }
        }
        return list;
    }

    ArmySpec BuildArmy(QuickSideConfig side, int team, Random rng)
    {
        var raw = side.Custom && side.Companies.Count > 0
            ? side.Companies.Where(c => c.Count > 0 && Troops.Has(c.Troop)).ToList()
            : BuildFactionCompanies(side.Faction, side.Men, side.Tier, rng);
        if (raw.Count == 0) raw.Add(("brevon_fyrd", 20));

        var comps = new List<Company>();
        int id = 1;
        foreach (var (troop, count) in raw)
            comps.Add(new Company(troop, Math.Clamp(count, 1, 200), Rules.TroopRole(troop), team * 1000 + id++));

        var spec = new ArmySpec(FactionName(side.Faction), side.Tier, comps);
        if (!side.Divisions) return spec;

        // If large enough, use standard Armies.Divide; otherwise force a clean division split so sergeants can be tested at any size.
        var divided = Armies.Divide(spec);
        if (divided.Divisions is { Count: > 0 })
        {
            var tuned = divided.Divisions.Select(d => d with { Skill = side.OfficerSkill }).ToList();
            return divided with { Divisions = tuned };
        }
        return ForceDivide(spec, side.OfficerSkill);
    }

    static ArmySpec ForceDivide(ArmySpec army, int skill)
    {
        var comps = army.Companies.ToList();
        if (comps.Count < 2) return army;
        var specs = new List<DivisionSpec>();
        int AddDiv(string name, List<int> idxs, string goal, string temper, string nature)
        {
            if (idxs.Count == 0) return -1;
            int id = specs.Count;
            foreach (int i in idxs) comps[i] = comps[i] with { Division = id };
            int lead = idxs.OrderByDescending(i => Troops.Get(comps[i].Troop).Tier).First();
            specs.Add(new DivisionSpec(id, name, temper, Math.Clamp(skill, 1, 9), goal, "", comps[lead].Id, new[] { nature }));
            return id;
        }
        var horse = Enumerable.Range(0, comps.Count).Where(i => comps[i].Role == "horse").ToList();
        var bows = Enumerable.Range(0, comps.Count).Where(i => comps[i].Role == "archers").ToList();
        var foot = Enumerable.Range(0, comps.Count).Where(i => comps[i].Role is not ("horse" or "archers")).ToList();
        if (horse.Count > 0) AddDiv("Right horse", horse, "RightWing:Guard", "Bold", "Hothead");
        if (bows.Count > 0) AddDiv("Skirmishers", bows, "Skirmish:Screen", "Cautious", "Steady");
        if (foot.Count >= 2)
        {
            int half = foot.Count / 2;
            AddDiv("Left battle", foot.Take(half).ToList(), "Front:Hold", "Steady", "Stubborn");
            AddDiv("Right battle", foot.Skip(half).ToList(), "Front:Hold", "Steady", "Veteran");
        }
        else if (foot.Count == 1) AddDiv("Vanguard", foot, "Front:Hold", "Steady", "Veteran");
        return army with { Companies = comps, Divisions = specs };
    }

    /// Build the concrete Battle arguments from this config (headless, deterministic for a given Seed).
    public (int Seed, ArmySpec Player, ArmySpec Enemy, BattleOptions Options) Build()
    {
        var rng = new Random(Seed);
        bool siege = Mode == QuickBattleMode.Siege;
        bool duel = Mode == QuickBattleMode.Duel;

        UnitType? duelEnemyLeader = null;
        ArmySpec pArmy, eArmy;
        if (duel)
        {
            int allies = Math.Clamp(DuelAllies, 1, 5);
            int enemies = Math.Clamp(DuelEnemies, 1, 6);
            string allyId = DuelAllyTroop != "" && Troops.Has(DuelAllyTroop) ? DuelAllyTroop : PickDuelTroop(Player.Faction, Player.Tier);
            string enemyId = DuelEnemyTroop != "" && Troops.Has(DuelEnemyTroop) ? DuelEnemyTroop : PickDuelTroop(Enemy.Faction, Enemy.Tier);
            duelEnemyLeader = Troops.Get(enemyId);
            var pComps = new List<Company>();
            if (allies > 1) pComps.Add(new Company(allyId, allies - 1, Rules.TroopRole(allyId), 1001));
            var eComps = new List<Company>();
            if (enemies > 1) eComps.Add(new Company(enemyId, enemies - 1, Rules.TroopRole(enemyId), 2001));
            pArmy = new ArmySpec(FactionName(Player.Faction), Player.Tier, pComps);
            eArmy = new ArmySpec($"{allies}V{enemies} {duelEnemyLeader.Name.ToUpperInvariant()}", Enemy.Tier, eComps);
        }
        else
        {
            pArmy = BuildArmy(Player, 0, rng);
            eArmy = BuildArmy(Enemy, 1, rng);
        }

        int pPal = PaletteOf(Player.Faction), ePal = PaletteOf(Enemy.Faction);
        if (pPal == ePal) ePal = pPal == 1 ? 0 : 1; // keep sides readable if same faction is pitted against itself
        var baseGround = TerrainParams.Named(Ground) ?? TerrainParams.Meadow;
        string defStyle = CultureOf(Enemy.Faction);
        var ground = baseGround with
        {
            River = !siege && !duel && (River || baseGround.River),
            Layouts = Layout is { } l ? new[] { l } : baseGround.Layouts,
            Style = defStyle is "wild" or "bandit" ? null : defStyle,
            Castle = siege && FortressKind is FortressKind.Castle or FortressKind.GreatCastle or FortressKind.TowerHouse,
            Banner = siege ? ePal : -1,
        };

        var weather = WeatherKind switch
        {
            1 => new Weather(Hour, Rain: 0.55f),
            2 => new Weather(Hour, Rain: 1f, Fog: 0.35f),
            3 => new Weather(Hour, Fog: 0.75f),
            4 => new Weather(Hour, Rain: 0.45f, Snow: true),
            _ => new Weather(Hour),
        };

        // Player captain dressed in player faction's starting kit + chosen road.
        var capState = Origins.Preview(new CaptainMaking
        {
            Realm = Math.Clamp(Player.Faction, 0, Factions.All.Count - 1),
            FreeStart = Player.Faction < 0,
            Road = Road,
        });
        var pType = Rules.CaptainType(capState);
        var pSide = capState.Kit.Side != "" ? Rules.CaptainType(capState, side: true) : null;
        var pMount = Road == "lance" || capState.Kit.Horse != "" ? Rules.CaptainMount(capState) : null;

        var engines = new List<EngineSpec>();
        if (siege)
        {
            for (int i = 0; i < Rams; i++) engines.Add(new EngineSpec { Team = 0, Id = "ram" });
            for (int i = 0; i < Towers; i++) engines.Add(new EngineSpec { Team = 0, Id = "tower" });
            for (int i = 0; i < Mangonels; i++) engines.Add(new EngineSpec { Team = 0, Id = "mangonel" });
            for (int i = 0; i < Trebuchets; i++) engines.Add(new EngineSpec { Team = 0, Id = "trebuchet" });
            for (int i = 0; i < Springalds; i++) engines.Add(new EngineSpec { Team = 0, Id = "springald" });
        }

        var forms = AuraTier > 0
            ? Forms.Generic.Where(f => f.Tier <= AuraTier).Select(f => f.Id).ToArray()
            : Array.Empty<string>();

        int totalMen = pArmy.Companies.Sum(c => c.Count) + eArmy.Companies.Sum(c => c.Count);
        var opts = new BattleOptions
        {
            EnemyDamage = Settings.Current.EnemyDamage,
            Weather = weather,
            Cap = Math.Max(Settings.Current.BattleSize, totalMen + 40),
            Ground = ground,
            Palette = new[] { pPal, ePal },
            PlayerType = pType,
            PlayerSide = pSide,
            PlayerMount = pMount,
            Siege = siege,
            FortressKind = FortressKind,
            WallsLevel = Math.Clamp(WallsLevel, 1, 3),
            Ladders = Math.Clamp(Ladders, 0, 6),
            Breaches = Math.Clamp(Breaches, 0, 3),
            WallEngines = Math.Clamp(WallEngines, 0, 4),
            Engines = engines,
            Duel = duel,
            DuelMounted = duel && DuelMounted,
            EnemyLeader = duel ? duelEnemyLeader : null,
            AutoPlayer = AutoPlayer,
            Scouted = Scouted,
            AuraTier = Math.Clamp(AuraTier, 0, 5),
            Forms = forms,
            Tide = Math.Clamp(Tide, 0, 3),
            LeaderTraits = Enemy.Trait != "" ? new[] { Enemy.Trait } : Array.Empty<string>(),
        };
        return (Seed, pArmy, eArmy, opts);
    }
}
