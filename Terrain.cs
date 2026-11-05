using Microsoft.Xna.Framework;

namespace Ironfield;

public enum FoliageKind { Broadleaf, Pine, Bush, Rock, Town, Castle, Village }

/// One piece of foliage (or, on the campaign map, a settlement). Radius > 0 = a solid obstacle (tree trunk, boulder).
public readonly record struct Foliage(FoliageKind Kind, Vector2 Pos, float Scale, float Radius, int Seed);

/// How a battlefield is generated: set from the campaign map cell where the fight happens (docs/CAMPAIGN.md 3.2).
public sealed record TerrainParams(string Name, int HillsMin, int HillsMax, float HillHeight, float Roll, float FlankWoodDist,
    int Woods, int LoneTrees, int Bushes, bool Road)
{
    public static readonly TerrainParams Meadow = new("Meadow", 3, 5, 1f, 1f, 64, 11, 60, 220, true);
    public static readonly TerrainParams Farmland = new("Farmland", 1, 3, 0.7f, 0.6f, 70, 6, 30, 380, true);
    public static readonly TerrainParams Forest = new("Forest", 2, 4, 0.9f, 1f, 55, 22, 150, 320, false);
    public static readonly TerrainParams Hills = new("Hills", 5, 7, 1.7f, 1.4f, 66, 9, 50, 200, false);
}

/// Headless battlefield ground: a heightmap (gentle rolling meadow, a few hills, a rim of high ground,
/// a dirt road through the middle) and the placement of trees, bushes and rocks.
/// Heights are sampled per triangle exactly as the rendered mesh is split, so units stand on the drawn ground.
public sealed class Terrain
{
    public const float Half = 256, Step = 2.5f;
    public const int N = 205; // vertices per side = 2 * Half / Step + 1
    const float Cell = 8;
    const int Buckets = (int)(2 * Half / Cell);

    public readonly int Seed;
    public readonly bool IsFlat;
    public readonly List<Foliage> Foliage = new();
    readonly float[] grid = new float[N * N];
    readonly List<int>[] buckets = new List<int>[Buckets * Buckets];
    readonly List<(Vector2 C, float R, float H)> hills = new();
    readonly float roadZ, roadPhase, roadAmp;
    public readonly TerrainParams Params = TerrainParams.Meadow;
    // The campaign map supplies its own shape.
    readonly Func<float, float, float>? customHeight;
    readonly Func<Vector2, float>? customRoad;
    /// Optional ground colour multiplier (the campaign map's fields and forest floors). Visual only.
    public readonly Func<Vector2, Vector3>? Tint;
    /// Grass tufts and the minimap's reach (the campaign map has no grass and a wider overview).
    public readonly bool Grass = true;
    public readonly float MinimapRange = 110;

    public static Terrain Flat() => new(0, true, TerrainParams.Meadow);
    public Terrain(int seed) : this(seed, false, TerrainParams.Meadow) { }
    public Terrain(int seed, TerrainParams p) : this(seed, false, p) { }

    /// A campaign map: heights, roads, foliage and settlements all given; no grass, overview covers everything.
    public Terrain(int seed, Func<float, float, float> height, Func<Vector2, float> roadDist, IEnumerable<Foliage> foliage, Func<Vector2, Vector3>? tint)
    {
        Seed = seed;
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

    Terrain(int seed, bool flat, TerrainParams p)
    {
        Seed = seed;
        IsFlat = flat;
        Params = p;
        for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
        if (flat) return;

        var rng = new Random(seed);
        roadZ = rng.NextSingle() * 12 - 6;
        roadPhase = rng.NextSingle() * MathF.Tau;
        roadAmp = 8 + rng.NextSingle() * 6;
        int hillCount = p.HillsMin + rng.Next(p.HillsMax - p.HillsMin + 1);
        for (int i = 0; i < hillCount; i++)
        {
            float a = rng.NextSingle() * MathF.Tau, d = 30 + rng.NextSingle() * 70;
            hills.Add((new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, 18 + rng.NextSingle() * 17, (2.5f + rng.NextSingle() * 3.5f) * p.HillHeight));
        }
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                grid[z * N + x] = RawHeight(-Half + x * Step, -Half + z * Step);
        PlaceFoliage(new Random(seed + 99));
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
        // High ground all round frames the valley (and the horizon).
        h += SmoothStep(120, 240, r) * (14 + 10 * Noise(x * 0.008f, z * 0.008f, Seed + 3));
        float rd = RoadDist(new Vector2(x, z));
        if (rd < 5) h -= 0.25f * (1 - rd / 5); // worn a little into the ground
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
        int x = Math.Clamp((int)((p.X + Half) / Cell), 0, Buckets - 1), z = Math.Clamp((int)((p.Y + Half) / Cell), 0, Buckets - 1);
        return z * Buckets + x;
    }

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
                if (x < 0 || z < 0 || x >= Buckets || z >= Buckets) continue;
                foreach (int i in buckets[z * Buckets + x])
                    if (Vector2.Distance(Foliage[i].Pos, p) < Foliage[i].Radius + within) return true;
            }
        return false;
    }

    /// Push a circle of radius r out of any trunk or boulder it overlaps. Returns true if it hit something.
    public bool PushOut(ref Vector2 p, float r)
    {
        if (IsFlat) return false;
        bool hit = false;
        int bx = (int)((p.X + Half) / Cell), bz = (int)((p.Y + Half) / Cell);
        for (int z = bz - 1; z <= bz + 1; z++)
            for (int x = bx - 1; x <= bx + 1; x++)
            {
                if (x < 0 || z < 0 || x >= Buckets || z >= Buckets) continue;
                foreach (int i in buckets[z * Buckets + x])
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

    /// The open ground where the armies deploy and meet: no trees or boulders here.
    public static bool Core(Vector2 p) => MathF.Abs(p.X) < 50 && MathF.Abs(p.Y) < 54;

    void PlaceFoliage(Random rng)
    {
        bool Ok(Vector2 p, float space) => !Core(p) && RoadDist(p) > 4 && MathF.Abs(p.X) < Half - 8 && MathF.Abs(p.Y) < Half - 8 && !Blocked(p, space);
        var woods = new List<(Vector2 C, float R)>();

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
            Wood(new Vector2(side * (Params.FlankWoodDist + rng.NextSingle() * 8), rng.NextSingle() * 36 - 18), 13 + rng.NextSingle() * 4);
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
