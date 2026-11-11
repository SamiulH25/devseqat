using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: party, characters, skills and upgrade panels.
partial class MapScreen
{
    static string OpinionWord(float op) => op switch { >= 50 => "THE WORLD", >= 20 => "WELL", > -20 => "LITTLE", > -50 => "ILL", _ => "THE WORST" };
    static Color OpinionColor(float op) => op >= 20 ? Style.Ok : op > -20 ? Style.Dim : Style.Bad;
    static string RoleTextOf(string role) => Campaign.RoleText.TryGetValue(role, out var t) ? t : "";

    int charTab;
    /// The realm screen's tab: 0 the realms, 1 your career.
    int realmTab;

    /// The Characters screen (3.2): lords by realm with their traits, what they think of you and why; and your household.
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
        int x = r.X + ui.U(24), y = r.Y + ui.U(82), colW = (r.Width - ui.U(60)) / 2, rowH = ui.U(38);
        var list = charTab < realms
            ? C.S.Characters.Where(ch => ch.Kind == "lord" && !ch.Dead && ch.Faction == charTab).OrderByDescending(C.OpinionOfYou).ToList()
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
            string rose = C.RoseFrom(ch);
            string house = C.HouseOf(ch) is House hs ? hs.Name.ToUpperInvariant() + " . " : "";
            ui.Text(house + string.Join(", ", ch.Traits).ToUpperInvariant() + $" . AGE {ch.Age}" + (rose != "" ? " . " + rose : ""), box.X + ui.U(6), box.Y + ui.U(16), rose != "" ? Kit.Gilt : Style.Dim, 1);
            // The opinion bar: centre is indifference.
            var bar = new Rectangle(box.X + ui.U(6), box.Y + ui.U(29), box.Width - ui.U(12), ui.U(4));
            ui.Rect(bar, Color.White * 0.08f);
            int mid = bar.X + bar.Width / 2, len = (int)(bar.Width / 2f * MathF.Abs(op) / 100f);
            ui.Rect(new Rectangle(op >= 0 ? mid : mid - len, bar.Y, len, bar.Height), OpinionColor(op));
            ui.Rect(new Rectangle(mid, bar.Y - 1, 1, bar.Height + 2), Style.Text);
            if (ch.Kind == "lord" && ui.Clicked(box))
            {
                if (C.CannotTalk(ch) is string cant) note = cant;
                else { OpenTalk(ch, PanelKind.Characters); return; }
            }
            if (ui.Hover(box))
            {
                var why = C.MemoriesOf(ch, C.Me).Take(3).Select(m => $"{m.What.ToUpperInvariant()} ({m.Weight:+0;-0}, DAY {m.Day})").ToList();
                note = $"THINKS {OpinionWord(op)} OF YOU ({op:+0;-0;0})" + (why.Count > 0 ? ": " + string.Join("; ", why) : "");
                if (ch.Kind == "companion") note = CompanionNote(ch) + ". " + note;
                else if (C.FamilyLine(ch) is var fam && fam != "") note = fam.ToUpperInvariant() + ". " + note;
                if (ch.Kind == "lord" && C.CannotTalk(ch) == null) note = "CLICK TO TALK. " + note;
            }
            i2++;
        }
        if (charTab == realms) Vassals(r, r.Y + ui.U(82) + ((list.Count + 1) / 2) * (rowH + ui.U(4)) + ui.U(12));
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

    /// The promotion picker for one troop stack (2.2): each branch with what it costs to promote 1, 5 or every ready man,
    /// and the reason when it cannot; plus moving or splitting the stack.
    void UpgradePanel()
    {
        var c = upgrading!;
        var st = upStack;
        if (st == null || !c.Stacks.Contains(st) || !C.Player.Companies.Contains(c)) { panel = PanelKind.Party; upStack = null; return; }
        string man = Campaign.ManCulture(c, st.Troop); // only the lines these men may enter by culture (a stranger's are not shown)
        var options = st.Type.UpgradesTo.Where(to => Troops.Get(to) is var t && (t.Culture == Factions.Common || man == Factions.Common || Factions.Open(man, t.Culture))).ToArray();
        var r = Modal(Math.Max(860, options.Length * 190 + 60), 470, $"{st.Type.Name.ToUpperInvariant()} OF {c.Name.ToUpperInvariant()}");
        ui.TextC($"{st.Men} FIT, {st.Wounded} WOUNDED. POOL {st.Xp:0} / {st.PoolCap:0} XP: {st.Ready} READY ({st.Type.UpgradeXp} XP A MAN). EACH MAN PROMOTED SPENDS HIS SHARE.",
            r.X + r.Width / 2f, r.Y + ui.U(44), Style.Dim, 1);
        if (options.Length == 0) ui.TextC("THESE MEN ARE AT THE TOP OF THEIR LINE.", r.X + r.Width / 2f, r.Y + ui.U(120), Style.Text);
        int cw = options.Length >= 5 ? ui.U(176) : ui.U(190), gap = ui.U(10), x = r.X + (r.Width - options.Length * cw - (options.Length - 1) * gap) / 2, y = r.Y + ui.U(62);
        foreach (var to in options)
        {
            var t = Troops.Get(to);
            var card = new Rectangle(x, y, cw, ui.U(300));
            int one = C.Promotable(C.Player, c, st, to, 1, C.S.Gold, out int cost1, out string reason);
            ui.Panel(card, one > 0 ? Style.Accent : Style.PanelEdge, one > 0 ? 2 : 1);
            ui.Portrait(t, new Rectangle(card.X + ui.U(40), card.Y + ui.U(8), cw - ui.U(80), ui.U(70)));
            ui.TextC(t.Name.ToUpperInvariant(), card.X + cw / 2f, card.Y + ui.U(86), Style.Text, t.Name.Length > 16 ? 1 : 2);
            ui.TextC($"TIER {Style.Numerals[Math.Clamp(t.Tier, 0, 7)]}  {t.Class.ToString().ToUpperInvariant()}  {t.Culture.ToUpperInvariant()}", card.X + cw / 2f, card.Y + ui.U(106), Style.Accent, 1);
            int sy = card.Y + ui.U(122);
            var rows = new List<(string label, string value)> { ("HEALTH", $"{t.MaxHp:0}"), ("ARMOUR", $"{t.Armor:0}"), ("DAMAGE", $"{t.Damage:0}"), ("REACH", $"{t.Reach:0.0} M"),
                t.Range > 0 ? ("RANGE", $"{t.Range:0} M") : ("SPEED", $"{t.Speed:0.0}"), ("HORSE", t.Mount?.Name.ToUpperInvariant() ?? "-") };
            var (style, styleLine) = Battle.StyleText(t.Style);
            if (style != "") rows.Add(("STYLE", style.ToUpperInvariant())); // the way his blows land (BattleStyles.cs)
            if (t.Spells.Length > 0) rows.Add(("SPELLS", $"{t.Spells.Length}")); // a mage's spells (Data/abilities.json)
            int step = ui.U(rows.Count > 6 ? 12 : 14);
            foreach (var (label, value) in rows)
            {
                ui.Text(label, card.X + ui.U(14), sy, Style.Dim, 1);
                ui.TextR(value, card.Right - ui.U(14), sy, label == "STYLE" ? Style.Accent : Style.Text, 1);
                sy += step;
            }
            if (style != "" && ui.Hover(card))
            {
                var traits = t.Passives.Select((p, i) => (e: Effects.Get(p), k: Battle.PassiveWeight(t.Tier, i))).Where(x => x.k > 0).Select(x => x.e.Name.ToUpperInvariant());
                note = $"{style.ToUpperInvariant()}: {styleLine.ToUpperInvariant()} TRAITS: {string.Join(", ", traits)}.";
            }
            else if (t.Spells.Length > 0 && ui.Hover(card))
                note = "SPELLS: " + string.Join(", ", t.Spells.Select(id => Abilities.Get(id).Name.ToUpperInvariant())) + ". " + Abilities.Get(t.Spells[^1]).Line.ToUpperInvariant();
            if (Rules.TroopRole(to) != Rules.RoleOf(c)) ui.TextC($"JOINS A {Rules.TroopRole(to).ToUpperInvariant()} COMPANY", card.X + cw / 2f, card.Y + ui.U(210), Style.Warn, 1);
            ui.TextFitC(one > 0 ? $"{cost1} CROWNS A MAN" : reason, card.X + cw / 2f, card.Y + ui.U(226), cw - ui.U(8), one > 0 ? Style.Text : Style.Warn, 1);
            int bw = (cw - ui.U(28) - 2 * ui.U(4)) / 3, by = card.Bottom - ui.U(56);
            int b = 0;
            foreach (var (label, want) in new[] { ("1", 1), ("5", 5), ("ALL", st.Ready) })
            {
                int n = C.Promotable(C.Player, c, st, to, want, C.S.Gold, out int cost, out _);
                var br = new Rectangle(card.X + ui.U(14) + b * (bw + ui.U(4)), by, bw, ui.U(24));
                if (ui.Button(br, label, n > 0 && (want == 1 || n > 1)))
                {
                    note = C.Promote(c, st, to, n) ?? $"PROMOTED {n}";
                    if (!c.Stacks.Contains(st)) { upStack = null; panel = PanelKind.Party; }
                }
                if (ui.Hover(br)) note = n > 0 ? $"PROMOTE {n} FOR {cost} CROWNS" : reason;
                b++;
            }
            ui.TextC($"{st.Ready} READY, {C.S.Gold:0} CROWNS", card.X + cw / 2f, card.Bottom - ui.U(22), Style.Dim, 1);
            x += cw + gap;
        }
        // Moving and splitting the stack.
        int mbw = ui.U(200), mby = r.Bottom - ui.U(80), mbx = r.X + (r.Width - 3 * mbw - 2 * ui.U(8)) / 2;
        var target = C.Player.Companies.FirstOrDefault(o => o != c && Rules.Fits(o, st.Troop) && Rules.Places(o) + (st.Men + st.Wounded) * Rules.TierWeight(st.Type.Tier) <= Rules.Cap(Rules.RoleOf(o)) + 1e-3f);
        if (ui.Button(new Rectangle(mbx, mby, mbw, ui.U(26)), target != null ? $"MOVE TO {target.Name.ToUpperInvariant()}" : "MOVE (NO ROOM)", target != null))
        { note = C.MoveStack(c, st, target!) ?? "MOVED"; upStack = null; panel = PanelKind.Party; return; }
        if (ui.Button(new Rectangle(mbx + mbw + ui.U(8), mby, mbw, ui.U(26)), "SPLIT HALF OFF", st.Men >= 2))
        { note = C.SplitStack(c, st, st.Men / 2) ?? "SPLIT"; upStack = null; panel = PanelKind.Party; return; }
        if (ui.Button(new Rectangle(mbx + 2 * (mbw + ui.U(8)), mby, mbw, ui.U(26)), "SPLIT OFF ALL", st.Men >= 1 && c.Stacks.Count > 1))
        { note = C.SplitStack(c, st, st.Men) ?? "SPLIT"; upStack = null; panel = PanelKind.Party; return; }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "BACK"))
        { upStack = null; panel = at != null && C.SettlementAt(C.Player.Pos) == at ? PanelKind.Settlement : PanelKind.Party; }
    }

    static string GoalText(string g) => g switch { "FlankLeft" => "FLANK LEFT", "FlankRight" => "FLANK RIGHT", _ => g.ToUpperInvariant() };

    /// Divisions (docs/archive/COMBAT_OVERHAUL.md 1.5): group companies under a companion or a sergeant, who leads them in battle
    /// by goals and hears your orders late. Companies in no division are your retinue: they obey you at once.
    void DivisionsPanel()
    {
        var r = Modal(1000, 600, "DIVISIONS");
        int x = r.X + ui.U(20), y = r.Y + ui.U(44), w = ui.U(560);
        ui.Text("A DIVISION FOLLOWS THE GOAL YOU GIVE ITS LEADER. YOUR ORDERS REACH HIM LATE.", x, y, Style.Dim, 1);
        y += ui.U(18);
        var divs = C.S.Divisions.ToList();
        foreach (var d in divs)
        {
            var box = new Rectangle(x, y, w, ui.U(62));
            ui.Rect(box, Color.White * 0.04f);
            string temper = C.TemperOf(d).ToUpperInvariant();
            ui.Text($"{C.DivisionName(d).ToUpperInvariant()}", box.X + ui.U(6), box.Y + ui.U(5), Style.Accent, 1);
            string rose = C.RoseFrom(C.S.Characters.FirstOrDefault(ch => ch.Id == d.LeaderChar));
            ui.Text($"{temper}, COMMAND {C.CommandOf(d)}" + (rose != "" ? " . " + rose : ""), box.X + ui.U(6), box.Y + ui.U(17), rose != "" ? Kit.Gilt : Style.Dim, 1);
            var comps = C.CompaniesOf(d).ToList();
            string names = string.Join(", ", comps.Select(c => $"{c.Name.ToUpperInvariant()} ({c.Men})"));
            if (names.Length > 86) names = names[..84] + "..";
            ui.Text(comps.Count == 0 ? "NO COMPANIES YET: GIVE HIM SOME ON THE RIGHT." : $"{comps.Sum(c => c.Men)} MEN: {names}", box.X + ui.U(6), box.Y + ui.U(31), comps.Count == 0 ? Style.Warn : Style.Text, 1);
            if (ui.Hover(new Rectangle(box.X, box.Y, ui.U(300), ui.U(28))))
                note = temper switch { "BOLD" => "BOLD: CHARGES EARLY AND CHASES FAR.", "CAUTIOUS" => "CAUTIOUS: KEEPS A SHORT LEASH AND LETS THE ENEMY COME.", _ => "STEADY: KEEPS FORMATION AND WAITS FOR CONTACT." }
                    + " COMMAND MAKES HIM QUICKER TO ACT AND TO HEAR ORDERS.";
            int gi = Array.IndexOf(Campaign.DivisionGoals, d.Goal);
            if (ui.Button(new Rectangle(box.Right - ui.U(250), box.Y + ui.U(5), ui.U(150), ui.U(22)), "STARTS: " + GoalText(d.Goal)))
                C.SetDivisionGoal(d, Campaign.DivisionGoals[(gi + 1) % Campaign.DivisionGoals.Length]);
            if (ui.Button(new Rectangle(box.Right - ui.U(94), box.Y + ui.U(5), ui.U(88), ui.U(22)), "DISBAND")) C.DisbandDivision(d);
            y += ui.U(66);
        }
        if (divs.Count < Campaign.MaxDivisions)
        {
            y += ui.U(6);
            ui.Text("NEW DIVISION UNDER:", x, y, Style.Accent, 1);
            y += ui.U(16);
            var leaders = C.DivisionLeaders().ToList();
            if (leaders.Count == 0) ui.Text("NOBODY FREE TO LEAD ONE. COMPANIONS AND COMPANY SERGEANTS CAN.", x, y, Style.Dim, 1);
            int i = 0;
            foreach (var (name, ch, co) in leaders)
            {
                var b = new Rectangle(x + i % 3 * ui.U(188), y + i / 3 * ui.U(26), ui.U(182), ui.U(22));
                if (b.Bottom > r.Bottom - ui.U(60)) break;
                if (ui.Button(b, name.ToUpperInvariant())) note = C.MakeDivision(ch, co) ?? "";
                if (ui.Hover(b)) note = ch >= 0 ? "A COMPANION: HE GROWS BETTER AT COMMAND WITH EVERY TWO BATTLES." : "A COMPANY SERGEANT: HIS COMMAND GROWS WITH HIS BATTLES (AT MOST 6).";
                i++;
            }
        }
        // Every company, and the division it fights in: click to move it to the next one.
        int cx = r.X + ui.U(600), cy = r.Y + ui.U(44);
        ui.Text("COMPANIES", cx, cy, Style.Accent, 1);
        cy += ui.U(18);
        foreach (var c in C.Player.Companies)
        {
            if (cy > r.Bottom - ui.U(80)) break;
            var row = new Rectangle(cx, cy, ui.U(380), ui.U(24));
            ui.Rect(row, Color.White * 0.04f);
            string nm = c.Name.ToUpperInvariant();
            ui.Text(nm.Length > 18 ? nm[..18] : nm, row.X + ui.U(6), row.Y + ui.U(7), Style.Text, 1);
            ui.Text($"{c.Men}", row.X + ui.U(150), row.Y + ui.U(7), Style.Dim, 1);
            var cur = divs.FirstOrDefault(d => d.Id == c.Division);
            string label = cur == null ? "RETINUE (YOU)" : C.DivisionName(cur).ToUpperInvariant();
            if (ui.Button(new Rectangle(row.Right - ui.U(196), row.Y + ui.U(2), ui.U(192), ui.U(20)), label.Length > 22 ? label[..22] : label, divs.Count > 0, "", cur != null))
            {
                int at = cur == null ? -1 : divs.IndexOf(cur);
                int next = at + 1 >= divs.Count ? -1 : divs[at + 1].Id;
                note = C.AssignDivision(c, next) ?? "";
            }
            cy += ui.U(27);
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), from == PanelKind.Party ? "BACK" : "CLOSE", note: ctl.Name(Act.DivisionsScreen))) panel = CameFrom(PanelKind.Divisions);
    }
}
