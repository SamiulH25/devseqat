using System.Diagnostics;

namespace Ironfield;

/// `dotnet run -c Release -- --bench [men per side]`: a headless battle at scale, printing ms per tick for each stage.
/// The check for every change that touches the battle's hot path (docs/COMBAT_OVERHAUL.md 2.2).
public static class Bench
{
    public static void Run(int men, int seconds = 60)
    {
        var built = Stopwatch.StartNew();
        foreach (var g in new[] { TerrainParams.Meadow, TerrainParams.Forest, TerrainParams.Marsh }) _ = new Terrain(7, g);
        Console.WriteLine($"bench: terrain builds in {built.Elapsed.TotalMilliseconds / 3:0} ms (mean of Meadow, Forest, Marsh, ground layer included)");
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
        Console.WriteLine($"bench: {on} men on the field, {ticks.Count} ticks ({b.Time:0}s), winner {b.Winner}, {b.Units.Count(u => u.Dead)} dead; {b.Terrain.Layout} field, {b.FlowBuilds} paths built");
        Console.WriteLine($"  tick ms: mean {mean:0.00}, p95 {ticks[(int)(ticks.Count * 0.95)]:0.00}, max {ticks[^1]:0.00}");
        Console.WriteLine("  stages: " + string.Join(", ", Battle.Stages.Select((s, i) => $"{s} {profile[i] / ticks.Count:0.00}")));
    }

    /// `dotnet run -c Release -- --campaign-bench [days]`: a headless campaign for some days (the player idles, encounters
    /// are waved through), printing ms per day for each tick stage. The check for every change that grows the world (docs/WORLD.md W0).
    public static void Campaign(int days)
    {
        var clock = Stopwatch.StartNew();
        var c = Ironfield.Campaign.New(11);
        double setup = clock.Elapsed.TotalMilliseconds;
        var profile = new double[Ironfield.Campaign.Stages.Length];
        long markAt = 0;
        c.Probe = stage =>
        {
            long now = Stopwatch.GetTimestamp();
            if (stage >= 0) profile[stage] += (now - markAt) * 1000.0 / Stopwatch.Frequency;
            markAt = now;
        };
        clock.Restart();
        for (int h = 0; h < days * 24; h++) { c.Tick(1); c.Encounter = null; c.Arrived = null; }
        double total = clock.Elapsed.TotalMilliseconds;
        Console.WriteLine($"campaign bench: {c.Map.Provinces.Count} provinces, {c.S.Settlements.Count} settlements, {c.Map.Roads.Count} roads, {c.S.Factions.Count} realms");
        Console.WriteLine($"  setup {setup:0} ms; {days} days, {total / days:0.0} ms a day; parties now {c.S.Parties.Count(p => !p.Down)} ({c.S.Parties.Count(p => p.Kind == PartyKind.Lord && !p.Down)} lords)");
        Console.WriteLine("  ms a day by stage: " + string.Join(", ", Ironfield.Campaign.Stages.Select((s, i) => $"{s} {profile[i] / days:0.0}")));
    }
}
