using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// MapScreen: party, characters, skills and upgrade panels.
partial class MapScreen
{
    void PartyPanel()
    {
        var list = C.Player.Companies;
        int rh = list.Count > 8 ? 42 : 46, rstep = list.Count > 8 ? 45 : 50;
        var r = Modal(1000, list.Count > 8 ? 610 : 560, "YOUR COMPANY");
        int x = r.X + ui.U(20), y = r.Y + ui.U(46);
        foreach (var c in list.ToList())
        {
            var row = new Rectangle(x, y, ui.U(660), ui.U(rh));
            ui.Rect(row, Color.White * 0.04f);
            ui.Portrait(c.Type, new Rectangle(row.X + ui.U(4), row.Y + ui.U(2), ui.U(44), ui.U(rh - 4)));
            var nameBox = new Rectangle(row.X + ui.U(52), row.Y + ui.U(3), ui.U(150), ui.U(12));
            if (renaming == c) { ui.Rect(nameBox, Style.Accent * 0.25f); ui.Text(renameText + (time % 1 < 0.5f ? "_" : ""), row.X + ui.U(54), row.Y + ui.U(5), Style.Accent, 1); }
            else ui.Text(c.Name.ToUpperInvariant(), row.X + ui.U(54), row.Y + ui.U(5), Style.Text, 1);
            if (renaming == null && ui.Clicked(nameBox)) { renaming = c; renameText = c.Name.ToUpperInvariant(); Ui.Typed.Clear(); }
            if (ui.Hover(nameBox) && renaming == null) note = "CLICK TO RENAME (ENTER KEEPS IT)";
            var info = new Rectangle(row.X + ui.U(52), row.Y + ui.U(15), ui.U(150), ui.U(rh - 16));
            string role = Rules.RoleOf(c).ToUpperInvariant();
            ui.Text($"{role}{(c.Hero != "" ? "  " + c.Hero.ToUpperInvariant() + " (HERO)" : c.Officer != "" ? "  " + c.Officer.ToUpperInvariant() : "")}", row.X + ui.U(54), row.Y + ui.U(17), Style.Accent, 1);
            ui.Text($"{c.Men}{(c.Wounded > 0 ? $"+{c.Wounded}W" : "")} MEN  {Rules.Places(c):0}/{Rules.Cap(Rules.RoleOf(c)):0} PLACES", row.X + ui.U(54), row.Y + ui.U(29), Style.Dim, 1);
            ui.Rect(new Rectangle(row.X + ui.U(186), row.Y + ui.U(5), ui.U(10), ui.U(8)), Style.Friendly);
            for (int h = 0; h < c.Honours.Count; h++) ui.Rect(new Rectangle(row.X + ui.U(186) - (h + 1) * ui.U(8), row.Y + ui.U(17), ui.U(5), ui.U(5)), Style.Accent);
            if (ui.Hover(info)) note = CompanyNote(c);
            ui.Bar(new Rectangle(row.X + ui.U(54), row.Y + ui.U(rh - 6), ui.U(140), ui.U(3)), c.Cohesion / 30f, Style.Ok);
            // The troop stacks: click one for its promotion and moves.
            int sx = row.X + ui.U(206), sw = ui.U(76);
            foreach (var st in c.Stacks.OrderByDescending(t => t.Type.Tier).Take(4).ToList())
            {
                var chip = new Rectangle(sx, row.Y + ui.U(2), sw, ui.U(rh - 4));
                bool sel = upStack == st;
                ui.Panel(chip, st.Ready > 0 ? Style.Accent : sel ? Style.Text : Style.PanelEdge, st.Ready > 0 || sel ? 2 : 1);
                ui.Text($"{Style.Numerals[st.Type.Tier]} {st.Men}{(st.Wounded > 0 ? $"+{st.Wounded}" : "")}", chip.X + ui.U(4), chip.Y + ui.U(4), Style.Text, 1);
                string tn = st.Type.Name.ToUpperInvariant();
                ui.Text(tn.Length > 11 ? tn[..11] : tn, chip.X + ui.U(4), chip.Y + ui.U(15), Style.Dim, 1);
                ui.Bar(new Rectangle(chip.X + ui.U(4), chip.Y + ui.U(27), sw - ui.U(8), ui.U(3)), st.PoolCap > 0 ? st.Xp / st.PoolCap : 1, st.Ready > 0 ? Style.Accent : Style.Friendly);
                if (st.Ready > 0) ui.Text($"{st.Ready} READY", chip.X + ui.U(4), chip.Y + ui.U(32), Style.Accent, 1);
                if (ui.Hover(chip)) note = $"{st.Men} {tn}: POOL {st.Xp:0}/{st.PoolCap:0} XP, {st.Type.UpgradeXp} XP A MAN. CLICK TO PROMOTE, MOVE OR SPLIT.";
                if (ui.Clicked(chip)) { upgrading = c; upStack = st; panel = PanelKind.Upgrade; note = ""; }
                sx += sw + ui.U(3);
            }
            if (c.Stacks.Count > 4) ui.Text($"+{c.Stacks.Count - 4}", sx, row.Y + ui.U(16), Style.Dim, 1);
            int bx = row.X + ui.U(530), bw = ui.U(62), bh = ui.U(18);
            if (ui.Button(new Rectangle(bx, row.Y + ui.U(2), bw, bh), "UP", list.IndexOf(c) > 0)) C.MoveUp(c);
            if (ui.Button(new Rectangle(bx + bw + 4, row.Y + ui.U(2), bw, bh), "SPLIT", c.Men >= 2)) note = C.Split(c) ?? "";
            var mate = list.FirstOrDefault(o => Rules.CanMerge(c, o));
            if (ui.Button(new Rectangle(bx, row.Y + ui.U(22), bw, bh), "MERGE", mate != null)) C.Merge(c, mate!);
            if (ui.Button(new Rectangle(bx + bw + 4, row.Y + ui.U(22), bw, bh), "DISBAND", list.Count > 1)) C.Disband(c);
            y += ui.U(rstep);
        }
        // The captain and perks.
        int px = r.X + ui.U(700), py = r.Y + ui.U(50);
        var cap = C.Captain;
        ui.Text("THE CAPTAIN  " + C.Stage, px, py, Style.Accent);
        int picks = Skills.PicksTotal(cap);
        ui.Text($"RENOWN {cap.Renown:0}   PERKS {cap.Perks.Count}" + (picks > 0 ? $"   {picks} TO CHOOSE" : ""), px, py + ui.U(20), picks > 0 ? Style.Accent : Style.Text, 1);
        ui.Text($"{Rules.CompanySlots(cap)} COMPANIES, {Rules.MenLimit(cap)} MEN AT MOST", px, py + ui.U(32), Style.Dim, 1);
        // The renown ladder, the same line the map HUD shows: what the next rung buys and how close it is.
        var (unlock, at) = Rules.NextRenownUnlock(cap);
        ui.Bar(new Rectangle(px, py + ui.U(44), ui.U(260), ui.U(4)), (cap.Renown - RenownStep(cap, at)) / MathF.Max(1, at - RenownStep(cap, at)), Style.Accent);
        ui.Text($"NEXT: {unlock} AT {at:0} RENOWN", px, py + ui.U(50), Style.Dim, 1);
        py += ui.U(68);
        // The skills in brief; the tree itself is its own screen (K).
        for (int i = 0; i < Skills.All.Count; i++)
        {
            var sk = Skills.All[i];
            int sx = px + (i % 2) * ui.U(132), sy = py + i / 2 * ui.U(14);
            int open = Skills.Picks(cap, sk.Id);
            ui.Text($"{sk.Name.ToUpperInvariant()} {C.Skill(sk.Id):0}" + (open > 0 ? " *" : ""), sx, sy, open > 0 ? Style.Accent : Style.Dim, 1);
        }
        py += ui.U(60);
        if (ui.Button(new Rectangle(px, py, ui.U(260), ui.U(26)), "SKILLS AND PERKS", true, "K")) { panel = PanelKind.Skills; note = ""; }
        py += ui.U(30);
        if (ui.Button(new Rectangle(px, py, ui.U(260), ui.U(26)), $"DIVISIONS ({C.S.Divisions.Count})", true, "V")) { panel = PanelKind.Divisions; note = ""; }
        py += ui.U(30);
        // Prisoners: turn them to your cause (green, cohesion 0) or let them go.
        py += ui.U(8);
        ui.Text($"PRISONERS {C.PrisonerCount}", px, py, Style.Accent);
        py += ui.U(20);
        foreach (var g in C.Player.Prisoners.ToList())
        {
            if (py > r.Bottom - ui.U(90)) break;
            if (ui.Button(new Rectangle(px, py, ui.U(260), ui.U(22)), $"RECRUIT {g.Count} {Troops.Get(g.Troop).Name.ToUpperInvariant()}")) note = C.RecruitPrisoners(g) ?? "";
            py += ui.U(26);
        }
        if (C.PrisonerCount > 0 && ui.Button(new Rectangle(px, py, ui.U(260), ui.U(22)), "FREE THEM ALL (+RENOWN)")) C.FreePrisoners();
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE")) { panel = PanelKind.None; renaming = null; }
    }

    static string OpinionWord(float op) => op switch { >= 50 => "THE WORLD", >= 20 => "WELL", > -20 => "LITTLE", > -50 => "ILL", _ => "THE WORST" };
    static Color OpinionColor(float op) => op >= 20 ? Style.Ok : op > -20 ? Style.Dim : Style.Bad;
    static string RoleTextOf(string role) => Campaign.RoleText.TryGetValue(role, out var t) ? t : "";

    int charTab;

    /// The Characters screen (3.2): lords by realm with their traits, what they think of you and why; and your household.
    void CharactersPanel()
    {
        var r = Modal(1000, 600, "LORDS AND COMPANIONS");
        int realms = C.S.Factions.Count;
        int tw = ui.U(200), tx = r.X + (r.Width - (realms + 1) * (tw + ui.U(6))) / 2;
        for (int i = 0; i <= realms; i++)
        {
            string label = i < realms ? C.FactionNames[i].ToUpperInvariant() : "YOUR HOUSEHOLD";
            if (ui.Button(new Rectangle(tx + i * (tw + ui.U(6)), r.Y + ui.U(46), tw, ui.U(24)), label, true, "", charTab == i)) { charTab = i; note = ""; }
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
            string state = ch.Kind == "companion" ? $"{ch.Role.ToUpperInvariant()} ({RoleTextOf(ch.Role)})"
                : party == null ? "" : party.Captive ? "YOUR PRISONER" : party.Down ? "SCATTERED" : $"{party.Men} MEN, {party.Task.ToUpperInvariant()}";
            ui.Text(ch.Name.ToUpperInvariant(), box.X + ui.U(6), box.Y + ui.U(4), Style.Text, 1);
            ui.TextR(state, box.Right - ui.U(6), box.Y + ui.U(4), ch.Kind == "companion" && C.S.Hours < ch.WoundedUntil ? Style.Warn : Style.Dim, 1);
            ui.Text(string.Join(", ", ch.Traits).ToUpperInvariant() + $"  AGE {ch.Age}", box.X + ui.U(6), box.Y + ui.U(16), Style.Dim, 1);
            // The opinion bar: centre is indifference.
            var bar = new Rectangle(box.X + ui.U(6), box.Y + ui.U(29), box.Width - ui.U(12), ui.U(4));
            ui.Rect(bar, Color.White * 0.08f);
            int mid = bar.X + bar.Width / 2, len = (int)(bar.Width / 2f * MathF.Abs(op) / 100f);
            ui.Rect(new Rectangle(op >= 0 ? mid : mid - len, bar.Y, len, bar.Height), OpinionColor(op));
            ui.Rect(new Rectangle(mid, bar.Y - 1, 1, bar.Height + 2), Style.Text);
            if (ui.Hover(box))
            {
                var why = C.MemoriesOf(ch, C.Me).Take(3).Select(m => $"{m.What.ToUpperInvariant()} ({m.Weight:+0;-0}, DAY {m.Day})").ToList();
                note = $"THINKS {OpinionWord(op)} OF YOU ({op:+0;-0;0})" + (why.Count > 0 ? ": " + string.Join("; ", why) : "");
            }
            i2++;
        }
        if (charTab == realms) Vassals(r, r.Y + ui.U(82) + ((list.Count + 1) / 2) * (rowH + ui.U(4)) + ui.U(12));
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE (L)")) panel = PanelKind.None;
    }

    /// The captain's skill tree (F4): each skill's level and what raises it, and the four perks on its branch,
    /// one more of which may be chosen for every 25 points.
    void SkillsPanel()
    {
        var r = Modal(1040, 640, "SKILLS AND PERKS");
        var cap = C.Captain;
        int picks = Skills.PicksTotal(cap);
        ui.TextC(picks > 0 ? $"{picks} PERK{(picks == 1 ? "" : "S")} TO CHOOSE" : "SKILLS RISE BY USE. EVERY 25 POINTS OPENS A PERK ON THAT BRANCH.",
            r.X + r.Width / 2f, r.Y + ui.U(40), picks > 0 ? Style.Accent : Style.Dim, 1);
        int colW = (r.Width - ui.U(60)) / 2, blockH = ui.U(122);
        for (int i = 0; i < Skills.All.Count; i++)
        {
            var sk = Skills.All[i];
            int bx = r.X + ui.U(24) + (i % 2) * (colW + ui.U(12)), by = r.Y + ui.U(58) + i / 2 * blockH;
            var box = new Rectangle(bx, by, colW, blockH - ui.U(8));
            ui.Rect(box, Color.White * 0.04f);
            float lv = C.Skill(sk.Id);
            int open = Skills.Picks(cap, sk.Id);
            ui.Text($"{sk.Name.ToUpperInvariant()}  {lv:0}", bx + ui.U(8), by + ui.U(6), Style.Text, 2);
            if (open > 0) ui.TextR($"{open} TO CHOOSE", box.Right - ui.U(8), by + ui.U(8), Style.Accent, 1);
            var bar = new Rectangle(bx + ui.U(8), by + ui.U(26), colW - ui.U(16), ui.U(5));
            ui.Bar(bar, lv / Skills.Max, Style.Accent);
            for (int m = 1; m < 4; m++) ui.Rect(new Rectangle(bar.X + bar.Width * m / 4, bar.Y - 2, 1, bar.Height + 4), Style.Text * 0.6f);
            ui.Text($"RISES BY {sk.Rises}", bx + ui.U(8), by + ui.U(36), Style.Dim, 1);
            ui.Text(sk.Governs, bx + ui.U(8), by + ui.U(48), Style.Dim, 1);
            int pw = (colW - ui.U(16) - 3 * ui.U(4)) / 4;
            for (int k = 0; k < sk.Perks.Count && k < 4; k++)
            {
                var pk = sk.Perks[k];
                bool has = cap.Perks.Contains(pk.Id);
                var pr = new Rectangle(bx + ui.U(8) + k * (pw + ui.U(4)), by + ui.U(64), pw, ui.U(22));
                if (ui.Button(pr, pk.Name.ToUpperInvariant(), has || open > 0, "", has) && !has) note = C.TakePerk(pk.Id) ?? $"{pk.Name.ToUpperInvariant()} LEARNED";
                if (ui.Hover(pr)) note = $"{pk.Name.ToUpperInvariant()}: {pk.Text}" + (has ? " (TAKEN)" : open > 0 ? "" : $" (NEXT CHOICE AT {sk.Name.ToUpperInvariant()} {((int)(lv / Skills.Step) + 1) * (int)Skills.Step})");
            }
            ui.Text(string.Join("  ", sk.Perks.Where(p => cap.Perks.Contains(p.Id)).Select(p => p.Text)), bx + ui.U(8), by + ui.U(92), Style.Ok, 1);
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE (K)")) panel = PanelKind.None;
    }

    /// A company's story for the party screen: origin, honours, and its last fights.
    static string CompanyNote(CompanyRecord c)
    {
        var parts = new List<string>();
        parts.Add(c.Home != "" ? $"RAISED IN {c.Home.ToUpperInvariant()} ({c.Culture.ToUpperInvariant()})" : "NO HOME");
        parts.Add($"{c.Battles} BATTLES, {c.Kills} KILLS");
        if (c.Honours.Count > 0) parts.Add("HONOURS: " + string.Join(", ", c.Honours.Select(h => Honours.Get(h) is HonourDef d ? $"{d.Name.ToUpperInvariant()} ({d.Text.ToUpperInvariant()})" : h)));
        if (c.Traits.Count > 0) parts.Add($"{c.Officer.ToUpperInvariant()}: " + string.Join(", ", c.Traits.Select(t => t.ToUpperInvariant())));
        if (c.Hero != "") parts.Add($"LED BY {c.Hero.ToUpperInvariant()}");
        if (c.Ledger.Count > 0) parts.Add("LAST: " + string.Join("; ", c.Ledger.TakeLast(3).Reverse().Select(l => $"DAY {l.Day} {(l.Won ? "WON" : "LOST")} VS {l.Foe.ToUpperInvariant()}, {l.Kills} KILLS, {l.Lost} LOST")));
        return string.Join(". ", parts);
    }

    /// The promotion picker for one troop stack (2.2): each branch with what it costs to promote 1, 5 or every ready man,
    /// and the reason when it cannot; plus moving or splitting the stack.
    void UpgradePanel()
    {
        var c = upgrading!;
        var st = upStack;
        if (st == null || !c.Stacks.Contains(st) || !C.Player.Companies.Contains(c)) { panel = PanelKind.Party; upStack = null; return; }
        var options = st.Type.UpgradesTo;
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
            foreach (var (label, value) in new[] { ("HEALTH", $"{t.MaxHp:0}"), ("ARMOUR", $"{t.Armor:0}"), ("DAMAGE", $"{t.Damage:0}"), ("REACH", $"{t.Reach:0.0} M"),
                         t.Range > 0 ? ("RANGE", $"{t.Range:0} M") : ("SPEED", $"{t.Speed:0.0}"), ("HORSE", t.Mount?.Name.ToUpperInvariant() ?? "-") })
            {
                ui.Text(label, card.X + ui.U(14), sy, Style.Dim, 1);
                ui.TextR(value, card.Right - ui.U(14), sy, Style.Text, 1);
                sy += ui.U(14);
            }
            if (Rules.TroopRole(to) != Rules.RoleOf(c)) ui.TextC($"JOINS A {Rules.TroopRole(to).ToUpperInvariant()} COMPANY", card.X + cw / 2f, card.Y + ui.U(210), Style.Warn, 1);
            ui.TextC(one > 0 ? $"{cost1} CROWNS A MAN" : reason, card.X + cw / 2f, card.Y + ui.U(226), one > 0 ? Style.Text : Style.Warn, 1);
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

    /// Divisions (docs/COMBAT_OVERHAUL.md 1.5): group companies under a companion or a sergeant, who leads them in battle
    /// by goals and hears your orders late. Companies in no division are your retinue: they obey you at once.
    void DivisionsPanel()
    {
        var r = Modal(1000, 600, "DIVISIONS");
        int x = r.X + ui.U(20), y = r.Y + ui.U(44), w = ui.U(560);
        ui.Text("A DIVISION FOLLOWS A GOAL YOU GIVE ITS LEADER. ORDERS REACH HIM LATE, LATER THE FURTHER OFF HE IS.", x, y, Style.Dim, 1);
        y += ui.U(18);
        var divs = C.S.Divisions.ToList();
        foreach (var d in divs)
        {
            var box = new Rectangle(x, y, w, ui.U(62));
            ui.Rect(box, Color.White * 0.04f);
            string temper = C.TemperOf(d).ToUpperInvariant();
            ui.Text($"{C.DivisionName(d).ToUpperInvariant()}", box.X + ui.U(6), box.Y + ui.U(5), Style.Accent, 1);
            ui.Text($"{temper}, COMMAND {C.CommandOf(d)}", box.X + ui.U(6), box.Y + ui.U(17), Style.Dim, 1);
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
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "BACK (V)")) panel = PanelKind.Party;
    }
}
