using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: small modal panels (encounter, siege, battle results, events, rank beats, game over, menu).
partial class MapScreen
{
    /// Travellers on the road who are not your enemy (S4): buy from them, give alms, ask for news, rob them, or ride on.
    void MeetingPanel(Party e)
    {
        bool pilgrims = e.Breed == "pilgrims";
        var r = Modal(620, 250, pilgrims ? "PILGRIMS ON THE ROAD" : "A CARAVAN", PartyColor(e));
        int x = r.X + ui.U(24), y = r.Y + ui.U(52);
        ui.Text(e.Name.ToUpperInvariant(), x, y, PartyColor(e));
        ui.Text(Allegiance(e), x, y + ui.U(20), Style.Dim, 1);
        string bound = pilgrims ? "BOUND FOR THE SHRINE, SINGING." : C.SettlementById(e.GoalSettlement) is Settlement d ? $"BOUND FOR {d.Name.ToUpperInvariant()} WITH {e.Gold:0} CROWNS OF GOODS." : "ON THE ROAD.";
        ui.Text(bound, x, y + ui.U(40), Style.Text, 1);
        ui.Text($"{e.Men} GUARDS. ROBBING THEM WOULD ANGER THE {C.FactionNames[Math.Max(0, e.Faction)].ToUpperInvariant()}" + (e.Faction == C.Captain.Contract ? ", YOUR OWN REALM." : "."),
            x, y + ui.U(56), e.Faction == C.Captain.Contract ? Style.Bad : Style.Dim, 1);
        int px = x;
        foreach (var c in e.Companies.Where(c => c.Men > 0).Take(3))
        {
            ui.Portrait(c.Type, new Rectangle(px, y + ui.U(76), ui.U(30), ui.U(28)));
            ui.Text($"{c.Men} {c.Type.Name.ToUpperInvariant()}", px + ui.U(36), y + ui.U(86), Style.Text, 1);
            px += ui.U(180);
        }
        Note(r);
        int bw = ui.U(130), by = r.Bottom - ui.U(40), bx = r.X + (r.Width - 4 * bw - 3 * ui.U(8)) / 2;
        string first = pilgrims ? $"ALMS {Campaign.PilgrimAlms:0}" : $"FOOD {Campaign.CaravanFoodPrice:0}";
        if (ui.Button(new Rectangle(bx, by, bw, ui.U(30)), first))
        {
            note = (pilgrims ? C.GiveAlms() : C.BuyFromCaravan()) ?? (pilgrims ? "" : "4 DAYS OF FOOD BOUGHT.");
            if (C.Encounter == null) panel = PanelKind.None;
        }
        if (ui.Button(new Rectangle(bx + bw + ui.U(8), by, bw, ui.U(30)), "NEWS")) { C.AskNews(); note = "THEIR NEWS IS IN THE LOG."; }
        if (ui.Button(new Rectangle(bx + 2 * (bw + ui.U(8)), by, bw, ui.U(30)), "ROB THEM", C.Player.Men > 0)) C.Rob();
        if (ui.Button(new Rectangle(bx + 3 * (bw + ui.U(8)), by, bw, ui.U(30)), "RIDE ON")) { C.Part(); panel = PanelKind.None; }
    }

    /// At the walls of an enemy town or castle: besiege, assault, wait or leave.
    /// Your siege camp's works (docs/archive/SIEGECRAFT.md A3): how battered the walls are, what is built or building, and a row to
    /// order each work.
    /// "OF THE BREVON" vs "OF THE SKARLANDS" without doubling "THE".
    string OfRealm(int faction)
    {
        string n = C.FactionNames[faction].ToUpperInvariant();
        return n.StartsWith("THE ") ? $"OF {n}" : $"OF THE {n}";
    }

    void SiegeWorksRows(Settlement s, Rectangle r, int x, int y)
    {
        var works = C.WorksOf(C.Player);
        string state = works.Count == 0 ? "NO EXTRA ENGINES BUILT." : string.Join(", ", works.Select(w => Engines.Get(w.Id).Name.ToUpperInvariant() + (w.ReadyAt <= C.S.Hours ? " (READY)" : $" ({w.ReadyAt - C.S.Hours:0} H)")));
        ui.Text($"WALLS BATTERED {s.Battered * 100:0}%{(s.Battered >= 1 ? ": A BREACH" : "")}", x, y, s.Battered >= 1 ? Style.Ok : Style.Text, 1);
        ui.Text(ui.Fit(state, r.Width - ui.U(220)), x + ui.U(170), y, Style.Dim, 1);
        y += ui.U(18);
        foreach (var def in Campaign.SiegeWorkDefs)
        {
            var row = new Rectangle(x, y, r.Width - ui.U(48), ui.U(40));
            TownRow(row);
            ui.Text(def.Name.ToUpperInvariant(), row.X + ui.U(10), row.Y + ui.U(4), Style.Text, 1);
            int ly = row.Y + ui.U(16);
            foreach (var line in ui.Wrap(def.Line.ToUpperInvariant(), row.Width - ui.U(196)).Take(2)) { ui.Text(line, row.X + ui.U(10), ly, Style.Dim, 1); ly += ui.U(10); }
            var why = C.CannotBuildWork(s, def.Id);
            float hours = def.Build * (C.Has("Siegecraft") ? 0.5f : 1);
            var bt = new Rectangle(row.Right - ui.U(176), row.Y + ui.U(7), ui.U(172), ui.U(26));
            if (ui.Button(bt, $"BUILD {hours:0} H", why == null, $"{def.Cost:0}")) note = C.BuildWork(s, def.Id) ?? $"{def.Name.ToUpperInvariant()} UNDER WAY";
            if (why != null && ui.Hover(bt)) note = why;
            y += ui.U(44);
        }
    }

    void SiegePanel()
    {
        var s = at!;
        var r = Modal(680, C.Besieging == s ? 580 : 400, (C.Besieging == s ? "THE SIEGE OF " : "") + s.Name.ToUpperInvariant(), SideColor(s.Faction));
        int x = r.X + ui.U(24), y = r.Y + ui.U(50);
        ui.Text($"{s.KindWord} {OfRealm(s.Faction)}", x, y, Style.Dim, 1);
        y += ui.U(18);
        ui.Text($"GARRISON {C.GarrisonMen(s)} MEN   SUPPLIES {s.Supplies:0} DAYS", x, y, Style.Text);
        y += ui.U(24);
        foreach (var g in s.Garrison.Take(4))
        {
            ui.Portrait(g.Type, new Rectangle(x, y, ui.U(30), ui.U(28)));
            ui.Text($"{g.Men} {g.Type.Name.ToUpperInvariant()}", x + ui.U(36), y + ui.U(10), Style.Text, 1);
            x += ui.U(148);
        }
        x = r.X + ui.U(24);
        y += ui.U(40);
        var helpers = C.S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && p.Faction == s.Faction && Vector2.Distance(p.Pos, s.Pos) < 10).ToList();
        if (helpers.Count > 0) ui.Text("INSIDE: " + string.Join(", ", helpers.Select(h => h.Name.ToUpperInvariant())), x, y, Style.Foe, 1);
        y += ui.U(18);
        bool besieging = C.Besieging == s;
        if (besieging)
        {
            float wait = C.LaddersIn(s);
            ui.Text(wait > 0 ? $"LADDERS AND A RAM IN {wait:0} HOURS. ASSAULTING NOW MEANS THE GATE ALONE." : "LADDERS ARE BUILT AND THE RAM IS READY.", x, y, wait > 0 ? Style.Warn : Style.Ok, 1);
            y += ui.U(14);
            bool tight = C.TightEncirclement(s);
            int days = Math.Max(0, (int)((C.S.Hours - s.SiegeSince) / 24));
            ui.Text(tight ? $"TIGHT ENCIRCLEMENT (DAY {days}) - STORES FALLING DAILY." : $"LOOSE ENCIRCLEMENT (DAY {days}) - HALF SUPPLIES TRICKLE IN.", x, y, tight ? Style.Dim : Style.Warn, 1);
            y += ui.U(20);
            SiegeWorksRows(s, r, x, y);
            int sy = r.Bottom - ui.U(76);
            int sw = ui.U(156);
            string sapLabel = C.SapReady(s) ? "SAP READY" : s.SapReadyAt > 0 ? $"SAP ({MathF.Ceiling(s.SapReadyAt - (float)C.S.Hours)}H)" : "MINE SAP";
            if (ui.Button(new Rectangle(x, sy, sw, ui.U(26)), sapLabel, s.SapReadyAt <= 0 && C.S.Gold >= 180, s.SapReadyAt <= 0 ? "180" : "")) { note = C.MineSap(s) ?? ""; }
            if (Campaign.Level(s, "walls") >= 2)
            {
                string ditchLabel = s.DitchFilled ? "DITCH FILLED" : "FILL DITCH";
                if (ui.Button(new Rectangle(x + sw + ui.U(8), sy, sw, ui.U(26)), ditchLabel, !s.DitchFilled && C.S.Gold >= 80, !s.DitchFilled ? "80" : "")) { note = C.FillDitch(s) ?? ""; }
            }
            bool canTerms = C.CanOfferTerms(s, out string termsWhy);
            var termsRect = new Rectangle(r.Right - ui.U(24) - sw, sy, sw, ui.U(26));
            if (ui.Button(termsRect, "OFFER TERMS", canTerms)) { C.OfferTerms(s); panel = PanelKind.None; return; }
            if (!canTerms && ui.Hover(termsRect)) note = termsWhy;
        }
        else ui.Text("YOUR MEN WILL NEED A NIGHT TO BUILD LADDERS AND A RAM.", x, y, Style.Dim, 1);
        Note(r);
        int bw = ui.U(156), by = r.Bottom - ui.U(40), bx = r.X + (r.Width - 4 * bw - 3 * ui.U(8)) / 2;
        Rectangle Bt(int i) => new(bx + i * (bw + ui.U(8)), by, bw, ui.U(30));
        if (!besieging)
        {
            bool ok = C.CanBesiege(s, out string why);
            if (ui.Button(Bt(0), "BESIEGE", ok)) { C.Besiege(s); }
            if (!ok && ui.Hover(Bt(0))) note = why;
        }
        else
        {
            if (ui.Button(Bt(0), "ASSAULT", C.Player.Men > 0, active: C.Player.Men > 0)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareAssault(s)); return; }
            if (ui.Button(Bt(1), "AUTO-RESOLVE", C.Player.Men > 0)) { C.AutoResolveAssault(s); ShowResults(); return; }
            if (ui.Button(Bt(2), "WAIT 12 H")) { C.Rest(12); if (C.Encounter != null) panel = PanelKind.None; }
        }
        if (ui.Button(Bt(3), besieging ? "LIFT SIEGE" : "LEAVE")) { if (besieging) C.LiftSiege(); panel = PanelKind.None; }
    }


    object? beatHeard;

    /// A career beat, a council's decision that concerns you, or the war won (F5): shown once.
    void BeatPanel()
    {
        if (C.Beat is not var (title, lines)) { panel = PanelKind.None; return; }
        bool won = title == "THE WAR IS WON";
        if (!ReferenceEquals(lines, beatHeard)) { beatHeard = lines; AudioEngine.Current?.Play(Cue.Horn); AudioEngine.Current?.PlayStinger("sting_rank"); } // a horn as the banner sweeps in
        var r = Modal(640, 150 + 16 * lines.Length + (won ? 40 : 0), title, won ? Style.Accent : Style.Text);
        int y = r.Y + ui.U(62);
        foreach (var line in lines) { ui.TextC(line, r.X + r.Width / 2f, y, Style.Text, 1); y += ui.U(16); }
        if (won)
        {
            if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(44), ui.U(220), ui.U(30)), "PLAY ON", active: true))
            { C.Beat = null; panel = PanelKind.None; }
            if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(80), ui.U(220), ui.U(30)), "TO THE TITLE"))
            { C.Beat = null; Write(SaveKind.Auto); OnTitle?.Invoke(); }
        }
        else
        {
            int cw = ui.U(180), rw = ui.U(150), gap = ui.U(14), bx0 = r.Center.X - (cw + gap + rw) / 2;
            if (ui.Button(new Rectangle(bx0, r.Bottom - ui.U(44), cw, ui.U(30)), "CONTINUE", active: true))
            { C.Beat = null; panel = PanelKind.None; }
            if (ui.Button(new Rectangle(bx0 + cw + gap, r.Bottom - ui.U(44), rw, ui.U(30)), $"REALM ({ctl.Name(Act.RealmScreen)})"))
            { C.Beat = null; realmTab = 1; OpenScreen(PanelKind.Realm); }
        }
    }

    /// Ironman: the captain was taken and executed. The campaign (and its save) ends here.
    void GameOverPanel()
    {
        var faces = C.StoryFaces();
        var r = Modal(760, faces.Count > 0 ? 440 : 400, C.S.Retired ? "THE COMPANY'S LEGEND" : "YOUR WAR IS OVER", C.S.Retired ? Kit.Gilt : Style.Bad);
        ui.TextC(C.S.EndReason != "" ? C.S.EndReason : "THE ENEMY DID NOT RANSOM YOU.", r.X + r.Width / 2f, r.Y + ui.U(66), Style.Warn, 1);
        int y = r.Y + ui.U(96);
        foreach (var line in C.Summary()) { ui.TextC(line, r.X + r.Width / 2f, y, Style.Text, 1); y += ui.U(18); }
        if (C.S.Deeds.Count > 0) ui.TextC("LAST DEED: " + C.S.Deeds[^1].Text.ToUpperInvariant(), r.X + r.Width / 2f, y + ui.U(8), Kit.Parchment, 1);
        if (faces.Count > 0) StoryFaceRow(faces, r, y + ui.U(32));
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(50), ui.U(220), ui.U(32)), "TO THE TITLE"))
        {
            Saves.EndCampaign(C.S);
            OnTitle?.Invoke();
        }
    }

    /// The company's men at the end (C7): a group a column, their faces in a row under its title, the dead greyed; the name under
    /// the mouse.
    void StoryFaceRow(List<(string Title, List<Soldier> Men)> faces, Rectangle r, int y)
    {
        int fw = ui.U(40), fh = ui.U(48), gap = ui.U(10), cols = faces.Count;
        int colW = (r.Width - ui.U(48)) / cols;
        for (int g = 0; g < cols; g++)
        {
            var (title, men) = faces[g];
            int cx = r.X + ui.U(24) + g * colW + colW / 2;
            ui.TextC(title, cx, y, Kit.Gilt, 1);
            int rowW = men.Count * fw + (men.Count - 1) * gap, x0 = cx - rowW / 2;
            for (int i = 0; i < men.Count; i++)
            {
                var m = men[i];
                bool dead = m.Fate is "fell" or "missing";
                var fb = new Rectangle(x0 + i * (fw + gap), y + ui.U(14), fw, fh);
                ui.Rect(new Rectangle(fb.X - 1, fb.Y - 1, fb.Width + 2, fb.Height + 2), Campaign.OfBanner(m) ? Kit.Gilt : Kit.IronDark);
                var k = C.Player.Companies.FirstOrDefault(c => c.Roll.Contains(m) || c.Id == m.Company);
                if (dead) ui.FaceHead(PortraitPainter.Of(m, k) with { Mood = 1 }, fb, new Color(150, 150, 156));
                else ui.FaceHead(PortraitPainter.Of(m, k), fb);
                var first = m.Name.Split(' ')[0].ToUpperInvariant(); // his first name under his face; the tooltip has the rest
                ui.TextC(ui.TW(first, 1) > fw + gap ? first[..Math.Min(first.Length, 6)] : first, fb.Center.X, fb.Bottom + ui.U(5), dead ? Style.Dim : Style.Text, 1);
                if (ui.Hover(fb)) ui.Tooltip(fb.Center.X, fb.Y - ui.U(4), m.Name.ToUpperInvariant(), $"{Campaign.N(m.Battles, "BATTLE")}, {Campaign.N(m.Kills, "KILL")}" + (dead ? $". FELL DAY {m.DiedDay}" : ""));
            }
        }
    }

    void MenuPanel()
    {
        var r = Modal(300, 386, "PAUSED");
        int y = r.Y + ui.U(54);
        Rectangle B() { var b = new Rectangle(r.X + ui.U(40), y, r.Width - ui.U(80), ui.U(30)); y += ui.U(38); return b; }
        if (ui.Button(B(), "RESUME")) panel = PanelKind.None;
        bool quick = C.S.CampaignId != "" && File.Exists(Path.Combine(Saves.DirOf(C.S.CampaignId), "quick.json"));
        if (ui.Button(B(), C.S.Ironman ? "SAVE" : "QUICK SAVE", note: ctl.Name(Act.QuickSave))) { Save(); panel = PanelKind.None; }
        if (ui.Button(B(), "QUICK LOAD", !C.S.Ironman && quick, ctl.Name(Act.QuickLoad))) { QuickLoad(); panel = PanelKind.None; }
        if (ui.Button(B(), "SAVE AS...", !C.S.Ironman)) { saves.Show(SaveScreen.Use.Save, C.S); panel = PanelKind.Saves; }
        if (ui.Button(B(), "LOAD...")) { saves.Show(SaveScreen.Use.Load, C.S); panel = PanelKind.Saves; }
        if (ui.Button(B(), "SETTINGS")) OnSettings?.Invoke();
        if (ui.Button(B(), "QUIT TO TITLE")) { Write(SaveKind.Auto); OnTitle?.Invoke(); }
    }

    /// SAVE AS and LOAD: the save screen over the map (SaveScreen.cs). Escape or BACK returns to the menu.
    void SavesPanel()
    {
        bool loading = false;
        saves.OnLoad ??= p =>
        {
            if (OnLoadSave?.Invoke(p) is string why) C.Log("Not loaded: " + why.ToLowerInvariant() + ".");
            else loading = true;
        };
        saves.OnSaved ??= p => thumbFor = p;
        saves.Draw(ui, ms.ScrollWheelValue - pms.ScrollWheelValue, kb, pkb);
        if (!saves.Open) panel = loading ? PanelKind.None : PanelKind.Menu;
    }
}
