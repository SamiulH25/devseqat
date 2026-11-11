using Microsoft.Xna.Framework;

namespace Ironfield;

/// `MapLife` on a battlefield (docs/WORLD_LIFE.md WL6): a herd or two grazing out at the edge of the field that runs
/// when the men come near, birds roosting in the woods that lift in a cloud when the armies close, and crows that
/// gather over the dead. Drawing only: the sim never reads it, so replays are untouched.
sealed partial class MapLife
{
    readonly List<(Vector2 Pos, bool Flown)> roosts = new();
    readonly List<Flock> flushed = new();

    /// Grazing herds at the edge of the field, clear of the water and the trees.
    void BattleHomes()
    {
        var r = new Random(t.Seed * 7 + 3);
        for (int i = 0, tries = 0; i < 2 + r.Next(2) && tries < 60; tries++)
        {
            float a = r.NextSingle() * MathF.Tau, d = 110 + r.NextSingle() * 60;
            var p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (t.WaterDepth(p) > 0 || MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y)) > t.Half - 20 || t.Foliage.Any(f => Vector2.DistanceSquared(f.Pos, p) < 14 * 14)) continue;
            homes.Add((p, r.Next(3) == 0 ? Beast.Cow : Beast.Sheep, null, 1000 + i));
            i++;
        }
    }

    /// Where birds roost: the heart of each patch of wood (an 8 m cell holding three trees or more), at most ten.
    void FindRoosts()
    {
        var count = new Dictionary<(int, int), int>();
        foreach (var f in t.Foliage)
            if (f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Birch)
            {
                var k = ((int)MathF.Floor(f.Pos.X / 8), (int)MathF.Floor(f.Pos.Y / 8));
                count[k] = count.GetValueOrDefault(k) + 1;
            }
        foreach (var ((x, z), n) in count.Where(c => c.Value >= 3).OrderBy(c => Hash(c.Key.Item1, c.Key.Item2)))
        {
            var p = new Vector2(x * 8 + 4, z * 8 + 4);
            if (roosts.Any(q => Vector2.Distance(q.Pos, p) < 40)) continue;
            roosts.Add((p, false));
            if (roosts.Count >= 10) break;
        }
    }

    /// Birds lift from a wood once men come within 35 m of it, and fly off climbing, away from them.
    void UpdateRoosts(float dt, Vector2 focus, float reach, IReadOnlyList<Vector2> men)
    {
        for (int i = 0; i < roosts.Count; i++)
        {
            var (p, flown) = roosts[i];
            if (flown) continue;
            Vector2? near = null;
            foreach (var m in men) if (Vector2.DistanceSquared(m, p) < 35 * 35) { near = m; break; }
            if (near is not Vector2 by) continue;
            roosts[i] = (p, true);
            var f = MakeFlock(p, 10 + rng.Next(10), rng.Next(3) == 0 ? new Color(70, 62, 54) : new Color(38, 36, 40), 0.6f, 4, false, 0, 9 + rng.NextSingle() * 3, false);
            var away = p - by;
            f.Dir = away.LengthSquared() > 1e-3f ? Vector2.Normalize(away) : Vector2.UnitX;
            f.Climb = 3 + rng.NextSingle() * 2;
            flushed.Add(f);
        }
        foreach (var f in flushed)
        {
            f.Pos += f.Dir * f.Speed * dt;
            f.Alt = MathF.Min(f.Alt + f.Climb * dt, 30);
        }
        flushed.RemoveAll(f => Vector2.Distance(f.Pos, focus) > reach * 1.5f);
    }
}
