using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the realm panel (R) and the vassals list.
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

    /// The career and realm screen (F5): the rank ladder on the left; the realm you serve (ruler, marshal, objective, the
    /// council) and the acts your rank allows on the right.
    void RealmPanel()
    {
        var r = Modal(1040, 620, "YOUR CAREER AND REALM");
        var rank = C.Rank;
        int x = r.X + ui.U(24), y = r.Y + ui.U(52), lw = ui.U(470);
        // The ladder.
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

        // The realm.
        int rx = x + lw + ui.U(24), ry = r.Y + ui.U(52), rw = r.Right - ui.U(24) - rx;
        int f = C.Captain.Contract;
        if (C.Realm is not FactionState fs)
        {
            ui.Text("YOU SERVE NO REALM.", rx, ry, Style.Text);
            ui.Text("SIGN WITH ONE AT A TOWN'S STEWARD (CONTRACT TAB).", rx, ry + ui.U(20), Style.Dim, 1);
        }
        else
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
            if (rank == Rank.King)
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
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE (R)")) panel = PanelKind.None;
    }
}
