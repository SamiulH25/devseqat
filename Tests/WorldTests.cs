namespace Ironfield;

/// Self-tests of the side content (docs/SIDE_CONTENT.md): names, sites, quests, roads, town life, events.
public static partial class SelfTest
{
    /// S1: no place name repeats on a map, and a realm's lords carry its culture's names.
    static void WorldNames()
    {
        foreach (int seed in new[] { 11, 12, 13 })
        {
            var m = WorldMap.Generate(seed);
            var dup = m.Settlements.GroupBy(s => s.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Check(dup.Count == 0, $"seed {seed}: no repeated place names ({string.Join(", ", dup.Take(5))})");
            Check(m.Provinces.All(p => m.Settlements.Any(s => s.Province == p.Id && s.Name == p.Name && s.Kind != SettlementKind.Village)),
                "a province takes its seat's name");
        }
        var c = Campaign.New(5);
        var names = Names.Get();
        var lords = c.S.Characters.Where(x => x.Kind == "lord" && x.Faction is >= 0 and < 3).ToList();
        int native = lords.Count(l => names.LordsOf(Factions.CultureOf(l.Faction)).Contains(l.Name.Split(' ')[1]));
        Check(lords.Count > 0 && native == lords.Count, $"lords carry their culture's names ({native}/{lords.Count})");
        Check(lords.Select(l => l.Name).Distinct().Count() >= lords.Count - 1, "lords' names hardly ever repeat");
        var m11 = WorldMap.Generate(11);
        Console.WriteLine($"names: {m11.Settlements.Count} places on seed 11, e.g. {string.Join(", ", m11.Settlements.Take(4).Select(s => s.Name))}; "
            + $"{c.S.Provinces[0].Title}; {lords[0].Name}");
    }
}
