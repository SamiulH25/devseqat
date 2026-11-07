using System.Diagnostics;

namespace Ironfield;

/// `dotnet run -c Release -- --bench [men per side]`: a headless battle at scale, printing ms per tick for each stage.
/// The check for every change that touches the battle's hot path (docs/COMBAT_OVERHAUL.md 2.2).
public static class Bench
{
    public static void Run(int men, int seconds = 60)
    {
        var rng = new Random(7);
        var b = new Battle(7, Armies.Divide(Armies.Large(rng, 2, 0, men)), Armies.Divide(Armies.Large(rng, 2, 1, men)),
            new BattleOptions { AutoPlayer = true, Cap = men * 2 + 20 });
        var profile = new double[Battle.Stages.Length];
        long markAt = 0;
        b.Probe = stage =>
        {
            long now = Stopwatch.GetTimestamp();
            if (stage >= 0) profile[stage] += (now - markAt) * 1000.0 / Stopwatch.Frequency;
            markAt = now;
        };
        var ticks = new List<double>();
        var clock = new Stopwatch();
        int on = b.Units.Count(u => u.Active);
        for (int i = 0; i < 60 * seconds && b.Winner < 0; i++)
        {
            clock.Restart();
            b.Update(1 / 60f);
            ticks.Add(clock.Elapsed.TotalMilliseconds);
        }
        ticks.Sort();
        double mean = ticks.Average();
        Console.WriteLine($"bench: {on} men on the field, {ticks.Count} ticks ({b.Time:0}s), winner {b.Winner}, {b.Units.Count(u => u.Dead)} dead");
        Console.WriteLine($"  tick ms: mean {mean:0.00}, p95 {ticks[(int)(ticks.Count * 0.95)]:0.00}, max {ticks[^1]:0.00}");
        Console.WriteLine("  stages: " + string.Join(", ", Battle.Stages.Select((s, i) => $"{s} {profile[i] / ticks.Count:0.00}")));
    }
}
