using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the frame of the settlement screen (docs/PLAYTEST.md PT2): the place painted across the top at the hour it
/// is, its tabs as a rail of pictured tiles with counts, and the small pieces the tabs are built from (cards, stats,
/// outcomes, job cards). Drawing only.
partial class MapScreen
{
    /// How each tab shows on the rail: its picture and its word.
    static readonly Dictionary<string, (string Icon, string Label)> TabLook = new()
    {
        ["TRAIN"] = ("up", "TRAIN"), ["RECRUIT"] = ("helm", "RECRUIT"), ["MERCENARIES"] = ("coin", "FOR HIRE"), ["ESQUIRES"] = ("horse", "ESQUIRES"),
        ["TAVERN"] = ("mug", "TAVERN"), ["HALL"] = ("crown", "HALL"), ["MARKET"] = ("scales", "MARKET"), ["ARMOURER"] = ("anvil", "ARMOURER"),
        ["WORKSHOP"] = ("hammer", "WORKSHOP"), ["FOOD"] = ("bread", "FOOD"), ["REST"] = ("bed", "REST"), ["FORAGE"] = ("wheat", "FORAGE"),
        ["CAPTIVES"] = ("chain", "CAPTIVES"), ["CONTRACT"] = ("scroll", "CONTRACT"), ["ELDERS"] = ("house", "ELDERS"), ["CHAPTER"] = ("cross", "CHAPTER"),
        ["MUSTER"] = ("flag", "MUSTER"), ["FIEF"] = ("flag", "FIEF"), ["GARRISON"] = ("tower", "GARRISON"), ["HARBOUR"] = ("anchor", "HARBOUR"),
    };

    /// The number a tab's tile carries (0 = none): what waits for you there.
    int TabCount(string t, Settlement s) => t switch
    {
        "TRAIN" => C.Player.Companies.Sum(c => c.Stacks.Where(st => st.Type.UpgradesTo.Any(t => C.CanTrainHere(C.Player, t))).Sum(st => st.Ready)),
        "MUSTER" => C.CannotMuster(s) == null ? C.MusterRecruits(s) : 0,
        "RECRUIT" => C.RecruitsForYou(s),
        "MERCENARIES" => s.Mercenaries.Count,
        "ESQUIRES" => C.CannotRecruitNobles(s) == null ? s.Nobles : 0,
        "REST" => C.Player.Wounded,
        "CAPTIVES" => C.PrisonerCount + C.Player.Captives.Count,
        "GARRISON" => s.Garrison.Sum(g => g.Men),
        _ => 0,
    };

    /// The rail: one tile a tab, its picture over its word, the open one lit in gilt. Returns the y under it.
    int PlaceRail(Rectangle r, string[] tabs, Settlement s)
    {
        int tw = Math.Min(ui.U(84), r.Width / tabs.Length), x0 = r.X + (r.Width - tw * tabs.Length) / 2;
        for (int i = 0; i < tabs.Length; i++)
        {
            var (icon, label) = TabLook.GetValueOrDefault(tabs[i], ("scroll", tabs[i]));
            var t = new Rectangle(x0 + i * tw, r.Y, tw - ui.U(2), r.Height);
            bool on = tabs[i] == tab, hover = ui.Hover(t);
            ui.VGrad(t, on ? Kit.Mul(Kit.Gilt, 0.34f) : hover ? new Color(48, 50, 58) : new Color(30, 33, 42), on ? Kit.Mul(Kit.Gilt, 0.1f) : Kit.IronDark, 1);
            ui.Frame(t, on ? Kit.Gilt : Color.White * (hover ? 0.2f : 0.07f), ui.U(1));
            if (on) ui.Rect(new Rectangle(t.X, t.Bottom - ui.U(2), t.Width, ui.U(2)), Kit.Gilt);
            var col = on ? Kit.Gilt : hover ? Style.Text : Kit.Mul(Kit.Parchment, 0.78f);
            ui.Icon(icon, t.Center.X - ui.U(7), t.Y + ui.U(5), col, 2);
            ui.TextFitC(label, t.Center.X, t.Y + ui.U(24), t.Width - ui.U(2), on ? Kit.Gilt : hover ? Style.Text : Style.Dim, 1);
            int n = TabCount(tabs[i], s);
            if (n > 0)
            {
                string num = n > 999 ? "999" : n.ToString();
                var b = new Rectangle(t.Right - ui.TW(num, 1) - ui.U(5), t.Y + ui.U(2), ui.TW(num, 1) + ui.U(3), ui.U(10));
                ui.Rect(b, tabs[i] == "REST" ? Kit.Mul(Style.Warn, 0.6f) : Kit.Mul(Kit.Gilt, 0.5f));
                ui.Text(num, b.X + ui.U(2), b.Y + ui.U(2), Color.White, 1);
            }
            if (hover) Ui.Hovered(t);
            if (!on && ui.Clicked(t)) { tab = tabs[i]; note = ""; }
        }
        return r.Bottom;
    }

    /// The place itself across the top of its screen: a town with its tavern and walls, a castle on its mound, a village's
    /// few roofs, each as its people build and at the hour it is; then its arms, name and standing laid over the picture.
    void PlaceHero(Settlement s, Rectangle p)
    {
        float hour = (float)(C.S.Hours % 24);
        // The picture fills the right of the strip; the left is the plate the writing sits on.
        var pic = new Rectangle(p.X + p.Width * 2 / 5 - ui.U(10), p.Y, p.Width - p.Width * 2 / 5 + ui.U(10), p.Height);
        if (s.Kind == SettlementKind.Town) Townscape(pic, s, hour);
        else PlaceScene(pic, s, hour);
        // A dark plate on the left for the writing, fading into the picture.
        int pw = p.Width * 2 / 5;
        ui.Rect(new Rectangle(p.X, p.Y, pw, p.Height), Kit.IronDark * 0.9f);
        ui.HGrad(new Rectangle(p.X + pw, p.Y, ui.U(48), p.Height), Kit.IronDark * 0.9f, Kit.IronDark * 0f, 2);
        ui.VGrad(new Rectangle(p.X + pw, p.Bottom - ui.U(24), p.Width - pw, ui.U(24)), Kit.IronDark * 0f, Kit.IronDark * 0.6f, 2);
        int x = p.X + ui.U(14), y = p.Y + ui.U(14);
        ui.Shield(new Rectangle(x, y, ui.U(54), ui.U(65)), Style.Sides[Style.PaletteOf(s.Faction)]);
        int tx = x + ui.U(68), tw = p.X + pw - tx;
        string name = s.Name.ToUpperInvariant();
        int ns = ui.TW(name, 4) <= tw + ui.U(30) ? 4 : 3;
        ui.Text(name, tx, y - ui.U(2), Style.Text, ns);
        int cx = tx, cy = y + ui.U(ns == 4 ? 32 : 26);
        void Tag(string text, Color col, string? icon = null)
        {
            int w = ui.TW(text, 1) + ui.U(icon == null ? 8 : 18);
            if (cx > tx && cx + w > tx + tw + ui.U(30)) { cx = tx; cy += ui.U(15); }
            cx += ui.Chip(cx, cy, text, col, icon) + ui.U(4);
        }
        Tag(s.KindWord, Style.Text, s.Kind == SettlementKind.Village ? "house" : "tower");
        Tag(C.FactionNames[s.Faction].ToUpperInvariant(), SideColor(s.Faction), "flag");
        if (C.IsPort(s)) Tag("PORT", Style.Text, "anchor");
        if (s.Owner == C.Player.Id) Tag("YOUR FIEF", Kit.Gilt, "crown");
        if (C.Refuses(s)) Tag("HOSTILE", Style.Bad);
        if (C.HolderOf(s) is Character holder)
        {
            float o = C.OpinionOfYou(holder);
            Tag(ui.Fit($"{holder.Name.ToUpperInvariant()}: {OpinionWord(o)}", tw), OpinionColor(o), "heart");
        }
        int fy = Math.Max(cy + ui.U(18), y + ui.U(72));
        foreach (var line in ui.Wrap(C.Flavour(s).ToUpperInvariant(), pw - ui.U(24)).Take(Math.Max(1, (p.Bottom - ui.U(6) - fy) / ui.U(11))))
        { ui.Text(line, x, fy, Kit.Mul(Kit.Parchment, 0.9f), 1); fy += ui.U(11); }
        // Prosperity and unrest on a dark plate at the right.
        var plate = new Rectangle(p.Right - ui.U(168), p.Y + ui.U(10), ui.U(158), ui.U(62));
        ui.Rect(plate, Kit.IronDark * 0.78f);
        ui.Frame(plate, Color.White * 0.1f, ui.U(1));
        bool revolt = s.Unrest >= Campaign.RevoltAt;
        ui.Ring(new Vector2(plate.X + ui.U(40), plate.Y + ui.U(24)), ui.U(17), s.Prosperity / 100f, Style.Ok, $"{s.Prosperity:0}");
        ui.TextC(s.Prosperity >= 70 ? "PROSPEROUS" : s.Prosperity >= 40 ? "PROSPERITY" : "POOR", plate.X + ui.U(40), plate.Y + ui.U(48), Style.Dim, 1);
        ui.Ring(new Vector2(plate.Right - ui.U(40), plate.Y + ui.U(24)), ui.U(17), s.Unrest / 100f, revolt ? Style.Bad : Style.Warn, $"{s.Unrest:0}");
        ui.TextC(revolt ? "REVOLT" : "UNREST", plate.Right - ui.U(40), plate.Y + ui.U(48), revolt ? Style.Bad : Style.Dim, 1);
        // A gilt frame struck like metal: light on the top and left, shade on the bottom and right.
        ui.Frame(p, Kit.GiltDark, ui.U(2));
        ui.Rect(new Rectangle(p.X, p.Y, p.Width, ui.U(1)), Kit.Gilt);
        ui.Rect(new Rectangle(p.X, p.Y, ui.U(1), p.Height), Kit.Gilt);
        ui.Rect(new Rectangle(p.X, p.Bottom - ui.U(1), p.Width, ui.U(1)), Kit.Mul(Kit.GiltDark, 0.6f));
        ui.Rect(new Rectangle(p.Right - ui.U(1), p.Y, ui.U(1), p.Height), Kit.Mul(Kit.GiltDark, 0.6f));
    }

    /// A castle or a village painted from the townscape's own pieces: the same sky, country, ground and houses; a castle
    /// adds its keep on a mound under the realm's flag, a village only its roofs, folk and herds.
    void PlaceScene(Rectangle p, Settlement s, float hour)
    {
        float t = Environment.TickCount64 / 1000f;
        var l = LookOf(s, hour);
        var c = ColoursOf(l);
        var rng = new Random(s.Id * 7919 + 13);
        var cloth = Kit.Mul(Style.Sides[Style.PaletteOf(s.Faction)].Cloth, l.Night ? 0.5f : 0.9f);
        int ground = p.Bottom - ui.U(10);
        ui.VGrad(p, c.SkyTop, c.SkyLow, 2);
        if (l.Night) ui.Stars(new Rectangle(p.X, p.Y, p.Width, p.Height / 2), t, 50); else Clouds(p, l, c, t, s.Id);
        Orb(p, l, 0.42f + (float)new Random(s.Id * 31 + 7).NextDouble() * 0.1f);
        snowCap = l.Snow ? c.Snow : Color.Transparent;
        Country(p, l, c, ground, rng);
        bool castle = s.Kind == SettlementKind.Castle;
        int keepX = p.X + p.Width * 7 / 10;
        if (castle)
        {
            // The mound, then the keep on it, its windows lit after dusk, the realm's flag at the top.
            for (int k = 0; k < ui.U(14); k += Math.Max(1, ui.U(1)))
                ui.Rect(new Rectangle(keepX - ui.U(70) - k * 3, ground - ui.U(14) + k, ui.U(140) + k * 6, Math.Max(1, ui.U(1))), Color.Lerp(c.Mid, c.Ground, k / (float)ui.U(14)));
            ui.Castle(keepX, ground - ui.U(13), 3, Color.Lerp(c.Mid, c.Wall, 0.35f), l.Day ? null : c.Lit);
            ui.Rect(new Rectangle(keepX + ui.U(3), ground - ui.U(13) - ui.U(78), ui.U(12), ui.U(6)), cloth);
            ui.Rect(new Rectangle(keepX + ui.U(3), ground - ui.U(13) - ui.U(75), ui.U(12), ui.U(1)), Kit.Mul(cloth, 0.7f));
        }
        else
        {
            var back = c with { Wall = Color.Lerp(c.Wall, c.Mid, 0.6f), Roof = Color.Lerp(c.Roof, c.Mid, 0.6f) };
            HouseRow(p.X + p.Width / 2, p.Right - ui.U(30), ground - ui.U(6), l, back, new Random(s.Id * 13 + 5), t, false);
        }
        Ground(p, l, c, ground, t);
        rimLight = c.Rim;
        if (castle) HouseRow(p.X + p.Width * 2 / 5, keepX - ui.U(60), ground, l, c, rng, t, true);
        else HouseRow(p.X + p.Width * 2 / 5, p.Right - ui.U(8), ground, l, c, rng, t, true);
        rimLight = Color.Transparent;
        if (l.Build == "tent") Herd(p, ground, c, s.Id, p.X - ui.U(400));
        Folk(p, l, c, ground, s.Id, p.X - ui.U(400));
        if (l.Snow) Snowfall(p, t, l.Night);
        snowCap = Color.Transparent;
    }

    /// The purse along the bottom as pictures: crowns, men, days of food, horses.
    void Purse(Rectangle r)
    {
        int x = r.X + ui.U(24), y = r.Bottom - ui.U(34);
        ui.Icon("coin", x, y + ui.U(1), Kit.Gilt, 2);
        ui.Text($"{C.S.Gold:N0}", x + ui.U(20), y + ui.U(1), Kit.Gilt, 2);
        x += ui.U(34) + ui.TW($"{C.S.Gold:N0}", 2);
        foreach (var (icon, v, word, col) in new[]
        {
            ("helm", $"{C.Player.Men}", "MEN", Style.Text),
            ("bread", $"{C.Player.Food:0}", "DAYS", C.Player.Food < 3 ? Style.Bad : Style.Text),
            ("horse", $"{C.Player.Horses}", "HORSES", Style.Text),
        })
        {
            ui.Rect(new Rectangle(x - ui.U(8), y, ui.U(1), ui.U(16)), Color.White * 0.1f);
            ui.Icon(icon, x, y + ui.U(4), Kit.Mul(Kit.Parchment, 0.8f), 1);
            ui.Text(v, x + ui.U(12), y + ui.U(1), col, 2);
            ui.Text(word, x + ui.U(16) + ui.TW(v, 2), y + ui.U(5), Style.Dim, 1);
            x += ui.U(36) + ui.TW(v, 2) + ui.TW(word, 1);
        }
    }

    // ---------- the pieces the tabs are built from ----------

    /// A card: a faint plate that lights under the mouse, with a coloured edge on its left. Returns its inside.
    Rectangle Card(Rectangle r, Color? edge = null, bool lights = false)
    {
        Overflow.Box(r);
        ui.VGrad(r, Color.White * (lights && ui.Hover(r) ? 0.085f : 0.05f), Color.White * 0.02f, 2);
        ui.Frame(r, Color.White * 0.06f, ui.U(1));
        if (edge is Color e) ui.Rect(new Rectangle(r.X, r.Y, ui.U(2), r.Height), e);
        return Kit.Inset(r, ui.U(10));
    }

    /// A gilt heading with a rule running out from it. Returns the y under it.
    int Heading(int x, int y, int w, string title, string right = "")
    {
        ui.Text(title, x, y, Kit.Gilt, 1);
        int rw = right == "" ? 0 : ui.TW(right, 1) + ui.U(8);
        if (right != "") ui.TextR(right, x + w, y, Style.Dim, 1);
        ui.Divider(x + ui.TW(title, 1) + ui.U(8), y + ui.U(3), w - ui.TW(title, 1) - ui.U(8) - rw, Kit.Mul(Kit.Gilt, 0.5f));
        return y + ui.U(14);
    }

    /// A number worth reading at a glance: its picture, the figure large, a word under it.
    void Stat(int x, int y, string icon, string value, string caption, Color c)
    {
        ui.Icon(icon, x, y + ui.U(1), c, 2);
        ui.Text(value, x + ui.U(20), y + ui.U(1), c, 2);
        ui.Text(caption, x + ui.U(20), y + ui.U(18), Style.Dim, 1);
    }

    /// What a choice does, as one line: a small tile in green (a gain), amber (a cost) or grey with its picture, then the words.
    int Outcome(int x, int y, int w, string icon, string text, int good = 0)
    {
        var c = good > 0 ? Style.Ok : good < 0 ? Style.Warn : Kit.Mul(Kit.Parchment, 0.8f);
        var tile = new Rectangle(x, y, ui.U(14), ui.U(14));
        ui.Rect(tile, Kit.Mul(c, 0.28f));
        ui.Frame(tile, Kit.Mul(c, 0.7f), ui.U(1));
        ui.Icon(icon, x + ui.U(3), y + ui.U(3), c, 1);
        ui.TextFit(text, x + ui.U(20), y + ui.U(4), w - ui.U(20), good < 0 ? Style.Warn : Style.Text, 1);
        return y + ui.U(18);
    }

    /// A price: a coin and the figure, red when you cannot pay it.
    void Price(int x, int y, float crowns, string after = "")
    {
        var c = C.S.Gold >= crowns ? Kit.Gilt : Style.Bad;
        ui.Icon("coin", x, y, c, 1);
        ui.Text($"{crowns:0}", x + ui.U(11), y, c, 1);
        if (after != "") ui.Text(after, x + ui.U(15) + ui.TW($"{crowns:0}", 1), y, Style.Dim, 1);
    }

    /// The picture a job goes by.
    static string JobIcon(Contract o) => o.Kind switch
    {
        "defend" or "militia" => "shield",
        "escort" => "horse",
        "ransom" or "rescue" => "chain",
        "relic" => "cross",
        var k when k.Contains("hunt") || k.Contains("bandit") || k.Contains("kill") || k.Contains("clear") => "skull",
        var k when k.Contains("deliver") || k.Contains("supply") || k.Contains("food") => "bread",
        _ => "scroll",
    };

    /// A list of jobs as cards: the job's picture, what is asked in a line or two, its pay and its days, ACCEPT.
    void JobCards(Settlement s, Rectangle body, int y, List<Contract> jobs, string none)
    {
        if (jobs.Count == 0)
        {
            var e = Card(new Rectangle(body.X, y, body.Width, ui.U(44)));
            ui.Icon("clock", e.X, e.Y + ui.U(5), Style.Dim, 2);
            ui.TextFit(none, e.X + ui.U(24), e.Y + ui.U(9), e.Width - ui.U(24), Style.Dim, 1);
            return;
        }
        bool full = C.S.Contracts.Count >= 3;
        foreach (var o in jobs)
        {
            int rh = ui.U(50);
            if (y + rh > body.Bottom) break;
            var row = new Rectangle(body.X, y, body.Width, rh - ui.U(6));
            Card(row, Kit.GiltDark, true);
            var tile = new Rectangle(row.X + ui.U(8), row.Y + ui.U(6), ui.U(32), ui.U(32));
            ui.Rect(tile, Kit.Mul(Kit.Gilt, 0.14f));
            ui.Frame(tile, Kit.GiltDark, ui.U(1));
            ui.Icon(JobIcon(o), tile.X + ui.U(5), tile.Y + ui.U(5), Kit.Gilt, 3);
            int tx = tile.Right + ui.U(10), tw = row.Right - ui.U(196) - tx, ly = row.Y + ui.U(6);
            foreach (var line in ui.Wrap(o.Text.ToUpperInvariant(), tw).Take(2)) { ui.Text(line, tx, ly, Style.Text, 1); ly += ui.U(11); }
            float pay = o.Reward + (o.Then?.Reward ?? 0), days = (o.Deadline - (float)C.S.Hours) / 24;
            int fx = tx;
            if (pay > 0) { Price(fx, row.Bottom - ui.U(12), pay); fx += ui.U(22) + ui.TW($"{pay:0}", 1); }
            if (days > 0 && days < 400) { ui.Icon("clock", fx, row.Bottom - ui.U(12), Style.Dim, 1); ui.Text($"{days:0} DAYS", fx + ui.U(11), row.Bottom - ui.U(12), Style.Dim, 1); fx += ui.U(22) + ui.TW($"{days:0} DAYS", 1); }
            if (o.Then != null) ui.Text("MORE TO DO AFTER", fx, row.Bottom - ui.U(12), Style.Dim, 1);
            if (ui.Button(new Rectangle(row.Right - ui.U(180), row.Center.Y - ui.U(13), ui.U(172), ui.U(26)), full ? "THREE JOBS HELD" : "ACCEPT", !full, full ? "" : $"{pay:0}")) note = C.Accept(s, o) ?? "ACCEPTED";
            y += rh;
        }
    }

    /// The open tab's picture, huge and faint, in the corner of a tab that has room to spare.
    void Watermark(Rectangle well)
    {
        if (!TabLook.TryGetValue(tab, out var look)) return;
        int k = ui.U(22);
        ui.IconFlat(look.Icon, well.Right - 7 * k - ui.U(16), well.Bottom - 7 * k - ui.U(12), Color.White * 0.028f, k);
    }

    /// A big action: a tall button with its picture on the left.
    bool BigButton(Rectangle r, string icon, string label, bool enabled = true, string side = "", bool active = false)
    {
        bool hit = ui.Button(r, label, enabled, side, active);
        ui.Icon(icon, r.X + ui.U(10), r.Center.Y - ui.U(10), enabled ? Kit.Gilt : Style.Dim, 3);
        return hit;
    }
}
