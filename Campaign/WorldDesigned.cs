using Microsoft.Xna.Framework;

namespace Ironfield;

/// WorldMap layout 5 (docs/archive/WORLDMAP.md WM1): the map built from the designed world (`Data/world.json`, `WorldDesign`)
/// instead of from a seed. Every campaign on it has the same land; the campaign's seed still deals lords, weather and
/// battles. Provinces are the design's land provinces (the sea is a look-up, not a province); borders follow the design's
/// warped nearest-centre rule, sampled once on a grid.
public sealed partial class WorldMap
{
    /// How much larger the game's map is than the drawn one: every drawn place, lake and ridge is spread out by this,
    /// so towns stand further apart (the owner, 2026-11-17: the drawn spacing felt crowded). Rivers keep their widths.
    public const float DesignScale = 1.33f;
    static Vector2 Drawn(float x, float y) => new Vector2(x, y) * DesignScale;
    static Vector2 Drawn(float[] p) => WorldDesign.At(p) * DesignScale;

    /// The layout built from Data/world.json.
    public const int DesignLayout = 5;
    /// The seed every designed map's own noise uses (ground, hills, ponds, invented names): the same land for every campaign.
    public const int DesignSeed = 1105;
    /// The size of a cell of the province grid on a designed map (world units).
    const float DesignCell = 2;

    /// Built from the design (layout 5 and on).
    public bool Designed => Layout >= DesignLayout;
    /// The share of the ground in play that is land (1 on a generated map, which is all land).
    public float LandShare { get; private set; } = 1;

    // The designed map's grid: each cell's land province (for a sea cell, the nearest land province), whether it is sea,
    // and a softened land fraction for the shore's heights.
    short[]? cellProvince;
    bool[]? cellSea;
    /// For each sea cell, the nearest land cell over the grid (itself for a land cell): where InLand puts a point at sea.
    int[]? cellShore;
    float[]? cellLand;
    int gridN;
    float gridOrigin;

    int Cell(Vector2 p, out int cx, out int cz)
    {
        cx = Math.Clamp((int)((p.X - gridOrigin) / DesignCell), 0, gridN - 1);
        cz = Math.Clamp((int)((p.Y - gridOrigin) / DesignCell), 0, gridN - 1);
        return cz * gridN + cx;
    }

    /// In the sea (always false on a generated map).
    public bool InSea(Vector2 p) => cellSea != null && cellSea[Cell(p, out _, out _)];

    /// How much of the ground near p is land, 0-1, smooth across the shore (1 on a generated map).
    public float Landness(Vector2 p)
    {
        if (cellLand == null) return 1;
        float fx = (p.X - gridOrigin) / DesignCell - 0.5f, fz = (p.Y - gridOrigin) / DesignCell - 0.5f;
        int x0 = Math.Clamp((int)MathF.Floor(fx), 0, gridN - 2), z0 = Math.Clamp((int)MathF.Floor(fz), 0, gridN - 2);
        float tx = Math.Clamp(fx - x0, 0, 1), tz = Math.Clamp(fz - z0, 0, 1);
        float a = cellLand[z0 * gridN + x0], b = cellLand[z0 * gridN + x0 + 1], c = cellLand[(z0 + 1) * gridN + x0], d = cellLand[(z0 + 1) * gridN + x0 + 1];
        return MathHelper.Lerp(MathHelper.Lerp(a, b, tx), MathHelper.Lerp(c, d, tx), tz);
    }

    Province DesignProvinceAt(Vector2 p) => Provinces[cellProvince![Cell(p, out _, out _)]];

    /// The map of layout 5: everything from the design, then the same finishing as layout 4 (roads routed over the land).
    static WorldMap FromDesign(WorldDesign d, int realms)
    {
        var m = new WorldMap(DesignSeed) { ExtentUsed = d.Extent * DesignScale, Layout = DesignLayout };
        var rng = new Random(DesignSeed);
        var namer = new PlaceNamer(Names.Get(), DesignSeed);
        foreach (var s in d.Settlements) namer.Used.Add(s.Name); // invented province names never repeat a settlement's
        float span = m.ExtentUsed / LegacyExtent;
        int realmCount = realms > 0 ? Math.Min(realms, Factions.All.Count) : Factions.All.Count;
        // The design's realms are the data's realms, matched by culture (their ids need not agree).
        var faction = d.Realms.ToDictionary(r => r.Id, r => Math.Min(realmCount - 1, Math.Max(0, Factions.All.ToList().FindIndex(f => f.Culture == r.Culture))));

        // Provinces: the land ones, in the design's order.
        var landOf = new short[d.Provinces.Length];
        foreach (var p in d.Provinces)
        {
            if (p.Kind == "Sea") { landOf[p.Id] = -1; continue; }
            int f = faction[p.Realm];
            landOf[p.Id] = (short)m.Provinces.Count;
            m.Provinces.Add(new Province { Id = m.Provinces.Count, Name = namer.Next(Factions.CultureOf(f)), Kind = p.Kind, Center = Drawn(p.X, p.Y), Faction = f });
        }
        m.BuildGrid(d, landOf);
        m.LinkNeighbours();

        // Settlements as drawn, each in the province its place falls in.
        foreach (var s in d.Settlements)
        {
            var pos = Drawn(s.X, s.Y);
            int f = faction[s.Realm];
            m.Settlements.Add(new Settlement
            {
                // Its own people (WM5): the drawn subculture, whose villages raise the same root and whose men climb its own lines.
                Id = m.Settlements.Count, Province = m.DesignProvinceAt(pos).Id, Faction = f, Name = s.Name, Pos = pos,
                Culture = s.Subculture is { } sub && Factions.Culture(sub) is { Waiting: false } ? sub : Factions.CultureOf(f),
                Kind = s.Kind switch { "town" => SettlementKind.Town, "castle" => SettlementKind.Castle, _ => SettlementKind.Village },
                Rank = s.Kind == "castle" ? Math.Clamp(s.Rank == 0 ? 2 : s.Rank, 1, 3) : 0, // HD5: tower house, castle, great castle
            });
        }
        m.DesignRoads(d);
        m.DesignSeaLanes(d);
        foreach (var r in d.Rivers) m.Rivers.Add(m.DesignRiver(r, rng, span));
        foreach (var p in m.Provinces.Where(p => p.Kind is "Hills" or "Highlands"))
            for (int i = 0, n = p.Kind == "Highlands" ? 5 : 3; i < n; i++)
            {
                float a = rng.NextSingle() * MathF.Tau, dd = (10 + rng.NextSingle() * 30) * span;
                var c = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * dd;
                if (m.InSea(c)) continue;
                m.hills.Add((c, (16 + rng.NextSingle() * 14) * span, (p.Kind == "Highlands" ? 7 : 5) + rng.NextSingle() * 5));
            }
        foreach (var r in d.Ridges)
        {
            var pts = Meander(r.Points.Select(Drawn).ToList(), 0, rng);
            var passes = new List<Vector2>();
            for (int i = 0; i + 1 < pts.Count; i++)
                foreach (var (ra, rb) in m.Roads)
                    if (Cross(pts[i], pts[i + 1], ra, rb) is Vector2 x) passes.Add(x);
            m.Ridges.Add(new River(pts.ToArray(), r.Width * DesignScale, passes.ToArray(), 18 + rng.NextSingle() * 6));
        }
        m.ridgeIndex = null;
        foreach (var l in d.Lakes) m.Lakes.Add(new Lake(Drawn(l.X, l.Y), l.Radius * DesignScale, DesignSeed * 31 + l.Id));
        m.PlaceLakes(new Random(DesignSeed * 31 + 7), span, ponds: true);
        m.LayRoads(true);
        foreach (var s in d.Sites)
        {
            var pos = Drawn(s.X, s.Y);
            m.Sites.Add(new Site { Id = m.Sites.Count, Province = m.DesignProvinceAt(pos).Id, Kind = s.Kind, Pos = pos, Name = s.Name });
        }
        _ = m.Index; // build the look-up grid now, behind the loading screen: the generated maps' site placing builds it, this map's
                     // drawn sites do not, and the first campaign tick stalled 0.8 s building it (docs/archive/WORLDMAP.md WM4)
        return m;
    }

    /// The province grid: every cell's design province by the page's rule, the sea's cells given their nearest land
    /// province (so ProvinceAt always answers), and a land fraction softened over a few cells for the shore.
    void BuildGrid(WorldDesign d, short[] landOf)
    {
        gridOrigin = -GroundHalf;
        gridN = (int)MathF.Ceiling(2 * GroundHalf / DesignCell);
        int n = gridN;
        // The grid is the same for every campaign on this design: made once a process (the arrays are never written after).
        lock (gridCacheLock)
            if (gridCache is { } c && c.Design == d)
            {
                (cellProvince, cellSea, cellLand, cellShore, LandShare) = (c.Province, c.Sea, c.Land, c.Shore, c.Share);
                index = null;
                return;
            }
        var prov = new short[n * n];
        var sea = new bool[n * n];
        Parallel.For(0, n, z =>
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(gridOrigin + (x + 0.5f) * DesignCell, gridOrigin + (z + 0.5f) * DesignCell);
                // Beyond the design's extent is open sea.
                short l = MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y)) > d.Extent * DesignScale ? (short)-1 : landOf[d.ProvinceAt(p / DesignScale)];
                prov[z * n + x] = l;
                sea[z * n + x] = l < 0;
            }
        });
        // The sea's cells take the land province nearest them over the grid (a breadth-first fill from the shore).
        var queue = new Queue<int>();
        var shore = new int[n * n];
        for (int i = 0; i < prov.Length; i++) if (prov[i] >= 0) { queue.Enqueue(i); shore[i] = i; }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % n, z = i / n;
            void Visit(int j) { if (prov[j] < 0) { prov[j] = prov[i]; shore[j] = shore[i]; queue.Enqueue(j); } }
            if (x > 0) Visit(i - 1);
            if (x < n - 1) Visit(i + 1);
            if (z > 0) Visit(i - n);
            if (z < n - 1) Visit(i + n);
        }
        // Land fraction, box-blurred twice over three cells each way: a shore some 12 units wide.
        var land = new float[n * n];
        for (int i = 0; i < land.Length; i++) land[i] = sea[i] ? 0 : 1;
        for (int pass = 0; pass < 2; pass++) land = Blur(land, n, 3);
        cellProvince = prov;
        cellSea = sea;
        cellLand = land;
        cellShore = shore;
        LandShare = MathF.Max(0.05f, (float)(sea.Where((s, i) => !s && InExtent(i)).Count() / (double)Enumerable.Range(0, n * n).Count(InExtent)));
        lock (gridCacheLock) gridCache = (d, prov, sea, land, shore, LandShare);
        index = null;
    }

    static readonly object gridCacheLock = new();
    static (WorldDesign Design, short[] Province, bool[] Sea, float[] Land, int[] Shore, float Share)? gridCache;

    /// The nearest point on land to p (p itself on land): the middle of the nearest land cell, a step further in so a
    /// party stopped there stands clear of the water's edge.
    Vector2 OntoLand(Vector2 p)
    {
        int i = Cell(p, out _, out _);
        if (!cellSea![i]) return p;
        int j = cellShore![i];
        var land = new Vector2(gridOrigin + (j % gridN + 0.5f) * DesignCell, gridOrigin + (j / gridN + 0.5f) * DesignCell);
        var inward = land - p;
        return inward.LengthSquared() > 1e-4f ? land + Vector2.Normalize(inward) * DesignCell : land;
    }

    bool InExtent(int i)
    {
        float x = gridOrigin + (i % gridN + 0.5f) * DesignCell, z = gridOrigin + (i / gridN + 0.5f) * DesignCell;
        return MathF.Abs(x) <= ExtentUsed && MathF.Abs(z) <= ExtentUsed;
    }

    static float[] Blur(float[] src, int n, int r)
    {
        var tmp = new float[n * n];
        var dst = new float[n * n];
        for (int z = 0; z < n; z++)
        {
            float sum = 0;
            for (int x = -r; x <= r; x++) sum += src[z * n + Math.Clamp(x, 0, n - 1)];
            for (int x = 0; x < n; x++)
            {
                tmp[z * n + x] = sum / (2 * r + 1);
                sum += src[z * n + Math.Min(n - 1, x + r + 1)] - src[z * n + Math.Max(0, x - r)];
            }
        }
        for (int x = 0; x < n; x++)
        {
            float sum = 0;
            for (int z = -r; z <= r; z++) sum += tmp[Math.Clamp(z, 0, n - 1) * n + x];
            for (int z = 0; z < n; z++)
            {
                dst[z * n + x] = sum / (2 * r + 1);
                sum += tmp[Math.Min(n - 1, z + r + 1) * n + x] - tmp[Math.Max(0, z - r) * n + x];
            }
        }
        return dst;
    }

    /// Provinces are neighbours where their land meets on the grid (a few cells of shared border, not a corner).
    void LinkNeighbours()
    {
        var shared = new Dictionary<(int, int), int>();
        int n = gridN;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                if (cellSea![i]) continue;
                foreach (int j in new[] { x < n - 1 ? i + 1 : -1, z < n - 1 ? i + n : -1 })
                {
                    if (j < 0 || cellSea[j] || cellProvince![j] == cellProvince[i]) continue;
                    var key = (Math.Min(cellProvince[i], cellProvince[j]), Math.Max(cellProvince[i], cellProvince[j]));
                    shared[key] = shared.GetValueOrDefault(key) + 1;
                }
            }
        foreach (var ((a, b), cells) in shared.OrderBy(k => k.Key))
            if (cells >= 3) { Provinces[a].Neighbors.Add(b); Provinces[b].Neighbors.Add(a); }
    }

    /// Does the straight line from a to b keep to the land? (Always on a generated map.)
    const float SeaCell = 6;
    int seaN;
    float seaOrigin;
    bool[]? seaOpen;
    float[] seaCostOf = Array.Empty<float>();
    int[] seaPrev = Array.Empty<int>(), seaSeen = Array.Empty<int>();
    int seaStamp;
    readonly object seaLock = new();

    /// A way round the sea for the AI's parties (docs/archive/WORLDMAP.md WM4): A* on a coarse land grid (6 units, land or not, every
    /// step the same), then pulled into straight legs wherever the line stays on land. The player's full planner prices roads,
    /// rivers and woods on a 4-unit grid and cleared two 170 000-cell arrays a plan; with every chasing lord re-planning it
    /// was most of the drawn map's campaign tick. The last point is `to`; a goal no land way reaches gives just `to`.
    public List<Vector2> PlanAroundSea(Vector2 from, Vector2 to) => PlanOver(ref seaOpen, false, from, to);

    /// The same search over the other half of the grid: a ship's way through open water, no longer than `reach` (WorldSea.cs).
    List<Vector2> PlanBySea(Vector2 from, Vector2 to, float reach) => PlanOver(ref waterOpen, true, from, to, reach / SeaCell);
    bool[]? waterOpen;

    List<Vector2> PlanOver(ref bool[]? grid, bool water, Vector2 from, Vector2 to, float limit = float.MaxValue)
    {
        lock (seaLock)
        {
            if (seaCostOf.Length == 0)
            {
                seaOrigin = -ExtentUsed;
                seaN = (int)MathF.Ceiling(2 * ExtentUsed / SeaCell) + 1;
                seaCostOf = new float[seaN * seaN]; seaPrev = new int[seaN * seaN]; seaSeen = new int[seaN * seaN];
            }
            if (grid == null)
            {
                grid = new bool[seaN * seaN];
                for (int z = 0; z < seaN; z++)
                    for (int x = 0; x < seaN; x++)
                    {
                        var p = new Vector2(seaOrigin + x * SeaCell, seaOrigin + z * SeaCell);
                        grid[z * seaN + x] = water ? Landness(p) < Afloat : !InSea(p);
                    }
            }
            var seaOpen = grid;
            int n = seaN;
            int Of(Vector2 p) => Math.Clamp((int)MathF.Round((p.Y - seaOrigin) / SeaCell), 0, n - 1) * n + Math.Clamp((int)MathF.Round((p.X - seaOrigin) / SeaCell), 0, n - 1);
            Vector2 At(int c) => new(seaOrigin + c % n * SeaCell, seaOrigin + c / n * SeaCell);
            int start = Of(from), end = Of(to);
            if (++seaStamp == int.MaxValue) { Array.Clear(seaSeen); seaStamp = 1; }
            var open = new PriorityQueue<int, float>();
            seaSeen[start] = seaStamp; seaCostOf[start] = 0; seaPrev[start] = -1;
            open.Enqueue(start, 0);
            var goal = At(end);
            bool found = false;
            while (open.TryDequeue(out int c, out _))
            {
                if (c == end) { found = true; break; }
                int cx = c % n, cz = c / n;
                float cc = seaCostOf[c];
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = cx + dx, z = cz + dz, id = z * n + x;
                        if ((dx | dz) == 0 || x < 0 || z < 0 || x >= n || z >= n || !seaOpen[id] && id != end) continue;
                        if (dx != 0 && dz != 0 && !seaOpen[cz * n + x] && !seaOpen[z * n + cx]) continue; // no slipping between two sea cells
                        float nc = cc + (dx != 0 && dz != 0 ? 1.4142f : 1);
                        if (seaSeen[id] == seaStamp && nc >= seaCostOf[id]) continue;
                        float est = nc + Vector2.Distance(At(id), goal) / SeaCell;
                        if (est > limit) continue;
                        seaSeen[id] = seaStamp; seaCostOf[id] = nc; seaPrev[id] = c;
                        open.Enqueue(id, est);
                    }
            }
            if (!found) return new List<Vector2> { to };
            var cells = new List<Vector2>();
            for (int i = end; i >= 0 && i != start; i = seaPrev[i]) cells.Add(At(i));
            cells.Add(from);
            cells.Reverse();
            cells[^1] = to;
            // Straight legs: from each corner on to the furthest later point the line reaches over land.
            var way = new List<Vector2>();
            int at = 0;
            while (at < cells.Count - 1)
            {
                int next = at + 1;
                for (int j = at + 2, misses = 0; j < cells.Count && misses < 16; j++)
                    if (water ? OverWater(cells[at], cells[j]) : OverLand(cells[at], cells[j])) { next = j; misses = 0; } else misses++;
                way.Add(cells[next]);
                at = next;
            }
            return way;
        }
    }

    public bool OverLand(Vector2 a, Vector2 b)
    {
        if (cellSea == null) return true;
        int steps = (int)MathF.Ceiling(Vector2.Distance(a, b) / DesignCell);
        for (int i = 1; i < steps; i++) if (InSea(Vector2.Lerp(a, b, i / (float)steps))) return false;
        return true;
    }

    /// The drawn roads, then the links the drawing left out: every village to a town or castle near it, and every town and
    /// castle into one network wherever the land joins (the shortest land links first). Straight here; LayRoads routes them.
    void DesignRoads(WorldDesign d)
    {
        foreach (var r in d.Roads)
            for (int i = 0; i + 1 < r.Points.Length; i++) Roads.Add((Drawn(r.Points[i]), Drawn(r.Points[i + 1])));
        // Which settlement a road end is (the drawing puts ends on settlements); union-find over the settlements.
        var parent = Enumerable.Range(0, Settlements.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        void Join(int a, int b) => parent[Find(a)] = Find(b);
        int At(Vector2 p) => Settlements.FindIndex(s => Vector2.DistanceSquared(s.Pos, p) < 1);
        foreach (var (a, b) in Roads)
            if (At(a) is int i and >= 0 && At(b) is int j and >= 0) Join(i, j);
        var seats = Settlements.Where(s => s.Kind != SettlementKind.Village).ToList();
        bool Ends(Settlement s) => Roads.Any(r => Vector2.DistanceSquared(r.A, s.Pos) < 1 || Vector2.DistanceSquared(r.B, s.Pos) < 1);
        foreach (var v in Settlements.Where(s => s.Kind == SettlementKind.Village && !Ends(s)))
        {
            var seat = seats.Where(t => OverLand(v.Pos, t.Pos)).OrderBy(t => (t.Province == v.Province ? 0 : 40) + Vector2.Distance(t.Pos, v.Pos)).FirstOrDefault();
            if (seat == null) continue;
            Roads.Add((seat.Pos, v.Pos));
            Join(v.Id, seat.Id);
        }
        var pairs = new List<(float D, Settlement A, Settlement B)>();
        for (int i = 0; i < seats.Count; i++)
            for (int j = i + 1; j < seats.Count; j++)
            {
                float dd = Vector2.Distance(seats[i].Pos, seats[j].Pos);
                if (dd < 160 * DesignScale) pairs.Add((dd, seats[i], seats[j]));
            }
        foreach (var (_, a, b) in pairs.OrderBy(p => p.D))
            if (Find(a.Id) != Find(b.Id) && OverLand(a.Pos, b.Pos)) { Roads.Add((a.Pos, b.Pos)); Join(a.Id, b.Id); }
        // A seat whose every straight line crosses a bay: join it through the villages, hop by hop along the land.
        if (seats.Select(s => Find(s.Id)).Distinct().Count() > 1)
        {
            var hops = new List<(float D, Settlement A, Settlement B)>();
            for (int i = 0; i < Settlements.Count; i++)
                for (int j = i + 1; j < Settlements.Count; j++)
                {
                    float dd = Vector2.Distance(Settlements[i].Pos, Settlements[j].Pos);
                    if (dd < 90 * DesignScale) hops.Add((dd, Settlements[i], Settlements[j]));
                }
            foreach (var (_, a, b) in hops.OrderBy(p => p.D))
                if (Find(a.Id) != Find(b.Id) && OverLand(a.Pos, b.Pos)) { Roads.Add((a.Pos, b.Pos)); Join(a.Id, b.Id); }
        }
    }

    /// A drawn river as the map carries it: its few drawn points wander between, kept clear of settlements.
    River DesignRiver(WorldDesign.Line r, Random rng, float span)
    {
        var pts = Meander(r.Points.Select(Drawn).ToList(), 6 * MathF.Sqrt(span), rng);
        for (int i = 0; i < pts.Count; i++)
            foreach (var st in Settlements)
            {
                var away = pts[i] - st.Pos;
                float dist = away.Length();
                if (dist < 9 * span) pts[i] = st.Pos + (dist > 0.01f ? away / dist : Vector2.UnitX) * 9 * span;
            }
        var path = pts.ToArray();
        var bridges = new List<Vector2>();
        for (int i = 0; i + 1 < path.Length; i++)
            foreach (var (a, b) in Roads)
                if (Cross(path[i], path[i + 1], a, b) is Vector2 x) bridges.Add(x);
        return new River(path, r.Width, bridges.ToArray(), 1.5f) { Widen = 2 };
    }

    /// A drawn line's points every 4 units, swinging up to `swing` to either side between its drawn points (not at them).
    static List<Vector2> Meander(List<Vector2> drawn, float swing, Random rng)
    {
        var pts = new List<Vector2>();
        float ph = rng.NextSingle() * MathF.Tau;
        for (int k = 0; k + 1 < drawn.Count; k++)
        {
            Vector2 a = drawn[k], b = drawn[k + 1];
            float len = Vector2.Distance(a, b);
            var side = len > 0 ? new Vector2(-(b - a).Y, (b - a).X) / len : Vector2.Zero;
            int steps = Math.Max(1, (int)(len / 4));
            for (int i = k == 0 ? 0 : 1; i <= steps; i++)
            {
                float t = i / (float)steps, s = t * len;
                float w = MathF.Sin(MathF.PI * t) * (MathF.Sin(s * 0.05f + ph) + 0.4f * MathF.Sin(s * 0.13f + ph * 2)) * swing;
                pts.Add(Vector2.Lerp(a, b, t) + side * w);
            }
        }
        return pts;
    }
}
