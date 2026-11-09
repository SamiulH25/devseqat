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
        int picks = Skills.PicksTotal(C.Captain);
        var tree = Well(mid, picks > 0 ? $"SKILLS AND PERKS . {picks} PERK{(picks == 1 ? "" : "S")} TO CHOOSE" : "SKILLS AND PERKS", picks > 0 ? Kit.Gilt : Style.Friendly);
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
        ui.Portrait(UnitType.Captain, new Rectangle(port.X, port.Y + ui.U(4), port.Width, port.Height - ui.U(4)), 3 * ui.S);
        ui.Frame(port, Color.Black * 0.6f, ui.U(1));
        ui.Shield(new Rectangle(port.X + ui.U(6), port.Bottom - ui.U(42), ui.U(30), ui.U(36)), Style.Sides[2]);
        int y = port.Bottom + ui.U(10);
        string name = (me?.Name ?? "The Captain").ToUpperInvariant();
        ui.Text(name, c.X, y, Style.Text, ui.TW(name, 2) <= c.Width ? 2 : 1); y += ui.U(20);
        if (C.Title is { Length: > 0 } title) { ui.Text(title.ToUpperInvariant(), c.X, y - ui.U(4), Kit.Gilt, 1); y += ui.U(10); } // P5
        int cx = c.X;
        cx += ui.Chip(cx, y, Campaign.RankName(C.Rank).ToUpperInvariant(), Kit.Gilt, "crown") + ui.U(4);
        if (me != null) ui.Chip(cx, y, $"AGE {me.Age}", Style.Dim);
        y += ui.U(22);
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
        var cap = C.Captain;
        int n = Skills.All.Count, cols = n > 8 ? 5 : 4, rows = (n + cols - 1) / cols;
        int bw = c.Width / cols, bh = c.Height / Math.Max(1, rows);
        (Rectangle Node, PerkDef Perk, SkillDef Skill, int State)? tip = null;
        (Rectangle Box, SkillDef Skill)? skillTip = null;
        for (int i = 0; i < n; i++)
        {
            var sk = Skills.All[i];
            var box = new Rectangle(c.X + i % cols * bw, c.Y + i / cols * bh, bw, bh);
            float lv = C.Skill(sk.Id);
            int open = Skills.Picks(cap, sk.Id);
            float rr = Math.Min(ui.U(22), bw / 5f);
            var centre = new Vector2(box.Center.X, box.Y + rr + ui.U(6));
            ui.Ring(centre, rr, lv / Skills.Max, open > 0 ? Kit.Gilt : Style.Friendly, $"{lv:0}");
            var ringBox = new Rectangle((int)(centre.X - rr), (int)(centre.Y - rr), (int)(2 * rr), (int)(2 * rr));
            if (ui.Hover(ringBox)) skillTip = (ringBox, sk);
            ui.TextC(sk.Name.ToUpperInvariant(), box.Center.X, centre.Y + rr + ui.U(8), open > 0 ? Kit.Gilt : Style.Text, 1);
            int ty = (int)(centre.Y + rr) + ui.U(20);
            if (open > 0) ui.TextC($"{open} TO CHOOSE", box.Center.X, ty, Kit.Gilt, 1);
            else ui.TextC($"NEXT AT {((int)(lv / Skills.Step) + 1) * (int)Skills.Step}", box.Center.X, ty, Style.Dim * 0.7f, 1);

            // The branch (P3): tier I-III perks two by two, the capstone centred below them.
            // Room under each node for a name of two lines.
            int node = Math.Clamp((bh - (ty - box.Y) - ui.U(20)) / 3 - ui.U(22), ui.U(18), ui.U(38)), ic = node >= ui.U(32) ? 2 : 1;
            int gx = Math.Max(ui.U(16), bw / 2 - node), gy = ui.U(22), nx0 = box.Center.X - node - gx / 2, ny0 = ty + ui.U(14);
            for (int k = 0; k < sk.Perks.Count && k < 5; k++)
            {
                var pk = sk.Perks[k];
                bool has = cap.Perks.Contains(pk.Id), cap4 = Skills.Capstone(sk, pk);
                int state = has ? 2 : Skills.Locked(cap, sk, pk) == null ? 1 : 0;
                var nr = cap4 ? new Rectangle(box.Center.X - node / 2, ny0 + 2 * (node + gy), node, node)
                    : new Rectangle(nx0 + k % 2 * (node + gx), ny0 + k / 2 * (node + gy), node, node);
                bool hover = ui.Hover(nr);
                ui.Rect(new Rectangle(nr.X + ui.U(2), nr.Y + ui.U(3), nr.Width, nr.Height), Color.Black * 0.4f);
                ui.VGrad(nr, state == 2 ? Kit.Mul(Kit.Gilt, 0.6f) : state == 1 ? new Color(46, 46, 52) : new Color(24, 24, 28), state == 2 ? Kit.Mul(Kit.Gilt, 0.25f) : Kit.IronDark, 1);
                ui.Frame(nr, state == 2 ? Kit.Gilt : state == 1 ? (hover ? Kit.Gilt : Style.Text * 0.7f) : cap4 ? Kit.Gilt * 0.35f : Color.Black * 0.6f, ui.U(cap4 ? 2 : 1));
                string icon = cap4 ? "crown" : SkillIcons.TryGetValue(sk.Id, out var si) ? si : "star";
                ui.Icon(icon, nr.X + (node - ui.U(7 * ic)) / 2, nr.Y + (node - ui.U(7 * ic)) / 2, state == 2 ? Color.White : state == 1 ? Style.Text : Style.Dim * 0.4f, ic);
                string label = pk.Name.ToUpperInvariant();
                var shown = ui.Wrap(label, node + gx - ui.U(4));
                for (int li = 0; li < Math.Min(2, shown.Count); li++)
                    ui.TextFitC(li == 1 && shown.Count > 2 ? string.Join(" ", shown.Skip(1)) : shown[li], nr.Center.X, nr.Bottom + ui.U(3 + 9 * li), node + gx - ui.U(4),
                        state == 0 ? Style.Dim * 0.5f : state == 2 ? Kit.Gilt : Style.Text, 1);
                if (hover) tip = (nr, pk, sk, state);
                if (state == 1 && ui.Clicked(nr)) { note = C.TakePerk(pk.Id) ?? $"{label} LEARNED"; Ui.OnClick?.Invoke(); }
                // A taken perk can be forgotten in a town: the first click asks, the second pays.
                if (state == 2 && ui.Clicked(nr))
                {
                    if (forgetAsk != pk.Id) { forgetAsk = C.CannotForget(pk.Id) == null ? pk.Id : null; note = C.CannotForget(pk.Id) is string w ? $"TO FORGET: {w}" : $"CLICK AGAIN TO FORGET {label}"; }
                    else { note = C.ForgetPerk(pk.Id) ?? $"{label} FORGOTTEN"; forgetAsk = null; }
                    Ui.OnClick?.Invoke();
                }
            }
        }
        if (skillTip is var (sr, sd))
            ui.Tooltip(sr.Center.X, sr.Y - ui.U(2), sd.Name.ToUpperInvariant(), ui.Wrap($"RISES BY {sd.Rises}.", ui.U(170)).Concat(ui.Wrap($"{sd.Governs}.", ui.U(170))).ToArray());
        if (tip is var (tr, tp, ts, tst))
        {
            var lines = ui.Wrap(tp.Text.ToUpperInvariant(), ui.U(170)).ToList();
            if (tst == 2) lines.Add(C.CannotForget(tp.Id) == null ? $"CLICK TWICE TO FORGET IT FOR {C.RetrainCost} CROWNS." : "FORGET IT IN A TOWN, FOR GOLD.");
            lines.Add(tst == 2 ? "TAKEN." : tst == 1 ? "CLICK TO TAKE IT." : (Skills.Locked(cap, ts, tp) ?? "") + ".");
            int tier = Skills.TierOf(ts, tp);
            ui.Tooltip(tr.Center.X, tr.Y - ui.U(2), $"{tp.Name.ToUpperInvariant()} . {(tier == 4 ? "CAPSTONE" : "TIER " + Style.Numerals[tier])}", lines.ToArray());
        }
    }

    /// The aura (docs/KNIGHT_ORDERS.md 5): tier, XP to the next, the wall and what the tier gives. Returns the next y.
    int AuraBlock(Rectangle c, int y)
    {
        var cap = C.Captain;
        ui.Divider(c.X, y, c.Width, Kit.Gilt * 0.6f); y += ui.U(12);
        ui.Text("AURA", c.X, y, Style.Dim, 1);
        ui.TextR(Campaign.AuraName(cap.AuraTier).ToUpperInvariant(), c.Right, y, cap.AuraTier > 0 ? Style.Aura : Style.Dim, 1); y += ui.U(11);
        float need = C.AuraNeed;
        ui.Pips(new Rectangle(c.X, y, c.Width, ui.U(7)), need <= 0 ? 1 : cap.AuraXp / need, cap.AuraWall ? Style.Warn : Style.Aura, 10); y += ui.U(12);
        string line = cap.AuraWall ? $"A WALL. A TRIAL WILL BREAK IT: {Campaign.TrialsFor(cap.AuraTier)}." : Campaign.AuraGifts[Math.Clamp(cap.AuraTier, 0, Campaign.AuraTop)].ToUpperInvariant();
        if (cap.Wounds.Contains("core")) line = "YOUR CORE IS CRACKED: YOU FIGHT A TIER LOWER. " + line;
        foreach (var l in ui.Wrap(line, c.Width)) { ui.Text(l, c.X, y, cap.AuraWall ? Style.Warn : Style.Dim * 0.8f, 1); y += ui.U(11); }
        return FormList(c, FlameBlock(c, y));
    }

    /// The Black Flame (docs/KNIGHT_ORDERS.md 7, K7): corruption, and the buttons to burn through a wall or purge it. Returns the next y.
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
            ui.TextFit($"{f.Name.ToUpperInvariant()}{(rank > 1 ? $" {Style.Numerals[rank]}" : "")}", c.X + ui.U(14), y, c.Width - ui.U(40), on ? Style.Aura : Style.Dim, 1);
            ui.TextR($"{f.Aura:0}", c.Right, y, Style.Dim, 1);
            if (hover) ui.Tooltip(row.Center.X, row.Y - ui.U(2), f.Name.ToUpperInvariant(), ui.Wrap(f.Line.ToUpperInvariant() + (on ? "" : " CLICK TO PUT IT IN THE FIRST SLOT."), ui.U(180)).ToArray());
            if (ui.Clicked(row)) note = C.SlotForm(id) ?? "";
            y += ui.U(12);
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
                     ("clock", "DAYS IN THE FIELD", $"{C.Day}"), ("flag", "MOST BATTLES", $"{(comps.Count == 0 ? 0 : comps.Max(k => k.Battles))}"),
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
