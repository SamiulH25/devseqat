using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap E1: a uniform grid over the field so separation, morale, targeting and arrows look only at nearby men,
/// not at every man on the field. Rebuilt every tick (cheap); cells are 4 m, covering +-128 m (beyond is the edge cell).
public sealed partial class Battle
{
    const float CellSize = 4;
    const int GridN = 64;
    readonly List<Unit>[] cells = Enumerable.Range(0, GridN * GridN).Select(_ => new List<Unit>(8)).ToArray();
    readonly List<Unit> near = new(64), nearBrace = new(64);

    static int CellOf(float v) => Math.Clamp((int)MathF.Floor(v / CellSize) + GridN / 2, 0, GridN - 1);

    void BuildGrid()
    {
        foreach (var c in cells) c.Clear();
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            u.Index = i;
            if (u.Active) cells[CellOf(u.Pos.Y) * GridN + CellOf(u.Pos.X)].Add(u);
        }
    }

    /// Active men within (about) r of p, into `into` (cleared first). Callers check the exact distance.
    List<Unit> Near(Vector2 p, float r, List<Unit> into)
    {
        into.Clear();
        int x0 = CellOf(p.X - r), x1 = CellOf(p.X + r), z0 = CellOf(p.Y - r), z1 = CellOf(p.Y + r);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                into.AddRange(cells[z * GridN + x]);
        return into;
    }

    /// Nearest active, non-routing enemy of u (within `only` when given): grows the search ring before scanning everyone.
    static readonly float[] Rings = { 4f, 10f };

    (Unit? unit, float dist) NearestFast(Unit u, Squad? only)
    {
        if (only != null) return NearestScan(u, only.Units);
        foreach (float r in Rings)
        {
            Unit? best = null;
            float bd = r * r;
            foreach (var o in Near(u.Pos, r, near))
            {
                if (o.Team == u.Team || o.Routing) continue;
                float d = Vector2.DistanceSquared(o.Pos, u.Pos);
                if (d < bd) { bd = d; best = o; }
            }
            if (best != null) return (best, MathF.Sqrt(bd));
        }
        return NearestFar(u);
    }

    /// Nobody within 10 m: look in the enemy squad whose bounding circle comes closest (and at the enemy general).
    (Unit? unit, float dist) NearestFar(Unit u)
    {
        Squad? bestSquad = null;
        float bd = float.MaxValue;
        foreach (var e in Squads)
        {
            if (e.Team == u.Team || e.Alive == 0) continue;
            float d = Vector2.Distance(e.Center, u.Pos) - e.Radius;
            if (d < bd) { bd = d; bestSquad = e; }
        }
        var (best, dist) = bestSquad != null ? NearestScan(u, bestSquad.Units) : (null, float.MaxValue);
        if (Player.Team != u.Team && Player.Active && !Player.Routing && Vector2.Distance(Player.Pos, u.Pos) is float pd && pd < dist) (best, dist) = (Player, pd);
        return best == null ? NearestScan(u, Units) : (best, dist);
    }

    (Unit? unit, float dist) NearestScan(Unit u, List<Unit> among)
    {
        Unit? best = null;
        float bd = float.MaxValue;
        foreach (var o in among)
        {
            if (o.Team == u.Team || !o.Active || o.Routing) continue;
            float d = Vector2.DistanceSquared(o.Pos, u.Pos);
            if (d < bd) { bd = d; best = o; }
        }
        return (best, MathF.Sqrt(bd));
    }

    /// Push overlapping bodies apart (horses shove harder), and let riders at speed crash into footmen: only near pairs.
    void Separate(float dt)
    {
        BuildGrid();
        foreach (var a in Units)
        {
            if (!a.Active) continue;
            float ra = a.Mounted ? 1.0f : 0.375f, ma = a.Mounted ? 6 : 1;
            foreach (var b in Near(a.Pos, 2.1f, near))
            {
                if (b.Index <= a.Index || !b.Active) continue;
                float rb = b.Mounted ? 1.0f : 0.375f, r = ra + rb;
                var d = b.Pos - a.Pos;
                float l2 = d.LengthSquared();
                if (l2 >= r * r) continue;
                if (l2 < 1e-6f) { d = new Vector2(0.01f, 0); l2 = 1e-4f; }
                float l = MathF.Sqrt(l2), mb = b.Mounted ? 6 : 1;
                var n = d / l;
                var push = n * (r - l);
                a.Pos -= push * (mb / (ma + mb));
                b.Pos += push * (ma / (ma + mb));
                if (a.Team == b.Team) continue;
                if (a.Mounted && !b.Mounted) Impact(a, b, n, dt);
                else if (b.Mounted && !a.Mounted) Impact(b, a, -n, dt);
                else if (a.Mounted) { a.Speed *= 1 - 1.5f * dt; b.Speed *= 1 - 1.5f * dt; }
            }
        }
    }
}
