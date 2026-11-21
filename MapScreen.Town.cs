using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the frame of the settlement screen (docs/pre-demo/PLAYTEST.md PT2): the place painted across the top at the hour it
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
