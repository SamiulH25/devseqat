using Microsoft.Xna.Framework;

namespace Ironfield;

/// WorldMap: roads that follow the land and still water (docs/WORLD_LIFE.md WL1-WL2). On a layout-4 map each road is
/// routed over the ground (round hills and lakes, along the valleys) through the bridges and passes its straight line had,
/// so rivers and ridges keep the crossings they were made with; lakes and village ponds lie in basins. Older layouts keep
/// their straight roads and have no lakes: their maps never change. `RoadLines` is what the map draws either way.
public sealed partial class WorldMap
{
    /// Lakes and ponds (layout 4); none on older maps.
    public readonly List<Lake> Lakes = new();
    /// The roads as drawn: one line per road, routed or straight (`Roads` holds their segments for distances).
    public readonly List<RoadLine> RoadLines = new();
    /// Where roads cross rivers, as drawn (the crossing itself is a ford in the heights).
    public readonly List<MapBridge> Bridges = new();

    /// Inside a lake or pond (grown by `pad`).
    public bool InLake(Vector2 p, float pad = 0)
    {
        foreach (var l in Lakes) if (l.Inside(p, pad)) return true;
        return false;
    }

    /// A few lakes in open low country and a pond by some villages, clear of settlements, rivers, ridges and the rim.
    void PlaceLakes(Random rng, float span)
    {
        bool Clear(Vector2 p, float r, float fromTown) =>
            MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y)) < ExtentUsed - 20 - r
            && Settlements.All(s => Vector2.Distance(s.Pos, p) > fromTown + r)
            && RiverDist(p) > r * 1.3f + 8 && RidgeDist(p) > r + 30 * span
            && Lakes.All(l => Vector2.Distance(l.Center, p) > l.Radius + r + 12);
        int lakes = Math.Max(2, Provinces.Count / 9);
        for (int i = 0, tries = 0; i < lakes && tries < 400; tries++)
        {
            var prov = Provinces[rng.Next(Provinces.Count)];
            if (prov.Kind is "Badlands" or "Steppe" || rng.Next(3) == 0 && prov.Kind == "Hills") continue;
            float a = rng.NextSingle() * MathF.Tau, d = (18 + rng.NextSingle() * 22) * span, r = (prov.Kind == "Marsh" ? 4 : 6 + rng.NextSingle() * 7) * MathF.Sqrt(span);
            var p = prov.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (!Clear(p, r, 14 * span)) continue;
            Lakes.Add(new Lake(p, r, Seed * 31 + i));
            i++;
        }
        foreach (var v in Settlements.Where(s => s.Kind == SettlementKind.Village))
        {
            if (rng.Next(3) != 0) continue; // a pond by one village in three
            float a = rng.NextSingle() * MathF.Tau, r = 2.2f + rng.NextSingle() * 1.6f;
            var p = v.Pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (6.5f + rng.NextSingle() * 3) * MathF.Sqrt(span);
            if (Clear(p, r, 5)) Lakes.Add(new Lake(p, r, Seed * 31 + 1000 + v.Id, 0.9f));
        }
        index = null; // heights changed under the look-up grid's feet
    }

    /// The drawn roads, and on layout 4 the routed ones (which then replace the straight segments).
    void LayRoads(bool route)
    {
        var villages = Settlements.Where(s => s.Kind == SettlementKind.Village).Select(s => s.Pos).ToHashSet();
        var lines = new List<RoadLine>();
        var router = route ? new Router(this) : null;
        foreach (var (a, b) in Roads)
        {
            bool village = villages.Contains(a) || villages.Contains(b);
            var pts = router?.Route(a, b) ?? new[] { a, b };
            lines.Add(new RoadLine(pts, village ? 1.7f : 2.6f, village));
        }
        RoadLines.AddRange(lines);
        if (route)
        {
            // Distances (asked every tick) use a coarser copy of each line: within 1.2 m of the drawn road, far fewer segments.
            Roads.Clear();
            foreach (var l in lines)
            {
                var coarse = Router.Simplify(l.Points.ToList(), 1.2f);
                for (int i = 0; i + 1 < coarse.Count; i++) Roads.Add((coarse[i], coarse[i + 1]));
            }
            index = null;
        }
        // Bridges where the drawn roads meet the rivers.
        foreach (var l in RoadLines)
            for (int i = 0; i + 1 < l.Points.Length; i++)
                foreach (var r in Rivers)
                    for (int k = 0; k + 1 < r.Path.Length; k++)
                        if (Cross(r.Path[k], r.Path[k + 1], l.Points[i], l.Points[i + 1]) is Vector2 x)
                            Bridges.Add(new MapBridge(x, Vector2.Normalize(l.Points[i + 1] - l.Points[i]), r.Width / 2 + 2.2f, !l.Village));
    }

    /// Routes a road over the land: A* on a 4 m grid whose step cost grows with slope; lakes and rivers (away from the
    /// crossings it must keep) are barred. Heights are cached per cell across all roads.
    sealed class Router
    {
        const float C = 4;
        readonly WorldMap m;
        readonly int n;
        readonly float origin;
        readonly float[] height;
        readonly sbyte[] barred; // 0 unknown, 1 open, -1 barred
        readonly float riverHalf;

        public Router(WorldMap map)
        {
            m = map;
            riverHalf = map.Rivers.Select(r => r.Width).DefaultIfEmpty(0).Max() / 2;
            origin = -(map.ExtentUsed + 12);
            n = (int)MathF.Ceiling(-2 * origin / C) + 1;
            height = new float[n * n];
            Array.Fill(height, float.NaN);
            barred = new sbyte[n * n];
        }

        Vector2 At(int x, int z) => new(origin + x * C, origin + z * C);
        (int, int) Cell(Vector2 p) => (Math.Clamp((int)MathF.Round((p.X - origin) / C), 0, n - 1), Math.Clamp((int)MathF.Round((p.Y - origin) / C), 0, n - 1));

        float H(int x, int z)
        {
            ref float h = ref height[z * n + x];
            if (float.IsNaN(h)) { var p = At(x, z); h = m.LandHeight(p.X, p.Y); }
            return h;
        }

        bool Barred(int x, int z)
        {
            ref sbyte b = ref barred[z * n + x];
            if (b == 0)
            {
                var p = At(x, z);
                b = (sbyte)(m.InLake(p, 2.5f) || m.RiverDist(p) < riverHalf + 1.5f ? -1 : 1);
            }
            return b < 0;
        }

        /// The road from a to b through the crossings its straight line has: each river crossing and ridge pass is
        /// approached straight from a little way off on either side, and the stretches between are routed.
        public Vector2[] Route(Vector2 a, Vector2 b)
        {
            var dir = Vector2.Normalize(b - a);
            var stops = new List<(float T, Vector2 P, float Reach)>();
            void Crossings(List<River> lines, Func<River, float> reach)
            {
                foreach (var r in lines)
                    for (int k = 0; k + 1 < r.Path.Length; k++)
                        if (Cross(r.Path[k], r.Path[k + 1], a, b) is Vector2 x) stops.Add((Vector2.Dot(x - a, dir), x, reach(r)));
            }
            Crossings(m.Rivers, r => r.Width / 2 + 4);
            Crossings(m.Ridges, _ => 6);
            var pts = new List<Vector2> { a };
            Vector2 from = a;
            foreach (var (_, x, reach) in stops.OrderBy(s => s.T))
            {
                var near = x - dir * reach;
                Leg(from, near, pts);
                pts.Add(x);
                from = x + dir * reach;
                pts.Add(from);
            }
            Leg(from, b, pts);
            return pts.ToArray();
        }

        /// Append the routed stretch from `from` (already in `pts`) to `to`.
        void Leg(Vector2 from, Vector2 to, List<Vector2> pts)
        {
            float len = Vector2.Distance(from, to);
            if (len < C * 3) { pts.Add(to); return; }
            var cells = Search(from, to, len);
            if (cells == null) { pts.Add(to); return; }
            var raw = new List<Vector2> { from };
            for (int i = 1; i + 1 < cells.Count; i++) raw.Add(At(cells[i].X, cells[i].Z));
            raw.Add(to);
            var smooth = Simplify(Chaikin(Simplify(raw, 1.6f), 2), 0.45f); // few segments: RoadDist is asked every tick
            for (int i = 1; i < smooth.Count; i++) pts.Add(smooth[i]);
        }

        List<(int X, int Z)>? Search(Vector2 from, Vector2 to, float len)
        {
            var (sx, sz) = Cell(from);
            var (ex, ez) = Cell(to);
            float pad = MathF.Max(16, len * 0.22f);
            var lo = Cell(Vector2.Min(from, to) - new Vector2(pad));
            var hi = Cell(Vector2.Max(from, to) + new Vector2(pad));
            int w = hi.Item1 - lo.Item1 + 1, d = hi.Item2 - lo.Item2 + 1;
            var cost = new float[w * d];
            var prev = new int[w * d];
            Array.Fill(cost, float.MaxValue);
            Array.Fill(prev, -1);
            int Id(int x, int z) => (z - lo.Item2) * w + (x - lo.Item1);
            var open = new PriorityQueue<(int X, int Z), float>();
            cost[Id(sx, sz)] = 0;
            open.Enqueue((sx, sz), 0);
            int budget = w * d * 2;
            while (open.TryDequeue(out var c, out _) && budget-- > 0)
            {
                if (c.X == ex && c.Z == ez) break;
                float cc = cost[Id(c.X, c.Z)], hc = H(c.X, c.Z);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = c.X + dx, z = c.Z + dz;
                        if (x < lo.Item1 || z < lo.Item2 || x > hi.Item1 || z > hi.Item2) continue;
                        bool end = x == ex && z == ez;
                        if (!end && Barred(x, z) && !(Vector2.Distance(At(x, z), from) < 6)) continue; // leave a crossing's approach freely
                        float step = dx != 0 && dz != 0 ? C * 1.4142f : C, slope = MathF.Abs(H(x, z) - hc) / step;
                        float nc = cc + step * (1 + 10 * slope + 90 * slope * slope);
                        int id = Id(x, z);
                        if (nc >= cost[id]) continue;
                        cost[id] = nc;
                        prev[id] = Id(c.X, c.Z);
                        open.Enqueue((x, z), nc + Vector2.Distance(At(x, z), to));
                    }
            }
            int at = Id(ex, ez);
            if (prev[at] < 0) return null;
            var path = new List<(int, int)>();
            for (int i = at; i >= 0; i = prev[i]) path.Add((i % w + lo.Item1, i / w + lo.Item2));
            path.Reverse();
            return path;
        }

        /// Ramer-Douglas-Peucker: drop points within `eps` of the line through their neighbours (ends kept).
        public static List<Vector2> Simplify(List<Vector2> p, float eps)
        {
            if (p.Count < 3) return p;
            var keep = new bool[p.Count];
            keep[0] = keep[^1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, p.Count - 1));
            while (stack.Count > 0)
            {
                var (i, j) = stack.Pop();
                float best = 0; int at = -1;
                var ab = p[j] - p[i];
                float l2 = MathF.Max(ab.LengthSquared(), 1e-6f);
                for (int k = i + 1; k < j; k++)
                {
                    float t = Math.Clamp(Vector2.Dot(p[k] - p[i], ab) / l2, 0, 1), dd = Vector2.Distance(p[k], p[i] + ab * t);
                    if (dd > best) { best = dd; at = k; }
                }
                if (at >= 0 && best > eps) { keep[at] = true; stack.Push((i, at)); stack.Push((at, j)); }
            }
            return p.Where((_, k) => keep[k]).ToList();
        }

        /// Corner cutting, `rounds` times; the two ends stay where they are.
        static List<Vector2> Chaikin(List<Vector2> p, int rounds)
        {
            for (int r = 0; r < rounds && p.Count > 2; r++)
            {
                var q = new List<Vector2> { p[0] };
                for (int i = 0; i + 1 < p.Count; i++)
                {
                    q.Add(Vector2.Lerp(p[i], p[i + 1], 0.25f));
                    q.Add(Vector2.Lerp(p[i], p[i + 1], 0.75f));
                }
                q.Add(p[^1]);
                p = q;
            }
            return p;
        }
    }
}
