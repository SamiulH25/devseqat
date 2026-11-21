using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the camp (docs/pre-demo/COMPANY.md C5, key C). Where the company is between battles: the country at the hour it
/// is, the tents, a fire for each company with its banner and its named men standing round it (the battle's own puppets and
/// faces, `CampStage`), the hurt ones on pallets by the surgeon's tent. Click a man for his page. Drawing and input only; what
/// the camp knows is in Campaign/Camp.cs.
partial class MapScreen
{
    /// Fires on one screen; more companies page with the arrows on the bar.
    const int CampPerPage = 4;
    CampStage? campStage;
    readonly MeshKit3D? campKit;
    /// The men drawn this frame and where their fires are (laid out by `CampLayout`, the same in the draw and the UI).
    readonly List<(Unit U, Soldier M, CompanyRecord K)> campMen = new();
    readonly List<(CompanyRecord K, Vector3 Fire)> campFires = new();
    int campPage;
    /// The man whose page is open (null: the camp).
    Soldier? campMan;
    CompanyRecord? campManOf;
    long campTick;

    /// The art pixel of the camp (like the street's: about 240 to the screen's height).
    int CampPixel => Math.Max(2, gd.Viewport.Height / 240);

    float CampLight(TownLook l) => l.Night ? 0.52f : l.Dusk ? 0.78f : 1f;

    /// The look of the country you camp in, as a town's is read (`LookOf`): its people's land, the hour, snow.
    TownLook CampLook()
    {
        var prov = C.Map.ProvinceAt(C.Player.Pos);
        string people = prov.Faction >= 0 && prov.Faction < C.S.Factions.Count ? C.S.Factions[prov.Faction].Culture : "azure";
        float hour = (float)(C.S.Hours % 24);
        bool night = hour is >= 20 or < 5;
        bool snow = C.Winter ? !Lands.Of(prov.Kind).Hot : prov.Kind == "Tundra" && C.SeasonIndex != 1;
        return new TownLook(people, "timber", prov.Kind, "", snow, night, !night && hour >= 17);
    }

    /// Lay out the fires of this page and the men round them. Fit men stand in an arc behind their fire, facing it (so their faces
    /// are to you); after a lost field they stand farther off.
    void CampLayout(int w, int h)
    {
        campStage!.Frame(w, h);
        campMen.Clear(); campFires.Clear();
        var all = C.CampFires();
        int pages = Math.Max(1, (all.Count + CampPerPage - 1) / CampPerPage);
        campPage = Math.Clamp(campPage, 0, pages - 1);
        var shown = all.Skip(campPage * CampPerPage).Take(CampPerPage).ToList();
        var mood = C.MoodOfCamp();
        int pal = Style.PaletteOf(Math.Max(0, C.Captain.Contract)), index = 1;
        for (int i = 0; i < shown.Count; i++)
        {
            var k = shown[i];
            var fire = FireSpot(i, shown.Count);
            campFires.Add((k, fire));
            var (fit, _) = Campaign.AtFire(k);
            int n = Math.Min(6, fit.Count);
            float r = mood == Campaign.CampMood.Lost ? 2.1f : 1.55f;
            for (int j = 0; j < n; j++)
            {
                var m = fit[j];
                // From the left of the fire round behind it to the right; a lone man stands just behind.
                float a = MathF.PI * 0.9f + MathF.PI * 1.2f * (j + 1) / (n + 1);
                var pos = new Vector2(fire.X + MathF.Cos(a) * r, fire.Z + MathF.Sin(a) * r * 0.75f);
                float face = MathF.Atan2(fire.Z - pos.Y, fire.X - pos.X) * 0.6f + MathHelper.PiOver2 * 0.4f; // half to the fire, half to you
                var t = Troops.Get(Troops.Has(m.Troop) ? m.Troop : "levies");
                var u = new Unit
                {
                    Index = index++, SoldierId = m.Id, Type = t, Palette = pal, Coat = m.Id % Style.Coats.Length, Hp = t.MaxHp, Anim = Ease.Hash(m.Id) * 2,
                    Pos = pos, Facing = face, ViewFacing = face, Heading = face, ViewHeading = face, EnemyDist = float.MaxValue,
                };
                campMen.Add((u, m, k));
            }
        }
    }

    /// Where the `i`th of `n` fires burns: two near, wide apart, and the others farther back between them, so the field has depth
    /// and no group stands in front of another.
    static Vector3 FireSpot(int i, int n) => (n, i) switch
    {
        (1, _) => new Vector3(0, 0, 2f),
        (2, _) => new Vector3(i == 0 ? -3.2f : 3.2f, 0, i == 0 ? 2.6f : 1.4f),
        (3, 2) => new Vector3(0, 0, -2.6f),
        (_, 0) => new Vector3(-4.4f, 0, 4.2f),
        (_, 1) => new Vector3(4.4f, 0, 4.2f),
        (_, 2) => new Vector3(-1.7f, 0, -2.8f),
        _ => new Vector3(1.9f, 0, -3.4f),
    };

    /// Draw the men into their texture before the frame's sprites (from `Draw`).
    void PrepareCamp()
    {
        if (panel != PanelKind.Camp || bare) return;
        campStage ??= new CampStage(gd, art, shading, campKit, () => lifeQuality);
        long now = Environment.TickCount64;
        float dt = campTick == 0 ? 0 : Math.Min(0.1f, (now - campTick) / 1000f);
        campTick = now;
        int w = gd.Viewport.Width, h = gd.Viewport.Height;
        CampLayout(w, h);
        campStage.Render(w, h, campMen.Select(x => x.U).ToList(), CampPixel, CampLight(CampLook()), C.MoodOfCamp() == Campaign.CampMood.Won, dt);
    }

    void CampPanel()
    {
        modalIn = 1;
        ui.Block(new Rectangle(0, 0, ui.W, ui.H));
        if (campStage == null) { ui.Rect(new Rectangle(0, 0, ui.W, ui.H), Kit.IronDark); return; } // the first frame: the men are drawn from the next
        var l = CampLook();
        var c = ColoursOf(l);
        var mood = C.MoodOfCamp();
        float t = Ui.Instant ? 3.2f : Environment.TickCount64 / 1000f;
        var bar = new Rectangle(0, ui.H - ui.U(48), ui.W, ui.U(48));
        int s0 = ui.S;
        ui.S = CampPixel;
        try { CampScene(l, c, mood, t); }
        finally { ui.S = s0; }
        if (campStage.Texture != null) ui.Sb.Draw(campStage.Texture, new Rectangle(0, 0, ui.W, ui.H), Color.White);
        ui.S = CampPixel;
        try { foreach (var (fk, fire) in campFires.OrderBy(f => f.Fire.Z)) Fire(campStage.Project(fire), campStage.PerMetre(fire.Z), mood, l, t, fk.Id); }
        finally { ui.S = s0; }
        ui.S = CampPixel;
        try { CampGear(l, c, back: false); }
        finally { ui.S = s0; }
        if (l.Snow) { ui.S = CampPixel; try { Snowfall(new Rectangle(0, 0, ui.W, ui.H), t, l.Night); } finally { ui.S = s0; } }
        var hover = campMan == null ? CampMarks() : null;
        CampPlate(new Rectangle(ui.U(10), ui.U(10), Math.Min(ui.U(420), ui.W / 2), ui.U(78)), mood);
        CampBar(bar);
        if (hover is (Soldier m, CompanyRecord k) && ui.Clicked(ManBox(m))) { campMan = m; campManOf = k; note = ""; }
        if (campMan != null) ManPage();
    }

    // ---------- the scene ----------

    /// The sky, the country, the field, the tents, the banners and the surgeon's: everything behind the men.
    void CampScene(TownLook l, TownColours c, Campaign.CampMood mood, float t)
    {
        var st = campStage!;
        hurtSpots.Clear(); // laid again with the surgeon's pallets
        int seed = C.Map.ProvinceAt(C.Player.Pos).Id;
        var full = new Rectangle(0, 0, ui.W, ui.H);
        int horizon = (int)st.Project(new Vector3(0, 0, -60)).Y;
        var sky = new Rectangle(0, 0, ui.W, horizon);
        ui.VGrad(sky, c.SkyTop, c.SkyLow, 2);
        if (l.Night) ui.Stars(new Rectangle(0, 0, ui.W, horizon * 2 / 3), t, 70);
        else Clouds(sky, l, c, t, seed);
        Orb(sky, l, 0.78f);
        snowCap = l.Snow ? c.Snow : Color.Transparent;
        var far = Color.Lerp(Color.Lerp(c.Far, c.SkyTop, 0.5f), c.SkyLow, 0.35f);
        if (l.Country != "Coast") ui.Hills(full, horizon - ui.U(40), ui.U(14), 1.6f + seed % 5, far);
        Country(full, l, c, horizon, new Random(seed * 7 + 3));
        // The field: grass from the horizon down to your feet, darker near, and the snow over it in winter.
        Color grass = l.Snow ? c.Snow : Lands.Of(l.Country).Hot ? Color.Lerp(c.Near, new Color(196, 168, 118), CampLight(l) - 0.2f)
            : Color.Lerp(c.Near, new Color(96, 120, 64), l.Night ? 0.35f : l.Dusk ? 0.6f : 0.9f);
        ui.VGrad(new Rectangle(0, horizon, ui.W, ui.H - horizon), Color.Lerp(grass, c.Far, 0.35f), Kit.Mul(grass, 0.78f), 2);
        var rng = new Random(seed * 31 + 5);
        for (int i = 0; i < 70; i++) // tufts, thicker near
        {
            float d = rng.NextSingle();
            int y = horizon + (int)((ui.H - horizon) * d * d + ui.U(2)), x = rng.Next(ui.W);
            ui.Rect(new Rectangle(x, y, ui.U(2 + (int)(d * 3)), ui.U(1)), Kit.Mul(grass, l.Snow ? 0.92f : 0.82f));
        }
        // The tents in a row behind the fires, each company's in its cloth; plain canvas between them; a wagon at each end.
        var hd = Style.Sides[Style.PaletteOf(Math.Max(0, C.Captain.Contract))];
        float back = -7.5f, hw = st.HalfWidth(back);
        var canvas = Color.Lerp(c.Near, new Color(214, 200, 170), CampLight(l) * 0.85f);
        for (float x = -hw + 1.6f; x < hw - 1; x += 3.4f + (float)rng.NextDouble())
            Tent(st.Project(new Vector3(x, 0, back - 1.4f)), st.PerMetre(back - 1.4f), Kit.Mul(canvas, 0.86f), l);
        foreach (var (k, fire) in campFires)
            Tent(st.Project(new Vector3(fire.X + 0.4f, 0, back)), st.PerMetre(back), Color.Lerp(canvas, hd.Cloth, 0.55f), l);
        foreach (var x in new[] { -st.HalfWidth(-4.5f) + 2.2f })
        {
            var p = st.Project(new Vector3(x, 0, -4.5f));
            int sc = ui.S; ui.S = Math.Max(1, (int)(st.PerMetre(-4.5f) / 14f));
            try { Cart((int)p.X - ui.U(14), (int)p.Y, l, l.Night ? new Color(48, 34, 30) : new Color(112, 78, 50)); }
            finally { ui.S = sc; }
        }
        Surgeon(l, c, hd);
        foreach (var (k, fire) in campFires) // the smoke goes up behind the men
            if (mood != Campaign.CampMood.Lost) Smoke((int)st.Project(fire).X, (int)st.Project(fire + new Vector3(0, 0.9f, 0)).Y, t, l.Night);
        // Each company's banner by its fire, on the company's charge.
        foreach (var (k, fire) in campFires)
        {
            var foot = new Vector3(fire.X + 1.05f, 0, fire.Z - 1.1f);
            var p = st.Project(foot);
            float pm = st.PerMetre(foot.Z);
            int top = (int)(p.Y - 3.2f * pm), bw = Math.Max(ui.U(8), (int)(0.75f * pm)) / ui.S * ui.S;
            ui.Rect(new Rectangle((int)p.X - ui.U(1), top, ui.U(2), (int)p.Y - top), l.Night ? new Color(44, 32, 28) : new Color(96, 68, 44));
            ui.Banner((int)p.X + bw / 2 + ui.U(2), top + ui.U(2), bw, (int)(1.15f * pm), hd with { Charge = (Emblem)k.Banner }, t * 1.3f + k.Id);
        }
        CampGear(l, c, back: true);
        snowCap = Color.Transparent;
    }

    /// A ridge tent seen end on: a canvas triangle, its door flap open and dark, the pole's tip and two guy ropes.
    void Tent(Vector2 foot, float pm, Color cloth, TownLook l)
    {
        int w = (int)(2.6f * pm), h = (int)(2.0f * pm), s = ui.U(1), x0 = (int)foot.X - w / 2, y0 = (int)foot.Y;
        for (int row = 0; row < h; row += s)
        {
            int half = (int)(w / 2f * row / h);
            var col = row < s * 2 ? Kit.Mul(cloth, 1.08f) : cloth;
            ui.Rect(new Rectangle((int)foot.X - half, y0 - h + row, half, s), Kit.Mul(col, 0.86f)); // the shaded side
            ui.Rect(new Rectangle((int)foot.X, y0 - h + row, half, s), col);
            int door = (int)(w * 0.17f * row / h);
            if (row > h / 3) ui.Rect(new Rectangle((int)foot.X - door / 2, y0 - h + row, door, s), l.Night ? new Color(18, 14, 18) : new Color(46, 36, 32));
        }
        if (snowCap.A > 0) for (int row = 0; row < h / 4; row += s) ui.Rect(new Rectangle((int)foot.X - (int)(w / 2f * row / h), y0 - h + row, (int)(w * (float)row / h), s), snowCap);
        ui.Rect(new Rectangle((int)foot.X - s / 2, y0 - h - ui.U(3), s, ui.U(3)), l.Night ? new Color(40, 30, 26) : new Color(90, 64, 42));
        var rope = Kit.Mul(cloth, 0.6f) * 0.7f;
        for (int i = 0; i < 6; i++) { ui.Rect(new Rectangle(x0 - ui.U(2) - i * ui.U(1), y0 - h / 2 + i * h / 12, s, s), rope); ui.Rect(new Rectangle(x0 + w + ui.U(1) + i * ui.U(1), y0 - h / 2 + i * h / 12, s, s), rope); }
    }

    /// The surgeon's tent at the back right with its red cross, and a pallet before it for each hurt named man (up to six).
    void Surgeon(TownLook l, TownColours c, Heraldry hd)
    {
        var st = campStage!;
        var hurt = C.Player.Companies.SelectMany(k => Campaign.AtFire(k).Hurt.Select(m => (m, k))).Take(6).ToList();
        if (hurt.Count == 0 && C.Player.Wounded == 0) return;
        float z = -5.6f, x = st.HalfWidth(z) - 1.9f;
        var canvas = Color.Lerp(c.Near, new Color(226, 218, 200), CampLight(l) * 0.85f);
        var p = st.Project(new Vector3(x, 0, z - 1.2f));
        float pm = st.PerMetre(z - 1.2f);
        Tent(p, pm * 1.15f, canvas, l);
        int cs = Math.Max(ui.U(2), (int)(pm * 0.16f));
        ui.Rect(new Rectangle((int)p.X - cs / 2, (int)(p.Y - pm * 1.75f), cs, cs * 3), new Color(170, 40, 36));
        ui.Rect(new Rectangle((int)p.X - cs * 3 / 2, (int)(p.Y - pm * 1.75f) + cs, cs * 3, cs), new Color(170, 40, 36));
        for (int i = 0; i < hurt.Count; i++)
        {
            var (m, k) = hurt[i];
            var at = st.Project(new Vector3(x - 1.6f + i % 3 * 1.05f, 0, z + 0.9f + i / 3 * 1.0f));
            float q = st.PerMetre(z + 0.9f + i / 3 * 1.0f);
            int pw = (int)(1.2f * q), ph = Math.Max(ui.U(4), (int)(0.34f * q));
            var pallet = new Rectangle((int)at.X - pw / 2, (int)at.Y - ph, pw, ph);
            ui.Rect(new Rectangle(pallet.X, pallet.Bottom - ui.U(1), pallet.Width, ui.U(1)), Color.Black * 0.3f);
            ui.Rect(pallet, Kit.Mul(hd.Cloth, CampLight(l) * 0.9f));
            ui.Rect(new Rectangle(pallet.X, pallet.Y, pallet.Width, ui.U(1)), Kit.Mul(hd.Cloth, CampLight(l) * 1.15f));
            ui.Rect(new Rectangle(pallet.X - ui.U(1), pallet.Y - ui.U(1), pallet.Width / 4, ph / 2 + ui.U(1)), Kit.Mul(canvas, 0.95f)); // the pillow
            hurtSpots.Add((m, k, new Rectangle(pallet.X - ui.U(2), pallet.Y - ph * 2, pw / 3, ph * 2)));
        }
    }

    readonly List<(Soldier M, CompanyRecord K, Rectangle Head)> hurtSpots = new();

    /// A fire at `foot` (`pm` pixels a metre there): trodden earth, a ring of stones, logs, flames that move and their light;
    /// after a lost field it is embers. Drawn over the men, so those behind it stand in its light.
    void Fire(Vector2 foot, float pm, Campaign.CampMood mood, TownLook l, float t, int seed)
    {
        int x = (int)foot.X, y = (int)foot.Y, s = Math.Max(1, ui.U(1));
        float glow = l.Night ? 1f : l.Dusk ? 0.6f : 0.25f, hgt = mood == Campaign.CampMood.Lost ? 0.28f : 0.62f;
        var warm = new Color(255, 170, 80);
        for (int i = 0; i < 10; i++) ui.Disc(new Vector2(x, y - pm * 0.3f), pm * (2.6f - i * 0.22f), warm * (0.016f * glow));
        int rx = (int)(0.5f * pm), ry = Math.Max(s * 2, (int)(0.14f * pm));
        for (int i = 0; i < 12; i++) // the stones
        {
            float a = i / 12f * MathHelper.TwoPi;
            ui.Rect(new Rectangle(x + (int)(MathF.Cos(a) * rx) - s, y + (int)(MathF.Sin(a) * ry) - s, s * 3, s * 2), l.Night ? new Color(70, 60, 64) : new Color(120, 114, 108));
        }
        var log = l.Night ? new Color(46, 30, 24) : new Color(84, 56, 36);
        ui.Rect(new Rectangle(x - rx * 2 / 3, y - s * 2, rx * 4 / 3, s * 2), log);
        ui.Rect(new Rectangle(x - rx / 3, y - s * 4, rx * 2 / 3, s * 2), Kit.Mul(log, 1.2f));
        int tongues = mood == Campaign.CampMood.Lost ? 2 : 4;
        for (int i = 0; i < tongues; i++)
        {
            float f = 0.75f + 0.25f * MathF.Sin(t * (5.3f + i * 1.7f) + seed * 1.3f + i * 2.1f);
            int fh = (int)(hgt * pm * f * (i == 1 || i == 2 ? 1 : 0.7f)), fx = x + (int)((i - (tongues - 1) / 2f) * rx * 0.42f);
            for (int row = 0; row < fh; row += s)
            {
                float k = row / (float)Math.Max(1, fh);
                int half = (int)((1 - k) * rx * 0.3f) + s;
                var col = k < 0.35f ? new Color(255, 236, 170) : k < 0.7f ? new Color(250, 160, 60) : new Color(206, 76, 36);
                int sway = (int)(MathF.Sin(t * 7 + i + row * 0.2f) * k * s * 2);
                ui.Rect(new Rectangle(fx - half + sway, y - s * 3 - row, half * 2, s), col);
            }
        }
    }

    /// The camp's gear: barrels, sacks, a cooking pot on its tripod and a saddle on a log in the near ground (drawn over the men's
    /// texture, nearer than any of them), a rack of spears by the tents (`back`: drawn before the men).
    void CampGear(TownLook l, TownColours c, bool back)
    {
        var st = campStage!;
        float lit = CampLight(l);
        Color wood = Color.Lerp(c.Near, new Color(124, 86, 54), lit), iron = Color.Lerp(c.Near, new Color(84, 84, 92), lit),
            sack = Color.Lerp(c.Near, new Color(186, 164, 120), lit), leather = Color.Lerp(c.Near, new Color(110, 64, 40), lit);
        int s = Math.Max(1, ui.U(1));
        void Shadow(Vector2 p, float w) => ui.Rect(new Rectangle((int)(p.X - w / 2), (int)p.Y - s, (int)w, s * 2), Color.Black * 0.25f);
        Color line = Kit.Mul(c.Near, 0.55f);
        // A shape as rows (each row's half width from its height 0-1), outlined, lit from the left.
        void Blob(int cx, int bottom, int w, int h, Color col, Func<float, float> width)
        {
            for (int row = 0; row < h; row += s)
            {
                float k = row / (float)h;
                int half = Math.Max(s, (int)(w / 2f * width(k)));
                int y = bottom - h + row;
                ui.Rect(new Rectangle(cx - half - s, y, half * 2 + s * 2, s), line);
                ui.Rect(new Rectangle(cx - half, y, half * 2, s), col);
                ui.Rect(new Rectangle(cx + half / 3, y, half * 2 / 3, s), Kit.Mul(col, 0.82f)); // the shade side
            }
            ui.Rect(new Rectangle(cx - (int)(w / 2f * width(0)), bottom - h - s, (int)(w * width(0)), s), line);
        }
        foreach (var (kind, x, z) in new[] { ("spears", -6.4f, -6.2f), ("barrels", -3.9f, 9.0f), ("pot", -0.2f, 9.6f), ("sacks", 2.2f, 9.2f), ("log", -2.0f, 10.4f) })
        {
            var p = st.Project(new Vector3(x, 0, z));
            float pm = st.PerMetre(z);
            if (p.X < -pm * 2 || p.X > ui.W + pm * 2 || (z < 0) != back) continue; // what stands behind the men is drawn before them
            int X = (int)p.X, Y = (int)p.Y;
            switch (kind)
            {
                case "barrels":
                    for (int i = 0; i < 2; i++)
                    {
                        int bw = (int)(0.5f * pm), bh = (int)(0.72f * pm), bx = X + (int)((i - 0.5f) * 0.58f * pm);
                        Shadow(new Vector2(bx, Y), bw * 1.3f);
                        Blob(bx, Y, bw, bh, wood, k => 0.86f + 0.14f * MathF.Sin(k * MathF.PI)); // bellied
                        foreach (float k in new[] { 0.2f, 0.78f }) ui.Rect(new Rectangle(bx - bw / 2, Y - (int)(bh * k) - s, bw, s * 2), iron);
                        ui.Rect(new Rectangle(bx - bw * 43 / 100, Y - bh, bw * 86 / 100, s * 2), Kit.Mul(wood, 0.7f)); // the head, in shadow
                    }
                    break;
                case "pot":
                {
                    int leg = (int)(1.1f * pm), pw = (int)(0.5f * pm);
                    ui.Rect(new Rectangle(X - (int)(0.5f * pm), Y - s * 2, (int)pm, s * 2), Color.Black * 0.3f); // the cold hearth under it
                    for (int k = 0; k < leg; k += s) { ui.Rect(new Rectangle(X - (int)(0.45f * pm) + k * 4 / 10, Y - k, s * 2, s), Kit.Mul(wood, 0.8f)); ui.Rect(new Rectangle(X + (int)(0.45f * pm) - k * 4 / 10, Y - k, s * 2, s), wood); }
                    ui.Rect(new Rectangle(X - s / 2, Y - leg, s, (int)(0.42f * pm)), iron); // the chain
                    Blob(X, Y - leg + (int)(0.8f * pm), pw, (int)(0.38f * pm), Kit.Mul(iron, 0.75f), k => 0.75f + 0.25f * MathF.Sin(k * MathF.PI * 0.9f + 0.3f));
                    ui.Rect(new Rectangle(X - pw / 2 - s, Y - leg + (int)(0.42f * pm), pw + 2 * s, s * 2), iron); // the rim
                    break;
                }
                case "sacks":
                    foreach (var (dx, sh) in new[] { (-0.44f, 0.42f), (0.44f, 0.38f), (0f, 0.5f) })
                    {
                        int sw = (int)(0.5f * pm), hh = (int)(sh * pm), sx = X + (int)(dx * pm);
                        Shadow(new Vector2(sx, Y), sw * 1.2f);
                        Blob(sx, Y, sw, hh, sack, k => k < 0.14f ? 0.22f : k < 0.4f ? 0.55f + (k - 0.14f) * 1.7f : 1f); // a tied neck, round shoulders, a full body
                        ui.Rect(new Rectangle(sx - sw / 8, Y - hh + (int)(hh * 0.14f), sw / 4, s), Kit.Mul(sack, 0.6f)); // the cord
                    }
                    break;
                case "spears":
                {
                    int rh = (int)(0.9f * pm), rw = (int)(1.2f * pm);
                    for (int i = 0; i < 5; i++)
                    {
                        int sx = X - rw / 2 + s * 4 + i * (rw - s * 8) / 4, top = Y - (int)(2.3f * pm) + i % 2 * s * 3;
                        ui.Rect(new Rectangle(sx - s, top, s * 3, Y - top), line);
                        ui.Rect(new Rectangle(sx, top, s, Y - top), Kit.Mul(wood, 1.15f));
                        Blob(sx, top, s * 3, s * 5, Color.Lerp(c.Near, new Color(200, 204, 214), lit), k => k < 0.6f ? k / 0.6f : 1 - (k - 0.6f)); // the heads
                    }
                    ui.Rect(new Rectangle(X - rw / 2 - s, Y - rh - s, rw + s * 2, s * 4), line);
                    ui.Rect(new Rectangle(X - rw / 2, Y - rh, rw, s * 2), wood);
                    foreach (int px in new[] { X - rw / 2, X + rw / 2 - s * 2 }) { ui.Rect(new Rectangle(px - s, Y - rh, s * 4, rh), line); ui.Rect(new Rectangle(px, Y - rh, s * 2, rh), wood); }
                    break;
                }
                case "log": // a felled log the men sit on, a saddle thrown over one end
                {
                    int lw = (int)(1.7f * pm), lh = (int)(0.34f * pm);
                    var body = new Rectangle(X - lw / 2, Y - lh, lw, lh);
                    Shadow(new Vector2(X, Y), lw * 1.05f);
                    ui.Rect(new Rectangle(body.X - s, body.Y - s, body.Width + 2 * s, body.Height + 2 * s), line);
                    ui.Rect(body, Kit.Mul(wood, 0.85f));
                    ui.Rect(new Rectangle(body.X, body.Y, body.Width, s), Kit.Mul(wood, 1.1f));
                    for (int bx = body.X + s * 3; bx < body.Right - s * 6; bx += s * 7) ui.Rect(new Rectangle(bx, body.Y + s * 2 + bx % 3 * s, s * 4, s), Kit.Mul(wood, 0.65f)); // bark
                    Blob(body.X + lh / 2, body.Bottom, lh, lh, Kit.Mul(wood, 1.3f), k => 0.6f + 0.4f * MathF.Sin(k * MathF.PI)); // the cut end
                    ui.Rect(new Rectangle(body.X + lh / 2 - s, body.Bottom - lh / 2 - s, s * 2, s * 2), Kit.Mul(wood, 0.8f)); // its heart
                    int sx = body.Right - (int)(0.55f * pm);
                    Blob(sx, body.Y + s * 2, (int)(0.62f * pm), (int)(0.2f * pm), leather, k => k < 0.3f ? 0.5f + k : 1f); // the seat, its cantle up
                    ui.Rect(new Rectangle(sx - s, body.Y + s * 2, s * 3, (int)(0.32f * pm)), Kit.Mul(leather, 0.75f)); // the stirrup leather
                    ui.Rect(new Rectangle(sx - s * 2, body.Y + s * 2 + (int)(0.32f * pm), s * 5, s * 2), iron);
                    break;
                }
            }
        }
    }

    // ---------- the men's marks ----------

    /// Where a man stands on the screen: from the mark over his head to his feet.
    Rectangle ManBox(Soldier m)
    {
        foreach (var (u, mm, _) in campMen)
            if (mm == m)
            {
                var feet = campStage!.Project(new Vector3(u.Pos.X, 0, u.Pos.Y));
                var head = campStage.Project(new Vector3(u.Pos.X, 2.0f * Variety.Stature(u), u.Pos.Y));
                int w = (int)(campStage.PerMetre(u.Pos.Y) * 0.7f);
                return new Rectangle((int)feet.X - w / 2, (int)head.Y - ui.U(20), w, (int)(feet.Y - head.Y) + ui.U(20));
            }
        foreach (var (mm, _, r) in hurtSpots) if (mm == m) return r;
        return Rectangle.Empty;
    }

    /// A small face over each named man (and each hurt one); under the mouse his name and nature. Returns the man under it.
    (Soldier, CompanyRecord)? CampMarks()
    {
        (Soldier, CompanyRecord)? over = null;
        var marks = campMen.Select(x => (x.M, x.K, Hurt: false)).Concat(hurtSpots.Select(x => (x.M, x.K, Hurt: true))).ToList();
        foreach (var (m, k, hurt) in marks)
        {
            var box = ManBox(m);
            if (box.IsEmpty) continue;
            if (box.Contains(ui.Mouse) && ui.Mouse.Y < ui.H - ui.U(48)) over = (m, k);
        }
        foreach (var (m, k, hurt) in marks)
        {
            var box = ManBox(m);
            if (box.IsEmpty) continue;
            bool on = over?.Item1 == m;
            int fs = ui.U(16);
            var fb = new Rectangle(box.Center.X - fs / 2, hurt ? box.Y - fs / 2 : box.Y, fs, fs);
            ui.Rect(new Rectangle(fb.X - ui.U(1), fb.Y - ui.U(1), fb.Width + ui.U(2), fb.Height + ui.U(2)), on ? Kit.Gilt : Campaign.OfBanner(m) ? Kit.Mul(Kit.Gilt, 0.7f) : Kit.IronDark);
            ui.FaceHead(PortraitPainter.Of(m, k), fb);
            if (m.Avenging) ui.Rect(new Rectangle(fb.X, fb.Bottom - ui.U(2), fb.Width, ui.U(2)), Style.Warn);
            if (!on) continue;
            Ui.Hovered(box);
            string name = m.Name.ToUpperInvariant(), sub = $"{m.Nature.ToUpperInvariant()}{(m.Nature != "" ? ", " : "")}{Troops.Get(m.Troop).Name.ToUpperInvariant()}{(hurt ? ", HURT" : "")}";
            int w = Math.Max(ui.TW(name, 1), ui.TW(sub, 1)) + ui.U(12);
            var tag = new Rectangle(Math.Clamp(fb.Center.X - w / 2, ui.U(4), ui.W - w - ui.U(4)), fb.Y - ui.U(28), w, ui.U(25));
            ui.Rect(tag, Color.Black * 0.75f);
            ui.Frame(tag, Kit.Gilt * 0.7f, ui.U(1));
            ui.TextC(name, tag.Center.X, tag.Y + ui.U(4), Kit.Gilt, 1);
            ui.TextC(sub, tag.Center.X, tag.Y + ui.U(14), Style.Dim, 1);
        }
        return over;
    }

    // ---------- the plates ----------

    /// The camp's plate at the top left: where you lie, the day, how the men are.
    void CampPlate(Rectangle p, Campaign.CampMood mood)
    {
        ui.Rect(p, Kit.IronDark * 0.86f);
        ui.Frame(p, Kit.Mul(Kit.GiltDark, 0.8f), ui.U(1));
        int x = p.X + ui.U(12), y = p.Y + ui.U(8);
        ui.Text("THE CAMP", x, y, Style.Text, 3);
        var prov = C.Map.ProvinceAt(C.Player.Pos);
        ui.TextFit($"{prov.Title.ToUpperInvariant()}, DAY {C.Day}", x, y + ui.U(24), p.Width - ui.U(24), Kit.Gilt, 1);
        string line = mood switch
        {
            Campaign.CampMood.Won => "THE MEN DRINK TO THE DAY'S WORK.",
            Campaign.CampMood.Lost => "FEW FIRES TONIGHT. THE MEN SIT APART.",
            _ => "THE COMPANY RESTS.",
        };
        int named = C.Player.Companies.Sum(k => k.Roll.Count);
        ui.TextFit(line, x, y + ui.U(38), p.Width - ui.U(24), Kit.Mul(Kit.Parchment, 0.9f), 1);
        ui.TextFit($"{named} NAMED {(named == 1 ? "MAN" : "MEN")}, {C.Player.Wounded} WOUNDED. CLICK A MAN TO SEE HIM.", x, y + ui.U(50), p.Width - ui.U(24), Style.Dim, 1);
    }

    /// The bar along the bottom: the purse, the companies at these fires (arrows for more), rest, and the way out.
    void CampBar(Rectangle bar)
    {
        ui.VGrad(bar, new Color(30, 32, 40), Kit.IronDark, 1);
        ui.Rect(new Rectangle(bar.X, bar.Y, bar.Width, ui.U(1)), Kit.GiltDark);
        Purse(new Rectangle(bar.X, bar.Y, bar.Width, ui.U(48)));
        var leave = new Rectangle(bar.Right - ui.U(190), bar.Y + ui.U(10), ui.U(176), ui.U(28));
        int pages = Math.Max(1, (C.CampFires().Count + CampPerPage - 1) / CampPerPage);
        int mid = bar.X + ui.U(360), right = leave.X - ui.U(160);
        if (pages > 1)
        {
            if (ui.Button(new Rectangle(mid, bar.Y + ui.U(10), ui.U(28), ui.U(28)), "<", campPage > 0)) campPage--;
            if (ui.Button(new Rectangle(right - ui.U(36), bar.Y + ui.U(10), ui.U(28), ui.U(28)), ">", campPage < pages - 1)) campPage++;
        }
        string fires = string.Join("  .  ", campFires.Select(f => f.K.Name.ToUpperInvariant()));
        ui.TextFitC(fires, (mid + ui.U(36) + right - ui.U(36)) / 2f, bar.Y + ui.U(14), right - mid - ui.U(80), Kit.Mul(Kit.Parchment, 0.9f), 1);
        if (pages > 1) ui.TextC($"FIRES {campPage + 1} OF {pages}", (mid + right) / 2f, bar.Y + ui.U(28), Style.Dim, 1);
        if (ui.Button(new Rectangle(right, bar.Y + ui.U(10), ui.U(148), ui.U(28)), "REST A DAY", C.Encounter == null)) { C.Rest(24); note = ""; }
        if (ui.Gilded(leave, "LEAVE", ctl.Name(Act.Menu))) { panel = CameFrom(PanelKind.Camp); campMan = null; }
    }

    // ---------- a man's page ----------

    /// One named man, all of him: his face large, who he is, what he has done, whom he stands with, and what you can do for him.
    void ManPage()
    {
        var m = campMan!;
        var k = campManOf;
        if (k == null || !k.Roll.Contains(m)) { campMan = null; return; } // he left the roll (sent home, moved off)
        var r = Modal(760, 440, m.Name.ToUpperInvariant());
        var fb = new Rectangle(r.X + ui.U(24), r.Y + ui.U(34), PortraitPainter.W * 2, PortraitPainter.H * 2);
        if (fb.Height > r.Height - ui.U(150)) fb = new Rectangle(fb.X, fb.Y, PortraitPainter.W, PortraitPainter.H);
        ui.Rect(new Rectangle(fb.X - ui.U(2), fb.Y - ui.U(2), fb.Width + ui.U(4), fb.Height + ui.U(4)), Campaign.OfBanner(m) ? Kit.Gilt : Kit.GiltDark);
        ui.Face(PortraitPainter.Of(m, k), fb);
        int x = fb.Right + ui.U(20), w = r.Right - ui.U(24) - x, y = fb.Y;
        var t = Troops.Get(Troops.Has(m.Troop) ? m.Troop : "levies");
        ui.Text(t.Name.ToUpperInvariant() + (m.Hurt ? "  .  HURT" : ""), x, y, m.Hurt ? Style.Warn : Kit.Gilt, 2);
        y += ui.U(20);
        ui.TextFit($"{k.Name.ToUpperInvariant()}. AGE {Soldier.AgeOf(m)}" + (m.Home != "" ? $", FROM {m.Home.ToUpperInvariant()}" : "") + $". ON THE ROLL SINCE DAY {m.Joined}.", x, y, w, Style.Text, 1);
        y += ui.U(13);
        ui.TextFit($"{Campaign.N(m.Battles, "BATTLE")}, {Campaign.N(m.Kills, "KILL")}, {Campaign.N(m.Wounds, "WOUND")}" + (m.Mentions > 0 ? (Campaign.OfBanner(m) ? ". OF THE BANNER" : $". MENTIONED X{m.Mentions}") : "") + (m.Knight ? ". A KNIGHT OF YOUR ORDER" : ""), x, y, w, Style.Dim, 1);
        y += ui.U(18);
        if (m.Nature != "")
        {
            bool felt = Campaign.Senior(k) == m;
            ui.Text(m.Nature.ToUpperInvariant(), x, y, felt ? Kit.Gilt : Style.Friendly, 2);
            foreach (var line in ui.Wrap(Campaign.NatureLine(m.Nature).ToUpperInvariant() + (felt ? " HIS COMPANY FEELS IT: HE IS ITS SENIOR MAN." : " HIS COMPANY FEELS ITS SENIOR MAN'S NATURE, NOT HIS."), w).Take(2))
            { y += ui.U(18); ui.Text(line, x, y, Style.Dim, 1); y -= ui.U(6); }
            y += ui.U(22);
        }
        ui.Text("WHAT HE IS KNOWN FOR", x, y, Kit.Gilt, 1);
        y += ui.U(12);
        foreach (var d in m.Deeds)
            foreach (var line in ui.Wrap(d.ToUpperInvariant(), w - ui.U(10)))
            { ui.Text(line, x + ui.U(8), y, Style.Text, 1); y += ui.U(11); }
        if (m.Carries != "") { ui.TextFit($"HE CARRIES {m.Carries.ToUpperInvariant()}.", x + ui.U(8), y, w - ui.U(8), Kit.Gilt, 1); y += ui.U(11); }
        y += ui.U(6);
        if (m.Ties.Count > 0 || m.Avenging)
        {
            ui.Text("WHOM HE STANDS WITH", x, y, Kit.Gilt, 1);
            y += ui.U(12);
            foreach (var tie in m.Ties)
            {
                if (C.LivingMan(tie.Other) is not Soldier o) continue;
                var ok = C.Player.Companies.FirstOrDefault(z => z.Roll.Contains(o));
                var tb = new Rectangle(x + ui.U(8), y, ui.U(22), ui.U(22));
                ui.FaceHead(PortraitPainter.Of(o, ok), tb);
                string kind = tie.Kind switch { "Brothers" => "BROTHER TO", "Kin" => "KIN TO", "Rivals" => "RIVAL TO", "Master" => "MASTER TO", _ => "LAD TO" };
                ui.TextFit($"{kind} {o.Name.ToUpperInvariant()}" + (ok != null && ok != k ? $", OF {ok.Name.ToUpperInvariant()}" : ""), tb.Right + ui.U(8), y + ui.U(7), w - ui.U(40), Style.Friendly, 1);
                var row = new Rectangle(x, y, w, ui.U(22));
                if (ui.Clicked(row) && ok != null) { campMan = o; campManOf = ok; note = ""; }
                if (ui.Hover(row)) Ui.Hovered(row);
                y += ui.U(25);
            }
            if (m.Avenging) { ui.TextFit("OUT FOR VENGEANCE: HE STRIKES HARDER NEXT BATTLE, AND TAKES MORE RISK.", x + ui.U(8), y, w - ui.U(8), Style.Warn, 1); y += ui.U(12); }
        }
        ManActions(k, m, r.X + ui.U(24), r.Bottom - ui.U(84), r.Width - ui.U(48), () => campMan = null);
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(42), ui.U(180), ui.U(30)), "BACK TO THE FIRES", note: ctl.Name(Act.Menu))) campMan = null;
    }
}
