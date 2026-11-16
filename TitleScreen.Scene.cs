using Microsoft.Xna.Framework;

namespace Ironfield;

/// TitleScreen: the painted scene. The sun going down behind far peaks, a castle on its crag, a hamlet and pines on the
/// middle hills, and on the near ridge a company on the march under the colours of your last save. Drawing only, made of
/// rectangles, discs and lines; everything moves on `time` and leans a little with the mouse (far layers less than near ones).
sealed partial class TitleScreen
{
    static readonly (float At, Color C)[] Sky =
    {
        (0f, new Color(9, 11, 28)), (0.3f, new Color(28, 25, 64)), (0.55f, new Color(84, 44, 90)),
        (0.76f, new Color(178, 78, 84)), (0.9f, new Color(238, 138, 78)), (1f, new Color(255, 198, 116)),
    };
    static readonly Color FarC = new(122, 66, 94), CragC = new(74, 43, 73), MidC = new(43, 29, 51), NearC = new(29, 21, 39), FrontC = new(7, 5, 11),
        Rim = new(255, 172, 104), Flame = new(255, 206, 128);

    enum Who { Spear, Torch, Bow, Banner, Rider, Standard, Wagon }

    /// The company from its head back: who walks where, in units behind the first rider.
    static readonly List<(Who Who, float Back)> March = BuildMarch();
    static float marchLength;

    static List<(Who, float)> BuildMarch()
    {
        var l = new List<(Who, float)>();
        float x = 0;
        void Add(Who w, float gap) { l.Add((w, x)); x += gap; }
        Add(Who.Rider, 44); Add(Who.Standard, 46); Add(Who.Rider, 42); Add(Who.Rider, 50);
        Add(Who.Banner, 17);
        for (int i = 0; i < 10; i++) Add(i % 5 == 4 ? Who.Torch : Who.Spear, 12 + i % 2 * 3);
        x += 12; Add(Who.Banner, 17);
        for (int i = 0; i < 8; i++) Add(i == 3 ? Who.Torch : Who.Spear, 12 + i % 2 * 3);
        x += 12;
        for (int i = 0; i < 6; i++) Add(Who.Bow, 13);
        x += 34; Add(Who.Wagon, 40);
        for (int i = 0; i < 4; i++) Add(i == 1 ? Who.Torch : Who.Spear, 13);
        marchLength = x;
        return l;
    }

    float lean;  // the mouse's lean in pixels (the near ridge moves by all of it)
    int u = 1;   // one interface unit, at least a pixel

    Heraldry Colours => Style.Sides[card?.Palette ?? 0];

    /// A length of the picture: 1 at 540 lines, growing with the window (the men and the castle keep whole pixels instead).
    float Q(float v) => v * ui.H / 540f;
    static float Tri(float a) { a -= MathF.Floor(a); return 1 - MathF.Abs(2 * a - 1); }
    void R(float x, float y, float w, float h, Color c) => ui.Rect(new Rectangle((int)x, (int)y, Math.Max(1, (int)w), Math.Max(1, (int)h)), c);

    void Scene()
    {
        u = Math.Max(1, U(1));
        lean = Ui.Instant ? 0 : (ms.X / (float)Math.Max(1, ui.W) - 0.5f) * Q(18);
        PaintSky();
        ui.Stars(new Rectangle(0, 0, ui.W, (int)(ui.H * 0.46f)), time, 120);
        ShootingStar();
        Sun();
        Clouds();
        Layer(FarY, 0.2f, FarC, Rim * 0.3f);
        Haze(0.665f, 0.75f, new Color(255, 158, 96), 0.3f);
        Flock();
        Layer(CragY, 0.4f, CragC, Rim * 0.22f);
        Castle();
        Haze(0.735f, 0.81f, new Color(232, 122, 90), 0.26f);
        Layer(MidY, 0.7f, MidC, Rim * 0.16f);
        Pines();
        Hamlet();
        Haze(0.8f, 0.9f, new Color(226, 120, 86), 0.62f);
        Layer(NearY, 1f, NearC, Rim * 0.5f);
        Column();
        Grass(NearY, 1f, NearC, 1, 5);
        Layer(FrontY, 1.4f, FrontC, Rim * 0.12f);
        Foreground();
        Grass(FrontY, 1.4f, FrontC, 2.6f, 4);
        Embers();
        ui.HGrad(new Rectangle(0, 0, (int)(ui.W * 0.62f), ui.H), Color.Black * 0.55f, Color.Black * 0f, 6);
        ui.Vignette(0.75f);
    }

    // ---------- the sky ----------

    void PaintSky()
    {
        int s = 2 * u, h = (int)(ui.H * 0.74f);
        for (int y = 0; y < h; y += s)
        {
            float t = y / (float)h;
            int k = 0;
            while (k < Sky.Length - 2 && t > Sky[k + 1].At) k++;
            ui.Rect(new Rectangle(0, y, ui.W, s), Color.Lerp(Sky[k].C, Sky[k + 1].C, Math.Clamp((t - Sky[k].At) / (Sky[k + 1].At - Sky[k].At), 0, 1)));
        }
    }

    /// The low sun: a wide glow, the disc paler at its top, half sunk behind the far range.
    void Sun()
    {
        var c = new Vector2(ui.W * 0.66f - lean * 0.1f, ui.H * 0.655f);
        for (int i = 18; i >= 1; i--) ui.Disc(c, Q(62 + i * 11), new Color(255, 150, 84) * 0.021f);
        ui.Disc(c, Q(150), new Color(255, 170, 96) * 0.06f); ui.Disc(c, Q(108), new Color(255, 176, 100) * 0.1f);
        ui.Disc(c, Q(74), new Color(255, 196, 120) * 0.2f);
        float r = Q(62);
        for (float y = -r; y <= r; y += u)
        {
            float w = MathF.Sqrt(MathF.Max(0, r * r - y * y));
            R(c.X - w, c.Y + y, 2 * w, u, Color.Lerp(new Color(255, 246, 206), new Color(255, 196, 116), (y + r) / (2 * r)));
        }
    }

    /// A streak of cloud in stacked runs, dark on top and lit from beneath.
    void Streak(float cx, float cy, float w, int rows, int seed, Color top, Color under, float alpha)
    {
        for (int r = 0; r < rows; r++)
        {
            float f = rows == 1 ? 0.5f : r / (float)(rows - 1);
            float wr = w * (0.3f + 0.7f * MathF.Sin(MathF.PI * (0.15f + 0.7f * f))) * (0.75f + 0.25f * Ease.Hash(seed * 31 + r));
            float off = (Ease.Hash(seed * 17 + r) - 0.5f) * w * 0.2f;
            R(cx + off - wr / 2, cy + r * 2 * u, wr, 2 * u, Color.Lerp(top, under, f) * alpha);
        }
    }

    /// Long clouds drifting across the dusk: plum and faint high up, ember-bellied near the horizon, two bars over the sun.
    void Clouds()
    {
        float span = ui.W + Q(520);
        for (int i = 0; i < 11; i++)
        {
            float h = i / 10f; // 0 high, 1 at the horizon
            float x = (Ease.Hash(i * 7 + 3) * span + time * Q(2.5f + 4 * Ease.Hash(i + 40))) % span - Q(260);
            float y = ui.H * (0.13f + 0.47f * h + 0.03f * (Ease.Hash(i * 5 + 1) - 0.5f));
            Streak(x - lean * 0.1f, y, Q(150 + 260 * Ease.Hash(i * 3 + 9)), 3 + (int)(4 * Ease.Hash(i + 20) * (1 - 0.5f * h)), i,
                Color.Lerp(new Color(52, 40, 88), new Color(142, 66, 86), h), Color.Lerp(new Color(112, 70, 122), new Color(255, 178, 112), h), 0.45f + 0.45f * h);
        }
        float sx = ui.W * 0.66f - lean * 0.1f + MathF.Sin(time * 0.05f) * Q(30);
        Streak(sx - Q(18), ui.H * 0.6f, Q(230), 3, 91, new Color(122, 56, 78), new Color(214, 108, 84), 0.95f);
        Streak(sx + Q(46), ui.H * 0.64f, Q(150), 2, 92, new Color(140, 62, 80), new Color(226, 120, 88), 0.95f);
    }

    /// Once in a while a star falls.
    void ShootingStar()
    {
        if (Ui.Instant) return;
        float cycle = time % 23f;
        if (cycle > 0.7f) return;
        int n = (int)(time / 23f);
        float t = cycle / 0.7f;
        var a = new Vector2(ui.W * (0.2f + 0.5f * Ease.Hash(n * 13 + 5)), ui.H * (0.05f + 0.15f * Ease.Hash(n * 29 + 2)));
        var p = a + new Vector2(Q(150), Q(54)) * t;
        ui.Line(p - new Vector2(Q(34), Q(12)) * MathF.Min(1, t * 3), p, Color.White * (0.7f * (1 - t)), 1);
    }

    /// Geese going home in a skein, high over the hills.
    void Flock()
    {
        float span = ui.W + U(300);
        float fx = span - (time * U(9) + span * 0.55f) % span - U(150), fy = ui.H * 0.34f + MathF.Sin(time * 0.3f) * U(6);
        var c = new Color(34, 22, 40);
        for (int i = 0; i < 9; i++)
        {
            int rank = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1;
            var at = new Vector2(fx + rank * U(11), fy + side * rank * U(5) + MathF.Sin(time * 1.1f + i) * u);
            float flap = MathF.Sin(time * 7 + i * 1.9f) * U(2.5f);
            ui.Line(at, at + new Vector2(-U(4), -flap), c, 1);
            ui.Line(at, at + new Vector2(U(4), -flap), c, 1);
        }
    }

    // ---------- the land ----------

    float FarY(float wx)
    {
        float t = wx / Q(100);
        return ui.H * 0.7f - Q(92) * (0.58f * Tri(t * 0.21f + 0.68f) + 0.24f * Tri(t * 0.53f + 0.35f) + 0.12f * Tri(t * 1.37f + 0.2f) + 0.06f * Tri(t * 3.3f));
    }

    float CastleX => ui.W * 0.835f;

    /// Rolling ground with a flat-topped crag under the castle.
    float CragY(float wx)
    {
        float t = wx / Q(100), d = MathF.Abs(wx - CastleX) / u;
        float top = d < 62 ? 1 : d > 170 ? 0 : 0.5f + 0.5f * MathF.Cos(MathF.PI * (d - 62) / 108);
        float rough = d < 62 ? 0 : 5 * Tri(t * 4.3f) * top;
        return ui.H * 0.74f - Q(12) * (0.6f * MathF.Sin(t * 1.1f + 0.4f) + 0.4f * MathF.Sin(t * 2.7f + 1.7f)) - u * (72 * top + rough);
    }

    float MidY(float wx)
    {
        float t = wx / Q(100);
        return ui.H * 0.805f - Q(16) * (0.55f * MathF.Sin(t * 1.3f + 3.1f) + 0.3f * MathF.Sin(t * 2.9f + 4.8f) + 0.15f * MathF.Sin(t * 6.1f));
    }

    float NearY(float wx)
    {
        float t = wx / Q(100), rise = Math.Clamp(wx / Math.Max(1, ui.W), 0, 1);
        return ui.H * 0.915f - Q(9) * (0.6f * MathF.Sin(t * 0.9f + 5.7f) + 0.4f * MathF.Sin(t * 2.3f + 1f)) - Q(18) * rise * rise * (3 - 2 * rise);
    }

    /// The bank nearest the eye, along the foot of the picture.
    float FrontY(float wx)
    {
        float t = wx / Q(100);
        return ui.H * 0.962f - Q(9) * (0.6f * MathF.Sin(t * 1.7f + 2.2f) + 0.4f * MathF.Sin(t * 3.9f + 0.6f)) + Q(14) * Math.Clamp(wx / Math.Max(1, ui.W) - 0.25f, 0, 1);
    }

    /// On the near bank: stones, and two spears left standing with a helm and a rag of cloth on them.
    void Foreground()
    {
        for (int i = 0; i < 9; i++)
        {
            float wx = ui.W * (0.06f + 0.93f * Ease.Hash(i * 13 + 4)), x = wx - lean * 1.4f, y = FrontY(wx) + 2 * u, s = 5 + 9 * Ease.Hash(i * 5 + 2);
            R(x - s * u, y - s * 0.5f * u, 2 * s * u, s * u, FrontC); R(x - s * 0.6f * u, y - s * 0.8f * u, s * 1.3f * u, s * 0.4f * u, FrontC);
            R(x - s * 0.6f * u, y - s * 0.8f * u - u, s * 0.9f * u, u, Rim * 0.14f);
        }
        float gx = ui.W * 0.41f, px = gx - lean * 1.4f, gy = FrontY(gx) + 2 * u;
        ui.Line(new Vector2(px, gy), new Vector2(px + 7 * u, gy - 62 * u), FrontC, 2);
        ui.Line(new Vector2(px + 26 * u, gy), new Vector2(px + 18 * u, gy - 50 * u), FrontC, 2);
        R(px + 6 * u, gy - 68 * u, 3 * u, 7 * u, FrontC);
        R(px + 14 * u, gy - 55 * u, 9 * u, 6 * u, FrontC); R(px + 12 * u, gy - 50 * u, 13 * u, 2 * u, FrontC); // the helm hung on it
        Flag(px + 6 * u, gy - 58 * u, 16 * u, 7 * u, time * 2.6f, -1, Color.Lerp(Colours.Cloth, FrontC, 0.55f), Color.Lerp(Colours.Trim, FrontC, 0.55f));
    }

    /// A range as a silhouette with a lit edge along its crest. `depth` is how far it follows the mouse.
    void Layer(Func<float, float> yAt, float depth, Color c, Color rim)
    {
        int s = 2 * u;
        for (int x = 0; x < ui.W; x += s)
        {
            int y = (int)yAt(x + lean * depth) / u * u;
            ui.Rect(new Rectangle(x, y - u, s, u), rim);
            ui.Rect(new Rectangle(x, y, s, ui.H - y), c);
        }
    }

    /// Evening haze lying in front of a range: nothing at `from`, thickest from `to` down (fractions of the height).
    void Haze(float from, float to, Color c, float a)
    {
        int y0 = (int)(ui.H * from), y1 = (int)(ui.H * to);
        ui.VGrad(new Rectangle(0, y0, ui.W, y1 - y0), c * 0f, c * a, 2);
        ui.Rect(new Rectangle(0, y1, ui.W, ui.H - y1), c * a);
    }

    /// The castle on the crag: a curtain wall, five towers and a keep, its windows lit and the company's colours flying.
    void Castle()
    {
        int cx = (int)(CastleX - lean * 0.4f), g = (int)CragY(CastleX) / u * u, k = 2 * u;
        var lit = Color.Lerp(CragC, Rim, 0.42f);
        var fire = Color.Lerp(Kit.Ember, Flame, 0.5f + 0.5f * MathF.Sin(time * 7.3f) * MathF.Sin(time * 3.1f));
        void B(int x, int top, int w, int h, Color? c = null) => ui.Rect(new Rectangle(cx + x * k, g - top * k, w * k, h * k), c ?? CragC);
        void Edge(int x, int top, int h) => ui.Rect(new Rectangle(cx + x * k, g - top * k, u, h * k), lit); // the side that faces the sun
        void Tower(int x, int w, int h, bool cone)
        {
            B(x, h, w, h + 5); Edge(x, h, h);
            if (cone)
            {
                B(x - 1, h + 1, w + 2, 1);
                for (int i = 0; 2 * i < w + 2; i++) { B(x - 1 + i, h + 2 * (i + 1) + 1, w + 2 - 2 * i, 2); Edge(x - 1 + i, h + 2 * (i + 1) + 1, 2); }
            }
            else
            {
                B(x - 1, h + 1, w + 2, 1);
                for (int i = -1; i <= w; i += 2) B(x + i, h + 2, 1, 1);
            }
        }
        void Window(int x, int y, float dim = 1) { B(x, y, 1, 2, fire * dim); ui.Disc(new Vector2(cx + x * k + u, g - y * k + 2 * u), U(5), Kit.Ember * (0.07f * dim)); }

        B(-29, 10, 58, 15);                                     // curtain wall
        for (int i = -29; i < 29; i += 2) B(i, 11, 1, 1);
        B(-9, 24, 20, 29); Edge(-9, 24, 24);                    // the keep
        for (int i = -9; i < 11; i += 2) B(i, 25, 1, 1);
        Tower(-32, 6, 17, true); Tower(-19, 5, 14, false); Tower(-3, 8, 34, true); Tower(13, 5, 21, true); Tower(25, 6, 16, false);
        B(-13, 5, 3, 5, fire * 0.55f); B(-13, 6, 1, 1); B(-11, 6, 1, 1); // the gate, lit from within
        Window(-6, 19); Window(8, 18, 0.8f); Window(0, 29); Window(2, 22, 0.7f); Window(-30, 12, 0.8f); Window(15, 16); Window(27, 11, 0.7f); Window(-17, 9, 0.6f);
        int px = cx + k, top = g - 52 * k;                      // the flag on the great tower
        ui.Rect(new Rectangle(px, top, u, 8 * k), CragC);
        Flag(px + u, top, U(18), U(10), time * 3.2f, 1, Color.Lerp(Colours.Cloth, CragC, 0.3f), Color.Lerp(Colours.Trim, CragC, 0.3f));
    }

    /// A flag streaming from its pole (`dir` 1 to the right, -1 to the left), swallow-tailed, with a stripe of the trim.
    void Flag(float x, float y, int w, int h, float phase, int dir, Color cloth, Color trim)
    {
        int s = 2 * u;
        for (int col = 0; col < w; col += s)
        {
            float f = col / (float)w, wave = MathF.Sin(col / (float)u * 0.32f - phase) * 1.6f * u * (0.3f + f);
            for (int row = 0; row < h; row += u)
            {
                if (f > 0.7f && MathF.Abs(row + u / 2f - h / 2f) < (f - 0.7f) * h * 0.9f) continue; // the notch of the tail
                var c = row >= h / 4 && row < h / 4 + 2 * u ? trim : Kit.Mul(cloth, 1 - 0.35f * f + 0.12f * MathF.Sin(col / (float)u * 0.32f - phase));
                R(dir > 0 ? x + col : x - col - s, y + row + wave, s, u, c);
            }
        }
    }

    /// Pines along the middle hills, in clumps.
    void Pines()
    {
        var c = Kit.Mul(MidC, 0.8f);
        for (int i = 0; i < 64; i++)
        {
            float wx = (Ease.Hash(i / 4 * 11 + 2) * 1.1f - 0.05f) * ui.W + (Ease.Hash(i * 3 + 1) - 0.5f) * U(70);
            if (MathF.Abs(wx - ui.W * 0.47f) < U(46)) continue; // the hamlet's clearing
            float x = wx - lean * 0.7f, y = MidY(wx) + 2 * u, s = 0.8f + 0.7f * Ease.Hash(i * 7 + 5);
            for (int j = 0; j < 4; j++) R(x - (7 - 2 * j) * u * s / 2, y - (j + 1) * 4 * u * s, (7 - 2 * j) * u * s, 4 * u * s + u, c);
        }
    }

    /// A hamlet in the valley: a chapel and a few houses with their fires lit, smoke going straight up.
    void Hamlet()
    {
        float wx = ui.W * 0.47f, x0 = wx - lean * 0.7f;
        var c = Kit.Mul(MidC, 0.72f);
        var fire = Color.Lerp(Kit.Ember, Flame, 0.4f + 0.3f * MathF.Sin(time * 5.1f));
        void House(float dx, int w, int h, bool lit)
        {
            float x = x0 + dx * u, y = MidY(wx + dx * u) + 2 * u;
            R(x, y - h * u, w * u, h * u + 2 * u, c);
            for (int i = 0; 2 * i < w + 2; i++) R(x - u + i * u, y - (h + i + 1) * u, (w + 2 - 2 * i) * u, u, c);
            if (lit) { R(x + (w / 2 - 1) * u, y - (h - 1) * u, 3 * u, 3 * u, fire); ui.Disc(new Vector2(x + (w / 2 + 0.5f) * u, y - (h - 2.5f) * u), U(9), Kit.Ember * 0.1f); }
        }
        House(-34, 12, 6, true); House(-18, 10, 7, true); House(8, 13, 6, true); House(25, 9, 5, true);
        float cxh = x0 - 4 * u, cy = MidY(wx - 4 * u) + 2 * u;   // the chapel and its spire
        R(cxh, cy - 12 * u, 9 * u, 14 * u, c); R(cxh + 2 * u, cy - 22 * u, 5 * u, 10 * u, c);
        for (int i = 0; i < 3; i++) R(cxh + (2 + i) * u, cy - (24 + 2 * i) * u, (5 - 2 * i) * u, 2 * u, c);
        R(cxh + 4 * u, cy - 31 * u, u, 4 * u, c); R(cxh + 3 * u, cy - 30 * u, 3 * u, u, c);
        R(cxh + 4 * u, cy - 17 * u, u, 3 * u, fire * 0.8f);
        for (int i = 0; i < 7; i++) // smoke from the nearest house
        {
            float t = (time * 0.12f + i / 7f) % 1;
            ui.Disc(new Vector2(x0 + (13 + 8 * t * t) * u, MidY(wx) - (8 + 46 * t) * u), U(1.5f + 4 * t), new Color(150, 110, 120) * (0.16f * (1 - t)));
        }
    }

    // ---------- the company on the march ----------

    /// The company crossing the near ridge from left to right; it comes round again when the last man has gone.
    void Column()
    {
        float span = Math.Max(ui.W + U(160), U(marchLength + 260));
        float head = ui.W * 0.95f + time * U(4.5f);
        for (int i = March.Count - 1; i >= 0; i--)
        {
            var (who, back) = March[i];
            float wx = ((head - U(back)) % span + span) % span - U(80);
            float x = wx - lean;
            if (x < -U(70) || x > ui.W + U(70)) continue;
            float y = NearY(wx) + 2 * u, ph = wx / u * 0.42f + i * 2.1f;
            switch (who)
            {
                case Who.Rider or Who.Standard: Rider(x, y, ph, who == Who.Standard, i); break;
                case Who.Wagon: Wagon(x, y, ph); break;
                default: Man(x, y, ph, who, i); break;
            }
        }
    }

    void Man(float x, float y, float ph, Who who, int i)
    {
        float s = MathF.Sin(ph);
        y -= MathF.Abs(MathF.Cos(ph)) * u;
        var hip = new Vector2(x, y - 10 * u);
        ui.Line(hip, new Vector2(x + s * 4 * u, y), NearC, 2);
        ui.Line(hip, new Vector2(x - s * 4 * u, y), NearC, 2);
        R(x - 3 * u, y - 12 * u, 7 * u, 4 * u, NearC);                       // the skirt of his coat
        R(x - 2 * u, y - 15 * u, 5 * u, 4 * u, NearC);                       // belted waist
        R(x - 3 * u, y - 21 * u, 7 * u, 7 * u, NearC);                       // chest and shoulders
        R(x - u, y - 22 * u, 3 * u, u, NearC);                               // neck
        R(x - 2 * u, y - 26 * u, 5 * u, 4 * u, NearC);                       // head
        R(x - u, y - 27 * u, 3 * u, u, NearC);                               // the crown of his kettle helm
        R(x - 4 * u, y - 25 * u, 9 * u, u, NearC);                           // and its brim
        R(x + s * 4 * u, y - u, 3 * u, u, NearC); R(x - s * 4 * u, y - u, 3 * u, u, NearC); // feet
        if (who is Who.Spear or Who.Banner) ui.Disc(new Vector2(x - 3 * u, y - 16 * u), U(4.5f), NearC); // shield slung behind
        ui.Line(new Vector2(x + u, y - 19 * u), new Vector2(x + 4 * u, y - 12 * u), NearC, 2); // arm
        var hand = new Vector2(x + 4 * u, y - 10 * u);
        float tilt = (Ease.Hash(i * 5 + 3) - 0.5f) * 5 * u + s * u;
        switch (who)
        {
            case Who.Spear:
                var tip = new Vector2(hand.X + tilt, y - (42 + 5 * Ease.Hash(i)) * u);
                ui.Line(hand, tip, NearC, 1);
                R(tip.X - u, tip.Y - 3 * u, 2 * u, 4 * u, NearC);
                break;
            case Who.Torch:
                var top = new Vector2(hand.X + 2 * u, y - 31 * u);
                ui.Line(hand, top, NearC, 1);
                float fl = 0.75f + 0.25f * MathF.Sin(time * 11 + i * 3) * MathF.Sin(time * 6.3f + i);
                ui.Disc(top, U(16) * fl, Kit.Ember * 0.07f); ui.Disc(top, U(8) * fl, Kit.Ember * 0.16f);
                ui.Disc(top - new Vector2(0, u), U(2.6f) * fl, Kit.Ember); ui.Disc(top - new Vector2(0, 2 * u), U(1.5f), Flame);
                break;
            case Who.Bow:
                ui.Line(new Vector2(x - 3 * u, y - 27 * u), new Vector2(x - 7 * u, y - 17 * u), NearC, 1);
                ui.Line(new Vector2(x - 7 * u, y - 17 * u), new Vector2(x - 3 * u, y - 7 * u), NearC, 1);
                R(x - 5 * u, y - 21 * u, 3 * u, 8 * u, NearC);               // quiver
                break;
            case Who.Banner:
                ui.Line(hand, new Vector2(hand.X, y - 56 * u), NearC, 1.5f);
                R(hand.X - u, y - 58 * u, 3 * u, 2 * u, Color.Lerp(Kit.Gilt, NearC, 0.3f));
                Flag(hand.X - u, y - 55 * u, U(22), U(13), time * 4 + i, -1, Color.Lerp(Colours.Cloth, NearC, 0.18f), Color.Lerp(Colours.Trim, NearC, 0.18f));
                break;
        }
    }

    /// A horse at the walk, its four legs a beat apart.
    void Horse(float x, float y, float ph)
    {
        y -= MathF.Abs(MathF.Cos(ph)) * u;
        R(x - 12 * u, y - 23 * u, 24 * u, 9 * u, NearC);
        R(x - 13 * u, y - 22 * u, 2 * u, 6 * u, NearC); R(x + 11 * u, y - 24 * u, 3 * u, 8 * u, NearC);
        ui.Line(new Vector2(x + 9 * u, y - 19 * u), new Vector2(x + 16 * u, y - 32 * u), NearC, 8);
        ui.Line(new Vector2(x + 14 * u, y - 34 * u), new Vector2(x + 24 * u, y - 26 * u), NearC, 5.5f);
        R(x + 13 * u, y - 39 * u, 2 * u, 4 * u, NearC);
        ui.Line(new Vector2(x + 8 * u, y - 23 * u), new Vector2(x + 13 * u, y - 34 * u), Rim * 0.3f, 1); // the mane catches the sun
        R(x - 12 * u, y - 24 * u, 20 * u, u, Rim * 0.22f);
        ui.Line(new Vector2(x - 12 * u, y - 21 * u), new Vector2(x - 17 * u - MathF.Sin(ph * 0.5f) * u, y - 9 * u), NearC, 2);
        for (int l = 0; l < 4; l++)
        {
            float hx = x + (l < 2 ? -10 + l * 4 : 6 + (l - 2) * 4) * u, a = MathF.Sin(ph + l * MathF.PI / 2 * (l % 2 == 0 ? 1 : 3));
            var knee = new Vector2(hx + a * 2 * u, y - 8 * u);
            ui.Line(new Vector2(hx, y - 16 * u), knee, NearC, 3.5f);
            ui.Line(knee, new Vector2(hx + a * 5 * u, y), NearC, 2);
        }
    }

    void Rider(float x, float y, float ph, bool standard, int i)
    {
        Horse(x, y, ph);
        y -= MathF.Abs(MathF.Cos(ph)) * u;
        R(x - 3 * u, y - 31 * u, 6 * u, 9 * u, NearC);                       // body
        R(x - 5 * u, y - 36 * u, 9 * u, 5 * u, NearC);                       // shoulders
        R(x - 4 * u, y - 37 * u, 7 * u, u, NearC);
        R(x - 2 * u, y - 42 * u, 5 * u, 5 * u, NearC);                       // helm
        R(x - u, y - 43 * u, 3 * u, u, NearC); R(x, y - 46 * u, u, 3 * u, NearC); // its crown and crest
        for (int j = 0; j < 7; j++) R(x - (6 + j * 0.5f) * u, y - (34 - j * 2) * u, 3 * u, 2 * u, NearC); // cloak falling behind
        ui.Line(new Vector2(x + 2 * u, y - 34 * u), new Vector2(x + 5 * u, y - 25 * u), NearC, 2.5f); // arm to the shaft
        R(x - 5 * u, y - 37 * u, u, 9 * u, Rim * 0.28f);                     // the sun on his back
        ui.Line(new Vector2(x, y - 24 * u), new Vector2(x + 3 * u, y - 13 * u), NearC, 2.5f);
        var hand = new Vector2(x + 4 * u, y - 25 * u);
        if (standard)
        {
            ui.Line(hand, new Vector2(hand.X, y - 82 * u), NearC, 1.5f);
            R(hand.X - u, y - 84 * u, 3 * u, 3 * u, Color.Lerp(Kit.Gilt, NearC, 0.2f));
            Flag(hand.X - u, y - 80 * u, U(34), U(20), time * 3.6f, -1, Color.Lerp(Colours.Cloth, NearC, 0.1f), Color.Lerp(Colours.Trim, NearC, 0.1f));
        }
        else
        {
            var tip = new Vector2(hand.X + (Ease.Hash(i * 9) - 0.3f) * 5 * u, y - 70 * u);
            ui.Line(hand, tip, NearC, 1);
            Flag(tip.X, tip.Y + 2 * u, U(11), U(5), time * 5 + i, -1, Color.Lerp(Colours.Trim, NearC, 0.25f), Color.Lerp(Colours.Cloth, NearC, 0.25f));
        }
    }

    /// The baggage: a tilt wagon behind one horse.
    void Wagon(float x, float y, float ph)
    {
        Horse(x + 36 * u, y, ph);
        ui.Line(new Vector2(x + 13 * u, y - 12 * u), new Vector2(x + 26 * u, y - 16 * u), NearC, 1.5f);
        R(x - 17 * u, y - 16 * u, 31 * u, 8 * u, NearC);
        R(x - 16 * u, y - 27 * u, 29 * u, 11 * u, NearC); R(x - 14 * u, y - 29 * u, 25 * u, 2 * u, NearC);
        ui.Disc(new Vector2(x - 9 * u, y - 6 * u), U(6), NearC); ui.Disc(new Vector2(x + 7 * u, y - 6 * u), U(6), NearC);
    }

    /// Grass along a crest, in tufts, leaning in the wind.
    void Grass(Func<float, float> yAt, float depth, Color c, float size, int step)
    {
        for (int i = 0; i * step * u < ui.W + 40 * u; i++)
        {
            float wx = i * step * u + Ease.Hash(i) * 4 * u, x = wx - lean * depth;
            float h = (2 + 6 * Ease.Hash(i * 3 + 7) * Ease.Hash(i / 6 + 90)) * u * size;
            float bend = (1.2f + MathF.Sin(time * 1.4f + wx / Q(60))) * h * 0.3f;
            ui.Line(new Vector2(x, yAt(wx) + u), new Vector2(x + bend, yAt(wx) - h), c, size > 1.5f ? 1.5f : 1);
        }
    }

    /// Sparks rising off the torches into the dark.
    void Embers()
    {
        for (int i = 0; i < 20; i++)
        {
            float life = 5 + i % 4, t = ((time + i * 0.71f) % life) / life;
            float x = ui.W * (0.08f + 0.9f * ((i * 0.618f) % 1)) + MathF.Sin(t * 6 + i) * U(12);
            float y = ui.H * (0.93f - 0.34f * t);
            R(x, y, Math.Max(1, U(1.5f)), Math.Max(1, U(1.5f)), Color.Lerp(Flame, Kit.Ember, t) * ((1 - t) * 0.8f));
        }
    }
}
