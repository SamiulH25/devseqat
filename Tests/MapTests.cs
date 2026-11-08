using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the world map at scale (docs/WORLD.md).
public static partial class SelfTest
{
    /// W0: the look-up grid gives exactly the full scans' answers (on and off the grid, new and legacy maps), so a map
    /// built from a save's seed places everything where it was.
    static void MapIndexMatches()
    {
        var rng = new Random(3);
        foreach (var (seed, extent) in new[] { (11, WorldMap.Extent), (12, WorldMap.Extent), (11, WorldMap.PreviousExtent), (5, WorldMap.LegacyExtent) })
        {
            var m = WorldMap.Generate(seed, extent);
            float half = m.GroundHalf + 80;
            int bad = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 20000; i++)
            {
                var p = new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * half;
                if (i % 4 == 0 && m.Rivers.Count > 0) { var r = m.Rivers[i % m.Rivers.Count]; p = r.Path[rng.Next(r.Path.Length)] + new Vector2(rng.NextSingle() * 12 - 6, rng.NextSingle() * 12 - 6); }
                if (m.ProvinceAt(p) != m.ProvinceScan(p) || m.RoadDist(p) != m.RoadScan(p) || m.RiverDist(p) != m.RiverScan(p)) bad++;
            }
            Check(bad == 0, $"seed {seed} extent {extent}: the map index agrees with the full scan ({bad} of 20000 points differ)");
            Console.WriteLine($"map index: seed {seed}, {m.Provinces.Count} provinces, {m.Roads.Count} roads; 20000 checks in {clock.ElapsedMilliseconds} ms");
        }
    }

    /// W1: a new map grows each data realm from its home in one piece with a capital town; an old save's map keeps the old
    /// bands; the opening wars come from the data; a realm founded in play keeps its look whatever its id.
    static void RealmsFromData()
    {
        foreach (int seed in new[] { 11, 12, 13 })
        {
            var m = WorldMap.Generate(seed);
            var sizes = new List<int>();
            for (int f = 0; f < Factions.All.Count; f++)
            {
                var land = m.Provinces.Where(p => p.Faction == f).ToList();
                sizes.Add(land.Count);
                Check(land.Count >= 4, $"seed {seed}: {Factions.All[f].Name} holds land ({land.Count} provinces)");
                Check(m.Settlements.Any(s => s.Faction == f && s.Kind == SettlementKind.Town), $"seed {seed}: {Factions.All[f].Name} has a town");
                // One piece: from any of its provinces, walking its own links reaches all of them (unlinked provinces aside).
                var linked = land.Where(p => p.Neighbors.Count > 0).ToList();
                var seen = new HashSet<int> { linked[0].Id };
                var todo = new Queue<int>(seen);
                while (todo.TryDequeue(out int i))
                    foreach (int j in m.Provinces[i].Neighbors)
                        if (m.Provinces[j].Faction == f && seen.Add(j)) todo.Enqueue(j);
                Check(seen.Count == linked.Count, $"seed {seed}: {Factions.All[f].Name}'s land is in one piece ({seen.Count} of {linked.Count})");
            }
            Console.WriteLine($"realms: seed {seed}, provinces a realm {string.Join("/", sizes)}");
        }
        // The old bands: north 30% to the Northmarch, the south split west/east (an old save's map must not change).
        var old = WorldMap.Generate(11, WorldMap.Extent, WorldMap.LegacyLayout);
        var north = old.Provinces.OrderBy(p => p.Center.Y).Take(old.Provinces.Count * 3 / 10).ToHashSet();
        var south = old.Provinces.Where(p => !north.Contains(p)).OrderBy(p => p.Center.X).ToList();
        Check(old.Provinces.All(p => p.Faction == (north.Contains(p) ? 2 : south.IndexOf(p) < south.Count / 2 ? 0 : 1)), "an old save's map keeps the old realm bands");
        var c = Campaign.New(11);
        foreach (var r in Factions.Relations)
        {
            var w = c.WarBetween(Factions.IndexOf(r.A), Factions.IndexOf(r.B));
            Check(w != null && w.Active == r.War && (r.War || MathF.Abs(w.TruceUntil - r.TruceDays * 24) < 1e-3f), $"{r.A} and {r.B} open as the data says");
        }
        Check(c.S.Layout == WorldMap.CurrentLayout && c.S.DataRealms == Factions.All.Count, "a new campaign records its layout and realms");
        var states = Enumerable.Range(0, 2).Select(f => new FactionState { Id = f, Name = "x" }).ToList();
        states[1].Founded = true; states[1].Name = "Realm of Testford"; states[1].Palette = 2; states[1].Culture = "azure";
        Factions.Bind(states);
        Check(Factions.Get(1).Name == "Realm of Testford" && Factions.Get(0).Name == Factions.All[0].Name, "a founded realm is known by its flag, not its id");
        Factions.Bind(c.S.Factions);
    }

    /// W2: a new map is bigger and has its climates (fens, steppe, badlands where the data puts them), ridges crossed by
    /// roads at passes and kept clear of every settlement, slow going on the slopes and the road's pace at a pass; an old
    /// save's map has none of it.
    static void ClimatesAndRidges()
    {
        foreach (int seed in new[] { 11, 12, 13 })
        {
            var m = WorldMap.Generate(seed);
            float span = m.ExtentUsed / WorldMap.LegacyExtent;
            var kinds = m.Provinces.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count());
            foreach (var k in new[] { "Marsh", "Steppe", "Badlands", "Forest", "Hills" })
                Check(kinds.GetValueOrDefault(k) >= 2, $"seed {seed}: the map has {k} country ({kinds.GetValueOrDefault(k)})");
            Check(m.Provinces.Where(p => p.Kind == "Steppe").Average(p => p.Center.X) > m.ExtentUsed * 0.3f, $"seed {seed}: the steppe lies in the east");
            Check(m.Provinces.Where(p => p.Kind == "Badlands").Average(p => p.Center.Y) > m.ExtentUsed * 0.3f, $"seed {seed}: the badlands lie in the south");
            Check(m.Ridges.Count >= 2, $"seed {seed}: the land has ridges ({m.Ridges.Count})");
            Check(m.Ridges.Sum(r => r.Fords.Length) >= 2 && m.Ridges.All(r => r.Fords.All(f => m.RoadDist(f) < 0.5f)), $"seed {seed}: roads cross the ridges at passes");
            Check(m.Settlements.All(st => m.RidgeDist(st.Pos) > 9 * span), $"seed {seed}: no settlement stands on a ridge");
            // The crest point furthest from any pass, road or river.
            var crest = m.Ridges.SelectMany(r => r.Path).OrderByDescending(p => MathF.Min(MathF.Min(m.RoadDist(p), m.RiverDist(p)),
                m.Ridges.SelectMany(r => r.Fords).Select(f => Vector2.Distance(f, p)).DefaultIfEmpty(999).Min())).First();
            Check(m.SpeedFactor(crest, 1.3f) < 0.5f && m.Height(crest.X, crest.Y) > 8, $"seed {seed}: a ridge is high, slow ground ({m.SpeedFactor(crest, 1.3f):0.00}, {m.Height(crest.X, crest.Y):0.0})");
            foreach (var pass in m.Ridges.SelectMany(r => r.Fords)) Check(m.SpeedFactor(pass, 1.3f) == 1.3f, $"seed {seed}: a pass keeps the road's pace");
            Console.WriteLine($"climates: seed {seed}, {m.Provinces.Count} provinces ({string.Join(", ", kinds.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"))}), "
                + $"{m.Ridges.Count} ridges with {m.Ridges.Sum(r => r.Fords.Length)} passes, {m.Rivers.Count} rivers");
        }
        var old = WorldMap.Generate(11, WorldMap.PreviousExtent, WorldMap.LegacyLayout);
        Check(old.Ridges.Count == 0 && old.Rivers.Count == 2 && old.Provinces.All(p => Array.IndexOf(WorldMap.Kinds, p.Kind) < 4), "an old save's map has no ridges or new country");
    }
}
