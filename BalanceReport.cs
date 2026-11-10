using System.Globalization;
using System.Text;

namespace Ironfield;

/// `dotnet run -- --balance [n]` (A7): runs n headless campaigns and a grid of battles, and writes CSVs to ./balance/
/// for tuning Data/campaign.json and troops.json. Nothing here needs a display.
public static class BalanceReport
{
    /// How long each pacing run lasts (weeks of campaign time).
    public const int PacingWeeks = 40;

    /// Play time per campaign day (F9): about 4.5 minutes, counting the map at mixed speeds, a share of a fought battle every
    /// few days and the town screens. It turns CAMPAIGN_OVERHAUL.md 9's target hours into target days.
    public const float MinutesPerDay = 4.5f;
    public static float Hours(int day) => day * MinutesPerDay / 60;

    /// The spec's target play hours from the start, per career stage, as campaign days (9: 3-5 h to Vassal, 4-6 more to Lord,
    /// 6-10 more to Marshal or King).
    public static readonly (string Stage, int From, int To)[] Targets =
    {
        ("VASSAL", 40, 67), ("LORD", 93, 147), ("MARSHAL", 173, 280),
    };

    /// `--pacing [n]`: only the stand-in captain's runs (1b), for tuning the career's pace quickly.
    public static void Pacing(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;
        // 1b. Pacing (F0): a scripted stand-in captain plays each campaign; record the day it reaches each career stage, and a
        // weekly line of its progress. Every later overhaul step is tuned against these numbers (CAMPAIGN_OVERHAUL.md 9).
        var stages = new StringBuilder("seed,stage,day,play_hours,battles,won,renown,men,promotions\n");
        var weekly = new StringBuilder("seed,week,day,stage,renown,men,wounded,companies,tier_avg,gold,battles,won\n");
        for (int seed = 1; seed <= campaigns; seed++)
        {
            var c = Campaign.New(2000 + seed);
            var pilot = new Pilot(c);
            var seen = new HashSet<string>();
            for (int week = 1; week <= PacingWeeks && !c.S.GameOver; week++)
            {
                pilot.Run(168);
                foreach (var (stage, day) in pilot.StageDay.Where(kv => seen.Add(kv.Key)))
                    stages.AppendLine(string.Join(",", seed, Csv(stage), day, Hours(day).ToString("0.0", inv), pilot.Battles, pilot.Won, c.Captain.Renown.ToString("0", inv), c.Player.Men, pilot.Promotions));
                var cos = c.Player.Companies;
                weekly.AppendLine(string.Join(",", seed, week, c.Day, Csv(c.Stage), c.Captain.Renown.ToString("0", inv), c.Player.Men, c.Player.Wounded, cos.Count,
                    (cos.Count > 0 ? cos.Average(co => co.Type.Tier) : 0).ToString("0.00", inv), c.S.Gold.ToString("0", inv), pilot.Battles, pilot.Won));
            }
            Console.WriteLine($"pacing {seed}: day {c.Day}, {c.Stage}, renown {c.Captain.Renown:0}, {pilot.Battles} battles ({pilot.Won} won), "
                + $"{pilot.Promotions} promoted ({pilot.Promotions / (float)Math.Max(1, pilot.Battles):0.0} a battle), first tier V day {pilot.FirstT5Day}, {pilot.Taken} seats stormed, vassal renown day {pilot.RenownForVassalDay}, "
                + string.Join(", ", pilot.StageDay.Select(kv => $"{kv.Key} day {kv.Value}")));
            if (c.Rank == Rank.SwornSword && !c.CanSwearFealty(out string why)) Console.WriteLine($"  fealty refused: {why}");
            Console.WriteLine($"  name: {string.Join(" ", c.Me.Traits)} {c.Title}; fled {c.Tally("fled")}, wounds {string.Join(" ", c.Captain.Wounds)}, perks {string.Join(" ", c.Captain.Perks)}");
            foreach (var (stage, from, to) in Targets)
            {
                string got = pilot.StageDay.TryGetValue(stage, out int d) ? $"day {d} ({Hours(d):0.0} h)" : "not reached";
                bool ok = pilot.StageDay.TryGetValue(stage, out d) && d >= from && d <= to;
                Console.WriteLine($"  target {stage} day {from}-{to}: {got} {(ok ? "OK" : "OFF")}");
            }
        }
        File.WriteAllText("balance/pacing.csv", stages.ToString());
        File.WriteAllText("balance/pacing_weekly.csv", weekly.ToString());

    }

    /// `--realms [n]`: the snowball guard (docs/archive/WORLD.md W5). n campaigns run two years (240 days) with your company out of
    /// the way; each realm's share of the provinces at the end of each year, and the day any realm falls. Flags a realm gone
    /// in the first year or one holding more than 40% of the map. Writes balance/realms.csv.
    public static void Realms(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("seed,day,realm,provinces,share,seats,lords\n");
        int flags = 0;
        float peak = 0;
        for (int seed = 1; seed <= campaigns; seed++)
        {
            var c = Campaign.New(3000 + seed);
            c.Player.Cooldown = 1e6f;
            var fell = new Dictionary<int, int>();
            var line = new StringBuilder($"realms {seed}:");
            for (int day = 1; day <= 240; day++)
            {
                for (int h = 0; h < 24; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; c.S.Event = null; c.Beat = null; c.Report = null; }
                for (int f = 0; f < c.S.Factions.Count; f++) if (!c.Alive(f) && !fell.ContainsKey(f)) fell[f] = day;
                if (day % 120 != 0) continue;
                line.Append($" year {day / 120}:");
                for (int f = 0; f < c.S.Factions.Count; f++)
                {
                    int prov = c.S.Provinces.Count(p => p.Faction == f);
                    float share = prov / (float)c.S.Provinces.Count;
                    peak = MathF.Max(peak, share);
                    int seats = c.S.Settlements.Count(s => s.Faction == f && s.Kind != SettlementKind.Village);
                    int lords = c.S.Parties.Count(p => p.Kind == PartyKind.Lord && p.Faction == f);
                    csv.AppendLine(string.Join(",", seed, day, Csv(c.FactionNames[f]), prov, share.ToString("0.000", inv), seats, lords));
                    line.Append($" {share * 100:0}");
                    if (share > 0.4f) { flags++; line.Append("!"); }
                }
            }
            foreach (var (f, day) in fell)
            {
                line.Append($"; {c.FactionNames[f]} fell on day {day}");
                if (day <= 120) { flags++; line.Append("!"); }
            }
            Console.WriteLine(line + $"; wars {c.S.Wars.Count(w => w.Active)}, alliances {c.S.Pacts.Count}");
        }
        File.WriteAllText("balance/realms.csv", csv.ToString());
        Console.WriteLine($"realms: {campaigns} campaigns, largest share {peak * 100:0}%, {flags} flags (a realm over 40% or gone in year one)");
    }

    static string Csv(string s) => s.Contains(',') ? $"\"{s}\"" : s;

    public static void Run(int campaigns)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;

        // 1. Campaign economy and progression, week by week, with an idle contracted player.
        var eco = new StringBuilder("seed,week,player_gold,player_men,lord_parties,lord_men_avg,lord_tier_avg,lord_tier3plus,war0,war1,at_war,bandits,ai_battles,treas0,treas1,treas2\n");
        for (int seed = 1; seed <= campaigns; seed++)
        {
            var c = Campaign.New(1000 + seed);
            c.Player.Cooldown = 1e9f; // the player sits out: this measures the world
            for (int week = 1; week <= 20; week++)
            {
                for (int h = 0; h < 168; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; }
                var lords = c.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down).ToList();
                var cos = lords.SelectMany(l => l.Companies).ToList();
                eco.AppendLine(string.Join(",", seed, week, c.S.Gold.ToString("0", inv), c.Player.Men, lords.Count,
                    (lords.Count > 0 ? lords.Average(l => l.Men) : 0).ToString("0.0", inv),
                    (cos.Count > 0 ? cos.Average(co => co.Type.Tier) : 0).ToString("0.00", inv), cos.Count(co => co.Type.Tier >= 3),
                    c.S.Factions[0].WarScore.ToString("0", inv), c.S.Factions[1].WarScore.ToString("0", inv), c.S.Wars.Count(w => w.Active),
                    c.S.Parties.Count(p => p.Kind == PartyKind.Bandit), c.AiBattles,
                    c.S.Factions[0].Treasury.ToString("0", inv), c.S.Factions[1].Treasury.ToString("0", inv),
                    c.S.Factions.Count > 2 ? c.S.Factions[2].Treasury.ToString("0", inv) : "0"));
            }
            Console.WriteLine($"campaign {seed}: done");
        }
        File.WriteAllText("balance/economy.csv", eco.ToString());

        Pacing(campaigns);

        // 2. Battle win rates by army level (AI against AI), and how long they take. Each side is built by the same
        // rule (Armies.Random), so a fair system should sit near 50% on the diagonal; drift shows a balance problem.
        // Each seed fights on a different ground, so the columns show who wins on which field (docs/archive/TERRAIN.md T7).
        var grounds = new[] { TerrainParams.Meadow, TerrainParams.Farmland, TerrainParams.Forest, TerrainParams.Hills, TerrainParams.Marsh, TerrainParams.Badlands };
        var battles = new StringBuilder("level_a,level_b,seed,winner,seconds,dead,ground,layout,ridge_side\n");
        for (int a = 1; a <= 4; a++)
            for (int b = 1; b <= 4; b++)
                for (int seed = 0; seed < 6; seed++)
                {
                    var rng = new Random(a * 100 + b * 10 + seed);
                    var battle = new Battle(seed, Armies.Random(rng, a, 0), Armies.Random(rng, b, 0), new BattleOptions { AutoPlayer = true, PlayerWounded = true, Ground = grounds[seed % grounds.Length] });
                    for (int step = 0; battle.Winner < 0 && step < 60 * 600; step++) battle.Update(1 / 60f);
                    battles.AppendLine(string.Join(",", a, b, seed, battle.Winner, battle.Time.ToString("0", inv), battle.Units.Count(u => u.Dead),
                        battle.Terrain.Params.Name, battle.Terrain.Layout, battle.Terrain.RidgeSide));
                }
        File.WriteAllText("balance/battles.csv", battles.ToString());

        // 2b. The same level on both sides: an even fight should be close to a coin toss.
        int even = 0, evenN = 0;
        for (int level = 1; level <= 4; level++)
            for (int seed = 0; seed < 8; seed++)
            {
                var rng = new Random(level * 1000 + seed);
                var battle = new Battle(seed, Armies.Random(rng, level, 0), Armies.Random(rng, level, 1), new BattleOptions { AutoPlayer = true, PlayerWounded = true });
                for (int step = 0; battle.Winner < 0 && step < 60 * 600; step++) battle.Update(1 / 60f);
                evenN++;
                if (battle.Winner == 0) even++;
            }
        Console.WriteLine($"even fights: your side wins {even}/{evenN} ({100f * even / evenN:0}%) with identical army levels");

        // 3. The troop table, as the data has it.
        var troops = new StringBuilder("id,tier,class,hp,armor,damage,cost,upgrade_xp,strength_per_man\n");
        foreach (var t in Troops.All)
            troops.AppendLine(string.Join(",", t.Id, t.Tier, t.Class, t.MaxHp, t.Armor, t.Damage, t.Cost, t.UpgradeXp, Rules.Strength(t, 1).ToString("0.00", inv)));
        File.WriteAllText("balance/troops.csv", troops.ToString());
        Console.WriteLine("wrote balance/economy.csv, balance/pacing.csv, balance/pacing_weekly.csv, balance/battles.csv, balance/troops.csv");
    }

    /// `--grounds [n]` (docs/archive/TERRAIN.md T8): the same army on both sides, AI against AI, n seeds on each field layout, each fought
    /// twice with the ground chosen by each side in turn. A fair layout gives the chooser an edge but not a sure
    /// win; writes balance/grounds.csv and prints a line per layout.
    public static void Grounds(int seeds)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder("layout,level,seed,favour,winner,seconds,dead\n");
        foreach (var layout in Enum.GetValues<FieldLayout>())
        {
            var g = (layout switch
            {
                FieldLayout.Fen => TerrainParams.Marsh,
                FieldLayout.WoodedFlank or FieldLayout.ForestClearing => TerrainParams.Forest,
                FieldLayout.Village or FieldLayout.Hedgerows => TerrainParams.Farmland,
                FieldLayout.Ridge or FieldLayout.TwoHills or FieldLayout.Pass => TerrainParams.Hills,
                _ => TerrainParams.Meadow,
            }) with { Layouts = new[] { layout }, River = layout == FieldLayout.RiverLine, Road = layout is FieldLayout.RiverLine or FieldLayout.Pass or FieldLayout.Village };
            int chooser = 0, stalls = 0, n = 0;
            float time = 0;
            for (int seed = 0; seed < seeds; seed++)
                for (int favour = 0; favour < 2; favour++)
                {
                    // Both sides the same army with the same head count (team 1's): your side's stand-in still lacks the
                    // enemy captain's aura, so each seed is fought twice, the ground chosen by each side in turn.
                    int level = 1 + seed % 4;
                    var a = Armies.Random(new Random(seed * 7 + level), level, 1);
                    var b = Armies.Random(new Random(seed * 7 + level), level, 1);
                    var battle = new Battle(seed, a, b, new BattleOptions { AutoPlayer = true, Ground = g with { Favour = favour } });
                    for (int step = 0; battle.Winner < 0 && step < 60 * 400; step++) battle.Update(1 / 60f);
                    n++;
                    if (battle.Winner < 0) stalls++;
                    if (battle.Winner == favour) chooser++;
                    time += battle.Time;
                    csv.AppendLine(string.Join(",", layout, level, seed, favour, battle.Winner, battle.Time.ToString("0", inv), battle.Units.Count(u => u.Dead)));
                }
            Console.WriteLine($"{layout,-15} chooser wins {chooser}/{n}, {stalls} unfinished, {time / n:0} s a battle");
        }
        File.WriteAllText("balance/grounds.csv", csv.ToString());
        Console.WriteLine("wrote balance/grounds.csv");
    }

    /// Fought against auto-resolved (docs/archive/MELEE.md D8): one infantry company of each tier 1-6 against one of a higher tier,
    /// the numbers set so Rules.Might calls them even. If auto-resolve weighs drill right, each pair is a coin toss.
    /// Prints the higher tier's share of wins by tier gap, at the current weight and with weight `w` tried in its place.
    public static void Agree(int seeds, float w)
    {
        float Per(UnitType t, float weight) => Rules.MightAt(t, 1, weight, Balance.Get().TraitWeight); // Rules.Might at that drill weight
        foreach (float weight in new[] { Balance.Get().DrillWeight, w })
        {
            var byGap = new Dictionary<int, (int won, int n)>();
            for (int lo = 1; lo <= 6; lo++)
                for (int hi = lo + 1; hi <= 7; hi++)
                    for (int seed = 0; seed < seeds; seed++)
                    {
                        string a = Armies.LineTroop("infantry", lo), b = Armies.LineTroop("infantry", hi);
                        int na = 60, nb = Math.Max(4, (int)MathF.Round(na * Per(Troops.Get(a), weight) / Per(Troops.Get(b), weight)));
                        bool swap = seed % 2 == 1; // the higher tier on each side in turn (the captains differ)
                        var low = new ArmySpec("low", lo, new[] { new Company(a, na, "infantry") });
                        var high = new ArmySpec("high", hi, new[] { new Company(b, nb, "infantry") });
                        var battle = new Battle(seed * 31 + lo * 7 + hi, swap ? high : low, swap ? low : high, new BattleOptions { AutoPlayer = true, PlayerWounded = true });
                        for (int step = 0; battle.Winner < 0 && step < 60 * 300; step++) battle.Update(1 / 60f);
                        var (won, n) = byGap.GetValueOrDefault(hi - lo);
                        byGap[hi - lo] = (won + (battle.Winner == (swap ? 0 : 1) ? 1 : 0), n + 1);
                    }
            Console.WriteLine($"agree: drill weight {weight:0.00}: the higher tier wins " + string.Join(", ", byGap.OrderBy(k => k.Key).Select(k => $"gap {k.Key} {100f * k.Value.won / k.Value.n:0}% of {k.Value.n}")));
        }
    }
}
