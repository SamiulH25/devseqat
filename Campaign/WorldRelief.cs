using Microsoft.Xna.Framework;

namespace Ironfield;

/// Height from the design's uplands and cliffs (docs/pre-demo/WORLD_SCALE.md WS2; layout 6, the 4x draft). Headless.
/// An upland lifts the land inside its outline: a plateau a flat top on a short skirt, a massif peaks on a wide one, hills
/// rolling ground. A cliff is a step: on its high side (the left as the line runs, x east and z south) the land stands
/// `Height` higher right up to the line, easing back down over its reach inland (a scarp and its back slope); on the low
/// side nothing changes. Its height tapers off toward its two ends, so a gap in a cliff (a pass) is a ramp, not a wall end.
public sealed partial class WorldMap
{
    /// A cliff as the map carries it: its points (map units), height, reach of its back slope, and bounds.
    public sealed record CliffLine(Vector2[] Points, float Height, float Reach, float Length, Vector2 Min, Vector2 Max);
    /// An upland as the map carries it: its outline (map units), kind, height, skirt width and bounds.
    public sealed record UplandArea(Vector2[] Outline, string Kind, float Height, float Skirt, Vector2 Min, Vector2 Max);

    readonly List<CliffLine> cliffs = new();
    readonly List<UplandArea> uplands = new();
    public IReadOnlyList<CliffLine> Cliffs => cliffs;
    public IReadOnlyList<UplandArea> Uplands => uplands;

    /// How far a cliff's lift reaches back from its edge: four times its height, at least 40 units.
    public static float CliffReach(float height) => MathF.Max(40, height * 4);
    /// How wide an upland's skirt is (its rise from the plain to its full height).
    public static float SkirtOf(string kind) => kind switch { "plateau" => 30, "massif" => 140, _ => 90 };

    void DesignRelief(WorldDesign d)
    {
        foreach (var c in d.Cliffs)
        {
            var pts = c.Points.Select(Drawn).ToArray();
            float reach = CliffReach(c.Height), len = 0;
            for (int i = 1; i < pts.Length; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
            cliffs.Add(new CliffLine(pts, c.Height, reach, len, Bound(pts, -reach), Bound(pts, reach)));
        }
        foreach (var u in d.Uplands)
        {
            var pts = u.Points.Select(Drawn).ToArray();
            uplands.Add(new UplandArea(pts, u.Kind, u.Height, SkirtOf(u.Kind), Bound(pts, -RagAmp - 1), Bound(pts, RagAmp + 1)));
        }
    }

    static Vector2 Bound(Vector2[] pts, float pad) => pad < 0
        ? new Vector2(pts.Min(p => p.X), pts.Min(p => p.Y)) + new Vector2(pad)
        : new Vector2(pts.Max(p => p.X), pts.Max(p => p.Y)) + new Vector2(pad);

    /// How far (each axis) a point is pushed before the uplands and cliffs are read, so drawn straight edges come out ragged.
    public const float RagAmp = 8;

    /// Where (x, z) is looked up in the relief: the point moved by a smooth noise (uplands and cliffs both, so a cliff stays on
    /// its plateau's rim). Anything asking "which side of a cliff" (WS3) asks it of this point.
    public Vector2 Ragged(float x, float z) => new(
        x + Terrain.Noise(x * 0.012f, z * 0.012f, Seed + 31) * RagAmp,
        z + Terrain.Noise(x * 0.012f, z * 0.012f, Seed + 32) * RagAmp);

    /// Terrain's noise (-1 to 1) as 0 to 1.
    static float Unit01(float x, float z, int seed) => (Terrain.Noise(x, z, seed) + 1) * 0.5f;

    /// How much the uplands and cliffs lift the land at (x, z).
    public float Relief(float x, float z)
    {
        var p = Ragged(x, z);
        float h = 0;
        foreach (var u in uplands)
        {
            if (x < u.Min.X || z < u.Min.Y || x > u.Max.X || z > u.Max.Y) continue;
            float d = InsideDistance(p, u.Outline);
            if (d <= 0) continue;
            float rise = MathHelper.SmoothStep(0, 1, Math.Clamp(d / u.Skirt, 0, 1));
            h += u.Kind switch
            {
                // A plateau's top undulates a little; a massif's peaks and hills' swells are a few hundred units apart.
                "plateau" => u.Height * rise * (0.94f + 0.12f * Unit01(x * 0.008f, z * 0.008f, Seed + 21)),
                "massif" => u.Height * rise * (0.45f + 0.8f * (1 - MathF.Abs(Terrain.Noise(x * 0.01f, z * 0.01f, Seed + 22)))),
                _ => u.Height * rise * (0.55f + 0.6f * Unit01(x * 0.006f, z * 0.006f, Seed + 23)),
            };
        }
        foreach (var c in cliffs)
        {
            if (x < c.Min.X || z < c.Min.Y || x > c.Max.X || z > c.Max.Y) continue;
            h += CliffLift(p, c);
        }
        return h;
    }

    /// One cliff's lift at p: full height at the edge on the high side, easing out over its reach; none on the low side.
    static float CliffLift(Vector2 p, CliffLine c)
    {
        var (best, side, along) = Nearest(p, c);
        if (best > c.Reach) return 0;
        float step = MathHelper.SmoothStep(0, 1, Math.Clamp((side + 1) / 2, 0, 1)); // a step a couple of units wide at the edge
        float back = 1 - MathHelper.SmoothStep(0, 1, Math.Clamp((best - c.Reach * 0.25f) / (c.Reach * 0.75f), 0, 1));
        return c.Height * step * back * Taper(c, along);
    }

    /// How much of a cliff's height stands `along` units from its start: none at its ends, all of it past half its reach.
    static float Taper(CliffLine c, float along)
    {
        float ends = MathF.Min(c.Reach * 0.5f, c.Length / 3);
        return ends <= 0 ? 1 : MathHelper.SmoothStep(0, 1, Math.Clamp(along / ends, 0, 1)) * MathHelper.SmoothStep(0, 1, Math.Clamp((c.Length - along) / ends, 0, 1));
    }

    /// How much a climb off the road slows a party at p (WS3): 1 on the flat (a plateau's top too), down to 0.45 on an
    /// upland's skirt or a scarp's back slope. Only the uplands' and cliffs' slope counts (the old land is priced by its kind).
    public float Climb(Vector2 p)
    {
        if (cliffs.Count + uplands.Count == 0) return 1;
        const float d = 4;
        float gx = Relief(p.X + d, p.Y) - Relief(p.X - d, p.Y), gz = Relief(p.X, p.Y + d) - Relief(p.X, p.Y - d);
        float grade = MathF.Sqrt(gx * gx + gz * gz) / (2 * d);
        return MathF.Max(0.45f, 1 / (1 + 4 * grade));
    }

    /// The share of its height at which a cliff bars the way: below it, near the ends, it is a ramp a party walks up.
    public const float BarredFrom = 0.35f;

    /// Is p on a cliff's face (WS3): within `half` of the line (read at the ragged point, as the heights are) where the step
    /// still stands at least `BarredFrom` of its height. Nothing crosses such ground; a pass is the gap between two cliffs.
    public bool OnCliff(Vector2 p, float half)
    {
        if (cliffs.Count == 0) return false;
        var q = Ragged(p.X, p.Y);
        foreach (var c in cliffs)
        {
            if (q.X < c.Min.X || q.Y < c.Min.Y || q.X > c.Max.X || q.Y > c.Max.Y) continue;
            var (d, _, along) = Nearest(q, c);
            if (d < half && Taper(c, along) >= BarredFrom) return true;
        }
        return false;
    }

    /// Does the straight way a-b climb a cliff? (Sampled every 2 units against a band 6 wide.)
    public bool CrossesCliff(Vector2 a, Vector2 b)
    {
        if (cliffs.Count == 0) return false;
        int k = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / 2));
        for (int i = 0; i <= k; i++) if (OnCliff(Vector2.Lerp(a, b, i / (float)k), 3)) return true;
        return false;
    }

    /// The nearest point of a cliff's line to p: how far, how far to the high side (left of the run; past an end, the end
    /// segment's side), and how far along the line it lies.
    static (float Dist, float Side, float Along) Nearest(Vector2 p, CliffLine c)
    {
        float best = float.MaxValue, side = 0, along = 0, run = 0;
        for (int i = 1; i < c.Points.Length; i++)
        {
            Vector2 a = c.Points[i - 1], b = c.Points[i], ab = b - a;
            float l2 = ab.LengthSquared(), seg = MathF.Sqrt(l2);
            float t = l2 > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0, 1) : 0;
            float d = Vector2.Distance(p, a + ab * t);
            if (d < best)
            {
                best = d;
                along = run + t * seg;
                // Left of the run (z south): the normal (ab.Y, -ab.X). Past an end, the side is the end segment's.
                side = seg > 0 ? Vector2.Dot(p - a, new Vector2(ab.Y, -ab.X)) / seg : 0;
            }
            run += seg;
        }
        return (best, side, along);
    }

    /// How far inside an outline p lies (0 or less outside).
    static float InsideDistance(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        float best = float.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            var ab = b - a;
            float l2 = ab.LengthSquared(), t = l2 > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0, 1) : 0;
            best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return inside ? best : -best;
    }
}
