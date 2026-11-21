using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// `--tiers [seeds] [filter]` (docs/pre-demo/TIERS.md TI0): every upgrade link that keeps its role fought against its parent
/// three ways: the same men, the same upkeep (wages are per man, `Rules.WageCost`) and the same company places
/// (`Rules.TierWeight`); wins, men left and time to break, and whether auto-resolve (`Rules.Might`) calls the same winner.
/// A filter is an id fragment, a culture or a matchup. Writes balance/tiers.csv and prints a table by tier step against TI0's targets.
public static partial class BalanceReport
{
    /// The three matchups, by how the parent's head count is set against the upgrade's.
    public static readonly string[] TierMatchups = { "men", "upkeep", "places" };

    /// The upgrade's side is always `Base` men; the parent's is scaled by the matchup.
    const int Base = 30;

    /// One fight: `na` of `a` against `nb` of `b` on flat ground (foot charge, bows hold and shoot), as `SelfTest.TierFight`.
    /// Returns the share of each side still standing and the seconds it took one side to break.
    public static (float a, float b, float secs) TierBout(UnitType a, UnitType b, int na, int nb, int seed, float limit = 150)
    {
        bool shoot = a.Ammo > 0 && b.Ammo > 0;
        float gap = shoot ? 0.8f * MathF.Min(a.Range, b.Range) : 30;
        var battle = Battle.Empty(seed);
        var sa = battle.AddSquad(0, a, na, new(0, -gap / 2), 10, Vector2.UnitY);
        var sb = battle.AddSquad(1, b, nb, new(0, gap / 2), 10, -Vector2.UnitY);
        sa.Order = sb.Order = shoot ? Order.Hold : Order.Charge;
        sa.Target = sb;
        sb.Target = sa;
        static int Up(Squad s) => s.Units.Count(u => u.Active && !u.Routing);
        int i = 0;
        for (; i < limit * 60 && Up(sa) > 0 && Up(sb) > 0; i++) battle.Update(1 / 60f);
        return (Up(sa) / (float)na, Up(sb) / (float)nb, i / 60f);
    }

    /// The parent's head count against `Base` of the upgrade in a matchup.
    public static int ParentMen(UnitType from, UnitType to, string matchup) => matchup switch
    {
        "upkeep" => Math.Max(1, (int)MathF.Round(Base * Rules.WageCost(to) / MathF.Max(1, Rules.WageCost(from)))),
        "places" => Math.Max(1, (int)MathF.Round(Base * Rules.TierWeight(to.Tier) / Rules.TierWeight(from.Tier))),
        _ => Base,
    };

    /// Every upgrade link that keeps its role (class and shooting), parent first, in tier order.
    public static List<(UnitType from, UnitType to)> TierLinks(string filter = "") =>
        Troops.All.Where(t => !t.Wild && !t.Leader)
            .SelectMany(t => t.UpgradesTo.Select(Troops.Get).Where(n => n.Class == t.Class && (n.Ammo > 0) == (t.Ammo > 0)).Select(n => (from: t, to: n)))
            .Where(l => filter == "" || l.from.Id.Contains(filter) || l.to.Id.Contains(filter) || l.to.Culture == filter)
            .OrderBy(l => l.to.Tier).ThenBy(l => l.from.Id).ThenBy(l => l.to.Id).ToList();

    record TierRow(UnitType From, UnitType To, string Matchup, int NFrom, int Wins, int Fights, float KeptTo, float KeptFrom, float Secs, bool AutoAgrees);

    public static void Tiers(int seeds, string filter)
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;
        // A filter naming a matchup ("upkeep") runs only that one, over every link.
        var only = TierMatchups.Contains(filter) ? new[] { filter } : TierMatchups;
        var links = TierLinks(only.Length == 1 ? "" : filter);
        var jobs = links.SelectMany(l => only.Select(m => (l.from, l.to, m))).ToList();
        var rows = new TierRow[jobs.Count];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        // Each fight is its own Battle with its own dice, so the jobs run side by side and the numbers do not depend on the order.
        Parallel.For(0, jobs.Count, j =>
        {
            var (from, to, m) = jobs[j];
            int nf = ParentMen(from, to, m), wins = 0;
            float kt = 0, kf = 0, secs = 0;
            for (int s = 0; s < seeds; s++)
            {
                var (up, down, t) = TierBout(to, from, Base, nf, 7100 + s * 13 + j % 7);
                if (up > down) wins++;
                kt += up; kf += down; secs += t;
            }
            bool autoUp = Rules.Might(to, Base) > Rules.Might(from, nf);
            rows[j] = new TierRow(from, to, m, nf, wins, seeds, kt / seeds, kf / seeds, secs / seeds, autoUp == wins * 2 > seeds);
        });

        var csv = new StringBuilder("from,to,from_tier,to_tier,class,culture,matchup,to_men,from_men,wins,fights,kept_to,kept_from,seconds,might_to,might_from,auto_agrees\n");
        foreach (var r in rows)
            csv.AppendLine(string.Join(",", r.From.Id, r.To.Id, r.From.Tier, r.To.Tier, r.To.Class, r.To.Culture, r.Matchup, Base, r.NFrom, r.Wins, r.Fights,
                r.KeptTo.ToString("0.00", inv), r.KeptFrom.ToString("0.00", inv), r.Secs.ToString("0", inv),
                Rules.Might(r.To, Base).ToString("0", inv), Rules.Might(r.From, r.NFrom).ToString("0", inv), r.AutoAgrees ? 1 : 0));
        File.WriteAllText("balance/tiers.csv", csv.ToString());

        // By step: the share of fights the upgrade won, the men it kept against the parent's, and auto-resolve's agreement.
        Console.WriteLine($"tiers: {links.Count} links x {TierMatchups.Length} matchups x {seeds} seeds in {clock.Elapsed.TotalSeconds:0} s");
        Console.WriteLine("step    matchup  links  up wins  kept up/down  secs  auto agrees  (targets: men 90% wins, ~+33% kept; upkeep even at II, ahead from III; places: up wins)");
        foreach (var g in rows.GroupBy(r => (r.To.Tier, r.Matchup)).OrderBy(g => g.Key.Tier).ThenBy(g => Array.IndexOf(TierMatchups, g.Key.Matchup)))
        {
            float win = g.Sum(r => r.Wins) / (float)g.Sum(r => r.Fights);
            Console.WriteLine($"{Roman(g.Key.Tier - 1),3}->{Roman(g.Key.Tier),-3} {g.Key.Matchup,-8} {g.Count(),5}  {win,6:P0}   {g.Average(r => r.KeptTo),4:0.00}/{g.Average(r => r.KeptFrom),4:0.00}  {g.Average(r => r.Secs),4:0}  {g.Count(r => r.AutoAgrees) / (float)g.Count(),6:P0}");
        }
        // The flattest steps with equal men: what TI1 must fix first.
        Console.WriteLine("flattest steps (equal men):");
        foreach (var r in rows.Where(r => r.Matchup == "men" || only.Length == 1).OrderBy(r => r.Wins / (float)r.Fights).ThenBy(r => r.KeptTo - r.KeptFrom).Take(15))
            Console.WriteLine($"  {r.From.Id} ({Roman(r.From.Tier)}) -> {r.To.Id} ({Roman(r.To.Tier)}): won {r.Wins}/{r.Fights}, kept {r.KeptTo:0.00} against {r.KeptFrom:0.00}");
        Console.WriteLine("wrote balance/tiers.csv");
    }

    /// `--kit` (docs/pre-demo/KIT.md KT4): every man's numbers as built from base, skills and kit -> balance/kit-after.csv, and the
    /// averages by class and tier set against balance/kit-before.csv (the typed numbers before KT3) when it is there.
    public static void Kit()
    {
        Directory.CreateDirectory("balance");
        var inv = CultureInfo.InvariantCulture;
        string F(float v) => v.ToString("0.##", inv);
        var men = Troops.All.Where(t => !t.Wild).OrderBy(t => t.Tier).ThenBy(t => t.Id).ToList();
        var csv = new StringBuilder("id,class,culture,tier,arms,riding,bowcraft,hp,damage,armor,reach,windup,recover,speed,block,pierce,range,reload,ammo,vsMounted,toughness,offence\n");
        foreach (var t in men)
            csv.AppendLine(string.Join(",", t.Id, t.Class, t.Culture, t.Tier, F(t.Skills.Arms), F(t.Skills.Riding), F(t.Skills.Bowcraft), F(t.MaxHp), F(t.Damage), F(t.Armor),
                F(t.Reach), F(t.Windup), F(t.Recover), F(t.Speed), F(t.BlockChance), F(t.Pierce), F(t.Range), F(t.Reload), t.Ammo, F(t.VsMounted),
                F(Troops.Toughness(t)), F(Troops.Offence(t))));
        File.WriteAllText("balance/kit-after.csv", csv.ToString());
        // The typed numbers from before, by id, to set the built ones against.
        var before = new Dictionary<string, Dictionary<string, string>>();
        if (File.Exists("balance/kit-before.csv"))
        {
            var lines = File.ReadAllLines("balance/kit-before.csv");
            var head = lines[0].Split(',');
            foreach (var l in lines.Skip(1)) { var c = l.Split(','); before[c[0]] = head.Zip(c).ToDictionary(p => p.First, p => p.Second); }
        }
        float Was(UnitType t, string k) => before.TryGetValue(t.Id, out var r) && float.TryParse(r.GetValueOrDefault(k), NumberStyles.Float, inv, out var v) ? v : float.NaN;
        Console.WriteLine("class      tier  n   hp (was)      damage (was)   armour (was)  block (was)");
        foreach (var g in men.GroupBy(t => (t.Class, t.Tier)).OrderBy(g => g.Key.Class).ThenBy(g => g.Key.Tier))
        {
            string P(Func<UnitType, float> now, string k, string fmt) => $"{g.Average(now).ToString(fmt, inv),5} ({g.Select(t => Was(t, k)).Where(v => !float.IsNaN(v)).DefaultIfEmpty(float.NaN).Average().ToString(fmt, inv),5})";
            Console.WriteLine($"{g.Key.Class,-10} {Roman(g.Key.Tier),-4} {g.Count(),3}  {P(t => t.MaxHp, "hp", "0")}  {P(t => t.Damage, "damage", "0")}  {P(t => t.Armor, "armor", "0.0")}  {P(t => t.BlockChance, "block", "0.00")}");
        }
        Console.WriteLine("wrote balance/kit-after.csv");
    }

    static string Roman(int n) => n switch { 0 => "0", 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => n.ToString() };
}
