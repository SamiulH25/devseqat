using Microsoft.Xna.Framework;

namespace Ironfield;

/// Self-tests of the world map at scale (docs/archive/WORLD.md).
public static partial class SelfTest
{
    /// The river's segment grid (`River.Within`) answers exactly as `Dist(p) < radius` does, on campaign rivers and ridges,
    /// a battle river and a hand-made path with long diagonal runs and a repeated point (where `Dist` is NaN): the ground
    /// colours, `WaterDepth` and `WorldMap.Height` lean on it, so a seed's map and a replay must not move.
    static void RiverWithinExact()
    {
        var map = Campaign.New(11).Map;
        var rivers = map.Rivers.Concat(map.Ridges).ToList();
        rivers.Add(River.Across(new Random(4)));
        rivers.Add(new River(new[] { new Vector2(-300, -300), new Vector2(-100, 120), new Vector2(-100, 120), new Vector2(200, 160), new Vector2(260, -240) }, 9, Array.Empty<Vector2>()));
        var rng = new Random(9);
        int checks = 0, inside = 0;
        foreach (var r in rivers)
            for (int k = 0; k < 4000; k++)
            {
                var near = r.Path[rng.Next(r.Path.Length)] + new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * (k % 2 == 0 ? 12 : 70);
                var p = k % 5 == 0 ? new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * map.GroundHalf : near;
                foreach (float radius in new[] { 0.5f, r.Width / 2, r.Width / 2 + 3, r.Width, 30 })
                {
                    bool want = r.Dist(p) < radius, got = r.Within(p, radius);
                    if (want != got) { Check(false, $"Within({p}, {radius}) = {got} but Dist = {r.Dist(p)}"); return; }
                    checks++;
                    if (want) inside++;
                }
            }
        Check(inside > checks / 20, $"the check reached the water ({inside} of {checks} inside)");
        Console.WriteLine($"river grid: {checks} answers match Dist ({inside} inside)");
    }

    /// The player's march goes the way the land allows: across a river by its bridge rather than wading beside it, never
    /// through a lake, always ending on the goal, and planned quickly enough to answer a click.
    static void MarchRoutes()
    {
        var map = Campaign.New(11).Map;
        float WaterAt(Vector2 p) => map.Rivers.Any(r => r.Within(p, r.Width / 2) && !r.AtFord(p)) && map.RoadDist(p) > 3 ? 1 : 0;
        int tried = 0, wade = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var b in map.Bridges.Take(12))
        {
            var across = new Vector2(-b.Along.Y, b.Along.X);
            Vector2 from = b.At - b.Along * 18 + across * 35, to = b.At + b.Along * 18 + across * 35;
            if (WaterAt(from) + WaterAt(to) > 0 || map.InLake(from) || map.InLake(to)) continue;
            var route = map.PlanRoute(from, to, 1.3f);
            Check(route.Count > 0 && route[^1] == map.InLand(to), "a route ends on its goal");
            var at = from;
            foreach (var q in route)
            {
                for (float k = 0; k < 1; k += 0.5f / MathF.Max(1, Vector2.Distance(at, q)))
                {
                    var p = Vector2.Lerp(at, q, k);
                    Check(!map.InLake(p, -1), $"the march from {from} keeps out of the lakes");
                    wade += (int)WaterAt(p);
                }
                at = q;
            }
            tried++;
        }
        Check(tried >= 4, $"enough bridges to try ({tried})");
        Check(wade < tried * 4, $"marches cross by the bridges rather than wading ({wade} wet samples over {tried} marches)");
        var far = map.Settlements.OrderBy(s => s.Pos.X + s.Pos.Y).First().Pos;
        var other = map.Settlements.OrderBy(s => s.Pos.X + s.Pos.Y).Last().Pos;
        var t0 = clock.ElapsedMilliseconds;
        var long_ = map.PlanRoute(far, other, 1.3f);
        long ms = clock.ElapsedMilliseconds - t0;
        t0 = clock.ElapsedMilliseconds;
        map.PlanRoute(other, far, 1.3f);
        long again = clock.ElapsedMilliseconds - t0;
        Check(again < 400, $"a march across the map is planned in good time ({again} ms)");
        Check(long_[^1] == other, "the march across the map ends on its goal");
        Console.WriteLine($"march routes: {tried} bridge crossings, {wade} wet samples; across the map {long_.Count} legs in {ms} ms, {again} ms the second time (all {clock.ElapsedMilliseconds} ms)");
    }

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

    /// WM0 (docs/WORLDMAP.md): the designed world reads and is sound, and its province rule is the page's to the last bit
    /// (values from index.html's own `warp`, run in node).
    static void WorldDesignReads()
    {
        foreach (var (x, y, wx, wy) in new (double, double, double, double)[]
        {
            (0, 0, -7.7301459998078981, -10.837984924690796), (-620, -620, -618.87482998118787, -623.14446812961580),
            (619.5, -3.25, 622.66043395385566, -8.0668512108277373), (-232.1, 288.3, -227.87906672729753, 293.16291085347893),
            (100.7, -450.2, 108.26074332586522, -450.02547878008693), (-1.5, 1.5, -9.5507438729155645, -9.1130908568517128),
            (333.3, 77.7, 330.40776775642331, 82.971315689207401), (-555, 412, -561.85270567648479, 412.64126216712435),
            (12.25, -600, 13.641997776116668, -591.94377195733853), (-90.1, -0.4, -93.766216322053651, 4.5858507213790807),
            (250, 250, 241.62467547669448, 261.09049112777546), (-400.9, -120.6, -395.81745496762647, -114.54276686343474),
        })
        {
            var (ax, ay) = WorldDesign.Warp(x, y);
            Check(Math.Abs(ax - wx) < 1e-9 && Math.Abs(ay - wy) < 1e-9, $"the warp at {x}, {y} is the page's ({ax}, {ay} against {wx}, {wy})");
        }
        var w = WorldDesign.Get();
        var errs = w.Validate().ToList();
        Check(errs.Count == 0, "world.json is sound: " + string.Join("; ", errs.Take(5)));
        int land = w.Provinces.Count(p => p.Kind != "Sea");
        Console.WriteLine($"world: {w.Title} rev {w.Revision}: {land} land provinces, {w.Provinces.Length - land} sea, {w.Settlements.Length} settlements");
        Check(land > 0 && w.Realms.All(r => w.Settlements.Any(s => s.Realm == r.Id && s.Kind == "town")), "every realm has a town");
        Check(w.Realms.All(r => Factions.All.Any(f => f.Culture == r.Culture)), "every realm's culture is a realm culture of factions.json");
        Check(w.Subcultures.All(c => Factions.Culture(c.Id) != null), "every subculture is a culture in factions.json");
    }

    /// WM1 (docs/WORLDMAP.md): layout 5 is the drawn world, the same for every seed: its land provinces with invented names,
    /// its settlements in their own realm's land and none at sea, every town and castle joined by road, and a campaign runs.
    static void DesignedMap()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var a = Campaign.New(7, layout: WorldMap.DesignLayout);
        Console.WriteLine($"world: layout 5 campaign made in {clock.ElapsedMilliseconds} ms");
        clock.Restart();
        var b = Campaign.New(8, layout: WorldMap.DesignLayout);
        Console.WriteLine($"world: the next in {clock.ElapsedMilliseconds} ms (the province grid is made once)");
        var d = WorldDesign.Get();
        var m = a.Map;
        Check(m.Designed && m.Provinces.Count == d.Provinces.Count(p => p.Kind != "Sea"), $"one province a land province of the design ({m.Provinces.Count})");
        Check(m.Settlements.Count == d.Settlements.Length && m.Sites.Count == d.Sites.Length, "every drawn settlement and site is on the map");
        Check(m.Provinces.Select(p => p.Name).SequenceEqual(b.Map.Provinces.Select(p => p.Name)) && m.Settlements.Select(s => s.Pos).SequenceEqual(b.Map.Settlements.Select(s => s.Pos)),
            "the same land for every seed");
        Check(m.Provinces.Select(p => p.Name).Distinct().Count() == m.Provinces.Count && m.Provinces.All(p => d.Provinces.All(q => q.Name != p.Name)), "province names are invented and unique");
        Check(m.Settlements.All(s => !m.InSea(s.Pos) && m.ProvinceAt(s.Pos).Id == s.Province && m.Provinces[s.Province].Faction == s.Faction), "every settlement on land, in its own realm's province");
        Check(m.Provinces.All(p => p.Neighbors.Count > 0 && p.Neighbors.All(n => m.Provinces[n].Neighbors.Contains(p.Id))), "every province has neighbours, and they have it");
        var low = m.Settlements.MinBy(s => m.Height(s.Pos.X, s.Pos.Y))!;
        Check(m.Height(0, -600) < -WorldMap.SeaDepth * 0.8f, $"the open sea lies low ({m.Height(0, -600):0.0})");
        Check(m.Height(low.Pos.X, low.Pos.Y) > 0, $"every settlement stands above the sea ({low.Name} at {m.Height(low.Pos.X, low.Pos.Y):0.0}, land {m.Landness(low.Pos):0.00})");
        Check(m.LandShare is > 0.3f and < 0.6f, $"about two fifths of the map is land ({m.LandShare:0.00})");
        // Roads: every town and castle reaches every other over the road lines (through villages too).
        var all = m.Settlements;
        int At(Vector2 p) => all.FindIndex(s => Vector2.DistanceSquared(s.Pos, p) < 1);
        var links = all.Select(_ => new List<int>()).ToArray();
        foreach (var l in m.RoadLines)
            if (At(l.Points[0]) is int i and >= 0 && At(l.Points[^1]) is int j and >= 0) { links[i].Add(j); links[j].Add(i); }
        int first = all.FindIndex(s => s.Kind != SettlementKind.Village);
        var seen = new HashSet<int> { first };
        var open = new Queue<int>(seen);
        while (open.Count > 0) foreach (int j in links[open.Dequeue()]) if (seen.Add(j)) open.Enqueue(j);
        var cut = all.Where(s => s.Kind != SettlementKind.Village && !seen.Contains(s.Id)).ToList();
        Console.WriteLine($"world: {m.Provinces.Count} provinces, {m.Settlements.Count} settlements, {m.RoadLines.Count} roads, {m.Rivers.Count} rivers, {m.Ridges.Count} ridges, {m.Lakes.Count} lakes and ponds, land {m.LandShare:0.00}");
        Check(cut.Count == 0, "every town and castle is joined by road: not " + string.Join(", ", cut.Select(s => $"{s.Name} at {s.Pos}")));
        // The sea (WM2): a point at sea is put ashore, a way between far towns keeps to the land, nobody walks on water.
        var shore = m.InLand(new Vector2(0, -600));
        Check(m.InSea(new Vector2(0, -600)) && !m.InSea(shore), $"a point at sea is put on the shore ({shore})");
        var towns = m.Settlements.Where(s => s.Kind == SettlementKind.Town).ToList();
        foreach (var (from, to) in new[] { (towns[0], towns[^1]), (towns[3], towns[towns.Count / 2]) })
        {
            var way = m.PlanRoute(from.Pos, to.Pos, 1.3f);
            var at = from.Pos;
            foreach (var w in way)
            {
                for (float k = 0; k <= 1; k += 0.05f) Check(!m.InSea(Vector2.Lerp(at, w, k)), $"the way from {from.Name} to {to.Name} keeps off the sea");
                at = w;
            }
            Check(way[^1] == m.InLand(to.Pos), "a way ends at its goal");
        }
        a.Tick(24 * 3);
        Check(a.S.Parties.Count > 0, "a campaign runs on it");
        var wet = a.S.Parties.Where(q => !q.Down && m.InSea(q.Pos)).ToList();
        Check(wet.Count == 0, "nobody on the sea: " + string.Join(", ", wet.Select(q => $"{q.Name} at {q.Pos}")));
    }
}
