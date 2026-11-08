using Microsoft.Xna.Framework;

namespace Ironfield;

public enum FoliageKind { Broadleaf, Pine, Bush, Rock, Town, Castle, Village, Wall, Tower, Gate, Ladder, House, Wagon,
    // Sites on the campaign map (docs/SIDE_CONTENT.md S2).
    Ruin, Shrine, Stones, Field, Mine, Hut, Watch, Spring, Mill }

/// A river (roadmap C5): a centre line, its width, and the fords where it runs shallow.
public sealed record River(Vector2[] Path, float Width, Vector2[] Fords, float Depth = 1.3f)
{
    public const float FordRadius = 7, BridgeHalf = 2.4f;
    /// Bridges on a battlefield river: where each crosses and the way its deck runs (TerrainFeatures.cs `BridgeTheRoad`).
    public (Vector2 At, Vector2 Along)[] Bridges { get; init; } = Array.Empty<(Vector2, Vector2)>();

    /// On a bridge's deck (within its half-width of the road, and no further along it than the banks).
    public bool AtBridge(Vector2 p)
    {
        foreach (var (at, along) in Bridges)
        {
            var d = p - at;
            if (MathF.Abs(Vector2.Dot(d, along)) < Width / 2 + 3 && MathF.Abs(d.X * along.Y - d.Y * along.X) < BridgeHalf) return true;
        }
        return false;
    }

    /// Distance from p to the river's centre line.
    public float Dist(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < Path.Length; i++)
        {
            var a = Path[i]; var ab = Path[i + 1] - a;
            if (MathF.Abs(p.X - a.X) > 40 && MathF.Abs(p.X - Path[i + 1].X) > 40 && MathF.Abs(p.Y - a.Y) > 40 && MathF.Abs(p.Y - Path[i + 1].Y) > 40) continue;
            float k = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, a + ab * k));
        }
        return best;
    }

    public bool AtFord(Vector2 p) => Fords.Any(f => Vector2.DistanceSquared(f, p) < FordRadius * FordRadius);

    /// How far the bed is cut below the banks here (shallow at a ford).
    public float Carve(Vector2 p, float dist) => AtBridge(p) ? 0 : (AtFord(p) ? 0.35f : Depth) * Smooth(Width / 2 + 3, Width / 2 - 1, dist);

    static float Smooth(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

    /// A battlefield river across the field between the armies, with two fords.
    public static River Across(Random rng)
    {
        float z0 = rng.NextSingle() * 8 - 4, phase = rng.NextSingle() * MathF.Tau, amp = 5 + rng.NextSingle() * 5;
        var path = Enumerable.Range(0, 131).Select(i => { float x = -260 + i * 4; return new Vector2(x, z0 + MathF.Sin(x * 0.02f + phase) * amp); }).ToArray();
        float f1 = -20 - rng.NextSingle() * 15, f2 = 15 + rng.NextSingle() * 15;
        Vector2 At(float x) => new(x, z0 + MathF.Sin(x * 0.02f + phase) * amp);
        return new River(path, 9, new[] { At(f1), At(f2) });
    }
}

/// A town wall across the field for an assault (roadmap B1). It runs the whole width at z = 14..16.5 (a walkway on raised
/// ground, faced in stone toward the attackers, with an earth bank behind it), has a gate near x = 0 that attackers break
/// down, and ladders where men on foot can climb. The heights are part of the terrain, so units stand on the walkway and
/// arrows strike the wall's face; the face itself is a barrier in Battle.Move.
public sealed class SiegeWall
{
    public const float Outer = 11.5f, Top0 = 14, Top1 = 16.5f, Inner = 19, Height = 4.5f, LadderHalf = 1.3f;
    public const float GateX = 0.25f, GateHalf = 2.75f;
    public readonly float[] Ladders;
    public float GateHp, GateMax;
    public bool GateOpen => GateHp <= 0;

    public SiegeWall(int ladders, float gateHp)
    {
        Ladders = ladders switch { <= 0 => Array.Empty<float>(), 1 => new[] { -22f }, 2 => new[] { -24f, 22f }, _ => new[] { -36f, -16f, 18f, 38f } };
        GateHp = GateMax = gateHp;
    }

    public bool AtGate(float x) => MathF.Abs(x - GateX) < GateHalf;
    public bool AtLadder(float x) => Ladders.Any(l => MathF.Abs(x - l) < LadderHalf);
    /// Inside the walls (on the walkway or in the town).
    public static bool Inside(Vector2 p) => p.Y >= Outer;
    /// Height the wall adds at a terrain vertex (vertices sit every 2.5 m; 14 and 16.5 are raised).
    public float Raise(float x, float z) => z > 13 && z < 17.5f && !AtGate(x) ? Height : 0;
}

/// One piece of foliage (or, on the campaign map, a settlement). Radius > 0 = a solid obstacle (tree trunk, boulder).
public readonly record struct Foliage(FoliageKind Kind, Vector2 Pos, float Scale, float Radius, int Seed);

/// How a battlefield is generated: set from the campaign map cell where the fight happens (docs/CAMPAIGN.md 3.2).
/// `Layouts` are the field shapes this ground may take (docs/TERRAIN.md T2; null = open field), picked by the seed.
/// `Scale` grows the clear deployment ground and the flank woods with the size of the battle (1 up to 450 men).
public sealed record TerrainParams(string Name, int HillsMin, int HillsMax, float HillHeight, float Roll, float FlankWoodDist,
    int Woods, int LoneTrees, int Bushes, bool Road, bool River = false, FieldLayout[]? Layouts = null, float Scale = 1)
{
    public static readonly TerrainParams Meadow = new("Meadow", 3, 5, 1f, 1f, 64, 11, 60, 220, true, Layouts: new[] { FieldLayout.Open, FieldLayout.Open, FieldLayout.Ridge, FieldLayout.WoodedFlank, FieldLayout.Village, FieldLayout.RiverLine });
    public static readonly TerrainParams Farmland = new("Farmland", 1, 3, 0.7f, 0.6f, 70, 6, 30, 380, true, Layouts: new[] { FieldLayout.Hedgerows, FieldLayout.Hedgerows, FieldLayout.Village, FieldLayout.Open });
    public static readonly TerrainParams Forest = new("Forest", 2, 4, 0.9f, 1f, 55, 22, 150, 320, false, Layouts: new[] { FieldLayout.ForestClearing, FieldLayout.WoodedFlank });
    public static readonly TerrainParams Hills = new("Hills", 5, 7, 1.7f, 1.4f, 66, 9, 50, 200, false, Layouts: new[] { FieldLayout.Ridge, FieldLayout.TwoHills, FieldLayout.Pass });
    /// Fens: low and wet (a stream always runs through), thick with bushes and alders, soft mud underfoot.
    public static readonly TerrainParams Marsh = new("Marsh", 1, 2, 0.5f, 0.5f, 52, 14, 90, 520, false, true, new[] { FieldLayout.Fen });
    /// Steppe: open grass to the horizon, gentle swells, hardly a tree (room for horse).
    public static readonly TerrainParams Steppe = new("Steppe", 2, 3, 0.8f, 1.2f, 96, 1, 6, 60, false);
    /// Badlands: dry, broken ground with tall knolls, stones and scrub, and little cover.
    public static readonly TerrainParams Badlands = new("Badlands", 6, 8, 2.1f, 1.6f, 80, 2, 14, 90, false, Layouts: new[] { FieldLayout.Broken, FieldLayout.Broken, FieldLayout.TwoHills, FieldLayout.Pass });
    /// The tournament field (S5): flat, open, a few trees at the edge.
    public static readonly TerrainParams Lists = new("Lists", 0, 0, 0.2f, 0.3f, 90, 0, 6, 30, false);

    /// A preset by name (IRONFIELD_GROUND), or null.
    public static TerrainParams? Named(string name) =>
        new[] { Meadow, Farmland, Forest, Hills, Marsh, Steppe, Badlands, Lists }.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// Headless battlefield ground: a heightmap (gentle rolling meadow, a few hills, a rim of high ground,
/// a dirt road through the middle) and the placement of trees, bushes and rocks.
/// Heights are sampled per triangle exactly as the rendered mesh is split, so units stand on the drawn ground.
public sealed partial class Terrain
{
    /// A battlefield: +-256 m of ground on 2.5 m vertices. The campaign map is much bigger and coarser (WorldMap.WorldShape).
    public const float BattleHalf = 256, BattleStep = 2.5f;
    public const int BattleN = 205; // vertices per side = 2 * BattleHalf / BattleStep + 1
    const float Cell = 8;           // the obstacle grid's cell size

    public readonly int Seed;
    public readonly bool IsFlat;
    public readonly List<Foliage> Foliage = new();
    /// The ground's size: half its side, the spacing of its vertices, and the vertices per side (2 * Half / Step + 1).
    public readonly float Half, Step;
    public readonly int N;
    readonly float[] grid;
    readonly List<int>[] buckets;
    readonly int bucketSide;
    readonly List<(Vector2 C, float R, float H)> hills = new();
    readonly float roadZ, roadPhase, roadAmp;
    public readonly TerrainParams Params = TerrainParams.Meadow;
    // The campaign map supplies its own shape.
    readonly Func<float, float, float>? customHeight;
    readonly Func<Vector2, float>? customRoad;
    /// Optional ground colour multiplier (the campaign map's fields and forest floors). Visual only.
    public readonly Func<Vector2, Vector3, Vector3>? Tint; // (place, ground colour) -> the colour drawn
    /// Grass tufts and the minimap's reach (the campaign map has no grass and a wider overview).
    public readonly bool Grass = true;
    public readonly float MinimapRange = 110;
    /// How many metres one minimap texel covers (the renderer sizes the image from this).
    public const float MinimapMetresPerTexel = 2f;

    /// The town wall in an assault (null in field battles).
    public readonly SiegeWall? Wall;
    /// Rivers on this ground (a battlefield has at most one; the campaign map has a few).
    public readonly List<River> Rivers = new();

    /// How deep the water is at p (0 on dry land; shallow at a ford).
    public float WaterDepth(Vector2 p)
    {
        foreach (var r in Rivers)
        {
            float d = r.Dist(p);
            if (d < r.Width / 2) return r.AtBridge(p) ? 0 : r.AtFord(p) ? 0.3f : r.Depth;
        }
        return 0;
    }

    public static Terrain Flat() => new(0, true, TerrainParams.Meadow);
    public Terrain(int seed) : this(seed, false, TerrainParams.Meadow) { }
    public Terrain(int seed, TerrainParams p, SiegeWall? wall = null) : this(seed, false, p, wall) { }

    /// A campaign map: heights, roads, foliage and settlements all given; no grass, overview covers everything.
    /// `extent` is half the side of the ground (WorldMap.Extent); `step` its vertex spacing.
    public Terrain(int seed, float extent, float step, Func<float, float, float> height, Func<Vector2, float> roadDist, IEnumerable<Foliage> foliage, Func<Vector2, Vector3, Vector3>? tint, IEnumerable<River>? rivers = null)
    {
        if (rivers != null) Rivers.AddRange(rivers);
        Seed = seed;
        Half = extent;
        Step = step;
        N = (int)MathF.Round(2 * Half / Step) + 1;
        bucketSide = Math.Max(1, (int)(2 * Half / Cell));
        grid = new float[N * N];
        buckets = new List<int>[bucketSide * bucketSide];
        customHeight = height;
        customRoad = roadDist;
        Tint = tint;
        Grass = false;
        MinimapRange = Half;
        for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                grid[z * N + x] = RawHeight(-Half + x * Step, -Half + z * Step);
        foreach (var f in foliage) Add(f);
    }

    Terrain(int seed, bool flat, TerrainParams p, SiegeWall? wall = null)
    {
        Seed = seed;
        IsFlat = flat;
        Layout = wall != null || flat || p.Layouts is not { Length: > 0 } ? FieldLayout.Open : p.Layouts[new Random(seed + 17).Next(p.Layouts.Length)];
        Params = wall != null ? p with { Road = false } : Layout == FieldLayout.RiverLine ? p with { Road = true, River = true } : p;
        Wall = wall;
        p = Params;
        Half = BattleHalf;
        Step = BattleStep;
        N = BattleN;
        bucketSide = Math.Max(1, (int)(2 * Half / Cell));
        grid = new float[N * N];
        buckets = new List<int>[bucketSide * bucketSide];
        if (p.River && wall == null) Rivers.Add(River.Across(new Random(seed + 5)));
        for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
        if (flat) return;

        var rng = new Random(seed);
        roadZ = rng.NextSingle() * 12 - 6;
        roadPhase = rng.NextSingle() * MathF.Tau;
        roadAmp = 8 + rng.NextSingle() * 6;
        int hillCount = p.HillsMin + rng.Next(p.HillsMax - p.HillsMin + 1);
        for (int i = 0; i < hillCount; i++)
        {
            float a = rng.NextSingle() * MathF.Tau, d = (30 + rng.NextSingle() * 70) * p.Scale;
            hills.Add((new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, (18 + rng.NextSingle() * 17) * MathF.Sqrt(p.Scale), (2.5f + rng.NextSingle() * 3.5f) * p.HillHeight));
        }
        BridgeTheRoad();
        ShapeLayout(new Random(seed + 18));
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                grid[z * N + x] = RawHeight(-Half + x * Step, -Half + z * Step);
        PlaceFoliage(new Random(seed + 99));
        PaintGround();
    }

    // ---------- heights ----------

    float RawHeight(float x, float z)
    {
        if (customHeight != null) return customHeight(x, z);
        float r = MathF.Sqrt(x * x + z * z);
        // Rolling ground, calmer where the armies meet.
        float roll = Noise(x * 0.011f, z * 0.011f, Seed) * 3.5f + Noise(x * 0.027f, z * 0.027f, Seed + 1) * 1.3f
                   + Noise(x * 0.07f, z * 0.07f, Seed + 2) * 0.35f;
        float h = roll * Params.Roll * (0.55f + 0.45f * SmoothStep(30, 120, r));
        foreach (var (c, hr, hh) in hills)
        {
            float d = Vector2.Distance(new Vector2(x, z), c) / hr;
            if (d < 1) h += hh * (1 - d * d) * (1 - d * d);
        }
        h += LayoutHeight(x, z);
        // High ground all round frames the valley (and the horizon).
        h += SmoothStep(120, 240, r) * (14 + 10 * Noise(x * 0.008f, z * 0.008f, Seed + 3));
        float rd = RoadDist(new Vector2(x, z));
        if (rd < 5) h -= 0.25f * (1 - rd / 5); // worn a little into the ground
        foreach (var river in Rivers)
        {
            var at = new Vector2(x, z);
            float d = river.Dist(at);
            if (d < river.Width / 2 + 3) h -= river.Carve(at, d);
        }
        if (Wall != null)
        {
            h = MathHelper.Lerp(h, 0.4f * h, SmoothStep(40, 0, MathF.Abs(z - 15))); // level ground along the wall
            h += Wall.Raise(x, z);
        }
        return h;
    }

    public float RoadCenter(float x) => roadZ + MathF.Sin(x * 0.018f + roadPhase) * roadAmp;

    /// Approximate distance to the road's centre line (infinite when there is no road).
    public float RoadDist(Vector2 p)
    {
        if (customRoad != null) return customRoad(p);
        if (IsFlat || !Params.Road) return float.MaxValue;
        float slope = 0.018f * roadAmp * MathF.Cos(p.X * 0.018f + roadPhase);
        return MathF.Abs(p.Y - RoadCenter(p.X)) / MathF.Sqrt(1 + slope * slope);
    }

    /// Ground height, interpolated on the same triangles the renderer draws.
    public float Height(Vector2 p)
    {
        if (IsFlat) return 0;
        float gx = Math.Clamp((p.X + Half) / Step, 0, N - 1.001f), gz = Math.Clamp((p.Y + Half) / Step, 0, N - 1.001f);
        int x = (int)gx, z = (int)gz;
        float fx = gx - x, fz = gz - z;
        float h00 = grid[z * N + x], h10 = grid[z * N + x + 1], h01 = grid[(z + 1) * N + x], h11 = grid[(z + 1) * N + x + 1];
        return fx + fz <= 1
            ? h00 + (h10 - h00) * fx + (h01 - h00) * fz
            : h11 + (h01 - h11) * (1 - fx) + (h10 - h11) * (1 - fz);
    }

    public float Height(float x, float z) => Height(new Vector2(x, z));
    public float GridHeight(int x, int z) => grid[z * N + x];

    /// Slope as (dh/dx, dh/dz): rise per metre.
    public Vector2 Gradient(Vector2 p)
    {
        if (IsFlat) return Vector2.Zero;
        const float e = 0.75f;
        return new Vector2(Height(p + new Vector2(e, 0)) - Height(p - new Vector2(e, 0)),
                           Height(p + new Vector2(0, e)) - Height(p - new Vector2(0, e))) / (2 * e);
    }

    // ---------- obstacles ----------

    int BucketOf(Vector2 p)
    {
        int x = Math.Clamp((int)((p.X + Half) / Cell), 0, bucketSide - 1), z = Math.Clamp((int)((p.Y + Half) / Cell), 0, bucketSide - 1);
        return z * bucketSide + x;
    }

    /// A prop placed after generation (the battle's supply wagons).
    public void AddProp(Foliage f) => Add(f);

    void Add(Foliage f)
    {
        Foliage.Add(f);
        if (f.Radius > 0) buckets[BucketOf(f.Pos)].Add(Foliage.Count - 1);
    }

    /// True if a solid obstacle comes within `within` of p (measured from its edge).
    bool Blocked(Vector2 p, float within)
    {
        int bx = (int)((p.X + Half) / Cell), bz = (int)((p.Y + Half) / Cell);
        for (int z = bz - 1; z <= bz + 1; z++)
            for (int x = bx - 1; x <= bx + 1; x++)
            {
                if (x < 0 || z < 0 || x >= bucketSide || z >= bucketSide) continue;
                foreach (int i in buckets[z * bucketSide + x])
                    if (Vector2.Distance(Foliage[i].Pos, p) < Foliage[i].Radius + within) return true;
            }
        return false;
    }

    /// Push a circle of radius r out of any trunk or boulder it overlaps. Returns true if it hit something.
    public bool PushOut(ref Vector2 p, float r)
    {
        bool hit = PushOutFeatures(ref p, r);
        if (IsFlat) return hit;
        int bx = (int)((p.X + Half) / Cell), bz = (int)((p.Y + Half) / Cell);
        for (int z = bz - 1; z <= bz + 1; z++)
            for (int x = bx - 1; x <= bx + 1; x++)
            {
                if (x < 0 || z < 0 || x >= bucketSide || z >= bucketSide) continue;
                foreach (int i in buckets[z * bucketSide + x])
                {
                    var f = Foliage[i];
                    float rr = f.Radius + r;
                    var d = p - f.Pos;
                    float l2 = d.LengthSquared();
                    if (l2 >= rr * rr) continue;
                    float l = MathF.Sqrt(l2);
                    if (l < 1e-4f) { d = Vector2.UnitX; l = 1; }
                    p = f.Pos + d / l * rr;
                    hit = true;
                }
            }
        return hit;
    }

    /// The open ground where the armies deploy and meet: no trees or boulders here. It grows with the battle (`Scale`).
    public bool Core(Vector2 p) => MathF.Abs(p.X) < 50 * Params.Scale && MathF.Abs(p.Y) < 54 * Params.Scale;

    void PlaceFoliage(Random rng)
    {
        bool Ok(Vector2 p, float space) => !Core(p) && RoadDist(p) > 4 && WaterDepth(p) == 0 && Rivers.All(r => r.Dist(p) > r.Width / 2 + 2) && MathF.Abs(p.X) < Half - 8 && MathF.Abs(p.Y) < Half - 8 && !Blocked(p, space)
            && (Wall == null || p.Y < SiegeWall.Outer - 6 || p.Y > 70);
        if (Wall != null) PlaceTown(rng);
        if (Layout == FieldLayout.Village) PlaceVillage(new Random(Seed + 19));
        void Wood(Vector2 c, float r)
        {
            woods.Add((c, r));
            bool pines = rng.NextSingle() < 0.4f;
            int attempts = (int)(r * r * 0.25f);
            for (int i = 0; i < attempts; i++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * r;
                var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (!Ok(p, 2.4f)) continue;
                float s = 0.8f + rng.NextSingle() * 0.5f;
                bool pine = pines ? rng.NextSingle() < 0.85f : Height(p) > 7 || rng.NextSingle() < 0.15f;
                Add(new Foliage(pine ? FoliageKind.Pine : FoliageKind.Broadleaf, p, s, 0.32f * s, rng.Next()));
            }
        }

        // Two woods flank the field: cavalry has to ride round them.
        for (int side = -1; side <= 1; side += 2)
            Wood(new Vector2(side * (Params.FlankWoodDist * Params.Scale + rng.NextSingle() * 8), rng.NextSingle() * 36 - 18), 13 + rng.NextSingle() * 4);
        foreach (var (c, r) in layoutWoods) Wood(c, r);
        for (int i = 0; i < Params.Woods; i++)
        {
            float a = rng.NextSingle() * MathF.Tau, d = 80 + rng.NextSingle() * 130;
            var c = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (!Core(c)) Wood(c, 12 + rng.NextSingle() * 16);
        }
        for (int i = 0; i < Params.LoneTrees; i++) // lone trees
        {
            var p = new Vector2(rng.NextSingle() * 440 - 220, rng.NextSingle() * 440 - 220);
            if (!Ok(p, 3)) continue;
            float s = 0.9f + rng.NextSingle() * 0.5f;
            Add(new Foliage(rng.NextSingle() < 0.3f ? FoliageKind.Pine : FoliageKind.Broadleaf, p, s, 0.32f * s, rng.Next()));
        }
        for (int i = 0; i < Params.Bushes; i++) // bushes: no collision; many hug the edges of the woods
        {
            Vector2 p;
            if (i % 2 == 0 && woods.Count > 0)
            {
                var (c, r) = woods[rng.Next(woods.Count)];
                float a = rng.NextSingle() * MathF.Tau;
                p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (r + rng.NextSingle() * 4 - 1);
            }
            else
            {
                float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * 170;
                p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            }
            if (RoadDist(p) < 3 || Blocked(p, 0.5f)) continue;
            Add(new Foliage(FoliageKind.Bush, p, 0.7f + rng.NextSingle() * 0.6f, 0, rng.Next()));
        }
        if (Layout == FieldLayout.Hedgerows) PlaceHedges(new Random(Seed + 20));
        for (int i = 0; i < 90; i++) // pebbles and small rocks
        {
            float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * 180;
            var p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (RoadDist(p) < 3 || Blocked(p, 0.3f)) continue;
            Add(new Foliage(FoliageKind.Rock, p, 0.25f + rng.NextSingle() * 0.35f, 0, rng.Next()));
        }
        for (int i = 0; i < 30; i++) // boulders
        {
            var p = new Vector2(rng.NextSingle() * 400 - 200, rng.NextSingle() * 400 - 200);
            float s = 0.9f + rng.NextSingle() * 1.1f;
            if (!Ok(p, s)) continue;
            Add(new Foliage(FoliageKind.Rock, p, s, 0.75f * s, rng.Next()));
        }
    }

    /// The wall's stones, towers, gate and ladders, and the houses of the town behind it.
    void PlaceTown(Random rng)
    {
        var w = Wall!;
        for (float x = -250; x < 250; x += 5)
            if (!w.AtGate(x + 2.5f) && !w.AtGate(x) && !w.AtGate(x + 5)) Add(new Foliage(FoliageKind.Wall, new Vector2(x + 2.5f, 15.25f), 5, 0, rng.Next()));
        // The wall either side of the gate stops at the gatehouse towers.
        Add(new Foliage(FoliageKind.Wall, new Vector2(SiegeWall.GateX - 4.6f, 15.25f), 3.7f, 0, rng.Next()));
        Add(new Foliage(FoliageKind.Wall, new Vector2(SiegeWall.GateX + 4.6f, 15.25f), 3.7f, 0, rng.Next()));
        foreach (float x in new[] { SiegeWall.GateX - 5.2f, SiegeWall.GateX + 5.2f, -60, -30, 30, 60, -95, 95 })
            Add(new Foliage(FoliageKind.Tower, new Vector2(x, 15.25f), 1, 0, rng.Next()));
        Add(new Foliage(FoliageKind.Gate, new Vector2(SiegeWall.GateX, SiegeWall.Outer + 0.6f), 1, 0, rng.Next()));
        foreach (float x in w.Ladders) Add(new Foliage(FoliageKind.Ladder, new Vector2(x, SiegeWall.Outer), 1, 0, rng.Next()));
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector2(rng.NextSingle() * 150 - 75, 34 + rng.NextSingle() * 40);
            if (MathF.Abs(p.X - SiegeWall.GateX) < 9 || Blocked(p, 5)) continue;
            Add(new Foliage(FoliageKind.House, p, 1 + rng.NextSingle() * 0.4f, 2.6f, rng.Next()));
        }
    }

    // ---------- noise ----------

    static float SmoothStep(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF * 2 - 1;
        }
    }

    /// Smooth value noise in [-1, 1].
    public static float Noise(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }
}
