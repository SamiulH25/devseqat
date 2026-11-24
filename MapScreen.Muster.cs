using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the party screen's side pieces: a circle's doctrine and spellbook, the roll of a company's named men, and the
/// captain's panel. The screen itself (the board of divisions and companies) is in MapScreen.Board.cs.
partial class MapScreen
{
    /// The circle's doctrine (docs/archive/ARCANA.md AR4): a button for each of its tradition's while none is chosen, then which.
    int DoctrineRow(CompanyRecord c, int x, int y, int w)
    {
        if (c.Doctrine != "")
        {
            var d = Abilities.Doctrines.First(z => z.Id == c.Doctrine);
            ui.TextFit($"DOCTRINE OF {d.Name.ToUpperInvariant()}: {d.Line.ToUpperInvariant()}. ITS SPELLS COST {Battle.DoctrineThrift:0} LESS WILL.", x, y, w, Style.Aura, 1);
            return y + ui.U(14);
        }
        var mine = Abilities.Doctrines.Where(z => z.Tradition == Campaign.CircleTradition(c)).ToList();
        ui.Text("CHOOSE A DOCTRINE (ONCE):", x, y + ui.U(4), Style.Dim, 1);
        int lx = x + ui.TW("CHOOSE A DOCTRINE (ONCE):", 1) + ui.U(8), bw = (x + w - lx - ui.U(4) * (mine.Count - 1)) / Math.Max(1, mine.Count);
        for (int i = 0; i < mine.Count; i++)
        {
            var b = new Rectangle(lx + i * (bw + ui.U(4)), y, bw, ui.U(18));
            if (ui.Button(b, ui.Fit(mine[i].Name.ToUpperInvariant(), bw - ui.U(6)), true)) note = C.ChooseDoctrine(mine[i].Id) ?? "";
            if (ui.Hover(b)) note = $"{mine[i].Name.ToUpperInvariant()}: {mine[i].Line.ToUpperInvariant()}. ITS SPELLS COST LESS WILL; ONE MORE SPELL TO LEARN, AND ITS OWN.";
        }
        return y + ui.U(22);
    }

    /// The company the roll panel shows.
    CompanyRecord? rollOf;
    /// The man picked on the roll (C4): the action bar works on him.
    Soldier? rollPick;

    /// A company's roll (docs/archive/LEGACY.md L1): its named men, what each is known for, and the men it has lost.
    /// A circle's spellbook and master on its roll (docs/archive/MAGIC_AURA.md 3.2-3.3): click a known spell to prepare it or set it
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
        y = DoctrineRow(c, x, y, w);
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
        if (c.Spells.Count < Campaign.LearnRoom(c) && learn.Count > 0)
        {
            int cost = Campaign.LearnCost * (c.Spells.Count + 1);
            ui.TextFit($"LEARN ONE MORE: {cost} CROWNS, WITH A TEACHER ({(Campaign.CircleTradition(c) == "faith" ? "A PRIEST" : "A CUNNING ONE")}, A SHRINE, STONES OR A HERMIT)", x, y, w, Style.Dim, 1);
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
        y = SergeantPick(c, x, y, w);
        y += ui.U(3);
        ui.Text("THE ROLL", x, y, Kit.Gilt, 2);
        string count = $"{c.Roll.Count} OF {Balance.Get().RollSize} NAMED";
        ui.TextR(count, x + w, y + ui.U(4), Style.Dim, 1);
        if (Campaign.Senior(c) is not null) // the legend for the star (C2)
        {
            string legend = "THE COMPANY FEELS ITS SENIOR MAN'S NATURE";
            int lx = x + w - ui.TW(count, 1) - ui.U(24) - ui.TW(legend, 1);
            ui.Icon("star", lx, y + ui.U(4), Kit.Gilt, 1);
            ui.Text(legend, lx + ui.U(10), y + ui.U(4), Kit.Mul(Kit.Gilt, 0.8f), 1);
        }
        y += ui.U(20);
        if (c.Roll.Count == 0) { ui.Text("NO MAN HAS EARNED A NAME YET. THEY DO IT IN BATTLE.", x, y, Style.Dim, 1); y += ui.U(14); }
        if (rollPick != null && !c.Roll.Contains(rollPick)) rollPick = null;
        foreach (var m in c.Roll.OrderByDescending(Campaign.OfBanner).ThenByDescending(m => m.Battles)) // the banner's men first (C4)
        {
            if (y > r.Bottom - ui.U(190)) break;
            var row = new Rectangle(x - ui.U(4), y - ui.U(3), w + ui.U(8), ui.U(42));
            bool picked = rollPick == m;
            ui.Rect(row, picked ? Kit.Mul(Kit.Gilt, 0.25f) : Color.White * (ui.Hover(row) ? 0.07f : 0.035f));
            if (picked) ui.Frame(row, Kit.Gilt * 0.7f, ui.U(1));
            if (ui.Clicked(row)) { rollPick = picked ? null : m; note = ""; }
            // His face (C1): the man the battle draws, his age, his scars, dressed by his tier on the company's charge.
            var fb = new Rectangle(row.X + ui.U(1), row.Y + ui.U(1), ui.U(40), ui.U(40));
            ui.FaceHead(PortraitPainter.Of(m, c), fb);
            ui.Frame(fb, Color.Black * 0.5f, ui.U(1));
            int tx = x + ui.U(42);
            y += ui.U(5);
            ui.Text(m.Name.ToUpperInvariant(), tx, y, Style.Text, 1);
            if (m.Nature != "") // his nature beside his name (C2); gold with a star when his company feels it
            {
                int nx = tx + ui.TW(m.Name.ToUpperInvariant(), 1) + ui.U(12);
                bool felt = Campaign.Senior(c) == m;
                ui.Text(m.Nature.ToUpperInvariant(), nx, y, felt ? Kit.Gilt : Style.Friendly, 1);
                if (felt) ui.Icon("star", nx + ui.TW(m.Nature.ToUpperInvariant(), 1) + ui.U(4), y, Kit.Gilt, 1);
                if (m.Mentions > 0) // mentioned (C4): a flag each time, the words when he is of the banner
                {
                    int mx = nx + ui.TW(m.Nature.ToUpperInvariant(), 1) + ui.U(felt ? 22 : 8);
                    ui.Icon("flag", mx, y, Kit.Gilt, 1);
                    ui.Text(Campaign.OfBanner(m) ? "OF THE BANNER" : $"MENTIONED X{m.Mentions}", mx + ui.U(10), y, Kit.Gilt, 1);
                }
            }
            string stat = $"AGE {Soldier.AgeOf(m)}, {Campaign.N(m.Battles, "BATTLE")}, {Campaign.N(m.Kills, "KILL")}" + (m.Wounds > 0 ? $", {Campaign.N(m.Wounds, "WOUND")}" : "");
            ui.TextR(stat, x + w, y, Style.Dim, 1);
            ui.Text(Troops.Get(m.Troop).Name.ToUpperInvariant() + (m.Hurt ? " . HURT" : ""), tx, y + ui.U(10), m.Hurt ? Style.Warn : Kit.Gilt, 1);
            ui.Text(Cut((m.Deeds.Count > 0 ? m.Deeds[^1] : "").ToUpperInvariant(), fit - 30), tx + ui.U(150), y + ui.U(10), Style.Dim, 1);
            string ties = C.TieLine(m); // his ties (C3): brothers, kin, rivals, master and lad
            if (ties != "") ui.Text(Cut(ties, fit), tx, y + ui.U(20), m.Avenging ? Style.Warn : Kit.Mul(Style.Friendly, 0.7f), 1); // dimmer than the troop; amber, not the fallen's red
            if (ui.Hover(row))
            {
                var lines = ui.Wrap(string.Join(" ", m.Deeds).ToUpperInvariant() + $" ON THE ROLL SINCE DAY {m.Joined}." + (m.Home != "" ? $" FROM {m.Home.ToUpperInvariant()}." : ""), ui.U(260)).ToList();
                if (ties != "") lines.AddRange(ui.Wrap("TIES: " + ties + (m.Avenging ? ". HE STRIKES HARDER NEXT BATTLE, AND TAKES MORE RISK." : "."), ui.U(260)));
                if (m.Nature != "") lines.AddRange(ui.Wrap($"{m.Nature.ToUpperInvariant()}: {Campaign.NatureLine(m.Nature).ToUpperInvariant()}" + (Campaign.Senior(c) == m ? " THE COMPANY FEELS IT." : " HIS COMPANY FEELS ITS SENIOR MAN'S NATURE."), ui.U(260)));
                ui.Tooltip(row.Right - ui.U(150), row.Y - ui.U(2), m.Name.ToUpperInvariant(), lines.ToArray()); // to the right, clear of the names
            }
            y += ui.U(41);
        }
        y += ui.U(6);
        ui.Divider(x, y, w, Kit.Gilt * 0.5f);
        y += ui.U(10);
        ui.Text("THE FALLEN", x, y, Kit.Gilt, 2);
        var fallen = C.FallenOf(c).Reverse().ToList();
        if (ui.Button(new Rectangle(x + w - ui.U(150), y, ui.U(150), ui.U(18)), $"THE BOOK ({fallen.Count})")) OpenBook(PanelKind.Roll);
        y += ui.U(20);
        if (fallen.Count == 0) ui.Text("NONE YET.", x, y, Style.Dim, 1);
        foreach (var m in fallen)
        {
            if (y > r.Bottom - ui.U(96)) { ui.Text("AND MORE BEFORE THEM.", x, y, Style.Dim, 1); break; }
            string how = m.Fate switch { "missing" => "MISSING", "left" => "LEFT THE COMPANY", "pensioned" => "WENT HOME", _ => "FELL" };
            string at = m.DiedAt != "" ? $" AGAINST {m.DiedAt.ToUpperInvariant()}" : "";
            // The fallen's face, greyed (C1).
            var fb = new Rectangle(x - ui.U(2), y - ui.U(5), ui.U(18), ui.U(18));
            ui.FaceHead(PortraitPainter.Of(m, c) with { Mood = 1 }, fb, new Color(88, 88, 92)); // greyed
            ui.Text(Cut($"{m.Name.ToUpperInvariant()}, {Troops.Get(m.Troop).Name.ToUpperInvariant()}", fit / 2 - 3), x + ui.U(20), y, Style.Dim, 1);
            ui.TextR(Cut($"{how}{at}, DAY {m.DiedDay}. {Campaign.N(m.Battles, "BATTLE")}", fit / 2), x + w, y, m.Fate == "fell" ? Style.Bad : Style.Dim, 1);
            y += ui.U(18);
        }
        RollActions(c, r, x, w);
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "BACK", note: ctl.Name(Act.Menu))) panel = PanelKind.Party;
    }

    /// A sergeant just rose and others qualify (C4): their faces as buttons; the risen man is first and gilded.
    int SergeantPick(CompanyRecord c, int x, int y, int w)
    {
        var pick = C.SergeantChoice(c);
        if (pick.Count == 0) return y;
        y += ui.U(4);
        ui.Text("CHOOSE THE SERGEANT", x, y, Kit.Gilt, 1);
        ui.TextR($"UNTIL THE END OF DAY {c.SergeantPickUntil}, OR {pick[0].Name.ToUpperInvariant()} KEEPS IT", x + w, y, Style.Dim, 1);
        y += ui.U(12);
        int bw = (w - ui.U(8) * (pick.Count - 1)) / pick.Count;
        for (int i = 0; i < pick.Count; i++)
        {
            var m = pick[i];
            var b = new Rectangle(x + i * (bw + ui.U(8)), y, bw, ui.U(30));
            ui.Rect(b, Color.White * (ui.Hover(b) ? 0.1f : 0.05f));
            ui.Frame(b, i == 0 ? Kit.Gilt * 0.8f : Style.Dim * 0.6f, ui.U(1));
            var fb = new Rectangle(b.X + ui.U(1), b.Y + ui.U(1), ui.U(28), ui.U(28));
            ui.FaceHead(PortraitPainter.Of(m, c), fb);
            string trait = Campaign.NatureTrait(m.Nature) is string t ? $" -> {t.ToUpperInvariant()}" : "";
            ui.Text(ui.Fit(m.Name.ToUpperInvariant(), bw - ui.U(36)), b.X + ui.U(34), b.Y + ui.U(5), i == 0 ? Kit.Gilt : Style.Text, 1);
            ui.Text(ui.Fit($"{m.Nature.ToUpperInvariant()}{trait}, {Campaign.N(m.Battles, "BATTLE")}", bw - ui.U(36)), b.X + ui.U(34), b.Y + ui.U(16), Style.Dim, 1);
            if (ui.Clicked(b)) note = C.ChooseSergeant(c, m) ?? "";
        }
        return y + ui.U(36);
    }

    /// What you can do with the picked man (C4): mention him, arm him, send him home, move him.
    void RollActions(CompanyRecord c, Rectangle r, int x, int w)
    {
        int y = r.Bottom - ui.U(80);
        if (rollPick is not Soldier m) { ui.TextC("CLICK A MAN TO MENTION, ARM, PENSION OR MOVE HIM.", r.X + r.Width / 2f, y + ui.U(10), Style.Dim, 1); return; }
        ManActions(c, m, x, y, w, () => rollPick = null);
    }

    /// The four things you can do for a named man (C4), as a row of buttons at `y` with his name over them; `gone` runs when
    /// he leaves the roll (sent home, moved). The roll and the camp's man's page share it.
    void ManActions(CompanyRecord c, Soldier m, int x, int y, int w, Action gone)
    {
        ui.Divider(x, y - ui.U(22), w, Kit.Gilt * 0.4f);
        ui.TextFitC($"FOR {m.Name.ToUpperInvariant()}, {Troops.Get(m.Troop).Name.ToUpperInvariant()}" + (m.Carries != "" ? $". CARRIES {m.Carries.ToUpperInvariant()}" : ""), x + w / 2f, y - ui.U(14), w, Kit.Gilt, 1);
        int bw = (w - ui.U(24)) / 5, bh = ui.U(28);
        Rectangle B(int i) => new(x + i * (bw + ui.U(6)), y, bw, bh);
        var gift = C.Givable().OrderByDescending(g => g.Quality).FirstOrDefault();
        if (gift != null && C.CannotArm(m, gift) != null) gift = null;
        var to = C.Player.Companies.FirstOrDefault(k => C.CannotMoveMan(c, m, k) == null);
        string? moveWhy = to == null ? C.Player.Companies.Where(k => k != c).Select(k => C.CannotMoveMan(c, m, k)).FirstOrDefault() ?? "NO OTHER COMPANY" : null;
        string? kTarget = C.KnightTarget(c, m);
        var acts = new (string Label, string? Why, string Says, Func<string?> Do, bool Leaves)[]
        {
            ("MENTION", C.CannotMention(m), $"A PURSE OF {Balance.Get().MentionPurse} CROWNS AND +1 COHESION. THREE MENTIONS MAKE HIM ONE OF THE BANNER.", () => C.Mention(m), false),
            ("ARM", C.CannotArm(m, gift), $"GIVE HIM {(gift != null ? Campaign.ItemName(gift, Gear.Get(gift.Def)).ToUpperInvariant() : "A LOT OF LOOTED ARMS")}. HE DIES LESS OFTEN WHEN HE FALLS; IF HE DIES, IT IS LOST.", () => C.Arm(m, gift), false),
            ("KNIGHT", C.CannotKnight(c, m), $"KNIGHT HIM AS {(kTarget != null ? Troops.Get(kTarget).Name.ToUpperInvariant() : "A NOBLE")} FOR {Campaign.KnightCost} CROWNS (NEEDS {Campaign.KnightingBattles} BATTLES & {Campaign.KnightRenown:0} RENOWN).", () => C.KnightMan(c, m), false),
            ("SEND HOME", C.CannotPension(m), $"A PENSION OF {Balance.Get().PensionCost} CROWNS. HIS VILLAGE'S RECRUITS COME READIER.", () => C.Pension(m), true),
            ("MOVE", moveWhy, to != null ? $"TO {to.Name.ToUpperInvariant()}, WITH HIS PLACE IN THE RANKS." : "", () => C.MoveMan(c, m, to!), true),
        };
        for (int i = 0; i < acts.Length; i++)
        {
            var (label, why, says, act, leaves) = acts[i];
            if (ui.Button(B(i), label, why == null)) { note = act() ?? ""; if (leaves && note == "") gone(); }
            if (ui.Hover(B(i))) ui.Tooltip(B(i).X, B(i).Y - ui.U(4), label, ui.Wrap(why ?? says, ui.U(240)).ToArray());
        }
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
        y += (int)(2 * rr) + ui.U(20);
        ui.Text($"{C.Player.Companies.Count} OF {Rules.CompanySlots(cap)} COMPANIES, {limit} MEN AT MOST", x, y, Style.Dim, 1);
        y += ui.U(10);
        ui.Divider(x, y + ui.U(2), w, Kit.Gilt * 0.5f);
        y += ui.U(10);
        // The skills themselves are on the captain sheet (K); here only the way there, gold when a perk waits.
        if (ui.Button(new Rectangle(x, y, w, ui.U(22)), picks > 0 ? $"SKILLS: {picks} PERK{(picks > 1 ? "S" : "")} TO CHOOSE" : "SKILLS AND PERKS", true, ctl.Name(Act.SkillsScreen), picks > 0)) OpenScreen(PanelKind.Skills);
        y += ui.U(25);
        if (ui.Button(new Rectangle(x, y, w, ui.U(22)), $"DIVISIONS ({C.S.Divisions.Count})", true, ctl.Name(Act.DivisionsScreen))) OpenScreen(PanelKind.Divisions);
        y += ui.U(30);
        // The camp rites are worked at the camp (C5); here only the way there.
        if (Campaign.RiteList.Any(rt => C.CanWork(rt.Tradition)))
        {
            if (ui.Button(new Rectangle(x, y, w, ui.U(22)), C.S.Relic ? $"CAMP RITES   RELIC {C.S.RelicCharges}/{Campaign.RelicCharges:0}" : "CAMP RITES", true, ctl.Name(Act.CampScreen))) OpenScreen(PanelKind.Camp);
            y += ui.U(28);
        }
        // Prisoners: turn them to your cause (green, cohesion 0) or let them go. Nothing shows while you hold none.
        if (C.PrisonerCount == 0) return;
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
    }

}
