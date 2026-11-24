using Microsoft.Xna.Framework;
using static Ironfield.FoliageKind;

namespace Ironfield;

/// The dressed map (docs/archive/WORLD_DRESS.md WD1-WD3), the drawn map and drawing only: the ground between the stands gets
/// woods where a noise field runs high, and small cover (shrubs, tussocks, stones, heather, reeds, flowers, lone trees)
/// in drifts, each kind of country with its own plants. On its own dice, after everything else, so nothing else moves;
/// the campaign never reads it.
public sealed partial class WorldMap
{
    /// What grows loose on a kind of country. `Wood`: how readily it grows woods (0 none; 0.7 = wood with glades in it).
    /// `Fill`: the share of its open cells that hold something. `Dry`: how parched its plants are. `Mix`: what stands there, by weight.
    sealed record Cover(float Wood, float Fill, float Dry, params (FoliageKind Kind, int Weight)[] Mix)
    {
        public readonly int TotalWeight = Mix.Sum(m => m.Weight);
    }

    static readonly Dictionary<string, Cover> Covers = new()
    {
        ["Meadow"] = new(0.42f, 0.5f, 0.1f, (Flowers, 5), (Shrub, 4), (Tussock, 3), (Broadleaf, 2), (Rock, 1)),
        ["Farmland"] = new(0.32f, 0.36f, 0.15f, (Shrub, 4), (Flowers, 4), (Broadleaf, 2), (Tussock, 2)),
        ["Forest"] = new(0.85f, 0.6f, 0f, (Shrub, 6), (Broadleaf, 3), (Tussock, 2), (Flowers, 1), (Rock, 1), (DeadTree, 1)),
        ["Hills"] = new(0.42f, 0.52f, 0.3f, (Rock, 4), (Shrub, 3), (Tussock, 3), (Heather, 2), (Pine, 2), (Flowers, 1)),
        ["Marsh"] = new(0.38f, 0.6f, 0.2f, (Reed, 6), (Tussock, 3), (DeadTree, 2), (Shrub, 2)),
        ["Steppe"] = new(0.16f, 0.6f, 0.7f, (Tussock, 9), (Flowers, 3), (Shrub, 2), (Rock, 1)),
        ["Badlands"] = new(0f, 0.46f, 0.9f, (Rock, 5), (Shrub, 3), (Tussock, 3), (DeadTree, 1)),
        ["Coast"] = new(0.4f, 0.5f, 0.35f, (Tussock, 5), (Shrub, 4), (Flowers, 2), (Rock, 2), (Broadleaf, 1)),
        ["Taiga"] = new(0.75f, 0.55f, 0.2f, (Pine, 3), (Shrub, 3), (Rock, 3), (Tussock, 1), (DeadTree, 1)),
        ["Highlands"] = new(0.32f, 0.56f, 0.4f, (Heather, 6), (Rock, 4), (Tussock, 3), (Shrub, 1), (Pine, 1)),
        ["Tundra"] = new(0f, 0.45f, 0.6f, (Rock, 6), (Tussock, 3), (Heather, 2), (Shrub, 1)),
        ["Desert"] = new(0f, 0.3f, 1f, (Rock, 5), (Shrub, 4), (Tussock, 2), (DeadTree, 1)),
        ["Oasis"] = new(0.5f, 0.55f, 0.3f, (Palm, 4), (Shrub, 3), (Tussock, 3), (Reed, 1)),
    };

    const float DressCell = 7;

    /// Lays the cover into `foliage`: one try a 7 m cell that nothing stands in yet, where the ground is `open` (no road,
    /// water, field or site). Small cover comes up to a place's houses; trees stand a few metres further off.
    void Dress(float grow, Func<Vector2, bool> open, List<Foliage> foliage)
    {
        int n = (int)(ExtentUsed / DressCell), side = 2 * n;
        var taken = new bool[side * side];
        foreach (var f in foliage)
        {
            int tx = (int)MathF.Floor(f.Pos.X / DressCell) + n, tz = (int)MathF.Floor(f.Pos.Y / DressCell) + n;
            if ((uint)tx < (uint)side && (uint)tz < (uint)side) taken[tx * side + tz] = true;
        }
        var rows = new List<Foliage>?[side];
        Parallel.For(0, side, xi =>
        {
            int x = xi - n;
            List<Foliage>? row = null;
            for (int zi = 0; zi < side; zi++)
            {
                if (taken[xi * side + zi]) continue;
                int z = zi - n;
                var rng = new Random(Rng.Mix(Seed, 17, x, z)); // each cell its own dice: what else stands on the map never moves the cover
                var p = new Vector2(x + 0.15f + rng.NextSingle() * 0.7f, z + 0.15f + rng.NextSingle() * 0.7f) * DressCell;
                if (!open(p) || OnRidge(p)) continue;
                var land = Lands.Of(ProvinceAt(p).Kind);
                if (!Covers.TryGetValue(land.Kind, out var c)) continue;
                var (town, houses, place) = NearestPlace(p);
                if (town < houses) continue;
                // Trees keep a few metres off a village's houses, more off a town's or castle's walls, so the close view sees it.
                bool trees = town > houses + (place?.Kind == SettlementKind.Village ? 3 : 8) && RoadDist(p) > 4.5f && RiverDist(p) > 4; // a ride cut through a wood: roads and streams stay in sight
                // Trees shrink by a town as the stands' do, so it stands out among them.
                float near = 0.45f + 0.55f * Math.Clamp((town - 12 * grow * grow) / (30 * grow * grow), 0, 1);
                float dry = Math.Clamp(c.Dry + (rng.NextSingle() - 0.5f) * 0.3f, 0, 1);
                // Several woods and glades to a province (the noise's cells are 36 m; a province is some 120 m across).
                float wood = 0.5f + 0.375f * Terrain.Noise(p.X * 0.028f, p.Y * 0.028f, Seed + 83) + 0.125f * Terrain.Noise(p.X * 0.09f, p.Y * 0.09f, Seed + 84) + c.Wood;
                FoliageKind kind;
                if (c.Wood > 0 && wood > 1 && trees) // woodland: closed canopy at its heart, smaller trees and shrubs at its rim
                {
                    bool rim = wood < 1.05f;
                    if (!rim && (x + z & 1) != 0) continue; // the heart: a big tree every other cell, crowns meeting (half the triangles of small ones)
                    near = rim ? near * 0.75f : MathF.Max(near, 0.8f);
                    kind = rim && rng.Next(5) < 2 ? Shrub : land.Kind == "Oasis" ? Palm : land.Kind == "Marsh" && rng.Next(4) == 0 ? DeadTree
                        : land.Conifer || land.Kind == "Forest" && rng.Next(3) == 0 ? Pine : rng.Next(8) == 0 ? Birch : Broadleaf;
                }
                else
                {
                    // Cover gathers in drifts and leaves open lawns between them.
                    float drift = 0.5f + 0.5f * Terrain.Noise(p.X * 0.045f, p.Y * 0.045f, Seed + 85);
                    if (rng.NextSingle() > c.Fill * (0.3f + 1.4f * drift)) continue;
                    int roll = rng.Next(c.TotalWeight), i = 0;
                    while (roll >= c.Mix[i].Weight) roll -= c.Mix[i++].Weight;
                    kind = c.Mix[i].Kind;
                    if (kind is Broadleaf or Pine or Palm or DeadTree && !trees) kind = Shrub;
                    if (kind == Flowers && (Season >= 2 || drift < 0.5f)) kind = Tussock; // blooms in spring and summer, in the drifts' hearts
                }
                float size = kind switch
                {
                    Broadleaf or Pine or Birch => (1.7f + rng.NextSingle() * 1.3f) * grow * near,
                    Palm or DeadTree => (1.5f + rng.NextSingle() * 0.9f) * grow * near,
                    Rock => 0.7f + rng.NextSingle() * rng.NextSingle() * 2.2f, // mostly small, now and then a boulder
                    Flowers => grow,
                    _ => (0.9f + rng.NextSingle() * 0.8f) * grow,
                };
                (row ??= new List<Foliage>()).Add(new Foliage(kind, p, size, 0, rng.Next(), dry));
            }
            rows[xi] = row;
        });
        foreach (var row in rows) if (row != null) foliage.AddRange(row);
    }
}
