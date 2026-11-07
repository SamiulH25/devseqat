using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the realm panel (R): the realms, their relations and the chronicle; your career and acts; and the vassals list.
partial class MapScreen
{
    /// Your vassals and their standing orders, and companions you could raise to land (F6), under the household tab.
    void Vassals(Rectangle r, int y)
    {
        int x = r.X + ui.U(24);
        var vassals = C.Vassals.ToList();
        var raisable = C.Companions.ToList();
        if (vassals.Count == 0 && raisable.Count == 0) return;
        ui.Text("YOUR VASSALS", x, y, Style.Accent, 1);
        y += ui.U(16);
        foreach (var v in vassals.Take(4))
        {
            ui.Text($"{v.Name.ToUpperInvariant()}  {v.Men} MEN  {(v.Down ? "SCATTERED" : v.Task.ToUpperInvariant())}", x, y + ui.U(6), Style.Text, 1);
            for (int i = 0; i < Campaign.Orders.Length; i++)
            {
                string o = Campaign.Orders[i];
                string label = o == "defend" ? "DEFEND MY LANDS" : o == "follow" ? "FOLLOW ME" : "JOIN THE HOST";
                if (ui.Button(new Rectangle(r.Right - ui.U(24) - (3 - i) * ui.U(150), y, ui.U(144), ui.U(22)), label, true, "", v.Order == o)) note = C.SetOrder(v, o) ?? "";
            }
            y += ui.U(26);
        }
        if (vassals.Count == 0) { ui.Text("NONE YET. AS A LORD WITH TWO FIEFS YOU MAY GIVE ONE TO A COMPANION.", x, y, Style.Dim, 1); y += ui.U(16); }
        foreach (var k in raisable.Take(3))
        {
            bool can = C.CanRaiseVassal(k, out string why);
            if (ui.Button(new Rectangle(x, y, ui.U(460), ui.U(22)), $"RAISE {k.Name.ToUpperInvariant()} TO A LORD" + (can ? $" WITH {C.FiefToGrant()!.Name.ToUpperInvariant()}" : ""), can, can ? "" : why))
                note = C.RaiseVassal(k) ?? $"{k.Name.ToUpperInvariant()} IS YOUR VASSAL";
            y += ui.U(26);
        }
    }

    /// The realm screen (R) as two tabs: THE REALMS (a card per realm, their relations and the chronicle) and YOUR CAREER
    /// (the rank ladder on the left; the realm you serve, its council and the acts your rank allows on the right).
    void RealmPanel()
    {
        var r = Modal(1060, 620, "THE REALMS");
        var tabs = new[] { "THE REALMS", "YOUR CAREER" };
        int pick = ui.Tabs(new Rectangle(r.X + ui.U(20), r.Y + ui.U(24), ui.U(260), ui.U(22)), tabs, realmTab);
        if (pick >= 0) { realmTab = pick; note = ""; }
        var body = new Rectangle(r.X + ui.U(20), r.Y + ui.U(56), r.Width - ui.U(40), r.Height - ui.U(122));
        if (realmTab == 0) Realms(body);
        else
        {
            var rank = C.Rank;
            int x = body.X + ui.U(4), lw = ui.U(470);
            RankLadder(rank, x, body.Y, lw);
            int rx = x + lw + ui.U(24), ry = body.Y, rw = body.Right - ui.U(4) - rx;
            if (C.Realm is FactionState fs) RealmColumn(r, rank, fs, C.Captain.Contract, rx, ry, rw);
            else
            {
                ui.Text("YOU SERVE NO REALM.", rx, ry, Style.Text);
                ui.Text("SIGN WITH ONE AT A TOWN'S STEWARD (CONTRACT TAB).", rx, ry + ui.U(20), Style.Dim, 1);
            }
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: "R")) panel = PanelKind.None;
    }

    /// Every living realm as a card, then the relations between them and the chronicle of the war.
    void Realms(Rectangle a)
    {
        var alive = Enumerable.Range(0, C.S.Factions.Count).Where(C.Alive).ToList();
        int gap = ui.U(10), n = Math.Max(1, alive.Count), cw = (a.Width - (n - 1) * gap) / n, ch = (int)(a.Height * 0.56f);
        for (int i = 0; i < alive.Count; i++) RealmCard(alive[i], new Rectangle(a.X + i * (cw + gap), a.Y + ui.U(12), cw, ch - ui.U(12)));
        int by = a.Y + ch + gap, bh = a.Bottom - by, lw = (int)(a.Width * 0.42f);
        Relations(alive, Well(new Rectangle(a.X, by, lw, bh), "RELATIONS", Kit.Gilt));
        Chronicle(Well(new Rectangle(a.X + lw + gap, by, a.Width - lw - gap, bh), "CHRONICLE", Style.Friendly));
    }

    /// How a realm stands to you: your liege, at war with your liege, a truce, allied or at peace.
    (string Text, Color Colour) Stance(int f)
    {
        int me = C.Captain.Contract;
        if (f == me) return (C.RulerOf(f) == C.Me ? "YOUR REALM" : "YOUR LIEGE", Style.Ok);
        if (me < 0) return ("NEUTRAL", Style.Dim);
        if (C.FactionsAtWar(me, f)) return ("AT WAR", Style.Bad);
        if (C.Allied(me, f)) return ("ALLIED", Style.Ok);
        if (C.S.Factions[f].TributaryOf == me) return ("TRIBUTARY", Kit.Gilt);
        if (C.WarBetween(me, f) is War w && w.TruceUntil > C.S.Hours) return ($"TRUCE {(w.TruceUntil - (float)C.S.Hours) / 24:0} DAYS", Style.Warn);
        return ("AT PEACE", Style.Text);
    }

    /// One realm: shield, name, culture, stance, land held, war weariness, lords and holdings, ruler and the troops it fields.
    void RealmCard(int f, Rectangle box)
    {
        var hd = Style.Sides[Style.PaletteOf(f)];
        var c = ui.Plate(box, null, hd.Ui);
        int sh = Math.Min(ui.U(64), c.Height / 4);
        ui.Shield(new Rectangle(c.Center.X - (int)(sh / 1.2f) / 2, c.Y - ui.U(4), (int)(sh / 1.2f), sh), hd);
        int y = c.Y + sh + ui.U(2);
        string name = C.FactionNames[f].ToUpperInvariant();
        ui.TextC(name, c.Center.X, y, Style.Text, ui.TW(name, 2) <= c.Width ? 2 : 1); y += ui.U(18);
        var cult = Factions.Culture(Factions.CultureOf(f));
        string text = (cult?.Text ?? "").ToUpperInvariant();
        while (text.Length > 4 && ui.TW(text, 1) > c.Width) text = text[..^2];
        ui.TextC(text, c.Center.X, y, Style.Dim, 1); y += ui.U(14);
        var (st, sc) = Stance(f);
        int chipW = ui.TW(st, 1) + ui.U(10);
        ui.Chip(c.Center.X - chipW / 2, y, st, sc); y += ui.U(20);
        float share = C.RealmShare(f);
        ui.Text("LAND HELD", c.X, y, Style.Dim, 1); ui.TextR(Campaign.Pct(share), c.Right, y, Style.Text, 1); y += ui.U(10);
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(7)), share / MathF.Max(0.01f, Campaign.WinShare), hd.Ui, 10); y += ui.U(13);
        float weary = C.Exhaustion(f);
        if (weary > 0)
        {
            ui.Text("WAR WEARINESS", c.X, y, Style.Dim, 1); ui.TextR($"{weary:0}", c.Right, y, weary > 55 ? Style.Bad : Style.Warn, 1); y += ui.U(10);
            ui.Gauge(new Rectangle(c.X, y, c.Width, ui.U(5)), weary / 100f, weary > 55 ? Style.Bad : Style.Warn); y += ui.U(10);
        }
        int lords = C.S.Parties.Count(p => p.Kind == PartyKind.Lord && p.Faction == f);
        int towns = C.S.Settlements.Count(s => s.Faction == f && s.Kind == SettlementKind.Town);
        int castles = C.S.Settlements.Count(s => s.Faction == f && s.Kind == SettlementKind.Castle);
        int third = c.Width / 3;
        if (y + ui.U(14) <= c.Bottom)
        {
            foreach (var (k, icon, v) in new[] { (0, "crown", lords), (1, "tower", towns), (2, "flag", castles) })
            {
                int ix = c.X + k * third + third / 2;
                ui.Icon(icon, ix - ui.U(12), y, Style.Dim, 1);
                ui.Text(v.ToString(), ix, y, Style.Text, 1);
            }
            y += ui.U(15);
        }
        if (y + ui.U(10) <= c.Bottom)
        {
            var ruler = C.RulerOf(f);
            string rn = ruler == null ? "NONE" : ruler == C.Me ? "YOU" : ruler.Name.ToUpperInvariant();
            while (rn.Length > 4 && ui.TW(rn, 1) > c.Width - ui.U(56)) rn = rn[..^2];
            ui.Text("RULED BY", c.X, y, Style.Dim, 1); ui.TextR(rn, c.Right, y, Style.Text, 1); y += ui.U(14);
        }
        int ps = Math.Min((c.Width - ui.U(8)) / 3, c.Bottom - y);
        if (ps >= ui.U(30))
        {
            string[] roles = { "infantry", "archers", "horse" };
            for (int k = 0; k < 3; k++)
            {
                var pr = new Rectangle(c.X + k * (ps + ui.U(4)), y, ps, ps);
                ui.VGrad(pr, Kit.Mul(hd.Cloth, 0.5f), Kit.IronDark, 2);
                ui.Portrait(Troops.Get(Factions.Line(f, roles[k], 4)), pr, Math.Max(1, ps / ui.U(36)) * ui.S);
                ui.Frame(pr, Color.Black * 0.6f, ui.U(1));
            }
        }
    }

    /// A grid of who is at war with whom.
    void Relations(List<int> alive, Rectangle m)
    {
        int n = alive.Count;
        if (n == 0) return;
        int label = ui.U(64), cell = Math.Min((m.Width - label) / n, (m.Height - ui.U(14)) / n);
        int ox = m.X + label, oy = m.Y + ui.U(12);
        string Short(int f) { var w = C.FactionNames[f].ToUpperInvariant().Split(' ').Last(); return w.Length > 8 ? w[..8] : w; }
        for (int i = 0; i < n; i++)
        {
            var col = Style.Sides[Style.PaletteOf(alive[i])].Ui;
            string head = Short(alive[i]);
            if (ui.TW(head, 1) > cell - ui.U(4)) head = head[..Math.Max(1, Math.Min(head.Length, (cell - ui.U(4)) / (6 * ui.S)))];
            ui.TextC(head, ox + i * cell + cell / 2f, m.Y, col, 1);
            ui.Text(Short(alive[i]), m.X, oy + i * cell + cell / 2 - ui.U(3), col, 1);
            for (int j = 0; j < n; j++)
            {
                int a = alive[i], b = alive[j];
                var r = new Rectangle(ox + j * cell + ui.U(1), oy + i * cell + ui.U(1), cell - ui.U(2), cell - ui.U(2));
                string s = a == b ? "" : C.FactionsAtWar(a, b) ? "WAR" : C.Allied(a, b) ? "ALLY"
                    : C.S.Factions[b].TributaryOf == a || C.S.Factions[a].TributaryOf == b ? "TRIB"
                    : C.WarBetween(a, b) is War w && w.TruceUntil > C.S.Hours ? "TRUCE" : "PEACE";
                var sc = s switch { "WAR" => Style.Bad, "TRUCE" => Style.Warn, "TRIB" => Kit.Gilt, "" => Color.White * 0.2f, _ => Style.Ok };
                ui.Rect(r, s == "" ? Color.Black * 0.3f : Kit.Mul(sc, 0.35f));
                ui.Frame(r, s == "" ? Color.Black * 0.5f : Kit.Mul(sc, 0.8f), ui.U(1));
                if (s != "" && ui.TW(s, 1) <= r.Width) ui.TextC(s, r.Center.X + ui.U(1), r.Center.Y - ui.U(3), Color.Lerp(sc, Color.White, 0.5f), 1);
            }
        }
    }

    /// The latest lines of the campaign log, newest first, on a thread of dots.
    void Chronicle(Rectangle c)
    {
        int y = c.Y, step = ui.U(14);
        foreach (var e in Enumerable.Reverse(C.S.Log))
        {
            if (y + ui.U(10) > c.Bottom) break;
            string t = e.Text.ToUpperInvariant();
            var col = t.Contains("WAR") ? Style.Bad : t.StartsWith("YOU") ? Kit.Gilt : t.Contains("TRUCE") || t.Contains("PEACE") ? Style.Warn : Style.Text;
            ui.Rect(new Rectangle(c.X + ui.U(1), y + ui.U(5), ui.U(1), step - ui.U(4)), Color.White * 0.1f);
            ui.Rect(new Rectangle(c.X, y + ui.U(2), ui.U(3), ui.U(3)), col);
            ui.Text($"DAY {(int)(e.At / 24) + 1}", c.X + ui.U(10), y, Style.Dim, 1);
            int tx = c.X + ui.U(64), fit = (c.Right - tx) / (6 * ui.S);
            ui.Text(t.Length > fit ? t[..Math.Max(1, fit - 1)] + "." : t, tx, y, Style.Text, 1);
            y += step;
        }
        if (C.S.Log.Count == 0) ui.Text("NOTHING WORTH THE INK YET.", c.X, y, Style.Dim, 1);
    }

    /// The career ladder down the left: every rank, what it takes and what it opens, then what the next step needs.
    void RankLadder(Rank rank, int x, int y, int lw)
    {
        foreach (var (rk, how, opens) in Campaign.Ladder)
        {
            bool now = rk == rank, done = rk < rank;
            var box = new Rectangle(x, y, lw, ui.U(52));
            ui.Rect(box, now ? Style.Accent * 0.18f : Color.White * 0.04f);
            ui.Text(Campaign.RankName(rk), box.X + ui.U(8), box.Y + ui.U(5), now ? Style.Accent : done ? Style.Text : Style.Dim, 2);
            if (done) ui.TextR("REACHED", box.Right - ui.U(8), box.Y + ui.U(8), Style.Ok, 1);
            ui.Text(how, box.X + ui.U(8), box.Y + ui.U(25), Style.Dim, 1);
            ui.Text("OPENS: " + opens, box.X + ui.U(8), box.Y + ui.U(37), Style.Dim, 1);
            y += ui.U(56);
        }
        if (C.NextRank() is var (next, needs)) ui.Text(next == rank ? $"TO WIN: {needs}" : $"NEXT: {Campaign.RankName(next)}: {needs}", x, y + ui.U(4), Style.Accent, 1);
    }

    /// The right column when you serve a realm: its state, the council, and the acts your rank allows.
    void RealmColumn(Rectangle r, Rank rank, FactionState fs, int f, int rx, int ry, int rw)
    {
        ui.Text(C.FactionNames[f].ToUpperInvariant(), rx, ry, SideColor(f), 2);
        ry += ui.U(24);
        string Who(Character? ch) => ch == null ? "NONE" : ch == C.Me ? "YOU" : ch.Name.ToUpperInvariant();
        ui.Text($"RULER: {Who(C.RulerOf(f))}   MARSHAL: {Who(C.MarshalOf(f))}", rx, ry, Style.Text, 1);
        ry += ui.U(14);
        var obj = C.ObjectiveOf(f);
        ui.Text($"OBJECTIVE: {(obj == null ? "NONE" : obj.Name.ToUpperInvariant())}   LAND HELD: {Campaign.Pct(C.RealmShare(f))} (WIN AT {Campaign.Pct(Campaign.WinShare)})", rx, ry, Style.Dim, 1);
        ry += ui.U(14);
        var foes = Enumerable.Range(0, C.S.Factions.Count).Where(g => C.FactionsAtWar(f, g)).Select(g => C.FactionNames[g].ToUpperInvariant()).ToList();
        ui.Text("AT WAR WITH: " + (foes.Count == 0 ? "NOBODY" : string.Join(", ", foes)) + (foes.Count > 0 ? $"   WEARINESS {C.Exhaustion(f):0}" : ""), rx, ry, Style.Dim, 1);
        ry += ui.U(14);
        if (fs.TributaryOf >= 0) { ui.Text($"TRIBUTARY TO THE {C.FactionNames[fs.TributaryOf].ToUpperInvariant()}", rx, ry, Style.Warn, 1); ry += ui.U(14); }
        if (C.S.Summons is Summons sm)
        {
            ui.Text($"SUMMONED TO {C.SettlementById(sm.Seat).Name.ToUpperInvariant()}: {MathF.Max(0, (sm.Until - (float)C.S.Hours) / 24):0.0} DAYS LEFT", rx, ry, Style.Accent, 1);
            ry += ui.U(14);
        }
        ry += ui.U(6);
        if (rank != Rank.King)
        {
            ui.Text($"THE COUNCIL SITS IN {MathF.Max(0, (fs.NextCouncil - (float)C.S.Hours) / 24):0} DAYS. LAST TIME:", rx, ry, Style.Accent, 1);
            ry += ui.U(14);
            foreach (var line in fs.LastCouncil.Take(4)) { ui.Text(line.ToUpperInvariant(), rx + ui.U(8), ry, Style.Dim, 1); ry += ui.U(12); }
            if (fs.LastCouncil.Count == 0) { ui.Text("IT HAS NOT YET SAT.", rx + ui.U(8), ry, Style.Dim, 1); ry += ui.U(12); }
            ry += ui.U(8);
        }
        int bw = rw, bh = ui.U(24);
        Rectangle Btn() { var b = new Rectangle(rx, ry, bw, bh); ry += bh + ui.U(4); return b; }
        if (rank < Rank.Vassal)
        {
            bool can = C.CanSwearFealty(out string why);
            if (ui.Button(Btn(), "SWEAR FEALTY", can, can ? "" : why)) note = C.SwearFealty() ?? "YOU ARE A VASSAL";
            if (!can) { ui.Text(why, rx, ry, Style.Dim, 1); ry += ui.U(14); }
        }
        // Acts of a lord, a marshal and a king.
        var others = Enumerable.Range(0, C.S.Factions.Count).Where(g => g != f && C.Alive(g)).ToList();
        if (rank is Rank.Lord or Rank.Marshal)
        {
            foreach (int g in others.Where(g => !C.FactionsAtWar(f, g)))
                if (ui.Button(Btn(), $"PROPOSE WAR ON THE {C.FactionNames[g].ToUpperInvariant()}", fs.WarMotion < 0, fs.WarMotion == g ? "AT THE NEXT COUNCIL" : ""))
                    note = C.ProposeWar(g) ?? "THE MOTION GOES TO THE NEXT COUNCIL";
        }
        if (rank == Rank.King) ry = KingDiplomacy(rx, ry, bw, f, others);
        if (rank >= Rank.Marshal)
        {
            ui.Text("NAME THE HOSTS' OBJECTIVE:", rx, ry + ui.U(4), Style.Accent, 1);
            if (ui.Button(new Rectangle(rx + bw - ui.U(160), ry, ui.U(160), ui.U(20)), "CALL THE HOST", obj != null)) note = C.CallHost() ?? "THE LORDS MARCH";
            ry += ui.U(22);
            foreach (var s in C.ObjectiveChoices(f).Take(rank == Rank.King ? 3 : 4))
                if (ui.Button(Btn(), $"MARCH ON {s.Name.ToUpperInvariant()} ({C.FactionNames[s.Faction].ToUpperInvariant()})", true, "", obj == s))
                    note = C.SetObjective(s) ?? $"THE HOSTS MARCH ON {s.Name.ToUpperInvariant()}";
        }
        if (rank is Rank.Lord or Rank.Marshal && ry < r.Bottom - ui.U(90))
        {
            bool can = C.CanDeclareIndependence(out string why);
            if (ui.Button(new Rectangle(rx, r.Bottom - ui.U(84), bw, ui.U(26)), "DECLARE INDEPENDENCE", can, why))
                note = C.DeclareIndependence() ?? "";
        }
    }

    /// A king's row per other realm: war standing or peace, and buttons to offer terms, declare war or ally. Returns the new y.
    int KingDiplomacy(int rx, int ry, int bw, int f, List<int> others)
    {
        foreach (int g in others)
        {
            var wr = C.WarBetween(f, g);
            bool war = C.FactionsAtWar(f, g);
            string status = war ? $"AT WAR  WEARINESS {C.ExhaustionOf(wr!, f):0} / {C.ExhaustionOf(wr!, g):0}  STANDING {C.Margin(wr!, f, g):+0.0;-0.0}"
                : C.Allied(f, g) ? "ALLIED" : C.S.Factions[g].TributaryOf == f ? "YOUR TRIBUTARY"
                : wr != null && wr.TruceUntil > C.S.Hours ? $"TRUCE, {(wr.TruceUntil - (float)C.S.Hours) / 24:0} DAYS" : "AT PEACE";
            ui.Text($"{C.FactionNames[g].ToUpperInvariant()}: {status}", rx, ry, SideColor(g), 1);
            ry += ui.U(13);
            int sw = (bw - ui.U(12)) / 4;
            var labels = war ? new[] { "TRUCE", "GOLD", "A SEAT", "TRIBUTE" } : new[] { "DECLARE WAR", "ALLIANCE" };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = new Rectangle(rx + i * (sw + ui.U(4)), ry, war ? sw : sw * 2, ui.U(22));
                if (!war) b.X = rx + i * (sw * 2 + ui.U(4));
                if (ui.Button(b, labels[i]))
                    note = war ? C.OfferPeace(g, (Terms)i) ?? "THEY ACCEPT" : (i == 0 ? C.DeclareWar(g) : C.ProposeAlliance(g)) ?? (i == 0 ? "WAR IS DECLARED" : "AN ALLIANCE IS SWORN");
                if (war && ui.Hover(b)) note = $"OFFER PEACE ON {Campaign.TermsText((Terms)i)}";
            }
            ry += ui.U(28);
        }
        return ry;
    }
}
