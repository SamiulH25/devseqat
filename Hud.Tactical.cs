using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Unified Tactical HUD shared by on-horseback Command Mode (Left Ctrl) and high-altitude Command View (Tab) (Pillar 1 P1.2):
/// left-edge vertical company roster, bottom-left selected company card with formation/stance toggles, and bottom compact order bar.
sealed partial class Hud
{
    Rectangle selectedCard, compactBar;
    readonly List<(Rectangle R, Cmd C)> cardBtns = new();

    static string RoleIconOf(Squad s)
    {
        if (Battle.CasterCompany(s)) return "star";
        if (s.Cavalry) return "horse";
        if (s.Units.Count > 0 && s.Units[0].Type.Class == UnitClass.Archer) return "arrow";
        if (s.Units.Count > 0 && Look.GripOf(s.Units[0].Type.Look.Weapon) is Grip.Pole or Grip.Lance) return "spear";
        return "sword";
    }

    static (string Word, Color Col) RosterState(Squad s, int active, int routing, int morale)
    {
        if (active == 0) return ("LOST", Dim);
        if (routing * 2 > active) return ("ROUTING", Bad);
        if (morale < Battle.RoutAt) return ("BROKEN", Bad);
        if (morale < Battle.RallyAt) return ("WAVERING", Warn);
        if (s.InContact) return ("ENGAGING", Orange);
        if (s.Order == Order.Charge) return ("CHARGING", Orange);
        if (s.Order == Order.Follow) return ("FOLLOWING", Friendly);
        if (s.Rule == StandingRule.Skirmish) return ("SKIRMISH", Gold);
        bool moving = Vector2.DistanceSquared(s.Center, s.Anchor) > 9f;
        if (moving) return ("MOVING", Style.Text);
        return s.Order == Order.Hold ? ("HOLDING", Ok) : ("IDLE", Dim);
    }

    static string MoraleWord(int morale, bool routing) => routing ? "ROUTING" : morale >= 82 ? "INSPIRED" : morale >= 60 ? "CONFIDENT" : morale >= Battle.RallyAt ? "STEADY" : morale >= Battle.RoutAt ? "WAVERING" : "BROKEN";

    static string StanceWord(Squad s) => s.Rule switch
    {
        StandingRule.NoChase => "DEFENSIVE",
        StandingRule.ChargeWhenClose => "OFFENSIVE",
        StandingRule.Skirmish => "SKIRMISH",
        StandingRule.FallBackWhenWorn => "CAUTIOUS",
        _ => s.Order == Order.Charge ? "OFFENSIVE" : s.Formation is Formation.ShieldWall or Formation.Square ? "DEFENSIVE" : "NEUTRAL",
    };

    static string FormationIconOf(Formation f) => f switch
    {
        Formation.ShieldWall => "f_wall",
        Formation.Square => "f_square",
        Formation.Wedge => "f_wedge",
        Formation.Loose => "f_loose",
        _ => "f_line",
    };

    void LayoutRosterLeft(int maxBottom)
    {
        var mine = Mine;
        int rw = U(210), rh = U(28), gap = U(3);
        int x = U(10), y = U(14);
        int avail = Math.Max(rh, maxBottom - y);
        int fit = Math.Max(1, (avail + gap) / (rh + gap));
        int shown = Math.Min(mine.Count, fit);
        for (int i = 0; i < shown; i++)
            cards.Add((new Rectangle(x, y + i * (rh + gap), rw, rh), mine[i]));
        hiddenCards = mine.Count - shown;
    }

    void LayoutSelectedCard(int x, int bottomY)
    {
        cardBtns.Clear();
        selectedCard = Rectangle.Empty;
        if (Selected.Count == 0 && !FieldCommand) return;
        int cw = U(262), ch = U(82);
        selectedCard = new Rectangle(x, bottomY - ch, cw, ch);

        var who = Listening();
        if (who.Count == 0) return;
        // Formation toggle buttons inside the selected card's lower strip.
        int bx = selectedCard.X + U(62), by = selectedCard.Bottom - U(25), bw = U(30), bh = U(19), bg = U(3);
        for (int i = 0; i < ShapeRow.Length; i++)
        {
            var c = ShapeRow[i];
            var shape = ShapeOf(c)!.Value;
            bool ok = who.Any(s => s.Units.Count > 0 && Array.IndexOf(s.Shapes(s.Units[0].Type.HasShield), shape) >= 0);
            if (!ok) continue;
            var r = new Rectangle(bx, by, bw, bh);
            cardBtns.Add((r, c));
            buttons.Add((r, c));
            bx += bw + bg;
        }
        // Quick stance / standing-rule cycle button on the right of the formation row.
        if (!Deploying && bx + U(30) <= selectedCard.Right - U(6))
        {
            var sr = new Rectangle(selectedCard.Right - U(36), by, U(30), bh);
            cardBtns.Add((sr, Cmd.Rule));
            buttons.Add((sr, Cmd.Rule));
        }
    }

    static readonly Cmd[] CompactOrders = { Cmd.Charge, Cmd.Engage, Cmd.AttackMove, Cmd.Advance, Cmd.Hold, Cmd.Follow, Cmd.FallBack };

    void LayoutFieldCommand()
    {
        LayoutRosterLeft(H - U(104));
        LayoutSelectedCard(U(10), H - U(10));

        // Bottom-center / right compact 6-button order bar (+ Fire at Will if any listening company shoots).
        var who = Listening();
        var list = new List<Cmd>(CompactOrders);
        if (who.Any(Shoots)) list.Add(Cmd.FireAtWill);
        int bs = U(52), bh = U(44), gap = U(4);
        int totalW = list.Count * bs + (list.Count - 1) * gap;
        int minX = selectedCard.IsEmpty ? U(10) : selectedCard.Right + U(12);
        int x0 = Math.Max(minX, (W - totalW) / 2);
        if (x0 + totalW > W - U(12)) x0 = W - U(12) - totalW;
        int y0 = H - bh - U(12);
        compactBar = new Rectangle(x0 - U(6), y0 - U(6), totalW + U(12), bh + U(12));
        for (int i = 0; i < list.Count; i++)
            buttons.Add((new Rectangle(x0 + i * (bs + gap), y0, bs, bh), list[i]));
    }

    void DrawRosterLeft()
    {
        if (cards.Count == 0) return;
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int b = Math.Max(4, U(6));
        for (int ci = 0; ci < cards.Count; ci++)
        {
            var (r, s) = cards[ci];
            int active = 0, routing = 0;
            float moraleSum = 0;
            foreach (var u in s.Units) if (u.Active) { active++; if (u.Routing) routing++; moraleSum += u.Morale; }
            int morale = active == 0 ? 0 : (int)(moraleSum / active);
            var (state, sc) = RosterState(s, active, routing, morale);
            bool sel = Selected.Contains(s);
            bool hover = r.Contains(Mouse);

            // Forged-iron row plate with aged-brass accent when selected or hovered.
            chrome.Draw9(sb, r, UiChrome.PlateFill, 10, b, sel ? new Color(255, 240, 200) * 0.96f : Color.White * (hover ? 0.95f : 0.88f));
            chrome.Draw9(sb, r, UiChrome.PlateFrame, 10, b, sel ? Gold : hover ? Kit.Gilt * 0.85f : Kit.GiltDark * 0.7f);
            if (sel) Rect(new Rectangle(r.X + U(2), r.Y + U(3), U(3), r.Height - U(6)), Gold);
            if (active == 0) Rect(r, Color.Black * 0.45f);
            if (OrderFlash(s) is float fl and > 0) { Rect(r, Gold * (0.28f * fl)); Frame(r, Gold, U(1)); }

            // Number badge (1..9).
            int idx = ci + 1;
            var nb = new Rectangle(r.X + U(7), r.Y + U(5), U(14), U(15));
            Rect(nb, sel ? Gold : Color.Black * 0.65f);
            Frame(nb, sel ? Gold : Kit.GiltDark * 0.6f, U(1));
            TextC($"{idx}", nb.Center.X, nb.Y + U(4), sel ? Kit.IronDark : Color.White, 1);

            // Role icon + alive count + fitted short troop name + formation icon.
            var coHd = CompanyArms(s);
            kit.Icon(RoleIconOf(s), nb.Right + U(4), r.Y + U(6), active == 0 ? Dim : coHd.Ui, 2);
            Text($"{active}", nb.Right + U(20), r.Y + U(7), active == 0 ? Dim : Color.White, 1);

            string shortName = s.Name.ToUpperInvariant().Split(' ').Last();
            int nameX = nb.Right + U(40);
            int stateW = TW(state, 1);
            int nameRoom = Math.Max(U(24), r.Right - U(18) - stateW - nameX);
            Text(kit.Fit(shortName, nameRoom), nameX, r.Y + U(7), sel ? Kit.Parchment : Style.Text * 0.85f, 1);
            kit.Icon(FormationIconOf(s.Formation), r.Right - U(14) - stateW, r.Y + U(8), sel ? Gold : Dim, 1);

            // Live state word on right.
            TextR(state, r.Right - U(6), r.Y + U(7), sc, 1);

            // Slim bottom strength + morale hairline gauges.
            int gw = (r.Width - U(16)) / 2;
            Rect(new Rectangle(r.X + U(7), r.Bottom - U(5), gw, U(2)), Color.Black * 0.6f);
            Rect(new Rectangle(r.X + U(7), r.Bottom - U(5), (int)(gw * (active / (float)Math.Max(1, s.StartCount))), U(2)), coHd.Ui);
            Rect(new Rectangle(r.X + U(9) + gw, r.Bottom - U(5), gw, U(2)), Color.Black * 0.6f);
            Rect(new Rectangle(r.X + U(9) + gw, r.Bottom - U(5), (int)(gw * (morale / 100f)), U(2)), MoraleColor(morale));
        }
        if (hiddenCards > 0)
        {
            var last = cards[^1].R;
            Text($"+{hiddenCards} MORE", last.X + U(4), last.Bottom + U(4), Gold, 1);
        }
    }

    void DrawSelectedCard()
    {
        if (selectedCard.IsEmpty) return;
        var r = selectedCard;
        Panel(r, Selected.Count > 0 ? Gold : null, 1);
        var who = Listening();
        if (who.Count == 0)
        {
            Text("NO COMPANIES", r.X + U(10), r.Y + U(10), Dim, 1);
            return;
        }
        var lead = who[0];
        var coHd = CompanyArms(lead);

        // Left crest / portrait box.
        var port = new Rectangle(r.X + U(6), r.Y + U(6), U(50), r.Height - U(12));
        kit.VGrad(port, Kit.Mul(coHd.Cloth, 0.55f), Kit.IronDark, 2);
        var type = lead.Units.Count > 0 ? lead.Units[0].Type : UnitType.Shieldman;
        Icon(type, new Rectangle(port.X + U(4), port.Y + U(2), port.Width - U(8), port.Height - U(4)));
        Frame(port, Selected.Count > 0 ? Gold * 0.8f : Kit.GiltDark, U(1));

        // Title + headcount.
        int tx = port.Right + U(6), tw = r.Right - U(7) - tx;
        string title = Selected.Count == 0 ? "ALL COMPANIES"
            : who.Count == 1 ? (lead.Company?.Name is { Length: > 0 } cn ? cn : lead.Name).ToUpperInvariant()
            : $"{who.Count} COMPANIES";
        string cnt = $"{who.Sum(s => s.Alive)}/{who.Sum(s => s.StartCount)}";
        Text(kit.Fit(title, tw - TW(cnt, 1) - U(6)), tx, r.Y + U(6), Gold, 1);
        TextR(cnt, r.Right - U(7), r.Y + U(6), Kit.Parchment, 1);

        // Morale | Stance | Action strip.
        int active = 0, routing = 0; float mSum = 0;
        foreach (var s in who) foreach (var u in s.Units) if (u.Active) { active++; if (u.Routing) routing++; mSum += u.Morale; }
        int morale = active == 0 ? 0 : (int)(mSum / active);
        var (actWord, actCol) = RosterState(lead, lead.Alive, lead.Units.Count(u => u.Active && u.Routing), morale);
        string status = $"{MoraleWord(morale, routing * 2 > Math.Max(1, active))} | {StanceWord(lead)} | {actWord}";
        Text(kit.Fit(status, tw), tx, r.Y + U(18), actCol == Dim ? Style.Text : actCol, 1);

        // Morale bar + formation label.
        kit.Gauge(new Rectangle(tx, r.Y + U(30), tw, U(5)), morale / 100f, MoraleColor(morale));
        Text(FormationName(lead.Formation), tx, r.Y + U(39), Dim, 1);
        if (lead.Rule != StandingRule.None) TextR(Battle.RuleTag(lead.Rule), r.Right - U(7), r.Y + U(39), Gold, 1);

        // Formation + stance icon toggle buttons (forged dark-iron tinting).
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int bb = Math.Max(3, U(5));
        foreach (var (br, c) in cardBtns)
        {
            bool lit = Lit(c, who), hover = br.Contains(Mouse), locked = Unlearned(c, who) != null;
            var info = Info[c];
            var fill = locked ? new Color(26, 25, 29) : lit ? new Color(118, 90, 36) : hover ? new Color(82, 68, 42) : new Color(44, 42, 48);
            chrome.Draw9(sb, br, UiChrome.BtnFill, 8, bb, fill);
            chrome.Draw9(sb, br, UiChrome.BtnFrame, 8, bb, lit || hover ? Gold : Kit.GiltDark * 0.85f);
            var ic = locked ? Dim * 0.6f : lit ? Color.White : hover ? Kit.Parchment : Style.Text;
            kit.Icon(info.Icon, br.X + (br.Width - U(14)) / 2, br.Y + (br.Height - U(14)) / 2, ic, 2);
        }
    }

    void DrawCompactOrderBar()
    {
        if (compactBar.IsEmpty) return;
        Panel(compactBar);
        var who = Listening();
        var chrome = UiChrome.For(sb.GraphicsDevice);
        int bb = Math.Max(4, U(6));
        Cmd? over = null;
        foreach (var (r, c) in buttons)
        {
            if (!compactBar.Contains(r.Center)) continue;
            bool lit = Lit(c, who), hover = r.Contains(Mouse);
            if (hover) over = c;
            var info = Info[c];
            var fill = lit ? new Color(118, 90, 36) : hover ? new Color(82, 68, 42) : new Color(44, 42, 48);
            chrome.Draw9(sb, r, UiChrome.BtnFill, 8, bb, fill);
            chrome.Draw9(sb, r, UiChrome.BtnFrame, 8, bb, lit || hover ? Gold : Kit.GiltDark * 0.85f);
            var col = lit ? Color.White : hover ? Kit.Parchment : Style.Text;
            kit.Icon(info.Icon, r.X + (r.Width - U(21)) / 2, r.Y + U(5), col, 3);
            TextC(kit.Fit(info.Short, r.Width - U(4)), r.Center.X, r.Bottom - U(12), col, 1);
            if (info.Key is Act k && Ctl.First(k) is { Length: > 0 and <= 2 } key)
                TextR(key, r.Right - U(4), r.Y + U(3), lit ? Kit.Parchment : hover ? Gold : Dim, 1);
        }
        if (over is Cmd oc)
        {
            var oi = Info[oc];
            int hw = Math.Max(compactBar.Width, TW(oi.Tip, 1) + U(18));
            var hr = new Rectangle(Math.Clamp(compactBar.Center.X - hw / 2, U(8), W - hw - U(8)), compactBar.Y - U(24), hw, U(18));
            Panel(hr, Gold);
            TextC($"{oi.Name}: {oi.Tip}", hr.Center.X, hr.Y + U(5), Color.White, 1);
        }
    }
}
