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

    /// W3: seven realms, each with its own heraldry (colour and charge both differ), its own troops in at least two lines and
    /// borrowed ones only from its kin; men may train as their kin's troops but not as a stranger's.
    static void SevenRealms()
    {
        Check(Factions.All.Count == 7, $"seven realms ({Factions.All.Count})");
        var palettes = Factions.All.Select(f => f.Palette).ToList();
        Check(palettes.Distinct().Count() == 7 && palettes.All(p => p != 2 && p != 3 && p < Style.Sides.Length), "each realm has its own palette (not yours, not the brigands')");
        Check(palettes.Select(p => Style.Sides[p].Charge).Distinct().Count() == 7, "each realm has its own charge, so colour is never the only difference");
        // W4: the new realms' four lines are their own culture's from tier III up (tiers I-II are the shared or kin recruits);
        // no realm fields a stranger's troops.
        foreach (var f in Factions.All)
            Check(f.Lines.Values.All(l => l.All(t => Factions.Open(f.Culture, Troops.Get(t).Culture))), $"{f.Name} fields no stranger's troops");
        foreach (var f in Factions.All.Skip(3))
        {
            int ownLines = f.Lines.Values.Count(l => l.Skip(2).All(t => Troops.Get(t).Culture == f.Culture));
            Check(ownLines == 4, $"{f.Name} fields its own troops in all four lines ({ownLines})");
        }
        Check(Factions.All.Skip(3).All(f => Factions.Culture(f.Culture)!.Kin.Length > 0), "the new realms' men may still train as their kin's troops");
        var looks = Troops.All.Where(t => t.Culture is "southern" or "steppe" or "league").Select(t => t.Look).ToList();
        Check(looks.Any(l => l.Head == "turban") && looks.Any(l => l.Head == "steppe") && looks.Any(l => l.Body == "lamellar") && looks.Any(l => l.Body == "deel")
            && looks.Any(l => l.Body == "brigandine") && looks.Any(l => l.Shield == "pavise"), "the new cultures wear their own kit");
        Check(Troops.All.Any(t => t.Mount?.Barding == "camel") && Troops.All.Any(t => t.Mount?.Id == "steppe_pony"), "the south rides camels, the grass ponies");
        Check(Factions.Open("southern", "crimson") && Factions.Open("order", "azure") && !Factions.Open("azure", "southern") && !Factions.Open("league", "northern"),
            "men train as their kin's troops, not a stranger's");
        Console.WriteLine("realms: " + string.Join("; ", Factions.All.Select(f => $"{f.Name} ({Style.Sides[f.Palette].Name}, {Style.Sides[f.Palette].Charge})")));
    }

    /// W5: each realm's temper from the data (aggression, garrisons, siege, tribute, what it hunts); wars only on neighbours;
    /// realms ally against a stronger common enemy; a realm with 30% of seven realms' seats unites its neighbours against it.
    static void RealmTempers()
    {
        var order = Factions.All.First(f => f.Hunts == "monsters");
        Check(order.Culture == "order" && Factions.All.Select(f => f.Aggression).Distinct().Count() > 2, "realms differ in temper; the Order hunts monsters");
        var c = Campaign.New(5101);
        int orderId = Factions.IndexOf(order.Name), azure = 0;
        // Garrisons: the Saltmarsh League keeps fuller seats than the Khaganate.
        float Fill(int f) => c.S.Settlements.Where(s => s.Faction == f && s.Kind != SettlementKind.Village).SelectMany(s => s.Garrison)
            .Average(g => g.Men / (float)Rules.CapOf(g.Troop, Rules.RoleOf(g)));
        int league = Factions.IndexOf("Saltmarsh League"), khaganate = Factions.IndexOf("Khaganate of the Grass");
        Console.WriteLine($"tempers: garrison fill league {Fill(league):0.00}, khaganate {Fill(khaganate):0.00}");
        Check(Fill(league) > Fill(khaganate), "the League keeps fuller garrisons than the Khaganate");
        // A lord of the Order digs out a holdout he outweighs; an Azure lord leaves it be.
        string Hunt(int f)
        {
            var w = Campaign.New(5102);
            w.Player.Cooldown = 1e6f;
            var lord = w.S.Parties.First(p => p.Kind == PartyKind.Lord && p.Faction == f);
            var den = w.S.Parties.First(p => p.Kind == PartyKind.Monster && p.Lair);
            foreach (var p in w.S.Parties.Where(p => p != lord && p != den)) p.Pos = new Vector2(5000, 5000);
            lord.Pos = den.Pos + new Vector2(10, 0);
            lord.Companies.Clear();
            foreach (var role in new[] { "infantry", "archers", "spears", "infantry" }) lord.Companies.Add(w.NewCompany(Armies.LineTroop(role, 5, f), role, 60, 0));
            den.Companies.RemoveAll(k => k != den.Companies[0]);
            den.Companies[0].Stacks[0].Men = 8;
            lord.SiegeTarget = -1; lord.Task = "patrol";
            w.LordThink(lord);
            return lord.Task == "hunt" && lord.ChaseId == den.Id ? "hunt" : lord.Task;
        }
        string o = Hunt(orderId), a = Hunt(azure);
        Console.WriteLine($"tempers: by a holdout the Order's lord does {o}, the Crown's {a}");
        Check(o == "hunt" && a != "hunt", "the Order hunts a holdout it outweighs; the Crown leaves it be");
        // Wars: with every truce lifted, the realms go to war only with neighbours.
        var d = Campaign.New(5103);
        foreach (var w in d.S.Wars) { w.Active = false; w.TruceUntil = 0; }
        var daily = typeof(Campaign).GetMethod("DiplomacyDaily", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var borders = typeof(Campaign).GetMethod("Borders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        for (int i = 0; i < 200; i++) daily.Invoke(d, null);
        var wars = d.S.Wars.Where(w => w.Active).ToList();
        Console.WriteLine($"tempers: {wars.Count} wars declared in 200 days of diplomacy, {d.S.Pacts.Count} alliances");
        Check(wars.Count > 0 && wars.All(w => (bool)borders.Invoke(d, new object[] { w.A, w.B })!), "realms declare war only on their neighbours");
        // Alliances: two realms at war with the same stronger realm ally against it.
        var e = Campaign.New(5104);
        foreach (var w in e.S.Wars) { w.Active = false; w.TruceUntil = 1e9f; }
        int big = Enumerable.Range(0, e.S.Factions.Count).MaxBy(f => e.RealmStrength(f));
        var two = Enumerable.Range(0, e.S.Factions.Count).Where(f => f != big).Take(2).ToList();
        foreach (var f in two) { if (e.WarBetween(f, big) is War old) old.Active = true; else e.S.Wars.Add(new War { A = f, B = big }); }
        var alliances = typeof(Campaign).GetMethod("AlliancesDaily", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        for (int i = 0; i < 200 && !e.Allied(two[0], two[1]); i++) alliances.Invoke(e, null);
        Check(e.Allied(two[0], two[1]), "two realms at war with a stronger one ally against it");
        // The coalition: 30% of the seats unites the neighbours.
        var g = Campaign.New(5105);
        Check(MathF.Abs(g.LeaderShare - 0.3f) < 1e-3f, $"seven realms unite against 30% of the seats ({g.LeaderShare:0.00})");
        var seats = g.S.Settlements.Where(s => s.Kind != SettlementKind.Village).ToList();
        foreach (var s in seats.OrderBy(s => Vector2.Distance(s.Pos, seats.Where(t => t.Faction == azure).First().Pos)).Take((int)(seats.Count * 0.33f))) s.Faction = azure;
        Check(g.Leading(azure), "a realm with a third of the seats leads");
        Days(g, 1);
        var neighbours = Enumerable.Range(1, g.S.Factions.Count - 1).Where(f => g.Alive(f) && (bool)borders.Invoke(g, new object[] { azure, f })!).ToList();
        Check(neighbours.Count > 0 && neighbours.All(f => g.FactionsAtWar(azure, f)), "its neighbours take up arms against it");
        // Choose your start: a company raised in the Khaganate starts in its land with its villages' clansmen, and takes service there.
        var k = Campaign.New(5106, khaganate);
        var at = k.SettlementAt(k.Player.Pos) ?? k.S.Settlements.OrderBy(s => Vector2.Distance(s.Pos, k.Player.Pos)).First();
        Check(at.Faction == khaganate && k.Player.Companies[0].Troop == "clansmen", $"a Khaganate start is in its land with clansmen ({at.Name}, {k.Player.Companies[0].Troop})");
        var pilot = new Pilot(k);
        pilot.Run(6);
        Check(k.Captain.Contract == khaganate, $"the stand-in takes service with the realm it started in (contract {k.Captain.Contract}, faction {k.Player.Faction}, over {k.S.GameOver} {k.S.EndReason})");
    }
}
