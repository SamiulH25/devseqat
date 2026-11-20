using Microsoft.Xna.Framework;

namespace Ironfield;

/// The campaign map's small cover (docs/WORLD_DRESS.md; placed by `WorldMap.Dress`): cheap pieces, a few dozen triangles
/// each, since thousands of them stand between the woods.
sealed partial class World
{
    static readonly Color Straw = new(204, 182, 112), DeadWood = new(112, 100, 86), PalmBark = new(134, 110, 78);
    static readonly Color[] Blooms = { new(236, 214, 80), new(240, 240, 232), new(176, 120, 200), new(206, 70, 54), new(96, 128, 206) };

    /// A plant's green drawn toward straw by how parched it is, and a little more in autumn and winter.
    Color Parched(Color green, float dry) => Color.Lerp(green, Straw, Math.Min(1, dry + (t.MapSeason >= 2 ? 0.25f : 0)) * 0.85f);

    /// A low shrub: one or two leafy lumps (thorn scrub where the land is dry).
    void Shrub(Mesh m, Vector3 b, float s, float dry, Random r)
    {
        var leaf = Parched(Color.Lerp(new Color(60, 100, 44), new Color(96, 126, 56), r.NextSingle()), dry);
        m.Tag = new Vector2(0.2f, 1);
        m.Lump(b + Vector3.Up * 0.3f * s, new Vector3(0.9f, 0.62f, 0.9f) * s, leaf, r, 0.25f);
        if (r.Next(2) == 0)
            m.Lump(b + new Vector3((r.NextSingle() - 0.5f) * 1.5f, 0.2f, (r.NextSingle() - 0.5f) * 1.5f) * s, new Vector3(0.6f, 0.45f, 0.6f) * s, Jitter(leaf, r, 0.1f), r, 0.25f);
    }

    /// A tussock of long grass: a low mound (its body from far off) with blades standing out of it.
    void Tussock(Mesh m, Vector3 b, float s, float dry, Random r)
    {
        Color low = Parched(new Color(92, 122, 54), dry), tip = Parched(new Color(168, 184, 96), dry);
        m.Tag = new Vector2(0.15f, 1);
        m.Lump(b + Vector3.Up * 0.05f * s, new Vector3(0.75f, 0.32f, 0.75f) * s, Color.Lerp(low, tip, 0.4f), r, 0.2f);
        Blades(m, b, s, 5 + r.Next(4), 0.6f, 0.14f, 0.7f, low, tip, r);
    }

    /// Reeds on wet ground: a clump of tall blades, a brown cattail head on some.
    void ReedClump(Mesh m, Vector3 b, float s, Random r)
    {
        Blades(m, b, s, 8 + r.Next(5), 1.0f, 0.11f, 1.3f, ReedLow, ReedTip, r);
        for (int i = r.Next(3); i > 0; i--)
        {
            var top = b + new Vector3((r.NextSingle() - 0.5f) * 1.2f, 1.5f + r.NextSingle() * 0.5f, (r.NextSingle() - 0.5f) * 1.2f) * s;
            m.TriUp(top - Vector3.UnitX * 0.14f * s, top + Vector3.UnitX * 0.14f * s, top + Vector3.Up * 0.5f * s, Cattail, Cattail, Cattail, 0.6f);
        }
    }

    /// Grass blades fanning out of `b`: rooted within `spread`, `width` wide at the foot, about `height` tall (all times `s`).
    static void Blades(Mesh m, Vector3 b, float s, int count, float spread, float width, float height, Color low, Color tip, Random r)
    {
        for (int k = 0; k < count; k++)
        {
            float ang = r.NextSingle() * MathF.Tau, turn = r.NextSingle() * MathF.Tau;
            var o = new Vector3(MathF.Cos(ang), 0, MathF.Sin(ang)) * r.NextSingle() * spread * s;
            var side = new Vector3(MathF.Cos(turn), 0, MathF.Sin(turn)) * width * s;
            var c = Jitter(low, r, 0.1f);
            m.TriUp(b + o - side, b + o + side, b + o * 1.9f + Vector3.Up * height * (0.7f + r.NextSingle() * 0.6f) * s, c, c, Jitter(tip, r, 0.1f), 0.6f);
        }
    }

    /// Heather: low mats, purple in bloom through summer and autumn.
    void Heather(Mesh m, Vector3 b, float s, Random r)
    {
        var mat = t.MapSeason is 1 or 2 ? Color.Lerp(new Color(132, 84, 128), new Color(162, 106, 144), r.NextSingle())
            : Color.Lerp(new Color(92, 88, 58), new Color(112, 98, 72), r.NextSingle());
        m.Tag = new Vector2(0.1f, 1);
        for (int i = 2 + r.Next(2); i > 0; i--)
            m.Lump(b + new Vector3((r.NextSingle() - 0.5f) * 2.2f, 0.05f, (r.NextSingle() - 0.5f) * 2.2f) * s, new Vector3(1, 0.3f, 1) * s * (0.6f + r.NextSingle() * 0.4f), Jitter(mat, r, 0.1f), r, 0.2f);
    }

    /// A dead tree: a bare grey trunk and three boughs.
    static void DeadTree(Mesh m, Vector3 b, float s, Random r)
    {
        var wood = Jitter(DeadWood, r, 0.1f);
        var top = b + new Vector3((r.NextSingle() - 0.5f) * 0.6f, 3.4f + r.NextSingle(), (r.NextSingle() - 0.5f) * 0.6f) * s;
        m.Tag = Vector2.Zero;
        m.Frustum(b - Vector3.Up * 0.4f, top, 0.24f * s, 0.07f * s, 5, wood, r);
        for (int i = 0; i < 3; i++)
        {
            float a = r.NextSingle() * MathF.Tau;
            var from = Vector3.Lerp(b, top, 0.45f + 0.15f * i);
            m.Frustum(from, from + new Vector3(MathF.Cos(a), 0.9f, MathF.Sin(a)) * (1.3f - 0.3f * i) * s, 0.09f * s, 0.02f * s, 4, wood, r);
        }
    }

    /// A palm: a leaning trunk and a crown of drooping fronds.
    static void Palm(Mesh m, Vector3 b, float s, Random r)
    {
        var bark = Jitter(PalmBark, r, 0.08f);
        float a = r.NextSingle() * MathF.Tau, h = (3.6f + r.NextSingle() * 1.4f) * s;
        var lean = new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 0.6f * s;
        Vector3 mid = b + Vector3.Up * h * 0.55f + lean * 0.35f, top = b + Vector3.Up * h + lean;
        m.Tag = Vector2.Zero;
        m.Frustum(b - Vector3.Up * 0.4f, mid, 0.2f * s, 0.15f * s, 5, bark, r);
        m.Frustum(mid, top, 0.15f * s, 0.11f * s, 5, bark, r);
        var uv = new Vector2(0.4f, 1);
        var green = Color.Lerp(new Color(96, 136, 52), new Color(132, 160, 64), r.NextSingle());
        for (int i = 0; i < 7; i++)
        {
            float fa = a + i * MathF.Tau / 7 + (r.NextSingle() - 0.5f) * 0.4f, len = (1.9f + r.NextSingle() * 0.6f) * s;
            var dir = new Vector3(MathF.Cos(fa), 0, MathF.Sin(fa));
            var side = new Vector3(-dir.Z, 0, dir.X) * 0.36f * s;
            Vector3 elbow = top + dir * len * 0.55f + Vector3.Up * 0.35f * s, tip = top + dir * len - Vector3.Up * 0.55f * s;
            var c = Jitter(green, r, 0.1f);
            var nrm = Vector3.Normalize(Vector3.Up + dir * 0.5f);
            foreach (var q in new[] { top, elbow - side, elbow + side, elbow - side, elbow + side, tip }) m.Raw(q, nrm, c, uv);
        }
    }

    /// A drift of wild flowers: small blooms lying on the grass, one colour to a drift (neighbouring drifts mostly share it).
    void Flowers(Mesh m, Vector2 at, float s, Random r)
    {
        int patch = ((int)MathF.Floor(at.X / 60) * 73856093 ^ (int)MathF.Floor(at.Y / 60) * 19349663) & int.MaxValue;
        var col = Blooms[(r.Next(4) == 0 ? r.Next() : patch) % Blooms.Length];
        for (int i = 9 + r.Next(8); i > 0; i--)
        {
            float ang = r.NextSingle() * MathF.Tau, d = MathF.Sqrt(r.NextSingle()) * 2.4f * s, f = (0.2f + r.NextSingle() * 0.12f) * s;
            var q = at + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * d;
            var c = new Vector3(q.X, t.Height(q) + 0.3f, q.Y);
            var bloom = Jitter(col, r, 0.08f);
            m.TriUp(c - Vector3.UnitX * f, c + Vector3.UnitX * f, c + Vector3.UnitZ * f, bloom, bloom, bloom, 0.2f);
            m.TriUp(c - Vector3.UnitX * f, c + Vector3.UnitX * f, c - Vector3.UnitZ * f, bloom, bloom, bloom, 0.2f);
        }
    }

    // ---------- the water's edge and the roadside (WD4) ----------

    static readonly Color Bleached = new(126, 112, 94), Hull = new(104, 78, 54), HullPaint = new(66, 94, 110), MileGrey = new(222, 218, 204), Thwart = new(150, 120, 84);

    /// Driftwood: one or two short thick logs, bleached grey, lying half sunk in the sand along its slope.
    void Driftwood(Mesh m, Vector3 b, float s, Random r)
    {
        m.Tag = Vector2.Zero;
        for (int i = 1 + r.Next(2), k = 0; k < i; k++)
        {
            float a = r.NextSingle() * MathF.Tau, len = (1.1f + r.NextSingle() * 0.6f) * s;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            var at = b + new Vector3(-dir.Z, 0, dir.X) * k * 0.7f * s;
            Vector3 End(float u) { var q = new Vector2(at.X + dir.X * u, at.Z + dir.Z * u); return new Vector3(q.X, t.Height(q) + 0.12f * s, q.Y); }
            var wood = Jitter(Bleached, r, 0.12f);
            m.Frustum(End(-len / 2), End(len / 2), 0.24f * s, 0.1f * s, 5, wood, r, true); // tapered to the top, root end thick
            var fork = End(-len * 0.15f);                                                  // a snapped branch standing off it
            var off = new Vector3(-dir.Z, 0, dir.X) * (r.Next(2) * 2 - 1);
            m.Frustum(fork, fork + (off * 0.5f + dir * 0.3f + Vector3.Up * 0.25f) * s, 0.08f * s, 0.04f * s, 4, new Color(wood.ToVector3() * 0.85f), r, true);
        }
    }

    /// Marram on the bank behind a beach: a low fan of stiff pale blades, bleached by salt and wind.
    static void Marram(Mesh m, Vector3 b, float s, float dry, Random r)
    {
        Color low = Color.Lerp(new Color(132, 146, 84), Straw, dry * 0.6f), tip = Color.Lerp(new Color(206, 204, 150), Straw, dry * 0.5f);
        m.Tag = new Vector2(0.15f, 1);
        Blades(m, b, s, 12 + r.Next(6), 0.55f, 0.09f, 0.9f, low, tip, r);
    }

    /// A milestone at the roadside: a squat whitewashed block with a flat top and a dark foot.
    static void Milestone(Mesh m, Vector3 b, float s, Random r)
    {
        float yaw = r.NextSingle() * MathF.Tau;
        m.Tag = Vector2.Zero;
        m.Box(b - Vector3.Up * 0.25f, 1.1f * s, 0.8f * s, 0.4f * s, yaw, new Color(92, 88, 80));          // the foot
        m.Box(b + Vector3.Up * 0.2f * s, 0.8f * s, 0.55f * s, 0.75f * s, yaw, Jitter(MileGrey, r, 0.04f)); // the stone
    }

    /// A wayside shrine: a stone pier with a little roof over a niche, a cross on its ridge.
    static void Wayside(Mesh m, Vector3 b, float s, Random r)
    {
        float yaw = r.NextSingle() * MathF.Tau;
        var stone = Jitter(Stone, r, 0.06f);
        m.Tag = Vector2.Zero;
        m.Box(b - Vector3.Up * 0.3f, 1.3f * s, 1.3f * s, 0.5f * s, yaw, stone);                    // the step
        m.Box(b + Vector3.Up * 0.2f * s, 0.8f * s, 0.8f * s, 1.5f * s, yaw, stone);                // the pier
        m.Gable(b + Vector3.Up * 1.7f * s, 1.15f * s, 1.15f * s, 0.6f * s, yaw, Jitter(Tile, r, 0.08f));
        var top = b + Vector3.Up * 2.3f * s;
        m.Box(top, 0.08f * s, 0.08f * s, 0.55f * s, yaw, DeadWood);                                // the cross
        m.Box(top + Vector3.Up * 0.32f * s, 0.34f * s, 0.08f * s, 0.08f * s, yaw, DeadWood);
    }

    /// A well: a round stone kerb, two posts and a little roof over the winch.
    static void Well(Mesh m, Vector3 b, float s, Random r)
    {
        float yaw = r.NextSingle() * MathF.Tau;
        var f = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        m.Tag = Vector2.Zero;
        m.Frustum(b - Vector3.Up * 0.3f, b + Vector3.Up * 0.75f * s, 0.85f * s, 0.8f * s, 8, Jitter(Stone, r, 0.06f), r);
        m.Frustum(b + Vector3.Up * 0.74f * s, b + Vector3.Up * 0.76f * s, 0.62f * s, 0.62f * s, 8, new Color(40, 46, 52), r, true); // the dark water inside
        foreach (int side in new[] { -1, 1 }) m.Box(b + f * 0.7f * side * s + Vector3.Up * 0.5f * s, 0.12f * s, 0.12f * s, 1.5f * s, yaw, DeadWood);
        m.Box(b + Vector3.Up * 1.55f * s, 1.5f * s, 0.1f * s, 0.1f * s, yaw, DeadWood);              // the winch
        m.Gable(b + Vector3.Up * 1.95f * s, 1.8f * s, 1.3f * s, 0.55f * s, yaw, Jitter(Thatch, r, 0.08f));
    }

    /// A boat drawn up on the beach, bow to the water (down the slope), open to the sky (keel up it read as a plank from above).
    void Rowboat(Mesh m, Vector2 at, float s, Random r)
    {
        var down = new Vector2(t.Height(at - Vector2.UnitX) - t.Height(at + Vector2.UnitX), t.Height(at - Vector2.UnitY) - t.Height(at + Vector2.UnitY));
        float yaw = down.LengthSquared() > 1e-6f ? MathF.Atan2(down.Y, down.X) + (r.NextSingle() - 0.5f) * 0.6f : r.NextSingle() * MathF.Tau;
        var f = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
        var b = new Vector3(at.X, t.Height(at) - 0.05f, at.Y);
        var hull = Jitter(r.Next(3) == 0 ? HullPaint : Hull, r, 0.08f);
        m.Tag = Vector2.Zero;
        var across = new Vector3(-f.Z, 0, f.X);
        foreach (int side in new[] { -1, 1 }) m.Box(b + across * 0.48f * side * s, 2.6f * s, 0.14f * s, 0.5f * s, yaw, hull); // the sides
        m.Box(b, 2.6f * s, 0.9f * s, 0.12f * s, yaw, new Color(hull.ToVector3() * 0.7f));                                         // the bottom boards
        m.Box(b + f * 1.45f * s, 0.5f * s, 0.62f * s, 0.6f * s, yaw, hull);                                                       // the prow
        m.Box(b + f * 1.75f * s, 0.3f * s, 0.3f * s, 0.68f * s, yaw, hull);                                                       // the stem
        foreach (int side in new[] { -1, 1 }) m.Box(b + across * 0.5f * side * s + Vector3.Up * 0.48f * s, 2.7f * s, 0.2f * s, 0.07f * s, yaw, Jitter(Thwart, r, 0.05f)); // the gunwales
        m.Box(b - f * 1.3f * s, 0.12f * s, 1.0f * s, 0.5f * s, yaw, hull);                                                        // the transom
        m.Box(b + Vector3.Up * 0.3f * s, 0.22f * s, 0.9f * s, 0.06f * s, yaw, Jitter(Thwart, r, 0.06f));                           // a thwart
    }
}
