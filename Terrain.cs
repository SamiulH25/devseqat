using Microsoft.Xna.Framework;

namespace Ironfield;

public enum FoliageKind { Broadleaf, Pine, Bush, Rock, Town, Castle, Village, Wall, Tower, Gate, Ladder, House, Wagon,
    // Sites on the campaign map (docs/archive/SIDE_CONTENT.md S2).
    Ruin, Shrine, Stones, Field, Mine, Hut, Watch, Spring, Mill,
    Birch, // a slim pale-barked tree (campaign map, WL3)
    // The campaign map's small cover (docs/archive/WORLD_DRESS.md): cheap pieces that stand between the woods.
    Shrub, Tussock, Heather, Reed, DeadTree, Palm, Flowers,
    // The water's edge and the roadside (docs/archive/WORLD_DRESS.md WD4).
    Driftwood, Milestone, Wayside, Well, Rowboat, Marram,
    // An assault field's keep (a castle's) or landmark (a town's), behind the wall (docs/archive/SETTLEMENTS.md SM6).
    Keep, Landmark }

/// A river (roadmap C5): a centre line, its width, and the fords where it runs shallow.
public sealed record River(Vector2[] Path, float Width, Vector2[] Fords, float Depth = 1.3f)
{
    public const float FordRadius = 7, BridgeHalf = 2.4f;
    /// Extra bed width for coarse ground (the campaign map's 4 m grid): the cut is wider and its banks gentler, so a grid
    /// vertex always lands in the channel and the water sheet never floats over uncut ground.
    public float Widen { get; init; }
    /// How far off a bend's ends `Dist` still measures it (40 kept from the first maps; layout 6's ridges, ~100 wide, need more).
    public float Skip { get; init; } = 40;
    /// How far from the centre line the ground is touched by the cut.
    public float CarveReach => Width / 2 + 3 + 2 * Widen;
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
        for (int i = 0; i + 1 < Path.Length; i++) best = MathF.Min(best, SegmentDist(p, i));
        return best;
    }

    /// Distance from p to segment i, or MaxValue for a segment `Dist` passes over (p far from both its ends on both axes:
    /// a rule kept as it was, since map generation reads `Dist` and a seed's map must not change).
    float SegmentDist(Vector2 p, int i)
    {
        var a = Path[i]; var ab = Path[i + 1] - a;
        if (MathF.Abs(p.X - a.X) > Skip && MathF.Abs(p.X - Path[i + 1].X) > Skip && MathF.Abs(p.Y - a.Y) > Skip && MathF.Abs(p.Y - Path[i + 1].Y) > Skip) return float.MaxValue;
        float k = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * k);
    }

    /// Whether `Dist(p) < radius`, exactly, but from a grid of the river's segments: only the segments near p are measured
    /// (a full `Dist` measures them all, and the campaign map asks this for every ground vertex and minimap texel).
    public bool Within(Vector2 p, float radius)
    {
        var g = Grids.GetValue(this, r => new SegmentGrid(r.Path));
        // A repeated point on the path makes `Dist` NaN wherever that segment is measured, and NaN is never < radius.
        foreach (int i in g.Repeated)
            if (!(MathF.Abs(p.X - Path[i].X) > 40 && MathF.Abs(p.Y - Path[i].Y) > 40)) return false;
        if (p.X + radius < g.Min.X || p.Y + radius < g.Min.Y || p.X - radius > g.Max.X || p.Y - radius > g.Max.Y) return false;
        int x0 = g.CellX(p.X - radius), x1 = g.CellX(p.X + radius), y0 = g.CellY(p.Y - radius), y1 = g.CellY(p.Y + radius);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                foreach (int i in g.Cells[y * g.W + x])
                    if (SegmentDist(p, i) < radius) return true;
        return false;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<River, SegmentGrid> Grids = new();

    /// The river's segments by 16 m cell: each listed in every cell its bounding box touches.
    sealed class SegmentGrid
    {
        const float Cell = 16;
        public readonly Vector2 Min, Max;
        public readonly int W, H;
        public readonly List<int>[] Cells;
        /// Segments of no length (a point repeated on the path).
        public readonly List<int> Repeated = new();
        public SegmentGrid(Vector2[] path)
        {
            Min = new Vector2(float.MaxValue); Max = new Vector2(float.MinValue);
            foreach (var q in path) { Min = Vector2.Min(Min, q); Max = Vector2.Max(Max, q); }
            if (path.Length == 0) Min = Max = Vector2.Zero;
            W = Math.Max(1, (int)((Max.X - Min.X) / Cell) + 1); H = Math.Max(1, (int)((Max.Y - Min.Y) / Cell) + 1);
            Cells = new List<int>[W * H];
            for (int c = 0; c < Cells.Length; c++) Cells[c] = new List<int>();
            for (int i = 0; i + 1 < path.Length; i++)
            {
                if (path[i] == path[i + 1]) Repeated.Add(i);
                Vector2 lo = Vector2.Min(path[i], path[i + 1]), hi = Vector2.Max(path[i], path[i + 1]);
                for (int y = CellY(lo.Y); y <= CellY(hi.Y); y++)
                    for (int x = CellX(lo.X); x <= CellX(hi.X); x++) Cells[y * W + x].Add(i);
            }
        }
        public int CellX(float x) => Math.Clamp((int)((x - Min.X) / Cell), 0, W - 1);
        public int CellY(float y) => Math.Clamp((int)((y - Min.Y) / Cell), 0, H - 1);
    }

    public bool AtFord(Vector2 p) => Fords.Any(f => Vector2.DistanceSquared(f, p) < FordRadius * FordRadius);

    /// The water's surface at each path point (battlefields only; null on the campaign map, which cuts below the ground).
    /// Set by `Terrain.LevelRivers` from the uncut banks, so the bed is cut below one level across the river and never
    /// stands above the water where the field slopes across it.
    public float[]? Levels { get; set; }

    /// The water's surface near p, between the two nearest path points (needs `Levels`).
    public float LevelAt(Vector2 p)
    {
        int best = 0; float bestD = float.MaxValue, bestK = 0;
        for (int i = 0; i + 1 < Path.Length; i++)
        {
            var a = Path[i]; var ab = Path[i + 1] - a;
            float k = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0, 1), d = Vector2.DistanceSquared(p, a + ab * k);
            if (d < bestD) { bestD = d; best = i; bestK = k; }
        }
        return MathHelper.Lerp(Levels![best], Levels[best + 1], bestK);
    }

    /// How far the bed is cut below the banks here (shallow at a ford).
    public float Carve(Vector2 p, float dist) => AtBridge(p) ? 0 : (AtFord(p) ? 0.35f : Depth) * Smooth(CarveReach, Width / 2 - 1 + Widen, dist);

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

/// Still water on the campaign map (docs/archive/WORLD_LIFE.md WL2, layout 4): a lake or a village pond. Its shore wanders with
/// noise round `Radius`; the ground is cut into a basin `Depth` deep inside it.
public sealed record Lake(Vector2 Center, float Radius, int Seed, float Depth = 1.6f)
{
    /// The shore's distance from the centre toward angle `a` (radians).
    public float Edge(float a) => Radius * (0.82f + 0.36f * Terrain.Noise(MathF.Cos(a) * 1.7f + 5, MathF.Sin(a) * 1.7f + 5, Seed));

    /// Inside the shore (grown by `pad`).
    public bool Inside(Vector2 p, float pad = 0)
    {
        var d = p - Center;
        float r2 = d.LengthSquared(), reach = Radius * 1.18f + pad;
        if (r2 > reach * reach) return false;
        return MathF.Sqrt(r2) < Edge(MathF.Atan2(d.Y, d.X)) + pad;
    }

    /// How far the ground is lowered at p: the full depth well inside, easing to nothing 3 m past the shore.
    public float Carve(Vector2 p)
    {
        var d = p - Center;
        float r2 = d.LengthSquared(), reach = Radius * 1.18f + 3;
        if (r2 > reach * reach) return 0;
        float r = MathF.Sqrt(r2), e = Edge(MathF.Atan2(d.Y, d.X));
        float t = Math.Clamp((e + 3 - r) / (3 + e * 0.35f), 0, 1);
        return Depth * t * t * (3 - 2 * t);
    }
}

/// A road on the campaign map, as drawn (docs/archive/WORLD_LIFE.md WL1): its line and half-width (wider between towns than out to a village).
public readonly record struct RoadLine(Vector2[] Points, float Half, bool Village);

/// A bridge on the campaign map where a road crosses a river: where, the way the road runs, how far it spans, and whether
/// it is built of stone (between towns) or planks (to a village). Drawing only: the map's crossing is a shallow ford in its heights.
public readonly record struct MapBridge(Vector2 At, Vector2 Along, float Reach, bool Stone);

/// What a field round a village or town holds (docs/archive/WORLD_LIFE.md WL3). Drawing only.
public enum FieldKind { Grain, Green, Ploughed, Fallow, Pasture, Orchard }

/// A field plot on the campaign map (layout 4): a rectangle at `Center`, its long side along `Axis` (unit), `Half` its
/// half length and half width; `Hedged` holds a bit per side (1, 2, 4, 8) that has a hedge (pastures get fences instead).
public readonly record struct FieldPlot(Vector2 Center, Vector2 Axis, Vector2 Half, FieldKind Kind, int Hedged, int Seed)
{
    /// Whether p lies in the plot (grown by `pad` metres).
    public bool Inside(Vector2 p, float pad = 0)
    {
        var d = p - Center;
        return MathF.Abs(Vector2.Dot(d, Axis)) < Half.X + pad && MathF.Abs(d.X * -Axis.Y + d.Y * Axis.X) < Half.Y + pad;
    }
    /// A point of the plot from local coordinates (u along the axis, v across).
    public Vector2 At(float u, float v) => Center + Axis * u + new Vector2(-Axis.Y, Axis.X) * v;

    /// Whether this plot and another come within `gap` metres (a separating-axis test on the two rotated rectangles).
    public bool Overlaps(FieldPlot o, float gap)
    {
        var d = o.Center - Center;
        foreach (var ax in new[] { Axis, new Vector2(-Axis.Y, Axis.X), o.Axis, new Vector2(-o.Axis.Y, o.Axis.X) })
        {
            float Reach(FieldPlot f) => MathF.Abs(Vector2.Dot(f.Axis, ax)) * f.Half.X + MathF.Abs(Vector2.Dot(new Vector2(-f.Axis.Y, f.Axis.X), ax)) * f.Half.Y;
            if (MathF.Abs(Vector2.Dot(d, ax)) > Reach(this) + Reach(o) + gap) return false;
        }
        return true;
    }
}

/// One piece of foliage (or, on the campaign map, a settlement). Radius > 0 = a solid obstacle (tree trunk, boulder).
/// `Dry` (0-1) draws the map's small cover from green toward straw and thorn (docs/archive/WORLD_DRESS.md).
public readonly record struct Foliage(FoliageKind Kind, Vector2 Pos, float Scale, float Radius, int Seed, float Dry = 0);

/// How a battlefield is generated: set from the campaign map cell where the fight happens (docs/CAMPAIGN.md 3.2).
/// `Layouts` are the field shapes this ground may take (docs/archive/TERRAIN.md T2; null = open field), picked by the seed.
/// `Scale` grows the clear deployment ground and the flank woods with the size of the battle (1 up to 450 men).
public sealed record TerrainParams(string Name, int HillsMin, int HillsMax, float HillHeight, float Roll, float FlankWoodDist,
    int Woods, int LoneTrees, int Bushes, bool Road, bool River = false, FieldLayout[]? Layouts = null, float Scale = 1)
{
    /// From the campaign (docs/archive/TERRAIN.md T7): how wet the ground is from recent rain (0-1: low ground turns to mud),
    /// the season (0 spring .. 3 winter; -1 any: winter fields lie bare), and the team that chose where to stand
    /// (-1 nobody: the seed decides; it gets the crest, the village, the hills on its side).
    public float Wet { get; init; }
    public int Season { get; init; } = -1;
    public int Favour { get; init; } = -1;
    /// The drawn world's grounds (docs/archive/WORLDMAP.md WM3): the colour the open ground takes (green, moss, frost, sand, dry
    /// grass), and woods of pines only.
    public GroundLook Look { get; init; }
    public bool Conifer { get; init; }
    /// Who built what stands on the field (docs/archive/SETTLEMENTS.md SM6): an `Architecture` style id (a place's culture) for the
    /// houses and an assault's wall, null = the old plain look. `Castle`: the assault is on a castle (a curtain and a keep,
    /// not a town wall and its houses). `Banner`: the defenders' colours over the gate (a `Style.Sides` palette, -1 none).
    public string? Style { get; init; }
    public bool Castle { get; init; }
    public int Banner { get; init; } = -1;

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
    /// Coast: low rolling ground by the sea; on the shore the sea runs along one flank (FieldLayout.Shore).
    public static readonly TerrainParams Coast = new("Coast", 2, 3, 0.7f, 0.8f, 72, 4, 24, 160, true, Layouts: new[] { FieldLayout.Shore, FieldLayout.Shore, FieldLayout.Open, FieldLayout.Village });
    /// Taiga: dark pine forest on mossy ground; the fight is in a clearing or beside a great wood.
    public static readonly TerrainParams Taiga = new("Taiga", 2, 4, 1f, 1f, 55, 20, 260, 220, false, Layouts: new[] { FieldLayout.ForestClearing, FieldLayout.WoodedFlank }) { Look = GroundLook.Moss, Conifer = true };
    /// Highlands: high, bare moorland, steeper than the hills, stony on its slopes, a few pines.
    public static readonly TerrainParams Highlands = new("Highlands", 5, 8, 2.2f, 1.6f, 70, 5, 60, 160, false, Layouts: new[] { FieldLayout.Ridge, FieldLayout.Pass, FieldLayout.Pass, FieldLayout.TwoHills }) { Look = GroundLook.Moor, Conifer = true };
    /// Tundra: cold open barrens, frost on the grass, stones everywhere, stunted pines.
    public static readonly TerrainParams Tundra = new("Tundra", 2, 4, 0.8f, 1.1f, 90, 2, 10, 70, false) { Look = GroundLook.Frost, Conifer = true };
    /// Desert: dunes of loose sand between hard, stony pans; no woods at all.
    public static readonly TerrainParams Desert = new("Desert", 3, 5, 1.3f, 1.4f, 100, 0, 0, 20, false, Layouts: new[] { FieldLayout.Open, FieldLayout.Open, FieldLayout.Broken }) { Look = GroundLook.Sand };
    /// Oasis: dry grass and palms by a stream in the sand; the desert lies round the edge.
    public static readonly TerrainParams Oasis = new("Oasis", 1, 3, 0.8f, 0.9f, 60, 3, 30, 120, true, true, new[] { FieldLayout.Open, FieldLayout.Village }) { Look = GroundLook.Dry };
    /// The tournament field (S5): flat, open, a few trees at the edge.
    public static readonly TerrainParams Lists = new("Lists", 0, 0, 0.2f, 0.3f, 90, 0, 6, 30, false);

    /// A preset by name (IRONFIELD_GROUND), or null.
    public static TerrainParams? Named(string name) =>
        new[] { Meadow, Farmland, Forest, Hills, Marsh, Steppe, Badlands, Coast, Taiga, Highlands, Tundra, Desert, Oasis, Lists }.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// Headless battlefield ground: a heightmap (gentle rolling meadow, a few hills, a rim of high ground,
/// a dirt road through the middle) and the placement of trees, bushes and rocks.
/// Heights are sampled per triangle exactly as the rendered mesh is split, so units stand on the drawn ground.
public sealed partial class Terrain
{
    /// The layout a field with this seed and ground is built to (the encounter panel asks before the battle).
    public static FieldLayout LayoutFor(int seed, TerrainParams p, bool siege = false) =>
        siege || p.Layouts is not { Length: > 0 } ? FieldLayout.Open : p.Layouts[new Random(seed + 17).Next(p.Layouts.Length)];

    /// A battlefield: +-256 m of ground on 2.5 m vertices. The campaign map is much bigger and coarser (WorldMap.WorldShape).
    public const float BattleHalf = 256, BattleStep = 2.5f;
    public const int BattleN = 205; // vertices per side = 2 * BattleHalf / BattleStep + 1
    const float Cell = 8;           // the obstacle grid's cell size

    public readonly int Seed;
    public readonly bool IsFlat;
    public readonly List<Foliage> Foliage = new();
    /// The campaign map's roads, lakes and bridges as drawn (docs/archive/WORLD_LIFE.md WL1-WL2); empty on a battlefield.
    public readonly List<RoadLine> RoadLines = new();
    public readonly List<Lake> Lakes = new();
    public readonly List<MapBridge> MapBridges = new();
    /// Ships lying at the ports' piers on the campaign map (docs/archive/WORLDMAP.md WM7; drawing only).
    public readonly List<(Vector2 At, float Yaw)> Moored = new();
    /// The campaign map's settlements as laid out for their models (docs/archive/SETTLEMENTS.md); empty on a battlefield.
    public readonly List<PlacePlan> Places = new();
    /// The sea's surface on a designed campaign map (docs/archive/WORLDMAP.md WM2); null where there is no sea.
    public float? SeaLevel;
    /// Field plots round the settlements (campaign map, layout 4) and the season they were drawn for (-1: a battlefield).
    public readonly List<FieldPlot> Fields = new();
    public int MapSeason = -1;
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
    /// The campaign map: how much of the colour `Tint` gives a place keeps through the map's grassland look (0-1: sand,
    /// frost, heath; docs/archive/WORLDMAP.md WM3). Null or 0 = the grassland look, as every map had before. Visual only.
    public Func<Vector2, float>? Own { get; init; }
    /// The share of their drawn size that places, trees and sites are built at (the map's `WorldMap.ThingScale`; 1 in battle).
    public float ThingScale { get; init; } = 1;
    /// Grass tufts and the minimap's reach (the campaign map has no grass and a wider overview).
    public readonly bool Grass = true;
    public readonly float MinimapRange = 110;
    /// How many metres one minimap texel covers (the renderer sizes the image from this).
    public const float MinimapMetresPerTexel = 2f;

    /// The siege fortress in an assault (docs/pre-demo/SIEGES.md SE1; null in field battles).
    public readonly Fortress? Wall;
    /// Rivers on this ground (a battlefield has at most one; the campaign map has a few).
    public readonly List<River> Rivers = new();

    /// How deep the water is at p (0 on dry land; shallow at a ford).
    public float WaterDepth(Vector2 p)
    {
        foreach (var r in Rivers)
            if (r.Within(p, r.Width / 2)) return r.AtBridge(p) ? 0 : r.AtFord(p) ? 0.3f : r.Depth;
        if (Layout == FieldLayout.Shore && customHeight == null && SeaLevel is float sea && AtSea(p)) return MathF.Max(0, sea - Height(p));
        return 0;
    }

    public static Terrain Flat() => new(0, true, TerrainParams.Meadow);
    public Terrain(int seed) : this(seed, false, TerrainParams.Meadow) { }
    public Terrain(int seed, TerrainParams p, Fortress? wall = null) : this(seed, false, p, wall) { }

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
        RawHeight(0, 0); // builds the map's look-up grids once, before the rows below read them in parallel
        Parallel.For(0, N, z => // the map's height is a pure function of the point: each core takes some rows
        {
            for (int x = 0; x < N; x++)
                grid[z * N + x] = RawHeight(-Half + x * Step, -Half + z * Step);
        });
        foreach (var f in foliage) Add(f);
    }

    Terrain(int seed, bool flat, TerrainParams p, Fortress? wall = null)
    {
        Seed = seed;
        IsFlat = flat;
        Layout = flat ? FieldLayout.Open : LayoutFor(seed, p, wall != null);
        Params = wall != null ? p with { Road = false } : Layout == FieldLayout.RiverLine ? p with { Road = true, River = true }
            : Layout == FieldLayout.Shore ? p with { Road = false } : p; // no road runs into the sea
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
        foreach (var r in Rivers) LevelRivers(r);
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
                grid[z * N + x] = RawHeight(-Half + x * Step, -Half + z * Step);
        PlaceFoliage(new Random(seed + 99));
        PaintGround();
    }

    // ---------- heights ----------

    /// A battlefield river's water level along its course: a little under the lower of its uncut banks, eased along the
    /// course and never above the bank it had (World.Water.cs draws the surface at it).
    void LevelRivers(River r)
    {
        int n = r.Path.Length;
        var raw = new float[n];
        for (int i = 0; i < n; i++)
        {
            var d = r.Path[Math.Min(i + 1, n - 1)] - r.Path[Math.Max(i - 1, 0)];
            var across = Vector2.Normalize(new Vector2(-d.Y, d.X)) * (r.Width / 2 + 3.5f);
            Vector2 a = r.Path[i] + across, b = r.Path[i] - across;
            raw[i] = MathF.Min(RawHeight(a.X, a.Y, false), RawHeight(b.X, b.Y, false)) - 0.3f;
        }
        var lv = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0; int k = 0;
            for (int j = Math.Max(0, i - 3); j <= Math.Min(n - 1, i + 3); j++) { sum += raw[j]; k++; }
            lv[i] = MathF.Min(raw[i], sum / k);
        }
        r.Levels = lv;
    }

    float RawHeight(float x, float z, bool carve = true)
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
        float rim = SmoothStep(120, 240, r) * (14 + 10 * Noise(x * 0.008f, z * 0.008f, Seed + 3));
        if (Layout == FieldLayout.Shore) rim *= 1 - SmoothStep(-90, -10, PastShore(x, z)); // no rim between the field and the sea
        h += rim;
        float rd = RoadDist(new Vector2(x, z));
        if (rd < 5) h -= 0.25f * (1 - rd / 5); // worn a little into the ground
        foreach (var river in Rivers)
        {
            var at = new Vector2(x, z);
            float d = river.Dist(at);
            if (!carve || d >= river.Width / 2 + 3) continue;
            if (river.Levels == null) h -= river.Carve(at, d);
            else if (!river.AtBridge(at)) // the bed a set depth under the water, whatever the ground does across the river
            {
                bool ford = river.AtFord(at); // a ford's shallow bed reaches out under the water's edge, or its banks show through
                h = MathHelper.Lerp(h, MathF.Min(h, river.LevelAt(at) - (ford ? 0.3f : river.Depth)), SmoothStep(river.CarveReach, river.Width / 2 + (ford ? 1.2f : -1), d));
            }
        }
        if (Layout == FieldLayout.Shore) h = ShoreHeight(x, z, h);
        if (Wall != null)
        {
            var pt = new Vector2(x, z);
            float dFort = Vector2.Distance(pt, Wall.Center);
            // Flatten ground inside the fortress and its immediate approach; push the outer valley rim clear of the walls.
            float calm = SmoothStep(Wall.Radius + 65, Wall.Radius + 15, dFort);
            h = MathHelper.Lerp(h, 0.15f * h, calm);
            if (Wall.InDitch(pt)) h -= 1.4f;
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

    /// Raw terrain heightmap elevation (without dynamic ladder/ramp climb elevation).
    public float BaseHeight(Vector2 p)
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

    /// Ground height, interpolated on the same triangles the renderer draws, plus smooth ladder/ramp climb height.
    public float Height(Vector2 p)
    {
        float h = BaseHeight(p);
        if (Wall != null && Wall.ClimbElevation(p, BaseHeight, out float cy) && cy > h) return cy;
        return h;
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

    /// Proactive local obstacle avoidance: if a solid building, rock or trunk lies ahead along `dir` within `lookAhead`,
    /// returns a unit detour direction curving smoothly around the nearer side (or Vector2.Zero when the way ahead is clear).
    public Vector2 AvoidObstacle(Vector2 pos, Vector2 dir, float r, float lookAhead, int tieSign = 1)
    {
        if (Foliage.Count == 0 && Wall == null) return Vector2.Zero;
        var right = new Vector2(-dir.Y, dir.X);
        float bestAlong = float.MaxValue;
        Vector2 bestDetour = Vector2.Zero;

        void Consider(Vector2 center, float alongHalf, float acrossHalf)
        {
            var rel = center - pos;
            float along = Vector2.Dot(rel, dir);
            float clearance = acrossHalf + r + 0.2f;
            if (along < 0 || along > lookAhead + alongHalf) return;
            float lat = Vector2.Dot(rel, right);
            if (MathF.Abs(lat) >= clearance) return;
            float hitAt = MathF.Max(0, along - alongHalf);
            if (hitAt >= bestAlong) return;
            bestAlong = hitAt;
            float side = MathF.Abs(lat) < 0.25f ? tieSign : (lat > 0 ? -1 : 1);
            float urgency = Math.Clamp(1 - hitAt / MathF.Max(1f, lookAhead), 0.2f, 1f);
            var tangent = dir * (1 - urgency * 0.45f) + right * (side * (0.45f + urgency * 0.55f));
            bestDetour = tangent.LengthSquared() > 1e-4f ? Vector2.Normalize(tangent) : right * side;
        }

        if (Wall != null)
        {
            foreach (var b in Wall.Buildings)
            {
                float alongHalf = MathF.Abs(Vector2.Dot(dir, b.Along)) * b.W * 0.5f + MathF.Abs(Vector2.Dot(dir, b.Across)) * b.D * 0.5f;
                float acrossHalf = MathF.Abs(Vector2.Dot(right, b.Along)) * b.W * 0.5f + MathF.Abs(Vector2.Dot(right, b.Across)) * b.D * 0.5f;
                Consider(b.Pos, alongHalf, acrossHalf);
            }
        }
        if (Foliage.Count > 0)
        {
            var probe = pos + dir * (lookAhead * 0.5f);
            int bx = (int)((probe.X + Half) / Cell), bz = (int)((probe.Y + Half) / Cell);
            for (int z = bz - 1; z <= bz + 1; z++)
                for (int x = bx - 1; x <= bx + 1; x++)
                {
                    if (x < 0 || z < 0 || x >= bucketSide || z >= bucketSide) continue;
                    foreach (int i in buckets[z * bucketSide + x])
                    {
                        var f = Foliage[i];
                        if (f.Radius <= 0) continue;
                        Consider(f.Pos, f.Radius, f.Radius);
                    }
                }
        }
        return bestDetour;
    }

    /// Push a circle of radius r out of any trunk or boulder it overlaps. Returns true if it hit something.
    public bool PushOut(ref Vector2 p, float r, bool mounted = true)
    {
        bool hit = PushOutFeatures(ref p, r, mounted);
        if (Wall != null && Wall.PushOutBuildings(ref p, r)) hit = true;
        if (Foliage.Count == 0) return hit;
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
    public bool Core(Vector2 p) => MathF.Abs(p.X) < 50 * Params.Scale && MathF.Abs(p.Y) < (54 + Battle.Stand) * Params.Scale;

    void PlaceFoliage(Random rng)
    {
        bool Ok(Vector2 p, float space) => !Core(p) && RoadDist(p) > 4 && WaterDepth(p) == 0 && Rivers.All(r => r.Dist(p) > r.Width / 2 + 2) && MathF.Abs(p.X) < Half - 8 && MathF.Abs(p.Y) < Half - 8 && !Blocked(p, space)
            && (Wall == null || (Vector2.Distance(p, Wall.Center) > Wall.Radius + 24 && !(p.Y > -120 && p.Y < Wall.Center.Y && MathF.Abs(p.X) < 105)));
        if (Layout == FieldLayout.Village) PlaceVillage(new Random(Seed + 19));
        void Wood(Vector2 c, float r)
        {
            if (Layout == FieldLayout.Shore && PastShore(c.X, c.Y) > -24 - r) return; // no wood on the beach or in the sea
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
                if (Params.Conifer) pine = true;
                Add(new Foliage(pine ? FoliageKind.Pine : FoliageKind.Broadleaf, p, s, 0.32f * s, rng.Next()));
            }
        }

        // Two woods flank the field: cavalry has to ride round them (not in the open desert).
        for (int side = -1; side <= (Params.Look == GroundLook.Sand ? -3 : 1); side += 2)
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
            Add(new Foliage(rng.NextSingle() < 0.3f || Params.Conifer ? FoliageKind.Pine : FoliageKind.Broadleaf, p, s, 0.32f * s, rng.Next()));
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
            if (RoadDist(p) < 3 || Blocked(p, 0.5f) || AtSea(p)) continue;
            Add(new Foliage(FoliageKind.Bush, p, 0.7f + rng.NextSingle() * 0.6f, 0, rng.Next()));
        }
        if (Layout == FieldLayout.Hedgerows) PlaceHedges(new Random(Seed + 20));
        for (int i = 0; i < 90; i++) // pebbles and small rocks
        {
            float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * 180;
            var p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (RoadDist(p) < 3 || Blocked(p, 0.3f) || AtSea(p)) continue;
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
