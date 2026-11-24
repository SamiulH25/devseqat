using Microsoft.Xna.Framework;

namespace Ironfield;

/// A bucket grid over the campaign map (docs/archive/WORLD.md W0), so "which province is this", "how far to a road" and "how far to a
/// river" look at a cell's few candidates and not at every province, road and river bend. Each cell keeps every item that could
/// give the answer for some point inside it, so the results are exactly the full scan's (old saves rebuild their map from the
/// seed, and sites placed with these queries must not move). Points off the grid fall back to the full scan.
public sealed class MapIndex
{
    public const float Cell = 16f;
    readonly float origin;
    readonly int n;
    readonly int[][] provinces, roads;
    readonly float[] roadNear; // each cell centre's distance to its nearest road: no point in the cell is nearer than this less h
    readonly (int River, int Seg)[][] rivers;
    readonly List<Province> provinceList;
    readonly List<(Vector2 A, Vector2 B)> roadList;
    readonly List<River> riverList;

    /// River.Skip of the rivers indexed (all the same): how far off a bend's ends River.Dist still measures it.
    readonly float skip;

    public MapIndex(List<Province> provinces, List<(Vector2 A, Vector2 B)> roads, List<River> rivers, float half)
    {
        skip = rivers.Count > 0 ? rivers[0].Skip : 40;
        provinceList = provinces; roadList = roads; riverList = rivers;
        origin = -half;
        n = Math.Max(1, (int)MathF.Ceiling(2 * half / Cell));
        this.provinces = new int[n * n][];
        this.roads = new int[n * n][];
        roadNear = new float[n * n];
        this.rivers = new (int, int)[n * n][];
        float h = Cell * 0.70711f + 0.01f; // half the cell's diagonal, and a hair for rounding
        var segs = new List<(int River, int Seg, Vector2 A, Vector2 B)>();
        for (int r = 0; r < rivers.Count; r++)
            for (int i = 0; i + 1 < rivers[r].Path.Length; i++) segs.Add((r, i, rivers[r].Path[i], rivers[r].Path[i + 1]));
        // Cells are worked out a block of Block x Block at a time, in parallel (each cell's lists are its own, so the result is
        // the same in any order). A block first keeps the roads that could matter to any of its cells: a road nearest some
        // cell centre, or within 2h of that nearest, is within (block's nearest + 2 x the block's half diagonal + 2h) of the
        // block's middle, so the cells' lists are exactly the full scan's (WS2: the 4x map has ~180k cells and many roads).
        int blocks = (n + Block - 1) / Block;
        float bh = Block * Cell * 0.70711f + 0.01f;
        Parallel.For(0, blocks * blocks, b =>
        {
            int bx = b % blocks, by = b / blocks;
            var bc = new Vector2(origin + (bx * Block + Block / 2f) * Cell, origin + (by * Block + Block / 2f) * Cell);
            var near = new List<int>();
            if (roads.Count > 0)
            {
                var bd = new float[roads.Count];
                float best = float.MaxValue;
                for (int i = 0; i < bd.Length; i++) best = MathF.Min(best, bd[i] = SegDist(bc, roads[i].A, roads[i].B));
                for (int i = 0; i < bd.Length; i++) if (bd[i] <= best + 2 * bh + 2 * h) near.Add(i);
            }
            var pd = new float[provinces.Count];
            var rd = new float[near.Count];
            var keep = new List<int>();
            var cand = new List<(float D, int Order)>();
            for (int cy = by * Block; cy < Math.Min(n, by * Block + Block); cy++)
                for (int cx = bx * Block; cx < Math.Min(n, bx * Block + Block); cx++)
                    Fill(cy * n + cx, cx, cy, h, provinces, roads, near, segs, pd, rd, keep, cand);
        });
    }

    /// Cells a side of the blocks the index is built in.
    const int Block = 8;

    /// One cell's lists: the provinces, roads (from the block's `near` ones, in road order) and river bends that can answer
    /// for some point in it.
    void Fill(int k, int cx, int cy, float h, List<Province> provinces, List<(Vector2 A, Vector2 B)> roads, List<int> near,
        List<(int River, int Seg, Vector2 A, Vector2 B)> segs, float[] pd, float[] rd, List<int> keep, List<(float D, int Order)> cand)
    {
        var lo = new Vector2(origin + cx * Cell, origin + cy * Cell);
        var cc = lo + new Vector2(Cell / 2);
        // The nearest centre to a point in the cell is within (nearest to the cell's centre) + 2h of the cell's centre.
        float nearest = provinces.Count == 0 ? 0 : float.MaxValue;
        for (int i = 0; i < pd.Length; i++) nearest = MathF.Min(nearest, pd[i] = Vector2.Distance(provinces[i].Center, cc));
        keep.Clear();
        for (int i = 0; i < pd.Length; i++) if (pd[i] <= nearest + 2 * h) keep.Add(i);
        this.provinces[k] = keep.ToArray();
        float road = roads.Count == 0 ? 0 : float.MaxValue;
        for (int j = 0; j < near.Count; j++) road = MathF.Min(road, rd[j] = SegDist(cc, roads[near[j]].A, roads[near[j]].B));
        keep.Clear();
        for (int j = 0; j < near.Count; j++) if (rd[j] <= road + 2 * h) keep.Add(near[j]);
        this.roads[k] = keep.ToArray();
        roadNear[k] = road;
        // River.Dist skips a bend whose ends are more than 40 away on both axes; keep every bend it might not skip
        // for some point in the cell, nearest first (ties in river order), so the look-up can stop once no bend can come closer.
        // A bend no point of the cell skips (an end within 40 of the whole cell on an axis) bounds the answer: bends further
        // than that bound plus 2h can never be reached before the look-up stops, so they are left out (same answers, far
        // smaller lists on a big map).
        cand.Clear();
        float bound = float.MaxValue;
        for (int i = 0; i < segs.Count; i++)
            if (Within(segs[i].A, lo, skip) || Within(segs[i].B, lo, skip))
            {
                float d = SegDist(cc, segs[i].A, segs[i].B);
                cand.Add((d, i));
                if (Always(segs[i].A, lo, skip) || Always(segs[i].B, lo, skip)) bound = MathF.Min(bound, d + h);
            }
        cand.Sort((x, y) => x.D != y.D ? x.D.CompareTo(y.D) : x.Order.CompareTo(y.Order));
        int count = 0;
        while (count < cand.Count && cand[count].D - h <= bound) count++;
        var list = new (int, int)[count];
        for (int i = 0; i < count; i++) list[i] = (segs[cand[i].Order].River, segs[cand[i].Order].Seg);
        this.rivers[k] = list;
    }

    /// True when every point of the cell starting at lo is within d of p on the X or the Y axis (River.Dist never skips it).
    static bool Always(Vector2 p, Vector2 lo, float d) => p.X >= lo.X + Cell - d && p.X <= lo.X + d || p.Y >= lo.Y + Cell - d && p.Y <= lo.Y + d;

    /// True when some point of the cell starting at lo is within d of p on the X or the Y axis.
    static bool Within(Vector2 p, Vector2 lo, float d) => p.X >= lo.X - d && p.X <= lo.X + Cell + d || p.Y >= lo.Y - d && p.Y <= lo.Y + Cell + d;

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float k = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * k);
    }

    int CellOf(Vector2 p)
    {
        int cx = (int)MathF.Floor((p.X - origin) / Cell), cy = (int)MathF.Floor((p.Y - origin) / Cell);
        return cx < 0 || cy < 0 || cx >= n || cy >= n ? -1 : cy * n + cx;
    }

    /// The nearest province centre (ties go to the earlier province, as in the full scan); null off the grid.
    public Province? ProvinceAt(Vector2 p)
    {
        int k = CellOf(p);
        if (k < 0) return null;
        Province? best = null;
        float bd = float.MaxValue;
        foreach (int i in provinces[k])
        {
            float d = Vector2.DistanceSquared(provinceList[i].Center, p);
            if (d < bd) { bd = d; best = provinceList[i]; }
        }
        return best;
    }

    /// Distance to the nearest road; null off the grid.
    public float? RoadDist(Vector2 p)
    {
        int k = CellOf(p);
        if (k < 0) return null;
        float best = float.MaxValue;
        foreach (int i in roads[k])
        {
            var (a, b) = roadList[i];
            var ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    /// Whether a road lies nearer than r: the same answer as RoadDist(p) < r, but a cell far from every road says no at once
    /// and a near one stops at the first road in reach (the drawn map's parties ask it for every step: docs/archive/WORLDMAP.md WM4).
    public bool? NearRoad(Vector2 p, float r)
    {
        int k = CellOf(p);
        if (k < 0) return null;
        if (roadNear[k] - (Cell * 0.70711f + 0.01f) > r) return false;
        foreach (int i in roads[k])
        {
            var (a, b) = roadList[i];
            var ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            if (Vector2.Distance(p, a + ab * t) < r) return true;
        }
        return false;
    }

    /// The same answer as the smallest River.Dist over the rivers; null off the grid.
    public float? RiverDist(Vector2 p)
    {
        int k = CellOf(p);
        if (k < 0) return null;
        float best = float.MaxValue, h = Cell * 0.70711f + 0.01f;
        var cc = new Vector2(origin + (k % n + 0.5f) * Cell, origin + (k / n + 0.5f) * Cell);
        foreach (var (r, i) in rivers[k])
        {
            var path = riverList[r].Path;
            Vector2 a = path[i], b = path[i + 1];
            if (best < float.MaxValue && SegDist(cc, a, b) - h > best) break; // nearest first: nothing further on can beat it
            if (MathF.Abs(p.X - a.X) > skip && MathF.Abs(p.X - b.X) > skip && MathF.Abs(p.Y - a.Y) > skip && MathF.Abs(p.Y - b.Y) > skip) continue;
            var ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }
}
