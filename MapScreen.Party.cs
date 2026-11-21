using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: party, characters, skills and upgrade panels.
partial class MapScreen
{
    /// What a lord's opinion of you is, in one word for the lords list.
    static string Regard(float op) => op switch { >= 50 => "DEVOTED", >= 20 => "FRIENDLY", > -20 => "INDIFFERENT", > -50 => "HOSTILE", _ => "HATES YOU" };

    static string OpinionWord(float op) => op switch { >= 50 => "THE WORLD", >= 20 => "WELL", > -20 => "LITTLE", > -50 => "ILL", _ => "THE WORST" };
    static Color OpinionColor(float op) => op >= 20 ? Style.Ok : op > -20 ? Style.Dim : Style.Bad;
    static string RoleTextOf(string role) => Campaign.RoleText.TryGetValue(role, out var t) ? t : "";

    int charTab;
    /// The realm screen's tab: 0 the realms, 1 your career.
    int realmTab;

    /// The Characters screen (3.2): lords by realm with their traits, what they think of you and why; and your household.
    /// How the lords tab sorts a realm's lords: 0 by their opinion of you, 1 by the men they lead.
    int lordSort;

    void CharactersPanel()
    {
        var r = Modal(1000, 600, "LORDS AND COMPANIONS");
        int realms = C.S.Factions.Count;
        // One tab per realm and the household, all inside the panel: a long name shows its last word ("EMIRATES").
        int tw = Math.Min(ui.U(200), (r.Width - ui.U(40) - realms * ui.U(4)) / (realms + 1)), tx = r.X + (r.Width - (realms + 1) * (tw + ui.U(4)) + ui.U(4)) / 2;
        for (int i = 0; i <= realms; i++)
        {
            string label = i < realms ? C.FactionNames[i].ToUpperInvariant() : "YOUR HOUSEHOLD";
            if (ui.TW(label, 1) > tw - ui.U(8)) label = ui.Fit(label.Split(' ').Last(), tw - ui.U(8));
            if (ui.Button(new Rectangle(tx + i * (tw + ui.U(4)), r.Y + ui.U(46), tw, ui.U(24)), label, true, "", charTab == i)) { charTab = i; note = ""; }
        }
        int x = r.X + ui.U(24), y = r.Y + ui.U(100), colW = (r.Width - ui.U(60)) / 2, rowH = ui.U(38);
        // Sort the realm's lords by what you came to find: who likes you, or whose host is strongest (docs/pre-demo/PLAYTEST.md PT2e).
        if (charTab < realms && ui.WordTabs(x + ui.U(4), r.Y + ui.U(76), new[] { "FRIENDLIEST FIRST", "STRONGEST FIRST" }, lordSort) is int ls and >= 0) lordSort = ls;
        var lords = C.S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == charTab);
        var list = charTab < realms
            ? (lordSort == 0 ? lords.OrderByDescending(C.OpinionOfYou) : lords.OrderByDescending(ch => ch.PartyId >= 0 && C.PartyById(ch.PartyId) is Party pp ? pp.Men : 0)).ToList()
            : C.Companions.ToList();
        if (list.Count == 0) ui.Text(charTab < realms ? "NO LORDS." : $"NOBODY YET. OFFICERS WHO LIVE THROUGH {Campaign.CompanionBattles} BATTLES JOIN YOU, AND WANDERERS WAIT IN SOME TOWNS' TAVERNS.", x, y, Style.Dim, 1);
        int i2 = 0;
        foreach (var ch in list)
        {
            int col = i2 % 2, row = i2 / 2;
            var box = new Rectangle(x + col * (colW + ui.U(12)), y + row * (rowH + ui.U(4)), colW, rowH);
            if (box.Bottom > r.Bottom - ui.U(60)) break;
            ui.Rect(box, Color.White * 0.04f);
            float op = C.OpinionOfYou(ch);
            var party = ch.PartyId >= 0 ? C.PartyById(ch.PartyId) : null;
            // His face at the left of the row (docs/archive/LORDS.md LD1); the rest of the row moves over.
            ui.Face(PortraitPainter.Of(C, ch), new Rectangle(box.X + ui.U(2), box.Y + ui.U(2), ui.U(28), rowH - ui.U(4)));
            box = new Rectangle(box.X + ui.U(30), box.Y, box.Width - ui.U(30), box.Height);
            string state = ch.Kind == "companion" ? $"{ch.Calling.ToUpperInvariant()} LV {ch.Level}, {ch.Role.ToUpperInvariant()}"
                : party == null ? "" : party.Captive ? "YOUR PRISONER" : party.Down ? "SCATTERED" : $"{party.Men} MEN, {party.Task.ToUpperInvariant()}";
            ui.Text(ch.Name.ToUpperInvariant(), box.X + ui.U(6), box.Y + ui.U(4), Style.Text, 1);
            ui.TextR(state, box.Right - ui.U(6), box.Y + ui.U(4), ch.Kind == "companion" && C.S.Hours < ch.WoundedUntil ? Style.Warn : Style.Dim, 1);
            // The second line: what he thinks of you in a word, and his first trait; house, the rest of his traits and his age
            // are on hover (the note line), with his memories of you.
            string rose = C.RoseFrom(ch);
            // Indifference, the usual state, has no chip: only lords who feel something about you stand out.
            int cx = box.X + ui.U(6) + (op > -20 && op < 20 ? 0 : ui.Chip(box.X + ui.U(6), box.Y + ui.U(18), Regard(op), OpinionColor(op), "heart") + ui.U(6));
            string first = rose != "" ? rose : ch.Traits.Count > 0 ? ch.Traits[0].ToUpperInvariant() : "";
            ui.TextFit(first, cx, box.Y + ui.U(21), box.Right - ui.U(6) - cx, rose != "" ? Kit.Gilt : Style.Dim, 1);
            if (ch.Kind == "lord" && ui.Clicked(box))
            {
                if (C.CannotTalk(ch) is string cant) note = cant;
                else { OpenTalk(ch, PanelKind.Characters); return; }
            }
            // A companion leads one company; a click moves him to the next (he swaps with whoever led it).
            if (ch.Kind == "companion" && ui.Clicked(box) && C.Player.Companies.Count > 1)
            {
                var cos = C.Player.Companies;
                int k = cos.FindIndex(co => co.Id == ch.CompanyId);
                C.AssignCompanion(ch, cos[(k + 1) % cos.Count]);
            }
            if (ui.Hover(box))
            {
                var why = C.MemoriesOf(ch, C.Me).Take(3).Select(m => $"{m.What.ToUpperInvariant()} ({m.Weight:+0;-0}, DAY {m.Day})").ToList();
                string house = C.HouseOf(ch) is House hs ? hs.Name.ToUpperInvariant() + ", " : "";
                note = house + string.Join(", ", ch.Traits).ToUpperInvariant() + $", AGE {ch.Age}. THINKS {OpinionWord(op)} OF YOU ({op:+0;-0;0})" + (why.Count > 0 ? ": " + string.Join("; ", why) : "");
                if (ch.Kind == "companion")
                {
                    string leads = C.Player.Companies.FirstOrDefault(co => co.Id == ch.CompanyId) is CompanyRecord led ? $"LEADS {led.Name.ToUpperInvariant()}" : "LEADS NO COMPANY";
                    note = leads + (C.Player.Companies.Count > 1 ? " (CLICK: THE NEXT COMPANY). " : ". ") + CompanionNote(ch) + ". " + note;
                }
                else if (C.FamilyLine(ch) is var fam && fam != "") note = fam.ToUpperInvariant() + ". " + note;
                if (ch.Kind == "lord" && C.CannotTalk(ch) == null) note = "CLICK TO TALK. " + note;
            }
            i2++;
        }
        if (charTab == realms) Vassals(r, r.Y + ui.U(100) + ((list.Count + 1) / 2) * (rowH + ui.U(4)) + ui.U(12));
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.LordsScreen))) panel = CameFrom(PanelKind.Characters);
    }

    /// A companion's sheet in one line (docs/archive/COMPANIONS_MAGIC.md B1-B5): calling and what it does, abilities, talents,
    /// XP to the next level, his story, his sworn brother and his student.
    string CompanionNote(Character ch)
    {
        var parts = new List<string> { $"{ch.Role.ToUpperInvariant()}: {RoleTextOf(ch.Role)}" };
        if (Callings.Get(ch.Calling) is CallingDef cd) parts.Add($"{cd.Id.ToUpperInvariant()}: {cd.Line.ToUpperInvariant()}");
        parts.Add("ABILITIES: " + string.Join(", ", Campaign.GiftsOf(ch).Select(a => Abilities.Get(a).Name.ToUpperInvariant())));
        var talents = Campaign.TalentsOf(ch).Select(t => Effects.Get(t).Name.ToUpperInvariant()).Distinct().ToList();
        if (talents.Count > 0) parts.Add("BRINGS: " + string.Join(", ", talents));
        parts.Add(ch.Level >= Campaign.MaxCompanionLevel ? "TOP LEVEL" : $"{Campaign.XpToNext(ch)} XP TO LEVEL {ch.Level + 1}");
        if (ch.Story == 1) parts.Add($"STORY: {Campaign.StoryOf(ch).ToUpperInvariant()} ({ch.StoryWins}/{Campaign.StoryWinsNeeded} WINS)");
        if (ch.Sworn >= 0 && C.CharacterById(ch.Sworn) is Character b) parts.Add($"SWORN BROTHER OF {b.Name.ToUpperInvariant()}");
        if (ch.Warned) parts.Add("MAY LEAVE YOU");
        return string.Join(". ", parts);
    }

    /// A company's story for the party screen: origin, honours, and its last fights.
    static string CompanyNote(CompanyRecord c)
    {
        var parts = new List<string>();
        parts.Add(c.Home != "" ? $"RAISED IN {c.Home.ToUpperInvariant()} ({c.Culture.ToUpperInvariant()})" : "NO HOME");
        parts.Add($"{c.Battles} BATTLES, {c.Kills} KILLS");
        if (c.BattleHonours.Count > 0) parts.Add("BATTLE HONOURS: " + string.Join(", ", c.BattleHonours).ToUpperInvariant());
        if (c.Honours.Count > 0) parts.Add("HONOURS: " + string.Join(", ", c.Honours.Select(h => Honours.Get(h) is HonourDef d ? $"{d.Name.ToUpperInvariant()} ({d.Text.ToUpperInvariant()})" : h)));
        if (c.Traits.Count > 0) parts.Add($"{c.Officer.ToUpperInvariant()}: " + string.Join(", ", c.Traits.Select(t => t.ToUpperInvariant())));
        if (c.Hero != "") parts.Add($"LED BY {c.Hero.ToUpperInvariant()}");
        if (c.Ledger.Count > 0) parts.Add("LAST: " + string.Join("; ", c.Ledger.TakeLast(3).Reverse().Select(l => $"DAY {l.Day} {(l.Won ? "WON" : "LOST")}{(l.Name != "" ? $" AT {l.Name.ToUpperInvariant()}" : "")} VS {l.Foe.ToUpperInvariant()}, {l.Kills} KILLS, {l.Lost} LOST")));
        return string.Join(". ", parts);
    }

    /// A division's post and stance as two buttons side by side (docs/archive/OFFICERS.md OF6): each click steps to the next post, or
    /// the next of the post's stances. True when one was clicked.
    bool PostButtons(DivisionRecord d, Rectangle area, string? arrow, bool captions = false)
    {
        var g = Campaign.GoalOf(d);
        int half = (area.Width - ui.U(4)) * 4 / 7; // the post's names are the longer ("RIGHT WING")
        var pb = new Rectangle(area.X, area.Y, half, area.Height);
        var sb = new Rectangle(area.X + half + ui.U(4), area.Y, area.Width - half - ui.U(4), area.Height);
        int room = pb.Width - ui.U(arrow != null ? 22 : 8), sroom = sb.Width - ui.U(arrow != null ? 22 : 8);
        // Which is which: a caption over each, or the word in the button where it fits.
        string Named(string head, string word) => captions || ui.TW(head + word, 1) > (head == "POST: " ? room : sroom) ? word : head + word;
        if (captions)
        {
            ui.Text("POST", pb.X + ui.U(1), pb.Y - ui.U(9), Style.Text * 0.75f, 1);
            ui.Text("STANCE", sb.X + ui.U(1), sb.Y - ui.U(9), Style.Text * 0.75f, 1);
        }
        bool hit = false;
        if (ui.Button(pb, ui.Fit(Named("POST: ", Goal.Label(g.Post)), room), true, arrow ?? ""))
        {
            var posts = Enum.GetValues<Post>();
            C.SetDivisionPost(d, posts[(Array.IndexOf(posts, g.Post) + 1) % posts.Length]);
            hit = true;
        }
        if (ui.Button(sb, ui.Fit(Named("STANCE: ", Goal.Label(g.Stance)), sroom), true, arrow ?? ""))
        {
            var st = Goal.StancesOf(g.Post);
            C.SetDivisionStance(d, st[(Array.IndexOf(st, g.Stance) + 1) % st.Length]);
            hit = true;
        }
        if (ui.Hover(pb)) note = "HIS POST WHEN THE BATTLE OPENS: THE FRONT, A WING, THE SKIRMISHERS OR THE RESERVE. CLICK FOR THE NEXT.";
        if (ui.Hover(sb)) note = $"HIS STANCE AT THAT POST: HE WILL {g.Describe.ToUpperInvariant()}. CLICK FOR THE NEXT.";
        return hit;
    }

    /// Divisions (docs/archive/COMBAT_OVERHAUL.md 1.5): group companies under a companion or a sergeant, who leads them in battle
    /// by goals and hears your orders late. Companies in no division are your retinue: they obey you at once.
    /// The company picked on the divisions screen (its id), to move with the next click on a division card; -1 for none.
    int divPick = -1;

    /// The divisions screen (docs/pre-demo/PLAYTEST.md PT2f): a card per sergeant (your retinue first) with his temper, command and
    /// starting goal, and his companies as chips. Click a chip, then a card, to move that company there. The last card
    /// makes a new division.
    void DivisionsPanel()
    {
        var divs = C.S.Divisions.ToList();
        // Four cards a row (retinue, up to six divisions and the new card fill two at most); the panel is as tall as its
        // rows of cards (112 + 10 each), the hint and CLOSE.
        int cols = 4, rows = (divs.Count + (divs.Count < Campaign.MaxDivisions ? 2 : 1) + cols - 1) / cols;
        var r = Modal(1000, 46 + rows * 122 + 106, "DIVISIONS");
        int x = r.X + ui.U(20), y = r.Y + ui.U(46), gap = ui.U(10);
        int cw = (r.Width - ui.U(40) - gap * (cols - 1)) / cols, ch = ui.U(112);
        if (divPick >= 0 && C.Player.Companies.All(c => c.Id != divPick)) divPick = -1;
        Rectangle Slot(int i) => new(x + i % cols * (cw + gap), y + i / cols * (ch + gap), cw, ch);
        DivisionCard(null, Slot(0), divs);
        for (int i = 0; i < divs.Count; i++) DivisionCard(divs[i], Slot(i + 1), divs);
        if (divs.Count < Campaign.MaxDivisions) NewDivisionCard(Slot(divs.Count + 1));
        // What a click does now, under the cards.
        var picked = C.Player.Companies.FirstOrDefault(c => c.Id == divPick);
        string hint = picked != null ? $"NOW CLICK THE DIVISION {picked.Name.ToUpperInvariant()} SHOULD JOIN."
            : "CLICK A COMPANY, THEN THE DIVISION IT SHOULD JOIN.";
        // Right under the last row of cards, not down by CLOSE.
        ui.TextFitC(hint, r.Center.X, y + rows * (ch + gap) + ui.U(4), r.Width - ui.U(40), picked != null ? Kit.Gilt : Style.Text, 1);
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), from == PanelKind.Party ? "BACK" : "CLOSE", note: ctl.Name(Act.DivisionsScreen))) panel = CameFrom(PanelKind.Divisions);
    }

    /// One division's card (null = your retinue: the companies you lead yourself).
    void DivisionCard(DivisionRecord? d, Rectangle box, List<DivisionRecord> divs)
    {
        var comps = C.Player.Companies.Where(c => d == null ? divs.All(v => v.Id != c.Division) : c.Division == d.Id).ToList();
        bool target = divPick >= 0 && comps.All(c => c.Id != divPick) && ui.Hover(box);
        ui.Rect(box, Color.White * 0.04f);
        ui.Frame(box, target ? Kit.Gilt : Color.White * 0.14f, ui.U(target ? 2 : 1));
        int px = box.X + ui.U(8), py = box.Y + ui.U(8), pw = box.Width - ui.U(16);
        ui.TextFit(d == null ? "YOUR RETINUE" : C.DivisionName(d).ToUpperInvariant(), px, py, pw, Style.Accent, 1);
        py += ui.U(14);
        if (d == null) { ui.Text("YOU LEAD THESE YOURSELF", px, py + ui.U(3), Style.Dim, 1); py += ui.U(18); }
        else
        {
            string temper = C.TemperOf(d).ToUpperInvariant();
            var tb = new Rectangle(px, py, ui.Chip(px, py, temper, Style.Text, temper == "BOLD" ? "fire" : temper == "CAUTIOUS" ? "shield" : "flag"), ui.U(13));
            if (ui.Hover(tb)) note = temper switch { "BOLD" => "BOLD: CHARGES EARLY AND CHASES FAR.", "CAUTIOUS" => "CAUTIOUS: KEEPS A SHORT LEASH AND LETS THE ENEMY COME.", _ => "STEADY: KEEPS FORMATION AND WAITS FOR CONTACT." };
            int cmd = C.CommandOf(d), qx = tb.Right + ui.U(10);
            ui.Text("COMMAND", qx, py + ui.U(3), Style.Dim, 1);
            var pr = new Rectangle(qx + ui.TW("COMMAND ", 1), py + ui.U(3), ui.U(48), ui.U(7));
            ui.Pips(pr, cmd / 6f, Kit.Gilt, 6);
            ui.Text($"{cmd}/6", pr.Right + ui.U(5), py + ui.U(3), Style.Text, 1);
            if (ui.Hover(new Rectangle(qx, py, pr.Right - qx, ui.U(13)))) note = $"COMMAND {cmd} OF 6: HE IS QUICKER TO ACT AND TO HEAR YOUR ORDERS.";
            py += ui.U(20);
        }
        // The companies as chips; a click picks one (and the same click again lets it go).
        bool chipClicked = false;
        int cx = px, cy = py;
        foreach (var c in comps)
        {
            string label = ui.Fit(c.Name.ToUpperInvariant(), pw - ui.U(40)) + $" {c.Men}";
            int w = ui.TW(label, 1) + ui.U(10);
            if (cx + w > box.Right - ui.U(8)) { cx = px; cy += ui.U(17); }
            if (cy + ui.U(13) > box.Bottom - ui.U(30)) break;
            var cr = new Rectangle(cx, cy, w, ui.U(13));
            bool on = c.Id == divPick;
            // Companies are bright chips that light gold under the mouse: things to pick, not labels.
            ui.Chip(cx, cy, label, on || ui.Hover(cr) ? Kit.Gilt : Style.Friendly);
            if (on) ui.Frame(new Rectangle(cr.X - ui.U(1), cr.Y - ui.U(1), cr.Width + ui.U(2), cr.Height + ui.U(2)), Kit.Gilt, ui.U(1));
            if (ui.Clicked(cr)) { divPick = on ? -1 : c.Id; chipClicked = true; note = ""; }
            cx += w + ui.U(5);
        }
        if (comps.Count == 0) ui.Text(d == null ? "NONE. EVERY COMPANY HAS A SERGEANT." : "NO COMPANIES YET.", px, cy, d == null ? Style.Dim : Style.Warn, 1);
        if (d != null)
        {
            if (PostButtons(d, new Rectangle(px, box.Bottom - ui.U(28), pw - ui.U(88), ui.U(20)), null)) chipClicked = true;
            var db = new Rectangle(box.Right - ui.U(80), box.Bottom - ui.U(28), ui.U(72), ui.U(20));
            if (ui.Button(db, "DISBAND")) { C.DisbandDivision(d); chipClicked = true; }
        }
        // A click elsewhere on the card moves the picked company here.
        if (!chipClicked && target && ui.Clicked(box) && C.Player.Companies.FirstOrDefault(c => c.Id == divPick) is CompanyRecord mv)
        {
            note = C.AssignDivision(mv, d?.Id ?? -1) ?? "";
            divPick = -1;
        }
    }

    /// The last card: who could lead a new division, one button each.
    void NewDivisionCard(Rectangle box)
    {
        ui.Rect(box, Color.White * 0.02f);
        ui.Frame(box, Color.White * 0.12f, ui.U(1));
        int px = box.X + ui.U(8), py = box.Y + ui.U(8), pw = box.Width - ui.U(16);
        ui.Text("NEW DIVISION", px, py, Style.Accent, 1);
        py += ui.U(18);
        var leaders = C.DivisionLeaders().ToList();
        if (leaders.Count == 0)
        {
            foreach (var w in ui.Wrap("NOBODY FREE TO LEAD ONE. COMPANIONS AND COMPANY SERGEANTS CAN.", pw)) { ui.Text(w, px, py, Style.Dim, 1); py += ui.U(12); }
            return;
        }
        ui.Text("UNDER:", px, py, Style.Dim, 1);
        py += ui.U(14);
        foreach (var (name, chr, co) in leaders)
        {
            var b = new Rectangle(px, py, pw, ui.U(20));
            if (b.Bottom > box.Bottom - ui.U(6)) break;
            if (ui.Button(b, ui.Fit(name.ToUpperInvariant(), pw - ui.U(10)))) note = C.MakeDivision(chr, co) ?? "";
            if (ui.Hover(b)) note = chr >= 0 ? "A COMPANION: HE GROWS BETTER AT COMMAND WITH EVERY TWO BATTLES." : "A COMPANY SERGEANT: HIS COMMAND GROWS WITH HIS BATTLES (AT MOST 6).";
            py += ui.U(24);
        }
    }
}
