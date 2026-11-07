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

    void ResultsPanel()
    {
        var rep = C.Report;
        if (rep == null) { panel = PanelKind.None; return; }
        var r = Modal(900, 520, rep.Won ? "VICTORY" : "DEFEAT", rep.Won ? Style.Accent : Style.Bad);
        ui.TextC("AGAINST " + rep.Enemy.ToUpperInvariant(), r.X + r.Width / 2f, r.Y + ui.U(44), Style.Dim, 1);
        int x = r.X + ui.U(24), y = r.Y + ui.U(64);
        string[] heads = { "COMPANY", "MEN", "KILLED", "WOUNDED", "LOST", "XP" };
        int[] cols = { 0, 330, 450, 540, 640, 720 };
        for (int i = 0; i < heads.Length; i++) ui.Text(heads[i], x + ui.U(cols[i]), y, Style.Dim, 1);
        y += ui.U(16);
        foreach (var c in rep.Companies.Where(c => c.Team == 0).Take(12))
        {
            ui.Text($"{c.Name.ToUpperInvariant()} ({Troops.Get(c.Troop).Name.ToUpperInvariant()})", x, y, Style.Text, 1);
            ui.Text($"{c.MenBefore} -> {c.MenAfter}", x + ui.U(cols[1]), y, Style.Text, 1);
            ui.Text($"{c.Killed}", x + ui.U(cols[2]), y, c.Killed > 0 ? Style.Bad : Style.Dim, 1);
            ui.Text($"{c.Wounded}", x + ui.U(cols[3]), y, Style.Warn, 1);
            ui.Text($"{c.Captured + c.FledLost}", x + ui.U(cols[4]), y, Style.Dim, 1);
            ui.Text(c.Deployed ? $"+{c.XpGain:0}" : "RESERVE", x + ui.U(cols[5]), y, Style.Ok, 1);
            if (c.UpgradeReady) ui.Text("UPGRADE READY", x + ui.U(770), y, Style.Accent, 1);
            y += ui.U(16);
        }
        y += ui.U(10);
        if (rep.Won)
            ui.Text($"LOOT {rep.Loot:0}   RANSOMS {rep.Ransom:0}   BOUNTY {rep.Bounty:0}   RENOWN +{rep.Renown:0}   HORSES +{rep.Horses}", x, y, Style.Accent);
        else ui.Text("YOUR ARMY FALLS BACK. WOUNDED LEFT ON THE FIELD MAY BE TAKEN.", x, y, Style.Bad);
        y += ui.U(20);
        // Progression, spelled out: what this battle was worth and what it unlocked, so the loop is never a mystery.
        var (unlock, at) = Rules.NextRenownUnlock(C.Captain);
        ui.Text($"RENOWN {C.Captain.Renown:0} / {at:0} TO THE NEXT {unlock}", x, y, Style.Dim, 1);
        ui.Bar(new Rectangle(x + ui.U(300), y + ui.U(4), ui.U(240), ui.U(4)), (C.Captain.Renown - RenownStep(C.Captain, at)) / MathF.Max(1, at - RenownStep(C.Captain, at)), Style.Accent);
        int ready = rep.Companies.Count(c => c.UpgradeReady);
        if (ready > 0) ui.Text($"({ready} COMPANY{(ready == 1 ? "" : "IES")} CAN BE TRAINED UP IN A TOWN)", x + ui.U(560), y, Style.Accent, 1);
        y += ui.U(20);
        if (rep.Captain == LeaderFate.Wounded) ui.Text("YOU ARE WOUNDED: FOR A FEW DAYS YOU COMMAND FROM THE REAR.", x, y, Style.Warn, 1);
        if (rep.Captain == LeaderFate.Captured) ui.Text("YOU WERE TAKEN AND RANSOMED (30% OF YOUR GOLD).", x, y, Style.Bad, 1);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CONTINUE"))
        { panel = C.S.GameOver ? PanelKind.GameOver : PanelKind.None; C.Report = null; }
    }

    /// An event (F8): what happened, and two or three choices with what each will do.
    void EventPanel()
    {
        if (C.S.Event is not PendingEvent pe || Campaign.EventOf(pe.Id) is not EventDef e) { panel = PanelKind.None; return; }
        var r = Modal(720, 330, C.Fill(e.Title, pe).ToUpperInvariant(), Style.Accent);
        // The text, wrapped to the panel.
        int x = r.X + ui.U(30), y = r.Y + ui.U(56), maxW = r.Width - ui.U(60);
        var line = "";
        foreach (var word in C.Fill(e.Text, pe).ToUpperInvariant().Split(' '))
        {
            if (ui.TW(line + " " + word, 1) > maxW && line != "") { ui.Text(line, x, y, Style.Text, 1); y += ui.U(15); line = word; }
            else line = line == "" ? word : line + " " + word;
        }
        if (line != "") { ui.Text(line, x, y, Style.Text, 1); y += ui.U(15); }
        y += ui.U(14);
        for (int i = 0; i < e.Choices.Count; i++)
        {
            var ch = e.Choices[i];
            string why = C.ChoiceBlocked(ch);
            var b = new Rectangle(x, y, maxW, ui.U(28));
            if (ui.Button(b, C.Fill(ch.Label, pe).ToUpperInvariant(), why == "", why == "" ? EffectsText(ch) : why)) { note = C.Choose(i) ?? ""; if (C.S.Event == null) panel = PanelKind.None; }
            y += ui.U(34);
        }
        Note(r);
    }

    /// A choice's effects in a few words ("GOLD -150, UNREST -20").
    static string EffectsText(ChoiceDef c) => string.Join(", ", c.Effects.Select(fx => fx.Replace('_', ' ').Replace(":", " ").ToUpperInvariant()
        .Replace("OPINION REALM", "LORDS").Replace("OPINION LIEGE", "LIEGE").Replace("PRISONERS ", "PRISONERS: ").Replace("GAMBLE", "PRIZE")));

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
        { C.Beat = null; panel = PanelKind.Realm; note = ""; }
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
