using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the houses and the tavern in the tavern tab's picture, built as the town's people build
/// (timber and plaster, whitewash and flat roofs, logs and turf, felt tents). Drawing only.
partial class MapScreen
{
    /// The houses in front of the wall, shoulder to shoulder, in the town's way of building: windows (lit at night),
    /// chimneys and their smoke. Returns each ground-floor light (x, width) for the water.
    List<(int X, int W)> HouseRow(int x0, int x1, int baseLine, TownLook l, TownColours c, Random rng, float t, bool front)
    {
        var lights = new List<(int X, int W)>();
        backRow = !front;
        for (int x = x0; x < x1 - ui.U(12);)
        {
            int w = Math.Min(ui.U(14 + rng.Next(28)) * (l.Build == "tent" ? 3 : 2) / 2, x1 - x), h = ui.U(12 + rng.Next(20)) - (front ? 0 : ui.U(2));
            int ground = baseLine + ui.U(rng.Next(3)); // the street is not a ruler
            switch (l.Build)
            {
                case "white": WhiteHouse(x, ground, w, h + ui.U(4), l, c, rng, lights); break;
                case "log": LogHouse(x, ground, w + ui.U(4), h - ui.U(2), l, c, rng, lights, t); w += ui.U(4); break;
                case "tent": Yurt(x, ground, w + ui.U(6), l, c, rng, lights, t); w += ui.U(10); break;
                default: TimberHouse(x, ground, w, h, l, c, rng, lights, t); break;
            }
            if (front && rng.Next(5) == 0 && x + w + ui.U(14) < x1) // a gap with a low wall or a shed in it
            {
                int gap = ui.U(8 + rng.Next(8));
                ui.Rect(new Rectangle(x + w, ground - ui.U(6), gap, ui.U(6)), Kit.Mul(c.Wall, 0.8f));
                if (rng.Next(2) == 0) Gable(x + w + ui.U(1), ground - ui.U(6), gap - ui.U(2), 0, ui.U(4), c.Roof);
                w += gap;
            }
            x += w + (rng.Next(4) == 0 ? ui.U(3) : 0);
        }
        if (!front) lights.Clear();
        backRow = false;
        return lights;
    }

    /// A small window: lit glass with a glow at night (some), dark glass by day; ground-floor lights go on the list for the water.
    void Glass(int x, int y, int w, int h, Color wall, TownLook l, TownColours c, bool on, bool low, List<(int X, int W)> lights)
    {
        on &= l.Night || l.Dusk;
        if (on) ui.Rect(new Rectangle(x - ui.U(1), y - ui.U(1), w + ui.U(2), h + ui.U(2)), c.Lit * 0.16f);
        ui.Rect(new Rectangle(x, y, w, h), on ? c.Lit : Kit.Mul(wall, l.Night ? 1.7f : 0.45f));
        if (on && low) lights.Add((x, w));
    }

    /// A timber-and-plaster house with a pitched roof (in League towns, often a brick house with a stepped gable).
    void TimberHouse(int x, int ground, int w, int h, TownLook l, TownColours c, Random rng, List<(int X, int W)> lights, float t)
    {
        int roof = rng.Next(3) switch { 0 => w / 2, 1 => w / 3, _ => w / 5 } + ui.U(rng.Next(3));
        if (l.Culture == "league" && rng.Next(3) > 0) { LeagueHouse(x, ground, w, h + ui.U(8), l, c, rng, lights, t); return; } // brick under a stepped gable
        {
            int s1 = Math.Max(1, ui.U(1));
            Gable(x - s1, ground - h, w + 2 * s1, 0, roof, c.Roof);
            for (int ty = ui.U(4); ty < roof; ty += ui.U(4))
            {
                int inset = (int)(ty / (float)roof * (w + 2 * s1) / 2);
                ui.Rect(new Rectangle(x - s1 + inset, ground - h - ty, w + 2 * s1 - 2 * inset, s1), Kit.Mul(c.Roof, 0.78f));
            }
            ui.Rect(new Rectangle(x, ground - h, w, h), c.Wall);
            ui.Rect(new Rectangle(x, ground - h, w, ui.U(2)), Color.Black * 0.22f); // eave shadow
            ui.Rect(new Rectangle(x, ground - ui.U(4), w, ui.U(4)), Kit.Mul(c.Wall, 0.78f)); // stone plinth
            Edges(x, ground - h, w, h, c.Wall);
            var beam = Kit.Mul(c.Wall, l.Night ? 0.55f : 0.48f);
            ui.Rect(new Rectangle(x, ground - h + ui.U(10), w, s1), beam); // horizontal beam
            ui.Rect(new Rectangle(x + s1, ground - h, s1, h - ui.U(4)), beam);
            ui.Rect(new Rectangle(x + w - 2 * s1, ground - h, s1, h - ui.U(4)), beam);
            if (w >= ui.U(24)) ui.Rect(new Rectangle(x + w / 2, ground - h, s1, ui.U(10)), beam);
        }
        if (rng.Next(4) == 0)
        {
            int cx = x + w * 3 / 4, cy = ground - h - roof / 2;
            ui.Rect(new Rectangle(cx, cy - ui.U(5), ui.U(3), ui.U(6)), c.Roof);
            Smoke(cx + ui.U(1), cy - ui.U(6), t + x * 0.01f, l.Night);
        }
        Openings(x, ground, w, h, l, c, rng, lights);
    }

    /// The windows and door of a small house, in one of four layouts: a grid, a door and a window, an upper row only, one wide window.
    void Openings(int x, int ground, int w, int h, TownLook l, TownColours c, Random rng, List<(int X, int W)> lights)
    {
        int layout = rng.Next(4);
        var door = Kit.Mul(c.Wall, l.Night ? 0.6f : 0.4f);
        switch (layout)
        {
            case 0:
                for (int row = 0; row < (h > ui.U(26) ? 2 : 1); row++)
                    for (int wx = x + ui.U(4); wx + ui.U(5) < x + w; wx += ui.U(8))
                        Glass(wx, ground - ui.U(11) - row * ui.U(11), ui.U(3), ui.U(4), c.Wall, l, c, rng.Next(5) < 3, row == 0, lights);
                break;
            case 1:
                ui.Rect(new Rectangle(x + ui.U(3), ground - ui.U(9), ui.U(5), ui.U(9)), door);
                if (w > ui.U(16)) Glass(x + w - ui.U(9), ground - ui.U(11), ui.U(4), ui.U(4), c.Wall, l, c, rng.Next(2) == 0, true, lights);
                break;
            case 2:
                for (int wx = x + ui.U(4); wx + ui.U(5) < x + w; wx += ui.U(7))
                    Glass(wx, ground - h + ui.U(4), ui.U(3), ui.U(3), c.Wall, l, c, rng.Next(3) == 0, false, lights);
                break;
            default:
                if (w > ui.U(14)) Glass(x + w / 2 - ui.U(4), ground - ui.U(12), ui.U(8), ui.U(5), c.Wall, l, c, rng.Next(3) > 0, true, lights);
                break;
        }
    }

    /// A whitewashed house with a flat roof and a parapet, arched windows; now and then a little dome.
    void WhiteHouse(int x, int ground, int w, int h, TownLook l, TownColours c, Random rng, List<(int X, int W)> lights)
    {
        ui.Rect(new Rectangle(x, ground - h, w, h), c.Wall);
        ui.Rect(new Rectangle(x - ui.U(1), ground - h - ui.U(2), w + ui.U(2), ui.U(2)), Kit.Mul(c.Wall, 0.85f)); // the parapet
        if (rng.Next(2) == 0) ui.Rect(new Rectangle(x + w / 3, ground - h - ui.U(8), w / 2, ui.U(6)), c.Wall); // a roof room, a step up
        ui.Rect(new Rectangle(x, ground - h, ui.U(3), h), Kit.Mul(c.Wall, 0.78f)); // the shaded side
        if (snowCap.A > 0) ui.Rect(new Rectangle(x - ui.U(1), ground - h - ui.U(3), w + ui.U(2), ui.U(1)), snowCap);
        if (rng.Next(5) == 0) Dome(x + w / 2, ground - h - ui.U(2), w / 3, w / 4, c.Roof);
        Edges(x, ground - h, w, h, c.Wall);
        for (int row = 0; row < (h > ui.U(26) ? 2 : 1); row++)
            for (int wx = x + ui.U(4); wx + ui.U(5) < x + w; wx += ui.U(9))
            {
                int wy = ground - ui.U(13) - row * ui.U(12);
                Glass(wx, wy, ui.U(3), ui.U(5), c.Wall, l, c, rng.Next(5) < 2, row == 0, lights);
                ui.Rect(new Rectangle(wx + ui.U(1), wy - ui.U(1), ui.U(1), ui.U(1)), Kit.Mul(c.Wall, l.Night ? 1.4f : 0.6f)); // the arch
            }
    }

    /// A log house under a steep turf roof, a small shuttered window, smoke out of the roof.
    void LogHouse(int x, int ground, int w, int h, TownLook l, TownColours c, Random rng, List<(int X, int W)> lights, float t)
    {
        int roof = w * 3 / 5;
        Gable(x - ui.U(2), ground - h, w + ui.U(4), 0, roof, c.Roof);
        Turf(x - ui.U(2), ground - h, w + ui.U(4), roof, c.Roof, l.Snow);
        ui.Rect(new Rectangle(x, ground - h, w, h), c.Wall);
        for (int ly = ground - h + ui.U(3); ly < ground; ly += ui.U(3)) ui.Rect(new Rectangle(x, ly, w, ui.U(1)), Kit.Mul(c.Wall, 0.7f)); // the logs
        Edges(x, ground - h, w, h, c.Wall);
        Glass(x + w / 2 - ui.U(2), ground - h + ui.U(5), ui.U(4), ui.U(4), c.Wall, l, c, rng.Next(2) == 0, true, lights);
        if (rng.Next(3) == 0) Smoke(x + w / 2, ground - h - roof, t + x * 0.01f, l.Night);
    }

    /// A felt tent (a yurt): a round wall and a low dome, a smoke ring at the crown, its door glowing at night.
    void Yurt(int x, int ground, int w, TownLook l, TownColours c, Random rng, List<(int X, int W)> lights, float t)
    {
        int drum = ui.U(7 + rng.Next(6));
        Color[] felts = { new(200, 170, 130), new(176, 150, 120), new(214, 204, 180), new(150, 112, 86) };
        var felt = Color.Lerp(c.Roof, felts[rng.Next(felts.Length)], l.Night ? 0.15f : 0.5f);
        var rim0 = rimLight;
        if (l.Night && rimLight.A > 0) rimLight = Color.Lerp(felt, rimLight, 0.3f); // moonlight only grazes the felt
        Dome(x + w / 2, ground - drum, w / 2, w / (3 + rng.Next(2)), felt);
        rimLight = rim0;
        ui.Rect(new Rectangle(x, ground - drum, w, drum), c.Wall);
        Color[] bands = { new(150, 50, 44), new(54, 84, 130), new(176, 130, 50) };
        var band = rng.Next(3) == 0 ? Color.Lerp(c.Wall, bands[rng.Next(bands.Length)], l.Night ? 0.3f : 0.75f) : Kit.Mul(c.Wall, 0.7f);
        ui.Rect(new Rectangle(x, ground - drum + ui.U(3), w, ui.U(2)), band); // the band round it
        Edges(x, ground - drum, w, drum, c.Wall);
        Glass(x + w / 2 - ui.U(2), ground - ui.U(7), ui.U(5), ui.U(7), c.Wall, l, c, rng.Next(3) > 0, true, lights);
        if (rng.Next(6) == 0) Smoke(x + w / 2, ground - drum - w / 3, t + x * 0.01f, l.Night);
    }

    /// Turf on a roof: a lighter green edge down both pitches and tufts of grass along it (none under snow).
    void Turf(int x, int eave, int w, int roof, Color c, bool snow)
    {
        if (snow) return;
        var grass = Color.Lerp(c, new Color(150, 176, 90), 0.45f);
        int s = Math.Max(1, ui.U(1));
        for (int k = 0; k < roof; k += s)
        {
            int inset = (int)(k / (float)roof * w / 2);
            ui.Rect(new Rectangle(x + inset, eave - k - s, s, s), grass);
            ui.Rect(new Rectangle(x + w - inset - s, eave - k - s, s, s), grass);
            if (k % ui.U(5) == 0) ui.Rect(new Rectangle(x + inset - s, eave - k - 2 * s, s, s), grass);
        }
        ui.Rect(new Rectangle(x, eave - s, w, s), grass);
    }

    /// The moon (or sun) catches a wall's right edge; the left one is a shade lighter than the wall, to part it from its neighbour.
    void Edges(int x, int y, int w, int h, Color wall)
    {
        if (rimLight.A == 0) return;
        ui.Rect(new Rectangle(x + w - ui.U(1), y, ui.U(1), h), Color.Lerp(wall, rimLight, 0.45f));
        ui.Rect(new Rectangle(x, y, ui.U(1), h), Color.Lerp(wall, rimLight, 0.18f));
    }

    /// A dome or tent roof: half an ellipse on `baseY`, snow on its crown in winter.
    void Dome(int cx, int baseY, int halfW, int h, Color c)
    {
        int s = Math.Max(1, ui.U(1));
        for (int y = 0; y < h; y += s)
        {
            float k = y / (float)h;
            int hw = (int)(halfW * MathF.Sqrt(1 - k * k));
            var col = snowCap.A > 0 && k > 0.6f ? snowCap : c;
            ui.Rect(new Rectangle(cx - hw, baseY - y - s, 2 * hw, s), col);
            if (rimLight.A > 0) ui.Rect(new Rectangle(cx + hw - s, baseY - y - s, s, s), rimLight);
        }
    }

    // ---------- the tavern, nearest the eye ----------

    /// The tavern in the town's way of building, the picture's first stop: lit door and windows, a lantern, the sign with its mug,
    /// its light on the ground before it. Returns its lights (x, width) for the water.
    List<(int X, int W)> Tavern(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        var saved = rimLight;
        var lights = l.Build switch
        {
            "white" => Caravanserai(r, l, c, cloth, t),
            "log" => MeadHall(r, l, c, cloth, t),
            "tent" => FeastTent(r, l, c, cloth, t),
            _ when l.Culture == "league" => Brewhouse(r, l, c, cloth, t),
            _ when l.Culture == "order" => Hospice(r, l, c, cloth, t),
            _ when l.Culture == "azure" => Auberge(r, l, c, cloth, t),
            _ => TimberTavern(r, l, c, t),
        };
        rimLight = saved;
        return lights;
    }

    /// The tavern's walls: brighter than the houses (it is the subject), warmed by its own light at night.
    static Color HeroWall(TownLook l, TownColours c, Color day) => l.Night ? Color.Lerp(Color.Lerp(c.Near, day, 0.3f), c.Lit, 0.08f) : l.Dusk ? Color.Lerp(c.Near, day, 0.65f) : day;

    /// A window of the tavern: a timber surround, mullions, lit glass with a halo by night.
    void TavernWindow(Rectangle w, TownLook l, TownColours c, Color frame, bool on, bool hearth = false)
    {
        if (hearth && l.Day) { ui.Rect(new Rectangle(w.X - ui.U(1), w.Y - ui.U(1), w.Width + ui.U(2), w.Height + ui.U(2)), frame); ui.Rect(w, new Color(150, 96, 52)); return; } // firelight within, even by day
        on &= !l.Day;
        if (on) ui.Rect(new Rectangle(w.X - ui.U(2), w.Y - ui.U(2), w.Width + ui.U(4), w.Height + ui.U(4)), c.Lit * 0.18f);
        ui.Rect(new Rectangle(w.X - ui.U(1), w.Y - ui.U(1), w.Width + ui.U(2), w.Height + ui.U(2)), frame);
        ui.Rect(w, on ? c.Lit : new Color(40, 44, 56));
        ui.Rect(new Rectangle(w.Center.X, w.Y, ui.U(1), w.Height), frame);
        ui.Rect(new Rectangle(w.X, w.Center.Y, w.Width, ui.U(1)), frame);
    }

    /// The door: lit and half open by night, its light spread on the ground before it; a lantern beside it.
    void TavernDoor(Rectangle door, int ground, TownLook l, TownColours c, Color frame, List<(int X, int W)> lights, bool lamp = true)
    {
        ui.Rect(new Rectangle(door.X - ui.U(2), door.Y - ui.U(3), door.Width + ui.U(4), door.Height + ui.U(3)), frame);
        ui.Rect(door, l.Day ? Kit.Mul(frame, 1.3f) : new Color(255, 200, 120));
        if (l.Day) return;
        ui.Rect(new Rectangle(door.X + ui.U(3), door.Y + ui.U(4), ui.U(6), door.Height - ui.U(4)), Kit.Mul(c.Lit, 0.8f));
        lights.Add((door.X, door.Width));
        int st = Math.Max(1, ui.U(1)), depth = ui.U(8);
        for (int k = 0; k < depth; k += st)
            ui.Rect(new Rectangle(door.X - k * 2, ground + k, door.Width + 4 * k, st), c.Lit * (0.6f * (1 - MathF.Floor(k * 3f / depth) / 3f)));
        if (!lamp) return;
        var at = new Vector2(door.X - ui.U(8), door.Y + ui.U(6));
        Lantern(at, c, frame);
    }

    /// A hanging lantern and its glow.
    void Lantern(Vector2 at, TownColours c, Color frame)
    {
        ui.Disc(at, ui.U(9), c.Lit * 0.12f); ui.Disc(at, ui.U(5), c.Lit * 0.2f);
        ui.Rect(new Rectangle((int)at.X - ui.U(2), (int)at.Y - ui.U(3), ui.U(4), ui.U(6)), new Color(255, 226, 150));
        ui.Rect(new Rectangle((int)at.X - ui.U(2), (int)at.Y - ui.U(4), ui.U(4), ui.U(1)), frame);
    }

    /// The sign: a gilt board with a mug, hung from a bracket at (ax, ay).
    void TavernSign(int ax, int ay, TownLook l, TownColours c, Color frame)
    {
        ui.Rect(new Rectangle(ax - ui.U(2), ay, ui.U(26), ui.U(2)), frame);
        ui.Line(new Vector2(ax, ay + ui.U(10)), new Vector2(ax + ui.U(12), ay + ui.U(2)), frame, 2);
        ui.Rect(new Rectangle(ax + ui.U(6), ay + ui.U(2), ui.U(1), ui.U(4)), frame);
        ui.Rect(new Rectangle(ax + ui.U(19), ay + ui.U(2), ui.U(1), ui.U(4)), frame);
        var board = new Rectangle(ax + ui.U(2), ay + ui.U(6), ui.U(22), ui.U(18));
        if (!l.Day) ui.Disc(board.Center.ToVector2(), ui.U(16), c.Lit * 0.1f);
        ui.Rect(board, l.Night ? new Color(84, 56, 40) : new Color(132, 88, 52)); // painted wood, lit by the window below at night
        ui.Frame(board, frame, ui.U(1));
        ui.Rect(new Rectangle(board.X + ui.U(1), board.Y + ui.U(1), board.Width - ui.U(2), ui.U(1)), l.Night ? new Color(120, 84, 56) : new Color(170, 120, 74));
        var ink = l.Night ? new Color(236, 196, 120) : new Color(240, 214, 150);
        ui.Rect(new Rectangle(board.X + ui.U(6), board.Y + ui.U(6), ui.U(8), ui.U(9)), ink);
        ui.Rect(new Rectangle(board.X + ui.U(14), board.Y + ui.U(7), ui.U(3), ui.U(1)), ink);
        ui.Rect(new Rectangle(board.X + ui.U(16), board.Y + ui.U(7), ui.U(1), ui.U(5)), ink);
        ui.Rect(new Rectangle(board.X + ui.U(14), board.Y + ui.U(11), ui.U(3), ui.U(1)), ink);
        ui.Rect(new Rectangle(board.X + ui.U(5), board.Y + ui.U(3), ui.U(10), ui.U(3)), new Color(255, 248, 228));
        ui.Rect(new Rectangle(board.X + ui.U(8), board.Y + ui.U(8), ui.U(1), ui.U(5)), Kit.Mul(ink, 0.7f));
    }

    /// The timbered tavern: warm plaster and dark beams over two storeys, a tiled roof with a smoking chimney.
    List<(int X, int W)> TimberTavern(Rectangle r, TownLook l, TownColours c, float t)
    {
        Color plaster = HeroWall(l, c, new Color(206, 188, 158)), timber = l.Night ? new Color(30, 21, 28) : new Color(70, 48, 36),
            roof = l.Night ? new Color(88, 60, 84) : Color.Lerp(c.Roof, c.Rim, 0.1f);
        int roofH = ui.U(20), upper = ui.U(26);
        rimLight = Color.Transparent;
        Gable(r.X - ui.U(4), r.Y, r.Width + ui.U(8), 0, roofH, roof);
        for (int k = ui.U(4); k < roofH; k += ui.U(5)) // courses of tiles
        {
            int inset = (int)(k / (float)roofH * (r.Width + ui.U(8)) / 2);
            ui.Rect(new Rectangle(r.X - ui.U(4) + inset, r.Y - k, r.Width + ui.U(8) - 2 * inset, ui.U(1)), Kit.Mul(roof, 0.72f));
        }
        ui.Line(new Vector2(r.Center.X, r.Y - roofH), new Vector2(r.Right + ui.U(4), r.Y), c.Rim, 1);
        ui.Rect(new Rectangle(r.X - ui.U(5), r.Y - ui.U(1), r.Width + ui.U(10), ui.U(1)), c.Rim); // the eave
        ui.Rect(new Rectangle(r.X + ui.U(30), r.Y - ui.U(17), ui.U(6), ui.U(10)), roof); // the chimney
        Smoke(r.X + ui.U(33), r.Y - ui.U(20), t, l.Night);
        ui.Rect(r, plaster);
        ui.Rect(new Rectangle(r.X, r.Y, r.Width, ui.U(3)), Color.Black * 0.24f); // deep eave shadow
        // Stone plinth along the tavern base.
        var plinth = HeroWall(l, c, new Color(134, 128, 120));
        ui.Rect(new Rectangle(r.X, r.Bottom - ui.U(7), r.Width, ui.U(7)), plinth);
        ui.Rect(new Rectangle(r.X, r.Bottom - ui.U(7), r.Width, Math.Max(1, ui.U(1))), Kit.Mul(plinth, 0.72f));
        for (int sx = r.X + ui.U(10); sx < r.Right - ui.U(6); sx += ui.U(14))
            ui.Rect(new Rectangle(sx, r.Bottom - ui.U(6), Math.Max(1, ui.U(1)), ui.U(6)), Kit.Mul(plinth, 0.78f));
        foreach (int by in new[] { r.Y, r.Y + upper, r.Bottom - ui.U(2) }) ui.Rect(new Rectangle(r.X, by, r.Width, ui.U(2)), timber);
        int[] posts = { r.X, r.X + ui.U(52), r.X + ui.U(106), r.Right - ui.U(3) };
        foreach (int px in posts) ui.Rect(new Rectangle(px, r.Y, ui.U(3), r.Height), timber);
        for (int i = 0; i < 3; i++)
        {
            int a = posts[i] + ui.U(3), b = posts[i + 1];
            ui.Line(new Vector2(a, r.Y + upper), new Vector2(a + ui.U(10), r.Y + ui.U(2)), timber, 2);
            ui.Line(new Vector2(b, r.Y + upper), new Vector2(b - ui.U(10), r.Y + ui.U(2)), timber, 2);
        }
        var lights = new List<(int X, int W)>();
        for (int i = 0; i < 3; i++) TavernWindow(new Rectangle(posts[i] + ui.U(19), r.Y + ui.U(8), ui.U(14), ui.U(12)), l, c, timber, true);
        foreach (int wx in new[] { r.X + ui.U(10), r.X + ui.U(112) })
        {
            var w = new Rectangle(wx, r.Y + upper + ui.U(9), ui.U(36), ui.U(18));
            TavernWindow(w, l, c, timber, true);
            if (!l.Day) lights.Add((w.X, w.Width));
        }
        var door = new Rectangle(r.X + ui.U(67), r.Bottom - ui.U(32), ui.U(24), ui.U(30));
        TavernDoor(door, r.Bottom, l, c, timber, lights);
        if (l.Culture == "crimson") // a pitched tiled porch over the door, banners on the posts, barrels by the wall
        {
            int s1 = Math.Max(1, ui.U(1)), py = door.Y - ui.U(3), pw = door.Width + ui.U(20), px0 = door.X - ui.U(10);
            Gable(px0, py, pw, 0, ui.U(8), roof);
            for (int ty = ui.U(3); ty < ui.U(8); ty += ui.U(3))
            {
                int inset = (int)(ty / (float)ui.U(8) * pw / 2);
                ui.Rect(new Rectangle(px0 + inset, py - ty, pw - 2 * inset, s1), Kit.Mul(roof, 0.72f));
            }
            ui.Rect(new Rectangle(px0, py, pw, s1), Color.Lerp(roof, c.Rim, 0.45f));
            ui.Rect(new Rectangle(px0 + ui.U(1), py + s1, ui.U(2), r.Bottom - py - s1), timber);
            ui.Rect(new Rectangle(px0 + pw - ui.U(3), py + s1, ui.U(2), r.Bottom - py - s1), timber);
            ui.Line(new Vector2(px0 + ui.U(2), py + ui.U(6)), new Vector2(px0 + ui.U(7), py + s1), timber, 1);
            ui.Line(new Vector2(px0 + pw - ui.U(2), py + ui.U(6)), new Vector2(px0 + pw - ui.U(7), py + s1), timber, 1);
            // Crimson & gold pennants hung cleanly on the upper timber posts (never blocking windows).
            var red = l.Night ? new Color(118, 36, 36) : new Color(184, 52, 44);
            foreach (int bx in new[] { posts[1] - ui.U(3), posts[2] - ui.U(2) })
            {
                ui.Rect(new Rectangle(bx - s1, r.Y + ui.U(5), ui.U(9), s1), timber);
                ui.Rect(new Rectangle(bx, r.Y + ui.U(6), ui.U(7), ui.U(15)), red);
                ui.Rect(new Rectangle(bx + ui.U(2), r.Y + ui.U(21), ui.U(3), ui.U(2)), red);
                ui.Rect(new Rectangle(bx + ui.U(3), r.Y + ui.U(9), s1, ui.U(8)), Color.Lerp(red, Kit.Gilt, 0.6f));
            }
            // Ale barrels sitting outside the right wall.
            var wood = l.Night ? new Color(68, 46, 34) : new Color(128, 86, 52);
            foreach (int bx in new[] { r.X + ui.U(2), r.Right - ui.U(10) })
            {
                ui.Rect(new Rectangle(bx, r.Bottom - ui.U(9), ui.U(7), ui.U(9)), wood);
                ui.Rect(new Rectangle(bx, r.Bottom - ui.U(7), ui.U(7), s1), Kit.Mul(wood, 0.55f));
                ui.Rect(new Rectangle(bx, r.Bottom - ui.U(3), ui.U(7), s1), Kit.Mul(wood, 0.55f));
            }
        }
        TavernSign(r.Right, r.Y + ui.U(4), l, c, timber);
        return lights;
    }
}
