using Microsoft.Xna.Framework;

namespace Ironfield;

/// A map of the cheapest way to one goal from every cell of the field (docs/TERRAIN.md T4): each cell holds its cost to the goal.
public sealed class FlowField
{
    public readonly Vector2 Goal;
    public readonly bool Horse;
    readonly float[] cost;
    readonly Terrain t;

    internal FlowField(Terrain t, Vector2 goal, bool horse, float[] cost) { this.t = t; Goal = goal; Horse = horse; this.cost = cost; }

    /// The way to go from p: toward the cheapest of the eight neighbouring cells (zero at the goal or where nothing leads there).
    public Vector2 Dir(Vector2 p)
    {
        int n = t.N;
        int x = Math.Clamp((int)MathF.Round((p.X + t.Half) / t.Step), 0, n - 1), z = Math.Clamp((int)MathF.Round((p.Y + t.Half) / t.Step), 0, n - 1);
        float here = cost[z * n + x], best = here;
        int bx = x, bz = z;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if ((dx | dz) == 0 || nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                if (cost[nz * n + nx] >= best) continue;
                if (dx != 0 && dz != 0 && (float.IsPositiveInfinity(cost[z * n + nx]) || float.IsPositiveInfinity(cost[nz * n + x]))) continue; // no cutting round a hedge's end
                if (t.CrossesFeatureNear(p, new Vector2(-t.Half + nx * t.Step, -t.Half + nz * t.Step))) continue; // not over the hedge he stands against
                best = cost[nz * n + nx]; bx = nx; bz = nz;
            }
        if (bx == x && bz == z) return float.IsPositiveInfinity(here) ? Vector2.Zero : Goal - p is var g && g.LengthSquared() > 1e-4f ? Vector2.Normalize(g) : Vector2.Zero;
        var to = new Vector2(-t.Half + bx * t.Step, -t.Half + bz * t.Step) - p;
        return to.LengthSquared() > 1e-4f ? Vector2.Normalize(to) : Vector2.Zero;
    }

    /// Cost to the goal from p (infinite where it cannot be reached).
    public float CostFrom(Vector2 p)
    {
        int n = t.N;
        int x = Math.Clamp((int)MathF.Round((p.X + t.Half) / t.Step), 0, n - 1), z = Math.Clamp((int)MathF.Round((p.Y + t.Half) / t.Step), 0, n - 1);
        return cost[z * n + x];
    }
}

/// Battle: pathing (docs/TERRAIN.md T4). On open ground men steer straight, as ever. When the straight line from a company to its
/// goal is blocked (deep water, a hedge or fence, a house; thick wood for horse), the company follows a flow field to the goal
/// until its men are close. Fields are built on the terrain's 2.5 m cells and kept in a small cache.
public partial class Battle
{
    float[]?[] stepCost = new float[]?[2]; // per cell, for foot and horse: the cost of crossing it (infinite = blocked)
    readonly List<FlowField> flowCache = new();
    const int FlowCacheSize = 48;
    /// Within this of its destination a man stops following the field and goes straight.
    const float PathNear = 12;

    /// How many flow fields this battle has built (for the tests and the bench).
    public int FlowBuilds { get; private set; }
    internal int FlowCached => flowCache.Count;

    /// Refresh a company's route every half second: a field when the way is blocked, none when it is clear.
    void UpdatePath(Squad s, float dt)
    {
        if (scripted && Terrain.Features.Count == 0 && Terrain.Rivers.Count == 0) return;
        if (Wall != null || (s.PathTimer -= dt) > 0) return;
        s.PathTimer = 0.5f;
        if (s.Alive == 0) { s.Path = null; return; }
        var goal = s.Order == Order.Charge ? (s.Target?.Alive > 0 ? s.Target.Center : NearestSquadCenter(s)) : s.Anchor;
        if (goal is not Vector2 g) { s.Path = null; return; }
        if (!AnyBlocked(s, g))
        {
            if (++s.PathClear >= 4) s.Path = null; // clear four times running (different men each time): drop the route
            return;
        }
        s.PathClear = 0;
        if (s.Path != null && s.Path.Horse == s.Cavalry && Vector2.DistanceSquared(s.Path.Goal, g) < 25) return; // still good
        if (Cached(g, s.Cavalry) is { } hit) { s.Path = hit; return; }
        if (builtTick == tick) { s.PathTimer = 0; return; } // one new field a tick (each is a few ms): ask again next tick
        builtTick = tick;
        s.Path = Flow(g, s.Cavalry);
    }

    Vector2? NearestSquadCenter(Squad s)
    {
        Vector2? best = null;
        float bd = float.MaxValue;
        foreach (var o in Squads)
        {
            if (o.Team == s.Team || o.Alive == 0) continue;
            float d = Vector2.DistanceSquared(o.Center, s.Center);
            if (d < bd) { bd = d; best = o.Center; }
        }
        return best;
    }

    /// Is the way to g barred for the company: from its centre, or from any of four of its men? Each look takes the next
    /// four, so over the looks that keep a route every man is tried and the route lasts until the last of them is through.
    bool AnyBlocked(Squad s, Vector2 g)
    {
        bool Far(Vector2 p) => Vector2.DistanceSquared(p, g) > PathNear * PathNear;
        if (Far(s.Center) && WayBlocked(s.Center, g, s.Cavalry)) return true;
        int n = s.Units.Count;
        for (int k = 0; k < 4; k++)
        {
            var u = s.Units[(k * 4 + s.PathClear % 4) * n / 16];
            if (!u.Dead && !u.Routing && Far(u.Pos) && WayBlocked(u.Pos, g, s.Cavalry)) return true;
        }
        return false;
    }

    /// Is the straight way from a to b barred: deep water, a hedge or fence, a house; for horse also thick wood?
    internal bool WayBlocked(Vector2 a, Vector2 b, bool horse)
    {
        if (Terrain.Features.Count > 0 && Terrain.CrossesFeature(a, b)) return true;
        float len = Vector2.Distance(a, b);
        var dir = (b - a) / MathF.Max(len, 0.01f);
        for (float s = 0; s < len; s += 2.5f)
        {
            var p = a + dir * s;
            var g = Terrain.GroundAt(p);
            if (g == GroundKind.Water || horse && g == GroundKind.DenseWood || Terrain.House(p)) return true;
        }
        return false;
    }

    /// Where a man heading for `dest` should steer now: along his company's field while far, straight when near.
    Vector2 PathVia(Unit u, Vector2 dest)
    {
        var f = u.Squad?.Path;
        if (f == null || u.Routing || f.Horse != u.Mounted || Vector2.DistanceSquared(u.Pos, dest) < PathNear * PathNear || Vector2.DistanceSquared(dest, f.Goal) > 30 * 30) return dest;
        var d = f.Dir(u.Pos);
        return d == Vector2.Zero ? dest : u.Pos + d * 3;
    }

    int builtTick = -1;

    static Vector2 FlowKey(Vector2 goal) => new(MathF.Round(goal.X / 5) * 5, MathF.Round(goal.Y / 5) * 5);

    /// A field already built for this goal (the 5 m square it falls in), moved to the back of the cache; null if none.
    FlowField? Cached(Vector2 goal, bool horse)
    {
        var key = FlowKey(goal);
        for (int i = 0; i < flowCache.Count; i++)
            if (flowCache[i].Horse == horse && flowCache[i].Goal == key)
            {
                var hit = flowCache[i];
                flowCache.RemoveAt(i);
                flowCache.Add(hit);
                return hit;
            }
        return null;
    }

    /// The flow field to a goal: from the cache, else built (the oldest field makes room).
    FlowField Flow(Vector2 goal, bool horse)
    {
        if (Cached(goal, horse) is { } hit) return hit;
        var f = BuildFlow(FlowKey(goal), horse);
        if (flowCache.Count >= FlowCacheSize) flowCache.RemoveAt(0);
        flowCache.Add(f);
        return f;
    }

    FlowField BuildFlow(Vector2 goal, bool horse)
    {
        FlowBuilds++;

        var step = stepCost[horse ? 1 : 0] ??= StepCosts(horse);
        int n = Terrain.N;
        var cost = new float[n * n];
        Array.Fill(cost, float.PositiveInfinity);
        int gx = Math.Clamp((int)MathF.Round((goal.X + Terrain.Half) / Terrain.Step), 0, n - 1), gz = Math.Clamp((int)MathF.Round((goal.Y + Terrain.Half) / Terrain.Step), 0, n - 1);
        // Only the part of the field the battle uses (a margin past the flight radius) is searched.
        int lim = (int)MathF.Min(n / 2, (MapRadius + 40) / Terrain.Step), lo = n / 2 - lim, hi = n / 2 + lim;
        var open = new PriorityQueue<int, float>();
        cost[gz * n + gx] = 0;
        open.Enqueue(gz * n + gx, 0);
        while (open.TryDequeue(out int c, out float cc))
        {
            if (cc > cost[c]) continue;
            int x = c % n, z = c / n;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if ((dx | dz) == 0 || nx < lo || nz < lo || nx > hi || nz > hi) continue;
                    int k = nz * n + nx;
                    float w = step[k];
                    if (float.IsPositiveInfinity(w)) continue;
                    if (dx != 0 && dz != 0 && (float.IsPositiveInfinity(step[z * n + nx]) || float.IsPositiveInfinity(step[nz * n + x]))) continue; // no cutting a corner
                    float nc = cc + w * (dx != 0 && dz != 0 ? 1.4142f : 1);
                    if (nc < cost[k]) { cost[k] = nc; open.Enqueue(k, nc); }
                }
        }
        return new FlowField(Terrain, goal, horse, cost);
    }

    /// What crossing each cell costs: the inverse of the ground's speed, deep water up to ten times dearer still (go to the ford),
    /// and blocked where a hedge, fence or house stands.
    float[] StepCosts(bool horse)
    {
        int n = Terrain.N;
        var step = new float[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(-Terrain.Half + x * Terrain.Step, -Terrain.Half + z * Terrain.Step);
                var g = Terrain.GroundAt(p);
                var def = Grounds.Get(g);
                float w = 1 / (horse ? def.HorseSpeed : def.FootSpeed);
                if (g == GroundKind.Water) w *= 2 + 8 * MathF.Min(1, Terrain.WaterDepth(p)); // deep water much dearer: men go to the ford
                step[z * n + x] = Terrain.House(p) ? float.PositiveInfinity : w;
            }
        foreach (var f in Terrain.Features)
        {
            float len = Vector2.Distance(f.A, f.B);
            for (float s = 0; s <= len; s += 0.5f)
            {
                var p = Vector2.Lerp(f.A, f.B, len < 0.01f ? 0 : s / len);
                int x = (int)MathF.Round((p.X + Terrain.Half) / Terrain.Step), z = (int)MathF.Round((p.Y + Terrain.Half) / Terrain.Step);
                if (x >= 0 && z >= 0 && x < n && z < n) step[z * n + x] = float.PositiveInfinity;
            }
        }
        return step;
    }
}
