using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: the party screen (P) as an order of battle. Each division is a lane under its sergeant (your retinue first)
/// with its companies as tiles: drag a tile to another lane, or pick it and click the lane's open place, to move it; drop it
/// on the last lane to make a division under its sergeant. The company picked is told in full down the right, with its
/// troops, and a count of men to send to another company or split off. Drawing and input only.
partial class MapScreen
{
    /// The company picked on the board (click its tile): the panel on the right is about it.
    CompanyRecord? musterPick;
    /// The troop of the picked company whose men SEND moves, and how many of them.
    string musterStack = "";
    int musterSend = 10;
    /// The side panel's tab: 0 the company picked, 1 the captain.
    int musterTab;
    /// The company being dragged (its id; -1 none), where the drag began, and whether the mouse has moved far enough to be one.
    int dragCo = -1;
    Point dragFrom;
    bool dragging;

    static readonly (string Role, string Label, Color Col)[] RoleLook =
    {
        ("infantry", "FOOT", Style.Sides[2].Ui), ("spears", "SPEARS", Style.Friendly), ("archers", "BOWS", Style.Ok), ("horse", "HORSE", Style.Orange), ("casters", "MAGES", new Color(190, 140, 230)),
    };
    static Color RoleColor(string role) => RoleLook.FirstOrDefault(r => r.Role == role) is { Role: not null } r ? r.Col : Style.Dim;

    void PartyPanel()
    {
        var list = C.Player.Companies;
        var r = Modal(1240, 680, "YOUR COMPANY");
        if (musterPick == null || !list.Contains(musterPick)) musterPick = list.FirstOrDefault();
        int sideW = ui.U(372);
        var board = new Rectangle(r.X + ui.U(18), r.Y + ui.U(28), r.Width - sideW - ui.U(48), r.Height - ui.U(84));
        var side = new Rectangle(board.Right + ui.U(12), board.Y, sideW, board.Height);
        bool released = dragCo >= 0 && ms.LeftButton == ButtonState.Released;
        if (dragCo >= 0 && !dragging && Math.Abs(ui.Mouse.X - dragFrom.X) + Math.Abs(ui.Mouse.Y - dragFrom.Y) > ui.U(6)) dragging = true;
        var moving = dragging ? list.FirstOrDefault(c => c.Id == dragCo) : null;

        int y = BoardSummary(new Rectangle(board.X, board.Y, board.Width, ui.U(46)));
        // The lanes: your retinue, then a lane a division; companies whose division is gone count as your retinue.
        var divs = C.S.Divisions.ToList();
        var lanes = new List<(DivisionRecord? D, List<CompanyRecord> Comps)> { (null, list.Where(c => divs.All(v => v.Id != c.Division)).ToList()) };
        foreach (var d in divs) lanes.Add((d, list.Where(c => c.Division == d.Id).ToList()));
        int headW = ui.U(164), gap = ui.U(6), tileW = ui.U(156); // wide enough for a full company name (25 letters)
        int perRow = Math.Max(1, (board.Width - headW - ui.U(14) + gap) / (tileW + gap));
        bool newLane = divs.Count < Campaign.MaxDivisions;
        int avail = board.Bottom - y - (newLane ? ui.U(50) : 0);
        int LaneH(DivisionRecord? d, int tiles, int th) => Math.Max(ui.U(d == null ? 48 : 72), Math.Max(1, (tiles + perRow - 1) / perRow) * (th + gap) + gap);
        // A place to put the picked company shows in every lane it is not in (one tile more).
        int Tiles((DivisionRecord? D, List<CompanyRecord> Comps) l) => l.Comps.Count + (musterPick != null && !l.Comps.Contains(musterPick) ? 1 : 0);
        int tileH = ui.U(66);
        if (lanes.Sum(l => LaneH(l.D, Tiles(l), tileH) + gap) > avail) tileH = ui.U(48);
        for (int i = 0; i < lanes.Count; i++)
        {
            var (d, comps) = lanes[i];
            var lane = new Rectangle(board.X, y, board.Width, LaneH(d, Tiles(lanes[i]), tileH));
            DrawLane(lane, d, comps, headW, tileW, tileH, perRow, moving, released);
            y = lane.Bottom + gap;
        }
        if (newLane && y + ui.U(40) <= board.Bottom) { NewLane(new Rectangle(board.X, y, board.Width, ui.U(42)), headW, moving, released); y += ui.U(42) + gap; }
        if (C.S.LastFight is LastFight lf && board.Bottom - y >= ui.U(110)) LastFightBlock(new Rectangle(board.X, y + ui.U(8), board.Width, board.Bottom - y - ui.U(8)), lf);

        // The side: the company picked, or the captain.
        ui.Rect(side, Color.Black * 0.22f);
        ui.Frame(side, Color.White * 0.06f, ui.U(1));
        int tabPick = ui.WordTabs(side.X + ui.U(12), side.Y + ui.U(8), new[] { "THE COMPANY PICKED", "THE CAPTAIN" }, musterTab);
        if (tabPick >= 0) musterTab = tabPick;
        var inner = new Rectangle(side.X, side.Y + ui.U(26), side.Width, side.Height - ui.U(26));
        if (musterTab == 1) MusterSide(inner);
        else if (musterPick != null) CompanyDetail(inner, musterPick);

        // The company in the hand.
        if (moving != null)
        {
            var g = new Rectangle(ui.Mouse.X + ui.U(10), ui.Mouse.Y + ui.U(6), ui.TW(moving.Name.ToUpperInvariant(), 1) + ui.U(34), ui.U(22));
            ui.Rect(g, Kit.IronDark * 0.92f);
            ui.Frame(g, Kit.Gilt, ui.U(1));
            ui.Icon("flag", g.X + ui.U(5), g.Y + ui.U(7), Kit.Gilt, 1);
            ui.Text(moving.Name.ToUpperInvariant(), g.X + ui.U(18), g.Y + ui.U(7), Style.Text, 1);
        }
        if (ms.LeftButton == ButtonState.Released) { dragCo = -1; dragging = false; }

        if (note != "") ui.TextFitC(note, board.Center.X, r.Bottom - ui.U(46), board.Width, Style.Warn, 1);
        else ui.TextC("DRAG A COMPANY TO ANOTHER LANE TO CHANGE WHO LEADS IT. CLICK ONE TO SEE IT AND MOVE ITS MEN.", board.Center.X, r.Bottom - ui.U(46), Style.Dim, 1);
        if (ui.Gilded(new Rectangle(r.Right - ui.U(200), r.Bottom - ui.U(40), ui.U(180), ui.U(28)), "CLOSE", ctl.Name(Act.Menu))) { panel = PanelKind.None; renaming = null; }
    }

    /// The board's lower half (docs/pre-demo/COMPANY.md C8): your last battle, and a card for each named man it touched.
    void LastFightBlock(Rectangle b, LastFight lf)
    {
        string head = "THE LAST BATTLE" + (lf.Field != "" ? ": " + lf.Field.ToUpperInvariant() : "");
        ui.Text(head, b.X, b.Y, Kit.Gilt, 1);
        int hx = b.X + ui.TW(head, 1) + ui.U(10);
        hx += ui.Chip(hx, b.Y - ui.U(3), lf.Won ? "WON" : "LOST", lf.Won ? Style.Ok : Style.Bad) + ui.U(8);
        ui.TextFit($"DAY {lf.Day}, AGAINST {lf.Foe.ToUpperInvariant()}. {lf.Kills} KILLED, {lf.Lost} LOST.", hx, b.Y, b.Right - hx, Style.Dim, 1);
        ui.Divider(b.X, b.Y + ui.U(14), b.Width, Kit.Gilt * 0.35f);
        var men = lf.Men.Select(x => (Man: C.ManOfLastFight(x.Id), x.What)).Where(x => x.Man != null).ToList();
        int y = b.Y + ui.U(22);
        if (men.Count == 0) { ui.Text("NO NAMED MAN OF YOURS DID ANYTHING TO TELL OF.", b.X, y, Style.Dim, 1); return; }
        int cols = 3, gap = ui.U(8), cw = (b.Width - gap * (cols - 1)) / cols, ch = ui.U(58);
        for (int i = 0; i < men.Count; i++)
        {
            var card = new Rectangle(b.X + i % cols * (cw + gap), y + i / cols * (ch + gap), cw, ch);
            if (card.Bottom > b.Bottom)
            {
                ui.TextR($"+{men.Count - i} MORE", b.Right, b.Bottom - ui.U(10), Style.Dim, 1);
                break;
            }
            var (m, what) = men[i];
            bool gone = m!.Fate is "fell" or "missing";
            var k = C.Player.Companies.FirstOrDefault(c => c.Roll.Contains(m) || c.Id == m.Company);
            ui.Rect(card, Color.White * 0.035f);
            var fb = new Rectangle(card.X + ui.U(4), card.Y + ui.U(4), ui.U(40), card.Height - ui.U(8));
            ui.Rect(new Rectangle(fb.X - 1, fb.Y - 1, fb.Width + 2, fb.Height + 2), gone ? Kit.IronDark : Campaign.OfBanner(m) ? Kit.Mul(Kit.Gilt, 0.7f) : Kit.IronDark);
            if (gone) ui.FaceHead(PortraitPainter.Of(m, k) with { Mood = 1 }, fb, new Color(120, 122, 128));
            else ui.FaceHead(PortraitPainter.Of(m, k), fb);
            int tx = fb.Right + ui.U(8), tw = card.Right - tx - ui.U(4);
            ui.TextFit(m.Name.ToUpperInvariant(), tx, card.Y + ui.U(6), tw, gone ? Style.Dim : Style.Text, 1);
            var lines = ui.Wrap(what.ToUpperInvariant(), tw);
            Color wc = gone ? Style.Bad : what.StartsWith("Wounded") ? Style.Warn : Kit.Mul(Kit.Parchment, 0.85f);
            for (int l = 0; l < Math.Min(2, lines.Count); l++) ui.TextFit(lines[l], tx, card.Y + ui.U(20 + l * 12), tw, wc, 1);
            if (k != null && !gone && ui.Clicked(card)) { musterPick = k; note = ""; }
        }
    }

    /// The whole company in one strip: men against the cap, companies against the slots, wounded, horses, and the men by kind.
    int BoardSummary(Rectangle box)
    {
        var list = C.Player.Companies;
        var cap = C.Captain;
        var c = Card(box, Kit.Gilt);
        int limit = Math.Max(1, Rules.MenLimit(cap)), men = C.Player.Men, slots = Math.Max(list.Count, Rules.CompanySlots(cap));
        c = new Rectangle(c.X, box.Y + ui.U(6), c.Width, box.Height - ui.U(12));
        Stat(c.X, c.Y, "helm", $"{men}/{limit}", "MEN", men > limit * 0.95f ? Style.Warn : Style.Text);
        Stat(c.X + ui.U(116), c.Y, "flag", $"{list.Count}/{slots}", "COMPANIES", Style.Text);
        Stat(c.X + ui.U(212), c.Y, "heart", $"{C.Player.Wounded}", "WOUNDED", C.Player.Wounded > 0 ? Style.Warn : Style.Dim);
        Stat(c.X + ui.U(300), c.Y, "horse", $"{C.Player.Horses}", "HORSES", Style.Text);
        int bx = c.X + ui.U(390), bw = c.Right - bx, total = Math.Max(1, list.Sum(k => k.Men)), x = bx, lx = bx;
        foreach (var (role, label, col) in RoleLook)
        {
            int n = list.Where(k => Rules.RoleOf(k) == role).Sum(k => k.Men), w = bw * n / total;
            if (w > ui.U(2)) ui.Gauge(new Rectangle(x, c.Y + ui.U(2), w - ui.U(2), ui.U(10)), 1, col);
            x += w;
            if (n == 0) continue;
            ui.Rect(new Rectangle(lx, c.Y + ui.U(21), ui.U(5), ui.U(5)), col);
            ui.Text($"{label} {n}", lx + ui.U(8), c.Y + ui.U(20), Style.Text, 1);
            lx += ui.U(18) + ui.TW($"{label} {n}", 1);
        }
        return box.Bottom + ui.U(8);
    }

    /// One lane: who leads it on the left (a division's temper, command, opening goal and DISBAND), its companies as tiles,
    /// and a dashed place for the picked company when it stands elsewhere. A company dropped on the lane joins it.
    void DrawLane(Rectangle lane, DivisionRecord? d, List<CompanyRecord> comps, int headW, int tileW, int tileH, int perRow, CompanyRecord? moving, bool released)
    {
        bool drop = moving != null && !comps.Contains(moving) && ui.Hover(lane);
        ui.Rect(lane, Color.White * (drop ? 0.1f : 0.035f));
        ui.Frame(lane, drop ? Kit.Gilt : Color.White * 0.07f, ui.U(drop ? 2 : 1));
        ui.Rect(new Rectangle(lane.X, lane.Y, ui.U(3), lane.Height), d == null ? Kit.Gilt : Style.Friendly);
        int hx = lane.X + ui.U(10), hy = lane.Y + ui.U(7), hw = headW - ui.U(16);
        bool acted = false;
        if (d == null)
        {
            ui.Text("YOUR RETINUE", hx, hy, Kit.Gilt, 1);
            ui.Text("THEY OBEY YOU AT ONCE", hx, hy + ui.U(13), Style.Dim, 1);
            ui.Text($"{comps.Count} COMPANIES . {comps.Sum(c => c.Men)} MEN", hx, hy + ui.U(25), Style.Dim, 1);
        }
        else
        {
            ui.TextFit(C.DivisionName(d).ToUpperInvariant(), hx, hy, hw - ui.U(20), Style.Accent, 1);
            var x = new Rectangle(hx + hw - ui.U(16), hy - ui.U(3), ui.U(16), ui.U(14));
            if (ui.Button(x, "X")) { C.DisbandDivision(d); acted = true; }
            if (ui.Hover(x)) note = "DISBAND THIS DIVISION: ITS COMPANIES COME BACK TO YOUR RETINUE.";
            string temper = C.TemperOf(d).ToUpperInvariant();
            int cw = ui.Chip(hx, hy + ui.U(14), temper, Style.Text, temper == "BOLD" ? "fire" : temper == "CAUTIOUS" ? "shield" : "flag");
            if (ui.Hover(new Rectangle(hx, hy + ui.U(14), cw, ui.U(13)))) note = temper switch { "BOLD" => "BOLD: CHARGES EARLY AND CHASES FAR.", "CAUTIOUS" => "CAUTIOUS: KEEPS A SHORT LEASH AND LETS THE ENEMY COME.", _ => "STEADY: KEEPS FORMATION AND WAITS FOR CONTACT." };
            int cmd = C.CommandOf(d);
            var pr = new Rectangle(hx + cw + ui.U(8), hy + ui.U(17), ui.U(42), ui.U(7));
            ui.Pips(pr, cmd / 6f, Kit.Gilt, 6);
            ui.Text($"{cmd}", pr.Right + ui.U(4), hy + ui.U(17), Style.Text, 1);
            if (ui.Hover(new Rectangle(pr.X, pr.Y - ui.U(3), pr.Width + ui.U(14), ui.U(13)))) note = $"COMMAND {cmd} OF 6: HE IS QUICKER TO ACT AND TO HEAR YOUR ORDERS.";
            // His post and his stance when the battle opens (OF6), each a button that steps to the next.
            if (PostButtons(d, new Rectangle(hx, hy + ui.U(41), hw, ui.U(20)), null, captions: true)) acted = true; // no arrows: room for RIGHT WING
        }
        ui.Rect(new Rectangle(lane.X + headW, lane.Y + ui.U(6), ui.U(1), lane.Height - ui.U(12)), Color.White * 0.08f);
        int gap = ui.U(6), x0 = lane.X + headW + ui.U(8), i = 0;
        var leader = d == null ? null : C.LeaderCompanyOf(d);
        foreach (var c in comps)
        {
            CompanyTile(c, new Rectangle(x0 + i % perRow * (tileW + gap), lane.Y + gap + i / perRow * (tileH + gap), tileW, tileH), c == leader);
            i++;
        }
        if (comps.Count == 0 && (musterPick == null || moving != null)) ui.Text(d == null ? "EVERY COMPANY HAS A SERGEANT OVER IT." : "NO COMPANIES YET.", x0, lane.Y + ui.U(14), Style.Dim, 1);
        // The open place: click it to put the picked company here.
        if (musterPick is CompanyRecord pick && !comps.Contains(pick) && moving == null)
        {
            var slot = new Rectangle(x0 + i % perRow * (tileW + gap), lane.Y + gap + i / perRow * (tileH + gap), tileW, tileH);
            bool h = ui.Hover(slot);
            Dashed(slot, h ? Kit.Gilt : Color.White * 0.22f);
            if (h) { ui.Rect(slot, Kit.Gilt * 0.1f); Ui.Hovered(slot); note = $"PUT {pick.Name.ToUpperInvariant()} {(d == null ? "IN YOUR RETINUE" : "UNDER " + C.DivisionName(d).ToUpperInvariant())}."; }
            ui.TextC("PUT HERE", slot.Center.X, slot.Center.Y - ui.U(9), h ? Kit.Gilt : Style.Dim, 1);
            ui.TextFitC(pick.Name.ToUpperInvariant(), slot.Center.X, slot.Center.Y + ui.U(2), slot.Width - ui.U(8), h ? Style.Text : Kit.Mul(Style.Dim, 0.8f), 1);
            if (ui.Clicked(slot)) { note = C.AssignDivision(pick, d?.Id ?? -1) ?? ""; acted = true; }
        }
        if (!acted && drop && released) note = C.AssignDivision(moving!, d?.Id ?? -1) ?? "";
    }

    /// The last lane: where a new division is made. Drop a company on it and its sergeant takes command; or name a free
    /// companion to lead one.
    void NewLane(Rectangle lane, int headW, CompanyRecord? moving, bool released)
    {
        bool drop = moving != null && ui.Hover(lane);
        if (drop) ui.Rect(lane, Kit.Gilt * 0.1f);
        Dashed(lane, drop ? Kit.Gilt : Color.White * 0.2f);
        int hx = lane.X + ui.U(10);
        ui.Text("NEW DIVISION", hx, lane.Y + ui.U(8), Kit.Gilt, 1);
        ui.Text($"{C.S.Divisions.Count} OF {Campaign.MaxDivisions}", hx, lane.Y + ui.U(21), Style.Dim, 1);
        var leaders = C.DivisionLeaders().ToList();
        int x = lane.X + headW + ui.U(8), bh = ui.U(24), by = lane.Center.Y - bh / 2;
        // The picked company's own sergeant, then any companion free to lead.
        if (musterPick is CompanyRecord pick && leaders.Any(l => l.Company == pick.Id))
        {
            string sgt = pick.Officer.ToUpperInvariant(); // the officer's name may carry his rank already
            string label = ui.Fit("UNDER " + (sgt.StartsWith("SGT ") ? sgt : "SGT " + sgt), ui.U(190));
            var b = new Rectangle(x, by, ui.TW(label, 1) + ui.U(20), bh);
            if (ui.Button(b, label, true, "", true)) note = C.MakeDivision(-1, pick.Id) ?? "";
            if (ui.Hover(b)) note = $"{pick.Name.ToUpperInvariant()} BECOMES A DIVISION UNDER ITS OWN SERGEANT. HIS COMMAND GROWS WITH HIS BATTLES (AT MOST 6).";
            x = b.Right + ui.U(6);
        }
        foreach (var (name, chr, _) in leaders.Where(l => l.Char >= 0))
        {
            string label = ui.Fit("UNDER " + name.ToUpperInvariant(), ui.U(170));
            var b = new Rectangle(x, by, ui.TW(label, 1) + ui.U(20), bh);
            if (b.Right > lane.Right - ui.U(8)) break;
            if (ui.Button(b, label)) note = C.MakeDivision(chr, -1) ?? "";
            if (ui.Hover(b)) note = "A COMPANION: HE GROWS BETTER AT COMMAND WITH EVERY TWO BATTLES.";
            x = b.Right + ui.U(6);
        }
        if (x + ui.U(260) < lane.Right) ui.TextFit(leaders.Count == 0 ? "NOBODY IS FREE TO LEAD ONE: COMPANIONS AND COMPANY SERGEANTS CAN." : "OR DRAG A COMPANY HERE: ITS SERGEANT TAKES COMMAND.", x + ui.U(6), lane.Center.Y - ui.U(3), lane.Right - x - ui.U(14), Style.Dim, 1);
        if (drop && released) note = leaders.Any(l => l.Company == moving!.Id) ? C.MakeDivision(-1, moving!.Id) ?? "" : moving!.Officer == "" ? "IT HAS NO SERGEANT TO LEAD A DIVISION" : "ITS SERGEANT LEADS A DIVISION ALREADY";
    }

    /// A dashed outline: an empty place.
    void Dashed(Rectangle r, Color c)
    {
        for (int x = r.X; x < r.Right; x += ui.U(8)) { int w = Math.Min(ui.U(4), r.Right - x); ui.Rect(new Rectangle(x, r.Y, w, ui.U(1)), c); ui.Rect(new Rectangle(x, r.Bottom - ui.U(1), w, ui.U(1)), c); }
        for (int y = r.Y; y < r.Bottom; y += ui.U(8)) { int h = Math.Min(ui.U(4), r.Bottom - y); ui.Rect(new Rectangle(r.X, y, ui.U(1), h), c); ui.Rect(new Rectangle(r.Right - ui.U(1), y, ui.U(1), h), c); }
    }

    /// One company as a tile: its man, name, men (and wounded), how full it is, its cohesion, men ready to promote; a crown
    /// if its sergeant leads the lane. A press picks it and begins a drag.
    void CompanyTile(CompanyRecord c, Rectangle r, bool leads)
    {
        bool picked = musterPick == c, ready = c.Ready > 0, hover = ui.Hover(r), held = dragging && dragCo == c.Id, small = r.Height < ui.U(60);
        string role = Rules.RoleOf(c);
        ui.Rect(new Rectangle(r.X + ui.U(1), r.Y + ui.U(2), r.Width, r.Height), Color.Black * 0.35f);
        ui.VGrad(r, picked ? new Color(58, 56, 52) : hover ? new Color(46, 48, 56) : new Color(32, 35, 44), Kit.IronDark, 1);
        ui.Rect(new Rectangle(r.X, r.Y, ui.U(2), r.Height), RoleColor(role));
        // The name has the whole width of the tile to itself; the man and the numbers sit under it.
        ui.TextFit(c.Name.ToUpperInvariant(), r.X + ui.U(6), r.Y + ui.U(5), r.Width - ui.U(10), picked ? Kit.Gilt : Style.Text, 1);
        int top = r.Y + ui.U(16), ph = r.Bottom - ui.U(4) - top;
        var port = new Rectangle(r.X + ui.U(5), top, Math.Min(ph, ui.U(40)), ph);
        ui.VGrad(port, Kit.Mul(Style.Sides[2].Cloth, 0.5f), Kit.IronDark, 2);
        ui.Portrait(c.Type, port, ui.S);
        ui.Frame(port, Color.Black * 0.6f, ui.U(1));
        int tx = port.Right + ui.U(6), tw = r.Right - tx - ui.U(5);
        ui.Text($"{c.Men}", tx, top + ui.U(1), Style.Text, 1);
        int mx = tx + ui.TW($"{c.Men}", 1) + ui.U(3);
        if (c.Wounded > 0) { ui.Text($"+{c.Wounded}", mx, top + ui.U(1), Style.Warn, 1); mx += ui.TW($"+{c.Wounded}", 1) + ui.U(3); }
        ui.Text(Style.Numerals[Math.Clamp(c.Type.Tier, 0, 7)], mx + ui.U(2), top + ui.U(1), Kit.Mul(Kit.Gilt, 0.8f), 1);
        int rx = r.Right - ui.U(5);
        if (leads) { ui.Icon("crown", rx - ui.U(7), top + ui.U(1), Kit.Gilt, 1); rx -= ui.U(11); }
        if (ready) { ui.TextR($"{c.Ready}", rx, top + ui.U(1), Kit.Gilt, 1); ui.Icon("up", rx - ui.TW($"{c.Ready}", 1) - ui.U(10), top + ui.U(1), Kit.Gilt, 1); }
        float full = Rules.Places(c) / MathF.Max(1, Rules.Cap(role));
        ui.Gauge(new Rectangle(tx, top + ui.U(13), tw, ui.U(4)), full, RoleColor(role));
        if (!small) ui.Gauge(new Rectangle(tx, top + ui.U(21), tw, ui.U(4)), c.Cohesion / 30f, c.Cohesion >= 15 ? Style.Ok : c.Cohesion >= 8 ? Style.Warn : Style.Bad);
        if (!small) ui.TextFit(c.Officer != "" ? c.Officer.ToUpperInvariant() : role.ToUpperInvariant(), tx, top + ui.U(30), tw, Style.Dim, 1);
        if (held) ui.Rect(r, Kit.IronDark * 0.6f);
        ui.Frame(r, picked ? Kit.Gilt : ready ? Kit.Mul(Kit.Gilt, 0.6f) : Color.White * (hover ? 0.3f : 0.1f), ui.U(picked ? 2 : 1));
        if (hover && !dragging)
        {
            Ui.Hovered(r);
            note = $"{c.Name.ToUpperInvariant()}: {c.Men} MEN{(c.Wounded > 0 ? $", {c.Wounded} WOUNDED" : "")}, {Rules.Places(c):0} OF {Rules.Cap(role):0} PLACES, COHESION {c.Cohesion:0}.{(leads ? " ITS SERGEANT LEADS THIS DIVISION." : "")}";
        }
        if (ui.Clicked(r)) { musterPick = c; musterTab = 0; dragCo = c.Id; dragFrom = ui.Mouse; dragging = false; if (renaming != c) renaming = null; }
    }

    /// The company picked, in full: its man and name (click to rename), men, places and cohesion as bars, its story, the
    /// moves on the whole company, its troops (PROMOTE opens the tree), and SEND: so many men of the troop picked to
    /// another company that takes their kind, or off into a new one.
    void CompanyDetail(Rectangle s, CompanyRecord c)
    {
        var list = C.Player.Companies;
        string role = Rules.RoleOf(c);
        int x = s.X + ui.U(12), w = s.Width - ui.U(24), y = s.Y + ui.U(6);
        var port = new Rectangle(x, y, ui.U(64), ui.U(64));
        ui.VGrad(port, Kit.Mul(Style.Sides[2].Cloth, 0.55f), new Color(20, 18, 16), 3);
        ui.Portrait(c.Type, port, ui.S);
        ui.Frame(port, Kit.GiltDark, ui.U(1));
        int tx = port.Right + ui.U(10), tw = x + w - tx;
        var nameBox = new Rectangle(tx - ui.U(3), y - ui.U(2), tw + ui.U(6), ui.U(19));
        string nm = c.Name.ToUpperInvariant();
        if (renaming == c) { ui.Rect(nameBox, Style.Accent * 0.25f); ui.Text(renameText + (time % 1 < 0.5f ? "_" : ""), tx, y + ui.U(4), Style.Accent, 1); }
        else { if (ui.Hover(nameBox)) ui.Rect(nameBox, Color.White * 0.06f); ui.TextFit(nm, tx, y + (ui.FitScale(nm, tw) == 2 ? 0 : ui.U(4)), tw, Style.Text, ui.FitScale(nm, tw)); }
        if (renaming == null && ui.Clicked(nameBox)) { renaming = c; renameText = nm; Ui.Typed.Clear(); }
        if (ui.Hover(nameBox) && renaming == null) note = "CLICK THE NAME TO CHANGE IT (ENTER KEEPS IT).";
        string who = c.Hero != "" ? c.Hero.ToUpperInvariant() + " (HERO)" : c.Officer != "" ? c.Officer.ToUpperInvariant() : "NO SERGEANT";
        ui.TextFit(role.ToUpperInvariant() + " . " + who, tx, y + ui.U(22), tw, RoleColor(role), 1);
        var grade = Battle.GradeOf(Battle.DrillOf(c)); // what its drill makes it in battle (docs/archive/MELEE.md D1)
        int chx = tx;
        chx += ui.Chip(chx, y + ui.U(36), Battle.GradeName(grade).ToUpperInvariant(), grade >= Grade.Veteran ? Kit.Gilt : Style.Text, "shield") + ui.U(4);
        chx += ui.Chip(chx, y + ui.U(36), $"{c.Battles} BATTLES", Style.Dim, "sword") + ui.U(4);
        if (c.Honours.Count > 0) ui.Chip(chx, y + ui.U(36), $"{c.Honours.Count}", Kit.Gilt, "star");
        ui.TextFit(c.Home != "" ? $"RAISED IN {c.Home.ToUpperInvariant()}" : "", tx, y + ui.U(54), tw, Style.Dim, 1);
        if (ui.Hover(port)) note = CompanyNote(c);
        y = port.Bottom + ui.U(10);
        void Row(string label, float frac, Color col, string value, Color vc)
        {
            ui.Text(label, x, y, Style.Dim, 1);
            ui.Gauge(new Rectangle(x + ui.U(62), y, w - ui.U(176), ui.U(7)), Math.Clamp(frac, 0, 1), col);
            ui.TextR(value, x + w, y, vc, 1);
            y += ui.U(13);
        }
        Row("PLACES", Rules.Places(c) / MathF.Max(1, Rules.Cap(role)), RoleColor(role), $"{c.Men} MEN, {Rules.Places(c):0}/{Rules.Cap(role):0}", Style.Text);
        Row("WOUNDED", c.Wounded / (float)Math.Max(1, c.Men + c.Wounded), Style.Warn, $"{c.Wounded}", c.Wounded > 0 ? Style.Warn : Style.Dim);
        Row("COHESION", c.Cohesion / 30f, c.Cohesion >= 15 ? Style.Ok : c.Cohesion >= 8 ? Style.Warn : Style.Bad, $"{c.Cohesion:0}", Style.Text);
        y += ui.U(4);

        // The moves on the whole company.
        int bw = (w - ui.U(12)) / 4, bh = ui.U(22);
        var mate = list.FirstOrDefault(o => Rules.CanMerge(c, o));
        var b0 = new Rectangle(x, y, bw, bh);
        if (ui.Button(b0, $"ROLL {c.Roll.Count}")) { rollOf = c; panel = PanelKind.Roll; note = ""; return; }
        if (ui.Hover(b0)) note = c.Roll.Count > 0 ? string.Join(", ", c.Roll.Select(m => m.Name.ToUpperInvariant())) + "." : "NO NAMED MEN YET. MEN EARN NAMES IN BATTLE.";
        var b1 = new Rectangle(x + bw + ui.U(4), y, bw, bh);
        if (ui.Button(b1, "SPLIT", c.Men >= 2)) { note = C.Split(c) ?? ""; return; }
        if (ui.Hover(b1)) note = $"SPLIT {nm} INTO TWO COMPANIES, HALF OF EVERY TROOP EACH.";
        var b2 = new Rectangle(x + 2 * (bw + ui.U(4)), y, bw, bh);
        if (ui.Button(b2, "MERGE", mate != null)) { C.Merge(c, mate!); return; }
        if (ui.Hover(b2)) note = mate != null ? $"FOLD {mate.Name.ToUpperInvariant()} INTO {nm}." : "NO COMPANY OF ITS KIND WITH ROOM TO FOLD IN.";
        var b3 = new Rectangle(x + 3 * (bw + ui.U(4)), y, bw, bh);
        if (ui.Button(b3, "FIRST", list.IndexOf(c) > 0)) { while (list.IndexOf(c) > 0) C.MoveUp(c); return; }
        if (ui.Hover(b3)) note = $"PUT {nm} FIRST IN THE LINE: THE ORDER COMPANIES DRAW UP IN.";
        y += bh + ui.U(10);

        // Its troops.
        y = Heading(x, y, w, "ITS TROOPS", "CLICK ONE TO SEND ITS MEN");
        var stacks = c.Stacks.OrderByDescending(t => t.Type.Tier).ThenByDescending(t => t.Men).ToList();
        if (stacks.Count == 0) return;
        var st = stacks.FirstOrDefault(k => k.Troop == musterStack) ?? stacks[0];
        musterStack = st.Troop;
        int sendRoom = ui.U(150), rowH = ui.U(26);
        foreach (var k in stacks)
        {
            if (y + rowH > s.Bottom - sendRoom) { ui.Text($"+{stacks.Count - stacks.IndexOf(k)} MORE TROOPS", x, y, Style.Dim, 1); y += ui.U(12); break; }
            var row = new Rectangle(x, y, w, rowH - ui.U(3));
            bool on = k == st, h = ui.Hover(row);
            ui.Rect(row, on ? Kit.Gilt * 0.16f : Color.White * (h ? 0.08f : 0.035f));
            ui.Frame(row, on ? Kit.Gilt * 0.8f : Color.White * 0.06f, ui.U(1));
            ui.Text(Style.Numerals[k.Type.Tier], row.X + ui.U(5), row.Y + ui.U(4), Kit.Gilt, 1);
            ui.TextFit(k.Type.Name.ToUpperInvariant(), row.X + ui.U(24), row.Y + ui.U(4), w - ui.U(190), Style.Text, 1);
            ui.TextR($"{k.Men}{(k.Wounded > 0 ? $"+{k.Wounded}" : "")}", row.Right - ui.U(96), row.Y + ui.U(4), Style.Text, 1);
            ui.Bar(new Rectangle(row.X + ui.U(24), row.Bottom - ui.U(6), w - ui.U(124), ui.U(3)), k.PoolCap > 0 ? k.Xp / k.PoolCap : 1, k.Ready > 0 ? Kit.Gilt : Style.Friendly);
            var pb = new Rectangle(row.Right - ui.U(88), row.Y + ui.U(2), ui.U(86), row.Height - ui.U(4));
            if (ui.Button(pb, k.Ready > 0 ? $"PROMOTE {k.Ready}" : "TREE", true, "", k.Ready > 0)) { upgrading = c; upStack = k; panel = PanelKind.Upgrade; note = ""; return; }
            if (ui.Hover(pb)) note = k.Ready > 0 ? $"{k.Ready} OF THEM HAVE THE XP TO RISE. SEE THE TREE AND PROMOTE THEM." : "SEE WHAT THESE MEN CAN BECOME.";
            else if (h) note = $"{k.Men} {k.Type.Name.ToUpperInvariant()}: POOL {k.Xp:0}/{k.PoolCap:0} XP, {k.Type.UpgradeXp} A MAN.";
            if (ui.Clicked(row)) { musterStack = k.Troop; st = k; }
            y += rowH;
        }
        y += ui.U(6);

        // Send so many of them elsewhere.
        y = Heading(x, y, w, ui.Fit("SEND " + st.Type.Name.ToUpperInvariant(), w - ui.U(120)), $"{st.Men} FIT");
        musterSend = Math.Clamp(musterSend, Math.Min(1, st.Men), Math.Max(1, st.Men));
        int sw = ui.U(34), sx = x;
        foreach (var (label, by) in new[] { ("-10", -10), ("-1", -1) })
        { if (ui.Button(new Rectangle(sx, y, sw, bh), label, musterSend > 1)) musterSend = Math.Max(1, musterSend + by); sx += sw + ui.U(3); }
        var num = new Rectangle(sx, y, w - 5 * (sw + ui.U(3)) - ui.U(12), bh);
        ui.Rect(num, Color.Black * 0.45f);
        ui.Frame(num, Kit.GiltDark, ui.U(1));
        ui.TextC($"{musterSend} MEN", num.Center.X, num.Y + ui.U(4), Kit.Gilt, 2);
        sx = num.Right + ui.U(3);
        foreach (var (label, by) in new[] { ("+1", 1), ("+10", 10) })
        { if (ui.Button(new Rectangle(sx, y, sw, bh), label, musterSend < st.Men)) musterSend = Math.Min(st.Men, musterSend + by); sx += sw + ui.U(3); }
        if (ui.Button(new Rectangle(sx, y, x + w - sx, bh), "ALL", st.Men > 0, "", musterSend == st.Men)) musterSend = st.Men;
        y += bh + ui.U(6);
        int foot = s.Bottom - ui.U(30);
        foreach (var o in list.Where(o => o != c && Rules.Fits(o, st.Troop)))
        {
            if (y + ui.U(22) > foot - ui.U(24)) break;
            int room = Rules.Room(o, st.Troop), n = Math.Min(musterSend, room);
            var tb = new Rectangle(x, y, w, ui.U(20));
            if (ui.Button(tb, ui.Fit("TO " + o.Name.ToUpperInvariant(), w - ui.U(110)), n > 0 && st.Men > 0, room > 0 ? $"ROOM FOR {room}" : "FULL")) { note = C.MoveMen(c, st, o, n) ?? $"SENT {n} TO {o.Name.ToUpperInvariant()}"; return; }
            if (ui.Hover(tb)) note = n > 0 ? $"SEND {n} {st.Type.Name.ToUpperInvariant()} TO {o.Name.ToUpperInvariant()}, WITH THEIR SHARE OF THE XP." : $"{o.Name.ToUpperInvariant()} HAS NO ROOM FOR THEM.";
            y += ui.U(23);
        }
        if (y + ui.U(22) <= foot)
        {
            var nb = new Rectangle(x, y, w, ui.U(20));
            bool free = list.Count < Rules.CompanySlots(C.Captain);
            if (ui.Button(nb, "TO A NEW COMPANY", free && st.Men > 0, free ? "" : "NO FREE SLOT")) { note = C.SplitStack(c, st, musterSend) ?? $"{musterSend} MEN FORM A NEW COMPANY"; return; }
            if (ui.Hover(nb)) note = $"{musterSend} {st.Type.Name.ToUpperInvariant()} LEAVE TO FORM A COMPANY OF THEIR OWN.";
        }
        // Set apart at the foot: it cannot be undone.
        var db = new Rectangle(x + w - ui.U(110), s.Bottom - ui.U(26), ui.U(110), ui.U(20));
        if (ui.Gilded(db, "DISBAND", "", list.Count > 1, Style.Bad)) { C.Disband(c); return; }
        if (ui.Hover(db)) note = $"SEND THE MEN OF {nm} HOME. THIS CANNOT BE UNDONE.";
    }
}
