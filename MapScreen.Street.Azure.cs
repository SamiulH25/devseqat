using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the Azure people's pieces of the street (pale limestone under blue slate): the auberge (their tavern), the château
/// (their keep), the châtelet gatehouse (their barracks), the town hall with its belfry (their elders' house), the cathedral
/// front over the roofs (their landmark); and the parts they share: pepper-pot cones, steep slate hip roofs with dormers, cross
/// windows between blue shutters, round arches. Drawing only.
partial class MapScreen
{
    /// Dressed limestone, the blue slate of their roofs, their painted shutters and dark oak.
    static Color Ashlar(TownLook l, TownColours c) => HeroWall(l, c, new Color(224, 214, 194));
    static Color Slate(TownLook l, TownColours c) => l.Night ? new Color(50, 56, 84) : l.Dusk ? Color.Lerp(c.Near, new Color(78, 100, 142), 0.7f) : new Color(78, 100, 142);
    static Color Shutter(TownLook l) => l.Night ? new Color(40, 50, 78) : new Color(66, 106, 162);
    static Color AzureWood(TownLook l) => l.Night ? new Color(36, 28, 34) : new Color(84, 58, 42);
    static Color Tip(TownLook l) => l.Day ? Kit.Gilt : Kit.Mul(Kit.Gilt, 0.5f);

    /// A pepper-pot: a steep cone of slate `h` high on a tower `half` wide each side, courses across it, shaded on the left and
    /// caught by the light on the right, a gilt finial.
    void Cone(int cx, int baseY, int half, int h, Color slate, Color tip)
    {
        int s = Math.Max(1, ui.U(1));
        for (int y = 0; y < h; y += s)
        {
            int hw = Math.Max(s, (int)(half * (1 - y / (float)h)));
            var row = y > 0 && (y / s) % 3 == 0 ? Kit.Mul(slate, 0.84f) : slate;
            if (snowCap.A > 0 && (y > h * 0.5f || (y / s) % 4 == 1)) row = Color.Lerp(row, snowCap, y > h * 0.5f ? 0.75f : 0.5f); // snow lodged on the courses
            ui.Rect(new Rectangle(cx - hw, baseY - y - s, 2 * hw, s), row);
            ui.Rect(new Rectangle(cx - hw, baseY - y - s, Math.Max(s, hw / 2), s), Kit.Mul(row, 0.82f)); // the shaded side
            if (rimLight.A > 0) ui.Rect(new Rectangle(cx + hw - s, baseY - y - s, s, s), rimLight);
        }
        ui.Rect(new Rectangle(cx - half - s, baseY - s, 2 * half + 2 * s, s), Kit.Mul(slate, 0.6f)); // the eave
        if (snowCap.A > 0) ui.Rect(new Rectangle(cx - half - s, baseY - 2 * s, 2 * half + 2 * s, s), snowCap);
        ui.Rect(new Rectangle(cx - s / 2, baseY - h - ui.U(4), s, ui.U(4)), tip);
        ui.Rect(new Rectangle(cx - s, baseY - h - ui.U(2), 2 * s, s), tip);
    }

    /// A steep hip roof of slate from `x0` to `x1`, `h` high, its ridge `inset` in from each end; lead along the ridge, snow on
    /// its upper slope in winter.
    void SlateRoof(int x0, int x1, int baseY, int h, int inset, Color slate)
    {
        int s = Math.Max(1, ui.U(1));
        for (int y = 0; y < h; y += s)
        {
            int i = (int)(inset * y / (float)h);
            var row = y > 0 && (y / s) % 3 == 0 ? Kit.Mul(slate, 0.84f) : slate;
            if (snowCap.A > 0 && y > h * 0.6f) row = snowCap;
            ui.Rect(new Rectangle(x0 + i, baseY - y - s, x1 - x0 - 2 * i, s), row);
            ui.Rect(new Rectangle(x0 + i, baseY - y - s, Math.Max(s, (inset - i) / 2 + s), s), Kit.Mul(row, 0.82f)); // the hip in shade
            if (rimLight.A > 0) ui.Rect(new Rectangle(x1 - i - s, baseY - y - s, s, s), rimLight);
        }
        ui.Rect(new Rectangle(x0 + inset, baseY - h - s, x1 - x0 - 2 * inset, s), Kit.Mul(slate, 0.55f)); // the lead ridge
        ui.Rect(new Rectangle(x0 - s, baseY - s, x1 - x0 + 2 * s, ui.U(2)), Kit.Mul(slate, 0.62f)); // the eave
    }

    /// A dormer standing on a slate roof: a stone face with a window, its own little steep cap.
    void Dormer(int x, int baseY, int w, TownLook l, TownColours c, Color stone, Color slate)
    {
        int s = Math.Max(1, ui.U(1)), h = w;
        ui.Rect(new Rectangle(x, baseY - h, w, h), stone);
        var win = new Rectangle(x + ui.U(2), baseY - h + ui.U(3), w - ui.U(4), h - ui.U(4));
        ui.Rect(win, l.Day ? new Color(42, 48, 64) : c.Lit * 0.9f);
        ui.Rect(new Rectangle(win.Center.X, win.Y, s, win.Height), Kit.Mul(stone, 0.6f));
        int capH = w * 3 / 4, cx = x + w / 2;
        for (int y = 0; y < capH; y += s)
        {
            int hw = (int)((w / 2 + s) * (1 - y / (float)capH)) + s;
            ui.Rect(new Rectangle(cx - hw, baseY - h - y - s, 2 * hw, s), y > capH / 2 && snowCap.A > 0 ? snowCap : slate);
        }
        ui.Rect(new Rectangle(cx - s / 2, baseY - h - capH - ui.U(2), s, ui.U(2)), Kit.Mul(slate, 0.6f));
    }

    /// A cross window: stone frame, a mullion and a transom, blue shutters folded back either side, a sill.
    void CrossWindow(Rectangle w, TownLook l, TownColours c, Color stone, bool on, bool shutters = true)
    {
        int s = Math.Max(1, ui.U(1));
        on &= !l.Day;
        if (on) ui.Rect(new Rectangle(w.X - ui.U(3), w.Y - ui.U(3), w.Width + ui.U(6), w.Height + ui.U(6)), c.Lit * 0.16f);
        if (shutters)
            foreach (int sx in new[] { w.X - ui.U(1) - w.Width / 2, w.Right + ui.U(1) })
            {
                ui.Rect(new Rectangle(sx, w.Y, w.Width / 2, w.Height), Shutter(l));
                ui.Rect(new Rectangle(sx + w.Width / 4, w.Y, s, w.Height), Kit.Mul(Shutter(l), 0.7f));
                ui.Rect(new Rectangle(sx, w.Y + w.Height / 3, w.Width / 2, s), Kit.Mul(Shutter(l), 0.7f));
            }
        ui.Rect(new Rectangle(w.X - ui.U(1), w.Y - ui.U(1), w.Width + ui.U(2), w.Height + ui.U(2)), Kit.Mul(stone, 0.76f));
        ui.Rect(w, on ? c.Lit : new Color(44, 50, 68));
        if (!on) ui.Rect(new Rectangle(w.Right - ui.U(3), w.Y + ui.U(1), ui.U(2), s), new Color(120, 140, 170) * (l.Day ? 0.8f : 0.3f)); // a glint
        ui.Rect(new Rectangle(w.Center.X, w.Y, ui.U(1), w.Height), stone);
        ui.Rect(new Rectangle(w.X, w.Y + w.Height / 3, w.Width, ui.U(1)), stone);
        ui.Rect(new Rectangle(w.X - ui.U(2), w.Bottom + ui.U(1), w.Width + ui.U(4), ui.U(2)), Color.Lerp(stone, c.Rim, 0.25f));
    }

    /// A round arch: jambs `w` apart, a half-round head; `frame` (if given) drawn two units round it first as the voussoirs, a
    /// keystone at the crown.
    void RoundArch(int x, int bottom, int w, int h, Color fill, Color? frame = null)
    {
        int s = Math.Max(1, ui.U(1));
        void Shape(int grow, Color col)
        {
            int r = w / 2 + grow, cx = x + w / 2, spring = bottom - h + w / 2;
            for (int y = spring - r; y < spring; y += s)
            {
                int dy = spring - y, hw = (int)MathF.Sqrt(Math.Max(0, r * r - dy * dy));
                if (hw > 0) ui.Rect(new Rectangle(cx - hw, y, 2 * hw, s), col);
            }
            ui.Rect(new Rectangle(x - grow, spring, w + 2 * grow, bottom - spring), col);
        }
        if (frame is Color f)
        {
            Shape(ui.U(2), f);
            ui.Rect(new Rectangle(x + w / 2 - ui.U(2), bottom - h - ui.U(3), ui.U(4), ui.U(4)), Kit.Mul(f, 1.12f)); // the keystone
        }
        Shape(0, fill);
    }

    // ---------- the auberge ----------

    /// The Azure tavern: an auberge of dressed stone, two storeys under a steep slate roof with dormers and tall chimneys; arched
    /// shop windows and an arched door below, cross windows with blue shutters and flower boxes above, a round turret corbelled
    /// out at the corner under its pepper-pot, a gilt bunch of grapes hung from an iron bracket for its sign.
    List<(int X, int W)> Auberge(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color stone = Ashlar(l, c), slate = Slate(l, c), wood = AzureWood(l), shade = Kit.Mul(stone, 0.8f);
        var lights = new List<(int X, int W)>();
        int s = Math.Max(1, ui.U(1)), floor = r.Y + ui.U(28);
        rimLight = c.Rim;
        // Chimneys behind the roof, then the roof and its dormers.
        foreach (int chx in new[] { r.X + ui.U(16), r.X + ui.U(106) })
        {
            ui.Rect(new Rectangle(chx, r.Y - ui.U(38), ui.U(8), ui.U(30)), Kit.Mul(stone, 0.92f));
            ui.Rect(new Rectangle(chx - s, r.Y - ui.U(38), ui.U(8) + 2 * s, ui.U(2)), Kit.Mul(stone, 0.7f));
        }
        Smoke(r.X + ui.U(20), r.Y - ui.U(40), t, l.Night);
        SlateRoof(r.X - ui.U(4), r.Right - ui.U(8), r.Y, ui.U(30), ui.U(28), slate);
        foreach (int dx in new[] { 36, 66, 96 }) Dormer(r.X + ui.U(dx), r.Y - ui.U(3), ui.U(14), l, c, stone, slate);
        // The front: coursed stone, the string course between the storeys, a plinth.
        ui.Rect(r, stone);
        for (int y = r.Y + ui.U(7); y < r.Bottom; y += ui.U(7)) ui.Rect(new Rectangle(r.X, y, r.Width, s), Kit.Mul(stone, 0.92f));
        for (int row = 0, y = r.Y; y < r.Bottom; row++, y += ui.U(7))
            for (int x = r.X + ui.U(row % 2 == 0 ? 9 : 18); x < r.Right; x += ui.U(18)) ui.Rect(new Rectangle(x, y, s, ui.U(7)), Kit.Mul(stone, 0.93f));
        ui.Rect(new Rectangle(r.X, r.Y, r.Width, ui.U(3)), Color.Black * 0.24f); // the eave's shadow
        ui.Rect(new Rectangle(r.X - s, floor, r.Width + 2 * s, ui.U(2)), Color.Lerp(stone, c.Rim, 0.35f));
        ui.Rect(new Rectangle(r.X, floor + ui.U(2), r.Width, s), shade);
        ui.Rect(new Rectangle(r.X, r.Bottom - ui.U(4), r.Width, ui.U(4)), Kit.Mul(stone, 0.78f));
        Edges(r.X, r.Y, r.Width, r.Height, stone);
        // Upstairs: three cross windows with their shutters and boxes of red flowers.
        var bloom = l.Night ? new Color(110, 50, 60) : new Color(206, 56, 64);
        var leaf = l.Night ? new Color(40, 56, 46) : new Color(70, 120, 60);
        foreach (int wx in new[] { 18, 60, 102 })
        {
            var w = new Rectangle(r.X + ui.U(wx), r.Y + ui.U(6), ui.U(14), ui.U(16));
            CrossWindow(w, l, c, stone, true);
            ui.Rect(new Rectangle(w.X - ui.U(1), w.Bottom + ui.U(3), w.Width + ui.U(2), ui.U(3)), wood);
            for (int f = w.X; f < w.Right; f += ui.U(3))
            {
                ui.Rect(new Rectangle(f, w.Bottom + ui.U(1), ui.U(2), ui.U(2)), leaf);
                ui.Rect(new Rectangle(f + s, w.Bottom, ui.U(1) + s, ui.U(1) + s), bloom);
            }
        }
        // Below: two arched shop windows and the arched door between them.
        var glass = l.Day ? new Color(46, 52, 68) : c.Lit;
        foreach (int wx in new[] { 10, 98 })
        {
            int ax = r.X + ui.U(wx), aw = ui.U(40);
            RoundArch(ax, r.Bottom - ui.U(4), aw, ui.U(24), glass, Kit.Mul(stone, 0.84f));
            for (int m = ax + ui.U(10); m < ax + aw; m += ui.U(10)) ui.Rect(new Rectangle(m, r.Bottom - ui.U(28), ui.U(1), ui.U(24)), wood);
            ui.Rect(new Rectangle(ax, r.Bottom - ui.U(14), aw, ui.U(1)), wood);
            if (!l.Day) lights.Add((ax, aw));
            else ui.Rect(new Rectangle(ax + ui.U(2), r.Bottom - ui.U(9), aw - ui.U(4), ui.U(5)), Kit.Mul(cloth, 0.6f)); // a curtain half drawn
        }
        int door = r.X + ui.U(66);
        RoundArch(door, r.Bottom, ui.U(20), ui.U(28), l.Day ? wood : new Color(255, 200, 120), Kit.Mul(stone, 0.84f));
        if (l.Day)
        {
            ui.Rect(new Rectangle(door + ui.U(10), r.Bottom - ui.U(18), s, ui.U(18)), Kit.Mul(wood, 0.6f));
            foreach (int ny in new[] { 6, 13 }) ui.Rect(new Rectangle(door + ui.U(2), r.Bottom - ui.U(ny), ui.U(16), s), Kit.Mul(wood, 0.7f));
        }
        else
        {
            ui.Rect(new Rectangle(door, r.Bottom - ui.U(18), ui.U(6), ui.U(18)), wood); // one leaf swung in
            lights.Add((door, ui.U(20)));
            for (int k = 0; k < ui.U(8); k += s)
                ui.Rect(new Rectangle(door - k * 2, r.Bottom + k, ui.U(20) + 4 * k, s), c.Lit * (0.6f * (1 - MathF.Floor(k * 3f / ui.U(8)) / 3f)));
            Lantern(new Vector2(door - ui.U(6), r.Bottom - ui.U(22)), c, wood);
        }
        // The turret at the right corner, corbelled out from under the upper storey.
        int tw = ui.U(22), tx = r.Right - ui.U(16), ttop = r.Y - ui.U(14), tcx = tx + tw / 2;
        for (int i = 0; i < 4; i++) // the corbel, stepped in
        {
            int cw = tw - i * ui.U(5);
            ui.Rect(new Rectangle(tcx - cw / 2, floor + i * ui.U(3), cw, ui.U(3)), i % 2 == 0 ? shade : stone);
        }
        ui.Rect(new Rectangle(tx, ttop, tw, floor - ttop), stone);
        ui.Rect(new Rectangle(tx, ttop, ui.U(4), floor - ttop), shade);
        ui.Rect(new Rectangle(tx + tw - s, ttop, s, floor - ttop), Color.Lerp(stone, c.Rim, 0.5f));
        CrossWindow(new Rectangle(tcx - ui.U(4), r.Y + ui.U(4), ui.U(8), ui.U(12)), l, c, stone, true, shutters: false);
        Cone(tcx, ttop, tw / 2 + ui.U(2), ui.U(30), slate, Tip(l));
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * s);
        ui.Rect(new Rectangle(tcx + s, ttop - ui.U(38), ui.U(9), ui.U(3) + wave), cloth); // a pennon off the finial
        ui.Rect(new Rectangle(tcx, ttop - ui.U(38), s, ui.U(6)), Tip(l));
        Grapes(r.Right + ui.U(2), floor + ui.U(14), l, c, wood);
        return lights;
    }

    /// The auberge's sign: an iron bracket out to the right from the wall at (x, y) with a gilt bunch of grapes and its leaf hung
    /// under it.
    void Grapes(int x, int y, TownLook l, TownColours c, Color wood)
    {
        int s = Math.Max(1, ui.U(1));
        var iron = l.Night ? new Color(30, 28, 36) : new Color(46, 44, 50);
        ui.Rect(new Rectangle(x - ui.U(4), y, ui.U(22), ui.U(2)), iron);
        ui.Line(new Vector2(x, y + ui.U(10)), new Vector2(x + ui.U(10), y + ui.U(2)), iron, 2);
        ui.Disc(new Vector2(x + ui.U(17), y - ui.U(1)), ui.U(2), iron); // the scroll at its end
        int gx = x + ui.U(13), gy = y + ui.U(2);
        ui.Rect(new Rectangle(gx, gy, s, ui.U(3)), iron);
        var gold = l.Night ? new Color(170, 130, 70) : new Color(226, 184, 84);
        if (!l.Day) ui.Disc(new Vector2(gx, gy + ui.U(10)), ui.U(12), c.Lit * 0.1f);
        ui.Rect(new Rectangle(gx - ui.U(5), gy + ui.U(3), ui.U(5), ui.U(3)), l.Night ? new Color(70, 96, 60) : new Color(96, 150, 70)); // the leaf
        int[] row = { 4, 3, 3, 2, 1 };
        for (int i = 0; i < row.Length; i++)
            for (int j = 0; j < row[i]; j++)
            {
                var at = new Vector2(gx + (j - (row[i] - 1) / 2f) * ui.U(4), gy + ui.U(7) + i * ui.U(3));
                ui.Disc(at, ui.U(2), gold);
                ui.Rect(new Rectangle((int)at.X, (int)at.Y - s, s, s), Color.Lerp(gold, Color.White, 0.5f));
            }
    }

    // ---------- the château ----------

    /// The Azure keep: a château in pale stone, its lodging under a steep slate roof with dormers, a round tower at each end with a
    /// machicolated top and a tall pepper-pot, a stair turret before the roof flying the realm's colours, a gate in the low
    /// curtain before it. `scale` pixels make a keep unit; `hero` is the castle's own (by the street, on its motte).
    void Chateau(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        int k = ui.U(scale), s1 = Math.Max(1, ui.U(1));
        int wave = (int)MathF.Round(MathF.Sin(t * 3.2f) * s1);
        Color stone = hero ? Ashlar(l, c) : Color.Lerp(c.Far, c.Mid, 0.45f);
        Color shade = Kit.Mul(stone, hero ? 0.76f : 0.84f), dark = Kit.Mul(stone, hero ? 0.52f : 0.68f);
        Color litEdge = Color.Lerp(stone, c.Rim, hero ? 0.45f : 0.25f);
        Color slate = hero ? Slate(l, c) : Color.Lerp(c.Far, Slate(l, c), 0.5f);
        Color win = l.Day ? new Color(40, 44, 58) : c.Lit;
        var rim0 = rimLight;
        rimLight = hero ? c.Rim : Color.Transparent;
        KeepBase(cx, baseY, k, l, c, hero, stone, shade, dark, litEdge);
        void Box(int x, int y, int w, int h, Color col) => ui.Rect(new Rectangle(cx + x * k, baseY - y * k, w * k, h * k), col);

        // The lodging behind the curtain, its roof and dormers.
        SlateRoof(cx - 9 * k, cx + 9 * k, baseY - 19 * k, 8 * k, 5 * k, slate);
        if (hero) foreach (int dx in new[] { -6, 3 }) Dormer(cx + dx * k, baseY - 19 * k - k / 2, 3 * k, l, c, stone, slate);
        Box(-8, 19, 16, 10, Kit.Mul(stone, 0.96f));
        Box(-8, 19, 16, 1, Kit.Mul(stone, 0.7f)); // under the eave
        foreach (int wx in new[] { -6, -3, 1, 4 })
        {
            var wr = new Rectangle(cx + wx * k, baseY - 16 * k, 2 * k, 4 * k);
            if (hero) CrossWindow(wr, l, c, stone, (wx + 7) % 2 == 0, shutters: false);
            else if (!l.Day && wx % 2 == 0) ui.Rect(new Rectangle(wr.X, wr.Y + k, k, 2 * k), c.Lit * 0.9f);
        }

        // The towers at each end: round (shaded left, lit right), slits, a machicolated gallery, the cone.
        foreach (int tx in new[] { -15, 9 })
        {
            Box(tx, 22, 6, 22, stone);
            Box(tx, 22, 2, 22, shade);
            ui.Rect(new Rectangle(cx + (tx + 6) * k - s1, baseY - 22 * k, s1, 22 * k), litEdge);
            Box(tx, 22, 6, 1, dark);
            ui.Rect(new Rectangle(cx + tx * k - s1, baseY - 23 * k, 6 * k + 2 * s1, k), Kit.Mul(stone, 1.04f)); // the gallery
            for (int m = 0; m < 6; m++) ui.Rect(new Rectangle(cx + tx * k + m * k + k / 3, baseY - 22 * k, k / 3 + s1, k / 2), dark); // its corbels
            foreach (int sy in new[] { 8, 15 })
                if (hero) { ui.Rect(new Rectangle(cx + (tx + 3) * k - s1, baseY - (sy + 2) * k, 2 * s1, 3 * k), l.Day ? dark : c.Lit * 0.85f); }
                else if (!l.Day && sy == 15) ui.Rect(new Rectangle(cx + (tx + 3) * k - s1, baseY - (sy + 2) * k, 2 * s1, 2 * k), c.Lit * 0.8f);
            Cone(cx + (tx + 3) * k, baseY - 23 * k, 3 * k + k / 2, 10 * k, slate, Tip(l));
        }

        // The curtain before the lodging, its crenels, the gate.
        Box(-9, 9, 18, 9, stone);
        for (int y = 2; y < 9; y += 2) ui.Rect(new Rectangle(cx - 9 * k, baseY - y * k, 18 * k, s1), Kit.Mul(stone, 0.9f));
        for (int m = -9; m < 9; m += 2) Box(m, 10, 1, 1, stone);
        ui.Rect(new Rectangle(cx - 9 * k, baseY - 9 * k, 18 * k, s1), litEdge);
        RoundArch(cx - 2 * k, baseY, 4 * k, 6 * k, l.Day ? new Color(36, 30, 30) : Color.Lerp(new Color(70, 40, 26), c.Lit, 0.4f), hero ? Kit.Mul(stone, 0.82f) : null);
        if (hero) for (int x = cx - 2 * k + k / 2; x < cx + 2 * k; x += k) ui.Rect(new Rectangle(x, baseY - 5 * k, s1, 4 * k), new Color(70, 66, 66)); // the portcullis

        // The stair turret before the roof, its cone and the standard.
        Box(-2, 25, 4, 16, Kit.Mul(stone, 0.98f));
        Box(-2, 25, 1, 16, shade);
        if (hero || !l.Day) ui.Rect(new Rectangle(cx - s1, baseY - 21 * k, 2 * s1, 2 * k), l.Day ? dark : c.Lit * 0.85f);
        Cone(cx, baseY - 25 * k, 2 * k + k / 2, 8 * k, slate, Tip(l));
        int poleTop = baseY - 37 * k;
        ui.Rect(new Rectangle(cx, poleTop, s1, 4 * k), dark);
        ui.Rect(new Rectangle(cx + s1, poleTop, 5 * k, 2 * k + wave), cloth);
        ui.Rect(new Rectangle(cx + s1 + 5 * k, poleTop + s1, k, k + wave), cloth);
        ui.Rect(new Rectangle(cx + s1, poleTop + k, 5 * k, s1), Color.Lerp(cloth, Kit.Gilt, 0.4f));
        rimLight = rim0;
    }

    // ---------- the châtelet ----------

    /// The Azure barracks: a gatehouse between two round towers under pepper-pots, crosslet slits, the gate with its portcullis
    /// and the realm's banner over it, torches by night; the yard beside it.
    void Chatelet(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        Color stone = Ashlar(l, c), shade = Kit.Mul(stone, 0.76f), dark = Kit.Mul(stone, 0.55f), slate = Slate(l, c);
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * s1);
        // The gate block between the towers, machicolated, crenels on top.
        var block = new Rectangle(r.X + ui.U(12), ground - ui.U(42), ui.U(34), ui.U(42));
        ui.Rect(block, stone);
        for (int y = block.Y + ui.U(7); y < ground; y += ui.U(7)) ui.Rect(new Rectangle(block.X, y, block.Width, s1), Kit.Mul(stone, 0.9f));
        for (int x = block.X; x < block.Right - ui.U(3); x += ui.U(7)) ui.Rect(new Rectangle(x, block.Y - ui.U(4), ui.U(4), ui.U(4)), stone);
        ui.Rect(new Rectangle(block.X, block.Y, block.Width, s1), Color.Lerp(stone, c.Rim, 0.5f));
        for (int x = block.X + ui.U(2); x < block.Right; x += ui.U(5)) ui.Rect(new Rectangle(x, block.Y + ui.U(2), ui.U(2), ui.U(2)), dark); // corbels
        int gx = block.X + ui.U(9);
        RoundArch(gx, ground, ui.U(16), ui.U(24), l.Day ? new Color(36, 30, 28) : Color.Lerp(new Color(60, 36, 24), c.Lit, 0.35f), Kit.Mul(stone, 0.84f));
        for (int x = gx + ui.U(2); x < gx + ui.U(16); x += ui.U(4)) ui.Rect(new Rectangle(x, ground - ui.U(20), s1, ui.U(14)), new Color(72, 68, 70));
        ui.Rect(new Rectangle(gx, ground - ui.U(14), ui.U(16), s1), new Color(72, 68, 70));
        // The banner over the gate.
        ui.Rect(new Rectangle(gx + ui.U(2), block.Y + ui.U(6), ui.U(12), ui.U(10)), cloth);
        ui.Rect(new Rectangle(gx + ui.U(4), block.Y + ui.U(16), ui.U(8), ui.U(2)), cloth);
        var fleur = Color.Lerp(cloth, Kit.Gilt, 0.7f); // a lily: the middle petal, one each side, the band
        ui.Rect(new Rectangle(gx + ui.U(7), block.Y + ui.U(7), ui.U(2), ui.U(8)), fleur);
        ui.Rect(new Rectangle(gx + ui.U(4), block.Y + ui.U(9), ui.U(2), ui.U(4)), fleur);
        ui.Rect(new Rectangle(gx + ui.U(10), block.Y + ui.U(9), ui.U(2), ui.U(4)), fleur);
        ui.Rect(new Rectangle(gx + ui.U(5), block.Y + ui.U(12), ui.U(6), s1), fleur);
        // The towers.
        foreach (int tx in new[] { r.X, r.X + ui.U(40) })
        {
            var tw = new Rectangle(tx, ground - ui.U(50), ui.U(18), ui.U(50));
            ui.Rect(tw, stone);
            ui.Rect(new Rectangle(tw.X, tw.Y, ui.U(5), tw.Height), shade);
            ui.Rect(new Rectangle(tw.Right - s1, tw.Y, s1, tw.Height), Color.Lerp(stone, c.Rim, 0.5f));
            ui.Rect(new Rectangle(tw.X - s1, tw.Y - ui.U(3), tw.Width + 2 * s1, ui.U(3)), Kit.Mul(stone, 1.04f));
            for (int x = tw.X + ui.U(1); x < tw.Right; x += ui.U(4)) ui.Rect(new Rectangle(x, tw.Y, ui.U(2), ui.U(2)), dark);
            foreach (int sy in new[] { 14, 30 }) // crosslet slits
            {
                var slitCol = l.Day ? new Color(34, 30, 32) : c.Lit * 0.85f;
                ui.Rect(new Rectangle(tw.X + ui.U(8), tw.Y + ui.U(sy), ui.U(2), ui.U(9)), slitCol);
                ui.Rect(new Rectangle(tw.X + ui.U(6), tw.Y + ui.U(sy + 3), ui.U(6), s1), l.Day ? slitCol : Kit.Mul(stone, 0.5f)); // the arms stay dark: only the slit shows the light
            }
            Cone(tw.Center.X, tw.Y - ui.U(3), ui.U(11), ui.U(18), slate, Tip(l));
        }
        ui.Rect(new Rectangle(r.X + ui.U(9), ground - ui.U(80), s1, ui.U(9)), Tip(l)); // a pennon off the left cone
        ui.Rect(new Rectangle(r.X + ui.U(9) + s1, ground - ui.U(80), ui.U(10), ui.U(3) + wave), cloth);
        if (!l.Day)
            foreach (int tx in new[] { gx - ui.U(5), gx + ui.U(20) })
            {
                var tp = new Vector2(tx, ground - ui.U(17));
                ui.Rect(new Rectangle(tx, ground - ui.U(17), s1, ui.U(4)), shade);
                ui.Disc(tp, ui.U(7), c.Lit * 0.14f); ui.Disc(tp, ui.U(3), c.Lit * 0.28f);
                ui.Rect(new Rectangle(tx - s1, ground - ui.U(19), ui.U(2), ui.U(2)), new Color(255, 228, 148));
            }
        Yard(r.X + ui.U(60), r.Right, ground, l, cloth);
    }

    // ---------- the town hall ----------

    /// The Azure elders' house: a town hall over an open arcade, cross windows with shutters above, a hip roof of slate and a
    /// belfry on its ridge with the bell in it, the realm's banner hung from the middle window.
    void HotelDeVille(Rectangle r, TownLook l, TownColours c, Color cloth, float t, List<(int X, int W)> lights)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        int wallH = ui.U(34), wallTop = ground - wallH;
        Color stone = Ashlar(l, c), shade = Kit.Mul(stone, 0.8f), slate = Slate(l, c), wood = AzureWood(l);
        // The belfry behind the ridge, then the roof.
        int bx = r.Center.X, by = wallTop - ui.U(14);
        ui.Rect(new Rectangle(bx - ui.U(6), by - ui.U(10), ui.U(12), ui.U(12)), stone);
        ui.Rect(new Rectangle(bx - ui.U(4), by - ui.U(8), ui.U(8), ui.U(8)), l.Day ? new Color(40, 40, 52) : Kit.Mul(c.Lit, 0.5f));
        var bell = l.Night ? new Color(150, 116, 64) : new Color(210, 166, 82);
        ui.Rect(new Rectangle(bx - ui.U(2), by - ui.U(6), ui.U(4), ui.U(4)), bell);
        ui.Rect(new Rectangle(bx - ui.U(3), by - ui.U(3), ui.U(6), s1), bell);
        Cone(bx, by - ui.U(10), ui.U(8), ui.U(10), slate, Tip(l));
        SlateRoof(r.X - ui.U(4), r.Right + ui.U(4), wallTop, ui.U(16), ui.U(18), slate);
        // The front and its arcade.
        ui.Rect(new Rectangle(r.X, wallTop, r.Width, wallH), stone);
        ui.Rect(new Rectangle(r.X, wallTop, r.Width, ui.U(2)), Color.Black * 0.26f);
        ui.Rect(new Rectangle(r.X - s1, ground - ui.U(18), r.Width + 2 * s1, ui.U(2)), Color.Lerp(stone, c.Rim, 0.3f)); // the string course
        Edges(r.X, wallTop, r.Width, wallH, stone);
        var inside = l.Day ? new Color(60, 52, 50) : Color.Lerp(new Color(100, 60, 36), c.Lit, 0.5f);
        for (int i = 0; i < 3; i++)
        {
            int ax = r.X + ui.U(5 + i * 23);
            bool door = i == 1;
            RoundArch(ax, ground, ui.U(18), ui.U(16), door && !l.Day ? new Color(255, 200, 120) : inside, Kit.Mul(stone, 0.84f));
            if (door && l.Day) ui.Rect(new Rectangle(ax + ui.U(3), ground - ui.U(10), ui.U(12), ui.U(10)), wood);
            if (!l.Day) lights.Add((ax, ui.U(18)));
        }
        if (!l.Day)
            for (int k = 0; k < ui.U(6); k += s1)
                ui.Rect(new Rectangle(r.X + ui.U(28) - k * 2, ground + k, ui.U(18) + 4 * k, s1), c.Lit * (0.5f * (1 - MathF.Floor(k * 3f / ui.U(6)) / 3f)));
        foreach (int wx in new[] { 10, 33, 56 })
            CrossWindow(new Rectangle(r.X + ui.U(wx), wallTop + ui.U(4), ui.U(8), ui.U(10)), l, c, stone, true, shutters: wx != 33);
        // The banner from the middle window.
        int wave = (int)MathF.Round(MathF.Sin(t * 2.6f) * s1);
        ui.Rect(new Rectangle(r.X + ui.U(34), wallTop + ui.U(13), ui.U(6), ui.U(9) + wave), cloth);
        ui.Rect(new Rectangle(r.X + ui.U(36), wallTop + ui.U(15), ui.U(2), ui.U(4)), Color.Lerp(cloth, Kit.Gilt, 0.6f));
        ui.Rect(new Rectangle(r.X, ground - ui.U(3), r.Width, ui.U(3)), shade);
    }

    // ---------- the cathedral ----------

    /// The Azure landmark behind the roofs: a cathedral's west front, flat in the far colour: two towers with spires, the nave's
    /// gable between them with its rose window, flying buttresses down the side; the rose and lancets lit at night.
    void Cathedral(int x, int ground, Color c, bool night, Color lit)
    {
        int s = Math.Max(1, ui.U(1));
        // The nave running back to the right, its buttresses.
        ui.Rect(new Rectangle(x + ui.U(54), ground - ui.U(44), ui.U(40), ui.U(44)), Kit.Mul(c, 0.94f));
        Gable(x + ui.U(54), ground - ui.U(44), ui.U(40), 0, ui.U(12), Kit.Mul(c, 0.94f));
        for (int b = 0; b < 3; b++)
        {
            int bx = x + ui.U(60 + b * 13);
            ui.Line(new Vector2(bx, ground - ui.U(36)), new Vector2(bx + ui.U(8), ground - ui.U(22)), c, ui.U(2));
            ui.Rect(new Rectangle(bx + ui.U(7), ground - ui.U(22), ui.U(3), ui.U(22)), c);
            ui.Rect(new Rectangle(bx + ui.U(2), ground - ui.U(32), ui.U(3), ui.U(10)), night ? lit * 0.7f : Kit.Mul(c, 0.8f)); // a lancet
        }
        // The front: the gable and two towers.
        Gable(x + ui.U(14), ground, ui.U(28), ui.U(52), ui.U(16), c);
        foreach (int tx in new[] { 0, 40 })
        {
            ui.Rect(new Rectangle(x + ui.U(tx), ground - ui.U(80), ui.U(16), ui.U(80)), c);
            ui.Rect(new Rectangle(x + ui.U(tx + 15), ground - ui.U(80), s, ui.U(80)), Color.Lerp(c, rimLight.A > 0 ? rimLight : c, 0.5f));
            for (int y = 0; y < ui.U(30); y += s) // the spire
            {
                int hw = (int)(ui.U(8) * (1 - y / (float)ui.U(30)));
                ui.Rect(new Rectangle(x + ui.U(tx + 8) - hw, ground - ui.U(80) - y - s, 2 * hw, s), c);
            }
            ui.Rect(new Rectangle(x + ui.U(tx + 8) - s / 2, ground - ui.U(116), s, ui.U(6)), c);
            ui.Rect(new Rectangle(x + ui.U(tx + 6), ground - ui.U(70), ui.U(4), ui.U(12)), night ? lit * 0.75f : Kit.Mul(c, 0.78f)); // the belfry openings
        }
        var rose = new Vector2(x + ui.U(28), ground - ui.U(40));
        ui.Disc(rose, ui.U(9), Kit.Mul(c, 0.84f)); // its deep frame
        ui.Disc(rose, ui.U(7), night ? lit * 0.85f : Kit.Mul(c, 0.66f));
        for (int i = 0; i < 6; i++) { float a = i * MathF.PI / 3; ui.Line(rose, rose + new Vector2(MathF.Cos(a), MathF.Sin(a)) * ui.U(7), Kit.Mul(c, 0.9f), 1); }
        ui.Rect(new Rectangle(x + ui.U(20), ground - ui.U(26), ui.U(16), ui.U(26)), Kit.Mul(c, 0.86f)); // the portal's deep jambs
        ui.Rect(new Rectangle(x + ui.U(23), ground - ui.U(22), ui.U(10), ui.U(22)), night ? lit * 0.5f : Kit.Mul(c, 0.68f)); // the great door
    }
}
