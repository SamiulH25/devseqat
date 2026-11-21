using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the southern people's pieces of the street (white and earthen building): the caravanserai (their tavern), the
/// kasbah on its hill (their keep), the ribat (their barracks), the majlis (their elders' house), the great mosque with its
/// minaret over the roofs (their landmark); and the parts they share: horseshoe arches, stepped merlons, beam ends in earthen
/// walls, tile bands, wooden lattice windows, hung carpets. Drawing only.
partial class MapScreen
{
    /// Earthen walls (rammed clay, ochre), their shade and the dark of carved wood.
    static Color Clay(TownLook l, TownColours c) => HeroWall(l, c, new Color(204, 158, 108));
    static Color SouthWood(TownLook l) => l.Night ? new Color(46, 30, 30) : new Color(108, 70, 44);
    static Color Lime(TownLook l, TownColours c) => HeroWall(l, c, new Color(238, 230, 212));

    /// A horseshoe arch: jambs `w` apart up to the spring, then a round head wider than the jambs that turns back in below its
    /// widest. `x, bottom` is its lower left; `frame` (if given) is drawn one pixel round it first.
    void HorseshoeArch(int x, int bottom, int w, int h, Color fill, Color? frame = null)
    {
        int s = Math.Max(1, ui.U(1));
        void Shape(int grow, Color col)
        {
            int r = w / 2 + s + grow, cx = x + w / 2, spring = bottom - h + r;
            int back = spring + r / 3; // the horseshoe turns back in below the spring
            for (int y = bottom - h - grow; y < back; y += s)
            {
                int dy = Math.Abs(spring - y);
                int hw = dy >= r ? 0 : (int)MathF.Sqrt(r * r - dy * dy);
                if (y > spring) hw = Math.Max(w / 2 + grow, hw);
                if (hw > 0) ui.Rect(new Rectangle(cx - hw, y, 2 * hw, s), col);
            }
            ui.Rect(new Rectangle(x - grow, back, w + 2 * grow, bottom - back), col);
        }
        if (frame is Color f) Shape(s, f);
        Shape(0, fill);
    }

    /// Stepped merlons along a wall top from `x0` to `x1`: a broad step and a narrow one over it, `a` pixels to a step.
    void Merlons(int x0, int x1, int top, Color col, int a)
    {
        for (int x = x0; x + 4 * a <= x1; x += 6 * a)
        {
            ui.Rect(new Rectangle(x, top - 2 * a, 4 * a, 2 * a), col);
            ui.Rect(new Rectangle(x + a, top - 3 * a, 2 * a, a), col);
        }
        if (snowCap.A > 0) ui.Rect(new Rectangle(x0, top - Math.Max(1, ui.U(1)), x1 - x0, Math.Max(1, ui.U(1))), snowCap);
    }

    /// The ends of the floor beams standing out of an earthen wall in a row, each with its shadow.
    void Toron(int x0, int x1, int y, Color wood, int a)
    {
        for (int x = x0; x + a <= x1; x += 5 * a)
        {
            ui.Rect(new Rectangle(x, y, a, a), wood);
            ui.Rect(new Rectangle(x, y + a, a, Math.Max(1, a / 2)), Color.Black * 0.25f);
        }
    }

    /// A band of glazed tiles: small squares in blue, teal and white, two rows.
    void TileBand(int x0, int x1, int y, TownLook l)
    {
        int a = ui.U(2);
        float dim = l.Night ? 0.4f : l.Dusk ? 0.75f : 1f;
        Color[] tiles = { new(46, 92, 150), new(240, 234, 220), new(40, 130, 130), new(240, 234, 220) };
        for (int row = 0; row < 2; row++)
            for (int x = x0, i = row * 2; x < x1; x += a, i++)
                ui.Rect(new Rectangle(x, y + row * a, Math.Min(a, x1 - x), a), Kit.Mul(tiles[i % tiles.Length], dim));
    }

    /// A wooden lattice window (a mashrabiya): a box of turned wood that juts from the wall, with a little roof and its
    /// shadow under it; lamplight shows through its holes at night.
    void Lattice(Rectangle w, TownLook l, TownColours c, List<(int X, int W)> lights)
    {
        int s = Math.Max(1, ui.U(1));
        var wood = SouthWood(l);
        ui.Rect(new Rectangle(w.X - ui.U(2), w.Bottom, w.Width + ui.U(4), ui.U(2)), Color.Black * 0.22f); // its shadow on the wall
        ui.Rect(new Rectangle(w.X - ui.U(1), w.Y - ui.U(1), w.Width + ui.U(2), w.Height + ui.U(2)), wood);
        var hole = l.Day ? new Color(40, 34, 36) : Color.Lerp(new Color(90, 50, 30), c.Lit, 0.8f);
        for (int y = w.Y + s; y < w.Bottom - s; y += ui.U(2))
            for (int x = w.X + s + (y / ui.U(2) % 2) * s; x < w.Right - s; x += ui.U(2))
                ui.Rect(new Rectangle(x, y, s, s), hole);
        ui.Rect(new Rectangle(w.X - ui.U(2), w.Y - ui.U(3), w.Width + ui.U(4), ui.U(2)), Kit.Mul(wood, 1.2f)); // its little roof
        ui.Rect(new Rectangle(w.X - ui.U(1), w.Bottom + ui.U(1), w.Width + ui.U(2), s), wood); // its sill
        if (!l.Day) { ui.Disc(w.Center.ToVector2(), w.Width * 0.7f, c.Lit * 0.08f); lights.Add((w.X, w.Width)); }
    }

    /// A carpet hung over a rail: its field, a border, a diamond at its middle and fringes at its foot.
    void Rug(Rectangle r, Color field, Color border, TownLook l)
    {
        int s = Math.Max(1, ui.U(1));
        float dim = l.Night ? 0.45f : l.Dusk ? 0.78f : 1f;
        field = Kit.Mul(field, dim); border = Kit.Mul(border, dim);
        ui.Rect(r, border);
        ui.Rect(new Rectangle(r.X + ui.U(2), r.Y + ui.U(2), r.Width - ui.U(4), r.Height - ui.U(4)), field);
        int cx = r.Center.X, cy = r.Center.Y, d = Math.Min(r.Width, r.Height) / 4;
        for (int k = -d; k <= d; k += s) ui.Rect(new Rectangle(cx - (d - Math.Abs(k)), cy + k, 2 * (d - Math.Abs(k)) + s, s), border);
        for (int x = r.X + s; x < r.Right; x += ui.U(2)) ui.Rect(new Rectangle(x, r.Bottom, s, ui.U(2)), Kit.Mul(border, 0.9f));
    }

    /// A star lantern of pierced brass hung on a chain, and its glow (lit only after dusk).
    void StarLantern(int x, int y, TownLook l, TownColours c, int chain)
    {
        int s = Math.Max(1, ui.U(1));
        var brass = l.Day ? new Color(180, 140, 70) : new Color(255, 214, 130);
        ui.Rect(new Rectangle(x, y - chain, s, chain), SouthWood(l));
        if (!l.Day) { ui.Disc(new Vector2(x, y + ui.U(3)), ui.U(9), c.Lit * 0.13f); ui.Disc(new Vector2(x, y + ui.U(3)), ui.U(4), c.Lit * 0.22f); }
        ui.Rect(new Rectangle(x - ui.U(2), y + ui.U(1), ui.U(5), ui.U(4)), brass);
        ui.Rect(new Rectangle(x - s, y, ui.U(3), ui.U(6)), brass);
        ui.Rect(new Rectangle(x, y + ui.U(6), s, ui.U(2)), brass);
    }

    // ---------- the caravanserai ----------

    /// The southern tavern, a caravanserai: a whitewashed front over an arcade of horseshoe arches (the middle one its door),
    /// a band of tiles, an upper storey with lattice windows, stepped merlons along its roof, a wind tower at one end and a small
    /// dome at the other, a carpet hung from the roof, lanterns in the arches and a pot sign.
    List<(int X, int W)> Caravanserai(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color lime = Lime(l, c), wood = SouthWood(l), shade = Kit.Mul(lime, 0.8f);
        var lights = new List<(int X, int W)>();
        int s = Math.Max(1, ui.U(1)), arcadeTop = r.Bottom - ui.U(30), roofY = r.Y + ui.U(2);
        rimLight = c.Rim;
        // The wind tower at the left end and the dome over the right, behind the parapet.
        var tower = new Rectangle(r.X + ui.U(6), roofY - ui.U(22), ui.U(14), ui.U(22));
        ui.Rect(tower, lime);
        for (int i = 0; i < 3; i++) ui.Rect(new Rectangle(tower.X + ui.U(2 + i * 4), tower.Y + ui.U(4), ui.U(2), ui.U(9)), l.Day ? new Color(60, 50, 48) : Kit.Mul(wood, 0.8f)); // its vents
        Merlons(tower.X, tower.Right, tower.Y, lime, ui.U(1));
        Edges(tower.X, tower.Y, tower.Width, tower.Height, lime);
        var drum = new Rectangle(r.Right - ui.U(44), roofY - ui.U(8), ui.U(28), ui.U(8));
        ui.Rect(drum, Kit.Mul(lime, 0.95f));
        Dome(drum.Center.X, drum.Y, ui.U(14), ui.U(14), l.Night ? Color.Lerp(c.Near, new Color(90, 130, 150), 0.35f) : new Color(96, 150, 160)); // glazed green-blue
        ui.Rect(new Rectangle(drum.Center.X, drum.Y - ui.U(19), s, ui.U(5)), l.Day ? Kit.Gilt : Kit.Mul(Kit.Gilt, 0.5f));
        // The front.
        ui.Rect(new Rectangle(r.X, roofY, r.Width, r.Bottom - roofY), lime);
        Merlons(r.X, r.Right, roofY, lime, ui.U(2));
        ui.Rect(new Rectangle(r.X, roofY, r.Width, s), Color.Lerp(lime, c.Rim, 0.3f));
        ui.Rect(new Rectangle(r.Right - ui.U(10), roofY, ui.U(10), r.Bottom - roofY), shade); // the shaded end
        Edges(r.X, roofY, r.Width, r.Bottom - roofY, lime);
        // Upstairs: three lattice windows, and a carpet hung over the sill between two of them.
        foreach (int wx in new[] { r.X + ui.U(16), r.X + ui.U(70), r.X + ui.U(124) })
            Lattice(new Rectangle(wx, roofY + ui.U(8), ui.U(20), ui.U(14)), l, c, lights);
        ui.Rect(new Rectangle(r.X + ui.U(42), roofY + ui.U(9), ui.U(22), s), wood);
        Rug(new Rectangle(r.X + ui.U(44), roofY + ui.U(10), ui.U(18), ui.U(16)), Kit.Mul(cloth, 0.95f), new Color(214, 170, 90), l);
        // The tiles over the arcade, and the arcade: five arches, the middle one the door.
        TileBand(r.X, r.Right, arcadeTop - ui.U(5), l);
        var inside = l.Day ? new Color(70, 50, 44) : Color.Lerp(new Color(110, 60, 36), c.Lit, 0.55f);
        for (int i = 0; i < 5; i++)
        {
            int ax = r.X + ui.U(6 + i * 30);
            if (i == 2)
            {
                HorseshoeArch(ax, r.Bottom, ui.U(22), ui.U(27), l.Day ? new Color(64, 44, 34) : new Color(255, 200, 120), wood);
                ui.Rect(new Rectangle(ax + ui.U(1), r.Bottom - ui.U(20), ui.U(4), ui.U(20)), wood); // the doors of carved wood, swung open
                ui.Rect(new Rectangle(ax + ui.U(17), r.Bottom - ui.U(20), ui.U(4), ui.U(20)), wood);
                ui.Rect(new Rectangle(ax + ui.U(5), r.Bottom - ui.U(2), ui.U(12), ui.U(2)), Kit.Mul(lime, 0.7f)); // the step
                StarLantern(ax + ui.U(11), r.Bottom - ui.U(22), l, c, ui.U(2));
                if (!l.Day)
                {
                    lights.Add((ax, ui.U(22)));
                    for (int k = 0; k < ui.U(8); k += s) // its light on the street
                        ui.Rect(new Rectangle(ax - k * 2, r.Bottom + k, ui.U(22) + 4 * k, s), c.Lit * (0.6f * (1 - MathF.Floor(k * 3f / ui.U(8)) / 3f)));
                }
                continue;
            }
            HorseshoeArch(ax, r.Bottom, ui.U(22), ui.U(24), inside, Kit.Mul(lime, 0.78f));
            ui.Rect(new Rectangle(ax + ui.U(2), r.Bottom - ui.U(6), ui.U(18), ui.U(6)), Kit.Mul(cloth, l.Night ? 0.5f : 0.8f)); // a cushioned bench within
            ui.Rect(new Rectangle(ax + ui.U(2), r.Bottom - ui.U(6), ui.U(18), s), Kit.Mul(cloth, l.Night ? 0.7f : 1.1f));
            StarLantern(ax + ui.U(11), r.Bottom - ui.U(20), l, c, ui.U(3));
            if (!l.Day) lights.Add((ax + ui.U(4), ui.U(14)));
        }
        // A palm in a great pot by the door.
        int px = r.X - ui.U(7); // at the corner, clear of the arcade
        var pot = l.Night ? new Color(96, 56, 44) : new Color(176, 96, 62);
        ui.Rect(new Rectangle(px - ui.U(4), r.Bottom - ui.U(8), ui.U(9), ui.U(8)), pot);
        ui.Rect(new Rectangle(px - ui.U(5), r.Bottom - ui.U(9), ui.U(11), ui.U(2)), Kit.Mul(pot, 1.15f));
        Palm(px, r.Bottom - ui.U(8), Color.Lerp(c.Near, new Color(70, 110, 60), l.Night ? 0.25f : 0.8f), 0.45f);
        PotSign(r.Right + ui.U(2), arcadeTop - ui.U(2), l, c, wood);
        return lights;
    }

    /// The caravanserai's sign: a board on a bracket with a brass coffee pot on it.
    static readonly string[] PotMap =
    {
        "....##....",
        "...####...",
        "..######.#",
        "#.######.#",
        ".#######.#",
        "..#######.",
        "..######..",
        "...####...",
    };

    void PotSign(int ax, int ay, TownLook l, TownColours c, Color frame)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(ax - ui.U(6), ay, ui.U(30), ui.U(2)), frame);
        ui.Line(new Vector2(ax - ui.U(4), ay + ui.U(10)), new Vector2(ax + ui.U(6), ay + ui.U(2)), frame, 2);
        ui.Rect(new Rectangle(ax + ui.U(6), ay + ui.U(2), s, ui.U(4)), frame);
        ui.Rect(new Rectangle(ax + ui.U(19), ay + ui.U(2), s, ui.U(4)), frame);
        var board = new Rectangle(ax + ui.U(2), ay + ui.U(6), ui.U(22), ui.U(16));
        if (!l.Day) ui.Disc(board.Center.ToVector2(), ui.U(15), c.Lit * 0.1f);
        ui.Rect(new Rectangle(board.X - s, board.Y - s, board.Width + 2 * s, board.Height + 2 * s), Kit.Gilt * (l.Night ? 0.5f : 0.9f));
        ui.Rect(board, l.Night ? new Color(40, 56, 70) : new Color(46, 92, 120)); // a blue board
        var brass = l.Night ? new Color(210, 170, 90) : new Color(232, 190, 96);
        int cs = ui.U(1.5f) > 0 ? ui.U(1.5f) : s, ox = board.X + ui.U(4), oy = board.Y + ui.U(2);
        for (int row = 0; row < PotMap.Length; row++)
            for (int i = 0; i < PotMap[row].Length; i++)
                if (PotMap[row][i] == '#') ui.Rect(new Rectangle(ox + i * cs, oy + row * cs, cs, cs), row < 3 ? Kit.Mul(brass, 1.1f) : brass);
    }

    // ---------- the kasbah ----------

    /// The southern keep: a kasbah of rammed earth on its hill. Tapering corner towers and a taller tower over the gate, all with
    /// stepped merlons, rows of beam ends and a band of carved relief under the top, a horseshoe gate, slit windows, the flag.
    /// `hero` paints the near one of a castle (on its mound, with palms, the gate's studded doors and torches); else a hilltop fort.
    void Kasbah(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        int k = ui.U(scale), s = Math.Max(1, ui.U(1)), a = Math.Max(s, k / 2);
        int wave = (int)MathF.Round(MathF.Sin(t * 3.2f) * s);
        Color clay = hero ? Clay(l, c) : Color.Lerp(Color.Lerp(c.Far, c.Mid, 0.52f), new Color(204, 158, 108), l.Day ? 0.35f : 0.12f);
        Color shade = Kit.Mul(clay, 0.8f), dark = Kit.Mul(clay, 0.62f), wood = hero ? SouthWood(l) : dark;
        Color slit = l.Day ? new Color(52, 36, 32) : c.Lit * 0.85f;
        if (hero)
        {
            int mh = ui.U(14);
            var sand = l.Night ? Color.Lerp(c.Near, new Color(90, 70, 56), 0.4f) : new Color(206, 172, 120);
            for (int m = 0; m < mh; m += s)
            {
                float f = m / (float)mh;
                int hw = 17 * k + (int)(f * ui.U(40));
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s), m < ui.U(3) ? sand : Color.Lerp(Kit.Mul(sand, 0.82f), c.Ground, f * 0.8f));
            }
            for (int st = 0; st < 5; st++)
            {
                int sy = baseY + st * ui.U(3), sw = ui.U(20) + st * ui.U(4);
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, ui.U(3)), Color.Lerp(clay, shade, 0.2f + st * 0.08f));
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, s), Color.Lerp(clay, c.Rim, 0.35f));
            }
        }
        else
            for (int m = 0; m < 10 * k; m += s)
            {
                float f = m / (float)(10 * k);
                int hw = 16 * k + (int)(f * 10 * k);
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s), Color.Lerp(Kit.Mul(c.Far, 0.85f), c.Far, MathF.Min(1, f * 1.2f)));
            }

        // A tapering tower: `bw` wide at the foot, `tw` at the top, `h` tall, its middle at `mx` (all in k).
        void Tower(float mx, float bw, float tw, float h, bool flag)
        {
            int top = baseY - (int)(h * k);
            for (int y = top; y < baseY; y += s)
            {
                float f = (baseY - y) / (h * k);
                int hw = (int)(MathHelper.Lerp(bw, tw, f) * k / 2);
                int x0 = cx + (int)(mx * k) - hw;
                ui.Rect(new Rectangle(x0, y, 2 * hw, s), clay);
                ui.Rect(new Rectangle(x0 + 2 * hw - Math.Max(s, hw / 4), y, Math.Max(s, hw / 4), s), shade); // the shaded side
                if (rimLight.A > 0 && hero) ui.Rect(new Rectangle(x0, y, s, s), Color.Lerp(clay, c.Rim, 0.35f));
            }
            int thw = (int)(tw * k / 2), tx0 = cx + (int)(mx * k) - thw;
            // The relief band under the top: a row of small diamonds cut into the clay.
            int band = top + 2 * k;
            ui.Rect(new Rectangle(tx0, band, 2 * thw, s), dark);
            for (int x = tx0 + a; x + 2 * a < tx0 + 2 * thw; x += 3 * a)
            {
                ui.Rect(new Rectangle(x + a / 2, band + s, a, a), dark);
                ui.Rect(new Rectangle(x, band + s + a / 2, 2 * a, Math.Max(s, a / 2)), dark);
            }
            ui.Rect(new Rectangle(tx0, band + 2 * a + s, 2 * thw, s), dark);
            Merlons(tx0 - a, tx0 + 2 * thw + a, top, clay, Math.Max(s, a * 2 / 3));
            if (hero) Toron(tx0 + a, tx0 + 2 * thw - a, top + 5 * k, wood, Math.Max(s, a * 2 / 3));
            ui.Rect(new Rectangle(cx + (int)(mx * k) - s, top + 7 * k, Math.Max(s, a), 2 * k), slit);
            ui.Rect(new Rectangle(cx + (int)(mx * k) - s, top + 12 * k, Math.Max(s, a), 2 * k), slit);
            if (!flag) return;
            int pole = cx + (int)(mx * k), poleTop = top - 8 * k;
            ui.Rect(new Rectangle(pole, poleTop, s, 6 * k), dark);
            ui.Rect(new Rectangle(pole + s, poleTop, 5 * k, 2 * k + wave), cloth);
            ui.Rect(new Rectangle(pole + s + 5 * k, poleTop + s, k, k + wave), cloth);
        }
        var rim0 = rimLight;
        rimLight = hero ? c.Rim : Color.Transparent;
        // The curtain between the towers.
        int wallTop = baseY - 12 * k;
        ui.Rect(new Rectangle(cx - 13 * k, wallTop, 26 * k, 12 * k), clay);
        Merlons(cx - 13 * k, cx + 13 * k, wallTop, clay, Math.Max(s, a * 2 / 3));
        if (hero) { Toron(cx - 12 * k, cx - 5 * k, wallTop + 3 * k, wood, Math.Max(s, a * 2 / 3)); Toron(cx + 5 * k, cx + 12 * k, wallTop + 3 * k, wood, Math.Max(s, a * 2 / 3)); }
        Tower(-13, 7, 5, 21, false);
        Tower(13, 7, 5, 21, false);
        Tower(0, 10, 7.5f, 27, true);
        // The gate: a horseshoe arch in the gate tower, studded doors, torches.
        int gw = 4 * k, gh = 8 * k;
        HorseshoeArch(cx - gw / 2, baseY, gw, gh, l.Day ? new Color(46, 32, 28) : Color.Lerp(new Color(70, 40, 26), c.Lit, hero ? 0.2f : 0.15f), dark);
        if (hero)
        {
            ui.Rect(new Rectangle(cx - gw / 2 - k, baseY - gh - k, gw + 2 * k, s), dark); // the arch's frame of carved plaster
            foreach (int dx in new[] { -gw / 2, gw / 2 - k })
            {
                ui.Rect(new Rectangle(cx + dx, baseY - gh + 2 * k, k, gh - 2 * k), wood);
                for (int y = baseY - gh + 3 * k; y < baseY - k; y += 2 * k) ui.Rect(new Rectangle(cx + dx + k / 2 - s / 2, y, s, s), new Color(200, 180, 120)); // studs
            }
            if (!l.Day)
                foreach (int side in new[] { -1, 1 })
                {
                    var tp = new Vector2(cx + side * (gw / 2 + ui.U(6)), baseY - 5 * k);
                    ui.Disc(tp, ui.U(9), c.Lit * 0.14f); ui.Disc(tp, ui.U(4), c.Lit * 0.28f);
                    ui.Rect(new Rectangle((int)tp.X - s, (int)tp.Y - ui.U(2), ui.U(2), ui.U(3)), new Color(255, 228, 148));
                }
        }
        rimLight = rim0;
    }

    // ---------- the ribat ----------

    /// The southern barracks, a ribat: a tapering watchtower of clay with merlons and beam ends, a whitewashed hall with a
    /// horseshoe gate under a band of tiles and the realm's banners, and the yard.
    void Ribat(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color clay = Clay(l, c), lime = Lime(l, c), wood = SouthWood(l), shade = Kit.Mul(clay, 0.8f);
        int ground = r.Bottom, s = Math.Max(1, ui.U(1));
        // The hall.
        var hall = new Rectangle(r.X + ui.U(22), ground - ui.U(40), ui.U(52), ui.U(40));
        ui.Rect(hall, lime);
        Merlons(hall.X, hall.Right, hall.Y, lime, ui.U(2));
        ui.Rect(new Rectangle(hall.Right - ui.U(4), hall.Y, ui.U(4), hall.Height), Kit.Mul(lime, 0.8f));
        Edges(hall.X, hall.Y, hall.Width, hall.Height, lime);
        TileBand(hall.X, hall.Right, hall.Y + ui.U(4), l);
        HorseshoeArch(hall.X + ui.U(18), ground, ui.U(16), ui.U(24), l.Day ? new Color(46, 32, 28) : Color.Lerp(new Color(70, 40, 26), c.Lit, 0.4f), Kit.Mul(lime, 0.72f));
        foreach (int dx in new[] { 0, 9 }) // studded cedar doors, ajar
        {
            var leaf = new Rectangle(hall.X + ui.U(18 + dx), ground - ui.U(17), ui.U(7), ui.U(17));
            ui.Rect(leaf, wood);
            ui.Rect(new Rectangle(leaf.X + ui.U(3), leaf.Y, s, leaf.Height), Kit.Mul(wood, 0.7f));
            for (int y = leaf.Y + ui.U(3); y < leaf.Bottom - ui.U(2); y += ui.U(5))
                foreach (int sx in new[] { 1, 5 }) ui.Rect(new Rectangle(leaf.X + ui.U(sx), y, s, s), new Color(200, 180, 120));
        }
        foreach (int bx in new[] { hall.X + ui.U(5), hall.Right - ui.U(14) })
        {
            ui.Rect(new Rectangle(bx - s, hall.Y + ui.U(10), ui.U(10), s), wood);
            ui.Rect(new Rectangle(bx, hall.Y + ui.U(11), ui.U(8), ui.U(16)), cloth);
            for (int x = bx; x < bx + ui.U(8); x += ui.U(2)) ui.Rect(new Rectangle(x, hall.Y + ui.U(27), s, ui.U(2)), Kit.Mul(cloth, 0.8f)); // its fringe
            ui.Rect(new Rectangle(bx + ui.U(3), hall.Y + ui.U(16), ui.U(2), ui.U(6)), Color.Lerp(cloth, Kit.Gilt, 0.6f));
        }
        if (!l.Day) StarLantern(hall.X + ui.U(26), hall.Y + ui.U(10), l, c, ui.U(3));
        // The tower.
        int top = ground - ui.U(66), bw = ui.U(24), tw = ui.U(18);
        for (int y = top; y < ground; y += s)
        {
            float f = (ground - y) / (float)(ground - top);
            int w = (int)MathHelper.Lerp(bw, tw, f), x0 = r.X + (bw - w) / 2;
            ui.Rect(new Rectangle(x0, y, w, s), clay);
            ui.Rect(new Rectangle(x0 + w - ui.U(4), y, ui.U(4), s), shade);
        }
        int tx0 = r.X + (bw - tw) / 2;
        ui.Rect(new Rectangle(tx0, top + ui.U(6), tw, s), Kit.Mul(clay, 0.62f));
        for (int x = tx0 + ui.U(2); x < tx0 + tw - ui.U(2); x += ui.U(4)) ui.Rect(new Rectangle(x, top + ui.U(7), ui.U(2), ui.U(2)), Kit.Mul(clay, 0.62f));
        Merlons(tx0 - ui.U(1), tx0 + tw + ui.U(1), top, clay, ui.U(2));
        Toron(tx0 + ui.U(2), tx0 + tw - ui.U(2), top + ui.U(14), wood, ui.U(1.5f));
        Toron(r.X + ui.U(3), r.X + bw - ui.U(3), top + ui.U(38), wood, ui.U(1.5f));
        foreach (int sy in new[] { 20, 44 })
            ui.Rect(new Rectangle(r.X + ui.U(10), top + ui.U(sy), ui.U(3), ui.U(7)), l.Day ? new Color(52, 36, 32) : c.Lit * 0.85f);
        int pole = r.X + ui.U(12);
        ui.Rect(new Rectangle(pole, top - ui.U(20), s, ui.U(16)), wood);
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * ui.U(1));
        ui.Rect(new Rectangle(pole + s, top - ui.U(20), ui.U(14), ui.U(7) + wave), cloth);
        ui.Rect(new Rectangle(pole + s, top - ui.U(17), ui.U(14), s), Color.Lerp(cloth, Kit.Gilt, 0.5f));
        Yard(hall.Right + ui.U(2), r.Right, ground, l, cloth);
    }

    // ---------- the majlis ----------

    /// The southern elders' house, a majlis: a flat-roofed whitewashed hall with stepped merlons, a dome on a drum over its
    /// middle, an arcade of three horseshoe arches for its porch with cushions within, a lattice window either side, a palm.
    void Majlis(Rectangle r, TownLook l, TownColours c, Color cloth, float t, List<(int X, int W)> lights)
    {
        Color lime = Lime(l, c), wood = SouthWood(l);
        int ground = r.Bottom, s = Math.Max(1, ui.U(1)), wallTop = ground - ui.U(34);
        rimLight = c.Rim;
        var drum = new Rectangle(r.Center.X - ui.U(12), wallTop - ui.U(7), ui.U(24), ui.U(7));
        ui.Rect(drum, Kit.Mul(lime, 0.94f));
        for (int x = drum.X + ui.U(3); x < drum.Right - ui.U(2); x += ui.U(5)) ui.Rect(new Rectangle(x, drum.Y + ui.U(2), ui.U(2), ui.U(3)), l.Day ? new Color(60, 50, 48) : c.Lit * 0.8f);
        Dome(drum.Center.X, drum.Y, ui.U(13), ui.U(15), l.Night ? Color.Lerp(c.Near, new Color(200, 190, 170), 0.3f) : Kit.Mul(lime, 0.96f));
        ui.Rect(new Rectangle(drum.Center.X, drum.Y - ui.U(20), s, ui.U(5)), l.Day ? Kit.Gilt : Kit.Mul(Kit.Gilt, 0.5f));
        ui.Rect(new Rectangle(r.X, wallTop, r.Width, ground - wallTop), lime);
        Merlons(r.X, r.Right, wallTop, lime, ui.U(2));
        ui.Rect(new Rectangle(r.Right - ui.U(6), wallTop, ui.U(6), ground - wallTop), Kit.Mul(lime, 0.8f));
        Edges(r.X, wallTop, r.Width, ground - wallTop, lime);
        TileBand(r.X, r.Right, wallTop + ui.U(3), l);
        var inside = l.Day ? new Color(70, 50, 44) : Color.Lerp(new Color(110, 60, 36), c.Lit, 0.55f);
        for (int i = 0; i < 3; i++)
        {
            int ax = r.Center.X - ui.U(25) + i * ui.U(18);
            HorseshoeArch(ax, ground, ui.U(14), ui.U(20), i == 1 && !l.Day ? new Color(255, 200, 120) : inside, Kit.Mul(lime, 0.76f));
            ui.Rect(new Rectangle(ax + ui.U(1), ground - ui.U(4), ui.U(12), ui.U(4)), Kit.Mul(cloth, l.Night ? 0.5f : 0.85f));
            if (!l.Day) lights.Add((ax, ui.U(14)));
        }
        int poleX = r.X + ui.U(8), poleY = wallTop - ui.U(18);
        ui.Rect(new Rectangle(poleX, poleY, s, ui.U(16)), wood);
        int wave = (int)MathF.Round(MathF.Sin(t * 2.8f) * s);
        ui.Rect(new Rectangle(poleX + s, poleY, ui.U(11), ui.U(5) + wave), cloth);
    }

    // ---------- the great mosque ----------

    /// The southern landmark over the roofs: a great dome on its drum over a cube of a prayer hall, and beside it a square
    /// minaret with a gallery under merlons, a narrower stage over it and a little domed lantern with its finial; flat in the far
    /// colour, its lamps lit at night. `x` is the minaret's middle.
    void GreatMosque(int x, int ground, Color c, bool night, Color lit)
    {
        int s = Math.Max(1, ui.U(1));
        var rim0 = rimLight;
        if (rimLight.A > 0) rimLight = Color.Lerp(c, rimLight, 0.3f);
        var dark = Kit.Mul(c, 0.82f);
        // The prayer hall and its dome, to the left of the minaret.
        int hx = x - ui.U(52);
        ui.Rect(new Rectangle(hx, ground - ui.U(46), ui.U(44), ui.U(46)), c);
        Merlons(hx, hx + ui.U(44), ground - ui.U(46), c, ui.U(2));
        ui.Rect(new Rectangle(hx + ui.U(8), ground - ui.U(56), ui.U(28), ui.U(10)), c);
        Dome(hx + ui.U(22), ground - ui.U(56), ui.U(18), ui.U(20), c);
        ui.Rect(new Rectangle(hx + ui.U(22), ground - ui.U(82), s, ui.U(6)), c);
        // The minaret.
        ui.Rect(new Rectangle(x - ui.U(6), ground - ui.U(82), ui.U(12), ui.U(82)), c);
        ui.Rect(new Rectangle(x + ui.U(3), ground - ui.U(82), ui.U(3), ui.U(82)), dark);
        ui.Rect(new Rectangle(x - ui.U(8), ground - ui.U(84), ui.U(16), ui.U(3)), c); // the gallery
        Merlons(x - ui.U(8), x + ui.U(8), ground - ui.U(84), c, s);
        ui.Rect(new Rectangle(x - ui.U(4), ground - ui.U(98), ui.U(8), ui.U(13)), c);
        Dome(x, ground - ui.U(98), ui.U(4), ui.U(5), c);
        ui.Rect(new Rectangle(x, ground - ui.U(106), s, ui.U(4)), c); // the finial: a short rod and a ball on it
        ui.Rect(new Rectangle(x - s, ground - ui.U(109), 3 * s, 3 * s), c);
        foreach (int wy in new[] { 30, 52, 70 }) ui.Rect(new Rectangle(x - s, ground - ui.U(wy), ui.U(2), ui.U(4)), night ? lit * 0.8f : dark);
        if (night)
        {
            ui.Rect(new Rectangle(x - ui.U(2), ground - ui.U(94), ui.U(4), ui.U(4)), lit * 0.9f);
            foreach (int wx in new[] { 6, 16, 26, 36 }) ui.Rect(new Rectangle(hx + ui.U(wx), ground - ui.U(32), ui.U(3), ui.U(6)), lit * 0.75f);
        }
        rimLight = rim0;
    }
}
