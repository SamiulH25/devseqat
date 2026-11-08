using Microsoft.Xna.Framework;

namespace Ironfield;

/// The shape a battlefield is built to (docs/TERRAIN.md T2): what lies between and around the two armies.
/// The armies meet along Z = 0; team 0 deploys at negative Z, team 1 at positive Z.
public enum FieldLayout { Open, Ridge, TwoHills, Broken, Fen, RiverLine, WoodedFlank, ForestClearing, Village, Hedgerows, Pass }

/// Terrain: field layouts. Each one shapes the heights (`LayoutHeight`) and paints its ground (`PaintLayout`).
/// A layout draws on its own random stream, so the rest of a field (hills, woods, trees) is the same seed for seed.
public sealed partial class Terrain
{
    /// The layout this field was built to.
    public readonly FieldLayout Layout;
    /// Ridge: the crest's line (Z at X = 0; it bends a little), height and half-width. RidgeSide is the team whose front it covers.
    public float RidgeZ { get; private set; }
    public int RidgeSide { get; private set; } = -1;
    float ridgeH, ridgeW, ridgePhase;
    /// Pass: the floor's centre (X), its half-width before the sides rise, and how high they climb.
    float passX, passFloor, passH;
    /// Village: where it stands and how far it spreads (an ellipse, wider than deep).
    public Vector2 VillageAt { get; private set; }
    const float VillageRx = 30, VillageRz = 13;
    /// Woods a layout asks for (centre, radius), grown by PlaceFoliage like any other wood.
    readonly List<(Vector2 C, float R)> layoutWoods = new();

    /// Pick the layout's features (called before the heights are built).
    void ShapeLayout(Random rng)
    {
        var p = Params;
        switch (Layout)
        {
            case FieldLayout.Ridge:
                // A long crest just in front of one army's line, with a gentle reverse slope behind it.
                RidgeSide = rng.Next(2);
                if (p.Favour >= 0) RidgeSide = p.Favour; // the side that chose its ground stands on the crest
                RidgeZ = (RidgeSide == 0 ? -1 : 1) * (24 + rng.NextSingle() * 6);
                ridgeH = (4 + rng.NextSingle() * 2.5f) * MathF.Max(1, p.HillHeight * 0.8f);
                ridgeW = 14 + rng.NextSingle() * 5;
                ridgePhase = rng.NextSingle() * MathF.Tau;
                break;
            case FieldLayout.TwoHills:
                // A knoll on each wing, between the lines: whoever holds both rules the field.
                for (int side = -1; side <= 1; side += 2)
                    hills.Add((new Vector2(side * (40 + rng.NextSingle() * 16) * p.Scale, rng.NextSingle() * 16 - 8 + (p.Favour < 0 ? 0 : p.Favour == 0 ? -10 : 10)), // nearer the side that chose
                        20 + rng.NextSingle() * 6, (5 + rng.NextSingle() * 2) * MathF.Max(1, p.HillHeight * 0.8f)));
                break;
            case FieldLayout.WoodedFlank:
                // One big wood on a wing, reaching forward past the lines: it hides a way round.
                int ws = rng.Next(2) * 2 - 1;
                layoutWoods.Add((new Vector2(ws * (50 * p.Scale + 30 + rng.NextSingle() * 8), rng.NextSingle() * 24 - 12), 28 + rng.NextSingle() * 7));
                break;
            case FieldLayout.ForestClearing:
                // The fight is in a clearing: a ring of woods all round, with rides cut through it.
                int skip = rng.Next(12);
                for (int k = 0; k < 12; k++)
                {
                    if (k == skip || k == (skip + 6) % 12) continue; // two rides, on opposite sides
                    float a = k * MathF.Tau / 12 + rng.NextSingle() * 0.15f, d = 80 * p.Scale + 14 + rng.NextSingle() * 10;
                    layoutWoods.Add((new Vector2(MathF.Cos(a) * d, MathF.Sin(a) * d * 0.9f), 20 + rng.NextSingle() * 6));
                }
                break;
            case FieldLayout.Pass:
                // High ground both sides; the armies meet on the narrow floor between.
                passX = rng.NextSingle() * 16 - 8;
                passFloor = (38 + rng.NextSingle() * 10) * p.Scale;
                passH = (10 + rng.NextSingle() * 5) * MathF.Max(1, p.HillHeight * 0.7f);
                break;
            case FieldLayout.Village:
                VillageAt = new Vector2((rng.NextSingle() * 50 - 25) * p.Scale, rng.NextSingle() * 6 - 3 + (p.Favour < 0 ? 0 : p.Favour == 0 ? -8 : 8)); // on the chooser's side
                break;
            case FieldLayout.Broken:
                // Tall, steep knolls scattered over the field.
                for (int i = 0; i < 4; i++)
                {
                    float a = rng.NextSingle() * MathF.Tau, d = (15 + rng.NextSingle() * 60) * p.Scale;
                    hills.Add((new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, 9 + rng.NextSingle() * 6, 4 + rng.NextSingle() * 3));
                }
                break;
        }
    }

    /// Height a layout adds at (x, z).
    float LayoutHeight(float x, float z)
    {
        if (Layout == FieldLayout.Pass)
            return passH * SmoothStep(passFloor, passFloor * 1.8f, MathF.Abs(x - passX - MathF.Sin(z * 0.02f) * 6)) * (0.8f + 0.2f * Noise(x * 0.03f, z * 0.03f, Seed + 42));
        if (Layout == FieldLayout.Village) // the village sits on a gentle rise, levelled for its houses
        {
            float v = MathF.Sqrt(Sq((x - VillageAt.X) / VillageRx) + Sq((z - VillageAt.Y) / VillageRz));
            return 0.8f * (1 - SmoothStep(0.6f, 1.6f, v));
        }
        if (Layout != FieldLayout.Ridge) return 0;
        float crest = RidgeZ + MathF.Sin(x * 0.015f + ridgePhase) * 5;
        float dz = (z - crest) / ridgeW;
        // The face toward the enemy is steeper than the reverse slope behind the crest.
        bool behind = RidgeSide == 0 ? dz < 0 : dz > 0;
        float profile = MathF.Exp(-dz * dz * (behind ? 0.45f : 1));
        float along = 1 - SmoothStep(110 * Params.Scale, 190 * Params.Scale, MathF.Abs(x));
        return ridgeH * profile * along * (0.85f + 0.15f * Noise(x * 0.02f, 0, Seed + 41));
    }

    /// The ground at p: from the field's shape and layout (`ShapeGround`), then the season and the rain (T7): winter fields lie
    /// bare, and after rain the low, flat ground turns to mud.
    GroundKind GroundOf(Vector2 p, GroundKind k)
    {
        var g = ShapeGround(p, k);
        if (g == GroundKind.Crops && Params.Season == 3) g = Params.Wet > 0.3f ? GroundKind.Mud : GroundKind.Grass;
        if (Params.Wet > 0 && g is GroundKind.Grass or GroundKind.Crops or GroundKind.Scrub)
        {
            float soak = Noise(p.X * 0.035f, p.Y * 0.035f, Seed + 57) * 0.6f + Params.Wet - Gradient(p).Length() * 3 - MathF.Max(0, Height(p)) * 0.08f;
            if (Rivers.Any(r => r.Dist(p) < r.Width / 2 + 8)) soak += 0.35f; // the banks go first
            if (soak > 0.85f) g = GroundKind.Mud;
        }
        return g;
    }

    /// Ground every field gets from its shape: thick wood in the heart of a big wood, scrub at wood edges, stones on the
    /// steepest slopes; then the layout's own ground. Water and the road are never painted over.
    GroundKind ShapeGround(Vector2 p, GroundKind k)
    {
        if (k is GroundKind.Water or GroundKind.Ford or GroundKind.Road or GroundKind.Bridge) return k;
        foreach (var (c, r) in woods)
        {
            float d2 = Vector2.DistanceSquared(p, c);
            if (r > 18 && d2 < r * r * 0.3f) return GroundKind.DenseWood;
            if (k != GroundKind.Wood && d2 < (r + 3) * (r + 3) && Noise(p.X * 0.2f, p.Y * 0.2f, Seed + 51) > 0) return GroundKind.Scrub;
        }
        if (k == GroundKind.Wood) return k;
        float steep = Gradient(p).Length();
        switch (Layout)
        {
            case FieldLayout.Broken:
                if (steep > 0.22f) return GroundKind.Stony;
                if (Noise(p.X * 0.05f, p.Y * 0.05f, Seed + 52) > 0.25f) return GroundKind.Scrub;
                break;
            case FieldLayout.Village:
                float v = MathF.Sqrt(Sq((p.X - VillageAt.X) / VillageRx) + Sq((p.Y - VillageAt.Y) / VillageRz));
                if (v < 0.95f) return GroundKind.Street;
                if (v < 2.2f && Noise(p.X * 0.04f, p.Y * 0.04f, Seed + 55) > -0.2f) return GroundKind.Crops; // the village's fields
                break;
            case FieldLayout.Hedgerows:
                if (Noise(p.X * 0.03f, p.Y * 0.03f, Seed + 56) > 0.1f) return GroundKind.Crops;
                break;
            case FieldLayout.Fen:
                float wet = Noise(p.X * 0.025f, p.Y * 0.025f, Seed + 53) + 0.25f * Noise(p.X * 0.09f, p.Y * 0.09f, Seed + 54);
                if (wet > 0 || Rivers.Any(r => r.Dist(p) < r.Width / 2 + 5)) return GroundKind.Mud;
                break;
        }
        return steep > 0.45f ? GroundKind.Stony : k;
    }

    static float Sq(float v) => v * v;

    /// The village: houses on its rise (solid, like boulders), garden fences, and a fence round its edge with gaps.
    void PlaceVillage(Random rng)
    {
        for (int i = 0, placed = 0; i < 80 && placed < 10; i++)
        {
            var p = VillageAt + new Vector2((rng.NextSingle() * 2 - 1) * VillageRx * 0.85f, (rng.NextSingle() * 2 - 1) * VillageRz * 0.8f);
            if (Sq((p.X - VillageAt.X) / VillageRx) + Sq((p.Y - VillageAt.Y) / VillageRz) > 0.7f || RoadDist(p) < 5 || WaterDepth(p) > 0 || Blocked(p, 5.5f)) continue; // lanes between the houses
            Add(new Foliage(FoliageKind.House, p, 0.9f + rng.NextSingle() * 0.3f, 2.6f, rng.Next()));
            placed++;
            // A garden fence behind some houses (away from the street).
            if (rng.Next(2) == 0)
            {
                float side = p.Y > VillageAt.Y ? 1 : -1;
                var g = p + new Vector2(0, side * 5);
                if (RoadDist(g) > 4) AddFeature(FeatureKind.Fence, g - new Vector2(4, 0), g + new Vector2(4, 0));
            }
        }
        // The fence round the village, broken by gaps for lanes.
        const int segs = 20;
        for (int i = 0; i < segs; i++)
        {
            if (rng.Next(3) == 0) continue;
            float a0 = i * MathF.Tau / segs, a1 = (i + 0.7f) * MathF.Tau / segs;
            Vector2 E(float a) => VillageAt + new Vector2(MathF.Cos(a) * VillageRx * 1.05f, MathF.Sin(a) * VillageRz * 1.1f);
            if (RoadDist(E(a0)) > 4 && RoadDist(E(a1)) > 4 && WaterDepth(E(a0)) == 0 && WaterDepth(E(a1)) == 0) AddFeature(FeatureKind.Fence, E(a0), E(a1));
        }
    }

    /// Hedgerows: a hedge across the field in front of each army, and hedges running between them that cut it into fields.
    /// Every hedge has gaps a company can pass through.
    void PlaceHedges(Random rng)
    {
        float s = Params.Scale, w = 95 * s;
        for (int side = -1; side <= 1; side += 2)
        {
            float z = side * (12 + rng.NextSingle() * 5);
            HedgeLine(new Vector2(-w, z), new Vector2(w, z), rng);
        }
        for (int i = 0; i < 4; i++)
        {
            float x = (i < 2 ? -1 : 1) * (28 + 34 * (i % 2) + rng.NextSingle() * 10) * s;
            HedgeLine(new Vector2(x, -10), new Vector2(x + rng.NextSingle() * 8 - 4, 10), rng); // between the lines, not where they deploy
        }
    }

    /// A hedge in runs of about 18 m with 6 m gaps, kept off the road and out of the water.
    void HedgeLine(Vector2 a, Vector2 b, Random rng)
    {
        int before = Features.Count;
        FeatureLine(FeatureKind.Hedge, a, b, 18, 6, rng);
        for (int i = Features.Count - 1; i >= before; i--)
        {
            var f = Features[i];
            var mid = (f.A + f.B) / 2;
            if (RoadDist(mid) < 6 || WaterDepth(mid) > 0 || RoadDist(f.A) < 4 || RoadDist(f.B) < 4) RemoveFeatureAt(i);
        }
    }
}

