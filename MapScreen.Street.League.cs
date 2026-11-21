using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the League's pieces of the street (red brick, stepped gables, green copper): the brewhouse inn (their tavern), the
/// brick burg with its copper spire (their keep), the armoury (their barracks), the town hall with its show front (their elders'
/// house), the brick church spire over the harbour cranes (their landmark), and their brick houses; the parts they share: brick
/// courses, stepped gables set with white blind niches, white-framed windows, hoist beams. Drawing only.
partial class MapScreen
{
    /// Red brick, its mortar, the whitewashed trim and niches, copper gone green, the painted green of doors.
    static Color Brick(TownLook l, TownColours c) => HeroWall(l, c, new Color(160, 80, 60));
    static Color Trim(TownLook l, TownColours c) => HeroWall(l, c, new Color(230, 222, 204));
    static Color Copper(TownLook l) => l.Night ? new Color(58, 84, 86) : new Color(98, 154, 134);
    static Color DoorGreen(TownLook l) => l.Night ? new Color(30, 44, 40) : new Color(50, 86, 66);

    /// Brick courses over a wall: mortar every three units and staggered joints.
    void Bricks(Rectangle r, Color brick)
    {
        int s = Math.Max(1, ui.U(1));
        var mortar = Kit.Mul(brick, 0.84f);
        for (int row = 0, y = r.Y + ui.U(3); y < r.Bottom; row++, y += ui.U(3))
        {
            ui.Rect(new Rectangle(r.X, y, r.Width, s), mortar);
            for (int x = r.X + ui.U(row % 2 == 0 ? 3 : 6); x < r.Right; x += ui.U(6)) ui.Rect(new Rectangle(x, y - ui.U(3) + s, s, ui.U(3) - s), mortar);
        }
    }

    /// A stepped gable on a front `w` wide at `baseY`: `steps` steps rising `h` in all, each capped with a lighter coping, and the
    /// gable set with tall white blind niches that climb with it.
    void Stepped(int x, int baseY, int w, int h, int steps, Color brick, Color niche)
    {
        int s = Math.Max(1, ui.U(1)), sh = h / steps;
        for (int i = 0; i < steps; i++)
        {
            int inset = i * w / (2 * steps + 1), y = baseY - (i + 1) * sh;
            ui.Rect(new Rectangle(x + inset, y, w - 2 * inset, sh), brick);
            ui.Rect(new Rectangle(x + inset - s, y, ui.U(4) + s, ui.U(2)), Color.Lerp(brick, niche, 0.35f)); // the copings
            ui.Rect(new Rectangle(x + w - inset - ui.U(4), y, ui.U(4) + s, ui.U(2)), Color.Lerp(brick, niche, 0.35f));
            if (snowCap.A > 0) { ui.Rect(new Rectangle(x + inset - s, y - ui.U(2), ui.U(4) + s, ui.U(2)), snowCap); ui.Rect(new Rectangle(x + w - inset - ui.U(4), y - ui.U(2), ui.U(4) + s, ui.U(2)), snowCap); }
            if (rimLight.A > 0) ui.Rect(new Rectangle(x + w - inset - s, y, s, sh), Color.Lerp(brick, rimLight, 0.5f));
        }
        // The niches: one in each bay between the steps, as tall as the gable lets it be.
        int bays = Math.Max(1, steps * 2 - 1), bw = w / (bays + 1);
        for (int b = 0; b < bays; b++)
        {
            int nx = x + bw / 2 + b * bw + bw / 4, mid = Math.Abs(b - (bays - 1) / 2);
            int top = baseY - h + (mid + 1) * sh + ui.U(2), nh = baseY - top - ui.U(4);
            if (nh < ui.U(6) || bw / 2 < ui.U(2)) continue;
            Niche(nx, top, Math.Max(ui.U(2), bw / 2), nh, niche);
        }
    }

    /// A tall blind niche with a pointed head, in whitewash.
    void Niche(int x, int y, int w, int h, Color col)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(x, y + w / 2, w, h - w / 2), col);
        for (int k = 0; k < w / 2; k += s) ui.Rect(new Rectangle(x + w / 2 - k - s, y + k, 2 * (k + s), s), col);
    }

    /// A window framed in white: small leaded panes, lit at night (and then on the list if it is low).
    void LeagueWindow(Rectangle w, TownLook l, TownColours c, Color trim, bool on, List<(int X, int W)>? lights)
    {
        int s = Math.Max(1, ui.U(1));
        on &= !l.Day;
        if (on) ui.Rect(new Rectangle(w.X - ui.U(3), w.Y - ui.U(3), w.Width + ui.U(6), w.Height + ui.U(6)), c.Lit * 0.15f);
        ui.Rect(new Rectangle(w.X - ui.U(1), w.Y - ui.U(1), w.Width + ui.U(2), w.Height + ui.U(2)), trim);
        ui.Rect(w, on ? c.Lit : new Color(46, 48, 62));
        var lead = on ? Kit.Mul(c.Lit, 0.7f) : Kit.Mul(trim, 0.7f);
        ui.Rect(new Rectangle(w.Center.X, w.Y, s, w.Height), trim);
        for (int y = w.Y + ui.U(4); y < w.Bottom; y += ui.U(4)) ui.Rect(new Rectangle(w.X, y, w.Width, s), lead);
        if (on && lights != null) lights.Add((w.X, w.Width));
    }

    /// A hoist beam out from a gable at (x, y), its pulley, the rope and what hangs on it.
    void Hoist(int x, int y, TownLook l, Color load)
    {
        int s = Math.Max(1, ui.U(1));
        var wood = l.Night ? new Color(40, 30, 30) : new Color(76, 52, 38);
        ui.Rect(new Rectangle(x - ui.U(2), y, ui.U(12), ui.U(2)), wood);
        ui.Rect(new Rectangle(x + ui.U(8), y + ui.U(2), ui.U(2), ui.U(2)), Kit.Mul(wood, 1.4f));
        ui.Rect(new Rectangle(x + ui.U(9), y + ui.U(4), s, ui.U(10)), Kit.Mul(wood, 1.6f));
        ui.Rect(new Rectangle(x + ui.U(6), y + ui.U(14), ui.U(7), ui.U(6)), load);
        ui.Rect(new Rectangle(x + ui.U(6), y + ui.U(16), ui.U(7), s), Kit.Mul(load, 0.7f));
    }

    /// A League house in a row: brick under a stepped gable, white-framed windows in rows, a green door, now and then a hoist.
    void LeagueHouse(int x, int ground, int w, int h, TownLook l, TownColours c, Random rng, List<(int X, int W)> lights, float t)
    {
        int s = Math.Max(1, ui.U(1));
        var brick = c.Roof;
        var trim = Color.Lerp(c.Wall, Color.White, backRow ? 0f : 0.2f);
        int steps = w > ui.U(30) ? 4 : 3, gh = Math.Max(ui.U(10), w * 2 / 3);
        ui.Rect(new Rectangle(x, ground - h, w, h), brick);
        Stepped(x, ground - h, w, gh, steps, brick, Color.Lerp(brick, trim, 0.55f));
        ui.Rect(new Rectangle(x, ground - ui.U(3), w, ui.U(3)), Kit.Mul(brick, 0.78f));
        if (rimLight.A > 0) ui.Rect(new Rectangle(x + w - s, ground - h, s, h), Color.Lerp(brick, rimLight, 0.45f));
        int door = rng.Next(Math.Max(1, (w - ui.U(8)) / ui.U(8)));
        for (int row = 0; ground - ui.U(12) - row * ui.U(11) > ground - h; row++)
            for (int i = 0, wx = x + ui.U(3); wx + ui.U(4) < x + w - ui.U(1); i++, wx += ui.U(8))
            {
                int wy = ground - ui.U(12) - row * ui.U(11);
                if (row == 0 && i == door) { ui.Rect(new Rectangle(wx, ground - ui.U(9), ui.U(5), ui.U(9)), Color.Lerp(DoorGreen(l), brick, backRow ? 0.6f : 0f)); continue; }
                ui.Rect(new Rectangle(wx - s, wy - s, ui.U(4) + 2 * s, ui.U(6) + 2 * s), trim);
                Glass(wx, wy, ui.U(4), ui.U(6), c.Wall, l, c, rng.Next(5) < 3, row == 0, lights);
            }
        if (w > ui.U(22) && rng.Next(3) == 0) Hoist(x + w / 2 - ui.U(1), ground - h - gh + ui.U(4), l, Kit.Mul(new Color(150, 120, 80), l.Night ? 0.45f : 1f));
    }

    // ---------- the brewhouse inn ----------

    /// The League tavern: a brewer's house under a tall stepped gable with its niches and the hoist with a barrel at the top, the
    /// great pointed door of its hall and white-framed windows; beside it the low brewhouse under dark pantiles with a hoist
    /// dormer and a smoking chimney, kegs stacked by the door and a ship on the sign.
    List<(int X, int W)> Brewhouse(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color brick = Brick(l, c), trim = Trim(l, c), green = DoorGreen(l);
        Color tile = l.Night ? new Color(66, 44, 52) : new Color(122, 62, 50);
        var lights = new List<(int X, int W)>();
        int s = Math.Max(1, ui.U(1)), split = r.X + ui.U(100);
        rimLight = c.Rim;
        // The brewhouse wing at the right: chimney, pantile roof, hoist dormer.
        var wing = new Rectangle(split, r.Y + ui.U(10), r.Right - split, r.Height - ui.U(10));
        ui.Rect(new Rectangle(wing.Right - ui.U(18), wing.Y - ui.U(34), ui.U(9), ui.U(26)), Kit.Mul(brick, 0.9f));
        Smoke(wing.Right - ui.U(14), wing.Y - ui.U(36), t, l.Night);
        int rh = ui.U(24);
        for (int y = 0; y < rh; y += s)
        {
            int i = y * ui.U(10) / rh;
            var row = (y / ui.U(4)) % 2 == 0 ? tile : Kit.Mul(tile, 0.86f);
            if (snowCap.A > 0 && y > rh / 3) row = snowCap;
            ui.Rect(new Rectangle(wing.X + i, wing.Y - y - s, wing.Width + ui.U(4) - 2 * i, s), row);
        }
        for (int x = wing.X + ui.U(4); x < wing.Right; x += ui.U(4)) ui.Rect(new Rectangle(x, wing.Y - ui.U(6), s, ui.U(6)), Kit.Mul(tile, 0.75f)); // the pans' ridges at the eave
        ui.Rect(new Rectangle(wing.X, wing.Y - s, wing.Width + ui.U(4), ui.U(2)), Kit.Mul(tile, 0.6f));
        var dormer = new Rectangle(wing.X + ui.U(24), wing.Y - ui.U(20), ui.U(14), ui.U(16));
        ui.Rect(dormer, brick);
        Gable(dormer.X - s, dormer.Y, dormer.Width + 2 * s, 0, ui.U(7), tile);
        ui.Rect(new Rectangle(dormer.X + ui.U(3), dormer.Y + ui.U(3), ui.U(8), ui.U(11)), l.Day ? new Color(46, 36, 34) : Kit.Mul(c.Lit, 0.6f)); // the loading door
        Hoist(dormer.X + ui.U(6), dormer.Y - ui.U(3), l, Kit.Mul(new Color(170, 140, 96), l.Night ? 0.45f : 1f));
        ui.Rect(wing, brick);
        Bricks(wing, brick);
        ui.Rect(new Rectangle(wing.X, wing.Y, wing.Width, ui.U(2)), Color.Black * 0.24f);
        foreach (int wx in new[] { 6, 24, 42 }) LeagueWindow(new Rectangle(wing.X + ui.U(wx), wing.Y + ui.U(5), ui.U(9), ui.U(12)), l, c, trim, true, null);
        var low = new Rectangle(wing.X + ui.U(6), r.Bottom - ui.U(24), ui.U(20), ui.U(16));
        LeagueWindow(low, l, c, trim, true, lights);
        var bdoor = new Rectangle(wing.X + ui.U(31), r.Bottom - ui.U(20), ui.U(11), ui.U(20));
        ui.Rect(bdoor, l.Day ? green : new Color(255, 200, 120)); // the brewhouse door, ajar at night
        ui.Rect(new Rectangle(bdoor.X, bdoor.Y, bdoor.Width, s), trim);
        if (!l.Day) { ui.Rect(new Rectangle(bdoor.X, bdoor.Y + s, ui.U(4), bdoor.Height - s), green); lights.Add((bdoor.X, bdoor.Width)); }
        Edges(wing.X, wing.Y, wing.Width, wing.Height, brick);
        // The gabled house at the left: its gable, then the front.
        var house = new Rectangle(r.X, r.Y, split - r.X, r.Height);
        Stepped(house.X, house.Y, house.Width, ui.U(44), 5, brick, trim);
        ui.Rect(house, brick);
        Bricks(house, brick);
        Bricks(new Rectangle(house.X + house.Width / 5, house.Y - ui.U(26), house.Width * 3 / 5, ui.U(26)), brick);
        Hoist(house.Center.X - ui.U(1), house.Y - ui.U(40), l, Kit.Mul(new Color(132, 92, 56), l.Night ? 0.45f : 1f));
        ui.Rect(new Rectangle(house.X - s, house.Y + ui.U(22), house.Width + 2 * s, ui.U(2)), trim); // a white band between the floors
        if (rimLight.A > 0) ui.Rect(new Rectangle(house.Right - s, house.Y, s, house.Height), Color.Lerp(brick, rimLight, 0.45f));
        foreach (int wx in new[] { 10, 45, 80 }) LeagueWindow(new Rectangle(house.X + ui.U(wx), house.Y + ui.U(5), ui.U(10), ui.U(14)), l, c, trim, true, null);
        // The hall door, pointed, two storeys of it nearly, green leaves; a window either side.
        int dx = house.X + ui.U(39);
        var arch = l.Day ? green : new Color(255, 200, 120);
        Niche(dx - ui.U(2), r.Bottom - ui.U(36), ui.U(26), ui.U(36), trim);
        Niche(dx, r.Bottom - ui.U(34), ui.U(22), ui.U(34), arch);
        if (l.Day) ui.Rect(new Rectangle(dx + ui.U(11), r.Bottom - ui.U(24), s, ui.U(24)), Kit.Mul(green, 0.6f));
        else
        {
            ui.Rect(new Rectangle(dx, r.Bottom - ui.U(22), ui.U(7), ui.U(22)), green); // a leaf swung in
            lights.Add((dx, ui.U(22)));
            for (int k = 0; k < ui.U(8); k += s)
                ui.Rect(new Rectangle(dx - k * 2, r.Bottom + k, ui.U(22) + 4 * k, s), c.Lit * (0.6f * (1 - MathF.Floor(k * 3f / ui.U(8)) / 3f)));
            Lantern(new Vector2(dx - ui.U(6), r.Bottom - ui.U(26)), c, green);
        }
        foreach (int wx in new[] { 10, 76 }) LeagueWindow(new Rectangle(house.X + ui.U(wx), r.Bottom - ui.U(30), ui.U(12), ui.U(18)), l, c, trim, true, lights);
        // Kegs stacked by the brewhouse door and the ship sign.
        var keg = l.Night ? new Color(70, 50, 40) : new Color(140, 96, 58);
        foreach (var (kx, ky) in new[] { (44, 0), (52, 0), (48, 8) })
        {
            var kr = new Rectangle(wing.X + ui.U(kx), r.Bottom - ui.U(8) - ui.U(ky), ui.U(8), ui.U(8));
            ui.Rect(kr, keg);
            ui.Rect(new Rectangle(kr.X, kr.Y + ui.U(2), kr.Width, s), Kit.Mul(keg, 0.6f));
            ui.Rect(new Rectangle(kr.X, kr.Bottom - ui.U(3), kr.Width, s), Kit.Mul(keg, 0.6f));
        }
        CogSign(wing.Right - ui.U(2), wing.Y + ui.U(14), l, c, cloth);
        return lights;
    }

    /// The inn's sign: a board on an iron arm with a cog under sail painted on it.
    static readonly string[] CogMap =
    {
        "......#......",
        "...#######...",
        "...#######...",
        "...#######...",
        "...#######...",
        "......#......",
        "#############",
        ".###########.",
        "..#########..",
    };

    void CogSign(int x, int y, TownLook l, TownColours c, Color cloth)
    {
        int s = Math.Max(1, ui.U(1));
        var iron = l.Night ? new Color(30, 28, 36) : new Color(46, 44, 50);
        ui.Rect(new Rectangle(x, y, ui.U(20), ui.U(2)), iron);
        ui.Line(new Vector2(x, y + ui.U(10)), new Vector2(x + ui.U(10), y + ui.U(2)), iron, 2);
        var board = new Rectangle(x + ui.U(3), y + ui.U(5), ui.U(18), ui.U(15));
        ui.Rect(new Rectangle(board.X + ui.U(2), y + ui.U(2), s, ui.U(3)), iron);
        ui.Rect(new Rectangle(board.Right - ui.U(3), y + ui.U(2), s, ui.U(3)), iron);
        if (!l.Day) ui.Disc(board.Center.ToVector2(), ui.U(14), c.Lit * 0.1f);
        ui.Rect(board, l.Night ? new Color(40, 50, 70) : new Color(64, 96, 140)); // painted sea blue
        ui.Frame(board, l.Night ? new Color(120, 96, 50) : Kit.Gilt, s);
        var ink = l.Night ? new Color(200, 180, 140) : new Color(240, 226, 196);
        int cell = s;
        for (int row = 0; row < CogMap.Length; row++)
            for (int col = 0; col < CogMap[row].Length; col++)
                if (CogMap[row][col] == '#')
                    ui.Rect(new Rectangle(board.X + ui.U(3) + col * cell, board.Y + ui.U(2) + row * cell, cell, cell), row < 5 && col > 3 && col < 9 ? (row == 2 ? Kit.Mul(cloth, 1.2f) : ink) : row >= 6 ? new Color(120, 76, 46) : ink);
    }

    // ---------- the burg ----------

    /// The League keep: a brick hall under a great stepped gable, a square tower beside it with a band of white niches and a tall
    /// spire of green copper, a little copper-capped turret at the corner, the curtain before them with a white-trimmed gate.
    /// `scale` pixels make a keep unit; `hero` is the castle's own.
    void Burg(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        int k = ui.U(scale), s1 = Math.Max(1, ui.U(1));
        int wave = (int)MathF.Round(MathF.Sin(t * 3.2f) * s1);
        Color brick = hero ? Brick(l, c) : Color.Lerp(c.Far, new Color(150, 80, 64), l.Night ? 0.2f : 0.4f);
        Color shade = Kit.Mul(brick, 0.8f), dark = Kit.Mul(brick, 0.6f), litEdge = Color.Lerp(brick, c.Rim, hero ? 0.45f : 0.25f);
        Color trim = hero ? Trim(l, c) : Color.Lerp(brick, c.Far, 0.6f);
        Color copper = hero ? Copper(l) : Color.Lerp(c.Far, Copper(l), 0.5f);
        var rim0 = rimLight;
        rimLight = hero ? c.Rim : Color.Transparent;
        KeepBase(cx, baseY, k, l, c, hero, brick, shade, dark, litEdge);
        void Box(int x, int y, int w, int h, Color col) => ui.Rect(new Rectangle(cx + x * k, baseY - y * k, w * k, h * k), col);

        // The tower at the right with its spire.
        Box(4, 26, 7, 26, brick);
        Box(4, 26, 2, 26, shade);
        if (hero) Bricks(new Rectangle(cx + 6 * k, baseY - 26 * k, 5 * k, 26 * k), brick);
        for (int n = 0; n < 3; n++) Niche(cx + (5 + n * 2) * k, baseY - 24 * k, k, 5 * k, trim);
        Box(4, 27, 7, 1, Color.Lerp(brick, trim, 0.3f));
        Cone(cx + 7 * k + k / 2, baseY - 27 * k, 4 * k, 10 * k, copper, Tip(l));
        if (!l.Day) ui.Rect(new Rectangle(cx + 7 * k, baseY - 18 * k, k, 2 * k), c.Lit * 0.85f);
        // The hall under its gable.
        Box(-12, 20, 16, 20, Kit.Mul(brick, 0.97f));
        if (hero) Bricks(new Rectangle(cx - 12 * k, baseY - 20 * k, 16 * k, 20 * k), brick);
        Stepped(cx - 12 * k, baseY - 20 * k, 16 * k, 11 * k, 5, Kit.Mul(brick, 0.97f), trim);
        Box(-12, 20, 16, 1, Color.Lerp(brick, trim, 0.4f));
        foreach (int wx in new[] { -10, -6, -2 })
        {
            var wr = new Rectangle(cx + wx * k, baseY - 17 * k, 2 * k, 4 * k);
            if (hero) LeagueWindow(wr, l, c, trim, wx != -6, null); // square-headed, framed in white: a hall, not a church
            else if (!l.Day && wx != -6) ui.Rect(new Rectangle(wr.X, wr.Y + k, k, 2 * k), c.Lit * 0.9f);
        }
        if (rimLight.A > 0) ui.Rect(new Rectangle(cx + 4 * k - s1, baseY - 20 * k, s1, 20 * k), litEdge);
        // The corner turret at the left.
        Box(-15, 16, 3, 16, Kit.Mul(brick, 0.92f));
        Box(-15, 16, 1, 16, shade);
        Cone(cx - 13 * k - k / 2, baseY - 16 * k, 2 * k, 6 * k, copper, Tip(l));
        // The curtain with its white arcade frieze and the gate.
        Box(-15, 8, 30, 8, brick);
        if (hero) Bricks(new Rectangle(cx - 15 * k, baseY - 8 * k, 30 * k, 8 * k), brick);
        for (int a = -14; a < 15; a += 2) Niche(cx + a * k + k / 4, baseY - 8 * k + k / 2, k / 2 + s1, k, trim);
        for (int m = -15; m < 15; m += 2) Box(m, 9, 1, 1, brick);
        ui.Rect(new Rectangle(cx - 15 * k, baseY - 8 * k, 30 * k, s1), litEdge);
        Niche(cx - 2 * k - k / 2, baseY - 7 * k, 5 * k, 7 * k, trim);
        Niche(cx - 2 * k, baseY - 6 * k - k / 2, 4 * k, 6 * k + k / 2, l.Day ? new Color(40, 30, 30) : Color.Lerp(new Color(70, 40, 26), c.Lit, 0.4f));
        // The standard off the gable's peak.
        int poleX = cx - 4 * k, poleTop = baseY - 35 * k;
        ui.Rect(new Rectangle(poleX, poleTop, s1, 4 * k), dark);
        ui.Rect(new Rectangle(poleX + s1, poleTop, 5 * k, 2 * k + wave), cloth);
        ui.Rect(new Rectangle(poleX + s1 + 5 * k, poleTop + s1, k, k + wave), cloth);
        rimLight = rim0;
    }

    // ---------- the armoury ----------

    /// The League barracks: the town's armoury, brick with its stepped gable to the street, the great pointed door studded with
    /// iron and the town's shield over it, halberds leaning by it, the banner off the gable; the yard beside it.
    void Armoury(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        Color brick = Brick(l, c), trim = Trim(l, c), green = DoorGreen(l);
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * s1);
        var hall = new Rectangle(r.X, ground - ui.U(42), ui.U(60), ui.U(42));
        Stepped(hall.X, hall.Y, hall.Width, ui.U(26), 4, brick, trim);
        ui.Rect(hall, brick);
        Bricks(hall, brick);
        Bricks(new Rectangle(hall.X + hall.Width / 5, hall.Y - ui.U(16), hall.Width * 3 / 5, ui.U(16)), brick);
        ui.Rect(new Rectangle(hall.X - s1, hall.Y, hall.Width + 2 * s1, ui.U(2)), trim);
        if (rimLight.A > 0) ui.Rect(new Rectangle(hall.Right - s1, hall.Y, s1, hall.Height), Color.Lerp(brick, rimLight, 0.45f));
        // The door.
        int dx = hall.X + ui.U(20);
        Niche(dx - ui.U(2), ground - ui.U(30), ui.U(24), ui.U(30), trim);
        Niche(dx, ground - ui.U(28), ui.U(20), ui.U(28), l.Day ? green : Color.Lerp(new Color(70, 40, 26), c.Lit, 0.45f));
        var stud = l.Night ? new Color(70, 70, 78) : new Color(40, 40, 44);
        if (l.Day) for (int y = ground - ui.U(18); y < ground - ui.U(2); y += ui.U(4)) for (int x = dx + ui.U(3); x < dx + ui.U(18); x += ui.U(4)) ui.Rect(new Rectangle(x, y, s1, s1), stud);
        ui.Rect(new Rectangle(dx + ui.U(10), ground - ui.U(22), s1, ui.U(22)), Kit.Mul(green, 0.6f));
        // The town's shield over the door, the windows either side, the banner on the gable.
        var shield = new Rectangle(dx + ui.U(6), hall.Y + ui.U(5), ui.U(8), ui.U(8));
        ui.Rect(shield, cloth);
        ui.Rect(new Rectangle(shield.X + ui.U(2), shield.Bottom, ui.U(4), ui.U(2)), cloth);
        ui.Rect(new Rectangle(shield.X + ui.U(3), shield.Y + ui.U(2), ui.U(2), ui.U(5)), Color.Lerp(cloth, Color.White, 0.6f));
        ui.Frame(shield, trim, s1);
        foreach (int wx in new[] { 5, 46 }) LeagueWindow(new Rectangle(hall.X + ui.U(wx), hall.Y + ui.U(8), ui.U(9), ui.U(14)), l, c, trim, true, null);
        int pole = hall.Center.X;
        ui.Rect(new Rectangle(pole, hall.Y - ui.U(40), s1, ui.U(16)), Kit.Mul(brick, 0.5f));
        ui.Rect(new Rectangle(pole + s1, hall.Y - ui.U(40), ui.U(14), ui.U(7) + wave), cloth);
        ui.Rect(new Rectangle(pole + s1, hall.Y - ui.U(37), ui.U(14), s1), Kit.Mul(cloth, 0.7f));
        // Halberds leaning by the door.
        var shaft = l.Night ? new Color(60, 44, 36) : new Color(120, 86, 54);
        var steel = l.Night ? new Color(120, 124, 140) : new Color(200, 204, 214);
        foreach (int hx in new[] { dx - ui.U(8), dx - ui.U(5) })
        {
            ui.Line(new Vector2(hx, ground), new Vector2(hx + ui.U(3), ground - ui.U(30)), shaft, 1);
            ui.Rect(new Rectangle(hx + ui.U(2), ground - ui.U(33), ui.U(4), ui.U(4)), steel);
        }
        if (!l.Day)
        {
            var tp = new Vector2(dx + ui.U(24), ground - ui.U(18));
            ui.Disc(tp, ui.U(7), c.Lit * 0.14f); ui.Disc(tp, ui.U(3), c.Lit * 0.28f);
            ui.Rect(new Rectangle((int)tp.X - s1, (int)tp.Y - ui.U(2), ui.U(2), ui.U(3)), new Color(255, 228, 148));
        }
        Yard(r.X + ui.U(62), r.Right, ground, l, cloth);
    }

    // ---------- the town hall ----------

    /// The League elders' house: a brick town hall over a pointed arcade, its show front rising above the roof in tall white
    /// niches pierced with round openings, slender turrets with copper caps along its top.
    void Rathaus(Rectangle r, TownLook l, TownColours c, Color cloth, float t, List<(int X, int W)> lights)
    {
        int ground = r.Bottom, s1 = Math.Max(1, ui.U(1));
        int wallH = ui.U(34), wallTop = ground - wallH, screen = ui.U(20);
        Color brick = Brick(l, c), trim = Trim(l, c), copper = Copper(l);
        var body = new Rectangle(r.X, wallTop - screen, r.Width, wallH + screen);
        ui.Rect(body, brick);
        Bricks(body, brick);
        Edges(body.X, body.Y, body.Width, body.Height, brick);
        // The show front: niches with round openings, turrets with copper caps.
        for (int i = 0; i < 4; i++)
        {
            int nx = r.X + ui.U(8 + i * 16);
            Niche(nx, body.Y + ui.U(3), ui.U(8), ui.U(16), trim);
            ui.Rect(new Rectangle(nx + ui.U(3), body.Y + ui.U(9), ui.U(2), ui.U(8)), Kit.Mul(brick, 0.7f)); // a slit through the screen
        }
        foreach (int tx in new[] { 0, 22, 46, 68 })
        {
            int px = r.X + ui.U(tx);
            ui.Rect(new Rectangle(px, body.Y - ui.U(6), ui.U(6), ui.U(6) + ui.U(4)), Kit.Mul(brick, 0.94f));
            Cone(px + ui.U(3), body.Y - ui.U(6), ui.U(4), ui.U(8), copper, Tip(l));
        }
        ui.Rect(new Rectangle(r.X - s1, wallTop, r.Width + 2 * s1, ui.U(2)), trim);
        if (rimLight.A > 0) ui.Rect(new Rectangle(r.Right - s1, body.Y, s1, body.Height), Color.Lerp(brick, rimLight, 0.45f));
        // Upstairs windows, the arcade under them.
        foreach (int wx in new[] { 8, 32, 56 }) LeagueWindow(new Rectangle(r.X + ui.U(wx), wallTop + ui.U(4), ui.U(10), ui.U(10)), l, c, trim, true, null);
        var inside = l.Day ? new Color(60, 46, 44) : Color.Lerp(new Color(100, 60, 36), c.Lit, 0.5f);
        for (int i = 0; i < 3; i++)
        {
            int ax = r.X + ui.U(5 + i * 23);
            Niche(ax - ui.U(1), ground - ui.U(18), ui.U(20), ui.U(18), trim);
            Niche(ax + ui.U(1), ground - ui.U(16), ui.U(16), ui.U(16), i == 1 && !l.Day ? new Color(255, 200, 120) : inside);
            if (!l.Day) lights.Add((ax, ui.U(18)));
        }
        if (!l.Day)
            for (int k = 0; k < ui.U(6); k += s1)
                ui.Rect(new Rectangle(r.X + ui.U(29) - k * 2, ground + k, ui.U(16) + 4 * k, s1), c.Lit * (0.5f * (1 - MathF.Floor(k * 3f / ui.U(6)) / 3f)));
        int wave = (int)MathF.Round(MathF.Sin(t * 2.6f) * s1);
        ui.Rect(new Rectangle(r.X + ui.U(34), wallTop + ui.U(15), ui.U(6), ui.U(9) + wave), cloth);
    }

    // ---------- the church and the cranes ----------

    /// The League landmark: a brick hall church with its great west tower and a tall copper spire, flat in the far colour, the
    /// harbour cranes either side.
    void HallChurch(int x, int ground, Color c, bool night, Color lit)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(x + ui.U(22), ground - ui.U(46), ui.U(56), ui.U(46)), Kit.Mul(c, 0.94f)); // the hall
        Gable(x + ui.U(22), ground - ui.U(46), ui.U(56), 0, ui.U(26), Kit.Mul(c, 0.94f));
        for (int i = 0; i < 3; i++) ui.Rect(new Rectangle(x + ui.U(30 + i * 16), ground - ui.U(38), ui.U(5), ui.U(24)), night ? lit * 0.6f : Kit.Mul(c, 0.8f));
        ui.Rect(new Rectangle(x, ground - ui.U(92), ui.U(24), ui.U(92)), c); // the tower
        ui.Rect(new Rectangle(x + ui.U(23), ground - ui.U(92), s, ui.U(92)), Color.Lerp(c, rimLight.A > 0 ? rimLight : c, 0.5f));
        for (int n = 0; n < 3; n++) ui.Rect(new Rectangle(x + ui.U(3 + n * 7), ground - ui.U(84), ui.U(4), ui.U(20)), Kit.Mul(c, 0.84f)); // its niches
        ui.Rect(new Rectangle(x + ui.U(9), ground - ui.U(56), ui.U(6), ui.U(10)), night ? lit * 0.75f : Kit.Mul(c, 0.78f));
        for (int y = 0; y < ui.U(48); y += s) // the spire
        {
            int hw = (int)(ui.U(12) * (1 - y / (float)ui.U(48)));
            ui.Rect(new Rectangle(x + ui.U(12) - hw, ground - ui.U(92) - y - s, 2 * hw, s), Kit.Mul(c, 1.04f));
        }
        ui.Rect(new Rectangle(x + ui.U(12) - s / 2, ground - ui.U(146), s, ui.U(6)), c);
    }
}
