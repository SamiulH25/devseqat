using Microsoft.Xna.Framework;

namespace Ironfield;

/// World: the land with meaning (docs/WORLD_LIFE.md WL3). Field plots round the settlements drawn as their own patches
/// laid over the ground (crops by season, furrows drawn in the shader from the UV: x = 100 + metres across the furrows,
/// y = how strongly they show), hedges on most sides, post-and-rail fences round pastures, haystacks in summer and autumn;
/// and the birch, a third tree shape.
sealed partial class World
{
    static readonly Color Hay = new(184, 158, 92), Birchbark = new(214, 210, 196);

    /// A field's colour this season and how strongly its rows show.
    static (Vector3 Col, float Rows) FieldLook(FieldKind kind, int season) => (kind, season) switch
    {
        (FieldKind.Grain, 0) => (new(0.46f, 0.58f, 0.27f), 0.5f),     // young corn
        (FieldKind.Grain, 1) => (new(0.8f, 0.68f, 0.32f), 0.45f),      // ripe wheat
        (FieldKind.Grain, 2) => (new(0.72f, 0.62f, 0.4f), 0.35f),      // stubble
        (FieldKind.Grain, _) => (new(0.5f, 0.42f, 0.3f), 0.5f),
        (FieldKind.Green, 2) => (new(0.62f, 0.55f, 0.32f), 0.4f),
        (FieldKind.Green, 3) => (new(0.44f, 0.4f, 0.3f), 0.4f),
        (FieldKind.Green, _) => (new(0.4f, 0.57f, 0.25f), 0.45f),      // beans, barley, a hay meadow
        (FieldKind.Ploughed, 0) => (new(0.42f, 0.36f, 0.24f), 0.95f),
        (FieldKind.Ploughed, _) => (new(0.43f, 0.33f, 0.23f), 0.95f),
        (FieldKind.Fallow, _) => (new(0.53f, 0.54f, 0.32f), 0.15f),
        (FieldKind.Pasture, 2) => (new(0.5f, 0.54f, 0.29f), 0),
        (FieldKind.Pasture, _) => (new(0.38f, 0.56f, 0.25f), 0),
        _ => (new(0.4f, 0.53f, 0.26f), 0),                             // orchard grass
    };

    /// Every field plot, tiled by 48 m like the foliage so only those in view are drawn.
    void Fields(Mesh m)
    {
        foreach (var g in t.Fields.GroupBy(f => ((int)MathF.Floor(f.Center.X / 48), (int)MathF.Floor(f.Center.Y / 48))))
        {
            int start = m.V.Count;
            foreach (var f in g) Plot(m, f);
            AddTile(m, start);
        }
    }

    /// One plot: a patch following the ground in 2.5 m cells, then its hedges or fence and any haystacks.
    void Plot(Mesh m, FieldPlot f)
    {
        var rng = new Random(f.Seed);
        var (col, rows) = FieldLook(f.Kind, t.MapSeason);
        col *= 1 + (rng.NextSingle() - 0.5f) * 0.12f;
        int nu = Math.Max(2, (int)MathF.Ceiling(f.Half.X * 2 / 2.5f)), nv = Math.Max(2, (int)MathF.Ceiling(f.Half.Y * 2 / 2.5f));
        // The crop shows strongest in the middle and fades into the meadow at the plot's edge, so plots read as painted
        // fields rather than patches laid on the grass; the corners are rounded off the same way.
        void V(int i, int j)
        {
            float fu = 2f * i / nu - 1, fv = 2f * j / nv - 1;
            float u = fu * f.Half.X, v = fv * f.Half.Y;
            var p = f.At(u, v);
            float rim = MathF.Max(MathF.Abs(fu), MathF.Abs(fv)), corner = MathF.Abs(fu) * MathF.Abs(fv);
            float wobble = (Terrain.Noise(p.X * 0.2f, p.Y * 0.2f, t.Seed + 43) - 0.5f) * 0.5f;
            float blend = Math.Clamp(0.08f + 0.92f * MathF.Pow(Math.Clamp(rim + wobble * 0.4f, 0, 1), 3) + 0.35f * corner * corner, 0, 1);
            var c = Vector3.Lerp(col, GroundColor(p), blend) * (1 + (Terrain.Noise(p.X * 0.35f, p.Y * 0.35f, t.Seed + 41) - 0.5f) * 0.1f);
            m.Raw(new Vector3(p.X, t.Height(p) + 0.16f, p.Y), NormalAt(p), new Color(c.X, c.Y, c.Z, 1 - blend), new Vector2(100 + v + f.Half.Y, rows * (1 - blend))); // alpha: how much crop (the map shader)
        }
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                V(i, j); V(i + 1, j); V(i + 1, j + 1);
                V(i, j); V(i + 1, j + 1); V(i, j + 1);
            }
        for (int side = 0; side < 4; side++)
        {
            float sx = side switch { 1 => 1, 3 => -1, _ => 0 }, sy = side switch { 0 => -1, 2 => 1, _ => 0 };
            Vector2 a = f.At(sy != 0 ? -f.Half.X : sx * f.Half.X, sy != 0 ? sy * f.Half.Y : -f.Half.Y);
            Vector2 b = f.At(sy != 0 ? f.Half.X : sx * f.Half.X, sy != 0 ? sy * f.Half.Y : f.Half.Y);
            var shift = (f.At(sx * (f.Half.X + 0.5f), sy * (f.Half.Y + 0.5f)) - f.At(sx * f.Half.X, sy * f.Half.Y));
            if (f.Kind == FieldKind.Pasture) Fence(m, a + shift, b + shift, rng);
            else if ((f.Hedged & (1 << side)) != 0) Hedge(m, a + shift, b + shift, rng);
        }
        bool stacks = t.MapSeason == 1 && f.Kind == FieldKind.Green || t.MapSeason == 2 && f.Kind == FieldKind.Grain;
        if (stacks)
            for (int k = 2 + rng.Next(3); k > 0; k--)
            {
                var p = f.At((rng.NextSingle() * 2 - 1) * f.Half.X * 0.75f, (rng.NextSingle() * 2 - 1) * f.Half.Y * 0.7f);
                var b = new Vector3(p.X, t.Height(p) - 0.1f, p.Y);
                m.Tag = Vector2.Zero;
                var hay = Jitter(Hay, rng, 0.08f);
                m.Frustum(b, b + Vector3.Up * 1.0f, 0.85f, 0.6f, 7, hay, rng);
                m.Frustum(b + Vector3.Up * 1.0f, b + Vector3.Up * 1.7f, 0.6f, 0, 7, hay, rng);
            }
    }

    /// A hedge from a to b: a lumpy ridge of leaves a metre high, with a gap now and then.
    void Hedge(Mesh m, Vector2 a, Vector2 b, Random rng)
    {
        float len = Vector2.Distance(a, b);
        if (len < 1) return;
        var d = (b - a) / len; var n = new Vector2(-d.Y, d.X);
        var leaf = Jitter(new Color(74, 112, 52), rng, 0.08f); // the trees' green, not a dark outline
        if (t.MapSeason == 2) leaf = Color.Lerp(leaf, new Color(128, 108, 56), 0.35f);
        if (t.MapSeason == 3) leaf = Color.Lerp(leaf, new Color(196, 200, 206), 0.55f); // snow lies along it
        int steps = Math.Max(1, (int)(len / 1.8f));
        float hPrev = 0.55f + rng.NextSingle() * 0.3f;
        for (int i = 0; i < steps; i++)
        {
            float h = 0.55f + rng.NextSingle() * 0.3f;
            if (i == 0 || i == steps - 1 || rng.Next(5) == 0) { hPrev = h; continue; } // gaps, and open corners for the gates
            Vector2 p0 = a + d * (len * i / steps), p1 = a + d * (len * (i + 1) / steps);
            float y0 = t.Height(p0), y1 = t.Height(p1);
            Vector3 l0 = V3(p0 - n * 0.3f, y0 - 0.1f), l1 = V3(p1 - n * 0.3f, y1 - 0.1f), r0 = V3(p0 + n * 0.3f, y0 - 0.1f), r1 = V3(p1 + n * 0.3f, y1 - 0.1f);
            Vector3 t0 = V3(p0 + n * (rng.NextSingle() - 0.5f) * 0.2f, y0 + hPrev), t1 = V3(p1, y1 + h);
            var c = Jitter(leaf, rng, 0.07f);
            Face(l0, l1, t1, t0, c); Face(r1, r0, t0, t1, c);
            hPrev = h;
        }
        void Face(Vector3 p, Vector3 q, Vector3 r, Vector3 s, Color c)
        {
            var nn = Vector3.Normalize(Vector3.Cross(q - p, s - p));
            if (nn.Y < 0) nn = -nn;
            var uv = new Vector2(0.06f, 0.9f); // barely sways; soft like leaves
            m.Raw(p, nn, c, new Vector2(0, 0.9f)); m.Raw(q, nn, c, new Vector2(0, 0.9f)); m.Raw(r, nn, c, uv);
            m.Raw(p, nn, c, new Vector2(0, 0.9f)); m.Raw(r, nn, c, uv); m.Raw(s, nn, c, uv);
        }
    }

    /// A post-and-rail fence from a to b round a pasture.
    void Fence(Mesh m, Vector2 a, Vector2 b, Random rng)
    {
        float len = Vector2.Distance(a, b);
        if (len < 1) return;
        var d = (b - a) / len;
        float yaw = MathF.Atan2(d.Y, d.X);
        int posts = Math.Max(1, (int)(len / 3.5f));
        m.Tag = Vector2.Zero;
        for (int i = 0; i <= posts; i++)
        {
            var p = a + d * (len * i / posts);
            m.Box(V3(p, t.Height(p) - 0.2f), 0.16f, 0.16f, 1.15f, yaw, Jitter(FenceWood, rng, 0.08f));
            if (i == posts) break;
            var q = a + d * (len * (i + 0.5f) / posts);
            m.Box(V3(q, t.Height(q) + 0.62f), len / posts, 0.09f, 0.11f, yaw, Jitter(FenceWood, rng, 0.06f));
        }
    }

    /// A battlefield's outskirts (WL6), drawing only and far out past where the companies fight: a farmstead (a house,
    /// a barn, a paddock fence, haystacks) and a cart left by its gate, so the field reads as somebody's land.
    void Outskirts(Mesh m)
    {
        var r = new Random(t.Seed * 17 + 5);
        for (int tries = 0; tries < 40; tries++)
        {
            float a = r.NextSingle() * MathF.Tau, d = 175 + r.NextSingle() * 40;
            var p = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y)) > t.Half - 18) continue;
            if (t.WaterDepth(p) > 0 || t.Rivers.Any(rv => rv.Dist(p) < rv.Width + 14) || t.Foliage.Any(f => Vector2.DistanceSquared(f.Pos, p) < 16 * 16)) continue;
            float yaw = a + MathF.PI / 2 + (r.NextSingle() - 0.5f) * 0.4f;
            var ax = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw)); var az = new Vector2(-ax.Y, ax.X);
            Vector3 On(Vector2 q, float up = 0) => V3(q, t.Height(q) + up);
            House(m, On(p, -0.2f), 7.5f, 5f, 3.2f, yaw, Jitter(Wattle, r, 0.06f), Jitter(Thatch, r, 0.08f));
            Chimney(m, On(p, -0.2f), 7.5f, 3.2f, yaw);
            var barn = p + ax * 11 + az * 5;
            House(m, On(barn, -0.2f), 10f, 6.5f, 3.6f, yaw, Jitter(new Color(120, 96, 70), r, 0.06f), Jitter(Thatch, r, 0.08f));
            var pen = p - az * 14;
            Vector2 c0 = pen - ax * 9 - az * 6, c1 = pen + ax * 9 - az * 6, c2 = pen + ax * 9 + az * 6, c3 = pen - ax * 9 + az * 6;
            Fence(m, c0, c1, r); Fence(m, c1, c2, r); Fence(m, c2, c3, r); Fence(m, c3, c3 + (c0 - c3) * 0.6f, r); // a gap for the gate
            for (int k = 0; k < 3; k++)
            {
                var q = p + ax * (-8 - k * 3.2f) + az * (6 + r.NextSingle() * 2);
                var b = On(q, -0.1f);
                m.Tag = Vector2.Zero;
                var hay = Jitter(Hay, r, 0.08f);
                m.Frustum(b, b + Vector3.Up * 1.6f, 1.4f, 1.0f, 8, hay, r);
                m.Frustum(b + Vector3.Up * 1.6f, b + Vector3.Up * 2.7f, 1.0f, 0, 8, hay, r);
            }
            m.Tag = Vector2.Zero;
            var cart = p + az * 7 + ax * 4;
            float cyaw = yaw + 0.3f;
            var cartWood = Jitter(new Color(110, 84, 58), r, 0.06f);
            m.Box(On(cart, 0.55f), 2.6f, 1.5f, 0.35f, cyaw, cartWood);
            foreach (float s in new[] { -0.85f, 0.85f })
            {
                var wq = cart + new Vector2(-MathF.Sin(cyaw), MathF.Cos(cyaw)) * s;
                Wheel(m, On(wq, 0.55f), 0.55f, 0.1f, cyaw, cartWood, Iron);
            }
            return;
        }
    }

    static Vector3 V3(Vector2 p, float y) => new(p.X, y, p.Y);

    /// A birch: a slim pale trunk and a narrow, light crown (golden in autumn).
    void Birch(Mesh m, Vector3 b, float s, Random r)
    {
        float th = (2.6f + r.NextSingle() * 1.0f) * s;
        m.Tag = Vector2.Zero;
        m.Frustum(b - Vector3.Up * 0.4f, b + Vector3.Up * th, 0.16f * s, 0.08f * s, 5, Jitter(Birchbark, r, 0.05f), r);
        m.Tag = new Vector2(0.45f, 1);
        var leaf = t.MapSeason == 2 ? Color.Lerp(new Color(190, 150, 60), new Color(214, 178, 74), r.NextSingle())
            : Color.Lerp(new Color(104, 140, 62), new Color(132, 160, 72), r.NextSingle());
        m.Blob(b + new Vector3(0, th + 0.6f * s, 0), new Vector3(0.95f, 1.75f, 0.95f) * s, leaf, r, 0.2f);
        m.Blob(b + new Vector3(0.35f * s, th - 0.4f * s, 0.2f * s), new Vector3(0.75f, 1.1f, 0.75f) * s, Jitter(leaf, r, 0.08f), r, 0.2f);
    }
}
