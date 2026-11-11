using Microsoft.Xna.Framework;

namespace Ironfield;

/// The land with meaning (docs/WORLD_LIFE.md WL3), layout 4 and drawing only: field plots round villages and towns, and
/// woods grown as stands with edges and clearings rather than an even scatter. None of it is read by the campaign; it
/// lives on the renderable `Terrain` that `BuildTerrain` makes, on its own dice so the rest of the map is untouched.
public sealed partial class WorldMap
{
    /// Field plots round each village and town: a rotated grid of strips in a ring outside the houses, kept off roads,
    /// rivers, lakes, ridges, sites and other settlements. One plot in five is pasture; a town keeps an orchard or two.
    List<FieldPlot> LayFields(float span)
    {
        var plots = new List<FieldPlot>();
        float k = MathF.Sqrt(span);
        foreach (var s in Settlements.Where(s => s.Kind != SettlementKind.Castle))
        {
            var rng = new Random(Seed * 13 + s.Id * 7919);
            bool town = s.Kind == SettlementKind.Town;
            float yaw = rng.NextSingle() * MathF.PI, cw = (11 + rng.NextSingle() * 5) * k, ch = (7 + rng.NextSingle() * 3) * k;
            float r0 = town ? 12.5f : 9.5f, r1 = r0 + (town ? 26 : 22) * k;
            var axis = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
            var across = new Vector2(-axis.Y, axis.X);
            var cells = new List<(Vector2 C, float D)>();
            int n = (int)MathF.Ceiling(r1 / ch) + 1;
            for (int i = -n; i <= n; i++)
                for (int j = -n; j <= n; j++)
                {
                    var c = s.Pos + axis * (i * cw) + across * (j * ch) + axis * (j % 2 == 0 ? 0 : cw * 0.5f); // rows offset like strips
                    float d = Vector2.Distance(c, s.Pos);
                    if (d > r0 + ch * 0.5f && d < r1) cells.Add((c, d));
                }
            int orchards = town ? 1 + rng.Next(2) : 0, left = town ? 9 : 5 + rng.Next(3);
            foreach (var (c, d) in cells.OrderBy(x => x.D + rng.NextSingle() * ch * 2)) // a ragged ring, not a full one
            {
                if (left <= 0) break;
                // Sizes and angles vary plot to plot, so the country is not laid out with a ruler.
                var half = new Vector2(cw * (0.3f + rng.NextSingle() * 0.17f), ch * (0.3f + rng.NextSingle() * 0.15f));
                float turn = (rng.NextSingle() - 0.5f) * 0.3f;
                var plot = new FieldPlot(c, new Vector2(axis.X * MathF.Cos(turn) - axis.Y * MathF.Sin(turn), axis.X * MathF.Sin(turn) + axis.Y * MathF.Cos(turn)), half, FieldKind.Grain, 0, rng.Next());
                if (!FieldClear(plot, s))
                {
                    // A road or a stream through the cell: keep whichever half of it is clear.
                    var halves = new[] { -1f, 1f }.Select(side => plot with { Center = c + axis * side * cw * 0.25f, Half = new Vector2(cw * 0.2f, half.Y) })
                        .Where(h => FieldClear(h, s)).ToList();
                    if (halves.Count == 0) continue;
                    plot = halves[rng.Next(halves.Count)];
                }
                var kind = orchards > 0 && d < r0 + cw ? FieldKind.Orchard : rng.Next(10) switch
                {
                    0 or 1 => FieldKind.Pasture, 2 or 3 or 4 => FieldKind.Grain, 5 or 6 => FieldKind.Green, 7 or 8 => FieldKind.Ploughed, _ => FieldKind.Fallow,
                };
                if (kind == FieldKind.Orchard) orchards--;
                int hedged = 0;
                for (int side = 0; side < 4; side++) if (rng.Next(100) < 33) hedged |= 1 << side;
                plots.Add(plot with { Kind = kind, Hedged = hedged });
                left--;
            }
        }
        return plots;
    }

    /// Whether a plot lies clear of the roads, water, ridges, sites and every settlement but its own.
    bool FieldClear(FieldPlot f, Settlement own)
    {
        if (MathF.Max(MathF.Abs(f.Center.X), MathF.Abs(f.Center.Y)) > ExtentUsed - 12 || OnRidge(f.Center)) return false;
        foreach (var (u, v) in new[] { (0f, 0f), (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f), (0f, -1f), (0f, 1f), (-1f, 0f), (1f, 0f) })
        {
            var p = f.At(u * f.Half.X, v * f.Half.Y);
            if (RoadDist(p) < 3.2f || RiverDist(p) < Rivers.Select(r => r.Width).DefaultIfEmpty(0).Max() / 2 + 3 || InLake(p, 2.5f)) return false;
        }
        foreach (var s in Settlements) if (s != own && Vector2.Distance(s.Pos, f.Center) < (s.Kind == SettlementKind.Village ? 10 : 15) + f.Half.X) return false;
        foreach (var s in Sites) if (Vector2.Distance(s.Pos, f.Center) < 7 + f.Half.X) return false;
        return true;
    }

    /// A look-up of field plots by 32 m cell (trees are kept out of them).
    static Dictionary<(int, int), List<FieldPlot>> FieldCells(List<FieldPlot> plots)
    {
        var cells = new Dictionary<(int, int), List<FieldPlot>>();
        foreach (var f in plots)
        {
            float r = f.Half.Length() + 2;
            for (int x = (int)MathF.Floor((f.Center.X - r) / 32); x <= (int)MathF.Floor((f.Center.X + r) / 32); x++)
                for (int z = (int)MathF.Floor((f.Center.Y - r) / 32); z <= (int)MathF.Floor((f.Center.Y + r) / 32); z++)
                {
                    if (!cells.TryGetValue((x, z), out var l)) cells[(x, z)] = l = new();
                    l.Add(f);
                }
        }
        return cells;
    }

    static bool InField(Dictionary<(int, int), List<FieldPlot>> cells, Vector2 p, float pad) =>
        cells.TryGetValue(((int)MathF.Floor(p.X / 32), (int)MathF.Floor(p.Y / 32)), out var l) && l.Any(f => f.Inside(p, pad));

    /// Layout 4 woods: the province's trees gathered into a few stands (dense at the heart, thinning to an edge of bushes),
    /// with clearings where a noise field dips; a third of the broadleaves are birches outside the hills.
    void GrowStands(Province p, int trees, float radius, float grow, Random rng, Func<Vector2, bool> clear, List<Foliage> foliage)
    {
        int stands = Math.Max(1, trees / 26);
        for (int s = 0; s < stands; s++)
        {
            float a = rng.NextSingle() * MathF.Tau, d = MathF.Sqrt(rng.NextSingle()) * radius * 0.8f;
            var centre = p.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            float sr = (p.Kind == "Forest" ? 20 : 11) * MathF.Sqrt(grow) * (0.7f + rng.NextSingle() * 0.6f);
            int count = trees / stands;
            for (int i = 0; i < count; i++)
            {
                float ta = rng.NextSingle() * MathF.Tau, td = rng.NextSingle() * sr; // thickest at the heart
                var pos = centre + new Vector2(MathF.Cos(ta), MathF.Sin(ta)) * td;
                if (!clear(pos) || ProvinceAt(pos) != p || OnRidge(pos)) continue;
                if (Terrain.Noise(pos.X * 0.06f, pos.Y * 0.06f, Seed + 77) < 0.28f) continue; // a clearing
                float edge = td / sr;
                if (rng.NextSingle() < edge * edge * 0.55f) continue; // the stand thins out toward its edge
                if (edge > 0.85f) { foliage.Add(new Foliage(FoliageKind.Bush, pos, (1.8f + rng.NextSingle()) * grow, 0, rng.Next())); continue; }
                bool pine = p.Kind == "Hills" || p.Kind == "Forest" && rng.NextSingle() < 0.35f || rng.NextSingle() < 0.15f;
                bool birch = !pine && p.Kind != "Hills" && rng.NextSingle() < 0.12f;
                float town = Settlements.Min(st => Vector2.Distance(st.Pos, pos)), near = 0.45f + 0.55f * Math.Clamp((town - 12 * grow * grow) / (30 * grow * grow), 0, 1);
                float size = (1.7f + rng.NextSingle() * 1.3f) * grow * near * (1.0f - 0.3f * edge); // three sizes in a stand, smaller at its edge
                foliage.Add(new Foliage(pine ? FoliageKind.Pine : birch ? FoliageKind.Birch : FoliageKind.Broadleaf, pos, size, 0, rng.Next()));
            }
        }
    }
}
