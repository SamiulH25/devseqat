using Microsoft.Xna.Framework;

namespace Ironfield;

/// What stands on a lot of a place (docs/SETTLEMENTS.md SM2).
public enum LotUse : byte { House, Landmark, Warehouse, Crane, Stall, Yurt, Mill }

/// One building's ground in a place: its middle, its turn (the door faces -`Across`, as `World.Dwelling` builds), its
/// frontage W along the street and its depth D.
public readonly record struct Lot(Vector2 Pos, float Yaw, float W, float D, LotUse Use)
{
    public Vector2 Along => new(MathF.Cos(Yaw), MathF.Sin(Yaw));
    public Vector2 Across => new(-MathF.Sin(Yaw), MathF.Cos(Yaw));
    public Vector2[] Corners
    {
        get
        {
            Vector2 a = Along * (W / 2), d = Across * (D / 2);
            return new[] { Pos - a - d, Pos + a - d, Pos + a + d, Pos - a + d };
        }
    }

    /// The turn of a lot whose door faces `front` (a unit vector).
    public static float Facing(Vector2 front) => MathF.Atan2(front.X, -front.Y);

    /// Whether two lots' ground overlaps (grown by `pad`): separating axes of two rectangles.
    public static bool Overlap(Lot a, Lot b, float pad)
    {
        if (Vector2.DistanceSquared(a.Pos, b.Pos) > MathF.Pow((a.W + a.D + b.W + b.D) / 2 + pad, 2)) return false;
        foreach (var axis in new[] { a.Along, a.Across, b.Along, b.Across })
        {
            float ra = (MathF.Abs(Vector2.Dot(a.Along, axis)) * a.W + MathF.Abs(Vector2.Dot(a.Across, axis)) * a.D) / 2;
            float rb = (MathF.Abs(Vector2.Dot(b.Along, axis)) * b.W + MathF.Abs(Vector2.Dot(b.Across, axis)) * b.D) / 2;
            if (MathF.Abs(Vector2.Dot(b.Pos - a.Pos, axis)) > ra + rb + pad) return false;
        }
        return true;
    }
}

/// A gate in a place's wall: where a road (or the way to the quay) passes it, the way out (bearing), and the wall edge it is on.
public readonly record struct Gate(Vector2 At, float Out, int Edge);

public sealed partial class WorldMap
{
    /// The drawn roads near p as polylines with their half-widths (the map's ribbons; old maps' straight roads else).
    List<(Vector2[] Pts, float Half)> LocalRoads(Vector2 p, float reach)
    {
        var found = new List<(Vector2[], float)>();
        if (RoadLines.Count > 0)
        {
            // Only the stretches that come near: a long road is cut to its runs of segments within reach.
            foreach (var l in RoadLines)
            {
                var run = new List<Vector2>();
                for (int i = 0; i + 1 < l.Points.Length; i++)
                {
                    bool near = SegDist(l.Points[i], l.Points[i + 1], p) < reach;
                    if (near) { if (run.Count == 0) run.Add(l.Points[i]); run.Add(l.Points[i + 1]); }
                    else if (run.Count > 0) { found.Add((run.ToArray(), l.Half)); run = new List<Vector2>(); }
                }
                if (run.Count > 0) found.Add((run.ToArray(), l.Half));
            }
        }
        else
            foreach (var (a, b) in Roads)
                if (LineDist(new[] { a, b }, p) < reach) found.Add((new[] { a, b }, 2.0f));
        return found;
    }

    /// Whether p stands on a road: within `keep` of its half-width (narrowed in a town's streets) from its middle.
    bool OnRoad(List<(Vector2[] Pts, float Half)> roads, Vector2 p, float keep)
    {
        float k = keep * PlacePlan.StreetScale(townPlans, p);
        foreach (var (pts, half) in roads) if (LineDist(pts, p) < half * k) return true;
        return false;
    }

    /// Every town's plan before its layout (their streets narrow): set by `PlanPlaces` before it lays the towns out.
    readonly List<PlacePlan> townPlans = new();

    /// Whether a point lies inside a closed ring.
    public static bool InRing(Vector2[] ring, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y) && p.X < (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                inside = !inside;
        return inside;
    }

    /// The distance from p to a closed ring's edge.
    public static float RingDist(Vector2[] ring, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < ring.Length; i++) best = MathF.Min(best, SegDist(ring[i], ring[(i + 1) % ring.Length], p));
        return best;
    }

    /// Whether a lot keeps off water and off every road's ribbon (the check `TownsLaidOut` makes of every town).
    public bool LotClear(Lot lot, float roadKeep = 0.8f)
    {
        var roads = LocalRoads(lot.Pos, 8);
        foreach (var c in lot.Corners.Append(lot.Pos))
            if (Wet(c, 0.2f) || OnRoad(roads, c, roadKeep)) return false;
        return true;
    }

    /// A town's layout (SM2): a wall ring stretched along the main road and pulled in off the water, gates where the roads
    /// (and the way to the quay) pass it, a market square where the roads meet, the landmark on it, and houses in rows
    /// along the streets, back to back, with lanes between. Ports get warehouses and a crane by the quay; the steppe pitches
    /// yurts outside the bank; the fens raise a windmill.
    PlacePlan LayTown(PlacePlan p)
    {
        var st = p.Look;
        var r = new Random(p.Seed ^ 0x51ab);
        float R = p.Radius * 0.94f;
        var roads = LocalRoads(p.Pos, p.Radius + 8);
        float main = p.Roads.Length > 0 ? p.Roads[0] : p.Front;

        // The ring.
        int n = R > 10 ? 12 : 10;
        var ring = new List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            float a = main + MathF.PI / n + i * MathF.Tau / n + (r.NextSingle() - 0.5f) * 0.16f;
            float rad = R * (0.8f + 0.2f * MathF.Abs(MathF.Cos(a - main))) * (0.94f + 0.06f * r.NextSingle());
            while (Wet(p.Pos + PlacePlan.Dir(a) * rad, 1.2f) && rad > R * 0.4f) rad *= 0.93f;
            ring.Add(p.Pos + PlacePlan.Dir(a) * rad);
        }
        for (int i = 0; i < ring.Count; i++) // an edge that crosses water bends in at its middle
        {
            var a = ring[i]; var b = ring[(i + 1) % ring.Count]; var mid = (a + b) / 2;
            if (!Wet(mid, 1.0f)) continue;
            var to = mid - p.Pos;
            float k = 1;
            while (Wet(p.Pos + to * k, 1.0f) && k > 0.4f) k *= 0.92f;
            ring.Insert(i + 1, p.Pos + to * k);
            i++;
        }
        if (ring.Count(v => !Wet(v, 0.6f)) >= 6) ring.RemoveAll(v => Wet(v, 0.6f)); // a town out on the water keeps to its dry ground
        var Ring = ring.ToArray();

        // The gates: where a road's middle crosses the wall, and toward the quay.
        var gates = new List<Gate>();
        void AddGate(Vector2 at, int edge)
        {
            if (gates.Any(g => Vector2.Distance(g.At, at) < 4.0f)) return;
            var a = Ring[edge]; var b = Ring[(edge + 1) % Ring.Length];
            var nrm = Vector2.Normalize(new Vector2(b.Y - a.Y, a.X - b.X));
            if (Vector2.Dot(nrm, (a + b) / 2 - p.Pos) < 0) nrm = -nrm;
            gates.Add(new Gate(at, MathF.Atan2(nrm.Y, nrm.X), edge));
        }
        for (int e = 0; e < Ring.Length; e++)
        {
            var a = Ring[e]; var b = Ring[(e + 1) % Ring.Length];
            float len = Vector2.Distance(a, b);
            int steps = Math.Max(2, (int)(len / 0.25f));
            float best = float.MaxValue; Vector2 bestAt = a;
            for (int k = 0; k <= steps; k++)
            {
                var q = Vector2.Lerp(a, b, k / (float)steps);
                float d = roads.Count == 0 ? float.MaxValue : roads.Min(x => LineDist(x.Pts, q));
                if (d < 1.0f && d < best) { best = d; bestAt = q; }
                else if (d >= 1.0f && best < 1.0f) { AddGate(bestAt, e); best = float.MaxValue; }
            }
            if (best < 1.0f) AddGate(bestAt, e);
        }
        if (p.Quay is Vector2 quay && Vector2.Distance(quay, p.Pos) < R + 6) // a water gate toward a near quay
        {
            int edge = 0; Vector2 near = Ring[0]; float bd = float.MaxValue;
            for (int e = 0; e < Ring.Length; e++)
            {
                var a = Ring[e]; var b = Ring[(e + 1) % Ring.Length];
                var ab = b - a;
                var q = a + ab * Math.Clamp(Vector2.Dot(quay - a, ab) / ab.LengthSquared(), 0.15f, 0.85f);
                if (Vector2.Distance(q, quay) < bd) { bd = Vector2.Distance(q, quay); near = q; edge = e; }
            }
            AddGate(near, edge);
        }

        // The square, and its landmark on the side away from the roads.
        var square = p.Pos;
        for (int k = 0; k < 8 && Wet(square, 1.2f); k++) square = Vector2.Lerp(square, Ring.Aggregate(Vector2.Zero, (s, v) => s + v) / Ring.Length, 0.3f);
        float sqR = 2.4f;
        var lots = new List<Lot>();
        bool Fits(Lot lot, float margin, bool walled = true)
        {
            foreach (var c in lot.Corners.Append(lot.Pos))
            {
                if (walled && (!InRing(Ring, c) || RingDist(Ring, c) < margin)) return false;
                if (Wet(c, 0.25f)) return false;
                if (OnRoad(roads, c, lot.Use == LotUse.Stall ? 0.35f : 0.8f)) return false;
                if (Vector2.Distance(c, square) < sqR && lot.Use is not (LotUse.Stall or LotUse.Landmark)) return false;
            }
            float clear = lot.Use is LotUse.Warehouse or LotUse.Crane ? 0.9f : 1.7f; // the quay's sheds crowd its gate
            if (gates.Any(g => Vector2.Distance(g.At, lot.Pos) < clear + MathF.Max(lot.W, lot.D) / 2)) return false;
            return !lots.Any(o => Lot.Overlap(o, lot, 0.03f));
        }
        Lot? landmark = null;
        float lw = st.Landmark is Landmark.Cathedral ? 6.0f : 5.0f, ld = 3.0f; // the nave and its tower (World.Chapel at 1.4-1.65 scale)
        for (int k = 0; k < 24 && landmark == null; k++)
        {
            float best = float.MinValue; Lot? pick = null;
            for (int i = 0; i < 16; i++)
            {
                float b = i * MathF.Tau / 16;
                var dir = PlacePlan.Dir(b);
                var lot = new Lot(square + dir * (sqR + ld / 2 + 0.3f + k * 0.4f), Lot.Facing(-dir), lw, ld, LotUse.Landmark);
                if (!Fits(lot, 1.2f)) continue;
                float score = p.Roads.Select(x => MathF.Abs(MathHelper.WrapAngle(x - b))).DefaultIfEmpty(MathF.PI).Min();
                if (score > best) { best = score; pick = lot; }
            }
            landmark = pick;
        }
        for (int k = 0; k < 30 && landmark == null; k++) // a crowded square: a smaller church anywhere near it
        {
            var dir = PlacePlan.Dir(main + MathF.PI / 2 + k * 2.4f);
            var lot = new Lot(square + dir * (sqR + 1.6f + k * 0.3f), Lot.Facing(-dir), 4.2f, 2.6f, LotUse.Landmark);
            if (Fits(lot, 0.8f)) landmark = lot;
        }
        for (int k = 0; k < 200 && landmark == null; k++) // no room by the square: wherever inside the wall it fits
        {
            var at = Vector2.Lerp(square, Ring[k % Ring.Length], 0.15f + 0.05f * (k / Ring.Length));
            var lot = new Lot(at, Lot.Facing(Vector2.Normalize(square - at + new Vector2(0.01f, 0))), 3.8f, 2.4f, LotUse.Landmark);
            if (Fits(lot, 0.6f)) landmark = lot;
        }
        if (landmark is Lot lm) lots.Add(lm);

        // Stalls round the square.
        for (int i = 0; i < 6; i++)
        {
            float b = main + 0.4f + i * MathF.Tau / 6;
            var dir = PlacePlan.Dir(b);
            var lot = new Lot(square + dir * (sqR * 0.72f), Lot.Facing(-dir), 0.85f, 0.6f, LotUse.Stall);
            if (Fits(lot, 1.0f)) lots.Add(lot); // stalls stand at the road's edge, not in its middle
        }

        // The port's warehouses and crane by the quay, wholly inside the wall or wholly outside it.
        bool Astride(Lot lot) => lot.Corners.Any(c => RingDist(Ring, c) < 0.6f) || lot.Corners.Select(c => InRing(Ring, c)).Distinct().Count() > 1;
        if (p.Quay is Vector2 qy)
        {
            var land = Vector2.Normalize(p.Pos - qy);
            for (float k = 0.9f; k < 4; k += 0.3f) // the crane on the first dry ground in from the quay, off the road
            {
                var crane = new Lot(qy + land * k, Lot.Facing(-land), 0.9f, 0.9f, LotUse.Crane);
                if (!Wet(crane.Pos, 0) && !OnRoad(roads, crane.Pos, 0.6f) && !Astride(crane)) { lots.Add(crane); break; }
            }
            int houses = 0;
            for (int k = 0; k < 160 && houses < 3; k++)
            {
                float a = MathF.Atan2(land.Y, land.X) + (r.NextSingle() - 0.5f) * 3.2f;
                var at = qy + PlacePlan.Dir(a) * (1.6f + r.NextSingle() * (3 + k / 25f));
                var face = Vector2.Normalize(qy - at);
                var lot = new Lot(at, Lot.Facing(face), 2.4f + r.NextSingle() * 0.6f, 1.4f, LotUse.Warehouse);
                if (Fits(lot, 0, walled: false) && !Astride(lot))
                { lots.Add(lot); houses++; }
            }
        }

        // Houses in rows along the streets: a row facing the street, one back to back with it, a lane, and on. Then the lanes
        // the map does not draw: out from the square between the roads, round inside the wall, and a ring halfway out.
        var (wMin, wMax, depth, gap, lane) = Rows(st);
        bool yurts = st.House == HouseKind.Yurt;
        var streets = roads.OrderByDescending(x => x.Half).Select(x => (x.Pts, Front: x.Half * 0.62f * 0.8f + 0.06f, Bands: 7)).ToList();
        var ways = p.Roads.Concat(gates.Select(g => MathF.Atan2(g.At.Y - square.Y, g.At.X - square.X))).Select(MathHelper.WrapAngle).OrderBy(x => x).ToList();
        for (int i = 0; i < ways.Count; i++)
        {
            float a0 = ways[i], a1 = i + 1 < ways.Count ? ways[i + 1] : ways[0] + MathF.Tau;
            if (a1 - a0 < 1.1f) continue;
            for (int k = 1, parts = (int)((a1 - a0) / 1.1f); k <= parts; k++)
            {
                var dir = PlacePlan.Dir(a0 + (a1 - a0) * k / (parts + 1));
                var end = square + dir * (sqR + 0.6f);
                while (InRing(Ring, end + dir * 1.8f)) end += dir * 0.4f;
                streets.Add((new[] { square + dir * (sqR + 0.6f), end }, 0.55f, 4));
            }
        }
        if (ways.Count == 0) streets.Add((new[] { square - PlacePlan.Dir(main) * R, square + PlacePlan.Dir(main) * R }, 0.55f, 6));
        var center = Ring.Aggregate(Vector2.Zero, (a, v) => a + v) / Ring.Length;
        streets.Add((Ring.Select(v => v + Vector2.Normalize(center - v) * 3.1f).Append(Ring[0] + Vector2.Normalize(center - Ring[0]) * 3.1f).ToArray(), 0.55f, 2));
        streets.Add((Ring.Select(v => Vector2.Lerp(square, v, 0.55f)).Append(Vector2.Lerp(square, Ring[0], 0.55f)).ToArray(), 0.55f, 2));
        foreach (var (pts, front0, bands) in streets)
        {
            var line = Resample(pts, 0.25f);
            if (line.Count < 2) continue;
            foreach (int side in new[] { -1, 1 })
            {
                float front = front0;
                for (int band = 0; band < bands && lots.Count < 170; band++)
                {
                    bool toward = band % 2 == 0;
                    float near = front, far = front + depth;
                    for (int i = 0; i < line.Count - 1;)
                    {
                        float w = yurts ? 1.4f : wMin + r.NextSingle() * (wMax - wMin), d = yurts ? 1.4f : depth * (0.85f + 0.15f * r.NextSingle());
                        int j = Math.Min(line.Count - 1, i + Math.Max(1, (int)MathF.Round(w / 0.25f)));
                        var a = line[i]; var b = line[j];
                        w = Vector2.Distance(a, b) - 0.06f; // the chord the lot takes: neighbours meet with a crack between
                        if (w < 0.5f) { i = j; continue; }
                        var t = Vector2.Normalize(b - a);
                        var nrm = new Vector2(-t.Y, t.X) * side;
                        var mid = (a + b) / 2;
                        float dist = toward ? near + d / 2 : far - d / 2;
                        var at = mid + nrm * dist;
                        if (Vector2.Distance(at, p.Pos) > R + 1) { i = j; continue; }
                        var lot = new Lot(at, Lot.Facing(toward ? -nrm : nrm), w, d, yurts && Vector2.Distance(at, square) > R * 0.42f ? LotUse.Yurt : LotUse.House);
                        if (Fits(lot, 0.5f)) { lots.Add(lot); i = j + (int)MathF.Round((gap + (yurts ? 0.5f : 0)) / 0.25f); }
                        else i += 2;
                    }
                    front = far + (toward ? 0.12f : lane);
                }
            }
        }

        // The steppe pitches its yurts outside the bank; the fens raise a windmill out in the fields.
        if (st.VillageHouse == HouseKind.Yurt)
            for (int k = 0, placed = 0; k < 120 && placed < 12; k++)
            {
                float a = r.NextSingle() * MathF.Tau;
                var dir = PlacePlan.Dir(a);
                var at = p.Pos;
                for (int s = 0; s < 80 && InRing(Ring, at); s++) at += dir * 0.4f;
                at += dir * (1.4f + r.NextSingle() * 2.4f);
                var lot = new Lot(at, Lot.Facing(-dir), 1.2f, 1.2f, LotUse.Yurt);
                if (Vector2.Distance(at, p.Pos) < p.Radius + 3 && Fits(lot, 0, walled: false) && RingDist(Ring, at) > 1.4f) { lots.Add(lot); placed++; }
            }
        if (st.Landmark == Landmark.Windmill)
            for (int k = 0; k < 40; k++)
            {
                float a = main + MathF.PI / 2 + (k % 2 == 0 ? 0 : MathF.PI) + (r.NextSingle() - 0.5f) * 1.2f;
                var dir = PlacePlan.Dir(a);
                var at = p.Pos + dir * (R + 2.5f + r.NextSingle() * 2);
                var lot = new Lot(at, Lot.Facing(-dir), 1.6f, 1.6f, LotUse.Mill);
                if (Fits(lot, 0, walled: false) && RingDist(Ring, at) > 1.6f && !InRing(Ring, at)) { lots.Add(lot); break; }
            }

        return p with { Ring = Ring, Gates = gates.ToArray(), Lots = lots.ToArray(), Square = square };
    }

    /// How a people lays its town houses: frontage (min, max), the depth of a row, the gap between houses, the lane between rows.
    static (float WMin, float WMax, float Depth, float Gap, float Lane) Rows(PlaceStyle st) => st.House switch
    {
        HouseKind.Tall => (1.05f, 1.4f, 2.0f, 0.04f, 1.1f),
        HouseKind.Stucco => (1.6f, 2.3f, 1.8f, 0.06f, 1.2f),
        HouseKind.Cube => (1.3f, 2.2f, 1.7f, 0f, 0.8f),
        HouseKind.Hall => (2.7f, 3.4f, 1.5f, 0.8f, 1.3f),
        HouseKind.Cabin => (1.6f, 2.1f, 1.45f, 0.6f, 1.2f),
        HouseKind.Chalet => (1.7f, 2.2f, 1.7f, 0.6f, 1.2f),
        HouseKind.Stone => (1.5f, 2.0f, 1.6f, 0.08f, 1.2f),
        _ => (1.4f, 2.0f, 1.6f, 0.06f, 1.2f),
    };

    /// Points every `step` along a polyline.
    static List<Vector2> Resample(Vector2[] pts, float step)
    {
        var o = new List<Vector2>();
        if (pts.Length == 0) return o;
        o.Add(pts[0]);
        float since = 0; // travelled since the last point laid
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            float len = Vector2.Distance(a, b), s = step - since;
            for (; s <= len; s += step) o.Add(Vector2.Lerp(a, b, s / len));
            since = len - (s - step);
        }
        return o;
    }
}
