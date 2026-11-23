using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// MapScreen: the encounter's field (`MapScreen.Encounter.cs`). The backdrop is painted pixel by pixel into one texture at the
/// art's resolution and kept while the encounter, the hour and the window stay the same: the sky in dithered bands with its
/// clouds or stars, ranges behind ranges going blue with distance (high ground behind the side that holds it, a treeline where
/// there are woods, the sea on a coast), and the ground under the camera as it lies, its grass in four tones over noise, the
/// trampled track between the lines (a river where one runs), pools, haze, and each man's shadow at his feet. Trees, a hamlet,
/// hedges, stones and the banners are painted over it; the men go over those. Drawing only.
partial class MapScreen
{
    Texture2D? encBack;
    string encBackKey = "";

    static readonly float[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

    /// Four tones (dark to light) of a ground, and a dither between them by `v` (0..1).
    static Color Tone(Color[] tones, float v, int x, int y)
    {
        float f = Math.Clamp(v, 0, 0.999f) * (tones.Length - 1);
        int i = (int)f;
        return tones[Math.Min(tones.Length - 1, i + (f - i > (Bayer[y & 3, x & 3] + 0.5f) / 16f ? 1 : 0))];
    }

    static float Hash(int x, int y, int s)
    {
        uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)s * 0xCB1AB31Fu;
        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    static float Noise(float x, float y, int s)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy, s), b = Hash(ix + 1, iy, s), c = Hash(ix, iy + 1, s), d = Hash(ix + 1, iy + 1, s);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    static float Fbm(float x, float y, int s) => Noise(x, y, s) * 0.55f + Noise(x * 2.1f, y * 2.1f, s + 1) * 0.3f + Noise(x * 4.3f, y * 4.3f, s + 2) * 0.15f;

    /// A colour as the hour lights it: warm and low at dusk, blue and dark at night.
    static Color Lit(Color c, TownLook l) =>
        l.Night ? Color.Lerp(Kit.Mul(c, 0.5f), new Color(24, 30, 54), 0.38f)
        : l.Dusk ? new Color((int)(c.R * 0.86f), (int)(c.G * 0.66f), (int)(c.B * 0.62f))
        : c;

    /// The four grass tones of the country (before the hour).
    static Color[] GrassOf(TownLook l) =>
        l.Snow ? new Color[] { new(132, 142, 176), new(176, 186, 210), new(212, 218, 234), new(240, 242, 250) }
        : l.Country is "Desert" or "Oasis" or "Badlands" ? new Color[] { new(138, 104, 68), new(176, 140, 92), new(204, 170, 116), new(226, 198, 148) }
        : l.Country is "Steppe" or "Tundra" ? new Color[] { new(84, 82, 48), new(118, 112, 62), new(148, 138, 78), new(178, 166, 100) }
        : l.Country is "Forest" or "Taiga" or "Marsh" ? new Color[] { new(32, 50, 32), new(48, 70, 40), new(66, 92, 48), new(90, 114, 58) }
        : new Color[] { new(44, 64, 36), new(62, 88, 44), new(84, 112, 54), new(114, 136, 66) };

    static Color[] DirtOf(TownLook l) => l.Snow
        ? new Color[] { new(84, 76, 78), new(116, 108, 110), new(150, 144, 150), new(186, 184, 194) }
        : new Color[] { new(64, 46, 34), new(92, 68, 48), new(120, 92, 64), new(146, 118, 84) };

    /// The painted backdrop, made again only when what it shows changes.
    void EnsureBackdrop(Party e, TownLook l, TownColours c, TerrainParams g, FieldLayout layout)
    {
        int px = EncPixel, lw = (ui.W + px - 1) / px, lh = (ui.H + px - 1) / px;
        string key = $"{e.Id}|{l}|{layout}|{g.Favour}|{g.Wet > 0.5f}|{lw}x{lh}|{encMen.Count}|{C.Map.ProvinceAt(C.Player.Pos).Id}";
        if (encBack != null && key == encBackKey) return;
        encBackKey = key;
        var pix = new Color[lw * lh];
        var st = encStage!;
        int seed = C.Map.ProvinceAt(C.Player.Pos).Id * 7 + 3;
        var grass = GrassOf(l).Select(x => Lit(x, l)).ToArray();
        var dirt = DirtOf(l).Select(x => Lit(x, l)).ToArray();
        var haze = l.Night ? Color.Lerp(new Color(40, 44, 72), c.SkyLow, 0.25f) : Color.Lerp(c.SkyLow, c.Far, 0.2f);
        var eye = st.EyeAt;
        int horizon = (int)(st.Project(new Vector3(0, 0, -3000)).Y / px);
        bool hot = l.Country is "Desert" or "Oasis" or "Badlands";
        var water = Lit(l.Snow ? new Color(150, 170, 200) : new Color(60, 96, 124), l);
        int holder = g.Favour == 1 ? 1 : -1;
        int windSide = (Rng.Mix(C.S.Seed, e.Id) & 1) == 0 ? -1 : 1; // the wooded flank's side
        // ---- the sky ----
        for (int y = 0; y < lh; y++)
        {
            float t = Math.Clamp(y / (float)Math.Max(1, horizon), 0, 1);
            for (int x = 0; x < lw; x++)
            {
                float k = MathF.Pow(t, 1.5f) * 10;
                int band = (int)k;
                float frac = k - band;
                if (frac > (Bayer[y & 3, x & 3] + 0.5f) / 16f) band++;
                var col = Color.Lerp(c.SkyTop, c.SkyLow, band / 10f);
                if (l.Night)
                {
                    float h = Hash(x, y, 77);
                    if (t < 0.8f && h < 0.006f) col = Color.Lerp(col, new Color(230, 226, 255), 0.4f + h * 100);
                }
                // Clouds: streaks of noise, lit from above (or from below at dusk), thinning toward the horizon.
                float m = Fbm(x * 0.03f, y * 0.11f, seed) - 0.56f + (1 - t) * 0.04f;
                if (!l.Night || m > 0.05f)
                    if (m > 0)
                    {
                        float under = Fbm(x * 0.03f, (y - 1.5f) * 0.11f, seed) - 0.56f; // the cloud's body above this pixel: lit from there
                        var lit = l.Dusk ? new Color(250, 168, 120) : l.Night ? new Color(70, 72, 108) : Color.Lerp(c.SkyLow, Color.White, 0.75f);
                        var shade = l.Dusk ? new Color(140, 84, 110) : l.Night ? new Color(36, 40, 70) : Color.Lerp(c.SkyTop, c.SkyLow, 0.6f);
                        var cc = Color.Lerp(lit, shade, Math.Clamp(under * 6, 0, 1));
                        col = Color.Lerp(col, cc, m * 8 > 1 ? 0.92f : (m * 8 > (Bayer[y & 3, x & 3] + 0.5f) / 16f ? 0.7f : 0));
                    }
                pix[y * lw + x] = col;
            }
        }
        // ---- the ranges behind the field, far to near ----
        float scale = lh / 240f;
        void Range(Func<int, float> top, Color col, Color rim)
        {
            for (int x = 0; x < lw; x++)
            {
                int y0 = (int)top(x);
                for (int y = Math.Max(0, y0); y < Math.Min(lh, horizon + 1); y++) pix[y * lw + x] = y == y0 ? rim : col;
            }
        }
        float tall = l.Country switch { "Highlands" => 2.2f, "Hills" => 1.5f, "Steppe" or "Tundra" or "Desert" or "Coast" => 0.5f, _ => 1f };
        var far = Color.Lerp(Lit(new Color(110, 120, 150), l), c.SkyLow, 0.55f);
        var mid = Color.Lerp(Lit(new Color(84, 98, 104), l), c.SkyLow, 0.35f);
        Range(x => horizon - (5 + Fbm(x * 0.012f, 3, seed + 9) * 22 * tall) * scale, far, Color.Lerp(far, c.Rim, l.Night ? 0.08f : 0.25f));
        Range(x => horizon - (2 + Fbm(x * 0.02f, 5, seed + 13) * 12 * tall) * scale, mid, Color.Lerp(mid, c.Rim, l.Night ? 0.06f : 0.2f));
        if (l.Country == "Coast") Range(x => horizon - 2 * scale, Color.Lerp(water, c.SkyLow, 0.4f), Color.Lerp(Color.Lerp(water, c.SkyLow, 0.4f), c.SkyLow, 0.5f));
        // High ground behind the side that holds it; a pass between steep sides.
        var rise = Color.Lerp(grass[1], haze, 0.35f);
        float Bump(int x, float centre, float width) => MathF.Max(0, 1 - MathF.Pow((x / (float)lw - centre) / width, 2));
        switch (layout)
        {
            case FieldLayout.Ridge: Range(x => horizon + 1 - 16 * scale * Bump(x, holder < 0 ? 0.2f : 0.8f, 0.42f) - Noise(x * 0.1f, 1, seed) * 2, rise, Color.Lerp(rise, c.Rim, 0.3f)); break;
            case FieldLayout.TwoHills:
                Range(x => horizon + 1 - 12 * scale * MathF.Max(Bump(x, 0.14f, 0.24f), Bump(x, 0.86f, 0.24f)) - Noise(x * 0.1f, 1, seed) * 2, rise, Color.Lerp(rise, c.Rim, 0.3f)); break;
            case FieldLayout.Pass:
            {
                var rock = Color.Lerp(Lit(new Color(98, 96, 92), l), haze, 0.25f);
                Range(x => horizon + 1 - 60 * scale * MathF.Pow(MathF.Max(Bump(x, -0.05f, 0.5f), Bump(x, 1.05f, 0.5f)), 0.8f) - Fbm(x * 0.08f, 2, seed) * 6 * scale, rock, Color.Lerp(rock, c.Rim, 0.3f));
                break;
            }
        }
        // A treeline where there are woods: crowns along the horizon, the nearer the darker.
        bool woods = layout is FieldLayout.ForestClearing or FieldLayout.WoodedFlank || l.Country is "Forest" or "Taiga";
        if (woods)
        {
            var tree = Color.Lerp(Lit(l.Snow ? new Color(70, 84, 82) : new Color(34, 56, 36), l), haze, 0.3f);
            bool pines = l.Country is "Taiga" || l.Snow;
            Range(x =>
            {
                float side = layout == FieldLayout.WoodedFlank ? MathF.Max(0, Math.Clamp((x / (float)lw - 0.5f) * windSide * 4, 0, 1)) : 1;
                if (side <= 0) return lh;
                float crown = pines ? MathF.Abs(((x + seed) % 7) - 3) / 3f : Noise(x * 0.35f, 4, seed);
                return horizon - (4 + crown * 5 + Noise(x * 0.05f, 6, seed) * 6) * scale * side;
            }, tree, Color.Lerp(tree, c.Rim, 0.2f));
        }
        // ---- the ground ----
        for (int y = Math.Max(0, horizon); y < lh; y++)
            for (int x = 0; x < lw; x++)
            {
                if (st.GroundAt((x + 0.5f) * px, (y + 0.5f) * px) is not Vector2 p) continue;
                float dist = Vector2.Distance(new Vector2(eye.X, eye.Z), p);
                if (dist > 2500) continue;
                float n = Fbm(p.X * 0.16f, p.Y * 0.16f, seed), fine = Noise(p.X * 1.3f, p.Y * 1.3f, seed + 5);
                float v = 0.4f + (n - 0.5f) * 0.85f + (fine - 0.5f) * 0.18f * MathF.Max(0, 1 - dist / 60);
                v += (1 - y / (float)lh) * 0.18f; // the far field catches more light
                var col = Tone(grass, v, x, y);
                // The track the armies would meet on, or the river between them.
                float wob = MathF.Sin(p.Y * 0.07f) * 1.6f + MathF.Sin(p.Y * 0.19f + 1) * 0.6f;
                float d = MathF.Abs(p.X - wob);
                if (layout == FieldLayout.RiverLine)
                {
                    float edge = 1.8f + n * 0.6f;
                    if (d < edge)
                    {
                        float ripple = MathF.Sin(p.Y * 2.4f + Noise(p.X * 2, p.Y * 0.6f, seed) * 7);
                        col = Color.Lerp(water, Color.Lerp(c.SkyLow, c.SkyTop, 0.3f), 0.25f + Math.Clamp(dist / 120, 0, 0.5f));
                        if (ripple > 0.86f) col = Color.Lerp(col, Lit(new Color(220, 230, 240), l), 0.5f);
                        else if (ripple < -0.8f) col = Kit.Mul(col, 0.85f);
                    }
                    else if (d < edge + 0.7f) col = Tone(dirt, 0.25f + fine * 0.3f, x, y);
                }
                else
                {
                    float edge = 0.7f + n * 0.9f;
                    if (d < edge) col = Tone(dirt, 0.42f + (fine - 0.5f) * 0.3f + (n - 0.5f) * 0.4f, x, y);
                    else if (d < edge + 1.2f && Hash(x, y, 3) < 0.7f - (d - edge) / 1.2f * 0.7f) col = Color.Lerp(col, Tone(dirt, 0.6f, x, y), 0.6f); // worn grass at its edge
                    if (d < edge && g.Wet > 0.5f && Noise(p.X * 0.5f, p.Y * 0.5f, seed + 8) > 0.68f) col = Color.Lerp(water, c.SkyLow, 0.3f); // puddles in the ruts
                }
                if (layout == FieldLayout.Fen && Fbm(p.X * 0.22f, p.Y * 0.22f, seed + 21) > 0.62f && !(MathF.Abs(p.X) is > 2.5f and < 13 && p.Y > -36))
                    col = Color.Lerp(water, c.SkyLow, 0.35f);
                if (hot && Noise(p.X * 0.6f, p.Y * 2.2f, seed + 4) > 0.72f) col = Tone(grass, v + 0.25f, x, y); // wind ripples in the sand
                // Haze toward the horizon.
                float hz = MathF.Pow(Math.Clamp((dist - 14) / 150, 0, 1), 0.6f) * 0.8f;
                pix[y * lw + x] = Color.Lerp(col, haze, hz);
            }
        // Tufts and flowers in the near grass: a lit blade with its shadow under it.
        var rng = new Random(seed);
        var bloom = Lit(l.Snow ? new Color(250, 250, 255) : hot ? new Color(232, 210, 160) : new Color(226, 214, 120), l);
        for (int i = 0; i < lw * lh / 110; i++)
        {
            int x = rng.Next(lw), y = horizon + 2 + (int)((lh - horizon - 2) * MathF.Sqrt(rng.NextSingle()));
            if (y >= lh - 1 || st.GroundAt((x + 0.5f) * px, (y + 0.5f) * px) is not Vector2 p) continue;
            if (MathF.Abs(p.X - (MathF.Sin(p.Y * 0.07f) * 1.6f + MathF.Sin(p.Y * 0.19f + 1) * 0.6f)) < 2.6f) continue; // not on the track
            bool flower = !l.Night && rng.NextSingle() < 0.12f && !l.Snow;
            pix[y * lw + x] = flower ? bloom : Color.Lerp(grass[3], c.Rim, 0.15f);
            pix[(y + 1) * lw + x] = grass[0];
            if (y > horizon + 12 && rng.NextSingle() < 0.5f && y > 0) pix[(y - 1) * lw + x] = Color.Lerp(grass[2], pix[(y - 1) * lw + x], 0.3f);
        }
        // Each man's shadow at his feet, thrown a little away from the light.
        foreach (var u in encMen)
        {
            var f = st.Project(new Vector3(u.Pos.X + 0.25f, 0, u.Pos.Y - 0.1f));
            float pm = st.PerMetre(u.Pos.Y) / px;
            float rx = pm * (u.Mounted ? 1.25f : u.Type.Look.Beast ? 0.9f * u.Type.Size : 0.5f * MathF.Max(1, u.Type.Size)), ry = MathF.Max(1, rx * 0.32f);
            int cx = (int)(f.X / px), cy = (int)(f.Y / px);
            for (int y = (int)(cy - ry); y <= cy + ry; y++)
                for (int x = (int)(cx - rx); x <= cx + rx; x++)
                {
                    if (x < 0 || y < 0 || x >= lw || y >= lh) continue;
                    float q = MathF.Pow((x - cx) / rx, 2) + MathF.Pow((y - cy) / ry, 2);
                    if (q > 1) continue;
                    if (q > 0.6f && (Bayer[y & 3, x & 3] + 0.5f) / 16f < (q - 0.6f) / 0.4f) continue; // a dithered edge
                    pix[y * lw + x] = Color.Lerp(pix[y * lw + x], Lit(new Color(20, 22, 30), l), 0.5f);
                }
        }
        if (encBack == null || encBack.Width != lw || encBack.Height != lh) { encBack?.Dispose(); encBack = new Texture2D(gd, lw, lh); }
        encBack.SetData(pix);
    }

    /// The backdrop, then what stands on the field behind the men: trees, the hamlet, hedges, stones, the banners.
    void FieldScene(Party e, TownLook l, TownColours c, TerrainParams g, FieldLayout layout, float t)
    {
        EnsureBackdrop(e, l, c, g, layout);
        int s0 = ui.S;
        ui.S = 1;
        try { ui.Sb.Draw(encBack!, new Rectangle(0, 0, encBack!.Width * EncPixel, encBack.Height * EncPixel), Color.White); }
        finally { ui.S = s0; }
        int horizon = (int)EP(0, -3000).Y;
        Orb(new Rectangle(0, horizon * 45 / 100, ui.W, horizon - horizon * 45 / 100), l, 0.64f);
        snowCap = l.Snow ? c.Snow : Color.Transparent;
        var rng = new Random(C.Map.ProvinceAt(C.Player.Pos).Id * 31 + 5);
        FieldProps(e, l, c, g, layout, rng);
        // The hovered company's ground glows under it.
        if (encHover >= 0)
        {
            var b = encBlocks[encHover];
            float cx = b.Men.Average(m => m.Pos.X), cz = b.Men.Average(m => m.Pos.Y);
            float rx = b.Men.Max(m => MathF.Abs(m.Pos.X - cx)) + 1.3f, rz = b.Men.Max(m => MathF.Abs(m.Pos.Y - cz)) + 1.1f;
            GroundRing(cx, cz, rx, rz, b.Side < 0 ? Style.Friendly : Style.Foe, t);
        }
        Banners(e, l, t);
        snowCap = Color.Transparent;
    }

    /// A ring of light on the ground round a company (the hovered one).
    void GroundRing(float x, float z, float rx, float rz, Color col, float t)
    {
        float a = 0.55f + 0.2f * MathF.Sin(t * 4);
        for (int i = 0; i < 72; i++)
        {
            float ang = i / 72f * MathHelper.TwoPi;
            var p = EP(x + MathF.Cos(ang) * rx, z + MathF.Sin(ang) * rz);
            ui.Rect(new Rectangle((int)p.X - ui.U(1), (int)p.Y, ui.U(2), ui.U(1)), col * a);
        }
    }

    /// Each company's banner behind its front rank: yours on your company's charge, theirs in their colours; none over beasts.
    void Banners(Party e, TownLook l, float t)
    {
        var st = encStage!;
        var mineHd = Style.Sides[C.PlayerPalette];
        var theirHd = Style.Sides[Style.PaletteOf(C.SideOf(e))];
        foreach (var b in encBlocks.Where(b => b.Side < 0 || e.Kind != PartyKind.Monster).OrderBy(b => b.Back.Y))
        {
            var p = EP(b.Back.X, b.Back.Y);
            float pm = st.PerMetre(b.Back.Y);
            int top = (int)(p.Y - (b.Men.Any(m => m.Mounted) ? 4.6f : 3.7f) * pm), bw = Math.Max(ui.U(5), (int)(0.7f * pm)) / ui.S * ui.S;
            var pole = l.Night ? new Color(44, 32, 28) : new Color(92, 66, 44);
            ui.Rect(new Rectangle((int)p.X, top, Math.Max(ui.U(1), (int)(0.07f * pm) / ui.S * ui.S), (int)p.Y - top), pole);
            ui.Rect(new Rectangle((int)p.X - ui.U(1), top - ui.U(2), ui.U(3), ui.U(2)), l.Night ? Kit.Mul(Kit.Gilt, 0.5f) : Kit.Gilt); // the finial
            var hd = b.Side < 0 ? mineHd with { Charge = (Emblem)b.K.Banner } : theirHd;
            int dir = b.Side < 0 ? 1 : -1; // the cloth flies toward the foe
            ui.Banner((int)p.X + dir * (bw / 2 + ui.U(1)), top + ui.U(1), bw, (int)(1.05f * pm), hd, t * 1.3f + b.K.Id);
        }
    }

    /// Sprites on the field by layout, all behind the companies: framing trees, a wood on one flank, a hamlet, hedges, stones.
    void FieldProps(Party e, TownLook l, TownColours c, TerrainParams g, FieldLayout layout, Random rng)
    {
        int s0 = ui.S;
        ui.S = EncPixel;
        try
        {
            int holder = g.Favour == 1 ? 1 : -1, wood = (Rng.Mix(C.S.Seed, e.Id) & 1) == 0 ? -1 : 1;
            void Trees(float x0, float x1, float z0, float z1, int n, float size = 1)
            {
                var pts = Enumerable.Range(0, n).Select(_ => (X: x0 + rng.NextSingle() * (x1 - x0), Z: z0 + rng.NextSingle() * (z1 - z0))).OrderBy(p => p.Z).ToList();
                foreach (var (x, z) in pts) { var p = EP(x, z); Tree((int)p.X, (int)p.Y, (int)(encStage!.PerMetre(z) * (6 + rng.NextSingle() * 3) * size), l, c, rng); }
            }
            bool open = l.Country is "Steppe" or "Tundra" or "Desert" or "Coast" or "Badlands";
            switch (layout)
            {
                case FieldLayout.WoodedFlank: Trees(wood * 12, wood * 30, -70, -18, 22); break;
                case FieldLayout.ForestClearing: Trees(-50, -16, -80, -10, 14); Trees(16, 50, -80, -10, 14); break;
                case FieldLayout.Village:
                    for (int i = 0; i < 5; i++)
                    {
                        float z = -34 - i % 3 * 7 - rng.NextSingle() * 3, x = holder * (7 + i * 4.2f + rng.NextSingle() * 2);
                        Hut(EP(x, z), encStage!.PerMetre(z), l, c);
                    }
                    Trees(holder * 8, holder * 30, -64, -54, 2, 0.8f);
                    break;
                case FieldLayout.Hedgerows:
                    foreach (var z in new[] { -22f, -44f })
                        foreach (var (x0, x1) in new[] { (-60f, -4f), (4f, 60f) })
                        {
                            var a = EP(x0, z); var b = EP(x1, z);
                            float pm = encStage!.PerMetre(z);
                            var hedge = Lit(new Color(46, 74, 40), l);
                            int top = (int)(a.Y - 1.3f * pm);
                            ui.Rect(new Rectangle((int)a.X, top, (int)(b.X - a.X), (int)a.Y - top), hedge);
                            for (float x = a.X; x < b.X; x += ui.U(3)) ui.Rect(new Rectangle((int)x, top - ui.U(rng.Next(2)), ui.U(2), ui.U(1)), Color.Lerp(hedge, c.Rim, 0.25f));
                        }
                    break;
                case FieldLayout.Broken:
                    for (int i = 0; i < 18; i++)
                    {
                        float x = -24 + rng.NextSingle() * 48, z = -50 + rng.NextSingle() * 62;
                        if (MathF.Abs(x) is > 2 and < 13 && z > -40 || MathF.Abs(x) < 3) continue; // not under the companies, not on the track
                        var p = EP(x, z); float pm = encStage!.PerMetre(z) * (0.5f + rng.NextSingle() * 0.6f);
                        var stone = Lit(new Color(126, 120, 110), l);
                        ui.Disc(new Vector2(p.X, p.Y - pm * 0.35f), pm * 0.6f, Kit.Mul(stone, 0.7f));
                        ui.Disc(new Vector2(p.X - pm * 0.15f, p.Y - pm * 0.5f), pm * 0.42f, stone);
                        ui.Rect(new Rectangle((int)(p.X - pm * 0.3f), (int)(p.Y - pm * 0.85f), (int)(pm * 0.3f), ui.U(1)), Color.Lerp(stone, c.Rim, 0.4f));
                    }
                    break;
            }
            // Framing trees at the screen's edges, near, where the country has them.
            if (!open && layout is not FieldLayout.Pass)
            {
                Trees(-24, -19, -4, 4, 2, 1.2f);
                Trees(19, 24, -4, 4, 2, 1.2f);
            }
        }
        finally { ui.S = s0; }
    }

    /// A cottage of the field's hamlet: walls, a pitched roof, a door (lit at night), a chimney.
    void Hut(Vector2 foot, float pm, TownLook l, TownColours c)
    {
        int w = (int)(3.6f * pm), wh = (int)(2.1f * pm), rh = (int)(1.7f * pm), x0 = (int)foot.X - w / 2, y0 = (int)foot.Y;
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(x0, y0 - wh, w, wh), c.Wall);
        ui.Rect(new Rectangle(x0, y0 - wh, Math.Max(s, w / 5), wh), Kit.Mul(c.Wall, 0.8f));
        ui.Rect(new Rectangle((int)foot.X + w / 3, y0 - wh - rh, Math.Max(s, w / 10), rh), Kit.Mul(c.Wall, 0.7f)); // the chimney
        for (int r = 0; r < rh; r += s)
        {
            int half = (int)((w / 2f + pm * 0.35f) * r / rh);
            ui.Rect(new Rectangle((int)foot.X - half, y0 - wh - rh + r, half * 2, s), r < s * 2 ? Color.Lerp(c.Roof, c.Rim, 0.3f) : c.Roof);
            ui.Rect(new Rectangle((int)foot.X - half, y0 - wh - rh + r, Math.Max(s, half / 3), s), Kit.Mul(c.Roof, 0.8f));
        }
        if (snowCap.A > 0) for (int r = 0; r < rh / 3; r += s) { int half = (int)(w / 2f * r / rh); ui.Rect(new Rectangle((int)foot.X - half, y0 - wh - rh + r, half * 2, s), snowCap); }
        ui.Rect(new Rectangle((int)foot.X - (int)(0.35f * pm), y0 - (int)(1.2f * pm), (int)(0.7f * pm), (int)(1.2f * pm)), l.Night ? new Color(255, 184, 92) : new Color(46, 36, 32));
        if (l.Night) ui.Rect(new Rectangle((int)foot.X - (int)(0.9f * pm), y0, (int)(1.8f * pm), Math.Max(s, (int)(0.15f * pm))), new Color(255, 184, 92) * 0.35f);
    }

    /// The hour over everything (men too): night's blue, dusk's warmth; rain or snow falling, fog laid over.
    void FieldWeather(TownLook l, TownColours c, Weather w, float t)
    {
        var full = new Rectangle(0, 0, ui.W, ui.H);
        if (l.Night) ui.Rect(full, Kit.Night * 0.32f);
        else if (l.Dusk) ui.VGrad(full, new Color(120, 60, 80) * 0.05f, new Color(60, 30, 50) * 0.2f, 8);
        if (w.Fog > 0.05f) ui.VGrad(full, Color.Lerp(c.SkyLow, Color.Gray, 0.4f) * (w.Fog * 0.2f), Color.Lerp(c.SkyLow, Color.Gray, 0.5f) * (w.Fog * 0.5f), 4);
        if (w.Snow || l.Snow && w.Rain >= 0.3f) { Snowfall(full, t, l.Night); return; }
        if (w.Rain < 0.3f) return;
        var rng = new Random(91);
        var drop = (l.Night ? new Color(140, 150, 190) : new Color(200, 210, 230)) * 0.3f;
        int n = (int)(220 * w.Rain), s = Math.Max(1, ui.U(1));
        for (int i = 0; i < n; i++)
        {
            float speed = 0.6f + rng.NextSingle() * 0.4f, near = rng.NextSingle();
            int len = ui.U(4 + (int)(near * 6));
            int x = (int)((rng.Next(ui.W + ui.U(40)) - t * ui.U(20) * speed) % (ui.W + ui.U(40)));
            if (x < 0) x += ui.W + ui.U(40);
            int y = (int)((rng.Next(ui.H) + t * ui.U(160) * speed) % ui.H);
            for (int k = 0; k < len; k += s) ui.Rect(new Rectangle(x - k / 3, y + k, s, s), drop * (0.5f + near * 0.5f));
        }
    }
}
