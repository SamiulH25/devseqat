using Microsoft.Xna.Framework;

namespace Ironfield;

/// A line across the field that men cannot walk through (docs/TERRAIN.md 3): a hedge, a fence, a bridge's rail; or a row of
/// sharpened stakes, which only stops horse (T6). Gaps are simply the spaces between segments.
public enum FeatureKind { Hedge, Fence, Rail, Stakes }

/// One straight length of a line feature, from A to B, `Half` thick either side of the line.
public readonly record struct Feature(FeatureKind Kind, Vector2 A, Vector2 B, float Half)
{
    public float Height => Kind switch { FeatureKind.Hedge => 1.7f, FeatureKind.Stakes => 1.0f, _ => 1.1f };
    /// Stakes: men on foot pass between them, horse cannot.
    public bool HorseOnly => Kind == FeatureKind.Stakes;
    /// Does it stop this man (foot pass stakes)?
    public bool Stops(bool mounted) => mounted || !HorseOnly;
}

/// Terrain: line features, kept in their own bucket grid so `PushOut` only looks at the near ones.
public sealed partial class Terrain
{
    public readonly List<Feature> Features = new();
    readonly Dictionary<int, List<int>> featureCells = new();

    /// Lay a length of hedge, fence or rail from a to b.
    public void AddFeature(FeatureKind kind, Vector2 a, Vector2 b)
    {
        var f = new Feature(kind, a, b, kind switch { FeatureKind.Hedge => 0.5f, FeatureKind.Stakes => 0.35f, _ => 0.12f });
        Features.Add(f);
        float pad = f.Half + 1;
        int x0 = CellX(MathF.Min(a.X, b.X) - pad), x1 = CellX(MathF.Max(a.X, b.X) + pad);
        int z0 = CellX(MathF.Min(a.Y, b.Y) - pad), z1 = CellX(MathF.Max(a.Y, b.Y) + pad);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!featureCells.TryGetValue(z * bucketSide + x, out var l)) featureCells[z * bucketSide + x] = l = new List<int>();
                l.Add(Features.Count - 1);
            }
    }

    /// Take back the last features laid (only while building the field: the cells hold indices).
    void RemoveFeatureAt(int i)
    {
        foreach (var l in featureCells.Values) { l.Remove(i); for (int k = 0; k < l.Count; k++) if (l[k] > i) l[k]--; }
        Features.RemoveAt(i);
    }

    int CellX(float v) => Math.Clamp((int)((v + Half) / Cell), 0, bucketSide - 1);

    /// A line of feature with gaps: segments `run` long, then `gap` open, from a toward b.
    void FeatureLine(FeatureKind kind, Vector2 a, Vector2 b, float run, float gap, Random rng)
    {
        float len = Vector2.Distance(a, b);
        if (len < 1) return;
        var d = (b - a) / len;
        for (float s = rng.NextSingle() * gap; s < len;)
        {
            float e = MathF.Min(len, s + run * (0.75f + rng.NextSingle() * 0.5f));
            if (e - s > 1) AddFeature(kind, a + d * s, a + d * e);
            s = e + gap;
        }
    }

    /// Push a circle of radius r out of any feature it overlaps (stakes only for horse). Returns true if it hit one.
    bool PushOutFeatures(ref Vector2 p, float r, bool mounted)
    {
        if (Features.Count == 0 || !featureCells.TryGetValue(CellX(p.Y) * bucketSide + CellX(p.X), out var near)) return false;
        bool hit = false;
        foreach (int i in near)
        {
            var f = Features[i];
            if (!f.Stops(mounted)) continue;
            var ab = f.B - f.A;
            float k = Math.Clamp(Vector2.Dot(p - f.A, ab) / ab.LengthSquared(), 0, 1);
            var c = f.A + ab * k;
            var d = p - c;
            float rr = r + f.Half, l2 = d.LengthSquared();
            if (l2 >= rr * rr) continue;
            float l = MathF.Sqrt(l2);
            if (l < 1e-4f) { d = new Vector2(-ab.Y, ab.X); l = d.Length(); }
            p = c + d / l * rr;
            hit = true;
        }
        return hit;
    }

    /// A man walking `dir` who is pressed against a feature: the way along it to its nearer end (zero when he is not).
    /// Steering has no paths yet (T4), so this is how men find the gap at the end of a fence.
    public Vector2 GoRound(Vector2 p, float r, Vector2 dir, bool mounted = true)
    {
        if (Features.Count == 0 || !featureCells.TryGetValue(CellX(p.Y) * bucketSide + CellX(p.X), out var near)) return Vector2.Zero;
        foreach (int i in near)
        {
            var f = Features[i];
            if (!f.Stops(mounted)) continue;
            var ab = f.B - f.A;
            float len2 = ab.LengthSquared(), k = Math.Clamp(Vector2.Dot(p - f.A, ab) / len2, 0, 1);
            var c = f.A + ab * k;
            if (Vector2.DistanceSquared(p, c) > Sq(r + f.Half + 0.08f) || Vector2.Dot(c - p, dir) <= 0) continue; // not touching, or walking away
            var t = ab / MathF.Sqrt(len2);
            if (Vector2.Dot(t, dir) is var along && MathF.Abs(along) > 0.3f) return t * MathF.Sign(along); // already angling one way: keep on
            return k < 0.5f ? -t : t;
        }
        return Vector2.Zero;
    }

    /// Is there a house (a big solid thing) at p? Trunks and boulders are left to steering.
    public bool House(Vector2 p)
    {
        int bx = (int)((p.X + Half) / Cell), bz = (int)((p.Y + Half) / Cell);
        for (int z = bz - 1; z <= bz + 1; z++)
            for (int x = bx - 1; x <= bx + 1; x++)
            {
                if (x < 0 || z < 0 || x >= bucketSide || z >= bucketSide) continue;
                foreach (int i in buckets[z * bucketSide + x])
                    if (Foliage[i].Kind == FoliageKind.House && Vector2.Distance(Foliage[i].Pos, p) < Foliage[i].Radius + 0.6f) return true;
            }
        return false;
    }

    /// Does a short step from a to b cross a feature that stops this man (stakes stop only horse; pass false for arrows)?
    /// Only the features near b are looked at.
    public bool CrossesFeatureNear(Vector2 a, Vector2 b, bool mounted = true)
    {
        if (!featureCells.TryGetValue(CellX(b.Y) * bucketSide + CellX(b.X), out var near)) return false;
        foreach (int i in near) if (Features[i].Stops(mounted) && SegmentsCross(a, b, Features[i].A, Features[i].B)) return true;
        return false;
    }

    /// Does the straight line from a to b cross a feature that stops this man? (Pathing uses it.)
    public bool CrossesFeature(Vector2 a, Vector2 b, bool mounted = true)
    {
        foreach (var f in Features)
            if (f.Stops(mounted) && SegmentsCross(a, b, f.A, f.B)) return true;
        return false;
    }

    static bool SegmentsCross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
    {
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        Vector2 r = p2 - p, s = q2 - q;
        float den = Cross(r, s);
        if (MathF.Abs(den) < 1e-6f) return false;
        float t = Cross(q - p, s) / den, u = Cross(q - p, r) / den;
        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    // ---------- bridges ----------

    /// Where the field's road crosses its river, a timber bridge: the river is not carved there, and rails line both sides.
    void BridgeTheRoad()
    {
        if (Rivers.Count == 0 || IsFlat || !Params.Road) return;
        var river = Rivers[0];
        float Gap(float x) => RoadCenter(x) - RiverZ(river, x);
        float best = float.MaxValue;
        for (float x = -150; x < 150; x += 1)
            if (MathF.Sign(Gap(x)) != MathF.Sign(Gap(x + 1)) && MathF.Abs(x) < MathF.Abs(best)) best = x;
        if (best == float.MaxValue) return;
        float bx = best + Gap(best) / (Gap(best) - Gap(best + 1));
        var at = new Vector2(bx, RoadCenter(bx));
        var along = Vector2.Normalize(new Vector2(1, RoadCenter(bx + 0.5f) - RoadCenter(bx - 0.5f)));
        Rivers[0] = river with { Bridges = new[] { (at, along) } };
        var side = new Vector2(-along.Y, along.X) * (River.BridgeHalf + 0.1f);
        float reach = river.Width / 2 + 3;
        AddFeature(FeatureKind.Rail, at - along * reach + side, at + along * reach + side);
        AddFeature(FeatureKind.Rail, at - along * reach - side, at + along * reach - side);
    }

    /// The river's centre line at x (battlefield rivers run along X).
    static float RiverZ(River r, float x)
    {
        float i = Math.Clamp((x - r.Path[0].X) / (r.Path[1].X - r.Path[0].X), 0, r.Path.Length - 1.001f);
        int k = (int)i;
        return MathHelper.Lerp(r.Path[k].Y, r.Path[k + 1].Y, i - k);
    }
}
