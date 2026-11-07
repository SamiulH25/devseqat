using Microsoft.Xna.Framework;

namespace Ironfield;

/// UI lab pages, part one: the widget kit, a title screen, the muster roll, the after-action report and a town market.
sealed partial class UiLab
{
    // Live state for the widgets, so the controls can be tried.
    bool optA = true, optB, optC = true, chkA = true, chkB;
    float volume = 0.7f, scale = 0.45f;
    int stepper = 12, tab, selected = 1, order = 2;
    readonly int[] hire = { 8, 0, 4, 0 };

    int U(float v) => ui.U(v);
    static UnitType T(string id) => Troops.Get(id);
    static Heraldry Side(int palette) => Style.Sides[palette];

    /// A small caption above a group of widgets; moves the cursor down.
    void Caption(string s, int x, ref int y, int w)
    {
        ui.Text(s, x, y, Kit.Gilt * 0.9f, 1);
        ui.Rect(new Rectangle(x + ui.TW(s, 1) + U(6), y + U(3), w - ui.TW(s, 1) - U(6), U(1)), Color.White * 0.08f);
        y += U(14);
    }

    // ---------- 1. the kit ----------

    void KitPage(Rectangle a)
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(24, 26, 34), Kit.IronDark);
        int gap = U(16), cw = (a.Width - 2 * gap) / 3, top = a.Y + U(12), h = a.Bottom - top;

        // Buttons, tabs, chips.
        var c = ui.Plate(new Rectangle(a.X, top, cw, h), "BUTTONS", Kit.Gilt);
        int y = c.Y, x = c.X, w = c.Width;
        Caption("STANDARD", x, ref y, w);
        ui.Button(new Rectangle(x, y, w, U(26)), "MARCH", note: "M"); y += U(30);
        ui.Button(new Rectangle(x, y, w / 2 - U(2), U(26)), "ACTIVE", active: true);
        ui.Button(new Rectangle(x + w / 2 + U(2), y, w / 2 - U(2), U(26)), "DISABLED", false); y += U(36);
        Caption("GILDED", x, ref y, w);
        ui.Gilded(new Rectangle(x, y, w, U(30)), "HIRE MEN", "H"); y += U(36);
        ui.Gilded(new Rectangle(x, y, w / 2 - U(3), U(26)), "CONFIRM", tone: Style.Ok);
        ui.Gilded(new Rectangle(x + w / 2 + U(3), y, w / 2 - U(3), U(26)), "DISMISS", tone: Style.Bad); y += U(36);
        Caption("TABS", x, ref y, w);
        if (ui.Tabs(new Rectangle(x, y, w, U(20)), new[] { "ARMY", "TOWN", "REALM" }, tab) is int t and >= 0) tab = t;
        y += U(30);
        Caption("CHIPS", x, ref y, w);
        int cx = x;
        cx += ui.Chip(cx, y, "AT WAR", Style.Bad) + U(4);
        cx += ui.Chip(cx, y, "ALLY", Style.Ok) + U(4);
        ui.Chip(cx, y, "TRUCE", Style.Warn);
        y += U(17);
        cx = x;
        cx += ui.Chip(cx, y, "+24 RENOWN", Kit.Gilt, "star") + U(4);
        ui.Chip(cx, y, "-80", Style.Bad, "coin");
        y += U(26);
        if (y + U(44) < c.Bottom)
        {
            Caption("RIBBONS", x, ref y, w);
            ui.Ribbon(x + w / 4, y, "VICTORY", new Color(150, 116, 40), 1);
            ui.Ribbon(x + 3 * w / 4, y, "DEFEAT", new Color(130, 50, 36), 1);
            y += U(34);
        }
        if (y + U(54) < c.Bottom)
        {
            Caption("ORDER KEYS", x, ref y, w);
            var keys = new[] { ("sword", "C"), ("up", "V"), ("shield", "H"), ("boot", "B"), ("arrow", "F"), ("flag", "G") };
            int kw = Math.Min(U(40), (w - U(4) * (keys.Length - 1)) / keys.Length);
            for (int i = 0; i < keys.Length; i++) if (ui.OrderKey(new Rectangle(x + i * (kw + U(4)), y, kw, U(34)), keys[i].Item1, keys[i].Item2, i == order)) order = i;
            y += U(44);
        }
        if (y + U(8) < c.Bottom) ui.Divider(x, y, w, Kit.Gilt * 0.7f);

        // Toggles, checks, sliders, stepper, a tooltip.
        c = ui.Plate(new Rectangle(a.X + cw + gap, top, cw, h), "CONTROLS", Style.Friendly);
        y = c.Y; x = c.X; w = c.Width;
        Caption("TOGGLES", x, ref y, w);
        ui.Toggle(new Rectangle(x, y, w, U(18)), "MARCH AT NIGHT", ref optA); y += U(22);
        ui.Toggle(new Rectangle(x, y, w, U(18)), "AUTO-RESOLVE SKIRMISHES", ref optB); y += U(22);
        ui.Toggle(new Rectangle(x, y, w, U(18)), "SHOW BANNERS", ref optC); y += U(28);
        Caption("CHECKS", x, ref y, w);
        ui.Check(new Rectangle(x, y, w, U(16)), "PAY WAGES FIRST", ref chkA); y += U(20);
        ui.Check(new Rectangle(x, y, w, U(16)), "SELL LOOT IN TOWN", ref chkB); y += U(26);
        Caption("SLIDERS", x, ref y, w);
        ui.Slider(new Rectangle(x, y, w, U(24)), "MUSIC", ref volume, held); y += U(30);
        ui.Slider(new Rectangle(x, y, w, U(24)), "RATIONS", ref scale, held); y += U(32);
        Caption("STEPPER", x, ref y, w);
        stepper = Math.Clamp(stepper + ui.Stepper(new Rectangle(x, y, U(100), U(20)), stepper), 0, 99);
        ui.Text("MEN TO HIRE", x + U(110), y + U(7), Style.Dim, 1);
        y += U(30);
        if (y + U(60) < c.Bottom - U(50))
        {
            Caption("TOASTS", x, ref y, w);
            ui.Toast(x, y, "ENEMY HORSE ON THE LEFT!", Style.Orange, "horse"); y += U(22);
            ui.Toast(x, y, "THE PIKES HOLD THE FORD", Style.Ok, "shield");
        }
        ui.Tooltip(c.Center.X, c.Bottom - U(2), "SHIELD WALL", "LOCKED SHIELDS: +60% BLOCK", "-40% SPEED. SPEARS BRACE.");

        // Readouts.
        c = ui.Plate(new Rectangle(a.X + 2 * (cw + gap), top, cw, h), "READOUTS", Style.Foe);
        y = c.Y; x = c.X; w = c.Width;
        Caption("BARS", x, ref y, w);
        ui.Gauge(new Rectangle(x, y, w, U(10)), 0.72f, Style.Ok); y += U(14);
        ui.Pips(new Rectangle(x, y, w, U(10)), 0.46f, Style.Warn, 12); y += U(18);
        ui.TugBar(new Rectangle(x, y, w, U(8)), 0.58f, Style.Friendly, Style.Foe); y += U(20);
        Caption("RINGS", x, ref y, w);
        float rr = Math.Min(U(26), w / 7f);
        ui.Ring(new Vector2(x + w / 6f, y + rr + U(2)), rr, 0.78f, Style.Ok, "78", "MORALE");
        ui.Ring(new Vector2(x + w / 2f, y + rr + U(2)), rr, 0.35f, Style.Warn, "12", "FOOD");
        ui.Ring(new Vector2(x + 5 * w / 6f, y + rr + U(2)), rr, 0.15f, Style.Bad, "3", "UNREST");
        y += (int)(2 * rr) + U(14);
        Caption("CHART", x, ref y, w);
        ui.Chart(new Rectangle(x, y, w, U(44)), new[] { 0.2f, 0.35f, 0.3f, 0.5f, 0.62f, 0.55f, 0.8f, 0.9f }, Kit.Gilt); y += U(52);
        Caption("ICONS", x, ref y, w);
        int ix = x, n = 0;
        foreach (var name in Kit.IconNames)
        {
            ui.Icon(name, ix, y, n % 3 == 0 ? Kit.Gilt : n % 3 == 1 ? Style.Text : Style.Dim, 2);
            ix += U(19);
            if (ix > x + w - U(14)) { ix = x; y += U(19); }
            n++;
        }
        y += U(24);
        Caption("HERALDRY", x, ref y, w);
        int sw = Math.Min(U(36), (w - U(16)) / 5);
        for (int i = 0; i < 5; i++) ui.Shield(new Rectangle(x + i * (sw + U(4)), y, sw, (int)(sw * 1.2f)), Side(i));
        y += (int)(sw * 1.2f) + U(12);
        if (y + U(110) < c.Bottom)
        {
            Caption("BANNERS", x, ref y, w);
            for (int i = 0; i < 3; i++) ui.Banner(x + w * (2 * i + 1) / 6, y + U(4), U(40), Math.Min(U(110), c.Bottom - y - U(8)), Side(new[] { 0, 1, 4 }[i]), time * 1.5f + i);
        }
    }

    // ---------- 2. title screen ----------

    void TitlePage(Rectangle a)
    {
        var screen = new Rectangle(0, 0, ui.W, ui.H);
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H / 2), Kit.Night, Kit.Dusk);
        ui.VGrad(new Rectangle(0, ui.H / 2, ui.W, ui.H / 2), Kit.Dusk, new Color(196, 110, 62));
        ui.Stars(new Rectangle(0, 0, ui.W, ui.H / 2), time);
        var moon = new Vector2(ui.W * 0.8f, ui.H * 0.2f);
        ui.Disc(moon, U(46), Color.White * 0.04f); ui.Disc(moon, U(34), Color.White * 0.06f);
        ui.Disc(moon, U(24), new Color(240, 232, 210)); ui.Disc(moon + new Vector2(U(6), U(-4)), U(5), new Color(214, 206, 186));
        int castleX = (int)(ui.W * 0.76f);
        ui.Castle(castleX, ui.HillY(screen, castleX, (int)(ui.H * 0.66f), U(26), 0.4f) + U(6), 3, new Color(78, 50, 70), Kit.Ember);
        ui.Hills(screen, (int)(ui.H * 0.66f), U(26), 0.4f, new Color(78, 50, 70));
        ui.Hills(screen, (int)(ui.H * 0.76f), U(22), 3.1f, new Color(46, 32, 48));
        ui.Hills(screen, (int)(ui.H * 0.88f), U(14), 5.7f, new Color(20, 16, 26));
        ui.Vignette(0.8f);

        int cx = ui.W / 2;
        int ty = (int)(ui.H * 0.13f);
        ui.TextC("IRONFIELD", cx + U(3), ty + U(4), Kit.GiltDark * 0.8f, 8);
        ui.TextC("IRONFIELD", cx, ty, Kit.Gilt, 8);
        ui.Rect(new Rectangle(cx - ui.TW("IRONFIELD", 8) / 2, ty + U(8), ui.TW("IRONFIELD", 8), U(2)), Color.White * 0.15f);
        ui.Divider(cx - U(180), ty + U(70), U(360), Kit.Gilt);
        ui.TextC("A CAPTAIN'S WAR", cx, ty + U(80), Kit.Parchment, 2);

        int mw = U(250), my = (int)(ui.H * 0.4f);
        var m = ui.Plate(new Rectangle(cx - mw / 2, my, mw, U(206)), solid: false);
        int y = m.Y;
        foreach (var (label, key) in new[] { ("NEW CAMPAIGN", ""), ("CONTINUE", "DAY 41"), ("QUICK BATTLE", ""), ("SETTINGS", ""), ("QUIT", "") })
        {
            ui.Gilded(new Rectangle(m.X, y, m.Width, U(30)), label, key);
            y += U(36);
        }
        float sway = time * 1.5f;
        ui.Banner(cx - mw / 2 - U(64), my - U(10), U(52), U(150), Side(0), sway);
        ui.Banner(cx + mw / 2 + U(64), my - U(10), U(52), U(150), Side(1), sway + 1.3f);

        // The last campaign, as a small card.
        var card = ui.Plate(new Rectangle(U(24), ui.H - U(104), U(250), U(80)), null, Kit.Gilt);
        ui.Shield(new Rectangle(card.X - U(2), card.Y - U(2), U(40), U(48)), Side(2));
        ui.Text("ALDRIC OF HOLM", card.X + U(46), card.Y, Style.Text, 1);
        ui.Text("BANNERET OF THE AZURE CROWN", card.X + U(46), card.Y + U(11), Style.Dim, 1);
        ui.Icon("clock", card.X + U(46), card.Y + U(26), Style.Dim, 1); ui.Text("DAY 41", card.X + U(58), card.Y + U(26), Style.Text, 1);
        ui.Icon("coin", card.X + U(104), card.Y + U(26), Kit.Gilt, 1); ui.Text("1,240", card.X + U(116), card.Y + U(26), Style.Text, 1);
        ui.Icon("shield", card.X + U(46), card.Y + U(39), Style.Dim, 1); ui.Text("212 MEN", card.X + U(58), card.Y + U(39), Style.Text, 1);
        ui.TextR("F2 GRAPHICS   V0.9", ui.W - U(24), ui.H - U(24), Style.Dim * 0.6f, 1);
    }

    // ---------- 3. muster roll ----------

    static readonly (string Troop, int Men, int Max, float Morale, float Xp, int Wounded)[] Roll =
    {
        ("shieldmen", 38, 40, 0.82f, 0.4f, 2), ("men_at_arms", 24, 24, 0.9f, 1, 0), ("pikemen", 31, 36, 0.64f, 0.7f, 5),
        ("longbowmen", 28, 30, 0.75f, 0.2f, 1), ("crossbowmen", 18, 24, 0.4f, 0.55f, 6), ("lancers", 16, 16, 0.95f, 0.85f, 0),
        ("greatswords", 12, 20, 0.28f, 0.3f, 8), ("huscarls", 20, 20, 0.7f, 1, 0),
    };

    void MusterPage(Rectangle a)
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(30, 28, 32), Kit.IronDark);
        int side = U(230), gap = U(12);
        var grid = new Rectangle(a.X, a.Y + U(44), a.Width - side - U(16), a.Height - U(44));
        ui.Text("THE GOLDEN COMPANY", a.X, a.Y + U(6), Kit.Gilt, 3);
        ui.Text("8 COMPANIES . 187 MEN . 2 READY TO TRAIN", a.X, a.Y + U(32), Style.Dim, 1);
        int cw = (grid.Width - 3 * gap) / 4, ch = (grid.Height - gap) / 2;
        for (int i = 0; i < Roll.Length; i++)
        {
            var r = new Rectangle(grid.X + i % 4 * (cw + gap), grid.Y + i / 4 * (ch + gap), cw, ch);
            if (CompanyCard(r, i, i == selected)) selected = i;
        }

        var s = ui.Plate(new Rectangle(a.Right - side, a.Y + U(12), side, a.Height - U(12)), "CAPTAIN", Kit.Gilt);
        ui.Shield(new Rectangle(s.X, s.Y + U(2), U(48), U(58)), Side(2));
        ui.Text("ALDRIC", s.X + U(56), s.Y + U(6), Style.Text, 2);
        ui.Text("OF HOLM", s.X + U(56), s.Y + U(24), Style.Dim, 1);
        ui.Chip(s.X + U(56), s.Y + U(38), "BANNERET", Kit.Gilt, "crown");
        int y = s.Y + U(72);
        float rr = Math.Min(U(24), s.Width / 6.5f);
        ui.Ring(new Vector2(s.X + s.Width / 4f, y + rr), rr, 0.78f, Style.Ok, "78", "MORALE");
        ui.Ring(new Vector2(s.X + 3 * s.Width / 4f, y + rr), rr, 0.4f, Style.Warn, "12", "DAYS FOOD");
        y += (int)(2 * rr) + U(18);
        ui.Divider(s.X, y, s.Width, Kit.Gilt * 0.6f); y += U(10);
        foreach (var (icon, label, value, col) in new[]
                 {
                     ("coin", "PURSE", "1,240", Kit.Gilt), ("scroll", "WAGES A WEEK", "-318", Style.Bad), ("shield", "MEN", "187 / 210", Style.Text),
                     ("horse", "HORSES", "16 + 6 SPARE", Style.Text), ("heart", "WOUNDED", "22", Style.Warn), ("boot", "MARCH", "SLOW", Style.Orange),
                 })
        {
            ui.Icon(icon, s.X, y, Style.Dim, 1);
            ui.Text(label, s.X + U(12), y, Style.Dim, 1);
            ui.TextR(value, s.Right, y, col, 1);
            y += U(15);
        }
        y += U(8);
        if (y + U(40) < s.Bottom - U(34))
        {
            ui.Text("THE COMPANY IS", s.X, y, Style.Dim, 1); y += U(12);
            var parts = new[] { ("FOOT", 4, Side(2).Ui), ("BOWS", 2, Style.Ok), ("HORSE", 1, Style.Orange), ("HEAVY", 1, Style.Friendly) };
            int bx = s.X, total = parts.Sum(p => p.Item2);
            foreach (var (_, n, col) in parts) { int bw = s.Width * n / total; ui.Gauge(new Rectangle(bx, y, bw - U(2), U(8)), 1, col); bx += bw; }
            y += U(14);
            bx = s.X;
            for (int k = 0; k < parts.Length; k++)
            {
                var (name, n, col) = parts[k];
                int lx = s.X + k % 2 * s.Width / 2, ly = y + k / 2 * U(12);
                ui.Rect(new Rectangle(lx, ly + U(2), U(4), U(4)), col);
                ui.Text($"{name} {n}", lx + U(8), ly, Style.Dim, 1);
            }
        }
        ui.Gilded(new Rectangle(s.X, s.Bottom - U(28), s.Width, U(28)), "TRAIN ALL", "U");
    }

    /// A company card: portrait over a wash of the side colour, tier and index badges, men, strength, morale and drill.
    bool CompanyCard(Rectangle r, int i, bool sel)
    {
        var (id, men, max, morale, xp, wounded) = Roll[i];
        var t = T(id);
        bool hover = ui.Hover(r);
        ui.Rect(new Rectangle(r.X + U(2), r.Y + U(3), r.Width, r.Height), Color.Black * 0.35f);
        ui.VGrad(r, hover ? new Color(40, 40, 46) : Kit.Iron, Kit.IronDark, 2);
        var port = new Rectangle(r.X + U(4), r.Y + U(4), r.Width - U(8), Math.Clamp(r.Height - U(124), U(64), U(150)));
        ui.VGrad(port, Kit.Mul(Side(2).Cloth, 0.55f), new Color(20, 18, 16), 2);
        ui.Rect(new Rectangle(port.X, port.Bottom - U(10), port.Width, U(10)), Color.Black * 0.25f);
        ui.Portrait(t, port, (port.Height >= U(120) ? 3 : 2) * ui.S);
        ui.Frame(r, sel ? Kit.Gilt : hover ? Style.PanelEdge * 2f : Color.Black * 0.6f, U(sel ? 2 : 1));

        // Badges: index (top left), tier (top right).
        var badge = new Rectangle(r.X + U(6), r.Y + U(6), U(16), U(16));
        ui.Rect(badge, sel ? Kit.Gilt : Color.Black * 0.7f);
        ui.TextC((i + 1).ToString(), badge.Center.X + U(1), badge.Y + U(5), sel ? Kit.IronDark : Style.Text, 1);
        string tier = Style.Numerals[Math.Clamp(t.Tier, 0, 7)];
        var tb = new Rectangle(r.Right - U(10) - ui.TW(tier, 1) - U(4), r.Y + U(6), ui.TW(tier, 1) + U(8), U(16));
        ui.Rect(tb, Color.Black * 0.7f); ui.Frame(tb, Kit.Gilt * 0.7f, U(1));
        ui.TextC(tier, tb.Center.X + U(1), tb.Y + U(5), Kit.Gilt, 1);

        int x = r.X + U(8), w = r.Width - U(16), y = port.Bottom + U(6);
        ui.Text(t.Name, x, y, Style.Text, 1);
        ui.TextR($"{men}/{max}", r.Right - U(8), y, men < max ? Style.Warn : Style.Ok, 1);
        y += U(13);
        ui.Text("STRENGTH", x, y, Style.Dim * 0.8f, 1); y += U(10);
        ui.Pips(new Rectangle(x, y, w, U(7)), men / (float)max, Side(2).Ui, 10); y += U(11);
        ui.Text("MORALE", x, y, Style.Dim * 0.8f, 1); y += U(10);
        ui.Gauge(new Rectangle(x, y, w, U(6)), morale, morale > 0.6f ? Style.Ok : morale > 0.35f ? Style.Warn : Style.Bad); y += U(10);
        ui.Gauge(new Rectangle(x, y, w, U(3)), xp, Kit.Gilt); y += U(8);
        if (y + U(13) <= r.Bottom - U(24))
        {
            int cx = x;
            if (xp >= 1) cx += ui.Chip(cx, y, "TRAIN", Kit.Gilt, "up") + U(3);
            if (wounded > 0) ui.Chip(cx, y, $"{wounded} HURT", Style.Bad, "heart");
        }
        // The troop's numbers along the foot of the card.
        var foot = new Rectangle(r.X + U(1), r.Bottom - U(18), r.Width - U(2), U(17));
        ui.Rect(foot, Color.Black * 0.35f);
        ui.Rect(new Rectangle(foot.X, foot.Y, foot.Width, U(1)), Color.White * 0.06f);
        var stats = new[] { ("sword", $"{t.Damage:0}"), ("shield", $"{t.Armor:0}"), ("boot", $"{t.Speed:0.0}"), ("coin", $"{t.Cost}") };
        for (int k = 0; k < stats.Length; k++)
        {
            int sx = foot.X + U(6) + k * (foot.Width - U(6)) / stats.Length;
            ui.Icon(stats[k].Item1, sx, foot.Y + U(5), Style.Dim, 1);
            ui.Text(stats[k].Item2, sx + U(11), foot.Y + U(5), Style.Text, 1);
        }
        ui.Block(r);
        return ui.Clicked(r);
    }

    // ---------- 4. after action ----------

    void ReportPage(Rectangle a)
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(34, 30, 26), Kit.IronDark);
        ui.Ribbon(a.Center.X, a.Y + U(4), "VICTORY", new Color(150, 116, 40), 3);
        ui.TextC("THE FIELD OF LOWMOOR . DAY 41, MIDDAY . 6 MIN 12 S", a.Center.X, a.Y + U(42), Style.Dim, 1);
        int top = a.Y + U(66), gap = U(14), lw = (int)(a.Width * 0.6f), rw = a.Width - lw - gap;
        int h1 = (int)((a.Bottom - top - gap) * 0.56f), h2 = a.Bottom - top - gap - h1;

        // The tally: your side against theirs, row by row.
        var c = ui.Plate(new Rectangle(a.X, top, lw, h1), "THE TALLY", Kit.Gilt);
        int sh = U(46);
        ui.Shield(new Rectangle(c.X, c.Y, (int)(sh / 1.2f), sh), Side(2));
        ui.Shield(new Rectangle(c.Right - (int)(sh / 1.2f), c.Y, (int)(sh / 1.2f), sh), Side(1));
        ui.Text("YOUR COMPANY", c.X + U(46), c.Y + U(8), Kit.Gilt, 2);
        ui.TextR("CRIMSON HOST", c.Right - U(46), c.Y + U(8), Style.Foe, 2);
        ui.Text("187 MEN", c.X + U(46), c.Y + U(26), Style.Dim, 1);
        ui.TextR("240 MEN . LORD OSRIC", c.Right - U(46), c.Y + U(26), Style.Dim, 1);
        int y = c.Y + sh + U(12), mid = c.Center.X, half = c.Width / 2 - U(52);
        var tally = new[] { ("ENGAGED", 187, 240, 270), ("FELL", 14, 61, 80), ("WOUNDED", 22, 38, 80), ("ROUTED", 3, 96, 120), ("TAKEN", 0, 18, 80) };
        bool hero = c.Bottom - y > U(170);
        int rowH = Math.Clamp((c.Bottom - y - (hero ? U(56) : 0)) / tally.Length, U(16), U(28)), bh = Math.Min(U(14), rowH - U(6));
        foreach (var (label, l, r, max) in tally)
        {
            if (y + bh > c.Bottom) break;
            int lw2 = (int)(half * l / (float)max), rw2 = (int)(half * r / (float)max), ty2 = y + (bh - U(7)) / 2;
            ui.Rect(new Rectangle(mid - U(40) - half, y, half, bh), Color.Black * 0.4f);
            ui.Rect(new Rectangle(mid + U(40), y, half, bh), Color.Black * 0.4f);
            ui.HGrad(new Rectangle(mid - U(40) - lw2, y, lw2, bh), Kit.GiltDark, Kit.Gilt);
            ui.HGrad(new Rectangle(mid + U(40), y, rw2, bh), Style.Foe, Kit.Mul(Style.Foe, 0.5f));
            ui.Rect(new Rectangle(mid - U(40) - lw2, y, lw2, U(1)), Color.White * 0.3f);
            ui.Rect(new Rectangle(mid + U(40), y, rw2, U(1)), Color.White * 0.3f);
            ui.TextC(label, mid, ty2, Style.Text, 1);
            ui.TextR(l.ToString(), mid - U(46) - lw2, ty2, Kit.Gilt, 1);
            ui.Text(r.ToString(), mid + U(46) + rw2, ty2, Style.Foe, 1);
            y += rowH;
        }
        if (hero)
        {
            // The company of the day.
            var best = new Rectangle(c.X, c.Bottom - U(48), c.Width, U(48));
            ui.HGrad(best, Kit.Mul(Kit.Gilt, 0.3f), Kit.Gilt * 0f);
            ui.Rect(new Rectangle(best.X, best.Y, U(2), best.Height), Kit.Gilt);
            var port = new Rectangle(best.X + U(8), best.Y + U(4), U(40), U(40));
            ui.VGrad(port, Kit.Mul(Side(2).Cloth, 0.55f), Kit.IronDark, 2);
            ui.Portrait(T("lancers"), port);
            ui.Text("COMPANY OF THE DAY", port.Right + U(10), best.Y + U(8), Kit.Gilt, 1);
            ui.Text("6. LANCERS", port.Right + U(10), best.Y + U(20), Style.Text, 2);
            ui.TextR("41 SLAIN . 0 LOST", best.Right - U(6), best.Y + U(22), Style.Dim, 1);
        }

        // Spoils.
        c = ui.Plate(new Rectangle(a.X + lw + gap, top, rw, h1), "SPOILS", Style.Ok);
        y = c.Y;
        int sRow = Math.Clamp(c.Height / 6, U(16), U(30)), sIcon = sRow >= U(26) ? 2 : 1;
        foreach (var (icon, label, value, col) in new[]
                 {
                     ("coin", "LOOT AND RANSOM", "+412", Kit.Gilt), ("chain", "PRISONERS", "18", Style.Text), ("horse", "HORSES TAKEN", "6", Style.Text),
                     ("star", "RENOWN", "+24", Kit.Gilt), ("up", "DRILL", "+310 XP", Style.Ok), ("bread", "FOOD", "+3 DAYS", Style.Text),
                 })
        {
            if (y + sRow - U(2) > c.Bottom) break;
            var row = new Rectangle(c.X - U(4), y, c.Width + U(8), sRow - U(2));
            ui.Rect(row, Color.White * 0.035f);
            int iy = row.Y + (row.Height - U(7 * sIcon)) / 2, ty2 = row.Y + (row.Height - U(7 * sIcon)) / 2;
            ui.Icon(icon, row.X + U(6), iy, col, sIcon);
            ui.Text(label, row.X + U(12 + 7 * sIcon), row.Y + (row.Height - U(7)) / 2, Style.Dim, 1);
            ui.TextR(value, row.Right - U(6), ty2, col, sIcon);
            y += sRow;
        }

        // The battle over time.
        c = ui.Plate(new Rectangle(a.X, top + h1 + gap, lw, h2), "THE BATTLE", Style.Friendly);
        var chart = new Rectangle(c.X + U(4), c.Y + U(4), c.Width - U(8), c.Height - U(20));
        for (int i = 0; i <= 4; i++) ui.Rect(new Rectangle(chart.X, chart.Y + i * chart.Height / 4, chart.Width, U(1)), Color.White * 0.05f);
        ui.Chart(chart, new[] { 0.78f, 0.77f, 0.74f, 0.7f, 0.68f, 0.66f, 0.65f, 0.65f, 0.64f }, Kit.Gilt);
        ui.Chart(chart, new[] { 1f, 0.97f, 0.9f, 0.86f, 0.62f, 0.4f, 0.22f, 0.12f, 0.08f }, Style.Foe);
        int mark = chart.X + chart.Width * 4 / 8;
        for (int yy = chart.Y; yy < chart.Bottom; yy += U(6)) ui.Rect(new Rectangle(mark, yy, U(1), U(3)), Style.Text * 0.6f);
        ui.Text("THE LANCERS CHARGE", mark + U(6), chart.Y + U(2), Style.Text, 1);
        for (int i = 0; i <= 6; i++) ui.TextC($"{i}:00", chart.X + chart.Width * i / 6f, chart.Bottom + U(6), Style.Dim * 0.8f, 1);

        // Deeds and the way out.
        c = ui.Plate(new Rectangle(a.X + lw + gap, top + h1 + gap, rw, h2), "DEEDS", Kit.Gilt);
        y = c.Y;
        var deeds = new[] { "THE LANCERS BROKE THE CRIMSON LEFT.", "THE PIKES HELD THE FORD THREE TIMES.", "YOU UNHORSED LORD OSRIC." };
        int ds = deeds.Sum(d => ui.Wrap(d, c.Width - U(12), 2).Count) * U(18) + deeds.Length * U(6) < c.Height - U(34) ? 2 : 1;
        foreach (var line in deeds)
        {
            bool first = true;
            foreach (var l in ui.Wrap(line, c.Width - U(12), ds))
            {
                if (y + U(7 * ds) > c.Bottom - U(34)) break;
                if (first) ui.Rect(new Rectangle(c.X, y + U(2 * ds), U(3), U(3)), Kit.Gilt);
                ui.Text(l, c.X + U(12), y, first ? Style.Text : Style.Text * 0.9f, ds);
                y += U(ds == 2 ? 18 : 13);
                first = false;
            }
            y += U(ds == 2 ? 6 : 2);
        }
        ui.Gilded(new Rectangle(c.X, c.Bottom - U(26), c.Width, U(26)), "RETURN TO THE MAP", "SPACE", tone: Style.Ok);
    }

    // ---------- 5. market ----------

    void MarketPage(Rectangle a)
    {
        ui.VGrad(new Rectangle(0, 0, ui.W, ui.H), new Color(38, 32, 28), Kit.IronDark);
        ui.Shield(new Rectangle(a.X, a.Y + U(4), U(40), U(48)), Side(0));
        ui.Text("UFFING", a.X + U(50), a.Y + U(6), Style.Text, 3);
        int cx = a.X + U(50);
        cx += ui.Chip(cx, a.Y + U(34), "TOWN", Style.Dim, "tower") + U(4);
        cx += ui.Chip(cx, a.Y + U(34), "AZURE CROWN", Style.Friendly) + U(4);
        ui.Chip(cx, a.Y + U(34), "PROSPEROUS", Style.Ok);
        float rr = U(18);
        ui.Ring(new Vector2(a.Right - U(140), a.Y + U(26)), rr, 0.72f, Style.Ok, "72");
        ui.TextC("PROSPERITY", a.Right - U(140), a.Y + U(50), Style.Dim, 1);
        ui.Ring(new Vector2(a.Right - U(50), a.Y + U(26)), rr, 0.18f, Style.Warn, "18");
        ui.TextC("UNREST", a.Right - U(50), a.Y + U(50), Style.Dim, 1);

        int top = a.Y + U(76), gap = U(14), bottomBar = U(46), lw = (int)(a.Width * 0.55f), rw = a.Width - lw - gap, h = a.Bottom - top - bottomBar - gap;

        // Recruits with portraits and steppers.
        var c = ui.Plate(new Rectangle(a.X, top, lw, h), "RECRUITS", Style.Friendly);
        var recruits = new[] { ("shieldmen", 28, 14), ("spearrunners", 22, 20), ("crossbowmen", 46, 9), ("lancers", 90, 4) };
        int y = c.Y, rowH = Math.Clamp(c.Height / 4, U(46), U(96));
        for (int i = 0; i < recruits.Length && y + rowH - U(6) <= c.Bottom; i++)
        {
            var (id, cost, avail) = recruits[i];
            var t = T(id);
            var row = new Rectangle(c.X, y, c.Width, rowH - U(6));
            ui.Rect(row, Color.White * (ui.Hover(row) ? 0.07f : 0.03f));
            var port = new Rectangle(row.X + U(4), row.Y + U(3), row.Height - U(6), row.Height - U(6));
            ui.VGrad(port, Kit.Mul(Side(0).Cloth, 0.5f), Kit.IronDark, 2);
            ui.Portrait(t, port, Math.Max(1, port.Height / U(36)) * ui.S);
            ui.Frame(port, Color.Black * 0.6f, U(1));
            int tx = port.Right + U(10);
            int ty = row.Y + (row.Height - U(40)) / 2;
            ui.Text(t.Name, tx, ty, Style.Text, 2);
            ui.Text($"TIER {Style.Numerals[t.Tier]} . {avail} WILLING", tx, ty + U(18), Style.Dim, 1);
            ui.Icon("coin", tx, ty + U(30), Kit.Gilt, 1);
            ui.Text($"{cost} A MAN", tx + U(12), ty + U(30), Kit.Gilt, 1);
            int sx = tx + U(90);
            foreach (var (icon, v) in new[] { ("sword", $"{t.Damage:0}"), ("shield", $"{t.Armor:0}"), ("boot", $"{t.Speed:0.0}") })
            {
                if (sx + U(40) > row.Right - U(104)) break;
                ui.Icon(icon, sx, ty + U(30), Style.Dim, 1); ui.Text(v, sx + U(11), ty + U(30), Style.Text, 1); sx += U(40);
            }
            hire[i] = Math.Clamp(hire[i] + 2 * ui.Stepper(new Rectangle(row.Right - U(96), row.Center.Y - U(10), U(90), U(20)), hire[i]), 0, avail);
            y += rowH;
        }

        // Supplies.
        c = ui.Plate(new Rectangle(a.X + lw + gap, top, rw, h), "SUPPLIES", Kit.Gilt);
        y = c.Y;
        int rh = Math.Clamp(c.Height / 5, U(28), U(60)), big = rh >= U(44) ? 3 : 2;
        foreach (var (icon, name, note, price) in new[]
                 {
                     ("bread", "GRAIN AND SALT PORK", "+10 DAYS", 60), ("horse", "COURSER", "4 IN THE STABLES", 120), ("shield", "MAIL AND HELMS", "ARMOUR FOR 10 MEN", 240),
                     ("arrow", "SHEAVES OF ARROWS", "REFILLS BOWMEN", 35), ("fire", "PITCH AND LADDERS", "FOR A SIEGE", 90),
                 })
        {
            if (y + rh - U(4) > c.Bottom) break;
            var row = new Rectangle(c.X, y, c.Width, rh - U(4));
            ui.Rect(row, Color.White * (ui.Hover(row) ? 0.07f : 0.03f));
            int ix = row.X + U(8), iw = U(7 * big), tx = ix + iw + U(10), my = row.Center.Y;
            ui.Icon(icon, ix, my - iw / 2, Style.Text, big);
            ui.Text(name, tx, my - U(10), Style.Text, 1);
            ui.Text(note, tx, my + U(2), Style.Dim, 1);
            if (ui.Button(new Rectangle(row.Right - U(66), my - U(11), U(60), U(22)), "", note: "")) { }
            ui.Icon("coin", row.Right - U(58), my - U(4), Kit.Gilt, 1);
            ui.TextR(price.ToString(), row.Right - U(12), my - U(3), Style.Text, 1);
            y += rh;
        }

        // The purse.
        int cost0 = hire[0] * 28 + hire[1] * 22 + hire[2] * 46;
        var bar = ui.Plate(new Rectangle(a.X, a.Bottom - bottomBar, a.Width, bottomBar), solid: true);
        int by = bar.Y - U(6);
        ui.Icon("coin", bar.X, by + U(2), Kit.Gilt, 2);
        ui.Text("1,240", bar.X + U(20), by + U(2), Kit.Gilt, 2);
        ui.Text($"-{Kit.N(cost0)}", bar.X + U(100), by + U(2), Style.Bad, 2);
        ui.Text($"= {Kit.N(1240 - cost0)} LEFT", bar.X + U(170), by + U(6), Style.Dim, 1);
        ui.Gilded(new Rectangle(bar.Right - U(150), by - U(3), U(150), U(24)), "CONFIRM", "ENTER", cost0 > 0, Style.Ok);
        ui.Button(new Rectangle(bar.Right - U(250), by - U(3), U(90), U(24)), "LEAVE", note: "ESC");
    }
}
