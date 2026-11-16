using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ironfield;

public static partial class BalanceReport
{
    /// `--troop-balance [tier|nobles|all] [seeds]`: high-speed parallel headless battle harness that tests every fighting
    /// troop against tier peers at equal gold cost (and verifies noble tier-ladder progression), writing balance/troops_balance.csv.
    public static void TroopBalance(string scope = "nobles", int seeds = 2)
    {
        Directory.CreateDirectory("balance");
        var sw = Stopwatch.StartNew();
        var inv = CultureInfo.InvariantCulture;
        var fighting = Troops.All
            .Where(t => t.Culture != "wild" && t.Class is not (UnitClass.Caster or UnitClass.Captain) && t.Tier >= 1)
            .OrderBy(t => t.Tier).ThenBy(t => t.Class).ThenBy(t => t.Culture).ThenBy(t => t.Id)
            .ToList();

        int? onlyTier = int.TryParse(scope, out int parsed) ? parsed : null;
        bool noblesOnly = scope.Equals("nobles", StringComparison.OrdinalIgnoreCase);

        var targets = fighting.Where(t =>
            onlyTier != null ? t.Tier == onlyTier :
            noblesOnly ? (t.Noble && t.Tier >= 4) :
            t.Tier >= 3).ToList();

        // Build matchups: each target fights up to 4 peers of the same tier (same class + cross-class) at equal gold budget (3200g).
        const int Budget = 3200;
        var jobs = new List<(UnitType A, UnitType B, int Na, int Nb, int Seed, string Kind)>();
        var rng = new Random(1337);
        foreach (var (a, ai) in targets.Select((t, i) => (t, i)))
        {
            var sameTier = fighting.Where(b => b.Tier == a.Tier && b.Id != a.Id).ToList();
            var sameClass = sameTier.Where(b => b.Class == a.Class).OrderBy(_ => rng.Next()).Take(2).ToList();
            var crossClass = sameTier.Where(b => b.Class != a.Class).OrderBy(_ => rng.Next()).Take(2).ToList();
            int bi = 0;
            foreach (var b in sameClass.Concat(crossClass))
            {
                int na = Math.Clamp((int)MathF.Round(Budget / Math.Max(1f, a.Cost)), 8, 80);
                int nb = Math.Clamp((int)MathF.Round(Budget / Math.Max(1f, b.Cost)), 8, 80);
                for (int s = 0; s < seeds; s++)
                    jobs.Add((a, b, na, nb, Rng.Mix(s, ai, bi++), b.Class == a.Class ? "peer" : "cross"));
            }
        }

        // Also test each troop against its direct upgradesTo children (equal headcount 24 vs 24) to ensure higher tiers win.
        foreach (var a in targets)
            foreach (var upId in a.UpgradesTo)
            {
                if (!Troops.Has(upId)) continue;
                var b = Troops.Get(upId);
                if (b.Class is UnitClass.Caster or UnitClass.Captain) continue;
                for (int s = 0; s < Math.Max(1, seeds); s++)
                    jobs.Add((a, b, 24, 24, 5000 + s * 31 + a.Id.Length, "upgrade"));
            }

        // Warm up lazy-loaded static tables before parallel battle execution.
        _ = Ironfield.Grounds.Get(GroundKind.Grass);
        _ = Factions.All;
        _ = Balance.Get();

        var results = new ConcurrentBag<(string A, string B, int TierA, int TierB, string ClassA, string CultureA, string Kind, bool WonA, float SurvA, float Secs)>();
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, job =>
        {
            bool swap = (Math.Abs(job.Seed) % 2) == 1;
            var left = new ArmySpec(job.A.Id, Math.Clamp(job.A.Tier - 2, 1, 4), new[] { new Company(job.A.Id, job.Na, RoleOf(job.A)) });
            var right = new ArmySpec(job.B.Id, Math.Clamp(job.B.Tier - 2, 1, 4), new[] { new Company(job.B.Id, job.Nb, RoleOf(job.B)) });
            var battle = new Battle(Math.Abs(job.Seed), swap ? right : left, swap ? left : right,
                new BattleOptions { AutoPlayer = true, PlayerWounded = true });
            for (int step = 0; battle.Winner < 0 && step < 60 * 180; step++)
                battle.Update(1 / 60f);

            int teamA = swap ? 1 : 0;
            bool wonA = battle.Winner == teamA;
            int aliveA = battle.Units.Count(u => u.Team == teamA && !u.Dead && !u.Routing);
            results.Add((job.A.Id, job.B.Id, job.A.Tier, job.B.Tier, job.A.Class.ToString(), job.A.Culture, job.Kind, wonA, aliveA / (float)job.Na, battle.Time));
        });

        var list = results.ToList();
        var csv = new StringBuilder("troop,tier,class,culture,stock,cost,fights,win_pct,surv_pct,avg_sec,upgrade_loss_pct\n");
        var rows = new List<(string Id, int Tier, string Class, string Culture, string Stock, int Cost, int Fights, float WinPct, float SurvPct, float Secs, float UpLossPct)>();

        foreach (var a in targets)
        {
            var equalCost = list.Where(r => r.A == a.Id && r.Kind != "upgrade").ToList();
            var upg = list.Where(r => r.A == a.Id && r.Kind == "upgrade").ToList();
            int n = equalCost.Count;
            float winPct = n > 0 ? 100f * equalCost.Count(r => r.WonA) / n : 0;
            float survPct = n > 0 ? 100f * equalCost.Average(r => r.SurvA) : 0;
            float secs = n > 0 ? equalCost.Average(r => r.Secs) : 0;
            float upLoss = upg.Count > 0 ? 100f * upg.Count(r => !r.WonA) / upg.Count : 0;
            rows.Add((a.Id, a.Tier, a.Class.ToString(), a.Culture, a.Stock, (int)a.Cost, n, winPct, survPct, secs, upLoss));
            csv.AppendLine(string.Join(",", a.Id, a.Tier, a.Class, a.Culture, a.Stock, (int)a.Cost, n,
                winPct.ToString("0.0", inv), survPct.ToString("0.0", inv), secs.ToString("0.0", inv), upLoss.ToString("0.0", inv)));
        }

        File.WriteAllText("balance/troops_balance.csv", csv.ToString());
        Console.WriteLine($"troop-balance ({scope}, {jobs.Count} battles across {Environment.ProcessorCount} cores in {sw.Elapsed.TotalSeconds:0.1}s):");
        Console.WriteLine($"{"troop",-22} {"T"} {"class",-10} {"cult",-8} {"cost",4} {"win%",6} {"surv%",6} {"sec",5}  flag");
        foreach (var r in rows.OrderBy(x => x.Tier).ThenByDescending(x => x.WinPct))
        {
            string flag = r.WinPct >= 80 ? "STRONG" : r.WinPct <= 20 ? "WEAK" : "";
            Console.WriteLine($"{r.Id,-22} {r.Tier} {r.Class,-10} {r.Culture,-8} {r.Cost,4} {r.WinPct,5:0.0}% {r.SurvPct,5:0.0}% {r.Secs,5:0.0}  {flag}");
        }
        Console.WriteLine("wrote balance/troops_balance.csv");
    }

    static string RoleOf(UnitType t) => t.Class switch
    {
        UnitClass.Archer => "archers",
        UnitClass.Skirmisher => "spears",
        UnitClass.Cavalry => "horse",
        _ => "infantry",
    };
}
