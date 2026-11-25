using Microsoft.Xna.Framework;

namespace Ironfield;

/// TitleScreen: the painted dynamic scene. Driven by the latest save's time of day (dawn, day, dusk, night),
/// biome/backdrop (plains, forest, hills, highlands, steppe, badlands/desert, coast, marsh, winter), weather (rain/snow),
/// and the player company's actual troop count, composition (cavalry, spears, archers, mages, wagons) and heraldry.
sealed partial class TitleScreen
{
    // 24-hour keyframe sky stops (zenith -> upper -> mid -> lower -> horizon -> glow)
    static readonly (float At, Color C)[] SkyNight =
    {
        (0f, new Color(4, 6, 18)), (0.35f, new Color(10, 14, 34)), (0.65f, new Color(18, 24, 52)),
        (0.85f, new Color(26, 36, 68)), (1f, new Color(36, 50, 84)),
    };
    static readonly (float At, Color C)[] SkyDawn =
    {
        (0f, new Color(18, 24, 56)), (0.3f, new Color(48, 46, 94)), (0.58f, new Color(118, 72, 108)),
        (0.78f, new Color(214, 122, 104)), (0.92f, new Color(248, 178, 124)), (1f, new Color(255, 224, 168)),
    };
    static readonly (float At, Color C)[] SkyDay =
    {
        (0f, new Color(40, 88, 152)), (0.32f, new Color(68, 124, 186)), (0.62f, new Color(114, 162, 208)),
        (0.84f, new Color(168, 198, 220)), (1f, new Color(216, 224, 222)),
    };
    static readonly (float At, Color C)[] SkyDusk =
    {
        (0f, new Color(9, 11, 28)), (0.3f, new Color(28, 25, 64)), (0.55f, new Color(84, 44, 90)),
        (0.76f, new Color(178, 78, 84)), (0.9f, new Color(238, 138, 78)), (1f, new Color(255, 198, 116)),
    };

    static readonly Color Flame = new(255, 206, 128);

    // Per-frame resolved dynamic scene colors & state
    Color farC, cragC, midC, nearC, frontC, rimC, hazeC;
    float sceneHour = 19.2f, sceneRain, starAlpha, dayAmount;
    bool sceneWinter, torchesLit;
    string sceneBiome = "Plains";

    enum Who { Spear, Torch, Bow, Banner, Rider, Standard, Wagon, Mage, CaptainFoot }

    List<(Who Who, float Back)> march = new();
    float marchLength;
    string marchKey = "";

    static readonly string? envHour = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_HOUR");
    static readonly string? envBiome = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_BIOME");
    static readonly string? envMen = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_MEN");
    static readonly string? envWinter = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_WINTER");
    static readonly string? envRain = Environment.GetEnvironmentVariable("IRONFIELD_TITLE_RAIN");

    float lean;
    int u = 1;

    Heraldry Colours => Style.Sides[Math.Clamp(Saves.Latest()?.Meta?.Palette ?? 0, 0, Style.Sides.Length - 1)];

    static string TimeLabel(float h) => h switch
    {
        >= 5f and < 8.5f => "DAWN",
        >= 8.5f and < 17f => "DAY",
        >= 17f and < 20.8f => "DUSK",
        _ => "NIGHT",
    };

    void ResolveSceneState(SaveMeta? save)
    {
        sceneHour = save?.Hour ?? 19.2f;
        sceneBiome = string.IsNullOrWhiteSpace(save?.Biome) ? "Plains" : save.Biome;
        sceneWinter = save?.Winter ?? false;
        sceneRain = save?.Rain ?? 0f;

        if (float.TryParse(envHour, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float eh))
            sceneHour = ((eh % 24f) + 24f) % 24f;
        if (!string.IsNullOrWhiteSpace(envBiome)) sceneBiome = envBiome;
        if (envWinter == "1") sceneWinter = true;
        if (float.TryParse(envRain, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float er))
            sceneRain = Math.Clamp(er, 0f, 1f);

        // Compute smooth day/night/dawn/dusk weights
        float h = sceneHour;
        dayAmount = h is >= 8.5f and <= 16.5f ? 1f
            : h is >= 5.5f and < 8.5f ? (h - 5.5f) / 3f
            : h is > 16.5f and <= 19.5f ? 1f - (h - 16.5f) / 3f : 0f;
        starAlpha = h is >= 21f or <= 4.5f ? 1f
            : h is > 4.5f and < 7.5f ? 1f - (h - 4.5f) / 3f
            : h is > 18f and < 21f ? (h - 18f) / 3f : 0f;
        if (sceneRain > 0.25f) starAlpha *= 1f - MathF.Min(0.85f, sceneRain);
        torchesLit = h < 7.2f || h > 17.5f;

        // Layer palette by time of day
        Color LerpTod(Color night, Color dawn, Color day, Color dusk)
        {
            if (h < 4.5f || h >= 21.5f) return night;
            if (h < 6.5f) return Color.Lerp(night, dawn, (h - 4.5f) / 2f);
            if (h < 9f) return Color.Lerp(dawn, day, (h - 6.5f) / 2.5f);
            if (h < 16.5f) return day;
            if (h < 19f) return Color.Lerp(day, dusk, (h - 16.5f) / 2.5f);
            return Color.Lerp(dusk, night, (h - 19f) / 2.5f);
        }

        farC = LerpTod(new(24, 30, 52), new(118, 78, 108), new(96, 126, 158), new(122, 66, 94));
        cragC = LerpTod(new(18, 22, 40), new(74, 52, 82), new(68, 94, 114), new(74, 43, 73));
        midC = LerpTod(new(14, 17, 30), new(46, 36, 58), new(46, 72, 62), new(43, 29, 51));
        nearC = LerpTod(new(10, 12, 22), new(30, 24, 42), new(28, 44, 38), new(29, 21, 39));
        frontC = LerpTod(new(4, 5, 11), new(10, 8, 16), new(12, 18, 18), new(7, 5, 11));
        rimC = LerpTod(new(136, 162, 216), new(255, 198, 146), new(226, 236, 214), new(255, 172, 104));
        hazeC = LerpTod(new(38, 52, 88), new(244, 168, 128), new(186, 210, 222), new(238, 134, 92));

        // Biome tinting
        if (sceneBiome is "Badlands" or "Desert")
        {
            farC = Color.Lerp(farC, new Color(156, 102, 74), 0.35f + 0.25f * dayAmount);
            cragC = Color.Lerp(cragC, new Color(118, 74, 52), 0.35f + 0.25f * dayAmount);
            midC = Color.Lerp(midC, new Color(88, 56, 38), 0.35f + 0.2f * dayAmount);
            nearC = Color.Lerp(nearC, new Color(56, 36, 26), 0.3f);
            hazeC = Color.Lerp(hazeC, new Color(238, 176, 118), 0.4f);
        }
        else if (sceneBiome == "Steppe")
        {
            midC = Color.Lerp(midC, new Color(82, 78, 48), 0.35f * dayAmount);
            nearC = Color.Lerp(nearC, new Color(54, 52, 34), 0.3f * dayAmount);
        }
        else if (sceneBiome == "Marsh")
        {
            farC = Color.Lerp(farC, new Color(56, 74, 72), 0.35f);
            midC = Color.Lerp(midC, new Color(34, 48, 44), 0.4f);
            hazeC = Color.Lerp(hazeC, new Color(144, 172, 166), 0.45f);
        }
        if (sceneWinter)
        {
            farC = Color.Lerp(farC, new Color(148, 164, 188), 0.28f + 0.25f * dayAmount);
            cragC = Color.Lerp(cragC, new Color(96, 110, 134), 0.22f + 0.2f * dayAmount);
            rimC = Color.Lerp(rimC, new Color(232, 242, 255), 0.65f);
            hazeC = Color.Lerp(hazeC, new Color(192, 210, 232), 0.35f);
        }
        if (sceneRain > 0.2f)
        {
            float rk = Math.Clamp(sceneRain * 0.65f, 0f, 0.55f);
            farC = Color.Lerp(farC, new Color(48, 56, 68), rk);
            cragC = Color.Lerp(cragC, new Color(36, 42, 52), rk);
            hazeC = Color.Lerp(hazeC, new Color(108, 122, 138), rk);
        }

        EnsureMarch(save);
    }

    void EnsureMarch(SaveMeta? save)
    {
        int men = save?.Men ?? 38;
        int inf = save != null && (save.Infantry + save.Archers + save.Cavalry + save.Mages) > 0 ? save.Infantry : (int)(men * 0.58f);
        int arch = save != null && (save.Infantry + save.Archers + save.Cavalry + save.Mages) > 0 ? save.Archers : (int)(men * 0.22f);
        int cav = save != null && (save.Infantry + save.Archers + save.Cavalry + save.Mages) > 0 ? save.Cavalry : (int)(men * 0.16f);
        int mages = save?.Mages ?? 0;
        bool mounted = save?.Mounted ?? true;

        if (int.TryParse(envMen, out int em) && em >= 0)
        {
            men = em;
            inf = (int)(men * 0.52f); arch = (int)(men * 0.24f); cav = (int)(men * 0.18f); mages = men >= 25 ? Math.Min(4, men / 25) : 0;
            mounted = men >= 8;
        }

        string key = $"{men}|{inf}|{arch}|{cav}|{mages}|{mounted}|{torchesLit}";
        if (key == marchKey && march.Count > 0) return;
        marchKey = key;

        var l = new List<(Who, float)>();
        float x = 0;
        void Add(Who w, float gap) { l.Add((w, x)); x += gap; }

        // Scale visual figures gracefully: 1:1 for small bands (<= 16), compressed for big hosts (up to ~62 figures)
        int Scale(int count, int cap) => count <= 0 ? 0 : Math.Min(cap, count <= 12 ? count : 12 + (count - 12) / 3);
        int visCav = Scale(cav, 10);
        int visInf = Scale(inf, 28);
        int visArch = Scale(arch, 14);
        int visMage = Math.Min(5, mages);

        // Captain leads the column
        if (mounted)
        {
            Add(Who.Rider, 44);
            if (men >= 10) Add(Who.Standard, 46);
            for (int i = 0; i < Math.Max(0, visCav - (men >= 10 ? 1 : 0)); i++)
                Add(Who.Rider, 42 + (i % 2) * 4);
        }
        else
        {
            Add(Who.CaptainFoot, 18);
            if (men >= 8) Add(Who.Banner, 18);
            for (int i = 0; i < visCav; i++) Add(Who.Rider, 44);
        }

        if (visCav > 0) x += 10;

        // Infantry ranks with company banners & night/dusk torches
        for (int i = 0; i < visInf; i++)
        {
            if (i > 0 && i % 10 == 0) { x += 10; Add(Who.Banner, 17); }
            else if (i == 0 && men >= 18) Add(Who.Banner, 17);
            bool torch = torchesLit && (i % 5 == 3 || (visInf < 5 && i == visInf - 1));
            Add(torch ? Who.Torch : Who.Spear, 12 + (i % 2) * 3);
        }

        // Mages walking with glowing staves
        if (visMage > 0)
        {
            x += 12;
            for (int i = 0; i < visMage; i++) Add(Who.Mage, 16);
        }

        // Archers
        if (visArch > 0)
        {
            x += 12;
            for (int i = 0; i < visArch; i++) Add(Who.Bow, 13);
        }

        // Supply wagons & rear guard scaled to company size
        int wagons = men >= 15 ? Math.Clamp(men / 30 + (men >= 15 ? 1 : 0), 1, 3) : 0;
        for (int w = 0; w < wagons; w++)
        {
            x += 34; Add(Who.Wagon, 40);
            int rear = men >= 25 ? 3 : 1;
            for (int i = 0; i < rear; i++) Add(torchesLit && i == 0 ? Who.Torch : Who.Spear, 13);
        }

        march = l;
        marchLength = MathF.Max(140, x);
    }

    float Q(float v) => v * ui.H / 540f;
    static float Tri(float a) { a -= MathF.Floor(a); return 1 - MathF.Abs(2 * a - 1); }
    void R(float x, float y, float w, float h, Color c) => ui.Rect(new Rectangle((int)x, (int)y, Math.Max(1, (int)w), Math.Max(1, (int)h)), c);

    /// `pan` slides the eye to the right by that many pixels on the near ridge (the loading screen drifts along the march).
    void Scene(float pan = 0)
    {
        u = Math.Max(1, U(1));
        lean = Ui.Instant ? 0 : (ms.X / (float)Math.Max(1, ui.W) - 0.5f) * Q(18) + pan;
        ResolveSceneState(Saves.Latest()?.Meta);
        PaintSky();
        if (starAlpha > 0.04f)
        {
            ui.Stars(new Rectangle(0, 0, ui.W, (int)(ui.H * 0.48f)), time, (int)(125 * starAlpha));
            if (starAlpha > 0.5f) ShootingStar();
        }
        CelestialBody();
        Clouds();
        Layer(FarY, 0.2f, farC, rimC * (sceneWinter ? 0.55f : 0.30f));
        if (sceneBiome == "Coast") SeaExpanse();
        Haze(0.665f, 0.75f, hazeC, sceneBiome == "Marsh" ? 0.42f : 0.26f);
        if (dayAmount > 0.15f || sceneHour is >= 5.5f and <= 20f) Flock();
        Layer(CragY, 0.4f, cragC, rimC * (sceneWinter ? 0.48f : 0.22f));
        Castle();
        Haze(0.735f, 0.81f, hazeC, sceneBiome == "Marsh" ? 0.38f : 0.22f);
        Layer(MidY, 0.7f, midC, rimC * (sceneWinter ? 0.42f : 0.16f));
        BiomeFlora();
        Hamlet();
        Haze(0.8f, 0.9f, hazeC, sceneBiome == "Marsh" ? 0.55f : 0.38f);
        Layer(NearY, 1f, nearC, rimC * (sceneWinter ? 0.65f : 0.45f));
        Column();
        Grass(NearY, 1f, nearC, 1, 5);
        Layer(FrontY, 1.4f, frontC, rimC * 0.14f);
        Foreground();
        Grass(FrontY, 1.4f, frontC, 2.6f, 4);
        if (sceneWinter) Snowfall();
        else if (sceneRain > 0.2f) Rainfall();
        else if (torchesLit) Embers();
        // Keep left side legible over bright day/snow skies without muddying the right vista
        float leftDim = 0.50f + 0.18f * dayAmount + (sceneWinter ? 0.08f : 0f);
        ui.HGrad(new Rectangle(0, 0, (int)(ui.W * 0.58f), ui.H), Color.Black * leftDim, Color.Black * 0f, 6);
        ui.Vignette(0.65f);
    }

    // ---------- the sky ----------

    static Color SampleStops((float At, Color C)[] stops, float t)
    {
        int k = 0;
        while (k < stops.Length - 2 && t > stops[k + 1].At) k++;
        return Color.Lerp(stops[k].C, stops[k + 1].C, Math.Clamp((t - stops[k].At) / (stops[k + 1].At - stops[k].At), 0, 1));
    }

    Color SkyAt(float t)
    {
        float h = sceneHour;
        Color c;
        if (h < 4.5f || h >= 21.5f) c = SampleStops(SkyNight, t);
        else if (h < 6.5f) c = Color.Lerp(SampleStops(SkyNight, t), SampleStops(SkyDawn, t), (h - 4.5f) / 2f);
        else if (h < 9f) c = Color.Lerp(SampleStops(SkyDawn, t), SampleStops(SkyDay, t), (h - 6.5f) / 2.5f);
        else if (h < 16.5f) c = SampleStops(SkyDay, t);
        else if (h < 19f) c = Color.Lerp(SampleStops(SkyDay, t), SampleStops(SkyDusk, t), (h - 16.5f) / 2.5f);
        else c = Color.Lerp(SampleStops(SkyDusk, t), SampleStops(SkyNight, t), (h - 19f) / 2.5f);

        if (sceneBiome is "Badlands" or "Desert" && dayAmount > 0.2f)
            c = Color.Lerp(c, new Color(224, 188, 136), t * 0.28f * dayAmount);
        if (sceneRain > 0.2f)
            c = Color.Lerp(c, new Color(46, 56, 70), Math.Clamp(sceneRain * 0.55f, 0f, 0.5f));
        return c;
    }

    void PaintSky()
    {
        int s = 2 * u, h = (int)(ui.H * 0.74f);
        for (int y = 0; y < h; y += s)
            ui.Rect(new Rectangle(0, y, ui.W, s), SkyAt(y / (float)h));
    }

    /// Sun by day/dawn/dusk, silver moon by night — with smoothly feathered glow (no harsh bullseye banding).
    void CelestialBody()
    {
        float h = sceneHour;
        bool isMoon = h < 5.2f || h >= 20.5f;
        if (isMoon)
        {
            float nt = h >= 20.5f ? (h - 20.5f) / 8.7f : (h + 3.5f) / 8.7f;
            var c = new Vector2(ui.W * (0.56f + 0.24f * nt) - lean * 0.08f, ui.H * (0.28f - 0.10f * MathF.Sin(nt * MathF.PI)));
            for (int i = 10; i >= 1; i--) ui.Disc(c, Q(24 + i * 10), new Color(148, 178, 232) * (0.014f * (11 - i) / 10f));
            float r = Q(22);
            for (float y = -r; y <= r; y += u)
            {
                float w = MathF.Sqrt(MathF.Max(0, r * r - y * y));
                R(c.X - w, c.Y + y, 2 * w, u, Color.Lerp(new Color(242, 246, 255), new Color(196, 212, 238), (y + r) / (2 * r)));
            }
            // Subtle crescent shadow
            ui.Disc(c + new Vector2(Q(9), -Q(4)), Q(19), SkyAt(Math.Clamp(c.Y / (ui.H * 0.74f), 0f, 1f)) * 0.88f);
            return;
        }

        // Sun arc across dawn (5.2) -> noon (12.5) -> dusk (20.5)
        float st = Math.Clamp((h - 5.2f) / 15.3f, 0f, 1f);
        float elev = MathF.Sin(st * MathF.PI); // 0 at horizon, 1 at noon
        var sc = new Vector2(ui.W * (0.54f + 0.22f * st) - lean * 0.1f, ui.H * (0.655f - 0.44f * elev));
        float rSun = Q(60 - 22 * elev);
        Color glowC = Color.Lerp(new Color(255, 154, 86), new Color(255, 244, 206), elev);
        // Smooth radial feathering (small alpha per step, non-linear radius spread -> no bullseye rings)
        for (int i = 14; i >= 1; i--)
        {
            float f = i / 14f;
            float rad = rSun + Q(170) * f * f;
            float a = (1f - f) * (1f - f) * (0.045f - 0.015f * elev);
            ui.Disc(sc, rad, glowC * a);
        }
        Color topC = Color.Lerp(new Color(255, 246, 206), new Color(255, 254, 240), elev);
        Color botC = Color.Lerp(new Color(255, 196, 116), new Color(255, 244, 202), elev);
        for (float y = -rSun; y <= rSun; y += u)
        {
            float w = MathF.Sqrt(MathF.Max(0, rSun * rSun - y * y));
            R(sc.X - w, sc.Y + y, 2 * w, u, Color.Lerp(topC, botC, (y + rSun) / (2 * rSun)));
        }
    }

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

    void Clouds()
    {
        float span = ui.W + Q(520);
        int count = sceneRain > 0.2f ? 15 : 11;
        for (int i = 0; i < count; i++)
        {
            float h = i / (float)(count - 1);
            float x = (Ease.Hash(i * 7 + 3) * span + time * Q(2.5f + 4 * Ease.Hash(i + 40))) % span - Q(260);
            float y = ui.H * (0.12f + 0.48f * h + 0.03f * (Ease.Hash(i * 5 + 1) - 0.5f));
            Color top, under;
            if (dayAmount > 0.5f)
            {
                top = Color.Lerp(new Color(242, 246, 252), new Color(188, 202, 218), h * 0.5f);
                under = Color.Lerp(new Color(186, 204, 224), new Color(156, 174, 198), h);
            }
            else if (starAlpha > 0.6f)
            {
                top = new Color(18, 24, 46);
                under = new Color(36, 48, 78);
            }
            else
            {
                top = Color.Lerp(new Color(52, 40, 88), new Color(142, 66, 86), h);
                under = Color.Lerp(new Color(112, 70, 122), new Color(255, 178, 112), h);
            }
            if (sceneRain > 0.2f) { top = Color.Lerp(top, new Color(48, 54, 66), 0.6f); under = Color.Lerp(under, new Color(36, 42, 52), 0.6f); }
            Streak(x - lean * 0.1f, y, Q(150 + 260 * Ease.Hash(i * 3 + 9)), 3 + (int)(4 * Ease.Hash(i + 20) * (1 - 0.5f * h)), i, top, under, 0.42f + 0.42f * h);
        }
    }

    void ShootingStar()
    {
        if (Ui.Instant) return;
        float cycle = time % 19f;
        if (cycle > 0.7f) return;
        int n = (int)(time / 19f);
        float t = cycle / 0.7f;
        var a = new Vector2(ui.W * (0.2f + 0.5f * Ease.Hash(n * 13 + 5)), ui.H * (0.05f + 0.15f * Ease.Hash(n * 29 + 2)));
        var p = a + new Vector2(Q(150), Q(54)) * t;
        ui.Line(p - new Vector2(Q(34), Q(12)) * MathF.Min(1, t * 3), p, Color.White * (0.7f * (1 - t)), 1);
    }

    void Flock()
    {
        float span = ui.W + U(300);
        float fx = span - (time * U(9) + span * 0.55f) % span - U(150), fy = ui.H * 0.34f + MathF.Sin(time * 0.3f) * U(6);
        var c = Color.Lerp(new Color(34, 22, 40), new Color(46, 56, 68), dayAmount);
        for (int i = 0; i < 9; i++)
        {
            int rank = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1;
            var at = new Vector2(fx + rank * U(11), fy + side * rank * U(5) + MathF.Sin(time * 1.1f + i) * u);
            float flap = MathF.Sin(time * 7 + i * 1.9f) * U(2.5f);
            ui.Line(at, at + new Vector2(-U(4), -flap), c, 1);
            ui.Line(at, at + new Vector2(U(4), -flap), c, 1);
        }
    }

    // ---------- the land (biome-shaped) ----------

    float FarY(float wx)
    {
        float t = wx / Q(100);
        if (sceneBiome is "Badlands" or "Desert")
        {
            // Stepped flat-topped desert mesas
            float m = MathF.Floor((0.55f * Tri(t * 0.19f + 0.4f) + 0.3f * Tri(t * 0.47f + 0.1f)) * 4.5f) / 4.5f;
            return ui.H * 0.70f - Q(78) * m;
        }
        float amp = sceneBiome == "Highlands" ? 132 : sceneBiome == "Hills" ? 104 : sceneBiome is "Steppe" or "Marsh" ? 46 : sceneBiome == "Coast" ? 64 : 88;
        return ui.H * 0.70f - Q(amp) * (0.58f * Tri(t * 0.21f + 0.68f) + 0.24f * Tri(t * 0.53f + 0.35f) + 0.12f * Tri(t * 1.37f + 0.2f) + 0.06f * Tri(t * 3.3f));
    }

    float CastleX => ui.W * 0.835f;

    float CragY(float wx)
    {
        float t = wx / Q(100), d = MathF.Abs(wx - CastleX) / u;
        float top = d < 62 ? 1 : d > 170 ? 0 : 0.5f + 0.5f * MathF.Cos(MathF.PI * (d - 62) / 108);
        float rough = d < 62 ? 0 : 5 * Tri(t * 4.3f) * top;
        float cragH = sceneBiome is "Steppe" or "Marsh" ? 44 : 72;
        return ui.H * 0.74f - Q(12) * (0.6f * MathF.Sin(t * 1.1f + 0.4f) + 0.4f * MathF.Sin(t * 2.7f + 1.7f)) - u * (cragH * top + rough);
    }

    float MidY(float wx)
    {
        float t = wx / Q(100);
        float amp = sceneBiome is "Steppe" or "Marsh" ? 7 : 16;
        return ui.H * 0.805f - Q(amp) * (0.55f * MathF.Sin(t * 1.3f + 3.1f) + 0.3f * MathF.Sin(t * 2.9f + 4.8f) + 0.15f * MathF.Sin(t * 6.1f));
    }

    float NearY(float wx)
    {
        float t = wx / Q(100), rise = Math.Clamp(wx / Math.Max(1, ui.W), 0, 1);
        return ui.H * 0.915f - Q(9) * (0.6f * MathF.Sin(t * 0.9f + 5.7f) + 0.4f * MathF.Sin(t * 2.3f + 1f)) - Q(18) * rise * rise * (3 - 2 * rise);
    }

    float FrontY(float wx)
    {
        float t = wx / Q(100);
        return ui.H * 0.962f - Q(9) * (0.6f * MathF.Sin(t * 1.7f + 2.2f) + 0.4f * MathF.Sin(t * 3.9f + 0.6f)) + Q(14) * Math.Clamp(wx / Math.Max(1, ui.W) - 0.25f, 0, 1);
    }

    /// Shimmering coastal sea expanse + distant sailing cog when the save is on the Coast.
    void SeaExpanse()
    {
        int y0 = (int)(ui.H * 0.695f), y1 = (int)(ui.H * 0.78f);
        Color seaTop = Color.Lerp(farC, new Color(28, 62, 102), 0.55f);
        Color seaBot = Color.Lerp(cragC, new Color(18, 42, 74), 0.55f);
        ui.VGrad(new Rectangle(0, y0, ui.W, y1 - y0), seaTop, seaBot, 2);
        // Animated water glints
        for (int i = 0; i < 48; i++)
        {
            float gx = ui.W * (0.35f + 0.58f * Ease.Hash(i * 19 + 3)) + MathF.Sin(time * 1.8f + i) * U(6) - lean * 0.3f;
            float gy = y0 + (y1 - y0) * Ease.Hash(i * 11 + 7);
            float gw = U(6 + 14 * Ease.Hash(i * 5 + 2));
            R(gx, gy, gw, u, rimC * (0.28f + 0.16f * MathF.Sin(time * 3.2f + i * 2.1f)));
        }
        // Distant sailing cog silhouette
        float sx = (ui.W * 0.58f + time * U(3.5f)) % (ui.W * 0.85f) + ui.W * 0.15f - lean * 0.32f;
        float sy = y0 + (y1 - y0) * 0.45f;
        R(sx - 9 * u, sy, 18 * u, 3 * u, cragC);
        R(sx - 7 * u, sy + 3 * u, 14 * u, 2 * u, cragC);
        R(sx, sy - 16 * u, u, 16 * u, cragC);
        R(sx - 5 * u, sy - 14 * u, 11 * u, 9 * u, Color.Lerp(cragC, rimC, 0.25f));
    }

    void Foreground()
    {
        for (int i = 0; i < 9; i++)
        {
            float wx = ui.W * (0.06f + 0.93f * Ease.Hash(i * 13 + 4)), x = wx - lean * 1.4f, y = FrontY(wx) + 2 * u, s = 5 + 9 * Ease.Hash(i * 5 + 2);
            R(x - s * u, y - s * 0.5f * u, 2 * s * u, s * u, frontC); R(x - s * 0.6f * u, y - s * 0.8f * u, s * 1.3f * u, s * 0.4f * u, frontC);
            R(x - s * 0.6f * u, y - s * 0.8f * u - u, s * 0.9f * u, u, rimC * 0.16f);
        }
        float gx = ui.W * 0.41f, px = gx - lean * 1.4f, gy = FrontY(gx) + 2 * u;
        ui.Line(new Vector2(px, gy), new Vector2(px + 7 * u, gy - 62 * u), frontC, 2);
        ui.Line(new Vector2(px + 26 * u, gy), new Vector2(px + 18 * u, gy - 50 * u), frontC, 2);
        R(px + 6 * u, gy - 68 * u, 3 * u, 7 * u, frontC);
        R(px + 14 * u, gy - 55 * u, 9 * u, 6 * u, frontC); R(px + 12 * u, gy - 50 * u, 13 * u, 2 * u, frontC);
        Flag(px + 6 * u, gy - 58 * u, 16 * u, 7 * u, time * 2.6f, -1, Color.Lerp(Colours.Cloth, frontC, 0.45f), Color.Lerp(Colours.Trim, frontC, 0.45f));
    }

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

    void Haze(float from, float to, Color c, float a)
    {
        int y0 = (int)(ui.H * from), y1 = (int)(ui.H * to);
        ui.VGrad(new Rectangle(0, y0, ui.W, y1 - y0), c * 0f, c * a, 2);
        ui.Rect(new Rectangle(0, y1, ui.W, ui.H - y1), c * a);
    }

    void Castle()
    {
        int cx = (int)(CastleX - lean * 0.4f), g = (int)CragY(CastleX) / u * u, k = 2 * u;
        var lit = Color.Lerp(cragC, rimC, 0.42f);
        var fire = Color.Lerp(Kit.Ember, Flame, 0.5f + 0.5f * MathF.Sin(time * 7.3f) * MathF.Sin(time * 3.1f));
        bool windowsLit = torchesLit;
        bool desert = sceneBiome is "Badlands" or "Desert";
        void B(int x, int top, int w, int h, Color? c = null) => ui.Rect(new Rectangle(cx + x * k, g - top * k, w * k, h * k), c ?? cragC);
        void Edge(int x, int top, int h) => ui.Rect(new Rectangle(cx + x * k, g - top * k, u, h * k), lit);
        void Tower(int x, int w, int h, bool roof)
        {
            B(x, h, w, h + 5); Edge(x, h, h);
            if (roof && desert)
            {
                // Rounded southern dome
                B(x, h + 2, w, 2); B(x + 1, h + 4, Math.Max(1, w - 2), 2); B(x + w / 2, h + 7, 1, 3);
            }
            else if (roof)
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
        void Window(int x, int y, float dim = 1)
        {
            if (!windowsLit) { B(x, y, 1, 2, Kit.Mul(cragC, 0.55f)); return; }
            B(x, y, 1, 2, fire * dim); ui.Disc(new Vector2(cx + x * k + u, g - y * k + 2 * u), U(5), Kit.Ember * (0.07f * dim));
        }

        B(-29, 10, 58, 15);
        for (int i = -29; i < 29; i += 2) B(i, 11, 1, 1);
        B(-9, 24, 20, 29); Edge(-9, 24, 24);
        for (int i = -9; i < 11; i += 2) B(i, 25, 1, 1);
        Tower(-32, 6, 17, true); Tower(-19, 5, 14, false); Tower(-3, 8, 34, true); Tower(13, 5, 21, true); Tower(25, 6, 16, false);
        B(-13, 5, 3, 5, windowsLit ? fire * 0.55f : Kit.Mul(cragC, 0.5f)); B(-13, 6, 1, 1); B(-11, 6, 1, 1);
        Window(-6, 19); Window(8, 18, 0.8f); Window(0, 29); Window(2, 22, 0.7f); Window(-30, 12, 0.8f); Window(15, 16); Window(27, 11, 0.7f); Window(-17, 9, 0.6f);
        int px = cx + k, top = g - (desert ? 46 : 52) * k;
        ui.Rect(new Rectangle(px, top, u, 8 * k), cragC);
        Flag(px + u, top, U(18), U(10), time * 3.2f, 1, Color.Lerp(Colours.Cloth, cragC, 0.25f), Color.Lerp(Colours.Trim, cragC, 0.25f));
    }

    void Flag(float x, float y, int w, int h, float phase, int dir, Color cloth, Color trim)
    {
        int s = 2 * u;
        for (int col = 0; col < w; col += s)
        {
            float f = col / (float)w, wave = MathF.Sin(col / (float)u * 0.32f - phase) * 1.6f * u * (0.3f + f);
            for (int row = 0; row < h; row += u)
            {
                if (f > 0.7f && MathF.Abs(row + u / 2f - h / 2f) < (f - 0.7f) * h * 0.9f) continue;
                var c = row >= h / 4 && row < h / 4 + 2 * u ? trim : Kit.Mul(cloth, 1 - 0.35f * f + 0.12f * MathF.Sin(col / (float)u * 0.32f - phase));
                R(dir > 0 ? x + col : x - col - s, y + row + wave, s, u, c);
            }
        }
    }

    void BiomeFlora()
    {
        var c = Kit.Mul(midC, 0.82f);
        if (sceneBiome is "Badlands" or "Desert")
        {
            // Sparse desert palms & scrub
            for (int i = 0; i < 20; i++)
            {
                float wx = (Ease.Hash(i * 11 + 5) * 1.05f - 0.02f) * ui.W;
                if (MathF.Abs(wx - ui.W * 0.47f) < U(48)) continue;
                float x = wx - lean * 0.7f, y = MidY(wx) + 2 * u, h = (10 + 9 * Ease.Hash(i * 7 + 3)) * u;
                ui.Line(new Vector2(x, y), new Vector2(x + 2 * u, y - h), c, 1.5f);
                for (int f = -2; f <= 2; f++)
                    ui.Line(new Vector2(x + 2 * u, y - h), new Vector2(x + (2 + f * 3.5f) * u, y - h + MathF.Abs(f) * 2 * u - u), c, 1);
            }
            return;
        }
        int count = sceneBiome == "Forest" ? 115 : sceneBiome == "Steppe" ? 18 : sceneBiome == "Marsh" ? 32 : 64;
        for (int i = 0; i < count; i++)
        {
            float wx = (Ease.Hash(i / 4 * 11 + 2) * 1.1f - 0.05f) * ui.W + (Ease.Hash(i * 3 + 1) - 0.5f) * U(70);
            if (MathF.Abs(wx - ui.W * 0.47f) < U(46)) continue;
            float x = wx - lean * 0.7f, y = MidY(wx) + 2 * u, s = (sceneBiome == "Forest" ? 1.05f : 0.8f) + 0.7f * Ease.Hash(i * 7 + 5);
            for (int j = 0; j < 4; j++) R(x - (7 - 2 * j) * u * s / 2, y - (j + 1) * 4 * u * s, (7 - 2 * j) * u * s, 4 * u * s + u, c);
            if (sceneWinter) R(x - 2.5f * u * s, y - 16 * u * s, 5 * u * s, u, rimC * 0.65f);
        }
    }

    void Hamlet()
    {
        float wx = ui.W * 0.47f, x0 = wx - lean * 0.7f;
        var c = Kit.Mul(midC, 0.72f);
        var fire = Color.Lerp(Kit.Ember, Flame, 0.4f + 0.3f * MathF.Sin(time * 5.1f));
        bool lit = torchesLit;
        void House(float dx, int w, int h)
        {
            float x = x0 + dx * u, y = MidY(wx + dx * u) + 2 * u;
            R(x, y - h * u, w * u, h * u + 2 * u, c);
            for (int i = 0; 2 * i < w + 2; i++) R(x - u + i * u, y - (h + i + 1) * u, (w + 2 - 2 * i) * u, u, c);
            if (lit) { R(x + (w / 2 - 1) * u, y - (h - 1) * u, 3 * u, 3 * u, fire); ui.Disc(new Vector2(x + (w / 2 + 0.5f) * u, y - (h - 2.5f) * u), U(9), Kit.Ember * 0.1f); }
        }
        House(-34, 12, 6); House(-18, 10, 7); House(8, 13, 6); House(25, 9, 5);
        float cxh = x0 - 4 * u, cy = MidY(wx - 4 * u) + 2 * u;
        R(cxh, cy - 12 * u, 9 * u, 14 * u, c); R(cxh + 2 * u, cy - 22 * u, 5 * u, 10 * u, c);
        for (int i = 0; i < 3; i++) R(cxh + (2 + i) * u, cy - (24 + 2 * i) * u, (5 - 2 * i) * u, 2 * u, c);
        R(cxh + 4 * u, cy - 31 * u, u, 4 * u, c); R(cxh + 3 * u, cy - 30 * u, 3 * u, u, c);
        if (lit) R(cxh + 4 * u, cy - 17 * u, u, 3 * u, fire * 0.8f);
        for (int i = 0; i < 7; i++)
        {
            float t = (time * 0.12f + i / 7f) % 1;
            ui.Disc(new Vector2(x0 + (13 + 8 * t * t) * u, MidY(wx) - (8 + 46 * t) * u), U(1.5f + 4 * t), hazeC * (0.16f * (1 - t)));
        }
    }

    // ---------- the company on the march (dynamic count & composition) ----------

    void Column()
    {
        if (march.Count == 0) return;
        float span = Math.Max(ui.W + U(160), U(marchLength + 240));
        float head = ui.W * 0.92f + time * U(4.5f);
        for (int i = march.Count - 1; i >= 0; i--)
        {
            var (who, back) = march[i];
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
        ui.Line(hip, new Vector2(x + s * 4 * u, y), nearC, 2);
        ui.Line(hip, new Vector2(x - s * 4 * u, y), nearC, 2);
        R(x - 3 * u, y - 12 * u, 7 * u, 4 * u, nearC);
        R(x - 2 * u, y - 15 * u, 5 * u, 4 * u, nearC);
        R(x - 3 * u, y - 21 * u, 7 * u, 7 * u, nearC);
        R(x - u, y - 22 * u, 3 * u, u, nearC);
        R(x - 2 * u, y - 26 * u, 5 * u, 4 * u, nearC);
        if (who == Who.Mage)
        {
            // Pointed hood & flowing robe skirt
            R(x - 4 * u, y - 13 * u, 9 * u, 9 * u, nearC);
            R(x - 3 * u, y - 28 * u, 6 * u, 3 * u, nearC);
            R(x - 4 * u, y - 29 * u, 4 * u, 2 * u, nearC);
        }
        else if (who == Who.CaptainFoot)
        {
            // Plumed helm & flowing cloak
            R(x - 2 * u, y - 29 * u, 4 * u, 3 * u, nearC);
            R(x - 6 * u, y - 29 * u, 4 * u, 2 * u, Color.Lerp(Colours.Cloth, nearC, 0.2f));
            for (int j = 0; j < 5; j++) R(x - (5 + j * 0.5f) * u, y - (20 - j * 2.5f) * u, 3 * u, 2.5f * u, nearC);
        }
        else
        {
            R(x - u, y - 27 * u, 3 * u, u, nearC);
            R(x - 4 * u, y - 25 * u, 9 * u, u, nearC);
        }
        R(x + s * 4 * u, y - u, 3 * u, u, nearC); R(x - s * 4 * u, y - u, 3 * u, u, nearC);
        if (who is Who.Spear or Who.Banner or Who.CaptainFoot) ui.Disc(new Vector2(x - 3 * u, y - 16 * u), U(4.5f), nearC);
        ui.Line(new Vector2(x + u, y - 19 * u), new Vector2(x + 4 * u, y - 12 * u), nearC, 2);
        var hand = new Vector2(x + 4 * u, y - 10 * u);
        float tilt = (Ease.Hash(i * 5 + 3) - 0.5f) * 5 * u + s * u;
        switch (who)
        {
            case Who.Spear or Who.CaptainFoot:
                var tip = new Vector2(hand.X + tilt, y - (42 + 5 * Ease.Hash(i)) * u);
                ui.Line(hand, tip, nearC, 1);
                R(tip.X - u, tip.Y - 3 * u, 2 * u, 4 * u, dayAmount > 0.5f ? Color.Lerp(nearC, rimC, 0.5f) : nearC);
                break;
            case Who.Torch:
                var top = new Vector2(hand.X + 2 * u, y - 31 * u);
                ui.Line(hand, top, nearC, 1);
                float fl = 0.75f + 0.25f * MathF.Sin(time * 11 + i * 3) * MathF.Sin(time * 6.3f + i);
                ui.Disc(top, U(18) * fl, Kit.Ember * 0.08f); ui.Disc(top, U(9) * fl, Kit.Ember * 0.18f);
                ui.Disc(top - new Vector2(0, u), U(2.6f) * fl, Kit.Ember); ui.Disc(top - new Vector2(0, 2 * u), U(1.5f), Flame);
                break;
            case Who.Mage:
                var staffTop = new Vector2(hand.X + 2 * u, y - 36 * u);
                ui.Line(new Vector2(hand.X, y), staffTop, nearC, 1.5f);
                float pulse = 0.8f + 0.2f * MathF.Sin(time * 4.5f + i * 2.3f);
                var arcane = new Color(118, 214, 255);
                ui.Disc(staffTop, U(13) * pulse, arcane * 0.10f);
                ui.Disc(staffTop, U(6) * pulse, arcane * 0.26f);
                ui.Disc(staffTop, U(2.2f), new Color(224, 248, 255));
                break;
            case Who.Bow:
                ui.Line(new Vector2(x - 3 * u, y - 27 * u), new Vector2(x - 7 * u, y - 17 * u), nearC, 1);
                ui.Line(new Vector2(x - 7 * u, y - 17 * u), new Vector2(x - 3 * u, y - 7 * u), nearC, 1);
                R(x - 5 * u, y - 21 * u, 3 * u, 8 * u, nearC);
                break;
            case Who.Banner:
                ui.Line(hand, new Vector2(hand.X, y - 56 * u), nearC, 1.5f);
                R(hand.X - u, y - 58 * u, 3 * u, 2 * u, Color.Lerp(Kit.Gilt, nearC, 0.3f));
                Flag(hand.X - u, y - 55 * u, U(22), U(13), time * 4 + i, -1, Color.Lerp(Colours.Cloth, nearC, 0.16f), Color.Lerp(Colours.Trim, nearC, 0.16f));
                break;
        }
    }

    void Horse(float x, float y, float ph)
    {
        y -= MathF.Abs(MathF.Cos(ph)) * u;
        R(x - 12 * u, y - 23 * u, 24 * u, 9 * u, nearC);
        R(x - 13 * u, y - 22 * u, 2 * u, 6 * u, nearC); R(x + 11 * u, y - 24 * u, 3 * u, 8 * u, nearC);
        ui.Line(new Vector2(x + 9 * u, y - 19 * u), new Vector2(x + 16 * u, y - 32 * u), nearC, 8);
        ui.Line(new Vector2(x + 14 * u, y - 34 * u), new Vector2(x + 24 * u, y - 26 * u), nearC, 5.5f);
        R(x + 13 * u, y - 39 * u, 2 * u, 4 * u, nearC);
        ui.Line(new Vector2(x + 8 * u, y - 23 * u), new Vector2(x + 13 * u, y - 34 * u), rimC * 0.3f, 1);
        R(x - 12 * u, y - 24 * u, 20 * u, u, rimC * 0.22f);
        ui.Line(new Vector2(x - 12 * u, y - 21 * u), new Vector2(x - 17 * u - MathF.Sin(ph * 0.5f) * u, y - 9 * u), nearC, 2);
        for (int l = 0; l < 4; l++)
        {
            float hx = x + (l < 2 ? -10 + l * 4 : 6 + (l - 2) * 4) * u, a = MathF.Sin(ph + l * MathF.PI / 2 * (l % 2 == 0 ? 1 : 3));
            var knee = new Vector2(hx + a * 2 * u, y - 8 * u);
            ui.Line(new Vector2(hx, y - 16 * u), knee, nearC, 3.5f);
            ui.Line(knee, new Vector2(hx + a * 5 * u, y), nearC, 2);
        }
    }

    void Rider(float x, float y, float ph, bool standard, int i)
    {
        Horse(x, y, ph);
        y -= MathF.Abs(MathF.Cos(ph)) * u;
        R(x - 3 * u, y - 31 * u, 6 * u, 9 * u, nearC);
        R(x - 5 * u, y - 36 * u, 9 * u, 5 * u, nearC);
        R(x - 4 * u, y - 37 * u, 7 * u, u, nearC);
        R(x - 2 * u, y - 42 * u, 5 * u, 5 * u, nearC);
        R(x - u, y - 43 * u, 3 * u, u, nearC); R(x, y - 46 * u, u, 3 * u, nearC);
        for (int j = 0; j < 7; j++) R(x - (6 + j * 0.5f) * u, y - (34 - j * 2) * u, 3 * u, 2 * u, nearC);
        ui.Line(new Vector2(x + 2 * u, y - 34 * u), new Vector2(x + 5 * u, y - 25 * u), nearC, 2.5f);
        R(x - 5 * u, y - 37 * u, u, 9 * u, rimC * 0.28f);
        ui.Line(new Vector2(x, y - 24 * u), new Vector2(x + 3 * u, y - 13 * u), nearC, 2.5f);
        var hand = new Vector2(x + 4 * u, y - 25 * u);
        if (standard)
        {
            ui.Line(hand, new Vector2(hand.X, y - 82 * u), nearC, 1.5f);
            R(hand.X - u, y - 84 * u, 3 * u, 3 * u, Color.Lerp(Kit.Gilt, nearC, 0.2f));
            Flag(hand.X - u, y - 80 * u, U(34), U(20), time * 3.6f, -1, Color.Lerp(Colours.Cloth, nearC, 0.1f), Color.Lerp(Colours.Trim, nearC, 0.1f));
        }
        else
        {
            var tip = new Vector2(hand.X + (Ease.Hash(i * 9) - 0.3f) * 5 * u, y - 70 * u);
            ui.Line(hand, tip, nearC, 1);
            Flag(tip.X, tip.Y + 2 * u, U(11), U(5), time * 5 + i, -1, Color.Lerp(Colours.Trim, nearC, 0.25f), Color.Lerp(Colours.Cloth, nearC, 0.25f));
        }
    }

    void Wagon(float x, float y, float ph)
    {
        Horse(x + 36 * u, y, ph);
        ui.Line(new Vector2(x + 13 * u, y - 12 * u), new Vector2(x + 26 * u, y - 16 * u), nearC, 1.5f);
        R(x - 17 * u, y - 16 * u, 31 * u, 8 * u, nearC);
        R(x - 16 * u, y - 27 * u, 29 * u, 11 * u, nearC); R(x - 14 * u, y - 29 * u, 25 * u, 2 * u, nearC);
        ui.Disc(new Vector2(x - 9 * u, y - 6 * u), U(6), nearC); ui.Disc(new Vector2(x + 7 * u, y - 6 * u), U(6), nearC);
    }

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

    void Snowfall()
    {
        for (int i = 0; i < 56; i++)
        {
            float life = 7 + (i % 5), t = ((time + i * 0.43f) % life) / life;
            float x = (ui.W * ((i * 0.618f) % 1) + MathF.Sin(t * 5 + i) * U(18) - lean * 0.4f + ui.W) % ui.W;
            float y = ui.H * t;
            R(x, y, Math.Max(1, U(1.5f)), Math.Max(1, U(1.5f)), new Color(235, 244, 255) * (0.55f + 0.3f * MathF.Sin(i)));
        }
    }

    void Rainfall()
    {
        int drops = (int)(35 + 55 * sceneRain);
        for (int i = 0; i < drops; i++)
        {
            float life = 1.1f + (i % 3) * 0.25f, t = ((time + i * 0.17f) % life) / life;
            float x = (ui.W * ((i * 0.382f) % 1) - t * U(55) + ui.W * 2) % ui.W;
            float y = ui.H * t;
            ui.Line(new Vector2(x, y), new Vector2(x - U(5), y + U(16)), new Color(188, 206, 224) * 0.32f, 1);
        }
    }
}
