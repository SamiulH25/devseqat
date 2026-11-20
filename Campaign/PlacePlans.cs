using Microsoft.Xna.Framework;

namespace Ironfield;

/// How a settlement lies on the map (docs/SETTLEMENTS.md): its people's style, its footprint, the roads that leave it, the water
/// by it, the way its gate faces and where a party stands outside that gate. Derived at each map build, never saved. Headless.
public sealed record PlacePlan(int Id, SettlementKind Kind, int Size, string Style, Vector2 Pos, int Seed, float Radius)
{
    /// Bearings (radians, atan2 of z over x) of the roads that leave it: the main road first.
    public float[] Roads { get; init; } = Array.Empty<float>();
    /// Bearings where water (sea, river, lake) lies within its footprint, or just past it.
    public float[] Water { get; init; } = Array.Empty<float>();
    /// The way its main gate faces (bearing).
    public float Front { get; init; }
    /// Where a party in it stands: before the gate, on dry ground.
    public Vector2 Doorstep { get; init; }
    /// A port's quay: the shore end of its pier.
    public Vector2? Quay { get; init; }

    public PlaceStyle Look => Architecture.Of(Style);
    public static Vector2 Dir(float bearing) => new(MathF.Cos(bearing), MathF.Sin(bearing));
}

/// What a place's model shows of its state (docs/SETTLEMENTS.md): the colours its flags fly (a `Style.Sides` palette; -1 none).
public readonly record struct PlaceState(int Palette)
{
    public static readonly PlaceState None = new(-1);
}

public sealed partial class WorldMap
{
    /// Each settlement's plan by id (`PlanPlaces`, made with the terrain in `BuildTerrain`).
    public IReadOnlyDictionary<int, PlacePlan> Plans { get; private set; } = new Dictionary<int, PlacePlan>();

    /// The footprint of a place: what its model covers, before the nearest neighbour trims it.
    public static float FootprintOf(SettlementKind kind, int size) => kind switch
    {
        SettlementKind.Town => 13,
        SettlementKind.Village => 5.5f,
        _ => size switch { 1 => 4.2f, 3 => 9, _ => 6.2f },
    };

    /// Lay out every settlement's plan from the map as built: roads, water, footprint and front.
    public List<PlacePlan> PlanPlaces()
    {
        var plans = new List<PlacePlan>(Settlements.Count);
        foreach (var s in Settlements)
        {
            float nearest = Settlements.Where(o => o != s).Select(o => Vector2.Distance(o.Pos, s.Pos) - FootprintOf(o.Kind, o.Size) * 0.6f).DefaultIfEmpty(99).Min();
            float radius = MathF.Max(FootprintOf(s.Kind, s.Size) * 0.55f, MathF.Min(FootprintOf(s.Kind, s.Size), nearest * 0.85f));
            var roads = RoadBearings(s.Pos, radius + 1.5f);
            var water = WaterBearings(s.Pos, radius * 1.15f);
            Vector2? quay = Quays.TryGetValue(s.Id, out var q) ? q.Shore : null;
            float front = roads.Length > 0 ? roads[0] : water.Length > 0 ? water[0] + MathF.PI : MathF.PI * 1.5f; // no road: turn its back on the water, else face the map's near side
            plans.Add(new PlacePlan(s.Id, s.Kind, s.Size, s.Culture, s.Pos, s.Id * 31 + Seed, radius)
            {
                Roads = roads, Water = water, Front = front, Doorstep = DoorstepOf(s.Pos, front, radius), Quay = quay,
            });
        }
        Plans = plans.ToDictionary(p => p.Id);
        return plans;
    }

    /// Water at p, for laying out a place: the sea, a river's channel or a lake (grown by `pad`).
    public bool Wet(Vector2 p, float pad = 0) =>
        Landness(p) < 0.55f || Rivers.Any(r => r.Within(p, r.Width / 2 + 1 + pad)) || InLake(p, pad);

    /// The roads that cross a circle round p, as bearings, the busiest first (a town road before a village track, then by
    /// how straight it runs out). Rays every 7.5 degrees; neighbouring hits are one road.
    float[] RoadBearings(Vector2 p, float r)
    {
        const int N = 48;
        var hit = new bool[N];
        for (int i = 0; i < N; i++) hit[i] = RoadDist(p + PlacePlan.Dir(i * MathF.Tau / N) * r) < 1.6f;
        bool all = hit.All(h => h);
        var found = new List<(float Bearing, float Weight)>();
        for (int i = 0; i < N; i++)
        {
            if (!hit[i] || (all ? i != 0 : hit[(i + N - 1) % N])) continue; // the start of a run (a ring all road is one run)
            int len = 0;
            while (len < N && hit[(i + len) % N]) len++;
            float b = (i + (len - 1) / 2f) * MathF.Tau / N;
            var at = p + PlacePlan.Dir(b) * r;
            bool village = RoadLines.Count > 0 && RoadLines.MinBy(l => LineDist(l.Points, at)).Village;
            found.Add((MathHelper.WrapAngle(b), (village ? 0 : 10) + (RoadDist(p + PlacePlan.Dir(b) * r * 1.6f) < 2 ? 1 : 0)));
        }
        return found.OrderByDescending(f => f.Weight).Select(f => f.Bearing).ToArray();
    }

    /// The bearings toward water within r of p (every 15 degrees, at half and full reach), merged into runs.
    float[] WaterBearings(Vector2 p, float r)
    {
        const int N = 24;
        var hit = new bool[N];
        for (int i = 0; i < N; i++) hit[i] = Wet(p + PlacePlan.Dir(i * MathF.Tau / N) * r * 0.5f) || Wet(p + PlacePlan.Dir(i * MathF.Tau / N) * r);
        bool all = hit.All(h => h);
        var found = new List<float>();
        for (int i = 0; i < N; i++)
        {
            if (!hit[i] || (all ? i != 0 : hit[(i + N - 1) % N])) continue;
            int len = 0;
            while (len < N && hit[(i + len) % N]) len++;
            found.Add(MathHelper.WrapAngle((i + (len - 1) / 2f) * MathF.Tau / N));
        }
        return found.ToArray();
    }

    /// Where a party stands at a place: out before its gate, turned toward dry ground if the gate looks onto water.
    Vector2 DoorstepOf(Vector2 p, float front, float radius)
    {
        for (int k = 0; k < 12; k++)
        {
            float b = front + (k % 2 == 0 ? 1 : -1) * (k + 1) / 2 * MathF.PI / 6;
            var at = p + PlacePlan.Dir(b) * (radius + 2.2f);
            if (!Wet(at, 0.5f)) return at;
        }
        return p + PlacePlan.Dir(front) * (radius + 2.2f);
    }

    static float LineDist(Vector2[] pts, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            var ab = pts[i + 1] - pts[i];
            float k = ab.LengthSquared() < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - pts[i], ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, pts[i] + ab * k));
        }
        return best;
    }
}
