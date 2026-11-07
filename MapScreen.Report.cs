using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the after-action report for a campaign battle: both sides' tally, the spoils, every company's part, and what
/// the battle did for your career.
partial class MapScreen
{
    void ResultsPanel()
    {
        var rep = C.Report;
        if (rep == null) { panel = PanelKind.None; return; }
        var r = Modal(1000, 600, rep.Won ? "VICTORY" : "DEFEAT", rep.Won ? Kit.Gilt : Style.Bad);
        ui.TextC($"AGAINST {rep.Enemy.ToUpperInvariant()} . DAY {C.Day}", r.Center.X, r.Y + ui.U(22), Style.Dim, 1);
        int pad = ui.U(20), gap = ui.U(12), top = r.Y + ui.U(40), lw = (int)((r.Width - 2 * pad - gap) * 0.6f), rw = r.Width - 2 * pad - gap - lw;
        int h1 = ui.U(190), h2 = r.Bottom - ui.U(52) - top - h1 - gap;
        var tally = new Rectangle(r.X + pad, top, lw, h1);
        Tally(rep, tally);
        Spoils(rep, new Rectangle(tally.Right + gap, top, rw, h1));
        ReportCompanies(rep, new Rectangle(r.X + pad, top + h1 + gap, lw, h2));
        Progress(rep, new Rectangle(tally.Right + gap, top + h1 + gap, rw, h2));
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(40), ui.U(220), ui.U(30)), "CONTINUE", active: true))
        { panel = C.S.GameOver ? PanelKind.GameOver : PanelKind.None; C.Report = null; }
    }

    /// A sunken box inside a modal, with a small heading.
    Rectangle Well(Rectangle r, string title, Color c)
    {
        ui.Rect(r, Color.Black * 0.22f);
        ui.Frame(r, Color.White * 0.06f, ui.U(1));
        ui.Rect(new Rectangle(r.X, r.Y, ui.U(2), ui.U(14)), c);
        ui.Text(title, r.X + ui.U(8), r.Y + ui.U(4), c, 1);
        return new Rectangle(r.X + ui.U(8), r.Y + ui.U(18), r.Width - ui.U(16), r.Height - ui.U(24));
    }

    /// Your side against theirs, row by row, and the company of the day.
    void Tally(BattleReport rep, Rectangle box)
    {
        var c = Well(box, "THE TALLY", Kit.Gilt);
        var foe = C.PartyById(C.S.LastBattleFoe);
        var foeSide = Style.Sides[rep.Won && foe != null ? Style.PaletteOf(C.SideOf(foe)) : 1];
        int sh = ui.U(34), sw = sh * 5 / 6;
        ui.Shield(new Rectangle(c.X, c.Y, sw, sh), Style.Sides[2]);
        ui.Shield(new Rectangle(c.Right - sw, c.Y, sw, sh), foeSide);
        ui.Text("YOUR COMPANY", c.X + sw + ui.U(8), c.Y + ui.U(4), Kit.Gilt, 2);
        string en = rep.Enemy.ToUpperInvariant();
        if (en.Length > 20) en = en[..20];
        ui.TextR(en, c.Right - sw - ui.U(8), c.Y + ui.U(4), foeSide.Ui, 2);
        var mine = rep.Companies.Where(x => x.Team == 0).ToList();
        var theirs = rep.Companies.Where(x => x.Team == 1).ToList();
        ui.Text($"{mine.Sum(x => x.MenBefore)} MEN", c.X + sw + ui.U(8), c.Y + ui.U(22), Style.Dim, 1);
        ui.TextR($"{theirs.Sum(x => x.MenBefore)} MEN", c.Right - sw - ui.U(8), c.Y + ui.U(22), Style.Dim, 1);
        var rows = new (string Label, int L, int R)[]
        {
            ("ENGAGED", mine.Sum(x => x.MenBefore), theirs.Sum(x => x.MenBefore)), ("FELL", mine.Sum(x => x.Killed), theirs.Sum(x => x.Killed)),
            ("WOUNDED", mine.Sum(x => x.Wounded), theirs.Sum(x => x.Wounded)), ("TAKEN", mine.Sum(x => x.Captured), theirs.Sum(x => x.Captured)),
            ("FLED", mine.Sum(x => x.FledLost), theirs.Sum(x => x.FledLost)),
        };
        int y = c.Y + sh + ui.U(8), mid = c.Center.X, half = c.Width / 2 - ui.U(44), bh = ui.U(9);
        foreach (var (label, l, rr) in rows)
        {
            int max = Math.Max(1, Math.Max(l, rr));
            if (label == "ENGAGED") max = Math.Max(1, (int)(max * 1.1f));
            int lw2 = (int)(half * l / (float)max), rw2 = (int)(half * rr / (float)max);
            ui.Rect(new Rectangle(mid - ui.U(36) - half, y, half, bh), Color.Black * 0.4f);
            ui.Rect(new Rectangle(mid + ui.U(36), y, half, bh), Color.Black * 0.4f);
            if (lw2 > 0) ui.HGrad(new Rectangle(mid - ui.U(36) - lw2, y, lw2, bh), Kit.GiltDark, Kit.Gilt);
            if (rw2 > 0) ui.HGrad(new Rectangle(mid + ui.U(36), y, rw2, bh), foeSide.Ui, Kit.Mul(foeSide.Ui, 0.5f));
            ui.TextC(label, mid, y + ui.U(1), Style.Text, 1);
            ui.TextR($"{l}", mid - ui.U(40) - lw2, y + ui.U(1), Kit.Gilt, 1);
            ui.Text($"{rr}", mid + ui.U(40) + rw2, y + ui.U(1), foeSide.Ui, 1);
            y += ui.U(14);
        }
        // The company of the day: most kills this battle, from its ledger.
        var best = mine.Select(x => (x, rec: C.Player.Companies.FirstOrDefault(k => k.Id == x.Id)))
            .Where(p => p.rec?.Ledger.LastOrDefault() is LedgerEntry le && le.Day == C.Day && le.Kills > 0)
            .OrderByDescending(p => p.rec!.Ledger[^1].Kills).FirstOrDefault();
        if (best.rec != null && y + ui.U(30) <= c.Bottom + ui.U(4))
        {
            var b = new Rectangle(c.X, c.Bottom - ui.U(28), c.Width, ui.U(28));
            ui.HGrad(b, Kit.Mul(Kit.Gilt, 0.3f), Kit.Gilt * 0f);
            ui.Rect(new Rectangle(b.X, b.Y, ui.U(2), b.Height), Kit.Gilt);
            ui.Portrait(best.rec.Type, new Rectangle(b.X + ui.U(6), b.Y + ui.U(2), ui.U(26), ui.U(24)));
            ui.Text("COMPANY OF THE DAY", b.X + ui.U(38), b.Y + ui.U(4), Kit.Gilt, 1);
            ui.Text(best.rec.Name.ToUpperInvariant(), b.X + ui.U(38), b.Y + ui.U(15), Style.Text, 1);
            var le = best.rec.Ledger[^1];
            ui.TextR($"{le.Kills} SLAIN . {le.Lost} LOST", b.Right - ui.U(6), b.Y + ui.U(10), Style.Dim, 1);
        }
    }

    /// What the battle paid: loot, ransoms, bounty, renown, horses.
    void Spoils(BattleReport rep, Rectangle box)
    {
        var c = Well(box, rep.Won ? "SPOILS" : "THE COST", rep.Won ? Style.Ok : Style.Bad);
        if (!rep.Won)
        {
            int y0 = c.Y;
            foreach (var l in ui.Wrap("YOUR ARMY FALLS BACK. WOUNDED LEFT ON THE FIELD MAY BE TAKEN.", c.Width, 1)) { ui.Text(l, c.X, y0, Style.Text, 1); y0 += ui.U(12); }
            return;
        }
        var rows = new (string Icon, string Label, string Value, Color Col)[]
        {
            ("coin", "LOOT", $"+{rep.Loot:0}", Kit.Gilt), ("chain", "RANSOMS", $"+{rep.Ransom:0}", Kit.Gilt), ("scroll", "BOUNTY", $"+{rep.Bounty:0}", Kit.Gilt),
            ("star", "RENOWN", $"+{rep.Renown:0}", Kit.Gilt), ("horse", "HORSES TAKEN", $"{rep.Horses}", Style.Text),
        };
        int rh = Math.Clamp(c.Height / rows.Length, ui.U(16), ui.U(28)), big = rh >= ui.U(26) ? 2 : 1, y = c.Y;
        foreach (var (icon, label, value, col) in rows)
        {
            var row = new Rectangle(c.X - ui.U(4), y, c.Width + ui.U(8), rh - ui.U(2));
            ui.Rect(row, Color.White * 0.035f);
            ui.Icon(icon, row.X + ui.U(5), row.Y + (row.Height - ui.U(7 * big)) / 2, col, big);
            ui.Text(label, row.X + ui.U(12 + 7 * big), row.Y + (row.Height - ui.U(7)) / 2, Style.Dim, 1);
            ui.TextR(value, row.Right - ui.U(5), row.Y + (row.Height - ui.U(7 * big)) / 2, value is "+0" or "0" ? Style.Dim : col, big);
            y += rh;
        }
    }

    /// Every company of yours: men before and after, the fallen, wounded and lost, and the drill it earned.
    void ReportCompanies(BattleReport rep, Rectangle box)
    {
        var c = Well(box, "YOUR COMPANIES", Style.Friendly);
        string[] heads = { "MEN", "FELL", "HURT", "LOST", "XP" };
        int[] cols = { c.Width - ui.U(250), c.Width - ui.U(180), c.Width - ui.U(140), c.Width - ui.U(100), c.Width - ui.U(56) };
        for (int i = 0; i < heads.Length; i++) ui.Text(heads[i], c.X + cols[i], c.Y, Style.Dim, 1);
        var mine = rep.Companies.Where(x => x.Team == 0).ToList();
        int y = c.Y + ui.U(12), rh = Math.Clamp((c.Bottom - y) / Math.Max(1, mine.Count), ui.U(14), ui.U(24));
        foreach (var x in mine)
        {
            if (y + rh > c.Bottom + ui.U(4)) { ui.Text($"+{mine.Count - mine.IndexOf(x)} MORE", c.X, y, Style.Dim, 1); break; }
            var t = Troops.Get(x.Troop);
            ui.Rect(new Rectangle(c.X - ui.U(4), y, c.Width + ui.U(8), rh - ui.U(2)), Color.White * (mine.IndexOf(x) % 2 == 0 ? 0.035f : 0.015f));
            int ty = y + (rh - ui.U(2) - ui.U(7)) / 2;
            if (rh >= ui.U(20)) ui.Portrait(t, new Rectangle(c.X - ui.U(2), y, ui.U(20), rh - ui.U(2)));
            int nx = c.X + (rh >= ui.U(20) ? ui.U(22) : 0);
            string nm = $"{x.Name} ({t.Name})".ToUpperInvariant();
            int fit = Math.Max(6, (c.X + cols[0] - ui.U(8) - nx) / (6 * ui.S));
            ui.Text(nm.Length > fit ? nm[..fit] : nm, nx, ty, Style.Text, 1);
            int bx = c.X + cols[0];
            ui.Bar(new Rectangle(bx, ty + ui.U(2), ui.U(30), ui.U(4)), x.MenAfter / (float)Math.Max(1, x.MenBefore), Style.Sides[2].Ui);
            ui.Text($"{x.MenAfter}", bx + ui.U(34), ty, Style.Text, 1);
            ui.Text($"{x.Killed}", c.X + cols[1], ty, x.Killed > 0 ? Style.Bad : Style.Dim, 1);
            ui.Text($"{x.Wounded}", c.X + cols[2], ty, x.Wounded > 0 ? Style.Warn : Style.Dim, 1);
            ui.Text($"{x.Captured + x.FledLost}", c.X + cols[3], ty, Style.Dim, 1);
            if (x.UpgradeReady) ui.Icon("up", c.X + cols[4] - ui.U(10), ty, Kit.Gilt, 1);
            ui.Text(x.Deployed ? $"+{x.XpGain:0}" : "RESERVE", c.X + cols[4], ty, x.Deployed ? Style.Ok : Style.Dim, 1);
            y += rh;
        }
    }

    /// What it did for your career: the renown ladder, companies ready to train, and your own fate.
    void Progress(BattleReport rep, Rectangle box)
    {
        var c = Well(box, "YOUR CAREER", Kit.Gilt);
        var cap = C.Captain;
        var (unlock, at) = Rules.NextRenownUnlock(cap);
        int y = c.Y;
        ui.Text("RENOWN", c.X, y, Style.Dim, 1);
        ui.TextR($"{cap.Renown:0} / {at:0}", c.Right, y, Kit.Gilt, 1);
        y += ui.U(11);
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(8)), (cap.Renown - RenownStep(cap, at)) / MathF.Max(1, at - RenownStep(cap, at)), Kit.Gilt, 12);
        y += ui.U(12);
        foreach (var l in ui.Wrap($"NEXT: {unlock}", c.Width, 1)) { ui.Text(l, c.X, y, Style.Dim, 1); y += ui.U(11); }
        y += ui.U(6);
        int ready = rep.Companies.Count(x => x.Team == 0 && x.UpgradeReady);
        if (ready > 0)
        {
            ui.Chip(c.X, y, $"{ready} COMPANY{(ready == 1 ? "" : "IES")} READY TO TRAIN", Kit.Gilt, "up");
            y += ui.U(16);
            ui.Text("TRAIN THEM UP IN A TOWN.", c.X, y, Style.Dim, 1);
            y += ui.U(16);
        }
        string? fate = rep.Captain switch
        {
            LeaderFate.Wounded => "YOU ARE WOUNDED: FOR A FEW DAYS YOU COMMAND FROM THE REAR.",
            LeaderFate.Captured => "YOU WERE TAKEN AND RANSOMED (30% OF YOUR GOLD).",
            _ => null,
        };
        if (fate != null)
        {
            ui.Icon(rep.Captain == LeaderFate.Captured ? "chain" : "heart", c.X, y, rep.Captain == LeaderFate.Captured ? Style.Bad : Style.Warn, 1);
            foreach (var l in ui.Wrap(fate, c.Width - ui.U(14), 1)) { ui.Text(l, c.X + ui.U(14), y, rep.Captain == LeaderFate.Captured ? Style.Bad : Style.Warn, 1); y += ui.U(11); }
        }
    }
}
