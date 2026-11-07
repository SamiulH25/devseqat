using Microsoft.Xna.Framework;

namespace Ironfield;

/// UI lab pages, part two: a battle HUD over a field, the captain's sheet, the realms and an event dialog.
sealed partial class UiLab
{
    int dockSel = 2;

    // ---------- 6. battle HUD ----------

    void HudPage(Rectangle a)
    {
        Field();
        // Top centre: the two armies' morale pulling on one bar.
        int tw = Math.Min(U(380), a.Width / 2), tx = ui.W / 2 - tw / 2, ty = a.Y + U(6);
        ui.Rect(new Rectangle(tx - U(60), ty - U(4), tw + U(120), U(40)), Color.Black * 0.45f);
        ui.Shield(new Rectangle(tx - U(52), ty - U(2), U(30), U(36)), Side(2));
        ui.Shield(new Rectangle(tx + tw + U(22), ty - U(2), U(30), U(36)), Side(1));
        ui.Text("412", tx, ty, Kit.Gilt, 2);
        ui.TextR("388", tx + tw, ty, Style.Foe, 2);
        ui.TextC("THE BALANCE", tx + tw / 2f, ty + U(3), Style.Dim, 1);
        ui.TugBar(new Rectangle(tx, ty + U(20), tw, U(8)), 0.56f, Kit.Gilt, Style.Foe);

        // Top left: time and weather.
        var tl = ui.Plate(new Rectangle(a.X, a.Y + U(4), U(150), U(56)), solid: false);
        ui.Icon("clock", tl.X - U(4), tl.Y - U(6), Style.Text, 1); ui.Text("11:40", tl.X + U(8), tl.Y - U(6), Style.Text, 1);
        ui.Icon("rain", tl.X + U(56), tl.Y - U(6), Style.Friendly, 1); ui.Text("LIGHT RAIN", tl.X + U(68), tl.Y - U(6), Style.Dim, 1);
        ui.Icon("arrow", tl.X - U(4), tl.Y + U(8), Style.Warn, 1); ui.Text("BOWS -15%", tl.X + U(8), tl.Y + U(8), Style.Warn, 1);

        // Top right: the minimap.
        var mm = new Rectangle(a.Right - U(160), a.Y + U(4), U(160), U(116));
        var mc = ui.Plate(mm, solid: true);
        var land = new Rectangle(mm.X + U(6), mm.Y + U(6), mm.Width - U(12), mm.Height - U(12));
        ui.VGrad(land, new Color(70, 88, 50), new Color(48, 64, 38), 4);
        ui.Line(new Vector2(land.X, land.Y + land.Height * 0.7f), new Vector2(land.Right, land.Y + land.Height * 0.45f), new Color(70, 110, 150), 3);
        var rng = new Random(3);
        for (int i = 0; i < 26; i++)
        {
            bool foe = i % 2 == 1;
            var p = new Point(land.X + (int)(land.Width * (0.2f + 0.6f * (float)rng.NextDouble())), land.Y + (int)(land.Height * (foe ? 0.15f + 0.3f * (float)rng.NextDouble() : 0.55f + 0.3f * (float)rng.NextDouble())));
            ui.Rect(new Rectangle(p.X, p.Y, U(3), U(3)), foe ? Style.Foe : Kit.Gilt);
        }
        ui.Frame(new Rectangle(land.X + land.Width / 3, land.Y + land.Height / 3, land.Width / 3, land.Height / 3), Color.White * 0.7f, U(1));
        _ = mc;

        // Left: the event feed.
        int fy = a.Y + U(84);
        foreach (var (text, col, icon) in new[] { ("ENEMY HORSE ON THE LEFT!", Style.Orange, "horse"), ("THE PIKES HOLD THE FORD", Style.Ok, "shield"), ("CROSSBOWS WAVERING", Style.Warn, "arrow") })
        {
            ui.Toast(a.X, fy, text, col, icon);
            fy += U(22);
        }

        // Bottom: the order bar and the command dock.
        int dockH = U(104), dw = Math.Min(a.Width, U(900)), dx = ui.W / 2 - dw / 2, dy = a.Bottom - dockH;
        var orders = new[] { ("sword", "CHARGE", "C"), ("up", "ADVANCE", "V"), ("shield", "HOLD", "H"), ("boot", "FALL BACK", "B"), ("arrow", "LOOSE", "F"), ("flag", "FOLLOW", "G") };
        int ow = U(76), ox = ui.W / 2 - orders.Length * (ow + U(4)) / 2;
        for (int i = 0; i < orders.Length; i++)
        {
            var (icon, label, key) = orders[i];
            var r = new Rectangle(ox + i * (ow + U(4)), dy - U(34), ow, U(26));
            bool on = i == 2, hover = ui.Hover(r);
            ui.VGrad(r, on ? Kit.Mul(Kit.Gilt, 0.5f) : hover ? new Color(50, 50, 54) : new Color(30, 32, 38), on ? Kit.Mul(Kit.Gilt, 0.2f) : Kit.IronDark, 1);
            ui.Frame(r, on ? Kit.Gilt : Color.Black * 0.7f, U(1));
            ui.Icon(icon, r.X + U(6), r.Y + U(6), on ? Color.White : Style.Text, 2);
            ui.Text(label, r.X + U(24), r.Y + U(6), on ? Color.White : Style.Text, 1);
            ui.Text(key, r.X + U(24), r.Y + U(15), on ? Kit.Parchment : Style.Dim, 1);
            ui.Block(r);
        }
        ui.Plate(new Rectangle(dx, dy, dw, dockH), solid: false);
        int n = 6, cw = (dw - U(20) - (n - 1) * U(6)) / n;
        for (int i = 0; i < n; i++)
        {
            var r = new Rectangle(dx + U(10) + i * (cw + U(6)), dy + U(10), cw, dockH - U(20));
            if (DockCard(r, i)) dockSel = i;
        }
    }

    /// The battlefield behind the HUD: grass, a road and ford, and the two armies as blocks of men with banners.
    void Field()
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(62, 80, 46), new Color(40, 54, 32), 6);
        var rng = new Random(11);
        for (int i = 0; i < 160; i++)
            ui.Rect(new Rectangle(rng.Next(ui.W), rng.Next(ui.H), U(6 + rng.Next(30)), U(3 + rng.Next(10))), (rng.Next(2) == 0 ? Color.Black : new Color(120, 140, 70)) * 0.08f);
        ui.Line(new Vector2(0, ui.H * 0.62f), new Vector2(ui.W, ui.H * 0.38f), new Color(66, 104, 136), 22);
        ui.Line(new Vector2(0, ui.H * 0.62f), new Vector2(ui.W, ui.H * 0.38f), new Color(96, 140, 170) * 0.5f, 8);
        ui.Line(new Vector2(ui.W * 0.4f, ui.H), new Vector2(ui.W * 0.55f, 0), new Color(120, 100, 70) * 0.7f, 14);
        void Block(float fx, float fy, int cols, int rows, Color c, bool sel, int num)
        {
            int x0 = (int)(ui.W * fx), y0 = (int)(ui.H * fy), sp = U(6);
            ui.Rect(new Rectangle(x0 - U(3), y0 - U(3), cols * sp + U(4), rows * sp + U(4)), Color.Black * 0.18f);
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                    ui.Rect(new Rectangle(x0 + k * sp + (r % 2) * U(1), y0 + r * sp, U(4), U(4)), Color.Lerp(c, Color.Black, r * 0.08f));
            if (sel) ui.Frame(new Rectangle(x0 - U(6), y0 - U(6), cols * sp + U(10), rows * sp + U(10)), Kit.Gilt, U(1));
            int bx = x0 + cols * sp / 2;
            ui.Rect(new Rectangle(bx, y0 - U(26), U(1), U(24)), Kit.Parchment);
            ui.Rect(new Rectangle(bx + U(1), y0 - U(26), U(16), U(11)), c);
            ui.Text(num > 0 ? num.ToString() : "C", bx + U(6), y0 - U(24), Color.White, 1);
            ui.Rect(new Rectangle(bx - U(8), y0 - U(32), U(18), U(2)), Style.Ok);
        }
        var gold = Side(2).Cloth;
        Block(0.18f, 0.66f, 12, 3, gold, false, 1); Block(0.38f, 0.62f, 10, 4, gold, false, 2);
        Block(0.56f, 0.55f, 12, 3, gold, true, 3); Block(0.74f, 0.5f, 8, 2, gold, false, 4);
        Block(0.28f, 0.34f, 12, 3, Style.Sides[1].Cloth, false, 0); Block(0.5f, 0.28f, 14, 3, Style.Sides[1].Cloth, false, 0);
        Block(0.72f, 0.22f, 8, 2, Style.Sides[1].Cloth, false, 0);
        // The ordered move for the selected company: a dashed path and a ghost of where it will stand.
        var from = new Vector2(ui.W * 0.6f, ui.H * 0.55f); var to = new Vector2(ui.W * 0.64f, ui.H * 0.42f);
        for (float t = 0; t < 1; t += 0.12f) ui.Line(Vector2.Lerp(from, to, t), Vector2.Lerp(from, to, t + 0.06f), Kit.Gilt, 2);
        ui.Frame(new Rectangle((int)to.X - U(36), (int)to.Y - U(10), U(72), U(18)), Kit.Gilt * 0.6f, U(1));
    }

    static readonly (string Troop, int Men, float Morale, string Order)[] Dock =
    {
        ("shieldmen", 38, 0.82f, "HLD"), ("pikemen", 31, 0.64f, "HLD"), ("lancers", 16, 0.95f, "CHG"),
        ("longbowmen", 28, 0.75f, "FIRE"), ("crossbowmen", 12, 0.3f, "HLD"), ("greatswords", 12, 0.5f, "FLW"),
    };

    bool DockCard(Rectangle r, int i)
    {
        var (id, men, morale, order) = Dock[i];
        var t = T(id);
        bool sel = i == dockSel, hover = ui.Hover(r);
        ui.VGrad(r, sel ? new Color(70, 60, 36) : hover ? new Color(46, 46, 50) : new Color(30, 32, 38), Kit.IronDark, 2);
        ui.Frame(r, sel ? Kit.Gilt : Color.Black * 0.7f, U(sel ? 2 : 1));
        if (morale < 0.35f) ui.Chip(r.X + U(4), r.Y - U(16), "WAVERING", Style.Warn);
        var port = new Rectangle(r.X + U(4), r.Y + U(4), U(40), r.Height - U(30));
        ui.VGrad(port, Kit.Mul(Side(2).Cloth, 0.5f), Kit.IronDark, 2);
        ui.Portrait(t, port);
        var badge = new Rectangle(r.X + U(4), r.Y + U(4), U(12), U(12));
        ui.Rect(badge, sel ? Kit.Gilt : Color.Black * 0.75f);
        ui.TextC((i + 1).ToString(), badge.Center.X + U(1), badge.Y + U(3), sel ? Kit.IronDark : Style.Text, 1);
        int x = port.Right + U(6), w = r.Right - U(5) - x;
        ui.Text(t.Name.Length > 11 ? t.Name[..11] : t.Name, x, r.Y + U(6), Style.Text, 1);
        ui.Text(men.ToString(), x, r.Y + U(18), Style.Text, 2);
        ui.TextR(order, r.Right - U(5), r.Y + U(22), order == "CHG" ? Style.Orange : Style.Dim, 1);
        int by = r.Bottom - U(22);
        ui.Gauge(new Rectangle(r.X + U(4), by, r.Width - U(8), U(6)), men / 40f, Side(2).Ui);
        ui.Gauge(new Rectangle(r.X + U(4), by + U(9), r.Width - U(8), U(6)), morale, morale > 0.6f ? Style.Ok : morale > 0.35f ? Style.Warn : Style.Bad);
        _ = w;
        ui.Block(r);
        return ui.Clicked(r);
    }

    // ---------- 7. the captain ----------

    void CaptainPage(Rectangle a)
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(28, 30, 40), Kit.IronDark);
        int gap = U(14), lw = U(240), rw = U(220), mw = a.Width - lw - rw - 2 * gap, top = a.Y + U(12), h = a.Bottom - top;

        var c = ui.Plate(new Rectangle(a.X, top, lw, h), "THE CAPTAIN", Kit.Gilt);
        var port = new Rectangle(c.X, c.Y, c.Width, Math.Min(U(170), c.Height / 2));
        ui.VGrad(port, Kit.Mul(Side(2).Cloth, 0.6f), new Color(18, 16, 14), 2);
        ui.Stars(new Rectangle(port.X, port.Y, port.Width, port.Height / 2), time, 20);
        ui.Hills(port, port.Bottom - U(20), U(8), 1.5f, new Color(30, 24, 20));
        ui.Portrait(T("captain"), new Rectangle(port.X, port.Y + U(4), port.Width, port.Height - U(4)), 3 * ui.S);
        ui.Frame(port, Color.Black * 0.6f, U(1));
        ui.Shield(new Rectangle(port.X + U(6), port.Bottom - U(42), U(30), U(36)), Side(2));
        int y = port.Bottom + U(10);
        ui.Text("ALDRIC OF HOLM", c.X, y, Style.Text, 2); y += U(20);
        int cx = c.X;
        cx += ui.Chip(cx, y, "BANNERET", Kit.Gilt, "crown") + U(4);
        ui.Chip(cx, y, "AGE 34", Style.Dim); y += U(22);
        ui.Text("RENOWN", c.X, y, Style.Dim, 1); ui.TextR("420 / 600", c.Right, y, Kit.Gilt, 1); y += U(11);
        ui.Pips(new Rectangle(c.X, y, c.Width, U(8)), 0.7f, Kit.Gilt, 12); y += U(16);
        ui.Text("NEXT: LORD OF THE REALM", c.X, y, Style.Dim * 0.8f, 1); y += U(18);
        cx = c.X;
        foreach (var (trait, col) in new[] { ("BRAVE", Style.Ok), ("STERN", Style.Warn), ("LUCKY", Kit.Gilt) }) cx += ui.Chip(cx, y, trait, col) + U(4);

        // Skills as rings over a perk tree.
        c = ui.Plate(new Rectangle(a.X + lw + gap, top, mw, h), "SKILLS AND PERKS", Style.Friendly);
        var skills = new[] { ("ARMS", 62, "sword"), ("LEADERSHIP", 41, "flag"), ("LOGISTICS", 27, "bread") };
        float rr = Math.Min(U(26), c.Width / 9f);
        for (int i = 0; i < skills.Length; i++)
        {
            var (name, v, _) = skills[i];
            float sx = c.X + c.Width * (i + 0.5f) / skills.Length;
            ui.Ring(new Vector2(sx, c.Y + rr + U(4)), rr, v / 100f, i == 0 ? Style.Bad : i == 1 ? Kit.Gilt : Style.Ok, v.ToString());
            ui.TextC(name, sx, c.Y + 2 * rr + U(14), Style.Text, 1);
        }
        int treeTop = c.Y + (int)(2 * rr) + U(34), rowsH = (c.Bottom - treeTop) / 3, node = Math.Clamp(rowsH - U(40), U(28), U(48)), ic = node >= U(42) ? 3 : 2;
        string[,] perks = { { "IRON GRIP", "RALLY", "FORAGERS" }, { "SHIELD BASH", "WAR CRY", "QUARTERMASTER" }, { "DUELLIST", "BANNER LORD", "SIEGECRAFT" } };
        string[,] icons = { { "sword", "flag", "bread" }, { "shield", "star", "coin" }, { "skull", "crown", "tower" } };
        int[,] state = { { 2, 2, 2 }, { 2, 2, 1 }, { 1, 0, 0 } }; // 2 taken, 1 open, 0 locked
        Point? tip = null;
        for (int col = 0; col < 3; col++)
            for (int row = 0; row < 3; row++)
            {
                int nx = (int)(c.X + c.Width * (col + 0.5f) / 3) - node / 2, ny = treeTop + row * rowsH + (rowsH - node) / 2 - U(6);
                if (row < 2) ui.Rect(new Rectangle(nx + node / 2 - U(1), ny + node, U(3), rowsH - node), state[row + 1, col] > 0 ? Kit.Gilt * 0.7f : Color.White * 0.08f);
                var r = new Rectangle(nx, ny, node, node);
                int s = state[row, col];
                bool hover = ui.Hover(r);
                ui.Rect(new Rectangle(r.X + U(2), r.Y + U(3), r.Width, r.Height), Color.Black * 0.4f);
                ui.VGrad(r, s == 2 ? Kit.Mul(Kit.Gilt, 0.6f) : s == 1 ? new Color(46, 46, 52) : new Color(24, 24, 28), s == 2 ? Kit.Mul(Kit.Gilt, 0.25f) : Kit.IronDark, 1);
                ui.Frame(r, s == 2 ? Kit.Gilt : s == 1 ? (hover ? Style.Text : Style.PanelEdge * 2f) : Color.Black * 0.6f, U(1));
                ui.Icon(icons[row, col], r.X + (node - U(7 * ic)) / 2, r.Y + (node - U(7 * ic)) / 2, s == 2 ? Color.White : s == 1 ? Style.Text : Style.Dim * 0.4f, ic);
                ui.TextC(perks[row, col], r.Center.X, r.Bottom + U(4), s == 0 ? Style.Dim * 0.5f : Style.Text, 1);
                if (hover || (row == 1 && col == 1 && !tip.HasValue)) tip = new Point(r.Center.X, r.Y - U(2));
            }
        if (tip is Point tp) ui.Tooltip(tp.X, tp.Y, "WAR CRY", "ONCE A BATTLE: +20 MORALE", "TO EVERY MAN IN 30 M.", "NEEDS LEADERSHIP 40.");

        // The record.
        c = ui.Plate(new Rectangle(a.Right - rw, top, rw, h), "RECORD", Kit.Gilt);
        y = c.Y;
        foreach (var (icon, label, value) in new[] { ("flag", "BATTLES", "23"), ("star", "WON", "19"), ("skull", "FOES SLAIN", "1,066"), ("heart", "MEN LOST", "141"), ("tower", "SIEGES", "3"), ("clock", "DAYS IN THE FIELD", "41") })
        {
            ui.Icon(icon, c.X, y, Style.Dim, 1);
            ui.Text(label, c.X + U(12), y, Style.Dim, 1);
            ui.TextR(value, c.Right, y, Style.Text, 1);
            y += U(15);
        }
        y += U(6);
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f); y += U(12);
        if (c.Bottom - y > U(150))
        {
            foreach (var (label, v, col) in new[] { ("HEALTH", 0.86f, Style.Ok), ("FATIGUE", 0.32f, Style.Warn) })
            {
                ui.Text(label, c.X, y, Style.Dim, 1); ui.TextR($"{(int)(v * 100)}%", c.Right, y, col, 1); y += U(11);
                ui.Gauge(new Rectangle(c.X, y, c.Width, U(7)), v, col); y += U(14);
            }
            foreach (var l in ui.Wrap("AN OLD SCAR ON THE SWORD ARM. -5% ARMS IN THE RAIN.", c.Width)) { ui.Text(l, c.X, y, Style.Dim * 0.8f, 1); y += U(11); }
            y += U(6);
            ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f); y += U(12);
        }
        ui.Text("RENOWN BY WEEK", c.X, y, Style.Dim, 1); y += U(12);
        int chH = Math.Clamp(c.Bottom - y - U(16), U(30), U(130));
        ui.Chart(new Rectangle(c.X, y, c.Width, chH), new[] { 0.05f, 0.1f, 0.14f, 0.26f, 0.3f, 0.46f, 0.7f }, Kit.Gilt);
        for (int i = 0; i < 7; i++) ui.TextC($"W{i + 1}", c.X + c.Width * i / 6f, y + chH + U(5), Style.Dim * 0.7f, 1);
    }

    // ---------- 8. realms ----------

    void RealmsPage(Rectangle a)
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(26, 32, 36), Kit.IronDark);
        var realms = new[]
        {
            ("AZURE CROWN", "HEAVY FOOT AND LANCES", 0, 0.8f, "YOUR LIEGE", Style.Ok, 7, 3, 5, 0.3f),
            ("CRIMSON COMPACT", "GREATSWORDS AND LONGBOWS", 1, 0.66f, "AT WAR", Style.Bad, 6, 4, 3, 0.62f),
            ("NORTHMARCH", "AXES AND LIGHT HORSE", 4, 0.44f, "TRUCE 12 DAYS", Style.Warn, 4, 2, 4, 0.48f),
            ("BRIGANDS", "OUTLAWS OF THE MOORS", 3, 0.18f, "HUNTED", Style.Orange, 0, 0, 0, 0f),
        };
        string[][] signature = { new[] { "men_at_arms", "crossbowmen", "lancers" }, new[] { "greatswords", "longbowmen", "halberdiers" },
            new[] { "huscarls", "glaivemen", "outriders" }, new[] { "levies", "clansmen", "bowmen" } };
        string[] rulers = { "KING EDMER II", "THE COUNCIL OF SIX", "JARL HROTHGAR", "RED WAT OF THE MOOR" };
        int gap = U(12), cw = (a.Width - 3 * gap) / 4, top = a.Y + U(14), ch = (int)((a.Bottom - top) * 0.58f);
        for (int i = 0; i < realms.Length; i++)
        {
            var (name, text, pal, str, stance, sc, lords, towns, castles, weary) = realms[i];
            var c = ui.Plate(new Rectangle(a.X + i * (cw + gap), top, cw, ch), null, Side(pal).Ui);
            int sh = Math.Min(U(72), c.Height / 3);
            ui.Shield(new Rectangle(c.Center.X - (int)(sh / 1.2f) / 2, c.Y - U(2), (int)(sh / 1.2f), sh), Side(pal));
            int y = c.Y + sh + U(6);
            ui.TextC(name, c.Center.X, y, Style.Text, 1); y += U(11);
            ui.TextC(text, c.Center.X, y, Style.Dim, 1); y += U(14);
            int chipW = ui.TW(stance, 1) + U(10);
            ui.Chip(c.Center.X - chipW / 2, y, stance, sc); y += U(20);
            ui.Text("STRENGTH", c.X, y, Style.Dim, 1); y += U(10);
            ui.Pips(new Rectangle(c.X, y, c.Width, U(7)), str, Side(pal).Ui, 10); y += U(14);
            if (lords > 0)
            {
                ui.Text("WAR WEARINESS", c.X, y, Style.Dim, 1); y += U(10);
                ui.Gauge(new Rectangle(c.X, y, c.Width, U(5)), weary, weary > 0.55f ? Style.Bad : Style.Warn); y += U(12);
            }
            int third = c.Width / 3;
            foreach (var (k, icon, v) in new[] { (0, "crown", lords), (1, "tower", towns), (2, "flag", castles) })
            {
                if (y + U(20) > c.Bottom || lords == 0) break;
                int ix = c.X + k * third + third / 2;
                ui.Icon(icon, ix - U(12), y, Style.Dim, 1);
                ui.Text(v.ToString(), ix, y, Style.Text, 1);
            }
            y += U(lords > 0 ? 18 : 0);
            if (y + U(12) < c.Bottom) { ui.Text("RULED BY", c.X, y, Style.Dim, 1); ui.TextR(rulers[i], c.Right, y, Style.Text, 1); y += U(16); }
            int ps = Math.Min((c.Width - U(8)) / 3, c.Bottom - y);
            if (ps >= U(34))
            {
                for (int k = 0; k < 3; k++)
                {
                    var pr = new Rectangle(c.X + k * (ps + U(4)), y, ps, ps);
                    ui.VGrad(pr, Kit.Mul(Side(pal).Cloth, 0.5f), Kit.IronDark, 2);
                    ui.Portrait(T(signature[i][k]), pr, Math.Max(1, ps / U(36)) * ui.S);
                    ui.Frame(pr, Color.Black * 0.6f, U(1));
                }
            }
        }

        // Relations and the chronicle.
        int by = top + ch + gap + U(10), bh = a.Bottom - by, lw = (int)(a.Width * 0.45f);
        var m = ui.Plate(new Rectangle(a.X, by, lw, bh), "RELATIONS", Kit.Gilt);
        int[] pals = { 0, 1, 4, 3 };
        string[] shortNames = { "AZURE", "CRIMSON", "NORTH", "OUTLAW" };
        string[,] rel = { { "", "WAR", "TRUCE", "WAR" }, { "WAR", "", "PEACE", "WAR" }, { "TRUCE", "PEACE", "", "WAR" }, { "WAR", "WAR", "WAR", "" } };
        int cell = Math.Min((m.Width - U(56)) / 4, (m.Height - U(14)) / 4);
        int ox = m.X + U(56), oy = m.Y + U(14);
        for (int i = 0; i < 4; i++)
        {
            string head = ui.TW(shortNames[i], 1) > cell - U(4) ? shortNames[i][..3] : shortNames[i];
            ui.TextC(head, ox + i * cell + cell / 2f, m.Y, Side(pals[i]).Ui, 1);
            ui.Text(shortNames[i], m.X, oy + i * cell + cell / 2 - U(3), Side(pals[i]).Ui, 1);
            for (int j = 0; j < 4; j++)
            {
                var r = new Rectangle(ox + j * cell + U(1), oy + i * cell + U(1), cell - U(2), cell - U(2));
                string s = rel[i, j];
                var col = s switch { "WAR" => Style.Bad, "TRUCE" => Style.Warn, "PEACE" => Style.Ok, _ => Color.White * 0.2f };
                ui.Rect(r, s == "" ? Color.Black * 0.3f : Kit.Mul(col, 0.35f));
                ui.Frame(r, s == "" ? Color.Black * 0.5f : Kit.Mul(col, 0.8f), U(1));
                if (s != "") ui.TextC(s, r.Center.X + U(1), r.Center.Y - U(3), Color.Lerp(col, Color.White, 0.5f), 1);
            }
        }
        var ch2 = ui.Plate(new Rectangle(a.X + lw + gap, by, a.Width - lw - gap, bh), "CHRONICLE", Style.Friendly);
        int yy = ch2.Y;
        foreach (var (day, text, col) in new[]
                 {
                     ("DAY 41", "YOU BROKE LORD OSRIC AT LOWMOOR.", Kit.Gilt), ("DAY 38", "THE CRIMSON COMPACT BESIEGES HARLOW.", Style.Foe),
                     ("DAY 33", "NORTHMARCH AND THE CROWN AGREE A TRUCE.", Style.Warn), ("DAY 29", "UFFING IS GRANTED TO SIR BERRAN.", Style.Text),
                     ("DAY 21", "WAR: THE COMPACT MARCHES ON THE CROWN.", Style.Bad), ("DAY 14", "RED WAT'S MEN BURN TWO VILLAGES IN THE MOOR.", Style.Orange),
                     ("DAY 6", "YOU SIGN ON WITH THE AZURE CROWN.", Kit.Gilt), ("DAY 1", "THE GOLDEN COMPANY IS RAISED AT HOLM.", Style.Text),
                 })
        {
            if (yy + U(14) > ch2.Bottom) break;
            ui.Rect(new Rectangle(ch2.X + U(1), yy + U(5), U(1), U(16)), Color.White * 0.1f);
            ui.Rect(new Rectangle(ch2.X, yy + U(2), U(3), U(3)), col);
            ui.Text(day, ch2.X + U(10), yy, Style.Dim, 1);
            ui.Text(text, ch2.X + U(64), yy, Style.Text, 1);
            yy += Math.Clamp(ch2.Height / 8, U(16), U(26));
        }
    }

    // ---------- 9. event ----------

    void EventPage(Rectangle a)
    {
        var screen = new Rectangle(0, 0, ui.W, ui.H);
        ui.VGrad(screen, Kit.Night, new Color(40, 30, 40));
        ui.Stars(new Rectangle(0, 0, ui.W, ui.H / 2), time, 60);
        int kx = (int)(ui.W * 0.15f);
        for (int g = 0; g < 5; g++) ui.Disc(new Vector2(kx, ui.H * 0.64f), U(90 - g * 16), Kit.Ember * 0.03f);
        ui.Castle(kx, ui.HillY(screen, kx, (int)(ui.H * 0.7f), U(24), 2.2f) + U(6), 3, new Color(26, 22, 30), Kit.Ember);
        ui.Hills(screen, (int)(ui.H * 0.7f), U(24), 2.2f, new Color(26, 22, 30));
        ui.Hills(screen, (int)(ui.H * 0.85f), U(14), 4.4f, new Color(14, 12, 18));
        ui.Rect(screen, Color.Black * 0.15f);
        ui.Vignette(0.9f);

        string body = "SMOKE RISES FROM HARLOW. A BOY ON A MULE SAYS CRIMSON RIDERS ARE BURNING THE BARNS, FORTY MEN AT MOST, AND THE REEVE IS HIDING IN THE CHURCH WITH THE GRAIN TAX.";
        int w = Math.Min(U(560), a.Width), picH = Math.Clamp(a.Height - U(330), U(110), U(190));
        int lines = ui.Wrap(body, w - U(28), 2).Count;
        int h = Math.Min(a.Height, picH + U(22) + lines * U(18) + U(20) + 3 * U(40) + U(20));
        var c = ui.Plate(new Rectangle(ui.W / 2 - w / 2, a.Y + (a.Height - h) / 2, w, h), null, Kit.Gilt);
        var pic = new Rectangle(c.X - U(4), c.Y - U(4), c.Width + U(8), picH);
        Burning(pic);
        ui.Frame(pic, Color.Black * 0.7f, U(1));
        ui.Ribbon(c.Center.X, pic.Bottom - U(12), "SMOKE OVER HARLOW", new Color(130, 50, 36), 2);
        int y = pic.Bottom + U(22);
        foreach (var line in ui.Wrap(body, c.Width, 2))
        {
            ui.Text(line, c.X, y, Kit.Parchment, 2);
            y += U(18);
        }
        y += U(6);
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f);
        y += U(14);
        var choices = new[]
        {
            ("RIDE TO HARLOW AT ONCE", new[] { ("+20 RENOWN", Kit.Gilt, "star"), ("MEN TIRED", Style.Warn, "boot") }),
            ("SEND THE LANCERS AHEAD", new[] { ("RISK 16 MEN", Style.Bad, "skull"), ("+10 RENOWN", Kit.Gilt, "star") }),
            ("LET IT BURN", new[] { ("-15 WITH THE CROWN", Style.Bad, "crown") }),
        };
        int ch = Math.Min(U(34), (c.Bottom - y) / choices.Length - U(4));
        for (int i = 0; i < choices.Length; i++)
        {
            var (label, effects) = choices[i];
            var r = new Rectangle(c.X, y, c.Width, ch);
            bool hover = ui.Hover(r);
            ui.VGrad(r, hover ? Kit.Mul(Kit.Gilt, 0.4f) : new Color(36, 34, 36), hover ? Kit.Mul(Kit.Gilt, 0.15f) : new Color(22, 22, 26), 1);
            ui.Frame(r, hover ? Kit.Gilt : Color.Black * 0.6f, U(1));
            var num = new Rectangle(r.X + U(4), r.Y + U(4), ch - U(8), ch - U(8));
            ui.Rect(num, hover ? Kit.Gilt : Color.Black * 0.6f);
            ui.TextC((i + 1).ToString(), num.Center.X + U(1), num.Center.Y - U(3), hover ? Kit.IronDark : Kit.Gilt, 1);
            ui.Text(label, num.Right + U(10), r.Y + (ch - U(7)) / 2, Style.Text, 1);
            int ex = r.Right - U(6);
            foreach (var (text, col, icon) in effects.Reverse())
            {
                int ew = ui.TW(text, 1) + U(20);
                ex -= ew;
                ui.Chip(ex, r.Y + (ch - U(13)) / 2, text, col, icon);
                ex -= U(4);
            }
            ui.Block(r);
            ui.Clicked(r);
            y += ch + U(6);
        }
    }

    /// The event's picture: a village burning under the moon, with smoke drifting up.
    void Burning(Rectangle p)
    {
        ui.VGrad(p, Kit.Night, new Color(120, 54, 40), 2);
        ui.Stars(new Rectangle(p.X, p.Y, p.Width, p.Height / 2), time, 30);
        ui.Disc(new Vector2(p.X + p.Width * 0.82f, p.Y + p.Height * 0.28f), U(12), new Color(236, 226, 200));
        ui.Hills(p, p.Bottom - U(36), U(10), 0.9f, new Color(54, 30, 36));
        int ground = p.Bottom - U(22);
        ui.Hills(p, ground + U(6), U(4), 3.3f, new Color(22, 14, 18));
        for (int i = 0; i < 6; i++)
        {
            int hx = p.X + (int)(p.Width * (0.2f + i * 0.09f)), hw = U(14 + i % 3 * 4), hh = U(10 + i % 2 * 4);
            ui.Rect(new Rectangle(hx, ground - hh, hw, hh), new Color(20, 12, 14));
            for (int k = 0; k < hw / 2; k += Math.Max(1, U(1))) ui.Rect(new Rectangle(hx + k, ground - hh - (hw / 2 - k) / 2 - U(1), hw - 2 * k, Math.Max(1, U(1))), new Color(20, 12, 14));
            if (i % 2 == 0)
            {
                ui.Disc(new Vector2(hx + hw / 2, ground - hh), U(14), Kit.Ember * 0.12f);
                ui.Icon("fire", hx + hw / 2 - U(7), ground - hh - U(12), Kit.Ember, 2);
                for (int s = 0; s < 5; s++)
                {
                    float t = (time * 0.3f + s * 0.2f + i) % 1;
                    ui.Disc(new Vector2(hx + hw / 2 + t * U(30), ground - hh - U(14) - t * p.Height * 0.6f), U(4) + t * U(10), new Color(60, 50, 56) * (0.5f * (1 - t)));
                }
            }
        }
        ui.Rect(new Rectangle(p.X + (int)(p.Width * 0.38f), ground - U(24), U(4), U(24)), new Color(20, 12, 14)); // church tower
        ui.Rect(new Rectangle(p.X + (int)(p.Width * 0.38f) - U(2), ground - U(28), U(8), U(4)), new Color(20, 12, 14));
    }
}
