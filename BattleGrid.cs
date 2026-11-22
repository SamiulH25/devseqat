using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap E1: a uniform grid over the field so separation, morale, targeting and arrows look only at nearby men,
/// not at every man on the field. Rebuilt every tick (cheap); cells are 4 m, covering +-256 m (the whole battle ground).
public sealed partial class Battle
{
    const float CellSize = 4;
    const int GridN = 128;
    readonly List<Unit>[] cells = Enumerable.Range(0, GridN * GridN).Select(_ => new List<Unit>(8)).ToArray();
    readonly List<int> dirtyCells = new(512);
    readonly List<Unit> near = new(64), nearBrace = new(64), nearMass = new(32);
    bool anyMounted0, anyMounted1;

    static int CellOf(float v) => Math.Clamp((int)MathF.Floor(v / CellSize) + GridN / 2, 0, GridN - 1);

    void BuildGrid()
    {
        for (int i = 0; i < dirtyCells.Count; i++) cells[dirtyCells[i]].Clear();
        dirtyCells.Clear();
        anyMounted0 = anyMounted1 = false;
        var units = Units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            u.Index = i;
            if (!u.Active) continue;
            if (u.Mounted) { if (u.Team == 0) anyMounted0 = true; else anyMounted1 = true; }
            int idx = CellOf(u.Pos.Y) * GridN + CellOf(u.Pos.X);
            var cell = cells[idx];
            if (cell.Count == 0) dirtyCells.Add(idx);
            cell.Add(u);
        }
    }

    /// Active men within (about) r of p, into `into` (cleared first). Callers check the exact distance.
    List<Unit> Near(Vector2 p, float r, List<Unit> into)
    {
        into.Clear();
        int x0 = CellOf(p.X - r), x1 = CellOf(p.X + r), z0 = CellOf(p.Y - r), z1 = CellOf(p.Y + r);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var cell = cells[z * GridN + x];
                if (cell.Count > 0) into.AddRange(cell);
            }
        return into;
    }

    /// Nearest active, non-routing enemy of u (within `only` when given): grows the search ring before scanning everyone.
    static readonly float[] Rings = { 4f, 10f };

    (Unit? unit, float dist) NearestFast(Unit u, Squad? only)
    {
        if (only != null) return NearestScan(u, only.Units);
        var up = u.Pos;
        int uTeam = u.Team;
        foreach (float r in Rings)
        {
            Unit? best = null;
            float bd = r * r;
            int x0 = CellOf(up.X - r), x1 = CellOf(up.X + r), z0 = CellOf(up.Y - r), z1 = CellOf(up.Y + r);
            for (int z = z0; z <= z1; z++)
            {
                int row = z * GridN;
                for (int x = x0; x <= x1; x++)
                {
                    var cell = cells[row + x];
                    int cnt = cell.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var o = cell[i];
                        if (o.Team == uTeam || o.Routing) continue;
                        float d = Vector2.DistanceSquared(o.Pos, up);
                        if (d < bd) { bd = d; best = o; }
                    }
                }
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

    /// The nearest routing enemy within 16 m that is closer than `than` (pursuers chase the broken too).
    (Unit unit, float dist)? NearestRouter(Unit u, float than)
    {
        Unit? best = null;
        float bd = MathF.Min(than, 16);
        bd *= bd;
        var up = u.Pos;
        int uTeam = u.Team;
        int x0 = CellOf(up.X - 16), x1 = CellOf(up.X + 16), z0 = CellOf(up.Y - 16), z1 = CellOf(up.Y + 16);
        for (int z = z0; z <= z1; z++)
        {
            int row = z * GridN;
            for (int x = x0; x <= x1; x++)
            {
                var cell = cells[row + x];
                int cnt = cell.Count;
                for (int i = 0; i < cnt; i++)
                {
                    var o = cell[i];
                    if (o.Team == uTeam || !o.Routing || !o.Active) continue;
                    float d = Vector2.DistanceSquared(o.Pos, up);
                    if (d < bd) { bd = d; best = o; }
                }
            }
        }
        return best == null ? null : (best, MathF.Sqrt(bd));
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

    /// The speed a footman's weight is good for against a horse: a charge is stopped when the weight braced against it, times
    /// this, is more than the horse's weight times the speed it comes on at (Impact).
    const float BraceSpeed = 8;

    /// The weight a footman puts against a horse coming at him along `dir` (from the horse to him), in BodyMass: his own,
    /// and, if he stands in his place in a formed company whose front the horse comes at, his footing (drill and order) on
    /// it and the men at his back in his file leaning in (up to three). From a flank or behind he has only himself.
    float BraceMass(Unit m, Vector2 dir)
    {
        float mass = BodyMass(m);
        if (m.Routing || m.Squad is not { Loose: false, Broken: false } s || !Ranked(s) || Vector2.Dot(-dir, s.Front) < 0.3f) return mass;
        float foot = Footing(m);
        if (foot <= 1) return mass; // out of his place: nobody at his back
        int behind = 0;
        var mp = m.Pos;
        int x0 = CellOf(mp.X - 4), x1 = CellOf(mp.X + 4), z0 = CellOf(mp.Y - 4), z1 = CellOf(mp.Y + 4);
        for (int z = z0; z <= z1 && behind < 3; z++)
        {
            int row = z * GridN;
            for (int x = x0; x <= x1 && behind < 3; x++)
            {
                var cell = cells[row + x];
                int cnt = cell.Count;
                for (int i = 0; i < cnt; i++)
                {
                    var o = cell[i];
                    if (o == m || o.Squad != s || !o.Active || o.Routing || Vector2.DistanceSquared(o.Pos, o.Slot) > 1) continue;
                    var d = o.Pos - mp;
                    float along = Vector2.Dot(d, dir), aside = MathF.Abs(d.X * dir.Y - d.Y * dir.X);
                    if (along > 0.3f && along < 4 && aside < 0.9f && ++behind == 3) break;
                }
            }
        }
        return (mass + behind) * foot;
    }

    /// Last word on horse against formed foot: a horse overlapping a man whose braced weight (BraceMass) is more than the
    /// horse's way can carry is put back out of him (the man does not give), whatever shoved it in (its own riders pressing
    /// from behind as often as not). A heavy horse still coming on hard is left to Impact, which may carry it through.
    void HoldHorseOff()
    {
        if (!anyMounted0 && !anyMounted1) return;
        foreach (var h in Units)
        {
            if (!h.Active || !h.Mounted || Airborne(h)) continue;
            float rh = BodyRadius(h), way = BodyMass(h) * MathF.Max(MathF.Abs(h.Speed), 1);
            var hp = h.Pos;
            int x0 = CellOf(hp.X - 2.1f), x1 = CellOf(hp.X + 2.1f), z0 = CellOf(hp.Y - 2.1f), z1 = CellOf(hp.Y + 2.1f);
            for (int z = z0; z <= z1; z++)
            {
                int row = z * GridN;
                for (int x = x0; x <= x1; x++)
                {
                    var cell = cells[row + x];
                    int cnt = cell.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var m = cell[i];
                        if (!m.Active || m.Mounted || m.Team == h.Team) continue;
                        float r = rh + BodyRadius(m);
                        var d = h.Pos - m.Pos;
                        float l2 = d.LengthSquared();
                        if (l2 >= r * r) continue;
                        float l = MathF.Sqrt(MathF.Max(l2, 1e-6f));
                        var n = l2 > 1e-6f ? d / l : -h.HeadingDir;
                        if (BraceMass(m, -n) * BraceSpeed < way) continue; // he cannot hold it
                        h.Pos = m.Pos + n * r;
                    }
                }
            }
        }
    }

    /// Push overlapping bodies apart (horses shove harder), and let riders at speed crash into footmen: only near pairs.
    void Separate(float dt)
    {
        BuildGrid();
        var units = Units;
        for (int ui = 0; ui < units.Count; ui++)
        {
            var a = units[ui];
            if (!a.Active || Airborne(a)) continue; // a wyvern aloft passes over them
            float ra = BodyRadius(a), ma = BodyMass(a);
            var ap = a.Pos;
            int x0 = CellOf(ap.X - 2.1f), x1 = CellOf(ap.X + 2.1f), z0 = CellOf(ap.Y - 2.1f), z1 = CellOf(ap.Y + 2.1f);
            for (int z = z0; z <= z1; z++)
            {
                int row = z * GridN;
                for (int x = x0; x <= x1; x++)
                {
                    var cell = cells[row + x];
                    int cnt = cell.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        var b = cell[i];
                        if (b.Index <= a.Index || !b.Active || Airborne(b)) continue;
                        float rb = BodyRadius(b), r = ra + rb;
                        var d = b.Pos - a.Pos;
                        float l2 = d.LengthSquared();
                        if (l2 >= r * r) continue;
                        if (l2 < 1e-6f) { d = new Vector2(0.01f, 0); l2 = 1e-4f; }
                        float l = MathF.Sqrt(l2), mb = BodyMass(b);
                        var n = d / l;
                        if (a.Team != b.Team)
                        {
                            // A man holding his place in the ranks is hard to shove. Against a horse he weighs what he can brace
                            // (BraceMass: the men at his back too, if the horse comes at their front), told against the horse's way:
                            // a slow horse is shoved back by a formed file, a galloping one shoves through all but the deepest.
                            if (b.Mounted && !a.Mounted) ma = BraceMass(a, -n) * BraceSpeed / MathF.Max(MathF.Abs(b.Speed), 2);
                            else ma = BodyMass(a) * Footing(a);
                            if (a.Mounted && !b.Mounted) mb = BraceMass(b, n) * BraceSpeed / MathF.Max(MathF.Abs(a.Speed), 2);
                            else mb *= Footing(b);
                        }
                        else ma = BodyMass(a);
                        var push = n * (r - l);
                        a.Pos -= push * (mb / (ma + mb));
                        b.Pos += push * (ma / (ma + mb));
                        if (a.Team == b.Team) continue;
                        if (a.Mounted && !b.Mounted) Impact(a, b, n, dt);
                        else if (b.Mounted && !a.Mounted) Impact(b, a, -n, dt);
                        else if (a.Mounted)
                        {
                            a.Speed *= 1 - 1.5f * dt;
                            b.Speed *= 1 - 1.5f * dt;
                            float closing = MathF.Abs(Vector2.Dot(a.Vel - b.Vel, n));
                            if (closing >= 6 && a.ImpactCd <= 0 && b.ImpactCd <= 0)
                            {
                                a.ImpactCd = b.ImpactCd = 0.6f;
                                Sound(Cue.Impact, (a.Pos + b.Pos) * 0.5f);
                                a.Speed *= 0.65f;
                                b.Speed *= 0.65f;
                                HitHorse(a, closing * 1.1f, b);
                                HitHorse(b, closing * 1.1f, a);
                            }
                        }
                    }
                }
            }
        }
    }
}
