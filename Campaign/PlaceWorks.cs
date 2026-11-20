using Microsoft.Xna.Framework;

namespace Ironfield;

/// Ground a place's state may build on (docs/SETTLEMENTS.md SM4): a rich town's suburbs along the roads past its gates, a
/// village's extra houses, and a site for each work a town or castle may raise (barracks, granary, training yard, stables,
/// chapel, kennel, watchtower; a town's market hall on its square). Planned once with the map, shown by the state.
public sealed partial class WorldMap
{
    /// Every place's plan before its layout: a place's suburbs and works keep to its own side of its neighbours.
    readonly List<PlacePlan> basePlans = new();
    /// The field plots by cell (set by `BuildTerrain` before the places are planned): no lot stands in a field.
    Dictionary<(int, int), List<FieldPlot>> placeFields = new();

    /// The sites' sizes, in `PlaceState.WorkIds` order (frontage, depth).
    static readonly (float W, float D)[] WorkSize = { (3.6f, 1.3f), (2.8f, 1.7f), (4.0f, 2.2f), (3.6f, 2.6f), (2.6f, 1.6f), (2.4f, 1.8f), (1.2f, 1.2f), (1.9f, 1.1f) };

    /// The places near p, whose ground its suburbs and works must leave alone.
    List<PlacePlan> Neighbours(PlacePlan p) => basePlans.Where(o => o.Id != p.Id && Vector2.Distance(o.Pos, p.Pos) < p.Radius + o.Radius + 20).ToList();

    /// Whether ground at c is p's: nearer p's edge than any neighbour's (each place builds on its own side), in no field.
    bool Owns(PlacePlan p, List<PlacePlan> near, Vector2 c)
    {
        float mine = Vector2.Distance(c, p.Pos) - p.Radius;
        foreach (var o in near) if (Vector2.Distance(c, o.Pos) - o.Radius < MathF.Max(mine + 1.0f, 0.3f)) return false;
        return !InField(placeFields, c, 0.5f);
    }

    /// A castle's plan: the sites for its works (it has no lots else; `World` draws the hold from its footprint).
    PlacePlan LayCastle(PlacePlan p)
    {
        var lots = new List<Lot>();
        LayWorks(p, lots, LocalRoads(p.Pos, p.Radius + 22), Neighbours(p), Array.Empty<Vector2>(), p.Pos);
        return p with { Lots = lots.ToArray() };
    }

    /// One site per `PlaceState.WorkIds`, in order (a zero-sized lot where none fits or the place cannot build it: castles
    /// raise no stables or market): round the place outside its wall, off roads, water and fields, on its own side of its
    /// neighbours, the door toward the place; the watchtower further out. A town's market hall stands on its square, off the
    /// roads that cross it.
    void LayWorks(PlacePlan p, List<Lot> lots, List<(Vector2[] Pts, float Half)> roads, List<PlacePlan> near, Vector2[] ring, Vector2 square)
    {
        bool town = p.Kind == SettlementKind.Town, walled = ring.Length >= 3;
        float edge = walled ? ring.Max(v => Vector2.Distance(v, p.Pos)) : p.Radius;
        int first = lots.Count;
        bool Fits(Lot lot)
        {
            foreach (var c in lot.Corners.Append(lot.Pos))
            {
                if (Wet(c, 0.3f) || OnRoad(roads, c, 1.0f) || !Owns(p, near, c)) return false;
                if (walled ? InRing(ring, c) || RingDist(ring, c) < 1.0f : Vector2.Distance(c, p.Pos) < p.Radius + 0.4f) return false;
            }
            return !lots.Any(o => o.W > 0 && Lot.Overlap(o, lot, 0.5f));
        }
        for (int k = 0; k < PlaceState.WorkIds.Length; k++)
        {
            string id = PlaceState.WorkIds[k];
            var (w, d) = WorkSize[k];
            Lot? got = null;
            if (id == "market")
            {
                if (town)
                    for (int i = 0; i < 40 && got == null; i++)
                    {
                        var at = square + PlacePlan.Dir(p.Front + i * 2.39996f) * (i * 0.04f);
                        var hall = new Lot(at, p.Front + (i % 2) * MathF.PI / 2, w, d, LotUse.Works);
                        if (hall.Corners.All(c => !OnRoad(roads, c, 0.45f) && !Wet(c, 0.2f)) && !lots.Any(o => o.W > 0 && Lot.Overlap(o, hall, 0.1f))) got = hall;
                    }
            }
            else if (town || id != "stables")
            {
                bool far = id == "watchtower";
                for (int i = 0; i < 120 && got == null; i++)
                {
                    float a = p.Front + MathF.PI + i * 2.39996f; // behind the place first, away from its road
                    float rad = (far ? edge + 4 : edge + 1.2f + MathF.Max(w, d) / 2) + i * 0.06f;
                    var at = p.Pos + PlacePlan.Dir(a) * rad;
                    var lot = new Lot(at, Lot.Facing(Vector2.Normalize(p.Pos - at + new Vector2(1e-5f, 0))), w, d, LotUse.Works);
                    if (Fits(lot)) got = lot;
                }
            }
            lots.Add(got ?? new Lot(p.Pos, 0, 0, 0, LotUse.Works));
        }
    }

    /// Cottages along the roads out past a town's gates (shown when it is rich): both sides of each road, from just outside
    /// the wall to 7 beyond it, facing the road.
    void LaySuburbs(PlacePlan p, List<Lot> lots, List<PlacePlan> near, Vector2[] ring)
    {
        var r = new Random(p.Seed ^ 0x5b1b);
        float edge = ring.Max(v => Vector2.Distance(v, p.Pos));
        var roads = LocalRoads(p.Pos, edge + 9); // the roads its suburbs front
        var clear = LocalRoads(p.Pos, edge + 16); // and every road they must keep off
        int made = 0;
        bool Fits(Lot lot)
        {
            foreach (var c in lot.Corners.Append(lot.Pos))
                if (Wet(c, 0.25f) || OnRoad(clear, c, 0.8f) || InRing(ring, c) || RingDist(ring, c) < 0.9f || !Owns(p, near, c)) return false;
            return !lots.Any(o => o.W > 0 && Lot.Overlap(o, lot, 0.1f));
        }
        foreach (var (pts, half) in roads.OrderByDescending(x => x.Half))
        {
            var line = Resample(pts, 0.25f);
            foreach (int side in new[] { 1, -1 })
                for (int i = 0; i + 1 < line.Count && made < 18;)
                {
                    float out_ = InRing(ring, line[i]) ? -1 : RingDist(ring, line[i]);
                    if (out_ < 1.2f || out_ > 7) { i++; continue; }
                    float w = 1.7f + r.NextSingle() * 0.6f, dd = 1.4f + r.NextSingle() * 0.3f;
                    int j = Math.Min(line.Count - 1, i + Math.Max(1, (int)MathF.Round(w / 0.25f)));
                    var t = Vector2.Normalize(line[j] - line[i] + new Vector2(1e-5f, 0));
                    var nrm = new Vector2(-t.Y, t.X) * side;
                    float front = half * PlacePlan.StreetScale(streetPlans, line[i]) * 0.8f + 0.15f;
                    var lot = new Lot((line[i] + line[j]) / 2 + nrm * (front + dd / 2), Lot.Facing(-nrm), Vector2.Distance(line[i], line[j]) - 0.05f, dd, LotUse.Suburb);
                    if (lot.W > 0.8f && Fits(lot)) { lots.Add(lot); made++; i = j + 1 + r.Next(4); }
                    else i += 2;
                }
        }
    }
}
