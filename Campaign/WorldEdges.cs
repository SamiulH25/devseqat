using Microsoft.Xna.Framework;
using static Ironfield.FoliageKind;

namespace Ironfield;

/// The water's edge and the roadside (docs/WORLD_DRESS.md WD4), the drawn map and drawing only: stones and driftwood on
/// the beach, marram on the bank behind it, rocks standing in the surf, boats drawn up by the ports and the fishing
/// villages; trees and hedges along the roads near a town, milestones, a wayside shrine or well on the long roads.
/// Own dice, laid before the small cover (which keeps clear of what stands here); the campaign never reads it.
public sealed partial class WorldMap
{
    /// Lays the shore and the roadsides into `foliage`. `ground`: no site, river, lake or field there.
    void DressEdges(float grow, Func<Vector2, bool> ground, List<Foliage> foliage)
    {
        var rng = new Random(Seed * 19 + 11);
        Shore(grow, ground, foliage, rng);
        Boats(foliage, rng);
        Roadside(grow, ground, foliage, rng);
    }

    /// Nearest place and how far its houses reach (a village's 11 m, a town's or castle's 14 m).
    (float Dist, float Houses, Settlement? Place) NearestPlace(Vector2 p)
    {
        (float, float, Settlement?) best = (float.MaxValue, 0, null);
        foreach (var st in Settlements)
            if (Vector2.Distance(st.Pos, p) is var d && d < best.Item1) best = (d, st.Kind == SettlementKind.Village ? 11 : 14, st);
        return best;
    }

    /// Near a port's pier (from the beach out to the ship at its head).
    bool ByQuay(Vector2 p, float r)
    {
        foreach (var (shore, berth) in Quays.Values)
        {
            var ab = berth - shore;
            float u = Math.Clamp(Vector2.Dot(p - shore, ab) / ab.LengthSquared(), 0, 1);
            if (Vector2.Distance(shore + ab * u, p) < r) return true;
        }
        return false;
    }

    /// One try a 4 m cell across the shore band: rocks in the surf off the rocky stretches, the beach's stones and
    /// driftwood above the tide, marram tussocks on the bank behind it.
    void Shore(float grow, Func<Vector2, bool> ground, List<Foliage> foliage, Random rng)
    {
        const float Cell = 4;
        int n = (int)(ExtentUsed / Cell);
        for (int x = -n; x < n; x++)
            for (int z = -n; z < n; z++)
            {
                var p = new Vector2(x + 0.15f + rng.NextSingle() * 0.7f, z + 0.15f + rng.NextSingle() * 0.7f) * Cell;
                float land = Landness(p), roll = rng.NextSingle();
                if (land is < 0.27f or > 0.95f) continue;
                if (!ground(p) || RoadDist(p) < 3.5f || ByQuay(p, 6)) continue;
                var (town, houses, _) = NearestPlace(p);
                if (town < houses + 2) continue;
                var kind = Lands.Of(ProvinceAt(p).Kind);
                // Rocky stretches and sandy ones alternate along the coast (noise cells some 40 m long).
                float rocky = Terrain.Noise(p.X * 0.025f, p.Y * 0.025f, Seed + 91);
                bool wet = Height(p.X, p.Y) < SeaLevel + 0.05f;
                if (land < 0.45f) // in the surf: only off the rocky stretches, standing out of the water
                {
                    if (wet && rocky > 0.6f && roll < 0.3f) foliage.Add(new Foliage(Rock, p, 1.3f + rng.NextSingle() * 1.2f, 0, rng.Next(), kind.Hot ? 0.6f : 0.1f));
                }
                else if (land < 0.8f) // the beach above the tide: stones where it is rocky, driftwood here and there
                {
                    if (wet) continue;
                    if (roll < 0.06f && kind.Kind != "Desert") foliage.Add(new Foliage(Driftwood, p, (0.8f + rng.NextSingle() * 0.5f) * grow, 0, rng.Next()));
                    else if (roll < 0.06f + 0.3f * Math.Max(0, rocky - 0.45f)) foliage.Add(new Foliage(Rock, p, 0.45f + rng.NextSingle() * 0.4f, 0, rng.Next(), kind.Hot ? 0.7f : 0.15f));
                }
                else if (roll < (kind.Kind is "Tundra" or "Desert" ? 0.2f : 0.45f)) // the bank behind the beach: marram grass, bleached by salt and wind
                    foliage.Add(new Foliage(Marram, p, (1.0f + rng.NextSingle() * 0.5f) * grow, 0, rng.Next(), Math.Clamp(0.45f + (kind.Hot ? 0.3f : 0) + rng.NextSingle() * 0.15f, 0, 1)));
            }
    }

    /// Boats drawn up on the beach: three by a port's pier, one or two by each village on the sea.
    void Boats(List<Foliage> foliage, Random rng)
    {
        foreach (var (shore, berth) in Quays.OrderBy(q => q.Key).Select(q => q.Value))
        {
            var along = Vector2.Normalize(berth - shore);
            var across = new Vector2(-along.Y, along.X);
            for (int i = 0; i < 3; i++)
                if (Beach(shore + across * ((i == 1 ? -1 : 1) * (7 + 3 * i)), along) is Vector2 at) foliage.Add(new Foliage(Rowboat, at, 1, 0, rng.Next()));
        }
        foreach (var v in Settlements.Where(s => s.Kind == SettlementKind.Village))
        {
            // The way to the water: the nearest of sixteen directions that reaches the sea within 34 m.
            Vector2? seaward = null;
            for (float d = 12; d <= 34 && seaward == null; d += 2)
                for (int k = 0; k < 16 && seaward == null; k++)
                {
                    var dir = new Vector2(MathF.Cos(k * MathF.Tau / 16), MathF.Sin(k * MathF.Tau / 16));
                    if (Landness(v.Pos + dir * d) < 0.5f) seaward = dir;
                }
            if (seaward is not Vector2 sd) continue;
            var side = new Vector2(-sd.Y, sd.X);
            for (int i = 1 + rng.Next(2); i > 0; i--)
                if (Beach(v.Pos + side * (i == 1 ? 4 : -5), sd) is Vector2 at && !ByQuay(at, 4)) foliage.Add(new Foliage(Rowboat, at, 1, 0, rng.Next()));
        }

        // Down `dir` from `from` to the dry beach above the tide (landness 0.6-0.75), or null if there is none within 40 m.
        Vector2? Beach(Vector2 from, Vector2 dir)
        {
            if (Landness(from) < 0.6f) dir = -dir;
            for (float d = 0; d < 40; d += 0.5f)
            {
                var q = from + dir * d;
                float l = Landness(q);
                if (l is >= 0.6f and <= 0.75f && Height(q.X, q.Y) > SeaLevel + 0.1f && RoadDist(q) > 6 && RiverDist(q) > 3 && !ByQuay(q, 3)) return q;
                if (l < 0.6f) return null;
            }
            return null;
        }
    }

    /// Along the roads: a row of trees with a hedge across the way for the last 50 m into a town or castle, a milestone
    /// every 45 m, and halfway along each long road between seats a wayside shrine or a well.
    void Roadside(float grow, Func<Vector2, bool> ground, List<Foliage> foliage, Random rng)
    {
        bool Free(Vector2 p, float road) => Landness(p) > 0.95f && RoadDist(p) > road && ground(p) && !OnRidge(p);
        int line = 0;
        foreach (var r in RoadLines)
        {
            line++;
            var pts = r.Points;
            float length = 0;
            for (int i = 1; i < pts.Length; i++) length += Vector2.Distance(pts[i - 1], pts[i]);
            float s = 0, nextTree = 3, nextMile = 20 + rng.NextSingle() * 20;
            bool shrine = !r.Village && length > 90, well = (line & 1) == 0;
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1];
                var seg = pts[i] - a;
                float len = seg.Length();
                if (len < 1e-3f) continue;
                var dir = seg / len;
                var side = new Vector2(-dir.Y, dir.X);
                for (float u = 0; u < len; u += 1)
                {
                    float at = s + u;
                    var p = a + dir * u;
                    var (town, houses, place) = NearestPlace(p);
                    if (town < houses + 4) continue;
                    if (at >= nextTree) // the approach to a seat: trees on one side, a hedge on the other
                    {
                        nextTree = at + 2.4f;
                        if (place is { Kind: not SettlementKind.Village } && town < houses + 50)
                        {
                            int k = (int)(at / 2.4f);
                            int flip = place.Id & 1;
                            // (Distances are to the road's coarse copy, up to 1.2 m off the drawn line: the margins allow for it.)
                            float near = 0.45f + 0.55f * Math.Clamp((town - 12 * grow * grow) / (30 * grow * grow), 0, 1); // small by the walls, as the stands' trees are
                            if (k % 3 == 0 && town > houses + 8 && Free(p + side * (r.Half + 3.2f) * (flip * 2 - 1), r.Half + 1.6f))
                                foliage.Add(new Foliage(Lands.Of(ProvinceAt(p).Kind).Conifer ? Pine : Broadleaf, p + side * (r.Half + 3.2f) * (flip * 2 - 1), (1.5f + rng.NextSingle() * 0.5f) * grow * near, 0, rng.Next()));
                            var hedge = p - side * (r.Half + 2.4f) * (flip * 2 - 1);
                            if (Free(hedge, r.Half + 0.9f)) foliage.Add(new Foliage(Shrub, hedge, (0.8f + rng.NextSingle() * 0.25f) * grow, 0, rng.Next(), 0.1f));
                        }
                    }
                    if (at >= nextMile && !r.Village)
                    {
                        nextMile = at + 45;
                        var m = p + side * (r.Half + 0.9f);
                        if (NearestPlace(m).Dist > 22 && RoadDist(m) > r.Half + 0.5f && Landness(m) > 0.95f && ground(m)) foliage.Add(new Foliage(Milestone, m, grow, 0, rng.Next()));
                    }
                    if (shrine && at >= length / 2)
                    {
                        shrine = false;
                        var w = p - side * (r.Half + 3.2f);
                        if (NearestPlace(w).Dist > 27 && Free(w, r.Half + 2)) foliage.Add(new Foliage(well ? Well : Wayside, w, grow, 0, rng.Next()));
                    }
                }
                s += len;
            }
        }
    }
}
