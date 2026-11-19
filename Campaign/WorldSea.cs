using Microsoft.Xna.Framework;

namespace Ironfield;

/// A sea lane (docs/WORLDMAP.md WM7): the two ports it joins (settlement ids) and the way a ship sails it, berth to berth.
public sealed record SeaLane(int A, int B, Vector2[] Way, float Length);

/// WorldMap: the drawn map's ports and sea lanes (docs/WORLDMAP.md WM7). A port is a settlement a drawn lane ends at; its
/// quay runs from the beach out to the nearest water deep enough for a ship. Made from the design each time the map is
/// built (nothing is saved), so a save from before the ships gains them.
public sealed partial class WorldMap
{
    public readonly List<SeaLane> SeaLanes = new();
    /// A port's quay by settlement id: where its pier leaves the beach, and the berth at its end where ships lie.
    public readonly Dictionary<int, (Vector2 Shore, Vector2 Berth)> Quays = new();
    /// The landness under which the water floats a ship (about 3 m deep: `DesignedHeight`), and over which the beach is dry.
    const float DeepWater = 0.15f, DryBeach = 0.6f;
    /// The landness a ship keeps under: clear of the surf line (the drawn coast is 0.5).
    const float Afloat = 0.3f;

    /// Does the straight line from a to b keep to the water?
    bool OverWater(Vector2 a, Vector2 b)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / DesignCell));
        for (int i = 0; i <= steps; i++) if (Landness(Vector2.Lerp(a, b, i / (float)steps)) > Afloat + 0.05f) return false; // a little leave: neighbouring cells always join
        return true;
    }

    void DesignSeaLanes(WorldDesign d)
    {
        foreach (var l in d.Sealanes)
        {
            var pts = l.Points.Select(Drawn).ToList();
            Settlement a = Settlements.MinBy(s => Vector2.DistanceSquared(s.Pos, pts[0]))!, b = Settlements.MinBy(s => Vector2.DistanceSquared(s.Pos, pts[^1]))!;
            if (a == b || QuayOf(a) is not { } qa || QuayOf(b) is not { } qb) continue;
            // Only the drawn ends count: spread out by DesignScale the drawn line cuts headlands (15 of the 27 did), so a
            // ship's way is found over the water, berth to berth.
            // A drawn line that crossed land the sea does not go round is no lane (Marrakin to Orda Tash, over the isthmus:
            // 13 days round the continent).
            float drawn = 0;
            for (int i = 1; i < pts.Count; i++) drawn += Vector2.Distance(pts[i - 1], pts[i]);
            var way = PlanBySea(qa.Berth, qb.Berth, drawn * 2);
            if (way.Count == 1 && !OverWater(qa.Berth, qb.Berth)) continue;
            way.Insert(0, qa.Berth);
            float len = 0;
            for (int i = 1; i < way.Count; i++) len += Vector2.Distance(way[i - 1], way[i]);
            SeaLanes.Add(new SeaLane(a.Id, b.Id, way.ToArray(), len));
        }
        foreach (int id in Quays.Keys.Where(id => SeaLanes.All(l => l.A != id && l.B != id)).ToList()) Quays.Remove(id); // a quay no lane kept
    }

    /// The quay of a place by the sea: the nearest deep water round it, and the beach on the line back to it.
    (Vector2 Shore, Vector2 Berth)? QuayOf(Settlement s)
    {
        if (Quays.TryGetValue(s.Id, out var q)) return q;
        for (float r = 2; r < 80; r += 2)
        {
            Vector2 dir = default;
            float deepest = DeepWater;
            for (int k = 0; k < 32; k++)
            {
                var way = new Vector2(MathF.Cos(k * MathF.Tau / 32), MathF.Sin(k * MathF.Tau / 32));
                if (Landness(s.Pos + way * r) is float land && land < deepest) { deepest = land; dir = way; }
            }
            if (dir == default) continue;
            Vector2 berth = s.Pos + dir * r, shore = berth;
            while (Landness(shore) < DryBeach && Vector2.DistanceSquared(shore, s.Pos) > 1) shore -= dir;
            return Quays[s.Id] = (shore, berth);
        }
        return null; // no sea within reach: no ships call here
    }
}
