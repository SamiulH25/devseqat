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
        var tabs = new[] { "THE REALMS", "YOUR CAREER", "THE LEGEND", "THE ORDERS" };
        int pick = ui.Tabs(new Rectangle(r.X + ui.U(20), r.Y + ui.U(24), ui.U(520), ui.U(22)), tabs, realmTab);
        if (pick >= 0) { realmTab = pick; note = ""; }
        var body = new Rectangle(r.X + ui.U(20), r.Y + ui.U(56), r.Width - ui.U(40), r.Height - ui.U(122));
        if (realmTab == 0) Realms(body);
        else if (realmTab == 2) Legend(body);
        else if (realmTab == 3) OrdersTab(body);
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
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.RealmScreen))) panel = CameFrom(PanelKind.Realm);
    }

    /// THE LEGEND (docs/archive/LEGACY.md L5-L7): each company's fame and battle honours, the honours realms have taken, the dead
    /// it remembers, and the two ways to end the story once a company is legend enough.
    void Legend(Rectangle a)
    {
        int x = a.X + ui.U(4), w = a.Width - ui.U(8), y = a.Y;
        int fit = Math.Max(20, w / (6 * ui.S));
        string Cut(string s) => s.Length > fit ? s[..fit] : s;
        ui.Text($"YOUR FAME: {C.Fame} ({Campaign.FameWord(C.Fame)})", x, y, Kit.Gilt, 2);
        y += ui.U(18);
        ui.Text(Cut($"CONTRACTS PAY +{(C.FamePay - 1) * 100:0}%. LORDS THINK +{C.FameRegard:0} BETTER OF YOU. MORE MEN FOLLOW YOU, AND YOUR OWN PEOPLE COME KNOWING THE WORK."), x, y, Style.Dim, 1);
        y += ui.U(18);
        foreach (var c in C.Player.Companies.OrderByDescending(Campaign.CompanyFame))
        {
            if (y > a.Bottom - ui.U(176)) break;
            var row = new Rectangle(x - ui.U(4), y - ui.U(3), w + ui.U(8), ui.U(30));
            ui.Rect(row, Color.White * 0.04f);
            ui.Text(c.Name.ToUpperInvariant(), x, y, Style.Text, 1);
            ui.TextR($"{Campaign.N(c.Battles, "BATTLE")}, FAME {Campaign.CompanyFame(c)} ({Campaign.FameWord(Campaign.CompanyFame(c))})", x + w, y, Style.Dim, 1);
            string hon = c.BattleHonours.Count > 0 ? "HONOURS: " + string.Join(", ", c.BattleHonours).ToUpperInvariant() : "NO BATTLE HONOURS YET. WIN OUTNUMBERED, SLAY THEIR CAPTAIN OR TAKE A WALL.";
            if (c.LostHonours.Count > 0) hon += ". TAKEN: " + string.Join(", ", c.LostHonours.Select(l => $"{l.Name} BY THE {C.FactionNames[l.Faction]}")).ToUpperInvariant();
            ui.Text(Cut(hon), x, y + ui.U(11), c.BattleHonours.Count > 0 ? Kit.Gilt : Style.Dim, 1);
            y += ui.U(34);
        }
        OwnOrderBlock(x, Math.Max(y, a.Bottom - ui.U(168)), w, Cut);
        y = Math.Max(y, a.Bottom - ui.U(122));
        ui.Divider(x, y, w, Kit.Gilt * 0.5f);
        y += ui.U(10);
        var dead = C.S.Fallen.Where(m => m.Fate is "fell" or "missing").ToList();
        ui.Text(Cut(dead.Count == 0 ? "NO NAMED MAN LOST YET." : $"{dead.Count} NAMED MEN LOST. " + string.Join(", ", dead.OrderByDescending(m => m.Battles).Take(6).Select(m => m.Name.ToUpperInvariant())) + "."), x, y, Style.Text, 1);
        y += ui.U(16);
        string? why = C.CannotRetire();
        ui.Text(Cut(why == null ? $"{C.LegendCompany!.Name.ToUpperInvariant()} IS A LEGEND. YOU MAY END YOUR STORY." : $"TO END YOUR STORY: {why}."), x, y, why == null ? Kit.Gilt : Style.Dim, 1);
        y += ui.U(16);
        var heir = C.Successor;
        if (ui.Button(new Rectangle(x, y, ui.U(220), ui.U(20)), "RETIRE ON THE LEGEND", why == null)) { note = C.Retire() ?? ""; if (C.S.GameOver) panel = PanelKind.GameOver; }
        if (ui.Button(new Rectangle(x + ui.U(230), y, ui.U(280), ui.U(20)), heir != null ? $"HAND OVER TO {heir.Name.ToUpperInvariant()}" : "HAND OVER (NO COMPANION)", why == null && heir != null)) note = C.HandOver() ?? "";
        if (ui.Hover(new Rectangle(x, y, ui.U(510), ui.U(26))))
            note = "RETIRE ENDS THE CAMPAIGN AS A WIN AND TELLS THE COMPANY'S STORY. HANDING OVER GIVES THE COMPANY TO A COMPANION (ONE WHO ROSE FROM ITS RANKS FIRST): YOU PLAY ON AS HIM, WITH HALF YOUR RENOWN.";
    }

    /// The three vows chosen for an order you are about to found (K8).
    readonly List<string> foundVows = new();

    /// Your own order (docs/archive/KNIGHT_ORDERS.md 8, K8): founded, its creed, form and knights, and the button to knight the next
    /// named man; or what founding needs, the vows to choose and the button to found it.
    void OwnOrderBlock(int x, int y, int w, Func<string, string> cut)
    {
        ui.Divider(x, y, w, Kit.Gilt * 0.5f);
        y += ui.U(8);
        int bw = ui.U(170), bh = ui.U(20);
        if (C.S.OwnOrder is FoundedOrder own)
        {
            string form = Forms.Has(own.Form) ? Forms.Get(own.Form).Name.ToUpperInvariant() : "NONE";
            ui.Text(cut($"THE {own.Name.ToUpperInvariant()}. FOUNDED DAY {own.Day}. GRAND MASTERS: {own.Masters}. FORM: {form}."), x, y, Kit.Gilt, 1);
            y += ui.U(12);
            ui.Text(cut("CREED: " + string.Join(", ", own.Vows.Select(VowWord)) + "."), x, y, Style.Dim, 1);
            y += ui.U(12);
            var knights = own.Knights.Select(C.NamedMan).Where(m => m != null).Select(m => "SIR " + m!.Name.ToUpperInvariant()).ToList();
            ui.Text(cut($"KNIGHTS {knights.Count}/{C.KnightsMax}: " + (knights.Count == 0 ? "NONE YET." : string.Join(", ", knights) + ".")), x, y, Style.Text, 1);
            var next = C.NextKnight;
            string? why = C.CannotKnight(next);
            if (ui.Button(new Rectangle(x + w - bw, y - ui.U(26), bw, bh), next != null ? $"KNIGHT {next.Name.ToUpperInvariant()}" : "KNIGHT A MAN", why == null)) note = C.Knight(next) ?? "";
            return;
        }
        string? cannot = C.CannotFound();
        if (cannot == null && ui.Button(new Rectangle(x + w - bw, y - ui.U(4), bw, bh), "FOUND THE ORDER", foundVows.Count == 3)) note = C.FoundOrder(foundVows) ?? "";
        ui.Text(cut(cannot == null ? $"YOU MAY FOUND THE {Campaign.OrderNameOf((C.LegendCompany ?? C.BestCompany)!.Name).ToUpperInvariant()}. CHOOSE THREE VOWS FOR ITS CREED." : $"TO FOUND YOUR OWN ORDER: {cannot}."), x, y, cannot == null ? Kit.Gilt : Style.Dim, 1);
        y += ui.U(13);
        if (cannot != null) return;
        int cx = x;
        foreach (var v in KnightOrders.VowKinds)
        {
            bool on = foundVows.Contains(v);
            int cw = ui.Chip(cx, y, VowWord(v), on ? Kit.Gilt : Style.Dim);
            if (ui.Clicked(new Rectangle(cx, y - ui.U(2), cw, ui.U(12)))) { if (on) foundVows.Remove(v); else if (foundVows.Count < 3) foundVows.Add(v); }
            cx += cw + ui.U(4);
        }
    }

    /// Every living realm as a card, then the relations between them and the chronicle of the war.
    void Realms(Rectangle a)
    {
        var alive = Enumerable.Range(0, C.S.Factions.Count).Where(C.Alive).ToList();
        int gap = ui.U(10), n = Math.Max(1, alive.Count), cw = (a.Width - (n - 1) * gap) / n, ch = (int)(a.Height * 0.46f);
        hoverRealm = null;
        for (int i = 0; i < alive.Count; i++) RealmCard(alive[i], new Rectangle(a.X + i * (cw + gap), a.Y + ui.U(12), cw, ch - ui.U(12)));
        int by = a.Y + ch + gap, bh = a.Bottom - by, lw = (int)(a.Width * 0.36f);
        Relations(alive, Well(new Rectangle(a.X, by, lw, bh), "WARS AND ALLIANCES", Kit.Gilt));
        Chronicle(Well(new Rectangle(a.X + lw + gap, by, a.Width - lw - gap, bh), "CHRONICLE", Style.Friendly));
        if (hoverRealm is var (hb, hn, hl)) ui.Tooltip(hb.Center.X, hb.Y + ui.U(20), hn, hl);
    }

    /// The realm card under the mouse and its tooltip lines, drawn after every card so none covers it.
    (Rectangle Box, string Name, string[] Lines)? hoverRealm;

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
        // Seven cards share the width: a thin inner margin, so names and lines have room.
        ui.PlateBody(box, hd.Ui);
        var c = new Rectangle(box.X + ui.U(7), box.Y + ui.U(12), box.Width - ui.U(14), box.Height - ui.U(19));
        int sh = Math.Min(ui.U(64), c.Height / 4);
        ui.Shield(new Rectangle(c.Center.X - (int)(sh / 1.2f) / 2, c.Y - ui.U(4), (int)(sh / 1.2f), sh), hd);
        int y = c.Y + sh + ui.U(2);
        string name = C.FactionNames[f].ToUpperInvariant();
        // One size for every realm, and two lines' room on every card (a long name such as "Order of the Ashen Sun" takes
        // both), so the rows under the names line up across the cards.
        var nl = ui.Wrap(name, c.Width);
        int ny = nl.Count >= 2 ? y : y + ui.U(5);
        for (int i = 0; i < Math.Min(2, nl.Count); i++) ui.TextFitC(i == 1 ? string.Join(" ", nl.Skip(1)) : nl[i], c.Center.X, ny + i * ui.U(10), c.Width, Style.Text, 1);
        y += ui.U(26);
        var (st, sc) = Stance(f);
        int chipW = ui.TW(st, 1) + ui.U(10);
        ui.Chip(c.Center.X - chipW / 2, y, st, sc); y += ui.U(22);
        float share = C.RealmShare(f);
        ui.Text("LAND", c.X, y, Style.Dim, 1); ui.TextR(Campaign.Pct(share), c.Right, y, Style.Text, 1); y += ui.U(10);
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(7)), share / MathF.Max(0.01f, Campaign.WinShare), hd.Ui, 10); y += ui.U(15);
        var ruler = C.RulerOf(f);
        string rn = ruler == null ? "NONE" : ruler == C.Me ? "YOU" : ruler.Name.ToUpperInvariant();
        var rl = ui.Wrap(rn, c.Width);
        for (int i = 0; i < Math.Min(2, rl.Count); i++) ui.TextFitC(i == 1 ? string.Join(" ", rl.Skip(1)) : rl[i], c.Center.X, y + (rl.Count >= 2 ? i * ui.U(10) : ui.U(5)), c.Width, Style.Dim, 1);
        y += ui.U(26);
        // The rest on hover (docs/PLAYTEST.md PT2e): its style of war, its strength and its weariness.
        if (ui.Hover(box))
        {
            var cult = Factions.Culture(Factions.CultureOf(f));
            int lords = C.S.Parties.Count(p => p.Kind == PartyKind.Lord && p.Faction == f);
            int towns = C.S.Settlements.Count(s => s.Faction == f && s.Kind == SettlementKind.Town);
            int castles = C.S.Settlements.Count(s => s.Faction == f && s.Kind == SettlementKind.Castle);
            var lines = ui.Wrap((cult?.Text ?? "").ToUpperInvariant(), ui.U(220)).ToList();
            lines.Add($"{lords} LORDS, {towns} TOWNS, {castles} CASTLES");
            if (C.Exhaustion(f) > 0) lines.Add($"WAR WEARINESS {C.Exhaustion(f):0}");
            hoverRealm = (box, name, lines.ToArray());
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

    /// Who is at war with whom, and who is allied: one line a pair (peace, the usual state, is left out). Your realm's own
    /// wars come first.
    void Relations(List<int> alive, Rectangle m)
    {
        int me = C.Captain.Contract, y = m.Y;
        var pairs = new List<(int A, int B, bool War)>();
        for (int i = 0; i < alive.Count; i++)
            for (int j = i + 1; j < alive.Count; j++)
            {
                int a = alive[i], b = alive[j];
                if (C.FactionsAtWar(a, b)) pairs.Add((a, b, true));
                else if (C.Allied(a, b)) pairs.Add((a, b, false));
            }
        foreach (var (a, b, war) in pairs.OrderByDescending(p => p.A == me || p.B == me).ThenByDescending(p => p.War))
        {
            if (y + ui.U(12) > m.Bottom) break;
            if (a == me || b == me) ui.Rect(new Rectangle(m.X - ui.U(2), y - ui.U(3), m.Width + ui.U(4), ui.U(14)), Kit.Gilt * 0.08f);
            int x = m.X + ui.U(2);
            string an = C.FactionNames[a].ToUpperInvariant(), bn = C.FactionNames[b].ToUpperInvariant(), mid = war ? "  AT WAR WITH  " : "  ALLIED TO  ";
            ui.Text(an, x, y, SideColor(a), 1); x += ui.TW(an, 1);
            ui.Text(mid, x, y, war ? Style.Bad : Style.Ok, 1); x += ui.TW(mid, 1);
            ui.TextFit(bn, x, y, m.Right - x, SideColor(b), 1);
            y += ui.U(14);
        }
        ui.Text(pairs.Count == 0 ? "ALL THE REALMS ARE AT PEACE." : "THE REST ARE AT PEACE.", m.X + ui.U(2), y + ui.U(4), Style.Dim, 1);
    }

    /// The latest lines of the campaign log, newest first, on a thread of dots.
    void Chronicle(Rectangle c)
    {
        int y = c.Y, step = ui.U(14);
        foreach (var e in C.ChronicleEntries)
        {
            if (y + ui.U(10) > c.Bottom) break;
            string t = e.Text.ToUpperInvariant();
            var col = C.IsDeed(e) || t.StartsWith("YOU") ? Kit.Gilt : t.Contains("WAR") ? Style.Bad : t.Contains("TRUCE") || t.Contains("PEACE") ? Style.Warn : Style.Text;
            ui.Rect(new Rectangle(c.X + ui.U(1), y + ui.U(5), ui.U(1), step - ui.U(4)), Color.White * 0.1f);
            ui.Rect(new Rectangle(c.X, y + ui.U(2), ui.U(3), ui.U(3)), col);
            ui.Text($"DAY {(int)(e.At / 24) + 1}", c.X + ui.U(10), y, Style.Dim, 1);
            int tx = c.X + ui.U(64), fit = (c.Right - tx) / (6 * ui.S);
            ui.Text(t.Length > fit ? t[..Math.Max(1, fit - 1)] + "." : t, tx, y, C.IsDeed(e) ? Kit.Parchment : Style.Text, 1);
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
        if (rank != Rank.King) // a king's rows below say it realm by realm
        {
            ui.Text("AT WAR WITH: " + (foes.Count == 0 ? "NOBODY" : string.Join(", ", foes)) + (foes.Count > 0 ? $"   WEARINESS {C.Exhaustion(f):0}" : ""), rx, ry, Style.Dim, 1);
            ry += ui.U(14);
        }
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

    /// The realm whose choices are open in the king's list (one at a time), or -1.
    int diploOpen = -1;

    /// A king's row per other realm: its name and standing, and one button that opens the choices for that realm alone
    /// (offer terms in a war; declare war or ally in peace). Returns the new y.
    int KingDiplomacy(int rx, int ry, int bw, int f, List<int> others)
    {
        foreach (int g in others)
        {
            var wr = C.WarBetween(f, g);
            bool war = C.FactionsAtWar(f, g), open = diploOpen == g;
            string status = war ? $"AT WAR, STANDING {C.Margin(wr!, f, g):+0.0;-0.0}"
                : C.Allied(f, g) ? "ALLIED" : C.S.Factions[g].TributaryOf == f ? "YOUR TRIBUTARY"
                : wr != null && wr.TruceUntil > C.S.Hours ? $"TRUCE, {(wr.TruceUntil - (float)C.S.Hours) / 24:0} DAYS" : "AT PEACE";
            ui.Text(C.FactionNames[g].ToUpperInvariant(), rx, ry + ui.U(5), SideColor(g), 1);
            ui.Text(status, rx + ui.U(190), ry + ui.U(5), war ? Style.Bad : Style.Dim, 1);
            if (ui.Button(new Rectangle(rx + bw - ui.U(110), ry, ui.U(110), ui.U(18)), open ? "CLOSE" : war ? "SEEK PEACE" : "DEALINGS", true, "", open))
                diploOpen = open ? -1 : g;
            ry += ui.U(22);
            if (!open) continue;
            if (war) { ui.Text($"WEARINESS: THEM {C.ExhaustionOf(wr!, g):0}, YOU {C.ExhaustionOf(wr!, f):0}. OFFER PEACE ON:", rx + ui.U(10), ry, Style.Dim, 1); ry += ui.U(13); }
            var labels = war ? new[] { "TRUCE", "GOLD", "A SEAT", "TRIBUTE" } : new[] { "DECLARE WAR", "ALLIANCE" };
            int sw = (bw - ui.U(10) - ui.U(4) * (labels.Length - 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                var b = new Rectangle(rx + ui.U(10) + i * (sw + ui.U(4)), ry, sw, ui.U(22));
                if (ui.Button(b, labels[i]))
                {
                    note = war ? C.OfferPeace(g, (Terms)i) ?? "THEY ACCEPT" : (i == 0 ? C.DeclareWar(g) : C.ProposeAlliance(g)) ?? (i == 0 ? "WAR IS DECLARED" : "AN ALLIANCE IS SWORN");
                    diploOpen = -1;
                }
                if (war && ui.Hover(b)) note = $"OFFER PEACE ON {Campaign.TermsText((Terms)i)}";
            }
            ry += ui.U(30);
        }
        return ry;
    }
}
