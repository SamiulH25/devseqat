using Microsoft.Xna.Framework;

namespace Ironfield;

/// A map of the cheapest way to one goal from every cell of the field (docs/archive/TERRAIN.md T4): each cell holds its cost to the goal.
public sealed class FlowField
{
    public readonly Vector2 Goal;
    public readonly bool Horse;
    /// Movement mode: 0 = attacker foot (may climb ladders), 1 = defender foot, 2 = horse.
    public readonly byte Mode;
    readonly float[] cost;
    readonly ushort[] bars;
    readonly Terrain t;

    internal FlowField(Terrain t, Vector2 goal, byte mode, float[] cost, ushort[] bars)
    {
        this.t = t; Goal = goal; Mode = mode; Horse = mode == 2; this.cost = cost; this.bars = bars;
    }

    /// The way to go from p: toward the cheapest of the eight neighbouring cells (zero at the goal or where nothing leads there).
    public Vector2 Dir(Vector2 p)
    {
        int n = t.N;
        int x = Math.Clamp((int)MathF.Round((p.X + t.Half) / t.Step), 0, n - 1), z = Math.Clamp((int)MathF.Round((p.Y + t.Half) / t.Step), 0, n - 1);
        // His cell's centre can lie over the fence he stands against (fences bar steps, not cells): then its cost is not his.
        float here = t.CrossesFeatureNear(p, new Vector2(-t.Half + x * t.Step, -t.Half + z * t.Step), Horse) ? float.PositiveInfinity : cost[z * n + x], best = here;
        int bx = x, bz = z;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if ((dx | dz) == 0 || nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                if (cost[nz * n + nx] >= best || !float.IsPositiveInfinity(here) && (bars[z * n + x] & Battle.StepBit(dx, dz)) != 0) continue; // the field's own barred steps
                if (dx != 0 && dz != 0 && (float.IsPositiveInfinity(cost[z * n + nx]) || float.IsPositiveInfinity(cost[nz * n + x]))) continue; // no cutting round a hedge's end
                if (t.CrossesFeatureNear(p, new Vector2(-t.Half + nx * t.Step, -t.Half + nz * t.Step), Horse)) continue; // not over the hedge he stands against
                best = cost[nz * n + nx]; bx = nx; bz = nz;
            }
        if (bx == x && bz == z && float.IsPositiveInfinity(here)) // in a house's cells, or his cell's centre over a fence: out to the cheapest open cell on his side
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n || cost[nz * n + nx] >= best) continue;
                    if (t.CrossesFeatureNear(p, new Vector2(-t.Half + nx * t.Step, -t.Half + nz * t.Step), Horse, Battle.Clearance(Horse))) continue;
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

/// Battle: pathing (docs/archive/TERRAIN.md T4). On open ground men steer straight, as ever. When the straight line from a company to its
/// goal is blocked (deep water, a hedge or fence, a house; thick wood for horse), the company follows a flow field to the goal
/// until its men are close. Fields are built on the terrain's 2.5 m cells and kept in a small cache.
public partial class Battle
{
    float[]?[] stepCost = new float[]?[3]; // per cell, by mode (0 attacker foot, 1 defender foot, 2 horse)
    ushort[]?[] edgeBlock = new ushort[]?[3]; // per cell, by mode: steps to eight neighbours barred by hedges, fences or fortress walls
    readonly List<FlowField> flowCache = new();
    const int FlowCacheSize = 48;
    /// Within this of its destination a man stops following the field and goes straight.
    const float PathNear = 12;

    /// How many flow fields this battle has built (for the tests and the bench).
    public int FlowBuilds { get; private set; }
    internal int FlowCached => flowCache.Count;

    static byte PathMode(bool horse, int team) => horse ? (byte)2 : team == 0 ? (byte)0 : (byte)1;

    /// Clear cached flow fields and edge blocks when a fortress wall changes (gate opens, breach, ladder, ramp).
    internal void InvalidatePath()
    {
        Array.Clear(stepCost);
        Array.Clear(edgeBlock);
        flowCache.Clear();
        previewFlow = null;
        foreach (var s in Squads) { s.Path = null; s.PathTimer = 0; }
    }

    /// Refresh a company's route every half second: a field when the way is blocked, none when it is clear.
    void UpdatePath(Squad s, float dt)
    {
        if (scripted && Wall == null && Terrain.Features.Count == 0 && Terrain.Rivers.Count == 0) return;
        if ((s.PathTimer -= dt) > 0) return;
        s.PathTimer = 0.5f;
        if (s.Alive == 0) { s.Path = null; return; }
        var goal = s.Order == Order.Charge ? (s.Target?.Alive > 0 ? s.Target.Center : NearestSquadCenter(s)) : s.Anchor;
        if (goal is not Vector2 g) { s.Path = null; return; }
        byte mode = PathMode(s.Cavalry, s.Team);
        if (!AnyBlocked(s, g))
        {
            if (++s.PathClear >= 4) s.Path = null; // clear four times running (different men each time): drop the route
            return;
        }
        s.PathClear = 0;
        if (s.Path != null && s.Path.Mode == mode && Vector2.DistanceSquared(s.Path.Goal, g) < 25) return; // still good
        if (Cached(g, mode) is { } hit) { s.Path = hit; return; }
        if (builtTick == tick) { s.PathTimer = 0; return; } // one new field a tick (each is a few ms): ask again next tick
        builtTick = tick;
        s.Path = Flow(g, mode);
    }

    /// Where a charge with no target makes for: the nearest enemy company, or once none is left the enemy's captain.
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
        if (best != null) return best;
        // No company left: the men outside companies (a captain on his own) are what is left to reach.
        var leader = s.Team == 0 ? EnemyCaptain : Player;
        return leader.Active ? leader.Pos : null;
    }

    /// Is the way to g barred for the company: from its centre, or from any of four of its men? Each look takes the next
    /// four, so over the looks that keep a route every man is tried and the route lasts until the last of them is through.
    bool AnyBlocked(Squad s, Vector2 g)
    {
        // Near the goal men walk straight, unless a fence or hedge stands in the way (a company could stand pressed to one
        // with its places a few metres beyond, and never settle to shoot or fight).
        bool Far(Vector2 p) => Vector2.DistanceSquared(p, g) > PathNear * PathNear
            || (Wall != null && Wall.CrossesWall(p, g, out _))
            || (Terrain.Features.Count > 0 && Terrain.CrossesFeatureNear(p, g, s.Cavalry, Clearance(s.Cavalry)));
        if (Far(s.Center) && WayBlocked(s.Center, g, s.Cavalry)) return true;
        int n = s.Units.Count;
        for (int k = 0; k < 4; k++)
        {
            var u = s.Units[(k * 4 + s.PathClear % 4) * n / 16];
            if (!u.Dead && !u.Routing && Far(u.Pos) && WayBlocked(u.Pos, g, s.Cavalry)) return true;
        }
        return false;
    }

    FlowField? previewFlow;

    /// The way a company would take to dest, as points 2.5 m apart, for the dotted line while you draw an order; null when
    /// it would go straight. The field is kept apart from the battle's cache, so looking never changes what the men do.
    public List<Vector2>? Route(Squad s, Vector2 dest)
    {
        if (s.Alive == 0 || !WayBlocked(s.Center, dest, s.Cavalry)) return null;
        var key = FlowKey(dest);
        byte mode = PathMode(s.Cavalry, s.Team);
        if (previewFlow == null || previewFlow.Goal != key || previewFlow.Mode != mode)
            previewFlow = BuildFlow(key, mode);
        if (float.IsPositiveInfinity(previewFlow.CostFrom(s.Center))) return null;
        var list = new List<Vector2>();
        var p = s.Center;
        for (int i = 0; i < 160 && Vector2.DistanceSquared(p, dest) > PathNear * PathNear; i++)
        {
            var d = previewFlow.Dir(p);
            if (d == Vector2.Zero) break;
            p += d * Terrain.Step;
            list.Add(p);
        }
        list.Add(dest);
        return list;
    }

    /// Is the straight way from a to b barred: deep water, a hedge or fence, a fortress wall, a house; for horse also thick wood?
    internal bool WayBlocked(Vector2 a, Vector2 b, bool horse)
    {
        if (Wall != null && Wall.CrossesWall(a, b, out _)) return true;
        if (Terrain.Features.Count > 0 && Terrain.CrossesFeature(a, b, horse)) return true;
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
        byte mode = PathMode(u.Mounted, u.Team);
        if (f == null || u.Routing || f.Mode != mode || Vector2.DistanceSquared(dest, f.Goal) > 30 * 30) return dest;
        if (Vector2.DistanceSquared(u.Pos, dest) < PathNear * PathNear
            && (Wall == null || !Wall.CrossesWall(u.Pos, dest, out _))
            && !Terrain.CrossesFeatureNear(u.Pos, dest, u.Mounted, Clearance(u.Mounted))) return dest; // near and nothing between
        var d = f.Dir(u.Pos);
        return d == Vector2.Zero ? dest : u.Pos + d * 3;
    }

    int builtTick = -1;

    static Vector2 FlowKey(Vector2 goal) => new(MathF.Round(goal.X / 5) * 5, MathF.Round(goal.Y / 5) * 5);

    /// A field already built for this goal (the 5 m square it falls in), moved to the back of the cache; null if none.
    FlowField? Cached(Vector2 goal, byte mode)
    {
        var key = FlowKey(goal);
        for (int i = 0; i < flowCache.Count; i++)
            if (flowCache[i].Mode == mode && flowCache[i].Goal == key)
            {
                var hit = flowCache[i];
                flowCache.RemoveAt(i);
                flowCache.Add(hit);
                return hit;
            }
        return null;
    }

    /// The flow field to a goal: from the cache, else built (the oldest field makes room).
    FlowField Flow(Vector2 goal, byte mode)
    {
        if (Cached(goal, mode) is { } hit) return hit;
        var f = BuildFlow(FlowKey(goal), mode);
        if (flowCache.Count >= FlowCacheSize) flowCache.RemoveAt(0);
        flowCache.Add(f);
        return f;
    }

    FlowField BuildFlow(Vector2 goal, byte mode)
    {
        FlowBuilds++;
        bool horse = mode == 2;
        int team = mode == 0 ? 0 : 1;
        var step = stepCost[mode] ??= StepCosts(horse);
        var bars = edgeBlock[mode] ??= EdgeBlocks(horse, team);
        int n = Terrain.N;
        var cost = new float[n * n];
        Array.Fill(cost, float.PositiveInfinity);
        int gx = Math.Clamp((int)MathF.Round((goal.X + Terrain.Half) / Terrain.Step), 0, n - 1), gz = Math.Clamp((int)MathF.Round((goal.Y + Terrain.Half) / Terrain.Step), 0, n - 1);
        // Only the part of the field the battle uses (a margin past the flight radius or fortress) is searched.
        float reach = Wall != null ? MathF.Max(MapRadius + 40, Wall.Center.Length() + Wall.Radius + 40) : MapRadius + 40;
        int lim = (int)MathF.Min(n / 2, reach / Terrain.Step), lo = n / 2 - lim, hi = n / 2 + lim;
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
                    if (float.IsPositiveInfinity(w) || (bars[c] & StepBit(dx, dz)) != 0) continue;
                    if (dx != 0 && dz != 0 && (float.IsPositiveInfinity(step[z * n + nx]) || float.IsPositiveInfinity(step[nz * n + x]))) continue; // no cutting a corner
                    float nc = cc + w * (dx != 0 && dz != 0 ? 1.4142f : 1);
                    if (nc < cost[k]) { cost[k] = nc; open.Enqueue(k, nc); }
                }
        }
        return new FlowField(Terrain, goal, mode, cost, bars);
    }

    /// What crossing each cell costs: the inverse of the ground's speed, deep water up to ten times dearer still (go to the ford),
    /// and blocked where a house stands or a hedge or fence comes within a man's width of the cell's centre (`Clearance`). The
    /// line of a thin fence between two rows of cells is barred by EdgeBlocks, so a gap only closes if it is truly too narrow.
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
                if (Wall != null && Wall.InDitch(p)) w *= 2.4f;
                step[z * n + x] = Terrain.House(p) ? float.PositiveInfinity : w;
            }
        float clear = Clearance(horse);
        foreach (var f in Terrain.Features)
        {
            if (!f.Stops(horse)) continue;
            float reach = f.Half + clear;
            var (x0, x1, z0, z1) = CellBox(f.A, f.B, reach);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var c = new Vector2(-Terrain.Half + x * Terrain.Step, -Terrain.Half + z * Terrain.Step);
                    var ab = f.B - f.A;
                    float k = ab.LengthSquared() < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(c - f.A, ab) / ab.LengthSquared(), 0, 1);
                    if (Vector2.DistanceSquared(c, f.A + ab * k) < reach * reach) step[z * n + x] = float.PositiveInfinity;
                }
        }
        return step;
    }

    /// The bit for a step of (dx, dz) in an `edgeBlock` cell.
    internal static ushort StepBit(int dx, int dz) => (ushort)(1 << ((dz + 1) * 3 + dx + 1));

    /// How far past a hedge's or fence's edge a man needs to pass it: a man on foot, a horse (so stakes' gaps stay shut to horse).
    internal static float Clearance(bool horse) => horse ? 1f : 0.4f;

    /// The cells round the line a-b, `pad` past it and one more.
    (int x0, int x1, int z0, int z1) CellBox(Vector2 a, Vector2 b, float pad)
    {
        int n = Terrain.N;
        float st = Terrain.Step, h = Terrain.Half;
        return (Math.Max(0, (int)MathF.Floor((MathF.Min(a.X, b.X) - pad + h) / st) - 1), Math.Min(n - 1, (int)MathF.Ceiling((MathF.Max(a.X, b.X) + pad + h) / st) + 1),
                Math.Max(0, (int)MathF.Floor((MathF.Min(a.Y, b.Y) - pad + h) / st) - 1), Math.Min(n - 1, (int)MathF.Ceiling((MathF.Max(a.Y, b.Y) + pad + h) / st) + 1));
    }

    /// For each cell, the steps to its neighbours that cross a hedge or fence that stops this kind of man. Closing every cell a
    /// fence touched shut every gap under about 5 m on the 2.5 m cells (men walked into a garden and the field had no way out);
    /// StepCosts now closes only the cells within a man's width of it, and this bars the steps over its line between them.
    /// Each feature is taken longer by its thickness and the clearance, so a gap too narrow to pass stays shut.
    ushort[] EdgeBlocks(bool horse, int team)
    {
        int n = Terrain.N;
        float st = Terrain.Step, h = Terrain.Half;
        var bars = new ushort[n * n];
        Vector2 Centre(int x, int z) => new(-h + x * st, -h + z * st);
        foreach (var f in Terrain.Features)
        {
            if (!f.Stops(horse)) continue;
            var along = f.B - f.A;
            along = along.LengthSquared() > 1e-6f ? Vector2.Normalize(along) * (f.Half + Clearance(horse)) : Vector2.Zero;
            Vector2 a = f.A - along, b = f.B + along;
            var (x0, x1, z0, z1) = CellBox(a, b, 0);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if ((dx | dz) == 0 || nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                            if (Terrain.SegmentsCross(Centre(x, z), Centre(nx, nz), a, b)) bars[z * n + x] |= StepBit(dx, dz);
                        }
        }
        // Fortress walls (SE2): bar steps crossing any wall segment unless an open gate, breach, ramp or (attacker foot) ladder lets them pass.
        if (Wall is Fortress fort)
        {
            foreach (var seg in fort.Segments)
            {
                if (seg.Breached) continue;
                if (seg.IsGate && fort.Gates[seg.GateIndex].Open) continue;
                var (x0, x1, z0, z1) = CellBox(seg.A, seg.B, 1.5f);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var c0 = Centre(x, z);
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, nz = z + dz;
                                if ((dx | dz) == 0 || nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                                var c1 = Centre(nx, nz);
                                if (!Terrain.SegmentsCross(c0, c1, seg.A, seg.B)) continue;
                                if (!fort.CanPassSegment(seg, (c0 + c1) * 0.5f, horse, team, out _))
                                    bars[z * n + x] |= StepBit(dx, dz);
                            }
                    }
            }
        }
        return bars;
    }
}
