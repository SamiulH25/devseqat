using Microsoft.Xna.Framework;

namespace Ironfield;

/// The campaign world's geography (docs/CAMPAIGN.md 3): provinces, settlements, roads and terrain, all generated from a seed.
/// Map units: the ground spans +-TerrainHalf (1 unit = 0.75 km). The same seed always gives the same map, so saves store only the seed
/// plus what changes (ownership, recruits, parties).
public sealed class WorldMap
{
    /// Where provinces live; mountains fill the rim out to the terrain's edge (TerrainHalf).
    public const float Extent = 392;
    /// Half the side of the ground: the provinces, plus a rim of mountains to close the world in (about 590 km across).
    public const float TerrainHalf = 448;
    /// Ground vertex spacing on the campaign map (coarser than a battlefield's 2.5 m). Small (pre-enlargement) maps keep the
    /// finer grid they were drawn with.
    public const float Step = 4f, LegacyStep = 2.5f;
    /// The mountain rim between the provinces' extent and the edge of the ground.
    const float Rim = TerrainHalf - Extent;
    /// The extent campaigns generated before the map was enlarged used; saves keep their own map (CampaignState.MapExtent).
    public const float LegacyExtent = 196;
    /// How far apart provinces stand on the legacy map. A bigger world spreads them by (extent/196)^0.3, which lands
    /// the full map on about Provinces of them: more provinces, and more ground between them, without every road
    /// becoming a day's march.
    public const float ProvinceSpacing = 66;
    /// Roughly how many provinces the enlarged map packs in, and how many the original map had (the baseline every
    /// balance number that scales with the world is quoted against).
    public const int ProvinceCount = 60;
    public const int LegacyProvinces = 26;
    public static readonly string[] Kinds = { "Meadow", "Farmland", "Forest", "Hills" };

    public readonly int Seed;
    public readonly List<Province> Provinces = new();
    public readonly List<Settlement> Settlements = new();
    public readonly List<(Vector2 A, Vector2 B)> Roads = new();
    /// Two rivers wind across the land; where a road meets one there is a bridge (a ford on the battlefield).
    public readonly List<River> Rivers = new();
    /// 0-3: spring to winter. The ground's colours follow it (set before BuildTerrain).
    public int Season;
    /// The extent this map was generated with (provinces live within it; mountains close the world beyond).
    public float ExtentUsed = Extent;
    /// How big this map is against the one the balance numbers were tuned on (WorldMap.LegacyProvinces provinces).
    /// Parties and events that should keep pace with the size of the world scale with this, so the balance numbers stay
    /// legible against the original map and a bigger map is not emptier or duller than the small one was.
    public float ProvinceScale => MathF.Max(0.1f, Provinces.Count / (float)LegacyProvinces);
    /// How far apart this map's places stand against the original map's (1 = the same). Distances that mean "the next
    /// village" or "a short ride" scale with this, so a lord on a big map still finds things to do nearby.
    public float Spread => MathF.Pow(MathF.Max(0.05f, ExtentUsed / LegacyExtent), 0.3f);
    /// Half the side of this map's ground: the provinces, plus the mountain rim (an old save keeps the small world it had).
    public float GroundHalf => ExtentUsed + Rim;
    readonly List<(Vector2 C, float R, float H)> hills = new();

    WorldMap(int seed) { Seed = seed; }

    /// The map a fresh campaign uses. `extent` is half the side the provinces may use (Extent for new campaigns,
    /// LegacyExtent for saves made before the map grew), so a save keeps the geography it was played on.
    public static WorldMap Generate(int seed, float extent = Extent)
    {
        var m = new WorldMap(seed) { ExtentUsed = extent };
        var rng = new Random(seed);
        var names = Names.Get();
        var places = names.Places.OrderBy(_ => rng.Next()).ToList();
        int nameAt = 0;
        string Place() => places[nameAt++ % places.Count];
        // Everything below is measured in "spans": 1 = the map before it grew (196), so a bigger world scales with it.
        float span = extent / LegacyExtent;
        float spacing = ProvinceSpacing * MathF.Pow(span, 0.3f);

        // Provinces: well-spaced random centres.
        var centres = new List<Vector2>();
        for (int tries = 0; tries < 60000 && centres.Count < 400; tries++)
        {
            var p = new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * extent * 0.92f;
            if (centres.All(c => Vector2.Distance(c, p) > spacing)) centres.Add(p);
        }
        // The north (low z) is the Northmarch; south of it, west is the Azure Crown and east the Crimson Compact.
        var north = centres.OrderBy(c => c.Y).Take(centres.Count * 3 / 10).ToHashSet();
        var south = centres.Where(c => !north.Contains(c)).OrderBy(c => c.X).ToList();
        foreach (var (c, i) in centres.OrderBy(c => c.X).Select((c, i) => (c, i)))
        {
            int kind = rng.NextSingle() switch { < 0.35f => 0, < 0.6f => 1, < 0.85f => 2, _ => 3 };
            if (MathF.Max(MathF.Abs(c.X), MathF.Abs(c.Y)) > extent * 0.75f && rng.Next(2) == 0) kind = 3; // hills toward the rim
            if (north.Contains(c) && kind == 1) kind = 2; // the north is forest, not farmland
            int faction = north.Contains(c) ? 2 : south.IndexOf(c) < south.Count / 2 ? 0 : 1;
            m.Provinces.Add(new Province { Id = i, Name = Place(), Kind = Kinds[kind], Center = c, Faction = faction });
        }
        // Neighbours: each province links to its nearest few; roads follow the links.
        foreach (var p in m.Provinces)
            foreach (var q in m.Provinces.Where(q => q != p).OrderBy(q => Vector2.Distance(p.Center, q.Center)).Take(3))
                if (Vector2.Distance(p.Center, q.Center) < 160 * span)
                {
                    if (!p.Neighbors.Contains(q.Id)) p.Neighbors.Add(q.Id);
                    if (!q.Neighbors.Contains(p.Id)) q.Neighbors.Add(p.Id);
                }
        foreach (var p in m.Provinces)
            foreach (int q in p.Neighbors.Where(q => q > p.Id))
                m.Roads.Add((p.Center, m.Provinces[q].Center));

        // Settlements: a town or castle at each centre (capitals are the outermost towns), and 2-3 villages around it.
        int id = 0;
        var westCap = m.Provinces.Where(p => p.Faction == 0).OrderBy(p => p.Center.X).First();
        var eastCap = m.Provinces.Where(p => p.Faction == 1).OrderBy(p => p.Center.X).Last();
        var northCap = m.Provinces.Where(p => p.Faction == 2).OrderBy(p => p.Center.Y).FirstOrDefault();
        foreach (var p in m.Provinces)
        {
            bool town = p == westCap || p == eastCap || p == northCap || p.Id % 2 == 0;
            m.Settlements.Add(new Settlement { Id = id++, Province = p.Id, Faction = p.Faction, Name = p.Name, Pos = p.Center,
                Kind = town ? SettlementKind.Town : SettlementKind.Castle });
            int villages = 2 + rng.Next(2);
            for (int v = 0, tries = 0; v < villages && tries < 200; tries++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = (16 + rng.NextSingle() * 16) * span;
                var pos = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (m.Settlements.Any(s => Vector2.Distance(s.Pos, pos) < 12 * span) || MathF.Abs(pos.X) > extent || MathF.Abs(pos.Y) > extent) continue;
                m.Settlements.Add(new Settlement { Id = id++, Province = p.Id, Faction = p.Faction, Name = Place(), Pos = pos, Kind = SettlementKind.Village });
                m.Roads.Add((p.Center, pos));
                v++;
            }
        }
        // Rivers: from one edge to the far one, wandering, kept clear of settlements; bridges where roads cross.
        for (int k = 0; k < 2; k++)
        {
            bool northSouth = k == 0;
            float off = (rng.NextSingle() - 0.5f) * extent * 0.8f, ph = rng.NextSingle() * MathF.Tau;
            var pts = new List<Vector2>();
            for (float s = -extent - 20; s <= extent + 20; s += 4)
            {
                float w = off + MathF.Sin(s * (0.018f / span) + ph) * 26 * span + MathF.Sin(s * (0.047f / span) + ph * 2) * 9 * span;
                var p = northSouth ? new Vector2(w, s) : new Vector2(s, w);
                foreach (var st in m.Settlements)
                {
                    var away = p - st.Pos;
                    float d = away.Length();
                    if (d < 9 * span) p = st.Pos + (d > 0.01f ? away / d : Vector2.UnitX) * 9 * span;
                }
                pts.Add(p);
            }
            var path = pts.ToArray();
            var bridges = new List<Vector2>();
            for (int i = 0; i + 1 < path.Length; i++)
                foreach (var (a, b) in m.Roads)
                    if (Cross(path[i], path[i + 1], a, b) is Vector2 x) bridges.Add(x);
            m.Rivers.Add(new River(path, 2.6f, bridges.ToArray(), 1.1f));
        }
        // Hill provinces get real hills.
        foreach (var p in m.Provinces.Where(p => p.Kind == "Hills"))
            for (int i = 0; i < 3; i++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = (10 + rng.NextSingle() * 30) * span;
                m.hills.Add((p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, (16 + rng.NextSingle() * 14) * span, 5 + rng.NextSingle() * 5));
            }
        return m;
    }

    static Vector2? Cross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
    {
        Vector2 r = p2 - p, s = q2 - q;
        float den = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(den) < 1e-6f) return null;
        float t = ((q.X - p.X) * s.Y - (q.Y - p.Y) * s.X) / den, u = ((q.X - p.X) * r.Y - (q.Y - p.Y) * r.X) / den;
        return t is >= 0 and <= 1 && u is >= 0 and <= 1 ? p + r * t : null;
    }

    public float RiverDist(Vector2 p) => Rivers.Count == 0 ? float.MaxValue : Rivers.Min(r => r.Dist(p));

    // ---------- queries ----------

    public Province ProvinceAt(Vector2 p)
    {
        Province best = Provinces[0];
        float bd = float.MaxValue;
        foreach (var q in Provinces)
        {
            float d = Vector2.DistanceSquared(q.Center, p);
            if (d < bd) { bd = d; best = q; }
        }
        return best;
    }

    public float RoadDist(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var (a, b) in Roads)
        {
            var ab = b - a;
            float k = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, a + ab * k));
        }
        return best;
    }

    /// Map travel speed factor at a point (terrain kind, roads).
    public float SpeedFactor(Vector2 p, float roadBonus) =>
        RoadDist(p) < 2.5f ? roadBonus : RiverDist(p) < 1.6f ? 0.35f : ProvinceAt(p).Kind switch { "Forest" => 0.7f, "Hills" => 0.8f, _ => 1f };

    /// Battle ground for a fight at p: the province's kind, with a road if one runs close.
    public TerrainParams BattleGround(Vector2 p)
    {
        var t = ProvinceAt(p).Kind switch { "Forest" => TerrainParams.Forest, "Hills" => TerrainParams.Hills, "Farmland" => TerrainParams.Farmland, _ => TerrainParams.Meadow };
        return t with { Road = RoadDist(p) < 6, River = RiverDist(p) < 12 };
    }

    public float Height(float x, float z)
    {
        float h = Terrain.Noise(x * 0.012f, z * 0.012f, Seed) * 3 + Terrain.Noise(x * 0.035f, z * 0.035f, Seed + 1) * 0.8f;
        foreach (var (c, r, hh) in hills)
        {
            float d = Vector2.Distance(new Vector2(x, z), c) / r;
            if (d < 1) h += hh * (1 - d * d) * (1 - d * d);
        }
        float edge = MathF.Max(MathF.Abs(x), MathF.Abs(z));
        float t = Math.Clamp((edge - (ExtentUsed - 10)) / (GroundHalf - ExtentUsed + 10), 0, 1);
        h += t * t * (3 - 2 * t) * (24 + 10 * Terrain.Noise(x * 0.02f, z * 0.02f, Seed + 2)); // mountains close the world
        foreach (var r in Rivers)
        {
            var at = new Vector2(x, z);
            float d = r.Dist(at);
            if (d < r.Width / 2 + 3) h -= r.Carve(at, d);
        }
        return h;
    }

    /// Ground tint: patchwork fields in farmland, dark forest floor, dry hills (visual only).
    public Vector3 Tint(Vector2 p, Vector3 ground)
    {
        var c = ground * KindTint(p);
        // Summer dries the grass, autumn browns it, winter lies white (more so on high ground; roads show through).
        return Season switch
        {
            1 => c * new Vector3(1.06f, 1.02f, 0.86f),
            2 => c * new Vector3(1.18f, 0.94f, 0.68f),
            3 => Vector3.Lerp(c, new Vector3(0.86f, 0.89f, 0.93f), RoadDist(p) < 2.5f ? 0.35f : 0.7f + 0.2f * Math.Clamp(Height(p.X, p.Y) / 10, 0, 1)),
            _ => c,
        };
    }

    Vector3 KindTint(Vector2 p)
    {
        switch (ProvinceAt(p).Kind)
        {
            case "Farmland":
            {
                int cx = (int)MathF.Floor(p.X / 9), cz = (int)MathF.Floor(p.Y / 7);
                int h = Math.Abs(cx * 73856093 ^ cz * 19349663) % 4;
                return h switch { 0 => new(1.16f, 1.06f, 0.74f), 1 => new(1.02f, 0.92f, 0.78f), 2 => new(0.94f, 1.03f, 0.9f), _ => Vector3.One };
            }
            case "Forest": return new(0.8f, 0.88f, 0.76f);
            case "Hills": return new(1.08f, 1.04f, 0.86f);
            default: return Vector3.One;
        }
    }

    /// The renderable terrain: heights, roads, map-scale trees and the settlements as 3D models.
    public Terrain BuildTerrain()
    {
        var rng = new Random(Seed + 7);
        var foliage = new List<Foliage>();
        float span = ExtentUsed / LegacyExtent;
        foreach (var s in Settlements)
            foliage.Add(new Foliage(s.Kind switch { SettlementKind.Town => FoliageKind.Town, SettlementKind.Castle => FoliageKind.Castle, _ => FoliageKind.Village },
                s.Pos, s.Kind == SettlementKind.Village ? 1.6f : 2.1f, 0, s.Id * 31 + Seed));
        float half = GroundHalf - 6;
        bool Clear(Vector2 p) => Settlements.All(s => Vector2.Distance(s.Pos, p) > 9 * span) && RoadDist(p) > 2.5f && RiverDist(p) > 3
            && MathF.Abs(p.X) < half && MathF.Abs(p.Y) < half;
        foreach (var p in Provinces)
        {
            // Woods scale with the ground they stand on (area), so the bigger map stays as wooded as the old one. Trees grow
            // a little larger too, so a whole-map view reads as woodland without holding four times the geometry.
            float grow = MathF.Sqrt(span);
            int trees = (int)(p.Kind switch { "Forest" => 90, "Hills" => 26, "Farmland" => 6, _ => 14 } * span * span * 1.4f);
            float radius = (p.Kind == "Forest" ? 48 : 60) * span;
            for (int i = 0; i < trees; i++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * radius;
                var pos = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (!Clear(pos) || ProvinceAt(pos) != p) continue;
                bool pine = p.Kind == "Hills" || rng.NextSingle() < 0.25f;
                foliage.Add(new Foliage(pine ? FoliageKind.Pine : FoliageKind.Broadleaf, pos, (2.2f + rng.NextSingle() * 1.2f) * grow, 0, rng.Next()));
            }
            if (p.Kind == "Farmland")
                for (int i = 0; i < 24; i++) // hedgerows
                {
                    var pos = p.Center + new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * 45 * span;
                    if (Clear(pos)) foliage.Add(new Foliage(FoliageKind.Bush, pos, (2 + rng.NextSingle()) * grow, 0, rng.Next()));
                }
        }
        for (int i = 0; i < 260; i++) // pines on the mountain rim
        {
            float rimHalf = GroundHalf - 4;
            var pos = new Vector2(rng.NextSingle() * 2 * rimHalf - rimHalf, rng.NextSingle() * 2 * rimHalf - rimHalf);
            if (MathF.Max(MathF.Abs(pos.X), MathF.Abs(pos.Y)) < ExtentUsed - 4) continue;
            foliage.Add(new Foliage(FoliageKind.Pine, pos, 2.4f + rng.NextSingle(), 0, rng.Next()));
        }
        return new Terrain(Seed, GroundHalf, ExtentUsed < Extent ? LegacyStep : Step, Height, RoadDist, foliage, Tint, Rivers);
    }
}
