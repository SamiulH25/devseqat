using Microsoft.Xna.Framework;

namespace Ironfield;

/// The campaign world's geography (docs/CAMPAIGN.md 3): provinces, settlements, roads and terrain, all generated from a seed.
/// Map units: the ground spans +-TerrainHalf (1 unit = 0.75 km). The same seed always gives the same map, so saves store only the seed
/// plus what changes (ownership, recruits, parties).
public sealed partial class WorldMap
{
    /// Where provinces live on a new map; mountains fill the rim out to the terrain's edge (about 930 km across, docs/archive/WORLD.md W2).
    public const float Extent = 620;
    /// The extent of the map before W2 (about 590 km across): saves made on it keep it (CampaignState.MapExtent).
    public const float PreviousExtent = 392;
    /// Half the side of a new map's ground: the provinces, plus a rim of mountains to close the world in.
    public const float TerrainHalf = Extent + Rim;
    /// Ground vertex spacing on the campaign map (coarser than a battlefield's 2.5 m). Small (pre-enlargement) maps keep the
    /// finer grid they were drawn with.
    public const float Step = 4f, LegacyStep = 2.5f;
    /// The mountain rim between the provinces' extent and the edge of the ground.
    const float Rim = 56;
    /// The extent campaigns generated before the map was enlarged used; saves keep their own map (CampaignState.MapExtent).
    public const float LegacyExtent = 196;
    /// How far apart provinces stand on the legacy map. A bigger world spreads them by (extent/196)^0.3, which lands
    /// the full map on about Provinces of them: more provinces, and more ground between them, without every road
    /// becoming a day's march.
    public const float ProvinceSpacing = 66;
    /// Roughly how many provinces the enlarged map packs in, and how many the original map had (the baseline every
    /// balance number that scales with the world is quoted against).
    public const int ProvinceCount = 115;
    public const int LegacyProvinces = 26;
    /// Province kinds; the first four are the original map's (indices kept), the rest come with the climates of layout 2.
    public static readonly string[] Kinds = { "Meadow", "Farmland", "Forest", "Hills", "Marsh", "Steppe", "Badlands" };

    public readonly int Seed;
    public readonly List<Province> Provinces = new();
    public readonly List<Settlement> Settlements = new();
    public readonly List<(Vector2 A, Vector2 B)> Roads = new();
    /// Rivers wind across the land (two; four on a layout-2 map); where a road meets one there is a bridge (a ford on the battlefield).
    public readonly List<River> Rivers = new();
    /// Places of interest between the settlements (docs/archive/SIDE_CONTENT.md S2): ruins, shrines, mines and the rest.
    public readonly List<Site> Sites = new();
    /// Mountain ridges inside the land (layout 2): high, slow ground, crossed by road at passes (Path = the crest, Width =
    /// how far its slopes reach, Fords = the passes). Old maps have none.
    public readonly List<River> Ridges = new();
    /// 0-3: spring to winter. The ground's colours follow it (set before BuildTerrain).
    public int Season;
    /// The extent this map was generated with (provinces live within it; mountains close the world beyond).
    public float ExtentUsed = Extent;
    /// How big this map is against the one the balance numbers were tuned on (WorldMap.LegacyProvinces provinces).
    /// Parties and events that should keep pace with the size of the world scale with this, so the balance numbers stay
    /// legible against the original map and a bigger map is not emptier or duller than the small one was.
    public float ProvinceScale => MathF.Max(0.1f, Provinces.Count / (float)LegacyProvinces);
    /// How many parties (lords, brigands, pilgrims) the world raises against the original map. Up to the pre-W2 map it is
    /// ProvinceScale; past it the count follows the land's area, so a bigger map is as crowded per league as that one was
    /// (provinces spread out as the map grows, and parties counted by province grew sparse; docs/archive/WORLD.md W2).
    public float PartyScale => ExtentUsed <= PreviousExtent + 1 ? ProvinceScale
        : PreviousProvinces / (float)LegacyProvinces * (ExtentUsed / PreviousExtent) * (ExtentUsed / PreviousExtent);
    /// About how many provinces the pre-W2 map had (the anchor PartyScale grows from).
    public const int PreviousProvinces = 60;
    /// How far apart this map's places stand against the original map's (1 = the same). Distances that mean "the next
    /// village" or "a short ride" scale with this, so a lord on a big map still finds things to do nearby.
    public float Spread => MathF.Pow(MathF.Max(0.05f, ExtentUsed / LegacyExtent), 0.3f);
    /// Half the side of this map's ground: the provinces, plus the mountain rim (an old save keeps the small world it had).
    public float GroundHalf => ExtentUsed + Rim;
    readonly List<(Vector2 C, float R, float H)> hills = new();
    /// The look-up grid for ProvinceAt, RoadDist and RiverDist, built on first use (once the roads and rivers are laid).
    MapIndex? index;
    MapIndex Index => index ??= new MapIndex(Provinces, Roads, Rivers, GroundHalf + 64);
    MapIndex? ridgeIndex;
    MapIndex RidgeIndex => ridgeIndex ??= new MapIndex(new List<Province>(), new List<(Vector2, Vector2)>(), Ridges, GroundHalf + 64);

    WorldMap(int seed) { Seed = seed; }

    /// The map a fresh campaign uses. `extent` is half the side the provinces may use (Extent for new campaigns,
    /// LegacyExtent for saves made before the map grew), so a save keeps the geography it was played on.
    /// How the map is laid out: 0 = the original three realms by fixed bands (every save from before docs/archive/WORLD.md W1 keeps it),
    /// 1 = each data realm grows from its home in Data/factions.json, 2 = that plus climates, ridges and more rivers (W2),
    /// 3 = all the data realms (seven, W3) at their homes in the data, 4 = that plus roads routed over the land, lakes and
    /// village ponds (docs/WORLD_LIFE.md WL1-WL2). Layouts 1-2 keep the three homes they were made with.
    public const int LegacyLayout = 0, CurrentLayout = 4;
    /// The three realms' homes (x, z, reach) on layout 1-2 maps, as factions.json had them before the realms became seven.
    static readonly (float X, float Z, float Reach)[] ThreeRealmHomes = { (-0.8f, 0.45f, 0), (0.8f, 0.45f, 0), (0, -0.85f, -20) };
    public int Layout;

    /// `realms` = how many data realms the map shares out (a save's DataRealms; 0 = all of them, as a new campaign has).
    public static WorldMap Generate(int seed, float extent = Extent, int layout = CurrentLayout, int realms = 0)
    {
        var m = new WorldMap(seed) { ExtentUsed = extent, Layout = layout };
        int realmCount = layout < 3 ? 3 : realms > 0 ? Math.Min(realms, Factions.All.Count) : Factions.All.Count;
        var rng = new Random(seed);
        var names = Names.Get();
        // Maps once shuffled a 60-name list here: keep those draws so an old seed still gives the same rivers and towns.
        for (int i = 0; i < 60; i++) rng.Next();
        // Names come from their own generator, so changing the name lists never moves a river or a town.
        var namer = new PlaceNamer(names, seed);
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
        var byX = centres.OrderBy(c => c.X).ToList();
        int[]? owners = layout >= 1 ? GrowRealms(byX, extent, span, layout, realmCount) : null;
        foreach (var (c, i) in byX.Select((c, i) => (c, i)))
        {
            int kind = rng.NextSingle() switch { < 0.35f => 0, < 0.6f => 1, < 0.85f => 2, _ => 3 };
            if (MathF.Max(MathF.Abs(c.X), MathF.Abs(c.Y)) > extent * 0.75f && rng.Next(2) == 0) kind = 3; // hills toward the rim
            if (north.Contains(c) && kind == 1) kind = 2; // the north is forest, not farmland
            if (layout >= 2) kind = Climate(c, extent, seed, rng.NextSingle(), kind);
            int faction = owners?[i] ?? (north.Contains(c) ? 2 : south.IndexOf(c) < south.Count / 2 ? 0 : 1);
            m.Provinces.Add(new Province { Id = i, Name = namer.Next(Factions.CultureOf(faction)), Kind = Kinds[kind], Center = c, Faction = faction });
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
        var capitals = layout >= 1
            ? Enumerable.Range(0, realmCount).Select(f => m.Provinces.Where(p => p.Faction == f).OrderBy(p => Vector2.Distance(p.Center, HomeOf(f, extent, layout))).FirstOrDefault()).ToHashSet()
            : new HashSet<Province?>
            {
                m.Provinces.Where(p => p.Faction == 0).OrderBy(p => p.Center.X).First(),
                m.Provinces.Where(p => p.Faction == 1).OrderBy(p => p.Center.X).Last(),
                m.Provinces.Where(p => p.Faction == 2).OrderBy(p => p.Center.Y).FirstOrDefault(),
            };
        foreach (var p in m.Provinces)
        {
            bool town = capitals.Contains(p) || p.Id % 2 == 0;
            m.Settlements.Add(new Settlement { Id = id++, Province = p.Id, Faction = p.Faction, Culture = Factions.CultureOf(p.Faction), Name = p.Name, Pos = p.Center,
                Kind = town ? SettlementKind.Town : SettlementKind.Castle });
            int villages = 2 + rng.Next(2);
            for (int v = 0, tries = 0; v < villages && tries < 200; tries++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = (16 + rng.NextSingle() * 16) * span;
                var pos = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (m.Settlements.Any(s => Vector2.Distance(s.Pos, pos) < 12 * span) || MathF.Abs(pos.X) > extent || MathF.Abs(pos.Y) > extent) continue;
                m.Settlements.Add(new Settlement { Id = id++, Province = p.Id, Faction = p.Faction, Culture = Factions.CultureOf(p.Faction), Name = namer.Next(Factions.CultureOf(p.Faction)), Pos = pos, Kind = SettlementKind.Village });
                m.Roads.Add((p.Center, pos));
                v++;
            }
        }
        // Rivers: from one edge to the far one, wandering, kept clear of settlements; bridges where roads cross.
        for (int k = 0; k < (layout >= 2 ? 4 : 2); k++)
        {
            bool northSouth = k % 2 == 0;
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
            m.Rivers.Add(layout >= 4 ? new River(path, 4.6f, bridges.ToArray(), 1.5f) : new River(path, 2.6f, bridges.ToArray(), 1.1f)); // layout 4: wide enough to read on the map
        }
        // Hill provinces get real hills.
        foreach (var p in m.Provinces.Where(p => p.Kind == "Hills"))
            for (int i = 0; i < 3; i++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = (10 + rng.NextSingle() * 30) * span;
                m.hills.Add((p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, (16 + rng.NextSingle() * 14) * span, 5 + rng.NextSingle() * 5));
            }
        if (layout >= 2) m.RaiseRidges(rng, span);
        // Layout 4 (docs/WORLD_LIFE.md WL1-WL2): lakes and village ponds, then roads routed over the land (WorldRoads.cs).
        if (layout >= 4) m.PlaceLakes(new Random(seed * 31 + 7), span); // own dice: lakes leave the rest of the map as layout 3 drew it
        m.LayRoads(layout >= 4);
        m.PlaceSites(namer, span);
        return m;
    }

    /// The country a province lies in on a layout-2 map: cold forest in the north, open steppe in the east, dry badlands
    /// in the south, fens in the south-west, highlands in the middle; elsewhere the old mix (`kind`). Edges wander with noise.
    static int Climate(Vector2 c, float extent, int seed, float roll, int kind)
    {
        float x = c.X / extent + Terrain.Noise(c.X * 0.01f, c.Y * 0.01f, seed + 11) * 0.12f;
        float z = c.Y / extent + Terrain.Noise(c.X * 0.01f, c.Y * 0.01f, seed + 12) * 0.12f;
        if (z < -0.45f) return roll < 0.5f ? 2 : roll < 0.75f ? 3 : 0;                              // the north: forest and hills
        if (x > 0.55f) return roll < 0.65f ? 5 : roll < 0.85f ? 0 : 3;                              // the east: steppe
        if (z > 0.55f && x > -0.35f) return roll < 0.5f ? 6 : roll < 0.75f ? 0 : roll < 0.9f ? 3 : 1; // the south: badlands
        if (x < -0.45f && z > 0.3f) return roll < 0.5f ? 4 : roll < 0.8f ? 1 : 0;                   // the south-west: fens
        if (MathF.Abs(x) < 0.25f && MathF.Abs(z) < 0.25f) return roll < 0.5f ? 3 : roll < 0.75f ? 2 : 0; // the highlands
        return kind;
    }

    /// Two or three mountain ridges between the settlements, each crossed by the roads that meet it (the passes). A ridge
    /// wanders like a river but stops short of the edges, and keeps well clear of every settlement.
    void RaiseRidges(Random rng, float span)
    {
        int count = 2 + rng.Next(2);
        for (int k = 0; k < count; k++)
        {
            float a = rng.NextSingle() * MathF.Tau, len = ExtentUsed * (0.45f + rng.NextSingle() * 0.35f), ph = rng.NextSingle() * MathF.Tau;
            var start = new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * ExtentUsed * 0.6f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var side = new Vector2(-dir.Y, dir.X);
            var pts = new List<Vector2>();
            float height = 16 + rng.NextSingle() * 8;
            for (float t = 0; t <= len; t += 4)
            {
                var p = start + dir * t + side * MathF.Sin(t * 0.012f / span + ph) * 30 * span;
                if (MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y)) > ExtentUsed - 30) break;
                foreach (var st in Settlements)
                {
                    var away = p - st.Pos;
                    float d = away.Length();
                    if (d < 10 * span) p = st.Pos + (d > 0.01f ? away / d : Vector2.UnitX) * 10 * span;
                }
                // Pushed off one settlement onto another: the ridge breaks here and may rise again further on.
                if (Settlements.Any(st => Vector2.Distance(st.Pos, p) < 9.5f * span)) { AddRidge(pts, span, height); pts.Clear(); continue; }
                pts.Add(p);
            }
            AddRidge(pts, span, height);
        }
        ridgeIndex = null;
    }

    /// A ridge from its crest points (too short a stretch is dropped), with a pass wherever a road crosses it.
    void AddRidge(List<Vector2> pts, float span, float height)
    {
        if (pts.Count < 20) return;
        var path = pts.ToArray();
        var passes = new List<Vector2>();
        for (int i = 0; i + 1 < path.Length; i++)
            foreach (var (ra, rb) in Roads)
                if (Cross(path[i], path[i + 1], ra, rb) is Vector2 x) passes.Add(x);
        Ridges.Add(new River(path, 8 * span, passes.ToArray(), height));
    }

    /// Distance to the nearest ridge crest (float.MaxValue on a map without ridges).
    public float RidgeDist(Vector2 p) => Ridges.Count == 0 ? float.MaxValue : RidgeIndex.RiverDist(p) ?? Ridges.Min(r => r.Dist(p));

    /// How high the ridges stand at p (0 away from them; low at a pass).
    float RidgeHeight(Vector2 p)
    {
        if (Ridges.Count == 0) return 0;
        float d = RidgeDist(p), best = 0;
        foreach (var r in Ridges)
        {
            if (d > r.Width) break;
            float rd = r.Dist(p);
            if (rd > r.Width) continue;
            float t = 1 - rd / r.Width, h = r.Depth * t * t * (3 - 2 * t);
            float pass = r.Fords.Length == 0 ? float.MaxValue : r.Fords.Min(f => Vector2.Distance(f, p));
            if (pass < r.Width * 1.2f) h *= 0.25f + 0.75f * (pass / (r.Width * 1.2f)) * (pass / (r.Width * 1.2f));
            best = MathF.Max(best, h);
        }
        return best;
    }

    /// On a ridge's slopes, away from the passes: slow going (the passes keep the road's pace).
    public bool OnRidge(Vector2 p) => Ridges.Count > 0 && RidgeDist(p) < Ridges[0].Width * 0.6f;

    /// Where a data realm's land grows from on a map of this extent and layout, and how much further it reaches.
    static Vector2 HomeOf(int f, float extent, int layout) =>
        (layout < 3 ? new Vector2(ThreeRealmHomes[f].X, ThreeRealmHomes[f].Z) : new Vector2(Factions.All[f].Home[0], Factions.All[f].Home[1])) * extent * 0.92f;
    static float ReachOf(int f, int layout) => layout < 3 ? ThreeRealmHomes[f].Reach : Factions.All[f].Reach;

    /// The realm of each province (layout 1): every realm grows from the province nearest its home along the links between
    /// neighbouring provinces, the cheapest claim winning (distance less the realm's reach). Growing over the links keeps each
    /// realm's land in one piece; provinces no link reaches go to the nearest claim in a straight line.
    static int[] GrowRealms(List<Vector2> centres, float extent, float span, int layout, int realms)
    {
        int n = centres.Count;
        var links = new List<int>[n];
        for (int i = 0; i < n; i++) links[i] = new List<int>();
        for (int i = 0; i < n; i++)
            foreach (int j in Enumerable.Range(0, n).Where(j => j != i).OrderBy(j => Vector2.Distance(centres[i], centres[j])).Take(3))
                if (Vector2.Distance(centres[i], centres[j]) < 160 * span)
                {
                    if (!links[i].Contains(j)) links[i].Add(j);
                    if (!links[j].Contains(i)) links[j].Add(i);
                }
        var cost = Enumerable.Repeat(float.MaxValue, n).ToArray();
        var owner = Enumerable.Repeat(-1, n).ToArray();
        var open = new PriorityQueue<int, float>();
        var seeds = new HashSet<int>();
        for (int f = 0; f < realms; f++)
        {
            var home = HomeOf(f, extent, layout);
            int seed = Enumerable.Range(0, n).Where(i => !seeds.Contains(i)).OrderBy(i => Vector2.Distance(centres[i], home)).First();
            seeds.Add(seed);
            cost[seed] = -ReachOf(f, layout) * span;
            owner[seed] = f;
            open.Enqueue(seed, cost[seed]);
        }
        while (open.TryDequeue(out int i, out float at))
        {
            if (at > cost[i]) continue;
            foreach (int j in links[i])
            {
                if (seeds.Contains(j)) continue;
                float c = at + Vector2.Distance(centres[i], centres[j]);
                if (c < cost[j]) { cost[j] = c; owner[j] = owner[i]; open.Enqueue(j, c); }
            }
        }
        for (int i = 0; i < n; i++)
            if (owner[i] < 0)
                owner[i] = Enumerable.Range(0, n).Where(j => owner[j] >= 0).OrderBy(j => cost[j] + Vector2.Distance(centres[i], centres[j])).Select(j => owner[j]).First();
        return owner;
    }

    /// Which sites each kind of country holds (the first kinds are the likeliest).
    static string[] SiteKindsFor(string province) => province switch
    {
        "Hills" => new[] { "ruin", "stones", "mine", "watchtower", "hermit", "spring" },
        "Forest" => new[] { "ruin", "hermit", "spring", "shrine", "stones", "watchtower" },
        "Farmland" => new[] { "mill", "shrine", "battlefield", "watchtower", "spring", "ruin" },
        "Marsh" => new[] { "shrine", "hermit", "ruin", "spring", "stones", "mill" },
        "Steppe" => new[] { "stones", "battlefield", "watchtower", "ruin", "shrine", "spring" },
        "Badlands" => new[] { "mine", "ruin", "watchtower", "stones", "hermit", "spring" },
        _ => new[] { "shrine", "battlefield", "mill", "spring", "ruin", "watchtower" },
    };

    /// One or two sites per province, off the roads and clear of settlements and rivers. They use their own random numbers,
    /// so adding them moved nothing else on the map.
    void PlaceSites(PlaceNamer namer, float span)
    {
        var rng = new Random(Seed * 17 + 101);
        int id = 0;
        foreach (var p in Provinces)
        {
            int want = rng.NextSingle() < 0.5f ? 1 : 2;
            var kinds = SiteKindsFor(p.Kind);
            var taken = new List<string>();
            for (int tries = 0; tries < 60 && taken.Count < want; tries++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = (14 + rng.NextSingle() * 26) * span;
                var pos = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                // The likelier kinds come first: the square favours the head of the list.
                string kind = kinds[(int)(rng.NextSingle() * rng.NextSingle() * kinds.Length)];
                if (taken.Contains(kind) || MathF.Abs(pos.X) > ExtentUsed - 8 || MathF.Abs(pos.Y) > ExtentUsed - 8) continue;
                if (ProvinceAt(pos) != p || RoadDist(pos) < 4 || RiverDist(pos) < (kind is "mill" or "spring" ? 4 : 6) || RidgeDist(pos) < 6 * span) continue;
                if (Settlements.Any(s => Vector2.Distance(s.Pos, pos) < 9 * span) || Sites.Any(x => Vector2.Distance(x.Pos, pos) < 10 * span)) continue;
                if (kind is "mill" or "spring" && RiverDist(pos) > 30 * span && rng.NextSingle() < 0.5f) continue; // mills and springs like water
                Sites.Add(new Site { Id = id++, Province = p.Id, Kind = kind, Pos = pos, Name = Site.NameFor(kind, p.Name, namer, rng) });
                taken.Add(kind);
            }
        }
    }

    static Vector2? Cross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
    {
        Vector2 r = p2 - p, s = q2 - q;
        float den = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(den) < 1e-6f) return null;
        float t = ((q.X - p.X) * s.Y - (q.Y - p.Y) * s.X) / den, u = ((q.X - p.X) * r.Y - (q.Y - p.Y) * r.X) / den;
        return t is >= 0 and <= 1 && u is >= 0 and <= 1 ? p + r * t : null;
    }

    public float RiverDist(Vector2 p) => Index.RiverDist(p) ?? RiverScan(p);
    public float RiverScan(Vector2 p) => Rivers.Count == 0 ? float.MaxValue : Rivers.Min(r => r.Dist(p));

    // ---------- queries ----------

    public Province ProvinceAt(Vector2 p) => Index.ProvinceAt(p) ?? ProvinceScan(p);

    /// The full scans the index answers for; kept for points off the grid and for the test that checks the two agree.
    public Province ProvinceScan(Vector2 p)
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

    public float RoadDist(Vector2 p) => Index.RoadDist(p) ?? RoadScan(p);

    public float RoadScan(Vector2 p)
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
        RoadDist(p) < 2.5f ? roadBonus : RiverDist(p) < 1.6f || InLake(p) ? 0.35f : OnRidge(p) ? 0.45f
        : ProvinceAt(p).Kind switch { "Forest" => 0.7f, "Hills" => 0.8f, "Marsh" => 0.6f, "Steppe" => 1.1f, "Badlands" => 0.85f, _ => 1f };

    /// Battle ground for a fight at p: the province's kind, with a road if one runs close.
    public TerrainParams BattleGround(Vector2 p, float wet = 0, int season = -1)
    {
        var t = OnRidge(p) ? TerrainParams.Hills : ProvinceAt(p).Kind switch
        {
            "Forest" => TerrainParams.Forest, "Hills" => TerrainParams.Hills, "Farmland" => TerrainParams.Farmland,
            "Marsh" => TerrainParams.Marsh, "Steppe" => TerrainParams.Steppe, "Badlands" => TerrainParams.Badlands, _ => TerrainParams.Meadow,
        };
        bool road = RoadDist(p) < 6, river = RiverDist(p) < 12;
        // The field's shape from what is on the map here (docs/archive/TERRAIN.md T7): a road meeting a river is a river line
        // with its bridge, a village gives a village between the lines, a ridge a crest or (near a road) a pass.
        var layouts = t.Layouts;
        if (road && river && t != TerrainParams.Marsh) layouts = new[] { FieldLayout.RiverLine };
        else if (Settlements.Any(s => s.Kind == SettlementKind.Village && Vector2.Distance(s.Pos, p) < VillageNear) && t != TerrainParams.Marsh && t != TerrainParams.Forest) layouts = new[] { FieldLayout.Village };
        else if (OnRidge(p)) layouts = road ? new[] { FieldLayout.Pass } : new[] { FieldLayout.Ridge, FieldLayout.Ridge, FieldLayout.TwoHills };
        return t with { Road = road, River = river || t == TerrainParams.Marsh, Layouts = layouts, Wet = wet, Season = season };
    }

    /// How near a village a fight must be to be fought among its houses.
    public const float VillageNear = 8;

    public float Height(float x, float z)
    {
        float h = LandHeight(x, z);
        foreach (var r in Rivers)
        {
            var at = new Vector2(x, z);
            float d = r.Dist(at);
            if (d < r.Width / 2 + 3) h -= r.Carve(at, d);
        }
        foreach (var l in Lakes) h -= l.Carve(new Vector2(x, z));
        return h;
    }

    /// The land before rivers and lakes cut into it (what roads are routed over: the water is barred to them anyway).
    float LandHeight(float x, float z)
    {
        float h = Terrain.Noise(x * 0.012f, z * 0.012f, Seed) * 3 + Terrain.Noise(x * 0.035f, z * 0.035f, Seed + 1) * 0.8f;
        foreach (var (c, r, hh) in hills)
        {
            float d = Vector2.Distance(new Vector2(x, z), c) / r;
            if (d < 1) h += hh * (1 - d * d) * (1 - d * d);
        }
        float edge = MathF.Max(MathF.Abs(x), MathF.Abs(z));
        float t = Math.Clamp((edge - (ExtentUsed - 10)) / (GroundHalf - ExtentUsed + 10), 0, 1);
        if (Layout >= 4) // graded: rolling foothills first, then the range climbing to its peaks (no wall at the edge of the land)
            h += 7 * MathHelper.SmoothStep(0, 1, Math.Clamp(t / 0.35f, 0, 1)) * (0.6f + 0.8f * Terrain.Noise(x * 0.03f, z * 0.03f, Seed + 12))
                + MathF.Pow(MathHelper.SmoothStep(0, 1, Math.Clamp((t - 0.2f) / 0.8f, 0, 1)), 1.4f) * (15 + 9 * Terrain.Noise(x * 0.02f, z * 0.02f, Seed + 2));
        else h += t * t * (3 - 2 * t) * (24 + 10 * Terrain.Noise(x * 0.02f, z * 0.02f, Seed + 2)); // mountains close the world
        float rim = RimAt(edge);
        if (rim > 0) // crags and peaks on the rim, beyond where anyone goes (the land in play keeps its heights)
            h += rim * rim * (Layout >= 4 ? 0.5f : 1) * (18 * Ridged(x * 0.045f, z * 0.045f, Seed + 4) + 7 * Ridged(x * 0.11f, z * 0.11f, Seed + 5));
        if (Ridges.Count > 0) h += RidgeHeight(new Vector2(x, z)) * (0.85f + 0.3f * Terrain.Noise(x * 0.05f, z * 0.05f, Seed + 3));
        return h;
    }

    /// How far out on the mountain rim a point is: 0 at the edge of the land in play (and inside it), 1 at the world's end.
    float RimAt(float edge) => Math.Clamp((edge - ExtentUsed - 4) / (GroundHalf - ExtentUsed - 4), 0, 1);
    /// Sharp-crested noise (0..1): ridges where plain noise crosses its middle.
    static float Ridged(float x, float z, int seed) => 1 - MathF.Abs(Terrain.Noise(x, z, seed) * 2 - 1);

    /// Ground tint: patchwork fields in farmland, dark forest floor, dry hills, grey rock and snow on the rim (visual only).
    public Vector3 Tint(Vector2 p, Vector3 ground)
    {
        var c = ground * KindTint(p);
        float rim = RimAt(MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y)));
        if (rim > 0)
        {
            float n = Terrain.Noise(p.X * 0.08f, p.Y * 0.08f, Seed + 6), h = Height(p.X, p.Y);
            // A graded range: warm brown foothills, warm grey rock, cool blue-grey heights; strata bands, and gullies
            // (steep ground) darker so the faces read as modelled rock rather than one flat grey.
            float up = Math.Clamp(h / 34, 0, 1), strata = Ridged(p.X * 0.05f + h * 0.08f, p.Y * 0.05f, Seed + 9);
            var rock = Vector3.Lerp(new Vector3(0.5f, 0.45f, 0.36f), new Vector3(0.56f, 0.53f, 0.49f), MathHelper.SmoothStep(0, 1, up * 1.6f));
            rock = Vector3.Lerp(rock, new Vector3(0.5f, 0.54f, 0.6f), MathHelper.SmoothStep(0, 1, (up - 0.5f) * 2));
            float steep = Math.Clamp((MathF.Abs(Height(p.X + 1, p.Y) - Height(p.X - 1, p.Y)) + MathF.Abs(Height(p.X, p.Y + 1) - Height(p.X, p.Y - 1))) / 3.5f, 0, 1);
            rock *= (0.82f + 0.3f * n) * (1.04f - 0.3f * steep) * (0.94f + 0.12f * strata);
            var foot = Vector3.Lerp(c, new Vector3(0.42f, 0.44f, 0.27f), 0.5f); // scrubby foothills between grass and rock
            c = Vector3.Lerp(c, foot, MathHelper.SmoothStep(0, 1, rim * 6));
            c = Vector3.Lerp(c, rock, MathHelper.SmoothStep(0, 1, rim * 3 - 0.3f));
            float snow = Math.Clamp((h - 30 + 4 * n) / 8, 0, 1) * MathHelper.SmoothStep(0, 1, rim * 1.6f) * (1 - 0.6f * steep); // snow slides off the steep faces
            c = Vector3.Lerp(c, new Vector3(0.9f, 0.92f, 0.95f), snow);
        }
        if (Ridges.Count > 0 && RidgeDist(p) is float rd && rd < Ridges[0].Width) c = Vector3.Lerp(c, new Vector3(0.62f, 0.6f, 0.58f), 0.6f * (1 - rd / Ridges[0].Width)); // bare rock
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
            case "Marsh": return new(0.76f, 0.88f, 0.82f);
            case "Steppe": return new(1.14f, 1.08f, 0.78f);
            case "Badlands": return new(1.26f, 1.0f, 0.7f);
            default: return Vector3.One;
        }
    }

    /// The renderable terrain: heights, roads, map-scale trees and the settlements as 3D models.
    public Terrain BuildTerrain()
    {
        var rng = new Random(Seed + 7);
        var foliage = new List<Foliage>();
        float span = ExtentUsed / LegacyExtent, grow0 = MathF.Sqrt(span);
        foreach (var s in Settlements)
            foliage.Add(new Foliage(s.Kind switch { SettlementKind.Town => FoliageKind.Town, SettlementKind.Castle => FoliageKind.Castle, _ => FoliageKind.Village },
                s.Pos, s.Kind == SettlementKind.Village ? 1.8f : 2.5f, 0, s.Id * 31 + Seed)); // a little larger than life beside the trees (WL3)
        foreach (var site in Sites)
            foliage.Add(new Foliage(site.Kind switch
            {
                "ruin" => FoliageKind.Ruin, "shrine" => FoliageKind.Shrine, "stones" => FoliageKind.Stones, "battlefield" => FoliageKind.Field,
                "mine" => FoliageKind.Mine, "hermit" => FoliageKind.Hut, "watchtower" => FoliageKind.Watch, "spring" => FoliageKind.Spring, _ => FoliageKind.Mill,
            }, site.Pos, 2.0f, 0, site.Id * 53 + Seed));
        float half = GroundHalf - 6;
        var fields = Layout >= 4 ? LayFields(span) : new List<FieldPlot>();
        var fieldCells = FieldCells(fields);
        var standRng = new Random(Seed * 5 + 3); // layout 4 woods: their own dice
        bool Clear(Vector2 p) => Settlements.All(s => Vector2.Distance(s.Pos, p) > 9 * span) && Sites.All(s => Vector2.Distance(s.Pos, p) > 5) && RoadDist(p) > 2.5f && RiverDist(p) > 3 && !InLake(p, 2)
            && MathF.Abs(p.X) < half && MathF.Abs(p.Y) < half && !InField(fieldCells, p, 1.5f);
        foreach (var f in fields.Where(f => f.Kind == FieldKind.Orchard)) // rows of small round fruit trees
        {
            var orng = new Random(f.Seed);
            for (float u = -f.Half.X + 1.6f; u < f.Half.X - 1; u += 3.3f)
                for (float v = -f.Half.Y + 1.6f; v < f.Half.Y - 1; v += 3.3f)
                    foliage.Add(new Foliage(FoliageKind.Broadleaf, f.At(u, v), 1.15f + orng.NextSingle() * 0.2f, 0, orng.Next()));
        }
        foreach (var p in Provinces)
        {
            // Woods scale with the ground they stand on (area), so the bigger map stays as wooded as the old one. Trees grow
            // a little larger too, so a whole-map view reads as woodland without holding four times the geometry.
            float grow = MathF.Sqrt(span);
            int trees = (int)(p.Kind switch { "Forest" => 90, "Hills" => 26, "Farmland" => 6, "Marsh" => 30, "Steppe" => 2, "Badlands" => 4, _ => 14 } * span * span * 1.4f);
            float radius = (p.Kind == "Forest" ? 48 : 60) * span;
            if (Layout >= 4) { GrowStands(p, trees, radius, grow, standRng, Clear, foliage); continue; }
            for (int i = 0; i < trees; i++)
            {
                float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * radius;
                var pos = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (!Clear(pos) || ProvinceAt(pos) != p) continue;
                if (OnRidge(pos)) continue;
                bool pine = p.Kind == "Hills" || rng.NextSingle() < 0.25f;
                // Trees by a town are smaller, so the town stands out among them rather than under them.
                float town = Settlements.Min(s => Vector2.Distance(s.Pos, pos)), near = 0.55f + 0.45f * Math.Clamp((town - 9 * span) / (16 * span), 0, 1);
                foliage.Add(new Foliage(pine ? FoliageKind.Pine : FoliageKind.Broadleaf, pos, (2.2f + rng.NextSingle() * 1.2f) * grow * near, 0, rng.Next()));
            }
            if (p.Kind == "Farmland")
                for (int i = 0; i < 24; i++) // hedgerows
                {
                    var pos = p.Center + new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * 45 * span;
                    if (Clear(pos)) foliage.Add(new Foliage(FoliageKind.Bush, pos, (2 + rng.NextSingle()) * grow, 0, rng.Next()));
                }
        }
        foreach (var r in Ridges) // pines on the ridges' lower slopes
            for (int i = 0; i < r.Path.Length; i += 2)
            {
                var pos = r.Path[i] + new Vector2(rng.NextSingle() * 2 - 1, rng.NextSingle() * 2 - 1) * r.Width;
                float d = r.Dist(pos);
                if (d > r.Width * 0.45f && d < r.Width && Clear(pos)) foliage.Add(new Foliage(FoliageKind.Pine, pos, (2.2f + rng.NextSingle()) * grow0, 0, rng.Next()));
            }
        for (int i = 0; i < 260; i++) // pines on the mountain rim
        {
            float rimHalf = GroundHalf - 4;
            var pos = new Vector2(rng.NextSingle() * 2 * rimHalf - rimHalf, rng.NextSingle() * 2 * rimHalf - rimHalf);
            if (MathF.Max(MathF.Abs(pos.X), MathF.Abs(pos.Y)) < ExtentUsed - 4) continue;
            foliage.Add(new Foliage(FoliageKind.Pine, pos, 2.4f + rng.NextSingle(), 0, rng.Next()));
        }
        var terrain = new Terrain(Seed, GroundHalf, ExtentUsed < Extent ? LegacyStep : Step, Height, RoadDist, foliage, Tint, Rivers);
        terrain.RoadLines.AddRange(RoadLines);
        terrain.Lakes.AddRange(Lakes);
        terrain.MapBridges.AddRange(Bridges);
        terrain.Fields.AddRange(fields);
        terrain.MapSeason = Season;
        return terrain;
    }
}

/// Hands out place names without repeats: the culture's own list first, then the common list, then built names
/// ("Ash" + "ford"), and as a last resort a numbered one.
public sealed class PlaceNamer
{
    readonly Names names;
    readonly Random rng;
    readonly Dictionary<string, Queue<string>> pools = new();
    public readonly HashSet<string> Used = new();

    public PlaceNamer(Names names, int seed) { this.names = names; rng = new Random(seed * 31 + 7); }

    Queue<string> Pool(string key, List<string> list)
    {
        if (!pools.TryGetValue(key, out var q)) pools[key] = q = new Queue<string>(list.OrderBy(_ => rng.Next()));
        return q;
    }

    public string Next(string culture)
    {
        foreach (var q in new[] { Pool(culture, names.PlacesOf(culture)), Pool("", names.Places) })
            while (q.Count > 0) { var n = q.Dequeue(); if (Used.Add(n)) return n; }
        for (int i = 0; i < 200 && names.Prefixes.Count > 0 && names.Suffixes.Count > 0; i++)
        {
            var n = names.Prefixes[rng.Next(names.Prefixes.Count)] + names.Suffixes[rng.Next(names.Suffixes.Count)];
            if (Used.Add(n)) return n;
        }
        for (int i = 2; ; i++) if (Used.Add($"Newstead {i}")) return $"Newstead {i}";
    }
}
