using Microsoft.Xna.Framework;

namespace Ironfield;

/// WorldMap: the way a party takes across the land. A* on a 4 m grid weighted by how slowly the ground is crossed
/// (roads quick, woods and ridges slow), with rivers waded only where no bridge or ford is worth the walk and lakes
/// gone round; the cells' path is then pulled straight wherever a straight line costs no more.
public sealed partial class WorldMap
{
    const float TravelCell = 4;
    float[]? travelCost;
    int travelN;
    float travelOrigin;

    /// What a metre of ground at p costs to cross (1 = open meadow), for planning only: wading a river away from its
    /// bridges and fords is weighed far above its real pace so a party goes round to them, and a lake is all but barred.
    float TravelCostAt(Vector2 p, float roadBonus)
    {
        if (InSea(p) || Landness(p) < 0.35f) return float.PositiveInfinity; // the sea is not crossed (docs/archive/WORLDMAP.md WM2)
        if (OnCliff(p, 3)) return float.PositiveInfinity; // nor a cliff's face (WS3): a band ~6 wide, so no diagonal step slips through
        if (InLake(p)) return 40;
        if (RoadDist(p) < 2.5f) return 1 / roadBonus;
        foreach (var r in Rivers)
            if (r.Within(p, r.Width / 2 + 1.5f)) return r.AtFord(p) ? 2.5f : 18;
        return 1 / SpeedFactor(p, roadBonus);
    }

    float CellCost(int x, int z, float roadBonus)
    {
        ref float c = ref travelCost![z * travelN + x];
        if (float.IsNaN(c)) c = TravelCostAt(new Vector2(travelOrigin + x * TravelCell, travelOrigin + z * TravelCell), roadBonus);
        return c;
    }

    (int X, int Z) TravelCellOf(Vector2 p) =>
        (Math.Clamp((int)MathF.Round((p.X - travelOrigin) / TravelCell), 0, travelN - 1), Math.Clamp((int)MathF.Round((p.Y - travelOrigin) / TravelCell), 0, travelN - 1));

    /// The way from `from` to `to` over the land in play, as points to walk to in turn (the last is `to`).
    public List<Vector2> PlanRoute(Vector2 from, Vector2 to, float roadBonus)
    {
        to = InLand(to);
        if (travelCost == null)
        {
            travelOrigin = -ExtentUsed;
            travelN = (int)MathF.Ceiling(2 * ExtentUsed / TravelCell) + 1;
            travelCost = new float[travelN * travelN];
            Array.Fill(travelCost, float.NaN);
        }
        var direct = new List<Vector2> { to };
        if (Vector2.Distance(from, to) < TravelCell * 2) return direct;
        var (sx, sz) = TravelCellOf(from);
        var (ex, ez) = TravelCellOf(to);
        int n = travelN;
        var cost = new float[n * n];
        var prev = new int[n * n];
        Array.Fill(cost, float.MaxValue);
        Array.Fill(prev, -1);
        var open = new PriorityQueue<int, float>();
        int start = sz * n + sx, end = ez * n + ex;
        cost[start] = 0;
        open.Enqueue(start, 0);
        // The estimate prices the rest of the way as open ground: a little over a road's cost, so the search heads for the
        // goal rather than flooding the map (a way along a road may come out a touch longer than the very best).
        const float best = 1;
        var goal = new Vector2(ex, ez);
        while (open.TryDequeue(out int c, out _))
        {
            if (c == end) break;
            int cx = c % n, cz = c / n;
            float cc = cost[c], here = CellCost(cx, cz, roadBonus);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= n || z >= n) continue;
                    float len = (dx != 0 && dz != 0 ? 1.4142f : 1) * TravelCell, there = CellCost(x, z, roadBonus);
                    if (float.IsPositiveInfinity(there)) continue;
                    float nc = cc + len * 0.5f * (here + there);
                    int id = z * n + x;
                    if (nc >= cost[id]) continue;
                    cost[id] = nc;
                    prev[id] = c;
                    open.Enqueue(id, nc + Vector2.Distance(new Vector2(x, z), goal) * TravelCell * best);
                }
        }
        if (prev[end] < 0) return direct;
        var cells = new List<Vector2>();
        for (int i = end; i >= 0; i = prev[i]) cells.Add(new Vector2(travelOrigin + i % n * TravelCell, travelOrigin + i / n * TravelCell));
        cells.Reverse();
        cells[0] = from;
        cells[^1] = to;
        return Straighten(cells, roadBonus);
    }

    /// What walking the straight line a-b costs (sampled every 2 m).
    float LineCost(Vector2 a, Vector2 b, float roadBonus)
    {
        float len = Vector2.Distance(a, b);
        int k = Math.Max(1, (int)MathF.Ceiling(len / 2));
        float sum = 0;
        for (int i = 0; i < k; i++)
        {
            var (x, z) = TravelCellOf(Vector2.Lerp(a, b, (i + 0.5f) / k));
            sum += CellCost(x, z, roadBonus);
        }
        return sum / k * len;
    }

    /// Pull the cells' zigzag straight: from each corner, on to the furthest later point the straight line reaches for
    /// no more than the path it replaces (so it never cuts a road's bend into the fields or wades round a bridge).
    List<Vector2> Straighten(List<Vector2> pts, float roadBonus)
    {
        var along = new float[pts.Count]; // the path's own cost up to each point
        for (int i = 1; i < pts.Count; i++) along[i] = along[i - 1] + LineCost(pts[i - 1], pts[i], roadBonus);
        var o = new List<Vector2>();
        int at = 0;
        while (at < pts.Count - 1)
        {
            int next = at + 1;
            for (int j = at + 2, misses = 0; j < pts.Count && misses < 12; j++)
            {
                if (LineCost(pts[at], pts[j], roadBonus) <= (along[j] - along[at]) * 1.01f + 0.5f) { next = j; misses = 0; }
                else misses++;
            }
            o.Add(pts[next]);
            at = next;
        }
        return o;
    }
}
