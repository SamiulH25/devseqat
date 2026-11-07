using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: small modal panels (encounter, siege, battle results, events, rank beats, game over, menu).
partial class MapScreen
{
    void EncounterPanel()
    {
        var e = C.Encounter!;
        var r = Modal(680, 380, "ENCOUNTER", Style.Foe);
        int x = r.X + ui.U(24), y = r.Y + ui.U(52);
        ui.Text(e.Name.ToUpperInvariant(), x, y, PartyColor(e));
        ui.Text(e.Kind == PartyKind.Bandit ? "BRIGANDS" : C.FactionNames[e.Faction].ToUpperInvariant(), x, y + ui.U(20), Style.Dim, 1);
        if (C.CharacterOf(e) is Character ech)
        {
            float op = C.OpinionOfYou(ech);
            ui.Text($"{string.Join(", ", ech.Traits).ToUpperInvariant()}   THINKS {OpinionWord(op)} OF YOU ({op:+0;-0;0})", x + ui.U(170), y + ui.U(20), OpinionColor(op), 1);
        }
        float me = Rules.Strength(C.Player), them = Rules.Strength(e);
        ui.TextR($"YOU {C.Player.Men}   THEM {e.Men}", r.Right - ui.U(24), y + ui.U(2), Style.Text);
        var bar = new Rectangle(x, y + ui.U(36), r.Width - ui.U(48), ui.U(10));
        ui.Rect(bar, Style.Foe);
        ui.Rect(new Rectangle(bar.X, bar.Y, (int)(bar.Width * me / MathF.Max(1, me + them)), bar.Height), Style.Friendly);
        y += ui.U(60);
        foreach (var c in e.Companies.Where(c => c.Men > 0).Take(6))
        {
            ui.Portrait(c.Type, new Rectangle(x, y, ui.U(32), ui.U(30)));
            ui.Text($"{c.Men} {c.Type.Name.ToUpperInvariant()}", x + ui.U(40), y + ui.U(4), Style.Text, 1);
            ui.Text($"TIER {Style.Numerals[c.Type.Tier]}", x + ui.U(40), y + ui.U(16), Style.Dim, 1);
            x += ui.U(190);
            if (x > r.Right - ui.U(180)) { x = r.X + ui.U(24); y += ui.U(36); }
        }
        Note(r);
        int bw = ui.U(156), by = r.Bottom - ui.U(40), bx = r.X + (r.Width - 4 * bw - 3 * ui.U(8)) / 2;
        bool fit = C.Player.Men > 0;
        if (ui.Button(new Rectangle(bx, by, bw, ui.U(30)), "ATTACK", fit)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareBattle(e)); }
        if (ui.Button(new Rectangle(bx + bw + ui.U(8), by, bw, ui.U(30)), "AUTO-RESOLVE", fit)) { C.AutoResolveEncounter(); ShowResults(); }
        bool canRetreat = C.CanRetreat(out string why);
        if (ui.Button(new Rectangle(bx + 2 * (bw + ui.U(8)), by, bw, ui.U(30)), "RETREAT", canRetreat)) { C.Retreat(); panel = PanelKind.None; }
        if (!canRetreat && ui.Hover(new Rectangle(bx + 2 * (bw + ui.U(8)), by, bw, ui.U(30)))) note = why;
        int price = C.ParleyPrice(e);
        if (ui.Button(new Rectangle(bx + 3 * (bw + ui.U(8)), by, bw, ui.U(30)), price > 0 ? $"PAY {price}" : "PARLEY", price > 0 && C.S.Gold >= price))
        {
            if (C.Parley()) panel = PanelKind.None;
        }
        if (price < 0 && ui.Hover(new Rectangle(bx + 3 * (bw + ui.U(8)), by, bw, ui.U(30)))) note = "LORDS AT WAR DO NOT BARGAIN";
    }

    /// At the walls of an enemy town or castle: besiege, assault, wait or leave.
    void SiegePanel()
    {
        var s = at!;
        var r = Modal(680, 400, (C.Besieging == s ? "THE SIEGE OF " : "") + s.Name.ToUpperInvariant(), SideColor(s.Faction));
        int x = r.X + ui.U(24), y = r.Y + ui.U(50);
        ui.Text($"{s.Kind.ToString().ToUpperInvariant()} OF THE {C.FactionNames[s.Faction].ToUpperInvariant()}", x, y, Style.Dim, 1);
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
            ui.Text("STARVING THEM OUT: EACH DAY COSTS THEM A DAY OF SUPPLIES, THEN MEN.", x, y, Style.Dim, 1);
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
            if (ui.Button(Bt(0), "ASSAULT", C.Player.Men > 0)) { panel = PanelKind.None; speed = 0; OnBattle?.Invoke(C.PrepareAssault(s)); return; }
            if (ui.Button(Bt(1), "AUTO-RESOLVE", C.Player.Men > 0)) { C.AutoResolveAssault(s); ShowResults(); return; }
            if (ui.Button(Bt(2), "WAIT 12 H")) { C.Rest(12); if (C.Encounter != null) panel = PanelKind.None; }
        }
        if (ui.Button(Bt(3), besieging ? "LIFT SIEGE" : "LEAVE")) { if (besieging) C.LiftSiege(); panel = PanelKind.None; }
    }


    /// A career beat, a council's decision that concerns you, or the war won (F5): shown once.
    void BeatPanel()
    {
        if (C.Beat is not var (title, lines)) { panel = PanelKind.None; return; }
        bool won = title == "THE WAR IS WON";
        var r = Modal(640, 150 + 16 * lines.Length + (won ? 40 : 0), title, won ? Style.Accent : Style.Text);
        int y = r.Y + ui.U(62);
        foreach (var line in lines) { ui.TextC(line, r.X + r.Width / 2f, y, Style.Text, 1); y += ui.U(16); }
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(44), ui.U(220), ui.U(30)), won ? "PLAY ON" : "CONTINUE"))
        { C.Beat = null; panel = PanelKind.None; }
        if (won && ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(80), ui.U(220), ui.U(30)), "TO THE TITLE"))
        { C.Beat = null; C.S.Save("autosave"); OnTitle?.Invoke(); }
        if (!won && ui.Button(new Rectangle(r.Right - ui.U(150), r.Bottom - ui.U(44), ui.U(130), ui.U(30)), "REALM (R)"))
        { C.Beat = null; realmTab = 1; panel = PanelKind.Realm; note = ""; }
    }

    /// Ironman: the captain was taken and executed. The campaign (and its save) ends here.
    void GameOverPanel()
    {
        var r = Modal(620, 260, "YOUR WAR IS OVER", Style.Bad);
        ui.TextC($"YOU LED YOUR COMPANY FOR {C.Day} DAYS AND WON {C.Captain.Renown:0} RENOWN.", r.X + r.Width / 2f, r.Y + ui.U(70), Style.Text, 1);
        ui.TextC(C.S.EndReason != "" ? C.S.EndReason : "THE ENEMY DID NOT RANSOM YOU.", r.X + r.Width / 2f, r.Y + ui.U(90), Style.Dim, 1);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(110), r.Bottom - ui.U(50), ui.U(220), ui.U(32)), "TO THE TITLE"))
        {
            var p = Path.Combine(CampaignState.SaveDir, "autosave.json");
            if (File.Exists(p)) File.Delete(p);
            OnTitle?.Invoke();
        }
    }

    void MenuPanel()
    {
        var r = Modal(300, 310, "PAUSED");
        int y = r.Y + ui.U(54);
        Rectangle B() { var b = new Rectangle(r.X + ui.U(40), y, r.Width - ui.U(80), ui.U(30)); y += ui.U(38); return b; }
        if (ui.Button(B(), "RESUME")) panel = PanelKind.None;
        if (ui.Button(B(), $"SAVE ({ctl.Name(Act.QuickSave)})")) { Save(); panel = PanelKind.None; }
        if (ui.Button(B(), $"LOAD ({ctl.Name(Act.QuickLoad)})", !C.S.Ironman && CampaignState.Exists("quicksave")) )
        {
            if (CampaignState.Load("quicksave") is CampaignState s) Open(new Campaign(s));
            else note = (CampaignState.LoadError ?? "NO SAVE").ToUpperInvariant();
        }
        if (ui.Button(B(), "SETTINGS")) OnSettings?.Invoke();
        if (ui.Button(B(), "QUIT TO TITLE")) { C.S.Save("autosave"); OnTitle?.Invoke(); }
    }
}
