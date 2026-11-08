using Microsoft.Xna.Framework;

namespace Ironfield;

/// The shape a battlefield is built to (docs/TERRAIN.md T2): what lies between and around the two armies.
/// The armies meet along Z = 0; team 0 deploys at negative Z, team 1 at positive Z.
public enum FieldLayout { Open, Ridge, TwoHills, Broken, Fen }

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

    /// Pick the layout's features (called before the heights are built).
    void ShapeLayout(Random rng)
    {
        var p = Params;
        switch (Layout)
        {
            case FieldLayout.Ridge:
                // A long crest just in front of one army's line, with a gentle reverse slope behind it.
                RidgeSide = rng.Next(2);
                RidgeZ = (RidgeSide == 0 ? -1 : 1) * (24 + rng.NextSingle() * 6);
                ridgeH = (4 + rng.NextSingle() * 2.5f) * MathF.Max(1, p.HillHeight * 0.8f);
                ridgeW = 14 + rng.NextSingle() * 5;
                ridgePhase = rng.NextSingle() * MathF.Tau;
                break;
            case FieldLayout.TwoHills:
                // A knoll on each wing, between the lines: whoever holds both rules the field.
                for (int side = -1; side <= 1; side += 2)
                    hills.Add((new Vector2(side * (40 + rng.NextSingle() * 16) * p.Scale, rng.NextSingle() * 16 - 8),
                        20 + rng.NextSingle() * 6, (5 + rng.NextSingle() * 2) * MathF.Max(1, p.HillHeight * 0.8f)));
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
        if (Layout != FieldLayout.Ridge) return 0;
        float crest = RidgeZ + MathF.Sin(x * 0.015f + ridgePhase) * 5;
        float dz = (z - crest) / ridgeW;
        // The face toward the enemy is steeper than the reverse slope behind the crest.
        bool behind = RidgeSide == 0 ? dz < 0 : dz > 0;
        float profile = MathF.Exp(-dz * dz * (behind ? 0.45f : 1));
        float along = 1 - SmoothStep(110 * Params.Scale, 190 * Params.Scale, MathF.Abs(x));
        return ridgeH * profile * along * (0.85f + 0.15f * Noise(x * 0.02f, 0, Seed + 41));
    }

    /// Ground every field gets from its shape: thick wood in the heart of a big wood, scrub at wood edges, stones on the
    /// steepest slopes; then the layout's own ground. Water and the road are never painted over.
    GroundKind GroundOf(Vector2 p, GroundKind k)
    {
        if (k is GroundKind.Water or GroundKind.Ford or GroundKind.Road) return k;
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
            case FieldLayout.Fen:
                float wet = Noise(p.X * 0.025f, p.Y * 0.025f, Seed + 53) + 0.25f * Noise(p.X * 0.09f, p.Y * 0.09f, Seed + 54);
                if (wet > 0 || Rivers.Any(r => r.Dist(p) < r.Width / 2 + 5)) return GroundKind.Mud;
                break;
        }
        return steep > 0.45f ? GroundKind.Stony : k;
    }
}
