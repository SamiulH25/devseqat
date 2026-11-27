using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the skills screen (K) as a captain's sheet: the captain on the left, a ring and a branch of perks per skill
/// in the middle (click an open perk to take it), and the record on the right.
partial class MapScreen
{
    static readonly Dictionary<string, string> SkillIcons = new()
    {
        ["Leadership"] = "flag", ["Tactics"] = "arrow", ["Logistics"] = "bread", ["Training"] = "up",
        ["Medicine"] = "heart", ["Stewardship"] = "coin", ["Charm"] = "star", ["Arms"] = "sword", ["Riding"] = "horse", ["Bowcraft"] = "arrow",
    };

    void SkillsPanel()
    {
        var r = Modal(1060, 620, "THE CAPTAIN");
        int gap = ui.U(10), lw = ui.U(220), rw = ui.U(200), top = r.Y + ui.U(30), h = r.Height - ui.U(96);
        CaptainCard(Well(new Rectangle(r.X + ui.U(20), top, lw, h), "THE CAPTAIN", Kit.Gilt));
        var mid = new Rectangle(r.X + ui.U(20) + lw + gap, top, r.Width - lw - rw - 2 * gap - ui.U(40), h);
        int picks = Skills.PicksTotal(C.Captain), sp = C.Captain.SkillPoints, fp = C.Captain.FocusPoints;
        var headParts = new List<string> { "SKILLS AND PERKS" };
        if (sp > 0) headParts.Add($"{sp} SKILL PT{(sp == 1 ? "" : "S")}");
        if (fp > 0) headParts.Add($"{fp} FOCUS");
        if (picks > 0) headParts.Add($"{picks} PERK{(picks == 1 ? "" : "S")}");
        var tree = Well(mid, string.Join(" . ", headParts), sp > 0 || fp > 0 || picks > 0 ? Kit.Gilt : Style.Friendly);
        SkillTree(tree);
        CaptainRecord(Well(new Rectangle(r.Right - ui.U(20) - rw, top, rw, h), "RECORD", Kit.Gilt));
        Note(r);
        if (ui.Button(new Rectangle(r.X + r.Width / 2 - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.SkillsScreen))) panel = CameFrom(PanelKind.Skills);
    }

    /// The captain's portrait under a night sky, name, rank, renown and traits.
    void CaptainCard(Rectangle c)
    {
        var cap = C.Captain;
        var me = C.S.Characters.FirstOrDefault(ch => ch.Id == C.S.CaptainChar);
        var port = new Rectangle(c.X, c.Y, c.Width, Math.Min(ui.U(160), c.Height / 2));
        ui.VGrad(port, Kit.Mul(Style.Sides[2].Cloth, 0.6f), new Color(18, 16, 14), 2);
        ui.Stars(new Rectangle(port.X, port.Y, port.Width, port.Height / 2), (float)C.S.Hours, 20);
        ui.Hills(port, port.Bottom - ui.U(20), ui.U(8), 1.5f, new Color(30, 24, 20));
        ui.Portrait(Rules.CaptainType(cap), new Rectangle(port.X, port.Y + ui.U(4), port.Width, port.Height - ui.U(4)), 3 * ui.S); // your kit and face
        ui.Frame(port, Color.Black * 0.6f, ui.U(1));
        ui.Shield(new Rectangle(port.X + ui.U(6), port.Bottom - ui.U(42), ui.U(30), ui.U(36)), Style.Sides[2]);
        int y = port.Bottom + ui.U(10);
        string name = (me?.Name ?? "The Captain").ToUpperInvariant();
        ui.Text(name, c.X, y, Style.Text, ui.TW(name, 2) <= c.Width ? 2 : 1); y += ui.U(20);
        if (C.Title is { Length: > 0 } title) { ui.Text(title.ToUpperInvariant(), c.X, y - ui.U(4), Kit.Gilt, 1); y += ui.U(10); } // P5
        if (C.OriginNote is { Length: > 0 } origin) { ui.TextFit(origin.ToUpperInvariant(), c.X, y - ui.U(4), c.Width, Style.Dim, 1); y += ui.U(10); } // the creator
        int cx = c.X;
        cx += ui.Chip(cx, y, $"LVL {cap.Level}", Kit.Gilt, "star") + ui.U(4);
        cx += ui.Chip(cx, y, Campaign.RankName(C.Rank).ToUpperInvariant(), Style.Friendly, "crown") + ui.U(4);
        y += ui.U(18);
        float xpNeed = Skills.XpForLevel(cap.Level);
        ui.Text("EXPERIENCE", c.X, y, Style.Dim, 1); ui.TextR($"{cap.Xp:0} / {xpNeed:0}", c.Right, y, Style.Ok, 1); y += ui.U(11);
        ui.Bar(new Rectangle(c.X, y, c.Width, ui.U(6)), cap.Xp / MathF.Max(1, xpNeed), Style.Ok); y += ui.U(11);
        if (cap.SkillPoints > 0 || cap.FocusPoints > 0)
        {
            ui.Text($"{cap.SkillPoints} SKILL PT{(cap.SkillPoints == 1 ? "" : "S")}  .  {cap.FocusPoints} FOCUS", c.X, y, Kit.Gilt, 1);
            y += ui.U(13);
        }
        var (unlock, at) = Rules.NextRenownUnlock(cap);
        float from = RenownStep(cap, at);
        ui.Text("RENOWN", c.X, y, Style.Dim, 1); ui.TextR($"{cap.Renown:0} / {at:0}", c.Right, y, Kit.Gilt, 1); y += ui.U(11);
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(8)), (cap.Renown - from) / MathF.Max(1, at - from), Kit.Gilt, 12); y += ui.U(14);
        foreach (var l in ui.Wrap($"NEXT: {unlock}", c.Width)) { ui.Text(l, c.X, y, Style.Dim * 0.8f, 1); y += ui.U(11); }
        if (C.NextRank() is var (next, needs))
        {
            y += ui.U(4);
            foreach (var l in ui.Wrap($"TO BE {Campaign.RankName(next).ToUpperInvariant()}: {needs.ToUpperInvariant()}", c.Width)) { if (y > c.Bottom - ui.U(30)) break; ui.Text(l, c.X, y, Style.Dim * 0.8f, 1); y += ui.U(11); }
        }
        y += ui.U(6);
        cx = c.X;
        foreach (var t in me?.Traits ?? new List<string>())
        {
            string tt = t.ToUpperInvariant();
            if (cx + ui.TW(tt, 1) + ui.U(10) > c.Right) { cx = c.X; y += ui.U(16); }
            if (y > c.Bottom - ui.U(14)) break;
            cx += ui.Chip(cx, y, tt, t is "Coward" ? Style.Bad : t is "Ruthless" ? Style.Warn : Style.Ok) + ui.U(4);
        }
        // Lasting wounds (P4), in red; the tooltip says what each costs.
        foreach (var w in cap.Wounds.Select(Campaign.WoundOf).OfType<WoundDef>())
        {
            string wt = w.Name.ToUpperInvariant();
            if (cx + ui.TW(wt, 1) + ui.U(10) > c.Right) { cx = c.X; y += ui.U(16); }
            if (y > c.Bottom - ui.U(14)) break;
            var chip = new Rectangle(cx, y, ui.TW(wt, 1) + ui.U(8), ui.U(14));
            cx += ui.Chip(cx, y, wt, Style.Bad) + ui.U(4);
            if (ui.Hover(chip)) ui.Tooltip(chip.Center.X, chip.Y - ui.U(2), wt, ui.Wrap(w.Text + ". A HEALING SPRING MAY MEND IT.", ui.U(170)).ToArray());
        }
    }

    /// The perk the next click on the K sheet forgets (asked once before paying).
    string? forgetAsk;

    /// Each skill as a ring with its perks beneath as nodes: gold when taken, lit when one may be taken now, dark when locked.
    void SkillTree(Rectangle c)
    {
        // Two levels (docs/archive/PLAYTEST.md PT2d): the ten skills as rings at the top (click one), and the picked skill's perks
        // below as rows that say what each does and whether you can take it, so nothing needs a hover to be read.
        var cap = C.Captain;
        int n = Skills.All.Count, cols = 5, rows = (n + cols - 1) / cols;
        if (skillPick == null || Skills.All.All(k => k.Id != skillPick))
            skillPick = (Skills.All.FirstOrDefault(k => Skills.Picks(cap, k.Id) > 0) ?? Skills.All[0]).Id;
        int bw = c.Width / cols, bh = ui.U(66);
        for (int i = 0; i < n; i++)
        {
            var sk = Skills.All[i];
            var box = new Rectangle(c.X + i % cols * bw, c.Y + i / cols * bh, bw, bh);
            float lv = C.Skill(sk.Id);
            int open = Skills.Picks(cap, sk.Id);
            bool on = sk.Id == skillPick, over = ui.Hover(box);
            if (on || over) ui.Rect(Kit.Inset(box, ui.U(2)), Color.White * (on ? 0.07f : 0.04f));
            if (on) ui.Frame(Kit.Inset(box, ui.U(2)), Kit.Gilt, ui.U(2));
            float rr = ui.U(16);
            var centre = new Vector2(box.Center.X, box.Y + rr + ui.U(4));
            ui.Ring(centre, rr, lv / Skills.Max, open > 0 ? Kit.Gilt : Style.Friendly, $"{lv:0}");
            ui.TextC(sk.Name.ToUpperInvariant(), box.Center.X, centre.Y + rr + ui.U(3), open > 0 || on ? Kit.Gilt : Style.Text, 1);
            int foc = Skills.FocusOf(cap, sk.Id);
            if (open > 0) ui.TextC($"{open} PERK", box.Center.X, centre.Y + rr + ui.U(13), Kit.Gilt, 1);
            else if (foc > 0)
            {
                int pw = ui.U(4), pg = ui.U(3), tot = foc * pw + (foc - 1) * pg, fx = box.Center.X - tot / 2, fy = (int)(centre.Y + rr + ui.U(15));
                for (int fi = 0; fi < foc; fi++) ui.Rect(new Rectangle(fx + fi * (pw + pg), fy, pw, pw), Kit.Gilt);
            }
            if (!on && ui.Clicked(box)) { skillPick = sk.Id; forgetAsk = null; note = ""; }
        }
        var pick = Skills.All.First(k => k.Id == skillPick);
        int y = c.Y + rows * bh + ui.U(6);
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.5f);
        y += ui.U(8);
        float plv = C.Skill(pick.Id);
        int pfoc = Skills.FocusOf(cap, pick.Id);
        ui.Text($"{pick.Name.ToUpperInvariant()} {plv:0}", c.X, y, Kit.Gilt, 2);
        int btnW = ui.U(110), btnH = ui.U(20);
        var trainBtn = new Rectangle(c.Right - btnW * 2 - ui.U(6), y, btnW, btnH);
        var focusBtn = new Rectangle(c.Right - btnW, y, btnW, btnH);
        if (ui.Button(trainBtn, "+5 TRAIN (1 SP)", cap.SkillPoints > 0 && plv < Skills.Max))
            note = C.InvestSkill(pick.Id) ?? $"{pick.Name.ToUpperInvariant()} +5";
        if (ui.Button(focusBtn, $"FOCUS {pfoc}/{Skills.MaxFocus}", cap.FocusPoints > 0 && pfoc < Skills.MaxFocus))
            note = C.InvestFocus(pick.Id) ?? $"{pick.Name.ToUpperInvariant()} FOCUS x{Skills.FocusMult(cap, pick.Id):0.#}";
        y += ui.U(22);
        ui.TextFit(Skills.CurrentBonus(cap, pick.Id), c.X, y, c.Width, Style.Text, 1);
        y += ui.U(12);
        ui.TextFit($"RISES BY {pick.Rises}. LEARN RATE x{Skills.FocusMult(cap, pick.Id):0.#}.", c.X, y, c.Width, Style.Dim, 1);
        y += ui.U(13);
        int rh = Math.Min(ui.U(44), (c.Bottom - y) / Math.Max(1, Math.Min(5, pick.Perks.Count)));
        for (int k = 0; k < pick.Perks.Count && k < 5; k++)
        {
            var pk = pick.Perks[k];
            bool has = cap.Perks.Contains(pk.Id), cap4 = Skills.Capstone(pick, pk);
            string? locked = has ? null : Skills.Locked(cap, pick, pk);
            int state = has ? 2 : locked == null ? 1 : 0;
            var row = new Rectangle(c.X, y, c.Width, rh - ui.U(4));
            bool over = ui.Hover(row);
            ui.Rect(row, state == 2 ? Kit.Gilt * 0.14f : Color.White * (over && state != 0 ? 0.08f : 0.035f));
            ui.Frame(row, state == 2 ? Kit.Gilt * 0.7f : state == 1 ? Kit.Gilt : Color.Black * 0.5f, ui.U(state == 1 ? 2 : 1)); // every perk you can take is framed gold
            string icon = cap4 ? "crown" : SkillIcons.TryGetValue(pick.Id, out var si) ? si : "star";
            ui.Icon(icon, row.X + ui.U(8), row.Y + (row.Height - ui.U(14)) / 2, state == 2 ? Kit.Gilt : state == 1 ? Style.Text : Style.Dim * 0.5f, 2);
            int tx = row.X + ui.U(30), tier = Skills.TierOf(pick, pk), right = ui.U(150);
            string label = pk.Name.ToUpperInvariant();
            ui.Text(label, tx, row.Y + ui.U(5), state == 0 ? Style.Dim : state == 2 ? Kit.Gilt : Style.Text, 1);
            ui.Text(tier == 4 ? "CAPSTONE" : "TIER " + Style.Numerals[tier], tx + ui.TW(label, 1) + ui.U(8), row.Y + ui.U(5), Style.Dim * 0.8f, 1);
            var lines = ui.Wrap(pk.Text.ToUpperInvariant(), row.Width - ui.U(30) - right);
            for (int li = 0; li < Math.Min(2, lines.Count); li++)
                ui.Text(li == 1 && lines.Count > 2 ? ui.Fit(lines[1] + " " + lines[2], row.Width - ui.U(30) - right) : lines[li], tx, row.Y + ui.U(17 + 10 * li), state == 0 ? Style.Dim * 0.7f : Style.Dim, 1);
            string what = state == 2 ? (forgetAsk == pk.Id ? "CLICK AGAIN TO FORGET" : "TAKEN") : state == 1 ? "CLICK TO TAKE" : (locked ?? "").ToUpperInvariant();
            foreach (var (l, i) in ui.Wrap(what, right - ui.U(12)).Take(2).Select((l, i) => (l, i)))
                ui.TextR(l, row.Right - ui.U(8), row.Y + ui.U(5 + 10 * i), state == 1 ? Kit.Gilt : state == 2 ? Kit.Gilt * 0.8f : Style.Dim * 0.8f, 1);
            if (state == 1 && ui.Clicked(row)) { note = C.TakePerk(pk.Id) ?? $"{label} LEARNED"; Ui.OnClick?.Invoke(); }
            // A taken perk can be forgotten in a town: the first click asks, the second pays.
            if (state == 2 && ui.Clicked(row))
            {
                if (forgetAsk != pk.Id) { forgetAsk = C.CannotForget(pk.Id) == null ? pk.Id : null; note = C.CannotForget(pk.Id) is string w ? $"TO FORGET: {w}" : $"CLICK AGAIN TO FORGET {label} FOR {C.RetrainCost} CROWNS"; }
                else { note = C.ForgetPerk(pk.Id) ?? $"{label} FORGOTTEN"; forgetAsk = null; }
                Ui.OnClick?.Invoke();
            }
            y += rh;
        }
    }

    /// The skill whose perks the captain sheet shows (click its ring).
    string? skillPick;

    /// The aura (docs/archive/KNIGHT_ORDERS.md 5): tier, XP to the next, the wall and what the tier gives. Returns the next y.
    int AuraBlock(Rectangle c, int y)
    {
        var cap = C.Captain;
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f); y += ui.U(12);
        ui.Text("AURA", c.X, y, Style.Dim, 1);
        ui.TextR(Campaign.AuraName(cap.AuraTier).ToUpperInvariant(), c.Right, y, cap.AuraTier > 0 ? Style.Aura : Style.Dim, 1); y += ui.U(11);
        float need = C.AuraNeed;
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(7)), need <= 0 ? 1 : cap.AuraXp / need, cap.AuraWall ? Style.Warn : Style.Aura, 10); y += ui.U(12);
        string line = cap.AuraWall ? "A WALL. ONE TRIAL WILL BREAK IT:" : Campaign.AuraGifts[Math.Clamp(cap.AuraTier, 0, Campaign.AuraTop)].ToUpperInvariant();
        if (cap.Wounds.Contains("core")) line = "YOUR CORE IS CRACKED: YOU FIGHT A TIER LOWER. " + line;
        foreach (var l in ui.Wrap(line, c.Width)) { ui.Text(l, c.X, y, cap.AuraWall ? Style.Warn : Style.Dim * 0.8f, 1); y += ui.U(11); }
        // The trial card (docs/archive/ARCANA.md AR1): every way through the wall, and where to find it now.
        foreach (var trial in C.NearestTrials())
            foreach (var l in ui.Wrap(trial, c.Width - ui.U(8))) { ui.Text(l, c.X + ui.U(8), y, Style.Text * 0.9f, 1); y += ui.U(11); }
        return FormList(c, OwnFormBlock(c, FlameBlock(c, PathBlock(c, y))));
    }

    /// The path the aura walks (docs/archive/ARCANA.md AR6): three buttons once it wakes, then the path chosen and what it gives.
    int PathBlock(Rectangle c, int y)
    {
        var cap = C.Captain;
        if (cap.AuraTier < 1) return y;
        y += ui.U(2);
        if (cap.Path != "")
        {
            foreach (var l in ui.Wrap(Campaign.PathLine(cap.Path), c.Width)) { ui.Text(l, c.X, y, Style.Aura, 1); y += ui.U(11); }
            return y + ui.U(2);
        }
        ui.Text("CHOOSE YOUR PATH (ONCE):", c.X, y, Kit.Gilt, 1); y += ui.U(12);
        int w = (c.Width - ui.U(8)) / 3;
        for (int i = 0; i < Forms.Paths.Length; i++)
        {
            string path = Forms.Paths[i];
            var b = new Rectangle(c.X + i * (w + ui.U(4)), y, w, ui.U(20));
            if (ui.Button(b, path.ToUpperInvariant(), true)) note = C.ChoosePath(path) ?? "";
            if (b.Contains(ui.Mouse)) ui.Tooltip(b.Center.X, b.Y - ui.U(2), path.ToUpperInvariant(), ui.Wrap(Campaign.PathLine(path), ui.U(260)).ToArray());
        }
        return y + ui.U(26);
    }

    /// Your own form on offer (docs/archive/MAGIC_AURA.md 4.4): the two styles you fight in most, one button each. Returns the next y.
    int OwnFormBlock(Rectangle c, int y)
    {
        var cap = C.Captain;
        if (cap.OwnFormOffer.Count == 0 && cap.OwnForm == null && cap.Order == "" && cap.Forsworn.Count == 0 && cap.AuraTier > 0)
        {
            // The road to your own form (MA9): how far the tally has come, and what else it needs.
            float t = cap.StyleTally.Values.Sum();
            string top = cap.StyleTally.Count == 0 ? "" : $" MOSTLY {Campaign.Styles.First(s => s.Style == cap.StyleTally.MaxBy(kv => kv.Value).Key).Word.ToUpperInvariant()}.";
            string line = $"YOUR STYLE: {MathF.Min(t, Campaign.OwnFormAt):0} OF {Campaign.OwnFormAt:0} TOWARD A FORM OF YOUR OWN.{top}" + (cap.AuraTier < 3 ? " AT AURA EXPERT." : "");
            foreach (var l in ui.Wrap(line, c.Width)) { ui.Text(l, c.X, y, Style.Dim * 0.8f, 1); y += ui.U(11); }
            return y + ui.U(2);
        }
        if (cap.OwnFormOffer.Count == 0 || !C.MayMakeForm) return y;
        ui.Text("A FORM OF YOUR OWN", c.X, y, Style.Aura, 1); y += ui.U(11);
        int w = (c.Width - ui.U(6)) / 2, h = ui.U(18);
        for (int i = 0; i < cap.OwnFormOffer.Count; i++)
        {
            string style = cap.OwnFormOffer[i];
            var f = C.MakeOwnForm(style);
            var b = new Rectangle(c.X + i * (w + ui.U(6)), y, w, h);
            if (ui.Button(b, ui.Fit(f.Name.ToUpperInvariant(), w - ui.U(8)), true)) note = C.ChooseOwnForm(style) ?? "";
            if (ui.Hover(b)) ui.Tooltip(b.Center.X, b.Y - ui.U(2), f.Name.ToUpperInvariant(), ui.Wrap(f.Line.ToUpperInvariant() + " YOUR ORDER WOULD TEACH IT. YOU CHOOSE ONCE.", ui.U(180)).ToArray());
        }
        return y + h + ui.U(6);
    }

    /// The Black Flame (docs/archive/KNIGHT_ORDERS.md 7, K7): corruption, and the buttons to burn through a wall or purge it. Returns the next y.
    int FlameBlock(Rectangle c, int y)
    {
        var cap = C.Captain;
        if (!cap.Pact && cap.Corruption <= 0) return y + ui.U(4);
        y += ui.U(4);
        ui.Text("BLACK FLAME", c.X, y, Style.Bad, 1);
        ui.TextR($"{cap.Corruption:0} OF 100", c.Right, y, Style.Bad, 1); y += ui.U(11);
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(5)), cap.Corruption / 100f, Style.Bad, 4); y += ui.U(9);
        int w = (c.Width - ui.U(6)) / 2, h = ui.U(18);
        if (cap.Pact)
        {
            string? burn = C.CannotBurnWall();
            var br = new Rectangle(c.X, y, w, h);
            if (ui.Button(br, "BURN THE WALL", burn == null)) note = C.BurnWall() ?? "";
            if (ui.Hover(br)) ui.Tooltip(br.Center.X, br.Y - ui.U(2), "BURN THROUGH THE WALL", ui.Wrap($"NO TRIAL: A TIER NOW, AND {Campaign.BurnCost:0} MORE CORRUPTION. {burn ?? "YOU CAN DO IT NOW"}.", ui.U(180)).ToArray());
        }
        string? purge = C.CannotPurge();
        var pr = new Rectangle(c.Right - w, y, w, h);
        if (ui.Button(pr, "PURGE IT", purge == null)) note = C.Purge() ?? "";
        if (ui.Hover(pr)) ui.Tooltip(pr.Center.X, pr.Y - ui.U(2), "PURGE THE FLAME", ui.Wrap($"ONCE ONLY. THE PACT AND ITS FORMS GO, CORRUPTION FALLS BY {Campaign.PurgeOff:0}, AND YOUR AURA DROPS A TIER. {purge ?? "YOU CAN DO IT NOW"}.", ui.U(180)).ToArray());
        return y + h + ui.U(6);
    }

    /// The forms you know (K2): those in battle slots with their keys, the rest dim. Click one to put it in the first slot.
    int FormList(Rectangle c, int y)
    {
        var cap = C.Captain;
        if (cap.Forms.Count == 0) return y;
        var slotted = Campaign.SlottedForms(cap);
        var keys = new[] { Act.Form1, Act.Form2, Act.Form3 };
        int n = 0;
        foreach (var id in cap.Forms.Where(Forms.Has))
        {
            if (y > c.Bottom - ui.U(24)) break;
            var f = Forms.Get(id);
            bool on = slotted.Contains(id);
            string key = !on ? "" : f.Great ? ctl.Name(Act.GreatForm) : ctl.Name(keys[Math.Min(2, n++)]);
            var row = new Rectangle(c.X, y - ui.U(1), c.Width, ui.U(11));
            bool hover = ui.Hover(row);
            if (hover) ui.Rect(row, Kit.Mul(Style.Aura, 0.15f));
            ui.Text(key, c.X, y, Kit.Gilt, 1);
            int rank = Campaign.FormRank(cap, id);
            string asp = cap.FormAspects.TryGetValue(id, out var aid) && Forms.Aspect(aid) is { } ad ? $" ({ad.Name.ToUpperInvariant()})" : "";
            ui.TextFit($"{f.Name.ToUpperInvariant()}{(rank > 1 ? $" {Style.Numerals[rank]}" : "")}{asp}", c.X + ui.U(14), y, c.Width - ui.U(40), on ? Style.Aura : Style.Dim, 1);
            ui.TextR($"{f.Aura * (C.Captain.Path == "sword" ? Battle.SwordThrift : 1):0}", c.Right, y, Style.Dim, 1);
            if (hover) ui.Tooltip(row.Center.X, row.Y - ui.U(2), f.Name.ToUpperInvariant(), ui.Wrap(f.Line.ToUpperInvariant() + (on ? "" : " CLICK TO PUT IT IN THE FIRST SLOT."), ui.U(180)).ToArray());
            if (ui.Clicked(row)) note = C.SlotForm(id) ?? "";
            y += ui.U(12);
            var aspects = Forms.AspectsFor(f).ToList();
            if (aspects.Count == 2 && C.CannotTakeAspect(id, aspects[0].Id) == null && y < c.Bottom - ui.U(30))
            {
                int aw = (c.Width - ui.U(20)) / 2;
                for (int i = 0; i < 2; i++)
                {
                    var b = new Rectangle(c.X + ui.U(14) + i * (aw + ui.U(6)), y, aw, ui.U(14));
                    if (ui.Button(b, ui.Fit(aspects[i].Name.ToUpperInvariant(), aw - ui.U(6)), true)) note = C.TakeAspect(id, aspects[i].Id) ?? "";
                    if (ui.Hover(b)) ui.Tooltip(b.Center.X, b.Y - ui.U(2), $"{f.Name.ToUpperInvariant()}: {aspects[i].Name.ToUpperInvariant()}", ui.Wrap(aspects[i].Line.ToUpperInvariant() + " FOR GOOD: ONE OF THE TWO.", ui.U(180)).ToArray());
                }
                y += ui.U(17);
            }
        }
        return y + ui.U(4);
    }

    /// The captain's record: days, fights, kills, holdings, health, and how each skill rises.
    void CaptainRecord(Rectangle c)
    {
        var cap = C.Captain;
        var comps = C.Player.Companies;
        int y = c.Y;
        int fiefs = C.S.Settlements.Count(s => s.Owner == C.Player.Id);
        foreach (var (icon, label, value) in new[]
                 {
                     ("clock", "DAYS IN THE FIELD", $"{C.Day}"), ("flag", "BATTLES FOUGHT", $"{(comps.Count == 0 ? 0 : comps.Max(k => k.Battles))}"),
                     ("skull", "FOES SLAIN", $"{comps.Sum(k => k.Kills):N0}"), ("shield", "COMPANIES", $"{comps.Count}"),
                     ("star", "PERKS", $"{cap.Perks.Count}"), ("tower", "FIEFS", $"{fiefs}"), ("chain", "LORDS HELD", $"{C.Player.Captives.Count}"),
                 })
        {
            ui.Icon(icon, c.X, y, Style.Dim, 1);
            ui.Text(label, c.X + ui.U(12), y, Style.Dim, 1);
            ui.TextR(value, c.Right, y, Style.Text, 1);
            y += ui.U(15);
        }
        y += ui.U(4);
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f); y += ui.U(12);
        float hurt = (cap.WoundedUntil - (float)C.S.Hours) / 24;
        ui.Text("HEALTH", c.X, y, Style.Dim, 1);
        ui.TextR(hurt > 0 ? $"WOUNDED {hurt:0.0} DAYS" : "HALE", c.Right, y, hurt > 0 ? Style.Warn : Style.Ok, 1); y += ui.U(11);
        ui.Gauge(new Rectangle(c.X, y, c.Width, ui.U(7)), hurt > 0 ? 0.35f : 1, hurt > 0 ? Style.Warn : Style.Ok); y += ui.U(16);
        y = AuraBlock(c, y);
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f); y += ui.U(12);
        ui.Text("PERKS TAKEN", c.X, y, Kit.Gilt, 1); y += ui.U(13);
        foreach (var id in cap.Perks)
        {
            if (y > c.Bottom - ui.U(11)) break;
            var pk = Skills.All.SelectMany(s => s.Perks).FirstOrDefault(p => p.Id == id);
            foreach (var l in ui.Wrap(pk == null ? id.ToUpperInvariant() : $"{pk.Name.ToUpperInvariant()}: {pk.Text.ToUpperInvariant()}", c.Width))
            { if (y > c.Bottom - ui.U(11)) break; ui.Text(l, c.X, y, Style.Dim, 1); y += ui.U(11); }
            y += ui.U(3);
        }
        if (cap.Perks.Count == 0) foreach (var l in ui.Wrap("NONE YET. SKILLS RISE BY USE; EVERY 25 POINTS OPENS A PERK ON THAT BRANCH.", c.Width)) { ui.Text(l, c.X, y, Style.Dim * 0.8f, 1); y += ui.U(11); }
    }

}
