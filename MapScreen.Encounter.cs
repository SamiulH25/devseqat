using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the encounter as a scene ("The Field Between"). The field the battle would be fought on is painted full screen
/// from the real ground (`Campaign.FieldOf`: the kind of country, the layout, who holds it, the hour and the weather), your
/// companies drawn up on the left and theirs on the right as the battle's own puppets (`CampStage`, one figure for about six
/// men), each under its banner. The leaders' plates sit in the top corners with their companies listed; the balance of
/// strength and the odds in words across the top; the choices on a bar along the bottom, each with what it would bring.
/// Drawing and input only; the forecast is `Campaign.AutoForecast`.
partial class MapScreen
{
    CampStage? encStage;
    /// The figures drawn this frame, and each company's figures and screen box (laid out by `EncounterLayout`).
    readonly List<Unit> encMen = new();
    readonly List<EncBlock> encBlocks = new();
    /// The block under the mouse (or its roster row), -1 for none; read by the scene and the plates.
    int encHover = -1;
    long encTick;
    (int Id, int Quarter, (float Win, int Few, int Many, float Mine, float Theirs) F)? encForecast;

    /// One company on the field: its side (-1 yours, +1 theirs), the record, where its men stand, its box on the screen.
    sealed record EncBlock(int Side, CompanyRecord K, List<Unit> Men, Vector2 Back)
    {
        public Rectangle Box;
    }

    const int EncShown = 6;
    int EncPixel => Math.Max(2, gd.Viewport.Height / 240);

    /// Where the `i`th company of a side stands (its front corner nearest the foe): three depths, then further out.
    static Vector2 EncSlot(int i, int side) => new(side * (4.2f + i / 3 * 6f + i % 3 * 1.6f), (i % 3) switch { 0 => 3.5f, 1 => 11f, _ => -4f });

    /// Lay out both sides' figures: each company a block of files facing the foe, mounted men spaced wider.
    void EncounterLayout(Party e, int w, int h)
    {
        encStage!.Frame(w, h);
        encMen.Clear(); encBlocks.Clear();
        int index = 1;
        void Side(IEnumerable<CompanyRecord> companies, int side, int palette)
        {
            int i = 0;
            foreach (var k in companies.Where(k => k.Men > 0).OrderByDescending(k => k.Men).Take(EncShown))
            {
                var at = EncSlot(i++, side);
                bool big = k.Type.Size >= 1.4f || k.Type.Look.Beast; // a troll or a bear is shown one for one
                int per = big ? 1 : 4, n = Math.Clamp((k.Men + per - 1) / per, 1, big ? 6 : 12), deep = Math.Min(3, n);
                var stacks = k.Stacks.Where(s => s.Men > 0).ToList();
                var men = new List<Unit>();
                for (int j = 0; j < n; j++)
                {
                    // Men of each stack in proportion to its share, so a mixed company shows its mix.
                    var t = stacks.Count == 0 ? k.Type : stacks[Math.Min(stacks.Count - 1, (int)(j / (float)n * stacks.Count))].Type;
                    float gap = (t.Mount != null ? 1.6f : 0.85f) * MathF.Max(1, t.Size * (t.Look.Beast ? 1.6f : 1));
                    int file = j / deep, rank = j % deep;
                    var pos = new Vector2(at.X + side * (file * gap + (rank % 2) * 0.25f), at.Y - rank * gap * 0.9f);
                    float face = side < 0 ? 0 : MathF.PI;
                    var u = new Unit
                    {
                        Index = index++, Type = t, Palette = palette, Coat = (k.Id * 7 + j) % Style.Coats.Length, Hp = t.MaxHp, Anim = Ease.Hash(k.Id * 31 + j) * 2,
                        Pos = pos, Facing = face, ViewFacing = face, Heading = face, ViewHeading = face, EnemyDist = float.MaxValue,
                    };
                    if (t.Mount != null) { u.Horse = t.Mount; u.HorseHp = t.Mount.Hp; }
                    men.Add(u);
                    encMen.Add(u);
                }
                var back = new Vector2(men.Max(m => side * m.Pos.X) * side + side * 0.9f, at.Y - (deep - 1) * 0.8f);
                var b = new EncBlock(side, k, men, back);
                encBlocks.Add(b);
            }
        }
        Side(C.Player.Companies, -1, C.PlayerPalette);
        Side(e.Companies, 1, Style.PaletteOf(C.SideOf(e)));
        // The captain at the head of your side.
        var ct = Rules.CaptainType(C.Captain);
        var cap = new Unit
        {
            Index = index++, Type = ct, Palette = C.PlayerPalette, Hp = ct.MaxHp, Pos = new Vector2(-2.2f, 6.5f), EnemyDist = float.MaxValue,
            Facing = 0, ViewFacing = 0, Heading = 0, ViewHeading = 0,
        };
        if (ct.Mount != null) { cap.Horse = ct.Mount; cap.HorseHp = ct.Mount.Hp; }
        encMen.Add(cap);
        // Each block's box on the screen, for hovering.
        foreach (var b in encBlocks)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var u in b.Men)
            {
                float top = u.Mounted ? 2.7f : 2f;
                var f = encStage.Project(new Vector3(u.Pos.X, 0, u.Pos.Y)); var hd = encStage.Project(new Vector3(u.Pos.X, top, u.Pos.Y));
                float hw = encStage.PerMetre(u.Pos.Y) * (u.Mounted ? 1.2f : 0.45f);
                x0 = MathF.Min(x0, f.X - hw); x1 = MathF.Max(x1, f.X + hw); y0 = MathF.Min(y0, hd.Y); y1 = MathF.Max(y1, f.Y);
            }
            b.Box = new Rectangle((int)x0, (int)y0, (int)(x1 - x0), (int)(y1 - y0));
        }
    }

    /// Draw the figures into their texture before the frame's sprites (from `Draw`).
    void PrepareEncounter()
    {
        if (panel != PanelKind.Encounter || bare || C.Encounter is not Party e || C.Meeting(e)) return;
        encStage ??= new CampStage(gd, art, shading, campKit, () => lifeQuality, new Vector3(0, 7f, 32f), new Vector3(0, 1.5f, -1f), 0.5f);
        long now = Environment.TickCount64;
        float dt = encTick == 0 ? 0 : Math.Min(0.1f, (now - encTick) / 1000f);
        encTick = now;
        int w = gd.Viewport.Width, h = gd.Viewport.Height;
        EncounterLayout(e, w, h);
        var look = CampLook();
        encStage.Render(w, h, encMen, EncPixel, look.Night ? 0.45f : CampLight(look), false, dt);
    }

    /// What auto-resolve would likely bring, worked out once a quarter hour for this foe.
    (float Win, int Few, int Many, float Mine, float Theirs) Forecast(Party e)
    {
        int q = (int)(C.S.Hours * 4);
        if (encForecast is not { } f || f.Id != e.Id || f.Quarter != q) encForecast = f = (e.Id, q, C.AutoForecast(e));
        return f.F;
    }

    void EncounterPanel()
    {
        var e = C.Encounter!;
        if (C.Meeting(e))
        {
            if (e.Kind == PartyKind.Lord && C.CharacterOf(e) is { Kind: "lord" } met) OpenTalk(met, PanelKind.Encounter); // a friendly lord: talk (LD3)
            else MeetingPanel(e);
            return;
        }
        modalIn = 1;
        ui.Block(new Rectangle(0, 0, ui.W, ui.H));
        if (encStage == null || encBlocks.Count == 0 && encMen.Count == 0) { ui.Rect(new Rectangle(0, 0, ui.W, ui.H), Kit.IronDark); return; } // the first frame: the men are drawn from the next
        var l = CampLook();
        var c = ColoursOf(l);
        float t = Ui.Instant ? 3.2f : Environment.TickCount64 / 1000f;
        var (ground, layout) = C.FieldOf(e);
        var weather = C.WeatherFor(e);
        // Which company the mouse is on: a block on the field, or (below) a row of a plate.
        encHover = -1;
        for (int i = encBlocks.Count - 1; i >= 0; i--) if (ui.Hover(encBlocks[i].Box)) { encHover = i; break; }
        int s0 = ui.S;
        ui.S = EncPixel;
        try { FieldScene(e, l, c, ground, layout, t); }
        finally { ui.S = s0; }
        if (encStage.Texture != null) ui.Sb.Draw(encStage.Texture, new Rectangle(0, 0, ui.W, ui.H), Color.White);
        ui.S = EncPixel;
        try { FieldWeather(l, c, weather, t); }
        finally { ui.S = s0; }
        ui.Vignette(0.5f);
        int plateW = Math.Min(ui.U(330), ui.W / 3 - ui.U(16));
        var mine = SidePlate(new Rectangle(ui.U(10), ui.U(10), plateW, 0), e, -1);
        var theirs = SidePlate(new Rectangle(ui.W - ui.U(10) - plateW, ui.U(10), plateW, 0), e, 1);
        StrengthStrip(new Rectangle(mine.Right + ui.U(16), ui.U(8), theirs.X - mine.Right - ui.U(32), 0), e, ground, layout, weather, l);
        if (encHover >= 0) BlockCard(encBlocks[encHover]);
        ChoiceBar(e, theirs);
    }

    // ---------- the field ----------

    Vector2 EP(float x, float z, float y = 0) => encStage!.Project(new Vector3(x, y, z));

    /// The sky, the country and the ground of the field, its features by layout, and each company's banner: everything behind the men.
    void FieldScene(Party e, TownLook l, TownColours c, TerrainParams g, FieldLayout layout, float t)
    {
        var st = encStage!;
        int seed = C.Map.ProvinceAt(C.Player.Pos).Id;
        var full = new Rectangle(0, 0, ui.W, ui.H);
        int horizon = (int)EP(0, -90).Y;
        var sky = new Rectangle(0, 0, ui.W, horizon);
        ui.VGrad(sky, c.SkyTop, c.SkyLow, 2);
        if (l.Night) ui.Stars(new Rectangle(0, 0, ui.W, horizon * 2 / 3), t, 70);
        else Clouds(sky, l, c, t, seed);
        Orb(sky, l, 0.5f);
        snowCap = l.Snow ? c.Snow : Color.Transparent;
        var far = Color.Lerp(Color.Lerp(c.Far, c.SkyTop, 0.5f), c.SkyLow, 0.35f);
        if (l.Country != "Coast") ui.Hills(full, horizon - ui.U(30), ui.U(12), 1.6f + seed % 5, far);
        Country(full, l, c, horizon, new Random(seed * 7 + 3));
        // The ground: lighter far off, darker near; darker still when sodden.
        Color grass = l.Snow ? c.Snow : Lands.Of(l.Country).Hot ? Color.Lerp(c.Near, new Color(196, 168, 118), CampLight(l) - 0.2f)
            : Color.Lerp(c.Near, new Color(96, 120, 64), l.Night ? 0.35f : l.Dusk ? 0.6f : 0.9f);
        if (g.Wet > 0.5f) grass = Kit.Mul(Color.Lerp(grass, new Color(92, 80, 60), 0.3f), 0.9f);
        ui.VGrad(new Rectangle(0, horizon, ui.W, ui.H - horizon), Color.Lerp(grass, c.Far, 0.35f), Kit.Mul(grass, 0.72f), 2);
        var rng = new Random(seed * 31 + 5);
        for (int i = 0; i < 90; i++) // tufts, thicker near
        {
            float d = rng.NextSingle();
            int y = horizon + (int)((ui.H - horizon) * d * d + ui.U(2)), x = rng.Next(ui.W);
            ui.Rect(new Rectangle(x, y, ui.U(2 + (int)(d * 3)), ui.U(1)), Kit.Mul(grass, l.Snow ? 0.92f : 0.82f));
        }
        FieldFeatures(l, c, g, layout, grass, rng);
        // The hovered company's ground glows under it.
        if (encHover >= 0)
        {
            var b = encBlocks[encHover];
            float cx = b.Men.Average(m => m.Pos.X), cz = b.Men.Average(m => m.Pos.Y);
            float rx = b.Men.Max(m => MathF.Abs(m.Pos.X - cx)) + 1.1f, rz = b.Men.Max(m => MathF.Abs(m.Pos.Y - cz)) + 0.9f;
            GroundBlob(cx, cz, rx, rz, (b.Side < 0 ? Style.Friendly : Style.Foe) * 0.35f);
        }
        // Each company's banner behind it: yours on your company's charge, theirs in their colours.
        var mineHd = Style.Sides[C.PlayerPalette];
        var theirHd = Style.Sides[Style.PaletteOf(C.SideOf(e))];
        foreach (var b in encBlocks.Where(b => b.Side < 0 || e.Kind != PartyKind.Monster).OrderBy(b => b.Back.Y))
        {
            var p = EP(b.Back.X, b.Back.Y);
            float pm = st.PerMetre(b.Back.Y);
            int top = (int)(p.Y - (b.Men.Any(m => m.Mounted) ? 4.4f : 3.6f) * pm), bw = Math.Max(ui.U(6), (int)(0.75f * pm)) / ui.S * ui.S;
            ui.Rect(new Rectangle((int)p.X - ui.U(1), top, Math.Max(ui.U(1), (int)(0.08f * pm)), (int)p.Y - top), l.Night ? new Color(44, 32, 28) : new Color(96, 68, 44));
            var hd = b.Side < 0 ? mineHd with { Charge = (Emblem)b.K.Banner } : theirHd;
            int dir = b.Side < 0 ? 1 : -1; // the cloth flies toward the foe
            ui.Banner((int)p.X + dir * (bw / 2 + ui.U(1)), top + ui.U(1), bw, (int)(1.1f * pm), hd, t * 1.3f + b.K.Id);
        }
        snowCap = Color.Transparent;
    }

    /// A flat patch of ground (an ellipse lying on the field), painted in strips: pools, glows, a ford.
    void GroundBlob(float x, float z, float rx, float rz, Color col)
    {
        float step = MathF.Max(0.08f, rz / 24);
        for (float k = -rz; k < rz; k += step)
        {
            float w = rx * MathF.Sqrt(MathF.Max(0, 1 - k * k / (rz * rz)));
            var a = EP(x - w, z + k); var b = EP(x + w, z + k); var nb = EP(x, z + k + step);
            int y0 = (int)MathF.Min(a.Y, nb.Y), hgt = Math.Max(1, (int)MathF.Abs(nb.Y - a.Y) + 1);
            ui.Rect(new Rectangle((int)a.X, y0, Math.Max(1, (int)(b.X - a.X)), hgt), col);
        }
    }

    /// The layout's features, all behind the companies or flat on the ground: a wood, high ground, a river between the lines,
    /// hedges, pools, a hamlet, rocks.
    void FieldFeatures(TownLook l, TownColours c, TerrainParams g, FieldLayout layout, Color grass, Random rng)
    {
        int holder = g.Favour == 1 ? 1 : -1; // the side that chose the ground
        var water = l.Snow ? new Color(150, 170, 200) : l.Night ? new Color(40, 56, 84) : l.Dusk ? new Color(120, 110, 140) : new Color(84, 126, 156);
        void Trees(float x0, float x1, float z0, float z1, int n)
        {
            var pts = Enumerable.Range(0, n).Select(_ => (X: x0 + rng.NextSingle() * (x1 - x0), Z: z0 + rng.NextSingle() * (z1 - z0))).OrderBy(p => p.Z).ToList();
            foreach (var (x, z) in pts) { var p = EP(x, z); Tree((int)p.X, (int)p.Y, (int)(encStage!.PerMetre(z) * (5 + rng.NextSingle() * 2.5f)), l, c, rng); }
        }
        void Rise(int side, float height, float steep)
        {
            // High ground behind a side: a hump whose silhouette stands against the far field, lit on top.
            var rise = Color.Lerp(grass, c.Rim, l.Night ? 0.05f : 0.12f);
            const float z = -14;
            for (float x = -26; x < 26; x += 0.25f)
            {
                float d = (x * side - 9) / 9; // centred behind the side's lines
                float hgt = height * MathF.Max(0, 1 - MathF.Pow(MathF.Abs(d), steep));
                if (hgt <= 0.02f) continue;
                var bottom = EP(x, z); var top = EP(x, z, hgt); var next = EP(x + 0.25f, z);
                ui.Rect(new Rectangle((int)bottom.X, (int)top.Y, Math.Max(1, (int)(next.X - bottom.X) + 1), (int)(bottom.Y - top.Y)), rise);
                ui.Rect(new Rectangle((int)bottom.X, (int)top.Y, Math.Max(1, (int)(next.X - bottom.X) + 1), ui.U(1)), Color.Lerp(rise, c.Rim, 0.3f));
            }
        }
        switch (layout)
        {
            case FieldLayout.Ridge: Rise(holder, 3.2f, 1.6f); break;
            case FieldLayout.TwoHills: Rise(-1, 2.4f, 2f); Rise(1, 2.4f, 2f); break;
            case FieldLayout.Pass: Rise(-1, 9f, 0.7f); Rise(1, 9f, 0.7f); break;
            case FieldLayout.WoodedFlank:
            {
                int side = (Rng.Mix(C.S.Seed, C.Encounter!.Id) & 1) == 0 ? -1 : 1;
                Trees(side * 7, side * 20, -22, -9, 16);
                break;
            }
            case FieldLayout.ForestClearing:
                Trees(-24, 24, -26, -12, 24);
                Trees(-24, -14, -11, 2, 6); Trees(14, 24, -11, 2, 6);
                break;
            case FieldLayout.Village:
                for (int i = 0; i < 4; i++) { float x = holder * (5 + i * 3.2f) + (float)rng.NextDouble(); Hut(EP(x, -13 - i % 2 * 3), encStage!.PerMetre(-13 - i % 2 * 3), l, c); }
                break;
            case FieldLayout.Hedgerows:
                foreach (var (z, x0, x1) in new[] { (-16f, -30f, 30f), (-9f, -30f, -10f), (-9f, 10f, 30f) })
                {
                    var a = EP(x0, z); var b = EP(x1, z);
                    float pm = encStage!.PerMetre(z);
                    var hedge = Color.Lerp(c.Near, new Color(56, 84, 44), l.Night ? 0.3f : 0.75f);
                    ui.Rect(new Rectangle((int)a.X, (int)(a.Y - 1.2f * pm), (int)(b.X - a.X), (int)(1.2f * pm)), hedge);
                    for (float x = a.X; x < b.X; x += ui.U(5)) ui.Rect(new Rectangle((int)x, (int)(a.Y - 1.2f * pm) - ui.U(1), ui.U(3), ui.U(1)), Kit.Mul(hedge, 1.15f));
                }
                break;
            case FieldLayout.Fen:
                for (int i = 0; i < 9; i++) GroundBlob(-16 + rng.NextSingle() * 32, -14 + rng.NextSingle() * 22, 1.2f + rng.NextSingle() * 2, 0.5f + rng.NextSingle() * 0.8f, water);
                break;
            case FieldLayout.Broken:
                for (int i = 0; i < 14; i++)
                {
                    float x = -18 + rng.NextSingle() * 36, z = -16 + rng.NextSingle() * 24;
                    if (MathF.Abs(x) > 2 && MathF.Abs(x) < 12 && z > -5) continue; // not under the companies
                    var p = EP(x, z); float pm = encStage!.PerMetre(z) * (0.4f + rng.NextSingle() * 0.5f);
                    var stone = l.Night ? new Color(64, 62, 78) : new Color(128, 122, 112);
                    ui.Disc(new Vector2(p.X, p.Y - pm * 0.4f), pm * 0.6f, Kit.Mul(stone, 0.8f));
                    ui.Disc(new Vector2(p.X - pm * 0.15f, p.Y - pm * 0.55f), pm * 0.4f, stone);
                }
                break;
            case FieldLayout.RiverLine:
            {
                // The river runs between the lines, from the far field to your feet; a pale edge where it meets each bank.
                for (float z = -90; z < 16; z += 0.5f)
                {
                    float wob = MathF.Sin(z * 0.15f) * 0.6f;
                    var a = EP(wob - 1.3f, z); var b = EP(wob + 1.3f, z); var nb = EP(0, z + 0.5f);
                    int y0 = (int)a.Y, hgt = Math.Max(1, (int)(nb.Y - a.Y) + 1);
                    ui.Rect(new Rectangle((int)a.X, y0, Math.Max(1, (int)(b.X - a.X)), hgt), water);
                    ui.Rect(new Rectangle((int)a.X - ui.U(1), y0, ui.U(1), hgt), Color.Lerp(water, Color.White, 0.25f));
                    ui.Rect(new Rectangle((int)b.X, y0, ui.U(1), hgt), Kit.Mul(water, 0.75f));
                }
                break;
            }
        }
    }

    /// A cottage of the field's hamlet: whitewashed or timber walls, a pitched roof, a dark door.
    void Hut(Vector2 foot, float pm, TownLook l, TownColours c)
    {
        int w = (int)(3.4f * pm), wh = (int)(2.0f * pm), rh = (int)(1.6f * pm), x0 = (int)foot.X - w / 2, y0 = (int)foot.Y;
        ui.Rect(new Rectangle(x0, y0 - wh, w, wh), c.Wall);
        ui.Rect(new Rectangle(x0, y0 - wh, w / 5, wh), Kit.Mul(c.Wall, 0.82f));
        int s = Math.Max(1, ui.U(1));
        for (int r = 0; r < rh; r += s)
        {
            int half = (int)((w / 2f + pm * 0.3f) * r / rh);
            ui.Rect(new Rectangle((int)foot.X - half, y0 - wh - rh + r, half * 2, s), r < s * 2 ? Kit.Mul(c.Roof, 1.15f) : c.Roof);
        }
        if (snowCap.A > 0) for (int r = 0; r < rh / 3; r += s) { int half = (int)((w / 2f) * r / rh); ui.Rect(new Rectangle((int)foot.X - half, y0 - wh - rh + r, half * 2, s), snowCap); }
        ui.Rect(new Rectangle((int)foot.X - (int)(0.35f * pm), y0 - (int)(1.2f * pm), (int)(0.7f * pm), (int)(1.2f * pm)), l.Night ? new Color(255, 184, 92) * 0.8f : new Color(46, 36, 32));
    }

    /// Rain or snow falling across the field, fog laid over it.
    void FieldWeather(TownLook l, TownColours c, Weather w, float t)
    {
        var full = new Rectangle(0, 0, ui.W, ui.H);
        if (l.Night) ui.Rect(full, Kit.Night * 0.35f); // the men take the night's dark with the field
        else if (l.Dusk) ui.Rect(full, Kit.Dusk * 0.15f);
        if (w.Fog > 0.05f) ui.VGrad(full, Color.Lerp(c.SkyLow, Color.Gray, 0.4f) * (w.Fog * 0.25f), Color.Lerp(c.SkyLow, Color.Gray, 0.5f) * (w.Fog * 0.55f), 4);
        if (w.Snow || l.Snow && w.Rain >= 0.3f) { Snowfall(full, t, l.Night); return; }
        if (w.Rain < 0.3f) return;
        var rng = new Random(91);
        var drop = (l.Night ? new Color(140, 150, 190) : new Color(200, 210, 230)) * 0.35f;
        int n = (int)(160 * w.Rain), len = ui.U(6), s = Math.Max(1, ui.U(1));
        for (int i = 0; i < n; i++)
        {
            float speed = 0.6f + rng.NextSingle() * 0.4f;
            int x = (int)((rng.Next(ui.W + ui.U(40)) - t * ui.U(20) * speed) % (ui.W + ui.U(40)));
            if (x < 0) x += ui.W + ui.U(40);
            int y = (int)((rng.Next(ui.H) + t * ui.U(160) * speed) % ui.H);
            for (int k = 0; k < len; k += s) ui.Rect(new Rectangle(x - k / 3, y + k, s, s), drop);
        }
    }

    // ---------- the plates ----------

    /// A side's plate in a top corner: its leader's face, its name, and its companies (hover a row to find it on the field).
    /// Returns the plate's rectangle (its height follows the rows).
    Rectangle SidePlate(Rectangle r, Party e, int side)
    {
        var blocks = encBlocks.Where(b => b.Side == side).ToList();
        var all = (side < 0 ? C.Player.Companies : e.Companies).Where(k => k.Men > 0).ToList();
        bool lord = side > 0 && e.Kind == PartyKind.Lord;
        int rowH = ui.U(26), head = ui.U(78), extra = all.Count > blocks.Count ? ui.U(14) : 0, buttons = lord ? ui.U(38) : 0;
        string flavour = side > 0 ? FoeLine(e) : "";
        int flavourH = flavour != "" ? ui.U(14) : 0;
        r = new Rectangle(r.X, r.Y, r.Width, head + flavourH + blocks.Count * rowH + extra + buttons + ui.U(8));
        ui.PlateBody(r, side < 0 ? Style.Friendly : Style.Foe, 1, 0.92f);
        // The face: a painted bust for lords and for you, else the troop at the head of the band.
        var face = new Rectangle(side < 0 ? r.X + ui.U(8) : r.Right - ui.U(8) - ui.U(56), r.Y + ui.U(8), ui.U(56), ui.U(64));
        ui.Rect(face, Color.Black * 0.4f);
        var who = side < 0 ? C.S.Characters.FirstOrDefault(k => k.Kind == "captain") : C.CharacterOf(e);
        if (who != null) ui.Face(PortraitPainter.Of(C, who), face);
        else if (all.Count > 0) ui.Portrait(all.OrderByDescending(k => k.Type.Tier).First().Type, face, palette: Style.PaletteOf(C.SideOf(e)));
        int tx = side < 0 ? face.Right + ui.U(10) : r.X + ui.U(10), tw = r.Width - face.Width - ui.U(28);
        string name = side < 0 ? "YOUR COMPANY" : e.Name.ToUpperInvariant();
        int ns = ui.FitScale(name, tw);
        ui.TextFit(name, tx, r.Y + ui.U(ns == 2 ? 10 : 14), tw, side < 0 ? Style.Friendly : PartyColor(e), ns);
        ui.TextFit(side < 0 ? $"{C.Player.Men} MEN" + (C.Player.Wounded > 0 ? $", {C.Player.Wounded} HURT" : "") : Allegiance(e), tx, r.Y + ui.U(30), tw, Style.Dim, 1);
        if (side > 0 && who != null && who.Kind == "lord")
        {
            float op = C.OpinionOfYou(who);
            ui.TextFit($"THINKS {OpinionWord(op)} OF YOU", tx, r.Y + ui.U(44), tw, OpinionColor(op), 1);
            if (who.Traits.Count > 0) ui.TextFit(string.Join(", ", who.Traits).ToUpperInvariant(), tx, r.Y + ui.U(56), tw, Style.Dim, 1);
        }
        else if (side < 0) ui.TextFit($"{C.S.Gold:0} CROWNS", tx, r.Y + ui.U(44), tw, Kit.Gilt, 1);
        int y = r.Y + head;
        if (flavour != "") { ui.TextFit(flavour, r.X + ui.U(10), y - ui.U(4), r.Width - ui.U(20), C.GrudgeOf(e) is { Length: > 0 } ? Style.Bad : Kit.Parchment, 1); y += flavourH; }
        foreach (var b in blocks)
        {
            var row = new Rectangle(r.X + ui.U(6), y, r.Width - ui.U(12), rowH - ui.U(2));
            int bi = encBlocks.IndexOf(b);
            if (ui.Hover(row)) encHover = bi;
            if (encHover == bi) ui.Rect(row, (side < 0 ? Style.Friendly : Style.Foe) * 0.18f);
            var k = b.K;
            ui.Portrait(k.Type, new Rectangle(row.X + ui.U(2), row.Y, ui.U(24), ui.U(24)), palette: side < 0 ? C.PlayerPalette : Style.PaletteOf(C.SideOf(e)));
            string label = side < 0 ? k.Name.ToUpperInvariant() : $"{k.Men} {k.Type.Name.ToUpperInvariant()}";
            ui.TextFit(label, row.X + ui.U(32), row.Y + ui.U(3), row.Width - ui.U(80), Style.Text, 1);
            string sub = side < 0 ? $"{k.Men} MEN" + (k.Wounded > 0 ? $" +{k.Wounded} HURT" : "")
                : Rules.RoleOf(k) == "casters" && C.CourtMageOf(e) is Character mage && mage.CompanyId == k.Id ? $"LED BY {mage.Name.ToUpperInvariant()}" : "";
            if (sub != "") ui.TextFit(sub, row.X + ui.U(32), row.Y + ui.U(14), row.Width - ui.U(80), sub.StartsWith("LED") ? Style.Aura : Style.Dim, 1);
            ui.TextR($"TIER {Style.Numerals[k.Type.Tier]}", row.Right - ui.U(4), row.Y + ui.U(8), Style.Dim, 1);
            y += rowH;
        }
        if (extra > 0) { ui.Text($"AND {all.Count - blocks.Count} MORE COMPANIES", r.X + ui.U(10), y + ui.U(2), Style.Dim, 1); y += extra; }
        if (lord) LordButtons(new Rectangle(r.X + ui.U(8), y + ui.U(4), r.Width - ui.U(16), ui.U(28)), e);
        return r;
    }

    /// The one line under the foe's name: he comes for you (a grudge), or how he fights (his nature).
    string FoeLine(Party e)
    {
        if (C.GrudgeOf(e) is { Length: > 0 } grudge) return grudge.ToUpperInvariant();
        if (e.Kind == PartyKind.Lord && C.CharacterOf(e) is Character ch && Nature.Line(ch.Traits) is { Length: > 0 } how) return how.ToUpperInvariant();
        return "";
    }

    /// Against a lord: meet him in single combat, or talk before steel.
    void LordButtons(Rectangle r, Party e)
    {
        bool talk = C.CharacterOf(e) is { Kind: "lord" };
        int bw = talk ? (r.Width - ui.U(8)) / 2 : r.Width;
        var duel = new Rectangle(r.X, r.Y, bw, r.Height);
        string? cant = C.CannotChallenge(e);
        if (ui.Button(duel, "CHALLENGE", cant == null)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareDuel(e)); return; }
        if (ui.Hover(duel)) note = cant ?? "MEET HIS LORD IN SINGLE COMBAT. WIN AND HIS MEN START THE BATTLE SHAKEN.";
        if (!talk) return;
        var tb = new Rectangle(r.Right - bw, r.Y, bw, r.Height);
        if (ui.Button(tb, "TALK")) OpenTalk(C.CharacterOf(e)!, PanelKind.Encounter);
        if (ui.Hover(tb)) note = "SPEAK WITH HIM BEFORE THE FIGHT: DEMAND HE YIELD, SUE FOR PEACE, OR INSULT HIM.";
    }

    /// Across the top between the plates: the field's name and weather, the balance of strength, the odds in words, and the
    /// notes on the ground, the Tide and the spells.
    void StrengthStrip(Rectangle r, Party e, TerrainParams g, FieldLayout layout, Weather w, TownLook l)
    {
        int cx = r.Center.X;
        ui.Ribbon(cx, r.Y + ui.U(4), C.BattleName(C.Player.Pos).ToUpperInvariant(), Kit.Mul(Style.Foe, 0.66f));
        string hour = l.Night ? "NIGHT" : l.Dusk ? "DUSK" : "DAY";
        string sky = w.Snow ? "SNOW" : w.Rain >= 0.3f ? "RAIN" : w.Fog > 0.3f ? "FOG" : "CLEAR";
        string soil = g.Wet > 0.5f ? "SODDEN GROUND" : "DRY GROUND";
        int y = r.Y + ui.U(34);
        TextOnDark($"{hour}  ·  {sky}  ·  {soil}", cx, y, Style.Dim, 1);
        y += ui.U(18);
        var f = C.Player.Men > 0 ? Forecast(e) : (0, 0, 0, 0, 1); // the bar weighs what auto-resolve weighs, so it agrees with the words
        float me = f.Mine, them = f.Theirs, win = f.Win;
        var bar = new Rectangle(r.X + ui.U(50), y + ui.U(4), r.Width - ui.U(100), ui.U(10));
        ui.TugBar(bar, me / MathF.Max(1, me + them), Style.Friendly, Style.Foe);
        TextOnDark($"{C.Player.Men}", bar.X - ui.U(26), y + ui.U(2), Style.Friendly, 1);
        TextOnDark($"{e.Men}", bar.Right + ui.U(26), y + ui.U(2), Style.Foe, 1);
        y += ui.U(22);
        string word = win >= 0.9f ? "THEY ARE FAR WEAKER" : win >= 0.6f ? "YOU HAVE THE EDGE" : win > 0.4f ? "AN EVEN FIGHT" : win > 0.1f ? "THEY HAVE THE EDGE" : "THEY ARE FAR STRONGER";
        TextOnDark(word, cx, y, win >= 0.6f ? Style.Ok : win > 0.4f ? Style.Warn : Style.Bad, 2);
        y += ui.U(24);
        TextOnDark(C.GroundNote(e), cx, y, Kit.Gilt, 1);
        string tide = C.TideNote(e.Pos), spells = C.SpellNote(e);
        if (tide != "") { y += ui.U(14); TextOnDark(ui.Fit(tide, r.Width), cx, y, TideColour(C.TideAt(e.Pos).Tradition), 1); }
        if (spells is { Length: > 0 }) { y += ui.U(14); TextOnDark(ui.Fit(spells.ToUpperInvariant(), r.Width), cx, y, Style.Dim, 1); }
    }

    /// Centred text on a soft dark band, so it reads over sky or field.
    void TextOnDark(string s, int cx, int y, Color col, int scale)
    {
        int w = ui.TW(s, scale) + ui.U(12), h = 7 * scale * ui.S + ui.U(6);
        ui.Rect(new Rectangle(cx - w / 2, y - ui.U(3), w, h), Color.Black * 0.45f);
        ui.TextC(s, cx, y, col, scale);
    }

    /// The card of the company under the mouse, beside its block.
    void BlockCard(EncBlock b)
    {
        var k = b.K;
        var lines = new List<(string, Color)>
        {
            (b.Side < 0 ? k.Name.ToUpperInvariant() : k.Type.Name.ToUpperInvariant(), b.Side < 0 ? Style.Friendly : Style.Foe),
            ($"{k.Men} MEN, TIER {Style.Numerals[k.Type.Tier]}" + (k.Wounded > 0 ? $", {k.Wounded} HURT" : ""), Style.Text),
        };
        foreach (var s in k.Stacks.Where(s => s.Men > 0 && s.Type != k.Type).Take(3)) lines.Add(($"  {s.Men} {s.Type.Name.ToUpperInvariant()}", Style.Dim));
        if (k.Type.Mount != null) lines.Add(("MOUNTED", Style.Dim));
        if (b.Side < 0 && k.BattleHonours.Count > 0) lines.Add(($"{k.BattleHonours.Count} BATTLE HONOURS", Kit.Gilt));
        int w = lines.Max(x => ui.TW(x.Item1, 1)) + ui.U(20), h = lines.Count * ui.U(13) + ui.U(12);
        int x = b.Side < 0 ? b.Box.Right + ui.U(8) : b.Box.X - ui.U(8) - w, y = b.Box.Y - ui.U(4);
        x = Math.Clamp(x, ui.U(4), ui.W - w - ui.U(4));
        var r = new Rectangle(x, y, w, h);
        ui.PlateBody(r, b.Side < 0 ? Style.Friendly : Style.Foe, 1, 0.95f);
        for (int i = 0; i < lines.Count; i++) ui.Text(lines[i].Item1, r.X + ui.U(10), r.Y + ui.U(7) + i * ui.U(13), lines[i].Item2, 1);
    }

    // ---------- the choices ----------

    /// The bar along the bottom: auto-resolve with its forecast, ATTACK large in the middle, retreat and pay with what they cost.
    void ChoiceBar(Party e, Rectangle foePlate)
    {
        var bar = new Rectangle(0, ui.H - ui.U(78), ui.W, ui.U(78));
        ui.VGrad(bar, Kit.IronDark * 0.6f, Kit.IronDark, 2);
        ui.Rect(new Rectangle(0, bar.Y, ui.W, ui.U(1)), Kit.Mul(Kit.Gilt, 0.6f));
        if (note != "") TextOnDark(note, ui.W / 2, bar.Y - ui.U(18), Style.Warn, 1);
        int gap = ui.U(18), aw = ui.U(220), sw = Math.Min(ui.U(170), (ui.W - aw - ui.U(80) - 3 * gap) / 3), autoW = sw + ui.U(60);
        int total = autoW + aw + 2 * sw + 3 * gap, x = (ui.W - total) / 2, by = bar.Y + ui.U(14);
        bool fit = C.Player.Men > 0;
        // AUTO-RESOLVE, with what it would likely bring.
        var auto = new Rectangle(x, by, autoW, ui.U(32));
        var (win, few, many, _, _) = fit ? Forecast(e) : (0, 0, 0, 0, 1);
        string winWord = win >= 0.95f ? "A SURE WIN" : win <= 0.05f ? "A SURE LOSS" : $"WIN {MathF.Round(win * 10):0} IN 10";
        string fall = many == 0 ? "NONE OF YOURS FALL" : few == many ? $"{many} OF YOURS FALL" : $"{few}-{many} OF YOURS FALL";
        if (ui.Button(auto, "AUTO-RESOLVE", fit)) { C.AutoResolveEncounter(); ShowResults(); return; } // Encounter is cleared now
        ui.TextC($"{winWord}, {fall}", auto.Center.X, auto.Bottom + ui.U(8), win >= 0.7f ? Style.Ok : win >= 0.4f ? Style.Warn : Style.Bad, 1);
        if (ui.Hover(auto)) note = "YOUR SERGEANTS FIGHT IT WITHOUT YOU. THE FORECAST IS FROM FORTY TRIES ON PAPER.";
        x = auto.Right + gap;
        // ATTACK: the big one.
        var atk = new Rectangle(x, by - ui.U(6), aw, ui.U(44));
        if (fit) ui.Rect(Kit.Inset(atk, -ui.U(3)), Kit.Gilt * (0.25f + 0.15f * MathF.Sin((Ui.Instant ? 0 : Environment.TickCount64 / 1000f) * 3)));
        if (ui.Button(atk, "ATTACK", fit, active: fit)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareBattle(e)); return; }
        ui.TextC("LEAD THEM YOURSELF", atk.Center.X, atk.Bottom + ui.U(4), Style.Dim, 1);
        x = atk.Right + gap;
        // RETREAT: a rearguard pays, or you cannot.
        var ret = new Rectangle(x, by, sw, ui.U(32));
        bool canRetreat = C.CanRetreat(out string why);
        if (ui.Button(ret, "RETREAT", canRetreat)) { C.Retreat(); panel = PanelKind.None; return; }
        ui.TextFitC(canRetreat ? "A REARGUARD PAYS" : why, ret.Center.X, ret.Bottom + ui.U(8), sw + gap, canRetreat ? Style.Dim : Style.Bad, 1);
        if (canRetreat && ui.Hover(ret)) note = "YOUR SLOWEST COMPANY COVERS THE RETREAT AND LOSES SOME MEN.";
        x = ret.Right + gap;
        // PAY or PARLEY.
        var pay = new Rectangle(x, by, sw, ui.U(32));
        int price = C.ParleyPrice(e);
        if (ui.Button(pay, price > 0 ? $"PAY {price}" : "PARLEY", price > 0 && C.S.Gold >= price)) { if (C.Parley()) panel = PanelKind.None; return; }
        string payLine = price < 0 ? (e.Kind == PartyKind.Monster ? $"{Campaign.Plural(e.Breed).ToUpperInvariant()} DO NOT BARGAIN" : "LORDS AT WAR DO NOT BARGAIN")
            : C.S.Gold >= price ? "THEY LET YOU BE" : "NOT ENOUGH CROWNS";
        ui.TextFitC(payLine, pay.Center.X, pay.Bottom + ui.U(8), sw + gap, price > 0 && C.S.Gold >= price ? Style.Dim : Style.Bad, 1);
    }
}
