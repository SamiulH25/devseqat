using Microsoft.Xna.Framework;

namespace Ironfield;

/// A named place on the battlefield worth fighting for (docs/TERRAIN.md T5).
public enum PlaceKind { Crest, Knoll, WoodEdge, Ford, Bridge, Hedge, Village }

/// A place and how good it is to hold, to shoot from and to hide in (0 = no use).
public readonly record struct Place(PlaceKind Kind, Vector2 Pos, float Hold, float Shoot, float Hide);

/// Battle: the ground survey (docs/TERRAIN.md T5). Built once, the first time the AI or a sergeant asks, from the heights,
/// the ground layer, the rivers and the features. The general and the sergeants pick their ground from it.
public partial class Battle
{
    List<Place>? places;
    /// The good places on this field (crests, knolls, wood edges, fords, bridges, hedges, the village).
    public IReadOnlyList<Place> Places => places ??= Survey();

    List<Place> Survey()
    {
        var found = new List<Place>();
        var t = Terrain;
        if (t.IsFlat && t.Features.Count == 0 && t.Rivers.Count == 0) return found;
        float reach = MapRadius * 0.9f;
        var ring = new Vector2[8];
        for (int k = 0; k < 8; k++) ring[k] = new Vector2(MathF.Cos(k * MathF.Tau / 8), MathF.Sin(k * MathF.Tau / 8));
        var rises = new List<Place>();
        var edges = new List<Place>();
        for (float z = -reach; z <= reach; z += 8)
            for (float x = -reach; x <= reach; x += 8)
            {
                var p = new Vector2(x, z);
                if (t.WaterDepth(p) > 0) continue;
                // A rise: higher than the ring 14 m round it (all round: a knoll; most of the way: a crest).
                float h = t.Height(p), sum = 0;
                int lower = 0;
                foreach (var d in ring) { float hr = t.Height(p + d * 14); sum += hr; if (h - hr > 1) lower++; }
                float rise = h - sum / 8;
                if (rise > 1.2f && lower >= 5) rises.Add(new Place(lower == 8 ? PlaceKind.Knoll : PlaceKind.Crest, p, rise, rise, 0));
                // A wood edge: in cover, with open ground close by.
                if (t.CoverAt(p) >= 0.5f)
                {
                    int open = 0;
                    foreach (var d in ring) if (t.CoverAt(p + d * 8) < 0.25f) open++;
                    if (open >= 2) edges.Add(new Place(PlaceKind.WoodEdge, p, 1.5f, 0.5f, 2));
                }
            }
        found.AddRange(Thin(rises, 22));
        found.AddRange(Thin(edges, 25));
        foreach (var r in t.Rivers)
        {
            foreach (var f in r.Fords) found.Add(new Place(PlaceKind.Ford, f, 1, 0, 0));
            foreach (var (at, _) in r.Bridges) found.Add(new Place(PlaceKind.Bridge, at, 1.5f, 0, 0));
        }
        foreach (var f in t.Features)
            if (f.Kind == FeatureKind.Hedge) found.Add(new Place(PlaceKind.Hedge, (f.A + f.B) / 2, 1, 2, 0.5f));
        if (t.Layout == FieldLayout.Village) found.Add(new Place(PlaceKind.Village, t.VillageAt, 2.5f, 1, 1));
        return found;
    }

    /// Keep the best of places closer together than `apart` (best = highest Hold).
    static IEnumerable<Place> Thin(List<Place> list, float apart)
    {
        var kept = new List<Place>();
        foreach (var p in list.OrderByDescending(p => p.Hold))
            if (kept.All(k => Vector2.DistanceSquared(k.Pos, p.Pos) > apart * apart)) kept.Add(p);
        return kept;
    }

    /// The best place within `radius` of `near` by `score` (a near place beats a slightly better far one); null if none scores.
    public Place? BestPlace(Vector2 near, float radius, Func<Place, float> score)
    {
        Place? best = null;
        float bs = 0;
        foreach (var p in Places)
        {
            float d = Vector2.Distance(p.Pos, near);
            if (d > radius) continue;
            float s = score(p) * (1 - 0.5f * d / radius);
            if (s > bs) { bs = s; best = p; }
        }
        return best;
    }

    /// Where a sergeant told to hold or take `point` actually stands: the best place to hold within 25 m, else the point.
    Vector2 SnapToGround(Vector2 point) => BestPlace(point, 25, p => p.Kind is PlaceKind.Crest or PlaceKind.Knoll or PlaceKind.WoodEdge or PlaceKind.Village ? p.Hold : 0)?.Pos ?? point;

    // ---------- the general's eye for ground ----------

    /// Where each side's line began to hold its good ground (null: not holding), when, and how many men it had then.
    readonly Vector2?[] holdAt = new Vector2?[2];
    readonly float[] holdSince = new float[2];
    readonly int[] holdMen = new int[2];
    readonly bool[] holdDone = new bool[2];

    /// How long a side holding good ground waits for the enemy to come before it goes to him: a minute and more,
    /// less for an aggressive realm (`BattleOptions.EnemyAggression`, the enemy's temper).
    float Patience(int team) => Math.Clamp(70 / (team == 1 ? Options.EnemyAggression : 1), 25, 150);

    /// Is the line at `front` on better ground than the enemy at `foeFront`: clearly higher, or in a wood or village
    /// while they stand in the open?
    bool GoodGround(Vector2 front, Vector2 foeFront)
    {
        if (Terrain.Height(front) - Terrain.Height(foeFront) > 2.5f) return true;
        bool Covered(Vector2 p) => Terrain.CoverAt(p) >= 0.5f || Terrain.GroundAt(p) == GroundKind.Street;
        return Covered(front) && !Covered(foeFront);
    }

    /// Hold good ground (T5): while the line stands on better ground than the enemy and has not charged, it stays where it is
    /// and lets the enemy come up to it. It gives that up when its patience runs out or it bleeds while waiting.
    /// Returns where the line holds (null: it advances as usual).
    Vector2? HoldsGround(int team, Vector2 front, int foe)
    {
        if (holdDone[team] || charged[team]) return null;
        int men = SquadsOf(team).Sum(s => s.Alive);
        if (holdAt[team] is Vector2 at)
        {
            if (Time - holdSince[team] > Patience(team) || men < holdMen[team] * 0.85f)
            {
                holdDone[team] = true;
                Log(team == 1 ? "The enemy comes down off his ground." : "Your line leaves its ground and advances.");
                return null;
            }
            return at;
        }
        if (Time > 20 || !GoodGround(front, centroid[foe])) return null; // only the ground it starts on
        holdAt[team] = front; holdSince[team] = Time; holdMen[team] = men;
        Log(team == 1 ? "The enemy holds his ground and waits for you." : "Your line holds its ground.");
        return front;
    }

    /// An anchor the general would put in deep water moves to the nearest crossing (a ford or bridge on its side),
    /// so the line goes over where it can instead of wading (T5 "take the crossing").
    Vector2 DryAnchor(Vector2 anchor)
    {
        if (Terrain.Rivers.Count == 0 || Terrain.WaterDepth(anchor) < 0.6f) return anchor;
        var crossing = BestPlace(anchor, 200, p => p.Kind is PlaceKind.Ford or PlaceKind.Bridge ? 1 : 0);
        return crossing?.Pos ?? anchor;
    }

    /// How bad an enemy company's ground is for a horse charge: woods, mud, water, or well uphill (0 = good going).
    float HorseGoing(Squad horse, Squad target)
    {
        var g = Terrain.GroundAt(target.Center);
        float bad = g is GroundKind.Wood or GroundKind.DenseWood or GroundKind.Mud or GroundKind.Water ? 1 : 0;
        if (Terrain.Height(target.Center) - Terrain.Height(horse.Center) > 3) bad += 0.5f;
        return bad;
    }

    /// The side the horse goes round, and how far out: the flank with a wood on it (the horse rides round its outside,
    /// out of sight), else the right. Decided once, at the start.
    readonly (int Sign, float Out)?[] horseFlank = new (int, float)?[2];

    (int Sign, float Out) HorseFlank(int team, Vector2 front, Vector2 dir, Vector2 side)
    {
        if (horseFlank[team] is { } f) return f;
        f = (-1, 0);
        var wood = Places.Where(p => p.Kind == PlaceKind.WoodEdge)
            .Select(p => (p, lat: Vector2.Dot(p.Pos - front, side), fwd: Vector2.Dot(p.Pos - front, dir)))
            .Where(w => MathF.Abs(w.lat) is > 30 and < 100 && w.fwd is > -10 and < 60)
            .OrderBy(w => MathF.Abs(w.lat)).FirstOrDefault();
        if (wood.p.Kind == PlaceKind.WoodEdge) f = (MathF.Sign(wood.lat), MathF.Max(0, MathF.Abs(wood.lat) + 18 - 26));
        horseFlank[team] = f;
        return f;
    }
}
