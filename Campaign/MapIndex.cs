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
    readonly (int River, int Seg)[][] rivers;
    readonly List<Province> provinceList;
    readonly List<(Vector2 A, Vector2 B)> roadList;
    readonly List<River> riverList;

    public MapIndex(List<Province> provinces, List<(Vector2 A, Vector2 B)> roads, List<River> rivers, float half)
    {
        provinceList = provinces; roadList = roads; riverList = rivers;
        origin = -half;
        n = Math.Max(1, (int)MathF.Ceiling(2 * half / Cell));
        this.provinces = new int[n * n][];
        this.roads = new int[n * n][];
        this.rivers = new (int, int)[n * n][];
        float h = Cell * 0.70711f + 0.01f; // half the cell's diagonal, and a hair for rounding
        // Each distance is worked out once per cell (the nearest, then everything within reach of it), in plain loops.
        var pd = new float[provinces.Count];
        var rd = new float[roads.Count];
        var segs = new List<(int River, int Seg, Vector2 A, Vector2 B)>();
        for (int r = 0; r < rivers.Count; r++)
            for (int i = 0; i + 1 < rivers[r].Path.Length; i++) segs.Add((r, i, rivers[r].Path[i], rivers[r].Path[i + 1]));
        var keep = new List<int>();
        var cand = new List<(float D, int Order)>();
        for (int cy = 0; cy < n; cy++)
            for (int cx = 0; cx < n; cx++)
            {
                int k = cy * n + cx;
                var lo = new Vector2(origin + cx * Cell, origin + cy * Cell);
                var cc = lo + new Vector2(Cell / 2);
                // The nearest centre to a point in the cell is within (nearest to the cell's centre) + 2h of the cell's centre.
                float near = provinces.Count == 0 ? 0 : float.MaxValue;
                for (int i = 0; i < pd.Length; i++) near = MathF.Min(near, pd[i] = Vector2.Distance(provinces[i].Center, cc));
                keep.Clear();
                for (int i = 0; i < pd.Length; i++) if (pd[i] <= near + 2 * h) keep.Add(i);
                this.provinces[k] = keep.ToArray();
                float road = roads.Count == 0 ? 0 : float.MaxValue;
                for (int i = 0; i < rd.Length; i++) road = MathF.Min(road, rd[i] = SegDist(cc, roads[i].A, roads[i].B));
                keep.Clear();
                for (int i = 0; i < rd.Length; i++) if (rd[i] <= road + 2 * h) keep.Add(i);
                this.roads[k] = keep.ToArray();
                // River.Dist skips a bend whose ends are more than 40 away on both axes; keep every bend it might not skip
                // for some point in the cell, nearest first (ties in river order), so the look-up can stop once no bend can come closer.
                cand.Clear();
                for (int i = 0; i < segs.Count; i++)
                    if (Within(segs[i].A, lo, 40) || Within(segs[i].B, lo, 40)) cand.Add((SegDist(cc, segs[i].A, segs[i].B), i));
                cand.Sort((x, y) => x.D != y.D ? x.D.CompareTo(y.D) : x.Order.CompareTo(y.Order));
                var list = new (int, int)[cand.Count];
                for (int i = 0; i < list.Length; i++) list[i] = (segs[cand[i].Order].River, segs[cand[i].Order].Seg);
                this.rivers[k] = list;
            }
    }

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
            if (MathF.Abs(p.X - a.X) > 40 && MathF.Abs(p.X - b.X) > 40 && MathF.Abs(p.Y - a.Y) > 40 && MathF.Abs(p.Y - b.Y) > 40) continue;
            var ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }
}
