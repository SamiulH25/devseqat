using Microsoft.Xna.Framework;

namespace Ironfield;

/// World: line features (hedges, fences, a bridge's rails) and bridge decks (TerrainFeatures.cs).
sealed partial class World
{
    static readonly Color HedgeLeaf = new(58, 92, 40), FenceWood = new(118, 88, 58), DeckWood = new(112, 82, 52);

    void Features(Mesh m)
    {
        m.Tag = Vector2.Zero;
        var rng = new Random(t.Seed + 77);
        foreach (var f in t.Features)
        {
            var d = f.B - f.A;
            float len = d.Length();
            if (len < 0.1f) continue;
            d /= len;
            float yaw = MathF.Atan2(d.Y, d.X);
            Vector3 At(float s, float lift = 0) { var p = f.A + d * s; return new Vector3(p.X, t.Height(p) + lift, p.Y); }
            if (f.Kind == FeatureKind.Hedge)
            {
                // Overlapping leafy blocks of uneven height, sunk a little into the ground.
                for (float s = 0.6f; s < len; s += 1.1f)
                    m.Box(At(s, -0.3f), 1.5f, 1.0f + rng.NextSingle() * 0.25f, f.Height + 0.3f + rng.NextSingle() * 0.35f, yaw + (rng.NextSingle() - 0.5f) * 0.2f, Jitter(HedgeLeaf, rng, 0.1f));
            }
            else
            {
                // Posts every 2.4 m and two rails between them.
                int posts = Math.Max(1, (int)(len / 2.4f));
                for (int i = 0; i <= posts; i++) m.Box(At(i * len / posts, -0.2f), 0.14f, 0.14f, f.Height + 0.2f, yaw, Jitter(FenceWood, rng, 0.08f));
                for (int i = 0; i < posts; i++)
                {
                    float s0 = i * len / posts, s1 = (i + 1) * len / posts, mid = (s0 + s1) / 2;
                    foreach (float h in new[] { 0.45f, f.Height - 0.15f })
                        m.Box(At(mid, h), s1 - s0, 0.07f, 0.09f, yaw, Jitter(FenceWood, rng, 0.06f));
                }
            }
        }
        // Bridge decks: planks bank to bank, with beams hanging down to the water to hide the causeway under them.
        foreach (var r in t.Rivers)
            foreach (var (at, along) in r.Bridges)
            {
                float yaw = MathF.Atan2(along.Y, along.X), reach = r.Width / 2 + 3;
                for (float s = -reach; s < reach; s += 0.9f)
                {
                    var p = at + along * (s + 0.45f);
                    float h = t.Height(p);
                    m.Box(new Vector3(p.X, h - 1.6f, p.Y), 0.88f, River.BridgeHalf * 2 + 0.3f, 1.72f, yaw, Jitter(DeckWood, rng, 0.07f));
                }
            }
    }
}
