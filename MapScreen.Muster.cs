using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the party screen (P) as a muster roll: a card per company (portrait, men, places, cohesion, its troop stacks to
/// promote, and its moves), the open company slots, and the captain down the right.
partial class MapScreen
{
    void PartyPanel()
    {
        var list = C.Player.Companies;
        var cap = C.Captain;
        var r = Modal(1060, 620, "YOUR COMPANY");
        int side = ui.U(250), gap = ui.U(10);
        var grid = new Rectangle(r.X + ui.U(20), r.Y + ui.U(30), r.Width - side - ui.U(50), r.Height - ui.U(96));
        // Real companies get the room; open slots only fill out the last row. A summary of the whole company sits beneath.
        int slots = Math.Max(list.Count, Rules.CompanySlots(cap));
        int cols = list.Count <= 8 ? 4 : list.Count <= 10 ? 5 : 6, rows = Math.Max(1, (list.Count + cols - 1) / cols);
        int summary = rows == 1 ? ui.U(120) : 0;
        int cw = (grid.Width - (cols - 1) * gap) / cols, ch = Math.Min(ui.U(rows == 1 ? 290 : 330), (grid.Height - summary - (rows - 1) * gap) / rows);
        for (int i = 0; i < Math.Min(slots, rows * cols); i++)
        {
            var cr = new Rectangle(grid.X + i % cols * (cw + gap), grid.Y + i / cols * (ch + gap), cw, ch);
            if (i < list.Count) MusterCard(list[i], cr, i);
            else OpenSlot(cr);
        }
        if (summary > 0) MusterSummary(new Rectangle(grid.X, grid.Y + ch + gap, grid.Width, grid.Bottom - grid.Y - ch - gap), slots);
        MusterSide(new Rectangle(r.Right - side - ui.U(20), r.Y + ui.U(30), side, r.Height - ui.U(96)));
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.Menu))) { panel = PanelKind.None; renaming = null; }
    }

    /// One company: portrait, badges, name (click to rename), men, places, cohesion, stacks (click to promote) and its moves.
    void MusterCard(CompanyRecord c, Rectangle r, int index)
    {
        var list = C.Player.Companies;
        bool ready = c.Ready > 0, hover = ui.Hover(r);
        ui.Rect(new Rectangle(r.X + ui.U(2), r.Y + ui.U(3), r.Width, r.Height), Color.Black * 0.35f);
        ui.VGrad(r, hover ? new Color(40, 40, 46) : Kit.Iron, Kit.IronDark, 3);
        var port = new Rectangle(r.X + ui.U(3), r.Y + ui.U(3), r.Width - ui.U(6), Math.Clamp(r.Height - ui.U(196), ui.U(40), ui.U(110)));
        ui.VGrad(port, Kit.Mul(Style.Sides[2].Cloth, 0.55f), new Color(20, 18, 16), 3);
        ui.Rect(new Rectangle(port.X, port.Bottom - ui.U(8), port.Width, ui.U(8)), Color.Black * 0.25f);
        ui.Portrait(c.Type, port, (port.Height >= ui.U(84) ? 2 : 1) * ui.S);
        ui.Frame(r, ready ? Kit.Gilt : Color.Black * 0.6f, ui.U(ready ? 2 : 1));

        // Badges: index, tier, honours.
        var badge = new Rectangle(r.X + ui.U(5), r.Y + ui.U(5), ui.U(14), ui.U(14));
        ui.Rect(badge, Color.Black * 0.7f);
        ui.TextC($"{index + 1}", badge.Center.X + ui.U(1), badge.Y + ui.U(4), Style.Text, 1);
        string tier = Style.Numerals[Math.Clamp(c.Type.Tier, 0, 7)];
        var tb = new Rectangle(r.Right - ui.U(12) - ui.TW(tier, 1), r.Y + ui.U(5), ui.TW(tier, 1) + ui.U(7), ui.U(14));
        ui.Rect(tb, Color.Black * 0.7f); ui.Frame(tb, Kit.Gilt * 0.7f, ui.U(1));
        ui.TextC(tier, tb.Center.X + ui.U(1), tb.Y + ui.U(4), Kit.Gilt, 1);
        for (int h = 0; h < c.Honours.Count && h < 5; h++) ui.Icon("star", r.X + ui.U(5), r.Y + ui.U(22 + h * 10), Kit.Gilt, 1);
        if (ready) ui.Chip(port.X + ui.U(3), port.Bottom - ui.U(16), $"{c.Ready} READY", Kit.Gilt, "up");

        int x = r.X + ui.U(7), w = r.Width - ui.U(14), y = port.Bottom + ui.U(5);
        var nameBox = new Rectangle(x - ui.U(2), y - ui.U(2), w + ui.U(4), ui.U(12));
        string nm = c.Name.ToUpperInvariant();
        int fit = Math.Max(4, w / (6 * ui.S));
        if (renaming == c) { ui.Rect(nameBox, Style.Accent * 0.25f); ui.Text(renameText + (time % 1 < 0.5f ? "_" : ""), x, y, Style.Accent, 1); }
        else ui.Text(nm.Length > fit ? nm[..fit] : nm, x, y, Style.Text, 1);
        if (renaming == null && ui.Clicked(nameBox)) { renaming = c; renameText = nm; Ui.Typed.Clear(); }
        if (ui.Hover(nameBox) && renaming == null) note = "CLICK TO RENAME (ENTER KEEPS IT)";
        y += ui.U(12);
        string who = c.Hero != "" ? c.Hero.ToUpperInvariant() + " (HERO)" : c.Officer != "" ? c.Officer.ToUpperInvariant() : "";
        string role = Rules.RoleOf(c).ToUpperInvariant() + (who != "" ? " . " + who : "");
        ui.Text(role.Length > fit ? role[..fit] : role, x, y, Kit.Gilt, 1);
        y += ui.U(12);
        var info = new Rectangle(x, y, w, ui.U(34));
        ui.Text($"{c.Men}{(c.Wounded > 0 ? $"+{c.Wounded}W" : "")} MEN", x, y, Style.Text, 1);
        ui.TextR($"{Rules.Places(c):0}/{Rules.Cap(Rules.RoleOf(c)):0}", r.Right - ui.U(7), y, Style.Dim, 1);
        y += ui.U(10);
        ui.Pips(new Rectangle(x, y, w, ui.U(6)), Rules.Places(c) / MathF.Max(1, Rules.Cap(Rules.RoleOf(c))), Style.Sides[2].Ui, 10);
        y += ui.U(9);
        ui.Text("COHESION", x, y, Style.Dim * 0.8f, 1);
        var grade = Battle.GradeOf(Battle.DrillOf(c)); // what its drill makes it in battle (docs/MELEE.md D1)
        ui.TextR(Battle.GradeName(grade).ToUpperInvariant(), r.Right - ui.U(7), y, grade >= Grade.Veteran ? Kit.Gilt : grade == Grade.Trained ? Style.Text : Style.Dim, 1);
        y += ui.U(9);
        ui.Gauge(new Rectangle(x, y, w, ui.U(5)), c.Cohesion / 30f, c.Cohesion >= 15 ? Style.Ok : c.Cohesion >= 8 ? Style.Warn : Style.Bad);
        y += ui.U(9);
        if (ui.Hover(info) || ui.Hover(port)) note = CompanyNote(c);
        // The roll (docs/LEGACY.md L1): click for the named men and the fallen.
        var roll = new Rectangle(x - ui.U(2), y - ui.U(1), w + ui.U(4), ui.U(12));
        bool rh = ui.Hover(roll);
        if (rh) ui.Rect(roll, Color.White * 0.06f);
        ui.Icon("flag", x, y, Kit.Gilt, 1);
        int lost = C.FallenOf(c).Count();
        string rl = $"ROLL {c.Roll.Count}" + (lost > 0 ? $", {lost} FALLEN" : "");
        ui.Text(rl.Length > fit - 2 ? rl[..(fit - 2)] : rl, x + ui.U(11), y, rh ? Style.Text : Style.Dim, 1);
        if (rh) note = c.Roll.Count > 0 ? string.Join(", ", c.Roll.Select(m => m.Name.ToUpperInvariant())) + ". CLICK FOR THE ROLL." : "NO NAMED MEN YET. MEN EARN NAMES IN BATTLE.";
        if (ui.Clicked(roll)) { rollOf = c; panel = PanelKind.Roll; note = ""; }
        y += ui.U(12);

        // The troop stacks: click one for its promotion and moves.
        int bh = ui.U(16), stackRoom = r.Bottom - ui.U(6) - 2 * bh - ui.U(4) - y, sh = ui.U(19);
        var stacks = c.Stacks.OrderByDescending(t => t.Type.Tier).ToList();
        int show = Math.Max(0, Math.Min(stacks.Count, stackRoom / sh));
        if (show < stacks.Count && show > 0) show--;
        for (int k = 0; k < show; k++)
        {
            var st = stacks[k];
            var chip = new Rectangle(x, y, w, sh - ui.U(2));
            bool sel = upStack == st, h2 = ui.Hover(chip);
            ui.Rect(chip, st.Ready > 0 ? Kit.Gilt * 0.16f : Color.White * (h2 ? 0.08f : 0.035f));
            ui.Frame(chip, st.Ready > 0 ? Kit.Gilt * 0.8f : sel || h2 ? Style.Text * 0.6f : Color.Black * 0.5f, ui.U(1));
            string tn = st.Type.Name.ToUpperInvariant();
            int tf = Math.Max(3, (w - ui.U(46)) / (6 * ui.S));
            ui.Text(Style.Numerals[st.Type.Tier], chip.X + ui.U(3), chip.Y + ui.U(3), Kit.Gilt, 1);
            ui.Text(tn.Length > tf ? tn[..tf] : tn, chip.X + ui.U(18), chip.Y + ui.U(3), Style.Text, 1);
            ui.TextR($"{st.Men}{(st.Wounded > 0 ? $"+{st.Wounded}" : "")}", chip.Right - ui.U(3), chip.Y + ui.U(3), Style.Dim, 1);
            ui.Bar(new Rectangle(chip.X + ui.U(3), chip.Bottom - ui.U(4), chip.Width - ui.U(6), ui.U(2)), st.PoolCap > 0 ? st.Xp / st.PoolCap : 1, st.Ready > 0 ? Kit.Gilt : Style.Friendly);
            if (h2) note = $"{st.Men} {tn}: POOL {st.Xp:0}/{st.PoolCap:0} XP, {st.Type.UpgradeXp} XP A MAN. {(st.Ready > 0 ? $"{st.Ready} READY. " : "")}CLICK TO PROMOTE, MOVE OR SPLIT.";
            if (ui.Clicked(chip)) { upgrading = c; upStack = st; panel = PanelKind.Upgrade; note = ""; }
            y += sh;
        }
        if (show < stacks.Count) ui.Text($"+{stacks.Count - show} MORE STACKS", x, y, Style.Dim, 1);

        // Moves, two by two along the foot.
        int bw = (w - ui.U(3)) / 2, by = r.Bottom - ui.U(5) - 2 * bh - ui.U(3);
        var mate = list.FirstOrDefault(o => Rules.CanMerge(c, o));
        if (ui.Button(new Rectangle(x, by, bw, bh), "UP", list.IndexOf(c) > 0)) C.MoveUp(c);
        if (ui.Button(new Rectangle(x + bw + ui.U(3), by, bw, bh), "SPLIT", c.Men >= 2)) note = C.Split(c) ?? "";
        if (ui.Button(new Rectangle(x, by + bh + ui.U(3), bw, bh), "MERGE", mate != null)) C.Merge(c, mate!);
        if (ui.Button(new Rectangle(x + bw + ui.U(3), by + bh + ui.U(3), bw, bh), "DISBAND", list.Count > 1)) C.Disband(c);
        ui.Block(r);
    }

    /// The company the roll panel shows.
    CompanyRecord? rollOf;

    /// A company's roll (docs/LEGACY.md L1): its named men, what each is known for, and the men it has lost.
    /// A circle's spellbook and master on its roll (docs/MAGIC_AURA.md 3.2-3.3): click a known spell to prepare it or set it
    /// aside, click one it could learn to learn it. Returns the y below it.
    int Spellbook(CompanyRecord c, int x, int y, int w)
    {
        y += ui.U(3);
        var master = Campaign.MasterOf(c);
        string head = master != null ? $"{master.Name.ToUpperInvariant()}, {Campaign.MasterTitle(c).ToUpperInvariant()}, RANK {Campaign.MasterRank(master)}"
            : C.Leaderless(c) ? $"LEADERLESS FOR {(c.LeaderlessUntil - C.S.Hours) / 24:0} MORE DAYS: NO GREAT WORKING" : "NO MASTER YET";
        ui.Text("THE SPELLBOOK", x, y, Kit.Gilt, 2);
        ui.TextR(ui.Fit(head, w - ui.U(150)), x + w, y + ui.U(4), master != null ? Kit.Gilt : Style.Warn, 1);
        y += ui.U(20);
        var prep = Campaign.Prepared(c);
        ui.Text($"PREPARED {prep.Length} OF {Campaign.PrepareSlots(c)}. CLICK TO PREPARE OR SET ASIDE.", x, y, Style.Dim, 1);
        y += ui.U(12);
        var known = Campaign.Known(c);
        int bw = (w - ui.U(8)) / 3;
        for (int i = 0; i < known.Count; i++)
        {
            var a = Abilities.Get(known[i]);
            bool on = prep.Contains(a.Id);
            var b = new Rectangle(x + i % 3 * (bw + ui.U(4)), y + i / 3 * ui.U(21), bw, ui.U(18));
            if (ui.Button(b, ui.Fit((on ? "* " : "") + a.Name.ToUpperInvariant(), b.Width - ui.U(8)), true)) note = C.Prepare(a.Id, !on) ?? a.Line.ToUpperInvariant();
            if (ui.Hover(b)) note = a.Line.ToUpperInvariant() + (a.Great ? " ONCE A BATTLE." : "");
        }
        y += (known.Count + 2) / 3 * ui.U(21) + ui.U(4);
        var learn = Campaign.Learnable(c).ToList();
        if (c.Spells.Count < Campaign.MaxLearned && learn.Count > 0)
        {
            int cost = Campaign.LearnCost * (c.Spells.Count + 1);
            ui.Text($"LEARN ONE MORE ({cost} CROWNS; A TEACHER: {(Campaign.CircleTradition(c) == "faith" ? "A PRIEST" : "A CUNNING ONE")}, A SHRINE, STONES OR A HERMIT)", x, y, Style.Dim, 1);
            y += ui.U(12);
            for (int i = 0; i < learn.Count && i < 6; i++)
            {
                var a = learn[i];
                var b = new Rectangle(x + i % 3 * (bw + ui.U(4)), y + i / 3 * ui.U(21), bw, ui.U(18));
                if (ui.Button(b, ui.Fit("LEARN " + a.Name.ToUpperInvariant(), b.Width - ui.U(8)), C.CannotLearn(a.Id) == null)) note = C.LearnSpell(a.Id) ?? $"{c.Name.ToUpperInvariant()} LEARN {a.Name.ToUpperInvariant()}.";
                if (ui.Hover(b)) note = (C.CannotLearn(a.Id) is string why ? why + ". " : "") + a.Line.ToUpperInvariant();
            }
            y += (Math.Min(6, learn.Count) + 2) / 3 * ui.U(21);
        }
        ui.Divider(x, y + ui.U(2), w, Kit.Gilt * 0.5f);
        return y + ui.U(10);
    }

    void RollPanel()
    {
        var c = rollOf;
        if (c == null) { panel = PanelKind.Party; return; }
        var r = Modal(760, 560, c.Name.ToUpperInvariant());
        int x = r.X + ui.U(24), w = r.Width - ui.U(48), y = r.Y + ui.U(34);
        int fit = Math.Max(10, w / (6 * ui.S));
        string Cut(string s, int n) => s.Length > n ? s[..n] : s;
        string record = $"{Campaign.N(c.Battles, "BATTLE")}, {Campaign.N(c.Kills, "KILL")}.";
        ui.Text(c.Home != "" ? $"RAISED IN {c.Home.ToUpperInvariant()}. {record}" : record, x, y, Style.Dim, 1);
        ui.TextR($"FAME {Campaign.CompanyFame(c)}: {Campaign.FameWord(Campaign.CompanyFame(c))}", x + w, y, Kit.Gilt, 1);
        y += ui.U(11);
        if (c.BattleHonours.Count > 0 || c.LostHonours.Count > 0)
        {
            string hon = "BATTLE HONOURS: " + (c.BattleHonours.Count > 0 ? string.Join(", ", c.BattleHonours) : "NONE") + (c.LostHonours.Count > 0 ? ". TAKEN: " + string.Join(", ", c.LostHonours.Select(l => l.Name)) : "");
            ui.Text(Cut(hon.ToUpperInvariant(), fit), x, y, Kit.Gilt, 1);
            y += ui.U(11);
        }
        if (Rules.RoleOf(c) == "casters") y = Spellbook(c, x, y, w);
        y += ui.U(3);
        ui.Text("THE ROLL", x, y, Kit.Gilt, 2);
        ui.TextR($"{c.Roll.Count} OF {Balance.Get().RollSize} NAMED", x + w, y + ui.U(4), Style.Dim, 1);
        y += ui.U(20);
        if (c.Roll.Count == 0) { ui.Text("NO MAN HAS EARNED A NAME YET. THEY DO IT IN BATTLE.", x, y, Style.Dim, 1); y += ui.U(14); }
        foreach (var m in c.Roll.OrderByDescending(m => m.Battles))
        {
            if (y > r.Bottom - ui.U(150)) break;
            var row = new Rectangle(x - ui.U(4), y - ui.U(3), w + ui.U(8), ui.U(32));
            ui.Rect(row, Color.White * (ui.Hover(row) ? 0.07f : 0.035f));
            ui.Text(m.Name.ToUpperInvariant(), x, y, Style.Text, 1);
            string stat = $"{Campaign.N(m.Battles, "BATTLE")}, {Campaign.N(m.Kills, "KILL")}" + (m.Wounds > 0 ? $", {Campaign.N(m.Wounds, "WOUND")}" : "");
            ui.TextR(stat, x + w, y, Style.Dim, 1);
            ui.Text(Troops.Get(m.Troop).Name.ToUpperInvariant() + (m.Hurt ? " . HURT" : ""), x, y + ui.U(10), m.Hurt ? Style.Warn : Kit.Gilt, 1);
            ui.Text(Cut((m.Deeds.Count > 0 ? m.Deeds[^1] : "").ToUpperInvariant(), fit - 22), x + ui.U(150), y + ui.U(10), Style.Dim, 1);
            if (ui.Hover(row)) note = string.Join(" ", m.Deeds).ToUpperInvariant() + $" ON THE ROLL SINCE DAY {m.Joined}.";
            y += ui.U(36);
        }
        y += ui.U(6);
        ui.Divider(x, y, w, Kit.Gilt * 0.5f);
        y += ui.U(10);
        ui.Text("THE FALLEN", x, y, Kit.Gilt, 2);
        var fallen = C.FallenOf(c).Reverse().ToList();
        ui.TextR($"{fallen.Count}", x + w, y + ui.U(4), Style.Dim, 1);
        y += ui.U(20);
        if (fallen.Count == 0) ui.Text("NONE YET.", x, y, Style.Dim, 1);
        foreach (var m in fallen)
        {
            if (y > r.Bottom - ui.U(56)) { ui.Text("AND MORE BEFORE THEM.", x, y, Style.Dim, 1); break; }
            string how = m.Fate switch { "missing" => "MISSING", "left" => "LEFT THE COMPANY", _ => "FELL" };
            string at = m.DiedAt != "" ? $" AGAINST {m.DiedAt.ToUpperInvariant()}" : "";
            ui.Text(Cut($"{m.Name.ToUpperInvariant()}, {Troops.Get(m.Troop).Name.ToUpperInvariant()}", fit / 2), x, y, Style.Text, 1);
            ui.TextR(Cut($"{how}{at}, DAY {m.DiedDay}. {Campaign.N(m.Battles, "BATTLE")}", fit / 2), x + w, y, m.Fate == "fell" ? Style.Bad : Style.Dim, 1);
            y += ui.U(12);
        }
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "BACK", note: ctl.Name(Act.Menu))) panel = PanelKind.Party;
    }

    /// The whole company in one strip: men by role, and the numbers that keep it in the field.
    void MusterSummary(Rectangle box, int slots)
    {
        var c = Well(box, "THE COMPANY", Kit.Gilt);
        var list = C.Player.Companies;
        var roles = new (string Role, string Label, Color Col)[] { ("infantry", "FOOT", Style.Sides[2].Ui), ("spears", "SPEARS", Style.Friendly), ("archers", "BOWS", Style.Ok), ("horse", "HORSE", Style.Orange), ("casters", "MAGES", Kit.Gilt) };
        int total = Math.Max(1, list.Sum(k => k.Men)), bx = c.X;
        foreach (var (role, _, col) in roles)
        {
            int n = list.Where(k => Rules.RoleOf(k) == role).Sum(k => k.Men), bw = c.Width * n / total;
            if (bw > ui.U(2)) ui.Gauge(new Rectangle(bx, c.Y, bw - ui.U(2), ui.U(10)), 1, col);
            bx += bw;
        }
        int y = c.Y + ui.U(16), lx = c.X;
        foreach (var (role, label, col) in roles)
        {
            int n = list.Where(k => Rules.RoleOf(k) == role).Sum(k => k.Men);
            ui.Rect(new Rectangle(lx, y + ui.U(2), ui.U(5), ui.U(5)), col);
            ui.Text($"{label} {n}", lx + ui.U(9), y, n > 0 ? Style.Text : Style.Dim * 0.6f, 1);
            lx += c.Width / roles.Length;
        }
        y += ui.U(18);
        float bal = C.S.LastWeekBalance, coh = list.Count == 0 ? 0 : list.Average(k => k.Cohesion);
        var stats = new (string Icon, string Label, string Value, Color Col)[]
        {
            ("coin", "PURSE", $"{C.S.Gold:0}", Kit.Gilt), ("scroll", "LAST WEEK", $"{(bal >= 0 ? "+" : "")}{bal:0}", bal >= 0 ? Style.Ok : Style.Bad),
            ("heart", "WOUNDED", $"{C.Player.Wounded}", C.Player.Wounded > 0 ? Style.Warn : Style.Dim), ("horse", "HORSES", $"{C.Player.Horses}", Style.Text),
            ("shield", "COHESION", $"{coh:0}", coh >= 15 ? Style.Ok : Style.Warn), ("flag", "SLOTS", $"{list.Count}/{slots}", Style.Text),
        };
        int cw = c.Width / 3;
        for (int i = 0; i < stats.Length; i++)
        {
            var (icon, label, value, col) = stats[i];
            int sx = c.X + i % 3 * cw, sy = y + i / 3 * ui.U(14);
            ui.Icon(icon, sx, sy, Style.Dim, 1);
            ui.Text(label, sx + ui.U(12), sy, Style.Dim, 1);
            ui.TextR(value, sx + cw - ui.U(12), sy, col, 1);
        }
    }

    /// A company slot you have not filled: where the next company will stand.
    void OpenSlot(Rectangle r)
    {
        ui.Rect(r, Color.Black * 0.18f);
        for (int x = r.X; x < r.Right; x += ui.U(8)) { ui.Rect(new Rectangle(x, r.Y, ui.U(4), ui.U(1)), Color.White * 0.12f); ui.Rect(new Rectangle(x, r.Bottom - ui.U(1), ui.U(4), ui.U(1)), Color.White * 0.12f); }
        for (int y = r.Y; y < r.Bottom; y += ui.U(8)) { ui.Rect(new Rectangle(r.X, y, ui.U(1), ui.U(4)), Color.White * 0.12f); ui.Rect(new Rectangle(r.Right - ui.U(1), y, ui.U(1), ui.U(4)), Color.White * 0.12f); }
        ui.Icon("flag", r.Center.X - ui.U(7), r.Center.Y - ui.U(20), Style.Dim * 0.5f, 2);
        ui.TextC("OPEN", r.Center.X, r.Center.Y + ui.U(2), Style.Dim * 0.7f, 1);
        ui.TextC("RECRUIT OR HIRE", r.Center.X, r.Center.Y + ui.U(14), Style.Dim * 0.5f, 1);
    }

    /// The captain down the right of the muster: rank, renown, men and food, skills in brief, and the prisoners.
    void MusterSide(Rectangle s)
    {
        var cap = C.Captain;
        ui.Rect(s, Color.Black * 0.22f);
        ui.Frame(s, Color.White * 0.06f, ui.U(1));
        int x = s.X + ui.U(10), w = s.Width - ui.U(20), y = s.Y + ui.U(10);
        ui.Shield(new Rectangle(x, y, ui.U(36), ui.U(44)), Style.Sides[2]);
        ui.Text("THE CAPTAIN", x + ui.U(44), y + ui.U(2), Style.Text, 2);
        ui.Chip(x + ui.U(44), y + ui.U(22), C.Stage, Kit.Gilt, "crown");
        y += ui.U(52);
        int picks = Skills.PicksTotal(cap);
        ui.Text($"RENOWN {cap.Renown:0}", x, y, Style.Text, 1);
        ui.TextR($"PERKS {cap.Perks.Count}" + (picks > 0 ? $" (+{picks})" : ""), x + w, y, picks > 0 ? Kit.Gilt : Style.Dim, 1);
        y += ui.U(11);
        var (unlock, at) = Rules.NextRenownUnlock(cap);
        ui.Pips(new Rectangle(x, y, w, ui.U(7)), (cap.Renown - RenownStep(cap, at)) / MathF.Max(1, at - RenownStep(cap, at)), Kit.Gilt, 12);
        y += ui.U(10);
        string next = $"NEXT: {unlock} AT {at:0}";
        ui.Text(next.Length > w / (6 * ui.S) ? next[..(w / (6 * ui.S))] : next, x, y, Style.Dim, 1);
        y += ui.U(14);
        float rr = Math.Min(ui.U(21), w / 6f);
        int men = C.Player.Men, limit = Math.Max(1, Rules.MenLimit(cap));
        ui.Ring(new Vector2(x + w / 4f, y + rr), rr, men / (float)limit, men > limit * 0.95f ? Style.Warn : Style.Ok, $"{men}", "MEN");
        ui.Ring(new Vector2(x + 3 * w / 4f, y + rr), rr, MathF.Min(1, C.Player.Food / 14f), C.Player.Food < 3 ? Style.Bad : C.Player.Food < 6 ? Style.Warn : Style.Ok, $"{C.Player.Food:0}", "DAYS FOOD");
        y += (int)(2 * rr) + ui.U(10);
        ui.Text($"{C.Player.Companies.Count} OF {Rules.CompanySlots(cap)} COMPANIES, {limit} MEN AT MOST", x, y, Style.Dim, 1);
        y += ui.U(10);
        ui.Divider(x, y + ui.U(2), w, Kit.Gilt * 0.5f);
        y += ui.U(10);
        for (int i = 0; i < Skills.All.Count; i++)
        {
            var sk = Skills.All[i];
            int sx = x + i % 2 * (w / 2), sy = y + i / 2 * ui.U(12);
            int open = Skills.Picks(cap, sk.Id);
            string label = sk.Name.ToUpperInvariant();
            int lf = (w / 2 - ui.U(26)) / (6 * ui.S);
            if (label.Length > lf) label = label[..lf];
            ui.Text(label, sx, sy, open > 0 ? Kit.Gilt : Style.Dim, 1);
            ui.TextR($"{C.Skill(sk.Id):0}" + (open > 0 ? "*" : ""), sx + w / 2 - ui.U(6), sy, open > 0 ? Kit.Gilt : Style.Text, 1);
        }
        y += (Skills.All.Count + 1) / 2 * ui.U(12) + ui.U(6);
        if (ui.Button(new Rectangle(x, y, w, ui.U(22)), "SKILLS AND PERKS", true, ctl.Name(Act.SkillsScreen))) OpenScreen(PanelKind.Skills);
        y += ui.U(25);
        if (ui.Button(new Rectangle(x, y, w, ui.U(22)), $"DIVISIONS ({C.S.Divisions.Count})", true, ctl.Name(Act.DivisionsScreen))) OpenScreen(PanelKind.Divisions);
        y += ui.U(30);
        // Camp rites (Rites.cs): only those someone in the company can work, two to a row.
        var rites = Campaign.RiteList.Where(rt => C.CanWork(rt.Tradition)).ToList();
        if (rites.Count > 0)
        {
            ui.Text(C.S.Relic ? $"CAMP RITES   RELIC {C.S.RelicCharges}/{Campaign.RelicCharges:0}" : "CAMP RITES", x, y, Kit.Gilt, 1);
            y += ui.U(13);
            for (int i = 0; i < rites.Count; i++)
            {
                var rt = rites[i];
                var rb = new Rectangle(x + i % 2 * (w / 2 + ui.U(2)), y + i / 2 * ui.U(21), w / 2 - ui.U(2), ui.U(18));
                if (ui.Button(rb, ui.Fit(rt.Name, rb.Width - ui.U(8)), C.S.Gold >= rt.Gold))
                    note = C.Rite(rt.Id) ?? rt.Line;
            }
            y += (rites.Count + 1) / 2 * ui.U(21) + ui.U(6);
        }
        // Prisoners: turn them to your cause (green, cohesion 0) or let them go.
        ui.Icon("chain", x, y, Kit.Gilt, 1);
        ui.Text($"PRISONERS {C.PrisonerCount}", x + ui.U(12), y, Kit.Gilt, 1);
        y += ui.U(13);
        foreach (var g in C.Player.Prisoners.ToList())
        {
            if (y > s.Bottom - ui.U(46)) break;
            if (ui.Button(new Rectangle(x, y, w, ui.U(18)), $"RECRUIT {g.Count} {Troops.Get(g.Troop).Name.ToUpperInvariant()}")) note = C.RecruitPrisoners(g) ?? "";
            y += ui.U(21);
        }
        if (C.PrisonerCount > 0 && ui.Button(new Rectangle(x, Math.Min(y, s.Bottom - ui.U(26)), w, ui.U(18)), "FREE THEM ALL (+RENOWN)")) C.FreePrisoners();
        if (C.PrisonerCount == 0) ui.Text("NONE. MEN TAKEN IN BATTLE WAIT HERE.", x, y, Style.Dim * 0.8f, 1);
    }

}
