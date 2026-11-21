using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the northern people's pieces of the street (log and stave building): the mead hall (their tavern), the hall-fort
/// on its hill (their keep), the log barracks with its watchtower, the stave church over the roofs (their landmark); and the
/// parts they share: long shingled roofs, dragon-headed gables, round shields. Drawing only.
partial class MapScreen
{
    /// The colours of northern timber: weathered logs, the dark of carved posts and barge boards, shingles of split wood.
    static Color NorthLogs(TownLook l, TownColours c) => HeroWall(l, c, new Color(132, 92, 60));
    static Color NorthDark(TownLook l) => l.Night ? new Color(30, 21, 24) : new Color(62, 40, 28);
    static Color NorthShingle(TownLook l, TownColours c) => l.Night ? new Color(56, 46, 52) : l.Dusk ? new Color(98, 70, 62) : new Color(104, 80, 58);

    /// A long roof seen from its side: from the eave (xl..xr) up `h` to the ridge (rl..rr), laid in courses of shingles, snow on its
    /// upper part in winter; its sloping ends are the barge boards and its top the ridge beam, in `board`.
    void LongRoof(int xl, int xr, int rl, int rr, int eave, int h, Color roof, Color board)
    {
        int s = Math.Max(1, ui.U(1)), course = ui.U(3);
        for (int k = 0; k < h; k += s)
        {
            float f = k / (float)h;
            int a = (int)MathHelper.Lerp(xl, rl, f), b = (int)MathHelper.Lerp(xr, rr, f), y = eave - k - s;
            bool snow = snowCap.A > 0 && f >= 0.3f;
            ui.Rect(new Rectangle(a, y, b - a, s), snow ? snowCap : roof);
            if (!snow && k % course == 0 && k > 0)
            {
                ui.Rect(new Rectangle(a, y, b - a, s), Kit.Mul(roof, 0.72f)); // the butts of a course
                int off = k / course % 2 * ui.U(2);
                for (int x = a + off + ui.U(2); x < b - ui.U(2); x += ui.U(4)) ui.Rect(new Rectangle(x, y - course + s, s, course - s), Kit.Mul(roof, 0.86f));
            }
            ui.Rect(new Rectangle(a, y, 2 * s, s), board);
            ui.Rect(new Rectangle(b - 2 * s, y, 2 * s, s), rimLight.A > 0 ? Color.Lerp(board, rimLight, 0.4f) : board);
        }
        ui.Rect(new Rectangle(rl - s, eave - h - 2 * s, rr - rl + 2 * s, 2 * s), snowCap.A > 0 ? Color.Lerp(snowCap, board, 0.2f) : board); // the ridge beam
        ui.Rect(new Rectangle(xl, eave - s, xr - xl, s), Kit.Mul(roof, 0.6f));
    }

    /// A carved dragon's head and neck rising from a gable's end, facing out (`dir` 1 to the right, -1 to the left); `x, y` is
    /// where its neck meets the ridge.
    static readonly string[] DragonMap =
    {
        "..###..",
        ".##e###",
        ".##..#.",
        "###....",
        "##.....",
        "##.....",
        ".##....",
    };

    void Dragon(int x, int y, int dir, float cell, Color wood, Color eye)
    {
        int cs = Math.Max(1, ui.U(cell)), rows = DragonMap.Length;
        for (int r = 0; r < rows; r++)
            for (int i = 0; i < DragonMap[r].Length; i++)
            {
                char ch = DragonMap[r][i];
                if (ch == '.') continue;
                int ox = dir > 0 ? (i - 1) * cs : -(i - 1) * cs - cs;
                ui.Rect(new Rectangle(x + ox, y - (rows - r) * cs, cs, cs), ch == 'e' ? eye : wood);
            }
    }

    /// A round shield hung on a wall: its rim, a painted face (plain, halved or with a band), the iron boss.
    void RoundShield(int cx, int cy, int rad, Color paint, Color second, int design, bool night)
    {
        var at = new Vector2(cx, cy);
        int s = Math.Max(1, ui.U(1));
        var rim = night ? new Color(40, 34, 36) : new Color(70, 52, 38);
        ui.Disc(at, rad, rim);
        ui.Disc(at, rad - s, paint);
        if (design == 1) ui.Rect(new Rectangle(cx - rad + 2 * s, cy - s, 2 * rad - 4 * s, 2 * s), second); // a band across
        else if (design == 2) ui.Rect(new Rectangle(cx - s, cy - rad + 2 * s, 2 * s, 2 * rad - 4 * s), second); // a band down
        ui.Disc(at, Math.Max(s, rad / 3), night ? new Color(90, 92, 104) : new Color(150, 152, 160));
        ui.Rect(new Rectangle(cx - s, cy - rad / 3, s, s), night ? new Color(140, 140, 156) : new Color(220, 222, 228)); // the boss catches the light
    }

    /// The shield paints of a northern hall: the holder's cloth, then red, ochre, white and blue-black, darkened by the hour.
    static Color ShieldPaint(int i, Color cloth, TownLook l)
    {
        Color[] paints = { cloth, new(160, 48, 40), new(198, 150, 64), new(222, 214, 196), new(46, 58, 84) };
        var p = paints[i % paints.Length];
        return l.Night ? Kit.Mul(p, 0.5f) : l.Dusk ? Kit.Mul(p, 0.78f) : p;
    }

    /// A post with a fire basket on it, the fire and its glow (only after dusk is it lit).
    void FirePost(int x, int ground, int h, TownLook l, TownColours c, Color dark, float t)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(x, ground - h, ui.U(2), h), dark);
        ui.Rect(new Rectangle(x - ui.U(2), ground - h - ui.U(3), ui.U(6), ui.U(3)), dark);
        if (l.Day) return;
        var at = new Vector2(x + s, ground - h - ui.U(5));
        ui.Disc(at, ui.U(11), c.Lit * 0.12f); ui.Disc(at, ui.U(5), c.Lit * 0.25f);
        int flick = (int)(MathF.Sin(t * 9 + x) * 1.5f + 1.5f);
        ui.Rect(new Rectangle(x - s, ground - h - ui.U(5 + flick), ui.U(4), ui.U(3 + flick)), new Color(255, 170, 70));
        ui.Rect(new Rectangle(x, ground - h - ui.U(4 + flick), ui.U(2), ui.U(2 + flick)), new Color(255, 232, 150));
    }

    // ---------- the mead hall ----------

    /// The northern tavern, a mead hall: stave walls on a stone footing, a long shingled roof with a dragon at each end of the
    /// ridge and a smoke louver on it, a carved portal, round shields under the eaves, fire posts by the door and a horn on the sign.
    List<(int X, int W)> MeadHall(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color logs = NorthLogs(l, c), dark = NorthDark(l), shingle = NorthShingle(l, c), carve = l.Night ? new Color(96, 70, 44) : new Color(176, 128, 70);
        var lights = new List<(int X, int W)>();
        int s = Math.Max(1, ui.U(1)), wallH = ui.U(28), top = r.Bottom - wallH, roofH = ui.U(28);
        // The roof, over the long side: it overhangs the walls; the ridge is shorter than the eave, the ends hipped back.
        rimLight = c.Rim;
        int xl = r.X - ui.U(10), xr = r.Right + ui.U(10), rl = r.X + ui.U(14), rr = r.Right - ui.U(14);
        LongRoof(xl, xr, rl, rr, top + ui.U(2), roofH, shingle, dark);
        Dragon(rl - ui.U(1), top + ui.U(2) - roofH - ui.U(1), -1, 1.5f, dark, carve);
        Dragon(rr + ui.U(1), top + ui.U(2) - roofH - ui.U(1), 1, 1.5f, dark, carve);
        // The smoke louver on the ridge, a little roof of its own.
        int lx = r.X + ui.U(102);
        ui.Rect(new Rectangle(lx, top - roofH - ui.U(4), ui.U(12), ui.U(4)), dark);
        Gable(lx - ui.U(2), top - roofH - ui.U(4), ui.U(16), 0, ui.U(4), shingle);
        Smoke(lx + ui.U(6), top - roofH - ui.U(8), t, l.Night);
        // Stave walls: upright planks, each lit on its left edge; a stone footing below; a deep shadow under the eave.
        rimLight = Color.Transparent;
        ui.Rect(new Rectangle(r.X, top, r.Width, wallH), logs);
        for (int x = r.X + ui.U(5); x < r.Right; x += ui.U(5))
        {
            ui.Rect(new Rectangle(x, top, s, wallH), Kit.Mul(logs, 0.66f));
            ui.Rect(new Rectangle(x + s, top, s, wallH), Color.Lerp(logs, c.Rim, 0.12f));
        }
        ui.Rect(new Rectangle(r.X, top, r.Width, ui.U(3)), Color.Black * 0.3f);
        var foot = HeroWall(l, c, new Color(126, 122, 116));
        ui.Rect(new Rectangle(r.X - ui.U(2), r.Bottom - ui.U(5), r.Width + ui.U(4), ui.U(5)), foot);
        for (int x = r.X + ui.U(4); x < r.Right; x += ui.U(11)) ui.Rect(new Rectangle(x, r.Bottom - ui.U(5), s, ui.U(5)), Kit.Mul(foot, 0.75f));
        ui.Rect(new Rectangle(r.X - ui.U(2), r.Bottom - ui.U(5), r.Width + ui.U(4), s), Color.Lerp(foot, c.Rim, 0.3f));
        // Corner posts with carved knobs.
        foreach (int px in new[] { r.X - ui.U(1), r.Right - ui.U(3) })
        {
            ui.Rect(new Rectangle(px, top, ui.U(4), wallH - ui.U(5)), dark);
            ui.Rect(new Rectangle(px - s, top + ui.U(2), ui.U(4) + 2 * s, ui.U(2)), carve);
        }
        ui.Rect(new Rectangle(r.Right - ui.U(3) + ui.U(3), top, s, wallH - ui.U(5)), Color.Lerp(dark, c.Rim, 0.4f));
        // Two small shuttered windows, firelit within.
        foreach (int wx in new[] { r.X + ui.U(16), r.X + ui.U(124) })
        {
            var w = new Rectangle(wx, top + ui.U(9), ui.U(14), ui.U(9));
            TavernWindow(w, l, c, dark, true);
            ui.Rect(new Rectangle(w.X - ui.U(5), w.Y - s, ui.U(4), w.Height + 2 * s), dark); // the shutter
            if (!l.Day) lights.Add((w.X, w.Width));
        }
        // Shields along the wall under the eave, between the windows and the door.
        int sh = 0;
        foreach (int sx in new[] { r.X + ui.U(39), r.X + ui.U(52), r.X + ui.U(109), r.X + ui.U(151) })
        {
            RoundShield(sx, top + ui.U(8), ui.U(6), ShieldPaint(sh, cloth, l), ShieldPaint(sh + 1, cloth, l), (sh + 1) % 3, l.Night);
            sh++;
        }
        // The portal: a round-headed door between carved posts, a carved lintel over it.
        var door = new Rectangle(r.X + ui.U(70), r.Bottom - ui.U(24), ui.U(20), ui.U(24));
        TavernDoor(door, r.Bottom, l, c, dark, lights, lamp: false);
        ui.Rect(new Rectangle(door.X, door.Y - ui.U(3), door.Width, ui.U(3)), dark); // the round head, stepped
        ui.Rect(new Rectangle(door.X + ui.U(3), door.Y - ui.U(5), door.Width - ui.U(6), ui.U(2)), dark);
        foreach (int px in new[] { door.X - ui.U(5), door.Right + ui.U(2) })
        {
            ui.Rect(new Rectangle(px, door.Y - ui.U(4), ui.U(3), door.Height + ui.U(4) - ui.U(5)), carve);
            for (int y = door.Y - ui.U(2); y < door.Bottom - ui.U(6); y += ui.U(3)) // the knotwork, a zigzag down each post
                ui.Rect(new Rectangle(px + (y / ui.U(3) % 2 == 0 ? 0 : ui.U(2)), y, s, ui.U(2)), dark);
        }
        ui.Rect(new Rectangle(door.X - ui.U(6), door.Y - ui.U(8), door.Width + ui.U(12), ui.U(3)), carve);
        ui.Rect(new Rectangle(door.X - ui.U(6), door.Y - ui.U(6), door.Width + ui.U(12), s), dark);
        FirePost(door.X - ui.U(11), r.Bottom, ui.U(18), l, c, dark, t);
        FirePost(door.Right + ui.U(9), r.Bottom, ui.U(18), l, c, dark, t);
        // Mead casks by the wall.
        var wood = l.Night ? new Color(68, 46, 34) : new Color(128, 86, 52);
        foreach (int bx in new[] { r.X + ui.U(34), r.X + ui.U(42) })
        {
            ui.Rect(new Rectangle(bx, r.Bottom - ui.U(9), ui.U(7), ui.U(9)), wood);
            ui.Rect(new Rectangle(bx, r.Bottom - ui.U(7), ui.U(7), s), Kit.Mul(wood, 0.55f));
            ui.Rect(new Rectangle(bx, r.Bottom - ui.U(3), ui.U(7), s), Kit.Mul(wood, 0.55f));
        }
        HornSign(r.Right + ui.U(8), top - ui.U(2), l, c, dark);
        rimLight = c.Rim;
        return lights;
    }

    /// The mead hall's sign: a board on an iron bracket with a drinking horn painted on it.
    void HornSign(int ax, int ay, TownLook l, TownColours c, Color frame)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(ax - ui.U(10), ay, ui.U(34), ui.U(2)), frame);
        ui.Line(new Vector2(ax - ui.U(8), ay + ui.U(10)), new Vector2(ax + ui.U(4), ay + ui.U(2)), frame, 2);
        ui.Rect(new Rectangle(ax + ui.U(5), ay + ui.U(2), s, ui.U(4)), frame);
        ui.Rect(new Rectangle(ax + ui.U(18), ay + ui.U(2), s, ui.U(4)), frame);
        var board = new Rectangle(ax + ui.U(1), ay + ui.U(6), ui.U(22), ui.U(16));
        if (!l.Day) ui.Disc(board.Center.ToVector2(), ui.U(15), c.Lit * 0.1f);
        ui.Rect(new Rectangle(board.X - s, board.Y - s, board.Width + 2 * s, board.Height + 2 * s), frame);
        ui.Rect(board, l.Night ? new Color(84, 56, 40) : new Color(132, 88, 52));
        if (snowCap.A > 0) ui.Rect(new Rectangle(board.X - s, board.Y - 2 * s, board.Width + 2 * s, 2 * s), snowCap);
        // The horn: a curve rising from its tip at the lower left and widening to its mouth at the upper right, a gilt band at the rim.
        var horn = l.Night ? new Color(200, 170, 120) : new Color(236, 214, 170);
        for (int i = 0; i < 14; i++)
        {
            float yc = i * i / 22f, th = 1 + i / 3.5f;
            int hx = board.X + ui.U(4 + i), hy = board.Bottom - ui.U(4 + yc + th / 2);
            ui.Rect(new Rectangle(hx, hy, ui.U(1), ui.U(th)), i >= 12 ? (l.Night ? new Color(200, 160, 80) : Kit.Gilt) : horn);
            if (i < 12) ui.Rect(new Rectangle(hx, hy + ui.U(th) - s, ui.U(1), s), Kit.Mul(horn, 0.72f)); // its shaded underside
        }
    }

    // ---------- the hall-fort ----------

    /// The northern keep: a hall-fort on its hill. A ring of pointed stakes with a gate and shields on it, the lord's long hall
    /// behind with dragons on its ridge and smoke from its louver, a tall timber watchtower flying the colours.
    /// `hero` paints the near one of a castle (on its mound, plank and log detail, fire posts at the gate); else a hilltop fort.
    void HallFort(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        int k = ui.U(scale), s = Math.Max(1, ui.U(1));
        int wave = (int)MathF.Round(MathF.Sin(t * 3.2f) * s);
        Color far(Color day) => hero ? HeroWall(l, c, day) : Color.Lerp(Color.Lerp(c.Far, c.Mid, 0.52f), Kit.Mul(day, 0.7f), l.Day ? 0.25f : 0.1f);
        Color logs = far(new Color(132, 92, 60)), stakes = far(new Color(118, 84, 56)), dark = hero ? NorthDark(l) : Kit.Mul(logs, 0.62f);
        Color shingle = hero ? NorthShingle(l, c) : Color.Lerp(logs, c.Far, 0.3f);
        void Box(int x, int y, int w, int h, Color col) => ui.Rect(new Rectangle(cx + x * k, baseY - y * k, w * k, h * k), col);

        // The ground it stands on: an earthen mound with a log stair (the castle), or the crag of its hill.
        if (hero)
        {
            int mh = ui.U(14);
            var earth = l.Snow ? c.Snow : l.Night ? Color.Lerp(c.Near, new Color(46, 48, 42), 0.45f) : new Color(92, 98, 66);
            for (int m = 0; m < mh; m += s)
            {
                float f = m / (float)mh;
                int hw = 17 * k + (int)(f * ui.U(40));
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s), m < ui.U(3) ? earth : Color.Lerp(Kit.Mul(earth, 0.7f), c.Ground, f * 0.8f));
            }
            for (int st = 0; st < 5; st++)
            {
                int sy = baseY + st * ui.U(3), sw = ui.U(16) + st * ui.U(3);
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, ui.U(3)), Kit.Mul(logs, 0.9f - st * 0.04f));
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, s), Color.Lerp(logs, c.Rim, 0.3f));
            }
        }
        else
            for (int m = 0; m < 10 * k; m += s)
            {
                float f = m / (float)(10 * k);
                int hw = 16 * k + (int)(f * 10 * k);
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s), Color.Lerp(Kit.Mul(c.Far, 0.85f), c.Far, MathF.Min(1, f * 1.2f)));
            }

        // The watchtower at the right, behind the stakes: legs, a fighting platform that juts out, a shingled cap, the flag.
        Box(8, 25, 1, 25, dark); Box(12, 25, 1, 25, dark);
        ui.Line(new Vector2(cx + 9 * k, baseY - 22 * k), new Vector2(cx + 12 * k, baseY - 12 * k), dark, Math.Max(1, k / 2));
        ui.Line(new Vector2(cx + 12 * k, baseY - 22 * k), new Vector2(cx + 9 * k, baseY - 12 * k), dark, Math.Max(1, k / 2));
        Box(7, 27, 7, 3, logs); // the platform's breastwork
        for (int i = 7; i < 14; i += 2) Box(i, 27, 1, 3, Kit.Mul(logs, 0.8f));
        Box(7, 25, 7, 1, dark);
        if (!l.Day) Box(9, 26, 1, 1, c.Lit * 0.9f);
        var oldSnow = snowCap;
        Gable(cx + 6 * k, baseY - 27 * k, 9 * k, 0, 4 * k, shingle);
        int pole = cx + 10 * k + k / 2, poleTop = baseY - 36 * k;
        ui.Rect(new Rectangle(pole, poleTop, s, 5 * k), dark);
        ui.Rect(new Rectangle(pole + s, poleTop, 5 * k, 2 * k + wave), cloth);
        ui.Rect(new Rectangle(pole + s + 5 * k, poleTop + s, k, k + wave), cloth);

        // The long hall behind the stakes: log walls above them, the long roof, dragons at the ridge's ends, the louver's smoke.
        Box(-12, 13, 18, 5, logs);
        for (int y = 9; y < 13; y++) ui.Rect(new Rectangle(cx - 12 * k, baseY - y * k, 18 * k, s), Kit.Mul(logs, 0.72f));
        if (!l.Day) foreach (int wx in new[] { -9, -4, 2 }) Box(wx, 11, 1, 1, c.Lit * 0.9f);
        else if (hero) foreach (int wx in new[] { -9, -4, 2 }) Box(wx, 11, 1, 1, new Color(40, 30, 28));
        var oldRim = rimLight;
        rimLight = hero ? c.Rim : Color.Transparent;
        LongRoof(cx - 14 * k, cx + 8 * k, cx - 9 * k, cx + 3 * k, baseY - 12 * k, 8 * k, shingle, dark);
        rimLight = oldRim;
        Dragon(cx - 9 * k, baseY - 20 * k - s, -1, scale * 0.5f, dark, hero ? new Color(200, 150, 80) : dark);
        Dragon(cx + 3 * k, baseY - 20 * k - s, 1, scale * 0.5f, dark, hero ? new Color(200, 150, 80) : dark);
        if (hero) Smoke(cx - 3 * k, baseY - 21 * k, t, l.Night); // the far one's would cross its sign
        snowCap = oldSnow;

        // The ring of stakes: pointed tops of uneven height, a walk behind them, the gate in the middle.
        for (int x = -15; x < 15; x++)
        {
            if (x is >= -2 and < 2) continue;
            int h = 8 + (x * 7 + 3) % 2;
            Box(x, h, 1, h, x % 2 == 0 ? stakes : Kit.Mul(stakes, 0.88f));
            ui.Rect(new Rectangle(cx + x * k + k / 4, baseY - h * k - k / 2, k / 2, k / 2), x % 2 == 0 ? stakes : Kit.Mul(stakes, 0.88f)); // the point
            if (snowCap.A > 0) ui.Rect(new Rectangle(cx + x * k, baseY - h * k, k, s), snowCap);
        }
        Box(-15, 6, 30, 1, Kit.Mul(stakes, 0.62f)); // the binding rail
        if (hero)
        {
            for (int x = -15; x < 15; x++) if (x is < -2 or >= 2) ui.Rect(new Rectangle(cx + x * k, baseY - 8 * k, s, 8 * k), Kit.Mul(stakes, 0.7f));
            int i = 0;
            foreach (int sx in new[] { -13, -9, 8, 12 }) { RoundShield(cx + sx * k, baseY - 6 * k - k / 2, k + k / 2, ShieldPaint(i, cloth, l), ShieldPaint(i + 3, cloth, l), i % 3, l.Night); i++; }
        }
        // The gate: two tall posts and a lintel with crossed horns, the doors dark (firelight in the yard after dusk).
        Box(-3, 11, 1, 11, dark); Box(2, 11, 1, 11, dark);
        Box(-3, 11, 6, 1, dark);
        ui.Line(new Vector2(cx - 2 * k, baseY - 11 * k), new Vector2(cx + k, baseY - 14 * k), dark, Math.Max(1, k / 2));
        ui.Line(new Vector2(cx + 2 * k, baseY - 11 * k), new Vector2(cx - k, baseY - 14 * k), dark, Math.Max(1, k / 2));
        Box(-2, 9, 4, 9, l.Day ? new Color(36, 28, 26) : Color.Lerp(new Color(60, 36, 24), c.Lit, hero ? 0.18f : 0.12f)); // firelight in the yard, faint
        if (hero)
        {
            Box(-2, 9, 1, 9, Kit.Mul(logs, 0.7f)); Box(1, 9, 1, 9, Kit.Mul(logs, 0.7f)); // the doors swung in
            foreach (int dx in new[] { -2, 1 }) ui.Rect(new Rectangle(cx + dx * k + k / 2, baseY - 9 * k, s, 9 * k), Kit.Mul(logs, 0.5f)); // their planks
            for (int y = 2; y <= 7; y += 5) // their iron bands
            { ui.Rect(new Rectangle(cx - 2 * k, baseY - y * k - s, k, s), dark); ui.Rect(new Rectangle(cx + k, baseY - y * k - s, k, s), dark); }
            ui.Rect(new Rectangle(cx - k, baseY - s * 2, 2 * k, 2 * s), Kit.Mul(logs, 0.5f)); // the worn threshold
            FirePost(cx - 5 * k, baseY, 5 * k, l, c, dark, t);
            FirePost(cx + 4 * k, baseY, 5 * k, l, c, dark, t);
        }
    }

    // ---------- the barracks ----------

    /// The northern barracks: a log watchtower (a log base, open legs braced with a cross, a breastwork and a shingled cap with the
    /// flag), the warband's longhouse with dragons on its ridge and shields on its wall, and the yard. Returns nothing: drawing only.
    void NorthBarracks(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color logs = NorthLogs(l, c), dark = NorthDark(l), shingle = NorthShingle(l, c), carve = l.Night ? new Color(96, 70, 44) : new Color(176, 128, 70);
        int ground = r.Bottom, s = Math.Max(1, ui.U(1));
        // The longhouse.
        var hall = new Rectangle(r.X + ui.U(30), ground - ui.U(22), ui.U(48), ui.U(22)); // clear of the tower's platform
        LongRoof(hall.X - ui.U(6), hall.Right + ui.U(6), hall.X + ui.U(8), hall.Right - ui.U(8), hall.Y + ui.U(2), ui.U(18), shingle, dark);
        Dragon(hall.X + ui.U(8), hall.Y + ui.U(2) - ui.U(18) - s, -1, 1, dark, carve);
        Dragon(hall.Right - ui.U(8), hall.Y + ui.U(2) - ui.U(18) - s, 1, 1, dark, carve);
        ui.Rect(hall, logs);
        for (int y = hall.Y + ui.U(3); y < ground; y += ui.U(3)) ui.Rect(new Rectangle(hall.X, y, hall.Width, s), Kit.Mul(logs, 0.68f));
        ui.Rect(new Rectangle(hall.X, hall.Y, hall.Width, ui.U(2)), Color.Black * 0.3f);
        Edges(hall.X, hall.Y, hall.Width, hall.Height, logs);
        var gate = new Rectangle(hall.X + ui.U(20), ground - ui.U(16), ui.U(12), ui.U(16));
        ui.Rect(new Rectangle(gate.X - ui.U(2), gate.Y - ui.U(2), gate.Width + ui.U(4), gate.Height + ui.U(2)), dark);
        ui.Rect(gate, l.Day ? new Color(36, 28, 26) : Color.Lerp(new Color(60, 36, 24), c.Lit, 0.4f));
        ui.Rect(new Rectangle(gate.Center.X, gate.Y, s, gate.Height), dark);
        int i = 0;
        foreach (int sx in new[] { hall.X + ui.U(12), hall.X + ui.U(40) }) // clear of the guard at the door
        { RoundShield(sx, hall.Y + ui.U(9), ui.U(6), ShieldPaint(i, cloth, l), ShieldPaint(i + 3, cloth, l), i + 1, l.Night); i++; }
        if (!l.Day) FirePost(gate.Right + ui.U(4), ground, ui.U(14), l, c, dark, t);
        // The watchtower.
        int tx = r.X, tw = ui.U(22);
        ui.Rect(new Rectangle(tx + ui.U(2), ground - ui.U(26), tw - ui.U(4), ui.U(26)), logs); // the log base
        for (int y = ground - ui.U(23); y < ground; y += ui.U(3)) ui.Rect(new Rectangle(tx + ui.U(2), y, tw - ui.U(4), s), Kit.Mul(logs, 0.68f));
        for (int y = ground - ui.U(26); y < ground; y += ui.U(6)) // the log ends at the corners
        {
            ui.Rect(new Rectangle(tx, y, ui.U(3), ui.U(3)), Kit.Mul(logs, 0.85f));
            ui.Rect(new Rectangle(tx + tw - ui.U(3), y, ui.U(3), ui.U(3)), Kit.Mul(logs, 0.85f));
        }
        if (!l.Day) ui.Rect(new Rectangle(tx + ui.U(9), ground - ui.U(18), ui.U(3), ui.U(5)), c.Lit * 0.85f);
        int legTop = ground - ui.U(46);
        foreach (int lx in new[] { tx + ui.U(3), tx + tw - ui.U(5) }) ui.Rect(new Rectangle(lx, legTop, ui.U(2), ui.U(20)), dark);
        ui.Line(new Vector2(tx + ui.U(4), legTop + ui.U(2)), new Vector2(tx + tw - ui.U(4), ground - ui.U(27)), dark, 1);
        ui.Line(new Vector2(tx + tw - ui.U(4), legTop + ui.U(2)), new Vector2(tx + ui.U(4), ground - ui.U(27)), dark, 1);
        var plat = new Rectangle(tx - ui.U(3), legTop - ui.U(9), tw + ui.U(6), ui.U(9));
        ui.Rect(plat, logs);
        for (int x = plat.X + ui.U(3); x < plat.Right; x += ui.U(3)) ui.Rect(new Rectangle(x, plat.Y, s, plat.Height), Kit.Mul(logs, 0.72f));
        ui.Rect(new Rectangle(plat.X, plat.Bottom - s, plat.Width, ui.U(2)), dark);
        Edges(plat.X, plat.Y, plat.Width, plat.Height, logs);
        if (!l.Day) ui.Rect(new Rectangle(plat.X + ui.U(8), plat.Y + ui.U(3), ui.U(3), ui.U(3)), c.Lit * 0.85f);
        Gable(plat.X - ui.U(2), plat.Y, plat.Width + ui.U(4), 0, ui.U(12), shingle);
        int pole = tx + tw / 2, poleTop = plat.Y - ui.U(26);
        ui.Rect(new Rectangle(pole, poleTop, s, ui.U(16)), dark);
        int wave = (int)MathF.Round(MathF.Sin(t * 3) * ui.U(1));
        ui.Rect(new Rectangle(pole + s, poleTop, ui.U(14), ui.U(7) + wave), cloth);
        ui.Rect(new Rectangle(pole + s + ui.U(10), poleTop + ui.U(7) + wave, ui.U(4), ui.U(2)), cloth); // a tail
        Yard(hall.Right + ui.U(2), r.Right, ground, l, cloth);
    }

    // ---------- the stave church ----------

    /// The northern landmark over the roofs: a stave church, roof on roof, dragons on the lower ridges, a spire; drawn flat in
    /// the far colour, its windows lit at night. `x` is its middle.
    void StaveChurch(int x, int ground, Color c, bool night, Color lit, float t)
    {
        var dark = Kit.Mul(c, 0.8f);
        var rim0 = rimLight;
        if (rimLight.A > 0) rimLight = Color.Lerp(c, rimLight, 0.3f); // far off: the light only grazes it
        ui.Rect(new Rectangle(x - ui.U(22), ground - ui.U(34), ui.U(44), ui.U(34)), c);
        LongRoofFlat(x - ui.U(28), x + ui.U(28), x - ui.U(16), x + ui.U(16), ground - ui.U(32), ui.U(12), c);
        Dragon(x - ui.U(16), ground - ui.U(44), -1, 1, c, c);
        Dragon(x + ui.U(16), ground - ui.U(44), 1, 1, c, c);
        // The second storey and its roof.
        ui.Rect(new Rectangle(x - ui.U(14), ground - ui.U(58), ui.U(28), ui.U(14)), c);
        LongRoofFlat(x - ui.U(20), x + ui.U(20), x - ui.U(9), x + ui.U(9), ground - ui.U(56), ui.U(10), c);
        Dragon(x - ui.U(9), ground - ui.U(66), -1, 1, c, c);
        Dragon(x + ui.U(9), ground - ui.U(66), 1, 1, c, c);
        // The belfry and its spire.
        ui.Rect(new Rectangle(x - ui.U(7), ground - ui.U(78), ui.U(14), ui.U(12)), c);
        ui.Rect(new Rectangle(x - ui.U(5), ground - ui.U(75), ui.U(2), ui.U(5)), night ? lit * 0.6f : dark);
        ui.Rect(new Rectangle(x + ui.U(3), ground - ui.U(75), ui.U(2), ui.U(5)), night ? lit * 0.6f : dark);
        Gable(x - ui.U(9), ground - ui.U(78), ui.U(18), 0, ui.U(30), c);
        ui.Rect(new Rectangle(x, ground - ui.U(114), Math.Max(1, ui.U(1)), ui.U(6)), c);
        ui.Rect(new Rectangle(x - ui.U(2), ground - ui.U(112), ui.U(5), Math.Max(1, ui.U(1))), c);
        if (night)
            foreach (int wx in new[] { -14, -4, 8 }) ui.Rect(new Rectangle(x + ui.U(wx), ground - ui.U(26), ui.U(3), ui.U(6)), lit * 0.8f);        rimLight = rim0;
    }

    /// A long roof in one flat colour (for far pieces): the same shape as `LongRoof`, its snow on top.
    void LongRoofFlat(int xl, int xr, int rl, int rr, int eave, int h, Color col)
    {
        int s = Math.Max(1, ui.U(1));
        for (int k = 0; k < h; k += s)
        {
            float f = k / (float)h;
            int a = (int)MathHelper.Lerp(xl, rl, f), b = (int)MathHelper.Lerp(xr, rr, f);
            ui.Rect(new Rectangle(a, eave - k - s, b - a, s), snowCap.A > 0 && f > 0.35f ? Color.Lerp(snowCap, col, 0.3f) : col);
            if (rimLight.A > 0) ui.Rect(new Rectangle(b - s, eave - k - s, s, s), Color.Lerp(col, rimLight, 0.5f));
        }
    }
}
