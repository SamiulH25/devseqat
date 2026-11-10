using Microsoft.Xna.Framework;

namespace Ironfield;

/// The talk screen (docs/archive/LORDS.md LD3): his face large, what you know of him, what he says, and the topics you can raise,
/// each with its odds; checked topics take up to three arguments before you make your case.
sealed partial class MapScreen
{
    Character? talkWith;
    string talkTopic = "", talkSaid = "";
    readonly List<string> talkArgs = new();
    PanelKind talkBack = PanelKind.None;

    /// Open the talk screen with him; `back` is the panel LEAVE returns to.
    void OpenTalk(Character ch, PanelKind back)
    {
        talkWith = ch;
        talkTopic = "";
        talkArgs.Clear();
        talkSaid = C.Greeting(ch);
        talkBack = back;
        panel = PanelKind.Talk;
        note = "";
    }

    void LeaveTalk()
    {
        talkWith = null;
        if (talkBack == PanelKind.Encounter && C.Encounter is Party e)
        {
            if (C.Meeting(e)) { C.Part(); panel = PanelKind.None; }
            else panel = PanelKind.Encounter;
        }
        else panel = talkBack == PanelKind.Encounter ? PanelKind.None : talkBack;
        note = "";
    }

    void TalkPanel()
    {
        if (talkWith is not Character ch || ch.Dead) { LeaveTalk(); return; }
        var r = Modal(920, 520, "TALK", Kit.Gilt);
        int lx = r.X + ui.U(24), ly = r.Y + ui.U(46), lw = ui.U(220);
        // Left: who he is.
        ui.Face(PortraitPainter.Of(C, ch), new Rectangle(lx + (lw - ui.U(192)) / 2, ly, ui.U(192), ui.U(240)));
        int y = ly + ui.U(248);
        ui.Text(ui.Fit(ch.Name.ToUpperInvariant(), lw), lx, y, Style.Text, 1); y += ui.U(13);
        string house = C.HouseOf(ch) is House h ? h.Name.ToUpperInvariant() : "";
        string realm = ch.Faction >= 0 && ch.Faction < C.FactionNames.Length ? C.FactionNames[ch.Faction].ToUpperInvariant() : "";
        ui.Text(ui.Fit((house != "" ? house + ", " : "") + realm, lw), lx, y, Style.Dim, 1); y += ui.U(13);
        ui.Text(ui.Fit(string.Join(", ", ch.Traits).ToUpperInvariant() + $"  AGE {ch.Age}", lw), lx, y, Style.Dim, 1); y += ui.U(15);
        float op = C.OpinionOfYou(ch);
        ui.Text($"THINKS {OpinionWord(op)} OF YOU ({op:+0;-0;0})", lx, y, OpinionColor(op), 1); y += ui.U(13);
        foreach (var m in C.MemoriesOf(ch, C.Me).Take(2))
        {
            ui.Text(ui.Fit($"{m.What.ToUpperInvariant()} ({m.Weight:+0;-0})", lw), lx + ui.U(6), y, Style.Dim, 1); y += ui.U(12);
        }
        string fam = C.FamilyLine(ch), bonds = C.BondsLine(ch);
        foreach (var line in new[] { fam, bonds, ch.WantKnown && ch.Want != "" ? "WANTS: " + C.WantText(ch) : "" }.Where(l => l != ""))
            foreach (var w in ui.Wrap(line.ToUpperInvariant(), lw).Take(2)) { if (y > r.Bottom - ui.U(70)) break; ui.Text(w, lx, y, Kit.Gilt, 1); y += ui.U(12); }

        // Right: what he says, and the topics.
        int rx = lx + lw + ui.U(24), rw = r.Right - ui.U(24) - rx, ry = ly;
        ui.Rect(new Rectangle(rx, ry, rw, ui.U(64)), Color.White * 0.04f);
        int ty = ry + ui.U(8);
        foreach (var w in ui.Wrap("\"" + talkSaid + "\"", rw - ui.U(16), 2).Take(3)) { ui.Text(w, rx + ui.U(8), ty, Style.Text, 2); ty += ui.U(18); }
        ry += ui.U(76);
        string group = "";
        int bx = rx, bw = (rw - ui.U(16)) / 3, bh = ui.U(24);
        foreach (var t in C.TopicsFor(ch))
        {
            if (t.Group != group)
            {
                if (group != "") { ry += bh + ui.U(6); bx = rx; }
                group = t.Group;
                ui.Text(group, rx, ry, Style.Dim, 1);
                ry += ui.U(12);
            }
            if (bx + bw > rx + rw + 1) { bx = rx; ry += bh + ui.U(4); }
            var b = new Rectangle(bx, ry, bw, bh);
            string? why = C.CannotAsk(ch, t.Id);
            if (ui.Button(b, t.Label, why == null, "", talkTopic == t.Id))
            {
                if (C.Odds(ch, t.Id) < 1) { talkTopic = t.Id; talkArgs.Clear(); }
                else Speak(ch, t.Id);
            }
            if (ui.Hover(b)) note = (why ?? t.Text).ToUpperInvariant();
            bx += bw + ui.U(8);
        }
        ry += bh + ui.U(12);
        if (talkTopic != "" && C.CannotAsk(ch, talkTopic) == null) Arguments(ch, rx, ry, rw);
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "LEAVE")) LeaveTalk();
    }

    /// The case for a checked topic: argument chips (each shows what it adds), the odds, and MAKE YOUR CASE.
    void Arguments(Character ch, int x, int y, int w)
    {
        ui.Text($"{Campaign.TopicOf(talkTopic).Label}: BRING UP TO THREE ARGUMENTS", x, y, Kit.Gilt, 1);
        y += ui.U(14);
        int cw = (w - ui.U(5 * 6)) / 6;
        for (int i = 0; i < Campaign.Arguments.Length; i++)
        {
            string a = Campaign.Arguments[i];
            float add = C.ArgumentWeight(ch, talkTopic, a);
            bool on = talkArgs.Contains(a), can = on || talkArgs.Count < 3;
            if (a == "promise" && !(ch.WantKnown && ch.Want != "" && ch.Promise == "")) can = false;
            if (a == "gold" && C.S.Gold < 100) can = false;
            var b = new Rectangle(x + i * (cw + ui.U(6)), y, cw, ui.U(22));
            if (ui.Button(b, a.ToUpperInvariant(), can, "", on)) { if (on) talkArgs.Remove(a); else talkArgs.Add(a); }
            if (ui.Hover(b)) note = a switch
            {
                "gold" => "OFFER 100 CROWNS. SPENT WHATEVER HE SAYS.",
                "promise" => "PROMISE TO HELP WHAT HE WANTS. BREAK IT AND HE WILL KNOW.",
                "fear" => "REMIND HIM HOW STRONG YOU ARE.",
                _ => $"APPEAL TO HIS {a.ToUpperInvariant()}.",
            } + $" ({add * 100:+0;-0;0}%)";
        }
        y += ui.U(30);
        float odds = C.Odds(ch, talkTopic, talkArgs);
        ui.Text($"CHANCE: {odds * 100:0}%", x, y + ui.U(6), odds >= 0.6f ? Style.Ok : odds >= 0.3f ? Style.Warn : Style.Bad, 2);
        if (ui.Button(new Rectangle(x + w - ui.U(180), y, ui.U(180), ui.U(28)), "MAKE YOUR CASE")) Speak(ch, talkTopic);
    }

    void Speak(Character ch, string topic)
    {
        var (ok, line) = C.Say(ch, topic, talkArgs.ToList());
        talkSaid = line == "" ? (ok ? "Very well." : "No.") : line;
        talkTopic = "";
        talkArgs.Clear();
        note = ok ? "" : "HE REFUSES.";
        // A threat that worked, or peace made, ends the meeting.
        if (C.Encounter == null && talkBack == PanelKind.Encounter) { talkBack = PanelKind.None; }
        else if (C.Encounter is Party e && !C.Hostile(C.Player, e) && !C.Meeting(e)) { C.Part(); talkBack = PanelKind.None; }
    }

    /// A town's or castle's HALL (LD5): who is there in a row of portraits, the holder first, each with TALK; in your own
    /// hall, a feast for the lords around.
    void HallTab(Settlement s, Rectangle body)
    {
        var folk = C.HallFolk(s);
        int x = body.X, y = body.Y, cw = ui.U(112), ch = ui.U(196);
        string holder = s.Owner == C.Player.Id ? "YOUR HALL" : C.HolderOf(s) is Character h ? $"THE HALL OF {h.Name.ToUpperInvariant()}" : "THE REALM'S HALL";
        ui.Text(holder, x, y, Kit.Gilt, 1);
        y += ui.U(16);
        if (folk.Count == 0) { ui.Text("THE HALL IS EMPTY: ITS LORDS ARE IN THE FIELD.", x, y + ui.U(4), Style.Dim, 1); y += ui.U(24); }
        foreach (var p in folk)
        {
            if (x + cw > body.Right) break;
            var card = new Rectangle(x, y, cw - ui.U(8), ch);
            TownRow(card);
            ui.Face(PortraitPainter.Of(C, p), new Rectangle(card.X + ui.U(4), card.Y + ui.U(4), card.Width - ui.U(8), ui.U(120)));
            ui.Text(ui.Fit(p.Name.ToUpperInvariant(), card.Width - ui.U(8)), card.X + ui.U(4), card.Y + ui.U(130), Style.Text, 1);
            float op = C.OpinionOfYou(p);
            ui.Text(p.Kind == "kin" ? ui.Fit(C.HouseOf(p)?.Name.ToUpperInvariant() ?? "", card.Width - ui.U(8)) : $"{p.Age}  {OpinionWord(op)}", card.X + ui.U(4), card.Y + ui.U(143), p.Kind == "kin" ? Style.Dim : OpinionColor(op), 1);
            var tb = new Rectangle(card.X + ui.U(4), card.Bottom - ui.U(28), card.Width - ui.U(8), ui.U(24));
            string? why = C.CannotTalk(p);
            if (ui.Button(tb, "TALK", why == null)) { OpenTalk(p, PanelKind.Settlement); return; }
            if (ui.Hover(card)) note = (C.FamilyLine(p) is { Length: > 0 } f ? f + ". " : "").ToUpperInvariant() + (C.Suitor(p) ? "UNWED: YOU MAY COURT HER." : "");
            x += cw;
        }
        if (s.Owner != C.Player.Id) return;
        // A feast for the lords around (LD5).
        var row = new Rectangle(body.X, body.Bottom - ui.U(38), body.Width, ui.U(34));
        TownRow(row);
        var guests = C.FeastGuests(s);
        ui.Text(ui.Fit($"FEAST THE LORDS: {guests.Count} WOULD COME. THEY THINK BETTER OF YOU; RIVALS AT ONE TABLE MAY QUARREL.", row.Width - ui.U(220)), row.X + ui.U(8), row.Y + ui.U(12), Kit.Parchment, 1);
        string? cant = C.CannotHoldFeast(s);
        var fb = new Rectangle(row.Right - ui.U(200), row.Y + ui.U(5), ui.U(194), ui.U(24));
        if (ui.Button(fb, "HOLD A FEAST", cant == null, $"{C.HallFeastPrice(s)} CROWNS")) note = C.HoldFeast(s) ?? "THE LORDS FEAST IN YOUR HALL";
        if (cant != null && ui.Hover(fb)) note = cant;
    }
}
