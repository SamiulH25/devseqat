using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
            if (f.Kind == FeatureKind.Stakes) continue; // planted mid-battle: BattleScreen.Stakes draws them
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

sealed partial class World
{
    /// A flat colour for each kind of ground, for the ground map and its legend.
    public static Color KindColor(GroundKind k) => k switch
    {
        GroundKind.Road => new Color(170, 140, 96), GroundKind.Crops => new Color(196, 176, 92), GroundKind.Scrub => new Color(110, 128, 60),
        GroundKind.Stony => new Color(150, 146, 136), GroundKind.Mud => new Color(98, 78, 52), GroundKind.Wood => new Color(58, 104, 50),
        GroundKind.DenseWood => new Color(30, 66, 32), GroundKind.Ford => new Color(110, 160, 180), GroundKind.Water => new Color(48, 86, 128),
        GroundKind.Bridge => new Color(128, 92, 58), GroundKind.Street => new Color(140, 124, 108), GroundKind.Sand => new Color(214, 196, 140),
        _ => new Color(126, 158, 84),
    };

    /// Is there a 2 m height contour within a texel of w?
    bool Contour(Vector2 w, float texel)
    {
        float h = t.Height(w);
        int band = (int)MathF.Floor(h / 2);
        return (int)MathF.Floor(t.Height(w + new Vector2(texel, 0)) / 2) != band || (int)MathF.Floor(t.Height(w + new Vector2(0, texel)) / 2) != band;
    }

    Texture2D BuildGroundMap()
    {
        int S = 256;
        float texel = 2 * t.MinimapRange / S;
        var data = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var w = MinimapToWorld(new Vector2((x + 0.5f) / S, (y + 0.5f) / S), t.MinimapRange);
                var c = KindColor(t.GroundAt(w));
                if (Contour(w, texel)) c = Color.Lerp(c, Color.Black, 0.35f);
                data[y * S + x] = c;
            }
        MapFeatures(data, S, new Color(20, 40, 16), new Color(60, 40, 24));
        var tex = new Texture2D(gd, S, S);
        tex.SetData(data);
        return tex;
    }

    /// Hedges, fences and houses onto a map texture.
    void MapFeatures(Color[] data, int S, Color hedge, Color fence)
    {
        void Dot(Vector2 w, Color c)
        {
            var m = WorldToMinimap(w, t.MinimapRange) * S;
            int px = (int)m.X, py = (int)m.Y;
            if (px >= 0 && py >= 0 && px < S && py < S) data[py * S + px] = c;
        }
        float texel = 2 * t.MinimapRange / S;
        foreach (var f in t.Features)
        {
            float len = Vector2.Distance(f.A, f.B);
            for (float s = 0; s <= len; s += texel * 0.5f) Dot(Vector2.Lerp(f.A, f.B, len < 0.01f ? 0 : s / len), f.Kind == FeatureKind.Hedge ? hedge : fence);
        }
        foreach (var f in t.Foliage)
            if (f.Kind == FoliageKind.House)
                for (float dz = -f.Radius; dz <= f.Radius; dz += texel)
                    for (float dx = -f.Radius; dx <= f.Radius; dx += texel) Dot(f.Pos + new Vector2(dx, dz), new Color(80, 60, 50));
    }
}
