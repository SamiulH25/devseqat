using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the order's pieces of the street (grey dressed stone, lead roofs, the Ashen Sun in gilt): the pilgrims' hospice
/// (their tavern), the commandery with its chapel apse (their keep), the knights' hall under its gate tower (their barracks), the
/// priory hall under its bell gable (their elders' house); the parts they share: big coursed blocks, stepped buttresses, paired
/// round-headed windows, bell gables, long banners down a wall, the sun disc. Their landmark, the chapter house and sun tower,
/// is in `Landmark`. Drawing only.
partial class MapScreen
{
    /// Grey dressed stone, the lead of their roofs, the gilt of the sun.
    static Color Greystone(TownLook l, TownColours c) => HeroWall(l, c, new Color(164, 162, 168));
    static Color Lead(TownLook l, TownColours c) => l.Night ? new Color(52, 52, 66) : l.Dusk ? Color.Lerp(c.Near, new Color(92, 94, 104), 0.7f) : new Color(96, 98, 110);
    static Color SunGold(TownLook l) => l.Night ? new Color(176, 136, 76) : new Color(234, 192, 96);

    /// Big coursed blocks over a wall: a course every six units, joints staggered a block apart.
    void Dressed(Rectangle r, Color stone)
    {
        int s = Math.Max(1, ui.U(1));
        var joint = Kit.Mul(stone, 0.86f);
        for (int row = 0, y = r.Y + ui.U(6); y < r.Bottom; row++, y += ui.U(6))
        {
            ui.Rect(new Rectangle(r.X, y, r.Width, s), joint);
            for (int x = r.X + ui.U(row % 2 == 0 ? 7 : 14); x < r.Right; x += ui.U(14)) ui.Rect(new Rectangle(x, y - ui.U(6) + s, s, ui.U(6) - s), joint);
        }
    }

    /// The Ashen Sun: a gilt disc with eight rays, a darker heart.
    void AshenSun(Vector2 at, int r, Color gold)
    {
        for (int i = 0; i < 8; i++) { float a = i * MathF.PI / 4; ui.Line(at, at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, gold, Math.Max(1, ui.U(1))); }
        ui.Disc(at, r * 0.55f, gold);
        ui.Disc(at, r * 0.25f, Kit.Mul(gold, 0.75f));
    }

    /// A stepped buttress standing proud of a wall at `x`, `h` high, `w` wide at its foot.
    void Buttress(int x, int ground, int h, int w, Color stone)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(x, ground - h * 2 / 3, w, h * 2 / 3), Kit.Mul(stone, 0.92f));
        ui.Rect(new Rectangle(x + w / 4, ground - h, w * 3 / 4, h / 3 + s), Kit.Mul(stone, 0.92f));
        var edge = rimLight.A > 0 ? Color.Lerp(stone, rimLight, 0.4f) : Kit.Mul(stone, 1.08f);
        ui.Rect(new Rectangle(x, ground - h * 2 / 3, w, s), edge); // the weathering
        ui.Rect(new Rectangle(x + w - s, ground - h, s, h), edge);
    }

    /// A bell gable: a little wall `w` wide rising `h` from `baseY` with an arched opening, the bell hung in it, a gabled cap.
    void Bellcote(int cx, int baseY, int w, int h, Color stone, Color bell, Color roof)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(cx - w / 2, baseY - h, w, h), stone);
        int ow = w / 2;
        RoundArch(cx - ow / 2, baseY - ui.U(3), ow, h - ui.U(6), Kit.Mul(stone, 0.45f));
        ui.Rect(new Rectangle(cx - ui.U(2), baseY - h + ui.U(5), ui.U(4), ui.U(4)), bell);
        ui.Rect(new Rectangle(cx - ui.U(3), baseY - h + ui.U(8), ui.U(6), s), bell);
        Gable(cx - w / 2 - s, baseY - h, w + 2 * s, 0, w / 2, roof);
        ui.Rect(new Rectangle(cx - s / 2, baseY - h - w / 2 - ui.U(4), s, ui.U(4)), bell);
    }

    /// A pair of round-headed windows under one frame, lit at night.
    void Bifora(int x, int y, int w, int h, TownLook l, TownColours c, Color stone, bool on)
    {
        on &= !l.Day;
        if (on) ui.Rect(new Rectangle(x - ui.U(3), y - ui.U(3), w + ui.U(6), h + ui.U(6)), c.Lit * 0.15f);
        RoundArch(x, y + h, w, h, Kit.Mul(stone, 0.8f));
        var glass = on ? c.Lit : new Color(42, 42, 54);
        int half = (w - ui.U(3)) / 2;
        RoundArch(x + ui.U(1), y + h, half, h - ui.U(2), glass);
        RoundArch(x + w - ui.U(1) - half, y + h, half, h - ui.U(2), glass);
    }

    /// A long banner hung down a wall from (x, top): the realm's cloth, the sun in gilt on it, cut to a point at the foot.
    void LongBanner(int x, int top, int w, int h, Color cloth, Color gold, float t)
    {
        int s = Math.Max(1, ui.U(1)), sway = (int)MathF.Round(MathF.Sin(t * 1.6f + x * 0.01f) * s);
        ui.Rect(new Rectangle(x - s, top - s, w + 2 * s, ui.U(2)), Kit.Mul(cloth, 0.5f)); // the bar it hangs from
        ui.Rect(new Rectangle(x, top, w, h), cloth);
        for (int k = 0; k < w / 2; k += s) // the point ui.Rect(new Rectangle(x + k + sway, top + h + k, w - 2 * k, s), cloth);
        ui.Rect(new Rectangle(x, top, s, h), Kit.Mul(cloth, 0.8f));
        AshenSun(new Vector2(x + w / 2f, top + h * 0.4f), w / 3, gold);
    }

    // ---------- the hospice ----------

    /// The order's tavern: the pilgrims' hospice, a long hall of grey stone under a lead roof, a bell gable on its end; an arcade
    /// of round arches along its foot where pilgrims sit, the middle one its door; paired windows above, the order's long banner
    /// at its corner and the sun hung out on an iron arm for a sign.
    List<(int X, int W)> Hospice(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color stone = Greystone(l, c), lead = Lead(l, c), gold = SunGold(l), wood = l.Night ? new Color(40, 30, 32) : new Color(86, 60, 42);
        var lights = new List<(int X, int W)>();
        int s = Math.Max(1, ui.U(1)), roofH = ui.U(22), floor = r.Y + ui.U(28);
        rimLight = c.Rim;
        // Chimney and bell gable behind the roof line, the roof.
        ui.Rect(new Rectangle(r.X + ui.U(24), r.Y - ui.U(22), ui.U(8), ui.U(16)), Kit.Mul(stone, 0.9f));
        Smoke(r.X + ui.U(28), r.Y - ui.U(24), t, l.Night);
        rimLight = Color.Transparent; // the lead takes no bright edge: it would stair up the slope
        Gable(r.X - ui.U(4), r.Y, r.Width + ui.U(8), 0, roofH, lead);
        rimLight = c.Rim;
        for (int k = s; k < roofH; k += s)
        {
            int inset = (int)(k / (float)roofH * (r.Width + ui.U(8)) / 2);
            if (snowCap.A > 0 && k > roofH / 3) ui.Rect(new Rectangle(r.X - ui.U(4) + inset, r.Y - k, r.Width + ui.U(8) - 2 * inset, s), snowCap);
            else if (k % ui.U(3) == 0) ui.Rect(new Rectangle(r.X - ui.U(4) + inset, r.Y - k, r.Width + ui.U(8) - 2 * inset, s), Kit.Mul(lead, 0.82f));
        }
        ui.Rect(new Rectangle(r.X - ui.U(5), r.Y - s, r.Width + ui.U(10), ui.U(2)), Kit.Mul(lead, 0.6f));
        Bellcote(r.Right - ui.U(16), r.Y + s, ui.U(16), ui.U(22), stone, gold, lead);
        // The walls.
        ui.Rect(r, stone);
        Dressed(r, stone);
        ui.Rect(new Rectangle(r.X, r.Y, r.Width, ui.U(3)), Color.Black * 0.24f);
        ui.Rect(new Rectangle(r.X - s, floor, r.Width + 2 * s, ui.U(2)), Color.Lerp(stone, c.Rim, 0.35f));
        Edges(r.X, r.Y, r.Width, r.Height, stone);
        foreach (int wx in new[] { 22, 62, 102 }) Bifora(r.X + ui.U(wx), r.Y + ui.U(7), ui.U(16), ui.U(16), l, c, stone, true);
        // The arcade.
        var inside = l.Day ? new Color(58, 54, 58) : Color.Lerp(new Color(100, 60, 36), c.Lit, 0.5f);
        for (int i = 0; i < 5; i++)
        {
            int ax = r.X + ui.U(7 + i * 30);
            bool door = i == 2;
            RoundArch(ax, r.Bottom, ui.U(22), ui.U(26), door && !l.Day ? new Color(255, 200, 120) : inside, Kit.Mul(stone, 0.82f));
            if (door)
            {
                if (l.Day) { ui.Rect(new Rectangle(ax + ui.U(3), r.Bottom - ui.U(18), ui.U(16), ui.U(18)), wood); ui.Rect(new Rectangle(ax + ui.U(11), r.Bottom - ui.U(18), s, ui.U(18)), Kit.Mul(wood, 0.6f)); }
                else
                {
                    lights.Add((ax, ui.U(22)));
                    for (int k = 0; k < ui.U(8); k += s)
                        ui.Rect(new Rectangle(ax - k * 2, r.Bottom + k, ui.U(22) + 4 * k, s), c.Lit * (0.6f * (1 - MathF.Floor(k * 3f / ui.U(8)) / 3f)));
                }
                continue;
            }
            ui.Rect(new Rectangle(ax + ui.U(2), r.Bottom - ui.U(6), ui.U(18), ui.U(2)), wood); // the pilgrims' bench
            ui.Rect(new Rectangle(ax + ui.U(3), r.Bottom - ui.U(4), s, ui.U(4)), wood);
            ui.Rect(new Rectangle(ax + ui.U(18), r.Bottom - ui.U(4), s, ui.U(4)), wood);
            if (!l.Day) { lights.Add((ax + ui.U(4), ui.U(14))); Lantern(new Vector2(ax + ui.U(11), r.Bottom - ui.U(18)), c, wood); }
        }
        LongBanner(r.X + ui.U(4), r.Y + ui.U(4), ui.U(12), ui.U(20), cloth, gold, t);
        // The sign: the sun on an iron arm.
        var iron = l.Night ? new Color(30, 28, 36) : new Color(46, 44, 50);
        int sx = r.Right, sy = floor + ui.U(4);
        ui.Rect(new Rectangle(sx - ui.U(2), sy, ui.U(20), ui.U(2)), iron);
        ui.Line(new Vector2(sx, sy + ui.U(10)), new Vector2(sx + ui.U(10), sy + ui.U(2)), iron, 2);
        ui.Rect(new Rectangle(sx + ui.U(13), sy + ui.U(2), s, ui.U(3)), iron);
        if (!l.Day) ui.Disc(new Vector2(sx + ui.U(13), sy + ui.U(12)), ui.U(13), c.Lit * 0.1f);
        AshenSun(new Vector2(sx + ui.U(13), sy + ui.U(12)), ui.U(8), gold);
        return lights;
    }

    // ---------- the commandery ----------

    /// The order's keep: a commandery, a massive grey donjon with buttress strips, a frieze of little arches under its corbelled
    /// parapet and the order's long banner down its face; a square tower at the right flying the colours, the chapel at the left
    /// with its round apse and bell gable, a low curtain with the gate before them.
    void Commandery(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        int k = ui.U(scale), s1 = Math.Max(1, ui.U(1));
        int wave = (int)MathF.Round(MathF.Sin(t * 3.2f) * s1);
        Color stone = hero ? Greystone(l, c) : Color.Lerp(c.Far, c.Mid, 0.5f);
        Color shade = Kit.Mul(stone, hero ? 0.78f : 0.86f), dark = Kit.Mul(stone, hero ? 0.52f : 0.68f);
        Color litEdge = Color.Lerp(stone, c.Rim, hero ? 0.45f : 0.25f);
        Color lead = hero ? Lead(l, c) : Color.Lerp(c.Far, Lead(l, c), 0.5f), gold = hero ? SunGold(l) : Color.Lerp(c.Far, SunGold(l), 0.5f);
        var rim0 = rimLight;
        rimLight = hero ? c.Rim : Color.Transparent;
        KeepBase(cx, baseY, k, l, c, hero, stone, shade, dark, litEdge);
        void Box(int x, int y, int w, int h, Color col) => ui.Rect(new Rectangle(cx + x * k, baseY - y * k, w * k, h * k), col);

        // The square tower at the right.
        Box(6, 31, 6, 31, Kit.Mul(stone, 0.95f));
        Box(6, 31, 2, 31, shade);
        for (int m = 6; m < 12; m += 2) Box(m, 32, 1, 1, Kit.Mul(stone, 0.95f));
        foreach (int sy in new[] { 14, 22 }) if (hero || (!l.Day && sy == 22)) ui.Rect(new Rectangle(cx + 9 * k, baseY - sy * k, s1 + s1, 2 * k), l.Day ? dark : c.Lit * 0.85f);
        int poleTop = baseY - 37 * k;
        ui.Rect(new Rectangle(cx + 9 * k, poleTop, s1, 5 * k), dark);
        ui.Rect(new Rectangle(cx + 9 * k + s1, poleTop, 5 * k, 2 * k + wave), cloth);
        ui.Rect(new Rectangle(cx + 9 * k + s1 + 5 * k, poleTop + s1, k, k + wave), cloth);
        // The donjon.
        Box(-8, 26, 14, 26, stone);
        if (hero) Dressed(new Rectangle(cx - 8 * k, baseY - 26 * k, 14 * k, 26 * k), stone);
        foreach (int px in new[] { -8, -2, 5 }) Box(px, 24, 1, 24, Kit.Mul(stone, 0.9f)); // buttress strips
        Box(-8, 26, 14, 2, Kit.Mul(stone, 0.95f));
        for (int a = -8; a < 6; a++) ui.Rect(new Rectangle(cx + a * k + k / 4, baseY - 24 * k, k / 2, k / 2), dark); // the frieze of little arches
        ui.Rect(new Rectangle(cx - 8 * k - s1, baseY - 27 * k, 14 * k + 2 * s1, k), Kit.Mul(stone, 1.04f)); // the corbelled parapet
        for (int m = -8; m < 6; m += 2) Box(m, 28, 1, 1, Kit.Mul(stone, 1.04f));
        if (rimLight.A > 0) ui.Rect(new Rectangle(cx + 6 * k - s1, baseY - 26 * k, s1, 26 * k), litEdge);
        if (hero) foreach (int wx in new[] { -6, 3 }) Bifora(cx + wx * k, baseY - 21 * k, 2 * k, 3 * k, l, c, stone, true);
        else if (!l.Day) ui.Rect(new Rectangle(cx - 5 * k, baseY - 20 * k, k, 2 * k), c.Lit * 0.85f);
        LongBanner(cx - 2 * k, baseY - 23 * k, 4 * k, 9 * k, cloth, gold, t);
        // The chapel at the left, its apse and bell gable.
        Box(-14, 13, 7, 13, Kit.Mul(stone, 0.96f));
        Box(-14, 13, 1, 13, shade);
        Dome(cx - 10 * k - k / 2, baseY - 13 * k, 4 * k, 3 * k, lead);
        if (hero || !l.Day) ui.Disc(new Vector2(cx - 10 * k - k / 2, baseY - 9 * k), k, l.Day ? dark : c.Lit * 0.85f); // a round window
        if (hero) Bellcote(cx - 10 * k - k / 2, baseY - 16 * k, 3 * k, 4 * k, stone, gold, lead);
        // The curtain and the gate.
        Box(-15, 8, 30, 8, stone);
        if (hero) Dressed(new Rectangle(cx - 15 * k, baseY - 8 * k, 30 * k, 8 * k), stone);
        for (int m = -15; m < 15; m += 2) Box(m, 9, 1, 1, stone);
        ui.Rect(new Rectangle(cx - 15 * k, baseY - 8 * k, 30 * k, s1), litEdge);
        RoundArch(cx - 2 * k, baseY, 4 * k, 6 * k, l.Day ? new Color(36, 30, 30) : Color.Lerp(new Color(70, 40, 26), c.Lit, 0.4f), hero ? Kit.Mul(stone, 0.82f) : null);
        if (hero) AshenSun(new Vector2(cx, baseY - 7 * k), k, gold);
        rimLight = rim0;
    }

    // ---------- the knights' hall ----------

    /// The order's barracks: a gate tower with a machicolated top and the long banner down it, the knights' hall beside it with
    /// stepped buttresses between its tall windows; the yard beyond.
    void KnightsHall(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        Color stone = Greystone(l, c), dark = Kit.Mul(stone, 0.55f), lead = Lead(l, c), gold = SunGold(l);
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * s1);
        // The hall.
        var hall = new Rectangle(r.X + ui.U(24), ground - ui.U(40), ui.U(38), ui.U(40));
        Gable(hall.X - ui.U(2), hall.Y, hall.Width + ui.U(4), 0, ui.U(12), lead);
        ui.Rect(hall, stone);
        Dressed(hall, stone);
        ui.Rect(new Rectangle(hall.X, hall.Y, hall.Width, ui.U(2)), Color.Black * 0.24f);
        foreach (int wx in new[] { 6, 24 })
            RoundArch(hall.X + ui.U(wx), hall.Y + ui.U(30), ui.U(7), ui.U(22), l.Day ? new Color(42, 42, 54) : c.Lit, Kit.Mul(stone, 0.8f));
        foreach (int bx in new[] { 15, 34 }) Buttress(hall.X + ui.U(bx), ground, ui.U(30), ui.U(5), stone);
        // The gate tower.
        var tower = new Rectangle(r.X, ground - ui.U(64), ui.U(26), ui.U(64));
        ui.Rect(tower, stone);
        Dressed(tower, stone);
        ui.Rect(new Rectangle(tower.X, tower.Y, ui.U(5), tower.Height), Kit.Mul(stone, 0.8f));
        ui.Rect(new Rectangle(tower.X - s1, tower.Y - ui.U(3), tower.Width + 2 * s1, ui.U(3)), Kit.Mul(stone, 1.04f));
        for (int x = tower.X; x < tower.Right - ui.U(2); x += ui.U(6)) ui.Rect(new Rectangle(x, tower.Y - ui.U(7), ui.U(4), ui.U(4)), Kit.Mul(stone, 1.04f));
        for (int x = tower.X + ui.U(1); x < tower.Right; x += ui.U(4)) ui.Rect(new Rectangle(x, tower.Y, ui.U(2), ui.U(2)), dark);
        if (rimLight.A > 0) ui.Rect(new Rectangle(tower.Right - s1, tower.Y, s1, tower.Height), Color.Lerp(stone, rimLight, 0.5f));
        int gx = tower.X + ui.U(6);
        RoundArch(gx, ground, ui.U(14), ui.U(22), l.Day ? new Color(36, 30, 28) : Color.Lerp(new Color(60, 36, 24), c.Lit, 0.35f), Kit.Mul(stone, 0.82f));
        for (int x = gx + ui.U(2); x < gx + ui.U(14); x += ui.U(4)) ui.Rect(new Rectangle(x, ground - ui.U(18), s1, ui.U(12)), new Color(72, 68, 70));
        LongBanner(tower.X + ui.U(8), tower.Y + ui.U(6), ui.U(10), ui.U(22), cloth, gold, t);
        int pole = tower.X + ui.U(13);
        ui.Rect(new Rectangle(pole, tower.Y - ui.U(22), s1, ui.U(15)), dark);
        ui.Rect(new Rectangle(pole + s1, tower.Y - ui.U(22), ui.U(14), ui.U(7) + wave), cloth);
        ui.Rect(new Rectangle(pole + s1, tower.Y - ui.U(19), ui.U(14), s1), Kit.Mul(cloth, 0.7f));
        if (!l.Day)
        {
            var tp = new Vector2(gx + ui.U(18), ground - ui.U(16));
            ui.Disc(tp, ui.U(7), c.Lit * 0.14f); ui.Disc(tp, ui.U(3), c.Lit * 0.28f);
            ui.Rect(new Rectangle((int)tp.X - s1, (int)tp.Y - ui.U(2), ui.U(2), ui.U(3)), new Color(255, 228, 148));
        }
        Yard(r.X + ui.U(64), r.Right, ground, l, cloth);
    }

    // ---------- the priory hall ----------

    /// The order's elders' house: a priory hall of grey stone, its gable to the street with a bell gable of two bells on the
    /// apex, a deep round-arched door of three orders with the sun carved over it, a small window either side.
    void Priory(Rectangle r, TownLook l, TownColours c, Color cloth, float t, List<(int X, int W)> lights)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        int wallH = ui.U(34), wallTop = ground - wallH, gableH = ui.U(22);
        Color stone = Greystone(l, c), lead = Lead(l, c), gold = SunGold(l), wood = l.Night ? new Color(40, 30, 32) : new Color(86, 60, 42);
        // The bell gable on the apex, the roof seen beyond the gable, the gable.
        int bx = r.Center.X, by = wallTop - gableH + ui.U(2);
        ui.Rect(new Rectangle(bx - ui.U(12), by - ui.U(16), ui.U(24), ui.U(16)), stone);
        foreach (int ox in new[] { -9, 2 })
        {
            RoundArch(bx + ui.U(ox), by - ui.U(3), ui.U(7), ui.U(11), Kit.Mul(stone, 0.45f));
            ui.Rect(new Rectangle(bx + ui.U(ox + 2), by - ui.U(11), ui.U(3), ui.U(3)), gold);
        }
        Gable(bx - ui.U(13), by - ui.U(16), ui.U(26), 0, ui.U(8), lead);
        ui.Rect(new Rectangle(bx - s1 / 2, by - ui.U(28), s1, ui.U(4)), gold);
        Gable(r.X - ui.U(3), wallTop, r.Width + ui.U(6), 0, gableH + ui.U(2), Kit.Mul(lead, 0.9f));
        Gable(r.X, wallTop, r.Width, 0, gableH, stone);
        ui.Rect(new Rectangle(r.X, wallTop, r.Width, wallH), stone);
        Dressed(new Rectangle(r.X, wallTop, r.Width, wallH), stone);
        Edges(r.X, wallTop, r.Width, wallH, stone);
        var oc = new Vector2(bx, wallTop - gableH / 3); // the oculus: a stone ring, a cross of tracery in it
        ui.Disc(oc, ui.U(5), Kit.Mul(stone, 0.72f));
        ui.Disc(oc, ui.U(4), l.Day ? new Color(42, 42, 54) : c.Lit * 0.7f);
        ui.Rect(new Rectangle((int)oc.X - ui.U(4), (int)oc.Y - s1 / 2, ui.U(8), s1), Kit.Mul(stone, 0.72f));
        ui.Rect(new Rectangle((int)oc.X - s1 / 2, (int)oc.Y - ui.U(4), s1, ui.U(8)), Kit.Mul(stone, 0.72f));
        // The door of three orders, the sun over it.
        int dw = ui.U(16), dx = bx - dw / 2;
        RoundArch(dx - ui.U(4), ground, dw + ui.U(8), ui.U(28), Kit.Mul(stone, 0.92f));
        RoundArch(dx - ui.U(2), ground, dw + ui.U(4), ui.U(25), Kit.Mul(stone, 0.8f));
        RoundArch(dx, ground, dw, ui.U(22), l.Day ? wood : new Color(255, 200, 120));
        if (l.Day) ui.Rect(new Rectangle(bx, ground - ui.U(14), s1, ui.U(14)), Kit.Mul(wood, 0.6f));
        else
        {
            lights.Add((dx, dw));
            for (int k = 0; k < ui.U(6); k += s1)
                ui.Rect(new Rectangle(dx - k * 2, ground + k, dw + 4 * k, s1), c.Lit * (0.5f * (1 - MathF.Floor(k * 3f / ui.U(6)) / 3f)));
        }
        AshenSun(new Vector2(bx, wallTop + ui.U(3)), ui.U(4), gold);
        foreach (int wx in new[] { 8, 56 })
        {
            Bifora(r.X + ui.U(wx), ground - ui.U(26), ui.U(10), ui.U(12), l, c, stone, true);
            if (!l.Day) lights.Add((r.X + ui.U(wx), ui.U(10)));
        }
        LongBanner(r.Right - ui.U(4), wallTop + ui.U(2), ui.U(8), ui.U(12), cloth, gold, t);
    }
}
